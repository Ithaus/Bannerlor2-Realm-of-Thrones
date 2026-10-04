# Audyt pelny ekonomii (2026-10-04, po wpisach CHANGELOG 11-39)

Tylko odczyt - kodu nie ruszalem. Zrodla: `Armoury/src`, `RealisticCaptivity/src`, `CrashScribe/src`.
Dekompilacje: `tw` (TaleWorlds.CampaignSystem), `bk`, `be`, `dte`, `rot` (scratchpad sesji 20.09).
**Nic z dzisiejszych zmian nie bylo uruchomione w grze** - wszystkie liczby to wyliczenia z kodu, nie z logu.

Zasada Jeffa, wedle ktorej oceniam: zamknieta, realistyczna gospodarka sredniowieczna - nic nie powstaje
z niczego i nic nie znika w nicosc; ceny z podazy/popytu i kosztu odtworzenia; dochody proporcjonalne do ludnosci.

Zakres: tylko ekonomia (zloto i przedmioty) w naszym kodzie oraz w sciezkach gry/modow, ktorych dotyka.

---

## 0. W pieciu zdaniach

1. Naprawy z wpisow 31-38 (zamrozona polka z kierunkiem, renta miasta z nadwyzki, okup z ekranu druzyny,
   zloto kryjowki przy przeszukaniu, minimalny termin w Banku, transpiler skarbca) sa w kodzie i sa poprawne
   w tym, co robia.
2. Najwieksza otwarta dziura dla gracza to **wartosc z niczego przy kuciu i naprawie**: zbroja z BK CRAFT / naszej
   kuzni kosztuje ok. 1/10 surowca, ktory liczy rynek (K1), a naprawa w zbrojowni jest darmowa (K2).
3. **Bank znowu pozwala rolowac dlug** - drzwiami "termin juz minal" (K3); gracz moze tez wziac pozyczke
   i jej nie oddac prawie bez kary (W1).
4. **Zamowienia lordow na zbroje (Orders.cs)** placa 1.35 x Value z niczego, a przedmiot znika (K4) -
   tego nie bylo w poprzednich audytach.
5. Najwieksze zrodla zlota z niczego, ktore zostaly, to vanilla/BK: dochod rodow (podatki, cla, dochod wsi BK),
   dosypka kasy miast, zaplata BK za konsumpcje ludnosci i "pompa" kiesy notabli (sekcja 6).

---

## 1. KRYTYCZNE

### K1. Kucie zbroi kosztuje ~1/10 tego, co rynek liczy jako koszt (B3 z AUDYT-DZIURY nadal otwarte)
**Pliki:** `Armoury/src/Recipes.cs:72-91` (`ArmourUnits`, `ArmorMaterialScale` 0.5), `Armoury/src/Patches.cs:143-225`
(`TrueArmourCost.MaterialsPostfix` - rachunek BK CRAFT), wobec `Armoury/src/ArmsPricing.cs:163-231` (`CostOf`)
i `:234-244` (`BaseOf`).

**Co sie dzieje:** w kodzie sa dwa rachunki metalu.
- Kucie (BK CRAFT, nasza kuznia, przetop, naprawa): sztabki = suma pancerza / 10 x (1 + 0.15 x (tier-1)) x 0.5.
  Plyta t6 z pancerzem lacznie ~90: 9 x 1.675 x 0.5 = **8 sztabek** stali szlachetnej (8 x 281 = ~2.3 tys.).
- Rynek (ArmsPricing, WorkshopLaw): ta sama plyta 25 kg = 25 x 0.9 x 1.4 = 31.5 kg metalu = **63 sztabki**
  (~17.7 tys.) + wegiel, len, praca = koszt ~23 tys.; podstawa ceny nie schodzi ponizej koszt/2 = ~11.7 tys.
- Gracz kuje za ~2.5 tys. surowca (+ dniowka kuzni, stamina) i sprzedaje za 12-35 tys. (polka x2, surowce x1.5).
  Miasto placi z kasy, ktora gra dosypuje z niczego (sekcja 6, poz. 2). To jest **petla zlota** gracza.
- Te same 8 sztabek dostaje AI-warsztat? Nie: WorkshopLaw liczy 63 sztabki w rudzie (`WorkshopLaw.cs:85-96`),
  wiec warsztaty AI przy tej samej cenie maja marze bliska zera, a gracz 5-10x.

**Poprawka:** jeden rachunek metalu dla wszystkiego, co dotyka sztuki:
`Recipes.ArmourUnits` (czesc metalowa) = `ceil(ArmsPricing.CostOf(item).MetalKg / 0.5)` sztabek gatunku `CostOf.Grade`,
skora/len = `LeatherKg/10`, `LinenKg/10` (jednostki targu). `ArmorMaterialScale` zostawic tylko dla miekkich
surowcow (powod z 28.08: "rynek nie ma tyle skory"). Przetop zwraca np. 50% tej liczby (strata ognia), naprawa
bierze `(1 - stan) x` tej liczby. Wtedy BK CRAFT, nasza kuznia, warsztaty AI i cena rynku licza to samo.
Najpierw logiem `SmithAudit` porownac 10 sztuk (sztabki stare/nowe), dopiero potem zmiana.

