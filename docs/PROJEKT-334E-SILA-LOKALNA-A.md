# Projekt 334e (wariant A, minimalny): zasada siły lokalnej

Gałąź: `noc/334e-lokalna-sila` (98a7b10b = 334c/334d + scalone 335/335b). DLL testowe: `C:\Users\GAME\bannerlord-autotest\test\m334e`.
Nic nie wgrywamy do gry, gry Jeffa nie zamykamy, `Ironman0gXONzURi8yO.sav` nie ruszamy.

Zasada Jeffa (06.11) w jednym zdaniu: **o tym, czy uderzyć, decyduje siła wroga, która zdąży przyjść do celu, zanim skończymy, a nie ile wróg ma w ogóle.** Załogi innych osad się nie liczą, armia na drugim końcu mapy się nie liczy, a armia, która nadchodzi, się liczy.

Wariant A = najmniej nowych łatek. Jedna nowa miara (`Local334e`), jeden transpiler i jeden postfiks na modelu celu gry, jeden prefiks na łatce BK. Resztę robią zmiany w istniejących łatkach (334c postawa, SCA 320/334, 274 weto, Hold/F2, B8).

---

## 1. Miara: Z = siła wroga przy celu (`Local334e.Threat`)

Nowy plik `Armoury/src/Local334e.cs` (`internal static partial class Local334e`), plus `Local334e.Log.cs` (linie, liczniki) i `Local334e.Test.cs` (testy reguł bez gry).

### 1.1 Kto się liczy (jednostki w polu)

Migawka jednostek w polu, budowana w tej samej pętli co F334 `WarFront334.Compute` (raz na dobę, `Ensure`):
- drużyny lordów (`MobileParty.AllLordParties`), które są `IsActive`, nie siedzą w osadzie jako załoga i nie są doczepione do armii;
- armia = wódz + doczepieni, siła `Army.EstimatedStrength` (jak F334 `Compute`, bez podwójnego liczenia z SCA, R5);
- **gracz i jego armia liczą się jako siła wroga** (prawdziwa siła), ale nigdy nie są sterowani;
- pomijane: załogi (`IsGarrison`), milicja, karawany, chłopi, bandy, Inni, frakcje z `ScaNoBandits320.Skip != 0`.

Siła = `PartyBase.EstimatedStrength` (z modelu symulacji walki gry, ta sama miara co w `GetTargetScoreForFaction` i OB5). Zero złota i ludzi z niczego: tylko odczyt.

### 1.2 Czy zdąży (czas z modeli gry)

ETA jednostki do celu w dobach = `WarFront334.Days(nav, dist, land, naval)` z `AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty` (droga jak myśl AI, także morzem; R4: jedna miara = doby, nie jednostki mapy SCA). Prędkości: `Campaign.EstimatedAverageLordPartySpeed` / `EstimatedAverageLordPartyNavalSpeed`.

Czas naszej akcji `T` (doby):
| Akcja | T | Źródło |
|---|---|---|
| nowe oblężenie | ETA_nasza + 8.02 | `AiMilitaryBehavior.AverageSiegeDurationAsDays` (= `CanonWarBalance.DefendRangeDays`) |
| oblężenie w toku | max(1, 8.02 − doby od `SiegeEvent.SiegeStartTime`) | ta sama stała |
| najazd (rabunek wsi) | ETA_nasza + `SettlementHitPoints` / ((√N + 5) / 900) / 24 | `DefaultRaidModel.CalculateHitDamage` (N = nasi ludzie; 100 ludzi ≈ 2.5 doby, 1000 ≈ 1 doba) |
| obrona wsi pod rabunkiem | pozostały czas rabunku napastnika (ten sam wzór) | j.w. |

Jednostka wroga wchodzi do Z, gdy jej ETA ≤ T. Wynik dla celu trzymany jako lista (ETA, siła) posortowana po ETA z sumami narastającymi, więc Z(T) dla dowolnego T to jedno wyszukanie binarne.

Horyzont listy: 8.02 + 4.01 = 12.03 doby (czas oblężenia + próg dystansu obrony gry `GetDistanceScoreForDefending`). Dalej = nie zdąży nigdy.

### 1.3 Wiedza: zwiadowcy i plotki

