# MAPA: cudze mody w bitwie i oblezeniu - kto jest wlascicielem ktorej decyzji AI

Audyt tylko-do-odczytu, 11.11. Dekompilacje zrobione na nowo (decall.exe) do
`C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/bad3/dec/`
(rbmai.cs, rbm.cs, rbmcombat.cs, rbmconfig.cs, vais.cs, epic.cs, dr.cs, ib.cs, rotatm.cs, rb.cs, artem.cs, dismem.cs, naval.cs, bkrot.cs, bk.cs, dte.cs, scai*.cs, sow.cs).
Odwolania `rbmai.cs:N` = ten katalog. ROT = `scratchpad/rot_all.cs`.

## 0. Wniosek w trzech zdaniach

1. **Wlascicielem calego AI bitwy polowej jest RBM (RBMAI.dll, Harmony id `com.rbmai`)**: lista taktyk druzyny, wagi zachowan szykow w kazdej fazie, cele szarzy konnicy, ruch lucznikow, szyk (mur tarcz / luzny / linia), AI pojedynczego zolnierza (blok, parowanie, celnosc), postura i stamina. Gra (TaleWorlds) zostaje wlascicielem tylko petli wyboru (`TeamAIComponent.MakeDecision`, `FormationAI.FindBestBehavior`) i oblezenia.
2. **Armoury siedzi warstwa NA RBM** (latki `Priority.Last, after com.rbmai`: WarCouncil, Doctrine, CloseRanks, MarchToContact, Pendulum333, HorsePursuit, RoutHold...) - to jedyny inny mod, ktory naprawde zmienia decyzje taktyczne. Reszta modow z listy Jeffa to dzwiek, animacje, sprzet, posilki albo kampania.
3. **Oblezenie: plan szturmu nalezy do gry** (TeamAISiegeComponent / TacticBreachWalls / TacticDefendCastle / SiegeLane) - RBM tylko go "rozluznia", a VanillaAiSiegesforRBM + EpicSieges wiekszosc tych zmian RBM zdejmuja przy kazdej misji. Zostaje kilka resztek RBM (morale obroncow, punkty lucznikow, spawn bez koni) - patrz 3.

## 1. Co jest wlaczone u Jeffa (LauncherData.xml + konfiguracje)

Wlaczone i dotykajace misji: RBM, RBM_WS (tylko XML), ROT-Core/Content/Dragon, NavalDLC, DynamicReinforcements, EpicSieges, VanillaAISiegesforRBM, ImmersiveBattlefields, ROTBattleAtmosphere, ArtemsLivelyAnimations, DismembermentPlus, DTE, BannerKings.Redux, BKROTPatch, Armoury.
Wylaczone (IsSelected=false): AIInfluence, BattlefieldImmersion.ScoutingFog, MoreBloodPoolMod, FastMode, BetterExceptionWindow, CCsBanners, PrisonIsNotFun, VoiceActingPatch, ROT_AIInfluence_Compat.
Bez wplywu na misje (sprawdzone grepem po latkach/MissionLogic/modelach): RealisticBannerlord (same CampaignBehavior i modele kampanii: sezony, zaopatrzenie, SiegeAttrition na mapie, medycyna - rb.cs lista typow), StrategicCampaignAI145, BetterTime, Spoils of War (RealisticLoot), Diplomacy.

`Documents/.../Configs/RBM/config.xml` (zapis 11.11 13:27):
- RBMAI Enabled=1, **PostureEnabled=1, StaminaEnabled=1**, PlayerPostureMultiplier=0 (indeks listy, nie mnoznik zero - nie ustalalem mapowania), PostureGUI=1, **VanillaCombatAi=0** (czyli AI zolnierza wg RBM), **KeepBattleEnabled=1**, HitStopEnabled=1.
- RBMCombat Enabled=1, ArmorMultiplier=2, TroopOverhaulActive=1 (dotyczy vanillowych NPC; ROT ma wlasne drzewa), RealisticRangedReload=1, RealisticArrowArc=0, HorsePriceModifier=0.2.
- RBMTournament=1.
`ModSettings/Global/VanillaAiSiegesforRBM/VanillaAiSiegesforRBM.json`: wszystko `true` OPROCZ **RestoreAgentMoraleInteractionLogic=false**. Plik ma 3 klucze, ktorych kod juz nie zna (RestoreMission, RestoreLadderQueueManager, RestoreBehaviorDefendCastleKeyPosition), a brakuje mu RestoreSiegeLadder / RestoreSiegeTower - te biora domyslne `true` z kodu (vais.cs, klasa SiegeSettings).
`ModSettings/Global/EpicSieges/` - **brak pliku** -> EnableVanillaBehavior domyslnie true.
`DynamicReinforcements_v1.json`: Mode=1 (Balanced), **BattleSize=1 (Large = x1.5)**, AutonomousTeam=true, SeparateTeamPerLord=false, AllowSieges=true, MaxAgentsOnField=1500, MaxWaveSize=200, DelayBetweenWaves=30 s, WavesByTroopType=true.
`BannerlordConfig.txt`: BattleSize=6 (najwyzszy stopien), ReinforcementWaveCount=3. Z latka DR `BannerlordConfig.GetRealBattleSize` (dr.cs:3233) x1.5, sufit 2000.
`RealmOfThrones_v1.json`: RBMIntegration=true (tylko dane sprzetu, patrz 4), DragonTageting=true, FriendlyFire=true, elephantAttackChance=0.001, FormationOrderCallouts=true.
`ImmersiveBattlefields_MCM.json`: rogi i glosy; HeyJustDismount=false, ChargeShout=false.

