# MAPA (gra-taktyki): SILNIK - taktyki druzyny w bitwie polowej (Bannerlord 1.4.8)

Zrodlo: `C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/rally_dec/mb_full.cs` (dalej `mb:LINIA`).
RBM: `C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/bad3/dec/rbmai.cs` (dalej `rbmai:LINIA`).
Tylko czytanie kodu; nic nie uruchamiano, zadnych logow z gry w tej mapie nie czytano.

## 0. Najwazniejsze w 10 zdaniach

1. Wybor taktyki jest **w pelni deterministyczny**: `TeamAIComponent.MakeDecision` (mb:45487) co 5 s bierze `MaxBy(GetTacticWeight x 1.5 dla biezacej)`. Zadnej losowosci, pamieci, cech dowodcy poza progiem umiejetnosci Tactics. Ten sam sklad wojska = ta sama taktyka = ta sama bitwa.
2. Kazda taktyka polowa ma **najwyzej 2 fazy** (przed stykiem / po styku), przelaczane jednym warunkiem `HasBattleBeenJoined` (glowna piechota <= 5 s drogi od najblizszego szyku wroga). DefensiveRing i HoldChokePoint maja **1 faze**.
3. Kazda taktyka sklada wojsko w ten sam uklad **1-1-2-1** (`AssignTacticFormations1121`, mb:38332): 1 blok piechoty, 1 strzelcy, konnica lewa+prawa, 1 konni strzelcy. **Nie ma drugiej linii ani odwodu** - cala piechota to jeden szyk.
4. Jedyna reakcja DRUZYNY na ostrzal to `CheckIsDefenseApplicable` (mb:45448): **obronca**, ktory traci od pociskow nieproporcjonalnie wiecej niz wrog, dostaje wage 0 na wszystkich taktykach obronnych i przechodzi do ogolnego natarcia. Atakujacy nie ma zadnej reakcji na ostrzal na poziomie taktyki.
5. **Nigdzie w silniku nie ma rozkazu "konnica na strzelcow".** Cel szarzy to zawsze `CachedClosestEnemyFormation` (najblizszy szyk), a flankowanie (`BehaviorFlank`, mb:21503) idzie w punkt 50 m w bok od SRODKOWEGO szyku wroga (`TeamQuerySystem.Left/RightFlankEdgePosition`, mb:43960-43975), nie na jego lucznikow.
6. Konnica w fazie 1 kazdej taktyki (poza FrontalCavalryCharge) **stoi na skrzydlach** (`BehaviorProtectFlank` + `BehaviorCavalryScreen`) i rusza dopiero, gdy piechota jest 5 s od wroga - czyli przez caly okres wymiany ognia lucznikow nie robi nic ofensywnego.
7. `BehaviorReserve` istnieje, ale ma wage 0.04 (mb:22484-22511) - jest tylko zapchajdziura, gdy wszystko inne daje 0. Odwod jako decyzja dowodcy nie istnieje.
8. Pozorowany odwrot nie istnieje. Jest tylko lokalne `BehaviorPullBack` pojedynczego szyku (50 m w tyl, gdy miejscowo przegrywa, mb:22253-22366) i `TacticCoordinatedRetreat` = prawdziwa ucieczka za granice mapy.
9. Teren czytaja WYLACZNIE taktyki obronne (wzgorza, lasy, przewezenia ze sceny: `TacticalPosition`/`TacticalRegion`). Atakujacy nie uzywa terenu wcale.
10. **W grze Jeffa lista taktyk gry jest podmieniana przez RBMAI** (`rbmai:10031-10094`, postfiks `MissionCombatantsLogic.EarlyStart`): w bitwie polowej `ClearTacticOptions()` i nowa, krotsza lista - patrz sekcja 6. Progi Tactics 20/50 z gry przestaja wtedy dzialac, a TacticCharge / FrontalCavalryCharge / DefensiveRing / HoldChokePoint wypadaja z listy (chyba ze dokłada je nasz `Doctrine`/`WarCouncil` - to juz inna mapa).

## 1. TeamAIComponent / TeamAIGeneral

