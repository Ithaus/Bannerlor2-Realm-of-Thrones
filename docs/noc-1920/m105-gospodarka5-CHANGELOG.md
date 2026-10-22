## 2026-10-21/22 (m105-gospodarka5: biedne wsie - pan nie zabiera wsi ponizej skrzyni dzialki z wozu ani czynszu z pieniedzy skrzyni)

**Mod:** Armoury (+ `tools/sprawdz_logi.py`) | **Galaz:** `w-toku/m105-gospodarka5` (od `fd388555` = n33: WGRANIE 102 + gospodarka cz.4 + miasta + drobne + dziennik + optym + czcionki) | **Status:** DO SPRAWDZENIA w tescie koncowym (nic nie wgrane, gry nie uruchamialem; build 0 bledow, progi sprawdzone testami syntetycznymi)

Jeden nowy klucz MCM `DsLordReliefBelowChest` ("Ds Lord Relief Below Chest", grupa "Medieval dues: villages (DS F1, 17.10)"), domyslnie TAK. Klucz NIE = kod jak w `fd388555` (wylacznik opisany nizej). Nowego klucza nie ma w `Armoury.json` Jeffa - dziala domyslne TAK.

---

### 0. Skad zmiana (diagnoza gosp5, skrypty `a1..a6` w scratchpadzie gosp5)

- Prog M3W-2 (wsi z kiesa < 1000 zl, srednio 7 dob): n32 14-29-20 12.8% (73 z 569 wsi), n33a 103a 11.1% (64 z 571), n31 18.3%; cel <= 5% (P0 1.0% - bieg bez KS, tanszej gospodarki nie da sie przeniesc). Wszystkie przebiegi rosna w ostatnich 28 dobach.
- 64 z 73 biednych wsi to Westeros (Korona Krola 20/50, Polnoc 12/57, Zelazne Wyspy 9/28, Nocna Straz 7/11). 71 z 73 jest w stanie "normalna" - nie wojna ani rabunek. Wielkosc tez nie (mediana ognisk 350 jak w reszcie).
- Przeplywy na wies na dobe (28 dob): biedne - utarg do kiesy 79.8, zakupy 90.7, czynsz 17.4, budowy 0.7 -> bilans -13.9 zl/d; reszta +3.3 zl/d.
- Dlaczego oszczednosci schodza do zera: ziarno i sol sa poza budzetem (`ZcLadder` szczebel 1 `NoCap`), cena polki W ok. 2.2 x Planu; budzet liczony z prawa (`VilBasketMoney`), nie z kiesy; w Koronie pan bierze przy wozie ok. 45% oddanego; czynsz idzie pierwszy z calej kiesy (G5 chroni tylko koszyk 2 dob). Ziarno i sol same przekraczaja budzet w 36 z 52 biednych wsi W (analiza G4 cz.4).
- Odrzucone: ograniczenie ziarna do budzetu (glod), rozszerzenie G3 (tylko 16 z 73 wsi ma swoje ziarno), kupno ziarna u sasiedniej wsi (nowa droga handlu - rusza polki i m105miasta), wariant stopniowy ulgi (3.2-3.3%, ale nachylenie +0.13..+0.26 wsi/d, wyzszy koszt pod koniec i czas wozu kazdej wsi w zapisie).

### 1. Zmiana - klucz `DsLordReliefBelowChest`

Slowami gracza: pan nie bierze swojej dzialki z wozu ani czynszu z zapasu wsi, dopoki wies nie odbuduje zapasu na czarna godzine.

