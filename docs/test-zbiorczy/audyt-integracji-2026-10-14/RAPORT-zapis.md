# Audyt "zapis" - interakcje paczek nocy 13/14.10 (4d33042..43af413) na zapisie, wczytaniu, starym zapisie i nowej kampanii

Drzewo: `zb/int` (w-toku/zbior-e8, HEAD 43af413). Kod NIE zmieniany, nic nie commitowane, gra nie uruchamiana.
Zrodla: `git diff 4d33042..HEAD`, logi `Armoury-2026-10-13_20-41-38.log` (120 dob, kod 7a22817 = wszystko poza L5b),
`Armoury-2026-10-13_19-21-12.log` (bieg roczny GL A+B, N7, M6a, W7), `session-2026-10-13_19-21-10.log` (harness autotestu),
dekompilacja (ilspycmd) celow latek: `dec/*.cs` w tym katalogu.

## Znaleziska (od najwazniejszego)

### 1. [WAZNE] Sciezka WCZYTANIA zadnego nowego pola nie biegla w grze - lancuch po wczytaniu istnieje tylko miedzy paczkami
- **Gdzie (strona zapisu):** `ArmouryBehavior.cs:468-479` (arm_peoplescale, arm_villagepeople, arm_peoplemarket, arm_goodsrates), `:641-656` (arm_m6a),
  `:701-706` (arm_n7_rally, arm_sack, arm_ruins), `TownFolk.Live.cs:1326-1327/1376` (arm_townfolk pole 18 HorseOwed).
- **Gdzie (strona testu):** `session-2026-10-13_19-21-10.log:225` "AUTOZAPIS: autozapis gry WYLACZONY"; `:3270-3271` "ZAPIS autotest-dlugi" (co 60 dob - wiec
  **Export** kluczy GL/N7/M6a biegl bez wyjatku w biegu rocznym); oba logi Armoury maja po jednym naglowku sesji, 0 linii "(z zapisu", 0 linii "wczytano".
- **Scenariusz:** Jeff zaczyna nowa kampanie, gra, zapisuje, nastepnego dnia wczytuje. Po wczytaniu biegnie lancuch, ktorego zadna recenzja pojedynczej paczki
  nie mogla uruchomic w calosci: SyncData Import (8 kluczy + pole 18) -> OnSessionLaunched `VillageClimate.Apply` (1329) -> `GoodsRates.Apply` (1330, ponowna
  podmiana 16 wsi + wyrownanie klasy BK po T8) -> `VillageWoodlot.Calibrate` (1333) -> `PeopleScale.SessionStart` (1365, odniesienia z zapisu) ->
  `VillageByPeople.SessionStart` (1366, porownanie `refV` z zapisu GL0 z `_calRefV` z zapisu GL1 - `VillageByPeople.cs:239`, przejscie `_wasOn` ->
  `HarvestValve.RestartMeasurement` - `:246-251`) -> doba: `PeopleMarket.StartGold` (flaga `_start` z zapisu), `StragglersRally.Resolve` (`:852-874`),
  `CitySack.Ensure/EnsureRuins`, `TownFolk.EnsureImported` (pole 18).
- **Skutek:** statycznie lancuch przechodzi (patrz "Sprawdzone"), ale oczekiwania z CHANGELOG ("Po zapisie i wczytaniu: bez drugiej linii kalibracja, bez POMIAR OD NOWA",
  "klasa BK juz zgodna 16", "N7: zgubieni przy wczytaniu 0") nie sa niczym potwierdzone. Blad w ktoryms Import = po cichu "kalibracja od nowa" (inne g P1,
  inne mianowniki glow kiesy ludu, regulator 221 od nowa) albo pusta pula N7 / pusty stan lupu - tylko linia w logu, bez wyjatku.
