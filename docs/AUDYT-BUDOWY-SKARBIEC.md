# AUDYT: budowy w osadach i skarbiec krolestwa (2026-10-04)

Pytania Jeffa: (a) skarbce krolestw tylko rosna - krol powinien budowac i utrzymywac drogi i wydawac na to, na co krolowie
historycznie wydawali; (b) czy projekty w osadach nie buduja sie za szybko i bez pieniedzy - powinny wymagac zlota I materialow.

Oznaczenia: **[KOD]** = sprawdzone w zdekompilowanym kodzie (sciezka/typ/metoda), **[LOG]** = z logu/CSV,
**[SZAC]** = szacunek z wzorow (bez logu), **[HIST]** = zrodlo historyczne (lista na koncu), **[PROPOZYCJA]** = projekt, nic nie wdrozone.
1 moneta gry = 1 pens angielski (c. 1300), 1 L = 240 d, rok = 364 dni.

---

## 1. Jak dzis dziala budowa (vanilla 1.4.8 + BK + BKROT + BEE + nasze)

### 1.1 Lancuch wywolan [KOD]
- `TaleWorlds.CampaignSystem.CampaignBehaviors.BuildingsCampaignBehavior.DailyTickSettlement` (tylko warownie):
  - kazdy budynek `HitPointChanged(+10)` gdy nie ma oblezenia (punkty zycia budynku tylko wracaja do 100; spadaja do 0 -> `LevelDown`
    wylacznie przez zniszczenia; poza oblezeniem i `OnSiegeAftermathApplied` nic budynkow nie niszczy = **brak utrzymania**);
  - AI (nie gracz): 10%/dzien `DecideBuildingQueue` (gdy kolejka pusta - wybor z `BuildingScoreCalculationModel`), 1%/dzien zmiana projektu dziennego;
  - `TickCurrentBuildingForTown`: `building.BuildingProgress += town.Construction`; gdy postep >= koszt -> `LevelUp` (koszt odjety z postepu).
    Oblezona osada nie buduje. `Town.Construction` = `Campaign.Models.BuildingConstructionModel.CalculateDailyConstructionPower(town).ResultNumber`.
- Koszt poziomu = `Building.GetConstructionCost()` = `BuildingType.GetProductionCost(level)` (x0.8 dla zamkow z polityka Castle Charters).
  To sa **punkty budowy**, nie zloto.
- Model aktywny: BK `BannerKings.Models.Vanilla.BKConstructionModel` (rejestrowany `AddModel<BuildingConstructionModel>`),
  BKROT podmienia na `BKROTConstructionModel` (tylko straznik rekurencji, ta sama matematyka). BEE, ROT, Naval, nasze mody - nie dotykaja
  modelu budowy (przeszukane DLL: tylko BK, BKROTPatch, NavalDLC, Spoils of War maja w ogole nazwy typow budowy; RealisticLoot/Naval - nie o budowe).

### 1.2 Punkty budowy dziennie - BK `CalculateDailyConstructionPowerInternal` [KOD]
Vanillowe `0.01 x dobrobyt` BK USUWA. Sklad (sufit 100/dzien, `LimitMax(100)`):
1. **Workforce** (`GetWorkforce`): w MIESCIE/ZAMKU = 0, jesli polityka BK `workforce` != 3 (Construction). Przy Construction:
   `(SerfsConstructionForce + TenantsConstructionForce) x 0.015 + SlavesConstructionForce x 0.02 (x1.2 prawo SlavesHardLabor)`,
   gdzie `SerfsConstructionForce = serfs x 0.5 x 0.15` (tenants tak samo), `SlavesConstructionForce = (int)(StateSlaves x 0.5)` -
   `StateSlaves` to ulamek, wiec prawie zawsze 0. Wychodzi **~0.001125 punktu na glowe chlopa/dzierzawcy** w miescie.
2. **AI BK ustawia polityke** (`BannerKings.decompiled.cs` ~l.108900): krolestwo w POKOJU -> Construction (bo `town.CurrentBuilding`
   jest prawie zawsze != null - to tez projekt dzienny), w WOJNIE -> Martial_Law (bunt -> None). Czyli **w wojnie workforce = 0**.
3. Zamek: "Idle garrison" = `garnizon x (bezpieczenstwo/100 - 0.49) x 0.1` przy bezpieczenstwie >= 50.
4. Boost (tylko gracz, patrz 1.4), zarzadca (skill `TownProjectBuildingBonus`, perki), "Construction from Market" = 0.25 x liczba
   sprzedanych narzedzi (`ItemCategory.Property.BonusToProduction` ma tylko `tools`), budynek Mason (+3/6/9 miasto, +2/4/6 zamek),
   perk BK CivilEngineer +20%, innowacja Cranes +12%, feat kultury Battania (+%), lojalnosc (>75 do +12%, 25-50 do -100%, <=15 -> -100%).
