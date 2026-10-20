# p5-zalogi - wieksze zalogi zamkow granicznych w pokoju (R3-08g, POMYSLY-35 nr 26), galaz w-toku/p5-zalogi na 5189c0fe (kandydat WGRANIA 92)

## Dla Jeffa (prosto)
W pokoju zamek AI trzyma dzis 70 zbrojnych na kazde 100 z czasu wojny - wszedzie tak samo. Teraz zamek albo miasto, ktore ma "za miedza"
zamek lub miasto innego krolestwa (nie sojusznika), trzyma 75 na 100; zamki w glebi kraju dalej 70. "Za miedza" znaczy: obca twierdza
blizej niz ok. pol doby marszu armii (60 jednostek mapy; w Westeros najblizszy sasiad stoi zwykle 30-60 jednostek dalej). Placi pan zamku
ze swojego pulapu pokojowego jak dotad - biedny rod nie wyda wiecej, niz ma w pulapie, bo zalogi rodu i tak sa przycinane do 80% pulapu.
Twoich zalog to nie dotyczy; wylacznik: MCM "Border Garrisons Peace".

## Problem / zrodlo pomyslu
- POMYSLY-35 nr 26 (R3-08g), Jeff 20.10: "28 - nie, 29 - nie. Pozostale tak".
- Audyt 09.10 `docs/audyt-2026-10-09/12-DOCHODY-RODOW.md` W-10 i krytyka pkt 32: po 60 dobach pokoju zalogi wracaja do stanu wojennego
  dopiero po ok. 20 dobach wojny (zalogi 37 -> 71 tys. w ok. 20 dob), a oblezenia potrzebuja obroncow od 1. dnia - "do rozwazenia po
  autotescie: zamki przygraniczne 75% zalogi w pokoju".
- Stan dzis (kod 5189c0fe): `ClanBudget.Garrisons` - cel pokojowy kazdej zalogi rodu AI = `GarrisonPeaceShare` (0.70, Armoury.json Jeffa 0.7)
  x srednia zoldu zalogi z dob wojny, bez rozroznienia granica / glab kraju.
- Log testu 160 dob na WGRANIU 91 (`Modules/Armoury/Armoury-2026-10-20_00-44-29.log`, d109076-d109236): "Budzet rodow (166)" - rody AI
  w pokoju **7-10 z 547** (wojna 537-541), odcinek "zalogi w pokoju (B4)": **zalog z celem 3**, na celu 3, cel 1034 zl/d, przycietych
  do 0.80 pulapu 0. Czyli w tym biegu prawie caly swiat jest w wojnie i regula obejmie dzis malo zalog - zadziala, gdy przyjdzie pokoj.

## Zmiana (Armoury)
1. Nowy plik `Armoury/src/BorderGarrisons.cs`:
   - `PeaceShare(st, s, wr)` - udzial pokojowy zalogi: twierdza graniczna `max(GarrisonPeaceShare, clamp01(GarrisonPeaceShareBorder))`
     (0.75), reszta `GarrisonPeaceShare` (0.70) jak dotad.
   - Mapa granic raz na dobe (pierwsze wolanie doby, po zmianie promienia albo kampanii): wszystkie `Town.AllFiefs` (zamki i miasta),
     para twierdz rozne `MapFaction`, odleglosc w linii prostej <= `BorderGarrisonRadius`, nie sojusznicy (`Kingdom.IsAllyWith`, sojusze gry
     1.3+) -> obie graniczne. Mapa trzyma id twierdz (napisy), nie obiekty kampanii; kampania przez WeakReference.
   - Linia logu **"Zalogi graniczne (R3-08g): dzien N | twierdze graniczne na mapie B z F (...) | rody AI w pokoju: zalog granicznych z celem
     X (udzial 0.75), w glebi kraju Y (udzial 0.70); cel granicznych wyzej o Z zl/d (przed przycieciem do 0.80 pulapu rodu) | graniczne w
     pokoju: nazwa (rod), ... | wedlug frakcji (graniczne/wszystkie): ..."** (podzial wedlug frakcji w pierwszej linii sesji i co 28 dob).
     Pomiar - ciche logi (`LogGate.FullDay`): w grze co QuietLogEveryDays dob, w autotescie codziennie.
2. `ClanBudget.cs` (2 wpiecia): w `Garrisons` `Math.Max(0f, s.GarrisonPeaceShare)` -> `BorderGarrisons.PeaceShare(st, s, wr)`;
   w `Daily` po `Report(s, today)` -> `BorderGarrisons.Report(s, today)` (wlasny try).
3. `Settings.cs` (przy GarrisonPeaceShare, grupa MCM "The crown's income (stage 2)"): `BorderGarrisonsPeace` = true,
   `GarrisonPeaceShareBorder` = 0.75, `BorderGarrisonRadius` = 60. `tools/gen_mcm.py` -> McmSettings.cs (2067 ustawien Armoury; inne mody bez zmian).
   Nowe klucze - Armoury.json Jeffa ich nie ma, wiec dzialaja wartosci z kodu.

## Definicja granicy (decyzja projektu) i pomiar mapy
- Mapa ROT (`Modules/ROT-Map/ModuleData/settlements.xml`, 227 twierdz): odleglosc do najblizszej twierdzy - mediana 44 jedn., 1/4 ponizej 33,
  3/4 ponizej 60, najwiecej 119. Promien 60 = "sasiad za miedza". Przy wlascicielach startowych z XML graniczne: R 60 -> **77 z 227 (34%)**,
  R 75 -> 52%, R 90 -> 69%, R 120 -> 88% (wiecej niz polowa przy 75 to juz nie "granica").
