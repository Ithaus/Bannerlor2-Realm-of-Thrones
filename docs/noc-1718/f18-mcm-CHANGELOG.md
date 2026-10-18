## 2026-10-18 (paczka f18-mcm - Jeff: "jak wlaczylem drogi gra wyszla do pulpitu")

**Mod:** Armoury, RealisticCaptivity, GrandTourney (`tools/gen_mcm.py` i wygenerowane `src/McmSettings.cs`; CrashScribe i ForgeView nie maja wlasnych ustawien MCM -
CrashScribe czyta pola Armoury refleksja). Poprawka po recenzji: takze `Armoury/src/SlowMuster.cs`, `Armoury/src/BkArmourList.cs` i opis `FoodModelChain`
w `Armoury/src/Settings.cs`.
**Problem:** po zmianie dowolnego ustawienia naszych modow w Mod Options i kliknieciu Done gra pytala "Game Needs to Restart" i na "Yes" wychodzila do pulpitu
(Jeff wlaczyl drogi - `MapSideRoads` / `MapRoadSpeedBonus` - i gra sie zamknela). "Cancel" zostawial w menu bez zapisu zmian.
**Przyczyna:** MCM v5 (zdekompilowane: `MCMv5.dll` `BaseSettingPropertyAttribute(..., bool requireRestart = true, ...)`; `Bannerlord.MBOptionScreen.v1.4.7.dll` -
wersja ladowana przy grze v1.4.8 - `ModOptionsVM.ExecuteDoneInternal`: gdy jakiekolwiek zmienione pole ma `RequireRestart`, inquiry "Game Needs to Restart" i na "Yes"
`SaveSettings` + `Utilities.QuitGame()`). Generator `tools/gen_mcm.py` nigdy nie ustawial `RequireRestart`, wiec wszystkie 2128 pol (Armoury 1976, RC 112, GT 40) mialy domyslne true.
**Zmiana:** `gen_mcm.py` dopisuje `RequireRestart = false` do kazdego pola, poza tabela `RESTART_KEYS` (z komentarzem, dlaczego). Pola wchodzace dopiero po wczytaniu zapisu dostaja
w opisie MCM dopisek " (takes effect after loading a save)", o ile opis juz o tym nie mowi (`ALREADY_SAID`: next load / is loaded / at session start / new campaign only / restart...):
tabela `SESSION_KEYS` (Armoury) i wszystkie pola modulow z `APPLY_ON_LOAD_ONLY` (RC, GT). Literowka w tabelach zatrzymuje generator. Wynik: Armoury 1975 false + 1 true,
dopisek 15 (opis juz mowil: 24); RC 112 false, dopisek 112; GT 40 false, dopisek 40. Kod modow (poza wygenerowanym `McmSettings.cs`) bez zmian.
Po poprawce po recenzji (nizej): Armoury 1975 false + 1 true, dopisek "after loading a save" 17, dopisek cen 18, dopisek "read once at game start" 1, opis juz mowil 25;
RC i GT bez zmian.

**Jak MCM wnosi wartosc bez restartu (dekompilacja):** suwak/przelacznik od razu ustawia wlasciwosc obiektu z `BaseSettingsProvider.GetSettings(id)` (`SetValueTypeAction.DoAction`
-> `IRef.Value`); `AttributeGlobalSettings<T>.Instance` zwraca ten sam obiekt. Done bez pol z `RequireRestart` = `SaveSettings` (zapis `Armoury.json`, zdarzenie SAVE_TRIGGERED)
i zamkniecie ekranu - bez pytania. Cancel = `UndoAll`. Plik json trzyma tylko pary Id -> wartosc (`BaseSettingsJsonConverter.WriteJson`) - zmiana atrybutu nie rusza zapisanych wartosci
(`Armoury.json` Jeffa ma juz wszystkie klucze).
**Kiedy wartosc wchodzi do `Settings.Current`:**
- Armoury: `McmSettings.Apply` w `SubModuleMain.OnGameStart`, w `ArmouryBehavior.OnSessionLaunched` i CO GODZINE GRY (sluchacz HourlyTick "McmSettings.Apply+AmmoTracer") -
  odczyty w trakcie gry widza zmiane najpozniej po godzinie gry (na pauzie / w misji - po powrocie czasu na mape);
- RealisticCaptivity, GrandTourney: `McmSettings.Apply` TYLKO w `OnGameStart` - kazda zmiana po wczytaniu zapisu (stad dopisek przy wszystkich polach; gdyby te moduly dostaly
  Apply co godzine jak Armoury - zdjac je z `APPLY_ON_LOAD_ONLY`);
