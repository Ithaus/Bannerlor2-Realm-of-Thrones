# m105-optym - skroty przelomu doby (21.10, galaz w-toku/m105-optym, baza n32 77c17f3c + scalona m99-optym 1a709d07)

Plan: docs/PLAN-OPTYMALIZACJA-NOC.md (pkt 1 przelom doby, pkt 3 smieci GC; pkt 2 - rozlozenie na godziny - poza zakresem, zmienia kolejnosc doby).
Zasady: liczby gry bez zmian, kazda zmiana pod wlasnym kluczem MCM (domyslnie TAK, NIE = kod jak dotad 1:1), tryb kontrolny OptimVerify (wspolny z m99) liczy
stara i nowa droge, gra bierze stara, roznice ida do linii "Optym (m105)"; watek gry jak dotad; zapis gry bez zmian (nic nowego w SyncData).

## Dla Jeffa (prosto)
O polnocy gra liczyla dochod kazdego rodu krolestwa tylko po to, zeby go od razu wyrzucic: powinnosci wobec korony od dawna liczymy z ziemi rodu, a nie z tego
dochodu. U rodow bez zamkow, miast, wsi i warsztatu to liczenie nic po drodze nie zapisuje - wiec teraz go nie ma. Do tego trzy mniejsze rzeczy: markietani szukaja
najblizszego miasta i wsi na liscie zrobionej raz na dobe, budowy pytaja o wojne raz na krolestwo, a ksiega dochodow pyta Banner Kings bez wolnego "odbicia".
Pieniadze, ceny i wszystko w grze zostaja takie same - zmienia sie tylko ilosc pracy. Spodziewam sie ok. 25-40 ms mniej na kazda dobe (ok. 4-7% przelomu doby).
"O polowe" sie nie da bez ruszania liczenia finansow, ktore po drodze zapisuje pensje rady i podatki warsztatow - tego nie ruszalem.

## Pomiar przed (raport m105, logi kopie: 102 prolog 230 = Armoury-2026-10-21_14-29-20.log, n31 160 z zapisu = Armoury-2026-10-21_13-37-13.log)
| bieg | doby | takt doby mediana / srednia / max | ostatnie 60 dob (MO-1) | s/dobe |
|---|---|---|---|---|
| 102, prolog 230 | 229 | 552.0 / 546.1 / 709.7 ms | 580.6 / 582.0 | 11.96 |
| n31, 160 z zapisu | 160 | 581.1 / 586.0 / 837.9 ms | 591.0 / 596.2 | 15.12 |

Najwieksze wywolania taktu doby (srednio ms/dobe, 102 / n31): ClanIncomeBook.Daily 91.9 / 89.4, SupplyDemand.DailyTrade 78.4 / 64.2, KingdomTreasury.Daily 66.2 / 65.8,
BuildFunding.Daily 38.2 / 25.5, ClanBudget.Daily 20.6 / 22.4, Sutlers.Daily 16.4 / 46.0 (w 102 w polu 517 partii, koszt 30 ms w ostatniej dobie), TownFolk.Daily 16.4 / 15.6,
WarLedger.OnDaily 11.6 / 20.2, Stage6Measure.Daily 11.5 / 16.8. Linia "Korona": podstawa ziemia u 99.6% rodo-dob (102: 119567 / 529 stara; n31: 85839 / 537, stara tylko
w 1. dobie po wczytaniu).

## Zmiany
| # | plik / metoda | klucz MCM | przewidywany zysk ms/dobe | commit |
|---|---|---|---|---|
| 1 | KingdomTreasury.Optim105.cs, KingdomTreasury.Daily (petla rodow) | OptimKtSkipModel | ok. 20-30 (ok. 300 pominietych modeli po ok. 0.08-0.1 ms, minus sprawdzenie BK wsi ok. 2-3 ms) | 1ddf1ba4 |
| 2 | SutlerNear.cs, Sutlers.Daily / Sutlers.One | OptimSutlerNearest | ok. 3-8 (najblizsza wies: ok. 570 wsi x ok. 400 partii) | 49f1180c |
| 3 | BuildFunding.Optim105.cs, BuildFunding.Daily (petla warowni) | OptimBuildWar | ok. 0.3-0.5 | 49f1180c |
| 4 | ClanIncomeBook.Optim105.cs, ClanIncomeBook.StableBegin | OptimStableDelegates | ok. 1-2 (ok. 2000 MethodInfo.Invoke mniej) | 49f1180c |
| 5 | tools/sprawdz_logi.py - grupa m105optym (M5-0..M5-5) | - | - | 2780c7d0 |

