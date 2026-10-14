# Audyt bledow miedzy modami - 14.10.2026 (wf_bebe70f3-6a1)

Pytanie Jeffa (14.10, 18:01 UTC): "sprawdz, czy nie ma wiecej bledow miedzy modami ROT / BannerKings / patche - bo okazalo sie,
ze bledy powodowaly zle przyznawanie zamkow". Zakres: Bannerlord 1.4.8, ROT 8.1.8, czynne moduly wedlug LauncherData.xml.
Znane i naprawiane osobno - NIE liczone tutaj: nadawanie lenn (BKROTPatch CalculateHeroFiefScore = 0), oblezenia (czlon czasu),
bunty BK (MakeRebelKingdom), karawany BK w oblezeniu, premia pokoju RBL, warstwa dobrobytu BK x2, TournamentsXPanded,
licznik podatku BK 2^31.

## 0. Dla Jeffa - w skrocie

- Sprawdzilem 31 podejrzen. Kazde obejrzal osobny sceptyk: **27 to prawdziwe bledy** (1 krytyczny, 10 waznych, 16 drobnych), 4 odrzucone. Z 27 trzy psuja tylko log albo sa spisem, dwa byly juz znane.
- Najwazniejsze 5:
  1. **Krytyczne - tytuly BK po wczytaniu zapisu.** BK dokleja do Westeros i Essos 30 ksiestw z Calradii. Gdy rod wymrze, jego zamek idzie do takiego "ksiecia", czesto z innego krolestwa, a ten potem wygrywa nadania lenn. Twoj zapis z 04.10 juz to ma.
  2. **Ten sam blad BKROT co przy lennach, w kolejnych miejscach** (latka "pyta sama siebie" i dostaje 0): prawowitosc krolestw 0 (BK podsyca pretendentow), limit wplywu 0 (tansze wnioski o wojne i pokoj, zero paktow handlowych), wezwanie wasali za darmo, kultura stoi (+4 lojalnosci dziennie w kazdej osadzie, kultura osad nigdy sie nie zmienia).
  3. **25 twierdz bez tytulu BK i 3 krolestwa bez korony BK** (Lorath, Ibben, Qarth) - plik tytulow BKROT jest ze starego ROT. Przy Harrenhal, Seagard czy Lordsport nowa zasada lenn nie widzi prawowitego pana.
  4. **Nasz blad: przepychanka o rzemieslnikow** - BK przerabia ich na kulture pana (pan placi wplywem, relacja spada), a nasz CrashScribe nazajutrz to cofa; ok. 700 razy w 2 lata.
  5. **Ryzyko wywrotki gry** - gdy jako krol dasz lenno towarzyszowi z Westerlands, mniej wiecej raz na 6 razy gra wezmie zepsuty wzorzec ROT.
- Inne wazne: flota AI plynie jak po ladzie (bez wiatru i premii War Sails); zamek ponizej 100 dobrobytu dostaje nagle 2000 z niczego; rzeznicy robia ok. 33 miesa dziennie z niczego, piekarnie 59% ciast z samego zboza.
- Naprawy: tylko nasze latki z wylacznikiem w MCM, pliki obcych modow nietkniete. Kolejnosc: (1) tytuly BK, tor L, razem z nowa zasada lenn; (2) jedna latka na samowywolania BKROT, tor L + test 120 dob, dziala od nowej kampanii; (3) rzemieslnicy, wzorce ROT, reset dobrobytu, mieso z niczego, tory S/M; (4) flota i lancuchy modeli, tor L; (5) drobne i dokumenty.
- Kiedy: po biezacych pracach (bitwa szturmu, towarzysze), wszystko przed Twoja nowa kampania. Nic z tej listy nie jest jeszcze zrobione.
- Do sprawdzenia przy zamkach: Diplomacy sam sprzedaje najbiedniejszy zamek rodu, ktory ma wiecej twierdz niz swoj poziom i traci wplyw; nowa zasada lenn moze dac rodowi o jedna twierdze wiecej. Dodamy licznik, zanim cos zmienimy.
- Od Ciebie nic pilnego. Jedno pytanie na pozniej: czy rzemieslnicy podbitego miasta maja raz przyjac kulture nowego pana (tak chce BK), czy zawsze zostac miejscowi.

## 1. Jak powstal raport

- 4 katy: latki Harmony (takze nieudane przy starcie), modele gry (kto wygrywa, BaseModel, 0 / NaN), zachowania (liczone 2 razy albo wcale),
  dane XML i ustawienia. 31 znalezisk, kazde z dowodem [K] (kod w dekompilacji) i [P] (log), kazde sprawdzone przez osobnego sceptyka.
- Za potwierdzone uznane sa tylko werdykty sceptyka "prawdziwe: true". Waga i naprawa w tym raporcie to wersja PO sceptyku
  (pierwotna waga podana w tabeli). Odrzucone - rozdz. 4.
- Redaktor dopisal tylko dwie rzeczy sprawdzone w kodzie, oznaczone "[R]" (rozdz. 7): prog korupcji Diplomacy wobec pulapu FairFiefGrants
  oraz BKROTLoyaltyModel (z listy samowywolan) - nie jest nigdzie podpiety.
- Nic nie zostalo zmienione w kodzie, w grze ani w ustawieniach. Gry nie uruchamiano.

Skroty sciezek (scratchpady sa tymczasowe - dekompilacje mozna odtworzyc ilspycmd):

| Skrot | Sciezka |
|---|---|
| SC1 | `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad` |
| SC2 | `C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad` |
| SC3 | `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad` |
| G | `SC2\dec32\cs_full` (gra 1.4.8) |
| BK | `SC2\e4tools\bkfull` (BannerKings.Redux, zainstalowany) |
| BR | `SC1\dec\bkrot` (BKROTPatch.dll 26.07); te same klasy w `SC1\dec\BKROTPatch`, `SC1\k4\bkrot`, `SC2\bkrot_a8` (numery linii BKROTPatch.cs przesuniete: :184-208 w BR = :289-312 w bkrot_a8) |
| IL | `SC1\k2il\BKROTPatch-all.il` (zrzut IL BKROTPatch.dll) |
| ROT / NV / SCA / RBL | `SC1\dec\rot`, `SC1\dec\naval`, `SC1\dec\sca`, `SC1\dec\rbl` |
| AZ | `SC2\audyt-zloto` (DTE, BKROTPatch, ROT) |
| DIPL | `SC3\audyt-weryf-lordowie-kod\dipl` (Bannerlord.Diplomacy 1.4.7) |
| ZB | `SC2\zb\ln` (nasze drzewo: Armoury, CrashScribe; galaz `w-toku/lenna` 31c3d6d2) |
| LOG06 | `D:\Backup-Bannerlord\logs\2026-10-14-noc\session-2026-10-14_06-01-14.log` (bieg dwuletni, 728 dob) |
| LOG09 | `D:\Backup-Bannerlord\logs\2026-10-14-noc\session-2026-10-14_09-07-14.log` (test 120 dob) |
| ARM06 | `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-14_06-01-15.log` |
| TAB | `D:\Backup-Bannerlord\logs\2026-10-14-noc\tabele-06-01-15` |
| RGL | `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_33208.txt` (bieg 06:01) |
| ECO | `C:\Users\GAME\Documents\Mount and Blade II Bannerlord\CrashScribe\economy-2026-10-14_06-02-53.csv` |
| BKFC | `C:\Users\GAME\Documents\Mount and Blade II Bannerlord\Configs\ModLogs\bk-firstchance.log` |
| BKROTLOG | `<gra>\Modules\BKROTPatch\BKROTLog.txt` (bieg 09:07) |

## 2. Tabela wszystkich znalezisk

Kolejnosc: potwierdzone wedlug wagi po sceptyku, potem odrzucone. Numer AM-xx = kolejnosc zgloszenia. Tor: S (do 30 min), M (ok. 1 h), L (godziny, pelny lancuch + test 120 dob).

