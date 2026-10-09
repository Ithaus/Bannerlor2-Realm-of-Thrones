<!-- SPECYFIKACJA (08.10.2026, noc), galaz w-toku/172-strzaly na w-toku/171-zbrojenie-zalog (349e393). NIEWGRANE - DO SPRAWDZENIA.
     Paczka 171 rekomenduje wgranie razem z 172 albo po niej (171, pytanie 9). Wersja 2: po krytyce (10 uwag, wszystkie sprawdzone
     w kodzie, dekompilacji i logach - rozdz. 13) i wykonana w kodzie (rozdz. 14). -->

## 172 STRZALY - STRZELARZE I GROTNICY MIASTA: strzaly i belty z drewna i zelaza, reka rzemieslnikow miasta, na polke, z ktorej kupuja armie AI; koniec strzal z niczego i ich znikania w nicosc

**Mod:** Armoury | **Pliki:** NOWY `TownFletchers.cs` (+ `TownFletchersBehavior`), `WorkshopLaw.cs` (Reset, zamkniecie linii "arrows" w `CyclePrefix`,
`Revenue` -> wspolny wzor `RevenueOf`), `WorkshopTrade.cs` (szybkosc linii towarowych bez zamknietej linii strzal), `HistoricalPrices.cs` (`TownUse("arrows")`),
`BkSupplyTemper.cs` (zuzycie strzal BK partii AI = 0), `AiGear.cs` (licznik zakupow amunicji), `SupplyDemand.cs` (licznik "AI bez towaru"),
`OreLedger.cs` (pozycja "strzelarze (172)"), `GoodsLedger.cs` (ramka `FFletch`), `SubModuleMain.cs` (ApplyAll + behavior),
`ArmouryBehavior.cs` (SessionStart), `Settings.cs` + `McmSettings.cs` (gen_mcm), `CHANGELOG.md` (wpis "172 STRZALY", Status: NIEWGRANE - DO SPRAWDZENIA).

### 1. Problem (liczby z logow)
- **Strzaly powstaja z niczego i znikaja w nicosc.** Polki swiata w 40 dobach (18-24-10, bez 171): strzaly 668-1330 snopow, belty 370-739, wahania
  doba do doby +-150-300 snopow (np. 813 -> 1162 -> 813), suma wzrostow 2 202 strzal i 1 160 beltow; w 11-40-05 943 -> 1 553 strzal w jedna dobe.
  Linia "Warsztaty: dzien N" pokazuje przy tym 0 szt. Arrows/Bolts - bo nie liczy tej drogi (rozdz. 2). AUDYT-2026-10-05 W1 (wiersze 170-182) opisal to samo.
- Ujscia: mieszczanie zjadaja amunicje z polek (budzet kategorii "arrows" BK 10/10), zaopatrzenie BK kupuje ja partiom AI za zloto lorda (zloto w nicosc)
  i niszczy (`ConsumeItems`). Ani jedno, ani drugie nie jest w zadnej linii logu.
- Po 171 rekrut t2+ przychodzi "z tym, co ma" (330-760 ludzi dziennie na starcie, ok. 155 po roku - 171 rozdz. 7.2), wiec pan dokupuje kolczany
  z polek. Szacunek [S]: 20-30% ludzi strzela, 2 sloty amunicji na lucznika (`QuartermasterLaw.SlotsOfType`, `AiGear.NeedBuckets` liczy slot = snop)
  -> **ok. 130-450 snopow dziennie na starcie (srodek ok. 290), 60-100 po roku**.
- Decyzja Jeffa 05.10 (STAN-PRAC): "Strzaly i belty maja kosztowac drewno, zelazo i prace warsztatow - tak, ale razem z wieksza liczba rak."
  Zasada Jeffa: "jak znika to zamykamy, ma byc logiczny system ekonomii, ze wszystko z czegos wynika".

### 2. Przyczyna (sprawdzone w kodzie i dekompilacji)
- **Flaga towaru BK.** `BKItemCategories.cs:122`: `DefaultItemCategories.Arrows.InitializeObject(true, 10, 10, ...)` (wolane z `FixesPatches.cs:110`) -
  kategoria "arrows" (strzaly i belty) jest dla gry TOWAREM HANDLOWYM (`ItemCategory.IsTradeGood`). Dlatego `WorkshopLaw.AllOutputsArms` (:60) zwraca
  false, `CyclePrefix` (:219) oddaje cykl grze, a linia arrows nigdy nie wchodzi do rankingu WorkshopLaw (w `Logs/<sesja>/warsztaty.log` ani razu "/arrows").
