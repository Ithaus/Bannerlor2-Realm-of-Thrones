# Audyt surowcow: wydobycie wobec ludnosci i potrzeb gospodarki (2026-10-04)

Pytanie Jeffa: "sprawdz, czy surowce - drewno, len, ruda, skory, welna itd. - nie sa produkowane
za malo wzgledem ludnosci i potrzeb gospodarki".

Tylko odczyt - zadnych zmian w kodzie. Liczby oznaczone **(kod)** policzone ze wzorow i danych gry,
**(log)** odczytane z logu, **(SZACUNEK)** to moje przyblizenie tam, gdzie gra nie wypisuje wartosci.
Produkcji wsi gra NIGDZIE nie loguje - dlatego poprawka P1 (diagnostyka) jest pierwsza.

Zrodla: dekompilacja `BannerKings.dll`, `BKROTPatch.dll`, `BetterEconomy.dll`, `TaleWorlds.CampaignSystem.dll`
(scratchpad sesji, katalogi `bk/`, `be/`); `SandBox/ModuleData/spworkshops.xml`,
`BannerKings.Redux/ModuleData/workshops.xml`, `ROT-Map/ModuleData/settlements.xml`; logi
`Armoury-2026-10-04_09-01-00.log` (przed cenami historycznymi i przed blokada "z niczego") i
`Armoury-2026-10-04_13-00-48.log` (po); poprzednie audyty `AUDYT-PRODUKCJI-MODY.md`, `AUDYT-TOWARY.md`.

---

## 0. Odpowiedz w skrocie

1. **Tak, prawie wszystko poza drewnem jest produkowane za malo - ale glowna dziura nie jest po stronie
   wydobycia, tylko po stronie zjadania.** Ludnosc miast (konsumpcja BK) zjada surowce posrednie
   (len surowy, welne, skory surowe, rude) w ilosciach kilkukrotnie wiekszych niz wsie ich daja,
   zanim warsztaty zdaza je przerobic. Len: wsie ok. 110/dzien, ludnosc do ~570/dzien, tkalnie 22/dzien.
2. **Wydobycie wsi nie ma nic wspolnego z ludnoscia 52.6 mln.** BK liczy produkcje od wlasnej ludnosci wsi
   = hearth x 4-6 (ok. 1.1 mln ludzi w 571 wsiach), a PopulationLaw mowi, ze wsie to 42 mln ludzi
   (srednio 191 ludzi na hearth, Reach 454, Polnoc 135). Kazda wies produkuje jak 2 tys. ludzi, choc symbolizuje
   kilkadziesiat-kilkaset tysiecy. Do tego Reach (8 mln) ma na wies tyle samo co Polnoc (3 mln).
3. **Skory surowe nie maja ZADNEGO zrodla we wsiach** (zaden typ wsi ich nie robi) - tylko rzeznie (10 w swiecie)
   i linie uboju `artisans`. A ludnosc zjada je jak chleb (~190/dzien popytu). Garbarnie sa 3 (!), wiec skory
   wyprawionej powstaje ~12 szt./dzien na caly swiat przy popycie samych mieszczan ~40-50.
4. **Za malo warsztatow przetworczych to skutek algorytmu gry**: typ warsztatu wybiera sie wedle wsi wokol
   miasta (zboze -> 77 piekarni), a nie wedle tego, czego brakuje; zmiana typu tylko przy bankructwie.
   Skory nie ma we wsiach -> garbarni prawie nie ma.
5. **Ruda i drewno**: drewno w nadwyzce (zapas rosnie ~+270/dzien), ruda na styk lub w niedoborze -
   i to glownie dlatego, ze po cenach historycznych ruda i drewno kosztuja 1 d, a skladnik budzetu BK
   `dobrobyt/1000/indeks` (w zlocie, NIE przeliczany przez nasz DemandPostfix) zjada ok. **465 rud i 465 drewna
   dziennie** w samych miastach.
6. Wzgledem Anglii ok. 1300 (na glowe) gra produkuje 1:200 (ruda) do 1:2600 (skora wyprawiona) - wszystko
   jest symboliczne, ale **nierowno symboliczne**: najgorzej skora, plotno, skory surowe; najlepiej ruda.

