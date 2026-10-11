# KRYTYKA projektu etapu 4 (ksiega ludzi i zima) - 10.10.2026

Przedmiot: `scratchpad\e4\PROJEKT-ETAP4-KSIEGA-LUDZI-ZIMA-2026-10-10.md` (dalej **P:N** = linia projektu), przeczytany w calosci (526 linii).
Tylko odczyt: kod, gra, ustawienia i zapisy nietkniete. Jedyny nowy plik poza ta krytyka: skrypt pomiarowy `e4\krytyka_tools\culture_changes.py`.

Skroty dowodow:
- **K:** `scratchpad\e3\31m\Armoury\src` (galaz w-toku/e3-31m), **CS:** `...\31m\CrashScribe\src`, **CH:** `...\31m\CHANGELOG.md`;
- **G:** `scratchpad\dec32\cs_full` - pelna dekompilacja TaleWorlds.CampaignSystem z gry (linie VillagerCampaignBehavior 179/187 zgodne z `e4\dec\Villager.cs`);
- **dec:** `e4tools\dec`, **dec2:** `e4tools\dec2`, **BK:** `e4tools\bkfull`, **e4dec:** `e4\dec`;
- **SP:** `docs\STAN-PRAC.md`, **DOK:** `docs\DEMOGRAFIA-SILA-ROBOCZA-2026-10-05.md`, **PLAN:** `docs\PLAN-DO-KONCA-MODA-2026-10-09.md`;
- **log:N** = log 457 dob `kopia-okup1-457\Armoury-2026-10-10_10-38-24.log`; **csv-bk** = `kopia-okup1-457\2026-10-10_10-38-24\ludnosc-bk.csv`.

Wagi: **WYSOKA** (ksiega sie nie zamknie albo regula przeczy decyzji Jeffa - poprawic przed kodem danej paczki), **SREDNIA** (blad
w regule albo miejscu, latwy do naprawy w projekcie), **NISKA** (dokladnosc pomiaru, porzadek, redakcja).

---

## 0. Werdykt w skrocie

1. **E4/0 (pomiar) da sie zrobic tak, jak opisano** - wszystkie 9 celow okien istnieje z podana sygnatura, wszystkie miejsca w naszym kodzie
   sa pod podanymi liniami (tabela w rozdz. 1). Przed kodem 6 poprawek: nie dublowac nasluchow PeopleLedger (U8), dopisac linie jencow (U1),
   pozycja "zmiana kultury osad" (U3), okno odbitych jencow Innych (U6), osobne zrodlo siewu (U10), miejsce licznika zarazy (U9).
2. **Ksiega ludzi z rozdz. 2 sie NIE zamyka**: brak kont JENCY i WETERANI w rownaniu R0 (U1), "jedno miejsce powrotow" jest nieprawda
   - trzy drogi do ludnosci BK omijaja `LosersFlee.SendHome` (U2), ludzie wsi zaleza od kultury osady, ktora gra i mody zmieniaja (U3),
   kilka ujsc i zrodel bez nazwy (U15), dwie "pule" pod pobor (U17), stan ksiegi rozrzucony po 5 modulach bez wlasciciela rownania (U23).
3. **Niezgodnosci z decyzjami Jeffa:** Nocna Straz "dozywotnio" a 166 zwalnia braci do wsi (U5); odbici jency Innych zostaja zywymi
   ludzmi w bandzie (U6); "odbudowa 1 rok" zastapiona 85-90% w rok + 30 dob (U13); wolne rece we wzorze plonu w zlych jednostkach - "rekruci
   z sily roboczej" nigdy nie obnizaja plonu (U4); pobor gracza poza regula AI (U7); "ksiega w menu osady" dopiero w E4/11 (U21).
4. **Progi:** wszystkie progi PLAN maja linie logu, ale 3 trzeba doprecyzowac (U12 ludnosc - dopisana furtka -0.5%/rok i miara-tautologia,
   U13 spalona wioska - wymaga "bez kolejnego rabunku", U27 mediana zapasu - dwie rozne definicje); wojsko po E4/8 + E4/10 sprawdzane tylko
   120 dob (U20).

---

## 1. Sprawdzenie E4/0 w kodzie i dekompilacji (sygnatury, miejsca)

