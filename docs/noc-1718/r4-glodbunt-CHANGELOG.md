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
- `KsLordFood.cs` (nowy, klucz `KsLordFundsFood`) - nawias wokol `KsFoodCarts.Daily` w `SupplyDemand.DailyTrade` (2 linie: `Before` / `After`; `KsFoodCarts.cs` i
  `KsFoodCore.cs` NIE zmienione):
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
- `Settings.cs` + `McmSettings.cs` (gen_mcm): 5 kluczy. `tools/sprawdz_logi.py`: grupa `nglod` (NGL-0..9, NGL-6b/6c/6d), opis w naglowku.

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
- SCALENIE Z r4-kupcy (kredyt kupcow): podlaczyc dwa delegaty (np. w `KsFoodCarts` przy imporcie albo w `ArmouryBehavior.OnSessionLaunched` po `KsHunger.EnsureHooks`):
  `KsLordFood.FoodBillsOpen = st => <suma otwartych rachunkow zywnosci miasta st, zl>;` i `KsLordFood.FoodBillsPay = (st, x) => <splac najstarsze rachunki st kwota x, ktora pan juz
  oddal: przelew do kas wierzycieli (zrodel), zwroc zaplacone 0..x>;`. Nawias Before/After w `SupplyDemand.DailyTrade` zostaje (Before biegnie po Arrivals i przed Decide -
  splata zwloki odblokowuje dostawy tej samej doby; After w trybie rachunkow tylko pisze linie). Bez podlaczenia bezpiecznik wylacza doplate (NGL-6d NIE), wiec pan nie placi wcale.

**Status:** DO SPRAWDZENIA (kod `099acb39` na `w-toku/r4-glodbunt`; build rc 0; `nglod` na logu 15-37-31 - brak linii/INFO bez wyjatku; test w grze - nocny lead).
