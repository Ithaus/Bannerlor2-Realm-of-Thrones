# Audyt: towary handlowe, surowce, zywnosc i zwierzeta w prawie podazy i popytu (2026-10-04)

Pytanie Jeffa: "czy WSZYSTKIE surowce i towary - wszystko, co mozna kupic - sa w prawdziwym systemie
podazy i popytu?". Ten audyt dotyczy wszystkiego POZA uzbrojeniem (to jest w `AUDYT-PRODUKCJI-*.md`
i `MODEL-MATERIALOW.md`, tu nie powtarzam): surowce (ruda, drewno, skory, skora, len, plotno, welna,
glina, sol...), zywnosc, wyroby (piwo, wino, ceramika, aksamit, narzedzia, bizuteria, oliwa...),
materialy kuzni (wegiel, sztabki ironIngot1-6) i zywiec (krowy, owce, swinie).

Tylko odczyt - zadnych zmian w kodzie ani ustawieniach.

Skroty sciezek (zdekompilowane zrodla, numery linii z dekompilacji ILSpy):
- `tw/` = TaleWorlds.CampaignSystem, `core2/` = TaleWorlds.Core (kategorie), `bk/` = BannerKings, `be/` = BetterEconomy,
  `rot/` = ROT.dll, `nv/` = NavalDLC - katalog scratchpad z audytow 20.09 (`.../scratchpad/{tw,core2,bk,be,rot,nv}`).
- `bkrot/` = BKROTPatch.dll, zdekompilowany w tym audycie (scratchpad sesji, `.../scratchpad/bkrot`).
- `Armoury/src/` = nasz kod. `Modules/...` = katalog gry. Log Armoury: `Modules/Armoury/Armoury-2026-10-04_09-01-00.log`.
- AIInfluence.dll jest zaciemniony (ILSpy: "Illegal tables in compressed metadata stream") - nie da sie go przeczytac.

---

## 0. Odpowiedz w trzech zdaniach

**Nie - tylko czesciowo.** Towary handlowe (surowce, zywnosc, wyroby, zywiec, konie) maja ZYWA cene
z podazy i popytu (vanilla, mnoznik 0.1-10 liczony per KATEGORIA), a karawany naprawde wioza towar
z miast tanich do drogich i placa za niego - tu prawo dziala. Ale popyt to wzor od zamoznosci, a nie
prawdziwe potrzeby (warsztaty, armie), ludnosc "zjada" nawet rude i drewno, a miasto dostaje za to zloto
z niczego. Towar powstaje z niczego (ukryty warsztat `artisans` w kazdym miescie robi drewno, rude,
skory, mieso, len bez wsadu; BetterEconomy dosypuje 45% kopii produkcji wsi) i znika w nicosc (pobor
wsadu BEE bez wyrobu, kasowanie 2% nadwyzek BK); wegiel i sztabki nie maja wlasnego rynku - sa wyceniane
jak drewno i ruda, a na polki wpadaja losowo z `artisans` (w teorii takze stal valyrianska).

---

## 1. Lancuch ceny

### 1.1 Ktory model cen wygrywa

Gra bierze **ostatni** zarejestrowany model danego typu: `GameModelsManager.GetGameModel<T>` idzie od konca listy
(TaleWorlds.Core, `for (num = _gameModels.Count - 1; ...)`, zdekompilowane w tym audycie).

Kolejnosc ladowania (rgl_log_45804.txt:51-90): ... BannerKings.Redux, BetterEconomy, AIInfluence, NavalDLC, ...,
ROT-Core, ..., RealisticBannerlord, ..., Armoury, ..., **BKROTPatch**, ROT_AIInfluence_Compat, ...

