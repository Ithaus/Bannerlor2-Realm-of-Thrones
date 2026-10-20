# m96-len - len, plotno i sukno pod kalibracja startu (KS) (20.10, galaz w-toku/m96-len od 848b2e80)

**Dla Jeffa (prosto):** Gdy po ok. 5 tygodniach kampanii wlacza sie kalibracja startu, wsie przestawiaja rece z lnu i owiec na inne prace: lnu z pol
robi sie z ok. 1000 sztuk dziennie ok. 280, a tkacze zuzywaja 320-450. Dlatego przez pol roku len, plotno i sukno w swiecie topnieja (len 12 tys. -> 3 tys.,
plotno 7,3 tys. -> 5,5 tys., sukno 4,5 tys. -> 2,4 tys.). Przyczyna: plan kalibracji nie wiedzial, ze wsie kupuja plotno i sukno w miastach - uznal,
ze tkaja je w domu. Teraz plan liczy plotno i sukno, ktore wsie naprawde kupuja (bez tego, co juz mial w koszyku), wiec wiecej rak zostaje przy lnie
i owcach - tyle, ile trzeba tkaczom, bez zadnego mnoznika plonu. MCM: **Ks Village Cloth Buys In Plan** (wlaczone; wylaczone = jak dotad).

## Problem
RAPORT-NOCNY-2026-10-20 rozdz. 5: "len i plotno spadaja w dlugim biegu". PLAN etap 6: "plotna w swiecie nie ubywa szybciej niz 10 dziennie".

## Dowod (liczby z logow, `D:\Backup-Bannerlord\logi-testow\2026-10-20`)
Ksiega towarow ("Towary: dzien N - flax / linen"), okna 20 dob biegu 160 dob z zapisu autotest-F-d240 (WGRANIE 95, `Armoury-2026-10-20_12-25-19.log`):

| doby biegu | len: zapas | len: produkcja (wsie + pola) | len: zuzycie | plotno: zapas | plotno: produkcja | plotno: zuzycie | wsie kupuja plotno (E5/B1) |
|---|---|---|---|---|---|---|---|
| 0-19 | 11 296 | 924 | 1 117 | 4 529 | 369 | 364 | 143 |
| 20-39 | 11 721 | 973 | 952 | 5 006 | 322 | 281 | 84 |
| 40-59 | 12 181 | 919 | 896 | 8 657 | 506 | 324 | 72 |
| 60-79 | 9 504 | 320 (200 + 121) | 454 | 7 327 | 345 | 411 | 91 |
| 80-99 | 6 885 | 284 (158 + 126) | 415 | 6 577 | 310 | 347 | 77 |
| 100-119 | 5 160 | 280 (155 + 125) | 366 | 6 050 | 281 | 308 | 61 |
| 120-139 | 3 975 | 282 | 341 | 5 678 | 266 | 266 | 36 |
| 140-159 | 3 140 | 280 | 322 | 5 492 | 253 | 262 | 36 |

- Ten sam obraz w czterech biegach 160 dob (WGRANIE 91 `00-44-29`, 92 `04-01-39`, 95-fala1 `09-14-49`, 95 `12-25-19`): KS zastosowany od doby 109111/2
  (UZBROJONY 109076 - samo-uzbrojenie, 28 dob ksiegi + 7 dob Planow), w dobach D..D+14 (109126-109140) plon lnu wsi spada z 1014 do 152 dziennie
  (linia "KS: produkcja (zastosowane)": stawka KS lnu 145-173 szt./d przez caly bieg - 158 / 165 przy wlaczeniu, welny 214 / 218; przed wlaczeniem wsie
  len 960-1034, welna 352-360).
- **Sukno (recenzja "gra", sprawdzone):** zapas sukna swiata na koncu okien 40 dob: 12-25-19 5 826 -> 4 499 -> 3 120 -> 2 427; 09-14-49 5 731 -> 4 400 -> 3 159
  -> 2 674 -> 2 707; 00-44-29 i 04-01-39 (recenzja) 5 704 / 5 383 -> 2 392 / 2 337. Od D+28 do konca: nachylenie -23.3 (12-25-19) i -20.7 szt./d (09-14-49);
  sukno produkcja 175 / zuzycie 201, welna 267 / 263 (welna idzie cala do tkaczy, zapas welny stoi ok. 5 000). Wsie kupuja sukno w miastach: 1-9 szt./d przed KS,
  36-61 po KS. Plan wlaczenia chce welny na targ 213 + 14 (dzis 352). Nowa kampania bez KS (`08-45-54`): sukno 2 234 -> 2 608 -> 2 936 (+17.8 szt./d).
