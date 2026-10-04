# Audyt: produkcja i obieg uzbrojenia w VANILLA Bannerlord 1.4.8

Zakres: tylko kod gry (TaleWorlds.CampaignSystem, TaleWorlds.Core, SandBox) + XML
z `Modules\SandBox` / `SandBoxCore`. Inne mody (BannerKings, BetterEconomy, DTE, ROT)
moga to wszystko nadpisywac — opisane osobno.

"Uzbrojenie" = bron biala, strzelecka, tarcze, amunicja, pancerze, rzad konski, konie.
Nie: towary handlowe (Goods).

Skroty sciezek (zdekompilowane zrodla ILSpy, scratchpad):
- `tw/` = TaleWorlds.CampaignSystem
- `core2/` = TaleWorlds.Core (DefaultItemCategories, ItemCategory, DefaultItemCategorySelector)
- XML warsztatow: `Modules\SandBox\ModuleData\spworkshops.xml`

Numery linii odnosza sie do zdekompilowanego kodu, nie do oryginalu TaleWorlds.

---

## 0. Najwazniejsze wnioski (TL;DR)

1. **Glowne zrodlo uzbrojenia na rynkach to ukryty warsztat "artisans"** (Rzemieslnicy),
   ktory KAZDE miasto ma w slocie 0. Produkuje ~13 sztuk uzbrojenia na dzien na miasto
   (teoretyczne maksimum), z czego ~7,8 szt./dzien **bez zadnych surowcow**
   (bron T1, luki T1, tarcze T1, strzaly, odziez, lekkie pancerze).
2. Drugie zrodlo to warsztaty notabli **Kuznia (smithy)** i **Warsztat drzewny (wood_WorkshopType)**
   — moga trafic do slotow 1-3, jesli miasto ma blisko kopalnie zelaza / lesnikow.
3. **Produkcja uzbrojenia NIE kosztuje nikogo ani grosza.** Linie produkcji z wyjsciem/wejsciem
   nie bedacym towarem handlowym maja `effectCapital = false` — warsztat nie placi za surowce,
   miasto nie placi warsztatowi za wyrob. Surowce (zelazo, drewno, skora) znikaja z rynku za darmo.
4. **AI lordowie NIE dostaja przedmiotow z poleglych.** Lup z trupow jest dla AI zamieniany na
   zloto (`PartyTradeGold += 7,25 * poziom^2 / 55`). Prawdziwe przedmioty z trupow dostaje tylko gracz.
   Wiec "lordowie sprzedaja lup w miastach" dotyczy w praktyce towarow z rozbitych karawan/wiesniakow
   i koni, nie zbroi.
5. **AI nie kupuje uzbrojenia na rynku.** Bohaterowie AI maja ekwipunek z szablonow, wojsko
   ulepsza sie bez przedmiotow. Jedyne zakupy AI z rynku w kategorii "uzbrojenie" to **konie**
   (lordowie: tylko kategoria `horse`; karawany: kazda kategoria-zwierze).
6. **Glowny ujscie (sink) to "konsumpcja miasta"** (`ItemConsumptionBehavior`): kazde miasto
   codziennie "zjada" towar za budzet ~35-210 denarow na kategorie (przy dobrobycie 5000).
   Dla porownania: konie ~640-700 den./dzien na kategorie.
7. **Cena uzbrojenia prawie nie reaguje na podaz** — mnoznik ceny dla kategorii nie-handlowych
   jest przyciety do 0,8-1,3 (towary: 0,1-10). Nadwyzka nie hamuje produkcji.
8. Karawany **nie kupuja** broni/zbroi/strzal (tylko kategorie handlowe i zwierzeta). Moga
   sprzedac uzbrojenie, jesli je maja (np. z wygranej bitwy z cudzym ekwipunkiem w inwentarzu).
9. Rafinacja (ruda -> surowka -> ... -> stal damascenska, wegiel drzewny z drewna) istnieje
   **tylko dla kuzni gracza**. Zaden mechanizm AI ani warsztat nie produkuje wlewkow ani wegla.

---

## 1. Kategorie przedmiotow uzbrojenia

### 1.1 Przypisanie przedmiotu do kategorii
`core2/DefaultItemCategorySelector.cs:5-150` — kategoria wynika z typu i **tieru**
(pamietaj: `ItemTiers.Tier1 == 0`, w grze wyswietlane +1):

| Typ | Tier1 | Tier2 | Tier3 | Tier4 | Tier5 i Tier6 |
|---|---|---|---|---|---|
| Bron biala (`IsMeleeWeapon`) | melee_weapons | melee_weapons_2 | _3 | _4 | _5 |
| Bron strzelecka (luk, kusza, oszczep rzucany) | ranged_weapons | _2 | _3 | _4 | _5 |
| Tarcza | shield | shield_2 | _3 | _4 | _5 |
| Amunicja (`IsAmmo`) | arrows (wszystkie tiery) | | | | |
| Pancerz (glowa/tulow/rece/nogi/ramiona) | garment | light_armor | medium_armor | heavy_armor | ultra_armor |
| Rzad konski (HorseHarness) | horse_equipment | _2 | _3 | _4 | _5 |
| Siodlo (SaddleComponent) | horse_equipment | | | | |
| Kon bez kategorii w XML | horse | | | | |

