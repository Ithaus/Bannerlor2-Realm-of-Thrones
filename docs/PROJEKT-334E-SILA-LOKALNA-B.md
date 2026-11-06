# Projekt 334e (wariant B, pełny): zasada siły lokalnej

Gałąź `noc/334e-lokalna-sila` (od 99a05f27, po scaleniu `noc/335-ochrona-wodza`, HEAD 98a7b10b). DLL testowe: `C:\Users\GAME\bannerlord-autotest\test\m334e`. Do gry Jeffa nic nie wgrywamy w ramach tego projektu.

**Zasada Jeffa (06.11):** „liczy się, gdzie armia jest w danej chwili, a nie ile ogólnie jest”. Lord, który wie (zwiadowcy, plotki), że armie wroga stoją daleko, łupi, rabuje i zdobywa zamki tam, gdzie ich nie ma, bo zdąży, zanim wrócą. Gdy nadchodzi armia wroga, oblegający ocenia bitwę: jeśli przegra, odstępuje, a jeśli wygra, kontynuuje. Załogi innych osad i armie daleko się nie liczą.

Projekt zastępuje trzy miary globalne jedną miarą lokalną **Z(cel)**: „kto z wrogów w polu zdąży tu przyjść, zanim skończymy”. Decyzje wojna/pokój (264/330, BK `BKDiplomacyModel`, Diplomacy) dalej mogą liczyć sumy królestw, bo to kanon i polityka, a nie wybór celu.

---

## 0. Co zastępujemy (z rozpoznania)

| # | Mechanizm globalny | Miejsce | Po 334e |
|---|---|---|---|
| G1 | Postawa BK Defensive/Aggressive całego królestwa z `CurrentTotalStrength` ×1.5 (Defensive = 0 opcji Besieger/Raider) | BK `BKDiplomacyBehavior.TickKingdoms`, nasze `WarFront334.Stance.cs` (334c) | wszystkie królestwa AI = **Neutral**; ostrożność/agresja **per cel** z Z (rozdz. 3.2) |
| G2 | Fantom `num10` w grze: `CurrentTotalStrength` celu (z załogami) ×0.25/×0.5 rozdzielone na każdą osadę | `DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction:223` i gałąź Besieger | **0** (transpiler), zamiast tego Z(cel) jako mnożnik (3.2) |
| G3 | SCA „wyczerpane” (wróg > swoi ×1.35 z sum), cele ×0.55; 334c `ScaOwn` = suma koalicji | `ScaNoBandits320.CalcPostfix:305`, `WarFront334.ScaOwn:410`, SCA `StrategicTargetScoreModel:78` | swoi i wróg = **siła w polu** w zasięgu 8.02 doby od naszych ziem; ×0.55 zdjęte dla celów w oknie |
| G4 | SCA głębia ×0.18 / ×0.55 i `NearbyEnemyLordStrength` ×0.25 (linia prosta, armia ×2) | `StrategicTargetScoreModel:45,49,92` | zdjęte, gdy reguła lokalna oceniła cel (nasza miara zastępuje, bez podwójnej kary) |
| G5 | 274 powściągliwość silniejszego (weto oblężeń poza celami frontu przy rF > 1.5) | `CanonWarBalance.Restraint.cs:21`, `CanonWarBalance.cs:211/648` | weto tylko gdy Z(cel) zamyka okno; **weto siedzib zostaje** (kanon) |
| G6 | 274/326c `MusterBlocked` (strona silniejsza/powściągana) | `CanonWarBalance.Levers.cs:77` | blokada tylko, gdy w zasięgu zbiórki nie ma żadnego celu w oknie |
| G7 | Brak odstępowania (odwrót SCA martwy: szuka lordów w `AllPartiesWithoutPartyComponent`), BK SIEGE-STICK trzyma do 24 h | SCA `StrategicCampaignAI145Behavior:319`, BK `ArmySiegePersistencePatches` | własny moduł oblężenia (rozdz. 4) |

Zostaje bez zmian: F334 jako miara pary królestw dla dźwigni ekonomicznych (W1/W2/W5, B5 najem, 326c-E), teatr B7/B9, zwrot 326b, weto siedzib, pokój/rozejm 264/330, odsiecz OB5 i 326c (C1–C3), odpoczynek armii, nocne obozy, 223 LordsAvoidDanger, F2 „szturm do końca”, 335 ochrona wodza.

---

## 1. Mapa sił w polu (FieldMap)

