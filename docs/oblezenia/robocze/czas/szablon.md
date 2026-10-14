# ROZPOZNANIE - OBLEZENIA: CZAS I PRZEBIEG (14.10)

Zadanie: slowa Jeffa 14.10 po biegu dwuletnim - "sily nie sa rowne, oblezenia sa za szybkie i za latwe". Ta czesc: CZAS i PRZEBIEG oblezen
(start, machiny, decyzja o szturmie, szturm, wynik, glod, historia). Rozpoznanie - bez zmian kodu, bez decyzji.

Oznaczenia: **[P]** pomiar (plik / linia albo kolumna), **[K]** kod (plik:linia), **[U]** ustawienie Jeffa (plik), **[S]** szacunek z wzoru,
**[H]** historia, **[L]** lore ASOIAF - obie z wiedzy ogolnej, NIE sprawdzane w zrodlach w tej sesji.
Kalendarz: rok Armoury = 364 doby [U `Armoury.json`: `LongYearEnabled` true, `WeeksPerSeason` 13]; gra jest strojona pod rok 84 doby
(CLAUDE.md 7) - co w dobach trwa tyle samo, w ulamku roku trwa u nas 4.33 raza krocej niz w zamysle gry.

## 0. Prostym jezykiem

1. W biegu dwuletnim (fin30) bylo 206 oblezen; polowa trwala do 9 dob, najdluzsze 17 dob. Zdobyta twierdza padala po 12 dobach (mediana) - to 1/30 roku. W historii i w ksiazkach oblezenia trwaly miesiace (Storm's End prawie rok).
2. Mury przestaja sie liczyc po niecalym tygodniu: od 4. doby wzor gry co godzine "zjada" przewage murow; po 5.7 dobie wzor pozwala AI szturmowac nawet przy rownych silach, po ok. 6.5 przy dwa razy slabszych, po 7-8 przy cztery razy slabszych.
3. W praktyce AI nie zaczyna oblezenia bez okolo trzykrotnej przewagi (wzor wyboru celu gry), wiec w chwili decyzji o szturmie oblegajacy byli zawsze co najmniej 2.8 raza silniejsi (polowa - ponad 5.7 raza).
4. Dlatego 0 odpartych szturmow w 140 (fin30 + fin21): mury w symulacji daja obroncom tylko okolo dwa razy wiecej ciosow, a obroncy i tak sa w mniejszosci zwykle 3-12 do 1. 18 szturmow (fin30) skonczylo sie "bez rozstrzygniecia" i oblezenie upadlo - zadnego gra nie zapisala jako obrony.
5. Glod nie gra zadnej roli: twierdza ma jedzenia na 75-750 dob (polowa miast 274, zamkow 225), oblezenie konczy sie po 2 tygodniach - 0 dob glodu w 206 oblezeniach.
6. Twierdza ocala sie tylko wtedy, gdy przyjdzie wieksza odsiecz (42 wypady i 14 bitew pod murami przegranych przez oblegajacych; odsiecz zwykle 2 razy liczniejsza) albo gdy zaplaci okup (26 razy, po ok. 9 dobach).
7. Nasze spowolnienie budowy machin (razem z BK x0.25) przesuwa tylko decyzje o szturmie na ok. 8. dobe; sam szturm to bitwa, ktora w symulacji trwa 1-9 dob (mediana 4), i miasto pada ok. 12. doby.
8. Dzwignie sa w rozdz. 9 (czlon czasu w decyzji o szturmie, prog szturmu wobec murow, sila murow w symulacji, glod jako droga zdobycia, wytrzymalosc armii w dlugim oblezeniu) - nic nie zmienione, do decyzji.

## 1. Zrodla i metoda

| Co | Plik |
|---|---|
| fin30 (dwuletni, najnowszy kod) - log Armoury | `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-14_06-01-15.log` (287 MB) |
| fin30 - tabele | `D:\Backup-Bannerlord\logs\2026-10-14-noc\tabele-06-01-15\` (`spichlerze-regiony.csv`, `ludzie-regiony.csv`, `bitwy.log`) |
| fin30 - sesja CrashScribe | `D:\Backup-Bannerlord\logs\2026-10-14-noc\session-2026-10-14_06-01-14.log` |
| fin21 (dwuletni, wczesniejszy kod, bez okupu O1) | `...\Armoury-2026-10-13_21-14-13.log`, tabele `...\2026-10-14-noc\Logs\2026-10-13_21-14-13\` |
| skrypty i wyniki posrednie | `docs\oblezenia\robocze\czas\` - `oblezenia_czas.py`, `tabele_md.py`, `fin30.json`, `fin30-epizody.csv` (206 wierszy), `fin21-epizody.csv` (235), `*-oblezenia.log` (wyciag linii) |

**Epizod oblezenia** = ciag dob, w ktorych `spichlerze-regiony.csv` ma `oblezona=1` [P]. Kolumna to `Settlement.IsUnderSiege`, `zywnosc` = `Town.FoodStocks`,
`bilans_zywnosci` = `Town.FoodChange`, `glodna_warownia` = `IsStarving` [K `Armoury/src/Famine.cs:1048-1077`]. Wiersz pisany jest w ticku dobowym, czyli o polnocy:
wiersz z dniem D to stan na poczatku doby D+1 (sprawdzone: pierwsza linia dzienna logu ma `dzien 108837`, pierwszy wiersz CSV `108836` [P]). Dlugosc epizodu = liczba polnocy
w oblezeniu (+-1 doba). Kontrola: suma dob oblezen z CSV (miasta 410, zamki 1358) = suma linii "Oblezone (E5/A4): ... oblezone teraz" (409 + 1358) [P].

Do epizodow przypiete [P]: szturmy, wypady, bitwy pod murami (`bitwy.log`, linie `B175 dzien N | Siege / SallyOut / SiegeOutside`), zdobycia (`Lup miast (L0) ... bitwa szturm|wypad
| zdobycie tak`, 57 linii = 57 zdobyc Jeffa), decyzje gry o szturmie (`Okup oblezenia (O0)`, 113 linii), okupy (`Okup oblezenia (O1)`, 26), choroba obozu (`CampFever`).
Okno dopasowania: od doby startu do 2 dob po ostatniej polnocy oblezenia (bitwa konczy sie zwykle tuz po polnocy, przed tickiem dobowym - np. Cape Wrath: ostatni wiersz
`oblezona=1` 108856, B175 `dzien 108858` [P]).

Uwaga o liczbach B175 dla szturmu: "ludzi" = zdrowi teraz + ranni, polegli i rozbici w bitwie - tylko druzyn, ktore sa w bitwie przy jej koncu.
Przy szturmach wielodniowych bywa niepelne [K `KingdomBalance.cs:119-139`]: Krolewska Przystan B175 `O ... ludzi 92` [P `bitwy.log:36854`], a w CSV zaloga 541 + milicja 340 w dobie decyzji [P `ludzie-regiony.csv`
town_EW4, 109358]. Dlatego sile w chwili decyzji biore z linii O0 (stan tuz przed szturmem), a B175 tylko pomocniczo.

## 2. Przebieg oblezen - wyniki i czas

{TAB_FIN30}

{TAB_FIN21}

**Porownanie biegow [P]:** fin30 - 206 oblezen, 57 zdobyc (28%), 26 okupow; fin21 - 235 oblezen, 39 zdobyc (17%), 0 okupow (kod okupu O1 jeszcze nie istnial),
wiecej oblezen przerwanych wypadem (77 vs 42) i odstapionych (72 vs 47). Czas oblezen w obu biegach prawie ten sam: zdobycie mediana 12-13 dob, najdluzsze
oblezenie 17 (fin30) / 24 doby (fin21). Szturmow: fin30 73 (55 zdobyc, 18 bez rozstrzygniecia, **0 wygranych przez obronce**), fin21 67 (38 / 29 / **0**) [P `bitwy.log`, B175 "Siege"].

### 2.1 Typowe zdobycie krok po kroku (fin30)

| Etap | Ile trwa | Dowod |
|---|---|---|
| obozowanie, przygotowania i 2-3 machiny | ok. 4-6 dob przy 2352 ludziach w obozie (mediana) | [S] z kodu, rozdz. 4.1; brak linii w logu |
| pierwsza decyzja gry o szturmie (O0) | wiek oblezenia: mediana 8.4 doby, p10 6.4, min 4.2, max 12.7 (miasta 8.3, zamki 8.5) | [P] 113 linii O0, pole "oblezenie X dob" |
| decyzja -> poczatek szturmu | ta sama doba: 113 decyzji "true" = 113 cykli w calym biegu (po pierwszym "true" gra juz nie pytala - wodz przeszedl na szturm); zaloga zaczyna topniec w dobie decyzji (przyklady nizej). Ze 113 decyzji: 72 bitwy szturmu, 26 okupow (latka O1 wstrzymuje szturm), 14 bitew pod murami z odsiecza, 1 odstapienie | [P] suma "decyzje gry o szturmie (true)" i "cykli" z linii "Okup oblezenia (doba)", epizody; [K] `SiegeRansom.cs:266-283` |
| bitwa szturmu (symulacja co godzine) | 1-9 dob, mediana 4 (zaloga+milicja do 200: 2 doby, 200-400: 3, 400-900: 5) | [P] doba konca B175 - doba O0, 54 zdobycia z O0; [K] rozdz. 6 |
| zdobycie od startu oblezenia | mediana 12 dob (zamki 12, miasta 14), p90 14, max 17 | [P] tabela wyzej |
| w ulamku roku | 12 dob = 0.033 roku (364); w kalendarzu 84 bylaby to 0.143 roku (ok. 7 tygodni "roku gry") | [S] |

Przyklady z CSV (zaloga i milicja topnieja doba po dobie w czasie szturmu) [P `ludzie-regiony.csv`]:
- **Starfall** (miasto, Dorne): decyzja `dzien 108878`, wiek 6.1 doby, szansa na mysl 6.4% [P log:16532]; zaloga/milicja 285/181 -> 266/171 -> 235/148 -> 199/124 -> 161/109, zdobycie `dzien 108883`.
- **Krolewska Przystan** (Robb Stark): decyzja `dzien 109359`, wiek 8.0, sila gry 7700 vs 1556, przewaga 4.459^-1.141, "mury" 1.497 [P log:204696]; zaloga 541 -> 516 -> 469 -> 417 -> 357 -> 310 -> 271,
  milicja 340 -> 234, zdobycie `dzien 109365` (doba biegu 530). Zapas jedzenia w chwili upadku 24 125 przy ubytku 121/dobe = ok. 200 dob [P `spichlerze-regiony.csv` town_EW4, 109364].
- **Cape Wrath** (zamek): decyzja `dzien 108856`, wiek 10.4 [P log:8072]; zaloga 136 -> 102, zdobycie `dzien 108858` [P `bitwy.log:664`].

### 2.2 Czas w kalendarzu

| Miara | Doby | Lata (rok 364) | To samo w roku 84 (zamysl gry) | Zrodlo |
|---|---|---|---|---|
| oblezenie - mediana (fin30) | 9 | 0.025 | 0.107 | [P]/[S] |
| zdobycie - mediana | 12 | 0.033 | 0.143 | [P]/[S] |
| najdluzsze oblezenie fin30 / fin21 | 17 / 24 | 0.047 / 0.066 | 0.20 / 0.29 | [P]/[S] |
| decyzja o szturmie - mediana | 8.4 | 0.023 | 0.100 | [P]/[S] |
| zapas jedzenia miasta / zamku w oblezeniu - mediana | 274 / 225 | 0.75 / 0.62 | 3.3 / 2.7 | [P] rozdz. 7 |
| historyczne oblezenia twierdz (typowo) | ok. 50-330 | 0.15-0.9 | - | [H] rozdz. 8 |

Oblezenia sa rozlozone rowno: 1768 dob oblezen w 728 dobach = srednio 2.43 oblezonej twierdzy naraz na 227 twierdz [P]; ta sama twierdza bywa oblegana kilka razy
pod rzad (Demongate: 7 dob i odstapienie, 2 doby przerwy, 9 dob i okup, potem zdobyta w dobie biegu 132 [P aneks A, #4, #7, #32]).

## 3. Wszystkie zdobycia fin30

Kolumny "A/O moc", "ludzie" i "straty" sa z B175 - zastrzezenie z rozdz. 1 (przy dlugich szturmach obroncow bywa za malo, np. Krolewska Przystan 92 przy 541 + 340 w CSV); sila w chwili decyzji - kolumna O0.

{TAB_ZDOBYCIA}

## 4. Jak gra liczy postep oblezenia (kod)

### 4.1 Przygotowania i machiny

| Element | Wartosc | Zrodlo |
|---|---|---|
| postep na godzine | sqrt(zdrowi w obozie) / (osobodni x 24) + inzynieria, perki | [K] `DefaultSiegeEventModel.cs:351-401`, `:403-414` |
| buduje sie jedna rzecz naraz | najpierw przygotowania, potem pierwsza niezbudowana machina z listy | [K] `SiegeEvent.cs:1098-1124` |
| koszt (osobodni) | przygotowania 24 (Native 48), taran 12, wieza 16, katapulta 20, trebusz 40 | [K] `Modules\RBM\ModuleData\RBMCombat_siege_engines.xml` (RBM `SubModule.xml:194` laduje je po Native `siegeengines.xml`) |
| BannerKings `LongerSieges` | x(1 - 0.5) = x0.5 | [K] `bkfull\BannerKings.Patches\VanillaModelTweakPatches.cs:1251-1268`; [U] `BannerKings.json` 0.5 |
| RealisticBannerlord | x `SiegeConstructionSpeedMultiplier` (1.0), oblegajacy bez narzedzi (Tools) x0.75 | [K] RB `RealisticSiegeEventModel` (dekompilacja); [U] `RealisticBannerlord_v2.json` 1.0 |
| Armoury `SiegePacePercent` | x0.5, raz na lancuch modeli | [K] `Armoury/src/WorldPace.cs:128-145`; [U] `Armoury.json` 50 |
| razem dla oblegajacego | x0.25 (z narzedziami) albo x0.1875 (bez) wzgledem gry | [S] |

Czas do gotowosci (przygotowania + taran + wieza) [S z powyzszego]:

| Ludzi w obozie | koszty RBM bez mnoznikow (x1) | teraz, z narzedziami (x0.25) | teraz, bez narzedzi (x0.1875) |
|---|---|---|---|
| 1000 | 1.6 doby | 6.6 doby | 8.8 doby |
| 2000 | 1.2 | 4.7 | 6.2 |
| 2352 (mediana w chwili decyzji [P]) | 1.1 | 4.3 | 5.7 |
| 5000 | 0.7 | 2.9 | 3.9 |

(Bez perkow i umiejetnosci Engineering; czysta gra bez RBM ma przygotowania 48 osobodni zamiast 24.)

Wniosek [S]: obecne mnozniki (x0.25) odpowiadaja za ok. 3-5 z 8.4 doby do decyzji; reszte robi czekanie na okno w karze za machiny (rozdz. 5) i czlon czasu.
W chwili decyzji oboz mial zawsze 2-3 machiny (2: 84 razy, 3: 29), taran 110 razy, wieze 23 razy [P 113 linii O0].

### 4.2 Mury, ostrzal, naprawy

| Element | Wartosc | Zrodlo |
|---|---|---|
| wytrzymalosc murow | poziom 1: 30 000, 2: 50 000, 3: 67 000 (dzielone na odcinki) | [K] `DefaultWallHitPointCalculationModel.cs` |
| ostrzal (RBM) | katapulta 200 x 5 strzalow/dobe, onager 300 x 5, trebusz 1200 x 5 (przed trafieniem) | [K] `RBMCombat_siege_engines.xml` |
| naprawa murow | 0 w czasie oblezenia; po nim 4% calosci na dobe | [K] `DefaultSettlementGarrisonModel.cs:148-160`, `Town.cs:643-655` |
| wyrwa | przewaga osady x0.25 dopiero, gdy WSZYSTKIE odcinki maja 0 | [K] `DefaultCombatSimulationModel.cs:210-213` (cala przewaga `:172-227`) |
| czy szturm wymaga wyrwy | nie - decyzja patrzy na sily, przewage i czas | [K] `BesiegerCamp.cs:375-431` |

### 4.3 Straty obozu w czasie oblezenia

| Element | Wartosc | Zrodlo |
|---|---|---|
| RealisticBannerlord: 2-5 ludzi/dobe na druzyne (+1 co 5 dob) | wylaczone, gdy oboz nakarmiony (latka Mends); garnizon tylko bez jedzenia | [K] `CrashScribe/src/Mends.cs:4510-4570`; [P] sesja:283 "oboz oblezniczy nie gnije" |
| nasza choroba obozu (CampFever) | od 9. doby 0.6%/dobe, +15% za kazda dobe, x tlok | [K] `CampFever.cs:56-74`; [U] `Armoury.json` (Incubation 9, Base 0.6, Ramp 15) |
| ile to dalo w fin30 | 90 razy "zaraza wstaje" (9. doba oblezenia); 2129 chorych i 203 zmarlych oblegajacych w calym biegu | [P] linie "CampFever" (282 linie dzienne) |

## 5. Decyzja AI o szturmie

Wzor [K `BesiegerCamp.cs:375-431`], pytany tylko z mysli wodza obozu (`MobilePartyAi.GetBesiegeBehavior` [K `MobilePartyAi.cs:710-718`]), i tylko gdy przygotowania = 100%
(`IsReadyToBesiege` [K `BesiegerCamp.cs:49-59`]):

```
w     = 0.8 - 0.02 x (godzin oblezenia - 96)      (pierwsze 96 h: 0.8)        x1.25 bez taranu, x1.25 bez wiezy
s     = sila oblegajacych / (sila obroncow w murach x przewaga^w)          przewaga = (1 + 3 + poziom murow) / (1 + premie za machiny oblegajacych)
jesli s > 1: s x= min(3, machin)/3 x (1 - 0.85 x p^2),  p = postep budowanej machiny + 0.25 x (5 - machin)
szansa na mysl = 10% x s   (mysl AI co ok. 0.3 h w oblezeniu [K MobilePartyAi.cs:321-331; DefaultMobilePartyAIModel.cs:17])
```

Czlon czasu dziala w GODZINACH: po 96 h (4 doby) wykladnik spada o 0.02 na godzine, po 136 h (5.67 doby) jest 0, potem ujemny - mury zaczynaja dzialac na
KORZYSC oblegajacego (obroncy licza sie za mniej, niz ich jest). Ile dob, zanim wzor pozwoli na szturm przy danym stosunku sil [S z kodu; przygotowania musza byc gotowe]:

| Mury i machiny oblegajacego (przewaga, w poczatkowe) | 4:1 | 3:1 | 2:1 | 1:1 | 1:2 | 1:4 |
|---|---|---|---|---|---|---|
| mury 2, taran i wieza (3.82, 0.8) | od razu | od razu | 4.6 doby | 5.7 | 6.7 | 7.8 |
| mury 2, taran bez wiezy (4.26, 1.0) | 4.1 | 4.4 | 4.9 | 5.7 | 6.5 | 7.3 |
| mury 3, taran bez wiezy (4.97, 1.0) | 4.2 | 4.5 | 4.9 | 5.7 | 6.4 | 7.1 |
| mury 2 bez maszyn szturmowych (6.0, 1.25) | 4.6 | 4.8 | 5.2 | 5.7 | 6.2 | 6.7 |

Zmierzone w chwili decyzji (113 linii O0 fin30) [P]:

| Miara | Wartosc |
|---|---|
| wiek oblezenia | mediana 8.4 doby (p10 6.4, min 4.2, max 12.7) |
| wykladnik w | mediana -1.66; ujemny w 110 z 113 decyzji |
| przewaga murow | mediana 4.25 (3.06-5.56) |
| sila gry oblegajacy / obroncy | min 2.82, p10 3.45, mediana 5.74, p90 11.8, max 38.5; pustej warowni 0 |
| "mury" = stosunek bez czlonu czasu (jak gra w pierwszych 4 dobach) | mediana 1.50; <= 1 w 22 z 113 (19%) - tych szturmow bez czlonu czasu wzor by nie dopuscil |
| szansa na mysl | mediana 34.6%, p10 7.4%; 100% w 30 decyzjach |
| ludzie w obozie | p10 1350, mediana 2352, p90 4589 |

Co stalo sie dalej, wedlug "murow" w chwili decyzji [P, epizody fin30]:

| "mury" w chwili decyzji | szturm zdobyl | szturm bez rozstrzygniecia | okup O1 | oblegajacy pobity pod murami | odstapione |
|---|---|---|---|---|---|
| >= 1.5 (56 oblezen) | 43 | 5 | 0 | 8 | 0 |
| 1.0-1.5 (35) | 4 | 4 | 26 | 1 | 0 |
| <= 1.0 (22) | 7 | 9 | 0 | 5 | 1 |

(Okup bierze AI wlasnie przy "murach" < 1.5 - regula "krwawy szturm" [K `SiegeRansom.cs` naglowek, `Settings.cs:2150`].) Przy "murach" <= 1 szturm konczyl sie bez
rozstrzygniecia czesciej niz zdobyciem (9 vs 7) - tylko tu wzor gry "dopuscil" szturm, ktory mury powinny zatrzymac.

## 6. Szturm w symulacji - czemu 0 odpartych

**(a) AI nie zaczyna oblezenia bez przewagi** [K `DefaultTargetScoreCalculatingModel.cs:209-243`]: cel oblezenia odpada (wynik 0), gdy sila armii < 2 x (obroncy x (1 + 0.33 x poziom murow)
+ czesc sily krolestwa) - przy murach 2 to ok. 3.3 x sila obroncow (plus czesc sily krolestwa). SCA145 tylko mnozy wynik celu (front, ziemia, zmeczenie wojna) [K dekompilacja `StrategicTargetScoreModel`].
Skutek [P]: w chwili decyzji najslabsza przewaga 2.82, mediana 5.74 (rozdz. 5).

**(b) Mury w samej bitwie** [K `DefaultCombatSimulationModel.cs:241-281`, `:282-290`, `:336-343`; `MapEventSide.cs:993`]:
- runda co 60 minut (pole: co 30); w rundzie oblegajacy ma 2 x round(1.5 + O^0.3) ciosow, obronca 2 x round(0.5 + max(1 + O^0.3 x 0.7 x przewaga, (O+1)/(A+1))) - przy
  O = 450 obroncach i przewadze 4.25: 16 vs 40 ciosow, czyli obronca ok. 2.5 raza wiecej [S]; liczba ciosow NIE zalezy od liczby oblegajacych (dopoki obroncow > 100/30);
- premia murow tylko, gdy obroncow > 100 (miasto) / > 30 (zamek); ponizej tego ciosy licza sie od liczebnosci (A^0.6) i resztka garnizonu ginie szybko;
- oblegajacy -10% przewagi w szturmie; cios wylacza czlowieka z szansa obrazenia / max HP (ok. 1/4) [K];
- ROT owija model gry i zmienia tylko smoki [K dekompilacja `ROT.Models.ROTCombatSimulationModel`].
Stad bitwa szturmu trwa doby (rozdz. 2.1): oblegajacy traci ok. 1.5-2.5 czlowieka na kazdego obronce, wiec obronca moglby wygrac dopiero, gdy oblegajacych
(rownej jakosci) jest mniej niz ok. 1.5-2.5 raza tylu co obroncow [S]. AI szturmuje od 2.8 raza wzwyz [P] - wynik jest przesadzony, zanim bitwa sie zacznie.

**(c) Pomiar [P `bitwy.log` B175 "Siege"]:**

| Bieg | szturmow | zdobyte | bez rozstrzygniecia | wygral obronca | sila A/O (moc B175) zdobyte: min / mediana | bez rozstrzygniecia: min / mediana |
|---|---|---|---|---|---|---|
| fin30 | 73 | 55 | 18 | **0** | 2.81 / 7.48 | 2.56 / 3.42 |
| fin21 | 67 | 38 | 29 | **0** | 2.51 / 6.82 | 2.01 / 3.96 |

Straty w zdobytych szturmach fin30 (B175, z zastrzezeniem z rozdz. 1): oblegajacy 39 261 z 178 747 (22%), obroncy 25 553 z 25 553 (100%) - ok. 1.5 oblegajacego na obronce [P].
Zaloga na starcie oblezenia [P `ludzie-regiony.csv`]: miasta mediana 526 (+ milicja 205), zamki 257 (+191). Krolewska Przystan: 541 + 340 wobec 4484 ludzi Robba Starka [P log:204696].

**(d) "Bez rozstrzygniecia"** (gra: `WinningSide` = brak) - w fin30 kazde z 18 takich oblezen skonczylo sie razem ze szturmem (ta sama albo nastepna doba), drugiego szturmu nie bylo;
2 z 18 (fin21: 9 z 29) mialy po stronie oblegajacych 0 druzyn przy koncu [P]. Kod gry konczy bitwe bez zwyciezcy m.in., gdy jednej stronie zabraknie wodza albo strony nie sa
juz w wojnie (`DiplomaticallyFinished` [K `MapEvent.cs:1002-1005`]). Saltpans `dzien 108879`: szturm i koniec oblezenia o tej samej godzinie 15 [P log:16910, `bitwy.log:2008`], 2 doby pozniej armia odsieczy Edmure'a Tully'ego
(4539) bije oblegajacych [P `bitwy.log:2125`]. Przyczyny w pozostalych przypadkach nie zmierzono - nie zakladam.

**(e) Jak twierdza naprawde sie broni [P]:** wypad (`SallyOut`) - 44 bitwy, 42 wygrane przez wychodzacych; wychodza glownie armie i lordowie, ktorzy weszli do twierdzy
(lord 23, armia 17, sama zaloga 4), liczniejsi od oblegajacych mediana 1.9 raza. Bitwy pod murami (`SiegeOutside`) - 60, w 14 oblezeniach pobity oblegajacy, w pozostalych
pobita odsiecz. W historii twierdzy fin30: 42 oblezenia przerwane wypadem (mediana 6 dob), 14 bitwa pod murami (12-15 dob), 26 okupem (9 dob), 47 odstapione bez bitwy (4-5 dob).

## 7. Glod zalogi

| Miara (pierwsza doba oblezenia) [P `spichlerze-regiony.csv`] | Miasta (40 oblezen) | Zamki (166) |
|---|---|---|
| zapas jedzenia `zywnosc` - mediana (min - max) | 28 571 (19 264 - 45 910) | 8 616 (6 288 - 16 620) |
| dzienny bilans w oblezeniu - mediana (najgorszy) | -97 (-261) | -38 (-89) |
| doby jedzenia = zapas / ubytek - mediana (min) | 274 (75) | 225 (91) |
| doby glodu (`glodna_warownia` = 1) w oblezeniach | **0** | **0** |
| fin21 (dla porownania) | 263 doby (min 148), 0 glodu | 206 dob (min 108), 0 glodu |

Wniosek [P]: zadne oblezenie nie trwalo nawet 1/4 zapasu. Glod jako droga zdobycia nie istnieje, bo nikt nie czeka. Gra liczy glod przy wyborze celu dopiero przy zapasie < 100
[K `DefaultTargetScoreCalculatingModel.cs:227-229`] - u nas nigdy. Zapasy same w sobie sa historycznie rozsadne (zamek na 6-12 miesiecy [H]).
W oblezeniu bilans jedzenia przechodzi z dodatniego na ujemny (Cape Wrath: +55 w dobie przed, -32 w pierwszej dobie oblezenia [P `spichlerze-regiony.csv` ROT_castle18,
108844-108845]); uchodzcy chronia sie w murach i jedza z zapasu (`FamineSiegeShelter`) [K `Famine.cs:460-470`].

## 8. Historia i lore - ile trwaly oblezenia

| Oblezenie | Kiedy | Ile trwalo | Jak sie skonczylo | Znacznik |
|---|---|---|---|---|
| Antiochia | 1097-1098 | ok. 8 miesiecy | zdrada (otwarta wieza) | [H] |
| Jerozolima | 1099 | ok. 5.5 tygodnia | szturm | [H] |
| Akka | 1189-1191 | ok. 2 lata | kapitulacja | [H] |
| Chateau Gaillard | 1203-1204 | ok. 6-7 miesiecy | wyglodzenie, podkopy, szturm | [H] |
| Rochester | 1215 | ok. 7 tygodni | podkop pod wieze, glod | [H] |
| Kenilworth | 1266 | ok. 6 miesiecy | glod i choroby, kapitulacja | [H] |
| Calais | 1346-1347 | ok. 11 miesiecy | glod, kapitulacja | [H] |
| Malbork | 1410 | ok. 2 miesiace | odstapione | [H] |
| Rouen | 1418-1419 | ok. 6 miesiecy | glod, kapitulacja | [H] |
| Orlean | 1428-1429 | ok. 7 miesiecy | odsiecz | [H] |
| Konstantynopol | 1453 | 53 dni | szturm | [H] |
| Harlech | 1461-1468 | ok. 7 lat (blokada z przerwami) | kapitulacja | [H] |
| Storm's End (Rebelia Roberta) | 283 AC | prawie rok | wyglodzony garnizon Stannisa wytrzymal do odsieczy Eddarda Starka (przemyt Davosa) | [L] |
| Riverrun (Blackfish) | 299-300 AC | miesiace; zapasy "na dwa lata" | poddanie wynegocjowane (Jaime, Edmure) | [L] |
| Krolewska Przystan (zlupienie) | 283 AC | jeden dzien | zdrada - Tywin wpuszczony za mury | [L] |
| Czarny Nurt | 299 AC | jeden dzien | szturm odparty (odsiecz Tyrellow) | [L] |
| Harrenhal (Roose Bolton) | 299 AC | - | zdrada najemnikow | [L] |

Wzor [H]: szturm na cale mury byl rzadki i krwawy; twierdze padaly glownie z glodu, przez zdrade, po umowie o poddaniu ("jesli odsiecz nie przyjdzie do dnia X") albo
nie padaly wcale, gdy przyszla odsiecz. Szybkie szturmy - gdy obsada byla mala albo mury zaniedbane. W grze jest odwrotnie [P]: 55 z 57 zdobyc to szturm, 0 z glodu,
0 z umowy o poddaniu; obsada bywa mala (Krolewska Przystan 541 + 340, rozdz. 6).

Porownanie [S]: polowa zdobyc w grze 12 dob; typowe historyczne oblezenie twierdzy 2-7 miesiecy (60-210 dob) = 5-18 razy dluzej. W ulamku roku: gra 0.033, historia 0.15-0.6.

## 9. Dzwignie (rozpoznanie - nic nie zmienione, do decyzji)

{DZWIGNIE}

## 10. Czego nie wiem

{NIEWIEM}

## Aneks A - wszystkie 206 oblezen fin30 [P]

Kolumny: start = doba biegu (108836 = 1); dob = polnoce w oblezeniu (+-1); O0 = wiek oblezenia przy decyzji gry o szturmie; szturm = doba od startu, w ktorej skonczyla sie bitwa
szturmu; W = wypad, M = bitwa pod murami (doba od startu); okup = doba okupu O1. fin21: `robocze\czas\fin21-epizody.csv`.

{ANEKS}
