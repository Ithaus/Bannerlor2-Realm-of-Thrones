# m96-len - len i plotno pod kalibracja startu (KS) (20.10, galaz w-toku/m96-len od 848b2e80)

**Dla Jeffa (prosto):** Gdy po ok. 5 tygodniach kampanii wlacza sie kalibracja startu, wsie przestawiaja rece z lnu na inne prace: lnu z pol robi sie
z ok. 1000 sztuk dziennie ok. 280, a tkacze zuzywaja 320-450. Dlatego przez pol roku len i plotno w swiecie topnieja (len 12 tys. -> 3 tys., plotno
8,6 tys. -> 5,5 tys.). Przyczyna: plan kalibracji nie wiedzial, ze wsie kupuja plotno w miastach - uznal, ze tka je w domu (bez lnu i bez rak).
Teraz plan liczy plotno, ktore wsie naprawde kupuja, wiec wiecej rak zostaje przy lnie - tyle lnu, ile trzeba tkaczom, bez zadnego mnoznika plonu.
MCM: **Ks Linen Village Buys In Plan** (wlaczone; wylaczone = jak dotad).

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
  (linia "KS: produkcja (zastosowane)": stawka KS lnu 145-173 szt./d przez caly bieg; przed wlaczeniem wsie 960-1034).
- Nowa kampania 120 dob bez KS (`08-45-54`): len 720-1130 szt./d, zapas lnu 6 809 -> 11 310, plotna 1 887 -> 3 841 - spadek jest skutkiem KS, nie tkalni ani zagli.
- Po wlaczeniu tkacze miast (148) tkaja recepture KS 1.25 lnu na plotno (315 lnu -> 253 plotna; przed KS 3.0: 969 -> 323) - receptura nie jest przyczyna.
- Plan KS chce na targ 284 szt. lnu/d przy wlaczeniu (153 wsie + 131 pola mieszczan, linia "KS: na targ (plan doba 109109)"); jego popyt na len =
  1.25 x popyt na plotno (KsCore.Demand: potrzeby ludzi + 1:1 odziezy wojska, warsztatow zbrojnych, napraw, dworow). Plotna kupowanego przez wsie
  w miastach (ramka E5/B1, 36-143 szt./d) Plan nie liczy ponad koszyk zaplacony: niezaplacony koszyk wsi (plotno 115 W + 69 E = 184 szt./d, len 85 + 66)
  bierze jako "robiony w domu" (linia "KS: koszyk niezaplacony"), a w grze nikt nie tka w domu - wsie kupuja to plotno z kiesy w miescie targowym.
- Rece pod KS stoja na Planie wlaczenia: A7 (Ks Switch Signal Rule TAK u Jeffa) przesuwa je tylko za cena cienia Planu - len W 0.54, E 0.90 (Plan uwaza
  len za wystarczajacy), wiec w 120 dobach po wlaczeniu na len przeszedl 1 blok (fisherman -> flax_plant, 1 327 rak; bieg 12-25-19; w 09-14-49 - 0),
  a najczestsze przejscia to wheat_farm -> silk_plant i fisherman -> wheat_farm.
  Regula f20-d4 (rece za polkami rynku) len pomija z zalozenia ("welna, len ... bez sygnalu") i przy czynnej A7 i tak nie dziala.
- Regulator HarvestValve: len x1.30 (sufit) od poczatku, zuzycie / produkcja 1.23-1.76 po wlaczeniu - pod KS stawka KS zastepuje regulator 221, wiec nie pomaga.
- Krainy: miast bez lnu 23-51 z 97 (bez plotna 5-12) - plotno jest w miastach, maleje zapas swiata.

## Przyczyna
Popyt Planu KS na plotno nie obejmuje plotna kupowanego przez wsie w miastach (E5/B1) ponad koszyk zaplacony Planu - Plan liczy te sztuki jako
"robione w domu". Przy wlaczeniu KS Plan daje rekom wsi len tylko na 284 szt./d (zamiast ok. 400, ktore zuzywaja tkacze po wlaczeniu), a potem A7 nie
widzi braku (niska cena cienia lnu). Zapas lnu i plotna swiata topnieje, az ceny zdusza zakupy (wsie 143 -> 36 szt./d plotna).

## Zmiana
- Nowy plik `Armoury/src/StartCalibration.M96Len.cs`, klucz **KsLinenVillageBuysInPlan** (domyslnie TAK): co Plan (watek glowny, BuildInput) popyt 1:1 plotna
  dostaje max(0, srednia zakupow plotna przez wsie w miastach z ksiegi towarow (ramka E5/B1, najwyzej 28 dob, od 7 dob pomiaru) - plotno koszyka wsi
  zaplacone w poprzednim Planie (KsResult.DistPaid)). Popyt Planu na len rosnie o 1.25 x tyle - wiecej rak przy lnie przy wlaczeniu, a po wlaczeniu wyzsza
  cena cienia lnu w sygnale A7. Bez mnoznika plonu, bez zlota i towaru (zmienia sie tylko liczba popytu w Planie).
