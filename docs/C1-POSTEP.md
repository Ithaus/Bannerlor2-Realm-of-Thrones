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

---

## Recenzja C1 (workflow) - poprawki

Recenzja diffu `ae01f8e..HEAD` (soczewki: zloto, gra, wojsko), 10 potwierdzonych uwag. Build Release kod 0, `python tools/gen_mcm.py` (opisy 3 kluczy), gra
nie uruchamiana. Kazda poprawka ma w kodzie komentarz "recenzja C1 (id)".

1. **OBIEG-1 (165, splata dlugu wobec korony)** - `CrownIncome.cs`, `SoldierPay.cs`, `MoneyLedger.Obieg.cs`. Nowa ewidencja "zaliczki gry": `SoldierPay.Settle`
   liczy nowy dlug rodu w rozliczeniu brutto (zmiana `DebtToKingdom` + dzisiejsza splata w `AddPaymentForDebts`) i odejmuje czesc zrownowazona obcietym
   zoldem (`cutByDebt` = min(przyrost, zold nalezny minus brak salda)); reszta (portfel najemnikow / trybutu / wezwania do wojny uznany przez gre z niczego) ->
   `CrownIncome.NoteAdvance` (rod bez partii z zoldem albo saldo nieznane - caly nowy dlug). `ClanTickEnd`: splata naprawde zaplacona najpierw gasi zaliczke -
   ta czesc w nicosc (jak przed C1), do skarbca tylko reszta; zaliczka przycinana do dlugu przy kazdym rozliczeniu. Dlug z obcietego zoldu i dlugi ze starego
   zapisu (bez wpisu) - do skarbca jak w tabeli 165. Zapis: piate pole `arm_crown165` ("idRodu=zaliczka;...", tylko zywe rody z dlugiem; zapis bez tego pola
   wczytuje sie jak dotad). Log: w `Korona: wplywy dnia (165)` po "splata dlugu wobec korony do skarbcow" - ", splata zaliczki gry (portfel uznany z niczego) -
   w nicosc X (nowe zaliczki gry od wczoraj Y, niesplacone razem Z u N rodow)"; w `Obieg` (korona) - "(splata zaliczki gry - portfel uznany z niczego - w nicosc X)".
   Roznica wobec propozycji recenzenta: nowy dlug liczony brutto (z dzisiejsza splata), nie netto - rod, ktory tego samego dnia splacil stary dlug i dostal
   nowy z wezwania do wojny, inaczej zanizalby zaliczke o te splate. Zrodlo (uznawanie portfela w calosci) zostaje do 168 (krok D). Rozstrzyga "Uwage do decyzji"
   z pkt 3 poprawek po przegladzie.
2. **C1-G1 (Bank, pomoc rodziny)** - `IronBank.cs`, `ClanBudget.cs`. Usuniete wylaczenie T5 u rodow z budzetem (`ClanBudget.FamilyRuleFor` usuniete): rodzina
   pomaga glowie przed pozyczka i rata u wszystkich rodow AI, kiesa rodziny 166 dopelnia glowe po Banku. **Odstepstwo od projektu 166** ("`IronBankFamilyPays` ->
   false"): prog Banku to 10/20 dni zoldu, a cel `FamilyTopUp` - max(5 000; koszt dnia), wiec bez T5 Bank pozyczal rodom, ktorych rodzina ma zloto (powrot do
   stanu sprzed T5: 83 dluznikow), i liczyl spoznienia rat, ktore rodzina by pokryla. Oba przelewy czlonek -> glowa (`GiveGoldAction`). Prog pozyczki - 168.
3. **C1-G2 (185, umowa zerowa)** - `MercContract.cs`. Przeglad tylko przy kompanii w polu (ludzie > 0, zold > 0, glowa nie w niewoli) - inaczej `d.Review`
   zostaje i przeglad odbywa sie w pierwszej dobie z kompania w polu. Umowa przycieta do 0 (K, zold albo ludzie) jest usuwana - przy przegladzie i przy wejsciu
   (umowy zerowe z zapisu); gra placi wtedy jak dotad, nowa umowa powstaje w pierwszej dobie z kompania w polu. Dotad umowa 0 = brak dochodu, brak odejscia
   po niedoplacie i pulap 166 = 0 na zawsze.
4. **C1-G3 + W2 (185, przeglad)** - `MercContract.cs`, `Settings.cs` (opisy `MercContractEnabled`, `MercReviewFloor`). Przeglad **w zlocie** (zold kompanii
   wobec zoldu ludzi z umowy - ta sama jednostka co pulap 166 `CapOf`); K, zold i ludzie z umowy proporcjonalnie. Nowe pole umowy `Peace` (ostatnia doba pokoju
   korony): w pokoju i przez pierwsze `MercReviewDays` wojny po pokoju przeglad mierzy polowe umowy. Zapis: 9. pole `arm_merc185` (8-polowe wpisy ze starego
   zapisu - "dluga wojna"). **Rozstrzygniecie miedzy uwagami:** obie dodawaly 9. pole na przejscie pokoj -> wojna (C1-G3: `Peace`; W2: `War` i przesuniecie
   przegladu o 28 dob przy kazdej zmianie stanu). Wybrane `Peace` z C1-G3 - zachowuje rytm przegladu co 28 dob z projektu i dalej lapie kompanie rozbita w
   pierwszych tygodniach wojny (wobec polowy); jednostka przegladu - z W2. **Odstepstwo od litery projektu 185** ("< 75% ludzi z umowy"): przeglad w zlocie, bo
   pulap 166 najemnika jest w zlocie (zold ludzi z umowy) - przeglad w ludziach przy pulapie w zlocie cial umowe geometrycznie (zwolnienia od najtanszych
   zabieraja wiecej ludzi niz zlota, awanse do limitu podnosza zold czlowieka). "Ludzie z umowy" sa teraz informacyjne (maleja proporcjonalnie z umowa);
   test "ludzie najemnikow +-10% umowy" czytac razem z zoldem kompanii wobec zoldu umowy (awanse zmniejszaja ludzi przy tym samym zoldzie).
5. **C1-G4 (166, wczytanie w tym samym procesie)** - `ClanBudget.cs`. `Reset` czysci uchwyty BK (`_bkTried`, `_popMgr`, `_getPopData`, `_fromSoldiers`,
   `_populated`), `_grain`/`_grainTried`, `_spawnUsed`/`_spawnDay` (jak `LosersFlee.Reset`). Latki Harmony zostaja (zyja przez caly proces).
6. **W3 (166, rozklad zwolnien)** - `ClanBudget.cs`. Limity partii i zalog ustawiane przed zwolnieniami; kwota dnia dzielona miedzy partie: najpierw wedlug
   nadwyzki kazdej ponad jej wlasny limit zoldu, reszta wedlug zoldu partii (Straz bez zoldu - wedlug ludzi), osobno w przejsciu "najemnicy" i "wszyscy".
   Dotad cala kwota szla z pierwszej partii na liscie (petla werbunek -> zwolnienie, inne partie nad limitem na stale).
7. **W4 (166, zwolnienia w oblezeniu)** - `ClanBudget.cs`. Partie w obozie oblezniczym (`SiegeEvent`, `BesiegedSettlement`) i w oblezonej osadzie
   (`CurrentSettlement.IsUnderSiege`) pomijane jak partie w bitwie; reszta czeka (licznik dob ponad pulapem trwa).
8. **W5 (166, budowy w wojnie)** - `ClanBudget.cs`, `BuildFunding.cs` (komentarz). Przydzial budow 0.10 D takze w wojnie (dopelnienie ponad 120 D + 50 000 -
   tylko w pokoju); `BuildFunding` w wojnie i tak finansuje tylko mury, wieze i koszary. **Odstepstwo od tabeli 166** ("budowy w wojnie 0"): regula Jeffa
   05.10 "w wojnie 0, chyba ze mury - tak" (naglowek `BuildFunding`) - projekt jej nie odwolal, a "0" wylaczalo cala galaz wojskowa na niemal cala kampanie.
9. **W1 (test C1 - tylko plan testu, kod bez zmian)**: cel pokojowy zalog z 1. doby i ciecia od 4. doby sa wiazace (166). W biegu bazowym 267 z 308 rodow
   jest w pokoju do ok. d22, wiec C1 przytnie zalogi i partie przed wojna, a dobor do pelnych zalog trwa tygodnie. **Progi testu C1 zmienione** (tez we wpisie
   C1 w `CHANGELOG.md`; zastepuja "zalogi w wojnie >= 95% bazy", "wojsko w druzynach lordow w wojnie >= 90 tys." i ryzyko "prog >= 90 tys." wyzej):
   - zalogi w wojnie >= 95% bazy na krolestwo - tylko twierdze rodow, ktore w OBU biegach sa w wojnie nieprzerwanie od >= 28 dob; gdy w d40 takich krolestw
     brak albo pojedyncze - pomiar zalog w d60+ (bieg przedluzony albo odczyt z biegu 120);
   - wojsko w druzynach lordow w wojnie >= **96% bazy tej samej doby, w tym samym zbiorze rodow** (w wojnie w obu biegach od >= 18 dob), zamiast 90 tys.
     bezwzglednie (sam pulap wojenny C1 bez historii pokoju daje w d40 ok. 88.3-89.1 tys. wobec 91.7 tys. bazy - prog 90 tys. oblewa okno, nie kod);
   - do raportu dla Jeffa: przejscie pokoj -> wojna z polowa zalog to zamierzony skutek 166, dobor do pelnych zalog trwa kilka tygodni.

**Nie zmienione:** `tools/sprawdz_logi.py` (grupa etap2 ma progi bezwzgledne wojska dla etapu po C3 - progi C1 z pkt 9 czytac recznie z `budzet-rodow.csv`
obu biegow); projekt `PROJEKT-ETAP2-BANKRUCTWA-2026-10-09.md` (repo glowne) - odstepstwa opisane tutaj.

---

## Wojsko w wojnie po tescie 120 dob

Bieg C1 (C1-r, `kopia-c1r-120`, 2026-10-09_23-06-12) wobec bazy bez C1 (`kopia-e2b2-120`, 2026-10-09_18-54-52), `budzet-rodow.csv` obu biegow, doby 94-120
(rody krolestw w wojnie): ludzie w partiach **88.9 tys. wobec 114.9 tys. (-23%)**, zalogi 75.9 / 78.1 tys. (-3%). Skrypty: scratchpad sesji 7016f733, `diag-wojsko/`.

**Diagnoza (najpierw dane).**
1. **Pulap 166 wiaze - u rodow, ktore traca ludzi.** Suma "zold 47-51% pulapu" myli: pulap jest skupiony u ok. 190 bogatych rodow z duza skrzynia
   wojenna (pulap 1.22 mln/dobe, z tego skrzynia 0.67 mln, zold 0.49 mln, ludzie 71.3 tys.) - te sa blisko bazy. Rody w wojnie w obu biegach (>= 90% dob
   90-120), wedlug zuzycia pulapu w C1: **zold >= 0.9 pulapu - 65 rodow, partie 15.6 / 33.4 tys. (-53%, 73% calego braku)**; 0.6-0.9 - 40 rodow,
   20.0 / 24.1 tys. (-17%); < 0.6 - 162 rody, 52.1 / 54.7 tys. (-5%); najemnicy 0.25 / 0.50 tys. Wedlug rodzaju: panowie miast -23% (-14.4 tys.), zamkow
   -18% (-8.4 tys., prog R19 "panowie zamkow >= 90%" niespelniony), bez lenna -47%, najemnicy -79%.
2. **Dlaczego pulap jest nizszy niz dawny zold** (rody "na pulapie", ok. 97 na dobe w dobach 94-120): D 202 tys. (w tym korona 40 tys.), pulap 177 tys. =
   0.60 D x bieda 120 tys. + skrzynia 41 tys. (+ reszta); zold partie 116 + zalogi 66 (37% pulapu, finansowane pierwsze) + karawany 7 = 189 tys. W bazie te
   rody wydawaly ok. 1.1 D: zwrot korony w D 112 tys. (w C1 38 tys. - 165 placi tylko partie w polu, z wplywow dnia), lup 75 tys. (w C1 45 tys. - mniej
   ludzi), Bank 479 tys. dlugu (w C1 6 tys.), kiesy rodzin 12.5 mln (w C1 7.9 mln). **Dwor 0.20 D** (ok. 40 tys./dobe u tych rodow, ok. 165-175 tys./dobe
   w swiecie) szedl do kas siedzib takze wtedy, gdy rod byl na pulapie - to najwiekszy wydatek poza zoldem, ktory rod moze przesunac.
3. **Zwolnienia w wojnie:** doby 24-120 - 21.1 tys. ludzi z partii i 4.6 tys. z zalog (pokoj 1-23: 8.8 / 20.3 tys. - regula projektu). Rod-doby
   zwalniajace w wojnie: bez lenna 848, zamki 181, miasta 106; powod - spadek pulapu (skrzynia wojenna sie wyczerpuje, G < R w 58% przypadkow), zaloga
   pierwsza. Zwolnienia od najnizszego tieru, a wolne miejsce zajmuja awanse: zold na czlowieka u rodow na pulapie 6.75 wobec 5.37 w bazie (+26%).
4. **Limit pojedynczej partii:** staly podzial reszty pulapu 1.5 : 1 - partia ponad swoja czescia (zwykle druzyna glowy, najwieksza) nie werbuje ani nie
   awansuje, a gra (`CalculateMobilePartySizeLimitWithFoodAndWage`, `FindPartySizeNormalLimit` - PaymentLimit / AverageWage) uznaje ja za pelna, choc rod
   ma luz. Grupa 0.6-0.9 (luz 27% pulapu partii) ma -17% ludzi, grupa < 0.6 (limity = 10 000 = bez limitu) -5%. Pomiaru na partie w CSV nie ma.
5. Inne sprawdzone, **nie** przyczyna: kiesa glowy (rody na pulapie - glowa srednio ok. 100 tys.; warunek gry `StartRecruitingMoneyLimit` 50-3 050);
   kiesa rodziny do glow - 0-2 tys./dobe; nowe partie wstrzymane - 3-11/dobe w wojnie; Straz - 3.5 tys. ludzi przy pulapie w ludziach 6.6 tys.; sprzet
   (AiGear) - wydane 12-36 tys. z 0.4 mln przydzialu (nie hamuje ludzi); przeplyw partia -> zaloga gry netto do partii (+321 / -343 w d100).
6. **Zelazne Wyspy (-72%)** - ten sam mechanizm, mocniej: 20 z 25 rodow na pulapie; pulap 28.9 tys., zalogi 12.2 tys. (42%), partie 15.1 tys.; D 24.3 tys.
   + lup 9.4 tys. wobec 37.7 + 6.0 tys. w bazie (korona w D 7.0 wobec 24.6 tys. - baza zwracala 50% zoldu takze zalogom, ze skarbca). Baza zyla ponad stan:
   zold 54.5 tys./dobe przy D z lupem 43.7 tys. (kiesy -1.0 mln w 120 dob, Bank 209 tys.). W C1 zalogi rosly w wojnie 1.3 -> 4.1 tys. ludzi, partie
   3.1 -> 2.1 tys. (pulap staly, zaloga pierwsza).
7. **Dothrakowie (-57%)**: na pulapie tylko 2.6 z 18 rodow (pulap 33.5 tys., zold 18.0 tys.). Brak ok. 2.1 tys.: rody z lennem ok. -1.1 tys. (Pono, Jhago -
   zaloga 2 298 przy pulapie 2 349), najemnicy w sluzbie Dothrakow ok. -0.85 tys., rody gentry bez lenna ok. -0.85 tys. (partie BK pojawiaja sie seriami -
   w bazie seria w d80-100; szum). Dar 182 dzielony rowno na rod - 13 z 19 rodow to gentry bez partii.
8. **Najemnicy (-79%, 0.43 / 2.07 tys.)** - osobny mechanizm 185: umowa powstaje z dzisiejszych ludzi kompanii, przeglad tylko w dol - kompania najeta
   po pokoju albo po bitwie nie odrasta (Moon Brothers: umowa d61 z 31 ludzi = pulap 320; w bazie 350 ludzi). **Nie zmienione** (decyzja 185) - ponizej.

**Poprawka (build Release kod 0, `python tools/gen_mcm.py`).** Pliki: `ClanBudget.cs`, `Settings.cs` + `McmSettings.cs`, `CrownGifts.cs` (komentarz).
1. **`WarCourtYieldsToWages` (true) - w wojnie dwor ustepuje zoldowi.** Udzial dworu bez jedzenia partii (0.20 D x bieda - szacunek jedzenia) dochodzi do
   pulapu rodu w wojnie (`CourtRoom`); dwor dostaje tylko to, czego zold rodu ponad pulap bez dworu (`CapBase`) z niego nie zajal (Straz - zold nominalny).
   Rod z luzem placi dwor w calosci jak dotad; w pokoju bez zmian. Zloto nie powstaje: zostaje w kiesie glowy i idzie na zold. Siedziba i jedzenie partii
   liczone raz na dobe w `Daily` (nowa funkcja `PartyFood`, `Court` uzywa ich).
2. **`PartyLimitsShareFreeRoom` (true) - limity partii z wolnego miejsca rodu.** Rod ponizej pulapu partii: limit partii = jej zold + jej czesc wolnego
   miejsca (glowa 1.5, inni 1), suma limitow = pulap partii, zadna partia nie stoi, gdy rod ma luz. Rod ponad pulapem partii: limity proporcjonalnie do
   zoldu (zwolnienia W3 rozkladaja sie wtedy wedlug zoldu partii, nie uderzaja w druzyne glowy).
3. **Regula korekty 182:** `GiftFreeCitiesToDothrakiShare` 0.10 -> **0.15** (Dothrakowie w wojnie -57% < -25%).

Wylaczniki: oba nowe klucze false = zachowanie C1-r; `ClanBudgetEnabled` false = stan sprzed paczek (nic z tego nie biegnie). Gracz bez budzetu jak dotad.

**Odstepstwa od projektu.**
- Tabela 166 "dwor i wyzywienie w wojnie 0.20 D": teraz 0.20 D tylko u rodow z luzem; u rodu na pulapie dwor jest rezerwa zoldu (do 0.80 D x bieda + skrzynia).
  Liczby: w dobach 94-120 udzial dworu u rodow na pulapie ok. 40 tys./dobe, natychmiastowe miejsce w pulapie ok. 29 tys./dobe (ok. 4-5 tys. ludzi przy
  dzisiejszym zoldzie na czlowieka); rod-doby z zoldem > 1.10 pulapu w wojnie: 1 938 -> 971 (miasta i zamki zwalniajace: 287 -> 50; zwalniany zold panow
  miast i zamkow -71%), reszta to rody bez lenna (179 w C3). Kasy siedzib dostana w wojnie mniej dworu (ok. 40 tys./dobe z ok. 170 tys.).
- Podzial limitow partii: projekt mowil tylko "partie dostaja reszte pulapu"; staly podzial 1.5 : 1 byl wzorem z gry - teraz reszta jest wspolna.
- **Nie zrobione:** R19 (zaloga w wojnie <= 70% pulapu) - nadwyzka zalog ponad 70% to tylko ok. 2.4 tys./dobe zoldu (37 rodow/dobe), a cielaby zalogi
  wbrew [D] 08.10 "w wojnie pelne" (Zelazne Wyspy juz -13% zalog); kredyt wojenny (168, krok D); najemnicy 185 - umowa z ludzi z dnia najmu (propozycja do
  decyzji: umowa przy nowym najmie co najmniej z polowy pulapu wielkosci kompanii albo z umowy u poprzedniej korony).

**Nowe w logu:** linia `Budzet rodow (166): dzien N` - "w wojnie dwor ustepuje zoldowi: udzial dworu w pulapie X u N rodow, zold go zajal (dwor nie dostal) Y
u M rodow | limity partii z wolnego miejsca rodu, rody ponad pulapem partii K". Kolumna `pulap` w `budzet-rodow.csv` w wojnie zawiera udzial dworu
(0.60 D x bieda + skrzynia + dwor bez jedzenia); "pulap zoldu razem" tak samo. Bez nowych napisow w zapisie.

