# ROZPOZNANIE-MODY - kod i ustawienia, ktore ruszaja oblezenia, sily i bunty (14.10.2026)

Zadanie: slowa Jeffa po biegu dwuletnim fin30 - "sily nie sa rowne, oblezenia sa za szybkie i za latwe". Rozpoznanie
TYLKO DO ODCZYTU (bez zmian kodu, MCM, gry). Kazda liczba: [P] pomiar (plik / linia logu), [K] kod (plik:linia),
[U] ustawienie (plik), [S] szacunek. Rok kalendarza = 364 doby (gra liczona pod 84).

## 0. Dla Jeffa (prosto)

1. Oblezenie u nas trwa od rozbicia obozu do decyzji o szturmie srednio 8 dob (mediana 8.4, najkrocej 4.2, najdluzej 12.7) [P] - to tydzien w roku 364 dob; gra byla ustawiona pod rok 84 dni, a wojny Diplomacy juz rozciagnelismy 4.33 x, wiec w jedna wojne wchodzi 4.33 x wiecej oblezen [K][U].
2. Szturm jest "latwy", bo oblegajacy przychodzi z miazdzaca przewaga: przy zdobyciach armia miala mediane 8 x wiecej sily niz obroncy (4.6 tys. na 0.5 tys.) [P]. Gdy mury naprawde trzymaly (22 z 113 decyzji), szturm 2 razy na 3 nie konczyl sie zdobyciem [P] - sama walka o mury dziala.
3. Obroncy sa mali przez kilka regul naraz: nasza zaloga rosnie tylko z platnego werbunku (najwyzej 2 ludzi na dobe), w pokoju trzymamy ja na polowie, nowa druzyna pana bierze ludzi z zalogi siedziby, BK co jakis czas wyprowadza pol zalogi na patrol, a druzyny lordow BK maja x1.5-2 (zalogi nie) [K][U].
4. Gra po 4 dobach oblezenia "traci cierpliwosc": co godzine mury licza sie w decyzji o szturmie mniej, po ok. 6 dobach wcale, a potem nawet pomagaja oblegajacym - tak bylo w 110 z 113 decyzji [K][P].
5. Zaden obcy mod nie wzmacnia oblegajacych w samym szturmie; BK daje kulturom Essos +20% w autobitwie (strzemiona od startu), nasza przewaga Polnocy nie dziala w oblezeniach [K].
6. 14 nowych panstw w 2 lata to bunty: 11 z buntu miasta / zamku (gra + BK robi z buntownikow krolestwo "County / Barony of"), 3 z wojen domowych Diplomacy, wszystkie poza Westeros (Wolne Miasta, Rhoyne, Lorath, Vaes Diaf) [P]; Diplomacy losuje bunt co dobe z szansa liczona pod rok 84 dni [K][U].
7. Dzwignie (szczegoly w rozdz. 8): tempo budowy obozu (nasz suwak 50% -> ok. 12%), cierpliwosc szturmu x4.33 (nowa latka), mocniejsze mury w szturmie, wieksze zalogi (nasze klucze), do tego spojnosc armii i zaraza obozowa pod dluzsze oblezenia, bunty (BK bez "krolestw z buntu", Diplomacy szanse x0.23) [S].
8. Ustawienia obcych modow (BK, RealisticBannerlord, Diplomacy) - tylko z kopia i za Twoja zgoda; uwaga: 17.09 sam kazales "ustawic oblezenie na wskaznik 1" (RBL 0.4 -> 1.0), bo Twoje oblezenie bylo za wolne - wolniejsza budowa obozu dotknie tez Ciebie, chyba ze rozdzielimy AI i gracza [U].

---

## 1. Zrodla, skroty, metoda

| Skrot | Sciezka |
|---|---|
| G | gra 1.4.8, dekompilacja `SC(ae222cb7)\dec32\cs_full` |
| BK | BannerKings.dll (bez zmian od 21.08), dekompilacja `SC(ae222cb7)\e4tools\bkfull` |
| D\<mod\> | swieza dekompilacja (ilspycmd 9.1, 14.10) w `SC(c36b73ca)\scratchpad\dec\<mod>`: `rot` (ROT-Core ROT.dll), `bkrot`, `rbl`, `dr`, `sca` (StrategicCampaignAI145.dll - jedyny DLL w SubModule.xml), `rbm_*`, `dipl` (Diplomacy 1.4.7), `dte`, `naval` (NavalDLC.dll), `beco` |
| INT | nasz kod `SC(ae222cb7)\zb\int` (w-toku/zbior-e8; fin30 = 6e6313c) |
| L | `D:\Backup-Bannerlord\logs\2026-10-14-noc` (fin30: `Armoury-2026-10-14_06-01-15.log`, `session-2026-10-14_06-01-14.log`) |
| U-xx | `Documents\...\Configs\ModSettings\Global\<mod>\*.json` (Jeff), `Configs\DynamicReinforcements.cfg`, `Configs\RBM\config.xml`, `LauncherData.xml` |
| R | pliki robocze `docs\oblezenia\robocze\mody\` (rozdz. 10) |

SC(ae222cb7) = `C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad`,
SC(c36b73ca) = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad`.

Kolejnosc ladowania [U LauncherData.xml]: ... BannerKings, BetterEconomy, NavalDLC, BirthAndDeath, RBM, ROT-Core/Content/Map/Dragon, TXP, RealisticBannerlord, RealisticLoot,
DTE, StrategicCampaignAI145, Diplomacy, DynamicReinforcements, RealisticCaptivity, Armoury, ..., GrandTourney, **BKROTPatch** (ostatni), ROT_Hideouts. Wylaczone: AIInfluence,
FastMode. Model gry = OSTATNI dodany danego typu [K G-TaleWorlds.Core GameModelsManager.GetGameModel - petla od konca]; potwierdzenie kolejnosci w logu:
lancuch jedzenia "RBL -> ROT -> NavalDLC -> BEE -> BK" [P L Armoury:499-500].

## 2. Ktory model jest czynny (oblezenie, sily)

