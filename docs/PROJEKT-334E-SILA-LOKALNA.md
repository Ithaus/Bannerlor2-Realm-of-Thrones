# Projekt 334e (końcowy): zasada siły lokalnej

Gałąź `noc/334e-lokalna-sila` (od 99a05f27 + scalenie `noc/335-ochrona-wodza`). DLL testowe: `C:\Users\GAME\bannerlord-autotest\test\m334e` (ForgeView, GrandTourney, RealisticCaptivity z m335). Nie wgrywamy do gry, nie zamykamy gry Jeffa, nie ruszamy `Ironman0gXONzURi8yO.sav`. Worktree `n333` (test 334c/334d) nietknięty.

Źródła: `PROJEKT-334E-SILA-LOKALNA-A.md` (wariant minimalny) i `PROJEKT-334E-SILA-LOKALNA-B.md` (wariant pełny). Ten plik jest wiążący; A i B zostają jako materiał.

**Zasada Jeffa (06.11):** o ataku decyduje siła wroga w polu, która **zdąży dojść do celu, zanim skończymy**. Liczy się, gdzie armia jest teraz, a nie ile wróg ma w ogóle. Załogi innych osad i armie daleko się nie liczą. Gdy nadchodzi armia wroga, oblegający ocenia bitwę: przegra, to odstępuje; wygra albo zdąży zdobyć przed nią, to kontynuuje.

---

## 0. Werdykt sędziego

| Kryterium | A (minimalny) | B (pełny) | Do projektu końcowego |
|---|---|---|---|
| Zgodność z zasadą | dobra; armie dalej nie łupią, obrona wsi tylko „kto nie zdąży, ×0.25” | pełna: armie łupią w oknie, odwrót tylko przy odsieczy, która zdąży, przydział obrony | B |
| Pokrycie przypadków | brak „odsiecz silniejsza, ale nie zdąży → kontynuuj”; brak przypadku armii z 4000 ludźmi łupiącej pustą Północ | macierz pełna (oblężenie nowe/w toku, rabunek, obrona, armia/samotny lord, morze) | B |
| Wiedza (zwiad, plotki) | migawka dobowa (plotki) + na żywo w zasięgu wzroku wodza (zwiad z umiejętności) – dosłownie to, co mówi Jeff, tanie i stabilne | odczyt co 3 h z pesymizmem −3 h, bez zwiadu | **A** |
| Prędkość | średnia prędkość lorda | prędkość własna jednostki z modelu gry | **B** |
| Progi | pasy 0.33/0.8/1.2, zakaz oblężenia dopiero ≥ 1.2 (zaczyna oblężenie przy lekkiej przewadze wroga) | 0.8 / 1.0 / 1.25 – zakaz, gdy wróg silniejszy („przegra”) | **B** + histereza bieżącego celu z A |
| Wyczerpanie SCA | wyłączone całkiem (traci sens SCA dla pokoju) | przeliczone z siły w polu obu stron | **B** |
| Cofanie mnożników SCA | cofa też WantsPeace ×0.35 (to polityka, nie ocena celu) | zostawia ForcePeace/WantsPeace | **B** |
| Zbiórka armii | blokada wyłączona, decyduje ocena celu (już z q) | nowe sprawdzenie „czy jest cel w oknie” – dubluje ocenę | **A** |
| Ryzyko | niskie | wyższe (3 nowe moduły godzinne, przydział obrony) | B z osobnymi kluczami na każdy moduł, kolejność wdrożenia (rozdz. 9) |
| Wydajność | migawka raz na dobę w pętli F334 | odczyt co 3 h, leniwie | A (doba) + leniwe liczenie celów z B |
| Kanon i paczki | zgodne | zgodne | bez różnic: 264/330, weto siedzib, B7/B9, 326b/c, OB5, F2, Hold, odpoczynek, nocne obozy, 335 |

Wynik: **szkielet B** (pełne pokrycie zasady) z **modelem wiedzy, zbiórką, przerwaniem rabunku, warunkiem jedzenia i histerezą bieżącego celu z A**.

---

## 1. Co zastępujemy

| # | Mechanizm globalny | Miejsce | Po 334e |
|---|---|---|---|
| G1 | Postawa BK Defensive/Aggressive całego królestwa z `CurrentTotalStrength` ×1.5 (Defensive = 0 opcji Besieger/Raider) | BK `TickKingdoms`, `WarFront334.Stance.cs` (334c) | wszystkie królestwa AI = Neutral; ostrożność/agresja per cel z q (3.2) |
| G2 | „Fantom” `num10`: siła całego królestwa z załogami rozdzielona na każdą osadę | `DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction` | 0 (transpiler) |
| G3 | SCA „wyczerpane” z sum (wróg > swoi ×1.35), cele ×0.55; 334c `ScaOwn` = suma koalicji | `ScaNoBandits320.CalcPostfix:305`, `WarFront334.ScaOwn` | obie strony = siła w polu w zasięgu 8.02 doby (3.3) |
| G4 | SCA głębia ×0.18/×0.55, `NearbyEnemyLordStrength` ×0.25 (linia prosta, armia ×2) | `StrategicTargetScoreModel` | zdjęte, gdy reguła lokalna oceniła cel (3.2) |
| G5 | 274 powściągliwość silniejszego (weto „za frontem”) | `CanonWarBalance.Restraint.cs` | zastąpione q; weto siedzib zostaje |
| G6 | `MusterBlocked` (strona silniejsza/powściągana) | `CanonWarBalance.Levers.cs:77` | wyłączone; zbiórka rusza, gdy oblężenie z q wygra w myśli AI |
| G7 | Brak odstępowania (odwrót SCA martwy), BK SIEGE-STICK trzyma do 24 h | SCA `TryRetreatFromBadSiege`, BK `ArmySiegePersistencePatches` | moduł oblężenia (rozdz. 4) |

Bez zmian: decyzje wojna/pokój (BK, Diplomacy, 264/330 – mogą liczyć sumy), F334 jako miara pary królestw dla dźwigni ekonomicznych (W1/W2/W5, B5, 326c-E), teatr B7/B9, zwrot 326b, weto siedzib, OB5 i 326c (C1–C3, odsiecz atakuje przy przewadze), F2 „szturm do końca”, odpoczynek armii, nocne obozy, 223 LordsAvoidDanger, 335 ochrona wodza, Reach w koronie 332.

---

## 2. Miara: siła wroga przy celu

Nowy plik `Armoury/src/LocalFront334e.cs` (`internal static partial class LocalFront334e`), plus `.Score.cs`, `.Siege.cs`, `.Defence.cs`, `.Log.cs`, `.Test.cs`.

### 2.1 Jednostki w polu
- **Armia** = wódz + doczepieni, liczona raz (siła `GetTotalLandStrengthWithFollowers()` wodza / `Army.EstimatedStrength`, jak `ourStrength` gry), albo **samotna drużyna lorda** (`IsLordParty`, `Army == null`, `IsActive`).
- Nie liczymy: załóg (`IsGarrison`), milicji, karawan, wieśniaków, band, Innych, drużyn bez wodza / z wodzem w niewoli / `IsDisbanding`, frakcji z `ScaNoBandits320.Skip != 0`.
- Drużyna stojąca w samym celu (`CurrentSettlement == cel`) nie wchodzi do Z tego celu (gra liczy ją w `num6/num7`). Załoga i milicja celu wchodzą tylko w ocenę bitwy z odsieczą (4.1).
- **Gracz i jego armia** liczą się jako siła (wroga lub sojusznika), ale nigdy nie są sterowani.
- Siła = `EstimatedStrength` z modelu walki gry. Tylko odczyt – zero złota i ludzi z niczego.

### 2.2 Czas dojścia (doby)
`dni(U, cel)` = droga `AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty` (ląd albo morze, jak myśl AI) / **prędkość własna U** z modelu gry (armia tempem najwolniejszego, statki swoją). Gdy `U.Speed ≤ 0` (postój, nocny obóz) – `Campaign.EstimatedAverageLordPartySpeed` (lub morska średnia). Funkcja `WarFront334.Days` dostaje parametr prędkości.

Filtr wstępny: linia prosta / (najwyższa prędkość lorda × 24) > 12.03 doby → odrzuć bez `AiHelper` (droga nigdy nie jest krótsza od prostej). Horyzont 12.03 = 8.02 (`AverageSiegeDurationAsDays`) + 4.01 (próg obrony gry `GetDistanceScoreForDefending`); dalej = nie zdąży nigdy.

