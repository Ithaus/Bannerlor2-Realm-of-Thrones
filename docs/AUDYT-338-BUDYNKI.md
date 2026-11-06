# Audyt 338 - budynki: czy robia to, co pisze ich opis (06.11.2026)

Prosba Jeffa: "zrob audyt budynkow, czy w ogole dzialaja, czy trzeba je przeprogramowac i naprawic, aby to, co robia, dzialalo".
Galaz `noc/338-budynki` od `091ed773` (WGRANIE 168). Projekt naprawy: `docs/PROJEKT-338-BUDYNKI.md`.

Zakres: kazdy budynek, ktory gracz moze budowac (miasta, zamki, wsie: wanilia, BK, ROT, NavalDLC, Armoury), pod czynnym zestawem modeli
(BK podmienia wiele modeli gry; ROT, NavalDLC, BEE owijaja je w lancuchy; Armoury doklada postfiksy). Kazdy wniosek MARTWY / CZESCIOWO
przeszedl druga, niezalezna probe obalenia (weryfikacja). Zrodla: dekompilacje w scratchpadzie (`dek/cs`, `dekZ/tcs`, `dec334e/full` - gra 1.4.x;
`bk147`, `dekZ/bkall` - BK; `dekZ/BKROTPatch`; `dek338/rot|bee|sca|...`; `dec334e/naval`), zrodla Armoury `n338/Armoury/src`, logi testu `at337`, `at334c`.

Stany: **DZIALA** - efekt czyta czynny model, liczby jak w opisie. **CZESCIOWO** - dziala w czesci osad / czesc efektu / dubel z drugim systemem.
**MARTWY** - nikt efektu nie czyta albo wynik jest zastepowany. **NIEPOTWIERDZONE** - teza audytu obalona lub bez dowodu; bez naprawy w 338.

Skala strat znana z pomiaru: zold zalog - 213-214 twierdz, ok. 30 000-31 400 zl/dobe obnizki nieodebranej (at337, linia `Zold zalog (337)`: `budynki martwe (BK)`).

## 1. Zold i miejsca zalog (koszary, biuro kasztelana, Slacken Garrison, autowerbunek)

| Budynek | Efekt (opis / definicja) | Stan | Dowod |
|---|---|---|---|
| Barracks (miasto) | miejsca zalogi +60/90/120 (GarrisonCapacity) | DZIALA | odczyt modelu rozmiaru zalogi, Add |
| Barracks (zamek) | miejsca zalogi +20/40/80 | DZIALA | j.w. |
| Fortifications (miasto 60/90/120, zamek 50/75/100) | miejsca zalogi | DZIALA | j.w. |
| Barracks (miasto) | zold zalogi -5/-10/-15% (GarrisonWageReduction, enum 13, AddFactor) | MARTWY | Gra czyta efekt tylko w `DefaultPartyWageModel.GetTotalWage:147`; czynny lancuch BKROT -> ROT -> NavalDLC -> BEE -> BK tej metody nie wola (BK liczy wlasna kopia `GetVanillaWage`). W kopii BK `BKPartyWageModel.cs:208-214`: `if (GetBaseBuildingEffectAmount((BuildingEffectEnum)13) > 0f)` - wartosci gry sa ujemne (`DefaultBuildingTypes.cs:193`), warunek nigdy nie przechodzi; skala /100 i brak sprawdzenia projektu dziennego. Nikt inny efektu 13 nie czyta (grep BK, BKROT, ROT, BEE, DR, BAD, SCA, GT, RBL, RC, NavalDLC, Armoury). Log at337: oszczednosc 0. |
| Barracks (zamek) | zold zalogi -10/-20/-30% | MARTWY | `DefaultBuildingTypes.cs:252`; ta sama droga jak wyzej |
| Castellan's Office (zamek) | zold zalogi -10/-20/-30% | MARTWY | `DefaultBuildingTypes.cs:272`; potwierdzone takze na DLL z `Modules\BannerKings.Redux` (ilspycmd) |
| Slacken Garrison (projekt dzienny) | zold zalogi -5% (tylko gdy biezacy projekt) | MARTWY | `DefaultBuildingTypes.cs:316`; petla BK i tak nie sprawdza `CurrentDefaultBuilding` - odwrocenie znaku w BK dawaloby -5% stale |
| Castellan's Office (zamek) | autowerbunek +1/+2/+3 na dobe (GarrisonAutoRecruitment, enum 14) | MARTWY (takze w czystej grze) | Efektu 14 nie czyta nikt (gra 1.4.x, BK, ROT, NavalDLC, BKROTPatch, BEE, Armoury). `DefaultSettlementGarrisonModel.GetMaximumDailyAutoRecruitmentCount:40-43` = stala 1; ROT/NavalDLC tylko deleguja. Armoury K8 (`ArmsFromPeople.cs:366-382`) daje `max(gra, GarrisonAutoRecruitPerDay=2)` bez budynkow. |
| Train Militia (projekt dzienny miasta) | autowerbunek +1 | MARTWY | `DefaultBuildingTypes.cs:304`; brak czytnika efektu 14 |
| Raise Troops (projekt dzienny zamku) | autowerbunek +2 | MARTWY | `DefaultBuildingTypes.cs:321`; brak czytnika |

