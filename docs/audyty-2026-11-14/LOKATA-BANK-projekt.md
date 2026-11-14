# PROJEKT: lokata z odsetkami w Banku Zelaznym (decyzja 2, "najpierw projekt")

Stan: tylko projekt, kodu nie pisalem. Gry, autotestu i worktree nscal nie ruszalem. Kod czytalem z nscal (12e3ca1e = WGRANIE 236), liczby wzialem z `Modules/Armoury/Armoury-2026-11-14_17-30-16.log` (dni 108770-108798). Kalendarz: rok Armoury ma 364 dni (`IronBank.DaysPerYear` czyta go z modelu czasu gry).

## 0. Najwazniejsze w skrocie

Bank w obecnej grze prawie nic nie pozycza, wiec prawie nic nie zarabia:
- dlug wszystkich rodow wynosi 2 900-3 050 zl, przy kapitale 4 997 066 zl;
- od dnia 108649 do 108798 (149 dni) Bank zarobil na czysto 4 105 zl, a 4 073 zl z tego wyplacil do Braavos;
- daje to okolo **10 000 zl zysku na rok (364 dni)**.

Jesli odsetki lokat maja byc placone z tego, co Bank zarobi na pozyczkach (zamknieta ekonomia), to dzis cala pula na odsetki to kilka tysiecy zl rocznie. Starczy na rozsadny procent od jednej lokaty gracza. Jesli lokaty zalozy wielu lordow, wyjdzie ulamek procenta.

Nowe zloto z lokat nie zwiekszy pozyczek. W linii `Dlugi (168)` jest "na limicie 16" rodow, a ponad rezerwa Bank ma wolne 2 993 168 zl. Mniej pozycza sie wiec z powodu limitow (drabina 168 pozycza tylko tym, ktorzy moga splacic, i tylko na wojne), a nie z braku pieniedzy w Banku.

## 1. Jak dzis dziala Bank Zelazny (kod)

| Element | Jak dziala | Gdzie |
|---|---|---|
| Kapital | Na starcie `IronBankCapital` = 5 000 000. Pozyczki go zmniejszaja, splaty z odsetkami zwiekszaja. Pusty Bank nie pozycza. | `IronBank.EnsureCapital`, `_capital` |
| Kto pozycza | Rody krolestw i krolowie (AI i gracz). Przy wlaczonej drabinie 168 AI bierze tylko "kredyt wojenny" (`LendKw`, wyplata dzienna przez budzet 166). Korony biora na zwrot zoldu (`CrownBorrow`, 186). Gracz pozycza w menu Braavos (`town_EN5`, "Visit the Iron Bank"). | `IronBank.Lend/LendKw`, `CrownWarLoan431.cs`, `DebtLadder.cs` |
| Limit | Drabina 168: ile rod zdola splacic (stale dochody ze wsi, lenna, lupy z 30 dni) razy wiarygodnosc. Bez drabiny: 60 dni dochodu + 10 000 zl za miasto + 5 000 zl za zamek. | `IronBank.Limit` -> `DebtLadder.Limit` |
| Oprocentowanie roczne | Krol 20%, rod z lennami 30%, rod bez lenn 45%. Kolejny dlug +10 pp, po bankructwie +15 pp. Oplata za udzielenie pozyczki 2%. Odsetki dolicza sie codziennie: `dlug * stopa / 364`. | `RateFor`, `IronBank.Daily` |
| Splata i kary | Raty z dochodow stalych (Bank bierze najwyzej 10% dziennie). Spoznienie oznacza zaleglosc, potem zajecie dochodow wsi i kiesy ponad 38 000 zl, na koncu wyprzedaz. Dlug nie przepada: rod wymarly przekazuje go nowym panom wsi. | `DebtLadder` |
| Strata Banku | Dlug bez platnika albo upadla korona. Strate pokrywa najpierw pula zysku, potem kapital. | `IronBank.Dividends.cs` |
| Zysk | Liczony z pieniedzy naprawde wplaconych (odsetki i oplaty ponad koszt pozyczki). Co 28 dni pula idzie do Braavos: 50% do miasta (kiesy ludu i kasa kupcow), 50% do glow rodow Braavos z lennem (5 rodow). Nigdy nie schodzi ponizej rezerwy = max(1 000 000 zl, prog korony) + to, co Bank pozyczyl w ostatnich 28 dniach. | `IronBank.Dividends.cs` (f20-d3) |
| Skarbiec gracza w Braavos (521) | Juz istnieje. Klucz `"bank"` w `MoneyWeight._vault`: przechowanie BEZ odsetek, Bank tego zlota NIE pozycza, a zloto nic nie wazy. Wierzyciel siega do tego skarbca jako pierwszego (decyzja 5). W ksiedze to pozycja `HVault`, osobna od kapitalu Banku. | `MoneyWeight.cs` |
| Zapis | `IronBank.Export/Import` (`cap=` i dluznicy), osobno `arm_bankdiv` i `arm_clanvault`. | |

