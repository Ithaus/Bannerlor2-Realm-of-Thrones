# Etap 6 - rozpoznanie B: kod (11.10.2026)

Zakres: PLAN-DO-KONCA-MODA-2026-10-09.md, "Etap 6. Rzemioslo, surowce i konie", pozycje 6.1-6.19, z decyzjami Jeffa 11.10 17:30 (STAN-PRAC:1190):
6.1 wytop przy kopalni + przetop - TAK; 6.14 rycerz z wlasnym koniem, jazda AI ok. 20% - TAK; NOWE 6.19 kowale kuja wedlug kultury swojej krainy, nikt
nie kuje unikatow. Dla kazdej pozycji: co juz jest w kodzie Armoury (plik:linia), co jest w grze / BK / BEE / DTE / ROT, co trzeba zmienic, jakie latki
(sygnatura, kto wola, kolejnosc z naszymi). Tylko odczyt - kod, gra, zapisy i ustawienia nietkniete.

Skroty:
- SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad`
- KOD = `SCR\e4\koniec\Armoury\src` (etap 4 + E5/0). Bez przedrostka plik = KOD.
- GRA = `SCR\dec32\cs_full` (TaleWorlds 1.4.8), BK = `SCR\e4tools\bkfull`, DTE = `SCR\e410\dte\DynamicTroopEquipmentReupload`, BEE = `SCR\e3\dec36\bee`.
- ROT = `Modules\ROT-Content\ModuleData` (ROTassets.xml - przedmioty, ROT-Troops.xml - jednostki, spcultures.xml), MAPA = `Modules\ROT-Map\ModuleData\settlements.xml`.
- LOG = `SCR\kopia-z31-A\Armoury-2026-10-11_08-09-11.log` (120 dob nowej kampanii, zbior 31). d31-120 = srednia dob 31-120.
- DUMP = `Documents\Mount and Blade II Bannerlord\CrashScribe\items-dump.csv` (zrzut przedmiotow z gry PO prawach CS: tier = (int)Tier + 1, merchandise).
- Spis kultur przedmiotow: `SCR\e6B\census.py` (kultura z XML wszystkich modulow w kolejnosci ladowania z LauncherData.xml: Native, SandBoxCore,
  BK, NavalDLC, RBM, RBM_WS, ROT-Content, ROT-Dragon, DismembermentPlus - pozniejszy wygrywa) + DUMP -> `SCR\e6B\items-culture.csv` (2560 sztuk
  uzbrojenia i koni z towaru), `census.txt`, `coverage.txt`; pule warsztatow `pools.py`, `pools2.py`; co nosza zolnierze `worn.py`.
- [K] kod, [P] pomiar z logu, [W] wniosek z kodu (nie sprawdzony w grze), [X] dane XML/DUMP.

---

## 0. Najwazniejsze

1. **6.19 - warsztaty miast juz filtruja po kulturze, ale z trzema dziurami.** `WorkshopLaw.Candidates` (WorkshopLaw.cs:496-575) bierze z linii
   tylko sztuki "lokalne" = kultura przedmiotu == `town.Culture` albo brak kultury albo `neutral_culture` (:525); gdy lokalnych w calej LINII brak -
   wszystkie obce (:530). Tak samo strzelarze (TownFletchers.cs:396, :407 - per koszyk), dorobek startowy (ColdStart.cs:154, :171 - bez obcych) i gra
   (GRA WorkshopsCampaignBehavior.cs:875-882 `IsItemPreferredForTown`, :1039-1047). Dziury: (a) **bron bez kultury jest "lokalna" wszedzie** - ROT ma
   77 ze 144 broni kutej (CraftedItem) bez kultury, w tym `lannister_sword`, `lannister_kite_shield`, `lion_sword` "Kingslayer's Sword", `martell_sword`, `velaryon_cutlass`,
   `katana` (Yi Ti), `unsullied_sword`, `rapier` (Braavos) - Winterfell je dzis kuje [X]; (b) **linie tarcz t1-t2, lukow t3-5 i uprzezy t2/t4/t5 nie maja
   ani wlasnych, ani neutralnych sztuk u 28 z 30 kultur** - wtedy cala pula obca (Polnoc kuje tarcze Westerlands i Imperium) [X]; (c) **kultura miasta
   to biezaca kultura BK**, ktora BK przestawia asymilacja (BK CultureData.cs:200-206, prog 0.55; BKManagerBehavior.cs:171-180 przy kazdym starcie
   sesji) - zdobyte miasto z czasem kuje zbroje zdobywcy [W].
2. **6.19 - zbroje: wlasne wzory sa, ale nierowno.** 7 kultur "z vanilli" (battania=Polnoc 367 sztuk uzbrojenia, vlandia=Westerlands 288,
   aserai=Dorne 253, empire=Braavos 197, sturgia=Zelazne Wyspy 169, nord=Lorath 144, khuzait=Dothrakowie 136) ma pelne pokrycie tierow; 23 kultury
   tylko-ROT maja 7-89 sztuk i dziury w t1-t3 (np. Dorzecze bez wlasnego korpusu t2, Ibben/Qarth/Valyria/Skagos bez korpusu t1-t3, Sarnor bez korpusu
   t3-t6) i prawie bez wlasnej broni [X] (rozdz. 1.4). Neutralne "lokalne wszedzie" to w zbroi glownie stroje bandytow RBM_WS (`tacky_bandit*`,
   `bandit_hybrid_*`, `bandit_garb/cap/shoes`) - kuje je kazde miasto.
3. **6.19 - unikaty: warsztaty notabli ich nie kuja** (WorkshopLaw.cs:523: `ArmsPricing.IsUnique` = NotMerchandise albo zakaz bitwy albo 157 id
   z `RotUniques.cs:11-41`, w tym caly komplet Ramsaya; `LegendaryLaw.IsLegend`; `WorkshopForbiddenIds`). Dziura: **warsztat GRACZA z wsadem i start gry
   ida sciezka gry** (WorkshopLaw.cs:233-234, PlayerCyclePrefix :1516 przepuszcza linie z wsadem) - `GetRandomItemAux` nie zna unikatow, a slownik
   `_itemsInCategory` gra buduje PRZED `UniqueSpoils.OnSessionLaunched` (GRA :138, :183 wobec UniqueSpoils.cs:100-108) - linia ultra_armor (2 rudy + skora)
   w miescie Polnocy moze dac helm Ramsaya [W]. Plus `lion_sword` "Kingslayer's Sword" nie jest w spisie unikatow (decyzja Jeffa).
4. **6.1 - kodu wytopu nie ma.** Ruda `iron` = ladunek 100 kg (HistoricalPrices.cs:350-357, `HistBulkUnitFactor` 10), wydobycie 26 wsi `iron_mine`
   (MaterialLaw.cs:163-176: x `MineOutputMultiplier` 3 x `MineOutputStep` 1.5 / BulkScale), dymarka stoi w MIESCIE (WorkshopLaw.Needs :160-176: 5 ladunkow
   drewna na ladunek rudy + 12.5 kg drewna na kg metalu). [P d120]: wsie 301 ladunkow/d, warsztaty zbrojne zuzywaja 242 rudy i **1472 ladunki drewna**,
   "brak rudy" 640 cykli, miast bez rudy 30-34 z 97. Projekt gotowy: K13 paczka 133 (PLAN-K13:213-234). "Przetop" = zlom: `ArmsScrap` (OldStockToScrap,
   0.5 rudy ze sztuki) i przetop gracza (SmeltTab, MaterialLaw refine :105, :218).
5. **6.14 - waskim gardlem sa awanse na konnego, nie konie.** [P d120] konni 8.4% w partiach lordow, 6.2% w zalogach (Dothrakowie 45%, Targaryen 30%,
   Dolina 13%, Polnoc 5%, Zelazne Wyspy 1%); 1095 ludzi czeka na awans z braku konia wlasciwej kategorii, awanse 9/d, zakupy lordow 28 koni/d (cooldown
   `AiMountBuyCooldownDays` 4), a konie na polkach rosna 2171 -> 11622 w 120 dob. Projekt audytu 14 (5.2): awans linii szlacheckiej/rodowej = rod
   kupuje konia we wsiach swoich lenn (potem targ), kon do zbrojowni jezdzca. Punkt wpiecia: `Stables.FilterTargets` (:591) i `PayInHorses` (:648).
6. **Inne dziury "nic z niczego / nic w nicosc" znalezione przy okazji (etap 6):** BK `DeleteOverProduction` = true u Jeffa - 2%/d stosow towaru > 500
   (BK BKSettlementBehavior.cs:306-308) - to 6.4; BK `RunStuds` (BKBuildingsBehavior) - kon bojowy z niczego do skrytki zamku (6.12); DTE
   `ScrapCapPerCategory` 600 (6.11/6.13); BK `SpawnEquipment` = false (wylaczone - OK).

---

## 1. 6.19 - kowale kuja wedlug kultury swojej krainy, bez unikatow

### 1.1 Skad dzis bierze sie wybor "co kuje" - wszystkie drogi [K]

| Droga | Kto | Pula przedmiotow | Kultura | Unikaty |
|---|---|---|---|---|
| A. Warsztaty notabli i ukryci "artisans" BK, linie samego uzbrojenia | `WorkshopLaw.CyclePrefix` (WorkshopLaw.cs:206) prefiks `WorkshopsCampaignBehavior.TickOneProductionCycleForNotableWorkshop` (GRA :750) -> `Candidates` (:496) | `_itemsInCategory` gry (GRA :970-988, `IsProducable` :990: nie MP, nie NotMerchandise, nie kute przez gracza) dla KATEGORII wyjscia linii; kategoria = tier przy wczytaniu (DefaultItemCategorySelector, TaleWorlds.Core): zbroja t1 garment, t2 light, t3 medium, t4 heavy, t5-6 ultra; bron/tarcze/luki/uprzaz 1-5 (t5 i t6 razem) | lokalne = brak / `neutral_culture` / `== town.Culture` (:525); pusto w linii -> wszystkie obce (:530); potem losowa probka 40 (`WorkshopCandidates`, Settings.cs:540) | odciete (:523) |
| B. Strzelarze miasta (strzaly, belty) | `TownFletchers.CandidatesOf` (TownFletchers.cs:387) | `All()` :359-380 | jak A, ale per koszyk typ x tier (:396, :407); pamiec na sesje (`_town`) - zmiana kultury miasta dopiero po wczytaniu | odciete (:372) |
| C. Dorobek startowy nowej gry | `ColdStart.Markets` (ColdStart.cs:141) | wszystkie przedmioty uzbrojenia | kultura miasta albo neutralne, BEZ obcych (:154, :171, :185-188) | odciete (:151) |
| D. Warsztat GRACZA z wsadem; kazdy warsztat przed `GameStarted` | gra: `GetItemsToProduce` -> `GetRandomItem` (GRA :823-844, :1039-1072) | jak A | `IsItemPreferredForTown` (GRA :875-882) = ta sama regula, fallback na cala kategorie (:1046) | NIE odciete; nasz postfiks `WorkshopLaw.RandomItemPostfix` (:1479, wpiety :1584) zmienia tylko wegiel/sztabki |
| E. Kuznia gracza | `SmithMenu.ShowItems` (SmithMenu.cs:2600-2630) | wszystkie przedmioty typu; NotMerchandise odciete poza lukami/strzalami (`BannedRanged` :2544 - tylko oblezenie/cwiczebne) | brak filtra | legendy raz (`Forge.LegendAllowed` :526, `Recipes.IsLegendary` Recipes.cs:126 = NotMerchandise i wartosc >= progu); opanowany wzor bez limitu (Jeff 30.08) |
| F. Zamowienia u kowala dla ludzi | `SmithMenu.OrderShelfTiers` (`TroopOrderFromShelf` = true) | polka TEGO miasta | jak polka | jak polka |
| G. BK `HandleItemAvailability` (BKSettlementBehavior.cs:493-536) | BK co tydzien | `GetRandomItemAux` | kultura miasta | - | **wylaczone** (`SpawnEquipment: false` w configu BK Jeffa) |

Wniosek: regula kultury jest w czterech miejscach (A, B, C, D) - kazde ma wlasna kopie warunku. 6.19 musi zmienic wszystkie cztery naraz jedna funkcja.

### 1.2 Kultura miasta to nie kraina [K]

- `town.Culture` = `Settlement.Culture`, a BK go zmienia: `CultureData.Update` (BK BannerKings.Managers.Populations/CultureData.cs:190-207) - gdy kultura
  dominujaca ma `BasicTroop` i asymilacja >= 0.55, `data.Settlement.Culture = dominantCulture`; `BKManagerBehavior` (BK :171-180) robi to przy kazdym
  starcie sesji dla wszystkich osad. Notable obcej kultury (zdobywca osadza swoich) dokladaja kulture do mieszanki (CultureData :195-199).
- U nas jest juz "kraina": `PeopleBook.CultureAtCalibration(s)` (PeopleBook.cs:834-840 - kultura z dnia kalibracji, stary zapis: z dnia wczytania)
  i `FreeHands.RealmOf(region)` (FreeHands.cs:83-100). Uzywa ich E4 (zima, wolne rece, populacja). 6.19 powinno brac krainy stad, nie z `town.Culture`.
- Miasta wedlug kultury (MAPA, 97 miast / 130 zamkow): battania 9, river 9, sturgia 6, ghiscari 6, aserai 5, vale 5, reach 5, dragonstone 4, vlandia 4,
  stormlands 4, volantine 4, ibbenese 3, qartheen 3, freefolk 3, po 2: tyroshi, pentoshi, crownlands, lyseni, nord, sarnor, qohorik, khuzait, myrish,
  empire, norvos; po 1: valyrian, skagosi, nightswatch, summer, yiti. (Uwaga: `reach` to wlasne id ROT - Reach NIE jest w `vlandia`.)

### 1.3 Unikaty - jak oznaczone i gdzie przeciekaja [K]

- `ArmsPricing.IsUnique` (ArmsPricing.cs:85-93) = `NotMerchandise` || `QuartermasterLaw.BarredInBattle` || id w `RotUniques.Ids` (RotUniques.cs:11-41, 157 id
  wedlug docs/ROT-UNIKATY.md: imie postaci, legenda/material z legend, korona, cena autora >= 100 000). `UniqueSpoils.OnSessionLaunched`
  (UniqueSpoils.cs:100-123) ustawia im `NotMerchandise` i zdejmuje kopie startowe z polek; `Uniques.cs` - korony; `LegendaryLaw.IsLegend` (LegendaryLaw.cs:329)
  - bron legendarna; `WorkshopForbiddenIds` (Settings.cs:520: weirwood, giant_, ravens_teeth, dragonglass, obsidian, val_steel, valyrian, dragonbone).
- DUMP potwierdza: `ramsay_armor/helmet/gloves/shoulders/shoulders2/boots` merchandise = 0 w grze (w XML ROT bez `is_merchandise`, kultura battania).
- Przeciek 1 (droga D): `FillItemsInAllCategories` biegnie w `OnNewGameCreatedPartialFollowUp` (GRA :131-139) i `OnGameLoaded` (:161-183), czyli PRZED
  `OnSessionLaunched` - w slowniku gry zostaja unikaty ROT. Warsztat gracza z linia z wsadem (armorsmithy BK: ultra_armor = 2 rudy + skora,
  BK workshops.xml :441-447) i warsztaty przed startem gry losuja z nich [W].
- Przeciek 2 (dane): `lion_sword` "Kingslayer's Sword" (t6, bez kultury, merchandise 1) - przydomek postaci w nazwie; dzis towar i kuty wszedzie.
  Reszta broni rodow bez imienia (Lannister Sword, Martell Sword, Velaryon Cutlass) wedlug zasady Jeffa z 05.10 to NIE unikaty, tylko "bron rodu".
- Przeciek 3 (do decyzji): kuznia gracza ma legendy (raz albo bez limitu po opanowaniu wzoru, Forge.cs:526-545) i luki spoza kramow
  (`weirwood_bow`, `giant_bow`, `dragonbone_bow`, `goldenheart_longbow` sa w RotUniques i w liscie lukow SmithMenu.cs:2611-2617). "Nikt nie kuje unikatow"
  stoi w sprzecznosci z decyzja 30.08 ("moge kuc wiecej niz jeden, jak juz opanuje") - Jeff musi powiedziec, czy dotyczy tez jego kuzni.

### 1.4 Ile wzorow ma kazda kultura [X]

Uzbrojenie z towaru (merchandise = 1 w DUMP), kultura z XML (ostatni modul). Rodzaje: helm / tulow / rece / nogi / plaszcz / tarcza / bron 1H / 2H /
drzewcowa / luk / kusza / rzucana / uprzaz / kon.

```
battania (Polnoc)   56 103 25 34 56 19 | 22 9 13 5 0 6 | 16 3   razem 367
vlandia (Westerl.)  82  54 15 12 31 43 | 11 9 10 2 8 3 |  5 3   razem 288
aserai (Dorne)      60  75  6  8 24 15 | 20 7  7 3 0 3 | 11 9   razem 253
empire (Braavos)    49  44  8  8 18 29 | 18 0  8 0 3 3 |  6 3   razem 197
sturgia (Zel.Wysp.) 30  52  6 11 11 19 | 13 8  7 1 0 3 |  5 3   razem 169
nord (Lorath)       27  27  8  7 14 11 | 21 7 13 0 0 7 |  2 0   razem 144
khuzait (Dothrak.)  19  31  7  7 18 13 | 14 1  8 5 0 4 |  5 4   razem 136
(brak kultury)       1   0  0  0  1  2 | 43 7 11 3 1 2 |  5 8   razem 127  <- "lokalne" w kazdym miescie
river               19  28  4 12 15  1 |  3 0  0 1 0 0 |  5 0   razem  89
vale                12  26  6 11 15  2 |  0 1  2 0 0 0 |  4 0   razem  79
crownlands          14  24 10  9 15  0 |  0 0  1 0 0 0 |  5 0   razem  78
stormlands          14  17  4  3 10  2 |  2 1  3 0 0 0 |  5 0   razem  61
reach               12  26  5  2  7  1 |  0 0  0 0 0 0 |  4 0   razem  57
dragonstone          8  15  5  2 11  0 |  0 1  2 0 0 0 |  2 0   razem  46
freefolk            12   8  2  6  3  3 |  3 1  1 0 0 1 |  1 1   razem  42
neutral_culture      7  10  4  7 12  0 |  0 0  0 0 0 0 |  0 0   razem  40  <- "lokalne" w kazdym miescie
volantine 34, tyroshi 33, qohorik 32, ghiscari 29, valyrian 26, pentoshi 26, lyseni 20, norvos 19, yiti 19, ibbenese 17, nightswatch 16, skagosi 16,
qartheen 16, myrish 15, summer 10, sarnor 7 (szczegoly SCR\e6B\census.txt)
kultury spoza listy ROT (literowki / kultury bandytow - nigdy "lokalne"): looters 25, Empire 9 (RBM, wielka litera), sea_raiders 8, Vlandia 4,
volantene 2 (Golden Company Boots, zamiast volantine), riverlands 2 (Scalemail with Short/Long Tabard t5, zamiast river), wights 1
```

Pokrycie tierow 1-6 WLASNYMI wzorami (cyfra = sztuk w tierze, `.` = brak; H helm, T tulow, R rece, N nogi, P plaszcz, S tarcza, W bron biala):

```
battania       H 917999 | T 949979 | R 58138. | N 69118. | P 9351.9 | S ..9622 | W 563996
river          H 22.366 | T 9.2654 | R .1.21. | N 18111. | P 6124.2 | S .....1 | W ....21
sturgia        H 5.5539 | T 968.58 | R .1113. | N 242.3. | P 2.2115 | S .159.3 | W .57692
ghiscari       H ..143. | T 1..13. | R .21... | N .3.... | P ..11.1 | S ..1.11 | W ....2.
aserai         H 964692 | T 9.5939 | R 12.21. | N 52..1. | P 5.2439 | S .5532. | W 292981
vale           H ...435 | T 7.3129 | R ...33. | N 23.15. | P 2....9 | S .1..1. | W ....3.
reach          H ...462 | T 9123.7 | R ...41. | N ...11. | P 11...5 | S ..1... | W ......
dragonstone    H 1..151 | T 4.2225 | R .1.13. | N .2.... | P ...1.9 | S ...... | W ....3.
vlandia        H 996999 | T 935459 | R .2256. | N 24123. | P 5311.9 | S 199648 | W 245891
stormlands     H 21.452 | T 8.2223 | R .1.12. | N .2..1. | P 3....7 | S ..2... | W ....33
volantine      H 1..125 | T 12.222 | R ...1.. | N ...... | P 1....5 | S .....2 | W ....1.
ibbenese       H .1..11 | T ...111 | R .1..1. | N .2..1. | P .....1 | S ...11. | W ....3.
qartheen       H ....11 | T ...11. | R 11.... | N 1...1. | P ...11. | S .....2 | W ....3.
freefolk       H 31125. | T 1.1321 | R .1..1. | N 15.... | P ...2.1 | S ..1.11 | W .1..5.
tyroshi        H 1..211 | T 1.2313 | R .1.4.. | N .3.... | P ...1.3 | S .....1 | W ....3.
pentoshi       H ...241 | T 1....3 | R .1..1. | N 11..2. | P .....3 | S .....2 | W ....2.
crownlands     H ...338 | T 93.244 | R 2512.. | N 13131. | P 5..1.9 | S ...... | W .....1
lyseni         H ...231 | T 1.21.1 | R ...2.. | N .1.... | P .....2 | S .....1 | W ....1.
nord           H 423693 | T 842535 | R 22121. | N 24..1. | P 433.13 | S .12332 | W 754998
sarnor         H ...11. | T 11.... | R .1.... | N ...... | P .....1 | S .....1 | W ......
qohorik        H ...252 | T 1.12.3 | R .1.21. | N .1.11. | P .....2 | S ....11 | W ....12
khuzait        H 822142 | T 932712 | R .61... | N .61... | P 55521. | S .22.27 | W 138942
myrish         H ....11 | T 1.2..1 | R .1..1. | N .1..1. | P .....2 | S ...1.. | W ......
empire         H 959996 | T 974344 | R .3212. | N 322.1. | P 362412 | S 684344 | W 147491
norvos         H ..1211 | T 1..11. | R ...2.. | N .2.... | P .....3 | S ...... | W ....11
valyrian       H ...1.1 | T ...1.2 | R .1.11. | N .1..1. | P .....4 | S .....2 | W ......
skagosi        H ....2. | T ...11. | R .2.... | N .1.... | P 1....1 | S .....2 | W ....11
nightswatch    H 2...2. | T 1.121. | R .1.... | N .3.... | P 2....1 | S ...... | W ......
summer         H ...23. | T 1.11.. | R ...1.. | N ...... | P ...1.. | S ...... | W ......
yiti           H .1..11 | T 1.1111 | R .2.... | N .1.... | P ...111 | S .....2 | W ....12
```

Pule linii warsztatu (pools.py, regula WorkshopLaw.Candidates; bez unikatow i Forbidden):
- **Zbroja:** neutralnych sztuk w liniach garment/light/medium/heavy/ultra jest 24/7/7/1/3, wiec cala-pula-obca nie wlacza sie dzis w zadnej linii zbroi -
  "Polnoc nie kuje zbroi Lannisterow" JEST juz prawda na poziomie id kultury. Ale gdy kraina nie ma wlasnej sztuki danego rodzaju w linii, ta linia
  daje tylko neutralne: brak wlasnego KORPUSU w linii: river/vale/aserai/dragonstone/stormlands/freefolk/tyroshi/lyseni/qohorik/nightswatch/yiti - light (t2);
  sturgia - heavy (t4); ghiscari, norvos - light+medium; volantine, crownlands - medium; pentoshi - light+medium+heavy; myrish - light+heavy;
  ibbenese, qartheen, valyrian, skagosi - garment+light+medium; sarnor - medium+heavy+ultra; summer - light+ultra.
- **Bron, tarcze, luki, uprzaz:** neutralnych w liniach mel1-5: 8/13/8/7/27, ran1-5: 3/1/0/0/0, tarcze 1-5: 0/0/0/2/0, uprzaz 1-5: 3/0/2/0/0. Wiec:
  tarcze t1-t2 cala pula obca u 28 z 30 kultur (wlasne maja tylko vlandia i empire t1, kilka kultur t2), luki t3-t5 i uprzaz t2/t4/t5 - u wiekszosci.
  Winterfell w linii tarcz t2 kuje 29 obcych tarcz (vlandia 11, empire 8, aserai 5, ...). Dorzecze, Reach, Korona itd. kuja bron biala tylko neutralna.

### 1.5 Co jest "neutralne" i co nosza zolnierze [X]

- **Bron bez kultury (lokalna wszedzie):** chlopska (kilof, sierp, widly - SandBoxCore), falchiony i miecze SandBoxCore, pirackie NavalDLC, piki/wlocznie RBM
  oraz bron ROT bez kultury: `stark_sword` (Northern Sword), `stark_dagger`, `lannister_sword`, `lion_sword` (Kingslayer's Sword), `lannister_kite_shield`,
  `dorne_sword(2)` (Essosi Sword), `martell_sword`, `martell_scimitar`, `yronwood_sword`, `kopesh_sword`, `serpent_kopesh_sword`, `velaryon_cutlass`,
  `unsullied_sword`, `katana` (Yi Ti Katana), `yiti_sword`, `baratheon_spear`, `ranseur`, `bronze_sword` (Thenn), `rapier` (Braavosi), `falcata`,
  `golden_company_sword`, `lyseni_sword`, `dragonstone_shield`, `berserkr_axe`, `barghasts_great_axe` (RBM_WS).
- **Zbroja neutralna:** stroje mieszczan RBM (fartuchy, szale - glownie t0-t1) i **stroje bandytow RBM_WS/ROT**: `tacky_bandit` "Gilded Raider Armor" t5,
  `tacky_bandit_shoulder` t5, `bandit_hybrid_armor_shoulder_b` t6, `tacky_bandit_boot` t4, helmy `tacky_bandit_helmet*` t2, `bandit_garb/cap/shoes`. To one
  wypelniaja linie heavy/ultra w kazdym miescie (heavy: jedyna neutralna sztuka to buty bandyty). PROJEKT-175 :84 juz zdecydowal: przedmioty kultur
  bandytow tylko dla jednostek z occupation Bandit.
- **Co nosza zolnierze (ROT-Troops.xml, sloty zbroi bojowych zestawow, unikalne na jednostke):** Polnoc 360 sztuk - wlasne 296, obce 51 (vale 17,
  vlandia 16); Dorzecze 283 - wlasne 222, obce 59 (battania 36); Reach 189 - obce 66 (battania 36, vlandia 18); Ziemie Burzy obce 62 (battania 36).
  Obce "battania" u poludniowcow to sztuki wspolne: `first_men_boots` (nosi 91 jednostek), `jon_snow_boots` "Northern Leather Boots" (79), `karstark_gloves`
  "Northern Gloves" (44); vale - `andal_boots` (12 kultur jednostek). 20 przedmiotow zbroi nosza jednostki >= 4 kultur. Przy surowej regule tylko
  Polnoc kuje buty, ktore nosi pol Westeros.

### 1.6 Propozycja 6.19 (do planu kodu)

1. **Jedna funkcja "kraina sztuki"** `WorkshopLaw.CraftCulture(ItemObject)` (nowa, raz na sesje, slownik): kultura z XML, z poprawkami TYLKO dla produkcji
   (nie zmieniamy `Item.Culture` - czyta je DTE, kaskada 175 w CS, UniqueLaw.StandInFor, HeroGear):
   - literowki: `riverlands` -> river, `volantene` -> volantine, `Empire` -> empire, `Vlandia` -> vlandia;
   - bron rodow bez kultury wedlug prefiksu id (tabela w kodzie, do akceptacji Jeffa): `lannister_` -> vlandia, `stark_` -> battania, `martell_`, `yronwood_`,
     `dorne_` -> aserai, `baratheon_` -> stormlands, `velaryon_`, `dragonstone_` -> dragonstone, `unsullied_`, `kopesh`, `serpent_kopesh` -> ghiscari,
     `katana`, `yiti_` -> yiti, `bronze_sword` -> freefolk (Thenn), `rapier` -> empire (Braavos), `lyseni_` -> lyseni, `golden_company_` -> volantine;
   - "wspolne" (nosza je jednostki >= N kultur jednej strony morza - z wzorcow, raz na sesje jak `CivilianOnly` WorkshopLaw.cs:603) - kuje kazde miasto
     tej strony morza (`MountLaw.IsEssosCulture`, MountLaw.cs:41);
   - bandyckie (`bandit_`, `tacky_bandit`, `looter`, kultury looters/sea_raiders) - zadne miasto (spojnie z PROJEKT-175 :84).
2. **Kraina miasta** = `PeopleBook.CultureAtCalibration(town.Settlement)` (PeopleBook.cs:834), nie `town.Culture`.
3. **Lancuch per koszyk (typ x tier), nie per linia:** wlasna kraina -> kraina pokrewna (tabela) -> wspolne/neutralne tej strony morza -> nic (koszyk
   nie powstaje w tym miescie; kupuja go karawany, AiGear i zalogi z innych miast). Usunac cala-pule-obca (WorkshopLaw.cs:530, TownFletchers.cs:407,
   GRA :1046 przez postfiks). Propozycja pokrewienstwa (lore, do akceptacji):
   Polnoc <-> Skagos <-> Nocna Straz; Wolni Ludzie -> (brak; Thenn brazem) -> neutralne; Dorzecze <-> Dolina <-> Korona <-> Ziemie Burzy <-> Reach <-> Westerlands
   (Andalowie); Smocza Skala <-> Korona, Valyria; Dorne -> Ziemie Burzy, Reach (Marchia Dornijska); Zelazne Wyspy -> (brak; "zelazna cena" = lup) -> neutralne;
   Wolne Miasta: Braavos <-> Lorath <-> Pentos; Myr <-> Lys <-> Tyrosh (Sporne Ziemie); Norvos <-> Qohor; Volantis <-> Lys, Valyria; Zatoka Niewolnicza <->
   Volantis, Qarth; Qarth <-> Yi Ti; Ibben -> Lorath, Braavos; Sarnor -> Qohor, Dothrakowie; Dothrakowie -> (nie kuja - lup) -> neutralne; Wyspy Letnie -> neutralne.
4. **Te sama regula w czterech miejscach:** `WorkshopLaw.Candidates` (:518-530), `TownFletchers.CandidatesOf` (:387-410; pamiec `_town` zerowac przy
   zmianie kultury nie trzeba - kraina stala), `ColdStart.Markets` (:154, :171), oraz **droga D**: rozszerzyc `WorkshopLaw.RandomItemPostfix` (postfiks
   `WorkshopsCampaignBehavior.GetRandomItemAux(ItemCategory, Town)`, :1479) - gdy wynik jest unikatem/legenda/Forbidden albo spoza lancucha krainy, wylosowac
   ponownie z tej samej kategorii wedlug lancucha (wagi jak gra: 1/(max(100, wartosc)+100), GRA :1058); bez miasta (fallback gry) - tylko filtr unikatow.
   Kolejnosc: BK nie patchuje `GetRandomItemAux` (woluje go refleksja w wylaczonym HandleItemAvailability); nasz postfiks jest jedyny.
5. **Kuznia gracza (droga E):** bez zmiany kultury (gracz kuje z wzoru, ktory zna) - chyba ze Jeff zechce "jedna regula"; unikaty: decyzja z 1.3.
6. **Wylacznik** `WorkshopCraftByRealm` (Settings.cs + tools/gen_mcm.py); MCM napis po angielsku. Bez nowej latki Harmony (zmiana naszych metod + postfiks juz wpiety).
7. **Linia logu (6.19):** "Warsztaty wedlug krainy: dzien D - sztuk: wlasne X, pokrewne Y, wspolne/neutralne Z, obce 0; koszyki bez wzoru w krainie N
   (najwieksze: ...); unikatow odcietych na drodze gry U". Bramka testu: obce = 0, unikaty = 0, korpus dziennie nie mniej niz dzis (d31-120 **215.5/d**
   [P], helm 392, nogi 173, rece 186, tarcze 142) i "cos na tulowiu >= 75% zolnierzy AI" (Skonczone etapu 6).
8. **Ryzyka:** (a) male krainy straca czesc koszykow (Ibben, Qarth, Valyria, Skagos, Sarnor - bez korpusu t1-t3) - pokrewne je lataja, reszte przywiezie
   handel (174b, MaterialOrders, AiGear koszyk typ x tier AiGear.cs:564); (b) usuniecie bandytow z neutralnych zmniejsza pule heavy (jedyna neutralna)
   i ultra - linia heavy zostanie z samymi wlasnymi/pokrewnymi; (c) kategorie sa z tieru PRZED prawami CS (ArmorTierLaw) - tier w DUMP jest po nich,
   wiec liczby pul sa przyblizone; w kodzie liczyc koszyk z `TierOf` (WorkshopLaw.cs:584) jak dzis; (d) `CraftCulture` nie moze ruszac `Item.Culture`
   (DTE `Cache.GetItemsByTypeTierAndCulture`, kaskada 175, HeroGear.cs:330-331).

---

## 2. 6.1 - wytop przy kopalni (zelazo zamiast rudy) i przetop

**Dzis [K]:**
- Wydobycie: 26 wsi `iron_mine` (MAPA: sturgia 4, stormlands 3, ghiscari 3, reach 3, river 2, vlandia 2, po 1: vale, norvos, freefolk, battania, khuzait,
  nightswatch, empire, volantine, qohorik). `MaterialLaw.ProdPostfix` (MaterialLaw.cs:163-200) - postfiks `CalculateDailyProductionAmount` kazdego modelu
  produkcji wsi (vanilla / NavalDLC / BK / BEE, licznik zagniezdzenia), ruda x `MineOutputMultiplier` 3 (Settings.cs:439) x `MineOutputStep` 1.5 (:1272)
  / `HistoricalPrices.BulkScale` (HistoricalPrices.cs:142-147) x `MineralOnce.Times` (dubel mineralu BK) x `Devastation.YieldFactor` x czynnik rak E4/7.
- Jednostka: `iron` (i `hardwood`) waza 100 kg - `HistoricalPrices` (:350-357, `HistBulkUnitFactor` 10, Settings.cs:612). Kopalnie BK w warsztatach miast
  (linia artisans `iron` bez wsadu) - `BulkLineSkip` (WorkshopLaw.cs:112-130); [P] "kopalnie miast i zamkow +0".
- Wytop w MIESCIE: `WorkshopLaw.Needs` (:160-176): ruda = metal kg / `WorkshopCrudeKgPerOre` 1.5 x 10 kg x 1.25^stopien gatunku; drewno = drewno sztuki
  + ruda kg x `WorkshopWoodPerOre` 5 + metal kg x `WorkshopForgeWoodPerMetalKg` 12.5 (Settings.cs:534-539). To samo: naprawy (MendMaterial - ruda
  przez dymarke), strzelarze (TownFletchers), linia narzedzi (WorkshopTrade.cs:233-256), przetop gracza (MaterialLaw refine: 1 ruda + 20 wegla -> 3 surowki,
  :105, latka `DefaultSmithingModel.GetRefiningFormulas` :218), paliwo w cenie sztuki (HistoricalPrices.cs:291-296).
- Ksiega: `OreLedger` (OreLedger.cs:64 "Ruda", NoteModel :102, NoteWorkshop :148), StartStock (StartStock.cs:17-106 - zapas startowy w ladunkach).
- [P d120]: wsie dopisaly 301 ladunkow (26 wsi), zuzycie: warsztaty zbrojne 242, narzedzia 32, strzelarze 12, budowy 0; zapas razem 11 202 (miasta 5996,
  karawany 2219); miast bez rudy 34 z 97 (linia 174b: 30, m.in. Braavos, Myr, Pentos, Lorath, Ibben, Hardhome, Mormont Keep). Drewno: warsztaty zbrojne
  **1472** ladunkow/d (dymarki), budowy 525, zapas 59 167. Warsztaty: "brak surowca" d31-120 680 cykli/d, z tego ruda ok. 640.
- **Przetop (zlom):** `ArmsScrap` (ArmsScrap.cs, Settings.cs:1277-1279 `OldStockToScrap` = true w kodzie, w Armoury.json Jeffa brak klucza -> domyslna;
  komentarz klasy mowi "domyslnie wylaczony" - niespojnosc do poprawienia przy 6.1): nadmiar ponad rok popytu, 1%/d, wraca 0.5 RUDY z receptury.
  [P d120] "zlom z nadmiaru +1". Przetop wrakow przy naprawie: MendMaterial (polowa metalu, Recipes.SmeltYield).

**Cudzy kod czytajacy `iron` [K]:** BK `ItemExtensions.IsMineral` ("iron"), `MineralData.GetItem` (:58), budowy BK (`BKConstructionModel` - ruda jako
material budowy); BEE po fragmencie id (`Contains("iron")` - TownEconomyCampaignBehavior.cs:126, :130, :2175, :3857; CastleEconomy :1713, :1778;
VillageDevelopment :748; ProductionProfiles/RegionalProfiles) - zmiana ZNACZENIA przy tym samym id ich nie psuje; waga `iron` czyta tez DTE/BKROT -
nie sprawdzone (K13 133 "Ryzyka").

**Co zmienic (K13 paczka 133, PLAN-K13-2026-10-07.md:213-234):** `iron` staje sie lupa zelaza (15 kg = 1.5 kg z 10 kg rudy x 10, wartosc 15 x
`HistCrudeIronPerKg` 1.4 = 21 d, nazwa "Iron Bloom"); liczba sztuk z kopalni bez zmian; wegiel dymarki = praca lasu okregu (5 ladunkow drewna na lupe,
osobna pozycja ksiegi drewna, nie towar). W tej samej paczce: `WorkshopLaw.Needs` bez czlonu `oreKg x WorkshopWoodPerOre` (kuznia pali tylko wegiel
kowalski), metal liczony w kg lupy; narzedzia (WorkshopTrade :233-256); BK rzemieslnicy `iron>2tools`; przetop gracza 1 lupa -> 30 surowek + wegiel;
StartStock w lupach; MendMaterial i TownFletchers (ruda -> lupa); `ArmsScrap` zwraca lupy (kg metalu / 15); OreLedger "Zelazo (lupy)"; `tools/sprawdz_logi.py`.
**PULAPKA** (K13): `BulkScale(iron)` sluzy do liczenia sztuk (MaterialLaw :175, WorkshopLaw :126, WorkshopTrade :256) - przy 15 kg kopalnie daly by
6.7x zelaza; liczyc stala "100 kg rudy na lupe". Tylko NOWA kampania (stara ruda w zapisach stalaby sie lupami). Wylacznik.

**Latki:** bez nowych celow Harmony - nasze: `CalculateDailyProductionAmount` (MaterialLaw, Priority domyslne, licznik zagniezdzenia), `GetRefiningFormulas`
(MaterialLaw), `TickOneProductionCycleForNotableWorkshop` (WorkshopLaw.CyclePrefix - przed BK `ProduceOutputPrefix`, linie uzbrojenia nie dochodza do BK).
Wplyw: drewno w miastach -1472 -> ok. 300-400 ladunkow/d [W] -> `VillageWoodlotLoads` w dol osobnym wpisem (K13 133).

---

## 3. 6.14 - rycerz z wlasnym koniem, jazda AI ok. 20%

**Dzis [P d120]:** konni w partiach rodow AI 9213 z 109 703 (8.4%), garnizony 6.2%, karawany 56.9% (linia "Zold konnych (160)"). Wedlug krolestw
("Musztra AI wedlug krolestw"): Polnoc 5%, Korona 7%, Reach 8%, Dolina 13%, Dorne 10%, Dorzecze 6%, Ziemie Burzy 5%, Nocna Straz 3%, Smocza Skala 6%,
Volantis 6%, Targaryen 30%, Zelazne Wyspy 1%, Sarnor 13%, Qarth 8%, Braavos 3%, Wolni Ludzie 1%, Dothrakowie 45%, Qohor 12%.
"Konie AI (175)": awanse na jezdzca wykonane 9/d, odrzucone z braku konia 1228 (skret 133, czeka 1095), czeka 1504 ludzi w 110 partiach; konni bez
konia w zbrojowni 2911 z 14 224; wolne konie w zbrojowniach 2090; zakupy lordow 28 koni/d za 11 116, nieudane brak koni 26 (pusto 22, za drogie 4);
naplyw konnych: ochotnicy 16, najemnicy 1, jency 6, zamiany ROT +54/-10, awanse 9. Konie na polkach miast i zamkow: 2171 (d1) -> 11 622 (d120).
**Wniosek:** podaz koni jest, brakuje dopasowania - zakup lorda raz na 4 doby i "garstka" (Stables.cs:698-760, `AiMountBuyCooldownDays` 4 u Jeffa),
kategoria konia (zwykly/bojowy/szlachetny) i miejsce postoju.

**Kod [K]:**
- `Stables.RanksNeedHorses` (Stables.cs:96) - postfiks gettera `CharacterObject.UpgradeRequiresItemFromCategory`, Priority.Last (DTE ustawia null, my
  przywracamy - `CavalryNeedsMounts`); `RequiredMountFor` - kategoria wedlug tieru celu.
- AI: `FilterTargets` (:591, postfiks `PartyUpgraderCampaignBehavior.GetPossibleUpgradeTargets`) i `PayInHorses` (:648, prefiks `UpgradeTroop`) -
  awans na konnego tylko z koniem z taboru (z `AiFreeArmoryHorsesFirst` - wolny ze zbrojowni), kon -> zbrojownia DTE (`Consume` :395).
- Zakupy: `OnSettlementEntered` (:698) - `Remount` (dosadzanie z taboru), cooldown, targ/wsie. `Balance` (:300) - stan koni zbrojowni.
- Liczniki: `HorseCensus` (HorseCensus.cs, linia "Konie AI (175)", plik konie-krolestwa.csv), `MountedWage` (premia zoldu konnego x1.5), `RotHorseGuard`
  (Dothrakowie przy zamianach ROT), `VolunteerKit` (ochotnik na konnego u notabla - kupiony kon / cofniety), `RecruitCost` (kon najemnika: wlasny bez
  doplaty), `GentryService` (rycerze BK - bohaterowie jada przy panu, nie oddzialy).
- DTE: kon w bitwie z graczem ze zbrojowni (`PartyEquipmentDistributor` - brak = `ArmoryShortageType.Horse`, DTE :383); w autobitwie AI-AI kon nie gra roli
  (liczy `IsMounted` jednostki). Po bitwie zbrojownie pokonanych ida do zwyciezcow (DTE EveryoneCampaignBehavior.cs:529-562, `DistributeLootRandomly` :751,
  `GetAllLootItems` :828).

**Co zmienic (projekt audytu 14, 5.2 - Jeff 11.10 "tak"):**
1. Awans linii szlacheckiej i rodowej (drzewo `CultureObject.EliteBasicTroop` i linie rodow ROT - zbior jak w CS Army175/HouseLevies) na konnego: zamiast
   konia z taboru rod KUPUJE konia wlasciwej kategorii we wsiach swoich lenn (magazyn wsi - stado; zaplata do wsi/notabli, jak targ wsi), gdy pusto - na
   targu najblizszego miasta (polka, `ShopReserve`), placac kiesa rodu w pulapie sprzetu 166 (ClanBudget); kon do zbrojowni jezdzca (`Stables.Consume`).
   Linia chlopska bez zmian (kon z taboru). Miejsce: `FilterTargets`/`PayInHorses` (nowa galaz przed "czeka"), liczniki w HorseCensus.
2. Cel ok. 20% bez "konia z niczego": po (1) sprawdzic, czy waskim gardlem nie zostaje sklad ROT (szablony, `ROTTroopRecruiter.ExchangeClanTroops`,
   CS Army175 sklad); Dothrakowie (45%) i Zelazne Wyspy (1%) juz zgodne z lore - nie ruszac; ewentualnie suwak udzialu awansow na konnego per kraina.
3. Koszt: zold konnych x1.5 (MountedWage) - przy 20% jazdy zold partii ok. +10-15% wobec dzis [W] (dzis premia +8.4% przy 8.4% konnych; audyt 14: 11-15%); kon bojowy t4 1100-2400 zl, szlachetny 6-10 tys. (audyt 14).
4. Wylacznik `KnightBuysOwnHorse`; linia "Rycerze: konie kupione we wsiach/na targu, zaplacone, czekaja".
**Kolejnosc latek:** getter: DTE (null) -> nasz postfiks Priority.Last; `GetPossibleUpgradeTargets` / `UpgradeTroop` - tylko nasze (+ HorseCensus liczy
w tych samych). Zalezy od 1.4 (test zbrojowni AI) i 6.11-6.13 (bilans koni), bo wiecej awansow = wiecej koni zdejmowanych z rynku.

---

## 4. Pozostale pozycje 6.2-6.18

**6.2 Wiecej rak i kopaln** - [K] rece: `WorkshopLaw.TownHands`/`LegacyTownHands`, `WorkshopHandsPer1000People`, FreeHands (czynnik rak E4/7 w ProdPostfix);
ruda: `MineOutputMultiplier` 3 x `MineOutputStep` 1.5. Kopalnie: 26 wsi (MAPA), BK MineralData kazdej osady (BK MineralData.cs:69-120: wies gornicza
ma glowny mineral; inne losowo - ruda 2-4 z ~50 wag) i budynek kopalni BK w zamkach/miastach ([P] "kopalnie 0"). Zmiana po 6.1 (lupa) - P174 S4
(PROJEKT-174:629): rece 0.10 -> 0.15, kopalnie x2; nowe kopalnie = zmiana typu wsi jak VillageClimate (VillageClimate.cs, CS NorthernFare) - NK.

**6.3 Wsie x1.3 lnu, welny, skor; wsad wedlug wartosci** - [K] nie ma: `VillageOutputStep`/`ConsumerShareForStep` (K13 132, PLAN-K13:196-212) nie
istnieja w Settings.cs; wsad wedlug wartosci (K13 131) nie znaleziony w WorkshopTrade (linie towarowe gry 1:1). Miejsce: `MaterialLaw.ProdPostfix`
(czynnik obok rak), `HistoricalPrices.DemandPostfix/BudgetPostfix`; TownCrafts juz przerabia welne/len/skory wedle wartosci (TownCrafts.cs, paczka 148).

**6.4 Mniej towaru bez zbytu zamiast kasowania przez BK** - [K] BK `BKSettlementBehavior.DeleteOverProduction(Town)` (:296-311): `DeleteOverProduction`
= true w configu BK Jeffa - co dobe -2% stosu towaru > 500 (nie zywnosc, nie zwierzeta, nie uzbrojenie) **w nicosc**; nasze nie latamy go (grep:
tylko `ItemConsumptionBehavior.DeleteOverproducedItems` gry - ArmsLeaks.cs:89, :291, GoodsLedger "kasowanie gry" :1102). Zmiana: zawor K13 134
(PLAN-K13:235-248: raz na pore roku mnoznik towaru wsi przy zuzyciu < 50% produkcji i indeksie < 0.5) w `MaterialLaw.ProdPostfix` + prefiks BK
`DeleteOverProduction` (false gdy zawor czynny) albo prosba do Jeffa o wylaczenie w MCM BK (jedna zasada: zawor). Ksiega: GoodsLedger juz liczy.

**6.5 Warunkowo: chleb, piwo, wino, deski z wsadu; srebro; rzemioslo wsi BK** - [K] TownCrafts bierze tylko pary welna/len/skory (TownCrafts.cs:140-167,
nazwy :192-207) - K13 136 (wszystkie pary gry); `ArtisanInputs` (paczka 147 - rzemieslnicy BK z wlasnego wsadu) jest; srebro przy kopalni (K13 137)
i rzemioslo wsi BK tylko z wlasnego surowca (K13 138) - brak. Warunki z ksiegi towarow (GoodsLedger).

**6.6 Ksiega uzbrojenia wedlug zrodla** - [K] sa kawalki: "Warsztaty: dzien" (WorkshopLaw Flush), "Strzelarze (172)", "Uzbrojenie (ujscia 174)"
(ArmsLeaks), Measure174b, "Zlom z nadmiaru", "Rynek broni" (spis polek). Brak jednej linii zrodlo -> ujscie (warsztaty, strzelarze, kuznia gracza, start,
lup, Spoils, DTE ze zbrojowni / do zbrojowni, zlom, kasowanie). Wzor: GoodsLedger (ramki, ksiega towarow) - nowa ksiega dla `ArmsLeaks.ArmsPiece`.

**6.7 Dziury po 174** - [K/P] ruda i towar rozbitych partii: GoodsLedger.OnPartyDestroyed (GoodsLedger.cs:790-810) - "przepadlo z rozbitymi partiami"
(ruda, doba 109: 32 ladunki, z tego wozy 31 [P]) - w nicosc, powinno byc lupem zwyciezcy; uprzaz bez kupca (polki HorseHarness 2450 [P d120]); towar wozony bez wozu: zakupy
zalog (GarrisonArmory/GarrisonCarts.cs), handel dzienny PodazPopyt ("kupcy wywiezli 2902 szt. ... zaplacone" - teleport miedzy osadami [P]),
narzedzia (WorkshopTrade linia smithy).

**6.8 Warunkowo po tescie uzbrojenia** - tarczownicy, placa miasta w cenie, strzaly, helmy, "dobytek osady": kod w WorkshopLaw (cechy - `GuildOf` :664,
`LineShare` :914), TownFletchers; [P d31-120] tarcze 142/d, helmy 392/d - decyzja po tescie etapu 6.

**6.9 Warsztaty wedlug surowca i lore; smoki, mamuty, wielblady; "strawa Polnocy"** - [K] gra wybiera typ warsztatu przy starcie wedlug gestosci wsadu
z okolicznych wsi (GRA `FindTotalInputDensityScore` :1116, `BuildWorkshopForHeroAtGameStart` :1255; zmiana przy bankructwie :1093). CS `Mends`
"strawa Polnocy" (CrashScribe/src/Mends.cs:2117-2157) zmienia winnice wsi Polnocy, ale warsztatow miast nie (tlocznia bez winogron; audyt 02 C1:
przestawiac warsztaty, ktorym zniknal wsad; `WorkshopTrade.cs:172` juz pilnuje kapitalu przy `ChangeWorkshopProduction`). Zwierzeta: `MountLaw`
(MountLaw.cs - mamut tylko giganci, wielblad Dorne/Qarth, rydwan Essos, slon Volantis), `DragonUnmount`; wsie pustynne rodza wielblady (DIAGNOZA-NADMIARU-KONI 2).

**6.10 Klimat wsi takze dla warsztatow miast** - [K] `VillageClimate` (VillageClimate.cs, T8/D.1-D.4) i `WesterosClimate` zmieniaja tylko typy wsi; typ
warsztatu miasta nie zna klimatu. Miejsce: postfiks `FindTotalInputDensityScore` (wynik 0 dla typu, ktorego wsad nie rosnie w klimacie krainy) + jednorazowe
przestawienie warsztatow przy wczytaniu (jak CS). NK.

**6.11 Konie: linia w logu; ginie tylko zabity, ocalaly to lup zwyciezcy** - [K] brak bilansu koni (GoodsLedger nie liczy koni - lista towarow d120 bez Horse);
sa: HorseCensus (175), "Stajnia AI", "Konie rekrutow (160)". Zrodla: wsie-stadniny (produkcja gry x (hearth+1) x 0.5), juczne z niczego przy nowej
karawanie (CaravanPartyComponent.InitializationArgs) i BK GivePackAnimals (TravellerGoods36.cs:22-105 - tylko licznik), BK RunStuds. Ujscia: smierc
jezdzca (kon w zbrojowni DTE zostaje - wolne konie 2090 [P]), DTE `ScrapCapPerCategory` 600 (AUDYT-PELNY-LOGISTYKA N7), partia zniszczona (GoodsLedger
:790), BK `BKCaravansBehavior` wyrzuca zwierzeta po bitwie (:645-735), konsumpcja miast (popyt kategorii horse), gra `DeleteOverproducedItems` (ArmsLeaks
174.0). Zmiana: linia "Konie" (zrodlo/ujscie/stan jak "Ruda"), a ujscia bez przyczyny -> lup zwyciezcy.

**6.12 Hodowla wedlug popytu + przeglad 71 stadnin** - [X] 71 wsi-stadnin (MAPA): vale 7, vlandia 7, battania 7, ghiscari 5, river/stormlands/aserai/khuzait 4,
reach/crownlands/volantine/dragonstone/qartheen 3, ... ; [P] polki koni 2171 -> 11 622 w 120 dob (+80/d). Miejsce: `MaterialLaw.ProdPostfix` (czynnik
dla `Horse` wedlug indeksu ceny/popytu jak zawor 134), przeglad typow wsi jak VillageClimate (NK); BK `RunStuds` (BKBuildingsBehavior: kon bojowy do skrytki
zamku z niczego, 2%/d, limit pastwisk) - prefiks albo zaplata z produkcji wsi.

**6.13 Konie po poleglych ze zbrojowni AI na targ** - [K] `Stables.Balance` (:300) i `Remount` - "nadmiar ze zbrojowni do taboru" 12-25/d [P]; wolne konie
w zbrojowniach 2090 [P]; dalej sprzedaz z taboru - gra (PartiesBuyHorse/sprzedaz lupu) albo nasza; zalezy od 1.4. Zmiana: wolne konie ponad zapas
(np. 10% konnych) -> tabor -> sprzedaz w najblizszym miescie (cena polki), licznik w "Konie".

**6.15 Konie z lupu i zbrojowni nie niosa piechura** - [K] `MarchPace.CountColumn` (MarchPace.cs:33-52) czyta `ItemRoster.NumberOfMounts` /
`NumberOfPackAnimals`, ktore gra liczy tylko bez modyfikatora (AUDYT-PELNY-LOGISTYKA N6) - kon z lupu ze stanem (Lame itd.) nie niesie piechura; konie
w zbrojowni DTE w ogole sie nie licza. To samo w modelu predkosci gry. Zmiana: liczyc konie z modyfikatorem (poza kulawymi?) i wolne konie zbrojowni
(`Stables.Balance`) w MarchPace i w postfiksie `CalculateFinalSpeed` (juz nasz `SpeedPostfix` MarchPace.cs:59).

**6.16 Juczne konie z targu; zapasowe konie lordow; udzwig** - [K] udzwig gry 10 + 20 kg x ludzie + 100 kg x juczne (`DefaultInventoryCapacityModel`);
juczne z niczego przy karawanach (gra) i taborach BK (TravellerGoods36 - licznik); MarketCarts.cs:134 (kategoria PackAnimal), CastlePurse.cs:448.
Zmiana: nowa karawana/tabor kupuje juczne z targu (zamiast z niczego); lord kupuje zapasowe konie (Stables) - jedna regula z 6.14.

**6.17 Naprawy: material wedlug rodzaju, Twoje rece ta sama regula, AI naprawia tylko to, co nosi** - [K] u kowali miasta zrobione: `MendMaterial`
(MendMaterial.cs - metal, drewno, skora, plotno z receptury `ArmsPricing.CostOf`, `MendMaterialMaxShare` 0.20 x (1 - stan), Settings.cs:1123-1127),
AI przez `AiWear.MendWithMaterial` (AiWear.cs:427-470 - sztuki z ksiegi zuzycia partii `AddWorn`, czyli noszone [W]). Roznica: wlasne rece gracza
`Forge.SelfRepair` (Forge.cs:498-520) - `SelfRepairMaterialFactor` 0.25 x brak stanu (Settings.cs:90) i `Recipes.TakePartial` - inna stala niz 0.20 kowali;
do ujednolicenia (jedna liczba, ten sam podzial na rodzaje materialu).

**6.18 Wioski poboczne wedlug rodzaju** - [K/X] plik `Modules\Armoury\ModuleData\arm_map_villages.tsv` (2446 wiosek): kolumna `model` = village 1053,
farm 446, fishing 400, mill 358, windmill 98, granary 91 (kolumna `kind` to miejsce: droga 1249, rzeka 474, brzeg 441 ...). Czyta ja `MapVillageFile`
(MapVillagesView.cs:93-253, `rec.Model`), ksiega okregow `VillageBook` (VillageBook.cs - osady wioski, `Kind`, plon okregu = suma pracy wiosek, :827,
:1270-1275) - ale produkcja okregu nie zna rodzaju. Zmiana (audyt 02 A4): wymiana, nie dodatek - czesc "zboza" typu wsi gry na ryby wedlug udzialu ludzi
wiosek rybackich (mlyn/wiatrak/spichlerz/farma - zboze); miejsce: `MineralOnce` postfiks BK `GetProductions` (dopisanie `fish` do listy wsi) +
`MaterialLaw.ProdPostfix` (podzial). Razem z 6.3 i 6.9 (Jeff 11.10).

---

## 5. Styki latek (cele Harmony, ktore etap 6 rusza)

| Cel | Dzis (kto) | Etap 6 |
|---|---|---|
| `WorkshopsCampaignBehavior.TickOneProductionCycleForNotableWorkshop` | Armoury `WorkshopLaw.CyclePrefix` (zwraca false dla linii uzbrojenia - BK `ProduceOutputPrefix` i `ConsumeInputFromTownMarket` ich nie widza) | 6.19 (Candidates), 6.1 (Needs) - bez nowej latki |
| `WorkshopsCampaignBehavior.GetRandomItemAux(ItemCategory, Town)` | Armoury `RandomItemPostfix` (wegiel/sztabki -> surowiec); BK tylko refleksja (wylaczone) | 6.19 - unikaty i kraina na drodze gry |
| `TickOneProductionCycleForPlayerWorkshop`, `CanPlayerWorkshopProduceThisCycle` | Armoury PlayerCyclePrefix / PlayerGatePostfix | 6.19 przez GetRandomItemAux |
| `CalculateDailyProductionAmount` (modele produkcji wsi) | Armoury `MaterialLaw.ProdPostfix` (+ BK, BEE, NavalDLC modele) | 6.1, 6.3, 6.4, 6.12, 6.18 - jeden wspolny mnoznik |
| BK `PopulationManager.GetProductions` | Armoury `MineralOnce` postfiks (dubel mineralu, papirus) | 6.18 (ryby), 6.5 |
| `DefaultSmithingModel.GetRefiningFormulas` | Armoury MaterialLaw | 6.1 (lupa) |
| getter `CharacterObject.UpgradeRequiresItemFromCategory` | DTE (null), Armoury `RanksNeedHorses` Priority.Last | 6.14 |
| `PartyUpgraderCampaignBehavior.GetPossibleUpgradeTargets` / `UpgradeTroop` | Armoury Stables FilterTargets / PayInHorses (+HorseCensus) | 6.14 |
| BK `BKSettlementBehavior.DeleteOverProduction` | nikt (BK kasuje) | 6.4 |
| BK `BKBuildingsBehavior.RunStuds` | nikt | 6.12 |
| `GoodsLedger.OnPartyDestroyed` (zdarzenie MobilePartyDestroyed) | Armoury (licznik), DTE EveryoneCampaignBehavior :510 | 6.7, 6.11 |
| `CalculateFinalSpeed` | Armoury MarchPace.SpeedPostfix Priority.Last | 6.15 |

Pulapka MCM (CLAUDE.md 7): nowe klucze nie istnieja w Armoury.json Jeffa - obowiazuja domyslne z kodu. Dzis w Armoury.json brak: OldStockToScrap,
WorkshopForbiddenIds, WorkshopChooseByShortage, MineOutputMultiplier, MineOutputStep, HistBulkUnitFactor, PlayerWorkshopsSameRule (domyslne z kodu);
sa: CavalryNeedsMounts true, AiBuysMounts true, AiMountBuyCooldownDays 4.0.

## 6. Decyzje dla Jeffa (z tego rozpoznania)

1. Tabela pokrewienstwa krain (1.6 pkt 3) i przypisanie broni rodow bez kultury (1.6 pkt 1).
2. Stroje bandytow (Gilded Raider, Raider ...) - zadne miasto ich nie kuje (spojnie z 175)? To jedyna neutralna sztuka w linii heavy.
3. `lion_sword` "Kingslayer's Sword" - unikat (przydomek postaci)?
4. "Nikt nie kuje unikatow" - takze Twoja kuznia (legendy raz / opanowany wzor bez limitu - decyzja 30.08)?
5. Przedmioty "wspolne" (Northern Leather Boots, First Men Boots, Andal Boots - nosi je pol Westeros): kuje je kazde miasto tej strony morza?
6. 6.4: zawor zamiast kasowania BK - latka na BK czy wylaczenie `DeleteOverProduction` w MCM BK.