## 2. Jedzenie i spichlerz

| Budynek | Efekt | Stan | Dowod |
|---|---|---|---|
| Waterworks (miasto) | spozycie jedzenia -5/-10/-15% | DZIALA | FoodConsumption |
| Warehouse (miasto) | zapas jedzenia +100/300/500 (FoodStock) + perki Battlements/Contractors | MARTWY | Jedyny czytnik: `Town.FoodStocksUpperLimit:467` (AddEffectOfBuildings). BK `SettlementPatches.FoodStockPatch:96-118` (prefiks, `return false` przy popData) liczy 2 x ludnosc + 500/250 + 1000 x poziom TYLKO dla CastleGranary. Magazyn nie podnosi sufitu wcale. ROT/RealisticLoot/Armoury sufitu nie latkuja (tylko czytaja). |
| Warehouse (miasto) | dodatek BK spoza opisu: +30% za poziom produkcji jedzenia ludnosci ('Local farmland') | CZESCIOWO (efekt spoza opisu) | `BKFoodModel.GetPopulationFoodProduction:192-201` (pierwszy z CastleFarmlands / SettlementWarehouse, AddFactor poziom x 0.3 miasto / 0.5 zamek), do bilansu x0.15 (`:59-72`), czynny przez `ROTSettlementFoodModel:44 -> BKFoodModel`. Opis magazynu tego nie obiecuje; BK przeniosl efekt z zapasu na produkcje. |
| Granary (zamek) | zapas jedzenia +100/200/300 + perki | CZESCIOWO | BK daje wlasne 1000 x poziom (`SettlementPatches:109-114`), liczby gry i perki Battlements/Contractors gina. (Pobocznie: redukcja gnicia BK `BKSettlementBehavior:859-868` liczy `num2`, ktorego nikt nie uzywa - martwa.) |
| Farmlands (zamek) | jedzenie +6/12/18 na dobe (FoodProduction) | CZESCIOWO (dubel) | Efekt gry dziala (`DefaultSettlementFoodModel:76` przez BK -> base). Ten sam budynek daje w BK drugi, niezalezny dodatek +50% za poziom do produkcji ludnosci ('Local farmland'). |

## 3. Milicja, wiezienie, doswiadczenie, patrole