- **Droga gry = z niczego.** BK `workshops.xml`: ukryty "artisans" ma linie arrows x4 przy `conversion_speed 0.8` BEZ wsadu (:218-222), warsztat notabla
  "fletcher" x1 przy 1.5 (:519-524). Prog cyklu ukrytego warsztatu = koszt wsadu = 0 (`CanNotableWorkshopProduceThisCycle` :776-779), BK `ProduceOutputPrefix`
  mnozy sztuki artisans przez `1 + rzemieslnicy/45/wartosc`. Sam XML to ok. 310 snopow dziennie na swiat PRZED mnoznikiem BK. Nic tego nie zatrzymuje:
  `FreeRawLine` obejmuje 6 surowcow (:652), `ArtisanInputs` pomija linie bez wsadu (:111). `GoodsLedger` liczy tylko `ItemObject.IsTradeGood`
  (`ItemType == Goods`) - amunicji nie widzi.
- **Ujscia w nicosc.** (1) Mieszczanie: BK `EconomyPatches.MakeConsumption` (:613-649) zjada kazda sztuke polki wedlug budzetu kategorii (`CalculateBudget`);
  `HistoricalPrices.TownUse` zwraca dla "arrows" 1 (tnie tylko surowce). (2) Zaopatrzenie BK: `PartySupplies.BuyItems(ArrowsNeed x dni, ammoCategories)`
  (:326) kupuje z polki osady (zloto lorda w nicosc, osada nic nie dostaje), `ConsumeItems(ArrowsNeed x 2)` (:311) niszczy; `BKPartyNeedsModel.CalculateArrowsNeed`.
- **Krok 0 (pomiar, w kodzie):** sonda na `TickOneProductionCycleForNotableWorkshop` (prefiks z `Priority.First` + postfiks, `__state`): w cyklu linii arrows
  zmiana liczby snopow Arrows/Bolts na polce miasta - **ile snopow gra robi dzis z niczego (po mnozniku BK)**. Dziala tez przy wylaczonym 172, wiec jedna
  doba autotestu z `TownFletchersEnabled = false` daje dzisiejszy doplyw (linia "Strzelarze (172): NIECZYNNE ... z niczego"). Przy czynnym 172 sonda ma
  pokazac 0, a licznik zamkniec - liczbe cykli i snopow z receptury (przed mnoznikiem BK).

### 3. Kto robi: strzelarze i grotnicy miasta (osobne rzemioslo, nie linia warsztatu)
- Historia: luczarze (bowyers), strzelarze (fletchers), cieciwiarze i grotnicy (arrowsmiths) to w Anglii XIV w. osobne zawody (HISTORIA-STRZALY,
  April Munday "Medieval Bowyers, Fletchers, Stringers and Arrowsmiths"); belty robil osobny warsztat (Jan de Malemort, XIII w.).
- W grze: jeden "cech" amunicji w kazdym z 97 miast, rece wedle dobrobytu jak reszta rzemiosla:
  `rece = TownFletcherHandsPerArmsHand (0.3) x WorkshopLaw.TownHands(miasto)` (dobrobyt / 170, 6-60) - swiat: 0.3 x ok. 2 830 = **ok. 850 roboczodni dziennie**,
  przy pelnej pracy ok. 280 snopow (srednio ok. 3 roboczodnia na snop).
- Dlaczego 0.3 [S]: (a) stan ustalony z historii: korona angielska 1341-1359 kupila 1 232 400 strzal (Wadge, *Arrowstorm*), ok. 65 tys. rocznie na armie
  polowa 10-15 tys. -> ok. 0.001 roboczodnia na zolnierza dziennie; x 166 tys. ludzi partii i zalog = ok. 180; kupno korony to nie cale zuzycie (lucznicy
  z kontraktu przynosili wlasne, rok 1359 = 850 tys.), x2-3 -> 360-540 roboczodni = wskaznik 0.13-0.19; (b) start po 171 to skok: przezbrojenie swiata,
  srodek popytu 290 snopow x 3 roboczodnia = ok. 870 roboczodni = 0.3. Bierzemy 0.3 - rece bez roboty nic nie kosztuja i nie odkladaja sie (jak 148),
  bramka zysku zatrzymuje je przy pelnej polce; po roku czesc stoi. Wersja 1 brala 0.2 wobec zalozonego "0 produkcji" - bledne zalozenie (rozdz. 2).