5. **Blokada materialowa**: jesli dla biezacego projektu `GetMaterialSupply(material)` (= Stash osady + POLKA targu tej osady) < wymaganie
   -> wynik zerowany ("Missing X for project Y").

### 1.3 Materialy BK [KOD] `BKConstructionModel.GetMaterialRequirements`, `BKBuildingsBehavior.RunMaterials/OnBuildingChanged`
- Ilosc: `n = koszt_punktow / 20` jednostek, rozbite udzialami (zalezne od typu i poziomu):
  - Fortyfikacje: narzedzia 0.1; od poz. 1 dodatkowo drewno 0.1, wapien 0.8.
  - Koszary/Rynek: poz.0 drewno 0.9; poz.1 drewno 0.7, glina 0.15, narzedzia 0.05; poz.2 drewno 0.7, ruda 0.1, wapien 0.05, narzedzia 0.1.
  - Wodociagi/Teatr: wapien, drewno, glina, ruda, marmur (poz.2 marmur 0.5). Kopalnie: drewno, narzedzia, ruda.
  - Reszta: poz.0 drewno 0.7 + glina 0.2; poz.1 drewno 0.5, glina 0.2, ruda 0.1, narzedzia 0.05; poz.2 drewno 0.4, wapien 0.1, ruda 0.15, narzedzia 0.1.
- `RunMaterials` (DailyTickTown, **tylko gdy osada ma zarzadce**): dla kazdego brakujacego materialu przenosi do 5 szt./dzien z polki targu
  do `Settlement.Stash`; cena (x1.2 gdy z innego miasta) **dopisywana do kasy miasta `ChangeGold(+cena)` - z niczego w tej chwili** i do licznika
  `materialExpenses[town]`, ktory BK odejmuje od podatku miasta pana (`BKTaxModel.CalculateTownTax`).
  BLAD BK: dla ZAMKU `val` = najblizsze miasto, ale petla czyta `town.Settlement.ItemRoster` (polke ZAMKU, pusta), a odejmuje z polki
  miasta -> zamki praktycznie nie gromadza materialow; `GetMaterialSupply` tez patrzy tylko na zamek -> **zamki stoja na "Missing hardwood"**
  (wniosek z kodu, NIE potwierdzony w logu - brak logu budow).
- `OnBuildingChanged` (ukonczenie poziomu): materialy zdejmowane ze Stash (zuzyte - znikaja, OK). Brakujaca reszte wycenia po polce
  i pobiera od pana `ChangeHeroGold(-x)` **w nicosc** (nie do kasy miasta), a z polki ich NIE zdejmuje (BLAD: odejmuje drugi raz ze Stash).
  Gdy pan nie ma calej kwoty - traci wplywy `num7 x 0.5`, czyli polowe kwoty ZAPLACONEJ, nie brakujacej (blad BK).
- Jedno wymaganie liczy sie na CALY poziom; blokada sprawdza tylko, czy zapas istnieje (polka wystarczy), wiec bez zarzadcy budowa idzie,
  dopoki na targu lezy dosc drewna/narzedzi - nic nie jest zuzywane az do konca poziomu.

### 1.4 Kto placi - stan po wpisach 49/51 [KOD]
| Element | Platnik | Odbiorca | Bilans |
|---|---|---|---|
| Robocizna (punkty z workforce, garnizonu, zarzadcy) | **nikt** | - | robota za darmo |
| Materialy w trakcie (RunMaterials) | pan przez ujemna linie "Taxes" miasta (`PopulationLaw.KeptTownLines`: warsztaty + kopalnie - materialy, x(1-0.6 x autonomia)) | kasa miasta (+cena, w chwili zakupu z niczego) | prawie zachowane: pan placi `(1-0.6a)` czesci, reszta `0.6a` x koszt powstaje z niczego. Linia trafia do `ExplainedNumber` dochodu rodu (BK `AddSettlementIncome` dodaje wynik `CalculateTownTax` bez obcinania do 0) - **pieniadz NIE znika po wpisie 51, idzie od pana do kasy miasta** |
| Materialy na koniec poziomu (brakujaca reszta) | pan `ChangeHeroGold(-x)` | **nikt (nicosc)** | wyciek; towar zostaje na polce |
| Boost "Craftsmen services" (tylko gracz) | gracz `GiveGoldAction(MainHero, null)` | **nikt (nicosc)** | wyciek; BK: koszt = rzemieslnicy/2 (miasto) lub x2 (zamek), ale tick vanilla zdejmuje z puli stale 500/250 dziennie; efekt = `rzemieslnicy x 0.009` pkt/dzien (max 100) |
| BEE "Town infrastructure projects" (Roads and Patrol Posts, Granaries, Caravanserai, ...) | wirtualny `TownEconomyState.LocalTreasuryGold` (zasilany fikcyjnym "gross tax" BEE, wyplaty do panow wylaczone wpisem 49) | nikt | caly obieg BEE jest poza prawdziwym zlotem: nie zabiera nikomu, ale tez buduje "z niczego"; koszt 6 000/18 000/45 000 (zamki 4 000/12 000/30 000), max 800/dzien i 20% nadwyzki ponad rezerwe |

