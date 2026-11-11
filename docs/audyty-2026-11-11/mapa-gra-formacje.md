# MAPA: silnik gry - zachowania formacji (TaleWorlds.MountAndBlade 1.4.8)

Zrodlo: `C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/rally_dec/mb_full.cs` (numery linii = ten plik, skrot `mb:N`).
Zakres: tylko vanilla. RBM nadpisuje spora czesc tych klas (lista w pkt 8) - to, co nizej, jest "podloga", na ktorej stoja RBM i nasze latki.
Audyt tylko z czytania kodu; nic nie uruchamiano.

## 1. Jak formacja wybiera zachowanie (FormationAI, mb:25139-25493)

- Tick co 0.5 s, przesuniety o `0.05*FormationIndex + 0.025*TeamIndex` s (mb:25230-25246, 25360).
- `FindBestBehavior` (mb:25297): dla kazdego zachowania z `WeightFactor > 1e-7` liczy `GetAIWeight() * WeightFactor`; **wygrywa najwyzszy wynik, zero losowosci**.
  - `WeightFactor` ustawia TAKTYKA druzyny (`SetBehaviorWeight<T>`), `GetAiWeight()` to wlasna ocena sytuacji przez zachowanie.
  - Histereza: aktywne zachowanie dostaje mnoznik x2.0 przez pierwsze 5 s, potem liniowo do x1.2 w 10. sekundzie i x1.2 na stale (mb:25191, 25310). Czyli zmiana wymaga przewagi >20% (a tuz po zmianie >100%).
  - Kara za cel poza navmeshem (`NavmeshlessTargetPositionPenalty`, mb:20281) - wygasa po 10 s.
- `ResetBehaviorWeights` zeruje wszystko; potem taktyka zawsze wola `SetDefaultBehaviorWeights` = **Charge 1, PullBack 1, Stop 1, Reserve 1** (mb:38438-38444) i dokłada 1-3 zachowania roli.
- Lista zachowan dodawanych w bitwie polowej (TeamAIGeneral, mb:45659-45693): General/ProtectGeneral, Charge, PullBack, Regroup, Reserve, Retreat, Stop, TacticalCharge, Advance, CautiousAdvance, CavalryScreen, Defend, DefensiveRing, FireFromInfantryCover, Flank, HoldHighGround, HorseArcherSkirmish, MountedSkirmish, ProtectFlank, ScreenedSkirmish, Skirmish, SkirmishBehindFormation, SkirmishLine, Vanguard, ShootFromCliff.
- Wniosek: formacja NIE ma pamieci bitwy ani planu - co 0.5 s wybiera maksimum z kilku liczb. "Rozne warianty" moga przyjsc tylko z (a) innej taktyki druzyny, (b) zmiany wag/sygnalow.

## 2. Co formacja WIE (FormationQuerySystem, mb:42385-43140; TeamQuerySystem mb:43661-44250)