| Nr | Znalezisko | Werdykt | Waga (pierwotna -> po sceptyku) | Mody | Dowod (glowny) | Naprawa (klucz MCM, tor) |
|---|---|---|---|---|---|---|
| AM-26 | BK przy kazdym wczytaniu wszczepia 30 ksiestw kalradyjskich w drzewo tytulow ROT (sciezka pliku ROT ustawiana tylko dla nowej gry) | PRAWDZIWE | KRYTYCZNE -> KRYTYCZNE | BannerKings.Redux, BKROTPatch, ROT-Map | [K] BR `BKROTPatch.Behaviors\BKROTBehavior.cs:33-54`; BK `BKManagerBehavior.cs:159,194`, `TitleGenerator.cs:337-470`. [P] zapisy: `IronmanPEh4X2ByGArW.sav` (04.10) i `IronmangeCUdegikFtb` (30.08) 30/33 ksiestw, autotesty 0/33 (`SC1\k4\savescan3.py`) | 'BK Titles Use ROT File' (CrashScribe Mends, domyslnie TAK): sciezka ROT w InitializeTitles i RetrofitOrphanedTitles; opcjonalnie naprawa starego zapisu. Tor L |
| AM-01 | BKROT 'EarlyHeal': twierdza ponizej 100 dobrobytu dostaje 2000 (zamek) / 4000 (miasto) z niczego | PRAWDZIWE | WAZNE -> WAZNE | BKROTPatch (3 prefiksy Heal) vs gra, BK, Armoury | [K] BR `BKROTPatch.Patches\BKROTProsperityHeal_Patch.cs:8-9,25-32`; G `Town.cs:119-125`. [P] RGL:15716 Willow Wood 97.4 -> 2000, :16481 Pinkmaiden 99.0 -> 2000; TAB `ludnosc-bk.csv` | 'Stop BKROT Prosperity Reset' (Mends, TAK): zdjac 3 prefiksy BKROT, w zamian ochrona tylko przed NaN/Inf na setterze. Tor M |
| AM-10 | BKROTLegitimacyModel wola sam siebie: prawowitosc kazdego krolestwa = 0 | PRAWDZIWE | KRYTYCZNE -> WAZNE | BKROTPatch vs BannerKings (grupy radykalne, polityka wasali) | [IL] `legit.il:52287-52290` callvirt na this; [K] BR `BKROTLegitimacyModel.cs:21-27`, BK `KingdomDiplomacy.cs:83-100`, `DefaultRadicalGroups.cs:46-65`. [P] wprost brak (L nie jest logowane) | 'BKROT Self Call Fix' (Mends, TAK): transpiler callvirt -> call BKLegitimacyModel. Tor L |
| AM-11 | BKROTInfluenceModel: limit i zmiana wplywu = 0 dla calego BK | PRAWDZIWE | WAZNE -> WAZNE | BKROTPatch vs BannerKings (BKDiplomacyModel = model gry, armie, sukcesja, pakty) | [IL] IL:52047, :52140. [P] LOG06:6709 'koszt wniosku 100' (= 100 + 0.2 x 0) | 'BKROT Self Call Fix' (call BKInfluenceModel::CalculateInfluenceCap / Change). Tor L |
| AM-12 | BKROTArmyModel: wezwanie wasali (Call Banners) i laczenie armii za 0 wplywu | PRAWDZIWE | WAZNE -> WAZNE | BKROTPatch vs BannerKings (CallBannersGoal, MergeArmyGoal) | [IL] `k4army\army_il.txt:50654`; [K] BK `CallBannersGoal.cs:185-189, 313-324`. [P] brak licznika; `AIArmyFormation: true` | 'BKROT Self Call Fix' (call BKArmyManagementModel::CalculatePartyInfluenceCost). Tor L |
| AM-13 | 12 modeli BKROT bez BaseModel odcina War Sails: predkosc na morzu liczona wzorem ladowym | PRAWDZIWE | WAZNE -> WAZNE (predkosc), reszta DROBNE | BKROTPatch vs NavalDLC, ROT, RBL, BEE | [K] BR `BKROTPatch.cs:197-208` AddModel bez BaseModel; G `DefaultPartySpeedCalculatingModel.cs:225-228`; NV `NavalDLCPartySpeedCalculationModel.cs:55-62,163-212`. [P] ARM06:509 czynny BKROTPartyMoraleModel; flota AI 184 partio-godzin / 6 h (mediana) | 'Fix BKROT Model Chains' (Armoury, TAK): glowa lancucha z pominieciem RBL, najpierw predkosc. Tor L |
| AM-15 | BKROTCultureModel: przyjmowanie kultury i waga kultur = 0, asymilacja 1.0 kazdej kultury | PRAWDZIWE | WAZNE -> WAZNE | BKROTPatch vs BannerKings (CultureData, lojalnosc, stabilnosc) | [IL] `sk7\cm_il.txt` IL:51374, :51517; [K] BR `CultureDataClass_Assimilation_Setter_HealNaN_Patch.cs:6-16` (NaN -> 1), BK `BKLoyaltyModel.cs:216-219`. [P] TAB `ludnosc-bk.csv` 0 zmian kultury w 728 dobach | 'BKROT Culture Self Call Fix' (TAK) + osobny klucz na blogoslawienstwa; od nowej kampanii. Tor L |
| AM-20 | Petla kultury notabli: BK nawraca rzemieslnikow na kulture pana, CrashScribe co dobe cofa | PRAWDZIWE | WAZNE -> WAZNE | BannerKings.Redux + CrashScribe (nasz) | [K] BK `BKNotableBehavior.cs:98-124, 280-292`; ZB `CrashScribe\src\Mends.cs:5442-5487`. [P] LOG06: 675 Artisan + 24 Merchant u 71 notabli (Willamen 29 razy) | 'Local Levies: Preachers Only' (CrashScribe, TAK) - poprawka u nas. Tor M |
| AM-27 | Plik tytulow BKROT (titles_6_3.xml) z ROT 6.x: 25 twierdz bez tytulu BK, Lorath / Ibben / Qarth bez tytulu krolewskiego | PRAWDZIWE | WAZNE -> WAZNE | BKROTPatch, BannerKings.Redux, ROT-Map 8.1.8 | [K] `Modules\BKROTPatch\ModuleData\titles_6_3.xml:4,305,452-453`; BK `TitleGenerator.cs:320-323`; skrypt `SC1\k4\titles.py`. [P] LOG06:16405 'odciete 580000 razy' | 'BK Titles ROT 8.1.8' (CrashScribe, TAK): nasz plik `bk_titles_rot818.xml` + uzupelnienie w trwajacych kampaniach. Tor L (razem z AM-26) |
| AM-29 | ROT-Content: zerwane odwolania, m.in. spc_brotherhood_2 w lord_templates Westerlands - wywrotka przy zakladaniu rodu towarzysza | PRAWDZIWE | WAZNE -> WAZNE | ROT-Content (dane) + gra (CompanionRolesCampaignBehavior) | [P] RGL:7887, 7911-7928 'Null object reference'; [K] `ROT-Content\ModuleData\spcultures.xml:1929`, G `CompanionRolesCampaignBehavior.cs:363,382`, `DefaultHeroCreationModel:201-202` | 'Culture Template Repair' (Mends, TAK): odfiltrowac niegotowe wzorce przy kazdym wczytaniu + siatka na CreateNewHeroForNewCompanionClan. Tor S/M |
| AM-30 | BK workshops.xml: id przedmiotow zamiast kategorii (chicken, goose, carrot, orange) - linie bez wsadu | PRAWDZIWE | DROBNE -> WAZNE | BannerKings.Redux (dane) + gra + Armoury (WorkshopLaw, Gate200) | [K] `Modules\BannerKings.Redux\ModuleData\workshops.xml:79,87,590,599,630,638`; G `WorkshopType.cs:146-158`. [P] RGL:7857-7862; ARM06: rzeznicy ok. 32.9 miesa/d z niczego, piekarnie 59.1% ciast z samego zboza (skrypt `SC1\ws.py`) | (A) FreeRawLine na kazdy warsztat bez wsadu (klucz WorkshopNoFreeRaw), tor S; (B) 'BK Workshop Inputs Fix' (wsad 'apple' dla 2 linii piekarni), tor M z testem |
| AM-02 | BK nie wykrywa moda Diplomacy (ModCompat.DiplomacyMod = false) - dwie dyplomacje naraz | PRAWDZIWE | WAZNE -> DROBNE | BannerKings.Redux, Bannerlord.Diplomacy | [K] BK `BannerKings.Utils\ModCompat.cs:80,219`. [P] LOG06:838 czynny BKDiplomacyModel | Teraz nie przelaczac; poprawic docs; opcjonalnie 'BK Recognizes Diplomacy' (domyslnie NIE, decyzja Jeffa po tescie). Tor S |
| AM-03 | BKROT wylacza BK (rada, rycerze, polityka, waga glosu) dla rodow ROT bez lenna, takze w Westeros | PRAWDZIWE | WAZNE -> DROBNE | BKROTPatch (EssosiDailyTickSkip, EssosiCouncilSkip, VoteWeightSkip), BannerKings.Redux | [K] BR `EssosiClanUtil.cs:14,80-92`. [P] TAB `budzet-rodow.csv`: 0 -> 35 takich rodow w 2 lata; BKROTLOG:4347 '137 mod-added clans' | 'Keep BK Politics For Landless ROT Houses': zdjac 3 konkretne prefiksy; po naprawie lenn. Tor M, niski priorytet |
| AM-04 | BKROT polyka NullReference przy wyborze stolicy BK - Skagos bez stolicy przez cala kampanie | PRAWDZIWE | WAZNE -> DROBNE | BannerKings.Redux (BKCapitalBehavior), BKROTPatch (finalizer, titles_6_3.xml) | [K] BK `BKCapitalBehavior.cs:110-111`; BR `BKCapitalBehaviorGetIdealCapitalPatch.cs:43-60`. [P] BKFC + LOG06: 1 NRE na dobe (06:03:03.653, 06:03:14.331) | Znika po AM-27 (Driftwood Hall dostaje tytul); inaczej 'Fix BK Capital For Untitled Towns'. Tor S |
| AM-05 | Latki, ktore nie weszly przy starcie (BK 7 klas, BKROT 3 klasy + 1 reczna); pulapka Unpatch("*") | PRAWDZIWE | DROBNE -> DROBNE | BannerKings.Redux, BKROTPatch | [P] RGL:4412-4418; BKROTLOG:101,103,167,221; [K] BR `BKROTPatch.cs:148-159` | Bez latki; uzupelnic ERRORS.md A5; B10b dalej decyzja Jeffa. Tor S |
| AM-07 | Awaryjny wybor krola: BK liczy ocene z zamienionymi argumentami (remis -> najsilniejszy rod jednoglosnie) | PRAWDZIWE | DROBNE -> DROBNE | BannerKings.Redux, ROT, Diplomacy (abdykacja) | [K] BK `KingSelectionKingdomDecisionPatches.cs:35`, `BKTitleModel.cs:21`, `SuccessionRegistry.cs`. [P] brak takiej elekcji w logach | Najpierw pomiar elekcji; latka 'Fix Fallback King Election' tylko jesli pomiar pokaze trwale wyniki. Tor S (pomiar) |
| AM-08 | Spis metod gry latanych przez wiele modow: poza znanymi brak kolejnych kolizji | PRAWDZIWE (wniosek) | DROBNE -> DROBNE | BK, BKROT, ROT, RBM, DTE, Diplomacy, BEE, Spoils of War, Armoury, CrashScribe i inne | [K] spis `SC1\tools\inv.json`, `comb.txt`; 0Harmony 2.4.2 AffectsOriginal; [P] Diplomacy20261014.log 20/20 latek | Narzedzie 'Harmony Patch Census' (zrzut latek raz na sesje). Tor S/M |
| AM-09 | BKROTPartyWageModel odcina lancuch zoldu (ROT, NavalDLC, BEE): Inni i Nieskalani placa zold, brak obnizek na morzu | PRAWDZIWE | KRYTYCZNE -> DROBNE | BKROTPatch vs ROT, NavalDLC, BetterEconomy, BK | [K] BR `BKROTPatch.cs:204`, G `CampaignGameStarter.cs:71-81`, ROT `ROTPartyWageModel.cs:20-40`. [P] ARM06:293 czynny BKROTPartyWageModel; ECO rod Others zold 16521/d | 'Fix BKROT Wage Chain' (Armoury), dwa przelaczniki (regula ROT TAK, obnizka morska osobno). Tor L, niski priorytet |
| AM-14 | Rozdwojenie modeli: gra liczy na instancjach BK, BK na instancjach BKROT | PRAWDZIWE | WAZNE -> DROBNE | BKROTPatch vs BannerKings | [K] BK `Main.cs:189-227`; BR `BKROTPatch.cs:184-196`. [P] ARM06:319, LOG06:837-838 | Nie podmieniac instancji; po Self Call Fix opcjonalnie 'ROT Blessings In Game Models' (NIE). Tor S |
| AM-17 | StrategicCampaignAI zastepuje modele armii i celow, wola Default zamiast BaseModel | PRAWDZIWE | DROBNE -> DROBNE | StrategicCampaignAI145 vs BK, NavalDLC (ROT bez strat) | [IL] `il_sca\StrategicCampaignAI145.il:8679-8682, 5232-5428, 8216, 8514`; G `AiPatrollingBehavior.cs:257-268`, `DefaultTargetScoreCalculatingModel.cs:42-50` | 'Fix SCA Model Chains' (domyslnie NIE do testu). Tor L |
| AM-18 | BKROTMarriageModel.GetSpouseScore = 0 - kontrakt malzenski BK gracza bez czlonu pozycji | PRAWDZIWE (czesc o karawanach odrzucona) | DROBNE -> DROBNE | BKROTPatch vs BannerKings | [IL] IL:53066-53070; [K] BK `BKMarriageModel.cs:106-107`, `MarriageContract.cs:56` | 'BKROT Spouse Score Fix' w ramach Self Call Fix. Tor S |
| AM-19 | BKROT blogoslawienstwo Wielkiego Ksiecia: mnoznik zoldu -0.15 x liczba pozycji (ujemny zold) - tylko gracz | PRAWDZIWE | DROBNE -> DROBNE | BKROTPatch vs BannerKings (brak LimitMin) | [K] BR `BKROTPartyWageModel.cs:31-33`, G `TroopRoster.cs:46`, BK `BKReligionsBehavior.cs:579` (blogoslawienstwa tylko gracz) | 'Fix BKROT Wage Chain' albo 'Fix Princely Arbiter Wage': netto -15% + LimitMin(0). Tor S |
| AM-21 | Unikatowi towarzysze ROT znikaja i odradzaja sie (takze po smierci) | PRAWDZIWE | WAZNE -> DROBNE | gra (CompanionsCampaignBehavior) + ROT (ROTSpawnUniqueWanderers, patch9) | [K] G `CompanionsCampaignBehavior.cs:153-161,187-193,216-236`; ROT `ROTSpawnUniqueWanderers.cs:19,50-60`. [P] TAB `unikaty.log`:54-55, 105-108, 129-130, 462-463 | Juz w toku (wf_aed32ebf-821, decyzja Jeffa); przekazac uwagi sceptyka. Tor M |
| AM-23 | Licznik okupow AI-AI w linii 169c zawsze 0 - paczka 178 omija barter gry | PRAWDZIWE | DROBNE -> DROBNE (tylko log) | Armoury Measure169c + Armoury Ransom178 (nasze) | [K] ZB `Armoury\src\Measure169c.cs:93,163-197`, `Ransom178.cs:393-422`. [P] ARM06: 728/728 linii '0 na 0 zl' przy 2513 okupach | Kosmetyka logu pod kluczem 169c. Tor S |
| AM-24 | DTE daje konwojom 17 sztuk kazdego jedzenia z niczego; ksiega pisze '(nieznany)' | PRAWDZIWE (znane od m6b-5) | DROBNE -> DROBNE | Dynamic Troop Equipment Reupload + Armoury (GoodsLedger, PackSources) | [K] AZ `DTE\...\CutTheirSupplyBehavior.cs:76,736-751`. [P] ARM06 'miasta +17: (nieznany)' 3198 razy | Ramka ksiegi 'zapas drogi konwojow DTE' (log); potem 'DteConvoyFoodFromShelf' wedlug wzoru gry. Tor S / M, niski priorytet |
| AM-25 | Sprzatanie BKROT po pasowaniu rycerza pada na 'Ambiguous match' (GetCouncil) - dubel tego, co BK juz robi | PRAWDZIWE | DROBNE -> DROBNE (tylko log) | BKROTPatch + BannerKings.Redux | [K] BR `ClanActionsKnighthoodCleanupPatch.cs:119,152`; BK `ClanActions.cs:75-87`. [P] LOG06:9205-9290 (2 razy) | Filtr znanych pierwszych szans w CrashScribe. Tor S |
| AM-28 | BKROT uzywa starego id 'lorathi' (w ROT 8.1.8 'nord'): Lorath bez wiary BK, brak craftsman/noble w 9 miastach | PRAWDZIWE | WAZNE -> DROBNE | BKROTPatch (bk_civilians.xml, kod wiar) + ROT-Content + BK | [K] `spcultures.xml:9665-9667`; BR `ROTReligions.cs:153,174`, `CultureHelpers.cs:28`. [P] RGL:7926 'Null object reference ... lorathi' | 'Lorath Faith Fix' (postfiks IsCultureEssosi) + wzorce craftsman/noble w naszym XML. Tor M |
| AM-06 | BKROT nie czysci rady starego rodu, gdy rycerz zaklada rod | ODRZUCONE | DROBNE -> bez skutku (= AM-25) | BKROTPatch, BannerKings.Redux | BK sam czysci rade w CreateNewClan (`ClanActions.cs:75-91`) | Nie robic latki; filtr logu jak AM-25 |
| AM-16 | Latki BK nagrod bitwy: 'LootScale 0.5 nic nie robi, gracz ma 2x lupu' | ODRZUCONE | WAZNE -> DROBNE (zostaje tylko B10b) | BannerKings.Redux vs gra 1.4.8 | Postfiks lupu nakladany jako pierwszy i wchodzi (0Harmony `PatchClassProcessor.cs:176-234`, kolejnosc w IL BK) | Lupu nie ruszac (druga latka = 25% lupu); perki tylko jako B10b po decyzji Jeffa |
| AM-22 | Dworzanie BK (bk_courtiers_) jako biedne rody w 'Pokoj z biedy' | ODRZUCONE | WAZNE -> DROBNE (tylko liczniki CrashScribe) | BannerKings.Redux + CrashScribe PovertyPeace | Dworzanie nie glosuja (wplyw 0, pulap 50); LOG06: popierajacy = prawdziwe rody we wszystkich 7 decyzjach | Poprawic tylko liczniki logu; pomiar klanow dworzan |
| AM-31 | 'Martwe' ustawienia MCM: korupcja Diplomacy i spawn-size BK | ODRZUCONE (czesciowo prawdziwe) | DROBNE -> DROBNE (dokumentacja) + wazna korekta | Bannerlord.Diplomacy, BannerKings.Redux | Martwa jest tylko kara wplywu; DIPL `MaintainInfluenceBehavior.cs:16-51` sprzedaje lenna ZAWSZE | Zapisac w STAN-PRAC; NIE wlaczac EnableInfluenceBalancing; licznik zmian wlasciciela ByBarter |

## 3. Potwierdzone - szczegoly

### KRYTYCZNE

