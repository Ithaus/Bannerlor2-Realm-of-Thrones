# SKLAD TOWARY 3 (127-150) - galaz t3-sklad (07.10.2026)

> ERRATA 08.10 (po audycie; szczegoly i poprawki: `WYNIK.md` obok): (1) w grze jest n120 + W2 (Armoury 62e37795), a nie n120 25b87631 -
> W2 dolozone do galezi (1a2a6a3, 80dbb34, 3a9a41e); (2) sekcja 4: ustawien Armoury wzgledem GRY (n120) 631 -> 667 (36; brakowalo 121
> HistMixedCategoryShelf, 122 VillageCartWholeStore, 123 WorkshopTradeFairPrice, 125 NoFreeTimberAndTools, 126 VillageWoodlotLoads), po
> poprawkach 671 (+3 W2, +1 MendMaterialMenAndLords); w `Armoury.settings.xml` 127 zmienil tylko SmithingSkillPerTier (35) i dopisal
> SmithingDifficultyPerTier (45) - DurabilityPerArmorPoint i MinSellPercentOfValue w XML nie ma; PRZY COFNIECIU DLL przywrocic Armoury.json
> (.bak albo 45 / 20.0 / 5); (3) sekcja 5, gt_events: format i znaczenie 4. pola BEZ zmian (juz w n120 pula gracza prizeGold) - zmienia sie
> rozliczenie (pula do zwyciezcy, przy odwolaniu cala); (4) sekcja 7, wiersz 128: 15 latek = 10 + 5 latek naprawy 134 (133 w tej probie
> nie dochodzi); (5) HEAD galezi po poprawkach: c1e8bea, DLL: `dll-final` (stare `dll` = 8ee9c94, NIE do wgrania).

- Klon: `scratchpad\lancuch` (sesja 3cf3e0ac), galaz **t3-sklad**, worktree `scratchpad\dzien-6\towary3\repo`.
- Baza: `n126-drewno-z-produkcji` 4563c75 (121..126 w lancuchu, niewgrane). W grze: n120 + W2 - Armoury md5 62e37795 = build git archive 39b60a0 (sprostowanie 08.10; dotad pisalo tu n120 25b87631).
- HEAD: **8ee9c94** "docs: TOWARY 3 - opisy paczek 127-150" (kod = c3d163e "150"). 31 commitow na bazie: 24 paczki + 5 poprawek po przegladzie/recenzji + 135 czesc 2 + docs.
- NIC nie wgrane do gry, NIC nie wypchniete, repo Jeffa (`C:\Users\GAME\Bannerlor2-Realm-of-Thrones`) i `Armoury.json` tylko czytane.
- Kazde ogniwo: cherry-pick, konflikty wedle opisow obu paczek, `python tools/gen_mcm.py`, build Armoury (po zmianach GT/RC - ich build) z kodem wyjscia 0 przed nastepnym ogniwem.

## 1. Paczki: numer -> commit -> tytul -> galaz zrodlowa

