# PROJEKT - OBLEZENIA (14.10.2026)

Zadanie: slowa Jeffa 14.10 po biegu dwuletnim fin30 - "przeciez to tak nie moze wygladac, cos jest zle, sily nie sa rowne, oblezenia sa za szybkie
i za latwe, musimy zmienic oblezenia albo sily albo to i to". Ten projekt dotyczy tylko OBLEZEN: przygotowan i machin, glodu zalogi, murow,
przewagi obroncow w szturmie, decyzji AI o szturmie i wypadow. Sily (zalogi, druzyny, budzet, bunty) sa w osobnym projekcie.
Podstawa: `ROZPOZNANIE-CZAS.md`, `ROZPOZNANIE-SILY.md`, `ROZPOZNANIE-MODY.md`. To projekt: kod nie jest zmieniony, nic nie jest wgrane.

Oznaczenia: **[P]** pomiar (plik / linia), **[K]** kod (plik:linia), **[U]** ustawienie (plik), **[S]** szacunek. Rok = 364 doby; gra jest
liczona pod rok 84. Typ zmiany: **[N]** nowy kod u nas, **[M]** nasz klucz MCM. **[O]** (ustawienie obcego moda) - w tym projekcie zadnego.
Skroty sciezek: G = gra `dec32\cs_full`, INT = nasze drzewo `zb\int` (w-toku/zbior-e8), DEC = `scratchpad c36b73ca\dec\` (dekompilacje ROT, NavalDLC, SCA - pliki `sca_*.cs`), R = `docs\oblezenia\robocze\`.

## 0. Dla Jeffa (prosto)

1. Dzis zamek pada srednio po 12 dobach, miasto po 14, Krolewska Przystan tez po 14 [P]. To 1/30 roku. Przez 2 lata padlo 57 twierdz i zaden szturm nie zostal odparty [P].
2. Przyczyna nr 1: gra liczy cierpliwosc oblegajacych pod rok 84 dni. Po 4 dobach mury co godzine licza sie w decyzji mniej, a po ok. 6 dobach zaczynaja pomagac oblegajacemu.
   Tak bylo w 110 ze 113 decyzji o szturmie [K][P].
3. Przyczyna nr 2: w samym szturmie mury daja obroncom tylko ok. 2.5 raza wiecej ciosow, a przy 100 obroncach w miescie (30 w zamku) przestaja dzialac w ogole.
   Szturmujacy nigdy sie nie wycofuje. Bitwa trwa, az obroncow zabraknie [K].
4. **Zmiana 1 (O1):** na poczatku oblezenia mury licza sie w decyzji podwojnie. Przez pierwsze 2 tygodnie (zamek), 3 tygodnie (miasto) albo 5 tygodni (stolica)
   wodz szturmuje tylko przy miazdzacej przewadze. Potem przez kolejne 4 / 6 / 10 tygodni wymaganie spada do dzisiejszego, ale nigdy nizej.
5. **Zmiana 2 (O3 + O4):** obroncy na murach bija o 1/4 mocniej, a mury dzialaja prawie do konca (do 50 obroncow w miescie, 20 w zamku).
   Gdy padnie albo zostanie ranny co trzeci szturmujacy, a mury sa wciaz obsadzone, szturm sie cofa i oblezenie upada.
6. **Zmiany pomocnicze (O5, O6):** zaraza obozowa zaczyna sie po 3 tygodniach zamiast po 9 dobach i rosnie wolniej. Armia pod murami traci najwyzej 1 punkt spojnosci na dobe.
   Bez tego dluzsze oblezenia skonczylyby sie rozpadem armii, a nie zdobyciem.
7. Na biegu fin30 szacuje [S]: zdobyc 19-36 zamiast 57, odpartych szturmow 4-13 zamiast 0. Zamek pada zwykle po 3-5 tygodniach (miazdzaca armia nadal po 1-2), miasto po ok. 6-8, stolica po ok. 3 miesiacach i rzadko.
   Wiecej twierdz uratuje odsiecz albo wypad, bo bedzie na nie czas.
8. Bez zmian zostaja: tempo budowy machin (Twoja decyzja z 17.09, a i tak juz pasuje do roku 364), zapasy jedzenia (twierdze maja na 7-9 miesiecy), ustawienia obcych modow.
   Zmiany wchodza w 2 paczkach: najpierw czas (O1 + O5 + O6), potem szturm (O3 + O4); po kazdej bieg roczny.

## 1. Co jest zle (z rozpoznan)

| # | Fakt | Dowod |
|---|---|---|
| F1 | 206 oblezen w fin30; zdobycie po mediana 12 dob (zamki 12, miasta 14), najdluzej 17. Historycznie oblezenie twierdzy trwalo zwykle 2-7 miesiecy | [P] ROZPOZNANIE-CZAS 2, 8 [H] |
| F2 | decyzja o szturmie zapada po mediana 8.4 doby (4.2-12.7); czlon czasu w decyzji jest ujemny w 110/113 przypadkach (mediana -1.66), czyli mury pomagaja oblegajacemu | [K] G `BesiegerCamp.cs:407-418`; [P] 113 linii O0 |
| F3 | stale cierpliwosci 96 h i -0.02/h sa ustawione pod rok 84; tylko one nie sa przeliczone (wojny Diplomacy juz rozciagniete x4.33) | [K] G `BesiegerCamp.cs:408-409`; ROZPOZNANIE-MODY 7 |
| F4 | w szturmie obronca ma 2 x round(0.5 + 1 + D^0.3 x 0.7 x przewaga) ciosow na godzine, a oblegajacy 2 x round(1.5 + D^0.3); premia znika przy <= 100 (miasto) / <= 30 (zamek); w szturmie nie ma odwrotu (gra wylacza ucieczke dla oblezen) | [K] G `DefaultCombatSimulationModel.cs:249-256`; `DefaultEncounterModel.cs:381-389` |
| F5 | 0 szturmow wygranych przez obronce w 140 (fin30 + fin21); wymiana w zdobytych szturmach ok. 1.5 oblegajacego na obronce; ludzie przy szturmie mediana 6.1 : 1 | [P] `bitwy.log` B175; R `projekt/wynik-gorna.txt` |
| F6 | glod 0 dob w 206 oblezeniach; zapas miasta 274 doby, zamku 225 (mediana) | [P] `spichlerze-regiony.csv` |
| F7 | twierdzy ratuje tylko odsiecz albo okup: 42 wypady i 14 bitew pod murami wygranych przez strone twierdzy, 26 okupow | [P] ROZPOZNANIE-CZAS 6e |
| F8 | budowa obozu i machin: razem x0.25 tempa gry z RBM (BK 0.5 x RBL 1.0 x nasze 0.5) -> oboz w 4-6 dob przy 2352 ludziach | [K][U] ROZPOZNANIE-CZAS 4.1 |

## 2. Reguly (decyduje Claude)

### O1 - Cierpliwosc oblegajacych w roku 364 [N]

Ten sam wzor gry ([K] G `BesiegerCamp.cs:375-431`), tylko inna waga murow w czasie. s = sila oblegajacych / (sila obroncow x przewaga^(w0 x f)).
Szturm jest mozliwy, gdy s > 1, a dalej rzut jak w grze: 10% x s po karze za machiny.
- `w0` = waga murow gry: 0.8, x1.25 bez taranu, x1.25 bez wiezy (bez zmian);
- `f` = mnoznik czasu: **2.0** przez H dob, potem liniowo do **1.0** przez 2H dob, dalej **1.0** (nigdy mniej - mury nie pomagaja oblegajacemu);
- **H: zamek 14 dob, miasto 21 (x1.5), stolica 35 (x2.5)**. Pelny spadek konczy sie po 42 / 63 / 105 dobach. Stolica = `CapitalTreasury.IsCapital` (ta sama, co w okupie).
- Skutek w liczbach [S z wzoru]: wymagana przewaga sily na starcie to przewaga^(2 x w0), czyli ok. 10-18 razy. Pod koniec zostaje przewaga^w0, czyli ok. 3.2-4.3 razy (tyle co dzis w 1.-4. dobie).
  Dla 'murow' 1.5 (mediana decyzji fin30) szturm zapada po 34 / 51 / 85 dobach (zamek / miasto / stolica). Dla 2.2 (mediana zdobyc) po 27 / 40 / 67 dobach.
- Bez zmian: pusta warownia (gra szturmuje od razu); armia Innych (lore - umarli nie czekaja i nie choruja).
- Wylacznik: `SiegePatienceEnabled` (true); `SiegePatienceCastleDays` 14 (0 = gra).

Gdzie: postfiks juz jest - `SiegeRansom.AssaultPost` [K] INT `SiegeRansom.cs:266`. Na poczatku, przed `if (!Live || !AnyKey) return;` (:271), dopisac
`if (SiegePatience.Veto(__instance)) { __result = false; return; }`. Kolejnosc jest wazna: najpierw cierpliwosc, potem okup. Cykl okupu ma sie otwierac dopiero na naszym "tak".
Veto (nowy plik `Armoury/src/SiegePatience.cs`) czyta sily przez istniejace `SiegeRansom.GameRatio` [K :210-257] (tylko odczyt).
Gdy s_nasze <= 1, wynik to false. W przeciwnym razie "tak" gry przechodzi z prawdopodobienstwem min(1, 0.1 x s_nasze x kara) / min(1, 0.1 x s_gry x kara), czyli tak, jakby gra losowala z naszym s.
Machiny i wyrwa dzialaja jak w grze: mury z zerem na wszystkich odcinkach daja przewage x0.25 [K] G `DefaultCombatSimulationModel.cs:210-213`. Ostrzal sam wiec skraca czekanie.

### O3 - Mury trzymaja w szturmie [N]

Postfiks `DefaultCombatSimulationModel.GetSimulationTicksForBattleRound` [K] G `:241-280`. Warunki: szturm na ladzie (`EventType == Siege`, nie morski, nie sala lorda).
- ciosy obroncow: wspolczynnik murow **0.7 -> 0.875 (x1.25)**; ciosy oblegajacych bez zmian;
- mury dzialaja, dopoki obroncow jest **> 50 w miescie / > 20 w zamku** (gra: 100 / 30).
- [S]: wymiana rosnie z ok. 1.5 do ok. 1.9 oblegajacego na obronce. Bitwa szturmu trwa ok. 0.5-1 doby dluzej, bo wydluza sie faza murow.
- Dotyczy tez autobitwy gracza (ta sama regula dla obu stron). Bitwa w misji gracza - bez zmian.
- Lancuch: ROT i NavalDLC przepuszczaja ladowe oblezenie do Default [K] DEC `ROTCombatSimulationModel.cs:81-84`, `NavalDLCCombatSimulationModel.cs:117-188`; zaden mod tej metody nie lata.
- Wylacznik: `SiegeWallsAssaultPercent` 125 (100 = gra).

### O4 - Szturm odwolany [N]

Postfiks `DefaultEncounterModel.GetMapEventSideRunAwayChance` [K] G `:381-389`; gra zwraca 0 dla oblezen. Wynik ustawiamy na **0.5 na godzine symulacji**, gdy spelnione sa wszystkie warunki:
szturm na ladzie; strona oblegajaca; gracza po tej stronie nie ma (jak w grze); wodz nie jest z Innych; obroncow wiecej niz 50 / 20 (jak w O3);
**straty oblegajacych w tym szturmie >= 1/3**, liczone jako `TroopCasualties / (TroopCasualties + NumRemainingSimulationTroops)` [K] G `MapEventSide.cs:55, :126`.
- Dalej dziala sciezka gry [K] G `MapEvent.cs`. Ucieczka sprawdzana jest co sesje (:1016, :1326-1333). Potem 4 rundy poscigu, w ktorych oblegajacy nie bije (G `DefaultCombatSimulationModel.cs:267-273`), i `EndByRunAway` (:1134-1138, :1335-1338).
  Wygrywa obronca, `RemoveAllSiegeParties` (:2093-2096) i **oblezenie upada**. Wycofujacy sie nie trafiaja do niewoli (:1955-1960).
- NavalDLC przepuszcza do Default [K] DEC `NavalDLCEncounterModel.cs:99-102`; ROT lata tylko `GetCharacterSergeantScore`.
- [S] na biegu fin30, przy dzisiejszym czasie szturmow: odparte 19 z 54 zdobytych szturmow (35%). Po O1 zostaje 18-28% szturmow (rozdz. 4).
- Wylacznik: `SiegeAssaultBreakPercent` 33 (0 = gra).

### O5 - Zaraza obozowa pod dluzsze oblezenia [M]

`SiegeSicknessIncubationDays` **9 -> 21**, `SiegeSicknessRampPercent` **15 -> 3** [U] `Armoury.json` (oba klucze sa w pliku). Do tego domyslne w INT `Settings.cs:446, :448`.
Wzor bez zmian: 0.6%/d x (1 + rampa x doby po inkubacji) x tlok [K] INT `CampFever.cs:64`, `:145-154`.
- [S] Zachorowania narastajaco na 100 ludzi (tlok 1). Dzis: 45 do 34. doby. Po zmianie: 10 do 34. doby, 27 do 51., 76 do 85.
  Chorzy ida do rannych, umiera 1/10. Obroncy maja 40% tempa, a przy glodzie x2.
- Historia: zaraza w obozie byla glownym kosztem dlugiego oblezenia, wiec dlugie oblezenie stolicy ma kosztowac duzo.

### O6 - Spojnosc armii pod murami [N]

Postfiks na metodzie `CalculateDailyCohesionChange` czynnego modelu. Typ brany jest w kampanii z `Campaign.Current.Models.ArmyManagementCalculationModel.GetType()`.
Dzis to SCA `StrategicArmyManagementModel` [K] DEC `sca_StrategicArmyManagementModel.cs:72-80`. Wola on Default z postfiksem BK x0.5 i dodaje -3.5 "gleboko".
Gdy wodz armii oblega (`LeaderParty.BesiegerCamp != null`), spadek jest **nie wiekszy niz 1.0 na dobe**. Brakujaca czesc dodajemy wpisem "Holding the siege lines".
- [S] Dzis armia 5 druzyn ma 0 spojnosci po ok. 23 dobach gleboko w obcym kraju, a przy granicy 50 po ok. 29 dobach.
  Po zmianie wytrzymuje ok. 100 dob, bez zakupu za wplyw [ROZPOZNANIE-MODY 3 i 7].
- Latka wpinana jest w kampanii, jak `SiegeRansom.EnsureHooks` [K] INT `SiegeRansom.cs:425-440`. Wylacznik: `SiegeCohesionCapPerDay` 1.0 (0 = gra).

### Czego NIE zmieniam i dlaczego

| Co | Dzis | Decyzja | Dlaczego |
|---|---|---|---|
| tempo budowy obozu i machin | `SiegePacePercent` 50 [U], BK `LongerSieges` 0.5 [U], RBL 1.0 [U] -> x0.25 tempa z RBM | bez zmian | juz ok. x84/364 (0.23) [S]; Jeff 17.09 sam przestawil RBL na 1.0, bo jego oblezenie bylo za wolne [U CHANGELOG 2026-09-17]; dlugosc oblezenia ma wynikac z O1, a nie z wolniejszej budowy, ktora dotyka gracza |
| zapasy jedzenia | miasto 274 doby, zamek 225 (min 75 / 91) [P] | bez zmian | historycznie w normie (6-12 miesiecy) [H]; po O1 glod zagrozi tylko oblezeniom > 75 dob (stolice) [S]; zmiana zapasow ruszylaby Famine, VillageBook, GL2 |
| wytrzymalosc murow, naprawa | 30 000 / 50 000 / 67 000, naprawa 0 w oblezeniu [K] | bez zmian | wyrwa (x0.25) juz skraca O1; ostrzal nie jest logowany - najpierw pomiar (rozdz. 6) |
| wypady i odsiecz | 42 wypady wygrane przez twierdze [P] | bez zmian | to glowna droga ratunku; po O1 bedzie na nia czas [S] |
| okup (O1 Armoury) | `SiegeRansomBloodyRatio` 1.5 [K] | bez zmian | dziala dalej, tylko pozniej (na naszym "tak") |
| SiegeOutside (zaloga ginie w polu przed szturmem: 11 z 57 zdobyc, m.in. Krolewska Przystan) | [K] G `MapEvent.cs:651-654`; [P] ROZPOZNANIE-SILY | poza tym projektem | nowa mechanika (zaloga wychodzi w pole, gdy dolacza obronca spoza murow) - do projektu SILY albo osobnego rozpoznania |
| ustawienia obcych modow (BK, RBL, Diplomacy) | - | bez zmian | nic [O] nie jest potrzebne, wiec nie trzeba zgody Jeffa na cudze ustawienia |

## 3. Gdzie w kodzie - zestawienie

| Regula | Typ | Plik / cel latki | Klucz MCM (domyslna; wylacznik) |
|---|---|---|---|
| O1 cierpliwosc | [N] | nowy `Armoury/src/SiegePatience.cs`; wywolanie w `SiegeRansom.AssaultPost` przed :271 | `SiegePatienceEnabled` true, `SiegePatienceCastleDays` 14 |
| O3 mury w szturmie | [N] | nowy `Armoury/src/SiegeWalls.cs`: postfiks G `DefaultCombatSimulationModel.GetSimulationTicksForBattleRound` | `SiegeWallsAssaultPercent` 125 |
| O4 szturm odwolany | [N] | `SiegeWalls.cs`: postfiks G `DefaultEncounterModel.GetMapEventSideRunAwayChance` | `SiegeAssaultBreakPercent` 33 |
| O5 zaraza | [M] | `Armoury.json` (kopia `.bak`, gra zamknieta) + INT `Settings.cs:446, :448` | `SiegeSicknessIncubationDays` 21, `SiegeSicknessRampPercent` 3 |
| O6 spojnosc | [N] | `SiegePatience.cs`: postfiks na typie czynnego ArmyManagementCalculationModel, wpinany w kampanii | `SiegeCohesionCapPerDay` 1.0 |

Napisy w grze i opisy MCM po angielsku. Po dodaniu kluczy uruchomic `python tools/gen_mcm.py`. Klucze sa nowe, wiec w `Armoury.json` ich nie ma - domyslne zadzialaja (CLAUDE.md 7).

## 4. Skutek na biegu fin30 [S]

Model: R `projekt/skutek_fin30.py`, wyniki `wynik-gorna.txt`, `wynik-dolna.txt`, `wynik-srodek.txt`. Wejscie: 113 decyzji O0 i 206 epizodow z `czas/fin30-epizody.csv`, flaga stolicy z linii O0 (`projekt/o0-stolica.csv`).
Zalozenia: sily z chwili O0 sa stale. Nowa doba decyzji wynika z O1. Odparcie (O3 + O4) liczone jest, gdy wymiana z B175 x 1.25 x (obroncy - 50/20) >= 1/3 szturmujacych.
**Dolna granica:** oblezenie, ktore trwa dluzej, moze w kazdej dobie skonczyc sie odsiecza, wypadem albo odstapieniem. Prawdopodobienstwo przyjete z fin30: doby 1-17 wedlug pomiaru, potem 6.4%/d.
**Gorna granica:** zadnych dodatkowych przerwan.

| Miara (714-728 dob) | fin30 [P] | po zmianie - dolna | po zmianie - gorna |
|---|---|---|---|
| zdobycia razem | 57 | ok. 19 (16 z decyzji + 3 poza nia: Harrenhal skrypt ROT, Pinkmaiden i Blackhaven po wypadzie) | ok. 36 (33 + 3) |
| szturmy odparte (O4) | 0 | ok. 4 (18% szturmow) | 13 (28%) |
| okupy | 26 | ok. 3 | 26 |
| oblezenia przerwane przed nowa decyzja (odsiecz, wypad, odstapienie) | - | ok. 63 ze 113 | 1 (armia > 100 dob) |
| decyzje bez szturmu ('mury' < 1; w fin30 bylo z nich 7 zdobyc) | 0 | 22 | 22 |
| szturm od razu ('mury' >= przewaga^w0, sila 10-18 x) | - | 13 | 13 |
| zdobycie od startu - zamek | mediana 12 (8-17) | 15 (8-33); przetrwaly glownie miazdzace armie | 24 (8-33) |
| zdobycie od startu - miasto | 14 | 41 (n ok. 1) | 47 (31-54; n 5) |
| zdobycie od startu - stolica | Krolewska Przystan 14 | 87 (n ok. 0) | 87 (n 1) |
| zdobycia w roku 1 / roku 2 | 31 / 26 | - | 17 / 16 (25 / 21 dopuszczonych, 8 / 5 odpartych) |
| doby oblezen razem / naraz | 1768 / 2.43 | ok. 2970 / 4.1 | ok. 5150 / 7.1 |

Przyklady (gorna granica) [S]:
- Cape Wrath (zamek): 'mury' 2.35, decyzja 10.4 -> 22. doba.
- Starfall (miasto): 2.06, 6.1 -> 42. doba.
- Yronwood (miasto): 2.23, 7.7 -> 32. doba.
- Lorath (stolica): 1.93, 10.1 -> 62. doba, szturm odparty.
- Willow Wood d167: 9.04, szturm od razu (4469 na 183).
- Krolewska Przystan d516 (stolica): 1.50, 8.0 -> 81. doba. Ale padla po SiegeOutside (zaloga zginela w polu przed szturmem), wiec O3 i O4 jej nie chronia.
- Vaes Dothrak d415: 0.99, szturmu nie ma.

## 5. Ryzyko

| Obszar | Ryzyko | Co z tym |
|---|---|---|
| gospodarka | doby oblezen x1.7-2.9: oblezona osada nie placi (R22 `BesiegedPay`), wozy i karawany staja, uchodzcy jedza zapas (`FamineSiegeShelter`); armie placa zold dluzej (budzet 166); mniej lupu L0-L2 i mniej blizn lojalnosci -15 (mniej buntow w zdobytych miastach); Diplomacy: mniej zmeczenia "za oblezenie", wiec wojny moga trwac dluzej | bieg roczny: kiesy rodow i zold vs fin30 d364, linie BesiegedPay, Famine |
| jedzenie obozu | armia stoi 4-12 tygodni; RBL zjada oboz tylko przy glodzie (latka Mends) [K] | linia o glodzie obozu; jesli armie glodza sie masowo - osobna regula zaopatrzenia |
| wydajnosc | 4-7 oblezen naraz zamiast 2.4. O1 liczy sily przy kazdej mysli wodza (ok. 3 razy na godzine na oblezenie, gdy gra mowi "tak"). O3/O4 dzialaja raz na godzine szturmu. CampFever raz na dobe | czas doby gry w CrashScribe: najwyzej +10% |
| gracz | jego decyzja o szturmie bez zmian; budowa machin bez zmian; autobitwa szturmu trudniejsza (O3) dla obu stron; jako wasal w armii AI czeka tygodnie; jako obronca ma czas na odsiecz; O4 nie dotyczy strony gracza; zaraza pozniej, ale dluga przy dlugim oblezeniu | napisy EN; wylaczniki w MCM |
| fabula ROT | Harrenhal - skrypt bez decyzji O0 [P], bez wplywu; Czarny Nurt (d766, poza 2 latami) - jesli ROT czeka na decyzje AI, O1 go opozni | przed kodem sprawdzic dekompilacje zdarzenia Blackwater; w razie potrzeby wylaczyc O1 dla oblezen ze skryptu (lista StoryIds jest w `SiegeRansom.cs:69-70`) |
| "wieczne" oblezenia | 22 armie z 'murami' < 1 nie maja szturmu; gra sama porzuca cel ponizej ok. 3.3 x obrony [K] G `DefaultTargetScoreCalculatingModel.cs:226-243`, ale to nie jest zmierzone | prog testu: 0 oblezen > 100 dob, oblezenia > 60 dob bez szturmu policzone |
| mniej zdobyc niz trzeba | dolna granica 19 w 2 lata - wojny moga stanac | prog testu ponizej; korekta jednym kluczem (`SiegePatienceCastleDays`) |
| laczenie z projektem SILY | wieksze zalogi + O1-O4 mnoza efekt | osobne biegi; najpierw jedna paczka |

**Kontrola calosci (CLAUDE.md 8.0):**
- O1 siedzi w tym samym postfiksie, co okup, przed nim. Linia O0 dostaje pola f, klase i HP murow. Liczniki okupu licza po naszym "tak".
- Zaden obcy mod nie lata `StartingAssaultOnBesiegedSettlementIsLogical` [ROZPOZNANIE-MODY 3].
- O3: Default (lad), latki BK `SimulateHit` i nasze NorthHomeEdge / ValyrianWardSim siedza w innej metodzie - nie koliduja.
- O4: `LosersFlee` z zalozenia "szturmu nie dotyka" [K] INT `Settings.cs:823`. Odwrot nie bierze jencow. `SiegeRansom.OnMapEventEnded` policzy to jako szturm bez zdobycia (:343-352), co jest zgodne. `KingdomBalance` B175 wypisze "zdobyta nie".
- O6: liczy po BK i SCA (ostatni w lancuchu), wiec nie ma podwojnego liczenia.
- Nic z niczego: zadna regula nie tworzy zlota ani ludzi.

## 6. Test

Nowe linie logu (warunek wgrania):
- `Cierpliwosc oblezenia (O1)`: dziennie liczba oblezen oraz wstrzymanych i dopuszczonych decyzji. Przy dopuszczeniu: osada, klasa, doba, f, mury, s gry / s nasze, HP murow w %.
- `Mury w szturmie (O3)`: dziennie rundy z murami, w tym z przedzialu 50-100 / 20-30.
- `Szturm odwolany (O4)`: osada, oblegajacy na starcie / straty, obroncy.
- `Spojnosc w oblezeniu (O6)`: armie i suma przycietego spadku.
- Linia startowa: czynny typ `ArmyManagementCalculationModel` i wpiecie 4 latek.

| Bieg | Paczka | Progi (fin30 dla porownania) |
|---|---|---|
| A - roczny, 364 doby | O1 + O5 + O6 (bez O3/O4) | zdobycia 10-22 (fin30 rok 1: 31); mediana startu -> zdobycia: zamek >= 21 dob, miasto >= 35 (12 / 14); zaden szturm przed 14. doba poza 'mury' >= przewaga^w0; oblezen > 100 dob: 0; armie rozwiazane w oblezeniu z powodu spojnosci <= 10% oblezen; CampFever: zmarli oblegajacy <= 5% ludzi obozu w oblezeniach do 60 dob; czas doby gry <= +10%; kiesy rodow i zold d364 bez spadku > 15% |
| B - roczny | A + O3 + O4 | odparte 10-35% szturmow (0%); wymiana w zdobytych szturmach 1.7-2.5 (1.5); kazde odparcie z linia O4; zadne odparcie z udzialem Innych; szturm gracza w misji bez zmian (sprawdzenie w kodzie, nie w biegu) |
| C - dwuletni, 728 dob | A + B | zdobycia 20-36 (57); odparte >= 5 (0); Reach zdobywa w Dorne <= 6 twierdz (11); Krolewska Przystan nie pada szturmem przed 60. doba oblezenia; doby oblezen <= 5200; 0 oblezen > 100 dob |

Jesli A da < 10 zdobyc w roku, zmniejszyc `SiegePatienceCastleDays` 14 -> 10 (miasto i stolica ida proporcjonalnie). Jesli > 22, zwiekszyc do 18. Jeden krok na bieg.
Tor L (dotyka wszystkich wojen): pelny lancuch z recenzja, potem autotest i wgranie po udanym tescie (stala zgoda Jeffa).

## 7. Nie wiem / do sprawdzenia przed kodem

- Ile oblezen naprawde przerwie odsiecz, gdy beda dluzsze. Rozstep 19-36 zdobyc wynika z tego, ze nie wiem, czemu 47 oblezen fin30 odstapiono bez bitwy (ROZPOZNANIE-CZAS 10).
- Czy AI buduje machiny miotajace w dlugim oblezeniu i czy ostrzal zbija mury do zera (wtedy przewaga x0.25 skraca O1). Ostrzal nie jest logowany, wiec potrzebne jest pole HP murow w linii O1.
- Wymiana 1.5 z B175 jest orientacyjna (B175 niepelne przy szturmach wielodniowych; KP: 3 straty na 92). Prog 1/3 trzeba skalibrowac biegiem B.
- Czy zdarzenie ROT Czarny Nurt (i inne skrypty oblezen) czeka na decyzje AI o szturmie.
- Czy `GetMapEventSideRunAwayChance` nie jest w 1.4.8 latane przez inny mod poza sprawdzonymi (BK, ROT, NavalDLC) - przejrzec liste latek Harmony w logu startowym.
- Jedzenie oblegajacej armii przez 4-12 tygodni (zakup, wsie, glod obozu) - nie liczone.
- Skad 18 szturmow "bez rozstrzygniecia" - O4 tego nie zmienia; nadal nie wiem.
- Liczby historyczne - z wiedzy ogolnej (ROZPOZNANIE-CZAS 8).

## 8. Pliki robocze (`docs\oblezenia\robocze\projekt\`)

- `skutek_fin30.py` - model skutku (python -I; zmienne HZ/HM/HS, HLX, NOHAZ, WALLS_X, REPULSE, PRZYKLADY, DOBY).
- `wynik-gorna.txt`, `wynik-dolna.txt`, `wynik-srodek.txt` - wyniki dla H 14/21/35, O3 x1.25, O4 1/3 (srodek: pozne przerwania x0.5).
- `o0-stolica.csv` - 113 linii O0 fin30: dzien, osada, rodzaj, stolica, fabula, Inni.