Linie logu: start kampanii "Optym (m105): start | Optim Verify TAK/NIE | wlaczone: ... | wylaczone (kod jak dotad): ... | bez zmian ..." (z KingdomTreasury.OptimStartLine);
co dobe (LogEnabled, klucz TAK) "Optym (m105): dzien N | KingdomTreasury.Daily model dochodu: pominiety u X rodow (z Y ...)" albo "pominiecie WYLACZONE - powod",
"... | Sutlers.Daily najblizsze osady: z tablic doby (...)", "... | ClanIncomeBook.StableBegin wywolania BK przez delegaty: N (...)"; przy Optim Verify do kazdej
dopisek "kontrola (Optim Verify) <czesc>: porownan A, roznic B (sesja: ...)" i osobna linia BuildFunding (tylko w trybie kontrolnym).

## 1. KingdomTreasury.Daily - model dochodu pominiety (OptimKtSkipModel)
**Marnotrawstwo:** przy podstawie powinnosci "ziemia" (165, CrownDuesFromLand) wynik `model.CalculateClanIncome(c, false, false, false)` jest nadpisywany przez
`ClanIncomeBook.StableLand(c)` u kazdego rodu z pomiarem D stalego; liczba szla tylko do porownania 169b (`NoteModelIncome`, sam log). Model wolany ok. 537 razy na dobe
tylko dla skutkow ubocznych (m99-optym-CHANGELOG pkt 2: TownBase / pensje rady, workshopTaxes BK).

**Kiedy pomijany (wszystko naraz; KtSkip, wlasny try - wyjatek = model liczony jak dotad):** podstawa ziemia i `StableLand(c) >= 0` (odczyt sprzed modelu); `c.Fiefs.Count == 0`;
BK `GetActualVillages(c)` puste (delegat raz na proces); glowa rodu bez warsztatu (`Leader.OwnedWorkshops.Count == 0`); nie rod gracza. Rody krola, najemnicy, rody bez krolestwa -
jak dotad poza petla powinnosci.

**Dowod, ze model nic tym rodom nie zapisuje (dekompilacja z biezacych DLL: TaleWorlds.CampaignSystem 1.4.8, BannerKings.dll, ROT.dll, NavalDLC.dll, BKROTPatch.dll):**
- Lancuch modelu: ROTClanFinanceModel.CalculateClanIncome = `_previousModel.CalculateClanIncome(...)` 1:1; NavalDLCClanFinanceModel.CalculateClanIncome = `BaseModel.CalculateClanIncome(...)` 1:1;
  BKClanFinanceModel.CalculateClanIncome = `base.CalculateClanIncome` (gra) + `AddIncomes` (gdy TitleManager != null).
- Gra, CalculateClanIncomeInternal przy applyWithdrawals = false: AddRulingClanIncome (tylko rod krola - poza petla); "za tier" tylko poza krolestwem / najemnik; AddMercenaryIncome
  (tylko najemnik); AddSettlementIncome - petla po `clan.Fiefs` (pusta), a BK `ClanFinancesPatches.VillageIncomePrefix` (zastepuje ja) - po `clan.Fiefs` (pusta) i `GetActualVillages`
  (pusta: bez `CalculateVillageTaxFromIncome`, ktory dla osady bez danych BK wola GetPopData -> InitializeSettlementPops); CalculateHeroIncomeFromWorkshops(clan.Leader) - petla po
  warsztatach glowy (pusta: bez BK CalculateOwnerIncomeFromWorkshop, ktory pisze workshopTaxes); AddIncomeFromParties/AddIncomeFromParty, AddIncomeFromTribute, CalculateShareFactor,
  AddIncomeFromCallToWarAgrements, AddIncomeFromTradeAgreements - zapisy tylko pod `if (applyWithdrawals)`; AddIncomeFromKingdomBudget - pominiety przez BK KingdomBudgetPrefix
  (zapis tez tylko przy applyWithdrawals); SpringOfGold - odczyt.
