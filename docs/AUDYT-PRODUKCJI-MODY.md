# Audyt produkcji uzbrojenia - MODY i DANE (2026-10-04)

Zakres: skad bron, zbroje, tarcze, luki, amunicja i konie trafiaja na targi miast i zamkow,
w jakim tempie, ile moga zrobic warsztaty, jakie surowce trzeba kupic i gdzie pieniadze
"znikaja" albo "rodza sie". Ten dokument obejmuje **mody i dane** (BannerKings.Redux,
BetterEconomy, ROT, DTE, Spoils of War, Armoury, CrashScribe). Kod vanilla opisuje osobny audyt -
tu vanilla pojawia sie tylko tam, gdzie mod ja nadpisuje albo gdzie bez niej nie da sie policzyc.

Tylko odczyt: zadnych zmian w kodzie. Liczby "na dzien" policzone z kodu/XML przy predkosci 1.0
i bez mnoznikow polityk - **nie** z logu, chyba ze napisano "log".

Zrodla zdekompilowane: scratchpad `.../scratchpad/{bk,be,dte,rot,cs}` (sciezki nizej wzgledem
tych katalogow), `.../scratchpad/sow` (Spoils of War = `RealisticLoot.dll`, zdekompilowany w tym
audycie, NIE zaciemniony). Ustawienia gracza: `Documents\...\Configs\ModSettings\Global\*`.

Swiat ROT (ROT-Map `settlements.xml`): **98 miast, 130 zamkow, 572 wioski**. Wioski wedlug typu
(te wazne dla uzbrojenia): lumberjack 45, iron_mine 26, flax_plant 19, sheep_farm 23,
cattle_farm 24, swine_farm 10, trapper 23, silk_plant 13, silver_mine 30, clay_mine 16,
konie: europe 20, desert 15, steppe 13, vlandian 10, sturgian 7, battanian 6.

---

## 1. Obraz calosci w jednym miejscu

| Kierunek | Kto | Mechanizm | Rzad wielkosci |
|---|---|---|---|
| ZRODLO (targ) | vanilla + BK warsztaty | `artisans` (ukryty, w KAZDYM miescie, slot 0) + weaponsmithy/armorsmithy/barding-smithy/fletcher/smithy | ok. 10 szt./dzien/miasto **bez zadnego wsadu** (artisans), do ~26 z wsadem; specjalistyczny warsztat 6-20 szt./dzien |
| ZRODLO (targ) | BetterEconomy Armory | budynek miejski L1-L3 zuzywa iron/hardwood/leather/tools | do L+2 szt./dzien/miasto, tylko gdzie zbudowano (brak dowodu w logu, ze gdziekolwiek) |
| ZRODLO (targ) | Armoury SupplyDemand | NIE tworzy - przewozi 15% nadwyzki do najblizszej osady z brakiem | log: brak (funkcja z 04.10) |
| ZRODLO (targ) | CrashScribe Mends | relikwie imienne, 1 szt. raz na kampanie | pomijalne |
| ZRODLO (targ) | gracz | sprzedaz lupu (DTE/SoW) i wlasnych wyrobow | zalezy od gracza, najwiekszy pojedynczy strumien wysokich tierow |
| ZRODLO (poza targiem) | DTE | darmowy przydzial do zbrojowni AI: z wojska, z lenn, z klanu, przy werbunku | ~10-40 szt./dzien/partie AI, nigdy nie przechodzi przez targ |
| ZRODLO (poza targiem) | Armoury Stables | "hodowca" - kon z niczego, gdy na targu brak | log X.2026: 1118 z 1932 koni AI z hodowcy |
| ZRODLO (poza targiem) | Armoury SmithMenu "Order kit" | gracz placi Value x1.15, sztuki z niczego na polki zbrojowni | na zadanie gracza |
| UJSCIE (targ) | BK ItemConsumption (patch) | ludnosc miasta "zuzywa" kategorie wg popytu, miasto dostaje zloto | popyt bazowy kategorii broni/zbroi 3-9 |
| UJSCIE (targ) | BK PartySupplies (AI) | partie AI kupuja bron T2-3, tarcze T2-3, strzaly, konie i zuzywaja je | 0.0015-0.003 szt./zolnierz/dzien (x0.5 ustawienie) - **z bledem w BK, patrz 3.6** |
| UJSCIE (targ) | Armoury Stables (AI) | lord kupuje konie pod awanse | max 25% polki, 4 sztuki zawsze zostaja |
| UJSCIE (targ) | BK DeleteOverProduction | kasuje z polek wszystko `IsCraftedByPlayer` i bannery; nadwyzke >500 towarow -2%/dzien | wyroby gracza sprzedane miastu ZNIKAJA nastepnego dnia |
| UJSCIE (targ) | BK budynek wioski Marketplace | wioska kupuje z targu miasta to, czego podaz > popyt | max 5% zlota wioski/dzien |
| UJSCIE (poza targiem) | DTE | co tydzien kasuje nadwyzke zbrojowni AI ponad liczbe ludzi na typ | duze |
| UJSCIE (poza targiem) | Spoils of War | paser, salvage, auto-sprzedaz z magazynu, darowizna | sztuki ZNIKAJA, nie trafiaja na targ |

Wniosek ogolny (hipoteza do sprawdzenia logiem, nie fakt): **targi broni i zbroi sa zasilane
glownie przez warsztaty (artisans robi ~10 szt./dzien z niczego w kazdym z 98 miast), a AI prawie
nie kupuje uzbrojenia, bo DTE daje je za darmo**. Jedyni realni kupcy to gracz, BK PartySupplies
(waski wycinek, z bledem), stajnie Armoury (konie) i konsumpcja ludnosci. Stad niskie tiery leza
na polkach w nadmiarze, a SupplyDemand moze je tylko przesuwac miedzy miastami.

---

## 2. BannerKings.Redux

### 2.1 Warsztaty (BK `ModuleData/workshops.xml` + `EconomyPatches.cs`)

BK nadpisuje vanillowy `artisans` i dodaje `weaponsmithy`, `armorsmithy`, `barding-smithy`,
`fletcher`, `butcher`, `mines`, `bakery`, `meadery`. Vanillowe `smithy`, `tannery`,
`linen_weavery`, `wood_WorkshopType`, `velvet_weavery`, `wool_weavery` zostaja (SandBox `spworkshops.xml`).

