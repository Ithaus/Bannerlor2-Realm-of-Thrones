# Audyt ponowny: zmiany z 2026-10-04, wpisy (11)-(34)

To jest przeglad tylko do odczytu, kodu nie ruszalem. Zrodla: Armoury/src, RealisticCaptivity/src, CrashScribe/src.
Dekompilacje: tw, bk, be, dte, rot, nv, rbl. Nic z tego nie bylo jeszcze uruchomione w grze, wiec ponizsze
liczby i zachowania to wnioski z kodu. Logiem gry ich nie sprawdzalem.

Sprawdzone bez uwag:
- **Nazwy i indeksy parametrow Harmony we wszystkich nowych latkach.** OutlawLaw: GateClan, GateLooter, GatePirate, GateFill,
  GateBkInfest, GateBkHero, UpgradePrefix, StartGoldPostfix. Levy `__0..__2`, LevyGold `__0..__6` (`object` dla enuma
  RecruitingDetail, Harmony go pakuje), VolunteerKit `settlement`, Fabula `__instance/__state`,
  WinterSource `ref float __result`, Rations, MapClock. Wszystko zgadza sie z sygnaturami w dekompilacji.
- **Transpiler WesterosClimate i WinterSource.** Zamiana getterow `CampaignTime` (adres struktury, wynik enum/int/double)
  i `SeasonalityProfile.X(Season)` na nasze statyczne metody ma ten sam ksztalt stosu. `SeasonClock.Current` i `DayInSeason`
  sa statyczne, a enum BEE ma kolejnosc Spring..Winter = 0..3.
- **Settings.cs i McmSettings.cs.** Kazde z 473 pol ma wlasciwosc MCM z ta sama wartoscia domyslna i linie w `ApplyTo`
  (sprawdzone skryptem).
- **WinterBite.** Jego latki na Default* naprawde sie wykonuja: BEE wola `new DefaultVillageProductionCalculatorModel()`,
  a BKFoodModel wola base. "Jedno zrodlo zimy" nie wylacza wiec zimy calkiem.
- **Pozostale wyniki ze sprawdzenia kodu:**
  - Rations ma licznik zagniezdzenia na `[ThreadStatic]`, wiec dziala tez przy rownoleglym ticku.
  - DTE `AddItemToPartyArmory` sam tworzy wpis zbrojowni, wiec pominiecie `OnMobilePartyCreated` nie powoduje KeyNotFound.
  - Fabula: `StartDay/EndDay` to wlasciwosci z setterem, a stale 300/400/500 stoja zaraz po `ElapsedDaysUntilNow`.

Nie znalazlem pewnego crasha w nowym kodzie. Najwieksze problemy dotycza ekonomii: dwie dziury, w ktorych zloto
powstaje z niczego (K1, K2), oraz jedna dziura, ktora wpis 32 mial zamknac, a ktora nadal jest otwarta (W1).

---

## KRYTYCZNE (lamia cel: zamknieta ekonomia)

### K1. Zamrozona polka (wpis 31) dala odwrotny arbitraz: tani hurt, potem drozsza odsprzedaz
**Plik:** `Armoury/src/SupplyDemand.cs:186` (`Stock`) i `:193` (`Factor`)

**Co sie dzieje:** przy otwartym ekranie zapas to `max(zamrozony, zywy)`. Zakup gracza nie obniza wiec zapasu i cena
kupna stoi w miejscu przez caly koszyk. Przyklad: polka 10 sztuk, popyt 5, elastycznosc 1:
1. Gracz kupuje 10 sztuk po cenie przy pelnej polce, okolo 0.55 wartosci kazda (razem ~5.5 wartosci).
2. Zamyka ekran i otwiera go ponownie. Polka jest teraz pusta, nowy zamrozony stan wynosi 0.
3. Sprzedaje te same 10 sztuk po cenach zapasu 0, 1, ..., 9: 2.0, 2.0, 2.0, 1.5, 1.2, 1.0, 0.86 ... (razem ~12.6 wartosci).

