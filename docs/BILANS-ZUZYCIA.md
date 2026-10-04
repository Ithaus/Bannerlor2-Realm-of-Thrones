# Bilans zuzycia surowcow przez ludnosc miast (2026-10-04)

Pytanie Jeffa: "czy to ma sens, ze ludnosc miast zjada tyle (len ~570/dzien, welna ~275, skory ~240,
skora ~50, plotno ~80, ruda ~630, drewno ~720 na 97 miast) - sprawdz i zbuduj bilans, ktory ma sens".

Tylko odczyt - zadnych zmian w kodzie. Oznaczenia: **(kod)** sprawdzone w zdekompilowanym kodzie,
**(log)** z `Armoury-2026-10-04_14-05-27.log`, **(zrodlo)** z literatury (lista na koncu),
**(SZACUNEK)** moje przyblizenie.

---

## 0. Odpowiedz w skrocie

1. **Nie, to nie ma sensu.** Ludnosc miast zjada surowce POSREDNIE (len, welna, skory surowe, ruda), ktorych
   gospodarstwa domowe w XIV w. prawie nie kupowaly - kupowali je rzemieslnicy (tkacze, garbarze, kowale).
   Mieszczanin kupowal **wyroby**: sukno, plotno, buty, gwozdzie i noze, opal.
2. **Skala sie nie zgadza.** Wydobycie wsi jest symbolem 1:200-1:2600 historii (AUDYT-SUROWCE 4), a zjadanie
   przez miasta jest na glowe tylko 1:10-1:100 historii. Konsumpcja jest 5-20x "realistyczniejsza" niz produkcja,
   wiec zjada wszystko, zanim dojdzie do warsztatu.
3. **Ruda i drewno ~465/dzien kazde to blad przeliczenia monety**: skladnik BK `dobrobyt/1000/indeks` jest w
   zlocie i nie przechodzi przez nasz `HistDemandScaling` - przy cenie 1 d to 4.8 szt. na miasto dziennie
   na kazdy wiersz polki.
4. **Proponowany bilans**: miasto zjada tylko to, co historycznie szlo do gospodarstw: ruda 0, skory surowe ~4,
   welna ~5, len ~11, skora ~24, plotno ~29, drewno ~430 (opal i budulec) szt./dzien na swiat. Reszta zostaje
   na targu dla warsztatow. Zmiana: dwie latki (sekcja 4), kazda osobno do testu.

---

## 1. Jak to dziala w kodzie (kod)

### 1.1 Kto liczy zjadanie
- Vanilla `ItemConsumptionBehavior.DailyTickTown -> MakeConsumptionInTown` (raz dziennie na miasto):
  `UpdateSupplyAndDemand`, `UpdateDemandShift` (buduje slownik `categoryDemand[kat] = GetDailyDemandForCategory(town, kat)`,
  plus przesuniecia zamiennikow), `MakeConsumption`, `UpdateSellLog`, `UpdateTownGold`.
- **BK zastepuje `MakeConsumption`** prefiksem `BannerKings.Patches.EconomyPatches+ItemConsumptionPatch.Prefix(Town, Dictionary<ItemCategory,float> categoryDemand, Dictionary<ItemCategory,int> saleLog)`
  (atrybut `HarmonyPatch("MakeConsumption")`, zwraca false, gdy osada ma dane ludnosci BK - czyli zawsze).
  Drugi prefiks BK w tej klasie wylacza `DeleteOverproducedItems`.
- Aktywny model ekonomii osady to **vanilla `DefaultSettlementEconomyModel`** (BK nie rejestruje `BKEconomyModel`,
  gdy widzi BEE; BEE nie rejestruje swojego, gdy widzi BK - AUDYT-EKONOMIA-KROLESTW Z2). Nasz
  `HistoricalPrices.DemandPostfix` wpiety w 3 modelach (log: "wpiety w 3 modelach").

### 1.2 Wzor (na miasto, na dzien, na kazdy WIERSZ polki danej kategorii)
1. Popyt w zlocie (vanilla `GetDailyDemandForCategory`, extraProsperity = 0):
   `popyt = Base x dobrobyt + Lux x max(0, dobrobyt - 3000)`, Base/Lux z `TaleWorlds.Core.DefaultItemCategories.InitializeAll`
   (x0.001): len 10/20, plotno 28/30, welna 12/0, drewno 10/10, ruda 10/20, skory 17/10, narzedzia 30/30, skora 15/10, mieso 30/50.