Mechanika (vanilla `WorkshopsCampaignBehavior.RunTownWorkshop`, DailyTickTown): kazda linia
produkcji ma wlasny postep; co dzien `+conversion_speed` (x polityki krolestwa), przy >=1 jeden cykl.
Cykl = zuzycie wsadu z targu miasta + `output_count` sztuk na targ. **Dla linii, w ktorej
wejscie albo wyjscie nie jest towarem handlowym (cala bron i zbroja), `effectCapital=false`: warsztat
NIE placi za surowiec i NIE dostaje pieniedzy za wyrob, miasto tez nie** (vanilla linia 1405, BK
`ProduceOutputPrefix` EconomyPatches.cs:783-864 i `ConsumeInputFromTownMarketPrefix` :868-884).
Warunek startu cyklu (warsztaty nie-ukryte): wartosc wyrobu > koszt wsadu + 200/speed; ukryty
`artisans`: wartosc > koszt wsadu (dla linii bez wsadu - zawsze).

BK zmienia:
- `ProduceOutputPrefix` (:783): `artisans` dostaje `count = max(1, 1 + rzemieslnicy/45/wartosc)`;
  `mines` zamienia wyjscie na losowy mineral z `MineralData` (+1 przy ADEQUATE, +2 przy RICH).
- `ItemPreferredPostfix` (:720-744): obcy kulturowo wyrob moze sie trafic wg `MarketGroup`;
  **kusze tylko po innowacji Crossbows**.
- `DecideBestWorkshopTypePostfix` (:748-779): przy dublu typu 30% -> `mines`, inaczej losowo
  smithy/fletcher/barding-smithy/armorsmithy/weaponsmithy. Liczba slotow `DefaultWorkshopCountInSettlement = 6` (BKWorkshopModel.cs:24).
- `WorkshopData.DoProduction` (BK Workshops) jest martwy - `Tick()` pusty.

Wydajnosc na dzien (speed 1.0, wszystkie linie maja wsad):

| Warsztat | Wyroby/dzien (wg kategorii) | Wsad/dzien |
|---|---|---|
| `artisans` (BK, ukryty, slot 0 KAZDEGO miasta) | zbroje 4.0 (light 1, medium 2, heavy 0.8, ultra 0.2); bron biala 6.9 (T1 1.5, T2 2, T3 2.4, T4 0.8, T5 0.2); strzaly 3.2; luki/kusze 4.1; tarcze 3.8; uprzeze konskie 3.9 = **~25.9** | zelazo (ruda) 8.8, drewno 7.1, skora 2.5, hides 1.0 |
| `artisans` - linie BEZ wsadu | light_armor 1, melee T1 1.5, melee T2 2, arrows 3.2, ranged T1 1.2, shield T1 1 = **~9.9 z niczego** | - |
| `artisans` - surowce z niczego | hardwood 1, hides 0.6, iron (ruda) 1.25, leather 0.2, linen 0.5 | - |
| `weaponsmithy` (BK) | T1 9.6, T2 6.4, T3 2.4, T4 1.0, T5 0.3 = **19.7** | ruda 3.6, hardwood 2.4 |
| `armorsmithy` (BK) | light 5.5, medium 4.5, heavy 1, ultra 0.5 = **11.5** | hides 3.0, ruda 2.0 |
| `barding-smithy` (BK) | uprzeze T1-T5 = **6.3** | hides 8.1, ruda 4.4, flax 2 |
| `fletcher` (BK) | strzaly 1.5, luki/kusze T1 4, T2 4, T3 2.4, T4 1.5, T5 0.6 = **14.0** | hardwood 3.6 |
| `smithy` (vanilla) | bron 3.8, zbroje 1.35, narzedzia 6 | ruda 5.65 |
| `wood_WorkshopType` (vanilla) | deski 4, luki 2.4, tarcze 1.7 | hardwood 5.5 |
| `tannery` (vanilla) | leather 4 (2 cykle x 2) | hides 2 |
| `linen_weavery` (vanilla) | linen 4 | flax 2 |
| `butcher` (BK) | z krowy 10 miesa + 5 hides; owca 2+2; swinia 4+2 | zywe zwierzeta |
| `mines` (BK) | 1 cykl/dzien, 1-3 szt. mineralu (ruda zelaza z prawdopodobienstwem udzialu) | - |

Wazne: przy 98 miastach sam `artisans` daje ok. **970 sztuk niskiego tieru dziennie bez wsadu**,
a linie z wsadem sa ograniczone ruda. Popyt artisans na rude (8.8 x 98 = ~860/dzien) przewyzsza
podaz z kopaln wioskowych (26 x 10 = 260/dzien bazowo) - **ruda zelaza jest waskim gardlem**,
o ktora konkuruja: artisans, smithy, weaponsmithy, armorsmithy, barding-smithy, BK budowy,
BE Armory i gracz (rafinacja).

### 2.2 Produkcja wiosek i mineraly

- `TickGoodProduction` (EconomyPatches.cs:1130-1169): produkcja wsi = lista z `GetProductions`
  (PopulationManager.cs:278-303) - typ wioski + mineraly (`Mines` budynek wioski albo wioska
  gornicza: `0.5 x poziom x udzial mineralu`) + `leather 0.5 x poziom Tannery` + `tools 0.5 x poziom Blacksmith`.
  Ilosc: `BKVillageProductionModel` - **0.0045 szt. na robotnika (pansz+dzierzawcy) dziennie**,
  niewolnicy 0.0045 (0.008 przy prawie SlavesHardLabor), drewno z lasu x10/x5, potem mnozniki (kultura,
  budynek DailyProduction +15%, efektywnosc). Na koniec **soft-cap** `BKEconomyLayerInstaller`
  (:187-214): nadwyzka ponad `baza_typu x VillageProductionMultiplier (1.3)` liczy sie tylko w 20%.
  Wiec realnie wioska daje mniej wiecej `baza_typu x 1.3`.
- Bazy vanilla (DefaultVillageTypes): iron_mine **iron 10**, lumberjack **hardwood 18**, flax_plant
  **flax 18**, sheep_farm wool 10 + sheep 4, cattle_farm cow 2, trapper **fur 1.4**, silk_plant
  cotton 8, silver_mine silver 3, rancza koni 3.3-5.7 koni/dzien (w tym sumpter/muly). BK dopisuje
  (BKVillageTypes.cs): lumberjack +mead 2, clay_mine +limestone 8, silver_mine +marble 0.8 +gold_ore 0.2.
- `MineralData.Init` (MineralData.cs:88+): kazda osada losuje mineral glowny (60-90%), poboczny
  (zelazo z waga 20 wsrod pobocznych) i 8% szansy na zloto; bogactwo POOR 60% / ADEQUATE 25% / RICH 15%.
- `RunMines` (BKBuildingsBehavior.cs:426-463, dziennie): budynek Mines/CastleMines w miescie/zamku -
  za kazdy mineral rzut, sukces daje `poziom` sztuk na targ, **miasto placi ich cene** (albo do stash, gdy brak zlota).