### K2. Naprawa tworzy wartosc z niczego - wojsko latajace za darmo i tani kowal
**Pliki:** `Armoury/src/TroopSelfMend.cs:46-60`, `Armoury/src/SmithMenu.cs:405-411` (`PieceCost`),
`:485-516` (`DoMendLoot`), `:1020-1050` (naprawa zbrojowni), `Settings.cs:125, 158-159`.

**Co sie dzieje:**
- `TroopSelfMend.Run` co dzien w miescie przywraca do 100% min. 3 sztuki / 10% zuzytych w zbrojowni DTE,
  **zaczynajac od najgorszych** (sort po PriceMultiplier rosnaco). Nikt nie placi (komentarz "z wlasnego zoldu" -
  ale zaden grosz nie plynie), nie ma surowca.
- Kowal (`PieceCost`) bierze `Value x (1 - stan) x 0.5 / 2` = **25% utraconej wartosci**, bez surowca.
- Wpis 37/39 dokladaja wrakow (stan 8%) i zuzycia, ktore przechodzi przez bitwe - wiec zrodel "taniego zlomu"
  przybylo.

**Petla gracza:** wrak 8% z pobojowiska (albo zuzyta sztuka kupiona tanio na targu - cena kupna liczy
modyfikator) -> do zbrojowni DTE -> 1-3 dni postoju w miescie -> sztuka 100% -> sprzedaz po pelnej cenie.
Na plycie t6 (podstawa ~12-23 tys.) zysk rzedu 10-20 tys. za sztuke, bez kosztu. U kowala: koszt 23% Value
za wrak, sprzedaz ~Value x czynniki - nadal kilkukrotny zysk.

**Poprawka:**
1. `TroopSelfMend`: platnosc z kiesy gracza do kasy miasta (np. `PieceCost x 0.5` - "zold ludzi" to i tak pieniadz
   gracza) ORAZ surowiec z taboru wg K1 (`(1 - stan) x sztabki`); bez surowca - nie lataja. Kolejnosc:
   najpierw sztuki lekko zuzyte (taniej i realistyczniej), wraki < 20% tylko przetop.
2. `PieceCost`: koszt = materialy wg K1 po cenie targu + praca (`ArmsPricing.CostOf.Labor x (1 - stan)`),
   tak zeby "kup zuzyte -> napraw -> sprzedaj" bylo co najwyzej na zero.
3. Dodatkowo prog: wrak (stan < `LootMinConditionPercent` x 3) nie da sie naprawic, tylko przetopic.

### K3. Bank Zelazny: rolowanie dlugu wrocilo przez "termin juz minal"
**Plik:** `Armoury/src/IronBank.cs:163`, `:168`, kolejnosc `:199-209` przed `:211-270`.

**Co sie dzieje:** blokada z wpisu 36 dziala tylko, gdy `DueDay > today`. W dniu terminu (i po nim, dopoki
nie ma 3 spoznien) warunek jest falszywy, a `:168` ustawia nowy termin `today + pol roku` dla CALEGO dlugu.
W `Daily` krok 1 (AI pozycza) idzie PRZED krokiem 2 (raty) - wiec AI, ktore w dniu terminu ma zloto < 10 dni zoldu
(a pozycza wlasnie dlatego, ze jest biedne), dobiera kilka tysiecy i przesuwa termin calego dlugu o pol roku.
Gracz moze zrobic to samo w Braavos. To jest B5 (rolowanie w nieskonczonosc) w nowej postaci.

**Poprawka:** w `Lend`: `if (running && d.DueDay <= today0) return 0;` (najpierw splac) - albo transze:
lista `(kwota, termin)` per pozyczka, rata = suma `transza / dni do jej terminu`. Najprosciej pierwsze.

### K4. Zamowienia lordow na zbroje: zloto z niczego, przedmiot w nicosc
**Plik:** `Armoury/src/Orders.cs:236-253` (dostawa), `:23-41` (`PickItem`), `Settings.cs:51-62`.

**Co sie dzieje:** przy dostawie `GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, Value x 1.35)` -
zloto z niczego; sztuka jest zdejmowana z taboru i **nie trafia do lorda** (znika). `HasItem` przyjmuje
**dowolna** sztuke tego `ItemObject` - kupiona na targu, zuzyta, wrak. Oferta: 50% przy wjezdzie do miasta,
co 3 dni na miasto, sztuki t2-t5 do 12 tys. Value.
**Petla gracza:** kupic wskazana sztuke na targu (przy pelnej polce i tanich surowcach cena moze byc ponizej Value:
0.25-1 x 0.5-1.5) albo oddac zuzyta -> 1.35 x Value z niczego (do ~16 tys. za sztuke).

**Poprawka:** placi lord ze swojej kiesy (`GiveGoldAction.ApplyBetweenCharacters(lord, Hero.MainHero, pay)`),
`pay = min(lord.Gold, cena tej sztuki na targu tego miasta x 1.35)`; przedmiot do zbrojowni DTE partii lorda
(`AddItemToPartyArmory`, jak w AiGear) albo do jego taboru; sztuka z modyfikatorem o `PriceMultiplier < 1`
odrzucona albo placona proporcjonalnie. Gdy lord nie ma zlota - oferta znika.

---

## 2. WYSOKIE

