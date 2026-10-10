# C1 - postep (etap 2, krok C1: 165 + 182 + 166/162m + 185)

Galaz `w-toku/e2c` (od `w-toku/e2b` + 177-fix ae01f8e). Projekt: `docs/PROJEKT-ETAP2-BANKRUCTWA-2026-10-09.md` (repo glowne), rozdz. 2.0b, 165, 166 + 162m,
185, 182 i "Odpowiedzi Jeffa (09.10 ok. 11:05)". Gra nie uruchamiana, folder gry nietkniety; kazdy kawalek: build Release kod 0 -> commit -> ten plik.

Kolejnosc kawalkow: 1. 165 -> 2. 182 -> 3. 166 + 162m -> 4. 185.

---

## Kawalek 1: 165 korona z biezacych wplywow - ZROBIONE (commit 50bc17b)

**Pliki:** nowy `Armoury/src/CrownIncome.cs`; `KingdomTreasury.cs` (Daily, Levies, WageRefund, liczniki na krolestwo), `SoldierPay.cs` (zalogi bez
zwrotu przy 165), `MenPurse.cs` (K3: wydatki ludzi we wlasnym miescie rodu), `ClanIncomeBook.StableD.cs` (API `StableLand`, `StableDWithDays`),
`ArmouryBehavior.cs` (kolejnosc dobowa, Reset, SyncData `arm_crown165`, EnsureHooks), `SubModuleMain.cs`, `MoneyLedger.Obieg.cs` (linia "Obieg"),
`Settings.cs` + `McmSettings.cs` (gen_mcm), `tools/gen_mcm.py` (zakresy suwakow).

**Co dziala (przy CrownCurrentIncome = true I ClanBudgetEnabled = true; reszta - zachowanie sprzed paczki):**
- Wplywy dnia krolestwa = zmiana skarbca od wczorajszego zamkniecia korony (gra, BK, Diplomacy, splata dlugu wobec korony, podatek gry od bogatych
  rodow) + nasze liczniki dnia (powinnosci, danina wojenna, clo, 1/3 zaworu zamkow 114) - netto; + 1/`CrownReserveReleaseDays` (360) zapasu ponad
  `CrownReserveGold` (500 000); zapas = skarbiec minus wplywy dnia. Do wydania = min(skarbiec, wplywy + 1/360).
- Kolejnosc w ArmouryBehavior (kazdy krok we wlasnym `try`): powinnosci -> danina/clo -> `CrownIncome.Begin` -> [dary 182] -> `CrownIncome.Reparations`
  -> [kontrakty 185] -> `WageRefund` -> `CrownIncome.End` (migawka skarbcow, linia) -> KingdomLedger.
- Zwrot 50%: tylko partie w polu (zalogi nie trafiaja do podstawy - SoldierPay), minus `CrownRefundOwnTownsCut` (1.0) wydatkow "na zycie" ludzi partii
  we wlasnych miastach rodu (K3, takze gracz - jedna regula); placony z reszty wplywow dnia, proporcjonalnie; niedoplata przepada.
- Reparacje Diplomacy: prefiks na `Diplomacy.Costs.KingdomWalletCost.ApplyCost` (portfele Reparations/Reparations) - zamiast Diplomacy dlug korona A ->
  korona B; rata najwyzej `CrownReparationShare` (0.5) wplywow dnia A (na wszystkie raty A razem), B dostaje dokladnie rate do skarbca i do wplywow dnia.
  Rody nie placa, `TributeWallet`/`DebtToKingdom` z reparacji nie powstaje; krolestwo zniklo - dlug przepada (licznik). Okno ksiegi obiegu (169b,
  Priority.First) biegnie przed nami i widzi skarbce bez zmian.
- Splata `DebtToKingdom` (gra `AddPaymentForDebts`, stary zapis) - do skarbca krolestwa rodu (dotad w nicosc). Latka zakladana w kampanii (pulapka
  konstruktora statycznego `DefaultClanFinanceModel` - jak SoldierPay).
- Clo jednym poborem (`CrownCustomsSingleTake`, D-7): z licznika cel prosto do skarbca, kasa miasta nie placi drugi raz; ksiega: `LastCustomsTaken` = 0
  (nic nie znika - licznik cel jest posiadaczem swiata), osobny licznik `LastCustomsSingle`.
- Powinnosci od czesci "ziemia" D stalego (`CrownDuesFromLand`, 2.6); rod bez pomiaru D stalego - stara podstawa (licznik w linii powinnosci).