| Model | Kto rejestruje | Wynik |
|---|---|---|
| `TradeItemPriceFactorModel` | BEE tylko gdy BK NIE dziala (`be/BetterEconomy/BetterEconomySubModule.cs:108-112`) - BK dziala, wiec **nie**; BK nie rejestruje zadnego; BKROTPatch rejestruje `BKROTPriceModel` (`bkrot/BKROTPatch/BKROTPatch.cs:311`) | **wygrywa `BKROTPriceModel`** = vanilla `DefaultTradeItemPriceFactorModel` + rabat -10% za blogoslawienstwa (Kowal, Semosh, Matka Rhoyne) w `GetTradePenalty` (`bkrot/BKROTPatch.Models/BKROTPriceModel.cs:15-61`). Podstawa ceny nietknieta. |
| (AIInfluence) | `AIInfluence.DynamicEvents.AIInfluenceTradeItemPriceFactorModel` istnieje (rgl_log_45804.txt:1141), mnozniki zdarzen -50%..+100% per kategoria (`Configs/ModSettings/Global/AIInfluence/*.json`: MarketPriceChangePercentMin -50, Max 100) | AIInfluence laduje sie PRZED BKROTPatch, wiec jesli rejestruje model przez `AddModel`, to jest przykryty. Kodu nie da sie przeczytac - **NIEZWERYFIKOWANE**. Nasz `SupplyDemand.ApplyAll` znalazl 3 modele z wlasnym `GetPrice` (Armoury log :27) - Default, BEE i najpewniej AIInfluence. |
| `SettlementEconomyModel` (popyt, zloto miasta) | BK tylko gdy brak BEE (`bk/BannerKings/Main.cs:205-208`), BEE tylko gdy brak BK (`be/.../BetterEconomySubModule.cs:108-112`) - **wzajemnie sie wylaczaja** | **dziala czysty vanilla `DefaultSettlementEconomyModel`**. `BKEconomyModel.GetDailyDemandForCategory` (populacja x klasy, `bk/BannerKings.Models.Vanilla/BKEconomyModel.cs:610-630`) jest martwy - w inwentarzu (rgl_log:1148), ale nie zarejestrowany. |
| `SettlementFoodModel` | BK `BKFoodModel` (`bk/BannerKings/Main.cs:187-190`), potem ROT owija poprzedni `ROTSettlementFoodModel(GetGameModel<...>)` (`rot/ROT/SubModule.cs:314`) | wygrywa **ROT** (owijka nad BK) - patrz 4.3, to wylacza jedna funkcje BK. |
| `VillageProductionCalculatorModel` | BK i BEE wzajemnie sie wylaczaja jak wyzej (`bk/BannerKings/Main.cs:201-204`, `be/...SubModule.cs:104-107`); NavalDLC owija poprzedni (`nv/NavalDLC/NavalDLCSubModule.cs:183`) | vanilla + NavalDLC (ryby/olej wielorybi) + postfix BK "BK contributions" x`BKEconomyLayerStrength` i miekki sufit (`bk/BannerKings.Patches.BetterEconomy/BKEconomyLayerInstaller.cs:146-221`) + nasz `MaterialLaw` (ruda, drewno x3, Armoury log :228). BK `TickGoodProduction` i tak liczy przez `Campaign.Current.Models` (`bk/.../EconomyPatches.cs:1142`). Uwaga: wczesniejszy audyt (AUDYT-PRODUKCJI-MODY 2.2) zakladal `BKVillageProductionModel` jako model - to jest tylko postfix. |

Nasze prawo (`SupplyDemand`, `ArmsPricing`, `MarketGlut`) dziala **tylko** dla `Equipmentish` = bron, pancerze,
amunicja, `Horse`, `HorseHarness` (`Armoury/src/SupplyDemand.cs:51-76`) - jawny komentarz: "towary handlowe maja
wlasny, zywy rynek vanilla (0.1-10)" (:74).

### 1.2 Wzor ceny towaru w miescie

`TownMarketData.GetPrice` (`tw/TaleWorlds.CampaignSystem.Settlements/TownMarketData.cs:129-133`) bierze dane
**kategorii** przedmiotu (`ItemData`: Supply, Demand, InStore, InStoreValue) i wola model:

```
cena = Value x mnoznik_bazowy x (1 + kara)      (kupno)
cena = Value x mnoznik_bazowy / (1 + kara)      (sprzedaz)      tw/.../DefaultTradeItemPriceFactorModel.cs:159-168, 184-194
mnoznik_bazowy = ( Demand / (0.1 x Supply + 0.04 x InStoreValue + 2) ) ^ p     :170-182
  p = 0.6 (zwierzeta 0.3); przy sprzedazy InStoreValue += Value sprzedawanej sztuki (:172-175)
  towar handlowy: clamp 0.1 .. 10 (:177-179); nie-handlowy (bron, zbroje, noble_horse): 0.8 .. 1.3 (:181)
```

Kara transakcyjna (wojna, sprzedaz na wsi, karawana itd.) - opisana w AUDYT-PRODUKCJI-VANILLA 4.1; BK dokleja
swoje modyfikatory postfixem `GetTradePenaltyPostfix` (`bk/.../BKEconomyLayerInstaller.cs:435-464`).

Ktore kategorie to "towar handlowy" (`core2/DefaultItemCategories.cs:349-418`): zboze, ryby, mieso, ser, maslo,
winogrona, oliwki, daktyle, oliwa, len (flax), plotno (linen), welna, bawelna, aksamit, **drewno**, **zelazo (ruda)**,
sol, srebro, skory (hides), glina, piwo, wino, narzedzia, ceramika, skora (leather), futra, bizuteria, filc, deski,
**owce, krowy, swinie, juczne, horse, war_horse** (`isTradeGood: true`). Nie-handlowe: `cloth` (:363 - garment),
cale uzbrojenie, `noble_horse`.

### 1.3 Skad Supply i Demand (dziennie)

Kolejnosc dnia w miescie: `ItemConsumptionBehavior.MakeConsumptionInTown` (`tw/.../ItemConsumptionBehavior.cs:61-71`):
DeleteOverproducedItems -> UpdateSupplyAndDemand -> UpdateDemandShift -> MakeConsumption -> GetFoodFromMarket ->
UpdateSellLog -> UpdateTownGold. BK podmienia `MakeConsumption` (`bk/.../EconomyPatches.cs:578-703`) i wylacza
vanillowe `DeleteOverproducedItems` drugim prefiksem `Prefix(Town) => false` (:705-710; nazwa metody z atrybutu nie
zdekodowana, ale w DLL BK jedyne pasujace stringi to `MakeConsumption`, `MakeConsumptionInTown`, `DeleteOverproducedItems`).

