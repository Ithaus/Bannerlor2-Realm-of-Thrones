## m105-drobne - punkt 7 PRZEKAZANIE "Drobne" po tescie 102 (8 punktow, 21/22.10)

**Mody:** Armoury, CrashScribe (+ tools/sprawdz_logi.py, tools/autotest-2026-10-17/autotest-bitwa.ps1, bieg-bitwa.ps1) | **Galaz:** `w-toku/m105-drobne` (od n32 `77c17f3c` = WGRANIE 102)
| **Tor:** S/M bez gry (gry nie uruchamiano, nic nie wgrane, test/ i zapisy Jeffa nietkniete) | **Status calosci:** DO SPRAWDZENIA (zbudowane, nie testowane w grze).

| Pkt | Co | Stan | Klucz MCM | Commit |
|---|---|---|---|---|
| 1 | E5-8b: ludzie Gory pala wsie Dorzecza (bandy 90-130, cel "najszybciej spalona", Beric po pierwszej spalonej wsi) | ZROBIONE, zbudowane | `PrologueMountainRaiders`, `PrologueBericHunt` (bez zmian, bez nowych; podpowiedz Beric poprawiona - R4) | `c82c0469`, `2606f9f8`, `5de11a0e` |
| 2 | T-4: `DivideByZeroException` w `QuestPay8.ShownReward` (sprawa broni gangu) | ZROBIONE, zbudowane; po recenzji prefiks getterow nagrody i ilosci, ilosc w linii (R-1, R2) | `QuestGangAxesFix` (TAK, nowy) | `a7074fdf`, `2606f9f8` |
| 3 | Rabunek wioski pobocznej noca jako praca | juz w WGRANIU 102 (m103-drobne `3360ba69`, `5c30a953`) - bez kodu | `RaidCountsAsWork` (TAK) | - |
| 4 | Markietani - odziez i praczki | juz w WGRANIU 102 (m103-drobne `e31227d0`, `5c30a953`) - bez kodu | `SutlerRowClothing`, `SutlerLaundry` (TAK) + suwaki | - |
| 5 | L-8: zapis `arm_board` ponizej 16 KB | juz w WGRANIU 102 (m103-drobne `ea848ab0`, `19f6fcff`; format nb2) - bez kodu | - | - |
| 6 | KP-1: `SyncData.arm_drink` | juz w WGRANIU 102 (m103-drobne `4c9bb421`; slepa plamka narzedzia) - bez kodu | - | - |
| 7 | `DrillCampOnlyAi` domyslnie TAK + gen_mcm | juz w WGRANIU 102 (m103-drobne `0fa1695d`) - bez kodu | `DrillCampOnlyAi` (TAK; u Jeffa tez true) | - |
| 8a | CrashScribe: straznik zawieszen milknie przy zamykaniu gry (bez falszywych hang-*.log) | ZROBIONE, zbudowane; merge z drzewem autotestu bez konfliktow; DR-4 po recenzji R1 | - (diagnostyka) | `ed15bf82`, `5de11a0e` |
| 8b | Autotest: po QuitGame bez czekania 90 s na znany blad silnika | ZROBIONE w skrypcie (kopia w galezi - do przeniesienia); bieg-bitwa.ps1 z `-ExitQuickSec` (R1) | - (narzedzie) | `e8881baf`, `5de11a0e` |

Build: Armoury, CrashScribe, RealisticCaptivity, GrandTourney, ForgeView - kod 0, 0 bledow (Armoury `f59abf54`, CrashScribe `e61a9520`); gen_mcm Armoury 2276
ustawien (+1: QuestGangAxesFix); `sprawdz_narzedzie.py` OK. Grupa progow nowa: `m105drobne` (DR-1..DR-4); zmienione: `m104e5` (E5-8c nowy, E5-9 nowy warunek).
**Po recenzji (22.10, sekcja "Recenzja - poprawki" na koncu):** Armoury `a2c0283c` (md5; kod 0, 0 bledow, 7 starych ostrzezen), CrashScribe BEZ zmian (`e61a9520`),
RealisticCaptivity / GrandTourney / ForgeView bez zmian; gen_mcm Armoury 2276 (tylko podpowiedz Prologue Beric Hunt); `sprawdz_narzedzie.py` OK.

---

### Pkt 1. Ludzie Gory nie spalili zadnej wsi (E5-8b)

**Po ludzku (dla Jeffa):** w tescie 102 ludzie Gory wyszli w pole w dniu 155, ale nie spalili ani jednej wsi. Mieli po 43-45 ludzi, a nasz rabunek "osada po
osadzie" liczy, ile czasu taka banda potrzebuje, zeby przeszukac i spalic jedna osade wsi (ok. 250 ludzi): przy 28 ludziach (tyle zostalo po walce z milicja)
- ok. 20 dob. Beric dopadl ich wczesniej. Teraz bandy sa duze (90-130 ludzi, jak oddzialy Gregora z ksiazki) - jedna osada w ok. 4-6 dob, wybieraja wies, ktora
spala najszybciej i zdaza do konca prologu, omijaja wsie z silna milicja. Beric rusza dopiero, gdy splonie pierwsza wies (Ned wysyla go po skargach - jak w
ksiazce), najpozniej w dniu 168. Duza banda chodzi wolno (gra karze bande bez wodza za ludzi ponad limit partii) - to tez zgodne z ksiazka: ciezki oddzial grabiezcow.