| Sygnal | Definicja (mb) | Odswiezanie | Kto czyta w vanilla |
|---|---|---|---|
| CachedClosestEnemyFormation | najblizsza niepusta formacja wroga po srodku (Formation.CacheClosestEnemyFormation mb:70128) | cache formacji | prawie wszystko - cel Charge/TacticalCharge/ProtectFlank |
| ClosestSignificantlyLargeEnemyFormation | najblizsza o sile >20% naszej (mb:42898) | 1.5 s | Skirmish*, MountedSkirmish, CautiousAdvance, Vanguard |
| FastestSignificantlyLargeEnemyFormation | min czas dojscia (mb:42936) | 1.5 s | TYLKO MountedSkirmish (mb:21970) |
| FormationPower / FormationMeleeFightingPower | suma CharacterPowerCached; strzelcy x0.4 w walce wrecz (mb:70231-70249) | 2.5 s | Charge, PullBack |
| Infantry/Ranged/Cavalry/RangedCavalryUnitRatio, IsXFormation | sklad (mb:42803-42815) | 2.5-5 s | wszedzie |
| HasShield (>=40% z tarcza) | mb:42811 | 5 s | wybor ShieldWall vs Loose |
| **HasThrowing / HasThrowingUnitRatio** | mb:42805, 42812 | 2.5-5 s | **nikt z AI formacji** (tylko 128176, 128407 - poza AI) |
| MovementSpeedMaximum | mb:42817 | 10 s | "czas dojscia" = dystans/predkosc we wszystkich wagach |
| MaximumMissileRange / MissileRangeAdjusted | mb:42818-42846 | 10 s | Skirmish, CautiousAdvance, HorseArcher |
| **IsUnderRangedAttack** | czy KTORYKOLWIEK zolnierz dostal pociskiem w ostatnich 10 s (mb:42864, 69884) | 3 s | Advance i CautiousAdvance -> tylko ShieldWall/Loose (mb:19356, 19905) |
| **UnderRangedAttackRatio** | odsetek trafionych pociskiem w 10 s (mb:42865) | 3 s | TYLKO zmiana szyku na Loose (Defend 20501, DefendSiegeWeapon 20997, ProtectFlank 22165, ShootFromCliff 23605, Vanguard 24868) + odwrot konnych lucznikow (21819 >=0.4; 21970) |
| MakingRangedAttackRatio | odsetek strzelajacych w 10 s (mb:42871) | 3 s | Skirmish/CautiousAdvance ("czy mamy czysty strzal") |
| **CasualtyRatio** | 1 - polegli/(polegli+zywi) (mb:42855) | 10 s | **NIKT** |
| **LocalPowerRatio** | sqrt(moc swoich/wrogow w 30 m), 0.5-1.75 (mb:42854) | 5 s | **NIKT** |
| LocalAllyPower / LocalEnemyPower | w promieniu 30 m (mb:42852-42853) | 5 s | tylko wybor celu machin oblezniczych (mb:35722-35732) |
| **LocalInfantry/Ranged/Cavalry/RangedCavalryUnitRatio** | sklad swoich w 30 m (mb:42847-42850) | 15 s | **NIKT** |
| IsUnderCavalryChargeFromFront | jazda wroga jedzie na nas, <15 s (mb:43015) | 2 s | tylko LineFormation: pikinierzy do 1. szeregu (mb:30172) - NIE zmienia zachowania ani szyku |
| HighGroundCloseToForeseenBattleGround | mb:43006 | 10 s | HoldHighGround, TacticDefensiveEngagement |
| MainFormationReliabilityFactor | 0.5 gdy glowna szarzuje/ucieka, x0.8 gdy w zwarciu (mb:42995) | 5 s | ScreenedSkirmish, SkirmishLine |
| AverageAllyPosition, WeightedAverageEnemyPosition, ClosestEnemyAgent | mb:42780, 43005, 42877 | 5/0.5/1.5 s | MountedSkirmish; reszta poza behaviorami |
| EstimatedDirection/Interval, IdealAverageDisplacement | geometria szyku | 0.2/5 s | Regroup, szyk |
| Team: RemainingPowerRatio / TotalPowerRatio | moc z odjeciem strat / moc startowa (mb:44103, 44132) | 5/10 s | Charge, PullBack, Retreat, wybor taktyki |
| Team: MaxUnderRangedAttackRatio, DeathByRangedCount, DeathCount | trafieni BEZ tarczy w 10 s; licznik zabitych pociskiem (mb:44176, 44215, 15046) | - | TYLKO `TeamAIComponent.CheckIsDefenseApplicable` (mb:45448) |
| Team: EnemyRangedRatio / EnemyCavalryRatio | sklad wroga | 15 s | wagi taktyk (mb:38073, 40210, 40234, 40477, 41524) |
| Team: Left/RightFlankEdgePosition | 50 m w bok od "srodkowej" formacji wroga (mb:43960-43975) | 5 s | BehaviorFlank |
| **Team: AllyRangedRatio / AllyCavalryRatio / EnemyInfantryRatio** | - | 15 s | **NIKT** (poza licznikami pochodnymi) |

