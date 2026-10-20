## 2026-10-20 (p5-inzynieria: R3-10b Inzynieria przy budowach)

**Mod:** Armoury | **Galaz:** `w-toku/p5-inzynieria` (od `5189c0fe` = kandydat WGRANIA 92) | **Commit:** `0f2b2665` | **Tor:** S (bez gry - zakaz uruchamiania; build Armoury kod 0, 0 bledow, gen_mcm uruchomiony - 2065 ustawien Armoury; Armoury.dll z tego commitu md5 `59c5a8489cb5eb3bae466933cd56cd2f`) | **Klucz (nowy, domyslnie TAK; w Armoury.json Jeffa go nie ma - wchodzi domyslna z kodu):** `EngineeringOnPaidWork` ("Engineering On Paid Work", grupa "Army purchases", tuz pod Build Wages By Town) | **Zapis:** bez zmian (nic w SyncData; liczniki tylko na dobe) | **Pliki:** NOWY `Armoury/src/BuildFunding.Engineering.cs`; wpiecia jednolinijkowe w `BuildFunding.cs` (PowerPostfix, PowerIntPostfix, Daily x3); `Settings.cs` (pole), `McmSettings.cs` (gen_mcm).

**Po ludzku (dla Jeffa):** Zarzadca miasta albo zamku, ktory zna sie na Inzynierii, znow przyspiesza budowy, za ktore placi pan: za te same pieniadze mur rosnie szybciej o 0,25% za kazdy punkt Inzynierii zarzadcy (Inzynieria 100 = o czwarta czesc szybciej). Wracaja tez perki budowy zarzadcy: Carpenters (miasto), Military Planner (zamek), Stonecutters (mury, koszary, wodociagi) i Clockwork (przyspieszenie zlotem w miescie). Zarzadca codziennie zbiera 10 doswiadczenia Inzynierii za kazdy oplacony punkt budowy (dzis typowa osada dostaje ok. 6 punktow dziennie, czyli ok. 60 doswiadczenia). W dymku budowy zobaczysz osobna pozycje "Governor's engineering".

### Problem / zrodlo pomyslu

- POMYSLY-35-2026-10-20.md nr 32 (R3-10b), Jeff 20.10: "28 - nie, 29 - nie. Pozostale tak". Zrodla pomyslu: `docs/audyt-2026-10-09/13-UMIEJETNOSCI.md` Z11 (Inzynieria martwa poza oblezeniami), ESENCJA-AUDYTOW :123.
- Przyczyna w kodzie (sprawdzona w dekompilacji): gra dodaje dzienny postep budowy w `BuildingsCampaignBehavior.TickCurrentBuildingForTown`: `building.BuildingProgress += town.Construction`, a `Town.Construction` = `BuildingConstructionModel.CalculateDailyConstructionPower(town)`. Czynny lancuch modeli: `NavalDLCBuildingConstructionModel` -> `BKROTConstructionModel` -> `BKConstructionModel` (-> vanilla). Nasz `BuildFunding.PowerPostfix` (priorytet Last, tylko najbardziej zewnetrzne wywolanie - `_depth`) dla osad objetych rozliczeniem (`Covered`: AI zawsze, gracz przy `PaidConstructionPlayer`) PODMIENIA caly wynik na `ExplainedNumber(punkty pana) + przyspieszenie zlotem + roboty ludu`. Razem z wynikiem gry ginely: `SkillHelper.AddSkillBonusForTown(DefaultSkillEffects.TownProjectBuildingBonus)` (zarzadca, Inzynieria, AddFactor 0.0025 za punkt), perki zarzadcy `Engineering.Carpenters` (miasto, +12%), `MilitaryPlanner` (zamek, +25%), `Stonecutters` (+30% dla SettlementFortifications, CastleFortifications, CastleBarracks, SettlementBarracks, SettlementWaterworks - lista BK) i `Clockwork` (miasto, +20% kwoty przyspieszenia zlotem). Inzynieria rosla tylko przy oblezeniach (gra nie daje XP Inzynierii za budowy; `OnSettlementProjectFinished` daje 1000 XP Zarzadzania).
- Liczby z dzis (log testu 160 dob na WGRANIU 91, `Armoury-2026-10-20_00-44-29.log`): "Budowy oplacone: dzien 109077 - osad 69 ... punktow budowy 543", "dzien 109236 - osad 60 ... punktow budowy 350" - ok. 6 punktow pana na osade dziennie; premii zarzadcy w tych punktach nie bylo.

### Zmiana

- `BuildFunding.Engineering.cs` (partial BuildFunding):
  - `EngExtra(town, paid, boost, count)` - CZYSTE liczenie (bez stanu, bez alokacji; ExplainedNumber bez opisow nie alokuje): zarzadca zywy; Clockwork jak w grze bez warunku obecnosci (miasto, tylko przy przyspieszeniu zlotem: + boost x SecondaryBonus); reszta jak w grze tylko przy zarzadcy NA MIEJSCU (`Governor.CurrentSettlement.Town == town`) i niepustej kolejce: baza = punkty pana + przyspieszenie zlotem (+ Clockwork), `SkillHelper.AddSkillBonusForTown(TownProjectBuildingBonus)`, Military Planner (zamek) albo Carpenters (miasto), Stonecutters dla murow, koszar i wodociagow. Zwraca tylko DODATEK (wynik - baza pana). Wartosci perkow i efektu brane z obiektow gry (gdyby BK je zmienil - wejda zmienione).
  - `EngAdd` - pozycja dymku "Governor's engineering" w PowerPostfix (osada objeta rozliczeniem; lenna gracza bez PaidConstructionPlayer ida modelem gry, ktory te premie juz liczy - bez dubla).
  - `PowerIntPostfix` (moc bez przyspieszenia, `BuildingHelper.GetDaysToComplete`): + dodatek zarzadcy do punktow pana.
  - `EngPaid(town, pts)` - w bloku dobowym (watek glowny) zaraz po zaplacie: liczniki i XP: zarzadca na miejscu dostaje `pts x 10` XP Inzynierii (`HeroDeveloper.AddSkillXp(Engineering, xp, true, true)` - z mnoznikiem nauki jak XP zarzadcy w grze). pts = punkty oplacone przez pana PRZED premia.
  - `EngLine()` - linia logu po "Budowy oplacone" (patrz Test w grze).
