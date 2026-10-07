# Audyt: co produkuja wsie, jakie warsztaty stoja w miastach, czy to pasuje do swiata (2026-10-07)

Pytanie Jeffa: "sprawdz, ile czego jest hodowane i produkowane, zeby nie bylo bzdur typu oliwki na Polnocy;
czy warsztaty w miastach sa dobrze dobrane - jesli wsie obok kopia rude, w miescie powinno byc cos, co rude
przerabia, bo taniej; audyt, ktore warsztaty gdzie sa i czy to pasuje do lore - bez oliwek w sniegu".

Tylko odczyt - zadnych zmian w grze ani w repo. Oznaczenia: **(kod)** - z dekompilacji / XML, **(log)** - z logow
testu, **(symulacja)** - z odtworzenia algorytmu gry (2000 losowan na miasto, zweryfikowane z 12 sesjami testu),
**(SZACUNEK)** - moje przyblizenie.

Zrodla:
- `Modules\ROT-Map\ModuleData\settlements.xml` (571 czynnych wsi - jedna jest w komentarzu XML, 97 miast, 130 zamkow),
  `ROT-Content\ModuleData\spcultures.xml` (nazwy kultur), `SandBox\ModuleData\spworkshops.xml`,
  `BannerKings.Redux\ModuleData\workshops.xml`.
- Dekompilacja (scratchpad innych sesji, te same DLL co w `libs`): `TaleWorlds.CampaignSystem` -
  `DefaultVillageTypes`, `VillageType`, `WorkshopsCampaignBehavior`, `DefaultVillageTradeModel`,
  `VillageTradeBoundCampaignBehavior`; BannerKings - `BKVillageTypes`, `PopulationManager.GetProductions`,
  `BKVillageProductionModel`, `EconomyPatches.WorkshopsCampaignBehaviorPatches`; BKROTPatch -
  `BKROTVillageProductionModel`; NavalDLC (zdekompilowany w tym audycie ilspycmd) - `NavalVillageTypes`.
  Typy BK sprawdzone wprost na zainstalowanym `BannerKings.dll`.
- Logi: `Armoury-2026-10-07_12-15-40.log`, `Armoury-2026-10-07_12-37-02.log`, `Armoury-2026-10-07_03-18-52.log`,
  linie "Rynek surowcow" z 12 sesji 05-07.10, `Logs\2026-10-07_12-15-40\ludzie-regiony.csv` (krolestwo, kultura,
  dobrobyt kazdego miasta i zamku), CrashScribe `session-2026-10-07_12-15-37.log`, `session-2026-10-07_03-18-49.log`.
- Kod naszych modow: `CrashScribe\src\Mends.cs` (NorthernFare), `Armoury\src\WorkshopTrade.cs`, `WorkshopLaw.cs`,
  `MineralOnce.cs`, `VillageWoodlot.cs` (kopia robocza `scratchpad\lawa\repo` - w glownym repo jeszcze go nie ma).
- Wczesniejsze audyty: `docs/AUDYT-SUROWCE.md`, `docs/AUDYT-PRODUKCJI-MODY.md`, `docs/PLAN-K13-2026-10-07.md` - nie powtarzam,
  odsylam.

