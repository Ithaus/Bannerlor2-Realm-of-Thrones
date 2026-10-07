<!-- SKLAD TOWARY 3 (07.10.2026), galaz t3-sklad w klonie lancuch, baza 126 (4563c75). NIEWGRANE - **Status** dopisze sie przy wgraniu.
     Paczka 136 = commit 0ec2b5c na t3-sklad (zrodlo: n131c-place-warsztatow-z-dobrobytu cb21de2; opis docs/przekazanie-lawa-2026-10-07/CHANGELOG-wpis-2-warsztaty.md). NOWY TownWage.cs. -->

## 2026-10-07 (136) - WARSZTATY PLACA Z DNIOWEK HISTORYCZNYCH I DOBROBYTU MIASTA - warsztat broni placi za dzien roboty dniowke mistrza wedle tieru sztuki (ta sama, z ktorej liczy sie jej wartosc: 3 d t1 .. 10.5 d t6; dotad 3 d za kazdy dzien), a kazdy warsztat placi place i utrzymanie wedle poziomu plac swojego miasta (dobrobyt / 4800, 0.5-1.5)
**Mod:** Armoury | **Pliki:** NOWY `TownWage.cs` (jeden wzor poziomu plac miasta dla calej gry; czytaja go naprawy, warsztaty, kuznia, budowy, a przez refleksje RealisticCaptivity i GrandTourney), `HistoricalPrices.cs` (NOWE `DayWageOf` - dniowka mistrza przy sztuce; `MakingLabor` z niej), `MendMaterial.cs` (`LocalWage` -> `TownWage.Index`), `WorkshopLaw.cs` (NOWE `DayWage`; koszt przy wyborze, place przy wydaniu, ranking i diagnoza), `WorkshopTrade.cs` (`UpkeepAt`, `WageIndex`; place cyklu `CycleLabour`, utrzymanie `ExpensePrefix`, prog bankructwa, oczekiwany zysk, liczba bez kapitalu, linia dnia), `Settings.cs` + `McmSettings.cs` (NOWE `WorkshopWageByTier` = true; 641 -> 642).

**Problem:**
- Decyzja Jeffa 07.10: "wszelkie koszty w danym miescie powinny byc zalezne od dobrobytu i stawek historycznych"; "na stawkach w naszej grze - nasze stawki sa oparte o stawki historyczne - wszystko, co dotyczy pieniadza, placenia musi byc spojne".
- Warsztat broni placi 3 d (`WorkshopWagePerDay`) za kazdy dzien roboty, a wartosc tej samej sztuki liczy te same dni po dniowce mistrza wedle tieru (`HistoricalPrices.HistCost`: 3 d + 1.5 d na tier, t6 10.5 d): przy Brigandine (123 dni) warsztat placi 369 d robocizny wycenionej na 1292 d i roznice bierze jako zysk. Dowod z logu (autotest 3, 08:03, 40 dob): warsztaty broni wykonaly 31 358 szt. za 428 tys. d kosztu i sprzedaly za 1.33 mln (3.11 x koszt); ostatnia doba 605 szt.: koszt 7245, sprzedaz 33 069 (4.6 x).
- Place i utrzymanie wszedzie takie same (3 d roboczodzien, 4 d utrzymania na dobe), w Kings Landing i w Pebbleton.

**Przyczyna:** `WorkshopLaw` (cykl uzbrojenia): `float wage = s.WorkshopWagePerDay` dla kazdej sztuki; `WorkshopTrade.CycleLabour` = `WorkshopWorkers` x `WorkshopWagePerDay` / suma szybkosci; `Upkeep` = `WorkshopTradeUpkeepPerDay` - bez miasta.

**Zmiana (wylacznik `WorkshopWageByTier`, domyslnie wlaczony; wylaczony = 3 d i 4 d wszedzie jak dotad):**
1. NOWE `TownWage.Index(miasto)` = dobrobyt / `TownWageRefProsperity` (4800 = mediana 97 miast w tescie Jeffa), 0.5-1.5 - jedyny wzor; naprawy (`MendMaterial.LocalWage`) czytaja go stad.
2. Warsztat broni: placa za roboczodzien przy sztuce = `HistoricalPrices.DayWageOf(sztuka)` (dniowka mistrza wedle tieru, strzaly i belty - t1; ta sama co w wartosci i w naprawie) x poziom plac miasta (`WorkshopLaw.DayWage`) - w progu oplacalnosci, w placach przy wydaniu sztuki, w rankingu wyrobow i w diagnozie "pusty ranking". Dni roboty (`HistDays`) bez zmian.
3. Warsztat towarowy: place czeladnikow cyklu i utrzymanie mistrza z czynszem x poziom plac miasta (`WorkshopTrade.WageIndex`, `UpkeepAt`); cena sprawiedliwa (123), cena warsztatu (116), prog bankructwa i licznik "bez kapitalu" licza z tych samych liczb. Linia dnia: "zasady: utrzymanie 4 d na dobe x poziom plac miasta (dobrobyt / 4800, 0.5-1.5)".