**(1) Ulga przy wozie - `VillageTakings.LordRelief` (nowa metoda).**
- Krok okna powrotu wozu w `EnterPostfix` po `F5WoodCredit` (ulga nie splaca kredytu kupcow za drewno), przed `CartCsv` (csv `woz_netto_do_kiesy` zawiera ulge). Po daninach DS - ich podstawa bez zmian.
- Tylko woz w swojej wsi (`Here == null`), tylko wies DS (`DsVillage.On`), tylko gdy woz cos oddal.
- `got` = przyrost `TradeTaxAccumulated` wsi w tym oknie (dzialka pana z TEGO wozu - po BK, 112, F4b, regale i pracach okregu). `brak` = floor(skrzynia - kiesa). `zwrot = min(got, brak)`; licznik pana -= zwrot, kiesa += zwrot (z licznika schodzi najwyzej tyle, ile kiesa przyjela - jak F4b).
- Skrzynia: `DsVillage.ReliefChest` - skrzynia policzona dzis w kroku 4 (`ChestToday`), a przed blokiem doby - `DsVillage.Chest` (ta sama regula: DsVillageChestDays x (koszyk + czynsz)). Wies nieskalibrowana (skrzynia 0) - bez ulgi.
- Platnik: pan z biezacego zysku tego wozu (licznik nieodebranego podatku z tego okna), nigdy z oszczednosci ani z wczesniejszych wozow. Zloto nie powstaje z niczego. Przelew w otwartym oknie ksiegi pieniadza (kiesa rosnie, licznik maleje o to samo) - bez "zniklo", jak F4b.
- Ulga konczy sie sama, gdy kiesa dojdzie do skrzyni.

**(2) Czynsz - `DsVillage.RentPay`.**
- Rata dnia tylko z kiesy ponad skrzynie: `pc = max(0, floor(kiesa - skrzynia))`; gdy `pc < rata` (cale zl raty, `dayR = floor(rata)` - takze przy kiesie mniejszej od raty albo 0, poprawka po recenzji), roznica `dayR - pc` jest darowana bez zaleglosci (jak przy rabunku) - doliczona do kwoty "wstrzymane" linii DS (NDS-6c bez zmian), bez zmiany nawiasu z liczba wsi. Regula stoi przed G5 (skrzynia > koszyk 2 dob, wiec G5 ponizej skrzyni nic juz nie tnie).
- Niezaplacona reszta (ulamek zlotowki, G5) idzie do ksiegi jak dotad: `shortDay = rata - zaplacone - darowane do skrzyni` (ponizej skrzyni zawsze < 1 zl).
- Rata zaleglosci tylko z `kiesa - rata dnia - max(koszyk, skrzynia)`.
- Skrzynia liczona tylko, gdy jest rata albo zaleglosc (bez kosztu dla wsi bez czynszu).

**(3) Linie logu.**
- `Daniny wsi (DS)`, tylko przy kluczu TAK, przed "| kontrola": `| m105 gosp5 (Lord Relief Below Chest TAK): ulga pana przy wozie R zl w N wozach (W wsi), czynsz darowany do skrzyni D zl (V wsi)` (liczniki od poprzedniej linii; `NoteRelief`, `_lrZl/_lrN/_lrVil/_rChest/_rChestN`, zerowane w `ClearCounters`).
- `Akumulator kies`: segment "m105 wsie biedne (kiesa < min(1000 zl, skrzynia))" takze przy tym kluczu (dotad tylko przy G4 albo G5).
- "pan i majatki (bez zmian)" w linii DS zostaje BRUTTO (liczone w `DuesAtCart` przed ulga) - netto = minus R z segmentu.

**Sprawdzone przy wdrozeniu (pytania projektu):**
- Zaden prog nie liczy liczby wsi w nawiasie "wstrzymane (N: rabunek, oblezenie, glod)" - `NDS_RX["rent"]` uzywa tylko kwoty (grupa 10, NDS-6c); grupy 11-14 nieuzywane.
- "kontrola: platnicy - odbiorcy" = `_payOut - _payIn` (dziesiecina i korona) - przeplyw licznik pana -> kiesa wsi go nie dotyka.
- Alfa Planu wsi DS liczy sie z prawa (`VilBasketMoney` - tylko `a0` = oddane), wiec ulga dopisana do "do kiesy" (`NoteV`, jak F4b) nie zmienia alfy ani budzetu drabiny 1.

### 2. Wylacznik (dokladnie)

