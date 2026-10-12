# PLAN KODU - paczka E5/B1 (173, PLAN 5.5): kiesa ludu czynna - lud placi za towar (kotwica wedlug srodkow), kiesy W/R/P, gmina, podzamcze, wies 35/65 z oplatami stalymi, zadowolenie z koszyka (R14), zapasy startowe (R5), haki 75/25

Status: SAM PLAN (11.10.2026). Kod, gra, ustawienia, zapisy nietkniete; nic nie budowane, bez commita. Drzewo: `SCR\e5\b1` (galaz `w-toku/e5-b1` od `w-toku/zbior-e5`
c3855de = koniec E4 7658247 + E5/0 + A1-A6 po recenzji scalenia).
Wiazace: `docs\etap5\PROJEKT-ETAP5-KIESA-LUDU-KASY-MIAST-2026-10-11.md` v2 (**P**; B1 = P rozdz. 3 "E5/B1", R1, R5, R8, R9, R11-R14, D1, D4, tabela 5.1, progi rozdz. 4),
`docs\etap5\KRYTYKA-E5.md` (K1, K2, K3, E5K1-06, -09, -11, -12, W1, W3), model `docs\PROJEKT-KIESA-LUDU-2026-10-09.md` (**KL**, L1-L9, tabele 1.3-1.5, 5.2-5.3),
decyzje Jeffa (`docs\STAN-PRAC.md`): 09.10 A/B/C, 09.10 04:25 E/F/G, 09.10 05:15 (Qohor z niewola), decyzja 15 (wies 30-50%), 11.10 16:10 (A1 regulator tryb 2 bez dosypki,
E5/0 udzial zysku kupcow), 16:20 (A6 bezwarunkowo), **12.10 00:20 UTC** (2: bez sztucznych hamulcow - "jesli ubywa, to ubywa"; 8: ubogie miasta zostaja ubogie -
"niech miasta stana sie bogate"), **00:35 UTC** (zasada testow: najpierw kod E4+E5+E6, potem JEDEN krotki zestaw 120 dob + zima 120 + zapis 9).

