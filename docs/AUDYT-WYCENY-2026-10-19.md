# Audyt: co gra i mody licza z ceny przedmiotu (2026-10-19, f28-wyceny, TYLKO ANALIZA - kod gry nietkniety)

Zlecenie: Jeff 19.10 ("teraz gram co mozesz z tej listy robic kiedy ja gram"), punkt 7 listy: przeglad wszystkiego, co gra liczy
z ceny przedmiotu, po przeliczeniu cen historycznych (HistoricalPrices).

## Dla Jeffa (prosto)

Nasz mod przelicza ceny: bron i zbroje srednio 33 razy taniej (log 19.10: suma 34 397 461 -> 1 041 768 d za 3 330 sztuk), surowce,
towary, zywnosc i bydlo tez (zboze 10 -> 3, narzedzia 250 -> 40, garnki 210 -> 8, surowka 20 -> 1, owca 80 -> 17). Gra i mody w wielu
miejscach dziela albo porownuja cene ze STALA liczba wpisana w kod w starej skali. Kowalstwo, badania czesci, turnieje i ksiazki juz
naprawilismy. Znalazlem jeszcze takie miejsca:

1. **Zadania (questy) zadaja absurdalnych ilosci** - ilosc = stala / cena. Przy tanszym towarze wychodza setki sztuk:
   - "Gang potrzebuje broni" (siekiery jednoreczne): zamiast ok. 13 - ok. 250-400 siekier (szacunek); do tego straz przy wyjsciu z miasta
     zatrzymuje bron za 1 500 "starych" denarow, czyli kilkanascie-kilkadziesiat sztuk zamiast jednej-dwoch.
   - "Wies potrzebuje surowca do kuzni" (surowka): zamiast ok. 18 - ok. 375 sztuk.
   - "Sztuka handlu" (wlasciciel ziemski daje Ci towar wsi do sprzedania): np. drewno 625 ladunkow po 100 kg albo 833 worki zboza - nie
     ruszysz sie z miejsca.
   - "Rzemieslnik i za drogi towar": np. 600+ garnkow zamiast ok. 23.
   - Mniej groznie: narzedzia dla wsi (31 zamiast 5), stado do zagnania (75 zamiast 31), krowy (x1.8), skradziony towar gangu.
   Zloto za zadania juz poprawialismy (QuestPay8) - ale ILOSCI nie. To jest najwazniejsza rzecz z tego audytu.
2. **Lotrostwo (Roguery) za najazdy na wsie i karawany rosnie 3-20 razy wolniej** - gra daje XP od wartosci zrabowanych towarow.
   Dotyczy Ciebie i lordow AI.
3. **Handel z magazynu warsztatu** (opcja "do magazynu") daje mniej XP Handlu - tez od wartosci towaru.
4. **Spoils of War w bitwach automatycznych** przestal niszczyc drogie zbroje (prog 5 000 d - dzis prawie nic go nie przekracza), za to
   bron z valyrianskiej stali (wyceniona na 60-120 tys.) niszczy w 70%.
5. **Lordowie AI rzadziej jada do miasta sprzedac lup** - ich "chec sprzedazy" liczy sie od wartosci lupu wobec stalej 100 na glowe.
6. Drobiazgi: nocny atak na kryjowke daje wiecej towaru niz w zamysle; BK "Kup ksiazke" (cel/decyzja) kosztuje 30 x wiecej niz u
   ksiegarza; turniej BK we wlasnym miescie daje zawsze ok. -10 slawy; paser Spoils nie bierze broni tanszej niz 100 d; slawa za
   oddany sprzet do pod-klanu Spoils 20-30 x mniejsza; elitarne nagrody Tournaments XPanded tylko t6.

Nic z tego nie psuje gry tak jak badania czesci (nic nie crashuje), ale punkt 1 robi z kilku zadan zadania nie do wykonania,
a punkt 2 spowalnia rozwoj. Proponuje poprawiac w kolejnosci: 1, 2, 4, 3, 5, reszta jesli zechcesz. Kazda poprawka w tym samym stylu co
XP kowalstwa: gra liczy dalej swoim wzorem, ale z wartosci sprzed przeliczenia, pod nowym kluczem w MCM.

