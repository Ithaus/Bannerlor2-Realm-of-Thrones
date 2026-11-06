# Projekt 338 - naprawa budynkow (06.11.2026)

Podstawa: `docs/AUDYT-338-BUDYNKI.md`. Galaz `noc/338-budynki` od `091ed773` (WGRANIE 168), DLL testowe `C:\Users\GAME\bannerlord-autotest\test\m338`.
Rownolegle galezie 334e i 336 - 338 bedzie z nimi scalone, wiec caly kod w nowych plikach `Armoury/src/Buildings338*.cs`; w plikach wspolnych tylko
pojedyncze linie wywolan (wypisane przy kazdej grupie), wpisy kluczy w `Settings.cs` / `McmSettings.cs`, grupa `budynki338` w `tools/sprawdz_logi.py`, CHANGELOG.

## 0. Zasady wspolne

1. **Efekt z opisu budynku, liczby z gry/BK.** Gdzie sie da, wartosc liczy `town.AddEffectOfBuildings(efekt, ref e)` - to sama droga co w grze:
   poziomy 1-3 z definicji, poziom 0 pominiety, projekt dzienny tylko gdy jest biezacy (`Building.AddEffectOfBuilding:167`), perki modelu efektow
   (Contractors, Battlements, MasterOfPlanning) wchodza same. Dla budynkow BK bez efektow gry - wzor z kodu BK (jedno zrodlo prawdy).
2. **Nic z niczego.** Zloto tylko z istniejacych kies (kasa miasta, kiesa wsi), towary z rak ludzi (przesuniecie rak z innego plonu), konie z koni.
   Obnizki kosztow (zold) to oszczednosc, nie nowe zloto.
3. **Jeden system na efekt.** Kazda latka ma straznik "czy ktos juz to policzyl" (opisany przy grupie) i jest wpieta raz, na zewnetrznym poziomie
   lancucha (licznik zaglebienia jak `MountedWage._depth`, K8 `_k8Depth`, `BuildFunding._depth`).
4. **Klucze.** Wylacznik glowny `Buildings338` (domyslnie TAK) + osobny klucz na grupe (domyslnie TAK, chyba ze zaznaczono inaczej). Grupa dziala, gdy
   `Buildings338 && klucz grupy`. Wylaczenie przywraca stan sprzed 338 bez sladu w zapisie (zadna grupa nie zapisuje stanu, poza licznikami logu).
5. **Log.** Linia startu sesji `Budynki (338): start | grupy: zold TAK, autowerbunek TAK, ...` (SessionStart) i linia dobowa na grupe
   `Budynki (338) <grupa>: osad N z budynkiem, efekt X <jednostka>/d [, przyklad <osada> przed->po] [, bledy n]`. Pierwszy blad na grupe - pelny wyjatek
   do logu (Stumble), dalej licznik; grupa z bledem sie wylacza do konca sesji (jak inne paczki).
6. **sprawdz_logi.** Grupa `budynki338` (wejscie `m338`): obecnosc linii startu, kazda wlaczona grupa ma linie dobowa, `bledy 0`, kontrole liczbowe z
   punktow ponizej (zakresy w nawiasach przy kazdej grupie).

Kolejnosc wdrozenia (tory pracy wedlug ryzyka): **etap A** (S/M, male ryzyko, duzy efekt): grupy 1, 2, 4, 7. **etap B** (M): 3, 5, 6, 8.
**etap C** (L, gospodarka KS): 9, 10, 11, 12. Kazdy etap: build -> autotest 120 dob -> sprawdz_logi `budynki338` + pelny zestaw -> recenzja.

## Klucze (Settings.cs / McmSettings.cs, grupa MCM "Budynki (338)")

| Klucz | Dom. | Grupa |
|---|---|---|
| `Buildings338` | TAK | wylacznik glowny |
| `Buildings338Wage` | TAK | 1. zold zalog (koszary, biuro kasztelana, Slacken Garrison) |
| `Buildings338AutoRecruit` | TAK | 2. autowerbunek (biuro kasztelana, Train Militia, Raise Troops) |
| `Buildings338FoodStock` | TAK | 3a. zapas jedzenia (magazyn, spichlerz) |
| `Buildings338FoodDouble` | NIE (338c) | 3b. dubel BK 'Local farmland' (Farmlands, magazyn) - tylko razem z 3a; 338c: premia BK zostaje (p. 15 A) |
| `Buildings338Mason` | TAK | 4. punkty budowy murarza w budowach oplaconych |
| `Buildings338TaxOffice` | TAK | 5. urzad podatkowy miasta |
| `Buildings338Caravans` | TAK | 6. targowisko - wizyty karawan |
| `Buildings338Assimilation` | TAK | 7. asymilacja kulturowa (BK) |
| `Buildings338Roads` | TAK | 8. drogi - produkcja wsi pod KS |
| `Buildings338Ports` | TAK | 9. porty BK - ryby (zamki; 338c: miasta i zamki pod KS + Plan KS) |
| `Buildings338VillageProd` | TAK | 10. budynki produkcji wsi BK + projekt Production |
| `Buildings338VillageGoods` | TAK | 11. chleb (Bakery, Mill), skora (Fur Tannery), miod (Bee Skeps); 338c: takze rudy kopalni wsi BK (Mines) |
| `Buildings338VillageProject` | TAK | 10. projekt Production (338b) |
| `Buildings338CastleMines` | TAK (338c) | 9. kopalnie zamkow - produkcja z pracy (p. 15 C) |
| `Buildings338Studs` | TAK | 12. stadnina koni bojowych zamku |

---

## 1. Zold zalog - GarrisonWageReduction (Barracks miasto -5/-10/-15%, Barracks zamek -10/-20/-30%, Castellan's Office -10/-20/-30%, Slacken Garrison -5%)

**Plik:** `Buildings338Wage.cs`. **Klucz:** `Buildings338Wage`.

- **Gdzie:** jeden postfiks na zewnetrznym `GetTotalWage(MobileParty, ...)` zalogi - ta sama metoda i ten sam licznik zaglebienia co `MountedWage.TotalPostfix`
  (MountedWage wpina prefiks/postfiks we wszystkie modele lancucha BKROT -> ROT -> NavalDLC -> BEE -> BK i dziala tylko na zewnetrznym poziomie).
  Wpiecie: jedna linia w `MountedWage.TotalPostfix` przed `if (__0.IsCaravan)`: `Buildings338Wage.Apply(__0, ref __result);` (MountedWage juz ma
  straznik "przed"/wewnetrzne wywolanie tej samej partii). Bez osobnego postfiksu = nie ma ryzyka podwojnego liczenia przy BKROT -> BK.
