# Etap 6 - rozpoznanie D: styki z etapami 4 i 5, podzial na paczki (11.10.2026)

Status: SAMO ROZPOZNANIE. Kod, gra, ustawienia i zapisy nietkniete; nic nie budowane.
Pytanie: gdzie etap 6 (PLAN-DO-KONCA rozdz. "Etap 6", pozycje 6.1-6.19, decyzje Jeffa 11.10 17:30) dotyka tego samego stanu, metody albo progu co etap 4
(ksiega ludzi, wolne rece, wioski poboczne, zima, spichlerz) i etap 5 (kasy miast, kiesa ludu, markietani, lup); jak podzielic etap 6 na paczki i co mozna kodowac
rownolegle. Liczby z logow - `rozpoznanie-A-logi.md` (RA6); tu tylko te, ktore rozstrzygaja kolejnosc.

Skroty (SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad`):
- **K:** = `SCR\e4\koniec\Armoury\src` - galaz `w-toku/zbior-e4-koniec` 7658247 (etap 4 + E5/0 scalone; test koncowy etapu 4 trwa).
- **A1:** = `SCR\e5\a1\...\src` (`w-toku/e5-a1` e8868d2), **A25:** = `SCR\e5\a25\...` (`w-toku/e5-a2-5` f3f726a), **A6:** = `SCR\e5\a6\...` (`w-toku/e5-a6` c71aa64).
  Wszystkie trzy od bazy 392c519 (przed E4/8-E4/11); porcja B (kiesa ludu) - tylko projekt P5 rozdz. 3.
- **G:** = `SCR\dec32\cs_full` (gra 1.4.8). **z31A** = `SCR\kopia-z31-A\Armoury-2026-10-11_08-09-11.log` (120 dob, kod w grze), **L728** = `SCR\kopia-728\...log` (728 dob, starszy kod).
- **PL** = `docs\PLAN-DO-KONCA-MODA-2026-10-09.md`; **P4** = `docs\etap4\PROJEKT-ETAP4-...`; **P5** = `docs\etap5\PROJEKT-ETAP5-KIESA-LUDU-KASY-MIAST-2026-10-11.md`;
  **E50** = `docs\etap5\PLAN-KODU-E50.md`; **K13** = `docs\PLAN-K13-2026-10-07.md` (paczki 131-139 = zrodlo 6.1, 6.3, 6.4, 6.5, 6.17); **KL** = `docs\PROJEKT-KIESA-LUDU-2026-10-09.md`;
  **DK** = `docs\przekazanie-lawa-2026-10-07\DIAGNOZA-NADMIARU-KONI.md`; **SP** = `docs\STAN-PRAC.md`.

---

## 0. Odpowiedz krotko

1. **Etap 6 ma piec wezlow wspolnych z etapami 4-5** (rozdz. 2): (W1) jeden mnoznik produkcji wsi `MaterialLaw.ProdPostfix` - siedza w nim juz E4/5 i E4/7, wejda
   6.1, 6.3, 6.4, 6.12, 6.18; (W2) wartosc wyrobow w kiesie ludu (`OnItemProducedEvent`, 35% wartosci) - 6.1, 6.3, 6.5 wydluzaja lancuchy wyrobow posrednich;
   (W3) kazdy zakup przez kase miasta idzie od A1 przez `TownPurse.BuyFloor` - 6.1, 6.2, 6.7, 6.13 dodaja albo zmieniaja takie sciezki; (W4) konie - zrodla
   z niczego zostawione przez E4/8 i E4/10, pasza zimowa E4/1, lup A6, konsumpcja koni w koszyku kiesy; (W5) ludzie do pracy i "kraina" z ksiegi ludzi (6.2, 6.19).
2. **Kod etapu 6 mozna zaczac czesciowo juz teraz** (pomiar, 6.15, 6.18, 6.19, 6.9+6.10) - te paczki nie dotykaja plikow porcji A/B etapu 5. Ruda/zelazo (6.1),
   lup koni (6.11), konie z AI na targ (6.13), dziury (6.7), juczne (6.16) - **po scaleniu zbioru A etapu 5**. Pensje i popyt (6.3, 6.5, 6.17, 6.2, 6.14) - **po kodzie B1**
   (kiesa ludu). Kalibracja hodowli (6.12), zaworu (6.4) i jazdy 20% (6.14) - **po tescie B** (kiesy panow zamkow, budzety rodow).
3. **Szesc decyzji wymuszonych przez styki** (rozdz. 4) - najwazniejsze: D6-1 (35% placy od wartosci kazdego wyrobu liczy wsad drugi raz - musi rozstrzygnac B1
   przed 6.1/6.5), D6-3 (mieszczanie "zjadaja" konie - sprzeczne z "kon ginie tylko zabity" i z koszykiem B1), D6-4 (krok popytu x1.3 z paczki 132 po B1 nie moze
   byc mnoznikiem budzetu).
4. **Podzial: 17 paczek w 4 lancuchach + pomiar** (rozdz. 5): K konie (6), R ruda (4), W wies i towary (4), M rzemioslo i lore (2), N naprawy (1), E6/0 pomiar.
   Duze: 6.1, 6.2, 6.5 (warunkowa). NK: 6.1, 6.9+6.10, 6.12.
5. **Test etapu 6 osobno, po wgraniu zbioru B etapu 5** (rozdz. 7): E6/0 (tylko log) moze jechac w tescie zbioru A albo B jak E5/0 w tescie E4; test etapu
   120 dob nowej kampanii z progami "Skonczone, gdy" + regresja progow etapu 4 (zima: wiecej koni = wiecej paszy) i etapu 5 (plon, zatkane wsie zamkowe,
   panowie zamkow - konie to ok. 54% wartosci produkcji wsi zamkowych).

---

## 1. Stan na 11.10 (co jest gdzie)

| Co | Stan | Dla etapu 6 |
|---|---|---|
| Etap 4 (E4/0-E4/11, E4/6a, K8, R6/W5) + E5/0 | K: 7658247, test koncowy (364 + zima 120 + 728 dob) trwa | baza kodu etapu 6 "teraz"; `FreeHands`, `VillageBook`, `Famine`, `PeopleBook.*`, `TownFolk.*` |
| E5/A1 kasy miast, A2-A5, A6 lup | kod + recenzje, nie scalone ze soba ani z K | `TownPurse.BuyFloor` w 8 sciezkach zakupu; A3 rezerwy sakiewki; A6 lup tylko z kiesy pokonanego |
| E5/B1-B6, C1-C4 | tylko P5 (B1 kod po E4/7b, kalibracja po biegu A) | kiesa ludu `TownFolk.Earn/PayForGoods`; place rzemiosla 35% (KL L7); wies 65% / pan 35% |
| Etap 6 | nic w kodzie; K13 131 (receptury wedlug wartosci), 132 (x1.3), 133 (wytop), 134 (zawor), 135-138 - nie ma ich w K (grep `VillageOutputStep`, `HarvestValve`, `WorkshopRecipes` - brak) | 6.19 czesciowo jest: `WorkshopLaw.Candidates` woli kulture miasta i neutralne, unikaty odciete (K:WorkshopLaw.cs:523-530) |

Liczby, ktore ustawiaja kolejnosc (z31A, doba 120; szczegoly RA6):
- Warsztaty 1782 szt./d, korpus 71 (srednia 120 dob 181, d93-120 101), "brak surowca" 795 (ruda 640), "brak zlota/kupca" 0 (z31A:31497) - **A1 moze to zmienic** (BuyFloor).
- Ruda: wsie 301 ladunkow, zuzycie 286, miast bez rudy 34 z 97 (z31A:31544). Len/welna/skory na polkach - indeks 0.83-0.99 (z31A:31496).
- Konie na polkach 2 171 -> 11 622 (+79/d) w 120 dobach (z31A, "Rynek broni"); L728: 2.2 tys. -> 19.6 tys. (d360) -> 5.3 tys. (d720) - popyt rosnie z czasem,
  **120 dob nie wystarczy do kalibracji hodowli**. Konni 14 224 z 190 936 (7.4%), wolne konie w zbrojowniach 2 090 (z31A:31714).
- Konni rekruci z wlasnym koniem 43/d: ochotnicy 16 (kon kupiony z polki przez notabla - VolunteerKit), najemnicy 27 (kon z wzorca = z niczego) (z31A:31710).
- Utarg taborow wsi 221.6 tys./d, pan 59.3% (z31A:31737); zatkane magazyny wsi zamkowych 14 z 260 (z31A:31610). 71 stadnin, 47 jedzie do zamkow = ok. 54% wartosci
  produkcji wsi zamkowych (EKONOMIA-FUNDAMENT:87, :197).

---

## 2. Wezly wspolnego stanu

### W1. Jeden mnoznik produkcji wsi - `MaterialLaw.ProdPostfix` (K:MaterialLaw.cs:166-212)
- Dzis w nim: kopalnie i las (`MineOutputMultiplier` x `MineOutputStep`, :175-176), `HistoricalPrices.BulkScale` (:178), `MineralOnce.Times` (:184), E4/5 spustoszenie
  `Devastation.YieldFactor` (:196-201), E4/7 rece L `FreeHands.YieldFactor` (:203-208), ksiega `OreLedger.NoteModel` (:209). Zima E4/1 - osobny postfiks na modelu Default
  (`WinterBite.VillageProdPostfix`, K:WinterBite.cs:164), przed nami (Priority.Last tutaj).
- Wejda: 6.1 (sztuka `iron` = lupa 15 kg - PULAPKA K13:228: `BulkScale(iron)` liczy sztuki; liczyc przez stale 100 kg rudy na lupe), 6.3 (krok x1.3 dla lnu, welny,
  skor - K13 132 "czynnik przed NoteModel"), 6.4 (zawor 134 "mnoznik w ProdPostfix"), 6.12 (hodowla wedlug popytu - konie to towar wsi), 6.18 (mieszanka okregu wedlug
  rodzaju wiosek).
- **Kazdy czynnik dopisywany jest jako (m - 1) x (1 + suma) - mnozniki sa przemienne, kolejnosc w kodzie nie zmienia wyniku; konflikt tylko tekstowy.** E4/11 umowilo
  sie "bez trzeciego czynnika w ProdPostfix" (PLAN-KODU-E411.md:343) - etap 6 je dopisuje swiadomie; kazdy jako osobna linia opisu (`TextObject`), bez progu 0.001
  przy czynnikach ludzi (jak E4/5).
- Progi etapu 4 czytane z czynnika, nie z wyniku: "wies z >= 1/3 uchodzcow - mnoznik <= 0.67" (P4 E4/5) - liczyc dalej z `Devastation.YieldFactor`, nie z produkcji
  (6.3 x1.3 zamaskowalby prog liczony z wyniku).
- Rece: zawor 134 mowi "rece z przycietego towaru nie robia na targ" (K13:239) - to NIE sa wolne rece E4/7 (pobor, hamulec): propozycja - zawor nie zmienia `FreeHands`.

### W2. Wartosc wyrobow w kiesie ludu - `OnItemProducedEvent`
- Nasluchy: `OreLedger.OnProduced` (K:ArmouryBehavior.cs:629), `TownFolk.OnItemProduced` (K:ArmouryBehavior.cs:630; K:TownFolk.cs:192-203 - kazdy wyrob miasta i zamku
  po cenie targu do `ProdV`; podstawa "place rzemiosla 35% wartosci wyrobu", KL L7, P5 R11). Wolaja: `WorkshopLaw` (:103 garbowanie/tkanie, :328 bron), `TownCrafts` (:336 skora,
  plotno, filc - **wyroby posrednie**), `TownFletchers` (:585), gra i BK.
- Dzis juz: skora z `TownCrafts` liczy sie w `ProdV`, a potem zbroja ze skory drugi raz (wsad w wartosci wyrobu). Etap 6 wydluza lancuchy: 6.1 przetop (zlom -> lupa,
  135) i sztaby w miescie, 6.5 chleb/piwo/deski/aksamit z wsadu (136), 6.3 receptury wedlug wartosci (131). Bez reguly placa liczy wsad 2-3 razy - kasy miast placa
  wiecej, niz powstaje wartosci (decyzja D6-1).
- Wytop przy kopalni (6.1) biegnie we wsi: `TownFolk.OnItemProduced` wsi nie liczy (`IsTown || IsCastle`, K:TownFolk.cs:196) - place wytopu musza miec osobne miejsce
  (D6-2): kiesa wsi (utarg taborow: wies 65%, P5 R13) albo `KMine` (dzis tylko kopalnie BK w miastach, K:BuildFunding.cs:93-98).

### W3. Zakupy przez kase miasta - `TownPurse.BuyFloor` (A1:TownPurse.cs:239; `SpendableTown` :254)
- A1 wpina te sama podloge w 8 sciezek: `WorkshopLaw` (A1:WorkshopLaw.cs:317-319 - wyrob czeka u warsztatu, place nie ida), `GarrisonArmory` (A1 :336 - sprzedaz nadwyzek
  zalog), `MaterialOrders` (:800 - kontrakty surowca 174), `CaravanBulk` (:383, :579, :589), `CaravanAmmo` (:297), `Drill` (:938 - ruda z musztry). Zamek - cala kasa jak dotad.
- Etap 6: 6.1 - ladunek "Iron Bloom" ok. 2.6 x drozszy (K13:221) - te same kontrakty i karawany potrzebuja wiecej nadwyzki; 6.13 - konie z zbrojowni AI na targ to ta sama
  sciezka co `GarrisonArmory:355` (nadwyzki zalog: kasy osad -> 1/3 panom, reszta sakiewkom); 6.7 - zakupy zalog i handel dzienny; 6.2 - wiecej rak = wiecej wyrobow
  do wykupienia z tej samej nadwyzki.
- **Regula dla etapu 6: kazda nowa sciezka, w ktorej kasa miasta placi za towar, przez `BuyFloor`** (jedna zasada, A1 R3/D4). Ryzyko: A1 zabiera warsztatom zbrojnym
  ok. 131 tys./d "z calej kasy" (recenzja A1 R1-1, z31A sr28) - jesli test zbioru A pokaze "brak zlota/kupca" > 0 w linii "Warsztaty", prog etapu 6 (zbroje) zalezy
  od nadwyzki kas, nie od surowca. Najpierw liczba z testu A, potem 6.2/6.8.

### W4. Konie
- **Zrodla z niczego, zostawione etapowi 6 przez etap 4:** kon najemnika z wzorca (E4/10 tylko etykieta - K:RecruitKit.cs:505, "do kroku sprzetu po etapie 4", recenzja
  E4/10 R2-4); dary startowe nowej druzyny x7 koni i zywnosc (E4/8 tylko licznik - K:ArmsFromPeople.cs:970, PLAN-KODU-E48.md:48, :248-249); juczne nowych karawan i taborow
  0.5 x ludzie (G:...PartyComponents\CaravanPartyComponent.cs:71-82; DK:9); konie przy zamianach ROT (HorseCensus "echo werbunku").
- **Ujscia:** mieszczanie kupuja i "zjadaja" konie z budzetu BK (DK:12 "miasta zjadaja konie"; 174 nie blokuje koni - K:ArmsLeaks.cs:85 `ItemType != Horse`); w E5/0 ten wydatek
  idzie do grupy "bron-konie-zbytki" kiesy (E50 3.1 `SpendGrp[2]`) - po B1 kupuje je kiesa ludu. Sprzeczne z "kon ginie tylko zabity" (Jeff 07.10, DK P2) - D6-3.
  LegendaryLaw kasuje ok. 9.5 egzotycznych wierzchowcow/d (RA6 rozdz. 1).
- **Lup:** Jeff 07.10 "ocalaly kon = lup zwyciezcy (takze AI), przegrani odkupuja" (SP:423). A6 (R20) ustala zasade dla zlota: lup tylko z tego, co mial pokonany
  (A6:BattlePlunder.cs, migawka po 3.2). Konie jako lup - ta sama zasada (zadnego konia z niczego dla zwyciezcy); "przegrani odkupuja" = przelew sakiewka/kiesa
  pokonanego -> zwyciezca w tej samej ksiedze co 3.2/A6/`CasualtyLoot` (K:CasualtyLoot.cs:12-31).
- **Pasza (E4/1, wariant a Jeffa 11.10 06:50):** kazdy kon partii je po glowie (K:WinterRealm.cs:287-336; zima +0.5 pelnej paszy = 2.5 x czlowiek). 6.14 z 7.4% do ok. 20%
  jazdy to ok. +24 tys. koni pod jezdzcami (190.9 tys. ludzi) [S]; w zimie ok. +60 tys. "czlowiekodob" paszy na dobe wobec ok. 106 tys. ludzi w partiach - rzad +30-55%
  jedzenia partii zima [S, gorna granica - zalezy, ile jazdy w partiach, a ile w zalogach]. 6.16 (zapasowe konie lordow, juczne) doklada. Prog etapu 4 "zadna warownia AI
  nie glodzi przed 30. doba zimy" i "lordowie w glodzie" (z31B: 20) - do powtorzenia.
- **Pieniadz:** zakupy Stajni AI (`GiveGoldAction` lord -> osada, K:Stables.cs:842, :861) nie sa wpiete w E5/0 (lista 20 wpiec E50 2.2); kon ochotnika kupuje notabl
  (VolunteerKit; K:RecruitCost.cs:38-48) - po A3 kiesa notabla ma pasmo 4 500-10 500 przez kase miasta (P5 R10), wiec 6.14 (wiecej konnych ochotnikow) ciagnie z tego pasma;
  `MountedWage` - wiekszy zold konnych -> budzety rodow (progi etapu 2: wojsko lordow, bankruci) i kiesy panow (prog E5: mediana kiesy panow zamkow -10%/28 dob wojny).
- **Wsie zamkowe:** 47 z 71 stadnin jedzie do zamkow (ok. 54% wartosci produkcji wsi zamkowych). 6.12 (mniej koni przy malym popycie) obniza utarg tych wsi i kase zamkow
  -> prog E5 B3 "udzial panow zamkow w D stalym 30-35%" (dzis 24.3%) i 5.7 (wyrownanie); 6.14 dziala odwrotnie (wiecej zbytu koni). **Kolejnosc: 6.14 przed 6.12.**

### W5. Ludzie do pracy i "kraina"
- Rece warsztatow miast: `WorkshopLaw.TownHands` = `WorkshopHandsPer1000People` x ludzie rynku (`PopulationLaw.TablePeopleOf` - od E4/4 ksiega ludzi; K:WorkshopLaw.cs:784-830).
  E4/8 (migracja do miast, karawany z ludzi miasta) zmienia te liczbe; 6.2 podnosi stawke (174 S4: 0.10 -> 0.15) - rece rzemiosla to udzial ludzi, nie zdjecie z DOM.
- Kopalnie: w ksiedze ludzi nie ma konta pracy w kopalni (grep `kopal|mine` w PeopleBook*, FreeHands - brak); wies gornicza kopie swoimi ludzmi (towar wsi x L). "Wiecej
  rak do kopaln" (6.2) to albo wieksza wydajnosc wsi (czynnik W1), albo ludzie z DOM (wtedy w `FreeHands` jak sluzba) - D6-2. Zelazne Wyspy: niewolni ksiegi (E4/9 konto
  NIEWOLNI, `ThrallLaw`) juz podnosza prog wolnych rak wysp `IronF` (K:FreeHands.cs:47-52) - niewolni w kopalniach musza byc tymi samymi ludzmi (bez drugiego liczenia).
- Kraina: jedna definicja w etapie 4 - `FreeHands.RealmOf` = `PeopleBook.CultureAtCalibration` (kultura z dnia kalibracji, w zapisie; K:FreeHands.cs:80-99).
  `WorkshopLaw.Candidates` uzywa `town.Culture` (zmienia ja podbicie BK) i przy pustej puli bierze obce (:525-530). 6.19 "wedlug kultury swojej krainy" - ta sama
  definicja co E4/7 (D6-6). Id kultur ROT w kodzie: `battania` = Polnoc, `vlandia` = Zachod (Lannisterowie), `reach` osobno, `sturgia` = Zelazne Wyspy, `khuzait` = Dothrakowie,
  `aserai` = Dorne (K:PopulationLaw.cs:156, K:ClanBudget.cs:50, K:CrownGifts.cs:22-27).
- Klimat: `VillageClimate` (T8) ma juz krok etapu 4 (D.5 Skagos owce, E4/1) - 6.9/6.10 edytuja ten sam plik i te sama tabele (tekstowo).

### W6. Inne styki (mniejsze)
- **Spichlerz i zywnosc warowni (E4/6, E4/11, E5/B4):** plon spichlerza = 1.2 x ludzie x q, q z modelu ZYWNOSCI wsi (K:Famine.cs:398, :532), nie z towarow; zywnosc warowni od
  wsi - postfiks E4/11 na `CalculateTownFoodStocksChange` (K:VillageBook.cs:1241-1259), ta sama funkcja wejdzie w targ przy zamku (P5 B4 R17). 6.18 (mlyn = zboze, rybacka =
  ryby) zmienia towar na targ, nie q - D6-5. 6.5 (chleb, piwo z zboza z polki) zabiera jedzenie, ktore dzis wchodzi do zapasu miasta przez `SoldItems` (E5/0 linia
  "Jedzenie z targu", B4 i C2 jej uzywaja) - piwo nie jest jedzeniem gry.
- **Naprawy (6.17) i markietani (A3):** A3 `CampReserve` chroni zalegle naprawy przez `AiWear.OutstandingCost(mp, null)` - bez miasta, wiec bez szacunku materialu
  (A25:MenPurse.cs:364-375). 6.17 zmienia koszt naprawy w tej samej funkcji - jedna zmiana poprawi obie strony, ale w polu material dalej poza rezerwa (opisac w 6.17).
  Zaplaty za naprawy do kasy miasta (K:AiWear.cs:410, :450; K:TroopSelfMend.cs:154, :217) nie sa wpiete w kiese (E50 2.2) - to praca kowali (place rzemiosla) - B1 albo 6.17, jedno miejsce.
- **Karawany:** `CreateCaravanParty` - E4/8 bierze straz z ludzi miasta (K:ArmsFromPeople.cs:971), A3 bierze kapital z kiesy notabla/kasy (P5 R10); 6.16 (juczne z targu)
  to trzeci wpis na tym samym celu: juczne kupione z kapitalu karawany (jednorazowo przy tworzeniu), nie z niczego.
- **Koszyk kiesy (B1):** wies kupuje "sol, narzedzia, sukno, garnki, piwo" (P5 R13); 6.1 (narzedzia z lup drozsze, K13:223 "ok. 50-55 d") i 6.5 (piwo, chleb) zmieniaja ceny
  koszyka -> progi E5 "95% stac na jedzenie, 98% na pelny koszyk". 6.3 krok popytu - D6-4.
- **Lup towarow (6.7 "ruda rozbitych partii"):** towar z rozbitej partii - ta sama zasada co zloto A6 (zwyciezca bierze to, co bylo; reszta do domu/osady), nie znika.

---

## 3. Styki pozycja po pozycji

Kolumna "Kolejnosc": **teraz** = kod od K (7658247) bez czekania; **po A** = po scaleniu zbioru A etapu 5 (A1-A6) do galezi; **po B1** = po kodzie kiesy ludu;
**po testie B** = kalibracja po liczbach z testu zbioru B.

| # | Etap 4 | Etap 5 | Kolejnosc |
|---|---|---|---|
| 6.1 wytop + przetop (D, NK) | W1: `BulkScale(iron)`, L i spustoszenie mnoza lupy jak rude; wegiel z lasu okregu (VillageWoodlot) | W3: wartosc ladunku x2.6 w sciezkach `BuyFloor` z ruda (`MaterialOrders`, `CaravanBulk` x3, `Drill`); W2/D6-1, D6-2 place wytopu; `StartStock`/zapas startowy (NK) | po A; place po B1 (hak) |
| 6.2 rece i kopalnie (D) | W5: skad ludzie (D6-2), `TablePeopleOf` (E4/4, E4/8), NIEWOLNI/`IronF` (E4/9) | W3 (wiecej wyrobow z tej samej nadwyzki), `KMine` | po 6.1 i B1; liczby z testu A |
| 6.3 x1.3 len/welna/skory + wsad wedlug wartosci (S) | W1 (czynnik), zima E4/1 tnie ten sam towar | D6-4 (krok popytu), utarg wsi 65/35 (B1 R13), zatkane wsie zamkowe (prog B4), W2 (131) | po B1 |
| 6.4 zawor zamiast kasowania BK (S) | W1; "rece" zaworu poza `FreeHands` | progi "miasta kupuja >= 98% plonu", "zatkane <= 5.6%"; utarg wsi mniejszy | kod po W1-W2, kalibracja po testie B |
| 6.5 chleb/piwo/deski, srebro, rzemioslo wsi BK (D, warunkowo) | W6: zboze z polki (spichlerz E4/6 i zapas warowni przez `SoldItems`) | D6-1 (posrednie), koszyk kiesy (chleb, piwo), srebro = moneta (P5:689 "mennica przy > 2%/rok"; prog "zloto swiata < 0.1%/28 dob") | po B1 i po wynikach E4/6 |
| 6.6 ksiega uzbrojenia wedlug zrodla (M) | - | nasluch obok `TownFolk.OnItemProduced` (tylko odczyt) | teraz (E6/0) |
| 6.7 dziury po 174 (S) | E4/8 tabory i karawany z ludzi (towar bez wozu) | W3 (zakupy zalog `AiGear:747` juz w E5/0), A6 (towar rozbitych - jak zloto) | po A i po 6.1 (jednostka rudy) |
| 6.8 warunkowo po tescie (S) | - | "placa miasta w cenie" = to samo zjawisko co place B1 (R11) - jedna regula | po tescie etapu 6 i po B1 |
| 6.9 warsztaty wedlug surowca i lore (S, NK) | W5: `VillageClimate` (E4/1 D.5) | place warsztatow towarowych (`WorkshopTrade:329/:503` wpiete E5/0) - tylko tekst | teraz |
| 6.10 klimat wsi dla warsztatow (M, NK) | jw. | - | teraz (z 6.9) |
| 6.11 konie: linia, ginie tylko zabity, lup (S) | W4: kon najemnika z wzorca (E4/10), dary startowe x7 (E4/8) | W4: A6 (lup z pokonanego), "przegrani odkupuja", D6-3 konsumpcja koni w koszyku | linia teraz (E6/0); regula po A |
| 6.12 hodowla wedlug popytu + 71 stadnin (S, NK) | W1 (konie - towar wsi) | wsie zamkowe 54% wartosci -> kasy zamkow, prog B3 30-35%, 5.7 | po 6.14; kalibracja po testie B i po biegu rocznym (L728) |
| 6.13 konie poleglych ze zbrojowni AI na targ (M) | - | W3: ta sama sciezka co `GarrisonArmory:355` (`BuyFloor`) | po A |
| 6.14 rycerz z koniem, jazda ok. 20% (S) | W4: pasza zima (progi zimy E4), konni rekruci z ludzi (E4/8 `TakeLevy` - czlowiek z DOM, kon z polki) | W4: notable (A3), Stajnia AI bez wpiecia w kiese, `MountedWage` -> budzety rodow | kod po 6.11; wpiecia po B1; cel 20% po testie B |
| 6.15 konie z lupu/zbrojowni nie niosa piechura (M, blad) | - | - | teraz |
| 6.16 juczne z targu, zapasowe konie, udzwig (S) | W4: pasza; E4/8 karawany (`CreateCaravanParty`) | A3 kapital karawan (ten sam cel); udzwig czyta `MarchPace` (etap 7 - tempo) | po A (A3) i po 6.11 |
| 6.17 naprawy: material wedlug rodzaju (S) | - | W6: A3 `CampReserve`/`OutstandingCost`; zaplata kowalom poza kiesa | po B1 (wpiecie zaplaty) |
| 6.18 wioski poboczne wedlug rodzaju (S) | W1 + `VillageBook` (`Kind[]`, K:VillageBook.cs:69, :225; plon okregu bez drugiego mnoznika - PLAN-KODU-E411.md:60-63), spichlerz (D6-5) | utarg wsi (towar inny, ta sama droga) | teraz (po tescie koncowym E4 - liczby wiosek) |
| 6.19 kucie wedlug kultury krainy (M) | W5: kraina = `CultureAtCalibration` (D6-6) | `WorkshopLaw` - A1 zmienia tylko :317-319 (tekst daleko) | teraz |

---

## 4. Decyzje wymuszone przez styki (propozycje; D6-1, D6-3, D6-4 - do Jeffa przed odpowiednimi paczkami)

- **D6-1 (przed B1 / przed 6.1, 6.5) - place rzemiosla od czego?** KL L7 "35% wartosci wyrobu" liczy kazdy wyrob, takze posredni (skora, plotno, sztaby, lupa, chleb).
  Propozycja: **35% wartosci dodanej** (wyrob - wsad po cenie targu z chwili produkcji); dla wyrobow bez wsadu (strzaly ze stanu, warsztaty z dlugiem surowca) - jak dzis.
  Alternatywa: tylko wyroby koncowe (lista posrednich: skora, plotno, filc, sztaby, lupa, wegiel). Rozstrzyga projekt B1 (to jego regula), etap 6 tylko z niej korzysta.
- **D6-2 (przed 6.1/6.2) - kto pracuje przy wytopie i w kopalni?** Propozycja: wytop przy kopalni to praca wsi gorniczej i lasu okregu (jak K13 133 - "praca lasu okregu,
  nie towar"), wynik jako towar wsi (utarg 65/35 przez B1 R13), bez nowego konta ludzi; "wiecej rak" (6.2) = wiecej wsi gorniczych/wydajnosci (czynnik W1) i rak rzemiosla
  miast (`WorkshopHandsPer1000People`), nie zdjecie z DOM. Niewolni w kopalniach Wysp i Essos - tylko jesli Jeff chce: wtedy z konta NIEWOLNI tej krainy (E4/9), w `IronF` bez zmian.
- **D6-3 (przed 6.11) - mieszczanie "zjadaja" konie.** "Kon ginie tylko zabity" (Jeff 07.10) a BK/gra co dobe usuwa konie kupione przez mieszczan (DK:12). Propozycja: kon kupiony
  przez mieszczan **nie znika** - zostaje w miescie jako zapas pociagowy i wraca na targ (albo: konie poza koszykiem kiesy, budzet tej kategorii do innych zbytkow).
  Skutek dla B1: grupa "bron-konie-zbytki" bez koni (E50 3.1). Ujscie LegendaryLaw (egzotyczne wierzchowce) - osobno w linii "Konie".
- **D6-4 (przed 6.3) - krok popytu x1.3 z paczki 132.** 132 mnozy popyt i budzet mieszczan (`HistoricalPrices.DemandPostfix/BudgetPostfix`, K:HistoricalPrices.cs:816), a E5/0
  ma za nim postfiks `Priority.Last` (K:TownFolk.Probe.cs:114-115, :187), B1 robi z budzetu kotwice kiesy. Po B1 budzet bez pokrycia = zakup z niczego. Propozycja: **6.3 tylko
  produkcja x1.3 (towary z odbiorca)**; popyt rosnie sam przez kiese (wiecej towaru -> nizsza cena -> kiesa kupuje wiecej) - bez mnoznika budzetu.
- **D6-5 (przed 6.18) - mieszanka okregu a spichlerz.** Propozycja: rodzaj wioski zmienia tylko TOWAR na targ (model produkcji wsi); spichlerz E4/6 i zywnosc warowni E4/11
  dalej od ludzi (1.2 x L x q) - rybacka wioska nie zywi warowni drugi raz.
- **D6-6 (przed 6.19) - kultura krainy.** Propozycja: `PeopleBook.CultureAtCalibration` regionu warsztatu (ta sama co wolne rece E4/7); bez obcego zapasu (dzis :530 "pool = foreign"):
  pusta pula -> tylko neutralne, a bez neutralnych linia nic nie kuje (licznik w logu). Unikaty (np. `ramsay_helmet`) juz poza warsztatem (`RotUniques`, `ArmsPricing.IsUnique`
  K:ArmsPricing.cs:85-93).

---

## 5. Podzial etapu 6 na paczki

Numeracja robocza E6/x (numery paczek CHANGELOG - wolne od 206 wzwyz, do sprawdzenia grepem przy kodzie). Kazda paczka: wlasny wylacznik, wpis CHANGELOG, commit.

| Paczka | PL | Co | Wiel. | NK | Zalezy od | Baza kodu / kiedy |
|---|---|---|---|---|---|---|
| **E6/0** | 6.6, 6.11 (linia) | POMIAR (gra bez zmian): ksiega uzbrojenia wedlug zrodla; linia "Konie" - kazde wejscie i wyjscie konia z przyczyna (stadniny, juczne z niczego, dary startowe E4/8, wzorzec najemnika E4/10, zamiany ROT, lup, konsumpcja miast, LegendaryLaw, smierc w bitwie); rozbicie czynnikow ProdPostfix w linii | S | nie | - | **teraz** (K); jedzie w tescie zbioru A albo B etapu 5 |
| E6/M1 | 6.19 | kucie wedlug kultury krainy (D6-6) | M | nie | - | **teraz** |
| E6/M2 | 6.9 + 6.10 | warsztaty wedlug surowca i lore (Dorne: rumaki, winnice/oliwki - odp. Jeffa 08.10 I/J; smoki, mamuty, wielblady; blad "strawy Polnocy"), klimat dla warsztatow | S | tak | E6/M1 (ten sam przeglad kultur) | **teraz** |
| E6/K0 | 6.15 | blad: konie z lupu i zbrojowni nie niosa piechura | M | nie | - | **teraz** |
| E6/W2 | 6.18 | wioski poboczne wedlug rodzaju (D6-5) | S | nie | E4/11 (wyniki testu koncowego E4) | **teraz** (K) |
| E6/K1 | 6.11 | kon ginie tylko zabity, ocalaly - lup zwyciezcy, przegrani odkupuja; konie z niczego zamkniete (wzorzec najemnika, dary startowe x7 - z polki siedziby albo bez) (D6-3) | S | nie | E6/0, A6 | po A |
| E6/K2 | 6.13 | konie poleglych jezdzcow ze zbrojowni AI na targ (sciezka `GarrisonArmory`, `BuyFloor`) | M | nie | E6/K1 | po A |
| E6/K5 | 6.16 | juczne z targu (z kapitalu karawany/taboru), zapasowe konie lordow (srednia cena konia bez smokow i rydwanow - DK), udzwig (uwaga `MarchPace` - etap 7) | S | nie | E6/K1, A3 | po A |
| E6/R1 | 6.1 | wytop przy kopalni ("Iron Bloom" zamiast rudy, K13 133) + przetop (zlom -> lupa, K13 135; `SmeltTab` gracza) | D | tak | A1 (tekst w 6 sciezkach), D6-1, D6-2 | po A; place - hak do B1 |
| E6/R3 | 6.7 | dziury po 174: narzedzia, ruda rozbitych partii (jak A6), uprzaz bez kupca, towar bez wozu | S | nie | E6/R1 | po A + R1 |
| E6/W1 | 6.3 | x1.3 len/welna/skory (tylko produkcja - D6-4) + wsad wedlug wartosci (K13 131) | S | nie | B1 (kotwica), E6/W2 (ten sam ProdPostfix - tekst) | po B1 |
| E6/N1 | 6.17 | naprawy: material wedlug rodzaju, gracz ta sama regula, AI naprawia tylko to, co nosi; zaplata kowalom do kiesy (jedno miejsce z B1) | S | nie | B1, A3 | po B1 |
| E6/R2 | 6.2 | wiecej rak i kopaln (D6-2; `WorkshopHandsPer1000People`, wsie gornicze) | D | nie | E6/R1, B1, liczba "brak zlota/kupca" z testu A | po B1 |
| E6/K3 | 6.14 | rycerz z wlasnym koniem, jazda AI ok. 20% (Dothrakowie konni, Zelazne Wyspy malo - 175 `Army175IronbornFoot`); kon z polki, nie z niczego; wpiecie Stajni AI w kiese | S | nie | E6/K1, E6/K2, B1 | kod po B1; cel 20% po testie B |
| E6/K4 | 6.12 | hodowla wedlug popytu + przeglad 71 stadnin; najpierw "na sucho" (log), czynna po biegu rocznym | S | tak | E6/K3 (popyt), W1 | kod po K3; kalibracja po testie B i biegu rocznym |
| E6/W3 | 6.4 | zawor (K13 134) zamiast kasowania BK | S | nie | E6/W1, E6/W2 (ksiega towarow) | kod po W1; kalibracja po testie B |
| E6/W4 | 6.5 | WARUNKOWO: chleb/piwo/wino/deski (136), srebro (137), rzemioslo wsi BK (138) | D | nie | B1 (D6-1), E4/6 (bilans zboza), wynik testu | po tescie etapu 6 albo w nim, jesli warunek spelniony |
| E6/R4 | 6.8 | WARUNKOWO: tarczownicy, placa miasta w cenie (= B1 R11), strzaly, helmy, "dobytek osady" | S | nie | test etapu 6, B1 | po tescie etapu 6 |

Wielkosci: D - 6.1, 6.2, 6.5 (warunkowa); M - 6.15, 6.13, 6.19; reszta S. Bez warunkowych: 15 paczek (ok. 3 noce kodu przy 4-5 wgraniach na noc - PL zakladal 3-4).

---

## 6. Co rownolegle

Cztery lancuchy niezalezne od siebie (rozne pliki), wewnatrz lancucha po kolei:
- **M (rzemioslo i lore):** M1 -> M2. Pliki: `WorkshopLaw.Candidates`, `WorkshopTrade`, `VillageClimate`, dane lore. Bez plikow porcji A/B.
- **K (konie):** E6/0 -> K0 -> K1 -> K2 -> K5 -> K3 -> K4. Pliki: `Stables`, `HorseCensus`, `CasualtyLoot`/`BeatenPurse` (A6 dopisuje 1 linie w `BeatenPurse` - tekst),
  `RecruitKit`, `ArmsFromPeople` (dary startowe), `GarrisonArmory`, `MountedWage`, `WinterRealm` (tylko odczyt paszy).
- **R (ruda/zelazo):** R1 -> R3 -> R2 (-> R4). Pliki: `MaterialLaw`, `HistoricalPrices`, `WorkshopLaw.Needs`, `WorkshopTrade`, `OreLedger`, `StartStock`, `ArmsPricing`, `CaravanBulk`,
  `MaterialOrders`, `Drill`, `OutlawLaw`, `ArmsScrap`, `MendMaterial`, `TownFletchers`, `SmeltTab` (ok. 25 miejsc z "iron"). A1 zmienia sasiednie linie w 4 z nich - kod R1 od galezi ze scalonym A.
- **W (wies i towary):** W2 -> W1 -> W3 (-> W4). Pliki: `MaterialLaw.ProdPostfix`, `VillageBook`, nowy plik zaworu, `TownCrafts`/receptury.
- **Wspolny plik W1:** `MaterialLaw.ProdPostfix` - R1 (iron), W1, W2, W3, K4. Czynniki przemienne, wiec mozna kodowac rownolegle, ale **scalac raz, w kolejnosci R1 -> W2 -> W1 -> W3 -> K4**,
  z jedna linia rozbicia czynnikow (E6/0). Drugi wspolny: `GarrisonArmory` (K2 i A1).

Os czasu wobec etapu 5:
1. **Teraz (test koncowy E4 trwa, zbior A czeka):** E6/0, M1, M2, K0, W2 - od K (7658247). Rownolegle do kodu B1 (rozne pliki; jedyny wspolny: `ArmouryBehavior`/`SubModuleMain`/`Settings` - tekst).
2. **Po scaleniu zbioru A (A1-A6):** K1, K2, K5, R1, R3.
3. **Po kodzie B1:** W1, N1, R2, K3 (wpiecia kiesy: Stajnia AI, naprawy, place wytopu wedlug D6-1/D6-2).
4. **Po tescie B:** kalibracja K3 (cel 20%), K4 (na sucho -> czynna po biegu rocznym), W3. Warunkowe W4, R4 - po tescie etapu 6.

---

## 7. Test etapu 6 (osobno po etapie 5)

- **E6/0 w tescie etapu 5** (zbior A albo B), bo nic nie zmienia w grze - jak E5/0 w tescie E4. Daje stan wyjsciowy: linie "Konie" i ksiega uzbrojenia wedlug zrodla (prog
  "kazdy znikajacy kon ma przyczyne" dzis nie do zmierzenia - RA6 rozdz. 1).
- **Test etapu 6: po wgraniu zbioru B etapu 5** (kiesa ludu czynna - inaczej W2/W3 i D6-1/D6-4 mierzylyby stan, ktorego nie bedzie). 120 dob NOWEJ kampanii (NK: 6.1, 6.9+6.10, 6.12),
  autotest 40 dob + zapis 9 dob przed biegiem (standard P5), jeden zbiorczy test dla wszystkich paczek bez warunkowych (zasada Jeffa 11.10 03:05); kazda paczka z wylacznikiem -
  przy niezdanym progu bisekcja wylacznikami, nie nowy kod.
- **Progi etapu 6 (PL "Skonczone, gdy"):** cos na tulowiu >= 75% (dzis 93.6% - RA6; liczyc tez szczebel, 74.5%), skory i plotna nie ubywa > 10/d, lnu i welny nie przybywa
  3 miesiace z rzedu (dzis NIE - RA6), w linii "Konie" kazdy znikajacy kon z przyczyna (E6/0/K1). Dodatkowo: jazda AI ok. 20% swiata [cel K3; progi Dothrakow i Zelaznych Wysp - propozycja do projektu K3, np. > 80% / < 10%].
- **Regresja etapu 4 (bieg B z zima od 8. doby, jak z31B):** lordowie w glodzie i bez jedzenia nie wiecej niz w tescie koncowym E4, zadna warownia AI nie glodzi przed 30. doba zimy
  (pasza koni K3/K5); mnoznik plonu wsi z 1/3 uchodzcow <= 0.67 liczony z czynnika; przyrost zalog bez werbunku 0.
- **Regresja etapu 5:** miasta kupuja >= 98% plonu, zatkane wsie zamkowe <= 5.6% lato / <= wynik zimy, udzial panow zamkow 30-35%, mediana kiesy panow zamkow -10%/28 dob
  wojny najwyzej, zloto swiata < 0.1%/28 dob, "brak zlota/kupca" warsztatow (A1) nie wyzej niz w tescie B, stac na jedzenie >= 95% / koszyk >= 98% (6.1 narzedzia, 6.3).
- **Hodowla (K4) czynna dopiero po biegu rocznym** - L728 pokazuje odwrocenie popytu koni po d360; w tescie 120 dob K4 tylko "na sucho" (linia, ile by przycial).

---

## 8. Ryzyka i czego nie sprawdzilem

- Nie czytalem kodu BK konsumpcji koni (DK opisuje skutek: "miasta zjadaja konie") ani `VillagerPartyComponent` (juczne taborow wsi) - do E6/0 przed D6-3.
- Szacunek paszy przy 20% jazdy [S] zaklada, ze nowa jazda jest w partiach (garnizony jedza z zapasu miasta, nie z modelu partii); rzad wielkosci, nie wynik.
- `PeopleBook.CultureAtCalibration` w starym zapisie - zapas `Measure40.CultureAtStart` (sesja); 6.19 w zapisie sprzed E4 widzi kulture z poczatku sesji.
- Porcja B nie ma jeszcze planu kodu - miejsca wpiec kiesy (Earn/PayForGoods) moga sie przesunac; lista "niewpiete" (Stajnia AI :842/:861, naprawy :410/:450/:154/:217,
  `GarrisonArmory:355`) sprawdzona tylko w K (grep `TownFolk.Note|Sale`).
- Numery linii A1/A25 z hunkow diffu wobec 392c519 (+-3); K - sprawdzone w drzewie.