**Nowe klucze MCM (grupa "The crown's income (stage 2)"):** `CrownCurrentIncome` true, `CrownReserveGold` 500000, `CrownReserveReleaseDays` 360,
`CrownRefundOwnTownsCut` 1.0, `CrownReparationsRealm` true, `CrownReparationShare` 0.5, `CrownCustomsSingleTake` true, `CrownDuesFromLand` true,
`ClanBudgetEnabled` **false na tym etapie** (166 jeszcze nie ma - 165 nieczynne; kawalek 3 wlacza domyslnie).

**Nowe / zmienione linie logu:**
- NOWA `Korona: wplywy dnia (165): dzien N | wplywy dnia X (nasze: powinnosci, danina wojenna, clo, 1/3 zaworu zamkow A; inne do skarbcow od wczoraj ... B) + 1/360 zapasu ponad 500000 C = do wydania D | wydane z wplywow: dary (182) ..., raty reparacji ... (przyjete ...), kontrakty najemnikow (185) ..., zwrot zoldu G (nalezny H, wyplacone P%) | zostalo z wplywow dnia w skarbcach ... | dlugi reparacji: ... | splata dlugu wobec korony do skarbcow (stary zapis) ... | krolestwa bez wczorajszej migawki ... | reparacje Diplomacy przechwycone: TAK/BRAK, splata dlugu wobec korony: TAK/BRAK | na krolestwo (wplywy+zapas, wydatki): ...`
- NOWA (przy pokoju z reparacjami) `Korona: reparacje (165) - A jest winne B N zl; ...` (+ komunikat graczowi po angielsku, gdy dotyczy jego krolestwa).
- NOWE (start) `CrownIncome (165): korona z biezacych wplywow - reparacje Diplomacy ...` i (kampania) `CrownIncome (165): splata dlugu wobec korony (AddPaymentForDebts) do skarbca wpieta|BRAK.`
- ZMIENIONA `Korona: dzien N - zwrot zoldu ze skarbcow krolestw w wojnie (165: z wplywow dnia i 1/360 zapasu, partie w polu bez zalog, minus wydatki ludzi we wlasnych miastach rodu X): ...` (dopisek tylko przy 165; "wplywow dnia nie starczylo" zamiast "skarbiec nie mial dosc").
- ZMIENIONA `Korona: dzien N - powinnosci wasali ...; podstawa (165): czesc ziemia D stalego u N rodow, stara ... u M.`
- ZMIENIONA `Korona: dzien N - danina wojenna ... (do skarbcow krolestw; 165: clo raz - z licznika cel prosto do skarbca X, kasy miast nie placa drugi raz); ...`
- ZMIENIONA `Obieg: dzien N` (czesc korona): `; 165: wplywy dnia X + 1/360 zapasu Y = do wydania Z, raty reparacji skarbiec -> skarbiec ..., splata dlugu wobec korony z kies rodow do skarbcow ..., clo jednym poborem (licznik cel -> skarbiec) ..., zwrot przyciety o wydatki ludzi we wlasnych miastach (K3) ...`.
- Bez zmian, a potrzebna do testu: `Korona: niedoplata 28 dob (169c): swiat X% (dany / nalezny; wyplacone Y%)`.

**Odstepstwa od projektu (z powodem):**
1. `CrownWageRefundGarrisons` - projekt: domyslna false. Zrobione: domyslna zostaje true, ale dziala tylko przy wylaczonym 165; przy 165 zalogi nigdy
   nie sa podstawa zwrotu (regula 3). Powod: "wylacznik glowny = stan sprzed paczki" (zmiana domyslnej zmienialaby zachowanie takze przy 165 wylaczonym).
2. `CrownDebtToClans` - klucza nie dodalem (projekt: "kod bez tej galezi do slowa Jeffa") - martwy klucz w MCM nic by nie robil.
3. "Wplywy dnia" mierzone zmiana skarbca od wczorajszej migawki (netto) + nasze liczniki, zamiast sumy wymienionych pozycji osobno: lapie wszystko, co
   naprawde weszlo (podatek gry/BK od bogatych rodow, trybut, splate dlugu), bez listy zrodel do utrzymania; wydatki skarbca spoza nas (Diplomacy, gra)
   zmniejszaja wplywy dnia (netto). Pierwsza doba po wczytaniu starego zapisu (bez migawki) - tylko nasze liczniki (licznik w linii).
4. Kolejnosc daru i raty reparacji: obie liczone od wplywow dnia bez rat przyjetych (rata przyjeta dochodzi do reszty odbiorcy - na zwrot i kontrakty).
5. `ClanBudgetEnabled` w tym kawalku domyslnie false (165 nieczynne), zeby drzewo po kawalku 1 nie wlaczalo 165 bez 166 (R1).

**Nie zrobione w tym kawalku:** dary (182), kontrakty (185), nagroda za wielkiego jenca (178, krok D), renty (180, krok C2), podatek wojenny 184 (etap 5) -
miejsca w kolejnosci sa (`CrownIncome.Spent/Received/DayOf`).

