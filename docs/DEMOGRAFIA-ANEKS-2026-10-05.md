# Demografia - aneks po decyzjach Jeffa (2026-10-05)

Aneks do docs\DEMOGRAFIA-SILA-ROBOCZA-2026-10-05.md (dalej DOK) i docs\EKONOMIA-FUNDAMENT-2026-10-05.md. Nie powtarza ich.

**Jak powstal.** Trzy raporty (spustoszenie; male krainy; widocznosc ksiegi, bandyci, zaraza i najemnicy) oraz krytyk, ktory sprawdzil wszystkie trzy. Aneks jest zlozony wprost z tych raportow. Gdzie krytyk poprawil autora, stoi liczba krytyka z dopiskiem [KRYT], a liczba autora obok. Tylko odczyt: niczego nie zbudowano, nie wgrano i nie testowano w grze.

**Oznaczenia.**
- [KRYT] = poprawka krytyka aneksu; SZACUNEK = liczba z rachunku, nie z pomiaru; [RED] = zlozenie redaktora z zaleznosci, bez nowych liczb.
- SCR = C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad
- SRC = C:\Users\GAME\Bannerlor2-Realm-of-Thrones\Armoury\src
- LOG = C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Armoury\Armoury-2026-10-05_15-22-29.log (doby 108837-108864)
- BIT = C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Armoury\Logs\2026-10-05_15-22-29\bitwy.log
- cs = SCR\ore-supply\cs (TaleWorlds), bk = SCR\ore-supply\bk (BannerKings); twall / bkall / rotall = SCR\audyt-pieniadz\...; naval = SCR\przeglad100-latka\naval.

**Zakres danych.** Jedna sesja (2026-10-05 15:22), 28 dob poczatku kampanii - to nie stan ustalony. [KRYT] CrashScribe skaluje daty wojen fabularnych ROT x4.33 (C:\Users\GAME\Bannerlor2-Realm-of-Thrones\CrashScribe\src\Fabula.cs:245-262; session-2026-10-05_15-22-28.log l.198): wojna powszechna trwa od doby 22 do 433 kampanii (ok. 1.1 roku gry), wiec kazde przeliczenie "x13 z 28 dob" miesza pokoj ze szczytem.

**Miary zabitych w tym aneksie (ta sama sesja, inne zrodlo liczenia).**
- BIT: 480 starc, 11 601 zabitych = 414 dziennie (28 dob; rozdz. 2).
- Linie "Bitwy:" w LOG: 11 248 w 28 dob = 402 dziennie; w ostatnich 7 dobach 4 805 = 686 dziennie (rozdz. 2).
- Zabici wszystkich partii w 27 dobach 108838-108864: 11 377 = 421 dziennie (rozdz. 4).

## 0. Decyzje Jeffa i co z nich wynika

### 0.1 Decyzje z 05.10 (wiazace)

| Decyzja Jeffa | Co z niej wynika | Gdzie |
|---|---|---|
| Regula przyjeta: jedna ksiega ludzi na region (w domu / w sluzbie / wyrzutki / uchodzcy / polegli). Pytanie: "gdzie to mozna bedzie sprawdzic?" | Trzy miejsca: linia "Ludzie:" w glownym logu, plik ludzie.csv do Excela, opcja "People of the district" w menu osady | rozdz. 3 |
| Regula przyjeta: przyrost naturalny z dobrobytu, glodu i niebezpieczenstwa w granicach historycznych | Krok 3 DOK bez zmian | rozdz. 6 |
| Regula przyjeta: rekrut odejmuje czlowieka we wsi werbunku, polegly to ubytek trwaly, zwolniony i dezerter wracaja | Krok 5 DOK zostaje; dochodza bandyci, jency i zgony zarazy obozowej | rozdz. 4, 5, 6 |
| Regula przyjeta: plon zalezy od liczby ludzi | Krok 6a DOK zostaje; dochodzi czynnik spustoszenia (1 - S_v) | rozdz. 1.4, 6 |
| Regula przyjeta: prog wolnych rak 20% mezczyzn (historyczny) | Decyzja o progu zapadla. Na poziomie krolestwa w dobie 108864 ponad 20% mezczyzn pod bronia ma tylko Nocna Straz (62.52%); Sarnor 12.07%, Wolni Ludzie 7.69%. Krytyk DOK: przy 20% czynnik progu (krok 6b) nie zmienia plonu poza Nocna Straza | rozdz. 2.1, 6, 7 |
| Pytanie: "bandyci sa przyjezdni czy z danego regionu i jak ich zabije, to tez ubywaja? a co z kryjowkami bandytow - tam jest ich do kilkudziesieciu - jak ich zabije, tez ubywaja?" | Bandy powstaja tylko z puli wyrzutkow regionu i 4 sasiednich. Zabici ubywaja z puli i band i nikt ich nie dosypuje, ale dzis nie sa zapisani jako polegli. Kryjowka odradza sie tylko z puli | rozdz. 4 |
| SPALONA WIES (zmiana projektu): "to spalenie wsi to jest tylko symbol - to nie jest jedna wies, tylko szereg wsi (...) jak bedzie kilka lat [odbudowy], to wszystkie wioski beda spalone - trzeba to jakos inaczej zasymulowac" | Regula 4.3 i krok 4 DOK ("-39% hearth na lata") zastapione: rabunek pustoszy ulamek okregu zalezny od liczby napastnikow i czasu | rozdz. 1 |
| Zaraza: tylko z oblezen | Losowa zaraza i przenoszenie na sasiadow usuniete z kroku 8 DOK; zostaje zaraza obozowa (CampFever), jej zgony ida do ksiegi | rozdz. 5.1 |
| Najemnicy z karczmy: tylko ze zwolnionych z wojska | Nowe konto "weterani" na region z miastem; karczma pokazuje tylu ludzi, ilu jest weteranow. Najem spada o 81-97% | rozdz. 5.2 |
| Male krainy (Nocna Straz, Wolni Ludzie, Sarnor): "daj mi wiecej danych" | Pelna tabela 29 krolestw, zrodla ludzi, kanon, warianty po 1 / 3 / 5 latach. Decyzja nadal otwarta | rozdz. 2, 7 |

### 0.2 Wyniki w skrocie (po poprawkach krytyka)

1. **Spustoszenie.** Rabunek pustoszy `R x napastnicy x doby` ludzi (R = 0.5 czlowieka na zbrojnego na dobe, widelki 0.1-1.0), z czego 5% ginie, 5% idzie w wyrzutki, 90% to uchodzcy wracajacy 0.08% dziennie. Poczet 300 na okregu 75 tys. to 252 ludzi (0.34%) zamiast 29.6 tys. [KRYT] Typowy napastnik w logu to 112 ludzi: 135 ludzi = 0.18% okregu. Poza pograniczem wzor jest prawie bez znaczenia (okreg-mediana: stan ustalony ponizej 1%).
2. **Male krainy.** Zadna z trzech nie przezyje 3-5 lat przy dzisiejszym tempie strat w zadnym wariancie opartym tylko o wlasna ludnosc. Dla Nocnej Strazy dziala tylko rekrut z calego Westeros. [KRYT] Zaden wariant nie rusza przyczyny, czyli tempa strat.
3. **Widocznosc.** Wszystkie wskazane API istnieja [KRYT]. Piec zmian trzeba rozbic na osobne wpisy: log z CSV, potem menu, potem dymek [KRYT].
4. **Bandyci.** Odpowiedz zgodna z SRC\OutlawLaw.cs [KRYT, 4 drobne poprawki]. Ubytek band w walce i niewoli: 2 032 ludzi w 27 dob (75.3 dziennie); bandyci to 15% zabitych swiata.
5. **Zaraza.** Jedyna zaraza w grze (CampFever) juz dziala wylacznie w oblezeniu. [KRYT] Dlugie oblezenie (30 dni, 1 000 oblegajacych, 300 obroncow) to ok. 25 zmarlych zolnierzy, nie 50.
6. **Najemnicy.** Podaz bylych zolnierzy to 15-70 dziennie wobec 356-610 wynajmowanych; AI nigdy nikogo nie zwalnia, wiec potrzebna osobna regula zwolnien.

### 0.3 Co jest pewne, a co szacunkiem (lista krytyka)

**Pewne (kod, log, XML):**
- Rabunek vanilli: 900 / (sqrt(N) + 5) godzin, -39.7% hearth, 4 zl za hearth z niczego.
- AI rabuje tylko wies Normal; powrot do Normal daje +20 milicji.
- BIT: 53 starcia "Raid" na 15 wsiach, napastnicy 62-339 (mediana 112), 1 265 zabitych obroncow; pelne spalenia tylko w dobach 108859, 108861, 108862.
- Przyklady spustoszenia licza `(1 - S)`, nie `(S_MAX - S)`.
- 5 wierszy tabeli krain i przypisanie kultur.
- Wojny fabularne skalowane x4.33: start doba 22, koniec doba 433.
- Wojsko rodow 66 412 -> 86 432 w jedna dobe; ludnosc w ksiedze 52.6 -> 56.8 mln w 27 dob.
- Wszystkie API z raportu o widocznosci istnieja; bandy powstaja tylko z puli regionu i 4 sasiadow; tlok zarazy liczy sie per partia.
- Historia: liczby 1355 i McNamee potwierdzone u zrodla.

**Szacunki:**
- R = 0.5 i R_F = 0.2, D_MAX, S_MAX, udzialy zabitych i wyrzutkow.
- Odsetek lupien dokonczonych; gorna granica 103 tys. dotknietych rocznie; Wrath Village 4.25% w 20 dob.
- 80-100 rabunkow jednej wsi rocznie po skroceniu stanu Looted.
- Garnizony "dzis" (x1.02-1.06 z obserwacji wobec x1.32 z zoldu); pod bronia 145-158 tys.
- 25-43% przyrostu doroslych mezczyzn Westeros; lata do wyczerpania Ibbenu (2.4) i Tyroshu (2.6).
- Poprawiony wariant Sarnoru "0w".
- Wszystkie przeliczenia roczne z 28 dob.
- Liczby kanonu (tylko streszczenia wyszukiwarki; rozdzialy niesprawdzone).

## 1. Spustoszenie jako ulamek okregu

Projekt z odczytu kodu, logow i zrodel. Rachunki autora: SCR\aneks-spustoszenie\calc.py; krytyka: SCR\aneks-krytyk\k1.py - k8.py.

### 1.1 Wniosek

- **Dzis:** kazdy pelny rabunek zdejmuje ok. -39.7% hearth niezaleznie od liczby napastnikow. Poczet 300 i armia 2000 robia to samo, rozni je tylko czas (1.7 i 0.75 doby).
- **Zarzut Jeffa.** Autor: przy czestosci 0.32-0.43 rabunku na wies rocznie wersja "-39% na 3.4-7 lat" daje 66-95% wsi z blizna naraz i 12.6-16.8% ludzi wsi wygnanych rocznie.
  - [KRYT] Ta czestosc pochodzi z 6 dob szczytu; srednia sesji to ok. 0.09 na wies-rok (4 pelne spalenia w 28 dobach).
  - [KRYT] Rozklad nie jest rownomierny: wszystkie starcia "Raid" padly na 2.6% wsi, wiec liczby "66-95%" nie da sie podac z 27 dob.
  - [KRYT] Zarzut pozostaje sluszny dla pogranicza, gdzie ta sama wies jest bita wielokrotnie.
- **Propozycja:** rabunek pustoszy `R x napastnicy x doby` ludzi, R = 0.5 czlowieka na zbrojnego na dobe (widelki 0.1-1.0). Poczet 300 na okregu 75 tys. to 252 ludzi (0.34%), nie 29.6 tys.
- **Uczciwie:** pojedynczy rabunek pocztu staje sie prawie niewidoczny. Widoczny skutek daje dopiero armia stojaca dniami albo okreg najezdzany kilka razy w roku.
  - [KRYT] Okreg-mediana: jeden rabunek to -0.2%, stan ustalony ponizej 1%. Wzor nie wypala okregu w nierealnym tempie.
- **Warunek [KRYT]:** ze stanem "wies spalona" da sie to pogodzic po naprawie czterech rzeczy (1.7), a calosc ma sens dopiero po krokach 2 i 3 DOK (1.8).

### 1.2 Historia

| Miara | Dol | Srodek | Gora | Podstawa |
|---|---|---|---|---|
| Ludzi dotknietych na zbrojnego na dobe | 0.1 | 0.5 | 0.9 | SZACUNEK, rachunek nizej |
| Gospodarstw na zbrojnego na dobe (4.5 osoby) | 0.02 | 0.11 | 0.2 | jw. |
| Wsi na 1000 zbrojnych na dobe | - | 1.6 | - | 500 wsi / (5 500 x 58 dni), Langwedocja 1355 |
| Zabici wsrod dotknietych | 2% | 5% | 15% | SZACUNEK z Easingwold 1326 |
| Odbudowa czesci spalonej | 2-3 lata | 5-8 lat | nigdy / 50-100 lat | nizej |

- **Langwedocja 1355:** 5-6 tys. ludzi ([KRYT] u zrodla 4-6 tys.), 58 dni, 1 100 km (19 km na dobe), trzy rownolegle kolumny, 500 wsi zniszczonych, spalone m.in. Carcassonne, Narbonne i Limoux. Armia francuska unikala bitwy. Avignonet zwolniono z podatkow wojennych na 7 lat.
- **Rachunek gory (SZACUNEK):** 500 wsi x 50-104 dymy x 4.5 = 112-234 tys. ludzi na 319 tys. osobodni = 0.35-0.73; z miastami (+50 tys., zalozenie) 0.51-0.89. Wielkosc wsi to zalozenie (104 dymy na parafie: Francja 1328, DOK H3).
- **1356:** ok. 6 tys. walczacych w trzech dywizjach, 64 km miedzy skrzydlami, pas ponad 80 km.
- **Yorkshire 1319:** 106 wsi zwolnionych z podatku po spaleniu (49 North Riding, 57 West Riding) w ok. 3 tygodnie. Sily Szkotow zrodlo nie podaje; przy zalozeniu 5-10 tys. i 190 ludzi na wies wychodzi 0.10-0.19 (SZACUNEK).
- **Udzial spalonych w pasie przemarszu:** 10-25% (SZACUNEK: pas 30-50 km x 1 100 km x 30-40 ludzi na km2 = 1.0-2.2 mln; dotknietych 160-285 tys.). Reszta uciekala i wracala.
- **Spadek wartosci:** 1318 kazda parafia diecezji Carlisle ponad -50%; 1317 wszystkie poza jedna w dekanacie Copeland; Mortham -98%. Knaresborough: 140 domow zniszczonych, 20 zostalo. Czynsze Alnwick -4.1 / -16.5 / -28.6% rok po roku.
- **Losy ludzi (Easingwold 1326, 84 dzierzawcow niezdolnych placic):**
  - 22 polegli w bitwach (Myton 13, Byland 9);
  - 9 zabitych przez Szkotow (11%);
  - 10 zmarlo w nedzy;
  - 7 wygnanych bieda;
  - 17 zebrakow;
  - 17 zubozalych i spalonych.
- **Ucieczka i powrot:** Cumberland 1313 "a great part of the men" ucieklo z dobytkiem; w Acomb i Wall zostalo po 2 ludzi. Trwale opuszczonych osad "very few" (Mortham; Stenton pusta 4 lata).
- **Tempo odbudowy:**
  - welna Ponteland ponad poziom z 1312 w 1329/30;
  - welna Norhamshire z powrotem na 6 workow w 1330/31;
  - mlyny bez dochodu do 1329;
  - dziesieciny zbozowe Holy Island 112 -> 47 -> 21 -> 15 L, "never fully recovered".
