# AUDYT KALENDARZA - "1 dzien gry = 1 prawdziwy dzien" (rok ~365 dni)

Data: 2026-10-04. Audyt tylko do odczytu (kod niezmieniony). Zakres: kalendarz, wszystko co
liczone NA ROK / NA SEZON / NA DZIEN, os fabuly ROT, ekonomia (nasze systemy + Bank Zelazny).
Poza zakresem (inny audyt): predkosc na mapie, odleglosci, oblezenia, budowy, XP.

Zrodla: dekompilacja 1.4.8 (`tw` = TaleWorlds.CampaignSystem, `sb` SandBox, `bk` BannerKings,
`be` BetterEconomy, `rot` ROT.dll, `dip` Bannerlord.Diplomacy 1.4.7, `rbl` RealisticBannerlord,
`bad` BirthAndDeath, `fm` FastMode) + nasze `src`. Sciezki `plik:linia` odnosza sie do dekompilatu.
AIInfluence.dll nie dal sie zdekompilowac (ilspycmd bez wyniku) - opisany tylko z zapisanych ustawien MCM.

---

## 0. TL;DR

1. **365 dni sie nie da** - rok w Bannerlordzie to iloczyn `DaysInWeek x WeeksInSeason x SeasonsInYear`
   (liczby calkowite), a 365 = 5 x 73. **Rekomendacja: 364 = 7 x 13 x 4** (tydzien zostaje 7 dni,
   sezon 91 dni ~ prawdziwy kwartal). Jedyna zmiana kalendarza: `WeeksPerSeason` 6 -> 13.
   **UWAGA: w `Calendar.cs:33` i `:46` jest clamp `w > 12 -> 12`** - wpisanie 13 dalo by po cichu rok 336 dni.
2. Wszystko, co gra liczy w **latach** (wiek, smierc ze starosci, rozejmy BK, nauka jezykow BK,
   pora roku, pogoda, snieg, zima w RBL i WinterBite) **samo sie przeskaluje** - czyta `CampaignTime.DaysInYear`
   albo `TimeTicksPerYear`, liczone raz przy `CampaignTime.Initialize()` (`CampaignTime.cs:178-197`).
3. Wszystko, co gra liczy **na dzien** (jedzenie, zold, dochody, leczenie, produkcja, ochotnicy, dobrobyt,
   wplywy) **zostaje na dzien** - i to jest POPRAWNE przy "1 dzien = 1 dzien".
4. Psuje sie to, co jest **dzienna SZANSA, ktora w zamysle oznacza "ile razy na rok"** albo **stala w dniach,
   ktora w zamysle oznacza "rok/sezon"**: ciaza (36 dni i dzienna szansa), sojusze (84 dni), turnieje
   (tydzien sezonu), BetterEconomy (sezon zaszyty na 21 dni!), os fabuly ROT (dni od startu), wojny
   fabularne ROT (`StartDay/EndDay` - dzis NIESKALOWANE), ustawienia Diplomacy, popyt BK (dzielony przez DaysInYear).
5. Znalezisko uboczne: **nasz `LongYearTimeModel` dziedziczy po vanilli, nie po ROT**, wiec start kampanii
   to vanillowy rok **1084** (log CrashScribe: "Autumn 18, 1091"), a nie ROT-owe **299 AC**
   (`ROTCampaignTimeModel.CampaignStartTime = Years(299) + Hours(6)`).
6. **Tylko nowa gra.** Zmiana dlugosci roku w trwajacej kampanii odmladza wszystkich ~2.2x (wiek = ticki / ticki na rok).

---

## 1. Jak kalendarz jest zdefiniowany i zmieniany

### 1.1 Mechanika gry

| Element | Gdzie | Co robi |
|---|---|---|
| `CampaignTimeModel` (abstrakcja) | `tw/.../ComponentInterfaces/CampaignTimeModel.cs:53-76` | `CampaignStartTime, SunRise, SunSet, HoursInDay, DaysInWeek, WeeksInSeason, SeasonsInYear` |
| `DefaultCampaignTimeModel` | `tw/.../GameComponents/DefaultCampaignTimeModel.cs:7-47` | 7 dni x 3 tyg. x 4 sezony = **84**; w `GameAccelerationMode.Fast`: 3 x 2 x 4 = **24**. Start: `Years(1084) + Weeks(WeeksInSeason) + Hours(9)` (czyli 1. dzien LATA) |
| `ROTCampaignTimeModel` | `rot/ROT.Models/ROTCampaignTimeModel.cs` | 7 x 3 x 4 = 84 na sztywno (bez trybu Fast); **start `Years(299) + Hours(6)`** = 1. dzien wiosny 299 AC. Rejestrowany w `rot/ROT/SubModule.cs:337` |
| `LongYearTimeModel` (nasz) | `Armoury/src/Calendar.cs:21-64`, rejestracja `SubModuleMain.cs:139` | dziedziczy po **Default**, nadpisuje tylko `WeeksInSeason` (z MCM, clamp 1..12). Wygrywa z ROT (laduje sie pozniej) - log: "Kalendarz: rok ma 168 dni" |
| `CampaignTime.Initialize()` | `tw/.../CampaignTime.cs:178-197`, wolane w `Campaign.cs:1394` | **RAZ** przy starcie/wczytaniu kopiuje wartosci modelu do pol statycznych (`DaysInWeek`, `WeeksInSeason`, `TimeTicksPerWeek/Season/Year`). Potem `DaysInSeason = WeeksInSeason*DaysInWeek`, `DaysInYear = DaysInSeason*SeasonsInYear` (`:58-60`) |
| Czas w zapisie | `MapTimeTracker` (ticki, `Campaign.cs:1402`) | Zapis trzyma **ticki**, nie daty. Data/wiek/pora roku to dzielenie tickow przez `TimeTicksPerYear` przy kazdym odczycie |
| Tick tygodniowy | `Campaign.cs:787` | odpala, gdy `(dni od startu) % DaysInWeek == 0` - dlatego **nie ruszac `DaysInWeek`** |
| Opcja BirthAndDeath | `bad/.../BirthAndDeathOptionsProvider.cs` | to oficjalny modul TaleWorlds (nie mod!) - tylko przelacznik `CampaignOptions.IsLifeDeathCycleDisabled`. Wlaczony w LauncherData. Zadnych wlasnych stalych czasu |
| FastMode | `fm/.../FastModeSubModule.cs` | ustawia `AccelerationMode = Fast` (rok 24 dni). **Wylaczony** w LauncherData. Nasz model dziedziczy `DaysInWeek` z Default -> gdyby ktos wlaczyl FastMode, wyszloby 3 x 13 x 4 = 156 |

