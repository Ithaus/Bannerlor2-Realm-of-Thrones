## 2026-10-18 (noc 17/18.10, r4-glodbunt: glod miasta -> niepokoj i bunt mechanizmem gry; pan miasta placi kupcom za dowoz jedzenia)

**Mod:** Armoury + `tools/sprawdz_logi.py` (grupa `nglod`) | **Galaz:** `w-toku/r4-glodbunt` (od `efbbf0dc` = noc-ks4 "KS runda 4 cz. 5") | **Tor:** L (bez gry - test 120 dob prowadzi nocny lead)
| **Klucze (nowe, sekcja MCM "Hunger and the town lord (KS round 4)"):** `KsHungerUnrest` (tak), `KsHungerLoyaltyMax` 4, `KsHungerSecurityMax` 2, `KsHungerTolerance` 0.1,
`KsLordFundsFood` (tak) - wszystkie dzialaja TYLKO przy zastosowanej kalibracji KS (w grze Jeffa po WGRANIU 76 KS nie jest zastosowany - nic sie nie zmienia) | **Zapis:** nic nowego
(srednia glodu i doplaty wczoraj - pamiec sesji; po wczytaniu pierwsza doba bez linii "Hunger").

**Dla Jeffa (prosto):**
- Miasto, w ktorym na targu jest mniej jedzenia niz mieszczanie zjadaja w jeden dzien (liczy sie kazde jedzenie - glodny je tansze: chleb, kasze, ryby zamiast miesa), albo ktorego
  spichlerz jest pusty, dostaje w lojalnosci i w bezpieczenstwie pozycje "Hunger". Im wiecej ludzi bez jedzenia, tym mocniej: przy pelnym glodzie okolo -40 lojalnosci i -30
  bezpieczenstwa na stale. Do 10% braku nic sie nie dzieje (kuchnie, ogrody). Bunt robi gra sama (lojalnosc 15 i milicja silniejsza od zalogi) - nie ma nowego buntu.
- Kara gry za pusty spichlerz ("Starving") nie liczy sie dwa razy; premia "Well fed populace" nie dziala, gdy ludzie gloduja.
- Gdy miasto jest glodne, a jego kasa nie zaplaci kupcom, pan miasta doplaca kupcom za jedzenie, ktore naprawde przywioza - ze zlota ponad zapas na wojne i nie szybciej niz jego
  skrzynia wojenna (jak chleb dla biedoty). Miasto placi najpierw ze swojej kasy, pan reszte. Niewydane pieniadze wracaja do pana od razu, nigdy nie leza w kasie miasta.
  Twoja kiesa nigdy - swoje miasta karmisz sam (kupujac i wiozac jedzenie).
- Jedna regula dla kazdego miasta swiata. Gdy jedzenia nie ma nigdzie (glod globalny), kupcy nie maja czego wiezc - decyduje podaz i popyt.

**Problem:** decyzja Jeffa 17.10 (wiazaca, STAN-PRAC 82): "nie robmy sztucznego prawa, jak gloduja to znaczy ze musza jesc tansze jedzenie, a jak nie ma jedzenia to bedzie bunt;
w interesie miasta lorda jest aby dowiesc jedzenie ... chyba ze jest globalny glod". Pod KS mieszczanie jedza z targu (koszyk ZC4), a glod targu nie mial zadnego skutku politycznego:
lojalnosc gry/BK zna tylko spichlerz gry (Food). Szacunek z `ks-miasta-towary.csv` testow fin78 (15-37-31) i fin80 (16-26-10): 11-24 z 97 miast ma kcal na polce < 1 dnia
popytu zaplaconego (najgorsze: Zelazne Wyspy town_S4/S5/S6 0.0-0.1 dnia, Dorne town_A* 0.36-0.47, Polnoc town_B5 0.41, ROT_town23 0.23). Kupcy KS-H nie przywoza, gdy
kasa miasta za mala (NKS-22 "kasa celu za mala" 37.8% w fin80) - nikt nie placil za dowoz glodnego miasta.

**Przyczyna:** (1) brak pomiaru glodu miasta pod KS (sa: spichlerz gry Food 0, glod okregu E4/6, glod z braku pieniedzy E5/C2 - zadna nie mierzy braku jedzenia na targu);
(2) budzet KS-H to tylko 15% kasy ponad zapas kupcow (KsFoodCore.Decide) - pan miasta, ktory ma zloto, nie byl platnikiem.

