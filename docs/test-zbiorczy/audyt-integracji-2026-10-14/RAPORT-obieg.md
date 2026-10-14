# Audyt "obieg" - kolizje strumieni zlota i ludzi miedzy paczkami nocy 13/14.10

Drzewo: `zb/int`, galaz `w-toku/zbior-e8`, HEAD 43af413 (diff od 4d33042 = WGRANIE 43). Kodu nie zmienialem, nic nie commitowalem, gry nie uruchamialem, w folderze gry nic nie zapisalem.
Logi: `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-13_20-41-38.log` (wersja koncowa bez lupu 5b, 120 dob), dla porownania te same okna dob w biegach
`_16-21-05`, `_16-58-06`, `_17-32-32` (A), `_18-04-21`, `_18-49-17` (paczka B: z P1, W7, N7, GL-B, BEZ konie robocze / P1b / P2 / P3-P5 / lupu) i rocznym `_19-21-12` (kod paczki B, 364 doby).
Skrypty pomocnicze (tylko odczyt logu): `scratchpad\skrypty\*.py`; wyniki grup `sprawdz_logi.py` na logu koncowym: `audyt\obieg\chk-*.txt`.

## 1. Co sie zgadza (log 20-41-38)

- **Pieniadz swiata**: 120 dob - suma skladnikow = "razem" kazdej doby (0 rozjazdow); zmiana dobowa "razem" = "zmiana sumy" linii bilansu = zrodla - ujscia + netto + reszta (0 bledow arytmetyki); reszta przyczyn srednio +168 zl/d, najwiecej |4 640| (d115/d116, para +4 549 / -4 640 - przesuniecie doby, nie lup).
- **Kiesy ludu r = 0** w 119 z 119 dob, takze w 3 dobach lupu (L2 `SackTake` -> `_ctlOut`), w dobach W7 (`LevyTake`), P2 (`WorksSpend`), koni (`WorkHorsesDaily`), P5 (`BuyLadder`). Kontrola licznika koni r = 0.00 (120 dob).
- W7-1/W7-2, P1-5a/5b, LUP-5/LUP-8/LUP-14, N7-2, P2-1, ZB-1, GLB-11c-3 - TAK; ksiega ludzi "kontrola 0" kazdej doby; LUP-17/17b (L6 = BK zabici) TAK.
- `--grupa etap3`: reszta R 28 dob 88-257; jedna probka swiata z rozjazdem: okno "nowe-karawany" +65 zl w dobie 108896 (1 z 683 probek) - za malo, zeby przypisac paczce.
- Bez podwojnego poboru tej samej zlotowki "ponad zapas" w kiesach ludu: P1 (krok 5 doby ludu) -> konie (kiesa ponad 1 dzien potrzeby) -> P2 (2% `LevyRoom`) -> W7 (najwyzej 3%, `LevyRoom` liczony od nowa = min(DOvC po P1, kiesa TERAZ - zapas dni - oferta)). P2 placi place z powrotem do W/R tej osady (przelew wewnatrz ludu), a W7 nadal tnie do DOvC - razem najwyzej ok. 5% nadwyzki po P1. Lup L2 (zdarzenie w srodku doby) trafia do kontroli kies nastepnego bloku - r = 0.
- L5b-1 przesuwa migawke 165 u obu stron (`CrownIncome.ShiftSnapshot`) - skarb przejety nie liczy sie jako "inne wplywy" dnia; L5b-2 przelewa kiese karawany `GiveGoldAction` z dajacym != null, wiec prefiks RealPayer (dziala tylko na "z niczego") jej nie przechwytuje.

## 2. Znaleziska

### Z1 [WAZNE] Trzy paczki naraz odcinaja doplyw ludu do kas kupcow: lokata ludu rosnie 10x szybciej niz zakladal projekt, zawor do panow, pulap 166 i wojsko w dol

