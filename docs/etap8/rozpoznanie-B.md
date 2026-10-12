# Etap 8 - ROZPOZNANIE B: ceny, dochody gracza, kuznia, warsztaty, BetterEconomy (2026-10-12, tylko odczyt)

Pozycje PLAN-DO-KONCA-MODA rozdz. "Etap 8": **8.3, 8.5, 8.9, 8.14, 8.18**. Kod: `scratchpad\zb\58` (w-toku/zbior-5-8, b461cc5),
dalej skrot `zb58`. Logi: `Modules\Armoury\Armoury-2026-10-12_02-44-09.log` (120 dob, WGRANIE 33; dalej "log l.N"),
CrashScribe `session-2026-10-12_02-44-08.log` ("CS l.N"), `CrashScribe\items-dump.csv` (12.10 03:22, wartosci po przeliczeniu, "dump").
Dekompilacje: gra `dec32\cs_full`, SandBox `e410\sandbox`, BK `e4tools\bkfull`, BKROTPatch `bkrotdec`, BEE `a6dec\bee`.
Model: **1 zl = 1 pens**; zold zolnierza w partiach lordow **5.03 zl/dobe** (CENY-A-DOCHODY 2.3, log l.27887-35990); kalendarz roku 364 dni.
Oznaczenia: [K] kod, [L] log, [D] dump/dane, [S] szacunek. "NK" = dziala tylko w nowej kampanii.

---

## 0. W skrocie

| Poz. | Co jest naprawde | Rada | NK |
|---|---|---|---|
| 8.3a | 24 ksiazki BK maja wartosc 7-10 zl (BKROTPatch dzieli /100), 36 ksiazek ROT 1 000-1 500; kupno u ksiegarza BK: zloto w nicosc, ksiazka z niczego | Z6 z audytu 13: x100 dla ksiazek < 100 zl + platnosc do ksiegarza | nie |
| 8.3b | **Zlecenia kowalskie gry (CraftingOrder) placa w STAREJ monecie i z niczego**: nagroda = Value projektu wg gry x 1.3 (nasze ceny historyczne go nie widza) | cena projektu historyczna (PriceOne), platnik = zleceniodawca; wybor szablonu tylko z odblokowanych klas | nie |
| 8.3c | 7 slotow kucia bez zadnej darmowej czesci (brama T2, CS l.1418) - decyzja Jeffa 01.09 "zamkniete do przetopu"; opis MCM klamie ("Craft button never dies") | zostawic regule, poprawic opis; zlecenia nie z zamknietych klas (8.3b) | nie |
| 8.5a | surowce a cena broni: indeks surowcow juz nie binarny (ladunki 100 kg), wagi skladnikow w starej skali (dniowka 10 zl) | kosmetyka (+-2 pp ceny) - odrzucic albo 1 linia w paczce cennika | nie |
| 8.5b | popyt wojenny liczony dwa razy: premia oczekiwan (do +63%, log l.35865) + prawdziwe zamowienia armii | `WarExpectationEnabled` domyslnie false | nie |
| 8.5c | kon przy SPRZEDAZY liczony dwa razy (czynnik gry 0.1-10 x nasza polka 0.25-4); kupno juz naprawione (RetailFromWorth) | przy sprzedazy konia podstawa = Value ze stanem, jak przy kupnie | nie |
| 8.5d | rzad konski z wagi ROT: bardy t6 140 kg -> 11 522 zl (hist. 480-2 400) | waga do kosztu = min(waga gry, 0.4 kg x pancerz konia) -> t6 ok. 2 530, t5 1 510, siodla ROT t2 237 | nie |
| 8.5e | helmy t4-6: 97 / 176 / 298 zl (kapalin 12-36, bascinet 48, garnczkowy 120-240) | dni plyty helmu t4+: w x 1.0 + 0.5 x (t-3) -> 39 / 80 / 127 | nie |
| 8.5f | luki t3-6: 38 / 63 / 101 / 159 (luk wojenny 12-18) | DaysBow {1.5,1.5,1.5,1.7,2,2.5} -> 19 / 26 / 35 / 48 (t1-2: 8 / 13) | nie |
| 8.5g | konie bez przeliczenia (HistoricalPrices.cs:28) | opcja: t3 "horse" x0.4 (rouncey), t4 rumaki `t2_*` x1.5 (kon zbrojnego); reszta bez zmian | nie |
| 8.9 | "gubione welna i skory" = dodatkowe cykle rzemieslnikow BK bez wyrobow ubocznych (ArtisanInputs.cs:182); kowadlo BK - pekniecie zbroi niszczy caly wsad, broni 50%; wraki - ZROBIONE (158); 16 niskich zgloszen lawy - 4 zalatwione, 11 otwartych | dopisac brakujace wyroby uboczne z zaplata kasy miasta; jedna regula pekniecia; 11 zgloszen w 2 paczkach drobnych | nie |
| 8.14 | klucz 14 `EconomicEventDailySpawnChance` 0.14 -> 0 - do skryptu; **"sol i piwo jako potrzeby" NIE jest kluczem pliku BEE** (lista zaszyta w kodzie BEE, w trybie BK bez skutku) | dopisac klucz 14; sol/piwo - odrzucic w BEE (u nas juz sa w zakupach wsi i ludu) | nie |
| 8.18 | ok. 60 wyplat zadan gry i SandBox: `GiveGoldAction(null, gracz, nagroda)` - z niczego; zlecenia kowalskie z niczego; nagroda turnieju (przedmiot) i zloto TXP z niczego; lapowki 5 000/3 000/100, golibroda 100, karczmarz 500, kolejka 50+0.2/czl. w nicosc | jedna regula "nagroda z kiesy zleceniodawcy, praca wyceniona w dniach zoldu" (wzor rozdz. 5.1); stale kwoty gracza w dniach placy do kiesy osady | nie |

**Sprostowanie do CENY-A-DOCHODY rada 4:** sakiewki 3 000 / 8 000 / 15 000 to pula, ktora gracz SAM wystawia jako gospodarz
turnieju (GrandTourney `HostMenu.cs:54-73`, zdejmowana z kiesy gracza przy obwieszczeniu, `TourneyBehavior.cs:694-697`) - nie jest dochodem.
Dochod gracza z turnieju AI to przedmiot nagrody (z niczego) + zloto TXP 700 + premie (z niczego) + zaklady.

---

## 1. Pozycja 8.3 - ksiazki BK, kuznia lordow, brakujace czesci broni

### 1.1 Ksiazki BK po 7-10 zl

- **Dowod [D]:** dump - 24 ksiazki BK: 6 opowiesci po **7** (book_green_knight, book_love_castle ...), 18 ksiazek umiejetnosci po **10**
  (book_oneHanded, book_tactics ...); 36 ksiazek ROT po **1 000** (religie, historie) i 9 slownikow ROT po **1 500**; wszystkie `merchandise=1`.