MCM "Ds Lord Relief Below Chest" = NIE (pole `DsLordReliefBelowChest = false`). Dziala od nastepnego wozu i nastepnej doby, bez wczytywania; w zapisie nic nowego nie ma.
- `VillageTakings.LordRelief`: `return` w pierwszej linii (przed jakimkolwiek odczytem) - okno powrotu wozu jak w `fd388555` (kolejnosc i liczby: 112, F4b, regale, prace, SideFief, DS, F5, CartCsv).
- `DsVillage.RentPay`: blok skrzyni nie biegnie (`chestR = -1`, `chestRemit = 0`, skrzynia nieliczona); `shortDay = d - payDay - 0`; rata zaleglosci z `kiesa - rata - koszyk` (`keep = basket`) - liczby 1:1 jak w `fd388555`.
- Linia `Daniny wsi (DS)`: bez segmentu "m105 gosp5" - jak w `fd388555`. Liczniki ulgi zostaja na 0.
- `Akumulator kies`: "m105 wsie biedne" tylko przy Ks Village Chest Refill albo Ds Rent After Bread - jak w `fd388555`.
- Czego wylaczenie nie cofa: ulga juz oddana zostaje w kiesach wsi (pieniadz realny, zaplacil pan), czynsz darowany do skrzyni nie wraca do ksiegi zaleglosci (darowany jak przy rabunku).
- Narzedzie: bez segmentu `--grupa m105gosp5` daje M5G5-0 INFO, a M5G-8 grupy m105gosp4 liczy jak w `fd388555` (wynik na wycinku 103a identyczny z narzedziem z `fd388555`: m105gosp4, m103wsie, nds - 0 roznic).

### 3. Progi: nowa grupa `--grupa m105gosp5` (+ poprawka M5G-8)

`python -I tools/sprawdz_logi.py --grupa m105gosp5 <Armoury-*.log>` (strumieniowo):
- **M5G5-0** segment "m105 gosp5" w kazdej linii DS od pierwszej z segmentem; zly format - NIE; brak w calym logu - INFO (klucz NIE albo log sprzed gosp5).
- **M5G5-1** M3W-2 (linia "Przeplywy osad (kiesy wsi)", srednio 7 dob) <= 5%; bieg ze starego zapisu bez prologu - niespelniony = DECYZJA (model 7.7%: 27 wsi bez wozow, 19 z pelna skrzynia < 1000 zl).
- **M5G5-2** wsie biedne (kiesa < min(1000 zl, skrzynia), "Akumulator kies") <= 7% i nachylenie MNK 28 dob <= +0.1 wsi/d (cel M5G-7b).
- **M5G5-3** ulga pana przy wozie, pierwsze / ostatnie 28 linii (INFO; model: koniec prologu 1.5-3.5 tys./d, pierwsze 28 dob prologu 18-19 tys./d).
- **M5G5-4** M5G-8 po uldze: (pan i majatki - R + czynsz + danina od skarbca do panow) / oddane >= 0.514.
- **M5G5-5** czynsz darowany do skrzyni (INFO).
- **M5G5-6** potkniecia linii DS i ERROR `VillageTakings.LordRelief`, `DsVillage.RentPay`, `DsVillage.NoteRelief`, `DsVillage.Daily` = 0.
- **M5G-8** (grupa m105gosp4): od "pan i majatki" odejmuje R z segmentu "m105 gosp5" (bez segmentu - jak dotad).

Testy syntetyczne (`t_syn.py` w scratchpadzie gosp5, wycinek logu 103a 02-22-17, 230 linii DS): bez segmentu - M5G5-0 INFO, M5G5-4 = M5G-8 = 0.546; z segmentem R = 2500 zl/d - M5G5-0 TAK, M5G5-4 i M5G-8 0.537, M5G5-3/5 licza 2500 i 900; zly format - NIE; jedna linia bez segmentu - NIE; ERROR LordRelief - M5G5-6 NIE; tryb starego zapisu - M5G5-1 DECYZJA. `sprawdz_narzedzie.py`: dispatch, def, grupy, kompilacja - ok.

### 4. Przewidywany wynik (test koncowy prologiem 230 z cz.4; model na csv wsie-kiesy, nie bieg gry)