### W1. Gracz moze wziac pozyczke i jej nie oddac (bankructwo jest tanie)
**Pliki:** `Armoury/src/IronBank.cs:224-227`, `:237-249`; `RealisticCaptivity/src/Homes.cs:256-290` (skrzynia w domu).

**Co sie dzieje:** po 3 spoznieniach Bank zajmuje 50% `hero.Gold`, potem 25% `hero.Gold` raz dziennie; gracz traci
100 renomy. Zloto schowane w skrzyni domu (RealisticCaptivity: wplata = `GiveGoldAction(gracz, null)`, liczba
w slowniku `Vault`), w towarze, w warsztatach czy karawanach jest poza zasiegiem. Petla: pozyczyc limit (dochod
z 60 dni + 10 tys./miasto), schowac, zbankrutowac, wyjmowac zloto tylko na jeden dzien wydatkow.
**Poprawka:** przy bankructwie Bank zajmuje dziennie `max(25% zlota, 50% dziennego dochodu rodu z modelu + renty)`
(dochod rodu idzie na dlug, zanim trafi do kiesy), wlicza skrzynie domow (RealisticCaptivity moze wystawic
`internal static int VaultTotal()` - albo Bank czyta przez refleksje), a odsetki karne rosna; dodatkowo zakaz handlu
w Braavos i -relacja z Essos. AI ma ten sam mechanizm.

### W2. Ochotnik "z kupionym sprzetem": kupione sztuki dalej znikaja, a DTE dalej daje komplet z niczego (stare W2)
**Pliki:** `Armoury/src/VolunteerKit.cs:160` (`roster.AddToCounts(e, -1)` - sztuki nigdzie nie trafiaja),
`Armoury/src/AiGear.cs:69-75` (`AiRecruitsBringKit` = true -> DTE `OnTroopRecruited` daje pelny komplet).
Bez zmian od AUDYT-PONOWNY. Kazdy awans: towar z rynku w nicosc + komplet DTE z niczego.
**Poprawka:** jak w AUDYT-PONOWNY W2 - zapas notabla (`Dictionary<Hero, List<EquipmentElement>>`, w save), przy werbunku
(`LevyGold.ApplyInternalPostfix`, VolunteerFromIndividual) przelac go do zbrojowni werbujacego i pominac DTE `OnTroopRecruited`
dla tego rekruta.

### W3. VolunteerKit nie widzi czesci awansow (stare W3) - bez zmian
**Plik:** `Armoury/src/VolunteerKit.cs:66-82`. Blad roznicy pul (X->Y i nowy X tego samego dnia = darmowy Y) i zamki BK
(`BKNotableBehavior.UpdateVolunteers`) nadal nie sa obslugiwane. Poprawka jak w AUDYT-PONOWNY W3.

### W4. Kiesa notabla to pompa vanilli - LevyGold i VolunteerKit stoja na zlocie z niczego
**Pliki (gra):** `tw/.../NotablePowerManagementBehavior.cs:50-60` (`BalanceGoldAndPowerOfNotable`),
`tw/.../ClanVariablesCampaignBehavior.cs:477-487` (`DailyTickHero` - `CalculateNotableDailyGoldChange` z niczego),
`tw/.../NotablesCampaignBehavior.cs:47` (10 000 na start notabla). Nasze: `LevyGold.cs:37-42`, `VolunteerKit.cs:159-161`.

**Co sie dzieje:** notabl z ponad 10 500 zl traci nadwyzke w nicosc (zamieniana na wplyw), notabl ponizej 4 500 dostaje
dosypke z niczego (kosztem wplywu). Wiec:
- zloto, ktore LevyGold oddaje notablom za ochotnikow, w wiekszosci znika nastepnego dnia;
- zakupy sprzetu przez notabla (VolunteerKit) sa w praktyce finansowane dosypka z niczego.
**Poprawka:** prefix na `BalanceGoldAndPowerOfNotable`: nadwyzka ponad 10 500 idzie do kasy osady
(`HomeSettlement.SettlementComponent.ChangeGold`), a nie w nicosc; dosypka tylko z kasy osady (gdy ma), inaczej brak.
Postfix na `CalculateNotableDailyGoldChange` -> 0 albo z kasy osady.

### W5. Kryjowka: kasa kryjowki dalej rosnie z niczego, przedmioty wydawane bez przeszukania, stan poza Reset
**Pliki:** `tw/.../BanditSpawnCampaignBehavior.cs:149-179` (25% wartosci lupu bandy do bandy I do kryjowki przy
kazdym wejsciu - z niczego), `Armoury/src/HideoutPurge.cs:107-120` (`BuildLoot` przy zwyciestwie), `:144-151`, `:270`
("Leave" gasi `_pending`, a ekran lupow i tak sie otwiera), `ArmouryBehavior.cs:389` (brak `HideoutPurge.Reset`).