### 2.3 Okno czasu W (ile potrzebujemy)
| Akcja | W | Źródło |
|---|---|---|
| nowe oblężenie | nasz dojazd + 8.02 | `AverageSiegeDurationAsDays` |
| oblężenie w toku | `T_pozostało` (4.1) | j.w. + postęp oblężenia |
| rabunek wsi | nasz dojazd + `T_rabunku` | `SettlementHitPoints` / ((√N + 5)/900) / 24 z `DefaultRaidModel.CalculateHitDamage` (N = nasi ludzie; 100 ludzi ≈ 2.5 doby, 1000 ≈ 1 doba). Odczyt modelu przez aktywny model; gdy się nie da – 1 doba i linia „potknięcie” |
| obrona wsi pod rabunkiem | pozostały czas rabunku napastnika | ten sam wzór, od obecnych HP wsi |

### 2.4 Wiedza: plotki i zwiadowcy (z A)
AI w grze widzi wszystko. Jeff: przeciwnik „ma zwiadowców, plotki” – więc:
- **Plotki (daleko):** położenia wszystkich jednostek z **migawki dobowej**, budowanej w tej samej pętli co F334 `WarFront334.Compute` (`Ensure`). Daleka armia, która zawróciła, „dojdzie do wiadomości” najpóźniej po dobie – stąd działa „zanim wrócą, zdążymy”, a oceny dalekich celów nie skaczą co godzinę.
- **Zwiadowcy (blisko):** jednostki wroga w zasięgu wzroku decydującej drużyny / wodza obozu (`MapVisibilityModel.GetPartySpottingRange`, czyli umiejętność Zwiadu) są liczone na żywo co godzinę (`StartFindingLocatablesAroundPosition`). Wpis na żywo zastępuje wpis z migawki (bez podwójnego liczenia).
- **Zamiary wroga** widoczne w grze (cel `DefendSettlement`/`BesiegeSettlement` = ta osada, zbiórka armii na tę osadę w OB5/326c) liczą się na żywo – to wiadomość posłańców, a gra i tak je zna.
- Nie modelujemy fałszywych plotek ani niewidzialnych armii (losowość bez podstawy w modelu gry).

### 2.5 Z, O, q
- `Z(cel, W)` = Σ siły jednostek wrogich atakującemu (jego wojny i koalicja wroga), dla których `dni ≤ W`. Lista (dni, siła) per cel, posortowana, z sumami narastającymi – dowolne W to jedno wyszukanie binarne.
- `O` = `ourStrength` gry (z przyszłymi członkami zbiórki `PossibleArmyMembersUponArmyCreation` – gra dolicza sama) + sojusznicy z `TargetSettlement == cel` albo w `BesiegerCamp` celu, którzy dojdą przed pierwszym wrogiem z Z.
- `q = Z / O`.

### 2.6 Progi (wszystkie z istniejących liczb)
| Próg | Wartość | Skąd |
|---|---|---|
| okno otwarte | q ≤ 0.8 | SCA: wróg > nasi × 1.25 = groźny; `TryRetreatFromBadSiege` 0.8 |
| nowe oblężenie zakazane | q > 1.0 | wróg silniejszy = „przegra”; ostrzej niż odwrót, by nie zaczynać i zaraz nie uciekać |
| rabunek zakazany | q > 1.25 | SCA, ten sam próg |
| bieżący cel marszu porzucony | q > 1.25 (oblężenie i rabunek) | histereza z A: zaczynamy przy ≤ 1.0, porzucamy przy > 1.25; gra dodaje ×1.3 dla bieżącego celu |
| odwrót z oblężenia | q_obl > 1.25 | odwrót SCA 0.8 (przywracamy jego regułę) |
| kontynuacja pewna | q_obl ≤ 0.8 | symetrycznie |
| obrona: przydział wystarcza / rozpada się | ≥ 1.2 / < 1.0 × napastnik | 326c |

×1.2 i ×0.8 to mnożniki postawy Aggressive/Defensive gry (`AiMilitaryBehavior`) – teraz wynikają z sytuacji przy celu, a nie z całego królestwa.

---

## 3. Wybór celów

### 3.1 Postawa: Neutral dla wszystkich królestw AI (G1)
`WarFront334.Stance.cs` `StancePostfix` (Last po BK `TickKingdoms`), klucz `LocalStance334e`: `StanceApply` ustawia Neutral każdej drużynie lordów każdego królestwa AI w wojnie (tylko tam, gdzie jest inna wartość) + przegląd raz na dobę dla nowych drużyn. Królestwa gracza nie ruszamy. Koniec wycinania wszystkich opcji Besieger/Raider (Żelaźni: 0 opcji od doby 109524). `StanceRule` 334c zostaje ścieżką przy wyłączonym kluczu; linia startu „334c: zastąpione 334e”.

### 3.2 Ocena celu (G2, G4)
1. **Transpiler** (`LocalPhantomOff334e`) na `DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction` (klasa deklarująca): oba wywołania `IFaction.get_CurrentTotalStrength` → `LocalFront334e.PhantomStrength(f)` = 0 dla królestw AI, inaczej oryginał. Kotwice dokładnie ×2, inaczej łatka nie wchodzi („fantom: BRAK” w linii startu). Działa też pod SCA (SCA woła bazę).
2. **Postfiks** (`LocalPower334e`) na aktywnym modelu (`PatchTarget.OfActive`, jak `SiegeRansom.Act.cs`), priorytet przed `SiegeRansom.ScorePost` (rozejm 0 zostaje 0). Strażnik `[ThreadStatic] _depth` (SCA wołające bazę nie dostaje mnożnika dwa razy). Pomija `__result ≤ 0`, drużynę gracza i armię z graczem, frakcje nie-królestwa.

| Typ | q ≤ 0.8 | 0.8 < q ≤ próg | q > próg |
|---|---|---|---|
| Besieger, nowy cel | ×1.2 | ×0.8 | **0** (próg 1.0) |
| Besieger, bieżący cel marszu | ×1.2 | ×0.8 | **0** dopiero przy q > 1.25 |
| Besieger, już oblegamy | ×1 – decyduje rozdz. 4 | ×1 | ×1 |
| Raider | ×1.2 | ×0.8 | **0** (próg 1.25) |
| Defender wsi pod rabunkiem | ×1, gdy zdążymy | – | ×0.25, gdy `dni > T_rabunku pozostały` („nie zdążymy”, z A) |
| Defender twierdzy, Patrolling | ×1 (OB5/326c bez zmian) | ×1 | ×1 |

3. **Pod SCA** (`StrategicTargetScoreModel`) postfiks dzieli wynik przez mnożniki, które zastępujemy, odtwarzając warunki tymi samymi pomocnikami SCA (delegaty z refleksji, jak `ScaNoBandits320`; brak pomocnika = nic nie cofamy, linia startu):
   - `NearbyEnemyLordStrength` ×0.25 – zdjęte zawsze (zastępuje je q, w dobach, bez armii ×2);
   - głębia ×0.18 i ×0.55, wyczerpanie ×0.55 – zdjęte, gdy q ≤ 0.8;
   - ForcePeace/WantsPeace – **zostają** (cel wojny, polityka);
   - logistyka zostaje: jedzenie < 3 doby ×0.25, spójność, cooldown, osobowość, pogoda.

### 3.3 Wyczerpanie SCA z siły w polu (G3)
`ScaNoBandits320.CalcPostfix`, klucz `ScaLocal334e`, wszystkie królestwa AI:
- wróg = F334 `ScaEnemy` (wrogowie w polu dochodzący do naszych twierdz w 8.02 doby), rozszerzone na wszystkie królestwa AI;
- swoi = Σ naszych jednostek w polu (własne + koalicja) w tym samym zasięgu – lustro, bez załóg; `ScaOwn` (334c) przy kluczu nie działa;
- furtki lokalne SCA zostają (twierdze zagrożone ≥ 1/3 w promieniu 80, wsie złupione ≥ 3).
Wyczerpanie może dalej prowadzić do pokoju (polityka), ale cele w oknie nie dostają ×0.55 (3.2).

### 3.4 Zbiórka armii (G6, z A)
- `CanonWarBalance.Levers` `MusterBlocked`: przy `LocalPower334e` wyłączone. Zbiórka rusza, gdy opcja Besieger z `WillGatherArmy` wygra w myśli – jej wynik zawiera już q z siłą przyszłej armii. Wyzwalacz „7 dób bez bitwy” zostaje.
- B8 `WarFront334.Theatre.cs` `BkFirstPostfix` (BK `FindArmyObjective`): cel z q > 1.0 (O = suma `GetCustomStrength(Siege)` kandydatów BK) traktowany jak zawetowany → następna twierdza według reguły BK (`BkVetoNextTarget334`).

### 3.5 Weto 274 (G5)
`CanonWarBalance.Restraint.VetoPostfix`, klucz `LocalRestraint334e`: rodzaj „za frontem” (`_restrain`) wyłączony. Weto siedzib (do Krwawych Godów), B7/B9 i 326b zostają.

