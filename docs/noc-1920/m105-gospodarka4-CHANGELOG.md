## 2026-10-21 (m105-gospodarka4: czesc 4 gospodarki - G1-G6 i N1 z projektu przeliczonego na logach n31 12-48-45 i n32 14-29-20)

**Mod:** Armoury (+ `tools/sprawdz_logi.py`) | **Galaz:** `w-toku/m105-gospodarka4` (od `77c17f3c` = n32, WGRANIE 102: gospodarka czesci 1-3 z kluczem Ks Village Basket At Shelf) | **Status:** DO SPRAWDZENIA (nic nie wgrane, gry nie uruchamialem; test w autotescie tylko w przerwie Jeffa - plan nizej)

Szesc nowych kluczy MCM, kazdy domyslnie TAK; kazdy NIE = kod WGRANIA 102 tej czesci. Nowych kluczy nie ma w `Armoury.json` Jeffa - dzialaja domyslne. Kazdy krok to osobny commit.

---

### 0. Co z danych zdecydowalo o projekcie (logi n31 bez czesci 3 i n32 z czescia 3, prolog 230 dob z KS)

- **Brak kcal w Westeros to glownie brak ziarna na polkach, nie przyciecie budzetu W1.** n32, srednio 28 linii ZC4: chciane 9848 szt., bez towaru 963, bez kiesy 826 (prawie samo ziarno i sol), przyciete W1 1512 szt. (22.7 tys. zl). W kcal brak ziarna jest ok. 4 razy wiekszy niz przyciecie (po zakotwiczeniu ok. 5.7 pp wobec 1.4 pp kcal koszyka W). Linia `Towary grain` 108877, n31 -> n32: zboze w miastach 188 -> 108 tys., na wozach 34 -> 76 tys., u kupcow KS-H w drodze 35 -> 52 tys., dowoz wozami 25.6 -> 18.1 tys./d przy prawie tej samej liczbie wozow (132 / 129) - to logistyka swiata (E5, bandy 5187 -> 9458 ludzi), nie pieniadz wsi.
- **Warstwa Planu:** sam Plan zostawia w W 5.3-8.1% kcal bez pokrycia (cel Planu 3.0%; 57% z tego Reach), a gra ma stale ok. 2.6 pp mniej "robione w domu" niz Plan (tak samo w n31 i n32).
- **M3W-9** w n32 trzymal 77-82% w kazdym oknie 14 dob do doby 108850; spadek do 75.5 / 74.7% tylko w 28 ostatnich liniach (glod: 1 miasto, Starfall).
- **Krok 5 prawie nic nie wydaje:** budzet zbytkow ok. 262 tys./d, wydane ok. 35 tys. (towar 23, roboty gminy 12); nadwyzka ponad skrzynie 3.9-4.0 mln.
- **Ubostwo rosnie** (n32: 48 wsi w 108822 -> 73 w 108871): 34 nowe biedne wsie - utarg spadl z 208 do 48-130 zl/d, a zakupy koszyka zostaly na 190-200 zl/d przez 4 tygodnie (budzet ze sredniej 28 dob nie nadaza za spadkiem); czynsz placony pierwszy, do calej kiesy.
- **M3W-5 = 2% M3W-4 z samej reguly** (danina panom = 3%/d x 2/3 x nadwyzka ponad 3 x skrzynie; n32 18.3 tys. / 0.93 mln = 0.0197) - cele 40 tys. i 1.5 mln sa sprzeczne.
- **Bezposredni dochod panow ze wsi** (udzial przy wozie + czynsz + danina od skarbca): n31 152.2 tys./d (0.514 oddanego), n32 152.2 tys./d (0.556).
- **Zaleglosci zamkow (M3P-7):** prolog n32 39.0 tys. (+0.8 tys./d, Oros 18.1 tys. = 4.3 raty), stary zapis 13-37-13 449 tys. (+6.8 tys./d, Sweetport 144 tys. = 8.6 raty, kasa przy podlodze). Zamek nie mial drogi w dol poza przegladem kwartalnym.

---

### G1. Po chlebie najtansze kalorie - klucz `KsVillageCheapKcalFirst` (MCM "Ks Village Cheap Kcal First", grupa "Start calibration (KS)"; TAK; NIE = kod 102)

**Problem:** przy budzecie W1 (n32: przyciete 17.7% chcianych) drabina 1 po ziarnie i soli kupowala najpierw napoj (najtanszy za litr), potem bialko, potem reszte jedzenia - gdy budzet sie konczyl, czekaly owoce i bialko, choc wino i piwo daja mniej kcal za grosz.