2. Nasz `HistoricalPrices.DemandPostfix`: `popyt /= r(kat)` (log): ruda /57.1, drewno /15, skora /5.8, plotno /2.5,
   skory /5, len /0.8 (czyli x1.25, bo len PODROZAL 15 -> 20), welna bez zmiany.
3. BK `EconomyPatches.CalculateBudget(town, demand, kat)` (private static w klasie zewnetrznej):
   `budzet = demand x indeks^e + dobrobyt / 1000 / indeks`, e = 0.15 (podatek standard; 0.1 wysoki, 0.2 niski).
   **Drugi skladnik jest w zlocie, bez dzielenia przez r(kat)** - i dodawany od nowa dla KAZDEGO wiersza polki
   (kazdy przedmiot i kazdy modyfikator jakosci tej kategorii to osobny wiersz i osobne wywolanie).
4. Sztuk = `budzet / cena` (cena targu `GetPrice`), nie wiecej niz na polce; `RoundRandomized`;
   `categoryDemand[kat] = budzet - sztuk x cena` (reszta budzetu przechodzi na kolejny wiersz).
5. Brak towaru -> zadowolenie BK z danego typu konsumpcji -0.0015; towar byl -> +0.001.

### 1.3 Kto placi
`town.ChangeGold(sztuk x cena)` - **miasto dostaje zloto za towar, ktory znika**. To pieniadz "mieszczan"
z zewnatrz ukladu (nikt go nie traci). Potem vanilla `UpdateTownGold`: `GetTownGoldChange = 0.25 x (10 000 + 12 x dobrobyt - kasa)`
- czyli nadwyzka i tak jest scinana o 25% dziennie. Zjadanie to wiec: **towar znika (prawdziwe zuzycie), a miasto dostaje
monete z niczego** (drugie, obok dosypki do celu, zrodlo monety miast). Przy obecnych cenach to ok. 30 tys. zl/dzien
na swiat dla 7 surowcow (SZACUNEK z tabeli nizej) - male wobec rent (~0.36 mln/dzien).

### 1.4 Ile wychodzi (kod, 97 miast, suma dobrobytu 464 700, nadwyzka ponad 3000: 178 700, indeks ~1)

| Kategoria | Cena | Popyt zl/dzien (po r) | Szt. "popyt" | Szt. BK `dobrobyt/1000` | Razem swiat | Na miasto |
|---|---|---|---|---|---|---|
| len (`flax`) | 20 | 8 221 x1.25 = 10 276 | 514 | 23 | **~537** | 5.5 |
| welna (`wool`) | 22 | 5 576 | 253 | 21 | **~274** | 2.8 |
| skory (`hides`) | 10 | 9 687 /5 = 1 937 | 194 | 46 | **~240** | 2.5 |
| skora (`leather`) | 40 | 8 758 /5.8 = 1 510 | 38 | 12 | **~50** | 0.5 |
| plotno (`linen`) | 100 | 18 373 /2.5 = 7 349 | 73 | 5 | **~78** | 0.8 |
| ruda (`iron`) | 1 | 8 221 /57.1 = 144 | 144 | **465** | **~609** | 6.3 |
| drewno (`hardwood`) | 1 | 6 434 /15 = 429 | 429 | **465** | **~894** | 9.2 |

Drobne roznice wobec AUDYT-SUROWCE (len 570, drewno 720) - tam len liczony po 15-20 d, drewno po r = 25;
log mowi r(hardwood) = 15 (srednia z drewna 25 i wegla 9). To sufity: bez towaru na polce nic nie znika.

Uwaga dodatkowa (log 14:05): w tej sesji skory surowe na targach **rosna** (1033 -> 1184 -> 1261), ruda tez
(1231 -> 1721 -> 1949) - 20 rzezni i 10 garbarni, wiec stan swiata jest juz inny niz w audycie 13:00
(3 garbarnie). Len (206-280), welna (~200), skora (~20) i plotno (~20) dalej przy dnie.

---

## 2. Wzorzec historyczny ok. 1300-1400