## Jak szukalem (dowod, nie zgadywanie)

- Skaner IL (wlasny, System.Reflection.Metadata): kazda metoda kazdego zestawu, ktora woluje `ItemObject.get_Value`,
  `EquipmentElement.get_ItemValue / GetBaseValue`, `ItemRoster.TotalValue / TradeGoodsTotalValue`, plus stale >= 100 w tej samej
  metodzie. Drugi przebieg: wywolania cen (`GetItemPrice`, `GetPrice`, `GetPriceFactor`, `GetItemCategoryPriceIndex`) ze stalymi.
- Zestawy (gra 1.4.8 z dysku, tylko odczyt): TaleWorlds.Core (8 trafien), TaleWorlds.CampaignSystem (97), TaleWorlds.CampaignSystem.
  ViewModelCollection (0), TaleWorlds.MountAndBlade (0), SandBox (2), SandBox.ViewModelCollection (1), StoryMode (0), NavalDLC (1);
  mody: BannerKings (34), BetterEconomy (10), DynamicTroopEquipmentReupload 1.4.7 (15), RealisticLoot = Spoils of War (22),
  ROT (2), TournamentsXPanded 1.4.1 (8), BKROTPatch (0), RBM / RBMAI / RBMCombat / RBMTournament (0), RealisticBannerlord (0),
  StrategicCampaignAI (0), Diplomacy 1.4.7 (0), BannerlordPrisonIsNotFun (0), DynamicReinforcements (0), BirthAndDeath (0);
  nasze: GrandTourney (2), RealisticCaptivity (4), Armoury / CrashScribe / ForgeView (206 - przejrzane tylko te ze starymi stalymi).
- Kazde trafienie z CampaignSystem i modow przeczytane w dekompilacji (ilspycmd) i sprawdzone, czy kategoria jest przeliczona
  (log Jeffa `Armoury-2026-10-19_09-52-41.log`, linie 310-319, 431: bron/zbroje, surowce, 60+ towarow, bydlo, konie t3 x0.4 i t4 x1.5,
  ksiazki x100) i czy nasz kod juz to obsluguje (grep po Armoury/RealisticCaptivity/GrandTourney/CrashScribe).
- Nie sprawdzone: AIInfluence.dll (zaciemniony - czytnik metadanych nie otwiera); ustawienia MCM Spoils of War Jeffa (folder
  Documents poza zasiegiem tego zadania - przyjete wartosci domyslne z kodu Spoils).

## Tabela ustalen

Waga: **krytyczne** = funkcja zepsuta (jak badania czesci); **wazne** = zauwazalnie zla nagroda / postep / zachowanie AI;
**drobne** = kosmetyka albo pomijalny skutek; **obsluzone** = juz zalatane przez nas (albo z konstrukcji nie dotyczy).