**Ryzyka dla testu:** (a) wplywy netto moga byc zanizone w dobie, gdy Diplomacy/gra wyda ze skarbca duzo (np. koszt dyplomacji) - widac w "inne do skarbcow
od wczoraj" < 0; (b) `AddPaymentForDebts` - JIT moglby wkleic metode (mala) - wtedy "splata dlugu wobec korony: BRAK" nie, ale licznik 0 przy rodach z dlugiem;
(c) dlugi reparacji rosna tylko przy pokojach Diplomacy z odszkodowaniem (rzadko - test: suma przyjeta = suma zaplacona co do 1 zl w linii).

---

## Kawalek 2: 182 dary miedzy koronami, Straz bez zoldu - ZROBIONE (commit d4d9c0b)

**Pliki:** nowy `Armoury/src/CrownGifts.cs`; `MountedWage.cs` (zold Strazy 0), `ArmouryBehavior.cs` (krok po `CrownIncome.Begin`, Reset), `MoneyLedger.Obieg.cs`,
`Settings.cs` + `McmSettings.cs`.

**Co dziala:**
- Dary tylko przy 165 (`CrownGifts` i `CrownIncome.On` - dar to udzial w "wplywach dnia"). Polnoc -> Straz: `GiftNorthToWatchShare` (0.25) x do wydania
  Polnocy dzis (wplywy + 1/360). Wolne Miasta -> Dothrakowie: kazde `GiftFreeCitiesToDothrakiShare` (0.10) swoich. Najwyzej reszta wplywow dawcy.
- Dar: skarbiec dawcy -> skarbiec odbiorcy -> tego samego dnia glowy rodow odbiorcy wedlug wag (Straz: miasto/zamek 1, wies 0.25, rod bez lenna 0.5;
  Dothrakowie: rowno na rod); rody: bez najemnikow, dworzan BK i Innych. Reszta z zaokraglen zostaje w skarbcu odbiorcy. Do D (czesc "korona":
  `ClanIncomeBook.NoteInflow(..., KCrownLevies)`). Nie ma komu dac - dar nie wychodzi.
- Id krolestw ROT (z `balans-krolestw.csv` i `budzet-rodow.csv` biegu e2b120): Polnoc `battania`, Straz `nightswatch`, Dothrakowie `khuzait`, Wolne
  Miasta `bravos`, `volantis`, `pentos`, `myr`, `lys`, `tyrosh`, `norvos`, `qohor`, `nord` (Lorath - kultura `nord`). Pierwsza linia doby
  "Dary koron (182): krolestwa - ..." pokazuje, ktore znaleziono.
- `WatchUnpaid`: w kontekscie zoldu partii (`GetTotalWage` - nowy znacznik `_total` w MountedWage) zold jednostki w partii/zalodze Strazy (MapFaction
  `nightswatch`, takze gracz w Strazy) = 0. Cena werbunku i stawka nominalna (`MountedWage.Nominal` - do pulapu w ludziach 166) bez zmian. Czynne tylko z
  `ClanBudgetEnabled` (bez pulapu w ludziach werbunek Strazy nie mialby hamulca - zold 0 = limit zoldu nigdy nie wiaze).

**Nowe klucze:** `CrownGifts` true, `GiftNorthToWatchShare` 0.25, `GiftFreeCitiesToDothrakiShare` 0.10, `WatchUnpaid` true.

**Nowe / zmienione linie logu:**
- NOWA `Dary koron (182): krolestwa - Polnoc (battania) ..., Straz (nightswatch) ..., Dothrakowie (khuzait) ..., Wolne Miasta N z 9 (...)` (raz na sesje).
- NOWA `Dary koron (182): dzien N | Polnoc -> Straz X zl do N rodow (wagi stale: ...) | Wolne Miasta -> Dothrakowie Y zl do M rodow (rowno na rod) | ze skarbcow dawcow zeszlo A, do rodow doszlo B, w skarbcach odbiorcow zostalo (zaokraglenia) C | szczegoly: ... | Straz bez zoldu (WatchUnpaid): TAK/NIE.` - test "dary co do 1 zl": A = B + C.
- `Korona: wplywy dnia (165)`: pozycja "dary (182) X (przeszly do rodow odbiorcow Y)" i na krolestwo "dar -X / dar +Y".
- `Obieg: dzien N` (korona): "dary (182) skarbce dawcow -> glowy rodow Strazy X i Dothrakow Y (ze skarbcow dawcow A, reszta w skarbcach odbiorcow C)".