**Co sie dzieje:** wpis 34/36 wyplaca graczowi "prawdziwa kase kryjowki", ale jej zrodlem jest vanillowe +25% z niczego
przy kazdym wejsciu bandy (`OutlawLaw.GoldPrefix` blokuje tylko `DailyTick`). Przedmioty kryjowki sa zdejmowane przy
zwyciestwie i wydawane takze po "Leave without searching". `_lootRoster`, `_pendingHideout`, `_pending` przezywaja
wczytanie innego save'a - ekran lupow w nowej kampanii ze starymi `ItemObject` (ryzyko save/crash, jak przy WorkshopLaw wpis 24).
**Poprawka:** prefix/postfix na `BanditSpawnCampaignBehavior.OnSettlementEntered`: zapamietac `PartyTradeGold` bandy i kase
kryjowki, po metodzie przywrocic (albo przeniesc przedmioty bandy do `ItemRoster` kryjowki zamiast tworzyc zloto).
`BuildLoot` przeniesc do `DoSearch`; "Leave" czysci `_lootRoster` (towar zostaje w kryjowce). `HideoutPurge.Reset()` w konstruktorze.

### W6. Renty od ludnosci sa przeplywem tylko formalnie
**Pliki:** `Armoury/src/PopulationLaw.cs:161-171`; `bk/BannerKings.Patches/EconomyPatches.cs:649-651` (miasto dostaje
`cena x sztuki` za konsumpcje ludnosci z niczego), `:484` (dochod wsi BK `payTo.Gold += income` z niczego),
`:1094-1099` (wiesniak oddaje wsi 50% netto, reszta znika); `tw/.../DefaultSettlementEconomyModel.cs:75-79`.

**Co sie dzieje:**
- Nadwyzka kasy miasta ponad cel bierze sie glownie z zaplaty BK za konsumpcje ludnosci (z niczego). Renta miasta to
  wiec te pieniadze, przepuszczone przez kase. Do tego vanilla (biegnie przed nami w DailyTick) i tak kasuje 25% nadwyzki w nicosc.
- Kasa wsi pochodzi z wiesniakow, a ci sprzedaja w miescie, ktore dosypuje sobie kase z niczego (sekcja 6, poz. 2).
- Rownolegle zostaja podatki BK/vanilla od miasta i wsi (z niczego) - pan dostaje dwa dochody z tej samej osady.
  Zasada "dochod proporcjonalny do ludnosci" jest wiec spelniona tylko dla czesci dochodu.
- `:168` przepisuje recznie wzor gry `10000 + 12 x dobrobyt` - jesli kiedys BK albo BEE zarejestruje swoj model ekonomii
  osady, renta bedzie liczona od zlego celu.
**Poprawka (kolejno, kazda osobno):** (1) `:168` wolac `Campaign.Current.Models.SettlementEconomyModel` (cel = kasa + 4 x
`GetTownGoldChange`); (2) prefix na `GetTownGoldChange`: bez dosypki/kasowania (kasa miasta tylko z handlu) - to wymaga
przegladu wszystkich odplywow z miast (DailyTrade, WorkshopLaw, wiesniacy) i jest najwiekszym ryzykiem; (3) BK konsumpcja
placi z "kiesy ludnosci" zamiast `ChangeGold(+)`; (4) podatek BK od wsi i miasta zastapic renta (albo renta zastepuje
podatek: postfix na `CalculateTownTax`/`CalculateVillageTaxFromIncome` -> 0, gdy `PopulationRentEnabled`).

### W7. Arbitraz wskaznika surowcow (resztka B2): zrzut drewna obniza cene calej zbroi w miescie
**Plik:** `Armoury/src/ArmsPricing.cs:247-256` (`LocalRatio` z jednego miasta), `:287-294` (bezwladnosc 25%/dzien),
`:323` (metal = 0.3 ruda + 0.7 drewno).
**Co sie dzieje:** po zwezeniu do 0.5-1.5 nadal mozna: kupic ~250 drewna gdzie indziej (~6 tys.), sprzedac w miescie A
(spada indeks kategorii drewna do ~0.5), po 3-4 dniach metal w A liczy sie x0.65 - wszystkie plyty i kolczugi w A tansze
o ~35%, kupic, sprzedac w B. Na kilku plytach t5-t6 to dziesiatki tysiecy wobec kilku tysiecy straty na drewnie.
**Poprawka:** wskaznik z sredniej regionu (miasta w zasiegu `SupplyDemandTradeRange`, wazonej odlegloscia) i sredniej
z 7 dni; waga drewna w metalu 0.4 zamiast 0.7; wskaznik nie spada ponizej poprzedniego dnia o wiecej niz 5%.

### W8. Zamowienie kompletu u kowala tworzy sztuki z niczego (nowy blad z wpisu 34) + stare S4
**Plik:** `Armoury/src/SmithMenu.cs:1300-1307` (`DoOrderKit`), `:1195-1219` (`CheapestOf`, `OrderPieceCost`).
**Co sie dzieje:** `GetItemNumber(item)` zwraca ilosc PIERWSZEGO elementu tego przedmiotu (dowolny modyfikator,
`ItemRoster.FindIndexOfItem`), a `AddToCounts(item, -k)` zdejmuje tylko element BEZ modyfikatora. Gdy na polce leza tylko
zuzyte egzemplarze - `AddToCounts` konczy sie `Debug.FailedAssert` i nic nie zdejmuje, a gracz dostaje k nowych sztuk
(`armory.AddToCounts(item, k)`). Gdy sa obie wersje - zdejmuje mniej, niz dostaje. Do tego (stare S4): sztuka to
najtansza w CALEJ grze, a cena `Value x 1.15` z pominieciem cen rynku.
**Poprawka:** iterowac po elementach polki (`GetElementCopyAtIndex`), brac konkretne `EquipmentElement` (najpierw bez modyfikatora),
zdejmowac i wkladac TEN SAM element; cena = `MarketData.GetPrice(element, MainParty, false, st.Party) x TroopOrderMarkup`;
wybor sztuki - najtansza danego typu i tieru obecna na tej polce.

