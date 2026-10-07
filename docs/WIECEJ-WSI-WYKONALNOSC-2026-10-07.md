# Wiecej wsi na mapie (x10): czy sie da i jakim kosztem (2026-10-07)

**Pytanie Jeffa (07.10):** "a da sie stworzyc wiecej wsi x 10 i rozprowadzic ludzi po wsiach i produkcje po wsiach, zeby byly te wioski na mapie?"

**Material:** dwa niezalezne badania (kod gry 1.4.8 i modow, pliki ROT-Map, mod Player Settlement) + wlasne sprawdzenie
kluczowych miejsc w dekompilacji. Tylko odczyt - zadnych zmian w grze ani w modach.

**Oznaczenia:** [ZMIERZONE] liczba z pliku, logu albo kodu; [SZACUNEK] rachunek bez proby w grze; [SPRAWDZONE] autor tego
raportu sam zajrzal do kodu.

---

## 0. Wniosek w skrocie

1. **Technicznie TAK.** Gra 1.4.8 sama stawia na mapie osade, ktorej brakuje w scenie, a nowe wsie mozna dopisac drugim
   plikiem "Settlements" w module Armoury. Sceny ROT (108 MB) nie trzeba ruszac. Dziala to juz w modzie Player Settlement
   (wersja dla RoT i 1.4.8).
2. **10 x PELNYCH wsi gry - NIE polecam.** Kazda wies gry ciagnie za soba woz, milicje, 3 notabli, duchownego BK, cache
   odleglosci i ocene w AI lordow. x10 to ok. +10 tys. partii i ok. +20 tys. bohaterow: doba 25-45 s zamiast 12 s
   (2-4 x wolniej), sejw ok. 100-150 MB, obowiazkowa nowa kampania i crash mapy bez latki tablicy 2500.
3. **Nawet x10 to dalej symbol.** Wies x10 to srednio ok. 7.4 tys. ludzi, czyli ok. 25-30 prawdziwych osad po 250 ludzi.
   Rachunek rabunku "osada po osadzie" liczy mediane ok. 243 osad na okreg i ok. 168 tys. osad na swiecie. Mapy nie da sie
   wypelnic prawdziwymi wsiami - da sie tylko wybrac, ilu symboli uzyc.
4. **Najlepsza droga (polecana):**
   - (a) **menu okregu we wsi** ("People of the district": osady, ludzie, spalone osady, uchodzcy, spichlerz) - juz w planie
     (decyzja Jeffa 07.10, DEMOGRAFIA-ANEKS rozdz. 3.3a), koszt doby 0, dziala na obecnym zapisie;
   - (b) **przysiolki na mapie jako dekoracje** - ikony wsi rozsiane po okregu, liczba wedlug ludzi okregu, stan ikony
     (1/2/3 albo spalona) z naszej ksiegi i z rabunku osada po osadzie. Obejmuje 100% okregow, doba ok. +0 s, bez nowej
     kampanii, 1 paczka. Przysiolka nie da sie kliknac ani do niego wejsc.
5. **Satelity** (prawdziwe osady z nazwa i menu, ale bez notabli, milicji, wozu i oceny AI) - mozliwe, ok. +10-30% doby,
   3-5 paczek i nowa kampania. Tylko jesli Jeff chce wchodzic do przysiolkow; najpierw proba x2.
6. **Male obozy jako partie na mapie - NIE.** Ten sam wyglad co dekoracja, ale kazda partia kosztuje czas doby, AI lordow
   je sciga, a sejw rosnie (rozdz. 6).

---

## 1. Stan dzis [ZMIERZONE]

| Co | Ile | Zrodlo |
|---|---|---|
| Osady ROT | 571 wsi, 97 miast, 130 zamkow, 266 kryjowek (1065 + 1 inna) | `Modules\ROT-Map\ModuleData\settlements.xml` |
| Wsi na twierdze | 2 (131 twierdz), 3 (75), 4 (21); srednio 2.5 | jw. |
| Ludzie okregu (wies = symbol okregu) | srednio 73.6 tys., mediana 60.7 tys., od 1.3 tys. (Nocna Straz) do 302 tys. (Reach); razem 42 mln | `scratchpad\dzien-6\rabunek-osady\calc-wynik.txt` |
| Scena mapy | `scene.xscene` 23.7 MB na dysku (z terenem i flora ok. 108 MB), 53 550 encji; wies w scenie srednio 10.9 encji, 3.6 meshy | `spojrzenie-2-mapa\ent.py` |
| Odleglosc wsi do najblizszej osady | mediana 21 jedn. mapy, 10% 11, min 4.8 | `dens.py`, `calc-wynik.txt` |
| Doba w autotescie | 12.05 s srednio (dni 1-25: 10-13 s, dni 30-40: 13-16 s), FastMode | `Armoury-2026-10-07_09-11-44.log` |
| Partie | dzien 1: ok. 2 349; dzien 40: ok. 5 022 (milicje 792, tabory 577, karawany 797, rody 679) | jw. |
| Cache odleglosci ROT | 1065 osad: Default 566 580 par (12 MB), All 687 378 par (17 MB) | `cache.py` |