**Problem (log 1 `Armoury-2026-10-21_14-29-20.log`, prolog 230):** E5-8b NIE - "spalone wsie 0, powrot do domow linii 0". Linie 90610-90611 (d155): bandy 43 i 45 ludzi;
banda 1 zaczela palic Barleyfield i zostala rozbita w d156.6 ("Estate Retinue from Crossed Elms", linia 91765); banda 2 stracila 17 ludzi w walce z milicja i od d157
do d172 stala przy Ruby Ford (`ROT_town4_village3`, stan `BeingRaided`) z 28 ludzmi; R1 doby 108808-108819: "trafieni 12.47 ... uderzen 1" = 28 x 0.5 x 0.89;
linia 101164: Beric (67 ludzi, wyruszyl sztywno w d160 z odleglosci 528 jedn.) rozbil ja w d172.6 - krok niedokonczony, wies nigdy Looted, wiec nigdy "SPALONA".
Watpliwosc z rozpoznania (doba 108817 "trafieni 0, nowych 1") wyjasniona: sesja sie nie zerowala - postep 12.47/dobe przy uderzeniu co 1/20 kroku (ok. 12.5
czlowieka) czasem nie domyka uderzenia w dobie, a "nowych 1" to inny rabunek (Decide liczy kazdy rabunek AI w spustoszeniu, takze z milicja w walce).

**Przyczyna (kod):** nie blad logiki, tylko rozmiary. `DistrictRaid` (E7/R1): krok = `max(1, floor(N / RaidDetachmentMen 200))` osad = 1 osada, praca = ok.
`DistrictSettlementPeople` (250) ludzi, tempo `DevastationPerRaiderDay` 0.5 x N x f x u na dobe; `VillageState.Looted` dopiero po pelnym kroku (Win), a
`Prologue.RaiderTick` liczy "SPALONA" tylko przy Looted. `RaidMinMen 20 / RaidMaxMen 50` -> 12-20 dob na osade. Do tego `BericStep` ruszal w d160 bez wzgledu na stan.

**Zmiana:**
- `Prologue.World.Raiders.cs`: `RaidMinMen 90`, `RaidMaxMen 130`, `RaidDrawMin 60` (mniej z puli okregu Harrenhal - banda nie powstaje, linia "NIE powstala"; ludzie ta sama
  droga co dotad: `SpawnBand` -> `OutlawLaw.RaidDraw`: pula, potem domy wsi okregu, wies nie schodzi ponizej 50 hearth).
- `PickRaidTarget(mp, self, day, out est)`: dotad najblizsza wies Normal; teraz ta, ktora banda spali najszybciej: droga (odleglosc / (Speed / 1.2 h x 12 h marszu
  na dobe); Speed z kara gry za ludzi ponad limit 20, dno 1) + doby kroku `DistrictRaid.StepEstimate(wies, zdrowi, fid)` (ta sama arytmetyka co krok; rabunek gry -
  2.5 doby). Kolejnosc: zdazy do d186 i milicja <= 0.6 x zdrowi, potem zdazy z silna milicja, potem nie zdazy (najszybsza). Okreg wypalony (StepEstimate: 0 dob)
  pominiety - "Looted" bez osady nie jest spalona wsia. Filtry jak dotad (Dorzecze, Normal, nie cel drugiej bandy, nie spalona przez nas w 20 dniach).
- Linie "pierwszy cel" (narodziny) i "nowy cel" dostaja szacunek: `szac. N dob na osade (osada po osadzie | rabunek gry, M zdrowych), droga ok. X dob, milicja K
  [, NIE ZDAZY do d186] [, SILNA MILICJA], kandydatow C`.
- `Prologue.World.Beric.cs`: `BericCallBy = 168`; w fazie 0 od d160 wezwanie dopiero przy `_mmBurned > 0` albo w d168 (bez band w polu - jak dotad, koniec "brak Ludzi
  Gory"). Dzien wezwania `_brCall` w zapisie (czwarte pole `wbr=`; stary zapis bez pola - wezwanie liczone od nowa). Na druzyne czeka `BericLate` (5) dni od wezwania
  (dotad od d160). Linia "Beric: rusza" dostaje `[wezwanie dD: po spaleniu <wies dN> | d168 bez spalonej wsi, spalone wsie Ludzi Gory K]`; linia doby "przed (od d160 po
  pierwszej spalonej wsi, najpozniej d168)" / "wezwany dD, czeka na druzyne".
- `sprawdz_logi.py` grupa `m104e5`: **E5-8c** (INFO) - ludzie band przy narodzinach i szacunki z linii celu, liczba "NIE ZDAZY" / "SILNA MILICJA"; **E5-9** - rusza w d160-173
  i (pierwsza spalona wies <= dzien ruszenia albo dzien >= 168) (dotad d160-165; log 102 daje teraz NIE - dokladnie ten blad); brak linii przy logu >= 174 dob - NIE.
  Testy syntetyczne: spalona d162 / Beric d162 TAK, spalona d170 / Beric d168 TAK, spalona d170 / Beric d164 NIE.