Zysk wynosi okolo 130% na obrocie, nawet po vanillowej karze sprzedazy. Do tego wystarcza zamkniecie i otwarcie ekranu.
Przed wpisem 31 cena kupna rosla z kazda kupiona sztuka, wiec ta petla sie nie oplacala. B1 jest tylko odwrocone,
nie zamkniete. Placi za to kasa miasta, a ta jest dosypywana z niczego (patrz K2).

**Poprawka:** `Stock(shelf, it, isSelling)`:
- **kupno:** zawsze zywy stan `n`. Kazda kupiona sztuka podnosi cene. Cofniecie zakupu w tym samym ekranie vanilla i tak
  rozlicza po cenie z historii transakcji.
- **sprzedaz:** `max(zamrozony, n)`. Wykupienie polki "na kredyt" nie podnosi ceny sprzedazy.

To samo dotyczy `Substitution` (zamrozony widok tylko przy sprzedazy).

### K2. Renty od ludnosci (wpis 25) nadal powstaja z niczego, a do tego wysysaja kase miast, ktorej potrzebuja nasze systemy
**Pliki:**
- `Armoury/src/PopulationLaw.cs:161`
- `Armoury/src/WorkshopLaw.cs:178`
- `Armoury/src/SupplyDemand.cs:345`
- gra: `ItemConsumptionBehavior.UpdateTownGold`, `DefaultSettlementEconomyModel.GetTownGoldChange`

**Co sie dzieje:**
- **Kasa miasta odrasta z niczego.** Vanilla co dzien dodaje miastu `0.25 x (10000 + 12 x dobrobyt - kasa)`. BEE oddaje
  te funkcje `new DefaultSettlementEconomyModel()`, wiec tak dziala to u nas. Im wiecej pan zabierze (do 50% dziennie),
  tym wiecej gra dosypie nastepnego dnia. Renta z miasta to w praktyce zloto z niczego, tylko przepuszczone przez kase
  miasta. Wsie tego problemu nie maja: ich kasa pochodzi ze sprzedazy wiesniakow w miescie.
- **Ten sam drenaz glodzi nasze systemy.** WorkshopLaw pomija cykl, gdy `town.Gold < revenue` (powod 4), wywoz nadwyzki
  kupuje tylko za `Town.Gold / unit`, a gracz nie sprzeda towaru do pustej kasy.
- **Kolejnosc w ciagu dnia.** `PopulationLaw.Daily` biegnie przed `SupplyDemand.DailyTrade` (ArmouryBehavior.cs:1123-1126),
  wiec transport widzi juz polowe kasy.

**Poprawka:** dwa kroki.
1. Od razu: dla MIAST renta = `min(nalezne, max(0, kasa - rezerwa))`, gdzie rezerwa to np. `10000 + 12 x dobrobyt`
   (cel vanilli). Wtedy renta bierze tylko nadwyzke ponad cel i nie wywoluje dosypki. Albo wylaczyc renty miast do czasu
   kroku 2.
2. Docelowo: latka na `GetTownGoldChange`, ktora wylacza dosypke z niczego. Kasa miasta pochodzi wtedy tylko z handlu,
   cel, podatkow i oplat. To wymaga przegladu wszystkich wplywow (patrz "Czego brakuje" pkt 1).

---

## WYSOKIE

### W1. Okup u posrednika (wpis 32): glowna sciezka dalej placi naszej nadwyzki z niczego
**Pliki:**
- `RealisticCaptivity/src/FairRansom.cs:113`
- gra: `PartyScreenLogic.cs:826`
- gra: `SellPrisonersAction.ApplyByPartyScreen`

**Co sie dzieje:** "Choose the prisoners to be ransomed" (ekran druzyny w trybie TransferableWithTrade) liczy zloto
z `PrisonerRansomValue(..., MainHero)`, czyli z nasza nadwyzka ograniczona kiesa glowy rodu. Wyplaca to zloto z niczego,
a potem wola `ApplyByPartyScreen` z `applyConsequences: false`. W srodku `PrisonerRansomValue` w ogole nie jest wolane,
wiec `_inSale && ...` nigdy nie zdejmuje zlota z rodu. Wpis 32 zamyka tylko "sprzedaj wszystkich" przez
`ApplyForAllPrisoners`.