Konie w vanilli maja kategorie z XML (`horses_and_others.xml`): zwykle `horse`,
t2_* i `charger` -> `war_horse`, t3_* -> `noble_horse`, juczne -> `sumpter_horse`.

Tier6 wpada do tej samej kategorii co Tier5 (`_5` / `ultra_armor`).

### 1.2 Parametry popytu
`core2/DefaultItemCategories.cs:380-409`, `core2/ItemCategory.cs` (`InitializeObject`:
liczby * 0,001).

| Kategoria | IsTradeGood | IsAnimal | BaseDemand | LuxuryDemand |
|---|---|---|---|---|
| melee_weapons / ranged_weapons / shield (T1) | nie | nie | 9 | 7 |
| *_2 | nie | nie | 7 | 7 |
| *_3 | nie | nie | 5 | 10 |
| *_4 | nie | nie | 4 | 10 |
| *_5 | nie | nie | 4 | 10 |
| arrows | nie | nie | 30 | 30 |
| garment | nie | nie | 9 | 15 |
| light_armor | nie | nie | 7 | 16 |
| medium_armor | nie | nie | 5 | 17 |
| heavy_armor | nie | nie | 4 | 17 |
| ultra_armor | nie | nie | 3 | 10 |
| horse_equipment 1..5 | nie | nie | 9/7/5/5/5 | 5/6/7/8/9 |
| sumpter_horse | TAK | TAK | 20 | 3 |
| horse | TAK | TAK | 140 | 0 |
| war_horse | TAK | TAK | 120 | 20 |
| noble_horse | nie | TAK | 120 | 50 |
| (dla porownania) iron | TAK | nie | 10 | 20 |
| hardwood | TAK | nie | 10 | 10 |
| leather | TAK | nie | 15 | 10 |
| tools | TAK | nie | 30 | 30 |

Uwaga: wlewki (crude iron ... thamaskene steel) maja kategorie **iron**, a wegiel drzewny
kategorie **hardwood** (`tw/TaleWorlds.CampaignSystem/DefaultItems.cs:125-133`).

---

## 2. Zrodla uzbrojenia na rynku miasta (Settlement.ItemRoster)

Zamki nie maja rynku: `DailyTickTown` i wszystkie mechanizmy ponizej dzialaja tylko dla
`Town.AllTowns` (`tw/TaleWorlds.CampaignSystem/CampaignPeriodicEventManager.cs:238`).

### 2.1 Warsztat "artisans" (ukryty, w kazdym miescie) — ZRODLO GLOWNE

- Tworzenie: `WorkshopsCampaignBehavior.BuildArtisanWorkshop` (`tw/.../WorkshopsCampaignBehavior.cs:1361-1373`)
  — slot 0 kazdego miasta, wlasciciel = pierwszy notabl-rzemieslnik. 4 sloty na miasto
  (`DefaultWorkshopModel.DefaultWorkshopCountInSettlement = 4`, `tw/.../DefaultWorkshopModel.cs:27`).
- Wyzwalacz: codziennie, `DailyTickTown` -> `RunTownWorkshop` (`WorkshopsCampaignBehavior.cs:279-290`,
  `1387-1420`). Nie dziala, gdy miasto jest w stanie buntu.
- Tempo: postep produkcji += `ConversionSpeed` (zmodyfikowany przez `GetEffectiveConversionSpeedOfProduction`,
  `DefaultWorkshopModel.cs:31-53`: Magazyn (Warehouse) +5/10/15%, perki Mercenary Connections +25%
  (gubernator), Sweatshops +20% (wlasciciel), polityki Forgiveness of Debts -5%, State Monopolies -10%).
  Kazde pelne 1,0 = jeden cykl produkcji.
- Warunek cyklu (`CanNotableWorkshopProduceThisCycle`, `WorkshopsCampaignBehavior.cs:776-792`):
  dla ukrytego warsztatu: wartosc wyrobu (cena sprzedazy w miescie) > koszt surowcow; kapital warsztatu
  (10 000, nigdy sie nie zmienia dla linii uzbrojenia) >= koszt surowcow; surowce musza lezec na rynku.
- Wybor przedmiotu: `GetRandomItemAux` (`:1049-1072`) — losowo z kategorii, waga `1/(max(100, wartosc)+100)`
  (tanie przedmioty czesciej), tylko kultura miasta lub `neutral_culture` (`IsItemPreferredForTown`, `:875-882`);
  jesli nic nie pasuje kulturowo — dowolny. Losowy modyfikator (`GetRandomItemModifierProductionScoreBased`).
  Pula: przedmioty nie-multiplayer, `is_merchandise != false`, nie zrobione przez gracza (`IsProducable`, `:991-998`).