Nowy plik `Armoury/src/LocalFront334e.cs` (`internal static partial class LocalFront334e`).

### 1.1 Jednostka w polu
Jednostka = **armia** (wódz + doczepieni, siła = `GetTotalLandStrengthWithFollowers()` wodza, jak `ourStrength` gry; doczepionych nie liczymy osobno, R5) albo **samotna drużyna lorda** (`IsLordParty`, `Army == null`).

Nie liczymy: załóg (`IsGarrison`), milicji, karawan, wieśniaków, band, drużyn bez wodza, wodza w niewoli, drużyn `IsDisbanding`.

Drużyna stojąca w osadzie celu (`CurrentSettlement == cel`) nie wchodzi do Z tego celu, bo gra liczy ją już w `num6/num7`. Załoga i milicja celu są w ocenie gry (`num6`) i w module oblężenia (rozdz. 4) – innych osad nie liczymy nigdy.

Drużyna gracza i jego armia **są liczone** jako siła (to realna armia wroga lub sojusznika), ale **nie są sterowane** (rozdz. 6.4).

### 1.2 Czas dojścia
`dni(U, cel)` = droga z `AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(U, cel, …)` (ląd/morze, jak myśl AI i `WarFront334.InRange`) podzielona przez **prędkość własną jednostki** `U.Speed` (model prędkości gry: armia idzie tempem najwolniejszego, statki swoją prędkością), funkcja `WarFront334.Days` z prędkością U zamiast średniej. Gdy `U.Speed` ≤ 0 (postój, obóz nocny) – średnia `Campaign.EstimatedAverageLordPartySpeed` (3.36), bo postój nie trwa wiecznie.

Wstępny filtr bez kosztu: odległość w linii prostej / (najwyższa prędkość lorda na mapie × 24) > horyzont → jednostka na pewno nie zdąży, bez wołania `AiHelper` (droga nigdy nie jest krótsza od prostej).

### 1.3 Wiedza: zwiadowcy i plotki (uzasadnienie)
AI w grze widzi wszystko (rozpoznanie gry, p. 5) – brak mgły wojny. Jeff mówi, że wróg „ma zwiadowców, plotki”, więc **wiedza jest pełna, ale nieco spóźniona**. Przyjmujemy:
- Pozycje jednostek odczytujemy co **3 godziny gry** – to ten sam rytm, w którym gra odświeża zagrożenie osad (`PoliticalStagnationAndBorderIncidentCampaignBehavior.UpdateNearbyValues`, co ponad 3 h). Żadnej nowej stałej.
- Ostrożność zwiadowcy: wróg mógł się zbliżyć w czasie od odczytu, więc `dni_eff = dni − 3/24` (najgorszy przypadek: maszerował na nas od ostatniej plotki).
- Nie modelujemy fałszywych plotek ani „niewidzialnych” armii: to dałoby losowość bez podstawy w modelu gry, a gracz widziałby lordów idących na armię, której „nie widzieli”.

### 1.4 Z(cel, okno) – siła wroga przy celu
`Z(cel, W)` = Σ siła jednostek wrogich **dla atakującego** (`FactionsAtWarWith` atakującego, z sojusznikami koalicji wroga przez wspólne wojny), dla których `dni_eff ≤ W`.

Okno W (ile czasu potrzebujemy):
- **Oblężenie nowe:** `W = nasz_dojazd + 8.02` doby (`AverageSiegeDurationAsDays` gry; ta sama stała co `CanonWarBalance.DefendRangeDays`).
- **Oblężenie w toku:** `W = T_pozostało` (rozdz. 4.1).
- **Rabunek wsi:** `W = nasz_dojazd + T_rabunku`, gdzie `T_rabunku` = czas zniszczenia wsi z modelu gry dla naszej drużyny (punkty życia osady / obrażenia na godzinę z modelu rabunku, jak liczy `MapEvent` rabunku; DistrictRaid moda wpływa tym samym modelem). Gdy modelu nie da się odczytać – 1 doba (cykl dobowy gry) i linia „potknięcie”.
- `nasz_dojazd` = `dni(my, cel)` tą samą funkcją.

Jednostka już oblegająca coś innego (np. Robb pod Duskendale) liczy się normalnie po czasie dojścia – jeśli jest daleko, nie wejdzie w okno. Tak właśnie wynika z cytatu Jeffa.