| Budynek | Efekt | Stan |
|---|---|---|
| Guard House (zamek) | milicja +1/+2/+3, wiezienie +10/30/50 | DZIALA |
| Guard House (miasto) | patrol slaby/sredni/silny (odnowa 10/8/6 dni), wiezienie +30/60/90 | DZIALA |
| Train Militia (projekt dzienny miasta) | milicja +2 | DZIALA (autowerbunek z tego projektu - MARTWY, grupa 1) |
| Raise Troops (projekt dzienny zamku) | milicja +3 | DZIALA (autowerbunek - MARTWY, grupa 1) |
| Training Fields (miasto / zamek) | XP zalogi +1/2/3 (zamek +3/4/5), szansa weterana milicji +0.10/0.15/0.20 | DZIALA |
| Drills (projekt dzienny zamku) | XP zalogi +8 | DZIALA |
| Armory (BK, miasto) | XP +1 za poziom, szansa weterana milicji +4% za poziom | DZIALA |
| Trainning Grounds (BK, wies) | przyrost milicji wsi +0.2/0.5/1.0 | DZIALA |
| Arms Warehouse (BK, wies) | szansa weterana milicji wsi +4/8/12% | DZIALA |
| Fortifications (miasto / zamek) | wiezienie +50/75/100 (zamek +30/45/60), poziom murow | DZIALA |

## 4. Mury, oblezenia, tempo budowy

| Budynek | Efekt | Stan | Dowod |
|---|---|---|---|
| Siege Workshop | machiny obroncow na start oblezenia, szybsza budowa machin | DZIALA | |
| Mason | naprawa murow +10/20/30% (zamek +10/30/60%) | DZIALA | WallRepairSpeed |
| Mason (miasto +3/6/9, zamek +2/4/6) | punkty budowy na dobe (ConstructionPerDay, enum 4) | MARTWY | Czyta `BKConstructionModel:431` (`AddEffectOfBuildings((BuildingEffectEnum)4)`), ale Armoury `BuildFunding.PowerPostfix:134-153` i `PowerIntPostfix:154-160` (Priority.Last) dla osad `Covered()` - kazdy pan AI i gracz przy `PaidConstructionPlayer=true` (tak jest u Jeffa) - ZASTEPUJA caly wynik kupionymi punktami (`_funded` + Boost + EngAdd + robota ludu). Inzynierie juz naprawiono (`BuildFunding.Engineering.cs`), Masona nie. Dziala tylko w lennach gracza przy `PaidConstructionPlayer=false`. |
| Mines / Castle Mines (BK) | punkty budowy +0.5/1/1.5 (ConstructionPerDay w typie, dymek, brak w opisie) | MARTWY (efekt spoza opisu) | `BKBuildings.cs:146-154`; ginie w tej samej podmianie co Mason. Opis mowi tylko o urobku rud. |
| (pobocznie) polityka NavalDLC MaritimeWealEdict +20% budowy | nie budynek | MARTWY w osadach Covered | ginie w tej samej podmianie - poza zakresem 338, do odnotowania |

## 5. Dochod: podatki, cla, warsztaty, karawany

| Budynek | Efekt | Stan | Dowod |
|---|---|---|---|
| Marketplace (miasto) | cla +10/20/30% (TariffIncome) | DZIALA | |
| Craftsman's Quarters (zamek) | 0.2/0.4/0.6 zl x ogniska wsi zamku na dobe | DZIALA | |
| Warehouse (miasto) | produkcja warsztatow +5/10/15% | DZIALA | |
| Tax Office (miasto) | podatek +5/10/15% (TaxPerDay, AddFactor) | MARTWY | Jedyny czytnik `DefaultSettlementTaxModel.CalculateSettlementTaxDueToBuildings:146` wchodzi do wyniku BK, ale `PopulationLaw.TownTaxPostfix:1242-1270` (Priority.Last, `RentReplacesTownTax=true`) zastepuje caly wynik zachowanymi liniami + renta, ktore budynkow nie czytaja. U Jeffa `DsTowns=true` i rampa F3 = 1.000: pan i korona dostaja pieniadze miasta tylko droga firm `DsTowns.TownDay:1387 -> Pay(...)`; zawor `PopulationLaw.ValveTowns` i petla renty miast sa wtedy nieczynne. Grep `TaxPerDay` w Armoury = 0. |
| Marketplace (miasto) | "wiecej wizyt karawan" (CaravanAccessibility 1.02/1.04/1.06) - widoczne w podpowiedzi budynku (`module_strings.xml:6498`, NavalDLC `:130`) | MARTWY (takze w czystej grze) | Jedyny czytnik `CaravansCampaignBehavior.GetTradeScoreForTown:987-989` - w grze nie biegnie (BK `HourlyTickParty_Skip`, karawany prowadzi `BKCaravansBehavior.GetTradeScoreForTown:1377` bez budynkow), a w wanilii AddFactor na bazie 0 daje 0 (blad TaleWorlds). |