| Nr | Commit t3-sklad | Tytul | Zrodlo (galaz, commit) |
|---|---|---|---|
| 127 | 5fad11e | POKRETLA JEFFA - kowalstwo na poziom 35, zuzycie zbroi 1.5 x szybsze, cena minimalna 2% | n127-pokretla-jeffa 95baa63 |
| 128 | 9a42bdf | SPOILS OF WAR BEZ AUTOMATYCZNEJ SPRZEDAZY I BEZ ZLOTA Z NICZEGO | n128-spoils-bez-darmowego-zlota 83a8b1d |
| 129 | e496e9b | diagnoza zatkanych wsi (tylko log) | d126-diagnoza-zatkanych (w-toku/diagnoza-zatkanych-wsi) c87e321 |
| 130 | 34f435d | wozy nie utykaja w miastach | w126-wozy-nie-utykaja (w-toku/wozy-nie-utykaja-w-miastach) f8dced3 |
| 131 | dd48c12 | pamiec drog mapy ROT uzupelniona | w127-pamiec-drog (w-toku/pamiec-drog-rot-uzupelniona) c92f7e7 |
| 132 | 60b0d70 | ZBROJA Z KUZNI JAK BRON - zepsuta, slaba, dobra, bardzo dobra, legendarna wedle kowalstwa | n129-zbroja-craft-jak-bron 2344a61 |
| 133 | b50d454 | SPOILS: KLAN NAJEMNIKOW TYLKO Z PRAWDZIWYCH ZOLNIERZY | n130-spoils-najemnicy-prawdziwi 383a724 |
| 134 | 020d540 | SPOILS: NAPRAWA U KWATERMISTRZA PLACI KOWALOM MIASTA I ZUZYWA MATERIAL, BEZ ODNAWIANIA WRAKOW | n131-spoils-kwatermistrz-z-materialem f0aef78 |
| 135 | 97eec88 | LAWA NAPRAWCZA U KOWALA: ZA MONETE ROBOTA + MATERIAL Z TARGU, BEZ WRAKOW | n131b 81109ab |
| 135 (czesc 2) | 52ae550 | ROBOTA KOWALI MIASTA Z DNIOWEK HISTORYCZNYCH I DOBROBYTU MIASTA | n131b-lawa-naprawcza-z-materialem b36a6d6 |
| 136 | 0ec2b5c | WARSZTATY PLACA Z DNIOWEK HISTORYCZNYCH I DOBROBYTU MIASTA (NOWY TownWage.cs) | n131c-place-warsztatow-z-dobrobytu cb21de2 |
| 137 | bfaf320 | WYNAJEM KUZNI W SKALI HISTORYCZNEJ I WEDLE DOBROBYTU MIASTA | n131d-wynajem-kuzni-z-dobrobytu a9bb933 |
| 138 | 918938a | BUDOWY - PLACE ROBOTNIKOW WEDLE DNIOWKI MIASTA TARGOWEGO | n131e-budowy-z-dobrobytu 3290b01 |
| 139 | 61fd3b5 | NIEWOLA - DNIOWKA, WARTA I DOM W STAWKACH HISTORYCZNYCH, PLACI KASA OSADY (RealisticCaptivity) | n131f-niewola-stawki-historyczne 14504a5 |
| 140 | cc40d0a | TURNIEJE - OPLATA DO KASY MIASTA, PULA DLA ZWYCIEZCY, UTARG Z KASY MIASTA, KWOTY X POZIOM PLAC (GrandTourney) | n131g-turnieje-stawki-historyczne e010a25 |
| 141 | 99c8614 | KUZNIA ZA KAZDY DZIEN ROBOTY; PUSTE KASY OSAD (NIEWOLA) | n131h 5b121ea |
| 142 | fdddb38 | JEDNA CENA DNIA KUZNI (BEZ WZGLEDU NA TIER); REZERWA MIASTA PRZY SKUPIE DOMU I UTARGU TURNIEJU (Armoury, GT, RC) | n131h c471705 |
| 141/142 popr. | e933fd4 | (poprawka do 141 i 142) DOBA KUZNI W JEDNYM REJESTRZE Z KARNETEM BK (W ZAPISIE GRY); KAZDA ROBOTA PLACI | n131h 1fe5c35 |
| 135 popr. | 24807dd | (poprawka do 135) NAPRAWA ZALOZONEJ SZTUKI ZOSTAJE W KSIEDZE STANU; APEL KWATERMISTRZA JAK "SEND THE MEN'S WORN GEAR" | n131h 100112a |
| 140 popr. | cb75543 | (poprawka do 140) TURNIEJE - PULA GRACZA WRACA, GDY GRA ODWOLA TURNIEJ (OBLEZENIE) | n131h-puste-kasy-kuznia-za-dzien ad6f6ce |
| 143 | 8ee8b5f | KONIE PO CENIE TARGU - REKRUT KONNY I HODOWCA (HorsesAtMarketPrice) (robocze 7a) | n131i e10b449 |
| 144 | 492b8a7 | ZYWY INWENTARZ W CENACH HISTORYCZNYCH (HistLivestockPrices) (robocze 7b) | n131i 22336e0 |
| 143 popr. | e94199b | (poprawka do 143) KON - JEDNA CENA NA TARGU (REKRUT, HODOWCA I LORD Z POLKI JAK NOTABL I GRACZ) | n131i-konie-i-zwierzeta 7d919ee |
| 145 | b07458c | ZAMOWIENIE SPRZETU DLA LUDZI Z POLKI MIASTA - CENA POLKI + CHODZENIE KOWALA, BRAK TOWARU DLA WARSZTATOW (robocze 8) | n131j ce26642 |
| 145 popr. | 16930b8 | (poprawka do 145) ZAMOWIENIE Z POLKI - WYCENA BEZ RUSZANIA POLKI, ZDJECIE DOPIERO PRZY DOSTAWIE | n131j-zamowienie-sprzetu-z-polki a645723 |
| 146 | 97e33e7 | KSIEGA TOWAROW (tylko log) (robocze K13 127) | k1-ksiega-towarow 49b0b62 |
| 147 | 0793e93 | RZEMIESLNICY BK: KAZDA SZTUKA Z WLASNEGO WSADU (robocze K13 128) | k2-rzemieslnicy-bk-z-wsadu 9c95d07 |
| 148 | 4ded6cc | RZEMIOSLO MIASTA WEDLE WARTOSCI - folowanie i tkanie welny, tkanie lnu, garbowanie (robocze K13 129) | k3-rzemioslo-miasta 9f7408f |
| 149 | 6609ad4 | rzemioslo miasta placi wedle dobrobytu (TownWage) - NOWA przy skladaniu | - |
| 150 | c3d163e | ODZIEZ, BUTY I PLOTNO WOJSKA - zolnierze zdzieraja i kupuja w miastach z sakiewki (robocze K13 130) | k4-odziez-wojska 93ac4c1 |
| docs | 8ee9c94 | docs: TOWARY 3 - opisy paczek 127-150 | - |