**Strony:**
- K1-3a: `HistoricalPrices.cs:1011` (`NoHorseBasket` -> budzet koni w koszyku BK = 0) i `TownFolk.Horses.cs:135-233` (zastepczy zakup koni roboczych ok. 1.1 tys. zl/d).
- P3/P5: `TownFolk.Luxury.cs:228-237` (oferta W = nadwyzka/60 zamiast 15%/d), `:241-263` (`CastleLadder` - podzamcze kupuje w najblizszym MIESCIE, oferta w zamku 0).
- Paczka A: `TownPurse.cs:225-232` i `Seal31.cs:100-107` (zapas kupcow x m_t - zawor bierze tylko ponad nim); 11c: `TownFolk.Live.cs:803-818` (robocizna od skupu z kasy, przycieta tylko do stanu kasy, nie do zapasu).
- P1: `TownFolk.Live.cs:852-871` (danina 2.5% x s - wpis K1-3a: "Danina P1 2.5% jest policzona pod te regule").
- Odbiorcy: zawor kas miast/zamkow -> D rodu -> pulap 166 (`ClanBudget.Daily`, `ArmouryBehavior.cs:1765`).

**Scenariusz (liczby z logu, srednie dob 91-119; pozycje zamkow - srednie d30-90 linii "Przeplywy osad (kasy zamkow)"):**
1. Do paczki B lud miast placil w koszyku BK za konie ok. 124 tys. zl/d (linia "Konie (E6/0)", bieg `_18-49-17` d64: mieszczanie miast 61/21/2/129 szt. za 26 319/67 383/17 130/12 803 zl), zamki ok. 47 tys. zl/d. Od K1-3a: 0/0/0/0.
2. "Lud zaplacil kasom (E5/B1)": d1-7 189 tys. zl/d wobec 300-324 tys. w biegach bez K1-3a; d91-119 300 tys. wobec 377-410 tys. (5 biegow). W zamkach "lud za towar" 39.5 -> 9.1 tys. zl/d (P5 przenosi wydatek podzamcza do kas MIAST, konie zniknely).
3. Zawor bierze tylko nadwyzke ponad zapas x m_t, a 11c i robocizna dalej placa z kasy bez progu zapasu: "zawor do pana" 76 tys. zl/d (B 89, inne biegi 105-137), zawor zamkow do panow 17.9 -> 10.5 tys. zl/d; miasta >= 28 dob ponizej zapasu kupcow: 8 (B 2, A i baza 0; Tolos 89 dob z rzedu); ZB-6 NIE (9 miast >= 7 dob, prog <= 4).
4. Danina P1 rosnie (165 tys. zl/d wobec 120 w B), ale zabiera tylko czesc: `--grupa kiesa1 --regula-koni` na logu koncowym: **P1-2 przyrost lokaty W +108.9 tys. zl/d (prog <= +10 tys.; biegi bez P1 +47/+65)**, P1-3 kiesy ludu miast 28.76 mln (prog <= 26.0 mln), P1-1 lokata W 24.54 mln (prog roczny <= 12 mln). Kiesy ludu miast d119: 28.8 mln (B 20.9), podzamcza 4.5 mln (B 2.9).
5. Pieniadz, ktory dawniej szedl lud -> kasa -> zawor -> pan, stoi w kiesach: D rodow ("D razem" 166) 685 tys. (B 709, `_18-04` 782), pulap 166 1.053 mln (B 1.187, inne 1.14-1.32), zold naliczony 701 tys. (B 750), glowy rodow d119 44.6 mln (B 49.1).
6. Wojsko: druzyny lordow krolestw w wojnie (miara W5-7, d93-120) **101.3 tys. - najmniej z 6 biegow** (B 109.5; pozostale 105.7 / 112.6 / 113.9 / 116.5). Prog K1-3a P1-11 ("nie mniej niz bieg bez reguly koni minus 3 tys.") - 102.0 tys. wobec 109.5 w B = -7.5 tys.

**Dlaczego recenzje tego nie widzialy:** K1-3a sprawdzal KR-9 (kasy razem >= zapas - 1.13, TAK) i wpisal ryzyko "194 tys. zl/d przestaje plynac, zawor w dol" z kompensata przez P1 2.5%; P3 policzyl wzrost kies jako INFO (ZB-15); W7-7 / LUP-19 / L5B-8 porownuja d93-120 z "bieg roczny kodu 43: 101.4 tys. d337-364" (inne okno - patrz Z7), wiec 101.3 wyglada jak "bez zmian". Dopiero zestaw (K1-3a + P3/P5 + zapas x m_t + 11c) daje -20% doplywu do kas i -7% wojska.

**Skutek w grze:** lud siedzi na 29 mln zl i co dobe dokłada ok. 0.1 mln, kupcy w 8+ miastach stale ponizej zapasu (zaulki: nie kupuja plonu, nie placa naszych wyplat), panowie maja mniej z zaworu, druzyny mniejsze o kilka tysiecy ludzi - w biegu rocznym zagrozony prog 85 tys.