### 2.1 Ludnosc, ktora "zjada" w grze
`PopulationLaw` (log 14:07:58): swiat 52.6 mln, udzialy miast z tabeli krain -> **mieszczan ok. 10.5 mln**
(20%; Westeros ok. 2.6 mln, Essos ok. 7.9 mln), czyli srednio **~108 tys. ludzi na miasto** gry
(k: Reach 26/dobrobyt, Volantis 64, Qarth 80, Polnoc 5 ludzi na punkt dobrobytu). Miasto i wies to symbole krain.

### 2.2 Zuzycie na glowe rocznie (Anglia i Europa Zach. ok. 1300; rzad wielkosci)

| Dobro | kg/glowe/rok | Kto kupowal SUROWIEC | Gospodarstwa (miasto) | Zrodlo / pewnosc |
|---|---|---|---|---|
| Welna surowa | ~1.9 calosc, ~0.7 kraj (reszta eksport) | sukiennicy: przedzenie nakladcze, tkacze, folusze | sukno ~1.2 jard kw./glowe srednio (robotnik 1, zamozny chlop 2, pan 8) - ok. 1-2.5 kg welny w wyrobie; **surowej welny ~5%** | Broadberry i in.: 15 mln owiec w 1300, welna 19.8 mln funtow (~9 tys. t), 1.18 jarda kw. sukna na glowe (zrodlo); udzial 5% SZACUNEK |
| Len (wlokno) | ~0.5-1 | tkacze plotna; przedzenie czesciowo domowe (glownie na wsi) | koszule, posciel, obrusy: ~0.3-0.6 kg plotna; **surowego lnu ~10%** | koszula = 2-3 lokcie plotna (zrodlo); reszta SZACUNEK |
| Skory surowe | ~0.6 (sucha waga; ~2-3 mokra) | **wylacznie garbarze i irchiarze** (garbowanie ~rok) | **~0-2%** | skory 5.9 mln funtow w 1300 (~2.7 tys. t) wg Broadberry i in. (zrodlo) |
| Skora wyprawiona | ~0.5-1 | szewcy (najwiecej), siodlarze, rymarze, pasiarze | buty 1-2 pary/rok, pasy, sakwy: **~60% skory to wyroby dla domow** | SZACUNEK (Clarkson: obuwie glownym odbiorca skory) |
| Zelazo | ~0.25-0.4 (1 000 t kraju + import) | kowale, gwozdziarze, platnerze | kupuja gwozdzie, noze, narzedzia, podkowy - **rudy 0%** | 1 000 t w 1300 (Miller i Hatcher za Pollard i Crossley); 1.6 kg/glowe dopiero ok. 1500 (zrodlo) |
| Drewno opalowe | **~1 760** (Londyn) | piekarze i piwowarzy 21-27% opalu Londynu, reszta domy | **~70% opalu to domy** | Londyn 1300: 141 tys. t drewna rocznie, ~80 tys. ludzi (Galloway, Keene, Murphy 1996) (zrodlo) |
| Budulec | ~50 (SZACUNEK) | ciesle | domy przez ciesli | SZACUNEK |
| Mieso | ~20-40 mieszczanie (SZACUNEK) | rzeznicy | domy | biedni jedli malo miesa (~2% kalorii pod koniec XIII w.), miasto wiecej niz wies (zrodlo) - gra trzyma mieso w systemie zywnosci, poza tym bilansem |

**Wniosek historyczny**: surowce posrednie (welna, len, skory, ruda) szly w 90-100% do warsztatow.
Mieszczanie kupowali wyroby i opal. To, co zjada miasto w grze, powinno byc **zuzyciem gospodarstw**
(plotno, skora jako buty/pasy, drewno jako opal i budulec, a w innych kategoriach: szaty, sukno, narzedzia, mieso),
a nie surowcem dla rzemiosla.

### 2.3 Skala: produkcja kontra konsumpcja (kod + zrodlo)
Przelicznik: 1 szt./dzien na swiat = 10 kg x 168 dni / 10.5 mln mieszczan = 0.00016 kg/glowe/rok.

