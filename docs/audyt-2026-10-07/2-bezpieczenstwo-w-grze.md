# AUDYT GRUPY "TOWARY 2" (115-119) - SPOJRZENIE 2: BEZPIECZENSTWO W GRZE

Audytor niezalezny (sesja fa2fd7a6), 2026-10-06/07. Tylko odczyt repo; proby w kopiach w tym katalogu
(`SCRATCH\dzien-5\audyt\bezpieczenstwo`, SCRATCH = scratchpad sesji 3cf3e0ac). Kod: `paczki/107-zold-i-skarbiec` ->
`paczki/119-wozy-do-najlepszego-miasta` (diff 2708 linii, kopia `diff.txt`, zrodla obu galezi `src107`, `src119`).
DLL grupy: `dzien-5\zlozenie\Armoury-grupa-towary2.dll` (md5 faade7bf...).

## Wynik w jednym akapicie

Nic, co blokuje wgranie. Wszystkie nowe latki trafiaja w metody o sprawdzonych sygnaturach, zadna klasa z nowymi celami
nie ma konstruktora statycznego czytajacego stan gry (jedyna taka - `DefaultClanFinanceModel` - jest latana dopiero w
`OnSessionLaunched` po `RunClassConstructor`). Kolejnosc latek z cudzymi modami jest bezpieczna: BannerKings zaklada
WSZYSTKIE swoje latki dopiero w `OnGameStart` (po Armoury), a Armoury swoje modelowe w `OnBeforeInitialModuleScreenSetAsRoot`
(po `OnSubModuleLoad` wszystkich modow, w tym BKROTPatch i BetterEconomy) - sprawdzilem doswiadczalnie, ze pozniejsza
latka BK na metodzie wolanej z celow 116 NIE jest omijana (brak wklejenia przez JIT). Zapis i wczytanie sa bezpieczne w
obie strony (takze cofniecie DLL do 107). Koszt: 119 kosztuje ok. 10-25 ms na dobe gry, cala grupa ok. 50-80 ms na dobe
(pomiar na DLL grupy + szacunek kary handlowej gry). Trzy ustalenia DROBNE: (1) zapora "kuznia na dobe" (TrueArmourCost)
polyka place i utrzymanie warsztatu gracza placone z kiesy, a WorkshopTrade liczy je jako zaplacone; (2) kilka zatrzaskow
bledow bez licznika potkniec (wbrew regule z `_tentBroken`), jeden "raz na sesje" zamiast na kampanie; (3) brama
`WorkshopTrade.On` patrzy na ustawienie cen historycznych, nie na to, czy przeliczenie naprawde weszlo.

## Ustalenia

### D1 (DROBNE) - zapora kuzni polyka place warsztatu gracza; WorkshopTrade liczy je jako zaplacone
- Dowod: `Patches.cs:467-470` (galaz 119, kod sprzed paczek) - `TrueArmourCost.SwallowForgeFee` jest prefiksem na
  `GiveGoldAction.ApplyForCharacterToSettlement`; `Patches.cs:334-345`: gdy dajacy to gracz i `InForgeMenu()`, kupuje
  dobe kuzni (`DayPass.EnsureBought`, jesli nieoplacona) i zwraca `false` - przelew NIE nastepuje. `InForgeMenu()`
  (`Patches.cs:312`) = menu oczekiwania `arm_work_wait` / `arm_project_wait` / `bannerkings_wait_crafting` - czas plynie
  (`SmithMenu.cs`: `AddWaitGameMenu` + `StartWait`), wiec doby miast (`DailyTickTown`) biegna w trakcie. `ForgeDayPassEnabled`
  = true w kodzie i w `Armoury.json` Jeffa. Nowe w 116: `WorkshopTrade.PayToTown` (`WorkshopTrade.cs:264-268`) dla
  warsztatu gracza z kapitalem <= `WorkshopTradeLowCapital` (500) placi place cyklu i utrzymanie wlasnie przez
  `ApplyForCharacterToSettlement` i ZAKLADA, ze poszlo (`rec.Purse += amount`, `_dWagePurse/_dKeepPurse += amount`,
  `return amount` - wiec nie ma tez bankructwa).
