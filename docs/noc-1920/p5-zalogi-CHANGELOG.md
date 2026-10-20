# p5-zalogi - wieksze zalogi zamkow granicznych w pokoju (R3-08g, POMYSLY-35 nr 26), galaz w-toku/p5-zalogi na 5189c0fe (kandydat WGRANIA 92)

## Dla Jeffa (prosto)
W pokoju zamek AI trzyma dzis 70 zbrojnych na kazde 100 z czasu wojny - wszedzie tak samo. Teraz zamek albo miasto, ktore ma "za miedza"
zamek lub miasto innego krolestwa (nie sojusznika), jest ciete tylko do 75 na 100; zamki w glebi kraju dalej 70 i nigdy nie traca.
"Za miedza" znaczy: obca twierdza blizej niz ok. doba marszu armii (60 jednostek mapy; w Westeros najblizszy sasiad stoi zwykle 30-60 jednostek dalej).
W pokoju nikt nie werbuje do zalog, wiec regula trzyma ludzi, a nie dokupuje: z zamku granicznego mniej ludzi idzie do domu, a lordowie
chetniej zostawiaja w nim swoich. Placi pan zamku ze swojego pulapu - biedny rod, ktory juz jest na granicy 80% pulapu, nie dostaje
dodatku wcale. Twoich zalog to nie dotyczy; wylacznik: MCM "Border Garrisons Peace".

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
     1.3+) -> obie graniczne. Mapa trzyma id twierdz (napisy), nie obiekty kampanii; kampania przez WeakReference; `Reset()` przy kazdej kampanii.
   - `NoteTarget` (pomiar) - zalogi z celem po przycieciu, osobno graniczne i w glebi kraju: cel, zold dzis, na celu (zold >= 95% celu).
   - Linia logu **"Zalogi graniczne (R3-08g): dzien N | twierdze graniczne na mapie B z F (...) | rody AI w pokoju: zalog granicznych z celem
     X (udzial 0.75), w glebi kraju Y (udzial 0.70); cel granicznych wyzej o Z zl/d, po przycieciu Z2 zl/d (nadwyzka tylko z miejsca do 0.80
     pulapu rodu - glab kraju bez zmian) | na celu (zold >= 95% celu): graniczne A z X (zold W wobec celu T, P%), w glebi kraju A2 z Y (...)
     - w pokoju zalog nikt nie dobiera (BK), cel to mniej zwolnien | graniczne w pokoju: nazwa (rod), ... | wedlug frakcji (graniczne/wszystkie): ..."**
     (podzial wedlug frakcji w pierwszej linii sesji i co 28 dob). Pomiar - ciche logi (`LogGate.FullDay`): w grze co QuietLogEveryDays dob,
     w autotescie codziennie.
2. `ClanBudget.cs` (wpiecia): w `Garrisons` cel = `GarrisonPeaceShare x wr` (jak dotad) + nadwyzka graniczna `(PeaceShare - GarrisonPeaceShare) x wr`;
   cele 0.70 przycinane do `GarrisonMaxShareOfBudgetPeace x pulap` jak dotad (wspolczynnik k), a nadwyzka graniczna dostaje tylko miejsce,
   ktore zostaje do tego limitu (wspolczynnik kx) - glab kraju nigdy nie traci; `NoteTarget` przy kazdej zalodze z celem. W `Reset` -
   `BorderGarrisons.Reset()` (obok CourtFull.Reset). W odcinku B4 linii "Budzet rodow (166)" dopisek "(graniczne 0.75 - linia R3-08g)",
   gdy klucz wlaczony. W `Daily` po `Report(s, today)` -> `BorderGarrisons.Report(s, today)` (wlasny try).
3. `Settings.cs` (przy GarrisonPeaceShare, grupa MCM "The crown's income (stage 2)"): `BorderGarrisonsPeace` = true,
   `GarrisonPeaceShareBorder` = 0.75, `BorderGarrisonRadius` = 60. `tools/gen_mcm.py` -> McmSettings.cs (2067 ustawien Armoury; inne mody bez zmian).
   Nowe klucze - Armoury.json Jeffa ich nie ma, wiec dzialaja wartosci z kodu.

## Definicja granicy (decyzja projektu) i pomiar mapy
- Mapa ROT (`Modules/ROT-Map/ModuleData/settlements.xml`, 227 twierdz): odleglosc do najblizszej twierdzy - mediana 44 jedn., 1/4 ponizej 33,
  3/4 ponizej 60, najwiecej 119. Promien 60 = "sasiad za miedza". Przy wlascicielach startowych z XML (rod -> krolestwo z `spclans.xml`
  super_faction; Surusbec ma w XML wlasciciela-postac `lord_4_26` - przez jej rod Swyft to Zachod) graniczne: R 60 -> **71 z 227 (31%)**,
  R 75 -> 111 (49%), R 90 -> 153 (67%) (prawie polowa przy 75 to juz nie "granica").