### 1.2 Stale "czasu kompilacji"?

W `CampaignTime` **nie ma** `const` od dni/tygodni/lat - sa `static int` ustawiane w `Initialize()`. Dlatego
podmiana modelu dziala. Prawdziwe stale zaszyte w cudzym kodzie (nie przeskaluja sie same):

| Stala | Gdzie | Skutek przy 364 |
|---|---|---|
| `SeasonClock.DaysPerSeason = 21`, `DaysPerYear = 84` (`const`) | `be/BetterEconomy.Core/SeasonClock.cs` | BE ma WLASNY zegar: `(int)ToDays / 21 % 4`. Pory BE kreca sie 4.33x na rok gry, niezaleznie od sniegu i zimy gry. **Juz dzis rozjechane** (rok 168) |
| `Days(84f)`, `Days(42f)` | `tw/.../DefaultAllianceModel.cs:111,113` | sojusz max 84 dni = byl "rok", bedzie kwartal |
| `ElapsedDaysUntilNow < 150f` | `tw/.../DefaultDiplomacyModel.cs:1027` | AI mniej chetne do pokoju przez 150 dni wojny - zostaje 150 dni |
| `tributeDurationInDays = 100` | `tw/.../DefaultDiplomacyModel.cs:1060` | trybut 100 dni - zostaje |
| `DaysInYear * 5` (trybut) | `dip/Diplomacy.DiplomaticAction.WarPeace/KingdomPeaceAction.cs:186` | trybut z Diplomacy = 5 lat -> **1820 dni** zamiast 420 (4.3x wiecej zlota razem) |
| `KeepInHistoryTime` w dniach (1680, 84, 40, 7) | `tw/.../LogEntries/*.cs` | pamiec obraz (1680 dni = "20 lat" vanilli) skurczy sie do ~4.6 roku |
| dni od startu w wydarzeniach ROT | `rot/ROT.Events/*.cs` (rozdz. 3) | os fabuly w dniach, nie latach |
| `StartDay/EndDay` wojen fabularnych | `rot/ROT.CampaignBehaviors/ROTStorylineWars.cs:79-105` | jw. - i dzis NIE przechodza przez nasz `Fabula` |

### 1.3 Co juz wiemy z naszych dokumentow (efekty uboczne roku 168)

| Zrodlo | Efekt |
|---|---|
| `CHANGELOG 2026-08-31 Tempo swiata 50%` | marsz i oblezenia zwolnione, "zeby pasowaly do podwojonego roku" (WorldPace/SiegePace - inny audyt) |
| `CHANGELOG 2026-09-15 Rations` | dlugi rok + wolny marsz = te same trasy trwaja wiecej dni -> jedzenie -40% |
| `SlowMuster.cs:10` | "dlugi rok podwoil liczbe dziennych tickow na rok" -> ochotnicy 25% |
| `WinterBite.cs:14` | zima = 42 dni; przy 364 bedzie **91 dni** z +50% jedzenia i -50% produkcji wiosek |
| `HISTORY.md` / `Fabula.cs` | `FabulaTimeScale = 2.0` = 168/84 |
| `docs/AUDYT-CEN.md:30` | przeliczenia "na rok" robione na 168 dni |
| `docs/POPULACJA-WESTEROS-ESSOS.md:183` | przeliczenie populacji na 168 dni |
| log CrashScribe 04.10 | "Autumn 18, 1091", "FABULA: dzien 1236 \| czolo: RamsayEvent (na dzien 600)" - czyli rok liczony od 1084 (vanilla), nie 299 AC |

### 1.4 Co sie stanie przy zmianie dlugosci roku W TRWAJACEJ kampanii

Urodziny bohaterow to ticki ustawione przy starcie (`YearsFromNow(-wiek)` przy STAREJ dlugosci roku,
`Hero.cs:450-484`, `CharacterData.cs:192`). Po zmianie 168 -> 364 kazdy wiek dzieli sie przez 2.17:
40-latek ma 18 lat, dzieci staja sie niemowletami, rok kalendarza cofa sie (1091 -> ~1087),
pory roku przeskakuja. Ciaze, kontrakty BK, rozejmy w tickach zostaja w dniach (ok), ale wszystko w latach
sie rozjedzie. **Wniosek: zmiana kalendarza wylacznie przed nowa gra** (Jeff planuje nowa - dobrze).

---

## 2. Co jest NA ROK / NA SEZON (rozciagnie sie), a co NA DZIEN (zostaje)

Legenda kolumny "Przy 364": **AUTO** = przeskaluje sie samo (czyta dlugosc roku); **DZIEN** = zostaje
na dzien, to poprawne; **ZLE** = trzeba poprawic; **DECYZJA** = do ustalenia z Jeffem.
Czynnik `k = DaysInYear / 84` (vanilla) = **4.33** przy 364 (dzis 2.0).

### 2.1 Zycie bohaterow