- Tworzenie: `MissionCombatantsLogic.EarlyStart` (mb:83099): FieldBattle -> `new TeamAIGeneral(mission, team)` (mb:83107), domyslne `thinkTimerTime=10`, `applyTimerTime=1` (mb:45619).
- `Team.Tick` wola `TeamAI.Tick(dt)` tylko gdy `HasBots` i strona nie jest w trybie `Mission.RetreatSide` (mb:133610-133622).
- `TeamAIComponent.Tick` (mb:45429-45446):
  - gwardia bez generala -> `BehaviorCharge` (mb:45431-45435);
  - `MakeDecision()` co **5 s** na sztywno (mb:45436-45440). `_thinkTimer` (10 s) jest tylko resetowany w `ResetTactic` i nigdzie nie sprawdzany - martwy;
  - `TickOccasionally()` co `applyTimerTime` = **1 s** -> `CurrentTactic.TickOccasionally()` (mb:45441-45445, 45571-45577).
- `MakeDecision` (mb:45487-45569):
  - brak niepustych szykow wroga -> `TacticCharge` (albo pierwsza z listy) (mb:45494-45532);
  - `CheckIsDefenseApplicable()` (mb:45533);
  - kandydat = `MaxBy(waga x (biezaca ? 1.5 : 1))` (mb:45534);
  - zmiana gdy: brak biezacej; albo kandydat != biezaca i (`CurrentTactic.ResetTacticalPositions()` zwraca false -> zmiana od razu; albo waga kandydata > 1.5 x waga biezacej) (mb:45540-45555). `ResetTacticalPositions` zwraca true tylko w DefensiveLine / DefensiveRing / HoldChokePoint (mb:40109, 40384, 41031) i przy okazji przelicza pozycje. Poniewaz MaxBy juz zawiera x1.5, w praktyce: **zmiana = inna taktyka ma ponad 1.5x wage biezacej**. Brak minimalnego czasu trwania taktyki i brak limitu zmian.
  - Setter `CurrentTactic` (mb:45303-45319): `OnCancel` starej, `OnApply` + natychmiastowy `TickOccasionally` nowej.
- `TeamAIGeneral.OnUnitAddedToFormationForTheFirstTime` (mb:45624-45696) - komplet zachowan szyku w bitwie polowej (SP, galaz `else`): General/ProtectGeneral, Charge, PullBack, Regroup, Reserve, Retreat, Stop, TacticalCharge, Advance, CautiousAdvance, CavalryScreen, Defend, DefensiveRing, FireFromInfantryCover, Flank, HoldHighGround, HorseArcherSkirmish, MountedSkirmish, ProtectFlank, ScreenedSkirmish, Skirmish, SkirmishBehindFormation, SkirmishLine, Vanguard, ShootFromCliff. Taktyka nie wydaje rozkazow wprost - ustawia `WeightFactor` wybranym zachowaniom, a szyk sam wybiera (sekcja 4).
- `TeamAIGeneral.UpdateVariables` (mb:45698-45719) liczy wrogow w 200 m / 40 m od srodka druzyny, ale **nikt tego nie wola** (jedyne trafienie grepem to definicja) - martwy kod.
- `TacticalDecision` / `NotifyTacticalDecision` (mb:45382): w taktykach polowych nieuzywane (jedyne wywolanie mb:42291 to SallyOut).

### Ktore taktyki sa na liscie (gra bez RBM) - mb:83152-83187
`num` = najwyzszy skill **Tactics** wsrod postaci strony (`GetTacticsSkillAmount`, mb:65650; liczy wszystkie `_characters` kombatanta, nie tylko dowodce).

| Tactics | Atakujacy | Obronca |
|---|---|---|
| zawsze | Charge | Charge |
| >= 20 | + FullScaleAttack, RangedHarrassmentOffensive | + FullScaleAttack, DefensiveEngagement, DefensiveLine |
| >= 50 | + FrontalCavalryCharge, CoordinatedRetreat | + FrontalCavalryCharge, DefensiveRing, HoldChokePoint |

Wniosek: w grze bez modow dowodca z Tactics < 20 ma jedna taktyke (F1-F3), obronca nigdy nie ma RangedHarrassment ani odwrotu, atakujacy nigdy nie ma zadnej taktyki obronnej.

## 2. Co taktyki czytaja z TeamQuerySystem (mb:43661-44244)