- Przeplyw pieniedzy: **brak**. `flag3`/`effectCapital` (`:1405`) jest `false`, gdy jakiekolwiek wejscie
  lub wyjscie nie jest towarem handlowym — `ConsumeInputFromTownMarket` (`:857-873`) i `ProduceAnOutputToTown`
  (`:844-855`) nie ruszaja wtedy zlota warsztatu ani miasta.

Linie produkcji "artisans" dotyczace uzbrojenia (`spworkshops.xml`, id="artisans"):

| Wyjscie | szt./cykl | speed (cykle/dzien) | Wejscia |
|---|---|---|---|
| garment | 2 | 0,8 | — |
| light_armor | 1 | 0,8 | — |
| medium_armor | 1 | 0,3 | leather |
| heavy_armor | 1 | 0,15 | iron |
| ultra_armor | 1 | 0,075 | iron |
| melee_weapons | 1 | 1,5 | — |
| melee_weapons_2 | 1 | 0,6 | iron |
| melee_weapons_3 | 1 | 0,3 | iron |
| melee_weapons_4 | 1 | 0,15 | iron + hardwood |
| melee_weapons_5 | 1 | 0,075 | iron + hardwood + leather |
| arrows | 1 | 1,5 | — |
| ranged_weapons | 1 | 1,2 | — |
| ranged_weapons_2 | 1 | 0,6 | hardwood |
| ranged_weapons_3 | 1 | 0,3 | hardwood |
| ranged_weapons_4 | 1 | 0,1 | hardwood + leather |
| ranged_weapons_5 | 1 | 0,05 | hardwood + leather + iron |
| shield | 1 | 1,2 | — |
| shield_2 | 1 | 0,6 | hardwood |
| shield_3 | 1 | 0,3 | hardwood |
| shield_4 | 1 | 0,1 | hardwood + iron |
| shield_5 | 1 | 0,05 | hardwood + leather + iron |
| horse_equipment | 1 | 1,0 | hardwood |
| horse_equipment_2 | 1 | 0,4 | hardwood |
| horse_equipment_3 | 1 | 0,1 | hardwood + leather |
| horse_equipment_4 | 1 | 0,05 | hardwood + iron + leather |
| horse_equipment_5 | 1 | 0,025 | hardwood + iron + leather |

Ten sam warsztat robi tez: krowa -> 4 mieso + 2 skory surowe (speed 2), owca/swinia -> mieso,
winogrona -> wino, oliwki -> oliwa, **zelazo -> 2 narzedzia (0,05)**.

Suma na miasto (bez modyfikatorow, gdy sa surowce):

| Grupa | szt./dzien | z tego bez surowcow |
|---|---|---|
| Pancerze | 2,93 | 2,4 (garment + light) |
| Bron biala | 2,63 | 1,5 |
| Bron strzelecka | 2,25 | 1,2 |
| Tarcze | 2,25 | 1,2 |
| Strzaly | 1,5 | 1,5 |
| Rzad konski | 1,58 | 0 |
| **Razem** | **~13,1** | **~7,8** |

Zuzycie surowcow przez "artisans" na uzbrojenie (maks.): **iron ~1,63/dzien** (+0,05 na narzedzia),
**hardwood ~3,9/dzien**, **leather ~0,75/dzien**.

Wazne: produkcja "bezsurowcowa" nie ma zadnego hamulca — nie sprawdza zapasu na rynku ani zlota miasta.
Cena sprzedazy wyrobu ma podloge 0,8 * wartosci (patrz 4.1), wiec warunek "przychod > 0" zawsze spelniony.

### 2.2 Warsztaty notabli: Kuznia i Warsztat drzewny

Sloty 1-3 kazdego miasta. Typ wybierany przy starcie i przy bankructwie/zmianie wlasciciela przez
`DecideBestWorkshopType` (`WorkshopsCampaignBehavior.cs:1278-1338`), ktore punktuje typy wg produkcji
wiosek przypisanych do miasta (`TradeBound`) — liczone sa tylko linie z wyjsciem handlowym
(`FindTotalInputDensityScore`, `:1116-1170`), wiec Kuznie "ciagna" kopalnie zelaza (przez narzedzia),
a Warsztat drzewny — lesnicy (przez deski). Waga typu = `frequency` z XML (oba: 2), kara za powtarzanie
typu w miescie `1/(1+6n)^3`.

Warunek cyklu (nie-ukryty warsztat): **przychod ze sprzedazy wyrobu > koszt surowcow + 200/speed**
(`:778`). Dla linii o niskim speed oznacza to wysoki prog (np. speed 0,1 -> +2000 den.).
Tanie wyroby T1 czesto NIE przechodza progu (np. 3 luki T1 z jednego drewna musza byc warte
> ~630 den. przy sprzedazy).

Kuznia (`smithy`, koszt 5000):