- Skutek: gdy gracz kuje (czeka w kuzni) w dowolnym miescie, jego warsztaty z pustym kapitalem nie placa plac i utrzymania
  (zloto zostaje w kiesie, kasa miasta nic nie dostaje), a linia "Warsztaty towarowe: ... z kiesy gracza N" pokazuje
  przelew, ktorego nie bylo. Jesli doba kuzni w miescie pobytu akurat wygasla, wyplata warsztatu o polnocy KUPUJE nowa
  dobe kuzni (komunikat "The forge is yours for a full day - N gold"). Zloto nie powstaje z niczego.
- Proba obalenia: sciezka wymaga warsztatu gracza z kapitalem <= 500 (start 2000, BK wyplaca nadwyzke ponad znacznik) -
  rzadka, ale osiagalna (strata kilka dni). `ConvertPostfix` mierzy kiese przed/po, wiec tam problemu nie ma; `SalePostfix`
  uzywa `ApplyBetweenCharacters` (nie latane). Innych latek na `GiveGoldAction` nie ma (skan `src119` i DLL modow).
- Poprawka: w `PayToTown` zmierzyc `owner.Gold` przed i po (jak w `ConvertPostfix`), brak dolozyc z kapitalu albo uznac za
  niezaplacone; albo w `SwallowForgeFee` polykac tylko oplaty, gdy `settlement == Settlement.CurrentSettlement` i
  przelew pochodzi z kuzni.

### D2 (DROBNE) - zatrzaski bledow bez licznika potkniec; jeden zatrzask na cala sesje gry
- Dowod: `RawPrice.cs:98` (`_errFactor`) i `:137` (`_errUse`) - wyjatek idzie do logu raz na kampanie, kazdy nastepny
  znika bez sladu (prefiks na KAZDEJ wycenie ceny); `HistoricalPrices.cs:184` (`_errDefine`) - to samo;
  `CaravanBulk.cs:691` (`_errEarly`) i `:669` (`_errRoute`) - "raz na sesje", `CaravanBulk.Reset` ich nie zeruje, wiec w
  drugiej kampanii tej samej sesji pierwszy blad juz nie trafia do logu; `RawPrice.cs:220-223` (`_errBudget`) - po jednym
  wyjatku budzet BK nie jest juz pytany do konca kampanii (globalny wylacznik, ale tylko dla linii logu).
- Skutek: zadnego zlota ani towaru (bledy cofaja do wzoru gry / zakupu przy wyjezdzie), ale reguly z CLAUDE.md
  ("licz potkniecia, nie gas funkcji") - po pierwszym bledzie nie widac, czy to jeden przypadek, czy kazda wycena.
  MarketCarts, WorkshopTrade i MoneyLedger licza potkniecia poprawnie.
- Proba obalenia: zadna z tych sciezek nie rusza zlota, a linie dnia pokazuja sukcesy ("zakup przed wyborem celu N wizyt")
  - spadek do zera bylby widoczny posrednio; dlatego DROBNE, nie WAZNE.
- Poprawka: licznik potkniec w linii dnia ("Ceny surowcow", "Karawany (przyczyny)"); `_errEarly/_errRoute` zerowac w
  `CaravanBulk.Reset`.

### D3 (DROBNE) - brama 116 na ustawieniu, nie na przeliczeniu
- Dowod: `WorkshopTrade.cs:103` - `On = WorkshopTradeEnabled && HistoricalPrices.On`, a `HistoricalPrices.On`
  (`HistoricalPrices.cs:31`) to samo ustawienie MCM; `RawPrice` uzywa `HistoricalPrices.Applied`. W `ArmouryBehavior`
  (`OnSessionLaunched`, linia ~964) `HistoricalPrices.Apply()` stoi w jednym `try` za `MaterialLaw.Apply()` i
  `ArmsPricing.Build()` - wyjatek w nich pomija przeliczenie, a 116 dalej liczy utrzymanie 4, kapital 2000 i cene z
  zysku na cenach starej monety.
- Proba obalenia: test 06.10 - zero bledow w tym miejscu; `WorkshopLaw` (w grze) ma te sama brame. Stan bledu, nie normalny.
- Poprawka (przy okazji): `On` takze `&& HistoricalPrices.Applied` dla czesci zalezacych od cen (bez zmiany zachowania
  w normalnym starcie: przed `Apply` nie ma tickow).