- Roboty ludu (P2, "Town works (paid by the townsfolk)") - bez premii i bez XP (pomysl mowi o budowach oplaconych przez pana).
- Ekonomia zamknieta jak dotad: zloto i material bez zmian (pan placi tyle samo, kasy i targi dostaja tyle samo), premia to wiecej pracy z tych samych pieniedzy.

### Decyzje (Claude, zgodnie z "ekonomie projektuje Claude")

1. Premia tylko do punktow pana i przyspieszenia zlotem (oba placi wlasciciel), nie do robot ludu - doslownie wedlug pomyslu ("budowy oplacone przez pana").
2. Warunki i wartosci identyczne z gra/BK (zarzadca na miejscu, lista Stonecutters z BK z wodociagami, Clockwork bez warunku obecnosci) - zeby "perki wracaja" znaczylo dokladnie to, co gra pisze w ich opisie.
3. Nie przywracam innych skladnikow wyniku gry, ktore PowerPostfix tez zastepuje (Steward.ForcedLabor, perk BK Civil Engineer pana, innowacja Cranes, lojalnosc, kultura) - poza zakresem pomyslu (nic ponad zadanie).
4. XP 10 za punkt na stale (bez suwaka) - jeden klucz na pomysl.
5. Liczenie premii przy ticku osady (stan zarzadcy w chwili budowy), XP przy zaplacie w bloku dobowym (raz na dobe; po wczytaniu ze srodka doby punkty odtwarza A14 DayHeld bez ponownego XP).

### Ryzyko / co sprawdzic (kontrola calosci)

- Dubel: w osadach objetych rozliczeniem wynik gry (z premia zarzadcy) jest w calosci zastapiony, nasza premia wchodzi raz; w lennach gracza bez PaidConstructionPlayer nic nie dodajemy (premia gry zostaje). `_depth` - tylko najbardziej zewnetrzny model (Naval), wiec premia nie liczy sie dwa razy w lancuchu.
- Czytelnicy `Town.Construction`: `BuildDiary` (dziennik - pokaze moc z premia), `BuildFunding.Folk` (lud nie przeplaca ponad koszt poziomu: `rem -= t.Construction` - teraz liczy tez premie zarzadcy, czyli dokladniej). Zaden inny kod nie czyta `_funded`.
- Szybsze budowy AI: AI przydziela zarzadcow (`ClanVariablesCampaignBehavior`); linia logu pokaze, ile osad ma zarzadce na miejscu i jaka jest premia w procentach punktow pana. Spodziewane kilka-kilkanascie procent (Inzynieria zarzadcow AI zwykle niska); przy 30%+ sprawdzic.
- XP: zarzadcy AI i towarzysze gracza beda rosli w Inzynierii (ok. 60 XP dziennie przed mnoznikiem nauki). Uboczny skutek: wyzsza Inzynieria podnosi cene i pensje towarzysza (cennik towarzyszy liczy umiejetnosci) - do obserwacji. Powiadomienia o nowym poziomie Inzynierii tylko dla bohaterow gracza (jak kazde XP w grze).
- Watki: PowerPostfix wolaja tick osady i dymki (watek glowny); `EngExtra` i tak nie ma stanu, liczniki doby pisze tylko blok dobowy, potkniecia `Interlocked`, blad raz na sesje (nie gasi funkcji).
- Klucz wylaczony (albo PaidConstruction wylaczone) = dokladnie stara sciezka (EngExtra 0, brak XP, linia z "nie").

### Status

**DO SPRAWDZENIA** (zbudowane, zacommitowane, nie wgrane, nie testowane w grze - zakaz uruchamiania gry w tym zadaniu).

### Test w grze

- Linia co dobe po "Budowy oplacone": `Inzynieria przy budowach (R3-10b, Engineering On Paid Work tak): dzien N - osad oplaconych przez pana A, z zarzadca na miejscu Z (miasta x, zamki y), z Inzynieria > 0 E (srednio S, najwiecej M); premia zarzadcow (stan przy zaplacie) +P pkt do B pkt pana (Q%); perki: Carpenters c, Military Planner m, Stonecutters s, Clockwork k; XP Inzynierii zarzadcom X (zarzadcow K); potkniecia 0.`
- Regula dziala, gdy: A = "osad" z linii "Budowy oplacone"; Z > 0 i K = Z; X = 10 x suma punktow osad z zarzadca (X > 0); P > 0, gdy E > 0; potkniecia 0. B ~ "punktow budowy" z "Budowy oplacone" (+ przyspieszenie zlotem gracza).
- W grze: lenno gracza z towarzyszem-zarzadca (Inzynieria > 0) - dymek mocy budowy ma pozycje "Governor's engineering" (= punkty pana x Inzynieria x 0,25% + perki); w oknie postaci towarzysza Inzynieria rosnie z dnia na dzien.