Autor i data autora commitow - ze zrodla (Jeff, czas zrodla); wiadomosci commitow z "NN" -> numer; poprawki z dopiskiem "(poprawka do NNN)".
Opisy: `repo\docs\paczki\127..150-*.md` (naglowek: commity, zrodlo, mapowanie numerow w tekscie; na koncu kazdego wpisu "Skladanie TOWARY 3") i te same wpisy na gorze `CHANGELOG.md` (150 ... 127, bez Status).
UWAGA: `CHANGELOG.md` galezi lancucha konczyl sie na 102 - wpisy 103-126 sa tylko w repo Jeffa (CHANGELOG.md do 120, docs/paczki/121-126); przy wgraniu wpisy 127-150 przeniesc na gore CHANGELOG.md repo Jeffa (nad 120 / opisy 121-126) i dopisac Status.

## 2. Konflikty i jak rozwiazane

| Ogniwo | Plik / miejsce | Rozwiazanie |
|---|---|---|
| 129 diag | `ArmouryBehavior.cs` linia konstruktora (Reset) - z 128 `SpoilsSeal.Reset()` | suma obu list Reset (skrypt `ctor_merge.py`: diff3, kazdy Reset z obu stron, kolejnosc zrodla) |
| 130 wozy | linia konstruktora | suma (CartTownExit.Reset); komentarze "paczka NN" -> 130 |
| 131 pamiec drog | `SubModuleMain.cs` linia `CartTownExit.ApplyAll` (u mnie juz z etykieta 130) | wersja 131: `RoadMemoryFix.ApplyAll` przed `CartTownExit.ApplyAll`, etykiety 131 / 130 |
| 132-134, 135, 135 cz. 2, 136-142, 141/142 popr., 140 popr., 143-145 (+ popr.) | - | bez konfliktow tekstowych; sprawdzone semantycznie: `RepairAll` lawy (`PlanRepair` tylko stan < 100) zgodny z regula 132; `SpoilsSeal.Reset` ma i `SpoilsCompany.Reset` (133), i `MendMaterial.Reset` (134); zbroja-craft vs Forge/RepairAll stosu lawy - jedna petla przywracania |
| 135 popr. (100112a) | `ArmouryBehavior.ResetSlotCondition` - 132 przepisywal wpis ksiegi tylko przy jakosci zbroi i tylko gdy wpis byl; przeglad - nowy stan jako oryginal, 4 pola, dopisanie | jedna metoda: po naprawie zalozonej sztuki wpis = `BookLine` (5 pol) przy jakosci zbroi, inaczej 4 pola z nowym modyfikatorem; dopisany, gdy go nie bylo. Proba lawy 16 (132 wyl.), NOWE 16c (132 wl.) i 16d (legenda u kowali miasta z materialem) - zaliczone |
| 146 k1 | linia konstruktora | suma (GoodsLedger.Reset) |
| 148 k3 | (bez konfliktu) przepis `k13-rzemioslo-miasta\scalenie-k1.diff` | nalozony: `FTownCraft` = 18, pozycja "rzemioslo miasta (148)", ramka wokol `Work(town)` |
| 150 k4 | linia konstruktora; przepis `k13-odziez-wojska\scalenie-k1-130.diff` | suma (`ArmyClothing.Reset` po `MenPurse.Reset`); przepis z przenumerowaniem `FArmyCloth` = 19, Kinds 20, ujscie "odziez wojska (150)"; `MoneyLedger` `NCloth` = 6, Notes 7 (nikt inny w lancuchu nie dopisuje pozycji Notes - p114 tu nie ma) |

