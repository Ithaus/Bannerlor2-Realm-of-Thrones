# n22f-plany (20.10) - NKS-30n: zapas polek kroku 8 poza testem zbieznosci Planu (klucz Ks Plan Converge Shelf Level)

Galaz `w-toku/n22f-plany`, baza 4885ae75 (w-toku/n22-calosc). Poprawki fali 1 przed WGRANIEM 93. Status: **DO SPRAWDZENIA** (Armoury zbudowany, kod 0; harness Planu
przed/po; bez autotestu i bez wgrania - zakaz w zadaniu).

## Dla Jeffa (prosto)

Tygodniowy Plan kalibracji przed wlaczeniem KS celowo planuje troche wiecej towaru niz ludzie kupia - zapas na polki miast (3 na 100). Ten zapas rozklada sie rowno na
wszystkie towary rynku, po ok. 2 na 100, a sprawdzian Planu "czy rynek sie wyrownal" mial prog dokladnie 2 na 100 - wiec raz liczyl zboze, ser, maslo i oliwki jako
"niewyrownane", a raz nie, zaleznie od trzeciej cyfry po przecinku. Stad wynik 8 zamiast 4 w ostatnim tescie, choc swiat liczyl sie tak samo. Teraz sprawdzian odejmuje
ten celowy zapas (tylko do 3 na 100, braki dalej liczy jak dotad). W grze nie zmienia sie zloto ani towar - tylko miara Planu; Plan moze troche inaczej skonczyc liczenie rynku, dlatego to idzie do autotestu przed WGRANIEM 93.

## Problem

`sprawdz_logi --grupa npolnoc`, NKS-30n "Plany zbiezne przy Ks Plan Markets Clear (towary niezbiezne W + E, mediana Planow po pierwszym)", prog <= 6:
nowa 1, nowa88 1, r2nowa 4, r3nowa 5, **n21nowa 4** (policzone tu z `Armoury-2026-10-20_03-32-17.log`: Plany po pierwszym 36, 14, 19, 17, 3, 2, 1, 2, 3, 8, 5, 4, 3, 4,
1, 1), **n22anowa 8 (NIE)** (35, 0, 14, 17, 2, 14, 9, 2, 2, 5, 4, 4, 7, 8, 12, 8). Biegi 160 dob z zapisu (KS wlaczony, po D+28): r2160 4, r3160 4, n21160 5, **n22a160 3**.

## Dowod

1. **Fala 1 nie dotyka Planu w biegach "nowa".** We wszystkich czterech biegach nowej kampanii KS jest caly czas w stanie Plan (`Kalibracja startu (KS): start sesji |
   ... stan: Plan`, przy kazdym Planie `KS: sterowanie rynku ... czynnik 1.03 (polki startu)`, `margines popytu targu x1.03`). Czynniki gry z fali 1 w
   `ComputeDistrict(factors)` (p5-stada `HerdKs`/`LandKs`/`HerdLandOwn`, p5-urodzaj `HarvestYear.KsApply`) dzialaja tylko w stanie zastosowanym, a Plan liczy
   `factors = false`. Do tego w n22anowa: `Rok urodzaju (R3-05e) ... czynny NIE | okno zniw NIE` przez caly bieg; `Stada (R3-06c): dzien 108954 | wsie z niepelnym
   stadem 35 ... srednio 99%, najnizej 96%`. Jedyna zmiana fali 1 na wejsciu Planu to alfa wsi gorniczych (p5-regale, `MineRegale.KsShareOfTakings` w `VilAlphaFor`).
2. **Towary "niezbiezne" stoja na progu.** Linia `KS: bilans Westeros` poznych Planow: w KAZDYM biegu prawie kazdy towar Westeros ma podaz/popyt 1.01-1.02 (zboze,
   ser, maslo, jablka, winogrona, oliwki, len, bawelna, konie, futra), a na targ 1.02-1.09. Doba 108940: n21nowa wszystkie 1.01 (niezbieznych W 1), n22anowa wszystkie 1.02
   (niezbieznych W 10). r3nowa doba 108947: wszystkie 1.02, ale niezbieznych tylko 5 (3 W) - te same 1.02 z druku sa raz 1.019, raz 1.021.
