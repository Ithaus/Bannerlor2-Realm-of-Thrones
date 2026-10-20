# r3-jazda - wiecej jazdy u AI (PLAN 6.14), galaz w-toku/r3-jazda na 0c86ae9a (WGRANIE 90)

## Problem (dowod)
Logi testow na WGRANIU 90 (`Modules/Armoury/Armoury-2026-10-19_22-27-20.log` - 160 dob z zapisu, d109236; `Armoury-2026-10-19_21-58-59.log` -
nowa kampania d120):
- "Rycerze i jazda (219)": jazda partii rodow AI 11.7% (160 dob) / 9.3% (d120), t3+ 16.3% wobec celu szablonow 26.2%. Cel Jeffa 12.10:
  12-15% w 120 dob, ok. 20% po roku.
- "Konie AI (175)" d109236: konni bez konia w zbrojowni **3743 z 16568**, a jednoczesnie **wolne konie w zbrojowniach 2975** (zwykle 2349,
  bojowe 586, szlachetne 40); czeka na konia swojej kategorii 9742 ludzi w 428 partiach; zakupy lordow 54-82 koni/d.
- "Jezdzcy i uprzaz (E8/H)" d109236 rozdziela to na rodzaj partii: partie rodow AI - zbrojownie konie 10190, z koniem 9710, **bez konia 2066**;
  **zalogi - zbrojownie konie 5610 przy 3501 jezdzcach wedlug typu (z koniem 2703)**. Czyli wolne konie leza w ZALOGACH, a jezdzcy bez konia
  sa w PARTIACH LORDOW. Nowa kampania d120: partie rodow AI bez konia 1642, zalogi konie 5176 przy 4800 jezdzcach.
- "Hodowla (220)": mnozniki x0.12/x0.31/x0.10/x0.10, stos na targu 8316/2016/629 (zwykle/bojowe/szlachetne).
- H-1 (E8/H): w partiach rodow AI 490 jezdzcow z koniem bez uprzezy; brama wstrzymuje 216 celow awansu/d z braku uprzezy; w zbrojowniach zalog
  wolna uprzaz 854.

## Przyczyna
1. Konni bez konia mimo wolnych koni: rachunek koni (`Stables.Balance`) jest per partia - w jednej partii jezdziec bez konia i wolny kon nie
   wystepuja razem (Balance sadza wszystkich, co sie da), wiec 3743 i 2975 to rozne partie. Zalogi trzymaja konie, ktorych nikt nie rusza:
   `MenPurse.SellArmorySurplus` sprzedaje nadmiar koni TYLKO partii lordow (galaz `SellSpareHorses`: `!garrison`), zalogi koni nie kupuja
   (GarrisonKit pomija Horse/HorseHarness), a ani Remount (tylko tabor tej partii), ani Stajnia (targ, wsie) nie siegaja do zbrojowni zalogi.
   Konie zalog zostaja po poleglych jezdzcach zalog, po dorobku startowym (ColdStart) i po przeniesieniach A7, gdy pula koni dawcy jest dzielona
   proporcjonalnie (PlanKits `share`).
2. Hodowla x0.31 (bojowe): regulator `HarvestValve` (E6/K4) tnie produkcje, bo stos koni bojowych na targu (2016) przekracza ValveStockDaysHigh
   dni zuzycia - konie leza, nikt ich nie kupuje. Kolejka czekajacych nie stoi na braku koni na targu: "brak koni" to 30-40 wizyt/d (pusto 18-23,
   za drogie 12-16) wobec 63-85 wizyt z potrzeba; zakupy ogranicza budzet postoju (F5: "budzet wyznaczyl przydzial 23-34, kiesa 22-26";
   ok. 2270 zl za konia) i limit postoju. Wiecej hodowli = wiecej koni na polkach, nie wiecej jazdy.
3. Uprzaz: jak konie - wolna uprzaz zalog (854) nie trafia do lordow; zakup siodel ma zapas siodel (SaddleLaw.BuyNeed), ale tylko z polek.

