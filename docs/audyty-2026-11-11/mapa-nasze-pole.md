# MAPA: Armoury w misji bitwy polowej (stan worktree n367c, 11.11)

Zrodlo: `...\scratchpad\n367c\Armoury\src` (SRC), CHANGELOG.md worktree, docs/PROJEKT-344-AI-BITWY.md, logi `Modules\Armoury\Armoury-2026-11-*.log` (12 logow, 21 bitew polowych - prawie same autotesty), `bannerlord-autotest\test\at-395-konsola.txt`. Tylko odczyt. Odwolania plik:linia = naglowek-opis klasy (tam jest regula i dowod z dekompilacji).

## 1. Szkielet

- Rejestracja: `SubModuleMain.OnMissionBehaviorInitialize` (SubModuleMain.cs:699-815). W bitwie polowej dochodza: FieldCraft (:711), HorseWind (:715), LootWear.Logic, HeroHp.Logic, HitScribe, GuardMaster, BrokenMen (:730), RoutKeeper (:746), BattleStall307e (:749), BattleSense (:781), BannerCompaniesMission (:786), ArmyReviewCheck, FriendlyFire.Logic (:792), OwnFootAhead.Logic (:795), HorseFateMission, opcjonalnie CampScene.
- **BattleSense** (BattleSense.cs:9-30) to szyna: jeden OnMissionTick wola po kolei BattleCries -> MarchToContact -> Doctrine -> BattleSound -> ShieldsUp -> CloseRanks -> HorsePursuit (148/204/283/311a/344) -> HeroCaution (149/335/359) -> BloodPools/DuelCraft. Zachowania misji tykaja od konca listy: BattleSense PRZED RoutKeeper i BrokenMen (HeroCaution.cs:45).
- Latki misji zakladane raz na proces przy pierwszej bitwie (`NightHooks.FirstBattle`, NightHooks.cs:8-20; `Doctrine.ApplyMissionPatches` Doctrine.cs:182-222): postfiksy `GetTacticWeight` 8 taktyk gry, `Attack` 2 taktyk, `CheckIsDefenseApplicable`, `Formation.CacheClosestEnemyFormation`, prefiksy `Formation.SetMovementOrder` / `SetArrangementOrder`, postfiksy `Formation.CacheMovementSpeedOfUnits` i `HumanAIComponent.AdjustSpeedLimit`, prefiks+postfiks `RBMAI.Utilities.DecideArrangementOrderForFormation`, postfiks `AgentStatCalculateModel.SetAiRelatedProperties`, postfiks `Agent.UpdateFormationOrders`. Wszystkie Priority.Last, after "com.rbmai". FriendlyFire i ChargeTemper - w SubModuleMain (transpilery przed JIT).
- Wspolny przelacznik **SurvivalMode** (SurvivalMode.cs:6-28): PANIKA (IsPanicked / zbior RoutHold / odwrot komponentu bez rozkazu) kontra ROZSADEK (reszta, w tym odwrot na rozkaz i odwrot rannego 143). Paczki "rozsadku" pomijaja PANIKE, paczki paniki dzialaja tylko na nia.
- Zakres wspolny: prawie wszystko ma bramke `IsFieldBattle`, bez morza i kryjowki; **druzyna gracza i szyki pod jego rozkazami (HorsePursuit.PlayerForm) sa poza regulami taktyki i szyku**; sojusznicy AI - objeci. Reguly "ciala" (stamina, rany, panika, FF) - jedna dla gracza i AI.
- Klucze MCM: wszystkie nizej domyslnie TAK, chyba ze zaznaczono. Porownanie z `Armoury.json` Jeffa (plik z 08.11): 0 roznic; 102 klucze bitwy (wszystko od nocy 25.10) w pliku NIE WYSTEPUJA, czyli dzialaja wartosci z kodu.

## 2. Mapa systemow

R = regula reaktywna (reaguje na sytuacje), S = stala (parametr/profil), P = sam pomiar.

### A. Poziom armii - wybor taktyki (gra: `TeamAIComponent.MakeDecision` co ok. 5 s, MaxBy wag, histereza x1.5; my tylko zmieniamy liste i wagi)