#### AM-26. BK wszczepia 30 ksiestw kalradyjskich przy kazdym wczytaniu zapisu
- **Mechanizm.** Domyslna sciezka tytulow BK to `Modules/BannerKings.Redux/ModuleData/titles.xml` (Calradia, BK `BannerKingsConfig.cs:99`).
  BKROT zmienia ja na plik ROT tylko w `OnNewGameCreated` (BR `BKROTPatch.Behaviors\BKROTBehavior.cs:37-54`); `OnGameLoaded` (:33-35) jest pusty,
  `UpdateTitles` (:57) nikt nie wola. Przy kazdym wczytaniu BK wola `TitleGenerator.RetrofitOrphanedTitles()` (BK `BKManagerBehavior.cs:159,194`),
  ktory czyta plik spod biezacej sciezki (`TitleGenerator.cs:337-389`), zaklada brakujace ksiestwa (:412) i odpina hrabstwa/baronie ROT od ich
  ksiestw (DetachFromOtherDuchies :436, :522). Krolestwa szuka po StringId, a vlandia/battania/sturgia/aserai/khuzait/empire* istnieja w ROT.
- **Dodatkowo (sceptyk):** nowe ksiestwo dostaje `SetSovereign(kingdomTitle)` rekurencyjnie (:461-465, `FeudalTitle.cs:476-488`) - Highgarden,
  Oldtown, Riverrun, Castle Black i inne dostaja Sovereign = tytul innego krolestwa. Psuje to `GetTitleFaction` (`TitleManager.cs:978-982`),
  `CalculateHeroSuzerain` (:336-354), prawa (`DefaultDemesneLaws.cs:126`) i OnClanDestroyed.
- **Dowod.** [P] `SC1\k4\savescan3.py`: IronmanPEh4X2ByGArW.sav (kampania Jeffa, 04.10) i IronmangeCUdegikFtb (30.08) - 30/33 ksiestw kalradyjskich,
  napisy 'Lord Paramount of Ardus Dunnath' (Halmon Paege, lord Dorzecza); autotest-dlugi.sav i autotest-2026-10-14-0932.sav (nigdy niewczytane) - 0/33.
  Brakuje 3 ksiestw 'nord', bo titles_6_3 nie ma tytulu krolestwa 'nord' (zgodne z retrofitem, wyklucza InitializeTitles z pliku kalradyjskiego).
  `SC1\k4\retro.py`: 119 tytulow ROT trafia pod 30 ksiestw, 84 w drzewo innego krolestwa (lekko zawyzone - liczy tez baronie w tym samym hrabstwie).
  W logach brak sladu (Logs.Kingdom tylko przy LogKingdomDecisions).
- **Skutek.** Szkoda powstaje przy PIERWSZYM wczytaniu kampanii w swiezo uruchomionej grze i zostaje w zapisie na zawsze. Wplyw na zamki jest
  posredni: po wymarciu rodu jego tytul przechodzi na de jure suzerena, czyli ksiecia kalradyjskiego, czesto z innego krolestwa
  (BK `BKTitleBehavior.cs:561-590`), a ten dostaje potem +400 (de jure) / +1000 (ConquestClaim) w ocenie kandydatow do lenna.
  Autotest tego nie widzi, bo nigdy nie wczytuje zapisu. Dotyczy 100% wczytywanych kampanii, takze nowej kampanii Jeffa.
- **Naprawa (wersja sceptyka).** CrashScribe Mends, klucz 'BK Titles Use ROT File' (TAK). Pliki BK i BKROT nietkniete.
  - (A) obowiazkowe: prefiks na `TitleGenerator.RetrofitOrphanedTitles` (public static) i `InitializeTitles` (internal static) albo postfiks na
    getterze `BannerKingsConfig.TitlesGeneratorPath` (innych czytelnikow nie ma, `TitleGenerator.cs:306,347`). Sciezka ROT tylko, gdy wczytany
    jest BKROTPatch i plik istnieje; regula jak BKROT (jest ROT_town31 -> titles_6_3, inaczej 6_2). Uzyc NASZEJ kopii z AM-27 (bez duplikatu
    ROT_castle47 - inaczej retrofit z plikiem ROT przerzuca go miedzy hrabstwami przy kazdym wczytaniu).
  - (B) opcjonalne (Jeff i tak zaczyna nowa kampanie): jednorazowa naprawa zapisu z flaga w SyncData - retrofit z plikiem ROT, potem
    `D.SetSovereign(K)` dla kazdego ksiestwa ROT (NIEZBEDNE, bez tego hrabstwa zostaja z obcym Sovereign), potem usuniecie 30 ksiestw kalradyjskich
    (Vassals, Sovereign, TitleManager.Titles przez AccessTools, RefreshCaches). Nieodwracalne - tylko z kopia zapisu. Tytulow przejetych juz po
    wymarciu rodow nie naprawimy automatycznie.
  - Linia logu: 'Tytuly BK: sciezka <plik>, przepiete N, suwereni poprawieni S, usuniete ksiestwa kalradyjskie M' (na czystej kampanii N = 0).
  - Test: nowa gra -> zapis -> wyjscie -> NOWY proces -> wczytanie -> zapis; savescan3 0/33; licznik tytulow z GetTitleFaction innym niz krolestwo
    z pliku ROT = 0. Autotest potrzebuje kroku "wczytanie w nowym procesie".

### WAZNE

#### AM-01. BKROT 'EarlyHeal' - reset dobrobytu ponizej 100 do 2000 / 4000
- [K] BR `BKROTPatch.Patches\BKROTProsperityHeal_Patch.cs:8` latka na getter `Town.Prosperity` (Priority 800), :25 warunek `num < 100 || NaN || Inf`,
  :28-32 `SetValue(IsCastle ? 2000 : 4000)`; to samo w `VanillaLoyaltyChange_PREFIX_Heal_Patch.cs:31-35` i `VanillaSecurityChange_PREFIX_Heal_Patch.cs:31-35`.
  BKROTLOG:41-46 SUCCESS. Gra przycina dobrobyt tylko ponizej 0 (G `Town.cs:119-125`); ani gra, ani BK nie dziela przez dobrobyt.
- [P] RGL:15716 'Willow Wood: Prosperity 97.42 -> 2000', :16481 'Pinkmaiden 99.03 -> 2000'; w 5 innych rgl_log 0 trafien, 0 przypadkow NaN.
  TAB `ludnosc-bk.csv`: Willow Wood traci ok. 4-5 dziennie (221 -> 101 w 27 dob), nazajutrz 2000. Tylko 2 skoki > 500 w 728 dobach.
- Skutek: zamek, ktory naprawde upada (glod, oblezenie, spustoszenie, nasze ScorchedEarth / Famine), dostaje +1900 z niczego, mniej wiecej do
  mediany zamkow; zamek stale tracacy wpada w petle (2000 -> 100 w ok. 450 dni -> 2000). Wyzwala sam odczyt Town.Prosperity, takze nasze tabele.
  Spokojny bieg: ok. 1 raz w roku, +2% sumy dobrobytu zamkow; czestosc rosnie z glodem i oblezeniami. Lamie "nic z niczego".
- Naprawa: Mends, 'Stop BKROT Prosperity Reset' (TAK). (1) Unhook 3 prefiksow (BKROTProsperityHeal_Patch.Prefix z Town.get_Prosperity,
  VanillaLoyaltyChange_PREFIX_Heal_Patch.Prefix z DefaultSettlementLoyaltyModel.CalculateLoyaltyChange, VanillaSecurityChange_PREFIX_Heal_Patch.Prefix
  z DefaultSettlementSecurityModel.CalculateSecurityChange) w tym samym miejscu co zdjecie lataczki stylow zycia (`Mends.cs:2850-2861`), PO
  OnSubModuleLoad BKROT; linia 'BKROT EarlyHeal zdjety n/3'. (2) BKROTLoyaltyHeal_Patch / BKROTSecurityHeal_Patch i latki POSTFIX zostaja (prog < 0,
  w praktyce lecza tylko NaN). (3) Zamiast latki na getterze - prefiks na `Town.set_Prosperity`: NaN/Inf -> zostaje stara wartosc + licznik;
  jednorazowy przeglad przy wczytaniu (NaN/Inf -> 0). (4) Sprawdzic w tabelach dobowych, czy nasze mechanizmy (cel kasy miasta 10000 + 12 x dobrobyt,
  ScorchedEarth, Famine) nie zakladaja dobrobytu >= 100. (5) Konsolowe przelaczniki BKROT (BkrotPatchToggle 'Heal') moga nalozyc latki ponownie -
  na koniec doby `Harmony.GetPatchInfo(Town.get_Prosperity)` i linia, jesli prefiks BKROT wrocil. Tor M.

#### AM-10. Prawowitosc krolestw = 0 (BKROTLegitimacyModel)
- [IL] `sk_legit\legit.il:52287-52290` (zainstalowany BKROTPatch.dll): `ldarg.0; ldarg.1; ldarg.2; callvirt LegitimacyModel::CalculateKingdomLegitimacy`
  - wywolanie wirtualne na sobie. Straznik (`GlobalRecursionGuard`, HashSet na watek) przy drugim wejsciu zwraca 0 (BR `BKROTLegitimacyModel.cs:21-27`).
  Wynik: 0 (+0.1 za kazde blogoslawienstwo ROT). Model bazowy BK daje zwykle 0.3-0.6. Podpiecie: BR `BKROTPatch.cs:190`; nikt go nie nadpisuje.
- [K] Legitimacy startuje od 0, cel 0, krok 0.01/dobe (BK `KingdomDiplomacy.cs:57,83-100,665`). Przy L = 0: pretendent PushScore 1.0, secesja >= 0.6
  (`DefaultRadicalGroups.cs:46-65`); grupa radykalna powstaje przy PushScore >= 0.4 (`BKInterestGroupsModel.cs:100`) - w czystym BK prawie nigdy,
  tu zawsze; +0.25 do szansy dolaczenia (:594); 'slabosc korony' +0.4 (`BKVassalPoliticsBehavior.cs:58`); srodek centralizmu -0.5 i zamknieta
  droga do Cesarstwa (`BKDiplomacyBehavior.cs:613,702`). Grupy radykalne wstrzymane w 1. roku (RadicalGroupYears = 1).
- **Obalone przez sceptyka:** 3 'Civil War Breaks Out' z LOG06 (06:45:23, 07:35:47, 08:15:06) to wojny domowe moda Diplomacy
  (`civil_war_strings_xml.xml:23-24`), nie BK - ten blad ich NIE napedza (Diplomacy nie czyta Legitimacy), nie napedza tez MakeRebelKingdom.
  Jedyny slad warstwy radykalnej BK: zadanie pretendenta w `session-2026-10-13_21-14-11.log:15115`.
- Skutek: staly, 100% krolestw (takze gracza); skutki w warstwie politycznej BK niezmierzone - stad WAZNE, nie KRYTYCZNE.
- Naprawa: 'BKROT Self Call Fix' (Mends, TAK): transpiler na `BKROTLegitimacyModel.CalculateKingdomLegitimacy` - dokladnie jedno
  `callvirt LegitimacyModel::CalculateKingdomLegitimacy(KingdomDiplomacy,bool)` po ldarg.0/1/2 -> niewirtualne `call BKLegitimacyModel::...`;
  inna liczba trafien niz 1 = nic nie zmieniac, 'BRAK' w logu. L rosnie 0.01/dobe, cel osiaga po 30-60 dobach (grupy wygasaja same).
  Kontrola: linia startowa 'transpiler 1/1'; dobowa linia L, cel i PushScore pretendenta / secesji dla wszystkich krolestw.

#### AM-11. Limit i zmiana wplywu = 0 w BK (BKROTInfluenceModel)
- [IL] IL:52047 i :52140 - callvirt `InfluenceModel::CalculateInfluenceCap` i `ClanPoliticsModel::CalculateInfluenceChange` na ldarg.0. Podpiecie
  tylko w BannerKingsConfig (BR `BKROTPatch.cs:189`). Dzienny przyrost wplywu gra liczy dobrze (BK `Main.cs:193` rejestruje oryginalny BKInfluenceModel).
- [P] LOG06:6709 'Ohoros ... wplyw 869, koszt wniosku 100' (CrashScribe `PovertyPeace.cs:1195` = GetInfluenceCostOfProposingPeace = 100 + 0.2 x limit),
  :12877 'koszt wniosku 75'. LOG06:838 - model dyplomacji gry to BKDiplomacyModel, wiec te koszty placi gra.
- Skutki: (1) wnioski o wojne/pokoj/aneksje/wygnanie/lenno kosztuja sama podstawe (normalnie +60..300) - AI latwiej przechodzi bramke
  (wiecej glosowan to wniosek, niezmierzony); (2) bramka armii 0.4 x limit (`BKArmyBehavior.cs:105`) martwa; (3) premia 'dodatkowe partie za
  wplyw' nigdy (`BKArmyManagementModel.cs:234-238`); (4) **pakty handlowe NIGDY** - `BKKingdomDecisionModel.cs:42-46` wymaga limitu >= 100
  (AI nie zawiera, gracz ma wyszarzone 'Propose Trade Pact'); (5) sukcesja gubi skladnik limit/3 (`SuccessionRegistry.cs:191,440`);
  (6) UI pokazuje limit 0; (7) drobne: rada, malzenstwa, religia, koszt tytulu 1000 na sztywno.
- Naprawa: ta sama latka (call `BKInfluenceModel::CalculateInfluenceCap` / `::CalculateInfluenceChange`), klucze straznika rozne, wiec zagniezdzone
  Cap z Change nie wraca do 0. Sondy: limit z BannerKingsConfig dla 3 rodow obok modelu gry (rowne, poza +15% PrincelyArbiter); koszt wniosku E1b > 100;
  pary z IsTradePactAllowed i zawarte pakty; wnioski AI i armie BK na 100 dob przed i po.
  **Ryzyko:** drozsze wnioski = mniej pokojow AI (wplyw na E1b i premie pokoju RBL); wroca pakty handlowe (kazdy -7.5% limitu wplywu krola,
  zwolnienie z cel karawan). Nie opisywac tego jako zmiany dziennego przyrostu wplywu.

