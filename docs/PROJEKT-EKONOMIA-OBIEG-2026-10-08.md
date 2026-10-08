# PROJEKT - zamkniety obieg pieniadza i budzet rodow (WERSJA KONCOWA po krytyce, 08.10.2026)

Status: SAM PROJEKT, bez kodu. Nic nie wgrane, nic nie zacommitowane. Odpowiedz na slowa Jeffa (08.10): "widac, ze ekonomia nie dziala, wszyscy
zbankrutuja, minelo tylko 40 dni, po 4 latach lub wiecej bedzie masa bankructw".

Ten plik zastepuje roboczy `PROJEKT.md` z katalogu sesji (`scratchpad\...\dzien-6\ekonomia-obieg\`) - jest kompletny, nie trzeba go czytac razem
z tamtym. Zmiany wobec wersji porannej (krytyka) sa w rozdz. 2; reszta projektu (zasady, tabela przeplywow, budzet, korona, dlug, log) jest
tu przepisana juz z poprawkami.

Podstawa:
- autotest TOWARY 3 (40 dob, 08.10 06:51): log `Modules\Armoury\Armoury-2026-10-08_06-51-52.log`, CSV `CrashScribe\economy-2026-10-08_06-53-20.csv`;
- **autotest ROCZNY na tych samych zasadach (Armoury 5a7074c0 = TOWARY 3, nowa kampania, 364 doby, skonczony 08.10 10:19, wynik OK)**:
  log `Modules\Armoury\Armoury-2026-10-08_08-18-38.log`, CSV `CrashScribe\economy-2026-10-08_08-20-02.csv` - pierwszy prawdziwy pomiar roku
  (rano byl jeszcze w toku; badania B1-B3 i poranny projekt opieraly prognoze "dzis" na 40 dobach);
- badania B1 (bilans pieniadza), B2 (budzet lorda i AI), B3 (historia 1300-1400) w `...\dzien-6\ekonomia-obieg\B1..B3-*.md`;
- kod: worktree t3-sklad (2bde2bf = TOWARY 3 + 160 + poprawka opisu), dekompilacje `scratchpad\ore-supply` (gra 1.4.8 `cs`, BK `bk`, BetterEconomy `be`);
- gotowe opisy paczek `docs/paczki/108..114-*.md`, `docs/paczki/w-toku/160-*.md`, `docs/REGULY-KRAIN-I-DLUGU-2026-10-06.md` (rozdz. 5), decyzje Jeffa w `docs/STAN-PRAC.md`;
- rachunek krytyka (nowy, niezalezny od rachunku autora): `...\dzien-6\ekonomia-obieg\krytyk\sim_krytyk.py`, `gracz.py`, `przejscie.py`,
  analizy roku `rok_csv.py`, `rok_log.py`, `rok_rozklad.py`; wyniki `wynik-krytyk.txt`, `rok_*.txt` (rozdz. 11).

Kalendarz: rok = 364 dni (Calendar.cs), 4 lata = 1456 dob. Oznaczenia: [P] pomiar z logu / CSV, [K] przeczytane w kodzie, [S] szacunek / rachunek,
[H] historia (zrodla w B3). Wszystkie liczby rachunku to rzad wielkosci (+-30%).

---

## 0. Dla Jeffa - prostym jezykiem

1. **Rok na dzisiejszych zasadach juz zmierzony** (autotest 364 doby, 08.10 10:19): 92 z 308 glow rodow ma mniej niz 5 000 zl, Bank z Braavos
   ma 73 bankrutow i z 5 mln zostalo mu 0.7 mln, skarbce Polnocy, Reach, Dorzecza, Doliny i Zelaznych Wysp sa puste od ok. 9. miesiaca.
   Wojsko sie nie sypie (ok. 100 tys. przez caly rok), ale jest placone z pozyczek i pustoszejacych skarbcow.
2. **To nie jest "wszyscy na zero", tylko "biedni coraz biedniejsi, bogaci coraz bogatsi":** zloto rodow UROSLO dwa razy (62 -> 117 mln),
   bo gra codziennie robi z niczego ok. 1.4 mln, a kasuje ok. 1.0 mln. Srodkowy rod ma po roku 86 tys. (na starcie 230 tys.), a najbogatsza
   dziesiata czesc ponad 1 mln (na starcie 305 tys.). Bogaca sie krolowie, najemnicy i panowie miast; biednieja panowie zamkow, rycerze
   i caly Westeros (Polnoc ma 25 rodow i razem 2 mln).
3. **Zasada naprawy - jedna:** kazda moneta ma kogos, kto ja placi, i kogos, kto ja dostaje. Nikt poza rodami nie gromadzi bez konca: miasto,
   zamek, wies, sakiewka zolnierzy i skarbiec oddaja dalej to, co maja ponad swoj zapas.
4. **Pan wydaje tyle, ile zarabia** (Twoja zasada "z biezacego zysku"): w pokoju na wojsko ok. 1/4 dochodu, ok. 1/3 na dwor (jedzenie, sukno,
   sluzba - wydane w jego miescie albo zamku), reszta na sprzet, budowy i zapas. W wojnie wojsko dostaje ponad polowe dochodu, czesc zapasu
   i polowe zoldu od korony. Nadmiar wojska wraca do domu (wies albo karczma, ze swoim sprzetem i koniem).
5. **Korona placi polowe zoldu wojsk w polu (Twoja decyzja 05.10) - ale z tego, co biezaco zbiera,** a nie z zapasu, ktory sie konczy.
6. **Zolnierze wydaja zold** (w miescie i u markietanow w polu) - sakiewki przestaja puchnac (dzis 18 mln po roku).
7. **Miasto i zamek przestaja byc "zbiornikiem z przelewem":** gra nie kasuje nadwyzki; nadwyzka ponad zapas kupcow idzie co dzien w 2/3
   do pana, w 1/3 do korony. Pan zamku wreszcie dostaje cos ze swojego zamku.
8. **Dwie dziury, na ktorych gracz (i AI) zarabialby na wlasnym wojsku - zamkniete w tej wersji:** "renty od korony" liczone od zoldu zalog
   (kazda moneta zoldu zalogi wracalaby 1.4-2.9 razy) oraz zwrot polowy zoldu za ludzi, ktorzy wydaja ten zold w Twoim wlasnym miescie
   (wracalo 1.10 z monety). Teraz z kazdej monety zoldu wraca do Ciebie najwyzej 0.5-0.7 - wojsko zawsze kosztuje.
9. **Nowa dziura znaleziona w kodzie gry:** zold karawan notabli (ok. 730 karawan) gra zdejmuje co dzien w nicosc - szacunek ok. 150 tys. zl
   dziennie. To najpewniej wiekszosc "reszty niezmierzonej" i powod, dla ktorego gra dosypuje notablom 578 tys. dziennie z niczego.
10. **Najwazniejsze ostrzezenie:** zamkniety obieg nie ma "sprezyny" - kazda nieznaleziona dziura odejmuje zloto na zawsze. W rachunku dziura
    10 tys. dziennie to -3% wojska po 4 latach, 50 tys. to -50%, 150 tys. to upadek gospodarki w 2-3 lata. Dlatego najpierw pomiar, potem
    zatykanie ujsc, a dopiero na koncu wylaczenie dosypki gry (kolejnosc paczek przestawiona, rozdz. 12).
11. **Co zobaczysz po zmianach (rachunek, rozdz. 11):** w wojnie ok. 95 tys. zolnierzy w partiach rodow (dzis ok. 100 tys.), w pokoju ok. 37 tys.
    (38%; Twoja decyzja 35-40%); po wybuchu wojny pelna armia w ok. 2 tygodnie; biednych glow rodow 2-6 zamiast ok. 90; zaleglego zoldu 0;
    zaden skarbiec pusty; miasta kupuja caly plon wsi; Polnoc i Zelazne Wyspy przestaja biedniec.
12. **Pytania do Ciebie (tylko to, co zmienia gre)** - rozdz. 14: czy Twoj rod placi dwor; czy dlug wymarlego rodu przechodzi z ziemia na nowego
    pana; zalogi AI w pokoju o polowe mniejsze; karczmy na start nowej kampanii z pula starych zolnierzy.

---

## 1. Co sie dzieje dzis - zmierzone

### 1.1 Rok na zasadach TOWARY 3 (autotest 364 doby) [P]

Zrodlo: CSV `economy-2026-10-08_08-20-02.csv` (rody bez gracza i bez pseudo-rodow BK "Courtiers of ...", 308 rodow) i linie logu
`Pieniadz swiata`, `Pieniadz swiata (bilans)`, `Korona`, `Zold`, `Ludzie`, `IronBank`, `Skarbce` (skrypty `krytyk\rok_*.py`).

| Doba | 1 | 41 | 92 | 183 | 274 | 364 |
|---|---|---|---|---|---|---|
| wojsko w partiach rodow | 14.5 tys. | 104.4 tys. | 106.1 tys. | 98.2 tys. | 97.7 tys. | 100.4 tys. |
| zloto rodzin rodow | 62.3 mln | 57.6 mln | 56.1 mln | 72.2 mln | 97.4 mln | 116.5 mln |
| glowy rodow < 5 000 zl | 0 | 1 | 27 | 63 | 89 | 92 |
| cale rodziny < 5 000 zl | 0 | 0 | 9 | 14 | 21 | 21 |
| mediana kiesy rodziny / 90. centyl | 230 / 305 tys. | 140 / 398 tys. | 113 / 448 tys. | 95 / 626 tys. | 90 / 883 tys. | 86 / 1 040 tys. |
| skarbce krolestw | 58.1 mln | 56.0 mln | 51.3 mln | 44.7 mln | 44.0 mln | 38.9 mln |
| sakiewki zolnierzy | 0 | 4.8 mln | 8.5 mln | 14.6 mln | 11.6 mln | 17.9 mln |
| kasy miast / zamkow | 17.0 / 7.0 mln | 8.2 / 3.3 mln | 9.9 / 3.7 mln | 10.4 / 4.0 mln | 12.9 / 4.4 mln | 13.2 / 4.7 mln |
| Bank: kapital / bankruci | 5.0 mln / 0 | 4.5 mln / 0 | 1.75 mln / 9 | 0.13 mln / 53 | 0.13 mln / 67 | 0.70 mln / 73 (dlug 6.78 mln, 120 dluznikow) |
| zloto swiata | 188.9 mln | 173.6 mln | 168.9 mln | 181.9 mln | 205.6 mln | 227.0 mln |

Ostatni kwartal (doby 275-364), zl na dobe [P]: z niczego - "zakupy" mieszczan 578 tys. (na starcie 358 tys., rosna przez caly rok), dosypka
regulatora 77 tys., GiveGoldAction z niczego 793 tys.; w nicosc - regulator 350 tys., GiveGoldAction 552 tys., utarg wsi 47 tys., rozliczenia
rodow netto 56 tys.; reszta niezmierzona -144 tys.; **swiat +239 tys. na dobe**. Zold: partie 597 tys., zalogi 185 tys., karawany 20 tys.
Zwrot korony 265 tys. z naleznych 340 tys. (6 skarbcow nie ma dosc). Wojna przez caly rok w 27-31 z 35 krolestw.

### 1.2 Gdzie jest zloto po roku [P]

- Najbogatsze 25 rodow ma 46% zlota rodow (10 najbogatszych 26%): krolowie (Martell, Targaryen, Renly, Joffrey), najemnicy (Brotherhood without
  Banners 4.27 mln z 0.30 mln w 41. dobie, Moon Brothers 3.75, Bright Banners 2.37, Wild Hares 2.18, Stone Crows 1.33), panowie miast.
- Wedlug lenn: rody z miastem 82 - 58.0 mln, glowy < 5 000: 9; rody z zamkiem (bez miasta) 96 - 25.1 mln, glowy < 5 000: 42; rody bez lenna
  130 - 33.4 mln, glowy < 5 000: 40 (21 calych rodzin < 5 000, w tym 3 rody Wolnych Ludzi z dlugiem wobec korony 42-62 tys.).
- Krainy: Polnoc 25 rodow - 2.00 mln, 12 biednych glow, wojsko 9.0 tys.; Zelazne Wyspy 24 - 1.04 mln, 8 biednych; Reach 16 - 5.5 mln,
  8 biednych; Dolina 20 - 2.03 mln, 5 biednych. Ich skarbce: 0 (Polnoc, Reach, Dorzecze, Dolina od ok. doby 274, Zelazne Wyspy od 364).
  Dla porownania KL 28 rodow - 15.9 mln, Dorne 33 - 15.5 mln.

### 1.3 Trzy przyczyny biedy (B1-B3, potwierdzone rokiem)

| Przyczyna | Liczba | Kod [K] |
|---|---|---|
| **Wojsko bez budzetu.** Werbunek staje dopiero przy ok. 3 050 zl w kiesie wodza; limit zoldu BK tnie dopiero ponizej 112.9 tys. kiesy glowy; zaden kod (gra, BK, ROT, StrategicCampaignAI, Armoury) nie porownuje wojska z dochodem | wydatki wojska 105% dochodu rodow (doba 40); po roku zold 802 tys./dobe | `RecruitmentCampaignBehavior.cs:459-466`, `HeroHelper.cs:504-520`; BK `EconomyPatches.cs:117-156` (prefiks `Prefix2Impl` pomija oryginal `ClanVariablesCampaignBehavior.cs:353-387`) |
| **Pieniadz nie wraca do placacego.** Zold zalogi zamku kasuje regulator (zamki bez renty, `PopulationLaw.cs:287`); sakiewki rosna (wydatek tylko przy wyjezdzie z miasta, `MenPurse.cs:173-200`); BetterEconomy pobiera w nicosc (13 kluczy niewpisanych - `better_economy_settings.xml:33, 405, 621` maja dawne wartosci) | rody z zamkiem 27.6 -> 17.3 mln w 40 dob [P] | B1 7.1, 7.6 |
| **Utrzymanie z zapasu, ktory sie konczy, i zloto z niczego trafiajace do bogatych.** Zwrot 50% zoldu z zapasu skarbcow; renty miast z kas trzymanych przez regulator i "zakupy" mieszczan z niczego (`EconomyPatches.cs:579-651` BK, `ItemConsumptionBehavior.cs:61-77`) | 5 skarbcow Westeros puste po 9 miesiacach; Bank pusty po 6 miesiacach [P] | `KingdomTreasury.cs:134-214`, `PopulationLaw.cs:300-313`, `IronBank.cs:101-123` (limit 60 dni dochodu modelu, bez sprawdzenia, czy rod splaci) |

### 1.4 Co z porannej prognozy "dzis" sie nie sprawdzilo

- "Lordowie na zero ok. doby 560", "swiat po roku 170-178 mln" - **nie**: zloto rodow 2 x, swiat 227 mln (zloto z niczego rosnie z czasem).
- "Sakiewki 50-58 mln po roku" - **nie**: 17.9 mln (rosna, ale wolniej).
- "Glowy < 5 000 po roku 76-153" - **tak** (92); po 4 latach [S] 90-130: liczba stoi na ok. 88-92 od doby 250, rosnie dalej tylko tam, gdzie
  pustoszeja kolejne skarbce (Dragonstone 0.50 mln, Qohor 0.29 mln, Mopatis 0.15 mln w 364. dobie).
- "Bank: 30-60 bankructw w 1. roku, potem kapital wyczerpany" - **tak**, nawet szybciej (73; kapital 0.13 mln juz w 183. dobie).
- "Skarbce puste ok. doby 370" - **czesciowo**: razem 38.9 mln, ale wielkie krolestwa Westeros puste juz ok. doby 274.

---

## 2. Krytyka porannego projektu - co poprawiono i dlaczego

| # | Problem w porannej wersji | Dowod | Poprawka (w tym pliku) |
|---|---|---|---|
| K1 | Prognoza "dzis" z 40 dob | rok zmierzony (1.1) - zloto rodow 2 x, a nie zero | kolumna "dzis" z pomiaru roku, rozdz. 1 i 11 |
| K2 | **"Renty od korony wedlug zoldu zalog" = maszyna do pieniedzy.** Zold zalogi idzie do kasy wlasnego zamku/miasta (zawor oddaje 2/3), a korona oddaje cala nadwyzke wedlug zoldu zalog: w pokoju 2 zl na kazda 1 zl zoldu zalog (rachunek autora: korona 200 tys., zalogi 100 tys.) | `gracz.py`: zaloga gracza 200-600 ludzi wraca **1.43-1.48** z monety w wojnie, **2.54-2.88** w pokoju | renty od korony wedlug lenn (miasto 3, zamek 1, wies 0.25) - nie da sie ich zwiekszyc wydatkiem (7.1) |
| K3 | **Zwrot 50% zoldu + zawor wlasnego miasta > 1.** Ludzie partii stojacej przy wlasnym miescie wydaja tam zold (markietani 85%, wies 15%), pan dostaje 2/3 zaworu i 20% renty wsi, a korona oddaje polowe zoldu | `gracz.py`: partia 300-1000 ludzi przy wlasnym miescie w wojnie wraca **1.10** z monety | zwrot liczony od zoldu minus polowa tego, co ludzie tego rodu wydali wczoraj w jego wlasnych osadach (rozszerzenie `TownPurse.HomePart` z K6 - ta sama zasada co przy zalogach); po poprawce 0.52-0.68 z monety (7.1) |
| K4 | **Brak zapomogi gry dla biednych rodow.** Vanilla `AddIncomeFromKingdomBudget` (`DefaultClanFinanceModel.cs:517-528`, wolane `:155-158`): rod z kiesa glowy < 30 000 dostaje ze skarbca 500-2 000 dziennie, x2 gdy skarbiec > 1 mln, x2 dla krola. Rezerwa wojny projektu (20 000) lezy PONIZEJ tego progu - prawie kazdy maly rod bierze zapomoge co dzien | `sim_krytyk.py`, wariant "jak w projekcie": po 4 latach 6 skarbcow pustych, skarbce 30 mln zamiast 43 | zapomoga tylko z biezacych wplywow korony, po zwrocie zoldu (7.1) |
| K5 | **Brak w tabeli dwoch przeplywow gry:** (a) dochod "za tier" rodow bez lenna poza krolestwem i najemnikow - z niczego (`DefaultClanFinanceModel.cs:133-136`, tier x 80, najemnik x 120 dziennie); (b) podatek BK 0.1% dziennie od kiesy glowy ponad 100 000 do skarbca (`EconomyPatches.cs:211-219`, prawdziwy przeplyw) | kod | wiersze 18 i 19 tabeli 4.1 |
| K6 | **Nowe ujscie w nicosc: zold karawan notabli.** `NotablesCampaignBehavior.ManageCaravanExpensesOfNotable` (`:309-330`, co dobe dla kazdego notabla z `DailyTickHero :290`): zold karawany schodzi z jej kasy (`PartyTradeGold -= totalWage`, `:317`) albo z kiesy notabla (`notable.Gold -= num2`, `:322`) - bez odbiorcy; zaden mod tego nie lata. Opis paczki 123 widzial tylko doplate do 5 000 (przelew). Karawan jest 878, z tego 152 lordow (ich zold 33 tys./dobe = 219 na karawane) - zostaje ok. 726 karawan notabli | szacunek [S] 120-160 tys./dobe; to zgadza sie z reszta niezmierzona swiata (-144..-218 tys.) i tlumaczy, czemu gra dosypuje notablom 578 tys. dziennie z niczego | wiersz 1b tabeli 4.1: zold karawan notabli do sakiewki ludzi karawany (MenPurse, wydaja w miastach) - w 161 pomiar, w 164 zmiana; pasma notabli NIE zamykac na kasach miast przed ta poprawka |
| K7 | **Obieg zamkniety nie ma sprezyny.** W rachunku autora suma zlota stala "co do grosza", wiec kazda nieznaleziona dziura byla niewidoczna. Poziom pieniadza w obiegu jest obojetny: co zniknie, nie wraca nigdy | `sim_krytyk.py`, stala dziura w kasach miast przez 4 lata (zestaw koncowy): 10 tys./d -> wojsko 94.9 -> 92.4 tys.; 25 tys. -> 85.2; 50 tys. -> 48.2; 150 tys. -> 0 (upadek w 2-3 lata) | prog akceptacji reszty swiata < 10 tys. na dobe (srednia 28 dob); tryb 1 regulatora (dosypka do zapasu kupcow, liczona) zostaje jako bezpiecznik, dopoki prog nie jest spelniony (rozdz. 9) |
| K8 | **Kolejnosc paczek usuwala zrodla przed zatkaniem ujsc.** Grupa C (K5, K6, dwor, K7, markietani) przed 164 zabiera "zakupy" z niczego (390-578 tys./d), a zostawia ujscia: nadwyzka notabli 367 tys., awanse, ochotnicy z mapy, statki, zold karawan, karawany notabli | bilans B1 doby 31-40: po samej grupie C swiat -150..-300 tys./d | najpierw 164 (pieniadz), potem K5/K6 (rozdz. 12) |
| K9 | **Zapas koron 1.5 mln x 29 = 43.5 mln lezy na zawsze** (25% zlota w obiegu). W zamknietym obiegu wielkosc wojska wyznacza ilosc pieniadza w obiegu i to, jak dlugo kazdy go trzyma | rachunek: zapas korony 0.5 mln -> +5-6 tys. wojska; rezerwa wojny 20 dni zamiast 30 -> +3 tys. | `CrownReserveGold` 500 000, `WarReserveDays` 20 (7.1, 6.4) |
| K10 | Pokoj 28% dochodu na zold daje 40-42% stanu wojny | rachunek | `PeaceWageShare` 0.25 -> 38-40% (decyzja Jeffa 35-40%) |
| K11 | Zapas wojny dzielony 0.7 zold / 0.3 sprzet: rody na suficie partii wydaja go na sprzet - ok. 630 tys. dziennie, ok. 2.5 x dzisiejsze zakupy AI (ZakupyAI 145 tys. + najemnicy 85 + ochotnicy 27) | rachunek | 0.8 / 0.2 (ok. 545 tys.); budzet sprzetu niewydany przez 30 dni przechodzi na dwor - gdy towaru braknie, pieniadz krazy dalej zamiast lezec w kiesie |
| K12 | **Kiesa glowy a kiesa rodziny.** Zold placi glowa (gra dopisuje saldo glowie, `ClanVariablesCampaignBehavior.cs:413-414`), a po roku 92 glowy maja < 5 000 przy 21 rodzinach < 5 000 - zloto czlonkow lezy bezczynnie | rok [P] | budzet liczy G z kies rodziny, a dorosli czlonkowie AI oddaja glowie raz na dobe brak dnia (nigdy ponizej 5 000 w swojej kiesie) |
| K13 | Premia konnych 160 (zold +7.5-12.5%) nieuwzgledniona | opis 160 | rachunek z zoldem x1.10: wojna -2%, pokoj -7% ludzi |
| K14 | Dochod staly D na starcie kampanii: dochod modelu w 1. dobie jest sztucznie wysoki (CSV doba 1: 1.52 mln dziennie dla wszystkich rodow) | CSV | D(0) = kiesa rodziny / 60 (zapas pokoju), potem srednia 28 dob |
| K15 | Rachunek autora: jedna kasa miast swiata | - | rachunek krytyka: osobne kasy miast, zamkow, wsi, sakiewki i skarbce dla kazdego z 30 krolestw, z przeciekiem 20% (i 40% w probie) - wynik ten sam rzad wielkosci, zadna kraina nie biednieje |

Co sie potwierdzilo (sprawdzone w kodzie i logach, rozdz. 15): przyczyny B1-B3, wszystkie wskazania plik:linia w porannej tabeli (przesuniete
tylko numery linii `LevyGold.cs` po 160), liczby FAKTOW (wojsko 14 387 -> 101 144 - doba 1 przed wystawieniem partii; 14 rodow < 5 000 = 12 pseudo-rodow
BK + 2 prawdziwe), punkty zaczepienia budzetu (postfiks na `MakeClanFinancialEvaluation` biegnie takze, gdy prefiks BK pomija oryginal; limit zalog
w `UpdateClanSettlementsPaymentLimit :458`, wolane `:438` po saldzie dnia `:413`; decyzja o nowej partii `HeroSpawnCampaignBehavior.ConsiderSpawningLordParties :175-201`;
dezercja z limitu zoldu `DefaultPartyDesertionModel.cs:50-75` do 20 ludzi na partie dziennie; awans tylko w limicie `PartyUpgraderCampaignBehavior.cs:112`).

---

## 3. Zasady (jedna zasada na jedno zjawisko)

| # | Zasada | Uzasadnienie |
|---|---|---|
| Z1 | Kazda moneta ma platnika i odbiorce: zadne zloto z niczego, zadne w nicosc. | CLAUDE.md zasada 0, decyzja Jeffa (system szczelny). |
| Z2 | Kazdy posiadacz, ktory nie jest rodem (miasto, zamek, wies, sakiewka, notabl, skarbiec, Bank), oddaje dalej to, co ma ponad swoj zapas, w tempie liczonym od nadwyzki (zawor). | W zamknietym obiegu, gdy nikt poza rodami nie gromadzi bez konca, dochody rodow w stanie ustalonym rownaja sie ich wydatkom. |
| Z3 | Rod wydaje dochod biezacy (srednia 28 dob); zapas tylko w wojnie i powoli; w pokoju odbudowuje go do 60 dni dochodu. | Decyzja Jeffa "z biezacego zysku"; wlasna kampania pana trwala 40-80 dni z wolnej czesci dochodu [H]. |
| Z4 | Pan placi druzyne, zalogi i dwor; wojne w polu wspolfinansuje korona z biezacych wplywow. | Indentures Edwarda III [H]; decyzja Jeffa 05.10. |
| Z5 | Dwor pana to jego najwiekszy wydatek i glowny kanal, ktorym pieniadz wraca do miast; zastepuje "zakupy mieszczan z niczego". | Lancaster 1313-14: wydatki dworu 72% dochodu, w tym swita ok. 20% [H]. |
| Z6 | Ludzie w obiegu: zwolniony wraca do wsi albo karczmy (ze swoim sprzetem i koniem), dezerter do puli wyrzutkow. | Decyzje Jeffa 05.10 i 08.10. |
| Z7 | Dlug nie znika i nie zabiera lenna. | Decyzje Jeffa 05.10 i 07.10 (wariant A, odsetki zamrozone, bez umorzenia). |
| **Z8** | **Nikt nie zarabia na wlasnym wydatku:** z monety, ktora pan wyda (zold, zaloga, dwor, sprzet), do niego samego wraca kazda droga razem (zawor jego osad, zwrot korony, renty od korony) najwyzej 1 moneta - i to tylko w przypadku skrajnym. Czego pan nie moze zmienic wydatkiem (lenna), moze byc podstawa podzialu; czego moze (zold zalog), nie. | Krytyka K2-K3; gracz nie dostaje pieniedzy za darmo. |
| **Z9** | **Szczelnosc mierzymy, zanim zdejmiemy siatke:** dosypka gry ponizej zapasu kupcow (tryb 1, liczona) zostaje, dopoki reszta niezmierzona swiata nie spadnie ponizej 10 tys. na dobe przez 28 dob. | Krytyka K7-K8: zamkniety obieg nie wraca sam do rownowagi po ubytku. |

---

## 4. Kazde ujscie i kazde zrodlo -> prawdziwy posiadacz

### 4.1 Tabela glowna (kolejnosc wedlug wielkosci; zl na dobe; doby 31-40 [P] z B1, rok w nawiasie, gdy znany)

| # | Strumien | Ile | Dzis - kod [K] | Po zmianie: platnik -> odbiorca | Paczka |
|---|---|---|---|---|---|
| 1 | **Notable: z niczego 578 tys. / w nicosc 367 tys.** (pasmo 4 500-10 500) | 945 tys. obrotu | `NotablePowerManagementBehavior.cs:47-60` (500 zl <-> 1 wplywu, `GiveGoldAction(null, ...)`) | wymiana na wplyw zostaje; druga strona to kasa osady notabla: nadwyzka ponad 10 500 -> kasa jego miasta / kiesa wsi; dosypka ponizej 4 500 -> z tej samej kasy, najwyzej z nadwyzki ponad zapas kupcow (wies: ponad 1 000); brak = notabl zostaje biedny (wplyw stoi). **Dopiero po wierszu 1b i po pomiarze 161** | 161 -> 164 |
| 1b | **NOWE: zold karawan notabli w nicosc** (ok. 726 karawan) | ok. 120-160 tys. [S] (czesc reszty -144..-218) | `NotablesCampaignBehavior.cs:309-330` (`:317` kasa karawany, `:322` kiesa notabla; doplata do 5 000 `:326-328` to przelew) | zold -> sakiewka ludzi tej karawany (MenPurse), wydaja w miescie jak zolnierze; doplata do 5 000 bez zmian | 161 (licznik) -> 164 |
| 2 | **"Zakupy" mieszczan i zalog** (towar z polki znika, kasa dostaje cene z niczego) | 390 tys. (rok, ostatni kwartal: 578 tys.) | BK `EconomyPatches.cs:579-651`, vanilla `ItemConsumptionBehavior.cs:61-71` | cofniete (K5/K6, gotowe); w zamian **dwor rodu** (6.1) | 110, 111', 162 |
| 3 | **GiveGoldAction w nicosc - reszta** | 323 tys. | BEE `CastleEconomyCampaignBehavior.cs:358, 1074`, `VillageInvestment...:375`, `VillageDevelopment...:625`; awanse `PartyUpgraderCampaignBehavior.cs:139-151` (`ApplyEffects`); ochotnik z mapy `RecruitmentCampaignBehavior.cs:628-630`; statki NavalDLC `ChangeShipOwnerAction.cs:29-42` | BEE (105-185 tys.): 13 kluczy (`tools/bee/zamknij-ujscia-bee.ps1`); awans -> sakiewka ludzi tej partii; ochotnik z mapy -> kiesa najblizszej wsi; statek -> kasa miasta portowego | BEE, 164 |
| 4 | **Reszta niezmierzona [R]** | -218 tys. (srednio -150; rok -144) | rozbicie B1 7.3-7.4 + wiersz 1b | najpierw zmierzyc (4.2), potem kazdy skladnik jak w tej tabeli | 161 -> 164 |
| 5 | **Regulator kasy: kasuje 194 tys., dosypuje 71 tys.** (rok: 350 / 77) | 265 tys. obrotu | `DefaultSettlementEconomyModel.cs:75-79` (0.25 x (10 000 + 12 x dobrobyt - kasa)), wolany `ItemConsumptionBehavior.cs:73-77`; zamki przez BK `BKSettlementBehavior.cs:673-700` | K5 (zamki) i K6 (miasta): zero kasowania; zawor 7% nadwyzki dziennie: 2/3 pan, 1/3 skarbiec; dosypka trybu 1 (liczona) zostaje do spelnienia Z9, potem bezpiecznik z udzialu korony (7.1) | 110, 111', 114, 165 |
| 6 | **GiveGoldAction z niczego - reszta** (rosnie z wojna) | do 183 tys. | jency `SellPrisonersAction.cs:83, 88`; oblezenie `SiegeAftermathCampaignBehavior.cs:164, 168`; turniej BK `BKTournamentManager.cs:26`; jency z rabunku BK `BKRaidCaptureBehavior.cs:390` | jeniec sprzedany w miescie: placi kasa miasta (najwyzej jej nadwyzka ponad zapas; czlowiek przechodzi do ludnosci miasta w ksiedze ludzi 108); lup z oblezenia: z kasy zdobytego miasta (najwyzej 25% nadwyzki); nagroda turnieju BK: z kasy miasta-gospodarza | 164 |
| 7 | **Sakiewki zolnierzy rosna** (zbiornik bez wyjscia) | +163 tys. (rok: 17.9 mln) | `MenPurse.cs:173-200` (wydatek tylko przy wyjezdzie z miasta) | **markietani**: w polu ludzie wydaja 10% sakiewki dziennie: 85% kasa najblizszego niewrogiego miasta, 15% kiesa najblizszej wsi; zwolnieni zabieraja swoja czesc | 163 |
| 8 | **Skarbce zjadaja zapas** (zwrot 50% z zapasu) | -170 tys. | `KingdomTreasury.cs:134-214` | zwrot tylko z biezacych wplywow + 1/180 dziennie z zapasu ponad 0.5 mln; podstawa zwrotu bez wydatkow wlasnych ludzi we wlasnych osadach (K3) | 165 |
| 9 | **"Zniklo" 15% utargu taborow** | 55 tys. (doba 40: 70) | BK `EconomyPatches.cs:1096-1099` (`PartyTradeGold = 0`) | K7 (gotowe): cale 30% po podatku zostaje w kiesie wsi | 112 |
| 10 | **Zold karawan lordow** (SoldierPay zostawia go grze) | 33 tys. | `SoldierPay.cs:347` | do sakiewki ludzi karawany (MenPurse) - ta sama regula co 1b | 164 |
| 11 | **Trybut / reparacje** (jednorazowo -1.42 mln w dobach 28-31) | szczyt | `DefaultClanFinanceModel.cs:342-358` (platnik w nicosc), `:540-575` (odbiorca z niczego) | korona -> korona; rody placa koronie najwyzej 50% D dziennie, reszta to dlug krolestwa; przed 165 sprawdzic zrodlo -2.2 mln (Diplomacy `ScalingWarReparationsGoldCostMultiplier`?) | 165 |
| 12 | **"Debts" - splata dlugu wobec korony** | do 24 tys. | `DefaultClanFinanceModel.cs:219-230` (cala kiesa w nicosc) | splata do skarbca, tylko z kiesy ponad 38 000; dlug do naszej ksiegi z wierzycielem "korona" | 168 |
| 13 | **Zakup lorda we wsi - 100% "podatku"** | 11 tys. | `SellItemsAction.cs:70, 86`, `DefaultSettlementTaxModel.cs:21` (`SettlementCommissionRateVillage` 1.0) | K7 (gotowe): zaplata zostaje w kiesie wsi | 112 |
| 14 | **Lup z cial dla AI** (zloto zamiast sprzetu) | 10-25 tys. [S] | `MapEvent.cs:1850-1867` (`PartyTradeGold += num`) | z sakiewek poleglych i pojmanych ludzi strony przegranej; pusta sakiewka = 0 | 164 |
| 15 | **BK HandleMarketGold** (1% kasy ponad limit w nicosc) | male | BK `BKSettlementBehavior.cs:500-527` | K6 (gotowe): galaz pominieta; galaz "notabl doplaca 1 000 pustej kasie" (przelew) zostaje | 111 |
| 16 | **BK: porty, kopalnie, konwoje ludnosci, HandleExcessFood** | czesc z 40-50 tys. | `BKBuildingsBehavior` RunPorts / RunMines; `BKPartyBehavior.cs:845-858` | zmierzyc; towar i zloto tylko przez kase osady | 161, 164 |
| 17 | **BK: wydatki lordow bez odbiorcy** (tytuly, pasowanie, dwor BK, dylematy, przeniesienie dworu) | nieznane | `TitleManager.cs:562, 586, 620, 727`; `BKKnighthoodBehavior.cs:234`; `CourtGrace.cs:381`; `BKBuildingsBehavior.cs:187-188`; `BKDilemmaBehavior.cs:405`; `MoveCourtDecision.cs:110` | oplaty -> skarbiec; material na koniec budowy -> kasa osady | 161, 164 |
| 18 | **NOWE: dochod "za tier" rodow bez lenna** (poza krolestwem albo najemnicy) | kilka tys. [S] | `DefaultClanFinanceModel.cs:133-136` (tier x 80, najemnik tier x 120, z niczego) | najemnik: placi skarbiec krolestwa, ktoremu sluzy (z biezacych wplywow); rod bez krolestwa: 0 (zyje z lupu i kontraktow) | 165 |
| 19 | **NOWE: podatek BK od bogatych** (0.1% dziennie kiesy glowy ponad 100 000 -> skarbiec) | ok. 25-45 tys. [S] | BK `EconomyPatches.cs:211-219` | zostaje (prawdziwy przeplyw); wpisac do linii "Obieg" jako wplyw korony | 161 |
| 20 | **NOWE w projekcie: zapomoga gry dla biednych rodow** (skarbiec -> rod z kiesa glowy < 30 000) | 10-30 tys. [S] | `DefaultClanFinanceModel.cs:155-158, 517-528` | tylko z biezacych wplywow korony po zwrocie zoldu (7.1); nigdy z zapasu | 165 |
| 21 | **Rada ("Council wages")** | kilka tys. | vanilla model finansow | zmierzyc odbiorce; gdy w nicosc - do czlonkow rady | 161 |
| 22 | **Dar startowy kas** (12 mln, doby 2-10) | jednorazowo | `Town.cs:485` + BK `BKCampaignStartBehavior.cs:232-238` | K5/K6 (gotowe): przyciecie w 1. dobie | 110, 111 |
| 23 | **Kapital Banku rosnie z odsetek** | maly | `IronBank.cs` | zysk ponad kapital startowy -> kasa Braavos 1/180 dziennie | 168 |

Co juz jest szczelne (sprawdzone w B1): Bank (`IronBank.cs:173, 246, 264`), Spoils of War (128), lup zlota z bitew (0), GrandTourney, paser (106),
zold do sakiewek i kas osad (107), wplaty LevyGold do notabli i miast, skarbiec bez dosypki gry (`KingdomTreasury.Transpiler` zeruje 1000 / 100 000 /
200 000 / 400 000 w `DailyTickClan`, `ClanVariablesCampaignBehavior.cs:414-423`).

### 4.2 Pomiar reszty (paczka 161, sam log)

Kazdy pomiar to "okno": stan kies posiadaczy tuz przed i tuz po wywolaniu metody (jak dzis okno taboru w `MoneyLedger.cs:183-225`).

| Kandydat | Metoda do okna | Gdzie w logu |
|---|---|---|
| **zold karawan notabli** (1b) | `NotablesCampaignBehavior.ManageCaravanExpensesOfNotable` (prefiks/postfiks: suma kas karawan i kies notabli) | "Obieg: notable i karawany" - zold w nicosc, doplaty do 5 000 |
| kasa miasta placi warsztatom / warsztat placi za wsad | BK `EconomyPatches.cs:835-839`, `:873-881` | "Przeplywy osad (kasy miast)" |
| rzemieslnicy BK | BK `WorkshopData.cs:79-89` | j.w. |
| prowizja 10% od sprzedazy partiom | `SellItemsAction.cs:86` | j.w. + "licznik cel" |
| konwoje ludnosci, porty, kopalnie, HandleMarketGold, HandleExcessFood | BK (wiersze 15-16) | "Obieg: BK" |
| wydatki lordow BK bez odbiorcy | wiersz 17 | "Obieg: lordowie BK" |
| awanse, ochotnik z mapy, statki; jency, oblezenia, turnieje BK; lup z cial | wiersze 3, 6, 14 | "Pieniadz swiata (bilans)" - rozbicie na przyczyny |
| dochod "za tier", zapomoga gry, podatek BK | wiersze 18-20 | "Obieg: korona" |
| zakupy zaopatrzenia majatkow BK (rycerze ok. 220 zl dziennie) | `BKVillageSupplyAutoBehavior.cs:316-349` | "Budzet rodow": "majatki BK" |

Cel pomiaru: reszta swiata i reszta kas miast ponizej 10 tys. dziennie (srednia 28 dob). Dzis -144..-218 tys.

---

## 5. Obieg zoldu - petla po zmianie

### 5.1 Petla (wojna, stan ustalony, zestaw koncowy z premia konnych 160, rok 1; tys. zl na dobe [S], `krytyk\wynik-krytyk.txt`)

```
            zold partii 475                         zycie w miescie i markietani 475
   RODY ---------------------> SAKIEWKI LUDZI ----------------------------------------> KASY MIAST (85%) i KIESY WSI (15%)
    |   zold zalog 195, dwor 373, sprzet i werbunek 545, budowy 22 ----------------------> KASY MIAST I ZAMKOW
    |<------ podatek wsi 70% utargu: 262 <----- TABORY WSI <---- zakup plonu: 374 (100% oferowanego) <--+
    |<------ renta wsi 20%/dobe: 176 <--------- KIESY WSI (30% utargu + markietani)                    |
    |<------ zawor miast 2/3 z 684 + zamkow 2/3 z 181 <--------------------------------------------------+ zawor 7%/dobe nadwyzki
    |<------ skup lupu i jencow: 182 <--------- kasy miast
    |<------ korona: zwrot 50% zoldu partii (228) + renty od korony wedlug lenn (281) + zapomoga (18)
                     <--- 1/3 zaworow (288) + danina wojenna i clo (117) + powinnosci (39) + podatek BK (42)
```

Rody dostaja ok. 1.72 mln i wydaja ok. 1.65 mln dziennie (roznica to odbudowa zapasow w 1. roku). Z niczego 0, w nicosc 0 (w rachunku).

### 5.2 Czasy obiegu

| Posiadacz | Regula | Czas | Uzasadnienie |
|---|---|---|---|
| sakiewka ludzi | w miescie wydaja wszystko poza rezerwa na naprawy (jest); w polu 10% dziennie | ok. 10 dob zoldu | zolnierz trzyma kilka-kilkanascie dni zoldu, nie miesiace |
| kasa miasta i zamku | zawor 7% nadwyzki dziennie | polowa nadwyzki po 10 dobach | ta sama stawka co dzisiejsza renta (K5/K6) |
| kiesa wsi | renta 20% dziennie (jest) | ok. 5 dob | `PopulationRentMaxShare` 0.2 (wpis 49) |
| skarbiec | wyplaca biezace wplywy co dobe; zapas ponad 0.5 mln po 1/180 | 1 doba (+ zapas) | korona nie miala wielkich zapasow, wojne placila z biezacych podatkow i pozyczek [H] |
| rod | D = srednia 28 dob; zapas 60 dni D (pokoj), rezerwa 20 dni D (wojna) | 20-60 dob | rozdz. 6 |
| notabl | pasmo 4 500 - 10 500, druga strona kasa osady | - | wiersz 1 |

### 5.3 Parametry obiegu

| Parametr (klucz MCM) | Wartosc | Uzasadnienie |
|---|---|---|
| zapas kupcow miasta / zamku (`TownPurseFloorGold` + `PerProsperity`, `CastlePurseFloorGold`) | 10 000 + 12 x dobrobyt (K5/K6) | dzisiejszy cel regulatora - masa pieniadza w kasach sie nie zmienia. |
| zawor (`TownRentShare`, `CastleDuesShare`) | 7% nadwyzki dziennie | ta sama stawka co renta - jedna zasada. |
| podzial zaworu (`TownRentLordShare`; zamek - 114) | 2/3 pan, 1/3 skarbiec (K6 bez zmian) | przy poprawkach K2-K3 z monety wydanej we wlasnym miescie wraca najwyzej 0.68 (Z8); w rachunku podzial 1/2 nic nie zmienia w bankructwach. |
| markietani (`CampSpendPercent`) | 10% sakiewki dziennie w polu | dzis sakiewki rosna 163 tys. dziennie; przy 10% stoja na ok. 10 dobach zoldu. |
| podzial markietanow (`CampTownShare`) | 85% miasto, 15% wies | kupiec z miasta (piwo, sukno, podkowy), chlop sprzedaje zywnosc i pasze. |
| renta wsi (`PopulationRentMaxShare`) | 20% dziennie (bez zmian) | wieksza oprozniala wsie (wpis 49). |
| notabl wsi: dosypka tylko z kiesy wsi ponad | 1 000 | srednia kiesa wsi ok. 500 zl - dosypka rzadka i z prawdziwych pieniedzy. |
| lup z oblezenia | najwyzej 25% nadwyzki kasy zdobytego miasta | miasto nie zostaje bez kupcow, zwyciezca dostaje prawdziwe zloto. |
| jeniec sprzedany w miescie | cena gry, platnik kasa miasta, najwyzej jej nadwyzka | kupcy biora jenca do pracy; pusta kasa nie kupuje. |
| bezpiecznik kas miast (165, po Z9) | korona dopelnia kase miasta swojego krolestwa ponizej 50% zapasu, najwyzej 2% zapasu dziennie | dosypka z prawdziwym platnikiem; limit dzienny, zeby korona nie oddala zapasu jednemu miastu. |

---

## 6. Budzet rodu (wojsko AI wedlug dochodu)

### 6.1 Regula (raz na dobe dla kazdego rodu AI, w dziennym rozliczeniu rodu gry)

Oznaczenia: **D** = dochod staly: srednia 28 dob wszystkich prawdziwych wplywow do kies czlonkow rodu z zewnatrz rodu (podatek wsi, renty i zawory,
zwrot i renty od korony, zapomoga, skup lupu i jencow, kontrakty najemnikow, dodatnie saldo modelu gry), bez pozyczek i wyprzedazy; na starcie
kampanii albo po wlaczeniu paczki D(0) = G / 60 (K14). **G** = kiesy rodziny (glowa + dorosli czlonkowie). **R** = rezerwa wojny = max(20 000; 20 x D).

| Pozycja | Pokoj | Wojna |
|---|---|---|
| pulap zoldu (partie + zalogi) | 0.25 x D | 0.55 x D + 0.8 x (G - R) / 45, gdy G > R |
| dwor (do kasy siedziby) | 0.35 x D | 0.20 x D |
| sprzet i werbunek (pulap) | 0.17 x D | 0.22 x D + 0.2 x (G - R) / 45, gdy G > R |
| budowy (BuildFunding, jak dzis) | 0.10 x D | 0 (poza murami - jak dzis) |
| powinnosci do korony (jak dzis) | 2% D | 3% D |
| reszta | do zapasu, dopoki G < 60 x D; gdy G >= 60 x D - polowa na dwor, polowa na budowy | - |
| zapas ponad 120 x D + 50 000 | po 1/180 dziennie na dwor i budowy | j.w. |
| oszczedzanie przy biedzie | G < 60 x D: udzialy x (1 - 0.1 x (1 - G / 60D)) | G < R: udzialy x (0.8 + 0.2 x G / R) |
| niewydany budzet sprzetu | po 30 dniach przechodzi na dwor | towaru moze nie byc - pieniadz ma krazyc, nie lezec (K11) |
| kiesa rodziny | dorosly czlonek AI oddaje glowie raz na dobe tyle, ile brakuje glowie na dzisiejszy budzet, nie schodzac ponizej 5 000 | zold placi glowa (K12) |

Wykonanie w grze (miejsca sprawdzone w dekompilacji [K]):
- **pulap zoldu partii** - postfiks na `ClanVariablesCampaignBehavior.MakeClanFinancialEvaluation` (`:353`, wolana z `DailyTickClan :409-412` tylko dla rodow AI;
  BK zastepuje ja prefiksem `Prefix2Impl`, `EconomyPatches.cs:117-156`, ktory zwraca false - postfiks Harmony biegnie mimo to): `SetWagePaymentLimit` kazdej partii
  = jej czesc pulapu (wedlug dzisiejszego zoldu partii, partia glowy x1.5 jak BK). Dzialaja wtedy gotowe hamulce gry: brak werbunku ponad limit i brak awansow
  ponad limit (`PartyUpgraderCampaignBehavior.cs:112`). Limit dziala od nastepnego werbunku (saldo dnia `:413` liczone po nim);
- **pulap zalog** - postfiks na `UpdateClanSettlementsPaymentLimit` (`:458-475`, wolane `:438`): `SetGarrisonWagePaymentLimit` = min(limit gry; w pokoju 50%);
  zalogi najwyzej 60% pulapu zoldu;
- **nowa partia** - prefiks na `HeroSpawnCampaignBehavior.ConsiderSpawningLordParties` (`:175-201`; BK lata w nim `GetBestAvailableCommander`): nie, gdy wolne
  miejsce w pulapie < 30 ludzi x sredni zold;
- **werbunek** - prefiks na `RecruitmentCampaignBehavior.CheckRecruiting` (obok prefiksu MountedWage 160, bez zmiany kontekstu zoldu): partia AI werbuje tylko
  w granicach pulapu sprzetu i werbunku (koszt = dni zoldu, `RecruitCost`);
- **sprzet** - `AiGear.cs:192`: budzet wizyty = niewydany pulap sprzetu z dni od ostatnich zakupow (najwyzej 30 dni) zamiast 25% kiesy;
- **budowy** - `BuildFunding.cs:158` bez zmian (juz 10% dochodu), dochod = D;
- **dwor** - nowy przelew raz na dobe (glowa -> kasa siedziby `Clan.HomeSettlement`; gdy to wies - jej miasto albo zamek; rod bez lenna - miasto, w ktorym jest
  glowa, albo najblizsze miasto jego krolestwa), w ksiedze pieniadza jako przeplyw;
- **kiesa rodziny** - przelew czlonek -> glowa przed rozliczeniem rodu (tylko AI; rod gracza bez zmian).

### 6.2 Zwolnienia zamiast dezercji z limitu

- Gdy zold partii przekracza 1.10 x jej pulapu przez 3 doby: codziennie zwalnia 15% nadwyzki (najmniej 1 czlowieka).
- Kolejnosc: najpierw najemnicy z karczmy (najdrozsi; konni x1.5 zoldu - 160) -> do puli weteranow karczmy najblizszego miasta, ze sprzetem i koniem;
  potem najnizszy tier poboru -> do wsi pochodzenia (ksiega ludzi 108; gdy nieznana - najblizsza wies kultury czlowieka), sprzet z nim (`MusterOut.cs`).
- Czesc sakiewki partii (proporcjonalnie do liczby zwolnionych) idzie z nimi: do kiesy wsi albo kasy miasta karczmy.
- Dezercja gry z powodu limitu zoldu (`DefaultPartyDesertionModel.cs:50-75`, do 20 ludzi na partie dziennie) dla partii AI z budzetem = 0 (zastepuje ja zwolnienie);
  dezercja z morale i glodu bez zmian (`DesertionLaw`). `WarLedger` zostaje jako hamulec awaryjny.
- BK `DismissParties` (`BKClanBehavior.cs:1160-1230`) zostaje; ludzie rozwiazanych partii tym samym kanalem co zwolnieni (przy 108 nie liczyc drugi raz -
  BK oddaje ich do swojej ludnosci `UpdatePopFromSoldiers`).

### 6.3 Co sie dzieje w grze

| Sytuacja | Co robi rod |
|---|---|
| pokoj | druzyna i zalogi za 25% dochodu (ok. 37 tys. w partiach swiata), dwor 35%, budowy, odklada do 60 dni dochodu |
| wybuch wojny | pulap rosnie do 55% dochodu + 80% z 1/45 zapasu ponad rezerwe dziennie; pelna armia w ok. 14 dob (rachunek: 36 -> 55 tys. w 3 doby, 74 tys. w 7, 97 tys. w 14) |
| dluga wojna | zapas schodzi do rezerwy 20 dni dochodu (najmniej 20 000); armia stoi na tym, co daje dochod i korona |
| koniec wojny | pulap spada do pokojowego; nadwyzka zwalniana 15% dziennie - z ok. 95 do ok. 37 tys. w ok. 2-3 tygodnie |
| bieda (G < R) | wszystkie wydatki proporcjonalnie mniejsze (do 80%) |
| utrata lenna | D spada przez 28 dob, pulap razem z nim, armia zwalniana stopniowo |

Uwaga z rachunku: w wojnie 171 z 324 rodow stoi na fizycznym suficie partii (limity gry i BK - dzisiejsze plateau), a nie na budzecie. Budzet tnie glownie
panow samych zamkow (47 ze 119 ponizej sufitu) i rycerzy. Dlatego skala wojny prawie nie zalezy od udzialow zoldu, a mocno od ilosci pieniadza w obiegu (K9).

### 6.4 Parametry wojska i budzetu

| Parametr (klucz MCM, po angielsku) | Wartosc | Uzasadnienie |
|---|---|---|
| ClanBudgetEnabled | true | wylacznik paczki - wylaczony = gra jak przed paczka. |
| StableIncomeDays | 28 | 4 tygodnie: wygladza lup z pojedynczych bitew, na utrate wsi albo nowa wojne reaguje w miesiac. |
| StableIncomeStartDays | 60 (D(0) = G / 60) | dochod modelu w 1. dobie kampanii jest sztucznie wysoki (CSV: 1.52 mln dziennie); zapas pokoju to 60 dni dochodu. |
| PeaceWageShare | 0.25 | daje pokoj = 38-40% wojska wojny (decyzja Jeffa 05.10: 35-40%); historycznie swita i straz 20-27% dochodu (Lancaster 1313-14). |
| WarWageShare | 0.55 | tyle dzis rody wydaja w wojnie na zold (58%); 0.60 daje tylko +2 tys. ludzi, bo wiekszosc rodow stoi na suficie partii. |
| HouseholdSharePeace / War | 0.35 / 0.20 | dwor bez swity ok. 50% dochodu (Lancaster 72% z swita 20%); dzisiejsze "zakupy z niczego" to 37% dochodu rodow; w wojnie pan je z zapasow armii. |
| GearShare Peace / War | 0.17 / 0.22 | dzis w wojnie sprzet + najemnicy + ochotnicy = 27% wplywow; w pokoju uzupelnia sie tylko zuzycie. |
| WarChestToWages | 0.8 (sprzet 0.2) | zapas wojny to przede wszystkim zold; 0.7/0.3 dawalo sprzet ok. 630 tys. dziennie, 2.5 x dzisiejsze zakupy (K11). |
| GearUnspentToHouseholdDays | 30 | budzet sprzetu nie gromadzi sie dluzej niz miesiac; bez towaru pieniadz idzie na dwor, nie lezy. |
| BuildIncomeShare | 0.10 (bez zmian) | decyzja Jeffa 05.10. |
| ReserveDaysPeace / ReserveCapDays | 60 / 120 (+ 50 000) | dzis rody trzymaja ok. 57 dni dochodu; ponad pol roku dochodu zloto lezy bez celu. |
| WarReserveDays / WarReserveFloor | 20 / 20 000 | 20 000 to okup za glowe rodu i miesiac zycia; 20 dni zamiast 30 daje +3 tys. wojska bez zadnego biednego rodu wiecej (rachunek). |
| WarChestDays | 45 | sluzba lenna trwala 40 dni; wolna czesc dochodu pana starczala na 35-80 dni kampanii [H]. |
| BudgetHysteresis / Days | 1.10 / 3 | partia nie moze werbowac i zwalniac na zmiane co dobe. |
| ReleasePerDay | 0.15 nadwyzki | przejscie z wojny do pokoju w 2-3 tygodnie; jedna doba dzialalaby jak masowa ucieczka. |
| MinNewPartyMen | 30 | swita banneretu to ok. 32 ludzi [H]. |
| GarrisonPeaceShare | 0.5 | Marchia Wschodnia: pokoj 3 000 L, wojna 12 000 L (x4) [H]; bierzemy x2, bo oblezenia potrzebuja obroncow od 1. dnia wojny. |
| GarrisonMaxShareOfBudget | 0.6 | zaloga nie zje calego pulapu - pan zawsze ma druzyne. |
| AiGearDaysCap | 30 | lord nie robi jednorazowo wielkich zakupow. |
| FamilyPurseFloor | 5 000 | czlonek rodu zostawia sobie tyle na wlasne wydatki; reszte w razie potrzeby bierze glowa (K12). |

---

## 7. Korona - zold wojenny z biezacych wplywow

### 7.1 Parametry

| Parametr | Wartosc | Uzasadnienie |
|---|---|---|
| CrownWageRefundPercent | 50 (bez zmian) | decyzja Jeffa 05.10; zmienia sie zrodlo pieniedzy i podstawa. |
| zrodlo zwrotu | biezace wplywy korony z dnia (1/3 zaworow, danina wojenna, clo, powinnosci, podatek BK, trybut przyjety) + 1/180 dziennie zapasu ponad `CrownReserveGold` | zwrot staje sie stalym wplywem, ktory budzet rodu moze liczyc. |
| **podstawa zwrotu** | zold partii zaplacony dzis minus 50% tego, co ludzie partii tego rodu wydali wczoraj w osadach tego rodu (MenPurse: zycie w miescie i markietani; nie ponizej 0) | bez tego partia przy wlasnym miescie wraca 1.10 z monety (K3); ta sama zasada co `TownPurse.HomePart` dla zalog z K6. |
| CrownRefundSkipsHomeGarrisons | true (K6, bez zmian) | zold zalogi, ktory wraca panu zaworem, nie jest podstawa zwrotu. |
| **CrownReserveGold** | **500 000** (bylo 1 500 000) | korona historycznie nie trzymala wielkich zapasow [H]; 1.5 mln x 29 krolestw = 43.5 mln zlota poza obiegiem; 0.5 mln to ok. 2 miesiace zwrotu sredniego krolestwa; w rachunku +5-6 tys. wojska, skarbce nigdy puste. |
| **renty od korony** | to, co zostaje z biezacych wplywow po zwrocie i zapomodze - rodom krolestwa **wedlug lenn: miasto 3, zamek 1, wies 0.25** | korona placi panom za straz ziemi (Percy, Marchia [H]); podzial wedlug lenn, nie wedlug zoldu zalog, bo zold zalog pan moze dowolnie podniesc (K2: zwrot 1.4-2.9 z monety). |
| zapomoga gry (`AddIncomeFromKingdomBudget`) | zostaje, ale wyplacana tylko z tego, co zostalo z biezacych wplywow po zwrocie (prefiks: kwota min(gra, pozostale wplywy dnia)) | inaczej rody na rezerwie 20 000 (< 30 000) ciagna ja z zapasu codziennie: 6 pustych skarbcow w 4 lata (K4). |
| dochod "za tier" najemnikow (`DefaultClanFinanceModel.cs:133-136`) | placi skarbiec krolestwa, ktoremu sluza, z biezacych wplywow; rod poza krolestwem - 0 | dzis z niczego (K5). |
| bezpiecznik kas miast | z udzialu korony: dopelnienie kasy miasta ponizej 50% zapasu kupcow, najwyzej 2% zapasu dziennie; wlacza sie dopiero po Z9 (do tego czasu tryb 1 regulatora) | dosypka z prawdziwym platnikiem. |
| TributeMaxShareOfIncome | 0.5 D dziennie | jednorazowa danina (Stormlands ok. 2.2 mln) nie lamie rodu; reszta to dlug krolestwa. |

Danina wojenna i clo (`KingdomTreasury.cs:221-287`) bez zmian, z progiem `TownPurse.TaxFloor` (K6). Uwaga: komentarz w `KingdomTreasury.cs` mowi o 10% powinnosci
w wojnie, a `Settings.CrownDuesWarPercent` = 3 (Armoury.json Jeffa tego klucza nie ma) - czynne jest 3%; poprawic komentarz przy 165.

### 7.2 Czy gracz zarabia na wlasnym wojsku (`gracz.py`, 364 doby, lenno: 1 miasto, 1 zamek, 4 wsie; partia stoi przy wlasnym miescie - przypadek najgorszy)

| Ile wraca z 1 zl zoldu | Projekt poranny: wojna / pokoj | Ta wersja: wojna / pokoj |
|---|---|---|
| partia 300 albo 1 000 ludzi | **1.10** / 0.68 | 0.62 / 0.68 |
| zaloga 200 albo 600 ludzi | **1.43-1.48** / **2.54-2.88** | 0.52-0.55 / 0.59-0.62 |
| partia 1 000 + zaloga 600 | **1.19** / **1.15** | 0.60 / 0.66 |

Po poprawce zadna kombinacja nie daje zysku; zawor 1/2 (zamiast 2/3) obniza zwrot o dalsze 0.1-0.15 i nie jest potrzebny.

---

## 8. Zelazny Bank i dlugi

### 8.1 Decyzje Jeffa (wiazace)
- 05.10: lord NIGDY nie traci lenna; wierzyciel pobiera dochod z jego wsi az do splaty; w ostatecznosci wyprzedaz.
- 07.10, pytanie 7: **wariant A** - wierzyciel bierze caly dochod wsi + z kiesy to, co ponad 38 tys.
- 07.10, pytanie 8: **(c) odsetki zamrozone w dniu zajecia, BEZ umorzenia**.

### 8.2 Kiedy AI pozycza (po budzecie)
Budzet sprawia, ze zold nie przekracza dochodu, wiec pozyczka przestaje byc sposobem placenia zoldu (dzis: 73 bankructwa w roku, Bank pusty po 6 miesiacach).
AI pozycza tylko: (1) w wojnie, gdy G < R i pulap zoldu spadlby ponizej 80% wojennego; (2) na okup glowy albo dziedzica (RealisticCaptivity); (3) na trybut,
gdy rod musi zaplacic natychmiast. Nigdy: rod w zaleglosci, rod z dlugiem wobec korony (`Clan.DebtToKingdom > 0`), rod bez nadwyzki (D <= koszty stale).

| Parametr | Wartosc | Uzasadnienie |
|---|---|---|
| IronBankIncomeDays | 15 dni D (dzis 60 dni dochodu modelu brutto, `IronBank.cs:113`) | rata 10% D splaca w pol roku 15 dni dochodu z odsetkami do 45%; limit z D, czyli z tego, z czego rod naprawde splaca. |
| IronBankMaxInstalmentShare | 0.10 x D dziennie | rata miesci sie w czesci dworu i budow, ktore rod w dlugu tnie. |
| termin | pol roku (bez zmian, `IronBank.cs:172`) | regula zatwierdzona 04.10. |
| w dlugu (S1) | budowy stop, dwor -50%, sprzet tylko braki | pan w dlugu oszczedza na dworze, nie na zalodze (REGULY 5.2). |

### 8.3 Drabina dlugu (REGULY rozdz. 5 z poprawkami do decyzji (c))

| Szczebel | Wejscie | Co sie dzieje | Wyjscie |
|---|---|---|---|
| D1 kredyt | warunki 8.2 | pozyczka; wierzyciel po kolei: skarbiec krolestwa (10%), bogaty rod tego krolestwa (20%), Bank (20/30/45% jak dzis) | splata |
| D2 zaleglosc | rata niezaplacona | placi tyle, ile ma ponad 3 dni zoldu; wiarygodnosc x0.8, +2 pp (z poprawka bledu `IronBank.cs:239`, REGULY 5.5 pkt 3) | pelna rata |
| D3 zajecie | 3 zaleglosci pod rzad (zamiast dzisiejszego bankructwa 50% + 25% dziennie, `IronBank.cs:228-253`) | wierzyciel bierze u zrodla rente i utarg WSZYSTKICH wsi rodu i z kiesy to, co ponad 38 000 (i ponad 10 dni zoldu); odsetki zamrozone; kredyt odciety; poczet sam maleje do tego, co zostaje (zwolnienia 6.2) | dlug 0 |
| D4 wyprzedaz | prognoza splaty > 364 dni albo zajecia 0 przez 14 dni | co 7 dni jeden krok: nadwyzki zbrojowni i konie -> targ; karawany -> targ; warsztaty -> najbogatszy notabl miasta; przychod do wierzyciela | prognoza <= 364 dni |
| bez umorzenia | rod wymarly albo bez wsi | dlug przechodzi na nowego pana jego dawnych wsi (pytanie 14.2); gdy ziemi nie ma - na krolestwo (skarbiec splaca 10% biezacych wplywow dziennie) | dlug 0 |

Dlug wobec korony z gry (`Clan.DebtToKingdom`, `DefaultClanFinanceModel.cs:219-230`) -> nasz dlug z wierzycielem "korona" i ta sama drabina. Gracz-dluznik:
zajecie renty i utargu jego wsi + 25% kiesy dziennie (REGULY 5.4), majatku nie sprzedajemy za niego.

---

## 9. Szczelnosc i bezpiecznik (nowe, Z9)

- **Dlaczego:** w obiegu zamknietym ilosc pieniadza nie ma punktu powrotu. Rachunek (zestaw koncowy, wojna, 4 lata): stala dziura 10 tys./d -> wojsko 92 tys.
  (zamiast 95), 25 tys. -> 85 tys., 50 tys. -> 48 tys., 150 tys. -> gospodarka staje w 2-3 lata. Odwrotnie: stale zrodlo +25 tys./d -> zloto rodow rosnie bez konca.
- **Prog akceptacji kazdej grupy testowej:** reszta niezmierzona swiata < 10 tys. na dobe (srednia 28 dob) i zmiana zlota swiata < 0.1% na 28 dob.
- **Bezpiecznik na czas przejscia:** paczka 111' startuje w trybie 1 (regulator nic nie kasuje, ale dosypuje kase miasta do zapasu kupcow, z licznikiem w logu
  i w ksiedze). Licznik dosypki pokazuje to, co jeszcze gdzies ucieka, i miasta, do ktorych nikt nie wydaje (wtedy patrzec na dwor i markietanow w "Obieg").
  Tryb 2 (bez dosypki) i bezpiecznik korony (165) dopiero, gdy licznik i reszta sa ponizej 10 tys. przez 28 dob.
- **Gdyby po 164 reszta zostala ponad progiem**, a jej zrodla nie da sie znalezc: rozwazyc "kotwice ksiegi" - skarbce dostaja dokladnie to, co zniknelo bez
  platnika (suma zlota swiata stoi co do grosza, log krzyczy). To tez dosypka - dlatego tylko jako ostatecznosc i za wiedza Jeffa.

---

## 10. Log - co pokazac, zeby po roku bylo widac rownowage

### 10.1 Nowe linie (raz na dobe)
1. **"Obieg: dzien D"** - rody wydaly (zold partii, zalog, dwor, sprzet i werbunek, budowy, powinnosci) / dostaly (podatek wsi, renta wsi, zawory miast i zamkow,
   korona: zwrot / renty / zapomoga, lup i jency, Bank) | sakiewki: stan, wplynelo, wydaly w miastach, u markietanow, z poleglymi, ze zwolnionymi | kasy miast:
   stan, zapas, zawor (pan / korona), zakupy plonu N z M (P%), dosypka trybu 1, bezpiecznik korony | zamki | wsie | korona: wplywy (zawory, danina, clo,
   powinnosci, podatek BK, trybut) i wyplaty (zwrot - % zoldu, podstawa po odjeciu wydatkow wlasnych ludzi we wlasnych osadach; renty wedlug lenn; zapomoga;
   dochod najemnikow za tier), stan wobec 0.5 mln.
2. **"Obieg: notable i karawany"** (nowe) - pasmo: nadwyzka do kas osad / dosypka z kas osad (i dzis: z niczego / w nicosc); karawany notabli: liczba, zold
   (dzis w nicosc, po 164 do sakiewek ludzi karawan), doplaty notabli do 5 000.
3. **"Pieniadz swiata (bilans)"** (jest) - zrodla i ujscia rozbite na przyczyny (4.2); reszta z progiem 10 tys.
4. **"Budzet rodow: dzien D"** - rody AI w pokoju / wojnie; D (mediana, 10% i 90%); pulap zoldu a zold faktyczny; rody na suficie partii; zwolnieni dzis (karczma /
   wies); werbunek; rody ponizej rezerwy; przelewy czlonek -> glowa; budzet sprzetu przeniesiony na dwor; glowy < 5 000 i rodziny < 5 000 (bez "Courtiers of ...");
   zalegly zold; majatki BK.
5. **"Dlugi: dzien D"** (format REGULY 5.9) i "IronBank" (jest).
6. **"Wojsko: dzien D"** - partie i ludzie wedlug krolestw (pokoj / wojna), zalogi, konni / piesi (160), pula weteranow.
7. **"ROWNOWAGA (28 dob): dzien D"** - srednia dzienna zmiana kazdego posiadacza i werdykt OK / UWAGA wedlug 10.2.
8. CSV ekonomii (CrashScribe): nowe kolumny `dochod_staly`, `pulap_zoldu`, `zold_faktyczny`, `dwor`, `rezerwa_dni`, `dlug`, `szczebel_dlugu`.

### 10.2 Progi "rownowaga OK" (rok autotestu)
| Miara | Prog | Dlaczego |
|---|---|---|
| zloto swiata | zmiana < 0.1% na 28 dob | system zamkniety |
| reszta [R] swiata i kas miast; dosypka trybu 1 | < 10 tys. dziennie | ponizej tego progu ubytek kosztuje < 3% wojska w 4 lata (rozdz. 9) |
| glowy rodow < 5 000 (bez dworzan BK) | <= 10 z ok. 310 | dzis 92 po roku; rachunek 2-6 |
| zalegly zold | <= 2% partii | budzet wyprzedza zaleglosci |
| zakup plonu wsi | >= 98% oferowanego | miasta maja za co kupic |
| sakiewki | <= 15 dni zoldu partii | markietani dzialaja |
| skarbce | zaden w wojnie ponizej 0.25 mln dluzej niz 28 dob | korona nie bankrutuje |
| wojsko | wojna 85-100 tys., pokoj 30-40 tys.; zmiana w 2-3 tygodnie | decyzje Jeffa 05.10 |
| Bank | zajecia <= 5 rocznie; kapital 4-6 mln | 8.2 |
| zwrot z monety dla rodu gracza (z linii "Obieg") | < 1.0 | Z8 |
| rody z zamkiem / z miastem | zmiana kies na 28 dob w +-10% (poza pierwszymi 45 dobami wojny) | rozjazd z TOWARY 3 zamkniety |

Narzedzie `tools/sprawdz_logi.py`: nowa grupa (`--grupa obieg`) czyta linie 10.1 i wypisuje 10.2 z werdyktem; pierwszy log obejrzec tez z `--surowe`.

---

## 11. Projekcja: 1 rok i 4 lata

### 11.1 Jak liczone [S]
Rachunek krytyka `krytyk\sim_krytyk.py` (niezalezny od `sim\sim.py` autora). Stan wyjsciowy = autotest TOWARY 3 w 40. dobie: 324 prawdziwe rody z `b2\clans2.json`
(dochod modelu, zold partii i zalog, lenna, kiesy glowy i rodziny), zamki z CSV, skarbce z linii "Skarbce" (doba 108876). Kasy miast, zamkow, wsi, sakiewki
i skarbce osobno dla kazdego z 30 krolestw; 20% wydatkow i plonu idzie do miast innych krolestw. Zloto sprawdzane co dobe (blad > 50 zl = stop).
Uproszczenia: plon wsi staly 374 tys. dziennie (bez K13), fizyczny sufit partii = dzisiejsze plateau, w pokoju BK zostawia partie glowy rodu, notable i karawany
poza rachunkiem (zakladamy, ze 1/1b zamknieto bez strat - inaczej rozdz. 9), zold z premia konnych 160 (x1.10). Scenariusze: **wojna** (krolestwa jak w 40. dobie -
tak bylo przez caly rok autotestu), **fale** (180 dob wojny / 120 pokoju, ok. 60% wojny), **pokoj**.

### 11.2 Wynik - zestaw koncowy

| Miara | Dzis po roku [P] | Dzis po 4 latach [S] | Wojna r1 / r4 | Fale r1 / r4 | Pokoj r1 / r4 |
|---|---|---|---|---|---|
| wojsko w partiach rodow | 100.4 tys. | ok. 100 tys. (dopoki Bank i skarbce cos daja) | 95.5 / 94.9 tys. | 79.1 / 77.2 | 36.3 / 36.7 |
| glowy rodow < 5 000 | 92 z 308 | 90-130 | 2 / 2 z 324 | 4 / 6 | 2 / 2 |
| zalegly zold | Bank i puste skarbce | rosnie | 0 / 0 | 0 / 0 | 0 / 0 |
| zloto rodow | 116.5 mln (top 25 = 46%) | rosnie z niczego (+ok. 85 mln rocznie) | 83.6 / 89.0 mln | 84.6 / 90.3 | 87.7 / 93.4 |
| skarbce krolestw | 38.9 mln, 5 Westeros puste | wiekszosc Westeros pusta | 19.6 / 14.0 mln, pustych 0 | 19.6 / 14.0 | 19.7 / 14.2 |
| sakiewki zolnierzy | 17.9 mln | rosna | 4.8 / 4.7 mln | 3.9 / 3.8 | 1.8 / 1.8 |
| kasy miast (zapas kupcow 6.6 mln) | 13.2 mln (trzyma regulator) | - | 15.8 / 16.0 mln | 15.5 / 15.6 | 14.0 / 13.9 |
| zakup plonu wsi | - | - | 100% | 100% | 100% |
| Bank | 0.7 mln, 73 bankrutow | nie pozycza | zajecia 0-5 rocznie [S] | - | - |
| Polnoc (25 rodow), r4 | 2.0 mln, 12 biednych | - | 7.6 mln, wojsko 8.9 tys., 0 biednych | ok. 7.9 mln, 4.4 tys. | ok. 7.2 mln, 3.9 tys. |

Przejscie pokoj -> wojna (po roku pokoju): 36.3 tys. -> 41.1 (1. doba) -> 55.4 (3.) -> 73.9 (7.) -> 97.0 tys. (14. doba) - **AI nie zostaje bez wojska**.

### 11.3 Wrazliwosc (wojna, rok 4; zestaw koncowy = 94.9 tys., 2 biednych)

| Zmiana | Wojsko | Uwagi |
|---|---|---|
| projekt poranny w tym rachunku (zawor 2/3, renty od korony wedlug zalog, zapomoga gry z zapasu, zapas korony 1.5 mln, rezerwa 30 dni, bez 160) | 94.3 tys. | **6 skarbcow pustych**, skarbce 30 mln (zapomoga wypycha z nich do obiegu ok. 13 mln - stad podobne wojsko); gracz zarabia na zalogach (7.2) |
| zapas korony 1.5 mln (jak rano) | 89.0 | 43 mln lezy w skarbcach |
| rezerwa wojny 30 dni (jak rano) | 91.9 | - |
| zold wojny 0.60 | 96.9 | sufit partii |
| przeciek miedzy krolestwami 40% | 94.5 | krainy sie nie rozjezdzaja |
| zapomoga gry z zapasu (jak w grze) | 95.6 | skarbce 9.2 mln, 9 pustych (po roku 3) |
| renty od korony wedlug zalog | 95.6 | AI prawie bez zmian, gracz zarabia (7.2) |
| zalogi w pokoju pelne | 94.3 | zold zalog +4% |
| dziura 10 / 25 / 50 / 150 tys. dziennie | 92.4 / 85.2 / 48.2 / 0 | rozdz. 9 |
| bez dworu i bez wydawania nadwyzki (rachunek autora, poranny projekt 4.4) | pokoj 10 tys., zakup plonu 61% | dlatego dwor (Z5) |

### 11.4 Czego rachunek nie widzi
Rozkladu miedzy miastami jednego krolestwa, wstrzasow (trybut, utrata lenna, bitwy rozbijajace armie), nowych rodow, rzeczywistych pul ochotnikow, cen
i braku towaru (sprzet), notabli i karawan. Dlatego rok i 4 lata trzeba potwierdzic autotestem (rozdz. 12).

---

## 12. Paczki w kolejnosci (przestawione: najpierw pomiar, potem ujscia, potem zrodla)

Numery 161+ robocze. Stan wyjsciowy: **TOWARY 3 W GRZE** (08.10 08:20, Armoury 5a7074c0); **160** gotowa na t3-sklad (2bde2bf), czeka na autotest i "wgraj".
Paczki 108-114 leza na `paczki-na-120/*` - trzeba je przeniesc na t3-sklad + 160 (konflikty: konstruktor `ArmouryBehavior`, `ApplyAll`, `Settings.cs`,
numeracja migawek `MoneyLedger.cs`, `SoldierPay.cs`, `PopulationLaw.cs`, `KingdomTreasury.cs`).

| Kol. | Paczka | Co (slowami gracza) | Zalezy od | Test | Nowa kampania |
|---|---|---|---|---|---|
| 0 | **160 kon wlasnoscia zolnierza** (gotowa) | najemnik i ochotnik na swoim koniu, konny x1.5 zoldu | TOWARY 3 | A | nie |
| 1 | **161 KSIEGA OBIEGU** (tylko log) | nic w grze; log mierzy reszte (4.2) **z zoldem karawan notabli**, "Obieg", "Obieg: notable i karawany", "Budzet rodow (na sucho)", "Dlugi (na sucho)", D na rod | 160 | A (40 dob) | nie |
| 2 | **BetterEconomy 13 kluczy** (skrypt) | BEE przestaje pobierac w nicosc; funkcje gracza bez zmian | gra zamknieta, kopia | B | nie |
| 3 | **164a SZCZELNOSC - pieniadz** (przed K5/K6) | kazde ujscie dostaje odbiorce: zold karawan notabli i lordow do sakiewek ludzi karawan, awanse do sakiewki partii, ochotnik z mapy do wsi, statki do miasta portowego, lup z cial z sakiewek, jency / oblezenia / turnieje BK z kas miast, wydatki lordow BK do skarbca. Przy dzisiejszym regulatorze to bezpieczne: nadwyzke dalej zbiera gra, brak dosypuje | 161 (pomiar) | B (40 dob) | nie |
| 4 | **108 ludzie: jednostka** + **109 przyrost** | ksiega ludzi; przyrost wsi | BEE (109), 161 | B | nie |
| 5 | **164b** - jency i ochotnicy jako ludzie | jeniec sprzedany w miescie zostaje mieszczaninem; ochotnik z mapy ubywa z wsi | 108, 164a | B | nie |
| 6 | **110 K5** + **111' K6 w trybie 1** + **162 DWOR** + **112 K7** + **163 MARKIETANI** + **114 porzadki** + **pasmo notabli (164c)** | gra nie kasuje kas; "zakupy z niczego" znikaja; dosypka do zapasu kupcow zostaje jako liczony bezpiecznik (Z9); nadwyzka do pana i korony; dwor do kas siedzib; utarg wsi zostaje we wsi; zolnierze wydaja w polu; nadwyzka notabla do kasy jego osady | 164a, 161 (D) | C (40 dob + rok) | TAK (110/111 przycinaja dar startowy) |
| 7 | **113 spustoszenie** | rabunek wygania ulamek ludzi | 108, 109 | C albo D | zalecana |
| 8 | **165 KORONA** | zwrot polowy zoldu z biezacych wplywow (bez wydatkow wlasnych ludzi we wlasnych osadach), renty od korony wedlug lenn, zapomoga gry tylko z biezacych wplywow, dochod "za tier" najemnikow od korony, zapas 0.5 mln, trybut korona-korona; **tryb 2 regulatora i bezpiecznik korony dopiero, gdy Z9 spelnione** | 111', 114, 164 | D | nie |
| 9 | **166 BUDZET RODU** | lord trzyma tyle wojska, na ile ma dochodu; zwalnia nadwyzke; werbunek, sprzet, nowe partie z budzetu; kiesa rodziny | 161, 108, 162, 165, 160 | E (40 dob + rok) | zalecana |
| 10 | **167 WETERANI** | najemnik w karczmie tylko z prawdziwych ludzi | 160, 108, 166 | E | zalecana |
| 11 | **168 DLUG** | Bank pozycza na zdolnosc splaty; zajecie zamiast bankructwa; bez umorzenia; Debts do skarbca | 166, 165, BEE | F (rok) | nie |
| 12 | autotest roczny (ok. 120 min - rok na T3 trwal 119 min, 19.7 s/dobe), potem 4-letni (ok. 8 h) | odczyt 10.2; kalibracja jednej liczby naraz (najpierw `CrownReserveGold`, potem `WarReserveDays`, potem dwor) | wszystkie | - | TAK |

111' = K6 z dwiema zmianami wobec opisu z 06.10: tryb 1 do spelnienia Z9 (potem tryb 2) i bezpiecznik z udzialu korony (165). Kazda paczka ma wlasny wylacznik MCM;
gdy grupa nie dziala, wylaczamy po kolei. Do czasu 166 + 168 Bank w kampanii Jeffa na T3 bedzie pusty po ok. pol roku (pomiar roku) - to znany stan, nie blad nowych paczek.

---

## 13. Ryzyka i co sprawdzic (kontrola wedlug zasady 0)

1. **Szczelnosc** (rozdz. 9): najwieksze ryzyko projektu. Bez zatkania 1b (karawany notabli) i reszty 4.2 tryb 2 regulatora wyciagnie z obiegu 100-200 tys. dziennie.
2. **Notable:** pasmo z kasy osady moze przy biednych miastach zostawic notabli bez zlota - mniej doplat do karawan, mniej karawan notabli w biednych krainach.
   Sprawdzic w "Obieg: notable i karawany" (liczba karawan nie moze spasc o wiecej niz 10%).
3. **Rachunek zbiorczy** (rozdz. 11) - liczby +-30%; najwrazliwsze: ilosc pieniadza w obiegu (zapas korony, rezerwa wojny) i szczelnosc.
4. **Regresja rent z miast:** renty nie plyna juz z "zakupow z niczego"; miasto bez wojska i bez dworu pana da malo. Patrzec w "Obieg" na rozklad miedzy miastami.
5. **Kolizje w kodzie:** `DailyTickClan` (SoldierPay prefiks/postfiks, KingdomTreasury transpiler, nowy budzet); `MakeClanFinancialEvaluation` (BK prefiks pomija oryginal -
   nasz postfiks musi biec zawsze); `CheckRecruiting` (prefiks MountedWage 160 + budzet); `RecruitCost` / `LevyGold` a pulap werbunku; `AiGear`; `BuildFunding` (D);
   `DesertionLaw` / `WarLedger` (zwolnieni nie sa dezerterami); `MenPurse` (markietani a wydatek przy wyjezdzie - nie dwa razy tego samego dnia; licznik
   "wydatki ludzi we wlasnych osadach" dla podstawy zwrotu); `MoneyLedger` (nowe migawki); `KingdomTreasury.WageRefund` (zrodlo i podstawa) i `Levies` (TaxFloor);
   `IronBank` (limit z D); prefiks na `AddIncomeFromKingdomBudget` (prywatna metoda modelu - BK woluje ja przez refleksje tylko w wydatkach, w dochodach nie).
6. **Cudzy kod:** BK `DismissParties` i `UpdatePopFromSoldiers` (nie dublowac z 108); BK `GetBestAvailableCommander`; vanilla `NotablesCampaignBehavior` (karawany);
   Diplomacy (`KingdomBudgetWallet`, reparacje); NavalDLC (statki); StrategicCampaignAI (mniejsze partie moga rzadziej oblegac); BK `BKCaravansBehavior` (czy BK
   ma wlasny zold karawan - sprawdzic przy 161).
7. **Martwy kod robi sie zywy / zywy martwy:** tarcza zoldu (107b) przy K6 nieczynna; `WarLedger` prawie przestanie dzialac (zamierzone); bankructwo Banku
   zastapione zajeciem; zapomoga gry dla rodow AI prawie zniknie w wojnie (korona wydaje biezace wplywy na zwrot) - rody biedne dostaja ja w pokoju.
8. **Zapis gry:** nowe klucze (D, licznik histerezy, budzet sprzetu niewydany, pula weteranow, wiersz dlugu, flagi K5/K6 we wlasnym `try`); stary zapis: D(0) = G/60.
   Wszystkie `Reset` w konstruktorze `ArmouryBehavior`.
9. **Pulapka MCM:** przed kazdym wgraniem sprawdzic w `Armoury.json` Jeffa nowe klucze i zmienione domyslne (`IronBankIncomeDays`, `AiGearBudgetPercent`,
   `CrownReserveGold`, `PeaceWageShare`, `WarReserveDays`) - dzis zadnego z kluczy korony, renty i Banku tam nie ma (sprawdzone 08.10).
10. **Gracz:** zawor zamiast renty w jego miastach i zamkach; jego ludzie tez placa markietanom; zwrot korony mniejszy, gdy jego ludzie wydaja zold w jego osadach;
    jency i lup w biednym miescie warte mniej (placi kasa miasta); zalogi AI w pokoju slabsze.
11. **Kasy miast przy froncie:** jency i lup z oblezen placi kasa miasta - patrzec w "Obieg" na miasta ponizej 50% zapasu.
12. **Rycerze BK:** zakupy zaopatrzenia majatkow poza budzetem - po pomiarze 161 wliczyc do pulapu sprzetu.
13. **Trybut -2.2 mln:** zrodlo ujemnego `TributeWallet` nieustalone (Diplomacy?) - sprawdzic w dekompilacji przed 165.
14. **Wydajnosc:** 324 rody x kilka dzialan dziennie + okna 161 - pomijalne wobec ok. 20 s na dobe.
15. **Dane wejsciowe:** renty i zwrot przypisane rodom w B2 wedlug lenn; stan ustalony wojny z 10 dob; zold karawan notabli to szacunek (219 zl na karawane jak u lordow).

---

## 14. Pytania do Jeffa (tylko to, co zmienia gre)

1. **Czy Twoj rod tez ma placic dwor?** AI placi 35% dochodu w pokoju (20% w wojnie) do kasy swojej siedziby. Rekomendacja: **nie** - swoje wydatki wybierasz sam.
   (Skutek: Twoje renty z miast beda mniejsze niz dzis, bo znika "zloto z niczego" mieszczan.)
2. **Dlug wymarlego rodu przechodzi na nowego pana jego wsi** (wierzyciel dalej bierze dochod tych wsi), takze na Ciebie. Rekomendacja: **tak** - jedyny sposob na "bez umorzenia".
3. **Zalogi AI w pokoju o polowe mniejsze** (w wojnie znow pelne). Rekomendacja: **tak** (x2 zamiast historycznego x4, zeby oblezenia mialy obroncow).
4. **Karczmy w nowej kampanii (weterani):** start z pula "starych zolnierzy" rowna dzisiejszej liczbie najemnikow w karczmach, potem tylko z rozpuszczonych armii.
   Rekomendacja: **tak**.

Bez pytania (parametry ekonomii, decyzja projektu z uzasadnieniem w tabelach): zapas korony 0.5 mln, rezerwa wojny 20 dni, pokoj 25% dochodu na zold, renty od korony
wedlug lenn, zwrot bez wydatkow wlasnych ludzi we wlasnych osadach, zapomoga gry tylko z biezacych wplywow, zold karawan notabli do ludzi karawan, kolejnosc paczek.

---

### 14.1 ODPOWIEDZI JEFFA (08.10 ok. 11:50) - wiazace

1. Jeff: "w sensie podatki, jesli dolacze do jakiegos krolestwa to tak, place podatki jesli mam ziemie w posiadaniu". Czyli: **gracz-wasal z lennem
   placi koronie jak kazdy wasal AI** (udzial korony z jego osad, 165). **Dwor (wydatki pana na dom) graczowi nie jest naliczany** - swoje wydatki wybiera sam
   (rekomendacja z pytania 1 utrzymana; Jeff odpowiedzial o podatkach).
2. **Tak** - dlug wymarlego rodu przechodzi na nowego pana jego wsi (takze na gracza).
3. **Tak** - zalogi AI w pokoju o polowe mniejsze (w wojnie pelne).
4. **Tak** - karczmy w nowej kampanii startuja z pula starych zolnierzy.

Numeracja: "161 KSIEGA OBIEGU" z rozdz. 12 to teraz **169 KSIEGA OBIEGU** (161 = zapis bez dlugich napisow, wgrane 08.10 11:48 razem ze 160).
Stan wyjsciowy paczek ekonomii: paczki/161-zapis 2e235ea (T3 + 160 + 161, Armoury c01a54ba w grze).

## 15. Weryfikacja i pliki

### 15.1 Sprawdzone w kodzie (08.10, krytyk)
Gra 1.4.8: `DefaultSettlementEconomyModel.cs:75-79`; `ItemConsumptionBehavior.cs:61-77`; `NotablePowerManagementBehavior.cs:47-60`; `NotablesCampaignBehavior.cs:47, 290, 309-330`;
`ClanVariablesCampaignBehavior.cs:353-387, 409-423, 438, 458-475`; `DefaultClanFinanceModel.cs:133-136, 155-158, 176-185, 219-230, 323-358, 504-528, 540-560`;
`HeroSpawnCampaignBehavior.cs:175-203`; `PartyUpgraderCampaignBehavior.cs:105-152`; `DefaultPartyDesertionModel.cs:50-75`; `RecruitmentCampaignBehavior.cs:452-470, 615-636`;
`SellItemsAction.cs:64-88`; `DefaultSettlementTaxModel.cs:19-21`; `SellPrisonersAction.cs:78-90`; `SiegeAftermathCampaignBehavior.cs:160-170`; `MapEvent.cs:1848-1868`;
`ChangeShipOwnerAction.cs:26-44`; `Town.cs:485`. BK: `EconomyPatches.cs:100-156, 158-219, 579-590, 832-840, 1083-1110`; `BKSettlementBehavior.cs:498-528, 670-702`;
`BKCampaignStartBehavior.cs:232-252`; `BKTournamentManager.cs:20-27`; `BKClanBehavior.cs:1160`. BetterEconomy: `CastleEconomyCampaignBehavior.cs:355-360, 1072-1076`;
`better_economy_settings.xml:33, 405, 621` (dawne wartosci). Armoury (2bde2bf): `PopulationLaw.cs:272-315`; `SoldierPay.cs:300-347, 430-453`; `MenPurse.cs:160-200`;
`KingdomTreasury.cs:11-35, 100-287`; `IronBank.cs:101-123, 165-180, 222-270`; `AiGear.cs:188-196`; `BuildFunding.cs:154-160`; `LevyGold.cs:36-42` (`:74` z B1 po 160 przesuniete);
`Settings.cs:414, 421, 540, 545-546, 606-643`. Wszystkie zgodne z opisem B1/B2 i porannego projektu, poza nowymi znaleziskami K4-K6 i stalym komentarzem
o 10% powinnosci w `KingdomTreasury.cs`.

### 15.2 Sprawdzone liczby
FAKTY zlecenia zgodne z logiem 06:51 (wojsko 14 387 -> 101 145; zold partii 460 694, zalogi 159 065, karawany 33 292; zwrot 286 146; kasy miast 7 838 940, zamkow
3 303 732, sakiewki 4 807 656, skarbce 56 216 183, Bank 4 773 092, notable 29 706 759; swiat -16.02 mln; regulator miast -196 118 / zamkow -132 294 sr. 38 dob;
"zniklo" 70 464 w 40. dobie). Rok - rozdz. 1.1 (wlasne skrypty na logu i CSV).

### 15.3 Pliki
- Ten plik (jedyny w repo).
- Robocze (sesja): `...\scratchpad\dzien-6\ekonomia-obieg\PROJEKT.md` (wersja poranna), `B1-bilans.md`, `B2-lordowie.md`, `B3-historia.md`, `sim\*` (rachunek autora),
  `krytyk\sim_krytyk.py` (rachunek tej wersji; uruchomienie: `python sim_krytyk.py "s_lord=2/3,kres=500000,rez=20,a_p=0.25,xw=0.8,wage_up=1.10,scen='wojna'"`),
  `krytyk\gracz.py` (7.2), `krytyk\przejscie.py`, `krytyk\rok_csv.py`, `rok_log.py`, `rok_rozklad.py` (rozdz. 1), wyniki `krytyk\wynik-krytyk.txt`, `rok_*.txt`.
- Dane: logi `Modules\Armoury\Armoury-2026-10-08_06-51-52.log` i `Armoury-2026-10-08_08-18-38.log`; CSV `CrashScribe\economy-2026-10-08_06-53-20.csv` i `economy-2026-10-08_08-20-02.csv`.