- Przyklady przy R 60: Dorzecze 8 z 14 (Darry, Harroway <- Harrenhal; Maidenpool <- Duskendale; Seagard <- Greywater Watch),
  Zachod 12 z 22, Polnoc 2 z 22 (Greywater Watch, Oldcastle), Dolina 4 z 13, Smocza Skala 5 z 10, Krainy Burzy 7 z 13,
  Straz Nocy 3 z 5 (wobec Wolnych Ludzi; Straz i tak bez celu - b.Zero). Prawdziwe liczby (wlasciciele w grze) da linia logu po starcie.
- Odleglosc a czas (gra 1.4.8): krok mapy 1.2 h, predkosc bazowa 4 jedn. na krok (mniej dla duzych druzyn, noca -25%) - mala druzyna ok. 3.3
  jedn./h, armia mniej, wiec 60 jedn. to ok. 15-20 h, czyli ok. doby marszu (tak samo nazywa 60 opis CivilWorksFrontRange).
- Morze: linia prosta - wyspa albo brzeg za waska zatoka liczy sie jako graniczny (najazd z morza, NavalDLC); Morze Waskie jest szersze niz 60.
- Sojusznik nie jest granica (koniec sojuszu - od nastepnej doby graniczny). Rod niezalezny z twierdza = inna frakcja (graniczny).
- Zmiana wlasciciela: mapa od nowa co dobe - zdobyty zamek od razu robi z sasiadow twierdze graniczne (pokoj dotyczy calego rodu, wiec regula
  dziala tylko u rodow bez zadnej wojny z krolestwem - jak caly cel pokojowy B4).

## Ryzyko / co sprawdzic (kontrola calosci)
- Regresje: `GarrisonPeaceShare` czytaja tylko `ClanBudget.Garrisons` (cel) i `Report` (linia B4) - grep calego drzewa. `_garTarget` czyta
  `ApplyGarrisonLimits` (limit zoldu zalogi, tylko w dol) i `Releases` krok 1 (zwolnienia zalog ponad cel w pokoju) - oba dostaja wyzszy cel
  granicznych bez zmiany kodu. Klucz wylaczony = co do zlotowki jak 5189c0fe (nadwyzka 0, kx = 1, ten sam rachunek k; B4 bez dopisku).
- Co robi wyzszy cel w pokoju (recenzja): BK wylacza w pokoju autowerbunek zalog AI (`BannerKings.Patches.EconomyPatches`:
  `GarrisonAutoRecruitmentIsEnabled = enemyKingdoms.Any() && ...`), wiec zalogi nikt nie dobiera. Wyzszy cel dziala tylko tak:
  (a) mniej zwolnien - zaloga ponad cel jest cieta tylko do celu (Releases krok 1, u rodu ponad pulapem), a limit zoldu zalogi jest wyzszy;
  (b) wiekszy "idealny rozmiar", przy ktorym lord zostawia ludzi w zalodze (model gry: limit zoldu / sredni zold). Zaloga graniczna, ktora weszla
  w pokoj ponizej 0.75 (np. swiezo zdobyty zamek), sama nie podrosnie przed nastepna wojna - tak samo bylo przy 0.70. Typowo pokoj zaczyna sie
  z zaloga wojenna (pelny pulap gry), wiec glowny skutek to mniejsze ciecie granicznych. Linia logu pokazuje zold wobec celu osobno.
- Zamknieta ekonomia: nowego zlota nie ma. Platnik - rod pana (pulap pokoju 166), odbiorca - zolnierze (zold gry).
- Biedni panowie zamkow (audyt: 13 z 15 bankrutow): suma celow rodu dalej najwyzej `GarrisonMaxShareOfBudgetPeace` (0.80) pulapu. Recenzja:
  wczesniej wyzszy cel granicznych zmniejszal wspolczynnik k dla wszystkich twierdz rodu na limicie (glab kraju tracila). Teraz cele 0.70 sa
  przycinane jak przed zmiana, a nadwyzka graniczna dostaje tylko wolne miejsce do limitu - rod na limicie nie ma nadwyzki, glab kraju nie traci.
  Druzyny dostaja pulap minus zalogi (min(zold zalog, cel)) - u rodu z zaloga graniczna ponizej celu druzyny maja do ok. 7% celu tej zalogi mniej miejsca.
- Koszt (szacunek): pokojowe zalogi swiata ok. 58 tys./d (bieg dwuletni, d728); 31% granicznych x 0.05/0.70 -> ok. **+1.3 tys./d** w calkowitym
  pokoju (najwyzej; mniej, bo rody na limicie nadwyzki nie maja). Zamek z zaloga wojenna 300 zl/d: cel 210 -> 225 zl/d.
- Watek: wolane tylko z `ClanBudget.Daily` (watek glowny, raz na dobe); bez wolan z watkow AI, bez alokacji w goracych sciezkach
  (mapa ok. 26 tys. par raz na dobe, napisy do logu najwyzej 12 nazw).
