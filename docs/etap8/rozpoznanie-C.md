# Etap 8 - rozpoznanie C: 8.11, 8.12, 8.13, 8.15, 8.16 + "poza zakresem" etapow 5-7 + ERRORS.md (12.10.2026)

Zakres: pozycje PLAN-DO-KONCA-MODA-2026-10-09.md rozdz. "Etap 8" 8.11, 8.12, 8.13, 8.15, 8.16; wszystkie wpisy "Poza zakresem"
z CHANGELOG drzew `w-toku/zbior-5-8` (SCRATCH\zb\58, b461cc5), `w-toku/e6-p3`, `w-toku/e7-a`, `w-toku/e7-b` (32 rozne wpisy po
usunieciu powtorek - zbior-5-8 zawiera wszystkie); bledy cudzych modow z `docs/ERRORS.md`. Tylko odczyt - kod, gra i zapisy
nietkniete.

Oznaczenia dowodow: [K] kod `zb\58` (sciezka:linia), [G] dekompilacja gry (`dec32\cs_full`), [BK] dekompilacja BK (`e4tools\bkfull`),
[BR] BKROTPatch (`b2\bkrotfull`), [L] log Armoury-2026-10-12_02-44-09.log (120 dob, WGRANIE 33, doba 120 = dzien 108955),
[CS] session-2026-10-12_02-44-08.log, [D] dokument. Propozycja: **TAK** (do etapu 8), **NIE** (z powodem), **POMIAR** (linia logu /
test, kod dopiero po liczbach), **JEFF** (prawdziwy wybor - rada w rozdz. 9).

Zasady przylozone do kazdej propozycji: zamknieta ekonomia ("nic z niczego"), "nie robimy sztucznych praw ani doplat" (Jeff 12.10),
etap 8 osobno (nie przy okazji innych paczek), kazda paczka z wylacznikiem MCM, napisy po angielsku, nowa kampania Jeffa
(NK dozwolone, stary zapis ma sie wczytac), CLAUDE.md 8 pkt 4 (ozywiany martwy kod - policz, ile pracy przybedzie).

---

## 1. Wynik w skrocie

| Poz. | Co | Stan dzis (dowod) | Propozycja |
|---|---|---|---|
| 8.11a | Stany gubione / nie czyszczone miedzy kampaniami | 4 klasy bez Reset w konstruktorze `ArmouryBehavior` (HideoutPurge, Stables, CrossingLaw, MapClock) + pola NightRest i TerrainEase poza Reset | **TAK** paczka C1 (S) |
| 8.11b | Obozy nie czyszczone przy nowej grze | `NightRest.ResetWorld` nie zeruje `_tentStrikes`, `_tentChildren`, `_promptDay`, `PlayerCamped` [K NightRest.cs:254-273, :55, :942-952] (`_hadPos` juz zerowany) | **TAK** w C1 |
| 8.11c | Klimat Westeros - globalny wylacznik | `_initBroken = true` w catch `Ready()` gasi klimat do konca sesji [K WesterosClimate.cs:39, :72, :87] - pulapka CLAUDE.md 7 | **TAK** w C1 (licznik potkniec, ponowna proba nastepnej doby) |
| 8.12a | Karawany BK decyduja raz na dobe | BKROT `BKCaravansHourlyThrottlePatch`: AI-karawana dostaje `HourlyTickParty` BK 1 raz na 24 h [BR]; w miescie BK robi ruch tylko z szansa 1/3 na tik [BK BKCaravansBehavior.cs:858-860] -> postoj w miescie srednio ok. 3 doby zamiast ok. 3 h | **TAK** paczka C2 (S/M) + POMIAR postoju; **JEFF** (rada TAK) |
| 8.12b | Wyspy | lata IslandRoads dziala (0 odrzucen w 120 dobach [L]); zostaja 2 karawany lordow BK na wyspie z jednym miastem, czekaja 118 dob (Mormont Keep, Driftwood Hall) [L 03:21:08 "Wyspy i drogi"] | **TAK** paczka C3 (S) |
| 8.12c | Wies bez wozu | 28 z 571 wsi bez wozu w dobie 120, 13 z nich zatkane; prawie wszystkie spladrowane/najezdzane [L "Zatkane wsie (diagnoza)"]. Gra wystawia nowy woz tylko przy pelnym magazynie i stanie Normal [G VillagerCampaignBehavior.cs:131-157] - regula gry, nie blad | **NIE** (zamknac dowodem) + POMIAR "Normal bez wozu > 7 dob" = 0 w tescie |
| 8.12d | Wozy nie widza karawan | `VillageCartRoadNews` liczy tylko towar innych wozow [D CHANGELOG wpis 1b]; skutek = mozliwy nadmiar dostaw do jednego miasta, nie zloto/towar z niczego | **NIE** (usprawnienie, nie blad) - obserwowac "KUPNO STOI"/glut |
| 8.12e | Najemnicy ROT z wodzem z niczego | BK `ConvertTroopsMercenaries` co dobe: (1) w sluzbie krolestwa kazdy wpis spoza szablonu - TAKZE BOHATER - zamieniany 1:1 na zolnierza szablonu (wodz wyrzucony z druzyny, zolnierz z niczego); (2) klan BEZ krolestwa dostaje 3-4 ludzi szablonu (`RandomInt(3,5)`) na druzyne na dobe z niczego [BK BKClanBehavior.cs:480-540]. Nasza blokada tylko dla klanu Spoils (SpoilsCompany) | **TAK** paczka C4 (S) + POMIAR; **JEFF** (rada TAK) |
| 8.13a | Bledy startowe cudzych modow | [CS] 24 wyjatki, wszystkie przy starcie: BKROT A5 (3 klasy), BK A7 (7 klas), TournamentsXPanded loader (2), MonoMod przy latce klimatu (wewnetrzny, latki 25/25 wpiete [L:63]), DTE mixin (1 raz), **BK `CorrectPlayerEducation` ArgumentNullException** | **TAK** tylko BK Education (Mend); reszta **NIE** |
| 8.13b | Kara snu z dodatkiem morskim | NAPRAWIONE 11.10 (E4 bledy): partia po pozycji `__0`, "morale w 4 modelach" [L:133], bramka glebokosci (kara raz) [K NightRest.cs:1279-1297] | **NIE** (zamkniete) - tylko test 8.16 |
| 8.13c | Krolestwo bez rodu rzadzacego | nasze petle maja try na rod i straze `RulingClan != null` [K KingdomTreasury.cs:74-78, :387; CirculationWindows.cs:1220]; BK `GetHeroesToUpdate` NRE dzis nie wystepuje [D ERRORS A4] | **NIE** (zamkniete w kodzie) |
| 8.13d | Ustawienia czytane miedzy modami bez ostrzezenia | RC/GT `TownWageLink`, `WorthLink38`, `ArmouryBridge` pisza PODPIETY/BRAK; ciche `catch {}` zostaly w CrashScribe Mends [K Mends.cs:886-894, :1693-1697, :1806] i ForgeView [K ArmourFilterMixin.cs:189, :256] | **TAK** w C5 (jedna linia startu na odczyt) |
| 8.13e | Zbroja konia liczona jak pancerz | Armoury `ItemReq.SkillFor` - ladry = Jazda (Jeff 09.10) [K ItemReq.cs:78]; CrashScribe `Mends.ReqSkill` dalej ladry -> Atletyka [K Mends.cs:1044-1052] (warta DTE, przydzial z polki) | **TAK** w C5 (1 linia) |
| 8.15 | Pomiary | brak linii: model POD NavalDLC (produkcja wsi), rozklad kary Underequipped, dochod owiec; test Innych od doby 728 - procedura | **POMIAR** paczka C6 |
| 8.16 | Testy Jeffa | lista w rozdz. 7 (12 pozycji z PLAN + 9 nowych z etapow 5-7) | lista dla Jeffa |