**Jak naprawic:** (1) najpierw bieg A/B 120 dob tej samej wersji: `TownsfolkBuyOnlyWorkHorses` off vs on (P3 wlaczone) - rozdziela wklad koni od P3 (kampanie roznia sie o +-5 tys. wojska). (2) Jesli potwierdzone: w `HistoricalPrices.BudgetPostfix` (`:1011`) nie zerowac budzetu koni, tylko przelozyc go na pozostale kategorie koszyka tej osady (ta sama suma budzetu domow, konie nadal nie znikaja) - lud dalej placi kasom tyle co przed regula, zawor wraca. Alternatywa gorsza: podniesc stawke P1 wedlug przyrostu lokaty (pieniadz idzie wtedy do koron, nie do kupcow i panow zamkow). (3) Po poprawce: `--grupa kiesa1 --regula-koni` (P1-2 <= +10 tys./d) i wojsko d93-120 wobec biegow tego samego okna.

### Z2 [WAZNE] L5b-1 zostawia przegranemu "zapas wojny + dlug Banku", a gdy padla stolica byla OSTATNIA twierdza, krolestwo upada i ten zapas znika ze swiata; Bank traci kredyt

**Strony:** `CapitalTreasury.cs:213-262` (`Fall`: zabrane = floor(CapitalTreasuryShare x (skarbiec - zapas wojny - dlug Banku)); `:201` - tylko krolestwo jeszcze nie wyeliminowane) wobec `CirculationWindows.cs:1581-1592` (`OnKingdomDestroyed` - skarbiec upadlego krolestwa tylko liczony jako ujscie) i `MoneyLedger.Obieg.cs:373` ("skarbce upadlych krolestw"), `:652` / `CrownBorrow.cs:228` ("Bank stracil dzis (krolestwa zniszczone)").

**Scenariusz:**
1. Krolestwu zostaje jedno miasto; BK (`BKCapitalBehavior`, DailyTick) po utracie stolicy przenosi ja na pozostale lenno, wiec zwykle ostatnia twierdza JEST stolica - L5b-1 rozpoznaje stolice wedlug BK (gdy BK nie zdazyl - nic nie bierze, a skarbiec i tak przepada w kroku 3).
2. Szturm: `OnSettlementOwnerChanged` BySiege -> `Fall`: przegrany oddaje 1/3 nadwyzki ponad zapas wojny (min. 500 000 - CrownReserveGold) i dlug Banku; reszta zostaje "na zwrot zoldu wojska przegranego".
3. Ta sama doba: krolestwo bez twierdzy zostaje zniszczone, `KingdomBudgetWallet` wypada z posiadaczy - zapas wojny i 2/3 nadwyzki ida w nicosc, kredyt Banku przepada.
4. Dowod (bieg roczny `_19-21-12`, kod bez L5b): doba 109146 - `WarLedger: town_K2 wziete obleczeniem` (20:27:39), 5 s pozniej linia Obieg: skarbce -676 649, "skarbce upadlych krolestw -633 826", "krolestw zniszczonych 1", Bank stracil 154 497; doba 109163 - `ROT_town15 wziete obleczeniem`, skarbce -606 853, Bank stracil 94 668. Razem ok. 1.2 mln zl i 0.25 mln kredytu w roku.

**Skutek w grze:** regula "skarbiec przegranego nigdy ponizej zapasu wojny" chroni wojsko krolestwa, ktorego za chwile nie ma; zdobywca dostaje 1/3, reszta (zwykle > 0.5 mln) przepada; Bank traci kapital - "nic w nicosc" zlamane w kazdym upadku krolestwa.

**Jak naprawic:** w `Fall` sprawdzic, czy przegrany ma po tej zmianie jeszcze jakas twierdze (`loser.Fiefs` bez `st`); jesli nie - najpierw splata dlugu Banku ze skarbca (186 - przelew skarbiec -> kapital Banku), cala reszta do zdobywcy (te same linie i `ShiftSnapshot`). Szerzej: nasluch `KingdomDestroyedEvent` przed `CirculationWindows.OnKingdomDestroyed` - pozostaly skarbiec: dlug -> Bank, reszta -> krolestwo, ktore wzielo ostatnia twierdze (zapamietane w `CapitalTreasury.OnOwnerChanged`); obejmuje tez upadek bez szturmu stolicy.