| Nr | Plik | Co zmienia | Kiedy | Klucze (domyslne) | Typ |
|---|---|---|---|---|---|
| NB2 rada wojenna | WarCouncil.cs:12-33 | lista taktyk druzyny AI wg Tactics dowodcy: <30 "tlum" = Charge + FullScaleAttack (obronca + DefensiveEngagement/Line); 30-59 zestaw RBM wg kultury; >=60 + FrontalCavalryCharge, obronca + DefensiveRing/HoldChokePoint; >=150 i strona >=150 ludzi + RBM SplitInfantry (okrazenie) | raz, AfterStart | CommanderTactics | S |
| 114 doktryny kultur | Doctrine.cs:13-57, tabela :82-116 | mnozniki wag taktyk Shock/Shot/Horse/Hold + waga Flank + czas ostrzalu wg kultury krolestwa (30 profili, np. Dothraki Hold 0.3 Flank 1.5, Zelazne Wyspy Shock 1.3 Horse 0.5, Wolni Ludzie Shock 1.4 + Charge); dodaje RangedHarrassmentOffensive (liczy sie od 10% strzelcow) | stale przez bitwe | CultureDoctrines | S |
| 114 S1 najpierw ostrzal | Doctrine.cs:35-37 | nasi strzelcy >=15%, wroga <= max(5%, 1/3 naszych): Shot x2.5; licznik od 200 m; koniec po 90 s (x kultura), stracie polowy strzelcow albo zwarciu -> Shot x0.3 do konca | tick 2 s | ShootBeforeAttacking, ShootFirstSeconds=90 | R |
| 141 S2 obronca pod deszczem strzal | Doctrine.S2.cs:12-41 | druzyna z 3x mniej strzelcow, glowna piechota >=20% trafionych przez 20 s, pieszy wrog <=200 m: DefensiveLine/Engagement x0.3 -> gra przechodzi na atak; po zwarciu do konca | tick 2 s | DefendersAdvanceUnderFire | R |
| 291/291d obronca trzyma pozycje | Doctrine.Hold291.cs:13-59 | druzyna AI strony Defender: wagi taktyk ataku = 0, obrona gry wymuszona TAK; puszczona gdy: S2, napastnik nie podchodzi 90 s, zwarcie 25 m | decyzja taktyki | DefendersHoldTheirGround | R (odwrocenie ról: obronca czeka) |
| 313b 291e bilans ognia | Doctrine.Hold291.cs:60-71 | obronca z >=2 zabitymi od pociskow, gdy gra sama mowi "obrona NIE" - puszczony do ataku do konca bitwy | decyzja taktyki | DefendersChargeWhenOutshot | R |

W tych plikach nie ma zadnego losowania (0 wystapien Random/MBRandom w Doctrine*, WarCouncil, OutshotRush, Pendulum333, MarchToContact, CloseRanks, ShieldsUp, HorsePursuit*, HeroCaution*): przy tych samych armiach, dowodcy i terenie wybor taktyki jest deterministyczny.

### B. Poziom szyku