- BK AddIncomes przy applyWithdrawals = false: GetContract (pole), CalculateOwnerIncomeFromEstates (PopulationManager.GetEstates - kopia listy, LastIncome), TitleManager.CalculateVassals
  (GetAllDeJure, CalculateHeroSuzerain, GetImmediateSuzerain, GetTitleFaction - odczyty; BKROTPatch CalculateHeroSuzerainPatch.Postfix - filtr wyniku), CourtManager.GetHeroPositions
  (odczyt pamieci podrecznej / CouncilData.GetHeroPositions - nowa lista), DueWage / DueTax - wlasciwosci. Bez MBRandom na calej sciezce (pominiecie nie przesuwa losowan).
- Nasze latki na tej sciezce: MercContract.MercIncomePrefix (tylko najemnik z umowa), MercContract.TierPostfix (liczniki tylko przy applyWithdrawals), CouncilWages35.IncomesPostfix
  (wychodzi przy !applyWithdrawals), SideVillageFief.IncomeViewPostfix (tylko rod gracza). CrashScribe Mends na AddMercenaryIncome - cofniete 14.09 (nie zakladana).
  AIInfluence (latki CalculateClanGoldChange / AddSettlementIncome, zaciemniony) - wylaczony w LauncherData.xml Jeffa.
- `StableLand` przed modelem = po modelu: model tych rodow nie dotyka ksiegi ClanIncomeBook (NoteModelIncome pisze tylko `_modelSeen` - czytany wylacznie przez porownanie 169b).
- Petla powinnosci po pominieciu: `income = StableLand` -> ta sama stawka (WarOf), ta sama kwota, ta sama kolejnosc rodow i platnosci; zaokraglenia bez zmian.

**Straznik (KtGuard, codziennie przed petla - cudza latka moze dojsc w trakcie sesji):** (a) lancuch czynnego modelu (BaseModel i pola typu ClanFinanceModel, do 12 wezlow) tylko
z typow ROT / NavalDLC / BK / DefaultClanFinanceModel i BK w nim jest; (b) na kazdej metodzie sciezki (wyzej, 20+ metod gry i BK) tylko sprawdzone latki Harmony (prefiksy,
postfiksy, transpilery, finalizery, ILManipulators): `Armoury:MercContract.MercIncomePrefix`, `Armoury:MercContract.TierPostfix`, `Armoury:CouncilWages35.IncomesPostfix`,
`Armoury:SideVillageFief.IncomeViewPostfix`, `BannerKings:ClanFinancesPatches.KingdomBudgetPrefix / AddIncomeFromPartyPrefix / VillageIncomePrefix`,
`BKROTPatch:CalculateHeroSuzerainPatch.Postfix`; (c) delegat BK GetActualVillages i typy BK sciezki znalezione. Inaczej pominiecie WYLACZONE na te dobe (model jak dotad),
powod w linii "Optym (m105)" (np. "niesprawdzone latki na sciezce modelu: X na Y"). Nowa latka z innej galezi na tej sciezce = najpierw sprawdzic, czy przy applyWithdrawals = false
nic nie pisze, potem dopisac do `KtKnownPatches`.

**Tryb kontrolny (Optim Verify):** model liczony jak dotad (gra = stara droga, z NoteModelIncome), przed nim i po nim sondy: wywolania TownBase (CouncilWages35._baseCalls),
workshopTaxes BK (liczba i suma), kiesa glowy, KingdomBudgetWallet / TributeWallet / CallToWarWallet / MercenaryWallet krolestwa, StableLand przed vs po. Kazda zmiana = roznica
(pierwsza z nazwa rodu w linii). Grupa progow: M5-2 roznic 0.