---

## 1. Jak gra liczy wydobycie (kod)

Aktywny model produkcji wsi: `BKROTPatch.Models.BKROTVillageProductionModel` (dziedziczy po
`BannerKings.Models.Vanilla.BKVillageProductionModel`). BEE w trybie zgodnosci z BK - jego model produkcji
wsi NIE dziala (`AUDYT-PRODUKCJI-MODY.md` 3). Towar trafia do wsi przez BK
`EconomyPatches.TickGoodProductionPatch.TickGoodProduction` (`EconomyPatches.cs:1130-1165`), ktory woluje model
dla kazdej pozycji listy `PopulationManager.GetProductions` i robi `OnItemProduced(item, wies, n)`.

Lancuch dla jednej wsi i jednego towaru:

1. **Lista**: `VillageType.Productions` (vanilla `DefaultVillageTypes.cs:141-270`; kazdy typ ma tez zboze 3)
   + BK (`BKVillageTypes`: drwale +miod 2, glina +wapien 8, srebro +marmur 0.8 +zloto 0.2)
   + mineraly (tylko z budynkiem Mines), skora 0.5 x poziom budynku Tannery, narzedzia 0.5 x poziom Blacksmith
   (`PopulationManager.cs:278-303`).
2. **Ilosc** (`BKVillageProductionModel.CalculateDailyProductionAmount`): robotnicy = 0.85 x (pansz. + dzierzawcy)
   (`LandData.cs:90-130`), dzieleni wedle UDZIALU towaru w liscie (waga/suma wag), potem:
   - towar "ogolny" (ruda, len, welna, glina...): **0.0045 szt. na robotnika dziennie** (`AddGeneralProcution`);
   - drewno: min(las x 10, robotnicy) x 0.0012 x 10 = **0.012 szt./robotnika** (futro x5 zamiast x10);
   - zwierzeta: pastwiska x 0.0062 / mieso_ze_zwierzecia; zywnosc: pole x 0.025;
   - mnozniki: perki, budynek DailyProduction +15%, kultura, `ProductionEfficiency` BK (nieznana, ok. 1).
3. **Ludnosc wsi BK**: `PopulationManager.GetDesiredTotalPop` = **hearth x 4..6** (`PopulationManager.cs:478-480`).
4. **Sufit BK** (`BKEconomyLayerInstaller.VillageProductionSoftCapPostfix`): ponad baza_typu x 1.3 liczy sie 20%.
   Przy obecnych hearth produkcja jest PONIZEJ sufitu, wiec sufit nic nie robi.
5. **Nasz MaterialLaw** (`MaterialLaw.cs:ProdPostfix`, Priority.Last - po sufitach BK): ruda x3, drewno x3.
6. BKROT: blogoslawienstwa (Kowal +25%, Starzy Bogowie +50% dla drwali). Zima: `WinterBite` (teraz lato).

Hearth z `settlements.xml` (start gry): 571 wsi, suma 219 819, srednio 385.
Robotnicy na hearth (SZACUNEK): 5 ludzi x 0.85 (pansz./dzierz.) x 0.85 (sila robocza) = **3.6**.

### Wydobycie dzienne swiata (kod + SZACUNEK robotnikow)

| Surowiec | Wsie | Udzial w liscie | Na wies/dzien | Swiat/dzien | Uwagi |
|---|---|---|---|---|---|
| Ruda (`iron`) | iron_mine 26 | 10/13 | 4.8 x3 = 14.5 | **~380** | + BK warsztat `mines` 8 x 1 szt. (bez wsadu!) + budynki Mines w miastach (nieznane) |
| Drewno (`hardwood`) | lumberjack 45 | 18/23 | 12.0 x3 = 36 | **~1 620** | |
| Len surowy (`flax`) | flax_plant 19 | 18/21 | 5.8 | **~110** | vanilla dalaby 18/wies = 342 |
| Welna (`wool`) | sheep_farm 23 | 10/21 | 3.2 | **~75** | vanilla 230 |
| Futro (`fur`) | trapper 23 | 1.4/4.4 | 1.9 | ~45 | |
| Skory surowe (`hides`) | **zadna** | - | 0 | **0 ze wsi** | tylko ubój w miescie (ponizej) |
| Skora (`leather`) | wsie z budynkiem Tannery | 0.5 x poziom | ? | maly ulamek | |