| Pole | Jak liczone | Odswiezanie | Kto czyta |
|---|---|---|---|
| `InfantryRatio/RangedRatio/CavalryRatio/RangedCavalryRatio` | udzial klas w druzynie (mb:43976-43979) | 15 s | wszystkie wagi |
| `Enemy*Ratio`, `EnemyUnitCount`, `MemberCount` | suma po wrogich druzynach | 15 s / 2 s | CheckIsDefenseApplicable, NotEngagingAdvantage, RangedHarrassment, ChokePoint |
| `TotalPowerRatio` | (moc poczatkowa nasza+sojusznicy +1)/(wroga +1) z `BattlePowerCalculationLogic` (mb:44132) | 10 s | Charge, CoordinatedRetreat |
| `RemainingPowerRatio` | to samo minus straty mocy z `CasualtyHandler` (zabici ORAZ uciekajacy - `OnAgentFleeing`, mb:79961) (mb:44103) | 5 s | wszystkie wagi |
| `TeamPower` | suma `GetFormationPower` | 5 s | czynniki lasu w DefensiveLine/Ring |
| `MaxUnderRangedAttackRatio` | udzial ludzi BEZ TARCZY trafionych pociskiem w ostatnich 10 s, po wszystkich przyjaznych druzynach; **zapamietane maksimum, nigdy nie spada** (mb:44176-44204) | 3 s | tylko CheckIsDefenseApplicable |
| `DeathByRangedCount`, `DeathCount` | liczniki z `Agent.Die` (`b.IsMissile`, mb:15043-15046) | zdarzenie | tylko CheckIsDefenseApplicable |
| `AveragePosition`, `AverageEnemyPosition`, `MedianTargetFormation(Position)` | srodek ciezkosci; "szyk srodkowy" wroga = najblizszy sredniej pozycji wroga (mb:43934-43958) | 5 s / 1 s | pozycje obronne, Flank, Reserve, Skirmish |
| `LeftFlankEdgePosition/RightFlankEdgePosition` | srodkowy szyk wroga +-50 m prostopadle do osi my-wrog (mb:43960-43975) | 5 s | BehaviorFlank |

Czego TeamQuerySystem NIE ma: skad leci ostrzal (ktory szyk wroga strzela), straty na szyk w czasie (tempo), stan amunicji, morale, zmeczenie, pozycja/odslonietosc lucznikow wroga, czy wrog ma konnice w odwodzie. `GetLocalAllyPower/GetLocalEnemyPower` (mb:44220-44243) uzywa tylko MP i SallyOut.

### CheckIsDefenseApplicable (mb:45448-45480) - jedyna reakcja na ostrzal
Tylko `Side == Defender` (atakujacy: zawsze false).
- nasz = clamp((ludzie x MaxUnderRangedAttackRatio + polegli od pociskow) / (ludzie + polegli), 0.05, 1)
- wrog = to samo po druzynach wroga
- `num9 = (nasz / wrog) ^ (3 x (EnemyRangedRatio + EnemyRangedCavalryRatio))`; obrona dozwolona gdy `num9 <= 1.5`.

Skutek: obronca stojacy pod przewazajacym ostrzalem traci wszystkie taktyki obronne (wagi 0: mb:39902, 40117, 40392, 40968) i wybiera FullScaleAttack / FrontalCavalryCharge / Charge - czyli **cala armia rusza naprzod**, a nie "konnica na strzelcow". Poniewaz MaxUnderRangedAttackRatio to maksimum bez opadania, a wykladnik rosnie z udzialem strzelcow wroga, decyzja jest praktycznie jednokierunkowa. Ludzie z tarczami nie licza sie do ostrzalu (mb:44196), ale zabici od pociskow licza sie zawsze.

## 3. Taktyki bitwy polowej - waga, fazy, rozkazy

Wspolne:
- `SetDefaultBehaviorWeights` (mb:38438): kazdy szyk zawsze ma Charge 1, PullBack 1, Stop 1, Reserve 1 - potem taktyka dokłada swoje.
- `HasBattleBeenJoined` (np. mb:40781): true gdy glowna piechota jest w Charge/TacticalCharge, albo dystans do najblizszego szyku wroga / jego `MovementSpeedMaximum` <= 5 s (10 s zeby wrocic do fazy 1). W FrontalCavalryCharge to samo liczone dla konnicy, 7 s / 14 s (mb:40601). Uwaga: "najblizszy szyk wroga" moze byc konnica wroga - wtedy dzielenie przez jej duza predkosc odpala faze 2 z daleka.
- `sqrt(RemainingPowerRatio)`: taktyki ofensywne MNOZA, obronne DZIELA -> silniejszy atakuje, slabszy sie broni (ale bronic moze sie tylko strona Defender).
- `CalculateNotEngagingTacticalAdvantage` (mb:38070): clamp(konnica wroga / nasza, 1, 1.5) ^ (1.5 x max udzialu konnicy) - obrona atrakcyjniejsza, gdy wrog ma wiecej konnicy.
- Rogi (`SoundTacticalHorn`) tylko gdy gracz jest sierzantem w druzynie AI.