| Wyjscie | szt./cykl | speed | Wejscie | prog przychodu (bez surowca) |
|---|---|---|---|---|
| melee_weapons | 2 | 1 | iron | 200 |
| melee_weapons_2 | 1 | 1 | iron | 200 |
| melee_weapons_3 | 1 | 0,5 | iron | 400 |
| melee_weapons_4 | 1 | 0,2 | iron | 1000 |
| melee_weapons_5 | 1 | 0,1 | iron | 2000 |
| medium_armor | 1 | 1 | iron | 200 |
| heavy_armor | 1 | 0,25 | iron | 800 |
| ultra_armor | 1 | 0,1 | iron | 2000 |
| tools (towar) | 4 | 1,5 | iron | 133 |

Maks. ~5,65 iron/dzien. Tylko linia narzedzi zarabia (jedyne `effectCapital = true`).

Warsztat drzewny (`wood_WorkshopType`, koszt 1000):

| Wyjscie | szt./cykl | speed | Wejscie |
|---|---|---|---|
| planks (towar) | 2 | 2 | hardwood |
| ranged_weapons | 3 | 0,33 | hardwood |
| ranged_weapons_2 .. _5 | 1 | 0,5 / 0,4 / 0,3 / 0,2 | hardwood |
| shield | 1 | 1 | hardwood |
| shield_2 .. _5 | 1 | 0,33 / 0,2 / 0,1 / 0,1 | hardwood |

Maks. ~5,5 hardwood/dzien.

Pozostale warsztaty (browar, tkalnie, garbarnia, prasy, garncarnia, zlotnik) uzbrojenia nie robia.
Garbarnia (hides -> 2 leather, speed 2) i tkalnia welny (wool -> 2 garment!, speed 1) sa
posrednio w lancuchu (`garment` to kategoria pancerza T1 — tkalnia welny tez produkuje "uzbrojenie").

Warsztat gracza (`TickOneProductionCycleForPlayerWorkshop`, `:584-677`) dziala tak samo,
plus opcja odkladania do magazynu (tylko wyroby handlowe moga isc do magazynu, `:630`).

### 2.3 Start gry
- Warsztaty odpalane 4 razy (`i % 20 == 0` w `OnNewGameCreatedPartialFollowUp`, `:131-147`;
  maks. indeks 100) bez warunkow oplacalnosci (`GameStarted == false`) — ok. 4 dni produkcji w zapasie.
- Wioski wrzucaja startowy towar do miast (`VillageGoodProductionCampaignBehavior.DistributeInitialItemsToTowns`,
  `tw/.../VillageGoodProductionCampaignBehavior.cs:52-122`) — w tym konie z hodowli.
- Lordowie dostaja startowe zloto: 10 000 (czlonek), 50 000 + 10 000*tier klanu (+50 000 wladca)
  (`TradeCampaignBehavior.InitializeTrade`, `tw/.../TradeCampaignBehavior.cs:98-111`).

### 2.4 Lordowie AI sprzedajacy lup — PartiesSellLootCampaignBehavior
`tw/.../PartiesSellLootCampaignBehavior.cs:19-42`
- Wyzwalacz: wejscie partii lorda AI do MIASTA (nie zamku), nie w wojnie z miastem.
- Co: wszystko poza jedzeniem; konie tylko gdy nie-wierzchowe, z modyfikatorem albo juczne.
- Ile: `min(ilosc, zloto_miasta / cena)` — zloto miasta czytane RAZ na poczatku (`:25`), wiec przy wielu
  pozycjach miasto moze zaplacic wiecej niz ma.
- Pieniadze: miasto -> bohater (`SellItemsAction`, `tw/.../Actions/SellItemsAction.cs:54-66`).
  Cena sprzedazy dla nie-handlowych przedmiotow obciazona kara 1,56 + 0,25*(tier-1) (patrz 4.1) —
  lord dostaje ok. 25-40% wartosci.

**ALE**: skad lord AI ma przedmioty? `MapEvent.LootCasualtyCharacter` (`tw/.../MapEvents/MapEvent.cs:1850-1888`):
dla zwyciezcy AI lup z poleglych/rannych = `PartyTradeGold += round(7,25 * poziom^2) / 55`
(`DefaultBattleRewardModel.cs:173-190`), **zadnych przedmiotow**. Przedmioty dostaje AI tylko z
`LootDefeatedPartyItems` (`MapEvent.cs:1595-1649`) — czyli z INWENTARZA pokonanej partii
(towary karawan, ladunki wiesniakow, konie, jedzenie, ewentualnie inwentarz gracza).
Wniosek: w vanilli AI praktycznie nie dostarcza broni i zbroi na rynki.

### 2.5 PartiesBuyHorseCampaignBehavior (czesc "sprzedaz")
`tw/.../PartiesBuyHorseCampaignBehavior.cs:89-135`
- Przy kazdym wejsciu lorda AI do miasta (bez sprawdzania wojny i zlota miasta!) sprzedaje WSZYSTKO,
  co nie jest jedzeniem ani wierzchowcem (`:94-105`).