| # | Miejsce | Wzor | Skutek w grze Jeffa | Obsluzone? | Waga | Proponowana poprawka |
|---|---|---|---|---|---|---|
| 1 | `GangLeaderNeedsWeaponsIssue.RequestedWeaponAmount` + `CalculateAveragePriceForWeaponClass` (CampaignSystem) | `(int)(20000 / sredniaWartoscSiekierWMiastach x {0.1/0.2/0.3})` | sredni Value siekiery 1H spadl ok. 20-30 x -> zadanie zada ok. 250-400 siekier zamiast ok. 13 (szacunek: bron srednio /33) | NIE (QuestPay8 liczy zloto, nie ilosc) | krytyczne | klucz `HistQuestQtyOldCoin`: w getterze `RequestedWeaponAmount` stala 20000 przeskalowana o (srednie Value / srednie Worth siekier na polkach - ta sama petla co gra, z FixedSums38.Worth); `_averagePriceForItem`, nagroda i koszt zastepczy zostaja w nowej monecie |
| 2 | `GangLeaderNeedsWeaponsIssueQuest.OnSettlementLeft` | straz zatrzymuje bron, dopoki suma Value < 1500 (40% na sztuke) | budzet 1500 starych d = kilkanascie-kilkadziesiat sztuk zamiast 1-2 (QuestPay8 kladzie je na polke miasta, ale gracz je traci) | czesciowo (dokad trafiaja - QuestPay8; ile - nie) | wazne | ten sam klucz: transpiler ValueToWorth w `OnSettlementLeft` (porownanie z 1500 w starej monecie) |
| 3 | `VillageNeedsCraftingMaterialsIssue._numberOfRequestedItem` | `Round((int)(750 / Value) x trudnosc)`, przedmiot = surowka (ironIngot1 20->1, ironIngot2 30->1) | przy trudnosci 0.5: ok. 375 sztuk surowki zamiast 18 (przy 1.0 - 750) | NIE | krytyczne | `HistQuestQtyOldCoin`: transpiler ValueToWorth w getterze `get__numberOfRequestedItem` (zaplata `GetPayment` = 750 + cena x ilosc zostaje w nowej monecie; stala 750 skaluje juz QuestPay8) |
| 4 | `LandLordTheArtOfTheTradeIssue.SelectedItemObjectCount` | `max(1, Round((int)(5000 / Value) x trudnosc))`, towar = glowny wyrob wsi, gracz DOSTAJE ten towar (`AddToCounts`) | zboze 3 d -> 833 szt. (trudnosc 0.5), glina 1 d -> 2 500, drewno 4 d -> 625 ladunkow po 100 kg: gracz przeciazony, cel denarow (0.55 x Value x ilosc) bez sensu | NIE | krytyczne | `HistQuestQtyOldCoin`: ValueToWorth w `get_SelectedItemObjectCount` (cel 0.55 x Value x ilosc liczy sie dalej z Value - w nowej monecie, spojnie) |
| 5 | `ArtisanOverpricedGoodsIssue` (konstruktor, linia `RequestedTradeGoodAmount`) | `max((int)(10000 / Value x trudnosc), 1)` | garnki 8 d -> ok. 625 szt. zamiast 23; koszt zastepczy 0.75 x Value x ilosc w nowej monecie | NIE | krytyczne | `HistQuestQtyOldCoin`: ValueToWorth tylko na wywolaniu przed `ldc.r4 10000` w konstruktorze (kotwica na stalej, jak BookTranspiler) |
| 6 | `VillageNeedsToolsIssue` (konstruktor) | `Round((int)(2500 / Value) x trudnosc)`; narzedzia 250 -> 40 | 62 x trudnosc zamiast 10 x trudnosc (x6.25); wymiana na wyrob wsi liczona od kwoty - spojnie | NIE | wazne | `HistQuestQtyOldCoin`: ValueToWorth na wywolaniu przy `ldc.r4 2500` (nagroda 500 + szt. x (cena+Value)/2 zostaje w nowej monecie) |
| 7 | `HeadmanNeedsToDeliverAHerdIssue.AnimalCountToDeliver` | `Clamp(Round(5000 x trudnosc / Value), 10, 75)` | owca 17 d / swinia 30 d: prawie zawsze sufit 75 (bylo 31 przy 0.5), krowa x1.8 | NIE | wazne | `HistQuestQtyOldCoin`: ValueToWorth w `get_AnimalCountToDeliver` |
| 8 | `HeadmanVillageNeedsDraughtAnimalsIssue` (konstruktor + `OfferedMeatAmount`) | ilosc `(int)(5000 / Value) x trudnosc` (krowa 200->113, mul i juczny bez zmian); mieso `(300 + Value x ilosc) / Value(mieso) x 2` | krowy x1.8; mieso oferowane w zamian ok. x3.3 (mieso 30 -> 9) | NIE | drobne | ten sam klucz: ValueToWorth w konstruktorze i w `get_OfferedMeatAmount` |
| 9 | `GangLeaderNeedsToOffloadStolenGoodsIssue.StolenTradeGoodAmount` | `Ceiling(10000 / cenaWMiescie x trudnosc)` (cena, nie Value - ale cena idzie za Value) | towar kradziony tanszy -> wielokrotnie wiecej sztuk | NIE | wazne | ten sam klucz: w getterze cena x (Worth/Value) przedmiotu (pomocnik `FixedSums38.OldCoinPrice(town, item)`) |
| 10 | `SmugglersIssueQuest.GiveGoodsToParty` | lup przemytnikow: `(400 + 3000 x trudnosc) / Value` sztuk towaru | budzet w starej monecie zamieniony po nowej cenie - wiecej towaru do zdobycia | NIE | drobne | ten sam klucz: ValueToWorth w `GiveGoodsToParty` |
| 11 | `DefaultSkillLevelingManager.OnRaid / OnLoot / OnForceSupplies` | XP Lotrostwa = `TradeGoodsTotalValue x (0.5 / 0.1-0.75)` + stale za konie i bydlo | towary wsi i karawan 3-20 x tansze -> Lotrostwo z najazdow i rabunku spada tyle samo (gracz i lordowie AI); czesc "za sztuke zwierzecia" bez zmian | NIE (SkillLedger tylko liczy) | wazne | klucz `HistRogueryXpOldValue`: postfiks na trzech metodach (deklarowane w DefaultSkillLevelingManager) - delta = suma (Orig - Value) x ilosc po sztukach `IsTradeGood` z `lootedItems`, x ten sam mnoznik co gra; `LeaderHero.HeroDeveloper.AddSkillXp(Roguery, delta, true, true)` (ta sama droga co `OnPersonalSkillExercised`); nigdy mniej niz gra |
| 12 | `DefaultWorkshopModel.GetTradeXpPerWarehouseProduction` (wola `OnWarehouseProduction`) | XP Handlu = `GetBaseValue() x 0.1` za sztuke odlozona do magazynu warsztatu gracza | towary tansze -> mniej XP Handlu (piwo /16, garnki /26, narzedzia /6; aksamit x7 w gore) | NIE | wazne | klucz `HistWarehouseXpOldValue`: postfiks na `DefaultWorkshopModel.GetTradeXpPerWarehouseProduction` przez `AccessTools.DeclaredMethod` (pulapka 15.10) - `__result = max(__result, 0.1 x Orig x mnoznik modyfikatora)` |
| 13 | Spoils of War `RealisticLootModel.ProcessSingleCasualty` (i drugie miejsce z tym samym progiem, ok. linii 640) | `GetBaseValue > 5000` -> zniszczone w bitwie 30% / >20000 50% / >50000 70% (x DestructionChanceMultiplier) | w bitwach AUTOMATYCZNYCH gracza (w stoczonych lup daje DTE - BattlefieldLaw) zbroje t5-t6 juz nie gina (dawniej 20-50 tys., dzis 1-2.5 tys.); stal valyrianska (60-120 tys.) ginie w 70% | NIE (BattlefieldLaw zdejmuje tylko mnozniki tieru) | wazne | klucz `HistSpoilsDestroyOldCoin`: transpiler w obu metodach - `EquipmentElement.GetBaseValue` -> `Worth(item) x PriceMultiplier modyfikatora` (nowy pomocnik w FixedSums38) |
| 14 | `AiVisitSettlementBehavior.CalculateSellItemScore` i kopia BK `BKAIVisitSettlementBehavior.CalculateSellItemScore` | `1 + min(3, (wartoscLupu / ((ludzie + 5) x 100))^0.33)` | lup lorda wart 20-33 x mniej -> czynnik checi wizyty w miescie spada np. z 2.2 do 1.4: AI rzadziej jedzie sprzedac lup, bron wolniej wraca na targi | NIE | wazne | klucz `HistAiLootScoreOldCoin`: transpiler ValueToWorth w obu metodach (obie, bo nie wiadomo, ktora jest czynna - BK podmienia zachowanie) |
| 15 | `...CalculateVisitHideoutScoresForBanditParty` (vanilla i BK) | `1 + 4 x min(wartoscLupu, 1000) / 1000` | bandyci rzadziej wracaja do kryjowki z lupem | NIE | drobne | ten sam klucz, ta sama latka |
| 16 | `HideoutCampaignBehavior.OnCollectLootItems` (nocny atak na kryjowke) | dodatkowy towar az do `Clamp(lup x populacja x 135, min, 4750)` liczonego jako `GetTheoreticalMaxItemMarketValue + 1 = Value x 10 + 1` | 4750 "starych" = ok. 475 d towaru po nowych cenach - kilka-kilkanascie razy wiecej rzeczy niz w zamysle (z niczego) | NIE | wazne | klucz `HistHideoutLootOldCoin`: transpiler w `OnCollectLootItems` i lambdzie - wywolanie `GetItemValueForHideoutLoot` -> `Worth(item) x 10 + 1` |
| 17 | `DefaultBattleRewardModel.GetRandomItem` (lup z ciala dla gracza) | szansa `0.325 x cel / (max(cel, Value) x n)`, cel = `7.25 x poziom^2` (stara moneta) | prawie kazda sztuka ponizej celu -> szansa maksymalna, wiecej sztuk z ciala; u Jeffa dotyczy tylko bitew poza DTE/Spoils (oblezenia, kryjowki, morze) | NIE | drobne | jesli bedzie widac w logu: ValueToWorth w `GetRandomItem` |
| 18 | BK `AcquireBookDecision.ShowInquiry / ApplyGoal` (cel "Acquire Book") | cena = `Value x 30` | ksiazka 1 000 d (po naszym Book Price Fix) -> 30 000 d przez cel BK, u ksiegarza 1 000 d; zloto w nicosc | NIE (BookBuyPrefix tylko ekran ksiegarza) | wazne | rozszerzyc klucz `BookPriceFix` (albo nowy `BookGoalPriceFix`): transpiler jak BookTranspiler - `get_Value, ldc.i4.s 30` -> stala 1 w obu metodach |
| 19 | BK `BKTournamentBehavior.OnTournamentFinished` + `EconomyVM.OnTournamentPress` (turniej BK we wlasnym miescie) | lista nagrod: `Value > 100`; slawa `-10 + cena/1000` (cena <= 10 000) | do wyboru tylko zbroje t4+ i konie; slawa zawsze ok. -10 (w BK i tak zwykle ujemna - blad BK, my go poglebiamy) | NIE | drobne | ValueToWorth w `OnTournamentPress`; w `OnTournamentFinished` cena x Worth/Value |
| 20 | Tournaments XPanded `IsElitePrizeCandidate`, `CachePossibleEliteRewardItems` | elita: tier enum < Tier5 tylko gdy `Value >= GetMaxPrizeValue()` (2100 + 100 x poziom + 100 x lord) | po przeliczeniu elita = same t6 (enum Tier5); u Jeffa nagrode i tak wybiera RBMTournament, TXP to zapas | czesciowo (FixedSums38 laczy tylko nagrode zwykla) | drobne | `FixedSums38.Hook` na `IsElitePrizeCandidate` i lambdach sortujacych (ten sam ValueToWorth) |
| 21 | ROT `EnlistmentPatches.patch19` (lup gdy gracz sluzy w armii) | lup daje sie tylko gdy suma cen >= 250; koniec gdy reszta < 500; mak gdy reszta >= 1000 | po przeliczeniu progi 250/500/1000 rzadko osiagniete -> zaciezny rzadziej dostaje lup (i traci schowek) | NIE | drobne | jesli Jeff gra zacieznym: transpiler stalych (x moneta popytu broni) albo suma GetPrice x Worth/Value |
| 22 | NASZ `SpoilsSeal.FenceSale` (paser Spoils, paczka 128) | pomija sztuki `it.Value < FenceMinItemValue` (MCM Spoils, domyslnie 100) | paser nie bierze broni tanszej niz 100 d - prawie calej broni t1-t5 | NIE (nasz kod porownuje stara stala z nowa wartoscia) | drobne | w `FenceSale`: `FixedSums38.Worth(it) < min` (cena oferty dalej z Value) |
| 23 | NASZ `SpoilsSeal.Donate` (pod-klan Spoils) | slawa = `worth / 10000` (worth w nowej monecie) | slawa za oddany sprzet 20-33 x mniejsza | NIE | drobne | slawe liczyc z sumy Worth sprzedanych sztuk (zaplata dalej z Value) |
| 24 | `DefaultRaidModel.CommonLootItemSpawnChances` | waga lupu najazdu `100 / (Value + 1)` (skory, drewno, narzedzia, zboze, len, owca, mul, garnki) | sklad lupu przesuwa sie ku garnkom, drewnu i zbozu; mul ok. 4 x rzadszy | NIE | drobne | opcjonalnie: wagi z Orig (postfiks na getterze, raz) |
| 25 | `BanditSpawnCampaignBehavior` / `DesertersCampaignBehavior` / NavalDLC `PiratesCampaignBehavior.GiveFoodToBanditParty`, BK `GiveFood` | jedzenie bandy z niczego: `ludzie x (3 + 6r) / Value` | zboze 3 d -> 3 x wiecej jedzenia bandy (i lupu zboza po rozbiciu) | czesciowo (BK GiveFood liczy TravellerGoods36) | drobne | opcjonalnie ValueToWorth w tych metodach |
| 26 | `DefaultPartyFoodBuyingModel.FindItemToBuy` (+ BK `BKPartyBuyingFoodModel`), `FoodConsumptionBehavior.PartyConsumeFood` | wynik `(120 - cena)^2 x (100 - Value)^2`; zjada najtansze | cale jedzenie ponizej 100 -> preferencja taniego zboza slabnie (z 2.25 x do 1.24 x wzgledem sera); kolejnosc zjadania inna | NIE | drobne | bez zmian albo ValueToWorth (niski priorytet) |
| 27 | `ItemBarterBehavior.CheckForBarters`; `DefaultDiplomacyModel.GetScoreOfLettingPartyGo`; `SafePassageBarterable` | barter tylko sztuk `GetBaseValue > 100`; ocena puszczenia partii od wartosci jej taboru; cena przejscia od majatku | mniej sztuk do barteru; AI chetniej puszcza partie z lupem; przejscie tansze (spojnie z nowa moneta) | NIE | drobne | bez zmian (ceny w nowej monecie sa zamierzone) |
| 28 | `CaravansCampaignBehavior` / BK `BKCaravansBehavior` (stale 500, 4000, 5000, 10000, lapowka 2000) | sufity `min(500, Value)`, `min(4000, Value x n)`, wartownik 10000 przy wyborze najtanszego zwierzecia | sufity rzadziej osiagane; decyzje oparte na stosunkach cen - przeliczony popyt (DemandPostfix) je trzyma | czesciowo (DemandPostfix, MixedShelf) | drobne | bez zmian |
| 29 | BetterEconomy: `CastleEconomy...` (`spend / max(10, Value)`), `CaravanCampaignBehavior.TryDeliverContract`, `LordInvestment.InjectRegionalGoods`, `VillageDevelopment.CalculateSecondaryUnits` (podlogi 10) | podloga 10 starych d: towar tanszy niz 10 (zboze, piwo, sol, drewno, surowka) liczony jak 10 | mniej sztuk za to samo zloto (zboze 3 x mniej) tam, gdzie BEE jeszcze cos robi | czesciowo (BeeSeal zamyka zbrojownie, WealthAudit, oplaty) | drobne | bez zmian, chyba ze log BEE pokaze kontrakty z tymi towarami |
| 30 | BK `EconomyPatches.ProduceOutputPrefix` (rzemieslnicy "artisans") | `count = max(1, 1 + rzemieslnicy / 45 / Value)` | moc rzemieslnikow 5-15 x wieksza (piwo 3 d), choc kazda sztuka ma juz swoj wsad | czesciowo (ArtisanInputs: wsad i zysk na cykl) | drobne | w ArtisanInputs liczyc liczbe cykli z Orig zamiast Value (ten sam wzor BK) |
| 31 | DTE `CompareEquipment`, `CompareArmor`, `CalculateEquipmentValue`, `SellExcessEquipment...` | najpierw tier, potem Value (tylko porzadek); zakup amunicji za budzet po Value | tylko kolejnosc w obrebie tieru (koszt historyczny - rozsadny); amunicja tansza = wiecej sztuk za te same pieniadze (spojnie) | nie dotyczy (bez stalych) | drobne | bez zmian |
| 32 | Incydenty `IncidentsCampaignBehaviour` (handlarz kupuje za 2-3 x Value; inwestycja 500 -> `1000 / Value` towaru; kon kultury `Value > 500`) | stale w zlocie po obu stronach albo filtr ceny konia | spojne w nowej monecie; konie t3 x0.4 wypadaja z filtra > 500 | NIE | drobne | bez zmian |
| 33 | `FightTournamentGame.GetTournamentPrize` (sortowanie po Value), `DefaultTournamentModel.GetRegularRewardItems` (1600-5000) | przedzial i sortowanie | przedzial: FixedSums38 (transpiler), dzielenie /4 przy wylaczonym; sortowanie tylko porzadek; u Jeffa wybiera RBMTournament | TAK (FixedSums38 / PrizeRangePrefix, ValyrianBlades) | obsluzone | - |
| 34 | `DefaultSmithingModel` XP (3 metody) i badania czesci (2 metody) | `0.02 / 0.1 / 0.3 x Value`, `1 + floor(0.1 x Value x ...)` | - | TAK (XpPostfix, ResearchSmithPostfix / ResearchSmeltPostfix, WGRANIE 87) | obsluzone | - |
| 35 | Zlecenia kowalskie `CraftingOrder` (BaseGoldReward, GetOrderExperience, roznica ceny) | `GetTheoreticalMaxItemMarketValue` projektu | - | TAK (CraftOrders8 + PriceDesign) | obsluzone | - |
| 36 | BK ksiazki `BKEducationBehavior.OnBuyBookConsequence` | `Value x 1000` | - | TAK (BookTranspiler + CraftOrders8.BookBuyPrefix) | obsluzone | - |
| 37 | Zloto za zadania (`RewardGold`, GiveGoldAction w 76 metodach CampaignSystem) | stale + wartosc towaru | - | TAK (QuestPay8 - ale tylko zloto, ilosci patrz 1-10) | obsluzone | - |
| 38 | `PartiesBuyHorseCampaignBehavior` (srednia cena koni, 100 000) | handel konmi lordow | - | TAK (SpareHorses E6/K2 - gra nie handluje konmi lordow) | obsluzone | - |
| 39 | BK `PopulationPartyComponent.GiveItems` (budzety 30/100/300 starych d) | towar z niczego do budzetu po Value | - | TAK (TravellerGoods36 zdejmuje wszystko, co dodal) | obsluzone | - |
| 40 | BK ofiary religijne `Offering / WeaponOffering.GetPietyReward` | `sqrt(Value x n) x 3`, `sqrt(ItemValue)` | ROT (BKROTPatch) nadpisuje wszystkie ofiary stala 100 poboznosci - wzor BK nie biegnie | z konstrukcji | obsluzone | - |
| 41 | BEE zbrojownie miast (`IsArmoryOutputItem Value > 20`, `Value <= budzet x 2 / 250`) | filtry w starej monecie | produkcja gotowych zbrojowni BEE zablokowana | TAK (BeeSeal paczka 170) | obsluzone | - |
| 42 | ROT `ROTGankBehavior.GetRandomItem` (zamiennik broni jenca, 100) | | - | TAK (HeroGear Z16) | obsluzone | - |
| 43 | Spoils `QuartermasterBehavior` (auto-sprzedaz `ReserveExpensiveAbove 1500`, `QMAutoSellMaxValue 1000`), `AutoEquipPlanner.GetEffectiveValue` | | - | TAK (SpoilsSeal 1: auto-sprzedaz zablokowana; HeroGear: AutoEquipPlanner) | obsluzone | - |
| 44 | GrandTourney `TourneyBehavior.SetLocalPrize` (400 - LocalPrizeMaxValue) | | - | TAK (WorthLink38 -> FixedSums38.Worth) | obsluzone | - |
| 45 | Kosmetyka: `StatisticsCampaignBehavior.OnNewItemCrafted` (rekord najdrozszego wyrobu), `PrisonBreakMissionController`, `SandBoxUIHelper` (kolejnosc w powiadomieniu), `PartyScreenLogic`, BK `ArmorItemVM` / `MercenaryCareerVM` (napis Value), RealisticCaptivity `GiveRags` / odkup (Value x mnoznik - w nowej monecie) | | tylko napisy / kolejnosc | nie trzeba | drobne | bez zmian |