**Przyczyna:** `TownFolk.Ks.cs` `ZcLadder` - stala kolejnosc szczebli projektu ZC 8.4 (napoj -> bialko -> reszta), bez wzgledu na cene kcal przy budzecie.

**Zmiana (`TownFolk.Ks.cs`):**
- `ZcLadder`: przy budzecie W1 (`b.CapOn`) i kluczu - nowa `ZcCheapKcal`: jedzenie drabiny 1 po ziarnie i soli (napoje, bialko, reszta jedzenia z `ZLadder`) jedna lista wedlug zl za 1000 kcal przy cenie polki doby (`ZcPrice` / (kg x `KsBasket.Kcal` / 1000)); towar bez kcal (przyprawy) po towarze z kcal, towar bez polki na koncu (potem miasta obok, F2); remis - kolejnosc drabiny. Zakup zastepczy napoju (wino -> piwo -> ziarno na piwo domowe) po przejsciu listy, w tym samym budzecie. Rzeczy (przedza, len, skora, narzedzia, garnki) dalej po calym jedzeniu (krok (4)).
- `ZcDrinkSwap`: zakup zastepczy napoju wydzielony bez zmian (ta sama kolejnosc dzialan w sciezce NIE).
- Plan (`KsBasket.Rungs`) bez zmian.

**Linia logu:** w "Koszyk stanow (ZC4)" za segmentem "m103 wsie": `| m105 gosp4 (Cheap Kcal First TAK, Grain Spike From Surplus TAK, Own Grain First TAK, Chest Refill TAK): kcal kupione drabiny 1 na 1 zl (jedzenie po ziarnie i soli) Westeros R (K kcal / Z zl), Essos ...; skok ceny z nadwyzki Westeros X zl w N zakupach, Essos ...; swoje ziarno zamiast oszczednosci P szt. (Q zl, V wsi); ponizej skrzyni W wsi, budzet mniej o B zl`. Segment tylko przy budzecie W1 z zakupami drabiny 1 i ktoryms z kluczy G1-G4 (wszystkie NIE - linia jak w 102).

**Skutek (projekt, sim_drabina z kalibracja x0.67):** kcal czesci zaplaconej +2.7..+3.9 pp, kupione W ok. +0.65..+0.95 pp; bez pokrycia W ok. -0.4..-0.6 pp, E ok. -0.3..-0.5; pieniadz 0.

---

### G2. Skok ceny ziarna i soli z nadwyzki ponad skrzynie - klucz `KsVillageGrainSpikeFromSurplus` (MCM "Ks Village Grain Spike From Surplus", grupa "Start calibration (KS)"; TAK; NIE = kod 102)

**Problem:** ziarno W kosztuje 2.24 x startu (Plan liczy najwyzej 1.5); ziarno i sol obciazaly budzet W1 po cenie polki, wiec drogi chleb zjadal pieniadze na reszte jedzenia - takze w wsiach z kiesa duzo ponad skrzynie (nadwyzka 3.9-4.0 mln, krok 5 wydaje ok. 35 z 262 tys./d).

**Przyczyna:** `TownFolk.Ks.cs` `ZcTake` - `b.Cap -= paid` takze dla ziarna i soli (`NoCap`), bez zrodla na roznice ceny.

**Zmiana:**
- `StartCalibration.R3.cs`: nowa `ZPlanPrice(t, z)` = wartosc x indeks startu rynku x gradient krainy (to co `ZPass2Price` bez `P2Res`; pamiec doby T4 - te same liczby co budzet Planu w `Zc4VillagePaid`).
- `TownFolk.Ks.cs`: `Zc4BuyCore` przy budzecie W1 wola `SpikeRoomOf` - wies DS z kiesa ponad skrzynie (`DsVillage.Chest`) dostaje na ten zakup nadwyzke `lam x rampa DS x (kiesa - skrzynia) / DsSurplusSpendDays` (tempo kroku 5; kiesa przed zakupem). `ZcTake`: przy ziarnie i soli (`NoCap`) `SpikeFrom` - czesc zaplaconego ponad `k x ZPlanPrice` placi nadwyzka (do jej konca), budzet obciaza reszta (`b.Cap -= paid - fromS`; "ziarno i sol ponad budzet" `GsPaid` takze bez czesci z nadwyzki). Wies nie DS, na skrzyni albo ponizej, rampa 0, DsSurplusSpendDays 0 - placi jak dotad. Takze w miastach obok (F2), ta sama nadwyzka.
- `DsVillage.cs`: `SpikeNote` (zakup -> ksiega wsi `_spikeOwed`), `SpikeTake` w `SurplusDay`: czesc P5 kroku 5 mniejsza o skok ceny juz zaplacony (najwyzej P5 doby, reszta czeka na nastepne doby - zakup koszyka jest co 2. doba za 2 doby). `_kP5Plan` liczony po odjeciu (tozsamosc NDS-17 "P5 = towar + roboty + niewydane + w kiesie" bez zmian). `SpendDaysNow`, `ChestDaysNow` - odczyty ustawien. Ksiega sesyjna (Reset; bez zapisu - najwyzej 2 doby kroku 5 jednej wsi).