| Nr | Plik | Co zmienia | Kiedy | Klucze | Typ |
|---|---|---|---|---|---|
| 114/128/134c marsz do kontaktu | MarchToContact.cs:14-59 | szyk AI przy Move/Advance/FallBack idzie krokiem (cap predkosci szyku i czlowieka), bieg dopiero w szarzy / zwarciu 25 m; lucznicy krokiem do 120 m; konnica klusem (0.45) do 120 m; szarza z daleka marszem, bieg od 60 m; odpoczynek koni AI ponizej 35% staminy | tick 0.5 s, latki w watkach roboczych | MarchToContactEnabled, MarchAtWalkingPace, CavalryApproachPace=0.45, CavalryGallopDistance=120, ArchersWalkUntil=120, ChargeMarchesFirst, ChargeRunsFrom=60, PlayerMoveOrderWalks, HorseAiRestBelow=35/Until=65 | S z progami odleglosci (R tylko: odpoczynek koni) |
| 114 S3 konnica szuka flanki | Doctrine.cs:40-44, :245-282 | po przejsciu taktyki w Attack (tylko FullScaleAttack i RangedHarrass) lewa/prawa konnica dostaje BehaviorFlank z waga Flank kultury; przy Flank >=1.2 i lucznikach wroga >=5% `RBMBehaviorCavalryCharge.ChargeInfantry=false` ("najpierw lucznicy") | zmiana fazy taktyki | CavalrySeeksFlanks | S (warunek z kultury i skladu, nie ze strat) |
| 114 S4 lucznicy za piechote | Doctrine.cs:45-50 | szyk lucznikow AI, gdy piechota/konnica wroga <=45 m, a nasza piechota >= pol lucznikow: BehaviorScreenedSkirmish; powrot ponad 70 m | co 1 s | ArchersFallBackBehindInfantry, ArcherFallBackDistance=45 | R |
| 146/310 szyk pod ostrzalem | CloseRanks.cs:13-41 | szyk piechoty AI pod ostrzalem (>=3 pociski/5 s) przed zwarciem: ShieldWall przy >=60% tarcz w rece, inaczej Loose (nie przy konnicy wroga w 90 m); zmiana najwyzej co 10 s; zastepuje decyzje RBM tylko wtedy | latka na decyzji szyku RBM + pass | AiFormationsUnderFire, ShieldWallUnderFireAnyOrder | R |
| 291C/313b/320d bieg pod ostrzalem | OutshotRush.cs:14-41 | druzyna z >=2 zabitymi od pociskow w natarciu: piechota BIEGNIE do zwarcia (bez capu marszu, Loose zamiast muru tarcz), cel = pieszy szyk wroga zamiast konnych lucznikow AI (gracz-konny lucznik zostaje celem) | tick 0.5 s | OutshotRushWithShields, UnshieldedRushUnderFire | R |
| 283 R4/R5 postoj pod nekaniem | HorsePursuit.ReactHalt.cs:10-25 | szyk ofiary staje frontem do nekajacego (BehaviorStop); cala armia - od 291 domyslnie NIE staje (idzie dalej) | po szkodzie | ArmyHaltsForHarassers, ArmyMarchesPastHarassers | R |
| 333/344 arbiter rozkazu | Pendulum333.cs:14-42, :117-126 | przed kontaktem (>75 m) jeden rozkaz ruchu: martwa strefa max(12 m, glebokosc), zapadka kierunku 30-240 s, trzymanie 10 s, histereza zachowan 6 s, Line/Loose najwyzej co 20 s; szyki boczne ruszaja tylko za przyjeta decyzja prowadzacego | prefiks kazdego rozkazu, takze watek AI | FormationOrderArbiter | R-tlumik (gasi drgania, nie tworzy manewru) |
| 307e zastoj | BattleStall307e.cs:12-23 | 60 s bez ciosu i strzalu: ludzie AI bez broni i amunicji schodza z pola | co 1 s | BattleStallBreak, BattleStallSeconds=60 | R |

### C. Konnica i odpowiedz na strzelca