| Typ modelu | Czynny (od zewnatrz do srodka) | Latki Harmony na Default | Dowod |
|---|---|---|---|
| SiegeEventModel (budowa obozu i machin) | RBL `RealisticSiegeEventModel` -> Default | BK postfiks GetConstructionProgressPerHour (LongerSieges), nasz WorldPace (raz, licznik zagniezdzenia) | [K D rbl/RealisticBannerlordSubModule.cs:54; BK VanillaModelTweakPatches.cs:1252-1269; INT WorldPace.cs:128-145]; [P L Armoury: "WorldPace: ... budowa oblezen 50% (2 modeli)"] |
| CombatSimulationModel (autobitwa, szturm) | ROT `ROTCombatSimulationModel` -> NavalDLC -> Default | BK SimulateHit (SiegePlanner, Stirrups); nasza NorthHomeEdge (Low); CS ValyrianWardSim (Last) | [K D rot/ROT/SubModule.cs:227; D rot/ROT.Models/ROTCombatSimulationModel.cs:90-99 - tylko smoki; D naval/...NavalDLCCombatSimulationModel.cs:66-69, 117-188 - tylko morze] |
| MilitaryPowerModel (sila w symulacji i w decyzjach) | ROT -> NavalDLC -> Default | RBM lata `CharacterObject.GetPower` - symulacja 1.4.8 go NIE uzywa | [K G DefaultMilitaryPowerModel.cs:85-94 (GetDefaultTroopPower, tier), D rbm_RBMCombat/RBMCombat/CampaignChanges.cs:511-590] |
| ArmyManagementCalculationModel (spojnosc, zakladanie armii) | **SCA `StrategicArmyManagementModel`** (dziedziczy z Default - lancuch BK/NavalDLC/ROT pominiety) | BK postfiks CalculateDailyCohesionChange (CohesionBoost), BKROT (blogoslawienstwa - religia wylaczona) | [K D sca/.../SubModule.cs; StrategicArmyManagementModel.cs:68-78]; [S] kolejnosc z LauncherData - linii w logu brak (rozdz. 9) |
| TargetScoreCalculatingModel (wybor celu) | **SCA `StrategicTargetScoreModel`** -> Default | BK postfiksy (FrontFocus, casus belli, RaidIncentive) | [K D sca/.../StrategicTargetScoreModel.cs; BK VanillaModelTweakPatches.cs:1627-1750] |
| SettlementGarrisonModel | ROT -> NavalDLC -> BK `BKGarrisonModel` (: Default) | BK +1 "Enlistment"; nasz K8 / K1 | [K D rot/ROT.Models/ROTSettlementGarrisonModel.cs; BK BKGarrisonModel.cs; docs/etap4/PLAN-KODU-E48.md:126] |
| SettlementMilitiaModel | ROT EasyCastle -> ROT Militia -> NavalDLC -> BK `BKMilitiaModel` | - | [K D rot/ROT/SubModule.cs:224, 228] |
| SettlementFoodModel | ROT -> (NavalDLC brak) -> BK `BKFoodModel` | nasze WinterBite / VillageBook / GL2 / Famine | [K D rot/ROT/SubModule.cs:225; BK BKFoodModel.cs:29-35] |
| PartySizeLimitModel | ROT -> NavalDLC -> Default | BK postfiks GetPartyMemberSizeLimit (PartySizes) | [K BK VanillaModelTweakPatches.cs:814-878] |
| SettlementLoyaltyModel | BK `BKLoyaltyModel` (: Default; log: czynny BKLoyaltyModel) | ROT: RebellionStartLoyaltyThreshold = 10 | [P L Armoury:303; K D rot/ROT.HarmonyPatches.Core/DefaultLoyaltyPatch2.cs:11] |

## 3. Oblezenie krok po kroku - co zmienia ktory mod

Wiersz "364?" = czy liczba ma sens przy roku 364 dob (zasada Jeffa "1 dzien = 1 dzien": doba to doba; to, co gra rozpisala na doby pod rok 84, w latach dzieje sie 4.33 x szybciej).

