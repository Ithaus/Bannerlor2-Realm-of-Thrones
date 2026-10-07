<!-- SKLAD TOWARY 3 (07.10.2026), galaz t3-sklad w klonie lancuch, baza 126 (4563c75). NIEWGRANE - **Status** dopisze sie przy wgraniu.
     Paczka 138 = commit 918938a na t3-sklad (zrodlo: n131e-budowy-z-dobrobytu 3290b01). Opis: docs/przekazanie-lawa-2026-10-07/CHANGELOG-wpisy-3-6.md (UWAGA autorow: liczby kuzni w opisie 137 - projekt 3 + 2 x tier, karnet 15 d - sa NIEAKTUALNE po 142:
     dzien kuzni 3 d x poziom plac). -->

## 2026-10-07 (138) - BUDOWY: PLACE ROBOTNIKOW WEDLE DNIOWKI MIASTA TARGOWEGO - pens plac kupuje tyle pracy, ile dniowka w miescie, z ktorego sa murarze i robotnicy (dla zamku najblizsze miasto); materialy jak dotad
**Mod:** Armoury | **Pliki:** `BuildFunding.cs` (NOWE `PointsFor`, `WageIdx`; linia dnia "Budowy oplacone" ze srednim poziomem plac), `Settings.cs` + `McmSettings.cs` (NOWE `BuildWagesByTown` = true; 643 -> 644).

**Problem:** jak wyzej (decyzja Jeffa 07.10); spis (pozycja 25): budowy w lennach - pan placi 10% dochodu dziennie, 25% na materialy z targu, 75% na place; punkt budowy = zaplata / 48 d (wojskowe) albo 24 d (cywilne) - wszedzie tak samo, choc dniowka murarza w Kings Landing i w Pebbleton historycznie rozni sie 2 x.

**Przyczyna:** `BuildFunding.Daily`: `pts = (matSpent + labourI) / ppp` - pens plac liczony jak pens materialu w kazdym miescie.

**Zmiana (wylacznik `BuildWagesByTown`):** `PointsFor` = (materialy + place / poziom plac miasta targowego) / cena punktu. Cena punktu (48 / 24 d) jest teraz cena przy zwyklej dniowce. Budzet, podzial 25/75 i platnicy - bez zmian.

**Przyklady (proba, sekcja 13; 100 d dziennie = 25 materialy + 75 place):** srednie miasto: budowla cywilna 4.17 pkt (jak dotad), mury 2.08; Kings Landing: 3.20 / 1.60; Pebbleton: 5.82 / 2.91; miasto biedne (dolna granica 0.5): 7.29 / 3.65.

**Ryzyko / co sprawdzic:** REGRESJE - przy wylaczonym jak dotad (proba). SPOJNOSC - poziom plac czyta to samo miasto, z ktorego sa materialy (`NearestTown` dla zamku); dochod pana tez rosnie z bogactwem lenna (podatki), wiec w bogatym miescie wolniejsze tempo czesciowo sie wyrownuje. Linia dnia "Budowy oplacone: ... punktow budowy N (place wedle miast targowych: sredni poziom plac X)". **Co Jeff zobaczy:** budowy w bogatych miastach ida troche wolniej za te same pieniadze, w biednych szybciej.

**Skladanie TOWARY 3:** commit 918938a - bez konfliktow.