### 3.6 Armie też łupią (z B)
Klucz `ArmyRaid334e`, postfiks `AiHourlyTick` (ta sama kolejka co rozdz. 4): wódz armii AI bez oblężenia, gdy żadne oblężenie nie ma wyniku > 0, dostaje `RaidSettlement` najlepszej wsi z q ≤ 0.8, wynik = `GetTargetScoreForFaction(wieś, Raider, wódz, siła armii)` (formuła gry). Oblężenie w oknie wygrywa (wartość zamku > wsi w modelu gry). Nie dotyczy armii w odpoczynku/nocnym obozie (te łatki działają później i wygrywają). Do potwierdzenia w autoteście, że gra prowadzi rabunek armią AI (linia „armia rabuje” + KO rabunku); gdy nie – klucz off, reszta działa.

### 3.7 Żelaźni przez morze
Bez wyjątku w regule: `AiHelper` daje drogę morską dla drużyn ze statkami, przeprawa wchodzi do dojazdu; Północ bez floty (326) dochodzi do wybrzeża tylko lądem. Przygotowanie przeprawy `IronbornCanonReave334` i teatr B9 zostają.

---

## 4. Oblężenie w toku: kontynuuj albo odstąp

`LocalFront334e.Siege.cs`, klucz `LocalSiegeJudge334e`. Rozszerzenie istniejącego `SiegeWalls2.ThinkPost` (postfiks na `CampaignEventDispatcher.AiHourlyTick`, wspólny dla F2 i Hold; już omija bitwę szturmu, morze, skrypty, Innych) – bez nowej łatki na myśl AI (z A). Kolejność: po wetach 274, przed OB5 obroną własnej twierdzy (OB5 wygrywa), przed Hold.

### 4.1 Wejście (co godzinę, wódz obozu i samodzielna drużyna w obozie, nie gracz)
- `Obóz` = Σ `EstimatedStrength` drużyn `BesiegerCamp.GetInvolvedPartiesForEventType` + nasi w drodze, którzy dojdą przed odsieczą.
- `T_pozostało` = 0.5 doby, gdy machiny gotowe / mury SiegeWalls2 przebite (szturm najbliższy); inaczej max(1, 8.02 − doby od `SiegeStartTime`).
- `Odsiecz` = Σ wrogów **nadchodzących** z `dni ≤ T_pozostało`: cel `DefendSettlement`/armia z tym celem/zbiórka OB5 tej osady, albo jednostka w zasięgu zwiadu obozu, której odległość zmalała od poprzedniej godziny. Stojąca obok armia, która się nie zbliża, nie wywołuje odwrotu.
- `Załoga` = załoga + milicja oblężonej osady (wypad przy bitwie z odsieczą).
- `q_obl = (Odsiecz + Załoga) / Obóz`; `przyjazd` = najwcześniejsze `dni` jednostek, które razem dają q_obl > 1.25.

### 4.2 Macierz
| Stan | Warunek | Decyzja |
|---|---|---|
| szturm trwa | MapEvent szturmu | nic (F2) |
| brak odsieczy / słabsza | q_obl ≤ 0.8 i jedzenie ≥ 3 doby (SCA) | **KONTYNUUJ**: wpis oblężenia = max(1.05 × najlepszy + 0.01, `HoldFloor`) także po `FreshHours`; ustępuje tylko obronie własnej oblężonej twierdzy (OB5). Naprawia O2, O3 |
| strefa środkowa | 0.8 < q_obl ≤ 1.25 | bez ingerencji (gra ×4 za trwające oblężenie, `num17`, Hold) |
| przeważa, nie zdąży | q_obl > 1.25, `przyjazd > T_pozostało` | **KONTYNUUJ** (zdobędziemy przed nimi, potem bronimy się za murami) |
| przeważa, zdąży | q_obl > 1.25, `przyjazd ≤ T_pozostało`, **2 kolejne godziny** | **ODSTĄP**. Naprawia O1, O5, O6, O7 |
| brak jedzenia | jedzenie < 3 doby, q_obl ≤ 0.8 | bez ingerencji (logistyka gry/SCA) |

**Odstąpienie:** wpis `BesiegeSettlement` tej osady = −1; `GoToSettlement` do najbliższej własnej twierdzy (nieoblężonej) **leżącej dalej od odsieczy niż obóz**, droga `AiHelper`, wynik max(najlepszy, 0.01) × 1.05; flaga `Retreat[party]` na 24 h i blokada celu dla tej drużyny/armii na 1 dobę albo do q(cel) ≤ 0.8 w 2 kolejnych migawkach. Gra sama zwija obóz. Dalej prowadzi 223 LordsAvoidDanger. Linia podaje „ucieczka zdąży TAK/NIE”.

**Zgodność:**
- BK SIEGE-STICK: prefiks na `BannerKings.Patches.ArmySiegePersistencePatches.Postfix(MobileParty)` → `return false` przy fladze `Retreat` (klucz `BkSiegeStickYields334e`; brak klasy BK = linia startu „BK stick: brak”).
- `SiegeWalls2.HoldFresh` przy fladze `Retreat` nie trzyma.
- Martwy odwrót SCA zostaje martwy; `ScaRetreatPre` (F2) bez zmian.
- 326c C1–C3 (strona odsieczy) bez zmian. O4 (Duskendale) tylko mierzony (L8).

### 4.3 Rabunek w toku (z A)
Klucz `LocalRaidBreakOff334e`. Drużyna rabująca (`MapEvent.IsRaid`, atakujący): wrogowie nadchodzący z `dni ≤ pozostały czas rabunku` i siłą > 1.25 × nasza, 2 kolejne godziny → wpis `RaidVillage` tej wsi = −1, myśl gry wybiera co innego, ucieczkę prowadzi 223. Autotest sprawdzi, czy gra przerywa rabunek w trakcie MapEvent (linia „rabunek przerwany” + KO).

---

## 5. Obrona pustych ziem: kto wraca, kto zostaje

`LocalFront334e.Defence.cs`, klucz `LocalDefence334e` (z B), ten sam postfiks godzinny. Dotyczy rabunków naszych wsi i oblężeń, których nie obsługuje OB5 (oblężenia twierdz zostają przy OB5/326c).

1. `Napastnik` = siła wroga przy osadzie + jego sojusznicy, którzy dojdą przed końcem akcji.
2. `T_obrony` = pozostały czas rabunku (2.3) albo `T_pozostało` oblężenia.
3. Kandydaci = nasze jednostki AI z `dni ≤ T_obrony`, po czasie dojścia. Pomijamy gracza i jego armię, wodzów pod ochroną 335, armie w odpoczynku/nocnym obozie (łatki odpoczynku wygrywają) oraz oblężenia w stanie KONTYNUUJ, które skończą przed `T_obrony` (zamek > wieś w modelu gry).
4. Dobieramy najbliższych, aż suma ≥ 1.2 × Napastnik. Przydzieleni: `DefendSettlement` z najlepszą oceną. Nieprzydzieleni: Defender tej osady ×0.5 (nie lecą wszyscy).
5. Nikt nie zdąży albo nie uzbiera 1.2× → nikt nie wraca. Armia z południa nie zawraca po wieś, której nie uratuje.
6. Przydział trwa do końca ataku albo do spadku sumy < 1.0 × Napastnik (dobór od nowa); jednostka zmienia przydział najwyżej raz na dobę.

Gdy klucz wyłączony, zostaje minimum z A: Defender wsi ×0.25 dla tych, którzy nie zdążą (3.2).

---

## 6. Zabezpieczenia

- **Migotanie:** progi z przerwą (okno 0.8 / zakaz oblężenia 1.0 / porzucenie i odwrót 1.25; obrona 1.2/1.0); dalekie położenia raz na dobę (plotki); odwrót i przerwanie rabunku po 2 kolejnych godzinach; blokada celu 1 doba; ×1.3 gry dla bieżącego celu.
- **Wydajność:** migawka w pętli F334 (bez osobnego przejścia, ~300 drużyn); `dni` liczone leniwie tylko dla celów, o które pyta myśl AI (wynik gry > 0), po filtrze linii prostej, pamięć `(jednostka, cel) → dni` do końca doby; wsie dziedziczą `dni` swojej twierdzy (`Village.Bound`) + linia prosta twierdza–wieś; zwiad na żywo tylko wokół decydującej drużyny. Licznik `AiHelper` i ms w linii „koszt”. Cel: średnio < 5 ms na godzinę gry, maks. < 50 ms.
- **Wyjątki:** każda łatka `try/catch` → `WarFront334.Stumble("334e …")`, mnożnik 1 (stara gra), miara nieczynna do końca doby.
- **Gracz:** `MainParty` i armia z graczem nie są sterowane (ani wynik, ani postawa, ani Hold/odwrót/przydział). Postawy królestwa gracza nie ruszamy; jego lordowie AI dostają mnożniki celu (rozsądek wyboru), jak wszyscy.
- **Kanon:** rozejm/pokój 264/330 (w rozejmie nie ma celów), weto siedzib, B7, 326b, 332 bez zmian.
- **Odpoczynek, nocne obozy, 335:** mnożniki działają tylko na oceny celu; moduły 3.6/4/5 pomijają armie w odpoczynku i nocnym obozie; obóz oblężniczy to nie nocny obóz.
- **Złoto i ludzie:** projekt tylko przesuwa oceny; B334-19 ma zostać TAK.