- **InStore / InStoreValue** - ZYWE: kazda zmiana polki miasta (`Town.OnInventoryUpdated`,
  `tw/.../Settlements/Town.cs:751-754`) dopisuje sztuki i wartosc do kategorii (`TownMarketData.cs:68-78, 91-94`);
  raz dziennie przeliczane od zera (`TradeCampaignBehavior.cs:113-121` -> `TownMarketData.UpdateStores :135-148`).
  Ekran handlu widzi wiec kazda kupiona/sprzedana sztuke od razu.
- **Supply** = srednia wykladnicza **wartosci towaru na polce** (nie liczby sztuk): `S = 0.85 S + 0.15 InStoreValue`,
  min 0.1 (`tw/.../DefaultSettlementEconomyModel.cs:53-59`, wolane z `ItemConsumptionBehavior.cs:207-218`).
- **Demand** = srednia wykladnicza **wzoru od zamoznosci**: `D = 0.85 D + 0.15 x [BaseDemand x (Prosp+1000) +
  LuxuryDemand x max(0, Prosp-3000)]` (`DefaultSettlementEconomyModel.cs:61-73, 91-94`; BaseDemand = liczba z tabeli
  x 0.001, `core2/ItemCategory.cs:60-61`). **Popyt nie zalezy od tego, ile towaru zuzywaja warsztaty, armie czy
  karawany** - tylko od zamoznosci miasta. Jedyne inne ruchy popytu: substytucja (zboze<->ryby, mieso->ryby,
  `ItemConsumptionBehavior.cs:185-204` + `DefaultItemCategories.cs:349-356`), zdarzenia (`IncidentsCampaignBehaviour.cs:1904,1920`
  x1.1), start gry `AddDemand 3 / AddSupply 2` (`TradeCampaignBehavior.cs:123-136`).

Przyklad (zamoznosc 5000): ruda (Iron: Base 10, Lux 20) D = 0.01 x 6000 + 0.02 x 2000 = 100. Mnoznik = 1 przy
0.14 x wartosc_na_polce ~ 98, czyli ~700 denarow = **ok. 14 rud na polce**; 140 rud -> (100/982)^0.6 = **x0.25**;
2 rudy -> x2.8. Zboze (Base 140): D = 840 -> mnoznik 1 przy ~600 workach. Cena idzie za zapasem, ale z opoznieniem
(Supply dochodzi do stanu polki o 15% dziennie).

### 1.4 Gdzie idzie nadwyzka (ujscia)

| Ujscie | Gdzie | Co sie dzieje z towarem | Pieniadze |
|---|---|---|---|
| Konsumpcja ludnosci (BK) | `bk/.../EconomyPatches.cs:609-657` | dla KAZDEJ kategorii z popytem (takze ruda, drewno, skory, len, glina) budzet `D x indeks^0.10-0.20 + Prosp/1000/indeks` (`CalculateBudget :1273-1288`) zdejmuje `budzet/cena` sztuk - **znikaja** | **miasto dostaje `cena x sztuki` z niczego** (:649-651) |
| Glod: dodatkowe zjadanie zywnosci | `EconomyPatches.cs:628-632` | gdy zapas < 10% limitu, ludnosc zjada dodatkowo `-FoodChange` sztuk jedzenia | jw. |
| Wsad warsztatow (vanilla/BK) | `tw/.../WorkshopsCampaignBehavior.cs:750-774, 857-873`; BK `ConsumeInputFromTownMarketPrefix` `EconomyPatches.cs:868-884` | przetworzenie (zboze -> piwo, glina -> ceramika, krowa -> mieso + skory) - to jest uczciwe | warsztat placi miastu cene (BK placi za kazda sztuke; vanilla za jedna, :866-867) |
| **Pobor wsadu BEE** | `be/BetterEconomy.Behaviors/TownEconomyCampaignBehavior.cs:1653` -> `EnsureSnapshot(consumeInputs:true)` :2702-2723 -> `ConsumeInputs :3034-3083` | KAZDY dzien, w kazdym miescie, dla kazdego typu warsztatu zdejmuje `ceil(WorkshopDailyInputDrawPerShop x pokrycie) x liczba_warsztatow` sztuk kazdego wsadu receptury (:2878-2888) - **bez wyrobu, znika** | nikt nie placi |
| BK nadprodukcja | `bk/BannerKings.Behaviours/BKSettlementBehavior.cs:314-338` | towar handlowy nie-zywnosc, nie-zwierze >500 szt. w stosie: -2% dziennie (`DeleteOverProduction = true` w `Configs/.../BannerKings/BannerKings.json`) | - |
| Gnicie zywnosci BK | `BKSettlementBehavior.cs:847-924` | wylaczone (`"RottingFood": false` w BannerKings.json) | - |
| Wioska Marketplace BK | `bk/BannerKings.Behaviours/BKBuildingsBehavior.cs:221+` | wioska wykupuje 1 szt. towaru, ktorego podaz > popyt (AUDYT-PRODUKCJI-MODY 2.7) | wioska placi |
| Zakupy partii | `tw/.../PartiesBuyFoodCampaignBehavior.cs:56-69` (jedzenie), karawany (2.1), BK PartySupplies (len/welna/narzedzia/drewno) | przeniesienie | placi kupujacy |

