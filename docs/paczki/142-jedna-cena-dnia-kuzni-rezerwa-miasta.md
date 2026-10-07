<!-- SKLAD TOWARY 3 (07.10.2026), galaz t3-sklad w klonie lancuch, baza 126 (4563c75). NIEWGRANE - **Status** dopisze sie przy wgraniu.
     Paczka 142 = commit fdddb38 na t3-sklad (zrodlo: c471705, galaz n131h-puste-kasy-kuznia-za-dzien; Armoury, GrandTourney, RealisticCaptivity). Opis: docs/przekazanie-lawa-2026-10-07/CHANGELOG-wpisy-3-6.md (UWAGA autorow: liczby kuzni w opisie 137 - projekt 3 + 2 x tier, karnet 15 d - sa NIEAKTUALNE po 142:
     dzien kuzni 3 d x poziom plac). -->

## 2026-10-07 (142) - KUZNIA: JEDNA CENA DNIA KUZNI, BEZ WZGLEDU NA TO, CO KUJESZ; REZERWA MIASTA PRZY SKUPIE DOMU I UTARGU TURNIEJU
**Mody:** Armoury, RealisticCaptivity, GrandTourney | **Pliki:** Armoury `Forge.cs` (`ForgeFee`, NOWE `DayRentFee`, `PayDayRent` bez tieru, `ForgeDayRent` = `ForgeFeeBase` x poziom plac), `Patches.cs` (`DayPass.EnsureBought` = `DayRentFee`), `ArmouryBehavior.cs`; RealisticCaptivity i GrandTourney `TownWageLink.cs` (NOWE `Spare`), `Homes.cs` (`SellPrice`, sprzedaz domu), `TourneyBehavior.cs` (utarg). Commit c471705 (galaz n131h), bez nowych ustawien.

**Problem:** decyzja Jeffa 07.10: "koszt kuzni to koszt kuzni, a co ja kuje to moja sprawa" - po paczce kuzni 137 (a9bb933) dzien kuzni kosztowal (3 + 2 x tier) x poziom plac, a karnet BK jak robota t6 (15 d); do tego pytanie Jeffa o rezerwe miasta (20 000, `TownRentFloorGold`): miasto skupujace dom i dzielace sie utargiem turnieju moglo zejsc ponizej rezerwy (Banner Kings odbiera wtedy dobrobyt), a paser, przetop i karawany w Armoury trzymaja te rezerwe.

**Przyczyna:** tier w `ForgeFee` / `PayDayRent` / `ForgeDayRent`; `SellPrice` i utarg liczone do calej kasy osady.

**Zmiana:** dzien kuzni = `ForgeFeeBase` (dniowka rzemieslnika, 3 d - tyle kowal traci, oddajac kuznie) x poziom plac miasta, najmniej 1 - jedna cena dla wlasnego projektu i karnetu BK (`DayRentFee`), bez wzgledu na tier; przy wylaczonym `ForgeHireHistorical` - dawna oplata wedle tieru. Miasto kupuje dom i daje utarg turnieju tylko z kasy ponad rezerwe Armoury (`TownWageLink.Spare`: miasto - kasa minus `TownRentFloorGold`, wies - cala kasa, bez Armoury - cala kasa); przy niepelnym utargu komunikat "The coffers of X could spare only N of the M gold in takings.". Dniowki z biezacej kasy jak dotad.

**Przyklady (proba, sekcje 12, 14, 15):** dzien kuzni: srednie miasto 3 d, Kings Landing 4 d, Pebbleton 2 d - za buty, miecz i helm tak samo (dotad 7 / 13 / 15 d jednorazowo); karnet BK tyle samo (dotad ok. 200); stawka godzinowa w BK dzien / 8. Kasa 20 500: dom sprzedasz najwyzej za 500 (cena 1440); kasa 0 - sprzedaz nieaktywna. Proba 111/111 (po poprawkach z przegladu 118/118).

**Ryzyko / co sprawdzic:** SPOJNOSC - jedna cena dnia kuzni wszedzie; ta sama rezerwa miasta co w Armoury (odczyt przez refleksje). EKONOMIA - kuznia tansza niz dotad (3 d zamiast 7-15 d za projekt), kucie drogich sztuk nie placi wiecej za kuznie (placi materialem i czasem). **Co Jeff zobaczy:** "Forge hire N gold for every day of work" - te sama kwota przy kazdej robocie w danym miescie.

**Skladanie TOWARY 3:** commit fdddb38 - bez konfliktow; build 3 modulow kod 0. Doba kuzni w jednym rejestrze z karnetem BK - poprawka po przegladzie opisana przy 141 (commit e933fd4).