- Jesli wartosc wierzchowcow w inwentarzu > 10% zlota handlowego partii — sprzedaje najdrozsze
  konie po 1 szt. (do 10 razy) az zejdzie ponizej 10% (`:106-135`).

### 2.6 Konie z wiosek
- Hodowle koni produkuja codziennie do magazynu wioski (`VillageGoodProductionCampaignBehavior.TickGoodProduction`,
  `:157-176`); ilosc = baza * (poziom_ognisk+1)*0,5, czyli x0,5 / x1,0 / x1,5 dla ognisk <200 / 200-599 / >=600
  (`DefaultVillageProductionCalculatorModel.cs:17-74`, `Village.GetHearthLevel`).
  Produkcja staje, gdy w wiosce lezy >= 1,5 * 5 * dzienna produkcja (`:138-155`, `Village.cs:248-256`).
- Bazowe tempo (szt./dzien przy mnozniku 1,0), `tw/.../Settlements/DefaultVillageTypes.cs:165-258`:

| Hodowla | zwykly (horse) | t2 (war_horse) | t3 (noble_horse) | inne |
|---|---|---|---|---|
| europe (imperium) | 2,1 | 0,5 | 0,07 | sumpter 0,5, mule 0,5, saddle 0,5, old 0,5, hunter 0,2, charger 0,2 |
| sturgia | 2,5 | 0,7 | 0,1 | jw. |
| vlandia | 2,1 | 0,4 | 0,08 | jw. |
| battania | 2,3 | 0,7 | 0,09 | jw. |
| steppe (khuzait) | 1,8 | 0,4 | 0,05 | sumpter 0,5, mule 0,5 |
| desert (aserai) | 1,7 | 0,3 | 0,05 | camel 0,3, war_camel 0,08, pack_camel 0,3, sumpter 0,4, mule 0,5 |

- Transport: wiesniacy biora 20% kazdej pozycji (4 przebiegi) z wioski (`VillagerCampaignBehavior.MoveItemsToVillagerParty`,
  `:205-235`) i sprzedaja w miescie handlowym (`SellGoodsForTradeAction`, `tw/.../Actions/SellGoodsForTradeAction.cs:17-68`):
  miasto placi, ograniczenie `zloto_miasta / cena`.

### 2.7 Karawany
- Kupuja tylko kategorie `IsTradeGood || IsAnimal` (`CaravansCampaignBehavior.CalculateBuyValue`,
  `tw/.../CaravansCampaignBehavior.cs:1381-1386`; ocena miasta `:1019-1026`). **Bron, zbroje, tarcze,
  strzaly, rzad konski — nigdy.** Konie wszystkich kategorii (wlacznie z noble_horse, bo IsAnimal) — tak.
- Sprzedaja wszystko, co maja, gdy cena w miescie jest wyzsza od sredniej swiatowej (`SellGoodsInternal`,
  `:1134-1216`; najpierw nie-konie, potem konie). Uzbrojenie moze sie tam znalezc tylko z lupu po wygranej
  bitwie (`LootDefeatedPartyItems`), wiec to margines.
- Po bitwie karawana wyrzuca nadmiar zwierzat i najtansze ciezkie przedmioty (`OnMapEventEnded`, `:427-499`).

### 2.8 Gracz
- Gracz jest jedynym, kto dostaje realne przedmioty z trupow (`MapEvent.cs:1871-1886`) i jedynym,
  kto kuje bron. Sprzedaz gracza = wiekszosc "jakosciowego" lupu na rynkach vanilli.
- Bron wykuta przez gracza (`IsCraftedByPlayer`) jest **usuwana z rynku nastepnego dnia** w calosci
  (`ItemConsumptionBehavior.DeleteOverproducedItems`, `:79-96`). To samo sztandary.

### 2.9 Inne (marginalne)
- Zmiana wlasciciela osady z klanu gracza: zawartosc `Stash` trafia do `ItemRoster`
  (`SettlementClaimantCampaignBehavior.cs:63-67`).
- Questy (stado do dostarczenia, narzedzia dla wioski) — nie uzbrojenie.
- **Nie znaleziono** zadnego innego codziennego/tygodniowego "spawnu" uzbrojenia do rynku zaleznego od
  dobrobytu. Jedyne generatory to warsztaty (2.1, 2.2) i hodowle (2.6).

---

## 3. Ujscia (kto zabiera uzbrojenie z rynku)

### 3.1 Konsumpcja miasta — ItemConsumptionBehavior (GLOWNE)
`tw/.../ItemConsumptionBehavior.cs:47-71`, codziennie dla kazdego miasta:
1. `DeleteOverproducedItems` (`:79-96`): usuwa calosc przedmiotow gracza i sztandarow; dla kazdego stosu
   z modyfikatorem (np. "rdzawy", "doskonaly") 5% szans na usuniecie 1 sztuki.