### 1.5 Ile to kosztuje i jak dlugo trwa [SZAC z wzorow, brak logu budow]
Ceny materialow dzis: drewno = ladunek 100 kg ~4 d (wpis 50), narzedzia 250 (wartosc gry, nie przeliczona), glina 18,
**wapien 0 i marmur 1** (BKROT dzieli ceny towarow BK przez 100 - `docs/SPIS-CEN-GRY.md`), ruda ~8 d / 100 kg.

| Projekt (poziom) | Punkty | Materialy BK | Koszt dla pana dzis | Czas: miasto ~20 tys. chlopow+dzierzawcow (~22 pkt/d, pokoj) | Czas: zamek ~3 tys. + garnizon 100 (~6.5 pkt/d) |
|---|---|---|---|---|---|
| Mury miasta 1->2 | 6000 | drewno 30, narzedzia 30, wapien 240 | ~7.7 tys. d (32 L) | ~270 dni (0.75 roku) | - |
| Mury miasta 2->3 | 12000 | x2 | ~15 tys. d | ~545 dni | - |
| Rynek 0->1 | 2400 | drewno 108 | ~0.43 tys. d (1.8 L) | ~110 dni | - |
| Koszary miasta 0->1 | 1800 | drewno 81 | ~0.3 tys. d | ~80 dni | - |
| Mury zamku 1->2 | 1400 | drewno 7, narzedzia 7, wapien 56 | ~1.8 tys. d | - | ~215 dni (jesli w ogole ruszy - blad 1.3) |
| Spichlerz zamku 0->1 | 420 | drewno 14, glina 4 | ~0.1 tys. d | - | ~65 dni |
W WOJNIE (Martial_Law) workforce = 0 -> miasto prawie stoi (zostaja tylko narzedzia sprzedane na targu, zarzadca, Mason).
Wniosek: czas budowy nie jest absurdalnie krotki (miesiace, nie dni), ale **robocizna jest darmowa, a material kosztuje 1-3% historycznej
ceny** - wiec "bez pieniedzy" Jeffa jest prawda. Jesli Jeff widzi bardzo szybkie budowy - podejrzani: sufit 100/dzien w duzych miastach
(>90 tys. chlopow+dzierzawcow) i Mason; trzeba to zobaczyc w logu (PROPOZYCJA 5.7).

---

## 2. Drogi i infrastruktura - co istnieje [KOD]
- **Vanilla 1.4.8 ma budynek "Roads and Paths"** (`DefaultBuildingTypes._buildingSettlementRoadsAndPaths`: koszt 2400/3600/4800,
  zamek `_buildingCastleRoadsAndPaths` 560/840/1120): `VillageProduction` +5/10/15% (AddFactor), `VillageHeartsPerDay` +0.1/0.2/0.3.
  Nie wplywa na ruch na mapie.
- **BEE** `TownInfrastructureProject.RoadsAndPatrolPosts` (`TownEconomyState.RoadsLevel` 0-3): `GetRouteDangerMultiplier` x0.96/0.92/0.88
  (niebezpieczenstwo tras dostaw BEE), `GetDeliveryAttractivenessMultiplier` +3/6/10%, trade power +0.08/poziom. Placone wirtualnym skarbcem BEE.
- Teren mapy: `TaleWorlds.Core.TerrainType` nie ma typu "droga" (jest Bridge, Fording, RuralArea...). Nasze `TerrainEase.cs` (postfix na
  `CalculateFinalSpeed`, plaskie kary terenu) i `CrossingLaw.cs` (przeprawy) - to naturalne miejsce na efekt drog.
- BK: brak drog; NavalDLC: porty/stocznie, nie drogi; ROT: nic.
- Mozliwe znaczenie "drog" w grze: (1) mniejsza kara terenu (las/bagno/snieg) w promieniu R od osady z Roads and Paths; (2) mniejsze straty
  i wiecej dostaw w BEE (juz jest, ale z wirtualnego zlota); (3) produkcja wsi i przyrost (vanilla, juz jest).

---

