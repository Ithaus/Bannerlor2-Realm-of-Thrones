# BADANIE B - wiara, zajecia, historia wiosek (dane do pkt 4, 5 i 6 decyzji Jeffa 08.10)

Stan: tylko dane, skrypty i ten raport w `SCR\dzien-6\wioski-lore`. Repo, worktree i gra: tylko odczyt. Nic nie wgrane, nic nie
zacommitowane. Teksty w plikach danych po angielsku (gra), raport po polsku, komentarze w skryptach bez polskich znakow.

SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad`

Znaczniki w kolumnie `source`: nazwa strony AWOIAF = fakt z lore; **[STYL]** = szczegol dopisany w stylu krainy (lore nie opisuje
tej wioski wprost, ale nic mu nie przeczy); **[WYM]** = wymyslone w duchu lore (scenka wiejska bez nazw i dat).

---

## 0. W skrocie

1. **Kazda wioska dostaje 3 gotowe zdania** (po angielsku, do dymka po Alt i do "Hear more" w menu okregu):
   czym zyje (pkt 4), wiara i swiete miejsce (pkt 5), jedno zdanie historii (pkt 6). Pokrycie **2449 / 2449** (plik wgrany) i
   **2446 / 2446** (proba v4), **0 niewypelnionych wstawek**, tylko ASCII, zero slowa "hamlet".
2. **Wiara wedlug krainy z wyjatkami per siedziba i rzeka** (51 wierszy mapy, 30 kodow wiary, 96 zdan). Polnoc = starzy bogowie,
   ale okregi White Harbor i Ramsgate (Manderly) = Siedmiu; Raventree Hall (Blackwood) = starzy bogowie w Dorzeczu; Dorzecze i
   Dolina = Siedmiu, 10-12% wiosek ze starym czardrzewem; Zelazne Wyspy = Utopiony Bog; Dorne = Siedmiu, nad Greenblood - Matka
   Rhoyne; Lhazosh i Hesh = Wielki Pasterz; Essos - kazde Wolne Miasto ze swoja wiara (Czarny Koziol, kaplani brodaci, Ksiezycowe
   Spiewaczki, Trios, Placzaca Pani z Lys, R'hllor wsrod niewolnikow Volantis ...).
3. **Zajecie wedlug rodzaju obrazka v4 (mill / windmill / farm / granary / fishing / village) + typu wsi gry + grupy krain +
   miejsca** (69 zdan glownych, 19 zdan miejsca). Kto pracuje w polu zgodnie z lore: thralle na Wyspach, "bond servants" w Pentos,
   niewolnicy w Volantis / Lys / Myr / Tyrosh / Zatoce Niewolniczej.
4. **472 szablony historii** w 30 krainach: na kraine **15-30 ogolnych** + do 11 szablonow konkretnych siedzib (Seagard, Harrenhal,
   Tumbleton, Hellholt, Chroyane ...). Pula na jedna wioske po filtrach miejsca, siedziby i rzeki: **8-20 (mediana 13)**.
   Chronologia do 298 AC wlacznie; **nic z Wojny Pieciu Krolow** (dzieje sie w grze, ROT startuje w 299 AC).
5. **Deterministycznie**: FNV-1a z `uid` wioski + sol; w okregu szablon historii sie nie powtarza (wymuszone powtorki: 36 z 2449,
   tylko w okregach z wieksza liczba wiosek niz pula).
6. **Gotowy plik dla moda w kontrakcie TECHNIKA.md rozdz. 5**: `arm_map_village_lore-B.tsv` (`#uid livelihood faith history` +
   `# layout_crc32`), do zlaczenia z kolumnami badania A po `uid`. Mod tylko czyta zdania - zero losowania i zero kosztu w grze,
   nic do sejwu.
7. **20 przykladowych dymkow** z 20 krain: rozdz. 8 i `dymki-przyklady.txt` (plik 2449) oraz `v4-proba\dymki-przyklady.txt` (v4,
   prawdziwe rodzaje obrazkow).

---

## 1. Pliki

| Plik | Co | Wiersze |
|---|---|---|
| `wiara.csv` | `table=map`: klucz (`region:` / `seat:` / `river:<rzeka>@<kraina>`) -> wiara z waga; `table=line`: zdania wiary wedlug wiary, klasy miejsca (H / R / F / *) i krainy | 51 + 96 |
| `zajecia.csv` | `crops`, `farmers`, `fish` (slowniki grup), `main` (rodzaj v4 / typ wsi / grupa -> zdanie), `place` (drugie zdanie od miejsca), `group` (kraina -> grupa) | 119 |
| `historia.csv` | `id, region, place, needs, seats, era, text, source` | 472 |
| `dymki-przyklady.txt` | 20 dymkow (plik 2449) | 20 |
| `wioski-lore-przypis.tsv` | kazda wioska: kraina, miejsce, klasa, typ wsi, rodzaj, rzeka, kod wiary i klucz, 3 zdania, id i wielkosc puli historii (do przegladu okiem) | 2449 |
| `arm_map_village_lore-B.tsv` | **kontrakt dla moda** (TECHNIKA.md rozdz. 5): `#uid`, `livelihood`, `faith`, `history`; naglowek `# layout_crc32: 86c412f5` | 2449 |
| `v4-proba\*` | to samo na pliku v4 w toku (`wioski-v4\gen\arm_map_villages.tsv`, crc32 `2594a95d`, kolumna `model`) | 2446 |
| `b-skrypty\b_dane.py` | zlaczenie pliku ukladu z settlements.xml i spclans.xml, rzeka z nazwa (a-teren `rzeki.json`, do 6 jedn.), crc32 | |
| `b-skrypty\b_wiara_zajecia.py` | dane i zapis `wiara.csv`, `zajecia.csv` (+ kontrola sum wag) | |
| `b-skrypty\b_historia.py` | dane i zapis `historia.csv` (+ kontrola liczby na kraine, dlugosci, ASCII) | |
| `b-skrypty\b_dymki.py` | wybor zdan, kontrola na calej mapie, przyklady, kontrakt TSV | |