### Z3 [DROBNE] Lup L2 zdejmuje kase kupcow poza naszym tickiem bez pozycji w "Przeplywy osad" - laduje w "reszta - inne niezmierzone"

**Strony:** `CitySack.Coin.cs:76-78` (`town.ChangeGold(-want[0])` bez `MoneyLedger.Note`) i `:132` (zwrot przy wyjatku) wobec `MoneyLedger.cs:79-82` (pozycje "Armoury poza tickiem dobowym": NGear..NFolk - brak lupu).
**Dowod:** "Przeplywy osad (kasy zamkow)", reszta [R]: d37 12 454 / **d38 5 527** / d39 12 326 - lup ROT_castle18 d38 zdjal kupcom 7 749; d82 8 932 / **d83 -3 047** / d84 12 407 - lup ROT_castle33 zdjal 15 873. W miastach (lup ROT_town9, 32 872 zl) ginie w szumie reszty (-115..-317 tys. zl/d).
**Skutek:** kontrola per posiadacz nie odroznia lupu od wycieku; przy L4 (wiecej grabiezy) reszta kas rosnie bez nazwy. Suma swiata dobra (to przelew).
**Jak naprawic:** nowa pozycja `NSack = 10` w `MoneyLedger` (+ nazwa "lup miast (L2): kasa kupcow -> sakiewki i wodzowie"), `MoneyLedger.Note(NSack, st, -took[0])` po `:78` i `+rest` w galezi zwrotu `:132`.

### Z4 [DROBNE] L3 ("3 doby bez zoldu w lupionym miescie") nie obejmuje dziennych zakupow ludzi gracza z sakiewki

**Strony:** `CitySack.Window.cs:31-51` (`NoSpend` - wolany tylko z `MenPurse.OnLeft` `:288`/`:315` i `Sutlers.cs:224`) wobec `MenPurse.cs:240-251` (`OnDailyTickParty`: `BuyPlayerGaps` `:462-` i `MenUpgrade.ForPlayer` - sakiewka ludzi gracza -> polka i kasa tego miasta).
**Scenariusz:** gracz w armii zdobywcy stoi w zlupionym miescie; wiadomosc "they spend nothing here" przy wyjezdzie, a co dobe postoju ludzie kupuja braki i lepszy sprzet z polki tej osady za sakiewke.
**Skutek:** regula decyzji 3 Jeffa ("zero zoldu wydanego tam przez 3 doby") lamana tylko u gracza; LUP-13 tego nie liczy.
**Jak naprawic:** w `OnDailyTickParty` przed `BuyPlayerGaps` - `if (CitySack.NoSpendCloth(st, main)) return;` (ta sama bramka co odziez), licznik w linii L3.

### Z5 [DROBNE] L7 laduje tabory do udzwigu, a zaraz potem L5b-2 dorzuca CALY ladunek karawan bez udzwigu

**Strony:** `CitySack.Goods.cs:44`, `:149-171` (wolny udzwig `InventoryCapacity - TotalWeightCarried`, nadmiar przy spustoszeniu plonie) wobec `EnemyCaravans.cs:210-236` (caly roster karawany do `cap.ItemRoster`). Kolejnosc gry (`MapEvent.FinalizeEventAux`): `OnMapEventEnded` (nastepstwa AI - L2/L7/LB) PRZED `SiegeCompleted` (zmiana pana -> L5b-1, L5b-2).
**Scenariusz:** armia AI szturmuje miasto z 2 karawanami wroga i wybiera spustoszenie: L7 wypelnia udzwig i pali reszte polek; postfiks `SiegeCompleted` dokłada ladunek karawan (z jucznymi, ale bez udzwigu eskorty) - druzyna przeciazona. U gracza kolejnosc odwrotna (karawany przed wyborem w menu) - L7 bierze mniej.
**Skutek:** dwie zasady na jedno zjawisko (lup towaru w zdobytym miescie); AI wolniejsza na mapie po zdobyciu.
**Jak naprawic:** w L5b-2 ten sam rachunek co L7: konie/juczne same, reszta najcenniejsze na kg do wolnego udzwigu, nadmiar zostaje na polce osady (albo plonie przy spustoszeniu - wedlug `Track` L2).