## 3. Skarbiec krolestwa (`Kingdom.KingdomBudgetWallet`) - wplywy i wydatki [KOD]
| Kto | Co | Kierunek |
|---|---|---|
| vanilla `ClanVariablesCampaignBehavior.DailyTickClan` | start 2 mln; dosypki 1000/dzien i losowe 100-400 tys. | wplyw z niczego - **WYLACZONE** naszym transpilerem (`KingdomTreasury.Transpiler`) |
| vanilla `DefaultClanFinanceModel.CalculateClanExpensesInternal` | 1% zlota rodu ponad 100 tys. ("Kingdom Budget Expense") | wplyw; **BK podmienia na 0.1%** (`BKClanFinanceModel`, `(gold-100000)*0.001`) - w CSV np. 317 841 -> 217 |
| vanilla `AddIncomeFromKingdomBudget` | zapomoga 500-2000 (x2 przy skarbcu > 1 mln, x2 dla krola) dla rodow < 30 tys. | **jedyny staly wydatek** - nieczynny, bo rody maja 180-470 tys. |
| vanilla oplata najemnikow | `MercenaryWallet` dzielony na rody (`AddExpensesForHiredMercenaries`) | NIE ze skarbca |
| vanilla `GetMercenaryAwardFactor`-podobne (l.63282), BK `MercenaryClan` ocena | czyta skarbiec (powyzej 50/100 tys. chetniej najmuje) | tylko odczyt |
| BK `BKDiplomacyBehavior.MakeTruce(..., kingdomBudget)` | zaplata za rozejm | wywolywane z `kingdomBudget=false` - placi kiesa krola, nie skarbiec |
| **Bannerlord.Diplomacy 1.4.7** (aktywny!) `ApplyInternal/GetMoneyFromGiver` | reparacje wojenne: najpierw nadwyzka skarbca > 2 mln, potem `TributeWallet`; odbiorca: 1/3 krol, 1/6 najemnicy, gracz, reszta do skarbca | wydatek tylko przy pokoju z reparacjami (w 12 dniach nie wystapil) |
| nasze `KingdomTreasury.Daily` | powinnosci wasali 2% (pokoj) / 3% (wojna) dochodu | wplyw (~9 tys./dzien razem) |
| nasze `KingdomTreasury.Levies` | danina wojenna 1% kasy miast > 20 tys. i 1.5% wsi; clo 10% licznika cel miast | wplyw (~15-18 tys. + ~14 tys./dzien) |
| BEE, ROT | nic | - |

**[LOG]** CSV `economy-2026-10-04_16-55-41.csv`, kolumna `skarbiec_krolestwa`, 29 krolestw: suma 58 000 637 (dz. 108836) ->
58 746 758 (dz. 108848) = **+746 tys. w 12 dni (~62 tys./dzien, 52-76 tys.)**; zaden skarbiec nie zmalal (min 2 001 394, max 2 067 961).
Armoury log: `Korona: ... powinnosci wasali 8 908-9 286`, `danina wojenna 14 755-17 785, clo 14 074-14 690` dziennie; reszta = BK 0.1%.
**Wniosek: skarbiec ma tylko wplywy - 2 mln startu + ~2 tys. d/dzien na krolestwo (~0.75 mln d = 3 100 L rocznie) leza martwe.**
Dla skali: korona Anglii c.1300 ~40 000 L zwyklego dochodu rocznie [HIST] = 26 tys. d/dzien; nasze krolestwa sa 10x mniejsze - i to pasuje.

---

## 4. Historia: na co wydawali krolowie 1250-1400 [HIST]
### 4.1 Budzet
- Zwykly dochod Edwarda I ~40 000 L/rok; clo od welny (od 1275) ~10 000 L/rok; Riccardi z Lukki obracali ~20 000 L/rok i w 1286-89
  dali garderobie 107 000 L; krol zwykle "na minusie" 10-20 tys. L [cepr.org/voxeu; Wikipedia Riccardi].
- Wojna to wydatek nadzwyczajny: wojny 1294-98 (Gaskonia, Szkocja, Flandria) kosztowaly wielokrotnosc rocznego dochodu (Prestwich,
  "War, Politics and Finance under Edward I" - dokladnej liczby nie potwierdzilem w zrodle online; w literaturze pada rzad 750 tys. L -
  **DO SPRAWDZENIA**). Filip IV: wojna z Aragonia 1.5 mln lt, z Anglia 1294-99 1.73 mln lt; dlug u templariuszy = 17% dochodu [Wikipedia Coinage of Philip IV].
