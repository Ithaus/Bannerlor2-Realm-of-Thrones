# Etap 5 - rozpoznanie D: styki z etapem 4 (11.10.2026)

Status: SAMO ROZPOZNANIE. Kod, gra, ustawienia i zapisy nietkniete; nic nie budowane.
Pytanie Jeffa: "czy mozesz zaczac etap 5 czy dopiero po etapie 4". Uzupelnia `rozpoznanie-C-dokumenty.md` (decyzje i dokumenty) - tu tylko miejsca, w ktorych
etap 5 dotyka tego samego stanu, metody albo liczby co etap 4, i co z tego wynika dla kolejnosci kodu i testow.

Skroty (scratchpad = `C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad`):
- **B:** = `scratchpad\e4\bledy\Armoury\src` - kod w grze (zbior 31 = dfb12df, WGRANIE 31, Armoury cf40fefa).
- **K46:** = `scratchpad\e4\46\Armoury\src` - E4/6 (galaz `w-toku/e4-6`, 0cd60f1; scalone w `w-toku/zbior-e4-koniec` 497c8c4, niewgrane).
- **K47:** = `scratchpad\e4\47\Armoury\src` - E4/7 (galaz `w-toku/e4-7`, 61f75d7 z poprawkami recenzji; nie scalone).
- **G:** = `scratchpad\dec32\cs_full` (TaleWorlds.CampaignSystem 1.4.8).
- **P4** = `docs\etap4\PROJEKT-ETAP4-KSIEGA-LUDZI-ZIMA-2026-10-10.md`; **E47/E48/E49/E410** = `docs\etap4\PLAN-KODU-E4x.md`; **KL** = `docs\PROJEKT-KIESA-LUDU-2026-10-09.md` (173);
  **GL** = `docs\PROJEKT-GLOD-2026-10-07.md`; **PLAN** = `docs\PLAN-DO-KONCA-MODA-2026-10-09.md` (etap 5 :148-168); **SP** = `docs\STAN-PRAC.md`; **RC** = `docs\etap5\rozpoznanie-C-dokumenty.md`.
- **z31A** = `scratchpad\kopia-z31-A\Armoury-2026-10-11_08-09-11.log` (120 dob nowej kampanii, wszystko co w grze), liczby z doby 120 (linie podane).

---

## 0. Odpowiedz krotko

1. **Etap 5 mozna zaczac juz teraz - czesciowo.** Teraz: projekt etapu 5 (KL przeliczony na dzisiejsza gre), pomiar E5-0 (tylko log) i kod paczek pieniadza,
   ktore nie maja wspolnego stanu z ksiega ludzi: 5.1 kasy miast, 5.2 dwor, 5.3 markietani i notable, 5.4 drobne kas, strona korony 5.6 i 5.7.
2. **Rdzen etapu (5.5 kiesa ludu + 5.6 + 5.7 + statki 3.3 + 5.11 targ przy zamku) dopiero po wejsciu kodu E4/7 i E4/8 do galezi koncowej etapu 4** (E4/6 juz tam jest),
   a pieniadze weteranow i najmu - po E4/10. Powod: koszyk kiesy liczy ludzi miasta i wsi, a ich liczbe zmieniaja E4/6 (zgony), E4/7 (pobor z miasta), E4/8 (migracja,
   karawany z ludzi miasta); jedzenie mieszczan idzie ta sama droga co zapas warowni E4/6; obie strony edytuja `PopulationLaw.Daily`.
3. **Test etapu 5 osobno, po tescie koncowym etapu 4.** Wyjatek: pomiar E5-0 (gra bez zmian) moze jechac w tescie koncowym etapu 4. Powody w rozdz. 5:
   te same progi (wojsko, ludnosc, zapas warowni) ruszaja obie strony, a test koncowy E4 jest baza zlota (progi etapow 2-3), na ktorej etap 5 ma stanac.
