# PLAN KODU - paczka E5/A1 (111'): kasy miast - bez "zakupow" z niczego, regulator bez dosypki (tryb 2, miasta i zamki), jedna nadwyzka D4, zawor 7% pan 2/3 : korona 1/3, dar startowy, BK rynek, bez tarcz

Status: SAM PLAN (11.10.2026). Kod, gra, ustawienia, zapisy nietkniete; nic nie budowane. Jedyny zapis poza tym plikiem: wpis-rezerwacja indeksow
MoneyLedger na gorze `CHANGELOG.md` w drzewie A1 (rozdz. 8.4).
Wiazace: `docs\etap5\PROJEKT-ETAP5-KIESA-LUDU-KASY-MIAST-2026-10-11.md` v2 (dalej **P**; A1 = P rozdz. 3 "E5/A1", reguly R1-R7, D4 w rozdz. 2, tabela 5.1,
progi rozdz. 4), `docs\etap5\KRYTYKA-E5.md` (E5K1-03, E5K1-08, K3, K6, W1, W3), rozpoznania `docs\etap5\rozpoznanie-B-kod.md` (**RB** 0.1, 2 "5.1", 3, 4, 5.4),
decyzje Jeffa `docs\STAN-PRAC.md` (E 09.10 04:25 - wyrownanie panow zamkow takze u gracza; 11.10 03:05 - testy zbiorcze; 15:50 - test zbioru A osobno)
i **DECYZJE JEFFA 11.10 ok. 16:10 (P rozdz. 6, wiazace, zmieniaja v2):** (1) **A1 bez dosypki od razu - `TownPurseRegulator` domyslnie 2** (regulator gry nic
nie kasuje i nic nie dosypuje), **miasta i zamki ta sama regula** (CastlePurse), tryb 1 jako opcja MCM; C1 (5.8) przechodzi do A1; w tescie A liczyc zaulki
(kasy ponizej zapasu / ponizej 1 000 / 0, ile dni z rzedu, czego nie kupily z braku zlota - towar wsi, ktory nie zszedl); (2) udzial zysku kupcow zabierany
przez pana i korone - wzor E5/0, "ten sam wzor po A1 w linii Kasy miast" (rozdz. 7.3).