**Przyklady (proba, sekcja 11; warsztat dostaje 90% ceny targu, tu cena = wartosc):**

| Sztuka (dni roboty) | Miasto | Dniowka | Place | Koszt (material + place) | Warsztat dostaje | Zysk |
|---|---|---|---|---|---|---|
| miecz Highland Broad Blade t5 (8) | srednie | 3 -> 9 d | 24 -> 72 | 35 -> 83 | 94 | 58 (165%) -> 10 (13%) |
| Brigandine t6 (123) | srednie | 3 -> 10.5 d | 369 -> 1292 | 710 -> 1632 | 1836 | 1127 (159%) -> 204 (13%) |
| Brigandine t6 | Kings Landing | 3 -> 15.21 d | 369 -> 1871 | 710 -> 2211 | 1836 | 1127 -> -375 (nie kuje, dopoki cena nie wzrosnie do ok. 1.2 x wartosci) |
| Brigandine t6 | Pebbleton | 3 -> 6.86 d | 369 -> 844 | 710 -> 1184 | 1836 | 1127 -> 652 (55%) |
| buty Stark t2 (3.5) | srednie | 3 -> 4.5 d | 11 -> 16 | 18 -> 23 | 26 | 8 (45%) -> 3 (13%) |
| warsztat towarowy (utrzymanie na dobe / place 6 czeladnikow przy pelnej pracy) | srednie / Kings Landing / Pebbleton | | 18 / 26.1 / 11.8 d | utrzymanie 4 / 5.79 / 2.61 d | | |

**Autotest 40 dob (12:15) wobec autotestow 3 i 5 (bez paczki):** warsztaty broni wykonaly 29 732 szt. (31 358 / 30 284), koszt 534 tys. d (428 / 444 tys.), sprzedaz 1.36 mln (1.33 / 1.38 mln) - sprzedaz / koszt 2.55 (3.11 / 3.11); "bez zysku" 1987 (2080 / 1582); warsztaty towarowe w 40. dobie +8094 (+8914 / +8357); cena warsztatu dla gracza mediana 13 785 (13 343 / 13 013); ERROR 0, potkniecia 0. Produkcja broni praktycznie bez zmian (roznice w granicach rozrzutu przebiegow), koszt wyzszy o ok. 22% - w swiecie przewazaja tanie sztuki (helmy, luki, drzewce, rekawice, plaszcze), a ceny targu stoja wyzej niz wartosc (braki), wiec warsztaty dalej zarabiaja - juz z prawdziwych cen, nie z zanizonej dniowki.

**Ryzyko / co sprawdzic (kontrola wg zasady glownej):**
- REGRESJE: przy wylaczonym `WorkshopWageByTier` - 3 d i 4 d wszedzie (proba). `HistCost` (wartosc) bez zmian - `MakingLabor` liczy z `DayWageOf` te same liczby (proba: 5 przedmiotow co do tysiecznej).
- KOLIZJE: merge-tree z 10 galeziami w toku - tylko stary konflikt konstruktora; rownolegla k13-3 (rzemioslo miasta, `TownCrafts`) placi 3 d bez poziomu plac - przy skladaniu przelaczyc na `TownWage` / `WorkshopLaw.DayWage`.
- SPOJNOSC: jedna dniowka mistrza (wartosc, naprawa, warsztat), jeden poziom plac (`TownWage`). Ekran rodu ("Daily Wage" warsztatu gracza) pokazuje dalej 4 d - to wlasciwosc modelu gry bez wskazania warsztatu (zaplata idzie wedle miasta).
- EKONOMIA: w bogatych miastach drogie sztuki kuja sie dopiero przy cenie wyzszej niz wartosc (place 1.45 x) - produkcja przesuwa sie do biedniejszych miast, ceny w bogatych rosna z brakow (podaz i popyt); autotest nie pokazal spadku produkcji calego swiata.
- `tools/sprawdz_logi.py` - linie bez zmian formatu (poza dopiskiem w "zasady:").

**Co Jeff zobaczy w grze:** to samo co dotad na targu; warsztaty w bogatych miastach placa wiecej i drozej wyceniaja sie przy kupnie (zysk), w biednych taniej. W ustawieniach Armoury nowe "Workshop Wage By Tier" (wlaczone).

**Skladanie TOWARY 3:** commit 0ec2b5c - bez konfliktow. `TownWage.Index(Settlement)` i `Settings.TownRentFloorGold` czytaja przez refleksje GrandTourney i RealisticCaptivity (TownWageLink) - nazwy sa kontraktem, w lancuchu bez zmian (sprawdzone: proby 14 i 15 lawy na DLL lancucha zaliczone). Te sama dniowke x poziom plac bierze od 149 rzemioslo miasta.