**Sygnaly istniejace, a nieuzywane do decyzji ruchu** (najwazniejsze dla polecenia Jeffa):
1. `UnderRangedAttackRatio` / `IsUnderRangedAttack` - sluza WYLACZNIE do zmiany szyku (ShieldWall jesli >=40% tarcz, inaczej Loose). Zadna waga `GetAiWeight` ich nie czyta. Piechota i jazda pod ostrzalem nie przyspieszaja, nie szarzuja na strzelca, nie cofaja sie poza zasieg.
2. `CasualtyRatio` formacji - nieczytany nigdzie. Formacja po stracie 70% ludzi ocenia sytuacje tak samo (poza ubytkiem FormationPower).
3. `LocalPowerRatio` - nieczytany. Formacja nie wie, ze lokalnie jest otoczona/przewazona.
4. `IsUnderCavalryChargeFromFront` - tylko przetasowanie szeregu; brak automatycznego Square/Circle.
5. `HasThrowing` - AI nie odroznia oszczepnikow.
6. `DeathByRangedCount` / `MaxUnderRangedAttackRatio` - jedyne uzycie: OBRONCA porzuca taktyki obronne, gdy `(nasze straty od pociskow / straty wroga od pociskow) ^ (3 * udzial strzelcow wroga) > 1.5` (mb:45448-45480; bramka w TacticDefensiveEngagement 39902, DefensiveLine 40117, DefensiveRing 40392, HoldChokePoint 40968). To JEDYNA w silniku reakcja "przegrywamy wymiane ognia" - i tylko po stronie obroncy, i tylko "przestan stac".

## 3. Zachowania - co robia i jaka maja wage

Format: **Nazwa** (mb:klasa) - waga wlasna `GetAiWeight` | szyk/ogien | logika.

### Natarcie i zwarcie
- **BehaviorCharge** (mb:20059) - waga `CalculateAIWeight` (mb:20099-20218) = bliskosc x stosunek sil x teren x 1.2/1.2 x klasa.
  - bliskosc: piechota 0.1 (>=10 s dojscia) -> 1.0 (<=4 s); jazda 0.1 -> 1.4 w 4 s, ale **spada do 0.1 gdy juz stoi w celu** (mb:20105-20119).
  - stosunek sil: (moja moc wrecz + sojusznicy w 4 s od tego samego celu) / (cel + wrogowie, ktorzy zdaza dojsc), dzielone przez RemainingPowerRatio (clamp 0.2-3) -> **im bardziej druzyna wygrywa, tym MNIEJSZA waga Charge; im bardziej przegrywa, tym wieksza** (mb:20183), clamp 0.1-1.25.
  - teren +-10% (z gory/pod gore), x1.2 gdy 1.5-4 s od celu, x1.2 gdy cel jest zajety kims innym (mb:20206-20215).
  - klasa: wlasna jazda/konni lucznicy x1.5; cel-jazda x0.5; **cel-piechota i cel-strzelcy maja ten sam mnoznik 1.0** (mb:20216). Nie ma zadnej premii za szarze na lucznikow.
  - Rozkaz: `ChargeToTarget(najblizsza formacja)`, FireAtWill, ShieldWall -> Line (mb:20072, 20090-20097).
- **BehaviorTacticalCharge** (mb:24204) - waga 0.8-1.2 x teren x 1.2 x 1.2 x klasa (jw., mb:24472-24525); BEZ stosunku sil.
  - Maszyna stanow Undetermined/Charging/ChargingPast/Reforming/Bracing (mb:24206-24213). **Dla jazdy jest MARTWA**: `CalculateCurrentOrder` dla Cavalry/RangedCavalry robi `ChargeToTarget` i `return` przed maszyna stanow (mb:24345-24349). Przejazd przez szyk, odjazd 20-50 m i ponowna szarza (ChargingPast 5 s -> Reforming 2 s) nigdy sie nie wykonuje - jazda po prostu wbija sie i zostaje w zwarciu.
  - Dla piechoty zyje tylko: Charge gdy szersza od celu, inaczej Move na srodek celu (mb:24396-24409) oraz **Bracing**: gdy jazda wroga jedzie na nas (<6 s, kat <60 st.) piechota przesuwa sie 5 m w strone srodka wroga i stoi do <8 s (mb:24280-24292, 24311-24330, 24374-24378). Szyku nie zmienia.
  - Aktywacja: jazda -> Skein (klin "V"), piechota -> Line; FormWide (mb:24444-24459).