| Nr | Plik | Regula | Klucze | Typ |
|---|---|---|---|---|
| 148 poscig | HorsePursuit.cs:14-42 | konnica AI strony, ktora zlamala wroga (>=3 w PANICE, odsetek wiekszy niz wlasny) goni uciekajacych: 60 s / 250 m od glownej sily, 2 jezdzcow na ofiare, nie przy nietknietym szyku w 80 m, konie nie zajechane; noca polowa | AiCavalryPursues, CavalryPursuitNearVictim, PursuitDelegatedFormations | R |
| 204 jazda na strzelca | HorsePursuit.Hunt.cs:12-40 | wykrycie KONNEGO strzelca wroga z kazdego pocisku (trafienie, pudlo, przelot); L1 oddzial (2 + padli), L2 caly szyk (110 m / 3 pociski w 5 s / ktos padl), L3 cala jazda w 250 m; przeciecie + odciecie; lucznicy strony dostaja cel | RidersRunDownShooters | R |
| 283 nekanie z tylu/boku | HorsePursuit.React.cs:13-44, ReactFire.cs:9-16 | macierz zagrozenie x szyk: R1 lucznicy strzelaja tylko z czysta linia (krok w bok), R2 oddzial piechoty (smycz 50 m, 60 s), R3 jazda, R6 wodz trafiony = pelna sila, R7 chorazy. **Pieszy strzelec AI i nekajacy wrecz tylko gdy SAMOTNY** (bez nietknietego szyku w 80 m); strzelec w swoim szyku = "zwykla bitwa - RBM/141/146" | FormationsAnswerHarassers | R |
| 311a oslona strzelca | HorsePursuit.Cover.cs:12-40 | samotny pieszy strzelec schowany za zaslona: grupa obchodzi z dwoch stron (luk 60-90 st.), szturm przy 3:1 | AiFlankHiddenShooter, CoverHoldSeconds=45, CoverHiddenSeconds=2.5 | R |
| 344 (A) front/kontra | HorsePursuit.Front344.cs:10-46, docs/PROJEKT-344-AI-BITWY.md | stany grupy wobec konnego strzelca: poscig tylko z dala od jego armii (g>=30 m) i przy przewadze 2x; przy krawedzi strefy armii FRONT (stoi przodem, nie zawraca); KONTRA calej grupy gdy strzelec wyjdzie poza oslone; pogon za szybszym najwyzej 8 s; pamiec przynety | CavalryFaceShooterAtArmyEdge | R (jedyny modul z pamiecia i stanami) |
| 157 jezdziec pod ostrzalem | ShieldsUp.Riders.cs:13-26 | pomiar konnicy pod ostrzalem + tarcza jezdzca | CavalryUnderFireLog, RidersShieldsUpUnderFire | P + R |
| 305c jezdziec zwalnia przed swoim | OwnFootAhead.cs:16-27 | jezdziec AI hamuje, gdy w korytarzu 3-8 m przed koniem stoi swoj pieszy | CavalrySlowsForOwnFoot | R |
| 136 stamina koni | HorseWind.cs:45-71 | galop meczy konia (ok. 120 s przecietny, 80 s z bardem), ponizej 30% zwalnia do 0.55 | HorseStaminaEnabled, HorseGallopDrainScale=0.5, HorseTiredFloor=0.55 | S (fizyka) |
| miarkownik szarzy | Charge.cs:9-17 | obrazenia najechania x0.6 i krzywa predkosci | ChargeTemperEnabled | S |

### D. Czlowiek: przetrwanie ("AI walczy o zycie", 139-149)

| Nr | Plik | Regula | Klucze | Typ |
|---|---|---|---|---|
| 140 tarcza pod ostrzalem | ShieldsUp.cs:15-53 | czlowiek AI z tarcza w ekwipunku pod ostrzalem (trafienie, strzal w niego, pudlo obok, szyk >=3 pociski/5 s) przeklada tarcze + bron jednoreczna i unosi ja; nie w 8 m od wroga; pikinier trzyma pike przy konnicy w 90 m | AiShieldsUpUnderFire, ShieldsUpHoldSeconds=8, ShieldsUpFormationShots=3, PikesKeptAgainstHorse | R |
| 143 odwrot rannego z bronia | RoutHold.Withdraw.cs:11-33, WoundedWithdraw.cs:11-19, BrokenMen.cs:8-23 | ponizej 20% HP (rzut 0.35 wg morale/Atletyki/krwawienia) cofa sie za wlasny szyk albo do sztandaru, bez paniki, broni sie | WoundedFleeEnabled, WoundedFleePercent=20, WoundedWithdrawWithArms, WoundedFleeByChance, WoundedFleeHeroes=NIE | R |
| panika z bolu | FieldCraft.cs:1653 (RollPain), :21-23 | ponizej 10% HP rzut (0.5 x krwawienie 1.5 x morale x kondycja x tabela tieru): Panic raz na bitwe | AiFleeWhenNearDeath, AiFleeBelowPercent=10, PainBreakByChance, PainBreakTierSteadiness | R |
| 145 ranny walczy ostroznie | WoundedWary.cs:10-32 | 20-50% HP: rzadziej atakuje, czesciej blokuje (nie bohaterowie) | WoundedFightWarily | R |
| 144 AI oszczedza sily | AiBreath.cs:13-35 | pieszy AI biegnie tylko tyle, by dobiec z 50% tchu (25% pod ostrzalem); ostatnie 25 m biegiem | AiSavesBreath, AiArriveWithBreathPercent=50, AiRunsLastMetres=25 | R |
| 310 czysty strzal | CleanShot.cs:12-23 | lucznik AI nie strzela w klebowisko ze swoimi / przez wlasny szereg | ArchersAvoidOwnMen | R |
| NB2 weterani wg tieru | VeteranAi.cs:12-32 | wspolczynniki walki wrecz po RBM: poziom T1 0.05 ... T6 0.70, agresja x0.80..x1.28, kapitan z Tactics 150 +0.05 | VeteransByTier, VeteranAggressionFix, SoldierPostureByTier | S |
| m113 bohaterowie-pojedynkowicze | FieldDuel.cs:10-34, DuelAi.cs, DuelCraft.cs | bohater AI w bitwie ma profil pojedynku, zrywy "otwarcie"/"nacisk" po zdarzeniach ciosu, roznica umiejetnosci w posturze | DuelistsInBattles, DuelistBurstsInBattles, DuelSkillGapInBattles, HeroTierFromSkill | S + R (zrywy) |