AI w grze widzi wszystko (brak mgły wojny). Wprowadzamy opóźnienie wiedzy, bo bez niego „zanim wrócą, zdążymy” nie działałoby: wróg zawraca w tej samej godzinie.
- **Plotki (daleko):** położenie z migawki dobowej (ta sama co F334). Wiadomości o armiach przychodzą raz na dobę. To jednocześnie oszczędza czas (AiHelper raz na dobę) i jest stabilne (brak migotania).
- **Zwiadowcy (blisko):** jednostki wroga w zasięgu wzroku decydującej drużyny / wodza obozu (`Campaign.Current.Models.MapVisibilityModel.GetPartySpottingRange(party)`, czyli umiejętność Zwiadu wodza) są liczone na żywo co godzinę (`MobileParty.StartFindingLocatablesAroundPosition`). Nadchodząca odsiecz, która weszła w zasięg zwiadu, jest więc znana od razu.
- Jednostka widziana na żywo zastępuje swój wpis z migawki (bez podwójnego liczenia).

### 1.4 Nasza siła S

S = `ourStrength` gry (drużyna albo armia razem z doczepionymi, a przy zbiórce z przyszłymi członkami `PossibleArmyMembersUponArmyCreation` – gra liczy to sama w `AiHourlyTick`)
\+ sojusznicy w drodze: drużyny tej samej frakcji i sojuszników w tej samej wojnie, których `TargetSettlement == cel` (albo `BesiegedSettlement == cel`) i ETA ≤ T. Liczone z migawki dobowej + zwiad.

### 1.5 Wskaźnik q

`q = Z / S` (siła wroga, która zdąży, do naszej). Załoga i milicja samego celu **nie** wchodzą do q, bo gra liczy je już w bramce oblężenia (`num6`, `num7`, próg 2× przy Besieger, 0.75× przy Raider). Wchodzą tylko w ocenie bitwy z odsieczą (pkt 4), bo wtedy załoga robi wypad.

Progi (wszystkie z istniejących modeli, bez nowych stałych):
| Pas | q | Mnożnik celu | Pochodzenie liczby |
|---|---|---|---|
| pusto | q ≤ 0.33 | ×1.2 | 0.33 = gra `num17 −= ourStrength × 0.33`; ×1.2 = mnożnik Aggressive gry |
| słabo | 0.33 < q ≤ 0.8 | ×1.0 | 0.8 = próg odwrotu SCA (`TryRetreatFromBadSiege`) |
| równo | 0.8 < q < 1.2 | ×0.8 | ×0.8 = mnożnik Defensive gry |
| silnie | q ≥ 1.2 | nowe oblężenie: 0; najazd: ×0.25 | 1.2 = próg ataku odsieczy 326c (C1/C2) i OB5 miasta; 0.25 = SCA |

Histereza celu bieżącego (`party.TargetSettlement == cel` albo już oblega/łupi): pas liczony z q / 1.25 (1.25 = próg SCA `NearbyEnemyLordStrength`). Cel porzucamy dopiero przy q ≥ 1.5, a zaczynamy dopiero przy q < 1.2. Dodatkowo gra ma swoje ×1.3 dla bieżącego celu.

---

## 2. Wybór celów najazdu i oblężenia (decyzje 3)

### 2.1 Transpiler: usunięcie „fantomu” `num10` (P-A1)

`DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction` (klasa deklarująca, `dec334e:207-278`): dwa wywołania `IFaction.get_CurrentTotalStrength` zamienione na `Local334e.PhantomTotal(IFaction)`:
```
PhantomTotal(f) => LocalOn && Kingdom AI ? 0f : f.CurrentTotalStrength
```
Wtedy `max(0, (0 − num6 − num7)/…)` = 0, czyli bez „40k z garnizonami”. Transpiler na ciele metody bazowej działa też pod SCA (SCA woła bazę), więc rozwiązuje R6 bez dotykania `SiegeRansom.Act:664`. Gdy wzorzec IL nie pasuje (inna wersja gry): łatka nie wchodzi, linia startu „fantom: BRAK”, reszta działa.

### 2.2 Postfiks na aktywnym modelu celu (P-A2)

`Local334e.ScorePost(Settlement targetSettlement, Army.ArmyTypes missionType, MobileParty mobileParty, float ourStrength, ref float __result)`, wpięty przez `PatchTarget.OfActive(model, "GetTargetScoreForFaction", …)` jak `SiegeRansom.Act.cs:674`, priorytet `Priority.Low` (przed `SiegeRansom.ScorePost` Last, więc rozejm 0 zostaje 0).

Pomija: `__result <= 0`, drużynę gracza i armię z graczem, frakcje nie-królestwa, kampanię spoza kanonu nie (dotyczy **wszystkich** królestw AI).