---

## 3. SREDNIE

### S1. Konie liczone dwa razy (B6) - bez zmian
`Armoury/src/SupplyDemand.cs:70` (Horse w `Equipmentish`) na wierzchu vanilli (konie sa towarem handlowym, 0.1-10, p=0.3).
Polka koni x0.25-2 razy vanilla 0.1-10. Poprawka: dla `Horse` z kategoria `IsTradeGood` pominac nasz czynnik polki
(albo dzielic przez czynnik vanilli).

### S2. Sztabki i wegiel wyceniane indeksem rudy/drewna; wytop stali valyrianskiej +38%
`tw/.../TownMarketData.cs:129-133` (cena per kategoria), propozycja 3 z AUDYT-TOWARY niezrobiona; `MaterialLaw.cs:98-104`
+ `ValyrianSteel.cs:55-66` (10 stali szl. 2810 + 10 wegla 90 -> 4 valyrianskie 4000), drewno -> wegiel +44% (B7).
Gracz sprzedaje sztabke stali szlachetnej (281) w miescie z pustym rynkiem rudy po indeksie do x10 (pierwsza sztuka ~2.8 tys.).
Z przetopu tanio kupionej zbroi z zawalonej polki da sie na tym zarabiac. Poprawka: sztabki i wegiel do naszego `PricePostfix`
jako osobny koszyk (sztuki TEGO przedmiotu), ceny MaterialLaw = koszt lancucha x 1.1.

### S3. AiGear: kupiona zuzyta sztuka wchodzi do zbrojowni jako nowa (C9) - i AI wlasnie takie wybiera
`Armoury/src/AiGear.cs:192-205`: `score = Effectiveness / cena`, a cena liczy modyfikator, Effectiveness nie - wiec AI
preferuje najbardziej zuzyte sztuki; `_add.Invoke(mp.Id, Item, n)` gubi modyfikator. Wartosc z niczego po stronie AI
(i wyciaganie z rynku zlomu, ktory gracz tam sprzedal). Poprawka: pomijac `ItemModifier` o `PriceMultiplier < 0.9`
albo liczyc `score = Effectiveness x PriceMultiplier / cena`; do zbrojowni z modyfikatorem (DTE `ArmyArmory` ma wersje z `EquipmentElement`?
jesli nie - nie kupowac zuzytych).

### S4. LevyGold placi notablowi pelny koszt bez wzgledu na to, co lord faktycznie zaplacil
`Armoury/src/LevyGold.cs:35-41`: vanilla `GiveGoldAction(lord, null, koszt)` zabiera najwyzej tyle, ile lord ma (Gold >= 0),
a `LeaderHero == null` nie placi nic; nasz postfix i tak daje notablowi `koszt`. Rzadkie (AI zwykle ma zloto), ale to zloto
z niczego. Poprawka: prefix zapamietuje `LeaderHero.Gold` / `PartyTradeGold`, postfix oddaje roznice. Dodatkowo nadal
w nicosc: najemnicy gracza (`RecruitmentCampaignBehavior.cs:714, 794`), garnizon `VolunteerFromIndividualToGarrison` darmowy.

### S5. Nowe partie AI: brak kompletu na starcie, ale bitwa i lup dalej z szablonu (stare W4) - bez zmian
`LevyGold.cs:54-66`. DTE dopelnia puste sloty sprzetem szablonu w bitwie, vanilla generuje lup z szablonu poleglych.
Poprawka wymaga decyzji Jeffa (AUDYT-PONOWNY W4).

### S6. ROT: wycena lorda z naszym narzutem trafia do lupu zaciagnietego gracza, bez obciazenia rodu
`rot/ROT.HarmonyPatches.Core/EnlistmentPatches.cs:355-360` liczy `PrisonerRansomValue(..., Hero.MainHero)` - nasza `LordPrice`
(`FairRansom.cs:71-118`) daje do ~250 tys. (ograniczone kiesa glowy rodu), ale nie zdejmuje jej (nie jestesmy w `_inSale`).
ROT zamienia te wartosc na losowe przedmioty z niczego (`:396-420`, do 3 sztuk). Plus stare S5 (ROTTroopRecruiter doplaca roznice
z niczego) i S6 (kurier okupu `RansomOfferCampaignBehavior.cs:174-176` dosypuje placacemu do ceny + 1000). Poprawka: w `LordPrice`
nie podnosic, gdy wola ROT Enlistment (sprawdzic `SubModule.EnlistmentBehavior.IsEnlisted` refleksja); prefix na `AcceptRansomOffer`.

