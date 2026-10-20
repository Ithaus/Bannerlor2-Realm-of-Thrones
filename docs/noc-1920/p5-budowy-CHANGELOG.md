# p5-budowy - jedna budowa na rod naraz (R3-11a, POMYSLY-35 nr 34), galaz w-toku/p5-budowy na 5189c0fe (kandydat WGRANIA 92)

## Dla Jeffa (prosto)
Dotad pan AI dzielil pieniadze na budowy po rowno na wszystkie swoje zamki i miasta z budowa, wiec kazda szla zolwim tempem.
Teraz caly dzienny budzet budow rodu idzie na jedna budowe naraz: w wojnie na mury (wieze, koszary) warowni najblizej wroga, w pokoju na budowe
najblizsza konca - te, ktorej najwieksza czesc jest juz zrobiona (miasto w polowie gotowe idzie przed swiezo zaczetym zamkiem, choc zamek jest tanszy).
Pan daje najwyzej tyle, ile brakuje do konca poziomu; co zostanie (albo gdy na targu nie ma materialu) - idzie na nastepna budowe, jesli starczy
choc na jeden ladunek materialu, reszta czeka w kolejce. Twoich lenn to nie dotyczy (dalej dzielisz po rowno), a stare zasady zostaja: budowle cywilne przy froncie stoja, mieszczanie
dalej sami doplacaja do swoich budowli. Mozna to wylaczyc w MCM: "Build One Work Per House".

## Problem / zrodlo pomyslu
- `docs/POMYSLY-35-2026-10-20.md` nr 34 (R3-11a), zrodla: `docs/AUDYT-2026-10-05-EKONOMIA-WOJNA-SUROWCE-LORDOWIE.md` (P8, "Decyzja Jeffa" i poprawka
  weryfikatora), `docs/PLAN-DO-KONCA-MODA-2026-10-09.md:279`. Jeff 20.10: "28 - nie, 29 - nie. Pozostale tak".
- Kod (5189c0fe) `BuildFunding.Daily`: `budget = przydzial rodu / count[clan]` - kazda osada rodu z budowa dostaje rowna czesc, budowy koncza sie
  wszystkie naraz po latach.
- Log WGRANIA 91 (`Modules/Armoury/Armoury-2026-10-20_00-44-29.log`, d109236, koniec biegu 160 dob): "Budowy oplacone: osad 60, wydano 13222,
  punktow budowy 350" (srednio 5.8 pkt na oplacona osade); "wstrzymane: brak materialow 26" (tego dnia przydzial tych osad lezal w kiesie, choc
  inne lenna rodu mogly go wziac); dziennik "Budowy: w budowie 223 (idzie 78, stoi 145, ukonczonych poziomow 0), srednia moc 3.2 pkt/dzien".

## Zmiana (Armoury, nowy klucz `BuildOneWorkPerHouse` "Build One Work Per House", domyslnie TAK, grupa MCM jak Build Income Share)
1. Nowy plik `Armoury/src/BuildFunding.OneWork.cs` (partial BuildFunding):
   - `OnePlan` (raz na dobe, po zebraniu budow i dopuszczonych cywilnych w wojnie): rody AI z co najmniej 2 budowami w rozliczeniu doby; ich budowy
     ustawione w kolejce rodu. Rod w wojnie (krolestwo w wojnie z krolestwem - ta sama definicja co wstrzymanie cywilnych) z budowla wojskowa:
     budowle wojskowe wedlug odleglosci warowni do najblizszej wrogiej warowni (miasto/zamek frakcji w wojnie z frakcja rodu; bez wrogiej warowni -
     za nimi, wedlug konca poziomu). Pokoj (i wojna z samymi cywilnymi dopuszczonymi z dala od frontu): najmniejszy ULAMEK kosztu poziomu do zrobienia
     ((koszt - postep) / koszt, jak lud w `FolkTarget`); remis - mniej punktow do konca, potem kolejnosc osad gry. Poziom 3 (koszt 0) - na koniec.
   - `OneBudget`: budowa dostaje reszte przydzialu doby rodu, najwyzej brak do konca biezacego poziomu w pensach (odwrotnosc `PointsFor`: udzial
     materialu `BuildMaterialShare`, place wedle miasta targowego `WageIdx`, minus premia za zloto `Boost`; zapas 5% + 10 pensow - nadwyzka punktow
     zostaje w budowli, bo `Building.LevelUp` odejmuje koszt). Nic do wziecia (reszta przydzialu albo kiesa pana < 1): rod juz dzis zaplacil za inna
     budowe - osada czeka w kolejce; nie zaplacil (przydzial 0, pusta kiesa) - licznik "rod bez przydzialu albo kiesy", bez oznaczenia "czeka".
     Reszta po pierwszej oplaconej budowie idzie dalej tylko, gdy material z niej (reszta x `BuildMaterialShare`) kupi choc jedna sztuke na targu tej
     budowy (`OneCheapest` - ten sam wybor co `BuyOneCheapest`, bez kupna); inaczej osada czeka ("reszta za mala na sztuke materialu"). Brak materialu
     na targu - jak dotad ("wstrzymane: brak materialow", reszta idzie dalej). Poziom juz zaplacony (czeka na tick) - 0, przydzial idzie dalej.
   - `OnePaid`: wydane (material + place) odejmowane od reszty przydzialu; czego budowa nie wziela (poziom do konca, brak materialu na targu -
     wtedy place najwyzej 3 x material jak dotad) idzie na nastepna budowe kolejki.
   - Rod z jedna budowa i rod gracza: `OneBudget` zwraca dotychczasowy podzial po rowno - bez zmian co do monety.