- **Naprawa:** jeden bieg autotestu "zapis-wczytanie" przed wgraniem: nowa kampania -> doba 10 (po zlocie startowym, z grupami N7 i para M6a-3 w toku) zapis
  "autotest-zw" -> wczytanie w tym samym procesie -> 3 doby -> wyjscie, nowy proces, wczytanie -> 3 doby. Progi: (a) "Skale ludzi (GL0): start sesji ... (z zapisu"
  i brak "Skale ludzi (GL0): kalibracja"; (b) brak "Plon wsi od ludzi (GL1): kalibracja" i brak "pomiar od nowa" regulatora 221; (c) "Stawki towarow (GL5): start sesji
  ... podmienione teraz 16 ... klasa wsi BK ... juz zgodna 16"; (d) brak drugiej linii "Zloto startowe wedlug ludzi (GL2): dzien"; (e) N7 "czekali wczoraj" = W sprzed
  zapisu, zgubieni przy wczytaniu 0; M6a "wczytano wpisow N (bledne 0)", "wczytano par N"; (f) "Lup miast (L0 po nastepstwach)" te same osady z d+ ciaglym, LB
  "zgliszcza: osad/budowli/poziomow" rowne przed i po; (g) "Pieniadz swiata" i kiesy ludu przed zapisem = po wczytaniu (ta sama doba). Dopisac grupe `zapis` do
  `tools/sprawdz_logi.py`.

### 2. [DROBNE] Okna doby w pamieci sesji czytane przez ROZLOZONY ticker osad gry - wczytanie w trakcie doby gubi to, co blok doby juz zaplacil
- **Mechanizm gry (dekompilacja):** `CampaignPeriodicEventManager.PeriodicDailyTick` -> `_dailyTickSettlementTicker.PeriodicTickSome` (osady tykaja rowno przez cala dobe,
  `Index`/`TickDebt` w zapisie gry) -> `BuildingsCampaignBehavior.DailyTickSettlement` -> `TickCurrentBuildingForTown`: `building.BuildingProgress += town.Construction`
  (`dec/bcb.cs:100-118`); lojalnosc - `Town.DailyTick` `Loyalty += LoyaltyChange` (`dec/town.cs:596-598`) w tym samym tickerze.
- **(a) P2 lud buduje miasto:** `BuildFunding.Folk.cs:343` `_folk[t]/_folkB[t]` ustawiane w bloku doby (`ArmouryBehavior.cs:1690`, po zaplacie materialu i plac z kies ludu),
  czytane w `BuildFunding.cs:62-79` PowerPostfix przy ticku osady. Po wczytaniu o godzinie h slowniki sa puste -> osady bez ticku tej doby (ok. (24-h)/24 swiata) dostaja
  0 pkt ludu; tak samo 0 pkt pana (`_funded`, `BuildFunding.cs:222` - stara sprawa, P2 ja podwaja). Lud juz zaplacil (doba 108956 testu: 7 988 zl, 299 pkt, 12 czol kolejki).
  Zloto nie ginie (poszlo do kas targu i kies W/R), przepadaja punkty. Jeff wczytuje przy kazdym uruchomieniu gry - kumuluje sie (ok. pol doby budow swiata na wczytanie).
- **(b) W7:** `KingdomTreasury.LordLevy.cs:204` `_lvCash` -> `LordLevyRefundCut` `:398-410` (warunek `_lvDay == dzis-1`) wolane z `KingdomTreasury.cs:250` (WageRefund).
  Po wczytaniu `_lvDay = MinValue` -> zwrot 70% nastepnej doby liczy tez zold oplacony W7 (tak samo W5 `_naCash`, `KingdomTreasury.NeedAid.cs:256/366` - stare). Skala ~1 tys. zl/d.
- **(c) 11c:** `TownFolk.Skup.cs:50` `f.SkupWin += paid` przez cala dobe, zerowane w `TownFolk.Live.cs:807`; po wczytaniu robocizna od skupu z czesci doby przed zapisem
  nie jest placona (zostaje w kasie kupcow; robocizna 11c ~21 tys. zl/d w dobie 108956).
- **(d) P1b:** `FolkAidLoyalty.cs:50/220` `_snap` - kara reszty doby przepada (udokumentowane jako przyjete).
- **Naprawa:** (a) maly klucz `arm_buildday` = `"1|doba|idMiasta=pktPana,pktLudu,idTypuBudowli;..."` z `_funded/_folk/_folkB`, odtwarzany tylko gdy doba z zapisu == dzis
  (budowla rozwiazywana po typie w osadzie przy pierwszym PowerPostfix); prosciej - punkty czola kolejki dopisywac wprost do `BuildingProgress` w bloku doby (jak sciezka
  "poza czolem" P2) i zostawic w PowerPostfix tylko boost. (b) dopisac `_lvDay/_lvCash` (i `_naDay/_naCash`) jako kolejne pole `arm_crown165` (wzor `WarTaxExport`).
  (c), (d) - przyjac, dopisac do "Ryzyko" w CHANGELOG ("pierwsza doba po wczytaniu").

