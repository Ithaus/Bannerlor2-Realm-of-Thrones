> **POPRAWKA JEFFA 07.10 (wiazaca, ma pierwszenstwo przed tekstem ponizej):** nazwa SPICHLERZ (nie stodola). Plon spichlerza NIE jest abstrakcyjnym
> 1.25 racji na czlowieka - spichlerz napelnia PRAWDZIWA produkcja zywnosci wsi regionu w grze (wszystkie przedmioty IsFood z produkcji wsi: zboze,
> ryby, ser, maslo, mieso z hodowli, oliwki, winogrona, daktyle...), przeliczona na racje jedna stala skalibrowana tak, by zwykly rok dawal ok. 1.2 x
> potrzeby regionu. Zima, spustoszenie (113), brak rak, mniej zwierzat i stan wsi obnizaja plon same. Stan "spladrowana" gry nie zeruje produkcji
> calego okregu: wies produkuje wedle tego, co zostalo (decyzja Jeffa 05.10 - spalona wies to symbol).

# Glod z prawdziwego bilansu zywnosci - analiza kodu i projekt (2026-10-07)

Pytanie Jeffa: "glod jest, jak nie ma jedzenia - przeciez wioski maja albo nie maja jedzenia - i jak to jest przeliczane, ze jest food?".
Zastepuje propozycje z `docs/HISTORIA-RABUNKU-I-BITEW-2026-10-07.md` rozdz. 3.3 (glod od "15% spustoszonych w roku").
Tylko odczyt: nic nie budowane, nic nie wgrane. Skrypty: `csv1.py`, `csv2.py`, `csv3.py` (log), `zima.py`, `scen.py` (symulacje) w tym katalogu.
Dekompilacje: `SCR\ore-supply\cs`, `SCR\ore-supply\bk`, `SCR\ore-supply\be` (SCR = scratchpad sesji 3cf3e0ac); ROT i RealisticBannerlord
zdekompilowane tu: `rot\`, `rbl\`. Kod 113: `Devastation-113.cs` (z galezi `paczki-na-120/113-ludzie-spustoszenie`).

## 0. W trzech zdaniach

1. **Wsie w grze nie maja jedzenia i nic nie jedza.** Wies tylko produkuje towar (zboze, ser, maslo, ryby...) do magazynu, ktory woz wiezie na targ.
   Jedzenie jako liczba ("Food") maja tylko miasta i zamki, a ich bilans jest dzis tak hojny, ze w 40 dobach zadna z 227 warowni nie glodowala.
2. **Naszych 52.5 mln ludzi gra nie karmi wcale** - "Food" miasta zjada "dobrobyt / 40" i zaloga, nie ludzie z ksiegi; dlatego glodu ludzi nie da
   sie odczytac z "Food" ani z zadnego wskaznika typu "15% spustoszonych".
3. **Projekt: kazda wies (okreg) dostaje stodole w racjach** (1 racja = jedzenie 1 czlowieka na dobe). Plon okregu = tyle, ile gra mowi, ze wies
   dzis wyprodukowala, przeliczone na ludzi; zjadaja ludzie w domu i uchodzcy; rabunek pali zapas spustoszonej czesci okregu. **Glod = stodola pusta
   i plon mniejszy niz potrzeba.** Ile ginie - z braku jedzenia (krzywa skalowana na glodach 1315-17, 1693-94, 1695-97). Miasta i zamki glodu ja,
   gdy gra mowi "Food 0" (oblezenie) - i wtedy gina tez mieszczanie, nie tylko zaloga.

---

## 1. Jak dziala zywnosc dzis (kod + log)

### 1.1 Wies: produkuje towar, nie ma zapasu, nie je

- Wies nie ma pola zapasu zywnosci: `FoodStocks` jest w klasie `Fief` (`cs\...Settlements\Fief.cs:15`), ktora dziedzicza tylko miasta i zamki.
  `Settlement.IsStarving` = `Town.FoodStocks <= 0`, dla wsi zawsze `false` (`Settlement.cs:410-419`). Wiesniacy nie zjadaja niczego.
- Produkcja co dobe (`VillageGoodProductionCampaignBehavior.cs:137-153`), tylko gdy magazyn wsi < 1.5 x pojemnosc:
  - **"zywnosc wsi"**: `TickFoodProduction` (:178-209) - `CalculateDailyFoodProductionAmount` sztuk losowo z listy zboze / ser / maslo
    (`DefaultVillageTypes.cs:271-273`, czestosc odwrotnie do ceny - glownie zboze). Ile: `poziom hearth + 1` (1 / 2 / 3 przy progach 200 / 600),
    **0, gdy wies nie jest w stanie Normal** (spalona, lupiona, pod przymusem) (`DefaultVillageProductionCalculatorModel.cs:76-88`);
  - **towary typu wsi** (`:17-74`): baza typu x (poziom + 1) x 0.5 - np. ryby 28, oliwki 12, winogrona 11, daktyle 8, maslo i ser 2-4.
  - Czynny model: owijka NavalDLC na modelu bazowym (log `Armoury-2026-10-07_08-03-07.log:240`). Na bazowej metodzie siedza nasze postfiksy:
    zima `WinterBite.VillageFoodPostfix` (-50% x gradient polnocy 0.75-1.25, `WinterBite.cs:121-133`) i (paczka 113) `Devastation.FoodPostfix`
    (x mnoznik rak, `Devastation-113.cs:450-455`).
- Pojemnosc magazynu = 5 x dzienna produkcja (`Village.cs:248-257`). Woz (tabor wsi) zabiera magazyn na targ (119: najlepsze miasto w zasiegu 250;
  122: caly magazyn). **To, co lezy w magazynie wsi, to towar na sprzedaz, nie zapas na zime.**

### 1.2 Targ: mieszczanie "kupuja" jedzenie i to zasila "Food" miasta

- `ItemConsumptionBehavior.MakeConsumptionInTown` (`:61-71`) co dobe: mieszczanie zjadaja z polki towar wedle budzetu popytu (`:142-176`; miasto
  dostaje za to zloto), sztuki kategorii zywnosci (zboze, ryby, mieso, ser, maslo, winogrona, oliwki, daktyle, piwo - `BonusToFoodStores` w
  `TaleWorlds.Core.DefaultItemCategories`) ida do dziennika `SoldItems` (`:108-119`), a nastepnego dnia kazda sztuka to **+1 Food**
  (`DefaultSettlementFoodModel.cs:82-91`).
- Miasto oblezone z pustym Food dobiera jedzenie prosto z polki (`ItemConsumptionBehavior.cs:98-106`).

### 1.3 Spichlerz warowni ("Food" miasta i zamku): skladniki bilansu

Czynny lancuch modeli (log :240 "zywnosc ROT.Models.ROTSettlementFoodModel"; kolejnosc launchera: BannerKings przed ROT-Core):
`ROTSettlementFoodModel` (`rot\ROT.Models\ROTSettlementFoodModel.cs`; rejestracja `rot\ROT\SubModule.cs:301` - owija poprzedni model)
-> `BKFoodModel` (`bk\BannerKings.Models.Vanilla\BKFoodModel.cs`; rejestracja `bk\BannerKings\Main.cs:187-189`, gdy nie ma EconomyOverhaul)
-> baza gry `DefaultSettlementFoodModel` (`:43-97`). Skladniki dobowej zmiany (Food na dobe):

| Skladnik | Wartosc | Gdzie |
|---|---|---|
| ziemie wokol | +15 miasto, +10 zamek (nie w oblezeniu) | `DefaultSettlementFoodModel.cs:63-66` |
| kazda wies zwiazana w stanie Normal | +(poziom hearth + 1) x 6 = 6 / 12 / 18; spalona 0; w oblezeniu nic | `:67-75` |
| budynki, polityka "Hunting rights" | +2 i budynki | `:57-62, :76` |
| jedzenie kupione przez mieszczan z polki | +1 za sztuke | `:82-91` |
| mieszczanie | -dobrobyt / 40 (miasto 4800 -> -120, zamek 1000 -> -25) | `:47, :55` |
| zaloga | -ludzie / 20 | `:48, :56` |
| BK "Local farmland" | +0.15 x produkcja ziemi BK (akry x 0.025, sila robocza klas BK, zyznosc; 0 w oblezeniu) | `BKFoodModel.cs:50-73, :139-219` |
| ROT | Inni (White Walkers) 0; +100 town_EW4, +120 ROT_castle61 | `ROTSettlementFoodModel.cs` |
| nasza zima | -0.5 x dobrobyt / 1000 x gradient polnocy | `WinterBite.cs:135-146` |

- **Spozycie klas BK (chlopi -0.02, rzemieslnicy -0.04, szlachta -0.075 na glowe) NIE wchodzi do bilansu** (`BKFoodModel.cs:84-137`) - sluzy tylko
  prognozie oblezenia (`MilitaryData.cs:76`) i skupowi nadwyzek `HandleExcessFood`, ktory **nigdy nie biegnie**: sprawdza
  `SettlementFoodModel is BKFoodModel`, a czynny jest ROT (`BKSettlementBehavior.cs:613-617`).
- **Sufit** = ludnosc BK x 2 + 500 (miasto) / + 250 (zamek) + 1000 x poziom spichlerza - latka BK `FoodStockPatch` (`bk\...Patches\SettlementPatches.cs:96-118`).
  Co dobe: `Food += zmiana`, obciete do [0; sufit]; przy 0 `RemainingFoodPercentage = -100` (`Town.cs:596-617`).
- Zamki BK przy Food <= 5% sufitu zjadaja jedzenie ze schowka zamku (`BKSettlementBehavior.cs:727-730, :926-962`). BK Layered (`ClusterFoodTracker.cs:66-185`):
  przy wlaczonym `LayeredEconomyYields` miasto z nadwyzka wysyla tabor jedzenia do miasta tego samego krolestwa w niedoborze.

### 1.4 Liczby z ostatniego logu (08:03, 40 dob, `Logs\2026-10-07_08-03-07\ludzie-regiony.csv`)

| | doba 1 | doba 20 | doba 40 |
|---|---|---|---|
| miasta (97): Food mediana | 27 151 | 29 386 | 30 620 |
| miasta: zmiana mediana / min | +261 / +16 | +241 / +44 | +218 / +25 |
| zamki (130): Food mediana | 6 456 | 6 974 | 7 115 |
| zamki: zmiana mediana / min | +97 / +53 | +72 / -32 | +65 / -33 |
| warownie glodne (Food 0) | 0 | 0 | 0 |
| warownie z ujemnym bilansem | 0 | 1 | 2 (max 5 w dobie ok. 37) |

- **Zapas siedzi na suficie:** przez 39 dob Food miast zmienil sie o mediane +96 przy bilansie +200-300 na dobe - nadwyzka przepada co noc
  (The Eyrie: 41 240 -> 29 114 w 4 doby przy bilansie +286 - spadl sufit, bo zmalala ludnosc BK).
- **Zapasu starcza na ok. 200 dni** samego zjadania (mieszczanie + zaloga): miasta mediana 207, zamki 195 (min 58).
- `Dowoz (skutki)` (`MarketRoad.cs:139-149, :194-199`): jedzenie z polki zamkow razem 2 837 -> 107 na dobe (wpis 100 wysyla plon wsi zamkowych do
  miast), zamki z ujemnym bilansem 0 -> 4; pusty spichlerz 0.
- 60 region-dob z ujemnym bilansem (zamki, wojna, spalone wsie: Cape Wrath -32.6, Bisa -32.0 przy Food 5.6-7.1 tys. - to 175-220 dni).

### 1.5 Co gra i mody robia, gdy Food = 0 (`IsStarving`)

| Kto | Skutek | Gdzie |
|---|---|---|
| dobrobyt (czynny model RealisticBannerlord) | -20 na dobe "Severe Starvation"; dodatkowo -2 na dobe, z czego 80% przechodzi do najblizszego miasta krolestwa (tez przy samym oblezeniu) | `rbl\...Demographics\RealisticProsperityModel.cs:27-30`, `DemographicsExpansionBehavior.cs:73-103` |
| lojalnosc (BK) | -2 na dobe (pelny spichlerz ponad 80% sufitu +0.5) | `BKLoyaltyModel.cs:322-334` |
| zaloga | -10% zwyklych zolnierzy na dobe, gdy niedobor wiekszy niz jedzenie mieszczan | `DefaultPartyHealingModel.cs:132-139`, `SettlementHelper.cs:550-558` |
| partie lordow w oblezonym, glodnym miescie | -10% na dobe | BK `VanillaModelTweakPatches.cs:813` |
| oblezenie (RBL) | +1-2 zabitych obroncow na dobe | `SiegeAttritionBehavior.cs:55-66` |
| zaraza oblezenia (nasza) | obroncy choruja x2 | `CampFever.cs:95` |
| notable, wyrzutki, powroty (113) | notable omijaja miasto (x0.1); nedza OutlawLaw; powrot uchodzcow x0.25 | `HeroSpawnCampaignBehavior.cs:482`, `OutlawLaw.cs:468-511`, `Devastation-113.cs:473-487` |
| BetterEconomy (wlasna ludnosc, lustrzana w klasach BK) | **juz przy zmianie Food < -3 na dobe (nie przy pustym spichlerzu!)**: chlopi -0.5% na dobe, rzemieslnicy -0.4% | `be\...PopulationCampaignBehavior.cs:135-172`, `BetterEconomySettings.cs:25-29`; lustro `PopulationData.cs:402-428` |
| BK przyrost | czynnik -2 - tylko okno BK | `BKGrowthModel.cs:56-59` |

**Nikt w grze nie zabija z glodu zwyklych ludzi.** Gina tylko zolnierze; miastu spada "dobrobyt". Nasza ksiega ludzi (PopulationLaw: wsie 41.98 mln,
miasta 10.535 mln) zywnosci nie czyta poza licznikiem w "Ludzie:" i kolumnami CSV (`PeopleLedger.cs:261-263, :365-366`).

### 1.6 Dlaczego "Food" nie mierzy glodu ludzi - dwie skale w jednej liczbie

- Zolnierz (partia, zaloga): **1 jednostka jedzenia = dzienna racja 20 ludzi** (`NumberOfMenOnMapToEatOneFood` 20, `NumberOfMenOnGarrisonToEatOneFood` 20).
- Mieszczanie: "dobrobyt / 40". W naszej ksiedze miasta maja 10.535 mln ludzi, a zjadaja razem ok. 11.6 tys. jednostek -> **1 jednostka = ok. 906
  mieszczan** (mediana miast 260). To ta sama "symbolika" co hearth (1 punkt = 191 ludzi).
- Cala produkcja jedzenia w towarze wsi (kilka tysiecy sztuk dziennie) przy 20 racjach na sztuke wykarmilaby ok. 0.1-0.2 mln ludzi z 52.5 mln.
- Wniosek: glod ludzi trzeba liczyc w skali ksiegi (1:1), a gre czytac tam, gdzie mowi cos realnego: **ile wies dzis wyprodukowala wobec normy**
  (stan, zima, rece) i **czy spichlerz warowni jest pusty** (oblezenie).

---

## 2. Projekt: glod z prawdziwego bilansu

### 2.1 Zasada i jednostka

- **Racja** = jedzenie jednego czlowieka na jedna dobe (ok. 2 000 kcal, ok. 0.6 kg zboza w przeliczeniu). Ta sama skala co ksiega ludzi (1:1).
- **Dwa miejsca zapasu w kazdym regionie** (jedna regula dla wszystkich 571 wsi i 227 warowni):
  1. **Stodola okregu** (NOWA, bo gra jej nie ma) - zapas ludzi wsi w racjach, jeden na wies-okreg (wies to symbol okregu - decyzja Jeffa 05.10).
  2. **Spichlerz warowni** - "Food" gry (bez zmian w skali), zywi mieszczan i zaloge; czytamy z niego tylko "pusty / niepusty" i bilans dnia.
- Glod jest tam, gdzie zapas jest pusty i dzienny doplyw nie pokrywa potrzeby. Zadnych progow "15%".

### 2.2 Stodola okregu - rachunek dobowy (wies v)

Oznaczenia: `L` = ludzie okregu przed spustoszeniem (w domu + uchodzcy tej wsi, 113), `h` = ludzie w domu, `r` = uchodzcy przebywajacy w regionie.

1. **Plon** `H = L x 1.25 x q`, gdzie
   - **1.25 racji na czlowieka okregu na dobe** - z naszej ksiegi: 41.98 mln ludzi wsi zywi siebie i 10.535 mln mieszczan (52.513 / 41.978 = 1.251);
     te 25% to nadwyzka, ktora w grze jest towarem na wozach;
   - **`q` = mnoznik z gry** = `CalculateDailyFoodProductionAmount(v)` czynnego modelu / `(GetHearthLevel(v) + 1)`. Zawiera wszystko, co gra i nasze
     latki robia z plonem: stan wsi (spalona, lupiona, pod przymusem = 0), zime (-37.5..-62.5%), mnoznik rak 113 (`(h/L)^e`), zadania "polowa
     produkcji". Dzielnik usuwa skok progow 200 / 600 (113 i tak liczy poziom sprzed spustoszenia). Jedno zrodlo prawdy: plon ludzi zmienia sie
     o tyle procent, o ile gra zmienia produkcje wsi.
2. **Potrzeba** = `h + r` racji (kazdy je jedna racje). Uchodzcy jedza ze stodol regionu, w ktorym sa (zwykle swojego) - nie produkuja.
3. **Kolejnosc: gospodarz je pierwszy, uchodzca dostaje reszte** (historycznie najpierw umierali uchodzcy i najslabsi: Evesham 1070, Strasburg 1622).
4. **Przednowek (racjonowanie):** gdy w stodole jest mniej niz 90 dni potrzeby, a plon dnia jej nie pokrywa - wszyscy jedza 90%. Brak 10% nie zabija
   (patrz 2.5); to tylko odsuwa glod i daje w logu ostrzezenie.
5. **Sufit stodoly = 2 lata potrzeby okregu** (`2 x 364 x L`). Nadwyzka ponad sufit to towar dla miast (wozy) - tak jak dzis.
6. **Strata przy spustoszeniu:** kazde `Devastation.Strike` (rabunek i zerowanie armii, 113) zabiera stodole ten sam ulamek, jaki trafil ludzi:
   `stodola x trafieni / L`. Spalona czesc okregu traci ludzi i zapasy naraz - jedna regula dla rabunku i marszu. Zboze, ktore `ScorchedEarth`
   daje armii (1 + ludzie / 250 sztuk), miesci sie w tej stracie (20 racji na sztuke, ok. 2-3% zabranego).
7. **Start:** stodola pelna (10 lat lata przed kampania). Stary zapis: pelna w pierwszej dobie.

### 2.3 Kto je i skad

| Kto | Skad je | Kiedy glodny |
|---|---|---|
| ludzie wsi w domu | stodola swojej wsi + plon dnia (pierwsi) | stodola pusta i plon < potrzeba |
| uchodzcy (113 i z glodu) | stodoly regionu, w ktorym przebywaja (po gospodarzach) | to, co zostanie po gospodarzach, nie pokrywa potrzeby |
| mieszczanie (ksiega 9a) | spichlerz warowni (gra: dobrobyt / 40) | Food = 0 (gra) |
| uchodzcy w oblezonej warowni | spichlerz warowni: `uchodzcy / Q` jednostek na dobe, Q = mieszczanie / (dobrobyt / 40) tej warowni; zamek (0 mieszczan w ksiedze) - Q swiata 906 | Food = 0 |
| zaloga, partie | Food warowni / towar w taborze (gra, bez zmian) | gra (IsStarving partii / garnizonu) |

### 2.4 Kiedy jest glod

- **Glod wsi:** `stodola + plon dnia < potrzeba` -> stodola = 0, brak gospodarzy `u = 1 - (stodola + plon) / h`, brak uchodzcow
  `u_r = 1 - reszta / r`. Rano kolejnego dnia liczy sie od nowa (powroty plonu po zimie, po stanie "spalona").
- **Glod warowni:** gra `IsStarving` (Food <= 0). Brak `u_f = max(0, -zmiana Food) / spozycie dnia` (mieszczanie + zaloga + uchodzcy w srodku).
- **Glodu nie ma**, dopoki jest zapas, nawet przy ujemnym bilansie (zima w miastach i wsiach to normalne zjadanie zapasow).

### 2.5 Ile ginie - krzywa z historii

**Zgony z glodu rocznie = 70% x ((u - 0.10) / 0.90)^2 dla u > 0.10** (u = brakujaca czesc jedzenia po zjedzeniu zapasow), dziennie / 364,
osobno dla gospodarzy, uchodzcow i ludzi warowni. Brak do 10% nie zabija: ludzie jedza mniej, zastepniki, ubijaja zwierzeta.

| brak jedzenia u | zgony rocznie | odpowiednik historyczny |
|---|---|---|
| 0.10 | 0 | zwykly zly rok (+-20% plonu), przednowek |
| 0.20 | 0.9% | |
| 0.30 | 3.5% | Wielki Glod 1315-17: 5-12% ludnosci polnocnej Europy w 2-3 lata; Anglia -12% w 1315-25 (-1.3% rocznie); Brugia 1316 ok. 5.5%, Ypres 10% lub wiecej |
| 0.375 | 6.5% | Francja 1693-94: 1.3-1.5 mln z ok. 21 mln (6-7%) w ok. 1.5 roku |
| 0.50 | 13.8% | |
| 0.55 | 17.5% | Finlandia 1695-97: ok. 1/3 (150 tys. z 500 tys.) w 2 lata |
| 0.70 | 31% | polnocna Finlandia 1695-97: ok. polowa |
| 0.90 | 55% | oblezenie bez dowozu: Paryz 1590 - 13 tys. zmarlych z glodu z ok. 200-220 tys. w ok. 4 miesiace (szacunki starsze wyzsze) |
| 1.00 | 70% | pulap: nawet w calkowitym braku ludzie cos znajduja |

Kotwice sa wiazkami (szacunki brakow dla 1315-17 i 1693-94 to rzad 0.25-0.4 po zapasach, handlu i zastepnikach) - [PROJ z kotwicami], nie pomiar.
Zgony nie sa liczone podwojnie: zastepuja proponowane w HISTORIA "-20% rocznie uchodzcow przy glodnej / oblezonej warowni" i czlon H z paczki 109.

### 2.6 Uchodzcy z glodu

- **Tylu, ilu umiera, rusza w droge** (Irlandia 1845-52: ok. 1 mln zmarlych z 8.5 mln i podobna liczba emigrantow - Gray, O Grada).
- Dokad: do najblizszego regionu tego samego krolestwa (albo niewrogiego), ktorego stodoly maja ponad 180 dni zapasu; tam sa uchodzcami
  (jedza po gospodarzach). Brak takiego w zasiegu - zostaja (i glodu ja z reszty).
- Konto: to samo konto uchodzcow wsi co w 113 (wies pochodzenia) + **miejsce pobytu** (region). Jedno miejsce na konto wsi; przeprowadzka
  najwyzej raz na 30 dob.
- Powrot: regula 113 (0.1% dziennie x (1 - niebezpieczenstwo)) - **nikt nie wraca do wsi, ktorej stodola ma mniej niz 90 dni** (nie wraca sie do glodu).
- Z warowni oblezonej uchodzcy nie wychodza; z glodnej nieoblezonej - jak wyzej (do sasiedniego regionu).

### 2.7 Zima i pora roku

- Pory roku Westeros (`WesterosClimate.cs`; `Settings.cs:555-571`): start w lecie, jesien 300-450 dni, **pierwsza zima 3-5 lat**, dalej zimy 2-6 lat.
  Zima tnie produkcje wsi -50% x gradient polnocy (37.5% w Dorne, 62.5% za Murem; `WinterBite.cs:41-48, :121-133`) - czyli i plon stodol
  (przez `q`). W zimie kazda wies zjada zapas: polnoc 0.53, srodek 0.375, poludnie 0.22 potrzeby na dobe.
- "Jesien i zima najgorsze" wynika samo: latem zabrany zapas sie odbudowuje (25% potrzeby na dobe), w zimie nie ma z czego - spalona stodola
  przed zima oznacza glod w polowie zimy.
- Osobno (krok zgodnosci, poza rdzeniem): zima nie tnie dzis ziem warowni ("ziemie wokol", BK "Local farmland") - jedna regula zimy wymagalaby
  tego samego ciecia tam. Warownie zostaja na plusie (miasto ok. +100, zamek ok. +50 na dobe w zimie), wiec na glod to nie wplywa.

### 2.8 Rabunek, zerowanie armii, spustoszenie (113)

- Rabunek: stan "spalona" gry daje `q = 0` przez 8-17 dob (zjada sie zapas), `Strike` pali ulamek stodoly rowny ulamkowi trafionych ludzi.
- Zerowanie armii: ta sama `Strike` (0.2 czlowieka na zolnierza na dobe) - ten sam ulamek zapasu.
- Mniej rak = mniejszy plon (`q` zawiera mnoznik 113); ale tez mniej gab w domu - a uchodzcy dalej jedza z regionu. Bez uchodzcow-zjadaczy
  spustoszenie ZMNIEJSZALOBY glod (mniej ust, ten sam zapas) - dlatego uchodzcy sa w potrzebie regionu.
- Elastycznosc rak: 113 ma 0.5, HISTORIA proponuje 1.0 (rozdz. 2.6). Rachunki nizej dla obu.

### 2.9 Oblezenie: twierdza bez zapasu

- Gra: w oblezeniu odpadaja ziemie, wsie i "Local farmland"; zostaje polka targu. Food miasta (mediana 27 tys.) przy spozyciu ok. 136 na dobe
  starcza na ok. 200 dni, zamku (6.5 tys. / 31) ok. 210.
- **Nowe:** na czas oblezenia uchodzcy regionu sa w murach i jedza z Food (`uchodzcy / Q` na dobe; linia dymka "Refugees sheltering"). Przyklad:
  miasto-mediana + 20 tys. uchodzcow (Q 260): +77 na dobe -> zapas na ok. 127 dni zamiast 200. Zamek + 12 tys. (Q 906): +13 -> 148 dni.
- **Nowe:** gdy Food = 0, umieraja z glodu mieszczanie (ksiega 9a) i uchodzcy w srodku wedle `u_f` (zwykle 0.85-0.95 -> 50-60% rocznie, ok.
  4-5% miesiecznie). Zaloga - jak w grze.
- Stodoly wsi wokol oblezonego miasta traci zerowanie oblegajacych (2.8) - glod okregu przychodzi po oblezeniu.

### 2.10 Narodziny

- W glodzie (u > 0.10) **dodatni przyrost naturalny wsi = 0** (109: `GrowthPostfix`); ujemny zostaje. Chrzty w glodzie spadaly o 35-75% (Outram 2001).
- Czlon H z 109 (`GrowthHungerEnabled`, z ciecia zimy) zostaje WYLACZONY na stale - zastepuje go ten projekt (jeden glod, liczony raz).

### 2.11 Co projekt zastepuje

| Bylo w propozycjach | Teraz |
|---|---|
| HISTORIA 3.3(a): glod od F = trafieni w roku / ludzie, prog 15%, do 12% rocznie, wagi pory | stodola okregu + krzywa 2.5 |
| HISTORIA 2.5: uchodzcy -20% rocznie, gdy warownia gloduje albo jest oblezona | glod warowni: `u_f` (tylko pusty Food); samo oblezenie bez glodu = zaraza (`CampFever`), nie glod |
| 109: czlon H przyrostu (glod z zimy) | wylaczony; zgony z 2.5, przyrost dodatni = 0 w glodzie |
| 113: powrot x0.25 przy glodnej warowni | zostaje; dodane: brak powrotu do wsi z pusta stodola |

HISTORIA 2.5 "uchodzcy -1.3% rocznie (bieda)" to nie glod - zostaje do decyzji przy kalibracji 113.

### 2.12 Stan ustalony i scenariusze (symulacje `zima.py`, `scen.py`; okreg = 100% ludzi na starcie)

**Pokoj, lato / wiosna / jesien:** stodoly pelne (sufit 2 lata), plon 125% potrzeby, glodu 0. Swiat: zapas 30.6 mld racji, plon 52.5 mln na dobe.

**Sama zima, bez wojny** (stodoly pelne, racjonowanie, 571 wsi wedle polozenia z `settlements.xml` i ludzi z CSV):

| zima | stodola pusta wczesniej niz koniec zimy | zmarli z glodu (swiat) | gdzie |
|---|---|---|---|
| 3 lata | 0 wsi | 0 | - |
| 4 lata | 40 wsi (1.2% ludzi wsi) | ok. 4 tys. | daleka Polnoc |
| 5 lat | 96 wsi (7.9%) | ok. 164 tys. (0.39% ludzi wsi) + tylu uchodzcow | 38 regionow Polnocy (3.3 mln): Winterfell ok. 6.5%, za Murem ok. 15% |
| 6 lat | 249 wsi (29.6%) | ok. 613 tys. (1.46%) | Polnoc i srodek |

**Wojna** (srodek mapy = ciecie zimy 50%; 3% trafionych ginie od miecza, 90% to uchodzcy):

| Przypadek | dni glodu | zmarli z glodu | uciekli | zabici przy spustoszeniu |
|---|---|---|---|---|
| jeden rabunek pocztu (0.2% okregu) | 0 | 0 | 0 | 0.006% |
| polowa okregu spustoszona LATEM, potem 3 lata lata | 0 | 0 | 0 | 1.5% |
| najgoretsza wies sesji (6.5% trafionych rocznie) 5 lat lata | 0 | 0 | 0 | 0.9% |
| to samo + 4 lata zimy, rabunki trwaja | 15 | 0.6% | 0.6% | 0.9% |
| **polowa okregu spustoszona JESIENIA, potem 4 lata zimy** (elastycznosc 1.0 / 0.5) | 818 / 664 | **23.6% / 18.8%** | 23.6% / 18.8% | 1.5% |
| "Harrying": 80% okregu spustoszone w zimie, zima jeszcze 2 lata | 526 | **30.6%** | 30.6% | 2.4% |
| wies Wolnych Ludzi, 12 rabunkow rocznie, 5 lat lata (elastycznosc 1.0 / 0.5) | 104 / 0 | 6.0% / 0 | 6.0% / 0 | 2.3% |
| to samo w 4-letniej zimie za Murem | 583 | 31.0% | 31.0% | 1.8% |

Zgodnosc z historia (HISTORIA rozdz. 3.2): pojedyncze rabunki i chevauchee latem - bez glodu (1339, 1346, 1355); spustoszenie duzej czesci
regionu jesienia lub zima - glod zabija 10-20 razy wiecej niz miecz (Harrying 1069-70, Wegry 1241-42: 15-50%); glod przychodzi miesiace po
zniszczeniu (tu: gdy skonczy sie zapas). Pod oblezeniem: ok. 200 dni zapasu, potem 4-5% miasta miesiecznie.

### 2.13 Krok B (pozniej, po pomiarze A): spichlerz miasta zywiony przez stodoly

Dzis miasta zywi hojny, symboliczny bilans gry (BK "Local farmland" niezalezny od naszych ludzi; nadwyzka ginie na suficie), wiec miasto
gloduje tylko w oblezeniu. Historycznie miasta cierpialy w glodzie bardziej (1315-17: 10-25%). Krok B: doplyw ze wsi do spichlerza miasta =
nadwyzka stodol (zero, gdy okreg zjada zapas), mieszczanie jedza z tego i z targu. Rachunek z CSV: **43 z 97 miast** ma wiecej mieszczan niz
nadwyzka wlasnych wsi (King's Landing x14, Duskendale x26, Tyrosh x6.5) - zylyby z targu i karawan. To zmienia jedzenie wszystkich miast
i budzi BetterEconomy (-0.5% chlopow dziennie przy zmianie < -3) - dlatego osobno i dopiero po tym, jak A pokaze liczby w logu.

---

## 3. Gdzie w kodzie (plan wpiecia; baza: galaz `paczki-na-120/113-ludzie-spustoszenie`)

- **NOWY `Famine.cs`**: stan `Barn` na wies (racje, slownik id wsi -> double, jak `_scars` w 113), miejsce pobytu uchodzcow; `Daily()` w
  `ArmouryBehavior.OnDailyTick` PO `ScorchedEarth.OnDaily` i `Devastation.Daily`, PRZED `PeopleLedger.Daily` (zeby przyrost i ksiega widzialy
  dzisiejszy glod); `Reset()` w konstruktorze `ArmouryBehavior`; zapis jako sekcja `f:` w kluczu `arm_people` (`PopulationLaw.ExportPeople` /
  `ImportPeople`, format sekcji jak 109/113).
- Odczyt plonu: `Campaign.Current.Models.VillageProductionCalculatorModel.CalculateDailyFoodProductionAmount(v)` / `(v.GetHearthLevel() + 1)` -
  raz na wies na dobe (571 wywolan; w modelu bazowym nie ma losowania).
- `Devastation.Strike` (113): po zdjeciu ludzi `Famine.Burn(v, hit / L)`.
- `Devastation.ReturnRate` (113): 0, gdy `Famine.DaysOfFood(v) < 90`.
- `PopulationLaw.GrowthPostfix` (109): `g = min(g, 0)` przy glodzie wsi.
- Zgony: wies - `PeopleUnit.Shift(v, -zmarli / k)` (1/k jak 108); uchodzcy - konto 113; miasto - stan `TownPeople` (109 9a). Ucieczka - konto 113
  (+ miejsce pobytu).
- Oblezenie: postfiks na `DefaultSettlementFoodModel.CalculateTownFoodStocksChange` (jak `WinterBite.TownFoodPostfix`; BK wola baze, ROT owija BK,
  wiec postfiks na bazie dziala w calym lancuchu) - linia "Refugees sheltering", tylko gdy warownia oblezona.
- Log: `PeopleLedger` - nowa linia "Zywnosc:" i kolumny CSV `stodola_dni`, `plon_proc`, `glod_u`, `zmarli_glod`, `uciekli_glod`; w "Ludzie:"
  zmarli z glodu jako osobna pozycja.
- Gracz (napisy po angielsku): w menu wsi "Barns: N days of food"; dymek hearth "Famine: -N people a year"; komunikat "Famine in {REGION}: the barns
  are empty."; dymek Food warowni "Refugees sheltering".
- MCM (`Settings.cs` + `python tools/gen_mcm.py`): `FamineEnabled` (wl.), `FamineBarnYears` 2, `FamineYieldPerPerson` 1.25, `FamineDeathMaxPercent` 70,
  `FamineThreshold` 0.10, `FamineRationDays` 90. W `Armoury.json` Jeffa tych kluczy nie ma (nowe).

## 4. Ryzyka / co sprawdzic

1. **W 40-dobowym tescie nic nie bedzie widac** (lato, stodoly pelne). Pomiar: linia "Zywnosc:" (stodoly swiata w dniach, plon % potrzeby,
   racjonuja / gloduja) + autotest dlugi albo kampania testowa z krotkim latem i jesienia (ustawienia klimatu w MCM tylko na czas testu).
2. **Dluga zima zabija Polnoc.** Przy zimie 5 lat ok. 164 tys. zmarlych (Winterfell ok. 6.5%, za Murem 15%) - kanon Westeros, ale to sufit 2 lat
   decyduje. 1.5 roku: glod zaczyna sie ok. 25% wczesniej.
3. **Elastycznosc rak (113: 0.5, HISTORIA: 1.0)** zmienia wynik malych krain: Wolni Ludzie latem 0 / 6% w 5 lat; w zimie za Murem ok. 31%.
   Male krainy (Nocna Straz, Wolni Ludzie) krwawia przy stalym najezdzaniu - zbiega sie z ryzykiem 12 paczki 113.
4. **Miasta (10.5 mln) gloduja tylko w oblezeniu** - granica kroku A; krok B (2.13) osobno.
5. **BetterEconomy:** jej "glod" to zmiana Food < -3 na dobe przy dowolnym zapasie (chlopi BE -0.5% dziennie, lustro w klasach BK -> sufit Food BK,
   sila robocza BK). Uchodzcy w oblezonej warowni pogleba zmiane - dopisac do K2 klucz `StarvationFoodChangeThreshold` (wylaczyc) albo latka na
   `IsStarving`.
6. **Kolejnosc i zaleznosci:** wymaga 108 (1/k), 109 (stan miast, przyrost), 113 (konto uchodzcow, `Strike`, mnoznik rak). Bez 113: `L = h`,
   uchodzcow brak, spustoszenie = tylko stan "spalona" gry.
7. **`GetHearthLevel` i JIT** (ryzyko 13 paczki 113): `q` dzieli przez poziom - postfiks 113 musi byc wpiety przed pierwszym liczeniem produkcji.
8. **Model zywnosci:** dzis czynny ROT -> BK -> baza. Projekt nie zalezy od tego, ktory jest ostatni (czyta model wsi i `IsStarving`); `HandleExcessFood`
   BK jest martwy (sprawdza `is BKFoodModel`) - nie ruszamy.
9. **Wydajnosc:** 571 wywolan modelu + petla po kontach - ponizej 1 ms na dobe. Przeprowadzki uchodzcow: najwyzej raz na 30 dob na konto.
10. **Zapis:** nowa sekcja `f:` w `arm_people`; stary DLL ja pomija (sekcje obce sa pomijane). Stary zapis: stodoly pelne w pierwszej dobie.
11. **Q zamkow (906)** jest symboliczne - liczy sie tylko w oblezeniu z uchodzcami w murach.

## 5. Zrodla

- Wielki Glod 1315-17: 5-12% ludnosci polnocnej Europy; Brugia 1316 ok. 5.5%, Ypres 10% lub wiecej (Van Werveke) - https://en.wikipedia.org/wiki/Great_Famine_of_1315%E2%80%931317 ;
  W. C. Jordan, *The Great Famine* (Princeton 1996) - https://press.princeton.edu/node/51160 ; Anglia 4.69 -> 4.12 mln w 1315-25 (Broadberry, Campbell i in.) - jak w `DEMOGRAFIA-SILA-ROBOCZA` H3.
- Francja 1693-94: 1.3-1.5 mln, ok. 7-10% (M. Lachiver, *Les annees de misere*, 1991) - https://www.ebsco.com/research-starters/history/famine-and-inflation-17th-century-france
- Finlandia 1695-97: ok. 1/3 ludnosci (150 tys. z 500 tys.), polnoc ok. polowa - https://en.wikipedia.org/wiki/Great_Famine_of_1695%E2%80%931697
- Irlandia 1845-52: ok. 1 mln zmarlych z 8.5 mln (C. O Grada) - https://researchrepository.ucd.ie/entities/publication/fe0c5e5b-20c5-46ef-ae05-fafc89edd9c8
- Oblezenie Paryza 1590: ok. 200-220 tys. mieszkancow, 13 tys. zmarlych z glodu w ok. 4 miesiace - https://en.wikipedia.org/wiki/Siege_of_Paris_(1590)
- Chrzty w glodzie -35..75%, Strasburg 1622 (1/5 uchodzcow w kilka miesiecy): Outram 2001 - za `HISTORIA-RABUNKU-I-BITEW-2026-10-07.md` rozdz. 2.5 i 3.1.
- Harrying of the North, Wegry 1241-42, chevauchee 1339 / 1346 / 1355: tamze rozdz. 3.1-3.2.