**Linia logu:** ZC4 - pole "skok ceny z nadwyzki" segmentu m105; "Daniny wsi (DS)" przed "| kontrola": `| m105 gosp4 (Grain Spike From Surplus TAK, Rent After Bread TAK): krok 5 mniej o skok ceny chleba X zl (N wsi), czeka na nastepne doby Y zl (M wsi); czynsz po chlebie - rata dnia do zaleglosci Z zl (K wsi)` (tylko przy G2 albo G5).

**Skutek (projekt):** dziala tylko w W (ziarno W 2.24 > 1.5; E 1.01); kupione W ok. +0.9 pp, bez pokrycia W ok. -0.5..-0.6 pp; z nadwyzki ok. +7.8..8.0 tys. zl/d; nadwyzka ponad 3 x skrzynia nizej o ok. 0.27 mln, danina panom najwyzej -5.3 tys./d.

---

### G3. Swoje ziarno przed oszczednosciami - klucz `KsVillageOwnGrainFirst` (MCM "Ks Village Own Grain First", grupa "Start calibration (KS)"; TAK, dziala z Ks Own Food First; NIE = kod 102)

**Problem:** n32: ziarno i sol ponad budzet 6.4 tys. zl/d kupowane z oszczednosci, a wsie W i E produkujace zywnosc maja ziarno czekajace na woz - wies sprzedawala swoje zboze miastu, zeby je drozej odkupic z oszczednosci.

**Przyczyna:** `ZcLadder` szczebel (1) kupowal cale ziarno i sol; "wies je swoje" (G3 Ks Own Food First, `OwnFirst`) dopiero po calej drabinie i tylko wobec braku kcal.

**Zmiana (`TownFolk.Ks.cs`):**
- `ZcLadder` (1): przed `ZcTake` ziarna i soli `ZcOwnGs` - czesc, na ktora nie starcza reszty budzetu (`GsAfford`: przy G2 budzet obciaza cena Planu, dopoki starcza nadwyzki - ta sama regula co `ZcTake`/`SpikeFrom`), wies bierze z wlasnego magazynu (`OwnTake`, ramka `GoodsLedger.FKsOwn` jak G3; najwyzej polowa towaru na zakup razem z G3 po drabinie), kupuje reszte. Zjedzone liczy sie w chcianych drabiny 1 (Want, kcal i wartosc chciane) i jako "z magazynu".
- `OwnFirst` (G3 po drabinie): parametr `early` - polowa towaru liczona razem ze zjedzonym wczesniej; brak kcal `shortK` juz bez zjedzonego wczesniej (`b.OwnK`); licznik `KsOwnEaten` raz na zakup u wolajacego (`kr + OwnK`) - przy kluczu NIE te same wywolania i liczby co dotad.

**Skutek (projekt):** ok. 1.4 tys. zl/d mniej z oszczednosci (29 wsi W, 9 E); kcal bez zmiany (zjedzone zamiast kupionego); polki miast bez zmiany (woz wiezie mniej, wies mniej kupuje).

---

### G4. Wies ponizej skrzyni najpierw odklada - klucz `KsVillageChestRefill` (MCM "Ks Village Chest Refill", grupa "Start calibration (KS)"; TAK; NIE = kod 102)

**Problem:** n32: 73 wsie W i 31 E ponizej skrzyni wydawaly caly budzet z wplywu - biedna wies nie odbudowywala zapasu na czarna godzine (M3W-2 13.2%, kiesa < min(1000, skrzynia) 10.7%).

**Zmiana (`TownFolk.Ks.cs`):** `Zc4BuyCore` - `ChestRefillOf`: wies DS z kiesa ponizej skrzyni -> `lam x (skrzynia - kiesa) / DsVillageChestDays`; `ZcLadder` PO szczeblu ziarna i soli odejmuje te kwote od budzetu reszty drabiny (najwyzej do zera; ziarno i sol nietkniete). Pieniadz zostaje w kiesie; przyciete sztuki licza sie jak przyciete W1 (M3W-12).

**Linia logu:** pole "ponizej skrzyni W wsi, budzet mniej o B zl" segmentu m105 ZC4; w linii "Akumulator kies" przed "| czas": `| m105 wsie biedne (kiesa < min(1000 zl, skrzynia)) P z N wsi DS` (licznik w `DsVillage.AkNote`, odczyt `AkPoor`; tylko przy G4 albo G5).