Przenumerowanie etykiet w kodzie / logach (komentarze i napisy logu; napisy w grze bez numerow):
- "paczka NN" -> 130 / 131 (CartTownExit, RoadMemoryFix, SubModuleMain, ArmouryBehavior);
- stos lawy: "paczka 7a" -> 143 (RecruitCost, Stables, VolunteerKit), "paczka 7b" -> 144 (HistoricalPrices), "paczka 8" -> 145 (SmithMenu, SupplyDemand);
- K13: "(127)" / "paczka 127" -> 146 ("GoodsLedger: ksiega towarow (146)"; GoodsLedger, OreLedger, ArmouryBehavior, SubModuleMain) - prawdziwe "127" (pokretla) w komentarzach 132 zostaja;
  "(128)" -> 147 ("Rzemieslnicy BK (147)", "rzemieslnicy BK (147)" w ksiedze pieniadza; ArtisanInputs, MoneyLedger, WorkshopTrade) - prawdziwe "128" (Spoils) zostaja;
  "(129)" -> 148 ("Rzemioslo miasta (148)", linia "Warsztaty"; TownCrafts, WorkshopLaw, CaravanBulk, ArmouryBehavior, SubModuleMain; "krok 136" planu opisany jako roboczy krok planu);
  "(130)" -> 150 ("Odziez wojska (150)", "na odziez wojska (150)", "BkSupplyTemper: ... (150, ...)"; ArmyClothing, BkSupplyTemper, MenPurse, MoneyLedger, SoldierPay, Settings, ArmouryBehavior) - prawdziwe 130 (wozy) zostaja.
- Etykiety "Spoils of War (128)", "Las wsi (126)" - prawdziwe, bez zmian.

## 3. 149 i polka 150 / 145

- **149 (NOWY commit 6609ad4):** `TownCrafts` (148) liczyl prace w bramce zysku po 3 d wszedzie; teraz roboczodni x `WorkshopWagePerDay` x `TownWage.Index(miasto)` (ten sam wylacznik `WorkshopWageByTier` co place linii towarowych 136), roboczodni na sztuke bez zmian, zlota dalej nie ma. Rachunek: przerob, gdy indeks wyrobu >= 0.75 x indeks surowca + 0.25 x poziom plac (bogate +0.125, biedne -0.125 wartosci wyrobu wobec sredniego). Proba (proba autora 148 + regula harnessu z poziomem plac + scenariusz W): on 83/83, on2 83/83, off 40/40; W: dobrobyt 2400 / 4800 / 7200, filc 110 d -> 3 / 5 / 0 filcow (progi 70 / 95 / 120 d) = harness; DLL 148 na nowej regule 76/83 (proba lapie roznice). Wybralem `WorkshopWagePerDay` x `TownWage.Index` (wzor `WorkshopTrade.CycleLabour` dla linii towarowych), nie `WorkshopLaw.DayWage(it, town)`, bo `DayWageOf` to dniowka mistrza ZBROJNEGO wedle tieru sztuki - dla towarow (filc, plotno, skora) nie ma sensu.
- **Polka 150 vs 145:** bez podwojnego liczenia, bez poprawki. Zamowienie (145) bierze z polki tylko sprzet (`SupplyDemand.Equipmentish`: helmy, zbroje, bron - koszyki typ x tier), odziez (150) tylko skore, sukno i plotno (towary) - rozne sztuki; popyt 145 idzie do `SupplyDemand.NoteUnmetOnce` (koszyki sprzetu), 150 nie dopisuje popytu nigdzie (cena tylko przez oproznianie polki); 145 placi kiesa gracza, 150 sakiewka ludzi / kasa zamku. Wspolne sa tylko pozniejsze, prawdziwe zuzycia tego samego towaru (warsztaty zbrojne bior skore i len na przeszywanice - wedle ich wlasnego popytu). Zuzycie sprzetu (AiWear, naprawy) to zuzycie walki, odziez - codzienne (marsz) - dwa zjawiska.