## Sprawdzone i w porzadku (z dowodem)

1. **Ten sam cel latany kilka razy** (skan wszystkich DLL modow - atrybuty, TargetMethod, latki dynamiczne BK; `kolizje\wynik4.txt`,
   0 nierozwiazanych): `SellGoodsForTradeAction.ApplyInternal` - Armoury prefiks Priority.First (void) + postfiks, BK
   `SellGoodsPatch.Prefix` (bool); `SendVillagerPartyToTradeBoundTown` - Armoury First (bool) + BK `VillagerMoveItemsPatch`;
   `CalculateInventoryCapacity` - Armoury postfiks + BK postfiks (dodaje tylko bohaterom z atutem - bez kolizji);
   `BKItems.InitializeTradeGood` - Armoury First/Last + BKROT prefiks (Normal). Pozostale 21 celow: tylko Armoury.
   Semantyka Harmony 2.4.2 sprawdzona w dekompilacji `0Harmony.dll` (`MethodCreator.AddPrefixes`, `AffectsOriginal`): prefiks z
   parametrem referencyjnym/ref/out albo bool po prefiksie, ktory zwrocil false, jest pomijany - wiec `RoutePrefix` (First,
   false) naprawde pomija prefiks BK, a nasze prefiksy void (First) zawsze biegna przed BK. BK zaklada latki w `OnGameStart`
   (dekompilacja `ore-supply\bk\BannerKings\Main.cs:53-85`, `_patchesInstalled`), wiec przy rownym priorytecie i tak Armoury ma nizszy indeks. Finalizery i postfiksy
   bez `__state` z pominietego prefiksu sa obsluzone (`__state` domyslny -> wyjscie).
2. **Konstruktory statyczne** (pulapka 14.09; `kolizje\wynik4.txt` sekcja 2b): WorkshopsCampaignBehavior, Workshop,
   ChangeOwnerOfWorkshopAction, ChangeProductionTypeOfWorkshopAction, DefaultWorkshopModel, SellGoodsForTradeAction,
   VillagerCampaignBehavior, DefaultTradeItemPriceFactorModel, DefaultSettlementEconomyModel, BKItems, BKCaravansBehavior, modele
   BEE - bez cctor; DefaultInventoryCapacityModel - cctor tylko z `new TextObject`; DefaultClanFinanceModel (cctor czyta
   `Game.Current`) - latana tylko w `ApplyInCampaign` (OnSessionLaunched, `RunClassConstructor`, raz na proces). Nasze klasy
   (RawPrice, MarketCarts, WorkshopTrade, nowe pola MoneyLedger) - inicjatory pol bez stanu gry, kolejnosc inicjacji
   `MoneyLedger._shopWage` bez ryzyka (zadny inicjator nie wola `ClearDay`).
3. **Wklejanie (inlining) przez JIT**: kolejnosc zakladania latek w grze: OnSubModuleLoad wszystkich modow (BKROT, BEE, ...)
   -> Armoury `OnBeforeInitialModuleScreenSetAsRoot` -> BK `OnGameStart` -> `ApplyInCampaign`. Male metody wolane z nowych
   celow i latane przez innych: `Workshop.ChangeGold` (BEE, OnSubModuleLoad - wczesniej), `Town.get_Prosperity` (BKROT,
   OnSubModuleLoad - wczesniej) - bez ryzyka. `ProduceAnOutputToTown` (83 B, latana przez BK POZNIEJ): proba
   `inline-ws` - prawdziwe DailyTickTown na DLL grupy, probnik zalozony PO latkach Armoury liczy 70 wywolan (warsztaty
   notabli) i 28 (warsztat gracza) - nie wklejona. Odwrotnie: BK `TraceVanillaDailyTickParty` lata `DailyTickTown`
   (wola `HandleDailyExpense`, 42 B) w OnGameStart - proba z atrapa tej latki zalozona PO Armoury: 122 z 122 sprawdzen
   recenzenta (prefiks utrzymania dalej dziala). BK `CalculateClanExpensesInternal` (przepuszcza oryginal dla rodu gracza)
   wola `AddPlayerExpenseForWorkshops` - 105 B z obsluga wyjatkow, JIT .NET Framework nie wkleja; BK wola ja tez refleksja.