- Nigdzie w swiecie zadna wioska nie produkuje **hides** (tylko `artisans`, `butcher`, BE secondary).

### 2.3 Tworzenie i znikanie towaru w miescie (BKSettlementBehavior.cs)

- `TickTown` (:389-399) dziennie: `HandleItemAvailability` (:530-582) - **wylaczone** (`SpawnEquipment=false`);
  przy wlaczeniu dosypywalo co tydzien Ultra/Heavy armor i Melee/Ranged 4-5 wg prosperity.
- `DeleteOverProduction` (:314-338) dziennie w miescie i zamku: **kasuje z polki kazdy przedmiot
  `IsCraftedByPlayer` i bannery**, a towary handlowe >500 szt. tnie o 2%/dzien (`DeleteOverProduction=true`).
  Bron wykuta przez gracza i sprzedana miastu znika nazajutrz (zloto gracz juz dostal).
- `HandleExcessFood` (:613-671) tylko zywnosc; `HandleExcessWorkforce` (:584-611) nie jest wolane.
- `BuyOutput` (:840-845): miasto placi ze swojej kasy za dosypany towar.

### 2.4 Konsumpcja ludnosci (`ItemConsumptionPatch`, EconomyPatches.cs:579-711)

Zastepuje vanilla dla miast z danymi populacji. Dla kazdej sztuki na polce, ktorej kategoria ma
popyt: budzet z `CalculateBudget`, zdejmuje `budzet/cena` sztuk, **miasto dostaje `cena x sztuki`**
(ChangeGold +). Kategorie uzbrojenia maja popyt bazowy vanilla (DefaultItemCategories):
light 7, medium 5, heavy 4, ultra 3, melee 9/7/5/4/4, ranged 9/7/5/4/4, tarcze 9/7/5/4/4,
strzaly 30, uprzeze 9/7/5/5/5, horse 140, war_horse 120. To glowne ujscie broni z targow,
ale proporcjonalne do budzetu ludnosci, a nie do potrzeb armii.

### 2.5 Kuznia BK (zakladka CRAFT) - BKSmithingModel.cs

- `GetCraftingInputForArmor` (:181-259), tablica 11 pol: 0 ruda, 1-6 Iron1-Iron6, 7 drewno, 8 wegiel,
  **9 skora (`leather`), 10 len (`linen`)** (CraftingMixin.cs:272).
  BK oryginal: pancerz metalowy `waga x 0.8 / 0.5` sztuk, 90% glownego gatunku + 10% nizszego
  (Tierf<4: Iron3/Iron2 + 1 len; 4-5: Iron4/Iron3 + 1 skora; 5-6: Iron5/Iron4; >=6: Iron6/Iron5);
  skora `waga/10`; tkanina 1 len; tarcza 2 drewna (+ `waga` Iron4 dla metalowych); bron 2 drewna + 2 metalu tieru.
- **Armoury nadpisuje to postfixem `TrueArmourCost.MaterialsPostfix`** (Armoury Patches.cs:143-223) - patrz 7.2.
- Stawka kuzni `GetSmithingHourlyPrice` (:17-37): 50/h + prosperity/5000, x(1 + 0.1 x tier klanu),
  -15% perk; Armoury `BkForgeHourlyMultiplier 0.5`.
- Kucie tylko dla gracza (`CraftingMixin`, wyrob do `PartyBase.MainParty`, :255).

### 2.6 Zapasy partii AI (`PartySupplies`, `BKPartyNeedsModel`)

- Potrzeby na zolnierza/dzien (BKPartyNeedsModel.cs:20-36) x `PartySuppliesFactor 0.5`:
  strzaly 0.003 (lucznicy), tarcze 0.003, **bron 0.006**, konie 0.001, tkanina 0.01, narzedzia 0.01, drewno 0.02.
- Kategorie (PartySupplies.cs:96-135): bron = **tylko MeleeWeapons2 i MeleeWeapons3**, tarcze = Shield2/Shield3,
  amunicja = Arrows, konie = Horse, tkanina = wool/linen/flax, drewno = Wood (hardwood), narzedzia = Tools.
- Zakup w osadzie z kwatermistrzem: potrzeba x `DaysOfProvision` (BK 10; Armoury tnie do 4, jesienia x2,
  i sufit 12 sztuk na dobro - BkSupplyTemper.cs, Settings.cs:132-133,306).
- **Blad BK** (PartySupplies.cs:334-415): od zapotrzebowania odejmowany jest stan **polki osady**, nie taboru
  partii (:365) - przy pelnej polce AI nie kupuje nic, przy pustawej kupuje roznice. **Zloto lorda znika**
  (`ChangeHeroGold(-cena)`, :415) - miasto nic nie dostaje.
- Zuzycie: `ConsumeItems(potrzeba x 2)` dziennie z taboru (:307-315, :424) - realne ujscie broni.
- Interakcja z DTE: kupione miecze/tarcze/strzaly leza w taborze AI, a DTE co tydzien wciaga je do
  zbrojowni i **wyplaca lordowi pelna Value** (patrz 5.2) - lord kupuje po cenie targu, dostaje Value.

### 2.7 Inne BK

- `RunMaterials` (BKBuildingsBehavior.cs:297-368) + `BKConstructionModel.GetMaterialRequirements` (:70+):
  budowy zjadaja hardwood, clay, **iron (ruda)**, tools, limestone, marble (koszt/20 x udzialy) - konkurencja o rude i drewno.
- `HandleVillage` Marketplace (:242-275): wioska wykupuje z targu miasta 1 szt. towaru, ktorego
  podaz > popyt i ktorego sama nie robi, za max 5% swojego zlota dziennie (dotyczy tez broni).
- `RunStuds` (:369-403): stadnina - konie bojowe kultury miasta do STASH (2%/dzien, limit z pastwisk).
- `SiegePatch` (EconomyPatches.cs:498-544): polka zamku nie jest grabiona przy szturmie.
- `BKBattleRewardTweakPatches` (VanillaModelTweakPatches.cs:263+): vanillowy lup z trupa przepada z p=1-LootScale (0.5).
- `BKCaravansBehavior` (:645-735): karawana po wygranej bitwie wyrzuca nadmiar zwierzat i najtanszy ladunek.

---

## 3. BetterEconomy (Living Economy 1.4.5)