## Zmiana (Armoury; dwa nowe klucze, domyslnie TAK, grupa MCM "Crafts, raw materials and horses (stage 6)")
1. `GarrisonHorsesToLords` (nowy plik `Armoury/src/GarrisonHorses.cs`, wpiecie w `Stables.OnSettlementEntered` zaraz po `Remount`):
   lord rodu AI (`CavalryTemplate.Steered`, nie rod gracza) na postoju w miescie albo zamku swojego krolestwa (nie oblezonym, nie twierdza gracza),
   przy TroopsFightWithOwnKitOnly:
   - (1) jezdzcy bez konia dostaja do zbrojowni DTE wolne konie zbrojowni zalogi - ta sama kolejnosc kategorii co Remount (z prawem do bojowego:
     bojowy, szlachetny, zwykly; reszta: zwykly, bojowy, szlachetny; najtansze w kategorii);
   - (2) przy AiSellsSpareHorses gotowi do awansu na jezdnego dostaja do taboru konie swojej kategorii - tylko brak (`Reserve.Short - Have`,
     bez luzu), najwyzej `AiMountMaxPerVisit` na postoj; awans placi tym koniem jak kazdym z taboru (PayInHorses). To NIE jest hodowla: krok
     przesuwa zapas zalog, nie dodaje swiatu ani jednego konia bojowego (punkt (2) zadania - OTWARTY, patrz D2 i Recenzja).
   - Wolne konie zalogi = `Stables.Balance(zaloga, jezdzcy jej patroli BK)` minus luz zalogi `max(AiMountSpareBuffer, AiSpareHorseShare x jej jezdzcy)`
     (luz najpierw ze zwyklych). Zaloga z jezdzcem bez konia nie ma wolnych, wiec nic nie oddaje. Patrole BK: mapa patroli doby (wspolna z C10 i A7)
     dostaje teraz takze patrol powstaly w ciagu doby (postfiks BK `GarrisonPartyComponent.CreateParty`), a patrol nieczynny (wrocil, zginal) sie nie liczy.
   - Odmowa DTE: sztuka z czarnej listy DTE (zbior odmow sesji `AiGear.DteTakes`) jest pomijana przy wyborze; po odmowie bierzemy kolejna najtansza
     tej kategorii (dotad odmowa blokowala cala kategorie, a przy uprzezy caly krok, przy kazdym postoju).
   - Zamknieta ekonomia: twierdza wlasnego rodu - przesuniecie bez zlota (kon zostaje wlasnoscia rodu); twierdza innego rodu tego krolestwa - pan
     partii placi glowie rodu pana twierdzy cene konia na targu (`Stables.MarketPrice`; bez targu wartosc sztuki) `GiveGoldAction.ApplyBetweenCharacters`
     (bohater -> bohater), z budzetu zwyklego zakupu koni (F5: min(przydzial sprzetu rodu, AiMountGearPurseShare x kiesa ponad AiGearGoldReserve);
     rod bez przydzialu i najemnicy - AiMountPurseShare kiesy); kwota schodzi z przydzialu (`ClanBudget.GearSpent`).
   - Przeniesienie jak A7 (`GarrisonArmory.MoveKits`): najpierw `AiGear.AddToArmory` u lorda (odmowa DTE - kon zostaje w zalodze), potem
     `AiWear.MoveWorn` (u koni tylko spis "nie lup"), potem zdjecie ze slownika zalogi (zapis zbrojowni zalog 171 zapisuje stan).
   - Targ NIETKNIETY: polki, AiMountMarketSharePercent 40, AiMountShelfFloor bez zmian; zakup Stajni tego postoju liczy brak PO tym kroku
     (`want = waitUp + lack`, `Reserve`), wiec z polek schodzi mniej, nie wiecej.
2. `GarrisonHarnessToLords` (`GarrisonHorses.GiveHarness`, wpiecie w `SaddleLaw.OnSettlementEntered` po uprzezy z taboru, przed zakupem siodel):
   te same warunki i rozliczenie; uprzaz rodziny konia (`SaddleLaw.Fits`) ze zbrojowni zalogi ponad jej jezdzcow z koniem (`SaddleLaw.Pairs` z
   jezdzcami patroli BK) i luz, najwyzej `SaddleLaw.BuyNeed` lorda (ta sama miara co zakup siodel). `SaddleLaw.OnSettlementEntered` nie wychodzi juz
   przy pustym taborze przed tym krokiem (blok taboru bez zmian w srodku). Klucz dziala sam (wymaga tylko TroopsFightWithOwnKitOnly i
   RiderNeedsHarness) - wylaczenie GarrisonHorsesToLords nie wylacza uprzezy.