## 2. RBM w bitwie polowej - konkretne klasy i latki

**Jak sie wpina**: `RBM.SubModule.ApplyHarmonyPatches` (rbm.cs:44) robi tylko `RBMAiPatcher.FirstPatch` (EarlyStart). Pelne `PatchAll` idzie **przy kazdej misji** z `RBMAIPatchLogic.EarlyStart` -> `RBMAiPatcher.DoPatching` = `UnpatchAll("com.rbmai")` + `PatchAll()` (rbmai.cs:10454, rbm.cs:215). Skutek: latki RBM sa zakladane od nowa w kazdej bitwie; latki obce "after com.rbmai" (Armoury, VAIS, EpicSieges) sa wtedy przeliczane - VAIS/EpicSieges zdejmuja swoje cele ponownie w `MissionCombatantsLogic.AfterStart` (po EarlyStart), wiec kolejnosc sie zgadza.

### 2.1 Lista taktyk druzyny (kto w ogole moze cos wymyslic)
`Tactics.EarlyStartPatch.Postfix` na `MissionCombatantsLogic.EarlyStart` (rbmai.cs:10031-10093), tylko `MissionTeamAIType == FieldBattle`, dla KAZDEJ druzyny z TeamAI (takze gracza): `ClearTacticOptions()` i staly zestaw wedlug **StringId kultury dowodcy strony**:
- Atakujacy: zawsze `TacticFullScaleAttack` + `TacticCoordinatedRetreat`; dodatkowo `empire` -> RBMTacticEmbolon, `aserai`/`darshi` -> RBMTacticAttackSplitSkirmishers, `sturgia`/`nord` -> RBMTacticAttackSplitInfantry, `battania` -> RBMTacticAttackSplitArchers, `khuzait` -> TacticRangedHarrassmentOffensive.
- Obronca: zawsze `TacticDefensiveEngagement`, `TacticDefensiveLine`, `TacticFullScaleAttack`, `TacticCoordinatedRetreat`; `battania` -> + RBMTacticDefendSplitArchers, `sturgia`/`nord` -> + RBMTacticDefendSplitInfantry.
- **Nigdy na liscie RBM**: TacticFrontalCavalryCharge, TacticCharge, TacticDefensiveRing, TacticHoldChokePoint, TacticRangedHarrassment dla nie-khuzait (choc RBM ma na nie latki - rbmai.cs:10159, 10209 - martwe bez Armoury).
- Umiejetnosc Tactics dowodcy (gra: progi 20 / 50, mb_full.cs:83161, 83174) pod RBM **nie ma zadnego wplywu**.

**Pulapka ROT**: RBM porownuje vanillowe id, a ROT je nadpisal. Faktyczne przypisanie (nazwy wg tabeli w Armoury `Doctrine.cs:85-113`): `battania` = **Polnoc** dostaje rozdzielonych lucznikow (atak i obrona), `sturgia` = **Zelazne Wyspy** i `nord` = Lorath okrazenie piechota, `aserai` = **Dorne** harcownikow, `khuzait` = **Dothrakowie** nekanie strzeleckie, `empire` = **Braavos** klin ciezkiej jazdy (Embolon). Wszystkie pozostale kultury ROT (vlandia = Westerlands/Reach, river, vale, reach, stormlands, crownlands, dragonstone, freefolk, wildlings, nightswatch, skagosi, ghiscari, volantine itd.) maja u RBM **jedna taktyke ataku i dwie obrony** - to jest zrodlo "bitwa ciagle taka sama".