Z "poza zakresem" (rozdz. 8) do etapu 8 proponuje 14 pozycji (5 to zrodla z niczego / w nicosc), NIE - 18. Z ERRORS.md (rozdz. 9)
- tylko BK Education (8.13a) i CrossingLaw (paczka C8).

---

## 2. 8.11 - stany miedzy kampaniami i zapisem

### 2.1 Co znalazlem w kodzie
Skan: kazda klasa z `static void Reset()` i kto ja wola. Konstruktor `ArmouryBehavior` (nowa gra i wczytanie, przed SyncData)
wola ok. 150 Resetow [K ArmouryBehavior.cs:395]; klasy bez Resetu w konstruktorze, ktore resetuja sie same (Seal31 :200,
ArmourQuality :73, SellByCondition :426) albo przez inne Resety (Seal31 -> RealPayer, CaravanCapital, ...) sa w porzadku.
Zostaje:

| Klasa | Stan zywy miedzy kampaniami | Skutek | Poprawka |
|---|---|---|---|
| `HideoutPurge` (brak Reset) | `_pending`, `_pendingGold/_Bands/_Name/_Pos/_Hideout`, `_reprisalPack` (lista MobileParty), `_searchLeave` [K HideoutPurge.cs:34-42, :360] | po wczytaniu innego zapisu: stara kryjowka (obiekt z poprzedniej gry) w `_pendingHideout`, odwet starej bandy; zapis w trakcie przeszukania gubi przeszukanie (SyncData pusty :63) | Reset w konstruktorze; zapis przerwanego przeszukania NIE (rzadkie, kasa kryjowki schodzi dopiero przy przeszukaniu - nic nie ginie) |
| `HideoutPurge.OnTick` | catch zeruje `_pending` po jednym wyjatku [K :170] | lup przeszukania przepada | licznik potkniec, `_pending` zostaje (najwyzej N prob) |
| `NightRest` | `_tentStrikes`, `_tentChildren`, `_promptDay`, `PlayerCamped` [K :55, :942-952] | z kampanii A do B: licznik potkniec namiotu, stan "gracz w obozie", pytanie o sen | dopisac do `ResetWorld` (recenzja T1 juz to wskazala - CH zb58:6253) |
| `TerrainEase` | `_lastAuditDay` poza Reset [K :63, :177, :253] | audyt gracza pomija dobe po wczytaniu innej kampanii | 1 linia w Reset |
| `Stables` | `_lastBuy` (StringId druzyny -> dzien) [K :788, :818-819, :1006] | warunek `today - last < cooldown` przy dniu z DALSZEJ kampanii daje roznice ujemna = blokada: po nowej grze (start zawsze 108836) albo wczytaniu starszego zapisu w tej samej sesji stajnie AI nie kupuja koni przez tyle dob, o ile stara kampania byla dalej | Reset (Clear) |
| `CrossingLaw` | `_lastSafe/_haveSafe` (pozycja gracza), `_aiCooldown` (MobileParty starej gry) [K :36-37, :75-82] | po nowej grze we wrogiej strefie gracz odepchniety do pozycji ze STAREJ kampanii | Reset (z paczka C8 albo C1) |
| `MapClock` | brak Reset (tylko predkosc - wraca co godzine) | brak | NIE |
| `WesterosClimate` | `_initBroken` - jeden wyjatek w `Ready()` gasi klimat (pory, zima, ekonomia 19 metod) do konca sesji [K :72, :87] | jw. | licznik potkniec + ponowienie w kolejnej dobie (najwyzej 3, potem linia ERROR raz na dobe) |