### 3. [DROBNE] Sarnor (GL-1/P6, "tylko nowa kampania") liczony ZANIM MCM nalozy ustawienia
- **Gdzie:** `PopulationLaw.cs:81-85` `TablePop` czyta `Settings.Current.SarnorPeopleRaised` w `Calibrate()` (`:102`), a ta biegnie leniwie z `PeopleOf` (`:239`) /
  `EnsureCalibrated` (`Devastation.cs:166`, `Famine.cs:547`, `OutlawLaw.cs:500`) w tickach startowych OnNewGameCreated - w logu 20-41-38 linia 216 o 20:42:35, zaraz po
  `WinterTrial.OnNewGame`, PRZED `McmSettings.Apply()` (`ArmouryBehavior.cs:1324`, OnSessionLaunched 20:42:42). `Settings.Current` w tej chwili = `Armoury.settings.xml`
  (`Settings.Load`, `SubModuleMain.cs:26`) + domyslne pola (nowego klucza nie ma w xml ani w Armoury.json).
- **Scenariusz:** Jeff wylacza "Sarnor People Raised" w MCM, uruchamia gre, nowa kampania -> `_k` Sarnoru z 0.25 mln (domyslne true); godzine pozniej McmSettings.Apply ->
  false -> `TableTotal` (`:220`) i `_kTab` (`:202`) z 0.1 mln: linia "Rece (174)" i wzor tabeli (przy wylaczonym TownPeopleFrozen) niezgodne z `_k` z zapisu.
  Przy domyslnym "tak" bez skutku (log: "Sarnor (GL-1 P6, Sarnor People Raised): tak").
- **Naprawa:** w `PopulationLaw.Calibrate()` przy `_k.Count == 0` raz zawolac `McmSettings.Apply()` przed odczytem ustawien (to samo naprawia stare PopulationScale /
  PopulationMaxPerHearth w tym miejscu).

### 4. [DROBNE] Wylaczenie T8 (VillageClimateFix) w kampanii zaczetej z GL5 cofa 5 wsi lnianych, a g plonu P1 zostaje z kalibracji z lnem
- **Gdzie:** `GoodsRates.cs:60,62-65` - Berrybush, Lanthas, Sagora, Shirosi, Port Yhos maja w tabeli typ "silk_plant", ktory jest WYNIKIEM T8 (`VillageClimate.cs:72-76,84`:
  cattle_farm / sheep_farm / lumberjack -> silk_plant); `GoodsRates.cs:168` `if (curId != row[1]) other` (nie rusza); `VillageClimate.cs:119-131` (T8 wylaczone: typ z mapy,
  klasa BK cofnieta); `VillageByPeople.cs:236-242` - g liczone od nowa tylko przy innym `refV` albo innym kluczu Strazy.
- **Scenariusz:** kampania zaczeta z GL5 i T8 (g lnu z 36 wsi), Jeff wylacza "Village Climate Fix", wczytuje: 5 wsi wraca do bydla/owiec/drwali na stale, g lnu i tych typow
  bez zmian, regulator 221 bez "pomiaru od nowa". Len swiata ok. -14% (5 z 36), drwale +3 wsie (drewno i tak na suficie regulatora). Obietnica GL5 "wsie lniane na stale,
  klucz decyduje tylko o soli" lamie sie przez INNY klucz. (Cider Hall - typ mapy flax_plant - zostaje lnem.)
- **Naprawa:** w tabeli GL5 druga kolumna "typ z mapy" i podmiana przy `_flax == 1` z typu T8 ALBO z typu mapy; albo `arm_villagepeople` trzyma podpis zbioru typow pod regula
  (liczba wsi na typ) i przy innym podpisie na starcie sesji - g od nowa + `HarvestValve.RestartMeasurement`.

