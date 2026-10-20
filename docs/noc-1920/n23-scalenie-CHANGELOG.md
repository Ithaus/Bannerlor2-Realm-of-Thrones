## 2026-10-20 (n23: poprawki po recenzji scalenia fali 2 - p6-szubienice, p6-pola, p6-sztormy, p6-okup, p6-drogi, p6-spichlerz, p6-a7)

**Mody:** Armoury (TownGranary.cs, StartCalibration.Arm.cs, MoneyLedger.Pumps.cs, Settings.cs, McmSettings.cs z gen_mcm), tools/gen_mcm.py | **Galaz:** `w-toku/n23-calosc` (baza `3c3a7ad9` = n22-calosc + 7 galezi fali 2, scalenie `5f2c89e2`) | **Tor:** M (bez gry, bez autotestu; 2 recenzje: ekonomia, technika; build Armoury, CrashScribe, GrandTourney kod 0; gen_mcm stabilny, 2124 ustawien) | **Status:** DO SPRAWDZENIA (autotest przed WGRANIEM)

**Dla Jeffa (prosto):**
Spichlerz miejski z bramka glodu gryzly sie ze soba: gdy miasto mialo za malo jedzenia, wsie sprzedawaly mu zboze az do roku zapasu, a to jest dokladnie granica, ponizej ktorej bramka glodu uznaje kraine za "oprozniona wojna" i po tygodniu chudszych racji (zima, przebudowy zajec) wraca ja do starych zasad. Teraz wies w takiej sytuacji zostawia sobie rok i kwartal zapasu (455 dni: rok plus caly kwartal, ktory bramka pozwala stracic), a samej sprzedazy zboza miastu bramka nie liczy juz jako straty spichlerza. Do tego dwie drobne poprawki: suwak ochrony wsi po okupie siega teraz roku (364 dni, jak w kodzie, zamiast 84), a linia "Pompy" w logu liczy tez pieniadze, ktore kasy miast oddaja wsiom za roboty (p6-a7).

### Znaleziska i wynik

| # | soczewka / waga | znalezisko | wynik |
|---|---|---|---|
| 1 | ekonomia / wazne | TownGranary w niedoborze miasta sprzedaje stodoly wsi do 50% sufitu = 364 dni = `KsLandGateReserveDays` - bramka krainy traci budzet straty 91 dni; dzien sprzedazy liczy sie jako spadek | **poprawione** (podloga 455 dni + sprzedaz poza spadkiem) |
| 2 | ekonomia / drobne | duzy zakup zboza w niedoborze zbija doplyw netto kupcow (TownPurse.CloseWindow) - przeglad firm DS moze sciac firme o 15% na kwartal | **odrzucone** - nie wada (patrz pkt 2) |
| 3 | ekonomia / drobne | linia "Pompy - sztuczne przelewy (nekon)" nie widzi zwrotu TownHire (kasy miast -> kiesy wsi bez towaru) | **poprawione** (pozycja w grupie A, tylko log) |
| 4 | technika / drobne | suwak MCM `RaidTributeSpareDays` 1..84, a kod i komentarz 1..364 | **poprawione** (1..364, gen_mcm) |

### 1. Spichlerz miejski i bramka krainy KS (poprawione)

**Sprawdzone w kodzie (prawdziwe).**
- `TownGranary.cs`: `KeepShareShort = 0.5`, w niedoborze `sur = food - KeepShareShort * vcap`. Sufit spichlerza wsi `Famine.cs:702` `x.Cap = years * yd * x.L` (FamineBarnYears 2 x 364 dni x potrzeba dnia `x.L = Hearth x k + uchodzcy`), wiec podloga = 364 dni potrzeby wsi.
- Bramka `StartCalibration.Arm.cs LandGateDaily`: `need = Hearth x k + uchodzcy` (ta sama potrzeba co Famine), `days = a.Food / a.Need`, `count = fall && (... || lossC >= lossKey || days < reserve)` przy `KsLandGateReserveDays = 364`. Kraina z zapasem ponizej 364 dni liczy serie spadku od pierwszej doby, bez budzetu `KsLandGateLossDays = 91`.
- Kolejnosc doby (`ArmouryBehavior.cs`): `Famine.Daily` (2504) -> `TownGranary.Daily` (2574) -> `StartCalibration.Daily` (2749) -> `LandGateDaily`, ktora czyta `Famine.FoodOf` juz po sprzedazy. Dzien sprzedazy daje `dv = prev - food` z cala sprzedaza: w mierze C (`KsLandGateExactLoss` TAK) ucina to `min(dv, gap)` do luki racji KS, ale w mierze A (klucz NIE - jest w MCM) jeden dzien sprzedazy dopisuje do straty krainy setki dni potrzeby naraz.
- Bez p6-spichlerz stodoly wsi z nadwyzka stoja przy suficie (ok. 700 dni), wiec liczy sie tylko budzet 91 dni. Z p6-spichlerz wies przy miescie w niedoborze schodzi do 364 dni i w zimie (bez plonu) zjada dalej ponizej progu - kraina zalezna od takich wsi wraca do starych zasad po 7 dobach spadku z winy KS.