- **Wzor:** gdy `party.IsGarrison && party.CurrentSettlement?.Town != null`: `town.AddEffectOfBuildings(BuildingEffectEnum.GarrisonWageReduction, ref __result)`
  - jak w grze (`DefaultPartyWageModel:147`): AddFactor -0.05..-0.30 do tego samego ExplainedNumber, w ktorym BK trzyma perki zalogi. Bez /100. Projekt
  dzienny Slacken Garrison liczy sie tylko, gdy jest biezacy (gra sama to sprawdza).
- **Poziomy:** miasto Barracks 1-3: -5/-10/-15%; zamek Barracks -10/-20/-30%; Castellan's Office -10/-20/-30%; Slacken Garrison -5%. Zamek z koszarami 3 i
  biurem 3 = -60% podstawy (jak w grze; AddFactor sumuje sie z innymi czynnikami tego wyniku).
- **Brak dublowania:** (a) w BK `val2` (mnoznik budynkow) przy obecnym kodzie = 1 zawsze; straznik: jesli w `__result` jest juz linia z nazwa ktoregos
  budynku z efektem 13 albo `GetBaseBuildingEffectAmount(13, poziom) > 0` dla ktoregos budynku (BK poprawil znak) - nie dodawac, licznik `pominiete (BK liczy)`.
  (b) Paczka 337 (galaz n337, nie scalona) ma opcje B `GarrisonBuildingWage337` (domyslnie wyl.) - to ten sam efekt. Przy scalaniu 337+338: 338 jest jedynym
  zrodlem; `GarrisonBuildingWage337` usunac albo `Buildings338Wage.Apply` pomija partie, gdy `GarrisonBuildingWage337 == true` (zapisac w CHANGELOG i scal.sh).
  (c) Czapa 337 (zaloga <= 0,80 pola) - liczona od wyniku z budynkami: budynki dzialaja przed czapa (postfiks MountedWage biegnie przed postfiksem 337,
  sprawdzic priorytety przy scalaniu; jesli 337 ma Last - wywolanie 338 przeniesc na poczatek `GarrisonWage337.Apply`).
- **Skutki ekonomiczne:** ok. 30 000-31 400 zl/dobe mniej zoldu w 213 twierdzach (at337). Zalogi z panem placa dzis ok. 0,50 pola (BK -50%), historycznie
  0,80; z koszarami zejda do ok. 0,35-0,45 pola - to nagroda za inwestycje w budynek, zgodnie z opisem. **Zeby AI nie napychalo zalog** (wszystkie hamulce
  zalog sa w zlocie: `GetAvailableWageBudget`, `IsWageLimitExceeded`, werbunek gry, K8) - mechanizm z projektu 337 p. 3.2(a): postfiks Priority.High na
  `ClanVariablesCampaignBehavior.UpdateClanSettlementsPaymentLimit(Clan)` (przed `ClanBudget.GarLimitPostfix`): `GarrisonWagePaymentLimit` kazdej twierdzy
  rodu AI x `s` = (zold z budynkami / zold bez budynkow) tej zalogi z ostatniego pomiaru (1,0 = brak budynku). Ta sama liczba ludzi, oszczednosc zostaje w
  kiesie pana (wiecej na druzyny w polu i budowy). Rod gracza bez skalowania (limit to wybor Jeffa) - uwaga w CHANGELOG. Kasy miast (`GarrisonPayToCoffers`)
  dostaja mniej - ten sam pieniadz zostaje u pana, Obieg sie domyka.
- **Log:** `Budynki (338) zold: twierdz N z budynkiem (miasto Barracks a, zamek Barracks b, biuro c, Slacken d), zold przed X -> po Y, oszczednosc Z zl/d,
  limit AI x s: n twierdz, pominiete (BK liczy) 0`.
- **sprawdz_logi:** Z w zakresie 20 000-40 000 zl/d przy ok. 210 twierdzach (bez czapy 337); `pominiete (BK liczy) 0`; liczba ludzi w zalogach AI po 120
  dobach w +-5% biegu bazowego (bez 338) - kontrola, ze zalogi nie puchna.

## 2. Autowerbunek - GarrisonAutoRecruitment (Castellan's Office +1/+2/+3, Train Militia +1, Raise Troops +2)

**Plik:** `Buildings338AutoRecruit.cs`. **Klucz:** `Buildings338AutoRecruit`.

- **Gdzie:** tylko na zewnetrznym poziomie `GetMaximumDailyAutoRecruitmentCount(Town)` - tam, gdzie dziala K8 (`ArmsFromPeople.K8Postfix:366`, licznik `_k8Depth`).
  NIE na `DefaultSettlementGarrisonModel` (ROT -> NavalDLC -> BK -> Default wola metode kilka razy, a K8 bierze max - efekt budynku by zniknal).
  Zmiana w ArmsFromPeople (jedna linia) - K8 z "max" na "dodaj premie": `if (lim > 1) __result = game + (lim - 1)` zamiast `if (lim > game) __result = lim`,
  a zaraz potem `__result += Buildings338AutoRecruit.Bonus(__0)` (takze w galeziach K8Off/K8Out/K8Brake - budynek dziala niezaleznie od K8).
- **Wzor:** `e = new ExplainedNumber(0); town.AddEffectOfBuildings(GarrisonAutoRecruitment, ref e); bonus = (int)Math.Round(e.ResultNumber)`.
  Limit dobowy = 1 (gra) + premia K8 (domyslnie +1) + budynki. Raise Troops/Train Militia tylko gdy biezacym projektem dziennym (gra sprawdza sama).
- **Poziomy:** Castellan's Office 1-3: +1/+2/+3; Train Militia +1; Raise Troops +2. Maks. zamek: 1 + 1 + 3 + 2 = 7 ludzi/dobe.
- **Brak dublowania:** nikt efektu 14 nie czyta (gra, BK, ROT, NavalDLC, BEE, Armoury). Straznik: jesli wynik na wejsciu zewnetrznego postfiksu > 1 przy K8Off
  (czyli ktos inny juz podniosl limit) - log `inny model podnosi limit`, budynek dalej dodaje (to nie jest ten sam efekt). Licznik `k8Extra` w A5LimitPostfix
  rozdzielic: ludzie ponad (1 + premia K8) zaliczac do `budynki`, nie do K8, zeby linia K8 w sprawdz_logi sie nie mylila.