"Stany gubione przy zapisie" z dawnych audytow: WorkshopLaw (D5 AUDYT-DZIURY) - JUZ w zapisie (`arm_workshops`, [K ArmouryBehavior.cs:594]),
zamowienia `arm_unmet` [:596], zlom `arm_scrap` (174b.6). Swiadomie bez zapisu (liczniki sesji, opisane w projektach): premie wojenne
`ArmsPricing`, pomiary E4/E5/E7. OT pkt 11 (ksiega stanow magazynu `arm_armory_wear` przez zapis) - test 8.16.

### 2.2 Paczka C1 "porzadek stanu" - TAK
Wielkosc S. Wylacznik: nie zmienia gry poza przypadkiem "druga kampania w tej samej sesji" i odpornosci klimatu, ale wg zasady
Jeffa dostaje `StateHygieneEnabled` (domyslnie TAK). Ryzyko (zasada 0): `HideoutPurge.Reset` musi byc wolany PRZED SyncData (jak reszta)
i nie moze zdjac sluchaczy menu (`AddMenus` na OnSessionLaunched - tylko pola statyczne); `_reprisalPack` trzyma partie - Clear, bez
`SetInitiative` (partie starej gry nie istnieja). Klimat: ponowienie `Ready()` nie moze dodac drugiego segmentu lata (warunek
`_segs.Count > 0` zostaje). Test: autotest = nowa kampania + wczytanie zapisu w tej samej sesji (jest w procedurze "zapis 9 dob").

---

## 3. 8.12 - karawany, wozy, wyspy, najemnicy

### 3.1 Karawany BK raz na dobe (C2) - TAK, JEFF
- [BR] `BKCaravansHourlyThrottlePatch.Prefix`: dla karawan AI (nie gracza) `HourlyTickParty` BK biegnie tylko, gdy
  `hash(StringId) % 24 == godzina` - raz na 24 h. Cel BKROT: wydajnosc.
- [BK BKCaravansBehavior.cs:858-860] karawana w miescie przy kazdym tiku: zakup/sprzedaz/nowy cel tylko gdy `randomFloat < 1/3`
  (albo zadanie). Z dlawikiem: srednio 3 tiki = ok. 3 doby postoju na kazde miasto (w grze bez dlawika ok. 3 h). Karawana na drodze:
  tik BK tylko poprawia cel/ucieczke - tu dlawik jest rozsadny.
- [L] karawan bez statkow stojacych 7+ dob w tym samym miescie: 29 (Castle Black 6, Pyke 3, Meereen 2, ...); karawany kupuja rude
  64 wyjazdy/dobe, z tego "w drodze dosc" 40 - handel jest, ale wolny. Brak linii sredniego postoju.
- Propozycja: nasz prefiks z wyzszym priorytetem (albo Mend jak A1/A2) - w miescie (`CurrentSettlement?.Town != null`, nie oblezone)
  tik BK przechodzi co godzine; na drodze dlawik zostaje. Wylacznik `CaravanTownHourly` (rada TAK). POMIAR przed i po: "karawany w
  miescie: postoj sredni h, wyjazdow/dobe".
- Ryzyko (zasada 0): wiecej kursow = wiecej handlu, myt, podatku miast, zakupow `CaravanBulk` (nasz zakup surowcow siedzi w tiku BK -
  "115 wizyt/dobe" urosnie) i zuzycia `CaravanAmmo`; zysk karawan rosnie - to prawdziwy przeplyw (kasa karawany -> kasy miast), nie
  zloto z niczego. Koszt (8.17): dodatkowe tiki tylko dla karawan w miescie (ok. 700 karawan, czesc w miescie) - zmierzyc stoperem
  w linii, prog < 5 ms/dobe. Kolizje: IslandRoads i BuyGoods-postfiksy biegna w tym samym tiku - bez zmian kolejnosci.

### 3.2 Wyspy (C3) - TAK
- [L 03:21:08] "karawany bez statkow w miescie bez innego miasta w zasiegu ladu (wyspa z jednym miastem) - czekaja: 2 [Mormont Keep 1,
  Driftwood Hall 1] (lord - dom wies 2), najdluzej 118.0 dob". Przyczyna znana (STAN-PRAC:66): karawany lordow kupione przez BK w zamku/wsi
  wyspy (`BKLordPropertyBehavior.cs:69-79`) - nie maja dokad jechac.
- Propozycja: (a) prefiks na zakup karawany BK: wyspa z jednym miastem i rod bez statkow -> zakup pominiety (zloto zostaje u lorda);
  (b) istniejaca karawana czekajaca > 14 dob -> rozwiazana: towar sprzedany w miescie (kasa miasta placi), kiesa + utarg do wlasciciela,
  ludzie do karczmy/wsi (sciezka zwolnienia jak VetPurse). Nic z niczego, nic w nicosc. Wylacznik `IslandCaravanFix`.
- Ryzyko: nalezy sprawdzic w BK, czy rozwiazanie karawany lorda nie odpala BK-zwrotu ceny (podwojny zwrot).

### 3.3 Wies bez wozu - NIE (regula gry)
[G VillagerCampaignBehavior.cs:122-157]: nowy woz tylko gdy wies w stanie Normal, magazyn pelny (`num >= GetWarehouseCapacity`) i los
15% na przeglad. [L] doba 120: bez wozu 28 wsi; zatkanych z BRAK WOZU 13, a zatkane wsie to w 18 z 22 przypadkow spladrowane lub
najezdzane; "hearth <= 12 - gra nie wystawi nowego: 0". Maks. czas bez wozu w biegu 44.9 doby (wojna). Do testu: prog "zatkane
Normal bez wozu > 7 dob" = 0 (narzedzie ma dane z linii diagnozy).