Format CSV: RFC 4180 (przecinek, cudzyslow tylko gdy pole ma przecinek), UTF-8 bez BOM, `\n`. W polach nie ma cudzyslowow,
tabulatorow ani nowych linii. Pole `region`, `seats`, `group` z wieloma wartosciami - oddzielone `|`.

Przebieg (offline, ok. 5 s): `python b_dane.py [plik_ukladu.tsv] [tag]` -> `python b_wiara_zajecia.py` -> `python b_historia.py` ->
`python b_dymki.py [tag]`. Bez argumentow = plik wgrany u Jeffa; z tagiem wyniki ida do `wioski-lore\<tag>\`.

---

## 2. Zrodla i metoda

- **AWOIAF** (awoiaf.westeros.org): WebFetch i curl dostaja 403 / strone Cloudflare, archiwum tez zablokowane - fakty zebrane
  przez WebSearch ograniczony do stron AWOIAF (wyniki = streszczenia tych stron z adresami). Ok. 60 zapytan: wiara (starzy bogowie,
  Siedmiu, Utopiony Bog, R'hllor, Wieloboski, Czarny Koziol, kaplani brodaci, Wielki Pasterz, bog-kon, Gracje Ghis, Trios, Placzaca
  Pani, Boash, Matka Rhoyne, Wyspy Letnie, Yi Ti, Sarnor, Ibben, Qarth) i historia (rozdz. 6). Lista stron: rozdz. 10.
  Badanie A czytalo pelne strony przez wbudowana przegladarke i API MediaWiki - fakty B nie przecza danym A.
- **ROT (tylko odczyt)**: `Modules\ROT-Map\ModuleData\settlements.xml` (kultura i `village_type` wsi gry, `bound`, wlasciciel),
  `Modules\ROT-Content\ModuleData\spclans.xml` (nazwy klanow - tylko do podgladu linii 2 dymka).
- **Kraina** z kultury wsi gry (ta sama tablica co generator nazw: `c-nazwy\korpus.py CULTURE_REGION`, 30 krain).
- **Rzeka z nazwa**: najblizsza sciezka rzeki ze sceny (`a-teren\rzeki.json`) do 6 jedn. (wioski nad rzeka stoja 1.5-5.2 jedn. od
  sciezki). 25 rzek z nazwa z lore (the Trident, the Red Fork, the Mander, the Greenblood, the Rhoyne, the Skahazadhan = rzeka
  Meereen ...); sciezki bez nazwy w lore (vale1, haystack, braavos ...) = "the river". Rzeke z nazwa ma 531 z 2449 wiosek.
- Cytaty: zadnego doslownego cytatu z ksiazek w danych ani w raporcie (same parafrazy).

---

## 3. Jak wybierane sa zdania (deterministycznie)

Hash = **FNV-1a 32 bit** po bajtach UTF-8 napisu `"<uid>|<sol>"` (offset 0x811C9DC5, mnoznik 0x01000193). Ten sam `uid` = te same
zdania przy kazdym przebiegu. `uid` zmienia sie, gdy generator przestawi wioske (inne polozenie) - wtedy plik B liczy sie od nowa
(5 s), a mod i tak wylacza lore przy innym `layout_crc32` (TECHNIKA.md rozdz. 5).

**Wiara (sol `faith`, `faithline`)**
1. Klucz: najpierw `river:<rzeka>@<kraina>` (np. Greenblood w Dorne), potem `seat:<siedziba pana okregu>`, potem `region:<kraina>`.
2. Wiara z wag klucza (suma 100): `H % 100` wzdluz skumulowanych wag (np. Dorzecze: Siedmiu 88, Siedmiu z czardrzewem 12).
3. Zdanie: wiersze `line` tej wiary z `cls` = klasa wioski (H wezel / R przy linii / F ubocze) albo `*`, i z kraina pasujaca do
   `region` (puste = kazda); wybor `H % liczba`. Wstawki: `{DISTRICT}` (wies gry - tam jest sept / swiatynia okregu), `{SEAT}`.

**Zajecie (sol `work`, `workplace`)**
1. Rodzaj obrazka = kolumna `model` pliku v4 (mill / windmill / farm / granary / fishing / village). Plik 2449 nie ma rodzaju -
   do przykladow przyblizenie `kind_guess` (rybacy nad woda = fishing, zboze nad rzeka z nazwa = mill, wezel na skrzyzowaniu = granary,
   ubocze = farm, reszta village); w modzie zbedne.
2. `farm` przy typie hodowlanym (bydlo, owce, swinie, konie) = zdanie typu wsi (hodowcy), nie "crofters".
3. Wiersz `main` o najwyzszej szczegolowosci: rodzaj 4 pkt + typ wsi 2 pkt + grupa krain 1 pkt (`*` = kazdy); przy remisie wariant
   z hasha. Wstawki: `{CROPS}` i `{FARMERS}` grupy, `{FISH}` (morze / rzeka / jezioro + grupa), `{SEAT}`, `{RIVER_OR}` (rzeka z nazwa
   albo "the river").
4. Drugie zdanie od miejsca (`place`: most, brod, skrzyzowanie, droga, rzeka, brzeg, jezioro) tylko po jednozdaniowym zajeciu, nie
   przy rybakach nad woda i tylko gdy calosc <= 125 znakow.

Grupy krain: N = Polnoc, Dar, Za Murem, Skagos; W = Dorzecze, Dolina, Zachod, Korona, Ziemie Burzy, Smocza Skala; R = Reach;
D = Dorne; I = Zelazne Wyspy; E1 = Braavos, Lorath, Ibben (wolni); E2 = Pentos, Norvos, Qohor ("bond servants" Pentos jako wzorzec
"Farmers and bond servants"); E3 = Myr, Lys, Tyrosh, Volantis, Valyria (niewolnicy 3:1 i 5:1); G = Zatoka Niewolnicza;
X = Qarth, Dothrakowie, Sarnor, Yi Ti, Wyspy Letnie.

**Historia (sol `hist`)**
1. Pula wioski = szablony, w ktorych: kraina jest na liscie `region`; `place` = `any` albo miejsce wioski (most -> most + rzeka,
   brod -> brod + rzeka, skrzyzowanie -> skrzyzowanie + droga, ujscie / widly -> rzeka, zatoka / przystan -> brzeg); `seats` puste
   albo zawiera siedzibe pana okregu; `needs` puste, `RIVER` (wioska ma rzeke z nazwa) albo `RIVER=the Trident` (ta rzeka).
2. Wioski okregu po kolei wedlug `order` (kolejnosc palenia z pliku), potem `uid`; start `H % n`, krok +1, az do szablonu jeszcze
   nieuzytego w tym okregu (gdy wszystkie uzyte - powtorka).
3. Wstawki: `{VILLAGE}`, `{DISTRICT}`, `{SEAT}`, `{RIVER}`; nazwa z "The " w srodku zdania -> "the" ("the Twins", "the Inn of the
   Kneeling Man"); pierwsza litera zdania wielka.
4. **Historia nie uzywa obecnego pana** (zmienia sie w grze) - tylko nazwy miejsc i fakty lore. Zdanie "Lord Fell rode to Summerhall"
   jest przypiete do okregu Fellwood, nie do rodu, ktory go dzis trzyma.

Rekomendacja: wybor robi Python przy generowaniu danych (jak dzis nazwy wiosek), mod czyta gotowe zdania z
`arm_map_village_lore.tsv` (zlaczenie kolumn A + B po `uid`). W C# nie trzeba ani hasha, ani tabel, ani parsera CSV.
Gdyby kiedys trzeba bylo liczyc w grze, FNV-1a w C#:
```csharp
// FNV-1a 32 bit over UTF-8 bytes of uid + "|" + salt (same as b_dymki.py)
static uint Fnv1a(string s) { uint h = 2166136261u; foreach (byte b in System.Text.Encoding.UTF8.GetBytes(s)) { h ^= b; h *= 16777619u; } return h; }
```

---

## 4. Wiara (pkt 5)

| Kraina | Wiara (udzial) | Jak wyglada swiete miejsce (przyklady zdan) | Zrodlo |
|---|---|---|---|
| Polnoc | starzy bogowie 100% | czardrzewo z twarza na bloni; modla sie w lesie nad polami; slub przed drzewem serca; "no priests and no temple" | Old_gods (bez kaplanow, godswoody tez we wsiach), Heart_tree, Weirwood (czerwony sok) |
| - White Harbor / Ramsgate | Siedmiu 75 / 60%, starzy bogowie reszta | sept z bialego kamienia; "brought north by the Manderlys" | White_Harbor, Sept_of_the_Snows, House_Manderly (Shield of the Faith) |
| Dar (Nocna Straz) | starzy bogowie 70%, Siedmiu 30% | czardrzewo i drewniany sept na jednej bloni | Castle_Black, New_Gift [STYL] |
| Za Murem | starzy bogowie | weirwoody Nawiedzonego Lasu; pala zmarlych | Free_folk |
| Skagos | starzy bogowie | plotki o krwi dla czardrzew | Skagosi |
| Dorzecze | Siedmiu 88%, Siedmiu + stare czardrzewo 12% | sept siedmioboczny z dzwonem (H); drewniany sept, septon z wsi gry w swieta (R); wedrowny septon dwa razy w roku (F); septria brazowych braci z ulami i mlynem (H) | Faith_of_the_Seven (septon dwa razy w roku), Septry (ASOS 39), Holy_brother |
| - Raventree Hall | starzy bogowie 60% | "Ravens roost in the village weirwood, as they do at Raventree" | Raventree_Hall, House_Blackwood |
| - Acorn Hall, Harrenhal | Siedmiu z czardrzewem 40 / 25% | High Heart, belki z czardrzew Harrenhalu | High_Heart, Harrenhal |
| Dolina | Siedmiu 90%, z czardrzewem 10% | jak wyzej | Faith_of_the_Seven |
| Zachod, Reach, Korona | Siedmiu 100% | jak wyzej | Faith_of_the_Seven |
| Ziemie Burzy | Siedmiu 95%, z czardrzewem 5% | jak wyzej | [STYL] |
| Smocza Skala i Zatoka | Siedmiu 85%, "czerwony bog" 15% | "Lately men from Dragonstone speak of a red god, and the septon frowns" | R'hllor, Melisandre na Smoczej Skale (298-299 AC) |
| - Dyre Den (Crackclaw) | Siedmiu z czardrzewem 30% | krew Pierwszych Ludzi | Crackclaw_Point [STYL] |
| Zelazne Wyspy | Utopiony Bog | bez swiatyn i idoli; topieni chrzcza w przyboju; kaplan z maczuga z drewna wyrzuconego przez morze dostaje strawe i dach | Drowned_God, Priest_of_the_Drowned_God |
| Dorne | Siedmiu (dornijskie) | kopulasty sept z piaskowca przy studni; sept z cegly; septon z karawanami; corki dziedzicza jak synowie | Dorne [STYL], rownosc dziedziczenia (Rhoynar) |
| - nad Greenblood | Matka Rhoyne 60% | "Orphans of the Greenblood moor here and sing to Mother Rhoyne" | Mother_Rhoyne, Orphans_of_the_Greenblood |
| Braavos | wielu bogow | kapliczka Ksiezycowych Spiewaczek (wyprowadzily zalozycieli do laguny); Ojciec Wod; nocny ogien kaplanow R'hllora | Temple_of_the_Moonsingers, Isle_of_the_Gods |
| Pentos | wielu bogow | kapliczka polna - "w zly rok zaplaci Ksiaze" (ofiara z Ksiecia przy glodzie i przegranej wojnie); nocny ogien | Prince_of_Pentos, Red_temple |
| Norvos | bog brodatych kaplanow (imie znaja tylko wtajemniczeni) | dzwon godzin modlitwy; kaplan we wlosiennicy; tylko kaplani nosza brode | Bearded_priests, Norvos (trzy dzwony) |
| Qohor | Czarny Koziol | oltarz, krew kazdego dnia; w swieta skazancy | Black_Goat |
| Lorath | wielu bogow | kamienne labirynty Mazemakers; kult Slepego Boga dawno wymarly | Mazemakers, Boash |
| Myr | bez jednej wiary | kapliczki przy drodze, czerwony kaplan w dni targowe; Thoros oddany swiatyni jako dziecko | Myr, Thoros_of_Myr |
| Lys | wielu bogow | kapliczka Placzacej Pani z Lys (ulubiona starych kobiet); bogini milosci z monet | Weeping_Lady_of_Lys |
| Tyrosh | wielu bogow | kapliczka trojglowego Triosa (pierwsza glowa pozera umierajacych, trzecia oddaje) | Trios |
| Volantis | wielu bogow, R'hllor wsrod niewolnikow | najwieksza swiatynia R'hllora na swiecie; kaplani to niewolnicy swiatyni | Temple_of_the_Lord_of_Light |
| Valyria | wielu bogow | "Men here fear the Fourteen Flames more than any of them" | Fourteen_Flames |
| Zatoka Niewolnicza | bogowie Ghis (Gracje w swiatyniach miast) | harpia nad drzwiami kapliczki, bozki domowe panow | Ghiscari, Temple_of_the_Graces |
| - Lhazosh, Hesh | Wielki Pasterz | swiatynia z gliny z kopula, owcze skory na polepie, oltarz z niebieskimi zylkami, "godswife" | Great_Shepherd |
| Qarth | wielu dziwnych bogow | swiatynie tlocza sie na nabrzezu Qarthu, we wsi kapliczka | Qartheen |
| Dothrakowie | bog-kon (w ksiazkach "horse god", nie serialowy "Great Stallion") | Lono Swiata, Matka Gor | Great_Stallion, Womb_of_the_World |
| Sarnor | sto bogow Wysokich Ludzi (dzis tylko w Saath) | | Sarnori |
| Ibben | bogowie Ibbenu (kaplani w Tysiacu) | | Ibben [STYL] - lore nie podaje imion |
| Yi Ti | Lew Nocy i Panna ze Swiatla | | Lion_of_Night, Maiden-Made-of-Light |
| Wyspy Letnie | bog i bogini milosci, piekna i plodnosci | swiatynia milosci | Summer_Isles |

Na mapie 2449: Siedmiu 891, starzy bogowie 248, Norvos 149, Pentos 128, Volantis 127, Ghis 113, Myr 109, Dorne 106, Qohor 100,
Braavos 96, Tyrosh 67, Lorath 60, Siedmiu z czardrzewem 59, Lys 56, Wielki Pasterz 38, Siedmiu na Polnocy 28, Ibben 18, Raventree 15,
Matka Rhoyne 13, reszta pojedyncze. Dla klasy R (84% wiosek) kazda duza wiara ma 4-8 zdan, wiec jedno zdanie trafia najwyzej
ok. 170 wiosek na calej mapie (Siedmiu, 891 wiosek).

---

## 5. Zajecia (pkt 4)

- **Rodzaj obrazka v4** (kolumna `model`, proba v4: village 1053, farm 446, fishing 400, mill 358, windmill 98, granary 91):
  mill "Farmers and millers: {CROPS}. The mill wheel turns on {RIVER_OR}."; windmill "... A windmill on the rise grinds for the whole
  district."; granary "{FARMERS} and carters: {CROPS}. The great barn here holds the grain of the district." (w krainach niewolnikow:
  "The masters' granary ..."); fishing "Fishers and net-makers: {FISH}." (Wyspy: "seven families in ten", wg archmaestra Haerega);
  farm "Crofters and herdsmen: {CROPS}, a few cows and pigs." (niewolnicy: "Slaves on a master's estate ...").
- **Typ wsi gry** (23 typy w settlements.xml): zboze, rybacy, drwale, srebro, winnice, zelazo, glina, len, oliwki, traperzy, sol,
  bydlo, owce, swinie, daktyle, "silk", wieloryby, 7 rodzajow hodowli koni. Odmiany krain z lore: drwale Braavosu tna sosny dla
  Arsenalu, drwale Qohoru - wielki las, kopacze rudy Qohoru dla kowali Qohoru, thralle w kopalniach Wysp, ceglarze Zatoki (czerwona
  cegla Astaporu, zolta Yunkai), len Myr dla koronczarek, winnice Dorne = "Dornish red", hodowcy Dorne = "sand steeds", wielorybnicy
  Ibbenu (lampy na olej wielorybi).
- **Plony grupy**: N "barley, oats and turnips", W "wheat, barley and oats", R "... and apples", D "on watered land",
  I "on thin, stony soil", E1 "barley, oats and cabbages", E2 "... and beans", E3 "... and figs", G "on irrigated land".
- **Rozjazdy ROT z lore, poprawione tekstem**: 9 "winnic" na Polnocy -> "Brewers and beekeepers: barley ale and heather mead";
  "silk_plant" w Westeros -> "Dyers and weavers of fine cloth" (jedwab tylko Yi Ti).
- Drugie zdanie od miejsca: most "Bridge tolls, and a smithy for passing riders." (Essos: "Bridge tolls for {SEAT}, paid in coin or
  in kind."), skrzyzowanie "An inn at the crossroads, and a market on feast days.", droga "An alehouse on the road ...", rzeka
  "Ferrymen and eel traps on the Red Fork.", brzeg "Boats drawn up on the shingle ...", Dorne / poludnie Essos "A well and a stable for
  the caravans on the road." - po 2-4 warianty.

---

## 6. Historia (pkt 6)

### 6.1 Zasady
- Jedno zdanie, 60-125 znakow, najczesciej "co sie tu stalo" + "co z tego zostalo we wsi". Bez nazwisk zywych postaci w roli
  dzisiejszego pana, bez wydarzen od 299 AC.
- **Miejsce** (most, brod, skrzyzowanie, droga, rzeka, brzeg, jezioro, ubocze) w 118 szablonach, 354 to "any". **Siedziba**:
  91 szablonow tylko dla okregow danej siedziby (bitwy, rody, ruiny). **Rzeka**: 19 szablonow wymaga rzeki z nazwa (rubiny Rhaegara
  tylko nad Tridentem, Sieroty tylko nad Greenblood, Noyne pod Norvos, Skahazadhan pod Meereen ...).
- Pochodzenie: 314 lore (strona AWOIAF), 97 [STYL], 61 [WYM]. 118 szablonow ma date AC/BC.
- Liczba ogolnych (bez siedzib) na kraine: Polnoc 19, Dar 15, Za Murem 15, Skagos 15, Dorzecze 30, Dolina 30, Zachod 28, Reach 30,
  Ziemie Burzy 28, Korona 28, Smocza Skala 25, Wyspy 16, Dorne 17, Braavos 20, Pentos 20, Norvos 19, Qohor 18, Lorath 17, Myr 21,
  Tyrosh 24, Lys 24, Volantis 20, Valyria 16, Ghis 16, Qarth 16, Dothrakowie 15, Sarnor 15, Ibben 16, Yi Ti 15, Wyspy Letnie 16.
  Dar, Za Murem i Skagos nie maja dzis zadnej wioski na mapie (2449 i v4) - szablony gotowe dla wsi gry (menu okregu, pkt 8).

### 6.2 Os czasu uzyta w szablonach (daty wedlug AWOIAF)
| Kiedy | Co | Krainy / siedziby |
|---|---|---|
| Era Switu / Bohaterow | Pierwsi Ludzie, ringforty, Krolowie Zimy, Krolowie Kurhanow, Czerwoni Krolowie, Garth Greenhand, Lann Sprytny, Szary Krol, Durran / Bran Budowniczy, Joramun, Bael Bard, Gendel i Gorne | Polnoc, Reach, Zachod, Wyspy, Ziemie Burzy, Za Murem |
| 2000-6000 lat temu (sporne) | Andalowie: Palce Doliny, Bitwa Siedmiu Gwiazd, Weeping Water (Theon Stark i Boltonowie), High Heart (Erreg), malzenstwa w Reach i z Lannisterami, Crackclaw sie nie dal | Dolina, Dorzecze, Reach, Zachod, Polnoc, Smocza Skala |
| ok. 4700 BC | Valyria niszczy Stare Ghis, sola pola | Ghis |
| ok. 1000 BC | Manderly wygnani z Reach do White Harbor | White Harbor, Ramsgate |
| ok. 700 BC | Wojny Rhoynarow (Garin, Sar Mell, Chroyane, Ny Sar, Ar Noy, Ghoyan Drohe), 10 tys. statkow Nymerii, ujscie Greenblood | Volantis, Myr, Norvos, Qohor, Pentos, Dorne |
| przed Podbojem | Arlan III bierze Dorzecze; Harwyn Hardhand pod Fairmarket; Harren buduje Harrenhal 40 lat rekami jencow, tnie czardrzewa | Ziemie Burzy, Dorzecze, Wyspy |
| ok. 400 lat temu (Doom 102 BC wg AWOIAF, inne zrodla 114 BC - w tekstach "four hundred years ago") | Zaglada Valyrii, Stulecie Krwi: tygrysy Volantis, flota w Dymiacym Morzu, Trzy Tysiace Qohoru przeciw Khal Temmo, Pole Wron i upadek Sarnoru | Valyria, Volantis, Lys, Myr, Qohor, Sarnor, Dothrakowie |
| 2-1 BC | Ladowanie Aegona, Rosby sie poddaje, Ostatnia Burza pod Bronzegate, Pole Ognia (spotkanie w Goldengrove), Edmyn Tully, spalenie Harrenhalu, Torrhen kleka, Vickon Greyjoy | Korona, Ziemie Burzy, Reach, Zachod, Dorzecze, Polnoc, Wyspy |
| 4-13 AC | I wojna dornijska, Rhaenys ginie pod Hellholt (10 AC), Fowler pali Nightsong | Dorne, Ziemie Burzy |
| 58, 62 AC | Nowy Dar (Alysanne), drogi Starego Krola (krolewski trakt od 62 AC) | Dar, Westeros |
| 96-97 AC | Krolestwo Trzech Corek; Kamienne Wrony zabijaja ojca i braci Jeyne Arryn | Myr, Lys, Tyrosh, Dolina |
| 129-131 AC | Taniec Smokow: Plonacy Mlyn (Bracken / Blackwood), Rook's Rest, Honeywine, Tumbleton, lupiez Lannisportu (Czerwony Kraken), Spicetown (Triarchia), Dragonpit; Dolina za Rhaenyra, Borros Baratheon za Aegonem II | Dorzecze, Smocza Skala, Reach, Zachod, Korona, Dolina, Ziemie Burzy |
| 131-134 AC | Wojna Corek - Myr traci najwiecej | Myr, Lys, Tyrosh |
| 157-161, 187 AC | Mlody Smok podbija i traci Dorne; Dorne w krolestwie przez slub | Dorne |
| 196-212 AC | Redgrass Field (196), bunt Skagos za Daerona II, Wielka Wiosenna Zaraza 209 (Dolina i Dorne zamknely drogi), Pentos: 4 ksiazeta poswieceni w roku wojny 209, Zlota Kompania (212) | Dorzecze, Skagos, Korona, Reach, Zachod, Dolina, Pentos, Sporne Ziemie |
| 226-281 AC | Long Lake (226), Smiejacy sie Sztorm (237+), Summerhall (259), Reyne-Tarbeck (261), Duskendale (277), Bractwo Krolewskiego Lasu (281) | Polnoc, Dar, Ziemie Burzy, Zachod, Korona |
| 282-284 AC | Rebelia Roberta: Gulltown, trzy bitwy pod Summerhall (smierc lorda Fella), Ashford, Dzwony, Trident (rubiny, Lewyn Martell), lupiez King's Landing, oblezenie Storm's End (Davos i cebula), Smocza Skala dla Stannisa | wszystkie krainy Westeros |
| 289 AC | Rebelia Greyjoya: plonie flota Lannisterow, Seagard, ladowania na wyspach, Ned pod Pyke | Zachod, Dorzecze, Wyspy, Polnoc |
| 298 AC | khalasar Drogo pod Pentos | Pentos |
| **od 299 AC - pominiete** | Wojna Pieciu Krolow (np. najazd na Stony Shore 299), Mance Rayder, Daenerys w Zatoce | - |

---

## 7. Kontrola (na pliku 2449; v4 - te same liczby z dokladnoscia do kilku wiosek)

- Pokrycie 2449 / 2449, niewypelnione wstawki 0, znaki spoza ASCII 0, "hamlet" 0, cudzyslow, tabulator i nowa linia w polach 0.
- Dlugosc: wiara <= 110, zajecie <= 125, historia <= 125 znakow (dymek po Alt zawija wiersz - TECHNIKA.md).
- Pula historii na wioske: min 8, mediana 13, max 20; wymuszone powtorki w okregu 36 (1.5%); uzytych szablonow 288 z 472 (reszta
  czeka na miejsca i siedziby, ktorych dzis brak, np. brody, jeziora, Dar). Najczesciej uzyty szablon: 71 wiosek (2.9%).
- Okregi z dwiema wioskami o identycznych trzech zdaniach: 4 z 418.
- Wagi wiary: kazdy klucz sumuje sie do 100; kazda wiara ma zdania.

---

## 8. Dwadziescia dymkow (plik 2449, po angielsku jak w grze)

Linia 2 to tylko podglad pkt 1 (badanie A) z wlasciciela zamku w settlements.xml; "souls" = osady x 250 (podglad). Linie 4-6 = B.

```
LOW WEIR                                                        [Dorzecze, rzeka]
A village of the Ruby Ford lands, held by House Tully of Harroway
4,000 souls in 16 settlements
Potters and brickmakers: clay pits by the water. A landing on the Trident for barges and boats.
Faith: the Seven. A small wooden sept; a septon from Ruby Ford comes on holy days.
Its men fought on the Trident in 283 AC, though no one here agrees on which side.