Zloto swiata (dzien 108797): razem 220,9 mln. Glowy rodow 51,3 mln, skarbce koron 74,2 mln, notable 28,1 mln, Bank 5,0 mln, gracz 0,18 mln. Gdyby lordowie AI odlozyli 10% swojego zlota, czyli okolo 5 mln, lokaty bylyby rowne calemu kapitalowi Banku.

## 2. Zasady wspolne wszystkich wariantow

1. **Lokata to nowa pozycja obok skarbca 521** (`bank`, bez odsetek). Gracz w menu Braavos przenosi zloto ze skarbca albo z sakiewki na lokate. Zloto lokaty wchodzi do kapitalu Banku, wiec Bank moze je pozyczac. Bank jest jednak winien to zloto lokujacemu, a ten dlug zapisujemy osobno (`_deposits`: wlasciciel, kwota, dzien zalozenia, termin, naliczone odsetki).
2. **Odsetki pochodza tylko z zysku zrealizowanego** (pula f20-d3), czyli z odsetek i oplat, ktore dluznicy naprawde wplacili. Na lokaty idzie czesc puli, zanim reszta trafi do Braavos. Ujemna pula (straty Banku) oznacza, ze w tym okresie odsetek nie ma. Platnik i odbiorca: kapital Banku -> lokujacy; klucznicy Braavos dostaja mniej o dokladnie te kwote.
3. **Rezerwa na wyplaty lokat.** Do rezerwy f20-d3 dochodzi `IronBankDepositReserve` (np. 50%) sumy lokat, a dywidenda do Braavos nie moze jej przekroczyc. Bank pozycza wtedy z lokat najwyzej polowe.
4. **Brak plynnosci.** Wyplata lokaty jest ograniczona wolnym kapitalem ponad prog 1 000 000 zl. Gdy go brakuje, Bank wyplaca czesc, reszte wpisuje do kolejki i doplaca codziennie ze splat, zanim udzieli nowych pozyczek. Dzis to praktycznie niemozliwe (wolne 2,99 mln).
5. **Upadlosc Banku.** Straty pokrywa najpierw pula zysku, potem kapital wlasny (5 mln), dopiero na koncu lokaty, wszystkim po rowno. Lokata traci wiec tylko wtedy, gdy Bank straci ponad 5 mln.
6. **Wojna z Braavos (krolestwo `bravos`).** Bank jest neutralny, jak w ksiazkach, wiec lokata nie przepada i odsetki dalej naliczaja sie z puli. Gracz w wojnie z Braavos nie wejdzie do miasta, wiec lokaty nie wyplaci. Weksel (pkt 7) nie jest honorowany w miastach krolestwa Braavos ani w miastach krolestw w wojnie z Braavos.
7. **Weksle (opcja).** Wyplata lokaty w dowolnym miescie, ktorego wlasciciel nie jest w wojnie z Braavos, za oplata 3%. Zloto wyplaca kasa kupcow tego miasta, najwyzej 20% jej stanu, a Bank od razu zwraca miastu kwote ze swojego kapitalu. Oplata 3% idzie do kapitalu Banku i liczy sie do zysku. Kasy miast maja razem 17 mln, wiec pieniadz sie nie bierze z niczego.
8. **Wierzyciele (decyzja 5).** Gdy gracz ma zaleglosc albo zajecie (drabina 168), Bank najpierw potraca dlug z lokaty, przed skarbcami 521.
9. **Podatek.** Braavos nie bierze podatku od lokat (zarabia na roznicy stop). **Mozliwa kolizja do sprawdzenia w dekompilacji BK:** "podatek BK od bogatych" (dzis -15 821 zl na dobe) liczy sie prawdopodobnie od `Hero.Gold`. Lokata AI zdjeta z `Hero.Gold` omijalaby wiec ten podatek. W fazie 1 (tylko gracz) to nie problem.
10. **Zapis i czyszczenie.** Nowy klucz `arm_bankdep`. Lokaty sa czyszczone w `IronBank.Reset`. Wylaczony klucz: nowych lokat nie ma, a istniejace mozna wyplacic (zloto nie utyka, jak w 521).