## 6. Lojalnosc, wplywy, stabilnosc, kultura

| Budynek | Efekt | Stan | Dowod |
|---|---|---|---|
| Courthouse (miasto, wanilia) | lojalnosc +0.3/0.6/1, wplywy +0.2/0.5/1 | DZIALA | |
| Festival and Games (projekt dzienny) | lojalnosc +3 | DZIALA | |
| Theater (BK) | lojalnosc +0.2/0.5/1, waga kultury pana +5 za poziom | DZIALA | |
| Court House (BK) | stabilnosc +0.02 za poziom | DZIALA | |
| Manor (BK, wies) | wplywy szlachty wsi +15/30/50%, orszak 15/30/45 | DZIALA | |
| Cultural assimilation (BK, projekt dzienny) | "zwieksza obecnosc kultury pana" | MARTWY | `BKBuildings.cs:171-172` - `InitializeDailyProject` z pusta tablica efektow; `BKCultureModel.CalculateCultureWeight:158-190` i BKROT nie sprawdzaja `CurrentDefaultBuilding`; bajtowy grep DLL: `daily_assim` tylko w BannerKings.dll (definicja + obrazek UI). Wybranie projektu = brak projektu. |

## 7. Dobrobyt i przyrost wsi z warowni

| Budynek | Efekt | Stan | Dowod |
|---|---|---|---|
| Housing (projekt dzienny miasta) | dobrobyt +2 | DZIALA | |
| Roads and Paths | przyrost wsi +0.1/0.2/0.3 | DZIALA | VillageHeartsPerDay |
| Irrigation (projekt dzienny) | przyrost wsi +1 (miasto) / +50% (zamek) | DZIALA | |
| Harbor (BK, miasto) / Port (BK, zamek) | dobrobyt +0.3/0.6/1.2 (zamek +0.2/0.5/1) | DZIALA | |
| Harbor (BK, miasto) | sila handlu +0.1 za poziom | DZIALA | |
| Roads and Paths (miasto, zamek) | produkcja wsi +5/10/15% (VillageProduction, AddFactor) | CZESCIOWO | Model gry liczy (`DefaultVillageProductionCalculatorModel:63-65`), ale `MaterialLaw.ProdPostfix -> StartCalibration.ProdStep` (`StartCalibration.Live.cs:142-163`) przy beta >= 0.9995 zastepuje wynik stawka KS z rak i ziemi okregu, bez budynkow. at337: KS zastosowany, 659 okregow, beta 1 wszystkich towarow -> premia Drog martwa prawie w calym swiecie, dziala tylko w krainach wroconych i poza KS. |
| Harbor (BK, miasto) | ryby: poziom x max(dzierzawcy/300, rzemieslnicy/200) na dobe | CZESCIOWO | `BKBuildingsBehavior.RunPorts:405-424`; Armoury `A17Fix.PortsPre:284-291` (Priority.First) pod KS zwraca false. at337: "zablokowane w KS 48 miast, poza KS 34 szt. w 2 miastach". Sprzecznosc z `StartCalibration.Arm:41,214-226` (KsBkPortsInLedger zaklada, ze ryby portow sa praca ludzi). Plan KS nie liczy ryb portow. |
| Port (BK, zamek) | sila handlu +0.08 za poziom | MARTWY (bez konsumenta) | Zamek nie ma rynku: BE `GetTradePower:211` = 0 dla !IsTown; zaden konsument (`BKCaravansBehavior:1322`, `GetProductionEfficiency:296`, `TradeAgreementScoring:92`) nie czyta zamkow. Naprawa niemozliwa bez wymyslania efektu. |
| Port (BK, zamek) | ryby (ten sam wzor RunPorts) | MARTWY - przyczyna inna niz w pierwszej tezie | BK wpina RunPorts tylko w `DailyTickTownEvent`, a gra odpala je wylacznie dla `Town.AllTowns` (`CampaignPeriodicEventManager:238-240`, `Campaign.cs:703-712` - zamki w osobnej liscie). Zamek z Portem nie dostaje ryb nigdy, niezaleznie od KS. |