- **Skora - poza regula (sprawdzone):** tez ostatni szczebel koszyka, ale zapas skory 2 568 -> 2 509 -> 2 269 -> 2 378 (prawie staly), produkcja = zuzycie
  (221 -> 327), skory surowe rosna 4 964 -> 17 240 - braku nie ma, a skora w Planie nie ma pracy domowej (KsBasket.RawGood[Leather] = -1).
- Nowa kampania 120 dob bez KS (`08-45-54`): len 720-1130 szt./d, zapas lnu 6 677 -> 11 310 (+53.5/d), plotna 1 304 -> 3 841 (+19.5/d) - spadek jest skutkiem KS,
  nie tkalni ani zagli.
- Po wlaczeniu tkacze miast (148) tkaja recepture KS 1.25 lnu na plotno (315 lnu -> 253 plotna; przed KS 3.0: 969 -> 323) - receptura nie jest przyczyna.
- Plan KS chce na targ 284 szt. lnu/d przy wlaczeniu (153 wsie + 131 pola mieszczan, linia "KS: na targ (plan doba 109109)"); jego popyt na len =
  1.25 x popyt na plotno, na welne = 1.45 x popyt na sukno (KsCore.Demand: potrzeby ludzi + 1:1 odziezy wojska, warsztatow zbrojnych, napraw, dworow).
  Plotna i sukna kupowanego przez wsie w miastach (ramka E5/B1) Plan nie liczy ponad koszyk zaplacony: niezaplacony koszyk wsi (plotno 116 W + 69 E,
  sukno 83 W + 45 E szt./d, linia "KS: koszyk niezaplacony") bierze jako "robiony w domu" z lnu i welny, a w grze nikt nie tka w domu - wsie kupuja to
  z kiesy w miescie targowym. Zaplacona czesc koszyka wsi jest mala: plotno 3-6, sukno 2-3 szt./d (linia "Koszyk stanow (ZC4)", "linen 45+3" = miasta + wsie).
- Rece pod KS stoja na Planie wlaczenia: A7 (Ks Switch Signal Rule TAK u Jeffa) przesuwa je tylko za cena cienia Planu - len W 0.54, E 0.90 (Plan uwaza
  len za wystarczajacy), wiec w 120 dobach po wlaczeniu na len przeszedl 1 blok (fisherman -> flax_plant, 1 327 rak; bieg 12-25-19; w 09-14-49 - 0).
  Regula f20-d4 (rece za polkami rynku) len pomija z zalozenia ("welna, len ... bez sygnalu") i przy czynnej A7 i tak nie dziala.
- Regulator HarvestValve: len x1.30 (sufit) od poczatku - pod KS stawka KS zastepuje regulator, wiec nie pomaga.
- Krainy: miast bez lnu 23-51 z 97 (bez plotna 5-12) - plotno jest w miastach, maleje zapas swiata.

## Przyczyna
Popyt Planu KS na plotno i sukno nie obejmuje plotna i sukna kupowanego przez wsie w miastach (E5/B1) ponad koszyk zaplacony Planu - Plan liczy te sztuki
jako "robione w domu". Przy wlaczeniu KS Plan daje rekom wsi len tylko na 284 szt./d (zamiast ok. 400, ktore zuzywaja tkacze po wlaczeniu) i welne na 227,
a potem A7 nie widzi braku (niska cena cienia lnu). Zapas lnu, plotna i sukna swiata topnieje, az ceny zdusza zakupy (wsie 143 -> 36 szt./d plotna).

## Zmiana
- Nowy plik `Armoury/src/StartCalibration.M96Len.cs`, klucz **KsVillageClothBuysInPlan** (domyslnie TAK): co Plan (watek glowny, BuildInput) popyt 1:1 plotna
  i sukna dostaje max(0, srednia zakupow towaru przez wsie w miastach z ksiegi towarow (ramka E5/B1, najwyzej 28 dob, od 5 dob pomiaru; 2 pierwsze doby sesji
  pominiete) - koszyk wsi tego towaru zaplacony w poprzednim Planie (KsMoney.PaidV, szt. gry/d)). Popyt Planu na len rosnie o 1.25 x tyle, na welne o 1.45 x tyle -
  wiecej rak przy lnie i owcach przy wlaczeniu, a po wlaczeniu wyzsza cena cienia lnu i welny w sygnale A7. Bez mnoznika plonu, bez zlota i towaru (zmienia sie
  tylko liczba popytu w Planie).
- Wpiecia: `GoodsLedger.KsNote` (1 linia - liczba doby), `StartCalibration.Core.cs` (pole KsItem.VilBuy, pole KsMoney.PaidV, 1 linia w OneToOne, 2 linie
  w kroku 8 - czyszczenie i suma paid x q), `StartCalibration.Plan.cs` (BuildInput i PlanReset - po 1 linii), `Settings.cs` (klucz), `McmSettings.cs` (gen_mcm),
  `tools/sprawdz_logi.py` (grupa m96len).
