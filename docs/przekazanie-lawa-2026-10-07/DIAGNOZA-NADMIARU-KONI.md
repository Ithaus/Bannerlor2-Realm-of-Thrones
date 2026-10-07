# Diagnoza nadmiaru koni na targach (paczka 7c) - sesja 2026-10-07_12-15-40 (autotest, 40 dob)

Tylko odczyt. Zrodla: Armoury-2026-10-07_12-15-40.log (porownawczo 03-18-52), Logs\2026-10-07_12-15-40\zakupy.log / bitwy.log / ludzie-regiony.csv,
kod Armoury\src, dekompilacja TaleWorlds.CampaignSystem 1.4.8, BannerKings.dll, BKROTPatch.dll, BetterEconomy.dll, NavalDLC.dll, XML (ROT-Map settlements,
ROT-Content items, SandBoxCore horses_and_others), CrashScribe\items-dump.csv, BetterEconomy\bee_log.txt. (Raport agenta badawczego 07.10, zapisany przez sesje glowna.)

## 0. W skrocie
- ZRODLO: 71 wsi-stadnin (20 europe, 15 desert, 13 steppe, 10 vlandian, 7 sturgian, 6 battanian) produkuje ok. 333 zwierzat typu Horse na dobe; wozy
  (VillageCartWholeStore, poprawka 122) zabieraja CALY magazyn - 100% trafia na targi, hamulec magazynu wsi (1.5 x W) nie dziala. Do tego juczne "z niczego"
  przy kazdej nowej karawanie i odtworzonym taborze wsi (vanilla).
- ODPLYW: Stajnia AI kupuje 3.2 konia/dobe (127 w 40 dobach), od hodowcow 0. Vanilla PartiesBuyHorseCampaignBehavior dziala, ale przy KUPNIE martwa (srednia cena
  "konia" ok. 11 000 przez smoki, jednorozce, rydwany ROT); przy SPRZEDAZY dziala i oddaje konie na targ. Miasta zjadaja konie ze stalego budzetu w zlocie, ktory
  prawie nie rosnie z nadmiarem.
- BRAK UBYTKU: smierc jazdy AI nie zabiera koni (konie wirtualne), lup AI to zloto zamiast przedmiotow, BK wylacza dzienne usuwanie kulawych sztuk (DeleteOverproducedItems).
- WYNIK: polki (miasta i zamki, ItemType.Horse) 2 305 (doba 0) -> 5 308 (13) -> 7 484 (26) -> 9 186 (39): +176/dobe srednio (+231 w dobach 0-13, +167 w 13-26,
  +131 w 26-39); czynnik polki dla AI spada z 0.73-1.0 do 0.46-0.55 wartosci. Sesja Jeffa 03:18: 2 200 -> 5 638 w 18 dobach (+191/dobe).

## 1. Pomiary
- Spis polek (ArmsPricing.Census, ItemType.Horse - takze juczne, wielblady, rydwany, slonie): Horse 2 305 / 5 308 / 7 484 / 9 186; HorseHarness 1 093 / 480 / 345 / 305;
  wszystkie sztuki 21 714 / 39 904 / 76 367 / 102 900 (doby 0 / 13 / 26 / 39).