### 1.5 O(cel) – nasza siła
`O = ourStrength` podany przez grę do `GetTargetScoreForFaction` (dla wodza z przyszłą armią gra dolicza już `PossibleArmyMembersUponArmyCreation`) **+** sojusznicy (ta sama frakcja lub koalicja z wojną z celem) z `TargetSettlement == cel` albo w `BesiegerCamp` celu, którzy dojdą przed pierwszym wrogiem z Z.

### 1.6 Stosunek q
`q = Z / O`. Wszystkie progi z istniejących liczb:

| Próg | Wartość | Skąd |
|---|---|---|
| okno otwarte | q ≤ 0.8 (= 1/1.25) | SCA `NearbyEnemyLordStrength > ours × 1.25` |
| oblężenie nowe zakazane | q > 1.0 | sztywniej niż odwrót, żeby nie zaczynać i zaraz nie uciekać (histereza) |
| rabunek zakazany | q > 1.25 | SCA, ten sam próg |
| odwrót z oblężenia | q_obl > 1.25 (my < 0.8 × wróg) | martwy odwrót SCA `TryRetreatFromBadSiege` (0.8) – przywracamy jego regułę |
| kontynuacja pewna | q_obl ≤ 0.8 | symetrycznie |
| obrona: przydział wystarcza | ≥ 1.2 × napastnik | 326c „odsiecz atakuje przy przewadze” |
| obrona: przydział się rozpada | < 1.0 × napastnik | histereza do 1.2 |

---

## 2. Wydajność i pamięć podręczna

- **FieldMap:** lista jednostek per frakcja, budowana leniwie przy pierwszym pytaniu w danym 3-godzinnym przedziale (klucz `(int)(CampaignTime.Now.ToHours / 3)`). Koszt: jedno przejście `MobileParty.AllLordParties` (~300).
- **Przyjazdy:** `Dictionary<(MobileParty, Settlement), float>` dni, ważne do końca przedziału 3 h. Liczone tylko dla celów, o które ktoś pyta (cele w zasięgu myśli AI), po filtrze linii prostej.
- **Z per cel:** posortowana lista (dni, siła) wrogów danego celu; dowolne okno W = suma prefiksowa z wyszukiwaniem binarnym, bez ponownego liczenia dróg.
- Licznik wywołań `AiHelper` i czas w ms raportowany w linii dobowej (6.5). Cel: średnio < 5 ms na godzinę gry, maks. jednej godziny < 50 ms (sprawdz_logi L12).
- Wyjątek w liczeniu: jak w `WarFront334.Ensure` – miara nieczynna do końca przedziału, gra działa po staremu (bez kary, bez premii), linia „potknięcie 334e”.

---

## 3. Wybór celów najazdu i oblężenia

### 3.1 Postawa: Neutral dla wszystkich królestw AI (G1)
`WarFront334.Stance.cs`: gdy `LocalStance334e`, `StanceApply` ustawia **Neutral** każdemu królestwu AI w wojnie (nie tylko stronom 274 i Wyspom), tym samym polem co BK (`SetPartyObjective` na drużynach, tylko tam, gdzie jest inna wartość). Postfiks na BK `TickKingdoms` już istnieje (`StancePostfix`) – zmienia się tylko reguła. Do tego przegląd raz na godzinę dla nowych drużyn (BK nadaje postawę przy `TickKingdoms` raz na dobę).

Pomijamy królestwo gracza (BK też je pomija; gracz decyduje sam).

Skutek: `FindBestTargetAndItsValueForFaction` nigdy już nie wyrzuca wszystkich opcji Besieger/Raider przez postawę (Żelaźni: 0 opcji od doby 109524). Rolę postawy przejmuje mnożnik per cel z 3.2, czyli **postawa partii wobec konkretnego celu** zamiast postawy królestwa. Inicjatywa (`SetInitiative`) wraca do domyślnej gry; ucieczkę samotnych lordów nadal robi 223 (lokalne, promień 60).

`CanonWarStance334c` zostaje jako klucz, ale przy włączonym `LocalStance334e` nie działa (linia startu pokazuje „334c: zastąpione 334e”).

### 3.2 Ocena celu: postfiks + transpiler (G2)
Nowy plik `LocalFront334e.Score.cs`.