### 3.4 Wozy nie widza karawan - NIE
Zysk: mniej podwojnych dostaw. Skutek dzis: najwyzej przejsciowo tanszy towar w jednym miescie - przeplyw prawdziwy. Nie blad. Jesli
test pokaze "KUPNO STOI"/glut tam, gdzie wozy i karawany jada razem - wtedy osobny krok.

### 3.5 Najemnicy swiata (C4) - TAK, JEFF
- [BK BKClanBehavior.cs:480-540] `ConvertTroopsMercenaries` (co dobe, `IsClanTypeMercenary`):
  (1) klan w krolestwie: kazdy wpis skladu spoza szablonu klanu zamieniany 1:1 na zolnierza szablonu tego samego poziomu
  (`MercenaryConversionChance` = 1.0). Dotyczy tez BOHATERA - wodz wyrzucony z druzyny, zolnierz z niczego (paczka Spoils, zb58 doc
  `spoils-najemnicy-prawdziwi.md:26`). Zamiana zwyklych zolnierzy 1:1 to tylko zmiana typu (liczba ludzi ta sama).
  (2) klan BEZ krolestwa: kazda druzyna dostaje 3-4 ludzi szablonu (`RandomInt(3,5)`, gorna granica wylaczna) do limitu - **z niczego, codziennie**.
- Nasza blokada tylko dla klanu Spoils (`SpoilsCompany` prefiks; [L] linia "Spoils - klan najemnikow"). 5 klanow najemnikow ROT - bez zmian.
- Propozycja: ten sam prefiks dla wszystkich klanow najemnikow: (1) bohater nie jest zamieniany; (2) dosypka poza krolestwem
  zablokowana; zamiana 1:1 zwyklych zolnierzy zostaje (barwy kompanii). Wylacznik `MercClansRealSoldiers`. Najpierw POMIAR w linii:
  ile razy BK zamienil bohatera i ile ludzi dosypal na dobe (dla 5 klanow).
- Ryzyko: klany bez sluzby beda rosnac tylko werbunkiem gry (druzyny lordow, takze pomniejszych frakcji, werbuja u notabli) - mniejsze
  druzyny najemnikow; spojne z "nie robimy sztucznych doplat". Kolizja: prefiks Spoils na tej samej metodzie - jeden prefiks z
  rozszerzonym warunkiem, nie drugi.

---

## 4. 8.13 - cudze mody i drobne

### 4.1 Bledy startowe [CS SUMMARY]
| Blad | Mod | Wplyw | Propozycja |
|---|---|---|---|
| `DynamicPartySizePerformancePatch`, `DynamicPartySizeBattleSkipPatch`, `RebellionsGameEntityInstantiatePatch` | BKROT | brak (mody nieobecne; ERRORS A5 - BattleSkip to ta sama wada, dopisac do A5) | NIE |
| 7 klas latek BK (`countChange`, `TraceMbEventClanInvoke`, 3 x `SetMove*`, `DeclareWarVMPatch`, `BKBattleRewardTweakPatches`) | BK | ERRORS A7: brak wplywu poza klasa 7 (atuty BK) | NIE; klasa 7 = B10b (JEFF, rada nie ruszac) |
| `Instances of abstract classes cannot be created` x2 (`Bannerlord.ModuleLoader.TournamentsXPanded.Load`) | TournamentsXPanded | loader probuje utworzyc klase abstrakcyjna; mod laduje sie dalej | NIE (dopisac do ERRORS) |
| `Cannot bind to the target method` (MonoMod przy `Armoury.WesterosClimate.ApplyAll`) | Harmony/MonoMod | first-chance wewnatrz MonoMod; latki klimatu 6 + 19 wpiete [L:63] | NIE |
| `AmbiguousMatchException` `BaseViewModelMixin<MapBarVM>` cctor z `MapArmoryReadinessMixin` | DTE + UIExtenderEx | dzis 1 raz na sesje (bylo 604) - koszt zerowy; linia Mends "MapArmoryReadinessMixin nieobecny" [CS:240] jest FALSZYWA (sprawdzenie przed rejestracja DTE) | NIE (pytanie O z PLAN: "nie wylaczac"); poprawic tekst linii Mends przy okazji C5 |
| **`ArgumentNullException` w `EducationManager.CorrectPlayerEducation`** (z `BKCampaignStartBehavior.OnCharacterCreationOver`) | BK + dane jezykow | [BK EducationManager.cs:119] jezyk z `Cultures == null` -> `Any` rzuca; edukacja gracza usunieta i NIE dodana; wyjatek wychodzi z `OnCharacterCreationOver` [BK BKCampaignStartBehavior.cs:229], a `MbEvent.InvokeList` nie ma try [G MbEvent.cs:37-44] - **pozostali sluchacze `OnCharacterCreationIsOver` po BK nie biegna** (nasz StartKit ma przez to obejscie godzinowe - K StartKit.cs:146) | **TAK** (C5): Mend - prefiks/finalizer na `CorrectPlayerEducation` pomijajacy jezyki bez kultur (wynik: jezyk kultury gracza albo Calradian, jak w BK) |

### 4.2 Kara snu z dodatkiem morskim - NIE (zamkniete)
AUDYT-LOGI-2026-10-04 pkt 12: postfiks morale nie wpinal sie w `NavalDLCPartyMoraleModel` (inna nazwa parametru). Od E4 bledy (11.10)
`MoralePostfix(MobileParty __0, ...)` + `SpeedDepth.OutermostMorale` (kara raz w lancuchu) [K NightRest.cs:1279-1297]; [L:133] "morale w
4 modelach". Do testu 8.16: sen na morzu (dlug snu na statku) - kara morale widoczna raz.