**Ryzyko / co sprawdzic (kontrola calosci):**
- Ludzie: z domow okregu Harrenhal (ROT_town32) schodzi ok. 180-260 ludzi zamiast 88; wracaja w d188 przez `FreeFolkRaids.Home` do puli okregu zrodla (zabici nie
  wracaja). Gdy okreg nie da 60 - banda nie powstaje (E5-8 NIE z linia "NIE powstala" - wtedy obnizyc RaidDrawMin).
- Wiecej spalonych wsi w Dorzeczu (szacunek 2-5): blizny wojny ZC7, uchodzcy, jency E4/9, glod spichlerzy - te same reguly co kazdy rabunek R1 (zgodne z ksiazka).
- Predkosc: banda bez bohatera ma limit 20 (DefaultPartySizeLimitModel), kara gry 1/(N/20) - 1 -> ok. dno 1 (ok. 10-15 jedn./dobe). Pierwsza wies 16-22 jedn. od
  Harrenhal - ok. 2 doby drogi; kolejne 20-40 jedn. - 2-4 doby. Gdy banda nie zdazy do zadnej wsi przed d186 - linia "NIE ZDAZY" (rabuje i tak). Lordowie Dorzecza
  latwiej dogonia wolna bande (w tescie 102 przez 15 dob zaden nie zaatakowal; swoi nie bija swoich - korona Clegane - bez zmian).
- Jedzenie: banda 110 ludzi je jak wojsko (m96 BandFood): w rabunku dostaje ze spichlerza okregu (DistrictRaid.Eat), w marszu bierze z magazynow wsi w promieniu dnia
  drogi; glod po 2 dobach = 10% x brak odchodzi do puli. Sprawdzic w linii "Bandy jedza" i w ludziach band na liniach doby.
- Beric (67 ludzi) moze przegrac z banda 100+ (w ksiazce tez). Beric startuje teraz pozniej (po spaleniu) - przy odleglosci ok. 500 jedn. (test 102) dojdzie ok. 12 dob
  pozniej, wiec moze nie zdazyc przed d188 - "Beric: KONIEC - koniec prologu" (bez progu na zwyciestwo Berica).
- Pokoj prologu bez zmian: klan bandycki jest w stalej wojnie z kazdym krolestwem (GetShallowDiplomaticStance), zadnej wojny krolestw.
- Zapis: `wbr` czwarte pole - odczyt `Dbl(a, 3, -1)` (stare zapisy bez pola). Bandy juz w polu ze starego zapisu zostaja male (20-50), nowy wybor celu dziala od nastepnego celu.
- Koszt: `StepEstimate` (tylko odczyt, `Plan`) dla ok. 40-60 wsi Dorzecza tylko przy zmianie celu (bez celu - co godzine, jak dotad petla, teraz z Plan) - pomijalny.
- Uboczna obserwacja (poza zakresem): druzyna Berica ma frakcje "lys" i startuje 528 jedn. od Dorzecza.

---

### Pkt 2. T-4 - `ERROR in QuestPay8.ShownReward: System.DivideByZeroException`

**Po ludzku:** na tablicy ogloszen jedna sprawa "Gang Leader Needs Weapons" miala nieczytelna nagrode, a rozmowa o niej wywrocilaby sie. Gra zle liczy srednia
cene siekier: dodaje ceny po jednej sztuce z kazdej polki i dzieli przez liczbe WSZYSTKICH siekier na polkach - przy naszych tanich siekierach i pelnych polkach wychodzi
0 (albo kilka denarow), a potem dzieli przez to zero. Teraz przywodca gangu liczy prawdziwa srednia cene siekiery z polek miast. Klucz: **Quest Gang Axes Fix** (grupa
"Quest rewards"), wylaczony = jak gra.

**Problem:** log 1, linie 97763-97766 (doba 108815, d168): stos `get_RequestedWeaponAmount_Patch1` <- `get_RewardGold_Patch1` <- `QuestPay8.ShownReward_Patch1`;
linia tablicy "Kurz the Steward, gang leader - Gang Leader Needs Weapons - ? - 11 days", "nagroda nieczytelna 1".

**Przyczyna (dekompilacja 1.4.8, `GangLeaderNeedsWeaponsIssueQuestBehavior`):** `CalculateAveragePriceForWeaponClass` (:208-228) robi `num += ItemValue` (raz na element)
i `num / num2` gdzie `num2 += Amount` - suma cen sztuk / suma sztuk, dzielenie calkowite. Konstruktor (:174-187) dodaje klase tylko przy wyniku > 0; pusta lista ->
`GetRandomElement` = default -> `_averagePriceForItem = 0`, `_requiredWeaponClassIndex = -1`. Getter `RequestedWeaponAmount` (:47-58) ma straznik `3 + avg == 0`
(literowka gry, nigdy prawda) -> `20000 / 0`. `QuestQty28.OldAvg` przy `avg <= 0` zwracal 0 (podmiana dzielnika) - ten sam wyjatek. Indeks -1 wywrocilby tez konstruktor
zadania przy przyjeciu (`_canBeRequestedWeaponClassList[-1]`, :506). Uwaga do rozpoznania: zero bierze sie nie tylko z pustych polek - glowna droga to dzielenie
calkowite (siekiery 8-52 d, polki z wieloma sztukami). Gdy nie zero, srednia gry to kilka denarow, a ilosc z QuestQty28 (20000 w starej monecie / srednia) - setki siekier.