| Etap | Gra (wzor) | Mod | Wartosc u Jeffa | Dowod | 364? |
|---|---|---|---|---|---|
| Wybor celu | armia zaczyna oblezenie tylko przy sile >= 2 x (obroncy x (1 + 0.33 x poziom murow - agresja wodza) + rezerwa) | BK: casus belli x1.2 na lenno sporu, front +FrontFocus | FrontFocus 0.1 [U BK] | [K G DefaultTargetScoreCalculatingModel.cs:226, 235-241; BK VanillaModelTweakPatches.cs:1706-1745] | tak (nie zalezy od czasu) |
| Wybor celu | - | **SCA** (model celu) mnozy wynik gry TYLKO gdy gra dala > 0 (prog 2 x zostaje): cel przy wlasnej warowni (55 jedn.) x2.4, przy wrogiej x0.18, > 85 jedn. od wlasnej x0.55, przesmyk x1.8, miasto / bogaty x1.45, roszczenie kultury x1.65, jency-szlachta x1.8, cel na cooldownie / wrogie druzyny > 1.25 x / jedzenie < 3 dob x0.25, zmeczenie x0.55, chec pokoju x0.35. Uwaga: w wczytanym DLL (StrategicCampaignAI145 v0.1.9) role armii i "cel agresywny" (AssignArmyRoles, TryRunAssignedArmyRole, MoveToAggressiveTarget) NIE sa wolane - martwy kod; wola je tylko starszy StrategicCampaignAI.dll, ktorego SubModule.xml nie laduje | stale w kodzie | [K D sca/StrategicCampaignAI145/StrategicTargetScoreModel.cs:13-93; StrategicCampaignAI145Behavior.cs:49-70 (godzinowo tylko odwrot i wzmocnienie zalogi), :98, :126 (bez wywolan); D sca0/StrategicCampaignAI/StrategicCampaignAiBehavior.cs:34, 52; U Modules/StrategicCampaignAI145/SubModule.xml] | tak |
| Zalozenie armii | wplyw, jedzenie 15 dob | SCA: armia gdy >= 2 czlonkow i sila >= 260; cooldown po porazce 2 doby | - | [K D sca/.../StrategicArmyManagementModel.cs:20-38; StrategicAiTuning.cs] | cooldown 2 doby - krotki, ale doba to doba |
| Przygotowania obozu | postep/h = sqrt(zdrowych) / (48 osobodni x 24) | BK LongerSieges x(1-0.5); RBL x SiegeConstructionSpeedMultiplier, x0.75 bez Tools w sakwach oblegajacych; obronca x1.25 (Tools x1.5, sufit 1.2); **nasz SiegePacePercent x0.5** | BK 0.5 [U BK]; RBL 1.0 [U RBL] (w kodzie 0.4 gdy MCM nie wczyta - rozdz. 9); Armoury 50 [U Armoury.json] | [K Native/ModuleData/siegeengines.xml (preparations 48, taran 12, wieza 16/24, katapulta 16, trebusz 24); G DefaultSiegeEventModel.cs:351-356, 403-414; BK :1264-1266; D rbl/.../RealisticSiegeEventModel.cs:18-41; INT WorldPace.cs:128-145] | NIE: lacznie x0.19-0.25 vanilli; armia 2-4 tys. robi oboz w 4-6 dob [S], przy roku 84 bylby to ~5% roku, przy 364 - ~1.5% |
| Machiny, mury | obrazenia machin, punkty murow | BK: perk SiegeEngineer +10% w mury, SiegePlanner +25% murow, CivilEngineer / SiegeEngineer darmowa katapulta / balista | perki - zalezne od edukacji BK | [K BK VanillaModelTweakPatches.cs:1271-1315, 1384-1401] | tak |
| Decyzja o szturmie | po 100% przygotowan: stosunek = oblegajacy / (obroncy x przewaga_murow^w), w = 0.8 (x1.25 bez taranu, x1.25 bez wiezy), **po 4 dobach w maleje o 0.02 na godzine** (zero po ok. 1.7-2.6 doby wiecej, potem UJEMNE - mury pomagaja oblegajacemu); potem kara za brak 3 machin; szansa na godzine = 10% stosunku | brak modow; nasz okup czyta te decyzje (postfiks) | - | [K G BesiegerCamp.cs:375-428 (:408-409 czas, :424-427 machiny i losowanie); INT SiegeRansom.cs:16-45, 264] | NIE: "4 doby cierpliwosci" i 0.02/h to stale pod rok 84 |
| Decyzja - pomiar | - | - | - | [P R fin30-decyzje-szturmu-O0.csv]: 113 decyzji, doba oblezenia mediana 8.4 (p10 6.4, p90 10.7, max 12.7); wykladnik murow ujemny w 110/113 (mediana -1.66); stosunek "z pelnymi murami" (bez czlonu czasu) mediana 1.50, < 1 w 22 decyzjach | - |
| Szturm (symulacja) | obroncy dostaja wiecej tur: tury_obr = 2 x round(0.5 + max(1 + N^0.3 x 0.7 x przewaga, ...)), oblegajacy 2 x round(1.5 + N^0.3); oblegajacy -10% przewagi; przewaga = (5 + poziom murow - 1) / (1 + premie machin), mury zburzone x0.25 | ROT, NavalDLC - przepuszczaja (ROT tylko smoki: obrazenia stale); BK +15% strzelcom piechoty dowodcy z SiegePlanner; **BK +20% obrazen calej kultury ze Stirrups** (BKROT daje Stirrups od startu kulturom Essos); NorthHomeEdge - **nigdy w oblezeniu**; RBM - brak (GetPower nie wchodzi do symulacji) | NorthHomeEdgePercent 10 (domyslna, brak w json) | [K G DefaultCombatSimulationModel.cs:172-227 (:209), 241-280 (:253), 282-290 (:288); BK VanillaModelTweakPatches.cs:293-325 (:320-322); D bkrot/BKROTPatch.Patches/GetCultureDefaultInnovationsPatch.cs; INT NorthHomeEdge.cs:16-33] | tak |
| Szturm - pomiar | - | - | - | [P L Armoury "Okup oblezenia (doba)" 583 linii]: szturmow zakonczonych 74, **bez zdobycia przy trwajacym oblezeniu 19**; zdobycie 1-9 dob po decyzji (mediana 4-5) [P R fin30-decyzje-szturmu-O0-wynik.csv]; przy zdobyciu surowy stosunek sil mediana 8.0 (min 2.9, p90 13.4), "z pelnymi murami" mediana 2.2; przy murach < 1: 7 zdobyc z 22 decyzji | - |
| Glod oblezonych | zapas: dobrobyt/40 + zaloga/20 na dobe; w oblezeniu bez doplywu wsi | BK: magazyn 500, zamek +250, jedzenie od ludnosci; ROT: King's Landing +100/d, Red Keep +120/d; nasze: schronienie uchodzcow w murach (FamineSiegeShelter) zjada zapas; RBL: straty zalogi 1-2/d (2-4 przy glodzie) - CrashScribe puszcza je TYLKO gdy spichlerz pusty | FamineSiegeShelter true (domyslna); RbSiegeAttritionOnlyStarving true | [K G DefaultSettlementFoodModel.cs:43-97; BK BKFoodModel.cs:19-35, 113; D rot/ROT.Models/ROTSettlementFoodModel.cs:37, 43; INT Settings.cs:892, Famine.cs:108; D rbl/.../SiegeAttritionBehavior.cs:55-66; INT CrashScribe Mends.cs:3205-3260, Config.cs:71] | na dobe - tak; dzis glod nie decyduje (szturm po 8 dobach) [S] |
| Straty oblegajacych | ostrzal machin obroncy | nasza zaraza obozowa (CampFever): od 9. doby 0.6%/d x (1 + 0.15 x dni ponad 9) x tlok, 10% chorych umiera, obroncy 40% tempa (glod x2); RBL straty obozu 2-5/d - CS tylko przy glodzie obozu | SiegeSickness* (json: 9 / 0.6 / 15 / 40 / 10 / 50) | [K INT CampFever.cs:14-31, 64, 145-154; Settings.cs:445-451]; [P] 90 linii "CampFever: ... zaraza wstaje" w fin30 [P R fin30-linie-oblezen.txt] | zaraza od 9. doby przy szturmie w 8. - prawie nie gra [S] |
| Spojnosc armii | -2/d, -0.25 na partie, -0.125 x (glodne+1); w oblezeniu armia sie nie rozwiazuje przy 30, dopiero przy 0; przy < 50 AI dokupuje spojnosc wplywem | BK CohesionBoost x(1-0.5); SCA -3.5/d, gdy armia 'gleboko' (najblizsza wlasna warownia >= 75 jedn. i wroga <= 55) od > 5 dob (po polowieniu BK); BK ArmySiegePersistence trzyma armie przy celu, ktory 'mignal' z tablicy (do 24 h) | CohesionBoost 0.5, ArmySiegePersistence true [U BK] | [K G DefaultArmyManagementCalculationModel.cs:45, 232-235; G Army.cs:354-361; BK :1447-1479; D sca/.../StrategicArmyManagementModel.cs:76; StrategicAiHelpers.cs:464-477; StrategicCampaignAI145Behavior.cs:33-46; BK ArmySiegePersistencePatches.cs] | armia 5 partii: oblezenie przy wlasnej granicy ok. -1.7/d -> 50 w ok. 29. dobie; gleboko ok. -5.2/d -> 50 w ok. 13., 0 w ok. 23. [S z wzoru; AUDYT-CZAS-MAPA 5.1] - limit dlugosci oblezenia, jesli je wydluzymy |
| Odstapienie | - | SCA zdejmuje oblezenie, gdy wrogie druzyny w 95 jedn. maja > 1.25 x sily armii i sojusznikow (stosunek < 0.8) | stale w kodzie | [K D sca/.../StrategicCampaignAI145Behavior.cs:302-326] | tak |
| Okup zamiast szturmu (nasz) | - | miasto placi 1/4 monety, gdy mury nie wytrzymaja; AI bierze, gdy szturm krwawy (stosunek z pelnymi murami < 1.5), w potrzebie albo po nieudanym szturmie; rozejm 60 dob; nigdy stolica i fabula ROT | SiegeRansomAi true, Share 0.25, BloodyRatio 1.5, Truce 60 (domyslne) | [K INT SiegeRansom.cs:16-45; Settings.cs:2148-2160]; [P]: 26 okupow AI z AI, wszystkie przy stosunku 1.0-1.5 [P R ...-wynik.csv] | tak |
| Pobici pod murami (nasz) | gra: pobity oblegajacy ginie / do niewoli | LosersFleeBesiegers: wypad / bitwa pod murami - pobity oblegajacy jak w polu (rozbici wracaja) | true (domyslna) | [K INT Settings.cs:823]; [P] 1456 linii "Przegrani pod murami (N3)" | tak |
| Po zdobyciu (nasze) | gra: lup z niczego | lup z prawdziwej monety (1/3 / 2/3), blizna -15% dobrobytu i -15 lojalnosci, budowle plona, 3 doby bez wydatkow | SackScar 15 / 15 [U Armoury.json] | [K INT Settings.cs:2093-2123]; [P] 57 linii L2/L0 | lojalnosc -15 sprzyja buntom w zdobytych miastach [S] |
| Fabula ROT | ROT wymusza oblezenia: Harrenhal (Roose), Czarny Nurt, Mur, Bekarty; przy szturmie milicja = 0.6 x armii | CrashScribe rozciaga os x4.33 | FabulaTimeScale 4.33 [K INT CrashScribe Config.cs:31] | [K D rot/ROT.Events/HarrenhalSiegeEvent.cs:181, 196-207, 222-233; INT Fabula.cs:36-58]; [P] Harrenhal zdobyty d127 bez linii O0 (skrypt ROT), Czarny Nurt = d766 (poza 2 latami) | tak |