- Linie "arrows" WSZYSTKICH warsztatow notabli (ukryty artisans i "fletcher") przy czynnym 172 zamkniete w `WorkshopLaw.CyclePrefix` PRZED `FreeRawLine`
  i `AllOutputsArms` (`__result = false; return false` + licznik cykli i snopow z receptury) - jedna droga amunicji. Warsztat "fletcher" GRACZA
  (`TickOneProductionCycleForPlayerWorkshop`) zostaje wedlug gry (decyzja Jeffa: funkcje gracza bez zmian) - jedyna pozostala droga z niczego, mala
  (gracz ma najwyzej kilka warsztatow), do decyzji (rozdz. 12). `WorkshopTrade`: szybkosc linii towarowych warsztatu notabla bez zamknietej linii strzal
  (place cyklu pozostalych linii fletchera nie dziela sie z linia, ktora nie dziala); `Expected` tez bez niej.
- Start nowej gry (`RunTownShopsAtGameStart`, przed `OnSessionLaunched`): 172 jeszcze nieczynne (kandydaci budowani w SessionStart) - gra wypelnia
  rynki jak dotad; to dorobek startowy swiata, jak `StartStock` i ColdStart.

### 4. Z czego (zero z niczego)
- **Receptura = ta, z ktorej liczona jest wartosc snopa:** `WorkshopLaw.Needs(it, out days)` -> [ruda, drewno, skora, len] w jednostkach rynku
  (`ArmsPricing.CostOf`: metal 15% masy snopa x1.4, drewno 85% x1.5 na drzewca; ruda = metal / 1.5 kg surowki z ladunku 10 kg x1.25 na stopien gatunku;
  wegiel: dymarka `WorkshopWoodPerOre` 5 kg drewna na kg rudy + kuznia `WorkshopForgeWoodPerMetalKg` 12.5 kg na kg metalu).
  **Rachunek (poprawiony po krytyce):** snop 30 x 0.05 kg = 1.5 kg: metal 0.315 kg (groty 10 g - jak bodkiny Mary Rose); Iron2: ruda 2.6 kg = **0.026 ladunku**,
  drewno 1.9 (drzewca) + 13.1 (dymarka) + 3.9 (kuznia) = 19 kg = **0.19 ladunku**; t5 (Iron5): ruda 0.051, drewno 0.31. 42 kg drewna na kg zelaza -
  w granicach historii (dymarka 4-8 kg wegla na kg lupy, wegiel 1:5-7 z drewna). Swiat przy 250 snopach: ruda 7-13, **drewno 48-78 ladunkow dziennie**
  wobec nadwyzki drewna +109/d i rudy +33/d (audyt 03). Wiaze drewno, nie ruda.
- Ulamki jako dlug miasta (4 liczby jak `WorkshopLaw._owed`): sztuka zaczeta, gdy `floor(dlug + potrzeba)` sztuk lezy na polce; dlug zawsze < 1 jednostki.
  Miasto bez drewna zrobi raz najwyzej ok. 5 snopow (1 ladunek dlugu), bez rudy ok. 38 - potem "brak: drewno / ruda". Ta sama regula co warsztaty
  zbrojne; dlug jest splacany pierwszym ladunkiem, ktory przyjdzie (najwyzej 1 ladunek kazdego surowca na miasto, raz).
- **Pierze, klej, nici - JAWNE UPROSZCZENIE do akceptacji Jeffa (nie "juz z czegos wynika"):** w swiecie gry nie ma towaru "pierze" (ges 4 d i kura 1 d
  istnieja jako zwierzeta, na polkach 0 szt. przez 40 dob). Pierze, klej i nici to ponizej 1% masy snopa; ich zebranie i przyciecie siedzi
  w roboczodniach strzelarza, bez towaru. Historia: pierze z gesi domowych - 1417/18 hrabstwa mialy dostarczyc 1 190 000 pior, 6 z gesi (HISTORIA-STRZALY
  rozdz. 3), ges skubana zyje; klej z mizdry, ktora 148 i tak odrzuca. Gdyby Jeff chcial pior jako towaru - osobna paczka (drob 144 -> pierze).