- **Skutki:** to tylko wyzszy sufit. Werbunek dalej tnie: budzet zoldu (Wage Limit, z p. 1 skalowany), miejsce w zalodze, ochotnicy notabli, `FoodChange > 0`,
  flaga BK (AI wlacza autowerbunek tylko w wojnie i przy zalodze < 500). Ludzie sa z puli notabli tej ziemi (K8 zasady: tylko ludzie tej ziemi, nie gdy kraina
  bez wolnych rak). `CanonWarBalance.FrontGarrison` wola werbunek w petli z wlasnym progiem - wyzszy limit zmniejsza liczbe wywolan, nie mnozy efektu.
- **Log:** `Budynki (338) autowerbunek: twierdz N z premia (biuro a, Train Militia b, Raise Troops c), suma limitu +X/d, dobrano ponad K8 Y ludzi`.
- **sprawdz_logi:** N > 0 w 120 dobach; Y <= X x doby; ludzie w zalogach AI w +-5% biegu bazowego.

## 3. Jedzenie - zapas (Warehouse, Granary) i dubel BK (Farmlands, Warehouse)

**Plik:** `Buildings338Food.cs`. **Klucze:** `Buildings338FoodStock`, `Buildings338FoodDouble` (3b dziala tylko przy 3a, bo inaczej magazyn straci jedyny efekt jedzeniowy).

### 3a. Zapas jedzenia (FoodStock)
- **Gdzie:** postfiks Priority.Last na getterze `Town.FoodStocksUpperLimit` (postfiks biegnie mimo `return false` prefiksu BK `FoodStockPatch`).
  Tylko gdy BK przejal wynik: `PopulationManager.GetPopData(settlement) != null` (refleksja). Gdy null - gra liczy sama z budynkami, nic nie robic.
- **Wzor:** `__result -= 1000 x (poziom CastleGranary, gdy > 0)` [zdjecie czlonu BK]; `e = new ExplainedNumber(0); town.AddEffectOfBuildings(FoodStock, ref e)`
  - JEDNO wywolanie dla obu budynkow; `__result += K x e.ResultNumber`, K = 10 (skala BK: Granary 1000/poziom = 10 x 100 gry; sufit BK to 2 x ludnosc, wiec
  golde +100..500 byloby niewidoczne, ok. 1%).
- **Poziomy:** Warehouse 1-3: +1000/3000/5000; Granary 1-3: +1000/2000/3000 (jak dotad) + perki Battlements i Contractors (x10).
- **Brak dublowania:** czlon BK odjety dokladnie; AddEffectOfBuildings raz; sciezka popData == null nietknieta.
- **Skutki:** wyzszy sufit przesuwa progi liczone od niego: "dobrze nakarmieni" 80% (BKLoyalty:326, BKProsperity:80/228, KsHunger:243, TownGranary:564),
  niedobor 10/20% (racjonowanie BK, dokup EconomyPatches:628, PeopleMarket), ClusterFoodTracker 25/50/80%. W miastach z magazynem 3 trudniej o premie za pelne
  spichlerze, za to dluzej wytrzymaja oblezenie i glod. Do pomiaru w tescie: odsetek miast "dobrze nakarmionych" przed/po (+-5 pp).
- **Log:** `Budynki (338) zapas jedzenia: miast z Warehouse N (sufit +X), zamkow z Granary M (perki +Y), przyklad <miasto> sufit A -> B`.

### 3b. Dubel BK 'Local farmland'
- **Gdzie:** transpiler na `BKFoodModel.GetPopulationFoodProduction`: oba wywolania `DefaultBuildingTypes.get_CastleFarmlands` i `get_SettlementWarehouse`
  zamienic na statyczna metode zwracajaca null (petla nic nie znajdzie, AddFactor znika). Oczekiwane dokladnie 2 podmiany - inaczej nie latac, blad do logu.
- **Efekt:** Farmlands zostaje z efektem z opisu +6/12/18 jedzenia/dobe (`DefaultSettlementFoodModel:76`, juz dziala); magazyn - zapas z 3a zamiast premii
  produkcji spoza opisu. 'Local farmland' (praca ludnosci BK) liczy sie dalej, tylko bez premii budynku.
- **Skutki/ryzyko:** premia BK (x0.15 produkcji ludnosci x 1.5-2.5) bywa wieksza niz 6/12/18. Przed wlaczeniem na stale: pomiar w logu - ile jedzenia/dobe
  traca zamki i miasta, ile osad schodzi z bilansem Food < 0. Jesli > 5% osad wpada w minus - zostawic 3b WYL i zapytac Jeffa (jedzenie to gospodarka).
- **Log:** `Budynki (338) dubel jedzenia BK: zamkow z Farmlands N, miast z Warehouse M, zdjeta premia X jedzenia/d, osad z bilansem < 0: a -> b`.
- **sprawdz_logi:** linia transpilera `podmiany 2`; b - a <= 5% osad.

## 4. Murarz (Mason) - punkty budowy w budowach oplaconych

**Plik:** `Buildings338Mason.cs`. **Klucz:** `Buildings338Mason`. Wywolania z `BuildFunding.PowerPostfix` i `PowerIntPostfix` (po jednej linii, w galezi Covered).

- **Gdzie:** tylko galaz `Covered(town) == true` (tam BuildFunding zastepuje wynik). Galaz nieobjeta (lenna gracza przy PaidConstructionPlayer=false) - nic,
  bo tam `BKConstructionModel:431` juz liczy Masona.
- **Wzor:** budowa w Armoury to oplacona praca, wiec Mason ma zwiekszac wydajnosc tej pracy, a nie dawac darmowe punkty przy zerowej wyplacie:
  `mason` = suma `building.GetBuildingEffectAmount(ConstructionPerDay)` TYLKO dla typow `DefaultBuildingTypes.SettlementMason` / `CastleMason` (nie
  AddEffectOfBuildings - to wciagneloby punkty kopalni BK spoza opisu). `base` = suma pozycji Add wyniku BK przed mnoznikami (BaseNumber z wynikiem przed
  podmiana) - nie `ResultNumber`, bo po linii 431 ida mnoznik Battanii, kary lojalnosci i LimitMax(100). `k = mason / max(5, base - mason)`, gornie
  ograniczone `k <= 1` (Mason nie wiecej niz podwaja prace). Dopisek `Mason = k x (f + fl)` (f = punkty kupione przez pana, fl = robota ludu).
  `PowerIntPostfix`: `(int)((f + fl) x (1 + k) + EngExtra)` - dymek i rzeczywisty postep musza byc te same.