- **Normandia i Ile-de-France (tylko streszczenia wyszukiwarki, tresci nie otwarto):** Normandia wschodnia indeks 100 (1314) -> 65 (ok. 1400) -> ponizej 30 (ok. 1450); odbudowa okolic Paryza 1450-1550. To wojna ciagla plus zaraza, nie pojedynczy najazd.
- **Od czego zalezala odbudowa:**
  - powtarzalnosc najazdow (Polnoc 1311-1322 prawie co rok);
  - pokoj (poprawa dopiero po rozejmie 1323);
  - rodzaj gospodarki (stada ewakuowano, wiec welna wracala szybko; zboze, woly, mlyny i stodoly wolno);
  - zwolnienia podatkowe;
  - okup zamiast palenia (Durham 800 i 1 600 marek, Ripon 1 000 marek, Beverley 400 L). Durham utrzymal czynsze.
- **Nie potwierdzono:** liczby parafii z misji papieskiej 1340 po najezdzie 1339 (pewne jest tylko 6 000 florenow = 8 900 liwrow i rejestr 228 kart) oraz sil szkockich.
- **[KRYT] Potwierdzone u zrodla:** Wikipedia 1355 (58 dni, 1 100 km, 3 kolumny, 500 wsi, Avignonet 7 lat); McNamee (Easingwold 84 dzierzawcow z rozbiciem jak wyzej, 49 + 57 wsi, Knaresborough 140 z 160 domow, Holy Island 112 / 47 / 21 / 15, Mortham 98%).
- **[KRYT] Zalozenie do zapisania:** R pochodzi z dni calej wyprawy (z marszem), a w grze mnozy tylko czas lupienia; marsz pokrywa R_F.

### 1.3 Stan w kodzie i logach

Pliki cs, bk, bkrot, be leza w SCR\ore-supply.

| Co | Dowod | Wartosc |
|---|---|---|
| Tempo rabunku | cs DefaultRaidModel.cs:46-49 | pelny rabunek = 900 / (sqrt(N) + 5) godzin: 40 ludzi 3.3 doby, 300 - 1.7, 2000 - 0.75 |
| Strata hearth | cs RaidEventComponent.cs:172-175 | hearth -= krok x 0.5 x hearth; razem -39.7%, bez zwiazku z N |
| Zloto lupiezcy | RaidEventComponent.cs:179-188, DefaultRaidModel.cs:44 | 4 zl za hearth, z niczego |
| Stany wsi | RaidEventComponent.cs:117, :130-138 | BeingRaided na starcie; Looted gdy punkty wsi = 0, inaczej Normal; RaidCompleted pada zawsze, takze po przerwanym rabunku |
| Looted: produkcja | cs DefaultVillageProductionCalculatorModel.cs:20, :78; BKVillageProductionModel.cs:59 | 0 dla calej wsi (towary i zywnosc) |
| Looted: hearth | cs DefaultSettlementProsperityModel.cs:43-50 | -1 dziennie, brak przyrostu |
| Looted: czas | cs VillageHealCampaignBehavior.cs:18-21, IncreaseSettlementHealthAction.cs:9-15 | +0.06..0.12 dziennie, czyli 8.3-16.7 doby |
| Blizna | SRC\ScorchedEarth.cs:98-107, Settings.cs:329-331 | ponizej 150 hearth odrost x0.25; ponizej 40 +0.5 dziennie |
| Zerowanie armii | SRC\ScorchedEarth.cs:57-84, Settings.cs:325-328 | partia lorda od 100 ludzi w promieniu 3: -0.8 hearth x N/500 dziennie, podloga 25 |
| Wyrzutki z rabunku | SRC\OutlawLaw.cs:298-313, Settings.cs:506-507 | 3% hearth w ludziach, hearth -= ludzie x 0.5 |
| Renta i nedza | SRC\PopulationLaw.cs:294, KingdomTreasury.cs:190, OutlawLaw.cs:386, :420 | Looted nie placi; nedza od udzialu wsi Looted |
| BK: jency | bk BKRaidCaptureModel.cs:16-20, :48-51 | min(10% chlopow, 0.5 x (N - 5)), sufit 150 na rabunek |
| BetterEconomy | Modules\BetterEconomy\ModuleData\better_economy_settings.xml:75-81 | -8% chlopow BK, dowoz x0.05-0.22 przez 10-28 dni (czy czynne przy BK - nie sprawdzano) |
| [KRYT] AI rabuje tylko wies Normal | twall AiMilitaryBehavior.cs:431 | stan Looted jest blokada ponownego rabunku |
| [KRYT] Powrot do Normal | cs IncreaseSettlementHealthAction.cs:14 | +20 milicji z niczego przy kazdym powrocie |
| [KRYT] Licznik pelnych spalen juz jest w grze | twall CampaignWarManagerBehavior.cs:28-37 | StanceLink.SuccessfulRaids1/2 |

- **Zerowanie w ludziach:** dzisiejsze 0.8 hearth na 500 ludzi to 0.31 czlowieka na zbrojnego na dobe przy sredniej swiata (191 ludzi na hearth). Reach 0.71, Zelazne Wyspy 0.08, Nocna Straz 0.006. To ten sam rzad co historyczne R, ale rozjechany przez skale hearth.
- **BK juz skaluje z liczba napastnikow** (0.5 jenca na zbrojnego) - to precedens w zestawie modow.
- **Wies-mediana dzis (350 hearth, 60.7 tys. ludzi):** 350 -> 211 po rabunku -> ok. 191 po Looted; powrot do 350 po ok. 88 dobach (SZACUNEK). W skali PopulationLaw 26 tys. ludzi znika i wraca w kwartal.

**Rabunki w logach.** Autor czytal tylko licznik "rabunki" w liniach "Wyrzutki:" i napisal "wczesniejsze 22 doby: 0 rabunkow" oraz "w logach nie ma liczebnosci napastnikow". [KRYT] Oba zdania sa bledne. Stan po poprawce:

| Miara | Autor | [KRYT] |
|---|---|---|
| Pelne spalenia (zdarzenie VillageLooted) | 3-4 wsie: LOG l.1509 (dzien 108859) "rabunki 10", l.1625 (108861) "rabunki 6", l.1688 (108862) "rabunki 6"; 22 ludzi, 6 ludzi to dokladnie wies-mediana (350 x 0.605 x 3% = 6.35) | 3-5 w calej sesji, tylko w dobach 108859, 108861, 108862 |
| Starcia "Raid" w BIT | nie liczone | 53 w dobach 108839-108864 na 15 wsiach; 29 przed wojna powszechna (108859), 24 po niej |
| Koncentracja | - | Wrath Village 19 starc, Skirling 6, Mistedge 6: trzy wsie biora 58% starc |
| Napastnicy | "brak w logach" | 62-339, mediana 112, srednia 127 |
| Zabici obroncy w starciach "Raid" | - | 1 265, czyli 47 dziennie i ok. 17 tys. rocznie (SZACUNEK x13) |
| Inne sesje | w 12 logach z 4-5.10 (ok. 124 doby) rabunki tylko w LOG | 5-7 starc "Raid" na ok. 12 dob kazda, 0 pelnych spalen |
| Czestosc pelnych spalen | 0.5-0.67 dziennie = 182-243 rocznie = 0.32-0.43 na wies-rok (SZACUNEK z 6 dob) | srednia sesji ok. 0.09 na wies-rok; szczyt jak u autora |
| Wsi spalonych naraz | zaden log nie podaje; SZACUNEK 4-11 z 571 | nie przeliczane |
| Wsi z blizna naraz w wersji "-39% na 3.4-7 lat" | 66-95% (rozklad rownomierny) | log temu przeczy: wszystkie starcia na 2.6% wsi; liczby nie da sie podac z 27 dob |

- **Raport o widocznosci** podaje to samo co krytyk: 53 zdarzenia "Raid" w BIT w 27 dobach (DOK podawal 0 w 12 dobach).
- **[KRYT] Starcie to nie spalenie.** Rabunek z obroncami konczy sie po walce (twall MapEvent.cs:1022-1025), a napastnik dostaje rozkaz ponownego rabunku (RaidEventComponent.cs:146-156). Faza lupienia to osobne zdarzenie bez strat, ktorego SRC\BattleChronicle.cs:59 nie zapisuje. Odsetek lupien przerwanych jest nieznany.
- **Kontekst wojenny (autor):** krolestw z "(WOJNA)" bylo 2-6 do dnia 108858 i 25 z 29 od 108859.
- **Nadal brak w logach:** liczby rabunkow przerwanych (autor i [KRYT]), liczby wsi Looted (autor), czasu lupienia (RaidDamage) i osobodni zerowania [KRYT].

### 1.4 Wzor dla gry

Wies gry 75 tys. ludzi = ok. 160 prawdziwych wsi po 470 ludzi.

**Stan na wies:** `Start_v` (ludzie w dniu siewu, z PopulationLaw), `U_v` (uchodzcy z okregu), `S_v = U_v / Start_v`.

**Rabunek:**
- `x = R x N x t / Start_v`, gdzie N to napastnicy, t to doby rabunku.
- `d = min(D_MAX, (S_MAX - S_v) x (1 - e^-x))`. Kazdy rabunek pali czesc jeszcze niespalona, suma nie przekroczy S_MAX.
- `A = d x Start_v` ludzi dotknietych:
  - zabici `min(N, 5% x A)` -> Polegli;
  - wyrzutki `5% x A` -> pula OutlawLaw regionu, 1:1;
  - reszta (90%) -> uchodzcy regionu.
- **[KRYT] Niezgodnosc wzoru z rachunkiem.** Zapisany wzor ma `(S_MAX - S_v)`, a calc.py:12-14 liczy `(1 - S0)`. Do wyboru: zostawic zapisany wzor (liczby przykladow nizsze, patrz 1.5) albo zmienic wzor na `(1 - S_v/S_MAX) x (1 - e^-x)` - wtedy liczby autora zostaja.
- **[KRYT] Osobodni pelnego rabunku** to `37.5 x N / (sqrt(N) + 5)`, czyli skutek rosnie mniej wiecej z pierwiastkiem N. Armia 2000 robi 3x tyle co poczet 300, nie 6.7x.

**Plon:** `M_v = (1 - S_v)^eS x ((Dom_v + U_v) / Start_v)^0.5`. Czesc spalona traci proporcjonalnie (eS = 1), bo ginie tez kapital: woly, ziarno, mlyny. To odstepstwo od reguly 4.6 DOK.

**Powrot:** `U_v` maleje o 0.08% dziennie (regula 4.3 DOK); w wojnie x0.5; zero, gdy wies nie jest Normal albo region gloduje. Polowa wraca po 2.4 roku, 63% po 3.4, 87% po 7 lat; w wojnie dwa razy wolniej.

**Zerowanie:** ten sam wzor co doba, z `R_F`, bez zmiany stanu wsi.

**Panika (stan Looted):** `1 + 100 x d` dob, w granicach 1-17. [KRYT] Leczenie dziala tylko w ticku dobowym (cs VillageHealCampaignBehavior.cs:18-30), wiec "1.1 / 1.3 doby" to w praktyce 2 doby.

| Pokretlo | Domyslnie | Widelki | Skad |
|---|---|---|---|
| R (rabunek) | 0.5 | 0.1-1.0 | 1.2 |
| R_F (zerowanie) | 0.2 | 0.1-0.5 | SZACUNEK; dzis odpowiednik 0.31 |
| D_MAX (sufit jednego rabunku) | 15% | 10-25% | pokretlo bez zrodla; rzad udzialu spalonych w pasie |
| S_MAX | 90% | - | trwale opuszczenie "very few" |
| Zabici | 5% z A, nie wiecej niz N | 2-15% | Easingwold; poprawka krytyka 8 z DOK |
| Wyrzutki | 5% z A | 3-20% | dzisiejsze 3%; Easingwold 17 z 84 zebrakow |
| eS | 1.0 | 0.5-1.0 | wyceny -50% i wiecej |

### 1.5 Przyklady (okreg 75 tys., R = 0.5)

[KRYT] Pewne sa: czas rabunku 3.31 / 1.68 / 0.75 doby, strata vanilli -39.7% bez zwiazku z N, osobodni 132 / 504 / 14 000.

| Przypadek | Osobodni | Dotknietych wg autora (liczone `1 - S`) | [KRYT] wg zapisanego wzoru (`S_MAX - S`) | Zabici / wyrzutki / uchodzcy (do liczb autora) | Plon | Looted | Dzis |
|---|---|---|---|---|---|---|---|
| Banda 40, 3.3 doby | 132 | 66 (0.09%) | 59 | 3 / 3 / 60 | -0.09% | 1.1 doby; [KRYT] w praktyce 2 | -29.6 tys. ludzi, 17 dob |
| Poczet 300, 1.7 doby | 504 | 252 (0.34%) | 227 | 13 / 13 / 226 | -0.34% | 1.3 doby; [KRYT] w praktyce 2 | to samo |
| Armia 2000, 7 dob | 14 000 | 6 683 (8.9%) | 6 015 | 334 / 334 / 6 015 | -8.9%; [KRYT] -8.4% | 9.9 doby | to samo |
| [KRYT] Napastnik-mediana z logu, 112 ludzi | 270 | 135 (0.18%) - liczba krytyka | - | - | - | - | - |

- **[KRYT] "Armia 2000, 7 dob" nie moze byc liczona samym R = 0.5.** Pelny rabunek 2000 ludzi trwa 18 godzin (754 ludzi), reszta tygodnia to zerowanie z R_F = 0.2. Razem 3 254 ludzi (4.3%), nie 6 683 (8.9%).
- **[KRYT] Plon armii:** wg wlasnego wzoru autora (S_v = 0.9 x d, czlon rak ^0.5) -8.4%, nie -8.9%.
- **Widelki R 0.1-1.0 (autor):** poczet 50-502 ludzi (0.07-0.67%); armia przez tydzien 1.8-15% (sufit).
- **Armia 2000 w jednym rabunku vanilli (18 godzin):** 750 ludzi (1.0%) wg autora, 754 wg krytyka. Tydzien wymaga doliczania obecnosci co dobe.
- **Powrot w przykladzie z armia (autor, od 8.9%):** w pokoju po 3.4 roku zostaje 3.3% okregu spustoszone, po 7 latach 1.2%. Okreg pracuje caly czas na co najmniej 91%.
- **Po 571 wsiach (poczet 300, autor):** mediana 0.42%; ponad 1% w 93 wsiach; ponad 5% w 11; sufit w 5 (Nocna Straz).

### 1.6 Rok wojny i stan ustalony

Tabela autora: poczty po 300, R = 0.5, czestosc szczytu (0.32-0.43 na wies-rok). [KRYT] poprawil czestosc i wielkosc pocztu tylko w sumie swiata (druga tabela); wierszy krain nie przeliczal, wiec sa liczone przy czestosci szczytu i poczcie 300.

| Kraina | Rabunkow rocznie | Dotknietych | % ludzi wsi | Wersja -39% na rabunek |
|---|---|---|---|---|
| Reach | 12.7-17 | 3.2-4.3 tys. | 0.045-0.06% | 0.9-1.2 mln (12.6-16.8%) |
| Polnoc | 18-24 | 4.6-6.1 tys. | 0.17-0.22% | 12.6-16.8% |
| Krainy Burzy | 9.9-13.2 | 2.5-3.3 tys. | 0.10-0.14% | jw. |
| Zelazne Wyspy | 8.9-11.9 | 2.2-3.0 tys. | 0.47-0.63% | jw. |
| Wolni Ludzie | 5.7-7.6 | 1.4-1.9 tys. | 0.95-1.27% | jw. |
| Sarnor | 4.1-5.5 | 1.0-1.4 tys. | 1.3-1.7% | jw. |
| Nocna Straz | 3.5-4.7 | 0.76-1.0 tys. | 3.8-5.1% | jw. |

| Miara swiata | Autor | [KRYT] |
|---|---|---|
| Dotknieci rocznie | 46-61 tys. (0.11-0.15% ludzi wsi) | 25-33 tys. przy N = 112; gorna granica 103 tys. (0.24% ludzi wsi), gdyby kazde z 53 starc konczylo sie pelnym rabunkiem (SZACUNEK) |
| Zabici rocznie ze wzoru | 2.3-3.1 tys. | prawdziwy rachunek zabitych rabunku to milicja i partie w obronie: ok. 17 tys. rocznie (SZACUNEK); wzor nie moze ich liczyc drugi raz |
| Stan ustalony, okreg-mediana | 0.7-1.0% (0.73%) | 0.66% |
| Stan ustalony, okreg graniczny (5 rabunkow rocznie) | 10% (10.3%) | 9.3% |
| Stan ustalony, dwa tygodniowe pobyty armii 2000 rocznie | 55% | 49.5% z S_MAX; 37%, gdy pobyt liczyc jako rabunek 18 h + zerowanie |