**Skutek tylko w logu:** porownanie 169b ("porownanie z liczba z chwili powinnosci (KingdomTreasury) u N rodow" w linii ksiegi i w linii "Obieg") liczy mniej rodow - o pominiete.
Zadna grupa sprawdz_logi go nie czyta (grep "chwili powinnosci"/"rozna u"/"oszczedziloby" - 0). Potkniecia modelu pominietego rodu nie licza sie w linii "Korona" (model nie biegnie).
Przy okazji mniej smieci GC: ok. 300 wywolan modelu BK na dobe mniej (TextObject, kopie list w GetAllDeJure / GetEstates).

## 2. Sutlers.Daily - najblizsze osady z tablic doby (OptimSutlerNearest)
**Marnotrawstwo:** Sutlers.NearestTown (Town.AllTowns, ok. 100) i NearestVillage (Village.All, ok. 570) od nowa na kazda z ok. 400-520 partii w polu - za kazdym razem
odczyt polozenia, stanu i frakcji osady.
**Zmiana:** SutlerNear.Begin (przed petla partii, gdy stawka > 0) buduje tablice: osada, GetPosition2D, MapFaction - ta sama kolejnosc co Town.AllTowns / Village.All, ten sam
filtr (st != null && st.IsTown; wies: st != null && VillageState == Normal). NearTown / NearVillage: to samo `p.DistanceSquared(pos)` (ten sam Vec2, ta sama arytmetyka float),
to samo scisle `d >= bd -> dalej`, ten sam test `FactionManager.IsAtWarAgainstFaction` (na zywo) dopiero dla blizszej - przy remisie wygrywa ta sama osada. End po petli czysci
tablice (bez osad poprzedniej kampanii do nastepnej doby).
**Dowod:** w petli partii (One) nic nie zmienia polozenia osad, stanu wsi ani frakcji: CampMend / CampFree / CampPlan (odczyty), ArmyClothing.FromSutlers (zakup z polki miasta),
CitySack (odczyt i licznik), CampTake, ChangeGold miasta i wsi, VillageTakings.Split, DsVillage.WarShareSutlers, TownFolk, liczniki. Rzad markietanow gracza (CampSutlers) dalej
wola Sutlers.NearestTown / NearestVillage (stara droga). Poza petla Daily i przy kluczu NIE NearTown / NearVillage = stara droga.
**Verify:** obie drogi, gra bierze stara, roznice (miasto / wies, pierwsza ze StringId).

## 3. BuildFunding.Daily - wojna krolestwa raz na krolestwo (OptimBuildWar)
**Zmiana:** test "krolestwo pana warowni w wojnie z innym krolestwem" (przeglad Kingdom.All, wstrzymanie budowli cywilnych - wpis 86) liczony raz na krolestwo w petli warowni
zamiast raz na kazda warownie z budowa (wzor m99 WarOf). BfWarOld = doslownie dawny kod (ten sam warunek, break, catch -> to, co ustalone przed wyjatkiem). Pamiec od zera na poczatku
i na koncu petli. W petli tylko odczyty (kolejka budow, WarFront.CivilAllowed) - stan wojny stoi. Verify: stara droga wygrywa.

## 4. ClanIncomeBook.StableBegin - BK przez delegaty (OptimStableDelegates)
**Zmiana:** GetActualVillages (static, Delegate.CreateDelegate), IsSettlementPopulated i TitleManager.GetTitle (PerfFast.M1), podatek wsi BK CalculateVillageTaxFromIncome(Village,
bool, bool) (wyrazenie skompilowane raz) zamiast MethodInfo.Invoke z new object[] i pakowaniem bool. To samo wywolanie (ten sam obiekt i argumenty, wywolanie wirtualne jak Invoke).
Inny podpis BK - delegat null - odbicie jak dotad. Jedyna roznica poza liczbami: wyjatek wewnatrz BK idzie do Stumble bez otoczki TargetInvocationException (tylko tekst logu).
**Verify:** odczyty czyste (wsie z tytulu, dane osady, tytul) obiema drogami - gra bierze stara; podatku wsi BK NIE liczymy drugi raz (postfiks TownFolk.VillageTaxPostfix dopisuje
liczniki okna `_win.VTax*`) - w trybie kontrolnym podatek idzie stara droga.