**Zmiana:**
- `KsHunger.cs` (nowy, klucz `KsHungerUnrest`):
  - POMIAR: postfiks `ItemConsumptionBehavior.UpdateDemandShift` (Priority.Last, tuz przed konsumpcja doby miasta): kcal koszyka mieszczan (ZC4 `zt.Q` zaplacony z Planu x lambda,
    pozycje z kcal > 0) wobec kcal jedzenia koszyka na polce (`StartCalibration.KsTownMeal`); brak rynku u_r = max(0, 1 - polka / koszyk), srednia kroczaca 1/3 (ok. 3 dob).
    Kcal sa wymienne - brak pieniedzy na drozsze jedzenie nie jest glodem (to kotwica kiesy, drabina ZC4, chleb M3). Tylko miasta z koszykiem ZC4 po rampie (lambda >= 0.999).
  - SPICHLERZ GRY: przy Food 0 u_s = niedobor / spozycie (wzor Famine.cs: dobrobyt/40 + zaloga/20). u = max(u_r, u_s); f = (u - prog) / (1 - prog).
  - SKUTEK: postfiksy na `CalculateLoyaltyChange` / `CalculateSecurityChange` CZYNNYCH modeli (PatchTarget.OfActive - klasa deklarujaca; w kampanii z OnSessionLaunched; wlasny
    Harmony `com.jeff.armoury.ksglod`, sprawdzenie GetPatchInfo): pozycja "Hunger" - lojalnosc min(0, -4 f - czlon jedzenia gry), bezpieczenstwo -2 f. Czlon jedzenia gry
    sprawdzony w dekompilacji: BKLoyaltyModel.GetSettlementLoyaltyChangeDueToFoodStocks (Food >= 80% limitu +0.5 "Well fed populace", Food 0 -2 "Starving"), model gry
    (-1, po 14 dobach -2); bezpieczenstwo (DefaultSettlementSecurityModel + BKSecurityTweakPatches + NavalDLCSettlementSecurityModel) czlonu jedzenia nie ma.
  - Bunt: bez nowego kodu - RebellionsCampaignBehavior (lojalnosc <= 15 i milicja >= 1.4 x zaloga z druzynami; stan buntu <= 25), BK dla zamkow; 50 000 przywodcy z kasy
    miasta (RotLeaks37 `RebelPurseFromTown`) - pieniadze pana nie leza w kasie poza nawiasem zakupow, wiec bunt ich nie bierze.
  - Linie: "Glod miast (KS): start sesji" (latki) i "Glod miast (KS): dzien N" (miasta w KS, brak rynku mediana/q90, glodne, kara lojalnosci i bezpieczenstwa, Food 0, kara gry
    Starving / Well fed i kontrola podwojnego liczenia, lojalnosc glodnych, stan buntu, bunty od wczoraj z lojalnoscia wczoraj, wywolania modelu, czas, potkniecia).
- `KsLordFood.cs` (nowy, klucz `KsLordFundsFood`) - nawias wokol `KsFoodCarts.Daily` w `SupplyDemand.DailyTrade` (2 linie: `Before` / `After`; `KsFoodCore.cs` NIE zmieniony,
  `KsFoodCarts.cs` - po recenzji 2 nowe odczyty `ReviewDay` i `PaidAsDstOn`, nic istniejacego nie ruszone):
  - KTO: miasto glodne (f > 0) z luka rynku dzis, ktorego budzet KS-H (wzor KsFoodCore: 15% ponad zapas kupcow, staple do polowy zapasu + 5%/d) < 7 x wartosc luki (ziarno po
    cenie polki). PLATNIK: glowa rodu wlasciciela, ponad rezerwe wojny R (ClanBudget.ChestOf) i tylko z czesci tempa skrzyni nie dla zoldu (TownFolk.BreadLordShare -
    jak chleb M3) minus chleb dzis, splata TP dzis, doplaty dzis i rezerwacje innych miast tego rodu; nigdy gracz; bez budzetu / dluznik / zajety / Straz / najemnik - nie.
  - TRYB DOPLATA (kod KS-H tej galezi - zaplata przy wysylce): Before - najmniejsza wplata X (glowa -> kasa), przy ktorej budzet KS-H kasy rosnie o e = min(tempo, potrzeba);
    After - wydatek kasy w nawiasie = kasa przed + X - kasa po; kasa placi najpierw do swojego budzetu, pan reszte (najwyzej e i X), reszta X wraca do glowy od razu.
  - TRYB RACHUNKI (po scaleniu z r4-kupcy): gdy ustawione `KsLordFood.FoodBillsOpen` i `FoodBillsPay`, Before splaca otwarte rachunki zywnosci glodnego miasta z kiesy pana
    (najwyzej tempo), bez wplaty do kasy. Bezpiecznik: gdy w `KsFoodCarts` / `KsFoodCore` sa skladowe o nazwach z bill/rachun/credit/kredyt/debt/owed/overdue/zwlok/zaleg, a
    rachunki nie sa podlaczone - doplata WYLACZONA (linia "KREDYT KUPCOW NIEPODLACZONY", NGL-6d = NIE).
  - Linia "Pan miasta placi za dowoz (KS): dzien N": glodne / kasa sama starcza / bez luki / do doplaty, platnicy, wplacone - zwrocone = zaplacone, wydatek KS-H tych kas (z kasy /
    od panow), rachunki, kontrola (zloto panow + kasy miast + wierzyciele = 0), ponizej rezerwy, ponad tempo, zwrot uciety, zaleglosc nawiasu, czas, potkniecia.