### TacticCharge (mb:37786-37868)
- Rozkaz: wszystkim `BehaviorCharge` x10000 (mb:37793-37805). Zero faz, zero rol.
- Waga (mb:37838-37867): `max(A, B)`, skala `1.6 x (max udzialu klasy [+0.33 obronca])`.
  - A = 0 dla atakujacego, gdy Remaining/Total < 0.5; inaczej rosnie liniowo z **udzialem mocy straconej lacznie przez nas i wroga** - pelna wartosc przy stracie 50% (atak) / 67% (obrona) calej mocy na polu.
  - B rosnie z `RemainingPowerRatio` - pelna wartosc przy przewadze ~4:1 (atak) / ~6:1 (obrona).
- Znaczenie: **koncowka kazdej bitwy to ogolna szarza obu stron**, takze strony przegrywajacej (obronca zawsze; atakujacy, dopoki nie stracil ponad polowy wzglednej przewagi).

### TacticFullScaleAttack (mb:40680-40859)
- Waga (mb:40854): `piechota / (1 - konni strzelcy) x sqrt(RemainingPowerRatio)`.
- Faza Advance (mb:40695): piechota `BehaviorAdvance`; strzelcy SkirmishLine + ScreenedSkirmish + Skirmish; konnica L/P `ProtectFlank` + `CavalryScreen`; konni strzelcy MountedSkirmish + HorseArcherSkirmish.
- Faza Attack (mb:40738): piechota Advance + `TacticalCharge`; strzelcy ScreenedSkirmish + Skirmish; konnica `Flank` + `TacticalCharge`; konni strzelcy bez zmian.
- NIE robi: odwodu, skosnego szyku, zatrzymania sie poza zasiegiem lucznikow, rozproszenia pod ostrzalem na poziomie taktyki, wyslania konnicy przed stykiem piechoty.

### TacticFrontalCavalryCharge (mb:40505-40679)
- Uklad 1-1-1-1: cala konnica w JEDNYM szyku (mb:40516-40527).
- Waga (mb:40674): `konnica / (1 - konni strzelcy) x sqrt(RPR)`.
- Advance: piechota Advance; strzelcy jak wyzej; konnica `Advance` + `Vanguard` (jedzie przed piechota). Attack: piechota Advance + TacticalCharge; konnica `Flank` + `TacticalCharge`.
- To jedyna taktyka gry, w ktorej konnica rusza PRZED piechota. Cel nadal: najblizszy szyk.

### TacticRangedHarrassmentOffensive (mb:41342-41528) - tylko atakujacy
- Waga (mb:41515): 0 bez szyku strzelcow; inaczej `strzelcy / (1 - konni strzelcy) x clamp(nasi strzelcy / strzelcy wroga, 0.5, 2) x sqrt(RPR)`.
- Advance: piechota `BehaviorCautiousAdvance` (mb:19700; ciala nie czytalem - z nazwy ostrozne podejscie); strzelcy SkirmishLine + ScreenedSkirmish; konnica ProtectFlank + CavalryScreen.
- Attack: piechota CautiousAdvance + TacticalCharge; strzelcy + Skirmish; konnica Flank + TacticalCharge.
- NIE ma warunku "skonczyly sie strzaly" ani "wrog podszedl do strzelcow" na poziomie taktyki - faza zmienia sie tym samym HasBattleBeenJoined piechoty.

### TacticDefensiveEngagement (mb:39734-39920) - tylko obronca
- Waga (mb:39900): 0 gdy !IsDefenseApplicable albo brak piechoty; inaczej `(piechota + strzelcy) x 1.1 x NotEngaging x lerp(0.7..1 wg odleglosci piechoty od wzniesienia 150..50 m) / sqrt(RPR)`.
- Defend: piechota `BehaviorHoldHighGround` (z `RangedAllyFormation = strzelcy`) + TacticalCharge; strzelcy SkirmishLine + ScreenedSkirmish; konnica ProtectFlank + CavalryScreen.
- Engage: piechota bez zmian; strzelcy Skirmish + ScreenedSkirmish; konnica Flank + TacticalCharge.
- Teren: `FormationQuerySystem.HighGroundCloseToForeseenBattleGround` (mb:43006) = punkt najwiekszego spadku w kwadracie o boku polowy dystansu do wroga - liczone z geometrii, nie wymaga znacznikow sceny.