- Wpiecia: `GoodsLedger.KsNote` (1 linia - liczba doby), `StartCalibration.Core.cs` (pole KsItem.VilBuy, 1 linia w OneToOne), `StartCalibration.Plan.cs`
  (BuildInput i PlanReset - po 1 linii), `Settings.cs` (klucz), `McmSettings.cs` (gen_mcm), `tools/sprawdz_logi.py` (grupa m96len).
- Zapis: bez zmian (srednia od zera w kazdej sesji, nic w SyncData) - zapis Jeffa z 94/95 wczytuje sie jak dotad; cofniecie DLL bezpieczne.
- Linia logu przy kazdym Planie: **`Len KS (m96-len): plan doba N`** (zakupy wsi, koszyk zaplacony, dopisek plotna i lnu, powod, potkniecia).

## Ryzyko / co sprawdzic
- Kontrola calosci: OneToOne("linen") wola tylko KsCore.Demand (D[len] = (potrzeby + 1:1 plotna) x 1.25); Corridors bierze tylko jedzenie. Zakupy wsi
  (E5/B1) nie byly w zadnym polu 1:1 (Army/Cast/Court/Cloth), a czesc zaplacona koszyka jest odjeta - plotno nie liczone dwa razy. Klucz NIE = VilBuy 0 =
  Plan co do bitu. Skala: +60-180 szt. lnu/d = ok. 25-70 t/d Planu przy popycie swiata ok. 60 tys. t/d (0.1%) - rece z jedzenia znikomo.
- Nie sprawdzone w biegu: ile koszyka wsi Plan uznaje za zaplacone (KsResult.DistPaid nie jest w logu) - jesli zaplacone >= zakupy wsi, dopisek = 0
  (M96LEN-1 INFO z powodem). Wtedy przyczyna jest inna niz zakladam i trzeba dalej szukac.
- Kampania Jeffa (save040): KS uzbrojony od doby 108837, Plan dojrzaly 108862 - wlaczy sie najpewniej jeszcze dzis na WGRANIU 95 (przed ta poprawka).
  Wtedy rece lnu zostana z Planu wlaczenia i poprawka dziala tylko przez cene cienia lnu w sygnale A7 (hamulec Jeffa: x1.25 przez 60 dob, 5% rak
  regionu na sezon) - efekt w jego kampanii wolniejszy (miesiace) i do sprawdzenia. Gdyby A7 lnu nie ruszyla, nastepny krok: rece lnu za polkami rynku
  jak f5-ruda dla zelaza (regula pod KS bez kosztu, 5% na 7 dob) - NIE zrobione.
- Watki: srednia liczona w GoodsLedger.KsNote i czytana w BuildInput - oba na watku glownym; rachunek Planu na watku roboczym czyta tylko gotowe pole KsItem.
- Bez alokacji w goracych sciezkach (raz na dobe, jedna liczba); linia logu raz na Plan (co 7 dob).

## Status
DO SPRAWDZENIA (kod + build kod 0; bez biegu w grze).

## Test w grze
- Bieg 160 dob z zapisu autotest-F-d240 (KS wlacza sie w dobie ok. 37): `python -I tools/sprawdz_logi.py --grupa m96len <Armoury-*.log>`.
  Baza WGRANIE 95 (12-25-19): M96LEN-2 len -79 szt./d NIE, M96LEN-3 plotno -21 NIE, M96LEN-4 0.78 NIE.
- Oczekiwane: M96LEN-0 TAK (ok. 23 Planow, potkniecia 0); M96LEN-1 TAK - dopisek przy Planie wlaczenia ok. 50-110 szt. plotna/d (srednia zakupow wsi
  dob 0-35 ok. 110 minus czesc zaplacona); "KS: na targ (plan doba 109109)" len ok. 350-420 zamiast 284; "KS: produkcja (zastosowane)" stawka lnu
  ok. 220-300 zamiast 145-173; w oknie D+28..koniec: len produkcja / zuzycie >= 0.90 (M96LEN-4), zapas lnu i plotna nachylenie >= -10 szt./d
  (M96LEN-2/3); zakupy plotna przez wsie nie spadaja ponizej ok. 60 szt./d (M96LEN-5).
- Nowa kampania 120 dob: KS nie wlacza sie przez 120 dob - M96LEN-0 TAK, M96LEN-2..5 INFO (bez zmian wobec bazy: len +53/d, plotno +20/d).