1. **Transpiler** na `DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction` (klasa deklarująca, raz): oba wywołania `IFaction.get_CurrentTotalStrength` zamienione na `LocalFront334e.PhantomStrength(IFaction f)`, które przy kluczu `LocalPower334e` zwraca **0** (fantom `num10` = 0, bo `max(0, …)`), a przy kluczu NIE – oryginalną wartość. Transpiler sprawdza, że znalazł dokładnie 2 miejsca (kotwice ×2); inaczej się nie wpina i linia startu mówi „BRAK”.
2. **Postfiks** na aktywnym modelu (`PatchTarget.OfActive`, jak `SiegeRansom.Act.cs:664`) – priorytet niższy niż SiegeRansom, więc działa po nim. Strażnik `[ThreadStatic] _depth`, żeby SCA wołające bazę nie dostało mnożnika dwa razy. Mnożnik:

| Typ | q ≤ 0.8 (okno) | 0.8 < q ≤ próg | q > próg |
|---|---|---|---|
| Besieger, cel nie oblegany przez nas | ×1.2 | ×0.8 | **0** (próg 1.0) |
| Besieger, już oblegamy ten cel | ×1 (decyduje rozdz. 4) | ×1 | ×1 |
| Raider | ×1.2 | ×0.8 | **0** (próg 1.25) |
| Defender, Patrolling | ×1 (lokalne w grze) | ×1 | ×1 |

×1.2 i ×0.8 to dokładnie mnożniki postawy Aggressive/Defensive gry (`AiMilitaryBehavior.CalculateMilitaryBehaviorForSettlement`) – teraz wynikają z sytuacji przy celu, a nie z królestwa. To jest „premia tam, gdzie wroga nie ma, kara tam, gdzie jest silny”. Dodatkowo puste cele zyskują w samej grze, bo fantom przestał zawyżać obronę każdej osady.

3. **SCA (G3, G4):** postfiks działa po metodzie SCA (`StrategicTargetScoreModel`), więc gdy reguła lokalna oceniła cel, **dzieli wynik** przez mnożniki SCA, które zastępujemy, odtwarzając ich warunki tymi samymi helperami SCA (`StrategicAiHelpers`):
   - głębia ×0.18 (:45) i ×0.55 (:49) – zdjęte, gdy q ≤ 0.8;
   - `NearbyEnemyLordStrength` ×0.25 (:92) – zdjęte zawsze, bo zastępuje je q (bez podwójnej kary i bez liczenia armii ×2);
   - wyczerpanie ×0.55 (:78) – zdjęte, gdy q ≤ 0.8.
   - ForcePeace/WantsPeace (:98, ×0.25/×0.35) zostaje: to cel wojny (polityka), nie ocena celu.
   - Logistyka SCA zostaje: spójność −3.5 po 5 dobach w głębi, jedzenie < 3 dni.

### 3.3 Wyczerpanie SCA z siły w polu (G3)
`ScaNoBandits320.CalcPostfix` (:305), klucz `ScaLocal334e`:
- **wróg** = F334 (`ScaEnemy`, już: tylko wrogowie w polu dochodzący do naszych twierdz w 8.02 doby) – rozszerzone na **wszystkie** królestwa AI, nie tylko strony 274 i Wyspy;
- **swoi** = Σ siły **naszych jednostek w polu** (własne + koalicja) w zasięgu 8.02 doby od naszych twierdz – lustro F334, **bez załóg**; `WarFront334.ScaOwn` (334c, suma `CurrentTotalStrength` koalicji) przestaje działać przy tym kluczu;
- furtki lokalne SCA zostają (twierdze zagrożone ≥ 1/3 w promieniu 80, wsie złupione ≥ 3).

Wyczerpanie dalej może prowadzić do ForcePeace (decyzja o pokoju), ale cele w oknie nie dostają już ×0.55 (3.2).

### 3.4 Zbiórka armii, gdy jest okno (G6, R2)
- Gra już woła armię dla Besieger (`WillGatherAnArmy`) z `ourStrength` przyszłej armii – po zdjęciu postawy i fantomu sama ruszy na cel w oknie.
- `CanonWarBalance.Levers.cs:77` `MusterBlocked`: przy `LocalMuster334e` blokada „strona silniejsza/powściągana” zastąpiona: zbiórka zablokowana tylko, gdy w zasięgu `MaximumDistanceToCallToArmy` wodza **nie ma żadnego celu z q ≤ 0.8** dla siły przyszłej armii. Wyzwalacz „7 dób bez bitwy” zostaje.
- BK `FindArmyObjective` (B8, `WarFront334.Theatre.cs`): cel z q > 1.0 traktowany jak zawetowany → następny cel według reguły BK (mechanizm `BkVetoNextTarget334`). Inaczej BK zbierze armię na cel, który myśl AI zaraz odrzuci (R2).