- **Poziomy:** miasto +3/6/9, zamek +2/4/6 punktow w modelu gry -> przy typowej bazie BK (ok. 10-15 pkt) to +20..60% wydajnosci oplaconej pracy.
- **Planowanie:** `EngPaidNeed` (`BuildFunding.Engineering.cs:110-117`) dzielic dodatkowo przez (1 + k) - pan nie przeplaca za punkty dane przez Masona.
- **Brak dublowania:** w galezi Covered zaden skladnik nie zawiera ConstructionPerDay; galaz nieobjeta nietknieta.
- **Kopalnie BK (+0.5/1/1.5 punktu budowy):** NIE przywracac - efektu nie ma w opisie budynku (opis: urobek rud). Odnotowac w CHANGELOG.
- **Skutki:** szybsze budowy przy tych samych pieniadzach w osadach z murarzem (nagroda za inwestycje). Nic z niczego: bez zaplaconej pracy Mason nie daje nic.
- **Log:** `Budynki (338) murarz: osad N z Masonem w budowach oplaconych, punktow +X/d (sredni k Y), pan zaoszczedzil Z zl`.
- **sprawdz_logi:** k <= 1 wszedzie; X / punkty oplacone ogolem <= 30%.

## 5. Urzad podatkowy miasta (Tax Office) +5/10/15% podatku

**Plik:** `Buildings338Income.cs`. **Klucz:** `Buildings338TaxOffice`.

- **Gdzie:** tam, gdzie u Jeffa pan i korona naprawde dostaja pieniadze miasta: rata firmy `DsTowns.Pay(...)` (DsTowns=true, rampa F3 = 1.000). Jedna linia
  w `DsTowns.Pay` po wyliczeniu `iL`, `iC` (przed RemitOf): `if (town) Buildings338Income.TaxOffice(t, ref iL, ref iC);`. Zapas: ten sam mnoznik w
  `PopulationLaw.ValveTowns` (udzial miasta) i w `takeShare` petli renty miast - tylko gdy droga firm nie dziala (`!DsTowns.FarmsLive`), zeby nie liczyc dwa razy.
- **Wzor:** `e = new ExplainedNumber(1f); t.AddEffectOfBuildings(TaxPerDay, ref e)` -> 1.05/1.10/1.15 (z perkami); `iL *= e`, `iC *= e`. Dalej ta sama droga
  co rata: zaplata z kasy miasta najwyzej z nadwyzki ponad zapas (`reserve` / `TownPurse.Reserve`), brak = zaleglosc jak dla raty (istniejaca logika).
  Budynek przyspiesza pobor z kasy miasta, nie tworzy zlota.
- **Brak dublowania:** nie ruszac `TownTaxPostfix` ani `CalculateTownTax` (inaczej efekt wszedlby do zastapionego podatku BK i do rady `CouncilWages35.TownBase`).
  Prognoza firmy (`DsNoteFarm`, ksiegi korony) - liczy z raty, wiec dostanie mnoznik raz; sprawdzic, ze nie mnozy drugi raz.
- **Skutki:** wiecej dochodu panow z rozwinietych miast, mniej zlota w kasie miasta (kasa i tak oddaje tylko nadwyzke). Obieg sie domyka.
- **Log:** `Budynki (338) urzad podatkowy: miast N, droga firm/zawor, rata +X zl/d (pan a, korona b), obcieta przez zapas kasy c zl`.
- **sprawdz_logi:** droga = firm (u Jeffa) i X > 0; dodatek <= nadwyzka kasy.

## 6. Targowisko - wizyty karawan (CaravanAccessibility 1.02/1.04/1.06)

**Plik:** `Buildings338Income.cs`. **Klucz:** `Buildings338Caravans`.

- Opis w podpowiedzi budynku obiecuje "wiecej wizyt karawan" (Native `module_strings.xml:6498`, NavalDLC `:130`), wiec to naprawa, nie nowy efekt.
- **Gdzie:** postfiks na `BKCaravansBehavior.GetTradeScoreForTown` (jedyne czynne miejsce wyboru celu karawany). Raz, bez lancucha.
- **Wzor:** liczby gry czytane jako mnoznik wyniku (jedyna sensowna interpretacja; AddFactor 1.02 na bazie 1 dalby x2.02): `__result *= max(1, wartosc)`,
  gdzie wartosc = `GetBuildingEffectAmount(CaravanAccessibility)` Marketplace miasta (1.02/1.04/1.06). Poziomy 1-3: +2/4/6% atrakcyjnosci miasta.
- **Brak dublowania:** czytnik wanilii nie biegnie (BK `HourlyTickParty_Skip`); Armoury (CaravanRoute294c, CaravanSea308, CaravanAmmo, CaravanBulk) wyniku nie
  modyfikuje pod budynki. Sprawdzic przy wdrozeniu kolejnosc z postfiksami tras karawan Armoury (mnoznik na koncu, Priority.Low).
- **Skutki:** maly przesuw ruchu karawan do miast z targiem (wiecej cel w tych miastach). Nic z niczego.
- **Log:** `Budynki (338) karawany: miast z Marketplace N, wywolan wyboru X, sredni mnoznik Y`.

## 7. Asymilacja kulturowa (BK, projekt dzienny `bk_building_daily_assimilation`)

**Plik:** `Buildings338Assimilation.cs`. **Klucz:** `Buildings338Assimilation`.

- **Gdzie:** postfiks na `CalculateCultureWeight(Settlement, CultureDataClass, float)` czynnego `BannerKingsConfig.Instance.CultureModel` (rejestracja w
  kampanii przez `PatchTarget.OfActive`, klasa deklarujaca BKROTCultureModel - jak FolkAidLoyalty).
- **Wzor:** gdy `settlement.IsTown || IsCastle`, `Town.CurrentDefaultBuilding?.BuildingType.StringId == "bk_building_daily_assimilation"` i
  `data.Culture == settlement.Owner.Culture`: `__result.Add(15, nazwa projektu)`. 15 = istniejaca liczba BK: Theater na poziomie 3 (5 x poziom, BKCultureModel:178),
  ta sama proporcja co w grze miedzy Festivalem (+3) a budynkiem lojalnosci na poziomie max; dla porownania gubernator +10, wies tej kultury +15.