### 5. Ile na dobe i jak wybiera (bramka zysku jak u warsztatow zbrojnych)
- **Kolejnosc dnia (poprawione po krytyce): PO warsztatach miasta.** Postfiks na `WorkshopsCampaignBehavior.DailyTickTown(Town)` - strzelarze biora
  rude i drewno, ktore zostaly po kowalach, platnerzach i liniach towarowych tego miasta (nie odbieraja rudy zbrojom i broni, ktorych 171 tez
  potrzebuje). Gdy latka sie nie zalozy - sluchacz `DailyTickTownEvent` (przed warsztatami, jak 148) i linia startowa mowi to wprost.
  Miasto w buncie - jak warsztaty: nic.
- Petla krokow po jednym snopie jak `TownCrafts.Work`: przed kazdym krokiem dla kazdego kandydata miasta `przychod = WorkshopLaw.RevenueOf(it, Factor koszyka,
  ArmsPricing.Multiplier)` (wartosc x `SupplyDemand.Factor` typ x tier x mnoznik wyceny x `WorkshopSellShare` - ten sam wzor co `WorkshopLaw.Revenue`),
  `koszt = suma need[m] x WorkshopLaw.MatPrice(town, mat, m) + days x WorkshopLaw.DayWage(it, town)`; robi snop o najwyzszym `(przychod - koszt) / days`,
  gdy `przychod >= koszt x (1 + WorkshopMinProfitPercent/100)` i surowiec jest na polce. Snop na polce obniza `Factor` swojego koszyka, wiec produkcja
  staje przy zapchanej polce i rusza, gdy AI wykupi.
- **Koszt (krytyka 9):** `Factor` liczony raz na koszyk w pracy miasta; po snopie przeliczany tylko koszyk tego snopa i koszyk o tier nizej (substytucja
  patrzy na tier wyzej). Mnoznik wyceny - raz na przedmiot na miasto; ceny rudy i drewna - raz na krok.
- Popyt AI wchodzi przez ceny: `AiGear` przy braku koszyka na polce wola `SupplyDemand.NoteUnmetOnce` -> zamowienia w `Demand` -> wyzszy `Factor`.
- Praca: `days` = `HistoricalPrices.HistDays` (snop t1 2.4 ... t6 4.0 roboczodnia x jakosc) - bez zmiany (z niej jest wartosc snopa). Rece: dlug rak
  na nastepna dobe jak 148 (`MinHands` 0.01). Malemort: ok. 30 beltow na roboczodzien samej roboty strzelarza bez grotow; nasze 8-12 strzal na roboczodzien
  obejmuje tez kucie grotow i struganie drzewc.
- **Kandydaci (poprawione po krytyce 5 i 10).** Swiat (raz na sesje, `SessionStart`): ItemType Arrows/Bolts, `!NotMerchandise`, `!IsCraftedByPlayer`,
  `Value > 0`, bez `ArmsPricing.IsUnique`, `LegendaryLaw.IsLegend`, `WorkshopLaw.Forbidden` i bez id z `TownFletcherSkipIds`
  ("tournament,blunt,ballista,burning,fire_,stealth,calradian_fire"). Belty "repeater_" (RBM) zostaja - to zwykle belty typu Bolts, koszyk typ x tier
  je przyjmuje. Miasto (raz na sesje, przy pierwszej pracy): **na kazdy koszyk typ x tier osobno** - wyroby kultury miasta albo neutralne, a gdy w tym
  koszyku takich brak - wyroby obce tego koszyka (zamowienie "Bolts t6" w miescie bez wlasnego beltu t6 ma kogo zatrudnic; kultury beltow po nadpisaniu
  przez RBM nie trzeba zgadywac). Najwyzej 4 wyroby na koszyk, rozne nazwy (podwojne warianty RBM "GRE_*" nie zajmuja miejsc).

### 6. Pieniadz i ujscia (spojnie z TownCrafts 148; ujscia zamkniete)
- Bez zlota w chwili roboty: ruda i drewno z polki miasta na snop na polce tego samego miasta - kasa miasta to kupcy i rzemieslnicy razem (148).
  Place sa kosztem w bramce zysku (dniowka x poziom plac miasta, 149), nie przelewem. Pieniadz przychodzi, gdy pan AI kupuje snop:
  `AiGear` - pan (sakiewka ludzi / kiesa) -> `Town.ChangeGold` (bez zmian).
- **Mieszczanie (krytyka 2):** `TownUse("arrows") = 0` przy czynnym 172 - budzet mieszczan na kategorie strzal (BK `CalculateBudget` przez
  `HistoricalPrices.BudgetPostfix`, przy `TownHouseholdUse`) = 0. Uzasadnienie: mieszczanin nie zuzywa wojennych grotow; mysliwskie strzaly sa poza skala
  (kolczan na lata). Wylaczone 172 - jak dotad.