**Skutek (projekt):** ok. 2.7 tys. zl/d zostaje w kiesach; kcal od ok. 0 do +0.2 pp (przy G1 odpada najdrozsza kaloria).

---

### G5. Czynsz po chlebie - klucz `DsRentAfterBread` (MCM "Ds Rent After Bread", grupa "Medieval dues: villages (DS F1, 17.10)"; TAK, dziala z Ds Rent Arrears; NIE = kod 102)

**Problem:** rata dnia czynszu placona pierwsza z calej kiesy (`DsVillage.RentPay` krok 1), takze gdy kiesa nie ma na koszyk dnia - przy spadku utargu biedna wies tracila pieniadze na chleb (34 nowe biedne wsie n32). Zaleglosc juz miala zasade "nigdy nie zabiera chleba".

**Zmiana (`DsVillage.cs` `RentPay`):** przy kluczu i ksiedze zaleglosci rata dnia najwyzej `kiesa - koszyk dnia` (`VillNeeds.ChestBasket` - ta sama miara co rata zaleglosci); reszta raty idzie istniejaca droga do ksiegi zaleglosci (x rampa, darowane po DsRentForgiveDays rat). Koszyk liczony raz (wspolny z krokiem 2). Licznik `_rBread` / `_rBreadN` - pole "czynsz po chlebie" segmentu m105 linii DS.

**Skutek (projekt):** ok. 0.5-0.75 tys. zl/d czynszu odlozone (33 wsie W, 5 E); kupione W ok. +0.35 pp, bez pokrycia W ok. -0.3 pp.

---

### G6 (B1). Obnizka z dochodu zamkow - klucz `DsCastleIncomeCut` (MCM "Ds Castle Income Cut", grupa "Town purses: the farm catches up..."; TAK, dziala z Ks Farm Follows Surplus + Ks Farm Surplus Cap Net i zastosowana KS; NIE = kod 102)

**Problem:** M3P-7 - zaleglosci zamkow rosna (prolog +0.8 tys./d, stary zapis +6.8 tys./d); przeglad wedlug nadwyzki i obnizka z dochodu (m103 P3 (e)) byly tylko dla miast z KS.

**Zmiana (`DsTowns.cs`):**
- `SurplusReview`: galaz zamku przed warunkiem miasta - `CastleIncomeCut`: kasa po naleznosciach (`CofferOwes`) najwyzej 1.5 x `CastlePurse.Reserve` albo zaleglosc firmy (>= raty) albo `RemitOf` -> `IncomeCut` (cykl Ks Farm Surplus Review Days 14, F = max(doplyw netto x 364, 0.75 x F)); kasa ponad progiem - cykl od nowa; w rampie DS - bez obnizki (jak miasta). Podwyzek zamkow i gersumy BRAK.
- `IncomeCut`: zwraca obnizke (zl/rok), parametr `castle` - obnizki zamkow poza licznikiem linii f5 (`KsFolkIncome.NoteIncomeCut` tylko miasta, M3P-3 bez zmian).
- `CapNetTown`: takze zamek przy kluczu (`CastleCutOn`: klucz + Ks Farm Follows Surplus + Ks Farm Surplus Cap Net + `StartCalibration.Applied`) - udzial firmy > 1 liczony jak 1 w przegladzie kwartalnym (`Review`, `Renew`; licznik `NoteCastleShare`).
- Linia "Firmy miast i zamkow (DS)" po segmencie m103 P3: `| m105 zamki (Ds Castle Income Cut TAK): obnizki z dochodu zamkow dzis N (-X zl/rok), w sesji M (-Y zl/rok); udzial zamku > 1 liczony jak 1 dzis K, w sesji L` (przy kluczu; bez KS albo bez Cap Net - "NIE - bez Ks Farm Surplus Cap Net albo kalibracji startu").

**Skutek (projekt, p4_zamki.py):** prolog n32 39.0 -> ok. 8.6 tys. (Oros 18.1 -> 3.5 tys.), przyrost ok. 0; stary zapis 449 -> ok. 260-310 tys. (Sweetport 144 -> 87 tys.), przyrost do ok. 0 po 3-5 cyklach. Zloto sie nie rusza - zmienia sie nalezna rata.

---

### N1. Narzedzia wsi - zmiana miary, nie gry (`tools/sprawdz_logi.py`, grupa m103wsie)