### 4.3 Krolestwo bez rodu rzadzacego - NIE (zamkniete)
AUDYT-PELNY-EKONOMIA S8 (`RulingClan == null` przerywal pobor wszystkim). Dzis: `KingdomTreasury` try na rod [K :74-78], wszystkie
odczyty `RulingClan` z kontrola null (2 miejsca bez `?.` - oba z warunkiem, [K CirculationWindows.cs:1220, KingdomTreasury.cs:387]).
BK `GetHeroesToUpdate` (ERRORS A4, trzecia galaz) - 0 wystapien. Bez kodu.

### 4.4 Ustawienia miedzy modami (C5) - TAK
OT pkt 6. Dzis z linia PODPIETY/BRAK: RC i GT `TownWageLink`, GT `WorthLink38`, RC `ArmouryBridge`. Ciche: CrashScribe Mends
`ForgePartsFreeBelowTier` [K Mends.cs:886-894], `ArmouryBehavior` [:1693-1697], `Armoury.Settings` [:1806]; ForgeView `RangedLore`
[K ArmourFilterMixin.cs:189-234], `UniqueLore` [:256-296]. Poprawka: przy pierwszym odczycie jedna linia "<link>: PODPIETY / BRAK
(<pole>)" - tylko log, bez zmiany zachowania (fallback zostaje).

### 4.5 Zbroja konia (C5) - TAK
P175:133: "ReqSkill liczy ladry jako pancerz jezdzca -> Atletyka". Armoury poprawione (Jeff 09.10) [K ItemReq.cs:78]; CrashScribe ma
wlasna kopie bez tej linii [K Mends.cs:1044-1052] - uzywana w warcie przydzialu DTE (:1060, :1215, :1229, :1268, :2434). Poprawka:
`HorseHarness -> Riding` przed `HasArmorComponent` (albo wolac `ItemReq.SkillFor` przez refleksje - nie: druga zaleznosc miedzy modami).
Skutek: jezdzcy dostaja ladry z polki wedlug Jazdy, nie Atletyki (zgodnie z Armoury).

### 4.6 Paczka C5 "cudze mody - drobne" - TAK
Zawartosc: BK Education (Mend), `ReqSkill` ladry, linie PODPIETY/BRAK, tekst linii Mends DTE, `MapVillageLore.SessionStart` po
`McmSettings.Apply` (rozdz. 8 poz. 5). Wielkosc S. Wylacznik dla Menda BK (`MendBkEducation`). Ryzyko: Mend BK - tylko gdy metoda
istnieje (`AccessTools.Method` != null), linia "Mends: edukacja BK gracza - pominiete jezyki bez kultur N".

---

## 5. 8.15 - pomiary (paczka C6 - POMIAR)

| Pomiar | Stan | Co dopisac | Gdzie przeczytac |
|---|---|---|---|
| Test Innych od doby 728 | dlugi bieg 10-11.10 doszedl do doby 728 (`kopia-728`, progi3: 20/20 TAK); do biegu dalej - zapis tej doby (kandydat `Game Saves\autotest-dlugi.sav` 12.10 03:02 - sprawdzic dobe przed uzyciem) | bez kodu; procedura autotestu "wczytaj zapis, N dob" na ostatnim DLL; w 120-dobowym biegu Inni: 4 bandy, 3034 trupow, brak 25 osad za Murem do inwazji [CS UMARLI] | CS "UMARLI", "Kalendarz Innych", "FABULA" |
| Produkcja wsi z NavalDLC | czynny model `NavalDLCVillageProductionCalculatorModel` [L "Przeplywy osad: modele czynne"]; nie wiadomo, co jest POD nim (`BaseModel`) | a02 N1: nazwa `BaseModel` przez refleksje + iloraz wynik/baza dla 10 typow wsi (raz na dobe 1) | linia "modele czynne" |
| Kara "Underequipped" | DTE `MarkUnderEquipped` -2 morale za brakujacy tier, max -20 [D P174:156]; brak linii | po bitwie gracza: ilu ludzi z kara 0 / 2-10 / 12-20 u gracza i u przeciwnika (P174:641) | linia po bitwie gracza (autotest bitew gracza nie gra - Jeff) |
| Dochod owiec | brak linii | owce: sztuk w stadach wsi, welna/dobe, utarg welny i miesa wsi owczych (sheep_farm 24 wsie) | linia dobowa |
| (nowe z rozdz. 3) postoj karawan | brak | sredni postoj w miescie (h), wyjazdy/dobe | C2 |
| (nowe) zamiana BK najemnikow | brak | bohaterowie zamienieni, ludzie dosypani (5 klanow ROT) | C4 |
| (nowe, rozdz. 8) zrodla BK bez platnika | brak | zywnosc BK `GiveFood` bandom hersztow; ujscia BK `Demand.FinancialCompromise`, `BKCourtierBehavior.DrainGold`, `BuyGoodsAction` (kwoty/dobe) | linia "BK i Diplomacy (E5/A8)" - dopisek |

Wszystko tylko log; jedna paczka C6, wylacznik = istniejacy `LogEnabled`.

---

## 6. CrossingLaw (z etapu 7 / ERRORS) - paczka C8, TAK

