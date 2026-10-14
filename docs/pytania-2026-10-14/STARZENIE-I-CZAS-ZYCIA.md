# Starzenie i czas zycia przy roku 364 dob - czy "1 rok w grze = 1 rok starzenia, 1 dzien = 1 dzien" (14.10)

Decyzja Jeffa 14.10 (STAN-PRAC 11:29 UTC): "starzenie: 1 rok w grze to 1 rok starzenia, 1 dzien to 1 dzien". Ten plik to TYLKO SPRAWDZENIE:
nic w kodzie nie zmienione, gry nie uruchamiano, nic nie wgrane, bez commita. Druga czesc tej decyzji (cecha co 3 poziomy, dodatkowe skupienie
co 4) jest robiona osobno (galaz `w-toku/rozwoj-punkty`) - tu jej nie ma.

Oznaczenia: **[K]** z kodu (plik:linia), **[U]** z ustawien, **[P]** z logu albo naglowka zapisu, **[S]** wyliczone skryptem z liczb [K]/[P].
Kalendarz: gra bez zmian = rok **84 doby** (7 x 3 x 4); u Ciebie Armoury `LongYearTimeModel` = rok **364 doby** (13 tygodni w porze).
Wspolczynnik 364 / 84 = **4,33**.

Skroty: `G` = dekompilacja gry 1.4.8 `...\ae222cb7-...\scratchpad\dec32\cs_full`; `D` = moje dekompilacje ZAINSTALOWANYCH DLL (ilspycmd 9.1)
`SC\dec\{rot,bk,bkrot,naval}`; `SC` = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad\rozwoj\starzenie`;
`ZB` = kod naszych modow, drzewo `...\ae222cb7-...\scratchpad\zb\int` (galaz `w-toku/zbior-e8`, 4adb0a5 = wersja wgrana).
Logi biegu dwuletniego: `A2` = `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-13_21-14-13.log`, `CS2` = `...\session-2026-10-13_21-14-11.log`
(nowa kampania, postac "Rafford", 728 dob, BirthAndDeath = cykl zycia wlaczony).

## 0. Dla Jeffa (prosto)

1. **Starzenie juz jest "1 rok = 1 rok, 1 dzien = 1 dzien".** Rok ma 364 doby, a wiek kazdej postaci rosnie o 1 co 364 doby. Dowod z zapisow: postacie autotestu zaczynaja z 20 latami i po 364 dobach maja 21 (5 roznych kampanii); w biegu dwuletnim (Rafford) po 728 dobach 22, po 780 dobach 22,14 (w zwyklej grze mialby juz 28,7). Twoje stare kampanie sprzed dlugiego roku starzaly sie o 1 rok co 84 doby - wiek idzie za dlugoscia roku.
2. **Zgodne, nic nie trzeba robic:** dorastanie (dorosly w 18 lat = 6 552 doby), etapy wychowania dzieci, smierc ze starosci (od 67 lat; szansa na rok ta sama co w ROT, na dobe 4,33 x mniejsza; z tych, co dozyja 67, polowa dozyje ok. 84 lat - tyle samo lat co w ROT przy starym roku 84 dni), wiek slubu (od 18 lat) i rodzenia (18-45 lat), wiek na starcie, niewola i leczenie ran (liczone w dobach).
3. **NIE - ciaza:** trwa 36 dob, czyli ok. 5 tygodni. Powinna ok. 273 doby (9 miesiecy).
4. **NIE - dzieci:** szansa na poczecie jest "na dobe" i dobrana pod stary rok 84 dni. Dlatego zona rodzi w pierwszym roku malzenstwa 1,9-3,7 dziecka (gra przewidziala 0,7-1,2), a w calym zyciu 5,5-11 (gra: 3-7). To symulacja wzoru gry - porodow zaden log nie liczy.
5. **NIE - sluby AI:** szansa tez jest "na dobe" pod stary rok. Wychodzi 146 slubow na rok (w logu: 291 slubow w 728 dob), a wedlug Twojej zasady powinno ok. 48.
6. **Proponuje:** ciaza 273 doby; szansa poczecia razy 84/364 (= 0,23); szansa slubu razy 0,23 zamiast Twojego hamulca 0,7. Symulacja: wtedy wychodzi najwyzej ok. 1 dziecko na rok i tyle dzieci w zyciu, ile przewidziala gra.
7. **Skutek uboczny:** bedzie mniej dzieci i slubow. Dzis bohaterow przybywa szybko: z 4 285 do 6 586 w 720 dob (+54%; ile z tego to dzieci - log nie mowi). Po zmianie bedzie ich mniej, ale bedzie tez mniej dziedzicow - bitwy dalej zabijaja tyle samo na dobe.
8. Zmiany zadzialaja dla nowych ciaz i slubow - najlepiej od nowej kampanii. W kodzie nic nie zmienialem.

## 1. Tabela - kazdy proces czasu zycia

| # | Proces | Jak jest (doby) | Wedlug zasady | Zgodne | Gdzie w kodzie | Dowod | Jak naprawic (klucz) |
|---:|---|---|---|---|---|---|---|
| 1 | **Dlugosc roku i wiek bohatera** | rok = 364 doby; wiek = doby od urodzin / 364 | +1 rok wieku co 364 doby | **TAK** | [K] G `Hero.cs`:472-485 (`Age` = `_birthDay.ElapsedYearsUntilNow`), `CampaignTime.cs`:116, 186-197 (rok w tikach z modelu czasu); ZB `Armoury\src\Calendar.cs`:26-60 (`WeeksInSeason` 13); [U] `Armoury.json`:47-48 | [P] A2:210 "rok ma 364 dni"; 1. doba kampanii = dzien 108 836 = 299 x 364; wiek gracza 20 -> 21 (d364) -> 22 (d728) -> 22,14 (d780) - rozdz. 2.1 | - |
| 2 | **Wiek na starcie - gracz** | kreator ROT 20 / 30 / 40 / 50 lat (urodziny = teraz minus N lat po 364 doby) | lata kalendarza | **TAK** | [K] D `rot\ROT\ROTCharacterCreationCampaignBehavior.cs`:7509, 7550, 7591, 7632, 7660 | [P] naglowki zapisow: d40 20,11; d69 20,19; d120 20,33 (= 20 + d/364) | - |
| 3 | **Wiek na starcie - NPC** | wiek z danych ROT, urodziny = rok swiata minus wiek | lata kalendarza | **TAK** | [K] G `Hero.cs`:2246-2250, `Helpers\HeroHelper.cs`:489-494 | [S] patrz nr 17 (rocznik dorasta na Nowy Rok) | - |
| 4 | **Dorastanie** (niemowle 3, dziecko 6, nastolatek 14, dorosly 18 lat) | 1 092 / 2 184 / 5 096 / **6 552** doby (gra bez zmian: 252 / 504 / 1 176 / 1 512) | 18 lat = 18 x 364 dob | **TAK** | [K] G `DefaultAgeModel.cs`:33-45 (ROT przekazuje dalej: D `rot\ROT.Models\ROTAgeModel.cs`:10-16), `AgingCampaignBehavior.cs`:109-131 (progi z calkowitego wieku w latach) | [P] CS2 linie "Swaty ROT (Z.3)": wolnych lordow d364 633 -> d365 689 (+56 w jedna dobe - rocznik doszedl do 18 lat na Nowy Rok [S]) | - |
| 5 | **Wychowanie dzieci** (etapy 2, 5, 8, 11, 14, 16 lat; BK przy dorosnieciu: jezyki, Tutor / Teacher) | etapy co 2-3 lata = co 728-1 092 doby | lata kalendarza | **TAK** | [K] G `EducationCampaignBehavior.cs`:16-28, 612 (`BirthDay + CampaignTime.Years(n)`); D `bk\BannerKings.Behaviours\BKEducationBehavior.cs`:36, 189-194 (zdarzenie "dorosl") | - | - |
| 6 | **Ciaza** | **36 dob** (= 0,10 roku; gra bez zmian: 0,43 roku) | prawdziwa ok. 273 doby (39 tygodni = 0,75 roku) | **NIE** | [K] G `DefaultPregnancyModel.cs`:13 (`PregnancyDurationInDays` 36; 18 tylko w trybie Fast - FastMode wylaczony); termin zapisany przy poczeciu `PregnancyCampaignBehavior.cs`:192-195; czynny model D `bkrot\BKROTPatch.Models\BKROTPregnancyModel.cs`:12 (dziedziczy, czasu ciazy nie nadpisuje), dodany `bkrot\BKROTPatch\BKROTPatch.cs`:310 | brak liczby porodow w logach | klucz Armoury **`PregnancyDays` = 273** (0 = gra): postfiks gettera `DefaultPregnancyModel.PregnancyDurationInDays` |
| 7 | **Plodnosc** (szansa poczecia) | na dobe: (1,2 - 0,04 x (wiek - 18)) / (dzieci + 1)^2 x 0,12 - np. 0,11 na dobe dla 25-latki bez dzieci; ta sama na dobe przy kazdej dlugosci roku | szansa na ROK jak w grze -> na dobe x 84/364 | **NIE** (4,33 x za duza na rok) | [K] G `DefaultPregnancyModel.cs`:32-46; rzut co dobe `PregnancyCampaignBehavior.cs`:90-124 (malzonek blisko albo 20% losu); BK drudzy malzonkowie: D `bk\BannerKings.Behaviours.Marriage\BKMarriageBehavior.cs`:256-290; mnozniki WARUNKOWE: BK x1,15 za doktryne Childbirth u matki i drugi raz u malzonka (`bk\BannerKings.Patches\VanillaModelTweakPatches.cs`:1143-1163), BKROT x1,25 za kazde z blogoslawienstw Mother / Pantera / GodsLove (`BKROTPregnancyModel.cs`:14-50) | [S] `SC\plodnosc_wynik.txt`: 1. rok malzenstwa 1,9-3,7 dziecka (gra 0,7-1,2), do 45 lat 5,5-11,4 (gra 3,2-6,8); [P] zywych bohaterow 4 285 (d0) -> 6 586 (d720) | klucz Armoury **`FertilityPerYear` = true**: postfiks `DefaultPregnancyModel.GetDailyChanceOfPregnancyForHero` x 84 / `CampaignTime.DaysInYear` |
| 8 | Porod: blizniaki 3%, martwe 1%, smierc matki 1,5% | na porod | na porod | **TAK** (spada samo z liczba porodow) | [K] G `DefaultPregnancyModel.cs`:15-21, `PregnancyCampaignBehavior.cs`:155-190 | - | - |
| 9 | Wiek rodzenia 18-45 lat | lata | lata | **TAK** | [K] G `DefaultPregnancyModel.cs`:23-30; D `bk\...\BKMarriageModel.cs`:394 ("Infertile") | - | - |
| 10 | Wiek slubu (min. 18; roznica do 25 lat; wiek w szansie) | lata | lata | **TAK** | [K] G `DefaultMarriageModel.cs`:13-15, 53-56; D `rot\ROT.Models\ROTMarriageModel.cs`:67; `bk\...\BKMarriageModel.cs`:254-268 | - | - |
| 11 | **Sluby AI** (szansa pary na dobe) | 0,002 na dobe x wiek x roznica x ... (BK x1,25); rzut co dobe dla kazdego wolnego lorda | szansa na ROK jak w ROT -> na dobe x 84/364 | **NIE** (4,33 x za duza na rok) | [K] G `DefaultMarriageModel.cs`:49-61; D `bk\...\BKMarriageModel.cs`:446-456; rzut ROT `rot\ROT.CampaignBehaviors\ROTRelationshipsBehavior.cs`:56 (ROT wylacza swaty gry: :18); nasza droga ZB `CrashScribe\src\MarriageFast.cs`:632 (hamulec 0,7 tylko wolny lord przy regule) | [P] CS2: 291 slubow w 728 dob = **146 na rok**; [U] `CrashScribe.settings.xml` `MarriageChanceScale` 0.7 | klucz CrashScribe **`MarriageChanceScale` 0.7 -> 0.23** (= 84/364; zakres klucza 0,1-1,0) - ok. 48 slubow na rok [S] |
| 12 | Oferty slubu dla Twojego rodu | szansa na dobe, oferta nie czesciej niz co 7 dob | jak nr 11 | **NIE** (drobne) | [K] G `MarriageOfferCampaignBehavior.cs`:310, 316-330 | - | razem z nr 11 (mnoznik w tym samym miejscu, nie latka na `NpcCoupleMarriageChance` - rozdz. 3) |
| 13 | **Smierc ze starosci** | od 67 lat: szansa roczna 0,3 x (wiek - 67) / 61, rozlozona na 364 doby (75 lat: 3,9% na rok = 0,011% na dobe) | szansa na ROK | **TAK** (na dobe 4,33 x mniejsza niz w grze, na rok ta sama) | [K] G `DefaultHeroDeathProbabilityCalculationModel.cs`:13-35 (`1 - (1 - p)^(1/DaysInYear)`), `AgingCampaignBehavior.cs`:286-310; D `ROTAgeModel.cs`:18 (67), czynny model D `bkrot\BKROTPatch.Models\BKROTDeathModel.cs` (tylko blogoslawienstwa x0,1 / x0,05), dodany `BKROTPatch.cs`:302 | [S] `SC\smierc_wynik.txt`: z 67-latkow 50% dozywa 83,6 roku w obu kalendarzach (6 028 dob przy 364, 1 392 przy 84) | - |
| 14 | Choroba gracza przed smiercia | 3 doby, potem -5% HP x dni | doby | **TAK** | [K] G `AgingCampaignBehavior.cs`:133-165 | - | - |
| 15 | Rekonwalescencja (leczenie ran bohaterow) | HP na dobe / godzine | doby | **TAK** (kalendarz nie wchodzi) | [K] G `DefaultPartyHealingModel.cs`; D `bkrot\BKROTPatch.Models\BKROTPartyHealingModel.cs` | - | - |
| 16 | Niewola lordow | ucieczka 4% na dobe w lochu, 8-16% w partii w drodze (u gracza polowa), okup, pokoj | doby | **TAK** (kalendarz nie wchodzi) | [K] G `PrisonerReleaseCampaignBehavior.cs`:201-255; nasze RealisticCaptivity - bez stalych rocznych (ZB `RealisticCaptivity\src`) | [P] A2 "Niewola lordow i okupy (169c)": mediana 2-4 doby, najdluzej 21-25 dob (d181-d728) | - |
| 17 | Urodziny bohaterow startowych | wszyscy z danych ROT maja urodziny 1. dnia roku (ROT zaczyna w dniu 0 roku 299, losowanie dnia z przedzialu [0, 0)) - caly rocznik dorasta / starzeje sie na Nowy Rok | - | **TAK** (nie dotyczy zasady; tak samo w ROT bez zmian) | [K] G `Helpers\HeroHelper.cs`:489-494; D `rot\ROT.Models\ROTCampaignTimeModel.cs`:10 (start `Years(299) + Hours(6)`) | [P] +56 wolnych lordow miedzy d364 a d365 (nr 4) | - |

## 2. Szczegoly i liczby

### 2.1 Wiek (proces 1) - liczba z biegu dwuletniego

Wiek gracza jest w naglowku zapisu (`MainHeroVisual` -> `<BodyProperties ... age="...">` = `Hero.Age` [K] G `Hero.cs`:178); skrypt
`SC\skrypty\wiek_naglowek.py` (tylko odczyt 64 KB naglowka) -> `SC\wiek_zapisy.txt` (300 zapisow) [P]:

| Kampania (postac) | Doba | Wiek w zapisie | 20 + doba / 364 | 20 + doba / 84 (gra bez zmian) |
|---|---:|---:|---:|---:|
| `cwCnHzDo7XEs` (Merlon) | 40 | 20,11 | 20,11 | 20,48 |
| `3IKVCncDp2Jq` (Hugh) | 69 / 120 | 20,19 / 20,33 | 20,19 / 20,33 | 20,82 / 21,43 |
| `Cdy6Cbx42Gsi` (Jason) | 360 / 364 / 371 | 20,99 / 21 / 21,02 | 20,99 / 21,00 / 21,02 | 24,3 / 24,3 / 24,4 |
| **`dD2zuOCd0oMv` (Rafford) - bieg dwuletni** | **728** | **22** | **22,00** | **28,67** |
| `dD2zuOCd0oMv` (Rafford, dalszy test) | 780 | 22,14 | 22,14 | 29,29 |

Wiek startu Rafforda w logu nie jest wypisany; kreator ROT ustawia 20 lat ([K] nr 2), a 20 + 728/364 = 22,00 zgadza sie z zapisem.
NPC nie maja wieku w zadnym logu - u nich ta sama funkcja `Hero.Age` [K], a rocznik 18-latkow wchodzi w doroslosc na Nowy Rok (nr 4, 17).

### 2.2 Ciaza i plodnosc (procesy 6-9)

Symulacja dzien po dniu wedlug wzorow gry (`SC\skrypty\plodnosc.py` -> `SC\plodnosc_wynik.txt`; 20 000 kobiet na wariant; rod nieprzepelniony,
bez perkow i blogoslawienstw; "blisko" = malzonek w tym samym miejscu 1,0 albo tylko los 20% gry) [S]:

| Wariant | 1. rok malzenstwa | 2 lata | 5 lat | do 45 lat |
|---|---:|---:|---:|---:|
| A gra bez zmian (rok 84, ciaza 36, szansa x1) | 0,67-1,21 | 1,22-2,29 | 2,00-3,91 | 3,15-6,76 |
| **B TERAZ (rok 364, ciaza 36, szansa x1)** | **1,91-3,71** | 2,67-5,18 | 3,86-7,39 | **5,54-11,36** |
| **C PROPOZYCJA (rok 364, ciaza 273, szansa x0,23)** | **0,38-0,97** | 0,99-1,72 | 1,83-3,35 | **3,06-6,48** |
| D tylko ciaza 273 | 0,88-1,02 | 1,51-2,02 | 2,96-4,58 | 5,08-9,89 |
| E tylko szansa x0,23 | 0,98-2,27 | 1,45-3,13 | 2,17-4,54 | 3,23-7,07 |

Zakres = slub w wieku 18-25 lat, malzonek blisko 0,2-1,0. Wniosek: sama ciaza (D) albo sama szansa (E) nie wystarcza; dopiero oba (C) daje
"na rok" to, co gra przewidziala dla swojego roku (A), i ok. 1 dziecko na rok (prawdziwe tempo). Wiek rodzenia (18-45 lat) i porod - bez zmian.

**Bohaterowie w biegu dwuletnim** [P]: zywych ludzi-bohaterow 4 285 na starcie (A2, linia "Rasy: start sesji") i 6 586 w zapisie kontrolnym tej kampanii
z doby 720 (`Armoury-2026-10-14_00-43-17.log`, wczytany `autotest-dlugi` z d720 - CS2 "ZAPIS ... doba 720.00"). Ile z +2 301 to dzieci - logi nie mowia
(dokladaja tez np. rodziny ziemian BK: D `bk\BannerKings.Behaviours\BKGentryBehavior.cs`:680-713, notable i wedrowcy).

### 2.3 Sluby (procesy 10-12)

[P] CS2 linie dobowe "Swaty ROT (Z.3)": slubow od startu d91 38, d182 80, d364 159, d546 238, d728 **291** (= 146 na rok); wolnych lordow szukajacych pary
637 (d1) -> 646 (d360) -> 704 (d728) - pula NIE maleje, zonatych w liscie 522 -> 1 009. Regula "tylko kandydaci z szansa" CZYNNA, hamulec 0.7 (CS2 21:15:17).
Rachunek [S]: ROT bez reguly mial 113 slubow na rok (pomiar 13.10, opis klucza w `CrashScribe.settings.xml`), z regula bez hamulca 215. "Na rok jak w ROT"
= na dobe x 84/364: bez reguly ok. 26, z regula Jeffa ok. 50; przy `MarriageChanceScale` 0.23 (liniowo jak w opisie klucza): 146 x 0,23 / 0,7 = **ok. 48 na rok**.
Wiek (min. 18, roznica do 25 lat, czynniki wieku w szansie) jest w latach - zgodny.

### 2.4 Smierc ze starosci (proces 13)

[S] `SC\skrypty\smierc.py` -> `SC\smierc_wynik.txt` (wzor gry, `BecomeOldAge` 67 ROT, `MaxAge` 128):

| Wiek | Na rok (oba kalendarze) | Na dobe przy roku 364 | Na dobe przy roku 84 |
|---:|---:|---:|---:|
| 70 | 1,5% | 0,0041% | 0,0177% |
| 75 | 3,9% | 0,0110% | 0,0478% |
| 80 | 6,4% | 0,0181% | 0,0786% |
| 90 | 11,3% | 0,0330% | 0,1428% |

Z dozywajacych 67 lat: 75% dozywa 77,7, 50% - 83,6, 25% - 90,3, 10% - 96,8 roku - **w obu kalendarzach to samo w latach**. Szansa dzienna jest 4,33 x mniejsza
niz w zwyklej grze, i tak ma byc (gra dzieli roczna przez `DaysInYear`). Wiek 127+ = smierc tego samego dnia (dotyczy tylko Innych ROT z wiekiem w tysiacach - ROT ich
nie zabija: Whitewalker 4 na starcie i 4 w d720 [P]).

### 2.5 Inne (procesy 14-17)

- Choroba gracza, leczenie ran, niewola: liczone w dobach i godzinach, bez roku - kalendarz ich nie zmienia (zgodne z "1 dzien = 1 dzien"). Ile ma trwac niewola
  (dzis mediana 2-4 doby) to osobna sprawa rozgrywki, nie kalendarza.
- Zasada R8 projektu (ZB `Armoury\src\WorldTime7.cs`:17-19: "stale kalendarza x rok/84, czasy trwania zostaja dniami") - plodnosc i sluby to stale
  "na rok" (R8 je obejmuje, dotad nie przeliczone); ciaza to czas trwania, ale wedlug Twojej zasady "1 dzien = 1 dzien" ma byc prawdziwy (273), nie x4,33 (156).
- Jesli w nowej kampanii wylaczysz cykl zycia (BirthAndDeath, opcja "IsLifeDeathCycleEnabled"), wiek stoi w miejscu i nikt nie rodzi ani nie umiera ze starosci
  [K] G `Hero.cs`:472-479, `AgingCampaignBehavior.cs`:94, `PregnancyCampaignBehavior.cs`:92; `...\BirthAndDeathOptionsProvider.cs`:9-20.

## 3. Propozycja naprawy (do zrobienia jako paczka - teraz nic nie zmienione)

1. **Ciaza 273 doby** - Armoury, klucz `PregnancyDays` (273; 0 = gra), postfiks gettera `DefaultPregnancyModel.PregnancyDurationInDays`.
   BKROT (czynny model) gettera nie nadpisuje, wiec postfiks dziala. Termin porodu gra zapisuje przy poczeciu - ciaze trwajace w starym zapisie koncza sie po staremu.
   Ten sam getter czytaja jeszcze wpisy dziennika gry (G `LogEntries\PregnancyLogEntry.cs`:19, `ChildbirthLogEntry.cs`:22 - jak dlugo wpis zostaje w historii):
   po zmianie 280 / 546 dob zamiast 43 / 72 - bez wplywu na rozgrywke.
2. **Plodnosc na rok** - Armoury, klucz `FertilityPerYear` (true), postfiks `DefaultPregnancyModel.GetDailyChanceOfPregnancyForHero` x 84 / `CampaignTime.DaysInYear`
   (przy roku 84 = x1, czyli gra). Mnozniki BK (x1,15) i BKROT (x1,25) mnoza sie dalej tak samo (BKROT wola metode bazowa - mnoznik raz).
   Obejmuje obie drogi poczecia (gra i BK drudzy malzonkowie - ta sama metoda modelu).
3. **Sluby na rok** - najprosciej `MarriageChanceScale` 0.7 -> 0.23 w `Modules\CrashScribe\ModuleData\CrashScribe.settings.xml` (gra zamknieta, kopia).
   Hamulec dziala tylko na wolnego lorda przy czynnej regule (`MarriageFast.cs`:632, 429). Pelniej: mnoznik kalendarza w samym `MarriageFast` (takze droga bez reguly)
   + oferty dla rodu gracza (nr 12). **NIE latka Harmony na `NpcCoupleMarriageChance`** - `MarriageFast` uznalby ja za obca i zatrzymal regule
   "tylko kandydaci z szansa" (ZB `CrashScribe\src\MarriageFast.cs`:62, 88, 429).
4. Kolizje sprawdzone w kodzie: RaceLaw (prefiksy `MakePregnantAction`, `CheckOffspringToDeliver`) - bez zmian, nie zalezy od czasu ciazy ani szansy;
   zaden nasz mod nie latkuje dzis modelu ciazy (grep `pregnan` w ZB: tylko RaceLaw i opis jej klucza `SameRaceOnly`). Skan DLL modow (`SC\skan.txt`): czas ciazy czyta jeszcze tylko AIInfluence (wylaczony).
5. Ryzyko do sprawdzenia w biegu dwuletnim po zmianie: liczba bohaterow (dzis +54% w 720 dob), rody AI bez dziedzica (smierc w bitwie liczy sie na dobe jak dotad),
   pula wolnych lordow i kandydatek dla gracza (dzis 637-704), liczba slubow AI na rok (cel ok. 48).

## 4. Czego nie wiem

1. **Ile porodow i zgonow ze starosci bylo w biegu** - zaden log ich nie liczy; liczby plodnosci to symulacja wzoru gry, nie pomiar.
2. **Ile z +2 301 bohaterow to dzieci** (reszta: rodziny ziemian BK, notable, wedrowcy).
3. **Wiek NPC w biegu** - logi nie wypisuja wieku zadnego NPC; dowod liczbowy tylko dla gracza (naglowki zapisow) i posredni dla rocznika (+56 na Nowy Rok).
4. Czy "ok. 48 slubow na rok" przy 0.23 - szacunek liniowy jak w opisie klucza (pomiar dopiero w biegu).
5. Jak czesto malzonkowie AI sa "blisko" - dlatego w tabelach zakres 0,2-1,0.

## 5. Pliki tego sprawdzenia (`SC`)

`skrypty\wiek_naglowek.py` -> `wiek_zapisy.txt` (wiek gracza z naglowkow zapisow), `skrypty\plodnosc.py` -> `plodnosc_wynik.txt`, `skrypty\smierc.py` -> `smierc_wynik.txt`,
`skrypty\skan.py` -> `skan.txt` (kto w DLL modow dotyka wieku, ciazy, smierci, slubow), `dec\` (ROT, BK, BKROTPatch, NavalDLC, BannerlordPrisonIsNotFun - zainstalowane DLL),
`prefiksy_armoury.txt`, `prefiksy_cs.txt` (spis rodzajow linii logow biegu dwuletniego).

## 6. Weryfikacja (14.10, osobny sprawdzajacy - sceptyk)

Sprawdzone samodzielnie w kodzie i logach 3 najwazniejsze twierdzenia. Tylko odczyt; gra nieuruchamiana, nic niewgrane, bez commita.
Skrypt kontrolny: `...\c36b73ca-...\scratchpad\wer_smierc.py` (smierc ze starosci w float32 jak w grze).

**A. Wiek rosnie o 1 na 364 doby - POTWIERDZONE.**
- [K] G `Hero.cs`:472-485: `Age` = `_birthDay.ElapsedYearsUntilNow`; `CampaignTime.cs`:116 = ticki / `TimeTicksPerYear`; :178-197 `TimeTicksPerYear` = pory x tygodnie x dni z
  modelu czasu. ZB `Armoury\src\Calendar.cs`:45-57 - `LongYearTimeModel` zmienia tylko `WeeksInSeason` (13). Zadna latka na `Hero.Age` ani `ElapsedYearsUntilNow`
  w D (rot, bk, bkrot, naval) ani w ZB (grep).
- [P] Naglowek zapisu: `DayLong` = `CampaignStartTime.ElapsedDaysUntilNow` (G `SaveHandler.cs`:197 - doby, niezalezne od roku), wiek = `Hero.Age` (`Hero.cs`:178).
  Rafford (`dD2zuOCd0oMv`): d728 -> 22, d780 -> 22,14; roznica 52 doby = 0,14 roku = 52/364 (przy 84 byloby 0,62). d364 -> 21 w 5 innych kampaniach
  (Jason, Alastor, Tyler, Emrick, Zachery); drugi bieg dwuletni (Martyn `5hVlKoedGwKF`) d728 -> 22.
- [P] Kontrola odwrotna: stare kampanie sprzed dlugiego roku (Adrianos `c4b4dee060af` d115,5 -> 21,38, d934,6 -> 31,13; Przemyslaw `72c218f65154` d364,7 -> 24,34)
  maja 1 rok = 84 doby - wiek naprawde idzie za dlugoscia roku.
- [P] `Armoury-2026-10-14_00-43-17.log` (wczytany zapis d720): "dzien 109556" - "dzien 108836" = 720 i "Year 300, Day 357" (= 364 + 356) - rok 364 w zegarze gry.
- Uwaga do dowodu: linia A2:210 "rok ma 364 dni" liczy Armoury samo (7 x 13 x 4, `Calendar.cs`:62-70), nie czyta gry - w trybie Fast pokazalaby 364, a rok mialby 156
  (`DefaultCampaignTimeModel.cs`:23-33: 3 dni w tygodniu). Prawdziwym dowodem sa naglowki zapisow (wyzej); one tez wykluczaja tryb Fast.

**B. Ciaza 36 dob - POTWIERDZONE.**
- [K] G `DefaultPregnancyModel.cs`:13: 36 (18 tylko przy `GameAccelerationMode.Fast`; Fast wykluczony - pkt A). Termin: G `PregnancyCampaignBehavior.cs`:192-195
  `CampaignTime.DaysFromNow(PregnancyDurationInDays)`; porod przy `DueDate.IsFuture == false` (:155-158).
- Czynny model D `bkrot\BKROTPatch.Models\BKROTPregnancyModel.cs`:12 dziedziczy po `DefaultPregnancyModel`, nadpisuje tylko szanse (:14), czasu nie; dodany `BKROTPatch.cs`:310.
  BK nie ma modelu ciazy - latka tylko na szanse (`VanillaModelTweakPatches.cs`:1143-1163). ROT czyta liste ciaz tylko przy przemianie w Innego
  (`OthersPatches.cs`:305-321). Nasz RaceLaw (ZB `Armoury\src\RaceLaw.cs`) - prefiksy na poczecie i porod, terminu nie zmienia. `SC\skan.txt`: getter czasu czyta
  jeszcze tylko AIInfluence (wylaczony).
- [P] Brak pomiaru: w logach biegu nie ma ani jednego porodu (grep "pregnan/porod/childbirth" - tylko nazwy latek RaceLaw).

**C. Smierc ze starosci: szansa na dobe 4,33 x mniejsza, na rok ta sama - POTWIERDZONE.**
- [K] G `DefaultHeroDeathProbabilityCalculationModel.cs`:24-25: roczna p = 0,3 x (wiek - 67) / (128 - 67), na dobe `1 - (1 - p)^(1/DaysInYear)`; rzut co dobe
  `AgingCampaignBehavior.cs`:91-106 -> `IsItTimeOfDeath` :286-288 (`RandomFloat < Hero.ProbabilityOfDeath`, `Hero.cs`:790 = czynny model). 67 = D `ROTAgeModel.cs`:18,
  128 = `DefaultAgeModel.cs`:45 (ROT przekazuje). Czynny D `BKROTDeathModel.cs`:13 (dodany `BKROTPatch.cs`:302): linia 26 to wywolanie wirtualne samego siebie,
  zatrzymane przez `GlobalRecursionGuard` (:20-22 -> model gry) - wynik = model gry x blogoslawienstwa (x0,1 / x0,05) jeden raz. Zaden inny mod nie dodaje
  modelu smierci ani wieku starosci (`SC\skan.txt`, grep D).
- [S] `wer_smierc.py` (float32 jak `MathF.Pow`): 75 lat 0,0110% na dobe / 3,93% na rok (rok 364) i 0,0478% / 3,93% (rok 84); mediana od 67 lat 83,6 roku w obu
  (6 028 dob wobec 1 392) - zgodne z `SC\smierc_wynik.txt` co do cyfry.
- [P] Brak pomiaru zgonow ze starosci w logach (rozdz. 4 pkt 1 - prawda).

**Dodatkowo sprawdzone liczby:** 291 slubow = suma "slubow N" z 728 linii dobowych "Swaty ROT (Z.3)" w CS2 (= 145,9 na rok); 4 285 -> 6 586 bohaterow-ludzi
(linie "Rasy: start sesji" w A2 i w `Armoury-2026-10-14_00-43-17.log`, ten drugi przy d720 - jak wyzej); szansa 0,11 na dobe dla 25-latki bez dzieci
= (1,2 - 7 x 0,04) x 0,12 = 0,110.

**NIE zgodne / poprawione w tym pliku:**
1. Rozdz. 0 pkt 1: "start 20 -> po 364 dobach 21 -> po 728 dobach 22" laczylo rozne kampanie jak jedna. Rafford nie ma zapisu z d364, a jego wieku startu nie ma
   w logu (20 = kreator ROT). Poprawione: d364 -> 21 z 5 innych kampanii, Rafford d728 -> 22 i d780 -> 22,14; dopisana kontrola odwrotna (stare kampanie, rok 84).
2. Rozdz. 0 pkt 2: "polowa dozyje 84 - jak w grze" - w grze bez ROT starosc zaczyna sie od 55 lat (`DefaultAgeModel.cs`:43), wiec mediana nie jest "jak w grze",
   tylko jak w ROT przy roku 84 dni. Poprawione.
3. Rozdz. 0 pkt 2: "wiek slubu i rodzenia (18-45 lat)" - slub ma tylko dolna granice 18 (`DefaultMarriageModel.cs`:13-15), 45 dotyczy tylko rodzenia. Poprawione.
4. Rozdz. 0 pkt 4: "2-3,7 dziecka" wobec 1,91-3,71 w tabeli 2.2 - poprawione na 1,9-3,7; dopisane, ze to symulacja, nie pomiar.
5. Tabela nr 7 (i rozdz. 3 pkt 2): mnozniki BK x1,15 i BKROT x1,25 sa WARUNKOWE (doktryna Childbirth u kazdego z malzonkow / kazde z trzech blogoslawienstw),
   nie stale. Poprawione w tabeli; symulacja slusznie je pomija.
6. Rozdz. 3 pkt 1: brakowal skutek uboczny `PregnancyDays` - getter czytaja tez wpisy dziennika gry (`PregnancyLogEntry.cs`:19, `ChildbirthLogEntry.cs`:22).
   Dopisany (bez wplywu na rozgrywke).
7. Tabela nr 1, dowod A2:210 - slabszy niz podany (tekst liczony przez Armoury, nie przez gre); wniosek stoi dzieki naglowkom zapisow (pkt A).

Trzy glowne twierdzenia (wiek 1 na 364 doby, ciaza 36 dob, smierc ze starosci: na rok tak samo, na dobe 4,33 x mniej) - bez zastrzezen co do tresci.
Nie sprawdzalem ponownie symulacji plodnosci (`plodnosc.py`) ani szacunku "ok. 48 slubow na rok" przy 0,23.