## 4. Zmiany DOMYSLNYCH wartosci ISTNIEJACYCH ustawien (Armoury.json Jeffa ma pierwszenstwo nad kodem)

| Modul | Klucz | Kod: bylo -> jest | Paczka | Armoury.json Jeffa (07.10) | Przy wgraniu |
|---|---|---|---|---|---|
| Armoury | `SmithingSkillPerTier` | 45 -> 35 | 127 | 45 | ZMIENIC na 35 (gra zamknieta, kopia .bak) |
| Armoury | `DurabilityPerArmorPoint` | 20 -> 13.33 | 127 | 20.0 | ZMIENIC na 13.33 |
| Armoury | `MinSellPercentOfValue` | 5 -> 2 | 127 | 5 | ZMIENIC na 2 |

GrandTourney i RealisticCaptivity: zadna domyslna istniejacego klucza sie nie zmienia. `GrandTourney.json` Jeffa - brak nowych kluczy; RealisticCaptivity - Jeff nie ma pliku MCM (obowiazuja wartosci z kodu). `ModuleData\Armoury.settings.xml` (wartosci startowe bez MCM) - 127 poprawil tam te same trzy liczby.
Kontrola po edycji: `grep '"SmithingSkillPerTier"\|"DurabilityPerArmorPoint"\|"MinSellPercentOfValue"' Armoury.json` -> 35 / 13.33 / 2. NIE dopisywac nowych kluczy (brak = domyslne z kodu).

Istniejace klucze, ktorym zmienia sie ROLA (wartosc bez zmian; w pliku Jeffa zostaja dla wylacznikow):
- Armoury `ForgeFeePerTier` (json 2): przy `ForgeHireHistorical` (nowy, wl.) dzien kuzni = `ForgeFeeBase` (json 3) x poziom plac, bez tieru (142); `ForgeDayHours` (json 8.0) dzieli dzien kuzni na stawke godzinowa BK (137).
- Armoury `TroopOrderMarkup` (json 1.15): przy `TroopOrderFromShelf` (nowy, wl.) nieuzywany - cena polki + chodzenie kowala (145).
- Armoury `RepairCostFactor` (json 0.5): przy `SmithMendFromMarket` (nowy, wl.) naprawy u kowali miasta licza robocizne z dniowek x dobrobyt (135), stary rachunek tylko przy wylaczonym.
- Armoury `MendMaterialMaxShare` (json 0.2, kod 0.2 - bez zmiany): od 135 (czesc 2) ten sam udzial liczy takze robote naprawy (ulamek dni roboty wykonania sztuki), nie tylko material.
- Armoury `ArtisanTanWeavePerCycle` (brak w json, 5): przy `TownCraftsEnabled` (nowy, wl.) nieuzywany (148).
- Armoury `WorkshopWagePerDay` (brak w json, 3): przy `WorkshopWageByTier` (nowy, wl.) x poziom plac miasta w liniach towarowych (136) i rzemiosle miasta (149); w warsztatach zbrojnych dniowka mistrza wedle tieru.
- Armoury `MarketGlutStartPercent` (json 5.0): przy prawie podazy i `OneScrapFloor` (nowy, wl.) bez dzialania (127).
- GrandTourney `HostBaseFee` (json 2000), `HostFeeProsperityFactor` (0.5), `HostTakingsProsperityFactor` (0.1), `CancelledFeeRefund` (0.5): przy `HistoricalTownRates` (nowy, wl.) oplata = `HostFeePence` x poziom plac, utarg = (`HostTakingsPerLord` x lordowie + `HostTakingsBasePence`) x poziom plac, przy odwolaniu wraca cala pula (140).