- M3W-10: narzedzia kupione przez wsie (ksiega towarow, linia Towary) / potrzeba wsi z koszyka ZC4 (pole `tools a+b` linii ZC4) >= 0.9: n31 1.02 (11.2 / 11.0), n32 1.25 (8.1 / 6.5) - TAK; bez koszyka ZC4 INFO (dawny cel 30/d starej listy z pieniedzy).
- Poprawione zdanie progu: wsie kupuja PRZED budowami (`ArmouryBehavior.cs` :2662 `TownFolk.Daily` przed :2688 `BuildFunding.Daily`); do pracy pola narzedzi nie potrzebuja.

### Decyzje o celach (z dowodem z projektu)

- **M3W-7 W <= 10%: nieosiagalne pieniedzmi wsi.** Czesc 4 daje W ok. 10.9-11.3% (ponizej bazy n31 11.4%); reszta to zboze na polkach W (ok. 5.7 pp) i warstwa Planu - osobny tor K-D (logistyka zboza na polnoc i KL), najpierw diagnoza. Narzedzie: M5G-1 <= 11.4% (TAK/NIE) + M5G-1b <= 10% (DECYZJA z odsylaczem do K-D), M5G-11 INFO.
- **M3W-9 >= 78%:** czesc 4 neutralna (74-77%); straznik M5G-12 (84 linie >= 77%, 28 ostatnich >= 74%; okno 84 niepelne - tylko 28).
- **M3W-4 i M3W-5: z celu na INFO** (sprzeczne z reguly - M3W-5 = 0.02 x M3W-4; podniesienie wymagaloby poboru resztowego - zakaz Jeffa - albo mniej jedzenia). Zastepuje je M5G-8 (bezposredni dochod panow / oddane >= 0.514).
- **M3W-2** bez zmian (<= 5%); dodane M5G-7 "kiesa < min(1000 zl, skrzynia)" <= 7% i nachylenie <= +0.1 wsi/d (n32 10.7%, P0 1.1%; 91 wsi ma skrzynie < 1000 zl - wies z pelna skrzynia nie jest biedna).
- **M3P-7** <= 200 tys. zostaje dla prologu; dla biegu ze starego zapisu M5G-14 (przyrost <= +1 tys./d w 28 dobach i razem <= 270 tys.) - te zaleglosci to firmy ustalone kodem 101, a Jeff gra kampanie z prologiem.
- **B4 (Z8 odlicza nominalna firme zamku): NIE w tej czesci** (ok. 300 zl/d tylko w wojnie; CLAUDE.md 8.3). Zapisane na pozniej.

### Progi: nowa grupa `--grupa m105gosp4`

| Prog | Co mierzy | Warunek |
|---|---|---|
| M5G-0 | segment "m105 gosp4" w kazdej linii ZC4 z segmentem "m103 wsie" | obecny (brak segmentow - INFO) |
| M5G-1 | M3W-7 W | <= 11.4% (n31) |
| M5G-1b | M3W-7 W | <= 10%; niespelniony = DECYZJA (tor K-D) |
| M5G-2 | M3W-7 E | <= 4.0% |
| M5G-3 | kupione W | >= 30.0% |
| M5G-4 | skok ceny z nadwyzki | W 3-12 tys./d, E <= 1 tys./d (G2 NIE - INFO) |
| M5G-4a/b/c | G1 kcal na 1 zl, G3 swoje ziarno, G4 ponizej skrzyni | INFO |
| M5G-5 | krok 5 mniej = skok ceny z nadwyzki - czeka na nastepne doby (caly log) | roznica <= 1% + 100 zl |
| M5G-6 | ziarno i sol ponad budzet (segment m103) | <= 4.8 tys./d |
| M5G-7 | kiesa < min(1000, skrzynia), srednio 7 dob; nachylenie MNK 28 dob | <= 7% i <= +0.1 wsi/d (bez segmentu - INFO z "ponizej 1000 zlota") |
| M5G-8 | (pan i majatki przy wozie + czynsz + danina od skarbca do panow) / oddane, 28 dob | >= 0.514 |
| M5G-8b | czynsz po chlebie - rata dnia do zaleglosci | <= 1.5 tys./d |
| M5G-9 | M3W-3 (ta sama miara co m103wsie) | <= 0.90 |
| M5G-11 | zboze na wozach / w miastach, u kupcow KS-H, dowoz, bez towaru W | INFO (K-D) |
| M5G-12 | M3W-9 z 84 linii / z 28 ostatnich | >= 77% / >= 74% |
| M5G-13 | zaleglosci zamkow - nowa kampania | <= 20 tys. i MNK 28 dob <= +0.2 tys./d |
| M5G-14 | zaleglosci zamkow - bieg z zapisu | <= 270 tys. i MNK 28 dob <= +1 tys./d |
| M5G-15 | obnizki zamkow w sesji - bieg z zapisu | > 0 |
| M5G-15b | potkniecia linii ZC4, DS wsi, DS miast i zamkow; ERROR nowych funkcji | 0 / 0 |