Wniosek: nadwyzka towaru **nie stoi** - znika przez konsumpcje (z zyskiem dla kasy miasta), pobor BEE i kasowanie BK,
a cena spada tylko do 0.1 wartosci.

---

## 2. Ruch towarow

### 2.1 Karawany - tak, sa sterowane roznica cen

- BK wylacza vanillowe AI karawan (`bk/BannerKings.Patches/CaravansCampaignBehavior_HourlyTickParty_Skip.cs:8-28`)
  i prowadzi je swoim `BKCaravansBehavior` (`bk/BannerKings/Main.cs:161`), ktory jest kopia vanilli:
  srednia i minimum indeksu ceny kazdej kategorii po wszystkich miastach (`bk/BannerKings.Behaviours/BKCaravansBehavior.cs:860-880`,
  vanilla `tw/.../CaravansCampaignBehavior.cs:539-569`), cel = miasto z najwyzszym wynikiem sprzedazy
  (`GetTradeScoreForTown` :1377, `CalculateTownSellScoreForCategory` :1450), kupno tylko towarow handlowych i zwierzat
  (`CalculateBuyValue` :1884-1924: `if (!category.IsTradeGood && !category.IsAnimal) return 0`), gdy indeks w miescie
  jest nizszy od sredniej (vanilla `CaravansCampaignBehavior.cs:1381-1414`), sprzedaz gdy wyzszy (`SellGoods` :1552).
  Karawana placi miastu i dostaje od miasta - towar jest przenoszony, nie tworzony.
- To jest **prawdziwy arbitraz**, ale: liczony na indeksie KATEGORII (nie przedmiotu), bez kosztu drogi w zysku
  (odleglosc jest tylko w wyniku miasta), i nie dotyczy uzbrojenia (dlatego zrobilismy `SupplyDemand.DailyTrade`).

### 2.2 Wiesniacy - ruch tak, ale bez patrzenia na cene

- Produkcja wsi trafia na polke wsi (`tw/.../VillageGoodProductionCampaignBehavior.cs:157-176`, BK `TickGoodProductionPatch`
  `bk/.../EconomyPatches.cs:1120-1163`), zywnosc tez (`:178-210`). Produkcja staje, gdy magazyn wsi > 1.5 x pojemnosc (`:138-155`).
- Wiesniacy niosa WSZYSTKO do miasta macierzystego (BK `VillagerMoveItemsPatch` `EconomyPatches.cs:1056-1080`) i sprzedaja
  **calosc po kazdej cenie**, ograniczeni tylko zlotem miasta (BK `SellGoodsPatch` :963-1053, vanilla
  `tw/.../Actions/SellGoodsForTradeAction.cs:40-62`). Zloto miasta odrasta samo: `0.25 x (10000 + 12 x Prosp - zloto)`
  dziennie (`DefaultSettlementEconomyModel.cs:75-79`, wolane `ItemConsumptionBehavior.cs:73-77`), wiec w praktyce
  wies zawsze sprzedaje. Zalew miasta obniza cene, ale nie zatrzymuje dostaw.

### 2.3 BetterEconomy - ruch bez zaplaty i kopie z niczego

- Dostawy karawan BEE z wioski do miasta: wioska zrodlowa **nie dostaje zaplaty** (`be/.../CaravanCampaignBehavior.cs:924-925`,
  szczegoly w AUDYT-PRODUKCJI-MODY 3.2).
- **Produkcja drugorzedna wsi** (`be/.../VillageDevelopmentCampaignBehavior.cs:396-450`): 20-35% produkcji glownej (max 8/dzien)
  do wsi **plus 45% tej ilosci dosypane od razu na polke miasta** (`:438-446`, `VillageSecondaryBoundTownShare 0.45`,
  `Modules/BetterEconomy/ModuleData/better_economy_settings.xml:132`) - kopia, nie przeniesienie. Wioski drwali -> narzedzia
  (`ChooseSecondaryItem :728-745`, `FirstExistingDifferent :759-773` bierze pierwszy istniejacy: tools), hodowle -> skory, len/welna -> plotno.
- "Przekierowanie handlu" wsi z zalem (`VillageDevelopmentCampaignBehavior.cs:520-560`): `AddToCounts(+num)` na polke innego
  miasta **bez zdjecia z wioski** - drugi strumien z niczego.
- `LordInvestment` / `CulturalGoods` (towar kultury z niczego) - **martwe** przy BK (`BetterEconomySubModule.cs:94-97`).

### 2.4 Tworzenie z niczego (towary)

| Zrodlo | Gdzie | Ile |
|---|---|---|
| **`artisans` (ukryty, kazde z 97 miast)** - linie BEZ wsadu z towarem | `Modules/BannerKings.Redux/ModuleData/workshops.xml:5-70` | drewno (kategoria `hardwood`) 1/dzien, skory 0.6, mieso 0.5, **kategoria `iron` 1.25**, skora 0.2, plotno 0.5 - na miasto |
| mnoznik sztuk `artisans` | `bk/.../EconomyPatches.cs:797-802` | `count = max(1, 1 + rzemieslnicy/45/Value)` - im TANSZY towar, tym wiecej sztuk z jednego cyklu |
| BEE produkcja drugorzedna +45% w miescie, przekierowanie | 2.3 | do 8/dzien/wies + 45% |
| Kopalnie BK w miescie/zamku | `BKBuildingsBehavior.cs:426-463` | mineral na targ, miasto placi (produkcja, nie kreacja zlota) |
| Warsztaty notabli z towarem | `WorkshopsCampaignBehavior.cs:750-792` | przetworzenie ze wsadu; produkuja tylko z zyskiem (`wyrob > wsad + 200/speed`, :776-781) - **hamuja przy zalewie** (cena wyrobu spada) |