| Dobro | Miasto zjada (szt./dzien) | = kg/mieszczanina/rok | Historia (gospodarstwa) | Konsumpcja : historia | Wydobycie : historia (AUDYT-SUROWCE 4) |
|---|---|---|---|---|---|
| len | 537 | 0.086 | ~0.1 (surowy) | **~1 : 1** (!) | 1 : 285 |
| welna | 274 | 0.044 | ~0.05-0.1 (surowa) | **~1 : 2** | 1 : 290 |
| skory | 240 | 0.038 | ~0.01 | **4 : 1** (wiecej niz historia) | 1 : 470 |
| skora | 50 | 0.008 | ~0.5 | 1 : 60 | 1 : 2 600 |
| plotno | 78 | 0.012 | ~0.4 | 1 : 30 | 1 : 500 |
| ruda | 609 | 0.097 | 0 | nieskonczenie za duzo | 1 : 190 |
| drewno | 894 | 0.14 | ~1 800 | 1 : 12 000 | 1 : 960 (bez opalu) |

Tu jest sedno: przy surowcach posrednich miasto zjada **w skali 1:1 do historii**, podczas gdy wsie produkuja w skali
1:200-1:500. Jeden symbol (wydobycie) i drugi symbol (zjadanie) sa w roznych skalach - stad "len znika".

---

## 3. Bilans dzienny swiata - stan i propozycja

Produkcja i warsztaty: AUDYT-SUROWCE 1 i 2.2 + log 14:05 (warsztaty: linen_weavery 12, wool_weavery 20, tannery 10,
butcher 20, smithy 12, wood 27, mines 13). WorkshopLaw: symulacja z CHANGELOG 48 (ruda ~440, drewno ~2 400 to sufity
przy pelnym zaopatrzeniu - w praktyce bierze to, co zostanie).

"Udzial gospodarstw" = jaka czesc towaru trafiajacego na targ historycznie kupowaly domy (2.2). Cel = udzial x doplyw.

| Towar | Doplyw (szt./dzien) | Warsztaty (wsad) | WorkshopLaw / partie | Miasto zjada DZIS | Udzial gospodarstw | Miasto zjada CEL | Wynik DZIS | Wynik z CELEM |
|---|---|---|---|---|---|---|---|---|
| len | ~110 (wsie) | tkalnie 12 x 2 = 24 | - | 537 | 10% | **~11** | -450 (dno) | **+75** -> zapas rosnie, tkalnie pracuja |
| welna | ~75 + owce w uboju (~90) | tkalnie welny 20 x 3 = 60 | partie do ~50 | 274 | 5% | **~5** | -250 (dno) | **~-25..+25** (styk) |
| skory surowe | ~200 (rzeznie, SZACUNEK) | garbarnie 10 x 2 = 20 | - | 240 | 2% | **~4** | ~0 (log: +100, bo polki sa pelne tylko w czesci miast) | **+175** -> potrzeba garbarni (P4) |
| skora | garbarnie 10 x 4 = 40 (gdy maja skory) | - | WorkshopLaw: zbroje skorzane, tarcze, uprzaz | 50 | 60% | **~24** | -10..-40 (dno) | **+16 dla WorkshopLaw** |
| plotno | tkalnie 12 x 4 = 48 (gdy maja len) | - | WorkshopLaw: przeszywanice, cieciwy; partie | 78 | 60% | **~29** | -30 (dno) | **+19 dla WorkshopLaw** |
| ruda | ~380 + 13 (`mines`, z niczego - P6) | kuznie narzedzi 12 x 1.5 = 18 | WorkshopLaw do ~440 | 609 | 0% | **0** | ujemny | **~+375 dla WorkshopLaw** (zbroje i bron wedlug rudy) |
| drewno | ~1 620 | deski 27 x 2 = 54 | wegiel WorkshopLaw (5 drewna/rude) do ~1 900, partie do ~300 | 894 | ~30% (opal + budulec) | **~430** | +300..+500 | **~+1 100 dla WorkshopLaw i partii** -> pierwszy realny niedobor pojawi sie przy weglu (historycznie sluszny) |