## 3. Warianty z liczbami (100 000 zl na rok 364 dni)

Przyjeta baza: zysk Banku dzis okolo 10 000 zl na rok. Dla porownania pokazuje tez czas wojny, gdyby pozyczki siegnely sredniej 300 000 zl przy 30%. Daloby to okolo 90 000 zl odsetek rocznie. To zalozenie, w logu tego nie widzialem.

### Wariant A: lokata na zadanie (bez terminu)

- Wyplata w kazdej chwili. Odsetki to `IronBankDepositShare` = 25% puli zysku, dzielone po rowno wedlug zlotowko-dni wszystkich lokat. Wyplacane razem z dywidenda, co 28 dni.
- 100 000 zl, tylko gracz:
  - dzis: 25% z 10 000 = **2 500 zl (2,5%)**;
  - w czasie wojny: 25% z 90 000 = 22 500 zl, ale pulap 6% obcina do **6 000 zl**.
- Gdy lordowie AI zaloza lokaty na 1 mln: dzis 0,25%, w czasie wojny 2,25%.
- Plus: proste i elastyczne. Minus: Bank nie wie, ile zlota naprawde moze pozyczyc (kazda lokata moze zniknac jutro).

### Wariant B: lokata terminowa z udzialem w zysku (REKOMENDACJA)

- Terminy: 91, 182 albo 364 dni. Udzial w puli zysku: 50% dla lokat. Zlotowko-dni wazone wedlug terminu: 91 dni x1,0, 182 dni x1,25, 364 dni x1,5. Pulap 8% rocznie (nadwyzka ponad pulap zostaje w puli dla Braavos).
- Wczesniejsza wyplata: tracisz odsetki z biezacego okresu, a kapital dostajesz w calosci.
- 100 000 zl na 364 dni, tylko gracz:
  - dzis: 50% z 10 000 = **5 000 zl (5%)**, na koniec roku **105 000 zl**;
  - w czasie wojny: 45 000 zl, ale pulap 8% obcina do **8 000 zl** (108 000 zl).
- Gdy lordowie AI zaloza lokaty na 1 mln (faza 2): dzis 0,5%, w czasie wojny 4,5%.
- Klucznicy Braavos dzis traca okolo 5 000 z 10 000 zl rocznie. Miasto Braavos (kiesy ludu i kupcy) dostaje mniej o 2 500 zl rocznie.
- Plus: zamknieta ekonomia co do zlotowki. Oprocentowanie samo podaza za tym, ile Bank pozycza (wojna = wieksze pozyczki = wyzsze odsetki). Gdy dluznicy bankrutuja, odsetki spadaja do zera, ale kapital zostaje. Tak dzialali bankierzy XIV wieku: lokata "na udzial" (depositi a discrezione) u Bardich i Peruzzich.

### Wariant C: stala stopa 5% gwarantowana

- 100 000 zl daje **5 000 zl** zawsze. Gdy pula zysku nie starcza, Bank doplaca z kapitalu wlasnego.
- Dzis pula (okolo 10 000 zl rocznie) wystarcza na lokaty do 200 000 zl. Przy lokatach AI na 5 mln (faza 2) Bank wyplacalby 250 000 zl rocznie z kapitalu. Zysk tego nie pokryje, wiec przez okolo 12 lat gry kapital spadnie z 5 mln do progu 1 mln i Bank przestanie pozyczac.
- To nie jest pieniadz z niczego (kapital to prawdziwe zloto), ale jest to sztuczna pompa: zloto z Banku plynie do bogatych bez zadnej pracy. Kloci sie z decyzja "ekonomia bez sztucznych doplat" (12.10). **Odradzam.**

## 4. Rekomendacja

**Wariant B, w dwoch krokach** (jedna zmiana naraz):

1. **Faza 1, tylko gracz:** lokata terminowa w menu Banku w Braavos (91/182/364 dni, udzial 50% puli zysku, pulap 8%), rezerwa 50% lokat, potracenie przez wierzycieli, zapis `arm_bankdep`. Bez weksli. Do sprawdzenia w logu: odsetki = czesc puli, a wyplata do Braavos maleje o tyle samo (kontrola co do zlotowki).
2. **Faza 2, osobna decyzja Jeffa:** lordowie AI zakladaja lokaty, gdy maja w skarbcu wiecej niz okolo 60 dni zoldu + 50 000 zl (najwyzej 25% nadwyzki). Wczesniej trzeba sprawdzic w dekompilacji BK, czy podatek od bogatych liczy sie od `Hero.Gold`. Trzeba tez ocenic deflacje: zloto AI z lokat lezy w Banku i nie krazy, dopoki Bank go nie pozyczy, a dzis pozycza 3 000 z 5 mln. Weksle tez dopiero w fazie 2 albo 3.

