# n22f-plany (20.10) - NKS-30n: zapas polek kroku 8 poza ocena zbieznosci Planu (klucz Ks Plan Converge Shelf Level)

Galaz `w-toku/n22f-plany`, baza 4885ae75 (w-toku/n22-calosc). Poprawki fali 1 przed WGRANIEM 93. Status: **DO SPRAWDZENIA** (Armoury zbudowany, kod 0; harness Planu
przed/po - swiat Planu bit w bit ten sam; bez autotestu i bez wgrania - zakaz w zadaniu). Wersja 2 (po recenzjach diagnoza + technika) - zastepuje wersje 2b1b9e24.

## Dla Jeffa (prosto)

Tygodniowy Plan celowo zamawia troche wiecej towaru, niz miasta kupia - zapas na polki straganow (jedzenie wedlug luki na polkach, do 25 na 100, reszta 3 na 100). Ten
zapas rozklada sie rowno na wszystkie towary rynku, po ok. 2 na 100, a sprawdzian Planu "czy rynek sie wyrownal" mial prog dokladnie 2 na 100 - wiec raz liczyl zboze,
ser i oliwki jako "niewyrownane", a raz nie, zaleznie od trzeciej cyfry po przecinku. Stad 8 zamiast 4 w ostatnim tescie, choc swiat liczyl sie tak samo. Teraz
sprawdzian uznaje za wyrownany towar, ktorego podaz lezy miedzy popytem a wspolnym poziomem zapasu rynku (nie wyzej, niz Plan zamowil). Zmienia sie tylko raport w logu -
Plan, rece, ceny, zloto i towar w grze sa dokladnie takie same jak dotad.

## Problem

`sprawdz_logi --grupa npolnoc`, NKS-30n "Plany zbiezne przy Ks Plan Markets Clear (towary niezbiezne W + E, mediana Planow po pierwszym)", prog <= 6:
nowa 1, nowa88 1, r2nowa 4, r3nowa 5, **n21nowa 4** (policzone tu z `Armoury-2026-10-20_03-32-17.log`: Plany po pierwszym 36, 14, 19, 17, 3, 2, 1, 2, 3, 8, 5, 4, 3, 4,
1, 1), **n22anowa 8 (NIE)** (35, 0, 14, 17, 2, 14, 9, 2, 2, 5, 4, 4, 7, 8, 12, 8). Biegi 160 dob z zapisu: r2160 4, r3160 4, n21160 5, **n22a160 3**.

## Dowod

1. **Towary "niezbiezne" stoja na progu.** `KS: bilans Westeros` poznych Planow: prawie kazdy towar Westeros ma podaz/popyt 1.01-1.02 (zboze, ser, maslo, jablka,
   winogrona, oliwki, len, bawelna, konie, futra). Doba 108940: n21nowa wszystkie 1.01 (niezbieznych W 1), n22anowa wszystkie 1.02 (niezbieznych W 10, welna 1.07).
   r3nowa doba 108947: wszystkie 1.02, niezbieznych tylko 3 W - te same 1.02 z druku sa raz 1.019, raz 1.021.
2. **Skad wspolny poziom (kod).** Krok 8 (`MoneyStep`, "RYNEK") oddaje rece z pracy domowej, az wartosc podazy = wartosc popytu z zapasem polek: `dk = Dm + (mfac - 1) x
   (Dm - zjedzone w okregach)`, `mfac` = czynnik towaru z `KsInput.MarketFactor` - w stanie Plan jedzenie z luki zapasu P4 (`R4cGapFactors`, 1.00-1.25; n22anowa doba
   108947: `grain f1.064 meat f1.185 cheese f1.250 olives f1.250`), reszta `ShelfMargin` 1.03; od D+14 sterowanie P4 0.85-1.25 (`MarketControl`), po D+49 czynnik wraca do 1.
   `Equilibrium` normalizuje ceny na rynek (srednia geometryczna wazona wartoscia popytu = 1), wiec przy nadwyzce wspolnej punkt staly ma jeden poziom S/D dla towarow,
   ktore rece moga przesunac - ceny wzgledne go nie zbijaja. Test zbieznosci (`|S/Dm - 1| > Tol`, `Tol = 0.02`) tego zapasu nie znal.
3. **Harness Planu (h4-feedswiat, swiat 16.10, klucze jak w grze, `KS_ROUND2=0` - deterministyczny, 6 Planow).** Linia rynkow Planu pokazuje teraz cel kroku 8 i poziom:
   z samym marginesem 1.03 (`KS_MARGIN=1.03`) cel Westeros x1.013 (Essos x1.015-1.016), poziom Westeros x1.012-1.013 (Essos x1.000); z czynnikami luki P4 z gry (`KS_MF` = linia `KS: luka zapasu` n22anowa
   108947, `KS_FMAX=1.25`) cel Westeros x1.025-1.028, poziom x1.016-1.020; Essos cel x1.048-1.052, poziom x1.000-1.010 (krok 8 Essos nie dochodzi do celu - brak rak).
   Towary niezbiezne, stara miara / nowa, Plany 1-6: margines 1.03 - 4,2,7,3,2,4 / 3,1,7,3,2,2 (mediana po pierwszym 3 / 2); czynniki P4 z gry - 9,4,3,4,3,4 /
   6,1,0,2,0,3 (mediana **4 / 1**). Wyniki Planu (csv okregow) przy kluczu wl. i wyl. **identyczne bajt w bajt** (f103 = g103 = d103 sprzed poprawki; h103 = i103).