BRACKENFIELD                                                    [Polnoc, droga]
A village of the Silver Rock lands, held by House Whitehill of Highpoint
Silver miners and smelters. A smithy on the road shoes the horses of passing riders.
Faith: the old gods. No priests and no temple, only an old weirwood by the spring.
When the Old King's roads were laid, the cattle track past the village became a road.

SUMMER MILL                                                     [Reach, rzeka, mlyn]
A village of the Whitegrove lands, held by House Tyrell of Highgarden
Farmers and millers: wheat, barley and apples. The mill wheel turns on the Mander.
Faith: the Seven. A roadside shrine bears the seven-pointed star; the sept is in Whitegrove.
Its men spent a year at the siege of Storm's End in Robert's war, and came home with nothing but stories.

DRAGGAZ                                                         [Zatoka Niewolnicza, rzeka, rybacy]
A village of the Vyrios lands, held by House Targaryen of Mantarys
Fishers and net-makers: eels, pike and trout.
Faith: the gods of Ghis. The masters' household gods sit by the door, and the slaves bow as they pass.
Its masters trace their blood to Old Ghis, and wear tokars to prove it.

KEPYR                                                           [Norvos, droga]
A village of the Laraia lands, held by House Kasgos of Ulentor
Vintners and coopers: grapes, casks and a wine press. An alehouse on the road serves carters and riders.
Faith: the nameless god of Norvos. A bearded priest in a hair shirt comes on holy days.
The road past the village is Valyrian, fused stone that has not cracked in a thousand years.