**Oczekiwany skutek (nastepny test 120 dob, doby 94-120, ten sam zbior):** wojsko lordow w wojnie ok. 95-105 tys. (bylo 88.9, baza 114.9); zwalniany zold
w wojnie ok. -40% (panowie miast i zamkow ok. -70%; zostaja glownie rody bez lenna); rody "na pulapie" maja dwor bliski 0 i zold ok. 0.95 D + lup; zalogi
bez zmian (-3%); "dwor ustepuje zoldowi: zold go zajal" ok. 30-45 tys./dobe.
Zelazne Wyspy ok. -55..-60% (bylo -72%; reszta to bieda z lore i luka kredytu wojennego - baza zyla z kies i Banku); Dothrakowie ok. -45..-50% (bylo -57%;
najemnicy i gentry zostaja; dar +50% idzie glownie do rodow gentry bez partii). Reszte luki do progu 95-115 tys. zamyka kredyt wojenny 168 (krok D).

**Ryzyka.** (a) Kasy miast-siedzib: mniej dworu w wojnie - sprawdzic linie "Przeplywy osad" i "dwor (162m): wplacone" (spadek ok. 20-25% w wojnie).
(b) Rody na pulapie wydaja prawie caly D na zold - glowy < 5 000 i "zold przyciety" moga wzrosnac u biednych (bieda x0.5 tnie tez udzial dworu).
(c) Limity z wolnego miejsca zmieniaja sie codziennie - "limity: partie zmienione" wzrosnie (koszt `SetWagePaymentLimit` maly). (d) Wojna -> pokoj: pulap
spada o skrzynie i dwor naraz - zwolnienia po wojnie wieksze niz w C1 (regula projektu 15%/dobe od 3. doby). (e) Najemnicy dalej maleja (185, wyzej).

---

## C2 - 180 renty korony wedlug lenn - ZROBIONE (commit 7c15a3a)

Projekt: rozdz. "180" (C2), 2.0b, 165 pkt 2 ("Do C2 reszta zostaje w skarbcu"), K10, K41. Build Release kod 0, `python -I tools/gen_mcm.py`, gra nie uruchamiana.

**Pliki:** nowy `Armoury/src/CrownRents.cs`; `CrownIncome.cs` (`KDay.Rent`/`RentHeld`, linia 165), `ClanIncomeBook.cs` + `ClanIncomeBook.StableD.cs` (nowy rodzaj
wplywu `KRent` - czesc "korona" D stalego, kolumny CSV), `ArmouryBehavior.cs` (krok po `WageRefund`, przed `CrownIncome.End`; `Reset`; zapis `arm_rent180`),
`MoneyLedger.Obieg.cs` (pozycje "renty" zamiast "-"), `Settings.cs` + `McmSettings.cs`, `tools/gen_mcm.py` (zakresy dwoch suwakow).

**Co dziala (CrownRents i 165 czynne; wylaczone = reszta zostaje w skarbcu jak w C1):**
- Kolejnosc dobowa (2.0b): powinnosci -> danina/clo -> `CrownIncome.Begin` -> dary 182 -> raty reparacji -> kontrakty 185 -> zwrot 165 -> **renty 180** ->
  `CrownIncome.End` (migawka skarbcow PO rentach - jutrzejsze "wplywy dnia" ich nie widza). Kazde krolestwo, kazdy rod i kazda wyplata we wlasnym `try`.
- Pula krolestwa = `CrownIncome.LeftFor` (reszta wplywow dnia z 1/360 zapasu, najwyzej skarbiec). Dzielona na rody krolestwa wedlug stalych wag lenn z
  `Clan.Settlements`: miasto 3, zamek 1, wies 0.25 (wsie wedlug wlasnosci gry - jak dar 182). Rod krola i gracz tak samo; bez najemnikow (185), dworzan BK, Innych
  i rodow bez zywej glowy. Udzial = floor(pula x waga / suma wag); rod z warunkiem dostaje go do kiesy glowy (skarbiec - x, glowa + x; nieudana wyplata
  oddaje x skarbcowi), udzial rodu bez warunku i zaokraglenia zostaja w skarbcu (zapas - jutro nie sa "wplywem dnia", wracaja tylko przez 1/360).
- **Warunek zalogi:** kazda twierdza rodu ma >= `CrownRentGarrisonShare` (0.5) stalej normy = sredniej zalogi (ludzie bez bohaterow) twierdz tego rodzaju
  (miasto / zamek) w krolestwie, srednia z 28 dob (pierscien na krolestwo i rodzaj; twierdze oblezone poza norma). Pomijane: twierdza oblezona i twierdza
  w rekach obecnego pana krocej niz 28 dob. Brak normy (pierwsza doba) - zaloga nie blokuje.
- **Warunek sluzby:** maska ostatnich 60 dob WOJNY krolestwa na rod (przesuwana tylko w dobach, gdy krolestwo jest w wojnie z innym krolestwem). Doba sluzby:
  partia rodu (lorda, `IsLordParty`/gracz) albo partia, w ktorej jest dorosly czlonek rodu: w armii swojego krolestwa, oblega albo broni oblezonej osady,
  w bitwie z krolestwem w wojnie, albo najblizsza osada (miasto, zamek, wies; partia w osadzie - ta osada) nalezy do krolestwa w wojnie; albo czlonek rodu
  w niewoli u krolestwa w wojnie. Wymog: >= `CrownRentServiceDays` (20) dob sluzby; rod, ktory widzial mniej niz 60 dob wojny tej korony - ceil(20 x widziane / 60);
  0 dob wojny - warunek nie obowiazuje. Zmiana krolestwa rodu - licznik od zera.
- D: wyplata `ClanIncomeBook.NoteInflow(glowa, x, KRent)` -> `Today` i `TodayRent`; w D stalym w czesci "korona" (`QCrown`), odjeta od "jednorazowych" - jeden raz.
  Budzet 166 liczy na D z wczoraj, wiec renta dnia dziala od nastepnej doby (pierscien 28 dob). D169 (gdy D staly wylaczony) tez ja widzi (przez `Today`).
  Powinnosci (`CrownDuesFromLand`) licza od "ziemi" - korona nie bierze 2-3% z wlasnych rent.
- Gracz: te same wagi i warunki; komunikat "The crown paid your house N denars in rents for its fiefs." w dniu renty i "The crown withholds your house's rent: ..."
  (zaloga albo sluzba S z N dob) tylko przy zmianie powodu.

**Nowe klucze (grupa "The crown's income (stage 2)"):** `CrownRents` true, `CrownRentWeightTown` 3, `CrownRentWeightCastle` 1, `CrownRentWeightVillage` 0.25,
`CrownRentServiceDays` 20 (suwak 0-60), `CrownRentGarrisonShare` 0.5 (suwak 0-1), `CrownRentGarrisonNormKingdom` true (false = norma swiata).

**Nowe / zmienione linie logu:**
- NOWA `Renty korony (180): dzien N | krolestwa z renta K, reszta wplywow dnia po zwrocie P = renty R + wstrzymane w skarbcach W (udzialy rodow bez warunku A, zaokraglenia B) | rody z lennem N (w krolestwach bez reszty po zwrocie - renta 0: n): z warunkiem a, bez warunku b (x%) - zaloga c, sluzba d, oba e | renta na udzial (zl/dobe): mediana krolestw M, najwyzsza H | wagi: miasto 3, zamek 1, wies 0.25; warunek: ... | sluzba dzis: rody w wojnie n, sluzylo m (armia, oblezenie, bitwa, ziemia wroga, niewola) | normy zalog (srednio na krolestwo, 28 dob): miasto ..., zamek ...; twierdze pominiete w warunku zalogi: w oblezeniu ...; twierdze swiata w rekach obecnego pana krocej niz 28 dob (pomijane) ... | gracz: renta ... (udzialy ...; zaloga TAK/NIE, sluzba S/N TAK/NIE)|bez lenna w krolestwie (albo najemnik), sluzba S z N dob wojny | na krolestwo (renty/reszta): <krolestwo> R/P (udzial U), ...` (+ `| wczytano: sluzba N rodow (bledne M)` w pierwszej dobie po wczytaniu).
- ZMIENIONA `Korona: wplywy dnia (165)`: po "zwrot zoldu ... )" - `, renty (180) R (wstrzymane w skarbcach - rody bez warunku i zaokraglenia W)`; na krolestwo ` renty R wstrz. W`.
  "zostalo z wplywow dnia w skarbcach" to teraz glownie wstrzymane udzialy.
- ZMIENIONA `Obieg: dzien N`: w "rody dostaly" `renty od korony (180) R` (bylo "-"); w "korona ... wyplaty" `renty wedlug lenn (180) skarbce -> glowy rodow R (z reszty wplywow dnia P; wstrzymane w skarbcach W)` (bylo "-").
- ZMIENIONA `D staly (169c)`: w "dzis (swiat)" po "zwrot korony X" - `, renty korony (180) R`.
- CSV `budzet-rodow.csv`: dwie nowe kolumny na koncu `renta_180;warunek_180` (warunek: `tak|zaloga|sluzba|zaloga+sluzba S/N`, liczony codziennie u kazdego
  rodu z lennem, takze gdy krolestwo nie ma dzis reszty - renta 0; puste - rod bez lenna, najemnik, 165/180 wylaczone). Narzedzia czytaja CSV po nazwach kolumn.

**Odstepstwa / rozstrzygniecia (z powodem):**
1. "Ostatnia wojna albo biezaca z ostatnich 60 dob" = ostatnie 60 dob WOJNY krolestwa (maska nie przesuwa sie w pokoju). W wojnie to biezaca wojna z 60 dob,
   w pokoju koncowka ostatniej wojny (gdy byla krotsza niz 60 dob - takze koncowka poprzedniej). Jeden licznik na rod, maly zapis.
2. Projekt nie mowi, co przed pierwsza wojna: rod, ktory widzial k < 60 dob wojny tej korony, potrzebuje ceil(20 k / 60) dob; k = 0 - warunek sluzby nie obowiazuje
   (nie bylo wojny, w ktorej mozna sluzyc). Bez tego w nowej kampanii przez pierwsze 20 dob kazdej pierwszej wojny nikt nie mialby renty, a stary zapis (licznik
   od zera) wstrzymalby renty wszystkim na 20 dob wojny. Nowy wasal (zmiana krolestwa) liczy od zera - sluzba innej koronie sie nie liczy.
3. Twierdza w rekach obecnego pana krocej niz 28 dob (zdobyta, nadana) i twierdza oblezona - pomijane w warunku zalogi (stala 28 dob, bez klucza). Powod:
   swiezo zdobyta twierdza ma zaloge bliska 0 - zdobywca, czyli wzor sluzby, tracilby cala rente. Twierdze oblezone tez poza norma krolestwa.
4. `CrownRentGarrisonNormKingdom` = false -> norma swiata dla rodzaju twierdzy (nie wlasna srednia rodu - K10 to odrzucil).
5. Sluzba: "bitwa z wrogiem" i "obrona oblezonej osady" dopisane obok armii/oblezenia/ziemi wroga (rod broniacy swojej twierdzy albo bijacy najezdzce na
   wlasnej ziemi sluzy). Armia krolestwa liczy sie takze, gdy stoi na wlasnej ziemi (projekt: "w armii krolestwa").
6. Wagi wedlug wlasnosci gry (`Clan.Settlements`), nie tytulow BK - jak dar 182.
7. Renta z calej reszty po zwrocie, takze z 1/360 zapasu (projekt: "renty roku 1 placi w duzej czesci zapas koron").

**Test do odczytu (C2: 40 dob + zapis 362; 120 dob po C3):**
- `Renty korony (180)`: co dobe P = R + W (co do 1 zl) i W = A + B; "bez warunku" < 20% (K41/projekt). Te same R i W w `Korona: wplywy dnia (165)` (renty, wstrzymane)
  i w `Obieg` (renty od korony = renty wedlug lenn = R).
- "zostalo z wplywow dnia w skarbcach" (165) spada do okolo W.
- `Pieniadz swiata`: bez zmiany tempa (renty to przelew skarbiec -> glowa; +-50 tys./dobe wobec C1).
- `D staly (169c)`: "korona" rosnie o ok. R; "renty korony (180)" = R tej doby; zamkniecie sumy bez zmian.
- Z8 gracza < 1 (`Obieg`) - renta nie zalezy od zalogi ani zoldu.
- Zapis/wczytanie: pierwsza doba po wczytaniu - "wczytano: sluzba N rodow (bledne 0)"; stary zapis (C1) - sluzba od zera, renty od pierwszej doby (warunek proporcjonalny).

**Czego sie spodziewac (z biegu C1-w, `kopia-c1w-120`, linia 165: "zostalo z wplywow dnia"):** reszta po zwrocie doby 1-30 ok. 143 tys./dobe (glownie
krolestwa w pokoju), 31-60 ok. 81 tys., 61-93 ok. 61 tys., 94-120 ok. 76 tys. (z tego ok. 24 tys. z 4 krolestw w pokoju, reszta z ok. 17 krolestw w wojnie,
ktore zwracaja zold w calosci). Renty = ta reszta minus wstrzymane (szacunek 10-25%): w dobach 94-120 ok. 55-70 tys./dobe, na starcie kampanii ok. 110-130 tys.
D rodow z lennem w tych krolestwach rosnie o rente (czesc "korona"); u rodow na pulapie 166 w wojnie +0.6 x renta pulapu (+ udzial dworu przy WarCourtYieldsToWages),
z opoznieniem pierscienia 28 dob; renty w pokoju podnosza G, a z nim skrzynie wojenna na start wojny. Szacunek: wojsko lordow w wojnie +3..+6 tys. wobec C1-w
(doby 94-120), glownie w krolestwach z nadwyzka (bogate); krolestwa, ktore nie zwracaja zoldu w calosci (ok. 8 z 25 w wojnie), renty nie dostana.

**Ryzyka:** (a) "bez warunku" > 20%: male zalogi zamkow wobec normy krolestwa (zamki z mala zaloga wojenna, w pokoju po cieciu 166 do 50%) i rody, ktorych
partie stoja we wlasnych osadach; (b) sprzezenie biedy: biedny rod -> mniejsza zaloga -> bez renty; (c) zapas korony rosnie wolniej (dotad niewydana reszta
szla do zapasu i wracala po 1/360) - "do wydania" w dobie 120 nizsze o ok. 20-30 tys./dobe, glownie w krolestwach z nadwyzka; (d) koszt: szukanie najblizszej osady
dla partii w wojnie (ok. 0.5 mln odleglosci na dobe, kilka ms) - nie mierzone w linii; (e) gracz: komunikat o rencie codziennie.

**Recenzja C2 (wlasna, diff 4e4e627..b649bbe; poprawki w osobnym commicie):** sprawdzone - zloto (kazda wyplata w parze, nieudana oddaje skarbcowi, pula <=
skarbiec, udzialy floor - zaokraglenia w skarbcu), D (renta raz: `TodayRent` w "korona", odjeta od "jednorazowych"; wyplata w bloku dobowym - `MoneyLedger`
nie liczy jej drugi raz jako zdarzenia, `ChangeHeroGold` nie odpala zdarzen gry), powinnosci (od "ziemi" - bez rent), zapis (napis przez `SaveText.Sync`, stary
zapis bez klucza), gracz (te same warunki, `MainParty`). Poprawione:
1. **Warunki liczone u wszystkich rodow z lennem codziennie**, takze w krolestwach bez reszty po zwrocie (renta 0): dotad linia i CSV liczyly "bez warunku"
   tylko tam, gdzie korona miala z czego placic - miara testu ("bez warunku < 20%") zalezala od puli, a w wojnie (pula zwykle 0) prawie znikala. Nowy licznik
   w linii: "w krolestwach bez reszty po zwrocie - renta 0: n".
2. **Wylacznik = stan sprzed paczki takze dla stanu:** przy wylaczonym 180 (albo 165) sluzba, normy i przejecia twierdz sa czyszczone (nie zapisywane, bez
   starej maski po ponownym wlaczeniu - wlaczenie zaczyna jak nowa kampania: warunek proporcjonalny).
3. **Blad przy jednej partii** (`Serving` we wlasnym `try`) nie zabiera rodowi doby wojny - dotad wyjatek przerywal aktualizacje maski i licznika dob rodu.
4. **Komunikat gracza:** doba bez wpisu gracza nie kasuje ostatniego powodu (bez powtarzania "withholds your house's rent" przy pulach 0/>0 na przemian).
5. Opis "gracz: bez lenna w krolestwie (albo najemnik)"; tablice pozycji osad czyszczone w `Reset`.

**Uwaga do oczekiwanego skutku (D i R):** renta podnosi D, a z nim rezerwe wojny R = max(20 000; 20 D) + 5 000 x doroslych - u rodu z G > R skrzynia wojenna
0.8 (G - R)/45 maleje o ok. 0.36 zl na 1 zl renty dziennie; pulap +0.6 i (przy WarCourtYieldsToWages) udzial dworu +0.2. Netto ok. +0.45..0.65 zl zoldu na 1 zl
renty u rodow na pulapie; reszta renty zostaje w kiesach (skrzynia na nastepna wojne, dwor i budowy w pokoju). Szacunek wojska w wojnie (+3..+6 tys. wobec C1-w)
- dolna polowa widelek bardziej prawdopodobna.

---

## C3 - 179 rycerze bez lenna bez oddzialow - ZROBIONE (commit b159d70)

Projekt: rozdz. "179" (C3), 2.0b, 166 (pulap, hamulec nowych partii), decyzje Jeffa 09.10 ("rycerze bez lenna jezdza przy panu", "nic z kosmosu").
Build Release kod 0, `python -I tools/gen_mcm.py`, gra nie uruchamiana.

**Pliki:** nowy `Armoury/src/GentryService.cs`; `ClanBudget.cs` (zold rycerzy w pulapie partii, limit 0 partii rycerzy, blokada nowej partii, dezercja z
limitu zdjeta z partii rycerzy, `ToVillagePop`), `SoldierPay.cs` (`AddKnightPaid` - podstawa zwrotu), `ArmouryBehavior.cs` (krok dobowy przed korona, `Reset`,
`EnsureHooks`), `SubModuleMain.cs`, `MoneyLedger.Obieg.cs`, `Settings.cs` + `McmSettings.cs`.

**Co dziala (GentryNoParties = true):**
- Rod rycerza = rod BK gentry: id `gentryClan_`, bez miasta i zamku, BK `IsGentryClan` = mniejsze parostwo (gracz moze nadac pelne - wtedy rod pana jak dotad);
  osada majatku z BK (`Estate.EstatesData.Settlement`). Gracz nigdy, Inni nigdy.
- **Bez wlasnych druzyn:** latka nowej partii 166 (`SpawnLordParty`) - rod rycerza zawsze `null` (takze glowa, takze rod bez partii, takze nowa kampania);
  prefiks BK `SummonGentry` - BK nie wystawia partii z ludzi majatku (`TakeRetinue` - "nic z kosmosu", ludzie zostaja w ludnosci BK).
- **Wezwanie:** (a) BK wezwanie choragwi (AI i gracz): glowa rodu rycerza do druzyny pana (wlasciciel wsi majatku), gdy ta jest w tej armii, inaczej do
  druzyny wzywajacego (wodza armii); (b) AI codziennie: krolestwo w wojnie z krolestwem, pan prowadzi druzyne w armii krolestwa - do pana; pan bez partii
  albo poza armia - do wodza najblizszej (od majatku) armii krolestwa. Druzyna gracza tylko na wezwanie gracza; rycerze, ktorych panem jest gracz, tylko na
  wezwanie gracza. Dolaczenie: `AddHeroToPartyAction` (rycerz jedzie sam, ze swoim ekwipunkiem). Rycerz musi byc wolny: zywy, dorosly, nie w niewoli, nie
  w partii, walczacy, nie namiestnik. Druzyna celu: AI, czynna, nie w bitwie, nie rozwiazywana, strona krolestwa rycerza, nie Inni.