## Czego nie zrobilem (i dlaczego)
- **BuildFunding.Daily, model dochodu (ok. 12-18 ms, kategoria B):** wynik nadpisuje ClanBudget.BuildShare, ale rody z budowa maja lenna - model pisze TownBase (DueWage) i
  workshopTaxes. Miedzy BuildFunding (ArmouryBehavior ok. 2690) a KingdomTreasury (2747) / ClanIncomeBook (2830) czyta je np. IronBank.Daily (2794, linia "Councillor role" w dochodzie
  modelu) - u rodow krolow nie ma posredniego liczenia, wiec IronBank widzialby pensje z innej chwili. Najpierw dowod, ze nikt nie czyta DueWage / workshopTaxes w tym oknie.
- **ClanIncomeBook.Daily, model (71 ms):** to wejscie do D (wartosc z KT rozni sie u 117 z 537 rodow) - nie da sie pominac.
- **SupplyDemand.DailyTrade (64-90 ms, kategoria C):** zostaje petla polki na kazda transakcje (2149 transakcji, prawie zawsze 1 sztuka). Indeks wznowienia przegladu polki wymaga
  sprawdzenia, jak ItemRoster przestawia elementy przy zdjeciu ostatniej sztuki i jak traktowac egzotyczne konie zatrzymane u zrodla (continue bez zdjecia) - bez stoperow wnetrza
  i w pliku, ktory zmieniaja galezie gospodarki (m105-gospodarka4 / m105-miasta), nie ruszalem. Nastepny krok: stopery probkowane 1/16 (spis, PickStart, FreeBand, petla polki, Ship).
- **Sutlers.Daily, reszta (CampMend = AiWear.OutstandingCost, ArmyClothing.FromSutlers):** najpierw stopery wnetrza. PendingCost dwa razy przy N6 - tani (slownik + petla po
  towarach), nie wart zmiany.
- **WarLedger.OnDaily:** zamiana MobileParty.All na AllLordParties zmienia kolejnosc, a w petli jest MBRandom - liczby gry by sie zmienily. Nie robic.
- **Stage6Measure.Daily (11-17 ms):** sam pomiar do logu; liczenie tylko w dobach LogGate zmienia linie czytane przez grupy progow - decyzja Jeffa (liczb gry nie zmienia).
- **ClanBudget, TownFolk, PeopleLedger (10-24 ms):** kategoria C - stopery wnetrza najpierw.
- **GC (sonda alokacji):** AppDomain.MonitoringIsEnabled wlacza sie na caly proces bez powrotu (net472, brak GC.GetAllocatedBytesForCurrentThread) - tylko na bieg pomiarowy,
  po decyzji. Zmiana 1 usuwa przy okazji alokacje ok. 300 wywolan modelu BK na dobe.
- **Rozlozenie pracy doby na godziny:** poza zakresem (zmienia kolejnosc zdarzen w dobie).

## Plan autotestu
1. **Bieg kontrolny, Optim Verify = TAK** (wszystkie klucze m105 TAK; np. 160 dob z zapisu jak n31 albo nowa kampania z prologiem): `python -I tools/sprawdz_logi.py --grupa m105optym
   <Armoury-*.log>` - M5-2 roznic 0 w kazdej czesci przy porownan > 0 (model bez lenn, najblizsze osady, BuildFunding.Daily wojna krolestwa, delegaty BK), M5-3 TAK (pominiete > 0
   w wiekszosci dob, straznik nie wylaczyl), M5-4 TAK, M5-5 potkniecia 0 i ERROR 0; m99optym MO-2 jak dotad.
