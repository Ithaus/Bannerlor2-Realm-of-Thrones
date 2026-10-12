# ETAP 7 - ROZPOZNANIE B: KOD 7.1, 7.4, 7.5, 7.6, 7.7 (12.10)

Tylko odczyt. Kod = najnowszy gotowy `SCRATCH\e5\wsie` (Armoury, CrashScribe, GrandTourney; = WGRANIE 33 + e5-wsie).
Logi = autotest 120 dob WGRANIA 33 (`Armoury-2026-10-12_02-44-09.log`, `Logs\2026-10-12_02-44-09\noc.log`, CrashScribe
`session-2026-10-12_02-44-08.log`). Dekompilacja: gra `SCRATCH\dec32\cs_full`, BK `SCRATCH\e4tools\bkfull`, ROT/NavalDLC
`SCRATCH\a6dec`, RBL i Diplomacy `SCRATCH\audyt-zloto`. Badanie drog 08.10: `B = C:\Users\GAME\AppData\Local\Temp\claude\
C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-...\scratchpad\dzien-6` (`B\wioski-v4\DROGI.md`).

Oznaczenia: [K] kod plik:linia, [P] pomiar z logu, [S] szacunek / hipoteza. Kalendarz = rok 364 dni (7 x 13 x 4).
Decyzje, ktore obowiazuja: 7.2/7.3 ODRZUCONE (Jeff 12.10 10:35 "zostaw predkosc poruszania sie jak jest") - tempo swiata
(WorldPace 75% u Jeffa, `Armoury.json:118`) zostaje.

---

## 0. W skrocie