## 4. Sily: zalogi, milicja, druzyny

| Co | Gra | Mod / nasze | Wartosc | Dowod | Skutek dla oblezen |
|---|---|---|---|---|---|
| Limit zalogi | zamek 200, miasto 400 + przywodztwo + budynki | nikt nie zmienia (ROT, NavalDLC przepuszczaja; BK postfiks tylko partie z bohaterem) | - | [K G DefaultPartySizeLimitModel.cs:172-180; BK VanillaModelTweakPatches.cs:814-822] | sufit obrony stoi, gdy druzyny rosna |
| Druzyny lordow | 20 + tier rodu | **BK PartySizes: glowa rodu x2, inni x1.5** (+ tytul do +40%) | PartySizes 2.0 [U BK] (zakres 1-3) | [K BK VanillaModelTweakPatches.cs:826, 830, 869-873] | armie 2-10 tys. na zalogi 0.1-1.8 tys. (sila) [P O0] |
| Przyrost zalogi | model zalogi (budynki, +2 buntownikom) + werbunek od notabli 1/d | **nasze K1: przyrost z modelu = 0**; A5 platny werbunek; **K8: do 2/d**; zaloga rosnie tez z druzyn i patroli BK | GarrisonGrowthOnlyRecruits true, GarrisonAutoRecruitPerDay 2 (domyslne; MCM 1-5) | [K INT ArmsFromPeople.cs:24-31, 56, 336; Settings.cs:917-919]; [P L E4/8 x728 dob]: A5 123 041 ludzi, K8 dobrano 60 152, zablokowany przyrost modelu 103 301 (gorna granica) | odbudowa 150 ludzi zalogi = 75 dob przy 2/d [S] |
| Zaloga w pokoju | - | **B4: w pokoju zaloga do 0.5 sredniej wojennej** (po pieniadzach roku 2: 0.7), wszystkie zalogi rodu najwyzej 0.8 pulapu; w wojnie pulap gry (GarrisonWarFull) | fin30: 0.5 [K git 6e6313c Settings.cs:1102]; dzis 0.7 [K INT Settings.cs:1113] | [K INT ClanBudget.cs:686-712, 920-931] | wojna zaczyna sie z polowa zalog [S] |
| Nowa druzyna pana | szablon z niczego | **K3: najpierw wolne rece regionu, potem zaloga siedziby** (zostawia 50 w miescie / 25 w zamku) | NewPartyFromSeatGarrison true | [K INT ArmsFromPeople.cs:30, 615]; [P L E4/8]: z zalog siedzib 4 068 ludzi w 728 dob | drenaz siedzib po porazkach [S - maly w sumie] |
| Zostawianie ludzi w zalodze | lord zostawia czesc druzyny | BK: tylko w wojnie, tylko gdy zaloga <= 500: vanilla/3 + druzyna x (2.5% + 2% na wroga) | - | [K BK BKGarrisonModel.cs:9-38] | zalogi rosna wolniej niz w grze |
| Patrole BK | - | **5%/d gdy zaloga >= 100 zdrowych, bezpieczenstwo > 50, lojalnosc > 30: wychodzi 30..50% zalogi na >= 48 h** | PatrolParties true [U BK] | [K BK BKSettlementBehavior.cs:446-460; BK GarrisonPartyComponent.cs:44-66] | czesc doby zaloga jest polowiczna [S] |
| Wzmocnienie zalogi SCA | - | armia w slabej warowni (sila < 420) oddaje 18 ludzi, ale do `settlement.Party.MemberRoster`, nie do partii zalogi | stale | [K D sca/.../StrategicCampaignAI145Behavior.cs:328-341] | [S] mozliwe, ze ludzie nie trafiaja do obrony (rozdz. 9) |
| Milicja | przyrost z dobrobytu itp. | BK: limit = ludnosc x 0.01 (+200 zamek, +20 wies), przyrost x0.5; ROT: +10/d "City Watch" (KP, Mur, Czarny Zamek), +5/d 14 miastom, +1 trzem; nasze MilitiaByPeople / MilitiaFromFreeHands (z wolnych rak regionu) | MilitiaGrowthMultiplier 0.5, MilitiaPopulationFactor 0.01 [U BK]; debuffedMilitias false [U ROT] | [K BK BKMilitiaModel.cs:129, 155, 163; D rot/ROT.Models/ROTSettlementMilitiaModel.cs:25, 41; ROTEasyCastleMilitiaModel.cs:39, 49; INT Settings.cs:920, 2192]; [P L E4/8] milicja +198 468 w 728 dob | milicja broni murow i liczy sie w decyzji o szturmie |
| Asymetria kultur | - | BK Stirrups +20% obrazen w autobitwie; BKROT: Essos ma Stirrups od startu, Westeros i Polnoc nie | Innovations 0.80 [U BK] | [K BK :320-322; D bkrot/.../GetCultureDefaultInnovationsPatch.cs; CultureHelpers.cs] | Essos vs Westeros nierowno [S]; czy Westeros dorobil Stirrups w 2 lata - rozdz. 9 |
| Przewaga Polnocy (nasza) | - | piechota Polnocy t3-t6 x1.07 na cios (10% mocy ^0.7), tylko bitwa polowa na sniegu / w lesie Polnocy | NorthHomeEdgePercent 10 | [K INT NorthHomeEdge.cs:16-33, 202-227] | w oblezeniach 0 |
| DynamicReinforcements | - | tylko bitwy gracza (misja) i "Call to arms" gracza | AllowSieges true itd. [U DR] | [K D dr/.../Core/SubModule.cs:100-118; Battle/BattleDetectorBehavior.cs (IsPlayerMapEvent); Scanning/CallToArmsBehavior.cs:41] | AI z AI: 0 |
| RBM | - | sila postaci (GetPower) i przezywalnosc - symulacja 1.4.8 liczy z tieru (GetDefaultTroopPower) | - | [K D rbm_RBMCombat/RBMCombat/CampaignChanges.cs:25-80, 511-590; G DefaultMilitaryPowerModel.cs:85-94, 244-253] | AI z AI: 0 (przezywalnosc - zabici/ranni) |
| DTE | - | konwoje zaopatrzenia (sprzet do druzyn), zbrojownie zalog w bitwach gracza | - | [K D dte/.../CutTheirSupplyBehavior.cs:266-291] | AI z AI: 0 (symulacja nie patrzy na sprzet) |
| NavalDLC | - | modele zalogi / milicji / symulacji przepuszczaja (ladowe), blokada portu = gra 1.4 | - | [K D naval/NavalDLC.GameComponents/NavalDLCSettlementGarrisonModel.cs; ...MilitiaModel.cs:16-38; ...CombatSimulationModel.cs] | brak zmian na ladzie |
| BetterEconomy | - | "gotowosc zalogi" zamku (wskaznik), XP zalogi | - | [K D beco/BetterEconomy.Behaviors/CastleEconomyCampaignBehavior.cs:166-219, 415-449] | brak wplywu na oblezenie |