- **Powrot:** codziennie, gdy druzyna nie jest w armii, krolestwo nie jest w wojnie, druzyna zmienila strone, jest rozwiazywana albo zniknela -
  `TeleportHeroAction.ApplyImmediateTeleportToSettlement` do wsi majatku (wies w rekach wroga - najblizsza twierdza krolestwa); druzyna w bitwie - jutro.
- **Zold:** `GentryKnightWage` (24) dziennie za dobe w druzynie: glowa rodu druzyny (gracz - jego kiesa) -> glowa rodu rycerza, `GiveGoldAction` w parze,
  najwyzej tyle, ile platnik ma (reszta: "pan bez zlota"). Straz (`WatchUnpaid`) - 0. W pulapie zoldu partii 166 platnika (`KnightWage`: zold partii w
  `WPar`, limit gry partii = jej czesc minus zold rycerzy), w podstawie zwrotu 50% korony (`SoldierPay.AddKnightPaid` - WageRefund filtruje wojne i najemnikow),
  u rycerza `NoteInflow(KContract)` (D: czesc "kontrakt"). Krok dobowy przed `KingdomTreasury.Daily` - zold dnia jest w dzisiejszej podstawie zwrotu.
- **Stare partie rycerzy** (zapis sprzed 179): limit zoldu 0 (Daily, `ApplyPartyLimits`, postfiks oceny finansow takze bez budzetu) - bez rekrutow, awansow
  i dosypki BK z majatku (BK `OnSettlementEntered`: `TotalWage < PaymentLimit`); dezercja gry z limitu zoldu zdjeta (`DesertPrefix`), zwolnienia 166 jak
  dotad (do wsi). BK odsyla je do majatku i rozwiazuje (`FinishParty`) - prefiks: zolnierze do ludnosci BK wsi majatku (`ClanBudget.ToVillagePop`);
  sakiewka partii - jak dotad (`MenPurse.OnPartyDestroyed` do kasy miasta), jency i statki - liczone w linii (gra: bohaterowie wolni, reszta przepada).
- **Majatek (GentryEstateSpendCap):** prefiksy BK `TryAutoBuyForEstate` (niewolnicy) i `RefillFromTownMarket` (zaopatrzenie wsi): zakup tylko przy niewydanym
  przydziale sprzetu 166 rodu wlasciciela (`ClanBudget.GearCap`) i kiesie > `FamilyPurseFloor` (5000); wydane schodzi z przydzialu (`GearSpent`).
  Rod bez budzetu - jak dotad.
- **180 (sluzba):** rycerz w druzynie pana sluzy razem z nia - `CrownRents` liczy doroslych czlonkow rodu w partii innego rodu (`AliveLords`, `mp.ActualClan != c`
  -> `Serving(mp)`), wiec rycerz w armii = doba sluzby jego rodu. Bez zmian w 180 (rody rycerzy i tak zwykle bez wag lenn - renta 0).
- **Wylaczone:** rycerze w sluzbie wracaja do domu (bez zoldu, bez wezwan), latki BK przepuszczaja (BK jak dotad); linia tylko, gdy ktos wracal.
  Stan pochodny ze swiata (rycerz w partii innego rodu) - bez zapisu w SyncData.

**Nowe klucze (grupa "Knights without fiefs (stage 2)"):** `GentryNoParties` true, `GentryKnightWage` 24 (suwak 0-96), `GentryEstateSpendCap` true.

**Nowe / zmienione linie logu:**
- NOWA (kampania) `Rycerze (179): latki BK - wezwanie rycerza (BK SummonGentry), rozwiazanie partii rycerza (BK FinishParty), majatek: niewolnicy (BK TryAutoBuyForEstate), majatek: zaopatrzenie wsi (BK RefillFromTownMarket); BRAK: -; rozpoznanie rodow rycerzy: BK IsGentryClan TAK.`
- NOWA `Rycerze (179): dzien N | rody rycerzy (BK gentry bez lenna) N, w sluzbie M (w druzynie pana a, u wodza armii b, u gracza c), wolni w krolestwach w wojnie F (bez armii w krolestwie G) | wezwani dzis: pan w armii x, wodz armii y; wezwania BK (od wczoraj) S (do pana s1, do wzywajacego s2, rycerz niedostepny s3) | wrocili do majatku H, czeka (bitwa) W | armie A, z rycerzem B | zold rycerzy 24 zl: nalezny X, zaplacony Y (gracz P), pan bez zlota U, Straz bez zoldu Z | wlasne partie rycerzy O (ludzi L, limit zoldu 0), nowe partie zablokowane Q, rozwiazane przez BK R (ludzie do wsi majatku r1, bez wsi z danymi BK r2, jency w rozwiazanych r3, statki r4) | majatki BK (przydzial sprzetu 166): zakupy wstrzymane E, wydane V.`
- ZMIENIONA `Obieg: dzien N` (rody wydaly): po "kiesa rodziny czlonek -> glowa" - `, zold rycerzy (179) pan -> rycerz X` (= "zaplacony" z linii 179).
- Bez zmian, potrzebne do testu: `Wydatki rycerzy (169c)` (rody gentry, bez ludzi, glowy < 5000, niewolnicy majatku), `Budzet rodow (166)`, `Korona: dzien N - zwrot zoldu`.
- Gracz (po angielsku): przy wezwaniu "<rycerz> of <rod> rides with you as a knight of your banner - 24 denars a day while your army is in the field.";
  co 7 dob "Your knights' wages this past week: N denars (24 a day for each knight riding in your party)."

**Odstepstwa / rozstrzygniecia (z powodem):**
1. Wezwanie AI codziennie (pan w armii krolestwa albo wodz najblizszej armii), nie tylko przez BK `SummonGentry`: BK wola wezwanie tylko dla wodza z >= 2
   choragwiami i zapasem wplywow - bez tego rycerze prawie nigdy by nie jechali, a test projektu wymaga "rycerzy w druzynach panow > 0 przy kazdej armii".
   Armie gry (AI krolestwa) to wezwanie choragwi krolestwa.
2. Zold placi glowa rodu druzyny, w ktorej rycerz jedzie (pan albo wodz armii) - projekt: "pan placi"; u wodza armii panem rycerza na czas wyprawy jest wodz.
3. Wariant zapasowy projektu (rycerz-zolnierz t5 z majatku) - nie uzyty: gra i BK licza bohatera obcego rodu w partii jak towarzysza (BK `BKPartyWageModel`:
   glowa rodu bez zoldu gry - zold tylko nasz, bez dubla; `HeroSpawnCampaignBehavior` nie rusza bohatera w partii; BK sadza w majatku tylko bohaterow bez
   partii; smierc i niewola - jak kazdego bohatera w partii). Ryzyko R7 zostaje do testu (0 bledow).
4. Wezwanie samych rycerzy przez BK nie tworzy armii (rycerz nie jest partia - BK liczy `army.Parties < 2`): gracz musi wezwac tez pana z druzyna; rycerz
   dolaczony do niedoszlej armii wraca nastepnego dnia.
5. Zakupy majatkow BK w przydziale sprzetu 166 dla wszystkich rodow z budzetem (jedna regula, tabela 166 "zaopatrzenie majatkow BK w pulapie sprzetu") -
   w tescie C2 placili tylko rycerze (`Wydatki rycerzy`: niewolnicy 3 536/dobe = "wszyscy"), wiec skutek ten sam. Osobny pulap "majatek" 0.10 D - nie dodany
   (przydzial sprzetu rycerza 0.17 D jest wolny - rycerz nie ma druzyny).
6. Jency i statki starych partii rycerzy przy rozwiazaniu przez BK - jak dotad (gra: bohaterowie wolni, szeregowi i statki przepadaja), tylko licznik w linii;
   dotyczy jednorazowo partii z zapisu sprzed 179 (nowe nie powstaja).

**Czego sie spodziewac w tescie (40 dob / 120 dob):**
- `Rycerze (179)`: "wlasne partie rycerzy" maleja do 0 w ok. 10 dob (stare partie wracaja do majatku po armii); "nowe partie zablokowane" > 0 codziennie
  (gra probuje co dobe); `Wydatki rycerzy (169c)`: "bez ludzi" = "rody gentry" od ok. doby 10.
- W wojnie (ok. 300 z 312 rodow w d120): "w sluzbie" ok. 30-60 rycerzy (rycerze wolni w krolestwach z armia), "armie z rycerzem" bliskie "armie";
  zold rycerzy ok. 0.7-1.5 tys./dobe w swiecie (projekt: 0.4-0.7 tys. - wiecej, bo wezwanie AI codziennie).
- Ludzie w druzynach lordow: minus ludzie dawnych partii rycerzy (w C2 d120 10 partii, w bazie 12-18 partii ok. 24 ludzi = ok. 0.3-0.5 tys.).
- `Wydatki rycerzy (169c)`: "BK niewolnicy majatku" spada (przydzial 0.17 D), "glowy < 5000" -> 0 (cel testu 120 dob: 0); kiesy rodzin rycerzy rosna.
- `Renty korony (180)`: rody z lennem bez zmian (rycerz sluzy razem z druzyna pana - dzien sluzby jego rodu; rody rycerzy zwykle bez wag).

**Ryzyka:** (a) R7 - bohater obcego rodu w partii AI (ekran druzyny gracza, smierc pana, rozbicie partii z rycerzem) - test: 0 bledow, "czeka (bitwa)" nie
rosnie bez konca; (b) rycerz teleportuje sie do armii i z niej (jak BK sadzajacy rodzine w majatku) - bez drogi po mapie; (c) zakupy majatkow wolniejsze
(niewolnicy BK = dochod majatku w przyszlosci); (d) gdy BK nie da sie rozpoznac (`IsGentryClan` BRAK) - rozpoznanie tylko po id i braku lenna.

---

## C3 - 183 dezercja AI wedlug poziomu - ZROBIONE (commit 6273048)

Projekt: rozdz. "183" (C3), 2.0b, OTWARTE nr 2, odpowiedz Jeffa 09.10 11:05 pkt 1 (podloga 30 ludzi takze dla druzyny gracza). Build Release kod 0,
`python -I tools/gen_mcm.py`, gra nie uruchamiana.

**Pliki:** `DesertionLaw.cs` (AI pod prawem dezercji, liczniki AI `DesertionAi`), `WarLedger.cs` (stawka AI, podloga, linia doby), `Settings.cs` + `McmSettings.cs`.

**Co dziala:**
- `DesertionLawForAi` -> **true**: progi morale wedlug tieru (t1 < 25, -3 na tier, podloga 10; 1%/pkt ponizej progu, sufit 25%/dzien) dla wszystkich partii
  AI, ktore gra przepuszcza przez model dezercji (`DesertionCampaignBehavior`: partie lordow, zalogi, karawany) - jak u gracza i jego rodu. Inni (`Undead.Party`)
  zostaja przy grze. Limit zoldu i przepelnienie - wprost z gry (166 dalej zdejmuje limit zoldu u rodow z budzetem - `DesertPrefix`, bez zmian).
- `WarLedger`: stawka zaleglego zoldu dla AI pelna (`WarLedgerAiHalf` false; dotad x0.5 dla kazdej partii poza druzyna gracza), sufit 8 dni bez zmian;
  **podloga** `WarLedgerMinMen` (30): przez zalegly zold partia nie schodzi ponizej 30 ludzi (`TotalManCount`, z bohaterami) - odchodzi najwyzej nadwyzka
  ponad 30; dla druzyny gracza i partii jego rodu przy `WarLedgerMinMenPlayer` (true). Dezerterzy dalej do puli wyrzutkow i ksiegi ludzi (T4, bez zmian).
- AI bez linii "DesertionLaw: <partia> ..." na partie (byloby kilkadziesiat linii na dobe) - liczniki doby w nowej linii; gracz i jego rod - jak dotad.
- Wylaczniki = stan sprzed paczki: `DesertionLawForAi` false, `WarLedgerAiHalf` true, `WarLedgerMinMen` 0.

**Nowe klucze:** grupa "Desertion": `DesertionLawForAi` true (byl false); grupa wojny (obok `WarLedgerToOutlaws`): `WarLedgerAiHalf` false, `WarLedgerMinMen` 30
(suwak 0-120), `WarLedgerMinMenPlayer` true. Opis `WagesDesertPercentPerDay` poprawiony (AI juz nie "polowa").

**Nowe / zmienione linie logu:**
- NOWA (raz na dobe, z `WarLedger.OnDaily`) `Dezercja AI (183): dzien N | prawo dezercji wedlug poziomu dla AI: TAK (progi t1<25 t2<22 ..., 1.0%/pkt, sufit 25%) | z morale (prawo): partie lordow a, zalogi b, karawany i inne c ludzi (tier 1-2 x, 3-4 y, 5+ z; w glodzie g; najnizsze morale partii z dezercja M), partii z dezercja: lordow p, zalog q, innych r | limit zoldu i przepelnienie (gra, w tych partiach) L | zalegly zold (WarLedger): AI W ludzi w K partiach, stawka AI pelna, podloga 30 ludzi zatrzymala: AI F w n partiach, gracz i jego rod G w m | prog 183: dezercja AI z morale i zaleglego zoldu (linia 169c 'AI morale i zalegly zold') <= bieg bazowy + 50%.`
  Liczniki modelu - od poprzedniej linii (dezercja gry biegnie w dobowym ticku partii).
- ZMIENIONA w tresci (kod bez zmian): `Dezercja AI wedlug przyczyny (169c)` - "morale" u AI to teraz prawo dezercji (wczesniej gra ponizej 10); koncowka
  "prawo dezercji dla AI (DesertionLaw): tak". To jest miara progu 183 ("AI morale i zalegly zold").
- ZMIENIONA przy starcie: `DesertionLaw: model dodany - ..., AI: tak.`
- Znika: linie `DesertionLaw: <partia AI> morale ...` (zostaja dla gracza i jego rodu).

**Odstepstwa / rozstrzygniecia:**
1. Prawo obejmuje tez zalogi i karawany AI (model gry pyta o nie tym samym wywolaniem; u gracza obejmowalo je od zawsze - jedna regula). Projekt mowi "AI
   jak dla gracza" bez rozroznienia rodzajow partii.
2. Podloga liczona od `TotalManCount` (ludzie z bohaterami, jak "ludzi" w linii WarLedger) - partia 30 ludzi z lordem traci przez zalegly zold najwyzej do 30.
3. Podloga dotyczy tylko zaleglego zoldu (WarLedger) - projekt; dezercja z morale, przepelnienie i zwolnienia 166 jej nie maja.

**Baza progu (linia 169c "AI morale i zalegly zold", srednia dobowa 120 dob):** bieg bazowy `kopia-baza120` 41.7/dobe (partie lordow: morale 32.0, morale
w glodzie 7.4, WarLedger 1.8), `kopia-e2b2-120` 37.8/dobe, C1+C2 (`kopia-c2-120`) 20.7/dobe (lordowie: morale 14.0, w glodzie 6.7, WarLedger 0.01).
Prog projektu "baza + 50%": od biegu bazowego ok. 62/dobe, od C2 ok. 31/dobe (srednia 28 dob). WarLedger AI <= 1 000 ludzi w 120 dobach (C2: 1).

**Czego sie spodziewac:** dezercja AI z morale ok. 1.5-3 x C2 (prog nizszy od gry: 25 zamiast 10 dla t1, wyzsza stawka ponizej 10: t1 przy morale 5 - 20%/dobe,
gra ok. 8%) - ok. 30-60 ludzi/dobe, glownie tier 1-2 i partie w glodzie; zalogi AI moga dezerterowac w oblezeniu (glod). WarLedger AI bez zmian w praktyce
(z budzetem 166 zaleglosc AI prawie nie wystepuje), podloga zatrzyma pojedyncze partie.

**Ryzyka:** (a) przekroczenie progu wobec C2 (31/dobe) - wtedy wedlug projektu "naprawiamy morale, nie dezercje" (np. niewyspani T10, glod); test: linia 183
"najnizsze morale partii z dezercja" i "w glodzie"; (b) zalogi AI w oblezeniu z glodem traca ludzi szybciej niz dotad (gra: ponizej 10).

---

## C3 - 180m pomiar sluzby rent (obserwacja z testu C1+C2) - ZROBIONE (commit fdb9d43, poprawka 1238127)

**Dane (`kopia-c2-120`, `budzet-rodow.csv` kolumna `warunek_180`, d120):** 94 z 215 rodow z lennem bez warunku - wszystkie przez sluzbe (zaloga 0).
Wedlug krolestw: **Zelazne Wyspy 10/10 rodow 0/20, Dolina 12/12 rodow 0/20** - oba krolestwa w wojnie od tej samej doby 108858 (98 dob wojny), w `bitwy.log`
zadnej bitwy lordow miedzy nimi (Dolina bije bandytow Ksiezycowych Braci, Zelazne Wyspy - piratow; jedyny styk: Lysa Arryn rozbija karawane Zelaznych Wysp
w d108858) - wojna z krolestwem za morzem, w ktorej nikt nie moze sluzyc. Dorzecze 14 rodow: 0/16 przez ok. 45 dob wojny mimo potyczek lordow z karawanami
Krain Burzy (d108929, 108932, 108942, 108949) - tick widzi tylko chwile doby, bitwa trwa godziny; sluzba pojawila sie dopiero z armia (8-10/19 w ostatnich
9 dobach). Krolestwa z prawdziwym frontem (Reach, Stormlands, Dorne, Braavos, Norvos) - prawie wszystkie rody z warunkiem.

**Poprawka pomiaru (regula 20 z 60 dob wojny bez zmian):**
1. `CrownRentServiceWholeDay` (true): bitwa, rabunek albo szturm z krolestwem w wojnie liczy sie o kazdej porze doby (`MapEventStarted`/`MapEventEnded` -
   rody druzyn lordow i ich czlonkow w druzynach innych rodow, np. rycerzy 179); w chwili ticku takze poscig za partia wroga (`EngageParty`) i odsiecz
   oblezonej albo rabowanej osady krolestwa (`DefendSettlement`) - "obrona wlasnej ziemi przed wrogiem w poblizu".
2. `CrownRentWarDayNeedsContact` (true): doba wojny przesuwa maske sluzby (i "widziane doby wojny") tylko, gdy krolestwo naprawde prowadzilo wojne:
   armia krolestwa w polu (tick), oblezenie miedzy krolestwem a wrogiem (tick), albo zdarzenie od wczoraj: rabunek, wymuszenie we wsi, szturm/wypad/blokada,
   bitwa z lordami po obu stronach. Sama potyczka z karawana albo chlopami nie czyni doby wojny (Dolina/Zelazne Wyspy). Doba bez styku = jak doba pokoju.
3. Zapis `arm_rent180` v2; zapis v1 (C2) przy wlaczonym 2. - sluzba rodow od zera (maski v1 maja doby bez styku jako niesluzbe i nie wysunelyby sie nigdy);
   normy zalog i przejecia twierdz zostaja.
Armia/oblezenie po stronie sojusznika - nie zmienione: armia gry sklada sie tylko z partii jednego krolestwa, a oblezenie (`BesiegerCamp`) liczylo sie
juz dla kazdej partii.

**Nowe klucze (grupa "The crown's income (stage 2)"):** `CrownRentServiceWholeDay` true, `CrownRentWarDayNeedsContact` true (oba false = pomiar C2).

**Zmieniona linia `Renty korony (180)`:** w "sluzba dzis" - `..., niewola n, poscig i odsiecz g, bitwa w ciagu doby (zdarzenie) e); pomiar z calej doby TAK (zdarzen bitew z wrogiem od wczoraj E); doba wojny bez styku z wrogiem (nie liczy sie) K krolestw, R rodow`;
przy wczytaniu zapisu C2: `| wczytano: sluzba 0 rodow (bledne 0) - zapis C2 (v1): sluzba od zera, nowy pomiar`.

**Czego sie spodziewac (test 120 dob):** "bez warunku" ponizej 20% (projekt/K41): Zelazne Wyspy i Dolina bez wymogu sluzby, dopoki ich wojna nie ma styku
(rody 0 widzianych dob - warunek nie obowiazuje); krolestwa z frontem - wiecej dob sluzby (bitwy poza tickiem), rody siedzace w domu nadal bez renty.
Renty rosna (mniej udzialow wstrzymanych w skarbcach) - "wstrzymane w skarbcach" spada, `Pieniadz swiata` bez zmiany tempa (przelew skarbiec -> glowa).

