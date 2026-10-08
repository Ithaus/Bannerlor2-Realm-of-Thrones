<!-- SKLAD TOWARY 3 - dokonczenie 08.10.2026, galaz t3-sklad w klonie lancuch. NIEWGRANE - **Status** dopisze sie przy wgraniu.
     Paczka 159 = zgloszenie recenzji ceny sprzedazy (155) 07.10 i STAN-PRAC "OTWARTE (sprzed paczki): CleanseAmmo leczy tez kulawe konie gracza" (propozycja osobnego zadania od pomocnika wycofana - poprawka w tym lancuchu).
     Proba: towary3\dokonczenie\pl (kopia proby lawy + NOWA sekcja 19), wyniki p2\wyniki-E-przed159 (DLL 158) i p2\wyniki-F (DLL 159). -->

## 2026-10-08 (159) - CLEANSEAMMO NIE LECZY KONI: kulawy kon gracza zostaje kulawy - amunicja i towar jak dotad bez ujemnych stanow, kon i zwierze zachowuja swoj stan
**Mod:** Armoury | **Pliki:** `ArmouryBehavior.cs` (`CleanseAmmo`: pomija `IsBeast`; linia logu "Amunicja oczyszczona ze stanow: N szt. (konie i zwierzeta zachowuja stan - poprawka 159)"). Bez nowych ustawien (poprawka bledu - zamiar CleanseAmmo to amunicja i towar), bez zapisu w grze.

**Problem (recenzja paczki 155 "cena sprzedazy sprzetu", 07.10; dowod z kodu i proby):** `CleanseAmmo` (start kazdej sesji - `OnSessionLaunched` - i koniec kazdej bitwy gracza) zdejmuje ujemne stany z amunicji i z kazdego "towaru" w sakwach gracza. Kon nie ma komponentu broni ani zbroi, wiec `IsGoods` bral go za towar: kulawy kon (`lame_horse`, Native price_factor 0.1) wracal zdrowy przy kazdym wczytaniu i po kazdej bitwie. Kulawy kon kupiony w miescie tanio (55% x polka) i sprzedany zdrowy (do ok. 93% x polka) dawal zarobek z niczego przy Handlu 300 - w miastach tak samo przed paczka 155 jak po niej (BK koni nie mnozy), w zamku po 155 (kara BK x3 zamiast x9) przy skrajnym stosie tez minimalnie. To samo dotykalo zwierzat ze stanem (owca, krowa).

**Przyczyna:** `if (!IsAmmo(el.Item) && !IsGoods(el.Item)) continue;` - `IsGoods` = brak komponentu broni i zbroi, a kon i zwierze (`IsBeast`: HorseComponent, ItemType Horse / Animal) go nie maja. Opis `IsBeast` mowi wprost: "Kulawy kon to kulawy kon (vanilla), nie kon do naprawy".

**Zmiana:** w petli sakw `IsBeast` -> pomijamy (stan konia i zwierzecia zostaje, takze dodatni). Amunicja (sakwy i kolczany na grzbiecie) i towar (jedzenie, surowce - stany z innych modow) - jak dotad.

**Proba (kopia proby lawy, NOWA sekcja 19: sakwy z 2 kulawymi konmi, koniem "spirited" 1.2, owca ze stanem 0.5, 3 strzalami "bent" 0.7, 4 rybami 0.5 i kolczan "bent" na grzbiecie; prawdziwe `ArmouryBehavior.CleanseAmmo`):**
- PRZED (DLL 158): 146 z 147 - kulawe konie i owca wracaja zdrowe ("Amunicja oczyszczona ze stanow: 11 szt."), strzaly i ryby czyste.
- PO (DLL 159): **147 z 147** - kulawe konie (2) zostaja kulawe, kon "spirited" i owca bez zmian; strzaly (3 + kolczan) i ryby (4) czyste jak dotad ("... 8 szt. (konie i zwierzeta zachowuja stan - poprawka 159)").
- Inne proby na DLL 159: konie 149 z 149, zamowienia 130 z 130, kwatermistrz r0 52 z 52 (r1 47 z 52 jak przed).

**Ryzyko / co sprawdzic (zasada 0):** kto jeszcze zmienia stan konia gracza: kowale miasta - nie (brak receptury; od 158 `lame_horse` 0.1 jest tez "wrakiem" dla `TroopSelfMend` bez materialu - tam kulawe konie w zbrojowni ludzi tez juz nie zdrowieja); wlasne kowadlo - konie poza systemem zuzycia (`NoWear`). Sufit ceny skupu (155) dalej nie obejmuje koni (`IsBeast`) - po tej poprawce nie ma petli "kup kulawego, sprzedaj zdrowego": kulawy sprzedaje sie jako kulawy. W logu: linia "Amunicja oczyszczona ze stanow" z dopiskiem 159.

**Co Jeff zobaczy w grze:** kulawy kon w sakwach zostaje kulawy po wczytaniu gry i po bitwie (dotad cudownie zdrowial) - mozna go sprzedac jako kulawego albo uzywac; strzaly i belty dalej wychodza z bitwy czyste.

**Poprawka po recenzji dokonczenia (08.10):** sprostowanie - w grze `lame_horse` ma cene 0.5 (RBM nadpisuje Native 0.1), wiec NIE jest wrakiem z 158, a `TroopSelfMend` nie mial filtra konia: przy wylaczonych naprawach z materialem (`MendMaterialMenAndLords` / `SmithMendFromMarket` = false) kowale "leczyli" kulawego konia w zbrojowni wojska za 1/4 utraconej wartosci. Teraz `TroopSelfMend.Mendable` i `Run` pomijaja `IsBeast` - kon zachowuje stan na kazdej drodze. Proba lawy, sekcja 19b: przed 149 z 150, po 150 z 150. Wpis w CHANGELOG "159, poprawka po recenzji dokonczenia TOWARY 3".