## 5. Bunty i nowe krolestwa

| Droga | Warunek | Wartosc | Dowod |
|---|---|---|---|
| Bunt miasta (gra) | codziennie 25% sprawdzenia: lojalnosc <= prog **i** milicja >= 1.4 x (zaloga + lordowie wlasnej frakcji w murach); nie w oblezeniu | prog ROT 10 (gra 15) | [K G RebellionsCampaignBehavior.cs:104-112, 187-204; D rot/.../DefaultLoyaltyPatch2.cs:11] |
| Bunt zamku (BK) | ta sama regula dla zamkow; liczy tylko zaloge (+ obcych niewrogich lordow) | - | [K BK BKSettlementBehavior.cs:674-708] |
| Buntownicy -> krolestwo (BK) | po 30 dobach klan buntownikow z lennem staje sie zwyklym klanem, a BK od razu robi z niego KROLESTWO (nazwa tytulu BK: "County / Barony of X") i wywoluje wojny z wrogami | `_disableRebelKingdom` = false (tylko cheat BK) | [K G RebellionsCampaignBehavior.cs:147-160; BK BKClanBehavior.cs:44, 71-82, 454-478; BK BannerKingsCheats.cs:2466] |
| Wojna domowa (Diplomacy) | co dobe dla kazdego rodu: start frakcji 0.05, dolaczenie 0.1, start wojny 0.1 (przy krytycznym poparciu); przerwa po wojnie domowej, frakcja wygasa | 0.05 / 0.1 / 0.1; MinimumTimeSinceLastCivilWarInDays 1000; MaximumFactionDurationInDays 520 [U Diplomacy] | [K D dipl/Diplomacy.CampaignBehaviors/CivilWarBehavior.cs:136-158; Diplomacy/Settings.cs:301-317; CivilWar.Actions/CreateFactionAction.cs:104-115] |
| Znikanie krolestwa (Diplomacy) | przy pokoju krolestwo bez lenn, w wojnie z kims jeszcze -> usuniete; zwrot lenn przy pokoju z wyczerpania (EnableFiefRepatriation) | EnableKingdomElimination true, EnableFiefRepatriation true [U] | [K D dipl/.../KingdomPeaceAction.cs:319-330, 368] |
| Nasze reguly lojalnosci | danina ludu do 60 (podloga), pobor W7 nie ponizej 40, blizna lupu -15 po zdobyciu | FolkWarAidLoyaltyFloor 60, LordWarLevyMinLoyalty 40, SackLoyaltyHit 15 | [K git 6e6313c Settings.cs:1753-1756, 1787-1789, 2044] - nasze podatki nie zbijaja lojalnosci do progu 10; blizna moze [S] |
| Pomiar fin30 | 14 nowych panstw: Antaryon League (Braavos Rebels) d204, Confederation of Phassar (Lorath Rebels) d427, Barony of Bisa d451, County of Pentos d492, County of Myrth d517, Barony of Ulentor d539, County of Norvos d542, Barony of Ghoyan Drohe d595, Drahar Conspiracy (Myr Rebels) d608, Barony of Ny Sar d627, County of Draconys d640, Barony of Tolerrol d690, Rakh Jhattar of Vaes Diaf d697, County of Bolozo d701 | 11 drog BK, 3 Diplomacy | [P L Armoury "Wojna i pokoj (E7/0)" przyczyna CausedByKingdomCreation, 16 linii] |