### Z6 [DROBNE] Zloto startowe GL-2: kiesy wsi nadal z ubytkiem po poprawce 2c0a225

**Strony:** `PeopleMarket.cs:645-697` (dwa przejscia + pula zwrotu) - poprawka "kiesy wsi przed = po".
**Dowod:** linia "Zloto startowe wedlug ludzi (GL2)" (log koncowy, wiersz 517): kiesy wsi przed 1 105 071 = po 1 100 819, "suma swiata ... (ROZJAZD)" -4 252 zl, potkniecie "StartGold(wies niepelna)" bez "wies pula". To samo w `_18-04` (-4 000), `_18-49` (-4 000), `_19-21` (-3 400). Gra (`SettlementComponent.ChangeGold`) tnie tylko ponizej 0, a cel `Spread` jest >= 0 - przyciecie robi cudza latka albo licznik `b1` (Max(0, Gold)) liczy inaczej niz `cur`.
**Skutek:** jednorazowo ok. 4 tys. zl z niczego w nicosc w dobie 1 nowej kampanii - poza kazda kontrola dobowa (pierwsza linia "Pieniadz swiata" juz po przelewie).
**Jak naprawic:** w przejsciu 1 i w zwrocie zapisywac `real` do listy i liczyc `b1` jako `cur + suma real` (nie z `Gold`), a przy `real != d` logowac id wsi i oba stany - wtedy widac, czyja latka tnie.

### Z7 [DROBNE] Progi wojska porownuja okno d93-120 z koncem roku innego biegu

**Strony:** `tools/sprawdz_logi.py:16364` (L5B-8), `:17895` (W7-7), `:19185` (LUP-19) - "bieg roczny kodu 43: 101.4 tys. d337-364".
**Skutek:** 101.3 tys. w biegu koncowym wyglada jak "bez zmian", choc w tym samym oknie d93-120 piec biegow 120 dob mialo 105.7-116.5 tys. (B 109.5) - Z1 schowane.
**Jak naprawic:** odniesienie z tego samego okna (lista biegow 120 dob: 105.7 / 116.5 / 113.9 / 112.6 / 109.5) albo `--baza-doby` z biegu bez paczki.

### Z8 [DROBNE] W7 nie zapisuje poboru w kolumnie wydatkow osady (kiesa-ludu.csv)

**Strony:** `TownFolk.LordLevy.cs:84-97` (`LevyTake`: `_ctlOut += x` bez `f.LDayOut += x`; `LevyBack` tak samo) wobec `TownFolk.Sack.cs:47-48`, `TownFolk.Horses.cs:197`, `TownFolk.Works.cs:98-100`, `TownFolk.Savings.cs:89` (wszystkie dopisuja `LDayOut`) i wiersza CSV `TownFolk.Live.cs:1586`.
**Skutek:** kontrola calosci r = 0 dobra, ale wiersz osady w kiesa-ludu.csv w dobie poboru W7 sie nie bilansuje (stan - wejscia + wyjscia != stan wczoraj) - recenzja L2 dopisala `LDayOut` wlasnie dla tego bilansu.
**Jak naprawic:** `f.LDayOut += x` w `LevyTake` i `-= amount` w `LevyBack`.

## 3. Obserwacje bez wagi

- Reszta "Przeplywy osad (kasy zamkow)" d30-120: 12.5-13.1 tys. zl/d (B 5.4-7.7, A 5.4, roczny B 7.2-12.5) - wyzej, ale w rozrzucie biegow; jesli wroci w A/B z Z1 - sprawdzic latke P4 na `BEE_SettlementEconomyModel` (jedyna nowa latka modelu ekonomii zamkow).
- P1b (migawka kary z bloku N, nakladana w `Town.DailyTick` doby N+1): miasto zdobyte w dobie N+1 dostaje kare za danine starej korony - po zdobyciu lojalnosc < 60, wiec podloga P1b daje 0; bez skutku.
- N7 odbiera ludzi rano wedlug wczorajszego limitu zoldu (przed `ClanBudget.Daily`), W5/W7 placa potem za ludzi, ktorzy SA w druzynach (takze N7) - spojne, bez podwojnej zaplaty.
- EnemyCaravans: kiesa karawany 100% wodzowi (1/9 korona), moneta miasta L2 - prawo trzecich 2/3 ludziom; rozne, ale z uzasadnieniem w kodzie (karawana to partia - regula 3.2).