3. **Mechanizm (kod).** `KsCore.Equilibrium` normalizuje ceny na rynek (srednia geometryczna wazona wartoscia popytu = 1), wiec punkt staly tatonnementu przy wspolnej
   nadwyzce to jeden poziom S/D dla wszystkich towarow - ceny wzgledne go nie zbijaja. Te nadwyzke robi krok 8 (`MoneyStep`, "RYNEK"): praca domowa oddaje rece, az
   podaz targu = `MarketMargin` x popyt targu, a `MarketMargin` = 1.03 w stanie Plan i do D+28 (`StartCalibration.Plan.cs`, `ShelfMargin`). Przy udziale targu w popycie
   ok. 2/3 daje to ok. +2% S/D na kazdym towarze, a test zbieznosci (`dev = |S/Dm - 1| > Tol`, `Tol = 0.02`) nie zna marginesu. Wzrost miedzy budowami to wolne
   przesuwanie tego poziomu przez 0.02 (praca domowa wsi spada w biegu z 76% do 60% - udzial targu rosnie).
4. **Faza startu (zmiennosc).** Plany, w ktorych mieso i marchew nie stoja na podlodze ceny (`na granicy ceny ... 1` - sam miod), maja 12-18 towarow niezbieznych we
   wszystkich biegach (doby 108857-108870); w n22anowa jeden Plan wiecej (108877: 13 W) - to dokladalo sie do mediany.
5. **Harness Planu (h4-feedswiat, swiat 16.10, klucze jak w grze, 6 Planow).** Ten sam swiat, rozni sie tylko margines: `KS_MARGIN=1.0` niezbiezne 3, 3, 2, 4, 4, 3
   (mediana po pierwszym 3), `KS_MARGIN=1.03` 4, 6, 10, 5, 2, 2 (mediana 5). Wynik zalezy tez od zegara: druga runda gradientu idzie tylko, gdy Plan trwa < 15 s
   (`GradSecondRound`) - przy obciazonym procesorze ten sam Plan 6 dal 33000 albo 19950 rund (b100 / c100); w logach gry to `rownowagi 22` zamiast 12/15.

6. **Szacunek nowej miary na logach gry** (`n22f-plany-tmp/est.py`: koncowe S/D z `KS: bilans` z 2 miejscami, wagi = popyt t/d, bez regale i towarow na granicy
   ceny, poziom w [1, 1.03]): wspolny poziom Westeros w KAZDYM Planie po pierwszym x1.018-1.030 we wszystkich biegach (Essos x1.000 - brak). Mediana NKS-30n tym
   testem: r2nowa 4 -> 1, r3nowa 5 -> 2, n21nowa 4 -> 1, **n22anowa 8 -> 2**; takze Plany fazy startu (14-20) spadaja do 1-4 - to ten sam poziom (x1.023-1.030).

## Przyczyna

Test zbieznosci Planu liczy celowy zapas polek kroku 8 (margines popytu targu 1.03) jako brak zbieznosci; towary stoja na samym progu 2%, wiec liczba "niezbieznych"
jest rzutem moneta. To nie regresja fali 1 (patrz Dowod 1) ani zmiana swiata Planu, tylko odczyt. Skok nowa88 1 -> r2nowa 4: rynek Westeros przeszedl z braku
(NKS-30m: -15.7% -> -16.4%, towary z narzutem) do nadwyzki (-6.0% -> -0.6%) - od tego czasu zapas polek widac w S/D; ktora zmiana rundy 2 to zrobila - nie sprawdzalem.

## Zmiana

- Nowy klucz MCM `Ks Plan Converge Shelf Level` (`KsPlanConvergeShelfLevel`, domyslnie TAK; `Settings.cs`, `McmSettings.cs` z gen_mcm), do Planu przez
  `FeedSwiatInput` (`KsInput.ConvShelfLevel`).
- `StartCalibration.Core.cs`, `Equilibrium`: test zbieznosci (punkt kontrolny co 50 rund i lista towarow niezbieznych na koncu) mierzy towar wobec wspolnego poziomu
  rynku `ShelfLevel` = srednia geometryczna S/D wazona wartoscia popytu (te same wagi co normalizacja cen) towarow czynnych poza granica ceny, ucieta do
  [1, `MarketMargin`]. Brak (poziom < 1) - jak dotad; nadwyzka ponad margines - liczona. Klucz wylaczony albo margines 1 (stan zastosowany po D+28) - poziom 1, bit w bit
  jak dotad (harness: Plany 1-5 identyczne b100/c100).