- **BehaviorAdvance** (mb:19225) - waga 1.0 stala. Idzie na przod "srodkowej" formacji wroga (MedianTargetFormation), Line/Wide. Pod ostrzalem (IsUnderRangedAttack) w odleglosci 10-80 m (histereza do 100 m): ShieldWall jesli HasShield, inaczej Loose; po zmianie 5 s na uporzadkowanie (mb:19353-19385). Gdy jazda wroga <30 m jedzie na nas: staje 5 m dalej (mb:19265-19287).
- **BehaviorCautiousAdvance** (mb:19700) - waga 1.0. Piechota oslania NASZYCH lucznikow: stany Shooting/Approaching/PullingBack liczone z `MakingRangedAttackRatio` naszej formacji strzeleckiej (mb:19762-19823); podchodzi dopiero, gdy lucznicy przez 10-15 s nie maja strzalu; cofa sie (stoi), gdy wrog wchodzi na <40% zasiegu. To jedyne zachowanie "piechota czeka, az ostrzal zmiekczy wroga".
- **BehaviorVanguard** (mb:24820) - waga 1.2 (0 bez formacji glownej). Jazda 10 m przed formacja glowna; Loose przy UnderRangedAttackRatio >0.2 i wrogu >40 m (mb:24868).

### Skrzydla i jazda
- **BehaviorFlank** (mb:21494) - waga 1.2 albo 0. Zero gdy: najblizszy wrog celuje w NAS, albo juz nie jestesmy przed jego frontem (iloczyn > -0.5, mb:21541-21547). Ruch: `Move` do punktu 50 m w bok od srodkowej formacji wroga (Team.Left/RightFlankEdgePosition), Line/Deep. Po dojsciu waga spada do 0 i wchodzi TacticalCharge na NAJBLIZSZA formacje. **Nie wybiera celu** (nie szuka lucznikow ani tylow) - to tylko dojazd do bocznego punktu.
- **BehaviorProtectFlank** (mb:22044) - waga 1.2 (0 bez glownej). Stoi (szerokosc glownej + wlasna + 10 m) w bok i z tylu glownej. HoldingFlank -> Charging gdy najblizszy wrog <50 m od posterunku -> Returning gdy odjedzie >60 m (mb:22107-22150). Loose pod ostrzalem (mb:22165). Reakcja czysto na ODLEGLOSC, nie na to, kogo wrog atakuje.
- **BehaviorCavalryScreen** (mb:19935) - waga 1.2 gdy jest "flankujaca" formacja wroga w stozku ~22 st. od kierunku skos-bok-tyl glownej (najsilniejsza), inaczej 0 (mb:19973-19999, 20046). Staje w polowie drogi miedzy glowna a zagrozeniem; Skein/Deep.
- **BehaviorMountedSkirmish** (mb:21861) - waga 1.0 (0.1 gdy wrog w zamku). Krazy po elipsie wokol celu (promien 20/35 m + polowa glebokosci, max zasieg), `_engaging` gasnie gdy `UnderRangedAttackRatio > MakingRangedAttackRatio` albo szybka jazda wroga <4 s (mb:21970) - wtedy wraca do swoich. Formacja >95% konnych lucznikow: zwykly `Charge` (silnikowy taniec agentow, mb:21999-22003).
- **BehaviorHorseArcherSkirmish** (mb:21746) - waga 0.9 (0.09). Dwa tryby: stoi 30 m za swoimi / "rush" na zasieg strzalu; odwrot gdy najblizszy wrog to jazda, <20 m, albo UnderRangedAttackRatio >=0.4 (mb:21819).

### Strzelcy
- **BehaviorSkirmish** (mb:23683) - waga 0.1-1.0 od udzialu strzelcow (pelna od 50%). Scatter/Wide/FireAtWill. Stany: Shooting (stoi), Approaching (brak strzalu 5-10 s -> idzie na wroga), **PullingBack tylko gdy najblizsza duza formacja wroga to PIECHOTA** i jest <40% zasiegu (mb:23758); timer 10 s. **Przed jazda lucznicy sie nie cofaja i nie zmieniaja szyku** - stoja w Scatter.
- **BehaviorSkirmishLine** (mb:23986) - waga = udzial strzelcow x Reliability x (0.5 gdy wrog <4 s ... 1.2 gdy >8 s) (mb:24073-24092). Linia 8 m PRZED glowna piechota, szerokosc 1.2x glownej.
- **BehaviorScreenedSkirmish** (mb:22838) - waga = udzial x Reliability x ustawienie x (1.2 gdy wrog <4 s ... 0.5 gdy >8 s) - lustrzane do SkirmishLine (mb:22925-22950). Staje ZA piechota; HoldYourFire dopoki nie jest na miejscu (mb:22890-22894). Razem daja: lucznicy z przodu gdy wrog daleko -> chowaja sie za piechote gdy podchodzi.
- **BehaviorFireFromInfantryCover** (mb:21412) - waga 2.0; tylko TacticDefensiveRing. Lucznicy w srodku pierscienia, szyk **Square** (jedyne uzycie Square przez AI, mb:21458).
- **BehaviorSkirmishBehindFormation** (10.0, tylko TacticPerimeterDefense), **BehaviorShootFromCliff** (1.0, tylko DefensiveLine.Engage), **BehaviorSparseSkirmish** (2.0, oblezenia).