LITTLE COVE                                                     [Dolina, droga]
A village of the Heartville lands, held by House Corbray of Heart's Home
Farmers: wheat, barley and oats. An alehouse on the road serves carters and riders.
Faith: the Seven, though an old weirwood still stands nearby and some leave gifts there.
The first Andal ships landed on the Fingers, and the village says its founders came with them.

GOLDROCK                                                        [Zachod, droga]
A village of the Stablehill lands, held by House Serrett of Silverhill
Horse breeders: destriers for the knights of the west. A smithy on the road shoes the horses of passing riders.
Faith: the Seven. Women pray to the Mother in the sept, men to the Warrior and the Smith.
Gold and silver have come out of these hills since the First Men, and the Lannisters have taken their share since Lann.

TIALO                                                           [Pentos, brzeg, rybacy]
A village of the Alatys lands, held by House Mopatis of Pentos
Fishers and net-makers: crabs, eels and mackerel.
Faith: many gods. The magisters' gods have temples in Pentos; the village makes do with a roadside altar.
The magisters' estates swallowed the village fields long ago; most here work another man's land.

ALIQUO                                                          [Volantis, rzeka]
A village of the Barihal lands, held by House Melane of Sar Mell
Potters and tile-makers, with kilns that smoke day and night. Ferrymen and eel traps on the river.
Faith: many gods. The free keep the gods of their Valyrian fathers; the slaves keep the fire.
The village paid its dues to Valyria before the Doom and pays Sar Mell now; the sum is about the same.