Czyli BK daje ok. **1/3 bazy vanilla** dla lnu i welny (vanilla mnozy baze przez poziom hearth, BK przez
robotnikow x 0.0045). Ruda i drewno wychodza blisko bazy vanilla tylko dzieki naszemu x3.

### Skory surowe - jedyne zrodla
- rzeznia BK `butcher` (10 w swiecie): krowa -> 10 miesa + 5 skor (1/dzien), owca -> 2+2 (2/dzien), swinia -> 4+2 (2/dzien);
- `artisans` (97 miast): krowa -> 6 miesa + 2 skory (1/dzien), owca -> 1+1+1 welna (2/dzien), swinia -> 2+1 (4/dzien);
- wsad: zywe krowy/owce/swinie z wsi (krowy 2/dzien na cattle_farm, wheat_farm 0.2/0.4/0.8 - wedle pastwisk BK).
- Nieznana dokladna ilosc (zalezy od podazy zwierzat). Zapas skor stoi w miejscu (227 -> 190 -> 221 -> 262, log),
  czyli doplyw ~ rowna sie temu, co zjadaja mieszczanie (~200/dzien, patrz 2.1).
- Wczesniej `artisans` robil 0.6 skory/dzien/miasto z niczego (58/dzien) - zablokowane wpisem w CHANGELOG (WorkshopLaw.FreeRawLine).

---

## 2. Kto zjada (kod)

### 2.1 Ludnosc miast - BK `EconomyPatches.ItemConsumptionPatch` (`EconomyPatches.cs:579-711`)

Dla kazdego wiersza polki, ktorego kategoria ma popyt: budzet = `popyt x indeks^0.15 + dobrobyt/1000/indeks`
(`CalculateBudget :1273-1288`), zdejmuje `budzet/cena` sztuk (towar znika, miasto dostaje zloto).
Popyt w zlocie z vanilla `DefaultSettlementEconomyModel.GetDailyDemandForCategory` = `Base x dobrobyt + Lux x max(0, dobrobyt-3000)`
(`DefaultItemCategories.cs:358-375`: len 10/20, welna 12/0, skory 17/10, skora 15/10, plotno 28/30, ruda 10/20, drewno 10/10; x0.001).

97 miast, suma dobrobytu 464 700 (XML startu), nadwyzka ponad 3000: 178 700. Wynik przy indeksie ~1
i popycie przeliczonym przez nasz `HistoricalPrices.DemandPostfix` (wpis 44 - sztuki jak przed zmiana cen):

| Kategoria | Popyt w zlocie/dzien | Sztuk ze skladnika "popyt" | Sztuk ze skladnika BK `dobrobyt/1000/indeks` (cena nowa) | Razem szt./dzien |
|---|---|---|---|---|
| len surowy (cena 15-20) | 8 221 | 548 | 23 | **~570** |
| welna (22) | 5 576 | 253 | 21 | **~275** |
| skory surowe (50 -> 10) | 9 687 | 194 | 46 | **~240** |
| skora (230 -> 40) | 8 758 | 38 | 12 | **~50** |
| plotno lniane (245 -> 100) | 18 373 | 75 | 5 | **~80** |
| ruda (50 -> 1) | 8 221 | 164 | **465** | **~630** |
| drewno (25 -> 1) | 6 434 | 257 | **465** | **~720** |

To sa sufity - w miescie bez towaru nic nie znika. Ale kazda sztuka, ktora dojedzie, jest zjadana
**zanim** tkalnia/garbarnia ja wezmie (ten sam dzienny tick).