- `StartCalibration.Glod.cs` (nowy, partial): `KsTownMeal` i `KsTownGrain` - tylko odczyt mapy koszyka ZC4 i polki.
- `ClanBudget.cs` (1 linia): skrzynia sprzetu w wojnie minus `KsLordFood.RecentPaid(c)` obok chleba M3 (pan w sumie nie szybciej niz skrzynia; kupcy biegna po budzecie doby -
  doplata z ostatniej doby); klucz wylaczony - 0. `TourneyPay.cs`: `RepaidToday` private -> internal (tempo wspolne z TP).
- `ArmouryBehavior.cs` (1 linia): `KsHunger.EnsureHooks()` w OnSessionLaunched po FolkAidLoyalty (latki w kampanii, stan sesji od nowa). `SupplyDemand.cs`: nawias Before/After.
- `Settings.cs` + `McmSettings.cs` (gen_mcm): 5 kluczy. `tools/sprawdz_logi.py`: grupa `nglod` (NGL-0..9, NGL-1b, NGL-6b/6c/6d/6e), opis w naglowku.

**Recenzja: 3 uwagi, poprawione 2, odrzucone 1** (poprawki: commit po `e5247958`; build rc 0; `nglod` na 15-37-31 - INFO bez wyjatku, na sztucznym logu z liniami jak w C# -
16 progow TAK, a z wstawiona "kasa netto ponad ksiege 1" - NGL-6c NIE, czyli kontrola dziala):
- (wazne, ODRZUCONA - przeslanka nieprawdziwa, sprawdzone w dekompilacji) "u_s liczony od spozycia gry, a Food Jeffa jest z populacji BK -> u_s 2-4x za duze". `Town.FoodChange`
  = `SettlementFoodModel.CalculateTownFoodStocksChange(this)`; czynny model `ROTSettlementFoodModel` wola poprzedni (`BKFoodModel`) i dodaje tylko +100/+120 dla town_EW4 /
  ROT_castle61; `BKFoodModel.CalculateTownFoodStocksChange` -> `CalculateTownFoodChangeInternal` = baza `DefaultSettlementFoodModel` (spozycie dobrobyt /
  `NumberOfProsperityToEatOneFood` + zaloga / `NumberOfMenOnGarrisonToEatOneFood`, BK nadpisuje je na 40 i 20, ROT przekazuje) + tylko "Local farmland" (15% produkcji ziemi BK).
  `GetPopulationFoodConsumption` (chlopi -0.02, rzemieslnicy -0.04, szlachta -0.075, dzierzawcy -0.03) wola tylko `GetFoodEstimate` i `BKSettlementBehavior.HandleExcessFood`
  (ta ostatnia wychodzi od razu, gdy czynny model nie jest `BKFoodModel` - u Jeffa jest ROT) - do bilansu Food nie wchodzi; BK/ROT/BKROTPatch nie lataja bazy. Nasze postfiksy
  na bazie (VillageBook, PeopleMarket, WinterBite, Famine, SiegeWalls2) zmieniaja produkcje albo dodaja zime (0.5 / 1000 dobrobytu = najwyzej +2% spozycia). Mianownik
  dobrobyt / 40 + zaloga / 20 JEST wiec spozyciem bilansu Food (jak Famine.cs). Utwardzenie bez zmiany liczb u Jeffa: stale czytane z czynnego modelu (`ReadFoodModel`,
  fallback 40 / 20), linia startu podaje model i "zgodne / NIEZGODNE ze wzorem Famine.cs (40, 20)", nowy prog NGL-1b. Zimy nie dodalem do mianownika (jedna zasada z Famine.cs;
  roznica <= 2%).