### S7. Stan statyczny poza `Reset()` (stare N3, czesciowo)
`ArmouryBehavior.cs:389` wola teraz 13 resetow, ale nadal brakuje: `HideoutPurge` (W5), `SupplyDemand._frozen/_loggedHour`,
`Stables._lastBuy`, `OutlawLaw._war` (stare S3 - `_war` czyszczony tylko w `Daily` przy wlaczonych wyrzutkach, `OutlawLaw.cs:360-370, 409`),
`MapClock`. Najgrozniejsze HideoutPurge (stare obiekty gry w nowej kampanii).

### S8. Powinnosci wobec korony od dochodu BRUTTO; jeden wyjatek zatrzymuje caly dzien
`Armoury/src/KingdomTreasury.cs:57-65`: 10% w wojnie od `CalculateClanIncome` (brutto, bez zoldu) - rod z ujemnym bilansem
placi i tak, co pcha AI do Banku (zamierzone, ale skala nieznana). Petla w jednym `try` (`:49-75`) - wyjatek przy jednym rodzie
(np. `c.Kingdom.RulingClan == null`) przerywa pobor dla wszystkich na ten dzien; to samo w `IronBank.Daily` (`:187-283`).
Gracz placi bez komunikatu. Poprawka: podstawa = `max(0, CalculateClanGoldChange)` (netto) + renty; `try` per rod; komunikat dla gracza.

### S9. Bankrut AI zostaje bankrutem na zawsze; kapital Banku poza swiatem (C4)
`IronBank.cs:224-227`: 25% zlota dziennie + odsetki od rosnacego dlugu, bez konca - rod nie odbuduje zoldu, dezercje ->
wyrzutki. Odsetki i zajecia wychodza ze swiata na stale (`_capital`). Poprawka: po bankructwie stala rata (np. 30% dochodu),
odsetki zamrozone; kapital Banku jako kasa Braavos (`town_EN5`) - pozyczka z kasy miasta, splata do niej.

### S10. RealisticCaptivity: drobne zlota z niczego i w nicosc
`Homes.cs:184` (kupno domu w nicosc), `:240` (sprzedaz domu + skrzynia z niczego; dom rodzinny dany za darmo da sie sprzedac raz),
`:278-284` (skrzynia - neutralna, ale chowa zloto przed Bankiem, W1), `Work.cs:289, 325, 341` (dniowka jenca/strazy z niczego),
`Patches.cs:96` (lapowka w nicosc). Poprawka: wszystko przez kase osady (`GiveGoldAction.ApplyForCharacterToSettlement` /
`ApplyForSettlementToCharacter`), dniowka tylko gdy osada ma zloto.

---

## 4. NISKIE

- **N1.** `MarketGlut.cs:97-104` - podloga 5% Value podnosi cene wraku (stan 3-8%) do 5%, a potem `SupplyDemand` mnozy to jeszcze
  polka i surowcami (do ~x3). Podloge liczyc po mnozniku stanu: `max(5% x PriceMultiplier, ...)`.
- **N2.** WorkshopLaw: `_owed`/`_labor` poza save (D5); vanilla dzienny koszt warsztatu notabla w nicosc
  (`tw/.../WorkshopsCampaignBehavior.cs:794-799`, C3) obok naszych plac.
- **N3.** `Patches.cs:330-341` (`SwallowForgeFee`) polyka KAZDA platnosc gracza do osady w menu `bannerkings_wait_crafting`,
  `arm_project_wait`, `arm_work_wait` przy oplaconej dobie. Dzis zadna nasza `Pay.ToSettlement` tam nie wypada (`WorkTick` przelacza
  menu przed `apply()`, `SmithMenu.cs:1655-1659`), ale to pulapka na przyszlosc - lepiej flaga "to jest godzinowka BK" niz menu.
- **N4.** `StartKit.cs:99` - zamiana konia bez sprawdzenia rodziny uprzezy (stare S7, ryzyko natywnego crasha w `AddMountMesh`).
- **N5.** `FairRansom.cs:114-118` - gdy jakis sluchacz `OnPrisonerSold`/`OnHeroPrisonerReleased` wywola `PrisonerRansomValue(x, gracz)`
  w trakcie `ApplyInternal`, nadwyzka zejdzie drugi raz. Dzis w BK/ROT nie znalazlem takiego wywolania; zabezpieczenie: `HashSet<Hero>` na sprzedaz.
- **N6.** `LevyGold.cs:33` rozpoznaje rodzaj werbunku przez `ToString()` enuma (stare N6); `IronBank.cs:116, 217` `Clan.FindFirst` w petlach
  (stare N4); `SupplyDemand.DailyTrade` O(koszyki x osady^2) - przy x8 kosztowne.
- **N7.** WorkshopLaw zdejmuje surowiec `AddToCounts(ItemObject, -n)` (bez modyfikatora) po `GetItemNumber` - ten sam wzorzec co W8;
  surowce zwykle nie maja modyfikatorow, wiec praktycznie bez skutku, ale warto ujednolicic.

---

## 5. Potwierdzone naprawy (sprawdzone w kodzie)