Besieger / Raider:
1. q z pkt 1, pas, mnożnik.
2. Gdy aktywny model to SCA (`StrategicTargetScoreModel`), cofamy jego mnożniki z sum i geografii, a zostawiamy logistykę (jedzenie < 3 doby ×0.25, spójność, cooldown, osobowość, pogoda):
   - q ≤ 0.8: cofnąć ×0.18 (wroga twierdza w 55), ×0.55 (dalej niż 85), ×0.55 (`IsExhausted`), ×0.35 (`WantsPeace`/`ForcePeace`), ×0.25 (`NearbyEnemyLordStrength(95)`);
   - q > 0.8: cofnąć tylko ×0.25 `NearbyEnemyLordStrength` (zastępuje go nasze q, liczone w dobach i bez podwójnej armii).
   Warunki odczytane tymi samymi pomocnikami SCA (`StrategicAiHelpers.*`, delegaty z refleksji, jak `ScaNoBandits320`). Brak pomocnika = nic nie cofamy (linia startu).
3. Raider: gra dopuszcza tylko samotnych lordów (D3) – zostaje (wariant A nie uczy armii rabować).

Defender (pkt 5): tylko wieś pod rabunkiem: ETA naszej drużyny > pozostały czas rabunku → ×0.25 („nie zdążymy”). Obrona oblężonych twierdz (OB5, 326c) **nietknięta**.

### 2.3 Postawa: Neutral dla wszystkich AI (P-A3, zmiana istniejącej łatki)

`WarFront334.Stance.cs`: `StancePostfix` (Last po `BKDiplomacyBehavior.TickKingdoms`) przy `LocalStance334e` ustawia `PartyObjective.Neutral` każdej drużynie lordów każdego królestwa AI (nie tylko stron 274). Skutek: koniec obcinania wszystkich opcji Besieger/Raider przez Defensive (`AiMilitaryBehavior.cs:121`). Rolę Defensive/Aggressive przejmuje mnożnik pasa q z 2.2 (×0.8 / ×1.2 – te same liczby), już per cel i per drużyna. `StanceRule` 334c zostaje jako ścieżka przy wyłączonym kluczu. R3: inicjatywa wraca do Neutral – świadome.

### 2.4 Wyczerpanie SCA lokalnie (P-A4, zmiana istniejącej łatki)

`ScaNoBandits320.CalcPostfix`: przy `ScaLocalExhaustion334e` człon „wróg > swoi × 1.35” jest wyłączony dla wszystkich królestw AI (ustawiamy `EnemyStrength` = swoi, czyli stosunek 1.0, po zapisaniu surowej wartości do linii). Zostają furtki lokalne SCA: zagrożone twierdze ≥ twierdze/3 (promień 80) i wsie złupione ≥ 3. `ScaOwn` (334c, suma koalicji) przy włączonym 334e nie jest wołany. Decyzje wojna/pokój (BK, Diplomacy, 264/330) dalej liczą sumy – kanon bez zmian.

### 2.5 Weto 274 „za frontem” i zbiórka (P-A5, zmiany istniejących łatek)

- `CanonWarBalance.Restraint.VetoPostfix`: przy `LocalStrength334e` rodzaj 2 („za frontem”, powściągliwość silniejszego) nie działa – zastępuje go q. **Weto siedzib (rodzaj 1) zostaje.**
- `CanonWarBalance.Levers` `MusterBlocked`: przy 334e wyłączone. Zbiórka armii rusza, gdy opcja Besieger z `WillGatherArmy` wygra w myśli – a jej wynik zawiera już q liczone z siłą przyszłej armii. Wyzwalacz „7 dób bez bitwy” zostaje.
- B8 `WarFront334.Theatre` `BkFirstPostfix` (BK `FindArmyObjective`, R2): cel z q ≥ 1.2 (S = suma `GetCustomStrength(Siege)` kandydatów BK) traktowany jak zawetowany → następna twierdza wg reguły BK. Inaczej BK zebrałby armię na cel, który myśl odrzuci.

---

## 3. Oblężenie w toku: kontynuuj albo odstąp (decyzja 4)

Bez nowej łatki na myśl AI: rozszerzamy istniejący `SiegeWalls2.ThinkPost` (postfiks Last na `CampaignEventDispatcher.AiHourlyTick`, wspólny dla F2 i Hold), który już omija bitwę szturmu, morze, skrypty i Innych.