| Element projektu | Wynik | Dowod |
|---|---|---|
| W1 `Village.DailyTick()` | OK - `public void DailyTick()`; BK zastepuje ja prefiksem `bool` (domyslny priorytet 400, zawsze false), ktory czyta `HearthChange` raz, podloga 10, milicja; void-prefiks Priority.First biegnie przed nim, finalizer zawsze | dec Village.cs:223-240, :134; BK NotablePatches.cs:207-225 |
| model hearth = Default (dziedziczony) | OK - czynny model RBL `RealisticProsperityModel : DefaultSettlementProsperityModel` bez override hearth; latki BK (400), BKROT (200), ScorchedEarth (Last) dzialaja; wszystkie deterministyczne (drugie wywolanie da ten sam wynik) | log:515; dec RealisticProsperityModel.cs:11-13; BK VanillaModelTweakPatches.cs:1557+; e4dec BKROTPatch.all.cs:7876-7950 |
| W2 `Town.DailyTick()` | OK - `internal void`, `Prosperity +=` :637, `Militia +=` :638; w BK/BEE/BKROT brak latek tej metody (BK lata tylko `Town.FoodStocksUpperLimit`) | dec Town.cs:596-640; BK SettlementPatches.cs:88 |
| W3/W4 `AddVillagersToParty(MobileParty,int)` / `CreateVillagerParty(Village)` | OK - obie `private void`, `Hearth -= (n+1)/2` :179, :187 | G VillagerCampaignBehavior.cs:173-189 |
| W5 NavalDLC `OnDailySettlementTick(Settlement)` / `TryReinforceParty(FishingPartyComponent)` | OK - `private`, :186, :227; FishingPartyComponent dziedziczy po VillagerPartyComponent (wzor `find` z 108 dziala) | dec2 Fishing.cs:180-189, :207-230 |
| W6 `RaidEventComponent.Update(ref bool)` | OK - `internal override void Update(ref bool finish)`, `Hearth -= dmg x 0.5 x hearth` :174-175, zloto :176-186 | G RaidEventComponent.cs:160-190 |
| W7 BK `VillageDecreeManager.OnDailyTickSettlement(Settlement)` | OK - `private`, +0.5 :47; `StartDecree` tylko z konsoli (BannerKingsCheats.cs:4307, :4328), `ActiveDecree =` tylko w tej klasie | BK ...Economy.Layered\VillageDecreeManager.cs:27-57, :133 |
| W8/W9 ROT `OnMapEventEnded(MapEvent)` / `OnRaidCompleted(BattleSideEnum, RaidEventComponent)` | OK - `private`; okno 169c wpiete K:Measure169c.cs:104-105, :678-679; `IsBeyondTheWall(Vec2)` (CS NightKingCall.cs:296, :355) | dec ROTOthers.cs:327-341, :587-616 |
| hooki w naszym kodzie | OK - SubModuleMain.cs:133 (ScorchedEarth.ApplyAll), ArmouryBehavior.cs:395 (konstruktor), :713, :1501, :1535; OutlawLaw.cs:275, :289, :566; LosersFlee.cs:413, :452-462; ScorchedEarth.cs:74, :95-113; CampFever.cs:115-117; WinterBite.cs:108-119; Settings.cs:622 | odczyt plikow |
| proba zimy | OK - lato `soFar + 1`, jesien `Max(7, Rand(7,7))` = 7, zima 3-5 lat od ok. doby 8; zakres MCM `0..max(10, 4 x domyslna)` miesci 1 i 7 | K:WesterosClimate.cs:64-67, :88-89, :120; tools\gen_mcm.py:3-4 |
| inni pisarze `Village.Hearth` | OK z dopiskiem - w grze tylko: tick, tabory, rabunek, pobor wymuszony gracza, zadania gracza (Quest/Issue), incydenty, konsola; w BK tick + dekret; w BEE inwestycje (:27705); w BKROT nic. Zadania AI nie ruszaja hearth (zmiany sa w klasach Quest - OnTimedOut/Fail tylko u gracza) | G grep `Hearth [-+]?=`: VillageHostileActionCampaignBehavior.cs:619, IncidentEffect.cs:510/:541, Issues\*.cs, CampaignCheats.cs:1040 |

Wniosek: miejsca i sygnatury sie zgadzaja. Problemy E4/0 sa w zakresie (co mierzy i czy dubluje), nie w wykonalnosci.

---

## 2. Uwagi (od najwazniejszych)