**Odstepstwa:**
1. `GiftSplitFixedWeights` - klucza nie dodalem: jedyna alternatywa (wedlug liczby ludzi) jest zakazana przez Z8, wiec klucz bylby martwy.
2. Regula korekty 10% -> 15% (Dothrakowie biedni po 40/120 dobach) - nie automatyczna: suwak `GiftFreeCitiesToDothrakiShare` (opis MCM mowi o 0.15),
   decyzja przy tescie (projekt: "udzial rosnie do 15% i test sie powtarza").
3. Dar liczony od "do wydania" dawcy (wplywy + 1/360) przed ratami reparacji przyjetymi tego dnia (kolejnosc 2.0b: dary przed ratami).
4. Lista Wolnych Miast z id krolestw (nie z kultury): bunty Wolnych Miast (`new_kingdom*`, np. "Volentin League") nie placa. `KingdomBalance.FreeCities`
   (175) ma "lorath" zamiast "nord" - tylko pomiar 175, nie ruszane (uwaga do przegladu).

**Nie zrobione:** pulap Strazy w ludziach - kawalek 3 (166).

---

## Kawalek 3: 166 budzet rodu + 162m dwor - ZROBIONE (commit 6dff51b)

**Pliki:** nowy `Armoury/src/ClanBudget.cs`; `SoldierPay.cs` (tarcza dworu `HoldCourt`, `DecayCourt`, regulator z dwoma znacznikami, zapis),
`AiGear.cs` (przydzial sprzetu), `BuildFunding.cs` (przydzial budow), `IronBank.cs` (`FamilyOn` wylaczone przy kiesie rodziny budzetu),
`ClanIncomeBook.cs` (linia na sucho: jedna formula `ClanBudget.Ceiling`), `ClanIncomeBook.StableD.cs` (`StableDWithDays` + jednorazowe),
`MoneyLedger.cs` (pozycja `MBudget`), `MoneyLedger.Obieg.cs`, `ArmouryBehavior.cs` (krok po Banku miedzy `Mark(MRest)` a `Mark(MBudget)`,
zapisy, `EnsureHooks`), `SubModuleMain.cs`, `Settings.cs` + `McmSettings.cs`.

**Co dziala (ClanBudgetEnabled = true - teraz domyslnie; wlacza tez 165 i 182):**
- Rody: AI w krolestwie, nie najemnicy (185), nie dworzanie BK, nie Inni; gracz nigdy. Rody pomniejsze poza krolestwem - bez budzetu (ich dochod to
  "za tier" z niczego, poza D - budzet na D zabralby im cale wojsko; etap 3).
- D: D staly 169c (`ClanBudgetStableD`), u najezdzcow (krolestwa `sturgia` Zelazne Wyspy, `khuzait` Dothrakowie, `freefolk`) + srednie "jednorazowe"
  bez podwojnych (lup); pomiar krotszy niz 28 dob mieszany z D z zapisu (pierscienie D stalego sa tylko w sesji) albo G/60 (start). Bez D stalego - D169.
- Pulap (jedna formula `ClanBudget.Ceiling` - ta sama w linii "Budzet rodow (na sucho)"), udzialy dworu/sprzetu/budow, R z podloga na doroslego.
- Zalogi: w wojnie (`GarrisonWarFull`) pulap gry, finansowane pierwsze; w pokoju cel = `GarrisonPeaceShare` x srednia zoldu zalogi z dob wojny (28-dobowa;
  przed pierwsza wojna - stan pierwszej doby), razem najwyzej 80% pulapu -> `SetGarrisonWagePaymentLimit` (w Daily i w postfiksie
  `UpdateClanSettlementsPaymentLimit`). Partie: reszta pulapu, glowa 1.5 : inni 1 -> `SetWagePaymentLimit` (Daily + postfiks `MakeClanFinancialEvaluation`).
- Zwolnienia: `BudgetHysteresis`/`BudgetHysteresisDays`/`ReleasePerDay`; kolejnosc: zalogi ponad cel (pokoj), najemnicy (Occupation.Mercenary), najnizszy
  tier partii; zalogi w wojnie nietkniete. Ludzie -> `PopulationData.UpdatePopFromSoldiers` wsi z danymi BK (zaloga: wsie tej twierdzy; partia: najblizsza
  wies rodu, potem krolestwa); bez takiej wsi - nikt nie odchodzi (licznik "nie zwolniono"). Czesc sakiewki partii (ludzie odchodzacy / ludzie) -> kiesa wsi.
  Sprzet zostaje w zbrojowni partii (DTE) - nadwyzki sprzedaje jak dotad `SellArmorySurplus`.