---

## C3 - recenzja wlasna (diff 1220510..1238127; poprawki w 1238127)

Sprawdzone: zloto (zold rycerza `GiveGoldAction` w parze, najwyzej kiesa platnika; zwrot 50% przez `AddPaid` - WageRefund filtruje wojne i najemnikow;
majatek - tylko blokada zakupu), ludzie (rycerz bez zolnierzy i sprzetu - "nic z kosmosu"; stare partie: ludzie do wsi majatku, sakiewka - jak dotad
do kasy miasta, jency i statki - licznik, jak dotad w BK), dezerterzy 183 - pula wyrzutkow i ksiega ludzi gry (bez zmian), wyjatki (kazdy rod, wezwanie,
wyplata, zdarzenie i latka we wlasnym try; blad w prefiksie BK - BK jak dotad), ROT/BK (Inni poza 179/183; Straz - rycerz bez zoldu, dezercja jak u wszystkich;
rycerze Dothrakow i Wolnych Ludzi - ta sama regula; rody BK z pelnym parostwem od gracza - nie rycerze), gracz (rycerze gracza tylko na jego wezwanie,
gracz placi 24 zl, komunikat co 7 dob; podloga 30 ludzi takze dla niego), zapis (179/183 bez stanu, 180 v2 przez SaveText), wylaczniki (179: rycerze
wracaja do domu, latki przepuszczaja; 183: `DesertionLawForAi` false, `WarLedgerAiHalf` true, `WarLedgerMinMen` 0 = stan sprzed; 180m: oba false =
pomiar C2), wydajnosc (pamiec rodow rycerzy raz na dobe; skan rosteru tylko przy > 1 bohaterze w partii; zdarzenia bitew - petla po partiach stron).

**Poprawione (1238127):**
1. **Przepelnienie przez rycerza** - pelna druzyna + rycerz = 1 ponad limit, a gra (`GetTroopsToDesertDueToWageAndPartySize`) wypedza wtedy co dobe zolnierza
   (do 30-60 ludzi/dobe w swiecie). Postfiks na `GetPartyMemberSizeLimit` czynnego modelu: limit +1 na rycerza w sluzbie ("Knights of the banner").
   Linia startowa 179: dodatkowa latka `miejsce rycerza w druzynie (GetPartyMemberSizeLimit)`.
2. **BK z cudzej petli** - `IsGentryClan` (BK `GetCouncil`) nie jest juz wolany z latek gry (limit wielkosci partii, ocena finansow, dezercja) - pamiec rodow
   rycerzy odswiezana tylko w ticku dobowym i przy starcie sesji. Bezposrednio (bez pamieci) tylko w `SpawnLordParty` i BK `SummonGentry`.
3. **Gracz:** partie rodu gracza nie sa celem wezwania AI; przy wezwaniu AI krola rycerz gracza jedzie u wzywajacego (gracz nie placi za cudze wezwanie).
4. **Oblezenie:** rycerz nie wyjezdza z oblezonej osady; z druzyny w bitwie albo w oblezeniu nie wraca do domu (czeka do konca - placony za dobe).
5. Liczniki linii 179 zerowane takze przy wylaczonym logu; zapis C2 zeruje sluzbe tylko przy `CrownRentWarDayNeedsContact`.

**Uwagi bez zmian w kodzie:** (a) D staly - zold rycerza w czesci "kontrakt" (obok kontraktu najemnika 185); linia `D staly (169c)` "kontrakt" obejmuje teraz
oba. (b) Ryzyko 183: zalogi AI maja morale gry 50 +/- (glod w oblezeniu, zima) - przy progu t1 < 25 glodne zalogi w oblezeniu traca ludzi szybciej niz
w grze; linia `Dezercja AI (183)` liczy zalogi osobno. (c) Ryzyko R7 (179) - bohater obcego rodu w partii AI: wymaga autotestu (0 bledow), szczegolnie
smierc pana, rozbicie i niewola druzyny z rycerzem, ekran druzyny gracza z rycerzem.

---

## C3 po tescie 120 dob (kopia-c3-120 wobec kopia-c2-120; poprawka 179 w a5063af)

Dane: `kopia-c3-120/Armoury-2026-10-10_02-44-22.log` + CSV, `kopia-c2-120/Armoury-2026-10-10_01-14-58.log` + CSV; skrypty `scratchpad/c3diag/*.py`.
Build Release kod 0, gra nie uruchamiana.

### Problem 1 (blad): rycerze nigdy nie jechali - POPRAWIONE (a5063af)

**Linie `Rycerze (179)` (121):** rody rycerzy 92 codziennie; w sluzbie 0, wolni w krolestwach w wojnie 0, bez armii 0 - przez caly bieg; armie 1-19
(srednio 10.2), z rycerzem 0; wezwania BK 164 w biegu - wszystkie "rycerz niedostepny"; wlasne partie 0, nowe partie zablokowane 177 (0-2 na dobe); potkniecia 0.
Czyli kazdy z 92 rodow odpadal na filtrze `Free()` (gdyby nie - bylby "wolny" albo "bez armii").

**Przyczyna:** `Free()` wymagal `h.IsActive`. Glowy rodow BK gentry sa w stanie `NotSpawned`: gra aktywuje bohaterow raz, przy tworzeniu swiata
(`HeroSpawnCampaignBehavior.OnNewGameCreatedPartialFollowUp`, i == 0), a BK tworzy rody rycerzy pozniej (`OnCharacterCreationIsOver` -> `InitializeGentry`
-> `CreateGentryClan` -> `HeroCreator.CreateSpecialHero`, stan poczatkowy `NotSpawned`) i co tydzien sadza rodzine w majatku przez
`EnterSettlementAction.ApplyForCharacterOnly`, ktora stanu nie zmienia. Aktywuja tylko `TeleportHeroAction`, dorastanie (`OnHeroComesOfAge`), ucieczka
z niewoli - albo dawniej wlasna partia BK i jej rozwiazanie. Po 179 (bez partii) glowy rodow rycerzy nie stawaly sie czynne nigdy. Slad w danych: gra
(`ConsiderSpawningLordParties` -> `GetBestAvailableCommander`, tez wymaga `IsActive`) probowala wystawic partie rycerzom tylko 0-2 razy na dobe
(dorosle dzieci po `OnHeroComesOfAge`), przy czynnych glowach bylaby to prawie kazda doba kazdego rodu. Drugi mozliwy filtr: `IsNoncombatant` (gra: zadna
umiejetnosc broni >= 100) - bez danych szablonow BK nie do rozstrzygniecia z logu; zdjety (projekt: "jedzie glowa rodu"; BK przy wezwaniu AI tez go nie sprawdza).
**Sciezka BK `SummonGentry`:** BK AI (`CallBannersGoal.DoAiDecision`) wzywa wszystkich wasali z majatkiem bez `IsAvailableForSummoning`; nasz prefiks
odrzucal ich tym samym `Free()` - stad "rycerz niedostepny" 164. Ta sama poprawka.

**Poprawka (`GentryService.cs`):**
1. `Free()`: stan czynny albo `NotSpawned` (`CanRide`); ucieczka, podroz, wylaczony - czekamy, az gra przywroci bohatera. Bez warunku "walczacy".
2. `Join()`: `NotSpawned` -> `Active` przed dolaczeniem (jak kazdy lord w druzynie; powrot `TeleportHeroAction` i tak aktywuje). Dziala w wezwaniu AI
   (codziennie) i w prefiksie BK `SummonGentry`.
3. Linia `Rycerze (179)` (zmieniona): po "wolni w krolestwach w wojnie F (bez armii G)" - `, niedostepni w krolestwach w wojnie N (niewola a, wlasna partia b,
   stan gry - ucieczka, podroz, wylaczony c, oblezona osada d, inne e), niewalczacy w sluzbie x`; po "wezwani dzis: ..." - `; pierwszy wyjazd (BK NotSpawned
   -> czynny, od wczoraj) w`. Kontrola: rody w krolestwach w wojnie = w sluzbie + wolni + niedostepni.

**Skutek uboczny (oczekiwany):** rycerz po pierwszej wyprawie jest czynny jak kazdy lord bez partii: gra probuje co dobe wystawic mu partie (latka 166
`SpawnLordParty` -> `BlocksSpawn`, licznik "nowe partie zablokowane" wzrosnie do kilkudziesieciu na dobe), czasem przenosi go do innej osady krolestwa
(`OnHeroDailyTick`), BK co tydzien sadza z powrotem w majatku. Wezwanie liczy odleglosc od majatku - bez zmian.

### Problem 2 (pomiar): wojsko lordow w wojnie C2 101.8 tys. -> C3 90.7 tys. - rozklad (bez zmian w kodzie 183 i 180m)

**Wskaznik `sprawdz_logi` (srednia dob 94-121):** C2 101 796 (26.0 krolestw w wojnie), C3 90 701 (26.5) = **-11.1 tys.**

| Przyczyna | Tys. ludzi | Dowod |
|---|---|---|
| Los: Dorzecze w pokoju w C3 | -6.1 | C2: wojna w dobach 45-61 i 89-121, srednio 6 084 ludzi w dobach 94-121; C3: wojna 57-92, pokoj od 93 (1 529; doba 93 "reszta" -4 042 = ciecie 166 do pokoju). Krolestw w wojnie 26.5 w C3, bo bunty licza sie osobno: Norvos + Konfederacja Quarro 2 415 -> 2 491, Tyrosh + Spisek Ryndoon 1 484 -> 1 563 - bez straty. |
| 179 (zgodnie z projektem) | -0.4 | partie rodow gentry w tych krolestwach: C2 395 ludzi, C3 0. |
| 183 dezercja AI z morale | ok. -2.4 | AI lordowie (169c: morale + morale w glodzie) C2 2 483, C3 4 862 w 120 dobach; 73% w dobach 54-88 (C2 47%). Przyczyna morale: dlug snu T10 = 3 (morale -95%, ok. 3-5) u 11-15 partii AI przez ok. 2 tygodnie (NocnyMarsz "z dlugiem teraz 3" w dobach 66-82: 11-15; probki kar: Oberyn Martell, Obara i Nymeria Sand - Dorne -1.4 tys.) plus glod. C2 mial taki sam epizod (12 partii, doby 51-60) przy dezercji gry 50-100/dobe. |
| Smierc w bitwach (los) | ok. -2.2 | zabici z partii rodow C3 34.6 tys., C2 32.2 tys.; duze bitwy C3 w dobach 75, 79, 84, 90, 95 (1.2-1.6 tys. zabitych na dobe). |
| 180m | ok. 0 | renty dob 94-121 C3 41.8 tys./dobe, C2 23.0 tys. (bez warunku 53 wobec 102 rodow); pulap 166 bez Dorzecza 1.548 mln wobec 1.517 mln (+2%), G +3.8%; zold partii 463 tys. wobec 496 tys. - pulap uzyty w 42% (C2 46%): pieniadz nie ogranicza wojska. |
| Reszta (werbunek, zwolnienia 166) | ok. 0..-0.5 | zwolnienia 166 z partii C3 18.9 tys., C2 24.3 tys. w biegu (mniej). |

Kontrola przeplywami (linia `Ludzie`, wszystkie partie rodow, srednia dob 94-121): stan C3 - C2 = -10.4 tys. = start +1.0, werbunek -6.9 (glownie
Dorzecze: wojenny werbunek w C2, pokoj w C3), zabici -2.2, dezercja -2.4, reszta +0.1.

**183 - czy wycina za duzo:** AI ma dokladnie to samo prawo co gracz (`TierDesertionModel.Governs` - jedna sciezka, ta sama tabela i podloga 30) - decyzja
Jeffa spelniona, bledu liczenia brak (linia `Ludzie` "dezercja partie rodow" = 169c: 740 w dobach 94-121). Oblezenia: zalogi AI z morale 0 ludzi
w calym biegu. Prog projektu (169c "AI morale i zalegly zold", srednia 28 dob): ostatnie 28 dob 23.2/dobe (C2 22.3, baza 41.7 -> prog 62) - TAK; najgorsze
okno (od doby 69) 116.6/dobe - ponad prog (C2 najgorsze 51.7). Przy morale 0-5 prawo daje t1-t3 14-25%/dobe, gra ok. 8-15%; miedzy 10 a 25 gra nic.
Wedlug projektu (rozdz. 183): "wiecej niz prog - naprawiamy morale, nie dezercje" -> **do decyzji (poza C3):** partie AI z dlugiem snu 3 przez
2 tygodnie (T10; "zapasc trwa", Dorne). Kod 183 bez zmian.

**Zalogi:** swiat 81.7 -> 77.5 tys. (-4.2 tys.): Dorzecze w pokoju 3 347 -> 1 296 (-2.05 tys., cel pokojowy 166), krolestwa buntow trzymaja 1.7 tys.
(twierdze Norvos 5 -> 3, Tyrosh 5 -> 4), reszta ok. -2.2 tys. (na twierdze bez Dorzecza 367.9 -> 355.9, -3.3%). Nie 183 (z morale 0, z limitu zoldu
i przepelnienia 249 wobec 350 w C2), nie 180m (zold zalog 192 wobec 199 tys./dobe, pulap wyzszy) - los oblezen i szturmow.

### Czego sie spodziewac w kolejnym tescie 120 dob

- `Rycerze (179)`: "pierwszy wyjazd" > 0 przy pierwszych armiach (ok. doby 7-25), potem prawie 0; "w sluzbie" w okresie wojny ok. 40-80 (92 rody, armie
  srednio ok. 10 na dobe), "armie z rycerzem" bliskie "armie" (poza krolestwami bez rycerzy); "niedostepni" pojedyncze (niewola, stan gry);
  zold rycerzy ok. 1-2 tys./dobe (= `Obieg` "zold rycerzy (179)"); "nowe partie zablokowane" do kilkudziesieciu na dobe (oczekiwane);
  "niewalczacy w sluzbie" pokaze, czy `IsNoncombatant` byl drugim filtrem.
- 0 bledow - pierwszy prawdziwy test R7 (bohater obcego rodu w partii AI: smierc pana, rozbicie i niewola druzyny z rycerzem, ekran druzyny gracza).
- Wojsko lordow: 179 -0.4 tys. jak w projekcie; 183 zalezy od epizodow dlugu snu 3 (T10); 180m ok. 0. Wskaznik "w wojnie" zalezy od skladu wojen -
  jedno krolestwo jak Dorzecze to +-6 tys.

### Test 120 dob z a5063af (kopia-c3b-120, 2026-10-10_03-42-12) - rod rycerza placil zold druzyny pana - POPRAWIONE (5ce5faf)

**Wynik:** rycerze jada (w sluzbie do 50, srednio 23 na dobe, 0 bledow), ale glowy < 5000 = 33 i wszystkie to rody gentry (Dorne 18, Baratheon 6, Pentos 9); w biegu C3
(bez a5063af) 0. Przyklad Astrethides (Pentos): od doby 65 (108901) `zold_partii` 1 389-1 452 zl/dobe przy `ludzi_partie` 0, kiesa 13 860 -> 56
w 16 dob, potem 0.

**Przyczyna:** gra (`DefaultClanFinanceModel.AddExpenseFromLeaderParty`) i BK (`EconomyPatches.ClanFinancesPatches.PartyExpensesPrefix`, zastepuje cialo
`AddExpensesFromPartiesAndGarrisons`) obciazaja rod zoldem partii, w ktorej JEST jego glowa (`clan.Leader.PartyBelongedTo`), a nie tej, ktora prowadzi.
Rycerz w druzynie pana albo wodza armii placil caly zold tej druzyny (`CalculatePartyWage(druzyna pana, kiesa rycerza)`); pan placil go tez w swoim
rozliczeniu. Rody gentry w biegu: `zold_partii` 1.93 mln zl w 2 895 rod-dobach, zwrot korony 50% tylko 0.61 mln (`Wydatki rycerzy (169c)` "zold": 11.5 tys.
/dobe w dobie 35, 37 tys./dobe w dobie 74). Bez tego (+zold - zwrot) wszystkie 33 glowy mialyby >= 5 000. Skutek uboczny: przy kiesie rycerza < 2 000
gra liczyla zold druzyny pana jako niezaplacony i nakladala na nia kare morale (`ApplyMoraleEffect`); zaplacone kwoty szly do sakiewek ludzi pana
(SoldierPay) - drugi raz.

**Poprawka (`GentryService.cs`):** okno "ktory rod jest teraz obciazany" - prefiks `Priority.First` (przed prefiksem BK) i finalizer na
`DefaultClanFinanceModel.AddExpensesFromPartiesAndGarrisons`; prefiks `CalculatePartyWage`: partia, w ktorej jest glowa obciazanego rodu, ale prowadzi
ja ktos inny i nalezy do innego rodu -> 0 dla tego rodu (bez kary morale, przekazania do sakiewek i zwrotu korony). Pan placi zold swojej druzyny
jak dotad i 24 zl rycerzowi (`Pay`). Wylaczone 179 - gra jak dotad. Linia `Rycerze (179)`: po "Straz bez zoldu" - `; zold druzyny pana zdjety
z rozliczen rodow rycerzy (gra liczy partie, w ktorej jest glowa rodu; od wczoraj) X zl w N`. Linia startowa 179: nowa latka
"zold druzyny pana nie z kiesy rycerza (AddExpensesFromPartiesAndGarrisons + CalculatePartyWage)".

**Jednorazowy spadek ok. 21 tys. w Pentos (doby 36-60, Astrethides 35 160 -> 13 800) - nie 179:** udzial w kosztach "wezwania do wojny" krolestwa
(gra `AddExpensesForCallToWarAgreements`: kazdy rod placi (-CallToWarWallet) x udzial; rod bez lenna 1 / (wagi lenn + 1 + rody) - tyle co pan bez lenna).
`Pieniadz swiata (rody - przyczyny)`: "wezwanie do wojny zaplacone" -57 459, -39 793, -27 557 ... (ok. x0.69 na dobe). To samo w biegu C3 bez poprawki
(doby 105-107, 10 rodow gentry Pentos). Zostawione (dyplomacja krolestwa, jedna regula dla wszystkich rodow); do rozwazenia w 165 (koszt wezwania
z korony, nie z kies).

**"Niewalczacy w sluzbie" = wszyscy:** to nie stan bohatera (NotSpawned/Active), tylko umiejetnosci: gra `IsNoncombatant` = zadna z broni (jednoreczna,
dwureczna, drzewcowa, rzucana, kusza, luk) >= 100; szablony BK "bannerkings_gentry_*" maja slabe umiejetnosci. Gra nie uzywa `IsNoncombatant` w bitwie
(tylko rozmowy, turnieje, teleport AI, dyplomacja, ocena dowodcy partii) - rycerz walczy w bitwie jak kazdy bohater w druzynie, tylko slabo. Bez zmian.

**Czego sie spodziewac w kolejnym tescie 120 dob:** `budzet-rodow.csv` - `zold_partii` rodow gentry 0 (rod bez partii); `Wydatki rycerzy (169c)` "zold" 0;
linia 179 "zold druzyny pana zdjety" ok. 30-70 tys. zl/dobe w wojnie (suma zoldow druzyn z rycerzem, liczona raz na rod rycerza); glowy gentry < 5000
w dobie 120: 0 (poza krolestwami z wezwaniem do wojny - tam tyle co inne rody bez lenna); kiesy rycerzy w sluzbie rosna o 24 zl/dobe.

---

## D - 168 dlug, kredyt wojenny i zajecie zamiast bankructwa (+ dodatek: wezwanie do wojny placi korona)

Projekt: rozdz. "168" (krok D; 2.12), 2.0b, 166 ("Dluznik", B5), odpowiedzi Jeffa; decyzja Jeffa 10.10 "tak, korona" (wezwanie sojusznika do wojny). Build Release
kod 0, `python -I tools/gen_mcm.py`, gra nie uruchamiana.