### Obrona, odwod, odwrot
- **BehaviorDefend** (mb:20446) - waga 1.0. Stoi na pozycji taktycznej; w promieniu 10 m: ShieldWall (tarcze) / Loose pod ostrzalem >20% (mb:20495-20503).
- **BehaviorHoldHighGround** (mb:21666) - waga 1.0. Wzgorze w polowie drogi do wroga; pozycja zamrozona, gdy wrog blizej niz 0.8 zasiegu naszych lucznikow (min 30 m).
- **BehaviorDefensiveRing** (mb:21045) - waga 1.0 gdy jest pozycja; szyk **Circle** (jedyne uzycie Circle przez AI, mb:21078).
- **BehaviorPullBack** (mb:22244) - waga (mb:22285-22339): tylko gdy cel (duza formacja) celuje W NAS i nie jest szybszy o >2 m/s; = (sila wroga, ktory zdazy)/(nasza + sojusznicy) x RemainingPowerRatio/3, clamp 0.1-1.21, x bliskosc (1.0 gdy <4 s, 0.1 gdy >10 s). Rozkaz: 50 m od wroga, Loose. Czyli cofa sie formacja lokalnie przewazona, gdy druzyna ogolnie WYGRYWA; nie cofa sie przed szybsza jazda i nie cofa sie przed ostrzalem (strzelcy nie "celuja" w sensie CachedClosestEnemyFormation, o ile nie sa najblizej).
- **BehaviorReserve** (mb:22410) - waga **0.04** (mb:22484-22508): wygrywa tylko ze Stop (0.01). W praktyce odwod nie istnieje: formacja, ktora nie ma nic lepszego, staje 40 m za glowna.
- **BehaviorRegroup** (mb:22374) - waga 0.1-1.2 od rozproszenia; **w vanilla nikt nie ustawia jej WeightFactor** (brak `SetBehaviorWeight<BehaviorRegroup>` w mb_full.cs) -> martwe. RBM je ozywia (rbmai.cs:410, 779, 1038, 1588, 1912: 1.25-1.75).
- **BehaviorRetreat** (mb:22653) - waga = TotalPowerRatio/RemainingPowerRatio x (0.05 + sqrt(utracona moc/(obecna+utracona))) (mb:22673-22677). WeightFactor dostaje tylko w `TacticCoordinatedRetreat.RunForTheBorder` (mb:38554). Poza ta taktyka formacja nigdy sama sie nie wycofuje z pola - ucieczka to morale pojedynczych agentow.
- **BehaviorStop** (mb:24176) - waga 0.01; ShieldWall/Line.
- **BehaviorGeneral** (1.2) / **BehaviorProtectGeneral** (1.0) - dowodca stoi za glowna formacja, straz za nim (`MovementOrderFollow`, mb:22219).

### Oblezenie (wagi - szczegoly w mapie oblezen)
AssaultWalls 0.25/0.75 (przed/po wylomie; 0.1 przy bramie), UseSiegeMachines 0.75/0.25, WaitForLadders 1.0/0.5, DestroySiegeWeapons 1.0/0.7, ShootFromCastleWalls 10 x udzial strzelcow, DefendCastleKeyPosition 1, RetakeCastleKeyPosition 1, EliminateEnemyInsideCastle 1, SallyOut 10, UseMurderHole 10/0, RetreatToKeep 1, RetreatToCastle 1, DefendSiegeWeapon 1, ShootFromSiegeTower **0 (martwe, mb:23678)**. Charge/TacticalCharge w oblezeniu sa bramkowane "po tej samej stronie muru" (mb:20222-20246, 24527-24553).

## 4. Role -> zachowania w taktykach polowych (kto co moze wybrac)

Wszystkie WeightFactor = 1 (wyjatki zaznaczone). Do kazdej roli dochodzi Charge/PullBack/Stop/Reserve.