3. Pomiar (tylko log):
   - nowa linia doby **"Konie zalog dla lordow (r3-jazda): dzien N"** - postoje, wolne konie zalog ponad luz, jezdzcy bez konia przy postojach
     PRZED / PO, do kolejki awansow (tabor), wedlug kategorii, z wlasnego rodu / od innych rodow za zl, bez budzetu, uprzaz (postoje z brakiem,
     brak przed, wzieli, bez budzetu, DTE nie przyjal - osobno dla uprzezy), "z targu 0 (polki nietkniete)", DTE nie przyjal koni, **jazda partii
     rodow AI ze spisu E8/H: wedlug typu % (to licza linia 219 i SZ-A8-1), z koniem % i jaka czesc jazdy wedlug typu ma konia - na starcie biegu
     i w ostatniej dobie**, **polki koni miast i zamkow (E6/0, zwykle/bojowe/szlachetne) na starcie biegu i w ostatniej dobie**, potkniecia
     (doby i razem). Zloto w linii tylko po udanym przelewie (konie i uprzaz osobno);
   - linia "Konie AI (175)": po wolnych koniach dopisany podzial `[r3-jazda: partie lordow - bez konia X, wolne Y; zalogi - bez konia Z, wolne W]`
     (spis tej samej doby z `HorseCensus.CountParty`);
   - jazda % - jak dotad linia "Rycerze i jazda (219)"; polki koni - linia "Konie (E6/0)" (SZ-A8-2) i "Hodowla (220)" (stos na targu).
4. `tools/sprawdz_logi.py --grupa szybkie`: wiersze **R3J-1** (linia codziennie, konie z zalog > 0/d, po < przed, potkniecia 0) i **R3J-2**
   (E8/H partie rodow AI bez konia w ostatniej dobie okna <= 80% WGRANIA 90 dla nowej kampanii (1642 -> <= 1313) i <= 70% dla biegu z zapisu
   d240-400 (1898 -> <= 1328)) i **R3J-3** (E8/H partie rodow AI: z koniem wsrod jazdy wedlug typu w ostatniej dobie okna >= WGRANIE 90 + 2 pkt:
   nowa kampania 77.8% -> >= 79.8%, bieg z zapisu 75.2% -> >= 77.2%). Na logach WGRANIA 90: R3J-1 NIE (brak linii), R3J-2 NIE (1641 / 1898),
   R3J-3 NIE (77.8% / 75.2%) - jak trzeba przed zmiana.

## Ryzyko / co sprawdzic (kontrola calosci)
- Regresje: Remount, zakup Stajni, KnightBuy, SellSpareHorses bez zmian w kodzie; Give zmienia tylko `lack` przekazywany dalej (mniej zakupow).
  Zaloga oddaje tylko konie ponad swoich jezdzcow, jezdzcow patroli BK i luz - jej obrona nie traci koni pod jezdzcami. Konie w drodze do taboru
  (krok 2) najwyzej do braku kolejki, a sprzedaz lorda zaczyna sie ponad 1.5 x zapas (`Reserve.Keep`), wiec nie ma pompy kupno-sprzedaz.
  Zalogi nie kupuja koni, wiec nic ich nie odkupuje.
- Kolizje: A7 (MoveKits) dalej przenosi konie z ludzmi; C10 (SellWeek) koni nie sprzedaje; zapis zbrojowni zalog (171) zapisuje slownik po zmianie.
  HorseState odswiezany dla obu partii. Zalogi nie kupuja koni (AiGear.Deficit liczy sloty 0-9, kon to Stajnia), a uprzaz kupuja tylko pod swoich
  jezdzcow - zabieramy wylacznie ponad nich i luz, wiec nic sie nie odkupuje. Sprawdzic w logu: "Zbrojownie zalog (171)" bez nowych odmow;
  "Stajnia AI (sklad7b-p)" - "jezdzcy bez konia po dosadzeniu" (Remount jest PRZED krokiem zalogi, wiec ta liczba zostaje, spada dopiero stan doby).
- Zloto: wlasny rod - zero zlota; inny rod - przelew miedzy bohaterami (suma swiata bez zmian), w "Pieniadz swiata" jako przelew gry miedzy
  postaciami; budzet F5 pomniejszony jak przy zakupie. Zaplata w `finally` - za to, co naprawde przeszlo, takze gdy przenoszenie przerwie
  wyjatek (licznik potkniec w linii ma byc 0). Kwota "za ... zl" przy koniach to tylko konie, uprzaz ma swoja; obie tylko po udanym przelewie.