**Pliki:** nowe `Armoury/src/DebtLadder.cs` (ksiega i drabina, KW, zajecie, wyprzedaz, wymarli, portfele gry, B5, zapis `arm_debt168`, linia "Dlugi (168)"),
`Armoury/src/CrownCallToWar.cs` (wezwanie do wojny ze skarbca, zapis `arm_ctw168`, linia "Wezwania do wojny (168)"); zmienione `IronBank.cs` (limit i petle przy drabinie,
`LendKw`, menu gracza), `ClanBudget.cs` (KW w pulapie i wyplata, dluznik, D w zajeciu, B5 `PropertyBlocked`, linia 166), `SoldierPay.cs` (DebtToKingdom do ksiegi),
`CrownIncome.cs` (`TakeAdvanceAll`, pozycja wezwan w KDay i linii 165), `ClanIncomeBook.cs` (`OnceToday`, rodzaj wplywu `KRansom`, linia "Dlugi (na sucho)" i kolumna
`kandydat_pozyczki` z drabiny), `ClanIncomeBook.StableD.cs` (`VillageIncomeOf`), `MoneyLedger.Obieg.cs`, `ArmouryBehavior.cs`, `SubModuleMain.cs`, `Settings.cs` + `McmSettings.cs`, `tools/gen_mcm.py`.

**Co dziala (DebtLadderEnabled; czynne tylko z IronBankEnabled i ClanBudgetEnabled):**
- **Ksiega rodu:** dlug Banku (`IronBank.Debt`: kapital z odsetkami, oprocentowanie, wiarygodnosc) + roszczenia innych wierzycieli (`DebtLadder.Claim`): dlug zoldu
  (wierzyciel - ludzie partii, StringId partii), dlug wobec korony (stary zapis - do skarbca krolestwa), zaliczka gry (OBIEG-1 - splata w nicosc), okupy (178).
  Szczeble: kredyt (D1), zaleglosc (D2), zajecie (D3), wyprzedaz (D4).
- **Kredyt wojenny (KW) - jedyna pozyczka AI** (stara petla "pozycza, gdy brak na 10/20 dni zoldu" wylaczona): w `ClanBudget.Daily` po pulapie bez KW (z udzialem
  dworu w wojnie) - `DebtLadder.WarCreditRoom`: rod AI w wojnie (nie najemnik, nie Straz bez zoldu, nie gracz), G < R, D > 0, bez zaleglosci i zajecia, nie w 182 dobach po
  zajeciu, D pokrywa dwor + 3% powinnosci, wolny kapital Banku ponad `BankFreeCapitalFloor`, limit niewyczerpany. Miejsce = min(0.40 D, limit - dlug, kapital - prog)
  dochodzi do pulapu (limity partii rosna - werbunek). Wyplata dnia: zold naliczony (partie + zalogi + karawany) ponad pulap bez KW, najwyzej miejsce KW -
  `IronBank.LendKw` (kapital Banku -> kiesa glowy, oplata 2% i oprocentowanie jak kazda pozyczka, bez progu dni do terminu). Czesc KW w ksiedze (`L.Kw`).
- **Limit** (`IronBank.Limit` przy drabinie): `DebtLimitLandDays` (15) x czesc "ziemia" D stalego + 10 000 za miasto + 5 000 za zamek + `WarCreditLootDays` (30) x srednie
  jednorazowe od prawdziwego platnika z 84 dob (sprzedaz osadom, od innych bohaterow i partii, trzecia, okupy 178; bez zdarzen z niczego - statki, majatki BK), x wiarygodnosc.
  Srednia 84 dob: pierwsze 84 doby srednia zwykla, potem kroczaca (zapisana w `arm_debt168`).
- **Splata** (krok Banku, przed budzetem): odsetki (poza zajeciem); u AI z dlugiem Banku najpierw jednorazowe doby (`ClanIncomeBook.OnceToday`: lup, okupy, sakwy, trzecia,
  statki; bez zwrotu, korony, kontraktu, renty, podwojnych) - w wojnie 50%, w pokoju 100% - z kiesy glowy ponad max(5 000; 3 dni zoldu), potem dorosli czlonkowie ponad
  5 000; poza limitem rat; najpierw gasi KW. Raty z D (D budzetu wczoraj, inaczej D staly, inaczej D169): Bank najwyzej 10% D (KW w wojnie AI - bez raty z D; stary dlug
  wedlug terminu), wszystkie raty najwyzej 15% D - Bank, potem dlug zoldu, dlug wobec korony, zaliczka gry, okupy wedlug wieku (kolejka bez blokowania).
  Przed zaleglosc - pomoc rodziny T5 (`FamilyCoverAll`).
- **D2 zaleglosc:** glowa nie ma na raty -> placi, ile ma ponad 3 dni zoldu (Bank pierwszy), wiarygodnosc x0.8 (min 0.5), +2 pp, KW wstrzymany. **D3 zajecie** po 3 zaleglosciach
  z rzedu (zamiast bankructwa): codziennie dochod wsi rodu (`VillageIncomeOf`: srednia 28 dob podatku wsi BK i renty wsi z dzisiejszych wsi) + kiesa glowy ponad
  `SeizeFloorGold` (38 000) -> wierzyciele (Bank, potem roszczenia); odsetki zamrozone; budzet 166 liczy D bez dochodu wsi (poczet sam maleje - zwolnienia). Splacone ->
  koniec zajecia, KW po 182 dobach. **D4 wyprzedaz** co 7 dni tylko przy 14 dobach zerowego zajecia albo prognozie (dlug / srednia zajecia) > 728 dni: nadwyzki zbrojowni
  partii AI w miescie (`GarrisonArmory.SellSurplus`, zapas 0%) na targ (kasa miasta placi), karawany bez bohatera i warsztaty do najbogatszego notabla (placi z kiesy, ile ma;
  karawana tylko gdy notabl ma choc jej gotowke) - do kwoty dlugu, przychod do wierzycieli; lenno zostaje. Bankruci Banku ze starego zapisu -> zajecie pierwszego dnia.
- **Bez umorzenia:** rod wyeliminowany - dlug (Bank + roszczenia) na nowych panow jego wsi (wsie zapamietane codziennie), proporcjonalnie do liczby wsi, takze gracz
  (komunikat); bez wsi - "dlug bez platnika" (ksiega, zapis). Glowa zmarla, rod zyje - doba przerwy.
- **Dlug zoldu (`WageDebtToMen`):** `SoldierPay.ClanTickPrefix` przenosi caly `DebtToKingdom` rodu do ksiegi przed rozliczeniem (zaliczka gry z `CrownIncome` - roszczenie
  "zaliczka gry", reszta - "dlug wobec korony" do skarbca krolestwa rodu); gra (`AddPaymentForDebts`) nie oproznia juz kiesy glowy jednym pobraniem. W `Settle` nowy dlug
  tego rozliczenia: czesc rownowazona obcietym zoldem (brak ukryty w dlugu) -> roszczenia ludzi kazdej partii wedlug jej obciecia; reszta -> zaliczka gry; `DebtToKingdom` = 0.
  Splata: partia lorda -> `MenPurse.Add`, zaloga -> kasa jej osady; partii nie ma - inna partia lorda rodu, potem kasa najblizszego miasta (pod tarcza dworu).
- **Portfele gry bez zaliczki (`GameWalletsNoAdvance`, OBIEG-1):** prefiks/postfiks `AddExpensesForHiredMercenaries`, `AddExpensesForTributes`, `AddExpensesForCallToWarAgreements`:
  gdy gra uznala portfel w calosci, a niezaplacony udzial dopisala rodowi do dlugu - ta czesc wraca do portfela krolestwa (jutro dzielona na rody), dlug rodu maleje.
  Brak zoldu ukryty w dlugu zostaje (dlug zoldu). Zrodlo zaliczki gry zamkniete; stare zaliczki splacane z ksiegi w nicosc (OBIEG-1).
- **Dluznik (166):** dlug w Banku, dlug zoldu albo wobec korony, zaleglosc albo zajecie (sam dlug okupu - nie): dwor -50%, budowy 0. "Sprzet tylko braki" - juz jest:
  pan placi tylko braki (`AiGear.TryBuyCore`), lepsze tylko z sakiewki ludzi (`MenUpgrade.ForAi`). **B5:** prefiksy BK `BKLordPropertyBehavior.ShouldHaveCaravan` /
  `ShouldHaveWorkshop` - rod AI z budzetem kupuje karawane i warsztat tylko przy G >= 60 D i bez dlugu.
- **Kapital:** pozyczki z kapitalu (posiadacz w "Pieniadz swiata"); zysk ponad `IronBankCapital` (5 mln) -> kasa Braavos po 1/180 dziennie (pod tarcza dworu).
- **Gracz (`PlayerSameLadder`):** ta sama ksiega, limit, raty, zaleglosc, zajecie (wsie i kiesa ponad 38 000) i wyprzedaz (karawany, warsztaty - nigdy ekwipunek); pozycza
  recznie w Braavos (w wojnie - KW), menu pokazuje szczebel i zasady; komunikaty po angielsku przy zaleglosci, zajeciu, wyprzedazy, koncu zajecia, dlugu po wymarlym rodzie.
- **Dodatek - wezwanie do wojny placi korona (`CrownPaysCallToWar`, decyzja Jeffa 10.10):** prefiks `AddExpensesForCallToWarAgreements` - rody (AI i gracz) nie placa nic;
  postfiks `AllianceCampaignBehavior.StartCallToWarAgreement` - wezwany nie dostaje ceny z gory (cofniety plus portfela; portfel wzywajacego zostaje dlugiem korony); w kroku
  korony 165 po kontraktach 185, przed zwrotem: skarbiec wzywajacego placi z reszty wplywow dnia czesc dnia = cena / 42 (doby sluzby gry) do portfela wezwanego (gra rozdziela
  go na jego rody - prawdziwy platnik). Gdy wplywow dnia nie starcza na czesc dnia: `EndCallToWarAgreement` gry i `MakePeaceAction` miedzy wezwanym a wrogiem (gdy nie trzyma
  go w wojnie inne wezwanie), reszta ceny skreslona. Koniec porozumienia przez gre (termin, pokoj) - reszta skreslona. Dlug portfela wezwania sprzed paczki (bez naszego wpisu) -
  skreslony pierwszego dnia (wezwany dostal go juz od gry z niczego, rody juz go nie placa). Bez zachowania sojuszy gry - zaplata proporcjonalna, licznik "niedoplata".

**Nowe klucze (grupa "Debts and the war credit (stage 2)"):** `DebtLadderEnabled` true, `WarCredit` true, `WarCreditMaxShareD` 0.40, `WarCreditLootRepayShare` 0.5,
`WarCreditLootDays` 30, `DebtLimitLandDays` 15, `BankFreeCapitalFloor` 1 000 000, `IronBankMaxInstalmentShare` 0.10, `AllInstalmentsMaxShare` 0.15, `SeizeFloorGold` 38 000,
`SaleForecastDays` 728, `SaleAfterZeroSeizeDays` 14, `CreditAfterSeizureDays` 182, `BankProfitToBraavosDays` 180, `WageDebtToMen` true, `GameWalletsNoAdvance` true,
`PlayerSameLadder` true. Grupa "The crown's income (stage 2)": `CrownPaysCallToWar` true.

**Nowe / zmienione linie logu:**
- NOWA (kampania) `Dlugi (168): latki - portfel najemnikow (AddExpensesForHiredMercenaries), portfel trybutu (...), portfel wezwania do wojny (...), B5 karawana BK (ShouldHaveCaravan), B5 warsztat BK (ShouldHaveWorkshop); BRAK: -.`
- NOWA (kampania) `Wezwania do wojny (168): latki - rody nie placa wezwania (...), porozumienie bez ceny z gory (StartCallToWarAgreement), koniec porozumienia (EndCallToWarAgreement); BRAK: -.`
- NOWA (po "Budzet rodow (166)") `Dlugi (168): dzien N | dluznicy: kredyt a (w tym kredyt wojenny b), zaleglosc c, zajecie d, wyprzedaz e, sam dlug okupu f; nowe dzis: zaleglosci, zajecia (z bankructw starego zapisu), wyjscia z zajecia | dlug: Bank X (kredyt wojenny Y), dlug zoldu, wobec korony, okupy | KW (166, dzis): miejsce w pulapie u N rodow (X zl), wyplacone Y zl u M rodow; bez KW: na limicie, zaleglosc/zajecie, po zajeciu, D nie pokrywa kosztow stalych, Bank ponizej progu wolnego kapitalu | splaty (Bank przed budzetem): z jednorazowych X (wojna 50%, pokoj 100%), raty z D: Bank, pozostali, w zaleglosci (ponad 3 dni zoldu); odsetki narosle; rod-raty, zaleglosci dzis; suma rat ponad 15% D: 0 rodow (kontrola - 0) | zajecie: dochod wsi, kiesy ponad 38000; wyprzedaz N: zbrojownie, karawany, warsztaty | do wierzycieli: ..., zaliczka gry w nicosc (OBIEG-1), okupy, ..., bez odbiorcy 0 (prog 0) | z gry do ksiegi (od wczoraj): dlug wobec korony + zaliczka gry, dlug zoldu (partii), nowa zaliczka gry; portfele gry ponownie otwarte X zl w N | wymarli: ..., dlug bez platnika razem | Bank: kapital, prog, zysk do kasy Braavos | latki: ...`
- NOWE (zdarzenia) `Dlugi (168): <rod> - ZAJECIE dochodu (D3) po 3 zaleglosciach z rzedu; ...`, `Dlugi (168): <rod> - zajecie zakonczone, ...`, `Dlugi (168): <rod> - WYPRZEDAZ (D4): ...`,
  `Dlugi (168): rod <X> wymarl - dlug ... przechodzi na nowych panow jego wsi: ...` / `... wymarl bez wsi - ... dlug bez platnika`.
- NOWA `Wezwania do wojny (168): dzien N | porozumien w toku N (nowe dzis), reszta cen do zaplaty przez korony X | zaplacone dzis ze skarbcow wzywajacych do portfeli wezwanych Y | zerwane z braku wplywow dnia N (reszta ceny skreslona, pokoj sojusznika), niedoplata | skreslone: koniec porozumienia przez gre, dlug portfela sprzed paczki | ...` (tylko gdy cos sie dzieje) i zdarzenia `Wezwania do wojny (168): <A> wzywa <B> przeciw <C> - cena ...`, `... porozumienie z ... zerwane ...`.
- ZMIENIONA `IronBank: dzien N (drabina dlugu 168: kredyt wojenny wyplaca budzet 166 po Banku - linia 'Dlugi (168)'; splaty i spoznienia z drabiny) - nowe pozyczki 0 ...` ("bankructwa dzis" zawsze 0).
- ZMIENIONA `Budzet rodow (166): dzien N` - po "dwor ustepuje zoldowi": `| kredyt wojenny (168): miejsce w pulapie X u N rodow, wyplacone dzis Y u M rodow; dluznicy (dwor -50%, budowy 0) K, w zajeciu (D bez dochodu wsi) J (dochod wsi Z)`.
- ZMIENIONA `Dlugi (na sucho)`: szczebel z drabiny (`StageOf`: kredyt / zaleglosc / zajecie / wyprzedaz, takze rody z samymi roszczeniami); "pozyczyliby wg planu: na wojne N" = rody
  z miejscem KW dzis (limit = limit drabiny - dlug); "na okup glowy" 0. Kolumna CSV `kandydat_pozyczki` = `KW`, `szczebel` z drabiny.
- ZMIENIONA `Korona: wplywy dnia (165)`: po kontraktach `, wezwania sojusznikow do wojny (168) X (porozumien zerwanych z braku wplywow N, niedoplata Y)`; na krolestwo `wezw. X`.
- ZMIENIONA `Obieg: dzien N` - korona: `, wezwania do wojny (168) skarbce wzywajacych -> portfele wezwanych X (...)`; Bank: `; 168: kredyt wojenny kapital -> glowy X (rodow N), splata z jednorazowych, raty, zajete (dochod wsi i kiesy ponad podloge), wyprzedaz, do sakiewek ludzi (dlug zoldu), do skarbcow (stary dlug wobec korony), zaliczka gry w nicosc (OBIEG-1), okupy do porywaczy, portfele gry ponownie otwarte (bez zaliczki), zysk Banku do kasy Braavos`.

**Odstepstwa / rozstrzygniecia (z powodem):**
1. `IronBankIncomeDays` zostaje 60 (stary wzor bez drabiny), limit drabiny ma nowy klucz `DebtLimitLandDays` 15 - "wylacznik = stan sprzed paczki" (zmiana domyslnej zmienialaby
   Bank takze przy wylaczonej drabinie).
2. Wyplata KW liczona w kroku budzetu 166 (po Banku), nie w kroku Banku: KW jest czescia pulapu, a brak do zoldu naliczonego wymaga dzisiejszego pulapu bez KW. Raty, splata
   z lupow, zaleglosci i zajecie - w kroku Banku (kolejnosc 2.0b). Linia "Dlugi (168)" po budzecie.
3. Miejsce KW w pulapie u kazdego rodu spelniajacego warunek (wojna, G < R, ...), a wyplata tylko przy zoldzie ponad pulap bez KW - inaczej rod nigdy nie przekroczylby pulapu,
   wiec warunek "zold > pulap bez kredytu" nie dalby sie spelnic (gra nie werbuje ponad limit partii).
4. Splata z jednorazowych liczona raz na dobe z licznikow ksiegi (nie od kazdego wplywu) i zbierana z kiesy glowy, potem z kies doroslych czlonkow (lup trafia do wodzow partii);
   po pokoju 100% jednorazowych takze dla starego dlugu Banku (nie tylko KW).
5. Gracz: bez automatycznej splaty z jednorazowych (ksiega liczy u gracza przychod z handlu jako jednorazowy) - raty z D takze w wojnie (10% D).
6. Kolejnosc roszczen po Banku: dlug zoldu, dlug wobec korony, zaliczka gry, okupy wedlug wieku (projekt: "najpierw Bank, potem okupy w kolejnosci powstania"; dlug zoldu
   nie mial miejsca - ludzie wlasnej partii przed obcym wierzycielem).
7. Zajety "dochod wsi u zrodla" = srednia 28 dob dochodu wsi z dzisiejszych wsi (podatek wsi BK + renta wsi), zdejmowana z kiesy glowy w kroku Banku (gdzie trafia renta
   i podatek wsi); budzet liczy D bez niego, wiec go nie planuje. Bez D stalego - renta wsi tej doby.
8. D4: nadwyzki zbrojowni tylko partii AI stojacych w miescie (sprzedaz na targ wymaga targu), konie - nie (konie i rzedy prowadzi Stajnia - poza nadwyzka zbrojowni);
   karawany z bohaterem na czele i karawany gracza z towarzyszem - nie (`TransferCaravanOwnership` przenioslby towarzysza); u gracza bez zbrojowni.
9. Dlug wymarlego rodu na nowych panow wsi wedlug liczby wsi; dlug zoldu wymarlego rodu idzie do partii dziedzica (zapasowy odbiorca).
10. Zapasowy odbiorca dla okupu (178) i dlugu wobec korony: glowa rodu porywacza, potem kasa najblizszego miasta (pod tarcza dworu) - bez kroku "nowy pan wsi wierzyciela".
11. Wylaczenie drabiny w trakcie kampanii: ksiega drabiny zostaje (zapis), ale nie jest obslugiwana (bez rat, zajec i KW); Bank wraca do starych zasad. Wylaczenie
    `CrownPaysCallToWar`: porozumienia w toku wracaja do gry (wezwany dostaje niezaplacona reszte, rody wzywajacego placa jak dotad).
12. Wezwanie do wojny: "oplata dnia" = cena gry / 42 doby sluzby (gra placi cala cene z gory) - porozumienie zrywa sie, gdy wplywow dnia nie starcza na czesc dnia;
    sojusznik wychodzi z wojny przez `MakePeaceAction` (gra przy koncu porozumienia sama pokoju nie zawiera).