| System | Jednostka | Gdzie | Przy 364 |
|---|---|---|---|
| Wiek bohatera | lata (ticki / TicksPerYear) | `tw/TaleWorlds.CampaignSystem/Hero.cs:482-484` | **AUTO** - 1 rok wieku = 364 dni |
| Progi wieku (3/6/14/18/35/55; ROT: stary = 67) | lata | `DefaultAgeModel.cs` (80-92), `rot/ROT.Models/ROTAgeModel.cs` | **AUTO**. Konsekwencja: dziecko urodzone w kampanii dorasta 18 x 364 = **6552 dni** - praktycznie nigdy w zasiegu gry. DECYZJA, czy to OK (lore: tak) |
| Dorastanie (teen, pelnoletnosc) | dzienny tick sprawdza wiek | `AgingCampaignBehavior.cs:91-130` | **AUTO** |
| Edukacja dzieci vanilla (etapy wieku) | lata | `EducationCampaignBehavior.cs:612` | **AUTO** |
| Smierc ze starosci | roczna szansa -> dzienna przez `DaysInYear` | `DefaultHeroDeathProbabilityCalculationModel.cs:25` (`1 - (1-p)^(1/DaysInYear)`) | **AUTO** (smiertelnosc NA ROK bez zmian) |
| Losowa data urodzin/smierci | `DaysInYear` | `Helpers/HeroHelper.cs:491-501` | **AUTO** |
| **Ciaza - czas trwania** | **36 dni** (Fast 18) | `DefaultPregnancyModel.cs:13`, uzycie `PregnancyCampaignBehavior.cs:194` | **ZLE** - 36 dni = 5 miesiecy vanilli, przy 364 to ~5 tygodni. Realnie **~270 dni** (9 mies.); proporcjonalnie do vanilli 36 x 4.33 = **156** |
| **Ciaza - dzienna szansa** | dzien | `DefaultPregnancyModel.cs:32-46`; BK x1.15 za doktryne `bk/BannerKings.Patches/VanillaModelTweakPatches.cs:1144-1166`; wolane z `PregnancyCampaignBehavior.cs:120` i `bk/BannerKings.Behaviours.Marriage/BKMarriageBehavior.cs:286` | **ZLE** - formula `(1.2-(wiek-18)*0.04)/(dzieci+1)^2*0.12` daje 18-latce 14%/dzien; przy 364 dniach urodzen na ROK bylo by 4.3x wiecej niz w vanilli. Mnozyc przez `1/k` (0.23) |
| Smiertelnosc przy porodzie, martwe urodzenia, blizniaki | na porod | `DefaultPregnancyModel.cs:15-21` | DZIEN (na zdarzenie) - bez zmian |
| Malzenstwa AI | dzienna szansa (`MarriageModel.NpcCoupleMarriageChance`, model ROT/BK) | `RomanceCampaignBehavior.cs:100-145` | **DECYZJA** - 4.3x wiecej prob na rok; ograniczone liczba wolnych, wiec pewnie nieszkodliwe. Obserwowac |
| Oferty malzenstwa dla gracza | co >= 7 dni | `MarriageOfferCampaignBehavior.cs:308` | DZIEN |
| AIInfluence: romans, poczecie | `RomanceDecayDays 7`, `IntimacyConceptionChance 0.15` (na zblizenie) | MCM `AIInfluenceSettings.json` | DZIEN (zdarzeniowe). Nie dekompilowane - sprawdzic, czy ma wlasny czas ciazy |
| Wedrowcy/towarzysze (spawn, zgon) | dni (np. 40 dni po smierci) | `CompanionsCampaignBehavior.cs:156`, `HeroSpawnCampaignBehavior.cs:120-144` | DZIEN |
| Notable: zastapienie zmarlego | 7 dni | `NotablesCampaignBehavior.cs:63` | DZIEN |

### 2.2 Rod, krolestwo, polityka