- odczyty przy starcie procesu (`OnSubModuleLoad`, `OnBeforeInitialModuleScreenSetAsRoot`, `ApplyAll`) widza `Settings.Current` z `ModuleData\Armoury.settings.xml` (albo domyslne z kodu),
  NIE z MCM - restart gry nie wnosi tam wartosci z MCM.

**Audyt (analiza IL wszystkich pieciu DLL, nie na oko):** zrzut IL (ICSharpCode.Decompiler `ReflectionDisassembler`), dla kazdej metody: odczyty pol `Settings` (ldfld/ldflda),
odczyty `McmSettings.get_*`, napisy z nazwami kluczy (CrashScribe `Mends.ArmouryFloat("...")`, RC/GT `TownWageLink`), wywolania i delegaty (ldftn + zdarzenie `CampaignEvents.get_*Event`).
Korzenie: PROC = `SubModuleMain.OnSubModuleLoad / OnBeforeInitialModuleScreenSetAsRoot`; SESS = `OnGameStart`, `RegisterEvents`, sluchacze OnSessionLaunched / OnNewGameCreated /
OnGameLoaded; RUN = reszta (latki Harmony, sluchacze tickow, menu, metody wirtualne, delegaty bez znanego zdarzenia - zachowawczo). Wynik (Armoury 1976 kluczy): RUN 1933,
SESS-tylko 36, PROC-tylko 2, nieczytane 1, napisy w tablicy statycznej CrashScribe 4. Klucz czytany wprost z pliku MCM przy starcie: tylko `MapSpeedButtons` (jedyne miejsce z
"Armoury.json" w kodzie). Latki zakladane po starcie procesu (EnsureHooks/Wire/SessionStart, ~70 miejsc) zakladane sa bez klucza, klucze czytane w latkach na zywo
(wyjatki sprawdzone recznie: `BanditLimit` - stopery `BanditCostLog` i wybor kryjowki przy starcie sesji, opis juz mowi; `MapRoads` - plik drog wczytywany leniwie w `Daily`, gdy klucz
wlaczony w trakcie sesji).
- **RESTART_KEYS (zostaja RequireRestart = true): Armoury `MapSpeedButtons`** - `MapSpeedUi.Register` (OnSubModuleLoad) czyta wpis z pliku `Armoury.json` i od niego zalezy
  rejestracja UIExtenderEx (wstawka MapBar + mixin), raz na proces; opis juz mowi o restarcie. Przy module Better Time (f17) rejestracja i tak nie zachodzi. RC, GT: brak.
- **SESSION_KEYS (Armoury, wszystkie odczyty przy starcie sesji):** LootPriceFollowsCondition, RichQualityModifiers, TroopSkillAutoFit, CharcoalValue, CrudeIronValue,
  WroughtIronValue, IronValue, SteelValue, FineSteelValue, CastleSteelValue, StartStockInLoads, ColdStartMarketDays, ColdStartTierUp, ColdStartLegacyHands, HistCharcoalPerKg,
  HistBulkUnitFactor, HistTradeGoods, HistLivestockPrices, HistHidesPerKg, HistFlaxPerKg, HistHorsePrices, XbowPriceVsBow, HistDemandFromDefinition, HistMixedCategoryShelf,
  BulkUnitPerCampaign, BkWorkshopInputsFix, VillageClimateFix, FlaxVillagesWithoutClimateFix, VineOliveByClimate, BloomeryAtMine, WorkshopRecipesByValue, SlaughterKeepsHides,
  MidDayLoadKeepsDay, CombatAiRealistic, CharcoalWeightKg, Army175IronbornFoot / NorthFoot / VolantisNorvos / MinorLore. Dopisek dostalo 15 (wartosci sztab x7, LootPriceFollowsCondition,
  RichQualityModifiers, HistCharcoalPerKg, HistTradeGoods, HistHidesPerKg, HistFlaxPerKg, HistHorsePrices, XbowPriceVsBow), reszta juz mowila (on load / next load / new campaign only).

