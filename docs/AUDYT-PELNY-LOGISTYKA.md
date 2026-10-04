# Audyt pelny: logistyka, lupy, zuzycie, jedzenie, amunicja, ruch (2026-10-04)

Tylko odczyt - zaden plik kodu ani ustawien nie zostal zmieniony. NIC z dzisiejszych wpisow (11-39)
nie bieglo jeszcze w grze, wiec wszystko ponizej to wnioski z kodu i dekompilacji, nie z logu.
Tam, gdzie brak dowodu - napisane wprost.

Sciezki: `Armoury/src/...`, `CrashScribe/src/...` - nasz kod. `dte/...`, `rbl/...`, `tw/...` - dekompilacje
w scratchpadzie (DynamicTroopEquipmentReupload, RealisticBannerlord, TaleWorlds.CampaignSystem).
`ItemRoster` i `EquipmentElement` zdekompilowane dzis z gry (ilspycmd, TaleWorlds.CampaignSystem.dll / TaleWorlds.Core.dll).

Ustawienia odczytane z `Configs/ModSettings/Global/Armoury/Armoury.json`: **WorldPacePercent 75** (nie 50,
jak mowi domyslna wartosc i komentarz), SiegePacePercent 50, TroopWearPercent 12, TroopSelfMendPercentPerDay 10,
PlayerLootSharePercent 30, LootMinConditionPercent 3, CaptiveSpoilsEnabled true (z konmi), SimBattleFullDrop true,
WinterPartyFoodBonusPercent 50, WinterVillageOutputCutPercent 50, NorthGradientPercent 25.
`FoodConsumptionCutPercent`, `KeepWearThroughBattle`, `FastForwardMultiplier`, `SingleWinterSource` nie ma
w JSON - dzialaja domyslne (40, true, 8, true). RBL: WinterSpeedPenalty -0.2, WinterFoodPenalty 0,
SummerFoodBonus -0.1, AutumnSpeedPenalty -0.1. DTE: DropRate 1.0, ScrapCapPerCategory 600.

---

## Jak dziala dzis przeplyw sprzetu wojska gracza w bitwie osobistej (dla kontekstu)

Kolejnosc w DTE `DynamicTroopMissionLogic.FinalizeMission` (dte DynamicTroopMissionLogic.cs:548-599):
1. dla kazdej partii `HandlePartyItems` (:703-725): zwyciezca dostaje NAJPIERW `ItemsToRecover` (rzeczy
   wlasnych poleglych/rannych), POTEM `LootedItems` (lup z cial wroga) - oba przez
   `ReturnItemsToDestination` -> `ArmyArmory.AddItemToArmory(item, n)` (:727-750);
   nasz postfix `BattlefieldLaw.AfterDtePartyItems` biegnie zaraz po tym (dzialka 30% na ekran);
2. DOPIERO POTEM `ReturnEquipmentFromAgents` (:765-790) - sprzet ZYWYCH zolnierzy wraca przez
   `AddItemToArmory(item)` po jednej sztuce.
3. Pozniej, przy `MapEventEnded`, DTE `EveryoneCampaignBehavior.OnMapEventEnded` (dte :529-560) oddaje
   zwyciezcom CALE zbrojownie pokonanych partii AI (`DistributeLootRandomly`, :751-810) - u gracza znowu
   przez `AddItemToArmory`.

Wydanie: `SpawnAgentPatch.Postfix` (dte Patches/SpawnAgentPatch.cs:125-131) -> `ArmyArmory.AssignEquipment`
(dte ArmyArmory.cs:191-268) zdejmuje PIERWSZY element rostera o danym StringId (dowolny stan).

`EquipmentElement.IsEqualTo` = ten sam `Item` i ten sam `ItemModifier` (referencje), `GetHashCode` z obu -
klucz slownika w WearKeep jest poprawny. `ItemRoster.FindIndexOfItem(item)` zwraca PIERWSZY element z tym
przedmiotem BEZ WZGLEDU na modyfikator, a `AddToCounts(ItemObject, n)` dziala na elemencie BEZ modyfikatora;
przy braku takiego elementu i n < 0 robi `Debug.FailedAssert` i NIC nie zdejmuje (ItemRoster.cs:117-127, 185-220).