- Zold: konny 6 d - 2 s dziennie, piechur 2 d [cepr.org/voxeu]. Od lat 1280 coraz wiecej magnatow sluzylo za zold korony.
- Dlug: Edward III w 1340 zostal winien Bardim ~135 000 L i Peruzzim ~90 000 L i nie splacil; obaj upadli 1343-46 [Wikipedia Bardi].
- Podzial w pokoju (rzad wielkosci, z literatury - **szacunek**): dwor/garderoba 30-40%, dzielo krolewskie (zamki, palace) 10-20%,
  renty i lenna pieniezne (annuities, fees) 10-15%, dyplomacja i posly 5%, fundacje koscielne kilka %, obsluga dlugu reszta.
  W latach wojny 70-90% na wojne (zold, zaopatrzenie, subsydia dla sojusznikow).
### 4.2 Budowy krolewskie
- Zamki Edwarda I w Walii ~80 000 L lacznie (kilka lat calego dochodu): Harlech 8 190 L (1283-89), Caernarfon 20-25 000 L (1283-1330),
  Beaumaris ~11 000 L do 1300 i 15 000 L do 1330 (niedokonczony) [castlewales.com; Wikipedia].
- Beaumaris 1295 (list Jakuba z St. George): 400 murarzy, 2 000 robotnikow, 200 kamieniarzy w lomach, 30 kowali, 100 wozow, 60 wozow ciezkich,
  30 lodzi; **~250-270 L tygodniowo** w sezonie [Wikipedia Beaumaris; castlewales.com]. To ~64 000 d/tydzien na ~2 700 ludzi = ~3.4 d na czlowieka
  dziennie z transportem i materialem.
- Henryk III: opactwo Westminster 1245-72 > 40 000 L (~2 lata dochodu) [westminster-abbey.org / Colvin].
- Place budowlane c.1300: rzemieslnik 3-3.5 d/dzien, robotnik 1.5-2 d [Clark; Phelps Brown-Hopkins]. Sezon budowlany ~ kwiecien-pazdziernik,
  zima prace ograniczone (krycie murow sloma).
- Struktura kosztu budowy (Salzman, Knoop & Jones - **szacunek z literatury**): robocizna ~60-70%, material 20-30% (kamien, wapno, drewno, zelazo,
  olow), transport 10-15% (Beaumaris: sam transport materialow 2 100 L z 15 000 L = 14%).
### 4.3 Drogi, mosty, mury miast - kto placil
- Mosty i drogi NIE byly wydatkiem korony: obowiazek lokalny (Magna Carta art. 23: tylko ci, ktorzy "od dawna" byli zobowiazani); korona
  dawala **pontage** (myto na most), **pavage** (bruk ulic, rzadko drog) i **murage** (mury miejskie) - patent na kilka lat, myto od wozow
  i towarow; przyklad: Lancaster 1291 pontage na 5 lat dla Edmunda Crouchbacka, Maidenhead 1297 na 3 lata [Wikipedia Pontage; Pavage].
- Murage: 890 grantow doplat do myta, 133 zwolnienia z czynszu, 127 grantow pienieznych - zwykle z innych podatkow; gotowke z korony dostawaly
  tylko Berwick i miasta Gaskonii; Southampton 1202-03 dostal 100 L na waly [gatehouse-gazetteer.info].
- Statut z Winchester 1285 kazal panom poszerzac i wycinac pobocza drog miedzy miastami targowymi (wiedza ogolna - bez cytatu online).
- Wniosek dla gry: krol nie powinien "budowac drog za skarbiec" w calym krolestwie - realistycznie (a) DAJE myto (pontage/pavage), za ktore
  miasto/pan buduje, (b) doplaca do drog i mostow tylko na trasach wojskowych i w lennach korony.

---

## 5. PROJEKT: zachowany system budow i wydatkow korony [PROPOZYCJA]
Zasada: kazdy denar od kogos do kogos; material z polki targu (zdjety, zaplacony kasie miasta); robocizna placona kasom osad
(mieszczanie = kasa miasta, chlopi = kiesa wsi). Jedna zmiana naraz - kolejnosc w 5.8.

### 5.1 Cena punktu budowy
- Punkt budowy = jednostka pracy z materialem. Cena: **wojskowe/kamienne 48 d/pkt**, cywilne 24 d/pkt (ustawienia MCM `BuildPencePerPointStone`,
  `BuildPencePerPointCivil`). Podzial ceny: 60% place, 25% material (z targu), 15% transport (plac wozakom = kasa miasta/wsi najblizszej).
- Przyklady (pens): mury miasta 1->2 = 6000 x 48 = **288 000 d (1 200 L)**; mury 2->3 = 576 000 d (2 400 L); mury zamku 1->2 = 67 200 d (280 L -
  ~jedna wieza); rynek 0->1 = 57 600 d (240 L - hala targowa); koszary miasta = 43 200 d; spichlerz zamku 0->1 = 10 080 d (42 L - duza stodola).
  Skala zgodna z HIST: Harlech 8 190 L to ~2 mln d = ~7 poziomow murow miasta.