| System | Jednostka | Gdzie | Przy 364 |
|---|---|---|---|
| Renoma / tier rodu | zdarzenia (bitwy, turnieje); vanilla nie ma zaniku renomy | `ClanTierModel` | DZIEN |
| Wplywy | dzien | `DefaultClanPoliticsModel.cs:32`; BK `InfluenceModel`; Diplomacy `InfluenceDecayPercentage 2` (dziennie) | DZIEN |
| Dochody rodu, zold | dzien | `DefaultClanFinanceModel.cs:108-212`, `DefaultPartyWageModel.cs:43`; ROT/BK/BE nadpisuja | DZIEN |
| Propozycje decyzji krolestwa (wojna, pokoj, polityki, aneksja, sojusz) | dzienna szansa na rod (max 0.33) | `KingdomDecisionProposalBehavior.cs:67-100` | **DECYZJA** - na rok 4.3x wiecej decyzji niz vanilla. Hamulce w dniach (ponizej) |
| Sojusz - max dlugosc | **84 dni** | `DefaultAllianceModel.cs:111` | **ZLE** -> 364 (byl "rok") |
| Udzial w wojnie sojusznika | **42 dni** | `DefaultAllianceModel.cs:113` | **ZLE** -> 182 (byl "pol roku") |
| Rozejm z barteru pokoju | `Years(1)` | `.../BarterBehaviors/DiplomaticBartersBehavior.cs:225`, `LordConversationsCampaignBehavior.cs:2707` | **AUTO** (364 dni) |
| Umowa handlowa | `Years(1)` | `DefaultTradeAgreementModel.cs:323` | **AUTO** |
| Pamiec nieudanej perswazji do zdrady | `ElapsedYears > 1` | `LordDefectionCampaignBehavior.cs:968` | **AUTO** |
| Niechec do pokoju na poczatku wojny | 150 dni | `DefaultDiplomacyModel.cs:1027` | DZIEN (pasuje do realnych dni) |
| Trybut vanilla | 100 dni | `DefaultDiplomacyModel.cs:1060` | DZIEN |
| **Diplomacy (mod)**: `MinimumWarDurationInDays 21`, `DeclareWarCooldownInDays 21`, `MinimumAllianceDuration 42`, `NonAggressionPactDuration 84`, `MinimumTimeSinceLastCivilWarInDays 240`, `MaximumFactionDurationInDays 120`, `WarExhaustionPerDay 0.25`, `ExpansionismDecayPerDay 1`, `DailyChanceToStartCivilWar 0.1` | dni | `dip/Diplomacy/Settings.cs:64-319`, zapis `Configs/ModSettings/Global/Diplomacy/DiplomacySettings_v1.2.json` | **DECYZJA** (MCM, bez kodu): pakt 84 -> 364, sojusz 42 -> 182, wojna min. 21 -> 90, cooldown 21 -> 90, wojna domowa 240 -> 1040, frakcja 120 -> 520. `WarExhaustionPerDay` NIE wedle kalendarza - wedle tempa mapy (inny audyt) |
| Trybut z Diplomacy | `DaysInYear * 5` | `dip/.../KingdomPeaceAction.cs:186` | **AUTO**, ale suma 4.3x wieksza niz w vanilli - zostawic (5 lat to 5 lat) |
| BK rozejmy | `YearsFromNow(years)` | `bk/BannerKings.Behaviours.Diplomacy/KingdomDiplomacy.cs:482` | **AUTO** |
| BK casus belli, grupy interesu, rada, prawa, demesne | `ElapsedYearsUntilNow >= 1` | `bk/.../CasusBelliRegistry.cs:82`, `InterestGroup.cs:294`, `RadicalGroup.cs:186`, `CouncilMember.cs:208`, `DemesneLaw.cs:23`, `BKRulerPoliticsBehavior.cs:157`, `BKTitleBehavior.cs:211` | **AUTO** |
| BK obowiazki lenne (levy) | sezony | `bk/.../FeudalTitle.cs:232`, `LevyDuty.cs:118` | **AUTO** |
| BK zmeczenie wojna | lata x DaysInYear = dni | `bk/.../BKWarModel.cs:474-478` | **AUTO** |
| BK kontrakt najemnika | `YearsFromNow(1)`, mapowanie po `DaysInYear` | `bk/.../MercenaryCareer.cs:80-597` | **AUTO** |
| BK dekrety wioski | `Years(2)` | `bk/.../VillageDecreeManager.cs:14` | **AUTO** |
| BK republika - wybory | `GetDayOfYear == 1` | `bk/.../BKRepublicBehavior.cs:28` | **AUTO** (raz na rok) |
| BK lask dworu | `GetDayOfSeason == 1`, co 7 dni | `bk/.../CourtGrace.cs:116-221` | **AUTO** |
| BK uczty | `WeeksFromNow(1)`, raz na rok | `bk/.../BKFeastBehavior.cs:69`, `OrganizeFeastDecision.cs:84` | **AUTO** |
| BK swieta religijne | dzien sezonu | `bk/.../Festival.cs:64-67` | **AUTO** (dni <= 21 istnieja w sezonie 91 dni) |
| BK inwazje (Essos?) | dzienna szansa `10/DaysInYear` | `bk/.../InvasionBehavior.cs:54` | **AUTO** (~10/rok) |
| BK koszt tytulu / imperium | `500k + dochod dzienny x DaysInYear` | `bk/.../BKTitleModel.cs:137-218`, `FoundEmpireGoal.cs:111` | **AUTO** semantycznie ("roczny dochod"), ale liczbowo 4.3x wiecej niz vanilla - OK |
| BK koszt odrzucenia rycerstwa | `10 + x*0.025*DaysInYear` | `bk/.../BKInfluenceModel.cs:94` | **AUTO** |

### 2.3 Osady, ekonomia, wojsko