---

## WYSOKIE

### W1. Dzialka gracza z lupu moze sie DUBLOWAC (towar z niczego) - WearKeep to ujawnia
**Gdzie:** `Armoury/src/BattlefieldLaw.cs:413-418` (`AfterDtePartyItems`) + `Armoury/src/WearKeep.cs:78-101` (`AddPrefix`).

```
int idx = armory.FindIndexOfItem(item);          // pierwszy element z TYM przedmiotem, dowolny stan
int avail = idx >= 0 ? armory.GetElementNumber(idx) : 0;
int take = Math.Min(share, avail);
armory.AddToCounts(item, -take);                 // zdejmuje z elementu BEZ modyfikatora
ShareQueue.AddToCounts(item, take);              // a do kolejki idzie "take" zawsze
```
Jesli w magazynie nie ma elementu bez modyfikatora (albo ma mniej sztuk niz `take`), zdjecie sie nie udaje
(FailedAssert, -1) albo obcina sie do zera, a kolejka lupow i tak dostaje `take` sztuk - **sztuki z niczego**.

Dotad (przed WearKeep) lup zawsze wchodzil jako "nowy", wiec element bez modyfikatora mial co najmniej
tyle sztuk, ile lupu - problem byl uspiony. Od wpisu 39 lup idzie przez `AddPrefix` i **zabiera zapamietane
stany zuzytych sztuk** (patrz W2), bo lup jest zwracany PRZED zywymi zolnierzami, a lista stanow jest zdejmowana
od konca (LIFO), czyli od sztuk wydanych najpozniej - a te sa zwykle zuzyte (DTE wydaje najpierw pierwszy element
rostera). Przy kazdym typie, ktory nosza obie strony (w ROT bardzo czeste), element "nowy" bedzie za maly
i dzialka zdubluje towar.

**Poprawka (obie czesci):**
1. `AfterDtePartyItems`: zdejmowac po wszystkich elementach tego przedmiotu, z ich stanem:
   petla `for i` po `armory`, `if (el.EquipmentElement.Item == item)`, `t = min(left, el.Amount)`,
   `armory.AddToCounts(el.EquipmentElement, -t); ShareQueue.AddToCounts(el.EquipmentElement, t)`
   (iterowac od konca, bo element znika przy zerze). Kolejka wtedy niesie tez stan - `FlushShareToBaggage`
   i tak to obsluguje (`el.EquipmentElement.ItemModifier ?? PickWornModifier`).
2. WearKeep nie moze nadawac stanow lupowi (W2).

### W2. WearKeep nadaje zapamietane stany LUPOWI, a nie sztukom, ktore wrocily
**Gdzie:** `Armoury/src/WearKeep.cs:78-101`; kolejnosc DTE opisana wyzej (dte DynamicTroopMissionLogic.cs:550-598).

`AddPrefix` przechwytuje KAZDE `AddItemToArmory` dopoki `_out` nie jest wyczyszczone: zwrot poleglych,
lup z cial (przed zywymi!), zbrojownie pokonanych przy `MapEventEnded` (dte EveryoneCampaignBehavior.cs:772, 810),
lup symulacji (:718), komplet rekruta (dte RecruitmentPatch.cs:73), zakup oszczepow/strzal z przyciskiem DTE
(ArmyArmory.cs:435-459). Poniewaz sztuki tego samego StringId sa w magazynie zamienne, LACZNA liczba zuzytych
sztuk sie zgadza, ALE:
- stan sztuki ZNISZCZONEJ (rozbita ciosem, przepadla) przechodzi na lup zamiast zniknac razem z nia,
  gdy lupu tego typu jest dosc - zuzycie nie ginie nawet wtedy, gdy sztuka zginela;