### TacticDefensiveLine (mb:39921-40283) - tylko obronca
- Wymaga `TacticalPosition` typu HighGround / SpecialMissionPosition albo regionu Forest (3 pozycje syntetyczne: srodek, przedni i tylny skraj, mb:40177-40192). Kwalifikacja (mb:40132): do 20 m od piechoty albo blizej niz polowa dystansu do wroga, zwrocona ku wrogowi (dot > 0.5) lub IsInsurmountable.
- Waga (mb:40115-40129): `(piechota + strzelcy) x 1.2 x ocenaPozycji x NotEngaging / sqrt(RPR)`; ocena (mb:40151): spadek 1..1.5, dopasowanie szerokosci 0.67..1, nie do obejscia x1.3, polka dla strzelcow (Cliff) 1..1.5, las: czynnik strzelcow i konnicy (las lepszy, gdy wrog ma duzo strzelcow/konnicy, mb:40194-40244), odleglosc 0.7..1. SpecialMissionPosition = 100.
- Defend: piechota `BehaviorDefend` na pozycji + TacticalCharge; strzelcy SkirmishLine + ScreenedSkirmish (+ `BehaviorDefend` x10 na polce, jesli jest); konnica ProtectFlank + CavalryScreen.
- Engage: strzelcy Skirmish + ScreenedSkirmish (+ ShootFromCliff); konnica Flank + TacticalCharge.

### TacticDefensiveRing (mb:40284-40504) - tylko obronca
- Wymaga piechoty i strzelcow, kola piechoty wiekszego niz kwadrat strzelcow (mb:40396-40402) oraz pozycji HighGround **IsInsurmountable** albo regionu DifficultTerrain / Opening (mb:40414, 40442).
- Waga (mb:40411): `min(piechota, strzelcy) x 2 x 1.5 x ocena x NotEngaging / sqrt(RPR)`.
- JEDNA faza `Defend` (mb:40300): piechota `BehaviorDefensiveRing`; strzelcy `FireFromInfantryCover` + Skirmish; konnica ProtectFlank + CavalryScreen **na stale** (nigdy Flank/TacticalCharge); piechota bez TacticalCharge (zostaje jej tylko domyslny Charge).

### TacticHoldChokePoint (mb:40860-41072) - tylko obronca
- Wymaga `TacticalPosition` typu ChokePoint wezszego niz maks. szerokosc piechoty (mb:40987-41009).
- Waga (mb:40966): `(piechota + min(piechota, strzelcy)) x clamp(wrog/my liczebnie, 0.33, 3) x ocena x NotEngaging x 1.3 / sqrt(RPR)` - jedyna taktyka, ktora wprost nagradza bycie w mniejszosci.
- JEDNA faza: piechota `BehaviorDefend` w przewezeniu (bez TacticalCharge); strzelcy SkirmishLine/ScreenedSkirmish (+ Defend x10 na polce); konnica ProtectFlank + CavalryScreen na stale.

### TacticCoordinatedRetreat (mb:38477-38651) - tylko atakujacy, Tactics >= 50
- Waga (mb:38629): `LinearExtrapolation(0, maxUdzialKlasy, clamp(Total/Remaining, 0, 4) / 2) x lerp(0.8..1.2 wg naszej konnicy wzgledem wroga) x min(1, sqrt(RPR))`. Rosnie, gdy stosunek sil pogorszyl sie wzgledem poczatku; przy pogorszeniu 2x zrownuje sie mniej wiecej z FullScaleAttack.
- Faza `OrganizedRetreat` (mb:38493): piechota `BehaviorDefend` w najblizszym punkcie ucieczki na granicy mapy + PullBack; strzelcy ScreenedSkirmish + PullBack 1.5; konnica ProtectFlank + CavalryScreen + PullBack 1.5 (oslona).
- Gdy cala piechota jest <= 75 m od punktu (mb:38559) -> `RunForTheBorder`: wszystkim `BehaviorRetreat` (mb:38543).
- To prawdziwe zejscie z pola, nie manewr; nie ma powrotu do walki inaczej niz przez zmiane taktyki wagami.

### Pozostale w pliku (nie bitwa polowa)
TacticPerimeterDefense (mb:41073; waga 10 gdy jest FleePosition obroncy - nie jest dodawana w `EarlyStart` dla FieldBattle), TacticStop, TacticSergeantMPBotTactic, SallyOut*, BreachWalls, DefendCastle - poza zakresem tej mapy.