- Nowa latka Harmony: postfiks na BK `GarrisonPartyComponent.CreateParty(Settlement)` (tylko dopisanie do mapy patroli biezacej doby, w try).
  Brak metody w innej wersji BK - latka sie nie wpina (linia startu "GarrisonArmory: ... nowy patrol w ciagu doby (r3-jazda) BRAK"), reszta dziala
  jak przed recenzja.
- Targi: nie dotykane bezposrednio; posrednio mniej zakupow z polek - polki koni (SZ-A8-2, "Konie (E6/0)") nie powinny spasc; regulator hodowli
  moze ciac dalej (mniejsze zuzycie z targu) - to zgodne z zasada "hodowla wedlug popytu".
- Koszt: tylko przy postoju lorda w twierdzy swojego krolestwa: Balance zalogi (jeden przebieg slownika i rostera), patrole BK z mapy doby (bez
  nowego przegladu), Reserve lorda (jak zakup). Uprzaz: BuyNeed liczony dopiero, gdy zaloga ma zbrojownie. Bez alokacji w goracych sciezkach
  (postoj to zdarzenie, nie tick).
- Watki: nasluch postoju (watek gry); liczniki statyczne bez wspoldzielenia. Potkniecia per postoj, nic sie nie gasi.
- Zapis: bez nowego stanu w zapisie; klucze nowe (nie ma ich w Armoury.json Jeffa - domyslne TAK dzialaja).
- Nie sprawdzone w grze (brak autotestu w tej galezi): ile koni zalogi naprawde odda - zalezy od tego, jak czesto lordowie stoja w twierdzach
  swojego krolestwa z wolnymi konmi.

## Status
DO SPRAWDZENIA (build Armoury kod 0, 0 Error(s); bez autotestu, bez wgrania).

## Decyzje (podjete samodzielnie, Jeff spi)
- D1: kolejnosc zadania (1) -> (2) -> (3) zrobiona jednym mechanizmem "zapas zalogi dla lordow swojego krolestwa": (1) jezdzcy bez konia,
  (2) kolejka awansow konmi swojej kategorii, (3) uprzaz - kazde pod kluczem (konie i kolejka - GarrisonHorsesToLords, uprzaz - GarrisonHarnessToLords).
- D2: regulatora hodowli (x0.31) NIE luzuje w tej paczce: stos bojowych na targu 2016 szt. lezy, a wiekszosc nieudanych zakupow to budzet i limit
  postoju. Regulator sam idzie w gore (x1.3 na decyzje), gdy zuzycie >= 98% produkcji i stos < 30 dni. Ale (recenzja) 18 z 63 wizyt z potrzeba
  d109236 trafilo na pusto (brak konia potrzebnej kategorii na polce i we wsiach) - to ograniczenie od strony podazy (rozklad koni miedzy
  miastami), ktorego ta paczka NIE dotyka. Punkt (2) zadania (konie bojowe z hodowli dla kolejki) jest OTWARTY - krok (1b) nie jest hodowla.
- D3: wlasny rod bez zlota, inny rod krolestwa za cene targu glowie rodu pana twierdzy (nie kasie miasta): zaloge i jej zbrojownie utrzymuje rod pana.
  Obce krolestwa i twierdze gracza - nie.
- D4: zaloga zatrzymuje luz `max(AiMountSpareBuffer, AiSpareHorseShare x jezdzcy)` - ta sama miara co zapas lorda (K2), bez nowego parametru.
- D5: konie dla kolejki do TABORU (nie do zbrojowni) - awans placi koniem z taboru (PayInHorses); limit AiMountMaxPerVisit jak zakup.
- D6: granice targu (AiMountMarketSharePercent 40, AiMountShelfFloor) bez zmian (Jeff 15.09).