Wagi taktyk RBM (GetTacticWeight): SplitArchers 10 gdy strzelcy > 20% (rbmai.cs:559-570, 1186); SplitInfantry atak 10 / obrona 5 gdy piechota > 50% (rbmai.cs:1746, 934); Embolon 5 gdy konnica > 20% i >= 60% ciezkiej (poziom >= 21, zbroja konia > 40; rbmai.cs:1358-1394), inaczej 0.01; SplitSkirmishers tylko gdy nasza piechota >= 1.25 x sily piechoty wroga i >= 60 ludzi (rbmai.cs:2068+). Waga 10 przy wadze gry rzedu 1 oznacza: jesli kultura ma taktyke specjalna i warunek skladu jest spelniony, **wybierze ja zawsze** - wariantu nie ma takze tam.
`TacticCoordinatedRetreat.GetTacticWeight` -> prefiks zwraca **zawsze 0** (rbmai.cs:10096-10156): AI nigdy nie robi zorganizowanego odwrotu; jedyny efekt to `SetMorale(0)` calej stronie, gdy `RemainingPowerRatio < 0.1` i strona nie ma juz ani jednego pieszego.
`TacticFullScaleAttack.GetTacticWeight` postfiks: obronca z piechota > 90% -> waga 100 (sam idzie do ataku) (rbmai.cs:12868-12881).
`TacticDefensiveLine.IsTacticalPositionEligible` postfiks: odrzuca pozycje blizej niz 100 m od granicy mapy (rbmai.cs:12692).

### 2.2 Fazy bitwy - wagi zachowan szykow (to jest "scenariusz")
RBM dopisuje kazdemu szykowi 7 wlasnych zachowan (`TeamAIGeneral.OnUnitAddedToFormationForTheFirstTime` postfiks, rbmai.cs:9961-9976): RBMBehaviorArcherSkirmish, ForwardSkirmish, InfantryAttackFlank, CavalryCharge, Embolon, ArcherFlank, HorseArcherSkirmish.
Przelacznik faz = `Utilities.HasBattleBeenJoined` (rbmai.cs:10601): glowna piechota ma >= 35% ludzi w zwarciu ALBO odleglosc mediana-mediana do "znaczacego" szyku wroga (piechota lub lucznicy) <= **75 m** (+ polowy glebokosci obu; histereza +5 m). Jedno kryterium dla wszystkich taktyk (prefiksy/postfiksy HasBattleBeenJoined: rbmai.cs:9600, 12708, 12862, 10195 [konnica 125 m], 10270).

Faza 1 "podejscie" (postfiksy `Advance`/`Defend`: FullScaleAttack rbmai.cs:12794, DefensiveLine 12716, DefensiveEngagement 9608):
- piechota: BehaviorRegroup 1.75 (trzyma szyk), lucznicy: BehaviorScreenedSkirmish 1 (za piechota, 5 m za jej tylem - rbmai.cs:3301+), Skirmish/SkirmishLine 0;
- konnica lewa/prawa: `ResetBehaviorWeights` + **BehaviorProtectFlank** (w ataku dodatkowo RBMBehaviorForwardSkirmish); waga ProtectFlank na sztywno 10 (rbmai.cs:3958-3973) - konnica w fazie 1 **nie ma zadnej innej opcji** niz stac 90 m w bok i 55 m przed linia; rusza tylko, gdy konnica wroga zblizy sie na 110 m albo `UnderRangedAttackRatio > 0.1` i wtedy szarzuje **na te konnice wroga**, nie na strzelcow (rbmai.cs:3903-3916);
- konni lucznicy: ScreenedSkirmish + MountedSkirmish.

Faza 2 "starcie" (postfiksy `Attack`/`Engage`: rbmai.cs:12830, 12750, 9642):
- piechota: `Utilities.FixCharge` = reset wag i **tylko BehaviorCharge 1** (rbmai.cs:10626);
- lucznicy: reset i **tylko RBMBehaviorArcherSkirmish**;
- konnica: reset i BehaviorMountedSkirmish 1 + **RBMBehaviorCavalryCharge 1** (BehaviorFlank gry zdjety - Armoury Doctrine S3 go przywraca);
- konni lucznicy: MountedSkirmish.
Po wejsciu w faze 2 nie ma powrotu ani trzeciej fazy (odwod, poscig, przegrupowanie na poziomie druzyny) - `SetDefaultBehaviorWeights` RBM zeruje BehaviorPullBack, BehaviorStop i **BehaviorReserve** (rbmai.cs:10337-10359): **AI nie trzyma odwodu nigdy**.