Uwaga do projektu: zysk z handlu (`OnTradeProfitMade`) to zysk w zlocie - po tanszych towarach XP Handlu z handlu tez jest nizsze. SPIS-CEN-GRY
3.3 pkt 7 uznal to za zamierzone; jesli Jeff chce postep Handlu jak w grze, to osobna decyzja (nie liczylem tego jako bledu).

## Wspolny klocek poprawek (propozycja, nic nie wpisane)

Wszystko w jednym stylu, jak XpPostfix / ResearchSmithPostfix i FixedSums38:

1. `FixedSums38.Worth(ItemObject)` juz jest (Orig przy wlaczonych cenach, inaczej Value) - wystarcza dla transpilerow ValueToWorth
   w punktach 1-10, 14-16, 20, 22. Brakuje dwoch pomocnikow: `WorthOf(EquipmentElement)` = Worth x PriceMultiplier modyfikatora
   (punkt 13) i `OldCoinPrice(Town, ItemObject)` = cena x Worth / Value (punkty 9, 19, 21).
2. Nowe klucze MCM (domyslnie wlaczone, opis po angielsku, `tools/gen_mcm.py`): `HistQuestQtyOldCoin` (1-10),
   `HistRogueryXpOldValue` (11), `HistWarehouseXpOldValue` (12), `HistSpoilsDestroyOldCoin` (13), `HistAiLootScoreOldCoin` (14-15),
   `HistHideoutLootOldCoin` (16); punkt 18 pod `BookPriceFix`. Linia startowa jak "XP kowalstwa od dawnych wartosci w 3/3 metodach"
   z liczba wpietych metod i BRAK - zeby bylo widac w logu, co weszlo.