Dla wodza obozu i samodzielnej drużyny w obozie (`BesiegedSettlement != null`, nie doczepiona do armii, nie gracz):
- C = siła obozu (`BesiegerCamp.GetInvolvedPartiesForEventType` – suma `EstimatedStrength`, jak gra) + nasi w drodze z ETA ≤ ETA odsieczy;
- R = odsiecz znana: jednostki wroga z ETA ≤ pozostały czas oblężenia, które **idą tu** (cel `DefendSettlement` tej osady, armia, której wódz ma ten cel, zbiórka OB5 tej osady) albo są w zasięgu zwiadu obozu; + załoga i milicja osady (wypad przy bitwie z odsieczą).

| Stan | Warunek | Akcja |
|---|---|---|
| ODSTĄP | R ≥ 1.25 × C (= 1 / 0.8, próg SCA) | wpis `BesiegeSettlement` tej osady = −1; `AddBehaviorScore` `GoToSettlement` najbliższej własnej twierdzy (droga AiHelper, nie oblężonej) z wynikiem max(najlepszy, 0.01) × 1.05; flaga `Local334e.Retreat[party]` na 24 h |
| KONTYNUUJ | R ≤ C / 1.2 (próg 326c), jedzenie ≥ 3 doby (SCA), najlepszy cel nie jest obroną oblężonej własnej twierdzy | Hold także po `FreshHours`: wpis oblężenia = max(1.05 × najlepszy + 0.01, `HoldFloor`) – dokładnie jak `HoldFresh` |
| BEZ ZMIAN | pomiędzy | gra decyduje (ma ×4 za trwające oblężenie i `num17`) |

Szczegóły:
- W bitwie szturmu F2 („szturm do końca”) nic nie zmieniamy.
- `HoldFresh` przy fladze Retreat nie trzyma (dziś patrzy tylko na `BesiegedRelief.ReliefReady`).
- BK SIEGE-STICK (D2): nowy prefiks na `BannerKings.Patches.ArmySiegePersistencePatches.Postfix(MobileParty)` – `return false`, gdy drużyna ma flagę Retreat. Klucz `BkSiegeStickYields334e`. Brak klasy BK = linia startu „BK stick: brak”.
- Martwy odwrót SCA (D1) zostaje martwy; `ScaRetreatPre` (F2) bez zmian.
- Ucieczka: po odstąpieniu drużyną prowadzą 223 LordsAvoidDanger i inicjatywa gry. Linia odstąpienia podaje „ucieczka zdąży TAK/NIE” (ETA do twierdzy < ETA odsieczy).
- Nowe oblężenie przy q ≥ 1.2 daje 0 już w 2.2 (L7, przypadek O7).

Rabunek w toku (D4): ta sama myśl, drużyna z `MapEvent.IsRaid` jako atakujący: jednostki wroga z ETA ≤ pozostały czas rabunku i siłą ≥ 1.25 × nasza → wpis `RaidVillage` tej wsi = −1 (myśl gry wybiera co innego, dalej prowadzi 223). Klucz `LocalRaidBreakOff334e`. Do potwierdzenia w autoteście, czy myśl gry przerywa rabunek w trakcie MapEvent (linia „rabunek przerwany” + KO rabunku).

---

## 4. Obrona pustych ziem: kto wraca, kto zostaje (decyzja 5)

Bez nowej łatki:
- Wraca ten, kto **zdąży**: Defender wsi pod rabunkiem ×0.25, gdy ETA > pozostały czas rabunku (2.2). Armia z południa nie biegnie na północ po wieś, której i tak nie uratuje.
- Zostaje oblężenie wygrywane: KONTYNUUJ z pkt 3 ma pierwszeństwo przed obroną wsi; ustępuje tylko obronie oblężonej własnej twierdzy (OB5, jak `HoldFresh`).
- Twierdze: OB5, 326c i homeGuard 274/334 (`RealThreat`) bez zmian. Odsiecz atakuje przy przewadze (326c) – nie ruszamy.
- Anti ping-pong: dalekie położenia zmieniają się raz na dobę (plotki), więc ocena dalekich celów nie skacze co godzinę; bliskie zmiany wnosi tylko zwiad (realne zagrożenie). Do tego histereza 1.2 / 1.5 z pkt 1.5 i ×1.3 gry dla bieżącego celu.

---

## 5. Zabezpieczenia (decyzja 6)