### 2.3 Konnica - odpowiedz na pytanie Jeffa "czy ruszy na lucznikow"
`RBMBehaviorCavalryCharge` (rbmai.cs:12-320), pola `ChargeArchers=true, ChargeInfantry=true, ChargeCavalry=false, ChargeHorseArchers=false`:
- wybor celu przy pierwszym namierzeniu: **najpierw piechota**, lucznicy dopiero gdy piechoty nie ma (rbmai.cs:104-112); a przy kazdym wejsciu w stan Charging `CheckForNewChargeTarget` bierze `FindSignificantEnemy(piechota + lucznicy)` (rbmai.cs:184-197), gdzie kryterium to `dystans / liczebnosc WLASNEGO szyku`, z premia x1.5 dla szyku glownego wroga (rbmai.cs:11180-11196) - czyli praktycznie **najblizszy szyk pieszy, zwykle glowna piechota**. Straty zadawane przez lucznikow wroga NIE wchodza do zadnej decyzji (jedyne uzycie `UnderRangedAttackRatio`: skrocenie przegrupowania po szarzy > 0.2, rbmai.cs:173, i wyzwolenie ProtectFlank z 2.2).
- cykl: Charging -> ChargingPast (przejazd 110 m + glebokosc celu, limit 19 s) -> Reforming (10 s albo zwartosc < 12 m) -> Charging. Szerokosc szyku = szerokosc celu.
- RBM ma licznik obrazen wg klasy sprawcy (`Tactics.agentDamage`, postfiks `Mission.OnAgentHit`, rbmai.cs:9915-9958), ale sluzy on **tylko statystykom** (BattleStatsLogic, tryb developera) - zadna taktyka go nie czyta.
- Wniosek: w czystym RBM szarza konnicy na strzelcow zdarza sie tylko przypadkiem (gdy lucznicy sa najblizej). Reakcje "ostrzal boli -> jazda na lucznikow" daje dopiero Armoury (Doctrine S3 ustawia `ChargeInfantry=false` przy Flank >= 1.2 i lucznikach wroga >= 5% - Doctrine.cs:54-58; HorsePursuit.*; S2 Doctrine.S2.cs dla obroncy) - poza zakresem tej mapy, ale to jest miejsce, gdzie ta decyzje sie stroi.
- Piechota wobec szarzy: gdy konnica wroga celuje w nasz szyk i jest blizej niz piechota wroga (> 35 m) - stoi, mur tarcz, twarza do jazdy, piki "brace" (`OverrideBehaviorCharge` rbmai.cs:12951-13055, `OverrideBehaviorAdvance` 4613+, `OverrideAgent.SetFiringOrder` 4399+).

### 2.4 Lucznicy
`RBMBehaviorArcherSkirmish` (rbmai.cs:8502-8769): automat Shooting / Approaching / PullingBack liczony **wylacznie wzgledem piechoty wroga** (`FindSignificantEnemy(includeInfantry only)`), dystans roboczy = polowa max zasiegu; cofa sie przy < 40% tego dystansu i tylko gdy ma wlasna piechote w zasiegu; boczne przesuniecie losowe do 70 m w strone wylosowana raz na bitwe (`side = RandomInt(2)`). **Konnica wroga nie istnieje w tej logice** - lucznicy nie uciekaja przed szarza i nie chowaja sie za piechote (to dopisuje Armoury Doctrine S4).
Pojedynczy strzelec: `AiUseShieldAgainstEnemyMissileProbability = 0` i brak obserwacji pociskow dla agentow strzeleckich (rbmai.cs:2527-2531); w zwarciu (wrog < 2.5 m) wychodzi z szyku (rbmai.cs:5108-5116, promien 2.5 m). Strzelcy bez amunicji (<= 5) sa co decyzje przenoszeni do szyku piechoty, konni bez amunicji do konnicy (`ManageFormationCounts` prefiks, rbmai.cs:10362-10407) - RBM sam przetasowuje szyki wg aktualnej broni.

