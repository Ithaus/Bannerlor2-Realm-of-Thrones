# n22f-najemnicy - prog P3W3-3 "ludzie najemnikow AI" po fali 1 (galaz w-toku/n22f-najemnicy na 4885ae75, poprawki przed WGRANIEM 93)

## Dla Jeffa (prosto)
Narzedzie testow pokazalo "za malo najemnikow" w biegu nowej kampanii z fala 1 (1 679 ludzi w kompaniach, prog 2 000). Sprawdzilem
osiem biegow tej nocy i 16 starszych: kompanie najemne nie sa biedne (kazda ma ok. 200 tys. zl w kiesie i moze placic o jedna trzecia
wiecej ludzi, niz ma), a ich liczba skacze z biegu na bieg o kilkaset ludzi, bo zalezy od tego, kto je najmie i gdzie walcza. Ten sam bieg
bez fali 1 (WGRANIE 92, w Twojej grze) tez dal za malo (1 869), a w biegu 160 dob z zapisu fala 1 wypada dokladnie w sredniej 12 buildow.
To nie jest blad w grze, tylko zle ustawiony prog narzedzia (liczba z konca roku przykladana do 120 dob). Kodu gry nie zmienialem -
poprawilem tylko narzedzie testow.

## Problem
- Grupa `etap8`, wiersz `P3W3-3: ludzie najemnikow AI (sr28)`, prog `>= 2 000`: n22anowa (92 + 15 galezi fali 1, 91bf9c63) **1 679 - NIE**;
  nowa 2 236, nowa88 2 114, r2nowa 2 153, r3nowa 2 142 - TAK.

## Dowod
1. **Skad liczba.** `Armoury/src/MercContract.cs` `Daily()` - linia doby `Kontrakty najemnikow (185): dzien N | ... | ludzie najemnikow AI X / z umowy ...`;
   X = suma `MemberRoster.TotalRegulars` aktywnych partii wojennych rodow AI w sluzbie najemnej (`Company`). Narzedzie (`etap8_p3woj`) - srednia X
   z ostatnich 28 dob okna. Ta sama liczba jest w `budzet-rodow.csv` (wiersze `rodzaj = najemnik`, kolumna `ludzi_partie`) - zgodna z linia (r3nowa d113-140: 2 251 / 2 251).
2. **Wszystkie biegi (sr28 z linii 185):**
   | bieg | sr28 | d29-56 | d57-84 | d85-112 | d113-140 |
   |---|---|---|---|---|---|
   | r2nowa (kandydat 90) | 2 153 | 2 047 | 2 164 | 2 217 | 2 062 |
   | r3nowa (WGRANIE 91) | 2 142 | 1 737 | 2 240 | 2 121 | 2 251 |
   | **n21nowa (WGRANIE 92, bez fali 1)** | **1 869 (< 2 000)** | 2 262 | 2 077 | 1 890 | 1 793 |
   | n22anowa (92 + fala 1) | 1 679 | 2 226 | 1 900 | 1 723 | 1 594 |
   | r2160 / r3160 / n21160 / n22a160 (z zapisu d240, sr28 = d373-400) | 2 421 / 2 403 / 2 295 / 2 197 | | | | |
   - Biegi 160 dob z TEGO SAMEGO zapisu (ten sam stan startu, rozni sie tylko kod) - 12 buildow 17-20.10 z `budzet-rodow.csv`: srednia d29-161
     1 936, 2 046, 2 211, 1 996, 2 180, 2 176, 2 138, 2 080, 2 117 (r2), 2 047 (r3), **1 843 (n21)**, **2 081 (n22a = fala 1)**; srednia 2 071, odchylenie ok. 105.
     Fala 1 jest w sredniej; najnizszy jest n21 (bez fali 1). W d57-140 n22a160 mial o 250-510 ludzi WIECEJ niz n21160.
   - Biegi 120 dob z zapisu d240 (6 buildow 17.10): 2 115, 2 208, 2 062, **1 763**, 2 129, 2 326 - rozrzut 560 ludzi miedzy buildami tego samego dnia.
   - Narzedzie progow nie liczylo P3W3-3 dla biegow 160 dob (okno 1-120 dob kampanii, a biegi sa w d240-400 - "0 dob"); policzone z linii 185 i CSV wyzej.
3. **Zloto i pulap nie wiaza** (CSV, sr28 wierszy `najemnik`): ludzie / limit partii 25% (n22anowa) - 28% (n21nowa) - 32% (r3nowa, n22a160);
   zold / pulap 166 (`CapOf` = zold ludzi z umowy) 62% / 68% / 76% / 69%; kiesa na kompanie 211 / 201 / 231 / 286 tys. zl; zwolnienia 166 kompanii 0-15
   ludzi na dobe. Marza: kontrakt `d_kontrakt` / zold partii = 1.30 (n22anowa d113-140: 11 097 / 8 553), 1.27 (n21nowa) - jak zamierzone 1.30.
   Zaplacone = nalezne w kazdym biegu (niedoplata <= 3%), odejsc po niedoplacie 0, kompanii bez ludzi w polu 0-0.07 na dobe. Prog mowi
   "mniej = marza 0.30 nie starcza kompaniom" - tego tu nie ma: kompanie maja luz w zlocie i w pulapie, a i tak nie dobieraja ludzi.