- Zapis: bez zmian (srednia od zera w kazdej sesji, nic w SyncData; KsMoney - tylko wynik Planu w pamieci) - zapis Jeffa z 94/95 wczytuje sie jak dotad;
  cofniecie DLL bezpieczne.
- Linia logu przy kazdym Planie: **`Len KS (m96-len): plan doba N`** - osobno plotno i sukno: zakupy wsi (srednia, liczba dob, ostatnia doba), koszyk zaplacony,
  dopisek i jego surowiec (szt./d i t/d w Planie), powod; potkniecia.

### Poprawki po recenzjach (20.10, druga runda)
1. **(wazne, PRAWDA) Zle jednostki zaplaconego koszyka.** `KsResult.DistPaid` to ULAMEK zaplacony 0..1 na pozycje koszyka (KsBasket.Ladder: `paid[z] = f`;
   Core.cs `fp[z] = (float)paid[z]`), a poprzednia wersja sumowala te ulamki jak kg i dzielila przez sigma x 10 - wychodzilo najwyzej 1.5 szt./d (w praktyce ok. 0.1),
   wiec odjecie czesci zaplaconej nie dzialalo, a liczba w logu nie nadawala sie do sprawdzenia ryzyka. Teraz krok 8 sumuje zaplacone kg koszyka wsi
   (`KsMoney.PaidV[m * N + z] += q[z] - u[z]` = paid x q, ostatnia runda - tak jak `UnpaidV`), a regula przelicza je jak linia "KS: koszyk niezaplacony"
   (kg / (sigma x kg sztuki)). Oczekiwane: plotno ok. 3-6, sukno ok. 2-3 szt./d (linia ZC4 "linen 45+3").
2. **(wazne, PRAWDA) Sukno tym samym mechanizmem.** Sprawdzone na ksiedze (liczby wyzej, Dowod). Regula obejmuje teraz plotno i sukno (te dwie pozycje koszyka
   wsi Plan bierze jako robione w domu z surowca: KsBasket.RawGood Linen -> len, Felt -> welna); klucz przemianowany z KsLinenVillageBuysInPlan na
   **KsVillageClothBuysInPlan** (galaz jeszcze nie wgrana - zaden zapis MCM go nie ma). Skora poza regula z dowodem (zapas staly, skory surowe rosna).
   W sprawdz_logi: M96LEN-6 (zapas sukna, prog >= -10 szt./d, baza -23.3) i M96LEN-7 (welna i sukno, INFO).
3. **(drobne, PRAWDA - opisane, bez zmiany kodu)** Kupiona czesc zostaje tez w niezaplaconym koszyku kroku 8 (`u[z]` - surowiec pracy domowej okregu z lnem
   i owcami): Plan dalej trzyma dla niej troche rak "w domu". To nie jest drugi popyt na targu, tylko stara strata rak pracy domowej (rece w domu w grze nic
   nie tkaja), ok. 0.1% popytu swiata (plotno ok. 100 szt./d x 10 kg x sigma 37 x 1.25 = ok. 46 t/d lnu wobec ok. 60 tys. t/d). Zmiana `u[z]` ruszalaby rece
   pracy domowej i kcal racji wszystkich okregow - za duzo jak na ten zysk. Zdanie "plotno nie liczone dwa razy" dotyczy tylko popytu targu.
4. **(drobne / wazne, PRAWDA) Skok zakupow po wczytaniu zapisu.** Plotno wsi w 1. i 2. dobie po wczytaniu: 433 i 245/246/250/251 (wszystkie 4 biegi), potem
   120-150; nowa kampania 74, 37 (bez skoku). Srednia pomija teraz 2 pierwsze doby sesji i liczy od 5 dob pomiaru - Plan wlaczenia ok. 7-8 dob po wczytaniu
   (pomiar pieniedzy z 7 dob sesji) dostaje dopisek z 5 zwyklych dob (symulacja na 12-25-19, Plan 109082: 140 zamiast 195 z dwoma skokami).

## Ryzyko / co sprawdzic
- Kontrola calosci: OneToOne("linen") / OneToOne("felt") wola tylko KsCore.Demand (D[len] = (potrzeby + 1:1 plotna) x 1.25, D[welna] = (potrzeby + 1:1 sukna) x 1.45);
  Corridors bierze tylko jedzenie. Zakupy wsi (E5/B1) nie byly w zadnym polu 1:1 (Army/Cast/Court/Cloth), a czesc zaplacona koszyka (w popycie przez potrzeby
  ludzi) jest odjeta. Klucz NIE = VilBuy 0 = Plan co do bitu (PaidV tylko liczone, nigdzie wiecej nie czytane). Skala: len +125-130, welna +25-35 szt./d przy
  wlaczeniu = ok. 60 t/d Planu przy popycie swiata ok. 60 tys. t/d (0.1%) - rece z jedzenia znikomo.