Drugie ryzyko: w sciezce z `applyConsequences = true` kazde dodatkowe wywolanie `PrisonerRansomValue` dla tego samego
jenca wewnatrz `SellPrisonersAction.ApplyInternal` pobierze nadwyzke drugi raz. Takie wywolanie moze przyjsc ze sluchaczy
`OnHeroPrisonerReleased` albo `OnPrisonerSold` w innych modach.

**Poprawka:** obciazac rod w jednym miejscu, przy faktycznej transakcji:
- prefix na `ApplyByPartyScreen(TroopRoster prisoners)` i istniejaca sciezka `ApplyInternal`;
- dla kazdego bohatera w rosterze policzyc nadwyzke ta sama funkcja i zdjac ja z glowy rodu;
- `HashSet<Hero>` na czas jednej sprzedazy, zeby nikogo nie obciazyc dwa razy;
- w podgladzie zostawic sam limit (`extra <= payer.Gold`).

### W2. Ochotnik "z kupionym sprzetem" (wpis 27): kupione sztuki znikaja, a DTE dalej daje pelny komplet z niczego
**Pliki:**
- `Armoury/src/VolunteerKit.cs:160`
- gra/mod: DTE `EveryoneCampaignBehavior.OnTroopRecruited` (:870-892)

**Co sie dzieje:** notabl zdejmuje sztuki z targu (`AddToCounts(e,-1)`), ale nigdzie ich nie odklada. Przy werbunku DTE
dorzuca werbujacemu pelny komplet szablonu z niczego. Kazdy awans to wiec jednoczesnie odplyw z rynku (przedmioty
niszczone) i doplyw z niczego (komplet DTE). Rynek pustoszeje, AiGear dokupuje braki, ceny rosna. Rekrut tieru 1 dostaje
caly komplet z niczego (to vanilla/DTE, ale plan zakladal, ze tak juz nie bedzie).

**Poprawka:**
1. `Dictionary<Hero, List<EquipmentElement>>` jako zapas notabla, zapisywany w save.
2. W `LevyGold.ApplyInternalPostfix` (VolunteerFromIndividual) przelac jego zawartosc do zbrojowni DTE werbujacego
   (`AddItemToPartyArmory` przez refleksje).
3. Prefix na DTE `OnTroopRecruited`: pominac dla ochotnikow od notabla, gdy VolunteerKit jest wlaczony.

### W3. VolunteerKit nie widzi czesci awansow
**Plik:** `Armoury/src/VolunteerKit.cs:66-82` (+ BK `BKNotableBehavior.UpdateVolunteers`)

**Co sie dzieje:**
- **Blad roznicy pul.** Ten sam dzien: X -> Y w jednym miejscu i nowy podstawowy X w pustym. Przed = {X}, po = {X, Y},
  wiec Y wyglada na "nowego ochotnika tieru 1" i awans jest darmowy. Przy podstawowym ochotniku w wielu miejscach
  zdarza sie to stale.
- **Zamki BK.** BK ma wlasne `UpdateVolunteers` dla zamkow (HandleCastles), a VolunteerKit lata tylko vanillowe
  `UpdateVolunteersOfNotablesInSettlement`. W zamkach awanse zostaja darmowe.

**Poprawka:**
- Kazdy przybyly Y rozny od `VolunteerModel.GetBasicVolunteer(n)` traktowac jako awans. Zrodlo X szukac wsrod
  znikniętych, a jesli tam go nie ma, wsrod tych, ktore zostaly (X, ktorego cel to Y).
- Ten sam prefix i postfix dolozyc na BK `BKNotableBehavior.UpdateVolunteers(Settlement)`.

### W4. "Bez darmowego kompletu nowej partii" (wpis 28) tylko przenosi darmowy sprzet na czas bitwy
**Plik:** `Armoury/src/LevyGold.cs:55-66`

