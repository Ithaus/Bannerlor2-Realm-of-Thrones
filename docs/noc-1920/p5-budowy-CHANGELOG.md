# p5-budowy - jedna budowa na rod naraz (R3-11a, POMYSLY-35 nr 34), galaz w-toku/p5-budowy na 5189c0fe (kandydat WGRANIA 92)

## Dla Jeffa (prosto)
Dotad pan AI dzielil pieniadze na budowy po rowno na wszystkie swoje zamki i miasta z budowa, wiec kazda szla zolwim tempem.
Teraz caly dzienny budzet budow rodu idzie na jedna budowe naraz: w wojnie na mury (wieze, koszary) warowni najblizej wroga, w pokoju na budowe
najblizsza konca. Pan daje najwyzej tyle, ile brakuje do konca poziomu; co zostanie (albo gdy na targu nie ma materialu) - idzie na nastepna budowe,
reszta czeka w kolejce. Twoich lenn to nie dotyczy (dalej dzielisz po rowno), a stare zasady zostaja: budowle cywilne przy froncie stoja, mieszczanie
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
     za nimi, wedlug konca poziomu). Pokoj (i wojna z samymi cywilnymi dopuszczonymi z dala od frontu): najmniej punktow do konca poziomu. Remis -
     kolejnosc osad gry.
   - `OneBudget`: budowa dostaje reszte przydzialu doby rodu, najwyzej brak do konca biezacego poziomu w pensach (odwrotnosc `PointsFor`: udzial
     materialu `BuildMaterialShare`, place wedle miasta targowego `WageIdx`, minus premia za zloto `Boost`; zapas 5% + 10 pensow - nadwyzka punktow
     zostaje w budowli, bo `Building.LevelUp` odejmuje koszt). Reszta < 1 - osada czeka w kolejce (0). Poziom juz zaplacony (czeka na tick) - 0,
     przydzial idzie dalej.
   - `OnePaid`: wydane (material + place) odejmowane od reszty przydzialu; czego budowa nie wziela (poziom do konca, brak materialu na targu -
     wtedy place najwyzej 3 x material jak dotad) idzie na nastepna budowe kolejki.
   - Rod z jedna budowa i rod gracza: `OneBudget` zwraca dotychczasowy podzial po rowno - bez zmian co do monety.
2. `BuildFunding.cs` (minimalne wpiecia): `OnePlan(jobs, count)` przed petla, przydzial rodu liczony raz jako `all` (podzial `all / count` - ta sama
   arytmetyka co dotad), `OneBudget` przed ograniczeniem kiesa pana (obliczenie `ppp` i targu przeniesione przed to ograniczenie - tylko obliczenia),
   `OnePaid` po zaplacie, `OneReset` w `Reset`, `OneTail()` na koncu linii "Budowy oplacone".
3. `BuildDiary.cs`: osada czekajaca dzis w kolejce rodu ma w linii "Budowa:" dopisek "(w kolejce rodu - R3-11a)", a podsumowanie "Budowy:" -
   "w kolejce rodu (R3-11a) N".
4. `Settings.cs` + `tools/gen_mcm.py` -> `McmSettings.cs` (2065 ustawien).

## Linia logu (log_grep)
Koniec linii "Budowy oplacone: dzien N ...":
`| R3-11a jedna budowa na rod naraz (Build One Work Per House tak): rody AI z kilkoma budowami N (budow M; kolejnosc - w wojnie mur najblizej wroga
N rodow, najblizsza konca N); oplacone budowy tych rodow N (pierwsza z kolejki N, dalej z reszty przydzialu N), punktow N (srednio X na budowe),
poziom zaplacony do konca N, poziom juz zaplacony N; czeka w kolejce rodu N osad; potkniecia N.`
Klucz wylaczony: `| R3-11a jedna budowa na rod naraz: WYLACZONE (Build One Work Per House) - ...`.

