# Plan gospodarki 21.10 (drugie konto) - diagnoza biegow 100/101 (wf_8383a672-6d7, tylko odczyt)

## Plan poprawek gospodarki: krytyka diagnoz i kolejnosc (21.10, tylko odczyt, gra nieruszana)

**Najwazniejszy wniosek:** autotest "nowa kampania" nie odpowiada grze Jeffa. W kazdym biegu "nowa" kalibracja startu (KS) zostaje w stanie Plan i nie wlacza sie. W grze Jeffa wlacza sie sama. Dlatego przed jakakolwiek zmiana kodu gospodarki potrzebny jest jeden bieg bazowy na drodze Jeffa (P2). Jedna poprawka jest pilna: stan z wlaczona KS ma wlasny, wiekszy problem (P3), a kampania Jeffa wejdzie w ten stan okolo 35. doby albo pozniej.

### A. Co sprawdzilem sam (kod i logi)

**1. Kasy miast. Mechanizm diagnozy 1 sie potwierdza, ale wniosek jest niepelny.**
- W `DsTowns.cs` podwyzka firmy za nadwyzke (SurplusReview), gersuma i "bez obnizki przy nadwyzce" dzialaja tylko w miastach z KS. Warunek `StartCalibration.KsTownApplied` stoi w liniach :527, :586, :718, :819 i :869.
- Log 101-nowa, doba 109015:
  - KS ma "stan Plan, czeka";
  - linia f7-kasy: "miasta z KS 0, od startu sesji: panom 0, koronie 0".
- Wszystkie biegi "nowa" (23-25, 02-12, 04-09, 06-23 prolog, 07-17) koncza w stanie Plan. Wszystkie biegi "160 z zapisu" maja KS zastosowana od dob 109111-109112.
- `Armoury.json` Jeffa: `StartCalibrationAutoArm: true`, `StartCalibrationAutoArmAutotest: false`. W jego kampanii KS uzbraja sie wiec sama, mierzy 28 + 7 dob i wlacza sie z rampa 14 + 14 dob, jesli przejda twarde bramki. Autotest bez pliku `armoury-ks-auto.flag` tej drogi nie przechodzi.
- Stan z KS wyglada odwrotnie niz "nowa": regula nie jest za slaba, tylko za mocna.

| Bieg (stan z KS) | Co widac |
|---|---|
| 96160 | podwyzki firm +88.6 mln zl/rok, M96D-3 zaleglosci 558 tys. |
| 97160 | podwyzki firm +83.4 mln zl/rok, zaleglosci 743 tys.; NAKUM-7: glowy rodow +73.5 tys./d (ponad prog); kasy miast z KS -52 tys./d w ostatnich 28 dobach; NAKUM-27 NIE (miast ponizej zapasu z 12 do 16) |
| 100-160 | firmy miast 231 mln zl/rok (w "nowa" 35.7 mln), dlug miast 1.06 mln, zaleglosci 801 tys., 9 rad niewyplacalnych |

- **Wniosek:** propozycja diagnozy 1 ("regula dla 100% miast") przeniesie na wszystkie miasta mechanizm, ktory pod KS robi zaleglosci (diagnoza 5) i przelewa nadmiar do glow rodow. Kolejnosc musi byc taka: najpierw naprawic sama regule (P3), potem bieg na drodze Jeffa (P2/P3), dopiero potem ewentualne rozszerzenie (P4).
- Brak gornej granicy podwyzki potwierdzony w kodzie:
  - `DsTowns.cs` (SurplusReview): `add = Math.Min(grow * YearDays, mx * F)`, bez sufitu "firma <= doplyw netto x 364". Share jest obciety do 1, ale sama firma nie.
  - Kwalifikacja patrzy na kase brutto (`g <= OverReserve * Reserve`). Odliczenie naleznosci (CofferOwes) ma tylko SurplusHolds.
  - SurplusReview biegnie takze w czasie rampy DS.

**2. Blad SideVillage. Potwierdzony, ale nieszkodliwy dla rozgrywki.**
- CrashScribe 07-17-34, 07:18:26: "ERROR IN A MOD [Armoury] SideVillageVisit.Today", NullReferenceException. Stos: ArmouryBehavior..ctor -> M97WioskiReset -> Reset -> RaidReset -> ProtSync -> Today0 -> Today.
- Przyczyna: w konstruktorze nowej kampanii `Campaign.Current` juz jest, ale zegar mapy jeszcze nie, wiec `CampaignTime.Now` rzuca wyjatek. `Today0` (`SideVillageVisit.Raid.cs:82`) lapie go w `catch { return 0; }`.
- CrashScribe zapisuje go, bo nasluchuje wyjatkow pierwszej szansy (`FirstChanceException` w `SubModuleMain.cs:55`).
- Skutek w grze: zaden. `_prot` jest w tej chwili pusty, migawka i tak wychodzi pusta.
- Skutek w pomiarach i dla gracza: jeden blok ERROR na start kazdej nowej kampanii. Jest w sesjach 20-10_21-49, 21-02-12, 21-04-09, 21-06-23 (prolog) i 21-07-17, czyli od m97-wioski. Psuje progi T1/E8 T0. Jeff zobaczy go w swojej kampanii z prologiem.

**3. Piraci bez jedzenia (MB-2). Potwierdzone.**
- `BandFood.cs:330` `if (want > 0 && !sea)`: rabunek tylko na ladzie.
- `:324`: zakup tylko z kiesa > 0.
- `:169`: `Starving()` daje false na morzu, wiec glodni piraci dalej werbuja.

**Dodatkowo: VR-3 (premia drog Valyrii) to blad pomiaru, nie gry.**
- W biegach "nowa" premia wynosi dokladnie 25.00%.
- W 98-160 (00-07-42) ta sama partia z premia 1.01% wraca w kazdej linii od doby 109144 przez okolo 25 dob. Audyt bierze pierwsze 6 partii z `MobileParty.All`, wiec co dobe ta sama. W 97160 to samo daje 23.35%.
- Hipoteza: partia stoi na podlodze predkosci gry albo pod sufitem kolumny, albo utknela.

### B. Ocena diagnoz: co potwierdzone, co hipoteza

| Diagnoza | Potwierdzone | Hipoteza / uwaga |
|---|---|---|
| 1. Kasy miast | Brak zlota z niczego (ARYTMETYKA OK, BLAD 0 w 96160/97160). Bramka KS w DsTowns. KS w Planie w autotestach "nowa". | Teza "to jest problem gry Jeffa" niepewna: pod KS jest odwrotnie. Rozstrzygnie bieg P2. Skok od WGRANIA 96 (m96-sakiewki) przekonujacy, ale logi 96/97 sa skasowane. |
| 2. MB-2 piraci | Kod potwierdzony. Liczby z logu 101 zgodne z linia m96-bandy. | Brak. |
| 3. Drewno R1-10b | Stocznia bierze drewno bez zapasu budow (`A17Fix.cs:506-507`, ksiegowana jako budowa FBuild). Dymarka zeruje polke (`Bloomery.cs:529`). | Udzialy przyczyn (60-80 cykli itd.) to szacunki z regresji. Prog 177 to jeden pomiar (srednia 11 biegow 203 +-33). |
| 4. W12-4 / W12-6 | Bledy pomiaru, wiarygodne (5.001% i 0.0186). | NC-16b i GL2-6 (Volantis/Braavos) "z KS lepiej" sprawdzone tylko na jednym biegu z zapisu. Qarth (geografia) i A1-T19 (zaulki) wiarygodne. Udzial N2-E3 w A1-T19 nierozdzielony. |
| 5. M96D-3 | P1 potwierdzony w kodzie i w liczbach biegow 160. | P2 (firma ustalona ponad doplyw zaraz po wczytaniu) dotyczy glownie biegu z zapisu. W nowej kampanii do sprawdzenia w P2. |

### C. Plan poprawek (najpierw bledy, potem najwiekszy wplyw; jedna zmiana na jeden test)

**P0. Wyjatek SideVillage przy starcie nowej kampanii (tor S, BLAD, potwierdzony)**
- Co: w `ProtSync` czytac zegar tylko przy niepustym `_prot` (`if (_prot.Count > 0) { int today = Today0(); ... }`). RaidReset czysci `_prot` przed ProtSync, wiec przy resecie zegar nie bedzie czytany.
- Gdzie: `Armoury/src/SideVillageVisit.Raid.cs` (ProtSync, okolo :75-80).
- Klucz MCM: brak (czysta poprawka bledu, zachowanie bez zmian).
- Efekt: 0 blokow ERROR w CrashScribe na starcie nowej kampanii; T1/E8 T0 TAK.
- Ryzyko: minimalne. DefReset i DefPrune przy pustych slownikach nie czytaja czasu (sprawdzone).
- Kolizje: brak.

**P1. Narzedzie progow: same bledy pomiaru (tor S, bez zmian w grze)**
- Gdzie: `tools/sprawdz_logi.py`.
- W12-4: doliczyc "dziesiecina N" z linii "Daniny wsi (DS)". Oczekiwane 5.00% TAK.
- W12-6: przy "P3: oferta W 1/60" prog = 1/FolkSurplusSpendDays. Oczekiwane 0.0186 TAK.
- GL2-8: prog zgodny z regulami (zapas wazony W10/R21/P30, okolo 12-14 dni, plus GL2-8b >= 95%). Oczekiwane 27-32 dni TAK.
- VR-3: audyt w progu bez probek zablokowanych, a te osobno jako INFO z liczba partii.
- W grze tylko napisy: `TownFolk.Live.cs:1930` i `:1991` ("tempo W 1/60 (P3), place R/P 0.15"); `MapRoadKinds.Audit` (`:327-357`) dopisuje id partii i powod przy premii < 5%, a start listy przesuwa sie z doba.
- Ryzyko: GL2-8 to zmiana progu. Trzeba to powiedziec Jeffowi wprost. Stan z KS (14-19 dni, koszyk 82-94%) zostaje jako osobny temat.
- Kolizje: `sprawdz_logi.py` zmieniaja tez P4 (NAKUM-26/27), P6 (MB-0/MB-13) i P7 (prog R1-10b). Kazda zmiana narzedzia w osobnym commicie, kolejno (pulapki scal.sh: dispatch w cudzym if, te same nazwy grup).
- Sprawdzenie: ponowne uruchomienie na istniejacych logach (07-17-36 i 06-23-39), bez nowego biegu.

**P2. Bieg bazowy na drodze Jeffa (bez zmiany kodu, warunek dla P3-P5 i P10)**
- Co: WGRANIE 101, nowa kampania 180 dob z `armoury-ks-auto.flag` (albo `StartCalibrationAutoArmAutotest`). Zmiana tylko w skrypcie autotestu w scratchpadzie.
- Rozstrzyga: czy w nowej kampanii KS sie wlacza (okolo d35-64) i czy przechodzi bramki.
  - Jesli TAK: grozna jest pila pod KS (P3 pilne), a P4 dotyczy tylko okna przed KS i krain wroconych.
  - Jesli NIE (KS zostaje w Planie): diagnoza 1 trafia w gre Jeffa i P4 staje sie glowne.
- Przy okazji: NC-16b i GL2-6 (Volantis/Braavos) z KS. Diagnoza 4 przewiduje chleb okolo 1.6.
- Od tej pory biegi "nowa" robic z flaga, zeby test odpowiadal grze Jeffa.

**P3. Pila firm pod KS: M96D-3 i nadmiar u glow rodow (tor M, najwiekszy wplyw na gre Jeffa, mechanizm potwierdzony)**
- Gdzie: `Armoury/src/DsTowns.cs` (SurplusReview, Fix).
- Co:
  - (a) sufit podwyzki: firma po podwyzce <= NetAvg x 364 (ta sama miara co Share <= 1), czyli z biezacego zysku, bez nowego mnoznika;
  - (b) kwalifikacja do podwyzki z kasy po odjeciu naleznosci (CofferOwes, jak w SurplusHolds);
  - (c) okno pomiaru przyrostu nie liczy dob rampy DS (NewShare < 1), pomiar od konca rampy;
  - (d) w Fix firma <= doplyw netto okna (Share <= 1).
- Klucz MCM: `KsFarmSurplusCapNet` (TAK; NIE = kod 101). Po dodaniu `gen_mcm.py`.
- Efekt (bieg 160 z zapisu i P2):
  - M96D-3 z 558-873 tys. do <= 105 tys.;
  - NAKUM-7 "glowy rodow" < +30 tys./d (97160: +73.5);
  - NAKUM-27 <= 14;
  - linia "Dlugi miast": niewyplacalnych 0-2;
  - firmy miast ponizej 231 mln zl/rok.
- Ryzyko: wolniejsze zdejmowanie nadwyzki, wiec kasy pod KS moga znow rosnac (96160: kasy +40.5 tys./d). Sprawdzic NAKUM-7 kasy i NAKUM-26 w tym samym biegu. Gersuma f7 (CatchUpDecide) zostaje ujsciem zapasu.
- Kolizje:
  - P4 zmienia te sama funkcje i stan (SG0, SGDay, SMin, Share, CLeft), wiec tylko po P3 i w tej samej galezi;
  - m96-dlugi (`TownDebt.NoteTerm`/`ArrearsLike`) czyta zaleglosci, wiec progi m96dlugi do porownania;
  - KsFolkIncome (NoteRaise, NoteShareUp) to tylko liczniki.

**P4. Rozszerzenie reguly na miasta bez KS (diagnoza 1, tor M; robic tylko wedlug wyniku P2 i tylko na poprawionej regule z P3)**
- Co: `SurplusTown(f,t) = KsTownApplied(t) || (s.DsFarmSurplusAllTowns && f.Fixed >= 0)` w miejscach :527, :586, :718, :819 i :869.
- W miastach bez KS zastepczy warunek "domy najpierw" (R albo P ponizej zapasu dni), bo `KsFolkWage.Active` daje tam zawsze false.
- Gdzie: `DsTowns.cs`, `KsFolkIncome.cs`, `Settings.cs`/`McmSettings.cs`, `sprawdz_logi.py` (NAKUM-26/27 "miasta z regula").
- Klucz MCM: `DsFarmSurplusAllTowns` (TAK).
- Efekt w "nowa" bez KS albo przed KS: NAKUM-7 kasy z +52.8 do okolo +5..28 tys./d; NAKUM-1a < 30 tys./d; G1-5a <= 110% Z33; pompa C > 126 tys./d; "5% zaworu" okolo 50-80 tys./d.
- Ryzyko:
  - powrot zlota przez zold (model f7: +28 tys./d przy zwrocie 0.5, blisko progu);
  - glowy rodow ponad 30 tys./d;
  - bez P3 przyszlaby ta sama pila, co pod KS.