4. **Jedna decyzja wymuszona przez styki (D1):** kogo liczy koszyk kiesy ludu - KL liczyl ludzi z klas BK (2.9 mln), etap 4 mowi "klasy BK nie sa ludzmi" (P4 R0).
   Propozycja: liczba z ksiegi ludzi, rozklad klas i skala pieniadza z BK zamrozone na osade przy kalibracji (jak k w E4/3). Szczegoly rozdz. 3.

---

## 1. Stan na 11.10 (co jest gdzie)

| Paczka | Stan | Dla etapu 5 |
|---|---|---|
| E4/0-E4/5, E4/6a (lancuch jedzenia, pasza) | w grze (WGRANIE 31) | stan miasta "t:" (E4/4), konto UCHODZCY (E4/5), H w przyroscie czeka na E4/6 |
| E4/6 spichlerz i glod | kod + 2 recenzje, scalony w `w-toku/zbior-e4-koniec`, niewgrany | Food warowni, `TownDie`, schronienie w oblezeniu, H w przyroscie |
| E4/7 pochodzenie, pobor | kod + recenzja (`w-toku/e4-7`), nie scalony | pobor z miasta = -1 stanu "t:", `PeopleMenu`, czynnik rak L w produkcji |
| E4/8, E4/9, E4/10 | same plany kodu (pytania Jeffa rozstrzygniete 11.10) | migracja do miast, karawany z ludzi miasta, jency/RealPayer, najem z karczmy |
| E4/11 wioski poboczne | tylko P4 rozdz. 3 :529-532 (planu kodu nie znalazlem) | plon okregu, "zywnosc warowni tak samo" |
| etap 2 (110/112/114, 162m, 165, 166, 168) | w grze | kasy zamkow, utarg wsi, dwor minimalny, korona |
| 3.3 statki (paczka 181) | w grze, 3 wylaczniki OFF (Jeff 11.10: wlaczyc razem z podatkiem wojennym 184) | `B ShipsToPort181.cs` |
| 111' kasy miast (TownPurse) | NIE w grze (paczka ze starego lancucha; P4 rozdz. 7 "etap 5") | `TownPurse.cs` nie ma w B |

Liczby z z31A (doba 120, srodek biegu 120 dob - pojedyncza doba, nie srednia 28 dob):
- "Pieniadz swiata" (z31A:31717-31718): "zakupy" mieszkancow miast i zamkow z niczego **425 318 zl/d**, regulator kas skasowal **248 065**, dosypal **20 920**;
  statki NavalDLC inne **370 870** + sprzedaz osadom 12 808 (z niczego, bo 3.3 OFF); zold do kas osad 106 375.