**Co sie dzieje:**
- **Kto traci komplet.** Pominiete jest wszystko, co nie jest partia gracza: partie towarzyszy gracza, karawany,
  odrodzone partie lordow po niewoli, nowe garnizony i milicje po zmianie wlasciciela.
- **Bitwa i tak daje sprzet.** Wg wpisu 28 DTE w bitwie i tak dopelnia puste sloty sprzetem szablonu. Wyglad i walka
  sa wiec na sprzecie z niczego.
- **Lup tez powstaje z niczego.** Vanilla generuje lup z ekwipunku poleglych wg szablonu, a nie ze zbrojowni. Komplet
  pominiety na starcie wraca jako lup.

**Poprawka:** to wymaga decyzji Jeffa. Sam prefix nie zamknie obiegu; trzeba:
- (a) wylaczyc FillEmptySlots dla AI poza startem gry, wtedy rekrut walczy w tym, co ma zbrojownia;
- (b) lup z poleglych brac ze zbrojowni DTE przegranych (to C5 z poprzedniego audytu).

Do tego czasu warto co najmniej przepuszczac partie z `ActualClan == Clan.PlayerClan`, bo gracz zobaczy nagich ludzi
towarzysza.

### W5. Kryjowka (wpis 34): zloto zdjete przy zwyciestwie, przepada przy "Leave", a jego zrodlo i tak jest z niczego
**Pliki:**
- `Armoury/src/HideoutPurge.cs:91`, `:148`, `:274`
- gra: `BanditSpawnCampaignBehavior.cs:162-178`

**Co sie dzieje:**
- **Za wczesne zdjecie zlota i towaru.** Zloto i magazyn kryjowki sa zdejmowane od razu po bitwie.
  "Leave without searching" gasi `_pending` i zloto przepada w nicosc. `_lootRoster` zostaje, wiec ekran lupow i tak
  sie otworzy: przedmioty bez przeszukania.
- **Stan poza save.** `_pending`, `_pendingGold` i `_lootRoster` nie sa w save ani w `Reset()`. Wczytanie gry w trakcie
  daje stary lup w innej kampanii (to samo ryzyko co stare ItemObject w WorkshopLaw, wpis 24).
- **Vanillowe zrodlo kasy kryjowki.** Kazde wejscie bandy do kryjowki dodaje 25% wartosci jej lupow i bandzie, i
  kryjowce. Towar przy tym nie schodzi, wiec "prawdziwa kasa kryjowki" jest z niczego i rosnie przy kazdej wizycie.
  `OutlawLaw.GoldPrefix` blokuje tylko DailyTick.

**Poprawka:**
- Zdejmowac zloto i towar w `DoSearch`, a nie przy zwyciestwie. "Leave" ma czyscic `_lootRoster`.
- Dodac `HideoutPurge.Reset()` w konstruktorze ArmouryBehavior.
- Prefix i postfix na `BanditSpawnCampaignBehavior.OnSettlementEntered` (bandy): odjac przyrost `PartyTradeGold` bandy
  i kasy kryjowki, albo zamiast tego przeniesc te przedmioty z bandy do `ItemRoster` kryjowki (lup zostaje w kryjowce,
  zlota nie przybywa).

### W6. Bank Zelazny (wpis 33): termin bez rolowania i pozyczki tuz przed terminem = lawina bankructw AI
**Plik:** `Armoury/src/IronBank.cs:165`, `:204`, `:224-226`

**Co sie dzieje:**
- **Rata wystrzeliwuje.** Dobranie pozyczki przy biezacym dlugu nie przesuwa terminu. Rata `dlug / dni do terminu` nagle
  rosnie: pozyczka na 3 dni przed terminem to 1/3 calego dlugu dziennie.
- **AI pozycza wtedy, kiedy nie stac go na splate.** AI pozycza wlasnie wtedy, gdy brakuje mu zlota na zold (:204), wiec
  od razu nie placi raty. Po trzech dniach jest bankructwo i zajecie 50% skarbca, a dalej 25% dziennie. To odbiera zold,
  co prowadzi do dezercji, a dezerterzy zasilaja pule wyrzutkow.