Tryb nowa kampania / zapis - z logu ("Klimat: wczytano", "Wyrzutki: wczytano", "Stany (ZC2): wczytanie" bez "Prolog (m101): nowa kampania" / "Wiedza (m96-wzory): nowa kampania").

**Sprawdzenie narzedzia (bez gry):**
- `sprawdz_narzedzie.py`: dispatch, def, grupy, kompilacja OK.
- Prawdziwe logi (kontrola, ze progi licza - segmentow m105 tam nie ma): n31 12-48-45 - M5G-1 11.4% (NIE o ulamek - 11.4x), M5G-3 35.9%, M5G-8 0.514, M5G-9 0.96, M5G-12 78.4 / 78.5%, M5G-13 97.7 tys. +1.7 tys./d; n32 14-29-20 - M5G-1 12.48%, M5G-2 3.6%, M5G-3 29.1%, M5G-6 6401 zl/d, M5G-8 0.556, M5G-9 0.83, M5G-11 wozy / miasta 0.60 (srednio 28 dob), M5G-12 77.1 / 75.1%, M5G-13 39.0 tys. +794 zl/d; 13-37-13 (zapis) - M5G-14 449 tys. +6.8 tys./d NIE; 15-19-59 (zapis prologu +20 dob) - M5G-14 19.3 tys. TAK, M5G-12 okno 84 niepelne. m103wsie: M3W-10 n31 1.02 TAK, n32 1.25 TAK; M3W-4/5 INFO (n32 18.3 tys. = 0.0199 x M3W-4).
- Logi syntetyczne (wyciag 19 549 linii n32 + segmenty w formacie z kodu; skrypt `synt_m105.py` w scratchpadzie gosp4): v1 (spojne) - M5G-0 178/178 TAK, M5G-4 W 7900 / E 75 TAK, M5G-5 roznica 0 TAK, M5G-7 6.8% / +0.00 TAK, M5G-8b 600 TAK, M5G-15 INFO (nowa kampania); v2 (zepsute) - M5G-0 168/178 NIE, M5G-4 E 1500 NIE, M5G-5 +4.61% NIE, M5G-7 23.1% / +0.50 NIE, M5G-8b 2000 NIE, M5G-15b ERROR 1 NIE; v3 (zapis) - M5G-14 263.5 tys. +500 zl/d TAK, M5G-15 12 obnizek TAK, M5G-13 INFO; v3b - M5G-15 0 obnizek NIE; v4 (G2 NIE) - M5G-4 INFO, bez M5G-5; v5 (prolog, zamki 7.7 tys. -50 zl/d) - M5G-13 TAK.
- Regresja regexow: 15 grup czytajacych zmienione linie (m103wsie, m103p3, nds, nks, nakum, nkasy, nzajazdy, etap5, nwar, nopal, nruda, nwegiel, m96koszyk, m96bandy, m96dlugi_dlugi) na wyciagu bez segmentow i z segmentami (v1) - wyniki identyczne (po ujednoliceniu nazwy katalogu CSV w sciezkach).

### Kontrola calosci (CLAUDE.md 8.0)