- Potkniecia liczone (`potkniecia N` w linii, pierwszy blad miejsca do logu); wyjatek przy jednej twierdzy = dla niej udzial jak dotad, funkcja sie
  nie gasi. Liczniki, mapa i bledy zerowane przy kazdej kampanii (`BorderGarrisons.Reset` z `ClanBudget.Reset`).
- Zapis: brak nowego stanu (mapa liczona z mapy gry co dobe) - nic w SaveText, stare zapisy bez zmian.
- Straz Nocy to osobna frakcja: zamek Polnocy w promieniu 60 od zamku Strazy bylby graniczny (w XML zaden). Zostawione - sasiad innej frakcji.

## Status
DO SPRAWDZENIA - zbudowane (Armoury, kod 0, 0 Error(s), te same 5 ostrzezen co na bazie, zadne w nowych liniach), nie uruchamiane w grze (zakaz w zadaniu).

## Decyzje (projekt Claude, wedlug zasady Jeffa "ekonomie projektuje Claude")
- 0.75 z audytu; graniczna nigdy ponizej glebi kraju (max z obu), wiec podniesienie GarrisonPeaceShare nie odwroci reguly.
- Granica = obca twierdza (inna frakcja, nie sojusznik) w promieniu 60 jedn. w linii prostej; odrzucone: "N najblizszych twierdz" (nie widzi
  odleglosci - w Essos maly kraj z 5 twierdzami bylby caly graniczny przy kazdym promieniu) i droga ladem (wyspy nigdy graniczne, a NavalDLC plywa).
- Glab kraju bez zmian (0.70) i nigdy nie traci, takze u rodu na limicie 0.80 pulapu (recenzja) - nadwyzka graniczna tylko z wolnego miejsca.
- Nie wlaczam werbunku zalog w pokoju (BK go wylacza celowo) - to zmiana zachowania BK ponad zadanie; regula trzyma ludzi, nie dokupuje.

## Test w grze (log_grep)
- `grep "Zalogi graniczne (R3-08g)" Modules/Armoury/Armoury.log` - pierwsza linia sesji z podzialem "wedlug frakcji"; oczekiwane: twierdze
  graniczne ok. 1/4-1/2 wszystkich (przy 227 twierdzach ok. 60-110), Polnoc mala czesc, krolestwa w srodku mapy (Dorzecze, Zachod) duza.
- W dobie z rodami w pokoju: "zalog granicznych z celem X" + "w glebi kraju Y" = "zalog z celem" w odcinku B4 linii "Budzet rodow (166)"
  tej samej doby (dokladnie - liczone przy tych samych zalogach); "na celu" graniczne A + w glebi kraju A2 = "na celu" B4; zold i cel obu grup
  razem = "zold zalog ... wobec celu" B4. "cel granicznych wyzej o Z zl/d" > 0, gdy X > 0; "po przycieciu" <= Z (rowne, gdy zaden rod nie jest na limicie).
- Skutek: przy dlugim pokoju zold zalog granicznych wobec celu (P%) nie nizszy niz w glebi kraju; po wojnie graniczne cieta wolniej (mniej zwolnien).
- Odcinek B4 ma dopisek "(graniczne 0.75 - linia R3-08g)".
- "potkniecia" - brak w linii.
- Wylacznik: MCM "Border Garrisons Peace" = nie -> linia "wylaczone", odcinek B4 bez dopisku i jak przed zmiana.

## Recenzja (PELNA, 20.10) - co zrobione
Werdykt recenzji: mozna wgrac, 0 krytycznych, 0 waznych, 6 drobnych. Wszystkie 6 sprawdzone i prawdziwe; poprawione (commit fd376816):
1. Glab kraju tracila u rodu na limicie 0.80 (wspolny k) - prawda; poprawione w kodzie: k z celow 0.70 jak dotad, nadwyzka graniczna tylko z wolnego miejsca (kx).
2. "Werbunek zalog gry dobiera" - nieprawda w pokoju (sprawdzone dekompilacja BannerKings.Redux: autowerbunek tylko przy wrogach); poprawione opisy
   (Dla Jeffa, Ryzyko, komentarz, MCM) i dodany pomiar zoldu wobec celu osobno dla granicznych i glebi kraju.
3. Brak Reset - prawda; dodane `BorderGarrisons.Reset()` z `ClanBudget.Reset` (konstruktor ArmouryBehavior, bez ruszania wspolnej linii).
4. B4 z jednym udzialem - prawda; dopisek "(graniczne 0.75 - linia R3-08g)", gdy klucz wlaczony (wylaczony - B4 jak przed zmiana).
5. Liczby pomiaru - prawda (przeliczone: Surusbec przez pana do Zachodu): 71/227 = 31%, Zachod 12 z 22, R75 49%, R90 67%; poprawione w komentarzu i tu.
6. "pol doby marszu" - prawda, to ok. doba (15-20 h); poprawione w MCM, komentarzu i tu (spojnie z CivilWorksFrontRange).