### 2.5 Szyk, tempo, zolnierz
- Szyk piechoty: `Utilities.DecideArrangementOrderForFormation` (rbmai.cs:10584): > 70% z duza tarcza -> mur tarcz; > 50% broni dwurecznej -> luzny; inaczej linia. Armoury CloseRanks ma na tym prefiks+postfiks (CloseRanks.cs:132-148).
- `Formation.SetMovementOrder` prefiks: kazda "Charge" w bitwie polowej zamieniana na ChargeToTarget(najblizszy duzy szyk) (rbmai.cs:4513-4534); `MovementOrder.GetSubstituteOrder`, `GetPositionAux` (Advance/FallBack) przepisane (rbmai.cs:4271-4390).
- `HumanAIComponent.AdjustSpeedLimit` prefiks: konnica zawsze pelna predkosc konia; piechota w Advance pod ostrzalem (>= 33% trafianych) min. 0.6 (rbmai.cs:3984-4050). Armoury MarchToContact wchodzi po tym.
- `Frontline` (rbmai.cs:4855-5446): `Formation.GetOrderPositionOfUnit` / `GetDirectionOfUnit` - rotacja pierwszego szeregu, cofanie o krok, losowe czasy decyzji (aiDecisionCooldownDict); konny w tlumie 3+ pieszych wymusza ruch naprzod (rbmai.cs:2812-2822).
- `AgentStatCalculateModel.SetAiRelatedProperties` postfiks (rbmai.cs:2352-2556): blok/parowanie/celnosc wg umiejetnosci x `GetCombatAIDifficultyMultiplier` (0.7 / 0.85 / 1.0 wg opcji kampanii; rbmai.cs:11361); postura + stamina (StanceLogic rbmai.cs:6194-7797) - zmeczony traci 15% predkosci i tempa ciosu, wybita postura = upuszczona tarcza/bron.
- Morale: `CommonAIComponent.InitializeMorale` prefiks w RBMCombat (rbmcombat.cs:2654-2668): baza 35+30+dodatki -> `BattleMoraleModel.GetEffectiveInitialMorale`, clamp 15..100, morale powrotu = polowa.
- Panika z tratowania: `Mission.ChargeDamageCallback` postfiks (rbmai.cs:2915-2965) - stratowany przez konia (bitwa polowa / wypad) dostaje `CommonAIComponent.Retreat()` (powalony od przodu 50%, od tylu 100%; odepchniety zaleznie od tieru). A `HumanAIComponent.OnTick` postfiks (rbmai.cs:2825-2828): **kazdy agent z morale > 0, ktory od 10 s nie dostal ciosu wrecz, dostaje `StopRetreating()`** - RBM sam "zbiera" uciekajacych. To koliduje wprost z zasada Jeffa "panika = tylko ucieczka" i z Armoury BrokenMen ("Broken Men Never Rally"; w logu 11.11 13:43 linia `Bitwa (NB2)`: "morale przypiete 13 razy, ponowne paniki gry 6") - do sprawdzenia przez mape Armoury, kto wygrywa klatke.
- Spawn: `Mission.SpawnTroop` prefiks - posilki polowe wchodza w srodku strefy rozstawienia (rbmai.cs:12473-12517); `SandBoxBattleMissionSpawnHandler.AfterStart` prefiks - gdy ludzi wiecej niz BattleSize: fale (GlobalTimer, Wave) z `defenderAdvantageFactor` (rbmai.cs:12615-12648). `PlayerEncounter.CheckIfBattleShouldContinueAfterBattleMission` prefiks: po wygranej gracza w polu cala reszta pokonanych = "ranni", **bitwa nie ma drugiej rundy** (rbmai.cs:12650-12679).
- Bandyci: `OverrideBehaviorCharge` oddaje gre, gdy ktoras strona to bandyci (rbmai.cs:12955-12963) - tam zostaje vanillowa szarza.

## 3. Oblezenie - kto czym rzadzi

| Decyzja | Wlasciciel u Jeffa | Dowod |
|---|---|---|
| Plan szturmu: pasy, drabiny/taran/wieza, wybor bramy, wejscie do srodka | **gra** (TeamAISiegeComponent, TacticBreachWalls, SiegeLane, BehaviorAssaultWalls) | RBM nie ma latki na TacticBreachWalls; latki RBM na BehaviorAssaultWalls zdjete przez VAIS (vais.cs, RestoreBehaviorAssaultWalls=true) |
| Obrona murow: TacticDefendCastle, lucznicy na murach, obsluga machin | **gra** | VAIS zdejmuje prefiksy RBM: TacticDefendCastle.StopUsingStrategicAreas, BehaviorShootFromCastleWalls x2, BehaviorUseSiegeMachines, BehaviorWaitForLadders, TacticComponent.StopUsingAllMachines / StopUsingAllRangedSiegeWeapons |
| Spychanie drabin przez obroncow | **gra** (przywrocone dwa razy) | RBM `SiegeLadder.IsDisabledForBattleSide` prefiks (rbmai.cs:9787) zdejmuja VAIS (domyslne true) i EpicSieges (epic.cs, Priority 0) |
| Kolejki do drabin / wiezy | **gra** (VAIS RestoreSiegeTower domyslnie true) | rbmai.cs:9761 zdjete |
| **Morale obroncow** | **RBM** (RestoreAgentMoraleInteractionLogic=false, KeepBattleEnabled=1) | `AgentMoraleInteractionLogic.ApplyMoraleEffectOnAgentIncapacitated` prefiks (rbmai.cs:9815-9855): smierc obroncy w oblezeniu **nie zabiera morale nikomu**, dopoki druzyna obroncy ma >= 50 zywych; ponizej 50 i przy sile obroncow < 50% sily atakujacych - strata x2 w promieniu 500 m (lawinowe zalamanie = odwrot do donzonu) |
| Punkty lucznikow na murach | **RBM** na 16 scenach uzywanych przez ROT | `SiegeArcherPoints` (rbmai.cs:2119+) czyta `Modules/RBM/ModuleData/scene_positions/<scena>_<poziom>.xml`, usuwa vanillowe archer_position i stawia swoje; 16 z 19 scen RBM wystepuje w `ROT-Map/ModuleData/settlements.xml` (np. battania_castle_a/b, empire_castle_b/c/f/g, sturgia_castle_003, vlandia_castle_b); na wlasnych scenach ROT (ROT_Wintefell2 itd.) - vanilla. Tego VAIS nie zdejmuje |
| Zasieg "blisko pozycji strategicznej" | **RBM** | `ArrangementOrder.IsStrategicAreaClose` prefiks, 200 m (rbmai.cs:2135-2160) |
| Zolnierz w tloku na murze | **RBM** | `Formation.GetOrderPositionOfUnit` prefiks: wrog w 2.25 m -> brak pozycji szyku, walczy swobodnie (rbmai.cs:5048-5060) |
| Spawn | **RBM** | `SandBoxSiegeMissionSpawnHandler.AfterStart` prefiks: bez koni, fale, defenderAdvantageFactor (rbmai.cs:12520-12553) |
| Wytrzymalosc machin, flaga anti-personnel | **EpicSieges** (XML) | `siegeengines.xslt`: wieze 10000 / 15000 HP, is_anti_personnel=false poza balistami; prefaby drabin/bram/taranow/wiez podmienione |
| Posilki w oblezeniu | **DynamicReinforcements** (AllowSieges=true) | dr.cs:2023-2027 (w oblezeniu plan rozstawienia zostawiony "latce zapasowej") |
| Wyniszczenie na mapie (nie misja) | RealisticBannerlord SiegeAttrition 2-5 | rb.cs:512 |
| Przelamanie, trzymanie, dowodcy, kamienie itd. | Armoury (SiegeBreak371, SiegeHold372, SiegeCommanders381, SiegeArchers372b, SiegeStones383...) | poza ta mapa |