- **Przyczyna [K]:** BKROTPatch `BKItemsInitializePatch.Prefix` - `value /= 100` (bkrotdec `BKROTPatch.decompiled.cs:6941-6984`, linia
  `value /= 100` ok. :7017). BK u ksiegarza liczy `Value x 1000` (`BKEducationBehavior.cs:385, :397`), nasz `BookTranspiler` zamienia 1000 -> 1
  (`HistoricalPrices.cs:969-1028`), wiec u ksiegarza 7-10 zl. Audyt 13 (docs/audyt-2026-10-09/13-UMIEJETNOSCI.md:280, :358 "Z6", :512) - **Z6
  niezrobione** (grep `Book` w `HistoricalPrices.Apply` - brak).
- **Druga usterka (zamknieta ekonomia) [K]:** `BKEducationBehavior.OnBuyBookConsequence` :396-398 - `Hero.MainHero.ChangeHeroGold(-cena)`
  (zloto w nicosc, ksiegarz nic nie dostaje) i `PartyBelongedTo.ItemRoster.AddToCounts(ksiazka, 1)` (ksiazka z niczego - z rosteru
  ksiegarza nie schodzi).
- **Zmiana:**
  1. `HistoricalPrices.Apply`: przedmioty z `book_` / kategoria ksiazek BK z Value < 100 -> x100 (24 szt.: 700 / 1 000 zl = 3-4 L, rekopis XIV w.
     od kilku szylingow do kilku funtow); dopisac do `_target` (blokada nadpisan `ValueSetPrefix` je chroni). Warunek "< 100" = bez
     podwojenia; przy wczytaniu BKROT znow daje 7-10, my znow x100.
  2. Transpiler na `OnBuyBookConsequence` (delegat w srodku - lambda `<OnBuyBookConsequence>b__..`): `ChangeHeroGold(-x)` ->
     `GiveGoldAction(gracz, ksiegarz = Hero.OneToOneConversationHero, x)`; ksiazka zdejmowana z rosteru ksiegarza (`GetBookRoster`), gdy tam jest.
- **Platnik / odbiorca:** gracz -> kiesa ksiegarza (notabl BK). Na targu - jak kazdy towar (kasa miasta).
- **Latki:** `HistoricalPrices.Apply` (nasza metoda), 1 transpiler na BK. Kolizje: `BookTranspiler` (ta sama metoda - stala 1000 juz zamieniona;
  nowy transpiler musi szukac `ChangeHeroGold`, nie stalej). Popyt kategorii ksiazek: jesli kategoria jest w `HistDemandScaling`, przelicznik
  policzy sie sam z nowej wartosci (sprawdzic linie startowa "popyt miast przeliczony ... w N kategoriach").
- **Klucz:** `BookPriceFix` (domyslnie true). **NK:** nie. **Koszt:** S.

### 1.2 "Kuznia lordow" = zlecenia kowalskie gry (CraftingOrder) - NOWE ZNALEZISKO

- **Dowod [K]:**
  - `CraftingOrder` (konstruktor, `TaleWorlds.CampaignSystem.CraftingSystem/CraftingOrder.cs:107-122`): projekt zlecenia powstaje przez
    `Crafting.GenerateItem` (wartosc wzorem gry), `BaseGoldReward = GetTheoreticalMaxItemMarketValue(projekt) x (0.9-1.1)`, a dla broni
    `GetTheoreticalMaxItemMarketValue = Value x 1.3` (`DefaultTradeItemPriceFactorModel.cs:196-203`).
  - Nasze `HistoricalPrices.PriceOne` przelicza tylko sztuki wykute przez gracza (`ArmouryBehavior.cs:1741-1743`, zdarzenie `OnNewItemCrafted`);
    projektu zlecenia nikt nie przelicza (grep `GenerateItem|BaseGoldReward|ItemValueModel` w zb58 - zero) -> **nagroda w starej monecie**
    (bron t3 wzorem gry rzedu tysiecy, t5 kilkanascie tysiecy [S]; ta sama bron po przeliczeniu: miecz t3 ok. 30 zl, t5 ok. 116 zl - CENY 1).
  - `CompleteOrder` (`CraftingCampaignBehavior.cs:1150-1154`): `GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, kwota)` - **z niczego**.
  - `CreateTownOrder` (:944-956) i `CreateCustomOrderForHero` (:986-1001): szablon `CraftingTemplate.All.GetRandomElement()`, czesci do tieru
    trudnosc/50 - bez `IsOpened`. To jest pozycja 5 z OTWARTE-2026-09-01 ("zamowienia lordow nie znaja IsOpened").
- **Skutek:** gracz moze dostawac tysiace zl z niczego za jedna bron (zrodlo wrazenia "wszystko tanie" obok nagrod zadan); zlecenia z klas
  zamknietych bramka (1.3) sa niewykonalne.
- **Zmiana:**
  1. Postfiks na konstruktorze `CraftingOrder`: `HistoricalPrices.PriceOne(PreCraftedWeaponDesignItem)` i przeliczenie `BaseGoldReward`
     (pole readonly - refleksja) = Value historyczna x 1.3 x (0.9-1.1). Przy wczytaniu: ten sam przelicznik dla zlecen z zapisu
     (`_craftingOrders`, sloty miast i zlecenia wlasne) - tylko te, ktorych `BaseGoldReward` > 3 x nowej wartosci (bez podwojenia).
  2. Transpiler na `CompleteOrder`: `null` -> `craftingOrder.OrderOwner` (GiveGoldAction sam tnie kwote do kiesy placacego -
     `GiveGoldAction.cs:13`); przy niepelnej zaplacie komunikat po angielsku ("X could pay only N of the agreed M.").
  3. Prefiks na `CreateTownOrder` / `CreateCustomOrderForHero`: szablon losowany tylko sposrod szablonow, w ktorych kazdy wymagany slot ma
     co najmniej 1 otwarta czesc gracza (`IsOpened`), tier czesci najwyzej najwyzszy otwarty tier tego szablonu + 1.
- **Platnik / odbiorca:** zleceniodawca (notabl albo lord w miescie) -> gracz. Sztuka oddana zleceniodawcy - jak dotad.
- **Klucze:** `CraftOrdersHistorical`, `CraftOrdersOpenedOnly` (oba true). **NK:** nie (zlecenia z zapisu przeliczone przy wczytaniu).
- **Kolizje:** XP kowalstwa za zlecenie liczy model gry od Value (`HistoricalPrices.cs:991` - postfiks na `GetSkillXpForSmithingInCraftingOrderMode`
  przelicza XP od wartosci sprzed przeliczenia) - po PriceOne projektu XP musi dalej brac `Orig`; sprawdzic, ze `Orig` projektu zapisze sie przed
  PriceOne (PriceOne zapisuje `_orig[it] = was` - :339, OK). Nasze zamowienia na ZBROJE (`Orders.cs`) to osobny system - nietkniety.