#### AM-12. Wezwanie wasali i laczenie armii za 0 wplywu (BKROTArmyModel)
- [IL] `SC1\k4army\army_il.txt:50654` callvirt `ArmyManagementCalculationModel::CalculatePartyInfluenceCost` na ldarg.0 - zawsze 0 (mnozniki
  blogoslawienstw x0.75 nic nie zmieniaja). Podpiecie: BR `BKROTPatch.cs:185` (tylko BannerKingsConfig; model gry to SCA - armie z gry placa normalnie).
- [K] `CallBannersGoal.cs:185-189` (wasal z partia: 0), :144/:153 (opis 'Calling them will cost 0 influence'), :165 (gracz przy wplywie >= 0),
  DoAiDecision :313-324 (zawsze sie miesci), AddBanners :166 schodzi przez wasali wasali; `MergeArmyGoal.cs:129` (0; AI 5%/dobe).
  `AIArmyFormation: true` w `BannerKings.json:14`. Normalnie koszt x2 (ArmyPrivate) / x5 (ArmyLegion), w CallBanners x0.75.
- [P] brak licznika CallBanners; linie 507/947 z ARM06 niczego nie dowodza. Ograniczenia, ktore zostaja: >= 100 wplywu, CanCreateArmy, >= 2 wezwanych,
  przerwa 2 pory roku na lorda (`CallBannersGoal.cs:115`).
- Skutek: kazda armia BK to wszyscy gotowi wasale z tytulow bez zuzycia wplywu; wplyw krolow sie nie wyczerpuje. Gracz ma darmowe Call Banners i Merge Army.
- Naprawa: 'BKROT Self Call Fix' (call `BKArmyManagementModel::CalculatePartyInfluenceCost`, kotwica x1). Pomiar: postfiks na `CallBannersGoal.ApplyGoal`
  (armie BK na dobe, partie, pobrany wplyw, sredni koszt) + linia startowa z kosztem probnym krol-wasal (> 0).

#### AM-13. Modele BKROT odcinaja War Sails - predkosc floty na morzu
- [K] BR `BKROTPatch.cs:197-208`: 12 modeli przez `AddModel((GameModel)...)` - bez BaseModel (G `CampaignGameStarter.cs:71-81`), ostatnie na liscie, wiec czynne.
  BKROTPartySpeedModel wola wprost `DefaultPartySpeedCalculatingModel::CalculateFinalSpeed` (`SC1\bkrot_il.txt:54121`) - to nie samowywolanie,
  tylko pominiecie lancucha. Baza zawsze ladowa (G `DefaultPartySpeedCalculatingModel.cs:225-228`).
- Ginie z NV `NavalDLCPartySpeedCalculationModel.cs`: baza ze statkow i zalogi (:55-62), pelne morze +44.8%, rzeka +50%, plycizny, wiatr do +150%,
  sufit 10 (:163-212). Flota AI na otwartym morzu plynie ok. 1.5-2.5 raza wolniej niz zaklada DLC; na morzu liczy sie premia za jazde, a kary za
  brak zalogi i flote > 3 statki nie dzialaja.
- [P] ARM06:509 czynny BKROTPartyMoraleModel, :293 BKROTPartyWageModel (ten sam blok); :64 lancuch predkosci 6 modeli; partie na morzu w oknie 6 h:
  mediana 184, max 375 partio-godzin. Rozpiski predkosci w logu nie ma (0 linii 'Open Sea').
- **Poprawki sceptyka:** (1) model przed BKROT to RBL RealisticPartySpeedModel, ktory tez odcina Naval - "delegowanie do poprzedniego" nic by nie dalo;
  (2) RBL predkosc i morale juz nic nie robia (Mends wymusza SeasonsEnabled = false, `Mends.cs:3064-3067`), realna strata RBL to tylko leczenie
  (decyzja o balansie, nie naprawa); (3) teza 'AI zle ocenia czas przeprawy' niepotwierdzona. Wzrok, najazd, leczenie, morale partii i w bitwie -
  DROBNE (perki DLC, galiony, +30% wzroku na morzu).
- Naprawa: Armoury 'Fix BKROT Model Chains' (TAK, jak FoodChain). (1) Glowa lancucha: idac w dol listy od BKROT pomijac modele 'samotne' (podklasy
  Default bez BaseModel / _previousModel / _baseModel - RBL), brac pierwszy owijajacy (predkosc: ROTPartySpeedModel -> Naval -> BEE -> Default;
  reszta: NavalDLC*; morale w bitwie: NavalBattleMoraleModel); linia lancucha w logu. (2) W metodach zadeklarowanych w BKROT transpiler
  `call DefaultX::M` -> `ChainHead.M(this, ...)` (kod blogoslawienstw zostaje). (3) Metody niezadeklarowane w BKROT (CalculateBaseSpeed - konieczne):
  prefiks na Default z warunkiem `__instance is BKROT*`; UWAGA na cudze postfiksy (liczylyby sie 2 razy) - na CalculateBaseSpeed jest tylko nasz
  WorldPace (SpeedDepth); na metodach z postfiksami BK (`VanillaModelTweakPatches.cs` :932, :615, :601, :625, :729, :747, :884, :1042, :214, :224)
  NIE uzywac tego wariantu; sprawdzac GetPatchInfo na starcie. (4) RBL pominac. (5) Najpierw predkosc, potem wzrok. Sprawdzic SpeedDepth,
  SightRange, NightRest przy dluzszym lancuchu. (6) Kontrola dobowa: 3 partie na morzu i 1 na ladzie z rozpiska ('Open Sea', 'Wind', 'Shallow Draft';
  linie BK i BKROT raz; lad bez zmian). Tor L.

#### AM-15. Kultura BK stoi (BKROTCultureModel)
- [IL] `sk7\cm_il.txt` IL:51374, :51517 - callvirt `CultureModel::CalculateAcceptanceGain` / `::CalculateCultureWeight` na ldarg.0; straznik daje 0;
  BKCultureModel nie wykonuje sie nigdy. Podpiecie: BR `BKROTPatch.cs:184`.
- **Poprawka sceptyka:** nie ma 0/0 ani NaN w zapisie - `CultureDataClass::Tick` wola setter `set_assimilation`, a BKROT go lata
  (BR `CultureDataClass_Assimilation_Setter_HealNaN_Patch.cs:6-16`: NaN/Inf -> 1f; BKROTLOG:77-78 SUCCESS). Asymilacja = **1.0 dla kazdej kultury
  w kazdej osadzie**. Sonda NaN i naprawa NaN zbedne.
- Skutek: lojalnosc stale +4/dobe w kazdym miescie i zamku, takze swiezo zdobytym obcym (BK `BKLoyaltyModel.cs:216-219`, w zamysle -4..+4);
  stabilnosc +0.2 (`BKStabilityModel.cs:163`); bezpieczenstwo +1/dobe (`VanillaModelTweakPatches.cs:1140-1142`); kara za obca kulture pana
  nie dziala (log: lojalnosc srednio 85.2). Kultura osad NIGDY sie nie zmienia - [P] `ludnosc-bk.csv` (bieg 13.10, 728 dni, 798 osad):
  167 zmian krolestwa, 0 zmian kultury. Notable dostaja zawsze pierwsza kulture z listy. Blogoslawienstwa (Trios, GodsBlood, Harpy) dodaja +25/+50
  do wagi KAZDEJ kultury, wiec rozmywaja premie wlasciciela.
- Naprawa: 'BKROT Culture Self Call Fix' (TAK): transpiler callvirt -> call BKCultureModel::X (albo ReversePatch + doklejenie blogoslawienstw);
  osobny klucz: premie blogoslawienstw tylko dla `data.Culture == kultura wlasciciela`. Bez naprawy NaN. Sonda raz na sesje: 5 osad (2 zdobyte obce),
  Assimilation / Acceptance (teraz 1.0, po latce suma ok. 1.0). Linia dobowa: osady z asymilacja kultury pana < 0.5, zmiany kultury, srednia
  lojalnosc, bunty. **Wdrozenie od NOWEJ kampanii** - na starym zapisie czlon lojalnosci w zdobytych osadach spadnie z +4 do -4 w jedna dobe; razem
  z naprawianymi buntami BK grozi fala buntow. Autotest wl/wyl 1-2 lata. Sprawdzic kultury bez BasicTroop (drzewa rekrutow ROT; BK pomija null).

#### AM-20. Petla kultury notabli (BK nawraca, nasz CrashScribe cofa)
- [K] Jedyne miejsce nadajace notablowi kulture pana: BK `BKNotableBehavior.ApplyNotableCultureConversion` (:280-292; koszt 30-150 wplywu
  `BKCultureModel.cs:77-97`, relacja -8). Wolaja ja: HandleCultureConversions (:108-124, co dobe, FirstOrDefault, 5%, wplyw >= 1.5 x koszt,
  !IsEnemy, bez rodu gracza), rada PromoteCulture (`BKClanBehavior.cs:691-701`, bez progu, takze gracz), rozmowa gracza (:296).
  Nasz ZB `CrashScribe\src\Mends.cs:5442-5487` (LocalLevies, co dobe z `WarReport.cs:22/69`) ustawia `h.Culture = st.Culture` WSZYSTKIM notablom.
- [P] LOG06: 1368 linii 'Mends: X w Y byl A, jest B': Preacher 669 (jednorazowe sprzatanie kaplanow), Artisan 675 i Merchant 24 u 71 notabli.
  Dowod kierunku: Willamen w Harrenhal mial vlandia (Lannister) do 06:28:27; 06:28:17 'Harrenhal zdobyta przez Roose Bolton'; od 06:32:12 tylko battania.
  Willamen 29 nawrocen. Rzemieslnicze pary: reach->aserai 111, stormlands->dragonstone 85, valyrian->ghiscari 65, vlandia->river 51.
  Test 120 dob: petla 73 linie (ok. 0.6/dobe).
- **Poprawki sceptyka:** wyrzucanie ochotnikow prawie nie wystepuje (0 w 592 dobach - LocalRecruitsPrefix juz ustawia werbunek wedlug kultury
  osady); BK zatrzymuje sie przy relacji < -50 (HostileThreshold), wiec przy stalym panu ok. 7 nawrocen i jeden notabl na miasto stoi przy -50
  (blokujac reszte, bo FirstOrDefault); rod pana praktycznie nie werbuje u niego (`BKVolunteerModel.cs:54-56,214-218`: -5 w indeksie).
- Skutek: ok. 699 nawrocen x >= 30 wplywu = >= 21 tys. wplywu w 2 lata (ok. 630 ponad to, co BK zrobiloby samo); asymilacja przez notabli stoi;
  gracz z zadaniem rady PromoteCulture placi, dostaje 'converted', a nazajutrz jest jak bylo. Przyczyna w naszym kodzie.
- Naprawa (u zrodla, u nas): CrashScribe 'Local Levies: Preachers Only' (TAK, podlegly LocalRecruits). Krok 1 LocalLevies (zmiana kultury) tylko
  dla Occupation == Preacher i z pominieciem notabla, ktorego kultura == kultura glowy rodu pana; krok 2 (wyrzucanie obcych ochotnikow) dla
  wszystkich. BK nawraca kazdego notabla raz, zgodnie z projektem. Linia doby: kaplani poprawieni, notable pominieci jako nawroceni przez pana.
  Alternatywa (pytanie kierunkowe do Jeffa): notable zawsze miejscowi -> prefiks return false na ApplyNotableCultureConversion (wszystkie drogi;
  opcje rozmowy ukryc warunkiem). **Nigdy oba naraz.** Autotest 120 dob: 0 linii 'Mends: ... (Artisan|Merchant)' po starcie. Tor M.

#### AM-27. titles_6_3.xml nie pasuje do mapy ROT 8.1.8
- [K] BKROT ustawia `titles_6_3.xml` (jest ROT_town31; BR `BKROTBehavior.cs:42-49`); `autoGenerate="false"` (:4) - BK przerywa w
  `TitleGenerator.cs:320-323`, osada spoza pliku nie dostaje tytulu ani lordship wsi; RetrofitOrphanedTitles tylko podpina istniejace.
  Skrypt (`SC1\k4\titles.py`, powtorzony przez sceptyka) wobec `ROT-Map\ModuleData\settlements.xml` (97 miast, 130 zamkow):
  - 14 miast bez tytulu: Harrenhal ROT_town32, Seagard 33, Flint's Finger 34, Qarth 35, New Ghys 36, Qarkash 37, Ibben 38, New Ibbish 39, Omber 40,
    Driftwood Hall town_N1, Mormont Keep N2, Lonely Light N3, Pebbleton N4, Lordsport town_S2;
  - 11 zamkow bez tytulu: Qamayl, City of Bones, Highpoint, Widow's Watch, Ramsgate, Goldgrass, Nightfort, Hornfoot, The Rills, Redfort, Pinkmaiden;
  - 16 baronii wskazuje osady, ktorych nie ma (ROT_castle1-14, 43, 48); ROT_castle47 dwa razy (:452-453);
  - krolestwo `faction="lorath"` (:305), a Lorath w ROT to 'nord' -> BKROT podstawia 'Unknown Kingdom' (`TitleGeneratorNullFactionPatch.cs:63`),
    `GetSovereignTitle(nord)` = null; krolestw 'ibb' i 'qarth' brak; 4 ksiestwa (Barrowlands :51, Maidenpool :116, Gulltown :138, Celtigar :168)
    bez posiadacza de jure.