Wszystkie wyjscia warsztatow sa KATEGORIA, przedmiot jest losowany z kategorii z waga `1 / (max(100, Value) + 100)`
(`WorkshopsCampaignBehavior.cs:1049-1069`, lista `FillItemsInAllCategories :970-989`, warunek `IsProducable :991-998` =
nie-multiplayer, merchandise, nie od gracza). To ma skutek dla kuzni - patrz 3.2.

---

## 3. Surowce kuzni (wegiel, sztabki, ruda, drewno)

### 3.1 Czy sa na targach

- Wszystkie sa **towarami handlowymi i merchandise**: `charcoal` kategoria **Wood** (`tw/TaleWorlds.CampaignSystem/DefaultItems.cs:127`),
  `ironIngot1-6` kategoria **Iron** - ta sama co ruda (`:128-133`), ruda `iron` Iron (:125), `hardwood` Wood (:126).
  `items-dump.csv` (CrashScribe): charcoal, ironIngot1-6 `merchandise=1`. Kategoria Wood ma id `hardwood`, Iron `iron`
  (`core2/DefaultItemCategories.cs:294-295`).
- Ceny bazowe ustawia nasz `MaterialLaw` (`Armoury/src/MaterialLaw.cs:46-80`): wegiel 9, sztabki 87/118/157/210/281/1000
  (Armoury log :228).
- Spis z gry (Armoury log :321, :359, :399, :435 - "Rynek surowcow", 4 kolejne dni): ruda 1327 -> 1077, drewno 4701 -> 5678,
  **wegiel 17 -> 22 -> 30 -> 37** (rosnie, choc nikt w swiecie go nie wytapia), len 1649 -> 188 (spadek x9 w 4 dni),
  skora 103 -> 73. Sztabek spis nie liczy.
- Indeks ceny rudy miedzy miastami 0.22 - 5.42, drewna 0.16 - 1.99 (log :322) - rynek surowcow jest zywy.

### 3.2 Jak sa wyceniane - i skad sie biora

- **Cena = Value x mnoznik KATEGORII.** Wegiel ma indeks drewna, sztabka stali valyrianskiej - indeks rudy. Brak wegla
  przy 500 drewnach na polce = wegiel tani; 20 sztabek stali na polce nie obniza ceny stali, tylko... cene rudy.
- **Sztabki psuja cene rudy.** `InStoreValue` i `Supply` licza WARTOSC kategorii (`TownMarketData.cs:91-94, 135-148`),
  wiec 10 sztabek valyrianskich (10 000) to dla wzoru tyle co 200 rud - `0.04 x 10000 = 400` w mianowniku zbija cene
  rudy w tym miescie do minimum. Przez to klamie tez nasz wskaznik surowcow `ArmsPricing.LocalRatio`
  (`Armoury/src/ArmsPricing.cs:246-256` - liczy cene rudy z tego samego rynku).
- **Skad wegiel i sztabki na polkach (hipoteza z kodu, mocna; do potwierdzenia spisem sztabek):** linia `artisans`
  "kategoria iron 1.25/dzien bez wsadu" (`workshops.xml:28-33`) i "kategoria hardwood 1/dzien" (:7-12) losuja
  przedmiot z kategorii (`WorkshopsCampaignBehavior.cs:1049-1069`). Przy cenach MaterialLaw wagi `1/(max(100,V)+100)` daja:
  - kategoria Wood: drewno 50%, wegiel 50% (oba V <= 100);
  - kategoria Iron: ruda 19.8%, surowka 19.8%, zelazo kute 18.2%, zelazo 15.4%, stal 12.8%, stal szlachetna 10.4%,
    **stal valyrianska 3.6%** (o ile ROT nie dodal innych przedmiotow kategorii iron).
  Do tego BK mnozy sztuki tanich wyrobow (`EconomyPatches.cs:801`) - wegiel (V 9) wychodzi po kilka sztuk na cykl.
  97 miast x 1.25 = ok. 120 losowan kategorii iron dziennie = rzedu **4 sztabek valyrianskich dziennie z niczego** na swiat.
  Wzrost wegla 17 -> 37 przy braku jakiegokolwiek wytopu AI pasuje do tej hipotezy.
- **Zjadanie:** wsad warsztatow jest KATEGORIA i BK bierze z calej polki (`ConsumeAcrossRoster`, `EconomyPatches.cs:868-884`),
  wiec warsztat potrzebujacy "rudy" moze zjesc sztabke stali, a potrzebujacy "drewna" - wegiel (znany blad, AUDYT-PRODUKCJI-VANILLA 3.4).
  Ludnosc tez "konsumuje" kategorie Iron i Wood (Base 10/Lux 20 i 10/10, `DefaultItemCategories.cs:365-366`) - sztabki i wegiel
  znikaja jak chleb, z zyskiem dla kasy miasta.