Uwaga do wiersza "morale": opis w MCM VAIS mowi, ze przy ON obroncy moga sie zalamac i cofnac do donzonu normalnie; u Jeffa jest OFF, wiec **obroncy walcza bez strat morale az do progu 50 ludzi** - to zachowanie RBM, nie gry. Kazda nasza latka na morale w oblezeniu (SiegeBreak371 / SiegeHold372) dziala pod tym prefiksem.

## 4. Pozostale mody - co naprawde robia w misji

- **ROT (ROT.dll)**: nie dotyka taktyk ani zachowan szykow. Latki misji: `Mission.SpawnAgent` (smok gracza, sprzet zaciagnietego; rot_all.cs:20208), `BehaviorComponent.InformSergeantPlayer` (komunikaty i rog dla zaciagnietego gracza; 20317), `OrderController.AreGesturesEnabled` (20361), kolizje/blok (`MissionCombatMechanicsHelper`, `Mission.MeleeHitCallback`; 19091-19143). MissionLogic: ROTDragonMissionBehavior + `AIDragon` (wlasny pilot smoka: lot 22 m, nalot z 55 m na **najwieksze skupisko** `FindBestClusterTarget`, ogien 3.5 s, ucieczka ponizej 35% zdrowia; rot_all.cs:26772-27980) - smok nie nalezy do systemu szykow, wiec AI druzyny go nie widzi i na niego nie reaguje; RoTElephantMissionLogic (losowy cios slonia, szansa 0.001), ChariotMissionBehavior (pusty tick), ROTEnlistmentMissionBehavior (gracz w przydzielonym szyku, CommandFormation), ROTDuelsMissionController, ROTRedWeddingController. `ROTAgentStatCalculateModel.UpdateAgentStats`: kara plyty do tempa ciosu i predkosci (15345-15365). **RBMIntegration** (`ROT.HarmonyPatches.ModCompatibility.RBM`, 17361+): przelicza pancerze, strzaly i siodlo smoka przy wczytywaniu XML - zero integracji taktycznej; ROT nie mapuje swoich kultur na taktyki RBM (stad pulapka z 2.1).
- **NavalDLC**: wlasne AI tylko w misjach morskich i rajdach (TeamAINavalComponent, NavalTacticComponent, TacticNavalRaidDefense, BehaviorNavalRaid*; naval.cs:1184-1840, 69914-72343). W ladzie tylko lancuch modeli: NavalBattleMoraleModel (naval.cs:73854; zmiany strat morale bramkowane `IsNavalBattle`, morale poczatkowe z perkow), NavalAgentStatCalculateModel, NavalAgentApplyDamageModel. RBM w misji morskiej nie zmienia listy taktyk (warunek FieldBattle), ale Frontline dziala (`IsFieldBattle || IsNavalBattle`, rbmai.cs:5019).
- **DynamicReinforcements**: `BattleStateBehavior` (dr.cs:2527) dosyla sasiednie partie falami. Przy stronie gracza tworzy **osobna druzyne AI** (`CreateAutonomousTeam`, dr.cs:1960-2010: `TeamAIGeneral(3 s, 3 s)` + stala lista TacticCharge, FullScaleAttack, DefensiveEngagement, DefensiveLine - **powstaje po EarlyStart, wiec RBM jej listy nie czysci**, a zachowania RBM dostaje przez latke OnUnitAdded...). Posilki wroga dolaczaja do istniejacej druzyny wroga. Latki: GetRealBattleSize x1.5, DefaultMissionDeploymentPlan.GetDeploymentBoundaries, BattlePowerCalculationLogic.GetTotalTeamPower. Armoury obsluguje te druzyny w Doctrine ("pozne DR").
- **BannerKings.Redux**: `SandboxBattleMoraleModel.GetEffectiveInitialMorale` postfiks - **obronca x1.3 morale poczatkowego** w kazdej misji (bk.cs:33544-33556); perki kapitana (szarza +10%, predkosc konia). **BKROTPatch**: `BKROTBattleMoraleModel` (bkrot.cs:11296) - blogoslawienstwa x1.3 / x0.8 / x0.6 do morale poczatkowego.
- **ImmersiveBattlefields**: rogi, glosy rozkazow, `OrderController.PlayOrderGestures`; AutoDismount wylaczony - bez wplywu na decyzje. **ROTBattleAtmosphere**: tylko dzwiek (BattleAtmosphereMissionLogic). **ArtemsLivelyAnimations**: AgentStatCalculateModel przepuszczajacy + animacje (Armoury latka jego DropAllWeapons, klucz 359b). **DismembermentPlus**, **DTE** (`Mission.SpawnAgent` - sprzet), **Spoils of War**: bez AI.