Nowe ustawienia (zadnego nie ma w plikach Jeffa - obowiazuja domyslne z kodu): Armoury 636 -> 667 (31), GrandTourney 37 -> 40 (3), RealisticCaptivity 98 -> 104 (6):
- Armoury: 127 `SmithingDifficultyPerTier` 45, `OneScrapFloor` true; 128 `SpoilsNoAutoSale` true, `SpoilsNoFreeGold` true; 129 `VillageClogDiagnostics` true; 130 `VillageCartLeaveTown` true, `VillageCartTownMaxDays` 2; 131 `MapRoadTableFix` true; 132 `ArmourCraftLikeWeapons` true; 133 `SpoilsClanRealSoldiers` true; 134 `SpoilsQuartermasterRepair` true; 135 `SmithMendFromMarket` true, `TownWageRefProsperity` 4800; 136 `WorkshopWageByTier` true; 137 `ForgeHireHistorical` true; 138 `BuildWagesByTown` true; 143 `HorsesAtMarketPrice` true; 144 `HistLivestockPrices` true; 145 `TroopOrderFromShelf` true; 146 `GoodsLedgerEnabled` true; 147 `ArtisanOwnInputs` true; 148 `TownCraftsEnabled` true, `TownCraftHandsPerArmsHand` 2; 150 `ArmyClothingEnabled` true, `ArmyClothingFieldLeatherKg` 2, `ArmyClothingFieldClothKg` 3, `ArmyClothingFieldLinenKg` 2.5, `ArmyClothingGarrisonLeatherKg` 1.2, `ArmyClothingGarrisonClothKg` 2, `ArmyClothingGarrisonLinenKg` 1, `ArmyClothingMaxWaitDays` 120.
- GrandTourney (140): `HistoricalTownRates` true, `HostFeePence` 4400, `HostTakingsBasePence` 480.
- RealisticCaptivity (139): `HistoricalTownRates` true, `LabourerDayWage` 1.5, `VillageWageShare` 0.75, `GuardBrawlBonusDays` 3, `HomeTownPence` 2400, `HomeVillagePence` 480.
(Pelna lista z porownania Settings.cs 4563c75..HEAD: `logi\ustawienia.txt`; skrypt `ustawienia.py`.)

## 5. Klucze zapisu gry (SyncData)

NOWE:
- `arm_mendstock` (134, ArmouryBehavior.SyncData) - zapas kowali miast (reszty calych sztuk materialu do napraw; `MendMaterial.Export/Import`).
- `arm_towncrafts` (148, NOWY `TownCraftsBehavior.SyncData`) - dlug wsadu, dlug rak i srednia zuzycia rzemiosla na miasto i pare (ok. 15 KB tekstu).
- `arm_armyclothing` (150, ArmouryBehavior.SyncData) - potrzeba odziezy partii (id partii) i zalog ("@" + id osady) (ok. 40 KB).
Bez klucza (stary zapis) - pusty stan; nowa gra / wczytanie czysci stan przez Reset.

ISTNIEJACE z nowa trescia (ten sam klucz, ten sam format):
- `arm_condition` (132): wpis ksiegi stanu uprzezy gracza ma piate pole (stan, w jakim sztuke zostawilismy); stary wpis 4 pola czytany jak dotad (stan oryginalny / stan zuzycia).
- `arm_daypass` (poprawka 141/142): w tym samym rejestrze (format bez zmian) takze doby kuzni wlasnych projektow, nie tylko karnet BK.
- `gt_events` (140): czwarte pole wpisu = pula gracza czekajaca na zwyciezce (dotad oplata gospodarza, ktora przy odwolaniu wracala w polowie) - zapis z OGLOSZONYM turniejem gracza sprzed 140 potraktuje dawna oplate jak pule.

## 6. Build i DLL (z czystego `git archive` HEAD 8ee9c94, katalog `archiwum`)

`python tools/gen_mcm.py` w archiwum - McmSettings bez zmian (to, co w commitach). Build `-c Release -v q --nologo "-p:GameLibs=C:/Users/GAME/Bannerlor2-Realm-of-Thrones/libs"`: Armoury kod 0 (stare CS0169), GrandTourney 0, RealisticCaptivity 0 (stare CS0618), CrashScribe 0 (niezmieniony w lancuchu - DLL nie dolaczony; w grze zostaje zatwierdzony CrashScribe 11fa0214).

| DLL (`towary3\dll`) | md5 |
|---|---|
| Armoury.dll | ca2a3b29e29b0d1c4be54891dd92c4fe |
| GrandTourney.dll | e8f21fcf9e7c350aac8b609994c274c3 |
| RealisticCaptivity.dll | 7bbc1312a3da3e5dd9429ae472fadfd6 |