- Nasz `WorkshopLaw` bierze konkretne przedmioty `iron`, `hardwood`, `leather`, `linen`, `wool` (`Armoury/src/WorkshopLaw.cs:58-66, 96-99`)
  i placi miastu cene targu (`:34-35`) - sztabek i wegla nie rusza; linie z towarem handlowym zostawia vanilli (`:51-56`, `:38`).

### 3.3 Ruda i drewno dla WorkshopLaw

- Wydobycie: kopalnie i drwale wsi x3 (`MaterialLaw.cs:131-146`, log :228) + `artisans` z niczego (2.4) + kopalnie BK.
- Zuzycie: WorkshopLaw (placi), warsztaty vanilla/BK z wsadem (placa), budowy BK (AUDYT-PRODUKCJI-MODY 2.7), **ludnosc** (1.4),
  **pobor BEE** (1.4), BEE Armory (AUDYT-PRODUKCJI-MODY 3.1).
- Cena rudy i drewna dla warsztatu = cena targu (vanilla, kategoria) - prawdziwa podaz, ale popyt to wzor od zamoznosci,
  nie zapotrzebowanie kuzni. Gdy WorkshopLaw wykupi rude, cena rosnie tylko dlatego, ze spadla polka - zgodnie z prawem,
  tylko z opoznieniem 15%/dzien (1.3).

---

## 4. Zywnosc i zapasy miasta

### 4.1 Dwa rozne "spichrze"

Miasto ma **liczbe** `FoodStocks` (glod, oblezenie) i **polke** z workami zboza, rybami itd. To nie jest to samo.

Zmiana zapasu na dzien (`tw/.../DefaultSettlementFoodModel.cs:43-97`, BK `bk/BannerKings.Models.Vanilla/BKFoodModel.cs:39-69`,
ROT `rot/ROT.Models/ROTSettlementFoodModel.cs`):
- przychod: **15 "ziemie wokol miasta" + (poziom paleniska + 1) x 6 za kazda wies** (`:63-77`) - abstrakcja, bez towaru;
  BK + 15% produkcji ziemi uprawnej z populacji (`BKFoodModel.cs:62-71`); ROT +100 dla `town_EW4`, +120 `ROT_castle61`;
- przychod: **+1 za kazda sztuke kategorii `BonusToFoodStores` zjedzona wczoraj z polki** (`SoldItems`, :82-91) - jedyne
  polaczenie polki z zapasem;
- rozchod: zamoznosc/40 + garnizon/20 (:47-56), BK populacja (`BKFoodModel.cs:84-137`).
- dzienny bilans i **sufit**: `FoodStocks += FoodChange`, nadwyzka ponad `FoodStocksUpperLimit` **znika** (`tw/.../Town.cs:596-617`;
  limit BK 500, zamek +250, `BKFoodModel.cs:31,37`).

### 4.2 Polka a zapas

- Zywnosc z wiosek fizycznie jedzie na polke (2.2) i jest zjadana przez konsumpcje ludnosci (1.4) - kazda zjedzona sztuka
  daje +1 do zapasu nastepnego dnia. Gdy zapas < 10% limitu, ludnosc zjada dodatkowo (`EconomyPatches.cs:628-632`);
  przy 5% BK oproznia schowek (stash) - ale dodaje do zapasu tylko konie (`:658-690`, `if (item2.HasHorseComponent)`),
  jedzenie ze schowka po prostu znika (blad BK).
- Wies **liczy sie podwojnie**: daje abstrakcyjne (poziom+1) x 6 dziennie ORAZ produkuje fizyczne jedzenie, ktore po
  zjedzeniu tez dodaje do zapasu.
- **Filc liczy sie jako jedzenie** (`DefaultItemCategories.cs:417`, `BonusToFoodStores`) - blad vanilli; piwo tez (:370).
- Oblezenie: vanilla bierze jedzenie z polki tylko gdy zapas = 0 (`ItemConsumptionBehavior.cs:98-106`).

### 4.3 Nadwyzka zywnosci - martwa funkcja BK

`HandleExcessFood` (`bk/BannerKings.Behaviours/BKSettlementBehavior.cs:613-671`) mial raz w tygodniu, przy zapasie >= 95%
limitu, zamieniac nadwyzke produkcji na towar na polce (miasto placi pol ceny, `BuyOutput :840-845`). Wymaga
`Campaign.Current.Models.SettlementFoodModel is BKFoodModel` (:615) - a model to `ROTSettlementFoodModel` (owijka), wiec
**funkcja nigdy nie dziala**. Nadwyzka zapasu ponad limit po prostu znika (`Town.cs:614-617`). (Wniosek z kodu i kolejnosci
ladowania; logiem nie potwierdzony.)

### 4.4 Zywiec