- [P] LOG06:16405 'relacje notabl-lord bez tytulu osady (Essos) - odciete 580000 razy' (LOG09:4598 - 88000); napis 'Essos' mylacy - brama obejmuje Westeros.
- **Poprawki sceptyka:** dzis brak tytulow NIE jest przyczyna zlego przydzialu (przy bledzie BKROT wynik 0 wszedzie); ConquestMight i ConquestDistributed
  wymagaja tylko tytulu krolewskiego (gina tylko w Lorath, Ibben, Qarth). Po FairFiefGrants realna strata: czlon J +100 (prawowity pan) na 25 twierdzach
  (m.in. czesto zdobywane Harrenhal, Seagard, Lordsport, Mormont Keep, Widow's Watch, Ramsgate). 3 krolestwa bez kontraktu BK (prawa podboju, rzad, sukcesja).
- Naprawa: CrashScribe 'BK Titles ROT 8.1.8' (TAK) - nasz plik `CrashScribe\ModuleData\bk_titles_rot818.xml` (kopia titles_6_3 z poprawkami:
  `faction="nord"` + `name="Lorath"`; nowe krolestwa ibb i qarth z ksiestwami; 14 miast i 11 zamkow dopietych wedlug wlasciciela z settlements.xml;
  usuniete baronie-widma; jeden ROT_castle47; deJure na istniejacych lordow). Sciezka - ten sam prefiks co AM-26 (BKROTBehavior nadpisuje
  sciezke w OnNewGameCreated, wiec ustawic ja raz nie wystarczy). Trwajace kampanie, jednorazowo z flaga: 'Unknown Kingdom' przepiac na Kingdom 'nord'
  (bez drugiego tytulu); CreateKingdom dla ibb i qarth; CreateLandedTitle dla 25 osad (sprawdzic OwnerClan i Leader != null); RetrofitOrphanedTitles.
  Log: 'twierdz bez tytulu przed X, po Y; krolestw bez tytulu krolewskiego przed A, po B'. Kontrola: Y = 0, B = 0, licznik EssosTitleGate prawie 0,
  czlon J w linii nadania przy tych 25. Kolejnosc: z naprawa lenn albo po niej. Tor L (razem z AM-26).

#### AM-29. ROT-Content - zepsuty wzorzec lorda Westerlands (wywrotka przy zakladaniu rodu towarzysza)
- [P] RGL:7887, 7911-7928 'Null object reference found' dla spc_brotherhood_2, spc_notable_reach_6, spc_yiti_headman_1, yiti_clansman,
  freehouses_recruit, wight_duel_preset_template. W 6 rgl_log brak 'creating hero from template with id: spc_brotherhood_2' - zdarzenia jeszcze nie bylo.
- [K] `ROT-Content\ModuleData\spcultures.xml:1929` - vlandia (Westerlands) ma spc_brotherhood_2 jako 1 z 12 wzorcow lorda; gra dodaje odwolanie bez
  sprawdzenia (`CultureObject.cs:427-433`), MBObjectManager zostawia w liscie obiekt zastepczy (Culture = null, BodyPropertyRange = null;
  `MBObjectManager.cs:141-151,1437-1458`). SpawnNewHeroesForNewCompanionClan losuje wzorzec DWA razy (G `CompanionRolesCampaignBehavior.cs:363,382`),
  wiec szansa ok. 16% (1 - (11/12)^2). Wynik: pewny NullReferenceException w `DefaultHeroCreationModel.GetStaticBodyProperties:201-202` w srodku
  ClanNameSelectionIsDone (:259-290), gdy towarzysz jest juz usuniety z rodu gracza - wywrotka albo pol-zalozony rod w zapisie.
  Sciezka czynna: gracz-wladca daje lenno (turn_companion_to_lord) albo rycerz BK gracza (`BKKnighthoodBehavior.cs:203`). Nikt tego nie lata.
- Reszta bez skutku albo kosmetyka (notable filtrowane po zawodzie, yiti_bandits BasicTroop nieuzywany, wight duel tylko w miescie kultury
  whitewalker - takiego nie ma, rebels_freehouses_template bez odwolan). Przemapowanie kultur przedmiotow ODRZUCONE (zmienia pule DTE, nagrody
  turniejow, warsztaty).
- Naprawa: Mends 'Culture Template Repair' (TAK), przy KAZDYM wczytaniu (OnSessionLaunched - listy nie sa w zapisie): w LordTemplates,
  RebelliousHeroTemplates i NotableTemplates kazdej kultury usunac wpisy null / !IsReady / BodyPropertyRange == null (nowa MBList przez setter
  prywatny; pusta lista = zostawic oryginal + linia w logu; oczekiwane vlandia lord 12 -> 11, reach notable 26 -> 25, yiti notable 26 -> 25).
  Siatka: prefiks na `CompanionRolesCampaignBehavior.CreateNewHeroForNewCompanionClan` podmienia niegotowy wzorzec na gotowy tej samej kultury.
  Opcjonalnie yiti_bandits.BasicTroop = yiti_criminal. Linia logu z licznikami; autotest sprawdza IsReady wszystkich wzorcow. Tor S/M.

#### AM-30. BK workshops.xml - wsad bez kategorii (mieso z niczego, ciasta z samego zboza)
- [K] `Modules\BannerKings.Redux\ModuleData\workshops.xml`: artisans chicken/goose x4 -> meat (:79, :87); bakery grain + carrot / orange -> 4 pie
  (:590, :599); butcher chicken / goose x2 (:630, :638). Gra przy braku kategorii tylko wypisuje komunikat i dodaje linie bez wsadu
  (G `WorkshopType.cs:146-158,181`); `_inputs` to wspolna lista. Kura i ges maja kategorie 'animal', carrot i orange to przedmioty kategorii 'apple'.
- [P] RGL:7857-7862 'Unable to Find Item Category: chicken/goose/carrot/orange'. Bilans z linii 'Towary' ARM06 (728 dni, skrypt `SC1\ws.py`):
  rzeznicy srednio 32.9 miesa dziennie z niczego (24-56; LOG09 39), ok. 5% miesa swiata; piekarnie: 191 436 z 323 692 ciast (59.1%) z samego zboza.
  Linia zboze + owoc prawie nie rusza (owocow brak na polkach miast).
- **Poprawka sceptyka:** linie ruszaja, bo nasza latka `WorkshopTrade.GateTranspiler` (Gate200, `WorkshopTrade.cs:229-256`) zastepuje prog 200;
  FreeRawLine (WorkshopNoFreeRaw) pilnuje tylko 'artisans' (`WorkshopLaw.cs:638-650`). Wpis 128 ('linie nie ruszaja') bledny.
- Naprawa: (A) rozszerzyc FreeRawLine na kazdy warsztat notabla z linia bez wsadu i wyrobem z FreeRawCats (ten sam klucz WorkshopNoFreeRaw);
  linii NIE usuwac z Productions (numery linii w zapisach i w oknie warsztatu); licznik 'rzeznicy bez wsadu zablokowani N'. Tor S.
  (B) 'BK Workshop Inputs Fix': raz na sesje dopisac wsad 'apple' do 2 linii piekarni (z ochrona przed podwojnym dodaniem). **Uwaga:** podaz ciast
  spadnie o ok. 55-60%, dopoki owoce nie trafiaja do miast (ciast brak juz w 91 miastach) - autotest z bilansem ciast, zboza i jedzenia miast;
  jesli braki rosna, klucz zostaje wylaczony i najpierw dowoz owocow. Tor M. (C) poprawic wpis 128 (`docs\paczki\w-toku\k13-2-rzemieslnicy-bk-z-wsadu.md:53`).

### DROBNE

#### AM-02. BK nie wykrywa Diplomacy (ModCompat.DiplomacyMod = false)
- [K] BK `BannerKings.Utils\ModCompat.cs:80` szuka modulu 'Diplomacy' / zestawu 'DiplomacyFixes'; :219 wymaga bezparametrowego GetModules, a w 1.4.8
  jest tylko `ModuleHelper.GetModules(Func cond = null)` (`SC1\mmdec\ModuleHelper.cs:178`); Id modulu 'Bannerlord.Diplomacy', zestaw
  'Bannerlord.Diplomacy.1.4.7', zaden DLL nie ma napisu 'DiplomacyFixes'. [P] LOG06:838 czynny BKDiplomacyModel; Diplomacy20261014.log:15,26 - 20 latek.
- Przy fladze false BK: wlasny model dyplomacji gry (`Main.cs:218-221`), cotygodniowe ConsiderAIDiplomacy (`BKDiplomacyBehavior.cs:473-479`),
  nacisk w glosowaniu o pokoj i prawa (`DiplomacyPatches.cs:354`, `KingdomPolicyDecisionPatches.cs:34`), prefiks odejscia najemnika (:120-138),
  podmiana glosujacych (`DetermineSupportersPatch.cs:13-35`).
- **Poprawki sceptyka:** podmiana glosujacych daje ten sam sklad co gra (Peerage CanVote, AddPeerage codziennie - `Peerage.cs:69-85`,
  `BKClanBehavior.cs:425-436`), wiec NIE wplywa na przyznawanie zamkow; ForceProposePeaceFromLosingSide (:459) i BKDeclareWarDecision nie zaleza od flagi;
  Diplomacy nie ma wlasnego DiplomacyModel (po przelaczeniu wrocilby Default z gry); projekt (PovertyPeace E1, audyty wojny) skalibrowano na obecnym
  stanie; zaden log nie pokazuje szkody ze wspolistnienia.
- Naprawa: teraz NIE przelaczac. Dokumentacja (rozdz. 6). Opcjonalnie CrashScribe 'BK Recognizes Diplomacy' (domyslnie NIE): w OnSubModuleLoad wpis
  do `ModCompat._cache` klucza 'Diplomacy|DiplomacyFixes' = true albo prefiks na `ModCompat.IsLoaded`; NIE postfiks na getterze (JIT moze go wkleic).
  Wlaczac tylko decyzja Jeffa po porownawczym tescie 120 dob (E7, E1, rozejmy); po wlaczeniu nie dziala postfiks BannerKings/400.

#### AM-03. BKROT wylacza BK dla rodow ROT bez lenna
- [K] BR `EssosiClanUtil.cs:14` wzorzec `^(ROTclan_|clan_[A-Z]{2})` - 137 rodow ROT (`spclans.xml` ROTclan_10..146), takze Westeros (Bracken,
  Vance, Dondarrion, Connington...); :80-92 pomija rod bez osady. `EssosiDailyTickSkipPatch.cs:12-23,60-68` (9 celow - BKBanditBehavior nie trafia),
  `EssosiCouncilSkipPatch.cs:22-30`, `BKKingdomDecisionVoteWeightSkipPatch.cs:40-48`. BKROTLOG:25-26, :116-119 SUCCESS.
- [P] TAB `budzet-rodow.csv`: 0 / 9 / 21 / 30 / 35 takich rodow w dobach 108836 / 109018 / 109200 / 109382 / 109563.
- **Poprawki sceptyka:** bunty BK, rycerze, dochod z majatkow, gentry, kariera najemnika, polityka wladcy, EstatePolicyAI - dla rodu bez osady i tak nic
  nie robia; 'mniej partii i armii' bez dowodu; brak wplywu na lenna (CanHaveFief nietkniete, waga glosu tylko w decyzjach BK, nie w SettlementClaimant);
  waga 1 nie oslabia (BK dalby 0.5). Realnie dla ok. 16 rodow z partiami: brak werbunku towarzyszy BK, BK nie dopycha do armii (JoinArmies :550),
  brak polityki wasali, rada sie nie odswieza. Liczba takich rodow spadnie po naprawie lenn.
- Naprawa (niski priorytet, po lennach): 'Keep BK Politics For Landless ROT Houses' - zdjac tylko EssosiDailyTickSkipPatch.Prefix z
  `BKClanBehavior.DailyClanTick` i `BKVassalPoliticsBehavior.OnDailyTickClan` oraz BKKingdomDecisionVoteWeightSkipPatch.Prefix. NIE latac
  `IsSkippableExpansionClan` (uzywa go tez LivingEconomyClanTickSkipPatch - wlaczylby tick BetterEconomy). Zmierzyc takt doby przed i po.

#### AM-04. Stolica BK - Skagos bez stolicy
- [K] BK `BKCapitalBehavior.cs:110-111` NRE przy title == null i istniejacym tytule suwerena; finalizer BR `BKCapitalBehaviorGetIdealCapitalPatch.cs:43-60`
  polyka kazdy wyjatek i zwraca null; proba codziennie (:84-90). Zrodlo nulla - AM-27.
- **Poprawki sceptyka:** licznik BKFC nie mierzy czestosci (BK loguje max 32 wpisy na 10 min, plik bez dat); NRE wypada raz na dobe rowno z dobami
  LOG06 (Day 2 06:03:03 / NRE 06:03:03.653; Day 3 06:03:14 / 06:03:14.331) = **jedno krolestwo: Skagos** (Stane, Driftwood Hall town_N1).
  Qarth i Ibben nie maja tytulu krolewskiego, wiec stolice dostaja normalnie. Skutek: Driftwood Hall bez +40% pulapu ludnosci, +0.2 zlota targu
  i dworzan BK; skladnik bezpieczenstwa stolicy to kara -1, wiec jego brak pomaga; nasz CapitalTreasury bierze InitialHomeSettlement.
  Ukryte ryzyko (0 w logach): ChangeCapital(item, null) w :94 przy krolestwie, ktore straci stolice, a trzyma miasto bez tytulu.
- Naprawa: **po AM-27 Driftwood Hall dostaje tytul, wiec przyczyna znika** (uwaga redaktora). Po wdrozeniu AM-27 sprawdzic, czy wpisy
  'GetIdealCapital_Patch1' w BKFC spadly do 0. Jesli nie (albo jako zabezpieczenie przed ChangeCapital(null)): 'Fix BK Capital For Untitled Towns' -
  prefiks Priority.High na GetIdealCapital z ta sama logika, bez kary -150 przy title == null i z `__result = null`, gdy krolestwo nie ma lenn;
  linia 'stolica BK ustawiona dla krolestw bez niej: N (nazwy)'. Tworzenie 25 tytulow - to AM-27, nie tu.

#### AM-05. Latki, ktore nie weszly przy starcie
- [P] RGL:4412-4418 - 7 linii '[BK] Skipped Harmony patches on' (TroopRosterSafetyPatch, Diag.TraceMbEventClanInvoke, AiDecisionTracePatches x3,
  DeclareWarVMPatch, BKBattleRewardTweakPatches); BKROTLOG:101,103,167 FAILED, :221 'SKIPPED: HeroCreator.CreateHeroAtOccupation method not found'.
  [K] 0Harmony `PatchClassProcessor.cs:189-198,321-351` (pierwszy blad przerywa reszte klasy).
- To powtorka ERRORS.md A5 / A7 / B10b. Nowe, bez skutku w grze: (1) trzecia klasa BKROT DynamicPartySizeBattleSkipPatch (moda nie ma);
  (2) reczna latka BKROT na CreateHeroAtOccupation (metody brak w 1.4.8; jej filtr 'ROT' bez znaczenia - 0 z 764 szablonow notabli ma 'ROT');
  (3) BR `BKROTPatch.cs:148-159` wola `harmony.Unpatch(metoda, Prefix)` z domyslnym harmonyID "*" - zdjelby prefiksy wszystkich modow, ale tylko
  nalozone PRZED BKROT (OnSubModuleLoad) i tylko gdy sama GRA przywroci te metode.
- Naprawa: bez kodu; uzupelnic ERRORS.md A5 (rozdz. 6); w linii kontrolnej po aktualizacji GRY sprawdzac 'SKIPPED: HeroCreator.CreateHeroAtOccupation'.
  B10b (perki CommanderWarband +25% wplywu, FamousSellswords +20% i Cataphract +12% slawy) - decyzja Jeffa; jesli TAK, tylko postfiksy z
  winnerParty / __0 i kontrola GetPatchInfo, nigdy ponowne CreateClassProcessor na klasie BK.

#### AM-07. Awaryjny wybor krola - zamienione argumenty w BK
- [K] BK `KingSelectionKingdomDecisionPatches.cs:35` wola `GetSuccessionHeirScore(king, clan.Leader, title)`, a sygnatura to (currentLeader, candidate)
  (`BKTitleModel.cs:21`). Wszystkie sukcesje wybierane (`SuccessionRegistry.cs`: Republic :281-297, FeudalElective :425-446, TribalElective :482-497,
  TheocraticElective :320-343) licza wynik z cech 'candidate' - po zamianie z cech GLOSUJACEGO, wiec dla danego glosujacego wynik jest staly =
  remis; DetermineSupportOption (G `KingdomDecision.cs:285`) bierze pierwszego z listy (najsilniejszy rod) -> jednoglosna FullyPush.
- **Poprawki sceptyka:** trojke wyznacza sila rodu (Take(3)), nie ocena; ROT x999 nie rozstrzyga (rod daje najwyzej 3 punkty, IsKingsVoteAllowed = false);
  krolestwa bez tytulu suwerena nie maja zamiany; skutek to 'wygrywa najsilniejszy rod', a nie przypadek - lagodniej niz lenna.
- Zakres: przy zwyklej smierci wladcy BK wstawia wlasna elekcje, ale gra robi natychmiast swoja (zamieniona) i daje tron chwilowemu wladcy - BK zastepuje
  go do doby pozniej (podwojna elekcja i podwojny koszt wplywu w 16 krolestwach z sukcesja wybierana). Trwale tylko: `SuccessionHelper.cs:72`,
  Diplomacy `AbdicationFaction.cs:52`, `ClanVariablesCampaignBehavior.cs:253`, abdykacja gracza. Ryzyko NRE w TheocraticElective (Qohor) przy
  glosujacym bez religii (:323-325). [P] w biegu dwuletnim 0 zmian wladcy w niebedacych republikami krolestwach wybieranych; 3 zmiany w srodku roku
  (Tyrosh 109297, Volantis 109381, Myr 109437) - przyczyna nieustalona.
- Naprawa: (1) pomiar w CrashScribe (tylko odczyt): KingdomDecisionAdded / Concluded dla dokladnego typu KingSelectionKingdomDecision - krolestwo,
  sukcesja BK, zrodlo z ramki stosu, trojka, poparcie kazdego rodu, zwyciezca, czy elekcja BK w 2 dni wybrala innego; plus miejsce wywolania przy
  kazdej zmianie rodu panujacego (wyjasni 3 zmiany w republikach). (2) 'Fix Fallback King Election' tylko przy trwalych wynikach w pomiarze -
  postfiks Priority.Last na CalculateMeritOfOutcomeForClan z poprawna kolejnoscia (prev = deJure albo kingdom.Leader) + czlony glosujacego jak w
  BKKingElectionDecision.DetermineSupport :59-94, try/catch z wzorem gry; x999 ROT nie ruszac. (3) Opcjonalnie osobny klucz: nie dodawac
  KingSelectionKingdomDecision, gdy czeka juz BKKingElectionDecision (prefiks Kingdom.AddDecision).

#### AM-08. Spis metod latanych przez wiele modow - brak kolejnych kolizji
- Spis z atrybutow i wywolan (SC1 `tools\inv.json`, `comb.txt`): 27 metod gry z latkami kilku modow. Sprawdzone zgodne: RecruitmentVM.OnDone (DTE void
  z __state dziala obok BK false), PartyHourlyAiTick (ROT kopiuje kod 1.4.8 bez `!IsCurrentlyAtSea` w warunku rabunku, AIThinkPatch.cs:102),
  ConsiderRansomPrisoner, TakePrisonerAction, set_Amount, GetXpFromHit, ItemModifier, DoLootInventory; BKPeaceProposalAmortizePatch (1 dzien na 7,
  rytm 14 dni zostaje); Diplomacy 20/20 latek; RBL, SCA, Dismember, NavalDLC bez Harmony. Uspiony NRE: ROT postfiks CreateNotable bez sprawdzenia null (0 w logach).
- **Poprawki sceptyka:** 'BK i BKROT dodaja skladniki w CalculateInfluenceGain / RenownGain' - falsz, BK tam nie dziala (B10b), dziala tylko BKROT (__0);
  ROTCalculateDamagePatch celuje w nieistniejaca w 1.4.8 `SandboxAgentApplyDamageModel.ComputeBlowDamageOnAgent` (Prepare = false), a BKROTLOG:181
  pisze 'SUCCESS' - premie obrazen z blogoslawienstw ROT nie dzialaja.
- Naprawa: bez latki na kolizje. Narzedzie CrashScribe 'Harmony Patch Census' (TAK, tylko log): raz na sesje po latkach BK (OnSessionLaunched)
  GetAllPatchedMethods + GetPatchInfo (wlasciciel, priorytet, rodzaj, czy prefiks zwraca bool); flagi: prefiks bool przed cudzym prefiksem / transpilerem
  oraz latki ogloszone jako dzialajace, ktorych nie ma (np. BKBattleRewardTweakPatches, ROTCalculateDamagePatch).

#### AM-09. Lancuch zoldu odciety przez BKROTPartyWageModel
- [K] BR `BKROTPatch.cs:204` AddModel bez poprzednika; `BKROTPartyWageModel.cs:14` dziedziczy po BKPartyWageModel. Kolejnosc LauncherData: BK 11, BEE 12,
  NavalDLC 13, ROT-Core 17, BKROTPatch 35. Gina: ROT `ROTPartyWageModel.cs:20-40` (Nieskalani 0, jednostki whitewalker = Tier, rod whitewalker 0),
  NV `NavalDLCPartyWageModel.cs:29-60` (morze -20%, konwoj -80%, PopularCaptain), bezpiecznik BEE. [P] ARM06:293; ECO rod Others: d60 zold 4010,
  d720 wojsko 5498 i zold 16521.
- **Poprawki sceptyka:** Umarli nie slabna - armia rosnie 616 -> 5498, dezercje z morale glownie w dobach 0-59, inwazje blokuje warunek osad
  ('brakuje im 25 osad za Murem'), ROT nie uzywa zlota Nocnego Krola; 0 garnizonow. Kasa pusta od ok. 90. doby. Realnie: Nieskalani placa pelny zold
  (kasa Zatoki Niewolniczej i Targaryenow, niezmierzone), brak obnizek na morzu. ROT moze pokazywac 'Game Model Error' przy wczytaniu (kosmetyka).
- Naprawa (niski priorytet): Armoury 'Fix BKROT Wage Chain' obok FoodChain. Po OnGameStart BKROTPatch znalezc instancje BKROTPartyWageModel i ostatni
  PartyWageModel przed nia (oczekiwany ROT; brak = latka nieczynna + log). Prefiks na GetTotalWage (zadeklarowany w BKROT): `__result = prev.GetTotalWage(...)`
  + blogoslawienstwo, return false. GetCharacterWage i GetTroopRecruitmentCost NIE sa zadeklarowane w BKROT - prefiks tylko przy
  `__instance.GetType() == typeof(BKROTPartyWageModel)` i z licznikiem zagniezdzenia (inaczej nieskonczona rekursja na dnie lancucha).
  Dwa przelaczniki: regula ROT (Inni, Nieskalani) TAK; obnizka NavalDLC na morzu osobno (zmienia zold calego swiata). MountedWage ma sie liczyc raz.
  Kontrola w ECO: rod Others zold 0. Tor L.

#### AM-14. Rozdwojenie modeli BK / BKROT
- [K] BK `Main.cs:189,193,207,208,222,227` rejestruje w grze instancje BK (w :222 osobna BKDiplomacyModel); BKROT (41. modul) podmienia potem tylko pola
  BannerKingsConfig (BR `BKROTPatch.cs:185-196`). [P] ARM06:319 'ROTClanFinanceModel -> BKClanFinanceModel'; LOG06:837-838.
- **Poprawki sceptyka:** blogoslawienstwa ma tylko gracz (AddBlessing tylko w rozmowie z kaplanem, `BKReligionsBehavior.cs:579`); karawany +20% bez skutku
  (RealisticCaravanIncome = true -> 0); budowa bez blogoslawienstw; zera w decyzjach BK to samowywolania (AM-10..12, AM-18). Zostaje: gracz z
  blogoslawienstwem nie dostaje w rachunku gry x1.3 wplywu dziennego, x1.15 limitu, x0.75 kosztu wezwania, x1.2 oceny malzonka.
- Naprawa: NIE podmieniac instancji ani BaseModel w opakowaniach. Po Self Call Fix opcjonalnie 'ROT Blessings In Game Models' (NIE do autotestu):
  postfiksy na BKInfluenceModel.CalculateInfluenceChange / Cap, BKArmyManagementModel.CalculatePartyInfluenceCost, BKMarriageModel.GetSpouseScore -
  tylko gdy `__instance` nie jest BKROT* i bohater ma blogoslawienstwo. Linia na sesje: typy w BannerKingsConfig, stan kluczy, probka dla gracza.

#### AM-17. StrategicCampaignAI zastepuje model armii i celow
- [IL] `il_sca\StrategicCampaignAI145.il:8679,8682` AddModel<T> (BaseModel ustawiony, ale nieuzywany); base w :5232, :5329, :5428, :8216, :8514 idzie
  wprost do Default. SCA nie nadpisuje CalculatePartyInfluenceCost.
- **Poprawki sceptyka:** ROT nic nie traci (filtr zaciagnietego gracza to martwy kod, AiMilitaryBehavior.cs:522). Szerzej niz zgloszono: koszt wplywu BK
  (x1.5 / x2 / x5) ginie dla WSZYSTKICH armii AI, takze skladanych przez CallBannersGoal (`Army.cs:921-925`) - armie AI o ok. 1/3 tansze;
  filtry BK CanLordCreateArmy (`BKArmyManagementModel.cs:116-211`) gina w sciezce gry; **AI nigdy nie wychodzi na zaczepne patrole morskie**
  (Default zwraca 0, `DefaultTargetScoreCalculatingModel.cs:42-50`; `AiPatrollingBehavior.cs:257-268`); patrole portow liczone wzorem ladowym;
  premia spojnosci Nord (`NavalDLCArmyManagementCalculationModel.cs:31-38`). 108 osad z portem; statki ma ok. 5% partii.
- Naprawa: 'Fix SCA Model Chains' (domyslnie NIE do testu). (a) Transpiler na SCA: `call Default*::X` -> pomocnik wolajacy X na BaseModel instancji SCA
  (null -> Default); w patrolach portow przy isTargetingPort oddac wynik BaseModel bez mnoznikow ladowych SCA. (b) Metody niezadeklarowane w SCA:
  prefiksy na Default tylko gdy typ to DOKLADNIE SCA (inaczej petla dla BK i Naval wolajacych base). (c) Zabezpieczenie przed zerem z BKROTArmyModel -
  to AM-12. (d) ROT bez zmian. (e) Linia dzienna: patrole zaczepne na porty, sredni koszt dolaczenia (lancuch vs Default), rody AI z ujemnym wplywem.
  Uwaga: singleton BannerKingsConfig przezywa drugi start kampanii w tym samym procesie (BK wpina wtedy BKROTArmyModel do lancucha). Tor L.

#### AM-18. Ocena malzonka = 0 w kontraktach BK (BKROTMarriageModel)
- [IL] IL:53066-53070 callvirt `MarriageModel::GetSpouseScore` na this; straznik zwraca 0, +20% za blogoslawienstwa nadal 0. Podpiecie BR `BKROTPatch.cs:186`.
  [K] BK `MarriageContract.cs:56` -> `BKMarriageModel.cs:106-107` (czlon 'Score differences' zawsze 0); UI `MarriageContractProposalVM.cs:627,639,662`.
- Dotyczy tylko propozycji malzenstwa GRACZA przez kontrakt BK (`BKMarriageBehavior.cs:335,347`); AI liczy na oryginalnym BKMarriageModel
  (ARM06:337 ROTMarriageModel). Gracz z nizszego rodu latwiej dostaje zgode, z wyzszego traci przewage.
- **Odrzucona czesc:** dochod z karawan 0 w rachunkach BK - metody nikt nie wola (G `DefaultClanFinanceModel.cs:881` wola model gry), a przy
  RealisticCaravanIncome = true BK i tak daje 0. Budowa - bez skutku (gra wola model gry, `BuildingHelper.cs:94`).
- Naprawa: w ramach Self Call Fix (klucz np. 'BKROT Spouse Score Fix', TAK): transpiler callvirt -> `call BKMarriageModel::GetSpouseScore(Hero,bool)`,
  kotwica x1. Karawany i budowa najwyzej z adnotacja 'bez skutku w grze'. Kontrola: w UI kontraktu wartosc malzonka > 0 i wiersz 'Score differences'.

#### AM-19. Blogoslawienstwo Wielkiego Ksiecia - ujemny zold (tylko gracz)
- [K] BR `BKROTPartyWageModel.cs:31-33` `AddFactor(-0.15 * MemberRoster.Count)` - Count to liczba pozycji, nie ludzi (G `TroopRoster.cs:46,61`);
  ExplainedNumber bez dolnego limitu; BK nie daje LimitMin (`BKPartyWageModel.cs:160-344`); ujemny zold staje sie dochodem rodu
  (G `DefaultClanFinanceModel.cs:820,941-950`); nasze SoldierPay i MountedWage nie przycinaja. Od ok. 7 pozycji zold ujemny.
- **Poprawka sceptyka:** AI tego blogoslawienstwa nie dostanie (AddBlessing tylko Hero.MainHero, `BKReligionsBehavior.cs:579`). [P] ECO: 0 ujemnych
  wierszy na 255 514 - z budowy kodu, nie z przypadku. Luka tylko dla gracza z wiara Princes Mercies (pieniadze od wlasnego wojska przez rok).
- Naprawa: 'Fix BKROT Wage Chain' albo osobny 'Fix Princely Arbiter Wage': postfiks na `BKROTPartyWageModel.GetTotalWage` - przy blogoslawienstwie i n > 1
  `AddFactor(0.15 * (n - 1))` (netto -15%, jak opis bostwa), na koncu `LimitMin(0)` dla kazdej partii; linia 'wpieta, dzis zadzialala 0/1'.

#### AM-21. Unikatowi towarzysze ROT znikaja i odradzaja sie
- **Juz w toku:** wf_aed32ebf-821 (galaz `w-toku/unikaty`, decyzja Jeffa 14.10 "jak jest martwy companion, to jest martwy"). Uwagi sceptyka trzeba
  przekazac temu workflow.
- [K] G `CompanionsCampaignBehavior.cs:216-236` (10%/dobe usuwa nienajetego wedrowca, ApplyByRemove), :187-193 (niespotkany od razu UnregisterDeadHero),
  :153-161 (zmarli po 40 dobach); ROT `ROTSpawnUniqueWanderers.cs:19,50-60` tworzy unikat od nowa, gdy nie ma go ani wsrod zywych, ani zmarlych
  (ROT chcial, zeby martwy zostal martwy - gra mu to psuje). [P] TAB `unikaty.log`: Varys 54-55, Brienne 105-108 (smierc: 'dziedzictwo po Brienne'),
  Sandor 129-130, Bronn 462-463 - kazdy wraca po 1 dobie.
- **Poprawki sceptyka:** TrySpawnNewCompanion gry zastepuje prefiks ROT patch9 (`OthersPatches.cs:183-199`, prog 0.6 x 98 miast = 58.8, pomija BK
  WorldCompanions) - 63 unikaty same zapelniaja prog, zwykli wedrowcy zanikaja (z kodu ok. 30 -> 12); 'bez poziomu i relacji' przesadzone
  (szablon ten sam, gracz nie widzi znikniecia w swojej osadzie); realne: najety unikat, ktory zginal, wraca po 40 dobach + kurczy sie pula zwyklych.
  Postfiks na TrySpawnNewCompanion nic nie da; prefiks na UnregisterDeadHero ryzykowny (wola go tez Hero.cs:1719).
- Naprawa (dla wf unikaty): 'Rot Unique Companions Persist' (TAK). (1) postfiks na `AddToAliveCompanions` - szablony 'ROTwanderer*' usuwac z
  `_aliveCompanionTemplates` (wszystkie 3 drogi dopisania); (2) postfiks na `InitializeCompanionTemplateList` - usunac je z `_companionsOfTemplates`;
  (3) lista zmarlych unikatow w zapisie (SaveText.Sync) + prefiks na `ROTSpawnUniqueWanderers.SpawnHero` (prywatna) zwraca false dla nich;
  NIE latac UnregisterDeadHero. Linia doby: unikaty zywe / w karczmie / najete / martwe, zablokowane wskrzeszenia, zwykli wedrowcy wobec 58.8.

#### AM-23. Licznik okupow AI-AI w linii 169c (tylko log)
- [K] ZB `Armoury\src\Measure169c.cs:93,163-197` liczy okup z BarterManager.ExecuteAiBarter; `Ransom178.cs:393-422` prowadzi okupy AI-AI wlasna droga
  i zwraca false (barter gry nie rusza; G `RansomOfferCampaignBehavior.cs:102-108`). [P] ARM06: 728/728 linii '(barter gry) dzis 0 na 0 zl';
  linie 178: AI-AI 2512 + kurier 1 = 2513 (= suma 'uwolnieni dzis: okup' z 169c; M6a-2 'sesja: okupy 2513').
- Poprawki sceptyka: zadne narzedzie (sprawdz_logi.py) tego pola nie czyta; pole jest podpisane '(barter gry)' i obok jest dobre 'uwolnieni dzis: okup'.
- Naprawa (kosmetyka, klucz 169c): (a) przy wlaczonym 178 wypisac 'nieczynny przy 178 - patrz linia Okupy (178)' albo (b) liczniki LastAiN / LastAiPrice
  zatrzaskiwane w finally Report (NIE czytac _dAiN - zerowane w ZeroDay przed linia 169c). Hook barteru zostaje na wypadek wylaczonego 178.

#### AM-24. Zywnosc z niczego dla konwojow DTE (znane od m6b-5)
- [K] AZ `DTE\DynamicTroopEquipmentReupload\CutTheirSupplyBehavior.cs:76` (17), :736-751 (17 sztuk kazdego IsFood + 17 sumpter_horse), :265-289,
  :798, :871-889 (RemoveParty). ZB `Armoury\src\PackSources.cs:170-186` liczy tylko konie. [P] ARM06 'miasta +17: (nieznany) +17' 3198 razy itd.;
  17 rodzajow jedzenia; w 711 z 728 dob '(nieznany)' = 17 x liczba konwojow z linii m6b. 945 konwojow, ok. 273 tys. sztuk.
- **Poprawki sceptyka:** ok. 93% znika przy CleanupCaravan; do swiata ok. 7% (ok. 1.5 szt./dobe na rodzaj) - obsada, lup, sprzedaz na polki
  (G `CaravansCampaignBehavior.cs:691-718`). Opisane juz w CHANGELOG m6b-5 (13.10); nowe jest tylko '(nieznany)' w K4.
- Naprawa: krok 1 (log, bez klucza) - ramka GoodsLedger 'zapas drogi konwojow DTE' wokol AddReinforcementSupplies. Krok 2 z pierwotnej tresci
  (17 x 17 z polki) ODRZUCONY - zrobilby z zera ujscie ok. 375 szt./dobe z polek miast. Zamiast tego 'DteConvoyFoodFromShelf': zdjac dopisana zywnosc,
  racje wedlug wzoru gry (ludzie/20 na dobe x doby drogi + 2) z polki miasta zrodla za zloto rodu, resztki przy dostawie do celu / zrodla. Niski priorytet.

#### AM-25. 'Ambiguous match' w sprzataniu BKROT po pasowaniu rycerza (tylko log)
- [K] BR `ClanActionsKnighthoodCleanupPatch.cs:119` `AccessTools.Method(typ, "GetCouncil")` bez typow; BK ma GetCouncil(Hero) i GetCouncil(Clan)
  (`CourtManager.cs:163,177`); lapane w :152. BK sam czysci rade i role w CreateNewClan (`ClanActions.cs:75-87, 92-120`), wiec postfiks BKROT to dubel.
  [P] LOG06:9205-9290 (07:22:07, Year 300 Day 4), w podsumowaniu 2 x; LOG09 - 0.
- Poprawka sceptyka: Finalizer BKROT (:206) to oslona przed wywrotka (BK TryCatch lapie tylko NRE) - nie szkodzi; Unpatch calej latki bylby gorszy.
- Naprawa: CrashScribe - wzor do znanych niegroznych pierwszych szans (AmbiguousMatchException ze stosu ClanActionsKnighthoodCleanupPatch.ClearCouncilPositions),
  liczony jako 'odfiltrowane'. Opcjonalnie (domyslnie NIE): zdjac sam Postfix BKROT z ClanActions.CreateNewClan, Prefix i Finalizer zostaja.

#### AM-28. Lorath: stare id 'lorathi' w BKROT
- [K] `ROT-Content\ModuleData\spcultures.xml:9665-9667` kultura 'nord' = Lorathi; 'lorathi' nie istnieje. `bk_civilians.xml`: 19 wzorcow Culture.lorathi.
  BR `ROTReligions.cs:153,174` (Pattern z lista {null}), `CultureHelpers.cs:28` (IsCultureEssosi bez 'nord'). [P] RGL:7926.
- Prawdziwe: zadna wiara BKROT nie uznaje 'nord' za swoja, wiec osady Lorath z panem kultury nord nie maja wiary BK ani kaplanow (na starcie
  0 linii Mends o kaplanie w osadzie Lorath), lordowie nord nie dostaja wiary; w 9 miastach (Lorath, Tolero Aeksio, 3 Qarthu, 3 Ibbenu, Castle Black)
  brak 40% wedrowcow BK (craftsman_ / noble_; villager_ jest).
- **Obalone:** gentry - prawdziwa blokada to wspolne pule nazw rodow (rozdz. 7); 'wiara Braavos' - skutek podboju (108980 Lorath -> Antaryon);
  kaplani Lorath w obcych osadach - Pattern uznaje wszystkie kultury Essos; BeardedGodFaith to martwa klasa.
- Naprawa: Mends 'Lorath Faith Fix': postfiks na `BKROTPatch.Helpers.CultureHelpers.IsCultureEssosi` (nord -> true); null w Pattern.FavoredCultures -> nord;
  jednorazowo InitializeHeroFaith dla lordow nord bez religii. Nasz XML: tylko craftsman_ i noble_ dla nord, ibbenese, qartheen, nightswatch.
  Gentry_nord i ksiegarzy NIE dodawac. Linia logu 'Lorath Faith Fix: osady nord z wiara N/M, lordowie N/M'. Tor M.

## 4. Odrzucone przez sceptykow

| Nr | Teza | Dlaczego odrzucone | Co zostaje |
|---|---|---|---|
| AM-06 | Bohater-rycerz zostaje w radzie starego rodu (BKROT AmbiguousMatch na GetCouncil) | BK sam czysci rade w CreateNewClan (`ClanActions.cs:75-91`, przeciazenie wybiera kompilator); postfiks BKROT dziala po oryginale i nie mialby czego czyscic | Tylko wpis 'ERROR CAUGHT FURTHER UP' - patrz AM-25 (filtr logu). Latki 'Clean Council On Knight Clan' NIE robic |
| AM-16 | LootScale 0.5 nie dziala, gracz ma 2x lupu | Harmony naklada zadania klasy po kolei; postfiks lupu (character, targetValue - poprawne nazwy w 1.4.8) jest pierwszy w metadanych i wchodzi; blad dopiero na CalculateInfluenceGain (session :723-777, RGL:4418) | Martwe perki BK = B10b (ERRORS.md:132,135), decyzja Jeffa. Proponowana latka zrobilaby 25% lupu (dwa losowania po 0.5) |
| AM-22 | Dworzanie BK przeciagaja male krolestwa na pokoj z biedy | Dworzanie nie glosuja: wplyw 0, pulap 50 i -50% za nizsze parostwo, a glos wymaga ok. 29 wplywu (G `KingdomDecision.cs:306-345,431-447`); we wszystkich 7 decyzjach LOG06 liczba popierajacych = liczba prawdziwych rodow; bieda skarbca 0.83-1.00 | Szum w licznikach CrashScribe PovertyPeace (pominac 'bk_courtiers_' w Update, Flip, 'glosujacych'); klanow dworzan przybywa (3 -> 47) - najpierw linia pomiaru |
| AM-31 | Korupcja Diplomacy i spawn-size BK to martwe ustawienia | Prawda tylko czesciowo: martwa jest kara wplywu za korupcje, zanik > 1000 i limit -20/d (`DefaultClanPoliticsModelPatch.cs:27-30`), spawn-size BK nie ma kodu. FALSZ: DIPL `MaintainInfluenceBehavior.cs:16,23-51` rejestrowany bez warunku (`SubModule.cs:106`) - rod AI z twierdzami > tier i przyrostem wplywu <= 0 sprzedaje najbiedniejsza twierdze innemu rodowi krolestwa (ApplyByBarter). Teza 'jedynym hamulcem lenn jest limit BK' falszywa; BK ma tez wlasna kare 'Clan Influence Limit' (`BKInfluenceModel.cs:192-196`) | Zapisac w STAN-PRAC; NIE wlaczac EnableInfluenceBalancing (wiecej wymuszonych sprzedazy); licznik zmian wlasciciela twierdz wedlug ChangeOwnerOfSettlementDetail (ByBarter z nazwami rodow) przy weryfikacji lenn. Zwiazek z FairFiefGrants - rozdz. 7 [R] |

## 5. Plan napraw

Zasady: kazda naprawa = nasza latka pod wlasnym kluczem MCM, pliki BK / BKROT / ROT / Diplomacy / DTE nietkniete; jedna rzecz naraz, autotest przed
wgraniem; linia kontrolna w logu przy kazdej. Nic z tego nie jest jeszcze zrobione. Miejsce w kolejce: po biezacych pracach (bitwa szturmu
wf_f6682868-53f, unikaty wf_aed32ebf-821), przed nowa kampania Jeffa ("najpierw wszystko").

| Pakiet | Znaleziska | Klucze MCM (domyslnie) | Tor / czas | Kontrola | Zaleznosci i ryzyko |
|---|---|---|---|---|---|
| P1 Tytuly BK | AM-26 (A obowiazkowe, B opcjonalne), AM-27, sprawdzenie AM-04 | 'BK Titles Use ROT File' (TAK), 'BK Titles ROT 8.1.8' (TAK) | L / godziny | savescan3 0/33 po wczytaniu w nowym procesie; twierdz bez tytulu 0; krolestw bez tytulu 0; BKFC 'GetIdealCapital_Patch1' 0 | Razem z nowa zasada lenn (czlon J +100). Autotest musi dostac krok zapis -> nowy proces -> wczytanie |
| P2 BKROT Self Call Fix | AM-10, AM-11, AM-12, AM-15, AM-18 | jeden transpiler, osobny klucz na model (TAK); AM-15 drugi klucz: blogoslawienstwa tylko dla kultury pana | L / godziny + test 120 dob wl/wyl | linia 'transpiler 1/1' na metode; L i cel krolestw; koszt wniosku E1b > 100; pakty handlowe; koszt wezwania wasala > 0; asymilacja i zmiany kultury; lojalnosc i bunty | Od NOWEJ kampanii (AM-15: skok lojalnosci). Wplyw na E1b, premie pokoju RBL, bunty BK (S9 / S11). Zmienia polityke swiata - zapisac w CHANGELOG jako zmiane ekonomii / polityki |
| P3a Petla kultury notabli | AM-20 | 'Local Levies: Preachers Only' (TAK) | M / ok. 1 h | 0 linii 'Mends: ...' dla rzemieslnikow i kupcow (Artisan, Merchant) w 120 dobach | Nasz kod; pytanie kierunkowe do Jeffa (raz nawroceni czy zawsze miejscowi) - domyslnie jak BK |
| P3b Wzorce ROT | AM-29 | 'Culture Template Repair' (TAK) | S-M / do 1 h | liczniki usunietych wzorcow; IsReady wszystkich wzorcow | Bezpieczne (lista budowana co wczytanie) |
| P3c Reset dobrobytu | AM-01 | 'Stop BKROT Prosperity Reset' (TAK) | M / ok. 1 h | 'BKROT EarlyHeal zdjety 3/3'; licznik NaN; GetPatchInfo na koniec doby | Sprawdzic nasze mechanizmy przy dobrobycie < 100 |
| P3d Warsztaty BK | AM-30 (A, potem B) | WorkshopNoFreeRaw (istniejacy), 'BK Workshop Inputs Fix' (po tescie) | A: S; B: M | 'rzeznicy bez wsadu zablokowani N'; bilans ciast, zboza i jedzenia miast | B obniza podaz ciast o 55-60% - bez dowozu owocow zostaje wylaczony |
| P4 Lancuchy modeli | AM-13 (najpierw predkosc), AM-09 + AM-19, AM-17 | 'Fix BKROT Model Chains' (TAK), 'Fix BKROT Wage Chain' (regula ROT TAK, obnizka morska osobno), 'Fix SCA Model Chains' (NIE do testu) | L / godziny | rozpiska predkosci 3 partii na morzu; zold rodu Others 0; patrole zaczepne na porty | Rekursja na metodach wspolnych (straznik typu); cudze postfiksy na Default; zold swiata |
| P5 Drobne | AM-07 (pomiar), AM-08 (census), AM-23, AM-24 (krok 1), AM-25, AM-28, AM-03, AM-14 (opcja) | wedlug opisow w rozdz. 3 | S, AM-28 i AM-03 M | wedlug opisow | AM-03 po naprawie lenn; AM-14 po P2 |
| P6 Dokumenty | AM-02, AM-05, AM-30 (wpis 128), AM-31 | - | S | - | rozdz. 6 |

Decyzje Jeffa (tylko kierunkowe): B10b (perki BK z bitew) - otwarte jak dotad; AM-02 'BK Recognizes Diplomacy' - na razie NIE przelaczamy;
AM-20 - rzemieslnicy raz przejmuja kulture pana (BK) czy zawsze miejscowi.

## 6. Poprawki dokumentacji (bez kodu)

- `docs/ERRORS.md:131` (AM-02): pelna lista skutkow flagi DiplomacyMod = false (model gry BKDiplomacyModel `Main.cs:218-221`, ConsiderAIDiplomacy
  `BKDiplomacyBehavior.cs:473-479`, nacisk BK w glosowaniu o pokoj i prawa, prefiks odejscia najemnika, DetermineSupporters podmienione ze skladem jak w grze)
  oraz czego flaga NIE wylacza (ForceProposePeaceFromLosingSide, BKDeclareWarDecision).
- `docs/lenna/ROZPOZNANIE-LENNA.md` kr. 6 (galaz `w-toku/zbior-e8`): przypis - glosujacych wybiera latka BK, sklad taki sam jak w grze
  (`Peerage.cs:69-85`, `BKClanBehavior.cs:425-436`); rozdz. 8 pkt 4 - wynik tego audytu dla kazdej metody z listy samowywolan (rozdz. 7 [R] dla Loyalty).
- `docs/ERRORS.md` A5 (AM-05): trzecia klasa BKROT DynamicPartySizeBattleSkipPatch; reczna latka CreateHeroAtOccupation ('SKIPPED', filtr 'ROT' bez znaczenia);
  uwaga o `Unpatch(Prefix, "*")` w OnSubModuleLoad BKROT.
- `docs/paczki/w-toku/k13-2-rzemieslnicy-bk-z-wsadu.md:53` (AM-30): linie kur i gesi u rzeznika RUSZAJA (Gate200), ok. 33 miesa dziennie z niczego.
- `docs/STAN-PRAC.md` (AM-31): w Diplomacy martwa jest tylko kara wplywu za korupcje; ReduceCorruption sprzedaje twierdze zawsze; spawn-size BK bez kodu.
- Opis AM-08 w przyszlych audytach: 'CalculateInfluenceGain / RenownGain - BK nie dziala (party zamiast winnerParty), dziala tylko BKROT (__0)'.

## 7. Nowe tropy i luki (NIE sprawdzone osobnym sceptykiem)

- **[R] Prog korupcji Diplomacy wobec pulapu FairFiefGrants.** DIPL `Diplomacy.Extensions\ClanExtensions.cs:12-22`: korupcja > 0, gdy liczba twierdz
  (GetPermanentFiefs) > tier; `MaintainInfluenceBehavior.cs:30-50`: wtedy, przy przyroscie wplywu z modelu gry <= 0, sprzedaz najbiedniejszej twierdzy
  rodowi tego krolestwa z mniej twierdzami niz tier (bez gracza i najemnikow). FairFiefGrants (ZB `Armoury\src\FiefGrants.cs:30-36,258-259`):
  pulap P + FairFiefCapExtra = max(1, tier - 1) + 2, liczony miasto 2 / zamek 1 - rod z samymi zamkami moze dostac tier + 1 twierdz, czyli wpasc w
  korupcje Diplomacy. Kod sprawdzony; czestosc nieznana. Do autotestu lenn: licznik zmian wlasciciela ByBarter miedzy rodami AI. Jesli czeste -
  albo pulap FairFiefGrants liczony w twierdzach <= tier, albo prefiks na ReduceCorruption pod kluczem MCM.
- **[R] BKROTLoyaltyModel** (z listy samowywolan ROZPOZNANIE-LENNA rozdz. 8 pkt 4): IL:52540 callvirt na this, ale modelu nie podpina ani BannerKingsConfig,
  ani AddModel (BR `BKROTPatch.cs:184-208`); ARM06:303 czynny BKLoyaltyModel. Bez skutku.
- Nie sprawdzone: pozostale modele z bloku AddModel BKROT - BKROTDeathModel, BKROTDisguiseDetectionModel, BKROTPersuasionModel, BKROTPregnancyModel
  (ciaze obsluguje nasz CR), **BKROTPriceModel** - czy odcinaja lancuch ROT / NavalDLC / BEE tak jak AM-09 i AM-13. Sprawdzic przy P4.
- Gentry BK nie powstaje w ok. 20 krolestwach (Braavos, Lys, Myr, Tyrosh, Norvos, Qohor, Volantis, Reach, Riverlands, Stormlands, Dragonstone, Lorath,
  Ibben, Qarth, Nights Watch): pule nazw rodow sa wspolne dla wielu kultur, a gra odrzuca nazwe noszona przez jakikolwiek rod (G `FactionHelper.cs:112`,
  BK ClanActions.GetRandomAvailableName). TAB `budzet-rodow.csv`: gentry tylko w 9 krolestwach (88). Wedlug sceptyka AM-28 wiekszy skutek niz cale
  AM-28. Propozycja: postfiks na GetRandomAvailableName - pierwsza nazwa wolna w calej grze, zapas: nazwa osady + kultura.
- ROTCalculateDamagePatch martwy mimo 'SUCCESS' w BKROTLOG:181 - premie obrazen z blogoslawienstw ROT (tylko gracz) nie dzialaja.
- Wojny domowe w Essos to Diplomacy (RebelFactionScoringModel), nie BK - osobne rozpoznanie; szanse buntow Diplomacy juz zmniejszone (S10, 14.10).
- Ryzyka uspione (0 w logach): ChangeCapital(item, null) w BK (AM-04); NRE w TheocraticElective (AM-07); ROT postfiks CreateNotable bez null (AM-08);
  ROT ValidateGameModel 'Game Model Error' przy wczytaniu (AM-09, kosmetyka).
- Kaplani BK z kultura 'lorathi' (bannerkings_preacher_lorathi_pattern_1, 36 razy w RGL) - kultura-widmo; Mends prostuje, AM-28 / AM-29 pkt 4.
- Patch9 ROT pomija ustawienie BK World Companions (98) i zostawia 0.6 (58.8) - decyzja projektowa, mozna zostawic.

## 8. Lista potwierdzonych (do przekazania)

| Nr | Tytul | Waga | Naprawa |
|---|---|---|---|
| AM-26 | Kalradyjskie ksiestwa BK wszczepiane przy wczytaniu zapisu | KRYTYCZNE | 'BK Titles Use ROT File' (P1) |
| AM-01 | BKROT EarlyHeal: dobrobyt < 100 -> 2000 / 4000 | WAZNE | 'Stop BKROT Prosperity Reset' (P3c) |
| AM-10 | Prawowitosc krolestw 0 (BKROTLegitimacyModel) | WAZNE | 'BKROT Self Call Fix' (P2) |
| AM-11 | Limit i zmiana wplywu 0 w BK (BKROTInfluenceModel) | WAZNE | 'BKROT Self Call Fix' (P2) |
| AM-12 | Call Banners i laczenie armii za 0 wplywu (BKROTArmyModel) | WAZNE | 'BKROT Self Call Fix' (P2) |
| AM-13 | Predkosc floty na morzu wzorem ladowym (modele BKROT bez BaseModel) | WAZNE | 'Fix BKROT Model Chains' (P4) |
| AM-15 | Kultura BK stoi, asymilacja 1.0, lojalnosc +4/d (BKROTCultureModel) | WAZNE | 'BKROT Culture Self Call Fix' + klucz blogoslawienstw (P2) |
| AM-20 | Petla kultury notabli BK / CrashScribe | WAZNE | 'Local Levies: Preachers Only' (P3a) |
| AM-27 | titles_6_3.xml: 25 twierdz i 3 krolestwa bez tytulu BK | WAZNE | 'BK Titles ROT 8.1.8' (P1) |
| AM-29 | Zepsuty wzorzec lorda Westerlands - wywrotka przy rodzie towarzysza | WAZNE | 'Culture Template Repair' (P3b) |
| AM-30 | BK workshops.xml: mieso z niczego, ciasta z samego zboza | WAZNE | FreeRawLine na wszystkie warsztaty; 'BK Workshop Inputs Fix' po tescie (P3d) |
| AM-02 | BK nie wykrywa Diplomacy | DROBNE | dokumentacja; opcja 'BK Recognizes Diplomacy' (NIE) |
| AM-03 | BK wylaczony dla rodow ROT bez lenna | DROBNE | 'Keep BK Politics For Landless ROT Houses', po lennach |
| AM-04 | Skagos bez stolicy BK | DROBNE | znika po AM-27; inaczej 'Fix BK Capital For Untitled Towns' |
| AM-05 | Latki nieudane przy starcie, Unpatch("*") | DROBNE | dokumentacja ERRORS A5; B10b decyzja Jeffa |
| AM-07 | Awaryjny wybor krola - zamienione argumenty | DROBNE | pomiar; 'Fix Fallback King Election' warunkowo |
| AM-08 | Spis latek - brak kolejnych kolizji | DROBNE | 'Harmony Patch Census' (log) |
| AM-09 | Lancuch zoldu odciety (ROT, NavalDLC, BEE) | DROBNE | 'Fix BKROT Wage Chain' |
| AM-14 | Rozdwojenie modeli BK / BKROT | DROBNE | bez podmiany; opcja 'ROT Blessings In Game Models' |
| AM-17 | SCA wola Default zamiast BaseModel | DROBNE | 'Fix SCA Model Chains' (NIE do testu) |
| AM-18 | Ocena malzonka 0 w kontraktach BK gracza | DROBNE | 'BKROT Spouse Score Fix' w P2 |
| AM-19 | Ujemny zold z blogoslawienstwa Wielkiego Ksiecia (gracz) | DROBNE | 'Fix Princely Arbiter Wage' / w 'Fix BKROT Wage Chain' |
| AM-21 | Unikaty ROT znikaja i wracaja | DROBNE | 'Rot Unique Companions Persist' - w toku (wf unikaty) |
| AM-23 | Licznik okupow 169c = 0 | DROBNE | kosmetyka logu 169c |
| AM-24 | Jedzenie z niczego dla konwojow DTE | DROBNE | ramka ksiegi; 'DteConvoyFoodFromShelf' |
| AM-25 | AmbiguousMatch GetCouncil w BKROT | DROBNE | filtr znanych pierwszych szans |
| AM-28 | Lorath 'lorathi' / 'nord' - brak wiary BK i wedrowcow | DROBNE | 'Lorath Faith Fix' + wzorce craftsman / noble |