| Taktyka (mb) | Piechota | Lucznicy | Jazda L/P | Konni lucznicy |
|---|---|---|---|---|
| FullScaleAttack.Advance (40695) | Advance | SkirmishLine, ScreenedSkirmish, Skirmish | ProtectFlank, CavalryScreen | MountedSkirmish, HorseArcherSkirmish |
| FullScaleAttack.Attack (40738) | Advance, TacticalCharge | ScreenedSkirmish, Skirmish | Flank, TacticalCharge | jw. |
| FrontalCavalryCharge.Advance/Attack (40529/40565) | Advance (+TacticalCharge) | jw. | Advance+Vanguard -> Flank+TacticalCharge | jw. |
| RangedHarrassmentOffensive (41356/41398) | CautiousAdvance (+TacticalCharge) | SkirmishLine, ScreenedSkirmish (+Skirmish) | ProtectFlank, CavalryScreen -> Flank, TacticalCharge | jw. |
| DefensiveLine.Defend/Engage (39941/39988) | Defend (+TacticalCharge w obu) | SkirmishLine, ScreenedSkirmish, Defend x10 -> Skirmish, ScreenedSkirmish, ShootFromCliff | ProtectFlank, CavalryScreen -> Flank, TacticalCharge | jw. |
| DefensiveEngagement (39750/39793) | HoldHighGround, TacticalCharge | jw. | jw. | jw. |
| DefensiveRing (40300) | DefensiveRing | FireFromInfantryCover, Skirmish | ProtectFlank, CavalryScreen | jw. |
| HoldChokePoint (40878) | Defend | SkirmishLine, ScreenedSkirmish, Defend x10 | ProtectFlank, CavalryScreen | jw. |
| CoordinatedRetreat (38493/38543) | Defend, PullBack -> Retreat | ScreenedSkirmish, PullBack x1.5 | ProtectFlank, CavalryScreen, PullBack x1.5 | MountedSkirmish, HorseArcher, PullBack x1.5 |
| Charge (37786) | Charge x10000 - wszyscy | | | |

Obserwacje:
- Jazda ma tylko DWA tryby: "pilnuj skrzydla" (ProtectFlank/CavalryScreen) i "objedz bok i wbij sie w najblizszego" (Flank -> TacticalCharge). Brak trybu "poluj na strzelcow", "uderz w tyl", "poscig", "pozorowana ucieczka", "odwod do rozstrzygajacego uderzenia".
- Piechota w natarciu: Advance z waga 1.0 vs Charge (do ~1.25 x 1.2 x 1.2) - Charge wygrywa dopiero blisko (<4 s) i przy korzystnym stosunku sil. Faza "zwarcia" = wszyscy w ChargeToTarget na najblizsza formacje.
- Przejscie Advance -> Attack i wybor taktyki to juz TacticComponent/TeamAIComponent (osobna mapa).

## 5. Szyki i rozkazy niskiego poziomu

- **ArrangementOrder** (mb:33133): Circle, Column, Line, Loose, Scatter, ShieldWall, Skein, Square. Odstepy: Loose 6, ShieldWall/Square 0, reszta 2 (mb:33184). Ograniczenie biegu: Line 0.8, Circle 0.5, ShieldWall/Square 0.3, Loose/Scatter/Skein 0.9, Column 1.0 (mb:33203-33227). Tarcze: ShieldWall - 1. szereg DefendDown, skrajni Left/Right, reszta nad glowe; Circle/Square - 1. szereg (mb:33286-33322). Wplyw na AI agenta: ShieldWall/Circle/Square -> `BehaviorValueSet.DefensiveArrangementMove`, Charge -> `Charge`, reszta `DefaultMove` (HumanAIComponent.RefreshBehaviorValues mb:19152).
  - Uzycie przez AI: Line (domyslny), ShieldWall (Advance/CautiousAdvance pod ostrzalem, Defend, Stop, oblezenie), Loose (pod ostrzalem bez tarcz, PullBack), Scatter (wszyscy strzelcy), Skein (jazda: TacticalCharge, CavalryScreen, DestroySiegeWeapons).
  - **Square: tylko lucznicy w pierscieniu (mb:21458). Circle: tylko DefensiveRing (mb:21078). Column: AI nigdy (tylko OrderController gracza mb:31803).** Piechota AI nie formuje czworoboku/jeza przeciw jezdzie.
  - `WedgeFormation` (mb:30740) i `CircularSchiltronFormation` (mb:25746) istnieja jako klasy, ale zaden ArrangementOrder ich nie tworzy (mb:33237-33247) - niedostepne bez latki.
