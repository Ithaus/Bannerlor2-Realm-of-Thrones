# Etap 7 - rozpoznanie A: liczby z logow i decyzje Jeffa (12.10)

Tylko odczyt: kod nie byl zmieniany, gry nie uruchamiano, nic nie commitowano. Rok gry = **364 dni** (CLAUDE.md 7);
liczby "na rok" z dokumentow sprzed 05.10 (rok 168) oznaczam jako nieaktualne.

**Zrodla**
- **Z33** = `Modules/Armoury/Armoury-2026-10-12_02-44-09.log` (36 104 linie), autotest WGRANIA 33, nowa kampania, 120 dob
  (dzien gry 108836 = doba 1 = pierwszy dzien roku 299 AC i pierwszy dzien sezonu przy 364 dniach).
- **N33** = `Modules/Armoury/Logs/2026-10-12_02-44-09/noc.log` (2 838 linii, 120 nocy) - oboz swiata, godzina po godzinie.
- **W33** = `.../Logs/2026-10-12_02-44-09/wioski.log` (848 linii) i `wioski-okregi.csv`.
- **CS33** = `Documents/.../CrashScribe/session-2026-10-12_02-44-08.log` (4 425 linii) - KRONIKA WOJEN.
- Parsery: `scratchpad/e7A/parse_main.py`, `parse_noc.py`, `report.py` (wynik `main.json`, `noc.json`).

**Notacja:** `L123` = linia w Z33; `N123` = linia w N33; `W123` = linia w W33; `CS123` = linia w CS33. Okna: d1-30 / d31-60 /
d61-90 / d91-120 (srednia dobowa, w nawiasie pierwsza i ostatnia linia okna).

## 0. Najwazniejsze (dla projektu etapu 7)

1. **7.4 kary terenu:** zero danych - w 11 logach z audytem predkosci (Z33, 8 innych z 11-12.10, `kopia-728` z 10.10 i jej wycinek) gracz stoi na `Plain`; blad znany tylko z kodu
   (`AUDYT-KONFLIKTY.md:176-180`). Paczka musi przyniesc wlasny pomiar po partiach AI.
2. **7.6 obozy:** oboz 0-6 dziala (spi 600-880 na godzine, wodzow armii 2-8). Otwarte: **91 obudzen cudzym rozkazem na noc** (10 960 w 120
   nocy; zjazd sr. 0.40 / maks. 1.00 jedn. - straznik cofa po fakcie, O-3 niezrobione). **"AI placi za nieprzespana noc" juz dziala** (T10,
   kara -25% / -40% jak u gracza, dlug 3 = 0). Zbiorka armii - brak jakiegokolwiek pomiaru.
3. **7.7:** turnieje gry: **0 wyplat przez 61 dob (doby 36-96)**, wszystkie w tygodniach 0-4 sezonu - potwierdza blad tygodnia sezonu przy
   roku 364. Ustawienia Diplomacy u Jeffa domyslne (21/21/42/84). Pokoj i nowa wojna tej samej pary po 3, 21 i 24 dniach.
4. **7.10:** 346 ogni w 120 dob, sprawca w logu przy kazdym; **0 wiosek "spalonych"** (prog 50% osad) - test wygladu wymaga wymuszenia.
5. **7.11:** 656 sesji rabunku, 24 676 trafionych ludzi (93% uchodzcy); **lup gry "z niczego" 80 767 zl** - nadal otwarte zrodlo.
6. **7.12:** licznika ruchu brak; proxy 169c: 503 wejsc do Bliznakow (4.3/dobe, 68% karawany).
7. **Decyzje:** 12.10 tempo zostaje (SP:1252). **Do pytania Jeffa:** +15% po drodze (decyzja 09.10 sprzed 12.10), T-1 (jazda -7%), ile drog
   (proba 1.13 niepokazana), Diplomacy na skale 364, myto (przypisanie 22/23 niejasne). PL:259 (L) i PL:260 (M) sa juz rozstrzygniete (SP:841).

---

## 1. Liczby z logow

### 1.1 (7.4) Kary terenu - zwrot kary, ktorej gra nie nalozyla

- **Z33 nie ma zadnego pomiaru tego bledu.** Jedyny pomiar to "Audyt predkosci" gracza raz na dobe: 120 z 120 dob na terenie
  `Plain`, zawsze w dzien (L381 ... L35848) - czynniki: Base +3.99, Cavalry +1.20, Cargo -0.06, Low morale -0.40 (87 dob),
  Game Difficulty +0.40, World pace -1.00. Ani jednego wiersza z lasem, pustynia, sniegiem, brodem czy noca.
- Linia startowa L132: `Kary terenu po naszemu: las -0.1, pustynia -0.2, snieg -0.2, brod -0.5, noc -0.5 (6 modeli)`.
  L60: `WorldPace: mapa 50% (4 modeli)`, a L382 `WorldPace z MCM 75%` (World pace -1.00 = 25% z bazy 3.99 - dziala 75%;
  linia L60 podaje wartosc z chwili wpiecia, przed wczytaniem MCM).
- Opis bledu jest tylko w dokumencie: `docs/AUDYT-KONFLIKTY.md:176-180` (S7, 04.10, POTWIERDZONE w kodzie): `TerrainEase.Swap`
  zawsze oddaje +30% (las) / +10% (pustynia) / +10% (snieg) / +25% (noc), a gra nie naklada lasu przy perku ForestKin
  (>= 75% pieszych - czynnik DODATNI) ani pustyni przy cesze `AseraiDesertFeat` (Dorne = kultura `aserai`).
- **Wniosek dla projektu:** skali bledu (ile partii, ile km) z logow nie da sie podac. Paczka 7.4 musi dodac pomiar
  (np. raz na dobe: ile partii AI w lesie/pustyni dostalo zwrot bez pozycji gry i o ile jedn. predkosci).
- Sprawdzone takze w 10 innych logach: 8 logow Armoury z 11-12.10 oraz `kopia-728/Armoury-2026-10-10_20-58-02.log` (10.10, 728 audytow) i `czesc-d563.log`:
  gracz zawsze `Plain`. Autotest nie wchodzi w las/pustynie - **pomiar 7.4 musi isc po partiach AI**, nie po graczu.