### 5.2 Postep ograniczony oplacona praca
- Hook: prefix na `BuildingsCampaignBehavior.TickCurrentBuildingForTown(Town)` (prywatna, vanilla) albo postfix na
  `BKConstructionModel.CalculateDailyConstructionPower` + wlasne pole. Lepiej: **prefix na `TickCurrentBuildingForTown`**, ktory liczy
  `pts = town.Construction` (model BK jak dzis = ile ludzi jest dostepnych), `cost = pts x cena`, placi i ustawia `town.BoostBuildingProcess`
  bez zmian; gdy platnik ma tylko czesc - `pts` proporcjonalnie, reszte dnia zwraca (return false i sami dodajemy `building.BuildingProgress`).
- Platnik: pan osady (`town.OwnerClan.Leader`), gdy kiesa < rezerwa (np. 30 dni zoldu) - kasa miasta ponad `TownRentFloorGold` (projekt miejski),
  w lennach krola dodatkowo skarbiec (5.4). Placa: 60% do `town.SettlementComponent.ChangeGold(+)` (zamek: do miasta-rynku/wsi zwiazanych, po rowno),
  15% transport do kasy najblizszej wsi zwiazanej.
- Material (25%): kupowany dziennie z polki targu osady (zamek: najblizszego miasta bez wojny - POPRAWIONA petla, czyta `val.Settlement.ItemRoster`):
  drewno (ladunek 100 kg), narzedzia, ruda/zelazo, glina, wapien. Ilosc = wartosc_materialu / cena_polki; zdejmowane z polki (`ItemRoster.AddToCounts(-)`),
  zloto do kasy miasta. Brak towaru na polce -> te punkty dnia nie powstaja (zamiast obecnego "zero, jesli brakuje calego zapasu").
  Ceny do poprawy w `HistoricalPrices.cs`: wapien (BK `limestone`, dzis 0) ~2 d za 10 kg (kamien+wapno, **szacunek**), marmur ~20 d, narzedzia ~40 d / 10 kg
  (`docs/SPIS-CEN-GRY.md` l.99: 5x za drogie).
- Wylaczyc BK, zeby nie liczyc dwa razy: prefix `return false` na `BKBuildingsBehavior.RunMaterials` (private) i `OnBuildingChanged` (private) oraz
  w `BKConstructionModel.CalculateDailyConstructionPowerInternal` zdjac blokade materialowa (postfix: jesli w opisie "Missing" - przelicz bez niej;
  prosciej: Harmony prefix na `GetMaterialRequirements` zwracajacy pusta liste gdy nasz system wlaczony). Wtedy w `PopulationLaw.KeptTownLines`
  materialy = 0 samoczynnie.
- Robotnicy: gdy BK workforce w wojnie = 0, zostawic (historycznie w wojnie budowano mniej - poza zamkami granicznymi, ktore buduje krol, 5.4).
- Czas [SZAC]: przy ~22 pkt/dzien miasta i placy 48 d/pkt pan placi ~1 050 d/dzien (renta miasta mediany ~3.7 tys./dzien, wpis 49) - mury 1->2
  w ~270 dni, jesli pan placi; inaczej stoi. Opcjonalnie sufit pkt/dzien wg sezonu: zima x0.3 (`Calendar.cs`).
### 5.3 Utrzymanie fortyfikacji
- Hook: prefix na `Building.HitPointChanged(float change)` gdy `change > 0` i budynek = Fortifications/Barracks/Siege Workshop: dzienny koszt
  = **0.4% rocznie wartosci zbudowanych poziomow / 364** (np. mury miasta poz.3 = 864 000 d -> ~9.5 d/dzien; zamek poz.3 = 201 600 d -> ~2.2 d/dzien),
  plus garnizon i tak placi pan. Niezaplacone -> `change = -0.3` zamiast +10 (punkty zycia spadaja; po ~330 dniach zaniedbania `LevelDown`).
  Platnik i odbiorca jak w 5.2 (place murarzy do kasy miasta). Liczby **szacunek** (Colvin: utrzymanie zamkow krolewskich to dziesiatki L rocznie na zamek).