4. **Transpiler 116** (`GateTranspiler`): obie metody instancyjne, `production` = arg 1 (struct przez wartosc), `workshop` =
   arg 2 (twall `WorkshopsCampaignBehavior.cs:706,776`), stala 200 jedna na metode, etykiety przeniesione; probe recenzenta
   powtorzylem na DLL grupy (122/122).
5. **Wyjatki na sciezkach zlota**: MarketCarts (`Stumble`: licznik + log raz na miejsce; `TopUp` z cofnieciem;
   `SellPostfix` zwraca tylko przy zgodnosci co do denara), WorkshopTrade (`Stumble` w kazdej latce; przelewy parami
   obok siebie), MoneyLedger (`_stumbles`). Pustych `catch` na sciezce zlota w nowym kodzie brak (`catch { }` tylko w
   `CaravanBulk.Players` i `RawPrice.SeedNewCampaign` - rodzaj sesji). `GiveGoldAction` przycina kwote do kiesy dajacego
   (twall `GiveGoldAction.cs:12-14`) - `ConvertPostfix` nie tworzy zlota. Zadnego globalnego wylacznika rozgrywki.
6. **Menu oczekiwania / wizerunki**: nowy kod nie wola `GameMenu.SwitchToMenu` ani nie dotyka wizerunkow na mapie.
7. **Zapis i wczytanie**: jedyny nowy klucz `arm_wstrade` (string) w NOWYM `WorkshopTradeBehavior`. Stary zapis: gra nie
   wola `SyncData` zachowania, ktorego nie ma w zapisie (twall `CampaignBehaviorDataStore.cs:86-104`) - stan z `Reset`
   (konstruktor ArmouryBehavior -> WorkshopLaw.Reset -> WorkshopTrade.Reset). Zapis z wylaczonymi wlacznikami - tylko
   przepisanie zapamietanych wierszy. Cofniecie DLL do 107: rekord stringa w `CampaignBehaviorDataStore` bez typow Armoury,
   `Workshop.InitialCapital` to zwykly int - wczytuje sie. Wiesc z drogi wozow, migawki karawan - bez zapisu (pusty start).
   `Export/Import`: InvariantCulture, 5/6 pol, uszkodzony wiersz pomijany.
8. **Reset w konstruktorze ArmouryBehavior** dla kazdej nowej klasy stanu: RawPrice (przez HistoricalPrices.Reset),
   WorkshopTrade (przez WorkshopLaw.Reset), MarketCarts.Reset (dopisany), nowe pola CaravanBulk (Reset/NewDay), nowe
   tablice MoneyLedger (ClearDay w Reset), nowe slowniki HistoricalPrices (Reset). Dwie kampanie w jednej sesji: latki
   i zatrzaski "na proces" (`_finTried`, `_wired`, `_bkRule`, `_settersTried`) dotycza tylko wpiecia - poprawne.
   Konstruktor biegnie PRZED definicjami przedmiotow nowej kampanii (twall `Campaign.cs:1391` OnGameStart przed `:1398/1524`
   InitializeDefaultCampaignObjects -> DefaultItems -> postfiks BK `InitializeAll` -> `BKItems.Initialize`).
9. **Start nowej kampanii - kolejnosc** (twall `CampaignEvents.cs:2078-2086`, `Campaign.cs:1690-1694`): definicje BK
   (OnGameStart) -> `OnNewGameCreated` (BK AdjustPrices, warsztaty `InitializeWorkshop` w starej monecie, 100 tickow
   follow-up, wygladzanie rynku w `FollowUpEnd`) -> `OnSessionLaunched`: HistoricalPrices.Apply -> StartStock.Run ->
   RawPrice.SeedNewCampaign -> (WorkshopTradeBehavior, dodany po ArmouryBehavior) ApplyInCampaign + WorkshopTrade.SeedNewCampaign.
   Zgodne z zalozeniami 115/116/118; `RunTownShopsAtGameStart` nie rusza kapitalu (GameStarted = false), wiec seed 116 obejmuje
   wszystkie warsztaty.