- psuje rachunek dzialki (W1).
**Poprawka:** przyjmowac zwroty tylko z dwoch zrodel: `ItemsToRecover` i `ReturnEquipmentFromAgents`.
Najprosciej: prefix na `DynamicTroopMissionLogic.HandlePartyItems` ustawia `_call = 0`, prefix na
`ReturnItemsToDestination` robi `_call++`, a `AddPrefix` przepuszcza bez zmian (return true), gdy `_call == 2`
(druga wywolka = `LootedItems`). Do tego czyszczenie `_out` w postfixie `FinalizeMission` (S1) - wtedy zbrojownie
pokonanych, rekruci i symulacje nigdy nie dostana cudzych stanow.

### W3. Jeniec z bitwy placi DWA razy (sprzet z niczego)
**Gdzie:** `Armoury/src/ArmouryBehavior.cs:1558-1610` (`TryStripNewCaptives`, `bag.AddToCounts(...)` :1601),
wolane po bitwie z :610, :1277, :1536; dte DynamicTroopMissionLogic.cs:241 (`if (agentState - 3 <= 1)`).

Model z CHANGELOG (30.08, "kazda sztuka wroga placi DOKLADNIE RAZ") zaklada: zabity -> scena DTE,
**ranny wziety do niewoli -> obszukanie**. Ale DTE traktuje NIEPRZYTOMNEGO (AgentState 3) tak samo jak zabitego:
caly jego sprzet (poza rozbita czescia) idzie do lupu zabojcy z szansa DropRate = 1.0. Ten sam czlowiek trafia
potem do niewoli i `TryStripNewCaptives` dosypuje mu DRUGI komplet - z szablonu `troop.BattleEquipments`,
caly (11 slotow, z koniem, bo CaptiveSpoilsIncludeMounts). W symulacji to samo: DTE
`DistributePlayerSimulationLoot` liczy zabitych I rannych przegranych (dte EveryoneCampaignBehavior.cs:571-725,
przy SimBattleFullDrop), a obszukanie dosypuje drugi raz. Do tego zywi przegrani, ktorzy zlozyli bron na koniec,
oddaja sprzet do zbrojowni swojej partii (ReturnEquipmentFromAgents), a ta zbrojownia jedzie do zwyciezcow przy
MapEventEnded - i tez sa obszukiwani.
**Poprawka:** obszukiwac tylko kapitulantow BEZ bitwy (jak w regule 29.08). W `TryStripNewCaptives` dla zrodel
"bitwa"/"menu"/"hourly" w oknie po bitwie: jesli bitwa szla z DTE (`BattlefieldLaw.CasualtyLootCut` i walka
osobista albo symulacja z DTE) - tylko `_prisonerBaseline = SnapshotPrisoners()` i wyjscie. Kryjowka: sprawdzic
osobno (DTE w kryjowce tez liczy rannych - wtedy tak samo bez obszukania).

### W4. Naprawa "z niczego": darmowa samonaprawa do 100% i wrak -> nowa sztuka za grosze
**Gdzie:** `Armoury/src/TroopSelfMend.cs:51-61`; `Armoury/src/SmithMenu.cs:923-929` (`TroopPieceCost`);
DTE `ArmyArmoryBehavior.cs:393` (`OpenScreenAsStash` - zbrojownia dziala w obie strony).

- TroopSelfMend: codziennie w miescie 10% zuzytych sztuk (min. 3) wraca do 100% BEZ zlota i BEZ materialu
  (napis "out of their pay", ale nic nie plynie). Obejmuje tez wraki 8%.
- Kowal "Mend the men's kit": wrak 8% -> 100% za `Value x 0.92 x 0.10 x (1 - rabat do 30%)` = ok. 6-9% wartosci.
- Zbrojownia przyjmuje i oddaje przedmioty. Wiec: wrak (lup 8%, takze z wlasnych poleglych od wpisu 39) albo
  zuzyty lup -> wloz do zbrojowni -> poczekaj w miescie albo zaplac 6-9% -> wyjmij nowa sztuke. Wartosc
  z niczego, im wiecej wrakow (wpis 37 i 39 ich dodaja), tym wieksza.