### E. Panika, ucieczka, zbiorka

| Nr | Plik | Regula | Klucze | Typ |
|---|---|---|---|---|
| NB2 zlamany raz | RoutHold.cs:12-41 | jeden Panic na czlowieka, morale przypiete na 0 (RBM nie zawroci go po 10 s), kierunek ucieczki od wrogow (16 kierunkow), 307b trzyma kurs | BrokenMenNeverRally, FleeAwayFromEnemies, PanicFleeHoldCourse | R |
| 142 panika bez powrotu | NoReturn.cs:11-24 | kazda panika gry w polu trafia do RoutHold | RoutedMenNeverRally | R |
| 147 bieg bez odsapki | PanicRun.cs:10-20 | spanikowany pije oddech do zera, potem 50% predkosci | PanicRunsUntilSpent | S |
| 359 zbiorka | RallyRouted359.cs:13-35, HeroCaution.Rally359.cs:9-21 | zywy wodz/kapitan/gracz w 12 m zawraca uciekajacego: rzut raz na zolnierza p = 0.10 + 0.35 Leadership + 0.25 morale..., strona rozbita (<0.3 x wroga) nie zawraca; dowodca AI jedzie do grupy tylko z eskorta >=3 i z dala od wroga; uciekajacy nie rzuca broni | RallyRoutedMen, RoutedMenKeepArms | R |
| 115/305c/307b/309/318c choragwie | BannerCompanies.cs:14-36, BannerCompanies.Lead.cs:11-22, Pickup.cs:14-25 | szyk dzielony na kompanie ok. 50 ludzi z dowodca i chorazym; zdarzenia: sztandar padl, dowodca padl, strata 1/6, 1/3, 1/2... -> rzut zalamania p = 0.5 x F x udzial strat x stan bitwy (0.3..1.5) x tabela tieru (T1 0.8 .. T7 0.04) x dowodztwo (0.5-1.0), max 0.9; zalamana kompania ucieka falami; biegacz podnosi sztandar | BannerCompaniesEnabled, BannerCompanySize=50, BannerBreakChance=0.5, BannerShockStandardLost=12, BannerShockCommanderLost=12, BannerBreakEarly(0.8), BannerLossBreakByShare, BannerBreakBattleState, BannerTierSteadiness, BannerLeaderHold, BannerPickupRange=20 | R |
| 139 los zlamanych | BrokenFate.cs:14-31 | kto dopadl uciekajacych, ile uszlo | FateOfTheBrokenLog | P |

### F. Dowodcy

| Nr | Plik | Regula | Klucze | Typ |
|---|---|---|---|---|
| 149 bohater za szykiem | HeroCaution.cs:13-50 | bohater AI w ostatnich szeregach; samotny (0 swoich w 5 m, >=2 wrogow w 10 m) cofa sie na 6 s; ranny <45% za szyk do konca bitwy | HeroesKeepBehindTheirMen | R |
| 335 ochrona wodza | HeroCaution.Guard335.cs:14-73 | wodz/kapitan: z eskorta konna >=10 jedzie z nia, inaczej STANOWISKO za glownym szykiem; zagrozony wycofuje sie do najblizszego szyku, 3 straznikow na napastnika (3..20) biegnie go bronic | CommandersKeepCommandPost | R |