- **Kapital Banku jest poza swiatem (C4).** Odsetki i zajecia na stale wyjmuja pieniadz z gospodarki.

**Poprawka:**
- Odmowic nowej pozyczki, gdy `DueDay - today < 30` (gracz i AI), albo prowadzic transze z osobnym terminem.
- Rate ograniczyc do udzialu dziennego dochodu rodu.
- W `Daily` AI pozycza tylko wtedy, gdy przewidywana rata < 50% dziennego dochodu.

---

## SREDNIE

### S1. Puste bandy nadal powstaja i sa usuwane (D2 z AUDYT-DZIURY nie zamkniete)
**Plik:** `Armoury/src/OutlawLaw.cs:582`, `:597`, `:550`

**Co sie dzieje:**
- **Bramka sprawdza inna kryjowke niz ta, z ktorej idzie sklad.** `GateClan` przepuszcza, gdy przy DOWOLNEJ kryjowce
  klanu jest >= 6 ludzi. Sklad ciagnie z regionu `HomeSettlement` partii, a vanilla losuje inna kryjowke.
- **Bramka globalna patrzy na caly swiat.** `GateGlobal` (lupiezcy, piraci, BK) patrzy na sume puli calego swiata.

Efekt to puste bandy, `DestroyPartyAction` co godzine i 70-380 takich par tworz/usun dziennie. Koszt i ryzyko crasha
pojawiaja sie przy kazdym usunieciu.

**Poprawka:** w `RosterPostfix`, gdy `Avail(region) < want`, wziac najblizszy region z `Avail >= MinBand`. Gdy takiego
nie ma, zwrocic sklad z 1 bandyty z puli dowolnego regionu, zamiast pustego.

### S2. Wyrzutki: ubytki i zwroty poza ludnoscia BK; czesc ludzi znika
**Plik:** `Armoury/src/OutlawLaw.cs:291`, `:184-186`, `TakeCommoners`

**Co sie dzieje:**
- **50% rozbitkow znika w nicosc.** `OutlawRoutedShare` 0.5 bierze polowe; druga polowa nie wraca do domu ani do BK.
- **Ludzie sa brani i oddawani tylko w hearth vanilli.** Produkcja BEE liczy od chlopow BK, wiec bandy nie zmniejszaja
  produkcji wsi.
- **Asymetria przy zerowym ustawieniu.** `ReturnHome` uzywa surowego `OutlawHearthPerMan`, a `TakeCommoners` wartosci
  `max(0.01, ...)`. Przy ustawieniu 0 hearth ubywa i nie wraca.

**Poprawka:**
- Druga polowe rozbitkow (i wracajacych) dodac do ludnosci BK najblizszej osady (`PopulationData.UpdatePopType`).
- W `ReturnHome` uzyc tego samego `max(0.01, per)`.

### S3. Nedza dla Levy korzysta z cache wojny, ktory czysci tylko OutlawLaw
**Plik:** `Armoury/src/OutlawLaw.cs:409` (`_war.Clear()` tylko w `Daily`, gdy `On`), `Reset()` :66

**Co sie dzieje:** przy wylaczonych wyrzutkach cache wojny w `MiseryOf` nigdy sie nie odswieza. Levy liczy wtedy chec
do sluzby po stanie wojen z pierwszego zapytania. `_war` nie jest tez czyszczony w `Reset()`, wiec frakcje z poprzedniej
kampanii zostaja w slowniku.

**Poprawka:** cache z datownikiem dnia w samym `AtWar` (jak w `Levy.Willingness`) oraz `_war.Clear()` w `Reset()`.

### S4. Zamowienie kompletu u kowala (wpis 34) prawie zawsze pusto i obok ceny rynku
**Plik:** `Armoury/src/SmithMenu.cs:1218`, `:1300` (`CheapestOf` :1195)

**Co sie dzieje:**
- **Wybrany przedmiot zwykle nie lezy na targu.** Przedmiot wybiera `CheapestOf`, czyli najtanszy w CALEJ grze dla danego
  tieru, a potem szuka go na tym targu. Najczesciej go tam nie ma ("could not find a single").