### 5.4 Skarbiec - na co krol wydaje (nowy `KingdomTreasury.Spend()`, dzienny tick po `Levies`)
Rezerwa: skarbiec nie schodzi ponizej `TreasuryReserve` = 90 dni sredniego wplywu (dzis ~180 tys.) albo 500 tys. Kolejnosc priorytetow:
1. **Zold armii krolewskiej w wojnie** (HIST: 70-90% wydatkow wojennych). Dla kazdej `Army` krolestwa (`kingdom.Armies`) - partie czlonkow:
   skarbiec placi **50%** dziennego zoldu partii (`Campaign.Current.Models.PartyWageModel.GetTotalWage(party).ResultNumber`) glowie rodu
   (`party.LeaderHero.Clan.Leader.ChangeHeroGold(+)`), rodowi krola 100%. Sufit: 60% dziennego wplywu + nadwyzka nad rezerwa / 30.
   Przy ~12 partiach po 1 700 d zoldu na armie -> ~10 tys. d/dzien - tyle, ile dzis wplywa na krolestwo w wojnie (danina+clo).
2. **Dzielo krolewskie**: w lennach rodu krola i w zamkach/miastach granicznych (osada w promieniu X od wroga) skarbiec placi cene z 5.2
   zamiast pana (do 50% wolnej nadwyzki dziennie). AI krola dobiera kolejke: najpierw Fortifications w osadach granicznych w wojnie.
3. **Drogi i mosty** (nie z calego skarbca - HIST): (a) krol finansuje "Roads and Paths" (vanilla) tylko w lennach korony; (b) dla innych osad
   "patent na myto" = darmowe dla korony: osada z Roads and Paths w budowie dostaje +X% do licznika cel (`Town.TradeTaxAccumulated`) przez N dni,
   pobierane od kupcow (ten licznik juz jest prawdziwym przeplywem). Efekt drog: w `TerrainEase` kara terenu x(1 - 0.15 x poziom) w promieniu 8 j. mapy
   od osady z budynkiem Roads and Paths (szukanie najblizszej osady raz na godzine gry na partie, nie co klatke).
4. **Renty (annuities) dla wiernych wasali**: raz na 7 dni rodom z relacja z krolem >= 10 i tierem >= 2: tier 2 - 40 d/dzien, 3 - 80, 4 - 160,
   5 - 300, 6 - 450 (HIST: earl ~1000 grzywien/rok = 160 tys. d = ~440 d/dzien); laczny sufit 15% wplywow. Daje relacje +1 / wplywy.
   Zastapic tym vanillowa zapomoge < 30 tys. albo ja zostawic jako "pomoc w biedzie" (dzis nieczynna).
5. Dwor: krol co dzien bierze ze skarbca na dwor `0.3 x wplyw_dnia` do wlasnej kiesy (garderoba) - to zrownuje "kiese krola" i skarbiec;
   historycznie skarbiec i garderoba to te same pieniadze. Opcjonalne.
Log dzienny: `Korona: dzien N - wydatki: zold armii X, dzielo krolewskie Y, drogi Z, renty R, dwor D; skarbce razem S (zmiana +/-)`.
### 5.5 Zachowanie zlota - kontrola
Dzis wycieki do nicosci: koniec poziomu BK (`OnBuildingChanged` ChangeHeroGold), boost gracza (GiveGoldAction do null), `0.6 x autonomia` czesci materialow
z niczego. Po 5.2 wszystkie trzy znikaja (boost: prefix na `BuildingHelper.BoostBuildingProcessWithGold` - zloto do kasy miasta, nie null).
BEE projekty (wirtualny skarbiec) - do decyzji Jeffa: zostawic jako "osobna gra" albo wylaczyc AI projektow BEE (`TownProjectAiCheckIntervalDays`
bardzo duze w `better_economy_settings.xml`) - wtedy Roads BEE tylko za prawdziwe zloto naszym systemem.
### 5.6 Liczby do MCM (wszystko angielskie nazwy)
`BuildPaidConstruction` (true), `BuildPencePerPointStone` 48, `BuildPencePerPointCivil` 24, `BuildLabourShare` 0.6, `BuildMaterialShare` 0.25,
`BuildCartageShare` 0.15, `FortUpkeepPercentPerYear` 0.4, `CrownArmyWageShare` 0.5, `CrownReserveDays` 90, `CrownReserveMin` 500000,
`CrownAnnuityTier2..6`, `RoadTerrainEasePerLevel` 0.15, `RoadRadius` 8. Po dodaniu: `python3 tools/gen_mcm.py`.
### 5.7 Najpierw log (zasada 8.1 CLAUDE.md)
Przed jakakolwiek zmiana dodac TYLKO diagnostyke (CrashScribe lub Armoury `Logs/<sesja>/budowy.log`, raz dziennie): dla 10 losowych osad
`town.Construction`, rozpiska (`CalculateDailyConstructionPower(town, true).GetLines()`), biezacy projekt, postep/koszt, polityka workforce BK,
"Missing ..." i licznik ukonczonych poziomow dnia (event `OnBuildingLevelChanged`). To rozstrzygnie, czy budowy sa "za szybkie" i czy zamki stoja.
### 5.8 Kolejnosc wdrozen (jedna na DLL)
1. Log budow (5.7). 2. Ceny wapienia/narzedzi (`HistoricalPrices`). 3. Platne budowy 5.2 (z wylaczeniem materialow BK). 4. Wydatki korony 5.4 pkt 1
(zold) - od razu widac spadek skarbcow. 5. Renty 5.4.4. 6. Dzielo krolewskie 5.4.2. 7. Utrzymanie 5.3. 8. Drogi 5.4.3 (TerrainEase).