Przyrost +2 670 partii wydluzyl dobe o ok. 2.5 s, czyli gorna granica ok. 0.9-1 ms na partie na dobe.

---

## 2. Jak gra stawia osade, ktorej nie ma w scenie

- `SettlementVisual.OnStartup` szuka w scenie encji o nazwie rownej id osady. Gdy jej nie ma, wola
  `MapScene.AddNewEntityToMapScene(id, pozycja)`, a ta robi `GameEntity.Instantiate(prefab o nazwie id)` na wysokosci terenu
  (`SettlementVisual.cs:442-473`, wywolanie w :471 [SPRAWDZONE]; `MapScene.cs:129-151`).
- Bez prefabu o tej nazwie encja to null i gra pada przy starcie mapy (`SettlementVisual.cs:589`). Rozwiazanie: latka Harmony
  (prefiks) na `AddNewEntityToMapScene` stawia jeden z 6 prefabow wsi, ktorych ROT uzywa dla swoich 571 wsi
  (`map_village_emp_all_40` 184, battania 84, khuzait 80, vlandia 77, sturgia 73, aserai 70), i zmienia nazwe encji na id.
  Ok. 50 linii; wzor `Player Settlement\Patches\MapScenePatch.cs`. Nie kopiujemy zadnych plikow ROT.
- Poziomy wsi 1-3 i stan "looted" (spalona) przelacza gra sama, bo prefab je ma.
- Navmesh zostaje bez zmian; pozycja musi lezec na ladzie, po ktorym da sie chodzic, i miec sciezke do swojej twierdzy.
- Wpis XML (id, nazwa, posX/posY, kultura, `village_type`, `hearth`, `bound`, scena) w drugim pliku "Settlements" w Armoury.
  Gra laczy pliki Settlements z wielu modulow, a Armoury laduje sie po ROT_Map (`Armoury\SubModule.xml:28`).
- Pozycja, brama, kultura, nazwa i typ wsi NIE ida do sejwu (`Settlement.cs:69-73`, `Village.cs:42`) - zawsze czyta je XML.
  Osada zrobiona w kodzie bez XML po wczytaniu traci pozycje albo jest usuwana (`Campaign.cs:1432-1439`,
  `UnregisterNonReadyObjects`) [SPRAWDZONE: wolanie stoi tam dla kazdego wczytania poza samouczkiem].
- Przy obecnym zapisie nowe osady z XML nie dostana stanu nowej gry (wlasciciel, notable, dane BK, tickery). W praktyce:
  **nowa kampania** albo jednorazowe zalozenie w starym zapisie metoda Player Settlement (XML w danych zachowania, ponowne
  `MBObjectManager.LoadXml`, przebudowa list i tickerow; `PlayerSettlementBehaviour.cs:3519-3566, 3846-3856`) - kruche.

---

## 3. Co kazda PRAWDZIWA wies ciagnie za soba (x10 = +5 140 wsi, ok. 6 200 osad)