- **Zaopatrzenie BK (krytyka 2):** postfiks na `BKPartyNeedsModel.CalculateArrowsNeed` (po czapce, `Priority.Last`): 0 dla partii AI przy czynnym 172
  i zakupach AI (`AiGear.On`) + zerowanie zapisanej `ArrowsNeed` (precedens: tekstylia 150). Amunicje partii AI liczy juz Armoury: zakupy `AiGear`
  wedlug wzorcow, zuzycie w bitwie i odzysk (QuartermasterLaw, AmmoRecovery). Partia gracza - bez zmian (BK jak dotad).
- Po tych trzech zamknieciach strzala powstaje tylko z rudy i drewna (albo w warsztacie gracza), znika tylko w bitwie (zuzycie wedlug Armoury).

### 7. Polka -> AiGear -> QuartermasterLaw (sprawdzone w kodzie)
- `AiGear.Order` i `QuartermasterLaw.KitTypes` zawieraja Arrows i Bolts; `SupplyDemand.Equipmentish` - tak; koszyk = typ x tier (`TierOf` = Tier+1),
  pan kupuje tier t albo t-1 (`BuyLoop`) z `market.ItemRoster` - ta sama polka, na ktora strzelarze kladli snop (`town.Owner.ItemRoster`).
- `SessionStart` sprawdza pokrycie GLOBALNE (koszyk ze wzorcow oddzialow nie-bohaterow, `CharacterObject.All`, sloty 0-3 `BattleEquipments`, musi miec
  kandydata tieru t albo t-1). Pokrycie na miasto zapewnia regula "obcy wyrob, gdy brak swojego" (rozdz. 5); linia dnia podaje, w ilu miastach i koszykach
  obcy wyrob byl potrzebny.
- Ryzyko: amunicja jest 9. w kolejnosci zakupow (`AiGear.Order`) - licznik w linii pokaze; zmiana kolejnosci nie wchodzi do 172.

### 8. Linie logu
Linia dnia (raz na dobe, przy pierwszym miescie nastepnej doby - jak 148; przykladowe liczby):
```
Strzelarze (172): dzien N - 97 miast; strzaly: zrobiono 120 snopow [t1 40, t2 50, t3 30], kupione przez AI 95, AI bez towaru 12 razy;
belty: zrobiono 25 [t2 15, t4 10], kupione przez AI 20, AI bez towaru 3 razy; koniec pracy w miastach (bez zysku / brak surowca / rece) 30/12/55
[brak: ruda 4, drewno 10]; rece: zajete 610 z 850 roboczodni (72%), bez roboty 240, dlug rak 20; zuzyto: ruda 4 (wedle proporcji 4.2), drewno 29 (28.6);
obcy wyrob w 6 miastach (koszyki 7); na polkach: strzaly 1450 (miast bez strzal 5), belty 700 (miast bez beltow 12); mediana indeksu ceny strzal 1.10,
beltow 1.25; zamkniete z niczego: linie strzal warsztatow 98 cykli (392 snopy z receptury, przed mnoznikiem BK), sonda: warsztaty gry zrobily 0 snopow
w 0 cyklach; mieszczanie: budzet strzal 0 (310 wywolan); BK: zuzycie strzal partii AI 0 (zerowan zapisu 3); potkniecia 0 (od startu 0).
```
Przy wylaczonym 172 (krok 0): `Strzelarze (172): NIECZYNNE (powod) - dzien N: warsztaty gry zrobily z niczego X snopow w Y cyklach linii arrows (sonda);
na polkach: strzaly A, belty B.` "Kupione przez AI" = licznik w `AiGear.BuyLoop` (polki miast i zamkow), "AI bez towaru" = wpisy `NoteUnmetOnce`
dla Arrows/Bolts. Linia startowa: kandydaci (strzaly / belty, koszyki), rece swiata, koszyki wzorcow bez wyrobu, droga ticku (po warsztatach albo zapasowa),
zamkniecia (linie warsztatow, mieszczanie, BK), "NIECZYNNE (powod)" gdy wylaczone.
Ksiega rudy i drewna (`Ruda:` / `Drewno:`): nowa pozycja zuzycia "strzelarze (172) N" (w "bez wyjasnienia" jak budowy).