| Prog | Prognoza | Uwagi |
|---|---|---|
| M3W-2 / M5G5-1 | ok. 2-4% | n31 18.3 -> 2.3%, n32 12.8 -> 2.1%; zapis 13-37-13 15.5 -> 7.7% |
| M5G-7 / M5G-7b / M5G5-2 | 0.5-1.5%, nachylenie ok. 0 | nachylenie +0.29..+0.40 -> +0.06 i +0.01 wsi/d |
| M3W-1 | +0.02..+0.04%/d | pomaga przy ryzyku cz.4 (-0.09..-0.13) |
| M3W-3 / M5G-9 | +-0.01 | |
| M3W-7 W | -0.3..-1.2 pp | ulga kupuje czesc ziarna z "bez kiesy"; raczej dalej ponad 10% (M5G-1b DECYZJA zostaje) |
| M3W-9 / M5G-12 | ok. +-0.3 pp | M3W-9 juz na granicy (78.0) |
| M5G-8 / M5G5-4 | 103a 0.546 -> ok. 0.535; z cz.4 0.52-0.54 | najwezszy margines (0.514) |
| M5G-8b | maleje | ponizej skrzyni czynsz darowany zamiast zaleglosci |
| NDS-6c | bez zmian | darowane w "wstrzymane" |
| m105miasta | neutralnie | kod miast nietkniety; ziarno na polkach ok. +1% popytu wsi |
| M3W-10 | lekko lepiej | skrzynie pelne szybciej, G4 tnie mniej |

Koszt panow (model): prolog n31 18.2 / 6.7 tys./d, n32 18.9 / 2.2 tys./d (pierwsze / ostatnie 28 dob); zapis 13-37-13 8.1 / 4.8 tys./d. Odpornosc: wynik prawie bez zmian przy wydatkach wsi z pusta kiesa 32-250 zl/d i udziale pana 0.80-0.95.

### Ryzyko / co sprawdzic

- **M5G-8 (najwezszy margines, prog 0.514).** Model 0.52-0.54 z cz.4. NIE w tescie koncowym = wylaczyc klucz (wylacznik wyzej - bez nowego testu).
- **Poczatek biegu.** W prologu panowie traca przez pierwsze 28 dob 18-19 tys./d (ok. 12% dochodu ze wsi), w zapisie Jeffa ok. 8 tys./d (ok. 5%), potem 4.8. Sprawdzic C-4c (rody na minusie) i NAKUM-7.
- **G5 a skrzynia.** Ponizej skrzyni czynsz darowany zamiast zapisu w ksiedze zaleglosci; G5 dziala dalej tylko powyzej skrzyni. M5G-8b spada, "nowa zaleglosc" w linii DS spada, "wstrzymane" rosnie o "czynsz darowany do skrzyni".
- **Liczniki brutto/netto.** "pan i majatki (bez zmian)" w linii DS - brutto (M5G-8 i M5G5-4 odejmuja R); csv `woz_netto_do_kiesy` - z ulga; "do kiesy" utargu wsi (`NoteV`) - z ulga (jak zwrot F4b); z tego licznika jest "wplyw do kies wsi (utarg doby)" linii ZC4 (`StartCalibration.Money.cs` :76), wiec M3W-3 / M5G-9 lekko w dol (mianownik wiekszy o R). Alfa Planu wsi DS bez zmian (tylko oddane).
- **Wioska poboczna gracza (n20/r4-wioska).** `SideVillageFief.Pay` bierze udzial gracza najpierw z licznika pana, brak - z kiesy pana. Gdy wies okregu gracza jest DS i ponizej skrzyni, ulga zmniejsza licznik - reszta udzialu gracza idzie z kiesy pana (dotyczy jednego okregu, tylko gracz z wioska).
- **Co zostaje biedne (model zapisu 13-37-13, 48 wsi):** 27 wsi z utargiem < 30 zl/d (wozy nie wracaja - Wolni Ludzie, Zelazne Wyspy, Ziemie Burzy) - nie ma platnika; 19 z pelna skrzynia < 1000 zl - M3W-2 je liczy, M5G-7b juz nie. Na to ta zmiana nie dziala (osobny krok, jesli Jeff zechce).
- **Koszt czasu.** Skrzynia przy wozie liczona raz na powrot wozu wsi DS, tylko gdy licznik pana urosl w oknie: `ChestToday` (slownik) albo - przed blokiem doby - `Chest` (DsVillageDay + dekret); w RentPay tylko przy racie albo zaleglosci. Sprawdzic czasy w liniach doby (oczekiwane bez zmian w granicach szumu).
- **Brak testu w grze.** Wynik z modelu na csv; kod sprawdzony buildem i progami na syntetycznych liniach. Pierwszy bieg gry z kluczem - test koncowy.