| System | Jednostka | Gdzie | Przy 364 |
|---|---|---|---|
| Zuzycie jedzenia | dzien (20 ludzi = 1 jedzenie) | `DefaultMobilePartyFoodConsumptionModel.cs:16-25`; BK/ROT/BE/RBL nadpisuja; nasze `Rations` -40% | DZIEN |
| Produkcja wiosek | dzien | `DefaultVillageProductionCalculatorModel.cs:17,76`; BK/BE nadpisuja; nasze `WinterBite` -50% zima | DZIEN (zima bedzie 91 dni - patrz WinterBite) |
| Dobrobyt / ogniska | dzien | `DefaultSettlementProsperityModel.cs:27,34`; BK (`ProsperityGrowthMultiplier 0.3`), BE, RBL (`RealisticProsperityModel.cs:59` - kara zimowa wg pory gry) | DZIEN |
| Lojalnosc, bezpieczenstwo | dzien | `DefaultSettlementLoyaltyModel.cs:92`, `DefaultSettlementSecurityModel.cs:83` | DZIEN |
| Ochotnicy | dzienna szansa | `DefaultVolunteerModel.cs:87`, BK `BKVolunteerModel`, nasze `SlowMuster` 25% | DZIEN |
| Leczenie | dzien (regularni), godzina (bohaterowie) | `DefaultPartyHealingModel.cs:122,232`; RBL `RealisticHealingModel.cs:103` (kara zimowa); nasze `SlowHealing` | DZIEN |
| Jency, okup, ucieczki (RealisticCaptivity) | dni | `RealisticCaptivity/src/Settings.cs:22-145` | DZIEN |
| Warsztaty vanilla | dzien | `WorkshopsCampaignBehavior` (RunTownWorkshop) | DZIEN |
| **BK popyt miasta** | populacja -> **roczny popyt / DaysInYear** | `bk/BannerKings.Models.Vanilla/BKEconomyModel.cs:628` | **ZLE** - dzienny popyt na towary spada 4.3x vs vanilla (dzis 2x), a produkcja wiosek jest dzienna -> nadwyzki, tanie towary. Sufit podazy `dailyDemand x DaysInYear` (`:634`) jest ok. Wymaga kompensaty x`k` albo swiadomej decyzji |
| BK gnicie zboza | `1/(2*DaysInYear)` dziennie | `bk/.../BKSettlementBehavior.cs:65,100` | **AUTO** (pol roku) |
| BK gnicie sera/masla | `1/DaysInWeek` | `:77-116` | DZIEN |
| BK wzrost populacji | dzien (`0.005*0.4` na klase + 5, hamowany pojemnoscia) | `bk/.../BKGrowthModel.cs:28-80` | DZIEN - logistyczny do sufitu, na rok szybszy, ale sufit to samo. Obserwowac |
| BK akceptacja kultury, konwersja | dzien | `bk/.../BKCultureModel.cs:23-264` | DZIEN |
| BK innowacje (badania) | dzien | `bk/.../BKInnovationsModel.cs:21-87` | DZIEN - innowacje na rok 4.3x szybciej niz vanilla. DECYZJA |
| BK nauka jezykow/ksiazek | `1/DaysInYear`, `1/(1.5*DaysInYear)`, `1/(3*DaysInYear)` | `bk/.../EducationData.cs:67-69`, `BKEducationModel.cs:58` | **AUTO** (jezyk w rok) |
| BK cena warsztatu dla gracza | `wydatek dzienny x 15 x DaysInYear` | `bk/.../BKWorkshopModel.cs:119,149` | **AUTO**, liczbowo 4.3x drozej niz vanilla. DECYZJA |
| BK wycena majatkow | `LastIncome x DaysInYear` | `bk/.../BKEstatesModel.cs:504` | **AUTO** |
| Wydarzenia losowe (Incidents) | cooldown 8/15 dni | `DefaultIncidentModel.cs:9,14` | DZIEN |
| **Turnieje - start** | **tydzien sezonu** (`hash % 3 == GetWeekOfSeason`) | `DefaultTournamentModel.cs:27` | **ZLE** - mozliwe tylko w tygodniach 0-2 sezonu: dzis 3 z 6, przy 364 **3 z 13** (77% czasu bez nowych turniejow) |
| Turnieje - koniec, odstep | 10-15 dni | `DefaultTournamentModel.cs` (`GetTournamentEndChance`), `TournamentCampaignBehavior.cs:71,129` | DZIEN |
| GrandTourney | `GatherDays 4`, `HostCooldownDays 365` | `GrandTourney/src/Settings.cs:14,32` | DZIEN (365 juz "prawdziwy rok") |
| BetterEconomy pory roku | **21 dni** na sztywno | `be/BetterEconomy.Core/SeasonClock.cs` -> `PopulationCampaignBehavior.cs:145`, `CaravanCampaignBehavior.cs:1378`, `BEE_ItemPriceFactorModel.cs:73`, `BEE_PartySpeedModel.cs:62`, `BEE_SettlementEconomyModel.cs:82` | **ZLE** - patrz 1.2 |
| BetterEconomy reszta | dni (zdarzenia 7-21 dni, szansa 0.14/dzien, `CultureVanillaConversionCooldownDays 365`) | `be/BetterEconomy.Config/BetterEconomySettings.cs:5-781` | DZIEN |
| AIInfluence | dni (`DynamicEventsInterval 7`, `DurationDaysMin/Max 7/90`, `DiseaseMinDaysBetweenOutbreaks 14`, choroby sezonowe z bonusem wg pory gry) | MCM `AIInfluenceSettings.json` | DZIEN; choroby sezonowe zima przez 91 dni |
| RBL zima (jedzenie, morale, predkosc, leczenie, infekcje) | pora roku gry | `rbl/.../Seasons/*.cs`, `Medicine/InfectionBehavior.cs:59-89` | **AUTO** (zima dluzsza w dniach) |
| StrategicCampaignAI (zima) | pora roku gry | `sai/StrategicCampaignAI145/StrategicAiHelpers.cs:255` | **AUTO** |
| Pogoda, snieg | `ToSeasons` | `DefaultMapWeatherModel.cs:322-602` | **AUTO** |
| NavalDLC | `WeeksFromNow(3)` (fabula) | `nv/.../NavalStorylineCampaignBehavior.cs:890` | DZIEN |

### 2.4 Nasze mody

| System | Gdzie | Przy 364 |
|---|---|---|
| `LongYearTimeModel` | `Armoury/src/Calendar.cs:21-64`; MCM `McmSettings.cs:899` (zakres 0-24), `Settings.cs:277-278` | podniesc clamp `12` -> min. 13 (`:33`, `:46`); domyslnie `WeeksPerSeason = 13` |
| `WinterBite` | `WinterBite.cs:44-102` | AUTO - zima 91 dni z +50% jedzenia i -50% wiosek = ~2.2x wiecej "zimowych dni" niz dzis. Rozwazyc 50% -> ~30% albo zostawic (lore Polnocy) |
| `SlowMuster` 25%, `SlowHealing` 50%, `Rations` -40%, `CampFever`, `WarLedger` (dezercje %/dzien), `ScorchedEarth` (ogniska/dzien), `TroopSelfMend` 10%/dzien | `Settings.cs:136-320` | DZIEN - zostaja (komentarze odwoluja sie do "dlugiego roku" - uzasadnienie sie zmieni, liczby nie musza) |
| `Fabula` (CrashScribe) | `CrashScribe/src/Fabula.cs:36-58,80-86`, `Config.cs:27-29` | `FabulaTimeScale` 2.0 -> **4.33** (clamp 0.25-6 pozwala) - rozdz. 3 |
| `NightKingCall` | `CrashScribe/src/NightKingCall.cs` | DZIEN (raz na dzien) |
| RealisticCaptivity, GrandTourney, ForgeView | - | brak uzyc roku (grep czysty) |

---

## 3. Fabula ROT - wydarzenia i ich dni

Bramka kazdego datowanego wydarzenia: `CampaignTime.Now < CampaignStartTime + CampaignTime.Days(N)` -> czekaj.
Liczone w DNIACH od startu kampanii (`Models.CampaignTimeModel.CampaignStartTime`, wiec niezalezne od roku 1084/299).
Wyjatek: `RooseDeathEvent` liczy `Years(4)` - skaluje sie sam. Nasz `Fabula.GatePrefix` nadklada wlasna os
(z poprawkami Karstark 100->190, Craster 150->200) x `FabulaTimeScale`; oryginalna bramka ROT jest zawsze
wczesniejsza, wiec przepuszcza.

### 3.1 Wydarzenia datowane (`rot/ROT.Events/*.cs`)