**Tryb zgodnosci z BK jest aktywny** (`bee_log.txt`: "compatibility mode defers core population,
prosperity, tax, village production, price, economy and workshop ownership to BannerKings").
Skutek (BetterEconomySubModule.cs:72-112): **nie dzialaja** BEE_VillageProductionCalculatorModel
(wiec `ProductionProfiles` / kulturowe mnozniki produkcji wsi), BEE_ItemPriceFactorModel, BEE_SettlementEconomyModel,
`WorkshopProductionPatch` (tylko przy !BK). **Dzialaja** TownEconomy, Caravan, Castle, VillageDevelopment,
VillageSupply, CulturalMarket (bez wplywu na ceny), LordInvestment.

### 3.1 Armory miejskie (TownEconomyCampaignBehavior.cs)
- Budowa: gracz (TryPlayerBuildOrUpgradeArmory :568-583) albo AI (`TryApplyAiArmory` :2081-2145, co 14 dni,
  tylko w wojnie albo przy specjalizacji MilitarySupplier). Wymogi: rzemieslnicy >=220, korupcja <70,
  Workshop District L1 lub >=3 sloty, na targu choc 1 z iron/hardwood/leather/tools. Koszt L1 100-200 tys.,
  L2 150-260 tys., L3 220-400 tys. (:3772-3813) - z kasy miasta + lorda (rezerwa 60 tys.).
- Produkcja dzienna `TickArmoryProduction` (:2152-2266): zjada `2 x poziom` szt. KAZDEGO z
  **iron, hardwood, leather, tools** (pokrycie min 35%), budzet = `900 x poziom x pokrycie x korupcja x jakosc`;
  wybiera przedmiot (`PickArmoryProducedItem` :3963, typy: bron biala, tarcze, luki, kusze, rzucane, helmy,
  korpusy, nogawice, rekawice, peleryny - bez amunicji i koni) o wartosci <= 2x budzet i tierze <= poziom+2.5;
  ilosc = budzet/wartosc, **max poziom+2 szt./dzien**, na targ. Pieniadze: brak - surowiec znika, wyrob z niczego.
- Eksport: karawana z "armament contract" przewozi wyrob z miasta-zbrojowni do miasta w potrzebie
  (CaravanCampaignBehavior.cs:729-735); zrodlo dostaje 4% (min 50) do skarbca (:653-672).
- Log: `bee_log.txt` nie ma zadnej linii armory (verbose wylaczony) - **nie wiadomo, czy ktores Armory istnieje**.

### 3.2 Zaopatrzenie wojskowe przez karawany
- `BuildMilitaryDemand` (:4487-4555): cel zapasu = 18 + prosperity x 0.01 + zmobilizowani x 0.18 (x1.25 War Levy);
  materialy wojskowe **iron, hardwood, leather, tools** (cel /2, min 8), wierzchowce horse/warhorse/sumpter/cow/sheep (cel /3, min 4).
- Dostawa (CaravanCampaignBehavior.cs:680-960): karawana bierze towar z **wioski** (`FindSourceVillage`) albo z
  miasta-zbrojowni i kladzie na targ miasta/zamku; **wioska zrodlowa nie dostaje zaplaty** (:924-925);
  przy zaopatrzeniu wojskowym placi skarbiec BE miasta-odbiorcy (`TryRegisterMilitaryProcurement` :2626-2657);
  karawana dostaje wydatek + marza (5% +3.5%/4%). Zamki - analogicznie (CastleEconomy :1448-1488).

### 3.3 Drugorzedna produkcja wsi (VillageDevelopmentCampaignBehavior.cs)
- `ChooseSecondaryItem` (:728+): wioski drewna -> **tools**; zwierzat/koni -> **hides**; rudy/gliny/srebra -> **tools**;
  lnu/bawelny/welny -> **linen**. Odblokowanie: hearth >=650, chlopi >=1800, 30 dni stabilnosci.
- `TickSecondaryProduction` (:396-450): 20-35% produkcji glownej, max 8/dzien, do wioski **plus dodatkowe 45%
  tej ilosci tworzone na targu miasta** (kopia, nie przeniesienie).

### 3.4 Pobor surowcow przez warsztaty
- `ConsumeInputs` (:3034-3083) + receptury (:2811-2889): BE dodatkowo zdejmuje z targu `WorkshopDailyInputDrawPerShop (1.0) x pokrycie`
  na warsztat dziennie dla kazdego wejscia receptury - **drugie zuzycie surowca obok vanilla/BK** (bez wyrobu).
  Ma sens tylko z `WorkshopProductionPatch`, ktory przy BK jest wylaczony (hipoteza: czysty dodatkowy drenaz rudy/drewna/skor).

### 3.5 ProductionProfiles / CulturalGoods
- `ProductionProfiles.Mult` (np. sturgia iron x1.15, battania hardwood x1.3, khuzait horse x1.5) - **martwe** przy BK
  (uzywa go tylko BEE_VillageProductionCalculatorModel i CulturalMarket). Do tego id kultur vanilla - w ROT `battania` = Polnoc itd.
- `CulturalGoods` (LordInvestmentCampaignBehavior.cs:230-260): inwestycja lorda tworzy na targu towar kultury
  (zboze, drewno, ryby...) - **bez broni**.

---

## 4. ROT (ROT-Core / ROT-Content / ROT-Map)

- ROT.dll **nie ma** zadnego zachowania targu ani produkcji: brak wlasnych modeli cen/warsztatow/produkcji wsi,
  brak patchy na Workshops/ItemConsumption/Caravans. `ROTTownTradersBehavior` to 4 handlarzy dialogowych dla gracza
  (mak 1000, Nieskalani 1000, giganci 10 000, Golden Company 200) + ulepszanie jakosci przedmiotu za zloto (`FindUpgradeCost`).
- ROT nie definiuje typow warsztatow (`WorkshopType` sa tylko w BK i SandBox).
- Przedmioty: `ROT-Content/ModuleData/ROTassets.xml` 1177 Item + 143 CraftedItem, `items.xml` 32;
  **jawna kategorie maja prawie tylko konie** (18+12 horse, noble/war_horse) - bron i zbroje dostaja kategorie
  automatycznie z typu i tieru (light/medium/heavy/ultra_armor, melee_weapons_1-5 itd.), wiec trafiaja do linii warsztatow.

---

## 5. Dynamic Troop Equipment Reupload (DTE)

Ustawienia gracza (`DynamicTroop/DynamicTroopSettings.json`): DropRate 1.0, Difficulty 1.0, **CommandersGreed true**,
ScrapCapPerCategory 600, UseVanillaLootingSystem false. (`bannerlord.dynamictroop.json` z DropRate 0.5 to stary plik - mod czyta MCM `DynamicTroopSettings`.)

### 5.1 Zrodla - darmowe zbrojownie AI (EveryoneCampaignBehavior.cs)
Dotyczy partii prowadzonych przez bohatera AI (`IsValid`, MobilePartyExtension.cs:39). Nic z tego nie przechodzi przez targ.
- Dziennie `AllocateRandomEquipmentToPartyArmory` (:425-466):
  - z wojska: `floor(ludzie / 40) + 1` losowych sztuk ze slotow wzorcow (MobilePartyExtension.cs:115-184; 40 = max(5, 50-10xDifficulty));
  - z lenn: dla KAZDEGO miasta/zamku klanu: 1 szt. za kazda nienapadnieta wioske + 1 za miasto nie w oblezeniu,
    tier <= `2 x poziom_prosperity + 1` (0-based), kultura miasta (TownExtension.cs:10-48) - **kazda partia klanu dostaje to osobno**;
  - z klanu: `tier_klanu + 2` sztuk tieru <= tier_klanu+1 (:186-227).
  - Przyklad: 200 ludzi, klan tier 4, 2 miasta po 3 wioski -> 6 + 8 + 6 = **~20 szt./dzien na partie**.
- Przy powstaniu partii i przy werbunku: pelny zestaw wzorca na kazdego czlowieka (:468-508, :870-930).
- Karawany posilkowe `CutTheirSupplyBehavior` (:316-344, :547-552, :954-1059): miasto AI z prosperity >=2000,
  szansa 1% + (p-2000)x1e-6 (max 12.5%)/dzien; wiezie **17 szt. KAZDEGO rodzaju jedzenia, 17 sumpter_horse**, 25 ludzi
  i do 26 losowych sztuk sprzetu T1-T6; sprzet trafia do zbrojowni partii docelowej (:1086-1121). Wszystko z niczego.

### 5.2 Ujscia i pieniadze
- Co tydzien (dzien tygodnia = Id % 7) `GarbageCollectEquipments` (:256-309): nadwyzka sztuk danego typu **ponad liczbe
  ludzi** jest kasowana (najslabsze najpierw). Uwaga: progi `EquipmentAndThresholds` (:41-61: max(2x, ludzie+100))
  sa zdefiniowane, ale tu uzywana jest sama liczba ludzi.
- Co tydzien `MoveRosterToArmory` (:368-423): kazda bron i zbroja z TABORU partii AI idzie do zbrojowni, a lider dostaje
  **`Value x ilosc` zlota z niczego** (:419). Dotyczy lupu vanilla, zakupow BK PartySupplies i ladunku.
- Zniszczona partia: zbrojownia znika (:510-527).
- Zbrojownia gracza (ArmyArmoryBehavior.cs): co 3 dni scrap do 599/typ - **wylaczony** (dziala tylko przy CommandersGreed=false, :267-290);
  niewola gracza = -80% zbrojowni (:300-352). CommandersGreed=true pozwala wyjmowac i sprzedawac sprzet ze zbrojowni.

### 5.3 Lupy
- Bitwa AI vs AI (`DistributeLootRandomly` :751+): **cala zbrojownia pokonanych** przechodzi do zbrojowni zwyciezcow
  proporcjonalnie do sily - nie na targ.
- Bitwa gracza, osobiscie (`ItemRosterForPlayerLootSharePatch` :22+): lup = zbrojownie pokonanych x udzial gracza x DropRate.
- Bitwa gracza, symulowana (`DistributePlayerSimulationLoot` :570-729): z kazdego zabitego/rannego losowy zwyciezca;
  zolnierz T3/T4/T5/T6 traci jeden pancerz z p=0.25/0.35/0.45/0.6, reszta slotow z p=DropRate; dla gracza do **ArmyArmory**.
- Lup gracza to glowna droga wysokotierowego sprzetu NA TARG (przez sprzedaz gracza).

---

## 6. Spoils of War (`RealisticLoot.dll`, ustawienia `RealisticLoot/RealisticLootSettings.json`)

Dotyczy **tylko bitew gracza** (LootCollectionBehavior.cs:717-722 `if (!mapEvent.IsPlayerMapEvent) return`).
- `SkipVanillaLootPatch` (RealisticLoot.Patches): dla bitwy polowej (nie morskiej, nie w rozmowie) vanilla `DoLootInventory` jest pominiete - lup liczy SoW.
- `ProcessSingleCasualty` (RealisticLootModel.cs:631-712): na zabitego 1 szt. (T<4) lub 2 (T>=4), +1 przy Roguery 150;
  szansa slotu = Drop% (100) x mnoznik tieru 0.10/0.18/0.28/0.36/0.43/0.50 (T>=6 0.58) x kary; zniszczenie drogich (>5000) x `DestructionChanceMultiplier`=0 (wylaczone);
  stan 40-85% (EnableDamageSystem) -> modyfikatory obnizajace cene. Do tego zloto "z trupow" (GetGoldFromBody).
- Ujscia bez targu (przedmiot ZNIKA, zloto z niczego):
  - paser (`ExecuteFenceSale` :2549-2620): 60% wartosci, limit 5000 + 2 x prosperity, cooldown 3 dni, ryzyko -1 security;
  - salvage (QuartermasterBehavior.cs:1426-1470): 15% wartosci dla gracza;
  - auto-sprzedaz z magazynu wojennego (`OnDailyTick` :1559-1717): do 5 szt./dzien o wartosci <=1000, 40% wartosci -
    **zloto trafia do kasy MIASTA (`Town.ChangeGold`), nie do gracza**, przedmioty znikaja;
  - darowizna dla strazy (DonateEquipmentBehavior.cs:184-193): +security/+milicja, przedmioty znikaja.
- Naprawa 30% wartosci (RepairCostPercent) - zloto gracza znika.

---

## 7. Armoury (nasz mod)

### 7.1 Ceny: MarketGlut + SupplyDemand
- `SupplyDemand` (SupplyDemand.cs): koszyk = typ x tier (16 typow uzbrojenia + konie + uprzeze, :51-76).
  Popyt `D = 4 x clamp(prosperity/3000, 0.3, 3) x waga_tieru` (t1 1.0, t2 0.9, t3 0.7, t4 0.5, t5 0.3, t6 0.15), zamek x0.5 (:89-101).
  Mnoznik ceny `((D+1)/(S+1))^0.5` w granicach 0.25-2.0 (:116-125), dla kupna i sprzedazy, gracza i AI, tylko w miescie/zamku (:137-171).
  Przyklad: miasto 3000 prosperity, miecz T1 - D=4; przy 30 sztukach na polce cena x0.36; przy pustej polce x2.
- `DailyTrade` (:182-276) codziennie: 15% nadwyzki (S-D) jedzie do NAJBLIZSZEJ osady w promieniu 250, ktorej brakuje
  (`ceil(D) - S`); odbiorca placi `Value x 50% x mnoznik zrodla` z `Town.Gold`. **Nic nie powstaje i nic nie znika**;
  gdy nikomu nie brakuje - zostaje.
- `MarketGlut` (MarketGlut.cs:80-103): przy SupplyDemand tylko podloga 5% wartosci dla sprzedawanego lupu gracza.
- Popyt nie zalezy od wojen/armii (swiadomie, CHANGELOG 04.10 (6)).

### 7.2 Kuznia: materialy na sztuke
Rozdzielone na dwie sciezki, ktore licza **roznie** (patrz sprzecznosc 9.1):

| Wyrob | Kwit Armoury `Recipes.For` (wlasne menu, naprawa wlasnym metalem, PRZETOP) | Kwit w zakladce CRAFT BK (`TrueArmourCost.MaterialsPostfix`) |
|---|---|---|
| Pancerz metalowy (Plate/Chain) | Recipes.cs:224-245: `ceil(waga x 0.8 / 0.5)` metalu swojego tieru + `ceil(waga x 0.2 / 0.5)` tier nizej; +1 skora dla korpusu i ladrow | Patches.cs:202-220: `u = round(punkty_ochrony/10 x (1+0.15 x (Tierf-1)) x 0.5)`; 90% glowny gatunek BK + reszta drugi; podszewka max(1,u/6) <= tier (skora albo len) |
| Pancerz skorzany | :203-215: `u` jak obok, miekkie max `tier` (67% skora, reszta len), nadwyzka w zelazo tieru | :188-200: to samo |
| Pancerz tkaninowy | :192-202: len max `tier`, reszta zelazo | :178-186: to samo |
| Luk / kusza | :258-267: drewno `ceil(waga x 0.75/10)`, metal tieru `ceil(waga x 0.25/0.5)`, cieciwa 1 len (albo skora); T5-6 x2 | to samo (kwit z Recipes przelozony na tablice BK, Patches.cs:153-165) |
| Strzaly / belty | :268-284: metal tieru `floor(waga x stack / 0.5) x 3` (seria 3 kolczanow) + 1 drewno | to samo |
| Bron biala (tylko naprawa/przetop/zamowienia) | :311-347: `waga x 1.4 x klasa` rozbite 35/35/30% na 3 gatunki (T>=3), wegiel 0.6/jedn., T1-2 len, T3-4 skora, T5-6 skora + **velvet** | BK oryginal: 2 drewna + 2 metalu |
| Tarcza | jak bron (klasa 0.55) + drewno `ceil(waga)` | BK: 2 drewna (+metal dla metalowych) |

Mapowanie tier -> metal (Recipes.cs:411-426): T1-T2 `ironIngot2` (Wrought Iron), T3 `ironIngot3`, T4 `ironIngot4`, T5 `ironIngot5`, T6 `ironIngot6` (Valyrian).
Przetop (SmeltTab.cs:51-58 -> Recipes.SmeltYield :579-602): najwyzej 50% metalu z `Recipes.For`, Iron6 jeszcze /2.
Legenda (NotMerchandise, Value>=25000): wszystko x4 + Iron6 x tier (:135-150).

Przyklady (ceny z items-dump.csv, mediany wartosci z tego samego pliku):

| Przyklad | Kwit | Koszt materialow | Wartosc | Koszt/wartosc |
|---|---|---|---|---|
| Korpus T1 skora 1.6 kg / 54 pkt | 1 leather + 2 Iron2 | 290 | 1469 | 19% |
| Korpus T3 kolczuga 10 kg / 104 (Recipes) | 16 Iron3 + 4 Iron2 + 1 leather | 1310 | 3054 | 42% |
| ten sam w CRAFT BK | 7 Iron3 + 1 linen | 665 | 3054 | 21% |
| Korpus T5 plyta 18 kg / 142 (Recipes) | 29 Iron5 + 8 Iron4 + 1 leather | 5670 | 22200 | 25% |
| ten sam w CRAFT BK | 10 Iron5 + 1 Iron4 + 1 leather | 1930 | 22200 | 8% |
| Korpus T6 33 kg / 170 (Recipes) | 53 Iron6 + 14 Iron5 + 1 leather | 16250 | 40316 | 40% |
| ten sam w CRAFT BK | 14 Iron6 + 1 Iron5 + 2 leather | 4260 | 40316 | 10% |
| Helm T4 metal 1.5 kg / 128 (Recipes) | 3 Iron4 + 1 Iron3 | 360 | 14860 | 2% |
| ten sam w CRAFT BK | 9 Iron4 + 1 leather | 1130 | 14860 | 7% |
| Miecz 1h T3 1.14 kg | 1 Iron3 + 2 Iron2 + 1 wegiel + 1 leather | 400 | 1908 | 20% |
| Miecz 1h T5 1.23 kg | Iron5+Iron4+Iron3 + 2 wegiel + leather + velvet | 1225 | 11383 | 10% |
| Tarcza T3 4.7 kg | 2 Iron3 + 4 Iron2 + 3 wegiel + 3 leather + 5 drewna | 1205 | 568 | **212%** |
| Luk T3 0.4 kg | 1 drewno + 1 Iron3 + 1 len | 330 | 3225 | 10% |
| Luk T6 0.3 kg | 2 drewna + 2 Iron6 + 2 len | 1060 | 115000 | 1% |
| Strzaly T3 (3 kolczany) | 12 Iron3 + 1 drewno | 745 | 690 | 107% |
| Strzaly T6 (3 kolczany) | 18 Iron6 + 1 drewno | 4705 | 21606 | 21% |

UWAGA: ceny sztabek z dumpu (20-260) sa **duzo nizsze od kosztu ich wytopu** (tabela 8.2) - a sztabek nikt
w swiecie AI nie produkuje, wiec realny koszt dla gracza liczy sie w rudzie i drewnie.

### 7.3 Inne zrodla/ujscia Armoury
- `Stables` (Stables.cs:353-472): przy wjezdzie lorda do osady, tylko pod czekajace awanse na jezdnego (+2 zapasu),
  max 10/wizyte, co 4 dni, max 15% sakiewki; z targu max 25% polki i nigdy ostatnie 4 (:404-439); reszta od **hodowcy:
  kon z niczego za Value x1.3** (:444-461); zloto lorda idzie do osady (:466). Log X.2026: **655 zakupow, 1932 koni,
  814 z targu, 1118 od hodowcy, w 435 wizytach polka byla pusta**.
- `Orders` (Orders.cs): 50% szansy przy wjezdzie, co 3 dni na miasto, tier 2-5, Value <= 12000; dostawa = przedmiot znika,
  gracz dostaje **Value x1.35 z niczego** (:240-248).
- `SmithMenu` "Order kit for the men" (:1195-1302): najtansza kupna sztuka typu/tieru, gracz placi Value x1.15 (zloto znika),
  sztuki powstaja na polkach zbrojowni DTE.
- `TroopSelfMend` (TroopSelfMend.cs:22-70): codziennie w miescie 10% zuzytych sztuk zbrojowni (min 3) wraca do pelnego stanu -
  "z zoldu", ale **zadne zloto nie schodzi** (wartosc z niczego).
- Naprawa u kowala (SmithMenu.cs:397/410): `Value x (1-mnoznik) x 0.5 / 2` - zloto znika.
- `BkSupplyTemper` - ciecie zapasow BK (patrz 2.6). `LegendaryLaw`, `Uniques`, `ElephantQuarantine` - zdejmuja pojedyncze sztuki z polek.

---

## 8. Surowce do uzbrojenia

### 8.1 Tabela surowcow

Ceny = `value` z `items-dump.csv`. "Zrodlo AI" = co w swiecie produkuje bez gracza.

| Surowiec (id) | Cena | Waga | Zrodlo (wioska / warsztat / mod) | Kto zuzywa |
|---|---|---|---|---|
| Ruda zelaza (`iron`) | 50 | 10 | iron_mine 10/dzien (26 wiosek); BK Mines (wioska, miasto, warsztat `mines`) z MineralData; BK artisans 1.25/dzien z niczego | smithy, BK weapon/armor/barding-smithy, BK artisans (8.8/dzien), BE Armory, BK budowy, BK party (nie), gracz: rafinacja |
| Drewno twarde (`hardwood`) | 25 | 10 | lumberjack 18/dzien (45 wiosek); BK artisans 1/dzien | fletcher, weaponsmithy (T1-2), wood workshop, artisans (7.1/dzien), BE Armory, BK budowy, BK party (0.02/zolnierz), wegiel drzewny, Armoury: luki/kusze/strzaly/tarcze |
| Wegiel (`charcoal`) | 50 | 0.5 | **tylko rafinacja gracza** (2 drewna -> 1, perk 3) | rafinacja kazdego gatunku, Armoury bron biala (0.6/jedn. metalu) |
| Crude Iron (`ironIngot1`) | 20 | 0.5 | tylko rafinacja/przetop gracza | rafinacja (ruda+wegiel -> 2) |
| Wrought Iron (`ironIngot2`) | 30 | 0.5 | j.w. | Armoury T1-T2, BK tab (len/skora - reszta w zelazo) |
| Iron (`ironIngot3`) | 60 | 0.5 | j.w. | Armoury T3, BK tab T<4 |
| Steel (`ironIngot4`) | 100 | 0.5 | j.w. (perk SteelMaker) | Armoury T4, BK tab T4 |
| Fine Steel (`ironIngot5`) | 160 | 0.5 | j.w. (perk SteelMaker2) | Armoury T5 |
| Valyrian Steel (`ironIngot6`) | 260 | 0.5 | j.w. (perk SteelMaker3; Armoury: wsad x2, ValyrianSteel.cs) | Armoury T6, legendy |
| Skory surowe (`hides`) | 50 | 10 | **zadna wioska**; BK artisans 0.6/dzien z niczego; butcher (krowa 5, owca 2, swinia 2); BE secondary (wsie zwierzat/koni) | tannery, BK armorsmithy (3/dzien), barding-smithy (8.1/dzien), artisans |
| Skora (`leather`) | 230 | 10 | tannery (1 hides -> 2); BK budynek wioski Tannery 0.5 x poziom; BK artisans 0.2/dzien; BE secondary | BK tab (pole 9), Armoury (pancerze skorzane, podpinka, bron T3+, tarcze), BE Armory, BK artisans (uprzeze), vanilla artisans |
| Len surowy (`flax`) | 15 | 10 | flax_plant 18/dzien (19 wiosek) | linen_weavery, barding-smithy, BK party (tkanina) |
| Plotno lniane (`linen`) | 245 | 10 | linen_weavery (1 flax -> 2); BK artisans 0.5/dzien; BE secondary (wsie lnu/welny) | BK tab (pole 10), Armoury (pancerze tkaninowe, podszycie, bron T1-2, cieciwy), BK party |
| Welna (`wool`) | 22 | 10 | sheep_farm 10/dzien | wool_weavery (garment/felt); Armoury: zamiennik lnu tylko gdy brak itemu `linen` |
| Velvet (`velvet`) | 575 | 10 | velvet_weavery z `cotton` (Raw Silk, silk_plant 8/dzien, 13 wiosek) | Armoury: bron biala/tarcze T5-T6 |
| Futro (`fur`) | **1** | 10 | trapper 1.4/dzien | Armoury: zamiennik skory w `Resolve` tylko gdy brak `leather`; mapowany na pole 9 w kwitach strzeleckich |
| Narzedzia (`tools`) | 250 | 10 | smithy (1 ruda -> 4); BK artisans; BK budynek wioski Blacksmith 0.5 x poziom; BE secondary (wsie drewna/rudy) | BE Armory, BK party (0.01/zolnierz), BK budowy |
| Konie (`horse` i in.) | 99-30000 (mediany wg tieru) | - | rancza ~3.3-5.7/dzien (71 wiosek); wheat_farm zwierzeta; BK stadniny (stash); Armoury hodowca | Armoury awanse jezdnych, BK party (0.001/zolnierz), konsumpcja miasta (popyt 140) |

Anomalie cen w dumpie: `fur` 1, `marble` 1, `mead` 1, `gold_ore` 4, `limestone` 0, `honey` 0 - prawdopodobnie przedmioty
BK/ROT bez ustawionej wartosci; `fur` jako zamiennik skory jest przez to bezwartosciowy w handlu.

### 8.2 Koszt sztabek w surowcu (vanilla `GetRefiningFormulas`, Armoury x2 dla Iron6; zwrot crude odliczony)

| Sztabka | Ruda | Drewno | Koszt surowca | Cena itemu |
|---|---|---|---|---|
| Wegiel | 0 | 2 | 50 | 50 |
| Crude | 0.5 | 1 | 50 | 20 |
| Wrought | 0.5 | 3 | 100 | 30 |
| Iron | 0.5 | 7 | 200 | 60 |
| Steel | 0.5 | 15 | 400 | 100 |
| Fine Steel | 0.5 | 31 | 800 | 160 |
| Valyrian | 1.5 | 127 | 3250 | 260 |

Wniosek: **waskim gardlem gracza jest drewno** (wegiel). Korpus T5 z prawa wagi (29 Fine + 8 Steel) to ~1020 sztuk
drewna i ~19 rudy (~26 tys. denarow surowca przy wartosci 22 tys.); korpus T6 (53 Valyrian + 14 Fine) - ~7200 drewna.
Lumberjacki calego swiata daja ~810 drewna dziennie.

---

## 9. Sprzecznosci miedzy modami (i wewnatrz)

1. **Armoury: dwa rozne kwity na ten sam pancerz metalowy = petla kucie -> przetop.** Zakladka CRAFT BK liczy z punktow
   ochrony (Patches.cs:202-220), przetop z prawa wagi (Recipes.cs:224-245 przez SmeltTab.cs:54-57). Korpus T5 18 kg:
   kucie 10 Fine + 1 Steel + 1 skora, przetop przy udziale 50% oddaje 14 Fine + 4 Steel. **Kazdy cykl daje +4 Fine i +3 Steel
   z niczego** (koszt: stamina i 1 skora). T6 33 kg: kucie 14 Valyrian, przetop 13 Valyrian + 7 Fine. Komentarz w
   `Recipes.ArmourUnits` (:83-86) twierdzi, ze wszystkie sciezki widza te sama liczbe - po prawie wagi z 01.09 to nieprawda;
   CHANGELOG 01.09 sam zapisal "sprawdzic, czy odzysk nie przewyzsza nowego kwitu (ryzyko dojenia)". Do potwierdzenia w grze.
2. **Darmowy sprzet AI (DTE) vs prawo podazy i popytu (Armoury).** AI dostaje sprzet z niczego i nie kupuje go na targu,
   wiec popyt na bron pochodzi tylko od gracza, BK PartySupplies, stajni i konsumpcji ludnosci. SupplyDemand "kupcy wywoza
   nadwyzke" nie ma odbiorcow, gdy wszedzie jest nadwyzka - niskie tiery zostaja tanie wszedzie.
3. **Drukarnie pieniedzy:** DTE `MoveRosterToArmory` (Value za kazda sztuke z taboru AI, tygodniowo), Armoury Orders (Value x1.35),
   SoW paser/salvage/auto-sprzedaz (zloto bez kupca; auto-sprzedaz zasila kase miasta), BE kontrakty uzbrojeniowe (marza karawany bez placacego),
   BK ItemConsumption (miasto dostaje zloto za "zjedzony" towar). **Czarne dziury:** BK PartySupplies (zloto lorda znika),
   Armoury "Order kit" i naprawy, SoW naprawa, BE budowa Armory.
4. **Arbitraz BK + DTE:** partia AI kupuje miecze T2-3/tarcze/strzaly po cenie targu (BK), a DTE w ciagu tygodnia wciaga je do
   zbrojowni i wyplaca pelna Value - przy SupplyDemand (cena do x0.25 na zawalonym targu) lord zarabia na kazdym zakupie.
5. **BK PartySupplies liczy stan polki zamiast taboru** (PartySupplies.cs:365) - im wiecej towaru na targu, tym mniej AI kupuje.
6. **BK DeleteOverProduction kasuje wyroby gracza z targow** - sprzedaz wlasnej broni miastu niczego nie dodaje do podazy
   (z punktu widzenia SupplyDemand: sztuka obnizyla cene w chwili sprzedazy, potem znika).
7. **BE w trybie BK:** ProductionProfiles i ceny BE martwe, ale BE nadal zdejmuje surowce warsztatom (`ConsumeInputs`) i robi
   karawany wojskowe biorace rude/drewno/skory z wiosek **bez zaplaty**; Armory BE i BK artisans konkuruja o te same 4 surowce.
8. **Lup gracza moze liczyc sie dwa razy (hipoteza):** w bitwie symulowanej DTE `DistributePlayerSimulationLoot` daje lup do
   ArmyArmory, a SoW pomija vanilla `DoLootInventory` dla bitwy polowej i liczy wlasny lup - do sprawdzenia w logu SoW i DTE po
   jednej bitwie symulowanej.
9. **ROT kultury vs mody:** BE `ProductionProfiles`/`ScoreCultureArmoryFlavor`, BK `MarketGroup` i `GetProductions` (kury/gesi wg
   aserai/sturgia/...) uzywaja vanillowych id kultur - w ROT `battania` = Polnoc, `vlandia` = Westerlands itd.
10. **Stajnie Armoury vs rynek koni:** 57% koni AI pochodzi od "hodowcy" z niczego, bo polki sa puste - a konie BK PartySupplies
    i konsumpcja ludnosci (popyt 140) tez je zjadaja.

---

## 10. Luki i rzeczy do sprawdzenia

- **Sztabek i wegla nie produkuje nikt w swiecie AI** - ani wioska, ani warsztat. Kwity Armoury wymagaja sztabek; gracz musi
  rafinowac (drewno!) albo przetapiac lup. Ceny sztabek w dumpie (20-260) sa oderwane od kosztu wytopu (50-3250).
- **Brak wioskowego zrodla `hides`**, a od skor zalezy cala skora/len na pancerze (tannery) i zbrojmistrz BK.
- Brak danych o rzeczywistym stanie polek z broni - CrashScribe nie spisuje targow. Dobrze byloby dopisac do CrashScribe
  (tylko odczyt) spis: ile sztuk kazdego koszyka typ x tier lezy na targu kazdego miasta raz na dzien.
- Brak logu SupplyDemand z gry (funkcja z 04.10) - linie `PodazPopyt: kupcy wywiezli` / `bez odbiorcy` pokaza, czy teza z pkt 9.2 jest prawdziwa.
- Brak logu BE armory (verbose wylaczony) - nie wiadomo, czy jakiekolwiek miasto ma Armory BE.
- Liczba warsztatow danego typu w swiecie nieznana bez zapisu gry (6 slotow/miasto; slot 0 = artisans).
- `fur` = 1 denar i inne zerowe wartosci (limestone, honey, marble, mead) - sprawdzic definicje tych itemow.
- Tarcze i amunicja T3 w kwitach Armoury kosztuja wiecej niz sa warte (212%, 107%) - nieoplacalne do kucia.
- `ProductionProfiles` BE i `HandleExcessWorkforce` BK - martwy kod; `WorkshopData.DoProduction` BK - martwy.
- Wyliczenia wydajnosci warsztatow sa z XML przy predkosci 1.0; mnozniki polityk (vanilla `GetEffectiveConversionSpeedOfProduction`)
  i warunek oplacalnosci moga je obnizyc - szczegoly w audycie vanilla.