### 9. Wylacznik, ustawienia, zapis
- `TownFletchersEnabled = true` (MCM, domyslnie wlaczony), `TownFletcherHandsPerArmsHand = 0.3f` (0 = wylaczone), `TownFletcherSkipIds` (tekst, poza MCM).
  Opisy po angielsku, `python tools/gen_mcm.py`. `Active` = wlaczony && rece > 0 && `HistoricalPrices.Applied` && kandydaci > 0.
  Wylaczone: zadnych zamkniec (linie arrows warsztatow, mieszczanie, BK jak dzis), sonda i linia NIECZYNNE dzialaja.
- Stan miasta: dlug rak + 4 dlugi materialu -> `SaveText.Sync(dataStore, "arm_fletchers", ref data)` w `TownFletchersBehavior.SyncData`,
  format "1|miasto~rece~ruda:drewno:skora:len|..." (kultura niezmienna). `TownFletchers.Reset()` w `WorkshopLaw.Reset()` obok `TownCrafts.Reset()`
  (wolane z konstruktora `ArmouryBehavior`, przed SyncData). Behavior dopisany w `SubModuleMain` po `TownCraftsBehavior`.
- Ksiega towarow: ramka `GoodsLedger.FFletch = 21` ("strzelarze (172)", `Kinds = 22`) wokol pracy miasta - ruda i drewno jako ujscie "strzelarze (172)".
  Ksiega rudy: `OreLedger.NoteFletch` (NIE `NoteWorkshop` - ta wola `GoodsLedger.NoteArms`, ktora wymaga ramki cyklu warsztatu). Kontrola "Towary (bilans)"
  porownuje wsie / las / warsztaty zbrojne / linie / budowy / zapas - strzelarze nie wchodza do zadnej z tych grup po obu stronach, wiec ZGODNA zostaje.
  Bez `OnItemConsumed` dla rudy i drewna (ksiega rudy policzylaby je drugi raz jako "linie towarowe"); `OnItemProduced` dla snopa jak warsztaty zbrojne.
- Wszystko w try/catch (licznik potkniec, wyjatek jednego miasta nie zatrzymuje reszty).

### 10. Metody (TownFletchers.cs)
`Enabled`, `Active`, `Reset()`, `ApplyAll(Harmony)` (postfiks doby miasta, sonda), `SessionStart()` (kandydaci, koszyki wzorcow, linia startowa),
`TickPostfix(Town)` / `OnDailyTickTown(Town)` (Flush przy nowej dobie, ramka GoodsLedger, `Work`), `Work(Town)` (rozdz. 5), `CandidatesOf(Town)`,
`ClosesLine(Production, Workshop)` + `NoteClosed(Production)` (dla WorkshopLaw i WorkshopTrade), `ProbePrefix/ProbePostfix` (krok 0),
`NoteBought`, `NoteUnmet`, `HouseUse()` (TownUse), `ZeroBkArrows(object)` (BkSupplyTemper), `Flush()`, `Export()`/`Import(string)`.

### 11. Test (autotest; zgoda Jeffa 07.10 na autotest)
**Krok 0 (1 doba, `TownFletchersEnabled = false`, stos z 171):** linia "Strzelarze (172): NIECZYNNE ... z niczego X snopow w Y cyklach" - dzisiejszy
doplyw gry. Jesli X (doba) > 2x pojemnosci strzelarzy (ok. 280) - suwak rak do decyzji po tescie (nie zmienia zasady).
**Glowny (40 dob nowej kampanii, stos z 171, 172 wlaczone):**

| Co | Gdzie w logu | Brama |
|---|---|---|
| Zamkniecia | linia 172 "zamkniete z niczego", "sonda", "mieszczanie", "BK" | linie warsztatow > 0 cykli dziennie; sonda 0 snopow kazdego dnia; budzet mieszczan - wywolania > 0; |
| Amunicja na polkach | "Rynek broni: na polkach" Arrows / Bolts, linia 172 "na polkach" | doby 1-10: zapis liczb (oczekiwany spadek - rozdz. 12); doba 40 >= 30% doby 1; "miast bez strzal" w dobie 40 <= 30% |
| Zakupy AI amunicji | linia 172 "kupione przez AI" | > 0 w kazdym 5-dobowym oknie od doby 5; suma 40 dob > 0 dla strzal i beltow |
| Pokrycie zbrojowni (171) | "Pokrycie zbrojowni AI (171)" doba 40 | strzaly i belty >= 70% (brama 171) |
| Produkcja | linia 172 "zrobiono" | > 0 od doby 2; po dobie 20 rece nie zawsze 100% albo "brak surowca" wyjasnia (bramka dziala) |
| Surowce na starcie | linia 172 "koniec pracy ... brak: ruda / drewno" doby 1-10 | zapis udzialu; "brak: drewno" oczekiwany (rozdz. 12), nie blad |
| Zero z niczego | linia 172 "zuzyto ... (wedle proporcji ...)", "Ruda:"/"Drewno:" "strzelarze (172)", "Towary (bilans)" | zuzyto - wedle proporcji < 1 jednostki na miasto; ruda i drewno ZGODNA |
| Ceny | linia 172 mediana indeksu | 0.7-2.5 (nie przy podlodze 0.25 ani suficie 4) |
| Koszyki | linia startowa 172 | brak "koszyki wzorcow bez wyrobu" albo lista do decyzji |
| Regresje | wylaczony `TownFletchersEnabled` | linia "NIECZYNNE", sonda > 0, zapis wczytuje sie bez bledu (stary zapis bez "arm_fletchers" - dlugi 0) |