**Zmiana:**
- Nowy `Armoury/src/QuestGangAxes.cs`: postfiks konstruktora `GangLeaderNeedsWeaponsIssue(Hero)` (Harmony `com.jeff.armoury.m105gangaxes`, W KAMPANII z
  `ArmouryBehavior.OnSessionLaunched` po QuestQty28, raz na proces): nowa sprawa dostaje srednia WAZONA ceny siekier 1H z polek miast (suma ItemValue x sztuki /
  sztuki, te same przedmioty i ta sama petla co gra, >= 1); polki bez siekier 1H - srednia Value towarowych siekier 1H z definicji; nic - 1. Zepsuta sprawa (srednia <= 0
  albo indeks poza lista) - takze indeks 0. Sprawy z zapisu: przy starcie sesji naprawiane TYLKO zepsute (poprawna zostaje - gracz mogl slyszec ilosc).
- `QuestQty28.OldAvg`: przy sredniej <= 0 najpierw `QuestGangAxes.RepairLate` (sprawa sprzed latki / z kluczem wylaczonym przy tworzeniu), potem jak dotad.
- Nagroda: `500 + srednia x ilosc` (QuestPay8: czesc stala to praca, reszta towar po cenie) - teraz "srednia x ilosc" to naprawde koszt siekier. Ilosc liczy dalej QuestQty28.
- **Recenzja R-1 (22.10, `2606f9f8`):** prefiks `GetterPre` na `get_RewardGold` i `get_RequestedWeaponAmount` sprawy (`AccessTools.DeclaredPropertyGetter` - metody
  zadeklarowane w klasie sprawy, ta sama Harmony `com.jeff.armoury.m105gangaxes`): srednia <= 0 -> `RepairLate` PRZED cialem gettera. Powod: IL 1.4.8 `get_RewardGold` =
  `ldc.i4 500; ldfld _averagePriceForItem; call get_RequestedWeaponAmount; mul; add` - pole lezy na stosie, zanim getter ilosci (QuestQty28.OldAvg -> RepairLate) je
  naprawi, wiec pierwszy odczyt nagrody zepsutej sprawy (wiersz tablicy `NoticeBoard.Fill` -> `RewardOf` i kolejnosc wpisow) liczyl 500 + 0, po skali QuestPay8 (K=500)
  sama praca; karta wpisu i nastepne otwarcie byly juz dobre. Bez f28 (klucz `HistQuestQtyOldCoin` NIE albo ceny historyczne wylaczone) naprawy przed `20000 / 0`
  nie bylo wcale (DivideByZero w tekscie rozmowy). Zepsuta sprawa przy kluczu TAK zostaje tylko po wlaczeniu klucza w MCM w trakcie gry (`RequireRestart = false`) albo
  po potknieciu postfiksu konstruktora - rzadko. Wersja jednej linii w `NoticeBoard.RewardOf` (propozycja recenzji) nie objelaby luki bez f28.
- **Recenzja R2:** linia sprawy dostaje `| ilosc siekier Q, nagroda gry (bez skali) R` - getter `RequestedWeaponAmount` (refleksja) i `QuestPay8.RawReward` (teraz
  `internal`, bez skali RewardShown) po zmianie pola, w try; `?` - getter rzucil (nazwa wyjatku w nawiasie) albo nagroda nieczytelna.
- Podobne dzielenia w `QuestPay8` sprawdzone: `Rate()` dzieli przez `PartyMen()` (>= 1), `ExchangeEnter` przez `__3` (wczesniej `__3 <= 0` -> return); reszta to pomiary
  czasu. W zadaniach gry to jedyne dzielenie calkowite przez pole (rozpoznanie: grep `TaleWorlds.CampaignSystem.Issues`); dzielenia QuestQty28 przez `Value` (>= 1).

**Linie logu:**
- Start sesji: `Bron gangu (T-4, klucz Quest Gang Axes Fix TAK|NIE): latka konstruktora GangLeaderNeedsWeaponsIssue(Hero) WPIETA (...), prefiksy getterow nagrody i ilosci N/2 [(brak / blad)] | sprawy broni gangu z zapisu N, naprawione M.`
- Sprawa (najwyzej 40 na sesje): `Bron gangu (T-4): NAPRAWIONA sprawa|nowa sprawa <wodz> w <miasto> (nowa|zapis|pozny odczyt) - srednia ceny siekier gry X [(zero: ...), indeks klasy
  I -> 0] -> Y d (srednia wazona polek (S szt. w T miastach) | polki bez siekier 1H - srednia z definicji (K rodzajow)) | ilosc siekier Q, nagroda gry (bez skali) R | w sesji: nowe ..., naprawione ...`

**Progi (grupa `m105drobne`):** DR-1 ERROR QuestGangAxes / QuestPay8.ShownReward = 0; DR-2 linia startu WPIETA (po recenzji takze "prefiksy getterow nagrody i ilosci 2/2",
gdy linia je wymienia) w kazdej sesji z kluczem (NIE - INFO); DR-3 sprawy - kazda srednia po zmianie >= 1, po recenzji R2 takze ilosc siekier >= 1 i nagroda gry czytelna
(linie sprzed recenzji - sama srednia; brak spraw - INFO "T-4 niesprawdzone", recenzja R3; mediana gry -> nasza). Log 102: DR-1 NIE (1) - baza. Testy syntetyczne TAK/NIE w obie strony.