### U1. WYSOKA - rownanie ksiegi bez kont JENCY i WETERANI
- **Problem.** R0 ma konta: dom, sluzba, wyrzutki, uchodzcy, niewolni, polegli (P:99-102). Reguly uzywaja jednak jencow (R4 "z jencow
  (zmiana stron)", R6 "jeniec wedlug I1", P:111-113) i puli weteranow (R16, P:123; E4/10). Czlowiek w lochu albo w karczmie nie jest w zadnym
  koncie, wiec rownanie regionu ("< 0.1%", P:382, P:438) nie moze wyjsc albo wyjdzie przypadkiem. Pula cial Innych (R13) tez nie jest nazwana
  jako ksiega NIE-ludzi (inaczej wight = czlowiek z niczego).
- **Dowod.** Jency w partiach 22 -> 2 048, w lochach 0 -> 52 (C-logi 4 "Bilans"; K:PeopleLedger.cs:285-296 liczy tylko stan); przeplywy: do
  domu 111/d, sprzedani 76/d (C-logi U9, U10). Gra: jency pokonanego ida wprost do `MemberRoster` zwyciezcy-NPC (zmiana stron)
  - G MapEvent.cs:1529-1567 + MapEventParty.cs:75-85 (`RosterToReceiveLootMembers` = `Party.MemberRoster`); bez odbiorcy (zwyciezca: tabor,
  karawana, milicja, patrol, bandy dla nie-bandytow - G DefaultBattleRewardModel `GetLootPrisonerChances`) sa usuwani (MapEvent.cs:1546),
  H3 lapie ich jako SrcFreed (K:LosersFlee.cs:1105-1113).
- **Poprawka.** R0: konta JENCY (z pochodzeniem, w partiach i lochach) i WETERANI (karczma miasta); pula cial jako osobna ksiega nie-ludzi
  (zasilana tylko z R3 w miejscach R13). Rownanie: `dom + sluzba + wyrzutki + uchodzcy + niewolni + jency + weterani = start + narodziny -
  zgony +- migracja`. E4/0: linia "Jency (E4/0)": wzieci w bitwach, wcieleni (zmiana stron w `LootDefeatedPartyPrisoners`), wypuszczeni
  i sprzedani (I1 - liczniki juz sa), na Mur, uwolnieni bez odbiorcy (H3 SrcFreed - licznik juz jest), straceni, stan; narzedzie T7 liczy
  jencow jak w C-logi 4.

### U2. WYSOKA - "jedno miejsce powrotow" to nieprawda: W4c, 166 i 179 omijaja `LosersFlee.SendHome`
- **Problem.** P:314 i tabela 5.1 (P:460): "PrisonerLaw, OverflowHome, CartTownExit ida przez SendHome - jedno miejsce". E4/7 (P:378) wymienia
  jako powroty tylko `ReleaseFrom` i `UpdatePopFromSoldiers`. W kodzie sa TRZY punkty dopisywania ludzi do domu, a dwa nie przechodza przez
  SendHome - przy 1/k (E4/3) i pochodzeniu (E4/7) przepelnienie i zwolnienia rycerzy zostana poza ksiega (ludzie do BK, z PL ani ze sluzby nie
  zejda).
- **Dowod.** `OverflowHome.SendHome` (inna metoda o tej samej nazwie) -> `ClanBudget.OverflowVillage` + `ClanBudget.ToVillagePop` (K:OverflowHome.cs:127-154);
  `GentryService.cs:647` -> `ClanBudget.ToVillagePop`; `ToVillagePop` -> `ToVillage` -> BK `UpdatePopFromSoldiers` (K:ClanBudget.cs:791-806);
  `ReleaseFrom` -> `ToVillage` (K:ClanBudget.cs:711-737). `CartTownExit.SendHome` to tylko rozkaz ruchu wozu (K:CartTownExit.cs:216-226).
  `LosersFlee.ToBk` osobno pisze do BK (K:LosersFlee.cs:464-495), tabor do hearth (:413), prosci przez `OutlawLaw.ReturnHome` (:456, OutlawLaw.cs:282-290,
  takze :777 - powrot z biedy).
- **Poprawka.** W E4/3, E4/7 i w tabeli 5.1 nazwac trzy punkty: `LosersFlee.SendHome` (z `ToBk` i :413), `ClanBudget.ToVillage` (166, W4c, 179)
  i `OutlawLaw.ReturnHome`; kazdy wola jedna funkcje ksiegi (U23) z regionem pochodzenia i przyczyna. E4/0: w linii "Partie"/"Hearth" licznik
  "powroty do ludnosci BK wedlug drogi" (H3, I1, 166, W4c, 179).

### U3. WYSOKA (utajona) - ludzie wsi = hearth x k KULTURY osady, a kultura osady sie zmienia
- **Problem.** R0 zaklada "k zamrozone na kampanie, wiec hearth wsi to po prostu ludzie / k" (P:101) i jednostke "1/k hearth kultury wsi" (P:104).
  k jest jednak zapisane na kulture, a odczytywane z biezacej `Settlement.Culture`. Zmiana kultury osady przelicza od razu wszystkich jej
  ludzi (z niczego / w nicosc) i zmienia wage czlowieka w hearth. Osada zdobyta przez Innych dostaje kulture `whitewalker`, ktorej nie ma w
  tabeli - jej ludzie znikaja z ksiegi (PeopleOf = 0) i wracaja przy odbiciu.
- **Dowod.** K:PopulationLaw.cs:170-179 (`_k[s.Culture.StringId]`), tabela bez whitewalker :39-71; BK `CultureData.Update` -
  `data.Settlement.Culture = dominantCulture` przy asymilacji >= 0.55 (BK Managers.Populations\CultureData.cs:200-206) i przy starcie
  (BK Behaviours\BKManagerBehavior.cs:171-179); ROT: `settlement.Culture = WhiteWalkers.Culture` (dec ROTOthers.cs:732, :749). k kultur od 3
  (nightswatch) do 4 655 (summer) - log:325 (wies przechodzaca z nightswatch do battania = x45 ludzi). W biegu 457 dob: **0 zmian kultury** osad
  (csv-bk, 365 tys. wierszy, skrypt `e4\krytyka_tools\culture_changes.py`) - dzis nie wystepuje, ale bieg 728 dob (Inni oblegaja od doby 728)
  i dluga gra Jeffa moga to wywolac.
- **Poprawka.** E4/3: zamrozic k na OSADE (wies: ludzie/hearth, miasto: stan) w `arm_population` przy kalibracji; reguly krain (prog wolnych
  rak, zima E4/1, niewola) czytac z kultury z dnia kalibracji albo z regionu, nie z biezacej kultury. E4/0: w linii Hearth pozycja "zmiana
  kultury osad: n (lista), ludzie tabeli +-X" - jesli kiedykolwiek > 0, to dowod.

### U4. WYSOKA - czynnik rak R9(b) w zlych jednostkach: pobor nigdy nie obnizy plonu
- **Problem.** R9(b): `L = min(1, ludzie w domu / ((1 - prog) x ludzie odniesienia))` (P:116), a prog = "20% mezczyzn" (R5, P:112). W ludziach
  (wszystkich) L spada dopiero, gdy z domu ubedzie 20% CALEJ ludnosci = 74% mezczyzn. R5 pozwala wziac najwyzej 20% mezczyzn = 5.4% ludnosci,
  a P3 blokuje pobor ponad prog - wiec plon od poboru nie spada nigdy, a od smierci dopiero przy katastrofie. To przeczy pamieci Jeffa
  ("Every recruit lowers the workforce ... village production drops while he serves", levy-from-the-fields.md) i decyzji 05.10 "Plon zalezy od
  liczby ludzi" (SP:347). Zmieniona tez bez uzasadnienia elastycznosc (DOK: ^0.5).
- **Dowod.** DOK:53 "prog f = 20% mezczyzn regionu (5.4% ludnosci) ... ponad prog plon x ((1-a)/(1-f))^0.5"; DOK:283 "Pula wolnych rak osady =
  5.4% jej ludnosci"; P:112, P:116.
- **Poprawka.** Liczyc w mezczyznach: `a = (pod bronia z regionu + polegli mezczyzni + mezczyzni uchodzcy) / (0.27 x ludzie odniesienia)`,
  `L = min(1, ((1 - a) / (1 - f))^e)`, f = 0.20 (zniwa 0.10), e = 0.5 (DOK; albo uzasadnic 1.0). W rozdz. 0 pkt 2 napisac Jeffowi wprost: w ramach
  wolnych rak rekrut plonu nie obniza (jego 05.10 "czesc byla wolna"), ponad - obniza; przy P3 = "tak" ponad progiem nikt nie pojdzie, wiec
  skutkiem poboru dla plonu sa tylko polegli i zniwa.

### U5. SREDNIA - Nocna Straz "dozywotnio" a 166 zwalnia braci ponad pule do wsi
- **Problem.** R14 "dozywotnio ... jeden pulap Strazy (166)" (P:121), decyzja Jeffa 05.10 "sluzba dozywotnia" (SP:359). Pulap 166 dla Strazy
  nie tylko blokuje werbunek, ale ZWALNIA ludzi ponad pule do ludnosci BK wsi.
- **Dowod.** K:ClanBudget.cs:633-643 (`b.Zero` = Straz: `over = _watchPoolMen > hyst x _watchPoolCap` -> `left` w ludziach), :651-656 i :691/:705
  (`ReleaseFrom` -> `ToVillage`).
- **Poprawka.** E4/9: dla Strazy zwolnienia = 0; nadmiar zatrzymuje tylko blokada werbunku i przyrostu zalog (juz jest: K:ClanBudget.cs:909,
  :1051-1079). Test E4/9: "zwolnieni Strazy 0" w linii 166.

### U6. SREDNIA - Inni: odbici jency wchodza do bandy jako zywi; E4/2 tego nie zamyka
- **Problem.** Jeff 07.10 pkt 1: "pojmany i odbity jeniec to polegly swojego regionu i cialo dla Innych, tylko za Murem i przy Murze"
  (SP:435-436); lista 05.10 "zablokowac Innym ... odbitych jencow" (SP:372-374). E4/2 (P:293-298) zamyka nekromancje, rajd, szablon, przejecie
  osady i pule wyrzutkow, ale nie odbitych jencow.
- **Dowod.** G MapEvent.cs:1529-1567 (`LootDefeatedPartyPrisoners`, wywolanie :1425): nie-bohater z lochu pokonanego idzie do
  `RosterToReceiveLootMembers` zwyciezcy, a dla NPC to `Party.MemberRoster` (MapEventParty.cs:75-85) - stad zywy jeniec w bandzie Innych.
  ROT czysci tylko `RosterToReceiveLootPrisoners` (= caly loch bandy, dec ROTOthers.cs:612-615). CS OthersGrowth.cs:23, :131 i CH:2867:
  "Zostaje: nekromancja z poleglych i odbici jency". Wchodza PRZED `OnMapEventEnded`, wiec okno 169c (W8) liczy ich w "reszcie" Wzrostu Innych.
- **Poprawka.** E4/2 (f): okno na `MapEvent.LootDefeatedPartyPrisoners(MBReadOnlyList<MapEventParty>, MBReadOnlyList<MapEventParty>)` - gdy
  zwyciezca to Inni: przybyli do bandy zywi = polegli regionu pochodzenia (R3), wight tylko za Murem / przy Murze, reszta w nicosc jako cialo
  spalone (0% - decyzja 07.10 pyt. 2). E4/0 W8: osobny licznik "odbici jency wcieleni do Innych" (stan bandy przed/po tej metodzie).

### U7. SREDNIA - zdjecie rekruta ze wsi tylko u AI i za bramka zlota
- **Problem.** E4/7: "rekrut -1/k z wsi werbunku (`LevyGold.ApplyInternalPostfix` ...)" (P:376). Ta latka dziala tylko na sciezce AI
  (`RecruitmentCampaignBehavior.ApplyInternal`) i tylko przy wlaczonym `RecruitGoldToSeller`; werbunek gracza idzie ekranem BK. Jedna regula
  gracz/AI zlamana, a wylaczenie ustawienia zlota wylacza ksiege ludzi.
- **Dowod.** K:LevyGold.cs:75 (`if (... !s.RecruitGoldToSeller ...) return;`); BK `DeduceManpower` wolaja: AI - RecruitmentApplyInternalPatch.cs:20,
  gracz - BK UI.Patches\RecruitmentOnDonePatch.cs:33; DOK:568 (krytyk kroku 1: "gracz omija ApplyInternal").
- **Poprawka.** Punkt zdjecia = postfiks na BK `MilitaryData.DeduceManpower(PopulationData, int, CharacterObject, Hero)` (BK MilitaryData.cs:151)
  - wspolny dla AI i gracza, z osada i liczba; do tego A5 (autowerbunek zalogi) i karczma osobno; bez zaleznosci od ustawien zlota.

### U8. SREDNIA - E4/0 dubluje istniejace nasluchy i linie
- **Problem.** `Measure40.RegisterEvents` slucha `MapEventEnded`, `OnTroopRecruitedEvent`, `OnUnitRecruitedEvent`, `OnTroopsDesertedEvent`
  (P:210-213) - tych samych zdarzen co PeopleLedger; "Krainy" (P:221) liczy to samo co wiersze regionow PeopleLedger (zaloga, milicja, pula,
  ludzie, obciazenie) tylko wedlug kultury; `hearth-regiony.csv` obok `ludzie-regiony.csv` (ma juz kolumne hearth); "Milicja" obok milicji
  w linii zalog 169c; `NoteHearth` w LosersFlee/OutlawLaw obok istniejacych licznikow hearth. Dwa liczniki jednego zjawiska = dwie liczby
  do godzenia (zasada "jedna zasada na zjawisko") i wiecej dotkniec zywego kodu w paczce "tylko log".
- **Dowod.** K:ArmouryBehavior.cs:689-692 -> K:PeopleLedger.cs:111-179; PeopleLedger.cs:250-276 (wiersze regionow), :34-37 i :437 (CSV z hearth);
  K:Measure169c.cs:281-297 (milicja i jej zmiana); K:LosersFlee.cs:413-415 (`_toVillageHearth`), :458 (`_toCommonHearth`); K:OutlawLaw.cs:568
  (`_inRaid`); DOK:565 ("Nie liczyc drugi raz tego, co juz licza 'Bitwy:' i 'Wyrzutki:' - czytac ich liczniki").
- **Poprawka.** Podzial wedlug frakcji (Straz, Wolni Ludzie, Inni) dopisac w nasluchach PeopleLedger; "Krainy" = wiersze PeopleLedger zgrupowane
  po kulturze z dnia kalibracji (U3); kolumny budzetu hearth dopisac do `ludzie-regiony.csv`; milicje z modelu dopisac do linii zalog 169c;
  w Hearth czytac liczniki LosersFlee/OutlawLaw x per (gdzie sa w hearth - wprost). Nowe linie tylko tam, gdzie pomiaru dzis nie ma (okna W1-W9).

### U9. NISKA - licznik zarazy w zlym miejscu
- **Problem.** "jedna linia po :117 ... `NoteDisease(st, attDead, defDead, ...)` - zmarli z kultura oddzialow i strona" (P:189). Na koncu
  `TickSiege` sa tylko sumy stron; kultury ani frakcji zmarlych nie ma, a linia Strazy/Wolnych Ludzi (P:222) wymaga frakcji.
- **Dowod.** K:CampFever.cs:84-89 i :104-110 (`Strike` per partia), :184 (`roster.AddToCounts(ch, -1)` - jedyne miejsce z postacia).
- **Poprawka.** `NoteDisease(mp, dead)` po kazdym `Strike` (:87-88, :109-110) albo `NoteDisease(ch, party)` przy :184 - wtedy kultura i frakcja sa znane.

### U10. NISKA - siew puli wyrzutkow i bieda w jednym zrodle
- **Problem.** Hook na :275 (`TakeCommoners`) liczy razem siew startowy i biede (P:190). Siew to ok. -3 281 hearth jednego dnia (DOK:574) -
  psuje T2/T3 tej doby i myli "bieda ok. -13 hearth/d".
- **Dowod.** K:OutlawLaw.cs:292-305 (Seed -> TakeCommoners); 108 liczyl siew osobno (`PeopleUnit.SeedBegin/SeedEnd`, e4tools\p_1ffe021.patch:381-391).
- **Poprawka.** Osobne zrodlo "siew puli" (flaga w `Seed`), poza ruchem doby.

### U11. NISKA - rozklad W1: "stala gry" bez mnoznikow modelu
- **Problem.** W1 liczy stala jako 4 / 1.2 / 0.2 ze stanu sprzed (P:200). Mnozniki `AddFactor` (cecha kultury, dekret podatkowy BK -15..+20%,
  blizna ScorchedEarth) mnoza tez stala, wiec czesc stalej trafi do "reszty modelu"; prog T3 "stala ok. 720/d" porownuje rozne rzeczy.
- **Dowod.** dec DefaultSettlementProsperityModel.cs:41-70 (baza pasma :45, `AddFactor` cechy :63-66); BK VanillaModelTweakPatches.cs:1557-1625; K:ScorchedEarth.cs:110.
- **Poprawka.** W prefiksie wolac model z `includeDescriptions: true` (571 wywolan na dobe) i rozdzielic: baza pasma, dodatki, mnozniki; w linii
  "stala gry (baza)" i "mnozniki modelu".

### U12. SREDNIA - prog "ludnosc rosnie 0.2-0.8% rocznie" po cichu oslabiony
- **Problem.** Rozdz. 4 (P:432): w ciezkiej wojnie prog dotyczy "przyrostu naturalnego", a netto moze byc do -0.5%/rok. To zmiana progu
  "Skonczone, gdy" (PLAN:146) bez slowa Jeffa; do tego "przyrost naturalny" to wynik wzoru R1 z korytarzem [-1.3; +0.8], wiec jego prog
  sprawdza parametry, nie swiat. Liczby niespojne: rozdz. 1 "0.2-0.8% = 290-1 150/d" (P:58), test E4/4 "250-600/d (0.2-0.5%/rok)" (P:333) -
  250/d to ok. 0.17%/rok.
- **Dowod.** P:58, P:333, P:432; PLAN:146; 52.5 mln x 0.2% / 364 = 288/d, x 0.5% = 721/d.
- **Poprawka.** Prog etapu na NETTO (d28-d364 biegu konca etapu); w tescie E4/4 (bez smierci w ksiedze) netto = przyrost, wiec 290-1 150/d;
  furtke "w wojnie nizej" przeniesc do rozdz. 6 jako informacje dla Jeffa z liczba (polegli 0.16-0.25%/rok, DOK L3).

### U13. SREDNIA - "spalona wioska wraca po roku": decyzja Jeffa 1 rok, projekt 85-90% w rok + 30 dob
- **Problem.** Jeff 09.10 (4): "ODBUDOWA SPALONEJ WIOSKI: 1 ROK (nie 2-3)" (SP:760). Projekt: powrot 0.7%/d po 30 dobach spokoju = ok. 90% po
  364 dobach od startu powrotu, nigdy 100% (P:115, P:346-347); prog etapu ">= 80% ludzi w domu po 364 dobach" (P:435); P4 (P:500-501) pyta,
  jakby decyzji nie bylo. Do tego prog jest niemierzalny dla wsi rabowanych ponownie (powrot wstrzymany).
- **Poprawka.** Powrot liniowy: dziennie `reszta / (364 - dni od konca rabunkow w regionie)` -> 100% w 364 dni od konca najazdow (30 dob
  spokoju liczone w te 364 albo osobno - do decyzji projektu, zgodnej z HR); prog: "wsie bez kolejnego rabunku w regionie przez 364 doby:
  >= 95% ludzi w domu i wioska poboczna odbudowana"; P4 przeformulowac na potwierdzenie, ze "rok" liczy sie od konca najazdow w okolicy
  (cytat SP:760).

### U14. SREDNIA - R8 (spustoszenie) i R15 (jency z rabunku) biora z tych samych ludzi
- **Problem.** R8: trafieni = zabici + las + uchodzcy (P:115). R15: jency z rabunku to ludzie zdjeci z napadnietej wsi (P:122; E4/9 P:404-405).
  Pojmanych nie ma w wynikach R8 - albo zdejma sie drugi raz, albo trafieni nie zamkna sie z kontrola linii (P:354).
- **Poprawka.** R8: czwarty wynik "pojmani" (tylko najezdzca z kultury z niewola, liczba z BK `ExecuteCapture`, odejmowana z trafionych);
  kontrola "trafieni = zabici + las + uchodzcy + pojmani"; E4/9 tylko przenosi pojmanych do JENCY (U1), nie zdejmuje drugi raz z wsi.

### U15. SREDNIA - ujscia i zrodla bez nazwy (ksiega nie zamyka sie w tych punktach)
- **Problem i dowod.**
  (a) R10: "tylu, ilu umiera, rusza w droge" (P:117) - dokad (uchodzcy wsi? wyrzutki? inny region)?
  (b) R15: Zelazne Wyspy "ubytek 5%/rok" niewolnych (P:122) - smierc czy wolnosc; czy 27 tys. na start to czesc 500 tys. Zelaznych (RK:348-349 liczy
      z nich) czy ludzie z innych krain (Jeff 05.10: "niewolni ... przenoszeni z ksiegi napadnietego regionu", SP:362)?
  (c) R16/E4/10: weteran niewynajety - zostaje w karczmie na zawsze, wraca do domu, umiera?
  (d) BK podloga 10 hearth (BK NotablePatches.cs:218-221) - z niczego; E4/0 ja mierzy (P:200), ale zadna paczka jej nie zamyka.
  (e) Gra rozwiazuje tabory i lodzie bez zwrotu ludzi do wsi: G VillagerCampaignBehavior.cs:253-259 (0 zdrowych), :268-272 (sierota), dec2 Fishing.cs:344-349
      (nadmiar lodzi po rozladunku) - E4/3 (108) obejmuje tylko tworzenie i uzupelnianie.
  (f) Partie ludnosci BK ("wedrowcy BK", K:Measure169c.cs linia "Towar wedrowcow BK"; C-logi Z6 32.5/d "inne") - R4 ich nie wymienia (P:111).
- **Poprawka.** Dopisac do regul: (a) do konta uchodzcow regionu najblizszego bez glodu (albo do wyrzutkow - jedna regula); (b) ubytek = zgon
  (R3 regionu niewoli), start = przeniesienie z regionow napadanych (RK) albo jawnie "z Zelaznych"; (c) po N dobach bez najmu do domu (R6);
  (d) E4/4: okno na BK `VillageDailyTicktPatch` cofa podloge (albo przypisuje ja przyrostowi R1) - licznik; (e) E4/3: `DestroyPartyAction`
  taboru/lodzi bez niszczyciela -> powrot +n/k do wsi; (f) E4/8: wedrowcy BK z ludnosci osady (zdjecie i powrot).

### U16. SREDNIA - pochodzenie (E4/7) bez jencow i bez przenosin
- **Problem.** Pochodzenie trzymane per partia (P:375) - nie ma go dla lochow partii i osad, wiec jeniec wcielony przez zwyciezce (G MapEvent.cs:1553-1577),
  sprzedany albo wypuszczony traci region. I1 "do domu" dzis = najblizsza osada kultury oddzialu (K:LosersFlee.cs:404, :529), nie region pochodzenia.
  Przenosiny partia-zaloga, "Donate troops", laczenie partii - tylko jako ryzyko "znosi sie na poziomie kultury" (P:385).
- **Poprawka.** Slownik pochodzenia takze dla `PrisonRoster` partii i lochow; kazde przeniesienie rosteru (okna 169c: `LeaveTroopsToGarrison`,
  `TakeTroopsFromGarrison`, K:Measure169c.cs:94-95; ekran druzyny - GarrisonArmory) przenosi udzial pochodzenia; po E4/7 I1 i H3 wysylaja do regionu
  pochodzenia, `LawHomePos` tylko dla ludzi bez pochodzenia (siew).

### U17. SREDNIA - dwa hamulce poboru i druga ludnosc rosnaca z niczego
- **Problem.** R0: "klasy BK/BEE nie sa ludzmi - to model checi" (P:102), ale pula BK dalej bramkuje werbunek (DeduceManpower, pula 10% klasy
  + 1%/d), a R5 + P3 dodaja drugi hamulec z ksiegi. BEE dopisuje klasom 0.22%/d i podlogi 50/300 z niczego. Jeff 05.10: "Jedna ksiega ludzi
  na region" (SP:345). Dwa liczniki tej samej sily roboczej moga sie rozjechac (dzis: BK wsi -23%, zamkow +58% przy hearth +66% - C-logi 2).
- **Dowod.** dec BetterEconomy...PopulationCampaignBehavior.cs:99, :116; DOK krok 7 ryzyko ("Pula poboru BK ... dalej jest bramka"); P:516 (odlozone).
- **Poprawka.** W R5 napisac, co steruje iloscia (ksiega: wolne rece), a co skladem (BK: klasa, notabl); BK manpower nie moze byc ostrzejsza od
  ksiegi bez slowa - linia "Pobor:" pokazuje, ktory hamulec zadzialal; odlozone dopasowanie BEE (P:516) dostaje prog liczbowy z E4/0
  (np. rozjazd ksiega/BK regionu > 30%).

### U18. NISKA - klimat (E4/1) i progi krain wedlug kultury osady
- **Problem.** Czynnik zimy "wedlug krain" po kulturze (P:118, P:275-276). Klimat to geografia: wies Dorne przejeta kulturowo przez Reach
  dostalaby pelna zime (U3). Dla jedzenia partii trzeba krainy pod partia, nie jej kultury.
- **Poprawka.** Czynnik z regionu (`OutlawLaw.RegionAt`) i kultury tego regionu z dnia kalibracji; partia - region pod partia.

### U19. NISKA - trzy definicje "pod bronia" i kolizja id krolestw z id kultur
- **Problem.** Obciazenie PeopleLedger = (zaloga + wyrzutki + bandy) / mezczyzni (K:PeopleLedger.cs:312); "Krainy" = partie rodow krolestw kultury
  + zalogi + milicje (P:221); R5 = pod bronia z regionu wedlug pochodzenia (P:112). Do tego w ROT id krolestw nie sa id krain: krolestwo
  `vlandia` = Zelazny Tron, `empire` = Dolina (K:CrownGifts.cs:37-39), a kultura `vlandia` = Westerlands, `empire` = Braavos (K:PopulationLaw.cs:46, :56).
- **Poprawka.** Jedna definicja (R5, z decyzja: milicja liczy sie do wolnych rak czy nie) w jednym miejscu, uzywana w linii, hamulcu i R9(b);
  w "Krainy" wiazac krolestwo przez `Kingdom.Culture` (obiekt), nigdy przez StringId krolestwa.

### U20. SREDNIA - wojsko po E4/8 i E4/10 sprawdzane tylko 120 dob
- **Problem.** Zalogi bez przyrostu z modelu (dzis +52 tys. w 457 dobach, log:123790) i najemnicy o 81-97% mniej (P:44) zmniejszaja wojsko
  liniowo w czasie; test E4/8 to 120 dob i "zalogi >= 85% biegu bazowego" (P:395). Jeff: "Wojsko zostaje ok. 108 tys. (skala 1:1)" (SP:329)
  i 09.10 C "mniej wojska AI NIE" (SP:771).
- **Poprawka.** Prog w biegu konca etapu (364 doby): wojsko lordow w wojnie 95-115 tys. i zalogi >= 85% bazowego w d364; w rozdz. 0 pkt 7
  liczba "zalogi w d364 ok. X tys. zamiast Y" z pomiaru E4/0 (przyrost z modelu x 364).

### U21. NISKA - "ksiega do sprawdzenia w menu osady" dopiero w E4/11
- **Problem.** Jeff 05.10 zapytal "gdzie to mozna bedzie sprawdzic?" -> "log 'Ludzie:', plik CSV regionow, pozycja w menu osady" (SP:347; B 1.1).
  W projekcie menu jest tylko w E4/11 (menu okregu, P:419).
- **Poprawka.** Prosta pozycja menu osady (napisy po angielsku: ludzie w domu, w sluzbie, wyrzutki, uchodzcy, polegli) razem z E4/7.

### U22. NISKA - pytanie P2 jest juz rozstrzygniete
- **Problem.** P2 (jency gracza z rabunku wedlug reguly AI, P:496-497). Jeff: jedna regula gracz/AI (zasada stala) i 07.10 pkt 6a "jency z
  rabunku biora tylko kultury z niewolnictwem ... ludzie zdjeci z napadnietej wsi - TAK" (SP:444-445).
- **Poprawka.** Decyzja projektu (jak AI), przeniesc do "Informacji"; zostaja 3 pytania.

### U23. SREDNIA - stan ksiegi w 5 modulach, brak wlasciciela rownania
- **Problem.** Konta trafiaja do roznych miejsc: miasta `t:` PopulationLaw (E4/4), uchodzcy `u:` Devastation (E4/5), spichlerz `f:` (E4/6),
  sluzba `s:` (E4/7), ciala `c:` Armoury albo CS (E4/2, P:300), wyrzutki OutlawLaw, niewolni BK (P:462). Rownanie R0 i progi "z niczego = 0"
  wymagaja jednego miejsca, przez ktore idzie kazde przesuniecie (jak ksiega pieniadza) - `PeopleUnit.Shift` (P:104) obejmuje tylko zapis hearth.
- **Poprawka.** Nazwac wlasciciela (np. PeopleLedger jako ksiega) z jedna funkcja `Move(region, z_konta, do_konta, n, przyczyna)`; moduly tylko
  ja wolaja; rownanie i linia "z niczego / w nicosc" licza sie tam. Wpisac to w R0 i w E4/3 (pierwsza paczka ze stanem).

### U24. NISKA - liczniki w zywym kodzie nie moga rzucac
- **Problem.** E4/0 dopisuje `Measure40.Note*` do OutlawLaw, LosersFlee, ScorchedEarth, CampFever (P:189-192), a "gra bez zmian" (P:41, P:177).
  `TakeCommoners` nie ma wlasnego try w petli: wyjatek po `v.Hearth -=` (:275), a przed `Add(region, Commoner, taken)` (:278) zgubilby ludzi.
- **Poprawka.** Kazde `Note*` z wewnetrznym try/catch i licznikiem potkniec (albo czytanie istniejacych licznikow - U8); w recenzji kodu
  sprawdzic, ze zadne `Note*` nie stoi miedzy zdjeciem a dopisaniem.

### U25. NISKA - diagnoza 112 i "brak latek" sprawdzone tylko w naszym kodzie
- **Problem.** `VillageTakings.HooksLine` uznaje sufit kiesy wsi za wylaczony, gdy na `Village.DailyTick` jest JAKIKOLWIEK prefiks - nasz void
  W1 da "jest" takze bez BK (P:258-259 zaklada, ze BK zawsze jest). "Dzis bez latek" (P:259-260) sprawdzono grepem w K i CS (BK/BEE/BKROT tez -
  potwierdzam), ale nie w RBL, ROT, Diplomacy, StrategicCampaignAI.
- **Dowod.** K:VillageTakings.cs:466-467 (`capOff = ti.Prefixes.Count > 0`).
- **Poprawka.** HooksLine liczy prefiksy `bool` spoza naszego Harmony id; linia startowa E4/0 wypisuje wlascicieli latek (Harmony.GetPatchInfo)
  na 9 celach.

### U26. NISKA - wlaczanie okien przy ladowaniu modulu
- **Problem.** "ApplyAll przy ladowaniu modulu" (P:186) - jesli wzorem ScorchedEarth.ApplyAll (K:ScorchedEarth.cs:120-121) okna nie zostana
  wpiete przy `PeopleMeasureEnabled = false`, wlaczenie w MCM nic nie da do restartu.
- **Poprawka.** Okna wpinac zawsze (koszt pomijalny), liczyc tylko przy `On`; linia startowa jak P:216.

### U27. NISKA-SREDNIA - "mediana zapasu >= rok" ma dwie definicje
- **Problem.** E4/0 T9 czyta linie T6 (P:253), w ktorej mediana dni zapasu liczy sie tylko z warowni "z ubytkiem", a "bez ubytku" sa poza mediana;
  E4/6 mierzy inaczej: "spichlerz okregu + Food warowni, w dniach potrzeby" (P:367). "Zadna warownia AI nie glodzi" - linia liczy wszystkie
  `IsStarving`, takze gracza.
- **Dowod.** K:PeopleLedger.cs:192-205 (FoodChange >= 0 -> "bez ubytku", poza mediana), :320 (`if (r.St.IsStarving) starving++`).
- **Poprawka.** Jedna definicja w narzedziu od E4/0 B: wszystkie warownie AI, "bez ubytku" = >= 364 dni (w medianie), gracz osobno; ta sama w E4/6.

### U28. NISKA - drobne
- Rozdz. 3 laczy 4.3 z E4/4 (P:146-149) - uzasadnienie dobre, ale 4.3 Jeff mial jako "zdecyd." srodek przejsciowy (PLAN:128): dopisac to w rozdz. 6
  "Informacja" (przez E4/0-E4/3 gra Jeffa dalej rosnie +57%/rok).
- PLAN 4.12 "zalezy od 4.11" (PLAN:137), projekt robi 4.12 przed 4.11 - uzasadnione (P:466), zostawic z jednym zdaniem w tabeli paczek.
- Krainy/Straz/Inni z 169c (niewolni BK, W8) biegna tylko przy `ClanIncomeBookEnabled + StableD` (K:Measure169c.cs:31) - w linii E4/0 napisac
  "brak danych 169c", gdy wylaczone.

---

## 3. Ksiega ludzi - zrodlo i ujscie kazdego czlowieka (zestawienie)

| Przeplyw | Regula | Stan w projekcie |
|---|---|---|
| narodziny / zgony naturalne wsi, miasta | R1, R2 | nazwane |
| dom -> sluzba (ochotnik, szablon, A5, milicja, karawana, tabor) | R4 | nazwane; punkt zdjecia u gracza brak (U7), wedrowcy BK brak (U15f) |
| sluzba -> polegli (bitwa, zaraza, stracenie) | R3 | nazwane; licznik zarazy bez frakcji (U9) |
| sluzba -> dom (rozbici, zwolnieni, rozwiazani) | R6 | nazwane; 2 z 3 drog poza SendHome (U2); tabory/lodzie rozwiazane przez gre bez zwrotu (U15e) |
| sluzba -> wyrzutki (dezercja) | R6, R7 | nazwane |
| sluzba -> jency -> sluzba / dom / niewolni / Mur / polegli | R4, R6, R15 | **brak konta JENCY** (U1), pochodzenie jencow (U16) |
| dom -> wyrzutki -> dom / bandy | R7 | nazwane; siew osobno (U10) |
| dom -> uchodzcy -> dom / zgon | R8 | nazwane; powrot nie "1 rok" (U13) |
| dom -> pojmani (rabunek) -> niewolni | R8, R15 | **podwojne zdjecie** (U14) |
| glod: dom -> zgon / w droge | R10 | "w droge" bez celu (U15a) |
| niewolni -> ? | R15 | ubytek 5%/rok bez celu (U15b) |
| sluzba -> weterani -> sluzba / ? | R16 | **brak konta WETERANI** (U1), niewynajeci bez celu (U15c) |
| polegly -> cialo -> wight | R13 | pula cial jako ksiega nie-ludzi - dopisac (U1); odbici jency Innych (U6) |
| zmiana kultury osady | - | **ludzie z niczego / w nicosc** (U3) |
| podloga 10 hearth BK | - | z niczego, niezamkniete (U15d) |

---

## 4. Co sie zgadza (sprawdzone, bez uwag)

- Liczby rozdz. 1 z logu: k kultur (log:325), regiony ponad progiem 6 z 227 i 0 glodnych warowni (log:123833), pula startowa 6 562 (log:151).
- Dekret BK Growth tylko z konsoli; BKROT hearth nie pisze; BEE tylko inwestycje; RBL dziedziczy hearth z Default (rozdz. 1 tabeli).
- Okno zalog z modelu (`TickGarrisonChangeForTown`) tylko dodaje - zero ze strony modelu wystarczy na prog "przyrost bez werbunku = 0" (dec
  GarrisonRecruitmentCampaignBehavior.cs:97-108).
- BK juz liczy pasze w sniegu (pol) i na pustyni (cala) - regula E4/1 (c) "chyba ze BK juz to liczy" jest poprawna (BK BKPartyConsumptionModel.cs:200-211).
- Kolejnosc jednostka -> przyrost -> spustoszenie (P:465) i rachunek "bez 1/k wsie traca 17-20% hearth rocznie" (P:146-149) sa spojne z DOK krok 2-3.
- Numery 188-193 wolne (CH i SP - 0 trafien).

## 5. Proponowana kolejnosc poprawek projektu

1. Przed kodem E4/0: U8 (bez dublowania), U1 (linia jencow), U3 (pozycja kultury), U6 (okno odbitych jencow w W8), U9, U10, U24, U26.
2. W rozdz. 2 (reguly) przed E4/3: U1 (konta), U2 (trzy punkty powrotu), U3 (k na osade), U4 (wzor rak), U14, U15, U23 (wlasciciel ksiegi).
3. W paczkach: U5 (E4/9), U6 (E4/2), U7 i U16 (E4/7), U13 (E4/5), U18 (E4/1), U20 (koniec etapu), U21 (E4/7), U27 (E4/0 B / E4/6).
4. Rozdz. 6: P2 skreslic (U22), P4 przeformulowac z cytatem SP:760 (U13), dopisac informacje U12, U20, U28.