## 8. Budynki wsi BK

| Budynek | Efekt | Stan | Dowod |
|---|---|---|---|
| Marketplace (wies) | wies kupuje towary miasta; wiekszy przyrost | DZIALA | (petle targu prowadzi `BkSeal34`) |
| Palisade (wies) | tempo rabunku -12/-24/-36% | DZIALA | |
| Farming / Mining (Infrastructure) / Animal Housing / Sawmill / Fish Farm | +5/10/15% produkcji swojej kategorii | MARTWY | Jedyny czytnik `PopulationManager.ApplyProductionBuildingEffect:391-418` wolany tylko z `BKVillageProductionModel`, ktory BK rejestruje tylko przy `!ModCompat.BetterEconomy` (`Main.cs:201-204`); BetterEconomy jest wlaczony. Czynny lancuch NavalDLC -> Default budynkow wsi BK nie zna; warstwa BK `VillageProductionPostfix` dodaje tylko SlavesHardLabor / konie / RitterIronHorses. Okno BK moze pokazywac premie, ktorej nie ma w produkcji. |
| Projekt dzienny Production (wies) | +15% produkcji przy czynnym projekcie | MARTWY (czesc z ziemia dziala) | +15% tylko w nieczynnym `BKVillageProductionModel:134-137` (sprawdza `villageData.CurrentBuilding`, nie CurrentDefault). Czynna jest tylko czesc: `LandData.Update` nie powieksza akrow przy tym projekcie. |
| Mill | chleb +50/100/150% | MARTWY | Mnoznik wagi chleba w `PopulationManager.GetProductions:365-369`, ale chleba nie ma w zadnym `VillageType.Productions`; tick BK (`EconomyPatches.TickGoodProductionPatch:1130-1165`) pyta czynny model Default, ktory dla towaru spoza typu daje 0. at337 `Towary ... bread`: brak pozycji 'wsie'. |
| Bakery | "dodaje chleb do produkcji wsi" | MARTWY | Ta sama droga co Mill; BK dodaje tez 0.1 x zboze bez piekarni (spoza opisu) - rowniez 0. |
| Fur Tannery | dodaje skore (leather) | MARTWY | `GetProductions:294-298`; Default daje 0; at337 leather tylko z rzemiosla miasta; kategoria ksiegi 'garbarnia BK' = 0 trafien. |
| Bee Skeps | miod +1 wagi za poziom (tylko drwale i trapperzy, tak w BK) | CZESCIOWO | Miod bazowy daje Armoury `VillageFruit.Base` ze stala waga 0.5; poziom ula pominiety (`VillageFruit.cs:28` "Ul BK (Skeps) pominiety"). |
| Tax Office (wies) | podatek od szlachty i rzemieslnikow wsi; podatek od zuzycia z targu | MARTWY (czesc celowo) | `BKTaxModel.CalculateVillageTaxes:275-279` -> Armoury `KingdomTreasury.TaxOfficePrefix:776/809` zwraca false przy `PolicyIncomeConserved=true` (celowo: zloto z niczego, zastapione renta). "Podatek od zuzycia z targu" nie istnieje w BK. Oplaty targowe wsi pan juz dostaje w `VillageDuesPerHeadYear` ("court, mill and market dues"). Budynek kosztuje 1500/2500/3500 i nic nie daje. |
| Courier Post | wiadomosci o okolicy bez wzgledu na odleglosc | MARTWY (niedokonczony w BK) | Brak czytnika (BK: tylko definicja i ikona); w grze i BK nie ma filtra wiadomosci po odleglosci, ktory budynek moglby przepuszczac. Uwaga: wedlug weryfikacji Butter Mill Courier moze nie byc na liscie `DefaultVillageBuildings.All` ani `VillageBuildings(village)` - do sprawdzenia, czy w ogole da sie go zbudowac. |