## 6. Pomiar fin30 z naszych linii (oblezenia)

| Miara | Wartosc | Zrodlo |
|---|---|---|
| Decyzje gry o szturmie (O0) | 113 (miasta 30, zamki 83) | [P L "Okup oblezenia (O0)"; R fin30-decyzje-szturmu-O0.csv] |
| Doba oblezenia przy decyzji | mediana 8.4, p10 6.4, p90 10.7, min 4.2, max 12.7 | jw. |
| Sila obroncow / oblegajacych przy decyzji | mediana 580 / 3 466 (min 125 / 1 417, max 1 765 / 10 133) | jw. |
| Przewaga murow (gra) | mediana 4.26 (3.06-5.56) | jw. |
| Wykladnik murow ujemny (mury pomagaja oblegajacemu) | 110 z 113 | jw. |
| Stosunek z pelnymi murami < 1 / < 1.5 / < 2 / < 3 | 22 / 57 / 72 / 99 decyzji | [R fin30-decyzje-szturmu-O0-wynik.csv] |
| Wynik w 10 dob po decyzji | zdobyte 54, okup 26, inne 33 (odparte / odsiecz / rozpad) | jw. (zdobycia z linii L5b-2, okupy O1) |
| Zdobycia razem / bez decyzji O0 | 57 / 2 (Harrenhal d127 - skrypt ROT; Tumbleton d617) | [P L "Karawany w zdobytym miescie (L5b-2)"] |
| Szturmy zakonczone / bez zdobycia przy trwajacym oblezeniu | 74 / 19 | [P L "Okup oblezenia (doba)", suma 583 linii] |
| Okupy AI z AI | 26 (wszystkie przy stosunku 1.0-1.5) | [P L "Okup oblezenia (O1)"] |
| Zaraza obozowa "wstaje" (oblezenia > 9 dob) | 90 | [P L "CampFever: oblezenie"] |
| Ludzie z zalog siedzib do nowych druzyn (K3) | 4 068 / 728 dob | [P L "Wojsko z ludzi (E4/8)"] |

Uwaga do "0 odpartych szturmow" z zadania: nasz licznik (SiegeRansom.OnMapEventEnded - szturm, ktorego nie wygrali oblegajacy, a oblezenie trwa)
pokazuje 19 takich szturmow w fin30 [K INT SiegeRansom.cs:332-352; P jw.]. Ktora miara jest wlasciwa - do uzgodnienia z rozpoznaniem CZAS (rozdz. 9).

## 7. Co jest liczone "na dobe / godzine" i czy to ma sens przy roku 364

| Liczba | Gdzie | Dzis | Uwagi |
|---|---|---|---|
| Cierpliwosc szturmu 4 doby + 0.02/h | G BesiegerCamp.cs:408-409 | stala gry | pod rok 84; w roku 364 x4.33 = 17.3 doby i 0.0046/h [S] |
| Budowa obozu i machin | G DefaultSiegeEventModel + BK + RBL + nasze | x0.19-0.25 vanilli | oboz 4-6 dob [S]; historycznie oboz 1-2 tyg., oblezenie zamku tygodnie-miesiace, miasta miesiace (AUDYT-CZAS-MAPA 3.1-3.2) |
| Wojna (Diplomacy) | U Diplomacy | min. wojna 91 dob, zmeczenie /4.33 (od 14.10 rano - czynne w fin30) | wojny trwaja w dobach 4.33 x dluzej, oblezenia tyle samo dob -> 4.33 x wiecej oblezen na wojne [S] - sedno "1 dzien = 1 dzien" |
| Spojnosc armii | G, BK, SCA | -2/d (x0.5 BK), SCA -3.5/d tylko 'gleboko' | oblezenie przy granicy wytrzyma ok. 4 tyg. do progu 50, gleboko ok. 2-3 tyg. bez doplat wplywem [S] |
| Zaraza obozowa | INT CampFever | od 9. doby | przy dluzszych oblezeniach mocna (do 30. doby ok. 1/3 ludzi przechodzi chorobe [S z wzoru]) |
| Przyrost zalogi | INT K8 | 2/d | doba to doba - zgodne |
| Patrole BK | BK | 5%/d | zgodne (stan, nie zdarzenie roczne) |
| Bunt miasta | G | 25%/d przy warunku stanu | zgodne (warunek stanu) |
| Wojna domowa Diplomacy | U Diplomacy | 0.05 / 0.1 / 0.1 na dobe | NIE: szansa zdarzenia na dobe pod rok 84 (jak ciaza i sluby) - wedlug zasady x84/364 = 0.0115 / 0.023 / 0.023 [S] |
| Rozejm po okupie | INT | 60 dob | doba to doba |
| Klan buntownikow -> krolestwo | G stala 30 dob | 30 | doba to doba |