## Sprawdzone - bez zarzutu (z dowodem)

**Klucze i format.** 77 kluczy w `ArmouryBehavior.SyncData`, 0 duplikatow (jedyny "dubel" arm_garrisonarmory to komentarz :605), zaden klucz nie koliduje z sufiksem
`_parts` (SaveText). Nowe klucze maja wersje: "1" (peoplescale, villagepeople, peoplemarket, goodsrates, n7_rally, sack, ruins), "v1" (m6a); pole 18 arm_townfolk dopisane
na koncu (stary DLL pomija, stary zapis = 0, `TownFolk.Live.cs:1376`). Kazde nowe pole we wlasnym try (`ArmouryBehavior.cs:468-479, 641-656, 699-706`). Eksporty odrzucaja
id ze znakami separatorow (PeopleScale.SafeId, StragglersRally.Bad, M6a Bad, VillageByPeople `|;=:`); arm_m6a sklada segmenty po '|' - zaden segment go nie zawiera.
Round-trip na sucho (czytanie Export/Import obok siebie): PeopleScale 12 pol / Import >= 9 (normy 9-11 opcjonalne); VillageByPeople 8 pol; StragglersRally naglowek 6 pol +
typy + udzialy + W (udzialy "0.###" docinane do ludzi - `:839-840`); CitySack Track 14 pol, ruiny 5 pol; doubles w "R".

**Brak pola (stary zapis).** peoplescale -> kalibracja przy wczytaniu ze stanu dnia; villagepeople -> g przy pierwszej dobie z kluczem; peoplemarket -> `_start = 2` (zloto
startowe nigdy); goodsrates -> `_flax = 0` (bez lnu, sama sol); m6a -> pamiec pusta; n7_rally -> pula pusta; sack/ruins -> pusto; pole 18 -> licznik 0. Sarnor - `_k` z
arm_population (stary zapis bez zmian). Zgodne z decyzja "stary zapis bez ochrony".

**Rozmiar po 2 latach (stan ograniczony, nie rosnie z czasem).** arm_peoplescale ~20-25 KB (571 wsi x targ - kawalki SaveText), villagepeople ~2 KB (typy), peoplemarket
i goodsrates < 20 B, m6a: kompanie ~20 x 30 B + okupy w oknie 56 dob (test: 194 osoby ~5 KB) + pary 24 h, n7_rally: grupy do czasu czekania (test d120: 45 grup ~9 KB),
sack: okno 30 dob, ruins: najwyzej budowle osad (realnie setki wpisow), pole 18: ~6 B x osada. Wszystko przez SaveText.Sync (kawalki 8000 zn.).

**Nowa kampania - kolejnosc (log 20-41-38).** OnNewGameCreated: `PeopleMarket.OnNewGame` (`_start = 0`), `GoodsRates.OnNewGame` (`_newGame`) - `ArmouryBehavior.cs:724-725`;
OnSessionLaunched: McmSettings.Apply (1324) -> T8 (1329, l.277) -> GL5 len "kampania zaczeta z kluczem (dzien 108836)" (1330, l.278) -> drwale K (1333) -> GL0 kalibracja
"nowa kampania" (1365, l.314) i mianowniki (l.315) -> GL1 g z lnem (1366, l.317) -> RawPrice.SeedNewCampaign (1368; popyt z `GetEstimatedDemandForCategory` liczony TERAZ,
przy gotowej pamieci skal). Doba 1: TownPurse.BeforeRents (1664) -> zloto startowe (1665, l.517 "doba kampanii 1.00", po darze kas i kiesach z daru) -> CastlePurse.Daily
(1672) -> pula podzamczy (1673, l.521 "dar zamkow rozliczony", "zgodne"). StartStock/ColdStart przed GL0 - nie uzywaja m_t (sztabki/drewno po cenie, sprzet).