- Historycznie: przebity helm czy rozcieta przeszywanica to zlom na material; kolczuge latano ogniwami (drut),
  plyte klepano - robocizna i material, nie darmo.
**Poprawka:** (a) TroopSelfMend: podnosi o JEDEN stopien (jak WearTheTroops, tylko w gore), nie do 100%;
pomija stany <= 10% (wraki tylko u kowala albo do przetopu); placi: robocizna (np. 1 zl za sztuke t1-2,
3 zl t5-6) + material wedle `SelfMendParts` z taboru, zloto przez `Pay.ToSettlement`. Bez zlota/materialu - nie
naprawia. (b) Kowal: wrak (<= 10%) wymaga materialu ~50% receptury (sztabki/skora/len) i kosztuje co najmniej
tyle, co ten material; inaczej tylko przetop.

### W5. RYZYKO (niesprawdzone): latka na `AddItemToArmory` moze byc martwa przez inlining JIT
**Gdzie:** `Armoury/src/WearKeep.cs:120-122`; dte ArmyArmory.cs:95-101.
`AddItemToArmory` to malutka statyczna metoda (warunek + 2 wywolania + AddToCounts). Ten sam problem opisuje
`WesterosClimate.cs` (gettery wklejane przez JIT w wywolujacych - latka nie dziala). Jesli JIT wklei
`AddItemToArmory` do `ReturnItemsToDestination` / lambdy `ReturnEquipmentFromAgents`, `AddPrefix` nigdy nie
pobiegnie, a WearKeep po cichu nic nie zrobi. Nie da sie tego rozstrzygnac z kodu.
**Co sprawdzic / poprawka:** obecny log pisze tylko, gdy `_kept > 0` - zero linii nie odrozni "brak zuzytych
sztuk" od "latka martwa". Dodac licznik wywolan `AddPrefix` i liczbe sztuk w `_out` do linii po bitwie
("WearKeep: wydano X, zwrotow przechwyconych Y, ze stanem Z"). Jesli Y = 0 przy X > 0 - przeniesc logike na
nie-inlinowalne miejsca: prefix na `ReturnItemsToDestination` i na `ReturnEquipmentFromAgents` (duze metody).

---

## SREDNIE

### S1. Pamiec `_out` czyszczona za pozno i nie zawsze
**Gdzie:** `Armoury/src/ArmouryBehavior.cs:484-485` (AfterBattle tylko w `MapEventEnded` i tylko `IsPlayerMapEvent`);
`WearKeep.cs:104-109`.
- DTE oddaje zbrojownie pokonanych w SWOIM sluchaczu `MapEventEnded`; jesli biegnie przed naszym (DTE
  rejestruje sie wczesniej), stany sztuk zniszczonych przechodza na ten lup.
- Misje bez MapEvent (walki w zaulku, zadania w miescie) i drugie podejscie w tym samym MapEvent (oblezenie):
  resztki `_out` czekaja i trafia na rekrutow (RecruitmentPatch) albo na zwroty nastepnej bitwy.
**Poprawka:** czyscic `_out` w postfixie `DynamicTroopMissionLogic.FinalizeMission` (po zwrocie zywych) oraz
na wszelki wypadek w `OnEndMission`. Log "N zuzytych sztuk wrocilo" przeniesc tam.

### S2. Wydajnosc: dwa pelne zrzuty magazynu na KAZDEGO wystawionego zolnierza
**Gdzie:** `WearKeep.cs:39-56` (`Snapshot` w prefixie i postfixie `AssignEquipment`).
Magazyn do 600 sztuk na kategorie, setki-tysiace roznych elementow (przedmiot x stan); przy 300-500 ludziach
w pierwszej fali to 2 x N wpisow do slownika (z boxingiem `Equals(object)`) na glowe - mozliwa przycinka przy
rozstawieniu armii. Nie crash, ale niepotrzebne.
**Poprawka:** w prefixie dla kazdego slotu `equipment` (te same sloty i ten sam warunek co DTE, :205-239)
znalezc PIERWSZY element o tym StringId (to jest dokladnie ten, ktory DTE zdejmie) i zapisac jego modyfikator;
bez slownikow, bez postfixa. Uwaga na dwa sloty z tym samym przedmiotem (drugi bierze kolejny element, gdy
pierwszy mial 1 sztuke).