- **Cena nie jest cena rynkowa.** Cena to `Value x TroopOrderMarkup`, bez SupplyDemand i ArmsPricing (do x4.7). Gdy
  sztuka jest, zamowienie bywa tansze od kupna na tym samym targu, a towar mozna odsprzedac drozej.

**Poprawka:** wybierac najtansza sztuke danego typu i tieru obecna na targu miasta, cena = `MarketData.GetPrice(...)`
x oplata kowala.

### S5. ROT zamienia zwerbowanych na swoje typy i dopłaca lub pobiera roznice z niczego
**Plik (mod):** `rot/ROT.CampaignBehaviors/ROTTroopRecruiter.cs:205-211`

**Co sie dzieje:** po werbunku ROT podmienia zolnierza na typ z szablonu bohatera i wola
`GiveGoldAction(null, owner, koszt_stary - koszt_nowy)`. To zloto z nicosci albo w nicosc. Rekrut zmienia tez typ juz po
tym, jak VolunteerKit i DTE rozliczyly sprzet. Nasze nowe przeplywy tego nie widza.

**Poprawka:** postfix, ktory przekierowuje roznice do notabla albo miasta (albo ja zeruje), oraz log liczby podmian.

### S6. Kurier okupu (vanilla) dosypuje placacemu zlota, a nasza wycena lorda to wzmacnia
**Plik (gra):** `RansomOfferCampaignBehavior.AcceptRansomOffer` (:170-178)

**Co sie dzieje:** przy akceptacji vanilla robi `payer.Gold = ransomPrice + 1000`, gdy placacy ma mniej. Cena to x1.1
wyceny z chwili oferty, a ta wycena to nasza `LordPrice` (krol do ~250 tys.). Roznica (~10% + 1000, plus to, co placacy
wydal od oferty) powstaje z niczego.

**Poprawka:** prefix: gdy placacy (AI) ma mniej, cena = jego zloto (albo oferta anulowana); bez dosypki.

### S7. StartKit: zamiana konia moze dac uprzaz z innej rodziny
**Plik:** `Armoury/src/StartKit.cs:99`

**Co sie dzieje:** kon w slocie 10 moze byc podmieniony na konia innej rodziny, a uprzaz w slocie 11 zostaje. Wedlug
CLAUDE.md taka para to natywny crash w `AddMountMesh` (MountMeshGuard).

**Poprawka:** przy zamianie konia sprawdzic `HorseComponent.Monster.FamilyType` i w razie niezgodnosci zdjac uprzaz do
taboru (albo nie zamieniac konia).

### S8. Fabula x4.33: inne progi dni ROT bez skali
**Plik:** `CrashScribe/src/Fabula.cs` (InstallWars) + `rot/.../ROTOthersCampaignBehavior.cs:1120`

**Co sie dzieje:** sila najazdow Innych (`0.01 x dni`, sufit 50) i inne liczniki `ElapsedDaysUntilNow` w ROT rosna wedlug
starej skali. Okno ROT "Edit Storyline Wars" pokazuje daty bez skali.

**Poprawka:** dolozyc do transpilera mnoznik `1/k` przy `0.01` w tej metodzie i opisac w logu, ktore progi sa skalowane.

---

## NISKIE

### N1. Zegar przy x8 przepisuje date w kazdej klatce
**Plik:** `Armoury/src/MapClock.cs:33`

**Co sie dzieje:** minuta gry zmienia sie co klatke, wiec co klatke wykonuje sie `now.ToString()`. To przechodzi przez
`ToStringPrefix` i `GameTexts.FindText`, a potem refleksyjny `SetValue`.

**Poprawka:** zapamietac tekst daty na dzien i doklejac tylko godzine; `PropertyInfo.SetValue` zamienic na skompilowany
delegat.

### N2. TradeScreenOpen przy kazdej cenie i slownik bez zabezpieczenia watkow
**Plik:** `Armoury/src/SupplyDemand.cs:146`