### 1.2 (7.6) Obozy - spiacy lordowie, rozkazy, nieprzespane noce AI, zbiorka

Oboz swiata 0:00-6:00 dziala od T1 (09.10): L308 `NightRest: oboz swiata 0:00-6:00 (AiCampsAtNight=True,
ArmyLeadersAlwaysCamp=True, AiCampSkipPercent=15, ... krok straznika 0.10 h gry). T10: AiNightMarchByReason=True, AiSleepDebt=True ...`.

**Spiacy (N33, srednia na godzine obozu)**

| Miara | d1-30 (N2-647) | d31-60 (N650-1365) | d61-90 (N1369-2104) | d91-120 (N2108-2837) |
|---|---|---|---|---|
| spi razem | 599 | 862 | 877 | 848 |
| w tym lordow | 364 | 489 | 464 | 431 |
| w tym karawan | 235 | 373 | 413 | 418 |
| w tym wodzow armii | 1.7 | 6.7 | 7.7 | 5.9 (maks. 11 na godzine) |
| samotni lordowie w polu (linia "marsz") | 364 | 490 | 466 | 435 - spi 100% poza "ida noca" |

**Spiacy lord dostaje rozkazy (obudzeni cudza reka)** - to jest glowny otwarty blad 7.6:
- Razem w sesji **10 960 obudzen** (N2835 "Razem w sesji: 10960") = **ok. 91 na noc**; z linii godzinowych 10 094 (ostatnia godzina 5-6
  idzie osobno do Z33: 119 linii "NightRest swit 6:00", L650-L35799, razem 878, sr. 7.4, maks. 17).
- Na noc: d1-30 **56**, d31-60 **91**, d61-90 **97**, d91-120 **93** (ten sam podzial okien co wyzej).
  Rodzaj: "rozkaz jak nasz zapamietany" 33 / 59 / 64 / 66, "inny rozkaz" 22 / 32 / 32 / 26, "Hold bez rozkazu" ok. 0.6.
  Probka linii "obudzony cudza reka" (pierwsze 3 i co 20., 551 linii): GoToSettlement 367, GoToPoint 180, Hold 4.
- Zjazd obudzonego przed polozeniem z powrotem: **srednio 0.40 jedn. (ok. 1.9 km)** (wazone obudzeniami, 596 godzin), maks. na noc
  0.73-0.79 (srednie okien), **maks. w sesji 1.00 jedn.** (osiagniete w kilku nocach, np. noc 120 N2817-N2837). Przed T1 (rok 08.10): 39 218 obudzen/rok = 108/noc, zjazd
  sr. 1.29, maks. 5.69 (`audyt-2026-10-09/07-mapa-czas-obozy.md:122-127`). **Straznik skrocil zjazd ok. 3x, ale liczba obudzen
  zostala** (91 wobec 108 na noc) - rozkazy dalej przychodza (SCA/BK/AIInfluence), my je tylko cofamy po fakcie (O-3 z audytu 07 - niezrobione).
- Ruch lordow budzonych w oknie obozu (N33 linia "ruch", "BEZ WPISU POWODU"): noce 1-22 **56-134 partio-godzin na noc**, prawie
  wszystko "obudzeni cudza reka" (np. noc 3 N46: 134, w tym obudzeni 133); od nocy 24 (N492) **0-4** - w polowie d1-30 wyraznie
  sie urywa (w tym samym czasie liczba lordow w polu skacze 315 -> 546, N446 -> N492; przyczyny log nie podaje - do wyjasnienia w B).
- Namioty zdjete po zjezdzie: 2.1 / 3.4 / 4.3 / 2.6 na noc (+1.7 o switaniu, Z33).
- Koszt petli obozu (Z33 linia "NocnyMarsz: swit", "oboz swiata (petla AiNightCamp)"): sr. 3.9 / 8.1 / 8.7 / **9.0 ms na tick**, maks. 18-37 ms
  (L306-L35797) - przy 6 tickach na noc ok. 54 ms/dobe; do 8.17 (optymalizacja), nie do 7.6.

**AI placi za nieprzespana noc - JUZ DZIALA (T10 + MUSZTRA-p, 09.10):**
- L35797 (doba 120): ksiega snu AI 643 partii; nowy dlug 1/2/3 10/1/0; splaconych 92%; "z dlugiem teraz" 10/1/0.
- Na dobe: nowy dlug 2.3 / 9.9 / 11.9 / 9.9 (razem 1 019 w 120 dob); z dlugiem 2.5 / 10.1 / 12.3 / 10.2 (maks. 37); **dlug 3 - 0 przez caly bieg**.
- Kara dziala jak u gracza: L35498 / L35798 `NocnyMarsz: kara - probka 3 z 14 dluznikow AI: ... dlug 1: predkosc 3.00 -> 2.25 (-25.0%,
  tablica -25%), morale 58.6 -> 44.0 (-25.0%) ... dlug 2: predkosc 2.97 -> 1.78 (-40.0%) ... zgodnosc z tablica +-1 pp: 3/3`.
- Szly noca z powodem (partii na dobe): 3.5 / 11.2 / 15.8 / 15.3; partio-godziny nocnego marszu na dobe: ucieczka 9 / 18 / 30 / 29,
  poscig 2.6 / 11 / 13 / 10, odsiecz 1.8 / 15 / 16 / 22. Wymuszony odpoczynek (T10-R) 0.07-0.23 partii na dobe.
- Martwy klucz z audytu 07 (`07-mapa-czas-obozy.md:191-193`, pkt 8 "AiNightsAwakeInChase nigdzie nieczytany") jest **nieaktualny** -
  czyta go `NightMarch.cs:504` (prog dlugu dla poscigu i odsieczy). Pozycja PLAN 7.6 "AI placi za nieprzespana noc" - do zamkniecia
  (sprawdzenie w B, czy nie zostala jakas luka, np. karawany - ksiega obejmuje tylko "partie lordow AI").

**Zbiorka (czlonkowie armii w drodze do wodza):**
- **Brak jakiegokolwiek pomiaru w logach.** Wodzow armii w polu 1.7-7.9 na godzine, wodzowie w ruchu w oknie obozu 0.5-2.3 partio-godzin
  na noc (z czego "bez powodu" 0.5-2.1, Z33 L306-L35797: razem 192 partio-godzin w 120 dob, bez powodu 160). Czlonek armii nie bedacy
  wodzem jest pomijany przez oboz (kod: przyczyna "armia" - B potwierdzi linie), wiec idzie noca - ile, log nie mowi.