Co miasto POWINNO zjadac (opis tego, co reprezentuje konsumpcja):
- **len, welna, skory surowe, ruda** - prawie nic (domowe przedzenie, rzemien, laty); to surowce dla cechow;
- **skora** - buty, pasy, sakwy kupowane przez domy (gra nie ma osobnego "obuwia", wiec skora gra jego role);
- **plotno** - bielizna, posciel, obrusy;
- **drewno** - opal i budulec domow (dominujace zuzycie historycznie, w grze symbolicznie ~30% wyrebu);
- **wyroby** (szaty, sukno, narzedzia, mieso, piwo...) - bez zmian; to wlasciwy popyt gospodarstw, ktory placi
  za prace warsztatow. Nic nie powstaje z niczego: surowiec, ktorego miasto nie zje, kupuje warsztat (placi
  miastu cene targu, WorkshopLaw - cene historyczna), wyrob wraca na targ i to on jest zjadany.

---

## 4. Proponowana zmiana w kodzie (do akceptacji Jeffa; dwa kroki osobno)

### Krok 1 (naprawa bledu, male ryzyko): skladnik BK w nowej monecie
- **Gdzie**: `Armoury/src/HistoricalPrices.cs`, rejestracja w `ApplyAll` obok `DemandPostfix`.
- **Co**: postfix na `BannerKings.Patches.EconomyPatches.CalculateBudget(Town, float, ItemCategory)`
  (private static w klasie ZEWNETRZNEJ `EconomyPatches`; `AccessTools.Method(AccessTools.TypeByName("BannerKings.Patches.EconomyPatches"), "CalculateBudget")`, brak typu = log i nic):
  ```
  float idx = town.GetItemCategoryPriceIndex(category);
  float extra = town.Prosperity / 1000f / idx;          // to samo, co liczy BK
  float r = _catRatio[category] (domyslnie 1);
  float u = TownUse(category) (domyslnie 1; krok 2);
  __result -= extra * (1f - u / r);
  ```
- **Efekt (kod)**: ruda 465 -> 8, drewno 465 -> 31, skory 46 -> 9, skora 12 -> 2 szt./dzien; dotyczy tez broni i zbroi
  (pomijalnie). Zloto miast z konsumpcji -~1 tys. zl/dzien na swiat.
- **Ustawienie**: brak nowego - dziala pod istniejacym `HistDemandScaling`.

### Krok 2 (bilans): mieszczanie nie jedza surowcow posrednich
- **Gdzie**: ten sam plik. Prefix na vanilla `ItemConsumptionBehavior.MakeConsumption(Town, Dictionary<ItemCategory,float>, Dictionary<ItemCategory,int>)`
  z `priority = Priority.First` i `HarmonyBefore` na id Harmony BK - zeby wykonal sie PRZED prefiksem BK, ktory
  dostaje ten sam slownik. W nim: `categoryDemand[kat] *= TownUse(kat)` dla kategorii z tabeli. Raz na miasto na dzien
  (nie per wiersz - dlatego nie w `CalculateBudget`, tam budzet resztkowy bylby mnozony wielokrotnie).
- **Nie ruszac** `GetDailyDemandForCategory` - z niego vanilla liczy tez szacowany popyt i indeks cen; obciecie go
  zbiloby ceny surowcow i dochody wsi. Zjadanie zmieniamy tylko w miejscu zjadania.
- **Ustawienia** (`Settings.cs` + `python3 tools/gen_mcm.py`; opisy po angielsku):

| Pole | Domyslnie | Opis MCM |
|---|---|---|
| `TownRawUse` | true | "Townsfolk buy finished goods, not craft materials: raw flax, wool, hides and ore go to the workshops" |
| `TownUseFlax` | 0.02 | "Share of town demand for raw flax kept (home spinning)" |
| `TownUseWool` | 0.02 | "Share of town demand for raw wool kept" |
| `TownUseHides` | 0.02 | "Share of town demand for raw hides kept" |
| `TownUseIron` | 0.0 | "Share of town demand for iron ore kept (households buy tools and nails, never ore)" |
| `TownUseLeather` | 0.6 | "Share of town demand for leather kept (shoes, belts, purses)" |
| `TownUseLinen` | 0.4 | "Share of town demand for linen cloth kept (shirts, bedding)" |
| `TownUseHardwood` | 1.0 | "Share of town demand for wood kept (firewood and building)" |

  Mnozniki = cel / dzis ze skladnika "popyt": len 11/514, welna 5/253, skory 4/194, skora 24/38, plotno 29/73, drewno 430/429.