## 5. Tabela wlascicieli (stan u Jeffa)

| Decyzja bitewna | Wlasciciel bazowy | Kto nadpisuje / dopisuje |
|---|---|---|
| Lista taktyk druzyny (pole) | RBM Tactics.EarlyStartPatch (kultura) | Armoury WarCouncil (AfterStart: Tactics dowodcy < 30 tlum, >= 60 + FrontalCavalryCharge / Ring / ChokePoint, >= 150 okrazenie) + Doctrine.ApplyList; druzyny posilkow: DR |
| Wybor taktyki co ~5 s | gra MakeDecision (max wagi, histereza 1.5) | wagi: RBM (splity 10, Retreat 0, FrontalCav x0.75, RangedHarrass x1.1 / x0.6), potem Armoury Doctrine (mnozniki kultur, S1, S2) |
| Fazy i wagi zachowan szykow | RBM (postfiksy Advance/Attack/Defend/Engage, prog 75 m) | Armoury Doctrine S3/S4, Hold291, Pendulum333 |
| Cel szarzy konnicy | RBM RBMBehaviorCavalryCharge (piechota pierwsza) | Armoury Doctrine S3 (ChargeInfantry=false), HorsePursuit.*, OwnFootAhead |
| Oslona skrzydel | RBM OverrideBehaviorProtectFlank (waga 10) | - |
| Ruch lucznikow | RBM RBMBehaviorArcherSkirmish (tylko wobec piechoty) | Armoury Doctrine S4, CleanShot |
| Odwod | nikt (RBM zeruje BehaviorReserve) | - |
| Zorganizowany odwrot druzyny | nikt (RBM waga 0) | Armoury RoutHold.Withdraw (poza mapa) |
| Szyk (mur tarcz / luzny / linia) | RBM DecideArrangementOrderForFormation | Armoury CloseRanks, ShieldsUp |
| Tempo marszu | RBM AdjustSpeedLimit | Armoury MarchToContact (po com.rbmai) |
| AI zolnierza: blok, parowanie, celnosc | RBM SetAiRelatedProperties + opcja trudnosci | Armoury (weterani wg tieru, DuelAi, FieldCraft) |
| Postura / stamina | RBM StanceLogic | Armoury BattleWind, RbmNoRegen, DuelGuard |
| Morale poczatkowe | RBMCombat InitializeMorale (65 + dodatki) | BK x1.3 obronca, BKROT blogoslawienstwa, NavalDLC perki |
| Strata morale po smierci (pole) | gra AgentMoraleInteractionLogic | Armoury (BannerCompanies, BrokenMen) |
| Strata morale po smierci (obronca oblezenia) | **RBM - zamrozona do progu 50 ludzi** | Armoury SiegeBreak371 / SiegeHold372 |
| Panika z tratowania i samoczynny powrot po 10 s | RBM ChargeDamageCallback + HumanAIComponent.OnTick | Armoury BrokenMen / PanicRun / RallyRouted359 - mozliwa kolizja |
| Plan szturmu i obrony murow | gra (po zdjeciu latek RBM przez VAIS + EpicSieges) | Armoury Siege* |
| Punkty lucznikow na murach | RBM SiegeArcherPoints (16 scen ROT) / gra (reszta) | Armoury SiegeArchers372b |
| Spawn, fale, wielkosc bitwy | RBM spawn handlery + DR (x1.5, posilki) | - |
| Smoki, slonie | ROT (poza systemem szykow) | - |
| Bitwa morska | NavalDLC | RBM Frontline |