- **Brak dublowania:** zaden system nie daje wagi za ten projekt; Theater to osobne zrodlo zalezne od poziomu.
- Dodawanie projektu do miast, gdy go brak (`BKBuildingsBehavior:486` pomija projekty dzienne) - najpierw policzyc w logu; gra dodaje typy w
  `BuildDevelopmentsAtGameStart`, wiec najpewniej jest. Dodawac tylko, gdy licznik pokaze braki.
- **Skutki:** szybsza asymilacja w osadach, gdzie pan swiadomie wybierze projekt zamiast innych (koszt: brak innego projektu dziennego).
- **Log:** `Budynki (338) asymilacja: osad z projektem w liscie N/M, wybrany w K, waga +15`.

## 8. Drogi (Roads and Paths) - produkcja wsi +5/10/15% pod KS

**Plik:** `Buildings338Roads.cs`. **Klucz:** `Buildings338Roads`.

- **Gdzie:** przy skladaniu stawek KS wsi (`VRate.A` w Plan/BuildLive) - wtedy Plan widzi wieksza podaz w cenach i rencie, a Opal, TownFolk.Opal i Ruda,
  ktore czytaja `a.A` wprost, tez. Jedna linia w miejscu skladania A: `A[i] *= Buildings338Roads.Factor(v)`. NIE w ProdStep (bylby dubel lub brak w Opalu).
- **Wzor:** `Factor(v)`: gdy `v.Bound?.IsFortification`: `e = new ExplainedNumber(1f); v.Bound.Town.AddEffectOfBuildings(VillageProduction, ref e)` ->
  1.05/1.10/1.15 (miasto i zamek, `DefaultBuildingTypes:241/294`).
- **Brak dublowania:** wsie poza KS i krainy wrocone - premie liczy juz model gry (`DefaultVillageProductionCalculatorModel:63-65`), Factor tam nie wchodzi.
  BlendStep (rampa) miesza gre (z Drogami) i KS (z Drogami) - kazda strona ma premie raz. Bloomery.Cap i RockGold.Cap dalej przycinaja po czynniku.
- **Skutki:** te same rece daja wiecej towaru we wsiach warowni z drogami (jak w opisie); nizsze ceny tych towarow w okregu. Nie przybywa ludzi ani zlota.
- **Log:** `Budynki (338) drogi: wsi KS z drogami N, dopisane sztuki X/d (wartosc Y zl/d)`.
- **sprawdz_logi:** X > 0; reszta kontroli KS (ceny, renta) bez nowych bledow.

## 9. Porty BK - ryby (Harbor miasta pod KS, Port zamku)

**Plik:** `Buildings338Ports.cs`. **Klucz:** `Buildings338Ports`.

- **Miasta (Harbor) pod KS:** w `A17Fix.PortsPre` blokada KS zalezna od klucza (jedna linia: `if (Buildings338Ports.On) <nie blokuj>`); dalej istniejaca
  sciezka krainy wroconej: ramka polowu A17, zloto zdjete przez BK idzie do kiesy ludu jako placa rybakow (`A17PortFishPaysFolk`, `TownFolk.Earn`, okno O19
  `CirculationWindows.PortsFin`) - nie dopisywac nowej zaplaty. Ryby to praca ludzi miasta za zloto kasy.
- **Zamki (Port):** BK nie wola RunPorts dla zamkow (`DailyTickTownEvent` tylko dla miast). Wlasny tick dobowy: petla po `Town.AllCastles` z budynkiem Port > 0,
  wzor RunPorts (pierwszy z Harbor/Port, `FirstOrDefault` jak BK): n = poziom x max(dzierzawcy/300, rzemieslnicy/200) z PopulationData BK; tylko gdy kasa zamku
  >= cena x n; kasa zamku placi cene -> kiesa ludu podzamcza (TownFolk.Earn), ryby do rynku/Stash jak BK. Petla zamkow pomija miasta, petla miast (BK) zamkow nie ma.
- **Plan KS:** w `StartCalibration.Plan` obok lodzi (ok. linii 200) jedna linia `Buildings338Ports.AddPlanFish(inp)`: dla kazdej osady z Harbor/Port > 0
  `AddExo(inp, kraina, KsTab.GFish, n x kg)` tym samym wzorem i warunkiem kasy. To jedyne miejsce, gdzie Plan liczy te podaz.
- **Jedna nazwa w ksiedze:** po zdjeciu blokady przejda dwie latki (ramka A17 FishFrame i `BkPortPostfix` ze StartCalibration.Arm -> GKnown). Ustalic jedno
  nazwane zrodlo: "porty BK (praca rybakow)"; `BkPortPostfix` nie zapisuje, gdy ramka A17 juz zapisala. Linia `Ryby z niczego (A17 F2)` nie moze pokazywac portow.
- **Log:** `Budynki (338) porty: miast N (ryb X/d), zamkow M (ryb Y/d), placa rybakow Z zl/d, bez kasy k osad`.
- **sprawdz_logi:** `zablokowane w KS 0`; X + Y > 0; Plan GFish zawiera porty (rozbieznosc Plan vs tick < 10%).

## 10. Budynki produkcji wsi BK (Farming, Mining, Animal Housing, Sawmill, Fish Farm +5/10/15%) i projekt Production +15%

**Plik:** `Buildings338Village.cs`. **Klucz:** `Buildings338VillageProd`.

- **Gdzie:** jeden krok w `MaterialLaw.ProdPostfix`, w dwoch miejscach tej samej metody (jedna linia kazde): (a) po `VillageCrafts.Base` (droga zwykla),
  (b) w galezi KS - przed `return` po `StartCalibration.ProdStep` (ProdStep zastepuje lancuch; bez tego efekt dalej martwy pod KS). Ten sam straznik `_depth`.
- **Wzor:** BK publiczne `PopulationManager.ApplyProductionBuildingEffect(ref x, item, popData.VillageData)` na pustym ExplainedNumber(1) -> czynnik
  1 + 0.05 x poziom budynku wlasciwego dla kategorii towaru (jedno zrodlo prawdy, przypisanie kategorii z BK). Projekt Production: gdy
  `villageData.CurrentBuilding.BuildingType.StringId == "bannerkings_daily_production"` (CurrentBuilding, nie CurrentDefault - w trakcie budowy nie dziala,
  jak w BK) +0.15. Skladanie wzorem Armoury "czynnik x suma" (mnozy wynik, nie sumuje sie z zima i regulatorem). Tylko wies Normal z danymi BK.