### S3. "Jedno zrodlo zimy" niepelne: RBL dalej dokłada zimowe kary globalnie (takze w Dorne)
**Gdzie:** rbl RealisticBannerlord.Systems.Seasons/RealisticFoodConsumptionModel.cs (Blizzard +0.4, Heatwave +0.2);
RealisticPartySpeedModel.cs (Winter -0.2, Blizzard -0.3, Flood -0.25); WeatherEventBehavior.cs (5% dziennie
zdarzenie na CALY swiat na 2-4 dni, w zimie zawsze Blizzard). WesterosClimate podmienia RBL pore roku, wiec
przy zimie 3-5 lat:
- jedzenie partii: WinterBite +50% x polnoc **plus** RBL "Active Blizzard" +40% przez ~15-20% dni zimy -
  dubel, ktory wpis 30 mial usunac (wylaczono tylko `WinterFoodPenalty`);
- predkosc: RBL zima -20% przez cale lata **i w Dorne**, w zamieci -50% razem, plus nasza kara sniegu
  (TerrainEase, lokalnie, wedle pogody mapy) - zamiec liczy sie dwa razy (globalna RBL i lokalna mapy).
**Poprawka:** postfix na RBL `RealisticFoodConsumptionModel.CalculateDailyFoodConsumptionf` usuwajacy czynnik
"Active Blizzard" (jak WinterSource dla BEE), a kare zimy/zamieci RBL dla predkosci skalowac `Northness`
z WinterBite (w Dorne ~0.75 x -> albo zero dla Dorne/Essos) lub zostawic tylko lokalny snieg z pogody mapy.

### S4. Tempo marszu i racje policzone pod stary kalendarz (168 dni)
**Gdzie:** `Settings.cs:136` (FoodConsumptionCutPercent 40 - "the world marches slower ... long year"),
`Settings.cs:282` i `WorldPace.cs:14-27` (komentarz: 50% = miesiac z kalendarza 168 dni), `Rations.cs:11-19`;
`NightRest.cs` (`SleepHoursNeeded` 6 - wiec marsz do 18 h na dobe bez kary).
- Po wpisie 11 rok ma 364 dni ("1 dzien = 1 dzien"), a u Jeffa WorldPace = 75%. Uzasadnienie ciecia racji
  (-40% "bo marsz trwa dluzej") juz nie istnieje.
- Szacunek (do zmierzenia, nie dowod): wg skali z `WorldPace.cs` (~4.75 km na jednostke, vanilla ~340 km/dobe)
  przy 75% i ~16-18 h marszu wychodzi rzedu 150-250 km/dobe dla kolumny pieszej. Historycznie: armia piesza
  20-30 km/dobe (6-8 h marszu), jazda 40-60 km, wyjatkowo 80+. Winterfell -> Krolewska Przystan (~1500 km)
  zajmuje u nas ~9 dni, historycznie piechota ~2 miesiace, jazda ~4 tygodnie.
**Poprawka:** najpierw ZMIERZYC: TerrainEase.DailyAudit (`SpeedAuditEnabled`) + pozycja gracza o 6:00 dzien po dniu
-> km/dobe. Potem: (a) limit godzin marszu - ponad 10 h marszu w dobie rosnie dlug zmeczenia (NightRest juz ma
mechanike dlugu), (b) WorldPace w strone 25-35%, (c) `FoodConsumptionCutPercent` -> 0 razem z waga zbrojowni
i wozami (patrz "Czego brakuje") - nie wczesniej, bo AI zacznie glodowac.