SALTSANDS                                                       [Dorne, brzeg]
A village of the Red Mines lands, held by House Dayne of Starfall
Silver miners and smelters. Salt fish and seaweed are dried along the shore.
Faith: the Seven. The sept is cool and dim inside, and the Mother's statue is veiled against the sun.
The sun is the village's oldest enemy; work stops at noon and begins again at dusk.

TOMSIO                                                          [Myr, brzeg, wezel]
A village of the Neptali lands, held by House Albersan of Odivo
Fishers and net-makers: crabs, octopus and tuna.
Faith: no single faith, as in Myr. Shrines to many gods line the road; a red priest comes on market days.
Sellswords of the free companies winter here between wars; the village feeds them and counts the spoons.

QHOYVESSA                                                       [Qohor, most]
A village of the Rhodari lands, held by House Tendiras of Dorgoil
Farmers and bond servants: wheat, barley and beans. Bridge tolls for Dorgoil, paid in coin or in kind.
Faith: the Black Goat of Qohor, who must have blood every day.
The bridge is Valyrian work, older than the Doom, and no one here knows how it was made.

OAKHOLT                                                         [Ziemie Burzy, skrzyzowanie]
A village of the Amberly lands, held by House Wylde of Rain House
Fishers and net-makers: eels, pike and trout. Carters and drovers meet at the crossroads inn.
Faith: the Seven. A stone sept with seven windows, the pride of the village.
Its men followed Argilac the Arrogant to the Last Storm, where Orys Baratheon slew him.