**Czego sie spodziewac w tescie 120 dob:**
- `IronBank: dzien`: "nowe pozyczki 0", "bankructwa dzis 0" przez caly bieg. Bank: kapital >= 1 mln w kazdej dobie.
- `Dlugi (168)`: KW "miejsce w pulapie" u ok. 40-70 rodow w wojnie (w C3c rodow w wojnie z G < R: 66-69 w dobach 94-120, ich 0.40 D razem 14-22 tys. zl/dobe), "wyplacone"
  kilka tys. zl/dobe u rodow na pulapie (w C3c tylko 4-9 takich rodow); dluznikow ok. 10-40; "suma rat ponad 15% D: 0"; nowe zajecia <= 1 w 120 dobach; "bez odbiorcy 0".
- **Wojsko lordow w wojnie: tylko ok. +1-3 tys.** wobec C3c (ok. 93 tys. -> ok. 94-96 tys.): w C3c pulap 166 byl uzyty w 42-46% i wiazal tylko u kilku rodow z G < R;
  rody z G >= R maja skrzynie wojenna, a KW ich nie dotyczy (projekt). Cel 95-115 tys. - dolna krawedz; reszta luki to nie pieniadz (diagnoza C3: smierc w bitwach, dezercja
  183 z dlugu snu T10, sklad wojen).
- `Pieniadz swiata`: bez zmiany tempa poza splata starych zaliczek gry w nicosc (nowych 0) i brakiem zlota z niczego dla wezwanych do wojny (portfel wezwanego dostaje tylko
  zaplacone przez skarbiec).
- `Korona: wplywy dnia (165)`: "wezwania sojusznikow do wojny (168)" > 0 tylko przy porozumieniach; "zerwanych z braku wplywow" - przy biednych koronach.
- Glowy < 5000: rody gentry Pentos nie placa juz udzialu w wezwaniu do wojny (spadek ok. 21 tys. z C3b znika).

**Ryzyka:** (a) `MakePeaceAction` przy zerwaniu wezwania - pokoj bez daniny miedzy wezwanym a wrogiem (Diplomacy moze reagowac na zdarzenie pokoju); (b) KW przy G tuz
pod R: wyplata podnosi G ponad R, nastepnego dnia KW znika (skrzynia wojenna), pulap spada - mozliwe kolysanie limitow partii (zwolnienia dopiero po 3 dobach ponad 1.10 x pulap);
(c) D4 przenosi karawany i warsztaty do notabli - rzadkie, do obejrzenia w autotescie (0 bledow); (d) `MercenaryWallet` ma setter internal - ponowne otwarcie portfela
najemnikow przez refleksje (portfel najemnikow zmienia tylko kontrakt gracza); (e) dlug wobec korony przeniesiony z gry: `ChangeKingdomAction` juz go nie kasuje
(bez umorzenia), a warunek gry "najemnicy odchodza od krola z dlugiem > 10 000" nie zachodzi.

---

## D - 178 okupy wedlug majatku, wielcy jency i prawo trzecich

Projekt: rozdz. "178" (krok D; 2.13, 2.14), 2.0b (kolejnosc: 1/9 przed darami, nagroda za wielkiego jenca po kontraktach, przed zwrotem), odpowiedzi Jeffa 11:05
(pulap rok D, okup krola ze skarbca). Build Release Armoury i RealisticCaptivity kod 0, `python -I tools/gen_mcm.py`, gra nie uruchamiana.

**Pliki:** nowy `Armoury/src/Ransom178.cs` (cena, przeplyw okupu, AI-AI, kurier, posrednik, okup gracza, wielcy jency, 1/9, linia "Okupy (178)", zapis `arm_rans178`);
zmienione `CrownIncome.cs` (dlug korony za okup krola - raty jak reparacje, odbiorca krolestwo albo "h:" bohater; 1/9 w "naszych" wplywach dnia), `DebtLadder.cs` (rata okupu -> 1/9),
`RansomFlows.cs` (kurier i okup gracza przez 178, harness 178), `ClanIncomeBook.cs` (trzecia lorda -> 1/9), `MenPurse.cs` (sakwa ludzi gracza -> 1/9), `MoneyLedger.Obieg.cs`,
`ArmouryBehavior.cs`, `SubModuleMain.cs`, `Settings.cs` + `McmSettings.cs`, `tools/gen_mcm.py`; RealisticCaptivity: nowy `src/ArmouryBridge.cs` (refleksja do `Armoury.Ransom178`),
`src/FairRansom.cs` (`LordPrice` - cena z Armoury dla lordow trzymanych/sprzedawanych przez gracza, bez potracen RC; `SalePostfix` - pominiety przy 178), `src/Patches.cs`
(`RansomAmountPatch` - gotowka okupu gracza z Armoury).

**Co dziala (LordRansomByIncome; czynne z drabina 168 - raty okupow w tej samej ksiedze):**
- **Cena** (`PriceOf`): glowa rodu `RansomHeadYears` (0.5) x 364 x D, kazdy inny lord i dama `RansomLordDays` (60) x D; D = D rodu do rat 168 (D budzetu, D staly, D169).
  Krol (`RansomKingFromTreasury`): 0.5 x 364 x srednia 28 dob wplywow dnia jego korony (165), placi skarbiec. Bez minimow, bez wzrostu za czas niewoli.
- **Zaplata** (`Execute`): gotowka = min(cena, `RansomCashShare` (0.5) x (kiesy rodziny - 5 000)), zbierana z kiesy glowy, potem doroslych czlonkow (kazdy do 5 000);
  reszta - dlug okupu w ksiedze 168 (wierzyciel glowa rodu porywacza; raty w limicie 15% D po Banku, kolejka wedlug wieku), pulap wszystkich dlugow okupow rodu
  `RansomDebtCapDays` (364) x D - okup ponad pulap mniejszy (porywacz dostaje mniej). Krol: gotowka z polowy nadwyzki skarbca ponad `CrownReserveGold`, reszta - dlug korony
  (`CrownIncome.AddCrownDebt`, raty najwyzej 50% wplywow dnia placacego, jak reparacje). Jeniec wolny po gotowce (`EndCaptivityAction.ApplyByRansom`).
- **AI-AI:** prefiks `RansomOfferCampaignBehavior.ConsiderRansomPrisoner` (gra wola go raz na dobe dla jenca z rodu z co najmniej 2 lordami) - stale 10% dziennie,
  wlasny przeplyw zamiast barteru gry (rodzina jenca -> glowa rodu porywacza); `ExecuteAiBarter` dla okupow juz nie biegnie.
- **Kurier** (jeniec u gracza albo gracz placi za czlonka rodu): oferta bez sprawdzania kiesy placacego (ta sama szansa 20% / 12% po odmowie); wartosc w dialogu
  = cena 178 (postfiks `SetPrisonerFreeBarterable.GetUnitValueForFaction` - cena / 1.1, gra mnozy x1.1); przyjecie (`RansomFlows.AcceptPrefix` -> `CourierAccept`): ten sam
  przeplyw, bez dosypki z niczego; gracz placacy - czesc gotowka, reszta jego dlug (przycisk czynny takze przy kiesie < ceny - prefiks `IsAffirmativeOptionEnabled`).
- **Posrednik i ekran druzyny** (gracz sprzedaje lorda): RC `LordPrice` bierze cene z Armoury - przy sprzedazy tylko gotowke (tyle gra wyplaca graczowi z niczego);
  postfiks `SellPrisonersAction.ApplyInternal`: rodzina jenca oddaje te gotowke (w nicosc - rownowazy zloto gry, netto 0), reszta ceny - dlug rodu wobec gracza.
  Lord przeniesiony do lochu osady (osada w wojnie z jencem - nie uwolniony) - jak w grze, licznik.
- **Okup gracza:** RC `RansomAmountPatch` - kwota w menu niewoli = gotowka (polowa kies rodu gracza ponad 5 000, z ceny 0.5 roku D gracza); `RansomFlows.MenuRansomPrefix` (2.14)
  placi ja porywaczowi i `PlayerRansomPaid` zapisuje reszte jako dlug gracza wobec odbiorcy (ksiega 168). Przy graczu poza drabina (`PlayerSameLadder` wylaczone) - RC jak dotad.
- **Wielcy jency** (`CrownGreatCaptives`): krol albo nastepca tronu (najwyzej punktowany w `Clan.GetHeirApparents` rodu krola) pojmany przez rod krolestwa - przy pojmaniu
  (zdarzenie `HeroPrisonerTaken`) przechodzi na korone zdobywcy (wpis do zapisu); caly okup (gotowka i raty) do skarbca zdobywcy; nagroda `CrownGreatCaptiveReward` (1/10)
  okupu dla zdobywcy w kroku korony (po kontraktach, wezwaniach 168, przed zwrotem): z wplywow dnia, potem zapasu ponad rezerwe; reszta czeka (pierwsze wplaty okupu).
  Zdobywca bez krolestwa - okup dla niego. Gracz - nagroda i komunikat; okup jenca u gracza idzie do skarbca jego krola.
- **Prawo trzecich** (`CrownThirds`, 1/9 = `CrownThirdsShare` 0.111): korona krolestwa w wojnie (bez najemnikow; gracz - wasal, nie krol) - podstawa doby: okupy otrzymane
  (gotowka, raty; bez wielkich jencow), 1/3 trzeciej lorda AI (`NoteInflow` KThird: nadwyzki ludzi, sakwy rozbitych), sprzedaz lupu lorda AI przez gre (pre/postfiks
  `PartiesSellLootCampaignBehavior.OnSettlementEntered` - przyrost kiesy wodza), od gracza 1/9 sakw jego ludzi (z sakiewki ludzi) i 1/9 wartosci lupu z ekranu po bitwie
  (`PlayerEncounter.DoLootInventory`, cena najblizszego miasta przy sprzedazy; `CrownThirdsPlayerLoot`). Rozliczenie raz na dobe (`ThirdsSettle`, krok korony po daninie
  i cle, przed `CrownIncome.Begin`) z kiesy odbiorcy do skarbca jego krolestwa; czego brak - zaleglosc na jutro (zapis). Wplacone 1/9 sa "naszymi" wplywami dnia 165.

**Nowe klucze (grupa "Ransoms by wealth (stage 2)"):** `LordRansomByIncome` true, `RansomHeadYears` 0.5, `RansomLordDays` 60, `RansomCashShare` 0.5, `RansomQueueNoBlock` true,
`RansomDebtCapDays` 364, `RansomKingFromTreasury` true, `CrownGreatCaptives` true, `CrownGreatCaptiveReward` 0.10, `CrownThirds` true, `CrownThirdsShare` 0.111,
`CrownThirdsPlayerLoot` true.

**Nowe / zmienione linie logu:**
- NOWA (kampania) `Okupy (178): latki - okup AI-AI i oferty kuriera (ConsiderRansomPrisoner), kurier: gracz placi czesc gotowka (IsAffirmativeOptionEnabled), cena w barterze i kurierze (GetUnitValueForFaction), posrednik i ekran druzyny (SellPrisonersAction), 1/9 sprzedazy lupu lorda AI (PartiesSellLoot), 1/9 lupu gracza z ekranu po bitwie (DoLootInventory); BRAK: -.`
- NOWE (zdarzenia) `Okupy (178): AI-AI - <jeniec> (glowa rodu|lord, <rod>) wolny za P zl (D d[, placi skarbiec <krolestwo>]): gotowka C, dlug X -> <odbiorca> (<rod>)|skarbiec <krolestwo> (wielki jeniec).` (tez `kurier - `), `Okupy (178): posrednik - gracz sprzedal ...`, `Okupy (178): okup gracza - gotowka ...`, `Okupy (178): wielki jeniec - <jeniec> (krol|nastepca tronu, <krolestwo>) pojmany przez <zdobywca> - przechodzi na korone ...; okup ok. P zl ..., nagroda dla zdobywcy R zl ze skarbca.`
- NOWA (po "Okupy (2.14)") `Okupy (178): dzien N | okupy dzis: AI-AI a, kurier b, posrednik i ekran druzyny c, okup gracza d - cena razem P = gotowka C + dlug okupu X (najwiekszy: ...) | pulap dlugu okupow (364 D): przycieto n okupow o Y; okup 0 (...) z, bez umowy - kolejka zablokowana q | krol ze skarbca: k (gotowka, nowy dlug korony; raty dlugu korony dzis, do splaty razem) | wielcy jency: pojmani dzis, u koron zdobywcow, okup do skarbcow zdobywcow; nagrody zdobywcow: nowe, wyplacone, czeka | prawo trzecich (1/9): podstawa - trzecie lordow (korona 1/3), sprzedaz lupu lordow AI, okupy otrzymane, sakwy ludzi gracza, lup gracza z ekranu; nalezne z dzisiejszej podstawy F | sciagniete do skarbcow (rozliczenie dzis rano, podstawa wczoraj) S (z sakiewki ludzi gracza), zaleglosc odbiorcow | lochy: ..., posrednik - gotowki nie zebrano od rodziny | latki: ...`
- NOWE (harness autotestu, doba sesji 8, zamiast krokow 2b/2c przy 178) `Harness niewoli (178): krok 2 - kurier prawdziwy, okup wedlug majatku: ... -> rodzina zaplacila gotowka X, gracz dostal Y, z niczego 0, dlug okupu wobec gracza +D, jeniec wolny, oferta zamknieta - OK|BLAD.`
- ZMIENIONA `Korona: wplywy dnia (165)`: "nasze: powinnosci, danina wojenna, clo, 1/3 zaworu zamkow, 1/9 prawa trzecich (178) X"; "raty reparacji" obejmuja raty okupu krola (skarbiec -> skarbiec albo porywacz).
- ZMIENIONA `Obieg: dzien N` (korona): `, 1/9 (178) z kies odbiorcow okupow i lupu do skarbcow X, okupy wielkich jencow do skarbcow zdobywcow Y, raty okupu krola skarbiec -> porywacz Z, nagrody za wielkich jencow skarbiec -> zdobywca W`.
- ZMIENIONA w tresci: `Niewola lordow i okupy (169c)` "okupy AI-AI (barter gry)" = 0 (barter gry dla okupow juz nie biegnie - liczba w "Okupy (178)"); RC `Wykup: vanilla N -> M (178 Armoury: gotowka okupu wedlug majatku, reszta na raty)`.

**Odstepstwa / rozstrzygniecia (z powodem):**
1. Klucze 178 w Armoury (projekt: `LordRansomByIncome` w RC) - cena potrzebuje D i ksiegi 168 (Armoury); RC pyta Armoury przez refleksje (`ArmouryBridge`), bez Armoury - RC jak dotad.
2. `PrisonerRansomValue` gry nie jest zmieniany dla wszystkich lordow (lapowki w lochach, dyplomacja, darowizny liczylyby sie od 0.5 roku D) - cena 178 dziala w przeplywach
   okupu (AI-AI, kurier, posrednik, okup gracza) i w wycenie lordow trzymanych/sprzedawanych przez gracza (jak RC dotad).
3. Posrednik: gra dalej placi graczowi z niczego - ale tylko gotowke, ktora rodzina jenca w tej samej chwili oddaje w nicosc (netto 0); lord sprzedany do lochu osady
   bedacej w wojnie z jencem nie jest uwalniany - zostaje zloto gry (licznik "lochy").
4. Nagroda za wielkiego jenca przy pojmaniu ("od razu"), nie przy okupie; jeniec, ktory ucieknie, kosztuje korone nagrode (ryzyko wojny). Nastepca tronu = najwyzej
   punktowany kandydat `GetHeirApparents` rodu krola (gra wybiera nastepce z remisow losowo - tu wiek).
5. Okup krola: gotowka z polowy nadwyzki skarbca ponad rezerwe 165 (projekt: "gdy skarbca nie starcza - raty"); bez pulapu 364 D (to pulap rodow). Odbiorca bez krolestwa -
   dlug korony wobec bohatera ("h:<id>"). Okup krola dla porywacza bez krolestwa nie ma 1/9.
6. 1/9 z okupow i sprzedazy lupu liczone wedlug krolestwa odbiorcy w chwili wplywu (w wojnie, nie najemnik), rozliczane nastepnego ranka; gracz-krol nie placi 1/9 (sam jest korona).
   "Rozliczenie z licznikow" - podstawa dzienna na bohatera, bez zaokraglen na sztuce.
7. Okup gracza: cena 0.5 roku D rodu gracza (gracz jest glowa rodu) - ten sam wzor; dlug honorowy RC (`OfferDebtDeal`) zostaje jako osobna droga RC (kwota od gotowki 178).
8. Harness autotestu: krok 2 (kurier prawdziwy) sprawdza przeplyw 178; scenariusz "krol AI jako jeniec lorda AI i gracza" - nie dodany (pojmanie krola w autotescie zmienia
   wojne swiata); sprawdzenie w logu: linie "wielki jeniec" i "nagrody zdobywcow" co do 1 zl z "Obieg".
9. AI-AI dalej tylko dla jencow z rodu z co najmniej 2 lordami (warunek gry przed `ConsiderRansomPrisoner`) - zmiana warunku poza prefiksem zmienialaby takze kuriera.

**Czego sie spodziewac w tescie 120 dob:**
- `Okupy (178)`: AI-AI kilka dziennie (10% dziennie od kazdego jenca z rodu z 2+ lordami; w C3c w niewoli 20-45 lordow) - wobec 57 barterow gry w 91 dobach C3c duzo wiecej;
  `Niewola lordow i okupy (169c)`: "uwolnieni dzis: okup" rosnie, "ponad 60 dni" ok. 0, mediana dni w niewoli spada.
- Kwoty: lord (nie glowa) ok. 60 D (pan zamku ok. 60-80 tys.), glowa ok. 182 D; gotowka zwykle polowa kies ponad 5 000, reszta dlug okupu -> `Dlugi (168)` "sam dlug okupu" rosnie,
  raty okupow w "raty z D: pozostali"; "suma rat ponad 15% D: 0"; "pulap dlugu okupow: przycieto" > 0 u rodow pojmanych wielokrotnie.
- Prawo trzecich: "sciagniete do skarbcow" / "nalezne" bliskie 100% (zaleglosc odbiorcow mala); `Korona: wplywy dnia (165)` "nasze" rosnie o 1/9; `Pieniadz swiata` bez zmiany tempa
  (przelewy rod -> rod, rod -> skarbiec; posrednik netto 0).
- Kula sniezna (prog rozdz. 1): stale przeplywy od przegranych do zwyciezcow - obserwowac wojsko na krolestwo i udzial wygranych bitew wobec biegu bez 178.
- Wielcy jency rzadko (krol i nastepca w polu rzadko wpadaja); gdy wpadna - "nagrody zdobywcow: nowe" = 0.1 x okup, "wyplacone" w kolejnych dobach.

**Ryzyka:** (a) liczba okupow AI-AI rosnie kilkukrotnie - wieksze przeplywy miedzy rodami i wiecej dlugow okupow (raty 15% D tna budzet przegranych); jesli kula sniezna
przekroczy prog - "gotowka 60% zamiast 50%" (jedna liczba, `RansomCashShare`) albo mniejsza szansa; (b) kurier przy 178 nie sprawdza kiesy placacego - oferty czestsze;
(c) RC i Armoury musza byc wgrane razem (bez Armoury RC liczy po staremu - spojnie, bez dubla); (d) `ExplainedNumber`/sygnatury latek sprawdzone w dekompilacji 1.4.8,
ale `IsAffirmativeOptionEnabled` zwraca krotke `(bool, string)` - pierwszy realny test w autotescie (linia latek: BRAK = nic sie nie zmienia w dialogu).

---

## D - recenzja wlasna (diff 0175e8d..8d620e3; poprawki w osobnym commicie)