**Ryzyko / co sprawdzic:** zmienia sie ilosc siekier i nagroda TYLKO nowych spraw broni gangu (realna srednia zamiast kilku denarow - mniej siekier, nagroda pokrywa ich
koszt); sprawy z zapisu - tylko zepsute. Latka na konstruktor - zadna inna DLL w Modules nie odwoluje sie do tej klasy (skan nazw UTF-8 i UTF-16 wszystkich
`Modules/*/bin/Win64_Shipping_Client/*.dll`: tylko Armoury); postfiks addytywny. Klucz
wylaczony - konstruktor bez zmian, OldAvg bez naprawy (dawny stan). Sprawa rzadka (1 na 230 dob) - w autotescie zwykle DR-3 INFO.
- Po recenzji: prefiks getterow - jeden odczyt pola przez refleksje na wywolanie gettera tej jednej sprawy (pomijalne); Harmony przebudowuje getter ilosci razem
  z transpilerem QuestQty28 (`Il` przypisuje `Swaps`, nie dodaje - liczby bez zmian); klucz NIE - prefiks wychodzi od razu. Rekurencji nie ma: `Repair` wpisuje srednia
  >= 1 przed odczytem getterow do linii, wiec zagniezdzone wywolanie nie naprawia drugi raz.
- Linia sprawy czyta getter ilosci juz w postfiksie konstruktora: QuestQty28 zapisuje wtedy notatke ilosci i stosunek cen (raz na sprawe) w chwili utworzenia zamiast
  przy pierwszym odczycie w rozmowie - ten sam stan polek co nasza srednia. Linia "Ilosci zadan (f28): zadanie przyjete" pokazuje ilosc z chwili utworzenia (mnoznik
  trudnosci gracza moze sie potem zmienic - tylko tekst logu, nie ilosc zadania).
- **NIEPEWNE (recenzja R2) - ilosc 0 siekier:** `IssueDifficultyMultiplier` przed przyjeciem = Clamp(PlayerProgress, 0.1, 1); ilosc = (int)((20000 / OldAvg) x 0.1) = 0
  przy OldAvg > 2000 (mnoznik 0.2: > 4000, 0.3: > 5000). Szacunek z logu 102: K ok. 63, siekiery 1H 15-30 d -> OldAvg ok. 1000-1900 - zwykle 1 siekiera, 0 mozliwe
  (sprawa "przynies 0 siekier" za 500). Bez dowodu nie zmieniane - DR-3 pokaze ilosc. Przy 0 poprawka: postfiks `get_RequestedWeaponAmount`
  `if (QuestGangAxes.On && __result < 1) __result = 1;` (nagroda, teksty i zadanie czytaja ten getter - spojne).

---

### Pkt 3-7 - juz w WGRANIU 102 (m103-drobne; scalone w n31 `620847f7`, w n32 `77c17f3c`)

Sprawdzone w kodzie tej galezi (grep): `RaidCountsAsWork` = true (`Settings.cs`, `McmSettings.cs:4929`, `NightRest.cs` 9 miejsc, `ArmyRestDay.cs`); `SutlerRowClothing`,
`SutlerLaundry` = true (`McmSettings.cs:8977/8981`, `CampSutlers.cs` 14 miejsc); `NoticeBoard.Ver2 = "nb2"` (zapis 5053 B w logu 1, L-8 TAK, odczyt nb1 bez zmian);
KP-1 liczy `ERROR in SyncData.arm_drink/arm_rumours` (`sprawdz_logi.py` m98plotki); `DrillCampOnlyAi` = true (`McmSettings.cs:5933`; Armoury.json Jeffa: true; reszty kluczy
w pliku Jeffa nie ma - dzialaja domyslne). Nie sprawdzone w grze (z testu 102): pkt 3 (WP-7 TAK przy 0 rabunkach gracza), pkt 4 (MK-8/MK-9 INFO - 0 uzyc), pkt 5 odczyt
starego zapisu nb1 (tylko test refleksja w m103). Nic nie dopisano (CLAUDE.md 8.3) - propozycje testu nizej.
**Recenzja R6:** w raporcie dla Jeffa pkt 3 (rabunek wioski pobocznej noca jako praca) i pkt 4 (markietani - odziez, praczki) = **NIESPRAWDZONE W GRZE** - w progach 102
przeszly tylko formalnie (WP-7 TAK przy "linii 0, sesji z rabunkiem 0"; MK-8/MK-9 INFO przy 0 zakupow i 0 najec). Zaden plik `armoury-*.flag` (17 nazw) ich nie
wywoluje - test w autotescie wymaga nowego kodu (poza zakresem).

---

### Pkt 8a. CrashScribe - falszywe hang-*.log przy wyjsciu z gry

**Po ludzku:** po kazdym wyjsciu z gry CrashScribe zostawial plik "zawieszenie gry", bo przestawal slyszec bicie serca gry, ktora sie juz zamykala. Teraz wie, ze gra
sie zamyka, i nic nie pisze. Prawdziwe zawieszenia w trakcie gry (takze przy konczeniu kampanii i zapisie) dalej beda zapisane.