| # | Co jest dzis | Co zmienic | Wielkosc | Koszt na dobe [S] |
|---|---|---|---|---|
| 7.1 | "Blokady tempa" (podloga 1.0, plaskie kary terenu, `Estimated*Speed`) byly warunkiem zwolnienia swiata - po odrzuceniu 7.2 dwie z trzech nic nie daja, trzecia zmienia AI i ekonomie wozow | Zostawic blokady (bez zmiany tempa nie sa potrzebne); "jedna skala czasu" = logika gry w czasie GRY, nie w sekundach realnych / klatkach (CrossingLaw, HideoutPurge), komentarze roku 168 -> 364 | S | 0 (mniej pracy przy x8) |
| 7.4 | `TerrainEase` zawsze oddaje pelny procent vanilli: Polnoc (+7 kultur z cecha lasu) i Dorne (pustynia) maja dzis PREMIE w lesie / na pustyni zamiast kary | Zwracac dokladnie to, co vanilla nalozyla (te same warunki: cecha kultury, ForestKin), plaska kare skalowac tym samym ulamkiem | S-M | < 0.01 s |
| 7.5 | Zadna droga nie przyspiesza (gra nie zna "drogi": `TerrainType` bez Road, predkosc cache'owana per sciana siatki 8-10 jedn.) | Widok drog (proba 1.13 nie zrobiona) + premia +15% wymaga wlasnej maski drog i latki na cache predkosci - **pytanie do Jeffa** | D | 0.04-0.1 s (premia), widok - koszt klatki |
| 7.6 | Spiacy: 91 obudzen na noc, zjazd sr. 0.40 jedn. (maks. 1.0), ok. 20 lordow/noc w ruchu bez powodu; 2/3 obudzen to "rozkaz jak nasz" rownomiernie w kazdej godzinie | Blokada `SetMove*` u spiacych (O-3) + odnawianie wylaczenia AI w przegladzie co 0.1 h (nie `DisableForHours(2)` - kolizja z `ForeignHold`) i `EnableAi` o swicie; "AI placi za noc" JUZ JEST (T10, WGRANIE 19), "zbiorka" JUZ JEST w sciezce T10 | S | < 0.01 s |
| 7.7 | Turnieje gry startuja tylko w tygodniach 0-2 z 13 (pomiar: zero zdarzen turniejowych w tygodniach 5-12); sojusz 84 dni / udzial w wojnie 42; Diplomacy 21/21/42/84/240/120 | Turnieje: tydzien liczony `ToWeeks % 3`; sojusze 364/182 (postfiks modelu); Diplomacy - wpisy w JSON Jeffa (instrukcja Z.9) | S | ~0 |

---

## 1. Lancuch predkosci dzis (wspolny dla 7.1, 7.4, 7.5)

### 1.1 Modele i nasze latki [K][P]

- Log startu: `SpeedDepth: ... predkosc koncowa 6, bazowa 4 ... modeli`, `MarchPace: ... 6 modeli`, `Kary terenu po naszemu ... (6 modeli)`
  (`Armoury-2026-10-12_02-44-09.log:59, 131-132`). DLL z `CalculateFinalSpeed`: vanilla, ROT, RealisticBannerlord, NavalDLC, BannerKings,
  BKROTPatch (+ AIInfluence, BetterEconomy maja napis - grep DLL).
- Kolejnosc wywolan: `ROTPartySpeedModel.CalculateFinalSpeed` = sam `_previousModel` (`a6dec\rot\ROT.Models\ROTPartySpeedModel.cs`);
  `NavalDLCPartySpeedCalculationModel` = `BaseModel.CalculateFinalSpeed` + wiatr / rzeka na morzu (`...NavalDLCPartySpeedCalculationModel.cs:181-265`);
  `RealisticPartySpeedModel : DefaultPartySpeedCalculatingModel` = `base` + pory roku ("Spring Roads" +, jesien/zima -) + pogoda RB,
  `LimitMin(0.2)` (`audyt-zloto\RealisticBannerlord\...\Seasons\RealisticPartySpeedModel.cs`); BK postfiks na Default (`BKPartySpeedTweakPatches`,
  `bkfull\BannerKings.Patches\VanillaModelTweakPatches.cs:929-990`, perki edukacji).
- Vanilla `DefaultPartySpeedCalculatingModel.CalculateFinalSpeed` (`dec32\...\GameComponents\DefaultPartySpeedCalculatingModel.cs:256-359`):
  las `:264-283` (ForestKin `:277`, cecha `BattanianForestSpeedFeat` `:281`), brod/most/rzeka `:287`, pustynia z cecha `AseraiDesertFeat` `:295`,
  snieg `:317`, noc `:323`, podloga `LimitMin(MinimumSpeed = 1.0)` `:357` (i w bazie `:221`).
- Nasze (Armoury): `SpeedDepth` prefiks Priority.First + finalizer na kazdym modelu (`SpeedDepth.cs:35-61`) - nasze postfiksy dzialaja tylko
  na najbardziej zewnetrznym poziomie (`OutermostFinal`, `_final <= 1`, `:26`); `WorldPace.BasePostfix` na `CalculateBaseSpeed`
  (`WorldPace.cs:48-59`, `AddFactor(p/100 - 1)`); `TerrainEase.SpeedPostfix` Priority.Normal (`TerrainEase.cs:62-107, 161-162`);
  `NightRest.SpeedPostfix` (dlug snu gracza i AI) Priority.Last (`NightRest.cs:1779, 1806-1811`); `MarchPace.SpeedPostfix` Priority.Last -
  sufit kolumny x WorldPace (`MarchPace.cs:95-150, 162`).

### 1.2 Arytmetyka [K]

- `ExplainedNumber.ResultNumber = BaseNumber x (1 + SumOfFactors)`, `Add` dopisuje do `BaseNumber` (`dec32\...\ExplainedNumber.cs:122, 156`).
- `MobileParty.CalculateSpeedForPartyUnified` podaje do `CalculateFinalSpeed` CALY wynik bazy z jej czynnikami (`MobileParty.cs:3098-3134`),
  wiec czynniki bazy (WorldPace -0.25, jazda, trudnosc) i koncowe (teren, noc, pory RB) sa JEDNA suma. Plaska kara `Add(-0.1)` schodzi wiec
  jako `0.1 x (1 + suma)`.
- **Cache predkosci** (`MobileParty.cs:3056-3071`): predkosc koncowa liczy sie od nowa tylko, gdy zmieni sie sciana siatki nawigacji,
  noc (`Campaign.IsNight`, ROT 22-02), status jenca lidera albo wiatr (morze), albo gdy zmieni sie wersja wagi (baza). Sciany siatki ROT maja
  8-10 jedn. (`B\wioski-v4\DROGI.md` rozdz. 5). Wszystko, co zalezy od POZYCJI wewnatrz sciany albo od GODZINY (poza noca), wchodzi z opoznieniem.

---

## 2. 7.1 - "Najpierw blokady tempa; jedna skala czasu"

### 2.1 Co bylo w planie (AS: `AUDYT-CZAS-MAPA.md` rozdz. 0 pkt 3 i rozdz. 8 krok 2; a07 T-1..T-4)

Trzy rzeczy, ktore "zablokuja kazde dalsze zwolnienie swiata": podloga `MinimumSpeed` 1.0, plaskie kary `TerrainEase` nieskalowane
z tempem, `Campaign.Estimated*Speed` (AI liczy dni drogi na vanillowe tempo). Do tego T-1 (WorldPace jako mnoznik wyniku zamiast
`AddFactor`). Wszystko bylo przygotowaniem pod 7.2.

### 2.2 Stan w kodzie [K]

| Blokada | Dzis | Po odrzuceniu 7.2 |
|---|---|---|
| Podloga 1.0 | nie ruszana (vanilla `:357`; RBL ma wlasna 0.2) | przy 75% lapie tylko armie ok. 2000 w nocy (22-02) / w lesie; T10 i tak kladzie armie spac 0-6 - **nie zmieniac** (bylby to wylacznie zysk predkosci duzych armii, czyli zmiana tempa) |
| Kary terenu x pace | plaskie (`TerrainEase.cs:79-103`), liczby Jeffa z 02.09 w jednostkach mapy | skalowanie x0.75 zmieniloby liczby Jeffa (las -0.075 zamiast -0.1) = zmiana tempa - **nie zmieniac** |
| WorldPace jako mnoznik (T-1) | `AddFactor` na bazie (`WorldPace.cs:56`) | przy 75% jazda -7%, piechota bez zmian = zmiana tempa - **nie zmieniac** |
| `Estimated*Speed` | vanilla stale 3.36 / 4.2 / 3.43 / 3.41 / morskie, ustawiane przy KAZDYM wczytaniu i nowej kampanii (`Campaign.cs:1193-1204`, wolane `:1667, 1686`) | niezgodnosc istnieje DZIS: pomiar [P] "Miara: marsz" - partie > 300 ludzi mediana 117-124 km/dobe = ok. 25 jedn./dobe, a AI liczy `3.36 x 24` = 81 jedn./dobe (vanilla sama przeszacowuje o 1.2x, u nas 2-3x). Uzywa tego 12 plikow gry: wybor celu armii (`DefaultTargetScoreCalculatingModel.cs:248`), poscig (`AiEngagePartyBehavior.cs:59`), wizyty w osadach (`AiVisitSettlementBehavior.cs:683, 793`), teleport bohaterow (`DefaultDelayedTeleportationModel.cs:18`, `HeroSpawnCampaignBehavior.cs:282`), karawany (`CaravansCampaignBehavior.cs:423, 977`), wies (`DefaultVillageTradeModel.cs:12, 19` - tu sie skraca, zasieg wsi niezmienny) |

Kolizje `Estimated*Speed`, gdyby go zmieniac: `MarketCarts.PerDay` juz mnozy `EstimatedAverageVillagerPartySpeed x WorldPace`
(`MarketCarts.cs:338-345`) - podwojne liczenie; `HorseFate.cs:883` (zasieg = 2 x speed x 24); `PartyBase.cs:653` (promien spotkania
z `EstimatedMaximumLordPartySpeedExceptPlayer` - NIE ruszac). **Rada projektu:** nie w etapie 7 (zmienia wybor celow AI i dlugosc wojen
bez zmiany tempa; nikt o to nie prosil). Jesli kiedys - tylko z linia pomiarowa i przegladem KRONIKI WOJEN.

### 2.3 "Jedna skala czasu" - co realnie zostalo [K]

T1 (09.10) przestawil na zegar gry: okno obozu `InCamp` (`NightRest.cs:216-230`), straznik snu `GuardDue` co 0.1 h gry
(`NightRest.cs:344, 1334-1354`), meldunek kursu x1.2 i godziny obozu (`Wayfinder.cs:63-74`). Logika gry nadal liczona w sekundach realnych
albo w klatkach:

| Miejsce | Dzis | Skutek przy x8 (`FastForwardMultiplier` 8, `Settings.cs:1108`) | Zmiana |
|---|---|---|---|
| `CrossingLaw.AiTick` (`CrossingLaw.cs:111-146`) | co 4. wywolanie `TickEvent` (`_aiAcc += 0.5`, prog 2) - zalezy od FPS i szybkosci zegara; `SetMoveGoToPoint` wrogim lordom bez sprawdzenia snu | przeglad co ok. 4 klatki = rozna liczba godzin gry; budzi spiacych (7.6) | `CampaignTime` co 0.1 h gry (wzor `GuardDue`) + pomijac spiacych |
| `HideoutPurge.OnTick` (`HideoutPurge.cs:135-139`) | odwet: rozkaz pogoni odnawiany co 3 s realne | przy x8 co ok. 7 h gry, przy x1 co ok. 0.9 h | co 0.25-0.5 h gry |
| `NightRest` namioty (`:1033-1034` 5 s, `:1324-1326` 2 s) | odswiezanie WIDOKU namiotow | tylko wyglad | zostawic (widok, nie logika) |
| `Wayfinder.cs:51`, `QuartermasterLaw.cs:1033, 1065`, `CrossingLaw.cs:92` | dlawienie komunikatow | tylko UI | zostawic |

Komentarze roku 168 (mylace nastepnego Claude): `WorldPace.cs:14-28`, opis `WorldPacePercent` (`Settings.cs:370` "lore-true month of the
168-day calendar" - to jest tekst MCM po angielsku, widoczny w grze). "Szesc nocy w kodzie" (a07 3.6) po T1/T10: oboz 0-6 (`InCamp`),
ksiega snu 21-swit (`NightRest.cs:123`, `NightMarch.cs:965`), menu snu i HideoutAlarm 21-5 (`NightRest.cs:1685, 1703`, `HideoutAlarm.cs:102`),
wzrok 21-4 (`SightRange.cs:35`), warsztaty 23-5 (`ArmouryBehavior.cs:1406`, `TroopSelfMend.cs:133`), ciemnosc gry 22-02 (ROT SunSet/SunRise).
To rozne zjawiska (sen, praca, wzrok, ciemnosc) - **nie scalac** w etapie 7 (zmiana rozgrywki bez prosby); ewentualnie jedna klasa
`DayClock` z nazwanymi oknami - to porzadek, etap 8 / Z.8.

### 2.4 Propozycja 7.1 (do projektu)

7.1 = (a) dwa przeglady z klatek / sekund realnych na zegar gry (CrossingLaw, HideoutPurge), (b) CrossingLaw nie rusza spiacych (styk 7.6),
(c) poprawka tekstu MCM `WorldPacePercent` i komentarzy 168 -> 364. Blokady tempa: **skreslic** z etapu 7 jako niepotrzebne (warunek
odrzuconego 7.2); `Estimated*Speed` - osobny pomysl na pozniej. Ryzyko: male. Nowa kampania: nie. Stary zapis: bez stanu.

---

## 3. 7.4 - Kary terenu: zwrot kary, ktorej gra nie nalozyla

### 3.1 Blad [K] (AS: `AUDYT-KONFLIKTY.md` S7, `AUDYT-PELNY-LOGISTYKA.md` N4 - POTWIERDZONE ponownie)

`TerrainEase.Swap` (`TerrainEase.cs:56-60`) zawsze robi `AddFactor(+0.3)` w lesie (`:79`), `+0.1` na pustyni (`:84`), `+0.1` w sniegu (`:98`),
`+0.25` noca (`:103`), potem `Add(-kara Jeffa)`. Vanilla nakłada mniej w trzech przypadkach:

| Przypadek | Vanilla nalozyla | My zwracamy | Wynik dzis |
|---|---|---|---|
| Las, zwiadowca z ForestKin, >= 75% pieszych | `-0.3 x (0 - (-0.5))` = **-0.15** (`:277`; `DefaultPerks.cs:2139` PrimaryBonus -0.5) | +0.30 | +0.15 premii ponad plaska kare |
| Las, kultura z `battanian_forest_speed` | -0.30 i osobno **+0.15** (`EffectBonus 0.5 x 0.3`, `:281`; `DefaultCulturalFeats.cs:119`) | +0.30 (cecha +0.15 zostaje) | +0.15 premii ponad plaska kare |
| Pustynia/wydmy, kultura z `aserai_desert_speed` | **0** (`:295`; opis cechy "No speed penalty on desert", `DefaultCulturalFeats.cs:117`) | +0.10 | +0.10 premii, a potem -0.2 plasko |

Kogo dotyczy (ROT `ROT-Content\ModuleData\spcultures.xml`, policzone dzis): cecha lasu - `battania` (**Polnoc**), `nightswatch`, `skagosi`,
`freefolk`, `qohorik`, `summer`, `whitewalker` (Inni), `ibbenese`; cecha pustyni - `aserai` (**Dorne**). Przyklad [S]: kolumna Polnocy
o bazie 4.0 i sumie czynnikow ok. 1.1 - w lesie `4.0 x 0.15 = +0.6` minus `0.1 x 1.1` = **ok. +0.5 (+12%) szybciej niz na rowninie**.
Dorne na pustyni ok. +0.4 - 0.22 = ok. +5%. Zamysl Jeffa (02.09): las, pustynia maja SPOWALNIAC; cecha kultury ma kare ZMNIEJSZAC
(Polnoc "50% less speed penalty", Dorne "no penalty"), nie zamieniac w premie.

Sprawdzone bez bledu: brod/most/rzeka (`:287` zawsze -0.3 na ladzie - zwrot rowny), snieg (`:317` zawsze -0.1, takze na morzu - nasz zwrot
tez bez warunku morza), noc (`:323` zawsze -0.25 na ladzie; NightRunner/DayTraveler to osobne premie - zostaja jak DesertBorn i Pathfinder).
Lancuch: ROT/NavalDLC/RBL wolaja vanille, wiec zwrot na najbardziej zewnetrznym poziomie trafia w to, co nalozyla vanilla.
Nie da sie czytac linii rozpiski: gra liczy ruch bez opisow (`includeDescriptions = false`), linie sa puste (`TerrainEase.cs:23-25`).

### 3.2 Zmiana

W `TerrainEase.SpeedPostfix` te same warunki co vanilla (bez czytania rozpiski):
- las: `vf` = ForestKin(zwiadowca `mp.EffectiveScout`, udzial pieszych >= 0.75 z `MemberRoster`, jak `:268-277`) ? 0.15 : 0.30;
  `feat` = `PartyBaseHelper.HasFeat(mp.Party, DefaultCulturalFeats.BattanianForestSpeedFeat)` ? `EffectBonus x 0.3` : 0;
  zwrot `AddFactor(+vf - feat)` (oddajemy dokladnie netto vanilli), nasza kara `Add(-ForestSpeedPenalty x (vf - feat) / 0.3)`
  (ulamek kary vanilli, ktory zostal: ForestKin 50%, cecha 50%, oba 0%);
- pustynia: `HasFeat(AseraiDesertFeat)` ? zwrot 0 i kara 0 : jak dzis (DesertBorn bez zmian - osobna premia);
- reszta bez zmian. Wpis w rozpisce z ta sama nazwa ("Forest") - dymek pokaze prawde (u Polnocy "Forest +0.15 / Forest -0.05").
- Opcjonalnie (projekt): linia testowa w `DailyAudit` (`TerrainEase.cs:126-153`) - raz na dobe licznik partii AI w lesie / na pustyni
  wedlug cechy, tylko log.

Koszt [S]: dodatkowe sprawdzenie tylko w lesie i na pustyni (perk zwiadowcy, `List.Contains` cech kultury, petla po skladzie tylko przy ForestKin);
`CalculateFinalSpeed` biegnie przy zmianie sciany / nocy - dziesiatki tysiecy wywolan na dobe -> **< 0.01 s/dobe**.
Ryzyko: male; Polnoc, Dorne i 6 kultur ROT zwolnia w swoim terenie o kilka-kilkanascie procent (to naprawa, nie nowa kara).
Kolizje: MarchPace (sufit po nas - bez zmian), NightRest (procent dlugu - bez zmian), 7.5 (premia drogi w lesie - patrz 4.3).
Nowa kampania: nie. Stary zapis: bez stanu. MCM: bez nowych kluczy (wylacznik `TerrainEaseEnabled` jest).

---

## 4. 7.5 - Pelne drogi, +15% po drodze, drogi do wiosek i Valyrii

### 4.1 Stan [K][P]

- Gra nie zna drogi: `TaleWorlds.Core.TerrainType` nie ma "Road" (Plain 1 ... UnderBridge 25 - ilspycmd dzis); w siatce ROT 9 typow,
  farba drog ROT (`terrain.bin` warstwa 13, ok. 10 000 jedn., szer. ok. 0.5 jedn.) lezy na zwyklych scianach Plain/Steppe (`DROGI.md` 1, 5).
- Trasa = najkrotsza po scianach siatki bez kosztu terenu (`MobileParty.cs:3712`, `SandBoxNavigationCache.cs:88, 134`,
  `DefaultPartyNavigationModel.cs:19-23` - za `DROGI.md` 5). AI nie skreci na droge.
- Armoury: zadnego widoku ani premii; `RoadMemoryFix.cs`, `IslandRoads.cs`, `MarketRoad.cs` to pamiec tras / wyspy / cel wozow.
  Proba 1.13 (2 kawalki mapy, decale) **nie zrobiona** (brak w CHANGELOG/STAN-PRAC/kodzie). Pytanie 20 ("ile drog") czeka na probe.
- Dane offline sa: `B\wioski-na-mapie\a-teren\drogi_raster.npz` (maska farby ROT), `B\wioski-v4\drogi\arm_map_roads.tsv` (3 107 linii,
  ok. 32 tys. jedn.) i `arm_map_road_stamps.tsv` - ALE siec liczona na ukladzie 4000 wiosek: konce sciezek trafiaja w 843 z 2 446 wiosek
  w grze (a08 3.2) -> przed uzyciem ponowny przebieg `s6_siec.py` na `ModuleData\arm_map_villages.tsv` z gry (D-0, 7.6 s).
- Decyzja Jeffa 09.10 (H): "proba drog TAK + po drodze szybciej (+15%) TAK" (`STAN-PRAC.md:773`). 12.10: "zostaw predkosc poruszania
  sie jak jest". **Sprzecznosc - pytanie do Jeffa (rozdz. 9).**

### 4.2 Jak zrobic premie, jesli Jeff powie TAK [K][S]

1. **Maska drog w ModuleData** (`arm_map_roadmask.bin`, raster 1 jedn., ok. 1600 x 1600 bit = 320 KB, naglowek: CRC sceny ROT + CRC ukladu
   wiosek jak `layout_crc32` W-4): bit = 1, gdy srodek komorki <= 0.75 jedn. od farby ROT albo od naszego traktu / drogi wiejskiej
   (sciezki do wiosek i polne - decyzja 20). Odczyt O(1). Zly CRC = premia wylaczona + jedna linia w logu (nie gasic reszty).
2. **Premia** - wlasny postfiks na `CalculateFinalSpeed` kazdego modelu, `SpeedDepth.OutermostFinal`, Priority.Normal (PO TerrainEase,
   PRZED sufitem MarchPace i dlugiem snu - oba Priority.Last): na ladzie, poza lasem (M-2: las juz spowalnia), poza brodem/rzeka,
   bit maski = 1 -> `__result.Add(0.15 x wynik / (1 + SumOfFactors), "Road")` (dokladnie +15% biezacego wyniku, wzor `MarchPace.cs:131-135`;
   `AddFactor(0.15)` dawaloby rozne procenty zaleznie od sumy czynnikow). Sufit kolumny dalej obowiazuje (jazda moze go dotknac).
3. **Cache** (rozdz. 1.2): bez tego premia wlaczalaby sie / gasla dopiero przy zmianie sciany 8-10 jedn. albo o 22:00 / 02:00, czyli
   "przyklejalaby sie" do calej sciany. Latka: postfiks na prywatnym `MobileParty.IsLastSpeedCacheInvalid` (`MobileParty.cs:3056`):
   gdy `__result == false`, a bit maski pod partia rozni sie od zapamietanego - `true`. Stan: slownik partia -> bit, czyszczony przy nowej
   kampanii / wczytaniu (bez zapisu). Ryzyko: JIT moze wkleic mala metode - przy starcie licznik wywolan w logu ("wpieta, N wywolan
   w pierwszej godzinie"; 0 = premia dziala z opoznieniem, zglosic).
4. Kto dostaje: wszyscy (gracz, lordowie, armie, karawany, wiesniacy, wozy MarketCarts) - jedna regula; inaczej gracz zyskuje kosztem
   swiata (AI i tak nie wybiera drog).
5. MCM (po angielsku): "Faster on roads" (wl./wyl.), "Road speed bonus (%)" 15. Widok osobno: "Unpaved roads (visual only)".

Skala [P][S]: odcinek prosty miedzy osadami biegnie po farbie ROT w 32% (twierdza-twierdza) i 55% (wies-twierdza) (a08 3.4) -> armie
ok. +5% km/dobe, wiesniacy ok. +8%, po dorysowaniu naszych drog wiecej. To JEST zmiana tempa swiata w tym zakresie.

### 4.3 Kolizje i spojnosc

- 7.4: premia poza lasem - kolejnosc kar i premii niezalezna (oba Add/AddFactor na najbardziej zewnetrznym poziomie).
- Most: Jeff ma "brod i most" -0.5 (`Settings.cs:376` "fording rivers and crossing bridges") - most na drodze dalej karany; zostawic (decyzja
  Jeffa), ewentualnie pytanie przy 7.5.
- RBL "Spring Roads" (+% wiosna wszedzie, nie tylko na drogach) i BKROTPatch +15% wiary wg terenu - niezalezne, sumuja sie.
- Ekonomia: wozy wsi i karawany szybsze -> wiecej kursow; `MarketCarts.PerDay` liczy z `Estimated x WorldPace` (`MarketCarts.cs:338-345`) -
  nie zobaczy premii (plan kursow lekko zanizony; do pomiaru, nie do latania na zapas). Warunek "miasta kupuja >= 98% plonu" sprawdzic.
- WorldMeasure ("Miara: marsz") pokaze wzrost km/dobe - dobry pomiar.
- Widok (D-0..D-3 z a08): decale jak tropy gry (pula 2 048), menedzer komorek 50 x 50, LOD, budzet stempli na klatke; proba 1.13 tylko
  w autotescie (prefab XML z bledem = blad przy starcie). Valyria (D-3): ciemniejszy stempel na drogach z lore (Pentos - Ghoyan Drohe - Norvos -
  Qohor, demon road); w Essos poza tymi trasami tylko szlaki (a08 3.8).

Koszt na dobe [S]: premia - odczyt maski + slownik przy kazdym sprawdzeniu cache (ok. 1 500 partii w ruchu x 600-1 200 klatek na dobe przy x8)
ok. 50-100 tys. odczytow x ok. 50 ns = **0.04-0.1 s/dobe** (doba dzis 11.7-18 s -> +0.3-0.7%); mozna zdlawic do co 0.05 h gry na partie.
Widok - koszt klatki (DROGI.md 4: 150-550 decali w kadrze), nie doby. Wielkosc: D. Nowa kampania: nie. Stary zapis: bez stanu.

---

## 5. 7.6 - Obozy: spiacy lord dostaje rozkazy; AI placi za noc; zbiorka

### 5.1 Co juz jest [K][P]

- **AI placi za nieprzespana noc - JEST:** T10 + T10-R (`NightMarch.cs:14-58`, wgrane w WGRANIU 19, 10.10): ksiega snu AI jak gracza,
  dlug 1-3, kary z tych samych tablic (`NightRest.SpeedPostfix/MoralePostfix`), nocny marsz tylko z powodu, max 2 noce forsowne.
  Pomiar [P]: "NocnyMarsz: swit" co dobe - np. doba 108955: nowy dlug 1/2/3 = 10/1/0, splaconych 92%. Martwy klucz `AiNightsAwakeInChase`
  zyje (`Settings.cs:1122`, uzyty w T10 R1). Do etapu 7 nic.
- **Zbiorka - JEST w sciezce T10:** w `LordNight` (`NightMarch.cs:783-850`) niedoczepiony czlonek armii (w drodze na zbiorke) liczy sie jak
  samotny lord (`LoneField`) i spi; stara regula T1 "armia -> nie spi" (`NightRest.cs:801`) dziala tylko przy `AiNightMarchByReason = false`
  (domyslnie true, w `Armoury.json` Jeffa klucza nie ma -> domyslna). Do potwierdzenia w tescie linia "AiNightCamp: marsz" (licznik
  "w drodze na zbiorke" dotyczy tylko porownania na sucho).
- **Spiacy dostaje rozkazy - NADAL:** straznik `HoldSleepers` (`NightRest.cs:414-468`) kladzie z powrotem po fakcie, co 0.1 h gry.

### 5.2 Pomiar [P] (`noc.log`, 120 nocy, 720 linii godzinowych + linie switu w glownym logu)

| Miara | Wartosc |
|---|---|
| spiacych na godzine | sr. 797, maks. 957; wodzow armii sr. 5.5 |
| obudzonych cudza reka | 10 094 w liniach godzinowych + 878 w liniach switu = **91 na noc**; rodzaj: Hold bez rozkazu 74+4, **rozkaz jak nasz zapamietany 6 647+535**, inny rozkaz 3 373+339 |
| zjazd obudzonych | sr. wazona **0.40 jedn.** (ok. 1.9 km), maks. 1.0 (cel a07: < 0.3) |
| wg godziny (linia 1:00 = obudzeni 0-1 itd.) | 1:00 32/1247/576, 2:00 20/1330/697, 3:00 8/1509/873, 4:00 7/1255/621, 5:00 7/1306/606 (Hold/jak nasz/inny) - **rownomiernie** |
| "AiNightCamp: ruch" (pomiar z pozycji) | samotni lordowie w ruchu > 0.35 jedn./h w godzinie obozu: 7 000 partio-godzin, z tego BEZ POWODU 2 435 (**ok. 20 na noc**), w tym obudzeni cudza reka 2 235 (92%) |

Wniosek [S] (hipoteza do sprawdzenia): StrategicCampaignAI wola `SetMove*` co 4 h (0:00 i 4:00) - dalby skoki w liniach 1:00 i 5:00; ich nie ma.
Rownomierne "rozkaz jak nasz" w kazdej godzinie pasuje do WLASNEGO AI partii: `Ai.DisableForHours(1)` (`NightRest.cs:444, 838`,
`NightMarch.cs` tamze) ustawia `_enableAgainAtHour = teraz + 1 h` (`MobilePartyAi.cs:1561-1565`), a nastepny nasz takt jest o pelnej
godzinie - jesli tick AI partii trafi przed nasz takt, `EnableAgainAtHourIsPast` budzi AI (`MobilePartyAi.cs:437-447`) i ono od razu
wybiera ten sam cel. "Inny rozkaz" (ok. 1/3) to cudze mody i NASZE: `CrossingLaw.AiTick` (`CrossingLaw.cs:146`), GrandTourney
`SummonLords` o 0:00 z dziennego ticku (`TourneyBehavior.cs:313, 478`), odwet `HideoutPurge` (bandy), BK (uczty - osobny licznik T10).

### 5.3 Zmiana (O-3 z a07 + poprawka fazy)

1. **Faza:** wylaczenie AI spiacego nie moze wygasnac przed naszym taktem. **Kolizja:** `NightMarch.ForeignHold` (`NightMarch.cs:232-252`)
   rozpoznaje cudza blokade AI (uczta BK, gentry 72 h) po terminie `_enableAgainAtHour` > 1.5 h - proste `DisableForHours(2)` uznaloby nasz
   wlasny sen za cudza blokade. Dwie drogi: (a) odnawiac `DisableForHours(1)` w przegladzie straznika co 0.1 h gry (`NightRest.cs:1334`) dla
   partii w `_bedPos`/`_debtSleep`, gdy do konca zostalo < 0.3 h (termin zawsze <= 1 h - `ForeignHold` bez zmian) - **rada**; (b) `DisableForHours(2)`
   i prog `ForeignHold` 2.5 h. W obu: o swicie (galaz `!night` w `AiNightCamp`, `NightRest.cs:708-734`) i przy oddaniu rozkazu (`ApplyOrder`)
   `mp.Ai.EnableAi()` - inaczej AI wstalby do godziny po swicie. T10 alarm juz wola `EnableAi` (`NightMarch.cs:813, 833`). Miejsca z
   `DisableForHours(1)`: `NightRest.cs:444, 838, 900`, `NightMarch.cs:776, 842, 1445, 1486, 1547, 1623`.
2. **Blokada `SetMove*` u spiacych:** prefiksy na `MobileParty.SetMoveEngageParty / GoAroundParty / GoToSettlement / GoToPoint /
   GoToInteractablePoint / EscortParty / PatrolAroundPoint / PatrolAroundSettlement / RaidSettlement / BesiegeSettlement / DefendSettlement /
   ToNearestLand` (`MobileParty.cs:3919-4028`; NIE `SetMoveModeHold`). Warunek blokady: wlacznik MCM, `InCamp(godzina)` (najpierw - w dzien
   zero kosztu), partia lezy w obozie (`_bedPos.ContainsKey(mp)` - O(1); `_camping` to `List`, `NightRest.cs:210`) albo w snie dlugu
   (`_debtSleep`), `mp.MapEvent == null`, wywolanie NIE z naszego oddania rozkazu (flaga [ThreadStatic] ustawiana w `ApplyOrder`/
   `GiveOrderBack`/alarmie T10). Skutek: rozkaz nie wchodzi, a zamiast tego nadpisuje zapamietany (`_orders[mp]`) - o swicie partia idzie
   tam, gdzie chcial ja poslac ostatni rozkazodawca (SCA / BK). Licznik "zablokowane rozkazy" wedlug metody w linii godzinowej.
3. **Nasze wlasne:** `CrossingLaw.AiTick` pomija spiacych (albo blokada go zatrzyma - wtedy rozkaz odwrotu zostaje zapamietany i wykona
   sie o swicie, co ma sens: straz mostu i tak stoi); GrandTourney `SummonLords` - zostawic (blokada przesunie wyjazd na swit).
4. `HoldSleepers` zostaje jako siatka bezpieczenstwa (zjazd > 0.3 bez rozkazu).

Koszt [S]: prefiks = porownanie godziny; w nocy jedno sprawdzenie slownika. `SetMove*` wolane przy decyzjach AI - kilkadziesiat tysiecy
na dobe -> **< 0.01 s/dobe**; mniej obudzen = mniej pracy straznika i mniej przeliczen trasy.
Ryzyko: srednie - zablokowany rozkaz, ktory byl potrzebny (ucieczka nadana przez cudzy mod u spiacego): alarm T10 najpierw zdejmuje partie
z obozu (`_camping.Remove`, `_bedPos.Remove`), wiec po alarmie blokada nie dziala; bitwa (`MapEvent`) - bez blokady. W grze `SetMove*` poza AI partii
wolaja m.in. `Army.cs:930` (wodz nowej armii na punkt zbiorki - przy blokadzie wyruszy o swicie), `SetPartyAiAction.cs:33, 48` (rozkazy
z dialogu/akcji); reszta akcji wola tylko `SetMoveModeHold` (nieblokowane). Zaproszenie na uczte BK u spiacego tez przesunie sie na swit.
Stary zapis: bez nowego stanu (zapamietane rozkazy i tak nie ida do zapisu - jak dzis). Nowa kampania: nie.

---

## 6. 7.7 - Turnieje, sojusze i rozejmy licza czas jak reszta swiata

### 6.1 Turnieje gry [K][P]

- `DefaultTournamentModel.GetTournamentStartChance` (`dec32\...\DefaultTournamentModel.cs:21-32`): `|hash(town) % 3| != CampaignTime.Now.GetWeekOfSeason`
  -> 0. Przy 13 tygodniach w sezonie turniej moze wystartowac tylko w tygodniach 0-2 (3 z 13 = 23% czasu). Jedyny uzytkownik `GetWeekOfSeason`
  w grze (grep). Model czynny: `NavalDLC.GameComponents.NavalDLCTournamentModel` (CrashScribe "Tournament mdl"), ktory przekazuje to do
  `BaseModel` (`a6dec\naval\...\NavalDLCTournamentModel.cs:73-76`) = Default. Wolane raz na dobe na miasto bez turnieju, po 15 dniach od
  ostatniego (`TournamentCampaignBehavior.cs:119-141`). TournamentsXPanded tej metody nie lata (AUDYT-KALENDARZ 5.2 pkt 6); jego
  `TournamentRemoveAfterDays 7`, `LeaderboardLossPerWeek 1` - w dniach/tygodniach (ok).
- **Pomiar [P]:** zdarzenia "turniej" z ksiegi obiegu (linia "Obieg", licznik `turniej N/M` na dobe, 120 dob) wedlug tygodnia sezonu
  `(dzien / 7) % 13`: tydz. 0: 3, 1: 10, 2: 30, 3: 25, 4: 12, **5-12: 0** (w 5-12 tylko konczace sie wczesniejsze). Potwierdza blad.
- Zmiana: prefiks na `DefaultTournamentModel.GetTournamentStartChance` - ta sama formula, tydzien `(int)CampaignTime.Now.ToWeeks % 3`
  (przy 3 tygodniach w sezonie to dokladnie vanilla). Kazde miasto znow co 3. tydzien, jak w vanilli na dobe -> turniejow na rok ok. 4.3x
  wiecej niz dzis (na dobe tyle co vanilla). Koszt: ~100 wywolan na dobe - **0**.
- Kolizja GrandTourney: przejmuje turnieje gry i wzywa lordow (`TourneyBehavior.cs:380-400`, `SummonLords :262-320`) - ok. 4x wiecej
  zjazdow; `LocalWhenAtWar` (krolestwo w wojnie -> impreza lokalna bez wzywania) zostaje bezpiecznikiem. Zloto nagrod: ksiega obiegu
  pokazuje `turniej N/0` (0 z niczego) - sprawdzic w tescie, ze M dalej 0. GrandTourney sam liczy w dniach gry (`Today = ToDays`,
  `TourneyBehavior.cs:55`; `GatherDays 4`, `HostCooldownDays 365`, `AdoptSilenceDays 6`) - zgodny z rokiem 364, bez zmian.

### 6.2 Sojusze i wezwania do wojny (vanilla) [K][P]

- `DefaultAllianceModel.MaxDurationOfAlliance => Days(84)`, `MaxDurationOfWarParticipation => Days(42)` (`DefaultAllianceModel.cs:112, 114`) -
  w vanilli "rok" i "pol roku", u nas kwartal i 6 tygodni. Uzycie: `AllianceCampaignBehavior.cs:463, 489` (koniec sojuszu / porozumienia),
  cena i budzet wezwania `:569, 583` (oba x dni -> stosunek bez zmian). Zaden mod nie nadpisuje `AllianceModel` (grep DLL: tylko Armoury).
- Pomiar [P]: 9 wezwan w 120 dobach ("Wezwania do wojny (168): X wzywa Y ... przez 42 dob"), 3 porozumienia w toku na koniec.
- Zmiana: postfiks na getterach `DefaultAllianceModel` -> `Days(364)` i `Days(182)` (albo `DaysInYear` i `DaysInYear/2`, liczone przy odczycie).
- **Kolizja CrownCallToWar** (`CrownCallToWar.cs:16-24, 60`): `Days()` czyta `MaxDurationOfWarParticipation` -> cena x4.3 i rata dzienna
  bez zmian (cena / dni); korona placi 4.3x dluzej (zgodnie z regula "rata z wplywow dnia, brak = koniec porozumienia"). Prog gry
  `CallToWarWallet < -100000` (`DefaultAllianceModel.cs:378`) staly - z wieksza cena AI szybciej przestanie wzywac. Do obserwacji w linii
  "Wezwania do wojny". Istniejace sojusze i porozumienia trzymaja zapisane daty konca (stary zapis bez bledu).

### 6.3 Rozejmy i Diplomacy [K]

- Vanilla: propozycja wojny dopiero 20 dni po pokoju (`KingdomDecisionProposalBehavior.cs:242`), niechec do pokoju przez 150 dni wojny
  (`DefaultDiplomacyModel.cs:1027`), trybut 100 dni - liczone w dniach, "1 dzien = 1 dzien" -> zostaja (AUDYT-KALENDARZ 2.x).
- BK: rozejmy w latach (`BKDiplomacyBehavior.cs:206-257`, `KingdomDiplomacy.cs:51` - `YearsFromNow`) -> same sie przeskalowaly.
- Bannerlord.Diplomacy (`audyt-zloto\Diplomacy\Diplomacy\Settings.cs`, `CooldownManager.cs:56, 90, 112`): JSON Jeffa
  `Configs\ModSettings\Global\Diplomacy\DiplomacySettings_v1.2.json` ma nadal wartosci pod rok 84: `MinimumWarDurationInDays 21`,
  `DeclareWarCooldownInDays 21`, `MinimumAllianceDuration 42`, `NonAggressionPactDuration 84`, `MinimumTimeSinceLastCivilWarInDays 240`,
  `MaximumFactionDurationInDays 120`. Propozycja AUDYT-KALENDARZ 5.2 pkt 8: 90 / 90 / 182 / 364 / 1000 (zakres MCM 0-1000; 1040 sie nie zmiesci) / 520.
  Zakresy MCM pozwalaja (0-500, 0-500, 0-500, 0-1000, 0-1000, 0-1000). Zero kodu: wpis w JSON (gra zamknieta, kopia `.bak`) albo suwak w MCM -
  to jest instrukcja dla Jeffa (Z.9 "suwak Diplomacy"). `WarExhaustionPerDay 0.25`, `ExpansionismDecayPerDay 1`, szanse dzienne - zostaja (dzienne).
  `MessengerTravelTime 3` (zakres 0-10) - przy marszu ok. 120-280 km/dobe kurier 3 dni to ok. 600-800 km; zostaje.
- Pomiar (rozpoznanie A, rozdz. 1.3): pokoj i nowa wojna tej samej pary po 3, 21 i 24 dniach - 21/24 to dzisiejsze progi 20-21 dni;
  przypadek 3 dni omija oba progi (wojna fabularna ROT / BK / Diplomacy z innego powodu?) - przed kodem sprawdzic, kto ja wypowiedzial.

Koszt 7.7 na dobe: **~0**. Ryzyko: srednie dla mapy wojen (dluzsze sojusze i pakty = mniej rownoczesnych wojen) - miara: KRONIKA WOJEN
(liczba wojen), "Wezwania do wojny". Nowa kampania: nie. Stary zapis: trwajace sojusze/pakty koncza sie wedlug starych dat.

---

## 7. Kolejnosc i kolizje miedzy paczkami etapu 7 (moje pozycje)

| Paczka | Pliki | Styka sie z |
|---|---|---|
| 7.4 | `TerrainEase.cs` | 7.5 (ten sam postfiks / ta sama kolejnosc), MarchPace (sufit po nas), BK postfiks na Default (niezalezny) |
| 7.6 | `NightRest.cs`, `NightMarch.cs` (faza `DisableForHours`), nowa latka `SetMove*`, `CrossingLaw.cs` | 7.1 (CrossingLaw - jeden przeglad, jedna zmiana), T10 (alarm, sen dlugu), GrandTourney (wzywanie o 0:00), 7.12 (licznik Bliznakow - `CrossingLaw` strzeze Bliznakow `ROT_town3`, Moat Cailin `castle_B1`, Bloody Gate `castle_EN2`, `CrossingLaw.cs:31-33`; `Measure169c.cs:891-910` liczy dzis tylko wjazdy do osady Bliznaki) |
| 7.1 | `CrossingLaw.cs`, `HideoutPurge.cs`, `Settings.cs` (tekst MCM) + `tools/gen_mcm.py`, komentarze | 7.6 (CrossingLaw), 7.12 (myto przy przeprawach - ten sam plik) |
| 7.7 | nowa latka turnieju + sojuszu (Armoury, np. `WorldTime77.cs`), JSON Diplomacy (Jeff) | CrownCallToWar, GrandTourney, ksiega obiegu ("turniej") |
| 7.5 | maska drog (dane), nowy postfiks + latka cache, widok (menedzer decali) | 7.4, MarketCarts, WorldMeasure, 7.9/7.10 (dymek i wyglad wiosek - ten sam menedzer widoku `MapVillagesView`?), 1.13 |

Proponowana kolejnosc: 7.4 -> 7.7 -> 7.1+7.6 (jeden plik CrossingLaw, ale osobne commity) -> 7.5 (po odpowiedzi Jeffa i probie 1.13).
Wszystkie w jednym scaleniu z etapami 5-8 (zasada Jeffa 12.10 10:30), kazda z wylacznikiem MCM.

---

## 8. Koszt na dobe (podsumowanie) [S]

| Pozycja | CPU na dobe gry | Uwagi |
|---|---|---|
| 7.1 | 0 albo mniej (przeglad co 0.1 h gry zamiast co 4 klatki) | |
| 7.4 | < 0.01 s | tylko las/pustynia |
| 7.5 premia | 0.04-0.1 s | sprawdzenie maski przy cache; zmierzyc stoperem 1/16 jak `HorseState.SpeedBegin` |
| 7.5 widok | koszt klatki (ulamek ms) | nie doby |
| 7.6 | < 0.01 s | mniej obudzen = mniej pracy |
| 7.7 | ~0 | |

Doba dzis: 11.7 s nowa kampania, 18.2 s zapis z konca biegu (STAN-PRAC, WGRANIE 33). Etap 7 (bez widoku) dokłada < 1%.

---

## 9. Pytania do Jeffa (zmieniaja rozgrywke)

1. **Drogi a predkosc (7.5).** 09.10 powiedziales "po drodze szybciej +15% TAK", 12.10 "zostaw predkosc poruszania sie jak jest". Premia za
   droge przyspiesza marsz tam, gdzie biegnie droga (armie ok. +5% na dobe, wozy wsi ok. +8%), AI nie wybiera drog. Opcje:
   (A) drogi tylko jako widok, predkosc bez zmian; (B) +15% po drodze dla wszystkich (gracz, lordowie, karawany, wsie).
   **Rada: A teraz** (zgodne z 12.10, zero ryzyka dla ekonomii), B jako wylacznik w MCM domyslnie wylaczony - do wlaczenia po obejrzeniu drog
   w grze; jesli B, to dla wszystkich, nigdy tylko dla Ciebie.
2. **Diplomacy (7.7) - wpis do Twojego pliku ustawien** (zero kodu): pakt o nieagresji 84 -> 364, minimalny sojusz 42 -> 182, minimalna wojna
   21 -> 90, przerwa przed nowa wojna 21 -> 90, wojna domowa 240 -> 1000, frakcja buntu 120 -> 520 dni. **Rada: tak** (ten sam rok co reszta
   swiata); zrobimy to przy zamknietej grze z kopia albo dostaniesz instrukcje (Z.9).

Bez pytania (bledy, decyzja projektu): 7.4, 7.6, turnieje i sojusze vanilli (7.7), 7.1.

---

## 10. Nowe "Skonczone, gdy" (zamiast "Winterfell - KP ok. 77 dni")

Propozycja dla calego etapu 7 (moje pozycje + warunki wspolne):
- **Tempo bez zmian:** "Miara: marsz" w tescie 120 dob - mediana km/dobe partii > 300 ludzi w granicach +-5% od dzis (117-124), czysto konne
  +-10% (przy opcji B drog: wzrost zgodny z udzialem drog, opisany w CHANGELOG).
- **7.4:** w rozpisce predkosci (audyt dzienny / linia testowa) Polnoc w lesie i Dorne na pustyni WOLNIEJSI niz na rowninie; zwrot = netto vanilli
  (0 roznic w liczniku testowym).
- **7.6:** obudzonych cudza reka < 10 na noc (dzis 91), zjazd sr. < 0.15 jedn. (dzis 0.40), lordowie w ruchu bez powodu < 3 na noc (dzis ok. 20),
  0 bledow `NightRest.*`/`HoldSleepers`/latki `SetMove*`; zbiorka: niedoczepieni czlonkowie armii spia (linia "AiNightCamp: marsz").
- **7.7:** zdarzenia turniejowe w KAZDYM tygodniu sezonu (dzis tygodnie 5-12: 0); nowe sojusze 364 dni, wezwania 182 dni w linii "Wezwania do wojny";
  JSON Diplomacy z nowymi wartosciami; liczba rownoczesnych wojen (KRONIKA WOJEN) bez zapasci (np. nie mniej niz polowa dzisiejszej).
- **7.1:** zadna logika gry nie liczy sekund realnych ani klatek (grep `DateTime.Now`/akumulatorow klatek - zostaja tylko widok i komunikaty);
  tekst MCM `WorldPacePercent` bez "168".
- **7.5 (jesli wchodzi):** proba 1.13 obejrzana przez Ciebie, decyzja 20 podjeta; widok drog bez spadku klatki > 1 ms; przy B - linia "Road"
  w rozpisce gracza na drodze i znikajaca po zjechaniu (latka cache wpieta).
- **Wspolne:** 0 bledow naszych modow; miasta kupuja co najmniej 98% plonu; progi etapow 2 i 5 (pomiar, bez spadku przez etap 7); koszt doby
  +< 1%; Twoj stary zapis wczytuje sie bez bledu.

---

## 11. Czego nie sprawdzilem / niepewne

- Hipoteza fazy `DisableForHours(1)` (5.2) - tylko z rozkladu godzinowego; potwierdzi ja dopiero test (spadek "rozkaz jak nasz" po zmianie).
- Czy JIT wkleja `MobileParty.IsLastSpeedCacheInvalid` (latka cache 7.5) - sprawdzic licznikiem przy starcie.
- Czy gra/BK/SCA wolaja `SetMove*` u wodza przy doczepianiu do armii w nocy (5.3 ryzyko) - przed kodem przejrzec `Army.cs` i SCA.
- Liczba wywolan `CalculateFinalSpeed` na dobe - szacunek; koszty to rzad wielkosci, zmierzyc stoperem 1/16.
- AIInfluence (zaciemniony) - nie wiem, czy ma wlasne stale sojuszy/turniejow.
- Data w CrashScribe przy tworzeniu postaci "Day 3641 of Summer, 299 AC" (`session-2026-10-12_02-44-08.log`, `GameState.cs:20` =
  `CampaignTime.Now.ToString()`) - dziwny dzien sezonu; poza zakresem (wyswietlanie, etap 8), notuje tylko.