- Kolizje: P3 (ta sama funkcja), P5, P8 (zaulki zaleza od kas).

**P5. Podatek 33% od nadwyzki ludu do pana i korony zamiast do kasy osady (tor S, tylko gdy po P3/P4 kasy > +30 tys./d)**
- Gdzie: `TownFolk.Hoard.cs:146-160`.
- Klucz MCM: `FolkHoardTaxToFarm`.
- Efekt: petla kasa -> lud -> podatek -> kasa slabnie (dzis +79.5 tys./d).
- Ryzyko: wiecej u glow rodow (ten sam prog co w P3).
- Kolizje: P3 i P4 (wspolne progi nakum).

**P6. Piraci bez jedzenia, MB-2 (tor M, luka w regule obejmujaca 42% potrzeby band, potwierdzona)**
- Co:
  - (a) zakup u pasera najblizszego miasta z portem (HomeSettlement albo HasPort);
  - (b) polow zalogi na morzu, tylko brakujaca czesc dzisiejszej porcji, zjadany od razu, najwyzej 24 szt./d (miara NavalDLC). Ksiegowac jako nazwane zrodlo `FFish` z etykieta "polow zalogi bandy|", NIE w GFish/KsFishBoats (`A17Fix.cs:229` liczy te grupe jako podaz KS);
  - (c) rabunek wsi portowych z morza (`v.Bound.HasPort`) jako OSOBNY drugi krok z osobnym kluczem, po sprawdzeniu progow zywnosci.
- Proponuje odwrocic kolejnosc diagnozy: polow przed rabunkiem. Polow obejmuje 100% band bez ruszania zapasow wsi, a NC-16b i tak stoi na granicy.
- Gdzie: `BandFood.cs`, `Settings.cs`/`McmSettings.cs`, `sprawdz_logi.py` (nowy odcinek przed "| potkniecia" i MB-13).
- Klucze MCM: `BandFoodAtSea` (TAK), `BandSeaRaidCoast` (domyslnie decyzja po tescie).
- Efekt: MB-2 z 0.575 do okolo 0.97-0.99; MB-12 (glodne na morzu) z 61.9/d do okolo 0; MB-3 okolo 0; morale z glodu okolo 0.
- Ryzyko:
  - ryby nie moga trafic do zapasu ani do pasera (inaczej zloto z niczego);
  - bramka KS (3) "zrodla bez nazwy" bez przyrostu;
  - najedzeni piraci walcza czesciej (K1, lup).
- Kolizje: GoodsLedger i KS (nazwy zrodel), M96BANDY_RX w narzedziu. Poza tym niezalezne od pieniedzy, wiec mozna przygotowac rownolegle, ale testowac osobno.

**P7. Drewno, R1-10b (tor S, kroki osobno)**
- P7a (pierwszy, to niespojnosc reguly): stocznia jako budowa zostawia zapas K1. `A17Fix.cs:506`: `hw = max(0, Count - BuildFunding.WoodHold(...))`. Klucz `ShipyardWoodAboveCraftStock`.
- P7b: dymarka zostawia kuzni jedna dobe zuzycia miasta (`Bloomery.cs:529`). Klucz `BloomeryShelfLeavesForges`.
- P7c (tor L, tylko gdy po P7a i P7b R1-7 > 5% albo T2-K1-1 > 100): wywoz przed budowami.
- Efekt:
  - R1-10b okolo 120-160 (dzis 222);
  - T2-K1-2 okolo 9-11;
  - NA17-5b w dol;
  - R1-7 moze wzrosnac do 8-9% (wymiana do powiedzenia Jeffowi).
- Nowy prog R1-10b ze sredniej co najmniej 2 biegow (`CHANGELOG.md:16260`).
- Ryzyko: mniej statkow AI w d1-120 (linia "Statki (181)").
- Kolizje: P7c i P9 zmieniaja `MaterialOrders.cs`, wiec nigdy razem.

**P8. Towar wsi niesprzedany, A1-T19 (tor M, po P3/P4, bo pierwotna przyczyna to puste kasy "zaulkow")**
- Co: rezerwacja zlota w `MarketCarts.cs` (`Value`, okolo :215; zdjecie przy kazdym koncu kursu) i N2-E3 (:494-501) tylko przy pelnej zaplacie.
- Klucz MCM: `VillageCartGoldAware`.
- Efekt: A1-T19 < 0.1%.
- Ryzyko: rezerwy, ktore sie nie zdejmuja, zaczna omijac zdrowe miasta.

**P9. Jedzenie dla Qarth, GL2-6 (tor L, po decyzji w P7c)**
- Co: kontrakty 174 takze na jedzenie (`MaterialOrders.cs:49`), wyzwalacz < 7 dob na polce, zrodlo > 14 dob, droga morska.
- Klucz MCM: `TownFoodOrders`.
- Efekt: Qarth >= 7 dob.
- Ryzyko: ceny jedzenia w zrodlach (NC-16b, GL2-4).

**P10. Chleb, NC-16b (po P2)**
- Jesli z KS chleb <= 3.0, bez zmiany gry.
- W przeciwnym razie przezbrajanie warsztatow takze dla zysku (`WorkshopRetool.cs:147`, klucz `WorkshopRetoolForGain`). Kolizja z P7 (ta sama pula warsztatow).

**Bez poprawki, do obserwacji:**
- C-4c i A16-5b: wartosci jak przed 96, roznica przez dlugosc biegu i warunek progu;
- G1-5c: przyczyna to liczba karawan (482 wobec 962), nie zloto, wiec to osobny temat;
- VR-3 w grze: tylko gdy P1 wskaze partie, ktora naprawde utknela.

### D. Kolizje zbiorczo i kolejnosc

1. Kolejnosc: P0 -> P1 -> P2 (bieg bazowy) -> P3 -> (P4 wedlug P2) -> P5 (warunkowo) -> P6 -> P7a -> P7b -> P8 -> (P7c albo P9, nigdy razem) -> P10 (warunkowo).
2. Wspolne pliki:

| Plik | Kto go zmienia | Jak |
|---|---|---|
| `DsTowns.cs` | P3, P4 | jedna galaz, po kolei |
| `MaterialOrders.cs` | P7c, P9 | wylacznie |
| `sprawdz_logi.py` | P1, P4, P6, P7 | osobne commity, kolejno |
| `Settings.cs`/`McmSettings.cs` | wszystkie z kluczem | `gen_mcm` raz na scalenie |
| GoodsLedger i KS | P6 | nazwy zrodel |

3. Kazda zmiana gospodarki: autotest "nowa" z flaga KS 180 dob oraz "160 z zapisu" na regresje (miasta z KS). Wgranie dopiero po udanym tescie i nie wtedy, gdy Jeff gra.

### E. Podsumowanie dla gracza

1. Zloto w swiecie sie zgadza: nic nie powstaje z niczego i nic nie liczy sie dwa razy.
2. Kupcy w miastach odkladaja jednak za duzo, bo zasada "nadwyzka kasy idzie panu i koronie" dziala dopiero po wlaczeniu kalibracji startu.
3. W Twojej kampanii kalibracja wlaczy sie sama po okolo 5 tygodniach.
4. Wtedy jest odwrotny klopot: miasta dostaja za szybko wyzsze oplaty, popadaja w zaleglosci i dlugi, a zloto zbiera sie u glow rodow.
5. Dlatego najpierw naprawiam samo tempo tej zasady: miasto nie zaplaci wiecej, niz naprawde zarabia. Sprawdze to testem, ktory przechodzi te sama droge co Twoja gra.
6. Piraci na morzu nie maja dzis skad wziac jedzenia. Dostana polow wlasnej zalogi, a pustoszenie wybrzezy bedzie osobna decyzja.
7. Stocznie i dymarki przestana wybierac kowalom cale drewno z polek.
8. Usune tez nieszkodliwy komunikat bledu, ktory pojawia sie przy starcie kazdej nowej kampanii. Rozgrywki nie zmienial.

---

# Zalacznik: diagnozy szczegolowe (6 tematow)

## MB-2: bandy jedza 57-66% potrzeby (m96-bandy, BandFood)

**cause:**

Jedna przyczyna odpowiada za 99% luki: piraci NavalDLC (southern_pirates, northern_pirates). Caly czas sa na morzu i w obecnym kodzie nie maja zadnego zrodla jedzenia. Bandy na ladzie jedza ok. 100% potrzeby.

DOWOD Z LOGU (bieg 101-nowa, Armoury-2026-10-21_07-17-36.log, 180 dob; policzone z linii "Bandy - jedzenie i kiesy (m96-bandy)" i "Paser ... bandy wedlug klanu"):
- Razem: potrzeba 68988, zjedzone 39700, czyli 0.575. Brakuje 29288 szt.
- Potrzeba samych piratow (ludzie/20, bez jencow) to 29076 szt., 42.1% calej potrzeby. Brak u piratow = 99.3% luki.
- Lad bez piratow: potrzeba 39912, zjedzone 39700, czyli 0.995.
- Bandy na morzu: srednio 62.6/d, piratow 62.8/d. Glodne na morzu 61.9/d, wszystkie "bez jedzenia". Glodne razem 62.1/d, wiec glodnych na ladzie jest 0.2/d. Zakupow z morza 0.2 bandy/d.
- Doba 180: potrzeba 586.8, zjedzone 368, brak 218.8. Piraci w tej dobie: 82 bandy, 4621 ludzi (southern 48/2584, northern 34/2037), czyli ok. 231 szt./d. Glodne na morzu: 79 z 82.
- To samo w pozostalych biegach:
  - 100-nowa: 0.578, piraci 42.4% potrzeby, lad 1.003.
  - 101-prolog: 0.580, piraci 41.7%, lad 0.995.
  - 100-160 z zapisu: 0.678, piraci 36.6%, na morzu najedzonych 16.6 z 114 band/d. Tylko tam piraci mieli kiesy z zapisu (zakupy z morza 13.5 bandy/d) - stad lepszy wynik.

KOD (dlaczego pirat nie dostaje jedzenia):
1. BandFood.cs:330 `if (want > 0 && !sea)` - rabunek tylko na ladzie.
2. BandFood.cs:324 - zakup u pasera tylko z kiesa > 0 (`p.PartyTradeGold > 0`). Piraci startuja z pusta kiesa: OutlawLaw.StartGoldPostfix (OutlawLaw.cs:1673-1695) daje 0 bandom bez kryjowki. Sam NavalDLC dawal 10 zl na czlowieka (PiratesCampaignBehavior.PirateStartGoldPerBandit).
3. Kieszonkowe z kryjowki (StashDay) dostaja tylko bandy z kryjowka, piraci nie maja kryjowki.
4. Jedzenie na start z NavalDLC jest zablokowane: OutlawLaw.FoodPrefix (OutlawLaw.cs:1713-1724), latka na PiratesCampaignBehavior.GiveFoodToBanditParty (OutlawLaw.cs:2929).
5. Piraci nigdy nie schodza na lad ani nie wplywaja do portu. W NavalDLC PiratesCampaignBehavior.AiHourlyTick kaze im krazyc wokol strefy patrolu na morzu, z waga 5. Lup z bitew daje im malo: paser ma 4 bandy z ladunkiem na dobe, dla wszystkich klanow razem.

CO POGARSZA SPRAWE: na morzu glod nie zmniejsza bandy. BandFood.cs:169 - Starving() zwraca false na morzu, wiec glodni piraci dalej werbuja. BandFood.cs:362 - na morzu nikt nie odchodzi. Liczba piratow rosnie bez jedzenia: z 2729 do 4621 ludzi (+69%).

RESZTA LUKI (0.7%, 212 szt.): rozruch w dobie 1. W BandFood.cs:337 `units=(int)owed` - bandy ponizej 20 ludzi pierwszego dnia nie maja pelnej sztuki do zjedzenia (doba 1: potrzeba 277.4, zjedzone 16; ok. 125 szt.). Pozostale ok. 87 szt. to ulamki, ktore przepadaja, gdy banda znika z mapy. Nie wymaga poprawki.

**confidence:**

wysoka

**fix:**

Proponuje nowy wylacznik MCM "Bands Forage At Sea" (pole BandFoodAtSea = true w Settings.cs i McmSettings.cs, potem tools/gen_mcm.py). Po wylaczeniu wszystko dziala jak dotad. Zmiany w BandFood.Band, w kolejnosci dnia bandy:

1. ZAKUP (bez zmian w zasadzie). Banda na morzu kupuje u pasera najblizszego miasta Z PORTEM (HasPort; najlepiej p.HomeSettlement, bo NavalDLC daje piratom port jako dom), a nie najblizszego miasta w ogole. Zasada jest ta sama: wlasna kiesa, towar z polki (ShelfBuy.Take), zaplata do kasy miasta.

2. RABUNEK Z MORZA. W BandFood.cs:330 zamiast `!sea` dopuscic morze. Do Rob() przekazac `sea`. Na morzu brac tylko wsie, ktore gra pozwala rabowac z morza: `v.Bound != null && v.Bound.HasPort` (ta sama regula co NavalDLC raiderHasSeaPresence; w kodzie juz w ReaverLaw.cs:275/520 i DistrictRaid.cs:548). Te same limity co na ladzie: RobRadius, RobVillageShare, MaxRobVillages i stan Normal. Towar przechodzi z wsi do jukow, nic nie powstaje z niczego. Wies gracza dostaje ten sam komunikat co przy rabunku z ladu. Tak wygladalo piractwo w sredniowieczu: pustoszenie wybrzezy.