### 3.5 Weto 274 lokalnie (G5)
`CanonWarBalance.Restraint.cs` `VetoPostfix`, klucz `LocalRestraint334e`: rodzaj „powściągliwość silniejszego” (`_restrain`) wyłączony – zamiast niego działa q. Weto siedzib (Winterfell, Riverrun i inne do Krwawych Godów), teatr B7/B9 i zwrot 326b zostają.

### 3.6 Armie też łupią (D3)
Gra nie daje Raider armiom ani „zbierz armię”. Klucz `ArmyRaid334e`, postfiks `AiHourlyTick` (ten sam co 4/5): wódz armii AI bez oblężenia, gdy żadne oblężenie w oknie nie ma wyniku > 0, dostaje ocenę `RaidSettlement` najlepszej wsi z q ≤ 0.8 (wynik = `GetTargetScoreForFaction(wieś, Raider, wódz, siła armii)` – ta sama formuła gry, bez nowej stałej). Gdy jest oblężenie w oknie, wygrywa oblężenie (wartość zamku > wieś w modelu gry).

### 3.7 Żelaźni przez morze
Bez wyjątków w regule: `AiHelper` zwraca drogę morską dla drużyn ze statkami, więc czas przeprawy wchodzi do `nasz_dojazd`, a Północ bez floty (326) dochodzi do zachodniego wybrzeża tylko lądem. Z(wieś na wybrzeżu) liczy lordów Północy, którzy dojdą lądem przed końcem rabunku. Przygotowanie przeprawy 334 (`IronbornCanonReave334`: jedzenie, naprawa statków w porcie) zostaje i działa przed oceną celu.

---

## 4. Oblężenie w toku: kontynuuj albo odstąp

Nowy plik `LocalFront334e.Siege.cs`, klucz `SiegeLocalRetreat334e`. Postfiks `CampaignEventDispatcher.AiHourlyTick` – w kolejce przed OB5 (`BesiegedRelief`, Low), F2 i Hold (Last), po wetach 274 (R1; dokładny priorytet ustawić według kolejki z `CanonWarBalance.Restraint.cs:12` i sprawdzić w linii startu).

### 4.1 Wejście (co godzinę, dla każdego obozu AI)
- `Obóz` = Σ siły drużyn w `BesiegerCamp` (jak gra przy oblężeniu: `GetInvolvedPartiesForEventType().EstimatedStrength`) + sojusznicy w drodze na ten cel, dochodzący przed odsieczą.
- `T_pozostało` = czas do szturmu z postępu oblężenia: gdy machiny gotowe / mury SiegeWalls2 przebite – 0.5 doby (szturm najbliższy); inaczej z tempa budowy `DefaultSiegeEventModel.GetConstructionProgressPerHour` i postępu murów SiegeWalls2; awaryjnie `max(0.5, 8.02 − doby_oblężenia)`.
- `Odsiecz` = Σ wrogich jednostek **nadchodzących**: `TargetSettlement == cel`, armia zbierana na ten cel (OB5/326c), albo odległość do celu zmalała od poprzedniego odczytu 3 h – i `dni_eff ≤ T_pozostało`. Stojąca w pobliżu armia, która się nie zbliża, nie wywołuje odwrotu („jak wiemy, że się zbliża”).
- `Załoga` = załoga + milicja celu (dołączą do bitwy z odsieczą – tylko tego celu).
- `q_obl = (Odsiecz + Załoga) / Obóz`.

### 4.2 Macierz decyzji

| Stan | Warunek | Decyzja |
|---|---|---|
| Szturm trwa (MapEvent szturmu) | – | nic (F2 „szturm do końca”) |
| Odsieczy brak | Odsiecz = 0 | **KONTYNUUJ**: ocena bieżącego oblężenia podniesiona tak, by była najlepsza (`SetBehaviorScore`), chyba że OB5 każe bronić własnej oblężonej twierdzy (OB5 działa później i wygrywa – zostaje). Naprawia O2/O3 |
| Odsiecz słabsza | q_obl ≤ 0.8 | **KONTYNUUJ** jak wyżej (O2, O3; regresja: Ashemark 8287/1951, Sarsfield, Deep Den, Bronzegate) |
| Strefa środkowa | 0.8 < q_obl ≤ 1.25 | **bez ingerencji** – gra (×4 trwającego oblężenia, `num17`) i Hold decydują; to histereza |
| Odsiecz przeważa, nie zdąży | q_obl > 1.25, ale przyjazd > T_pozostało | **KONTYNUUJ** (zdobędziemy przed nimi; wtedy bronimy się za murami) |
| Odsiecz przeważa, zdąży | q_obl > 1.25 i przyjazd ≤ T_pozostało, warunek 2 kolejne odczyty (2 h) | **ODSTĄP** (O1, O5, O6, O7; regresja: Stoney Sept, Rosby, Goldentooth, Red Keep) |