- "Kasy miast (169c)" (z31A:31686): 97 miast, kasy 12.56 mln wobec celu regulatora 7.22 mln, zawor do pana 512 470/d, "do korony -" (111' nie ma).
- "Ludnosc:" (z31A:31540): renty od ludzi zaplacone **616 911 z naleznych 5 723 175 zl** (z kas osad) - pieniadz juz dzis liczony od ksiegi ludzi.
- "Ludzie:" (z31A:31742): 52.03 mln, w tym miasta 10.47 mln; "Ludnosc BK (169c)" (z31A:31689): 2.90 mln, zuzycie BK 482 241, budzet konsumpcji gry 1 005 941.
- Najem z karczmy do kas miast ok. 43.9 tys. zl/d (z30A; E410 rozdz. 10.4).

---

## 2. Styki - kazde miejsce wspolnego stanu, metody albo liczby

Kolumna "Kolejnosc": **E4 najpierw** = kod E5 pisac po wejsciu kodu E4 do galezi koncowej; **niezalezne** = mozna teraz; **razem** = jedna regula, jeden projekt.

| # | Etap 5 | Etap 4 | Wspolny stan / metoda / liczba | Co sie zderza | Kolejnosc |
|---|---|---|---|---|---|
| S1 | 5.5 koszyk miasta (ilu ludzi je i kupuje) | E4/4 stan miasta "t:" (w grze); E4/6 `TownDie`; E4/7 pobor z miasta i karczma -1 stanu (E47 D1); E4/8 R2 migracja wies -> miasto (`TownMigrationRefillDays` 364, Jeff 11.10) i karawany z ludzi miasta | `PeopleBook` "t:" czytany przez `PopulationLaw.PeopleOf` (K46 PopulationLaw.cs:215; `TablePeopleOf` :149 = ta sama definicja od E4/4); `PeopleBook.TownDie` (K46 PeopleBook.cs:593) | KL liczy ludzi z klas BK (KL 1.2, L9 "dwie skale": lud na BK 3.09 mln, wojsko na PL). P4 R0: "klasy BK nie sa ludzmi". Dwie definicje ludzi miasta = dwie zasady na jedno zjawisko; skala: miasta PL 10.47 mln wobec ok. 1.65 mln BK (x6.3). Po E4/8 stan "t:" sie rusza (migracja, karawany, pobor) - koszyk z BK by tego nie widzial | **D1 teraz (projekt); kod 5.5 po kodzie E4/7+E4/8; kalibracja po tescie koncowym E4** |
| S2 | 5.5 wies: oplaty stale 4 zl na glowe rocznie zamiast renty 20% kiesy, zakupy wsi 0.07 zl na glowe; 5.10 dziesiecina od utargu | E4/3-E4/7 ludzie wsi w DOM (hearth x k_v): E4/5 uchodzcy, E4/6 zgony i ucieczki z glodu, E4/7 pobor -1/k | renty od ludzi `PopulationLaw.Daily` (K46 :1153-1216, `DailyRent` :985, `PopulationRentMaxShare` :1160), hak KL `VillageTakings.VillageRentYieldsToFees = false` (B VillageTakings.cs:53-59) | "na glowe" w KL = wolni ludzie BK wsi (hearth x 5 przy braku BK). Renta nalezna juz dzis idzie od ksiegi (ludzie x 40/364) - druga definicja glowy w tej samej metodzie | **razem z S1 (D1); E4/7 najpierw** (DOM wsi ostateczny po poborze) |
| S3 | 5.1 kasy miast (zawor, dosypka liczona), 5.8 kasy bez dosypki | E4/10 najem z karczmy (LevyGold, -30..-37 tys. zl/d do kas miast, E410 :386-387); E4/9 znika dzialka BK `ExecuteCapture` i jej platnik E3/1 RealPayer (kasa miasta), mniej sprzedazy jencow (E49 :461-462) | `Town.Gold` - przeplywy, nie ta sama metoda (B LevyGold.cs:87-105; RealPayer) | kalibracja zaworu i "liczonej dosypki" na przeplywach, ktore E4/9 i E4/10 zmieniaja; 5.8 wymaga reszty ksiegi < 10 tys. przez 28 dob zmierzonej PO nich | **kod 5.1 niezalezny (teraz); test i kalibracja po tescie koncowym E4** |
| S4 | 5.5 zakupy jedzenia mieszczan (kotwica, "stac na jedzenie") | E4/6 Food warowni: `ShelterPostfix` (K46 Famine.cs:386), mieszczanie gina przy Food 0 (`TownDie`), progi F6/F7 konca etapu 4 (zadna warownia AI nie glodzi przed 30. doba zimy, mediana zapasu U27 >= 364) | gra dolicza do Food sprzedane na targu jedzenie (`includeMarketStocks`, G DefaultSettlementFoodModel.cs:82-88); te same pozycje kupuje konsumpcja BK, ktora 5.5 skaluje (KL 5.2 kotwica); ten sam cel lataja WinterBite.TownFoodPostfix i E4/6 | lud bez pieniedzy kupuje mniej jedzenia -> mniej jedzenia do Food -> Food 0 -> zgony E4/6. Dwie przyczyny (bieda, brak plonu) jedna droga - dobrze, jesli zaprojektowane; ale progi F6/F7 etapu 4 rusza wtedy etap 5 | **E4/6 najpierw (jest w galezi koncowej); F6/F7 jako bramka w tescie E5** |
| S5 | 5.9 glod ma skutki (zadowolenie BK -> dobrobyt) | E4/6 glod wsi (`Famine.HungerOf` K46 Famine.cs:234), H w przyroscie i zero urodzen (K46 PopulationLaw.cs:540); P4 E4/6 ryzyko (:456): "5.9 dzieli ten sam glod - jedna definicja" | definicja "glodu"; dobrobyt warowni = W w stopie przyrostu R1 (g = 0.5 + 0.6 W - 0.6 U - 1.5 H, w grze od E4/4) | KL 5.2: glod 5.9 = `Afford_jedzenie` < 0.95 przez 7 dob (brak pieniedzy); E4/6: brak racji. Do tego obce skutki glodu: RBL -2 dobrobytu, BEE -0.5% chlopow/d przy zmianie Food < -3 (GL 2.13, CHANGELOG E4/6). Ryzyko podwojenia w przyroscie: H (E4/6) + nizszy W przez 5.9 | **razem (D2); po 5.5 i E4/6** |
| S6 | 5.11 targ przy zamku (woz wsi najpierw zamkowi jedzenie na 10 dni, Jeff 09.10 04:55) | E4/6 odstepstwo 4: "spichlerz miasta zywiony przez okregi (GL 2.13 krok B) - odlozone"; E4/8 zboze nowej druzyny z `Town.FoodStocks` siedziby (E48 6.2 pkt 6); E4/11 plon okregu = suma wiosek, "zywnosc warowni tak samo" | Food zamku/miasta + woz wsi z jedzeniem; nadwyzka spichlerza E4/6 "na wozy" | 5.11 i krok B to jedno zjawisko (warownia je z okregu) - zrobione osobno daja dwie reguly; 5.11 zmienia zapas warowni, ktory mierza F6/F7 i z ktorego E4/8 bierze zboze | **razem (D3); po E4/6 i E4/8; E4/11 tylko przez API plonu** |
| S7 | 5.6 podatek wojenny (im wyzszy, tym wolniej rosnie dobrobyt) | E4/4 R1: W = dobrobyt warowni wobec mediany krainy (w grze); prog konca E4: ludnosc 0.2-0.8%/rok, w wojnie do -0.5% (Jeff 11.10) | dobrobyt jako wejscie przyrostu | podatek obniza przyrost ludzi przez W - drugi powod tej samej liczby | **niezalezne w kodzie; osobny test (diagnoza)** |
| S8 | statki 3.3 (wlaczenie z 5.6) | brak wspolnego stanu: lodzie E4/3 to tabory wsi NavalDLC, E4/8 `RefillConvoyTroops` - tylko ludzie | wspolne zdarzenia `OnPartyDisbanded` / `MobilePartyDestroyed` (ShipsToPort181: zwrot zlota za statki; E4/7 okno 4.3; E4/8 10.4) - obserwatorzy roznych stanow | port placi "z nadwyzki kasy" (B ShipsToPort181.cs naglowek: kasa - zapas kupcow 10 000 + 12 x dobrobyt - tarcza zoldu - tarcza dworu) - zalezy od 5.1, nie od E4 | **niezalezne od E4; po 5.1, wlaczenie z 5.5/5.6** |
| S9 | 5.1/5.5 "nadwyzka kasy miasta" (KL: ponad zapas i ponad lokate ludu) | E4/9 zmienia tylko ilosc wyplat RealPayer | jedna funkcja nadwyzki uzywana przez RealPayer (E3/1), ShipsToPort181, CastlePurse, prog renty miasta (`TownRentFloorGold`, K46 PopulationLaw.cs:1193) | po 5.5 lokata ludu w kasie miasta: kazdy platnik "z nadwyzki" musi ja odjac, inaczej wyda pieniadze ludu | **wewnatrz E5 (D4); E4 niezalezny** |
| S10 | pieniadze weterana i najmu (poza lista 5.1-5.13; RC rozdz. 3) | E4/10: 1/3 zwolnionych od tieru 3 idzie do karczmy najblizszego miasta; sakiewka zwolnionych dalej do kiesy wsi (B ClanBudget.cs:751-756); "do kasy miasta karczmy" odlozone do etapu 5 (E410 0 pkt 11, 13); KL 5.2: najem = usluga, "docelowo sakiewka najetego (167)" | sakiewka (MenPurse) przy przejsciu SLUZBA -> WETERANI; LevyGold przy najmie | czlowiek idzie do miasta, jego pieniadze do wsi; najem placi miastu, choc najmuje sie prawdziwego weterana | **E4/10 najpierw (pula weteranow musi istniec)** |
| S11 | 5.13 napis "czy ludzi stac na chleb" w menu miasta | E4/7 `PeopleMenu` ("People of this land", `ShowInquiry`, bez `SwitchToMenu` - K47 PeopleMenu.cs:10-26; E4/11 rozszerzy te sama pozycje) | ta sama pozycja menu osady | druga pozycja menu o ludziach miasta = dwa okna na jedno zjawisko | **E4/7 najpierw; linia w tym samym oknie; po roku testu 5.5** |
| S12 | 5.5 kiesa wsi (czesc utargu), 5.10, 5.12 (RC: do skreslenia) | E4/5 wies Looted produkuje; E4/7 czynnik rak L w `MaterialLaw.ProdPostfix` i w modelu zywnosci (CHANGELOG E4/6 odstepstwo 1); E4/11 plon okregu | utarg taborow wsi (ilosc), nie kod | tylko wielkosc utargu, od ktorej liczone sa udzialy | **niezalezne w kodzie; kalibracja po E4** |
| S13 | 5.2 dwor pelny | E4/7 (`ClanBudget` 20 linii), E4/9 (Straz w `Releases`), E4/10 (wejscia W1 w `ClanBudget`) | plik `ClanBudget.cs` (dwor 162m siedzi w nim); prog wojska wspolny (E4: 95-115 tys. w wojnie, zalogi >= 85%; E5: "jak 2.7") | tylko konflikty scalania (rozne metody) + ta sama liczba w progu | **niezalezne w kodzie; osobny test** |
| S14 | 5.3 markietani (163), notabl bez 10 tys. z niczego (164c) | brak (E4/7-E4/8 zmieniaja, kim jest zolnierz, nie jego sakiewke) | MenPurse.OnLeft (B MenPurse.cs:258), tworzenie notabla | - | **niezalezne** |
| S15 | 5.4 drobne kas (m.in. oblezone miasto) | E4/6 schronienie w oblezonej warowni (Food, nie zloto) | tylko odczyt "oblezona" | - | **niezalezne** |
| S16 | 5.5 rozklad klas W/R/P z BK | E4/7 BK `DeduceManpower` przy werbunku (sklad BK), E4/10 weterani dopisani do BK miasta (E410 0 pkt 9) | klasy BK (`PopulationData`) | tylko udzialy; przy D1 liczba ludzi nie zalezy od BK | **niezalezne przy D1** |

**Kolejnosc w dobie** (do projektu E5): dzis `Devastation.Daily` -> `Famine.Daily` -> ksiega ludzi (CHANGELOG E4/6); E4/8 dopisuje przyrost i migracje miast w `PopulationLaw.Daily`
(E48 15.5); KL 5.2 chce `MoneyLedger.BlockOpen` -> `TownPurse.BeforeRents` -> `PopulationLaw.Daily` -> `TownPurse.Daily` -> `CastlePurse.Daily` -> `TownFolk.Daily` -> `BuildFunding.Daily`.
Koszyk dnia ma czytac stan miasta po migracji tej doby; schronienie E4/6 czyta stan Famine z poprzedniej doby - nie zderza sie, ale musi byc opisane w projekcie E5.

**Wspolne pliki (konflikty scalania, bez wspolnego stanu):** `PopulationLaw.cs` (E4/4, E4/6, E4/7, E4/8 R2; E5: renta wsi -> oplaty, koszyk), `ArmouryBehavior.cs` (kolejnosc doby),
`ClanBudget.cs`, `LevyGold.cs` (okna E4/7 i E4/10; E5: dokad idzie najem), `MoneyLedger*.cs` (E5: nowi posiadacze w "Pieniadz swiata" - jedna suma zlota swiata),
`Settings.cs`/`McmSettings.cs`/`tools/gen_mcm.py`, `tools/sprawdz_logi.py` (nowa grupa etap5), `CHANGELOG.md`. `PeopleBook*.cs` - etap 5 tylko czyta (przez `PeopleOf` i odczyty kont), nie pisze.

---

## 3. Decyzje wymuszone przez styki (do projektu etapu 5)

**D1 - kogo liczy koszyk kiesy ludu (S1, S2, S16).** Jedna definicja ludzi (P4 R0, zasada Jeffa "jedna zasada na jedno zjawisko") przy zachowaniu skali pieniadza z KL:
koszyk osady = ludzie BK osady z dnia kalibracji x koszyk KL na glowe x (`PeopleOf` dzis / `PeopleOf` z dnia kalibracji); rozklad W/R/P - udzialy klas BK. Mnoznik zamrozony na osade
przy kalibracji, jak k_v w E4/3. Skutek: smierc z glodu, pobor, migracja i karawany zmieniaja koszyk; suma pieniadza swiata zostaje w skali rachunku KL (KL L9); ryzyko KL 6.7
("ludnosc BK rosnie > 2%/rok") przechodzi na ksiege (z31A: +0.5%/rok, w wojnie moze spadac). To decyzja projektu (bez zmiany zasad gry) - Jeffowi tylko informacja.
Alternatywa (KL bez zmian - BK): koszyk nie widzi poboru, glodu ani migracji, a renty (juz od ksiegi) i oplaty wsi (od BK) liczylyby glowy dwoma sposobami.

**D2 - jedna definicja glodu (S4, S5).** Glod osady = brak jedzenia (E4/6: racje wsi, Food 0 warowni) albo brak pieniedzy na jedzenie (5.5 `Afford_jedzenie`); jedna funkcja
z nazwana przyczyna. Skutki: zgony i uchodzcy - tylko E4/6; zadowolenie BK i dobrobyt (5.9) - raz, nie na wierzchu RBL/BEE (sprawdzic w dekompilacji, ktore z nich dzialaja przy
naszym lancuchu jedzenia - ryzyko zapisane juz w P4 E4/6); w przyroscie R1 - zmierzyc w tescie, ile ubywa przez H, a ile przez nizszy W po 5.9 (prog: bez podwojnego ciecia tej samej doby).

**D3 - jedna regula "warownia je z okregu" (S6).** 5.11 (zamek) i GL 2.13 krok B (miasto) w jednym projekcie i jednej paczce: zrodlo = woz wsi / nadwyzka spichlerza E4/6,
platnik = kasa warowni (zamek) albo kiesa ludu (miasto, 5.5), zapas Food liczony tak samo jak w U27. Krok B budzi BEE (GL 2.13: "-0.5% chlopow dziennie przy zmianie < -3") - pomiar przed wlaczeniem.

**D4 - jedna funkcja "nadwyzki kasy miasta" (S9).** Zapas kupcow (10 000 + 12 x dobrobyt) - tarcza zoldu - tarcza dworu - lokata ludu; uzywaja jej wszyscy platnicy "z nadwyzki"
(RealPayer, ShipsToPort181, CastlePurse, renta miasta, zawor 5.1/5.7).

**D5 - pieniadze weterana i najmu (S10).** Nowa pozycja etapu 5 (poza 5.1-5.13): sakiewka zwolnionego idzie tam, dokad idzie czlowiek (dom - kiesa wsi, karczma - z nim w puli,
wydawana w miescie karczmy); zaplata za najem - do sakiewki najetego weterana (KL 5.2) zamiast do kasy miasta. Po kodzie E4/10.

---

## 4. Podzial etapu 5 na porcje

| Porcja | Pozycje | Kod | Test | Dlaczego tak |
|---|---|---|---|---|
| **E5-0 pomiar** (gra bez zmian) | "kiesa ludu na sucho" wedlug D1 (koszyk i `Afford` na `PeopleOf`, kotwica m); jedzenie z targu wobec Food na miasto; przeplywy kas miast po E4/9 i E4/10 (najem, RealPayer); sakiewki zwolnionych do wsi/karczmy; nadwyzka kasy wedlug D4 | **teraz** (tylko odczyt; linia startowa z BRAK, jak E4/0) | **moze jechac w tescie koncowym E4** | zero wplywu na gre i progi E4; daje baze do kalibracji 5.5 na swiecie po etapie 4 (KL liczyl na kodzie 2e235ea sprzed etapow 2-4 - RC pkt 5, 14) |
| **E5-A pieniadz bez ludzi** | 5.1 kasy miast (111' tryb 1, dosypka liczona, D4; z BK `HandleMarketGold` - RC rozdz. 3); 5.2 dwor pelny; 5.3 markietani + notabl bez 10 tys.; 5.4 drobne kas; kod strony korony 5.6 (stawka, decyzja AI krola) i 5.7 (wagi rent, 1/2 nadwyzki kupcow) - wlaczane dopiero z 5.5 | **teraz, rownolegle z E4/7-E4/11** (brak wspolnego stanu z ksiega ludzi; tylko konflikty scalania - rozdz. 2) | osobny test 120 dob po tescie koncowym E4 (jeden zbior wedlug zasady Jeffa) | kazdy przeplyw widac w "Pieniadz swiata"; progi etapow 2-4 jako bramki |
| **E5-A2** | D5: sakiewka weterana, najem do sakiewki | po kodzie E4/10 | z E5-A albo E5-B | potrzebuje puli WETERANI |
| **E5-B rdzen** (jedno wgranie - PLAN 5.5, Jeff 11.10 statki z 184) | 5.5 kiesa ludu (D1), 5.6 podatek wojenny (placi kiesa ludu - RC pkt 6), 5.7 wyrownanie, 3.3 statki ON, 5.11 + krok B (D3) | **po wejsciu kodu E4/7 i E4/8 do `w-toku/zbior-e4-koniec`** (E4/6 juz jest); liczby po tescie koncowym E4 i E5-0 | nowa kampania 40 dob + rok, plus bieg zimowy 120 dob (F6/F7 E4/6 jako bramka) | stan "t:" musi miec juz migracje i karawany (E4/8), DOM wsi pobor (E4/7); `PopulationLaw.Daily` scalony raz |
| **E5-C po roku testu E5-B** | 5.8 (bez bezpiecznika i dosypki), 5.9 (D2), 5.10 dziesiecina, 5.13 (linia w `PeopleMenu` E4/7), 5.12 (RC: do skreslenia) | po roku E5-B | drugi test roczny (PLAN :168) | PLAN: zaleza od 5.5 i roku jego testu; 5.8 wymaga reszty < 10 tys. przez 28 dob na kodzie bez dosypki |

Bez zaleznosci od etapu 4 i do zrobienia od razu: **projekt etapu 5 v1** - KL przeliczony na stan gry (RC rozdz. 4: wies 60%, zamek 1.5, progi 168) z D1-D5 i ta tabela.

---

## 5. Test etapu 5 a test koncowy etapu 4 - osobno

**Rada: test koncowy etapu 4 najpierw i osobno; etap 5 testowany swoimi zbiorami (E5-A, E5-B, E5-C). Jedyny wyjatek - E5-0 (tylko log) w tescie koncowym E4.**

1. **Diagnoza: te same progi, dwie przyczyny.**
   - wojsko lordow w wojnie 95-115 tys. i zalogi >= 85% (P4 rozdz. 4; E48 N4 "PROG GRANICZNY") - rusza je E4/8 (zalogi bez przyrostu z modelu, hamulec wolnych rak przy nowych
     druzynach) i etap 5 (mniej zlota u rodow - KL: rody ok. -10%, dwor pelny, podatki; PLAN 5: "wojsko jak w 2.7");
   - ludnosc netto 0.2-0.8%/rok (w wojnie do -0.5%) - rusza ja E4/4-E4/7 i etap 5 przez dobrobyt (W w R1: 5.6 podatek, 5.9 glod);
   - zapas warowni F6/F7 - rusza go E4/6 (schronienie, zgony) i etap 5 (jedzenie z targu do Food w 5.5, 5.11).
   Przy wspolnym tescie niezdany prog nie mowi, ktora strona go zepsula; procedura P4 rozdz. 4 ("wylaczamy kawalek w Armoury.json i powtarzamy") kosztuje wtedy wiecej biegow
   niz osobny test (CLAUDE.md 8 pkt 2).
2. **Progi etapow 2-3 na zlocie - baza dla etapu 5.** Etap 4 z zalozenia nie rusza zlota (P4 rozdz. 2, 5.2 pkt 4) poza E4/9 (znika dzialka BK z niczego i jej platnik E3/1) i E4/10
   (-30..-37 tys. zl/d najmu do kas miast). Test koncowy E4 potwierdza, ze etap 2-3 dalej TAK, i mierzy reszte ksiegi po E4/9 i E4/10. Etap 5 zmienia zloto celowo: znika ok. 425 tys./d
   "zakupow" z niczego i ok. 248 tys./d kasowania regulatora, ok. 384 tys./d statkow z niczego, dochodza nowi posiadacze (kiesy ludu, gmina, lokata; zapasy startowe z daru kas -
   RC pkt 12) i podatek wojenny. Warunek wejscia 5.5 (KL ryzyko 1: reszta < 10 tys./d) i 5.8 (reszta < 10 tys. przez 28 dob) wymaga reszty zmierzonej bez etapu 5. W tescie
   wspolnym reszta etapu 3 bylaby suma bledow E4/9/E4/10 i nowych posiadaczy E5 w jednej sumie zlota swiata - nie do rozdzielenia.
3. **Forma testu sie nie sklada.** Koniec E4: nowa kampania 364 doby + bieg zimowy 120 dob + bieg 728 dob ("spalona wioska wraca po roku"); etap 5: 40 dob + dwa lata (PLAN :168),
   oba na nowej kampanii, ale na innym DLL. Wspolny bieg nie skraca zadnego.
4. **Zasada Jeffa "testy zbiorcze" (SP 11.10 03:05) zostaje spelniona wewnatrz etapu:** E5-A jednym testem, E5-B jednym (40 dob + rok + zima), E5-C jednym.
5. **Wyjatek E5-0:** pomiar nie zmienia gry (jak E4/0), wiec nie psuje diagnozy E4, a test koncowy E4 da od razu liczby kiesy "na sucho" na swiecie po etapie 4.

**Kolejnosc calosci:** (1) teraz: projekt E5 v1, kod E5-0 i E5-A rownolegle z E4/7-E4/11; (2) test koncowy E4 (+ E5-0) -> wgranie E4; (3) test E5-A -> wgranie;
(4) kod E5-B (galaz od wgranego E4) -> 40 dob + rok + zima -> wgranie; (5) E5-C -> drugi rok. Nowa kampania Jeffa - po etapie 7 (PLAN rozdz. 4) albo najwczesniej po E5-B.

---

## 6. Czego to rozpoznanie nie sprawdzilo

- E4/8, E4/9, E4/10 sa tylko planami - numery linii i nazwy funkcji (`TakeLevy`, `TownShift`, `PeopleBook.Arms/Canon/Veterans`) moga sie zmienic przy kodzie; E4/11 nie ma planu kodu.
- Nie sprawdzalem w dekompilacji, ktore skutki glodu RBL/BEE/BK dzialaja przy naszym lancuchu jedzenia (D2) ani sygnatury BK `CalculateBudget` (KL ryzyko 4) - to praca projektu E5.
- Skala D1 (ludzie BK osady przy kalibracji) - wymaga sprawdzenia, czy BK ma dane dla kazdej z 798 osad w chwili kalibracji (z31A: "bez danych BK 0" w dobie 120; w pierwszej dobie - do sprawdzenia).
- Liczby z31A to jedna doba (120); w projekcie brac srednie 28 dob (RC pkt 15).