## Ryzyko / co sprawdzic (kontrola calosci)
- **Regresje**: przy kluczu wylaczonym `_oneMulti` puste - `OneBudget` zwraca `budget` bez zmian, kolejnosc `jobs` bez zmian; jedyna roznica to
  kolejnosc obliczenia `ppp`/`NearestTown` przed `Math.Min(budget, lord.Gold)` (czyste obliczenia, `NearestTown` z pamiecia). Rody z jedna budowa
  i gracz - jak dotad przy wlaczonym kluczu.
- **Zamknieta ekonomia**: platnik i odbiorcy bez zmian (kiesa pana -> kasa targu za sztuki zdjete z polki, kasa osady / kiesa ludu za place).
  Suma wydana przez rod <= przydzial doby (reszta przydzialu maleje o kazde wydanie); jedyny wyjatek to stary `BuyOneCheapest` (jedna najtansza
  sztuka do calej kiesy) - jak dotad, reszta wtedy schodzi ponizej 0 i kolejne budowy czekaja. Czego budowy nie wziely - zostaje w kiesie pana.
- **Kolizje**: roboty ludu (P2) licza swoj brak do konca poziomu od `t.Construction` (z punktami pana tej doby) - nie dopelnia drugi raz tego, co
  zaplacil pan; WarFront (N5-E) i "mury pierwsze" bez zmian (cywilne w wojnie dalej tylko u rodu bez budowy wojskowej); zgliszcza na czele kolejki
  osady (CitySack.Burn) bez zmian (dotyczy kolejki osady, nie rodu); DayHeld - `_funded` jak dotad.
- **Tempo swiata**: kolejka nie zwieksza sumy punktow rodu (uwaga weryfikatora audytu) - zmienia kolejnosc: budowy koncza sie jedna po drugiej.
  Rody bogate z duzym przydzialem moga wydac mniej niz dotad (sufit "do konca poziomu" - dotad nadwyzka szla w postep budowli). Rody, ktorych
  jedna budowa stala bez materialu, teraz placa za inna (dotad przydzial lezal) - suma punktow swiata moze lekko wzrosnac.
- **Skaczacy cel**: AI gry przestawia kolejke budowli osady (ok. 10% na dobe) - cel rodu moze sie zmienic, postep budowli nie przepada (jest
  w budowli). Regula bez pamieci miedzy dobami (bez pol zapisu).
- **Koszt**: raz na dobe; odleglosci tylko dla rodow w wojnie z kilkoma budowami (budowy x 227 warowni).
- Sprawdzic w logu: "oplacone budowy tych rodow" + "czeka w kolejce rodu" ~ "budow"; "srednio X na budowe" wyraznie wieksze niz srednia linii
  (punkty / osady); "poziom zaplacony do konca" > 0; "potkniecia 0"; w dzienniku "ukonczonych poziomow" wiecej niz dotad; suma "wydano" bez skoku.

## Decyzje (projektuje Claude)
- Tylko rody AI: lenna gracza dziela przydzial po rowno jak dotad - gracz sam ustawia kolejke w kazdym lennie i nie widzi kolejki rodu.
- Reszta przydzialu, ktorej pierwsza budowa nie wziela, idzie na nastepna - pieniadz nie lezy, gdy na targu czola nie ma materialu.
- Sufit "do konca poziomu" tylko w rodach z kolejka (rod z jedna budowa - bez zmian).
- "Najblizej wroga" = najblizsza wroga warownia w linii prostej (stabilne z dnia na dzien), nie chwilowe zagrozenie WarFront.

## Status
DO SPRAWDZENIA - zbudowane (Armoury, 0 bledow), bez testu w grze (zakaz uruchamiania w tym zadaniu).

## Test w grze
Autotest 120 dob z zapisu: grep `R3-11a jedna budowa na rod` w Armoury.log - liczniki jak wyzej; porownac "Budowy: ... ukonczonych poziomow"
i "srednia moc" z WGRANIEM 91 (d109236: 0 poziomow, 3.2 pkt/dzien); w liniach "Budowa:" osady z dopiskiem "(w kolejce rodu - R3-11a)".