**Problem:** hang-2026-10-21_15-18-25 / 15-27-20 / 15-35-43: "main thread silent 61 s, no managed frames, outside a campaign"; session-2026-10-21_15-28-42.log:2030
"WYJSCIE: QuitGame" 15:34:41, SUMMARY, hang 15:35:43; rgl_log_32920: Saving options 15:34:42.983 -> Managed Interface deleted 15:34:43.544 -> 0xC0000005.

**Przyczyna:** `Watchdog.Loop` nie wiedzial o zamykaniu; po `OnSubModuleUnloaded` nie ma `OnApplicationTick` (bicie), po 60 s poza misja - `Suspend` i raport.

**Zmiana:** `Watchdog._closing` (volatile) i `Watchdog.Closing(why)` - linia `Straznik zawieszen: gra sie zamyka (<why>) - koniec czuwania, bez raportu zawieszenia (...)`,
`Loop` konczy watek przy fladze, przed `Dump` dodatkowy warunek. Wolane z `SubModuleMain.OnSubModuleUnloaded` (pierwsza linia) i z `ProcessExit`. `Module.FinalizeModule`:
`Game.OnFinalize` -> zapis `BannerlordConfig` -> `OnSubModuleUnloaded` - zawieszenie w konczeniu kampanii albo zapisie nadal daje raport. Bez klucza (diagnostyka).

**Prog:** DR-4 (`--grupa m105drobne <Armoury-*.log> --crashscribe <session-*.log>`), po recenzji R1 (`5de11a0e`): bieg autotestu (linia `AUTOTEST: WYJSCIE: QuitGame`)
- po niej linia "Straznik zawieszen: gra sie zamyka" (brak - NIE: stara DLL CrashScribe-at) i 0 blokow `# GAME HANG` po QuitGame i po linii zamykania; bez QuitGame
(gra Jeffa) - po linii zamykania 0 blokow; bez obu linii - INFO; zawieszenia w trakcie gry - INFO z liczba. Dotad: po linii zamykania 0, brak linii - INFO - na logach
102 (kazdy `# GAME HANG` po QuitGame i po SUMMARY: session 14-29-18 :8283/:8314, 15-19-57 :2267/:2297, 15-28-42 :2030/:2061) stara DLL przechodzila bez NIE. Kotwica
NIE na SUMMARY: `OnSubModuleUnloaded` pisze linie zamykania PRZED `Scribe.Summary()`, uchwyt UnhandledException wola Summary bez Closing, sesja bez bledow konczy sie
"Session ended with no errors.". Testy: 3 logi 102 NIE, sztuczny (QuitGame, linia, SUMMARY) TAK, bez obu linii INFO, gra bez QuitGame z linia TAK / z GAME HANG
po linii NIE, GAME HANG miedzy QuitGame a linia NIE.
Uwaga: przy 8b hang-*.log po QuitGame nie powstanie niezaleznie od 8a (skrypt ubija gre kilka-kilkanascie s po QuitGame, straznik pisze po 60 s ciszy; test 102: QuitGame
15:34:46, wywrotka silnika 15:34:43.5, hang 15:35:43) - "zero hang-*.log" w biegu z 8b nic nie dowodzi; dowodem jest linia zamykania (AutoFlush, pisana w
`OnSubModuleUnloaded` przed "Managed Interface deleted", wiec przed ubiciem) i jeden bieg z `-ExitQuickSec 0` (plan krok 3).

**Ryzyko:** po wyladowaniu modulow zawieszenie silnika nie zostawi hang-*.log - to znany blad silnika (docs/ERRORS.md: 0xC0000005 przy wyjsciu), widac go w rgl_log_errors.

**DLL autotestu (CrashScribe-at):** zmiany w `Watchdog.cs` i `SubModuleMain.cs` scalaja sie z drzewem `zb/n22-at` (`4143bde5`) BEZ konfliktow (sprawdzone `git merge-file`
na plikach LF; build scalonego CrashScribe-at na n22-at - kod 0). Wersja AT ma w `Dump` dopisek `Autotest.NoteOffThread("ZAWIESZENIE ... QuitHint")` - przy fladze `Dump`
w ogole nie biegnie, wiec po zamykaniu nie bedzie tez linii "ZAWIESZENIE" w autotest-*.log. Trzeba zbudowac nowa DLL: n22-at + merge kandydata z m105-drobne
(np. `test/n103/CrashScribe-at-n103.dll`) - `CrashScribe-at-n101.dll` tego nie ma (po recenzji R1 DR-4 da na niej NIE "stara DLL?" - zamierzone). Nowej metody
w `Autotest.cs` nie trzeba (wariant 8b bez DLL). Recenzja nie zmienila CrashScribe - do CrashScribe-at przenosi sie tylko `ed15bf82` (Watchdog.cs, SubModuleMain.cs).

---

### Pkt 8b. Autotest - bez 90 s czekania po QuitGame

**Po ludzku:** po kazdym biegu autotestu gra wychodzi, silnik w ok. polowie wyjsc wywraca sie przy sprzataniu (znany blad gry, tak samo u Jeffa), a skrypt czekal
90 s, zanim ja zamknal. Teraz zamyka ja od razu, gdy widzi ten znany blad - ok. 70-80 s mniej na bieg, ok. 4 min na test koncowy. Zwykla gra Jeffa bez zmian.