2. `UpdateSupplyAndDemand` (`:207-218`): podaz = srednia wykladnicza (0,85/0,15) z wartosci towaru na rynku,
   popyt = srednia z `GetDailyDemandForCategory(+1000 dobrobytu)` (`DefaultSettlementEconomyModel.cs:53-59, 91-94`).
3. `UpdateDemandShift` (`:178-205`): budzet dnia = `GetDailyDemandForCategory(miasto, kat)`; substytucja tylko
   dla zywnosci (uzbrojenie nie ma `CanSubstitute`).
4. `MakeConsumption` (`:142-176`): dla kazdego stosu (od konca) budzet kategorii
   `= popyt * indeksCeny^0,3` (`DefaultSettlementEconomyModel.cs:81-84`); kupowane `RoundRandomized(budzet/cena)`
   szt.; budzet kategorii zmniejszany o `budzet/cena*cena` — czyli wydany caly na pierwszym stosie.
   **Oczekiwana wartosc zjedzona na kategorie na dzien ~= budzet.**
   Pieniadze: `town.ChangeGold(+ilosc*cena)` — miasto ZYSKUJE zloto za wlasna konsumpcje (zloto z niczego).

Wzor popytu (`DefaultSettlementEconomyModel.cs:61-73`):
`budzet_dzienny = BaseDemand*0,001*Dobrobyt + LuxuryDemand*0,001*max(0, Dobrobyt-3000)`

Przyklad: denary/dzien zjadane na kategorie (mnoznik indeksu ceny ~0,94-1,08 pominiety):

| Kategoria | Dobr. 2000 | Dobr. 5000 | Dobr. 8000 |
|---|---|---|---|
| melee/ranged/shield T1 | 18 | 59 | 107 |
| *_2 | 14 | 49 | 91 |
| *_3 | 10 | 45 | 90 |
| *_4, *_5 | 8 | 40 | 82 |
| arrows | 60 | 210 | 390 |
| garment | 18 | 75 | 147 |
| light_armor | 14 | 67 | 136 |
| medium_armor | 10 | 59 | 125 |
| heavy_armor | 8 | 54 | 117 |
| ultra_armor | 6 | 35 | 74 |
| horse_equipment 1..5 | 18/14/10/10/10 | 55/47/39/41/43 | 97/88/75/80/85 |
| horse | 280 | 700 | 1120 |
| war_horse | 240 | 640 | 1060 |
| noble_horse | 240 | 700 | 1210 |
| sumpter_horse | 40 | 106 | 175 |

Suma uzbrojenia bez koni przy dobrobycie 5000: ~1 420 den./dzien/miasto.
Przy cenie zbroi T5 ~3000+ den. oznacza to ~1 sztuke na 2-3 miesiace na miasto.

### 3.2 Lordowie AI kupujacy konie — PartiesBuyHorseCampaignBehavior
`:60-88`, `BuyHorses :138-176`
- Wyzwalacz: wejscie lorda AI do miasta (nie w wojnie). Tylko kategoria **`horse`** (nie war/noble/juczne).
- Warunek: liczba wierzchowcow <= liczba zwyklych zolnierzy, i `srednia_cena_konia * wierzchowce / zloto_handlowe < 0,08`.
- Budzet: `(0,08 - ten_stosunek) * zloto * r1*r2*r3` (trzy losowe 0-1, srednio x0,125), ograniczony do
  `(zolnierze - wierzchowce) * srednia_cena`. Kupuje najtanszy kon, 2 przebiegi.
- Pieniadze: bohater -> miasto. Konie lezace w inwentarzu partii tylko przyspieszaja ruch; nie
  "wyposazaja" kawalerii. Wracaja na rynek przez 2.5.

### 3.3 Karawany kupujace zwierzeta
Kupuja konie (wszystkie kategorie-zwierzeta) i obowiazkowo juczne, gdy maja ich < liczby ludzi
(`BuyGoods`, `:1246-1271`). Konie wracaja na rynek w innym miescie (handel, nie ujscie netto).

### 3.4 Zuzycie surowcow przez warsztaty
Warsztaty zabieraja z rynku iron/hardwood/leather **bez zaplaty** dla linii uzbrojenia (2.1/2.2).
`ConsumeInputFromTownMarket` bierze PIERWSZY stos danej kategorii (`FindIndex`) — moga to byc wlewki
stali albo wegiel drzewny sprzedane przez gracza (kategorie iron / hardwood).

### 3.5 Inne
- `DiscardItemsCampaignBehavior` (`:17-127`): lord AI co godzine wyrzuca nadmiar zwierzat i przedmioty
  przy przeciazeniu — dotyczy inwentarza partii, nie rynku.
- AI bohaterowie: ekwipunek z szablonow (`HeroCreator`, `EquipmentSelectionModel`, `NPCEquipmentsCampaignBehavior`
  przy zmianie wladcy — `:17-33`). **Zero zakupow z rynku.**