RBM wylacza szyk wodza gry (`CanTeamHaveGeneralsFormationPatch`), wiec 149/335 sa jedynym "zachowaniem dowodcy". Dowodca nie wydaje zadnych rozkazow - jego Tactics dziala raz (lista taktyk), Leadership w okrzykach i zbiorce.

### G. Cialo, sprzet, dzwiek, pomiar (bez decyzji taktycznych)

- Stamina/rany: FieldCraft.cs:12-26 (zadyszka -50%, ranny <50% wolniejszy), BattleWind.cs (bohaterowie wg Endurance), 292 HumanStaminaMultiplier=2, Bleed313.cs:12-23 (krwawienie rzutem, kustykanie), WeaponEase.cs (wprawa tnie koszt ciosu), RbmNoRegen.cs (bez leczenia RBM w bitwie), WindedShow.cs (widac zadyszke), GuardMaster.cs (parowanie mistrza - gracz).
- FF: FriendlyFire.cs:18-30 (pociski 100%, wrecz 50%, szarza 50%), FriendlyFireMeleeBySkill.
- Sprzet: TroopWearLedger.cs / WearKeep.cs (zuzycie z trafien), LootWear.cs i HeroHp.cs (stan lupu i ochrona z dokladnego HP), AmmoRecovery.cs (odzysk strzal 45% + 20%), HorseFate.cs (kon ginie tylko zabity), DragonUnmount.cs.
- Dzwiek: BattleCries.cs:11-22 (okrzyk przy szarzy AI - takze jedyny detektor szarzy `ChargeStarted`, z ktorego korzysta MarchToContact), BattleSound*.cs (rogi, bebny, rozkazy kapitanow, motta), 359d "Hold the line!/Turn back!". Dzwiek niczego nie decyduje.
- Pomiar: FrameCost309.cs, HitScribe.cs, ArmyReview.Battle.cs, linie "Bitwa (NB2)", "(291)", "(344)", "(148)", "(204)", "(283)", "(149)", "(335)", "(359)", "(CH)".
- Po bitwie (mapa): LosersFlee.cs:23-30 (przegrani: ginie 5-65% wg poscigu/terenu, reszta ucieka), DefeatMorale.cs (kara morale /2).

## 3. Reaktywne a stale - sedno

- **Reaguja na sytuacje**: prawie wszystko na poziomie CZLOWIEKA i ODDZIALU (tarcza, odwrot rannego, oddech, czysty strzal, bohater, straz wodza, zbiorka, zalamanie kompanii) oraz odpowiedz na NEKAJACEGO (204/283/311a/344). To sa odruchy obronne.
- **Na poziomie armii** reaktywne sa tylko trzy przelaczniki wag: S1 (wrog bez strzelcow -> strzelaj), S2/291e/313b (przegrywam ostrzal -> piechota idzie/biegnie do zwarcia), 291 (jestem obronca -> czekam). Reszta to stale mnozniki kultury i lista z Tactics dowodcy; sam wybor robi gra co 5 s z wag.
- **Nie ma w kodzie** (grep rezerw/Reserve/odwod/pozorowan/feign po Doctrine*, WarCouncil, HorsePursuit*, Pendulum333, CloseRanks, OutshotRush, HeroCaution*, BannerCompanies*, RoutHold*: 0; jedyne trafienie to `AiBreath.Reserve` = zapas tchu): odwodu, pozorowanego odwrotu, zmiany planu wg przebiegu (np. skrzydlo pekło -> przerzut), decyzji "wycofaj cala armie", wyboru celu konnicy wg tego, kto zadaje straty.
- **Przypadek z pytania Jeffa** (lucznicy wroga masakruja jedna strone -> szarza konnicy na lucznikow): reaguje tylko PIECHOTA (S2, 291e, 313b, tarcze 140, szyk 146). Konnica AI na PIESZY SZYK lucznikow nie ma zadnej reguly reaktywnej: 204 widzi wylacznie konnego strzelca (Hunt.cs:20), 283 pieszego strzelca tylko samotnego (React.cs:24-25), a S3 "najpierw lucznicy" jest stale i waskie - tylko kultury z Flank >=1.2 (Vale, Reach, Dragonstone, Dorne, Dothraki; Polnoc 1.1 i Westerlands 1.1 - nie), tylko w taktykach FullScaleAttack i RangedHarrass (nie w splitach RBM ani FrontalCavalryCharge) i tylko w chwili zmiany fazy. Cel konnicy zostaje przy RBM (`RBMBehaviorCavalryCharge`, najblizszy duzy szyk).