- **Koszt:** M.

### 1.3 Brakujace czesci broni (brama kucia)

- **Dowod [L]:** CS l.1418: "brama kucia - prog T2; odebrano darmowosc 267 czesciom, podloga ocalila 0; klasy zamkniete do odblokowania
  przetopem: ThrowingAxe.Blade, Pike.Handle, Javelin.Handle, OneHandedSwordDual.Blade, OneHandedSwordDual2.Blade, ROT_ThrowingAxe.Handle,
  ROT_ThrowingAxe.Blade". Jeff ma `ForgePartsFreeBelowTier` 2 (Armoury.json:149).
- **Kod [K]:** `CrashScribe Mends.LoreForgeGate` (`Mends.cs:870-997`) - przebieg 1 nadaje darmowosc najnizszemu pasmu slotu, jesli jest pod
  progiem; klasy z pula od T4 zostaja zamkniete celowo (komentarz :948-954, decyzja Jeffa 01.09). Odblokowanie: gra otwiera czesci od
  NAJNIZSZEGO tieru calego szablonu (`CraftingCampaignBehavior.OpenNewPart` :662-684), punkty badan z kucia i przetopu (:641-660); u nas
  `SmeltTab.DoSmeltingPostfix` otwiera wszystkie czesci tylko legend. ROT_ThrowingAxe ma 2 czesci, obie T4 (ROT-Content
  `crafting_templates.xml`; thenn_throw_head domyslna, thenn_throw_handle nie) - odblokowanie tylko przetopem toporkow Thennow.
- **Usterka:** opis MCM `ForgePartsFreeBelowTier` (`Settings.cs:1214`) obiecuje "A safety floor always keeps one band of parts per required
  slot, so the Craft button never dies" - nieprawda od decyzji 01.09.