- Stan ustalony w wojnie: `S* = L x d / (L x d + r)`. Dla porownania bitwy wg DOK: 84-129 tys. zabitych rocznie.
- **Najgoretsze wsie [KRYT] (gorna granica, SZACUNEK):** Wrath Village 2 267 ludzi = 4.25% w 20 dob; Skirling 801 = 9.5% w 14 dob. Po roku ciaglego najezdzania ok. 50%.
- **Zgodnosc z historia:** to rzad Carlisle (najazdy co rok, wyceny -50% i wiecej), wiec tempo nie jest nierealne [KRYT].

### 1.7 Wpiecie w kod

1. **Pomiar rabunku.**
   - Nasluch `VillageBeingRaided` zapamietuje hearth i czas; nasluch `RaidCompletedEvent` liczy N, t, d i A. Rejestracja obok SRC\ArmouryBehavior.cs:504. [KRYT] Zdarzenia istnieja: twall CampaignEvents.cs:763, 765, 1001.
   - N liczyc jak BK: suma partii strony atakujacej.
   - Autor: na koniec hearth = hearth sprzed rabunku minus A / k; vanillowe -39% znika.
   - [KRYT] Tak zapisane skasuje inne zmiany z czasu rabunku: biede (SRC\OutlawLaw.cs:166, bierze z najwiekszej wsi), powroty (:185, daje najslabszej) i zerowanie innych partii. Poprawka: oddawac tylko strate vanilli, odtworzona z `RaidDamage`: hearth sprzed = hearth po x e^(0.5 x RaidDamage) w przyblizeniu.
   - [KRYT] `RaidDamage` jest publiczne i zapisywane w save (RaidEventComponent.cs:29-30). Czas lupienia tez z niego wynika: t = RaidDamage x 900 / (sqrt(N) + 5) / 24. Zdarzenia z RaidDamage = 0 (sama walka z milicja) pomijac.
   - Wariant dokladniejszy (autor): prefiks i postfiks na `RaidEventComponent.Update` (naliczanie co tick).
2. **`OutlawLaw.OnVillageLooted` (:298-313):** wylaczyc regule 3% hearth, inaczej liczy podwojnie.
3. **`ScorchedEarth.OnDaily` (:73-74):** zamiast `Hearth -= drain` ta sama funkcja z R_F.
4. **`ScorchedEarth.HearthScarPostfix` (:91-110):** usunac, zastepuje go powrot uchodzcow.
5. **Powrot:** w dobowym kroku OutlawLaw obok ReturnHome (:436-450).
6. **Plon:**
   - czynnik `(1 - S_v)` w `MaterialLaw.ProdPostfix` (:158-182); prog 0.001 z :171 polknalby bande 40 (0.09%), wiec dla tego czynnika go zdjac;
   - zywnosc w `WinterBite.VillageFoodPostfix` (:121).
7. **Nedza:** SRC\OutlawLaw.cs:386, :420 - udzial ludzi spustoszonych regionu zamiast udzialu wsi w stanie Looted; to samo w czlonie U reguly 4.1 DOK.

**Pogodzenie ze stanem "wies spalona" (autor):**
- Stan zostaje jako symbol na mapie; AI, zadania i ikona go potrzebuja.
- Znaczy teraz "ludzie okregu w lasach i w zamku": produkcja i renta stoja krotko.
- Skrocenie: co dobe `IncreaseSettlementHealthAction.Apply(osada, 1/Dp - 0.06)` dla wsi Looted bez zdarzenia. To publiczna akcja vanilli, sama przywraca Normal.
- Koszt rabunku pocztu: ok. 3 doby postoju okregu (0.8% roku) zamiast 18 dob (5%).
- Produkcji w stanie Looted nie da sie wlaczyc bez obejscia warunkow w trzech modelach - autor nie poleca.

**Cztery rzeczy do naprawy [KRYT]:**
1. **Stan Looted jest blokada ponownego rabunku.** Skrocenie paniki do 1-2 dob skraca cykl z 10-19 dob do ok. 4. To daje do 80-100 pelnych rabunkow jednej wsi rocznie zamiast 20-35 (SZACUNEK).
   - Kazdy rabunek dalej daje lup w skali symbolu: 4 zl x 139 hearth = 556 zl dla wsi 350 hearth (RaidEventComponent.cs:174-188), towary z niczego (:272-301), jency BK do 150.
   - Poprawka: wlasna blokada ponownego rabunku na czas vanilli (8.3-16.7 doby) albo lup liczony od A.
2. **Panika "1.1 / 1.3 doby" to w praktyce 2 doby** (tick dobowy).
3. **Kazdy powrot do Normal dodaje +20 milicji z niczego.** Przy czestszych powrotach to realny przeciek ksiegi ludzi.
4. **Odtwarzanie hearth** - tylko strata vanilli z `RaidDamage` (punkt 1 wyzej). Znika przy tym ryzyko zapisu gry w trakcie rabunku.

### 1.8 Ryzyka, zaleznosci i decyzje

1. **Sila rabunku.** R = 0.5 czyni rabunek pocztu prawie niewidocznym (-0.3%; [KRYT] okreg-mediana -0.2%). Gorna granica historyczna to 1.0; wyzej to juz nie wzorzec. Do wyboru Jeffa.
2. **Czestosc rabunkow** autor zmierzyl na 6 dobach i 3-4 zdarzeniach. Przed wlaczeniem dodac linie logu "Spustoszenie:" z liczba rabunkow pelnych i przerwanych, napastnikami, osobodniami, dotknietymi, liczba wsi Looted i S srednim oraz najwyzszym. [KRYT] Dopisac osobodni zerowania i liczbe wsi; bez czasu lupienia R i R_F sa strojone na slepo.
3. **Male krainy.** Poczet 300 to 13.6% okregu Nocnej Strazy (1.7 tys. ludzi); przy czterokrotnie czestszych rabunkach 20% ludzi rocznie. Wymaga decyzji razem z rozdz. 2.
4. **Lup lupiezcy** (4 zl za hearth i towary) dalej liczy sie od vanillowych -39% i z niczego - sprawa ksiegi pieniadza.
5. **BK i BetterEconomy** maja wlasne ksiegi (-8% chlopow, jency do 150) w innej skali - nie ruszane, niespojnosc zostaje.
6. **Nie sprawdzono:** czy bandy w tym zestawie modow w ogole rabuja wsie; przyklad "banda 40" liczony jak maly poczet.
7. **Zapis w trakcie rabunku** gubi zapamietany hearth, chyba ze trafi do zapisu gry. [KRYT] Znika, gdy strate liczyc z `RaidDamage`.
8. **[KRYT] Zaleznosc od krokow 2 i 3 DOK** (autor jej nie wypisal):
   - bez kroku 3 vanilla odrabia A/k w godziny: 252 ludzi w Reach to 0.57 hearth przy +1.2 dziennie;
   - bez kroku 3 stan Looted zdejmuje -1 hearth dziennie w nicosc, czyli 51-643 ludzi na dobe zaleznie od krainy - wiecej niz samo A;
   - bez kroku 2 kazdy wracajacy wyrzutek dodaje 0.5 hearth: 13 wyrzutkow to +1 240 ludzi przy srednim k = 191.
9. **[KRYT] Milicja** nie ma konta w ksiedze, a to ona ginie przy rabunkach (1 265 zabitych obroncow w 27 dob) i rodzi sie z niczego (+20 przy kazdym powrocie wsi do Normal).

## 2. Male krainy: pelna tabela krain i warianty

Stan: doba 108864, po 28 dobach sesji. Skrypty i wyniki autora: SCR\aneks-male-krainy (m1_tabela.py, m2_krainy.py, m3_szczegoly.py, m4_warianty.py + *_out.txt); krytyka: SCR\aneks-krytyk\k2.py, k3.py, k4.py, k8.py.

### 2.0 Skrot

1. **Zadna z trzech krain nie przezyje 3-5 lat przy dzisiejszym tempie strat, w zadnym wariancie opartym tylko o wlasna ludnosc.** Nocna Straz traci 269-477% stanu wojska rocznie, Wolni Ludzie 303-457%, Sarnor 38-152%. Tabela i prog to pokretla drugorzedne; decyduje tempo strat.
   - [KRYT] Zaden wariant tempa strat nie zmienia. Mozliwe dzwignie: przerwy w stalej wojnie Straz - Wolni Ludzie; smiertelnosc bitew (przegrani 50.7% zabitych wobec historycznych 15-40%, LOG linia "Bitwy: dzien 108864"); Inni (385 zabitych Wolnych Ludzi).
2. **Nocna Straz:** dziala tylko wyjatek zrodla (rekrut z calego Westeros). Koszt dla Westeros: 9-16 tys. ludzi rocznie = 0.12-0.22% jego doroslych mezczyzn.
   - Autor: to 7-12% przyrostu naturalnego Westeros. [KRYT] To mezczyzni dzieleni przez przyrost wszystkich ludzi; wobec przyrostu doroslych mezczyzn (137 tys. x 0.27 = 37 tys. rocznie) to 25-43% (SZACUNEK).