Szczera uwaga dla Jeffa: dopoki AI pozycza tak malo, lokata jest glownie **bezpiecznym miejscem na zloto** (nic nie wazy, nie przepada przy szturmie osady). Odsetki beda male: okolo 5% dla samego gracza dzis, wiecej w czasie duzych wojen.

## 5. Klucze MCM (nowe, nazwy po angielsku)

| Klucz | Domyslnie | Opis (EN) |
|---|---|---|
| `IronBankDeposits` | true | You may place gold with the Iron Bank of Braavos for a term; it lends it on and shares with you the profit it really earns. |
| `IronBankDepositShare` | 0.5 | Share of the Bank's realized profit that goes to depositors before Braavos takes the rest. |
| `IronBankDepositMaxRate` | 8 | Most a deposit can earn in a year (percent); the rest of the profit goes to Braavos. |
| `IronBankDepositTerms` | 91/182/364 | Terms of a deposit in days (wagi 1.0/1.25/1.5). |
| `IronBankDepositReserve` | 0.5 | Share of all deposits the Bank keeps in its vaults and never lends or pays out as profit. |
| `IronBankDepositMin` | 10000 | Smallest deposit the Bank accepts. |
| `IronBankLetterFeePercent` (faza 2/3) | 3 | Fee for drawing your deposit in another town with a letter of the Bank. |

Po dodaniu kluczy trzeba uruchomic `python tools/gen_mcm.py`. Klucze sa nowe, wiec `Armoury.json` Jeffa ich nie nadpisze.

## 6. Linie logu

- Co dobe: `Bank Zelazny - lokaty (L1): dzien N | lokat X (gracz Y, rody Z), suma S, w rezerwie R | naliczone dzis a (z puli zysku), od startu b | wyplaty: w terminie c, przed terminem d (odsetki utracone e), czekajace w kolejce f | potracone przez wierzycieli g | potkniecia 0.`
- W dobie wyplaty, dopisek do istniejacej linii `Bank Zelazny - wyplata do Braavos (f20-d3)`: `pula przed P -> lokaty L (pulap 8%: obcieto O), Braavos P-L | kontrola: kapital A -> B, odbiorcy +x, roznica 0.`
- Do ksiegi `Pieniadz swiata`: nowa pozycja "lokaty w Banku (L1)". Jej zloto jest juz czescia kapitalu Banku, wiec suma swiata zostaje bez zmian, a lokata pokazuje tylko, czyje to zloto.

## 7. Ryzyko / co sprawdzic (kontrola calosci, przed kodem)

- **f20-d3 (dywidenda):** odsetki lokat zmniejszaja pule. Wplata lokaty to kapital wlasny, a nie zysk, wiec musi isc tak jak `NoteCapitalNoProfit`, bez wplywu doby. Inaczej wplata 100 000 zl wyszlaby do Braavos jako "zysk".
- **Zwrot nadwyzki kapitalu wlasnego:** lokaty trzeba odjac od kapitalu wlasnego. Inaczej kapital wlasny wynioslby 5,1 mln, a "nadwyzka" 100 000 zl poplynelaby do Braavos po 1/180 dziennie, czyli zloto gracza trafialoby do klucznikow.
- **Skarbiec 521 `bank`:** zostaje bez odsetek. Lokata to osobna pozycja, zeby nie podwoic ksiegi `HVault`.
- **Wierzyciele (decyzja 5):** kolejnosc potracania: lokata, potem `bank`, potem skarbce osad.
- **Podatek BK od bogatych:** tylko w fazie 2 (AI). Sprawdzic w dekompilacji.
- **Zajecie Braavos przez inne krolestwo:** menu Banku dziala w `town_EN5` niezaleznie od wlasciciela. Skarbce osad przechodza przy szturmie do zdobywcy (decyzja 1), ale lokata nie jest skarbcem osady, tylko dlugiem Banku, wiec nie przechodzi.
- **Kalendarz:** wszystkie przeliczenia na rok przez `IronBank.DaysPerYear()` (364).