- **Zamknieta ekonomia:** zadnego nowego przeplywu zlota. G1 - kolejnosc zakupow; G2 - ta sama cena polki do kasy miasta, inny budzet (nadwyzka zamiast budzetu W1), krok 5 wydaje o tyle mniej (zbytki i roboty gminy -> jedzenie); G3 - towar z magazynu wsi zjedzony (ramka FKsOwn, ujscie "wies je wlasny towar (KS)"), bez zlota; G4 - pieniadz zostaje w kiesie; G5 - rata do ksiegi zaleglosci (platnik i odbiorca bez zmian, pozniej); G6 - nalezna rata nizsza, zloto sie nie rusza. Kontrole "platnicy - odbiorcy" bez zmian.
- **Te same miejsca stanu:** `ZcTake` (budzet tylko przy `CapOn`; nowa czesc tylko przy `NoCap` + `Spike`), `ZcLadder` (G1 tylko przy `CapOn` - sciezka NIE co do wywolania jak 102; `ZcDrinkSwap` wydzielony bez zmian), `Zc4BuyCore` (pola G2-G4 ustawiane po `ShelfBudget`, zerowane w `finally`), `OwnFirst` (jedyny wolajacy - `Zc4BuyCore`), `KsOwnEaten` (raz na zakup), `DsVillage.SurplusDay` (jedyny odczyt `_spikeOwed`), `RentPay` (jedyny wolajacy `PopulationLaw.Daily`, NDS-15 - czynsz zaplacony = oplaty stale - dalej z tego samego `pay`), `IncomeCut` (miasta - te same liczby, licznik f5 bez zmian), `CapNetTown` (miasta - ta sama formula).
- **Kolejnosc w dobie:** `PopulationLaw.Daily` (czynsz, G5) przed `TownFolk.Daily` (zakupy wsi G1-G4, krok 5 G2 - ta sama petla wsi `Villages`, krok 5 po koszyku); `DsTowns.DayStart` -> `SurplusReview` (G6) przed `PopulationLaw.Daily` (raty). Linie ZC4, DS wsi, DS miast, Akumulator - po `TownFolk.Daily` w tym samym bloku doby (sumy M5G-5 z tych samych dob).
- **Stan miedzy kampaniami:** `_spikeOwed` w `DsVillage.Reset`; liczniki ZC4 w `Zc4Zero` (po linii i przy nowej sesji); liczniki zamkow doby w `ClearCounters`, sesji w `DsTowns.Reset`; `_akPoor` z doba `AkNote`. Nic nowego w zapisie (G2: po wczytaniu skok z ostatniego zakupu przed zapisem nie jest odejmowany od kroku 5 - najwyzej 2 doby jednej wsi).
- **Cudzy kod:** brak nowych latek; G3 uzywa tych samych wywolan `ItemRoster` co `OwnFirst`.
- **Koszt:** G1 - ok. 18 odczytow ceny na zakup zamiast 9 (ok. 288 zakupow/d); G2/G3 - 2-4 `ZPlanPrice` na zakup (pamiec T4); G6 - petla po firmach jak dotad. Ponizej 0.1 ms/d.

### Ryzyko / co sprawdzic

- **M3W-1 i danina (G2) - glowne ryzyko:** nadwyzka placi skok ceny, wiec kiesy wsi bogatych spadaja szybciej (przed wyrownaniem ok. -0.06%/d, stala czasowa daniny od skarbca ok. 33 doby). Jesli M3W-1 wyjdzie ponizej -0.10%/d - wylaczyc G2 (wtedy W ok. 11.5%). Danina panom (M3W-5 INFO) i M5G-8 moga spasc (G2 do -5.3 tys./d).
- **NKS-25 (wies je swoje <= 8%):** E z magazynu juz 10.2%, G3 doklada (ziarno ponad budzet) - porownac "z magazynu" W/E w kroku 8 i linie "KS: wies je swoje".
- **G1 zmienia popyt na napoje i oliwe** (KS-H wiezie wedlug koszyka Planu) - wyrownanie przez ceny; NKS-7 / NKS-7e, polki napojow.
- **G3 w chcianych:** zjedzone swoje ziarno jest w chcianych drabiny 1, a nie w kupionych - M3W-6 i NKS-13 "kupione / chciane" spadna lekko z zalozenia.
- **G4:** przyciete W1 (M3W-12) rosna o czesc odlozona; narzedzia drabiny 1 (M3W-10b) moga byc przyciete czesciej (koniec drabiny).
- **G5 powieksza zaleglosci czynszu** (linia DS "nowa zaleglosc", "zaleglosci ... ponad 91 rat", darowane po roku; grupa nds) i zmniejsza czynsz zaplacony dzis (M5G-8 w liczniku).
- **G6 obniza dochod panow i korony z zamkow** (C-4c, NAKUM-7); zamki bez KS - bez zmian (regula tylko przy zastosowanej kalibracji, jak miasta).
- **Kolizje przy scalaniu:** sprawdzic, czy m105-miasta (wf_e590bc20) zmienia `DsTowns.cs` (`SurplusReview`, `IncomeCut`, `CapNetTown`) albo progi nakum / C-4c; `tools/sprawdz_logi.py` scalac osobnymi commitami (pulapki scal.sh: dispatch w cudzym if, te same nazwy grup); pliki .cs zostaja CRLF. `TownFolk.Ks.cs` (ZcLadder, Zc4BuyCore, OwnFirst) - kazda galaz ruszajaca drabine wsi koliduje.
- **Wylaczniki:** kazdy klucz NIE musi dac swoj odcinek "NIE" w segmencie i liczby jak 102 (bieg A4-x).

### Plan autotestu (tylko w przerwie Jeffa; nic nie wgrywac, gdy gra dziala)