Skroty miejsc (SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad`):
- **K:** = `SCR\e5\a1\Armoury\src\` (galaz `w-toku/e5-a1` = 392c519 = zbior 31 + E4/6 + E4/7 scalone) - wszystkie linie Armoury z tego drzewa (P cytuje
  drzewo `e4\bledy` - numery przesuniete o 2-20 linii; tu poprawione).
- **G:** = `SCR\dec32\cs_full\` (TaleWorlds.CampaignSystem 1.4.8), **BK:** = `SCR\e4tools\bkfull\` (BannerKings.Redux), **BEE:** = `SCR\e3\dec36\bee\`.
- **L:N** = linia logu biegu A zbioru 31 `SCR\kopia-z31-A\Armoury-2026-10-11_08-09-11.log` (120 dob); **sr28** = srednia dob 93-120 (P rozdz. 1).
- **P111** = `docs\paczki\111-k6-kasy-miast.md` i kod starej 111 (galaz `paczki-na-120/111-k6-kasy-miast` 2ac5e72, `TownPurse.cs` 563 linie) - wzor, NIE do przeniesienia.

---

## 0. Streszczenie decyzji planu (co wykonawca robi, czego nie)

1. **NOWY `TownPurse.cs`** (ok. 550 linii, wzor `CastlePurse.cs` 110 + stara 111, przepisany na dzisiejszy kod). Wlasciciel calego stanu A1. Klucz glowny
   `TownPurseRegulator` (0 = wszystko jak dotad, co do znaku). Wszystkie inne pliki dostaja **male wpiecia** (1-15 linii) - etap 5 bedzie scalany z E4/8,
   E4/10, E4/11 i E5/0 (rozdz. 10.9).
2. **Jedno miejsce wpiecia latek gry dla miast = trzy latki 110** (`CastlePurse.ShelfPostfix` K:CastlePurse.cs:236, `ConsumePostfix` :251, `RegulatorPostfix`
   :272): galaz miasta oddaje do `TownPurse.OnShelf/OnConsumed/OnRegulator` (jak stara 111). Nowych latek na `ItemConsumptionBehavior` i `GetTownGoldChange`
   nie przybywa. Jedyna nowa latka cudzego kodu: prefiks bool `Priority.Last` na BK `BKSettlementBehavior.HandleMarketGold` (R6).
3. **R1 "zakupy" mieszczan:** migawka kasy przed konsumpcja (postfiks `DeleteOverproducedItems`) i po (postfiks `MakeConsumption`, First) - przyrost cofniety
   (`ChangeGold(-made)`); towar schodzi z polki jak dotad ("zjedzone bez zaplaty" - do B1). Czynne takze przy tworzeniu swiata (W3: dwa ticki przed
   `GameStarted`, nieliczone w linii) - inaczej "zakupy" z tworzenia zostalyby ponad darem startowym i zawor oddalby je panom.
4. **R2 regulator - domyslnie tryb 2 (Jeff 16:10):** wynik gry = 0 w obie strony (nic w nicosc, nic z niczego) - **dla miast (TownPurse) i zamkow
   (CastlePurse, ta sama wartosc klucza; zamki tylko przy `CastlePurseEnabled`)**. Tryb 1 (opcja MCM, powrot "gdyby kasy wysychaly"): ujemny = 0, dodatni
   tylko do zapasu kupcow (liczony). Klucz `TownPurseRegulator` jest NOWY (nie ma go w `Armoury.json` Jeffa), wiec domyslna 2 nie wpada w pulapke MCM -
   osobny klucz `TownPurseNoTopUp` z P (K3, C1) zbedny. Przed `GameStarted` regulator miasta = 0 w obie strony w kazdym trybie.
5. **R3 / D4 - jedna nadwyzka:** `TownPurse.Reserve(town)` = max(`TownPurseFloorGold` + `TownPurseFloorPerProsperity` x dobrobyt, 10 000 + 12 x dobrobyt
   = cel regulatora gry). Trzy odczyty, jedna liczba:
   - `TownPurse.Spare(town)` = max(0, kasa - Reserve) - RealPayer E3/1 i porty 181 przez `Seal31.Spare` (K:Seal31.cs:108 - jedna linia; ShipsToPort181
     i RealPayer bez zmian);
   - `TownPurse.TaxFloor(town, staryProg)` - podatki z kasy: zawor, danina, mennica, clo, polityki (Crown Duty, War Tax, Road Tolls), Spoils;
   - `TownPurse.BuyFloor(town, staryProg)` - **nasze** zakupy towaru przez kase miasta (E5K1-03, jedna regula z zamkami B-2), osobny wylacznik
     `TownPaysFromSurplus` (wzor `CastlePaysFromSurplus`; P go nie wymienia - decyzja planu, zeby test mogl wylaczyc sam ten kawalek - rozdz. 9).
   Przy `TownPurseRegulator` 0 wszystkie trzy zwracaja stary prog (20 000 / cala kasa / formula Seal31 z tarczami) - stan sprzed paczki.
6. **Wpiecie D4 przez dwie istniejace funkcje zamiast 20 miejsc:** `CastlePurse.Spendable` (K:CastlePurse.cs:383 - galaz miasta zwraca dzis cala kase) ->
   `TownPurse.SpendableTown(town)`; `CastlePurse.PayFloor` (:405 - galaz miasta zwraca dzis `townFloor`) -> `TownPurse.TaxFloor(town, townFloor)`. To od razu
   obejmuje: MenPurse :525, MenUpgrade :470, ArmyClothing :233, SupplyDemand :562, BkSeal34 :192/:210 (B-2) oraz Crown Duty (K:KingdomTreasury.cs:484),
   War Tax (:511), SpoilsSeal (:413, :476). **Odstepstwo od litery P** ("CastlePurse.Spendable -> TownPurse.Spendable" w kazdym miejscu): 2 linie w jednym
   pliku zamiast 9 w 6 - mniej scalania, ta sama regula. Dopisac w CHANGELOG.
7. **Pozostale nasze zakupy kasy miasta - audyt planu (P wymienia "paser, kontrakty 174, wywoz kupcow"; reszte znalazl grep `ChangeGold(-` w K:):**
   paser band (K:OutlawLaw.cs:1936), kontrakty 174 (K:MaterialOrders.cs:800), surowce masowe karawan (K:CaravanBulk.cs:383, :579, :589), amunicja karawan
   (K:CaravanAmmo.cs:297), nadwyzki zalog i lordow (K:GarrisonArmory.cs:336), nadwyzki ludzi gracza (K:MenPurse.cs:363), zlom z musztry (K:Drill.cs:938),
   wedrowcy 36b (K:TravellerTrade36b.cs:454) - kazde jedna linia `- TownPurse.BuyFloor(town, stary)`. **Bez zmian:** RecruitKit (K:RecruitKit.cs:389 - to
   likwidacja dobytku; czego kasa nie oplaci, idzie na polke bez zaplaty - Spare zrobilby z tego wiecej towaru bez zaplaty), zwrot zamowienia 171
   (K:GarrisonCarts.cs:263 - zwrot przedplaty, nie zakup), rebelianci ROT (K:RotLeaks37.cs:158). Zamki w miejscach z tego punktu - bez zmian (B-2 ich nie
   objelo; poza A1 - CLAUDE.md 8 pkt 3, odnotowane w rozdz. 10.5).
8. **R4 zawor miast w `PopulationLaw.Daily`, nie w osobnym `TownPurse.Daily`.** Petla rent pomija miasta przy czynnym zaworze (jedna linia obok haka wsi,
   K:PopulationLaw.cs:1182 - PO liczeniu `pop/due`, wiec "nalezne" w linii "Ludnosc:" zostaja), a nowa prywatna `PopulationLaw.ValveTowns(...)` po petli
   (przed `Log.Info("Ludnosc: ...")` K:PopulationLaw.cs:1216) przechodzi WSZYSTKIE miasta (takze kultury spoza tabeli :1172 i bez ludzi :1174 - "10 miast
   bez placenia") i dla kazdego wola `TownPurse.Valve(...)`. Zapisy `RentToday`, `_rentBy`, `DayTownRent`, `paid/totalPaid` robi `PopulationLaw` jak dzis
   (:1201-1206) - jeden wlasciciel tych pol. **Odstepstwo od P** ("TownPurse.Daily zaraz po PopulationLaw.Daily"): ta sama kolejnosc w dobie (przed
   `Seal31B.OwnerTaxDaily`, w odcinku MRent ksiegi), a "renty zaplacone" w linii "Ludnosc:" dalej obejmuja miasta - kontrola `tools\obieg169_sprawdz.py:357-360`
   (renta wsi + zawor miast z "Obieg" = "renty zaplacone" z "Ludnosc:") zostaje prawdziwa.
9. **Regula zaworu (R4, jak zamek 110 + 114):** dzien: nadwyzka = kasa - Reserve; pobor = `CastlePurse.Dues(kasa, Reserve, TownRentShare)` (7%, w dol);
   `ValveSplit.Split(pobor, udzialPana)` - korona (1 - `TownRentLordShare`) do najblizszej zlotowki, pan reszte (K:ValveSplit.cs:22-28); dekret BK
   (`PopulationLaw.TaxDecree` K:PopulationLaw.cs:1097) skaluje tylko czesc pana, razem nigdy ponad nadwyzke; miasto rodu bez krolestwa - calosc panu;
   martwy/brak pana - nic nie schodzi; **oblezone - nic** (jak zamek K:CastlePurse.cs:573, bez klucza; klucz `BesiegedPayNothing` dla reszty osad - A4);
   bez pulapu "naleznej od ludzi" (zostaje liczba w linii). Pan: `GiveGoldAction.ApplyForSettlementToCharacter` (ta sama akcja co dzis :1201); korona:
   `town.ChangeGold(-x)` i `KingdomBudgetWallet += zeszlo` (jak K:CastlePurse.cs:601-607) + `TownPurse.CrownToday[k]` do 165 "wplywy dnia".
10. **R5 dar startowy:** `TownPurse.BeforeRents()` w ticku dobowym po `MoneyLedger.BlockOpen` (K:ArmouryBehavior.cs:1460), przed `PopulationLaw.Daily`
    (:1461): raz na kampanie przyciecie do Reserve, najwyzej sam dar (gra 20 000 + BK 40 x dobrobyt) x czesc, ktorej nie zjadl regulator/zawor -
    `CastlePurse.StartGiftCut` (K:CastlePurse.cs:225) i logika wieku z `CastlePurse.TrimStartGift` (:466-537, flaga "done" / "on:<wiek>"). Flaga w zapisie
    `arm_townpurse` (`SaveText.Sync`, wlasny try przed glownym blokiem, jak arm_castlepurse K:ArmouryBehavior.cs:409-418). Ten sam `BeforeRents` zamyka
    liczniki doby (ticki osad gry od poprzedniego bloku -> `Last*`) i raz porzuca znaczniki tarcz z zapisu (R7).
11. **R6 BK rynek osady:** prefiks bool na `HandleMarketGold(Settlement)` - gdy kasa miasta (zamku: tylko przy `CastlePurseEnabled`) jest ponad limitem BK
    (`GetSettlementMarketGoldLimit` - refleksja, zapas: stale 50 000 + 25/12 x dobrobyt) -> `return false` (gorna galaz: -1% w nicosc). Dolna galaz (notabl
    z >= 30 000 doplaca 1 000 pustej kasie - przelew) zostaje. Wies - bez zmian.
12. **R7 tarcze 107b/162m przy czynnym A1:** `SoldierPay.Hold`/`HoldCourt` nie dopisuja znacznikow, `RegulatorPostfix` dla miasta wraca od razu,
    `HeldOf`/`CourtOf` = 0, `DecayHeld`/`DecayCourt` czyszcza slowniki (znaczniki z zapisu porzucone - jedna linia z liczbami). **Bez `HomePart`** i bez klucza
    `CrownRefundSkipsHomeGarrisons` (K6/E5K1-08a). Kod tarcz zostaje dla trybu 0.
13. **K3 (E5K1-08b):** ciecie zwrotu korony za wydatki ludzi we wlasnych miastach rodu (K:KingdomTreasury.cs:232) = `CrownRefundOwnTownsCut` x udzial pana
    w zaworze (`TownPurse.LordShare`) przy czynnym zaworze; inaczej jak dzis.
14. **Ksiega:** dwie nowe migawki MoneyLedger **M12 `MTownTrim`** (dar startowy kas miast - ujscie w nicosc, raz) i **M13 `MTownCrown`** (udzial korony
    z zaworu kas miast); `Marks` 12 -> 14. Rezerwacja: A1 12-16, drzewo A2-A5 od 17 (rozdz. 8.4). Czesc pana z zaworu zostaje w "renty" (MRent) jak dzis.
15. **Linie logu:** bez nowej linii dziennej (P) - rozszerzona "Kasy miast (169c)" (K:Measure169c.cs:370-374; z zaulkami i udzialem zysku kupcow - Jeff 16:10,
    rozdz. 7.3); linia startowa `TownPurse (E5/A1):`; dwie
    linie jednorazowe (dar startowy, porzucone tarcze); dopiski w "Pieniadz swiata (bilans)", "(bilans - przyczyny)", "Obieg", "Korona: wplywy dnia (165)".
16. **Poza A1 (nie dopisywac "przy okazji" - CLAUDE.md 8 pkt 3):** placi lud / kiesa W/R/P i 5% zaworu dla P (B1), 1/2 : 1/2 (B3), oblezone wsie/184/polityki
    (A4), dwor 80/20 i sprzet niewydany (A2; `HoldCourt` jest no-op przy A1, A2 nie musi ruszac K:ClanBudget.cs:623), notable i karawany notabli (A3;
    `CaravanCapital` bez zmian), lup wodza (A6 - Jeff 16:20 w porcji A, drzewo A2-A5), napis K:MoneyLedger.Obieg.cs:654 (robi E5/0 - ta sama linia w dwoch
    drzewach = konflikt). **C1 (regulator bez dosypki) wchodzi do A1** (Jeff 16:10) - w C zostaje tylko kontrola.

---

## 1. Styki i zaleznosci (co A1 czyta / co czyta A1)

| # | Kto | Co | Gdzie (K:) | Skutek A1 |
|---|---|---|---|---|
| S1 | 110 CastlePurse | trzy latki konsumpcji/regulatora, `Spendable`, `PayFloor`, `Dues`, `StartGiftCut`, `Reset` | CastlePurse.cs:123, :202, :225, :236, :251, :272, :288-327, :377-411 | host wpiec miast; zamki: jedyna zmiana - tryb 2 regulatora (dosypka zamkow 0, rozdz. 3.4) i tekst linii "Zawor zamkow (110)" :662 |
| S2 | 114 ValveSplit | podzial pan/korona | ValveSplit.cs:13-28 | wspolny pomocnik ("dla 111' i KL" - komentarz :6-8) |
| S3 | PopulationLaw renty | `On` (PopulationRentEnabled), `RentToday`, `DayTownRent`, `_rentBy`/`RentOf`, `TaxDecree`, petla `Daily` | PopulationLaw.cs:37, :1004-1019, :1097-1120, :1152-1219 | miasta przez zawor A1; wsie jak dzis (B1) |
| S4 | 166 / 169c / 3.5 | czytaja `RentToday` (BuildFunding.cs:156, KingdomTreasury.cs:87, ClanIncomeBook.cs:340) i `RentOf` (ClanIncomeBook.StableD.cs:344, CouncilWages35.cs:205, Measure169c.cs:354, SoldierPay.cs:787) | - | widza czesc pana (2/3) - te same pola, bez zmian w czytajacych |
| S5 | E3/1 RealPayer, 181 porty | `Seal31.Spare` | Seal31.cs:101-113 (RealPayer.cs:97, ShipsToPort181.cs:217) | miasto = `TownPurse.Spare` (jedna definicja D4) |
| S6 | 165 korona | `CrownIncome.Begin` wplywy "nasze" | CrownIncome.cs:177-179 | + `TownPurse.CrownToday[k]` |
| S7 | K3 | `KingdomTreasury.WageRefund` ciecie | KingdomTreasury.cs:232, :248-253 | cutShare x udzial pana |
| S8 | podatki z kas | danina :371/:391, Crown Duty :484, War Tax :511, Road Tolls :528; SpoilsSeal :133/:413/:476 | KingdomTreasury.cs, SpoilsSeal.cs | od Reserve zamiast 20 000 |
| S9 | 107b/162m tarcze | `Hold` :648, `HoldCourt` :604, `RegulatorPostfix` :694, `DecayHeld` :752, `DecayCourt` :776, `HeldOf` :803, `CourtOf` :809 | SoldierPay.cs | martwe przy A1 |
| S10 | MoneyLedger | migawki, `SplitCastleMark` :514, `TrimNow` :758, `OldBalance` :764-775, linia bilansu :801; Obieg: `ClearLast169` :86-101, RB :275-299, :342, linia "Obieg" :567-572 | MoneyLedger.cs, MoneyLedger.Obieg.cs | M12/M13, dopiski |
| S11 | E5/0 (drzewo `e5\0`) | linie "Nadwyzka kas (E5/0)" (nasze zakupy ponizej zapasu), ew. nawias konsumpcji | - (rownolegle) | prog A1-T14 czyta linie E5/0, gdy jest; scalanie rozdz. 10.9 |
| S12 | A2-A5 (drzewo `e5\a25`) | A2 dwor do kas siedzib, A3 markietani do kas miast (K3!), notable przez `Spare`, A4 oblezone, A5 wplata gracza do kasy | - | korzystaja z `TownPurse.Spare/LordShare`; A1 nie zalezy od nich |

---

## 2. Stan i zapis

### 2.1 Pamiec `TownPurse` (wszystko czyszczone w `Reset`)
```
// kampania
static bool _trimDone; static double _liveAge = NaN; static bool _trimSkip;          // jak CastlePurse :95-99
static bool _shieldsDropped;                                                          // R7: znaczniki z zapisu porzucone w tej sesji (linia raz)
static bool _offLogged, _errLogged;
// nawias dziennego ticku miasta (konsumpcja -> regulator)
static Town _consTown, _regDue; static int _consGold;
// liczniki zbierane w tickach osad gry (DailyTickTownEvent rozlozony po godzinach doby) - zamyka BeforeRents
static long _dRegDown, _dRegUp, _dRegUpCut, _dConsBack, _dBkSkipGold; static int _dRegDownN, _dRegUpN, _dRegUpCutN, _dConsN, _dConsHit,
            _dBkSkipTowns, _dBkSkipCastles, _dBuyAsk, _dBuyZero, _stumbles;
// odczyty dnia (Last*) - zeruje MoneyLedger.ClearLast169 (BlockOpen), ustawia BeforeRents (regulator, zakupy, BK, Buy*) i ValveDay (zawor)
internal static long LastRegDown, LastRegUp, LastRegUpCut, LastConsBack, LastBkSkipGold, LastLordPaid, LastLordBase, LastCrownPaid, LastDue, LastReserve, LastSpare, LastTrim;
internal static int  LastRegDownN, LastRegUpN, LastConsN, LastConsHit, LastBkSkipTowns, LastBkSkipCastles, LastBuyAsk, LastBuyZero, LastPayers, LastKingdoms,
                     LastBelow, LastSiege, LastNoLord, LastTiny, LastExempt, LastNoCrown, LastUnpaid, LastPlayerPaid, LastStumbles;
internal static readonly Dictionary<Kingdom, long> CrownToday;   // 165 (jak CastlePurse.CrownToday :120); czyszczony na poczatku ValveDay
// Jeff 16:10 (rozdz. 7.3): zaulki i udzial zysku kupcow - pamiec sesji (bez zapisu; po wczytaniu serie i pierscien od zera - opis w linii)
static readonly Dictionary<Town, int> _streak;                  // doby z rzedu ponizej zapasu
static readonly Dictionary<Town, long> _goldAtBlock, _takeWin;  // migawka kasy na poczatku BeforeRents; pobrane w oknie blok-blok (NoteTake + zawor)
static long _trimWin; static readonly long[] _ringTake = new long[28], _ringNet = new long[28]; static int _ringHead, _ringFilled;
internal static long LastTake, LastNet, LastTake28, LastNet28; internal static int LastBelowN, LastBelow1000, LastAtZero, LastStreakMax, LastStreak7, LastStreak28; internal static string LastStreakName;
```
Dlaczego zamkniecie licznikow w `BeforeRents`: ticki miast gry ida przez cala dobe poza naszym blokiem; `BeforeRents` biegnie zawsze (takze przy
`PopulationRentEnabled` off, gdy `PopulationLaw.Daily` wraca przed zaworem :1157), wiec okno "doba" jest to samo co w ksiedze pieniadza.

### 2.2 Zapis
- Klucz `arm_townpurse` (`SaveText.Sync`), format jak `CastlePurse.Export/Import` (K:CastlePurse.cs:145-159): `"done"` | `"on:<wiek>"` | `""`.
- Wlasny try PRZED glownym blokiem `SyncData` (za arm_castlepurse, K:ArmouryBehavior.cs:418): zgubiona flaga = drugie przyciecie prawdziwego doplywu.
- Stary zapis bez klucza: przyciecie liczy wiek kampanii (0.75^wiek - po 40 dobach nic). Starszy DLL ignoruje klucz.
- `Reset()` wolany z `CastlePurse.Reset()` (K:CastlePurse.cs:123), **nie** z konstruktora `ArmouryBehavior` (K:ArmouryBehavior.cs:395 to jedna linia,
  ktora zmienia kazda galaz E4/E5 - pewny konflikt scalenia; CastlePurse.Reset jest wolany z tej linii, stan kampanii czysci sie tak samo).

---

## 3. Okna latek (dowod w dekompilacji, kto wola, kolejnosc)

### 3.1 Tick miasta gry - kolejnosc (G:TaleWorlds.CampaignSystem.CampaignBehaviors\ItemConsumptionBehavior.cs)
`DailyTickTownEvent` (:19) -> `DailyTickTown` (:47) -> `MakeConsumptionInTown` (:61-71): `DeleteOverproducedItems` (:79) -> `UpdateSupplyAndDemand` ->
`UpdateDemandShift` (:178) -> `MakeConsumption` (:142, private static) -> `GetFoodFromMarket` (:98 - tylko polka, bez zlota) -> `UpdateSellLog` -> `UpdateTownGold`
(:73-77: `GetTownGoldChange` i `ChangeGold`). Przy tworzeniu swiata `OnNewGameCreatedFollowUp` (:22-28) dwa razy `MakeConsumptionAllTowns` -
`Campaign.GameStarted` jeszcze false (G:...\Campaign.cs:126, ustawiane :894-896 w pierwszym `RealTick`, :1357 przy wczytaniu).
BK: prefiks `ItemConsumptionPatch.Prefix` (BK:BannerKings.Patches\EconomyPatches.cs:505) dla osady z PopulationData robi cala konsumpcje i `return false`:
`town.ChangeGold(num6)` (:555) - jedyny zapis zlota w nawiasie; drugi prefiks na `DeleteOverproducedItems` `return false` (:606-609). Zamki BK - `TickCastle`
wola `MakeConsumptionInTown` refleksja (BK:BannerKings.Behaviours\BKSettlementBehavior.cs:620-631) - ta sama droga (110 juz to obsluguje).
Skan DLL modow w `Modules\*\bin\Win64_Shipping_Client` (nazwy metod w UTF-8 i UTF-16, 11.10): `GetTownGoldChange`/`HandleMarketGold`/`DeleteOverproducedItems`/
`MakeConsumption` maja tylko Armoury, BannerKings i BetterEconomy (BEE tylko model `BEE_SettlementEconomyModel.GetTownGoldChange` - deleguje do vanilli,
BEE:BetterEconomy.Models\BEE_SettlementEconomyModel.cs:43-45, nieczynny - L:537 "kasa osad DefaultSettlementEconomyModel"). ROT, RBL, NavalDLC, DTE,
Diplomacy, BKROTPatch - nic.

### 3.2 `DeleteOverproducedItems(Town)` - migawka przed
| Latka | Priorytet | Co |
|---|---|---|
| BK prefiks | - | `return false` |
| ArmsLeaks (174.0) prefiks | - | kasuje 5% stosow bez uzbrojenia - roster, nie zloto (K:SubModuleMain.cs:86) |
| `CastlePurse.ShelfPostfix` | Normal (K:CastlePurse.cs:298) | **A1:** pierwsza linia w try: `if (__0 != null && __0.IsTown) TownPurse.OnShelf(__0);` (przed galezia zamku `if (... !__0.IsCastle ...) return`) |
| `MoneyLedger.ShelfPostfix` | Normal | migawka ksiegi (bez zmian) |
`TownPurse.OnShelf`: `_consTown = null; if (!On || town == null || Campaign.Current == null) return; _consTown = town; _consGold = town.Gold;` (wlasny try).

### 3.3 `MakeConsumption(Town, ...)` - cofniecie "zakupow" (R1)
| Latka | Priorytet | Co |
|---|---|---|
| BK prefiks bool | - | `ChangeGold(num6)` za kazda pozycje (:555) |
| `CastlePurse.ConsumePostfix` | **First** (K:CastlePurse.cs:299) | **A1:** pierwsza linia w try: `if (__0 != null && __0.IsTown) TownPurse.OnConsumed(__0);` |
| `MoneyLedger.ConsumePostfix` | Normal | widzi kase po nas - "zakupy" kas miast = 0 |
`TownPurse.OnConsumed`: `t = _consTown; _consTown = null; if (t != town) return; _regDue = town; bool live = Campaign.Current.GameStarted; if (live) _dConsN++;`
`if (!NoMint) return; int made = town.Gold - _consGold; if (made <= 0) return; town.ChangeGold(-made); if (live) { _dConsBack += made; _dConsHit++; }`.
Postfiksy Harmony biegna zawsze (takze po prefiksie BK z `return false`) - zasada 110 (K:CastlePurse.cs:29-32). Prefiksow nie zakladamy.

### 3.4 `GetTownGoldChange(Town)` kazdej deklaracji modelu - regulator (R2)
| Latka | Priorytet | Co |
|---|---|---|
| `CastlePurse.RegulatorPostfix` | First, wszystkie deklaracje (K:CastlePurse.cs:308-324) | **A1:** na poczatku (po `if (__0 == null) return;`): `if (__0.IsTown) { TownPurse.OnRegulator(__0, ref __result); return; }`; w galezi zamku (:280) obok `if (wanted < 0) __result = 0;` dopisac `else if (TownPurse.NoTopUp) __result = 0;` + licznik `_dRegUpCut` (zamiast `_dRegUp`) - tryb 2 dla zamkow (Jeff 16:10) |
| `SoldierPay.RegulatorPostfix` | First, leniwie w kampanii (K:SoldierPay.cs:664-690) | **A1:** `if (TownPurse.On) return;` na poczatku (tarcze martwe) |
| `MoneyLedger.RegulatorPostfix` | Normal | widzi wynik po nas (`regIn` = dosypka, `regOut` = 0); `Measure169c.NoteRegulator` przez ksiege |
`TownPurse.OnRegulator(town, ref result)` (wzor stara 111 + CastlePurse :272-286): `mode = Mode; if (mode == 0 || Campaign.Current == null) return;`
`due = ReferenceEquals(town, _regDue); if (result == 0) { if (due) _regDue = null; return; }`
`creating = !GameStarted; wanted = result; if (wanted < 0) result = 0; else if (mode == 2 || creating) { cut = wanted; result = 0; }`
`else { room = max(0, Reserve(town) - town.Gold); if (wanted > room) { cut = wanted - room; result = room; } }`
`if (!due) return; _regDue = null; if (creating) return;` liczniki: `wanted < 0` -> `_dRegDown -= wanted, _dRegDownN++`; `result > 0` -> `_dRegUp, _dRegUpN`;
`cut > 0` -> `_dRegUpCut, _dRegUpCutN`. Pytania z ekranow i drugi poziom modelu dostaja ten sam wynik, do licznikow nie wchodza (regula 110).
Domyslnie (tryb 2) kazdy dodatni wynik idzie do `cut` ("dosypka zablokowana") - zrodlo z niczego kas miast i zamkow = 0. W trybie 1 przy modelu gry
(cel = 10 000 + 12 x dobrobyt, G:...\DefaultSettlementEconomyModel.cs:75-79) dosypka 0.25 x (cel - kasa) nigdy nie przekracza `room` (Reserve >= cel) -
ciecie `room` dziala tylko przy innym modelu (BK `GetMerchantIncome` - zawsze dosypuje). Kolejnosc dwoch First (CastlePurse wpiety w menu glownym,
SoldierPay leniwie w kampanii) bez znaczenia - SoldierPay przy A1 nic nie robi. Zamek (galaz CastlePurse): `TownPurse.NoTopUp` = `On && Mode == 2`;
`CastlePurse.Daily` (:662) drukuje "dosypka zablokowana (tryb 2, E5/A1) X" zamiast "dosypka do zapasu (tryb 1, zostaje do etapu 3)" gdy `NoTopUp`.

### 3.5 BK `BKSettlementBehavior.HandleMarketGold(Settlement)` - R6 (jedyna nowa latka)
- Cel: `private void HandleMarketGold(Settlement)` (BK:BannerKings.Behaviours\BKSettlementBehavior.cs:463-490), wolany z lambdy `RunStep("HandleMarketGold", ...)`
  (:241-243) w `DailySettlementTickImpl` (:206; zdarzenie `DailyTickSettlementEvent` :57) - dla wsi, zamkow i miast. Cialo: kasa < 0.2 x limit -> notabl
  z >= 30 000 daje 1 000 (przelew); kasa > limit -> `ChangeGold(-1%)`, potem przez blad znaku kasa +0.5% i notabl -0.5% (razem 1% swiata w nicosc).
  Limit: BK:BannerKings.Models.Vanilla\BKEconomyModel.cs:610-624 (miasto 50 000 + 25 x dobrobyt, zamek 50 000 + 12 x, wies 10 000 + 3 x hearth).
- Dzis na celu: okno O18 `MarketPre` (prefiks First) + `MarketFin` (finalizer) (K:CirculationWindows.cs:814-840, wpiecie :1789 przez `Wire` :1686-1689).
- **A1:** `public static bool BkSkimPrefix(Settlement __0)` **Priority.Last** (po prefiksie okna - okno mierzy przed nami, finalizer okna widzi 0 zmiany):
  `if (!On || !TownPurseNoBkSkim || __0?.Town == null) return true; castle = __0.IsCastle; if (castle && !CastlePurseEnabled) return true;`
  `if (__0.SettlementComponent.Gold <= BkLimit(__0)) return true;` liczniki (miasta/zamki, ok. 1% kasy) `return false;`. Wyjatek -> `true` + potkniecie.
  `BkLimit` jak stara 111 (refleksja `BannerKingsConfig.Instance.EconomyModel.GetSettlementMarketGoldLimit`, przy braku - stale z BK). Wies - `true`.
- Patch: `AccessTools.Method(TypeByName("BannerKings.Behaviours.BKSettlementBehavior"), "HandleMarketGold", new[] { typeof(Settlement) })`; brak = "BRAK" w linii
  startowej. Dzialanie latki na tym celu jest juz dowiedzione: okno O18 (latka na tej samej metodzie) mierzy "rynek osady -34 610" (L:31718).

### 3.6 Akcje gry uzywane przez zawor
`GiveGoldAction.ApplyForSettlementToCharacter(st, lord, x, true)` - jak dzis renty (K:PopulationLaw.cs:1201) i zawor zamku (K:CastlePurse.cs:589); prefiks
`RealPayer.GivePrefix` (Low, K:RealPayer.cs:74, :283) dziala tylko z otwartym kontekstem i dla zdarzen "z niczego" - zawor (dajacy = osada) go nie dotyczy.
Korona: `ChangeGold` + `KingdomBudgetWallet` bez zdarzenia gry (jak danina K:KingdomTreasury.cs:399 i 114).

---

## 4. Rachunek i funkcje `TownPurse` (dokladnie)

| Funkcja | Wynik |
|---|---|
| `int Mode` | `TownPurseRegulator` obciety do 0..2 (domyslnie 2) |
| `bool On` | `Mode > 0 && CastlePurse.HookedModels > 0 && CastlePurse.HookedConsumption` - dwa nowe pola ustawiane w `CastlePurse.ApplyAll` (:300, :320) |
| `bool NoTopUp` | `On && Mode == 2` - regulator bez dosypki dla miast i zamkow |
| `bool NoMint` | `On && TownFolkBuyWithoutMinting` |
| `bool ValveLive` | `On && PopulationLaw.On && Unit(TownRentShare) > 0` |
| `int Reserve(Town)` | max(`TownPurseFloorGold` + `TownPurseFloorPerProsperity` x p, 10 000 + 12 x p), p = dobrobyt (NaN/ujemny = 0) |
| `long Spare(Town)` | max(0, Gold - Reserve) |
| `int TaxFloor(Town t, int legacy)` | `t.IsTown && On` ? Reserve(t) : legacy |
| `int BuyFloor(Town t, int legacy)` | `t.IsTown && On && TownPaysFromSurplus` ? Reserve(t) : legacy (licznik `_dBuyAsk`, `_dBuyZero` gdy Gold <= Reserve) |
| `int SpendableTown(Town t)` | max(0, Gold - BuyFloor(t, 0)) |
| `float LordShare(Kingdom k)` | k zywe ? Unit(`TownRentLordShare`) : 1 |
| `bool Valve(Settlement st, float decree, out int lordPay, out int lordBase, out int crown, out long spare)` | jedno miasto (rozdz. 0 pkt 9); wlasny try, potkniecie - false |
| `void ValveDay(long crownSum, ...)` | konczy dobe zaworu: `MoneyLedger.SplitTownMark(MRent, MTownCrown, crownSum)`, Last* |
| `void BeforeRents()` | migawka kas miast i zamkniecie okna udzialu zysku (7.3); zamyka liczniki doby -> Last*; `TrimStartGift` (przy `On`); R7 raz na sesje |
| `void NoteTake(Settlement st, int x)` | licznik pobran z kasy miasta (7.3) - tylko liczba |
| `string LineSegment()` | dopisek do "Kasy miast (169c)" (rozdz. 7.2) |

Zmiany w istniejacych funkcjach (K:):
- `CastlePurse.Spendable` :383 `if (!town.IsCastle) return gold;` -> `if (!town.IsCastle) return TownPurse.SpendableTown(town);`
- `CastlePurse.PayFloor` :405 `if (town == null || !town.IsCastle) return townFloor;` -> `... return TownPurse.TaxFloor(town, townFloor);`
- `Seal31.Spare` :108 -> `if (st.IsTown) free = TownPurse.On ? TownPurse.Spare(t) : (long)t.Gold - TownReserve(t) - SoldierPay.HeldOf(st) - SoldierPay.CourtOf(st);`
- `KingdomTreasury.Levies` :391 `gold - (int)floor` -> `gold - TownPurse.TaxFloor(st.Town, (int)floor)` (danina, clo przy dwoch poborach, mennica - ten sam `spare`).
- `KingdomTreasury.RulingIncomePostfix` :528 (Road Tolls) `st.Town.Gold - (int)floorG` -> `st.Town.Gold - TownPurse.TaxFloor(st.Town, (int)floorG)`.
- `KingdomTreasury.WageRefund` :232 -> `float cutShare = Unit(CrownRefundOwnTownsCut) * (TownPurse.ValveLive ? TownPurse.LordShare(null-safe krolestwo rodu) : 1f);`
  (krolestwo `c.Kingdom` jest znane w petli :244 - liczyc `cutShare` w petli; :253 uzywa tego samego `cutShare`).
- Zakupy z rozdz. 0 pkt 7: `OutlawLaw.cs:1936` `b.Town.Gold - reserve` -> `b.Town.Gold - TownPurse.BuyFloor(b.Town, reserve)`; `MaterialOrders.cs:800`,
  `CaravanAmmo.cs:297`, `CaravanBulk.cs:589` `town.Gold - reserve` -> `town.Gold - TownPurse.BuyFloor(town, reserve)`; `CaravanBulk.cs:383` `t.Gold > reserve` ->
  `t.Gold > TownPurse.BuyFloor(t, reserve)` i :579 tak samo; `GarrisonArmory.cs:336` `market.Town.Gold < unit` -> `market.Town.Gold - TownPurse.BuyFloor(market.Town, 0) < unit`;
  `MenPurse.cs:363` `st.Town.Gold < unit` -> `st.Town.Gold - TownPurse.BuyFloor(st.Town, 0) < unit`; `Drill.cs:938` `Math.Max(0, st.Town.Gold)` ->
  `Math.Max(0, st.Town.Gold - TownPurse.BuyFloor(st.Town, 0))`; `TravellerTrade36b.cs:454` `town.Gold - itemPaid < p` -> `town.Gold - TownPurse.BuyFloor(town, 0) - itemPaid < p`.
  Zmienne `reserve` (20 000) zostaja jako "stary prog" (tryb 0 i zamki).

---

## 5. Zawor w dobie (R4) - `PopulationLaw.Daily` + `TownPurse.Valve`

Kolejnosc bloku (K:ArmouryBehavior.cs:1459-1468): `Seal31.BlockOpen` -> `MoneyLedger.BlockOpen` (:1460; `ClearLast169` zeruje `TownPurse.Last*`) ->
**`TownPurse.BeforeRents`** (nowa linia; po przycieciu `MoneyLedger.Mark(MTownTrim)` w srodku) -> `PopulationLaw.Daily` (:1461; E4/4 stan miast, E4/8
`TownGrowth.Daily`, renty wsi, **zawor miast `ValveTowns`**, linia "Ludnosc:") -> `Seal31B.OwnerTaxDaily` -> `RentMerge` -> `Mark(MRent)` (:1464; czesc pana
w "renty", korona przeniesiona do M13 przez `SplitTownMark`) -> `CastlePurse.Daily` (:1467) -> ... -> `KingdomTreasury.Levies` (danina od Reserve) ->
`CrownIncome.Begin` (czyta `TownPurse.CrownToday`) -> ... -> `WageRefund` (K3) -> `CrownRents` -> ... -> `Measure169c.Daily` ("Kasy miast (169c)" z dopiskiem A1)
-> `MoneyLedger.Daily`. Zawor jest po stanie miast E4/4/E4/8 tej doby i przed powinnosciami, budowami i rada (czytaja `RentToday`/`RentOf`).

`PopulationLaw.Daily` (K:PopulationLaw.cs:1152-1219):
1. w petli, obok haka wsi :1182: `if (st.IsTown && TownPurse.ValveLive) continue;` (po `pop/due` - "nalezne" bez zmian).
2. po petli, przed :1216: `if (TownPurse.ValveLive) ValveTowns(pop, paid, ref totalPaid);` - nowa metoda prywatna (ok. 30 linii):
   dla kazdego `st` w `Settlement.All` z `st.IsTown && st.Town != null`: `float decree = TaxDecree(st)`; `TownPurse.Valve(st, decree, out lp, out lb, out cr, out spare)`;
   `_rentBy[st] = { lp, spare }` (takze przy lp = 0 i spare > 0 - "wlasne" 169c i rada licza z tego); `lp > 0` -> `RentToday[clan] += lp`, `DayTownRent += lp`,
   `totalPaid += lp`, `paid[c] += lp` gdy kultura w `pop`; renta "nalezna" (`DailyRent(PeopleOf(st))`, kultura w tabeli) -> `TownPurse` do linii (dawny pulap -
   tylko liczba); na koncu `TownPurse.ValveDay(sumaKorony)`. Kazde miasto we wlasnym try w `Valve` (licznik potkniec) - nie `catch { }` jak petla :1209.
3. przy `ValveLive` false (A1 off albo `PopulationRentEnabled` off) - kod jak dzis.
`TownPurse.Valve`: `town = st.Town; reserve = Reserve(town); spare = Gold - reserve;` lord = `st.OwnerClan?.Leader`; kolejno: `spare <= 0` -> Below;
lord null/martwy -> NoLord (nic nie schodzi); `st.IsUnderSiege` -> Siege; `pay = CastlePurse.Dues(Gold, reserve, TownRentShare)`, `pay <= 0` -> Tiny;
`k = clan.Kingdom; crownTakes = k != null && !k.IsEliminated; ValveSplit.Split(pay, crownTakes ? LordShare(k) : 1f, out lordBase, out crown)`;
`lordPay = min(lordBase x decree, spare - crown)` (decree NaN/<0 = 0); `lordBase > 0 && lordPay == 0` -> Exempt; przelew pana; korona: `had = Gold;
ChangeGold(-crown); taken = had - Gold; k.KingdomBudgetWallet += taken; CrownToday[k] += taken`; `lordPay + taken == 0 && !Exempt` przy `spare x share >= 1`
-> **Unpaid** (prog "10 miast bez placenia" = 0). Gracz - ta sama regula (pan = `Hero.MainHero`, licznik osobno).

---

## 6. Dar startowy (R5) i tarcze z zapisu (R7) - `TownPurse.BeforeRents`

1. Zamkniecie licznikow doby -> `Last*`, zerowanie `_d*` (zawsze, takze przy A1 off - wtedy zera).
2. `if (!On) return;`
3. `TrimStartGift(s)` - kopia logiki K:CastlePurse.cs:466-537 dla `st.IsTown`: wiek kampanii (`CampaignTime.Now - CampaignStartTime`), NaN -> `_trimSkip` + linia;
   `TownPurseTrimAtStart` off -> zapis `_liveAge` ("on:<wiek>") i linia; wiek < -0.01 -> flaga bez ciecia; inaczej `left` = 0.75^wiek (wiek <= 1.5 -> 1) x
   (1 - TownRentShare)^(dni zaworu bez przyciecia); `cut = CastlePurse.StartGiftCut(gold, Reserve, p, left)`; `ChangeGold(-cut)`; flaga `_trimDone` PRZED petla;
   po petli `MoneyLedger.Mark(MoneyLedger.MTownTrim)` (od `BlockOpen` nic innego nie ruszalo kas - migawka = dokladnie dar). `LastTrim` = suma.
4. R7 raz na sesje: `n = SoldierPay.DropShields(out heldSum, out courtSum)` (nowa mala funkcja w SoldierPay: czysci `_held` i `_court`, zwraca liczby) - linia
   jednorazowa, gdy cos bylo (stary zapis). Zlota nie rusza (znaczniki to liczby).

Oczekiwane [S]: nowa kampania d1 - kasy miast ok. 20.5 mln (L:505: 17 397 059 po pierwszym kasowaniu regulatora 3 620 315) wobec zapasu 6 548 911 ->
przyciete ok. 14 mln (to samo zloto regulator kasowal dotad w ok. 12 dob; zostawione splyneloby zaworem do panow i koron).

---

## 7. Klucze i linie logu

### 7.1 Klucze (`Settings.cs`, nowa grupa `// --- The town's purse ---` po `CastlePaysFromSurplus` K:Settings.cs:836, przed grupa korony :838;
potem `python tools/gen_mcm.py`; RANGES: `TownPurseRegulator` (0, 2, "0"), `TownRentLordShare` (0.0, 1.0, "0.00"); `Armoury.json` Jeffa (9 875 B, 08.10)
sprawdzone 11.10 grep - zadnego z kluczy ponizej NIE MA, domyslne zadzialaja)
| Klucz | Typ / domyslna | Podpowiedz MCM (EN) |
|---|---|---|
| `TownPurseRegulator` | int **2** (0..2; Jeff 16:10) | 0 = the game's daily town-purse regulator as before; 1 = it never deletes coin from a town or castle purse and tops it up only up to the merchants' working coin, counted in the log; 2 = it does nothing at all - no coin is deleted and none comes from nowhere, in towns and castles alike (castles with Castle Purse Enabled). With 1 or 2 a town purse is real coin: the goods the townsfolk use up no longer pay gold into it from nowhere, its lord and the crown draw the dues from what lies above the working coin, and the wage and court shields are no longer needed. Use 1 if town purses run dry |
| `TownPurseFloorGold` | int 10000 | working coin the merchants of a town keep before any dues or taxes are drawn and below which the town buys nothing for us - the fixed part (the game's own target is 10 000 plus 12 a point of prosperity); never below that target, so these settings can only raise it |
| `TownPurseFloorPerProsperity` | float 12 | ... and the part of the working coin that grows with the town's prosperity, in coins a point |
| `TownFolkBuyWithoutMinting` | bool true | the goods the townsfolk use up still leave the market, but no longer pay their price into the town purse from nowhere (off with the regulator on: that gold stays and the dues hand it to the lords - for tests only) |
| `TownRentLordShare` | float 0.67 | of what a town purse gives up each day its lord keeps this share and the kingdom treasury the rest - you and the AI alike (a town held outside any kingdom pays everything to its lord) |
| `TownPurseTrimAtStart` | bool true | once per campaign, on its first day: the starting gift in every town purse (20 000 plus Banner Kings' 40 a point of prosperity) is cut down to the working coin - the game's regulator deleted that gold within two weeks anyway; an old save loses nothing |
| `TownPurseNoBkSkim` | bool true | Banner Kings no longer deletes 1% a day from a town purse above its limit (and from a castle purse, with Castle Purse Enabled); a notable still helps an empty purse with 1 000 as before |
| `TownPaysFromSurplus` | bool true | a town purse buys for us only with the coin it holds above the working coin: loot from outlaws, contract and bulk goods and arrows from caravans, spare kit of garrisons, lords and your men, drill scrap, the travellers' goods - as castles do. Below the working coin the game tops a purse up from nowhere, so whatever was bought from it was in truth paid from nowhere. Needs Town Purse Regulator. Off = the whole purse, as before |
Nowe opisy istniejacych (komentarz w Settings.cs - P i K3):
- `TownRentShare` (:642): "share of the town purse above the merchants' working coin that leaves it each day as rents, tolls and farms; with Town Purse Regulator on, the lord keeps Town Rent Lord Share of it and the crown the rest".
- `TownRentFloorGold` (:643): "with Town Purse Regulator 0 only: floor of a town purse for rents and taxes, as before (below 20 000 Banner Kings takes prosperity away)".
- `TownWageShield` (:824) i `HouseholdShield` (:880): dopisek "Not needed and idle with Town Purse Regulator on - nothing deletes town coin any more".
- `CrownRefundOwnTownsCut` (:843): dopisek "with the town purse valve on, the cut follows the lord's share of the valve".
- `CastleDuesSplitWithCrown` (:831): zdanie "Towns do not share their rents with the crown yet - ..." -> "Towns share theirs by Town Rent Lord Share (town purse)".
Bez klucza `CrownRefundSkipsHomeGarrisons` (K6). `TownFolkMode`/`TownFolkLive` to E5/0/B1 - nie tu.

### 7.2 Linie (napisy logu po polsku jak caly pomiar; komunikatow w grze A1 nie ma)
- **Start** (`TownPurse.ApplyAll` w menu glownym; stan klucza tylko informacyjnie - MCM moze go zmienic; stan czynny podaje linia dzienna):
  `TownPurse (E5/A1): kasy miast jako prawdziwy pieniadz - klucz Town Purse Regulator N (przy starcie) | latki: regulator i "zakupy" przez CastlePurse (110) w M modelach, konsumpcja: tak|BRAK, BK HandleMarketGold - prefiks Last: wpiete|BRAK; BRAK: - | zapas kupcow X + Y x dobrobyt (nie mniej niz cel gry 10000 + 12 x dobrobyt), zawor Z% nadwyzki, panu L% i reszta skarbcowi krolestwa, dar startowy przycinany: tak|nie, zakupy dla nas tylko z nadwyzki: tak|nie, BK rynek pominiety: tak|nie (zamki tylko przy Castle Purse Enabled).`
- **Raz (dar startowy)**, wzor K:CastlePurse.cs:528-536: `TownPurse (E5/A1): poczatek kampanii (doba 1.00) - dar startowy w kasach miast (gra 20000 + BK 40 x dobrobyt) przyciety do zapasu kupcow: miast 97, kasy A -> B (zdjeto C w n miastach, najwiecej M - Nazwa; zapas razem R). ... Flaga zapisze sie w sejwie (arm_townpurse).` (+ warianty: stary zapis, wylaczone, wiek ujemny, wieku brak).
- **Raz (tarcze)**: `TownPurse (E5/A1): tarcze zoldu i dworu porzucone - znaczniki zoldu S1 w n1 miastach, dworu S2 w n2 (liczby, zlota nie rusza; nic juz nie kasuje kas miast).`
- **Dziennie - dopisek w "Kasy miast (169c)"** zamiast ` | dwor -, do korony - (162m, 111')` (K:Measure169c.cs:373; `TownPurse.LineSegment()`):
  A1 czynne: ` | E5/A1: tryb regulatora 2 | zawor: zeszlo D = panom L (przed dekretem LB; z nP miast; rod gracza G) + koronie K (nK krolestw); renta nalezna od ludzi (dawny pulap, tylko liczba) R; bez poboru: kasa nie ponad zapasem a, oblezone b, bez zywego pana c, nadwyzka za mala d, zwolnione dekretem (tylko korona) e; bez krolestwa (calosc panu) f; miast z nadwyzka bez zaworu (10 miast) u | regulator: kasowanie zablokowane X (xn tickow), dosypka do zapasu (tryb 1) Y (yn miast), dosypka zablokowana (tryb 2 albo ponad zapas) Z (zn tickow) | "zakupy" mieszczan: zloto z niczego cofniete C (w h z t tickow; towar zjedzony jak dotad) | zapas kupcow Rs, ponad zapasem Sp | zaulki: kas ponizej zapasu nb, ponizej 1000 n1, przy 0 n0; najdluzej ponizej zapasu dd dob z rzedu (Nazwa), miast >= 7 dob w7, >= 28 dob w28; towar wsi niesprzedany w miastach (brak zlota w kasie) us szt. w uv wizytach | udzial zysku kupcow zabierany przez pana i korone: dzis U% (pobrano Pb z doplywu netto Dn), srednia 28 dob U28% (odniesienie historyczne ok. 10%, INFO) | zakupy dla nas z nadwyzki: zapytan q, przy zapasie (0) z | BK rynek osady: gorna galaz pominieta w bt miastach i bc zamkach (BK skasowalby ok. Q) | dar startowy dzis T | potkniecia P`.
  Zawor nieczynny przy rentach wylaczonych: `zawor: NIE DZIALA (Population Rent Enabled wylaczone) - kasy miast tylko rosna`. A1 wylaczone:
  ` | E5/A1: WYLACZONE (Town Purse Regulator 0) - regulator gry, "zakupy" z niczego, renta z pulapem i tarcze jak dotad`.
  `kasy-miast.csv` (:337): kolumny NA KONCU `zapas_kupcow;do_korony;pobrane;doplyw_netto;dni_ponizej_zapasu` (stare kolumny bez zmian - nikt ich nie parsuje, grep tools/).
  Linia biegnie tylko przy `Measure169c.On` (LogEnabled + ClanIncomeBook*, K:Measure169c.cs:32; domyslnie tak) - inaczej progi A1 = "brak linii" (INFO).
- **Ksiega** (bez nowych linii): "Przeplywy osad (kasy miast)" - "zakupy" +0, regulator "(dosypal Y, skasowal 0)", w "Armoury tick dobowy" pozycje
  M12/M13; "Pieniadz swiata (bilans)" (K:MoneyLedger.cs:801) i "(bilans - przyczyny)" (K:MoneyLedger.Obieg.cs:342): `, dar startowy kas miast przyciety (E5/A1) T`;
  "Obieg" (:569, :572): `do korony K` zamiast `do korony -`, `(tryb N)` zamiast `(tryb 1 -)`; "Korona: wplywy dnia (165)" (K:CrownIncome.cs:320):
  "1/3 zaworu zamkow" -> "1/3 zaworu zamkow i miast".
- `SoldierPay.CourtNote` (:618) i linia "Zold:" - przy A1 dopisek "tarcze zbedne (E5/A1)" zamiast liczb znacznikow.

### 7.3 Zaulki i udzial zysku kupcow (Jeff 16:10) - definicje do dopisku
- **Zaulki** (w `TownPurse.Valve`, raz na miasto i dobe): `Gold < Reserve` -> `_streak[town]++`, inaczej 0 (slownik sesji - po wczytaniu od 0, opis w linii);
  liczniki: ponizej zapasu, ponizej 1 000, przy 0, najdluzsza seria (nazwa miasta), serii >= 7 i >= 28 dob.
- **Czego nie kupily:** towar wsi niesprzedany w miastach - licznik ksiegi `_vUnsold[CTown]` / `_vUnsoldVisits[CTown]` (K:MoneyLedger.cs:120; okno wizyty taboru
  BK `SellGoodsPatch` - kupuje tyle, ile ma kasa) przez nowy getter `MoneyLedger.TownUnsoldToday(out szt, out wizyt)` (czytany w `Measure169c.Daily` - przed
  `MoneyLedger.Daily`, ktore zeruje licznik; ta sama doba co "Obieg" "towar niesprzedany").
- **Udzial zysku kupcow** (P rozdz. 6 pkt 2; wzor E5/0 - przy scaleniu jedna funkcja, wlasciciel E5/0, A1 tylko wola): okno blok-blok (migawka kas miast na
  POCZATKU `BeforeRents`, przed przycieciem); `pobrane` = zawor panu + zawor koronie (TownPurse) + danina z kasy miasta + clo z kasy (gdy nie `CrownCustomsSingleTake`)
  + mennica + Crown Duty + War Tax + Road Tolls z kasy miasta; `doplyw netto` = (kasa teraz - kasa w poprzedniej migawce) + `pobrane` + przyciecie daru w oknie
  (dosypka i "zakupy" z niczego = 0 przy trybie 2; przy trybie 1 odjac dosypke); udzial = pobrane / doplyw netto (doplyw <= 0 -> "-"); srednia 28 dob = suma
  pobranych / suma doplywow z pierscienia 28 dob (nie srednia ilorazow). Swiat w dopisku, miasto po miescie w `kasy-miast.csv`.
  Liczniki pobran: `TownPurse.NoteTake(Settlement st, int x)` (tylko `st.IsTown`, x > 0) w K:KingdomTreasury.cs:399 (danina - obok `TownLevyToday`), :422 (clo
  z kasy), :431 (mennica), :485 (Crown Duty, przy `applyWithdrawals`), :513 (War Tax, jw.), :529 (Road Tolls, jw.); zawor - w `Valve`. Monopole (:441 - kapital
  warsztatow) i Land Tax (:498 - wsie) poza wzorem.

---

## 8. Ksiega pieniadza (MoneyLedger) - zmiany

### 8.1 Indeksy
- `MTownTrim = 12` - "dar startowy kas miast przyciety (raz na kampanie, w nicosc, E5/A1)"; `MTownCrown = 13` - "udzial korony z zaworu kas miast (kasy miast -> skarbce krolestw, E5/A1)".
- `Marks` 12 -> 14 (K:MoneyLedger.cs:94); `MName` (:95-99) MUSI miec 14 pozycji (petla :929 indeksuje `MName[k]` do `Marks` - krotsza tablica = wyjatek w linii kas).
- Notes (N 0-8), Holders (H 0-21), N169 (0-5), CW - A1 nie dodaje nic.

### 8.2 Funkcje
- NOWA `SplitTownMark(int from, int to, long gone)` obok `SplitCastleMark` (:514-522) - to samo dla `CTown`. Wolana w `TownPurse.ValveDay` (MRent -> MTownCrown).
- `TrimNow()` (:758) zostaje (zamki); NOWA `TownTrimNow()` (suma `-_mark[c, MTownTrim]`); `OldBalance` (:774) `u0 += TownTrimNow()`; tekst :801.
- Obieg :275 `long townTrim = TownTrimNow();` -> `rb` (:276) `+ trim + townTrim`; w petli odcinkow (:284-299) `townTrim` do PIERWSZEGO liczonego odcinka
  (przyciecie stoi przed `Mark(MRent)` - odcinek "renty"; `trim` zamkow zostaje przy `i >= 1`); tekst :342.
- `ClearLast169` (Obieg :86-101): `try { TownPurse.ZeroLast(); } catch ...`.

### 8.3 Co ksiega zobaczy (oczekiwane)
"zakupy" kas miast 0 (dzis 425 318 d120, L:31722); regulator kas miast skasowal 0 (dzis 248 065), dosypal 0 w trybie 2 (dzis 20 220; zamki tez 0); "BK rynek osady" w ujsciach
ok. 0 (dzis -34 610, L:31718 - z miast i zamkow); ujscie jednorazowe "dar startowy kas miast" w d1; posiadacze bez zmian (zawor = przelew kasy -> glowa / skarbiec).

### 8.4 Rezerwacja indeksow (wpis w CHANGELOG drzewa A1)
A1: M12-M13 zajete, M14-M16 rezerwa A1. **Drzewo A2-A5: od M17** (Notes od N14, Holders od H27 - pierwszy wolny + 5); w swoim drzewie zaslepki
`MName[12..16]` = "(zarezerwowane E5/A1)" i `Marks` = ostatni + 1; przy scaleniu zaslepki 12-13 zastapic nazwami A1. E5/0 (drzewo `e5\0`) wg swojego
projektu nie dodaje pozycji MoneyLedger; gdyby musial - poza 12-21 (do uzgodnienia w jego planie).

---

## 9. Test (zbior A) i progi `--grupa etap5`

Zbior A (P rozdz. 4, decyzja Jeffa 11.10 03:05; wariant (a) - osobny test zaraz po tescie koncowym E4): autotest nowa kampania 40 dob + zapis z doby 362
(9 dob) + 120 dob, A1-A5 razem; progi A1 ponizej + progi etapow 2-4 (glowy < 5 000 najwyzej 10; 0 bankructw; wojsko lordow w wojnie 95-115 tys., podloga 85;
reszta ksiegi < 10 tys./d; etap 4). Nie przechodzi -> wylaczamy kawalek w `Armoury.json` autotestu w kolejnosci: `TownPaysFromSurplus`, `TownPurseNoBkSkim`,
`TownPurseRegulator` 1 (zaulki / plon - opcja z decyzji Jeffa 16:10, raport dla Jeffa), `TownPurseRegulator` 0 (cale A1) - i powtarzamy (jedna zmiana naraz).
Wojsko ponizej 95 tys. - naprawa przeplywem, nie wojskiem (P rozdz. 4).

### 9.1 Blok parsera `etap5_a1(rep, lines, start, win)` w `tools/sprawdz_logi.py`
Grupe `--grupa etap5` (`etap5_main`, dyspozytor w `main` K:..\tools\sprawdz_logi.py:1668-1671) zaklada E5/0. A1 dopisuje TYLKO funkcje bloku (rusza, gdy jest
linia startowa `TownPurse (E5/A1):`); w drzewie A1, dopoki E5/0 nie jest scalone, minimalny `etap5_main` (czytanie logu, start kampanii jak `etap4_main`
:530-560, `Report`, wolanie `etap5_a1`) - przy scaleniu zostaje wersja E5/0 + jedna linia wolania. Stan A1 - z dopisku dziennego, nie z linii startowej.
Okno: doby kampanii 2-120 (doba 1 = przyciecie i pierwsze ticki); "sr28" = ostatnie 28 dob.
| # | Prog | Jak (linia, regex) | Regula | Baza |
|---|---|---|---|---|
| A1-T1 | bledy i potkniecia | `ERROR in TownPurse` = 0; `potkniecia (\d+)` w dopisku 169c | 0 bledow; potkniecia <= 5 w biegu | standard |
| A1-T2 | linia startowa | `TownPurse (E5/A1):` z `BRAK: -`, modele >= 1, konsumpcja `tak` | wszystko wpiete | - |
| A1-T3 | A1 czynne | dopisek `E5/A1: tryb regulatora (\d)` kazdej doby okna | 2 kazdej doby (Jeff 16:10; 1 = bieg awaryjny - progi T6/T7 wedlug trybu 1; 0 caly bieg = bieg porownawczy - progi INFO; zmiana w biegu = NIE) | - |
| A1-T4 | "zakupy" w kasach miast | "Przeplywy osad (kasy miast)": `"zakupy" mieszkancow ([+-]?\d+)` | 0 kazdej doby okna | dzis 425 318 (L:31722) |
| A1-T5 | kasowanie regulatora | tamze `regulator kasy [+-]?\d+ \[P\] \(dosypal (\d+), skasowal (\d+)` | skasowal = 0 kazdej doby | dzis 248 065 (L:31686) |
| A1-T6 | dosypka kas miast | jw. `dosypal` | tryb 2: 0 kazdej doby okna (tryb 1: sr28 <= 35 000/d = 2 x dzis, wiecej - DECYZJA) | sr28 17 057 (P rozdz. 1) |
| A1-T7 | dosypka kas zamkow | "Przeplywy osad (kasy zamkow)" `dosypal (\d+), skasowal` i "Zawor zamkow (110)" `dosypka zablokowana \(tryb 2` | tryb 2: dosypal 0 kazdej doby; linia 110 z "tryb 2" | dzis ok. 1 tys./d (P rozdz. 6) |
| A1-T8 | zawor: suma i podzial | dopisek `zeszlo (\d+) = panom (\d+) \(przed dekretem (\d+);.*\+ koronie (\d+)` | D = L + K kazdej doby; sr28 K / (LB + K) w 0.32-0.34 | 1/3 (klucz) |
| A1-T9 | "10 miast bez placenia" | dopisek `miast z nadwyzka bez zaworu \(10 miast\) (\d+)` | 0 kazdej doby | 10 (RB 5.4) |
| A1-T10 | tarcze | "Kasy miast (169c)" `pod tarcza zoldu .*? (\d+)` od doby 2; "Budzet rodow (166)" bez znacznika dworu | 0 | dzis 2 863 805 (L:31686) |
| A1-T11 | BK rynek osady | "Pieniadz swiata (bilans - przyczyny)" `rynek osady (-?\d+)` sr28 | \|x\| <= 2 000/d (zostaje dolna galaz - przelew) | dzis -34 610 (L:31718) |
| A1-T12 | dar startowy | linia `TownPurse (E5/A1): poczatek kampanii` - `zdjeto (\d+)`; tego dnia ujscie "dar startowy kas miast" w "(bilans - przyczyny)" = zdjeto | nowa kampania: 1 linia, zdjeto 8-20 mln, rowne ujsciu +-1; zapis z doby 362: przyciecie 0 | [S] ok. 14 mln |
| A1-T13 | reszta ksiegi | "(bilans - przyczyny)" `srednia 28 dob ([+-]?\d+)` | \|R\| < 10 000 (etap 3) | -1 302 (sr28) |
| A1-T14 | nasze zakupy z pasa ponizej zapasu | linia E5/0 "Nadwyzka kas (E5/0)" `nasze sciezki (\d+)`, gdy jest; inaczej dopisek `zapytan q, przy zapasie z` | 0 (z linii E5/0); bez E5/0 - INFO (z budowy: `BuyFloor`) | - |
| A1-T15 | stan ustalony kas | dopisek `ponad zapasem (\d+)` wobec sr28 zaworu / 0.07 | INFO (stosunek ok. 1 = stan ustalony; P111) | dzis kasy 12.56 mln wobec 7.22 mln celu |
| A1-T16 | korona i K3 | "Korona: wplywy dnia (165)" `nasze: ... (\d+)` wobec biegu bazowego; `LastOwnCut` (zwrot przyciety K3) | INFO (wplywy korony w wojnie >= bieg bazowy - prog B2, tu tylko odczyt) | 165: 256 287 / d (P) |
| A1-T17 | zawor razem | sr28 D | INFO 250-550 tys./d (rachunek P: ok. 398 tys.; przy trybie 2 ok. 381 = panom 254 + koronie 127) | renta dzis 487 312 |
| A1-T18 | rody | D staly / "Budzet rodow (166)" - glowy, bankruci, wojsko | progi etapu 2 (osobno `--grupa etap2`); P [S]: rody ok. -89 tys./d (-8.6% D stalego) bez A3 | - |
| A1-T19 | zakup plonu | "Przeplywy osad": towar niesprzedany po wizycie | ponizej 0.1% (kazdy zbior) | dzis 0 szt. |
| A1-T21 | zaulki (Jeff 16:10) | dopisek `zaulki: kas ponizej zapasu (\d+), ponizej 1000 (\d+), przy 0 (\d+); najdluzej ponizej zapasu (\d+) dob` i `towar wsi niesprzedany w miastach .*? (\d+) szt.` | kasy przy 0: 0 kazdej doby; towar wsi niesprzedany <= 0.1% sztuk sprzedanych w miastach (prog "zakup plonu >= 98%" z zapasem) i 0 dob z rzedu > 7 w tym samym miescie z towarem niesprzedanym; przekroczenie -> bieg z `TownPurseRegulator` 1 (opcja Jeffa) i raport | dzis: 15 z 97 ponizej celu (L:31686), towar niesprzedany 0 |
| A1-T22 | udzial zysku kupcow (P rozdz. 6 pkt 2) | dopisek `udzial zysku kupcow .*? dzis (-|[\d.]+)%.*?srednia 28 dob (-|[\d.]+)%` | INFO (odniesienie historyczne ok. 10%; decyzja Jeffa po tescie) | dzis (przed A1): renta 487 tys. + danina ok. 90 tys. wobec doplywu - liczy E5/0 |
| A1-T20 | zakupy dla nas - towar zostaje u sprzedajacych | linie paser band (OutlawLaw "Wyrzutki"), "Kontrakty surowca (174)", "Karawany bez amunicji (172b)", CaravanBulk, "Wedrowcy BK - prawdziwy towar (36b)" - liczniki "brak kasy miasta" | INFO wobec biegu bazowego; skok x3 -> wylaczyc `TownPaysFromSurplus` i powtorzyc | - |

---

## 10. Ryzyka i kolizje (kontrola calosci - zasada 0; do "Ryzyko / co sprawdzic" w CHANGELOG)

1. **Wspolne cele latek:** `DeleteOverproducedItems` (BK prefiks false, ArmsLeaks prefiks, CastlePurse/MoneyLedger postfiksy), `MakeConsumption` (BK prefiks,
   CastlePurse First, MoneyLedger), `GetTownGoldChange` kazdej deklaracji (CastlePurse First, SoldierPay First leniwie, MoneyLedger), BK `HandleMarketGold`
   (okno O18 First + finalizer; nasz bool Last). Nowych latek na tych celach brak poza `BkSkimPrefix`; zadna nasza latka nie zmienia ilosci towaru (CalculateBudget,
   `UpdateDemandShift`, `HistoricalPrices.BudgetPostfix` K:HistoricalPrices.cs:832 - nietkniete; to E5/0 i B1).
2. **Nic drugi raz:** cofniecie "zakupow" w jednym miejscu (First, przed ksiega); regulator miasta liczony raz na tick (`_regDue`); zawor raz na miasto i dobe
   (petla rent pomija miasta, `ValveTowns` je placi); czesc pana w `RentToday` raz (nie przez `CastlePurse.LordDuesToday` - to tylko zamki); korona: `CrownToday`
   miast osobno od zamkow, `CrownIncome.Begin` dodaje obie; K3 tnie tylko udzial pana (2/3) - korona swojej 1/3 nie oddaje drugi raz zwrotem; tarcze martwe -
   regulator nie dostaje "keep" dwa razy.
3. **Jedna regula:** zapas kupcow - jedna funkcja `Reserve` dla podatkow, nadwyzki RealPayer/portow i naszych zakupow (miasta); zamki dalej `CastlePurse.Reserve`
   (ta sama formula - cel gry); 20 000 (`TownRentFloorGold`) zostaje tylko w trybie 0 i w liniach-licznikach (K:MarketRoad.cs:199, tekst K:CaravanAmmo.cs:361).
4. **Martwy kod staje sie zywy / zywy martwym:** tarcze 107b/162m (SoldierPay.cs:576-800) martwe przy A1 (kod zostaje dla trybu 0); `RealPayer._belowReserve`
   (K:RealPayer.cs:108) liczy dalej z `Seal31.TownReserve` - przy domyslnych kluczach ta sama liczba (tylko licznik). `CastlePurse.Spendable` dla miast z "cala kasa"
   staje sie "nadwyzka" - 6 wolajacych (MenPurse :525, MenUpgrade :470, ArmyClothing :233 - dla miast i tak bez zaplaty, SupplyDemand :562, BkSeal34 :192, :210).
5. **Zakupy dla nas ponizej zapasu ustaja:** 15 z 97 miast ponizej celu w d120 (L:31686) - tam paser, karawany (surowce, amunicja, kontrakty), wedrowcy 36b,
   nadwyzki zalog/lordow/ludzi gracza i zlom z musztry nie sprzedadza; towar zostaje u sprzedajacego (bandy z lupem, karawany z ladunkiem - nic nie znika).
   Prog A1-T20, wylacznik `TownPaysFromSurplus`. Zamki w tych samych sciezkach - cala kasa jak dzis (niespojnosc z B-2 odnotowana, nie zmieniana).
6. **Zaulki bez dosypki (glowne ryzyko A1 po decyzji 16:10):** miasta, ktorych prawdziwy doplyw netto < 0 (placa za plon wsi przez BK `SellGoodsPatch` cala kasa,
   BK:BannerKings.Patches\EconomyPatches.cs:816-871), nie dostaja juz dosypki - kasa schodzi ponizej zapasu (zawor i podatki stoja, bo biora tylko z nadwyzki), az do 0;
   wtedy miasto nie kupuje plonu (towar zostaje na wozie - zatkane magazyny wsi, prog etapu 4 <= 5.6%) i nic dla nas (BuyFloor). Kasa < 20 000 to tez kara BK
   "Merchants wealth" dla dobrobytu (BK BKProsperityModel.cs:88-91) - nizszy dobrobyt obniza zapas (10 000 + 12 x dobrobyt), ale i popyt. Dzis 15 z 97 miast
   ponizej celu (L:31686). Pomiar: A1-T21 (Jeff: ponizej zapasu / 1 000 / 0, dni z rzedu, towar wsi niesprzedany), wyjscie awaryjne - tryb 1 (MCM, bez kodu).
   Rachunek P [S] (zawor = doplyw netto ok. 398 tys./d) mowi, ze kasy miast jako calosc rosna - ryzyko dotyczy pojedynczych miast, nie swiata.
7. **Rody i korona:** zawor panom 2/3 (dzis 100%) i od nadwyzki ponad zapas (dzis ponad 20 000) - P [S]: panom ok. 265 tys./d zamiast 487 tys. (rody ok. -89 tys./d;
   przy trybie 2 doplyw netto mniejszy jeszcze o dosypke 17 tys./d - zawor ok. 381 tys. = panom ok. 254 + koronie ok. 127 [S]; zloto swiata od samego A1 ok. -110 tys./d
   mniej przyrostu: "zakupy" -422, kasowanie +295, dosypka miast i zamkow -18, BK rynek +35 [S]),
   koronie ok. 133 tys./d; danina wojenna od zapasu kupcow (mediana celu ok. 67 tys., RB 3) zamiast 20 000 - na poczatku nizsza. Powinnosci (KingdomTreasury.cs:87),
   budowy (BuildFunding.cs:156), rada 3.5, D staly 166 licza od mniejszej renty miast - zamierzone (P R4). Wojsko - prog etapu 2; A3 (markietani) oddaje kasom
   wiecej, niz A1 zabiera (P rozdz. E5/A3) - dlatego test zbiorczy A1-A5.
8. **Gracz = AI:** zawor z miast gracza 2/3 gracz / 1/3 korona (gracz w krolestwie), dekret gracza skaluje jego czesc; zwolnienie dekretem - korona bierze swoja 1/3;
   K3 i Z8 gracza (z 1 zl wlasnego wydatku we wlasnym miescie wraca najwyzej 2/3 przez zawor + zwrot korony za reszte) < 1.
9. **Scalanie (rownolegle drzewa od 392c519):**
   - E4/8 (`SCR\e4\48`): `PopulationLaw.cs` hunki :503-531, :905-911, :1154-1159 (`TownGrowth.Daily` przed `if (!On) return;`) - A1: :1182, przed :1216 i nowa metoda
     po `RentOf` (:1019) - rozne hunki; `ArmouryBehavior.cs` konstruktor :395 (A1 go nie rusza) i :1526; `SubModuleMain.cs` :150 (A1 :80-81); `Settings.cs` :745
     (A1 :836); `GarrisonArmory.cs` :79 (A1 :336). Tekstowo bez konfliktu poza `McmSettings.cs`/`gen_mcm.py` (zawsze `python tools/gen_mcm.py` po scaleniu).
   - E4/10 (`SCR\e4\410`): `OutlawLaw.cs` hunki :601-1237 (A1 :1936), `Settings.cs` :745, `SubModuleMain.cs` :145 - bez konfliktu.
   - E4/11 (`w-toku/e4-11`): `ArmouryBehavior.cs` :395, :439-441, :1155, :1446 (A1: :418 nowy try, :1460 nowa linia - 14 linii od :1446, osobne hunki), `Settings.cs` :745.
   - E5/0 (`SCR\e5\0`): jesli nawias konsumpcji E5/0 siedzi w `CastlePurse.ShelfPostfix/ConsumePostfix` - te same dwie metody co A1 (scalac recznie; A1 dopisuje
     jedna linie na poczatku try, niezaleznie od flag E5/0); `MoneyLedger.Obieg.cs` - A1: :86-101, :275-342, :569-572, E5/0: linia "Gotowka wsi" i :654 (A1 :654 nie rusza);
     `tools/sprawdz_logi.py` - dyspozytor `etap5` (rozdz. 9.1); `Settings.cs` - grupa "The townsfolk's purse" E5/0 i "The town's purse" A1 - obie zostaja.
   - A2-A5 (`SCR\e5\a25`): MoneyLedger od M17 (rozdz. 8.4); A2 `ClanBudget.cs:623` (`HoldCourt` no-op przy A1); A3 `CaravanCapital.cs`, markietani do kas miast i do K3;
     A4 `KingdomTreasury.Levies`/`RulingIncomePostfix` (A1 zmienia :391, :528 i dopisuje `NoteTake` przy :399, :422, :431, :485, :513, :529; A4 dopisuje oblezone -
     sasiednie linie, scalac recznie); A6 (lup wodza, Jeff 16:20) - `MapEvent`/`BeatenPurse`, bez wspolnych plikow z A1.
10. **Pulapka MCM (CLAUDE.md 7):** nowe klucze nie istnieja w `Armoury.json` Jeffa (grep 11.10). Przy wgraniu powtorzyc grep (szczegolnie `TownRentLordShare` -
    B3 zmieni domyslna 0.67 -> 0.5; jesli ktos zapisze MCM po A1, wartosc przyszpili sie w pliku). Linia dzienna podaje tryb czynny (nie startowa).
11. **Wyjatki:** kazde miasto w zaworze i przyciecie - wlasny try + licznik potkniec (bez globalnych wylacznikow; pierwszy wyjatek do logu - wzor
    `CastlePurse.Stumble` :171-177); latki: wyjatek = wynik/zachowanie gry bez zmian (prefiks BK -> `true`). Nic nie stoi miedzy zdjeciem a dopisaniem zlota
    (korona: `had`/`taken` z kasy, dopiero potem skarbiec).
12. **Watki i koszt:** latki i tick na watku glownym; zawor 97 miast/dobe, ticki miast - kilka operacji - ponizej 1 ms/dobe.
13. **Stan miedzy kampaniami:** `TownPurse.Reset` (przez `CastlePurse.Reset`) czysci wszystko z 2.1; `arm_townpurse` we wlasnym try.
14. **Wylaczenie w trakcie kampanii:** `TownPurseRegulator` 0 -> od nastepnego ticku regulator, "zakupy", renta z pulapem, tarcze (znaczniki od zera) jak przed paczka;
    przyciety dar nie wraca (flaga) - zamierzone. Wlaczenie w starym zapisie - patrz NK.

---

## 11. NK (nowa kampania) i stary zapis

- **Nowa kampania:** pelny skutek - dar startowy przyciety w d1 (ok. 14 mln [S]); "zakupy" przy tworzeniu swiata cofniete od pierwszego ticku.
- **Stary zapis (Jeffa, zapis testowy z doby 362):** przyciecie ok. 0 (0.75^wiek), znaczniki tarcz porzucone (linia), od nastepnej doby regulator nie kasuje,
  "zakupy" cofniete, regulator nie dosypuje (tryb 2 - miasta i zamki ponizej zapasu zostaja tam, az przyjdzie prawdziwy doplyw), 10 miast zaczyna placic. Nadwyzka kas ponad zapas (d120 z31-A: 12.56 mln wobec 7.22 mln = ok. 5.3 mln, w tym 2.86 mln dzis "pod tarcza")
  splywa zaworem 7%/d - jednorazowo ok. 0.37 mln/d na start, malejaco (2/3 panom, 1/3 koronom). Bez osobnego rozruchu (to prawdziwe zloto miast, dzis oddawane
  wolniej renta z pulapem albo kasowane).

---

## 12. Pytania

Do Jeffa - **brak** (reguly R1-R7 przyjete w projekcie v2, udzial 2/3 jak zamek - klucz 114, decyzja E 09.10; tryb 2 bez dosypki i liczenie zaulkow/udzialu
zysku kupcow - decyzja 11.10 16:10). Po tescie A - raport dla Jeffa: zaulki (A1-T21) i udzial zysku kupcow wobec ok. 10% (A1-T22).
Rozstrzygniete w planie (do recenzji, nie do Jeffa):
1. `TownPaysFromSurplus` + 8 sciezek zakupu spoza listy P (rozdz. 0 pkt 7) - TAK, bo regula E5K1-03 ("jedno dno zakupow") i prog P "nasze zakupy ponizej zapasu = 0";
   RecruitKit - NIE (likwidacja dobytku, nie zakup).
2. Zawor wolany z `PopulationLaw.Daily` (nie osobny `TownPurse.Daily`) - kontrola `obieg169_sprawdz.py:357-360` i jeden wlasciciel pol rent.
3. Oblezone miasto bez zaworu w A1 bez klucza (jak zamek 110); `BesiegedPayNothing` (A4) obejmie wsie, 184 i polityki.
4. Do koordynatora: E5/0 - nawias konsumpcji w `CastlePurse` (jesli tam) i linia Obieg :654; drzewo A2-A5 - indeksy od M17.

---

## 13. Kolejnosc kodowania (galaz `w-toku/e5-a1`; kazdy krok: build Release kod 0 - `cd` do katalogu projektu przed `dotnet build`; osobny commit; jeden test na koncu)

1. **K1 rdzen bez skutku przy kluczu 0:** `TownPurse.cs` (2.1, 4, 3.2-3.4, 6), wpiecia w `CastlePurse` (3 latki, galaz zamku trybu 2, tekst :662,
   `HookedModels/HookedConsumption`, `Reset`, `Spendable`, `PayFloor`), `Seal31.Spare`, klucze (7.1) + `python tools/gen_mcm.py`. Linia startowa.
2. **K2 zawor:** `PopulationLaw` (skip + `ValveTowns`), `TownPurse.Valve/ValveDay`, `KingdomTreasury` (:391, :528, :232 i `NoteTake` :399, :422, :431, :485, :513,
   :529), `CrownIncome` (:178, :320), `ArmouryBehavior` (`BeforeRents` :1460, `arm_townpurse` :418).
3. **K3 ksiega i linie:** `MoneyLedger` (8.1-8.2, getter `TownUnsoldToday`), `Measure169c` dopisek (z zaulkami i udzialem zysku kupcow - 7.3) i CSV, "Obieg",
   `SoldierPay` (R7: `Hold`, `HoldCourt`, `RegulatorPostfix`, `Decay*`, `HeldOf`, `CourtOf`, `DropShields`, `CourtNote`).
4. **K4 R6 i zakupy:** `BkSkimPrefix` + `ApplyAll`; 8 sciezek zakupu (rozdz. 4 ostatni punkt).
5. **K5 narzedzie i wpis:** `tools/sprawdz_logi.py` blok A1 (9.1), sprawdzenie na syntetycznych liniach; CHANGELOG (Problem z liczbami: "zakupy" 421 538/d sr28 i 425 318
   d120 L:31722, kasowanie 295 208/d sr28 i 248 065 d120 L:31686, dosypka 17 057/d sr28 (20 220 d120) i zamki ok. 1 tys./d - tryb 2 (Jeff 16:10), renta 100% panu 487 312/d sr28, pod tarcza 2.86 mln L:31686, BK rynek -34 610 L:31718, dar startowy
   17.4 mln wobec 6.5 mln L:505, "10 miast bez placenia" K:PopulationLaw.cs:1172/:1174; Przyczyna: BK EconomyPatches.cs:555, G DefaultSettlementEconomyModel.cs:75-79,
   K:PopulationLaw.cs:1192-1201, BK BKSettlementBehavior.cs:463-490, K:SoldierPay.cs:576-800; Odstepstwa: rozdz. 0 pkt 6-8, `TownPaysFromSurplus`, `Reset` przez
   CastlePurse; Ryzyko = rozdz. 10; Status: DO SPRAWDZENIA - test zbioru A po tescie koncowym etapu 4), dwie recenzje diffu (ekonomia/szczelnosc; kod i kolizje).