**Wczytanie - nic drugi raz.** Zloto startowe: flaga `_start` w zapisie, 1 = nigdy; zapis z doby 0 (`_start = 0`) wykonuje je raz, do doby 3.5 (`PeopleMarket.cs:458-482`).
Podmiana typow wsi: typ wsi nie idzie do zapisu gry - T8 i GL5 podmieniaja co sesje, `BkClass.Align` idempotentne (`VillageClimate.cs:409-410`: "juz zgodna" przed "inna").
Kalibracje: GL0 i GL1 z zapisu (`_cal = true`), `refV` round-trip "R" dokladny -> brak rekalibracji, `_wasOn` z zapisu -> brak restartu regulatora. Ruiny: `Lvl` w zapisie
-> `Grant` nie daje postepu drugi raz (`CitySack.Burn.cs:94-101`). Migawka skarbcow 165 przesuwana przez L5b-1 i zloto startowe (`CrownIncome.ShiftSnapshot`) jest w zapisie
arm_crown165 razem z doba migawki. Eksport przed rozwiazaniem wczytanego stanu oddaje surowy napis (CitySack `_pending`, ruiny `_ruinPending`, TownFolk `EnsureImported`,
N7 grupy jako id).

**Czyszczenie stanu miedzy kampaniami w jednym procesie.** Konstruktor ArmouryBehavior: +12 Reset (AlleyProbe, CitySack z RuinsReset, GoodsRates, MercTenure, PackSources,
PeaceHold, PeopleLevy, PeopleMarket, PeopleScale, RansomRepeat, StragglersRally, VillageByPeople); czesci klas: BuildFunding.Reset -> FolkReset; KingdomTreasury -> NeedAidReset ->
LordLevyReset + ScutageReset -> PlReset, WarTaxReset -> WarAidReset -> FolkAidLoyalty.Reset; TownFolk.Reset -> `_hDay`, `_skupN`, ResetLive -> ResetHorses, ResetLadder;
CapitalTreasury/EnemyCaravans.Reset w RegisterEvents (latki `_hookTried` raz na proces - poprawnie).

**Latki Harmony w menu (CLAUDE.md rozdz. 7) - wszystkie nowe tej nocy.** W menu (SubModuleMain / Seal31.ApplyAll): VillageByPeople.ApplyTip -> TooltipRefresherCollection
(jedyne pole statyczne: komparator, `dec/trc.cs:29`) i NavalTooltipRefresherCollection; TownFolk.ApplySkup i AlleyProbe -> SellGoodsForTradeAction.ApplyInternal (brak pol
statycznych, `dec/SellGoodsForTradeAction.cs`); PeopleMarket.ApplyAll -> DefaultSettlementFoodModel (GameTexts w polach INSTANCJI, nie statycznych - `dec/DefaultSettlementFoodModel.cs:14-26`);
TownFolk.LadderApplyAll -> DefaultSettlementEconomyModel i BEE_SettlementEconomyModel (bez pol statycznych); PackSources -> VillagerPartyComponent.InitializationArgs, DTE
CutTheirSupplyBehavior (statyczne MethodInfo/HashSet), BK BannerKingsComponent/PopulationPartyComponent (brak), MobileParty.RemoveParty (tylko wlasciwosci statyczne);
CitySack.ApplyAll -> SiegeAftermathCampaignBehavior (brak pol statycznych, `dec/sacb.cs`), ChangeOwnerOfSettlementAction (brak), BKSettlementBehavior (statyczne MethodInfo).
W kampanii (EnsureHooks z OnSessionLaunched): PeopleLevy (modele milicji gry i BK), FolkAidLoyalty (BKLoyaltyModel), MercTenure, PeaceHold, CapitalTreasury i EnemyCaravans
(KingdomManager.SiegeCompleted przez Seal31.EnsureHooks). Log 20-41-38: "latki przy starcie gry ... BRAK: -", 0 wyjatkow w calym logu, kampania przeszla 120 dob.
Uwaga: L5b (HEAD ponad 7a22817) nie byl w zadnym biegu w grze - ale jego latki sa wylacznie w kampanii.

**Wyjatki per obiekt.** Brak nowych globalnych wylacznikow (przeszukane `catch` w diffie: tylko flagi chwilowe `_inBkFiring`, `_aiGame`, `_inTick` zdejmowane w finalizerach).
Importy - per wpis (N7 grupa, lup wpis, M6a wpis), GL0/GL1 - caly stan od nowa z linia w logu (swiadomie). `AlleyProbe.TickOpen` poza try w `OnDailyTickBody` ma wlasny try.