## 9. Budynki gospodarcze zamkow i miast BK, stocznia

| Budynek | Efekt | Stan | Dowod |
|---|---|---|---|
| Mines (miasto) / Castle Mines (zamek) - BK | rudy z lokalnych zloz, poziom = sztuki na losowanie, urobek na rynek za kase miasta | DZIALA (miasta); zamki DO SPRAWDZENIA | RunMines + latki Armoury (`BuildFunding.MinesPrefix/MineRevenuePostfix`). UWAGA: RunMines siedzi w `BKBuildingsBehavior.OnTownDailyTick` (`DailyTickTownEvent`), ktore gra odpala tylko dla miast - Castle Mines prawdopodobnie nie dziala nigdy (patrz Port zamku). Do pomiaru w tescie 338. |
| Retinue Barracks (zamek) - BK | orszak elitarny +1/dobe do 20 x poziom | DZIALA wedlug pierwszego audytu; DO SPRAWDZENIA | jesli wolany z tego samego ticku co RunStuds/RunMines - ta sama watpliwosc; pomiar w tescie 338 |
| Warhorse Studs (zamek) - BK | hodowla koni bojowych do Stash: limit pastwiska/10000 + 2 x poziom, szansa 2%/dobe | MARTWY w praktyce (wedlug pierwszego audytu: CZESCIOWO) | `RunStuds:369-403`: (a) warunek odwrocony - biegnie tylko w zamkach BEZ rancza (42/130 zamkow ROT z ranczem zablokowane), choc `IsBuildingSuitable` uznaje Studs za sensowne tylko Z ranczem; (b) kon z kultury zamku - dla 59/130 zamkow kultur ROT `FirstOrDefault` = null -> NRE w `ItemRoster.OnRosterUpdated` przy udanym losowaniu; (c) kon z niczego; (d) dodatkowo RunStuds jest w `OnTownDailyTick` (tylko miasta) - a wymaga `IsCastle`, wiec prawdopodobnie nie biegnie wcale. Log: 145/145 linii `zamki BK 0`. |
| Shipyard (NavalDLC) | typy kadlubow 1/2/3, limit statkow 9/12/15 | DZIALA | |

## 10. NIEPOTWIERDZONE (bez naprawy w 338)

| Budynek | Teza pierwszego audytu | Dlaczego odrzucona |
|---|---|---|
| Smith (wies BK) - narzedzia | martwy (towar spoza typu = 0) | Tick BK iteruje liste `GetProductions`; tools nie sa towarem KS, wiec ProdStep przepuszcza. Brak 'wsie' w at337 = najpewniej brak zbudowanej kuzni (tylko wsie IronMine). Naprawa zdublowalaby. Do pomiaru: wsie z kuznia > 0, kategoria 'kuznia BK'. |
| Butter Mill (wies BK) | martwy | Nie ma go w `DefaultVillageBuildings.All` ani w `VillageBuildings(village)` - gracz nie moze go zbudowac. Poza zakresem. |
| Mines (wies BK) | czesciowo | Pod KS (beta 1) stawka mineralow z rak i ziemi okregu, poziom Mines nie wplywa na nic - czyli raczej martwy; naprawa musialaby wejsc w stawke KS, gdzie grozi dubel z pracami okregu (`StartCalibration.Inwest.cs`). Osobny audyt. |
| Projekty dzienne Farmland / Pastureland / Woodland (wies BK) | zamiana akrow | To ekspansja akrow (`LandData.Update:418-434` tylko dodaje), nie zamiana; nikt w czynnym lancuchu produkcji wsi nie czyta akrow. Kierunek "martwy" prawdopodobny, ale projekt naprawy byl bledny. Osobny audyt. |
| Port (BK, zamek) - ryby | blokada A17 | Przyczyna inna: zamki nie dostaja `DailyTickTownEvent` - przeniesione do grupy 7 jako MARTWY z nowa przyczyna. |