- (drobna, POPRAWIONA) doplata codziennie, a cel KS-H zamawia tylko w dobie przegladu (parzystosc Fnv, `KsFoodCore.Decide` pomija `!Review`) - potwierdzone: poza ta doba wplata X
  (ok. 6.7 x e) wracala cala. Teraz tryb DOPLATA tylko w dobie przegladu (`KsFoodCarts.ReviewDay` - ta sama parzystosc co Daily), licznik "poza doba przegladu KS-H (cel co 2.
  doba)" (NGL-6e INFO); tryb RACHUNKI bez zmian (splata wierzycieli nie jest pustym obiegiem). Limit zostaje tempem JEDNEJ doby (opcji 2 x tempo nie wzialem: ClanBudget zdejmuje
  doplate ze skrzyni sprzetu jednej doby z obcieciem do 0, wiec 2 x tempo raz na dwie doby pozwoliloby panu wydawac szybciej niz skrzynia - zlamana regula M3). Skutek: pan
  doplaca jednemu miastu najwyzej tempo jednej doby co druga dobe (mniej niz "tydzien dowozu" - po tescie mozna rozwazyc, ale wymaga zmiany RecentPaid w ClanBudget).
- (drobna, POPRAWIONA) kontrola NGL-6 nie mogla wypasc - potwierdzone: `Move` liczy obie strony z tej samej zmiany zlota, a "zaplacone - (wplacone - zwrocone)" = 0 z budowy.
  Teraz niezalezny pomiar: ksiega kupcow KS-H (`KsFoodCarts.PaidAsDstOn` - suma `Paid + Freight` wozow kupionych dzis do tego celu = to, co `Ship` zdjal z kasy celu).
  Udzial pana = min(X, e, delta kasy - budzet wlasny, ksiega - budzet wlasny); kontrola "kasa netto ponad ksiege" (delta kasy w nawiasie > ksiega = ktos poza KS-H ruszyl kase,
  oczekiwane 0 miast), licznik "udzial pana uciety ksiega". NGL-6c: od panow <= ksiega kupcow, od panow <= wydatek kasy, kasa netto ponad ksiege 0. Stara kontrola NGL-6 zostaje
  (zmierzone zmiany zlota glowy i kasy).

**Ryzyko / co sprawdzic (kontrola calosci, zasada 0):**
- REGRESJE: klucze wylaczone albo KS niezastosowany - postfiksy wracaja na pierwszej linii (migawka pusta), zloto nie rusza, ClanBudget `RecentPaid` = 0; zostaje tylko linia
  "Glod miast (KS)" (pomiar buntow). W grze Jeffa (WGRANIE 76, KS nie zastosowany) - zero zmian w grze.
- KOLIZJE: (a) FolkAidLoyalty (P1b) - drugi postfiks na tej samej metodzie lojalnosci, niezalezne pozycje; glod obniza lojalnosc, wiec podloga P1b (60) i prog poboru (30) zatrzymaja
  danine z oszczednosci w glodnych miastach (zgodne z regula P1b, mniej daniny P1 dla korony); (b) chleb M3, TP i nasza doplata - jedno tempo skrzyni (kazdy odejmuje
  poprzednikow tej doby; ClanBudget w wojnie odejmuje chleb i doplate z ostatniej doby); (c) KS-H: wplata zmienia budzet Decide tylko miast glodnych z doplata - liczniki NKS-22
  "kasa celu" (CofShort = kasa/zapas przy braku) i "loty z zapasu kupcow" dla tych miast licza kase razem z wplata pana; kontrola zlota KS-H (wlasne liczniki) bez zmian;
  okno MoneyLedger MKsH (Przeplywy osad) zawiera doplate panow netto (+P do kas, zloto swiata bez zmian); (d) RebelPurseFromTown - pieniadze pana poza nawiasem nie leza w kasie;
  (e) E5/C2 Hunger.Of (glod z braku jedzenia/pieniedzy -> zadowolenie BK) bez zmian - inne zjawisko (zadowolenie), nie lojalnosc.
- SPOJNOSC: zamkniety pieniadz - glowa -> kasa (na chwile) -> zrodla / furmani wsi / zalogi portow -> zwrot reszty do glowy; tozsamosc w linii. Jedna definicja glodu dla niepokoju
  i dla doplaty (migawka KsHunger). Kolejnosc doby: chleb M3 -> ClanBudget -> TP -> StartCalibration (mapa ZC4) -> KsFoodCarts.Arrivals -> KsHunger.Daily -> doplata -> KS-H.