- `AiWageLimitDesertionOff`: prefiks/finalizer na `DefaultPartyDesertionModel.GetTroopsToDesertDueToWageAndPartySize` - dla partii i zalog rodu z budzetem
  limit zoldu zdjety na czas wywolania (czesc "limit zoldu" = 0); przepelnienie partii i zalegly zold zalogi bez zmian.
- Nowa partia (po przegladzie: `SpawnLordParty`, kazda osobno): tylko gdy w pulapie partii miejsce na `MinNewPartyMen` ludzi x sredni zold; glowa rodu, rod bez zadnej partii i nowa kampania - zawsze.
- Straz (182, `UnpaidTroopsCapInMen`): pulap w ludziach = pulap zoldu / sredni nominalny zold czlowieka Strazy (`MountedWage.Nominal`); werbunek partii
  (`CheckRecruiting`) i zalog (`TickAutoRecruitmentGarrisonChange`, `TickGarrisonChangeForTown`) staja na nim; zwolnienia w ludziach, zalogi na Murze nie ciete.
- Kiesa rodziny (`FamilyTopsUpHead`): czlonkowie ponad `FamilyPurseFloor` dopelniaja glowe do max(5 000; koszt dnia rodu = zold + dwor) przez
  `GiveGoldAction` (jak T5); `IronBank.FamilyOn` = false przy tej regule.
- Sprzet: `AiGear` - budzet pana = min(dotychczasowy, niewydany przydzial; przydzial +0.17 D/dobe, najwyzej `AiGearDaysCap` dni), kazda zaplata pana schodzi z przydzialu.
- Budowy: `BuildFunding` - przydzial budow budzetu (0.10 D w pokoju + polowa nadwyzki ponad 120 D + 50 000 po 1/180; w wojnie 0 - takze mury).
- 162m dwor (`HouseholdMinimal`): glowa -> kasa siedziby: udzial dworu (0.35/0.20 D x bieda) minus jedzenie partii (szacunek: zuzycie dnia `FoodChange` x cena
  zboza w siedzibie), nie ponizej `FamilyPurseFloor` w kiesie glowy. Siedziba: `HomeSettlement` (wies -> `Village.Bound`), inaczej miasto, w ktorym jest glowa,
  inaczej najblizsze miasto krolestwa. "Wlasne" D stalego (NoteOwnPaid). Kasa MIASTA - znacznik tarczy dworu (`HouseholdShield`): regulator nie kasuje,
  schodzi tylko o czesc zaworu renty i daniny wojennej tego miasta dzis (proporcjonalnie znacznik / kasa ponad prog), przyciety do nadwyzki ponad cel
  regulatora (miasto wydalo). Zamek - bez znacznika (110: regulator zamku tylko w dol).

**Nowe klucze (grupa "The crown's income (stage 2)"):** `ClanBudgetEnabled` true (byl false w kawalkach 1-2), `AiWageLimitDesertionOff` true,
`ClanBudgetStableD` true, `PeaceWageShare` 0.28, `WarWageShare` 0.60, `HouseholdSharePeace` 0.35, `HouseholdShareWar` 0.20, `GearSharePeace` 0.17,
`GearShareWar` 0.17, `WarChestToWages` 0.8, `WarChestDays` 45, `ReserveDaysPeace` 60, `ReserveCapDays` 120, `WarReserveDays` 20, `WarReserveFloor` 20000,
`WarReservePerAdult` 5000, `PovertyDeepShare` 0.25, `PovertyDeepFactor` 0.5, `BudgetHysteresis` 1.10, `BudgetHysteresisDays` 3, `ReleasePerDay` 0.15,
`MinNewPartyMen` 30, `GarrisonPeaceShare` 0.5, `GarrisonWarFull` true, `GarrisonMaxShareOfBudgetPeace` 0.8, `AiGearDaysCap` 30, `FamilyPurseFloor` 5000,
`FamilyTopsUpHead` true, `UnpaidTroopsCapInMen` true, `HouseholdMinimal` true, `HouseholdShield` true. Udzial budow = istniejacy `BuildIncomeShare` (0.10).