- Wojsko AI: ulepszenia jednostek nie wymagaja przedmiotow. Zero zakupow.
- Zamowienia kowalskie (`CraftingCampaignBehavior`): tworzone przez AI (5%/dzien na bohatera w miescie,
  `:541-567`), realizuje tylko gracz; nagroda to zloto z niczego (`CompleteOrder :1149-1183`), lord dostaje
  zolnierza w tierze broni (`:1263-1274`). Rynek nieruszony.

---

## 4. Ceny i pieniadze

### 4.1 Mnoznik ceny
`tw/.../DefaultTradeItemPriceFactorModel.cs:170-182`:
`mnoznik = (popyt / (0,1*podaz + 0,04*wartosc_na_rynku + 2))^p`, p = 0,6 (zwierzeta 0,3).
- Kategorie handlowe (w tym horse, war_horse, sumpter): przyciecie **0,1-10**.
- Kategorie nie-handlowe (cale uzbrojenie, noble_horse): przyciecie **0,8-1,3**.

Kara transakcyjna (`:29-157`), mnozona przez cene (kupno) lub dzielaca (sprzedaz):
- baza 0,06; wojna +0,5;
- sprzedaz przedmiotu nie-handlowego, nie-zwierzecia, nie-konia przez nie-karawane: **+1,5 + 0,25*(tier-1)**;
- sprzedaz wierzchowca / jucznego: +0,8;
- wioska: +1 (sprzedaz) / +0,1 (kupno);
- karawana jako klient: kara x0,5; brak klienta (wycena warsztatu): x0,2.

Skutek: lord AI sprzedajac miecz T3 dostaje ~1/(1+0,06+2,0) = ~33% wartosci. Warsztat "wycenia"
swoj wyrob na ~1/(1+0,2*2,06) = ~70% wartosci.

### 4.2 Zloto miasta
`DefaultSettlementEconomyModel.GetTownGoldChange` (`:75-79`), codziennie:
`zmiana = 0,25 * (10000 + 12*Dobrobyt - zloto_miasta)` — zloto miasta jest "dociagane" do celu
(przy dobrobycie 5000: 70 000). To jest kran: wszystko, co miasto wyda na lup, wraca z niczego.

### 4.3 Bilans pieniezny uzbrojenia (vanilla)
| Przeplyw | Kto placi | Kto dostaje |
|---|---|---|
| Produkcja w warsztatach (2.1, 2.2) | nikt | nikt (surowce znikaja za darmo) |
| Konsumpcja miasta (3.1) | nikt | miasto (+zloto) |
| Lord sprzedaje lup (2.4, 2.5) | miasto | bohater |
| Lord kupuje konia (3.2) | bohater | miasto |
| Wiesniak sprzedaje konia (2.6) | miasto | wiesniak -> podatek wioski -> wlasciciel |
| Karawana kupuje/sprzedaje konia | karawana / miasto | miasto / karawana (+podatek handlowy) |

---

## 5. Lancuch surowcowy (vanilla)

### 5.1 Sciezka rynkowa (warsztaty — to, co faktycznie zasila rynki)

```
Kopalnia zelaza (iron_mine) --- iron (ruda, 50 den.) 10/dzien*(0,5..1,5) ---+
                                                                           |
Lesnik (lumberjack) ---------- hardwood (25 den.) 18/dzien*(0,5..1,5) -----+--> artisans / smithy / wood workshop
                                                                           |       --> bron, tarcze, luki,
Hodowla bydla (cattle_farm) -- cow 2/dzien --> artisans: cow -> 4 meat + 2 hides
Farma zboza (wheat_farm) ----- cow 0,2/dzien                                |       pancerze, rzad konski
                                     hides --> tannery (garbarnia): 1 -> 2 leather (230 den.)
                                                                           |
Farma owiec (sheep_farm) ----- wool 10/dzien --> wool_weavery: 1 -> 2 garment (pancerz T1!)
```

Brak: wegla drzewnego, wlewkow, stali, narzedzi jako wejscia. Wejscia sa abstrakcyjne:
- **bron biala**: T1 z niczego; T2-T3: 1 iron; T4: iron+hardwood; T5/T6: iron+hardwood+leather (artisans)
  lub po prostu 1 iron w kuzni;
- **luki/kusze**: T1 z niczego; T2-T3: hardwood; T4: hardwood+leather; T5: +iron (artisans) lub 1 hardwood w warsztacie drzewnym;
- **tarcze**: T1 z niczego; T2-T3 hardwood; T4 hardwood+iron; T5 hardwood+leather+iron;
- **strzaly**: z niczego;
- **pancerze**: garment i light z niczego (lub garment z welny); medium = leather (artisans) / iron (kuznia);
  heavy/ultra = iron;
- **rzad konski**: zawsze hardwood (+leather od T3, +iron od T4);
- **konie**: tylko hodowle w wioskach (zadnych warsztatow, zadnego rozmnazania).

1 jednostka surowca = 1 wyrob niezaleznie od tieru (zbroja T6 kosztuje 1 rude zelaza, jak miecz T2).