- **Brak dublowania:** warunek `!(Campaign.Current.Models.VillageProductionCalculatorModel is BKVillageProductionModel)` (gdy kiedys BetterEconomy wylaczony -
  BK liczy sam). Warstwa BK `VillageProductionPostfix` budynkow nie zna.
- **Skutki:** te same rece, wiecej plonu kategorii z budynkiem (+5..15%); projekt Production daje +15% kosztem braku innego projektu dziennego. Pod KS zmienia
  podaz okregu - Plan/ceny dostosuja sie same (podaz z wiekszej wydajnosci, nie z niczego).
- **Log:** `Budynki (338) produkcja wsi: wsi z budynkami N (Farming a, Mining b, AnimalHousing c, Sawmill d, FishFarm e), projekt Production p, dopisane X szt./d`.
- Okno BK (EconomyVM) pokazywalo te premie i dotad klamalo - po naprawie mowi prawde.

## 11. Nowe towary wsi BK: chleb (Bakery, Mill), skora (Fur Tannery), miod (Bee Skeps)

**Plik:** `Buildings338Village.cs`. **Klucz:** `Buildings338VillageGoods`.

- **Gdzie:** baza w `MaterialLaw.ProdPostfix` wzorem `VillageFruit.Base` (dla towaru z listy BK, ktorego nie ma w `VillageType.Productions`) - PO
  `StartCalibration.ProdStep` (dla wsi KS obnizka zrodlowego plonu musi dzialac na stawce KS, inaczej towar powstaje z niczego). Brama pelnego magazynu:
  dopisac te towary w `VillageFruit.CapPostfix` (jedna linia).
- **Chleb:** tylko gdy Bakery > 0 (opis: "Adds bread as production good"; BK-owe 0.1 x zboze bez piekarni to dodatek spoza opisu - nie). Waga = zboze x
  (0.1 + 0.15 x poziom Bakery) x (1 + 0.5 x poziom Mill) - wzor `GetProductions:357-371`; baza = waga x (ogniska + 1) x 0.5 (wzor modelu gry). Mill jest w
  wadze, wiec dziala bez osobnej liczby (Mill bez piekarni nadal nic - zgodnie z BK). Zboze tej wsi zmniejszone o te sama wartosc (HostFactor; receptura
  ziarno -> chleb 1.0, `StartCalibration.Crafts:76`). Nie psuc pamieci zboza `HamletKinds` (chleb na koncu listy).
- **Skora:** waga 0.5 x poziom Fur Tannery; baza jak wyzej; o te sama wartosc mniej futer/zwierzat tej wsi (HostFactor), gdy brak - proporcjonalnie z pozostalych plonow.
- **Miod (Skeps):** waga w `VillageFruit.WeightFor` = 0.5 + 1 x poziom Skeps (tylko drwale i trapperzy - jak BK); poziom przez uchwyty `BkSeal34`
  (`_getPopData`, `_getBuildingLevel`); ta sama waga w linii SessionStart. Jedno miejsce (nie dodatkowo w Buildings338 i w VillageFruit).
- **Brak dublowania:** czynny model Default daje 0 dla tych towarow; tick BK wola model dla pozycji listy - baza powstaje jednym torem. Warsztaty i rzemieslnicy
  miast (chleb, skora) to inne rece i inny surowiec. Warunek jak w 10 (nie przy czynnym BKVillageProductionModel). StartCalibration liczy chleb w GGrain - sprawdzic,
  ze nowy chleb wsi wchodzi do Plan jako przesuniecie ze zboza (suma wartosci GGrain bez zmian +-2%).
- **Log:** `Budynki (338) towary wsi: chleb wsi N (X szt./d, zboza -X'), skora M (Y, futer/zwierzat -Y'), miod z uli K (+Z)`; w liniach `Towary` pozycja 'wsie' dla bread i leather.
- **sprawdz_logi:** 'wsie' > 0 dla bread i leather, gdy N, M > 0; wartosc dodanego towaru ~ wartosc odjetego (+-10%).

## 12. Stadnina koni bojowych zamku (Warhorse Studs, BK)

**Plik:** `Buildings338Studs.cs`. **Klucz:** `Buildings338Studs`.

- **Najpierw pomiar:** RunStuds jest w `OnTownDailyTick` (`DailyTickTownEvent` - tylko miasta), a wymaga `IsCastle` - prawdopodobnie nie biegnie nigdy. W tescie
  liczyc wywolania prefiksu na RunStuds z zamkiem. To samo dla RunMines (Castle Mines) i orszaku (Retinue Barracks) - jesli 0 wywolan dla zamkow, dopisac je do
  audytu jako MARTWE i do tej samej petli zamkow (osobny krok, po decyzji na liczbach).
- **Gdzie:** wlasny tick dobowy po `Town.AllCastles` (ten sam co porty zamkow z p. 9) + prefiks na `BKBuildingsBehavior.RunStuds(Town)` zwracajacy false
  dla zamkow przy kluczu (gdyby BK kiedys wolal - brak podwojnego ticku).
- **Wzor (liczby BK):** zamek z Studs > 0 (bez wymogu braku rancza - opis mowi o pastwiskach i poziomie; dziala w 100% zamkow); limit = pastwiska/10000 +
  2 x poziom koni WarHorse w Stash; szansa 0.02/dobe. Typ konia: WarHorse kultury zamku -> z mapy `HarvestValve._studs` dla rancza wsi zamku -> kultura
  wlasciciela/krolestwa -> najblizsze ranczo -> dowolny WarHorse z ObjectManager (nigdy null - koniec NRE). Bez konia z niczego: hodowla zamienia 1 zwyklego
  konia (kategoria Horse) ze Stash zamku (najpierw) albo z rynku wsi zamku (zaliczyc jako przesuniecie w pomiarze HarvestValve) na 1 bojowego; brak zwyklego = brak przyrostu.
- **Log:** istniejaca linia HarvestValve `zamki BK n` (prefiks/postfiks 869-890 dalej licza) + `Budynki (338) stadniny: zamkow ze Studs N, konie +X, bez zrodla m, NRE 0`.