**Nowe / zmienione linie logu:**
- NOWA (kampania) `Budzet rodow (166): latki gry - limit partii (MakeClanFinancialEvaluation), limit zalog (...), dezercja z limitu zoldu (...), nowa partia (...), werbunek Strazy (...), zaloga Strazy: werbunek (...), zaloga Strazy: przyrost (...); BRAK: -.`
- NOWA `Budzet rodow (166): dzien N | rody AI z budzetem N (pokoj a, wojna b; ...) | D razem, pulap zoldu razem X, zold naliczony (partie + zalogi + karawany) Y (Z%) | ponad 1.10 x pulap N rodow, od 3 dob (zwalniaja) M | zwolnieni: do wsi N ludzi (z partii a, z zalog b), zniklo 0, nie zwolniono - brak wsi z danymi BK c, sakiewki zwolnionych do kies wsi d zl | dezercja gry z limitu zoldu wylaczona (rody z budzetem): N wywolan | limity: partie zmienione N, zalogi przyciete do celu pokojowego N, nowe partie wstrzymane N | dwor (162m): wplacone do kas siedzib X (w tym miasta pod tarcza dworu Y), jedzenie partii (szacunek, odjete) F, tarcza dworu wlaczona: znacznik ..., regulator nie skasowal ..., zeszlo z zaworem i danina ..., miasto wydalo (...) ..., skasowane przez regulator 0 | kiesa rodziny do glow: X zl w N przelewach; glowy < 5000 (rody z budzetem): N | sprzet: przydzial dnia X, wydane przez panow od wczoraj Y | budowy: przydzial dnia X | Straz w ludziach X / pulap w ludziach Y (werbunek wstrzymany a, przyrost zalog wstrzymany b).`
- ZMIENIONA `Budzet rodow (na sucho)`: pulap z jednej formuly (`ClanBudget.Ceiling`, udzialy z Settings 0.28/0.60, R z podloga na doroslego); u rodow z
  budzetem - pulap policzony dzis w grze. Liczby "pulap zoldu wg planu" zmieniaja sie wobec biegu bazowego (0.25/0.55 -> 0.28/0.60).
- ZMIENIONA `Przeplywy osad`: nowa pozycja "budzet rodow (166): dwor do kas siedzib, sakiewki zwolnionych do kies wsi".
- ZMIENIONA `Obieg: dzien N`: "dwor (166/162m) do kas siedzib X (w tym miasta pod tarcza dworu Y), kiesa rodziny czlonek -> glowa Z" i "ze zwolnionymi do kies wsi (166) X (ludzi N)".
- Bez zmian, potrzebne do testu: `Dezercja AI wedlug przyczyny (169c)` ("limit zoldu i wielkosci partii (gra)" - po 166 tylko przepelnienie), `Skarbce:`, `budzet-rodow.csv`.

**Odstepstwa (z powodem):**
1. Rody pomniejsze poza krolestwem - bez budzetu (powod wyzej); najemnicy AI - bez budzetu do kawalka 4 (pulap = umowa 185).
2. Zakup sprzetu: min(dotychczasowy budzet 25% kiesy, przydzial), nie sam przydzial - pan nie wyda wiecej niz dzis; `GearUnspentToHouseholdDays` (niewydany
   sprzet po 30 dniach na dwor) - nie zrobione: przydzial po prostu nie rosnie ponad 30 dni (zostaje w kiesie glowy - zapas rodu).
3. Werbunek (koszt rekruta) nie jest liczony w przydziale "sprzet i werbunek" - hamuje go limit zoldu partii (gra nie werbuje ponad limit).
   Zaopatrzenie majatkow BK (rycerze) poza budzetem - pomiar "Wydatki rycerzy (169c)" zdecyduje o osobnym pulapie (projekt).
4. Jedzenie partii odejmowane od dworu jako szacunek (zuzycie dnia x cena zboza w siedzibie), nie z licznika zakupow (gra/BK kupuja jedzenie bez naszego okna).
5. Dluznik (budowy 0, dwor -50%) i warunki zakupu karawan/warsztatow BK (B5) - z 168 (krok D), nie zrobione.
6. Zwolnieni: sprzet zostaje w zbrojowni partii (MusterOut nie ma juz mechanizmu oddawania - cofniete 29.08); "do karczmy" (167) - etap 4.
7. Pierscienie D stalego nie sa zapisywane (zapisujemy tylko D uzyte wczoraj na rod i mieszamy) - mniej danych w zapisie (R14), ten sam skutek po 28 dobach.
8. `ConsiderSpawningLordParties`: rod bez zadnej partii moze ja zawsze wystawic (inaczej glowa rodu zostaje bez druzyny na stale).

**Ryzyka dla testu 40/120 dob:**
- Straz: pulap w ludziach z D Strazy (dar Polnocy + wlasne wsie) moze byc duzo mniejszy niz dzisiejsze ok. 3.8 tys. - zwolnienia partii Strazy (zalogi na
  Murze nie ciete). Prog projektu: Straz -25%..+10%. Linia "Straz w ludziach X / pulap w ludziach Y".
- Wojsko w wojnie: pulap 0.60 D + skrzynia wojenna; projekt zaklada ok. -4% bez KW (168) - prog >= 90 tys. w druzynach lordow.
- Pierwsze doby po wczytaniu starego zapisu: D = G/60 (brak D w zapisie) - u rodow z duza kiesa pulap moze byc za wysoki, u biednych za niski przez ok. 28 dob.
- Zwolnienia potrzebuja wsi z danymi BK (`IsSettlementPopulated`); licznik "nie zwolniono - brak wsi z danymi BK".
- Dwor do kas miast + tarcza: "skasowane przez regulator" ma byc 0; "miasto wydalo" to zloto, ktore kasa wydala na towar (nie strata).