## 6. Hooki - spis [KOD = istnieje w podanym miejscu]
| Cel | Typ.metoda | Uwagi |
|---|---|---|
| Dzienny postep budowy | `TaleWorlds.CampaignSystem.CampaignBehaviors.BuildingsCampaignBehavior.TickCurrentBuildingForTown(Town)` private | prefix |
| Punkty budowy | `BannerKings.Models.Vanilla.BKConstructionModel.CalculateDailyConstructionPower(Town,bool)` | BKROT dziedziczy |
| Wymagania materialow | `BKConstructionModel.GetMaterialRequirements(Building)` public | pusta lista = brak blokady |
| Zakup materialow BK | `BannerKings.Behaviours.BKBuildingsBehavior.RunMaterials(Town)` private | wylaczyc |
| Rozliczenie konca poziomu BK | `BKBuildingsBehavior.OnBuildingChanged(Town,Building,int)` private | wylaczyc |
| Linia materialow w podatku | `Armoury PopulationLaw.KeptTownLines` | 0 po wylaczeniu RunMaterials |
| Boost gracza | `Helpers.BuildingHelper.BoostBuildingProcessWithGold(int,Town)` | zloto do kasy miasta |
| Utrzymanie | `TaleWorlds.CampaignSystem.Settlements.Buildings.Building.HitPointChanged(float)` | prefix |
| Skarbiec | `Kingdom.KingdomBudgetWallet` (int, set publiczny), nasz `KingdomTreasury` | nowe `Spend()` |
| Zold partii | `Campaign.Current.Models.PartyWageModel.GetTotalWage(MobileParty, ...)` | sygnatura do sprawdzenia w 1.4.8 |
| Armie | `Kingdom.Armies`, `Army.Parties` | |
| Drogi - ruch | Armoury `TerrainEase` postfix na `CalculateFinalSpeed` | |
| Drogi - budynek | `DefaultBuildingTypes.SettlementRoadsAndPaths` / `CastleRoadsAndPaths` | effect VillageProduction, VillageHeartsPerDay |
| Polityka BK | `BannerKingsConfig.Instance.PolicyManager.GetPolicy(st,"workforce")`, enum `BKWorkforcePolicy.WorkforcePolicy` (0 None,1 Land_Expansion,2 Martial_Law,3 Construction) | |

## Zrodla
- Riccardi, dochod Edwarda I, zold: https://cepr.org/voxeu/columns/credit-crunch-1294-causes-consequences-and-aftermath ; https://en.wikipedia.org/wiki/Riccardi_of_Lucca
- Zamki walijskie: https://www.castlewales.com/edward1.html ; https://en.wikipedia.org/wiki/Caernarfon_Castle ; https://en.wikipedia.org/wiki/Beaumaris_Castle ; https://www.castlewales.com/beau3.html
- Westminster: https://www.westminster-abbey.org/history/explore-our-history/architecture (Colvin, Building Accounts of Henry III)
- Place: https://faculty.econ.ucdavis.edu/faculty/gclark/papers/Condition.pdf ; https://core.ac.uk/download/pdf/12017678.pdf
- Pontage/pavage/murage: https://en.wikipedia.org/wiki/Pontage ; https://en.wikipedia.org/wiki/Pavage ; https://www.gatehouse-gazetteer.info/murage/muressay.html ; https://www.jstor.org/stable/10.7722/j.ctt81fk2 (Cooper, Bridges, Law and Power)
- Bardi/Peruzzi: https://en.wikipedia.org/wiki/Bardi_family
- Filip IV: https://en.wikipedia.org/wiki/Coinage_of_Philip_IV_of_France
- Prestwich, War, Politics and Finance under Edward I (1972); Salzman, Building in England down to 1540 (1952); Colvin, History of the King's Works -
  nie czytane w calosci online; liczby z nich oznaczone jako szacunek/DO SPRAWDZENIA.
- Dekompilacja: TaleWorlds.CampaignSystem.dll (1.4.8), BannerKings.dll, BKROTPatch.dll, BetterEconomy.dll, Bannerlord.Diplomacy.1.4.7.dll
  (ilspycmd, kopie w scratchpadzie sesji, nie w repo).