Wazne: skladnik `dobrobyt/1000/indeks` jest w ZLOCIE i nasz DemandPostfix go nie widzi (jest dodawany w
`CalculateBudget`, nie w `GetDailyDemandForCategory`). Przy rudzie i drewnie po 1 d robi z tego ~4.8 szt. na miasto
dziennie **na kazdy wiersz kategorii** (ruda + 6 sztabek, drewno + wegiel). W sesji 13:00 (bez DemandPostfix)
popyt na rude w sztukach byl nawet ~50x wiekszy - stad skoki rudy 595 -> 335 -> 892 -> 458.

### 2.2 Warsztaty z towarem (vanilla `WorkshopsCampaignBehavior.RunTownWorkshop :1387-1420`)
Postep = predkosc linii dziennie; cykl bierze 1 szt. wsadu (x input_count).

| Warsztat (log 13:00) | Ile | Wsad/dzien | Wyrob/dzien |
|---|---|---|---|
| linen_weavery | 11 | len 2 kazda = **22** | plotno 4 = **44** |
| wool_weavery | 23 | welna 1 (szaty) + 2 (filc) = **69** | szaty 46, filc 92 |
| tannery | **3** | skory 2 = **6** | skora 4 = **12** |
| butcher (BK) | 10 | zwierzeta | skory do ~13 kazda |
| smithy (linia narzedzi) | 20 | ruda 1.5 = **30** | narzedzia 120 |
| wood_WorkshopType (deski) | 31 | drewno 2 = **62** | deski 62 |
| mines (BK) | 8 | **brak wsadu** | ruda 8 (z niczego - FreeRawLine dotyczy tylko `artisans`) |

Linie z uzbrojeniem (barding-smithy z lnem/skorami, armorsmithy ze skorami itd.) przechwytuje nasz
`WorkshopLaw.CyclePrefix` - bierze rude, drewno, skore i plotno wedle `WorkshopLaw.Needs` (`WorkshopLaw.cs:85-96`):
ruda = kg metalu / 1.5 x 1.25^stopien, drewno = 5 na rude + 0.25 na kg metalu, skora/len = kg/10.
Przyklady: przeszywanica ~0.3 plotna; kurta skorzana ~0.4 skory; **kolczuga t3 10 kg: 10.4 rudy + 54 drewna**;
luk: 0.1 drewna + odrobina plotna. W sesji 09:00 warsztaty robily ~150 korpusow dziennie - kilkadziesiat kolczug
dziennie zjadloby cala rude swiata. W sesji 13:00 robily tylko luki (patrz wpis 44 CHANGELOG).

### 2.3 Partie BK (`PartySupplies.cs`, `BKPartyNeedsModel`)
Tkanina (welna/plotno/len) 0.01 i drewno 0.02 na zolnierza dziennie (x0.5 PartySuppliesFactor, zuzycie x2).
Wojsko lordow 14 934 ludzi (CrashScribe economy CSV, dzien 108836) -> do ~150 tkaniny i ~300 drewna dziennie,
tylko partie z kwatermistrzem w osadzie (realnie mniej).

### 2.4 Kuznia gracza, budowy BK, BE Armory
Kuznia gracza - pomijalna. Budowy BK (drewno, ruda, glina, narzedzia) - nieznane, brak logu.
BE: pobor wsadu bez wyrobu juz wylaczony (`WorkshopDailyInputDrawPerShop` 0), kopia produkcji wsi wylaczona (0).
BE wtorna produkcja wsi (hides/linen/tools) - wymaga 30 dni stabilnosci i hearth >= 650; na starcie zero.

---

## 3. Bilans dzienny swiata