2. **Bieg zwykly, Optim Verify = NIE** (ten sam start): `python -I tools/sprawdz_logi.py --grupa m105optym <log> --baza-doby Armoury-2026-10-21_13-37-13.log` (n31, 160 z zapisu;
   dla prologu - Armoury-2026-10-21_14-29-20.log) - M5-1: takt doby nizej (oczekiwane mediana ok. 25-40 ms nizej, KingdomTreasury.Daily ok. 20-30 ms nizej, Sutlers kilka ms nizej),
   M5-3/M5-4/M5-5 TAK; pozostale grupy wobec bazy - tylko zmiennosc biegu.
3. Gdy M5-3 = NIE z powodem "niesprawdzone latki": sprawdzic nazwana latke w dekompilacji (zapisy przy applyWithdrawals = false), dopiero potem dopisac do KtKnownPatches.

Test progow: 8 logow syntetycznych (generator scratchpad .../optym/gen_test.py): dobry bieg kontrolny - 0 NIE; roznica w KT - M5-2 NIE; straznik wylaczyl - M5-3 NIE;
Verify TAK bez porownan - M5-2 NIE; stary log - INFO; potkniecia + ERROR + doba bez tablic - M5-4 i M5-5 NIE; podstawa nie-ziemia - M5-3 INFO; zwykly bieg z baza - M5-1
z roznicami sekcji. Logi 102 / n31 (sprzed galezi): M5-0 / M5-1 INFO.

## Ryzyko / co sprawdzic
- **Regresje:** zmienione miejsca - KingdomTreasury.Daily (4 wstawki: KtSkipBegin, KtSkip + warunkowe wywolanie modelu, StableLand sprzed modelu, KtSkipEnd), Sutlers.Daily / One
  (Begin/End, 3 wywolania NearTown/NearVillage), BuildFunding.Daily (BfWarBegin, BfWarOf, BfWarEnd), ClanIncomeBook.StableBeginBody (CdBegin, 4 wywolania, CdEnd),
  KingdomTreasury.OptimStartLine (1 linia). Przy kluczu NIE kazde miejsce idzie dawnym kodem 1:1 (BfWarOld, Sutlers.NearestTown/NearestVillage, Invoke, model zawsze).
- **Kolizje:** nowe pliki czesciowe (KingdomTreasury.Optim105.cs, BuildFunding.Optim105.cs, ClanIncomeBook.Optim105.cs) i klasy (SutlerNear, Optim105); nazwy z przedrostkiem
  Kt/Bf/Cd - sprawdzone, nie wystepuja w tych klasach. Nikt poza Sutlers.One nie wola SutlerNear.NearTown/NearVillage.
- **Rownolegle galezie gospodarki (m105-gospodarka4 wsie, m105-miasta miasta):** logika nietknieta (cache/pominiecie bez zmiany wyniku). Gdyby ktoras dodala latke na sciezce modelu
  dochodu - straznik wylaczy pominiecie (bezpiecznie), linia powie ktora. Gdyby zmienila warunki "rodu bez lenn" (np. nowe zrodlo dochodu rodu w modelu z zapisem) - do przegladu
  KtSkip. Przy scaleniu mozliwe konflikty tekstowe w Sutlers.cs / BuildFunding.cs / ClanIncomeBook.StableD.cs / KingdomTreasury.cs (pojedyncze linie), Settings.cs (6 linii po
  OptimVerify), sprawdz_logi.py (pomoc, funkcje, dispatch m105optym przed m99optym).
- **Watki:** wszystko na watku gry (doba), jak dotad. **Zapis:** bez zmian (pamiec tylko w sesji, czyszczona przy starcie kampanii / wczytaniu).
- **Klucze MCM:** OptimKtSkipModel, OptimSutlerNearest, OptimBuildWar, OptimStableDelegates (TAK) - nowe, nie ma ich w Armoury.json Jeffa (dzialaja domyslne). gen_mcm: Armoury 2281.
- **Build:** Release Armoury kod 0 (7 ostrzezen jak przed galezia, 0 bledow). Gra nie uruchamiana (zakaz zadania) - dzialanie potwierdzi dopiero autotest.

## Status
DO SPRAWDZENIA - autotest: bieg kontrolny (M5-2 roznic 0) i bieg zwykly (M5-1 takt doby nizej wobec bazy).