### 12. Ryzyko / otwarte (uczciwy bilans startu - poprawione po krytyce 3)
- **Bilans dob 1-30 [S]:** zapas dnia 0: polki gry ok. 470 strzal + 160 beltow + ColdStart 60 dni ok. 2 450 + 1 200 (14 dni dalo 571 + 282) = ok. 4 300 snopow.
  Popyt AI 130-450/d (srodek 290) -> 3 900-13 500 w 30 dobach. Produkcja: doby 1-10 wiaze drewno (61 z 97 miast bez drewna w dobie 5, 81 bez rudy -
  ruda na dlugu starczy na ok. 38 snopow), ok. 40 miast x 3 snopy = ok. 100-120/d; doby 30-40 ok. 85-90 miast z drewnem - ok. 250/d; razem ok. 5 000.
  Mieszczanie i BK juz nie zjadaja. Wynik: przy niskim popycie polki rosna, przy srodku schodza blisko zera ok. dob 25-30, przy wysokim ok. doby 12.
  Strzelarze tego nie zmienia - wiaze drewno (sprawa drwali 126 i karawan 103, nie 172) i tempo przezbrojenia 171.
- Drewno: strzelarze ok. 50-80 ladunkow dziennie przy pelnej pracy wobec nadwyzki +109/d; jako ostatni w dobie miasta (po warsztatach) biora resztke -
  przy niedoborze stoja oni, nie kowale.
- Kolejnosc zakupow AI (amunicja 9.) i budzet wizyty - patrz 7.
- `HistAmmoLaborMultiplier` 8 (praca na snop) zostaje - zmiana ruszylaby ceny snopow wobec historii (10-18 d); Malemort sugeruje szybsza robote - po tescie.
- Warsztat "fletcher" gracza robi strzaly z niczego jak dotad (decyzja Jeffa: funkcje gracza bez zmian) - pytanie do Jeffa: zamknac i jego?
- Pierze, klej, nici - jawne uproszczenie (rozdz. 4), do akceptacji Jeffa.
- Receptury amunicji gracza (kuznia, `Recipes`: zelazo tieru za cala mase kolczana + 1 drewno na serie) i miasta (15% metalu, 85% drewna + wegiel) sa
  niespojne - ta sama sztuka ma dwa koszty materialu. Poza 172; pozycja do decyzji (spojnosc receptur amunicji gracza i miasta).
- Kampania Jeffa (2 900 strzal na polkach): 172 zaczyna dzialac od wczytania, bez dosypki; dlugi od zera; zapisana `ArrowsNeed` partii AI zerowana przy
  pierwszym przeliczeniu BK.