- **Efekt (kod)**: zjadanie swiata len 537 -> ~11, welna 274 -> ~5, skory 240 -> ~4, skora 50 -> ~23, plotno 78 -> ~29,
  ruda 609 -> ~0, drewno 894 -> ~430. Zloto miast z konsumpcji: ~30 tys. -> ~5 tys. zl/dzien na swiat (~250 zl na miasto
  dziennie; dosypka do celu 10 000 + 12 x dobrobyt to wyrowna).
- **Log**: raz dziennie "Mieszczanie: zjedli [flax N, wool N, hides N, leather N, linen N, iron N, hardwood N]" -
  suma `saleLog` z postfiksu na `Town.SetSoldItems` (to tez P1 z AUDYT-SUROWCE).

### Ryzyko / co sprawdzic
- Zapasy lnu, welny, skor i rudy na targach ("Rynek surowcow") powinny rosnac 2-3 dni z rzedu; plotno i skora wyjsc z dna.
- Rosnacy zapas obnizy ceny surowcow (wiecej podazy przy tym samym popycie) - dochod wsi lnianych i owczych spadnie;
  warsztatom to pomoze.
- Zadowolenie BK (typ konsumpcji surowcow) wzrosnie - mniej dni "brak towaru". Nic nie psuje.
- Jesli zapas skor surowych rosnie bez konca - to znak dla P4 (wiecej garbarni), nie dla podnoszenia `TownUseHides`.
- Kolejnosc prefiksow: w logu wypisac `Harmony.GetPatchInfo(MakeConsumption).Prefixes` (owner + priority) - nasz musi byc przed BK.
- Gdy P3/P5 podniosa wydobycie, mnozniki zostaja (cel liczony od dzisiejszego doplywu) - przeliczyc po P1.

---

## 5. Czego nie wiem (uczciwie)
- Dokladnego doplywu z wsi i rzezni - gra go nie loguje (P1 z AUDYT-SUROWCE to rozstrzygnie).
- Cen targu (`GetPrice`) - liczylem po wartosci przedmiotu (indeks ~1); cena moze byc 0.5-2x.
- Liczby historyczne dla lnu, skory i budulca to szacunki (brak twardych danych na glowe w zrodlach, ktore znalazlem);
  welna, skory (waga), zelazo i opal maja zrodla ponizej.
- Liczba wierszy polki na kategorie (modyfikatory jakosci) - skladnik BK mnozy sie przez nia; w kroku 1 to bez znaczenia.

## Zrodla
- Broadberry, Campbell, Klein, Overton, van Leeuwen, *English Economic Growth 1270-1700* (15 mln owiec w 1300; 1.18 jarda kw. sukna na glowe; Tabela 7: welna 19.82 i skory 5.92 mln funtow w 1300-1309): https://warwick.ac.uk/fac/soc/economics/seminars/seminars/conferences/venice3/programme/english_economic_growth_1270-1700.pdf
- Galloway, Keene, Murphy, "Fuelling the city... London's region 1290-1400", Economic History Review 1996: https://onlinelibrary.wiley.com/doi/10.1111/j.1468-0289.1996.tb00577.x ; https://www.researchgate.net/publication/228010410
- Zelazo ok. 1300 (~1 000 t, Miller i Hatcher za Pollard i Crossley): https://www.namho.org/research/SECTION_5_Iron_20131209.pdf ; 1.6 kg/glowe ok. 1500: https://medium.com/world-building-101/not-a-steel-age-a711ef7353d9
- Koszula = 2-3 lokcie plotna: https://www.bookandsword.com/2017/12/09/how-much-did-a-shirt-really-cost-in-the-middle-ages/
- Mieso w diecie ok. 1300: https://www.tandfonline.com/doi/full/10.1080/03044181.2023.2250952 ; https://www.medievalists.net/2026/02/medieval-diets-varied-by-social-status-in-england-study-finds/
- Szewcy kupuja skore od garbarzy: https://en.wikipedia.org/wiki/Cordwainer
- Kod: `BannerKings.dll` (`EconomyPatches+ItemConsumptionPatch`, `EconomyPatches.CalculateBudget`), `TaleWorlds.CampaignSystem.dll`
  (`ItemConsumptionBehavior`, `DefaultSettlementEconomyModel`), `TaleWorlds.Core.dll` (`DefaultItemCategories`, `ItemCategory.InitializeObject`).