2. `BuildFunding.cs` (minimalne wpiecia): `OnePlan(jobs, count)` przed petla, przydzial rodu liczony raz jako `all` (podzial `all / count` - ta sama
   arytmetyka co dotad), `OneBudget` przed ograniczeniem kiesa pana (obliczenie `ppp` i targu przeniesione przed to ograniczenie - tylko obliczenia),
   `OnePaid` po zaplacie, `OneReset` w `Reset`, `OneTail()` w linii "Budowy oplacone" PRZED dopiskiem N5-E (`WarFront.LineTail()` zostaje na koncu).
3. `BuildDiary.cs`: osada, ktora w POPRZEDNIM rozliczeniu czekala w kolejce rodu, ma w linii "Budowa:" dopisek "(wczoraj czekala w kolejce rodu -
   R3-11a)", a podsumowanie "Budowy:" - "w kolejce rodu (R3-11a) N (z tego stoi M)". Dziennik mierzy postep od wczoraj, a ten postep pochodzi
   z punktow poprzedniego rozliczenia (osady tykaja rozlozone w dobie - `CampaignPeriodicEventManager._dailyTickSettlementTicker`), wiec czyta
   kolejke poprzedniej doby (`OneWaitedLastDay`).
4. `tools/sprawdz_logi.py`: bramki N5-5 (`--grupa etap8`) i T2-K1-3 (`--grupa towary2`) licza "idzie" + osady stojace tylko w kolejce rodu
   (`r311_go`: "z tego stoi M"); log bez dopisku R3-11a - samo "idzie", jak dotad.
5. `Settings.cs` + `tools/gen_mcm.py` -> `McmSettings.cs` (2065 ustawien).

## Linia logu (log_grep)
Koniec linii "Budowy oplacone: dzien N ...":
`| R3-11a jedna budowa na rod naraz (Build One Work Per House tak): rody AI z kilkoma budowami N (budow M; kolejnosc - w wojnie mur najblizej wroga
N rodow, najblizsza konca poziomu (ulamek) N); oplacone budowy tych rodow N (pierwsza z kolejki N, dalej z reszty przydzialu N), punktow N (srednio
X na budowe), poziom zaplacony do konca N, poziom juz zaplacony N; czeka w kolejce rodu N osad (z tego reszta za mala na sztuke materialu N), rod bez
przydzialu albo kiesy N budow; potkniecia N.`, a po nim dopisek N5-E (`| N5-E budowle cywilne z dala od frontu: ... potkniecia N.`).
Dziennik: "Budowy: dzien N - ... bez budowy N, w kolejce rodu (R3-11a) N (z tego stoi M); srednia moc ..." i w liniach "Budowa:"
"(wczoraj czekala w kolejce rodu - R3-11a)".
Klucz wylaczony: `| R3-11a jedna budowa na rod naraz: WYLACZONE (Build One Work Per House) - ...`.