3. POLOW ZALOGI (zeby objac 100% band). Jesli banda na morzu po krokach 1-2 nadal nie ma dzisiejszej porcji, zaloga lowi tylko brakujaca czesc dzisiejszego jedzenia. Ryby sa zjadane od razu: nic nie idzie do zapasu ani do pasera, wiec nie zamieniaja sie w zloto. Gorny limit to miara gry: polow zalogi NavalDLC, 1 ryba na godzine na morzu (NavalFishingCampaignBehaviour.OnHourlyTickParty), czyli do 24 szt. na dobe. Pirat potrzebuje ok. 2.8 szt. na dobe, wiec nie jest to nowy mnoznik. Polow ksiegowac w ramce GoodsLedger.FFish z wlasna etykieta, np. "polow zalogi bandy|", tak jak A17Fix.CrewPre/FishFrame. Etykieta nie moze trafic do GFish (KsFishBoats), bo te grupe StartCalibration.Plan liczy jako podaz (A17Fix.cs:229). Zjedzenie dalej w ramce FFood w Eat().

4. Do linii doby dopisac nowy odcinek tuz przed "| potkniecia", np. "z morza: wsie portu X szt. (band, wsi), polow zalogi Y szt. (band)". W tym miejscu regex sprawdzarki ma juz `.*?`, wiec format MB-0 sie nie zepsuje. Do tools/sprawdz_logi.py dodac odczyt nowego odcinka i MB-13 (glodne na morzu).

OCZEKIWANE PROGI (bieg nowej kampanii 180 dob):
- MB-2: z 0.575 do ok. 0.97-0.99 (lad 0.995 plus ok. 29 tys. szt. potrzeby piratow pokryte).
- MB-12 glodne na morzu: z 61.9/d (0.989) do ok. 0.
- MB-3: z 0.135 do ok. 0.
- Morale z glodu: z ok. -310/d do ok. 0.

**touches:**

- Armoury/src/BandFood.cs
- Armoury/src/Settings.cs
- Armoury/src/McmSettings.cs
- tools/sprawdz_logi.py
- CHANGELOG.md

**risks:**

- Rabunek z morza dochodzi do wsi portowych przy strefach patrolu piratow: do ok. 230 szt./d przy dzisiejszych 180/d z wszystkich wsi. Moze obnizyc zapasy tych wsi i podazy KS i pogorszyc NC-16b (chleb juz 3.12 przy progu 3.0). Jesli progi zywnosci sie pogorsza, odwrocic kolejnosc: polow przed rabunkiem.
- Polow to zrodlo towaru. Musi pokrywac tylko dzisiejszy brak i byc zjadany od razu. Jesli ryby trafia do zapasu, paser je skupi i powstanie zloto z niczego. Ksiegowanie w FFish z wlasna etykieta, nie w GFish/KsFishBoats, bo StartCalibration.Plan liczy te grupe jako podaz.
- Najedzeni piraci straca dzisiejsza kare morale (ok. -5 na bande na dobe). Moga czesciej bic sie na morzu, wiec moga sie przesunac straty karawan i lordow na morzu (K1, lup, MB-6).
- Zmiana formatu linii doby. Nowy odcinek dopisac tylko przed '| potkniecia' i rownoczesnie poprawic M96BANDY_RX w tools/sprawdz_logi.py, inaczej MB-0 odrzuci linie.
- Odleglosc do wsi liczona w linii prostej z morza. Banda moze 'zrabowac' wies za cyplem - to ta sama abstrakcja co na ladzie (rabunek bez podrozy), do zaakceptowania.
- Nie mam pozycji stref patrolu NavalDLC, wiec nie wiem, jaki udzial piratow ma wies portowa w promieniu 100. Udzial rabunku i polowu pokaze dopiero nowy licznik w autotescie; polow i tak domyka do 100%.

**verify:**

Autotest nowej kampanii 180 dob (jak bieg 101-nowa), potem `python -I tools/sprawdz_logi.py --grupa m96bandy <Armoury-*.log>`:
- MB-2 >= 0.85, oczekiwane ok. 0.97-0.99 (bylo 0.575).
- MB-12: glodne na morzu ok. 0 (bylo 61.9/d, 0.989).
- MB-3: ok. 0.
- MB-0: format bez nierozpoznanych linii.
- MB-7: nowe pozycje "z morza: wsie portu" i "polow zalogi"; razem powinny dac ok. 150-230 szt./d (potrzeba piratow).

W linii "Bandy - jedzenie i kiesy (m96-bandy)" sprawdzic:
- "glodne N (... na morzu A z B)": A ma spasc do ok. 0.
- "morale z glodu": ok. 0.
- "potkniecia 0".

Kontrola obiegu i skutkow ubocznych:
- Ksiega towarow: polow zalogi bandy ma byc nazwanym zrodlem z wlasna etykieta. Bramka "zrodla bez nazwy <= 2 szt./d" (ZC1-6) bez zmian. GFish/KsFishBoats bez przyrostu.
- Linia Paser: "zywnosc" sprzedana z band nie rosnie przez polow. Ryby nie moga trafiac do pasera.
- Wsie portowe: progi zywnosci wsi i spichlerzy (nspichlerze, nglod, njedz) oraz NC-16b chleb (juz 3.12 przy progu 3.0) porownac z 101nowa. Rabunek z morza dokladnie do 230 szt./d przy dzisiejszych 180/d.
- Krotki bieg z wylaczonym kluczem musi dac MB-2 ok. 0.575, jak dotad (dowod, ze wylacznik dziala).

## CrashScribe: "ERROR IN A MOD" [Armoury] Armoury.SideVillageVisit.Today (NullReferenceException) przy starcie nowej kampanii - progi T1 (etap4) i E8 T0 (etap8)

**cause:**

JEDNA PRZYCZYNA, 100% blokow: przy NOWEJ kampanii zegar kampanii jest jeszcze pusty, a kod pyta o date, chociaz lista ochron jest pusta.

1) Pelny stos (session-2026-10-21_07-17-34.log, linie 800-825, godz. 07:18:26, 52 s po starcie sesji, jeszcze przed liniami Mends 07:18:38). TYPE System.NullReferenceException, WHERE [Armoury] Armoury.SideVillageVisit.Today. Lancuch wywolan (od gory): Today_Patch1 <- Today0_Patch1 <- ProtSync_Patch1 <- RaidReset_Patch1 <- Reset_Patch1 <- ArmouryBehavior.M97WioskiReset_Patch1 <- ArmouryBehavior..ctor <- SubModuleMain.OnGameStart <- MBGameManager.OnGameStart <- Campaign.OnInitialize <- Campaign.DoLoadingForGameType. Blok jest 1 raz, powtorek 0. Linia BLAME "no mod in the trace" jest bledna: w sladzie jest tylko kopia Harmony bez typu.

2) Kod (n30-calosc = WGRANIE 101):
- ArmouryBehavior.cs:726-727: inicjalizator pola `_m97WioskiReset = M97WioskiReset()` wola SideVillageVisit.Reset() w konstruktorze ArmouryBehavior, czyli w OnGameStart.
- SideVillageVisit.cs:78-85: Reset() wola DayZero(), a potem RaidReset() (linia 85).
- SideVillageVisit.Raid.cs:72: `RaidReset() { ...; _prot.Clear(); ...; ProtSync(); ... }`. ProtSync biegnie zaraz po wyczyszczeniu listy, wiec lista jest zawsze pusta.
- SideVillageVisit.Raid.cs:75-80: ProtSync() bez zadnego warunku wola `int today = Today0();` (linia 77).
- SideVillageVisit.Raid.cs:82: `Today0() { try { return Campaign.Current != null ? Today() : 0; } catch { return 0; } }`. Sprawdza tylko Campaign.Current, a to nie wystarcza.
- SideVillageVisit.cs:102: `Today() { return (int)CampaignTime.Now.ToDays; }`.

3) Gra (zdekompilowany TaleWorlds.CampaignSystem.dll):
- `CampaignTime.Now => Campaign.Current.MapTimeTracker.Now`.
- W Campaign.OnInitialize: `GameManager.OnGameStart(...)` (linia 1391 dekompilacji) biegnie PRZED `MapTimeTracker = new MapTimeTracker(...)` (linia 1402, tylko gdy kampania nie jest wczytana).
- Campaign.Current jest ustawiony juz w SetLoadingParameters (linia 1353), dlatego warunek w Today0 przepuszcza wywolanie Today() i leci NRE.
- Przy wczytaniu zapisu MapTimeTracker przychodzi z zapisu, wiec bledu nie ma.

4) Dlaczego blok jest mimo try/catch w Today0: CrashScribe zaklada finalizator na kazda metode Armoury (CrashScribe/src/Net.cs:31-44, ModFinalizer). Finalizator na Today() pierwszy lapie wyjatek, zapisuje "ERROR IN A MOD" i go polyka (SwallowModErrors=true, Config.cs:26), a Today zwraca 0. Catch w Today0 w ogole nie dochodzi do glosu. Do tego ShowInGame=true (Config.cs:28), wiec na starcie nowej kampanii gracz widzi czerwona notke "CrashScribe: error in Armoury (NullReferenceException)".

5) Skutek w grze: ZADEN. Today0 daje 0, wiec ProtSync nie usuwa wpisow (warunek today>0), migawka ochron = NoProt (lista i tak pusta). DefReset() i _sweepPending=true wykonuja sie normalnie. Liczby gry bez zmian, zostaje falszywy czerwony prog i notka na ekranie.

6) Od kiedy: od commita c578f4e7 (20.10 17:49, "m97-wioski: poprawki po recenzjach ... ochrona okupu w lancuchu"). Ten commit dopisal ProtSync() do RaidReset i dodal Today0. Kod jest identyczny w commitach WGRANIA 97 (66443eac), 98 (7f155112), 99 (510f6652), 100 (a2a5147e) i 101 (ed9273fa).