## 6. Co z tego wynika dla pytania Jeffa (tylko fakty z kodu cudzych modow)

- **Powtarzalnosc jest wbudowana w RBM**: dla wiekszosci kultur ROT 1 taktyka ataku i 2 obrony, dwie fazy przelaczane jednym progiem 75 m, konnica w fazie 1 zawsze na skrzydle, w fazie 2 zawsze cykl szarza-przejazd-przegrupowanie na najblizszy szyk pieszy. Losowosc w RBM to tylko strona przesuniecia lucznikow i czasy decyzji pojedynczego zolnierza.
- **Brak petli "boli mnie -> reaguje"**: w RBM zadna decyzja szczebla druzyny ani szyku nie czyta strat ani zrodla strat (licznik `agentDamage` istnieje, ale tylko do statystyk). Jedyne reakcje na ostrzal: krotsze przegrupowanie jazdy, wyzwolenie ProtectFlank (na konnice wroga, nie na strzelcow), min. tempo marszu 0.6.
- **Brak odwodu i brak odwrotu** - obie rzeczy RBM wylacza wprost (waga 0).
- **Tactics dowodcy nic nie znaczy w RBM**; kultura decyduje, ale przez vanillowe id, ktore w ROT znacza co innego (Polnoc = battania, Braavos = empire).
- Wszystko, co dzis roznicuje bitwy (dowodca, doktryny kultur, najpierw ostrzal, flanka, lucznicy za piechote, obronca pod ostrzalem), to juz warstwa Armoury nalozona na powyzsze; w logu 11.11 13:31 `Latki nocy (K3)`: "doktryna: GetTacticWeight 8/8 (Priority.Last po com.rbmai), Attack 2/2, typy RBM TAK". W tej samej sesji bitwa z linii `Bitwa (NB2)` 13:43: druzyna obroncy "T0/battania/tlum" (wodz bez Tactics) - lista Charge + FullScaleAttack + DefensiveEngagement + DefensiveLine, **"zmian taktyki 0", "konnica w BehaviorFlank 0", "najpierw ostrzal 0"** - czyli przy slabym dowodcy bitwa i tak idzie torem RBM.

## 7. Czego nie udalo sie ustalic

1. **Poziom trudnosci AI walki w kampanii Jeffa** (`CampaignOptions.CombatAIDifficulty`) - siedzi w zapisie, nie w plikach konfiguracji; od niego zalezy mnoznik 0.7 / 0.85 / 1.0 calego AI zolnierza RBM.
2. Znaczenie `PlayerPostureMultiplier=0` (indeks -> mnoznik) - nie czytalem RBMConfig pod tym katem; dotyczy tylko gracza.
3. Ktora kulture dokladnie zwraca `IBattleCombatant.BasicCulture` dla strony zlozonej z kilku partii / armii mieszanej (RBM bierze `AttackerLeaderBattleCombatant` / `DefenderLeaderBattleCombatant`) - opieram sie na opisie w Doctrine.cs ("kultura krolestwa"), nie sprawdzalem w cs_full.cs.
4. Czy RBM `SiegeArcherPoints` faktycznie laduje sie na scenach ROT na kazdym poziomie murow (plik musi pasowac takze do `GetUpgradeLevelMask`, np. `_19`, `_21`, `_25`) - sprawdzilem tylko zgodnosc nazw scen (16/19), nie maski.
5. Faktyczny wynik klatki przy kolizji RBM `StopRetreating()` po 10 s (rbmai.cs:2825) z Armoury "Broken Men Never Rally" - widac w logu "morale przypiete 13 razy", ale nie przesledzilem kodu Armoury (to zakres mapy Armoury).
6. Czy `KeepBattleEnabled` w RBM robi cos poza progiem morale obroncow (znalazlem jedno uzycie: rbmai.cs:9824) - kopia zapasowa konfiguracji "przed-keep" z 09.11 sugeruje, ze Jeff wlaczyl to niedawno; nie mam logu z oblezenia po tej zmianie.
7. Zawartosc prefabow EpicSieges (drabiny, bramy, tarany, wieze) - wiem, ze podmienia 5 plikow prefabow, nie porownywalem ich z vanilla (liczba drabin, punkty uzycia).
8. Mapowanie `ReinforcementWaveCount=3` i `BattleSize=6` na liczby (tablice w BannerlordConfig) - nie sprawdzalem w dekompilacji; przy najwyzszym stopniu i DR x1.5 limit to najpewniej 1500 agentow (zgodne z MaxAgentsOnField=1500), ale to wniosek, nie odczyt.
9. Nie uruchamialem gry ani nie mam logu z bitwy, w ktorej RBM wybral taktyke specjalna (Embolon / Split*) - ich dzialanie w ROT znam tylko z kodu.
10. Mody wylaczone w launcherze (AIInfluence, ScoutingFog/AILordsEnhanced) nie byly dekompilowane.