| Element | Dzis | x10 pelne | Co z tym zrobic przy satelitach |
|---|---|---|---|
| Woz wiesniakow (`VillagerCampaignBehavior.cs:131-190`) | 1 na wies (577 taborow) | +5 140 partii | bez wozu; towar do magazynu wsi-matki (mamy `VillageCartWholeStore`) |
| Milicja | 796 partii | +5 140 partii | bez milicji (latka) |
| Notable (`DefaultNotableSpawnModel`: 1 Headman + 2 RuralNotable) | 3 na wies | +15 420 bohaterow | 0 (latka modelu) |
| Duchowny BK rangi 1 (`PresetFaith.cs:195-214`) | 1 na wies | +5 140 bohaterow | 0 (latka `GetIdealRank`) |
| Dane BK (`PopulationData`, `BannerKingsConfig.cs:283`) | samo | +5 140 | zostaje albo pomijane |
| Tytul BK (lenno wsi) | tylko z XML tytulow (`TitleGenerator.cs:320-346`) | brak bez dopisania do BKROTPatch | bez tytulu |
| AI lordow: rabunek, obrona, odwiedziny, poscig (`AiMilitaryBehavior.cs:166-174, 244-255`, `AiVisitSettlementBehavior.cs:116, 703-720`, `AiEngagePartyBehavior.cs:46-47`) | przeglad kazdej wrogiej osady | koszt ok. x7 (wsie to 72% przegladanych) | wylaczyc latkami i powiazac ze spustoszeniem okregu |
| Cache odleglosci | 0.57 mln par | ok. 19 mln par na typ (ok. 0.4-0.5 GB pliku, 0.7-1.4 GB RAM) albo liczenie "w locie" z przycieciami | latka modelu odleglosci: do satelity = do matki + linia prosta x 1.1 |
| Tablica "brudnych" obrazkow osad | 2 500 miejsc | crash przy pierwszej klatce mapy | latka: wieksza tablica (ok. 10 linii) |
| Napisy nazw (`SettlementNameplatesVM.cs:238-246`, co klatke, szeregowo) | 1 066 | 6 200 | bez napisow albo tylko z bliska |
| Listy w UI (wsie twierdzy, encyklopedia) | do 4 wsi na twierdze | 25-40 na twierdze, encyklopedia ok. 6 200 pozycji | do sprawdzenia |

**Tablica 2500 [SPRAWDZONE]:** `SettlementVisualManager.cs:51` - `new SettlementVisual[2500]`; kazda brudna osada dopisuje
sie przez `Interlocked.Increment` (`SettlementVisual.cs:721`), a `Settlement.OnSessionStart` brudzi wszystkie naraz. Ponad
2 500 osad = `IndexOutOfRange` na starcie mapy. Bez latki miesci sie x2 (+571 wsi, razem ok. 1 640 osad z kryjowkami)
i x3 (+1 142, ok. 2 210 osad); x4 (+1 713, ok. 2 780) juz nie.

---

## 4. Drogi - porownanie

| Droga | Co Jeff widzi | Doba (dzis 12 s) | Nowa kampania | Praca | Ocena |
|---|---|---|---|---|---|
| **A. Pelne wsie x10** | ok. 5 700 zywych wsi z notablami, wozami, milicja | 25-45 s [SZACUNEK] | TAK | generator + latki (tablica, wyglad, cache) | NIE |
| **B. Satelity x10** | prawdziwe osady: klik, menu, encyklopedia, nazwa, typ, spalony obrazek | 13-19 s (+10-30%) [SZACUNEK] | TAK (albo kruche zalozenie w starym zapisie) | 3-5 paczek, 1-3 tygodnie | tylko jesli trzeba wchodzic do przysiolkow; najpierw x2 |
| **C. Przysiolki-dekoracje** | ikony wsi na mapie, poziom 1/2/3 i spalone wedlug ksiegi | ok. +0 s | NIE | 1 paczka, 2-4 dni z proba | **POLECAM** razem z D |
| **D. Okreg w menu wsi** | "People of the district": osady, ludzie, spalone, uchodzcy, spichlerz | 0 | NIE | 1 paczka (juz zaprojektowana) | **POLECAM, najpierw** |
| E. Obozy jako partie | ikony-partie | +do 4.5 s (+40%) [SZACUNEK] | NIE | 1-2 paczki + latki AI | NIE (rozdz. 6) |
| F. Edytor Modding Kit | jak A albo B | jak A albo B | TAK | 5-15 min na wies = 430-1 300 h; kopia sceny ROT 113 MB przy kazdej aktualizacji ROT | NIE (kit nie jest zainstalowany) |

### 4.1 Droga C - przysiolki-dekoracje (szczegoly)

- **Pozycje:** generator uruchamiany raz (Python na `navmesh.pkl` albo komenda w grze przez publiczne API `MapScene`:
  `GetFaceIndex`, `GetFaceTerrainType`, `GetHeightAtPoint`). Losowanie rownomierne (Poisson-disk) po ladzie okregu:
  rowniny, step, pustynia, tereny wiejskie; bez gor, wody, rzek, brodow, urwisk i mostow; las opcjonalnie (drzew nie
  usuniemy). Odstep od osad i kryjowek min. ok. 6 jedn., miedzy przysiolkami ok. 8-10 jedn. Okreg = obszar najblizszy danej
  wsi. Wynik: plik pozycji w `Armoury\ModuleData`.