| Surowiec | Wydobycie/doplyw | Zjada ludnosc (sufit) | Warsztaty + WorkshopLaw + partie | Bilans | Log (zapasy na targach) |
|---|---|---|---|---|---|
| **Len surowy** | ~110 | ~570 | 22 + do ~50 (partie) | **-450 do -500** | 1650 -> 1052 -> 522 -> 188 (09:00); 1586 -> 807 -> 569 -> 295 (13:00) - zgodne |
| **Welna** | ~75 (+ owce w uboju) | ~275 | 69 + partie | **-250** | 385 -> 373 -> 378 -> 245 (09:00); 387 -> 243 -> 337 -> 157 (13:00) |
| **Skory surowe** | ~200 (ubój, SZACUNEK z zapasu) | ~240 | 6 (garbarnie) | ~0, ale **97% idzie do zjedzenia, 3% do garbarni** | 370 -> 438 (09:00, z artisans); 227 -> 262 (13:00) |
| **Skora** | ~12 (3 garbarnie) | ~50 | WorkshopLaw (pancerze skorzane, tarcze) | **-40 i wiecej** | 103 -> 73 (09:00, +19/dzien z niczego); 17 -> 7 -> 10 (13:00) - dno |
| **Plotno lniane** | ~44 (o ile tkalnie maja len!) | ~80 | WorkshopLaw (przeszywanice, cieciwy) + partie | **-40 i wiecej** | 289 -> 247 (09:00, +48/dzien z niczego); 44 -> 29 -> 36 -> 35 (13:00) - dno |
| **Ruda** | ~380 + 8 (`mines`) + budynki Mines | ~630 (w tym 465 ze skladnika BK w zlocie) | 30 (narzedzia) + WorkshopLaw (kolczugi ~10 rudy/szt.) | **ujemny**, maskowany dostawami partiami | 1327 -> 1077 (09:00); 595 -> 335 -> 892 -> 458 (13:00) |
| **Drewno** | ~1 620 | ~720 | 62 + do ~300 + wegiel WorkshopLaw (5 drewna/rude) | **+300 do +500** | 4701 -> 5678 (09:00); 3116 -> 3915 (13:00) - rosnie |

Zgodnosc z logiem jest dobra dla lnu (najlepiej widoczny przypadek), skory, plotna i drewna. Dla rudy
i welny log jest szumny (wiesniacy przywoza towar partiami co kilka dni).

**Lancuch szkody**: len zjadaja mieszczanie -> tkalnie (11) stoja -> plotna brak -> WorkshopLaw nie robi
przeszywanic -> zostaja luki. Tak samo: skory zjadaja mieszczanie -> 3 garbarnie stoja -> brak skory.

---

## 4. Wzorzec historyczny: Anglia ok. 1300

Anglia ok. 1290-1300: ok. 4.75 mln ludzi (Broadberry i in. - ESTYMACJA, zakres 4.5-6). Zuzycie na glowe
rocznie - **wszystko ESTYMACJE z literatury przedmiotu, rzad wielkosci**:

| Surowiec | kg/glowe/rok | Z czego to wynika |
|---|---|---|
| Welna surowa | ~2 (w tym eksport ~1.2; w kraju ~0.7) | eksport 30-45 tys. workow po 166 kg; 10-12 mln owiec po ~0.7 kg runa |
| Skory surowe (bydlo, owce, swinie) | ~3 | ubój ~15% stada bydla (skora 25-30 kg) + skorki owcze |
| Skora wyprawiona | ~1 | buty (2 pary/rok), pasy, uprzaz; ~1/3 wagi skory surowej |
| Len/konopie (wlokno) | ~1 (zakres 0.5-1.5) | bielizna, worki, plotno; czesc importu |
| Plotno lniane | ~0.7 | |
| Zelazo | **0.3-0.4** (kraj ~1 000 t + import hiszpanski/szwedzki) | 1-2 kg/glowe to poziom XVI-XVII w., nie 1300 |
| Ruda zelaza | ~2-3 | wydajnosc dymarki 10-20% (nasz model: 15%) |
| Drewno: wegiel do zelaza + budowa | ~50 | 25-40 kg drewna na kg zelaza + budulec |
| Drewno opalowe | 1 000-1 800 | Londyn ok. 1300 ~1.7 t/glowe (ESTYMACJA) - gra tego nie modeluje |

Przeskalowanie na swiat gry: 52.6 mln ludzi, **rok Armoury = 168 dni** (`Calendar.cs`, `WeeksPerSeason = 6`),
jednostka towaru = 10 kg. Potrzeba dzienna = 52.6 mln x kg / 168 / 10.