- **FacingOrder** (mb:33636): LookAtEnemy / LookAtDirection.
- **FiringOrder** (mb:33746): FireAtWill / HoldYourFire. HoldYourFire w polu tylko: ScreenedSkirmish i FireFromInfantryCover w marszu na pozycje, SkirmishBehindFormation (mb:22894, 21465, 23954); w oblezeniu AssaultWalls (mb:19679). Brak salw, brak "wstrzymaj ogien do X metrow".
- **FormOrder** (mb:33801): Deep / Wide / Wider / Custom(szerokosc).
- **RidingOrder** (mb:35397): Free / Mount / Dismount. **AI nigdy nie wydaje** - jedyne wywolania to gracz (mb:31732, 31742) i kopiowanie przy laczeniu formacji (mb:71308, 71443). Spieszenie jazdy (np. pod mury, w lesie, jak pod Crecy/Agincourt) w silniku AI nie istnieje.
- **MovementOrder** (mb:34079): AttackEntity, Charge, ChargeToTarget, Follow, FollowEntity, Move, Retreat, Stop, Advance, FallBack. **Advance i FallBack: tylko gracz** (mb:31688, 31694; 32227). AI operuje wylacznie Move / Charge / ChargeToTarget / Retreat / Stop / Follow.

## 6. Odpowiedz na pytanie Jeffa na poziomie silnika: "lucznicy masakruja jedna strone - czy druga zaszarzuje jazda?"

Nie, w vanilla nie ma takiego mechanizmu:
1. Zaden `GetAiWeight` nie czyta UnderRangedAttackRatio/DeathByRangedCount (pkt 2).
2. Charge/TacticalCharge celuja w NAJBLIZSZA formacje i nie premiuja strzelcow (mnoznik klasy celu: piechota 1.0 = strzelcy 1.0, mb:20216, 24523).
3. Flank prowadzi do stalego punktu 50 m w bok od srodka wroga, nie do lucznikow (mb:43960, 21505).
4. Jedyna reakcja druzynowa: obronca porzuca obrone (IsDefenseApplicable, mb:45479) i przechodzi do taktyki ofensywnej - ogolnej, bez wskazania celu.
5. Reakcje formacji pod ostrzalem: ShieldWall/Loose (piechota), wycofanie konnych lucznikow, schowanie lucznikow za piechote wg odleglosci wroga (nie wg strat).
6. Lucznicy atakowani przez jazde: Skirmish nie przechodzi w PullingBack (warunek IsInfantryFormation, mb:23758), szyk zostaje Scatter; ochrona zalezy od tego, czy nasza jazda jest akurat w ProtectFlank i wrog przejedzie <50 m od jej posterunku.

## 7. Punkty zaczepienia dla urozmaicenia (fakty z kodu, bez projektu)

- Wagi sa mnozone przez publiczny `WeightFactor` i wybierane przez max - latka na `GetAiWeight` konkretnego zachowania albo dodatkowe `BehaviorComponent` (`FormationAI.AddAiBehavior`, mb:25261) wpina sie bez ruszania reszty. `AddSpecialBehavior` (mb:25285) dziala tylko, gdy aktywny jest BehaviorStop (mb:25396) - slaby punkt zaczepienia.
- `MovementOrderChargeToTarget(Formation)` (mb:34466) pozwala wskazac DOWOLNA formacje jako cel szarzy - silnik to umie, AI tego nie wykorzystuje.
- Martwa maszyna stanow jazdy w TacticalCharge (ChargingPast/Reforming) jest gotowa - blokuje ja tylko wczesny `return` (mb:24345-24349).
- Sygnaly gotowe i policzone, a nieczytane: CasualtyRatio, LocalPowerRatio, UnderRangedAttackRatio, IsUnderCavalryChargeFromFront, HasThrowing, DeathByRangedCount.
- Histereza x2 przez 5 s i tick 0.5 s (mb:25310) ograniczaja "migotanie" - kazde nowe zachowanie dostaje ja za darmo.
- Wybor jest deterministyczny: te same sklady i teren = ta sama bitwa. Losowosc trzeba wniesc na poziomie taktyki/wag.