3. **20 tys. dla Nocnej Strazy:** kanon nie podaje ludnosci Daru, ale 20% mezczyzn z 20 tys. to 1 080 - prawie dokladnie kanoniczna Straz (ponizej 1 000). Gra trzyma tam 3 376 ludzi (3.4x kanon).
4. **150 tys. dla Wolnych Ludzi ma oparcie w kanonie** (zastep Mance'a 30-40 tys. wg Jona, 100 tys. wg Satina, z kobietami i dziecmi). Wojsko w grze (3 115) to ok. 19% kanonicznych ok. 16 tys. wojownikow.
5. **Sarnor 100 tys. to 5x kanon** (ponizej 20 tys. ludzi w Saath). Wojsko 3 259 = 12.1% mezczyzn, czyli pod progiem 20%. Przy tabeli kanonicznej kraina znika w 1.3-1.8 roku.
6. **Nowe wobec DOK:** w dobie 23 sesji (108859) ruszyly naraz wojny fabularne ROT - 25 z 29 krolestw w wojnie. Partie rodow urosly z 65.5 do 93.9 tys. w 7 dob; swiat ma dzis ok. 157.6 tys. pod bronia (DOK: 112.9 tys. z doby 12). [KRYT] Czytac jako 145-158 tys.
   - [KRYT] Przyczyna jest znana (autor: "nieustalona"): skalowanie dat x4.33 - start 5 staje sie doba 22, koniec 100 - doba 433.
   - [KRYT] Skok z doby 108858 na 108859: 66 412 -> 86 432, czyli +20 020 ludzi w jedna dobe z szablonow partii. To 103-171 dob przyrostu doroslych mezczyzn calego swiata (117-195 dziennie, SZACUNEK). Nie ustalono, kto za niego placi w ksiedze.
   - Ten sam skok w CSV (raport o widocznosci): doba 108859 ma 394 -> 683 partie i 67 819 -> 87 287 wojska rodow.
7. **Garnizony startowe da sie policzyc dokladnie:** 70 x (1 + dobrobyt/1300) na warownie = 48 209 ludzi w 227 warowniach. Wojsko idzie za liczba rodow i warowni, nie za ludnoscia.

### 2.1 Tabela wszystkich krolestw (doba 108864, od najbardziej obciazonych)

Jak liczone:
- **Ludzie:** SRC\PopulationLaw.cs:37-69 rozlozone na osady wg wlasnosci z settlements.xml i spclans.xml (liczba lenn zgodna z CSV w 28 z 29 krolestw; Ibben 9 wsi w CSV wobec 11 w XML).
- **Partie:** linie "Skarbce: dzien 108864 ... wojsko rodow" (LOG l.1785-1813; suma 93 936; CSV 94 630 z rodami mniejszymi 693 i graczem).
- **Garnizony start:** wzor vanilli, cs GarrisonTroopsCampaignBehavior.cs:214-215 ([KRYT] :213-215), na dobrobycie z XML. Sprawdzony na zakupy.log co do sztuki: King's Landing 436, Qarth 490, Winterfell 404, Castle Black 366, Pyke 350, Frostfang's Camp 318; zamki o dobrobycie 1000 daja 124 wobec 125 w logu.
- **Garnizony dzis:** SZACUNEK = start x (zold garnizonow doby 108864 / doby 108836) z CSV; zawiera patrole. [KRYT] Zawyzone, patrz pod tabela.
- **Zabici:** BIT, 480 starc, 11 601 zabitych = 414 dziennie, wg flagi krolestwa. "Zolnierze" = partie lordow + patrole + garnizony; reszta to wiesniacy, karawany i obrona wsi.
- **Rocznie:** x 364/28 (SZACUNEK).
- **Lata do wyczerpania:** (wolne rece - wojsko dzis) / zabici rocznie; pierwsza liczba dla wszystkich zabitych, druga dla samych zolnierzy.
- [W] = w wojnie w dobie 108864.

| Kraina | Ludzie | Mezczyzni 27% | Wolne rece 20% | Partie | Garnizony start / dzis | Razem | % mezczyzn | % wolnych rak | Zabici 28 dob (zolnierze) | Rocznie (zolnierze) | Lata do wyczerpania | Dni wojny z 28 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Nocna Straz [W] | 20 000 | 5 400 | 1 080 | 2 471 | 877 / 905 | 3 376 | 62.52 | 312.6 | 702 (698) | 9 126 (9 074) | juz ponad pule | 28 |
| Sarnor [W] | 100 000 | 27 000 | 5 400 | 1 980 | 1 018 / 1 279 | 3 259 | 12.07 | 60.4 | 286 (95) | 3 718 (1 235) | 0.6 / 1.7 | 6 |
| Wolni Ludzie [W] | 150 000 | 40 500 | 8 100 | 1 833 | 1 438 / 1 282 | 3 115 | 7.69 | 38.5 | 1 173 (727) | 15 249 (9 451) | 0.3 / 0.5 | 28 |
| Zelazne Wyspy [W] | 500 000 | 135 000 | 27 000 | 4 328 | 2 267 / 3 750 | 8 078 | 5.98 | 29.9 | 75 (31) | 975 (403) | 19.4 / 47.0 | 6 |
| Ibben [W] | 350 000 | 94 500 | 18 900 | 2 265 | 905 / 925 | 3 190 | 3.38 | 16.9 | 107 (39) | 1 391 (507) | 11.3 / 31.0; [KRYT] 2.4 | 6 |
| Skagos | 50 000 | 13 500 | 2 700 | 224 | 237 / 206 | 430 | 3.19 | 15.9 | 21 (0) | 273 (0) | 8.3 / brak strat | 0 |
| Dorne [W] | 1 500 000 | 405 000 | 81 000 | 6 341 | 3 046 / 3 929 | 10 270 | 2.54 | 12.7 | 315 (20) | 4 095 (260) | 17.3 / ponad 100 | 6 |
| Smocza Skala, Stannis [W] | 850 000 | 229 500 | 45 900 | 2 972 | 2 063 / 1 937 | 4 909 | 2.14 | 10.7 | 2 306 (1 229) | 29 978 (15 977) | 1.4 / 2.6 | 27 |
| Polnoc [W] | 3 000 000 | 810 000 | 162 000 | 9 596 | 4 522 / 5 909 | 15 505 | 1.91 | 9.6 | 499 (52) | 6 487 (676) | 22.6 / ponad 100 | 19 |
| Dothrakowie [W] | 750 000 | 202 500 | 40 500 | 2 022 | 1 309 / 1 811 | 3 833 | 1.89 | 9.5 | 178 (25) | 2 314 (325) | 15.8 / ponad 100 | 6 |
| Lorath [W] | 800 000 | 216 000 | 43 200 | 2 078 | 1 059 / 1 164 | 3 242 | 1.50 | 7.5 | 98 (15) | 1 274 (195) | 31.4 / ponad 100 | 6 |
| Krainy Burzy, Renly [W] | 2 500 000 | 675 000 | 135 000 | 6 190 | 2 503 / 3 119 | 9 309 | 1.38 | 6.9 | 1 347 (759) | 17 511 (9 867) | 7.2 / 12.7 | 27 |
| Lys [W] | 1 000 000 | 270 000 | 54 000 | 2 043 | 1 065 / 1 445 | 3 488 | 1.29 | 6.5 | 187 (132) | 2 431 (1 716) | 20.8 / 29.4 | 6 |
| Yi Ti, wygnancy | 100 000 | 27 000 | 5 400 | 194 | 97 / 154 | 348 | 1.29 | 6.4 | 13 (13) | 169 (169) | 29.9 / 29.9 | 0 |
| Dorzecze | 3 021 785 | 815 882 | 163 176 | 4 373 | 3 116 / 6 079 | 10 452 | 1.28 | 6.4 | 71 (8) | 923 (104) | ponad 100 | 0 |
| Dolina [W] | 3 000 000 | 810 000 | 162 000 | 5 848 | 2 672 / 4 264 | 10 112 | 1.25 | 6.2 | 102 (29) | 1 326 (377) | ponad 100 | 6 |
| Myr [W] | 1 000 000 | 270 000 | 54 000 | 2 033 | 1 055 / 1 304 | 3 337 | 1.24 | 6.2 | 11 (11) | 143 (143) | ponad 100 | 6 |
| Korona Joffreya [W] | 5 078 215 | 1 371 118 | 274 224 | 9 238 | 4 470 / 5 752 | 14 990 | 1.09 | 5.5 | 268 (49) | 3 484 (637) | 74.4 / ponad 100 | 19 |
| Daenerys [W] | 2 008 907 | 542 405 | 108 481 | 3 101 | 1 632 / 2 542 | 5 643 | 1.04 | 5.2 | 95 (57) | 1 235 (741) | 83.3 / ponad 100 | 6 |
| Tyrosh [W] | 1 000 000 | 270 000 | 54 000 | 1 346 | 1 061 / 1 161 | 2 507 | 0.93 | 4.6 | 399 (180) | 5 187 (2 340) | 9.9 / 22.0; [KRYT] ok. 2.6 | 6 |
| Norvos [W] | 1 500 000 | 405 000 | 81 000 | 2 221 | 1 037 / 1 438 | 3 659 | 0.90 | 4.5 | 7 (7) | 91 (91) | ponad 100 | 6 |
| Pentos [W] | 1 500 000 | 405 000 | 81 000 | 2 213 | 1 120 / 1 335 | 3 548 | 0.88 | 4.4 | 0 (0) | 0 | brak strat | 6 |
| Aegon [W] | 1 426 952 | 385 277 | 77 055 | 1 812 | 1 222 / 1 402 | 3 214 | 0.83 | 4.2 | 53 (13) | 689 (169) | ponad 100 | 6 |
| Qohor [W] | 1 500 000 | 405 000 | 81 000 | 2 044 | 1 040 / 1 255 | 3 299 | 0.81 | 4.1 | 345 (29) | 4 485 (377) | 17.3 / ponad 100 | 6 |
| Braavos [W] | 2 500 000 | 675 000 | 135 000 | 2 461 | 1 098 / 1 370 | 3 831 | 0.57 | 2.8 | 179 (72) | 2 327 (936) | 56.4 / ponad 100 | 6 |
| Reach [W] | 8 000 000 | 2 160 000 | 432 000 | 7 150 | 3 292 / 4 761 | 11 911 | 0.55 | 2.8 | 101 (31) | 1 313 (403) | ponad 100 | 6 |
| Volantis [W] | 4 614 141 | 1 245 818 | 249 164 | 3 361 | 1 567 / 1 941 | 5 302 | 0.43 | 2.1 | 41 (0) | 533 (0) | ponad 100 | 6 |
| Qarth [W] | 4 000 000 | 1 080 000 | 216 000 | 1 963 | 1 324 / 1 130 | 3 093 | 0.29 | 1.4 | 44 (5) | 572 (65) | ponad 100 | 6 |
| Wyspy Letnie | 750 000 | 202 500 | 40 500 | 235 | 97 / 145 | 380 | 0.19 | 0.9 | 8 (0) | 104 (0) | ponad 100 | 0 |
| SWIAT | 52 570 000 | 14 193 900 | 2 838 780 | 93 936 | 48 209 / 63 697 | 157 633; [KRYT] 145-158 tys. | 1.11 | 5.6 | 9 031 (4 326) | 117 403 (56 238) | 22.8 / 47.7 | - |

**Poprawki krytyka do tabeli:**
- **Sprawdzenie niezalezne.** 5 wierszy (Nocna Straz, Sarnor, Wolni Ludzie, Zelazne Wyspy, Smocza Skala) zgadza sie co do sztuki: ludzie, osady, partie, zabici. Garnizon startowy Nocnej Strazy 878 (autor 877), Smoczej Skaly 2 062 (autor 2 063). Suma swiata 52.57 mln, 227 warowni.
- **Garnizony "dzis".** Iloraz zoldu daje x1.32 swiata, a bezposrednia obserwacja 20 garnizonow z co najmniej 2 odczytami w zakupy.log: mediana x1.02, srednia x1.06 (k4.py; proba to tylko warownie robiace zakupy). Reszta zoldu to patrole i wyzszy zold po awansach. Dla trzech malych krain roznica jest pomijalna.
- **"Lata do wyczerpania"** dla 20 krolestw w wojnie od doby 23 mieszaja 22 doby pokoju z 6 dobami wojny, a wojna jest wymuszona do doby 433. Ibben: 107 zabitych w 6 dob to ok. 6.5 tys. rocznie, czyli 2.4 roku, nie 11.3. Tyrosh: ok. 300 w 6 dob, czyli ok. 2.6 roku, nie 9.9 (SZACUNEK).
- **Tabela liczy ludzi ze startu.** W dobie 108864 ksiega ma 56.8 mln (LOG l.1776): Wyspy Letnie 0.75 -> 1.33 mln, Yi Ti 0.10 -> 0.26 mln, Sarnor 0.11 mln.

**Kultura -> krolestwo** (wg wlasnosci osad; [KRYT] przypisanie poprawne):
- battania = Polnoc; reach = Reach; aserai = Dorne; vale = Dolina; stormlands = Krainy Burzy; sturgia = Zelazne Wyspy; dragonstone = Stannis.
- river = Dorzecze 3.02 mln + 0.48 mln pod Joffreyem; vlandia 4.0 mln + crownlands 0.6 mln = Korona Joffreya.
- volantine = Volantis 4.61 mln + 0.39 mln Aegon; ghiscari = Daenerys 2.01 mln + Aegon 0.99 mln; valyrian = Aegon 0.05 mln.
- empire = Braavos; nord = Lorath; khuzait = Dothrakowie; pozostale 1:1.

**Rody bez krolestwa** (spclans.xml: 25 z 243):
- 12 rodow mniejszych i najemnych: 693 ludzi w partiach.
- 13 rodow bandyckich: 413 band, 5 692 ludzi, pula 568 (LOG l.1814).
- Inni (rod ROTclan_126) biora ludzi z niczego: +100 upiorow na nowa partie, +2 dziennie, po wygranej bitwie wskrzeszaja pokonanych (rotall ROTOthersCampaignBehavior.cs:1303, :1311, :1362-1369, :895).
- Zabici poza krolestwami w 28 dob: 2 570 (Broken Men 934, Inni 712, Sea Raiders 185, Wild Hares 160).

**Zastrzezenia autora:**
- Jedna sesja, 28 dob; tempo rosnie (linie "Bitwy:": 402 dziennie srednio, 686 dziennie w ostatnich 7 dobach = 250 tys. rocznie).
- Po wybuchu wojny partie krainy podwajaja sie w tydzien (doba 21 -> 28): Sarnor 1 047 -> 1 980, Ibben 1 057 -> 2 265, Zelazne Wyspy 2 173 -> 4 328, Reach 3 776 -> 7 150.
- Wg wyczerpania (ponizej 10 lat) zagrozone sa tez Smocza Skala 1.4, Krainy Burzy 7.2, Skagos 8.3 i Tyrosh 9.9 ([KRYT] ok. 2.6), mimo wojska 1-3% mezczyzn.

### 2.2 Skad biora sie ich ludzie

**Wspolne (zadne zrodlo nie zalezy od liczby ludzi krainy):**
- **Garnizon startowy:** 70 x (1 + dobrobyt/1300) z szablonu kultury.
- **Nowa partia lorda:** szablon kultury 53-56 ludzi (partyTemplatesROT.xml: nightswatch 54, freefolk 53, sarnor 53).
- **Ochotnicy:** 6 miejsc na notabla (cs Hero.cs:1502); wies 3 notabli, miasto 5 (cs DefaultNotableSpawnModel.cs:8-34; BK moze to zmieniac - SZACUNEK).
- Stad jedno miejsce ochotnika na 88 ludzi w Nocnej Strazy, 340 w Sarnorze, 362 u Wolnych Ludzi, 731 na Zelaznych Wyspach, 2 315 na Polnocy, 8 889 w Reach.

| Kraina | Osady (miasta / zamki / wsie) | Rody (bohaterowie) | Partie doby 28 | Wojny w logu |
|---|---|---|---|---|
| Nocna Straz | 1 / 4 / 11 (Castle Black) | 5 (22) | 16 partii po 134-171 ludzi; rod Pyke 0 (mial 406) | z Wolnymi Ludzmi 28/28 dob |
| Wolni Ludzie | 3 / 4 / 18 (Frostfang's Camp, Thenn, Hardhome) | 7 z ziemia (38) + 3 bez ziemi | 18 partii, szczyt 2 236 | z Nocna Straza 28/28 dob |
| Sarnor | 2 / 3 / 13 (Saath, Kyth; Rathylar, Mardosh, Sarys) | 5 (24) | 17 partii | z Ibbenem od doby 23 |
| Zelazne Wyspy | 6 / 4 / 28 | 10 z ziemia (60) + 19 bez ziemi | 33 partie | z Dolina od doby 23 |
| Ibben | 3 / 1 / 11 | 4 + najemnicy Sons of the Harpy (272) | 15 partii | z Sarnorem od doby 23 |
| Skagos | 1 / 0 / 4 (Driftwood Hall) | 1 (Stane, tier 2) | 1 partia, 224 | brak |
| Wyspy Letnie | 1 / 0 / 3 (Lotus Bay) | 1 | 1 partia, 235 | brak |
| Yi Ti | 1 / 0 / 3 (Ifequeveron) | 1 | 1 partia, 194 | brak |

**Nocna Straz:**
- Wsie: hearth 350-809, razem 5 424; 1 291-2 983 ludzi na wies (mediana 1 726). Castle Black ma 0 ludzi (udzial miast 0, SRC\PopulationLaw.cs:50, :119).
- Wojna stala i wymuszana co dobe: ROT_spkingdoms.xml:17-19; rotall ROTStorylineWars.cs:94 (start 0, koniec 0), :234-257.
- Straty wg tygodni: 9 / 70 / 318 / 305. Zabojcy: Wolni Ludzie 688, Inni 14.
- **Gra nie ma werbunku Strazy z Westeros.** "Zeslanie na Mur" ROT dotyczy tylko gracza-krola i tylko pojmanego lorda (rotall ROTSendToWallBehavior.cs:45-88, :104-113).
- Pieniadz: renty 698 zl dziennie z naleznych 2 315 (LOG l.1776) wobec zoldu ok. 14.4 tys. dziennie (partie 10 909 + garnizony 3 536, CSV). Kiesa wodza 157 127 przy bilansie -11 686 dziennie (l.1795) = 13 dni.

**Wolni Ludzie:**
- Wsie po 6 945-11 346 ludzi (mediana 7 300); miasta maja 0 ludzi.
- Zabici 1 173: partie 727, obrona wsi 186, wiesniacy 138, karawany 122. Zabojcy: Nocna Straz 777, Inni 385. Tygodnie: 93 / 196 / 400 / 484.
- Renty 1 620 z 16 978 zl dziennie wobec zoldu ok. 11.4 tys.

**Sarnor:**
- Wsie po 5 960-7 918 ludzi; miasta ok. 10 tys.
- Wojna z Ibbenem fabularna (ROTStorylineWars.cs:103, start 5, koniec 100). W logu ruszyla w dobie 23 sesji. [KRYT] Przez skalowanie x4.33 trwa do doby 433, nie do 100.
- Zabici 286: wiesniacy 163 (wszyscy od Sea Raiders), zolnierze 95 (wszyscy od Ibbenu, w ostatnich 6 dobach), karawany 28.
- Renty 4 398 z 11 735 zl dziennie wobec zoldu ok. 10.6 tys.

**Pozostale:**
- Zelazne Wyspy: wies mediana 17.7 tys. ludzi; wojna z Dolina fabularna (ROTStorylineWars.cs:87); zabici 75.
- Ibben: 107 zabitych w 6 dob wojny.
- Skagos: wsie po 12 500 ludzi; 21 wiesniakow zabitych przez Broken Men.
- Wyspy Letnie i Yi Ti: wsie po 50 hearth, czyli 225 000 i 26 667 ludzi na wies (k = 5 125 i 667 na hearth, LOG l.159). To klopot odwrotny: tabela za duza wobec hearth, nie wojsko wobec ludzi.

### 2.3 Kanon (A Wiki of Ice and Fire, czytane przegladarka 05.10.2026)

- **Nocna Straz:**
  - 10 000 przy Podboju; w 298 AC ponizej 1 000: 600 w Castle Black, 200 w Shadow Tower, mniej w Eastwatch (AGOT, Tyrion III).
  - Wielki zwiad 300 ludzi, wrocilo 14; przy wyborze dowodcy 588 zetonow, czyli ponizej 600 (ASOS, Samwell IV).
  - Bracia pochodza "z calych Siedmiu Krolestw, a nawet z Essos"; werbuja ich "wandering crows" z lochow, takze bekarty i zbedni synowie.
- **Dar:**
  - 50 lig w glab od Muru; Mur ma 100 lig.
  - Dar Brandona: ziemia cienka i kamienista, zdziczala. Nowy Dar: dobra ziemia, wsie placa Strazy towarem i praca, ale wiele opuszczono przez najazdy.
  - Kanon nie podaje liczby ludzi. SZACUNEK: 100 x 50 lig = 300 x 150 mil = ok. 116 tys. km2, wiec 20 tys. to 0.17 czlowieka na km2 (Anglia ok. 1300: ok. 36).
- **Wolni Ludzie:**
  - Zastep Mance'a: 30-40 tys. z kobietami, dziecmi i starcami (Jon, ASOS); 100 tys. (Satin z Muru, ASOS).
  - Wojownikow ok. 16 tys. - to wyliczenie wiki (Stannis: 20 na 1 przy 800 koniach); do tego setki olbrzymow i ponad 100 mamutow.
  - Po klesce: 3 119 przeszlo Mur z Tormundem, ponad 6 000 w Hardhome (ADWD).
- **Zelazne Wyspy:** ok. 20 tys. ludzi i 500 lodzi (pol-kanon, gra RPG 2005); Zelazna Flota 100 okretow; najmniejsza z 9 krain.
- **Sarnor:** jedyne miasto, ktore nie jest ruina, to Saath; "mniej niz dwadziescia tysiecy" Wysokich Ludzi, "gdy kiedys byly ich miliony" (TWOIAF, The Grasslands). Sarys, Mardosh i Kyth sa w kanonie zburzone.
- **Skagos:** brak liczb; bunt za Daerona II "kosztowal tysiace istnien".
- **Szacunki fanow** (Atlas of Ice and Fire, 1% ludnosci = armia): Zelazne Wyspy 15 tys. zbrojnych = 1.5 mln, Polnoc 4 mln, Reach 12 mln, razem ok. 40 mln. Dla Muru, Daru, Skagos i ziem za Murem brak liczb.

**Gra wobec kanonu:** Nocna Straz 3 376 wobec ponizej 1 000 (3.4x); Wolni Ludzie 3 115 wobec ok. 16 tys. (0.19x); Zelazne Wyspy 8 078 wobec 20 tys. (0.4x). Stosunek Wolni : Straz w kanonie ok. 16:1, w grze 0.9:1.

**[KRYT] Stan sprawdzenia kanonu.** AWOIAF zwraca 403 przy pobraniu wprost; krytyk sprawdzil liczby tylko w streszczeniach wyszukiwarki.
- Potwierdzone tam: Straz ponizej 1 000 (600 Castle Black, 200 Shadow Tower); zastep Mance'a 30-40 tys. (Jon) i 100 tys. (Satin); ok. 16 tys. wojownikow jako wyliczenie wiki; Saath ok. 20 tys. ostatnich Sarnorczykow, wsparcie Ib i Lorath.
- Niesprawdzone: numery rozdzialow, 588 zetonow, 14 ocalalych, 3 119 i 6 000, 20 tys. Zelaznych Wysp.

### 2.4 Warianty po 1, 3 i 5 latach

Wszystko w tej czesci to SZACUNEK z symulacji dobowej (m4_warianty.py). Zalozenia:
- Straty zolnierzy proporcjonalne do stanu wojska, straty cywilow do ludnosci; polegli to dorosli mezczyzni.
- Przyrost +0.5% rocznie; wojsko uzupelniane do dzisiejszego stanu.
- "Hamulec" = nabor tylko, gdy sluzacy sa ponizej progu x zyjacy mezczyzni.
- Widelki = tempo srednie z 28 dob .. tempo z tygodni 3-4. Dla Sarnoru gorne tempo zolnierzy to ostatnie 7 dob (95 zabitych).
- Symulacja nie widzi, ze mniejsza armia przegrywa szybciej.

**Tempa dzienne (zolnierze + cywile):** Nocna Straz 24.9 + 0.1 .. 44.2 + 0.3; Wolni Ludzie 26.0 + 15.9 .. 39.1 + 24.0; Sarnor 3.4 + 6.8 .. 13.6 + 6.8.

**Ile trzeba ludzi, zeby przyrost 0.5% pokryl straty:** Nocna Straz 1.8-3.2 mln, Wolni Ludzie 3.0-4.6 mln, Sarnor 0.74-1.5 mln.

#### Nocna Straz (5 400 mezczyzn, wojsko 3 376)

| Wariant | Po 1 roku | Po 3 latach | Po 5 latach |
|---|---|---|---|
| 0. Ksiega bez wyjatku i bez hamulca | mezczyzn 0 od dnia 122-217; dlug 7.1-14.1 tys. | dlug 25-46 tys. | dlug 43-78 tys. |
| 0h. Hamulec 20% (nabor staje w dniu 1) | wojsko 274-427, mezczyzni -60..-75% | wojsko 29-141 | wojsko 0-43 |
| a. Tabela 60 tys. + hamulec | wojsko 1 243-1 896, mezczyzni -41..-62% | wojsko 180-652, mezczyzni -80..-94% | wojsko 23-228 |
| b. Rekrut z calego Westeros | wojsko 3 376; z Westeros 9.1-16.1 tys. (0.12-0.22% jego mezczyzn) | 27-48 tys. (0.37-0.65%) | 45-81 tys. (0.61-1.09%) |
| c. Wojsko 1 080 z wlasnej krainy + hamulec | wojsko 406-627 | wojsko 49-209 | wojsko 0-66 |
| b + c. Wojsko 1 000 z Westeros | z Westeros 2.7-4.8 tys. | 8.1-14.3 tys. | 13.4-23.9 tys. |

- **(a)** 60 tys. to zalozenie autora do proby, bez liczby w kanonie. Zeby 3 376 zmiescilo sie w 20% mezczyzn, trzeba ok. 62.5 tys. ludzi; bez hamulca przy 60 tys. mezczyzni koncza sie w dniu 366-652.
- **(b)** Wlasna kraina Strazy prawie stoi (mezczyzni -2..-8% w 5 lat). Koszt wg autora to 7-12% przyrostu naturalnego Westeros (137 tys. rocznie przy 27.45 mln); [KRYT] 25-43% przyrostu doroslych mezczyzn (SZACUNEK). Rozklad wg ludnosci: Reach 29%, Westerlands 15%, Dorzecze 13%, Polnoc i Dolina po 11%.
- **(c)** Z wlasnej krainy przy tym tempie utrzyma sie 6-10 ludzi; nawet przy stratach 10% rocznie ok. 270 (27 dorastajacych mezczyzn rocznie / 0.10).
- Dzisiejsze 3 376 lezy miedzy kanonem "dzis" (ponizej 1 000) a kanonem szczytu (10 000).

#### Wolni Ludzie (40 500 mezczyzn, wojsko 3 115)

| Wariant | Nabor staje | Po 1 roku | Po 3 latach | Po 5 latach |
|---|---|---|---|---|
| 0. 150 tys., bez hamulca | nigdy | mezczyzni -36..-55% | mezczyzn 0 od dnia 682-1 031; dlug 5-26 tys. | dlug 31-62 tys. |
| 0h. 150 tys., prog 20% | dzien 411-622 | wojsko 3 115, mezczyzni -36..-55% | wojsko 7-630, mezczyzni -92..-100% | 0 |
| a. Tabela 100 tys. | dzien 186-282 | wojsko 1 420-2 502, mezczyzni -54..-74% | 0 | 0 |
| a2. Tabela 250 tys. (bez oparcia w kanonie) | dzien 868-1 316 | mezczyzni -22..-33% | wojsko 1 152-3 115, mezczyzni -64..-91% | mezczyzni -96..-100% |
| b. Prog 60% mezczyzn | dzien 590-893 | jak 0h | wojsko 6-603, mezczyzni -99..-100% | 0 |
| c. Wojsko 1 500 | dzien 831-1 258 | mezczyzni -25..-37% | mezczyzni -71..-99% | 0 |

- **(a)** 150 tys. miesci sie w kanonie; podnoszenie nie ma oparcia, a 100 tys. tylko przyspiesza koniec.
- **(b)** Prog 60% jest zgodny z kanonem (16 tys. wojownikow z najwyzej 100 tys. = 16% ludzi = 59% mezczyzn). Kupuje ok. pol roku naboru i szybciej wyludnia kraine.
- **(c)** Nie pomaga: 446 z 1 173 zabitych to cywile (obrona wsi, wiesniacy, karawany), a 385 zabili Inni.
- Straty 15-23 tys. rocznie wobec przyrostu 750 rocznie (20-30x).

#### Sarnor (27 000 mezczyzn, wojsko 3 259)

| Wariant | Po 1 roku | Po 3 latach | Po 5 latach |
|---|---|---|---|
| 0h. 100 tys., prog 20%, wojna stala | mezczyzni -13..-27% | wojsko 1 580-3 259, mezczyzni -39..-71% | wojsko 309-2 086, mezczyzni -61..-94% |
| 0w. Wojna do konca fabularnego, piraci bez zmian - [KRYT] koniec w dobie 433 | -13..-27% | -31..-48% | -48..-66% |
| 0w w wersji autora (koniec w dniu 100 - bledne) | -10..-14% | -27..-34% | -44..-53% |
| 0p. Sam pokoj (gina tylko wiesniacy i karawany) | -9% | -25% | -41% |
| a. Tabela 20 tys. (kanon) | wojsko 714-2 231, mezczyzni -61..-87% | mezczyzn 0 od dnia 461-655 | 0 |
| a2. Tabela 350 tys. (jak Ibben) | -3..-7% | -10..-22% | -17..-36% |
| c. Wojsko 1 080 przy 20 tys. | wojsko 412-739 | mezczyzn 0 | 0 |

- **(a)** Kanon popiera obnizenie, nie podniesienie; ROT daje jednak Sarnorowi 5 warowni i 13 wsi, wiec 20 tys. to 60% mezczyzn pod bronia od dnia 1.
- **(b)** Brak kanonicznego wyjatku zrodla. Kanon mowi tylko, ze Saath przetrwalo dzieki wsparciu Ibbenu i Lorath, a gra stawia Sarnor w wojnie z Ibbenem.
- **(c)** Przy 100 tys. wojsko jest pod progiem, zmniejszac nie trzeba.
- Staly ubytek robia piraci: 163 wiesniakow w 28 dob = 2.1 tys. rocznie wobec przyrostu 500. To 4-6 rozbitych taborow - mala proba.
- [KRYT] k8.py odtwarza liczby autora i liczy poprawiony wariant 0w.

### 2.5 Czego nie sprawdzono

- Stanu ustalonego: jedna sesja 28 dob, wojny fabularne od doby 23, ekstrapolacja x13.
- Garnizonow "dzis" wprost: log podaje stan tylko dla 27 warowni w zakupy.log; reszta ze zoldu.
- Liczby notabli po zmianach BK.
- Kanonicznego rocznego naplywu rekrutow Strazy: wiki nie podaje liczby.
- AWOIAF czytane przegladarka (pobranie bezposrednie zwraca 403); numery rozdzialow przy liczbach wg przypisow wiki, nie sprawdzane w ksiazkach.
- Ludnosci Daru kanon nie podaje; 60 tys. w wariancie (a) Nocnej Strazy to zalozenie autora.

## 3. Gdzie sprawdzic ksiege ludzi: log, CSV, widok w grze

Odpowiedz na pytanie Jeffa: w trzech miejscach - linia "Ludzie:" w glownym logu, plik ludzie.csv do Excela i okno w menu osady. Skrypty i dekompilacja dymka: SCR\aneks-ksiega-bandyci (b1-b4.py, c1.py, TooltipRefresherCollection.cs).

**Zastrzezenie do kroku 1 DOK.** "Polegli razem" i "plon wzgledem startu" wymagaja stanu w save (start i polegli na region, 2 x 227 liczb). Krok 1 zaklada "bez stanu w save", wiec bez nowego klucza obok `arm_population` / `arm_outlaws` (SRC\ArmouryBehavior.cs:417-423) te pola zeruja sie przy kazdym wczytaniu. [KRYT] Uwaga sluszna.

### 3.1 Log raz na dobe

- **Gdzie:** glowny plik Modules\Armoury\Armoury-<data_godz>.log (SRC\Log.cs:26). Prefiks "Ludzie:" nie jest na liscie tematow (SRC\Log.cs:75-90), wiec linia zostaje w glownym logu.
- **Wywolanie:** nowy `PeopleLedger.Daily()` po `OutlawLaw.Daily` (SRC\ArmouryBehavior.cs:1173).
- **Dostep:** `OutlawLaw._pool`, `Count`, `RegionOf`, `Nodes` sa dzis `private` (SRC\OutlawLaw.cs:53, 72, 90, 122) i musza byc `internal`.

Format (trzy linie; liczby band, puli i poleglych z LOG, reszta to przyklad autora):

```
Ludzie: dzien 108864 | swiat 52 601 000 (start 52 600 000 = 100.0%) | w domu 52 481 000 (wsie 41.9 mln, miasta 10.6 mln) | w sluzbie 113 200 | wyrzutki 6 260 (pula 568, bandy 5 692) | uchodzcy 0 | polegli dzis 421, razem 11 377 | przyrost naturalny dzis +577 (+0.40%/rok) | plon wzgledem startu 100.0% (najnizszy 97.8% Mistedge) | nieprzypisane: z niczego 0, w nicosc 0.
Ludzie: najwieksze straty - <region> 96.1% startu (polegli 212, uchodzcy 2 900, w sluzbie 340, plon 98.0%); <4 kolejne>.
Ludzie: najwiecej pod bronia - <region> 63.8% mezczyzn; <4 kolejne> | regionow ponad prog 20%: 5.
```

**[KRYT] Ten przyklad wprowadzi Jeffa w blad - w kroku 1 liczby beda inne:**
- swiat: 56.8 mln (108% startu) i ok. +155 tys. dziennie, nie "100.0%" i "+577". Tak bedzie az do kroku 3;
- "w sluzbie 113 200" to liczba z doby 12; w dobie 108864 same partie maja 93 936, a garnizony co najmniej 48 tys.

Od kroku 1 prawdziwe sa: w domu (`PopulationLaw.PeopleOf`, SRC\PopulationLaw.cs:112-121), pula wyrzutkow i bandy. "W sluzbie" i "polegli" na region sa do kroku 5 przyblizeniem wedlug osady macierzystej partii. Uchodzcy, przyrost i plon sa do krokow 3-6a "proba na sucho".

### 3.2 Plik poza gra (Excel)

- **Gdzie:** Modules\Armoury\Logs\<sesja>\ludzie.csv. Katalog sesji tworzy SRC\Log.cs:36-38 (pole `_topicDir` prywatne, :10).
- **Zmiana:** jedna metoda w Log.cs, np. `Log.Table(plik, naglowek, wiersze)`, pod istniejacym zamkiem `Gate` (:11). Gdy `_topicDir == null` (:44), pisac do katalogu modulu.
- **Wzorzec juz jest w repo:** CrashScribe\src\EconomyAudit.cs:61-62, :125 - jeden CSV na sesje, separator `;`, UTF-8.
- **Zawartosc:** 227 wierszy na dobe, same liczby calkowite (polski Excel nie pomyli przecinka):

```
dzien;region_id;region;krolestwo;kultura;typ;wsie;start;w_domu;w_tym_miasto;w_sluzbie;wyrzutki_pula;wyrzutki_bandy;uchodzcy;jency;polegli_dzis;polegli_razem;przyrost_dzis;przyrost_promile_rok;mezczyzni;pod_bronia_promile;plon_promile_startu;nieprzypisane
```

- **Rozmiar:** SZACUNEK 227 x ok. 120 B = ok. 27 KB na dobe, ok. 10 MB na rok gry.
- **Uwaga:** katalogi sesji sa kasowane powyzej 12 (SRC\Log.cs:12, :39-42), wiec historia dluzszej kampanii zniknie. Do decyzji: nie kasowac ludzie.csv albo trzymac drugi plik narastajacy na kampanie.

### 3.3 W grze - trzy mozliwosci, autor zaleca pierwsza

**(a) Wlasna opcja "People of the district" w menu `village`, `town`, `castle` - zalecane.**
- Rejestracja w `ArmouryBehavior.OnSessionLaunched` obok `IronBank.AddMenus` / `SmithMenu.Add` (SRC\ArmouryBehavior.cs:952-956), przez `starter.AddGameMenuOption("village", "arm_people", "{=!}People of the district", warunek, skutek, false, indeks)`.
- Te same id menu uzywa BK (bkall\BannerKings.Behaviours\BKSettlementActions.cs:1237, 1299, 1364).
- Warunek ustawia `args.Tooltip = new TextObject(skrot)` - wzorzec SRC\SmithMenu.cs:1595-1601.
- Skutek wola tylko `InformationManager.ShowInquiry(new InquiryData(tytul, tekst, true, false, "Good", "", null, null), true)` - dokladnie jak SRC\SmithMenu.cs:1130-1131 i :1609-1611.
- Tekst po angielsku (CLAUDE.md:9), do ok. 12 linii:

```
People of the district - <Region> (<Realm>)
At home: 74,880 (villages 74,880, town 0)
Under arms: 60 (0.3% of grown men; free hands up to 20%)
Outlaws: 60 (in the woods 0, in bands 60)
Refugees: 0    Captives: 0
Fallen: today 0, since the start 0
Natural growth: +1 a day (+0.40% a year)
Harvest against the start: 99.9%
```

- **Ryzyko:** niskie. Opcja siedzi w zwyklym menu i nie przelacza menu, wiec zakaz `SwitchToMenu` z menu oczekiwania (CLAUDE.md:98-100, SRC\NightRest.cs:1070-1075) nie jest dotykany. Nie dodawac jej do menu `*_wait`.
- Warunek biegnie przy kazdym odswiezeniu menu: czytac tylko liczby zapisane raz na dobe, calosc w try/catch.

**(b) Linia w dymku osady na mapie - opcjonalnie.**
- Postfiks na `TooltipRefresherCollection.RefreshSettlementTooltip(PropertyBasedTooltipVM, object[] args)`, `args[0]` to `Settlement` (SCR\aneks-ksiega-bandyci\TooltipRefresherCollection.cs:892-894).
- Mod juz latka te klase (SRC\TooltipCondition.cs:14, `RefreshItemTooltip`), a referencje sa w Armoury.csproj:27-28.
- Zawsze `AddProperty("People", "74,880")`; przy `IsExtended` (Alt, uzyte w :1052, :1116) dodatkowo "Under arms", "Outlaws", "Fallen".
- Powod: vanilla pokazuje zmiane hearth formatem `0.00` (:1001), wiec po kroku 3 bedzie tam "+0.00".
- Dziala tez dla osad wrogich, do ktorych gracz nie wejdzie.
- **Ryzyko:** srednie. Dymek odswieza sie co klatke, wiec tylko liczby z pamieci; pomijac kryjowki. ROT latka inna metode tej klasy (rotall\ROT.HarmonyPatches.Core\EnlistmentPatches.cs:462), autor kolizji nie widzi.

**(c) Dopisanie linii do panelu ludnosci BK - odradzane.**
- Panel to `OverviewVM.StatsInfo` (bkall\BannerKings.UI.Management\OverviewVM.cs:171-193, wiersze :354-385, np. "Total Population:" :365).
- Otwiera sie tylko wlascicielowi osady (BKSettlementActions.cs:1238, :2738-2757).
- Armoury nie ma referencji do BK (Armoury.csproj:21-41), wiec trzeba by tworzyc `InformationElement` refleksja.
- Obok staloby "Total Population" BK w skali ok. 1:17, czyli dwie rozne "ludnosci" w jednym oknie.

### 3.4 Meldunek po kryjowce

Meldunek `Log.Player` w `HideoutPurge.OnHideoutBattle` (SRC\HideoutPurge.cs:59-100) oraz zmienny tekst menu `arm_hideout_search` (dzis staly, :260-262; wzorzec zmiennej SRC\IronBank.cs:300), np.: "Of 60 outlaws 20 lie dead, 8 are in irons, 32 fled into the woods."

### 3.5 Sprawdzenie krytyka

**API - wszystko istnieje:**

| Co | Dowod |
|---|---|
| `AddGameMenuOption(menuId, optionId, text, condition, consequence, isLeave, index, ...)` | twall TaleWorlds.CampaignSystem\CampaignGameStarter.cs:93 |
| Menu "village", "town", "castle" | BK BKSettlementActions.cs:1237, 1299, 1364; Armoury juz dodaje opcje do "town" (SRC\SmithMenu.cs:48, IronBank.cs:292) |
| Okno `ShowInquiry(new InquiryData(...))` | SRC\SmithMenu.cs:1130-1131, :1609-1611 |
| `RefreshSettlementTooltip(PropertyBasedTooltipVM, object[])` | SCR\aneks-ksiega-bandyci\TooltipRefresherCollection.cs:892; format 0.00 w :1001 |
| `VillageBeingRaided`, `VillageLooted`, `RaidCompletedEvent`, `OnPrisonerSoldEvent`, `MercenaryNumberChangedInTown` | twall CampaignEvents.cs:763, 765, 1001, 957, 669 |
| `GetMercenaryData`, `ChangeMercenaryType` publiczne | twall RecruitmentCampaignBehavior.cs:53-66, :156-164 |
| Katalog sesji, zamek, kasowanie powyzej 12 | SRC\Log.cs:10-12, :36-44 |

**Zasady projektu:**
- Napisy po angielsku, brak `SwitchToMenu`, odczyt liczb raz na dobe - zgodne z CLAUDE.md.
- **Narusza "jedna zmiana naraz"** (CLAUDE.md, rozdz. 8 pkt 2): log, CSV, opcja menu, dymek i meldunek kryjowki to piec zmian. Rozbic na osobne wpisy: log z CSV, potem menu, potem dymek.

**Nie sprawdzono (autor):** limitu dlugosci tekstu `InquiryData` oraz nazw wartosci `GameMenuOption.LeaveType` dla nowej opcji.

## 4. Bandyci i kryjowki w ksiedze ludzi

Skrypty: SCR\aneks-ksiega-bandyci\b1.py - b4.py; krytyk: SCR\aneks-krytyk\k5.py. [KRYT] Odpowiedz jest zgodna z SRC\OutlawLaw.cs; pomiary z LOG zgadzaja sie co do liczby.

### 4.0 Odpowiedz na pytania Jeffa

- **"Przyjezdni czy z danego regionu?"** W wiekszosci miejscowi, ale gra tego nie zapamietuje.
  - Banda sklada sie z puli wyrzutkow regionu kryjowki i 4 najblizszych regionow.
  - Prosci ludzie puli pochodza z wsi tego regionu (zdjeci z hearth).
  - Zolnierze puli (dezerterzy, rozbitkowie) sa przyjezdni: trafiaja do puli regionu, w ktorym zdezerterowali albo przegrali bitwe. W 27 dobach to 78% naplywu nowych ludzi (56.2 z 72.4 dziennie).
- **"Jak ich zabije, to tez ubywaja?"** Tak: ubywa ich z puli i band i nikt ich nie dosypuje. Ale nie sa nigdzie zapisani jako polegli, a ich wies odrasta sama.
- **"A kryjowki?"** Tak samo. Kryjowka odradza sie tylko z puli regionu i sasiadow. Z niczego biora sie tylko herszt (+1) i dosypki misji przy graczu.
  - Misja kryjowki ma limit 28 ludzi na poczatku gry i 44 pozno; nadwyzka jest kasowana z rosterow przed walka.
- **Po zmianie (4.5):** zabity bandyta = wyrzutki regionu pochodzenia -1, polegli +1, trwale.

### 4.1 Skad sa ludzie band

- **Pula jest per region** (miasto albo zamek z wsiami, 227): SRC\OutlawLaw.cs:53, :72-77, :90-100. Kryjowka nalezy do najblizszej warowni w linii prostej (:95-99).
- **Sklad nowej bandy:** najpierw pula regionu kryjowki, potem 4 najblizsze regiony (`Near`, :102-112; `Draw`, :232-263; `OutlawNeighbourRegions = 4`, SRC\Settings.cs:509). Banda moze wiec byc zlozona z ludzi 5 regionow. Udzialu sasiadow log nie podaje.
- **Zapas:** gdy banda nie ma osady macierzystej, bierze z regionu zapasowego (:551). Autor: "o najwiekszej puli na swiecie". [KRYT] To region o najwiekszej puli razem z 4 sasiadami (`Avail`).
- **Prosci ludzie** sa z wsi tego regionu: `TakeCommoners` zdejmuje 0.5 hearth za czlowieka, zaczynajac od najwiekszej wsi, z podloga 50 hearth (:159-177, Settings.cs:507).
  - siew: 3% hearth (:189-206, Settings.cs:497); LOG l.94: 6 562 ludzi w 227 regionach;
  - bieda i wojna: 0.1 na 1000 hearth x nedza (:433-434); LOG: 15.4 dziennie;
  - spalona wies: 3% hearth (:298-313); LOG: 0.8 dziennie.
- **Zolnierze puli sa przyjezdni:** dezerter trafia do puli regionu, w ktorym zdezerterowal (:266-275), rozbitek do regionu bitwy (:277-296, udzial 0.5). LOG, 27 dob: dezercja 44.7 dziennie, rozbitkowie 11.5 dziennie.
- **Banda wedruje:** dobiera do 2 ludzi dziennie z puli regionu, w ktorym akurat stoi (:480-492, `DrawOnly` :516-534). Jency przechodza do niej w tempie do 10% zdrowych dziennie (:464-479). [KRYT] Tempo jest skalowane tierem (:472): tier 1 ok. 8.6%, tier 3 ok. 5.7%, tier 5 ok. 2.9%.
- **Rozwiazana banda** oddaje ludzi do puli miejsca rozwiazania, nie pochodzenia (:315-327).

### 4.2 Czy kazda banda przechodzi przez pule

Tak dla wszystkich sciezek swiata AI. LOG l.29: "sklad band z puli w 5 modelach; wpiete: vanilla banda, vanilla lupiezcy, vanilla nowa kryjowka, NavalDLC piraci, BK kryjowka, BK bohater, BK dosypka, vanilla dezerterzy, awanse band, zloto dzienne, zloto startowe" - bez "BRAK".

Mechanizm: `BanditPartyComponent.InitializeBanditOnCreation` (cs\...PartyComponents\BanditPartyComponent.cs:28-38) wola `InitializeMobilePartyAtPosition(szablon)`, a ta `FindAppropriateInitialRosterForMobileParty` (cs\...Party\MobileParty.cs:2639-2651). Tam siedzi `RosterPostfix` (SRC\OutlawLaw.cs:540-561, latka :848-867).

| Sciezka | Dowod | Przez pule? |
|---|---|---|
| Nocne bandy, lupiezcy, nowa kryjowka (vanilla) | cs\...\BanditSpawnCampaignBehavior.cs:458-474, :300-333 | tak |
| Piraci NavalDLC (macierzyste miasto portowe) | naval\...\PiratesCampaignBehavior.cs:425-438 | tak |
| Kryjowka BK (8 band) | bkall\...\BKBanditBehavior.cs:273-280 | tak |
| Bohater bandycki BK | BKBanditBehavior.cs:248, BanditHeroComponent.cs:18, :71 | tak; dosypka 4 x 2-6 ludzi (:251-254) wylaczona (OutlawLaw.cs:623, :831) |
| Slabi piraci usuwani przez NavalDLC | PiratesCampaignBehavior.cs:235-254, twall\...\DestroyPartyAction.cs:34-42 | ludzie wracaja do puli |

Dowod z logu: doba 1 - pula 6 562, z tego 391 band i 6 392 ludzi, zostalo 299 (LOG l.198; rachunek domyka sie do 1; [KRYT] do 1.4 czlowieka). Banda urodzona bez ludzi jest kasowana (:557, :329-345): 75.3 dziennie, odmow 46.6 dziennie. Bramki sa nieszczelne lokalnie (`GateGlobal` patrzy na pule swiata >= 18, :591-602), ale nikt nie rodzi sie z niczego.

**Dziury - wszystkie male albo tylko przy graczu:**
1. **Herszt:** +1 czlowiek z niczego na kryjowke. `CheckForSpawningBanditBoss` dopisuje `BanditBoss`, gdy partia herszta go nie ma (BanditSpawnCampaignBehavior.cs:182-197), a podmiana wlasnie go usunela. Reszta partii herszta (szablon 2-4 + 1 + 1, partyTemplatesROT.xml:344-350, :639-645) idzie z puli.
2. **Misja kryjowki tnie i dosypuje** (cs\...\HideoutCampaignBehavior.cs):
   - ponad limit ludzie sa kasowani z rosterow przed walka (:606-630, kasowanie :626);
   - ponizej 10 sa dosypywani z niczego (:652);
   - przy szturmie dosypka do 25 (:693-714, :712);
   - limit to 28 na poczatku gry i 44 pozno (twall\...\DefaultBanditDensityModel.cs:24-28; ROT tego nie zmienia, rotall\ROT.Models\ROTBanditDensityModel.cs:22-26).
3. **Zadania i incydenty gracza:** twall\...\IncidentsCampaignBehaviour.cs:2902-2903 (3 bandy po 5-17), CaravanAmbushIssueBehavior.cs:555-558 i EscortMerchantCaravanIssueBehavior.cs:1264-1268 (sklad z puli jest kasowany przez `MemberRoster.Clear()` i wstawiany nowy), ExtortionByDesertersIssueBehavior.cs:961, :976, LandlordNeedsAccessToVillageCommonsIssueBehavior.cs:695-696, rotall\...\ROTEnlistmentBehavior.cs:333.
4. **Bohater BK** to 1 osoba z niczego (BKBanditBehavior.cs:245).

### 4.3 Co dzis dzieje sie z zabitymi i jencami

- **Zabity w polu albo w kryjowce:** znika. Zdarzenia `OutlawLaw` ksieguja tylko rozbitych (:277-296) i bandy rozwiazane bez sprawcy (:320). Hearth zdjety przy wyjsciu w las nie wraca, ale vanilla dolewa wsi +1.2 dziennie, wiec sladu nie ma.
- **Zabity dezerter:** nie zmienia zadnej liczby ludnosci, bo nigdy nie zdjal hearth.
- **Zywi z przegranej bandy:** czesc trafia do niewoli zwyciezcy, reszta jest kasowana. `CaptureDefeatedPartyMembers` zdejmuje wszystkich z rostera, takze niezlapanych (twall\...\MapEvents\MapEvent.cs:1955-2031, zdjecie :2030). Nie wracaja do puli.
- **Jeniec sprzedany w osadzie:** kasowany (twall\...\Actions\SellPrisonersAction.cs:19-24). BK zalicza go do niewolnikow osady albo wypuszcza wedlug polityki karnej; przy wypuszczeniu kultury bandyckie i zawod "bandyta" sa pomijane (bkall\BannerKings.Patches\SettlementPatches.cs:21 i dalej).
- **Powrot z puli do wsi:** 0.5% dziennie plus 2% x dobrobyt w pokoju (:439-449, Settings.cs:503-504); LOG 3.0 dziennie. Wracajacy dezerter tez dodaje 0.5 hearth, ktorego nie zabral, i to najslabszej wsi regionu (:179-187).
- **"Wyslij ludzi" na kryjowke** (wynik sukces): bandyci znikaja tak samo (HideoutCampaignBehavior.cs:217-233).

**Kryjowka po oczyszczeniu:**
- Jest zarazona przy co najmniej 4 bandach w srodku (cs\...\Settlements\Hideout.cs:25, ROTBanditDensityModel.cs:18).
- Odradza sie tylko z puli: `FillANewHideoutWithBandits` wstawia 4 bandy z szablonu, czyli z puli regionu i sasiadow (BanditSpawnCampaignBehavior.cs:300-310).
- Jedyny warunek to 18 wyrzutkow gdziekolwiek na swiecie (:591-602, :619); gdy lokalnie nikogo nie ma, bandy rodza sie puste i sa kasowane.
- Z niczego: tylko herszt (+1) i dosypki misji z 4.2.

**`HideoutPurge` i `HideoutSpawnShim` nie ruszaja ludzi:**
- `HideoutPurge` rozdaje zloto i towar z kasy kryjowki (SRC\HideoutPurge.cs:85-93, :475-538). Odwet scala bandy bez strat (:367-395).
- `HideoutSpawnShim` to pusta zaslepka dla DTE (SRC\HideoutSpawnShim.cs:18-31, SRC\SubModuleMain.cs:141).

### 4.4 Pomiar, 27 dob 108838-108864 (b2.py, b3.py)

| Pozycja | Razem | Dziennie |
|---|---|---|
| Ludzi w bandach | 6 107 -> 5 692 (band 369 -> 413) | - |
| Weszlo do band (nowe bandy, werbunek, jency) | 2 376 | 88.0 |
| Wrocilo do puli z rozwiazanych band | 759 | 28.1 |
| Ubytek w walce i niewoli (z bilansu) | 2 032 | 75.3 |
| Bandyci zabici wg BIT (bez malych potyczek) | 1 722 | 63.8 |
| Zabici wszystkich partii | 11 377 | 421 |

- Bandyci to 15% zabitych swiata. Rocznie SZACUNEK ok. 27 tys. (75.3 x 364; poczatek kampanii, nie stan ustalony).
- W przegranych starciach band: 1 203 zabitych wobec 561 rannych, czyli 68% do 32%. "Rozbitych" bandytow: 0.
- [KRYT] "Rozwiazane bandy 759" zawiera tez jencow tych band (:324), wiec ubytek 2 032 jest lekko zanizony.
- [KRYT] Rownosc 2 032 (ubytek ludzi) = 2 032 (suma "puste usuniete") to przypadek: jedno liczy ludzi, drugie partie.
- [KRYT] Potwierdzone z LOG (k5.py): dezercja 44.7, rozbitkowie 11.5, bieda 15.4, powrot 3.0 dziennie.

### 4.5 Propozycja ujecia w ksiedze

- **Pochodzenie:** pula i kazda banda niosa slownik region pochodzenia -> liczba. `Draw` zna region kazdego losowania (:238-259); dla zolnierzy pochodzenie przychodzi z partii, z ktorej uciekli (krok 5 DOK).
- **Zabity bandyta:** wyrzutki regionu pochodzenia -1, polegli +1, trwale.
- **Jeniec:** konto "jency" regionu pochodzenia, dopoki siedzi w czyims rosterze. Wcielony przechodzi do "w sluzbie".
- **Jeniec sprzedany** (zdarzenie `OnPrisonerSoldEvent`, twall\...\CampaignEvents.cs:957) - do decyzji Jeffa:
  - przechodzi do ludzi osady kupujacej (zgodnie z niewolnictwem BK), albo
  - wraca do puli regionu pochodzenia.
- **Zywi niezlapani i przycieci przed misja** (MapEvent.cs:2030, HideoutCampaignBehavior.cs:626): ucieczka do puli regionu walki, pochodzenie zachowane.
- **Herszt i dosypki misji:** brane z puli regionu. Gdy pula pusta, pominac dosypke albo zalogowac "z niczego N".
- **Powrot do wsi:** prosty czlowiek wraca do hearth po 1/k. Zolnierz nie dodaje hearth, tylko idzie do "weteranow" (5.2).

**Przyklad: kryjowka 60 ludzi, region 75 000, wszyscy miejscowi, poczatek gry (limit misji 28).** Podzial 20 zabitych i 8 rannych to SZACUNEK z proporcji 68/32.

| Konto regionu | Przed | Po walce | Uwagi |
|---|---|---|---|
| W domu | 74 880 | 74 880 | |
| W sluzbie | 60 | 60 | |
| Wyrzutki | 60 (bandy 60) | 32 (pula 32) | 32 przycietych ucieka do lasu |
| Jency | 0 | 8 | w rosterze gracza |
| Polegli razem | 0 | 20 | trwale |
| Suma | 75 000 | 75 000 | |

- Trwala strata: 20 ludzi, czyli 0.027% regionu i 0.10% doroslych mezczyzn (20 250).
- Plon: (74 980 / 75 000)^0.5 = -0.013%.
- 32 uciekinierow wraca do wsi albo zbiera sie w nowa bande, gdy w okolicy jest ich co najmniej 6 (Settings.cs:508).
  - Autor: tempo do 2.5% dziennie w pokoju, polowa po ok. 28 dobach.
  - [KRYT] To sufit dla miasta o dobrobycie co najmniej 5 000 w pokoju. Region zamkowy (dobrobyt ok. 1 000-1 100; zamkow jest 130 z 227): 0.9% dziennie; w wojnie 0.5% dziennie; przy glodzie x0.25 (:439-440). Pomiar: 3.0 dziennie przy puli srednio 307, czyli ok. 1% dziennie. Polowa wraca po 70-140 dobach.
- Dla porownania dzisiejszy kod: 60 prostych bandytow to 30 hearth zdjete przy wyjsciu w las. Przy srednim k = 191 to 5 730 ludzi w skali 52.6 mln, czyli 7.6% regionu; vanilla odrabia to w ok. 12 dob (2 wsie x 1.2 dziennie).

**Nie sprawdzono:**
- Udzialu ludzi z sasiednich regionow w bandach i pochodzenia zolnierzy puli - log tego nie zapisuje.
- Losu jencow trzymanych przez bandy w oczyszczonej kryjowce i udzialu jencow lapanych wobec kasowanych (model `GetCaptureMemberChancesForWinnerParties`).

## 5. Zaraza z oblezen, najemnicy ze zwolnionych

### 5.1 Zaraza - tylko z oblezen

- **`PlagueWatch.cs` nie jest mechanika zarazy.** To meldunek o chorobie gracza z moda AIInfluence, czytany refleksja (SRC\PlagueWatch.cs:29-53, :56-97). AIInfluence jest wylaczony od 05.10 (docs\STAN-PRAC.md:9), wiec plik spi.
- **Warunek decyzji:** gdyby AIInfluence wrocil, choroby wybuchalyby poza oblezeniami (35 ognisk na partii gracza, SRC\ManLedger.cs:64-93), a Armoury ich nie bramkuje. Regula "tylko z oblezen" trzyma sie tylko przy wylaczonym AIInfluence.
- **Jedyna zaraza w grze to `CampFever.cs`, wylacznie w oblezeniu:**
  - petla po `SiegeEvents` (:38-54), inkubacja 9 dni (:62, Settings.cs:311);
  - 0.6% dziennie x narost 15% za dzien x tlok x strona (:137, :145-146);
  - obroncy 40% tempa, glod x2 (:98-100), umiera 10% chorych (:153);
  - zgon to `roster.AddToCounts(ch, -1)` (:184), dzis bez zadnego zapisu.

**Co zmienic:**
1. `Strike` (:121-161) zglasza zmarlych do ksiegi jako poleglych regionu pochodzenia partii.
2. Krok 8 DOK: losowa zaraza i przenoszenie na sasiadow usuniete (nie "domyslnie 0"). Wyzwalacz tylko `TickSiege` (:56-118).
3. Jesli cywile maja chorowac: tylko ludzie oblezonej warowni, nie regionu. Zamki maja 0 ludzi w tabeli (SRC\PopulationLaw.cs:118-120), wiec dotyczy to miast.

**Liczby (SZACUNEK ze wzoru, oblezenie 30 dni):**

| Pozycja | Autor | [KRYT] |
|---|---|---|
| Suma dziennych stawek za doby 9-30 | 0.6% x 56.65 | poprawne |
| 1 000 oblegajacych | tlok 1.41: do ok. 480 zachorowan, ok. 48 zmarlych | tlok liczy sie per partia (SRC\CampFever.cs:137), nie dla calej armii; partie po 112-250 ludzi maja tlok 0.60-0.71: 204-240 chorych, 20-24 zmarlych |
| 300 obroncow (tlok 0.77, x0.4) | ok. 32 zachorowania, ok. 3 zmarlych, w glodzie 6 | poprawne |
| Razem zmarlych zolnierzy na dlugie oblezenie | ok. 50 | ok. 25 |
| Dla porownania: odrzucona wersja regionowa (DOK) | 13-42 tys. | - |
| Ludnosc miasta 100 tys. przy stawce zalogi | ok. 1 050 zmarlych, w glodzie 2 100 | nie przeliczane |

- Cywile miasta wymagaja osobnej decyzji Jeffa i pokretla.
- **Pomiar:** w 28 dobach LOG 0 linii "CampFever" i 0 szturmow, 2 wypady. Jedyny zapis to Armoury-2026-10-04_05-45-35.log l.209-210, 312: `town_EN3`, dni 9-10, oblegajacy 3-4 chorych, obroncy 1 chory, 0 zmarlych.

### 5.2 Najemnicy z karczmy - tylko ze zwolnionych z wojska

**Dzis.** Kazde miasto co 2 dni losuje nowa paczke z niczego, a wykupione dolosowuje nazajutrz (twall\...\RecruitmentCampaignBehavior.cs:204-207, :341-371). Wielkosc paczki to (6 - tier) x 2 do x 5 (:403-412). AI wynajmuje przez `ApplyInternal(MercenaryFromTavern)` (:414-502, :606-643), a zloto idzie do miasta (SRC\LevyGold.cs:43-49).
- SZACUNEK podazy: 97 miast x 8.25 (tier 3) co 2 dni = 400 dziennie, do 800 przy codziennym wykupie. [KRYT] Rachunek zgadza sie z kodem.
- LOG, 26 dob: 85 356 zl dziennie do miast (od 113 017 do ok. 65 tys.). Przy 140-240 zl za czlowieka (AUDYT :990) to 356-610 najemnikow dziennie, w ostatnich dobach 270-480. [KRYT] Liczb zlota i wynajec krytyk nie przeliczal.

**`MusterOut.cs` nie zwalnia nikogo** - to auto-sortowanie partii gracza (SRC\MusterOut.cs:21-97). Gra nie ma tez zdarzenia "zwolniono zolnierza" (lista zdarzen w twall\...\CampaignEvents.cs).

**Co gra dzis robi z ludzmi odchodzacymi z wojska:**

| Strumien | Dowod | Dziennie (LOG) |
|---|---|---|
| Dezercja do puli wyrzutkow | SRC\OutlawLaw.cs:266-275 | 44.7 |
| Rozbici: polowa do puli, polowa znika | OutlawLaw.cs:277-296 | 11.5 + 11.5 |
| Powrot z puli do wsi | OutlawLaw.cs:439-449 | 3.0 |
| Nieoplaceni w `WarLedger` znikaja | SRC\WarLedger.cs:113-138 | brak licznika |
| Rozwiazana partia: garnizon swojej frakcji, inaczej kasowana; we wsi polowa do milicji | twall\...\DisbandPartyCampaignBehavior.cs:290-385 | brak licznika; partii ubywa 9.0 (CSV economy-2026-10-05_15-23-49.csv, razem z rozbitymi) |
| Zwolnienie pojedynczych ludzi przez AI | brak takiego kodu | 0 |
| Demobilizacja BK | martwa (DOK, tab. 1.2) | 0 |

**Wynik.** Mierzalna podaz "bylych zolnierzy" to 15-70 dziennie wobec 356-610 wynajmowanych, czyli spadek najmu o 81-97%.
- Waski wariant (rozbici wracajacy do domu plus powrot z lasu): ok. 15 dziennie.
- Szeroki wariant (z dezerterami): ok. 68 dziennie.
- Najemnicy to 9-16% naplywu wojska brutto (3 817 dziennie wg sumy wzrostow w CSV). Miasta traca 73-83 tys. zl dziennie.
- "Karczmy pelne po wojnie" nie zadziala samo: AI nigdy nie zwalnia, a pokoj nie rozwiazuje partii. Potrzebna osobna regula zwolnien, np. nadwyzka ponad budzet zoldu w pokoju - do decyzji Jeffa.

**Co zmienic:**
1. Nowe konto "weterani" per region z miastem, w formacie puli wyrzutkow.
2. **Doplywy:**
   - ludzie rozwiazanych partii niewchlonieci przez garnizon - prefiks na `MergeDisbandPartyToFortification` / `ToVillage`, przed kasowaniem rostera (:361);
   - druga polowa rozbitych;
   - zolnierze wracajacy z puli wyrzutkow (zamiast dodawac hearth);
   - nieoplaceni z `WarLedger.DesertElitesFirst`;
   - zwolnieni przez gracza - roznica rostera po ekranie partii; hak juz jest w SRC\MusterOut.cs:28-30.
3. **Karczma:** prefiks wylaczajacy `UpdateCurrentMercenaryTroopAndCount` i ustawienie `GetMercenaryData(town).ChangeMercenaryType(typ, liczba)` (publiczne, :53-77, :156-164), gdzie liczba = weterani regionu.
4. **Wynajecie** lapie jedno zdarzenie dla AI i gracza: `CampaignEvents.MercenaryNumberChangedInTown` (CampaignEvents.cs:669).
   - [KRYT] `ChangeMercenaryType` przy zmianie typu ustawia liczbe bez zdarzenia o liczbie (:53-60), a zdarzenie pada tez przy naszym wlasnym ustawieniu. Licznik wynajec potrzebuje flagi "to my".
5. **Ograniczenie pierwszej wersji:** typ w karczmie musi zostac najemnikiem kultury, bo AI wynajmuje tylko `Occupation.Mercenary` (:464), a karawany `CaravanGuard` (:419). Weteran zachowuje liczbe, traci wlasny typ oddzialu.
6. **Log:** "Karczmy: dzien N | weterani X w Y z 97 miast | przybylo: rozwiazane a, rozbici b, z lasu c, gracz d | wynajeci e".

**Nie sprawdzono:** liczby ludzi z rozwiazanych partii lordow i zwolnien gracza (brak licznika). Czy zwolnienie w ekranie partii przechodzi przez `TroopRoster.AddToCountsAtIndex`, autor nie czytal (`PartyScreenLogic` nie jest zdekompilowany).

## 6. Poprawiona kolejnosc krokow demografii

Podstawa: DOK rozdz. 5 (kolejnosc "zlozenie obu" z 5.1, kroki 1-10 z 5.2). Nizej tylko to, co zmieniaja decyzje Jeffa i raporty aneksu; reszta krokow zostaje jak w DOK. K1-K13c to kroki fundamentu.

### 6.1 Co sie zmienia w krokach

| Krok DOK | Zmiana | Skad |
|---|---|---|
| 1. Ksiega "Ludzie:" - sam log | Jeden wpis "log z CSV": linia "Ludzie:" i plik ludzie.csv. Do tego linia pomiarowa "Spustoszenie:" (rabunki pelne i przerwane, napastnicy, osobodni, czas lupienia z RaidDamage, osobodni zerowania, liczba wsi Looted, S srednie i najwyzsze). `OutlawLaw._pool`, `Count`, `RegionOf`, `Nodes` z private na internal. Konto milicji w spisie. | pytanie Jeffa; 3.1-3.2; 1.8 pkt 2 i 9; [KRYT] |
| 1. - stan w save | DOK: "bez stanu w save". "Polegli razem" i "plon wzgledem startu" wymagaja nowego klucza (2 x 227 liczb), inaczej zeruja sie przy wczytaniu. | 3 (zastrzezenie); [KRYT] sluszne |
| Widok w grze (nowe, osobne wpisy) | Po logu z CSV: opcja menu "People of the district", potem dymek osady (opcjonalnie). Meldunek po kryjowce osobno. | pytanie Jeffa; 3.3-3.5 |
| 2. Jednostka 1/k hearth | Bez zmian. Jest warunkiem spustoszenia: bez niego kazdy wracajacy wyrzutek dodaje 0.5 hearth. | [KRYT], 1.8 pkt 8 |
| 3. Przyrost naturalny + 9a | Bez zmian (regula przyjeta). Jest warunkiem spustoszenia: bez niego vanilla odrabia strate w godziny, a stan Looted zdejmuje -1 hearth dziennie w nicosc. | decyzja Jeffa; [KRYT], 1.8 pkt 8 |
| Sesja bazowa 60-90 dob | Ma zmierzyc rabunki linia "Spustoszenie:" przed wlaczeniem kroku 4. | 1.8 pkt 2 |
| 4. Spustoszenie | Nowy ksztalt. Zamiast "strata vanilli x k -> 10% polegli, 90% uchodzcy": rabunek pustoszy ulamek okregu (R x napastnicy x doby), 5% zabici (nie wiecej niz N), 5% wyrzutki, 90% uchodzcy. Z czterema naprawami krytyka (blokada ponownego rabunku albo lup od A; panika 2 doby; +20 milicji; strata vanilli z RaidDamage). Regula 3% hearth w `OnVillageLooted` wylaczona, blizna usunieta, nedza od udzialu ludzi spustoszonych. | decyzja Jeffa o spalonej wsi; rozdz. 1 |
| 6a. Plon od ludzi (razem z krokiem 4) | Dochodzi czynnik (1 - S_v)^eS; prog 0.001 w ProdPostfix zdjac takze dla niego. | 1.4, 1.7 pkt 6 |
| 5. Pobor, smierc i powrot + 9b | Dochodza: pochodzenie w puli i bandach, zabity bandyta jako polegly, konto "jency", uciekinierzy z misji do puli regionu walki; zgony CampFever jako polegli regionu pochodzenia partii. Wyjatek Nocnej Strazy czeka na decyzje o malych krainach. | 4.5; 5.1 pkt 1; rozdz. 2 |
| Weterani i karczma (nowe) | Konto "weterani" na region z miastem; karczma = liczba weteranow. Zmienia regule 4.4 DOK: zwolniony zolnierz nie dodaje hearth, tylko idzie do weteranow. Zmienia 9b: karczma nie schodzi z konta miasta [RED]. | decyzja Jeffa; 5.2; 4.5 |
| 6b. Czynnik progu wolnych rak | Prog przyjety: 20%. DOK: budowac dopiero po decyzji o progu, bo przy 20% nic nie zmieni poza Nocna Straza. | decyzja Jeffa; DOK 5.2 krok 6 |
| 7. Pobor czyta pule z ksiegi | f = 20% przyjete; reszta bez zmian. | decyzja Jeffa |
| 8. Zaraza regionu | Losowa zaraza i przenoszenie na sasiadow usuniete. Zostaje zaraza obozowa z oblezen (juz dziala). Cywile oblezonego miasta - tylko po osobnej decyzji Jeffa. | decyzja Jeffa; 5.1 |
| 9c, 10 | Bez zmian. | - |

### 6.2 Kolejnosc po zmianach

**K1 + krok 1 (log "Ludzie:" z ludzie.csv, pomiar "Spustoszenie:", milicje, lochy, zdarzenia gracza, proba na sucho przyrostu) -> opcja menu "People of the district" -> dymek osady (opcjonalnie) -> K2 -> K3 -> krok 2 -> krok 3 + 9a -> sesja bazowa 60-90 dob -> K5 -> krok 4 w nowym ksztalcie + 6a -> krok 5 + 9b (z bandytami, jencami i zgonami zarazy obozowej) -> weterani i karczma -> krok 7 -> K13a/b -> decyzja o pulapie zaworu -> K7, K6, K9, K10, K8, K11 -> K12 + 9c (nowa kampania) -> K13c.**

- **Po decyzjach Jeffa (nadal otwarte):** 6b; cywile oblezonego miasta; wariant dla malych krain i hamulec naboru; regula zwolnien AI. Krok 10 tylko, gdy log po K2 pokaze wysychanie klas BK (bez zmian).
- **[RED] Miejsca nowych elementow** wynikaja z zaleznosci, raporty nie podaja ich pozycji w lancuchu:
  - widok w grze zaraz po kroku 1 - krytyk podal tylko kolejnosc "log z CSV, potem menu, potem dymek"; do krokow 3-6a czesc pol okna to przyblizenie albo proba na sucho (3.1);
  - "weterani i karczma" po kroku 5 - doplywy weteranow to strumienie kroku 5 (rozwiazane partie, rozbici, powrot z puli wyrzutkow).

### 6.3 Uzasadnienia i braki wspolne (krytyk)

1. **Kolejnosc.** Spustoszenie i zmiany w OutlawLaw maja sens dopiero po krokach 2 i 3 DOK.
2. **Pomiar.** Zaden log nie podaje czasu lupienia (RaidDamage), liczby rabunkow przerwanych ani osobodni zerowania. Bez tego R i R_F sa strojone na slepo.
3. **Milicje** nie maja konta w zadnym z trzech raportow, a to one gina przy rabunkach (1 265 w 27 dob) i rodza sie z niczego (+20 przy kazdym powrocie wsi do Normal).
4. **Wojna powszechna trwa od doby 22 do 433** (ok. 1.1 roku gry). [RED] Sesja bazowa 60-90 dob liczona od poczatku kampanii obejmie wiec 22 doby pokoju, a reszte w wojnie.
5. **Jedna zmiana naraz:** kazdy element z 6.1 osobnym wpisem.

## 7. Pytania, ktore zostaly

### 7.1 Do decyzji Jeffa

**Spustoszenie**
1. **Sila rabunku R:** 0.5 (rabunek pocztu prawie niewidoczny: -0.3%, okreg-mediana -0.2% [KRYT]) czy blizej 1.0 (gorna granica historyczna)?
2. **Ponowny rabunek:** wlasna blokada na czas vanilli (8.3-16.7 doby) czy lup liczony od A [KRYT]? Bez tego do 80-100 pelnych rabunkow jednej wsi rocznie zamiast 20-35 (SZACUNEK).
3. **Wzor:** zostawic `(S_MAX - S_v)` (liczby nizsze) czy zmienic na `(1 - S_v/S_MAX)` (liczby autora) [KRYT]?
4. **Lup lupiezcy** (4 zl za hearth i towary z niczego, liczone od vanillowych -39%) - zostawic ksiedze pieniadza?
5. **BK i BetterEconomy** (-8% chlopow, jency do 150) w innej skali - zostawic niespojnosc?
6. **Male krainy przy rabunku:** poczet 300 to 13.6% okregu Nocnej Strazy.

**Male krainy**
7. **Nocna Straz:** (b) rekrut z calego Westeros przy wojsku 3 376 (9.1-16.1 tys. rocznie z Westeros), (b + c) wojsko 1 000 z Westeros (2.7-4.8 tys. rocznie), czy (a) tabela 60 tys. z hamulcem (zalozenie bez kanonu)?
8. **Wolni Ludzie:** zaden wariant tabeli, progu ani wielkosci wojska nie zmienia wyniku. Czy ruszac przyczyne [KRYT]: przerwy w stalej wojnie ze Straza, smiertelnosc bitew, Inni?
9. **Sarnor:** zostawic 100 tys. (5x kanon, wojsko pod progiem) czy 20 tys. (kanon; kraina znika w 1.3-1.8 roku)?
10. **Hamulec naboru** (pytanie 10 DOK): czy nabor ma stawac, gdy sluzacy przekrocza prog x zyjacy mezczyzni?
11. **Skok +20 020 ludzi w jedna dobe z szablonow partii** - kto za niego placi w ksiedze [KRYT]?
12. **Wyspy Letnie i Yi Ti:** tabela za duza wobec hearth (225 000 i 26 667 ludzi na wies) - poprawka tabeli (pytanie 13 DOK)?

**Ksiega i widok**
13. **Stan w save juz w kroku 1** (nowy klucz na start i poleglych regionu)?
14. **ludzie.csv:** nie kasowac z katalogiem sesji czy trzymac drugi plik narastajacy na kampanie?
15. **Dymek osady** (opcjonalny, ryzyko srednie) - robic?
16. **Milicje:** osobne konto w ksiedze [KRYT]?

**Bandyci**
17. **Jeniec sprzedany:** do ludzi osady kupujacej (zgodnie z niewolnictwem BK) czy do puli regionu pochodzenia?
18. **Herszt i dosypki misji przy pustej puli:** pominac dosypke czy logowac "z niczego N"?

**Zaraza i najemnicy**
19. **Cywile oblezonego miasta:** maja chorowac? Miasto 100 tys. przy stawce zalogi to ok. 1 050 zmarlych w 30 dni, w glodzie 2 100.
20. **AIInfluence:** regula "tylko z oblezen" trzyma sie tylko, gdy zostaje wylaczony.
21. **Spadek najmu o 81-97%** i 73-83 tys. zl dziennie mniej dla miast - do przyjecia?
22. **Doplyw weteranow:** waski (ok. 15 dziennie) czy szeroki, z dezerterami (ok. 68 dziennie)?
23. **Regula zwolnien AI** (np. nadwyzka ponad budzet zoldu w pokoju) - bez niej karczmy nie zapelnia sie po wojnie.
24. **Weteran traci wlasny typ oddzialu** (w karczmie stoi jako najemnik kultury) - do przyjecia w pierwszej wersji?
25. **[RED] Regula "zwolniony wraca" a konto weteranow:** w propozycji 5.2 zwolniony zolnierz nie dodaje hearth, tylko czeka w karczmie. Raporty nie mowia, czy i kiedy niewynajety weteran wraca do wsi.

**Prog 20%**
26. **Krok 6b:** przy przyjetym progu 20% czynnik nie zmienia plonu poza Nocna Straza (krytyk DOK). Budowac czy pominac?

### 7.2 Do pomiaru (nie do decyzji)

- Czestosc pelnych i przerwanych rabunkow, czas lupienia, osobodni zerowania, liczba wsi Looted naraz.
- Czy bandy w tym zestawie modow w ogole rabuja wsie.
- Stan ustalony strat krain: jedna sesja 28 dob, ekstrapolacja x13 miesza pokoj ze szczytem wojny.
- Garnizony "dzis" wprost (log podaje stan tylko dla 27 warowni) i liczba notabli po zmianach BK.
- Udzial ludzi z sasiednich regionow w bandach, pochodzenie zolnierzy puli, los jencow band.
- Liczba ludzi z rozwiazanych partii lordow i zwolnien gracza; sciezka zwolnienia w ekranie partii.
- Liczba oblezen co najmniej 9 dni rocznie (w 28 dobach: 0 linii "CampFever").
- Limit dlugosci tekstu `InquiryData` i wartosci `GameMenuOption.LeaveType`.
- Kanon: numery rozdzialow, 588 zetonow, 14 ocalalych, 3 119 i 6 000, 20 tys. Zelaznych Wysp; roczny naplyw rekrutow Strazy (wiki nie podaje).
- Historia: liczba parafii z misji papieskiej 1340 i sily szkockie 1319; Normandia i Ile-de-France tylko ze streszczen.

## 8. Zrodla

### 8.1 Kod Armoury i dokumenty repo (C:\Users\GAME\Bannerlor2-Realm-of-Thrones)

- Armoury\src: PopulationLaw.cs (:37-69, :112-121, :294), OutlawLaw.cs (:53-112, :159-206, :232-345, :386-492, :516-561, :591-623, :831-867), ScorchedEarth.cs (:57-110), HideoutPurge.cs (:59-100, :260-262, :367-395, :475-538), HideoutSpawnShim.cs (:18-31), CampFever.cs (:38-184), PlagueWatch.cs (:29-97), MusterOut.cs (:21-97), WarLedger.cs (:113-138), LevyGold.cs (:43-49), ManLedger.cs (:64-93), MaterialLaw.cs (:158-182), WinterBite.cs (:121), BattleChronicle.cs (:59), KingdomTreasury.cs (:190), Log.cs (:10-12, :26, :36-44, :75-90), SmithMenu.cs (:48, :1130-1131, :1595-1611), IronBank.cs (:292, :300), NightRest.cs (:1070-1075), TooltipCondition.cs (:14), ArmouryBehavior.cs (:417-423, :504, :952-956, :1173), SubModuleMain.cs (:141), Settings.cs (:311, :325-331, :497, :503-509); Armoury.csproj (:21-41).
- CrashScribe\src\Fabula.cs (:245-262), EconomyAudit.cs (:61-62, :125); CLAUDE.md (:9, :98-100, rozdz. 8 pkt 2).
- docs\DEMOGRAFIA-SILA-ROBOCZA-2026-10-05.md (rozdz. 4, 5, 6), docs\EKONOMIA-FUNDAMENT-2026-10-05.md, docs\STAN-PRAC.md (:9), docs\AUDYT-2026-10-05-EKONOMIA-WOJNA-SUROWCE-LORDOWIE.md (:990).

### 8.2 Logi, pomiary, pliki gry

- LOG (l.29, 94, 159, 198, 1509, 1625, 1688, 1776, 1785-1814; linie "Skarbce:", "Wyrzutki:", "Bitwy:", "Ludnosc:"); BIT; ...\Modules\Armoury\Logs\2026-10-05_15-22-29\zakupy.log.
- ...\Modules\Armoury\Armoury-2026-10-04_05-45-35.log (l.209-210, 312: zaraza obozowa); pozostale logi Armoury z 4-5.10 (12 logow, ok. 124 doby).
- C:\Users\GAME\Documents\Mount and Blade II Bannerlord\CrashScribe\economy-2026-10-05_15-23-49.csv; session-2026-10-05_15-22-28.log (l.198).
- ...\Modules\ROT-Map\ModuleData\settlements.xml; ...\Modules\ROT-Content\ModuleData\spclans.xml, ROT_spkingdoms.xml (:17-19), partyTemplatesROT.xml (:344-350, :639-645).
- ...\Modules\BetterEconomy\ModuleData\better_economy_settings.xml (:75-81).

### 8.3 Dekompilacje (tylko odczyt)

- cs (SCR\ore-supply\cs): DefaultRaidModel.cs, RaidEventComponent.cs, DefaultVillageProductionCalculatorModel.cs, DefaultSettlementProsperityModel.cs, VillageHealCampaignBehavior.cs, IncreaseSettlementHealthAction.cs, GarrisonTroopsCampaignBehavior.cs, Hero.cs, DefaultNotableSpawnModel.cs, BanditPartyComponent.cs, MobileParty.cs, BanditSpawnCampaignBehavior.cs, HideoutCampaignBehavior.cs, Hideout.cs.
- twall (SCR\audyt-pieniadz\twall): MapEvent.cs, AiMilitaryBehavior.cs, CampaignWarManagerBehavior.cs, CampaignEvents.cs, CampaignGameStarter.cs, RecruitmentCampaignBehavior.cs, DisbandPartyCampaignBehavior.cs, SellPrisonersAction.cs, DestroyPartyAction.cs, DefaultBanditDensityModel.cs, IncidentsCampaignBehaviour.cs, CaravanAmbushIssueBehavior.cs, EscortMerchantCaravanIssueBehavior.cs, ExtortionByDesertersIssueBehavior.cs, LandlordNeedsAccessToVillageCommonsIssueBehavior.cs.
- bk / bkall: BKRaidCaptureModel.cs, BKVillageProductionModel.cs, BKSettlementActions.cs, BKBanditBehavior.cs, BanditHeroComponent.cs, SettlementPatches.cs, OverviewVM.cs.
- rotall: ROTOthersCampaignBehavior.cs, ROTStorylineWars.cs, ROTSendToWallBehavior.cs, ROTBanditDensityModel.cs, ROTEnlistmentBehavior.cs, EnlistmentPatches.cs.
- naval (SCR\przeglad100-latka\naval): PiratesCampaignBehavior.cs.
- SCR\aneks-ksiega-bandyci\TooltipRefresherCollection.cs (dekompilacja dymka).

### 8.4 Skrypty i wyniki

- Spustoszenie: SCR\aneks-spustoszenie\calc.py.
- Male krainy: SCR\aneks-male-krainy\m1_tabela.py, m2_krainy.py, m3_szczegoly.py, m4_warianty.py, m1_out.txt - m4_out.txt.
- Widocznosc i bandyci: SCR\aneks-ksiega-bandyci\b1.py - b4.py, c1.py.
- Krytyk: SCR\aneks-krytyk\k1.py - k8.py.

### 8.5 Zrodla historyczne i kanon

- https://en.wikipedia.org/wiki/Black_Prince%27s_chevauch%C3%A9e_of_1355 (sprawdzone takze przez krytyka)
- https://en.wikipedia.org/wiki/Black_Prince%27s_chevauch%C3%A9e_of_1356
- https://erenow.org/ww/the-wars-of-the-bruces-scotland-england-and-ireland-1306-1328/6.php (McNamee; sprawdzone takze przez krytyka)
- https://www.persee.fr/doc/crai_0065-0536_1948_num_92_4_78341
- https://www.persee.fr/doc/assr_0335-5985_1977_num_44_2_2136_t1_0214_0000_3 (Bois, streszczenie)
- https://www.persee.fr/doc/rural_0014-2182_1964_num_15_1_1146_t1_0092_0000_1 (Fourquin, streszczenie)
- https://awoiaf.westeros.org/index.php/Military_strength (pobranie wprost zwraca 403; autor czytal przegladarka, krytyk ze streszczenia wyszukiwarki)
- https://awoiaf.westeros.org/index.php/Night%27s_Watch
- https://awoiaf.westeros.org/index.php/Gift
- https://awoiaf.westeros.org/index.php/Free_folk
- https://awoiaf.westeros.org/index.php/Iron_Islands
- https://awoiaf.westeros.org/index.php/Tall_Men ; https://awoiaf.westeros.org/index.php/Saath ; https://awoiaf.westeros.org/index.php/Kingdom_of_Sarnor
- https://awoiaf.westeros.org/index.php/Skagos
- https://atlasoficeandfireblog.wordpress.com/2016/03/06/the-population-of-the-seven-kingdoms/ (szacunki fanow)