## 4. Poziom szyku - jak rozkaz taktyki zamienia sie w ruch (skrot potrzebny do oceny taktyk)

- `FormationAI.Tick` co 0.5 s -> `FindBestBehavior` (mb:25297-25338): `max(GetAIWeight x WeightFactor)`, aktywne zachowanie ma premie x1.2..2 (gasnaca przez 10 s `PreserveExpireTime`, mb:25191, 25310). Tez deterministyczne.
- Wagi wlasne zachowan (stale, wiec o wyborze decyduja nieliczne zmienne):
  - Advance 1 (mb:19389), CautiousAdvance 1 (mb:19930), ProtectFlank 1.2 (mb:22199), Vanguard 1.2 (mb:24901), HorseArcherSkirmish 0.9 / 0.09 (mb:21759), MountedSkirmish 1 / 0.1 (mb:22035).
  - `CavalryScreen` 1.2 tylko gdy wykryto konnice wroga oskrzydlajaca (`_flankingEnemyCavalryFormation`), inaczej 0 (mb:20046-20057).
  - `Flank` 1.2 tylko gdy najblizszy szyk wroga NIE celuje w nas i jest zwrocony ku naszej stronie (dot <= -0.5); inaczej 0 (mb:21537-21568).
  - `TacticalCharge` 0.8..1.2 x wysokosc 0.9..1.1 x 1.2 x 1.2 x czynnik klasy (nasza konnica x1.5, cel-konnica x0.5) (mb:24472-24525). **Nie porownuje sil.** Cel = najblizszy szyk.
  - `Charge` (mb:20099-20218): bliskosc x lokalny stosunek mocy wrecz (my + sojusznicy przy tym samym celu) / (cel + wrogowie w poblizu), **dzielony** przez clamp(RemainingPowerRatio, 0.2, 3) - druzyna ogolnie silniejsza szarzuje domyslnym Charge MNIEJ chetnie; zakres 0.1..1.25.
  - `PullBack` (mb:22285-22366): tylko gdy wrog celujacy w nas nie jest szybszy o ponad 2 m/s; waga ~ lokalna przewaga wroga x RPR/3 x bliskosc. Cofa 50 m od najblizszego wroga w szyku luznym.
  - `Reserve` 0.04; pozycja 40 m + glebokosc za szykiem glownym (mb:22419-22428).
  - `Retreat` (mb:22673): waga ze strat szyku, ale WeightFactor ma tylko w CoordinatedRetreat.
- Reakcje na ostrzal na poziomie szyku (to jest CALOSC reakcji silnika):
  - piechota w `BehaviorAdvance`: `IsUnderRangedAttack` i 10..80 m od wroga -> ShieldWall (z tarczami) albo Loose (bez) (mb:19352-19384);
  - `BehaviorDefend`, `BehaviorDefendSiegeWeapon`, `BehaviorShootFromCliff`: `UnderRangedAttackRatio > 0.2` i wrog dalej niz 10 m -> Loose (mb:20501, 20997, 23605);
  - `BehaviorProtectFlank` (trzymajac skrzydlo) i `BehaviorVanguard`: `UnderRangedAttackRatio > 0.2` i wrog dalej niz 40 m -> Loose (mb:22165, 24868). Konnica pod ostrzalem **rozluznia szyk i stoi dalej**;
  - `BehaviorHorseArcherSkirmish` (konni strzelcy, tryb "rush"): przy `UnderRangedAttackRatio >= 0.4`, wrogiej konnicy jako najblizszym szyku albo wrogu <= 20 m wracaja 30 m za wlasne szyki (mb:21819-21824). Pieszego `BehaviorSkirmish` (mb:23683-23899) nie czytalem;
  - `BehaviorMountedSkirmish`: przerywa krazenie, gdy dostaje wiecej niz zadaje (mb:21970).
  `UnderRangedAttackRatio` szyku = udzial ludzi trafionych pociskiem w 10 s (mb:42865-42870).