4. **Swiat bez zmiany:** ludzie w partiach lordow AI d113-140: r3nowa 94 000, n21nowa 98 288, n22anowa 94 916 (werbunek swiata ten sam);
   bitwy H3 w 120 dobach: 1 961 / 1 992 / 1 870, zabici H3 24 809 / 23 891 / 22 267 (fala 1 mniej zabitych: R3-10c chirurg i Taktyka -
   oczekiwani zabici przegranych 23 946 -> 21 770, -6.4% - w strone WIEKSZEJ liczby ludzi, nie mniejszej). Kompanie w n21nowa sluzyly
   Qohorowi, Norvos, Myr, Sarnorowi, Dothrakom, Pentos, Smoczej Skale; w n22anowa - Targaryenom, Tyrosh, Volantis, Nocnej Strazy, Braavos, Lys,
   Skagos, Sarnorowi - inne wojny i inny werbunek w kazdym biegu.
5. **Kod fali 1** (`git diff 3957fa13 91bf9c63 -- '*.cs'`): zadna galaz nie dotyka `MercContract`, `CapOf`, werbunku kompanii ani linii 185.
   `AiDowry` i `VassalRansomAid` wprost pomijaja najemnikow (`IsUnderMercenaryService`), `BorderGarrisons` zmienia tylko cel zalog lenn w pokoju
   (kompanie nie maja lenn), `TourneyRansom` czyta zold tylko do wyceny wykupu, `LoserSkills` zmniejsza smiertelnosc przegranych.

## Przyczyna
Zmiennosc biegow, nie regresja. Prog 2 000 pochodzi z biegu rocznego (2 531 w d337-364, srednia roku 2 231 - CHANGELOG P3/W3-B) i wedlug
docstringu `etap8_p3woj` ("liczby 'na dobe 337-364' wiaza przy oknie >= 337 dob") powinien wiazac tylko w biegu rocznym - jak P3W-5, P3W1-7
i P3W2-4 w tej samej funkcji. W wierszu P3W3-3 brakowalo warunku `year`, wiec liczba z konca roku byla przykladana do ostatnich 28 dob
biegu 120 dob, gdzie kompanie dopiero rosna (d1-28 ok. 900-1 500), a pojedynczy bieg ma rozrzut ok. +-250 ludzi.

## Zmiana (tylko narzedzie, kod gry bez zmian)
`tools/sprawdz_logi.py`, `etap8_p3woj`:
1. `P3W3-3`: prog `>= 2 000` wiaze przy oknie >= 337 dob (`year`); krotsze okno - podloga `>= 1 500` (ponizej kazdego z 24 biegow 120-160 dob,
   1 679 - 2 421; zalamanie kompanii jak C1-m "-79%" albo brak marzy daje duzo mniej). Uzasadnienie z liczbami w komentarzu przy wierszu.
2. Nowy wiersz INFO `P3W3-3b: kompanie w sluzbie (sr28): ludzie / limit partii, zold / pulap 166, kiesa na kompanie` z `budzet-rodow.csv` -
   przy NIE w P3W3-3 od razu widac, czy to marza (zold przy pulapie, kiesy na wyczerpaniu), czy wojny i werbunek (luz w obu).

Wynik po zmianie (blok `etap8_p3woj` na logach z katalogu gry): n22anowa P3W3-3 **TAK 1 679**, P3W3-3b `1632 / 6516 (25%), 9036 / 14611 zl (62%),
210911 zl`; n21nowa TAK 1 869; r3nowa TAK 2 142; n22a160 z `--do-doby 400` (okno roczne, prog 2 000) TAK 2 197.

## Ryzyko / co sprawdzic (zasada 0)
- Nic w grze sie nie zmienia (bez DLL). Narzedzie: P3W3-3 w biegu 120 dob jest teraz lagodniejsze - prawdziwe zalamanie najemnikow ponizej 1 500
  dalej daje NIE; spadek 1 500-2 000 w krotkim biegu jest TAK, wiec przy takim wyniku czytac P3W3-3b i P3W3-4 (kiesy).
- Bieg roczny bez zmian (prog 2 000 jak dotad). Biegi 160 dob z zapisu d240 trzeba puszczac z `--do-doby 400` (domyslne 120 daje "0 dob").
- Otwarte (nie fala 1): kiesy kompanii w biegach nowej kampanii spadaja (P3W3-4: r3nowa -2.6, n21nowa -5.4, n22anowa -4.5 tys. zl/d na 11 kompanii,
  ok. -400 zl/d na kompanie przy kiesie ok. 210 tys. - zapas ponad rok); z zapisu d240 n22a160 +0.9 tys. zl/d. Patrzec w biegu rocznym.
- Inne wiersze narzedzia bez zmian; skladnia sprawdzona (`ast.parse`), plik z CRLF jak dotad.

## Test w grze
Nie dotyczy (bez DLL). Po nastepnym biegu: `python -I tools/sprawdz_logi.py --grupa etap8 <Armoury-*.log>` - P3W3-3 TAK przy >= 1 500
(120 dob) i wiersz P3W3-3b; dla biegu 160 dob z zapisu `--do-doby 400`.