## Ryzyko / co sprawdzic (kontrola calosci)
- **Regresje**: przy kluczu wylaczonym `_oneMulti` puste - `OneBudget` zwraca `budget` bez zmian, kolejnosc `jobs` bez zmian; jedyna roznica to
  kolejnosc obliczenia `ppp`/`NearestTown` przed `Math.Min(budget, lord.Gold)` (czyste obliczenia, `NearestTown` z pamiecia). Rody z jedna budowa
  i gracz - jak dotad przy wlaczonym kluczu.
- **Zamknieta ekonomia**: platnik i odbiorcy bez zmian (kiesa pana -> kasa targu za sztuki zdjete z polki, kasa osady / kiesa ludu za place).
  Suma wydana przez rod <= przydzial doby (reszta przydzialu maleje o kazde wydanie); jedyny wyjatek to stary `BuyOneCheapest` (jedna najtansza
  sztuka do calej kiesy) przy PIERWSZEJ oplaconej budowie rodu - jak dotad, reszta wtedy schodzi ponizej 0 i kolejne budowy czekaja. Budowa z reszty
  dostaje pieniadze tylko, gdy material z nich kupi sztuke (`BuyMaterials` kupuje wtedy w budzecie, a place <= budzet - material), wiec nie wola
  `BuyOneCheapest`. Czego budowy nie wziely - zostaje w kiesie pana.
- **Kolizje**: roboty ludu (P2) licza swoj brak do konca poziomu od `t.Construction` (z punktami pana tej doby) - nie dopelnia drugi raz tego, co
  zaplacil pan; WarFront (N5-E) i "mury pierwsze" bez zmian (cywilne w wojnie dalej tylko u rodu bez budowy wojskowej); zgliszcza na czele kolejki
  osady (CitySack.Burn) bez zmian (dotyczy kolejki osady, nie rodu); DayHeld - `_funded` jak dotad.
- **Tempo swiata**: kolejka nie zwieksza sumy punktow rodu (uwaga weryfikatora audytu) - zmienia kolejnosc: budowy koncza sie jedna po drugiej.
  Rody bogate z duzym przydzialem moga wydac mniej niz dotad (sufit "do konca poziomu" - dotad nadwyzka szla w postep budowli). Rody, ktorych
  jedna budowa stala bez materialu, teraz placa za inna (dotad przydzial lezal) - suma punktow swiata moze lekko wzrosnac.
- **Skaczacy cel**: AI gry przestawia kolejke budowli osady (ok. 10% na dobe) - cel rodu moze sie zmienic, postep budowli nie przepada (jest
  w budowli). Regula bez pamieci miedzy dobami (bez pol zapisu).
- **Koszt**: raz na dobe; odleglosci tylko dla rodow w wojnie z kilkoma budowami (budowy x 227 warowni).
- **Bramki autotestu**: z zalozenia reguly mniej osad ma postep kazdego dnia ("idzie" w "Budowy:" spada). N5-5 i T2-K1-3 (prog "idzie >= 80")
  licza teraz "idzie + stoi w kolejce rodu" (`r311_go`). Uwaga: na WGRANIU 91 (bez R3-11a, doby 109076-109236) obie bramki juz pokazuja NIE
  (77.5 i 77.6 < 80) - to nie jest skutek tej galezi. N5-5 czyta potkniecia N5-E wyrazeniem `potkniecia (\d+)\.$` - dopisek R3-11a stoi
  przed N5-E, wiec bramka dalej czyta N5-E. T2-K1-7 (punkty pana + ludu) - suma punktow podobna; reszta ponizej ceny sztuki zostaje w kiesie (mniej
  wydane o grosze na rod).
- Sprawdzic w logu: "oplacone budowy tych rodow" + "czeka w kolejce rodu" + "poziom juz zaplacony" + "rod bez przydzialu albo kiesy" + "wstrzymane:
  brak materialow" (tych rodow) ~ "budow"; "dalej z reszty przydzialu" wyraznie mniej niz "pierwsza z kolejki" (reszta tylko, gdy kupi sztuke);
  "srednio X na budowe" wyraznie wieksze niz srednia linii (punkty / osady); "poziom zaplacony do konca" > 0; "potkniecia 0"; w dzienniku
  "ukonczonych poziomow" wiecej niz dotad; suma "wydano" bez skoku; "w kolejce rodu (R3-11a) N (z tego stoi M)" - M blisko N (czekajace stoja,
  chyba ze doplaca lud albo premia za zloto).