- `BehaviorTacticalCharge.CalculateCurrentOrder` (mb:24337-24349): dla konnicy i konnych strzelcow od razu `MovementOrderChargeToTarget(najblizszy)` i `return` - **stany Charging -> ChargingPast -> Reforming (przejazd przez szyk i ponowne uformowanie, mb:24294-24310, 24410-24433) sa dla konnicy nieosiagalne**. Maszyna stanow dziala tylko dla piechoty i w praktyce sprowadza sie do `Bracing` (piechota zatrzymuje sie 5 m w przod, gdy konnica wroga jedzie na nia, mb:24280-24292, 24311-24331).
- `ClosestSignificantlyLargeEnemyFormation` (mb:42898-42935): najblizszy szyk o mocy > 20% naszej (bezwzglednie albo wzglednie do druzyny) - maly oddzial nie odciaga uwagi.

## 5. Czego silnik NIE robi (lista do porownania z historia)

| Zjawisko historyczne | Stan w silniku | Dowod |
|---|---|---|
| Szarza konnicy na strzelcow, ktorzy zadaja straty (np. proba pod Crecy/Agincourt, udana pod Patay) | BRAK. Cel = najblizszy szyk; flanka = +-50 m od srodkowego szyku | mb:20072, 24347, 21505, 43960 |
| Reakcja atakujacego na ostrzal (przyspieszyc, rozproszyc, cofnac sie poza zasieg) | BRAK na poziomie taktyki; tylko ShieldWall/Loose szyku | mb:45450, 19352 |
| Odwod / druga linia / wymiana linii (triplex acies, odwody Hastings, Bouvines) | BRAK: 1 blok piechoty, Reserve 0.04 | mb:38332-38334, 22484 |
| Pozorowany odwrot (Hastings, Mongolowie, Kresy) | BRAK | mb:22253, 38493 |
| Zasadzka / ukryty oddzial / uderzenie z lasu | BRAK (las tylko jako pozycja obronna) | mb:40177 |
| Kowadlo i mlot jako plan (piechota wiaze, konnica w tyl) | CZESCIOWO i przypadkiem: faza 2 daje Flank + TacticalCharge, ale Flank ma wage tylko gdy wrog nie celuje w konnice, a punkt docelowy to bok srodkowego szyku | mb:40758-40771, 21537 |
| Konnica rozbija konnice wroga zanim uderzy w piechote | BRAK jako cel; CavalryScreen tylko reaguje na oskrzydlenie | mb:20046 |
| Poscig za uciekajacymi / zakaz poscigu | BRAK decyzji; koncowka = TacticCharge | mb:37838 |
| Wybor terenu przez atakujacego (slonce, stok, bloto) | BRAK; teren czytaja tylko taktyki obroncy | mb:39916, 40151 |
| Skosny szyk / wzmocnione skrzydlo (Leuktry) | BRAK: konnica dzielona na pol, piechota niepodzielna | mb:38334 |
| Zmiana planu po smierci dowodcy / zlamaniu skrzydla | BRAK; liczy sie tylko RemainingPowerRatio calej druzyny | mb:44103 |
| Obronca kontratakuje, gdy wrog sie wykrwawi | TAK posrednio: `/sqrt(RPR)` obniza obrone, `xsqrt(RPR)` podnosi atak | mb:39918, 40857 |
| Obronca porzuca pozycje pod ostrzalem | TAK, ale jako natarcie calej armii i nieodwracalnie | mb:45448-45480, 44203 |
| Losowosc / osobowosc dowodcy | BRAK (poza progami Tactics 20/50) | mb:45534, 83159-83186 |

## 6. Uwaga krytyczna: RBMAI podmienia liste taktyk (stan w grze Jeffa)