### S5. Zbrojownia pokonanych AI jedzie do magazynu gracza w 100%, nowa, bez ekranu lupow
**Gdzie:** dte EveryoneCampaignBehavior.cs:529-560, 751-810 (`DistributeLootRandomly` po kazdej bitwie,
takze stoczonej osobiscie); u nas nic tego nie dotyka (grep: brak latek na `DistributeLootRandomly`).
Historycznie zdobycie taboru to prawdziwy lup (Agincourt, Poitiers), ale: omija regule dzialki 30%, sprzet
przychodzi w 100% stanie (WearTheLoot i Spoils go nie widza), a do tego jest dublem z W3 (zywi przegrani).
**Poprawka:** postfix po `DistributeLootRandomly`: to, co dostala partia gracza, przeliczyc jak lup z pola -
dzialka `PlayerSharePercent` na ekran/do sakw ze stanem `PickWornModifier`, reszta zostaje w magazynie wojska
(ale tez ze stanem "z taboru" - lzejszym). Najpierw log, ile sztuk tak przychodzi.

### S6. Zuzycie wojska losuje elementy, nie sztuki, i nie te, ktore byly w bitwie
**Gdzie:** `ArmouryBehavior.cs:1355-1415` (`WearTheTroops`, losowanie :1387 `idx[MBRandom.RandomInt(idx.Count)]`).
Kazdy ELEMENT (przedmiot x stan) ma rowna szanse - element z 1 zuzyta sztuka tak samo jak element z 300 nowymi,
wiec zuzycie skupia sie na juz zuzytych (szybko schodza na dno, reszta "nie jest ruszana"), a sztuki z rezerwy,
ktora nie wyszla w pole, tez sie zuzywaja. Od wpisu 39 wiemy dokladnie, co wyszlo na bitwe (`_out`).
**Poprawka:** losowac wazone liczba sztuk (`el.Amount`), a najlepiej: w `WearKeep` w chwili zwrotu zywego
zolnierza (ReturnEquipmentFromAgents) z prawdopodobienstwem `TroopWearPercent` nadac sztuce stan o stopien
nizszy - zuzycie dokladnie tam, gdzie byl boj (i zostaje dla bitew przegranych tez, bo zywi wracaja zawsze).

### S7. Kleska nie kosztuje taboru
**Gdzie:** brak kodu (grep po Armoury/RealisticCaptivity: nic nie rusza `ArmyArmory.Armory` przy porazce lub
niewoli gracza). Magazyn DTE gracza (setki-tysiace sztuk) przetrwa kazda kleske i niewole nietkniety.
Historycznie utrata taboru po przegranej to norma (Bannockburn, Towton, Crecy dla Francuzow).
**Poprawka:** przy porazce gracza (MapEventEnded, gracz przegral i nie uciekl) albo wzieciu do niewoli:
czesc magazynu (np. 50-100% wedle tego, czy partia przetrwala) do zbrojowni zwyciezcy (`AddItemToPartyArmory`)
- przeplyw, nie kasowanie.

### S8. Furaz: ziarno z niczego
**Gdzie:** `Armoury/src/ScorchedEarth.cs:76` (`mp.ItemRoster.AddToCounts(grain, 1 + ludzie/250)`).
Paleniska spadaja (koszt dla wsi), ale ziarno nie schodzi z zadnego zapasu.
**Poprawka:** brac z `v.Settlement.ItemRoster` (rynek wsi) jedzenie do tej ilosci; czego tam nie ma - nie ma.
Historycznie furaz to tez pasza dla koni - patrz "Czego brakuje".

---

## NISKIE

- **N1. WearKeep LIFO** (`WearKeep.cs:91-92`): przy sztukach, ktore nie wrocily, odrzucane sa stany wydane
  najwczesniej (zwykle "nowe"), wiec zniszczenia zabieraja sztuki nowe, a zostaja zuzyte. Losowac indeks
  zamiast brac ostatni.
- **N2.** `AddPrefix` (WearKeep.cs:93, 97) omija `TryResolveArmoryItem` i `ItemBlackList.Test` z DTE
  (ArmyArmory.cs:97). Wolajacy z bitwy i tak to sprawdzaja, ale RecruitmentPatch nie - przy resztkach `_out`
  (S1) przedmiot z czarnej listy moglby wejsc. Wolac `TryResolveArmoryItem` przez refleksje i dodawac
  rozwiazany przedmiot.