**Odstąpienie:** wódz (i każda samotna drużyna w obozie) dostaje `GoToSettlement` do najbliższej własnej twierdzy **leżącej dalej od odsieczy niż obóz** (drogą `AiHelper`), z oceną wyższą od każdej innej; ocena oblężenia celu zmniejszona do 0. Gra w `PartyHourlyAiTick` sama zwija obóz (`SetPartyAiAction`). Cel dostaje blokadę dla tej drużyny/armii na **1 dobę** albo do chwili, gdy q(cel) ≤ 0.8 przez 2 kolejne odczyty (anty ping-pong).

**Zgodność z innymi łatkami:**
- BK SIEGE-STICK (`ArmySiegePersistencePatches.Postfix`): prefiks Harmony na tę metodę BK – gdy drużyna jest w `_retreat334e`, BK jej nie trzyma (`return false`). Bez tego odwrót stoi do 24 h (D2).
- `SiegeWalls2.Hold`: pyta `LocalFront334e.RetreatOrdered(mp)` – przy odwrocie nie trzyma.
- Odwrót SCA `TryRetreatFromBadSiege` (martwy) zostawiamy martwym; naszego odwrotu nie dublujemy.
- 326c C1–C3 (odsiecz atakuje przy przewadze ≥ 1.2 × obóz): dotyczy strony **odsieczy** – nie zmieniamy. O4 (Duskendale: odsiecz 134% bez ataku) dostaje osobną diagnozę L8 – jeśli 326c tam nie zadziałało, to błąd 326c, poprawka w ramach tej paczki tylko po znalezieniu przyczyny w logu.

---

## 5. Obrona pustych ziem: kto wraca, kto zostaje

Nowy plik `LocalFront334e.Defence.cs`, klucz `LocalDefence334e`. Ten sam postfiks godzinny.

Zdarzenie: wróg rabuje naszą wieś albo oblega naszą twierdzę (dla oblężeń zostaje OB5/326c – przydział tylko dla rabunków i oblężeń, których OB5 nie obsługuje).

1. `Napastnik` = siła jednostki wroga przy osadzie + jego sojusznicy w drodze (Z osady dla naszego okna obrony).
2. `T_obrony` = czas do końca jego akcji: rabunek – punkty życia wsi / tempo z modelu; oblężenie – `T_pozostało` z 4.1 liczone dla napastnika.
3. Kandydaci = nasze jednostki AI, które dojdą w `T_obrony`, posortowane po czasie dojścia. Pomijamy: drużynę i armię gracza, armie z oblężeniem w stanie KONTYNUUJ, które **skończą przed** `T_obrony` (zdobędą zamek, zanim wróg spali wieś – wartość zamku > wieś w modelu gry), wodzów pod ochroną 335.
4. Przydział: dobieramy najbliższych, aż suma ≥ **1.2 × Napastnik** (326c). Przydzieleni dostają `DefendSettlement` z oceną najlepszą; nieprzydzieleni dostają na tę osadę Defender ×0.5 – nie lecą wszyscy naraz.
5. Gdy nikt nie zdąży albo nie zbierze 1.2× – **nikt nie wraca** (wieś przepada, armia robi swoje). To jest odpowiedź na ping-pong: armia Robba nie wraca z południa po jedną wieś, której i tak nie uratuje.
6. Histereza: przydział trwa do końca ataku albo do spadku sumy przydzielonych < 1.0 × Napastnik (wtedy dobór od nowa); jednostka może zmienić przydział najwyżej raz na dobę.

---

## 6. Zabezpieczenia

### 6.1 Migotanie
- Progi z przerwą: okno ≤ 0.8 / zakaz oblężenia > 1.0 / odwrót > 1.25; obrona 1.2 / 1.0.
- Odczyty co 3 h – decyzja nie zmienia się częściej niż wiedza.
- Odwrót wymaga 2 kolejnych odczytów; blokada celu 1 doba.
- Bieżący cel gry ma już ×1.3 (`num29`) – nie zmieniamy.