- **Liczba ikon wedlug ludzi okregu, nie po rowno:** ok. 1 ikona na 7-8 tys. ludzi (razem ok. 5 700 ikon = x10 sredni).
  Reach (ok. 155 tys.) ok. 20 ikon, okreg-mediana (61 tys.) ok. 8, Nocna Straz (1.7 tys.) 0 dodatkowych. Mapa pokaze wtedy,
  gdzie naprawde mieszkaja ludzie. Gdzie ciasno (98 z 571 okregow nie miesci 10 ikon przy odstepie 6 jedn.: miasta, wyspy),
  stanie mniej ikon - suma ludzi okregu sie nie zmienia.
- **Wyglad:** `GameEntity.Instantiate` jednego z 6 prefabow wsi kultury okregu (albo kopia ikony wsi-matki), wysokosc
  terenu, losowy obrot; usunac kule fizyki "_bo", bo lapie klikniecia myszy.
- **Stan ikony z ksiegi okregu (raz na dobe):** poziom 1/2/3 (`SetUpgradeLevelMask`) wedlug ludzi w domu na ikone;
  "spalona", gdy dany ulamek okregu jest spustoszony (uchodzcy / pulap z paczki 113). Przy rabunku "osada po osadzie"
  ikony najblizej napastnika spalaja sie po kolei - Jeff widzi na mapie, ktore przysiolki poszly z dymem.
- **Zapis:** nic nie trafia do sejwu (pozycje z pliku, stan z ksiegi), wiec dziala na obecnym zapisie i znika bez sladu po
  wylaczeniu.
- **Koszt:** doba ok. +0 s. Mapa: +5 100 ikon; przy pelnych kopiach ok. +56 tys. drobnych encji (dzis 53 550), przy
  uproszczonych (1-2 meshe) duzo mniej. Klatki na mapie [SZACUNEK] 0-15% mniej - do zmierzenia w probie.
- **Ograniczenia:** przysiolka nie da sie kliknac ani do niego wejsc (mozna dodac dymek); brak nazwy na mapie; ikona moze
  wypasc na drodze albo w lesie (drogi mozna omijac, czytajac encje `road_instance` ze sceny).

### 4.2 Droga D - okreg w menu wsi