Skrypty i dane posrednie: `scratchpad\audprod\` (sim.py - odtworzenie wyboru warsztatow, fix*.py - symulacja poprawek,
flag.py - klimat wsi, appx.md - tabela miast).

---

## 0. Odpowiedz w skrocie

1. **Oliwek w sniegu nie ma we wsiach** (kod): wszystkie 19 gajow oliwnych leza w Reach, Dorne, Stormlands i na
   poludniu Essos (jeden u Dothrakow, Rhaes Azor - watpliwy). Winnice na Polnocy i przy Murze (4) zamienia juz CrashScribe ("strawa Polnocy"). **Prawdziwa
   "oliwka w sniegu" to bawelna**: z 13 wsi `silk_plant` (bawelna -> aksamit) **10 lezy w strefie zimnej albo
   umiarkowanej** - w tym Ornstead przy Wiezy Cieni (Nocna Straz), Durlston pod Krwawa Brama (gory Doliny), Braavos,
   3 wsie Sarnoru, Norvos, Riverlands, Crownlands, Kingswood. To 80 ze 104 sztuk bawelny bazowej swiata. Do tego winnica
   w Lorath (Gelina) i daktyle na Tarth. Razem **12 bzdur wsi** (+4 juz naprawione) i ok. 37 "watpliwych".
2. **Oliwki w sniegu SA za to w warsztatach** (symulacja): w miastach, ktorych wsie produkuja tylko ryby, futra, konie,
   sol albo olej wielorybi (Mormont Keep, Ibben, Pyke, Sisterton, Driftwood Hall...), gra losuje warsztaty **na slepo, po
   rowno z 19 typow**. Skutek: **w 62% nowych kampanii co najmniej jedna olejarnia staje na Polnocy / Wyspach Zelaznych /
   za Murem / w Lorath-Ibben** (srednio 0.92 na kampanie; z pasem umiarkowanym 1.79 i 85%). Tak samo tlocznia wina bez
   winogron (1.60, 81%) i tkalnia aksamitu bez bawelny (1.61, 81%).
3. **Ruda bez kuzni** (symulacja): 24 miasta maja w swoich wsiach kopalnie zelaza, kuznia (`smithy`) trafia tylko do ok. 13
   (55%). **Ok. 11 miast na kampanie siedzi na rudzie bez kuzni** (najczesciej Castle Black 74%, Riverrun 71%, Lannisport 69%,
   Oldtown 66%, Vaes Diaf 65%, Frostfang's Camp 63%, Storm's End 58%, Fairmarket 56%), a ok. 5.4 kuzni stoi tam, gdzie rudy
   nie ma. W logu (doby 21-40): **ruda pietrzy sie na targach +50-56 ladunkow/dobe** (29 miast z nadwyzka sprzedaje po 3 d),
   **36-40 z 97 miast nie ma rudy wcale** (placa 36.5 d - 12x drozej), a warsztaty zbrojne odpuszczaja ok. **755 cykli
   dziennie z braku rudy** (90% wszystkich brakow).
4. **26% warsztatow notabli (76 z 291) nie ma wsadu z wlasnych wsi** (symulacja); ich typy sa zamrozone od startu
   (bankructw 0 przez 40 dob, log). Martwe w logu: kopalnie BK (`mines`, 5-10 na kampanie) i miodosytnie (0-4) -
   **0% pracy w 3 sesjach**; browary pracuja 12-24% dob, garncarnie 7-14%, zlotnicy 28-40%. Piekarnia stoi w 76-81
   z 97 miast (receptury ciast BK maja w algorytmie wage ok. 8x wieksza niz kazdy inny warsztat).
5. **Surowce posrednie sie nie przerabiaja** (log, doby 21-40): welna +144-178/dobe (zapas 6-7 tys., popyt rzemiosla 0),
   skory surowe +123-127/dobe (4.2-4.4 tys.; garbarni 5-15), len +62-74, plotno +54-59, drewno +392/dobe (12.5-12.8 tys.),
   ruda +50-56. Brak: ruda w 36-40 miastach. Produkcji oliwek, winogron, daktyli, bawelny, wina, oleju **gra nigdzie nie
   loguje** - liczby dla nich w tym raporcie to definicje gry (baza) x liczba wsi, nie pomiar.
6. Znaleziony blad w CrashScribe: "strawa Polnocy" przestawia prase do wina tylko w osadzie `Village.Bound`; Farsfog nalezy
   do zamku Moat Cailin, a handluje z **White Harbor** - tamtejsza tlocznia (30% kampanii) zostaje bez winogron
   (`Mends.cs:2134`, `:2140`). Poza tym poprawka biegnie w `OnSessionLaunched`, czyli **po** rozdaniu warsztatow nowej gry.

Najwazniejsze poprawki (rozdzial 6): **A1** tabela klimatu wsi (12 zmian + 5 kompensujacych bawelne) stosowana PRZED
rozdaniem warsztatow; **A3** bramka "przerobka tylko przy surowcu" w wyborze warsztatu (olejarnie w zimnych strefach
0.92 -> 0, warsztaty bez wsadu 76 -> 24); **B1** "przy kopalni kuznia" (pokrycie rudy 55% -> 100%); **B3** usunac z losowania
martwe kopalnie BK i miodosytnie; **B5** ksiega towarow spozywczych/luksusowych, zeby kolejne pytanie "ile oliwek" mialo
odpowiedz z logu.

---

## 1. Skad sie bierze produkcja wsi (kod)

| Warstwa | Co robi | Gdzie |
|---|---|---|
| ROT | Tylko **przypisuje typ** kazdej wsi (`village_type="VillageType.x"`), hearth, przynaleznosc (`bound`) i kulture. Nie definiuje wlasnych typow ani list produkcji. | `ROT-Map\ModuleData\settlements.xml` |
| Gra (vanilla 1.4.8) | 22 typy z lista produkcji/dobe: kazdy ma zboze 3 (pszeniczna farma 50), a do tego: kopalnia zelaza ruda 10, drwale drewno 18, len 18, owce welna 10 + owce 4 + maslo/ser 2, bydlo krowy 2 + maslo/ser 4, swinie 8, winnica winogrona 11, gaj oliwny oliwki 12, daktyle 8, "silkworm farm" bawelna 8, srebro 3, glina 10, sol 15, rybacy 28, traperzy futra 1.4, rancza koni 3-6 koni (pustynne + wielblady 0.68) | `DefaultVillageTypes.InitializeAll` / `AddProductions` |
| NavalDLC | `whaler` (olej wielorybi 1.8 + ryby 5), `walrus_hunter` (kly morsa 1.4 + ryby 5) | `NavalDLC.Settlements.NavalVillageTypes` |
| BK - lista typu (wspolna dla wszystkich wsi typu) | glina +wapien 8; srebro +marmur 0.8 +zloto 0.2; drwale +miod pitny 2; daktyle +przyprawy 0.5; **pszenica +papirus 0.5**; bydlo +jaja 1.5 | `BKVillageTypes.Initialize` (BannerKings.dll, sprawdzone na zainstalowanym) |
| BK - dodatki per wies | gaj oliwny **+pomarancze 2**; pszenica **+jablka 2 +marchew 2**; drwale i traperzy **+miod 0.5** (+1 za poziom pasieki); wsie rolnicze kury/gesi (aserai 0.15, sturgia/khuzait 0.45, empire/battania 0.75, reszta 1.0 - id vanilla, w ROT: Dorne, Wyspy Zelazne, Dothrakowie, Braavos, Polnoc); mineraly (wsie gornicze, losowane `MineralData`), skora/narzedzia z budynkow, chleb 10% zboza | `PopulationManager.GetProductions` (BK) |
| BK - ilosc | towar "ogolny" (ruda, len, welna, glina, sol, srebro, bawelna): 0.0045 szt. na robotnika x udzial pozycji w liscie; zywnosc z pol (farmland); drewno i futra z lasu; zwierzeta z pastwisk | `BKVillageProductionModel.CalculateDailyProductionAmount` |
| BKROTPatch | tylko mnozniki z blogoslawienstw (Kowal +25%, Bogowie Lata +25%, Starzy Bogowie +50% drwale, Krol Trytonow +50% rybacy) | `BKROTVillageProductionModel` |
| Armoury | ruda i drewno drwali x3 (`MaterialLaw`, `LumberOutputMultiplier 3`), **las wsi: kazda wies nie-drwali +2.5 ladunku drewna/dobe** (`VillageWoodlot`), mineraly bez dubli BK (`MineralOnce`) | `Armoury\src\MaterialLaw.cs`, `VillageWoodlot.cs`, `MineralOnce.cs` |
| CrashScribe | 4 winnice Polnocy -> rybacy/bydlo/swinie, tlocznie w Winterfell i Castle Black -> browary | `Mends.cs:2112-2160` (NorthernFare) |

Ile to realnie daje: gra loguje tylko rude i drewno. **Kalibracja (log 12:15, doby 21-40): 26 kopalni zelaza, baza 260/dobe,
model BK 196 z mnoznikiem x3 = ok. 65 bez x3 = ok. 25% bazy.** Dla innych towarow "ogolnych" przyjmuje (SZACUNEK) 1/4-1/3 bazy
(zgodnie z `AUDYT-SUROWCE.md`: len 5.8 zamiast 18, welna 3.2 zamiast 10 na wies). Zywnosc idzie innym wzorem - bez pomiaru.

---

## 2. Co produkuja wsie - wedlug krain

### 2.1 Typy wsi wedlug kultury (kod; po zmianach CrashScribe w nawiasie)

Kultury ROT: battania = Polnoc, vlandia = Westerlands, sturgia = Wyspy Zelazne, aserai = Dorne, empire = Braavos,
khuzait = Dothrakowie, nord = Lorath; reszta po nazwie (river, vale, reach, stormlands, crownlands, dragonstone,
freefolk, nightswatch, skagosi, ghiscari, qartheen, volantine, lyseni, tyroshi, myrish, pentoshi, norvos, qohorik,
sarnor, ibbenese, yiti, valyrian, summer). Kolejnosc od polnocy.

| Kultura | Wsi | Typy |
|---|---|---|
| freefolk (za Murem) | 18 | zboze 4, traperzy 3, drwale 3, rybacy 2, len 1, srebro 1, swinie 1, ruda 1, bydlo 1, konie 1 |
| skagosi | 4 | rybacy 1, wieloryby 1, zboze 1, morsy 1 |
| nightswatch | 11 | zboze 4, winnice 2 (-> rybacy, swinie), **bawelna 1**, drwale 1, glina 1, ruda 1, owce 1 |
| battania (Polnoc) | 57 | zboze 15, traperzy 7, drwale 7, garrony 6, bydlo 3 (+1), swinie 3, len 3, winnice 2 (-> rybacy, bydlo), owce 2, srebro 2, rybacy 2 (+1), wieloryby 2, ruda 1, glina 1, konie 1 |
| vale | 31 | zboze 7, rybacy 6, konie 7, bydlo 3, owce 2, **bawelna 1**, ruda 1, srebro 1, len 1, traperzy 1, winnica 1 |
| ibbenese | 11 | wieloryby 3, drwale 2, rybacy 2, swinie 2, zboze 2 |
| nord (Lorath) | 12 | zboze 4, traperzy 2, len 2, rybacy 1, drwale 1, glina 1, **winnica 1** |
| empire (Braavos) | 13 | zboze 3, rybacy 2, srebro 2, konie 1, **bawelna 1**, owce 1, glina 1, drwale 1, ruda 1 |
| river | 43 | zboze 14, winnice 6, bydlo 4, konie 4, glina 4, ruda 2, drwale 2, rybacy 2, srebro 2, **bawelna 1**, len 1, swinie 1 |
| sarnor | 13 | zboze 3, **bawelna 3**, rybacy 2, glina 1, len 1, bydlo 1, traperzy 1, konie step 1 |
| sturgia (Wyspy Zelazne) | 28 | zboze 6, rybacy 6, ruda 4, konie 2, traperzy 2, drwale 2, srebro 2, bydlo 1, owce 1, sol 1, len 1 |
| yiti | 3 | traperzy 1, drwale 1, zboze 1 |
| norvos | 12 | zboze 3, glina 2, konie 1, **bawelna 1**, ruda 1, traperzy 1, winnica 1, bydlo 1, len 1 |
| khuzait (Dothrakowie) | 17 | konie step 4, drwale 2, bydlo 2, zboze 2, srebro 1, rybacy 1, ruda 1, sol 1, glina 1, oliwki 1, owce 1 |
| vlandia (Westerlands) | 33 | konie 7, zboze 6, **srebro 6**, rybacy 3, traperzy 3, len 2, ruda 2, drwale 2, glina 1, bydlo 1 |
| dragonstone | 23 | rybacy 5, zboze 4, owce 2, srebro 2, bydlo 2, konie 3, winnica 1, swinie 1, drwale 1, **bawelna 1**, sol 1 |
| pentoshi | 13 | zboze 5, drwale 2, winnice 2, rybacy 1, bydlo 1, len 1, swinie 1 |
| crownlands | 11 | zboze 3, konie 3, sol 2, srebro 1, **bawelna 1**, rybacy 1 |
| qohorik | 13 | drwale 4, winnice 2, konie step 2, rybacy 1, traperzy 1, srebro 1, zboze 1, ruda 1 |
| stormlands | 31 | winnice 5, konie 4, zboze 4, rybacy 4, ruda 3, drwale 3, len 2, **daktyle 2**, oliwki 2, owce 1, sol 1 |
| reach | 40 | zboze 12, oliwki 5, rybacy 4, drwale 3, ruda 3, len 2, owce 2, bydlo 2, winnice 2, konie 3, traperzy 1, swinie 1 |
| ghiscari | 26 | zboze 5, owce 4, ruda 3, konie step 3, konie pust. 2, rybacy 2, daktyle 1, len 1, glina 1, drwale 1, srebro 1, winnica 1, bydlo 1 |
| tyroshi | 12 | daktyle 3, konie pust. 2, zboze 2, winnica 1, owce 1, rybacy 1, sol 1, oliwki 1 |
| myrish | 12 | zboze 3, rybacy 2, konie pust. 2, bydlo 1, drwale 1, oliwki 1, owce 1, bawelna 1 |
| volantine | 18 | zboze 3, konie pust. 3, oliwki 2, rybacy 2, owce 2, sol 1, bawelna 1, drwale 1, glina 1, ruda 1, daktyle 1 |
| qartheen | 11 | konie step 3, **drwale 3**, srebro 3, daktyle 1, owce 1 |
| aserai (Dorne) | 38 | zboze 8, rybacy 7, oliwki 6, konie pust. 4, daktyle 4, srebro 3, sol 3, winnica 1, bawelna 1, drwale 1 |
| lyseni | 12 | daktyle 3, zboze 2, konie pust. 2, sol 1, rybacy 1, srebro 1, oliwki 1, glina 1 |
| valyrian | 2 | owce 1, zboze 1 |
| summer | 3 | srebro 1, rybacy 1, drwale 1 |

Swiat (571): zboze 128, rybacy 62 (64), drwale 45, srebro 30, winnice 28 (24), ruda 26, bydlo 24 (25), owce 23,
traperzy 23, konie europejskie 20, len 19, oliwki 19, glina 16, daktyle 15, konie pustynne 15, bawelna 13, konie stepowe 13,
sol 12, swinie 10 (11), konie vlandyjskie 10, sturgijskie 7, garrony 6, wieloryby 6, morsy 1.

### 2.2 Ile czego - baza gry na dobe wedlug krain (kod; po zmianach CrashScribe)

Liczby to **definicja gry** (suma bazy typow wsi), nie realna produkcja. Realnie BK daje ok. 1/4 bazy dla towarow
"ogolnych" (kalibracja na rudzie, rozdz. 1); ruda i drewno drwali x3 przez Armoury.

| towar (baza/dobe) | Za Murem / Mur / Skagos | Polnoc | Wyspy Zelazne | Westeros srodek (Vale, Riverlands, Crownlands, Dragonstone, Westerlands) | Reach | Stormlands | Dorne | Essos polnoc | Essos poludnie | swiat |
|---|---|---|---|---|---|---|---|---|---|---|
| wsi | 33 | 57 | 28 | 141 | 40 | 31 | 38 | 107 | 96 | 571 |
| zboze | 450 | 750 | 300 | 1700 | 600 | 200 | 400 | 1200 | 800 | 6400 |
| ryby | 122 | 94 | 168 | 476 | 112 | 112 | 196 | 295 | 252 | 1827 |
| drewno (drwale) | 72 | 126 | 36 | 90 | 54 | 54 | 18 | 234 | 126 | 810 |
| ruda | 20 | 10 | 40 | 50 | 30 | 30 | - | 40 | 40 | 260 |
| srebro | 3 | 6 | 6 | 36 | - | - | 9 | 12 | 18 | 90 |
| glina | 10 | 10 | - | 50 | - | - | - | 60 | 30 | 160 |
| sol | - | - | 15 | 45 | - | 15 | 45 | 15 | 45 | 180 |
| len | 18 | 54 | 18 | 72 | 36 | 36 | - | 90 | 18 | 342 |
| welna | 10 | 20 | 10 | 40 | 20 | 10 | - | 20 | 100 | 230 |
| **bawelna** | **8** | - | - | **32** | - | - | 8 | **40** | 16 | 104 |
| futra | 4 | 10 | 3 | 6 | 1 | - | - | 8 | - | 32 |
| **winogrona** | - | - | - | **88** | **22** | 55 | 11 | 66 | 22 | 264 |
| oliwki | - | - | - | - | 60 | 24 | 72 | 12 | 60 | 228 |
| daktyle | - | - | - | - | - | 16 | 32 | - | 72 | 120 |
| pomarancze (BK) | - | - | - | - | 10 | 4 | 12 | 2 | 10 | 38 |
| jablka (BK) | 18 | 30 | 12 | 68 | 24 | 8 | 16 | 48 | 32 | 256 |
| papirus (BK) | 4 | 8 | 3 | 17 | 6 | 2 | 4 | 12 | 8 | 64 |
| miod (BK) | 4 | 7 | 2 | 4 | 2 | 2 | 0.5 | 10 | 4 | 34 |
| krowy | 4 | 11 | 3 | 27 | 6 | 0.8 | 2 | 15 | 7 | 76 |
| owce | 8 | 14 | 6 | 30 | 13 | 6 | 3 | 18 | 46 | 143 |
| swinie | 23 | 36 | 5 | 43 | 18 | 3 | 6 | 43 | 13 | 190 |
| maslo / ser (kazde) | 10 | 26 | 6 | 52 | 14 | 2 | - | 30 | 28 | 168 |
| konie | 6 | 42 | 12 | 141 | 17 | 24 | 12 | 35 | 52 | 342 |
| wielblady | - | - | - | - | - | - | 3 | - | 7 | 10 |
| olej wielorybi | 2 | 4 | - | - | - | - | - | 5 | - | 11 |
| kly morsa | 1 | - | - | - | - | - | - | - | - | 1 |

Co z tego widac:
- **Oliwki, daktyle, pomarancze - tylko poludnie. Dobrze.** Futra tylko na polnocy i w lasach umiarkowanych. Dobrze.
  Morsy tylko Skagos, wieloryby Polnoc / Skagos / Ibben. Dobrze.
- **Bawelna odwrotnie do klimatu**: 80 ze 104 w strefie zimnej/umiarkowanej (Mur, Braavos, Sarnor, Dolina, Riverlands,
  Crownlands, Kingswood). Gorace Essos (Qarth, Zatoka Niewolnicza, Yi Ti) - zero.
- **Wino odwrotnie do lore**: Riverlands (88 w "srodku") produkuja 4x tyle winogron co Reach (22 - tylko 2 winnice na Arbor),
  Dorne ma 1 winnice. W ksiazkach wino to Arbor/Reach, Dorne, Wolne Miasta.
- Polnoc ma **1 kopalnie zelaza** na 57 wsi (Barrowton); Wyspy Zelazne 4 (zgodne z lore - olow, cyna, zelazo).
- Westerlands: 6 kopalni srebra (+zloto BK 0.2/kopalnie) - zgodne z lore Lannisterow.

---

## 3. Bzdury i watpliwosci klimatyczne - wsie (kod)

Strefy: arktyka (freefolk, skagosi, nightswatch), zimno (battania, ibbenese, nord, sturgia), umiarkowany (vale, river,
empire, sarnor, yiti, norvos, crownlands, dragonstone, vlandia), cieply (pentoshi, qohorik, khuzait, Reach i Stormlands
na polnoc od y=245), srodziemnomorski (tyroshi, myrish, lyseni, volantine, valyrian, Reach i Stormlands na poludnie od
y=245), goraco-sucho (aserai, ghiscari, qartheen), tropik (summer). Wspolrzedne: y rosnie na polnoc (Winterfell 845, Moat
Cailin 703, King's Landing 397, Sunspear 104).

### 3.1 BZDURY (12) - do zmiany

| Wies (id) | Typ dzis | Gdzie | Bound -> miasto targowe | Propozycja (rozdz. 6, A1) |
|---|---|---|---|---|
| Ornstead (`castle_village_B7_1`) | bawelna | Nocna Straz, y=1066 | Shadow Tower -> Castle Black | owce (czarna welna plaszczy Strazy) |
| Karahan (`village_K6_1`) | bawelna | Sarnor, stepy | Saath | owce |
| Danara (`village_K6_3`) | bawelna | Sarnor | Saath | bydlo |
| Ispantar (`village_K5_3`) | bawelna | Sarnor | Kyth | konie stepowe |
| Samatha (`village_EN5_4`) | bawelna | Braavos, y=629 | Braavos | len (plotno zaglowe) |
| Durlston (`castle_village_EN2_1`) | bawelna | gory Doliny, Krwawa Brama | Bloody Gate -> The Eyrie | owce |
| Metachia (`castle_village_ES2_2`) | bawelna | Norvos (wzgorza) | Ny Sar -> Menetragos | len |
| Old Stonebridge (`village_EW5_3`) | bawelna | Crownlands | Duskendale | len |
| Rushing Falls (`castle_village_EN5_1`) | bawelna | Riverlands | Acorn Hall -> Harroway | bydlo |
| King's Mountain (`ROT_castle17_village2`) | bawelna | Kingswood | Fellwood -> Hull | drwale |
| Gelina (`ROT_castle49_village1`) | winnica | Lorath, y=651 | Anogar -> Tolero Aeksio | rybacy (Lorath - wielorybnicy; `whaler` jesli wies nad morzem) |
| Tarth (`ROT_town8_village1`) | daktyle | wyspa Tarth (umiark.) | Evenfall Hall | rybacy |

Juz zmienione przez CrashScribe NorthernFare (dziala, log 12:15 i 03:18): Farsfog winnica -> rybacy, Queenscrown -> rybacy,
Tumbledown -> bydlo, Olden Oak -> swinie.

Kompensata bawelny (inaczej tkalnie aksamitu - najbardziej dochodowy warsztat towarowy, 60-117 d/dobe/warsztat w logu -
straca 10 z 13 wsi): przeniesc bawelne tam, gdzie pasuje, w miejsce typow watpliwych:
Shirosi (`ROT_town35_village2`, drwale w Czerwonym Pustkowiu -> Qarth), Port Yhos (`ROT_town37_village2`, drwale -> Qarkash),
Sagora (`village_ES4_1`, drwale -> Valysar, delta Rhoyne), Abar (`ROT_town11_village1`, daktyle -> Aquos Dhaen, Lys),
Tyrono (`ROT_castle47_village2`, daktyle -> Tyrosh). Bawelna wtedy 8 wsi, wszystkie w cieplych strefach (64 bazy zamiast 104).

### 3.2 WATPLIWE (zostawic albo zmienic przy okazji)

- **Winnice w pasie umiarkowanym (9)**: Riverlands 6 (Hag's Mire - nazwa mowi "bagno" przy Przesmyku, Mudgrave, Nunn's Deep
  (hearth 100), Mummer's Ford, Briarwhite, Aldbridge), Clearstream (Dolina), Parchments (Dragonstone), Laraia (Norvos).
  Historycznie wino roslo nad Renem i w Burgundii - nie bzdura, ale proporcje (2.2) odwrotne do lore.
- **Oliwki na granicy (2)**: Rhaes Azor (Dothrakowie, y=323), Rooster's Top (Stormlands, y=269, tuz nad Marchiami).
- **Daktyle na wyspach srodziemnomorskich (7)**: Tyrosh 3 (Waltas, Tyrono, Bloodstone), Lys 3 (Asmait, Abar, Mussum),
  Greenview (Stormlands), Krysaro (Demongate). Palma daktylowa chce goraca i suszy; Lys/Tyrosh to raczej oliwki i winorosl.
- **Wielblady (rancza pustynne) w Lys, Tyrosh, Myr, Volantis (11)** - BK/gra daje im wielblady 0.68/dobe; w Dorne to
  "piaskowe rumaki", nie wielblady. Drobiazg.
- **Pszenica za Murem i przy Murze (9)** - Wolni Ludzie i Dar Strazy uprawiaja troche jeczmienia, OK; ale BK dokleja kazdej
  pszenicy **jablka 2 i papirus 0.5** - papirus na Polnocy i za Murem to bzdura (Westeros pisze na pergaminie),
  jablka za Murem - watpliwe.
- **Drwale na pustyni (Qarth 3, Dorne 1)** i **las wsi Armoury dla KAZDEJ wsi** (2.5 ladunku/dobe, takze Dorne, Czerwone
  Pustkowie, Zatoka Niewolnicza - ok. 70 wsi, ok. 175 ladunkow/dobe "lasu" na pustyni).
- **Braki lore** (bez zmiany danych - do decyzji): Zatoka Niewolnicza bez gajow oliwnych (ADWD - gaje pod Meereen);
  Lorath bez wielorybnikow; Yi Ti bez bawelny/jedwabiu.

Pelna lista z wspolrzednymi: `scratchpad\audprod\flags.txt`.

---

## 4. Warsztaty w miastach

### 4.1 Jak gra je wybiera (kod)

- Miasto ma 4 sloty: slot 0 = ukryci "rzemieslnicy" (`artisans`), sloty 1-3 = warsztaty notabli
  (log: 291 warsztatow = 97 x 3 w kazdej sesji).
- Typ wybiera `WorkshopsCampaignBehavior.DecideBestWorkshopType(settlement, atGameStart: true)` przy tworzeniu nowej gry
  (`BuildWorkshopsAtGameStart` -> `BuildWorkshopForHeroAtGameStart`), **losowo z wagami** z `FindTotalInputDensityScore`:
  - bierze tylko wsie, ktorych `TradeBound` to to miasto (wsie miejskie + wsie zamkowe przypisane przez
    `DefaultVillageTradeModel.GetTradeBoundToAssignForVillage` - najblizsze miasto wlasnego krolestwa w zasiegu 231),
  - sumuje `VillageType.Productions` (krowy liczy jako skory, owce jako welne, zboze tylko z farm pszenicy),
  - waga typu = `(0.01 + suma[podaz wsadu / (predkosc linii x ilosc wsadu)]) x frequency / (1 + 6 x podobienstwo do
    warsztatow juz w miescie)^3`, potem `^0.6`; liczone tylko linie z wyrobem-towarem (bron i zbroja sie nie licza).
- **Stala 0.01** daje kazdemu typowi wage ok. 0.06-0.10 nawet bez zadnego wsadu. Gdy wsie miasta nie daja nic do przerobu
  (ryby, futra, konie, sol, olej wielorybi), **wszystkie typy maja te sama wage - czysta loteria z 19 typow**.
- **BK** (`EconomyPatches.WorkshopsCampaignBehaviorPatches.DecideBestWorkshopTypePostfix`): jesli wylosowany typ juz jest
  w miescie - 30% "kopalnia" (`mines`), inaczej losowo kuznia / lukmistrz / siodlarz / platnerz / miecznik BK.
- Receptury ciast BK w piekarni (`grain`/0.5 x 5 linii) daja farmie pszenicy wage ok. 42 wobec ok. 4-6 innych typow,
  wiec **piekarnia w 76-81 z 97 miast**; nawet bez pszenicy z samego masla (Seagard 84%, Mhysa Faer 71%).
- Potem typ jest zamrozony: zmienia sie tylko przy bankructwie (`atGameStart: false` - wtedy dochodzi premia za tani wsad
  na targu), a **bankructw jest 0 przez 40 dob** (log). Kazda sesja testu to nowa kampania (dzien startu 108836 w kazdej) -
  stad rozne liczby warsztatow z sesji na sesje.
- CrashScribe NorthernFare (`OnSessionLaunched`) dziala **po** rozdaniu warsztatow - zmienia wsie, a warsztaty poprawia
  tylko czesciowo (rozdz. 4.5).

### 4.2 Walidacja odtworzenia algorytmu (symulacja vs log)

Wiernie odtworzylem `DecideBestWorkshopType` + postfiks BK + przypisanie wsi do miast (odleglosc w linii prostej zamiast
drogi; wynik 260/260 wsi zamkowych we wlasnym krolestwie, log mowi 258/2). Oczekiwana liczba na kampanie wobec 12 sesji:

| typ | symulacja | log (12 sesji, min-max) | log 12:15 |
|---|---|---|---|
| piekarnia (bakery) | 76.2 | 76-81 | 80 |
| stolarnia (wood) | 28.1 | 23-34 | 28 |
| tkalnia welny | 23.4 | 16-28 | 22 |
| kuznia (smithy) | 18.7 | 13-23 | 17 |
| zlotnik | 17.7 | 15-21 | 17 |
| rzeznia (BK) | 15.1 | 12-19 | 18 |
| tlocznia wina | 14.0 | 6-16 | 16 |
| browar | 13.4 | 8-18 | 10 |
| olejarnia | 11.6 | 8-14 | 12 |
| tkalnia lnu | 11.2 | 9-13 | 11 |
| tkalnia aksamitu | 10.5 | 6-14 | 12 |
| kopalnia BK (mines) | 9.6 | 5-10 | 7 |
| garncarnia | 9.0 | 7-12 | 8 |
| garbarnia | 8.8 | 5-15 | 5 |
| lukmistrz BK | 6.0 | 1-14 | 6 |
| platnerz / siodlarz / miecznik BK | 5.4 / 5.3 / 4.9 | 2-8 / 1-12 / 3-11 | 7 / 7 / 4 |
| miodosytnia | 2.1 | 0-4 | 4 |

Zgodnosc dobra - wnioski "na miasto" ponizej (rozdz. 4.3-4.4 i Zalacznik A) to prawdopodobienstwa z tej samej symulacji.
Gra nie wypisuje listy warsztatow miasto po miescie (jedyny przyklad w logu: Lannisport 12:15 - piekarnia, kuznia, zlotnik).

### 4.3 Bzdury klimatyczne w warsztatach (symulacja, start nowej gry)

| Warsztat bez wlasnego wsadu | Strefa arktyczna + zimna (Polnoc, za Murem, Wyspy Zelazne, Lorath, Ibben) | + strefa umiarkowana |
|---|---|---|
| olejarnia (zero oliwek w calej strefie) | 0.92 na kampanie, **w 62% kampanii co najmniej jedna** | 1.79, 85% |
| tlocznia wina bez winogron | 0.86, 59% | 1.60, 81% |
| tkalnia aksamitu bez bawelny | 0.91, 61% | 1.61, 81% |

Miasta-loterie (wsie bez surowca do przerobu, po rowno z 19 typow): Sisterton (olejarnia 20%), Ibben 14%, Mormont Keep 14%,
Pyke 13%, Kayce 8%, Last Hearth 7%, Driftwood Hall 5% itd. Z "watpliwych": Arbor (tylko winnice + ryby - tlocznia 99%, ale
drugi i trzeci slot losowy), Sunspear (oliwki - olejarnia 99%, reszta losowa).

Do tego warsztaty "zgodne z wsiami", ktore istnieja tylko dlatego, ze wies ma zly typ: tlocznia w Winterfell (45%) i Castle Black
(29%) - poprawiane przez CrashScribe; **tlocznia w White Harbor (30%) - nie poprawiana (blad, 4.5)**; tkalnie aksamitu w Saath 93%,
Duskendale 84%, Hull 72%, The Eyrie 59%, Menetragos 58%, Kyth 56%, Harroway 56%, Braavos 51%, Castle Black 33% - wszystkie
na bawelnie z rozdz. 3.1.

### 4.4 Niedopasowanie ekonomiczne - surowiec bez przerobu (symulacja)

| Surowiec wsi (prog) | Miast z surowcem | Oczekiwanie: z warsztatem przerobczym | Przerobczych bez surowca (z wszystkich) | Najczesciej BEZ warsztatu |
|---|---|---|---|---|
| **ruda -> kuznia** | 24 | **13.2 (55%)** | 5.4 z 18.7 | Castle Black 74%, Riverrun 71%, Lannisport 69%, Oldtown 66%, Vaes Diaf 65%, Frostfang's Camp 63%, Storm's End 58%, Fairmarket 56% |
| drewno -> stolarnia | 38 | 26.1 (69%) | 2.0 z 28.1 | Castle Black 69%, Riverrun 67%, Lannisport 65%, Vaes Diaf 60%, Oldtown 59%, White Harbor 59% |
| len -> tkalnia lnu | 16 | 8.3 (52%) | 2.9 z 11.2 | Riverrun 66%, Lannisport 65%, Frostfang's Camp 59%, White Harbor 58%, Pentos 50% |
| bawelna -> aksamit | 12 | 7.7 (64%) | 2.9 z 10.5 | Castle Black 67%, Braavos 49%, Starfall 47% |
| winogrona -> tlocznia | 22 | 11.4 (52%) | 2.6 z 14.0 | Tolero Aeksio 74%, Castle Black 71%, White Harbor 70%, Pentos 68%, Riverrun 68% |
| oliwki -> olejarnia | 13 | 8.9 (68%) | 2.7 z 11.6 | Vaes Diaf 67%, Starfall 57%, Highgarden 54%, Planky Town 42%, Oldtown 38% (4 gaje!) |
| glina -> garncarnia | 15 | 6.0 (40%) | 3.0 z 9.0 | Castle Black 77%, Lannisport 77%, Vaes Diaf 72%, Tolero Aeksio 69%, Braavos 66% |
| srebro -> zlotnik | 24 | 15.5 (65%) | 2.2 z 17.7 | Frostfang's Camp 74%, Lannisport 70% (2 kopalnie srebra w krainie zlota i srebra), Braavos 69% |
| owce (farma) -> tkalnia welny | 21 | ok. 13 (60%) | ok. 10.9 tkalni welny bez farmy owiec | - |
| bydlo (farma) -> garbarnia | 20 | ok. 4 (19%) | - | prawie wszedzie |

Wniosek: miasta z wieloma surowcami (Riverrun - zboze, glina x2, winnice x2, ruda, drewno, len; Lannisport - zboze x4,
srebro x2, glina, len, ruda, drewno; Castle Black, Oldtown, Vaes Diaf) maja 2 wolne sloty na 5-7 surowcow - i loteria
czesto daje ruda/drewno/len bez przerobu. **Razem 76 z 291 warsztatow (26%) nie ma wsadu z wlasnych wsi**: tkalnie welny 10.9
(z owiec farm pszenicy 0.4), rzeznie 8.4, kopalnie BK 5.7, kuznie 5.5, garbarnie 5.1, lukmistrzowie 4.6, miecznicy 4.2,
piekarnie 4.2 (na samym masle), platnerze 3.9, siodlarze 3.7, garncarnie/len/aksamit/olej/wino po 2.7-2.9, zlotnicy 2.3.

Wazne: wozy wsi (Armoury, "Dowoz (wozy)") jada do miasta, ktore najlepiej placi (55 z 94 wozow do innego miasta niz wlasne),
a karawany woza surowce - wiec surowiec nie musi byc przerobiony w swoim miescie. Ale log pokazuje, ze to nie wyrownuje rudy
(rozdz. 5).

### 4.5 Blad w CrashScribe "strawa Polnocy"

`CrashScribe\src\Mends.cs:2134` i `:2140`: `towns.Add(st.Village.Bound)`. Dla wsi zamkowej `Bound` to zamek (nie ma warsztatow),
a warsztaty wybieral `TradeBound`. Farsfog (zamek Moat Cailin) handluje z **White Harbor** - prasa do wina tam (30% kampanii)
zostaje, choc winogron juz nie ma. Olden Oak (Shadow Tower) handluje z Castle Black - tu ratuje to przypadkiem Queenscrown.
Ponadto (`:2145-2152`) zamieniane sa tylko tlocznie wina; tkalnie aksamitu po przyszlej zmianie bawelny - nie.
I kolejnosc: `MendsBehavior` wola NorthernFare w `OnSessionLaunched` (`Mends.cs:4693`), czyli po `BuildWorkshopsAtGameStart`
(dowod: sesja 03:18 zamienila juz istniejaca tlocznie w Winterfell).

### 4.6 Co warsztaty robia naprawde (log, srednie z 40 dob; 19 dla 03:18)

| typ | szt. 12:15 / 12:37 / 03:18 | pracowalo dob | wynik d/dobe/warsztat (12:15) | uwagi |
|---|---|---|---|---|
| piekarnia | 80 / 81 / 76 | 97% | +10.5 | |
| stolarnia | 28 / 30 / 26 | 76-78% | +53 | 0.13 cyklu desek/dobe - zyje z lukow i tarcz |
| tkalnia welny | 22 / 22 / 18 | 94-99% | +60 | welna i tak sie pietrzy |
| rzeznia BK | 18 / 12 / 17 | 100% | +21 | |
| kuznia | 17 / 17 / 23 | 82-88% | +91..+126 | najlepiej platna obok platnerza |
| zlotnik | 17 / 17 / 15 | 28-40% | -0.1 | |
| tlocznia wina | 16 / 12 / 14 | 51-82% | +0.6 | ok. 20% bez winogron (sieroty) |
| olejarnia | 12 / 13 / 10 | 58-67% | +1.5 | ok. 23% bez oliwek |
| tkalnia aksamitu | 12 / 8 / 8 | 45-48% | +60..+117 | |
| tkalnia lnu | 11 / 13 / 11 | 60-79% | +6 | |
| browar | 10 / 11 / 18 | **12-24%** | -3 | zboze zjada piekarnia |
| garncarnia | 8 / 10 / 12 | **7-14%** | -3 | |
| garbarnia | 5 / 15 / 12 | 38-64% | +2..+6 | |
| siodlarz BK | 7 / 6 / 1 | 21-27% | -3..+7 | 0 cykli towarowych |
| **kopalnia BK** | 7 / 9 / 9 | **0%** | -4 | ani razu przez 99 dob 3 sesji |
| **miodosytnia** | 4 / 3 / 2 | **0%** | -4 | ani razu |

---

## 5. Ilosci: nadwyzki i braki (log)

Sesje 12:15 i 12:37 (nowe kampanie, po 40 dob), srednie z dob 21-40; "targi" = targi 97 miast.

| Towar | Doplyw | Zuzycie | Bilans targow miast | Rozmieszczenie | Ocena |
|---|---|---|---|---|---|
| **ruda** (ladunek 100 kg) | wsie 180/dobe (model 196, 23-26 kopalni, z x3); kopalnie BK 0 | warsztaty zbrojne 64, narzedzia 28, budowy 0 | **+56 / +50 na dobe** (128 -> 2798) | **36-40 miast bez rudy** (placa 36.5 d), 29 z nadwyzka (sprzedaja po 3 d); indeks 0.13 / 0.75 / 10.0 | **zle rozlozona**: kuznie zbrojne odpuszczaja 755 cykli/dobe z braku rudy, a ruda lezy w miastach-kopalniach |
| drewno | drwale 262 + las wsi 1227 = 1489 | zbrojne 396, deski 4, budowy 519 | **+392 / +393** (5.2 -> 12.8 tys.) | 10-16 miast bez | nadwyzka ok. 25% - glownie z lasu wsi |
| skory surowe | rzeznie, rzemieslnicy (uboj) | garbarnie 5-15, rzemieslnicy (140/dobe garbowania) | **+127 / +123** (0.3 -> 4.4 tys.) | indeks 0.41, 72 miasta z nadwyzka | za malo garbarni |
| welna | 23 farmy owiec + uboj owiec | tkalnie welny ok. 40/dobe; **popyt rzemiosla 0 d** | **+144 / +178** (0.6 -> 6.2-7.2 tys.) | indeks 0.32, 24 miasta bez | najwieksza nadwyzka |
| len | 19 wsi | tkalnie lnu, rzemieslnicy (199 plotna/dobe) | +74 / +62 | 34 miasta bez, indeks 0.96 | rozlozony nierowno |
| plotno | tkalnie + rzemieslnicy | mieszczanie, wojsko | +54 / +59 | 26 bez | |
| skora | garbarnie + rzemieslnicy | zbrojne, siodlarze | +33 / +42 | 7 bez | |
| mieso | uboj | | +14 / +10 | | |
| bron i zbroja na polkach | - | - | 102 900 szt.; 10 323 bez odbiorcy w zasiegu 250 | | przesyt (powod, ze kuznie w miastach z ruda nie biora rudy - ranking "bez zysku") |

Najwieksze nadwyzki: **drewno +392, welna +144-178, skory surowe +123-127, len +62-74, plotno +54-59, ruda +50-56** (na dobe,
targi miast). Najwiekszy brak: **ruda w 36-40 miastach** (i 755 cykli zbrojnych/dobe bez rudy), drewno w 10-16 miastach,
len w 34. Tego, ile oliwek, winogron, daktyli, bawelny, wina, oleju, aksamitu jest produkowane i zjadane, **log nie podaje**
(poprawka B5).

---

## 6. Poprawki

Kolejnosc proponowana: A2 (blad) -> A1 + A3 + B1 + B3 razem w jednym miejscu kodu (jeden test nowej kampanii) -> B5 -> B2 -> B4 ->
A4/A5/A6. Kazda zmiana osobno do testu (CLAUDE.md, zasada 2), kontrola regresji ponizej.

### A. Bzdury wobec lore

**A1. Tabela klimatu wsi (dane w kodzie, nie w XML ROT)**
- Co: rozszerzyc mape `NorthernFare` o 12 bzdur z 3.1 i 5 wsi kompensujacych bawelne (3.1). Zrobic z niej tabele
  `id wsi -> typ` (latwo dopisac kolejne). Zmieniac tylko, gdy obecny typ = oczekiwany stary (jak dzis - idempotentnie).
- Gdzie: `CrashScribe\src\Mends.cs:2112-2160` (slownik `map` w :2117-2123) albo nowa klasa w Armoury obok `MineralOnce.cs`.
  **Kluczowe: wywolac PRZED rozdaniem warsztatow nowej gry** - prefiks Harmony na prywatnej
  `WorkshopsCampaignBehavior.BuildWorkshopsAtGameStart` (albo `OnNewGameCreatedPartialFollowUp` dla i == 0); obecne wywolanie
  w `OnSessionLaunched` zostaje dla starych zapisow.
- Dlaczego nie XML ROT (`ROT-Map\ModuleData\settlements.xml`, atrybut `village_type`): aktualizacja ROT nadpisze plik, a zmiana
  dziala tylko w nowej grze. W kodzie - jedno zrodlo prawdy, dziala tez na zapisach.
- Efekt (kod/symulacja): bawelna w strefach zimnych/umiark. 80 -> 0 bazy; tkalnie aksamitu w tych strefach 7.2 -> 0 na
  kampanie, w cieplych ok. 3.3 -> ok. 9 (z kompensata; razem 10.5 -> 9.1 - dochody z aksamitu prawie bez zmian); +2 farmy owiec i +1 bydla w Sarnorze, +3 wsie lnu (Braavos, Norvos,
  Crownlands) - wiecej welny i lnu, ktorych i tak jest nadwyzka (bez ryzyka niedoboru). Gelina/Tarth - wiecej ryb.

**A2. Naprawa bledu "strawy Polnocy"**
- Co: zbierac miasta po `st.Village.TradeBound` (i `Bound`, jesli to miasto); po zmianie typow wsi sprawdzic KAZDY warsztat
  tych miast: jesli jego wsad zniknal z wsi miasta - `ChangeWorkshopProduction` na typ z tej samej rodziny (tlocznia -> browar,
  aksamit -> tkalnia lnu/welny, wedle tego, co wsie daja).
- Gdzie: `Mends.cs:2134`, `:2140` (zbieranie miast), `:2145-2152` (petla warsztatow - tylko `wine_press`).
- Efekt: tlocznia bez winogron w White Harbor 30% kampanii -> 0. Ryzyko: male (ta sama metoda co dzis;
  `WorkshopTrade.Get` juz poprawia znacznik kapitalu przy zmianie typu, `WorkshopTrade.cs:139-142` / kopia robocza `:170`).

**A3. Bramka "przerobka tylko przy surowcu" w wyborze warsztatu**
- Co: (a) postfiks na prywatnej `WorkshopsCampaignBehavior.FindTotalInputDensityScore(settlement, type, productionDict, atGameStart)`:
  dla typu przerobczego (olejarnia - oliwki, tlocznia - winogrona, aksamit - bawelna, len, garncarnia - glina, zlotnik - srebro,
  garbarnia - skory, tkalnia welny - welna z farmy owiec, miodosytnia - miod) **wynik = 0**, gdy w `productionDict` nie ma
  wsadu (prog jak w symulacji: >= 5 szt. bazy, srebro >= 1, skory >= 1.5) i - przy `atGameStart == false` - wsad nie jest tani
  na targu (`GetPriceFactor < 1`). (b) postfiks na `DecideBestWorkshopType` z `[HarmonyAfter("BannerKings")]`/`Priority.Last`:
  jesli BK zamienil duplikat na `mines` albo losowego kowala BK, a miasto nie ma dla niego wsadu - wziac najlepszy typ z wsadem,
  ktorego jeszcze nie ma i ktory nie dubluje surowca (np. drugi przerob zboza); gdy nic nie pasuje - kuznia albo stolarnia
  (uniwersalne, wsad dowioza wozy).
- Gdzie: nowa latka w `Armoury\src\WorkshopTrade.cs` (juz trzyma `DecideBestWorkshopType` przez refleksje, `:415` / kopia robocza `:557`)
  albo `WorkshopLaw.cs` (juz latamy `WorkshopsCampaignBehavior`).
- Efekt (symulacja, 1500 kampanii): olejarnie bez oliwek w zimnych strefach 0.92 -> **0**, tlocznie bez winogron 1.66 -> **0**,
  warsztaty bez wsadu **76 -> 24** (tyle samo z B1). Uwaga: w tej symulacji kopalnie BK i miodosytnie byly jeszcze dozwolone
  przy wsiach gorniczych / lesnych i **rosna do ok. 14 i 10 na kampanie** (dzis 9.6 i 2.1) - a w logu pracuja 0% - dlatego
  **B3 musi wejsc razem z A3**. Dziala tez przy bankructwie (`WorkshopTrade.Fail` wola te sama metode) - to pozadane.
- Ryzyko: srednie. Sprawdzic, ze nie rozmnaza browarow (w wersji bez warunku "nie dubluj surowca" browarow robilo sie 31 - pracuja
  12-24% dob); ze kolejnosc z BK jest dobra (nasz postfiks PO BK); ze `artisans` (ukryty) nie wchodzi w bramke.

### B. Niedopasowanie ekonomiczne

**B1. "Przy kopalni kuznia" (regula Jeffa)**
- Co: po rozdaniu 3 warsztatow miasta (postfiks na `BuildWorkshopsAtGameStart`, ten sam co A3), jesli wsie handlowe miasta
  maja kopalnie zelaza (ruda >= 5 bazy), a kuzni nie ma - zamienic na `smithy` kolejno: warsztat bez wsadu, duplikat, warsztat
  z najmniej wartym wsadem (nigdy jedynej piekarni). Opcjonalnie to samo dla "sygnaturowych" towarow krainy: gaj oliwny ->
  olejarnia, winnica -> tlocznia, srebro -> zlotnik (Lannisport!).
- Gdzie: `Armoury\src\WorkshopTrade.cs` (nowa metoda np. `MatchLocalRaw(Town)`), zamiana przez `Workshop.ChangeWorkshopProduction`
  (jak CrashScribe; na starcie gry bez kosztu).
- Efekt (symulacja): miasta z ruda i kuznia **13/24 (55%) -> 24/24 (100%)**; kuzni na kampanie ok. 19 -> 35-40; pozostale
  pokrycia podobne albo lepsze (srebro 65 -> 75%, oliwki 69 -> 79%, len 52 -> 48-55%). Ile rudy zjedza nowe kuznie (SZACUNEK): linia narzedzi 1.5 rudy/dobe
  + linie zbrojne WorkshopLaw - ok. +15-40 rudy/dobe z +50-56 nadwyzki w miastach-kopalniach; braku w 36-40 miastach bez kopaln
  to nie usuwa (od tego jest dowoz - PLAN-K13 P5c "wytop przy kopalni").
- Ryzyko: male-srednie (wiecej kuzni = wiecej narzedzi i broni niskiego tieru przy przesycie 102 900 sztuk na polkach;
  sprawdzic linie "Warsztaty" i "Rynek broni").

**B2. Jednorazowa poprawka istniejacych zapisow**
- Co: raz na zapis (flaga w save, `SaveDefiner`/`SyncData`) w `OnSessionLaunched`: dla kazdego miasta zastosowac A3+B1 do juz
  stojacych warsztatow (tylko warsztaty notabli, nie gracza).
- Gdzie: `Armoury\src\WorkshopTrade.cs`. Efekt: stare kampanie dostaja te same proporcje co nowe. Ryzyko: srednie - zmiana typu
  w trakcie gry; kapital przepiecia - `WorkshopTrade.SyncInitialCapital` (juz obsluguje zmiane typu z cudzego kodu).
  Jeff testuje glownie nowe kampanie - mozna odlozyc.

**B3. Usunac martwe typy z losowania: kopalnia BK i miodosytnia**
- Co: w bramce A3 `mines` dopuszczac tylko w miastach z wsiami gorniczymi, a `meadery` wcale, dopoki nie ruszy (0% pracy przez
  99 dob w 3 sesjach - zbadac osobno: prog cyklu `WorkshopTrade` vs cena 1 rudy 3-36 d; miod BK 0.5/wies drwali zjadany przez
  mieszczan?). Istniejace zamienic jak w B2.
- Efekt: ok. 9-14 slotow na kampanie wraca do przerobu (garbarnie, tkalnie). Ryzyko: male.

**B4. Przestawianie najgorszego warsztatu tam, gdzie lezy tani surowiec**
- Co: raz na 30 dob w miescie: warsztat notabla ze srednim wynikiem < 0 przez 30 dob (`WorkshopTrade` ma juz srednia `Rec.Avg`)
  przechodzi na typ wskazany przez `DecideBestWorkshopType(..., atGameStart: false, obecny)` - gra sama doda premie za tani wsad
  (welna indeks 0.32, skory 0.41). Najwyzej 1 zmiana na miasto na 30 dob, koszt przestawienia wedle gry. To jest `AUDYT-SUROWCE.md`
  P4 (b) - tu z liczbami: kandydaci to browary (12-24% pracy), garncarnie (7-14%), zlotnicy (28-40%), sieroty olejarni/tloczni.
- Efekt (SZACUNEK): kilka-kilkanascie garbarni i tkalni welny wiecej; nadwyzka welny (+144-178/dobe) i skor (+123-127) maleje.
  Ryzyko: srednie - zmienia gospodarke miast w trakcie gry; log kazdej zmiany.

**B5. Ksiega towarow rolnych i luksusowych (bez zmiany zachowania)**
- Co: linia dzienna jak "Rynek surowcow" dla grape, olives, date_fruit, cotton, wine, oil, velvet, fish, grain, fur, salt:
  wydobyte (wsie), przerobione (warsztaty), zjedzone (mieszczanie), zapas, miast bez towaru.
- Gdzie: `Armoury\src\OreLedger.cs` (ma juz pozycje i zdarzenia), wypis obok "Rynek surowcow". Efekt: odpowiedz "ile oliwek" z logu.
  Ryzyko: male.

### A4-A6 (mniejsze, lore)

- **A4. Dodatki BK wedlug klimatu**: papirus tylko w Dorne i poludniu Essos, jablka nie za Murem. Gdzie: postfiks na
  `PopulationManager.GetProductions` - ten sam hak co `Armoury\src\MineralOnce.cs` (lista jest kopiowana per wies, wiec mozna ja
  filtrowac per kultura). Efekt: 0.5 papirusu x 24 farmy pszenicy Polnocy, Muru, Skagos i za Murem znika; udzial pracy wraca do zboza.
- **A5. Las wsi wedlug klimatu**: `VillageWoodlot.TickPostfix` mnozy stawke przez wspolczynnik kultury (pustynie Dorne / Ghis /
  Qarth 0.3, tropik 1, Polnoc i Riverlands 1.2). Efekt: ok. -120 ladunkow/dobe na pustyniach przy nadwyzce drewna +392 - bez ryzyka
  braku; Polnoc +ok. 50. Gdzie: `VillageWoodlot.cs:108` (kopia robocza) + pole w `Settings.cs`.
- **A6. Proporcje (opcjonalnie, decyzja Jeffa)**: +1-2 winnice w Reach (Highgarden/Oldtown) w miejsce watpliwych w Riverlands
  (Hag's Mire - bagno, Nunn's Deep - hearth 100); Lorath: Gelina jako wielorybnicy; Zatoka Niewolnicza: 1 gaj oliwny
  (np. w miejsce drwali Ghozai pod Elyria). Te same tabele co A1.

### Co sprawdzic po wdrozeniu (kontrola calosci)

- Regresje: `WorkshopTrade.Fail` (refleksja na `DecideBestWorkshopType`) dostanie bramke - dobrze, ale sprawdzic log "bankructwa";
  CrashScribe NorthernFare i A1 nie moga zmieniac tej samej wsi dwa razy (warunek "stary typ"); `MineralOnce` i A4 na tym samym
  `GetProductions` - jeden postfiks albo jawna kolejnosc.
- Kolizje: postfiks BK na `DecideBestWorkshopType` - nasz musi biec po nim; `WorkshopLaw.CyclePrefix` (linie zbrojne) - B1 doda
  kuznie, ktore tez kupuja rude.
- Spojnosc: zamiana typu na starcie nie kosztuje nikogo (warsztat dopiero powstaje); w trakcie gry (B2/B4) - koszt i kapital przez
  `WorkshopTrade`.
- Log: nowa linia "Warsztaty (dopasowanie)" - ile zamian i jakich, ile miast z ruda bez kuzni, ile warsztatow bez wsadu.

---

## Zalacznik A. Miasto -> wsie handlowe -> warsztaty (symulacja startu nowej gry, typy wsi przed CrashScribe)

P = prawdopodobienstwo, ze warsztat danego typu powstanie w miescie (w %). "Surowiec bez przerobki" - towar wsi miasta i szansa,
ze odpowiedniego warsztatu NIE bedzie. "P sieroty" - szansa, ze co najmniej 1 z 3 warsztatow nie ma wsadu z wlasnych wsi.
Kolejnosc od polnocy.

| Miasto | kultura (strefa) | wsie handlowe miasta (typy; wielkie litery = towar "klimatyczny") | warsztaty na starcie: P >= 20% | surowiec bez przerobki (P braku warsztatu > 40%) | P sieroty |
|---|---|---|---|---|---|
| Thenn | freefolk (arkt) | bydlo 1, zboze 1, drwale 1 | piekarnia 94, stolarnia 78, garbarnia 25 | - | 48% |
| Frostfang's Camp | freefolk (arkt) | futra 2, drwale 2, len 1, ryby 1, srebro 1, swinie 1, RUDA 1, zboze 1 | piekarnia 88, stolarnia 55, tk.lnu 41, kuznia 37, zlotnik 26 | ruda (63%), len (59%), srebro (74%), drewno (45%) | 10% |
| Hardhome | freefolk (arkt) | zboze 2, futra 1, ryby 1, konie 1 | piekarnia 98, rzeznia 31, tk.welny 27, kopalnia BK 21, browar 21 | - | 100% |
| Driftwood Hall | skagosi (arkt) | ryby 1, wieloryby 1, zboze 1, morsy 1 | piekarnia 97, rzeznia 30, tk.welny 26, browar 24 | - | 100% |
| Castle Black | nightswatch (arkt) | zboze 4, WINNICA 2, BAWELNA 1, drwale 1, glina 1, RUDA 1, owce 1 | piekarnia 92, tk.aksamitu 33, stolarnia 31, tlocznia wina 29, tk.welny 27, kuznia 26, garncarnia 23 | ruda (74%), bawelna (67%), winogrona (71%), glina (77%), drewno (69%) | 5% |
| Mormont Keep | battania (zimno) | srebro 1, ryby 1, wieloryby 1, futra 1 | zlotnik 98 | - | 99% |
| Last Hearth | battania (zimno) | srebro 1, garrony 1, ryby 1, futra 1, drwale 1 | stolarnia 96, zlotnik 94 | - | 84% |
| Deepwood Motte | battania (zimno) | drwale 3, zboze 2, futra 1, garrony 1, len 1 | piekarnia 94, stolarnia 80, tk.lnu 56 | len (44%) | 44% |
| Karhold | battania (zimno) | zboze 1, futra 1, drwale 1 | piekarnia 95, stolarnia 82 | - | 87% |
| Dreadfort | battania (zimno) | zboze 4, garrony 1, owce 1 | piekarnia 97, tk.welny 70, rzeznia 26, browar 22 | - | 74% |
| Winterfell | battania (zimno) | zboze 3, WINNICA 1, drwale 1, garrony 1 | piekarnia 94, stolarnia 60, tlocznia wina 45 | winogrona (55%), drewno (40%) | 66% |
| Barrowton | battania (zimno) | bydlo 2, RUDA 1, zboze 1, len 1, garrony 1, glina 1, futra 1 | piekarnia 90, tk.lnu 53, kuznia 50, garncarnia 41 | ruda (50%), len (47%), glina (59%) | 15% |
| White Harbor | battania (zimno) | zboze 3, futra 2, WINNICA 1, garrony 1, drwale 1, owce 1, bydlo 1, len 1 | piekarnia 92, tk.lnu 42, stolarnia 41, tk.welny 38, tlocznia wina 30 | len (58%), winogrona (70%), drewno (59%) | 14% |
| Flint's Finger | battania (zimno) | swinie 3, konie 1, wieloryby 1, zboze 1 | piekarnia 95, rzeznia 81 | - | 91% |
| Sisterton | vale (umiark) | ryby 2, futra 1 | tk.lnu 21, tlocznia wina 21, tk.welny 21, olejarnia 20 | - | 100% |
| Ibben | ibbenese (zimno) | drwale 1, wieloryby 1, ryby 1 | stolarnia 98 | - | 100% |
| Braavos | empire (umiark) | zboze 2, konie 1, ryby 1, BAWELNA 1, srebro 1, owce 1, glina 1 | piekarnia 92, tk.aksamitu 51, tk.welny 44, garncarnia 34, zlotnik 31 | bawelna (49%), glina (66%), srebro (69%) | 21% |
| Lorath | nord (zimno) | zboze 2, futra 1 | piekarnia 98, rzeznia 31, tk.welny 30, browar 23, kopalnia BK 22 | - | 100% |
| Saath | sarnor (umiark) | BAWELNA 2, ryby 1, zboze 1, futra 1, konie step 1 | piekarnia 94, tk.aksamitu 93 | - | 90% |
| New Ibbish | ibbenese (zimno) | swinie 1, wieloryby 1, ryby 1, zboze 1, drwale 1 | piekarnia 92, stolarnia 75, rzeznia 50 | - | 53% |
| Omber | ibbenese (zimno) | swinie 1, zboze 1, wieloryby 1 | piekarnia 95, rzeznia 66, tk.welny 23 | - | 92% |
| Lonely Light | sturgia (zimno) | drwale 1, bydlo 1, ryby 1, zboze 1 | piekarnia 94, stolarnia 79, garbarnia 24 | - | 49% |
| The Twins | river (umiark) | zboze 2, konie 1 | piekarnia 97, rzeznia 31, tk.welny 29, browar 24, kopalnia BK 22 | - | 100% |
| Blacktyde | sturgia (zimno) | ryby 2, zboze 1, RUDA 1 | piekarnia 95, kuznia 78 | - | 68% |
| Heart's Home | vale (umiark) | konie 3, zboze 3, bydlo 2, owce 1, ryby 1, WINNICA 1 | piekarnia 95, tk.welny 58, tlocznia wina 47, garbarnia 21 | winogrona (53%) | 35% |
| Tolero Aeksio | nord (zimno) | len 2, zboze 2, ryby 1, drwale 1, glina 1, WINNICA 1, futra 1 | piekarnia 91, tk.lnu 58, stolarnia 45, garncarnia 31, tlocznia wina 26 | len (42%), winogrona (74%), glina (69%), drewno (55%) | 27% |
| Seagard | river (umiark) | bydlo 2, srebro 1, WINNICA 1, ryby 1 | piekarnia 84, tlocznia wina 73, zlotnik 70, garbarnia 39 | - | 92% |
| Pebbleton | sturgia (zimno) | zboze 2, futra 2, srebro 2, konie 1, owce 1, sol 1, len 1 | piekarnia 92, zlotnik 52, tk.lnu 51, tk.welny 48 | len (49%), srebro (48%) | 23% |
| The Eyrie | vale (umiark) | ryby 2, BAWELNA 1, RUDA 1, zboze 1, len 1, konie 1 | piekarnia 90, tk.aksamitu 59, tk.lnu 53, kuznia 48 | ruda (52%), len (47%), bawelna (41%) | 25% |
| Ifequeveron | yiti (umiark) | futra 1, drwale 1, zboze 1 | piekarnia 94, stolarnia 82 | - | 87% |
| Ten Towers | sturgia (zimno) | ryby 1, zboze 1, RUDA 1 | piekarnia 95, kuznia 79 | - | 70% |
| Lordsport | sturgia (zimno) | zboze 1, RUDA 1, drwale 1 | piekarnia 92, stolarnia 70, kuznia 66 | - | 33% |
| Gulltown | vale (umiark) | zboze 2, bydlo 1, ryby 1, srebro 1, owce 1, konie 1 | piekarnia 95, tk.welny 63, zlotnik 49 | srebro (51%) | 26% |
| Pyke | sturgia (zimno) | ryby 2, RUDA 1, konie 1 | kuznia 97 | - | 99% |
| Fairmarket | river (umiark) | drwale 1, zboze 1, WINNICA 1, RUDA 1, glina 1 | piekarnia 90, stolarnia 49, kuznia 44, garncarnia 39, tlocznia wina 34 | ruda (56%), winogrona (66%), glina (61%), drewno (51%) | 19% |
| Rhyos | empire (umiark) | srebro 1, ryby 1, drwale 1, RUDA 1, zboze 1 | piekarnia 92, stolarnia 60, kuznia 53, zlotnik 40 | ruda (47%), srebro (60%) | 24% |
| Kyth | sarnor (umiark) | zboze 2, glina 1, ryby 1, BAWELNA 1, len 1, bydlo 1 | piekarnia 93, tk.aksamitu 56, tk.lnu 51, garncarnia 35 | len (49%), bawelna (44%), glina (65%) | 22% |
| Harroway | river (umiark) | zboze 2, BAWELNA 1, bydlo 1, glina 1, WINNICA 1 | piekarnia 92, tk.aksamitu 56, garncarnia 39, tlocznia wina 38 | bawelna (44%), winogrona (62%), glina (61%) | 23% |
| Wickenden | vale (umiark) | konie 2, zboze 1 | piekarnia 97, rzeznia 29, tk.welny 29, browar 22, kopalnia BK 21 | - | 100% |
| Norvos | norvos (umiark) | RUDA 1, zboze 1, glina 1, bydlo 1, len 1 | piekarnia 90, tk.lnu 57, kuznia 50, garncarnia 42 | ruda (50%), len (43%), glina (58%) | 16% |
| Riverrun | river (umiark) | zboze 3, glina 2, WINNICA 2, RUDA 1, drwale 1, len 1, konie 1 | piekarnia 90, garncarnia 37, tk.lnu 34, stolarnia 33, tlocznia wina 32, kuznia 29 | ruda (71%), len (66%), winogrona (68%), glina (63%), drewno (67%) | 18% |
| Saltpans | river (umiark) | zboze 2, konie 2 | piekarnia 97, tk.welny 31, rzeznia 30, browar 23, kopalnia BK 21 | - | 100% |
| Celtigar Keep | dragonstone (umiark) | zboze 1, ryby 1, konie 1 | piekarnia 97, rzeznia 28, tk.welny 28, kopalnia BK 20 | - | 100% |
| Maidenpool | river (umiark) | zboze 2, bydlo 1 | piekarnia 97, garbarnia 33, tk.welny 29, rzeznia 26, browar 22 | - | 79% |
| Harrenhal | river (umiark) | zboze 1, ryby 1, srebro 1 | piekarnia 96, zlotnik 73 | - | 85% |
| Dragonstone | dragonstone (umiark) | ryby 2, zboze 1, swinie 1, drwale 1 | piekarnia 93, stolarnia 78, rzeznia 49 | - | 55% |
| Duskendale | crownlands (umiark) | konie 2, zboze 1, BAWELNA 1 | piekarnia 95, tk.aksamitu 84 | - | 92% |
| Kayce | vlandia (umiark) | konie 2, ryby 2, futra 1, srebro 1, drwale 1 | stolarnia 97, zlotnik 94 | - | 85% |
| Menetragos | norvos (umiark) | zboze 2, konie 1, BAWELNA 1, glina 1, futra 1, WINNICA 1 | piekarnia 93, tk.aksamitu 58, garncarnia 42, tlocznia wina 40 | bawelna (42%), winogrona (60%), glina (58%) | 43% |
| Sharp Point | dragonstone (umiark) | srebro 2, WINNICA 1, owce 1, bydlo 1, zboze 1, ryby 1 | piekarnia 90, zlotnik 56, tk.welny 55, tlocznia wina 40 | winogrona (60%), srebro (44%) | 16% |
| Hull | dragonstone (umiark) | zboze 1, owce 1, bydlo 1, ryby 1, konie 1, BAWELNA 1 | piekarnia 91, tk.aksamitu 72, tk.welny 65 | - | 26% |
| Vaes Dothrak | khuzait (cieply) | konie step 2, drwale 1, srebro 1, zboze 1, ryby 1, sol 1 | piekarnia 92, stolarnia 72, zlotnik 56 | srebro (44%) | 47% |
| Qohor | qohorik (cieply) | drwale 3, konie step 1, ryby 1, zboze 1 | piekarnia 94, stolarnia 91 | - | 85% |
| Hornvale | vlandia (umiark) | konie 4, srebro 3, futra 2, zboze 2, len 1, RUDA 1 | piekarnia 92, zlotnik 56, tk.lnu 50, kuznia 44 | ruda (56%), len (50%), srebro (44%) | 28% |
| Pentos | pentoshi (cieply) | zboze 3, drwale 2, ryby 1, len 1, WINNICA 1 | piekarnia 93, stolarnia 63, tk.lnu 50, tlocznia wina 32 | len (50%), winogrona (68%) | 42% |
| King's Landing | crownlands (umiark) | zboze 2, sol 2, srebro 1, konie 1, ryby 1 | piekarnia 96, zlotnik 66, tk.welny 23 | - | 85% |
| Lannisport | vlandia (umiark) | zboze 4, srebro 2, glina 1, ryby 1, len 1, konie 1, RUDA 1, drwale 1 | piekarnia 91, stolarnia 35, tk.lnu 35, kuznia 31, zlotnik 30, garncarnia 23 | ruda (69%), len (65%), glina (77%), srebro (70%), drewno (65%) | 23% |
| Stoney Sept | river (umiark) | swinie 1, zboze 1, WINNICA 1, bydlo 1 | piekarnia 94, tlocznia wina 64, rzeznia 44, garbarnia 22 | - | 46% |
| Bolozo | pentoshi (cieply) | zboze 2, bydlo 1, WINNICA 1, swinie 1 | piekarnia 95, tlocznia wina 55, rzeznia 44 | winogrona (45%) | 51% |
| Vaes Diaf | khuzait (cieply) | bydlo 2, konie step 2, drwale 1, RUDA 1, zboze 1, glina 1, OLIWKI 1, owce 1 | piekarnia 88, stolarnia 40, tk.welny 35, kuznia 35, olejarnia 33, garncarnia 28 | ruda (65%), oliwki (67%), glina (72%), drewno (60%) | 3% |
| Draconys | qohorik (cieply) | WINNICA 2, futra 1, srebro 1, drwale 1, RUDA 1, konie step 1 | stolarnia 75, tlocznia wina 74, kuznia 71, zlotnik 55 | srebro (45%) | 15% |
| Tumbleton | reach (cieply) | zboze 2, konie 1, ryby 1 | piekarnia 97, rzeznia 30, tk.welny 30, browar 24, kopalnia BK 20 | - | 100% |
| Evenfall Hall | stormlands (cieply) | owce 1, DAKTYLE 1, zboze 1, sol 1 | piekarnia 95, tk.welny 84, browar 20 | - | 64% |
| Lhazosh | ghiscari (goraco) | owce 2, glina 1, zboze 1 | piekarnia 92, tk.welny 80, garncarnia 60 | - | 34% |
| Storm's End | stormlands (cieply) | len 2, WINNICA 2, zboze 2, RUDA 1, konie 1, ryby 1, sol 1 | piekarnia 92, tk.lnu 64, tlocznia wina 47, kuznia 42 | ruda (58%), winogrona (53%) | 27% |
| Mhysa Faer | tyroshi (srodz) | WINNICA 1, owce 1, ryby 1, sol 1 | tk.welny 91, tlocznia wina 85, piekarnia 70 | - | 97% |
| Lord Hewett's Town | reach (cieply) | RUDA 2, zboze 1, owce 1, konie 1, bydlo 1, ryby 1, drwale 1 | piekarnia 91, kuznia 62, stolarnia 53, tk.welny 47 | drewno (47%) | 7% |
| Highgarden | reach (srodz) | zboze 2, OLIWKI 1, owce 1, bydlo 1, drwale 1 | piekarnia 93, stolarnia 51, tk.welny 48, olejarnia 46 | oliwki (54%), drewno (49%) | 17% |
| Ashford | reach (srodz) | zboze 3, len 2, futra 1 | piekarnia 96, tk.lnu 80 | - | 86% |
| Stonehelm | stormlands (srodz) | konie 3, OLIWKI 2, WINNICA 2, ryby 2, RUDA 2, drwale 1 | olejarnia 74, kuznia 73, tlocznia wina 68, stolarnia 65 | - | 11% |
| Meereen | ghiscari (goraco) | zboze 2, RUDA 1, owce 1, len 1, konie step 1 | piekarnia 93, tk.lnu 55, tk.welny 49, kuznia 47 | ruda (53%), len (45%) | 15% |
| Myr | myrish (srodz) | zboze 2, ryby 2, konie+wielbl 2, OLIWKI 1 | piekarnia 96, olejarnia 71, rzeznia 21, tk.welny 20 | - | 92% |
| Tyrosh | tyroshi (srodz) | DAKTYLE 3, konie+wielbl 2, zboze 2, OLIWKI 1 | piekarnia 95, olejarnia 69, tk.welny 22 | - | 92% |
| Selhorys | volantine (srodz) | owce 2, konie+wielbl 2, zboze 1, ryby 1 | piekarnia 95, tk.welny 90 | - | 75% |
| Weeping Town | stormlands (srodz) | drwale 2, DAKTYLE 1, konie 1, zboze 1, WINNICA 1, ryby 1 | piekarnia 91, stolarnia 81, tlocznia wina 56 | winogrona (44%) | 49% |
| Tolos | ghiscari (goraco) | WINNICA 1, RUDA 1, bydlo 1 | kuznia 83, tlocznia wina 76, piekarnia 74, garbarnia 28 | - | 88% |
| Myrth | myrish (srodz) | bydlo 1, drwale 1, zboze 1, owce 1, BAWELNA 1 | piekarnia 90, tk.aksamitu 56, stolarnia 55, tk.welny 44 | bawelna (44%), drewno (45%) | 14% |
| Yunkai | ghiscari (goraco) | konie+wielbl 1, zboze 1, RUDA 1, ryby 1 | piekarnia 95, kuznia 80 | - | 69% |
| Mantarys | ghiscari (goraco) | zboze 1, konie step 1, owce 1, ryby 1, RUDA 1, DAKTYLE 1 | piekarnia 94, tk.welny 71, kuznia 66 | - | 22% |
| Valysar | volantine (srodz) | drwale 1, konie+wielbl 1, zboze 1 | piekarnia 95, stolarnia 82 | - | 87% |
| Qarkash | qartheen (goraco) | konie step 1, drwale 1, srebro 1, DAKTYLE 1, owce 1 | stolarnia 80, tk.welny 78, zlotnik 65, piekarnia 46 | - | 64% |
| Elyria | valyrian (srodz) | owce 1, konie step 1, drwale 1, srebro 1 | stolarnia 79, tk.welny 77, zlotnik 66, piekarnia 47 | - | 64% |
| Yronwood | aserai (goraco) | DAKTYLE 2, OLIWKI 2, ryby 2, sol 1, zboze 1, drwale 1 | piekarnia 92, olejarnia 76, stolarnia 69 | - | 40% |
| Oldtown | reach (srodz) | zboze 4, OLIWKI 4, drwale 1, RUDA 1, swinie 1, konie 1, ryby 1 | piekarnia 93, olejarnia 62, stolarnia 41, kuznia 34 | ruda (66%), drewno (59%) | 18% |
| Volantis | volantine (srodz) | zboze 1, OLIWKI 1, sol 1, glina 1 | piekarnia 92, olejarnia 66, garncarnia 61 | - | 52% |
| Volon Therys | volantine (srodz) | OLIWKI 1, ryby 1, BAWELNA 1 | tk.aksamitu 97, olejarnia 95 | - | 100% |
| Astapor | ghiscari (goraco) | DAKTYLE 1, konie+wielbl 1, zboze 1 | piekarnia 97, tk.welny 27, rzeznia 27, browar 22, kopalnia BK 20 | - | 100% |
| Qarth | qartheen (goraco) | konie step 1, drwale 1, srebro 1 | stolarnia 96, zlotnik 93 | - | 86% |
| Starfall | aserai (goraco) | ryby 2, zboze 2, srebro 2, OLIWKI 1, BAWELNA 1, sol 1 | piekarnia 92, tk.aksamitu 53, zlotnik 47, olejarnia 43 | bawelna (47%), oliwki (57%), srebro (53%) | 41% |
| Sunspear | aserai (goraco) | OLIWKI 2, ryby 1, DAKTYLE 1, konie+wielbl 1 | olejarnia 99 | - | 100% |
| Aquos Dhaen | lyseni (srodz) | zboze 2, DAKTYLE 1, konie+wielbl 1, glina 1 | piekarnia 96, garncarnia 68, rzeznia 21, tk.welny 20 | - | 84% |
| Planky Town | aserai (goraco) | zboze 2, sol 1, WINNICA 1, OLIWKI 1, konie+wielbl 1, ryby 1 | piekarnia 94, olejarnia 58, tlocznia wina 51 | winogrona (49%), oliwki (42%) | 62% |
| Lys | lyseni (srodz) | DAKTYLE 2, sol 1, ryby 1, srebro 1, OLIWKI 1, konie+wielbl 1 | olejarnia 96, zlotnik 94 | - | 93% |
| Vaith | aserai (goraco) | zboze 3, konie+wielbl 2, srebro 1, ryby 1, DAKTYLE 1 | piekarnia 97, zlotnik 57, rzeznia 25, tk.welny 22 | srebro (43%) | 85% |
| Arbor | reach (srodz) | WINNICA 2, ryby 1 | tlocznia wina 99 | - | 100% |
| New Ghys | qartheen (goraco) | konie step 1, drwale 1, srebro 1 | stolarnia 96, zlotnik 94 | - | 84% |
| Lotus Bay | summer (tropik) | srebro 1, ryby 1, drwale 1 | stolarnia 95, zlotnik 94 | - | 86% |

## Zalacznik B. Wsie bzdurne i watpliwe (kod; strefa, wspolrzedne, bound -> krolestwo, hearth)

```
===== BZDURA
date_farm          ROT_town8_village1     Tarth                  stormlands  CIEPLY_UMIARK y=   291 x=   688 -> Evenfall Hall (town, Stormlands) hearth=346
silk_plant         castle_village_B7_1    Ornstead               nightswatch ARKTYKA       y=  1066 x=   395 -> Shadow Tower (castle, Nights Watch) hearth=809
silk_plant         village_K6_1           Karahan                sarnor      UMIARK        y=   640 x=  1195 -> Saath (town, Sarnor) hearth=350
silk_plant         village_EN5_4          Samatha                empire      UMIARK        y=   629 x=   868 -> Braavos (town, Braavos) hearth=350
silk_plant         village_K6_3           Danara                 sarnor      UMIARK        y=   603 x=  1214 -> Saath (town, Sarnor) hearth=350
silk_plant         village_K5_3           Ispantar               sarnor      UMIARK        y=   539 x=  1222 -> Kyth (town, Sarnor) hearth=350
silk_plant         castle_village_EN2_1   Durlston               vale        UMIARK        y=   532 x=   503 -> Bloody Gate (castle, The Vale) hearth=557
silk_plant         castle_village_ES2_2   Metachia               norvos      UMIARK        y=   447 x=  1027 -> Ny Sar (castle, Norvos) hearth=350
silk_plant         village_EW5_3          Old Stonebridge        crownlands  UMIARK        y=   415 x=   554 -> Duskendale (town, House Baratheon of King's Landing) hearth=350
silk_plant         castle_village_EN5_1   Rushing Falls          river       UMIARK        y=   411 x=   413 -> Acorn Hall (castle, Riverlands) hearth=621
silk_plant         ROT_castle17_village2  King's Mountain        dragonstone UMIARK        y=   306 x=   550 -> Fellwood (castle, Dragonstone) hearth=347
vineyard           ROT_castle49_village1  Gelina                 nord        ZIMNO         y=   651 x=   997 -> Anogar (castle, Lorath) hearth=350
===== WATPLIWE
date_farm          village_A5_3           Waltas                 tyroshi     SRODZIEM      y=   222 x=   799 -> Tyrosh (town, Tyrosh) hearth=350
date_farm          castle_village_EW4_1   Greenview              stormlands  SRODZIEM      y=   197 x=   651 -> Greenstone (castle, Stormlands) hearth=346
date_farm          ROT_castle38_village2  Krysaro                volantine   SRODZIEM      y=   191 x=  1235 -> Demongate (castle, House Targaryen, Aegon) hearth=350
date_farm          ROT_castle47_village2  Tyrono                 tyroshi     SRODZIEM      y=   183 x=   819 -> Panosos (castle, Tyrosh) hearth=350
date_farm          ROT_castle28_village1  Bloodstone             tyroshi     SRODZIEM      y=   156 x=   755 -> Grey Gallows (castle, Tyrosh) hearth=477
date_farm          castle_village_A2_2    Asmait                 lyseni      SRODZIEM      y=   131 x=   903 -> Alnor (castle, Lys) hearth=401
date_farm          ROT_town11_village1    Abar                   lyseni      SRODZIEM      y=   114 x=   976 -> Aquos Dhaen (town, Lys) hearth=350
date_farm          village_A4_2           Mussum                 lyseni      SRODZIEM      y=    72 x=   853 -> Lys (town, Lys) hearth=454
desert_horse_ranch ROT_castle30_village1  Glyma                  myrish      SRODZIEM      y=   293 x=   979 -> Tolerrol (castle, Myr) hearth=350
desert_horse_ranch ROT_castle51_village2  Pantaro                myrish      SRODZIEM      y=   286 x=   952 -> Odivo (castle, Myr) hearth=350
desert_horse_ranch village_ES5_3          Parasemnos             volantine   SRODZIEM      y=   232 x=  1069 -> Selhorys (town, Volantis) hearth=350
desert_horse_ranch village_A5_2           Liwas                  tyroshi     SRODZIEM      y=   199 x=   826 -> Tyrosh (town, Tyrosh) hearth=350
desert_horse_ranch castle_village_A9_2    Wadar                  volantine   SRODZIEM      y=   183 x=  1105 -> Sar Mell (castle, Volantis) hearth=350
desert_horse_ranch ROT_castle50_village1  Terio                  lyseni      SRODZIEM      y=   170 x=   927 -> Sombazmion (castle, Lys) hearth=375
desert_horse_ranch village_ES4_3          Canterion              volantine   SRODZIEM      y=   154 x=  1041 -> Valysar (town, Volantis) hearth=350
desert_horse_ranch castle_village_A5_2    Hunab                  tyroshi     SRODZIEM      y=   147 x=   881 -> The Tree of Crowns (castle, Tyrosh) hearth=370
desert_horse_ranch ROT_town11_village2    Hoqar                  lyseni      SRODZIEM      y=   105 x=  1046 -> Aquos Dhaen (town, Lys) hearth=350
olive_trees        ROT_castle56_village1  Rhaes Azor             khuzait     CIEPLY_UMIARK y=   323 x=  1309 -> Ashefa Athaozar (castle, Dothraki Horde) hearth=448
olive_trees        castle_village_EW6_1   Rooster's Top          stormlands  CIEPLY_UMIARK y=   269 x=   572 -> Griffin's Roost (castle, Stormlands) hearth=347
vineyard           ROT_town33_village2    Hag's Mire             river       UMIARK        y=   596 x=   367 -> Seagard (town, Riverlands) hearth=577
vineyard           ROT_castle65_village1  Clearstream            vale        UMIARK        y=   586 x=   580 -> Ironoaks (castle, The Vale) hearth=350
vineyard           village_B1_2           Mudgrave               river       UMIARK        y=   528 x=   366 -> Fairmarket (town, Riverlands) hearth=490
vineyard           ROT_castle68_village1  Nunn's Deep            river       UMIARK        y=   476 x=   261 -> Willow Wood (castle, Riverlands) hearth=100
vineyard           ROT_castle39_village1  Mummer's Ford          river       UMIARK        y=   456 x=   350 -> Wayfarer's Rest (castle, Riverlands) hearth=100
vineyard           ROT_castle16_village1  Briarwhite             river       UMIARK        y=   445 x=   463 -> Darry (castle, Riverlands) hearth=312
vineyard           village_V3_4           Aldbridge              river       UMIARK        y=   383 x=   332 -> Stoney Sept (town, House Baratheon of King's Landing) hearth=360
vineyard           castle_village_EW5_1   Parchments             dragonstone UMIARK        y=   371 x=   639 -> Stonedance (castle, Dragonstone) hearth=421
vineyard           ROT_castle34_village2  Laraia                 norvos      UMIARK        y=   331 x=  1032 -> Ulentor (castle, Norvos) hearth=427
wheat_farm         village_S6_2           Crowgrave              freefolk    ARKTYKA       y=  1219 x=   478 -> Thenn (town, Free Folk) hearth=339
wheat_farm         village_S7_2           Storrold               freefolk    ARKTYKA       y=  1138 x=   588 -> Hardhome (town, Free Folk) hearth=350
wheat_farm         village_S7_3           Ghostcreek             freefolk    ARKTYKA       y=  1136 x=   571 -> Hardhome (town, Free Folk) hearth=350
wheat_farm         village_S4_2           Frostbank              freefolk    ARKTYKA       y=  1090 x=   382 -> Frostfang's Camp (town, Free Folk) hearth=350
wheat_farm         village_N1_3           Ravikayr               skagosi     ARKTYKA       y=  1081 x=   630 -> Driftwood Hall (town, Skagos) hearth=311
wheat_farm         village_B5_2           Molestown              nightswatch ARKTYKA       y=  1063 x=   497 -> Castle Black (town, Nights Watch) hearth=473
wheat_farm         castle_village_B8_2    Hogton                 nightswatch ARKTYKA       y=  1063 x=   563 -> Eastwatch by the Sea (castle, Nights Watch) hearth=350
wheat_farm         castle_village_N5_2    Snowwood               nightswatch ARKTYKA       y=  1049 x=   464 -> Nightfort (castle, Nights Watch) hearth=350
wheat_farm         ROT_castle60_village2  Giftmill               nightswatch ARKTYKA       y=  1039 x=   429 -> The Wall (castle, Nights Watch) hearth=350
fixed already by CrashScribe: [('Farsfog', 'vineyard', '->', 'fisherman'), ('Tumbledown', 'vineyard', '->', 'cattle_farm'), ('Olden Oak', 'vineyard', '->', 'swine_farm'), ('Queenscrown', 'vineyard', '->', 'fisherman')]
```

## Zalacznik C. Metoda i pliki

- `scratchpad\audprod\parse.py` - odczyt settlements.xml + ludzie-regiony.csv (krolestwo, dobrobyt) -> data.json; `vill.py`, `flag.py` - typy wsi i strefy klimatu.
- `sim.py` - odtworzenie `DecideBestWorkshopType` (gra) + `DecideBestWorkshopTypePostfix` (BK) + `GetTradeBoundToAssignForVillage` (linia prosta zamiast drogi, limit 231); `python sim.py start` = typy wsi z XML (stan przy tworzeniu warsztatow), bez argumentu = po CrashScribe. 2000 losowan na miasto.
- `ana.py` (match / lore / zones / towns), `orph.py` (warsztaty bez wsadu), `fix.py`, `fix2.py`, `fix3.py` (symulacja poprawek A1/A3/B1), `prod.py` (tabela 2.2), `appx.py` (Zalacznik A).
- Ograniczenia: odleglosc w linii prostej (gra liczy droge) - 2 wsie zamkowe moga trafic do innego miasta; wartosci przedmiotow do `PrimaryProduction` z bazy gry (Armoury zmienia ceny, ale tylko pszenica ma zboze jako glowny towar w obu wariantach); produkcja zywnosci BK nieliczona (brak logu).