7) Powtarzalnosc (CrashScribe + progi T1 w progi/*/etap4.txt):
- Nowe kampanie, po 1 bloku "Armoury.SideVillageVisit.Today x1": 97nowa (session-2026-10-20_20-17-27), 97bnowa (20_21-49-11), 99nowa (21_02-12-48), 100nowa (21_04-09-28), 101-prolog (21_06-23-37), 101nowa (21_07-17-34).
- 96nowa (20_17-49, przed c578f4e7): 0 blokow.
- Wszystkie biegi z zapisu autotest-F-d240: 0 blokow. Sa to 97160 (20_20-59), 20_22-29, 21_00-07, 21_02-56, 100-160 (21_04-57-24) oraz krotkie 02-06, 04-08, 05-41 i 07-11.
- Wzor: kazda nowa kampania z kodem m97 daje blok, kazde wczytanie zapisu nie daje.
- WYJATEK: 98nowa (20_23-25-06, nowa kampania, ten sam kod): 0 blokow i zadnej wzmianki SideVillage. Nie mam na to dowodu. Hipoteza: kolejnosc zakladania lat Harmony nie jest stala, wiec JIT mogl wkleic Today w kopie Today0 z pominieciem finalizatora. Wtedy wyjatek zlapal catch w Today0, a CrashScribe odsial go jako "bez moda". Na przyczyne to nie wplywa.

8) Uspiona druga droga (0 wystapien w logach, bo autotest to zawsze swiezy proces): Reset() najpierw wola DayZero() (SideVillageVisit.cs:84), dalej RaidDayZero, DefDayZero (Defenders.cs:80) i DefPrune (Defenders.cs:142). DefPrune przechodzi po _lack i _fallen, a dla kazdego wpisu Fallen (:100) i LackNow (:120) wolaja Today0(). Dzieje sie to zanim RaidReset wyczysci te slowniki. Jesli w tym samym procesie gracz wczyta zapis, wroci do menu i zacznie nowa kampanie, a slowniki maja wpisy z poprzedniej gry, poleci ten sam NRE w Today.

**confidence:**

wysoka

**fix:**

Bez nowego klucza MCM: to poprawka bledu, nie zmiana zachowania (liczby gry bez zmian). Obie zmiany sa w Armoury/src/SideVillageVisit.Raid.cs.

(a) ProtSync (Raid.cs:75-80): o dzien pytac tylko wtedy, gdy jest co wygaszac:
```
private static void ProtSync()
{
    if (_prot.Count > 0) { int today = Today0(); if (today > 0) _prot.RemoveAll(p => p == null || p.Until <= today); }
    System.Threading.Volatile.Write(ref _protSnap, _prot.Count == 0 ? NoProt : _prot.ToArray());
}
```
Po RaidReset lista jest zawsze pusta, wiec w konstruktorze ArmouryBehavior (nowa gra i wczytanie) zegar nie jest juz ruszany. To usuwa jedyna droge, ktora wystapila w logach.

(b) Today0 (Raid.cs:82): sprawdzac gotowosc zegara bez rzucania wyjatku, wedlug konwencji repo `Live` (np. A17Fix.cs:77, ArtisanInputs.cs:57, BeatenPurse.cs:73):
```
private static int Today0() { try { var c = Campaign.Current; return c != null && c.GameStarted ? Today() : 0; } catch { return 0; } }
```
- Przy wczytaniu GameStarted=true juz w SetLoadingParameters (Campaign.cs:1355-1357), a zegar jest z zapisu.
- Przy nowej grze GameStarted zmienia sie na true dopiero w pierwszym RealTick (Campaign.cs:894-896), juz po utworzeniu MapTimeTracker (1402).
- W oknie tworzenia swiata Today0 daje 0 ("dzien nieznany"), tak jak dzis po wyjatku. Ochrony, polegli i brak rak sa wtedy puste, wiec wynik sie nie zmienia.
- (b) zamyka tez uspiona droge DayZero, DefPrune, Fallen/LackNow przy zmianie kampanii w jednym procesie.
- Wyjatek nie powstaje wcale, wiec finalizator CrashScribe nie ma czego zapisac. Znika tez czerwona notka na ekranie.

Oczekiwany efekt w progach:
- etap4 "T1: bledy [Armoury] polkniete przez CrashScribe": NIE (bloki 1, Armoury.SideVillageVisit.Today x1) zmienia sie na TAK (bloki 0, powtorki 0).
- etap8 "E8 T0: bloki ERROR IN A MOD [Armoury] (CS)": NIE (bloki 1) zmienia sie na TAK (0 i 0).
- m97wioski WP-1/WP-2 i m97obroncy OB-1: dalej TAK, 0.
- Progi ekonomii bez zmian, bo zmieniona jest tylko sciezka przy pustych listach.

**touches:**

- Armoury/src/SideVillageVisit.Raid.cs
- CHANGELOG.md

**risks:**

- Today0 z warunkiem GameStarted zwraca 0 przez cale tworzenie nowej kampanii (od OnInitialize do pierwszego taktu). Gdyby kiedys ktos wolal Today0 w OnNewGameCreated i potrzebowal prawdziwej daty, dostanie 0. Semantyka 0 = 'dzien nieznany' jest juz opisana w ProtSync, Fallen i LackNow.
- Anomalia 98nowa (nowa kampania bez bloku przy tym samym kodzie) zostaje niewyjasniona. Mozliwe, ze siec CrashScribe nie zawsze lapie wyjatki w malych metodach (kolejnosc lat i JIT). Poprawka usuwa sam wyjatek, wiec nie zalezy od tego, ale taka sama przyczyna moze kiedys schowac inny blad.
- Ten sam wzorzec gdzie indziej: pomocnik `Today()` z CampaignTime.Now wolany w `try { ... } catch` innej metody Armoury i tak da blok 'ERROR IN A MOD', bo finalizator CrashScribe na pomocniku lapie wyjatek przed catch wolajacego. Przyklad: SideVillageFief.cs:110 `Today()` bez warunku. Dzis nie jest wolany z resetu w konstruktorze (SideVillageFief.Reset, linie 86-98, nie rusza zegara), ale warto o tym pamietac przy recenzjach.
- Pola statyczne SideVillageVisit przezywaja zmiane kampanii w jednym procesie. Sama poprawka (a) nie zamyka drogi DayZero, DefPrune, Fallen/LackNow, ktora biegnie przed RaidReset. Potrzebne jest tez (b), albo przestawienie RaidReset przed DayZero w Reset().
- Zmiana jest w pliku m97-wioski, scalonym juz w n30-calosc. Przy scalaniu z galeziami, ktore ruszaja SideVillageVisit.Raid.cs (np. dalsze prace nad okupem), latka moze trafic w cudzy if (pulapka scal.sh), wiec po scaleniu trzeba sprawdzic diff ProtSync i Today0.

**verify:**

1) Build Armoury: kod wyjscia 0, ostrzezenia jak dotad.
2) Autotest NOWA kampania (wystarczy krotki bieg, np. 30 dob, bo blad jest przy starcie; mozna w ramach zwyklego biegu 180 dob). W session-*.log CrashScribe `grep -c "SideVillageVisit"` = 0 oraz `grep -c "^# ERROR IN A MOD"` = 0 (dzis 1 blok ok. 50 s po starcie, w lancuchu ArmouryBehavior..ctor <- OnGameStart <- Campaign.OnInitialize).
3) Progi na logu Armoury tego biegu:
- `python -I tools/sprawdz_logi.py --grupa etap4 <log>`: linia "T1: bledy [Armoury] polkniete przez CrashScribe (log sesji)" = TAK, "bloki ERROR IN A MOD 0".
- `--grupa etap8`: "E8 T0: bloki ERROR IN A MOD [Armoury] (CS)" = TAK, "bloki 0, powtorki 0".
- `--grupa m97wioski`: WP-0 start, WP-1 0, WP-2 0 = TAK.
- `--grupa m97obroncy`: OB-1 0 = TAK.
4) Regresja: bieg z zapisu autotest-F-d240 (jak 100-160). Dzis 0 blokow, ma zostac 0. WP i OB jak wyzej. Jesli w logu jest okup wioski (WP-4 "kontrola okupu"), sprawdzic, ze ochrona wygasa w dniu Until (ProtSync z doby, SideVillageVisit.cs:767).
5) Opcjonalnie droga uspiona (b): w jednym procesie wczytac zapis, wyjsc do menu i zaczac nowa kampanie. W CrashScribe nadal 0 blokow SideVillageVisit.
6) Porownanie z dzisiejszymi wynikami: progi/101nowa/etap4.txt i etap8.txt (NIE, 1 blok) wobec nowego biegu.

## M96D-3: zaleglosci DS miast w biegu z zapisu (558 tys. w 96160, 743 tys. w 97160, 873 tys. w 100-160 przy progu 104 885) oraz VR-3: premia drog Valyrii w audycie 23.35% zamiast 25%

**cause:**

LOGI. Logi biegow 97160 i 96160 (Armoury-2026-10-20_20-59-03 i 18-34-36) zostaly juz skasowane przez rotacje. Dowody biore z biegow o tych samych objawach. Dla M96D-3: 100-160, Armoury-2026-10-21_04-57-25.log (sprawdz_logi m96dlugi_dlugi: M96D-3 NIE, 873 649 zl u 26 miast). Dla VR-3: Armoury-2026-10-21_00-07-42.log (160 dob z zapisu, VR-3 NIE, 23.39%, 963 probki). Biegi "nowa" przechodza (M96D-3 28-394 zl), bo w autotescie nowej kampanii KS nie jest zastosowana: w 101nowa stan KS to "Plan" i nie ma ani jednej linii f5-lud. Wtedy SurplusReview nic nie robi (DsTowns.cs:585, KsTownApplied). Ta sciezka zadziala dopiero w grze z uzbrojona KS. W 100-160 jest plik armoury-ks-auto.flag i KS jest ZASTOSOWANA od doby 109112.

=== M96D-3 ===
Kto ma zaleglosci (doba 109235): Starfall 658 857 (75.4%), Ifequeveron 55 276 (6.3%), pozostale 24 miasta 159 516 (18.3%). Dlugi rad nie pomagaja: 9 rad niewyplacalnych, "zaleglosc bez kredytu 6 - zostalo 791 190".

P1 (glowna). Przeglad wedlug nadwyzki (f5 SurplusReview) podnosi firme ponad doplyw netto kupcow.
- DsTowns.cs:613: add = min(grow*364, 0.25*F). Nie ma gornej granicy NetAvg*364.
- DsTowns.cs:590: kwalifikacja patrzy na kase brutto (g > 1.5 x zapas). Odliczenie naleznosci (CofferOwes) z f16 dodano tylko w SurplusHolds:528.
- DsTowns.cs:344-345: przeglad biegnie od doby po ustaleniu firm, takze w rampie DS. W rampie kasa placi tylko rampa x rate. Reszta zostaje w kasie, wyglada jak wzrost i liczy sie x364 jako nadwyzka.
- Log: 177 podwyzek, +89.7 mln zl/rok. Firmy miast urosly z 134.5 do 231.2 mln (+72%). Okna wzrostu lezace w rampie (podwyzki 109146 i 109160) daly 25.6 mln.
- Starfall, rata z kasy (kasy-miast.csv): 227 tys./tydz (109154: 186.5k przy rampie 0.82), potem 277.6k (109161; okno 109146-160 w rampie), potem 347.0k (109175, +25%). Okno tej drugiej podwyzki zawieralo jednorazowe wplywy: 109169 +209.8k, 109173 +186.9k, 109174 +96.8k.
- Skutek: firma 49.6 tys./d przy doplywie netto 36.0 tys./d (doby 109188-215) i 22.6 tys./d (109208-235). Zaleglosc rosnie o okolo 190 tys. tygodniowo (437 678 -> 658 857 w dobach 109224-231).
- Ifequeveron: rata wzrosla z ok. 17.6k do ok. 29k, a doplyw spadl z 4.5 do 1.06 tys./d.

P2. Firma ustalona ponad doplyw.
- DsTowns.cs:381: FL i FC = 364 x sredni zawor z okna. DsTowns.cs:389: Share = dayF/net, bez limitu 1.
- Okno 109103-131 wypadlo tuz po wczytaniu starego zapisu (rampa F1 od 109075) i w trakcie wlaczania KS. Zawor drenowal zapas kasy, a nie biezacy doplyw.
- Starfall (zdobyty przez Tarly ok. 109098, kasa 803k): zawor z korona 31.1 tys./d przy doplywie 26.9 tys./d, czyli Share 1.16. Kasa spadla w oknie z 660k do 460k.
- Ifequeveron: 3.04 wobec 2.12 tys./d, Share 1.43.
- Dla porownania: firmy miast 134.5 mln tutaj, 35.7 mln w nowej kampanii.
- Share > 1 nigdy nie schodzi. Przeglad kwartalny celuje w NetAvg x Share x 364 (DsTowns.cs:487-490), a SurplusReview Share tylko podnosi.

P3. Korekta w dol jest za wolna (zapadka: w gore +25% co 14 dob, w dol -15% co 91 dob).
- Wymuszona obnizka -15% wymaga zaleglosci w 2 kolejnych dniach kwartalu (DsTowns.cs:343 Terms tylko przy today%91==0; DsTowns.cs:480 TermsLike>=2).
- W 160 dobach byl jeden dzien kwartalu (109200), wiec "w dol wymuszone 0".
- Zwykly przeglad raz na 91 dob, najwyzej -15%. Przeglad 109222: w gore +15.0 mln, w dol -7.4 mln.

P4 (nie blad; wyjasnia, czemu m96-dlugi nie pomaga). Limit dlugu rady to 3 x zapas kupcow (TownDebt.cs:121, Settings.cs:2492). Starfall: 3 x 35.7k = 107k, mniej niz jedna rata 347k.

=== VR-3 ===
W logu 00-07-42 sa:
- 62 probki po 1.01% (serie 15, 1, 26 i 20 dob, jedna partia w probce),
- 2 probki po 0.00%,
- 1 probka 16.84%.
Suma brakow to 1546 pp na 963 probki = 1.61 pp, czyli dokladnie 25.00 - 23.39.

Wartosc 1.01% oznacza, ze r0 lezy na podlodze gry (LimitMin 1.0): predkosc bez podlogi to 0.81, z premia 1.0101. To jedna wolna partia jadaca tygodniami po drodze Valyrii (823 jedn.).

Kod: MapRoadKinds.Audit (MapRoadKinds.cs:339-348) bierze co dobe pierwsze 6 partii w kolejnosci MobileParty.All, wiec zawsze te same. Kazda probka idzie do sredniej, podlogi nie wyklucza. Audyt traktow DR robi to dobrze: losuje probki (MapRoads.cs:1207) i trzyma probki z podlogi poza srednia (MapRoads.cs:1217; w tym logu 10 takich).

100-160 przeszedl (24.97%), bo trafila sie tylko jedna probka 0.00 (doba 109083). Wynik zalezy wiec od jednej partii.

Wykluczone przyczyny:
- woz taborowy -10%: CampCart.cs:225, dotyczy tylko gracza i wodza armii z graczem;
- bloto: MapMud.cs:165, na drodze funkcja wychodzi od razu;
- noc i sen: NightRest.cs:1904, kara proporcjonalna, stosunek r1/r0 sie nie zmienia; lordow z dlugiem 3 w dobach 109150-151 bylo 0;
- sufit kolumny: MapRoads.cs:414 podnosi LimitMax o ta sama premie.
VR-3 to blad miary, nie premii.

**confidence:**

wysoka

**fix:**

M96D-3 (jedna paczka; klucz-wylacznik w Settings.cs, np. KsFarmInflowCap, domyslnie TAK; potem python tools/gen_mcm.py i sprawdzic Armoury.json):

1) DsTowns.Fix (DsTowns.cs:381-389). Firma z kasy (FL+FC) najwyzej rowna doplywowi netto kupcow z okna (NetAvg x 364).
- Jesli zawor w oknie byl wiekszy, FL i FC schodza w tym samym stosunku; Share = min(1, ...).
- Zapas zebrany przed oknem nie staje sie stala rata. Zabiera go jednorazowo gersuma f7 (KsFarmCatchUp), zgodnie z zamyslem f7.

2) SurplusReview (DsTowns.cs:571-635):
- (a) w rampie DS (NewShare < 1) bez pomiaru i bez podwyzek: SG0 = MinValue, continue;
- (b) add = min(grow*364, 0.25*F, max(0, NetAvg(f,7)*364 - F)), czyli firma nigdy ponad doplyw netto (ta sama granica, ktora kod juz stosuje do Share);
- (c) warunek kasy jak w SurplusHolds: g - CofferOwes > 1.5 x zapas (przy KsFarmSurplusNetOfDues).

3) Ta sama regula w dol, w tym samym takcie 14 dob.
- Warunek: miasto ma prawdziwa zaleglosc (AL+AC) >= 1 raty, RemitOf == 0 i F > NetAvg*364.
- Wtedy firma idzie w strone NetAvg*364, najwyzej -25% na przeglad; Share = min(Share, 1).
- Zaleglosc zostaje w ksiedze (splata z nadwyzki albo z pozyczki m96).
- TownDebtLimitReserve bez zmian.

Oczekiwany efekt na 100-160 (szacunek):
- Starfall: firma ok. 9.8 mln przy ustaleniu, na koniec ok. 8.2 mln zamiast 18.06 mln; rata ok. 158k zamiast 347k.
- M96D-3 ponizej 104 885: zostaja tylko zaleglosci z chwilowych spadkow.
- Suma firm miast nie wieksza niz suma doplywow netto (udzial swiata <= 1).

VR-3:
- MapRoadKinds.Audit: ProbeFor ma zwracac takze LimitMinValue (albo uzyc MapRoads.Probe). Probki z r0 <= lmin + 0.001 ida poza srednia, a w linii pojawia sie ", na podlodze LimitMin (poza srednia) N" z nazwa pierwszej takiej partii (do diagnozy).
- Kandydaci losowani przez _rng jak w AuditList.
- tools/sprawdz_logi.py: regex M96VALYRIA_AUD z opcjonalnym licznikiem podlogi (INFO).
- Premia w grze bez zmian. Oczekiwane: VR-3 25.00% w biegach typu 00-07-42, ok. 60 probek "na podlodze" jako INFO.
- Opcja do decyzji (nie polecam): SpeedRoad podnosi takze LimitMin o (1+pct), wtedy partia na podlodze dostaje premie drogi. To zmienia gre na wszystkich drogach.

**touches:**

- Armoury/src/DsTowns.cs
- Armoury/src/Settings.cs
- Armoury/src/McmSettings.cs (z tools/gen_mcm.py)
- Armoury/src/MapRoadKinds.cs
- Armoury/src/MapRoads.cs (ProbeFor z LimitMin)
- tools/sprawdz_logi.py (M96VALYRIA_AUD, ewentualnie licznik f5 w m96dlugi)
- CHANGELOG.md

**risks:**

- Mniej podwyzek f5 to mniejszy odplyw z kas miast. NAKUM-1a/7/22 i G1-5a (kasy juz ponad prog) moga sie pogorszyc. Zapas ma schodzic przez gersume f7 i M6, wiec sprawdzac razem z tematem kas miast.
- Panowie i korony dostana mniej z firm (np. Starfall okolo -9.9 mln zl/rok). Trzeba sprawdzic C-4c (rody na minusie) i progi korony.
- Firma liczona z 28 dob doplywu nadal moze byc za wysoka w miescie z jednym duzym wplywem w oknie. Dlatego obnizka musi dzialac w obie strony.
- Share przyciete do 1 zmienia cel przegladu kwartalnego (Review). Moga dojsc obnizki -15% w dniu przegladu. Dwie reguly ruszaja ten sam poziom firmy: zachowac kolejnosc w DayStart i blokade _renewN (DsTowns.cs:575).
- Stare zapisy z Share > 1 i firma ponad doplyw schodza stopniowo (-25% na 14 dob), nie skokiem.
- Logi 97160 i 96160 sa skasowane. Przyczyny potwierdzone na 100-160 (M96D-3) i 00-07-42 (VR-3), ktore maja te same objawy.
- VR to zmiana tylko miary. Regex progu trzeba poprawic razem z DLL, inaczej VR-1 da 'nierozpoznane'. Partia jadaca tygodniami z predkoscia 0.81 to osobna sprawa do zbadania (jej nazwa pojawi sie w audycie).
- W grze Jeffa (klucz Start Calibration Auto Arm TAK) KS sie zastosuje i bez poprawki pojawi sie ta sama zapadka firm. Autotest nowej kampanii tego nie wylapie, bo KS jest tam w stanie Plan.

**verify:**

1. Bieg 160 dob z zapisu autotest-F-d240 z plikiem armoury-ks-auto.flag (jak 100-160).
- python -I tools/sprawdz_logi.py --grupa m96dlugi_dlugi <log>: M96D-3 TAK (<= 104 885). W M96D-4 "zaleglosc bez kredytu" bliska 0, M96D-2 dlug ponizej limitu.
- Linia "Dochod ludu wedlug koszyka (f5-lud)": 0 podwyzek w dobach rampy (109132-109158), mniejsze "od startu sesji (+X zl/rok)", nowe liczniki przyciecia do doplywu i obnizki.
- Linia "Firmy miast i zamkow (DS)": "firmy ... miasta X zl/rok" blisko sumy doplywow netto, wsrod "najwieksze" brak Starfall z zaleglosca > 1 raty.
- kasy-miast.csv dla Starfall: suma "pobrane" z 28 dob <= suma "doplyw_netto" z 28 dob.

2. Regresja na tym samym logu: grupy nakum (NAKUM-1a/7/12/16/22), gl1 (G1-5a), nkasy, nds, npozyczka, korona2.

3. Nowa kampania 180 dob: M96D-3 bez zmian (rzad 28-394 zl).

4. VR: --grupa m96valyria na tym samym logu i na biegu typu 00-07-42 daje VR-3 TAK (sr. 25.00 +-1.5), VR-1 nierozpoznane 0, licznik "na podlodze" jako INFO. --grupa ndrogi: NDR-5 bez zmian.

## Kasy miast i kiesy ludu/notabli/karawan rosna (NAKUM-1a/7/22/26/27, G1-5a, G1-5c, A16-5b, C-4c): kto placi, czy nowe paczki dosypuja zloto, od kiedy

**cause:**

WNIOSEK: nikt nie dosypuje zlota z niczego ani dwa razy. Kasy miast i kiesy ludu dostaja pieniadze, ktore sa juz w swiecie: od korony przez zold wojska. W nowej kampanii nie maja ujscia, bo regula zdejmujaca nadwyzke kas dziala tylko w miastach z KS, a takich miast w nowej kampanii jest 0 z 97. Skok nastapil w WGRANIU 96, przez m96-sakiewki.

A. ZLOTO Z NICZEGO / PODWOJNIE: NIE
- Suma swiata w 101-nowa: 219 719 516 (d108836) -> 220 042 523 (d109015), czyli +0.32 mln w 180 dobach.
  - Jedyny skok: +671 099 w d108943, w ksiedze "zloto bohaterow, ktorzy wrocili do swiata 670715" (m101-postacie). Poszlo do glow rodow, nie do kas.
  - Ksiega "bilans - przyczyny": ARYTMETYKA OK. NAKUM-7s: BLAD 0.
  - Niezmierzona reszta kas miast w 'Przeplywy osad (przyczyny)': +10.2 tys./d w 101 i +18.5 tys./d w 100 ("srednia 28 dob +10234 - PONAD prog 10000"). To maly przelew, nie zloto z niczego.
- m96-dlugi (TownDebt.cs:372-422 Borrow: TdTake z kiesy P/R, potem t.ChangeGold, potem :291-293 DsTowns.TdPayArrears do pana i korony):
  - 101: pozyczki 246 038 zl przez 180 dob (1.4 tys./d), w kasach zostalo razem 69 zl, dlug na koniec 207 tys., podatek nadzwyczajny 1 zl.
  - 98-100: 1.5-2.1 tys./d.
  - Pieniadz idzie z kies mieszczan do panow i korony, nie do kas.
- m102 listy goncze (NoticeBoard.Watch.cs:536 osada -> bohater, :546 pan -> bohater): 24 502 zl przez 180 dob (136/d). Z kas miast 21 637, od panow 2 865, z niczego 0. To ODPLYW z kas.
- m99-markietani (CampSutlers): dziala tylko z menu obozu gracza, w autoteście 0.
- m96-sakiewki (SavingsByNeed.cs:287-317: MenPurse.Take, potem TownFolk.EarnService i t.ChangeGold(got-folk)): kontrola w logu "roznica 0".
  - Zold garnizonu idzie do tej samej kasy dwiema drogami. SoldierPay.cs:449/476 wplaca 50% od razu do kasy (zold garnizonow +53.5 tys./d), a sakiewke m96 oproznia potem do kasy (zalogi -> kasy miast 40.1 tys./d).
  - Zloto nie jest przy tym podwojone.

B. KTO PLACI (101-nowa, okno NAKUM-7 108988-109015, na dobe)
- Odbiorcy: kasy miast +60.0 tys. (NAKUM-7 liczy +52.8), kiesy ludu miast +41.4, kasy zamkow +17.7, podzamcza +5.4.
- Platnicy: skarbce krolestw -86.8 tys., glowy rodow -17.4, notable -14.0, kiesy wsi -8.6.
- Droga pieniadza:
  1. Korona -> rody 189 tys./d (we wczesnym oknie 114): W5b 71.0, zwrot zoldu 58.1, renty 180 44.0, W5c 16.1.
  2. Rody placa zold 723 tys./d do sakiewek ludzi.
  3. Sakiewki -> kasy miast: markietani 217 tys./d, zycie w miastach 208, zalogi m96 40, do tego zold garnizonow 53.5.
- Pozostale wplywy kas:
  - lud za towar +346 tys./d;
  - nadwyzka ceny warsztatow +97;
  - zakupy sprzetu AI +96;
  - pasmo notabli E5/A3 netto +61.5 (+127/-66) - stad A16-5b;
  - 33% podatku od nadwyzki ludu wraca do kasy osady +79.5 (TownFolk.Hoard.cs:146-160, FolkHoardTaxLordShare 0.67).
- Wyplywy kas: do panow i korony tylko 126 tys./d (pompa C), a do ludu 364 tys./d (pompa A).
- Kiesy ludu: wyplata kupcom M6 ("5% zaworu") urosla z 51 do 133 tys./d razem z nadwyzka kas. W d109000 nadwyzka ponad zapas to 5.6 mln w 75 miastach. Wojsko wydaje u ludu 157 tys./d. Wplywy kies 757 tys./d, wydatki 707, czyli +50 tys./d.

C. OD KIEDY: WGRANIE 96 (m96-sakiewki)
- Udzial w pieniadzu swiata na koniec biegu (NE3-9b):

| Biegi | Sakiewki ludzi | Kasy miast | Sakiewki w dniach zoldu (A3-2) |
|---|---|---|---|
| przed 96 (nowa, r2, r3, n22a; 120 dob) | 6.14-6.48% | 4.20-4.72% | 20.8-21.2 |
| 96-101 (180 dob) | 2.66-2.85% | 5.95-6.28% | 7.5-7.9 |

- G1-5a w dobie 120: przed 96 9.25-10.34 mln, w 96-100 11.10-11.68 mln (+8..+14%), w 101 9.92 mln.
- To samo okno 108927-108955 w obu grupach biegow: sakiewki rosly +79..97 tys./d przed 96, a w 98-101 +10..25 tys./d. Kasy miast: +24..44 przed 96, potem +47..67 tys./d.
- Czyli okolo 70 tys./d zoldu, ktory dotad zostawal w sakiewkach, trafia teraz do kas miast i kies ludu.
- 97-101: tylko rozrzut miedzy biegami. Logi 96 i 97 sie nie zachowaly (porownanie z wynikow progow).
- Zmiana dlugosci biegow (od 96 trwaja 180 dob, nie 120):
  - NAKUM-1a: dluzsze okno (do 109015) pogarsza wynik. W tym samym oknie co przed 96 bez zmian: +6.4 tys./d w 101 wobec +4.5 w r3.
  - A16-5b: wartosc lepsza (-23 wobec -33..-37 tys./d), ale przy biegu ponad 150 dob prog stal sie warunkiem, wiec wychodzi NIE.
  - C-4c bez zmian: 233-257 przed 96, 230-304 po.
  - G1-5c bez zmian (r3 +30%): przyczyna to brak karawan (482 wobec 962 w Z33), a nie doplyw zlota.

D. DLACZEGO NADMIAR NIE SCHODZI (sedno)
- Podwyzka firmy o przyrost kasy (f5-lud) i gersuma (f7) dzialaja tylko w miastach z KS. Te same bramki blokuja tez regule "bez obnizki przy nadwyzce":
  - DsTowns.cs:586 `if (!StartCalibration.KsTownApplied(t)) {...continue;}`
  - takze :527 (SurplusHolds), :718, :819, :869.
- W nowej kampanii KS stoi w stanie 'Plan'. Linia f7-kasy z d109000: "miasta z KS 0". NAKUM-26/27 wychodza TAK tylko dlatego, ze nie ma czego mierzyc.
- Zostaje przeglad co 91 dob, najwyzej +-15% (DsTowns.cs:467 i :495; DsFarmRenewMax 0.15). W 101 jedyny przeglad w d108982 podniosl firmy miast z 35.66 do 38.01 mln zl/rok (+6.4 tys./d), a kasy rosly +53 tys./d.
- Komentarz f7 (DsTowns.cs:640-662) opisuje ten sam akumulator (fin91: +52.6 tys./d) i wynik modelu dla miast z KS: +53.1 -> +5.0 tys./d.
- W kampanii Jeffa ta regula obejmuje 0% miast.

E. BIEG Z ZAPISU (100-160): osobna, starsza sprawa
- Kasy miast 12.11 -> 24.40 mln (+77 tys./d), choc dzialaja tam KS w 97 miastach i gersuma (1.21 mln od startu).
- Przed 96 kasy na koniec takich biegow mialy 12.05-12.75% pieniadza swiata.

**confidence:**

wysoka

**fix:**

1) GLOWNA ZMIANA (jedna zmiana naraz): regula "firma idzie za nadwyzka kasy" dla 100% miast z firma DS, nie tylko dla miast z KS.
- W DsTowns.cs dodac funkcje SurplusTown(Farm f, Town t) = KsTownApplied(t) || (s.DsFarmSurplusAllTowns && f.Fixed >= 0).
- Wstawic ja zamiast StartCalibration.KsTownApplied(t) w miejscach: :527 (SurplusHolds), :586 (SurplusReview), :718, :819, :869 (gersuma f7).
- Nowy klucz MCM w Settings.cs: DsFarmSurplusAllTowns, domyslnie TAK. Wylaczony = jak dotad (tylko KS). Po dodaniu uruchomic tools/gen_mcm.py.
- Klucze KsFarmFollowsSurplus, KsFarmSurplusReviewMax 0.25 i KsFarmCatchUp (udzial 0.5) bez zmian.
- Sprawdzic w Armoury.json Jeffa, czy ma zapisane KsFarm*.
- "Domy najpierw" (KsFolkIncome.cs:319, HousesFirst) dziala tylko w miastach z KS, bo KsFolkWage.Active. W pozostalych miastach podstawic warunek z ksiegi kiesy ludu: R albo P ponizej swojego zapasu dni. Inaczej podwyzka przyjdzie tez tam, gdzie R/P biednieja.
- Mechanika (zachowanie z biezacego zysku, zmiany powoli): miasto z kasa ponad 1.5 x zapas kupcow placi panu i koronie przez firme tyle, o ile kasa urosla (najwyzej +25% na 14 dob), plus gersume z zapasu, ktory przelezal cale okno. Zloto sie nie rusza poza droga raty.
- Oczekiwane progi:
  - NAKUM-7 kasy miast z +52.8 do ok. +5..28 tys./d (model f7: +5.0 bez zwrotu przez wojsko, +28.4 przy zwrocie 0.5).
  - M6 do kies ludu maleje razem z nadwyzka (133 -> ok. 50 tys./d), wiec NAKUM-7 'miasta' i NAKUM-1a ponizej 30 tys./d.
  - G1-5a w dobie 120 <= 110% Z33.
  - NAKUM-26/27 zaczna mierzyc ok. 75 miast.
  - Pompa C (kasy -> panowie i korony) z 126 do ok. 170-200 tys./d.
  - Skarbce krolestw tracilyby mniej.

2) DRUGI KROK, tylko jesli po 1) kasy dalej > +30 tys./d: przy czynnej firmie DS 33% podatku od nadwyzki ludu (TownFolk.Hoard.cs:157-160, tp do town.ChangeGold) kierowac do pana i korony w stosunku firmy, zamiast do kasy osady. Teraz wraca tak do kas +79.5 tys./d (petla: kasa -> lud -> podatek -> kasa).

3) LICZNIK (bez zmiany ekonomii): pomiar pozycji "Armoury bez licznika: inne" (-13.5 tys./d), zeby reszta kas miast (+10 tys./d) zeszla ponizej progu 10000.

Nie ruszac m96-dlugi, m102, m99, SavingsByNeed: nie sa zrodlem.

**touches:**

- C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n30-calosc/Armoury/src/DsTowns.cs
- C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n30-calosc/Armoury/src/Settings.cs
- C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n30-calosc/Armoury/src/McmSettings.cs (z tools/gen_mcm.py)
- C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n30-calosc/Armoury/src/KsFolkIncome.cs (HousesFirst dla miast bez KS)
- C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n30-calosc/tools/sprawdz_logi.py (NAKUM-26/27 i linia f7: 'miasta z regula' zamiast 'z KS')
- C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n30-calosc/Armoury/src/TownFolk.Hoard.cs (tylko krok 2)
- C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/n30-calosc/CHANGELOG.md

**risks:**

- Zloto zdjete firma wraca do miast przez wojsko (panowie i korona placa zold, ten trafia do markietanow i na zycie w miastach). Recenzja f7 podaje +5 tys./d jako dolna granice, a przy zwrocie 0.5 bedzie +28 tys./d. NAKUM-7 kasy moze zostac blisko progu, wtedy potrzebny krok 2.
- Wiecej pieniedzy u glow rodow: NAKUM-7 'glowy rodow' bylo juz +26..29 tys./d w biegach 98 i 100 i moze przekroczyc +30 tys./d.
- W miastach bez KS 'domy najpierw' daje zawsze false (KsFolkWage.Active), wiec bez zastepczego warunku podwyzki przyjda tez tam, gdzie R/P biednieja.
- Szybkie podwyzki firm (+25% na 14 dob) moga zrobic zaleglosci, gdy doplyw spadnie (wojna, oblezenie): pila firm, wiecej pozyczek m96-dlugi i M96D-3. Przeglad kwartalny powinien to cofac, trzeba sprawdzic w logu.
- Start nowej kampanii: kasy zaczynaja od 25.9 mln i spadaja do 7.3 mln do ok. d108896. Regula rusza dopiero po ustaleniu firm (F3 od d108892), ale gersume we wczesnych oknach trzeba sprawdzic.
- W trwajacej kampanii Jeffa pomiar przyrostu jest w pamieci sesji, wiec po wczytaniu pierwsza podwyzka przyjdzie dopiero po 14 dobach. Klucze KsFarm* moga byc zapisane w Armoury.json.
- NAKUM-26/27 w sprawdz_logi.py licza tylko miasta z KS. Bez poprawki narzedzia efekt nie bedzie widoczny w progach.
- Bieg z zapisu (100-160: kasy +77 tys./d mimo KS i gersumy) to osobny, starszy akumulator. Ta poprawka go nie rozwiaze.

**verify:**

Autotest nowej kampanii, 180 dob, potem 160 z zapisu na regresje (tam miasta z KS bez zmian).

1) --grupa nakum:
- NAKUM-7 (doby 108987-109015): kasy miast i 'miasta' <= +30 tys./d (w 101 bylo +52.8 i +43.3).
- NAKUM-26: liczba miast > 0, kasy i nadwyzka <= +30 tys./d.
- NAKUM-27: <= 2.
- NAKUM-1a: < +30 tys./d.
- NAKUM-7s: skarbce krolestw tracą mniej niz -88 tys./d.
- 'glowy rodow' nie przekraczaja +30 tys./d.

2) --grupa etap8:
- G1-5a w dobie 120: 80-110% Z33 (10 264 881).
- C-4c nie gorzej niz 274.

3) --grupa m96dlugi: M96D-3 zaleglosci na koniec nie rosna (101: 394 zl u 23 miast). Linia 'Dlugi miast (m96-dlugi)': dlug razem rzedu 0.2 mln, nie wiecej.

4) Linie logu Armoury:
- 'Gersuma kas miast (f7-kasy): dzien N': "miasta z KS 0" zmienia sie na ok. 97 miast. Wpis "od startu sesji: panom X, koronie Y" ma X, Y > 0.
- 'Firmy miast i zamkow (DS)': podwyzki takze miedzy przegladami kwartalnymi.
- 'Pompy - sztuczne przelewy (nekon)': C (kasy -> panowie i korony) powyzej 126 tys./d.
- 'Kiesa ludu': "5% zaworu" ok. 50-80 tys./d zamiast 133.
- 'Nadwyzka kas (E5/0)': "ponad zapasem" maleje (w 101 d109000: 5.6 mln w 75 miastach).
- 'Pieniadz swiata (bilans - przyczyny)': ARYTMETYKA OK.

5) --grupa a16: A16-5b bez pogorszenia (-23 tys./d).

Skrypty do powtorzenia pomiarow okien: scratchpad 7aee5f12.../scratchpad/k/flows.py i hold.py.

## R1-10b warsztaty bez drewna 222/d (prog 177) i R1-7 kopalnia bez wegla 7.5% (prog 5%) - skad niedobor drewna i co sie zmienilo od biegu sprzed wozow dymarek (a589def)

**cause:**

0) CO MIERZY R1-10b I JAK ZMIENNY JEST POMIAR. Licznik to WorkshopLaw.cs:318 (_skipMatBy[1]++ przy reason==2) z maska MissMask (WorkshopLaw.cs:436). Cykl liczy sie tylko wtedy, gdy na polce jest mniej drewna niz floor(owed+need). Sztuka bierze ok. 0.1 ladunku, wiec brak oznacza w praktyce PUSTA polke miasta. Dowod: R1-10b idzie razem z liczba miast bez drewna. W 11 biegach (19-21.10, ten sam kod drewna): 160/11.7, 206/14.4, 158/11.1, 189/11.4, 271/13.8, 186/10.8, 224/14.0, 182/11.3, 220/12.4, 217/12.9, 222/14.2 (R1-10b / T2-K1-2). Doba po dobie w biegu 101 (d91-180) r(brak drewna, miast bez drewna) = +0.74. Srednia 11 biegow to 203/d, odchylenie ok. 33/d. Prog 177 to JEDEN pomiar biegu a589def z 12.10. CHANGELOG.md:16260 mowi wprost: "progi rowne pomiarowi jednego biegu (R1-10b 177/d) - ustawic ponownie po pierwszym biegu z poprawkami". Wzorzec bez dymarek W33 mial 199. Biegi 98-101 (182/220/217/222) nie roznia sie kodem drewna. Mieszcza sie w szumie: w 98 bylo 11.3 pustych miast, w 101 14.2.

1) STOCZNIE A17 F5 - NOWY ODBIORCA DREWNA BEZ ZAPASU KUZNI (doszly 17/18.10, po a589def). A17Fix.cs:506-507: `int hw = Count(shelf,_hardwood)... int uh = Math.Min(hw, logs)`. Stocznia bierze drewno z polki portu bez BuildFunding.WoodHold, czyli bez zapasu, ktory zostawiaja budowy (K1). Przed A17 statki NavalDLC powstawaly z niczego, bez drewna. Liczby z biegu 101 w d91-120: stocznie 263 pni/d, wiecej niz wszystkie warsztaty zbrojne (201/d). Do tego 126 prob/d "pominiete - brak drewna". W 11 biegach stocznie braly 231-269 pni/d. Doba po dobie r(brak drewna w warsztatach, "brak drewna" stoczni) = +0.75. Regresja d31-180 w 4 biegach daje stoczniom 91-131 z 216-244 cykli, ale ten udzial jest zawyzony wspolnym trendem w czasie. Stocznia buduje tylko z pelnego kompletu (ok. 12 pni). Nie zeruje wiec polki sama, tylko zbija zapas portu ponizej 12 i dobijaja go mieszczanie i kuznie.

2) DYMARKA ZERUJE POLKE SWOJEGO MIASTA HANDLOWEGO (krok b, bez zapasu). Bloomery.cs:529: `int n = Math.Min(want, shelf.GetItemNumber(wood))`. Zakup idzie w Bloomery.Daily (ArmouryBehavior.cs:2667), przed budowami (:2684) i przed warsztatami, ktore pracuja w DailyTickTown rozlozonym w dobie. Gdy dymarka chce wiecej, niz lezy na polce, bierze calosc. W d91-120 "targ 105/d" i "brak na polce 139/d" w linii Zelazo (lupy) (214), wiec takie polki ida do zera. Miasta handlowe kopaln ponizej potrzeby to srednio 4.9/d: Yunkai 24 z 30 dob, Lordsport 23, Meereen 23, Pyke 21, Tolos 16, Oldtown 14, Vaes Diaf 10. Przy ok. 16 cyklach braku na puste miasto (222/14) daje to szacunkowo ok. 60-80 z 222 cykli/d. Krok b byl juz w a589def. Nowe jest to, ze te same miasta pustynne i wyspowe karmia teraz dymarki pustynne bez weglarzy (Bloomery.F7.cs:28: "pustynia i step - 0").

3) DREWNO NIE PRZECHODZI Z MIAST ZASOBNYCH DO UBOGICH, BO BUDOWY ZJADAJA NADWYZKE PRZED EKSPORTEM (przyczyna ukladowa, ta sama dla R1-7). Budowy zostawiaja na polce 10 dob zuzycia (CaravanBulk.TargetFor, CaravanBulk.cs:328). Gdzie indeks ceny < 1, zostawiaja polowe (E3-B1b, BuildFunding.cs:63, od 16.10). Eksport (kontrakty 174, wozy kupcow, wozy dymarek) ruszy dopiero ponad prog nadwyzki: KeepFor = 2 x Target = 20 dob (MaterialOrders.cs:961, Bloomery.cs:669-670, Settings CaravanBulkSurplusFactor 2). Budowy biora wiec cale pasmo 5-20 dob, ktore moglo isc na wywoz. Liczby z biegu 101 w d91-120:
- budowy zabieraja 1285/d, czyli 61% drewna schodzacego z polek miast;
- na polkach miast lezy 2524 szt. przy zapasie rzemiosla 4863;
- 72 miasta sa ponizej zapasu, mimo to 80 targow ma "polowe progu" (indeks ceny < 1, wozy wsi sprzedaja drewno po 0.49 wartosci);
- miast z nadwyzka jest srednio 7.0/d;
- miasta prosza o kontrakty na drewno 48 razy/d i nie dostaja nic (0 szt./d): brak zrodla z nadwyzka w zasiegu 24.5, bez drogi 11.3, bez zysku 8.5, kasa 4.0. Wozy kupcow wioza 0.4 szt./d drewna.
Miasta, do ktorych drewno nie doplywa, to klimat pustynny (las x0.44) i stepowy (x0.46) oraz wyspy. Essos ma deficyt: produkcja 742, zuzycie 822/d (linia KS: rynki). R1-7 ma ten sam korzen. W d61-120 1372 lup bez wegla, z tego Bir Seif [Yunkai] 34%, Tylemos [Tolos] 18%, Akiser [Meereen] 16%, razem 68%. To kopalnie pustynne: las 0, weglarzy 0, a "bez nadwyzki w zasiegu" wynosi 16253 ladunki (16.9% potrzeby). Bir Seif ma "wozem w drodze 0", bo w zasiegu 600 nie ma miasta z nadwyzka. R1-7 w biegach 96-101 trzyma sie w 7.5-10.8%.

4) OKNO d91-120 LAPIE ROZRUCH KAMPANII. Bieg 101, w oknach 30-dobowych:
- brak drewna w warsztatach: 344, 353, 217, 222, 212, 104;
- stocznie, pni/d: 401, 241, 248, 263, 183, 120;
- zbudowane statki/d: 34, 22, 22, 21, 15, 9;
- dymarki z wlasnej polki: 192, 175, 129, 105, 77, 42;
- wozy dymarek: 149 do 444;
- puste miasta: 21 do 8.
W biegu z zapisu (400 dob) w d340-400 bylo 38/d przy polkach 24 tys.

WYKLUCZONE (z liczbami):
- Wozy dymarek F4a biora tylko ponad 20 dob zuzycia zrodla (Bloomery.cs:669-670, 745, 767). R1-7d ma reszte 0. Z czasem odciazaja wlasne polki: targ spada ze 192 do 42/d.
- Potrzeba drewna w warsztatach nie urosla (m96-receptury, krok rak N5): warsztaty zbrojne zuzywaja 201-214/d, a 14.10 przed K1 zuzywaly 199/d. Wzor jest w WorkshopLaw.cs:174.
- Woz taborowy m97 kupuje tylko gracz (CampCart.cs, naglowek: "AI wozow nie kupuje").
- Wegiel drwali ZC1 bierze z magazynu wsi, nie z polki miasta (GoodsPaths.cs:450), i to tylko 31/d.
- Funkcje 99-101 (markietani, kryjowki, prolog, postacie) nie ruszaja drewna z polek.

ZMIANY OD a589def (12.10, 177/d):
- 14.10 bieg z wozami F4a, jeszcze przed K1: 303.6/d (CHANGELOG.md:10447).
- K1 BuildWoodAboveCraftStock (14.10) obnizyl to do ok. 160-220.
- E3-B1b (16.10) oslabil K1 do polowy zapasu w ok. 80 targach.
- A17 F5 (17/18.10) dolozyl stocznie: 231-269 pni/d bez zapasu.
- f7-wegiel (18.10) pomogl kopalniom z lasem, pustynnym nie.
Wynik to dzisiejsza srednia 203/d.

**confidence:**

srednia

**fix:**

Proponuje trzy kroki. Kazdy osobno, z autotestem i wlasnym kluczem, bez stalych mnoznikow: kolejnosc na polce ustala biezacy zysk.

KROK 1 (tor S): kuznia miasta pierwsza przed dymarka na wlasnej polce. W Bloomery.cs:529 zamiast `Math.Min(want, shelf.GetItemNumber(wood))` liczyc `Math.Min(want, Math.Max(0, shelf.GetItemNumber(wood) - ForgeKeep(town)))`. ForgeKeep = ceil(CaravanBulk.UseNow(town, wood)), czyli jedna doba wlasnego zuzycia miasta: kuznie wedlug rak, domy, rzemioslo, bez dymarek. To szacunek z rak, a nie z pomiaru, wiec nie wpada w petle przy pustej polce. Dymarka i tak kupuje "na nastepna dobe". Uzasadnienie dla Jeffa: kowal zarabia na ladunku drewna wiecej niz dymarka (ok. 0.1 ladunku na sztuke wobec 5 ladunkow na lupe), ten sam wzor co 174b.3 przy rudzie i strzelarzach. Nowy klucz BloomeryShelfLeavesForges (TAK; NIE = kod 101) i dopisek w linii Zelazo (lupy) (214): "zostawione kuzniom N".
Szacunek: R1-10b spada o ok. 40-80/d (4.9 miasta/d x ok. 16 cykli). R1-7 rosnie o najwyzej ok. 1 lupe na miasto-dobe, czyli ok. +2-5 lup/d przy 23/d (z 7.5% do ok. 8-9%). To wymiana do powiedzenia Jeffowi wprost.

KROK 2 (tor S): stocznia to budowa, ta sama regula K1. W A17Fix.cs:506 liczyc `int hw = Math.Max(0, Count(shelf, _hardwood) - BuildFunding.WoodHold(town.Settlement, _hardwood, false));`. Deski bez zmian. Nowy klucz ShipyardWoodAboveCraftStock (TAK; NIE = kod 101), dopisek w linii Stocznie (A17 F5): "zostawione rzemioslu N".
Szacunek: stocznie biora mniej drewna w d1-120 (dzis 263 pni/d), rosnie ich "brak drewna", porty nie schodza ponizej zapasu. R1-10b spada lacznie z krokiem 1 do ok. 120-160/d, T2-K1-2 do ok. 9-11.

KROK 3 (tor M/L): tylko gdy po krokach 1-2 R1-7 > 5% albo T2-K1-1 > 100. Wywoz przed budowami: dla drewna budowy biora tylko ponad prog wywozu miasta (KeepFor, 20 dob), gdy jakies miasto w zasiegu zamowilo drewno (MaterialOrders.NoteMissMask). Albo prosciej: BuildFunding.Daily po MaterialOrders.Daily i wozach dymarek. Do tego E3-B1b oprzec na stanie polki (polka >= TargetFor), a nie na indeksie ceny, bo dzis 80 "tanich" targow wspolistnieje z 72 miastami ponizej zapasu. Dotyka BuildFunding.cs:63 i :370, ArmouryBehavior.cs:2684/2900.
Szacunek: wiecej miast z nadwyzka (dzis 7.0), kontrakty na drewno > 0, wozy do Bir Seif, Akiser i Tylemos. R1-7 spada w strone 5%, R1-13 bez stale tych samych kopaln pustynnych.

PROG: po krokach 1-2 ustawic R1-10b ze sredniej co najmniej 2 biegow, jak kazal CHANGELOG.md:16260. Rozrzut przy tym samym kodzie to ok. 33/d, wiec jeden bieg nie przesadza.

**touches:**

- Armoury/src/Bloomery.cs
- Armoury/src/A17Fix.cs
- Armoury/src/Settings.cs
- Armoury/src/McmSettings.cs
- tools/sprawdz_logi.py
- CHANGELOG.md
- Armoury/src/BuildFunding.cs (tylko krok 3)
- Armoury/src/MaterialOrders.cs (tylko krok 3)
- Armoury/src/ArmouryBehavior.cs (tylko krok 3 - kolejnosc Daily)

**risks:**

- Krok 1 to wymiana R1-10b na R1-7 w miastach pustynnych i wyspowych (Yunkai, Meereen, Tolos, Pyke, Lordsport): dymarka zostawia kuzni ok. 2-5 ladunkow/d, czyli ok. 1 lupa na miasto-dobe. R1-7 moze wzrosnac z 7.5% do ok. 8-9%, dopoki krok 3 nie przywiezie drewna z zewnatrz. Trzeba to powiedziec Jeffowi slowami gracza.
- Krok 2: mniej statkow z portow ubogich w drewno w pierwszych 120 dobach (dzis 21/d w d91-120). AI kupuje statki od portow (ShipPortsBuyToIdeal) i porty wolniej dojda do liczby idealnej. Sprawdzic linie Statki (181) i NA17-5b.
- Udzialy przyczyn sa szacunkami: log nie ma linii z drewnem na polce kazdego miasta. Regresja d31-180 ma R2 0.39-0.72 i jest zawyzona wspolnym trendem w czasie (stocznie i dymarki slabna razem z napelnianiem polek). Wiadomo tylko, ze mechanizmy sa w kodzie, a sumy sie zgadzaja.
- R1-10b ma rozrzut ok. 33/d miedzy biegami tego samego kodu (158-271 w 11 biegach). Prog 177 to jeden pomiar a589def sprzed K1, E3-B1b, A17 F5 i F4a, wiec pojedynczy bieg po poprawce moze jeszcze wypasc NIE albo TAK przypadkiem.
- Okno d91-120 to rozruch: brak drewna spada samoistnie do 104/d w d151-180 i do 38/d w roku 2. Poprawka nie moze psuc stanu dojrzalego. Sprawdzic tez bieg z zapisu (autotest-F-d240).
- Krok 3 zmienia kolejnosc kupujacych: budowy dostaja mniej drewna (dzis 1285/d). Ryzyko dla tempa budow (N5-5, T2-K1-3, NE3-18). Kolejnosc Bloomery.Daily -> BuildFunding.Daily -> MaterialOrders.Daily jest uzywana takze przez SourceKeepFor (pamiec doby czyszczona w MaterialOrders.Daily) - przy przestawianiu sprawdzic zasade 0 (regresje, kolizje).
- Gospodarka zamknieta bez zmian we wszystkich krokach: nie powstaje zaden nowy przeplyw zlota ani towaru, zmienia sie tylko to, kto pierwszy bierze z tej samej polki. Mimo to wpisac kontrole w CHANGELOG (Ryzyko / co sprawdzic) i uruchomic gen_mcm po nowych kluczach.

**verify:**

Autotest nowa kampania 180 dob, osobno po kroku 1 i po kroku 2. Bazowe liczby sa z biegu 101, log Armoury-2026-10-21_07-17-36.log.

(1) python -I tools/sprawdz_logi.py --grupa etap5 <log>:
- R1-10b (srednio d91-until, prog <= 177), dzis 222;
- R1-7 (d61-until, prog <= 5%), dzis 7.5%. Po kroku 1 oczekiwany lekki wzrost do ok. 8-9%; spadek dopiero po kroku 3;
- R1-7e "bez nadwyzki w zasiegu", dzis 16.9%;
- R1-13: czy dalej stale Yunkai, Meereen, Tolos.

(2) --grupa towary2: T2-K1-1 (<= 100, dzis 222.1) i T2-K1-2 (<= 20, dzis 14.2).

(3) --grupa na17:
- NA17-5b: drewno stoczni pni/d, dzis 241 w 180 dobach; po kroku 2 ma spasc;
- NA17-5c: czesci statkow "brak drewna".

(4) Linie logu, srednio d91-120:
- "Warsztaty: dzien N ... brak surowca X [ruda a, drewno b": b < 177;
- "Zelazo (lupy) (214): ... targ N za .. zl (brak na polce M": po kroku 1 nowe pole "zostawione kuzniom" > 0; "miasta handlowe kopaln ponizej potrzeby": te same miasta, ale bez braku w warsztatach;
- "Stocznie (A17 F5): ... drewno N pni ... pominiete - ... brak drewna K": po kroku 2 N w dol, K w gore;
- "Roboty ludu (P2): ... miast ponizej zapasu rzemiosla A, bez drewna B z 97": B, dzis 14, w dol;
- "Drewno: dzien N ... miast bez towaru X z 97";
- "Kontrakty surowca (174): ... drewno N (brak zrodla z nadwyzka w zasiegu a, bez drogi b ...)": krok 3 ma dac "zawarto [.. drewno > 0".

(5) Kontrola regresji:
- Budowy: wiersze T2-K1-3 (budowa >= 80 osad), NE3-18 (brak materialow <= 35), linia "Budowy oplacone ... wstrzymane: brak materialow";
- statki AI: linia "Statki (181)";
- R1-7d (kontrola wozow, reszta 0), R1-14..R1-18 (rachunek za drewno);
- ERROR 0, kod wyjscia 0.

(6) Ocena: porownac z 4 biegami bazowymi (98: 182, 99: 220, 100: 217, 101: 222) i z oknem d151-180 (dzis 104). Jeden bieg nie wystarcza (odchylenie ok. 33/d). Najlepiej dwa biegi albo zgodny spadek T2-K1-2.

## NC-16b chleb, GL2-6 jedzenie Qarth/Volantis/Braavos, GL2-8 oszczednosci duzych rynkow, W12-4 dziesiecina wsi, W12-6 tempo nadwyzki, A1-T19 towar wsi niesprzedany

**cause:**

Tylko odczyt: zadnego pliku ani gry nie ruszalem. Kod: n30-calosc/Armoury/src. Logi: 101nowa = Armoury-2026-10-21_07-17-36.log (180 dob), prolog = 06-23-39 (230 dob), 100-160 = 04-57-25 (KS zastosowany od d109113).

Ogolnie: 2 z 6 progow to bledy pomiaru (W12-4, W12-6). NC-16b i czesc GL2-6 wynikaja glownie z tego, ze autotest "nowa" biegnie bez kalibracji startu (KS), a gra Jeffa ja ma. Prawdziwe braki w grze: Qarth (GL2-6) i A1-T19.

=== W12-4 (0.00%, prog 5%) - BLAD POMIARU, udzial 100% ===
- Przy DS (DuesMedieval + DsVillageCartDues) dziesiecine wsi pobiera woz: DsVillage.cs:361 wola TownFolk.TitheVillageAtCart.
- VillageTithe odejmuje ten utarg (TownFolk.Tithe.cs:158: vh -= DsTiSeen). Dlatego linia "Dziesiecina (E5/C3)" ma zawsze "wsie 0 (0 wsi; nalezne 0)", a sprawdz_logi.py:8105-8115 czyta "nalezne" tylko z niej.
- Dowod: linia "Daniny wsi (DS)" d109015: oddane 239217, dziesiecina 11965. Srednia z 28 dob: 391801 / 7834735 = 5.001%. Dziesiecina wiec biegnie dokladnie, tylko w innej linii.
- Ten sam blad daje "wsie 0" w W12-2.

=== W12-6 (0.018, prog 0.15) - BLAD POMIARU, udzial 100% (jest skutek uboczny) ===
- TownFolk.Live.cs:1078 ustawia oferte W = 0.15 x nadwyzka. Zaraz potem LadderOffers (TownFolk.Luxury.cs:353-361; P3 FolkLuxuryList, decyzja 13.10) ja nadpisuje: W = nadwyzka/60, R i P = 0.3 x nadwyzka/60.
- Log d109015: oferta 199025 = W 178606 + R/P 20419, lokata 10719078. 199025/10719078 = 0.0186, czyli dokladnie wzor P3.
- Linia stanu i linia "Kiesa ludu" dalej drukuja "tempo 0.15" (TownFolk.Live.cs:1930 i 1991), a z tej linii prog bierze wartosc 0.15.
- Skutek uboczny: z oferty kupiono tylko 39.6% (79034 z 199774; 41640 zl "grupa bez kategorii - zostaje w kiesie"). Lokata W stoi na 10-11.6 mln od ok. 45. doby (zapas W 52 dni przy zalozonych 10). Kiesy P rosna z 513 tys. do 2.2 mln (283 dni). To czesc tematu NAKUM.

=== NC-16b (chleb 3.12 w 96nowa; 101nowa 2.94 TAK, 100nowa 3.0, 99nowa 3.28, prolog 3.03 NIE) ===
Przyczyna 1 - autotest bez KS (rozstrzyga TAK/NIE):
- Autotest "nowa" nie ma pliku armoury-ks-auto.flag (log 07:18:53: "droga nieczynna"). W grze Jeffa KS uzbraja sie sam (StartCalibration.Arm.cs:16, Settings.cs:3360 StartCalibrationAutoArm=true).
- Po zastosowaniu KS zamienniki B2 sie wylaczaja (FoodSubstitute.cs:148), a dzialaja koszyki ZC4.
- Dowod z biegu 100-160: d109112 B2 liczy 97 osad, d109113 juz 0. Indeks chleba spada z 3.95 do 1.99 w 23 doby i do 1.61 pod koniec (NC-16b TAK). Starsze biegi 160 dawaly 1.58-1.64.
Przyczyna 2 - stala podaz chleba (bez KS):
- Produkcja 1050-1150/d (74 piekarnie 610 + rzemieslnicy BK 440) i zuzycie 1050-1190/d (mieszczanie 930-1027, jedzenie partii 114-157) sa praktycznie rowne.
- Na polkach miast 2500-3400 szt., czyli ok. 3 doby. Karawany trzymaja 2447 szt. (37% zapasu), lordowie 1533.
- Tempo warsztatow E3-D2 juz przyspiesza piekarnie (cykle 459.7 -> 608.9).
- Przezbrajanie E3-D3 bierze tylko warsztaty tracace przez 60 dob (WorkshopRetool.cs:147). Dzis: 7 kandydatow, 0 zmian, wiec drogi chleb nie sciaga nowych piekarni.
- Indeks rosnie z 2.46 (d60) do 2.94 (d180), w prologu z 2.25 do 3.03. Biegi 96-101: 3.12/3.08/3.03/3.28/3.0/2.94 - wahaja sie wokol progu.

=== GL2-6 (101: Qarth min 2.1, Braavos 3.6, Volantis 5.8; prog 7) ===
Przyczyna 1 - Qarth: geografia, nie pieniadze (udzial ok. 100%, takze z KS):
- Linia GL1 d109011: Qarth ma 9 wsi i 3.43 mln ludzi, a z jedzenia tylko zboze 69.3 szt./d.
- Na 1 mln ludzi: Qarth 20 szt./d, Reach 237, Volantis 151, Braavos 153.
- Miasta maja m_t 2.47, wiec popyt x m_t, a kasa jest: 816676 przy zapasie 654358.
- Jedzenie na polkach od 1. doby 1.5 dnia, najwyzej 4.7 w 26 tabelach tygodnia. Z KS (100-160) dalej 1.2-6.7.
- Nic nie przywozi jedzenia: kontrakty 174 obejmuja tylko surowce (MaterialOrders.cs:49: iron, hardwood, leather, linen, flax, hides, wool). GL2-7 (wywoz z Reach) to "brak miary w logu".
Przyczyna 2 - Volantis i Braavos: brak KS w autotescie:
- Bez KS popyt BK x m_t (2.14 i 2.32) przy plonie wsi liczonym od u_v (Braavos u_v 1.79). Volantis 2.6-9.2 dnia, Braavos 2.2-8.3.
- Z KS (100-160): Volantis 7.1-17.9, prawie stale >= 7 od d109123. Braavos 3.4-10.2 - czesciowo ten sam brak dowozu co Qarth.

=== GL2-8 (min 27, srednio 32; prog 60) - prog sprzeczny z regulami ===
- Zapas ludu to W 10, R 21, P 30 dni (TownFolk.Live.cs:1066 i dalej). Nadwyzke ponad niego zdejmuja: danina P1 2.5%/d w wojnie, kiesa2, oferta P3 1/60 i place R/P 0.15 (krok 5, TownFolk.Live.cs:1066-1112).
- sprawdz_logi sam pisze "prog nieosiagalny" (baza W43 bez reguly: 51).
- Grupa m_t > 1.5 startuje z 27 dniami (tabela d108837) i trzyma 25-38. Biegi 97-101 (min/srednio): 33/36, 35/40, 27/29, 26/28, 27/32.
- Dopisek 11c (zarobek z skupu plonu, TownFolk.Skup.cs:73, cel 60) tego nie zmienil.
- GL2-8b = 100% (lud stac na pelny koszyk), wiec gracz tego nie odczuwa.
- Uwaga: z KS (100-160) jest gorzej: 14-19 dni i stac tylko 82-94%.

=== A1-T19 (101: 6712 szt., 0.289%; prolog 38655 szt., 1.607%; 97/98: 0.026/0.031%) ===
Przyczyna 1 - towar zostaje tylko w miastach z pusta kasa ("zaulki", kasa ponizej zapasu kupcow) - pewna, udzial ok. 100%:
- Licznik MoneyLedger.cs:319 ("kasa kupuje tyle, ile ma"). Linia zaulkow: "plon w zaulkach ... niesprzedane" rowna sie calemu niesprzedanemu towarowi (1 miasto). d109003/109009/109014: 1330/1839/1140 szt. w 1-2 wizytach.
- Volon Therys: kasa 1.6-5.5 tys. przy zapasie 122 tys. przez 150-161 dob z rzedu. Placi wozom wsi 10-16 tys./d, od karawan dostaje +17 tys./-6 tys.
- Zaulkow w 101 jest 21-24, w prologu 49-55 - stad 1.607% w prologu.
- 48% niesprzedanego towaru przypada na ostatnie 30 dob.
Przyczyna 2 - jak wozy trafiaja do pustych kas (prawdopodobne, udzialu nie da sie rozdzielic z logu):
- MarketCarts.Value (MarketCarts.cs:215) liczy od kasy z chwili wyboru. Zloto, ktore zabiora wozy juz jadace do tego miasta, nie jest odjete (_news sledzi tylko towar).
- N2-E3 (MarketCarts.cs:494-501) kieruje woz z wloknem do miasta targowego przy dowolnej wycenie > 0. d109009: 1633 szt. wlokna, wycena 33516 wobec 64165; d109014: 1883 szt., 160109 wobec 225433.
- Pierwotna przyczyna zaulkow to temat kas miast.

**confidence:**

srednia

**fix:**

1. W12-4, tylko narzedzie progow:
- sprawdz_logi.py etap5_wsie (ok. 8105-8117): do "nalezne" doliczyc "dziesiecina N" z linii "Daniny wsi (DS)" z tych samych dob okna. Podstawa bez zmian: "oddane" z linii "Utarg wsi (112)".
- Opcjonalnie w linii TitheLine (TownFolk.Tithe.cs:266-275) dodac segment "wsie przy wozie (DS) X / nalezne Y" (liczniki DsVillage _cTithe), zeby W12-2 nie pokazywal "wsie 0".
- Gra bez zmian. Oczekiwane: W12-4 = 5.00% TAK.

2. W12-6, tylko narzedzie i napisy logu:
- sprawdz_logi.py ok. 8130-8135: gdy linia doby zawiera "P3: oferta W 1/60", prog = 1/FolkSurplusSpendDays (z linii "Zbytki ludu (P3-P5): tempo 1/60") z tolerancja na R/P. Oczekiwane 0.0186 TAK.
- TownFolk.Live.cs:1930 i 1991: przy LadderOn drukowac "tempo W 1/60 (P3), place R/P 0.15" zamiast samego 0.15.
- Tempa nie zmieniac. Gromadzenie kies (niekupione 60% oferty, kiesy P +283 dni) naprawiac w temacie NAKUM, nie przez W12-6.

3. NC-16b:
(a) Glowna poprawka: autotest "nowa" z plikiem armoury-ks-auto.flag, czyli tak jak gra Jeffa; KS zastosuje sie ok. 35. doby, po rampie 28 dob. Alternatywnie NC-16b jako INFO, gdy B2 dziala bez KS. Oczekiwane: chleb ok. 1.6 (jak 100-160), TAK.
(b) Dla fazy przed KS, opcjonalnie:
- WorkshopRetool.cs:147: kandydatem do przezbrojenia zostaje nie tylko warsztat tracacy (avg<0 i slow<0), ale kazdy z >= 60 dobami historii, jesli typ z cen F4 daje >= max(obecny + 10 d, obecny x 1.3).
- Limity bez zmian: 3 na dobe, 1 na miasto na 30 dob, warsztat raz na 120 dob.
- Nowy klucz MCM WorkshopRetoolForGain (Settings.cs + gen_mcm.py).
- Oczekiwane: nowe piekarnie tam, gdzie chleb drogi; indeks chleba 2.3-2.7 bez KS.

4. GL2-6 - zywnosc w kontraktach karawan 174:
- MaterialOrders.cs:49: Ids rozszerzyc o jedzenie (grain, fish, date_fruit, meat, cheese, butter, olives).
- Wyzwalacz: jedzenie na polce miasta < 7 dob sprzedazy mieszczanom - ta sama miara co GL2-6 (PeopleMarket ShelfFood/SoldFood).
- Zrodlo: niewrogie miasto z jedzeniem ponad 14 dob wlasnej sprzedazy.
- Kontrakt tylko gdy marza >= koszt drogi (CarterPencePerKgPer100; morzem x SeaFreightShare - 174b.2, potrzebne dla Qarth).
- Platnosc: kasa kupca -> kasa zrodla; prawdziwa karawana, ktora bandy moga rozbic.
- Nowy klucz MCM TownFoodOrders. Dopisac miare GL2-7 (wywoz jedzenia, szt./d).
- Oczekiwane: Qarth >= 7 dob w d91-120 (dowoz z Volantis/Lys morzem); Volantis i Braavos >= 7 z KS.

5. GL2-8 - decyzja o progu, bez zmiany gry:
- Prog zgodny z regulami: dni oszczednosci grupy m_t > 1.5 >= zapas wazony potrzeba (W 10 / R 21 / P 30, ok. 12-14 dni) i GL2-8b >= 95%. Albo tylko wobec biegu bazowego (--baza-doby, juz w narzedziu).
- SkupDaysGoal (TownFolk.Skup.cs:73) to tylko licznik w logu - ustawic na ten sam prog.
- Oczekiwane: 27-32 w 101nowa = TAK.
- Osobno zbadac stac 82-94% z KS (temat KS/ZC4).

6. A1-T19 - MarketCarts.cs:
(1) Rezerwacja zlota:
- przy wyborze miasta (Note po ok. linii 517) zapisac oczekiwana zaplate bestVal w nowym slowniku _committed[town]; zdjac przy wjezdzie wozu albo porzuceniu kursu (porzadki _news);
- w Value() (linia 215): gold = town.Gold - zloto zarezerwowane przez inne wozy.
(2) N2-E3 (linia 501): przekierowac woz do miasta targowego tylko wtedy, gdy zaplaci ono za cale wlokno (wycena przy limicie kasy rowna wycenie bez limitu); inaczej wybor jak dotad.
- Zamknieta ekonomia bez zmian (te same przelewy, inny cel wozu). Bez nowego klucza, albo jeden VillageCartGoldAware.
- Oczekiwane: A1-T19 < 0.1% (jak 97/98: 0.03%); w prologu ponizej 1.607%. Zaulki same nie znikna - temat kas miast.

**touches:**

- tools/sprawdz_logi.py
- Armoury/src/TownFolk.Live.cs
- Armoury/src/TownFolk.Tithe.cs
- Armoury/src/MarketCarts.cs
- Armoury/src/MaterialOrders.cs
- Armoury/src/PeopleMarket.cs
- Armoury/src/WorkshopRetool.cs
- Armoury/src/Settings.cs
- Armoury/src/McmSettings.cs
- skrypt autotestu testy-*.ps1 (scratchpad, plik armoury-ks-auto.flag dla biegu nowa)

**risks:**

- NC-16b i GL2-6 dla Volantis i Braavos sprawdzalem w stanie z KS tylko na jednym biegu 100-160, uruchomionym z zapisu z 240. doby. W nowej kampanii KS zastosuje sie dopiero ok. 35. doby, a po rampie ok. 63. Brama krainy albo zima moga to opoznic. Trzeba to sprawdzic biegiem 'nowa' z plikiem armoury-ks-auto.flag.
- W stanie z KS duze rynki maja tylko 14-19 dni oszczednosci i stac je na 82-94% koszyka (100-160). To mozliwy problem w grze Jeffa po KS - poza tym tematem, do sprawdzenia w KS/ZC4.
- Zywnosc w kontraktach 174 zwieksza ruch karawan i zmienia ceny jedzenia w miastach-zrodlach (Reach, Volantis). Trzeba pilnowac NC-16b w zrodlach i GL2-4 (Food 0 warowni), zeby zrodla nie oddaly jedzenia ponizej wlasnych 14 dob.
- Szersze przezbrajanie warsztatow (WorkshopRetool) moze zabierac warsztaty innym towarom (welna, drewno), bo to ta sama pula 160 warsztatow notabli. Limity 3 na dobe / 30 / 120 dob ograniczaja tempo, ale trzeba sprawdzic R1-10b i G-progi warsztatow.
- Dla A1-T19 udzialu N2-E3 i braku rezerwacji zlota nie da sie rozdzielic z logu: zgodnosc dni jest czesciowa. Przyczyna pierwotna to zaulki (Volon Therys 161 dob ponizej zapasu, w prologu 49-55 miast). Bez naprawy kas miast towar moze dalej zostawac w wozach, tylko w innych miastach.
- Rezerwacja zlota w MarketCarts musi zdejmowac rezerwe przy kazdym zakonczeniu kursu (wjazd, porzucenie, zniszczenie wozu). Inaczej rezerwy sie skumuluja i wozy zaczna omijac zdrowe miasta.
- W12-6 to blad pomiaru, ale stoi za nim prawdziwe gromadzenie kies: z oferty P3 kupowane jest tylko 39.6%, a kiesy P rosna do 283 dni. Poprawka narzedzia nie moze zamknac tematu kies ludu (NAKUM).
- Liczby niesprzedanego towaru z mojej wlasnej sumy (14942 szt.) roznia sie od wyniku narzedzia (6712): narzedzie liczy okno i jedna linie na dobe. W raporcie uzywac 6712 / 0.289%.

**verify:**

1. W12-4 i W12-6 bez nowego biegu, po poprawce narzedzia:
python -I tools/sprawdz_logi.py --grupa etap5 "Modules/Armoury/Armoury-2026-10-21_07-17-36.log"
Oczekiwane: W12-4 ok. 5.00% TAK, W12-6 0.0186 TAK. Kontrola reczna: linia "Daniny wsi (DS)" (dziesiecina/oddane); linia "Kiesa ludu" ("oferta nadwyzki na jutro ... P3"). Ten sam test na prologu (06-23-39).

2. NC-16b:
- bieg nowa 180 dob z plikiem armoury-ks-auto.flag, potem --grupa nceny;
- w logu linia "Kalibracja startu (KS)" ze stanem zastosowany; linia "Zamienniki jedzenia (B2)" z "osad przeliczonych 0" po KS;
- "indeks cen jedzenia (mediana miast): bread" <= 3.0 (oczekiwane ok. 1.6).
Dla wariantu (b), bez flagi: linia "Warsztaty za cena (E3-D3)" - zmiany > 0, typy: bakery > 74; "Towary: ... bread produkcja" > 1150/d.

3. GL2-6: --grupa gl2 na nowym biegu 180 dob.
- Linie "Rynek od ludzi wedlug krolestw (GL2)" (co 7 dob): Qarth, Volantis, Braavos "jedzenie na polkach" >= 7 w d91-120.
- Linia "Kontrakty surowca (174)": nowe pozycje jedzenia (zawarto, dojechalo, morzem).
- Bez zmian: GL2-2 r >= 0.8 i GL2-8b.

4. GL2-8: --grupa gl2 --baza-doby <bieg bazowy>; po zmianie progu TAK przy 27-32 dniach. Linia "Kiesa ludu" ("miasta m_t > 1.5: ..., dni oszczednosci min ...").

5. A1-T19: --grupa etap5 na nowym biegu i na prologu 230 dob.
- A1-T19 < 0.1%.
- Linia "Przeplywy osad": "towar niesprzedany po wizycie: w miastach N"; "zaulki: ... plon w zaulkach ... niesprzedane" bliskie 0.
- "Kasy zaulkow (m6b)": dla Volon Therys "tabory wsi" nie wieksze niz kasa.
- "Wlokno do miasta targowego (N2-E3)": nowe pole "odrzucone - miasto nie zaplaci za wlokno".
- Bez spadku: "Towary (utarg wozow): razem ... d" i "Dowoz (wozy)".

6. Regresja: --grupa etap2, etap4, nakum, nkasy - bez nowych NIE.