Krowy, owce, swinie: zwierzeta i towary handlowe (`DefaultItemCategories.cs:378-380`, Base 8/8/6, Lux 0), cena zywa z p = 0.3
(slabiej reaguje). Zrodla: wsie (cattle/sheep/swine farm), wsie zbozowe (krowa 0.2/dzien). Ujscia: konsumpcja ludnosci,
rzeznie BK/`artisans` (krowa -> 6 miesa + 2 skory, `workshops.xml` linie "Input cow"), karawany (kupuja zwierzeta),
zapasy BEE dla wojska (AUDYT-PRODUKCJI-MODY 3.2), partie zjadaja zywiec. Nie dotyczy ich nasze prawo (ItemType `Animal`,
nie `Horse`).

**Konie** (`horse`, `war_horse`) sa jednoczesnie towarem handlowym z zywa cena vanilli (0.1-10, p = 0.3) **i** w naszym
`SupplyDemand` (`Equipmentish` -> `Horse`, `SupplyDemand.cs:70`) - nadmiar konia na polce liczy sie dwa razy.

---

## 5. Luki (wzgledem naszego prawa dla uzbrojenia)

| # | Luka | Dowod | Skutek |
|---|---|---|---|
| L1 | **Towar z niczego w kazdym miescie** - `artisans` robi drewno, rude/sztabki, skory, mieso, skore, plotno bez wsadu | `workshops.xml:5-70`, `EconomyPatches.cs:797-802` | ok. 97 x (1 + 1.25 + 0.6 + 0.5 + 0.2 + 0.5) cykli dziennie, mnozone dla tanich; zalewa targi tanim surowcem, gasi sens kopaln i drwali |
| L2 | **Sztabki i wegiel losowane z kategorii** | `WorkshopsCampaignBehavior.cs:1049-1069`, log :321-435 (wegiel rosnie) | stal valyrianska z niczego; kowal gracza kupuje stal, ktorej nikt nie wytopil |
| L3 | **Cena per kategoria, nie per przedmiot** | `TownMarketData.cs:129-133` | wegiel = indeks drewna, sztabki = indeks rudy; drogie sztabki na polce zbijaja cene rudy i nasz `MaterialIndex` |
| L4 | **Popyt = wzor od zamoznosci** | `DefaultSettlementEconomyModel.cs:61-73` (dziala vanilla, BK/BEE wylaczone) | zapotrzebowanie warsztatow (WorkshopLaw), armii i budow nie podnosi popytu; cena reaguje tylko na spadek polki |
| L5 | **Ludnosc zjada surowce i polprodukty** (ruda, drewno, sztabki, wegiel, skory, len, glina) | `EconomyPatches.cs:609-657`, kategorie `DefaultItemCategories.cs:358-376` | towar posredni znika jak chleb |
| L6 | **Zloto z niczego** za konsumpcje i "dociaganie" kasy miasta | `EconomyPatches.cs:649-651`, `DefaultSettlementEconomyModel.cs:75-79` | miasto zawsze ma czym zaplacic wsi; zalew nie hamuje dostaw |
| L7 | **BEE zjada wsad bez wyrobu** | `TownEconomyCampaignBehavior.cs:1653, 3034-3083`; `better_economy_settings.xml:165` (1.0) | drugi pobor surowca obok prawdziwego warsztatu; prawdopodobna przyczyna spadku lnu 1649 -> 188 w 4 dni (hipoteza) |
| L8 | **BEE dosypuje kopie produkcji wsi i "przekierowania"** | `VillageDevelopmentCampaignBehavior.cs:438-446, 549-551`; xml :132 (0.45) | towar z niczego |
| L9 | **Wiesniacy sprzedaja po kazdej cenie** | `EconomyPatches.cs:1017-1045` | brak hamulca podazy od strony wsi (poza magazynem wsi, ktory sie nie zapelnia) |
| L10 | **Zywnosc: abstrakcyjny przychod + podwojne liczenie wsi, nadwyzka znika, HandleExcessFood martwy** | 4.1-4.3 | zapas miasta nie wynika z towaru; nadwyzka zboza nie wraca na rynek |
| L11 | Konie podwojnie (vanilla + SupplyDemand) | 4.4 | glebszy spadek ceny zalanej polki koni niz zamierzono |
| L12 | Brak kosztu drogi w arbitrazu karawan | `BKCaravansBehavior.cs:1377-1450` | slabe - drobne |
| L13 | Kasowanie 2% nadwyzek >500 (BK) | `BKSettlementBehavior.cs:333-336` | drobne, ale to tez znikanie |

Co **juz jest** realne: zywa cena od zapasu (0.1-10), natychmiastowa aktualizacja polki, karawany kupujace tanio
i sprzedajace drogo za zloto, warsztaty notabli placace za wsad i stajace przy braku zysku, wsie produkujace fizyczny towar.

---

## 6. Propozycje z kolejnoscia (jedna zmiana naraz - kazda osobno do testu)

Kolejnosc: najwiekszy zysk realizmu przy najmniejszym ryzyku najpierw.

1. **Wylaczyc dwa kurki BEE ustawieniem, bez kodu** - `WorkshopDailyInputDrawPerShop` 1.0 -> 0 i
   `VillageSecondaryBoundTownShare` 0.45 -> 0 w `Modules/BetterEconomy/ModuleData/better_economy_settings.xml:165, 132`.
   Przy 0 `num6 = ceil(0 x pokrycie) = 0` - zadnego poboru (`TownEconomyCampaignBehavior.cs:2878-2888`), reszta BEE (status
   warsztatu, deficyty) liczy dalej. Ryzyko: male (plik moda - przy aktualizacji BEE wroci; zapisac w CHANGELOG). Sprawdzic:
   linia "Rynek surowcow" - len i welna przestaja topniec.