### 6.2 Odpoczynek armii, nocne obozy, 335
Mnożniki z 3.2 działają tylko na oceny celu; nie dotykają `Hold`/odpoczynku. Odwrót z 4.2 nie rusza armii w nocnym obozie poza samym oblężeniem (obóz oblężniczy to nie nocny obóz). Wódz pod ochroną 335 – bez zmian.

### 6.3 Kanon
Pokój/rozejm 264/330 – bez zmian (w rozejmie nie ma wojny, więc nie ma celów). Weto siedzib, teatr Północy B7 przed Krwawymi Godami, Reach-korona 332 – bez zmian.

### 6.4 Gracz
Nie sterujemy `MobileParty.MainParty` ani armią, w której jest (`MainParty.Army`) – jak B7. Postawy królestwa gracza nie ruszamy. Lordowie AI królestwa gracza dostają mnożniki z 3.2 (to tylko rozsądek wyboru celu), ale `BehaviorPriority` gracza-króla gra liczy dalej po swojemu. Drużyna gracza liczy się w Z i O jako siła.

### 6.5 Złoto i ludzie
Projekt nie tworzy złota ani ludzi – tylko przesuwa oceny. B334-19 ma pozostać TAK.

---

## 7. Klucze Settings (`Settings.cs` + `McmSettings.cs`, domyślnie true, opisy EN jak 334)

| Klucz | Co włącza | Off |
|---|---|---|
| `LocalPower334e` | mapa sił w polu, fantom = 0, mnożnik celu 3.2, zdjęcie mnożników SCA | jak 334d |
| `LocalStance334e` | postawa Neutral dla wszystkich królestw AI (zastępuje `CanonWarStance334c`) | 334c / BK |
| `ScaLocal334e` | wyczerpanie SCA z siły w polu dla wszystkich (zastępuje `ScaCoalitionOwn334c`) | 334c |
| `LocalRestraint334e` | 274 powściągliwość silniejszego → q (weto siedzib zostaje) | 274 |
| `LocalMuster334e` | `MusterBlocked` i B8 według okna | 326c-A |
| `ArmyRaid334e` | armie łupią wsie w oknie | gra: tylko samotni lordowie |
| `SiegeLocalRetreat334e` | kontynuuj/odstąp, obejście BK SIEGE-STICK i Hold | gra + BK |
| `LocalDefence334e` | przydział obrony pustych ziem | gra |