4. **Roznica miedzy budowami to stan swiata, nie trend budow.** Nadwyzka targu Westeros po kroku 8 (NKS-30m) w Planach 926-947: r2nowa -2.0..-2.9%, n21nowa -1.3..-3.6%
   (krok 8 nie dobija do celu - poziom 1.01, niezbieznych 1-2), r3nowa -0.3..-0.8%, n22anowa -0.4..-0.6% (dobija - poziom 1.02, niezbieznych 3-12). Praca domowa wsi w tych
   dobach taka sama we wszystkich biegach (ok. 60%). Kolejnosc 4/5/4/8 nie rosnie z budowa: r2 i n21 nie dobijaja, r3 i n22a dobijaja. Plan czyta z gry ksiege towarow,
   polki miast do P4 i alfe wsi - fala 1 zmienia rozwoz (p5-handel: `MarketCarts.cs`, `RiverBarges.CartFactor`) i alfe wsi gorniczych (p5-regale,
   `MineRegale.KsShareOfTakings`), ktora z nich daje dobicie w poznych Planach n22a - nie sprawdzalem; NKS-30m n22a jest lepsze (blizej 0), nie gorsze.
5. **Szacunek nowej miary na logach gry** (`n22f-plany-tmp/est2.py`: S/D z `KS: bilans` na 2 miejscach, wagi = popyt t/d, cel kroku 8 nieznany z logu - sufit 1.25
   i 1.03 daja to samo): mediana NKS-30n r2nowa 4 -> 1, r3nowa 5 -> 2, n21nowa 4 -> 1, **n22anowa 8 -> 1**; biegi 160 dob r2160 4 -> 3, r3160 4 -> 2, n21160 5 -> 2,
   n22a160 3 -> 2. Tylko szacunek (druk 2 miejsca - w Planach z poziomem 1 roznice 1-2 to zaokraglenie); prawdziwa liczbe da autotest.

## Przyczyna

Ocena zbieznosci Planu liczyla celowy zapas polek kroku 8 (nadwyzka wspolna wszystkim towarom rynku, ok. +2% S/D) jako brak zbieznosci; towary staly na samym progu
2%, wiec liczba "niezbieznych" byla rzutem moneta zaleznym od tego, czy krok 8 dobil do swojego celu. To nie regresja fali 1 (Dowod 4; n22a160 ma najlepsze 3), tylko
odczyt.

## Zmiana

- Klucz MCM `Ks Plan Converge Shelf Level` (`KsPlanConvergeShelfLevel`, domyslnie TAK; `Settings.cs`, `McmSettings.cs` z gen_mcm), do Planu przez `FeedSwiatInput`
  (`KsInput.ConvShelfLevel`).
- `StartCalibration.Core.cs`, `MoneyStep`: w petli RYNEK cel kroku 8 rynku `x.LevT[m]` = suma `dk x pk` / suma `Dm x pk` - te same towary, ceny cienia i czynniki `mfac`
  co nadwyzka rynku (P4, sterowanie, margines - kazdy stan KS).
- `Equilibrium`, **tylko ocena stanu oddanego** (lista `Open`, linia rynkow Planu): towar zbiezny, gdy S/D w pasmie [min(1, poziom), max(1, poziom)], odchylenie liczone od
  pasma (`BandDev`); poziom `ShelfLevel` = srednia geometryczna S/D (wagi Dm x Val jak normalizacja cen) towarow sterowanych krokiem 8 (bez regale, koni, ExcessSkip, granicy
  ceny), ucieta do [min(1, cel), max(1, cel)]. Brak niezamowiony (S/D < 1 przy celu >= 1) - jak dotad; nadwyzka ponad poziom - liczona. Przed krokiem 8 albo klucz
  wylaczony - pasmo [1, 1], test jak dotad (bit w bit).
- **Przebieg rownowagi bez zmian:** punkt kontrolny co 50 rund (wyjscie "zbiezna", suma odchylen `mt`, najlepszy stan, wyjscie "stoi") - stara miara (cofniete z wersji
  2b1b9e24). `KsResult.OpenAbsM` - liczba niezbieznych stara miara; z niej `RoundsFirst` nastepnego Planu (`StartCalibration.Plan.cs`) - jak dotad.
- Linia `KS: rynki Planu (feed-swiat)`: `towary niezbiezne N (wobec wspolnego poziomu podazy/popytu x1.0xx, cel kroku 8 x1.0yy - ...; stara miara |S/D - 1| > 2%: M)`.
- `tools/sprawdz_logi.py` NKS-30n: prog bez zmian (<= 6); w wartosci (INFO) poziom, cel i liczba stara miara ostatniego Planu.