## 13. Bez naprawy (zapis w CHANGELOG)

- **Port (BK, zamek) - sila handlu:** zamek nie ma rynku, zaden konsument nie czyta sily handlu zamku. Naprawa = wymyslony efekt. Zmiana opisu BK - decyzja Jeffa.
- **Tax Office (wies BK):** podatek wylaczony celowo (`PolicyIncomeConserved`, zloto z niczego); oplaty targowe wsi pan juz dostaje w `VillageDuesPerHeadYear`
  ("court, mill and market dues") - nowy podatek od targu bylby dublem. Do decyzji Jeffa: (a) podpowiedz budynku "zastapione renta i oplata wsi", (b) urzad
  podnosi istniejaca oplate wsi (przeplyw z kiesy wsi, z budzetami DS/KS jak BkSeal34), (c) ukryc z listy budowy.
- **Courier Post (wies BK):** brak systemu wiadomosci zaleznego od odleglosci. Najpierw sprawdzic, czy da sie go zbudowac (lista `DefaultVillageBuildings.All`);
  jesli tak - ukrycie z listy budowy do decyzji Jeffa (ryzyko zapisu: `ReInitializeBuildings` robi GetById po All).
- **Kopalnie BK - punkty budowy:** spoza opisu, nie przywracac (p. 4).
- **NIEPOTWIERDZONE** (Smith, Butter Mill, Mines wsi, projekty Farmland/Pastureland/Woodland) - bez zmian w 338; Smith i Mines wsi do pomiaru w tescie
  (wsie z kuznia, kategoria 'kuznia BK'), akry wsi - osobny audyt.

## 14. Test i odbior

- Autotest 120 dob na kopii zapisu (`autotest-przekierowany-*`, sprzatac po serii), DLL z `test\m338`, bieg bazowy bez 338 (`Buildings338=false`) dla porownania.
  Nigdy nie wgrywac do gry, nie zamykac gry Jeffa, nie ruszac `Ironman0gXONzURi8yO.sav`.
- `python tools/sprawdz_logi.py m338 <log>`: grupa `budynki338` (linie i zakresy z p. 1-12) + pelny zestaw (zold 337/K8, KS, Obieg, HarvestValve, A17, jedzenie).
- Kontrole gospodarcze vs bieg bazowy: ludzie w zalogach AI +-5%; zloto rodow AI rosnie o ok. oszczednosc zoldu (bez wzrostu zalog); osad z Food < 0 <= +5%;
  ceny chleba/skory/ryb w okregach KS spadaja, nie rosna; Obieg 0 roznicy.
- Recenzja (tor M/L): sprawdzic straznikow dubli (p. 1 BK znak, p. 2 K8 vs budynki, p. 3 jedno AddEffectOfBuildings, p. 8 tylko w A, p. 9 jedna nazwa w ksiedze, p. 10 model BK).

## Pliki

- nowe: `Armoury/src/Buildings338.cs` (klucze, SessionStart, linia dobowa, Stumble), `Buildings338Wage.cs`, `Buildings338AutoRecruit.cs`, `Buildings338Food.cs`,
  `Buildings338Mason.cs`, `Buildings338Income.cs`, `Buildings338Assimilation.cs`, `Buildings338Roads.cs`, `Buildings338Ports.cs`, `Buildings338Village.cs`, `Buildings338Studs.cs`
- jednolinijkowe wpiecia: `MountedWage.cs` (TotalPostfix), `ArmsFromPeople.cs` (K8Postfix + licznik A5), `BuildFunding.cs` (PowerPostfix, PowerIntPostfix),
  `BuildFunding.Engineering.cs` (EngPaidNeed), `DsTowns.cs` (Pay), `PopulationLaw.cs` (ValveTowns, takeShare - zapas), `A17Fix.cs` (PortsPre),
  `StartCalibration.Plan.cs` (AddPlanFish; skladanie A - Drogi), `StartCalibration.Arm.cs` (BkPortPostfix - jedna nazwa), `MaterialLaw.cs` (ProdPostfix x2),
  `VillageFruit.cs` (WeightFor, CapPostfix), `ClanBudget` (kolejnosc z postfiksem limitu - tylko sprawdzic priorytet)
- `Settings.cs`, `McmSettings.cs` (14 kluczy), `tools/sprawdz_logi.py` (grupa `budynki338`), `CHANGELOG.md`, `docs/STAN-PRAC.md`

## 15. Paczka 338c - po tescie 30 dob (at338)

Test 338b (`at338`, Armoury-2026-11-06_19-36-12.log): kod 0, 23 bledy CS = baza; problemy: B338-7 (glod po zdjeciu dubla BK), B338-5 (prog stala za ciasny), martwe dalej
porty zamkow (0 zbudowanych), porty miast pod KS (blokada A17), kopalnie zamkow, chleb, skora, rudy kopalni wsi pod KS; linie zamkow i stadnin bez sum biegu.

**A. Dubel jedzenia BK - DECYZJA (Claude projektuje ekonomie).** Zdjecie premii BK 'Local farmland' (Farmlands zamku x0.5/poziom, Warehouse miasta x0.3/poziom produkcji
ludnosci) = ok. 10 100 jedzenia/d mniej i 172 zamiast 82 z 219 osad z ujemnym bilansem. Premia zostaje: to sposob, w jaki BK skalibrowal swoj model jedzenia, a efekt "jedzenie"
jest zgodny z duchem opisu farm. +6/12/18 gry dla Farmlands zostaje, zapas magazynu/spichlerza (3a) zostaje. `Buildings338FoodDouble` domyslnie NIE (CHANGELOG). Linia 3b podaje
klucz; przy NIE liczy tylko osady z budynkami. sprawdz_logi B338-7: przy kluczu NIE premia nietknieta kazdej doby (zdjeta 0, bilans bez zmiany); przy TAK - stary prog 5% osad.

**B. B338-5 z liczby budynkow.** Prog = sredni zold twierdzy z linii (zold przed / twierdz) x suma obnizek z opisu dla policzonych budynkow (koszary miasta 5..15%, zamku
10..30%, biuro 10..30%, Slacken 5%): dol = poziom 1 x 0.8, gora = poziom 3 x 2 x 1.2 (AddFactor obnizki sumuje sie z czynnikiem BK -50% zalog, wiec wzgledem zoldu po BK
obnizka moze siegac 2x opisu), nie wiecej niz zold przed. at338: 21 866..196 570 zl/d, wynik 41 972 - TAK.