- **Histereza:** pasy z przesunięciem 1.25 dla bieżącego celu; obóz: ODSTĄP ≥ 1.25, KONTYNUUJ ≤ 1/1.2, pośrodku gra.
- **Wydajność:** migawka raz na dobę w pętli F334 (bez osobnego przejścia). Z liczone leniwie (tylko cele, które gra oceniła > 0) i trzymane do końca doby; filtr wstępny linią prostą (`Days(linia prosta) > 12.03` → odrzuć bez AiHelper); wsie dziedziczą ETA twierdzy, do której należą (`Village.Bound`) + linia prosta twierdza–wieś. Zwiad na żywo: siatka `StartFindingLocatablesAroundPosition`. Licznik wywołań AiHelper i ms w linii doby.
- **Żelaźni:** ETA z `NavigationType.Naval/All` (statki), więc wybrzeże Północy liczy się drogą morską, a pusta Północ daleko od armii Robba jest „pusto”. Teatr 334 Wysp (B9) i `IronbornCanonReave334` zostają.
- **Gracz:** drużyna gracza i każda armia z graczem nie są sterowane (ani wynik, ani postawa, ani Hold/odwrót); jako wróg liczą się normalnie. Królestwo gracza: postawa jego lordów AI też Neutral (BK i tak go pomija – bez zmian, `LocalStance334e` tylko dla AI).
- **Kanon:** weto siedzib, B7 teatr Północy, 326b zwrot zdobyczy, rozejm/pokój 264/330, Reach w koronie 332 – bez zmian (to inna oś niż siła).
- **Odpoczynek i nocne obozy:** nie ruszane (nic nie zmienia `NightRest`/`ArmyRestDay`).
- **Wyjątki:** każda łatka `try/catch` → `WarFront334.Stumble("334e …")`, błąd = mnożnik 1 (stara gra).

---

## 6. Klucze Settings (`Settings.cs`, blok po 334c; MCM tylko EN-opis)

| Klucz | Domyślnie | Co |
|---|---|---|
| `LocalStrength334e` | true | główny: miara Z, postfiks celu, weto 274 rodzaj 2 i `MusterBlocked` wyłączone |
| `LocalPhantomOff334e` | true | transpiler `num10` |
| `LocalStance334e` | true | Neutral dla wszystkich AI (zastępuje `CanonWarStance334c`) |
| `ScaLocalExhaustion334e` | true | SCA bez „wróg > swoi × 1.35”, bez `ScaOwn` (zastępuje `ScaCoalitionOwn334c`) |
| `LocalSiegeJudge334e` | true | KONTYNUUJ/ODSTĄP w `SiegeWalls2.ThinkPost` |
| `BkSiegeStickYields334e` | true | BK SIEGE-STICK przepuszcza odwrót |
| `LocalRaidBreakOff334e` | true | przerwanie rabunku przed silniejszym |

Off = zachowanie 334d.

---

## 7. Linie logu (`Armoury-*.log`, tag `Sila lokalna (334e)`)

- start: `Sila lokalna (334e): start | fantom TAK/BRAK | cel OfActive <typ modelu> | SCA cofanie TAK/BRAK | BK stick TAK/BRAK | klucze ...`
- doba, każde królestwo AI w wojnie: `Sila lokalna (334e): krolestwo | dzien D | <krolestwo> | w polu nasi N/ludzi | wrog w polu M | puste cele wsie a / twierdze b | z przewaga c | opcje Raider x Besieger y | najazdy start r (puste r0) | oblezenia start s (puste s0, q>=1.2: 0) | postawa Neutral | wyczerpanie SCA lokalne TAK/NIE (furtka ...)`
- doba, kontrola L10: `Sila lokalna (334e): migawka | dzien D | jednostek u | zalog 0 | AiHelper k | ms t | zwiad na zywo z`
- oblężenie (raz na dobę na obóz + przy zmianie stanu): `Sila lokalna (334e): oblezenie | <osada> | oboz C | odsiecz R (idzie n, zwiad m, zaloga g) | ETA odsieczy d | pozostalo T | stan KONTYNUUJ/ODSTAP/BEZ ZMIAN | ucieczka zdazy TAK/NIE`
- `Sila lokalna (334e): rabunek przerwany | <wies> | nasi S | wrog Z | ETA d`
- `Sila lokalna (334e): potkniecie <miejsce> <wyjątek>`

## 8. sprawdz_logi: grupa `lokalna334e`