Skroty (SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad`):
- **K:** = `SCR\e5\b1\Armoury\src\` (linie z tego drzewa); **T:** = `SCR\e5\b1\tools\`.
- **G:** = `SCR\dec32\cs_full\` (TaleWorlds.CampaignSystem 1.4.8), **BK:** = `SCR\e4tools\bkfull\`, **BEE:** = `SCR\e3\dec36\bee\`.
- **L:** = kopia biegacego testu koncowego E4 z pomiarem E5/0 `SCR\e5\b1dane\live.log` (kopia `Modules\Armoury\Armoury-2026-10-11_14-45-59.log`, 405 pelnych dob
  108837-109241, jedna sesja od startu kampanii, BEZ porcji A) + `kiesa-ludu.csv`, `spichlerze-regiony.csv`, `zysk-kupcow.csv` z `Logs\2026-10-11_14-45-59` (kopie w tym samym
  katalogu); okna: **d2-40**, **d93-120** (= sr28 testu 120 dob), **d337-364** (sr28 konca roku). Skrypty odczytu (tylko odczyt kopii): `SCR\e5\b1tools\kl.py, sum.py, oth.py,
  towns.py, sens.py, food.py`. Raport progow E5/0 d120: `SCR\kopia-e4k\progi-d120-etap5.txt`.

---

## 0. Streszczenie decyzji planu

1. **Jeden przelacznik czynnej kiesy** (P K3): `Live` = `TownFolkLive` (NOWY bool, domyslnie true) && `TownFolkMode >= 1` && **A1 cofa "zakupy"** (`TownPurse.NoMint`) - bez A1
   lud placilby za towar, za ktory BK i tak dopisuje zloto (KL 5.1 pkt 3). Zamki dodatkowo `CastlePurseEnabled` (110 cofa "zakupy" zamkow). NIE zalezy od `LogEnabled`
   (dzis `TownFolk.On` wymaga logu - K:TownFolk.cs:113; pomiar zostaje pod `On`, gra pod `Live`). `TownFolkLive` off albo brak A1 = stan sprzed B1 co do zlota
   (E5/0 "na sucho" dalej liczy, gdy `On`).
2. **NOWY `TownFolk.Live.cs`** (partial, ok. 1000-1300 linii) - caly stan i ruch zlota B1; `TownFolk.cs`/`TownFolk.Probe.cs` tylko: bramki `On`/`Live`, kalibracja trwala w zapisie,
   czynniki kotwicy w istniejacym nawiasie konsumpcji, R14, regula niewolnych. Pozostale pliki - male wpiecia (1-15 linii) w miejscach, gdzie E5/0 ma dzis `TownFolk.Note/Sale`
   (ta sama lista, P "Pliki" B1). Model = model E5/0 (port sim2, K:TownFolk.cs:461-664) z prawdziwymi przelewami.
3. **Kotwica (R1, K1/E5K1-09)** raz na (osade, kategorie, dobe): w istniejacym postfiksie `DemandPostfix` na `UpdateDemandShift` (K:TownFolk.Probe.cs:154) - po pomiarze -
   slownik budzetu x czynnik grupy (jedzenie: `m x Afford_jedzenie`, reszta: `m x Afford_reszta`) + budzet nadwyzki (kategorie "lepsze"); NOWY postfiks `AnchorBudgetPostfix`
   (**Priority.Low** - po `HistoricalPrices.BudgetPostfix` Normal, przed pomiarem E5/0 Last) na BK `CalculateBudget` mnozy TYLKO czlon dobrobytu wiersza tym samym czynnikiem
   i tylko w nawiasie tej osady. `m = min(1, B / W)` zostaje (KL L3), ale z danych E5/0 prawie nie dziala (B 1.33 mln >> wydatek gry 0.43 mln, d93-120; m < 1 w 0.4 miasta) -
   **ogranicza Afford (srodki ludu)**, jak w KL 5.2 ("mnozy wynik przez m x Afford_grupy"). Czego lud mimo to nie zaplaci - "zjedzone bez zaplaty" (licznik, prog < 1%).
4. **Zaplata (R1):** `TownPurse.OnConsumed` (A1, z `CastlePurse.ConsumePostfix` First) zamiast cofniecia calego przyrostu: `TownFolk.PayForGoods` - klasy placa swoja czesc
   najwyzej z kiesy, kasa dostaje `paid x 98%`, gmina akcyze 2%, reszta przyrostu cofnieta jak w A1 (jedno `ChangeGold`). Zamki: podzamcze placi (bez akcyzy), **czesc
   wiktu z kuchni pana** - kredyt dworu (80% wplaty dworu do siedziby-zamku, A2) pokrywa reszte bez ruchu zlota (zloto dworu jest juz w kasie) - KL 1.2 "podzamcze: czesc wiktu
   z kuchni pana", dowod potrzeby w rozdz. 7 (podzamcze 48.8 tys. + dwor 32.1 tys. = 80.9 tys. wobec 79.8 tys. wydatku gry zamkow, d337-364). Wyjatek albo watek poza glownym -
   cofniecie A1 (nic z niczego), licznik.
5. **Kiesy klas (R11) i podzamcza (R12) - nowi posiadacze w pamieci moda** (jak sakiewki ludzi `MenPurse`, posiadacz H11): miasto W/R/P + gmina, zamek - podzamcze. Wies =
   `Village.Gold` (jak dzis). **Bez osobnego posiadacza "lokata"** (odstepstwo od P B1 "Pliki"): E5K1-12 juz mowi, ze lokata to czesc kiesy W poza `Gold` - ta sama kiesa W;
   niewydana nadwyzka zostaje w kiesie W (zwrot "przy biedzie" dzieje sie sam), w linii liczba "lokata = kiesa W ponad zapas 10 dni".
6. **Jedyne wejscie do ludu `TownFolk.Earn(st, kwota, rodzaj)`** - arytmetyka na slowniku (nie rzuca, zwraca kwote przyjeta); wolajacy przelewa platnikowi tylko reszte.
   Podatki od zarobku (pan 1/2/3% W/R/P, podzamcze 3%; cech R 1% -> gmina) liczone RAZ na dobe w `TownFolk.Daily` od zarobku okna i placone panu w bloku dobowym
   (`RentToday` - powinnosci 165, budowy, D staly 166 widza je jako dochod z ziemi). Korona 184 - **B2** (B1 nie zostawia zaslepki; B2 dopisze czlon w tej samej funkcji).
7. **Haki 75/25 (R9, KL 1.3):** jeden klucz `TownFolkServiceShare` (0.25) dla: zycia zolnierzy (`MenPurse.OnLeft`), markietanow (A3 `Sutlers.One`), ludzi karawan (E3/4),
   band i kryjowek w miescie (`OutlawLaw` - **dwa** miejsca :1846 i :1868; E5/0 liczyl tylko pierwsze), zoldu zalog (`SoldierPay.Route`). Dwor - istniejacy klucz A2
   `CourtFolkShare` (0.2) przez `CourtFull.FolkShare` (K:CourtFull.cs:63) - bez drugiego klucza `TownFolkCourtServiceShare` z P (jedna regula na jedno zjawisko).
   W zamku czesc ludu idzie do podzamcza. Liczniki K3 (`CrownIncome.NoteOwnSpend`) i 169c `NoteOwnPaid` dalej od calosci (KL 5.2 "liczy calosc, jak dotad"; zloto ludu wraca
   do kasy kupcow zakupami w ciagu dni), a ciecie K3 przy czynnym B1 = `LordShare x (1 - TownFolkMerchantProfitShare)` (P R4).
8. **Place, robocizna, obrot P, 35% wyrobow (R11, KL L5-L7):** z okna sprzedazy (te same liczniki co E5/0 `Sale`/`OnGold`/`OnItemProduced`) placone w `TownFolk.Daily`
   z kasy osady do ludu, najwyzej stan kasy (to koszt sprzedazy, nie zakup - bez `BuyFloor`); place juz wyplacone przez warsztaty (KCraftPaid) ida odtad wprost do ludu
   (`Earn`), a doplata do 35% wartosci wyrobu = max(0, 0.35 x wartosc - wyplacone).
9. **Zawor (R4):** w `TownPurse.Valve` najpierw `TownFolkMerchantProfitShare` (5%) poboru do ludu (W20/R10/P70, KL L6), reszta pan/korona jak A1. Zamek: hak
   `CastlePurse.CastleDuesSuburbShare` (dzis `static readonly 0f`, K:CastlePurse.cs:92) -> wlasciwosc `Live ? Unit(TownFolkCastleProfitShare) : 0`, czesc przelewana do podzamcza
   (dzis "zostaje w kasie zamku").
10. **Wies (R13):** postfiks na BK `BKTaxModel.CalculateVillageTaxFromIncome(Village, int)` x `VillageLordScale` (0.5) przy `Live` - 112 (`VillageTakings`) dopisuje wsi reszte sam
    ("dokladnie roznice", K:VillageTakings.cs:21-30, `TaxOf` :133-142 czyta ten sam model) -> pan z majatkami 35% (zwykla), wies 65%. Hak `VillageRentYieldsToFees`
    (K:VillageTakings.cs:59, `static readonly false`) -> wlasciwosc `Live`; w `PopulationLaw.Daily` (K:PopulationLaw.cs:1238) zamiast `continue` - **oplata stala** 4 zl rocznie
    na wolna glowe (D1) tym samym przelewem i tymi samymi licznikami co renta (jeden wlasciciel pol `RentToday`, `RentVillageToday`, `DayVillageRent`, `_rentBy`). Zakupy wsi
    w miescie targowym (0.07 zl na glowe + nadwyzka 7%/d ponad 15 dni) - prawdziwy towar z polki (`TownFolk.Villages`, ramka `GoodsLedger` F26). Danina wsi
    (`KingdomTreasury.Levies` K:KingdomTreasury.cs:393) od kiesy ponad 15 dni zakupow (P R15a).
11. **R14 (K2):** w `PayForGoods` (First, po prefiksie BK): dla grupy z Afford < 0.98 `Satisfactions[g] = migawka + min(0, delta BK) - 0.0015` (migawka - istniejace `Sat0`
    z `DemandPostfix`, przed `MakeConsumption`); Afford >= 0.98 - wynik BK. Jedzenie -> Food; reszta -> Industrial/General/Luxury. Bez latki na `UpdateSatisfaction`.
12. **R5 zapasy startowe:** w `TownPurse.TrimStartGift` (miasta) i `CastlePurse.TrimStartGift` (zamki) PRZED przycieciem - czesc daru do kies ludu (W 10, R 21, P 30, podzamcze
    10 dni, wsie do 15 dni zakupow z daru miasta targowego) **wedlug koszyka KL w cenach Clarka (indeks 1.0, KL 5.3)** - ok. 7.6 mln [R z linii sesji E5/0], reszta przycieta
    jak w A1/110. Stary zapis: raz z nadwyzki kasy ponad zapas, najwyzej polowa (KL 5.3).
13. **Niewolni (KL L8) - jedna lista z I1/H3:** `LosersFlee.SlaveCulture(kultura osady)` (K:LosersFlee.cs:74-77: ghiscari, volantine, lyseni, myrish, tyroshi, qohorik, norvos,
    valyrian, khuzait, sturgia, qartheen) zamiast nazw krolestw E5/0 (K:TownFolk.cs:66 - bez Qohoru, wbrew decyzji Jeffa 09.10 05:15). **Bez klucza `TownFolkSlaveRealms`** z P.
14. **Czynsz (KL 1.3) w calosci do przegrody P** (odstepstwo od KL H12 50% notable): zloto notabla ponad 10 500 gra zamienia na sile (+1 za 500, G NotablePowerManagementBehavior.cs
    :47-53) - czynsz ludu (ok. 22 tys./d dla notabli) stalby sie zrodlem sily notabli (rekruci). P to w BK wlasciciele domow miasta; nadwyzka P i tak wraca jako place W/R.
15. **Stawki = KL P0** (rozdz. 7): pomiar E5/0 potwierdza skale kazdej (wojsko, dwor, rzemioslo, robocizna, obrot P, akcyza, oplaty, zakupy wsi w 75-160% KL) i pokazuje,
    ze **rozklad, nie stawki** decyduje o progu "kazde miasto >= 95%" (x2 dochodu ludu - nadal 9 miast < 1.1 dochodu do wydatku) - rozdz. 7.3, pytanie do raportu Jeffa (rozdz. 14).
16. **Ksiega:** M18 `MFolk`, M19 `MFolkValve`, M20 `MFolkStart`; N9 `NFolk`; H22-H24 (kiesy ludu miast, podzamczy, gminy); `MoneyLedger.ConsumePostfix` rozdziela przyrost
    kasy w konsumpcji na "zakupy z niczego" (0 przy A1) i "lud za towar" (przelew); GoodsLedger F26; BesiegedPay K4 (rozdz. 10).
17. **Poza B1 (CLAUDE.md 8 pkt 3):** podatek wojenny 184 (B2), 1/2 zaworu i wagi rent (B3), targ przy zamku (B4), statki (B5), sakiewki weteranow i najmu (B6 - najem `LevyGold`
    dalej do kasy miasta: KL "Nie: werbunek"), glod ze skutkami (C2), dziesiecina (C3), napis w menu (C4), cena surowcow `RawPrice` (W1: zostaje na budzecie gry - odnotowane),
    `HistoricalPrices` m^k (W2).

---

## 1. Styki i zaleznosci

| # | Kto | Co B1 czyta / zmienia | Gdzie (K:) | Rozstrzygniecie |
|---|---|---|---|---|
| S1 | E5/0 TownFolk | model, okna `Note/Sale/OnGold/OnItemProduced`, kalibracja D1, nawias konsumpcji, `Sat0`, linie | TownFolk.cs:28-852, TownFolk.Probe.cs:94-266, :532-625 | B1 rozbudowuje; tryb "na sucho" zostaje przy `On && !Live` |
| S2 | A1 TownPurse | `OnConsumed` (cofniecie), `Valve` (zawor), `TrimStartGift` (dar), `NoMint`, `Reserve/Spare`, okno udzialu zysku | TownPurse.cs:291-318, :359-423, :547-620, :166-174 | haki: PayForGoods, 5% P, R5 przed cieciem; lokata nie lezy w kasie - `Spare` bez zmian |
| S3 | 110/114 CastlePurse | `ConsumePostfix` zamku, suburb share, `TrimStartGift`, `Reserve` | CastlePurse.cs:92, :259-274, :564-600, :481-542 | podzamcze placi; 15% zaworu do podzamcza; R5 zamkow |
| S4 | A2 CourtFull/ClanBudget.Court | `FolkShare`, wplata dworu | CourtFull.cs:61-63; ClanBudget.cs:612-643 | 80/20; kredyt dworu dla zamku (pkt 0.4) |
| S5 | A3 Sutlers / MenPurse | czesc miejska markietanow, `OnLeft`, `CampSpend` | Sutlers.cs:186-243 (:212 wplata, :222-223 hak B1); MenPurse.cs:270-330 (:312) | 75/25 w `One` miedzy `CampTake` a wplatami - tylko arytmetyka (recenzja A3 2-2) |
| S6 | A4 BesiegedPay | bramka oblezenia | BesiegedPay.cs:50-51, :131 | nowy rodzaj `KFolkDues` (podatki ludu i farma gminy w oblezonym miescie zostaja w kiesie/gminie) |
| S7 | A6 BattlePlunder | - | - | brak styku |
| S8 | E4/4, E4/7, E4/8 PeopleOf | skala D1 `r = PeopleOf / PeopleCal` | PopulationLaw.cs:214-223 (wolane z TownFolk.cs:392) | tylko odczyt; PeopleCal w zapisie |
| S9 | E4/6 Famine / food model | jedzenie kupione z polki -> zapas warowni (`SoldItems`) | G DefaultSettlementFoodModel.cs:82-90; Famine.cs | rozdz. 12 pkt 3 (policzone) |
| S10 | E4/10 Veterans / LevyGold | najem do kasy miasta | LevyGold.cs:154 | B1 bez zmian (B6) |
| S11 | E4/11 VillageBook | jedzenie warowni od ludzi | VillageBook.cs:1241-1260 | brak wspolnego stanu |
| S12 | 112 VillageTakings | podatek wsi z modelu, druga polowa | VillageTakings.cs:59, :133-142 | x0.5 w modelu - 112 bez zmian kodu |
| S13 | PopulationLaw renty wsi | 20% kiesy wsi | PopulationLaw.cs:1236-1266 | oplata stala przy `Live` |
| S14 | KingdomTreasury | danina wsi, K3 | KingdomTreasury.cs:393, :249 | od nadwyzki 15 dni; K3 x (1 - 5%) |
| S15 | BK | `CalculateBudget`, prefiks konsumpcji, `Satisfactions`, `BKTaxModel` | BK EconomyPatches.cs:501-600, :1085-1101; EconomicData.cs:62, :148-152; BKTaxModel.cs:271-284 | rozdz. 3 |
| S16 | B2 (drzewo `SCR\e5\b235`) | czlon korony przy podatkach ludu (184), wagi rent, statki | - (rownolegle) | B1 daje funkcje podatkow dnia; B2 dopisuje czlon; linia startowa B1 podaje klucze B3/B5 (P K3) |
| S17 | B4/B6 (drzewo `SCR\e5\b46`) | podzamcze (polka targu zamku), sakiewki weteranow do karczmy | - | B4 nie liczy jedzenia drugi raz (B1 placi konsumpcje zamku, B4 dostawa wozu); B6 przekieruje `KReleased` |

---

## 2. Stan i zapis

### 2.1 Pamiec (rozszerzenie `Folk`, K:TownFolk.cs:69-99)
```
// trwale (zapis arm_townfolk): HW, HR, HP, HS, PeopleCal, Slave, Cal (kalibracja D1 zamrozona), LW, LR, LP (kiesy: miasto W/R/P; zamek LW = podzamcze), Gmina (miasto),
//                              CourtCredit (zamek: niewykorzystany kredyt dworu, najwyzej 7 dob konsumpcji zamku)
// doba (bez zapisu): EarnWin[3] (zarobek okna W/R/P - podstawa podatkow), PaidGoods[3], ExtraOffer[3] (nadwyzka oferowana na towar), FacFood, FacRest (czynniki na jutro),
//                    PredFood, PredRest (przewidywany wydatek - SpendFood / SpendGame - SpendFood z nawiasu), AffFood, AffRest (do R14 i linii), TickPaid (MoneyLedger.ConsumePostfix)
static bool _seedDone; static double _seedAge = NaN;   // R5: "done" | "old" (stary zapis zalozony z kasy) | "" ; wiek jak A1
static bool _liveLast;                                   // wylaczenie w trakcie: zwrot kies do kas (rozdz. 6.3)
```
Liczniki linii: jak E5/0 (`Day`) + zywe: zaplacone przez lud (miasta, zamki), pokryte dworem, zjedzone bez zaplaty, korekty R14, podatki panu, farma, oplaty wsi, zakupy wsi
(kupione / chciane, sztuki), zapasy startowe, zwrot przy wylaczeniu, potkniecia.

### 2.2 Zapis
- Klucz `arm_townfolk` (`SaveText.Sync` - ok. 800 osad x ok. 70 znakow = ok. 56 KB, kawalki paczki 161), **wlasny try PRZED glownym blokiem** `SyncData` - zaraz po
  `arm_townpurse` (K:ArmouryBehavior.cs:419-427), bo zgubiona kiesa to zloto znikajace z sumy swiata. Format: `v1;seed=done|old|;cal=<doba>` + na osade
  `<StringId>,<rodzaj>,<HW>,<HR>,<HP>,<HS>,<PeopleCal>,<sl>,<LW>,<LR>,<LP>,<Gmina>,<CourtCredit>` (liczby `InvariantCulture`, `R`).
- Wczytanie bez klucza (stary zapis albo E5/0): nowa kalibracja D1 + "seed=old" przy pierwszej czynnej dobie (rozdz. 6.2). Starszy DLL ignoruje klucz - kiesy przepadaja
  z suma swiata (ostrzezenie w CHANGELOG: cofniecie DLL po B1 w trwajacej grze = utrata kies ludu, ok. 7-10 mln).
- `TownFolk.Reset()` (konstruktor, K:ArmouryBehavior.cs:395 - juz jest) czysci tez stan zywy; zapis tylko przy `Live` albo niepustych kiesach (przy wylaczonym B1 - pusty napis).
- Osada usunieta / nieznany `StringId` przy wczytaniu: kiesa do kasy osady o tym id, gdy istnieje, inaczej licznik "kiesy bez osady" i zloto do kasy najblizszego miasta (nic w nicosc).

---

## 3. Okna latek (dowod)

### 3.1 Tick osady gry - kolejnosc (G TaleWorlds.CampaignSystem.CampaignBehaviors\ItemConsumptionBehavior.cs)
`MakeConsumptionInTown` (:61-71): `DeleteOverproducedItems` (:64) -> `UpdateSupplyAndDemand` -> **`UpdateDemandShift(town, dictionary)`** (:66; cialo :178-205 wypelnia
`categoryBudget[item] = GetDailyDemandForCategory`) -> **`MakeConsumption(town, dictionary, saleLog)`** (:67 - TEN SAM slownik) -> `GetFoodFromMarket` (:68, tylko oblezenie
przy Food 0 - :98-105) -> `UpdateSellLog` -> `UpdateTownGold`. Zamki: BK `TickCastle` wola `MakeConsumptionInTown` refleksja (BK BKSettlementBehavior.cs:631). Przy tworzeniu
swiata `UpdateDemandShift` i konsumpcja biegna przed `Campaign.GameStarted` (W3, G :22-40) - wszystko B1 za bramka `GameStarted`.

### 3.2 Latki na celach konsumpcji (po B1)
| Cel | Latka | Priorytet | B1 |
|---|---|---|---|
| `UpdateDemandShift(Town, Dictionary<ItemCategory,float>)` (private) | `TownFolk.DemandPostfix` (K:TownFolk.Probe.cs:154, wpiecie :112-113) | Normal | po pomiarze G0/Idx/Sat0: przy `Live` slownik x `FacFood`/`FacRest` (kategoria jedzenia = `GroupOf == 0`, K:TownFolk.Probe.cs:274-284) + budzet nadwyzki na kategorie "lepsze" (rozdz. 4.4); zapamietuje osade i czynniki nawiasu; G0a (po kotwicy) do linii |
| BK `EconomyPatches.CalculateBudget(Town, float, ItemCategory)` (private static, BK :1085-1101; wolana przy KAZDYM wierszu polki z reszta budzetu - BK :514, :523, :552) | `HistoricalPrices.BudgetPostfix` (K:HistoricalPrices.cs:832, wpiecie :1005 bez priorytetu = Normal) -> **NOWY `TownFolk.AnchorBudgetPostfix(Town __0, float __1, ItemCategory __2, ref float __result)` Priority.Low** -> `TownFolk.BudgetPostfix` pomiar (Last, :114-115) | N / Low / Last | tylko gdy `_brOpen && __0 == _brTown && Live`: `__result -= (1 - fac_g) x HistoricalPrices.ProsperityPart(__0, __2)`; NOWY pomocnik `ProsperityPart` w HistoricalPrices zwraca czlon dobrobytu tak, jak go zostawil `BudgetPostfix` (extra / r przy `HistDemandScaling`, x `TownUse`, strzaly x `HouseUse`, bron 0) - jedno zrodlo prawdy; wywolania refleksja z `RawPrice.HouseBudget` (K:RawPrice.cs:231-234) sa poza nawiasem - bez zmian |
| `MakeConsumption(Town, Dict, Dict)` | BK prefiks bool (BK :505, `ChangeGold(num6)` :555); `CastlePurse.ConsumePostfix` **First** (K:CastlePurse.cs:259-274, wpiecie :312) -> `TownPurse.OnConsumed` (miasta, K:TownPurse.cs:291-318) / galaz zamku :265-271; `MoneyLedger.ConsumePostfix` Normal (K:MoneyLedger.cs:564-575); `TownFolk.ConsumePostfix` Last (pomiar) | First / N / Last | `OnConsumed`: przy `Live` -> `TownFolk.PayForGoods(town, made)` (rozdz. 4.2) i R14 (rozdz. 4.6); zamek tak samo w galezi zamku; ksiega po nas widzi `TickPaid` (rozdz. 10.2) |
| `DeleteOverproducedItems(Town)` | BK prefiks false (BK :606-609); `CastlePurse.ShelfPostfix` (migawka kasy) | - | bez zmian (migawka kasy dla `made`) |

Postfiksy Harmony biegna takze po prefiksie BK z `return false` (zasada 110, K:CastlePurse.cs:29-32). Skan DLL modow (11.10, plan A1 3.1): `UpdateDemandShift`, `MakeConsumption`,
`CalculateBudget` lataja tylko Armoury i BK. **BEE nie ma drugiej konsumpcji:** profile klas BEE (`GetClassDemandMultiplier`, BEE BetterEconomy.Behaviors\FeudalEconomyCampaignBehavior.cs
:174-206) czyta tylko `BEE_SettlementEconomyModel` (BEE BetterEconomy.Models\BEE_SettlementEconomyModel.cs:92), a czynny jest `DefaultSettlementEconomyModel`
(L: "Przeplywy osad: modele czynne - kasa osad TaleWorlds...DefaultSettlementEconomyModel", 14:48:20) - martwe (P ryzyko B1 "druga konsumpcja BEE" zamkniete).

### 3.3 Wies - podatek pana
- Cel: `BKTaxModel.CalculateVillageTaxFromIncome(Village village, int marketIncome)` (BK BannerKings.Models.Vanilla\BKTaxModel.cs:271-284: 0.7, Low 0.5, High 0.9);
  `AccessTools.Method(TypeByName("BannerKings.Models.Vanilla.BKTaxModel"), "CalculateVillageTaxFromIncome", new[] { typeof(Village), typeof(int) })` - **pelna sygnatura**
  (przeciazenie `(Village, bool, bool)` :212 dalo AmbiguousMatch w audycie 04.10, K:PopulationLaw.cs:1047-1052). Czynny model podatku = BKTaxModel (L 14:48:20 "podatek
  BannerKings.Models.Vanilla.BKTaxModel"); skan DLL modow: metode maja tylko BK i Armoury.
- Wolajacy: BK `VillagerSettlementEnterPatch` przez czynny model (BK EconomyPatches.cs:916; wies dostaje 50% reszty :918, majatki z czesci pana `AccumulateTradeTax` :927),
  112 `TaxOf` (K:VillageTakings.cs:133-142 - sufit pana i dopisanie reszty wsi), vanilla `VillagerCampaignBehavior` :334 (zastapiona prefiksem BK). Przeciazenie `(Village,bool,bool)`
  (169c `_mVTax`, K:ClanIncomeBook.StableD.cs:223; BK UI) - nie ruszane.
- Postfiks: `public static void VillageTaxPostfix(ref int __result)` - przy `Live` `__result = (int)(__result x Unit(VillageLordScale))`; wyjatek - wynik bez zmian + potkniecie.

### 3.4 Zadowolenie BK (R14)
`EconomicData.Satisfactions` - publiczna `float[]` (BK BannerKings.Populations\EconomicData.cs:62); BK zmienia ja tylko w prefiksie konsumpcji (+0.001 za wiersz z budzetem :549,
-0.0015 w galezi dokupu jedzenia :532-544), `UpdateSatisfaction` :148-152 (Clamp 0..1). Wplyw na dobrobyt: `Map(sat, 0, 0.85, 0.5, 0)` (BK BKProsperityModel.cs:60), a warstwa
BK liczy sie na lancuchu RBL **2 razy** (L 14:48:20 "postfiksow warstwy BK na lancuchu: 2") - kara R14 dziala na dobrobyt podwojnie (rozdz. 12 pkt 9). Indeksy grup jak w E5/0
(`Sats`, kolejnosc Food / Industrial / General / Luxury - K:TownFolk.Probe.cs:490-495).

### 3.5 Akcje gry
Pan z kiesy ludu / gminy: `lord.ChangeHeroGold(+x)` w try z wycofaniem do kiesy (wzor K:CrownRents.cs:466) + `PopulationLaw.RentToday[clan] += x` (jak zawor zamku
K:CastlePurse.cs:605). Oplata stala wsi - `GiveGoldAction.ApplyForSettlementToCharacter` w `PopulationLaw` (jak renta K:PopulationLaw.cs:1261). Kasa <-> kiesa: `Town.ChangeGold`
+ slownik (bez zdarzenia gry; posiadacz liczony w `MoneyLedger.ReadHolders`). Towar wsi: `ItemRoster.AddToCounts(-n)` na polce miasta targowego.

---

## 4. Model i funkcje (dokladnie)

### 4.1 Earn (jedyne wejscie do ludu)
`int Earn(Settlement st, int amt, int kind)`: `Live`, `GameStarted`, watek glowny; miasto - podzial klas (KL 1.3): zycie, karawany, bandy, zaloga, markietani 80/10/10;
dwor 80/20/0; place budowy 60/40/0; place rzemiosla (KCraftPaid i doplata) 70/30/0; robocizna 90/10/0; obrot -> P; zawor 5% 20/10/70; roboty gminy 50/50/0; kopalnie W;
praca niewolnych (krolestwo kultury z niewola): czesc W x HS/(HS+HW) -> R40/P60 (K:TownFolk.cs:485-489). Zamek - jedna kiesa podzamcza. Wies - nie (kiesa wsi to `Village.Gold`).
Zwraca przyjeta kwote (0 przy wylaczonym/ wyjatku - wolajacy przelewa calosc platnikowi jak dotad). `EarnService(st, amt, kind)` = `Earn(st, (int)(amt x ServiceShare), kind)`.

### 4.2 PayForGoods (z OnConsumed / galezi zamku, First)
```
made = kasa po - kasa przed (BK ChangeGold(num6) - jedyny zapis zlota w nawiasie); made <= 0 -> nic
miasto: udzialy klas w = (towar_c x AffDay + ExtraOffer_c) / suma  (towar_c - czesc koszyka klasy w kotwicy dnia, rozdz. 4.3)
        paid_c = min(made x w_c, L_c); L_c -= paid_c; paid = suma; jedzenie z dokupu BK (zapas warowni <= 10%, _brLowFood) niezaplacone przez lud -> gmina do jej stanu
        kasa: ChangeGold(-(made - paid x (1 - Excise) - zGminy x 1)); gmina += paid x Excise - zGminy; unpaid = made - paid - zGminy  -> "zjedzone bez zaplaty"
zamek:  paid = min(made, L_podzamcza); dwor = min(made - paid, CourtCredit); CourtCredit -= dwor; kasa: ChangeGold(-(made - paid)); unpaid = made - paid - dwor
TickPaid[town] = paid (do kasy); PaidGoods_c += paid_c (czynsz, leczenie, sluzba - jutro); okno robocizny: sprzedaz ludu wedlug grup nawiasu (Grp jedzenie/towar/bron)
wyjatek / poza watkiem -> cofniecie A1 calego made (jak dzis) + potkniecie
```

### 4.3 Kotwica i Afford (liczone w TownFolk.Daily na jutro; KL 5.2 z S4)
- `B = suma_c heads_c x r x koszyk_c x Idx` (koszyk W 0.27, R 0.50, P 1.46, niewolni 0.20 u wlascicieli; podzamcze 0.18 - K:TownFolk.cs:49); `W = PredFood + PredRest`
  (wydatek gry z nawiasu dzis; brak - budzet G0); `m = W > 0 ? min(1, B / W) : 1`; towar_c = B_c / B x min(W, B).
- potrzeba pelna `N_c = towar_c / keep_c` (keep W 0.91, R 0.85, P 0.92); jedzenie `F_c = N_c x fd_c` (0.80/0.70/0.45; podzamcze 0.80).
- `AffFood = suma_c s_c x min(1, L_c / F_c)`, `AffRest = suma_c s_c x min(1, max(0, L_c - F_c) / (N_c - F_c))`, `s_c = N_c / suma N` - mianownik PELNA potrzeba (S4).
  Zamek: `L = L_podzamcza + CourtCredit`. `FacFood = m x AffFood`, `FacRest = m x AffRest` (0..1).
- Osada bez nawiasu dnia (pierwsza doba po wczytaniu) - czynniki z kies wobec budzetu G0 dnia (liczone w `DemandPostfix`), nie 1.

### 4.4 Doba miasta (TownFolk.Daily, w bloku)
1. Zarobek okna -> podatki: pan `EarnWin_c x (0.01/0.02/0.03)`, cech `EarnWin_R x 0.01` -> gmina; osada oblezona - bramka `BesiegedPay.Holds(st, KFolkDues, x, true)` (zostaje w kiesie).
2. Z kasy do ludu (najwyzej stan kasy, ta kolejnosc): robocizna handlu (okno sprzedazy x 0.20/0.10/0.04 wedlug grupy; karawany i gracz 10% zakupow i sprzedazy), obrot P
   (3% sprzedazy + 3% zakupow karawan), doplata do 35% wartosci wyrobow (max(0, 0.35 x ProdV - KCraftPaid okna)).
3. Wewnatrz ludu (z wczorajszego `PaidGoods`): czynsz W 8%, R 13% wydatku (x 1/keep) -> P; leczenie 1/2/2% -> R; sluzba P 6% -> W.
4. Gmina: 7% stanu: 1/3 farma -> pan (`RentToday`, bramka oblezenia), 2/3 roboty: 75% place W/R po polowie, 25% material -> kasa (okno sprzedazy MixBuild).
5. Zapas i nadwyzka: W ponad 10 dni potrzeby: 7% -> `ExtraOffer_W` (towar jutro); R ponad 21, P ponad 30 dni: 7% -> 70% place (R -> W; P -> W 60 / R 40), 30% -> `ExtraOffer`.
6. Kotwica na jutro (4.3). Budzet nadwyzki: `ExtraOffer` (suma klas) dzielony na kategorie "lepsze" obecne w slowniku (meat, beer, wine, linen, velvet, pottery, cheese, oil -
   lista sprawdzana przy kodzie z `ItemCategories.All`, linia startowa podaje znalezione) proporcjonalnie do ich budzetu; kategorii brak - pieniadz zostaje w kiesie.
7. Linie i CSV.
Zamek: (1) pan 3% z zarobku okna; (2) robocizna z kasy zamku od sprzedazy zamku (75% zycia i zalogi x 0.20, 80% dworu x RateCourt, okno Sale); (5) nadwyzka 7% ponad 10 dni
-> `ExtraOffer` zamku (konsumpcja zamku); (6) kotwica z kredytem dworu. Wies: `TownFolk.Villages` (4.5).

### 4.5 Wies (TownFolk.Villages, w TownFolk.Daily po miastach i zamkach)
Wolne glowy `Hv = (HW + HR) x r` (D1; HW bez niewolnych w kulturze z niewola). Potrzeba `Nv = VillagePurchasePerHead x Hv`; zakupy: najpierw `min(Nv, Gold)`, potem 7% kiesy ponad
15 dni x Nv - w miescie targowym (`Village.TradeBound`, brak/zamek - `Bound`; zamek jako targ - kasa zamku), kategorie salt, tools, linen, pottery, beer (lista sprawdzana przy kodzie),
cena targu miasta, najwyzej sztuki ponad 5 na polce; towar `AddToCounts(-n)`, zloto `Village.ChangeGold(-p)` / `Town.ChangeGold(+p)`, okno robocizny miasta (RateVill
0.2 x 0.20 + 0.8 x 0.10). Brak towaru - zloto zostaje. Ramka `GoodsLedger.Begin(FFolkVill)` wokol calego `TownFolk.Daily`. Oplata stala - w `PopulationLaw` (pkt 0.10), danina -
w `KingdomTreasury.Levies` od `TownFolk.VillageFloor(st)` = 15 x Nv.

### 4.6 R14
W `PayForGoods` po zaplacie: `sat = Sats(f)`; `Sat0Ok`; dla g = Food przy `AffFood < 0.98` i g = Industrial/General/Luxury przy `AffRest < 0.98`: `sat[g] = Clamp01(Sat0[g] +
min(0, sat[g] - Sat0[g]) - 0.0015)`; liczniki "korekty R14" i "BK sam" (delta BK > 0 zastapiona). Klucz `TownFolkSatisfactionFromBasket` (off - wynik BK).

### 4.7 Funkcje (lista dla wykonawcy)
| Funkcja | Wynik |
|---|---|
| `bool Live` | pkt 0.1; zamki dodatkowo `CastleLive` = `Live && CastlePurseEnabled` |
| `int Earn / EarnService` | 4.1 |
| `void PayForGoods(Town, int made)` | 4.2 (+ R14) |
| `int TakeTickPaid(Town)` | zaplata ludu w tym ticku - dla `MoneyLedger.ConsumePostfix` (zeruje) |
| `float FacOf(Town, ItemCategory)`, `void ApplyAnchor(Town, Dictionary)` | 4.3 - w `DemandPostfix` / `AnchorBudgetPostfix` |
| `void DailyLive(int day)` | 4.4-4.5 (z `Daily()` przy `Live`; przy `!Live && On` - model na sucho jak dzis) |
| `int VillageFeeToday(Settlement)` | oplata stala dnia (D1, r) - dla `PopulationLaw` |
| `int VillageFloor(Settlement)` | 15 x Nv - dla `KingdomTreasury.Levies` |
| `int SeedTown(Settlement, int giftLeft)`, `int SeedCastle(...)`, `int SeedVillages(Settlement town, int giftLeft)` | R5 (rozdz. 6) |
| `void EarnValve(Settlement, int)`, `void EarnCastleValve(Settlement, int)` | 5% zaworu miasta (kasa -> lud), 15% zamku (kasa -> podzamcze) |
| `void CourtCredit(Settlement seat, int ix)` | 80% wplaty dworu do siedziby-zamku -> kredyt (ClanBudget.Court) |
| `long TotalTown(), TotalCastle(), TotalGmina()` | posiadacze H22-H24 (tylko `Live`; "na sucho" - 0) |
| `string Export(), void Import(string)` | 2.2 |
| `void ReturnAll()` | 6.3 |

---

## 5. Wpiecia w istniejace pliki (linie K:)

| Plik:linia | Dzis | B1 |
|---|---|---|
| `MenPurse.cs:312` (+ :315 `Note(NLife)`, :317 `TownFolk.Note`) | `st.Town.ChangeGold(life)` | `int fo = TownFolk.EarnService(st, life, KLife); st.Town.ChangeGold(life - fo);` ksiega `Note(NLife, st, life - fo)`; 169c i K3 od `life` |
| `Sutlers.cs:212` (+ `Pay` :224-243) | `town.Town.ChangeGold(townPart)` | `fo = EarnService(town, townPart, KLife)` przed wplata (arytmetyka), wplata `townPart - fo`; `Pay` z `townPart` (K3, 169c), E5/0 `Note` tylko przy `!Live`; komentarz :222-223 "hak B1" -> opis |
| `Seal31B.cs:454` (+ :456-457) | `st.Town.ChangeGold(t)` karawany E3/4 | 75/25 jak OnLeft, `Note(NLife, st, t - fo)` |
| `OutlawLaw.cs:1846` (+ :1847) i **:1868** | `town.Town.ChangeGold(spend)` - bandy i kryjowki | 75/25 (`KLifeBand`), ksiega `Note169(..., spend - fo)` przy :1847; :1868 bez ksiegi dzis (kryjowki - posiadacz "inne osady" -> kasa: migawka) - bez zmian ksiegi |
| `SoldierPay.cs:473` (+ :475-477) | `town.ChangeGold(coffers)` | `fo = EarnService(st, coffers, KGarrison)` (zamek -> podzamcze), `town.ChangeGold(coffers - fo)`, `Note(NWage, st, coffers - fo)`; `NoteOwnPaid`, `Hold`, `_castlePay` - jak dzis |
| `ClanBudget.cs:638` (+ :639-641) i `CourtFull.cs:63` | `seat.Town.ChangeGold(ix)`; `FolkShare => 0f` | `FolkShare => Live ? Unit(CourtFolkShare) : 0`; `fo = Earn(seat, (int)(ix x FolkShare), KCourt)`, kasa `ix - fo`; siedziba-zamek: `TownFolk.CourtCredit(seat, (ix - fo))` (80% - kuchnia pana); `CourtFull.Paid/Own` od czesci w kasie |
| `BuildFunding.cs:187` (+ :188-189) | `st.Town.ChangeGold(labourI)` | `Earn(st, labourI, KBuildLabour)` (calosc - place; zamek -> podzamcze), `NoteOwnPaid` tylko dla materialu (place nie wracaja zaworem) |
| `BuildFunding.cs:98-100` | `town.ChangeGold(revenue)` (kopalnie) | `Earn(town.Settlement, revenue, KMine)` przy `Live`, inaczej jak dzis |
| `WorkshopLaw.cs:323` (+ :326 `Note(NShop, wagesI)`, :328) | `town.ChangeGold(wagesI)` | `Earn(workshop.Settlement, wagesI, KCraftPaid)`; `Note(NShop)` tylko przy `!Live`; okno KCraftPaid (dla doplaty do 35%) |
| `WorkshopTrade.cs:329` (+ :330-331) i :305-322 (`PayToTown` z kiesy gracza) | `town.ChangeGold(paid)`; gracz `GiveGoldAction` -> kasa | place (`wages`): czesc z kapitalu `Earn`, z kiesy gracza - zaraz po przelewie gry `TownFolk.EarnFromTown(st, fromPurse, KCraftPaid)` (kasa -> lud, ksiega N9); utrzymanie (KShopKeep) i nadwyzka ceny (KCraftFair, :504) - kasa jak dzis |
| `TownPurse.cs:291-318` `OnConsumed` | cofniecie calego `made` | `if (TownFolk.Live) { TownFolk.PayForGoods(town, made); return; }` przed `ChangeGold(-made)`; liczniki `_dConsBack` = `made - paid` |
| `TownPurse.cs:359-423` `Valve` | `ValveSplit.Split(pay, ...)` | `int fp = Live ? (int)(pay x Unit(TownFolkMerchantProfitShare)) : 0;` `town.ChangeGold(-fp)`, `TownFolk.EarnValve(st, fp)`, podzial `pay - fp`; licznik do `ValveDay` -> `SplitTownMark(MRent, MFolkValve, suma)` |
| `TownPurse.cs:547-620` `TrimStartGift` | przyciecie | R5 (rozdz. 6.1) przed petla ciecia + `Mark(MFolkStart)` |
| `CastlePurse.cs:92` | `static readonly float CastleDuesSuburbShare = 0f` | wlasciwosc `CastleLive ? Unit(TownFolkCastleProfitShare) : 0`; :595-600 czesc podzamcza: `town.ChangeGold(-suburb); TownFolk.EarnCastleValve(st, suburb)`; po petli `SplitCastleMark(MCastle, MFolkValve, suma)`; linia startowa :356 |
| `CastlePurse.cs:259-274` galaz zamku | cofniecie `made` | `if (TownFolk.CastleLive) { TownFolk.PayForGoods(__0, made); return; }` |
| `CastlePurse.cs:481-542` `TrimStartGift` | przyciecie | R5 podzamcza przed cieciem + `Mark(MFolkStart)` przed `Mark(MTrim)` (:542) |
| `VillageTakings.cs:59` | `static readonly bool VillageRentYieldsToFees = false` | wlasciwosc `TownFolk.Live`; opis :53-54 i linia :437 |
| `PopulationLaw.cs:1238` (+ :1248-1266) | `continue` przy haku | przy haku: `rent = TownFolk.VillageFeeToday(st)`, `takeShare = 1`, dalej ta sama sciezka (dekret, przelew, liczniki); "nalezne" (:1232-1234) bez zmian |
| `KingdomTreasury.cs:393` | wies: `spare = max(0, gold)` | `TownFolk.Live ? max(0, gold - TownFolk.VillageFloor(st)) : max(0, gold)` |
| `KingdomTreasury.cs:249` (K3) | `cs = ValveLive ? cutShare x LordShare(k) : cutShare` | `x (TownFolk.Live ? 1 - Unit(TownFolkMerchantProfitShare) : 1)` |
| `BesiegedPay.cs:50-51` | `KN = 4` | `KFolkDues = 4`, `KN = 5`; nazwa w linii "Oblezone (E5/A4)" |
| `MoneyLedger.cs:82-113`, :564-575, :698-742, :795-806, :918-919 | indeksy, konsumpcja, posiadacze, bilans | rozdz. 10 |
| `MoneyLedger.Obieg.cs:244`, linia "Obieg", `VillageCashLine` | | "lud za towar" obok "zakupy"; "renta/oplaty wsi"; kiesy ludu w "Pieniadz swiata" |
| `GoodsLedger.cs:57-62` | `Kinds = 26` | `FFolkVill = 26` "zakupy wsi w miastach (B1)", `Kinds = 27`, nazwa |
| `HistoricalPrices.cs` (obok :832) | - | NOWY `internal static float ProsperityPart(Town, ItemCategory)` (3.2) |
| `TownFolk.Probe.cs:94-133` `ApplyAll` | 5 okien | + `AnchorBudgetPostfix` (Low) na `CalculateBudget`, + `VillageTaxPostfix` na `BKTaxModel` (3.3); w linii "Kiesa ludu (E5/0): okna wpiete ..." -> "(E5/B1)" z BRAK |
| `TownFolk.Probe.cs:154-266` | pomiar | bramka `On || Live` (nawias musi biec bez logu); czynniki i R14 przy `Live` |
| `TownFolk.Probe.cs:532-634` | kalibracja na sesje, niewolni po nazwach | kalibracja z zapisu (nie co sesje przy `Live`); `SlaveRealm` -> `LosersFlee.SlaveCulture(st.Culture)` (rowniez w trybie na sucho - jedna regula) |
| `TownFolk.cs:113-114, :348-373` | `On`, `Live` (GameStarted), `Daily` | `Measure` (dawne `On`), `Live` (B1), `Daily` rozgalezia na `DailyLive` |
| `ArmouryBehavior.cs:427` (po arm_townpurse) | - | `arm_townfolk` we wlasnym try (2.2) |
| `ArmouryBehavior.cs:1491` | `TownFolk.Daily()` | `var gfFolk = GoodsLedger.Begin(GoodsLedger.FFolkVill); try { TownFolk.Daily(); } ... finally { GoodsLedger.End(gfFolk); }` + `MoneyLedger.Mark(MoneyLedger.MFolk)` |
| `Settings.cs:1359-1361` + `McmSettings.cs` | grupa "The townsfolk's purse" | klucze (rozdz. 8), `python tools/gen_mcm.py` |
| `T:gen_mcm.py:105` RANGES | `'TownFolkMode': (0, 1, "0")` | zakresy nowych kluczy (rozdz. 8) |
| `T:sprawdz_logi.py` (CRLF w drzewie przez `core.autocrlf=true`, blob LF - `git ls-files --eol` = `i/lf`; bez mieszania koncow) | `etap5_main` :2590-2660 | `etap5_b1` + wolanie (rozdz. 11) |

Zaden nowy przelew nie stoi miedzy zdjeciem a dopisaniem zlota: `Earn` to arytmetyka; `PayForGoods` liczy, potem jedno `ChangeGold`; pan - `ChangeHeroGold` z wycofaniem (P4 U24).

---

## 6. Zapasy startowe (R5), stary zapis i wylaczenie

### 6.1 Nowa kampania
- Miasta (`TownPurse.TrimStartGift`, pierwszy `BeforeRents`, przed petla ciecia): dla miasta `gift = CastlePurse.StartGiftCut(gold, reserve, p, left)` (to, co A1 i tak zdejmie) ->
  `SeedTown`: kalibracja klas BK tej osady (faza a D1 - bez `PeopleOf`, ktorej przed kalibracja ksiegi nie wolamy - plan E5/0 pkt 0.4), kiesy W/R/P = dni x koszyk KL x heads
  **przy indeksie 1.0** / keep (W 10, R 21, P 30), najwyzej `gift`; potem `SeedVillages` - wsie, ktorych targiem jest to miasto, do 15 dni zakupow (ponad ich `Gold`), z reszty
  daru. Przelewy kasa -> kiesy / kiesa wsi, potem `Mark(MFolkStart)`, potem petla ciecia z `gift - seed` i `Mark(MTownTrim)` (ksiega: transfer osobno od ujscia w nicosc).
- Zamki (`CastlePurse.TrimStartGift`): podzamcze 10 dni x 0.18 x heads, najwyzej dar; `Mark(MFolkStart)` przed `Mark(MTrim)`.
- Wielkosc [R z linii sesji E5/0, L:560 "zapasy startowe na sucho 26418379 (W 16292489 / R 6425261 / P 1060879 / podzamcze 1541290 / wsie 1098461)" przy indeksie dnia 1 = ok. 4.0
  w miastach, 2.6 w zamkach]: przy indeksie 1.0 - W 4.04 mln, R 1.59 mln, P 0.26 mln, podzamcze 0.59 mln, wsie 1.10 mln = **ok. 7.6 mln** wobec daru do przyciecia ok. 14 mln
  w miastach (plan A1 rozdz. 6) i 5.09 mln w zamkach (L:"CastlePurse (110): poczatek kampanii ... zdjeto 5092298"). W kiesie W to ok. 15 dni prawdziwego wydatku W
  (towar ludu d2-40 ok. 270 tys./d). Dar mniejszy niz zapas w danym miescie - mniejszy zapas (licznik), bez siegania pod zapas kupcow.
- Linia jednorazowa `Kiesa ludu (E5/B1): zapasy startowe z daru ...` (rozdz. 9). Flaga `seed=done`.

### 6.2 Stary zapis (zapis Jeffa, zapis testowy z doby 362) i E5/0 -> B1
Pierwsza czynna doba: kalibracja D1 (klasy BK + `PeopleCal` z `PeopleOf`), kiesy z kasy kupcow ponad zapas D4 (`TownPurse.Spare`), najwyzej polowa nadwyzki i najwyzej zapas dni
(indeks 1.0) - przelew + `Mark(MFolkStart)`; podzamcze z `CastlePurse.Spendable` tak samo; wsie nic (maja wlasne kiesy; po zniesieniu renty 20% rosna same). Flaga `seed=old`.
Linia jednorazowa z liczbami. R14 od nastepnej doby (bez okresu ochronnego - C2 dotyczy glodu, nie R14).

### 6.3 Wylaczenie w trakcie (`TownFolkLive` off albo A1 off)
W pierwszej dobie bez `Live` przy niepustych kiesach: `ReturnAll` - kiesy W/R/P i gmina do kasy miasta, podzamcze do kasy zamku (przelew, `Mark(MFolk)`), kredyt dworu 0,
linia jednorazowa; potem stan sprzed B1 (haki 0, renta 20% wsi, podatek BK 70%, cofniecie A1). Ponowne wlaczenie - jak stary zapis.

---

## 7. Stawki z danych E5/0 (liczby z linii L:, okna d93-120 / d337-364)

Bieg: test koncowy E4 (bez porcji A - bez A1, A3; "zakupy" z niczego i regulator jak dawniej). P K7 kazal kalibrowac z biegu A; biegu A nie bedzie osobno (zasada testow
00:35 UTC: jeden zestaw na calosci) - dlatego stawki z tego biegu, a skutki A1/A3 doliczone w 7.4.

### 7.1 Co mierzy model "na sucho" przy stawkach KL P0 ("Kiesa ludu (na sucho)", sr28)
| Wielkosc | d93-120 | d337-364 | KL [R] (wojna) | Wniosek |
|---|---:|---:|---:|---|
| dochod ludu miast | 628 703 | 798 066 | 796 | skala zgodna |
| - wojsko (25% zycia, karawan, band, zalog) | 118 878 | 199 328 | 124 | `TownFolkServiceShare` 0.25 zostaje |
| - dwor (20%) | 28 161 | 35 169 | 49 | `CourtFolkShare` 0.2 zostaje |
| - place 35% wyrobow (z wartosci) | 115 298 (329 422) | 118 016 (337 188) | 85 (250) | 0.35 zostaje; juz wyplacone przez warsztaty 37 370 / 45 082 -> doplata z kas ok. 73-78 tys. |
| - robocizna handlu / obrot P | 170 543 / 41 554 | 234 806 / 53 502 | 284 / 64 | 0.20/0.10/0.04 i 0.03 zostaja |
| - karawany (10% + 3%) | 45 292 | 35 416 | 40 + 6.5 | zostaje |
| wydatek gry miast (towar, jedzenie) | 428 366 (98 263) | 529 268 (118 448) | 499 | dochod / wydatek = 1.47 / 1.51 - **w sumie dosc** |
| koszyk B / kotwica m (min; miast m < 1) | 1.33 mln / 1.00 (0.99; 0.4) | 1.41 mln / 1.00 (0.83; 2.9) | - | B nie wiaze (indeks cen ok. 3.4) - ogranicza Afford |
| kiesy W / R / P (dni zapasu) | 3.53 / 1.77 / 1.20 mln (9.0 / 24.0 / 136.7) | 3.99 / 1.75 / 1.51 mln (8.3 / 19.4 / 146.6) | - | P w BK = 2 600 glow (0.18% miasta, KL 2.2%) - nadwyzka P wraca placami do W/R (71-83 tys./d) |
| **stac jedzenie W / R / P** | 90.2 / 99.5 / 100% | 87.7 / 97.2 / 100% | >= 95% | **W ponizej** |
| **pelny W / R; miast < 95% jedzenie / pelny** | 87.0 / 99.2%; 23.7 / 27.3 (max 32) | 83.7 / 94.2%; 28.1 / 31.8 (max 35) | 0 | **NIE - rozklad** (7.3) |
| zjedzone bez zaplaty (bez kotwicy) | 59 596 (12.4%) | 102 816 (16.9%) | < 1% | z kotwica B1 - spodziewane < 1% (konsumpcja tnie sie do srodkow) |
| podzamcze: dochod / wydatek gry zamkow / stac | 42 755 / 51 683 / 75.1% | 48 764 / 79 817 / 61.1% (min 56.3) | 105 / 58 | dochod pol KL (dwor 8.0 tys., zawor 15% 5.3 tys. - kasy zamkow tylko 0.49 mln ponad zapas) -> **kredyt dworu** (pkt 0.4): 80% dworu do zamkow 32.1 tys. |
| wsie: dochod (65% utargu, przelewy, sakiewki) | 237 204 (197 543, 14 140, 25 521) | 240 667 (183 481, 13 710, 43 476) | 179 | zostaje |
| wsie: zakupy / potrzeba; nadwyzka; oplaty; danina | 57 420 / 72 834 (78.8%); 138 981; 9 357; 29 659 | 55 629 / 72 139 (77.1%); 141 091; 8 977; 29 442 | 70+82; 11; 17 | 0.07 i 4.0 zostaja; 77-79% to wsie bez gotowki (221 z 571 < 1 000 zl - rabunek, cykl wozu), nie stawka |
| gmina: stan / akcyza / farma / roboty | 141 269 / 9 522 / 3 544 / 7 089 | 169 405 / 11 654 / 4 250 / 8 501 | 10 + 1.3 / 4.5 / 9 | 0.02 i 7% zostaja |
| koszt (latki / doba) | 29.0 / 4.7 ms | 29.6 / 5.3 ms | - | E50-T2 NIE (34.0 ms d120) - B1 + ok. 5-10 ms [S] (rozdz. 11) |

### 7.2 Wies, zawor, zadowolenie (inne linie E5/0)
- "Gotowka wsi (E5/0)": G 343 102 / 339 939 (utarg 303 912 / 282 278); pan (licznik + renta) 86.2 / 79.4% (licznik 179 923 / 155 588 + renta 113 755 / 112 226), majatki BK 9.6 / 12.3%,
  korona 2.0 / 2.0%, wies 2.2 / 6.3%; polityka: tylko "zwykla" (545 wsi; 26 "?" bez utargu), w utargu pan 59.2 / 55.3%, majatki 10.8 / 14.7%, wies 30%.
  **`VillageLordScale` 0.5**: pan z majatkami 70% -> 35% utargu; w G: (0.35 x 282 tys. + oplaty 9.0 tys.) / 340 tys. = **ok. 31.6%** (prog zwykla 28-45%, kazda <= 50% - TAK [R]).
- "Nadwyzka kas (E5/0)": kasy miast 11.96 / 17.89 mln, zapas D4 7.14 / 8.26 mln, ponad zapasem 4.95 / 9.67 mln (83 / 91 miast); zawor A1 7% = 347 / 677 tys./d ->
  **5% dla ludu ok. 17 / 34 tys./d** (KL 39); zamki ponad zapasem 0.37 / 0.49 mln -> 15% zaworu 4.0 / 5.3 tys./d; K3 dzis 52 / 73 tys., przy A1 35 / 49 tys.
- "Zadowolenie BK (E5/0)": miasta Food / Industrial / General / Luxury 0.997 / 0.887 / 1.000 / 0.684 (d93-120), 1.000 / 0.994 / 1.000 / 0.985 (d337-364); zamki 0.55-0.64 /
  0.53-0.61 / 0.98-1.0 / 0.51-0.52; osad < 0.85: 227 / 135; zmiana dnia w konsumpcji miast BK +0.04-0.06 na grupe. **R14 -0.0015/d zostaje** (stala BK z galezi braku; od 1.0
  do progu 0.85 ok. 100 dob; zamki juz ponizej 0.85 w 3 grupach).
- "Jedzenie z targu (E5/0)": do zapasu warowni z konsumpcji miast 10 663 / 10 180 szt./d, zapas warowni 3.99 / 4.10 mln (rozdz. 12 pkt 3).

### 7.3 Dlaczego stawki nie przeprowadza progu "kazde miasto >= 95%" (kiesa-ludu.csv, doba 109199)
- 25 miast ze "stac jedzenie" < 95%: dochod ludu 86 907 wobec wydatku gry 134 315 (Y/W 0.65; dochod na glowe W 0.27), reszta 72 miast: 643 325 wobec 392 331 (1.64; 0.65 na glowe).
  Wydatek na glowe ten sam (0.42 / 0.40) - **biedne sa miasta bez wojska, warsztatow i handlu** (Hardhome 0.18, Ten Towers 0.29, Thenn 0.34, Driftwood Hall 0.37, Castle Black 0.68,
  Planky Town 0.49, Yunkai 0.54 ...), a budzet konsumpcji gry idzie z dobrobytu, nie z gospodarki miasta.
- Wrazliwosc (towns Y/W < 1.10 - z czynszem, leczeniem i podatkami): stawki x1.0 - 43 miasta, x1.25 - 29, **x1.5 - 18, x2 - 9, x3 - 4** (sens.py). Podwojenie wszystkich stawek
  (zabranie kupcom ok. 600 tys./d wiecej) nie daje zera. Dlatego stawki zostaja (P0, uzasadnienie KL), a prog projektu jest **do raportu Jeffa** (rozdz. 14): w B1 biedne miasto
  kupuje mniej (Afford), R14 obniza zadowolenie, dobrobyt i z nim budzet gry spadaja, polka tanieje - rownowaga wolno (R14 ok. 100 dob do 0.85); to zgodne z decyzjami Jeffa
  12.10 00:20 UTC pkt 2 i 8 (bez sztucznych praw; ubogie miasta zostaja ubogie).

### 7.4 Rachunek skutkow B1 [S] (d93-120, stan ustalony zaworu ok. 14 dob)
- Wsie (pan z majatkami 70% -> 35% utargu 304 tys.; renta 114 tys. -> oplaty 9.4 tys.): rody **-211 tys./d**.
- Kasy miast wobec A1: "kasy miast po B1 (szacunek)" -248 tys. + "zakupy" z niczego 432 tys. (A1 je zdejmuje) = **+184 tys./d** (lud placi 714 tys., dostaje z kas 529 tys.) ->
  zawor 7%: panom x 0.67 x 0.95 = +117 tys., koronie +58 tys., patrycjat +9 tys.
- Podatki ludu panu (pan, farma, podzamcze): +15 tys./d. Zamki: podzamcze placi kasom zamkow ok. 42 tys./d, odplywa 25% zalogi, 20% dworu, 15% zaworu, place budow i robocizna
  (ok. 42 tys.) - netto kas zamkow ok. 0.
- Korona: danina wsi od nadwyzki 29.7 tys. (dzis ok. 6.9 tys. = 2.0% z G) -> +22.8 tys. w wojnie.
- **Razem: rody ok. -79 tys./d, korona ok. +81 tys./d** (wraca rodom rentami 180 i zwrotem) - B1 przesuwa z panow wsi do panow miast i korony, w sumie dla rodow z korona ok. 0;
  wyrownanie dla panow zamkow - B3 (P). Wojsko: prog etapow w zestawie wspolnym (rozdz. 11 B1-T18); rachunek `sim_kiesa2` przed testem (P B2) - z tymi liczbami.
- A3 (markietani ok. 100-190 tys./d do kas miast - P A3) zwieksza o 25% tej kwoty dochod ludu miast z armia w poblizu (miasta biedne zwykle daleko) - poprawa "stac" glownie tam,
  gdzie juz jest dobrze.

### 7.5 Zestaw stawek (do Settings - rozdz. 8)
ServiceShare 0.25, CourtFolkShare 0.20 (A2), LabourFood/Goods/Arms 0.20/0.10/0.04, CraftWageShare 0.35, MerchantDraw 0.03, MerchantProfitShare 0.05, CastleProfitShare 0.15,
Excise 0.02, ReserveDays W 10 (R 21, P 30, podzamcze 10, wies 15 - stale KL), VillageLordScale 0.5, VillagePurchasePerHead 0.07, VillageDuesPerHeadYear 4.0, R14 0.0015
(stala), koszyk na glowe W 0.27 / R 0.50 / P 1.46 / niewolni 0.20 / podzamcze 0.18 / wies 0.07 (stale KL 1.5 - jak E5/0 K:TownFolk.cs:49). **Zmiany wobec P/KL z danych:**
kredyt dworu dla podzamcza (pkt 0.4), zapasy startowe przy indeksie 1.0 (6.1), czynsz do P (pkt 0.14), niewolni wedlug kultury (pkt 0.13).

---

## 8. Klucze (`Settings.cs`, grupa "The townsfolk's purse" po `TownFolkMode` K:Settings.cs:1361; `python tools/gen_mcm.py`; `Armoury.json` Jeffa (9 875 B, 08.10)
sprawdzone grep 12.10 - zadnego z ponizszych ani `CourtFolkShare`/`TownRentLordShare` NIE MA; przy wgraniu powtorzyc grep tez w json autotestu)

| Klucz | Typ / domyslna | RANGES | Podpowiedz MCM (EN) |
|---|---|---|---|
| `TownFolkLive` | bool true | - | the townsfolk's purse is live: townsfolk, castle folk and villages earn, pay their taxes and dues and buy their goods with their own coin; nothing the townsfolk eat appears or vanishes. Needs Town Purse Regulator (town purses as real coin). The war tax on the people and the lords' share of the town dues follow it. Off = as before stage 5 (their purses go back into the town and castle purses) |
| `TownFolkServiceShare` | float 0.25 | (0, 1) | of what soldiers, caravan men, outlaws and garrisons spend in a town or castle, this share goes to its people - beds, inns, washing, services - and the rest to the merchants |
| `TownFolkLabourFood` / `Goods` / `Arms` | float 0.20 / 0.10 / 0.04 | (0, 1) | of every sale by the merchants, this share is paid to the people who mill, bake, brew and carry (food and drink) / carry, sell and finish (cloth, tools, salt, pots) / carry and sell (arms, armour, horses, luxuries) |
| `TownFolkCraftWageShare` | float 0.35 | (0, 1) | the people's wages in every workshop's goods - the merchants pay the rest up to this share of their worth |
| `TownFolkMerchantDraw` | float 0.03 | (0, 0.2) | the merchant families' living out of their trade - a share of the merchants' daily sales |
| `TownFolkMerchantProfitShare` | float 0.05 | (0, 1) | of what a town purse gives up each day above the merchants' working coin, this share first goes to its clerks, coopers and the merchant families; the lord and the crown share the rest |
| `TownFolkCastleProfitShare` | float 0.15 | (0, 1) | of what a castle purse gives up each day, this share goes to the castle folk under its walls |
| `TownFolkExcise` | float 0.02 | (0, 0.2) | the town's excise on what its people buy - it pays the town's walls, streets and clerks, and a third goes to the lord as the town's farm |
| `TownFolkReserveDays` | int 10 | (1, 60) | days of their needs the labourers keep in hand; above that they buy better food, ale and cloth (craftsmen keep 21, merchant families 30, castle folk 10, villages 15) |
| `TownFolkAnchorBasket` | bool true | - | the townsfolk buy at most what the people of the town need and can pay for - when they run short they buy less, cloth and luxuries first, bread last |
| `TownFolkCastleCourtFeeds` | bool true | - | the lord's kitchen feeds part of his castle folk: the court's money spent at a castle pays for the food its people eat beyond what they can pay themselves |
| `TownFolkSatisfactionFromBasket` | bool true | - | the townsfolk's contentment with their goods follows what they could really buy, not the market alone |
| `TownFolkStartReserves` | bool true | - | in a new campaign the townsfolk, castle folk and villages start with a few days of their needs out of the towns' and castles' starting purses (in an old save - out of the merchants' spare coin, at most half) |
| `VillageLordScale` | float 0.5 | (0, 1) | the lord takes this share of what Banner Kings would give him from a village's market takings - 35% under the usual tax policy, 25% low, 45% high; the village keeps the rest. Replaces the old daily rent of a fifth of the village purse |
| `VillagePurchasePerHead` | float 0.07 | (0, 1) | coins a day each free villager spends in the market town on salt, tools, cloth, pots and ale |
| `VillageDuesPerHeadYear` | float 4.0 | (0, 50) | coins a year each free villager pays his lord in court, mill and market dues |

Nowe opisy istniejacych: `TownFolkMode` (K:Settings.cs:1361: "... unless Town Folk Live is on" - P), `CourtFolkShare` (A2: "with Town Folk Live ..."), `PopulationRentMaxShare`
("villages: only with Town Folk Live off"), `CastleDuesSuburbShare` - w linii startowej 110. Bez kluczy: `TownFolkCourtServiceShare` (= A2), `TownFolkSlaveRealms` (= lista I1/H3).

---

## 9. Linie logu (napisy logu po polsku jak caly pomiar; komunikatow w grze B1 nie ma)

- **Start** (`ApplyAll`): `Kiesa ludu (E5/B1): okna wpiete: ..., kotwica budzetu wiersza (CalculateBudget, Low), podatek wsi BK (BKTaxModel x VillageLordScale); BRAK: - | ...`.
- **Raz na sesje / gdy zmiana** (pierwsza doba): `Kiesa ludu (E5/B1): CZYNNA (TownFolkLive 1, TownFolkMode 1) | A1: tak | zamki (110): tak | stawki: uslugi 0.25, dwor 0.20, ... |
  kotwica: tak, kuchnia pana: tak, R14: tak | niewolni: kultury osad z listy I1 (n osad) | kategorie "lepsze": ..., zakupy wsi: ... | klucze B: TownRentLordShare x, CrownRentWeight
  Town/Castle/Village a/b/c, ShipsToPort/ShipTradeThroughPort/ShipPrizesToPort x/y/z (P K3)`.
- **Raz:** `Kiesa ludu (E5/B1): zapasy startowe z daru (nowa kampania) - kiesy W A / R B / P C, podzamcze D, wsie E (n wsi) = S; dar przyciety dalej T; miast z darem
  mniejszym niz zapas m` (warianty: stary zapis z nadwyzki, wylaczone, zwrot przy wylaczeniu).
- **Dziennie** `Kiesa ludu: dzien N | ...` - format "na sucho" (K:TownFolk.cs:726-768 - narzedzie czyta te same pola) bez "(na sucho)", z polami zywymi: `zaplacone przez lud
  miast P (W/R/P), zamki Pz (pokryte dworem Dz), zjedzone bez zaplaty A (q% wydatku gry; dokup jedzenia z gminy G)`, `kotwica: m srednio, czynnik jedzenie / reszta srednio
  (min), miast z czynnikiem < 1: k`, `lokata (kiesa W ponad 10 dni) X`, `przelewy z kas: robocizna, obrot P, doplata 35%, 5% zaworu, 15% zaworu zamkow`, `kontrola: zmiana kies =
  wplywy - wydatki (r = 0)`, `potkniecia`. CSV `kiesa-ludu.csv` (jak E5/0).
- `Podstawa robocizny (E5/B1): dzien N | sprzedaz kupcow S: towar ludu, nadwyzka, wsie, zolnierze i zalogi (75%), dwor (80%), sprzet AI, odziez, budowy, zamki, karawany i gracz,
  gmina | robocizna R (jedzenie / towar / bron), obrot P O`.
- `Prawa panskie (E5/B1): dzien N | od zarobku ludu miast X (W/R/P), podzamcza Y, farma gmin Z, oplaty stale wsi V (n wsi); gracz G; wstrzymane w oblezeniu H`.
- `Zadowolenie ludu (R14): dzien N | korekty a (Food / reszta), BK sam b; osad z Afford < 0.98: jedzenie n1, reszta n2`.
- "Gotowka wsi (E5/0)" - bez zmian nazwy (narzedzie), "renta" -> "renta/oplaty"; "Przeplywy osad", "Pieniadz swiata", "Obieg" - rozdz. 10.

---

## 10. Ksiega pieniadza i towarow - indeksy (od pierwszego wolnego; rezerwacja dla rownoleglych drzew B)

### 10.1 Indeksy
- **Marks** (dzis 0-17, M14-M16 rezerwa A1 nieuzywana - zostaje): `MFolk = 18` "kiesa ludu (B1): place, robocizna, obrot P, roboty gminy, zakupy wsi, zwrot przy wylaczeniu
  (kasy osad <-> kiesy ludu i wsi)"; `MFolkValve = 19` "zawor dla ludu (B1): 5% zaworu kas miast i 15% zaworu kas zamkow -> kiesy ludu"; `MFolkStart = 20` "zapasy startowe kies
  ludu z daru (B1, raz)"; `Marks` 18 -> 21, `MName` 21 pozycji (petla :961-964).
- **Notes** (0-8): `NFolk = 9` "kiesy ludu poza tickiem dobowym (B1): place warsztatu gracza z kiesy"; `Notes` 9 -> 10, `NName`.
- **Holders** (0-21): `HFolkTown = 22` "kiesy ludu miast", `HFolkCastle = 23` "kiesy podzamczy", `HGmina = 24` "gminy miast"; `Holders` 22 -> 25; `ReadHolders` (:698-742) +
  `TownFolk.TotalTown/TotalCastle/TotalGmina`; `StateLine` (:755+) - nowe pozycje; `WorldTotal` bez zmian (suma posiadaczy - jedna definicja).
- **GoodsLedger**: `FFolkVill = 26`, `Kinds` 27, `KindName` + grupa ujsc "wsie (zakupy w miescie, B1)".
- **BesiegedPay**: `KFolkDues = 4`, `KN = 5`.
- **Rezerwacja (do koordynatora):** B1 bierze M18-M20, N9, H22-H24, F26, K4 (BesiegedPay). Drzewa `w-toku/e5-b235` i `w-toku/e5-b46` (te sama baza c3855de) - od M21, N10, H25, F27,
  K5; w swoim drzewie zaslepki `MName[18..20]`/`HName` "(zarezerwowane E5/B1)", przy scaleniu - nazwy B1.

### 10.2 Konsumpcja w ksiedze
`MoneyLedger.ConsumePostfix` (:564-575) mierzy przyrost kasy w nawiasie - po B1 to zaplata ludu (przelew z posiadacza H22/H23), nie zrodlo: `int fp = TownFolk.TakeTickPaid(__0);
_cons[c] += delta - fp; _consFolk[c] += fp;` (NOWA tablica, czyszczona jak `_cons`). `OldBalance` (:795-806) - zrodla bez `_consFolk`; "Przeplywy osad" (:918-919):
`"zakupy" mieszkancow +0 [P] (...), lud za towar (B1) +X` - regex A1-T4 (`"zakupy" mieszkancow ([+-]?\d+)`) dalej 0. `MoneyLedger.Obieg.cs:244` - tak samo.

### 10.3 Co ksiega zobaczy (oczekiwane)
"zakupy" z niczego 0 (miasta i zamki), "lud za towar" ok. 0.43-0.63 mln/d (miasta) i 0.04-0.05 mln/d (zamki); M18 kasy miast ok. -0.3..-0.4 mln/d (place, robocizna, obrot)
i + zakupy wsi; M19 ok. -20..-40 tys./d; posiadacze H22-H24 ok. 7.6 mln w d1 nowej kampanii (zapasy startowe), potem wedlug przeplywow; **reszta niewyjasniona < 10 tys./d**.

---

## 11. Test i progi `--grupa etap5` (blok `etap5_b1`)

Zestaw wspolny (zasada 00:35 UTC): nowa kampania 120 dob + bieg zimowy 120 + zapis z doby 362 (9 dob) na calosci E4 + E5 (A, B) + E6; progi B1 + progi etapow 2-4.
Nie przechodzi -> w `Armoury.json` autotestu kolejno: `TownFolkSatisfactionFromBasket`, `TownFolkCastleCourtFeeds`, `TownFolkAnchorBasket` (wtedy "zjedzone bez zaplaty"
rosnie - tylko diagnoza), na koncu `TownFolkLive` (cale B1) - jedna zmiana naraz. Okno: doby 2-120; "sr28" = d93-120.

`etap5_b1(rep, lines, start, until)` - rusza przy linii `Kiesa ludu (E5/B1): CZYNNA`; wolane z `etap5_main` (T:sprawdz_logi.py:2614-2660) jak bloki A.
| # | Prog | Jak (linia) | Regula | Baza (E5/0) |
|---|---|---|---|---|
| B1-T1 | bledy, potkniecia | `ERROR in TownFolk` = 0; `potkniecia (\d+)` | 0 bledow; <= 5 w biegu | E50-T1 TAK |
| B1-T2 | czynna kazdej doby | linia `Kiesa ludu:` (bez "na sucho") + linia stanu CZYNNA, A1 tak | 120/120 dob; NIE, gdy bieg B bez kiesy (P K3) | - |
| B1-T2b | klucze B w linii stanu | `TownRentLordShare`, `CrownRentWeight`, statki | INFO (progi w blokach B3/B5) | 0.67; 3/1/0.25; OFF |
| B1-T3 | kontrola zamknieta | `kontrola: ... r = (\S+)` | \|r\| <= 1 zl kazdej doby | E50-T3 TAK |
| B1-T4 | reszta ksiegi | "(bilans - przyczyny)" sr28 | \|R\| < 10 000 (etap 3) | - |
| B1-T5 | "zakupy" z niczego | "Przeplywy osad (kasy miast/zamkow)" `"zakupy" mieszkancow` | 0 kazdej doby (A1-T4 dalej) | 425 318 d120 z31 |
| B1-T6 | zjedzone bez zaplaty | `zjedzone bez zaplaty (\d+) \(([\d.]+)%` sr28 | < 1% wydatku gry (miasta + zamki) | 12.4% (bez kotwicy) |
| B1-T7 | stac jedzenie - kazde miasto | `miast ze stac jedzenie < 95%: (\d+)` | **prog projektu 0 kazdej doby - spodziewane NIE** (7.3); raport: sr28, max, lista 10 najbiedniejszych z CSV, trend d93-120 wobec d40 | 23.7 (max 32) |
| B1-T8 | pelny koszyk swiat | `pelny W/R/P` waga potrzeb | >= 98% (P) - spodziewane NIE (7.1), raport | W 87.0% |
| B1-T9 | podzamcze | `podzamcze ... stac ([\d.]+)%` | >= 95% sr28 (z kuchnia pana; INFO, gdy NIE - liczba zamkow bez dworu) | 75.1% |
| B1-T10 | wies: pan w utargu | "Gotowka wsi (E5/0)" `w utargu: pan X% / majatki Y%` | pan + majatki = 25/35/45% +-2 pp wedlug polityki (dzis tylko zwykla) | 70% |
| B1-T11 | wies: pan w gotowce G | `pan z majatkami` + oplaty | zwykla 28-45%, kazda <= 50% | 95% |
| B1-T12 | renta 20% wsi | "Ludnosc:" renta wsi / `Prawa panskie` oplaty | renta 20% = 0; oplaty sr28 5-15 tys./d [R 9.0] | 112-114 tys. renty |
| B1-T13 | zakupy wsi | `zakupy (\d+) z (\d+)` | >= 75% (INFO; KL 90% - wsie bez gotowki) | 78.8% |
| B1-T14 | zapasy startowe | linia jednorazowa | nowa kampania: 1 linia, S 5-10 mln, = M20 w ksiedze +-1; zapis z d362: "z nadwyzki" | [R] 7.6 mln |
| B1-T15 | R14 | `Zadowolenie ludu (R14): ... korekty a` | korekty tylko w osadach z Afford < 0.98 (liczby zgodne z `Kiesa ludu:`); INFO | - |
| B1-T16 | koszt | `czas: latki X ms, doba Y ms` | <= 50 ms/dobe (INFO; optymalizacja na koniec - decyzja 08.10) | 34.0 ms (E50-T2 NIE) |
| B1-T17 | S4 jedzenie warowni | etap 4 F6/F7, "Jedzenie z targu" | warownie miast glodne 0 przed 30. doba zimy (etap 4) | 0 |
| B1-T18 | progi etapow 2-4 | `--grupa etap2/3/4` | glowy < 5 000 najwyzej 10; 0 bankructw; wojsko lordow w wojnie 95-115 tys. (podloga 85) | 110.0 tys. sr28 d120 |
| B1-T19 | K3 | "Korona: wplywy dnia (165)" zwrot przyciety | INFO (ciecie x 0.95) | - |
| B1-T20 | haki 75/25 | `Podstawa robocizny` / `Kiesa ludu` wojsko | wojsko w dochodzie ludu = 25% wydatkow zolnierzy w miastach +-1% (sr28) | - |

---

## 12. Kontrola calosci (zasada 0) - do "Ryzyko / co sprawdzic" w CHANGELOG

1. **A1 TownPurse.** `OnConsumed`: B1 nie dodaje drugiego zapisu zlota w nawiasie - jedno `ChangeGold(-(made - paid x 0.98))` zamiast `-made`; przy wyjatku cofniecie A1 (nic
   z niczego). `Valve`: 5% P przed podzialem - podzial pan/korona na reszcie, wiec prog A1-T8 (K / (LB + K) 0.32-0.34) bez zmian; `RentToday`/`_rentBy` bez czesci ludu;
   K3 x 0.95. `Reserve/Spare/TaxFloor/BuyFloor` bez zmian - kiesy ludu i gmina nie leza w `SettlementComponent.Gold` (E5K1-12). `TrimStartGift`: R5 przed cieciem, dwie migawki
   (M20 transfer, M12 ujscie) - `TownTrimNow()` bez zapasow ludu. Okno udzialu zysku kupcow (`CloseWindow`): zaplata ludu = prawdziwa sprzedaz, place/robocizna/obrot/5% = koszty -
   bez zmian kodu (zmiana kasy je zawiera); A1-T22 spadnie z ok. 105-128% (E5/0 sr28) - raport dla Jeffa wobec ok. 10%. Zaulki A1-T21: kasy biednych miast dostaja odtad zaplate
   ludu i zakupy wsi - mniej zaulkow [S]. Regulator tryb 2 - bez zmian.
2. **A3 markietani (hak 75/25).** `Sutlers.One` (K:Sutlers.cs:186-219): miedzy `CampTake` a wplatami tylko arytmetyka - `EarnService` nie rzuca (recenzja A3 2-2 zachowana);
   `Pay` liczy K3/169c od `townPart` (calosc - jak `OnLeft`, KL 5.2); czesc wsi (15%) bez zmian (kiesa wsi). `MenPurse.CampPlan` (rezerwy) bez zmian. Notable A3: czynsz nie idzie
   do notabli (pkt 0.14) - pasmo A3 bez nowego doplywu.
3. **E4/6 spichlerz miasta - zakupy jedzenia mieszczan (S4).** Jedzenie zjedzone przez mieszczan z polki gra dolicza do zapasu warowni (`SoldItems` z `BonusToFoodStores`,
   G DefaultSettlementFoodModel.cs:82-90; czynny model zywnosci ROT `ROTSettlementFoodModel` - L 14:48:20; czy dziedziczy ten skladnik - sprawdzic przy kodzie w dekompilacji ROT,
   linia "Jedzenie z targu" pokaze). Kotwica tnie zakupy jedzenia biednych miast (Afford_jedzenie). Policzone (food.py, doba 109199, 11.67 zl/szt.): po kotwicy ujemny bilans
   zywnosci w 6 z 97 miast (dzis 4), najkrocej **381 dob do pustego zapasu** (Elyria: zapas 19 426, bilans -8.6 -> -51.0), New Ibbish 404 doby - zapasy 17-36 tys. szt. wobec
   targu do 150 szt./d. W 120 dobach + zimie bez skutku; E4/6 (glod warowni przy Food 0, smierc mieszczan) dziala tylko przy pustym zapasie; C2 osobno. Prog B1-T17.
4. **E4/4 ludzie miast (D1).** `r = PeopleOf / PeopleCal` (stan "t:" E4/4; ksiega miast 10.47 -> 10.30 mln w roku, -1.6%); kalibracja zamrozona w zapisie. **Ryzyko:** poprawki E4
   w toku (decyzja 12.10 00:20 UTC pkt 1 "ludnosc = wszyscy zywi") moga zmienic definicje `PeopleOf` - w nowej kampanii bez skutku (stosunek), w starym zapisie skok `r` jednego dnia.
   Kontrola: CSV `skala_r` 0.9-1.1 (INFO), skok > 10% w dobie -> linia. Zamek: `PeopleOf` = 0 (K:PopulationLaw.cs:220-223) - podzamcze z ludzi BK bez skali (jak E5/0 O5).
5. **E4/10 najem i weterani.** Zaplata za najem (`LevyGold` K:LevyGold.cs:154 -> kasa miasta, 30-44 tys./d) - nie jest dochodem ludu (KL 5.2 "Nie: werbunek") - B6. Sakiewki
   zwolnionych do kiesy wsi (K:ClanBudget.cs:778, `KReleased`) - dochod wsi (G) jak dzis; B6 przekieruje czesc weteranow do karczmy. Karczma E4/10 (Veterans.cs) - bez przeplywow zlota.
6. **A2 dwor:** 80/20 i kredyt dworu (zamki) - `CourtFull.Own` (zapas "wlasnego") od czesci w kasie; gracz bez dworu (D 08.10). Kredyt dworu nie przenosi zlota - liczba w pamieci
   (zapis) najwyzej 7 dob konsumpcji zamku.
7. **A4 oblezenie:** podatki ludu i farma gminy w oblezonym miescie - `BesiegedPay.Holds(KFolkDues)`; zawor miasta/zamku juz wstrzymany (A1/110), wiec 5%/15% tez; oplaty wsi
   najezdzanych - istniejacy warunek K:PopulationLaw.cs:1242; zakupy wsi spladrowanej - jest gotowka, kupuje (nic nie blokuje); lud oblezonego miasta placi za towar (konsumpcja trwa).
8. **Nic drugi raz:** kotwica raz na (osade, kategorie, dobe) - slownik w `UpdateDemandShift`, czlon dobrobytu raz na wiersz (nie caly wynik - m^k, K1); akcyza tylko w
   `PayForGoods`; podatek pana od zarobku raz na dobe; robocizna od sprzedazy okna raz (okno czyszczone w Daily); zapasy startowe raz (flaga przed robota); jedzenie z targu do
   spichlerza liczy gra raz (B1 nic nie dopisuje); ksiega: zaplata ludu nie jest zrodlem "z niczego" (10.2); K3 nie tnie drugi raz czesci ludu.
9. **Martwy kod staje sie zywy:** haki `CastleDuesSuburbShare` (0 -> 15%), `VillageRentYieldsToFees` (false -> oplaty), `CourtFull.FolkShare` (0 -> 20%) - teraz wlasciwosci
   od `Live` (wylaczenie = stan sprzed B1 w tej samej dobie, P K3); `PopulationRentMaxShare` martwe dla wsi przy `Live` (zostaje dla miast w trybie 0 A1). Nowa praca: `DailyLive`
   ok. 800 osad + ok. 570 zakupow wsi (petla polki miasta targowego), postfiks na kazdym wierszu polki (setki na miasto) - B1-T16. R14 x2 przez warstwe BK na RBL (3.4) - wolny,
   ale podwojny skutek dla dobrobytu biednych miast.
10. **Zamknieta ekonomia:** kazdy przeplyw ma platnika i odbiorce - sakiewki/glowa/kasa -> kiesy (Earn), kiesy -> kasa (towar), kiesy -> pan (podatki), gmina -> pan (farma) /
    lud (place) / kasa (material), kasa -> kiesy (place, robocizna, obrot, 5%/15%), wies -> kasa (towar), wies -> pan (oplaty), wies -> korona (danina); jedyne zrodla/ujscia:
    bilans otwarcia (zapasy startowe z daru - zamiast ujscia daru w nicosc, wiec MNIEJ w nicosc niz A1). Nowi posiadacze w sumie swiata od d1.
11. **Ta sama kolejnosc w dobie:** `MoneyLedger.BlockOpen` -> `TownPurse.BeforeRents` (R5 miast) -> `PopulationLaw.Daily` (oplaty wsi, zawor miast z 5%) -> `Mark(MRent)` +
    `SplitTownMark(MRent, MFolkValve)` -> `CastlePurse.Daily` (R5 zamkow, 15%) -> `Mark(MCastle)` + `SplitCastleMark` -> **`TownFolk.Daily`** (podatki -> `RentToday` PRZED
    `BuildFunding` :156 i powinnosciami `KingdomTreasury` :87; place z kas; wsie; kotwica na jutro) -> `Mark(MFolk)` -> `BuildFunding` (place do ludu) -> ... -> `KingdomTreasury.Levies`
    (danina wsi od nadwyzki) -> ... -> `Sutlers` (75/25) -> `ClanBudget` (dwor 80/20, kredyt) -> ... -> `MoneyLedger.Daily` -> `TownFolk.Lines`. Zarobek po `TownFolk.Daily` (dwor,
    budowy, markietani) wchodzi do okna podatkow nastepnej doby.
12. **Gracz = AI:** 25% wydatkow ludzi gracza, podatki ludu jego miast do gracza (`ChangeHeroGold` - jak AI), wies gracza 35/65 i oplaty, kotwica i R14 w jego miastach; dworu brak.
    Z8: z 1 zl wydatku ludzi gracza we wlasnym miescie wraca najwyzej 0.75 x udzial zaworu + 0.25 x (1-3% podatku) < 1.
13. **Wyjatki i watki:** kazda osada we wlasnym try, licznik potkniec, pierwszy blad miejsca do logu - bez globalnych wylacznikow (CLAUDE.md 7); latki: wyjatek = zachowanie A1/gry.
    Wszystko na watku glownym (`OffMain` - pominiecie z cofnieciem A1 i licznikiem).
14. **Pulapka MCM:** nowe klucze nieobecne w `Armoury.json` Jeffa (12.10); B1 nie zmienia domyslnych istniejacych kluczy (wlacza nowym `TownFolkLive` - P K3).
15. **Scalanie:** B2 (`KingdomTreasury`, `TownFolk` podatki dnia, `CrownIncome`), B3 (`TownRentLordShare` 0.5 - ten sam `Valve`), B5 (`ShipsToPort181` - bez styku), B4
    (`MarketRoad`/`CastlePurse` - konsumpcja zamku placi B1, dostawa jedzenia B4), B6 (`ClanBudget`/`LevyGold`); E4 poprawki w `SCR\e4\*` - `PopulationLaw.cs` (B1: :1238 i :1258),
    `ClanBudget.cs` (:638), `ArmouryBehavior.cs` (:426, :1491) - tekstowo; `McmSettings.cs` zawsze z `gen_mcm.py` po scaleniu.

---

## 13. NK (nowa kampania) i stary zapis
- **Nowa kampania:** pelny skutek (zapasy startowe z daru, kalibracja w d1, kotwica od pierwszego ticku po `GameStarted`).
- **Stary zapis (Jeffa, zapis testowy d362):** rozdz. 6.2 - kiesy z nadwyzki kas (najwyzej polowa), wsie od razu bez renty 20% (oplaty), podatek wsi 35%, R14 od nastepnej doby;
  bez okresu rozruchu (KL 5.3). Zapis testowy d362 w zestawie (9 dob) pokazuje linie "z nadwyzki" i 0 bledow.
- Cofniecie DLL do wersji bez B1 w trwajacej grze - kiesy ludu (zloto) przepadaja: przed cofnieciem wylaczyc `TownFolkLive` i zapisac (zwrot do kas, 6.3) - wpis w CHANGELOG.

---

## 14. Pytania i uwagi
Do Jeffa - **jedno do raportu po tescie (nie blokuje kodu):** prog projektu "kazdego dnia w kazdym miescie >= 95% ludzi stac na jedzenie" (P rozdz. 4) nie przejdzie
przy zadnych rozsadnych stawkach - z pomiaru E5/0 25-32 miast (dochod ludu 0.18-0.96 wydatku gry; podwojenie stawek - nadal 9) - KL liczyl go na krolestwo, nie na miasto.
Wedlug Twoich zasad z 12.10 (bez sztucznych praw, ubogie miasta zostaja ubogie) B1 pozwala biednym miastom kupowac mniej i powoli tracic dobrobyt; w raporcie: lista miast i trend.
Rozstrzygniete w planie (do recenzji, nie do Jeffa): kredyt dworu (kuchnia pana) dla podzamcza; zapasy startowe przy indeksie 1.0; czynsz do P; niewolni wedlug listy I1; bez lokaty jako
osobnego posiadacza; bez kluczy `TownFolkCourtServiceShare`/`TownFolkSlaveRealms`; `RawPrice` bez kotwicy (W1).
Do koordynatora: rezerwacja indeksow (10.1); E5/0 nie liczyl zycia kryjowek (K:OutlawLaw.cs:1868) - B1 je obejmuje (pomiar "wojsko" wzrosnie o `_lifeHoards`).

---

## 15. Kolejnosc kodowania (galaz `w-toku/e5-b1`; kazdy krok: `cd` do katalogu projektu, `dotnet build Armoury/Armoury.csproj -c Release -v q --nologo` kod 0, osobny commit
z "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"; jeden test na koncu - zestaw wspolny)

1. **K1 szkielet bez skutku przy `TownFolkLive` off:** klucze (8) + `gen_mcm.py`; `TownFolk.Live.cs` (stan, `Live`, Earn, Export/Import, ReturnAll, Total*); bramki `Measure`/`Live`
   w TownFolk.cs/Probe.cs; kalibracja trwala; niewolni wedlug `LosersFlee`; `arm_townfolk`; MoneyLedger H22-H24 + Marks/Notes (zaslepki liczone 0); linia stanu.
2. **K2 konsumpcja:** kotwica (DemandPostfix, `AnchorBudgetPostfix` Low, `HistoricalPrices.ProsperityPart`), `PayForGoods` (TownPurse.OnConsumed, galaz zamku), `TakeTickPaid` +
   `MoneyLedger.ConsumePostfix`/`OldBalance`/"Przeplywy osad", R14, kredyt dworu.
3. **K3 doba:** `DailyLive` (podatki -> RentToday, BesiegedPay K4, place/robocizna/obrot/35%, gmina, wewnatrz ludu, nadwyzka, kotwica na jutro), `Valve` 5%, `CastleDuesSuburbShare`,
   `Mark(MFolk)`, `SplitTownMark/SplitCastleMark(MFolkValve)`, K3 x 0.95.
4. **K4 haki Earn:** MenPurse, Sutlers, Seal31B, OutlawLaw (2), SoldierPay, ClanBudget/CourtFull, BuildFunding (2), WorkshopLaw, WorkshopTrade (+ N9).
5. **K5 wies:** `VillageTaxPostfix` (BKTaxModel), `VillageRentYieldsToFees` -> oplaty w PopulationLaw, `Villages` (zakupy, GoodsLedger F26), danina od nadwyzki (KingdomTreasury :393).
6. **K6 R5 i NK:** `SeedTown/SeedVillages/SeedCastle` w obu `TrimStartGift`, stary zapis, `Mark(MFolkStart)`, linie jednorazowe.
7. **K7 linie i narzedzie:** `Kiesa ludu:` (zywa), `Podstawa robocizny`, `Prawa panskie`, `Zadowolenie ludu (R14)`, CSV; `T:sprawdz_logi.py` blok `etap5_b1` (rozdz. 11) - proba na
   logu L: (brak linii B1 - INFO, bez wyjatku) i na logu syntetycznym; CHANGELOG (Problem: "zakupy" z niczego 421-532 tys./d, stac W 87-90% i 24-32 miast < 95% na sucho,
   podzamcze 61-75%, pan 79-86% gotowki wsi, renta 20% kiesy wsi 112-114 tys./d - liczby rozdz. 7; Przyczyna: BK EconomyPatches.cs:555, K:PopulationLaw.cs:1236-1266,
   BK BKTaxModel.cs:271-284; Zmiana; Odstepstwa: pkt 0.4, 0.5, 0.7 (klucz dworu), 0.13, 0.14, R5 indeks 1.0; Ryzyko = rozdz. 12; Status: DO SPRAWDZENIA - zestaw wspolny);
   dwie recenzje diffu (ekonomia/szczelnosc; kod i kolizje).