**Zmiana (minimalna, bez nowego klucza).**
- `TownGranary.ShortKeepShare`: w niedoborze miasta wies zostawia sobie `max(KeepShareShort 50%, (Ks Land Gate Reserve Days + Ks Land Gate Loss Days) / sufit w dniach)`, najwyzej `KeepShare` 98%. Domyslnie (364 + 91) / 728 = 62.5% sufitu = 455 dni. Sufit w dniach jak w Famine.Daily (`FamineBarnYears x CampaignTime.DaysInYear`). Klucz bramki 0 (projekt 16.10 doslownie - spadek liczy sie zawsze) - zostaje 50%, bo podloga nie ma czego chronic. Nowa wlasciwosc `StartCalibration.LandGateFloorDays` (te same przyciecia co klucze bramki - jedno miejsce).
- `TownGranary.SoldToday` + `DayBook.Sold`: racje sprzedane dzis z kazdej wsi. `LandGateDaily`: `if (dv > 0) dv = max(0, dv - SoldToday)` - sprzedaz to targ, nie spadek. Spadek najwyzej do zera: sprzedaz nie udaje napelniania (inaczej wies przy suficie, ktora co dobe sprzedaje 2% ponad 98%, "napelnialaby sie" codziennie i zwracalaby krainie strate, ktorej bez p6-spichlerz by nie zwrocila - tam nadwyzka ponad sufit przepada).
- Opis MCM `TownGranaryFromVillages` (EN): "a year and a quarter when the town runs short - the famine gate's reserve and its loss allowance"; gen_mcm.
- Linia "Spichlerz miejski (p6)": w nawiasie progu niedoboru dopisane "wsie zostawiaja sobie X% sufitu spichlerza" (regex P6SPICHLERZ_DAY `\([^)]*\)` - sprawdzony na pelnej linii, grupy short/shv czytane jak dotad).

**Ryzyko / co sprawdzic.**
- Miasto w niedoborze dostaje mniej z jednej wsi: do 98% - 62.5% = 35.5% sufitu wsi (ok. 258 dni jej potrzeby) zamiast 48%. Jednodniowy zakup i tak tnie `room` do 2 x progu niedoboru miasta (`TownGranary.cs` "niedobor: miasto kupuje ... tylko do podwojnego progu"), wiec w pierwszej dobie zwykle nic sie nie zmienia; roznica wychodzi dopiero w miescie w przewleklym niedoborze (okreg nie nadaza) - wsie zostaja na 455, nie 364 dniach. Miasto, ktore wyczerpie te nadwyzke, dalej ma targ, wozy BK i dokup BK z polki (bez zmian).
- Podloga dziala zawsze (takze przed wlaczeniem KS): kraina wchodzi do KS z wsiami na 455 dniach, a nie na granicy bramki. Bez KS jedyny skutek - jak punkt wyzej.
- Bramka: w dniu sprzedazy spadek wsi nie liczy sie; spadek z Famine.Daily (plon minus jedzenie, zima, psucie) liczy sie jak dotad. Inne zdjecia ze spichlerzy (Mur, Wolni Ludzie, rabunek) bez zmian.
- Zmiana `Ks Land Gate Reserve Days` / `Loss Days` w MCM przesuwa tez podloge spichlerza miejskiego (zamierzone - jedna miara).
- Watki: `SoldToday` czyta tylko watek glowny (LandGateDaily); DayBook podmieniany w calosci jak dotad, postfiksy z watkow AI czytaja tylko `Map`.

**Test w grze.**
- Autotest 120 dob z KS: linia "Spichlerz miejski (p6)": "wsie zostawiaja sobie 62.5% sufitu spichlerza"; "kontrola: racje zdjete = ubytek stodol wsi" dalej rowne. Linia "KS: bramka krainy": przy miastach w niedoborze (Vaes Diaf, New Ghys, Vaes Dothrak, Yronwood - recenzja z biegu 07-52-35) zapas krain tych miast nie schodzi do 364 dni od samej sprzedazy, "powroty dzis" bez krain, ktorych jedyna przyczyna to sprzedaz miastu; seria spadku nie zaczyna sie w dniu, gdy miasto wchodzi w niedobor.
- Z `KsLandGateExactLoss` NIE (miara A): strata krainy nie skacze o setki dni w dniu pierwszej sprzedazy w niedoborze.