| Surowiec | Potrzeba historyczna (szt./dzien) | Gra wydobywa | Gra : historia |
|---|---|---|---|
| Ruda (2.3 kg) | ~72 000 | ~380 | **1 : 190** |
| Len (1 kg) | ~31 000 | ~110 | 1 : 285 |
| Welna kraj (0.7 kg) | ~22 000 | ~75 | 1 : 290 |
| Skory surowe (3 kg) | ~94 000 | ~200 | 1 : 470 |
| Plotno (0.7 kg) | ~22 000 | ~44 | 1 : 500 |
| Drewno bez opalu (50 kg) | ~1.56 mln | ~1 620 | 1 : 960 |
| **Skora wyprawiona (1 kg)** | ~31 000 | **~12** | **1 : 2 600** |

Wniosek: w grze wszystko jest symbolem (wojsko lordow ~15 tys. wobec historycznie mozliwych ~300 tys. przy 52.6 mln
to ok. 1:20; towary sa jeszcze 10-100x bardziej symboliczne). Bezwzglednych liczb nie da sie i nie trzeba
dogonic - ale **proporcje miedzy towarami powinny byc jak w historii**. Biorac rude (1:190) za wzorzec:
skora wyprawiona 14x za malo, plotno 2.6x, skory surowe 2.5x, welna i len ~1.5x, drewno bez opalu 5x
(ale drewno i tak w nadwyzce, bo nikt w grze nie spala go jako opalu na skale historyczna).

Druga miara - potrzeby samego wojska gry (SZACUNEK): 15 tys. zolnierzy x (plotno 1.5 kg, skora 1 kg, welna 2 kg,
zelazo ~1 kg zuzycia) rocznie / 168 dni = plotno ~13, skora ~9, welna ~18, ruda ~60 szt./dzien. Skora (~12/dzien
z garbarni) nie starcza nawet na samo wojsko, a mieszczanie zjadaja ~50.

---

## 5. Co jest za malo, co za duzo i dlaczego

**Za malo (od najgorszego):**
1. **Skora wyprawiona** - 3 garbarnie x 4 szt./dzien. Przyczyny: (a) brak skor ze wsi, wiec algorytm startu
   (`DecideBestWorkshopType` / `FindTotalInputDensityScore`, `WorkshopsCampaignBehavior.cs:1116-1339`) prawie nie stawia
   garbarni; (b) wymiana typu warsztatu tylko przy bankructwie i wedle TANICH wsadow, nigdy wedle drogiego wyrobu;
   (c) mieszczanie zjadaja ~97% skor surowych; (d) nasza blokada "z niczego" slusznie zabrala 19 szt./dzien z powietrza.
2. **Plotno lniane** - 11 tkalni x 4; tkalnie stoja, bo len zjadaja mieszczanie. Blokada zabrala 48/dzien z powietrza.
3. **Len surowy** - wsie ~110 (1/3 vanilli przez wzor BK 0.0045/robotnika), ludnosc ~570.
4. **Welna** - wsie ~75, ludnosc ~275 + 23 tkalnie welny 69.
5. **Skory surowe** - zero ze wsi; tylko ubój zwierzat w miastach.
6. **Ruda** - wydobycie ~380 (z x3), ale ludnosc przy cenie 1 d zjada ~630 (skladnik BK w zlocie) - niedobor
   wynika glownie z konsumpcji, nie z kopaln. Kolczugi WorkshopLaw (10 rudy/szt.) dopiero dojda.

**Za duzo:**
- **Drewno** - ~1 620/dzien przy zuzyciu ~1 100-1 300; zapas rosnie. x3 dla drwali jest za mocne,
  dopoki nie wroci wytop wegla dla kolczug (5 drewna na rude) - wtedy moze sie wyrownac.
- **Zboze -> piekarnie**: 77 piekarni wobec 3 garbarni to objaw tego samego algorytmu (nie surowiec, ale
  zajmuja sloty warsztatow, ktore moglyby przerabiac skory i len).

**Dlaczego - struktura:**
- **Produkcja wsi nie jest zwiazana z ludnoscia krainy** (PopulationLaw): BK = hearth x 5 ludzi x 0.0045,
  a kraina ma 135-454 (Summer 5 075) ludzi na hearth. Reach i Polnoc produkuja tyle samo na wies.