[K CrossingLaw.cs:73-76] `if (!inHostileZone) { ... return; }` stoi PRZED `AiTick(s)` [:100] - petla zawracania wrogich lordow biegnie
tylko, gdy GRACZ stoi we wrogiej strefie przeprawy. Projekt E7 (rozdz. 4, RB7): 0 linii w 120 dobach; zegar klatek i `DateTime.Now`
[:92] (regula R4 - logika na zegarze gry). Ustawienie Jeffa `CrossingLawAi = true` ("the bridge watch also turns back hostile AI lord
parties") [K Settings.cs:158] - dzis martwe.
Propozycja: AiTick niezalezny od gracza, na zegarze gry (raz na godzine gry), pomija spiacych (SleepGate E7/O1), Bliznaki po id
`ROT_town3` takze w `Measure169c` (dzis po fragmencie nazwy "Twins" - rozdz. 8 poz. 9), licznik zawrocen w linii E7/B.
Ryzyko (CLAUDE.md 8 pkt 4 - ozywiany kod): ile pracy - liczba wrogich partii w promieniu przeprawy na godzine (z licznika E7/B:
przejscia przez Bliznaki/dobe); zakleszczenia sciezek AI (jest punkt odwrotu + 3 h cooldownu); zmienia geografie wojny Dorzecza
(lordowie obchodza Bliznaki) - to zamiar ustawienia. Myto (decyzja Jeffa 15:00) liczy ruch z E7/B - zawracanie wrogow zmniejszy
ruch, wiec stawki myta liczyc PO wlaczeniu C8. Wylacznik istnieje (`CrossingLawAi`).

---

## 7. 8.16 - lista testow Jeffa (do recznego sprawdzenia; autotest tego nie gra)

| # | Co zrobic w grze | Co ma byc / linia w logu |
|---|---|---|
| 1 | Kuznia: "Kuj" przy bramie T1/T2 (szablon z kazdym slotem), panel wyniku (dzwiek + Done bez crasha), pokretlo 35, "Pick a piece" (wrak na zlom, sztuka na grzbiecie) | Kuj swieci tylko z darmowa czescia w slocie; CS "Mends: brama kucia - prog T2..." bez "SAMOKONTROLA przywrocila"; topory miotane ROT (OT 8) - czy Kuj dziala |
| 2 | Kuznia - naprawa wlasnymi rekami (N1/N1b): kilka lekkich napraw tej samej sztuki, potem zapis i wczytanie | dopisek "ulamki w pamieci" rosnie, po kilku naprawach schodzi 1 sztuka; licznik przezywa zapis (`arm_mend_frac`) |
| 3 | Okna Spoils: Done i Cancel; kwatermistrz Spoils z materialem | Cancel nic nie zmienia; naprawa zdejmuje material |
| 4 | Turniej (GT): zapis, walka, nagroda; turnieje co 3. tydzien (E7/K) | nagroda w skali 8.18 (po kodzie 8.18) |
| 5 | Niewola (RC): ucieczka, okup, praca; pojmujacy bierze tez juki | komunikat RC; linia RealisticCaptivity.log |
| 6 | Jency do domu i ekran po bitwie | jency z krain bez niewoli ida do domu (I1) |
| 7 | Sprzedaz czystej, Battered i wraku | cena wedlug stanu (155) |
| 8 | Kulawy kon | leczenie/cena kulawego |
| 9 | Rezerwa miasta 20 000 | sprzedaz w miescie z kasa przy rezerwie - kupiec odmawia ponad rezerwe |
| 10 | Samonaprawa (TroopSelfMend) | ludzie naprawiaja obity sprzet z zapasu |
| 11 | Karczma - najemnicy | najemnik z wlasnym koniem albo bez konia (decyzja 08.10), bez doplaty za konia |
| 12 | (nowe) Przeszukanie kryjowki: "Call the search off" i do konca | brak crasha przy przelaczaniu menu (SwitchToMenu z tiku = wzorzec gry, G PlayerArmyWaitBehavior.cs:189) |
| 13 | (nowe) Napad na oboz gracza - namioty i ognisko (E7/O2) | WYMAGA `CampBattlePropsEnabled: true` w Armoury.json Jeffa (dzis false, :302) |
| 14 | (nowe) Sen do switu w armii AI (E7/O1); sen na statku (kara morska, 8.13b) | lordowie nie maszeruja noca; kara morale raz |
| 15 | (nowe) Wioski: dymek (W2), menu okregu (W3), komunikat "plonie Twoja wioska" (W1), proporczyk pana (W5) | napisy po angielsku; herb pana u wiosek bez herbu |
| 16 | (nowe) Start nowej kampanii: okno BK (start + religia), jezyk postaci w BK | po C5: brak wyjatku BK Education w CS |
| 17 | (nowe) Wczytanie STAREGO zapisu Jeffa na koncowym DLL | wczytuje sie; brak ERROR w Armoury |
| 18 | (nowe) Nowa gra -> zapis -> wczytanie innego zapisu w tej samej sesji | po C1: brak starego obozu, kryjowki, cooldownow stajni |
| 19 | (nowe) Ksiega zuzycia magazynu przez zapis (OT 11, `arm_armory_wear`) | stan zbrojowni ten sam po wczytaniu |
| 20 | (nowe) Bliznaki we wojnie z Freyami (po C8) | gracz i wrodzy lordowie zawracani |
| 21 | (nowe) Bitwa gracza - kara Underequipped (po C6) | linia rozkladu kary |

---

## 8. "Poza zakresem" z CHANGELOG etapow 5-7 - klasyfikacja (zb58 = CHANGELOG `w-toku/zbior-5-8`, linia)

| # | Zrodlo | Co | Propozycja |
|---|---|---|---|
| 1 | zb58:27 (E5/A8) | BK `Feast.Automanage` :216-219 po zakupie DODAJE towar z powrotem do rynku (`+n` zamiast `-n`) - towar uczty z niczego | **TAK** (C7, Mend: odjac zakupione) |
| 2 | zb58:27 | BK `BannerKingsComponent.GiveFood` :53-65 - zywnosc z niczego bandom hersztow (< 10 zywnosci) | **POMIAR** (C6), decyzja po liczbach (zablokowanie = bandy glodne, dezercja do puli wyrzutkow) |
| 3 | zb58:27 | ujscia BK `Demand.FinancialCompromise`, `BKCourtierBehavior.DrainGold`, `BuyGoodsAction` (reszta E5/A8) | **POMIAR** (C6); kod po liczbach |
| 4 | zb58:57 (E5/A7) | gracz: hearth x4 z niczego za akcje wrogosci wobec wsi (`VillageHostileActionCampaignBehavior` :556); zwrot ceny warsztatu przy utracie z wojny (`ChangeOwnerOfWorkshopAction` :17); NavalDLC Coastal Guard Edict | **TAK** - do pozycji 8.18 (dochody gracza; to te same zasady) |
| 5 | zb58:101 (E7/W2) | `MapVillageLore.SessionStart` przed `McmSettings.Apply` - tylko linia startu | **TAK** (C5, 1 linia - przeniesc wywolanie) |
| 6 | zb58:166 | tekst `VilHalfempty` nieuzywany | NIE (Z.5 sprzatanie) |
| 7 | zb58:196, :294 | BKROT `bearded_god` (Norvos) z wiara `HuesOfTrios` (Tyrosh) - nazwa wiary | NIE (dane cudzego moda, kosmetyka; podmiana wiary w biegu = ryzyko doktryn); juz w ERRORS A7 |
| 8 | zb58:226 | `VillageBook.StateOf` - nazwa najezdzcy zamrozona; tolerancja T1 | NIE (kosmetyka dymka) |
| 9 | zb58:254 | `Measure169c` szuka Bliznakow po nazwie "Twins", CrossingLaw i E7/B po id | **TAK** (w C8) |
| 10 | zb58:384 | `HideoutPurge`: SwitchToMenu z tiku menu czekania | NIE - to wzorzec gry ([G PlayerArmyWaitBehavior.cs:189], SiegeEventCampaignBehavior.cs:266); zakaz CLAUDE.md dotyczy opcji menu |
| 11 | zb58:384 | `HideoutPurge` catch gasi `_pending`; statyki nie czyszczone | **TAK** (C1) |
| 12 | zb58:384 | `WorldPace` 0-4% vs `MarchPace` 5% (skrajne wartosci suwaka) | NIE (Jeff nie uzywa skrajnych; Z.5) |
| 13 | zb58:384 | komentarze "168 dni" (`Rations.cs:16`, `SlowMuster.cs:10`, CS `Fabula.cs:77`) | NIE (Z.7) |
| 14 | zb58:416 | nazwy terenu `{=!}Forest` w grze nieangielskiej | NIE (gra po angielsku) |
| 15 | zb58:416 | `TerrainEase._lastAuditDay` miedzy kampaniami | **TAK** (C1) |
| 16 | zb58:452 | BK `AiDecisionTracePatches` SetMove* nie wpinaja sie w 1.4.8 | NIE (ERRORS A7 3-5: tylko sledzenie BK; straznicy BK w innych klasach dzialaja) |
| 17 | zb58:452 | `EnterDebt` w osadzie zostawia wpis partii w `_orders` [NightMarch.cs:1496] | **TAK** (C1 - martwy wpis; usunac przy `Order == null`) |
| 18 | zb58:452 | `RememberOrder` nie pamieta drogi ani portu | NIE teraz - POMIAR w tescie ("rozkaz po switcie" vs przed snem dla partii morskich); kod tylko gdy rozjazd |
| 19 | zb58:483 | linia `AiNightCamp` liczy niedoczepionych czlonkow armii jako samotnych; GT PeaceOnly bez licznika | NIE (Z.4 logi) |
| 20 | zb58:508 (N1b) | **`DoMendOne` zdejmuje miekki surowiec dokladnym przedmiotem (`AddToCounts(p.Item, -n)`), a sprawdza kategoria (`CountInInventory`)** [K SmithMenu.cs:1064 - :1090/:1093 i :1126/:1129; wzorzec dobry: `Recipes.Take` :644] - przy samych odmianach (inna skora/len) material nie schodzi albo stos idzie pod zero | **TAK** - pasuje do 8.9 ("kowadlo gubi welne i skory"); zamiana na `Recipes.Take` |
| 21 | zb58:534 (K3) | brama zakupu `NeedForUpgrades + lack` vs `NeedByCategory` | NIE (zakup tylko z luzu K2) - obserwowac ksiege koni |
| 22 | zb58:560 (N1) | (1) lawa naprawcza gracza i kwatermistrz Spoils placa kowalom do KASY MIASTA, a N1 - do kiesy ludu | **TAK** (jedna regula - pasuje do 8.8/8.9) |
| 23 | zb58:560 | (2) dwie definicje "w uzyciu" (`NeedBuckets` vs `FitBest`); (3) zastepcza sztuka `AiAnyMeleeWhenShort` nie naprawiana | NIE (refaktor bez dowodu skutku) |
| 24 | zb58:560 | (4) `TroopSelfMend` naprawia caly obity sprzet ludzi gracza, takze lup ponad potrzebe | **JEFF** (rada: zostawic - Twoj material, Twoj wybor) |
| 25 | zb58:587 | ksiega koni E6/0 - "BK stadniny" niepodpiete | NIE (Z.4 log) |
| 26 | zb58:612 | "Las wsi (126)" bez mnoznika regulatora | NIE (Z.4 log) |
| 27 | zb58:634 | `TownCrafts` bez sufitu wydajnosci (dzis nie wiaze); stary opis naglowka | NIE (Z.7) |
| 28 | zb58:658 | **uprzaz po poleglych jezdzcach zostaje w zbrojowni AI na zawsze** (MenPurse jej nie sprzedaje) | **TAK** (C7 - jak konie K2: nadwyzka na polke miasta za zaplata; pasuje do 8.8) |
| 29 | zb58:658 | ksiega E6/0 bez zamowien zamkow w drodze (`GarrisonCarts`) i `CastleMarket`; zwrot zawroconego wozu poza `TownFolk`; zasieg kupcow prosta vs droga | ksiega i licznik - **TAK tylko jesli prog sprawdz_logi ich uzywa**, inaczej Z.4; zasieg - NIE |
| 30 | zb58:683 | **warsztat GRACZA typu kopalnia BK bez `BulkLineSkip`** (x10 metalu z niczego, teraz takze bez dymarki) [K WorkshopLaw.cs:114] | **TAK** (C7) |
| 31 | zb58:683 | log `StartStock` "szt. po 1.5 kg"; komentarz `ArmsScrap` "domyslnie wylaczony" | NIE (Z.4/Z.7) |
| 32 | zb58:2506 (A6) | **RC: pojmujacy bez bohatera (banda bez wodza, milicja, zaloga) -> `GiveGoldAction(gracz, null, 60%)` - zloto okupu gracza w nicosc** | **TAK + JEFF** (C7; rada: do kiesy bandy albo kasy osady pojmujacego - pasuje do 8.18 okupy) |
| - | zb58:997 (R19), :5488, :6253, :7598, :7821 | R19 zwolnieni bez sakiewki (projekt); okno Spoils celowo; `ResetWorld` obozu (= 8.11b); krok 5 demografii (pobor ze wsi - zakres E4, nie blad); lordowie/karawany w 3 osadach (NAPRAWIONE: [L] "Wozy w miastach" lordom 0, karawanom 0) | NIE / ujete wyzej |

Pozycje 4, 20, 22, 28, 32 naleza tematycznie do 8.8/8.9/8.18 (inne rozpoznania) - przekazac tam, zeby nie robic dwa razy.

---

## 9. ERRORS.md - klasyfikacja

| Wpis | Stan | Propozycja |
|---|---|---|
| A1-A4 | naprawione / zalatane bramami | NIE |
| A5 BKROT dwie latki bez celu | dalej przy starcie; w CS jest tez `DynamicPartySizeBattleSkipPatch` (ta sama wada) | NIE; dopisac BattleSkip do A5 |
| A6 DTE AmbiguousMatch | 1 raz na sesje [CS] (bylo 604) | NIE (pytanie O z PLAN: nie wylaczac); poprawic falszywa linie Mends (C5) |
| A7 BK 7 klas (w tym SetMove* 3-5) | bez wplywu poza klasa 7 | NIE; klasa 7 = B10b JEFF (rada: nie ruszac - zmienia wplyw i slawe AI) |
| CrossingLaw (projekt E7, nie w ERRORS) | petla AI martwa | **TAK** C8 |
| Nowe do dopisania | TournamentsXPanded loader (x2, nieszkodliwe), BK `CorrectPlayerEducation` (8.13a), MonoMod first-chance przy klimacie (nieszkodliwe) | wpis do ERRORS przy C5 |

---

## 10. Proponowane paczki etapu 8 z tej czesci (kazda: autor + recenzent, wylacznik MCM, wpis CHANGELOG)

| Paczka | Zawartosc | Wiel. | Wylacznik | NK |
|---|---|---|---|---|
| C1 porzadek stanu | 8.11a-c, poz. 11, 15, 17 | S | `StateHygieneEnabled` | nie |
| C2 karawany w miescie | 8.12a + pomiar postoju | S/M | `CaravanTownHourly` | nie |
| C3 wyspy | 8.12b | S | `IslandCaravanFix` | nie |
| C4 najemnicy swiata | 8.12e + pomiar | S | `MercClansRealSoldiers` | nie |
| C5 cudze mody drobne | 8.13a, d, e; poz. 5; tekst Mends DTE | S | `MendBkEducation` (reszta to log/1 linia) | czesc (Education - przy starcie postaci) |
| C6 pomiary | 8.15 + pomiary z C2/C4 + poz. 2, 3 | S | `LogEnabled` | nie |
| C7 drobne zrodla z niczego | poz. 1, 28, 30, 32 (i 4, 20, 22 - jesli nie wezma ich 8.8/8.9/8.18) | S | po kluczu na pozycje | nie |
| C8 CrossingLaw | rozdz. 6 + poz. 9 | S | `CrossingLawAi` (istnieje) | nie |

Kolejnosc: C6 (liczby) razem z C1/C5 (bez wplywu na ekonomie), potem C2/C4/C7 (zmieniaja przeplywy - mierzone w tym samym tescie
zbiorczym), C3, C8. Najwieksze ryzyko ekonomii: C2 (wiecej handlu karawan) - zmierzyc kasy miast, zysk karawan, ms/dobe.

## 11. Pytania do Jeffa (rada w nawiasie)
1. Karawany BK stoja w miescie ok. 3 doby zamiast kilku godzin (dlawik BKROT) - zdjac dlawik w miescie? (TAK)
2. 5 klanow najemnikow ROT: koniec 3-4 ludzi dziennie na druzyne z niczego bez sluzby i koniec wyrzucania wodza z druzyny? (TAK)
3. Okup gracza, gdy pojmal go ktos bez bohatera (banda bez wodza, milicja, zaloga) - dzis 60% znika; komu? (kiesa bandy / kasa osady)
4. Atuty BK z klasy 7 (B10b) - przywrocic? (NIE - zmienia wplyw i slawe AI w calym swiecie)
5. Samonaprawa Twoich ludzi naprawia tez lup ponad potrzebe (Twoj material) - zostawic? (TAK, zostawic)
6. Pasek gotowosci zbrojowni DTE (pytanie O) - dzis 1 wyjatek na sesje - wylaczac? (NIE)