---

## 7. Klucze Settings (`Settings.cs` + `McmSettings.cs`, domyślnie true, opisy EN; off = 334d)

| Klucz | Co |
|---|---|
| `LocalPower334e` | miara Z/O/q, postfiks oceny celu, cofanie mnożników SCA, `MusterBlocked` off, B8 q > 1.0 = weto |
| `LocalPhantomOff334e` | transpiler fantomu `num10` |
| `LocalStance334e` | Neutral dla wszystkich królestw AI (zastępuje `CanonWarStance334c`) |
| `ScaLocal334e` | wyczerpanie SCA z siły w polu (zastępuje `ScaCoalitionOwn334c`) |
| `LocalRestraint334e` | 274 „za frontem” → q (weto siedzib zostaje) |
| `ArmyRaid334e` | armie łupią wsie w oknie |
| `LocalSiegeJudge334e` | KONTYNUUJ/ODSTĄP oblężenia w toku |
| `BkSiegeStickYields334e` | BK SIEGE-STICK przepuszcza odwrót |
| `LocalRaidBreakOff334e` | przerwanie rabunku przed silniejszym, który zdąży |
| `LocalDefence334e` | przydział obrony pustych ziem |

Opis `LocalPower334e`: „Lords choose raids and sieges by the enemy hosts in the field that can reach the place before they finish (the game's own siege time and march speed), not by the whole strength of the enemy realm with all its garrisons. Off = as before.”

---

## 8. Linie logu (`Armoury-*.log`, tag `Sila lokalna (334e)`)

- **start:** `start | klucze … | fantom TAK (2/2)/BRAK | cel OfActive <model> | SCA cofanie TAK/BRAK | BK stick TAK/BRAK | kolejka AiHourlyTick …`
- **krolestwo** (doba, każde królestwo AI w wojnie): `krolestwo | dzien D | <krolestwo> | pole: N jednostek, L ludzi, S sily (zalogi 0) | wrog w polu M | cele: wsie W (okno w), twierdze T (okno t) | opcje Raider/Besieger >0: r/b | postawa Neutral (BK chcial D/A/N) | SCA wyczerpane lok TAK/NIE (glob TAK/NIE), wrog/swoi pole X/Y | start: rabunki r (puste r0, armia ra), oblezenia o (puste o0, q>1.0: 0) | armie w polu a`
- **migawka:** `migawka | dzien D | jednostek u | zalog 0 | zwiad na zywo z`
- **oblezenie** (doba na obóz + przy zmianie): `oblezenie | <osada> | wodz | oboz O | odsiecz R (idzie n, zwiad m, zaloga g) | q_obl | przyjazd d | pozostalo T | stan KONTYNUUJ/SRODEK/ODSTAP/SZTURM`
- **odstapienie:** `odstapienie | <osada> | oboz O vs odsiecz R+zaloga | dokad <twierdza> | ucieczka zdazy TAK/NIE | blokada do doby D`
- **rabunek przerwany:** `rabunek przerwany | <wies> | nasi S | wrog Z | przyjazd d`
- **armia rabuje:** `armia rabuje | <wodz> | <wies> | q`
- **obrona:** `obrona | <osada> | napastnik A | T_obrony h | przydzieleni k (suma, stosunek) | zostaja n`
- **koszt** (doba): `koszt | AiHelper c | ms suma/maks godziny | potkniecia p`
- **potkniecie:** `potkniecie <miejsce> <wyjatek>`

---

## 9. sprawdz_logi: grupa `lokalna334e`

| Kod | Miara | Próg |
|---|---|---|
| L1 | rabunki wsi Północy przez koronę, Reach, Żelaznych w dobach z oknem | ≥ 3 / 30 dób okna na każde królestwo w wojnie z Północą |
| L1z | Żelaźni: rabunki > 0 (bieg ≥ 30 dób wojny) i opcje Raider > 0 w ≥ 90% dób wojny | TAK |
| L2 | doby z oknem bez rabunku, oblężenia i marszu na cel | ≤ 30% dób okna |
| L2k | korona: armia w polu w dobach wojny (bez rozejmu) | ≥ 40% (baza 7/79; odpoczynek i nocne obozy zostają) |
| L3 | opcje Besieger/Raider = 0 przy lokalnym oknie | 0 dób |
| L4 | postawa ≠ Neutral u AI; wyczerpanie SCA z sum | 0 |
| L5 | KO „oblegający pokonani” po stanie ODSTĄP lub przy q_obl > 1.25 znanym ≥ 2 h i przyjeździe ≤ T | 0 |
| L6 | KO „myśl wodza”/rozejście obozu przy KONTYNUUJ | 0 |
| L7 | nowe oblężenie przy q > 1.0 | 0 |
| L8 | odsiecz zebrana ≥ 1.2 × obóz bez ataku > 1 doba (O4, 326c – pomiar) | raport, bez progu |
| L9 | regresja przypadków z 06.11: odwroty O1/O5/O6/O7, Red Keep, Stoney Sept, Rosby, Goldentooth; kontynuacje O2/O3, Ashemark, Sarsfield, Deep Den, Bronzegate | jak w testach reguł |
| L10 | migawka: załóg 0 | TAK |
| L11 | regresja: O326-C1/C2, odpoczynek armii, nocne obozy, 264/330, B334-19, siedziby 274, 335 | bez nowych NIE |
| L12 | koszt: średnio < 5 ms/h, maks. < 50 ms/h; potknięcia 0 | TAK |
| L13 | odsetek nowych rabunków i oblężeń AI na celach q ≤ 0.8 | ≥ 60% |
| L14 | odstąpienia (bieg ≥ 60 dób) | ≥ 1, każde z q_obl > 1.25 |
| L15 | ping-pong: zmiana przydziału obrony > 1/dobę albo powrót do celu w czasie blokady | 0 |

Aktualizacja `balans334` (R7): linie postawy 334c i `ScaOwn` przy kluczach 334e oczekują „zastąpione 334e” / „Neutral” i pól „wrog/swoi pole”.

---

## 10. Pliki

- **Nowe:** `Armoury/src/LocalFront334e.cs` (migawka, zwiad, Z/O/q), `.Score.cs` (transpiler, postfiks, cofanie SCA, armie łupią), `.Siege.cs` (4.1–4.3, prefiks BK stick), `.Defence.cs`, `.Log.cs`, `.Test.cs`.
- **Zmiany:** `WarFront334.cs` (`Compute` woła migawkę, `Days` z prędkością, `ScaOwn` za kluczem), `WarFront334.Stance.cs`, `WarFront334.Theatre.cs` (B8), `ScaNoBandits320.cs` (`CalcPostfix`), `CanonWarBalance.Restraint.cs`, `CanonWarBalance.Levers.cs` (`MusterBlocked`), `SiegeWalls2.Hold.cs` / `SiegeWalls2.Oblezenia3.cs` (`ThinkPost`, `HoldFresh` vs `Retreat`), rejestracja łatek w SubModule, `Settings.cs`, `McmSettings.cs`, `tools/sprawdz_logi.py`, `CHANGELOG.md`, `docs/STAN-PRAC.md`.

## 11. Kolejność wdrożenia i testy

1. Rdzeń: migawka + Z/O/q, transpiler, postfiks oceny, postawa Neutral, SCA lokalnie, 274 i zbiórka (to, co jest w obu wariantach). Build, testy reguł.
2. Oblężenie w toku + BK stick + przerwanie rabunku.
3. Armie łupią, obrona pustych ziem.
Każdy etap za swoim kluczem – gdy autotest pokaże problem, wyłączamy jeden klucz, nie całą paczkę.

- `LocalFront334e.Test.cs` (bez kampanii): progi i histereza; T rabunku z wzoru gry (100 ludzi ≈ 60 h); macierz 4.2 na przypadkach z logów: O1 (3908/6003 → ODSTĄP), O5, O6, O7, Red Keep (3487/4365 → ODSTĄP przy przyjeździe ≤ T), Deep Den (9182/8088 + załoga → ŚRODEK), Ashemark, Sarsfield, O2/O3 (→ KONTYNUUJ); przypadek „przeważa, nie zdąży → KONTYNUUJ”; przydział obrony (1.2×, nikt nie wraca).
- Bieg 90 dób na kopii `autotest-przekierowany-*` z DLL `m334e`, od zapisu z oknem P2 (Północ pod Sarsfield, Duskendale, Hornvale), plus bieg kontrolny od startu wojny; `sprawdz_logi --grupa lokalna334e` i `--grupa balans334`. Po serii posprzątać zapisy testowe.

## 12. Do sprawdzenia przy implementacji
- odczyt czasu rabunku z aktywnego modelu (inaczej 1 doba + potknięcie);
- czy AI prowadzi rabunek armią (3.6) i czy przerywa rabunek w trakcie MapEvent (4.3);
- dokładny priorytet postfiksu godzinnego: po wetach 274, przed OB5, F2 i Hold – potwierdzić w linii startu;
- kotwice transpilera (2/2) na bieżącej wersji gry;
- O4 Duskendale – osobna diagnoza 326c, ta paczka tylko mierzy.

---

## 13. Po recenzjach (logika, techniczna, adwokat diabła) – wiążące zmiany względem rozdz. 2–9

Kod: jedna wersja, jedno wpięcie (`SiegeWalls2.ThinkPost`, Last). `Judge` zwraca stan; `4` tylko gdy odwrót wykonano w tej godzinie (wtedy m116 Fresh nie trzyma).

| Recenzja | Zmiana |
|---|---|
| W1 (adwokat): okno W za długie | Wróg z migawki (nie widziany zwiadem, bez widocznego zamiaru na cel) liczy się z czasem **2 × doby drogi**: wieść idzie tempem lorda, potem marsz. Zwiad i zamiar – czas 1×. Tywin pod Harrenhal nie blokuje Robba na Zachodzie. |
| W2 (logika): odsiecz widziana za późno | Odsiecz w horyzoncie 12.03 doby; stan 3 (KONTYNUUJ-NIE-ZDĄŻY) gdy przyjazd przewagi > pozostały czas; ODSTĄP tylko gdy ucieczka zdąży (własna twierdza dalej od odsieczy, nasza droga < przyjazd przewagi). Ucieczka nie zdąży i brak pół doby zapasu – zostaje w obozie (linia „zostaje w obozie”). |
| W10 (adwokat): czas oblężenia ze stałej | Pozostały czas z postępu w grze: przygotowania (`SiegePreparations.Progress`), potem tempo burzenia najsłabszego odcinka murów od końca przygotowań; bez danych – 8.02 doby od startu. |
| W3 (adwokat): załoga wywołuje odwrót | Do ODSTĄP załoga liczona najwyżej 0.8 × obóz; przewagę musi dać odsiecz (test T3 „zaloga-nie-wywoluje-odwrotu”). |
| W4/D4: trzecie strony | Wróg naszego wroga, sam w wojnie z panem celu i spoza jego strony, nie wchodzi do Z ani do odsieczy; odsiecz = zamiar obrony (nie oblężenie/rabunek tego celu). |
| W3 (logika/techn.): nowe opcje omijają weta | `WarFront334.VetoedFor334e` (274, teatr Północy/Wysp/korony, 326b) przed każdą opcją armii łupiących i Żelaznych oraz przed dodaniem KONTYNUUJ bez wpisu. |
| W4: KONTYNUUJ wskrzesza weto | Wpis oblężenia ≤ 0 (weto, rozejm, zakaz m116) – bez zmian; brak wpisu – dodajemy tylko w wojnie i bez weta. Wódz chroniony 335 – bez KONTYNUUJ. |
| W5 (adwokat): korona za Przesmyk | Przy zdjętym „za frontem” 274: **teatr korony** – korona i jej strona do Krwawych Godów bez celów kultur Północy (battania, skagosi, za Murem); własne/odbicie bez weta. |
| W6 (adwokat): lordowie zostawiają ziemie | Lustro obrony: lord, którego własna twierdza w dobie drogi ma wroga z widocznym zamiarem oblężenia, nie dostaje ×1.2 ani cofnięć SCA; Defender takiej twierdzy (nieoblężonej) ×1.2. Królestwo z jedną twierdzą – postawa BK. |
| W7/D5: cofanie SCA | Warunki sprawdzone z dekompilacją `StrategicTargetScoreModel` (1:1); wszystkie cofnięcia (także ×0.25 lordów w 95) tylko przy q ≤ 0.8. |
| W8 (adwokat): wyczerpanie z siły w polu | `ScaLocal334e` domyślnie **NIE** – wygrywający z armią daleko od domu prosiłby o pokój; wojna/pokój zostają na sumach. |
| W9 (adwokat): B8 bez armii | O = wódz + kandydaci zbiórki z `ArmyManagementCalculationModel.CanLordCreateArmy` (zapas: wolne drużyny w zasięgu wezwania modelu, bez nocnego obozu). Siła lokalna nie daje `null` – bez następnego celu zostaje wybór BK, cel zeruje ocena w myśli armii. |
| W2 (adwokat): wieczne łupienie | Armia łupi tylko przy spójności > 2 × próg rozwiązania gry; oblężenia wracają dzięki W1. |
| Techn. 5/6/9/10 | Zamek przy potknięciach; migawka tylko z wątku głównego (Daily po liniach doby, Hourly po wczytaniu), bez niej miara milczy; czyszczenie obozów/flag/blokad/przydziałów w Daily; prefiks BK po pozycji `__0`. |
| Techn. drobne | Klucze słowników bez `GetHashCode` (struktury), blokada (drużyna, osada) bez sklejania napisów, `[ThreadStatic] _inOwn` przy naszym wołaniu modelu, wyczerpanie SCA pamiętane na dobę, `PhantomStrength(null)`, zasięg rabunku raz na sesję, linia startu podaje stan `ThinkPost` i postawy BK, osobne liczniki ping-pongu. |
| D1/D7/D8 | Nasi w drodze doliczeni do obozu; histereza bieżącego celu także przez `Army.AiBehaviorObject`; obrona wsi z koalicją (`SameSide`). |
| D6 | Przegląd postawy co godzinę (nowe drużyny Neutral). |
| Wydajność (techn. 7/8) | Wieś dziedziczy drogi swojej twierdzy; filtr linii prostej z prędkości lądowej dla drużyn bez statków; linie królestw liczą okno tylko dla celów pytanych przez myśl AI. **Pominięte:** pamięć Z per (cel, godzina) i liczenie dróg poza zamkiem – do decyzji po pomiarze L12. |
| Pominięte | Z_dom > O_dom w lustrze obrony (jest tylko zamiar wroga); D3 adwokata (pomiar B/A zbiórek) – w autoteście; STAN-PRAC prowadzony na master. |

`sprawdz_logi --grupa lokalna334e`: L0/L0b start i łatki, LT testy, L10 migawka, L12 koszt, L15 ping-pong, L4 postawa, L3 okno bez opcji (≤ 10%), L13 starty w oknie (≥ 60%), L7 oblężenia q > 1.0 (≤ 10%), L1z Żelaźni, L2k korona (≥ 40% dób z armią), L14 odstąpienia (q_obl > 1.25), L16 „zostaje w obozie” (INFO).

## 14. 334f – poprawki po teście 90 dób (log at334e `Armoury-2026-11-06_18-15-03.log`)

Wynik 334e: kod 0, bez wywrotki, wódz 335 OK, rF 0.972, Rosby nie padło, Wyspy z armią 50/89, najazdy głównie w puste. NIE: L14, L12, L7, B334-12, B334-7, brak linii Północy, O326-A, L3. Zasady bez zmian: liczby z istniejących modeli, nic z niczego, kanon, odpoczynek armii i nocne obozy zostają, gracz nie sterowany.

| # | Objaw (dowód z logu) | Przyczyna | Zmiana 334f |
|---|---|---|---|
| 1a | L14: 0 odstąpień przy 129 stanach ODSTAP; każda linia „(1/2 h)” | Warunek dwóch kolejnych godzin zegara (`r.Hour == h-1`), a sąd obozu działa co ok. 3 h | Histereza z ocen dowódcy `BadStep`: nowe ODSTAP liczy się, gdy od poprzedniego minęła ≥ 1 h (osobna myśl); ostatnie ODSTAP sprzed > 12 h – od nowa; KONTYNUUJ / GŁÓD zerują; ŚRODEK i NIE-ZDĄŻY (migotanie) zostawiają licznik. Odwrót przy 2 myślach w 12 h (test T7). |
| 1b | Rosby w dobie: odsiecz 4873 → 1119 → 621 | Zamiar obrony lorda zmienia się przy każdej jego myśli („miga”) | Pamięć dowódcy: zamiar obrony zapamiętany 12 h (2 × okres myśli gry 6 h, jak `FarCacheHours` 274); liczy się, dopóki dowódca odsieczy nie oddala się (zapas pół godziny drogi); wpis znika przy pokoju, nieaktywnym lub po 12 h. Linia: „idzie N, pamięć M, zwiad S”. |
| 1c | „pozostało 1.00” przez 3 doby (Rosby), Acorn Hall 7.5k vs 21.6k „nie zdąży” | Podłoga `max(1, 8.02 − el)`; brama OB1 (szturm przy taranie i wieży albo wyłomie, s > 1) wydłuża oblężenia | `TimeLeft` z modelu gry: przygotowania (reszta / `SiegeEventModel.GetConstructionProgressPerHour`), taran i wieża (reszta / tempo budowy; nierozstawione – cała budowa) albo droga murów (tempo burzenia), potem szturm z szansą OB1 na myśl (1 / szansa godzin, ≥ 12 h). s ≤ 1 albo brak drogi – `HorizonDays` (liczy się każda odsiecz w horyzoncie). Wyjątek modelu – stara średnia. |
| 2 | L12: mediana 90 ms/h, maks 301 ms | `ZFor` przeglądał całą listę (~429 jednostek, frakcja, wojna, strona, zamiar przy każdym wpisie) przy każdej z ~1150 ocen na godzinę; droga osada–osada liczona od nowa | **Widok godziny** (`LV`: stan drużyny raz na godzinę; macierz wojen i strony na indeksach frakcji); **pamięć dróg** osada–osada (trwała, czyszczona przy nowej kampanii); **sumy (cel, królestwo)** na godzinę (wrogowie po dobach z sumami narastającymi, sojusznicy idący na cel) – ocena: wyszukiwanie binarne + poprawka zwiadu drużyny. Co 128. ocena – kontrola starym przeglądem na tym samym widoku (`ZForRef`); rozbieżności w linii koszt (ma być 0). Pomiar: `Stopwatch.GetTimestamp`, czekanie na zamek, model gry (IronSiege, armie łupią) i linie doby poza kosztem – osobne liczniki „czasy ms (334f)”. Linie królestw o północy bez `ZFor`. |
| 3 | L7: Stannis → King's Landing q 6.02 | Wydarzenie ROT Blackwater (teleport, skrypt) – nie błąd progu; osobno 274: zbiórka władcy podbijała cele wyzerowane przez 334e (`e.Item2 > -1f`; Joffrey sam na Maidenpool) | Start oblężenia fabuły (`DoNotMakeNewDecisions` albo `SiegePhantomLift.StoryOf`) – linia „fabuła ROT (skrypt) – poza regułą”, nie liczony w L7; wydarzeń nie blokujemy. 274: kandydat zbiórki władcy tylko z wynikiem > 0 (weto siły lokalnej i rozejm nie wskrzeszane). |
| 4 | B334-12: Żelaźni 0 oblężeń Północy, „opcji 0 (weta 0)” 91 dób | `tried++` przed q (4 najbliższe – Ashemark, Flint's Finger, Seagard, Greywater – q > 1 – zużywały limit); sito w linii prostej odcinało rejs (Deepwood 444 > 411); jedna opcja gry > 0 na Zachodzie wyłączała ofertę; wyjścia ciche | Do Krwawych Godów kandydaci tylko z Północy właściwej (kultura battania – cel kanonu); odległość skorygowana gry (morze) raz na kandydata, pamięć 6 h na drużynę (czas modelu poza kosztem); limit 4 liczy tylko cele z q ≤ 1.0, osobno najwyżej 8 ocen q; wyjście tylko, gdy gra daje oblężenie celu kanonu (inaczej wyniki rywalizują). Linia koszt: wyjścia (zajęci, bez zbiórki, odpoczynek, opcja gry kanonu, bez kandydatów, q>1, bez drogi, model 0) i najniższe q z celem i siłą. Próg q bez zmian (reguła Jeffa) – gdy cele kanonu mają q > 1 przy pełnej zbiórce, to realny układ sił (DECYZJA). |
| 5 | B334-7: korona bez oblężeń w 6 oknach 28 dób | Siła lokalna (pole korony 7–11k vs 31–39k wroga) – zgodnie z zasadą; SCA tylko mnoży; pętla 274 (pkt 3) | Poprawka 274 z pkt 3; diagnoza Besieger w linii królestwa („ocen, model 0, q>próg (min q @cel), blokada, lustro”). Sprawdzarka B334-7: okno bez żadnej doby z opcją Besieger > 0 = DECYZJA (korona słabsza lokalnie – do decyzji Jeffa, czy kanon wymaga oblężeń mimo przewagi Robba w polu); NIE tylko przy oknie z opcjami bez oblężenia. |
| 6 | Brak linii Północy | `KingdomLines` pomijał królestwo gracza | Linia królestwa gracza z dopiskiem „gracz (tylko log)” – postawy i decyzji gracza nie sterujemy (przegląd postawy dalej pomija gracza; L4 pomija tę linię). |
| 7 | O326-A: Północ 5 zbiórek po rozejmie (B/A 0.522) | 334e zdjął blokadę 326c (`!LocalFront334e.PowerOn`); 4 z 5 to patrol na front, który nie przechodzi przez q | Blokada 326c przywrócona w pełni. (Poza 334e: licznik bezczynności 274 nalicza doby rozejmu – do rozważenia osobno.) |
| 8 | L3: Wolni Ludzie 90 dób „okno bez opcji” | Zachowanie zgodne z regułą (wódz ~90 siły vs Straż 5–5.8k – q > 1.25; armii brak – jedzenie poniżej progu gry); błąd był w mierze: okno liczone dla celów pytanych przez dowolne królestwo, siłą najsilniejszej jednostki, bez czasu dojścia i zwiadu | Okno = ta sama miara, którą dostała myśl AI: `ScorePost` zapisuje cele pytane (q policzone) i w oknie (q ≤ 0.8) per królestwo (wsie / twierdze). Zachowania Wolnych Ludzi nie zmieniamy. |
| 9 | Sprawdzarka | L4 łapała 1. dobę (postawa przed przeglądem); nbitwa z logiem sesji jako 2. argumentem brała log sesji; „rabunek start” liczył wznowienia tego samego rabunku (Straż na Skirling 3–4 razy dziennie) | L4 bez 1. doby i linii gracza; nbitwa bierze log `Armoury*`; kod: jeden start na (strona, wieś) w 24 h (powtórki w linii koszt), sprawdzarka L13r: linie i unikalne epizody. |

**Sprawdzarka `lokalna334e` (334f):** L12b kontrola ZFor – rozbieżności 0; L7 z linii startu: NIE przy q > 1.25 (AbandonQ) > 10% startów bez fabuły ROT, (1.0, 1.25] tylko INFO; L14 – ≥ 1 odstąpienie w biegu ≥ 60 dób i każde przy q odsieczy (`q_odw`: odsiecz + załoga do 0.8 × obóz; stara linia – q_obl) > 1.25; L17 Żelaźni – ≥ 1 oferta oblężenia wybrzeża w biegu ≥ 60 dób, 0 przy najniższym q > 1 wszędzie = DECYZJA; L18 linia Północy (battania) w ≥ 90% dób z liniami królestw; L13r INFO. `balans334` B334-7: okno bez opcji Besieger = DECYZJA. Testy syntetyczne obu grup: TAK.

**Do sprawdzenia w teście 90 dób:** L12 średnio < 5 ms/h i godzina < 50 ms, L12b 0; L14 ≥ 1 odstąpienie (Rosby / Acorn Hall), żadne przy q_odw ≤ 1.25, „zostaje w obozie” sensowne; L17 oferty Żelaznych na Moat Cailin / Torrhen's Square / Deepwood (albo DECYZJA z q); O326-A 0 zbiórek silniejszej strony; L3 po nowej mierze; B334-7.

## 15. 334g – poprawki recenzji 334f

1. **Pętla odstąpienie → ponowne oblężenie.** Odwrót zapisuje blokadę (królestwo, osada) do przyjazdu odsieczy + 0,5 doby (co najmniej 1 doba, najwyżej 12,03 + 0,5). W tym czasie start oblężenia (ScorePost, BK, Żelaźni) przechodzi tylko przy tej samej mierze co odwrót: wróg w polu w oknie max(dojście + 8,02; 12,03) + załoga do 0,8 × my, podzielone przez naszą siłę, ≤ 1,0. Sprawdzarka: L15b (≥ 2 powroty na parę w 3 dobach = NIE).
2. **Histereza:** ODSTAP w tej samej godzinie nie przesuwa godziny ostatniego ODSTAP; po odwrocie licznik zerowany (nowy wódz znów potrzebuje 2 myśli).
3. **Jedna miara w q:** Żelaźni i armie łupiące liczą swoją siłę jak wroga (`PartyBase.EstimatedStrength`); model gry dalej dostaje swoją miarę.
4. **Pozostały czas:** 12,03 doby tylko przy gotowych machinach / wyrwie i s ≤ 1. Przed gotowością: przygotowania + machiny (tempo budowy modelu gry) + szturm 12 h. Liczony raz na godzinę.
5. **Świadoma tolerancja L7 (q 1,0–1,25 = INFO):** bieżący cel marszu ma histerezę do 1,25 (`SiegeMult(q, current)`), a linia startu liczy okno 8,02 od chwili startu (dojście 0), więc q przy starcie może legalnie wyjść między 1,0 a 1,25. NIE dopiero powyżej 1,25.
6. **Koszt:** linia koszt podaje „razem z modelem gry” (nasze wywołania modelu: armie łupiące, Żelaźni) – L12m INFO; L12 od 2. doby (1. doba – zimna pamięć dróg – INFO). IronSiege: sito linii prostej przed drogą gry, pamięć dróg 2 h. Licznik „model 0” bez zamka. Czekanie na zamek w Judge poza kosztem.
7. **Fabuła ROT** = tylko skrypt rozpoznany przez SiegePhantomLift; drużyny z `DoNotMakeNewDecisions` – osobny dopisek „bez decyzji AI”, liczą się w L7.
8. **Sprawdzarka:** królestwo gracza tylko w L18; L17 DECYZJA tylko gdy wyjścia „q>1” przeważają nad „bez zbiórki/kandydatów/drogi”; rabunek – okno 24 h od pierwszego startu (jak DLL).

## 16. 340 – poprawki po teście 334f (55 dób, przerwany na prośbę Jeffa; log at334f `Armoury-2026-11-06_20-10-38.log`)

Gałąź `noc/340-razem` (334e/f/g + 335 + 336 + 338). Działa: odstąpienia 3 (każde przy q_odw > 1,25), najazdy w puste 80%, Żelaźni rabują 25 razy i mają armię 18 z 53 dób, 0 samobójczych oblężeń, linia Północy jest, 0 wywrotek. Zasady bez zmian: liczby z istniejących modeli, nic z niczego, kanon, odpoczynek armii i nocne obozy zostają, gracza nie sterujemy. Każda zmiana ma klucz (domyślnie TAK, NIE = 334g).

| # | Objaw (dowód z logu) | Przyczyna | Zmiana 340 | Klucz |
|---|---|---|---|---|
| a | L12: średnio maks. 35 ms/h, godzina maks. 101 ms (cel < 5 / < 50) | **Pomiar z linii koszt:** baza 10–14 ms/h. Na dobę (ok. 300 ms): sumy (cel, królestwo) 100–170 ms (8–9 tys. budów), zwiad 30–45, listy 20–27, reszta 85–120. Doby 109550–109565: 20–35 ms/h i godziny 50–101 ms przy **tych samych** sumach, zwiadzie i listach – reszta skacze z ~100 do 450–670 ms/dobę bez związku z żadnym licznikiem (sąd, SCA, obrona, armie). Taki skok przy stałej pracy to pauzy GC całego procesu: zatrzymują wszystkie wątki, więc trafiają też w nasze okna pomiaru (myśl AI idzie równolegle). Okno, w którym przeszło GC, liczyło się do naszego kosztu. Do tego czekanie na zamek w ArmyRaid i IronSiege oraz wywołania gry (jedzenie, siła modelu) liczyły się jako nasz czas. | **Pomiar:** okno z GC (`GC.CollectionCount(0)` przed i po) idzie do osobnego licznika „z GC (340)”. „Średnio / maks.” w linii koszt są bez tych okien, L12 ocenia je, a L12g (INFO) pokazuje koszt z oknami GC. Czekanie na zamek w myśli poza obozem jest poza kosztem, jak w ScorePost. Jedzenie i siła modelu w ArmyRaid i IronSiege idą do „model gry (poza)”. Nowy segment „ścieżki ms”: Besieger, Raider, Defender, BK, sąd, pole (L12p pokazuje najdroższą dobę). **Praca:** (1) wpisy celu na godzinę (TH): stan drużyny, zamiar i żywa droga liczone raz na cel, a nie raz na parę (cel, królestwo). Sumy królestwa to już tylko macierz wojen na indeksach. (2) Pula obiektów sum i wpisów oraz zbiory zwiadu z poprzedniej godziny, czyli mniej śmieci dla GC. (3) Siatka kotwic 48 × 48: najbliższa osada jednostki na żywo bez przeglądu ~1000 osad. (4) Pamięć OurO na myśl drużyny. Kontrola ZFor (co 128. ocena) zostaje, rozbieżności muszą wynosić 0. | – |
| b | L2k / B334-6: korona z armią 36% dób, a w dobach wojny z Północą 4 z 12 | (1) **Jedna miara w q nie objęła głównej drogi:** ScorePost liczył q = Z (EstimatedStrength wroga) / `__3` (miara gry: GetTotalLandStrengthWithFollowers + GetCustomStrength oblężenia kandydatów zbiórki). W logu armie: 945 wobec 658, 3478 wobec 2934, czyli nasza siła była zaniżona o 0–30%. Min q korony przy zbiórce wynosiło 1,05–1,30 (town_S5, town_EW5), tuż nad progiem. (2) **Armia odsieczy władcy:** OB5 daje opcję „zbierz armię i broń” tylko `k.Leader`. Władcą korony jest Joffrey, który w linii 264 jest „bez własnej partii”, więc przez cały bieg „opcje armii władcy 0”. Tywin (głowa rodu, Ręka) odsieczy armią nie zbierał. Bronzegate: zasięg 7043 ≥ próg 5107, zebrane tylko 660–1029 (4 drużyny). W wojnie z Północą (Robb 25,5 tys. w polu wobec 7,9 tys. korony) oblężeń w oknie nie było, co jest zgodne z zasadą lokalną; armia była możliwa tylko jako odsiecz. | (1) `OurO`: nasza siła w mierze wroga – drużyna, cała armia (członkowie w drodze też) albo przy zbiórce drużyna + kandydaci gry z `ThinkParamsCache.PossibleArmyMembersUponArmyCreation`. Ta sama O idzie do KBlocked. Linia koszt: „zbiórka nasza miara: ocen, w oknie/środku, nasza/gry”. (2) `BesiegedRelief.WarLeader`: gdy władca nie ma drużyny w polu, armię odsieczy zbiera głowa najsilniejszego rodu królestwa (`Clan.CurrentTotalStrength`, liczba gry) z własną drużyną, nie najemnik. W koronie jest to Tywin. Koszt wpływu i bramka zasięgu f12 jak u władcy. Linia OB5: „zastępca władcy (340): opcji N”. | `LocalOneMeasure340`, `RelievesWarLeader340` |
| c | L15b: „korona odstąpiła i wróciła 2× pod town_EW5 w 3 doby” | Sprawdzarka liczyła start oblężenia **przed** odstąpieniem tej samej doby (start 20:39:46 → odwrót 20:39:51) jako powrót. Rzeczywisty przebieg: start (q 1,09; „wróg w polu 2848”) → po 5 h odwrót (odsiecz z zamiarem 6227, przyjazd 4,49 doby) → po wygaśnięciu blokady (109569,61) ponowny start 109572 (q 0,52, „wróg 3201”) → po 8 h odwrót (odsiecz 10 896). Start widzi odsiecz jako plotkę (2 × doby: 4,5 → 9 > 8,02, poza oknem), a sąd widzi ją na żywo z zamiarem. Blokada czasowa (przyjazd + 0,5 doby) wygasła, choć odsiecz dalej stała w zasięgu. | Odwrót zapamiętuje drużyny odsieczy z sądu (`RelLeads`). Po czasie blokady start (ScorePost, BK, Żelaźni) jest dalej zakazany, dopóki te drużyny (żywa droga, nie plotka) są w oknie max(dojście + 8,02; 12,03) i razem z załogą (do 0,8 × my) mają > 1,0 × naszej siły. Histereza: odwrót przy > 1,25, powrót przy ≤ 1,0 albo gdy odsiecz wyjdzie z zasięgu (czas dojścia dłuższy niż nasze oblężenie). Pamięć znika, gdy żadna z tych drużyn nie żyje albo nie jest w wojnie. Linia koszt: „odsiecz w zasięgu po odstąpieniu: weta, zwolnione, pamięci”, a odwrót dopisuje „i dalej, dopóki odsiecz (N drużyn) w zasięgu”. Sprawdzarka L15b: powrót = start **po** linii odstąpienia (kolejność linii). Na logu 334f L15b daje TAK (0 powrotów). | `LocalReliefHold340` |
| d | L17: Żelaźni 0 ofert oblężeń wybrzeża; „bez zbiórki 145” | (1) „Bez zbiórki” liczyło też myśli **członków armii** (Wyspy miały armię 18 dób), więc wartość była zawyżona. (2) Lordowie Wysp są na morzu (22 z 48) albo na wyspie. CanLordCreateArmy wymaga lądu, wpływu > 100, głowy rodu, jedzenia i liczebności – na morzu gra nie daje zbiórki nigdy. (3) Armia Wysp na morzu: myśl 334e kończyła się „zajęci”, a gra nie daje jej oblężenia Północy (brak sąsiednich twierdz → 0), więc cel armii się rozmywał. (4) Na lądzie opcja gry na cel kanonu kończyła naszą ocenę („opcja gry kanonu” 1–10 na dobę), mimo to oblężeń 0, bo opcja przegrywała z rabunkami. | (1) Powody „bez zbiórki” w linii koszt (tylko odczyt warunków gry): członek armii, nie głowa rodu, wpływ ≤ 100, jedzenie, mały oddział, najem, kandydaci 0, inne. L17 odejmuje członków armii. (2) **Zbiórka z lądu:** wódz Wysp na morzu, który poza morzem spełnia warunki zbiórki gry, gdy Wyspy nie mają armii, są wolne drużyny Wysp w zasięgu wezwania gry, a najbliższy cel kanonu ma q ≤ 1,0 z ich siłą – idzie do najbliższego przyjaznego portu (GoToSettlement 1,05 × max(najlepsza, 0,5), jak Z1). Zbiórkę w porcie robi gra (CanLordCreateArmy, koszt wpływu gry). (3) **Armia na morzu trzyma cel kanonu** (oblężenie celu armii przy q ≤ 1,25 – histereza bieżącego celu, bez weta, nie przy obronie własnej oblężonej twierdzy). (4) **Pierwszeństwo kanonu:** cel kanonu w oknie (q ≤ 0,8) z wynikiem modelu > 0 dostaje 1,05 × max(najlepszy inny, 0,5), więc wygrywa z rabunkami. Opcja gry na cel kanonu nie kończy już oceny. Weta (teatr, 274, 326b), model 0 i rozejm zostają. Bez złota, wpływu ani ludzi z niczego. | `IronMuster340` |
| e | W335-5: zagrożeń 8, wysłań 3, straż wezwana 4 razy, „bez kandydatów 4”; W335-7: kontakt 3 m bez zagrożenia (swój 5,6 m) | **To nie regresja scalenia.** `git diff noc/335-ochrona-wodza..HEAD` na HeroCaution*, HorsePursuit*, Pendulum333 daje 0 zmian; w LordEscort jest tylko odczyt `Guard335Busy` (mapa, 334e). Przyczyna jest w bitwie: w bitwie z 4 zagrożeniami wszystkie szyki wodza były w szarży (Charge – RBM przy kontakcie). Recenzja 335b wykluczyła szyki w szarży z celu wycofania („bez miejsca 3”) i ze straży („bez kandydatów 4”). W at335b bitwy nie miały pełnej szarży. Test łowcy: wróg miał wodza za cel w 3 m, swój stał 5,6 m obok, przy remisie 1:1 nie było zagrożenia. | (1) Cel wycofania: gdy żadnego szyku poza szarżą nie ma, jest nim tył najbliższego szyku w szarży (nie środek – tam trwa zwarcie). (2) Straż: gdy kandydatów brak, drugie przejście bierze ludzi z szyków w szarży, ale nie z walki wręcz (cel w 3 m). (3) Zagrożenie: wróg w 10 m, który ma wodza za cel, przy swoich ≤ wrogach (remis go nie chroni). Linia bitwy: „340: atak na dowódcę (remis swoich), cel za szykiem w szarży, straż z szarży”. Sprawdzarka W335-9 (INFO). | `CommanderGuardCharging340` |

**Sprawdzarka (340):** `lokalna334e` – L12 bez okien GC (pola „średnio / maks.” od 340), L12g z GC (INFO), L12p ścieżki najdroższej doby (INFO), L15b z kolejnością linii, L15c odsiecz w zasięgu po odstąpieniu (INFO), L2g zbiórka w naszej mierze (INFO), L2z zastępca władcy OB5 (INFO), L17 bez członków armii w „bez zbiórki” oraz powody i dźwignie 340 w opisie. Regex linii koszt toleruje segmenty po „ping-pong”. `wodz335` – W335-9 (INFO). Synteza: +4 przypadki (gc, gcslow, sameday, ironmemb). TAK w lokalna334e, wodz335, balans334, sila336, budynki338.

**Do sprawdzenia w teście 45 dób (Jeff: „45 dni starczy”):**
- L12 < 5 ms/h i < 50 ms bez okien GC; L12g – ile okien z GC i ich ms; L12p – która ścieżka dominuje.
- L2k i B334-6 ≥ 40–50% (zastępca OB5, L2g); L2z > 0 przy oblężonej twierdzy korony.
- L15b 0 oraz L15c: czy blokada odsieczy nie trzyma bez końca.
- L17 ≥ 1 oferta albo powody „bez zbiórki” po odjęciu członków armii; zbiórka w porcie > 0; B334-12.
- W335-5 i W335-9.

Hipoteza GC (a) jest sprawdzalna dopiero w nowym logu: okna z GC wobec reszty kosztu.

## 16b. 340r – poprawki dwóch recenzji 340 (logika, technika i wydajność)

| Punkt | Zmiana | Klucz |
|---|---|---|
| (c) pamięć odsieczy (KBlocked) | Członek armii liczony jako wódz armii (bez powtórzeń) – połączenie odsieczy w armię nie gubi pamięci. Ta sama wiedza co reszta modelu: liczy się drużyna z zamiarem obrony celu teraz (żywa droga, jak wpis Tg w ZFor), z zamiarem widzianym w ostatnich 12 h (MemHours, jak sąd oblężenia; plotka – migawka × 2) albo widziana zwiadem oceniającego (żywa droga). Nasza siła z sojusznikami w drodze (`o + ally` z ZFor) w obu gałęziach. Zwolnienie tylko, gdy odsieczy brak (s = 0), nie przy pierwszej ocenie silniejszego lorda. Twardy koniec: doba odwrotu + 12,03 (HorizonDays), także w czyszczeniu dobowym. Wpisy na godzinę (zwiad – przy ocenie). | `LocalReliefHold340` |
| (c) armia Wysp na morzu | IronKeepSea: blokada królestwa = koniec trzymania celu (było: trzymanie mimo blokady przy q w (1,0; 1,25]). | `IronMuster340` |
| (d) zbiórka w porcie | Kandydaci jak `CanLordCreateArmy` gry (DefaultArmyManagementCalculationModel): wolne drużyny lordów, nie władca, `DoNotMakeNewDecisions`, oblężenie w osadzie, jedzenie, liczebność, `CanLeadParty`, tratwa, droga gry (DistanceHelper) od portu zbiórki < MaximumDistanceToCallToArmy, kolejność gry, limit ceil(0,7 × drużyn − w armiach), próg siły oblężenia 1000; przy SCA – jego sito (wódz 22 zdrowych / 80 siły, członek 10 / 35, co najmniej 2, razem 260). Warunek wodza jak gra (głowa rodu albo pierwsza drużyna rodu, gdy głowa bez drużyny). Nieudana próba (wódz w porcie zbiórki, gra nie dała członków) – bez nowej zbiórki w porcie przez 2 doby, linia „Zelazni zbiorka nieudana”, licznik „w porcie bez armii (wstrzymane)”. Powody „bez zbiórki”: + próg 1000, limit 0,7, sito SCA. | `IronMuster340` |
| (b) start → odstąpienie tej samej doby | Przy zbiórce okno wroga wydłużone o czas zbiórki (najdalszy kandydat gry do wodza, linia prosta / własna prędkość, maks. 12,03 doby) – wróg, który dojdzie w czasie zbiórki, liczy się już przy starcie. Flaga zbiórki (`ThinkParamsCache.WillGatherAnArmy`) tylko, gdy gra podała siłę większą od samej drużyny (ocena spoza myśli – stara flaga, sama drużyna). Nasza siła liczona przed zamkiem (czas jak model gry). | `LocalOneMeasure340` |
| (a) miara kosztu | Linia koszt: „okien z GC N / wszystkich M”; L12p – liczba wywołań tylko z okien bez GC (średnia na wywołanie nie zaniżona). L12: koszt bez GC wiarygodny tylko, gdy okna z GC ≤ 5% okien i mają < 50% ms okien bez GC (wtedy dodatkowo maks godziny z GC < 50); inaczej ocena Z GC (< 5 / < 50). | – |
| drobne | `_warLead` czyszczony przy wczytaniu (BesiegedRelief.Reset); `VisitedRecently` pod `_ironLock`; usunięte nieużywane `Lk()/Ul()`. | – |

**Sprawdzarka (340r):** L12 z GC przy częstych oknach GC; L15d – start i odstąpienie tej samej osady w ≤ 1 dobie (INFO, ≥ 3 na królestwo – NIE; na logu 334f: vlandia 2); L17m – zbiórka w porcie bez armii (INFO, bez armii > połowy zbiórek przy ≥ 4 – NIE). Synteza: gc → L12 NIE (średnio z GC 40), + gcok, gcfreq, gctrust, gcpeak, sameday3, mfail.

**Do obserwacji w teście 45 dób:** W335-9 – jeśli „atak na dowódcę” > ok. 10 × liczba zagrożeń, zawęzić regułę remisu do `foes10 >= 1 && allies5 == 0`; `Rear(f, back)` dla szyku w szarży, który zawraca.