### 2. Duzy zakup zboza w niedoborze w doplywie netto kupcow (odrzucone - nie wada)

**Zarzut.** `TownPurse.CloseWindow` liczy zakup TownGranary jak zwykly wydatek kasy; jednorazowy duzy zakup w niedoborze obniza srednia 28 dob i przeglad firm DS moze sciac firme pana i korony o 15% na kwartal.

**Dlaczego bez zmian.**
- Regula doplywu netto to "to, co kupcy zarobili sami", a jedzenie miasta jest jego wydatkiem - tak samo liczy sie jedzenie zaplacone przez pana (`_lordFoodWin`: "liczone jak wydatek kasy na to jedzenie", recenzja calosci 18.10). Zakup w niedoborze to jedzenie mieszczan na kolejne 1-2 miesiace (zakup ograniczony do 2 x progu niedoboru: 30 dni spozycia albo 30% sufitu), czyli zwykly koszt zywnosci, tylko zaplacony z gory.
- Blad pomiaru jest ograniczony i na korzysc miasta: `DsTowns.Review` zmienia firme najwyzej o `DsFarmRenewMax` (15%) na przeglad co 91 dni, nastepny przeglad oddaje do +15%. Wylaczenie zakupu z doplywu (propozycja recenzji) dawaloby blad odwrotny i trwaly: miasto w przewleklym niedoborze (najbiedniejsze) wygladaloby na bogatsze o caly koszt swojego jedzenia, a firma pana i korony rosla by z pieniedzy wydanych na chleb - to wlasnie blad, ktory `_rebLoopWin` (p6-a7) usuwal w druga strone.
- Rozlozenie zakupu na 28 dob wymagaloby nowego pierscienia na miasto w TownPurse - nie jest tanie ani potrzebne przy ograniczeniu 15%.

### 3. Pompy - roboty miast p6-a7 (poprawione, tylko log)

- `MoneyLedger.Pumps.cs PumpsLine`: nowa pozycja grupy A "roboty miast p6-a7" = `-_mark[CTown, MTownHire]` (zloto zdjete z kas miast w pozycji M31 tej doby - ta sama chwila co reszta linii; jak "korona z utargu wsi" `-_mark[CVill, MCrown]`). Wchodzi do A i do "razem" (przelew z urzedu, bez towaru - opis MCM `TownWorksHireDistrict`: "Only coin moves; nothing is built"). Przed startem reguly 0 - na liscie "uspione".
- Narzedzia: nic nie czyta wnetrza nawiasu grupy A (grep `zamki robocizna`/`kasy miast -> lud` w tools i docs - brak); NE-14/NE-14b czytaja tylko "razem" i "% zoldu" (INFO), NE3-11 tylko B, C i F. Liczba "razem" i "% zoldu" wzrosnie o zwrot TownHire - to nie zmiana znaczenia, tylko brakujaca pozycja.

### 4. Suwak ochrony wsi po okupie (poprawione)

- `tools/gen_mcm.py` RANGES `'RaidTributeSpareDays': (1, 364, "0")` (bylo 84 - dlugosc roku gry bez Armoury; kod `RaidTribute.SpareDays` przycina do 1..364, komentarz gen_mcm mowil 1..364). `McmSettings.cs` przegenerowany (gen_mcm drugi raz - plik bez zmian). Domyslna 30 bez zmian; wartosc w Armoury.json Jeffa (jesli jest) dziala jak dotad.

### Przypomnienie przy wgrywaniu (z recenzji techniki, bez zmian w kodzie)

- `Armoury.json` Jeffa ma `"KsSwitchSignalRule": false` (plik z 18.10) - nowa domyslna TAK z p6-a7 nie zadziala bez poprawki pliku (CLAUDE.md rozdz. 7; zapisane w `p6-a7-CHANGELOG.md` jako jeff_config_changes).

### Sprawdzenie

- `dotnet build -c Release` Armoury, CrashScribe, GrandTourney (GameLibs z lancucha): kod 0, 0 bledow; ostrzezenia Armoury te same 5 co przed zmiana (CS0169/CS0414/CS0649 w innych plikach).
- `python tools/gen_mcm.py` dwa razy: 2124 ustawien Armoury, drugi przebieg bez zmian (md5 rowne).
- Regex `P6SPICHLERZ_DAY` (sprawdz_logi) dopasowany do pelnej linii w nowym brzmieniu (short/shv czytane). sprawdz_logi.py bez zmian.