md5 zalezy od miejsca budowania (archiwum bez .git = bez numeru commitu w AssemblyInformationalVersion). Kontrola przed wgraniem: `git diff t3-sklad <galaz wgrania> -- Armoury GrandTourney RealisticCaptivity` pusty + build kod 0.

## 7. Proby poza gra na DLL calego lancucha (prawdziwe Harmony, DLL gry, BK; kopie prob w `towary3`, katalogi autorow nietkniete)

| Proba (paczka) | Wynik | Uwagi |
|---|---|---|
| pokretla (127) | nowa 22/22, wylacznik 22/22 | |
| spoils (128) | 75/76 | licznik latek Spoils 15 zamiast 10 - 133 i 134 dopisuja swoje (zgodnie z opisem) |
| diag (129) | 69/69 | |
| wozy (130) | 51/52 | `RoadMemoryFix.ApplyAll` (131) stoi miedzy MarketCarts a CartTownExit - projekt 131 |
| pamiec drog (131) | 56/56 | |
| zbroja (132), regula kowali miasta wylaczona | nowa 73/73, wylacznik 54/55 | W8 "zastane" juz nie wystepuje - poprawka 135 dziala takze przy wylaczonym 132 |
| zbroja (132), regula kowali wlaczona (domyslna) | 7 x FAIL (W4-W9) | atrapa proby bez targu z materialem - kowale nic nie naprawiaja (regula 135); to samo z materialem: proba lawy 16c / 16d OK |
| najemnicy (133) | 74/74 | |
| kwatermistrz (134), kopia lawy | REGULA_KOWALI=0 52/52, =1 48/52 | 4 kontrole z robocizna 25% na sztywno (przed 135) - ten sam wynik zglosila sesja lawy |
| lawa (135-142, +14 RC, +15 GT) | 121/121 | + sekcje 16 (132 wyl.), 16c, 16d; licznik ustawien uzupelniony o ustawienia spoza stosu lawy |
| konie (143-144) | 136/136 | (licznik ustawien jw.) |
| zamowienia (145) | 130/130 | (licznik ustawien jw.) |
| ksiega towarow (146) | 66/66 | |
| rzemieslnicy BK (147) | off 35/35, on 40/40, on2 40/40 | |
| rzemioslo miasta (148 + 149) | on 83/83, on2 83/83, off 40/40 | etykiety 148, regula z poziomem plac, scenariusz W |
| odziez wojska (150) | off 32/32, on 47/47 | etykiety 150 |
| finalne DLL z archiwum | lawa 121/121, rzemioslo on 83/83, odziez on 47/47 | |

Autotestu w grze NIE bylo (zadne DLL nie wgrane).

## 8. Otwarte / do wiadomosci przy wgraniu

- Ksiega towarow (146, tylko log): material zdejmowany z polek przez kowali miast na naprawy (134/135, `MendMaterial.Commit` - ruda, drewno, skora, len, welna) nie ma wlasnej ramki - pojdzie w "bez wyjasnienia" tych towarow (kontrola ruda / drewno ZGODNA od tego nie zalezy). Poprawka = jedna ramka wokol `MendMaterial.Order.Commit` (nie robilem - nikt jej nie opisal).
- Wpisy CHANGELOG 103-126 w tej galezi nie istnieja (sa w repo Jeffa) - patrz sekcja 1.
- 16 niskich zgloszen przegladu stosu lawy (PRZEKAZANIE-DLA-DRUGIEGO-CZATU.md sekcja 5) - dalej otwarte.
- Paczki "wyspy" i ewentualna "cena sprzedazy" - poza ta grupa (dojda na koncu).
- Test w grze: NOWA kampania (123/124/148/150 licza start; G1 = 147 + 148 + 150 razem), autotest 40 dob; w logu linie "Rzemioslo miasta (148)", "Odziez wojska (150)", "Rzemieslnicy BK (147)", "GoodsLedger: ksiega towarow (146)", "Spoils of War (128)", "Zatkane wsie (diagnoza)", "Wozy w miastach".

Narzedzia skladania (towary3): `b.sh` (gen_mcm + build, kod wyjscia), `c.sh`/`msg.py` (commit z wiadomoscia zrodla, NN -> numer, autor zrodla), `ctor_merge.py` (konflikt linii konstruktora), `pick.py` (wybor strony konfliktu z podmianami), `relabel.py`, `nn.py`, `gen_docs.py` (opisy), `ustawienia.py` (porownanie ustawien); logi w `logi\`.