**Poprawka po recenzji (4 uwagi, kazda sprawdzona w kodzie):**
1. **`VolunteerRegenPercent` - PRAWDA, naprawione w kodzie.** `SlowMuster.ApplyAll` (OnBeforeInitialModuleScreenSetAsRoot) wychodzil przy `>= 100` przed zalozeniem latki;
   domyslne 100 (`Settings.cs:186`), `Armoury.settings.xml` w repo tego klucza nie ma - latka nie wchodzila nigdy i suwak MCM nic nie zmienial (ani po restarcie). Zmiana: warunek
   zdjety, latka zakladana zawsze (gdy jest model ochotnikow); `SlowPostfix` czytal klucz na zywo juz wczesniej i przy `>= 100` wychodzi bez zmiany wyniku. Linia startu teraz:
   "SlowMuster: latka wpieta (model: BannerKings), klucz VolunteerRegenPercent na zywo - przy starcie 100% (vanilla tempo, postfiks nic nie zmienia)." (dotad "100% - vanilla tempo,
   patch spi"). Sprawdzone: `BKVolunteerModel` deklaruje jedna `GetDailyVolunteerProductionProbability(Hero, int, Settlement)` (dekompilacja BannerKings.dll z libs) - `AccessTools.Method`
   bez typow jej nie myli; na tej metodzie siedzi juz postfiks `Levy.ProbabilityPostfix` (wynik najwyzej 1), SlowMuster mnozy przez pct/100 <= 1 - kolejnosc latek jak w czasach,
   gdy SlowMuster dzialal (25-50%). Praca: jeden odczyt pola i powrot na wywolanie (Levy liczy 5-20 tys. losowan na dobe) - bez znaczenia. Przy 100 (Jeff: 100 w `Armoury.json`)
   wynik gry identyczny. Uwaga: obnizenie suwaka przywraca podwojny hamulec H4 (SlowMuster x Levy, `docs/AUDYT-PELNY-WERBUNEK.md`) - to wybor gracza, nie domyslne.
2. **`TidyBannerKingsArmourList` i `BattleStaminaEnabled` - PRAWDA.**
   - `TidyBannerKingsArmourList`: naprawione w kodzie - warunek w `BkArmourList.ApplyAll` zdjety (latki getterow `ItemTypeText` / `ItemName` zakladane, gdy jest panel BK),
     oba postfiksy czytaja klucz na zywo (`On`); wylaczony = podpisy Banner Kings. ForgeView (`ArmourListColour`) celuje w wiazanie `@ItemTypeText` w prefabie, nie w tekst - bez kolizji.
   - `BattleStaminaEnabled`: NIE zmieniane w kodzie - odczyt na zywo wymagalby zmian w `BattleWind` (Stamina/PostureInit) i w `FieldCraft.cs:572` naraz (`!BattleWind.Active` wlacza stary
     liniowy bonus Endurance - inaczej bohater bez bonusu albo z podwojnym). Zamiast tego nowa tabela `STARTUP_KEYS` w `gen_mcm.py`: dopisek " (read once at game start from
     ModuleData/Armoury.settings.xml - changing it here has no effect)". RequireRestart = true nic by nie dal (start procesu nie czyta MCM).
3. **SESSION_KEYS niepelne - PRAWDA, z poprawka zakresu.** `DesertionLawEnabled` (`TierDesertionModel.Install` w OnGameStart po `McmSettings.Apply`, model klucza juz nie czyta) i
   `BkProsperityLayerOnce` (`LayerOnce.Sync` tylko z `A16Signals.OnSessionLaunched`) - dopisane do `SESSION_KEYS`. 18 kluczy cen (`HistIronOrePerKg`, `HistWoodPerKg`, `HistLeatherPerKg`,
   `HistLinenPerKg`, `HistCrudeIronPerKg` ... `HistCastleSteelPerKg`, `HistSpecialFactor`, `HistMasterWageT1`, `HistMasterWagePerTier`, `HistProfitPercent`, `HistAmmoLaborMultiplier`,
   `HistBowLaborMultiplier`, `HistThrownOwnDays`, `HistUniquePrestige`): cena przedmiotow istniejacych rzeczywiscie tylko z `HistoricalPrices.Apply` (blokada `_target`), ALE te klucze czytaja
   tez miejsca na zywo - dniowka `WageFor` (TownWage, Qohor, zlecenia `SmithMenu`), dni roboty `HistDays` (ForgeClock, WorkshopLaw), robota naprawy `MakingLabor` (MendMaterial),
   cena surowca warsztatu `WorkshopLaw.MatPrice`, wycena nowo wykutych `PriceOne` / `PriceDesign`. Ogolny dopisek "takes effect after loading a save" bylby nieprawda (np. dniowka
   wchodzi po godzinie gry), wiec osobna tabela `PRICE_KEYS` z dopiskiem " (prices of items already in the world are recalculated after loading a save)".