- Stajnia AI (zakupy.log "Stajnia AI"): 73 zakupy, 127 koni, 87 625 zl (ok. 690/szt.), "wsiom-hodowcom" 0. Rasy: empire 50, aserai 43, khuzait 11, battania 7,
  t2_empire 7, sturgia 2, mieszane 7 - zawsze najtansze palfreye t3/t4; kupuja tylko pod awanse ("czekalo na awans" razem 115, "mial" 89). Konie pod siodlo na
  polce przy zakupie: pierwsze 15 zakupow srednio 19.3, ostatnie 15 srednio 49.8, max 93 (King's Landing). Cena/wartosc w miastach: na poczatku 0.73-1.02, pod
  koniec 0.46-0.55; we wsiach (Sun Ranch, Tor Stables, Horse Valley) zawsze 1.00 (PriceOf bez Town oddaje Value).
- Inne: pasery - zwierzeta sprzedane przez bandy 311 w 40 dobach (7.8/dobe); rozbite tabory wsi 198 (4.95/dobe, 97 przez bandy); karawan 358 -> 807 (+449 netto),
  pokonanych 81; bitwy srednio 545 zabitych i 511 rannych na dobe. Kasy miast na dobe: taborom wsi -356 tys., "zakupy" mieszkancow +332 tys., lordowie miastom
  +75.8 tys., miasta lordom -125.5 tys. Modele czynne: gospodarka osad DefaultSettlementEconomyModel (vanilla), produkcja wsi NavalDLCVillageProductionCalculatorModel
  nad Default; BetterEconomy (tryb zgodnosci z BK) i BK nie rejestruja swoich modeli.

## 2. Skad sa konie
1. Wsie-stadniny (glowne zrodlo): produkcja vanilla (DefaultVillageTypes.AddProductions) x (GetHearthLevel()+1) x 0.5; ogniska stadnin z ROT-Map 357-426 (poziom 1,
   mnoznik 1.00-1.10); wola je prefiks BK TickGoodProduction. Na dobe: europe 20 wsi x 5.07 = 109.0; vlandian 10 x 4.98 = 54.8; desert 15 x 3.63 = 54.5;
   steppe 13 x 3.25 = 42.3; sturgian 7 x 5.70 = 39.9; battanian 6 x 5.49 = 32.9; RAZEM ok. 333. Wedlug kategorii: horse (palfreye t3/t4, 929-1 258) ok. 148
   + camel 4.5; war_horse (t2_* 1 221-2 380 ok. 34, hunter 5 980 9.1, charger 10 973 9.1, war_camel 1.2) razem ok. 53; noble_horse (t3_*, 10 000) ok. 5;
   sumpter_horse (sumpter, mule, saddle_horse, old_horse, pack_camel; 99-140) ok. 122 (old_horse ma is_merchandise=false, a i tak jest produkowany i sprzedawany).
   Wartosc nominalna ok. 450 tys. zl/dobe.
2. Wozy oddaja wszystko: MarketCarts.cs (122, VillageCartWholeStore, TopUpPlan capped=false, CapFloor) - woz bierze caly magazyn, zwierzeta bez wagi; sprzedaje BK
   SellGoodsPatch (wszystko poza 0.5 x ludzie + 2 najtanszych jucznych). Wczesniej magazyn 1.5 x W zatrzymywal produkcje wsi - jedyny hamulec dopasowujacy hodowle
   do zbytu; teraz go nie ma.
3. Juczne z niczego (vanilla): CaravanPartyComponent.InitializationArgs - nowa karawana dostaje 0.5 x zaloga najtanszego jucznego (ok. 12 na karawane; +449 karawan
   = ok. 5 400 sztuk w 40 dobach, ok. 135/dobe) + odtworzenia po rozbiciu; VillagerPartyComponent.InitializeVillagerPartyProperties - odtworzony tabor 0.5 x ludzie
   (ok. 55/dobe). Na polke: karawany sprzedaja juczne ponad 0.6 x zaloga, zwyciezcy rozbitych karawan i taborow przejmuja ladunek, lordowie oddaja juczne zawsze
   (PartiesSellLootCampaignBehavior), bandy przez paserow; najazd (DefaultRaidModel) ma w puli "mule".
4. Lordowie: PartiesBuyHorseCampaignBehavior.OnSettlementEntered (galaz sprzedazy) - gdy wartosc wierzchowcow w jukach > 10% z min(100 000, zloto), sprzedaje do 10
   najdrozszych na wizyte; do tego caly niejadalny lup.
5. NIE sa zrodlem: lup z poleglych w bitwach AI (zwyciezca AI dostaje zloto, przedmioty tylko gracz); BK Warhorse Studs (zamki bez stadniny, do skrytki); ColdStart.

## 3. Dokad znikaja
1. Stajnia AI (Stables.OnSettlementEntered): ok. 3.2/dobe - tylko pod czekajace awanse (+2 zapasu), co 4 doby, max 10, 25% polki, zostawia 4; tylko IsPlainMount.
2. Vanilla PartiesBuyHorse - nie wylaczona i nie podmieniona (zaden DLL w Modules jej nie rusza), ale kupno martwe: CalculateAverageHorsePrice liczy srednia po
   WSZYSTKICH przedmiotach kategorii horse (palfreye, konie turniejowe, 6 rydwanow po 22 000, 12 smokow do 71 006, 3 jednorozce po 30 000, slon, mamut...) = ok. 11 000;
   warunek avg x mounts / min(zloto, 100k) < 0.08 => kupuje tylko lord bez zapasowego wierzchowca (Stajnia trzyma zapas +2, wiec prawie nigdy).
3. Konsumpcja miast (vanilla ItemConsumptionBehavior przez prefiks BK, budzet w zlocie): budzet kategorii = BaseDemand x dobrobyt + LuxuryDemand x (dobrobyt - 3000);
   przy sredniej 4 793: horse ok. 671 zl/miasto (65 tys. na 97 miast), war_horse 611 (59 tys.), noble_horse 665 (65 tys.), sumpter 101 (9.8 tys.). Cena zwierzat ledwo
   reaguje na nadmiar (wykladnik 0.3 dla IsAnimal): 10 x wiecej koni = cena tylko o polowe nizsza. Pulapka: budzet kategorii schodzi na PIERWSZY stos od konca rostera
   - gdy to rydwan za 22 000, miasto zjada 0.03 sztuki. Szacunek przepustowosci: palfreye 60-110/dobe, wojenne 15-40, szlachetne 5-8, juczne 100-200; zamki (dobrobyt
   ok. 1 100) maly budzet.
4. Awanse AI (Stables.PayInHorses) zuzywaja konie kupione przez Stajnie albo zdobyte.
5. Brak ubytku: konie jazdy AI wirtualne (smierc jezdzca nic nie zabiera); juki pokonanych przechodza do zwyciezcy; BK zwraca false w DeleteOverproducedItems (znika
   vanilla 5%/dobe ubytku sztuk z modyfikatorem); FieldCraft.HorseDeathPermanent tylko kon bohatera gracza.
6. SupplyDemand.DailyTrade tylko przewozi nadwyzke miedzy osadami - nic nie ubywa.

## 4. Bilans (sztuki na dobe; M = zmierzone, S = szacunek z kodu)
Doplyw: wsie-stadniny ok. 333 (S); juczne z niczego ok. 190 do partii (S), czesc wycieka na polki; pasery 7.8 (M); lordowie - nieznane. RAZEM na polki ok. 380-430 (S).
Odplyw: Stajnia AI -3.2 (M); kupno vanilla ok. 0 (S); konsumpcja miast ok. -200..-250 (z przyrostu; przepustowosc wzoru 180-350). NETTO +176 (M), +199 w pierwszych 26 dobach.

## 5. Co sie pietrzy
1. Palfreye (kategoria horse t3/t4) - 44% produkcji; kupuja je tylko Stajnia (3/dobe) i budzet miast; polki pod siodlo 19 -> 50-93.
2. Juczne (sumpter, mule, saddle, old_horse, pack_camel) - wsie i spawny karawan/taborow; wylaczone z kazdego kupca Armoury.
3. Wojenne t2, hunter, charger - budzet war_horse rozchodzi sie ulamkowo na drogie sztuki.
4. Wielblady - ok. 10/dobe w miastach pustynnych; Stajnia je pomija.
5. t3_* (10 000) nie pietrza sie ilosciowo, ale siedza w koszyku t4 z palfreyami - wyceniane po podlodze.

## 6. Dlaczego AI placi polowe
SupplyDemand.Factor: popyt koszyka (typ x tier) = 4 x dobrobyt/3000 (0.3-3) x waga tieru (t3 0.7) + zamowienia, ok. 5.6 dla typowego miasta - bez zwiazku z produkcja
wsi; przy ok. 30 sztukach t3 na polce ((5.6+1)/31)^0.5 = 0.46, z marza 1.1 = 0.5 wartosci; podloga 0.25 (0.275 z marza) przy ok. 105 sztukach.

## 7. Poprawki
- POPRAWKA 0 - pomiar (najpierw, 1 test): dzienna linia "Konie:" (produkcja z OnItemProducedEvent wedlug kategorii i rasy; sprzedaz wozow; konsumpcja miast; spawny
  jucznych - postfiksy na dwoch InitializationArgs; kupno i sprzedaz lordow; spis polek wedlug kategorii). Zamyka wszystkie "S".
- POPRAWKA 1 - hodowla wedle popytu (zrodlo): postfiks na CalculateDailyProductionAmount czynnego modelu produkcji wsi (lista podklas juz w MaterialLaw.cs:213, wzor
  dobowy w VillageWoodlot.cs) - dla ItemType.Horse mnoznik nasycenia targu TradeBound clamp(1 - (polka + w drodze - cel)/cel, 0, 1), cel = k x SupplyDemand.Demand
  (k = 2). Prosciej: w MarketCarts (WholeStore) wozy nie biora koni ponad miejsce na polce celu - wraca hamulec magazynu i stadnina sama staje. Szybki wlacznik
  HorseRanchOutput (np. 0.5) obetnie 333 do ok. 165 od razu.
- POPRAWKA 2 - konie padaja z jazda AI, Stajnia kupuje na remonty (odplyw): przy koncu starcia (MapEventEnded; hak juz w PeopleLedger.cs:104 i BattlefieldLaw.cs)
  policzyc stracone wierzchowce (polegli konni + np. 30% rannych konnych) jako "dlug remontowy" partii; w Stables.NeedForUpgrades / OnSettlementEntered dodac go do
  want. Przy 545 zabitych i 511 rannych na dobe i 20-25% jazdy: ok. 100-150 koni/dobe popytu (mniej wiecej nadwyzka). Opcjonalnie: dzienne odpadanie 5% sztuk z
  modyfikatorem na polce (w miejsce wylaczonego przez BK DeleteOverproducedItems).
- POPRAWKA 3 - juczne dla karawan i taborow z targu: postfiksy na CaravanPartyComponent.InitializationArgs i VillagerPartyComponent.InitializeVillagerPartyProperties
  - zdjac zespawnowane juczne i przeniesc N sztuk sumpter z polki miasta macierzystego, placi kiesa karawany / wsi do kasy miasta; gdy brak - reszta ze spawnu.
  Efekt: ok. 190/dobe jucznych przestaje powstawac z niczego.
- HIGIENA CEN: (a) postfiks na PartiesBuyHorseCampaignBehavior.CalculateAverageHorsePrice - srednia tylko z IsPlainMount i merchandise (ok. 1 100); kupno vanilla ozywa
  (prog kupna 8% < prog sprzedazy 10% - bez pompy); (b) rozdzielic koszyki SupplyDemand dla koni wedlug ItemCategory (sumpter, horse, war, noble) zamiast Tier;
  popyt na konie z rzeczywistej jazdy.

## 8. Niepewnosci
Produkcja 333/dobe - szacunek (perki, polityki NavalDLC, najazdy nieliczone); konsumpcja miast w sztukach tylko ze wzoru i zalezy od stosu na koncu rostera; udzial
jucznych w spisie "Horse" nieznany (z polek pod siodlo w logu Stajni ok. 70-80% to konie pod siodlo) - rozstrzyga Poprawka 0.
UWAGA do 7b (zwierzeta w cenach historycznych): jesli zmienia sie Value zwierzat, budzet miast w zlocie trzeba przeliczyc tym samym przelicznikiem
(HistoricalPrices.DemandPostfix), inaczej konsumpcja w sztukach zmieni sie razem z cena.