- Przyklady przy R 60 (wlasciciele z XML): Dorzecze 8 z 14 (Darry, Harroway <- Harrenhal; Maidenpool <- Duskendale; Seagard <- Greywater Watch),
  Polnoc 2 z 22 (Greywater Watch, Oldcastle), Dolina 4 z 13 (Bloody Gate <- Saltpans, Gulltown <- Dyre Den przez zatoke), Smocza Skala 5 z 10,
  Straz Nocy 3 z 5 (wobec Wolnych Ludzi; Straz i tak bez celu - b.Zero). Zachod 17 z 21 zawyzone w tym rachunku: jeden zamek (Surusbec) ma
  w XML wlasciciela-postac, nie rod - w grze to zapewne Zachod. Prawdziwe liczby da linia logu po starcie.
- Morze: linia prosta - wyspa albo brzeg za waska zatoka liczy sie jako graniczny (najazd z morza, NavalDLC); Morze Waskie jest szersze niz 60.
- Sojusznik nie jest granica (koniec sojuszu - od nastepnej doby graniczny). Rod niezalezny z twierdza = inna frakcja (graniczny).
- Zmiana wlasciciela: mapa od nowa co dobe - zdobyty zamek od razu robi z sasiadow twierdze graniczne (pokoj dotyczy calego rodu, wiec regula
  dziala tylko u rodow bez zadnej wojny z krolestwem - jak caly cel pokojowy B4).

## Ryzyko / co sprawdzic (kontrola calosci)
- Regresje: `GarrisonPeaceShare` czytaja tylko `ClanBudget.Garrisons` (cel) i `Report` (linia B4) - grep calego drzewa. `_garTarget` czyta
  `ApplyGarrisonLimits` (limit zoldu zalogi, tylko w dol) i `Releases` krok 1 (zwolnienia zalog ponad cel w pokoju) - oba dostaja wyzszy cel
  granicznych bez zmiany kodu. Klucz wylaczony = co do zlotowki jak 5189c0fe.
- Zamknieta ekonomia: nowego zlota nie ma. Platnik - rod pana (pulap pokoju 166), odbiorca - zolnierze (zold gry). Wyzszy cel = wyzszy limit
  `SetGarrisonWagePaymentLimit`, zaloge dobiera werbunek zalog gry jak dotad.
- Biedni panowie zamkow (audyt: 13 z 15 bankrutow): suma celow rodu dalej najwyzej `GarrisonMaxShareOfBudgetPeace` (0.80) pulapu - rod na limicie
  nie placi wiecej, tylko przesuwa czesc celu z zamkow w glebi kraju na graniczne (wspolczynnik k dla wszystkich jego twierdz). Druzyny dostaja
  pulap minus zalogi (min(zold zalog, cel)) - u rodu z zaloga graniczna ponizej celu druzyny maja do ok. 7% celu tej zalogi mniej miejsca.
- Koszt (szacunek): pokojowe zalogi swiata ok. 58 tys./d (bieg dwuletni, d728); 34% granicznych x 0.05/0.70 -> ok. **+1.4 tys./d** w calkowitym
  pokoju. Zamek z zaloga wojenna 300 zl/d: cel 210 -> 225 zl/d.
- Watek: wolane tylko z `ClanBudget.Daily` (watek glowny, raz na dobe); bez wolan z watkow AI, bez alokacji w goracych sciezkach
  (mapa ok. 26 tys. par raz na dobe, napisy do logu najwyzej 12 nazw).
- Potkniecia liczone (`potkniecia N` w linii, pierwszy blad miejsca do logu); wyjatek przy jednej twierdzy = dla niej udzial jak dotad, funkcja sie nie gasi.
- Zapis: brak nowego stanu (mapa liczona z mapy gry co dobe) - nic w SaveText, stare zapisy bez zmian.
- Straz Nocy to osobna frakcja: zamek Polnocy w promieniu 60 od zamku Strazy bylby graniczny (w XML zaden). Zostawione - sasiad innej frakcji.

## Status
DO SPRAWDZENIA - zbudowane (Armoury, kod 0, 0 Error(s)), nie uruchamiane w grze (zakaz w zadaniu).

## Decyzje (projekt Claude, wedlug zasady Jeffa "ekonomie projektuje Claude")
- 0.75 z audytu; graniczna nigdy ponizej glebi kraju (max z obu), wiec podniesienie GarrisonPeaceShare nie odwroci reguly.
- Granica = obca twierdza (inna frakcja, nie sojusznik) w promieniu 60 jedn. w linii prostej; odrzucone: "N najblizszych twierdz" (nie widzi
  odleglosci - w Essos maly kraj z 5 twierdzami bylby caly graniczny przy kazdym promieniu) i droga ladem (wyspy nigdy graniczne, a NavalDLC plywa).
- Glab kraju bez zmian (0.70) - nie obnizamy, zeby nie zabrac obroncow nikomu.

## Test w grze (log_grep)
- `grep "Zalogi graniczne (R3-08g)" Modules/Armoury/Armoury.log` - pierwsza linia sesji z podzialem "wedlug frakcji"; oczekiwane: twierdze
  graniczne ok. 1/4-1/2 wszystkich (przy 227 twierdzach ok. 60-110), Polnoc mala czesc, krolestwa w srodku mapy (Dorzecze, Zachod) duza.
- W dobie z rodami w pokoju: "zalog granicznych z celem X" + "w glebi kraju Y" = "zalog z celem" w odcinku B4 linii "Budzet rodow (166)"
  tej samej doby (dokladnie, gdy zaden rod nie ma zalogi z celem 0 po przycieciu); "cel granicznych wyzej o Z zl/d" > 0, gdy X > 0.
- "potkniecia" - brak w linii.
- Wylacznik: MCM "Border Garrisons Peace" = nie -> linia "wylaczone" i odcinek B4 jak przed zmiana.