Nowa grupa w `tools/sprawdz_logi.py` (+ aktualizacja `balans334`: B334 linii postawy i SCA 334 – przy 334e oczekiwane „Neutral” i „wyczerpanie lokalne”, R7). Progi:

| Kod | Miara | Próg |
|---|---|---|
| L334e-1 | rabunki wsi Północy przez koronę, Reach, Żelaznych w dobach z oknem (puste cele > 0) | ≥ 3 na 30 dób okna na każde królestwo w wojnie z Północą |
| L334e-2 | doby z oknem bez rabunku/oblężenia/marszu na cel | ≤ 30% dób okna |
| L334e-3 | Żelaźni: rabunki > 0 i opcje Raider > 0 w ≥ 90% dób wojny | TAK |
| L334e-4 | korona: doby z armią w polu w czasie wojny (bez rozejmu) | ≥ 40% (baza 7/79) |
| L334e-5 | odsetek najazdów/oblężeń zaczętych na celach „pusto/słabo” | ≥ 70% |
| L334e-6 | postawa ≠ Neutral u AI / wyczerpanie SCA z sum | 0 |
| L334e-7 | KO „oblegający pokonani” po linii stanu ODSTĄP lub przy R ≥ 1.25 C znanym ≥ 1 h | 0 |
| L334e-8 | KO „myśl wodza” przy stanie KONTYNUUJ | 0 |
| L334e-9 | nowe oblężenie przy q ≥ 1.2 | 0 |
| L334e-10 | migawka: załóg 0, ms ≤ 2 × ms F334 tej doby | TAK |
| L334e-11 | regresja: O326-C1/C2 (odsiecz z przewagą atakuje), odpoczynek, nocne obozy, 264/330, B334-19, siedziby 274 | bez nowych NIE |
| L334e-12 | potknięcia 334e | 0 |

O4 (zebrana odsiecz bez ataku, L8 z rozpoznania) należy do 326c – tylko mierzymy; zdjęcie Defensive z korony powinno samo podnieść inicjatywę.

## 9. Testy

- `Local334e.Test.cs` (autotest, bez kampanii): pasy q i histereza; T rabunku z wzoru gry (100 ludzi ≈ 60 h); decyzje obozu na przypadkach z logów: O1 (3908 / 6003 → ODSTĄP), O5, O6, O7, Red Keep (3487 / 4365 = 1.2518 → ODSTĄP), Deep Den (9182 / 8088 + załoga → BEZ ZMIAN, gra trzyma), Sarsfield, Ashemark 109485 (→ KONTYNUUJ), O2/O3 (odsiecz 0–44% → KONTYNUUJ).
- Bieg kampanii 90 dób na kopii `autotest-przekierowany-*` od zapisu z oknem P2 (Północ na południu), DLL z `m334e`, `sprawdz_logi --grupa lokalna334e` i `--grupa balans334`; sprzątanie po serii.

## 10. Miejsca w kodzie (podsumowanie)

| Co | Plik | Rodzaj |
|---|---|---|
| miara Z, S, q, migawka, zwiad | `Local334e.cs` (nowy) | – |
| budowa migawki | `WarFront334.cs` `Compute` | wołanie `Local334e.Snapshot` w pętli |
| fantom `num10` | `Local334e.cs` | transpiler na `DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction` |
| wynik celu + cofanie SCA | `Local334e.cs` `ScorePost` | postfiks Low, `PatchTarget.OfActive` |
| postawa | `WarFront334.Stance.cs` `StanceApply` | zmiana |
| wyczerpanie SCA | `ScaNoBandits320.cs` `CalcPostfix` | zmiana |
| weto rodzaj 2 | `CanonWarBalance.Restraint.cs` `VetoPostfix` | zmiana |
| zbiórka | `CanonWarBalance.Levers.cs` `MusterBlocked` | zmiana |
| cel armii BK | `WarFront334.Theatre.cs` `BkFirstPostfix` | zmiana |
| oblężenie w toku, rabunek | `SiegeWalls2.Hold.cs` / `SiegeWalls2.Oblezenia3.cs` `ThinkPost` | zmiana |
| BK stick | `Local334e.cs` | prefiks na `ArmySiegePersistencePatches.Postfix` |
| klucze | `Settings.cs`, `McmSettings.cs` | nowe |
| linie | `Local334e.Log.cs`, `ArmouryBehavior` Daily | nowe |
| sprawdz_logi | `tools/sprawdz_logi.py` grupa `lokalna334e`, aktualizacja `balans334` | nowe/zmiana |