| Co | Gdzie | Ocena |
|---|---|---|
| K1 (AUDYT-PONOWNY) zamrozona polka z kierunkiem | `SupplyDemand.cs:173-190`, `:196` | OK - kupno `min(zamrozony, zywy)`, sprzedaz `max`; obieg kup-zamknij-otworz-sprzedaj jest symetryczny (zysk ~0 minus kara vanilli). Stary snapshot po ponownym otwarciu dziala zawsze na niekorzysc gracza. Drobiazg: `Substitution` (`:115-138`) patrzy tylko na zamrozony widok. |
| K2 renta miasta tylko z nadwyzki | `PopulationLaw.cs:168-169` | OK w tym, co robi (dosypka nie jest wywolywana przez rente). Zastrzezenia - W6. |
| W1 okup z ekranu druzyny | `FairRansom.cs:161-186`, hak `:220-221` | OK - sygnatura `ApplyInternal(seller, buyer, prisoners, applyConsequences)` zgodna (`tw/.../SellPrisonersAction.cs:10`), `__3=false` = ekran, sciezka posrednika bez podwojnego pobrania. |
| W5 zloto kryjowki przy przeszukaniu | `HideoutPurge.cs:84-92`, `:475-491` | OK dla zlota; przedmioty i zrodlo - W5 wyzej. |
| W6 minimalny termin przed nowa pozyczka | `IronBank.cs:163` | Dziala tylko przed terminem - K3. |
| B5 oplata 2%, zajecie 50% | `IronBank.cs:159-165`, `:240-242` | OK. |
| Transpiler skarbca (wpis 38) | `KingdomTreasury.cs:23-36` vs `ClanVariablesCampaignBehavior.cs:415-424` | OK - w metodzie sa dokladnie int 1000/100000/200000/400000 (dosypki); 2000000 i progi float (1000000f, 100000f) nietkniete; 10000 z `DebtToKingdom` nietkniete. |
| Powinnosci wasali | `KingdomTreasury.cs:45-76` | Liczy `CalculateClanIncome(..., applyWithdrawals:false)` - bez skutkow ubocznych (BK `ApplyWithdrawal` tylko przy `true`, `EconomyPatches.cs:449-460`). Uwagi - S8. |
| C1 hodowca z zapasu wsi | `Stables.cs:447-476` | OK - kon ze wsi, zloto do wsi. |
| C2 oplaty kowala/kuzni do kasy osady | `Pay.cs`, `SmithMenu.cs:518, 853, 882, 1050`, `Forge.cs:274` | OK (`Forge.Begin` wolane z menu `armoury_forge`, nie polykane przez N3). Zamowienie kompletu - W8. |
| C6 zloto scalanej bandy do bossa | `HideoutPurge.cs:238, 392` | OK. |
| Zloto band: dzienna dosypka i start | `OutlawLaw.cs:627-646` | OK (prefix/postfix na `BanditSpawnCampaignBehavior.DailyTick` przywraca stan). Wejscie do kryjowki - W5. |
| Paser band | `OutlawLaw.cs:692-733` | OK - `Find` pomija sztuki z modyfikatorem, zloto do kasy miasta. |
| Wpis 19 - surowce z niczego | `WorkshopLaw.cs:110-117, 292-326` | OK - blokada linii `artisans` bez wsadu i zamiana wegla/sztabek z losowania. |
| Zerowanie stanow (wpis 24) | `ArmouryBehavior.cs:389` | Czesciowo - brakujace w S7. |
| LevyGold zloto AI do notabla/miasta | `LevyGold.cs:26-52` | Dziala; kwota - S4; los tego zlota - W4. |

---

## 6. Zloto i towar z niczego, ktore nadal dzialaja (vanilla i mody) - ranking

Kolejnosc wedle szacowanej skali (bez pomiaru w logu - do potwierdzenia linia `EconomyAudit` CrashScribe i `Ludnosc:`).