---

## Kawalek 4: 185 kontrakt najemnika AI - ZROBIONE (commit 1aa1580)

**Pliki:** nowy `Armoury/src/MercContract.cs`; `ClanBudget.cs` (najemnik w budzecie: pulap = zold ludzi z umowy, bez dworu), `ClanIncomeBook.cs` +
`ClanIncomeBook.StableD.cs` (nowy rodzaj wplywu `KContract` - czesc "kontrakt" D stalego, nie "jednorazowe"), `ArmouryBehavior.cs` (krok po ratach
reparacji, przed zwrotem; zapis `arm_merc185`; EnsureHooks), `SubModuleMain.cs`, `MoneyLedger.Obieg.cs`, `Settings.cs` + `McmSettings.cs`.

**Co dziala (MercContractEnabled i 165 czynne):**
- Kompania AI w sluzbie (nie gracz): w pierwszej dobie u danej korony umowa K = `MercContractFactor` (1.3) x dzisiejszy zold jej partii, ludzie z umowy =
  dzisiejsi ludzie partii. Zmiana korony = nowa umowa; rod poza sluzba - umowa wygasa. Stary zapis: umowy od pierwszej doby po wczytaniu.
- W wojnie korony K, w pokoju `MercPeaceShare` (0.5) x K; pulap budzetu 166 = zold ludzi z umowy (w pokoju polowa), bez dworu i budow.
- Przeglad co `MercReviewDays` (28) tylko w dol: ludzi < `MercReviewFloor` (0.75) x umowa -> K, zold i ludzie proporcjonalnie do stanu.
- Zaplata: skarbiec -> glowa rodu najemnego z reszty wplywow dnia (po darach i ratach, przed zwrotem), proporcjonalnie (`KingdomTreasury.Split`);
  niedoplata > 50% przez `MercUnpaidLeaveDays` (28) dob z rzedu -> `ChangeKingdomAction.ApplyByLeaveKingdomAsMercenary`.
- `MercGameContractAiOff`: prefiks `DefaultClanFinanceModel.AddMercenaryIncome` (AI w sluzbie - bez wplywu i bez `MercenaryWallet`) i postfiks
  `CalculateClanIncomeInternal` (AI w sluzbie bez lenn: "za tier" Tier x 120 zdjete tym samym wpisem bez opisu). Gracz-najemnik bez zmian. Latki w kampanii
  (pulapka konstruktora statycznego modelu finansow).

**Nowe klucze:** `MercContractEnabled` true, `MercContractFactor` 1.3, `MercPeaceShare` 0.5, `MercReviewDays` 28, `MercReviewFloor` 0.75,
`MercUnpaidLeaveDays` 28, `MercGameContractAiOff` true.

**Nowe / zmienione linie logu:**
- NOWA (kampania) `Kontrakty najemnikow (185): kontrakt gry dla AI (AddMercenaryIncome) wpiety|BRAK, "za tier" AI (CalculateClanIncomeInternal) wpiety|BRAK; ...`
- NOWA `Kontrakty najemnikow (185): <rod> w sluzbie <krolestwo> - kontrakt K zl dziennie w wojnie (1.30 x zold W), ludzi z umowy N; w pokoju polowa.`
- NOWA `Kontrakty najemnikow (185): <rod> odchodzi ze sluzby <krolestwo> - korona nie placila ponad polowy kontraktu przez 28 dob.`
- NOWA `Kontrakty najemnikow (185): dzien N | kompanii AI w sluzbie N (nowe umowy a, przeglad w dol b, odeszly po niedoplacie c) | kontrakty nalezne X, zaplacone z wplywow dnia Y, niedoplata Z | ludzie najemnikow AI A / z umowy (w pokoju polowa) B | gra dla AI w sluzbie: "za tier" wylaczone T zl, kontrakt gry wylaczony G zl (od wczoraj; AI dostaje 0 z gry) | MercenaryWallet krolestw razem W (zmienia sie tylko o kontrakt gracza).`
- `Korona: wplywy dnia (165)`: "kontrakty najemnikow (185) X" i na krolestwo "kontr. X".
- `Obieg: dzien N` (korona): "kontrakty najemnikow AI (185) skarbce -> glowy kompanii X (nalezne Y; gra dla AI: za tier wylaczone T, kontrakt gry wylaczony G)".
- `D staly (169c)`: kontrakt od korony w czesci "kontrakt" (dotad "kontrakt" = linia modelu gry; dla AI teraz 0, dla gracza bez zmian).