- CUDZY KOD (dekompilacja): BKLoyaltyModel (czlon jedzenia, dryf 0.1, fallback do modelu gry bez danych BK - wtedy -1/-2 liczone jak BK -2: najwyzej 1 pkt mniej kary, rzadkie),
  DefaultSettlementLoyaltyModel (progi 15/25), RebellionsCampaignBehavior (warunek buntu; BK/ROT/BKROT go nie wylaczaja), NavalDLCSettlementSecurityModel (BaseModel + RaidersSpoils),
  BKSecurityTweakPatches, ItemConsumptionBehavior.MakeConsumptionInTown (UpdateDemandShift tuz przed MakeConsumption; BK ItemConsumptionPatch - postfiksy biegna).
- DO SPRAWDZENIA W TESCIE 120 DOB (flaga armoury-ks-wlacz.flag, grupa `nglod` + `nks`): ile miast glodnych po rampie (szacunek 8-16 z 97: Zelazne Wyspy, Dorne, Polnoc) i czy
  lojalnosc spada tam do stanu buntu (bunty Zelaznych Wysp i Dorne beda zmiana rozgrywki - stroic `KsHungerTolerance` / `KsHungerLoyaltyMax`); czy doplaty panow plyna
  (NGL-7) i czy NKS-22 "kasa celu za mala" spada; NGL-3 / NGL-6 kontrola 0; koszt < 1 ms. Pierwsza doba po wczytaniu - bez linii "Hunger" (pamiec sesji).
- PO RECENZJI: (a) doplata tylko co druga doba na miasto (doba przegladu celu) - w linii "do doplaty" liczy tylko miasta w dobie przegladu, reszta w "poza doba przegladu";
  NKS-22 "kasa celu" widzi wplate tylko w dobie przegladu (poza nia cel i tak nie zamawia); (b) udzial pana ograniczony ksiega kupcow - przy przychodzie miasta jako zrodla
  (sprzedaz jedzenia innym celom w tym samym nawiasie) obowiazuje nadal mniejsza z dwoch (delta kasy), wiec pan placi nie wiecej niz dotad; ksiega dziala tylko jako
  bezpiecznik przy ruchu kasy spoza KS-H; (c) `KsFoodCarts.PaidAsDstOn` - jedno przejscie po wozach w drodze raz na dobe i tylko przy otwartym nawiasie (koszt w linii "czas");
  (d) mianownik spichlerza bez zmiany liczb u Jeffa (40 / 20 z czynnego modelu = stale wzoru), NGL-1b sprawdza to przy kazdym starcie; (e) scalenie z r4-kupcy: jesli r4-kupcy
  przeniesie zaplate z `Ship` na dostawe, `Cart.Paid` / `Freight` moga znaczyc co innego - tryb DOPLATA i tak wylacza bezpiecznik (kredyt bez podlaczonych rachunkow), a w trybie
  RACHUNKI ksiega nie jest uzywana; nazwy nowych skladowych (`ReviewDay`, `PaidAsDstOn`) nie zawieraja tokenow bezpiecznika (bill/credit/debt/owed/...).
- SCALENIE Z r4-kupcy (kredyt kupcow): podlaczyc dwa delegaty (np. w `KsFoodCarts` przy imporcie albo w `ArmouryBehavior.OnSessionLaunched` po `KsHunger.EnsureHooks`):
  `KsLordFood.FoodBillsOpen = st => <suma otwartych rachunkow zywnosci miasta st, zl>;` i `KsLordFood.FoodBillsPay = (st, x) => <splac najstarsze rachunki st kwota x, ktora pan juz
  oddal: przelew do kas wierzycieli (zrodel), zwroc zaplacone 0..x>;`. Nawias Before/After w `SupplyDemand.DailyTrade` zostaje (Before biegnie po Arrivals i przed Decide -
  splata zwloki odblokowuje dostawy tej samej doby; After w trybie rachunkow tylko pisze linie). Bez podlaczenia bezpiecznik wylacza doplate (NGL-6d NIE), wiec pan nie placi wcale.

**Status:** DO SPRAWDZENIA (kod `099acb39` + poprawki recenzji na `w-toku/r4-glodbunt`; build rc 0; `nglod` na logu 15-37-31 - brak linii/INFO bez wyjatku; test w grze -
nocny lead; tryb RACHUNKI czeka na API rachunkow r4-kupcy - galaz `w-toku/r4-kupcy` nadal na `efbbf0dc`, bez commitow).