## 4. Kolizje i zaleznosci

Nasze z naszymi (rozstrzygniete w kodzie, ale kruche):
- Jeden wlasciciel pozycji skryptowej czlowieka: 143 (ranny) > 149/335/359 (bohater) > 148/204/283/311a/344 (jezdzcy, lowcy) > 158 biegacz sztandaru; przekazania przez BeforeBreak/BeforeWithdraw/EndHunter (HeroCaution.cs:42-44, HorsePursuit.cs:43-52). Kazdy nowy modul sterujacy czlowiekiem musi wejsc w ten lancuch.
- Tempo: cap marszu (128/134c) kontra bieg pod ostrzalem (313b) kontra oddech (144) kontra kustykanie (313d) - wszystkie w jednej latce `MarchToContact.PostSpeed`; 344 zdejmuje cap na 30 s (powrot szykiem).
- Szyk: CloseRanks (ShieldWall pod ostrzalem) kontra OutshotRush (Loose, bo mur tarcz ma limit biegu 0.3) kontra arbiter 344 (Line/Loose co 20 s) - trzy moduly pisza to samo ustawienie szyku.
- Taktyka: 291 (trzymaj) kontra S2/291e (idz) - opisane wahadlo "S2 ponownie" (Hold291.cs:45-47); mnoznik Hold kultury naklada sie na S2 x0.3.
- Zatrzymanie armii R5 (283) wylaczone przez 291 (ArmyMarchesPastHarassers) - oba klucze TAK, wygrywa 291.
- Zalamanie kompanii (CH) i panika z bolu ida ta sama droga RoutHold.Break; 359 zawraca raz, druga panika na dobre.

Z RBM (RBMAI):
- RBM czysci liste taktyk i daje zestaw wg kultury - WarCouncil dopisuje PO nim (AfterStart); Tactics dowodcy pod samym RBM nie ma wplywu (WarCouncil.cs:16-19).
- RBM zdejmuje konnicy BehaviorFlank i nie szarzuje na konnice/konnych lucznikow (ChargeCavalry/ChargeHorseArchers=false) - S3 i 204 to obejscia; rozkazy calego szyku RBM nadpisuje co tick, dlatego 148/204/283/344 steruja POJEDYNCZYMI jezdzcami, a "szarza szyku na skrzydlo" (T8) jest swiadomie pominieta (React.cs:41).
- RBM `HasBattleBeenJoined` (75 m) przelacza obrone w Engage wczesniej niz nasze zwarcie 25 m - tylko mierzone (Hold291.cs:49-51). Splity RBM (waga 10) i CoordinatedRetreat sa poza doktryna.
- RBM co klatke `StopRetreating` przy morale > 0 - stad przypiecie morale 0 (RoutHold) i natywny odwrot rannego (143).
- RBM `AdjustSpeedLimitPatch` (konnica zawsze galop) - nasz postfiks po nim; RBM `SetAiRelatedProperties` (Polearm 35, "zolw" w walce) - VeteranAi/ShieldsUp/WoundedWary/DuelAi w jednym postfiksie po nim; RBM odswieza to co 3 s, wiec reakcja czlowieka na rane ma do 3 s opoznienia.
- Latka 146 siedzi na 84-bajtowej metodzie RBM - ryzyko inline (CloseRanks.cs:35-36, licznik w linii).
- RBM wylacza szyk wodza - 335 go zastepuje stanowiskiem.
- Pulapka watkow: AI szykow w zwyklym tempie idzie w watku silnika, w autotescie z przewijaniem w glownym - 333 dzialalo tylko w testach (PROJEKT-344 rozdz. 1B). W logu 11.11: "wywolania arbitra glowny watek 694 / watek AI 7", "czas misji z przewijaniem 99 s / bez 3 s" - autotesty dalej prawie nie cwicza watku AI.
- Inne mody glosu (Immersive Battlefields, ROT Battle Atmosphere) - ForeignVoices ustepuje.