### 5.2 Sciezka kuzni gracza (rafinacja) — `DefaultSmithingModel.GetRefiningFormulas` (`:93-118`)

| Przepis | Wejscie | Wyjscie | Wymaga perka |
|---|---|---|---|
| Wegiel drzewny | 2 hardwood | 1 charcoal (3 z CharcoalMaker) | — |
| Surowka | 1 iron ore + 1 charcoal | 2 crude iron (3 z IronMaker) | — |
| Zelazo kute | 1 crude iron + 1 charcoal | 1 wrought iron | — |
| Zelazo | 2 wrought iron + 1 charcoal | 1 iron + 1 crude iron | — |
| Stal | 2 iron + 1 charcoal | 1 steel + 1 crude iron | SteelMaker |
| Stal szlachetna | 2 steel + 1 charcoal | 1 fine steel + 1 crude iron | SteelMaker2 |
| Stal tamaskenska | 2 fine steel + 1 charcoal | 1 thamaskene + 1 crude iron | SteelMaker3 |

- Energia: rafinacja 6 (PracticalRefiner zmniejsza), wykuwanie 10 + 5*tier, przetapianie 10 (`:140-168`).
- Koszt wykucia: suma `MaterialsUsed` czesci + zawsze 1 charcoal (`GetSmithingCostsForWeaponDesign`, `:375-389`).
- Przetopienie broni zwraca materialy czesci minus redukcja (`GetSmeltingOutputForItem`, `:187-206`).
- Wartosci (`DefaultItems.cs:125-133`): ruda 50, hardwood 25, charcoal 50, crude 20, wrought 30, iron 60,
  steel 100, fine steel 160, thamaskene 260.
- Przeliczenie na rude (bez perkow): 1 stal ~ 2 iron ~ 4 wrought + ... — ok. 6 rudy i ~10 wegla
  (= ~20 hardwood) na 1 sztabke stali, plus produkty uboczne crude iron.
- Gracz w vanilli kuje tylko bron z szablonow kowalskich (miecze, topory, wlocznie, bron miotana itp.);
  pancerzy, tarcz, lukow, kusz i strzal kuc nie mozna.
- AI nigdy nie rafinuje i nie kuje.

---

## 6. Luki i niewiadome

1. **Brak hamulca nadprodukcji.** Linie "bezsurowcowe" artisans nie patrza na zapas. Przy dobrobycie 5000
   miasto produkuje np. 1,5 szt./dzien broni T1 (srednio ~100-200 den.), a zjada ~59 den./dzien tej kategorii
   — szacunkowo ~1 szt./dzien netto przyrostu na kategorie T1. Jedyne inne ujscie to 5%/dzien na stos
   z modyfikatorem. **Szacunek z kodu, nie zweryfikowany w grze/logu.**
2. Ceny uzbrojenia sa praktycznie stale (0,8-1,3) — rynek nie sygnalizuje nadmiaru ani braku.
3. Wysokie tiery sa ograniczone glownie dostepnoscia surowca i niskim speed (0,05-0,15/dzien), nie cena.
   Zbroja ultra z artisans: srednio 1 szt. na ~13 dni na miasto (gdy jest zelazo).
4. Produkcja i konsumpcja uzbrojenia jest **pieniezne neutralna dla AI** (nikt nie placi), wiec zloto
   lordow nie ma zwiazku z iloscia uzbrojenia w swiecie.
5. Armie AI nie zuzywaja ani nie kupuja uzbrojenia — w vanilli nie ma "popytu wojskowego".
6. `PartiesSellLootCampaignBehavior` liczy limit zlota miasta raz (`:25`) — przy wielu pozycjach miasto
   moze zejsc ponizej zera; `PartiesBuyHorse` w czesci sprzedazy (`:94-105`) w ogole nie sprawdza zlota miasta.
7. `ConsumeInputFromTownMarket` bierze pierwszy stos kategorii — wlewki stali gracza (kat. iron) i wegiel
   (kat. hardwood) moga zostac "zjedzone" jako zwykla ruda/drewno.
8. Liczba przedmiotow w kazdej kategorii (ile mieczy T3 itp. jest `is_merchandise`) i ich srednia cena
   nie zostaly policzone — tier jest liczony w runtime z modelu wartosci, wiec trzeba by to zrzucic z gry
   (np. przez CrashScribe). Bez tego wartosci denarowe produkcji sa szacunkami.
9. Nie sprawdzano: `GetRandomItemModifierProductionScoreBased` (jaki % wyrobow ma modyfikator i podlega
   5%/dzien kasacji), `OnTownInventoryUpdated` (czy `InStore` jest aktualizowany na biezaco po kazdej transakcji).
10. Kultura: warsztat wybiera przedmioty kultury miasta lub neutralne; w ROT kultury sa nadpisane
    (`battania` = Polnoc itd.), wiec asortyment zalezy od tego, jakie przedmioty ROT oznaczyl jaka kultura.