## 8. Uwaga o RBM (zainstalowany) - co z powyzszego jest nadpisane

`C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/bad3/dec/rbmai.cs` ma latki Harmony na: BehaviorSkirmishLine (3142), BehaviorDefend (3173), BehaviorHoldHighGround (3229), BehaviorScreenedSkirmish (3301), BehaviorCautiousAdvance (3355), BehaviorMountedSkirmish (3493), BehaviorProtectFlank (3769), BehaviorHorseArcherSkirmish (4057), BehaviorPullBack (4075), BehaviorVanguard (4094), BehaviorRegroup (4547), BehaviorAdvance (4613), BehaviorCharge (12942), BehaviorAssaultWalls (9687), BehaviorShootFromCastleWalls (9706), BehaviorUseSiegeMachines (9739), BehaviorWaitForLadders (9750), MovementOrder (4271), Formation.SetMovementOrder (4513), ArrangementOrder.GetShieldDirectionOfUnit (2634).
Wlasne klasy RBM: RBMBehaviorCavalryCharge (12), RBMBehaviorArcherSkirmish (8512), RBMBehaviorEmbolon (8779), RBMBehaviorArcherFlank (8874), RBMBehaviorHorseArcherSkirmish (8986), RBMBehaviorInfantryAttackFlank (9194), RBMBehaviorForwardSkirmish (9338); taktyki RBMTacticDefendSplitInfantry (580), RBMTacticEmbolon (1206), RBMTacticAttackSplitInfantry (1403), RBMTacticAttackSplitSkirmishers (1768).
NIE sa nadpisane przez RBM (wg grep atrybutow): BehaviorTacticalCharge, BehaviorFlank, BehaviorCavalryScreen, BehaviorSkirmish, BehaviorReserve, BehaviorRetreat, BehaviorStop, BehaviorDefensiveRing, BehaviorFireFromInfantryCover, FormationAI.FindBestBehavior, FormationQuerySystem.

## 9. Czego nie udalo sie ustalic

1. Co DOKLADNIE robia latki RBM z pkt 8 (czy sa aktywne w konfiguracji Jeffa, czy RBM AI module jest wlaczony, czy zastepuja wagi czy tylko rozkazy) - widzialem tylko naglowki latek; tresc nalezy do mapy RBM. Do czasu sprawdzenia nie wolno zakladac, ze opis vanilla z pkt 3 obowiazuje dla klas z listy.
2. Czy RBM szuka latek tez przez `AccessTools`/`TargetMethod` poza atrybutem `[HarmonyPatch(typeof(...))]` - grep objal tylko atrybuty, wiec lista "nienadpisanych" moze byc za dluga.
3. Zachowanie pojedynczego agenta w `ChargeToTarget`/`Charge` (wybor celu, taniec konnych lucznikow, uzycie wloczni przy szarzy) siedzi w natywnym silniku (`MBAPI.IMBAgent.*`, BehaviorValueSet) - z dekompilacji widac tylko zestawy parametrow, nie logike.
4. `IBattlePowerCalculationLogic.GetTotalTeamPower` i `CasualtyHandler.GetCasualtyPowerLossOfFormation` (podstawa RemainingPowerRatio, a wiec wag Charge/PullBack/Retreat) - nie czytalem implementacji; nie wiem, czy liczy sprzet/tier i czy ROT/RBM ja podmieniaja.
5. Morale/panika agentow (CommonAIComponent, AgentMoraleInteractionLogic) i ich sprzezenie z zachowaniami formacji - poza zakresem tej mapy; z kodu behaviorow wynika tylko, ze formacja sama nie reaguje na uciekajacych (poza spadkiem CountOfUnits/FormationPower).
6. Nie zweryfikowano w logach gry (Armoury-*.log, at-*-konsola.txt), jakie zachowania faktycznie wygrywaja w bitwach Jeffa - ta mapa jest wylacznie z kodu.
7. Szczegoly geometrii BehaviorDefensiveRing, BehaviorGeneral, SkirmishBehindFormation, ShootFromCliff oraz wszystkich zachowan oblezniczych - przeczytane tylko wagi i szyki, nie pelna logika ruchu.
8. Czy mnoznik histerezy i tick 0.5 s sa modyfikowane przez inne mody (nie sprawdzano latek na FormationAI w pozostalych DLL poza RBM).