GREYBRIDGE                                                      [Braavos, most, mlyn]
A village of the Giasos lands, held by House Volentin of Iksa Calo
Free farmers and millers: barley, oats and cabbages. The mill wheel turns on the Rhoyne.
Faith: many gods. A Westerosi sailor's widow keeps a little shrine of the Seven by the road.
A toll-keeper of Iksa Calo sits at the bridge; even beggars pay to cross.

WARIS                                                           [Tyrosh, brzeg]
A village of the Bloodstone lands, held by House Ohoros of Grey Gallows
Date growers and well-keepers. Boats drawn up on the shingle, and salt fish for the markets.
Faith: many gods. A shrine of three-headed Trios stands at the edge of the village.
Tyrosh was a Valyrian military outpost before it was a city, and the village still musters spears for the Archon.

TEKSIO                                                          [Lorath, skrzyzowanie, spichlerz]
A village of the Irksa lands, held by House Phassar of Tolero Aeksio
Free farmers and carters: barley, oats and cabbages. The great barn here holds the grain of the district.
Faith: many gods. Freedmen from every land each brought a god, and none of them quarrels.
Freed and runaway slaves have settled here for centuries, and no one asks a newcomer whose he was.

AQUIBLAB                                                        [Lys, skrzyzowanie, spichlerz]
A village of the Aqui lands, held by House Vaelaro of Aquos Dhaen
Field slaves and overseers: wheat, barley and figs. The masters' granary holds the grain of the district.
Faith: many gods. A red priest and a priestess of love share the village square on feast days.
In the Century of Blood the tigers of Volantis ruled Lys for two generations, and the village paid them.