## 11. Po tescie 30 dob (at338, Armoury-2026-11-06_19-36-12.log) - paczka 338c

Test 338b: kod 0, 23 bledy CS = baza. Dziala: zold zalog (-42 tys. zl/d w 214 warowniach), autowerbunek (565 ludzi), zapas jedzenia magazynu i spichlerza, murarz,
urzad podatkowy miasta, targowisko, asymilacja, drogi, produkcja wsi BK, ule, stadniny.

| Budynek | Stan po tescie 338b | 338c |
|---|---|---|
| Farmlands (zamek) / Warehouse (miasto) - premia BK 'Local farmland' | zdjecie (3b) = ok. 10 100 jedzenia/d mniej, osad z bilansem < 0: 82 -> 172 z 219 - GLOD | DECYZJA: premia BK zostaje (sposob kalibracji modelu jedzenia BK; efekt "jedzenie" zgodny z duchem opisu farm), +6/12/18 gry dla Farmlands tez; klucz `Buildings338FoodDouble` domyslnie NIE |
| Port (BK, zamek) - ryby | tick 338 gotowy, ale w 30 dobach 0 zamkow z Portem zbudowanym ("nasz tick 0 zamkow" - linia mylaca) | linia podaje zamki z Portem na liscie budowy i zbudowane oraz sumy biegu; pod KS port lowi (wiersz nizej) |
| Harbor (BK, miasto) i Port zamku pod KS - ryby | A17 PortsPre blokowal (`zablokowane w KS 48 miast`), Plan KS nie liczyl ryb portow | NAPRAWIONE: blokada zdjeta przy kluczu `Buildings338Ports` (ryby = praca dzierzawcow i rzemieslnikow wzorem BK, zaplata z kasy do kiesy ludu - zawsze, takze bez A17PortFishPaysFolk), ksiega - wlasna grupa "porty BK - praca rybakow (338)", Plan KS - podaz krainy (`PortFishExo`) tym samym wzorem i warunkiem kasy |
| Castle Mines (BK, zamek) | MARTWY potwierdzony: 11 zamkow z kopalnia, tick BK dla zamkow 0 wywolan | NAPRAWIONE: wlasna produkcja z pracy (losowanie BK ze zloza i bogactwa gruntu, tylko z wolnych rak zamku `LandData.WorkforceExcess`, rece na sztuke z wydajnosci gornika KS); kasa zamku stac - place gornikom do kiesy podzamcza, ruda na targ zamku; inaczej - Stash (opis). Klucz `Buildings338CastleMines` domyslnie TAK |
| Mill, Bakery (wies BK) - chleb | MARTWY (odlozone w 338) | NAPRAWIONE: udzial zboza wsi wedlug wagi BK idzie do piekarni (ziarno -> chleb 1:1 w kg); pod KS w stawce A i w Planie (zboze wraca do Planu - Plan liczy chleb ze zboza) |
| Fur Tannery (wies BK) - skora | MARTWY (odlozone) | NAPRAWIONE: udzial skor i futer wsi idzie do garbarni (2.4 kg surowca na kg skory, KsTab.LeatherHide); wies bez skor i futer - nic (licznik "bez surowca") |
| Mines (wies BK) - rudy | pod KS (beta 1) stawka KS bez kopalni - martwy | NAPRAWIONE: rudy z wagi BK (0.5 x poziom x udzial zloza), wartosc zdjeta z innych plonow wsi (inne najwyzej 15%, zboze i ryby 5%) - pod KS w stawce A (Plan widzi), poza KS baza + czynnik plonow |
| Tax Office (wies BK), Courier Post | bez zmian (decyzja) | bez zmian |
