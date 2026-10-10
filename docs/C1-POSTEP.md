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