- Linia `KS: rynki Planu (feed-swiat)`: po `towary niezbiezne N` dopisek `(wobec wspolnego poziomu podazy/popytu x1.0xx - ...)`; `KsResult.ConvLev`.
- `tools/sprawdz_logi.py` NKS-30n: prog bez zmian (<= 6), w wartosci poziom ostatniego Planu (INFO). Harness: `KS_CONVLEV=1`.

## Ryzyko / co sprawdzic (kontrola calosci)

- **Regresje:** tylko test zbieznosci Planu; Plan konczy rownowage wczesniej, gdy ceny wzgledne sa zbiezne (w stanie Plan i D..D+28) - wynik Planu inny o tyle, o ile
  stan z punktu kontrolnego "zbiezny wzglednie" rozni sie od najlepszego stanu po wyjsciu "stoi". Lista niezbieznych steruje tylko rundami pierwszej rownowagi
  nastepnego Planu (`RoundsFirst` 3000 / 6000) i logiem. Suma odchylen `mt` (najlepszy punkt, wyjscie "stoi") liczona ta sama miara - jedna regula.
- **Kolizje:** krok 8 dalej celuje w podaz targu = margines x popyt targu (bez zmian); narzut niedoboru (Clear.cs, `Tol` w kroku narzutu) bez zmian; `Bounds`
  i linia "na granicy ceny z odchyleniem ponad 2%" bez zmian. Czynnik P4 (sterowanie rynku D+14..D+49, 0.85-1.25 na towar) - poziom ucinany do `MarketMargin`, nie do P4.
- **Zamknieta ekonomia:** bez zlota i towaru - zmienia sie tylko miara zbieznosci. **Watki:** klucz czytany na watku glownym (BuildInput), rownowaga na watku
  roboczym czyta tylko `KsInput`; jedna tablica double[2] na rownowage (poza goraca sciezka).
- **Harness po zmianie (bez drugiej rundy gradientu - `KS_ROUND2=0`, margines 1.03, przed d103 / po e103):** niezbiezne 4,2,7,3,2,4 / 5,3,6,3,3,1 (mediana 3 / 3 - w swiecie
  16.10 towary niezbiezne to odstajace welna, ryby, futra, miod, nie wspolny poziom); nadwyzka Essos po kroku 8 -4.0% / -4.2%; kcal R/P miast Essos 98.2/96.3 bez zmian;
  NA TARG poza pasmem 4 / 1 towar; wyzywienie krain ostatniego Planu: Wolni Ludzie 0.78 / 0.74, Skagos 0.91 / 0.87, Nocna Straz 0.59 / 0.45 (INFO) - sprawdzic
  NKS-30b w autotescie (progi 0.60 i 0.80). Rund na Plan w harnessie nie mniej (stan wzglednie zbiezny rzadko w calym rynku - odstajace towary zostaja).
- **Progi do sprawdzenia:** NKS-30n (cel <= 6), NKS-30e (ta sama lista), NKS-30k (zbiezne > 0 - nowa liczba), NKS-30m (nadwyzka targu po kroku 8 >= -2%), grupy nks,
  nsrednim, nzc*. Druga runda gradientu zalezna od zegara (15 s) zostaje - zmiennosc biegow, poza tym zadaniem.

## Test w grze

1. Autotest nowej kampanii 120 dob: `sprawdz_logi --grupa npolnoc` NKS-30n <= 6, NKS-30e, NKS-30m; `grep "wobec wspolnego poziomu podazy/popytu x"` - Westeros ok.
   x1.015-1.03 w Planach stanu Plan, Essos x1.000 (rynek z brakiem).
2. Bieg 160 dob z zapisu (KS wlaczony, po D+28): poziom x1.000 w obu rynkach - liczby jak dotad (NKS-30n n22a160 3).
3. Czas Planu (`rund ... czas ... s`) nie wiekszy niz dotad.