**Zmiana (tylko skrypt, `tools/autotest-2026-10-17/autotest-bitwa.ps1` - w tej galezi KOPIA pliku z repo glownego, md5 bazy `9181beb4` = kopia w test/):**
- parametr `-ExitQuickSec` (5; 0 = jak dotad);
- `Get-ExitMarker`: koniec `rgl_log_<PID>.txt` tego biegu (ostatnie 256 KB, plik zapisany po starcie gry) ma "Managed Interface deleted." PO "Saving options..." (i po
  ostatnim "Starting new log file") - silnik zapisal opcje (safely_exited) i usunal czesc zarzadzana;
- w petli po QuitGame (przed regula 90 s): wywrotka w `rgl_log_errors_<PID>` (`Get-ExitCrash`) albo znacznik + proces zyje >= ExitQuickSec s -> `Stop-Game -Now` (od razu
  `Stop-Process`, bez `CloseMainWindow` i 30 s czekania), wynik `wyjscie-wisi` (jak po 90 s - nie wywrotka, werdykt bez zmian), notatka "znany blad silnika przy
  wyjsciu - zamkniety od razu, N s po QuitGame";
- skladnia sprawdzona parserem PowerShell (0 bledow); `Get-ExitMarker` / `Get-ExitCrash` sprawdzone na prawdziwym `rgl_log_32920` (TAK), brak pliku i stary plik (NIE).

**Do przeniesienia (glowna sesja):** plik z tej galezi (z drzewa roboczego - CRLF, jak oryginal) do repo glownego `tools/autotest-2026-10-17/autotest-bitwa.ps1`
(galaz `claude/bannerlord-rot-setup-o75kvo`) i do kopii w test/ (bieg-bitwa.ps1 wola test/autotest-bitwa.ps1). Wariant DLL (TerminateProcess w CrashScribe-at) odrzucony:
zamykanie procesu to zadanie skryptu autotestu, nie moda (zwykla gra Jeffa bez zmian). Uwaga (recenzja R-2 - poprawione zdanie): watki .NET w tle zyja dalej po
"Managed Interface deleted." ("There are no living managed objects" to rejestr obiektow silnika, nie koniec .NET) - straznik CrashScribe pisal jeszcze 60 s po nim
(session-2026-10-21_15-28-42.log:2061, hang-2026-10-21_15-35-43.log; rgl_log_32920 :11526-11528). DLL technicznie moglaby zamknac proces, ale ubicie przed
"Saving options..." grozi `safely_exited=0`, a po nim nic nie daje wzgledem skryptu.
**Do przeniesienia tez (recenzja R1, `5de11a0e`):** `tools/autotest-2026-10-17/bieg-bitwa.ps1` (kopia z repo glownego, md5 bazy `97d666b0`, CRLF): parametr
`-ExitQuickSec` (domyslnie -1 = nie przekazuj, skrypt ma 5; 0 = stare 90 s czekania) przekazywany w `@a` do autotest-bitwa.ps1; sciezka `$AT` bez zmian. Skladnia
sprawdzona parserem PowerShell (0 bledow).

**Ryzyko:** czyste wyjscie tez pisze "Managed Interface deleted." - ale proces konczy sie wtedy od razu (wszystkie 6 rgl_log z 21.10 mialy po znaczniku wywrotke);
ubicie po zapisie opcji nie psuje engine_config.

---

### Plan autotestu (po scaleniu do kandydata; DLL: Armoury, CrashScribe + nowa CrashScribe-at z n22-at; skrypty autotest-bitwa.ps1 i bieg-bitwa.ps1 z tej galezi)

1. **Prolog 230 dob (nowa kampania prologu):** `--grupa m104e5` (TYLKO na logu pelnego prologu - na logu z wczytania grupa daje falszywe NIE w E5-2/4/7/8/8b/9,
   recenzja R5): E5-8 TAK, **E5-8b >= 1 SPALONA wies do d186**, E5-8c ludzie band 90-130 i szacunki ok. 4-6 dob, **E5-9 TAK** (rusza po pierwszej spalonej albo od d168),
   E5-10 TAK; linia "ludzie Gory: koniec prologu - ... wraca do domow" w d188, jesli banda przezyla. `--grupa m105drobne <log> --crashscribe <session>`: DR-1 TAK,
   DR-2 TAK (WPIETA, prefiksy getterow 2/2), DR-3 TAK, DR-4 TAK. **Warunek T-4 (recenzja R3):** co najmniej jedna linia "Bron gangu (T-4): nowa sprawa" albo
   "NAPRAWIONA" (DR-3 TAK, nie INFO) - inaczej w raporcie "T-4 niesprawdzone" (DR-1 i DR-2 przechodza tez bez zadnej sprawy); to samo dla m102tablica T-4 = 0.
   **DR-3 NIE z iloscia 0 (NIEPEWNE R2):** dopisac postfiks ilosci >= 1 (pkt 2, Ryzyko) i powtorzyc - bez niego nie wgrywac.