**C. Martwe budynki - produkcja z pracy, liczby z opisu i modeli, nic z niczego.**
- *Porty BK pod KS (miasta i zamki)* - `A17Fix.PortsPre` nie blokuje przy `Buildings338Ports.KsOpen` (= klucz Buildings338Ports). Ryby: wzor BK RunPorts z pracy ludzi
  (poziom x max(dzierzawcy/300, rzemieslnicy/200)), za zloto kasy miasta/zamku, ktore idzie do kiesy ludu (place rybakow; pod KS zawsze - bez zlota w nicosc). Ksiega towarow:
  ramka z grupa `porty BK - praca rybakow (338)|` (nie "ryby z niczego poza KS", nie grupa lodzi = podaz Planu). Plan KS: `StartCalibration.Plan.PortFishExo` (miasta i zamki)
  dodaje do podazy krainy n x kg x sigma tym samym wzorem i warunkiem kasy (`Buildings338Ports.PlanFishPerDay`). Linia A17 F2: nowy segment `338c porty BK pod KS`.
- *Port zamku* - tick 338 bez zmian (BK RunPorts dla zamku); w 30 dobach zaden zamek go nie zbudowal. Linia podaje "Port na liscie budowy" i sumy biegu.
- *Kopalnie zamkow* - NIE BK RunMines. Losowanie BK z opisu (udzial zloza / (bogactwo + 0.5), poziom = sztuki), tylko z wolnych rak zamku (BK `LandData.WorkforceExcess`);
  rece na sztuke = kg sztuki x sigma KS / kg gornika na dobe z KsTab.P0 (zelazo 2, srebro 15, sol 60, glina 800, wapien 300, marmur 150, zloto 0.3). Kasa zamku stac na cene
  targu - placi gornikom (TownFolk.Earn KMine, reszta wraca do kasy), ruda na targ zamku; inaczej ruda do Stash (opis), bez zlota. Klucz `Buildings338CastleMines` domyslnie TAK.
  Plan KS zamkow nie liczy (zamki poza Planem) - ruda zamku zostaje na targu/w Stash zamku.
- *Chleb (Bakery, Mill), skora (Fur Tannery), rudy kopalni wsi BK (Mines)* - `Buildings338Village` (klucz Buildings338VillageGoods). Waga BK = udzial rak wsi (BK dzieli sile
  robocza na pozycje listy waga / suma wag). Chleb: w = 0.15 x piekarnia x (1 + 0.5 x mlyn), s = w / (1 + w) zboza wsi -> chleb (kg za kg, receptura KS ziarno -> chleb 1.0);
  bez piekarni nic. Skora: s = 0.5 x garbarnia / (wagi skor i futer typu (min 1) + 0.5 x garbarnia) skor i futer -> skora (2.4 kg surowca na kg skory); bez surowca nic. Rudy:
  baza 0.5 x poziom x udzial zloza x (poziom ognisk + 1) x 0.5, wartosc (cena bazowa) zdjeta z innych plonow wsi (inne najwyzej 15%, zboze i ryby 5%; gdy prog wiaze - mniej rudy).
  Pod KS (beta 1): w tablicy stawek A (`ScaleRates` -> `ScaleGoodsKs`, raz na przeliczenie okregu), chleb i skora - wlasna stawka (`KsGoods`, pierwszy krok MaterialLaw.ProdPostfix);
  Plan (mk, mkByM) widzi A, a zboze i skory/futra zamienione na przetwor wracaja do Planu (`PlanBack` - Plan liczy przetwory z surowca). Poza KS: baza (`GoodsBase`) i czynnik
  plonu-zrodla (`HostFactor`) w MaterialLaw, ruda / Times (MineralOnce). Magazyn wsi + 5 dob (`CapAdd` w VillageFruit.CapPostfix). Rampa KS (0 < beta < 1): obie strony z budynkami.

**D. Log.** `Budynki (338) zamki`: zamki tykane przez 338 dzis i w biegu, porty zamkow (zbudowane, na liscie, pod KS, ryby dzis i w biegu), porty pod KS (miasta i zamki - z A17
PortsFin) i Plan KS ryb portow, kopalnie zamkow (tykane, z urobkiem, ruda na targ / Stash, place, bez wolnych rak, w biegu). `stadniny`: suma biegu. `towary wsi`: chleb, skora,
rudy kopalni wsi (KS / poza KS, wartosc dodana i zdjeta).

**E. Bieg bazowy.** `Buildings338=false` (wszystkie grupy 338 i 338c wylaczone, linie doby dalej sa) - porownanie: B338-15 zalogi AI +-5%, B338-16 ceny zboza i drewna +-15%,
B338-20 zbytki ludu (OB2-6a: miasta + podzamcza + wsie, srednia na wspolnych dobach) >= 95% bazy, B338-21 rachunki KS-H po zwloce (NKA-4) - dob z dlugiem po zwloce <= baza.
Uzycie: `python -I tools/sprawdz_logi.py --grupa budynki338 <log> --baza <log z Buildings338 wyl.>`.

**Nowe progi sprawdz_logi:** B338-13 (sumy biegu), B338-17 porty pod KS (zablokowane 0 w liniach A17; gdy Plan > 0 - ryby pod KS w biegu > 0), B338-17b Plan ryb portow wobec
polowu (+-25%, 10 dob z Planem), B338-18 kopalnie zamkow (klucz TAK, kopalnie > 0, >= 10 dob - ruda w biegu > 0), B338-19 chleb/skora/rudy wsi (chleb z ziarna, skora > 0, wartosc
rudy = zdjete z innych plonow +-10%), B338-20, B338-21; synteza B338-S z nowymi przypadkami.

**Ryzyka 338c:** Plan KS liczy ryby portow od przebudowy Planu - do tego czasu podaz ponad Plan (ceny ryb w dol); chleb wsi zmniejsza zboze na targu (Plan widzi zboze
jak dotad - PlanBack); ruda wsi zabiera do 15% wartosci innych plonow wsi z kopalnia; kopalnie zamkow zaleza od wolnych rak (WorkforceExcess) - zamki bez nadmiaru rak nie kopia
(licznik "bez wolnych rak"); stawka KS chleba/skory/rud zmienia sie dopiero przy przeliczeniu okregu (jak drogi).