### 13. Krytyka i odpowiedzi (10 uwag; kazda sprawdzona w kodzie, dekompilacji albo logu)
| # | Waga | Uwaga | Sprawdzenie | Odpowiedz |
|---|---|---|---|---|
| 1 | krytyczne | Przyczyna zera bledna: BK robi z "arrows" towar handlowy, droga gry robi strzaly z niczego | `BKItemCategories.cs:122` (`InitializeObject(true, 10, 10, ...)`), `ItemCategory.IsTradeGood` w `AllOutputsArms` i `RunTownWorkshop`; BK `workshops.xml:218-222`, `:519-524`; `CanNotableWorkshopProduceThisCycle` (ukryty: prog = wsad = 0); `ProduceOutputPrefix` (`1 + craftsmen/45/value`); log 18-24-10: polki strzal 668-1330 przy 0 w "Warsztaty" | Przyjete. Rozdz. 1-2 przepisane; krok 0 = sonda (zmiana polki w cyklu linii arrows), dziala przy wylaczonym 172; zamkniecie przed `FreeRawLine`/`AllOutputsArms`; licznik zamkniec "cykle (snopy z receptury)"; wskaznik rak 0.2 -> 0.3 z popytu startowego, nie z zera |
| 2 | wazne | Ujscia: mieszczanie i zaopatrzenie BK | BK `MakeConsumption` (EconomyPatches :613-649, budzet `CalculateBudget`), `TownUse` bez "arrows"; `PartySupplies.BuyItems` (:326, zloto lorda w nicosc), `ConsumeItems` (:311) | Przyjete. `TownUse("arrows") = 0` i `CalculateArrowsNeed = 0` dla partii AI przy czynnym 172 (rozdz. 6), liczniki w linii |
| 3 | wazne | Start: brak surowcow, strzelarze przed kowalami, "pokrywa start" nieprawda | log 18-24-10 "Ruda"/"Drewno": doba 5 bez rudy 81/97, bez drewna 61/97; doba 40 - 34 i 8-11 | Przyjete. Strzelarze PO warsztatach (postfiks `DailyTickTown`); rozdz. 12 - uczciwy bilans dob 1-30; brama - zapis udzialu "brak surowca" w dobach 1-10 |
| 4 | wazne | Drewno zanizone 4-6x | `WorkshopLaw.Needs` :163-166 (dymarka 5 kg/kg rudy + kuznia 12.5 kg/kg metalu) | Przyjete. Rozdz. 4: 0.19-0.31 ladunku drewna na snop; pozycja "strzelarze (172)" w "Drewno:"; "brak: drewno" oczekiwany |
| 5 | wazne | Pokrycie globalne, kupno lokalne | regula `WorkshopLaw.Candidates` (kultura albo neutralne, inaczej wszystko - na cala linie) | Przyjete inaczej niz w propozycji: regula NA KOSZYK - obcy wyrob koszyka, gdy w miescie brak swojego (zawsze, nie tylko przy zamowieniach - zamowienie powstaje dopiero po pustej polce, a bez wyrobu nie byloby czym jej wypelnic); linia dnia: w ilu miastach i koszykach |
| 6 | drobne | Pierze z niczego | - | Przyjete: jawne uproszczenie do akceptacji Jeffa (rozdz. 4, CHANGELOG) |
| 7 | drobne | Receptury gracza i miasta niespojne | `Recipes.cs` (FletchForge) wobec `ArmsPricing.CostOf` | Przyjete: poza 172, pozycja do decyzji (rozdz. 12). Kod 172 na `WorkshopLaw.Needs/RevenueOf/MatPrice/DayWage` i szkielecie 148 |
| 8 | drobne | Warsztat gracza; TradeSpeed liczy zamknieta linie | `WorkshopTrade.TradeSpeed`, `CycleLabour`, `Expected` | Przyjete: gracz bez zmian (zapisane wprost); `TradeSpeed` i `Expected` dla warsztatow notabli bez zamknietej linii |
| 9 | drobne | Koszt Revenue (Factor przechodzi cala polke) | `SupplyDemand.Factor` :258 (Stock + Substitution) | Przyjete: Factor raz na koszyk, po snopie tylko koszyk t i t-1 |
| 10 | drobne | Typy trafione; duplikaty GRE_, repeater_ | `AiGear.Order`, `KitTypes`, `Equipmentish` | Przyjete: najwyzej 4 wyroby na koszyk o roznych nazwach; repeater_ zostaje (zwykle Bolts) |

### 14. Wykonanie (08.10 noc)
Kod wedlug rozdz. 3-10. Odchylenia od wersji 1: (1) zamkniecie w `CyclePrefix` przed `FreeRawLine` (linia nigdy nie dochodzila do `AllOutputsArms`);
warunek w `Candidates` i uwaga o `LineShare` usuniete (linia arrows tam nie trafia). (2) Sluchacz doby miasta zastapiony postfiksem na
`WorkshopsCampaignBehavior.DailyTickTown` (po warsztatach), sluchacz tylko zapasowo. (3) Rece 0.3 zamiast 0.2. (4) Trzy zamkniecia ujsc (rozdz. 6)
i sonda (rozdz. 2). (5) `WorkshopLaw.Revenue` zostaje prywatna, wzor wydzielony do `internal RevenueOf` (jeden wzor dla obu).