- **N3. Wraki** (`BattlefieldLaw.cs:562-575`): podpis postfixa (`affectedAgent`, `affectorAgent`) zgadza sie
  z DTE `OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState, KillingBlow)`; `GetRandomArmorByBodyPart`
  ma jedno wywolanie (DTE :330), wiec para zapis/odczyt jest spojna. Uwagi: (a) "dodatkowa rozbita czesc"
  wroga t3-t6 (25-60%, DTE :336-368) dalej znika bez wraka; (b) wraki idą do gracza w 100%, a lup w 30% - nie
  ta sama regula; (c) gdyby DTE rzucil wyjatek miedzy wyborem a koncem metody, `_lastPick` zostaje i trafi
  do nastepnego agenta - wyczyscic `_lastPick` w prefixie `OnAgentRemoved`. Wraki tylko po wygranej (DropShare
  przy przegranej) - zgodne z "kto trzyma pole".
- **N4. TerrainEase** (`TerrainEase.cs`, Swap 0.3 dla lasu): vanilla przy perku ForestKin i przy cesze kultury
  `BattanianForestSpeedFeat` (w ROT `battania` = Polnoc!) daje inne wartosci (tw DefaultPartySpeedCalculatingModel
  .cs:264-283); my cofamy zawsze 0.3, wiec Polnoc i ForestKin maja w lesie premie zamiast kary. Cofac dokladnie
  ten czynnik, ktory dala vanilla (czytac linie z `__result` o nazwie `_movingInForest`).
- **N5. Stare komentarze i komunikaty:** `Settings.cs:136, 282`, `WorldPace.cs:14-27`, `Rations.cs:15-19`,
  `WinterBite.cs:14-15` mowia o roku 168 dni; `CrashScribe/src/Mends.cs:2517` pisze "jedyna trwala strata
  amunicji to AmmoAttrition" - usuniete 17.09. Wprowadzaja w blad nastepnego Claude.
- **N6. MarchPace** (`MarchPace.cs:37-39`) liczy luzaki i juczne z `ItemRoster.NumberOfMounts/PackAnimals`,
  ktore vanilla liczy TYLKO bez modyfikatora (tw ItemRoster.cs:439-452) - kon z lupu ze stanem nie niesie
  piechura i nie jest taborem. Konie w zbrojowni DTE (tam laduja konie z awansow - Stables.cs:79) nie sa
  ani luzakami, ani stadem.
- **N7. ScrapCapPerCategory 600** (dte ArmyArmoryBehavior.cs:283): nadwyzka magazynu ponad 600 na kategorie
  znika (zlomowanie) - przy wadze magazynu (patrz nizej) ten limit powinien zastapic udzwig, a nadwyzka
  isc na sprzedaz/zostac w osadzie, nie w nicosc.

---

## Do decyzji Jeffa (sprzecznosc z wczesniejszymi poleceniami)

**Strzaly i belty nie zuzywaja sie wcale.** `CrashScribe/src/Mends.cs:4259-4268` (`QuiversComeBack`) zwraca
kazdy kolczan, takze pusty, i to pelny (magazyn nie pamieta liczby strzal). Jeff 13.09: "strzaly mialy sie nie
konczyc"; 17.09: "nic ma nie pekac". Dzis: "ma byc historycznie" - a historycznie strzaly byly glownym
materialem zuzywalnym kampanii (setki tysiecy snopow, docs/HISTORIA-STRZALY.md). Propozycja (wedle
HISTORIA-STRZALY.md, szacunki): w `QuiversComeBack` kolczan wraca z prawdopodobienstwem
`p = (Amount + odzysk x (Max - Amount)) / Max`, gdzie odzysk = 0.45 dla strony, ktora trzyma pole (belty 0.5),
0 dla pokonanego (`Mission.Current.MissionResult`); "do naprawy" (~20%) jako pozniejszy etap przez FletchForge.
Oczekiwana liczba strzal sie wtedy zgadza, a kolczan z 1 strzala nie odradza sie pelny. Bohater gracza ma
amunicje odnawiana przez vanilla - osobna sprawa.