1. **C0 kontrola, bez kodu:** DLL 102 z `KsVillageBasketAtShelf` NIE, prolog 230 dob z `armoury-ks-auto.flag`, ok. 45 min. Porownanie z 14-29-20 w tych samych dobach (M3W-7 W, M3W-9, M5G-11) oddziela W1 od E5 i wojen - od tego zalezy priorytet toru K-D.
2. **A4 - kandydat (wszystkie klucze TAK), prolog 230 dob z KS.** Musza dac TAK: m105gosp4 M5G-0..9, 12, 13, 15b (M5G-1b DECYZJA dopuszczalna, M5G-11 INFO); m103wsie M3W-0, 1, 3, 6, 7 E, 10, 13; m103p3 calosc; bez pogorszen: nks*, ne3, nakum (NAKUM-26/27), C-4c (etap8), m96bandy, m101prolog, m104wojny.
3. **B4 - kandydat, 160 dob z zapisu z KS** (jak 13-37-13): M5G-14, M5G-15, M3W-1, M3W-3, nachylenie M5G-7.
4. **Przy NIE:** biegi A4-x z jednym kluczem wylaczonym; kolejnosc podejrzanych: G2 (M3W-1, M5G-8), G1 (NKS-7, popyt napojow), G4 (M5G-1).

### Przewidywany wynik (prolog z KS, n32 -> czesc 4; z projektu)

| Prog | n32 | Czesc 4 |
|---|---|---|
| M3W-7 W | 12.5% | 10.9-11.3% |
| M3W-7 E | 3.6% | 3.0-3.3% |
| M3W-3 | 0.83 | 0.86 |
| M3W-1 (najgorsze okno) | -0.042%/d | -0.05..-0.09%/d (glowne ryzyko) |
| M3W-2 | 13.2% | 6-9% |
| M5G-7 | 10.7% | 4-7% |
| M3W-4 | 0.92 mln | 0.65-0.85 mln |
| M3W-5 | 18.3 tys./d | 13-17 tys./d |
| M5G-8 | 0.556 | 0.53-0.55 |
| M3W-9 | 75.1% | 74-77% |
| M3W-10 | 1.25 | ok. 1.25 |
| M3P-7 (prolog) | 39.0 tys. | ok. 9 tys. |

### Czego nie zrobilem

- **W-C (Plan wycenia koszyk po cenach polki):** czystsze zrodlo, ale nie do przeliczenia na logu, a sprzezenie praca domowa -> mniej towaru -> cena jest ryzykowne; krok warunkowy, gdy po A4 M5G-1 da NIE, a M3W-11 >= 1.8.
- **Budzet = sam wplyw (bez min z Planem):** +24-34 tys./d, M3W-3 > 1.0 na wiele dob.
- **Jedzenie z calej nadwyzki (C1):** do +54 tys./d, sprzeczne z M3W-1 i M3W-3.
- **B2, B3 (zamki):** B2 to szum okna, B3 wolniejszy niz B1. **B4** - patrz decyzje.

### Slowami gracza

- Po chlebie wies kupuje najpierw to, co najlepiej syci za grosz, a wino i owoce czekaja.
- Gdy chleb w Westeros drozeje, bogata wies doplaca z oszczednosci, zamiast rezygnowac z ryb i sera; mniej wtedy wydaje na zbytki.
- Wies nie sprzedaje swojego zboza miastu po to, zeby je drozej odkupic.
- Biedna wies najpierw odbudowuje zapas na czarna godzine, a czynsz pana nie zabiera jej pieniedzy na chleb - dlug czeka.
- Zamek, ktory nie zarabia na swoja oplate, dostaje nizsza oplate, jak miasta.
- Narzedzia sa w porzadku: wsie kupuja tyle, ile zuzywaja, a do pracy pola ich nie potrzebuja.
- Zostaje jedno: w Westeros brakuje zboza na polkach, bo utknelo w drodze. To osobna naprawa dowozu.

### Build i commity

**Build:** Armoury (Release, GameLibs z lancuch/libs) kod 0, 0 bledow, 7 ostrzezen jak przed zmiana. gen_mcm: Armoury 2281 ustawien (bylo 2275), RealisticCaptivity 115, GrandTourney 44. Pliki .cs CRLF (sprawdzone `file`). Gry nie uruchamialem, nic nie wgrane.

| Krok | Commit | Klucz MCM |
|---|---|---|
| klucze i przelaczniki | `6efce89c` | 6 nowych (TAK) |
| G1 najtansze kalorie | `11843471` | KsVillageCheapKcalFirst |
| G2 skok ceny z nadwyzki | `8412b513` | KsVillageGrainSpikeFromSurplus |
| G3 swoje ziarno | `c7be0f98` | KsVillageOwnGrainFirst |
| G4 skrzynia z budzetu + licznik biednych | `c64638b8` | KsVillageChestRefill |
| G5 czynsz po chlebie | `24cbba9d` | DsRentAfterBread |
| G6 obnizka zamkow | `023fdcc0` | DsCastleIncomeCut |
| narzedzie (m105gosp4, M3W-10, M3W-4/5) | `0673dba8` | - |