| Wydarzenie | ROT dzien (plik:linia) | Os Fabuli | dzis (x2, rok 168) | **364: x4.33** | lat (364) |
|---|---|---|---|---|---|
| NedEvent | 2 (`NedEvent.cs:48`) | 2 | 4 | **9** | 0.02 |
| HarrenhalSiegeNotificationEvent | 18 (`:51`) | 18 | 36 | **78** | 0.21 |
| RiverlandsDeclareWarEvent | 25 (`:48`) | 25 | 50 | **108** | 0.30 |
| HarrenhalSiegeEvent | 27 (`:88`, 240) | 27 | 54 | **117** | 0.32 |
| RenlyDeathEvent | 120 (`:50`) | 120 | 240 | **520** | 1.43 |
| BlackwaterSiegeNotificationEvent | 170 (`:51`) | 170 | 340 | **737** | 2.02 |
| BlackwaterSiegeEvent | 177 (`:94`; 180 w `:280`) | 177 | 354 | **767** | 2.11 |
| RickardKarstarkExecutionEvent | 100 (`:48`) | **190** | 380 | **823** | 2.26 |
| JeorMutinyEvent | 150 (`:49`) | **200** | 400 | **867** | 2.38 |
| RedWeddingEvent | 220 (`:244`) | 220 | 440 | **953** | 2.62 |
| JoffreyEvent | 250 (`:48`) | 250 | 500 | **1083** | 2.98 |
| WallSiegeNotificationEvent | 260 (`:51`) | 260 | 520 | **1127** | 3.10 |
| WallSiegeEvent | 274 (`:88`; 267 w `:236`) | 267 | 534 | **1157** | 3.18 |
| EastwatchEvent | 280 (`:48`) | 280 | 560 | **1213** | 3.33 |
| RamsayEvent | 300 (`:48`) | 300 | 600 | **1300** | 3.57 |
| JonEvent | 325 (`:48`) | 325 | 650 | **1408** | 3.87 |
| RooseDeathEvent | `Years(4)` (`:47`) | 336 | 672 | **1456** (= ROT sam) | 4.00 |
| BastardsSiegeNotificationEvent | 350 (`:51`) | 350 | 700 | **1517** | 4.17 |
| BastardsSiegeEvent | 357 (`:88`) | 357 | 714 | **1547** | 4.25 |
| AegonInvasionEvent | 400 (`:60`) | 400 | 800 | **1733** | 4.76 |
| DanyInvasionEvent | 450 (`:60`) | 450 | 900 | **1950** | 5.36 |

Wydarzenia bez daty (lancuchowe, odpalaja po swoim poprzedniku albo warunku swiata): `HarrenhalEvent`,
`BlackwaterEvent`, `WallEvent`, `BastardsEvent` (po swoim oblezeniu), `StannisDeathEvent` (Stannis martwy,
Renly zywy), `FistEvent` (Inni), `KingsLandingEvent` (KP/Czerwona Twierdza gracza), `NightKingDeathEvent`.
Skalowanie ich nie dotyczy.

Lata wedle serialu (Ned 298 AC, Renly ~299, Krwawe Gody ~299-300, Dany w Westeros ~305): os x4.33
**przy roku 364 trzyma proporcje serialu** tak samo jak vanilla przy 84. Koszt: Dany w dniu ~1950. Dzisiejsza
kampania doszla do dnia 1236 - wiec cala fabula to ~1.6 obecnej kampanii. Jesli za dlugo: `FabulaTimeScale`
2.0-3.0 (fabula szybsza niz serial), to jedna liczba w configu CrashScribe.

### 3.2 Inne zegary ROT, ktorych Fabula NIE skaluje

| Zegar | Gdzie | Dni ROT | Propozycja x4.33 |
|---|---|---|---|
| Wojny fabularne `StartDay/EndDay` (wojna 5 krolow, Zelazne Wyspy -> Polnoc, wojny Essos) | `rot/ROT.CampaignBehaviors/ROTStorylineWars.cs:79-105`, sprawdzane w `ROTStorylineWar.cs:76-90` (`Enforced`) | Westerlands-Riverlands **30**; Dragonstone/Stormlands/Reach vs Westerlands **135**; IronIslands-North **100-235**; Vale-IronIslands, Reach-Dorne, Braavos-Lorath, Tyrosh-Lys, Pentos-Myr, Norvos-Qohor, Volantis-Aegon, Qarth/Dothraki-Targaryen, Sarnor-Ibben **5-100**; North-Westerlands **350** | 30->130, 135->585, 100-235 -> 433-1018, 5-100 -> 22-433, 350->1517 |
| Inni: blokada atakow na Frozen Shore/Hornfoot, Thenn/Frostfangs, Hardhome (`OthersAIShackles`) | `ROTOthersCampaignBehavior.cs:1070-1086` | 300 / 400 / 500 | 1300 / 1733 / 2167 |
| Inni: rampa rajdow `min(0.01 x dni, 50)` | `ROTOthersCampaignBehavior.cs:1120` | sufit po 5000 dniach | podzielic dni przez k albo zostawic |
| Inni: zmiana celu co 4 dni | `ROTOthersCampaignBehavior.cs:989` | 4 | zostaje (dzien) |
| Nocna Straz nie atakuje za Murem pierwsze 1.5 roku | `rot/ROT.HarmonyPatches.Core/AIThinkPatch.cs:103` | `ElapsedYearsUntilNow < 1.5` | **AUTO** (546 dni) |
| Cooldown oblezen Innych | `ROTOthersCampaignBehavior.cs:431` | 5 dni | zostaje |
| Kruki, werbunek (ROTSettings: `RavenFlightTime 3`, `PartyRoleChangeCoolDown 5`, `EnlistWithEnemiesCooldown 7`), `OthersPartySizeLimitGrowth 3/dzien` | `rot/ROT/ROTSettings.cs:35-99` | dni | zostaja |