- **Zmiana:** regula zostaje (decyzja Jeffa); poprawic opis (po angielsku: "...classes whose whole pool starts above the threshold stay closed
  until you unlock their parts by smelting; the log lists them"); zlecenia z tych klas odpadaja przez 1.2 pkt 3. **NK:** nie. **Koszt:** S (tekst).

---

## 2. Pozycja 8.5 - ceny

### 2.1 Surowce a cena broni

- **Stan [L]:** log l.35865 "indeks rudy min/med/max 0.50/0.64/1.50, drewna 0.50/0.75/1.50, skory 0.50/1.45/1.50, lnu 0.80/1.50/1.50" -
  indeks juz nie jest binarny (problem S2 z AUDYT-KONFLIKTY rozwiazal ladunek 100 kg, wpis 50).
- **Co zostalo [K]:** `ArmsPricing.MaterialIndex` (:350-366) wazy skladniki kosztem z `Compute` (:187-256): robota `days x SmithDayWage` = 10 zl
  dla kazdego tieru (`Settings.cs:474`), rog/sciegno lukow `20 x t` w skali gry; a Value liczy `HistCost` (`HistoricalPrices.cs:262-275`):
  dniowka 3 + 1.5/tier, rog x0.1. Udzial materialu w cenie: kolczuga t4 dzis 17% (indeks rusza cene x0.91-1.09), po ujednoliceniu 22%
  (x0.89-1.11) [S]. Historycznie material to 10-25% ceny (CENY-HISTORYCZNE rozdz. 7) - mala reakcja ceny jest prawdziwa.
- **Rada:** odrzucic jako osobna pozycje (zysk +-2 pp); jesli paczka cennika (2.4-2.6) i tak rusza `ArmsPricing`, to 1 linia: `MaterialIndex`
  bierze wagi z tych samych skladnikow co `HistCost` (jedna zasada). **NK:** nie.

### 2.2 Popyt wojenny liczony dwa razy

- **Dowod [K]+[L]:** `SupplyDemand.Demand` (:188-204): `prosp *= 1 + ArmsPricing.WarPremium(frakcja)` (poza konmi); premia rosnie przy
  kazdym wypowiedzeniu wojny o 0.5 x sila wroga/wlasna (0.5-2), do 300%, gasnie 15 dni (`ArmsPricing.cs:395-411`, `:337-343`;
  `Settings.cs:482-484`). W 120 dobach 70 linii "kupcy oczekuja zakupow broni", premie z konca biegu: Tyrosh +63%, Myr +41%, Pentos +37%,
  Dorne +37%, Lys +34% (log l.35865). Jednoczesnie prawdziwe braki armii wchodza do tego samego popytu jako zamowienia (`_unmet x
  SupplyDemandOrderWeight`, :201-203), a AiGear naprawde wykupuje polki. Komentarz w kodzie mowi wprost: "popyt wojenny bierze sie z
  PRAWDZIWYCH zakupow armii ... nie z mnoznika" (:35). AUDYT-KONFLIKTY S3 "POTWIERDZONE".
- **Skutek:** ceny uzbrojenia w krolestwach w wojnie x1.29 (premia 66%) do x1.47 (116%) ponad prawdziwy niedobor [S: polka ((D+1)/(S+1))^0.5];
  premia steruje tez wywozem kupcow (DailyTrade czyta `Demand`) - towar jedzie tam, gdzie nikt go jeszcze nie kupil.
- **Zmiana:** `WarExpectationEnabled` domyslnie **false** (klucza nie ma w Armoury.json Jeffa - zadziala domyslna). Kod oczekiwan zostaje
  (linia logu "premie wojenne" znika). **Platnik:** bez zmian przeplywow - tylko cena. **NK:** nie. **Koszt:** S.

### 2.3 Cena konia liczona dwa razy

- **Dowod [K]:** `SupplyDemand.PricePostfix` (:381-403): przy KUPNIE (`!__3`) z `RetailFromWorth` podstawa = `ItemValue x 1.1` - cudze czynniki
  usuniete (konie tez, bo `Equipmentish` obejmuje Horse - :72). Przy SPRZEDAZY podstawa = `__result`, czyli cena gry z czynnikiem
  podazy/popytu gry - dla koni (towar handlowy, zwierze) 0.1-10, wykladnik 0.3 (`DefaultTradeItemPriceFactorModel.cs:170-181`;
  AUDYT-TOWARY.md:268, AUDYT-PELNY-EKONOMIA S1) - i dopiero razy nasza polka 0.25-4. Dla zbroi i broni czynnik gry przy sprzedazy to tylko 0.8-1.3.
- **Skutek:** sprzedaz konia (gracz, lordowie AI - "sprzedali nadwyzke 379 za 115 293 = 304 zl/szt.", CENY 3.4) moze byc zanizona albo zawyzona
  kilkukrotnie wobec kupna w tym samym miescie.
- **Zmiana:** przy sprzedazy konia (`ItemType Horse`) podstawa = `ItemValue` (stan) x mnoznik ceny sprzedazy gry bez czynnika podazy (albo
  `__result / czynnik gry`), potem polka i sufit `SellByCondition` jak dotad. **NK:** nie. **Koszt:** S. Sprawdzic petle "kup-sprzedaj" probe
  jak przy 155 (zero zysku w jednym miescie).

### 2.4 Rzad konski z prawdziwej wagi

- **Dowod [D]+[K]:** dump (merchandise=1): t0 mediana **588** (28 szt., m.in. padded_horse_armor 80 kg Leather 1 041), siodla ROT t2 (braavosi_saddle,
  myrish_saddle ... 100 kg Plate, pancerz 35) **1 626**, bardy ROT t5 (140 kg) **7 861**, t6 (140-145 kg, pancerz 72-80) **11 522-12 563**,
  rot_horse_armor19 **46 089** (unikat x4). Waniliowe bardy maja realne wagi: chain_barding 26 kg / pancerz 64 = 0.41 kg/pkt, imperial_scale 30/75 =
  0.40, studded_steppe 23/52 = 0.44. Historia: siodlo 24-60, barda 480-2 400 [S] (CENY-A-DOCHODY 4).
  `ArmsPricing.Compute` :204-212 liczy uprzaz jak zbroje z wagi gry (plyta t>3: dni = w x (1.5 + (t-3)) + t -> barda t6 140 kg = 636 dni pracy).
- **Zmiana:** w `Compute` dla `HorseHarness`: waga do kosztu `wr = min(waga gry, max(3, 0.4 x BodyArmor uprzezy))` (klucz `HarnessKgPerArmor` 0.4).
  Wyliczone tym samym modelem (q = 1):

  | Grupa | dzis (dump) | po | historia |
  |---|---|---|---|
  | t0 (siodla, kropierze) mediana | 588 | **145** (39-202) | siodlo 24-60, kropierz [S] |
  | bandit_saddle_steppe 5 kg | 78 | 54 | 24-60 |
  | siodla ROT t2 100 kg | 1 626 | **237** | - |
  | stark_horse_armor t3 70 kg | 4 588 | 1 235 | - |
  | chain_barding t4 26 kg (wanilia) | 1 563 | 1 539 (bez zmian) | 480-2 400 |
  | bardy ROT t5 140 kg | 7 861 | **1 506** | 480-2 400 |
  | bardy ROT t6 140-145 kg | 11 522 | **2 531** (2 302-4 002) | 480-2 400 (najlepsze wyzej) |

- **Kto czyta te sama liczbe (kontrola):** `HistCost` -> Value (targ, lup, skup, okup od ekwipunku); `WorkshopLaw.Needs` (:162-178 - material
  i dni warsztatu z tych samych kg - spojne: warsztat zuzyje mniej metalu na barde); `MendMaterial.Labor` (:429-437 - robota naprawy, ulamek
  `MakingLabor`). **Nie** czytaja: `Recipes.BuildRecipe` (:262-310, kwit kuzni gracza z wagi gry - barda t6 = 224 sztabki t6 + 56 t5 = ok. 1 568 zl
  materialu wobec nowej wartosci 2 531; **siodlo ROT t2: material ok. 250 zl > wartosc 237 - kucie ze strata**) i `MendMaterial.UnitKg` (:74,
  material naprawy z wagi gry). Rada: ta sama `wr` w `Recipes.BuildRecipe` i w materiale naprawy uprzezy (jedna zasada), inaczej kucie i
  naprawa siodel ROT zjedza wiecej niz sa warte.
- **Skutek w ekonomii:** E6/R3 "uprzaz dla jezdzcow t4+" (AiGear) i notable ("konie i rzedy 6/6 701 zl" w dobie, log l.36029) placa mniej - mniej
  zlota do warsztatow i kas miast; nic z niczego. Waga w ekwipunku bez zmian (140 kg). **NK:** nie. **Koszt:** S (+S za Recipes/Mend).

### 2.5 Helmy t4-6

- **Dowod [D]:** dump: helm plytowy t4 mediana **97** (112 szt., knight_kettle 1.5 kg 100), t5 **176** (lannister_helm_open 2.4 kg 186), t6 **298**
  (bolton_knight_helmet 3 kg 333). Historia: kapalin 12-36, bascinet 48 (Tower, pewne), helm garnczkowy 120-240 (CENY-HISTORYCZNE 7).
  Przyczyna: ten sam wzor dni co napiersnik t>3 (`ArmsPricing.cs:209`): t5 2.4 kg = 13.4 dnia mistrza po 9 zl.
- **Zmiana:** `HeadArmor` + `Plate`, t >= 4: dni = `w x 1.0 + 0.5 x (t - 3)` (klucz `HelmDaysPerKg` 1.0). Wynik (model, q = 1): t4 **39**
  (27-246), t5 **80** (47-298), t6 **127** (66-389); t1-3 i kolczy kaptur bez zmian (PLAN: tylko t4-6; opcja: ta sama regula od t3 - t3 53 -> ok. 24).
- **Uwaga [L]:** helmow na polkach 945 szt. wobec 16 375 napiersnikow (log l.35865) - dzis drogie takze z niedoboru; nizsze dni = warsztat
  (`WorkshopLaw` liczy dni z `HistDays`) zrobi ok. 2.4x wiecej helmow na roboczodzien - niedobor zmaleje. Zakupy helmow w dobie: 298 szt. za
  28 742 zl (log l.36029; zalogi miast 64 szt. za 15 276 - to glownie t4-6) - po zmianie ok. -10-15 tys. zl/dobe przeplywu do kas miast [S].
- **NK:** nie. **Koszt:** S.

### 2.6 Luki wojenne t3-6

- **Dowod [D]+[K]:** dump: luk t3 **37.5**, t4 **63**, t5 **101**, t6 **159** (woodland_longbow 173); historia: luk dlugi bialy 12, malowany 18 (1341,
  pewne). `DaysBow = {2,3,4,6,8,12}` (`ArmsPricing.cs:66`) x dniowka wg tieru; `HistBowLaborMultiplier` 1.0 (`Settings.cs:645`) mnozy wszystkie tiery.
- **Zmiana:** nowa tabela dni `{1.5, 1.5, 1.5, 1.7, 2, 2.5}` (klucz albo stala; mnoznik zostaje). Wynik: t1 8, t2 13, **t3 19, t4 26, t5 35, t6 48**
  (rog/sciegno `20t x 0.1` zostaje - kompozyty t5-6 drozsze z materialu). t1-2 musza spasc razem, inaczej luk t2 bylby drozszy od t3.
- **Skutek:** warsztat zrobi 2.6-4.8x wiecej lukow t3-6 na roboczodzien (`WorkshopLaw` z `HistDays`) - obserwowac polki lukow (dzis 2 995 szt.,
  ceny placone 1.05-1.17 x Value, CENY 3.3). **NK:** nie. **Koszt:** S.

### 2.7 Konie wedlug kategorii (opcja)

- **Stan [D]:** konie nieprzeliczone (`HistoricalPrices.cs:28`, zywy inwentarz tylko Animal - :59-68). Dump: t1 juczne 99, saddle_horse 140 (hist.
  60-160 / rouncey 160-480 - OK); t3 empire/battania/aserai_horse **929-1 081**, camel 1 177 (rouncey 160-480 - 2.2-6.6x); t4 vlandia/sturgia/
  khuzait_horse 1 114-1 258 (palafrej 480-2 400 - OK); t4 rumaki `t2_*` **1 221-2 380** (kon zbrojnego 1 820-3 940 - dolna granica);
  destriery `t3_*` 10 000, war_horse 10 018, charger 10 973 (9 600-24 000 - OK), hunter 5 980 (courser 2 400-9 600 - OK).
- **Zmiana (opcja):** tabela jak `LivestockEach`: t3 kategorii `horse` x0.4 (ok. 370-470), t4 `war_horse` (`t2_*`) x1.5 (1 830-3 570); reszta bez zmian.
  Lordowie w 28 dobach kupili 381 zwyklych i 262 bojowe konie za 888 588 zl (CENY 3.4) - bilans zmiany ok. 0 [S: -230 tys. na zwyklych,
  +236 tys. na bojowych]. Klucz `HistHorsePrices` (domyslnie: decyzja Jeffa).
- **Kolizje:** XP Jazdy za oddanie konia w Spoils liczone od wartosci konia (audyt 13 Riding) - to 8.2 (rozpoznanie A); K3 kon rycerza
  (Stables.KnightBuy), HorseFate, ceny rekruta konnego (`RecruitCost`) - czytaja Value, dzialaja same. **NK:** nie. **Koszt:** S.

---

## 3. Pozycja 8.9 - kowadlo i warsztaty

### 3.1 "Gubione welna i skory" (TOWARY 3, Z2 / paczka 147)

- **Dowod [K]:** `ArtisanInputs.cs:36-37` (regula: liczbe cykli ustala pierwszy wyrob, nastepne wyroby przyciete do niej) i :182
  (`remove = Math.Min(added - _cycles, ...)` - gdy BK dopisal MNIEJ sztuk wyrobu ubocznego niz cykli, nic nie dopisujemy). Krowa: mieso count 3,
  skory count 2 -> 3 krowy zuzyte, skor tylko za 2; owca: welna count 1 (wartosc 83) -> owce z dodatkowych cykli bez welny. Decyzja wpisana w
  CHANGELOG zb58 :7023 ("nic nie dopisujemy ... welna i tak zalega, indeks 0.38"); docs/towary3/WYNIK.md:91-92 "do osobnej poprawki".
- **Ocena:** wsad (zwierze) znika, a fizyczny wyrob uboczny nie powstaje - ujscie towaru. Dzis welna placona 0.65 x Value (CENY 3.1), ale
  od etapu 6 sa receptury wedlug wartosci (W1: filc 3 welny -> 2) i regulator wsi (W3 HarvestValve) - nadmiar zbije produkcja owczarni.
- **Zmiana:** dla wyrobu z `added < _cycles` dopisac `(_cycles - added)` sztuk na polke miasta, kasa miasta placi warsztatowi cene kupna
  (ta sama droga co BK - zloto miedzy dwiema kasami). Klucz `ArtisanByproductsFull`. Runo z uboju: granica fizyki ok. 0.15 szt. z owcy
  (plan K13 pkt 191) - jesli Jeff woli fizyke, to welna z dodatkowych cykli z prawdopodobienstwem 0.15 (rada: tak, jedna liczba w ustawieniu).
  **NK:** nie. **Koszt:** S.

### 3.2 Kowadlo BK: pekniecie zbroi niszczy caly wsad, broni 50% (TOWARY 3, Z3 / paczka 132)

- **Dowod:** docs/towary3/WYNIK.md:93 ("dwie reguly jednego zjawiska"; zwrot polowy wymaga proby z `GetCraftingInputForArmor`). W zb58 bez
  zmian (`ArmourQuality.cs:225-242` - pekniecie z `Forge.FailureChance`, kwit BK `FletchForge.cs:207, :265`; brak zwrotu).
- **Zmiana:** przy pekniecu zbroi w zakladce CRAFT zwrot 50% kwitu do sakw (jak bron w grze) - postfiks na sciezce pekniecia BK (do znalezienia w
  dekompilacji BK `BKArmorCraftingVM` / `BKSmithingModel`); klucz `ArmourBotchHalfBack`. **Koszt:** S-M (wymaga proby). **NK:** nie.

### 3.3 Wraki

- Paczka 158 (`WrecksToScrap`) + 0f43e2c: zaden kowal miasta nie odnawia wraku na zadnej drodze; wrak = zlom albo wlasne kowadlo
  (docs/towary3/WYNIK.md:210, :225-227, :302, :311). Z4 (Spoils "bandyci ukradli" do nikad) ZROBIONE w 3.9 (CHANGELOG zb58 :3441-3448).
  **Do etapu 8 nic w kodzie** - tylko reczne sprawdzenie Jeffa (8.16: sprzedaz czystej, Battered i wraku).
- Z5 (material zdejmowany przed petla w `SmithMenu.ApplyRoster` / `SpoilsSeal.Apply` - przepada tylko przy wyjatku) - niskie, zostawic.

### 3.4 16 niskich zgloszen przegladu lawy (docs/przekazanie-lawa-2026-10-07/PRZEKAZANIE-DLA-DRUGIEGO-CZATU.md:74-90; 15 roznych)

| # | Zgloszenie | Stan w zb58 | Rada |
|---|---|---|---|
| 1 | HarnessLabor rozni sie o 1 zl przy wylaczonej regule | ZROBIONE (WYNIK.md "HarnessLabor (135)") | - |
| 2 | `MendMaterial` szacunek bez 1e9 przepelnia int | czesciowo: `Pick(k, 1e9f, ...)` jest w :830; sprawdzic druga sciezke (:793) | paczka drobna |
| 3 | dwie ceny kowala tej samej sztuki ("Pick" od modyfikatora, uprzaz od ksiegi) | wraki ujednolicone (`SlotWreck`, CHANGELOG :6513); reszta otwarta | paczka drobna |
| 4 | stawka godzinowa BK 0.375 -> 0 (tylko bez `ForgeOneClock` i bez karnetu) | otwarte, tylko przy wylaczonym kluczu | odrzucic (domyslnie wlaczony) |
| 5 | karnet BK bez pieniedzy | ZROBIONE 1fe5c35 | - |
| 6 | ekran klanu: utrzymanie warsztatu bez poziomu plac (`WorkshopTrade.cs`) | otwarte (`UpkeepAt` = Upkeep x WageIndex :131 - ekran gry pokazuje gola stala) | paczka drobna (tekst) |
| 7/16 | RC: dniowki i warta nie patrza na rezerwe miasta 20 000 (`Work.cs`) | otwarte | paczka drobna RC |
| 8 | RC: `Math.Max(1)` nad `RoundRandomized` przeplaca dniowki < 1 zl i kasuje polowe placy po nasyceniu | otwarte (`Work.cs:118`, :131) | paczka drobna RC |
| 9 | RC: przy wylaczonym `HistoricalTownRates` sprzedaz domu nieaktywna przy cenie 0 (`Homes.cs:216`) | do sprawdzenia | odrzucic (domyslnie wlaczony) |
| 10 | RC: pusta kasa nie zatrzymuje pracy w toku, niezaplacone dni licza sie do nasycenia (`Work.cs` ok. :334) | otwarte | paczka drobna RC |
| 11 | RC: menu pracy pokazuje wlasny rzut dniowki, nie wyplate | czesciowo: przy `HistoricalTownRates` menu pokazuje dniowke oczekiwana (`Work.cs:249`), wyplata losuje grosze (:118) | odrzucic (kosmetyka) |
| 12 | konie: kon po cenie targu wypycha koszt najemnika konnego ponad prog 5 000 AI (`RecruitCost`) | prawdopodobnie zalatwione przez 160 (konny z wlasnym koniem, bez doplaty; `RecruitCost.cs:45-49`) | sprawdzic w projekcie |
| 13 | konie: mnozniki prawa BK i perkow mnoza tez konia (`RecruitCost`) | j.w. | sprawdzic w projekcie |
| 14 | zamowienia: pusta kiesa tlumi wpis popytu (`SmithMenu.cs` ok. :1877) | otwarte | paczka drobna |
| 15 | `AiWear` rezerwa AI na naprawy przy poziomie plac 1, naprawa przy poziomie miasta | otwarte (`AiWear.cs` - brak `TownWage` w pliku) | paczka drobna |

Rada: 2 paczki drobne - **E8/B-ARM** (2, 3, 6, 14, 15, 3.1, 3.2) i **E8/B-RC** (7, 8, 10); 4, 9 i 11 odrzucic (wylaczone klucze albo kosmetyka);
12-13 sprawdzic przy projekcie (po 160 moga byc martwe).

---

## 4. Pozycja 8.14 - BetterEconomy: bez losowych susz, sol i piwo

- **Stan pliku [D]:** `Modules\BetterEconomy\ModuleData\better_economy_settings.xml` - 41 185 B, 08.10 23:39, SHA-256 3a24554b6459d84c... (po
  skrypcie 13 kluczy `-ListaZFundamentu`, STAN-PRAC:753). Linia 361: `<EconomicEventDailySpawnChance>0.14</EconomicEventDailySpawnChance>`.
- **Losowe zdarzenia [K]:** BEE `EconomicEventCampaignBehavior` - szansa 0.14/dobe, do 12 naraz (XML :359-361), typ losowy (:339-348): jarmark, zly
  zbior, susza, zaraza bydla, boom gorniczy, sezon karawan. Przy BK modele produkcji BEE sa nieczynne - "susza" nie zmienia plonu gry, rusza
  tylko wirtualne liczby BEE (podatek, clo, plynnosc, popyt) i okno BEE (audyt 10-pomysly F10, Z5).
- **Zmiana:** klucz 14 w `tools/bee/zamknij-ujscia-bee.ps1` (lista `Nowy-Klucz` :50-58): `EconomicEventDailySpawnChance`, float, 0.14 -> 0, "World:
  BEE random events"; ten sam w `cofnij-ujscia-bee.ps1`; skrypt musi przyjac plik, w ktorym 13 kluczy juz ma wartosci docelowe (stan "juz
  zmieniony" - sprawdzic `Stan-Klucza` :193+ na kopii obecnego pliku). Uruchamia Jeff (gra zamknieta, kopia .bak), albo agent po zgodzie przy
  wgraniu koncowym. Trwajace zdarzenia wygasna same. **NK:** nie (BEE czyta plik przy starcie gry).
- **Sol i piwo jako potrzeby [K]:** w pliku BEE nie ma listy potrzeb. Lista podstawowa jest zaszyta w kodzie:
  `TownEconomyCampaignBehavior.cs:124` `_staples = { grain, fish, meat, butter, cheese }`, `EconomicEventCampaignBehavior.cs:592-598` `IsStaple`
  (grain, bread, fish, meat, butter); sol jest u BEE wrecz luksusem (`IsLuxuryItem` :3735: jewel, velvet, wine, spice, salt). Zmiana wymagalaby latki
  Harmony na prywatne metody BEE, a w trybie BK te listy steruja tylko wirtualnymi liczbami i dostawami karawan BEE, ktore zamknal klucz 7
  (`CaravanDeliveryMinGold` 1e9). **Skutek w grze ok. 0.** U nas sol i piwo juz sa potrzebami: zakupy kiesy wsi w miescie
  (`TownFolk.Live.cs:42` `VillIds = { salt, tools, linen, pottery, beer }`), cena przy pustej polce (`RawPrice.cs:300`).
- **Rada:** 8.14 = tylko klucz 14; "sol i piwo w BEE" odrzucic (powod: cudzy kod bez skutku przy BK, potrzeba juz pokryta naszym E5).
  Instrukcja dla Jeffa (Z.9) - 1 linia o kluczu 14. **Koszt:** S.

---

## 5. Pozycja 8.18 - dochody gracza w nowych cenach

### 5.1 Nagrody zadan

- **Dowod [K]:** wyplaty w zadaniach gry `TaleWorlds.CampaignSystem.Issues` (35 plikow z `RewardGold`): `GiveGoldAction.ApplyBetweenCharacters(null,
  Hero.MainHero, RewardGold)` x23, `(null, ..., _rewardGold)` x10, inne kwoty z `null` x ok. 17; tylko 3 wyplaty gracz -> zleceniodawca. SandBox
  (`SandBox.Issues`, 7 plikow): 11 wyplat z `null`. Czyli prawie kazda nagroda jest z niczego.
  Wzory: 250 + 1 000d, 500 + 1 500d, 500 + 3 000d, 750 + 3 000d, 800 + 4 200d, 2 000 + 4 000d (d = trudnosc), stale 3 000 (kryjowka, lowcy
  nagrod), 5 000 (rywal), 20 000 (podboj), 150 x 30-60 (bunt szlachty), dostawy: 500 + towar (`HeadmanNeedsToDeliverAHerd` 300 + 0.75 x Value x szt.,
  `LordNeedsHorses` 500 + 1.5 x szt. x wartosc, `VillageNeedsTools` 500 + szt. x cena).
- **Skala:** 5 000 zl = 21 L = roczny dochod rycerza (CENY 5 pkt 3). W dniach zoldu: druzyna 60 ludzi po 5 zl = 300 zl/dobe [S] - 5 000 = 17 dni.
  Gra sama definiuje prace: rozwiazanie zastepcze = N ludzi na D dni (`AlternativeSolutionBaseNeededMenCount`, `...DurationInDaysInternal`).
- **Regula (propozycja, jedna dla glownego i zastepczego rozwiazania):**
  `nagroda = min(nagroda gry, N x D x zold dzienny na czlowieka w druzynie gracza x 1.25) + wartosc towaru (zadania dostaw)`,
  zold na czlowieka = `MainParty.TotalWage / liczba ludzi` (fallback 5 zl); zadania bez rozwiazania zastepczego (rywal 5 000, podboj 20 000,
  zwiad, renta) - kwota gry, ale tylko z kiesy zleceniodawcy.
  Przyklady (d = 0.5, 5 zl/czl.): kryjowka 10 x 7 -> **438** (gra 3 000); zasadzka na karawane 37 x 6 -> **1 388** (2 500); dezerterzy 34 x 6 -> **1 275**
  (2 900); szkolenie zbrojnych 18 x 14 -> **1 575** (4 000); kupiec bez rynku 6 x 6 -> **225** (1 250); przemytnicy 20 x 12 -> **1 500** (2 250);
  bunt szlachty 43 x 13.5 -> **3 628** (4 500-9 000).
- **Platnik:** zleceniodawca (`IssueOwner` / `QuestGiver`: notabl, lord, herszt) - `GiveGoldAction` sam tnie kwote do jego kiesy
  (`GiveGoldAction.cs:13`), wiec wystarczy zamienic `null` na zleceniodawce. Komunikat po angielsku przy niepelnej zaplacie.
- **Latki (wzor `BookTranspiler` / `PeopleUnit.QuestSites`):** przy starcie: wszystkie typy `IssueBase`/`QuestBase` z zaladowanych zestawow (gra,
  SandBox, BK, ROT, NavalDLC); metody, ktorych IL wola `GiveGoldAction.ApplyBetweenCharacters` - transpiler: wywolanie -> nasz wrapper
  `QuestPay(giver, recipient, amount, show)`, prefiks metody zapamietuje `__instance` ([ThreadStatic]), finalizer czysci; wrapper: gdy
  `giver == null && recipient == MainHero` -> giver = zleceniodawca. Kwota: postfiks na getterach `RewardGold` nadpisanych w klasach zadan
  (oferta i kwota zapisana w zadaniu pokazuja juz nowa liczbe); zadania w toku z zapisu - tylko platnik. Linia dnia "Nagrody zadan (E8)": liczba,
  suma obiecana / zaplacona, ile z niczego (powinno byc 0).
- **Kolizje [K]:** `PeopleUnit.cs:320-340, :455-475` patchuje te same metody zakonczenia zadan wsi (prefiks/postfiks liczenia ludzi) - transpiler
  nie rusza ich prefiksow; `CirculationWindows` (okna obiegu) - dodac okno "nagrody zadan". **NK:** nie. **Koszt:** M.

### 5.2 Zlecenia kowalskie - rozdz. 1.2 (ta sama regula: platnik zleceniodawca, kwota w nowej monecie).

### 5.3 Turnieje

- **Stan [K]:** nagroda glowna turnieju AI to PRZEDMIOT z puli wartosci 1 600-5 000 starej monety (FixedSums38 - dzis zbroje t5-6, rumaki;
  ok. 1 200-3 000 zl wartosci historycznej) - **z niczego**; zloto TXP 700 + premie 100-150 (`TXPManager.cs:182`) - z niczego (SPIS-CEN-GRY zb58
  :256-270); zaklady gry - z niczego. AI-zwyciezca: cene nagrody placi u Jeffa `RBMTournament.GivePrizeToWinnerPrefix` (z niczego).
  Kwoty sa w skali historycznej (nagroda 5-12 L - kon albo klejnot XIV w.), problem to tylko platnik.
- **Zmiana:** przedmiot nagrody kupuje gospodarz (pan miasta, kiesa glowy rodu) po wartosci do kasy miasta przy wydaniu nagrody (okno
  `TournamentManager.GivePrizeToWinner` - juz wpiete do pomiaru w `CirculationWindows.cs:1823`); zloto TXP i wygrane zaklady - z kiesy gospodarza
  (przechwycenie `GiveGoldAction(null, ...)` w tych metodach jak w 5.1). Pule GrandTourney gracza - bez zmian (juz zamkniete, paczka 140).
  **NK:** nie. **Koszt:** S-M (TXP i RBM - cudze mody, sprawdzic IL przy wpinaniu).

### 5.4 Okupy i jency

- Okupy lordow - ZROBIONE (`Ransom178`: wedlug majatku, wszystkie drogi, rodzina albo skarbiec platnikiem - `Ransom178.cs:33, :355`); sredni
  okup AI-AI 40 104 zl = 167 L = okup barona (CENY 2.5) - w skali historycznej.
- Zostaje: sprzedaz ZWYKLYCH jencow posrednikowi - `SellPrisonersAction.cs:83` `GiveGoldAction(null, sprzedajacy, cena)` (z niczego); cena =
  koszt rekruta x 0.25 + podloga RC (`FairRansom.cs:28-33`) - juz w pensach (koszt rekruta = dni zoldu, `HistoricalRecruitCost`). Okno
  "jency 13/0" (log l.36056) tylko liczy. **Zmiana:** platnik = kasa miasta (posrednik kupuje na rachunek miasta; w Essos targ niewolnikow),
  przy pustej kasie - mniej. Klucz `PrisonerSaleTownPays`. **NK:** nie. **Koszt:** S.

### 5.5 Stale kwoty tylko gracza (resztka kroku 3.9)

| Kwota | Kod | Dzis | Propozycja (dni placy) | Odbiorca zamiast nicosci |
|---|---|---|---|---|
| Lapowka strazy (loch, sala lorda): wojna z rodem / z panem / neutralny | `DefaultBribeCalculationModel.cs:50-62`; `BribeGuardsAction.cs:17-18` (gracz -> null) | 5 000 / 3 000 / 100 | 30 / 18 / 3 dni placy straznika (2 zl - RC straz) = **60 / 36 / 6** (kilka szylingow) | sakiewka zalogi osady (MenPurse), bez zalogi - kasa osady |
| Golibroda | `BarberCampaignBehavior.cs:145` | 100 | **3** (dzien robotnika) | kiesa ludu miasta (TownFolk) |
| Karczmarz - wiesci o rodzie pana | `TavernEmployeesCampaignBehavior.cs:482` | 500 | **12** (szyling) | kiesa ludu miasta |
| Kolejka dla druzyny | `TavernEmployeesCampaignBehavior.cs:913, :926-929` | 50 + 0.2 x ludzi | **0.3 x ludzi** (litr piwa po 0.3 zl; 100 ludzi = 30) | kasa miasta / kiesa ludu |
| Napitek | `TavernEmployeesCampaignBehavior.cs:623` | 2 | bez zmian | kiesa ludu |

Latki: postfiks na `GetBaseBribeValue` (prywatna) albo `GetBribeToEnterLordsHall/Dungeon`; transpiler stalych 100 / 500 w dwoch metodach SandBox
(wzor `BookTranspiler`); przechwycenie odbiorcy `null` jak 5.1. Klucz `PlayerFeesHistorical`. **NK:** nie. **Koszt:** S.

---

## 6. Proponowane paczki etapu 8 (obszar B)

| Paczka | Zakres | Pliki | Klucze (true) | Koszt | Zaleznosci |
|---|---|---|---|---|---|
| **E8/B1 cennik** | 2.4 rzad konski (+ Recipes/Mend), 2.5 helmy, 2.6 luki, (2.1 jedna linia), opcja 2.7 konie | `ArmsPricing.cs`, `HistoricalPrices.cs`, `Recipes.cs`, `MendMaterial.cs` | `HarnessKgPerArmor` 0.4, `HelmDaysPerKg` 1.0, tabela DaysBow, `HistHorsePrices` (Jeff) | S-M | - |
| **E8/B2 rynek** | 2.2 popyt wojenny, 2.3 kon przy sprzedazy | `Settings.cs`, `SupplyDemand.cs` | `WarExpectationEnabled` false, `HorseSellFromWorth` | S | - |
| **E8/B3 kuznia** | 1.1 ksiazki, 1.2 zlecenia kowalskie, 1.3 opis MCM | `HistoricalPrices.cs`, nowy `CraftOrders8.cs`, `Settings.cs` | `BookPriceFix`, `CraftOrdersHistorical`, `CraftOrdersOpenedOnly` | M | - |
| **E8/B4 dochody gracza** | 5.1 nagrody zadan, 5.3 turnieje, 5.4 jency, 5.5 stale kwoty | nowy `QuestPay8.cs`, `PlayerFees8.cs`; GrandTourney bez zmian | `QuestRewardFromGiver`, `QuestRewardInWageDays`, `TourneyPrizeFromHost`, `PrisonerSaleTownPays`, `PlayerFeesHistorical` | M | B3 (wspolny wrapper wyplat) |
| **E8/B5 drobne** | 3.1 wyroby uboczne, 3.2 pekniecie zbroi, zgloszenia lawy 2, 3, 6, 14, 15; RC 7, 8, 10 | `ArtisanInputs.cs`, `ArmourQuality.cs`, `MendMaterial.cs`, `SmithMenu.cs`, `AiWear.cs`, RC `Work.cs` (7, 8, 10) | `ArtisanByproductsFull`, `ArmourBotchHalfBack` | M | - |
| **E8/B6 BEE** | 4: klucz 14 w skrypcie (bez DLL) | `tools/bee/*.ps1`, `tools/bee/OPIS.md` | - | S | uruchomienie przy wgraniu koncowym |

Test (autotest 120 dob + zapis): linie startowe "HistoricalPrices: ... ksiazek BK przeliczonych 24", "uprzaz z wagi prawdziwej N szt.", "helmy t4-6 N",
"luki N"; dzien: "Rynek broni ... premie wojenne" znika; "ZakupyAI wedlug kupujacego" - helm i "konie i rzedy" taniej; "Nagrody zadan (E8)" i okna
obiegu "nagrody zadan / zlecenia kowalskie / turniej / jency" z trafieniami, "z niczego 0". Gracz w autotescie nie robi zadan ani zlecen -
5.1-5.5 i 1.2 wymagaja recznego sprawdzenia Jeffa (8.16) albo sztucznej proby w autotescie (wywolanie `CompleteOrder` / zakonczenie zadania).

## 7. Kontrola calosci (zasada 0)

- **Jedna zasada wyceny:** 2.4-2.6 zmieniaja `ArmsPricing.Compute` - ta sama liczba idzie do Value, warsztatu (`WorkshopLaw.Needs`), naprawy
  (`MendMaterial.Labor`); kwit kuzni gracza (`Recipes`) i material naprawy uprzezy (`UnitKg`) licza z wagi gry - poprawic razem (2.4).
- **Produkcja:** nizsze dni helmow i lukow = 2.4-4.8x wiecej sztuk na roboczodzien warsztatow - obserwowac polki i ceny (mozliwy przesyt lukow).
- **Zamknieta ekonomia:** 1.1, 1.2, 5.1, 5.3-5.5 usuwaja zloto z niczego / w nicosc; 2.2-2.7 to tylko ceny. Spadek dochodu gracza jest celem
  decyzji 8.18 ("bez doplat" - nic nie dodajemy w zamian).
- **Cudzy kod:** BK (`BKEducationBehavior`), gra (`CraftingOrder`, `CraftingCampaignBehavior`, zadania, `DefaultBribeCalculationModel`,
  `SellPrisonersAction`), SandBox (golibroda, karczma), TXP/RBM (turniej) - przy wpinaniu sprawdzic IL w grze 1.4.8; kazda latka z linia
  "wpiete N/M". Kolizja z `PeopleUnit.QuestSites` (te same metody zadan wsi) i `CirculationWindows` (okna na tych samych metodach).
- **Stary zapis:** wszystko dziala od wczytania (Value przy starcie sesji; zlecenia przeliczane przy wczytaniu; zadania w toku - zmiana platnika).

## 8. Pytania do Jeffa (kierunek, z rada)

1. Konie wedlug kategorii (2.7): t3 taniej x0.4, rumaki t4 drozej x1.5 - **rada TAK** (ceny historyczne, bilans zakupow lordow ok. 0).
2. Nagroda zadania = praca druzyny w dniach zoldu (5.1, np. kryjowka 3 000 -> ok. 440, szkolenie zbrojnych 4 000 -> ok. 1 600), placona przez
   zleceniodawce do wysokosci jego kiesy - **rada TAK** (zgodne z decyzja 12.10).
3. Welna z dodatkowych cykli uboju (3.1): pelna sztuka jak w recepturze gry czy fizyka 0.15 szt. z owcy - **rada fizyka (0.15)**.
4. "Sol i piwo jako potrzeby" w BEE - **rada ODRZUCIC** (w BEE to lista w kodzie bez skutku przy BK; u nas juz sa w zakupach wsi).