2. **Wczytanie +20 i bitwy:** DR-4 TAK w kazdym biegu (po QuitGame linia zamykania - nowa CrashScribe-at; zero GAME HANG po QuitGame); w wynik.txt skryptu "wyjscie
   gry: znany blad silnika przy wyjsciu - zamkniety od razu, N s po QuitGame" (N ok. 5-20) albo "proces zakonczyl sie sam po QuitGame". "Zero hang-*.log" w tych
   biegach nic nie dowodzi o 8a (8b ubija gre przed 60 s straznika - recenzja R1).
3. **Jeden bieg sprawdzajacy 8a do konca (recenzja R1):** `bieg-bitwa.ps1 ... -ExitQuickSec 0` (stare 90 s czekania; krotki bieg, np. wczytanie +1 doba) - w
   session-*.log linia "Straznik zawieszen: gra sie zamyka" przed koncem, brak nowego bloku `# GAME HANG` i brak nowego hang-*.log "outside a campaign" po QuitGame
   (DR-4 TAK).
4. **Bez testu w autotescie (do raportu):** pkt 3 i 4 (rabunek noca jako praca, markietani) - NIESPRAWDZONE W GRZE (recenzja R6; flaga autotestu = nowy kod, poza
   zakresem). R-1 (pierwszy wiersz tablicy przy sprawie zepsutej w trakcie gry) sprawdzalny tylko z kluczem Quest Gang Axes Fix NIE na starcie i wlaczonym w MCM -
   autotest tego nie robi; przy domyslnym TAK wystarcza DR-1, DR-2 (getterow 2/2) i DR-3.

---

### Recenzja - poprawki (22.10; weryfikacja R-1/R-2 "gra" i R1-R6 "zadanie"; gry nie uruchamiano, nic nie wgrane)

| Uwaga | Werdykt | Co zrobione | Commit |
|---|---|---|---|
| R-1 pierwszy odczyt nagrody zepsutej sprawy broni gangu = 500 + 0 (pole na stosie przed getterem ilosci); luka bez f28 - DivideByZero w rozmowie | PRAWDA (wiersz tablicy kosmetyczny; luka - rzadka wywrotka) | prefiks getterow RewardGold / RequestedWeaponAmount (pkt 2) | `2606f9f8` |
| R-2 zdanie "po Managed Interface deleted nie ma czesci zarzadzanej" falszywe | PRAWDA | tekst pkt 8b poprawiony; decyzja (zamykanie w skrypcie) bez zmian | ten commit |
| R1 DR-4 przechodzil na starej DLL; "zero hang-*.log" przy 8b nic nie dowodzi | PRAWDA | DR-4 kotwica QuitGame; plan krok 3 z `-ExitQuickSec 0`; bieg-bitwa.ps1 z parametrem | `5de11a0e` |
| R2 DR-3 nie sprawdzal ilosci (srednia zawsze >= 1) | PRAWDA; ilosc 0 - NIEPEWNE | ilosc i nagroda w linii sprawy, DR-3 ilosc >= 1; postfiks ilosci >= 1 tylko po dowodzie (plan krok 1) | `2606f9f8`, `5de11a0e` |
| R3 DR-1/DR-2 przechodza bez zadnej sprawy | PRAWDA | DR-3 INFO "T-4 niesprawdzone", warunek w planie krok 1 | `5de11a0e` |
| R4 podpowiedz MCM Beric "from day 160" niezgodna z kodem | PRAWDA | Settings.cs + gen_mcm ("once the Mountain's Men burn their first village (not before day 160, and on day 168 at the latest ...)") | `2606f9f8` |
| R5 E5-9 falszywe NIE po wczytaniu zapisu z d160-168 po spaleniu | PRAWDA | liczba z linii "Beric: rusza" (stary format - jak dotad); m104e5 tylko na logu pelnego prologu (plan) | `5de11a0e` |
| R6 pkt 3-4 przeszly w progach 102 tylko formalnie | PRAWDA (informacja) | raport: "niesprawdzone w grze" (pkt 3-7, plan krok 4) | - |

Testy narzedzia (logi sztuczne w scratchpad drobne/wer2): DR-3 - ilosc 2 TAK, 0 NIE, "? (DivideByZeroException)" NIE, nagroda "?" NIE, linia sprzed recenzji TAK,
a_zero ("-> 0 d (") NIE; DR-2 getterow 1/2 NIE; DR-4 jak w pkt 8a; E5-9 - wczytanie d162 ze spalonymi 2 (bez linii SPALONA) TAK, 0 spalonych d164 NIE, d168 TAK,
spalona d162 TAK, stary format bez spalonej d164 NIE, stary format ze spalona d162 TAK, syntetyki m105 (162/162, 170/168) TAK, log 102 dalej NIE (E5-9, E5-8b);
log 102 m105drobne: DR-1 NIE (1 - baza), DR-2 INFO, DR-3 INFO "T-4 niesprawdzone". Armoury: w DLL napisy nowej linii i podpowiedzi (sprawdzone w pliku).
**Ryzyko po recenzji:** nowe latki tylko w Armoury (2 prefiksy na getterach jednej klasy sprawy, Harmony `com.jeff.armoury.m105gangaxes`; zadna inna DLL w Modules
nie odwoluje sie do tej klasy - skan z pkt 2); CrashScribe bez zmian; odczyt getterow do linii logu (najwyzej 40 na sesje) - patrz pkt 2, Ryzyko.