REEDBY                                                          [Smocza Skala i Zatoka, most]
A village of the Wendwater Bridge lands, held by House Fell of Fellwood
Horse breeders: palfreys and plough horses. A smithy and an alehouse by the bridge.
Faith: the Seven. A seven-sided sept of stone, with its own septon and a bell.
Lord Fell rode to Summerhall in 282 AC and died there by Robert's hand; the village mourned him.

SHILVAK                                                         [Ibben, most]
A village of the Vasso lands, held by House Thuk of Qamayl
Farmers and beekeepers: barley, oats and cabbages. Bridge tolls for Qamayl, paid in coin or in kind.
Faith: the gods of Ibben, whose priests sit among the Thousand.
Ibbenese traders say their islands were old when Valyria was young, and no one here argues.

GREENCROSS                                                      [Korona, skrzyzowanie, spichlerz]
A village of the Mallery lands, held by House Rykker of Duskendale
Farmers and carters: wheat, barley and oats. The great barn here holds the grain of the district.
Faith: the Seven. A stone sept with seven windows, the pride of the village.
Its people have seen Targaryen and Baratheon kings ride past, and paid the same taxes to both.
```
(Linia z liczba dusz pominieta od drugiego przykladu; pelne dymki w `dymki-przyklady.txt`, wersja v4 z prawdziwym rodzajem
obrazka w `v4-proba\dymki-przyklady.txt` - np. "KNIGHT'S MILL ... The mill wheel turns on the Red Fork." przy Stone Hedge.)

---

## 9. Ryzyka i sprawy otwarte

1. **Fakty z WebSearch, nie z pelnych stron** (AWOIAF blokuje WebFetch). Kazdy fakt z data ma strone AWOIAF w `source`; sporne daty
   pisane ostroznie ("four hundred years ago" przy Doom 102 / 114 BC, Andalowie bez liczby lat). Zdania [STYL] / [WYM] (158 z 472)
   to projekt, nie lore - lista w kolumnie `source`, do przejrzenia okiem przed zamrozeniem (jak nazwy wiosek).
2. **`uid` zmienia sie przy kazdym przestawieniu wioski w v4** - plik B trzeba przeliczyc na ostatecznym pliku v4 (5 s) i zlaczyc z A
   po tym samym `uid`. Mod ma bramke `layout_crc32` (TECHNIKA.md). Dzisiejszy glowny plik = uklad wgrany (crc32 86c412f5), zgodny z
   podgladem A (`podglad_rycerze.tsv`, 2449).
3. **Kolumny v4**: `kind` = miejsce, `model` = rodzaj obrazka (sprawdzone w naglowku `wioski-v4\gen\arm_map_villages.tsv`). Skrypt B
   czyta `model`, a gdyby v4 przemianowal kolumny - takze `type` / `place` (TECHNIKA.md rozdz. 5).
4. **Rzeka z nazwa** liczona w B z `rzeki.json` (6 jedn.). Lepiej, zeby generator v4 wpisywal ja w plik ukladu (ta sama sciezka, ktora
   daje nazwy "Mander Mill") - wtedy nazwa wioski i zdania beda o tej samej rzece. Dzis zgodnosc nie jest sprawdzona wioska po wiosce.
5. **Wlasciciele ROT** (np. Astapor = Jorah Mormont, Mantarys = Aegon Targaryen, Harrenhal = Clegane) - sprawa pkt 1 (A). Historia B
   celowo ich nie uzywa.
6. **Nazwy wsi gry z "The"** ("The Inn of the Kneeling Man") - B zmienia na "the" w srodku zdania; linia 1 dymka (A) musi zrobic to samo
   ("A village of the Inn of the Kneeling Man lands").
7. **Smocza Skala, "red god"** (15% wiosek krainy) zaklada, ze w 299 AC dwor Stannisa juz slucha Melisandre - zgodne z ksiazkami
   (ACOK), ale jesli Jeff woli bez tego - waga 0 w `wiara.csv`.
8. **Monotonia**: wspolne szablony ziem Andalow (19) i Essos sa najczestsze (do 71 wiosek jeden szablon na calej mapie, w okregu nigdy
   dwa razy, chyba ze okreg ma wiecej wiosek niz pula). Dopisanie szablonow = nowe wiersze w `b_historia.py`, bez zmian w modzie.
9. **Dorne w 3 odmianach** z generatora nazw (piaski, Czerwone Gory, Rhoynarowie) nie jest rozroznione osobnymi tabelami - Rhoynarow
   lapie filtr rzeki Greenblood i siedziby (Planky Town, Sunspear, Godsgrace ...), przelecze - filtr siedzib (Skyreach, Wyl, Yronwood).
10. Pkt 7 (pamiec wojny) i pkt 8 (menu) - teksty i stan z ksiegi 108-113 robi TECHNIKA / ksiega; B daje tylko zdania stale.

---

## 10. Zrodla (AWOIAF, wszystkie strony: https://awoiaf.westeros.org/index.php/<strona>)

Wiara: Old_gods, Heart_tree, Weirwood, Faith_of_the_Seven (wedrowni septoni, septon dwa razy w roku), Holy_brother, White_Harbor,
Sept_of_the_Snows, Shield_of_the_Faith, House_Manderly, Raventree_Hall, Godswood_of_Raventree_Hall, House_Blackwood, High_Heart,
Drowned_God, Priest_of_the_Drowned_God, Iron_Islands, R'hllor, Red_temple, Red_priest, Temple_of_the_Lord_of_Light, Thoros_of_Myr,
Braavos, Isle_of_the_Gods, Temple_of_the_Moonsingers, Black_Goat, Qohor, Norvos, Bearded_priests, Great_Shepherd, Lhazareen,
Great_Stallion, Womb_of_the_World, Mother_of_Mountains, Godsway, Temple_of_the_Graces, Ghiscari, Trios, Temple_of_Trios,
Lady_of_Spears (tylko Nieskalani - nie uzyta dla wiosek), Weeping_Lady_of_Lys, Lys, Myr, Boash, Blind_God, Mazemakers, Lorath,
Qartheen, Temple_of_Memory, Prince_of_Pentos, Pentos, Mother_Rhoyne, Orphans_of_the_Greenblood, Summer_Isles, Lion_of_Night,
Maiden-Made-of-Light, God-on-Earth, Sarnori, Kingdom_of_Sarnor, Ibben, Port_of_Ibben, God-King_of_Ib, Fourteen_Flames,
Doom_of_Valyria, Skagos, Skagosi.

Historia: Coming_of_the_Andals, Battle_of_the_Seven_Stars, Red_Kings, House_Bolton, Barrow_Kings, House_Stark, Moat_Cailin,
Battle_at_Long_Lake, Raymun_Redbeard, Stony_Shore, New_Gift, Brandon's_Gift, Nightfort, Hardhome, Barthogan_Stark, Harwyn_Hoare,
House_Hoare, Harrenhal, Edmyn_Tully, Arrec_Durrandon, House_Durrandon, Battle_of_the_Burning_Mill, Battle_of_the_Honeywine,
First_Battle_of_Tumbleton, Second_Battle_of_Tumbleton, Sack_of_Lannisport, Dalton_Greyjoy, Spicetown, Battle_of_the_Gullet,
Kingdom_of_the_Three_Daughters, Daughters'_War, Disputed_Lands, Golden_Company, Great_Spring_Sickness,
Battle_of_the_Redgrass_Field, Reyne-Tarbeck_revolt, Tragedy_at_Summerhall, War_of_the_Ninepenny_Kings, Defiance_of_Duskendale,
Kingswood_Brotherhood (281 AC), Lyonel_Baratheon, Robert's_Rebellion, Taking_of_Gulltown, Battles_at_Summerhall,
Siege_of_Storm's_End, Battle_of_the_Trident, Greyjoy's_Rebellion, Storming_of_Seagard, Burning_of_the_Lannister_fleet,
Field_of_Fire, House_Lannister, Westerlands, House_Gardener, King_of_the_Reach, Garth_Greenhand, Shield_Islands, Dornish_Marches,
Prince's_Pass, Red_Mountains, Conquest_of_Dorne, Daeron_I_Targaryen, Wedding_of_Maron_Martell_and_Daenerys_Targaryen,
Ten_thousand_ships, Rhoynish_Wars, Nymeria, Sand_steed, Crackclaw_Point, Aegon's_Landing, Kingsroad, Jaehaerys_I_Targaryen,
Vale_mountain_clans, Stone_Crows, Century_of_Blood, Tigers_(Volantis), Elephants_(Volantis), Three_Thousand_of_Qohor,
Field_of_Crows, Uncloaking_of_Uthero, Ghiscari_wars, Hills_of_Andalos, Tyrosh, Tyroshi_pear_brandy, Qarth, Pureborn, Thirteen,
Swan_ship, Goldenheart.