## 5. Co mowia logi (12 logow 09-11.11, 21 bitew polowych, w tym 15 z "tlumem" bandytow; autotesty)

- Wszystkie latki weszly (latki 8/8, potkniecia 0), koszt klatki p99 2-3 ms.
- S1: 1 raz na 21 bitew (skonczone "wrog jednak strzela"). S4: 9 cofniec w 3 bitwach. Zmian taktyki 15 w 6 bitwach.
- S3: "konnica w BehaviorFlank 0" i "najpierw lucznicy 0" we wszystkich 21 (wagi Flank ustawione 6 razy w jednej bitwie, zadnego wejscia konnicy w flanke).
- Taktyki w probkach duzych bitew (dowodca Polnocy T179/T301): prawie wylacznie splity RBM i FullScaleAttack; raz FrontalCavalryCharge w 605 s.
- 148 poscig: 0 wejsc (w tych testach wygrywala strona gracza, a jego szyki sa pomijane - "formacje gracza pominiete 2"); uciekajacych i tak dopadali konni (239 w jednej bitwie).
- 204: 1753 wykrycia w 19 bitwach, 283: 208 epizodow w 14, 344: 7 poscigow / 4 fronty / 11 kontr. 359: 63 udane zawrocenia w 5 bitwach. Zalaman kompanii 53 w 14 bitwach, zlamanych 1288.
- at-395 (bitwy armii): paniki to 50-75% strat (np. 118 panik / 159 poleglych i rannych, 135 / 197), czas 170-370 s.

## 6. Czego nie udalo sie ustalic

1. Zachowania w prawdziwej grze Jeffa po WGRANIU 205 - dostepne logi listopadowe to autotesty (krotkie, z przewijaniem, strona gracza na autopilocie); liczby z pkt 5 nie mowia, jak AI gra przeciw czlowiekowi.
2. Dlaczego S3 nigdy nie daje konnicy w BehaviorFlank: nie sprawdzalem w dekompilacji, czy RBM usuwa zachowanie po naszym postfiksie, czy warunek gry ("wrog zwiazany frontem") nie zachodzi - wymaga czytania RBMAI + BehaviorFlank.
3. Czy `ChargeInfantry=false` faktycznie kieruje szarze RBM na lucznikow - w logach 0 uzyc, dzialanie pola RBM nie zweryfikowane.
4. Rzeczywiste wartosci 102 kluczy u Jeffa: w Armoury.json ich nie ma (przyjalem domyslne z kodu); nie sprawdzalem, czy MCM trzyma je gdzie indziej (preset) ani pliku Armoury.settings.xml.
5. Ryzyko inline latki 146 i udzial watku AI w arbitrze 344 w zwyklym tempie - sa liczniki, ale brak biegu bez przewijania w tych logach.
6. Nie czytalem cial funkcji (poza Doctrine.cs:180-309 i FieldCraft.RollPain) - mapa opiera sie na opisach-naglowkach klas, kluczach Settings.cs i liniach logu; rozjazd opis/kod mozliwy tam, gdzie pozniejsze poprawki (347d-g, 320d, 309) nie trafily do naglowka.
7. Seria "WGRANIE 113-124" jako numery wgran nie mapuje sie 1:1 na numery wpisow - przyjalem wpisy CHANGELOG 114-158 (noc 25.10) jako "bitwe sredniowieczna".
8. Oblezenia (SiegeBreak371, SiegeHold372, SiegeCommanders381 itd.) - poza zakresem tej mapy. Ktore moduly polowe dzialaja tez w szturmie, nie sprawdzalem w kodzie; z naglowkow wynika tylko: FF (pociski 50%), VeteranAi/FieldDuel (BattleSense.InBattle obejmuje szturm), AiBreath w oblezeniu domyslnie NIE, reszta ma bramke IsFieldBattle.