## Decyzje (projektuje Claude)
- Tylko rody AI: lenna gracza dziela przydzial po rowno jak dotad - gracz sam ustawia kolejke w kazdym lennie i nie widzi kolejki rodu.
- Reszta przydzialu, ktorej pierwsza budowa nie wziela, idzie na nastepna - pieniadz nie lezy, gdy na targu czola nie ma materialu.
- Sufit "do konca poziomu" tylko w rodach z kolejka (rod z jedna budowa - bez zmian).
- "Najblizej wroga" = najblizsza wroga warownia w linii prostej (stabilne z dnia na dzien), nie chwilowe zagrozenie WarFront.
- "Najblizsza konca" = najmniejszy ulamek poziomu do zrobienia, nie najmniej punktow: oplacana budowa co dzien ma mniejszy ulamek, wiec zostaje na
  czele do konca poziomu (bez pamieci miedzy dobami i bez pola zapisu), a zamki nie wyprzedzaja miast tylko dlatego, ze sa tansze.
- Reszta przydzialu idzie dalej tylko, gdy kupi choc jedna sztuke materialu na targu nastepnej budowy - grosze zostaja w kiesie pana.
- "Czeka w kolejce rodu" tylko wtedy, gdy rod juz dzis za cos zaplacil; rod bez przydzialu albo bez kiesy - osobny licznik.

## Recenzja (PELNA) i poprawki
1. WAZNE - kolejnosc w pokoju wedlug samych brakujacych punktow (zamki zawsze przed miastami, swiezo zaczeta tania budowa przed zaczeta):
   PRAWDA (zamek 350-1120 pkt na poziom, miasto 1800-5400; d109236: zamki sredni brak 845, miasta 2173). Poprawione: klucz = ulamek poziomu do
   zrobienia, remis - punkty, potem kolejnosc osad. W wojnie bez zmian (odleglosc do wroga; drugi klucz - ulamek).
2. WAZNE - bramki N5-5 i T2-K1-3 (prog "idzie >= 80") i wyrazenie potkniec N5-5 zakotwiczone na koncu linii: PRAWDA. Poprawione: `OneTail()`
   przed `WarFront.LineTail()`; dziennik podaje "(z tego stoi M)" dla osad czekajacych w kolejce rodu, a obie bramki licza "idzie + M"
   (`tools/sprawdz_logi.py`, `r311_go`). Sprawdzone na kopii logu WGRANIA 91 (`--grupa etap8` / `--grupa towary2 --start-kampanii 109075
   --do-doby 160`): bez wyjatkow, wartosci jak dotad (77.5 / 77.6 - log bez R3-11a).
3. DROBNE - reszta przydzialu kapie co dzien na druga budowe przez `BuyOneCheapest` do calej kiesy i przekracza przydzial: PRAWDA. Poprawione:
   reszta idzie dalej tylko, gdy material z niej kupi sztuke na targu tej budowy (`OneCheapest`), inaczej osada czeka; licznik "reszta za mala na
   sztuke materialu".
4. DROBNE - liczniki "czeka" i dopisek w dzienniku: PRAWDA. Poprawione: "czeka" tylko, gdy rod juz dzis zaplacil za inna budowe (przydzial 0 albo
   pusta kiesa - "rod bez przydzialu albo kiesy"; obciecie kiesa pana liczone w `OneBudget`); dziennik czyta kolejke poprzedniej doby, z ktorej sa
   dzisiejsze punkty (dopisek "wczoraj czekala w kolejce rodu"). "pierwsza z kolejki" = pierwsza oplacona budowa rodu tej doby.

## Status
DO SPRAWDZENIA - zbudowane (Armoury, 0 bledow), bez testu w grze (zakaz uruchamiania w tym zadaniu).

## Test w grze
Autotest 120 dob z zapisu: grep `R3-11a jedna budowa na rod` w Armoury.log - liczniki jak wyzej; porownac "Budowy: ... ukonczonych poziomow"
i "srednia moc" z WGRANIEM 91 (d109236: 0 poziomow, 3.2 pkt/dzien); w liniach "Budowa:" osady z dopiskiem "(wczoraj czekala w kolejce rodu -
R3-11a)"; `python -I tools/sprawdz_logi.py --grupa etap8 <log>` i `--grupa towary2` - wiersze N5-5 i T2-K1-3 ("idzie (z czekajacymi w kolejce rodu
R3-11a)").