10. **Teksty w grze**: rozmowa kupna (`OfferText`), komunikat sprzedazy (`Log.Player`), `_txtCart`, wszystkie opisy MCM
    nowych ustawien - po angielsku.
11. **Settings.cs vs McmSettings.cs**: `tools/gen_mcm.py` na kopii drzewa 119 (`tree119`) - 631 ustawien, McmSettings.cs
    Armoury/RC/GT bez zmian (md5 przed = po); kazde z 19 nowych ustawien dokladnie raz w `ApplyTo`; zakresy MCM a klamry w
    kodzie zgodne (np. FillLimit 0..4 -> klamra 0.8..1, ResaleShare 0..3.2 -> 0..1).
12. **Petle / rekurencja**: `MarketCarts.Seg` (polowienie, glebokosc log n), `SumRun` <= 24, petla zakupu karawan z
    warunkami wyjscia, `Choose` po 97 miastach - skonczone. Przekierowanie co godzine (HourlyTickParty) - te same warunki
    wykluczenia (oblezenie, wojna) co w grze, wiec wybor nie wraca w kolko.

## Koszt (pomiar na DLL grupy)

Proba `koszt` (kopia proby recenzenta wozow): prawdziwe Harmony 2.4.2, prawdziwe latki BK (SellGoodsPatch,
VillagerMoveItemsPatch), latki ceny Armoury jak w grze (MarketGlut, SupplyDemand, ScrapFloor, RawPrice z przelicznikami z
logu 06.10), swiat z PRAWDZIWEJ geometrii ROT (97 miast, 571 wsi z `settlements.xml`, droga = prosta x 1.25 - srednia
odleglosc do najblizszego miasta 70.8 jak w opisie 119). Kara handlowa gry - atrapa (jak u recenzenta).

| pomiar | wynik |
|---|---|
| miast w zasiegu 250 na wies | 9.1 (droga x1.25), 13.3 (prosta); opis 119 zakladal 30-40 |
| wybor miasta (`Choose`) | 15.3-17.9 us na decyzje, 70-94 cen z modelu na decyzje |
| korekta ceny sprzedazy (`SellPostfix`) | 13.8 us i 110 cen na sprzedaz (571 sprzedazy, 62 882 sztuk, niezgodne 0) |
| jedna cena (model gry + latki Armoury) | 70 ns z prefiksem 115, 52 ns bez; `GetBasePriceFactor` 33 vs 17 ns |
| narzut BKROTPriceModel na cene (prawdziwy BKROTPatch.dll) | 74 ns (159 ns przy wlaczonej diagnostyce BkrotPerf) |

Na dobe gry (171 kursow wozow - z logu 06.10: 78 wyjazdow dziennie z 260 wsi zamkowych): ok. 31-35 tys. cen z 119.
Cena w grze = 70 + 74 ns + kara gry dla taboru bez bohatera (NIEZMIERZONA, szacunek 0.1-0.4 us) = ok. 0.25-0.55 us ->
119: ok. 10-25 ms na dobe. 115: +17 ns na kazda wycene bazowa (przy 1-2 mln wycen na dobe - szacunek autora 115 - ok.
17-34 ms). 117, 116, linie dnia RawPrice: szacunek ponizej 20 ms razem. Calosc ok. 50-80 ms na dobe gry - ponizej progu
300 ms z opisu 119. Linia "Dowoz (wozy)" wypisuje zmierzone ms - po tescie porownac.

## Pliki prob (ten katalog)

- `kolizje\` - skan latek wszystkich modow i wolanych metod (`wynik4.txt`), program `Program.cs`.
- `inline-ws\` - proba wklejania: kopia proby warsztatow recenzenta + probnik "latki pozniejszej" (`out-b`, `out-c\run.txt`).
- `koszt\` - pomiar kosztu (`out\run-1.25.txt`, `out\run-1.0.txt`), `geo.py` (miasta w zasiegu z settlements.xml).
- `bkrot-bench\` - narzut BKROTPriceModel (`wynik.txt`).
- `tree119\` - kopia drzewa 119 (git archive) do proby generatora MCM; `src107`, `src119`, `diff.txt`, `dec\` - odczyt.