### Poprawki po recenzji (recenzja `fd388555..b7a9bc32`: 0 wywrotek, 0 zlych stanow, 2 drobne)

- **Uwaga 1 (poprawiona): czynsz wsi z kiesa mniejsza od raty nie byl darowany w calosci.** Blok skrzyni w `RentPay` liczyl darowane od raty juz ograniczonej kiesa (`payDay = min(rata, kiesa)`): przy kiesie 5 zl i racie 17 zl darowal 5 zl, a 12 zl x rampa szlo do ksiegi zaleglosci; przy kiesie 0 blok nie biegl wcale (cala rata do ksiegi). Sprzeczne z opisem klucza i podpowiedzia MCM. Teraz darowane liczy sie od calej raty (`dayR = floor(rata)`): `chestRemit = dayR - pc`, `payDay = min(payDay, pc)` (= pc, bo pc <= kiesa), do ksiegi idzie tylko ulamek zlotowki. Zlota to nie rusza (zaleglosc wsi ponizej skrzyni i tak nie byla splacana - rata zaleglosci tylko ponad max(koszyk, skrzynia)); dotyczy glownie ok. 27 wsi bez wozow (ok. 17 zl/d kazda mniej nowej zaleglosci). Skutek w linii DS: "czynsz darowany do skrzyni" i "wstrzymane" wyzsze, "nowa zaleglosc" nizsza (NDS-6a/6b nizej, NDS-6c bez zmian albo wyzej). Klucz NIE - bez zmian (blok nie biegnie, `dayR` nieuzywane). Test: `pop/rent_sim.py` w scratchpadzie gosp5 - 432 przypadki (rata, kiesa, skrzynia, zaleglosc): bilans rata = zaplacone + darowane + do ksiegi, zaplacone <= kiesa, ponizej skrzyni do ksiegi < 1 zl, ponad skrzynie nic darowane, klucz NIE = `fd388555`, przy kiesie >= raty wynik jak w `b7a9bc32` - 0 bledow.
- **Uwaga 2 (nie wdrozona, wybor projektu): ulga pana bez rampy DS (28 dob).** Inne nowe reguly DS wchodza x Ramp; ulga i darowany czynsz - od razu w calosci. W zapisie Jeffa Ramp = 1 (bez znaczenia); w prologu szczyt kosztu panow (18-19 tys./d) wypada w 28 dobach rampy. Nie wdrozone, bo progi wiazace leza poza rampa w biegu 230 dob (C-4c to INFO, wiazacy C-4c3 liczy od doby 120 pliku; NAKUM-7 - ostatnie 28 dob), a wczesne napelnianie skrzyn jest celem zmiany. Gotowa poprawka, gdyby C-4c3 albo NAKUM-7 wyszly NIE i nie chcialo sie wylaczac calego klucza: w `VillageTakings.LordRelief` `long back = (long)Math.Min(Math.Floor(got * DsVillage.Ramp), Math.Min(lack, int.MaxValue));` (M3W-2 na koncu biegu bez zmian, bo po 28 dobach Ramp = 1).

### Build i commity

- `dotnet build Armoury/Armoury.csproj -c Release` - 0 bledow (7 starych ostrzezen); `python -I tools/gen_mcm.py` (McmSettings: wlasciwosc + przepisanie); `sprawdz_narzedzie.py` - ok.
- `c99aa641` kod (Settings, McmSettings, VillageTakings.LordRelief, DsVillage RentPay/ReliefChest/NoteRelief/linia, TownFolk.DsAkum).
- `c5c6314b` narzedzie (`--grupa m105gosp5`, M5G-8 po uldze).
- ten wpis CHANGELOG.
- poprawki po recenzji: uwaga 1 w `DsVillage.RentPay` (kod) i ten opis.