---

## Czego brakuje (do realistycznej logistyki)

1. **Waga zbrojowni DTE** - magazyn wojska (setki zbroi, tysiace strzal) nic nie wazy
   (tw DefaultInventoryCapacityModel.cs:102-113 sumuje tylko `ItemRoster` partii). To warunek wstepny
   dla wozow: postfix na najbardziej zewnetrzny `CalculateTotalWeightCarried` (licznik zagniezdzenia jak
   Rations/SpeedDepth) z osobnym wpisem "Army stores" - waga `ArmyArmory.Armory` minus sprzet na grzbietach
   (to, co nosza zolnierze, juz jest w ich 20 kg). Dla AI - `PartyArmories` DTE, gdy gracz zadziala.
2. **Wozy** - projekt w AUDYT-WOJNA-LOGISTYKA.md ("Projekt wozow"); bez pkt 1 i bez realnych racji nie maja
   sensu (dzis 100 ludzi niesie jedzenie na ~66 dni bez zwierzecia).
3. **Racje realne**: 0.3 kg/czlowieka/dzien (Rations -40%) wobec 1.5-2 kg historycznie (chleb, piwo, groch,
   mieso). Po wozach i wadze magazynu: `FoodConsumptionCutPercent` 0, a docelowo jednostka jedzenia na 6-7
   ludzi dziennie zamiast 20 (latka na `NumberOfMenOnMapToEatOneFood` albo czynnik w Rations).
4. **Konie jedza** - BK liczy pasze tylko na pustyni i (w polowie) w sniegu. Historycznie 5-10 kg obroku
   dziennie, latem wypas. Przy zimie 3-5 lat wypasu nie ma - pasza musi jechac wozami. To jest glowny koszt
   logistyczny jazdy i powod, czemu zimowe kampanie byly rzadkie. Dotyczy tez koni w zbrojowni DTE.
5. **Godziny marszu** (S4) - dzis kolumna moze isc 18 h na dobe bez kary.
6. **Utrata taboru przy klesce** (S7).
7. **Zuzycie i naprawy AI** - zbrojownie AI nie zuzywaja sie wcale (WearTheTroops tylko gracz), wiec AI ma
   wieczne nowe zbroje, a gracz nie. Ta sama regula po bitwie dla AI + platna naprawa w miescie (popyt na
   kowali, mniej kupowania nowego).
8. **Naprawa = robocizna + material** (W4) zamiast procentu `Value`.
9. **Amunicja zuzywalna + fletcherzy w obozie** (decyzja Jeffa wyzej).
10. **Furaz z zapasu wsi i dla koni** (S8, pkt 4); furazowanie wlasnych ziem za oplata (purveyance).
11. **Ranni i jency w kolumnie**: jency ida pieszo (MarchPace to liczy), ale nie jedza w racjach rownie
    z ludzmi (vanilla 1/2) - drobne, zostawic.
12. **Diagnostyka pod pomiar**: jedna linia dziennie dla gracza - km przebyte w dobie, godziny marszu,
    waga taboru i magazynu, zapas jedzenia w dniach, kolczany pelne/puste w magazynie. Bez tego kazde
    strojenie bedzie zgadywaniem.

---

## Kolejnosc proponowana (jedna zmiana naraz, wedle CLAUDE.md)

1. W1 (poprawka `AfterDtePartyItems`) - jest bledem niezaleznie od WearKeep, mala zmiana.
2. W2 + S1 (WearKeep tylko dla zwrotow, czyszczenie w FinalizeMission) + log z W5 - jedna zmiana w WearKeep.cs.
3. W3 (bez obszukiwania jencow z bitew DTE).
4. W4 (naprawy placone, wraki nie do darmowej naprawy).
5. S3 (blizzard RBL).
6. Pomiar tempa (S4) - dopiero potem WorldPace/racje/waga magazynu/wozy.