2. **Koniec surowcow z niczego w `artisans` i sztabek z losowania** - rozszerzyc nasz `WorkshopLaw.CyclePrefix`
   (hak juz wpiety na `WorkshopsCampaignBehavior.TickOneProductionCycleForNotableWorkshop`, `WorkshopLaw.cs:101, 278-280`):
   dla ukrytego `artisans` linie BEZ wsadu z wyjsciem-towarem (hardwood, iron, hides, meat, leather, linen) -> `__result=false`
   (albo tylko "praca z niczego" w malym ulamku, z MCM). Plus postfix na `WorkshopsCampaignBehavior.GetRandomItemAux`: kategoria
   `iron` -> zawsze `iron` (ruda), `hardwood` -> zawsze `hardwood` - sztabki i wegiel tylko z wytopu. Ryzyko: srednie - ruda juz
   jest waskim gardlem (AUDYT-PRODUKCJI-MODY 2.1), wiec najpierw zobaczyc spis, ewentualnie podniesc `MineOutputMultiplier`.
   Przed zmiana: dopisac do spisu "Rynek surowcow" ironIngot1-6, zeby miec liczby przed/po (zasada 8.4 CLAUDE.md - ile pracy znika).
3. **Wlasna cena wegla i sztabek** - dolaczyc je do naszego postfixu `SupplyDemand.PricePostfix` (`SupplyDemand.cs:171-212`) jako
   osobny koszyk "material kuzni" z cena `Value x ((D+1)/(S+1))^e` wedle sztuk TEGO przedmiotu; w `ArmsPricing.LocalRatio`
   liczyc rude z ceny samej rudy przy InStoreValue bez sztabek. Ryzyko: male (ten sam hak, ktory juz dziala).
4. **Popyt na polprodukty z prawdziwego zuzycia** - postfix na `DefaultSettlementEconomyModel.GetDailyDemandForCategory` (to jest
   aktywny model) dla kategorii iron, hardwood, hides, leather, flax, linen, wool, clay, silver: popyt = srednia z faktycznego
   dziennego wsadu warsztatow (zdarzenie `OnItemConsumed`, wolane w `WorkshopsCampaignBehavior.cs:871` i BK `EconomyPatches.cs:868-884`,
   plus WorkshopLaw) zamiast wzoru od zamoznosci. Efekt uboczny, ktory chcemy: budzet konsumpcji ludnosci dla tych kategorii spada
   prawie do zera (BK liczy go z tego popytu, `EconomyPatches.cs:616-619`), wiec ludnosc przestaje jesc rude (L4 + L5 naraz).
   Ryzyko: srednie - przestawia indeksy cen w calym swiecie, karawany zmienia trasy; najpierw log indeksow przed/po.
5. **Konie tylko w jednym prawie** - w `SupplyDemand` dla `Horse` z kategoria handlowa liczyc czynnik polki WZGLEDEM czynnika vanilli
   (albo wylaczyc Horse z naszego prawa, a zostawic vanilli). Ryzyko: male; dotyka stajni AI (Stables).
6. **Zywnosc w obiegu** - postfix na model zywnosci (`DefaultSettlementFoodModel.CalculateTownFoodStocksChange`, juz latany w
   `WinterBite.cs:174`): (a) usunac abstrakcyjne `(poziom+1) x 6` za wsie, ktore fizycznie wysylaja jedzenie (koniec podwojnego
   liczenia); (b) nadwyzke ponad limit zamiast kasowac (`Town.cs:614-617`) wystawiac na polke jako zboze (wlasna wersja martwego
   `HandleExcessFood`); (c) filc bez `BonusToFoodStores`. Ryzyko: duze - przy wieloletniej zimie (WesterosClimate) latwo o glod
   w calym swiecie; robic po 1-4 i z wylacznikiem MCM.
7. **Zloto miasta z handlu, nie z powietrza** (L6) - konsumpcja ludnosci placi z dochodu populacji BK, a nie `ChangeGold(+)`;
   `GetTownGoldChange` slabszy. Ryzyko: najwieksze (cala gospodarka BK/BEE opiera sie na tej kasie) - na koniec, osobny audyt.

Haki w skrocie: `WorkshopsCampaignBehavior.TickOneProductionCycleForNotableWorkshop` (mamy), `WorkshopsCampaignBehavior.GetRandomItemAux`,
`TradeItemPriceFactorModel.GetPrice` (mamy, wszystkie modele), `DefaultSettlementEconomyModel.GetDailyDemandForCategory`,
`DefaultSettlementFoodModel.CalculateTownFoodStocksChange` (mamy w WinterBite), XML BetterEconomy.

Do sprawdzenia logiem przed decyzjami: (a) ile sztabek ironIngot1-6 lezy na targach (hipoteza L2); (b) czy AIInfluence naprawde
nie zmienia cen (zdarzenia "market price"); (c) czy `HandleExcessFood` faktycznie nigdy nie wchodzi (BK `Logs`).