## 8. DZWIGNIE

Typ: [M] nasz klucz MCM (zmiana wartosci; gdy klucz jest w Armoury.json - poprawka pliku z kopia), [N] nowy kod / latka Harmony u nas,
[O] ustawienie obcego moda (tylko z kopia i za zgoda Jeffa). Skutki to szacunki [S] z fin30 - do sprawdzenia biegiem.

| # | Dzwignia | Typ / gdzie | Dzis | Propozycja | Skutek [S] | Ryzyko |
|---|---|---|---|---|---|---|
| D1 | Tempo budowy obozu i machin | [M] Armoury SiegePacePercent (Armoury.json ma 50; MCM 0-200) | 50 | 12-25 (x84/364 = 12) | oboz 4-6 -> 9-25 dob; decyzja o szturmie z ~8 do ~15-30 dob | dotyczy tez gracza (Jeff 17.09: za wolne); bez D9 (i D8 gleboko) obozy topnieja przed szturmem; rozdzielic AI / gracz = [N] |
| D2 | To samo w obcych modach | [O] BK LongerSieges 0.5 -> 0.75 (max); RBL 1.0 -> 0.4 | 0.5 / 1.0 | lepiej nie ruszac - jedno pokretlo (D1) | jak D1 | RBL 1.0 to decyzja Jeffa 17.09 |
| D3 | Cierpliwosc szturmu | [N] postfiks/prefiks `BesiegerCamp.StartingAssaultOnBesiegedSettlementIsLogical` (juz mamy postfiks w SiegeRansom.cs:264) - nowy klucz np. SiegePatienceScale | 4 doby, 0.02/h, wykladnik do ujemnych | start 17.3 doby, 0.0046/h, wykladnik nie ponizej 0 (albo nie ponizej w0) | 22 decyzje z murami < 1 czekaja do ok. 4 tyg. (z nich 7 zdobyc w fin30); z D1 - szturm najwczesniej po ~2.5-4 tyg. | glod i zaraza zaczynaja grac; gracz-obronca dluzej w oblezeniu |
| D4 | Mocniejsze mury w szturmie i w decyzji | [N] postfiks na Default/ROT `GetSettlementAdvantage` (np. x1.5-2; ta sama liczba wchodzi do decyzji i do tur symulacji) | przewaga 3.1-5.6 | x1.5-2 | stosunek z murami / 1.5-2 (w0 ~1): zdobycia przy murach < 2 to 24 z 54 -> czesc czeka, czesc idzie w okup, wiecej odpartych; ~-25..-45% zdobyc | szturm gracza tez trudniejszy; tury symulacji rosna (wydajnosc szturmu - drobna) |
| D5 | Zalogi wieksze | [M] GarrisonAutoRecruitPerDay 2 -> 4-5 (MCM 1-5; brak w json); GarrisonPeaceShare 0.5 (fin30) -> 0.7 (scalone) -> 0.85-1.0 | 2 / 0.5 | 4 / 0.85 | odbudowa 150 ludzi 75 -> ~38 dob; wojna zaczyna sie z 85% zalogi zamiast 50% | zold zalog (budzet rodow), ochotnicy notabli (ksiega ludzi) |
| D6 | Sufit zalogi wobec PartySizes BK | [N] postfiks CalculateGarrisonPartySizeLimit x(1 + (PartySizes-1) x 0.5) albo nowy klucz | 200 / 400 | x1.5 | obroncy przy decyzji mediana 580 -> do ~870 tam, gdzie zaloga jest przy sufice | koszt zalog; jedzenie zalogi /20 |
| D7 | Druzyny lordow | [O] BK PartySizes 2.0 -> 1.5 | 2.0 | 1.5 | armie ~-25%, stosunek sil spada o tyle | Inni (horda), gospodarka zoldu, wszystkie bitwy |
| D8 | Spojnosc armii w oblezeniu | [N] postfiks na SCA `StrategicArmyManagementModel.CalculateDailyCohesionChange` (zdjac -3.5, gdy armia oblega) albo [O] BK CohesionBoost 0.5 -> 0.8 (max) | -3.5/d 'gleboko' po 5 dobach; x0.5 | -3.5 -> 0 w oblezeniu; ewentualnie x0.5 calosci w oblezeniu | oblezenie moze trwac 4-8 tyg. bez rozpadu | potrzebne przy D1/D3 dla oblezen 'gleboko'; przy granicy wystarcza dzis do ok. 4 tyg. |
| D9 | Zaraza obozowa pod dluzsze oblezenia | [M] SiegeSicknessIncubationDays 9 -> 14-21, SiegeSicknessRampPercent 15 -> 5-8 (klucze w json) | 9 / 15 | 21 / 6 | oboz w 4. tygodniu traci kilka % zamiast ~1/3 | bez tego D1/D3 = prawie zero zdobyc |
| D10 | Okup czestszy zamiast szturmu | [M] SiegeRansomBloodyRatio 1.5 -> 2.0-3.0 (brak w json) | 1.5 | 2.0 | decyzje 1.5-2.0 (13 zdobyc w fin30) -> okup, gdy miasto ma monete; -13 zdobyc przy 2.0, -32 przy 3.0 (gorna granica) | pieniadze miast (kupcy, kiesy ludu) |
| D11 | Patrole BK | [O] BK PatrolParties true -> false | true | false albo zostawic | zaloga nie wychodzi w pole 30-50% | patrole lapia bandy |
| D12 | Milicja | [O] BK MilitiaGrowthMultiplier 0.5 -> 0.75-1.0 | 0.5 | 0.75 | wiecej obroncow | wiecej buntow (milicja >= 1.4 x zaloga) - tylko razem z D5 |
| D13 | Krolestwa z buntow | [N] CrashScribe: `BKClanBehavior._disableRebelKingdom = true` (pole statyczne, refleksja) | false | true | 11 z 14 nowych panstw nie powstaje jako krolestwa (klan buntownikow zostaje klanem z lennem) | bunt nadal odbiera miasto; zachowanie gry jak bez BK |
| D14 | Wojny domowe Diplomacy | [O] DiplomacySettings_v1.2.json: DailyChanceToStartRebelFaction 0.05 -> 0.0115, ...Join 0.1 -> 0.023, ...StartCivilWar 0.1 -> 0.023 | jw. | x84/364 | ok. 4.33 x mniej frakcji i wojen domowych na rok (3 w fin30) | zgodne z zasada Jeffa (jak zmeczenie wojna) |
| D15 | Prog buntu | [N] postfiks RebellionStartLoyaltyThreshold (ROT 10) albo warunek milicji 1.4 | 10 / 1.4 | bez zmian (najpierw D5, D13) | - | - |
| D16 | Strzemiona Essos | [N] postfiks po BK na SimulateHit: zdjac +20% Stirrups albo dac Westerosowi | +20% Essos | wyrownac | Essos vs Westeros rowno | lore (Dothrakowie konni); rozdz. 9 |
| D17 | Zmeczenie wojna od oblezen | [O] Diplomacy WarExhaustionPerSiege 2.31 / WhenOccupied 3.46 | /4.33 od 14.10 | bez zmian | - | wojny w latach jak w krotkim roku - zostawic; dlugosc wojny lepiej skracac dlugoscia oblezen (D1, D3) |