- Zaplacone liczone z POPRZEDNIEGO Planu (7 dob wczesniej) - pierwszy Plan sesji bez dopisku ("brak poprzedniego Planu"); zmiana koszyka zaplaconego w 7 dobach
  jest mala (ZC4: 45+3 -> 48+4 przez caly bieg).
- Kampania Jeffa (save040): KS uzbrojony od doby 108837, Plan dojrzaly 108862 - wlaczy sie najpewniej jeszcze dzis na WGRANIU 95 (przed ta poprawka).
  Wtedy rece lnu i owiec zostana z Planu wlaczenia i poprawka dziala tylko przez cene cienia lnu i welny w sygnale A7 (hamulec Jeffa: x1.25 przez 60 dob, 5% rak
  regionu na sezon) - efekt w jego kampanii wolniejszy (miesiace) i do sprawdzenia. Gdyby A7 lnu nie ruszyla, nastepny krok: rece lnu za polkami rynku
  jak f5-ruda dla zelaza (regula pod KS bez kosztu, 5% na 7 dob) - NIE zrobione.
- Sukno: przy Planie wlaczenia wsie kupuja jeszcze malo sukna (srednia ok. 19-25 szt./d - zakupy rosna dopiero po KS do 36-61), wiec dopisek welny przy wlaczeniu
  jest maly (ok. +25-35 szt./d), a reszte robi A7 z kolejnych Planow. M96LEN-6 moze zostac ponizej progu - wtedy to ten sam krok co dla lnu (rece za polkami).
- Watki: srednia liczona w GoodsLedger.KsNote i czytana w BuildInput - oba na watku glownym; PaidV pisany na watku roboczym przed oddaniem wyniku (volatile
  _wOut), czytany na watku glownym z gotowego `_plan`; rachunek Planu czyta tylko gotowe pole KsItem.VilBuy.
- Bez alokacji w goracych sciezkach (raz na dobe, dwa porownania napisow na towar ksiegi); linia logu raz na Plan (co 7 dob).

## Status
DO SPRAWDZENIA (kod + build kod 0; bez biegu w grze). Narzedzie `--grupa m96len` sprawdzone na logach bazowych (12-25-19, 09-14-49, 08-45-54) i na linii
w nowym formacie (log syntetyczny).

## Test w grze
- Bieg 160 dob z zapisu autotest-F-d240 (KS wlacza sie w dobie ok. 37): `python -I tools/sprawdz_logi.py --grupa m96len <Armoury-*.log>`.
  Baza WGRANIE 95 (12-25-19): M96LEN-2 len -79.0 NIE, M96LEN-3 plotno -21.2 NIE, M96LEN-6 sukno -23.3 NIE, M96LEN-4 0.78 (281 / 362) NIE;
  09-14-49: -48.0 / -20.4 / -20.7 / 0.84.
- Oczekiwane:
  - M96LEN-0 TAK (ok. 23 Planow, w kazdej linii plotno i sukno, potkniecia 0).
  - M96LEN-1 TAK - przy Planie wlaczenia (doba ok. 109109): plotno zakupy wsi ok. 105-110, zaplacone ok. 3-6, dopisek ok. 100-105 szt./d; sukno zakupy
    ok. 19-25, zaplacone ok. 2-3, dopisek ok. 17-23 szt./d (symulacja sredniej na 4 biegach bazowych). Zaplacone >= zakupy (dopisek 0) = przyczyna inna, szukac dalej.
  - "KS: na targ (plan doba 109109)": len ok. 400-420 zamiast 284, welna ok. 250-260 zamiast 227.
  - "KS: produkcja (zastosowane)": stawka lnu ok. 220-300 zamiast 158-165, welny ok. 235-250 zamiast 214-218.
  - W oknie D+28..koniec: M96LEN-4 len produkcja / zuzycie >= 0.90; M96LEN-2 / M96LEN-3 nachylenie zapasu lnu i plotna >= -10 szt./d;
    M96LEN-6 sukno >= -10 (co najmniej lepiej niz -23); M96LEN-5 zakupy plotna przez wsie nie spadaja ponizej ok. 60 szt./d (baza 53).
- Nowa kampania 120 dob (KS sie nie wlacza): M96LEN-0 TAK, M96LEN-1 TAK (dopisek tylko w Planie, KS nie zastosowany - gra bez zmian), M96LEN-2..7 INFO
  jak w bazie (len +53.5/d, plotno +19.5/d, sukno +17.8/d, len 868 / 806).

## Uwaga do prosby "menu zamku tez uporzadkuj"
Ta galaz menu zamku nie dotyka. Porzadek menu zamku robi osobna galaz `w-toku/m96-zamek` (commit 09f65934, klucz CastleMenuTidy) - tam recenzja i test.