- D7 (recenzja): miara kroku (1) to jazda RZECZYWISTA (E8/H "z koniem", R3J-2, R3J-3), nie linia 219. Krok (1) daje konia jezdzcom, ktorzy juz sa
  konni wedlug typu - w bitwie to wiecej ludzi na koniu (bez konia jezdziec przy TroopsFightWithOwnKitOnly walczy pieszo), ale linia 219 i SZ-A8-1
  licza konnych wedlug typu (ch.IsMounted), wiec ich nie rusza. Na linie 219 dziala tylko krok (2) (kolejka awansow, limit postoju, jednorazowy
  zapas wolnych koni zalog po kroku (1)) i posrednio zakup Stajni (Reserve.Short zawiera jezdzcow bez konia - mniej braku zostawia budzet i limit
  postoju na konie dla awansow). Celu Jeffa 12-15% / ok. 20% wedlug linii 219 ta paczka NIE przybliza wyraznie - to wymaga osobnego kroku po
  stronie podazy koni bojowych i budzetu zakupow (OTWARTE).

## Recenzja (PELNA) - co zrobione z uwagami
- WAZNE (miara 219 liczy wedlug typu, krok (1) jej nie rusza): PRAWDA (CavalryTemplate.cs `if (ch.IsMounted) mounted += el.Number`; te same 13067
  konnych w linii 219 i w E8/H "jazda wedlug typu"). Poprawione: D7, test w grze i notatki testu oceniaja krok (1) miara E8/H "z koniem" (R3J-2,
  nowy prog R3J-3), linie 219 / SZ-A8-1 przypisuja krokowi (2); linia "Konie zalog dla lordow" pokazuje jazde wedlug typu i z koniem (start biegu
  i ostatnia doba) oraz polki koni. Cel 20% wedlug linii 219 - OTWARTY, napisane wprost.
- drobne (patrol BK z ciagu doby poza mapa): PRAWDA (BK `CreateParty` w DailyTickSettlement zdejmuje ludzi z zalogi; mapa doby z `PatrolsToday`).
  Poprawione postfiksem na `CreateParty` (dopisanie do mapy biezacej doby) i pomijaniem nieczynnych patroli w `PatrolRiders`. BK ma najwyzej jeden
  patrol na osade (sprawdza id `bkGarrisonParty_` + nazwa), wiec mapa nie rosnie.
- drobne (punkt (2) zadania nie zrobiony): PRAWDA - D2 przepisane, punkt (2) OTWARTY, "pusto 18/63" opisane jako ograniczenie podazy.
- drobne (klucz uprzezy zalezny od klucza koni): PRAWDA - GiveHarness sprawdza teraz GarrisonHarnessToLords i TroopsFightWithOwnKitOnly.
- drobne (odmowa DTE blokuje kategorie i powtarza sie): PRAWDA - wybor pomija `!AiGear.DteTakes(it)`, po odmowie kolejna najtansza (straznik petli
  64; odmowa bez wpisu do zbioru odmow, czyli blad wywolania - koniec jak dotad).
- drobne (zdanie w naglowku, liczniki): PRAWDA - naglowek GarrisonHorses.cs poprawiony (A7 przenosi konie z ludzmi; wolne konie zostaja z dzielenia
  puli, poleglych i ColdStart); osobne "bez budzetu" i "DTE nie przyjal" dla uprzezy; `Pay` zwraca wynik, zloto liczone tylko po udanym przelewie.

## Test w grze (co ma pokazac)
- Linia "Konie zalog dla lordow (r3-jazda)" codziennie; "po" < "przed"; potkniecia 0; DTE nie przyjal ~0; segment jazdy: "z koniem" i "czesc jazdy
  wedlug typu z koniem" w ostatniej dobie wyzej niz na starcie biegu; segment polek: razem nie mniej niz ok. 70% startu biegu.
- "Konie AI (175)": konni bez konia (razem i w podziale partie lordow) w dol; wolne konie zalog w dol do luzu; "czeka na konia" w dol.
- "Jezdzcy i uprzaz (E8/H)": partie rodow AI bez konia w dol (R3J-2), z koniem wsrod jazdy wedlug typu w gore (R3J-3), z koniem bez uprzezy (H-1) w dol.
- "Rycerze i jazda (219)": jazda partii i t3+ - SZ-A8-1 nie gorzej niz baza - 1 pkt; wzrost mozliwy tylko z kroku (2) i zakupu (maly), nie z kroku (1).
- Polki koni ("Konie (E6/0)", SZ-A8-2) >= 70% bazy - targi nie pustoszeja.
- Linia startu "GarrisonArmory: ... nowy patrol w ciagu doby (r3-jazda) wpiety".