## Ryzyko / co sprawdzic (kontrola calosci)

- **Regresje:** zadne - stan Planu, rece, ceny, bloki gry i rundy bez zmian (harness: csv okregow bajt w bajt przy kluczu wl./wyl., z marginesem i z czynnikami P4).
  Zmienia sie tylko lista `Open` (linia Planu `niezbiezne:`, NKS-30e, NKS-30n) i dopisek linii rynkow. `EqConv` (NKS-30k "zbiezne") liczy dalej wyjscia stara miara.
- **Kolizje:** krok 8, narzut niedoboru (Clear.cs), `Bounds` i linia "na granicy ceny" bez zmian; cel liczony z tego samego `mfac`, ktory widzi nadwyzka rynku.
- **Pozostaly noz:** towar na krawedzi pasma (S/D = poziom x 1.02 albo 0.98) - jak kazdy prog; towar ponizej 1, gdy rynek planuje nadwyzke (np. jaja 1.00 przy poziomie
  1.02), jest w pasmie, wiec liczony jako zbiezny (jak w starym tescie).
- **Zamknieta ekonomia:** bez zlota i towaru. **Watki:** klucz czytany na watku glownym (BuildInput), `LevT` w kontekscie jednego `Solve` na watku roboczym; dwie tablice
  double[2] na rownowage/krok 8 (poza goraca sciezka).

## Test w grze

1. Autotest nowej kampanii 120 dob: `sprawdz_logi --grupa npolnoc` NKS-30n <= 6 (szacunek 1-2), NKS-30e w dol; NKS-30k, NKS-30m, NKS-30b i reszta grup nks, nsrednim, nzc*
   - **liczby jak w n22anowa w granicach zmiennosci biegow** (Plan liczy ten sam swiat). `grep "wobec wspolnego poziomu podazy/popytu x"` - Westeros poziom ok. x1.01-1.02
   przy celu x1.02-1.05, Essos poziom ok. x1.00-1.01 przy celu ok. x1.05.
2. Bieg 160 dob z zapisu (Plan wlaczenia ok. doby 109109, wlaczenie 109112): przed D+49 poziom > 1 jak w nowej kampanii, po D+49 cel -> 1 i test jak dotad; NKS-30n
   (szacunek 2) <= n22a160 3; wyzywienie NKS-30b i rece Planu wlaczenia jak w n22a160.
3. Czas Planu (`rund ... czas ... s`) jak dotad.

## Recenzja (wersja 2b1b9e24 -> wersja 2)

- **diagnoza, wazne - zrodlo poziomu zle wskazane (luka P4, nie margines 1.03): PRAWDA.** Krok 8 celuje w `mfac` (luka zapasu P4 do 1.25 dla jedzenia w stanie Plan).
  Poprawione: sufit poziomu = cel kroku 8 z tego samego `mfac` (`x.LevT`), w obie strony, w kazdym stanie KS (takze D+28..D+49 i czynniki < 1). Harness puszczony z
  `KS_MF` z linii luki n22anowa i `KS_FMAX=1.25` (Dowod 3). Hint MCM poprawiony (bez "3 in 100").
- **diagnoza/technika, wazne - zmiana nie jest "tylko miara" (najlepszy punkt kontrolny, wyjscie "stoi", Plan wlaczenia, harness: wyzywienie Nocnej Strazy 0.59 -> 0.45):
  PRAWDA dla wersji 2b1b9e24.** Poprawione: punkt kontrolny i wyjscia rownowagi wrocily do starej miary, `RoundsFirst` ze starej liczby; pasmo tylko w ocenie stanu
  oddanego. Teraz to naprawde tylko miara - csv okregow harnessu bajt w bajt (Dowod 3), wiec wyzywienie krain i Plan wlaczenia jak dotad.
- **technika, wazne - okno D+28..D+49 i poziom ponizej 1 bez poprawki: PRAWDA.** Poprawione tym samym celem z `mfac` (pasmo [min(1, cel), max(1, cel)]).
- **diagnoza/technika, wazne/drobne - zle oczekiwania (160 dob "x1.000", Essos "x1.000"): PRAWDA.** Poprawione w "Test w grze" (Essos poziom ok. 1.01, cel ok. 1.05;
  160 dob - poziom > 1 do D+49). Liczby gry inne niz NKS-30n/30e nie zmieniaja sie z konstrukcji.
- **drobne - poprawa tylko szacunkiem offline: CZESCIOWO.** Harness z czynnikami P4 z gry na nowym kodzie: mediana 4 -> 1 (wczesniejszy harness bez P4 dawal 3/3); w grze
  dalej tylko szacunek do autotestu.
- **drobne - noz na jajach (S/D 1.00 przy poziomie 1.02): PRAWDA.** Poprawione pasmem: towar miedzy 1 a poziomem jest zbiezny (jak w starym tescie); jaja (ExcessSkip)
  nie licza sie do poziomu.
- **drobne - roznica budow jako trend: PRAWDA.** Opis poprawiony (Dowod 4: dobicie kroku 8 do celu w poznych Planach; przyczyny dobicia w n22a nie sprawdzalem).