**Co sie dzieje:** `TradeScreenOpen` (`TopScreen.GetType().Name.IndexOf`) wykonuje sie przy kazdej cenie, takze dla AI.
`_frozen` to zwykly `Dictionary` pisany z wnetrza `GetPrice`.

**Poprawka:** ustawiac flage w zdarzeniu otwarcia i zamkniecia ekranu ekwipunku.

### N3. Konstruktor ArmouryBehavior nie zeruje wszystkich stanow statycznych
**Plik:** `Armoury/src/ArmouryBehavior.cs:389`

Pominiete sa:
- HideoutPurge (`_pending`, `_lootRoster`)
- WinterBite (`_lastSeason`, `_longNight`)
- SupplyDemand (`_frozen`, `_loggedHour`)
- OutlawLaw (`_war`)
- MapClock (`_lastMinute`)
- Stables (`_lastBuy`)

**Poprawka:** dopisac ich `Reset()` do konstruktora.

### N4. IronBank szuka rodow liniowo w petlach
**Plik:** `Armoury/src/IronBank.cs:116`, `:214`

**Co sie dzieje:** `Clan.FindFirst` stoi w petli po dlugach, a w `Limit` w petli po rodach. Daje to O(rody x dlugi x rody)
raz dziennie.

**Poprawka:** slownik `StringId -> Clan` budowany raz na `Daily`.

### N5. Zakresy suwakow MCM nie zgadzaja sie z kodem
- `FoodConsumptionCutPercent` ma suwak 0-160, a kod obcina do 90.
- `FastForwardMultiplier` ma suwak do 32, a kod do 64.

Wyrownac zakresy.

### N6. LevyGold rozpoznaje rodzaj werbunku przez ToString enuma
**Plik:** `Armoury/src/LevyGold.cs:168`

Rozpoznawanie przez `ToString()` enuma przy kazdym werbunku jest kruche. Lepiej porownac `(int)` z wartosciami
`RecruitingDetail`.

### N7. Cofniety awans zostaje na pozycji awansowanego
**Plik:** `Armoury/src/VolunteerKit.cs:81`

Cofniety awans zostawia nizszego X na miejscu Y po sortowaniu vanilli. Szkody nie ma, bo jest trudniej dostepny, ale
kolejnosc puli nie jest posortowana.

### N8. Renty gracza nie sa widoczne
**Plik:** `Armoury/src/PopulationLaw.cs:163`

Renta dla gracza jest wyplacana bez powiadomienia i bez linii w finansach rodu. Gracz widzi tylko, ze zloto przybywa.
**Poprawka:** dodac linie do `ClanFinanceModel` (tylko opis, kwota 0) albo wiadomosc dzienna.

---

## Czego brakuje do spojnej, zamknietej ekonomii i realistycznego poboru

1. **Kasa miast z niczego.**
   - Vanilla `GetTownGoldChange` (cel 10000 + 12 x dobrobyt, przez BEE) i BK `GetMerchantIncome` tworza zloto miast
     codziennie.
   - Podatek miasta i wsi dla panow (vanilla/BK `ClanFinanceModel`) tez powstaje z niczego.
   - Bez tego kazdy "przeplyw z kasy osady" (K2) jest pozorny.
2. **Zold w nicosc.** Zold partii i garnizonow placony przez lordow znika. W zamknietym obiegu powinien trafiac do osady,
   w ktorej partia stoi albo kupuje (zolnierz wydaje), czesciowo do notabli.
3. **Najemnicy z karczmy z niczego.** `TownMercenaryData` sam odrasta: ludzie i sprzet sa z niczego, bez puli ludnosci.
   Przy Levy ~20x mniej ochotnikow, a lordowie bogatsi z rent, najemnicy stana sie glownym zrodlem wojska. Powinni
   pochodzic z puli wyrzutkow i dezerterow regionu albo z ludnosci BK miasta. Do tego:
   - gracz za najemnikow placi w nicosc (`BuyMercenaries`, `buy_mercenaries_on_consequence`), a powinien kasie miasta;
   - straz karawan rekrutowana jest z niczego.