4. **`FoodModelChain` - PRAWDA, naprawione w opisie.** `FoodChain.Build` czyta klucz raz na kampanie (`_st` wedlug `Campaign.Current`), latki przy starcie procesu z XML / domyslnego TAK -
   restart nic nie zmienia. W `Settings.cs:168` "Restart the game after changing it" -> "Takes effect after loading a save"; klucz dopisany do `SESSION_KEYS` (opis juz mowi - bez
   podwojnego dopisku).
Generator: klucz w dwoch tabelach (RESTART / SESSION / PRICE / STARTUP) albo nieobecny w `Settings.cs` zatrzymuje skrypt. Diff `McmSettings.cs` (Armoury) = 22 linie atrybutow,
w kazdej tylko `HintText` (sprawdzone skryptem: po wycieciu HintText pary linii identyczne); RC i GT bez zmian.

**Ryzyko / co sprawdzic:**
- regresje: zmienia sie tylko atrybut i tekst opisu; nazwy, Id, grupy, zakresy i wartosci domyslne bez zmian (diff `McmSettings.cs` = tylko te dwie rzeczy w kazdej linii atrybutu).
  `McmSettings.Apply` / `ApplyTo` bez zmian. Generator na starym kodzie dawal plik identyczny z zapisanym (sprawdzone przed zmiana).
- kolizje: nic nie liczy drugi raz - restart dotad tylko zamykal gre; po zmianie te same wartosci wchodza ta sama droga (Apply), tylko bez wyjscia z gry.
- w grze: zmienic w MCM np. `Map Side Roads` / `Map Road Speed Bonus` i Done - okno sie zamyka, gra zostaje; po godzinie gry linia w Armoury.log ("Drogi boczne (f11)" w dobie /
  "Drogi (DR)"); `Map Speed Buttons` dalej pyta o restart (zamierzone). W RC/GT zmiana dziala po wczytaniu zapisu.
- poprawka po recenzji: `VolunteerRegenPercent` w MCM (np. 50) - po godzinie gry ochotnicy wolniej (linia startu "SlowMuster: latka wpieta ..." zamiast "patch spi");
  przy 100 bez zmian. `Tidy Banner Kings Armour List` wylaczony - po godzinie gry panel zbroi BK z podpisami BK (bez "[IV]" i kategorii); wlaczony z powrotem - nasze podpisy.
  Linia startu "BkArmourList: uporzadkowano ItemTypeText / ItemName" jak dotad. Opisy w MCM: `Battle Stamina Enabled`, ceny `Hist ...`, `Desertion Law Enabled`,
  `Bk Prosperity Layer Once`, `Food Model Chain`.
- znalezione przy audycie, NIE naprawiane (osobna decyzja): `BattleStaminaEnabled` (teraz z uczciwym dopiskiem) jest czytany tylko w `ApplyAll` przy starcie gry z
  `Armoury.settings.xml` - przelacznik w MCM nic nie zmienia (ani po restarcie); `NoFreeKingdomGold` (znalezione przy poprawce) - `KingdomTreasury.Transpiler` czyta klucz raz,
  przy zakladaniu latki na starcie procesu (z XML), a opis mowi "change needs a game restart" - restart nie wnosi wartosci z MCM (ta sama klasa co BattleStaminaEnabled; bez zmian -
  nie bylo w uwagach recenzji); to samo dotyczy czesci startowej ok. 280 kluczy czytanych tez w `ApplyAll` (decyzja o latce
  z pliku XML, dzialanie na zywo z MCM - zwykle bez skutku, bo domyslne TAK). `WorkshopWorkersArtisans` nie jest czytany nigdzie; `CharcoalWeight` tylko w linii logu
  (opis mowi "no longer read"). Pamieci podrecznej w trakcie gry (np. receptury `Recipes.For`) analiza IL nie rozpoznaje - takie klucze moga wchodzic dopiero po wczytaniu.
**Status:** DO SPRAWDZENIA (generator przebiegl, build Armoury / RealisticCaptivity / GrandTourney / CrashScribe / ForgeView kod 0; w Armoury.dll 1975 x RequireRestart = false
i Map Speed Buttons = true - sprawdzone w dekompilacji zbudowanego DLL). Poprawka po recenzji: generator przebiegl, build wszystkich pieciu kod 0; w zbudowanym Armoury.dll
(dekompilacja) `SlowMuster.ApplyAll` bez warunku 100, `BkArmourList` z odczytem klucza w obu postfiksach, McmSettings dalej 1 x RequireRestart = true.