Przykład opisu: `LocalPower334e` – „Lords choose raids and sieges by the enemy hosts in the field that can reach the place before they finish (the game's own siege time and march speed), not by the whole strength of the enemy realm with all its garrisons: when the enemy's armies are far away, they raid and besiege where nobody can stop them in time. Off = as before”.

---

## 8. Linie logu (`Armoury-*.log`)

- **Start:** `Sila lokalna 334e - start | klucze … | transpiler fantomu TAK (2/2) | postfiks oceny TAK (model …) | SCA TAK | BK SIEGE-STICK obejscie TAK | kolejka AiHourlyTick …`
- **Doba, każde królestwo AI w wojnie:** `Sila lokalna 334e - krolestwo | doba D | <krolestwo> | pole: N druzyn, L ludzi, S sily (zalogi 0) | cele ocenione: wsie W (okno w), twierdze T (okno t) | opcje Besieger/Raider >0: b/r | postawa N (BK chcial: D/A/N) | SCA wyczerpane lok TAK/NIE (glob TAK/NIE), wrog/swoi pole X/Y | rozpoczete: rabunki r (puste r0), oblezenia o (puste o0) | armie a`
- **Oblężenie (dobowo i przy zmianie decyzji):** `Sila lokalna 334e - oblezenie | <cel> | wodz | oboz O | odsiecz R (przyjazd h, wykryta h temu) | zaloga Z | q | T_pozostalo d | decyzja KONTYNUUJ/SRODEK/ODSTAP/SZTURM`
- **Odstąpienie:** `Sila lokalna 334e - odstapienie | <cel> | oboz O vs odsiecz R+zaloga | dokad <twierdza> | blokada do doby D`
- **Obrona:** `Sila lokalna 334e - obrona | <osada> | napastnik A | T_obrony h | przydzieleni k (suma S, ratio) | zostaja n`
- **Koszt (dobowo):** `Sila lokalna 334e - koszt | odczyty mapy m | AiHelper c | ms suma/maks godziny | potkniecia p`

---

## 9. sprawdz_logi: grupa `lokalna334e` (`tools/sprawdz_logi.py`)

| Miara | Opis | Próg | Baza (167 / 334b) |
|---|---|---|---|
| L1 | Rabunki wsi Północy przez koronę, Reach i Żelaznych w oknach P1–P5 | ≥ 3 / 30 dób okna na każde królestwo w wojnie | 0 w 135 dobach |
| L1z | **Żelaźni: rabunki > 0** w biegu ≥ 30 dób wojny z Północą | > 0 | 0 |
| L2 | Doby z oknem (cel q ≤ 0.8 w zasięgu) bez rabunku, oblężenia i marszu na cel | ≤ 30% dób okna | Żelaźni 79/79, Reach 79/79, korona ~72/79 |
| L2k | **Korona: armia w polu** w dobach okna (wojna, nie rozejm) | ≥ 50% dób okna | 7/79 |
| L3 | Opcje Besieger/Raider = 0 przy lokalnej przewadze | 0 dób | Żelaźni 78/79 |
| L4 | Postawa ≠ N u królestwa AI; wyczerpanie SCA z sum blokujące cel w oknie | 0 | cały log |
| L5 | KO „oblegający pokonani” przy odsieczy ≥ 1.2 × obóz znanej ≥ czas odwrotu | 0 | O1, O5, O6, O7 |
| L6 | KO „myśl wodza” / rozejście obozu przy q_obl ≤ 0.8, jedzenie ≥ 1.5 doby, bez obrony OB5 | 0 | O2, O3 |
| L7 | Nowe oblężenie przy q > 1.0 | 0 | O7 |
| L8 | Odsiecz zebrana ≥ 1.2 × obóz bez ataku > 1 doba | 0 | O4 |
| L9 | Regresja: 5 odwrotów i 4 kontynuacje z listy dobrych | bez zmian | poprawne |
| L10 | Siła lokalna: załogi 0, odległość w dobach z modelu (linia krolestwo/koszt) | TAK | brak linii |
| L11 | Regresja: O326-C1/C2, odpoczynek armii, nocne obozy, rozejm/pokój 264/330, B334-19 | jak dotąd | TAK |
| L13 | **Odsetek nowych rabunków i oblężeń AI na cele puste** (q ≤ 0.8 w chwili startu) | ≥ 60% | brak |
| L14 | **Odstąpienia przed silniejszą odsieczą** (linia odstapienie przy q_obl > 1.25) w biegu ≥ 60 dób | ≥ 1, i każde z q_obl > 1.25 | brak |
| L15 | Ping-pong: ta sama drużyna zmienia przydział obrony > 1 raz na dobę albo wraca do celu w czasie blokady | 0 | brak |
| L12 | Koszt: średnio < 5 ms/h, maks. < 50 ms/h, potknięcia 0 | TAK | brak |

Aktualizacja starych grup (R7): `balans334` B334-* dotyczące postawy 334c i `ScaOwn` – przy włączonych kluczach 334e oczekują „zastąpione 334e”, a nie wartości 334c. Linia SCA 334 dostaje pola „wróg/swoi pole”.

---

## 10. Pliki do zmiany

- **Nowe:** `Armoury/src/LocalFront334e.cs` (FieldMap, Z, O, q, linie), `LocalFront334e.Score.cs` (transpiler, postfiks oceny, SCA), `LocalFront334e.Siege.cs`, `LocalFront334e.Defence.cs`.
- **Zmiany:** `WarFront334.Stance.cs` (Neutral dla wszystkich AI), `ScaNoBandits320.cs:305` (swoi = pole), `WarFront334.cs` (`Days` z prędkością jednostki, `ScaOwn` za kluczem), `CanonWarBalance.Restraint.cs` (`_restrain` za kluczem), `CanonWarBalance.Levers.cs:77` (`MusterBlocked`), `WarFront334.Theatre.cs` (B8: q > 1.0 = weto), `SiegeWalls2.Hold.cs` (`RetreatOrdered`), rejestracja łatek w SubModule (jak `WarFront334.SessionStart`), `Settings.cs`, `McmSettings.cs`, `tools/sprawdz_logi.py`, `CHANGELOG.md`, `docs/STAN-PRAC.md`.

## 11. Test

Na kopii `autotest-przekierowany-*` z DLL `m334e` (ForgeView, GrandTourney, RealisticCaptivity z m335): bieg 90 dób od zapisu z oknem P2 (Północ pod Sarsfield/Duskendale/Hornvale) oraz bieg kontrolny od startu wojny. Po serii posprzątać zapisy testowe. Nie wgrywać do gry, nie zamykać gry Jeffa, nie ruszać `Ironman0gXONzURi8yO.sav`.