- **Ludnosc konsumuje polprodukty** jak chleb (len, welna, skory, ruda) - ten sam tick, przed warsztatami.
- **Skladnik budzetu BK `dobrobyt/1000/indeks` w zlocie** nie jest przeliczany na nowa monete (ruda/drewno po 1 d).
- **Za malo warsztatow przetworczych**, bo typ wybiera sie wedle wsi wokol miasta i nie reaguje na braki.
- **Nasze blokady** (`WorkshopNoFreeRaw`) sa sluszne (koniec towaru z niczego), ale odslonily, ze bez nich
  skory/skory wyprawionej/plotna prawie nie ma. Jedna dziura zostala: BK warsztat `mines` (8 rud/dzien bez wsadu).

---

## 6. Poprawki (6, kazda osobno do testu - zasada "jedna zmiana naraz")

### P1. Dzienny bilans surowcow w logu (bez zmiany zachowania) - NAJPIERW
Bez tego wszystkie liczby wydobycia w tym audycie sa szacunkiem. Linia raz dziennie dla iron, hardwood, flax,
wool, hides, leather, linen, fur: **wydobyto** (zdarzenie `CampaignEvents.OnItemProducedEvent`, osada = wies -
woluje je BK `TickGoodProduction`), **zjadla ludnosc** (postfix na `Town.SetSoldItems` - BK podaje tam `saleLog`
z konsumpcji), **wsad warsztatow** (`CampaignEvents.OnItemConsumedEvent` w miastach), **WorkshopLaw** (suma `take[]`
w `CyclePrefix`). Miejsce: `Armoury/src/MaterialLaw.cs` (nowa metoda + rejestracja zdarzen w `ArmouryBehavior`),
wypis obok "Rynek surowcow". Ryzyko: male (tylko liczniki).

### P2. Ludnosc nie zjada surowcow posrednich przed warsztatami
(a) Postfix na BK `BannerKings.Patches.EconomyPatches+ItemConsumptionPatch.CalculateBudget` (private static,
`EconomyPatches.cs:1273`): skladnik `dobrobyt/1000/indeks` dzielic przez ten sam wspolczynnik kategorii co
`HistoricalPrices.DemandPostfix` (`_catRatio`) - koniec ~465 rud i ~465 drewna dziennie.
(b) W `HistoricalPrices.DemandPostfix` (`HistoricalPrices.cs:198-205`) dla flax, wool, hides, iron mnoznik
`RawHouseholdShare` (MCM, np. 0.2 - przedzanie w domu bylo historyczne, ale to mniejszosc wobec warsztatow);
skora, plotno, drewno (opal) bez zmian.
Efekt (kod): len -450/dzien mniej zjadania, welna -200, skory -190, ruda -600. Ryzyko: srednie - spadnie zloto
miast z konsumpcji (BK placi miastu `cena x sztuki`), zmieni sie indeks cen; sprawdzic `Rynek surowcow` i `Ludnosc: renty`.

### P3. Skory ze stad we wsiach (nie z niczego - z tej samej pracy)
Przy starcie sesji `VillageType.AddProductions` (jak robi BK `BKVillageTypes.Initialize`) dodac `hides`:
cattle_farm 2, sheep_farm 1.5 (skorki), swine_farm 1, rancza koni 0.5, wheat_farm 0.3. W modelu BK produkcja
jest dzielona wedle UDZIALU, wiec skory zabieraja czesc pracy innym towarom tej wsi (welna -7%, maslo/ser ~-12%;
zwierzeta bez zmian - licza sie z pastwisk) - nic nie powstaje z powietrza. Uwaga: skory wejda wzorem "ogolnym"
(0.0045 x robotnicy x udzial), czyli ~0.8 szt./dzien na cattle_farm - przy tej skali wagi trzeba bedzie podniesc
albo dac skorom mnoznik w P5 (kalibracja z P1). Miejsce: `MaterialLaw.Apply` (raz na sesje, sprawdzic, czy pozycja juz jest).
Dodatkowo `DecideBestWorkshopType` juz liczy krowy jako skory - wiecej skor w okolicy = wiecej garbarni przy
nastepnych bankructwach. Ryzyko: male. Sprawdzic: P1 - `hides` wydobyto > 0.