Sprawdzone jak sceptyczny recenzent: zloto (kredyt wojenny z kapitalu Banku; kazda rata, zajecie i wyprzedaz zdjete z dluznika i oddane wierzycielowi w tej samej
chwili - nadwyzka wraca do dluznika; jedyne "w nicosc" - splata zaliczki gry OBIEG-1, licznik), okupy (cena = gotowka + dlug co do 1 zl w kazdej linii zdarzenia i w linii
dnia; posrednik netto 0 - gra wyplaca graczowi z niczego dokladnie gotowke, ktora rodzina jenca oddaje), 1/9 (podstawa doby na bohatera, rozliczenie z kiesy odbiorcy,
zaleglosc w zapisie), gracz (te same wzory; poza drabina - RC jak dotad), wyjatki (kazdy rod, roszczenie, porozumienie, okup i latka we wlasnym try), zapis (trzy nowe
klucze przez SaveText, stare zapisy bez kluczy - puste), wylaczniki (DebtLadderEnabled, CrownPaysCallToWar, LordRansomByIncome - stan sprzed paczek; CrownThirds osobno),
wydajnosc (petle raz na dobe; mapa partii raz na dobe tylko przy dlugu zoldu; Hero.Find/Kingdom.All tylko przy wplatach).

**Poprawione:**
1. **Wezwanie do wojny - dlug portfela sprzed paczki nie jest juz skreslany:** wezwany dostal go od gry z niczego, a rody splacalyby go w nicosc (to go rownowazylo);
   skreslenie zostawialo zloto z niczego. Teraz wpis "sprzed paczki" placi skarbiec wzywajacego czesciami dnia (cena / 42) z reszty wplywow dnia w nicosc; bez wplywow -
   czeka (nie ma porozumienia do zerwania). Linia "Wezwania do wojny (168)": `dlug portfela sprzed paczki (...): nowy X, splacone dzis ze skarbcow w nicosc (rownowazy) Y`;
   "Obieg" (korona): `; dlug portfela wezwania sprzed paczki ze skarbcow w nicosc - rownowazy zloto gry Y`.
2. **Wezwanie do wojny - bez przycinania portfela do 0** przy koncu porozumienia (krolestwo moze byc naraz wzywajacym i wezwanym - plus wezwanego by przepadl).
3. **Dlug wymarlego rodu:** udzial dziedzica w roszczeniach liczony od stanu sprzed podzialu (dotad od reszty - przy 3+ dziedzicach ostatni dostawal za duzo).
4. **Splata kredytu z lupow:** dorosly czlonek prowadzacy partie oddaje tylko zloto ponad max(5 000; 3 dni zoldu jego partii) - z jego kiesy gra placi zold tej partii.
5. **B5:** "bez dlugu" obejmuje takze dlug okupu (karawana i warsztat BK tylko przy G >= 60 D i bez zadnego dlugu).
6. **Harness 178:** kandydat na jenca harnessu nie moze byc wielkim jencem (nastepca tronu przeszedlby na korone gracza z nagroda).
7. **Dlug zoldu zalogi miasta:** splata do kasy miasta pod tarcza zoldu (`SoldierPay.Hold`, jak zold zalogi) - regulator gry jej nie skasuje.

**Uwagi bez zmian w kodzie:** (a) zysk Banku i zapasowy odbiorca w kasie miasta stoja pod tarcza dworu (`HoldCourt`) - przy wylaczonym `HouseholdShield` regulator moze
skasowac nadwyzke (ryzyko "w nicosc", licznik w "Kasy miast"); (b) 1/9 od trzeciej lorda liczy sie tylko przy wlaczonej ksiedze rodow (`NoteInflow`); (c) liczba okupow
AI-AI wzrosnie kilkukrotnie (10% dziennie od kazdego jenca zamiast barteru gry z warunkiem wartosci) - do obejrzenia kula sniezna (rozdz. 1).

---

## T10-R - AI musi odpoczywac (decyzja Jeffa 10.10) - ZROBIONE (commit a94b135)

Jeff 10.10: "musza odpoczywac; takie marsze i najwyzej 2 dni forsownego marszu, tylko w sytuacjach wyjatkowych" (do decyzji 09.10: noca tylko gdy trzeba,
kary jak gracz). Kod: `Armoury/src/NightMarch.cs` (ksiega snu AI, powody nocnego marszu), linia ustawien w `NightRest.cs` (LogCampConfig).