`rbmai:10031-10094` (postfiks `MissionCombatantsLogic.EarlyStart`), tylko FieldBattle, kazda druzyna z TeamAI:
- **Atakujacy**: `ClearTacticOptions()`; wg kultury dowodcy: `empire` -> RBMTacticEmbolon; `aserai`/`darshi` -> RBMTacticAttackSplitSkirmishers; `sturgia`/`nord` -> RBMTacticAttackSplitInfantry; `battania` -> RBMTacticAttackSplitArchers; `khuzait` -> TacticRangedHarrassmentOffensive; zawsze + TacticFullScaleAttack + TacticCoordinatedRetreat.
- **Obronca**: `ClearTacticOptions()`; `battania` -> RBMTacticDefendSplitArchers; zawsze TacticDefensiveEngagement + TacticDefensiveLine; `sturgia`/`nord` -> RBMTacticDefendSplitInfantry; zawsze + TacticFullScaleAttack + TacticCoordinatedRetreat.
- `TacticCoordinatedRetreat.GetTacticWeight` ma prefiks zwracajacy **zawsze 0** (rbmai:10096-10156); przy RPR < 0.1 i samej konnicy po stronie zeruje morale calej strony.
- Skutki: (a) progi Tactics 20/50 nie dzialaja; (b) TacticCharge, TacticFrontalCavalryCharge, DefensiveRing, HoldChokePoint nie sa na liscie RBM; (c) kultura bez dopasowanego id ma dokladnie JEDNA realna taktyke w ataku (FullScaleAttack) i 2-3 w obronie; (d) dobor idzie po vanillowych id kultur - w ROT `battania` = Polnoc, wiec to Polnoc dostaje "SplitArchers"; czy pozostale id (`empire`, `sturgia`, `khuzait`, `aserai`, `nord`, `darshi`) w ogole wystepuja u dowodcow ROT - nie sprawdzalem.
- RBM dodaje tez wlasne zachowania szykow (rbmai:9961-9977: RBMBehaviorArcherSkirmish, ForwardSkirmish, InfantryAttackFlank, CavalryCharge, Embolon, ArcherFlank, HorseArcherSkirmish) i latki na Advance/Attack/Defend/Engage taktyk gry (rbmai:9596, 10159-10335, 12689, 12790) - opis faz z sekcji 3 jest wiec **baza**, na ktora RBM naklada zmiany.
- Nasz mod: `Armoury/src/Doctrine.cs`, `Doctrine.Hold291.cs`, `Doctrine.S2.cs`, `WarCouncil.cs` (worktree n367c) - postfiksy `GetTacticWeight` z Priority.Last po `com.rbmai` i `WarCouncil.TryAdd` dokladajacy taktyki (Doctrine.cs:186-218, 444-446). Nie analizowalem ich tutaj - to zakres innej mapy.

## 7. Czego nie udalo sie ustalic

1. Czy modul RBMAI jest faktycznie wlaczony w konfiguracji RBM Jeffa (DLL jest w `Modules/RBM/bin/Win64_Shipping_Client/RBMAI.dll`; komentarze w Doctrine.cs zakladaja, ze tak; pliku konfiguracji RBM ani logu startu nie czytalem).
2. Jakie kultury maja dowodcy w ROT z punktu widzenia `BasicCulture.StringId` (poza `battania` = Polnoc z CLAUDE.md) - czyli ktore galezie kulturowe RBM w ogole sie odpalaja.
3. Wag i faz taktyk RBM (RBMTactic* - rbmai:328-2068) oraz dokladnej tresci latek RBM na taktyki gry - widzialem tylko naglowki i liste.
4. Jak czesto w praktyce wygrywa ktora taktyka - nie czytalem logow (`Armoury-*.log`, `at-*-konsola.txt`); wszystko powyzej to kod, nie obserwacja.
5. Czy sceny bitew ROT/1.4.8 maja znaczniki `TacticalPosition` / `TacticalRegion` (HighGround, ChokePoint, Forest, Cliff). Bez nich DefensiveLine, DefensiveRing i HoldChokePoint maja wage 0 i obroncy zostaje DefensiveEngagement.
6. `MBMath.LinearExtrapolation` - nie znalazlem ciala (TaleWorlds.Library nie ma w mb_full.cs); zakladam ekstrapolacje bez obciecia, wiec wagi Charge i CoordinatedRetreat moga przekraczac podana "pelna wartosc".
7. `BehaviorComponent.ResetBehavior` - zakladam, ze zeruje WeightFactor (nie czytalem ciala); na tym opiera sie stwierdzenie, ze Retreat i zachowania spoza listy taktyki sa wylaczone.
8. Wnetrza zachowan, ktorych tu tylko dotknalem (Skirmish pieszych strzelcow, CautiousAdvance, CavalryScreen - wykrywanie oskrzydlenia, HoldHighGround, SkirmishLine, ScreenedSkirmish, Vanguard, HorseArcherSkirmish) i zachowanie pojedynczego agenta w `MovementOrderChargeToTarget` (czy konny sam robi przejazdy) - to poziom agenta/silnika natywnego.
9. `BattlePowerCalculationLogic.CalculateTeamPowers` (mb:79822+) - z czego dokladnie liczona jest moc poczatkowa (czy z posilkami spoza pola); wplywa na Total/RemainingPowerRatio.
10. Czy druzyna gracza z TeamAI (gracz jako sierzant / oddanie dowodzenia) idzie ta sama sciezka w kampanii 1.4.8 - z kodu wynika, ze tak (mb:83153-83159), nie weryfikowalem w grze.