### P4. Notable przestawia warsztat tam, gdzie brakuje wyrobu (garbarnie, tkalnie)
(a) Postfix na `WorkshopsCampaignBehavior.FindTotalInputDensityScore` (private, `:1116`): dodac skladnik
"brak wyrobu" = suma `max(0, cenaWzgledna(wyrob) - 1)` dla wyjsc-towarow (lustro istniejacego skladnika
"tani wsad"). (b) Raz w tygodniu w kazdym miescie: najmniej dochodowy warsztat notabla (np. trzecia piekarnia),
jesli inny typ ma wynik > 1.5x, przechodzi na niego przez `ChangeProductionTypeOfWorkshopAction.Apply(workshop, typ)`
- koszt przestawienia placi warsztat (`GetConvertProductionCost`), najwyzej 1 zmiana na miasto na 4 tygodnie.
Miejsce: `WorkshopLaw.cs` (juz latamy `WorkshopsCampaignBehavior`). Ryzyko: srednie - przestawia gospodarke miast;
limit i log kazdej zmiany. Robic PO P2 i P3 (inaczej garbarnie nie beda mialy skor).

### P5. Wydobycie wedle ludnosci krainy (zamiast stalego x3)
W `MaterialLaw.ProdPostfix` mnoznik dla iron, hardwood, flax, wool, hides, fur:
`m = globalny(towar) x clamp(k_kultury / 191, 0.5, 2.5)`, gdzie `k_kultury` = ludzie na hearth z
`PopulationLaw` (`_k[kultura][0]`, potrzebny akcesor), 191 = srednia swiata (42 mln ludzi wsi / 219 819 hearth).
Reach x2.4, Westerlands x1.5, Polnoc x0.7, Summer (5 075/hearth) obciete do 2.5. Globalne startowo:
ruda 3, drewno 2 (dzis nadwyzka), len/welna/skory 2. Spalona wies ma mniej hearth - mniej towaru (juz tak jest).
Ryzyko: srednie - zmienia proporcje regionow; najpierw P1, potem kalibracja z logu.

### P6. Ostatnia dziura "z niczego": BK warsztat `mines`
`WorkshopLaw.FreeRawLine` dotyczy tylko `artisans`; BK `mines` (8 w swiecie) robi 1 rude dziennie bez wsadu
(`workshops.xml`: `mines`, linia `- -> iron`). Albo dopisac `mines` do blokady, albo (lepiej, historycznie)
zamienic na prace: ruda tylko z mineralow osady (`MineralData`) i za place. Miejsce: `WorkshopLaw.cs:292-305`.
Ryzyko: male (8 rud/dzien), ale porzadek "nic z niczego".

Kolejnosc: P1 -> P2 -> P3 -> P4 -> P5 (P6 dowolnie). Po P2 sprawdzic, czy len i welna przestaja topniec;
po P3 - czy skory rosna; po P4 - czy liczba garbarni/tkalni w "Rynek surowcow" rosnie.

---

## 7. Czego nie wiem (uczciwie)

- **Dokladnego wydobycia wsi** - gra go nie loguje. Liczby z p. 1 zakladaja 5 ludzi na hearth (BK losuje 4-6),
  85% panszczyznianych/dzierzawcow i `ProductionEfficiency` ~1. Moga byc +-40%. P1 to rozstrzygnie.
- Dobrobytu miast w grze - wzialem XML startu (srednio 4 790); BK go potem zmienia.
- Doplywu zwierzat do rzezni (wiec doplywu skor surowych) - oszacowany z tego, ze zapas skor stoi.
- Zuzycia budow BK i budynkow Mines w miastach - brak logu.
- Kolejnosci w dziennym ticku miasta (konsumpcja BK vs warsztaty) - obie sa sluchaczami `DailyTickTownEvent`;
  niezaleznie od kolejnosci przy zapasie 2-3 szt. lnu na miasto jedna strona wybiera wszystko.
- Dane historyczne w p. 4 to estymacje z literatury (rzad wielkosci), nie twarde zrodla jak w `CENY-HISTORYCZNE.md`.