| # | Zrodlo / ujscie | Gdzie | Skala | Uwagi |
|---|---|---|---|---|
| 1 | **Dochod rodow z modelu finansow** - podatki miast (BK od ludnosci), cla (`TradeTaxAccumulated` naliczane od obrotu bez potracenia), dochod wsi BK; w druga strone zold partii i garnizonow w nicosc | `ClanVariablesCampaignBehavior.cs:413-414` (`GiveGoldAction(null, leader, CalculateClanGoldChange)`), `bk/.../EconomyPatches.cs:400-470, 484` | najwieksza: ~0.9 mln/dzien dochodu (log 04.10) i podobny zold | rdzen gospodarki; renty (W6) dzialaja OBOK, nie zamiast |
| 2 | **Dosypka/kasowanie kasy miast** do `10000 + 12 x dobrobyt` (25% luki dziennie) | `ItemConsumptionBehavior.cs:73-77`, `DefaultSettlementEconomyModel.cs:75-79` | dziesiatki-setki tys./dzien na swiat | finansuje wiesniakow, nasze WorkshopLaw/DailyTrade, sprzedaz gracza, kucie (K1) |
| 3 | **BK: miasto dostaje zaplate za konsumpcje ludnosci** (towar znika, zloto z niczego) | `bk/.../EconomyPatches.cs:609-657` (`:649-651`) | duza | zrodlo nadwyzki miast, a wiec i renty miast (W6) |
| 4 | **Notable**: dzienny dochod z modelu, dosypka < 4500, kasowanie > 10 500, 10 000 na start | `ClanVariablesCampaignBehavior.cs:477-487`, `NotablePowerManagementBehavior.cs:50-60`, `NotablesCampaignBehavior.cs:47` | srednia | W4 |
| 5 | **Lup z bitew z szablonu** (przedmioty z niczego), DTE FillEmptySlots w bitwie | vanilla loot, `dte` | srednia-duza (przedmioty) | S5, AUDYT-PONOWNY "Czego brakuje" 7 |
| 6 | **Jency**: AI i posrednik placa z niczego (`SellPrisonersAction.cs:70-85`), kurier okupu dosypuje (`RansomOfferCampaignBehavior.cs:174-176`) | | srednia | nasza nadwyzka lorda juz z kiesy rodu; czesc vanilli z niczego |
| 7 | **Najemnicy z karczmy** odrastaja z niczego (ludzie i sprzet); gracz placi w nicosc | `RecruitmentCampaignBehavior.cs:714, 794` | srednia | przy malej liczbie ochotnikow (Levy) zyskaja na znaczeniu |
| 8 | **Wiesniacy**: polowa netto ze sprzedazy znika (BK) | `bk/.../EconomyPatches.cs:1094-1099` | srednia (ujscie) | |
| 9 | **Kryjowki**: +25% wartosci lupu do bandy i kryjowki przy wejsciu | `BanditSpawnCampaignBehavior.cs:149-179` | mala-srednia | W5 - gracz to wyplaca |
| 10 | **Zdobycie miasta / najazd na wies**: zloto z niczego | `SiegeAftermathCampaignBehavior.cs:164-168`, `VillageHostileActionCampaignBehavior.cs:556` | okazjonalnie duza | powinno isc z kasy osady |
| 11 | Warsztaty: dzienny koszt w nicosc, perk RapidDevelopment z niczego | `WorkshopsCampaignBehavior.cs:326, 794-799` | mala | N2 |
| 12 | Zdarzenia, rebelia (+50 000), rozmowy z lordami, zadania | `IncidentsCampaignBehaviour.cs:1234, 1260`, `RebellionsCampaignBehavior.cs:310`, `LordConversationsCampaignBehavior.cs:1152-1157, 3211, 3267` | mala | |
| 13 | ROT: Enlistment (lup -> losowe przedmioty), ROTTroopRecruiter (roznica kosztu), armia Innych | S6 | mala | |

Co ma znaczenie (moja kolejnosc): 1 i 2 razem (bez nich zadna nasza "kasa osady" nie jest zamknieta), potem 3 (renty),
4 (pobor), 5 (sprzet), 7 (wojsko). 8-13 to szlify.

---

## 7. Czego brakuje do zamknietej gospodarki

1. **Jeden rachunek metalu** (K1) dla kucia, naprawy, przetopu, warsztatow AI i ceny rynku.
2. **Naprawa kosztuje surowiec i prace** (K2) - bez tego zuzycie (wpisy 37-39) jest tylko kosztem czasu.
3. **Kasa miast bez dosypki** (sekcja 6, poz. 2) i **konsumpcja BK bez zlota z niczego** (poz. 3) - dopiero wtedy renty, WorkshopLaw,
   DailyTrade i sprzedaz gracza graja o prawdziwe pieniadze.
4. **Podatki gry zastapione renta** (W6 pkt 4), a **zold** platny do osady, w ktorej partia stoi albo kupuje (dzis w nicosc).
5. **Kiesa notabla** z osady, nie z pompy wplywu (W4); zapas sprzetu notabla przechodzacy do rekruta (W2).
6. **Bank** z terminem per transza (K3), egzekucja z dochodu (W1), kapitalem w Braavos i wyjsciem z bankructwa (S9).
7. **Lup z prawdziwej zbrojowni** przegranych (DTE) zamiast z szablonu; wyposazenie AI w bitwie tylko z jego zbrojowni.
8. **Zamowienia lordow** placone przez lorda, z dostawa do niego (K4).
9. **Zapis stanu w save**: WorkshopLaw (`_owed`, `_labor`), premie wojenne ArmsPricing, oczekujacy lup kryjowki.
10. **Najemnicy i garnizon** z puli ludnosci BK / wyrzutkow; zaplata do kasy miasta (S4).
11. **Widocznosc dla gracza**: renta i powinnosci wobec korony bez linii w finansach rodu ani komunikatu (S8, stare N8).
12. **Pomiar**: zanim ruszymy poz. 1-3 z sekcji 6, dzienny bilans zlota swiata w logu (suma kies rodow, notabli, kas osad, band,
    Banku + przeplywy z/do nicosci wedle zrodla) - bez tego nie wiadomo, czy zamkniecie jednego kurka nie wysuszy swiata.

## 8. Proponowana kolejnosc (jedna zmiana = jeden DLL = jeden test)

1. K3 (jedna linia w `IronBank.Lend`).
2. K4 (Orders: placi lord, sztuka do lorda).
3. W8 (DoOrderKit po elementach polki).
4. K2 (TroopSelfMend platny + koszt naprawy kowala).
5. K1 (jeden rachunek metalu) - najpierw porownanie w logu.
6. W5 (kryjowka: wejscie bandy, BuildLoot w DoSearch, Reset).
7. W1 (egzekucja Banku z dochodu).
8. W4 + W2 (notable i zapas sprzetu).
9. Pomiar bilansu swiata (pkt 12), potem sekcja 6 poz. 1-3.