**Odstepstwa:**
1. "W dniu najmu" = pierwsza doba w sluzbie widziana przez nasz tick (nie zdarzenie najmu gry) - obejmuje tez kompanie juz w sluzbie przy wczytaniu.
2. Zold kompanii przy najmie = suma `TotalWage` jej partii lordow (bez karawan). Ludzie w pokoju "polowa" realizuje pulap budzetu (zwolnienia 15%/dobe
   po 3 dobach), nie natychmiast.
3. "za tier" znoszony wpisem ujemnym bez opisu (ta sama kwota co gra), nie przez wyciecie dodawania (transpiler) - wynik ten sam, mniejsze ryzyko.

**Ryzyka:** (a) JIT moglby wkleic `AddMercenaryIncome` w wolajacego - wtedy "kontrakt gry wylaczony 0" przy kompaniach w sluzbie i niezerowa linia
"kontrakt" u AI w `D staly (169c)`; (b) kompanie w pokoju traca polowe ludzi (zgodnie z projektem "w oczekiwaniu"); (c) biedna korona nie placi -> po 28 dobach
kompanie odchodza (projekt: "biedna korona nie utrzyma najemnikow").

---

## Poprawki po przegladzie kodu (commity 57cbc1b, a660030)

Niezalezny przeglad diffu `ae01f8e..HEAD` (podpisy latek Harmony sprawdzone w dekompilacji 1.4.8, BK i Diplomacy - wszystkie zgodne; przelewy w parach;
wylacznik glowny = stan sprzed paczek poza tekstem logu). Poprawione:
1. **185 przeglad w pokoju** - porownanie z ludzmi wymaganymi dzis (w pokoju polowa umowy), nie z pelna umowa; obnizka proporcjonalna do tej liczby.
   Bez tego kazdy pokoj obcinal umowe do ok. 55%.
2. **185 umowa zerowa** - umowa powstaje dopiero, gdy kompania ma ludzi i zold (kompania Strazy - stawka nominalna `MountedWage.Nominal`); gra przestaje
   placic ("za tier", kontrakt gry) tylko kompaniom z umowa u obecnej korony (`HasDeal`), ksiega obiegu tak samo (`CancelsTier`).
3. **165 splata DebtToKingdom** - skarbiec dostaje splate dopiero na koncu rozliczenia rodu (`SoldierPay.ClanTickPostfix` -> `CrownIncome.ClanTickEnd`) i tylko
   czesc naprawde zaplacona: splata minus brak salda w kiesie glowy (saldo nie zmiescilo sie w kiesie - BK dopisuje wydatki po tym kroku); bez znanego salda - nic.
   Licznik w linii "Korona: wplywy dnia (165)": "niezaplacone mimo wpisu w saldzie - nie do skarbca X". **Uwaga do decyzji (przeglad):** dlug wobec korony
   powstaje w grze, gdy rod nie ma na swoj udzial w portfelu najemnikow/trybutu/wezwania do wojny, a portfel jest uznawany w calosci (z niczego) - dotad splata
   ginela i to "rownowazylo" tamto zrodlo. Zostawione wedlug projektu (165: "splata DebtToKingdom -> skarbiec, dzis w nicosc"): sama splata jest przelewem
   rod -> skarbiec (bez zlota z niczego); zrodlem z niczego jest pozyczka gry przy braku udzialu - zamyka ja 168 (krok D: dlug i drabina).
4. **Bank: pomoc rodziny** wylaczona tylko u rodow z budzetem dzis (`ClanBudget.FamilyRuleFor`); rody bez budzetu (pomniejsze, najemnicy bez umowy) - jak dotad.
   Ryzyko zostaje: u rodow z budzetem kiesa rodziny dopelnia glowe PO Banku (kolejnosc 2.0b) i do max(5 000; koszt dnia), nie do raty - spoznienia rat
   moga byc czestsze (linia IronBank).
5. **Zwolnienia z zalog** pomijaja zaloge w bitwie (`MapEvent`) i osade w oblezeniu.
6. **Pulap Strazy w ludziach** sprawdzany przy werbunku/przyroscie zalog od biezacego stanu ludzi rodu (`LiveMen`), nie od porannego.
7. **Nowa partia** - prefiks przeniesiony z `ConsiderSpawningLordParties` na `SpawnLordParty(Hero, bool)` (jedyny wolajacy): kazda partia sprawdzana osobno,
   z miejscem zajetym przez nowe partie tej doby; glowa rodu zawsze moze wystawic partie; nowa partia dostaje limit zoldu od razu (postfiks).
   Linia latek: "nowa partia (SpawnLordParty)".