- Miara marszu wodzow armii (Z33 "Miara: marsz", linia `| wodzowie armii: N partii`): 1.3 / 5.8 / 7.1 / 5.7 partii dziennie,
  km/dobe mediana 124 / 123 / 103 / 101 (L382-L35849) - tempo bez zmian (7.2 odrzucone), tylko tlo.

### 1.3 (7.7) Turnieje, sojusze, rozejmy - czas

**Turnieje - mocny dowod z Z33.** Linia `Szczelnosc (164a)` liczy co dobe wyplaty nagrod turniejow gry ("turniejow: gra chciala X,
zaplacili Y, uciete Z (N wyplat ...)"). Razem **72 wyplaty w 120 dob** (L620-L36077), ale:
- doby 2-35: 51 wyplat w 22 dobach (L915-L10629); **doby 36-96: ZERO przez 61 dob** (L10932-L28898 - zadnej doby z wyplata);
  doby 97-120: 21 wyplat (L29199-L36077). Okna: 1.47 / 0.23 / **0.00** / 0.70 wyplaty na dobe.
- Przy roku 364 (sezon 91 dni = 13 tygodni po 7 dni, doba 1 = poczatek sezonu) wyplaty wypadaja tylko w tygodniach 0-4 sezonu
  (sezon 0: tyg. 0-4, sezon 1 od doby 92: tyg. 0-4). To zgadza sie z `docs/AUDYT-KALENDARZ.md:169`: start turnieju gry tylko gdy
  `hash % 3 == GetWeekOfSeason` (`DefaultTournamentModel.cs:27`) - mozliwy tylko w tygodniach 0-2 sezonu; przy 13 tygodniach
  "3 z 13 (77% czasu bez nowych turniejow)"; turniej trwa 10-15 dni (`AUDYT-KALENDARZ.md:170`), stad wyplaty do tygodnia 4.
- GrandTourney: log 120-dobowego biegu nadpisany biegiem zapisu 9 dob (`Modules/GrandTourney/GrandTourney.log`, start 03:21:36) -
  liczby zjazdow z autotestu brak. W logu 9 dob: zjazdy w New Ibbish, Ibben, Pyke, Lordsport (LOKALNY - "krolestwo wojuje"), Kyth
  ("zdjety - wrog u bram"), Seagard. GrandTourney liczy w dniach (`GatherDays 4`, `HostCooldownDays 365` - juz prawdziwy rok,
  `AUDYT-KALENDARZ.md:171`).
- Utarg turniejow (Z33 "Szczelnosc"): gra chciala 29 796 zl, zaplacono 29 644 (platnik z nadwyzka) - pieniadz ma platnika (E5).

**Sojusze i rozejmy - w logach nie ma ani jednej linii** (CS33: 0 trafien "allian|sojusz|truce|rozejm"; Z33 tez). Jedyny pomiar to
KRONIKA WOJEN w CS33 (120 linii dobowych, CS1593-CS4359):
- wojen naraz: doba 1: 2, doba 10: 3, doba 20: 3, **doba 30: 16**, potem 14-17 (maks. 17); 18 par wojujacych w 120 dob (2 od startu, 16 nowych, do tego 3 ponowne wypowiedzenia tej samej pary), 5 pokojow.
- Pokoje i ponowne wojny tej samej pary (doby wedlug KRONIKI, +-1 do dob Z33):
  Qarth/Volantis - wojna od d22 (CS2519), pokoj d66 (CS3334), **znow wojna d90** (CS3790; przerwa 24 dni), pokoj d112 (CS4214);
  Stormlands/The Vale - od <=d45, pokoj d73 (CS3467), **znow wojna d94** (CS3873; przerwa 21 dni);
  Lys/Pentos - pokoj d68 (CS3371); **Myr/Tyrosh - wojna d96-d105 (10 dni, CS3910-CS4081), pokoj d106 (CS4101), znow wojna d109
  (CS4158; przerwa 3 dni)**.
- Ustawienia Diplomacy u Jeffa NIE byly przeliczane na rok 364:
  `Configs/ModSettings/Global/Diplomacy/DiplomacySettings_v1.2.json:27` MinimumWarDurationInDays 21, `:28` DeclareWarCooldownInDays 21,
  `:31` MinimumAllianceDuration 42, `:33` NonAggressionPactDuration 84, `:52` MinimumTimeSinceLastCivilWarInDays 240,
  `:53` MaximumFactionDurationInDays 120 - wartosci domyslne moda (pod rok 84 dni). Gra: sojusz maks. 84 dni, udzial w wojnie
  sojusznika 42 dni (`AUDYT-KALENDARZ.md:124-125`, ZLE); rozejm z barteru pokoju `Years(1)` i rozejmy BK `YearsFromNow` -
  AUTO 364 (`:126`, `:133`).
- **Wniosek:** przerwy 21 i 24 dni zgadzaja sie z cooldownem Diplomacy 21 dni (rok 84 = 1/4 roku, przy 364 = 3 tygodnie).
  Przerwa 3 dni (Myr/Tyrosh) i wojna krotsza niz 21 dni lamia nawet ustawienie 21 - albo wojne wypowiedziala druga strona /
  inny mod (ROT, BK, nasz E1b "pokoj z biedy" CS291), albo minimum Diplomacy nie obejmuje AI. Do wyjasnienia w B (kod), nie zgadywac.
- Aktywny model turniejow w grze to `NavalDLC.GameComponents.NavalDLCTournamentModel` (CS1514, CS1555; GrandTourney lata oba:
  `Lata na koniec turnieju: DefaultTournamentModel` i `NavalDLCTournamentModel`) - latka 7.7 na start turnieju musi trafic tez w NavalDLC
  (do sprawdzenia w B, czy NavalDLC dziedziczy `GetTournamentStartChance` z Default).

### 1.4 (7.10) Plonace wioski - komunikat, kto spalil, wyglad spalonej

- W33: **346 ogni w 120 dob** (pierwszy W160 "ogien nad Greenbridge (okreg Wrath Village, kolejnosc 0) - rabuje Ralph Buckler, Lord of
  Bronzegate's Party (ksiega)", ostatni W842), 343 zgaszone; Z33 L36104 `Wioski: ... ognie od wczytania 346 (teraz 3), dym 69 (teraz 12);
  potkniecia: ... ogien 0 ... dymek 0`. 341 z 346 ogni z ksiegi (E4/11), 5 z lancucha gry.
- Ogien chodzi po wioskach okregu: kolejnosc 0 - 260 razy, 1 - 54, 2 - 32 (druga i trzecia wioska tej samej sesji rabunku).
  66 okregow; najczesciej Jamayeh 29, Sagolina 19, Hunab 17, Parchments 14, Stormwind 12, Haybale 12. 174 roznych rabusiow
  (najwiecej Qatho Volentin's Party 8) - **"kto pali" jest juz w logu przy kazdym ogniu**, brak go tylko w grze (komunikat, dymek).
- Ksiega wiosek (Z33 "Ksiega wiosek (E4/11)", L389-L35852), srednio na dobe w oknach:

| Miara | d1-30 | d31-60 | d61-90 | d91-120 | maks. dnia |
|---|---|---|---|---|---|
| plonie (wiosek naraz) | 0.6 | 2.0 | 3.9 | 3.5 | 10 |
| **spalone (>= 50% osad wioski pustych)** | **0** | **0** | **0** | **0** | **0** |
| opuszczone / w odbudowie / odbudowane dzis | 0 | 0 | 0 | 0 | 0 |
| czesciowo puste (wiosek) | 2.6 | 15.9 | 38.6 | 64.4 | 70 |
| okregi z pustymi osadami | 4.9 | 25.1 | 53.1 | 74.7 | 79 |
| pustych osad (stan) | 3.0 | 14.3 | 42.3 | 80.5 | 97.6 |

- `wioski-okregi.csv` (4 732 wierszy, 117 dob): kolumna `spalone` = 0 w kazdym wierszu; `gracz` = 0 we wszystkich okregach (autotest
  nie ma wlasnej wioski). Najwyzszy udzial pustych osad w okregu 6.0% (Kytharo / Rathylar, dzien 108901); najwiecej pustych osad
  4.58 ze 132 (Red Mines, dzien 108951).
- **Wniosek:** w 120 dob zadna wioska nie doszla do progu "spalona" (decyzja Jeffa: >= 50% osad wioski, histereza 3% - CHANGELOG drzewa
  e5/wsie:1305). Wyglad spalonej i "spalona N dni temu" w dymku nie maja dzis w autotescie zadnego przypadku - test 7.10 wymaga
  wymuszenia (np. rabunek do konca w jednym okregu) albo dluzszego biegu. Komunikatu "Twoja wioska plonie" autotest nie sprawdzi
  (gracz bez lenna) - do testu Jeffa albo z wymuszonym lennem.

### 1.5 (7.11) Rabunki

| Miara (srednio na dobe) | d1-30 | d31-60 | d61-90 | d91-120 | razem 120 dob | Zrodlo |
|---|---|---|---|---|---|---|
| sesje rabunku (ksiega) | 1.4 | 5.8 | 7.9 | 6.7 | 656 | L389-L35852 |
| uderzenia (kroki rabunku) | 5.6 | 18.0 | 35.3 | 30.7 | 2 685 | jw. |
| nowe puste osady | 0.22 | 0.58 | 1.38 | 1.13 | 99.4 | jw. |
| wsie gry w stanie Looted | 0.9 | 4.2 | 10.0 | 11.4 | maks. 21 | jw. |
| wsie w rabunku lub spladrowane | 1.7 | 7.4 | 14.1 | 16.7 | maks. 26 | "Oblezone (E5/A4)" L624-L35779 |
| wsie rabowane dzis | 1.3 | 4.4 | 7.4 | 6.3 | 580 | "Ludzie", L1517-L36096 |
| trafieni ludzie | 60 | 144 | 343 | 281 | 24 676 | jw. |
| z tego zabici | 1.3 | 4.6 | 11.6 | 10.1 | 824 | jw. |
| z tego do lasu | 2.2 | 5.0 | 11.7 | 9.4 | 842 | jw. |
| z tego uchodzcy | 57 | 135 | 320 | 262 | 23 010 | jw. |
| kroki rabunku pod spustoszeniem E4/5 | 4.7 | 14.9 | 30.3 | 26.4 | 2 283 | "Hearth (E4/0)" L634-L36086 |
| **lup gry "z niczego" (zl)** | 228 | 519 | 1 056 | 896 | **80 767** | jw. |

- Rabunek gry dalej daje lup "z niczego": L36086 `rabunek gry -0.00 (krokow 0, wsi 0; lup z niczego 1710 zl, w tym gracz 0; pod spustoszeniem
  E4/5 krokow 51 - hearth w ksiedze, lup 1710 zl)`. Ok. 670 zl/dobe w 120 dob, rosnie z wojna. Ludzie i hearth sa juz w ksiedze (E4/5),
  zloto nie - to otwarte zrodlo "z niczego" (zasada zamknietej ekonomii). Moze nalezec do 7.11 albo do "malych zrodel zlota" etapu 5
  (STAN-PRAC:1277) - **do rozstrzygniecia, zeby nie liczyc dwa razy**.
- Rabunek osada po osadzie juz czesciowo jest (E4/11): ogien przechodzi na kolejna wioske okregu (kolejnosc 0/1/2 wyzej), ale wedlug
  mechaniki gry (pasek rabunku wsi), bez "dni na osade" i bez pytania gracza po kazdej osadzie (decyzja Jeffa 07.10 - rozdz. 2).
- Wszystkie rabunki w Z33 robi AI: lup gracza 0 zl w 119 z 119 dob ("w tym gracz 0", L634-L36086). Regiony najbardziej spustoszone w dobie 120 (L36096): Rathylar 3.91% uchodzcow, Stonedance 1.61% ...

### 1.6 (7.12) Bliznaki - ruch

- Licznika ruchu przez przeprawe (projekt T2 z `audyt-2026-10-09/10-pomysly.md:351`: "Przeprawa Bliznaki: karawany N (kiesy X zl),
  wozy wsi N, partie rodow N (wlasne / obce krolestwo), gracz tak/nie, myto gdyby bylo Z zl") **nie ma**.
- Jest tylko zastepczy pomiar 169c: "Miara historyczna cz. 2 ... (e) Bliznaki: wejscia do osady N (...)" - wejscia DO miasta, nie
  przejscia przez most. 118 dob z wpisem (L556-L36012): **503 wejsc = 4.3 na dobe** (okna 3.2 / 3.7 / 5.3 / 4.8, maks. 11):
  karawany 344 (68%), wiesniacy 119, lordowie 31, inne 9.
- CrossingLaw (30.08, CHANGELOG drzewa e5/wsie:12282) zatrzymuje tylko partie w WOJNIE z panem przeprawy (Bliznaki `ROT_town3`, Fosa
  Cailin `castle_B1`, Krwawa Brama `castle_EN2`; `10-pomysly.md:45`). W Z33 zadnej linii CrossingLaw w dobie - zawrocen nie liczymy.
- Myto: okno ksiegi obiegu "myto 17020/0" (L36056: 17 020 wywolan, 0 trafien) - dzis zadne myto nie plynie; "Road Tolls 0 z 406" w linii
  Szczelnosc (L36084) to podatek krolestwa BK z kas miast krola (`10-pomysly.md:70`), nie myto przeprawy.

### 1.7 Tlo: marsz bez zmian (7.2 odrzucone)

- Z33 "Miara: marsz" (partie > 300 ludzi albo wodzowie armii, >= 12 h ruchu): 14-33 partii dziennie, km/dobe mediana 138 / 137 / 134 / 131
  (L382-L35849; WorldPace z MCM 75%). Ruch samotnych lordow w oknie obozu 0-6: 101 / 59 / 70 / 75 h na dobe (17-74 partii).
- Wojny: 2 -> 14-17 rownoczesnie od doby 30 (CS1593-CS4359). Oblezone naraz: zamki 0.2-2.0, miasta 0-0.2 (Z33 "Oblezone").

---

## 2. Decyzje Jeffa i ustalenia (data, cytat, plik:linia)

Skroty: SP = `docs/STAN-PRAC.md`, PL = `docs/PLAN-DO-KONCA-MODA-2026-10-09.md`, CH = CHANGELOG drzewa kodu
`scratchpad/e5/wsie/CHANGELOG.md`, a07 / a08 / a10 = `docs/audyt-2026-10-09/07-..., 08-..., 10-...`, ES = `.../ESENCJA-AUDYTOW-2026-10-09.md`.

### 2.1 Caly etap 7 i zasady (wiazace)

| Data | Decyzja / cytat | Gdzie |
|---|---|---|
| 12.10 10:35 UTC | "olej predkosc, zostaw predkosc poruszania sie jak jest" -> 7.2 i 7.3 ODRZUCONE, reszta etapu 7 bez zmian | SP:1252; PL:201, 206-207 |
| 12.10 10:30 UTC | (uchylone 5 min pozniej) "tempo marszu O POLOWE wolniej niz dzis (nie do wartosci z ksiazek - za wolno)"; etap 8 takze optymalizacja (8.17) | SP:1250; PL:242 |
| 12.10 10:30 UTC | NOWA KOLEJNOSC: "wgrac wszystko z etapow 5, 6, 7 i 8 i potem testowac dokladnie i naprawiac" - kazda paczka z recenzja i wylacznikiem MCM, jeden test na koncu | SP:1263-1266 |
| 12.10 10:25 UTC | etap 8 (drobne bledy) NIE "przy okazji" - nie dokladac do paczek etapow 6/7 | SP:1248 |
| 12.10 09:50 UTC | "nie mamy na sile i sztucznie robic dobrze lordom ..." - podloga 85 tys. z etapu 2 przestaje byc warunkiem (tylko pomiar) | SP:1242-1244 |
| 12.10 00:20 UTC | "nie robmy sztucznych praw; jesli ubywa, to ubywa, jesli przybywa, to przybywa" | SP:1197 |
| 12.10 (WGRANIE 33) | "olej zime, jak wszystko wgramy, bedziemy sprawdzac zime" | SP:1260 |
| 09.10 rano | (UCHYLONE 12.10) "tak" tempo swiata wariant ksiazkowy, ale po naprawach ekonomii | SP:752-753, 761 |
| 09.10 04:15 | stala zgoda: wgrywaj po udanym autotescie (kopia, md5, wpis) | SP:838-840 |

### 2.2 Wedlug pozycji

**7.1 Blokady tempa, jedna skala czasu** - brak osobnej decyzji Jeffa. Zrodla: a07 rozdz. 3 pkt 1-6 i 14 (`07-mapa-czas-obozy.md:176-206`),
`00-AUDYT-SWIATA-2026-10-09.md:541` (E4 "TEMPO SWIATA 2: suwak jako mnoznik wyniku, podloga 1.0 x p, kary terenu x p, szacunki drogi AI ...
przy 75% prawie bez skutku; jedna skala 4.75 w kodzie i dokumentach (08 M-1); komentarze roku 168 -> 364").
**Uwaga po 12.10:** T-1 (suwak jako mnoznik) zmienia predkosc jazdy przy 75%: "jazda 4.16 -> 3.86 (-7%), piechota bez zmian" (a07:232) -
to jest zmiana tempa, ktorej Jeff nie chce; pozostale (podloga, kary x p, szacunki AI) przy 75% "prawie bez skutku".
Jedna noc: T1 ujednolicil oboz (CH:5536), ale zostaly osobne godziny - menu snu 21-5, `SightRange` 21-5, kara nocna gry 22-02,
warsztaty 23-5 (CH:5544 "Bez zmian: ...").

**7.4 Kary terenu** - brak decyzji Jeffa; blad potwierdzony w kodzie 04.10: `AUDYT-KONFLIKTY.md:176-180` (S7). Pokrewne pomysly (nie decyzje):
most liczony jak brod (a07:203-204), N-5 "most szybszy niz brod" P2 (a07:287). Historia kar: CH:9922 (13.09 "nowe kary terenu, bagno"),
CH:10671 (02.09 "kary terenu plaskie (-0.25/-0.5)").

**7.5 Drogi** (wszystko PRZED 12.10):
- 09.10 ok. 00:10: "H proba drog TAK + po drodze szybciej (+15%) TAK" (SP:773).
- 09.10 ok. 03:45: "21 drogi: 'pokaz mi najpierw; ma wygladac rozsadnie - gdzie wioska, tam droga, a nie przez pole'" (SP:816).
- 09.10 ok. 04:25: "20 drogi - najpierw pokazac proba (zdjecia), 'gdzie wioska, tam droga'" (SP:841); PL:248 "Zostaje tylko 20 - ile drog:
  po obejrzeniu proby (1.13), przed 7.5"; ES:214 pytanie 20 "Ile drog rysowac ... - Zdecydowac po obejrzeniu proby".
- Proba drog P2 zrobiona 08.10 (`PRZEKAZANIE-2026-10-08.md:158, 248`, galaz `w-toku/p2-drogi-proba`, plik `arm_map_roads_probe.tsv`), ale
  zablokowana i niepokazana (`RAPORT-NOCNY-2026-10-09.md:60, 90`); w SP nie ma sladu, ze Jeff ja obejrzal -> **1.13 otwarte, pytanie 20 otwarte**.
- Fakty z a08: "Dzis zadna droga (ani ROT, ani nasza) nie przyspiesza marszu" (a08:36); premia dotknelaby ok. 1/3 marszu armii miedzy
  twierdzami (32%, mediana 27%) i ok. 1/2 drogi chlopskich wozow (55%) (a08:200-201); M-2: +15% w promieniu 0.75 jedn. od drogi malowanej
  ROT (a08:264); BKROTPatch daje +15% za blogoslawienstwa wiary wedlug terenu, za droge nikt (a08:121).
- **Sprzecznosc do rozstrzygniecia:** "+15% TAK" (09.10) zapadlo przy planie spowolnienia swiata ok. 5x (a07:13, T-7 a07:238), gdzie droga miala
  oddac czesc tempa. Po 12.10 ("zostaw predkosc jak jest") +15% tylko PRZYSPIESZA swiat (z udzialow a08: armie ok. +5% calej trasy,
  wozy ok. +8%) i kosztuje odczyt rastra przy liczeniu predkosci. Zgodnie z poleceniem - pytanie do Jeffa z rada w projekcie.

**7.6 Obozy:**
- 09.10 (noc): "jak armia idzie, to trzeba rozbic oboz - daj godziny od 24 do 6 rano" (a07:3-4) -> T1: oboz swiata 0-6, wodz armii zawsze
  obozuje, straznik na zegarze gry (CH:5536; w WGRANIU 1, SP:727).
- 09.10 ok. 04:00: samotne kolumny "maszeruja noca, gdy trzeba - spiesza sie, by przerwac rabunek, uciekaja przed armia - ale maja miec
  takie same kary jak gracz" (SP:830-831) -> T10 (CH:4632, poprawki CH:4586).
- 10.10 ok. 06:00: "musza odpoczywac; forsowny marsz najwyzej 2 dni i tylko w sytuacjach wyjatkowych" (SP:991-994) -> T10-R (WGRANIE 19, SP:1031).
- 09.10 ok. 04:25: "M namioty i ogniska przy napadzie na oboz - WLACZYC" (SP:841).
- 09.10 ok. 03:45: "20 zima: a (dluzsza noc obozu zima) NIE" (SP:815) - godziny obozu zima bez zmian.
- Otwarte z a07 (techniczne, bez decyzji): O-3 prefiks na `SetMove*` u spiacych (a07:220), O-7 czlonkowie armii w drodze na zbiorke spia
  (a07:224; blad 9, a07:194-195). Z logu: 91 obudzen na noc (rozdz. 1.2) - O-3 wciaz potrzebne.

**7.7 Turnieje, sojusze, rozejmy** - **brak decyzji Jeffa** (grep SP / HISTORY / CH "turniej|sojusz|rozejm|Diplomacy": tylko reparacje 09.10
SP:755-757 i Call to War SP:997-998). Propozycje z 04.10: `AUDYT-KALENDARZ.md:303` (turnieje: postfiks `GetTournamentStartChance`, tydzien
z `ToWeeks % 3`), `:304` (sojusze gry 84 -> 364, 42 -> 182), `:305` (Diplomacy MCM: pakt 84 -> 364, sojusz min 42 -> 182, wojna min 21 -> 90,
cooldown 21 -> 90, wojna domowa 240 -> 1040, frakcja 120 -> 520). Zmiana pliku Diplomacy Jeffa - jak 09.10 przy reparacjach: gra zamknieta,
kopia `.bak` + D: (SP:755-757) i zgoda Jeffa (to jego ustawienia).

**7.8 / 7.9 Wioski - lore, dymek, menu:**
- 08.10 ok. 02:20: "bierzemy wszystkie 8 do wiosek": dymek z panem i herbem, proporczyk, rycerz, czym zyje, wiara, historia, pamiec wojny
  (kto spalil, kiedy, ilu ucieklo), menu okregu (SP:377-379; `PROJEKT-WIOSKI-LORE-2026-10-08.md:3-9`).
- 07.10 ok. 11:10: kazdy obrazek to WIOSKA Z NAZWA ("Wioska XYZ spalona", "plonie na mapie") (SP:425-427).
- 09.10 ok. 04:25: "L napisy na mapie tylko nad SPALONYMI wioskami" (SP:841).
- Zrobione: T7 (09.10) dymek "A village of the X lands, held by House Y" (CH:5444); E4/11 dymek ze stanem i menu okregu w "People of this
  land" (CH:1309-1310; odstepstwo od WM 1.6 - nie osobna opcja "Look over the district", CH:1321). Dane lore 2 446 wiosek "gotowe" 09.10
  (`RAPORT-ODPOWIEDZI-2026-10-09.md:18`), 13 poprawek P1-P13 przed zamrozeniem (`PROJEKT-WIOSKI-LORE-2026-10-08.md:49, 453`);
  w `Modules/Armoury/ModuleData` nie ma `arm_map_village_lore.tsv` (jest tylko `arm_map_villages.tsv`) - 7.8 niewgrane.

**7.10 Plonaca / spalona wioska:**
- 11.10 13:50-14:00: "wyglad spalonej wioski - etap 7 (7.10)" (SP:1182).
- 09.10 ok. 00:00: "ODBUDOWA SPALONEJ WIOSKI: 1 ROK (nie 2-3)" (SP:760) - jest w E4/5 + E4/11 (powrot najpozniej 364 doby po najezdzie).
- Spalona = >= 50% osad wioski pustych (decyzja Jeffa), histereza 3% (CH:1305).
- "pamiec wojny (kto spalil, kiedy, ilu ucieklo)" - punkt 7 z 08.10 (`PROJEKT-WIOSKI-LORE-2026-10-08.md:8`).

**7.11 Rabunek osada po osadzie:**
- 07.10: "RABUNEK OSADA PO OSADZIE - po kazdej osadzie komunikat 'spalono to i to, rabowac kolejna? zajmie to tyle czasu'; jedna osada to
  DNI (dojazd, przeszukanie, spalenie), caly okreg tygodnie; AI tak samo" (SP:461-463); 07.10 ok. 11:10 przyjal projekt (SP:425).
- Projekt RB: `PROJEKT-RABUNEK-OSADA-PO-OSADZIE-2026-10-07.md:3, 12` (50 ludzi ok. 10 dni na osade, 112 ok. 4.5, 300 ok. 2; okienko
  "Raid the next settlement (about N days)" / "End the raid", RB:53). Liczby RB sa sprzed E4/5: zabici 5% (RB:174), dzis 2% (przy niewoli 5%) (L122).
- 07.10: Jeff pyta o dane historyczne rabunku (R 0.5 czlowieka na napastnika na dobe) - "szacunek projektowy", kalibracja po badaniu (SP:442-443).
- PL:301: 7.11 jest NK (w starym zapisie dziala czesciowo).

**7.12 Bliznaki:**
- 30.08: "jak to mozliwe, ze jako wroga armia moge przejsc przez Twins?! Ten, kto ma zamek, przepuszcza sojusznikow, a wrogow nie!"
  -> CrossingLaw (CH:12282).
- 09.10 ok. 03:45: "22 'nie', 23 'tak' - NIEJASNE przypisanie (samotne kolumny / Inni w 1. roku / myto) - zapytane" (SP:816-817);
  PL:248 zapisuje wynik "myto: TAK (po liczniku ruchu, 7.12)". Rada z ES:215: "na Bliznakach 2 zl od czlowieka i 1 zl od konia ... placisz
  tez Ty, lordowie Freyow nie?" - "Tak, po pomiarze ruchu". **Wyjasnienia przypisania w SP nie znalazlem** - przed mytem (nie w 7.12) potwierdzic.
- Projekt licznika: a10:351 (T2); myto Z3 po T2 i odpowiedzi Jeffa (a10:361).

---

## 3. Co jest nieaktualne (nie budowac na tym)

| Miejsce | Co | Dlaczego |
|---|---|---|
| PL:218 | "Winterfell - Krolewska Przystan ok. 77 dni" | liczba z wariantu 20% tempa (a07:25-27, T-7) pod 7.2 - odrzucone 12.10 |
| PL:218 | "test roczny po zmianie tempa spelnia progi etapow 2 i 5" | tempa nie zmieniamy; test = jeden test zbiorczy na koncu (SP:1263); podloga 85 tys. etapu 2 tylko pomiar (SP:1242-1244) |
| PL:218 | "po drodze szybciej o 15%" | decyzja 09.10 sprzed "zostaw predkosc" - do ponownego pytania (rozdz. 2.2, 7.5) |
| PL:201 | cel "drogi przyspieszaja; drogi przyspieszaja" | powtorzone slowa (blad edycji) i ta sama watpliwosc co wyzej |
| PL:259 | L "Napisy ... tylko nad plonacymi i spalonymi (rada) czy wszystkie z bliska? - przed 7.9" jako otwarte | Jeff odpowiedzial 09.10 04:25: "tylko nad SPALONYMI" (SP:841) - inaczej niz rada |
| PL:260 | M "Namioty i ogniska przy napadzie na oboz - wlaczyc czy wyciac? - etap 7" jako otwarte | Jeff 09.10 04:25: "WLACZYC" (SP:841) |
| SP:752-753, 761 | "tempo swiata wariant ksiazkowy, ale po naprawach ekonomii" | uchylone 12.10 10:35 (SP:1252) |
| SP:1250 | "tempo marszu O POLOWE wolniej" | uchylone 5 minut pozniej (SP:1252) |
| a07:228-240, 334-345 | T-1..T-8 "tempo swiata 2", suwak 75 -> 20, osobne tempo wsi / karawan / morza | T-7 odrzucone; T-1 zmienia jazde -7% (sprzeczne z 12.10); T-5/T-6 mialy sens tylko przy spowolnieniu |
| a07:14-17, 100, 304 | oboz AI 22-5, pytanie gracza o 21:00, "szesc nocy" | od T1 (09.10) oboz 0-6 (CH:5536, L308); zostaly 4 inne godziny nocy (rozdz. 2.2, 7.1) |
| a07:107, 182-186 (pkt 5), 191-193 (pkt 8), 201-202 (pkt 12) | straznik na sekundach realnych; martwy `AiNightsAwakeInChase`; meldunek kursu -20% | naprawione: T1 (straznik 0.1 h gry, meldunek), T10 (`NightMarch.cs:504` czyta klucz) |
| a07:122-127 | 39 218 obudzen/rok, zjazd sr. 1.29 / maks. 5.69 | stan sprzed T1; dzis 91/noc, zjazd sr. 0.40 / maks. 1.00 (rozdz. 1.2) |
| `AUDYT-KALENDARZ.md:169` | "dzis 3 z 6" tygodni z turniejami | rok 168 (6 tygodni w sezonie); aktualne jest "przy 364 3 z 13" - potwierdzone w Z33 (61 dob bez turnieju) |
| `AUDYT-CZAS-MAPA.md:315` | "Przy roku 365 zamiast 168 ..." | dokument 04.10 liczy oba kalendarze - kazda liczbe sprawdzic, ktorego roku dotyczy |
| `HISTORY.md:20, 45` | "rok ma 168 dni", os fabuly x `FabulaTimeScale` 2.0 "bo rok wydluzylismy do 168 dni" | rok 364 od wpisu 11 (CH:8007); HISTORY.md nieuaktualniony |
| a07:205-206 (pkt 14), a08:210 | komentarze `WorldPace.cs:14-22`, `Settings.cs:313` o roku 168 | do sprawdzenia w kodzie (B); poprawka tekstu = drobiazg etapu 8, nie 7 |
| RB:174, RB:12 | zabici 5%, "50 ludzi ok. 10 dni na osade" | liczone na paczce 113; E4/5 ma zabitych 2% (przy niewoli 5%) i ksiege wiosek (L122, L125) - przeliczyc |
| SP:816-817 | 22 "nie" / 23 "tak" NIEJASNE (myto) | PL:248 zapisuje "myto: TAK" bez cytatu wyjasnienia - potwierdzic przed mytem |

---

## 4. Propozycja nowego "Skonczone, gdy" (zamiast PL:218)

Jeden test zbiorczy wedlug zasady Jeffa 12.10 (120 dob nowej kampanii + zapis 9 dob + zima + bieg dlugi, SP:1263-1266). Kazdy punkt ma linie
w logu (nowa albo istniejaca). Liczby "dzis" z Z33.

1. **Tempo bez zmian (7.2 odrzucone):** "Miara: marsz" km/dobe mediana partii > 300 ludzi w +-5% od dzis (131-138; L382-L35849), o ile Jeff
   nie przyjmie premii za droge (7.5); przy premii: wzrost tylko na odcinkach drog (osobna linia: udzial ruchu "po drodze", srednia premia).
2. **7.4:** nowa linia dobowa zwrotu kar: 0 partii ze zwrotem kary, ktorej gra nie nalozyla (las przy ForestKin, pustynia przy
   AseraiDesertFeat, noc, snieg), przy niezerowej liczbie partii AI w lesie / na pustyni (dzis brak pomiaru - gracz zawsze `Plain`).
3. **7.6:** obudzonych cudza reka na noc - z 91 do bliskiej zera (rozkaz nie dochodzi do spiacego, O-3) albo wprost "rozkazow
   zatrzymanych N, oddanych o 6:00 N"; nowa linia zbiorki: czlonkowie armii w drodze do wodza w oknie 0-6 (ilu, ile h ruchu) - spia;
   dlug snu 3 u AI = 0 (jak dzis), 0 potkniec straznikow.
4. **7.7:** zadne okno >= 14 dob bez wyplaty turnieju gry (dzis 61 dob zera, L10932-L28898); czasy sojuszu, paktu, minimum wojny i przerwy
   miedzy pokojem a nowa wojna tej samej pary w skali roku 364 (liczby po decyzji Jeffa) - linia KRONIKI z para, dniem pokoju i dniem nowej
   wojny (dzis przerwy 3 / 21 / 24 dni, CS4101-CS4158, CS3467-CS3873, CS3334-CS3790).
5. **7.8 / 7.9:** 2 446 wiosek z danymi lore wczytane (0 odrzuconych), dymek z herbem / rycerzem / wiara / historia, napisy tylko nad
   spalonymi (decyzja L), 0 potkniec widoku i dymka (dzis 0, L36104), koszt widoku nie wiekszy niz dzis.
6. **7.10:** przy wymuszonym spaleniu (>= 50% osad) - wyglad spalonej, dymek "spalona N dni temu przez X", komunikat graczowi o JEGO wiosce
   z nazwa sprawcy (dzis 0 spalonych w 120 dob - test musi to wymusic); 0 potkniec.
7. **7.11:** rabunek osada po osadzie z czasem w dobach na osade (gracz: okienko po kazdej osadzie; AI: ta sama regula, linia "sesja
   rabunku: osad N, dni M"); lup z rabunku ma platnika - "lup z niczego" w linii Hearth = 0 zl (dzis ok. 670 zl/dobe, L634-L36086),
   jesli Jeff nie przypisze tego do etapu 5.
8. **7.12:** linia dobowa "Przeprawa Bliznaki" (karawany, wozy, partie rodow wlasne / obce, gracz, "myto gdyby bylo"), co najmniej kilka
   karawan w 120 dob (dzis zastepczo 344 wejsc karawan do miasta, L556-L36012).
9. **Ogolne:** 0 bledow naszych modow; koszt doby naszych zmian etapu 7 nie wiekszy niz kilka ms (Jeff chce szybszej gry, 8.17;
   dzis sama petla obozu ok. 9 ms na tick, L35797); stary zapis wczytuje sie bez bledu (NK tylko 7.11).

---

## 5. Luki pomiarowe i rzeczy do rozstrzygniecia (dla projektu)

- **7.4:** w logach nie ma ani jednego przypadku - zakres bledu nieznany. Pomiar musi isc po partiach AI.
- **7.6 zbiorka:** zero pomiaru. Skok "ruchu obudzonych" w nocach 1-22 (56-134 partio-h) i spadek od nocy 24 (0-4) - przyczyna nieznana.
- **7.7:** brak linii o sojuszach i rozejmach; wojna Myr/Tyrosh 10 dni i ponowna po 3 dniach lamie nawet ustawienia Diplomacy 21/21 - kto ja
  wypowiedzial, log nie mowi (B: kod Diplomacy / BK / ROT / nasz E1b). Log GrandTourney z autotestu nadpisany (zapis 9 dob).
- **7.10:** zadnej spalonej wioski w 120 dob; autotest bez lenna gracza - komunikatu nie zobaczymy bez wymuszenia.
- **7.11:** lup gry z niczego (80 767 zl / 120 dob) - kto go zamyka: 7.11 czy etap 5 "male zrodla zlota" (SP:1277).
- **7.12:** jest tylko proxy (wejscia do miasta), nie przejscia przez most.
- **Pytania do Jeffa (z rada w projekcie):** (1) premia +15% po drodze po 12.10 (7.5); (2) T-1 suwak jako mnoznik zmienia jazde -7% (7.1);
  (3) ile drog - po obejrzeniu proby 1.13 (pytanie 20 otwarte od 09.10); (4) ustawienia Diplomacy na skale 364 (jego plik); (5) myto -
  potwierdzenie przypisania odpowiedzi 22/23 (po 7.12).