3. Zadania (1-10): transpiler tylko w getterach/konstruktorach ILOSCI, nie w nagrodzie - nagrode w nowej monecie liczy dalej QuestPay8.
   W metodach mieszanych (konstruktor VillageNeedsTools, ArtisanOverpricedGoods) kotwica na stalej `ldc.r4` stojacej przy
   wywolaniu (jak BookTranspiler), zeby nie ruszyc liczenia nagrody.
4. Kontrola calosci przed wgraniem (zasada glowna): kazda latka na metodzie deklarowanej (AccessTools.DeclaredMethod), sprawdzic
   BKROTLog "Latki czynnego modelu"; zadania zamrozone w zapisie (QuestPay8 arm_e8_questpay) - ilosc trzyma obiekt zadania od
   chwili przyjecia, wiec poprawka dziala dla zadan wzietych po wgraniu; stare zostaja jak sa.

## Kolejnosc (proponowana)

1. Zadania 1-7 (krytyczne: bron gangu, surowka, sztuka handlu, rzemieslnik) - jedna zmiana, jeden klucz.
2. Lotrostwo z najazdow (11).
3. Spoils - niszczenie w bitwie (13).
4. XP magazynu warsztatu (12), ksiazka przez cel BK (18).
5. Chec sprzedazy lupu AI (14-15) - najpierw w logu sprawdzic, ile lup lordow lezy w taborach.
6. Reszta drobnych - tylko jesli Jeff zechce.