**Uwaga - juz dzis niespojne:** przy x2 wydarzenie "Riverlands wypowiada wojne" czeka do dnia 50, a wojna
fabularna Westerlands-Riverlands odpala w dniu 30; wojna Stannisa (135) przed smiercia Renly'ego (240) jest ok,
ale np. Zelazne Wyspy przestaja byc wymuszane w dniu 235, gdy reszta osi jest w dniu ~470.

Najprostsza latka: **prefix na `ROTStorylineWar.Enforced`** (bez stanu - nie trzeba nic zapisywac), ktory
porownuje dni od startu z `StartDay*k` / `EndDay*k` i zwraca to samo, co oryginal. Lista wojen jest tworzona
raz w `OnNewGameCreated` (`ROTStorylineWars.cs:55`) i zapisywana - dlatego nie mnozyc `StartDay` w miejscu
(przy kazdym wczytaniu urosloby znowu).

---

## 4. Ekonomia: dzien kontra rok (nasze systemy i Bank)

| System | Jednostki | Czy potrzebuje "dni w roku"? |
|---|---|---|
| `MaterialLaw.cs` (wydobycie, przetopy) | sztuki na dzien (`MineOutputMultiplier`, `LumberOutputMultiplier`) | NIE - dzienny strumien |
| `ArmsPricing.cs` | `SmithDayWage` /dzien, tablice dni pracy (`:59-66`), `MaterialIndexInertia` /dzien, `WarExpectationDays 15` | NIE |
| `SupplyDemand.cs` | % nadwyzki przewozony dziennie (`SupplyDemandTradePercent 15`) | NIE - ale zalezy od popytu BK, ktory JEST dzielony przez `DaysInYear` (2.3) |
| `WorkshopLaw.cs` | roboczodni/dzien, place/dzien, pula do 60 dni | NIE |
| `AiGear.cs` | raz dziennie | NIE |
| `MarketGlut` | sztuki/dzien | NIE |
| **`IronBank.cs` (w toku - plik jest, w `SubModuleMain` jeszcze nie podpiety)** | oprocentowanie ROCZNE (`IronBankRate*` 20/30/45%), odsetki dzienne = `dlug x stopa / DaysPerYear()` (`:210`), termin `max(7, DaysPerYear()/2)` (`:159`), limit = dochod z 60 dni | TAK - i juz to robi dobrze: `DaysPerYear()` czyta ZYWY model (`:61-65`). Przy 364 termin = 182 dni (pol roku), odsetki dzienne 4.3x mniejsze niz przy 84. **Fallback `catch { return 84; }`** - lepiej `CampaignTime.DaysInYear` |
| `docs/AUDYT-CEN.md`, `POPULACJA-WESTEROS-ESSOS.md` | przeliczenia "na rok" na 168 dni | do przeliczenia na 364 (dokumenty, nie kod) |

**Propozycja:** jedna wspolna funkcja w Armoury, np. `Calendar.DaysPerYearLive => CampaignTime.DaysInYear`
(pole statyczne gry, prawdziwe po `Initialize`) i `Calendar.YearScale => DaysInYear / 84f`. Uzywac jej w
Banku, w latce ciazy, w kompensacie popytu BK, w latce BetterEconomy i wystawic CrashScribe (albo CrashScribe
liczy sam z `CampaignTime.DaysInYear` - nie ma zaleznosci miedzy DLL-ami). Obecne `LongYearTimeModel.DaysPerYear(Settings)`
czyta MCM, nie gre - przy wczytaniu starego zapisu z inna wartoscia klamalby.

---

## 5. Plan

### 5.1 Decyzja: 364, nie 360 i nie 365

| Wariant | Struktura | Za | Przeciw |
|---|---|---|---|
| **364 (rekomendacja)** | 7 x **13** x 4, sezon 91 | tydzien 7 dni - tick tygodniowy (`Campaign.cs:787`), BK ser/maslo, `NotablesCampaignBehavior.cs:206`, BK `WeeksFromNow(1)` bez zmian; jedna liczba w MCM; sezon = prawdziwy kwartal | rok o 1 dzien krotszy od prawdziwego (bez znaczenia) |
| 360 | 6 x 15 x 4 albo 10 x 9 x 4 | "12 x 30" | zmienia `DaysInWeek` -> zmienia rytm WSZYSTKICH tygodniowych tickow i turniejow; trzeba nadpisac `DaysInWeek` w modelu |
| 365 | niemozliwe | - | 365 = 5 x 73 |

Turnieje i tak sa wiazane z tygodniem sezonu (2.3) - przy 13 tygodniach trzeba to latac w obu wariantach.

### 5.2 Kolejnosc (jedna zmiana na raz, kazda z wpisem w CHANGELOG i testem)