Kolejnosc wedlug dowodu [S]: (1) D5 + D6 (zalogi - glowna roznica sil, 8 x przy zdobyciu), (2) D3 + D1 z D8 i D9 razem (czas), (3) D4 albo D10
(trudnosc szturmu), (4) D13 + D14 (bunty). D1/D3 bez D9 (zaraza od 9. doby) i bez D8 (oblezenia 'gleboko') grozi prawie zerem zdobyc.

## 9. Nie wiem / do sprawdzenia

- Czynny model armii: z kolejnosci ladowania wynika SCA `StrategicArmyManagementModel` (ostatni dodany), ale linii w logu z `Campaign.Current.Models.ArmyManagementCalculationModel.GetType()` brak - dopisac linie startowa.
- Co RBL naprawde czyta jako SiegeConstructionSpeedMultiplier: json 1.0, AUDYT-CZAS-MAPA cytuje log 17.09 i 04.10 "RB mnoznik budowy z MCM = 0.40"; linia jest tylko przy oblezeniu gracza (w fin30 0). Czasy decyzji (min 4.2, mediana 8.4 doby) pasuja raczej do 1.0 [S].
- SCA145 v0.1.9: logika rol armii (cel agresywny, obronca, przechwyt) jest w DLL, ale nikt jej nie wola (rozdz. 3) - starsze audyty (07-mapa-czas-obozy: 'SCA co 4 h wola SetMove*') mogly czytac inny DLL; warto potwierdzic jedna linia w logu (np. ile razy SCA zdjelo oblezenie - TryRetreatFromBadSiege).
- SCA TryReinforceFragileGarrison dopisuje 18 ludzi do `settlement.Party.MemberRoster` - czy ci ludzie bronia murow, czy znikaja.
- 19 "szturmow bez zdobycia" w naszym liczniku wobec "0 odpartych" w zadaniu - inna miara (bitwy.log?) - uzgodnic z rozpoznaniem CZAS.
- Sklad obroncow przy decyzji (zaloga / milicja / lordowie) - linia O0 podaje tylko sume; rozpoznanie SILY.
- Czy Westeros dorobil Stirrups w 2 lata (Innovations 0.80) - brak linii.
- WorldPace: linia startowa pisze "mapa 50%", a Armoury.json ma WorldPacePercent 75 - linia pisze sie przed wczytaniem MCM? (poza tematem oblezen, do sprawdzenia przy D1).
- BKROTPatch pomija dzienne ticki BK (m.in. BKClanBehavior.DailyClanTick, polityka wladcy i wasali) dla czesci rodow Essos (EssosiDailyTickSkipPatch, EssosiCouncilSkipPatch - `D bkrot/BKROTPatch.Patches/`); czy to ma zwiazek z tym, ze wszystkie 14 buntow jest w Essos - nie sprawdzalem.
- Blokada portow NavalDLC (gra 1.4): czy oblezone miasto portowe dostaje jedzenie morzem - model jedzenia gry nie ma takiego skladnika [K], ale nie sprawdzalem zachowan blokady.

## 10. Pliki robocze (`docs\oblezenia\robocze\mody\`)

- `armoury-settings-oblezenia.tsv` - klucze Settings.cs (zbior-e8) zwiazane z oblezeniami, zalogami, milicja, okupem, lupem (wiersz, typ, nazwa, domyslna, opis).
- `fin30-linie-oblezen.txt` - zliczenia i probki linii fin30: E4/8 (K3, K8, A5, milicja), O0/O1/doba, N3, CampFever, L0-L2, L5b-2, WarLedger.
- `fin30-decyzje-szturmu-O0.csv` - 113 decyzji o szturmie (doba oblezenia, sily, przewaga murow, wykladnik, maszyny, stosunki) + 26 okupow.
- `fin30-decyzje-szturmu-O0-wynik.csv` - to samo z wynikiem w 10 dob (zdobyte / okup / inne).
- Skrypty (scratchpad c36b73ca): `scan_mody.py`, `scan_o0.py`, `scan_doba.py`, `join_o0.py` (python -I, strumieniowo).