4. **Garnizon.** Auto-werbunek (`VolunteerFromIndividualToGarrison`) nie placi notablowi. Trzeba sprawdzic, czy zabiera
   pule BK (plan pkt 2.7).
5. **Demobilizacja.** Rozwiazane partie lordow (DisbandPartyAction), partie po smierci lorda i zwolnieni przez gracza
   nie wracaja do ludnosci BK ani do puli wyrzutkow. Ich zbrojownia DTE przepada (C5).
6. **Sprzet rekruta.**
   - Komplet DTE przy werbunku (gracz i AI) jest z niczego; zakup przez notabla powinien go zastapic (W2).
   - Tier 1 powinien dostawac odziez i narzedzie z towarow wsi (len, narzedzia), a nie z szablonu.
7. **Lup z bitew.** Vanilla tworzy lup z ekwipunku szablonu poleglych, a nie z ich zbrojowni DTE, wiec zrodlo jest
   z niczego. Polaczyc lup ze zbrojownia przegranych.
8. **Jency.**
   - AI sprzedaje jencow posrednikom za zloto z niczego (`SellPrisonersAction` -> `GiveGoldAction(null, ...)`) - kazdego
     dnia, w kazdym miescie.
   - Kurier okupu dosypuje zlota (S6).
   - Jency uciekajacy albo wypuszczeni nie wracaja do ludnosci.
9. **Zrodla zlota notabli.** Notable dostaja teraz zloto za ochotnikow (LevyGold), a wydaja na awanse (VolunteerKit).
   Brakuje im zwyklego dochodu z gospodarki osady (udzial w handlu, dzierzawy). Bez tego biedne osady szybko przestana
   miec awanse. Do sprawdzenia, co dzieje sie ze zlotem zmarlego notabla.
10. **Amunicja.** Kto robi strzaly i belty dla AI (FletchForge jest tylko u gracza)? Czy DTE dopelnia kolczany z niczego
    po bitwie? VolunteerKit pomija amunicje ("dokupuje pan"), ale nie widac, zeby AiGear ja kupowal. Do sprawdzenia
    w logu `ZakupyAI`.
11. **Ludnosc.**
    - Dwie ludnosci (D7): nasza symboliczna dla rent i BK dla poboru.
    - Przyrost naturalny BK (`BKGrowthModel`) moze nie byc stosowany do klas - do potwierdzenia w logu.
    - Wyrzutki i rozbitkowie powinni byc w ludnosci BK (S2).
12. **Zasady werbunku w miastach.** Dla miast `WorkforceExcess` ziemi BK nie ma sensu. Potrzebna zasada: rzemieslnicy
    bez pracy (warsztaty stojace z braku surowca lub zlota - WorkshopLaw zna to w liczniku powodow) oraz bieda miejska.
13. **Bank.** Kapital Banku poza swiatem (C4) - powinien byc skarbcem Braavos. Do tego W6.
14. **Warsztaty.** Dzienny koszt warsztatu vanilli lub BK idzie w nicosc (C3). Pula rak i dlugi warsztatow nie sa w save
    (D5).
15. **Inne zrodla z niczego, wciaz otwarte:**
    - zboze z furazu w ScorchedEarth (C7);
    - samonaprawa sprzetu (C8);
    - AiGear: uszkodzone sztuki wchodza jako cale (C9);
    - przekierowanie handlu wsi BEE (wpis 18, swiadomie nie zrobione);
    - kryjowka: 25% wartosci lupu przy kazdym wejsciu bandy (W5).
16. **Konie.** Waski przedzial ceny (B6), skad wsie maja konie (Stables bierze teraz z ich zapasu - kto je hoduje?),
    zuzycie koni.
17. **Zapis stanu.** Premie wojenne ArmsPricing i oczekujacy lup kryjowki nie sa w save.
18. **ROT.**
    - Dosypka i pobor zlota w ROTTroopRecruiter (S5).
    - Zold z ROT Enlistment.
    - Nocna Straz (przestepcy na Mur zamiast do puli wyrzutkow).
    - Armia Innych rosnie z niczego - mozna powiazac ja z poleglymi w regionie.