**Diagnoza z testu 120 dob (kopia-c3-120):** 11 partii na dlugu 3 od doby 68 do 82, potem 15 (probka kary: Oberyn Martell, Obara Sand, Nymeria Sand - morale -95%).
Dorne walczylo armia Dorana Martella ("Doran Martell (Dorne, partii 3)"): (1) czlonek armii nie mogl wejsc w sen dlugu (doczepiony - `can` w AiDebtCamp = false),
a wodz decydowal wedlug WLASNEGO dlugu: po splacie dlugu 1 (oboz od 20:00) wodz z dlugiem 0 znow gonil noca, czlonkowie szli 1 -> 2 -> 3 i na 3 utkneli
(splata dlugu 3 wymaga 21 h snu ciaglego, a w armii doba daje najwyzej ok. 18 h); (2) to samo w obozie oblezenia, w oblezonej osadzie i na morzu; (3) ksiega AI
gubila 1-5 tikow na dobe ("stoper ksiegi ... (19-23 tikow)") - numer godziny z `Math.Round(czas)`, a tick godzinowy gry ma dowolna faze; zgubiona godzina obozu 0-6
dawala 5 h zamiast bazy 6 i dlug 1 dla 237-375 partii naraz (doby 24, 27, 68, 104, 112, 114, 117, 119), a pomiar ruchu sklejal dwie godziny (420+ "obudzonych
cudza reka" w jednej godzinie 0-1 albo 5-6).

**Co zrobione:**
- **R4 obowiazkowy odpoczynek:** seria nocy bez snu (pelne doby bez bazy z rzedu) >= `MaxForcedNights` albo dlug >= `MaxForcedNights` -> partia nie idzie noca ani
  w poscigu, ani na odsiecz, ani w ucieczce; spi snem dlugu (dlug 2-3 sen ciagly tam, gdzie stoi; dlug 1 oboz od 20:00) az dlug zejdzie do 0. Jedyny wyjatek:
  ucieczka przed wrogiem co najmniej 2 razy silniejszym (sila armii albo partii, jak w grze przy ucieczce) - partia ucieka dalej, licznik "wyjatki" w linii switu.
  Alarm (wrog idzie na spiacych) przy obowiazkowym odpoczynku budzi tylko taki wrog. Zwykla podroz, patrol, zakupy - noca nie (bez zmian: tylko ucieczka / poscig /
  odsiecz z istniejacych powodow; poscig i odsiecz jak dotad tylko przy dlugu < `AiNightsAwakeInChase`).
- **R5 armia odpoczywa razem:** wodz decyduje wedlug najgorszego dlugu i odpoczynku obowiazkowego z siebie i doczepionych - oboz splaty od 20:00 albo sen ciagly
  calej armii; doczepieni spia z wodzem (licznik snu ciaglego, jak dotad `SleepsWithLeader`). Gra nie zmusza wodza do ruchu - cudze rozkazy (np. zbiorka armii)
  wraca na Hold straznik snu dluznikow (co 0.1 h gry), jak dotad.
- **R6 sen ciagly na miejscu:** partia z dlugiem >= 2, ktora w tej godzinie odpoczywa, a snem dlugu polozyc jej nie wolno (oboz oblezenia, oblezona osada, morze,
  czlonek armii, AI trzyma inny mod - np. uczta BK), dostaje licznik snu ciaglego jak w snie dlugu (sen na zmiany); ruch go konczy. Doczepieni do gracza - jak dotad.
- **R7 kazdy tick godzinowy to jedna godzina** (jak ksiega gracza): numer godziny ksiegi z licznika wywolan; tick nadrabiany w tej samej klatce (dwie godziny naraz)
  powtarza stan odpoczynku poprzedniej godziny, swit rozlicza sie raz.
- Zapis: seria nocy bez snu i doby na dlugu 3 jako 7. i 8. pole wpisu ksiegi `arm_nightrest_ai` (SaveText.Sync jak dotad; stary DLL je pomija, stary zapis = 0).

**Nowy klucz:** `MaxForcedNights` = 2 (MCM "A night's rest", 0-5; 0 = bez limitu, stare reguly). Opis `AiNightsAwakeInChase` poprawiony ("fleeing stays allowed
until Max Forced Nights orders a rest"). Stala w kodzie: `CrushRatio` = 2 (wrog "by ja zniszczyl").

**Linie logu (Armoury-*.log):**
- `NocnyMarsz: swit dnia N - ...` - nowy segment przed "| ruch w oknie obozu":
  `| ODPOCZYNEK T10-R (najwyzej 2 noce marszu z rzedu, potem oboz do dlugu 0): na dlugu 1/2/3: a/b/c; obowiazkowy odpoczynek teraz N (nowe dzis M);
  najdluzsza seria nocy bez snu X (nazwa), serii dluzszych niz 2: Y; dlug 3 dluzej niz 2 doby z rzedu Z (w tym z ucieczka-wyjatkiem W) [do 3 nazw: "imie D dob,
  gdzie"]; wymuszone odpoczynki dzis: partii P (zablokowany marsz, partio-godziny: ucieczka f, poscig c, odsiecz r, alarm a); wyjatki dzis - ucieczka przed wrogiem
  >= 2.0 x silniejszym: partii Q (partio-godzin H); wodzowie armii spia za zmeczonych czlonkow (partio-godziny) L; sen ciagly na miejscu bez snu dlugu (...) S,
  splacone tak T; ticki nadrabiane w jednej klatce U`.
  "gdzie" = `w armii <wodz>` / `wodz armii, sen dlugu` / `oboz oblezenia` / `oblezona osada X` / `na morzu` / `w osadzie X (AI trzyma inny mod)` / `w polu (ucieka)`.
- `NocnyMarsz: odpoczynek - <partia>: marsz zablokowany - <powod> - odpoczywa (dlug D, seria nocy bez snu S) [przyklad N w sesji]`,
  `... : WYJATEK - ucieczka przed X (...) - obowiazkowy odpoczynek, ale X (sila A >= 2.0 x B) zniszczylby ja: ucieka dalej ...`,
  `... : alarm zablokowany (wrog silniejszy, ale nie zniszczylby jej): ... - spi dalej ...` - przyklady: 5 pierwszych, potem co 25.
- `NightRest: oboz swiata ...` (linia ustawien) - dopisek `; T10-R: MaxForcedNights=2 (potem obowiazkowy odpoczynek do dlugu 0, ucieczka tylko przed wrogiem >= 2 x
  silniejszym; armia wedlug najbardziej zmeczonej partii)`.
- Zmiana pomiaru: `stoper ksiegi: ... (N tikow)` w linii switu - teraz 24 (bylo 17-23); linie `AiNightCamp: ruch H:00-H+1:00` - 6 na noc (bylo 5-6), bez
  skokow 400+ "obudzonych cudza reka" w godzinie 0-1 / 5-6 (byly sklejone godziny).

**Co czytac w tescie 120 dob:**
- Cel: `dlug 3 dluzej niz 2 doby z rzedu 0` w kazdej dobie (dopuszczalne tylko z "ucieczka-wyjatkiem"); jesli > 0 - nawias z nazwami mowi gdzie (armia / oblezenie /
  inny mod) - to wskazuje, ktora sciezka nie odpoczywa.
- `nowy dlug 1/2/3` bez dob masowych (200+ naraz) - zostaje kilka-kilkanascie dziennie; `z dlugiem teraz 1/2/3` - trzecia liczba zwykle 0-2, krotko.
- `najdluzsza seria nocy bez snu` zwykle <= 2; `serii dluzszych niz 2` - tylko partie z wyjatkiem (ucieczka) albo pod blokada innego moda.
- `wymuszone odpoczynki dzis` > 0 (regula dziala), `wyjatki dzis` male; `wodzowie armii spia za zmeczonych czlonkow` > 0 w dobach wojny.
- `NocnyMarsz: kara - probka` - dlug 3 rzadko i u roznych partii (nie te same przez tydzien).
- Dezercja (183) w Dorne / armiach - mniej odejsc z powodu morale -95%.

**Ryzyka:** (a) armia z jednym wyczerpanym czlonkiem stoi do ok. doby (sen ciagly 15-21 h) - wolniejsze zbiorki i marsze armii w wojnie; (b) partia w obowiazkowym
odpoczynku nie ucieka przed wrogiem 1-2 razy silniejszym - wiecej bitew przegranych przez wyczerpanych (zamierzone: wyczerpana kolumna nie ucieknie); (c) R6 zmienia
splate w oblezeniu/armii - dlug 3 schodzi tam w ok. 1-1.5 doby zamiast nigdy; (d) R7: po poprawce baza 6 h przy obozie 0-6 to rowno 6 tikow - kazdy tick sie liczy;
gdyby gra kiedys nie wolala ticku (pauza, wczytanie), pierwsza godzina po wczytaniu liczy sie jak dotad jako postoj; (e) partie w armii gracza - bez zmian (decyduje gracz).

---

## Rasy - olbrzymy tylko z olbrzymami (decyzja Jeffa 10.10) - ZROBIONE (commit ea606c7)

Jeff 10.10: "olbrzymy moga tylko z olbrzymami, ludzie z ludzmi; nie ma zadnej ciazy ani malzenstwa olbrzyma z czlowiekiem". Kod: `Armoury/src/RaceLaw.cs`
(wpiecie w `SubModuleMain`, start sesji i doba w `ArmouryBehavior`).

**Dowod:** test C3c (CrashScribe `session-2026-10-10_04-30-07.log`) - w dobie 43 SilentAssert w `HeroCreator.DeliverOffSpring`
("mother.CharacterObject.Race == father.CharacterObject.Race"), stos: `PregnancyCampaignBehavior.CheckOffspringToDeliver` <- `DailyTickHero_Patch1`, potem
"GAME HANG ... main thread silent for 61 s" w kodzie silnika.

**Sprawdzone w dekompilacji (1.4.8, BK, ROT):** model slubu, ktorego gra uzywa, to `ROTMarriageModel` (dekorator: wlasne warunki ROT, potem `_previousModel`);
pod nim `BKMarriageModel`, ktory `IsCoupleSuitableForMarriage` dziedziczy z `DefaultMarriageModel`. Z modelu korzystaja: sluby NPC gry (`RomanceCampaignBehavior` przez
`NpcCoupleMarriageChance`), ROT (`ROTRelationshipsBehavior`), oferty slubu gry, dialogi i kontrakty BK (`BKMarriageBehavior`); sama akcja `MarriageAction.ApplyInternal`
pyta model i przy false nic nie robi. Ciaza: `MakePregnantAction.Apply(matka)` (gra i BK) -> `ChildConceived` dopisuje (matka, matka.Spouse) do `_heroPregnancies`;
porod: `CheckOffspringToDeliver` -> `DeliverOffSpring`. Rasy liczone numerem `CharacterObject.Race` (human, giant, wight, whitewalker - nazwy z `FaceGen.GetRaceNames`).

**Co zrobione:**
- (a) postfiks `DefaultMarriageModel.IsCoupleSuitableForMarriage` -> false dla roznych ras; przy starcie sesji postfiks takze na kazdym wlasnym nadpisaniu w lancuchu
  typu modelu, ktorego gra naprawde uzywa (ROT) - para liczona raz. Bez osobnej latki na `MarriageAction` (akcja pyta model).
- (b) prefiks `MakePregnantAction.Apply` i `ApplyInternal`: matka i jej malzonek roznych ras - ciaza sie nie zaczyna (nic sie nie dzieje, bez komunikatu).
- (c) prefiks `PregnancyCampaignBehavior.CheckOffspringToDeliver`: ciaza roznych ras (stary zapis) konczy sie bez porodu - wpis zdjety z `_heroPregnancies`,
  `IsPregnant = false` (tak jak gra przy smierci matki i ROT przy przemianie w Innego), `DeliverOffSpring` nie jest wolany. Gra sprawdza ciezarne co dobe -
  koniec przy pierwszym ticku dnia po wczytaniu.
- Malzenstwa roznych ras ze startu ROT zostaja - tylko bez ciaz.

**Nowy klucz:** `SameRaceOnly` = true (MCM "Blood and race"; wylaczony = gra jak dotad).

**Linie logu (Armoury-*.log):**
- `Rasy: latki wpiete - model slubu gry, MakePregnantAction, porod (CheckOffspringToDeliver).` - przy ladowaniu moda (BRAK ... = cel nie znaleziony).
- `Rasy: start sesji - SameRaceOnly=True; model malzenstwa ROT.Models.ROTMarriageModel (postfiks takze na: ROT.Models.ROTMarriageModel); zywi bohaterowie wedlug rasy:
  human N, giant M, ...; malzenstwa roznych ras (zostaja, bez ciaz) K (w tym w ciazy L); ciaze roznych ras na liscie gry P[ - skoncza sie bez porodu ...].`
- `Rasy: dzien N | slubow roznych ras zablokowano X, ciaz Y, porodow roznych ras przerwano Z` - raz na dobe, tylko gdy cos zablokowano (X = rozne pary,
  ktore model uznalby za dobre, a odrzucila je rasa; model pytany wiele razy dziennie o te same pary, wiec X to kandydaci, nie odbyte sluby).
- `Rasy: ciaza <matka> (rasa) z <ojciec> (rasa) zakonczona bez porodu (rozne rasy) [n w sesji].` - 5 pierwszych w sesji.

**Co czytac w tescie 120 dob:**
- `Rasy: latki wpiete` bez "BRAK"; `Rasy: start sesji` - ile olbrzymow i ile malzenstw roznych ras, czy model to ROT z postfiksem.
- `Rasy: dzien N` - X pojawia sie w dobach, gdy ROT/gra szuka par (kilka-kilkadziesiat), Y > 0 tylko przy malzenstwach roznych ras ze startu, Z > 0 tylko przy
  starym zapisie z taka ciaza (po pierwszej dobie 0).
- Brak SilentAssert `DeliverOffSpring` w CrashScribe i brak zawieszenia w dobie ok. 43 (C3c).
- Kronika urodzin (jesli jest w logach) - dzieci olbrzymow tylko od dwojga olbrzymow.

**Ryzyka:** (a) model slubu zwraca false dla par roznych ras takze tam, gdzie gra tylko sprawdza (np. oferta slubu gracza z olbrzymka - nie pojawi sie; wojna
anuluje oferte jak przy kazdej innej nieodpowiedniej parze); (b) gdyby inny mod wolal `HeroCreator.DeliverOffSpring` z wlasnej listy ciaz (nie z gry) - straznik (c)
go nie obejmie (w dekompilacji BK/ROT takiego miejsca nie ma; BK gentry tworzy dzieci z szablonu malzonka tej samej kultury); (c) mieszane malzenstwa ze startu nie
beda mialy dzieci - rody z takim malzenstwem moga wymrzec szybciej (zamierzone).

---

## 186 - korona pozycza w Zelaznym Banku (decyzja Jeffa 10.10) - ZROBIONE (commit 57fbf30, recenzja wlasna w nastepnym)

Jeff 10.10: "oczywiscie, ze korona powinna pozyczac" (projekt etapu 2, rozdz. 3: Q4b "korona pozycza w Banku" - dotad pomysl na slowo Jeffa; dlug korony wobec
rodow z 165 pkt 3 - dalej nie). Lore (uzasadnienie): Zelazny Tron byl winien Bankowi i Lannisterom ok. 6 mln za Roberta; gdy Cersei wstrzymala splaty, Bank zaczal
pozyczac Stannisowi - Bank zawsze dostaje swoje. Zamknieta ekonomia: Bank pozycza tylko ze swojego kapitalu, kazda moneta z licznikiem.

**Regula.**
1. **Kiedy:** krolestwo w wojnie, a reszta wplywow dnia (165) po wczesniejszych wydatkach (rata, dary, raty reparacji, kontrakty, wezwania, nagroda za wielkiego jenca)
   nie starcza na nalezny zwrot zoldu -> w kroku zwrotu skarbiec pozycza w Banku brakujaca czesc zwrotu i tego samego dnia oddaje ja rodom jako zwrot. Tylko zwrot:
   renty 180 biora wylacznie reszte wplywow dnia, ktora przy pozyczce jest 0; dary i inne wydatki sa przed zwrotem i z kredytu nie rosna. W pokoju - zadnej nowej pozyczki.
2. **Ile:** dlug korony (z odsetkami i oplatami) najwyzej `CrownLoanLimitDays` (180) x sredni podatek krolestwa. Sredni podatek = srednia 84 dob "naszych" wplywow dnia
   165 (powinnosci, danina wojenna, clo, 1/3 zaworu zamkow, 1/9) - bez 1/360 zapasu i bez jednorazowych przelewow do skarbca (Diplomacy, gra), ktore nie sa podatkiem
   (w t10r jeden dzien buntownikow Pentos przyniosl 583 tys. z niczego - taki dzien podnioslby limit o ok. 1.25 mln). Kredyt dopiero od 7. doby pomiaru (stary zapis, nowe krolestwo).
   Bank daje koronom tylko z kapitalu ponad `CrownLoanBankFloor` (2 mln) i dziennie najwyzej 1/`CrownLoanPoolDays` (60) tej nadwyzki; gdy chetnych jest
   wiecej - dzieli proporcjonalnie do potrzeby.
3. **Cena:** jak kazda pozyczka Banku (`IronBank.RateFor`/`LendKw`): krol 20% rocznie (rok 364 dni), +10 pp, gdy dlug juz biegnie (srednia wazona - przy kredycie
   dobieranym codziennie ok. 30%), +15 pp dla korony po zaleglosci; oplata 2% od kazdej wyplaty (dopisana do dlugu). Odsetki narastaja codziennie, takze w pokoju.
4. **Rata:** pierwszy wydatek z wplywow dnia (krok zaraz po `CrownIncome.Begin`, przed darami 182): stala kwota = 1/`CrownLoanRepayDays` (182) najwiekszego dlugu tego
   kredytu (od ostatniej pelnej splaty), najwyzej `CrownLoanMaxIncomeShare` (0.30) x sredni podatek i nie wiecej niz dlug. Placona z wplywow dnia i 1/360 zapasu
   ("do wydania"), nigdy z rezerwy; czego nie ma - korona nie placi (okno 28 dob). W wojnie rata biegnie dalej (zmniejsza reszte na zwrot, wiec korona dobiera
   ja kredytem - dlug netto rosnie o brak zwrotu i odsetki); prawdziwa splata - w pokoju i gdy Bank jest na progu.
5. **Zaleglosc:** gdy w ostatnich 28 dobach z dlugiem korona zaplacila < 50% naleznych rat -> zaleglosc: Bank nie pozycza koronie ani jej rodom (KW 168 i pozyczka
   gracza w Braavos - limit 0) i daje pierwszenstwo jej wrogom: rody krolestw w wojnie z dluznikiem maja limit Banku x `CrownArrearsEnemyCredit` (1.5) - wiecej
   kredytu wojennego ("Bank pozycza Stannisowi"). Koniec zaleglosci: 28 dob pelnych rat z rzedu albo splata calego dlugu; nowe pozyczki odtad +15 pp.
6. **Krolestwo zniszczone:** dlug przepada - licznik "Bank stracil" (zloto juz wczesniej przeszlo z Banku do skarbca i dalej do rodow; nic nie znika i nic nie powstaje).
7. **Gracz:** ta sama regula (skarbiec jego krolestwa), bez okien; komunikat po angielsku raz przy pierwszej pozyczce (gracz-krol, raz na kredyt) i przy zaleglosci
   (gracz w krolestwie dluznika - dotyczy tez jego kredytu).
8. **Wylacznik** `CrownBorrows` (czynny tylko z 165 i `IronBankEnabled`): wylaczony = stan sprzed paczki - zadnych pozyczek, rat, odsetek ani skutkow zaleglosci; dlug
   zostaje w zapisie i rusza po wlaczeniu.

**Liczby i uzasadnienie** (t10r-120, ostatnie 28 dob, skrypt `scratchpad/p186/an.py`; symulacja `p186/sim2.py`):
- Brak zwrotu swiata ok. 40 tys./dobe (wyplacone ok. 82%): Krolewska Przystan 8.1 tys. (67%), Polnoc 6.2 (72%), Volantis 5.3 (28% - rata reparacji 4.5 tys.),
  Smocza Skala 3.8, Dorne 3.4, Norvos 2.1, Dolina 1.9, Targaryenowie 1.9, Reach 1.6, Qohor, Pentos, Tyrosh po ok. 1.1 tys.
- **180 dni sredniego podatku** = pol roku podatkow (podatek ok. 0.74 wplywow dnia - 155 z 210 tys. w swiecie): Krolewska Przystan i Polnoc po ok. 1.7 mln, Volantis
  ok. 0.8 mln, Norvos ok. 0.4 mln. Pelny limit przy racie 30% podatku i 30% rocznie korona splaca w pokoju w ok. 830 dob; dlug ok. 60 dni podatku (typowy po 120 dobach) -
  w ok. 230 dob. [H] korony zyly z kredytu na 1-2 roczne dochody (Edward III u Bardich i Peruzzich), dlug Roberta 6 mln.
- **2 mln progu:** Bank ma ok. 4.9 mln; 1 mln to prog kredytu wojennego rodow (168), drugi milion - miejsce na KW (dzis dlug KW ok. 0.12 mln). Korony moga wziac
  ok. 2.9 mln - tyle, ile w lore Bank pozyczyl Zelaznemu Tronowi (ok. polowa z 6 mln).
- **1/60 nadwyzki dziennie:** bez tego korony wziely by ok. 40 tys./dobe do wyczerpania nadwyzki (ok. 75 dob przy zwrocie 100%), a potem zwrot spadlby z dnia na dzien
  do ok. 82% (urwisko: zwrot jest w D rodu, wiec pulap wojska 166 rosnie i potem tnie - zwolnienia). Z 1/60 (symulacja, start z t10r): doby 8-20 ok. 100%, d40 ok. 94%,
  d80 ok. 88%, d120 ok. 85% (bez 186 ok. 82-84%); Bank d120 ok. 2.9 mln, dlug koron ok. 2.2 mln, pozyczone dzis ok. 15 tys., raty ok. 12 tys.
- **Rata 1/182 najwiekszego dlugu:** pol roku - termin pozyczki Banku dla rodow; stala kwota (jak rata kredytu), bo rata od biezacego dlugu maleje geometrycznie
  i dlugu nigdy nie domyka. **30% podatku** - wiecej niz rata Banku rodu (10% D), bo korona nie ma kiesy rodziny ani lupu; 70% zostaje na dary, zwrot i renty.
- **28 dob / 50%:** miesiac gry, polowa raty. Rata <= 30% sredniego podatku placona jako pierwsza - zdrowa korona zawsze ja placi; zaleglosc tylko przy zalamaniu
  wplywow (utrata lenn, wplywy dnia + 1/360 < 15% sredniego podatku przez wiekszosc miesiaca).
- **1.5:** ten sam mnoznik, co "Bank pozycza wrogom bankrutow" w starym Banku (`IronBank.Limit`).

**Platnik -> odbiorca.**

| Moneta | Platnik | Odbiorca |
|---|---|---|
| kredyt korony | kapital Banku | skarbiec krolestwa (tego samego dnia -> glowy rodow / rycerze jako zwrot zoldu 165) |
| rata | skarbiec (z wplywow dnia i 1/360 zapasu) | kapital Banku |
| odsetki, oplata 2% | - (tylko zapis dlugu) | - |
| dlug zniszczonego krolestwa | - (przepada, licznik "Bank stracil") | - |
| zysk Banku ponad 5 mln | kapital Banku | kasa Braavos (168, bez zmian) |

**Kod.** Nowy `Armoury/src/CrownBorrow.cs` (sredni podatek, odsetki, rata, zaleglosc, kredyt, linia "Kredyt korony (186)", zapis `arm_crown186`); zmienione
`KingdomTreasury.cs` (`WageRefund`: najpierw nalezny i reszta wplywow kazdego krolestwa w wojnie, potem `CrownBorrow.Lend` na brak - podzial puli Banku miedzy korony,
potem wyplata; kazde krolestwo we wlasnym try), `CrownIncome.cs` (`KDay.LoanIn/LoanOut`, `LastLoanIn/LastLoanOut`, linia 165), `ArmouryBehavior.cs` (krok
`CrownBorrow.Instalments` zaraz po `CrownIncome.Begin`, `CrownBorrow.Report` po `CrownIncome.End`, Reset, SyncData), `DebtLadder.cs` (`Limit` x `CrownBorrow.LimitFactor`,
KW i pozyczka gracza w Braavos zablokowane przy koronie w zaleglosci + licznik w "Dlugi (168)"), `IronBank.cs` (stary limit bez drabiny - ten sam mnoznik),
`KingdomLedger.cs` (dopisek w "Skarbce:"), `MoneyLedger.Obieg.cs` (Last* i "Obieg"), `Settings.cs` + `McmSettings.cs` (gen_mcm), `tools/gen_mcm.py` (zakresy).

**Nowe klucze (grupa "The crown's income (stage 2)"):** `CrownBorrows` true, `CrownLoanLimitDays` 180, `CrownLoanRepayDays` 182, `CrownLoanMaxIncomeShare` 0.30,
`CrownLoanBankFloor` 2 000 000, `CrownLoanPoolDays` 60, `CrownArrearsEnemyCredit` 1.5. Stale w kodzie: okno zaleglosci 28 dob, prog 50%, srednia podatku 84 doby,
kredyt od 7. doby pomiaru podatku. Oprocentowanie i oplata - klucze Banku (`IronBankRateKing` 20, `IronBankRatePerLoan` 10, `IronBankRateAfterDefault` 15,
`IronBankLoanFeePercent` 2).

**Nowe / zmienione linie logu:**
- NOWA (po "Korona: wplywy dnia (165)") `Kredyt korony (186): dzien N | pozyczono dzis X zl u n krolestw (brak zwrotu Y u m; bez kredytu: zaleglosc a, pomiar
  podatku < 7 dob b, na limicie c, limit przycial o d; pula Banku dzis P = 1/60 kapitalu ponad 2000000, przycieta o Q) | splacono R zl (raty nalezne S u t krolestw,
  zaplacone ponizej raty u); odsetki narosle I, oplaty E | dlug razem D u K krolestw; w zaleglosci Z[: nazwy (od dnia)]; nowe zaleglosci dzis, koniec zaleglosci |
  Bank: kapital C, prog kredytu koron 2000000, Bank stracil (krolestwa zniszczone) dzis l (n), razem L | od poczatku: pozyczono, splacono | na krolestwo (dlug/limit,
  oprocentowanie, rata zaplacona/nalezna, +pozyczone dzis, raty 28 dob): ...` (codziennie przy 186; wylaczone - tylko gdy stoi dlug, z dopiskiem WYLACZONE).
- NOWE (zdarzenia) `Kredyt korony (186): <K> pozycza w Zelaznym Banku X zl na zwrot zoldu (brak dzis Y), R% rocznie + oplata 2%; limit ... (180 x sredni podatek ...); rata ...`
  (pierwsza pozyczka kredytu), `... - ZALEGLOSC: w 28 dobach zaplacilo A z B zl rat (< 50%) - ...`, `... - koniec zaleglosci (28 dob pelnych rat) ...`,
  `... splacilo dlug w Banku - koniec zaleglosci.`, `<K> (id) zniszczone - dlug w Banku X zl przepada (Bank stracil razem L).`
- ZMIENIONA `Korona: wplywy dnia (165)`: `| wydane z wplywow: rata kredytu Banku (186) X, dary (182) ...`, `zwrot zoldu G (nalezny H, wyplacone P%; w tym z kredytu
  Banku (186) L)`; na krolestwo ` rata186 -X kredyt186 +Y`.
- ZMIENIONA `Korona: dzien N - zwrot zoldu ...`: `; z kredytu Banku (186, brak zwrotu - kapital -> skarbiec -> rody) X w N krolestwach`.
- ZMIENIONA `Skarbce: dzien N - <K>`: `, dlug w Banku (186) D (rata dzis a/b, pozyczone dzis c[, ZALEGLOSC])` przy dlugu.
- ZMIENIONA `Dlugi (168)`: w "bez KW" - `, korona w zaleglosci w Banku (186) N`.
- ZMIENIONA `Obieg: dzien N`: korona `, 186: rata kredytu korony skarbce -> Bank X, kredyt Banku -> skarbce (na zwrot zoldu) Y`; Bank `; 186: kredyt koron kapital ->
  skarbce Y (krolestw n), raty skarbce -> kapital X, Bank stracil dzis (krolestwa zniszczone, zloto nie rusza) L`.

**Odstepstwa / rozstrzygniecia (z powodem):**
1. Sredni podatek = nasze liczniki wplywow dnia (bez "innych do skarbcow od wczoraj"): jednorazowe przelewy (bunty, Diplomacy) nie sa podatkiem i potrafia jednego dnia
   podniesc limit o ponad milion; podatek gry/BK od bogatych rodow (w "innych") limitu nie podnosi - Bank liczy ostroznie.
2. Rata od najwiekszego dlugu (stala kwota), nie od biezacego - inaczej rata maleje geometrycznie i dlug nigdy nie dochodzi do 0.
3. "Podatki dnia" w pulapie raty = sredni podatek (84 doby), nie podatek dzisiejszy: przy pulapie od dzisiejszego podatku rata zawsze by sie zmiescila (placona
   pierwsza) i zaleglosc nie mialaby jak powstac; tak - korona, ktorej wplywy runely (utrata lenn), nie placi pelnej raty.
4. Pula dnia 1/60 nadwyzki Banku (nowy klucz, poza zadaniem) - bez niej urwisko kredytu po ok. 75 dobach (rozdz. "Liczby").
5. Odsetki +10 pp przy biegnacym dlugu jak `LendKw` (kredyt dobierany codziennie = ok. 30%), kara po zaleglosci +15 pp tylko dla nowych pozyczek (dlug w toku bez
   zmiany procentu - bez spirali u korony, ktora i tak nie placi).
6. Zaleglosc zamyka kredyt rodom korony w drabinie (KW, pozyczka gracza) i w starym Banku (limit x0); wrogowie - x1.5 do limitu Banku (KW rosnie przez `room`
   = limit - dlug; pula wolnego kapitalu KW bez zmian). Bez osobnej kolejki "pierwszenstwa" w kapitale Banku.
7. Brak komunikatu dla gracza-wasala przy pierwszej pozyczce jego korony (tylko krol); przy zaleglosci - kazdy gracz w krolestwie dluznika (blokuje i jego kredyt).

**Czego sie spodziewac w tescie 120 dob (start z zapisu t10r - Bank ok. 4.9 mln):**
- Doby 1-6: `Kredyt korony (186)` - "pomiar podatku < 7 dob" u ok. 10-13 krolestw, pozyczono 0. Od doby 7: pozyczono ok. 35-45 tys./dobe u 8-13 krolestw
  (Krolewska Przystan, Polnoc, Volantis, Smocza Skala, Dorne, Norvos, ...), pula Banku ok. 45 tys.; potem pula maleje (d60 ok. 24 tys., d120 ok. 15 tys.) - "przycieta" > 0.
- `Korona: wplywy dnia (165)` "wyplacone": doby 7-20 ok. 97-100%, d40 ok. 94%, d80 ok. 88%, d120 ok. 85% (t10r bez 186: ok. 81-82%); "w tym z kredytu Banku" = "pozyczono dzis".
  `Korona: niedoplata 28 dob (169c)`: Polnoc i Krolewska Przystan wyraznie nizej w dobach 20-50; Volantis i Pentos - wedlug limitu (maly podatek).
- Bank: kapital z ok. 4.9 mln do ok. 2.9-3.2 mln w d120 (nigdy ponizej 2 mln), dlug koron ok. 2-2.3 mln; zysk do kasy Braavos 0. Kontrola co do 1 zl:
  "pozyczono dzis" = "kredyt koron kapital -> skarbce" (Obieg) = suma "kredyt186 +" w 165; "splacono" = "raty skarbce -> kapital" = suma "rata186 -".
- Zaleglosci: 0 (ewentualnie krolestwo, ktore stracilo wiekszosc lenn); "Bank stracil" > 0 tylko przy zniszczeniu krolestwa z dlugiem.
- `Dlugi (168)`: "korona w zaleglosci w Banku (186) 0"; KW bez zmian (Bank ponad 1 mln).
- Wojsko lordow w wojnie: wyzszy zwrot to wyzsze D rodow krolestw z brakiem -> pulap 166 w pierwszych 40-60 dobach wyzej, potem lagodnie wraca (pula Banku maleje -
  bez urwiska).

**Ryzyka:** (a) przy dlugiej wojnie dlug koron rosnie o odsetki, a Bank zostaje blisko progu 2 mln - kredyt staje sie "pozyczam rate" (pozyczone ok. splacone);
prawdziwa splata dopiero w pokoju (ok. 230 dob dla dlugu ok. 60 dni podatku); (b) kolysanie pulapu 166 w krolestwach z brakiem (zwrot jest w D) - lagodzi pula 1/60;
(c) w pokoju rata do 30% podatku zmniejsza renty 180 zadluzonych koron (renty z reszty wplywow) - zamierzone (obsluga dlugu); (d) korona zniszczona z dlugiem - Bank traci
(licznik); (e) gracz-krol nie ma przycisku "nie pozyczaj" - jedyna droga to wylacznik `CrownBorrows` (jak u AI).

**Recenzja wlasna (diff 432e8e9..57fbf30; poprawki w osobnym commicie).** Sprawdzone: zloto (kredyt: dlug zapisany, kapital Banku -> skarbiec tym samym przelewem;
rata: skarbiec -> kapital; odsetki, oplata i "Bank stracil" - tylko zapis; pieniadz kredytu lezy w skarbcu tylko wewnatrz `WageRefund` - nagroda za wielkiego jenca,
kontrakty i wezwania biegna wczesniej, renty pozniej biora `LeftFor` = min(reszta wplywow, skarbiec), a reszta po zwrocie z kredytem jest 0), podwojne liczenie
(migawka skarbcow 165 przy `End` obejmuje rate i kredyt - jutrzejsze "inne do skarbcow" ich nie widza; w "Obieg" ten sam przelew w sekcji korony i Banku - jak KW),
wyjatki (kazde krolestwo i kredyt we wlasnym try; dwuprzebiegowy `WageRefund` przy braku kredytu zachowuje sie jak dotad - wyplata krolestwa A nie zmienia
reszty wplywow krolestwa B), zapis (12 pol na kredyt, stary zapis bez klucza - pusto, kredyt od 7. doby pomiaru; starszy DLL klucz pomija - Bank traci pozyczone, zlota nie przybywa),
wylacznik (`CrownBorrows` / 165 / Bank wylaczone: bez rat, odsetek, kredytu i skutkow zaleglosci - `LimitFactor` 1; jedyna zmiana bez wylacznika - kolejnosc
obliczen w `WageRefund`, wynik ten sam).

**Poprawione:**
1. Krolestwo bez dnia 165 (wyjatek w `CrownIncome.Begin`) nie pozycza: jego reszta wplywow to 0, wiec kredyt wzialby caly nalezny zwrot (przed 186 - zwrot 0).
2. Ten sam przypadek przy racie: bez raty i bez wpisu w oknie 28 dob (brak pomiaru nie jest zaleglosci); odsetki narastaja.
3. Skarbiec nie przyjal wyplaty kredytu (wyjatek setera) - zloto wraca do kapitalu Banku, dlug cofniety (jak `CrownRents` przy kiesie glowy).
4. Srednie podatku krolestw, ktorych juz nie ma (bez dlugu), usuwane - nie rosna w zapisie.
5. "Pula Banku dzis" w linii liczona przed sprawdzeniem chetnych.

**Uwagi bez zmian w kodzie:** (a) korona z zapasem ponad rezerwe 500 000 tez pozycza - zapas schodzi tylko 1/360 dziennie (regula 165); zadanie: "wplywy dnia nie
starcza" - bez warunku na zapas; (b) "wyplacone" w 165 i niedoplata 28 dob (169c) licza zwrot z kredytem - prog etapu 2 "zadne krolestwo w wojnie > 50% niedoplaty"
jest teraz czesciowo spelniany dlugiem (linia 186 mowi ile); (c) zwrot z kredytu wchodzi do D rodu (`KRefund`) jak kazdy zwrot - pulap 166 rosnie razem z kredytem.