Juz zaprojektowana: `docs/DEMOGRAFIA-ANEKS-2026-10-05.md` rozdz. 3.3 (a), decyzja Jeffa 07.10 w `STAN-PRAC.md` ("menu wsi
pokazuje okreg: osady, ludzie, spalone, spichlerz"). Opcja w menu `village` / `town` / `castle` przez `AddGameMenuOption`,
okno `ShowInquiry`. Do tekstu z aneksu dochodza: liczba osad okregu i ile z nich spalonych (z rabunku osada po osadzie),
dni spichlerza (`PROJEKT-GLOD-2026-10-07.md`). Ryzyko niskie, koszt doby 0, dziala na obecnym zapisie.

### 4.3 Droga B - satelity (gdyby Jeff chcial wchodzic do przysiolkow)

- Prawdziwe osady z XML (nazwa, kultura i scena wsi-matki, typ wedlug terenu i typu matki, `bound` = twierdza matki).
- Latki: wyglad (jak w C), tablica 2500, notable / duchowny BK / milicja / woz = 0, AI (military, visit, engage, patrol,
  bandyci) pomija satelity, odleglosc liczona przez matke.
- Ludzie i produkcja okregu dzielone w ksiedze; produkcja gry w satelicie albo zbierana do matki, albo zerowana (bez wozu
  magazyn sie zapcha: stop przy 1.5 x W, `Village.cs:248-257`).
- Werbunek zostaje we wsi-matce (tam sa notable) - pasuje do "wies = okreg".
- Nowa kampania. Kolejnosc: proba x2 (+571 satelitow, bez latki tablicy) w autotescie -> pomiar s/dobe i klatek -> dopiero x10.

---

## 5. Wydajnosc [SZACUNEK, do potwierdzenia proba]

**A (pelne x10):** partie +ok. 10 tys. x do 0.9 ms = do +9 s; bohaterowie +20.5 tys. (x5-6 dzisiejszych ok. 4 tys.) =
+2-6 s; tiki osad, BK i nasza ksiega na 5 140 wsi = +1-3 s; AI lordow x7 = +1-4 s; cache: brakujace pary liczone na zadanie
(3 zapytania sciezki na glownym watku, `NavigationCache.cs:107-123`) - przyciecia w pierwszych tygodniach, pamiec do 1-2 GB.
Razem **25-45 s na dobe**, sejw 100-150 MB, dluzsze wczytanie i autozapis.

**B (satelity):** partie i bohaterowie +0; tiki dzienne +0.3-1.5 s; tiki godzinne (24 x 5 140 = ok. 123 tys. wywolan na
dobe) +0.5-2.5 s, mniej gdy nasluchy wychodza od razu dla satelitow. Razem **13-19 s na dobe**. Mapa -5..15% klatek,
encyklopedia osad wolna.

**C (dekoracje):** doba **ok. +0 s**; klatki 0-15% mniej (zalezy od wersji ikony).

**D (menu):** 0.

**Uwaga:** 12 s/dobe jest z FastMode; procentow nie da sie przeniesc 1:1 na inne tempo.

---

## 6. Male obozy / osady jako partie na mapie - ocena

- Partia stoi w tickach kampanii jak kazda inna: gorna granica ok. 0.9-1 ms na partie na dobe, wiec 5 000 obozow = do
  +4.5 s na dobe (+40%).
- AI lordow i band widzi je jako partie: sciga, atakuje albo omija (`AiEngagePartyBehavior`) - trzeba latac.
- Ida do sejwu (+MB), maja tabliczki nazw, liczy je kazdy cudzy mod iterujacy po `MobileParty.All` (BK, StrategicCampaignAI,
  ScoutingFog).
- Wyglad ten sam co dekoracji C, a koszt wiekszy. **Nie polecam jako zamiennika wsi.**
- Sensowne tylko dla rzeczy, ktore sie ruszaja i jest ich malo: np. kolumny uchodzcow w drodze do twierdzy po rabunku
  (kilkadziesiat naraz) - osobny pomysl na pozniej.

---

## 7. Co by sie zepsulo (ryzyka)

1. **Crash mapy** przy ponad 2 500 osadach bez latki tablicy (A, B przy x3 i wiecej). Pewny. [SPRAWDZONE]
2. **Nowa kampania** dla A i B (obiekty spoza zapisu, `Campaign.cs:1438`); stary zapis tylko kruchym zalozeniem jak Player
   Settlement.
3. **+20 tys. bohaterow** (A): dzieci notabli, zadania wsi, AIInfluence, sejw x3-4.
4. **Cache odleglosci** (A): pelna regeneracja to GB i godziny; liczenie w locie to przyciecia. Wies bez sciezki (inna wyspa
   navmesha) dostaje odleglosc 0 (`SandBoxNavigationCache.cs:105`) - AI uzna ja za "tuz obok"; generator musi sprawdzac
   sciezke. Liczenie w locie dopisuje do slownika bez blokady, a ruch partii idzie rownolegle - mozliwy wyscig [PLAUSIBLE].
5. **AI wojny** (A): lordowie i bandy wybieraja nowe wsie do rabunku i odwiedzin (koszt x7, inna wojna). Przy satelitach
   wylaczonych z AI sa "nietykalne" - trzeba je powiazac ze spustoszeniem okregu.
6. **Produkcja gry w satelitach** bez wozu: magazyn sie zapcha (`Village.cs:248-257`).
7. **Interfejs:** listy wsi twierdzy robione pod 4 wsie (bedzie 25-40), encyklopedia ok. 6 200 osad, tabliczki nazw co
   klatke.
8. **Cudze mody** zakladaja ok. 571 wsi: BK (tytuly tylko z XML, petle po `Settlement.All` w EconomicCluster i
   ShippingGraph), BetterEconomy, RealisticBannerlord, ScoutingFog, StrategicCampaignAI. Nikt nie gral na 6 tys. osad -
   nie znam moda tej skali (ROT 1 066 osad to jedna z najwiekszych map).
9. **Aktualizacja ROT** (zmiana id, pozycji, kultur, navmesha) psuje wygenerowany XML albo plik pozycji: potrzebny straznik
   wersji ROT i ponowny przebieg generatora (dotyczy tez C, ale tam skutkiem jest tylko zle polozona ikona).
10. **Dekoracje (C):** kula "_bo" lapie klikniecia - trzeba ja zdjac; ikona na drodze albo w lesie; nieznane zachowanie pod
    mgla ScoutingFog; spadek klatek do zmierzenia.
11. **Licencja ROT** niesprawdzona (Nexus 403). Droga A/B/C przez latke nie kopiuje plikow ROT; dla prywatnej gry bez
    znaczenia.

---

## 8. Rozbieznosci miedzy badaniami

| Sprawa | Badanie 1 | Badanie 2 | Przyjete |
|---|---|---|---|
| Bohaterowie przy x10 | +20 560 (3 notable + duchowny BK) | +15 400 (same notable) | +20.5 tys. - badanie 2 nie liczylo BK |
| Odstep wsi przy x10 | ok. 9.5 jedn. (powierzchnia ladu z navmesha) | ok. 15 jedn. (pole w promieniu 40 od osad, z woda) | ok. 9-10 jedn.; rachunek z navmesha jest scislejszy |
| Teren pod wies | z lasem i brodami | bez brodow, las opcjonalnie | bez brodow, las opcjonalnie |
| Tablica 2500 | crash prawie pewny | nie wspomina | potwierdzone w kodzie (rozdz. 3) |
| Ile miesci sie bez latki tablicy | "x3 satelitow (ok. 1 700 nowych) miesci sie" | - | BLAD w badaniu 1: 1 066 + 1 700 = ok. 2 770 > 2 500; miesci sie x3 lacznie (+1 142 nowych) |
| Praca nad dekoracjami | 1-2 dni | 2-4 dni | 2-4 dni z proba w grze |
| Klatki przy dekoracjach | 0-5% (liczy ikone jak 1 encje) | ok. +56 tys. encji (pelne kopie) | 0-15%, zalezy od wersji ikony - zmierzyc |

---

## 9. Zalecana kolejnosc

1. **Paczka "okreg w menu wsi" (D)** - wedlug aneksu 3.3 (a) plus osady / spalone osady / spichlerz. Juz zdecydowane.
2. **Proba dekoracji (C) na sucho w grze:** 5 000 ikon z generatora, pomiar klatek na mapie, czasu ladowania mapy i doby
   w autotescie. Jesli klatki spadaja ponad 10% - uproscic ikone (1-2 meshe) albo pokazywac przysiolki tylko z bliska.
3. **Paczka "przysiolki na mapie" (C)** - liczba ikon wedlug ludzi okregu, stan z ksiegi i ze spustoszenia, razem z
   rabunkiem osada po osadzie (spalone ikony po kolei).
4. **Satelity (B)** tylko na wyrazne zyczenie Jeffa (wchodzenie do przysiolkow, nazwy w encyklopedii) i dopiero po probie x2
   na nowej kampanii.

**Pytanie do Jeffa (zmienia to, w co gra):** czy wystarczy, ze przysiolki WIDAC na mapie i plona przy rabunku, czy trzeba do
nich WCHODZIC (klikac, menu, nazwa w encyklopedii)? Pierwsze = C + D bez nowej kampanii; drugie = B i nowa kampania.

---

## 10. Zrodla

- Badania (sesja 3cf3e0ac): `scratchpad\dzien-6\wiecej-wsi\spojrzenie-2-mapa` (dekompilacje `dec\`, zrodla Player Settlement
  `playersettlement\`, skrypty `cache.py`, `dens.py`, `ent.py`); `scratchpad\ore-supply\cs` (TaleWorlds.CampaignSystem).
- Sprawdzone przez autora: `dzien-6\autotest\dec\SandBox.View\SandBox.View.Map.Managers\SettlementVisualManager.cs:49-87`,
  `...\SandBox.View.Map.Visuals\SettlementVisual.cs:471, 608, 721`, `ore-supply\cs\TaleWorlds.CampaignSystem\Campaign.cs:1420-1445`.
- Rachunek osad okregu: `scratchpad\dzien-6\rabunek-osady\calc-wynik.txt`.
- Pomiar doby: `Armoury-2026-10-07_09-11-44.log`, CrashScribe `session-2026-10-07_09-11-42.log`.
- Projekty powiazane: `docs/DEMOGRAFIA-ANEKS-2026-10-05.md` rozdz. 3.3, `docs/paczki/113-ludzie-spustoszenie.md`,
  `docs/PROJEKT-GLOD-2026-10-07.md`, `docs/STAN-PRAC.md` (decyzje 07.10).
- Mody: https://github.com/BOTLANNER/BannerlordPlayerSettlement (1.4.8, wariant RoT), Inhabitable-Isles (corbett3289),
  https://github.com/KesslerMan/BannerlordSettlementPatcher (stary), https://docs.bannerlordmodding.com/_tutorials/new_settlements,
  https://catalogue.smods.ru/?p=445538 (Performance Optimizer).