| # | Zmiana | Gdzie | Liczby | Ryzyko / co sprawdzic |
|---|---|---|---|---|
| 1 | Kalendarz 364 | `Calendar.cs:33,46` (clamp 12 -> 24), `Settings.cs:278` + `McmSettings.cs:899` (domyslnie 13, opis "13 = a 364-day year with 91-day seasons"), `gen_mcm.py` | `WeeksPerSeason = 13` | log "Kalendarz: rok ma 364 dni"; data w grze; wiek lordow ROT zgodny z XML. **Tylko nowa gra.** Zapisany `Armoury.json` ma 6 - trzeba zmienic w MCM przed startem |
| 1b (opcja) | Start 299 AC zamiast 1084 | `LongYearTimeModel.CampaignStartTime => Years(299) + Hours(6)` (jak ROT) | - | sprawdzic `BackstoryCampaignBehavior` (wpisy w `Years(1075-1080)` bylyby "w przyszlosci" - prawdopodobnie i tak nie trafia w id ROT), ekran daty, wiek bohaterow. Tylko nowa gra |
| 2 | Os fabuly | `CrashScribe/src/Config.cs:29` | `FabulaTimeScale` 2.0 -> **4.33** (albo auto `DaysInYear/84`); `FabulaPaceDays` 4 -> ~7-10 | linia "FABULA: dzien N \| czolo kolejki: NedEvent (na dzien 9)" |
| 3 | Wojny fabularne ROT | nowy prefix w CrashScribe na `ROT.CampaignBehaviors.ROTStorylineWar.Enforced` | `StartDay/EndDay x 4.33` | w Encyklopedii/menu wojen ROT "Start day not yet reached" dla wojny 5 krolow do dnia ~130 |
| 4 | Ciaza | Armoury: postfix na `DefaultPregnancyModel.get_PregnancyDurationInDays` i `GetDailyChanceOfPregnancyForHero` (po BK, `Priority.Last`) | czas 36 -> **270** (albo 156); szansa x **0.23** (= 84/364) | liczyc w logu ciaze/porody na rok; BK wola ten sam model (`BKMarriageBehavior.cs:286`) - latka obejmie oba |
| 5 | BetterEconomy pory roku | Armoury: prefix na `BetterEconomy.Core.SeasonClock.get_Current` -> `(Season)(int)CampaignTime.Now.GetSeasonOfYear` i `get_DayInSeason` -> `GetDayOfSeason` | enum zgodny (Spring, Summer, Autumn, Winter w obu) | log BE "daily [Winter dN/21]" pokaze zle `/21` (stala w stringu) - kosmetyka. Naprawia to tez dzisiejszy rok 168 |
| 6 | Turnieje | postfix na `DefaultTournamentModel.GetTournamentStartChance`: warunek tygodnia liczony z `(int)CampaignTime.Now.ToWeeks % 3` zamiast `GetWeekOfSeason` | - | czy turnieje pojawiaja sie przez caly sezon; BK tylko zamienia `CreateTournament`, TournamentsXPanded tej metody nie lata (sprawdzone w DLL) |
| 7 | Sojusze vanilla | postfix na `DefaultAllianceModel.MaxDurationOfAlliance` / `MaxDurationOfWarParticipation` | 84 -> 364, 42 -> 182 | czy sojusze nie zamrazaja mapy |
| 8 | Diplomacy (MCM, zero kodu) | `DiplomacySettings_v1.2.json` | pakt 84->364, sojusz min 42->182, wojna min 21->90, cooldown 21->90, wojna domowa 240->1040, frakcja 120->520 | KRONIKA WOJEN: liczba rownoczesnych wojen (dzis 13) |
| 9 | Popyt BK | postfix na `BKEconomyModel.GetDailyDemandForCategory` x `YearScale` (albo x `DaysInYear/84`) | x4.33 | DECYZJA po logu `Rynek surowcow` i cenach zywnosci - dzis (x2) tez jest zanizony; to zmienia ceny calego swiata, robic osobno i ostroznie |
| 10 | Bank | `IronBank.cs:61-65` | `DaysPerYear()` -> `CampaignTime.DaysInYear` w catch | brak |
| 11 | Przeglad "na wyczucie" | `WinterBite` 50/50% przy 91-dniowej zimie, BK innowacje/wzrost populacji, dzienna szansa decyzji krolestwa | DECYZJA Jeffa po pierwszym roku gry | obserwacja |

Nie ruszac: zoldu, jedzenia, leczenia, produkcji, ochotnikow, dobrobytu, wplywow (dzienne = poprawne),
`DaysInWeek`, niczego co juz czyta `DaysInYear`/`Years()`.

### 5.3 Ryzyka

| Ryzyko | Opis |
|---|---|
| Zgodnosc zapisow | Kalendarz czytany z MCM (globalnie), nie z zapisu. Po przestawieniu na 13 KAZDY stary zapis (rok 168) wczyta sie z wiekiem / 2.17 i przesunieta data. Rozwazyc zapis `WeeksPerSeason` w `SyncData` kampanii i uzywanie go przy wczytaniu - ale `CampaignTime.Initialize()` (`Campaign.cs:1394`) moze isc PRZED `SyncData` zachowan; do sprawdzenia w dekompilacji, zanim ktos to zrobi |
| Dynastie stoja | Dziecko dorasta 6552 dni; lordowie praktycznie sie nie starzeja w czasie, ktory Jeff zagra. Swiadoma cena "1 dzien = 1 dzien" |
| Clamp 12 | `Calendar.cs:33,46` - bez zmiany rok wyjdzie 336 dni, po cichu |
| FastMode | gdyby kiedys wlaczony: nasz model bierze `DaysInWeek` z vanilli (3) |
| Os fabuly | x4.33 = Dany w dniu ~1950. Jesli za wolno - mniejszy `FabulaTimeScale`, ale wtedy wojny fabularne (pkt 3) skalowac tym samym `k` |
| BetterEconomy/BK ceny | punkty 5 i 9 przestawiaja popyt i pory BE naraz w calym swiecie - osobne DLL-e, osobne testy, porownanie logow `Rynek surowcow` przed/po |
| AIInfluence | niezdekompilowane - nie wiadomo, czy ma wlasny zegar ciazy/sezonu; ustawienia sa w dniach i porach gry |

---

## 6. Niezalezne od kalendarza znaleziska

- Start kampanii to vanillowe **1084**, nie ROT **299 AC** - bo `LongYearTimeModel : DefaultCampaignTimeModel`
  zastepuje `ROTCampaignTimeModel` w calosci (`rot/ROT/SubModule.cs:337` vs `Armoury/src/SubModuleMain.cs:139`).
- BetterEconomy liczy pory roku na sztywno co 21 dni od tiku 0 (`SeasonClock.cs`) - **juz dzis** (rok 168)
  zima BE nie pokrywa sie z zima gry (snieg, RBL, WinterBite).
- Turnieje vanilla przy roku 168 startuja tylko w polowie tygodni sezonu (`DefaultTournamentModel.cs:27`).
- Popyt BK jest dzis o polowe nizszy niz w vanilli (`BKEconomyModel.cs:628`, dzielenie przez `DaysInYear` = 168).
- Wojny fabularne ROT (`StartDay/EndDay`) nie przechodza przez `Fabula` - dzis odpalaja wg dni vanilli przy osi x2.
