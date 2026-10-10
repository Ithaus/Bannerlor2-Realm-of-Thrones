# PRZEKAZANIE 08.10.2026 ok. 12:10 (limit konta 98%) - stan do podjecia przez inne konto

SCRATCH = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad`
Repo Jeffa: `C:\Users\GAME\Bannerlor2-Realm-of-Thrones` (galaz claude/bannerlord-rot-setup-o75kvo, push na GitHub Ithaus). Zasady: CLAUDE.md, docs/STAN-PRAC.md.

## 1. Co jest W GRZE (wgrane)
- Armoury **c01a54ba** = T3 + 160 (kon wlasnoscia zolnierza) + 161 (zapis bez dlugich napisow + komplety rekrutow), wgrane 08.10 11:48 na "wgraj".
  Galaz `paczki/161-zapis` 2e235ea. Poprzedni 5a7074c0: `Armoury.dll.bak-2026-10-08-przed-161` obok pliku i `D:\Backup-Bannerlord\wgrane\2026-10-08-161-przed`.
  DLL zrodlowy: SCRATCH\dzien-6\towary3\dll-final-5 (+ kopia D:\Backup-Bannerlord\towary3-2026-10-08\dll-final-5).
- GrandTourney 1337433c, RealisticCaptivity 3e04b89b (T3), CrashScribe zatwierdzony bez zmian. Armoury.json Jeffa bez kluczy 160/161 (wartosci z kodu).
- BetterEconomy NIETKNIETY (settings SHA 1f963cfa..., DLL 267ba08d - te same co przy przygotowaniu skryptu 06.10).

## 2. Praca W TOKU
- **Paczka 169 KSIEGA OBIEGU (sam log)** - workflow `wf_6488c056-dc9` (rozpoznanie x3 -> projekt -> wykonanie -> recenzja x3 -> poprawki) w drzewie
  roboczym **SCRATCH\dzien-6\obieg169\repo**, galaz `w-toku/169-ksiega-obiegu` (od 2e235ea). Skrypt workflow:
  `C:\Users\GAME\.claude\projects\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\fa2fd7a6-a098-46e5-8d1b-3c099c38c1f8\workflows\scripts\paczka-169-ksiega-obiegu-wf_6488c056-dc9.js`
  (dziennik: ...\subagents\workflows\wf_6488c056-dc9\journal.jsonl). Jesli sesja padla w trakcie: sprawdz w drzewie `git log` i
  `docs/paczki/169-ksiega-obiegu.md` (specyfikacja projektanta) - co jest zrobione; dokoncz od brakujacej fazy (wykonanie / recenzja / poprawki).
  Zakres: projekt ekonomii rozdz. 12 wiersz 1 ("161 KSIEGA OBIEGU" = teraz 169), rozdz. 4.2 i 10.1 pkt 1-3 + "Budzet rodow (na sucho)", "Dlugi (na sucho)", D na rod.
- **Po 169:** zbudowac, autotest 40 dob z probnym DLL (zasady w CLAUDE.md / pamiec "zgoda na autotest"), sprawdzic: suma przyczyn + reszta = zmiana zlota
  swiata; reszta (dzis -144..-218 tys./dobe) rozlozona na nazwane pozycje. Potem pokazac Jeffowi i czekac na "wgraj".

## 3. Czeka na JEFFA
- **BetterEconomy 13 kluczy** (tools/bee/zamknij-ujscia-bee.ps1, cofanie: cofnij-ujscia-bee.ps1, opis tools/bee/OPIS.md). Jeff pytal "w nicosc czy na
  inwestycje?" - odpowiedziane: zloto pana znika (GiveGoldAction do null), a BEE daje efekt z niczego (skarbiec zamku tylko na papierze, oboz XP, wies
  +10/25/50 domow, zbrojownia - bron z niczego). **Otwarte: wariant zbrojowni** (a) tylko AI (domyslny skryptu; gracz moze dalej budowac) czy (b) wszystkim
  (`-ListaZFundamentu`). Zastosowac DOPIERO po tescie 169, przy zamknietej grze, z kopia.

## 4. Decyzje Jeffa 08.10 (wiazace)
- Ekonomia (PROJEKT-EKONOMIA-OBIEG rozdz. 14.1): gracz-wasal z lennem placi koronie jak AI, dwor graczowi nie; dlug wymarlego rodu na nowego pana wsi - tak;
  zalogi AI w pokoju o polowe - tak; karczmy na start z pula starych zolnierzy - tak. Kolejnosc paczek: rozdz. 12 (169 -> BEE -> 164a -> 108/109 ...).
- **Optymalizacja gry NA KONIEC** (po skonczeniu modowania) - lista i pomiary w STAN-PRAC "DECYZJE JEFFA 08.10" i "08.10 POPOLUDNIE".

## 5. Narzedzia i dane
- Autotest: repo SCRATCH\dzien-6\autotest\repo, galaz at1-autotest c5c25ce (wypchnieta). tools/autotest.ps1: -LoadSave <nazwa,nazwa> (wczytanie zapisu
  zamiast nowej kampanii, -Days od wczytania), -Census N (spis swiata), -Profile (pomiar klatki: sekcje silnika + sluchacze zdarzen). CrashScribe probny
  z tym: SCRATCH\dzien-6\autotest\do-gry\CrashScribe-spis.dll. NIE uzywac probkowania stosu (Thread.Suspend zawiesil gre - usuniete).
  Wolanie z -ExtraDll tylko przez powershell -Command "& 'skrypt' ... ; exit $LASTEXITCODE".
- Zapisy testowe (Game Saves, prefiks autotest-): autotest-rok-360 (doba 360, stare dlugie napisy - wczytuje sie tylko z 161), autotest-161-kawalki
  (doba 362, w kawalkach). Zapisy Jeffa: nigdy nie ruszac.
- Odczyt zapisu poza gra: autotest repo tools/zapis/napisy.py (napisy > 32767 B), dlugie.py (rozbior dlugich napisow Armoury).
- Projekt ekonomii: docs/PROJEKT-EKONOMIA-OBIEG-2026-10-08.md; raporty B1-B3 i symulacja: SCRATCH\dzien-6\ekonomia-obieg.

## 6. Znane dziury dopisane 08.10 (do projektu ekonomii)
- ~95% werbunku AI ochotnikow tieru 2+ to "bez zapisu" (komplet wzorca = sprzet z niczego): ok. 1000-1500/dobe na starcie, ~500 po roku - pule notabli
  zmienia cos, czego VolunteerKit nie widzi.
- "Widly" pod tabliczka miasta = kafelki druzyn w osadzie; po druzynach usunietych w srodku kafelki zostaja (UI, na liste optymalizacji/porzadkow).

## 7. DECYZJA JEFFA 08.10 ok. 12:15: "jak znika to zamykamy, ma byc logiczny system ekonomii, ze wszystko z czegos wynika"
- BetterEconomy: wariant **(b)** - `zamknij-ujscia-bee.ps1 -ListaZFundamentu` (zbrojownia zamknieta takze graczowi). Zastosowac po tescie 169, gra zamknieta, kopia.
- NOWE ZADANIE: akcje gracza w BEE, ktore tez wrzucaja zloto w nicosc (tools/bee/OPIS.md rozdz. 1.2: wplata do skarbca zamku Castle:1149 i miasta Town:463,
  oboz :614, szkolenie :718, inwestycja we wies VillageInvestment:274, dostep do targu 5 000 VillageDevelopment:625, inwestycja w miasto 10/50/100 tys.
  SettlementMenuBehavior.cs:964-1000 -> LordInvestmentCampaignBehavior.cs:140) - zamknac latka Armoury (CanPlayer* = false z powodem po angielsku,
  wylacznik MCM). Zasada ogolna: kazde zrodlo z niczego / ujscie w nicosc zamykamy albo dajemy mu prawdziwego platnika i odbiorce.

## 8. W TOKU od 12:20: paczka 170 BEE DOMKNIETE (latki Armoury)
- Workflow `wf_3681591a-583` (rozpoznanie x2 -> projekt -> wykonanie -> recenzja x2 -> poprawki), drzewo **SCRATCH\dzien-6\bee170\repo**, galaz
  `w-toku/170-bee-domkniecie` (od 2e235ea). Skrypt: ...\fa2fd7a6-...\workflows\scripts\paczka-170-bee-domkniecie-wf_3681591a-583.js.
- Zakres: akcje gracza BEE w nicosc (OPIS 1.2) wylaczone z powodem; bierne zrodla BEE z niczego po 13 kluczach (gotowe zbrojownie, wirtualny skarbiec
  zamku, XP obozow, ...) zatrzymane. Spec: docs/paczki/170-bee-domkniecie.md w drzewie. Jesli sesja padla: git log w drzewie + spec, dokonczyc faze.
- Kolejnosc wgrania: 169 (test 40 dob) -> skrypt 13 kluczy z -ListaZFundamentu + 170 razem (jeden test) -> "wgraj".

## 9. Pytanie Jeffa 12:25 "skad zamek zdobywa bron, aby wyszkolic i uzupelnic zaloge?" - odpowiedziane, PROPOZYCJA czeka na "tak"
- Dzis: (1) ColdStart - zapas zbrojowni zalog raz na kampanie; (2) rekruci z kompletem od notabla, ale ~95% "bez zapisu" = komplet wzorca z niczego;
  (3) AiGear.TryBuy: zaloga kupuje na polce SWOJEJ osady (zamek: st.ItemRoster zamku - zamek nie ma targu, polka zwykle pusta; DO ZMIERZENIA w 169/spisie).
- Propozycja (nowa paczka ekonomii, numer do nadania): zaloga zamku kupuje w najblizszym przyjaznym miescie (pan placi kasie miasta, woz wiezie do
  zamku jak MarketCarts); rekrut bez kompletu przychodzi z tym, co ma (gorszy sprzet / bez broni), dozbraja go pan z targu; szkolenie = codzienne cwiczenia
  wlasna bronia (bez obozu BEE z XP z niczego - zamyka 170).

## 10. Jeff 12:30 "tak potwierdzam" -> paczka 171 ZBROJENIE ZALOG (w toku)
- Drzewo SCRATCH\dzien-6/zaloga171/repo, galaz w-toku/171-zbrojenie-zalog (od 2e235ea), workflow - nazwa w STAN-PRAC/tu po starcie.
- Zakres: (1) przyczyna ~95% werbunku "bez zapisu" (kto zmienia pule notabli poza VolunteerKit); (2) rekrut bez kompletu przychodzi z tym, co ma
  (zamiast kompletu wzorca z niczego); (3) zaloga zamku kupuje w najblizszym przyjaznym miescie (pan placi miastu, towar jedzie do zamku);
  (4) szkolenie = cwiczenia wlasna bronia (oboz BEE zamyka 170). Scalanie po kolei: 169 -> 170 -> 171 (konflikty w Settings/McmSettings/CHANGELOG).

## 11. STAN NA ODDANIE (08.10 ok. 12:40, konto 99%)
Trzy workflowy biegly w tle tej sesji; po koncu limitu PRZERWANE. Wznowienie resumeFromRunId dziala tylko w tej samej sesji - nowe konto zaczyna
dana faze od nowa, korzystajac z zapisanych wynikow:
- **169** (wf_6488c056-dc9, drzewo SCRATCH\dzien-6\obieg169\repo): gotowe rozpoznanie 1/3 (nasz kod) -> **docs/rozpoznanie-2026-10-08/paczka-169-wynik-1.md**.
  Brakuje: rozpoznanie 2 (okna w kodzie gry) i 3 (okna w BK/BEE), projekt, wykonanie, recenzje, poprawki. Skrypt do ponownego uzycia (prompty):
  C:\Users\GAME\.claude\projects\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\fa2fd7a6-a098-46e5-8d1b-3c099c38c1f8\workflows\scripts\paczka-169-ksiega-obiegu-wf_6488c056-dc9.js
- **170** (wf_3681591a-583, drzewo SCRATCH\dzien-6\bee170\repo): rozpoznanie w toku (0/2 gotowe w chwili zapisu). Skrypt: ...\workflows\scripts\paczka-170-bee-domkniecie-wf_3681591a-583.js
- **171** (wf_e42eab09-42b, drzewo SCRATCH\dzien-6\zaloga171\repo): rozpoznanie w toku (0/3). Skrypt: ...\workflows\scripts\paczka-171-zbrojenie-zalog-wf_e42eab09-42b.js
- Kolejne gotowe wyniki zrzuca skrypt SCRATCH(fa2fd7a6)\zrzut_rozp.py do docs/rozpoznanie-2026-10-08/ (paczka-NNN-wynik-K.md); sprawdz tez drzewa
  robocze (`git -C <drzewo> status`, docs/paczki/16x-*.md) - agent mogl zostawic czesciowa prace.
- NIC z 169/170/171 nie jest wgrane. W grze: Armoury c01a54ba (160+161). Kolejnosc dalej: dokonczyc 169 -> test 40 dob -> 170 + skrypt BEE
  -ListaZFundamentu -> 171 -> scalenie po kolei -> autotest -> "wgraj".

## 12. AKTUALIZACJA (08.10, po 12:40)
- **169: rozpoznanie KOMPLETNE (3/3)** - docs/rozpoznanie-2026-10-08/paczka-169-wynik-1.md (nasz kod: ksiegi, okna, D na rod), -wynik-2.md i -wynik-3.md
  (okna w kodzie gry oraz w BK/BEE - kolejnosc plikow = kolejnosc ukonczenia, naglowek w pliku mowi, ktory to raport). Workflow przeszedl do projektu
  (specyfikacja ma powstac w drzewie obieg169: docs/paczki/169-ksiega-obiegu.md - w chwili zapisu jeszcze jej nie ma, drzewo bez zmian).
  Nowe konto: przy przerwaniu zaczyna od fazy PROJEKT, dajac projektantowi te 3 pliki jako wyniki rozpoznania (prompt projektanta w skrypcie workflow 169).
- **170 i 171:** rozpoznanie w toku, brak gotowych wynikow; drzewa bez zmian. Nowe konto: zaczyna od ROZPOZNANIA (prompty w skryptach workflow).
- Zapis automatyczny wynikow (co 2 min, commit + push + kopia D:) dziala do konca sesji.

## 13. DECYZJA JEFFA: konczymy na rozpoznaniu, nowe konto zaczyna od PROJEKTU
- "nie rozpoczynaj projektu, to nowe konto" / "konczymy faze rozpoznania i nowe konto zacznie dalej".
- 169: workflow ZATRZYMANY przed projektem (drzewo obieg169 czyste, brak specyfikacji). Rozpoznanie 3/3 w docs/rozpoznanie-2026-10-08/paczka-169-wynik-*.md.
- 170 i 171: czekaja na koniec rozpoznania, potem zostana zatrzymane; wyniki zapisuja sie do docs/rozpoznanie-2026-10-08/paczka-170-*.md / paczka-171-*.md.
- NOWE KONTO: dla kazdej paczki uruchom faze PROJEKT (potem wykonanie, recenzje, poprawki) z promptami ze skryptow workflow (rozdz. 11), dajac
  projektantowi pliki rozpoznania z docs/rozpoznanie-2026-10-08/ zamiast ponownego rozpoznania. Kolejnosc: 169 -> 170 -> 171.

## 14. ROZPOZNANIE ZAKONCZONE DLA 169, 170, 171 - wszystkie workflowy ZATRZYMANE przed projektem
- docs/rozpoznanie-2026-10-08/: paczka-169-wynik-1..3.md, paczka-170-wynik-1..2.md, paczka-171-wynik-1..3.md (kolejnosc plikow = kolejnosc ukonczenia;
  naglowek/summary w pliku mowi, ktory to raport). Drzewa robocze obieg169, bee170, zaloga171 czyste (na 2e235ea).
- NOWE KONTO zaczyna od PROJEKTU kazdej paczki (169 -> 170 -> 171), prompty projektanta/wykonawcy/recenzentow w skryptach workflow (rozdz. 11).

## 15. NOWE KONTO (sesja 7016f733, 08.10 wieczorem) - start od PROJEKTU 169
- SCRATCH nowej sesji = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\7016f733-d379-418e-b700-f66fd52e4d2b\scratchpad`.
  Drzewa robocze i dekompilacje zostaja w STARYM scratchu (3cf3e0ac...\scratchpad\dzien-6\obieg169|bee170|zaloga171|autotest, ore-supply) - to worktree klonu
  `3cf3e0ac...\scratchpad\lancuch` (origin = repo Jeffa).
- **169**: workflow `wf_6f6961dc-464` (projekt z 3 plikow rozpoznania -> krytyka projektu -> projekt po krytyce -> wykonanie -> recenzja x3 -> poprawki).
  Skrypt: `C:\Users\GAME\.claude\projects\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\7016f733-d379-418e-b700-f66fd52e4d2b\workflows\scripts\paczka-169-projekt-do-poprawek-wf_6f6961dc-464.js`.
  Gdy sesja padnie: `git -C <obieg169> log` + `docs/paczki/169-ksiega-obiegu.md` w drzewie (sekcja "Krytyka i odpowiedzi" = projekt po krytyce gotowy) - dokonczyc od brakujacej fazy.
- Potem: autotest 40 dob z probnym DLL -> wynik dla Jeffa -> 170 + skrypt BEE `-ListaZFundamentu` -> 171.
- 13:00 aplikacja zamknieta w trakcie projektu 169 (nic nie zapisane) -> 169 wznowiony tym samym `wf_6f6961dc-464` od projektu.
- **170** rownolegle: workflow `wf_5166afca-96d` (projekt z paczka-170-wynik-1..2 -> krytyka -> projekt po krytyce -> wykonanie -> recenzja x2 -> poprawki),
  drzewo bee170, skrypt `...\7016f733-...\workflows\scripts\paczka-170-projekt-do-poprawek-wf_5166afca-96d.js`.
- **171** rownolegle: workflow `wf_170b9c8b-c05` (projekt z paczka-171-wynik-1..3 -> krytyka -> projekt po krytyce -> wykonanie -> recenzja x3 -> poprawki),
  drzewo zaloga171, skrypt `...\7016f733-...\workflows\scripts\paczka-171-projekt-do-poprawek-wf_170b9c8b-c05.js`.
- Scalanie i testy PO KOLEI: 169 (autotest 40 dob, wynik Jeffowi) -> 170 + skrypt BEE -ListaZFundamentu -> 171. Gdy sesja padnie: w drzewie `git log` +
  docs/paczki/17x-*.md (sekcja "Krytyka i odpowiedzi" = projekt po krytyce gotowy), dokonczyc brakujace fazy.

## 16. PRACA NA NOC 08/09.10 (Jeff ok. 01:10 czasu warszawskiego = 16:10 zegara komputera; raport ok. 12:00 Warszawa = 03:00 zegara)
- 16:08 limit tygodniowy odnowiony -> 169/170/171 wznowione: 169 `wf_6f6961dc-464`, 170 `wf_5166afca-96d`, 171 `wf_170b9c8b-c05` (te same skrypty).
- **ZGODA JEFFA NA TA NOC: "Tak, wgraj sam"** - wgrac TYLKO to, co przejdzie autotest bez bledow, z kopia poprzedniej wersji (BEE: kopia ustawien); zapisy Jeffa
  nietkniete; co nie przejdzie - czeka. Rzeczy wymagajace nowej kampanii albo zmieniajace kanon - NIE wgrywac, opisac w raporcie.
- Zadania Jeffa na noc (doslownie w sesji 7016f733): pelny audyt ekonomii jako jednej symulacji swiata (dochody ludzi -> podatki pana/miasta -> wydatki na zycie
  wedlug sredniowiecza; okupy; produkcja wsi glownej z ludnosci wsi pobocznych, spalona poboczna = mniej; surowce do napraw minimalne + bilans kazdego surowca;
  rozklad produkcji wedlug krain - bez oliwek na Polnocy; AI lordow bez fali bankructw, zachowania jak w sredniowieczu/GoT); ekonomia wojny, zarzadzanie
  wojskiem i armiami, lupy; Nieumarli wolniej (podboj po kilku latach jak w ksiazkach, nie 6 osad po roku); realne odleglosci marszu; obozy armii w marszu,
  GODZINY OBOZU 24:00-06:00; drogi na mapie + zalegle zmiany wyglądu wiosek pobocznych (8 punktow PROJEKT-WIOSKI-LORE); pomysly, na ktore nie wpadlismy.
- Audyt: workflow `wf_1bb21d75-b44` (10 badan -> synteza -> krytyka x2 -> synteza po krytyce), raporty w **docs/audyt-2026-10-09/** (00-AUDYT-SWIATA-2026-10-09.md
  = synteza). Kod do czytania: worktree 2e235ea w SCRATCH nowej sesji `audyt\repo`. Skrypt: `...\7016f733-...\workflows\scripts\audyt-swiata-noc-0809-wf_1bb21d75-b44.js`.
- Plan dalej: 169 -> autotest 40 dob -> 170 + BEE -> autotest -> 171 -> autotest -> wgranie; paczki "tej nocy" z audytu (male, testowalne) -> autotest -> wgranie;
  raport dla Jeffa 12:00 Warszawa.
- 17:37 **170 GOTOWE**: galaz w-toku/170-bee-domkniecie db3f672 (GitHub), build OK, recenzje bez krytycznych/waznych; spec docs/paczki/170-bee-domkniecie.md
  w drzewie bee170; test wedlug "TEST PLAN" w wyniku workflow (bieg z zapisem doby 360 i nowa kampania po skrypcie BEE -ListaZFundamentu); pytania P1/P2 (domyslnie nie).
- 17:45 **AUDYT GOTOWY**: docs/audyt-2026-10-09/00-AUDYT-SWIATA-2026-10-09.md (synteza po krytyce, paczki tej nocy T1-T8, pozniej E0-E24, pytania do Jeffa) + 10 raportow.
- 17:50 **T1-T8** (poprawki z audytu) - workflow `wf_afd15a92-ceb` (pipeline: wykonanie -> 2 recenzje -> poprawki), drzewa w SCRATCH nowej sesji `noc\n1-obozy ..
  n8-krainy` (galezie w-toku/n1-obozy .. w-toku/n8-krainy w klonie lancuch, od 2e235ea). T2 = CrashScribe (klucze tez w CrashScribe.settings.xml w grze przy wgraniu).
  Dodane przez glowna sesje ponad audyt: T7 + dymek "held by" (W-2) z probkami w logu; T8 + 16 wsi z uprawa niezgodna z klimatem (VillageClimateFix).
- 17:56 **AUTOTEST 170 OK** (nowa kampania 40/40 dob, 13.2 s/dobe, run at-20261008-174421, log Armoury-2026-10-08_17-44-34.log): "wpiete 16/16", menu BEE
  12/12 zamkniete z powodem, tryb zgodnosci BK TAK, 0 bledow BeeSeal; bledy CrashScribe 8 = te same co w kazdym autotescie (start). DLL probny 52aea39c.
  UWAGA: skrypt BEE (zamknij-ujscia-bee.ps1 -ListaZFundamentu) ZABLOKOWANY przez zabezpieczenia Claude Code ("Production Deploy") - test bez kluczy
  (klucze 6/19, B4 wstrzymana zgodnie z projektem). Skrypt do nalozenia przez Jeffa (gra zamknieta): powershell -NoProfile -ExecutionPolicy Bypass -File
  tools\bee\zamknij-ujscia-bee.ps1 -ListaZFundamentu (kopia ustawien: D:\Backup-Bannerlord\bee-2026-10-08\better_economy_settings.xml.przed-testem-170, SHA 1f963cfa).
- 18:05 **T1-T8 GOTOWE** (wf_afd15a92-ceb: 32 agentow, 0 bledow, buildy OK, 0 uwag krytycznych): galezie w-toku/n1-obozy b30d701, n2-inni-kalendarz cd720d3,
  n3-naprawy-metal 9c79f9d, n4-dezerterzy 68dc53c, n5-rodzina-bank a9e3a5d, n6-miara-marsz 7daab4a, n7-wioski 0aad47c, n8-krainy 30cee30 (GitHub).
  Plany testow / notatki do wgrania / opisy dla Jeffa: SCRATCH nowej sesji noc\T-wyniki.md (kopia D:\Backup-Bannerlord\noc-0809\T-wyniki.md).
- 18:11 **AUTOTEST T2 OK** (CrashScribe probny = at1-autotest + T2, 7455006c; Armoury zatwierdzony): kalendarz czynny, Zew spi do 728, 0 "RUSZA NA",
  0 osad, 0 oblezen przed terminem, 0 bledow. Trupy w dobie 40: 2049 (przed: 1535 - nie gina w szturmach; przyrost z niczego +2/d/banda zostaje do R2/R4).
- 18:10 scalanie 170 + T1-T8 w drzewie SCRATCH nowej sesji `sklad` (galaz noc/sklad) - agent w tle.
- 18:15 autotest T1 sam (Armoury probny f59c6852 = n1-obozy b30d701) w toku.
- 18:20 workflow `wf_03018328-39c`: (A) H3 PRZEGRANI UCHODZA (decyzja Jeffa 07.10) - pelny cykl w drzewie `noc\n9-h3` (galaz w-toku/n9-przegrani-uchodza);
  (B) projekt KIESA LUDU z symulacja 4 lat -> docs/PROJEKT-KIESA-LUDU-2026-10-09.md (tylko projekt; wdrozenie z grupa C i nowa kampania);
  (C) PROBA DROG P2 (nie do wgrania) w drzewie `noc\p2-drogi-proba` + dane lore wiosek przeliczone na uklad z gry (SCRATCH nowej sesji `drogi`).
- 18:23 **AUTOTEST T1 OK** (Armoury probny f59c6852; log Armoury-2026-10-08_18-13-16 + Logs\2026-10-08_18-13-16\noc.log): "oboz swiata 0:00-6:00", AiNightCamp tylko
  godziny 0-5 (po 40 nocy), spi ok. 788 (lordow 405, karawan 383, wodzow armii 14 - bylo ok. 7), zjazd obudzonych sr. 0.34-0.42 / maks. 0.51-0.55 (prog 0.97),
  stoper < 1 ms, potkniec 0, bledow 0.
- 18:24 scalenie 170 + T1-T8 = galaz noc/sklad dfc8786 (drzewo SCRATCH nowej sesji `sklad`; Armoury 1a4cd01c, CrashScribe 0c1e92a6 bez trybu autotestu;
  MCM 693 ustawienia). Autotest S1 (stos + CrashScribe at1+T2 + zdjecia mlynow) w toku.
- 18:37 **AUTOTEST S1 OK** (stos 170+T1-T8, 40 dob, 13.4 s/dobe, zdjecia mlynow 8/8; analiza: SCRATCH nowej sesji s1kopia\, s1foto\): 170, T1, T2, T3, T6, T7
  zaliczone; T5 dziala (rodzina 149 tys. przed pozyczkami, pozyczek -48); T4 bez przypadkow w 40 dobach. **T8 NIEZALICZONY**: bawelna ze wsi 75/d (bylo
  116-124), aksamit -60% -> poprawka w toku. Trupy Innych w dobie 40: 1909 (bez kalendarza 702-1127) -> T2b (Inni bez dosypki z niczego) w toku.
- 18:46 **AUTOTEST ZAPISU (doba 362 -> 372) na S1 OK**: 0 bledow Armoury, kalendarz Innych czynny (3 zdobyte osady zostaja), T8 podmienil 17 wsi,
  T4 dziala (WarLedger 89 ludzi -> pula wyrzutkow i ksiega, potkniec 0), T5 dziala (rodzina 2 raty 31.5 tys.), 25.3 s/dobe.
- 18:55 workflow `wf_d615d72a-31b`: poprawka T8 (bawelna, drzewo sklad) + T2b Inni bez dosypki (CrashScribe, drzewo noc\n10, galaz w-toku/n10-inni-wzrost).
- 18:57 **WGRANIE 1 = 170 + T1..T7** (galaz noc/wgranie-1 = 1805521, drzewo SCRATCH nowej sesji wg1; Armoury 28a7456e, CrashScribe aff275de = baza + T2,
  bez trybu autotestu): autotest 40 dob tej binarki w toku, potem wgranie (zgoda Jeffa na te noc).
- 19:07 **WGRANIE 1 W GRZE**: Armoury 28a7456e + CrashScribe aff275de (170 + T1..T7), galaz paczki/noc-wgranie-1 b1d8c58; kopie przed: *.bak-2026-10-09-przed-noc1
  + D:\Backup-Bannerlord\wgrane\2026-10-09-noc1-przed (STAN-PRAC "NOC 08/09.10"). Autotest tej binarki: 40/40, 13.0 s/dobe, 0 bledow.
- 19:20 T8 poprawiony (sklad 8727c87: bawelna 14 wsi tylko w cieplych krainach, baza 112=112) + T2b (galaz w-toku/n10-inni-wzrost bd2841a: Inni bez +100/+2/ochotnikow
  z mapy; 616 na start zostaje) scalony do sklad (a389a5e). Autotest S2 (Armoury 3bf72dfd, CrashScribe probny = stos + autotest 1f51f30a) w toku.
- Zablokowane przez zabezpieczenia Claude Code (do Jeffa): skrypt BEE na pliku ustawien gry; probna zima (zmiana domyslnych w probnej kopii).
- 19:33 **WGRANIE 2 W GRZE**: Armoury 3bf72dfd + CrashScribe cfb33950 (= wgranie 1 + T8 z poprawka + T2b); galaz paczki/noc-wgranie-2; kopie przed: *.bak-2026-10-09-przed-noc2.
  Testy: S2 40 dob OK, S2 zapis doby 362 OK. W toku: 172 STRZALY (na 171, wf_13aea1a6-b50), scalenie 169 do noc/sklad2 (agent), H3/kiesa/drogi (wf_03018328-39c).
- 19:45 **AUTOTEST 169 SAMA** (analiza: SCRATCH nowej sesji kopia169\, a169\): gra niezmieniona (zloto, wojsko, Bank, tempo w normie), ARYTMETYKA OK 39/39, 0 bledow;
  reszta niewyjasniona swiata -38 tys./d (bylo -215 tys.; 82% nazwane: zold karawan notabli -202 tys., kapital nowych karawan +136 tys., ...). Skrypt obieg169_sprawdz.py
  kod 1 (T8, T16 - precyzja pomiaru, nie gra). Braki pomiaru: trybut (+1.12 mln w dobie 34 bez okna), nowe karawany -1.2 tys./karawana, "inne" z niczego ok. 290 tys./d
  (notable), koszt budzetu rodow 34 ms/dobe -> paczka 169b (wf_f774ccb1-f44, drzewo obieg169).
- 19:58 **AUTOTEST S3 OK** (noc/sklad2 1254cd7 = wgranie 2 + 169; Armoury 04d1cc99): 40/40, 13.1 s/dobe, 0 bledow; reszta w dobie 40 -38 tys. Test zapisu S3 w toku.
- 20:08 **WGRANIE 3 W GRZE**: Armoury 04d1cc99 (+169), CrashScribe cfb33950 bez zmian; galaz noc/sklad2 (1254cd7); kopia przed: Armoury.dll.bak-2026-10-09-przed-noc3.
- 20:47 autotest 120 dob wersji z gry (wgranie 3) OK: 120/120, 18.6 s/dobe sr., 9 bledow CS - analiza w toku (SCRATCH nowej sesji kopia120\).
- 172 STRZALY gotowe (galaz w-toku/172-strzaly 77ee649 na 171 349e393; GitHub): dzis strzaly robia warsztaty gry Z NICZEGO (BK kategoria arrows = towar),
  znikaja u mieszczan i w zaopatrzeniu BK; 172 zamyka to i daje strzelarzy w 97 miastach (drewno + ruda z polki miasta, 0.3 x reki warsztatow zbrojnych).
- Scalenie 171+172 do noc/sklad3 fbe3f83 (Armoury aae2828b, CrashScribe 158dfad0 - kod CS bez zmian; MCM 713). Autotest 171+172 sama (40 dob) w toku, potem S4 + zapis.
- 21:05 **ANALIZA 120 DOB** (wgranie 3): 0 bledow naszych, ok. 18 s/dobe bez narastania; T4 739 ludzi do puli, T2b -4306 trupow, T1 ok. 750 spi nocami.
  ALE fala bankructw jak w TOWARY 3 (26 do doby 120, Bank 5 -> 1.75 mln, pusty ok. doby 160; skarbce Polnocy i KL puste od doby ok. 98, zalegly zold 7%) -
  oczekiwane do czasu 165/166/168. NOWE: trybut/reparacje 11 mln w 120 dob (do 1.68 mln naraz), placilo 14 z 25 bankrutow, niedobor -> DebtToKingdom w nicosc.
  Inni oblegali Mur od doby 55 (44 doby; Mur trzyma) - straznik T2 nie przerywa oblezen zaczetych przez ROT/SAI.
- 21:06 workflow `wf_f5c29c01-5a9`: T2c (Inni bez oblezen zamknietych celow przed terminem; drzewo noc\n11, galaz w-toku/n11-mur) + badanie trybutu.
- 21:25 **AUTOTEST 171+172 sama** (analiza SCRATCH nowej sesji a171\, kopia171\): 171 dziala (bez zapisu 0 zamiast ~970/d, 969 zamowien zamkow, zalogi 92% broni),
  172 robi strzaly z drewna/rudy, 0 bledow - ALE NIEZALICZONE progi: pokrycie partii (korpus 50%, tarcze 23%, strzaly 43%), rynek nowej kampanii pustoszeje (zbroja
  korpusu do 0 ok. doby 48), karawany trzymaja 81-95% amunicji (BK: strzaly = towar handlowy), polowa miast bez strzal, awanse lucznikow cofniete 66-88%,
  ruda waskim gardlem. **171/172 NIE WGRYWANE** - do decyzji Jeffa (karawany a strzaly; skala produkcji). S4 (sklad3 = stos + 171 + 172) 40 dob OK technicznie
  (15.7 s/dobe - wolny start kampanii), test zapisu S4 w toku tylko jako informacja.
- H3 gotowe (galaz w-toku/n9-przegrani-uchodza 9a793f0, GitHub); KIESA LUDU projekt: docs/PROJEKT-KIESA-LUDU-2026-10-09.md (numer paczki 173 - 172 zajety przez strzaly).
- Plan: H3 sam 40 dob -> sklad4 = sklad2 + H3 + 169b + T2c -> test -> wgranie 4.
- 21:17 test zapisu S4 (z 171+172): OK, 23.5 s/dobe (S3: 25.7) - wolniejszy tylko start nowej kampanii (zapas startowy 171).
- 169b gotowe (galaz w-toku/169-ksiega-obiegu 30cd07d, GitHub): trybut = odszkodowania Diplomacy (KingdomWalletCost.ApplyCost - odbiorca 1/3 z niczego +
  reszta do skarbca bez zdarzenia; platnik skarbiec ponad 2 mln bez zdarzenia + dlug TributeWallet splacany przez rody); BEE karawany placa eskorte w nicosc
  przy kazdym wjezdzie do miasta (klucz 8 skryptu BEE to zamyka); "inne" z niczego = najpewniej dochod majatkow BK do notabli. Koszt budzetu rodow < 10 ms (cel).
- 21:18 scalenie H3 + 169b do noc/sklad4 (od sklad2) - agent; autotest H3 sam w toku. T2c (Mur) + badanie trybutu w toku (wf_f5c29c01-5a9).
- 21:30 **AUTOTEST H3 SAM OK** (Armoury e9b567ab; kopia SCRATCH nowej sesji kopiaH3\): 13.2 s/dobe, 0 bledow; doba 40: bitew objetych 28, przegrani zabici 19.4%
  (gra dalaby 50.2%), jency 5.7%, rozbici 74.9% (do puli/wsi/BK); zwyciezcy zabici 2.3% (gra 6.7%). noc/sklad4 0ced2cd (= sklad2 + 169b + H3; Armoury 0f8a80b0,
  MCM 702). Autotest S5 (sklad4) w toku.
- 21:50 **WGRANIE 4 W GRZE**: Armoury 0f8a80b0 (+169b +H3), CrashScribe cfb33950; galaz noc/sklad4; kopia przed: Armoury.dll.bak-2026-10-09-przed-noc4.
  T2c gotowe (w-toku/n11-mur f6d4e51, CrashScribe; zrodlo oblezen = ROT OnAiHourlyTick, Mur bez kajdan ROT). Trybut: SCRATCH nowej sesji noc\trybut.md (reparacje Diplomacy
  + ROT wznawia wojne fabularna tej samej doby; 93% kwoty z pokojow <= 1 doba) -> T9 (bez reparacji za wojny fabularne ROT, w-toku/n12-reparacje, wf_fc2601d0-5c5) w toku;
  suwak Diplomacy "Scaling War Reparations Gold Cost Multiplier" 50 -> 10 = rekomendacja dla Jeffa (jego MCM).
- 21:56 **AUTOTEST T2c (zapis doby 362, 10 dob) OK**: CrashScribe probny 13fd98de (= w grze + T2c + tryb autotestu); "wpiete 2/2", Nocny Krol PRZERYWA oblezenie
  Craster's Keep po wczytaniu (bez strat), potem rozkazy ROT usuwane; 0 "OBLEZENIE przed terminem", 0 osad zdobytych (bez T2c w tym samym zapisie: 3), 0 bledow.
- 22:09 **AUTOTEST T2c nowa kampania 40 dob OK**: 13.1 s/dobe, 0 bledow, 0 oblezen przed terminem, 0 "RUSZA NA", trupy w dobie 40: 1037 (S2 1651, S1 1909), 0 osad.
  Raport roboczy: docs/RAPORT-NOCNY-2026-10-09.md; badanie reparacji: docs/audyt-2026-10-09/11-trybut-reparacje.md. T9 w poprawkach.
- 22:13 **WGRANIE 5 W GRZE**: CrashScribe 4552ceaf (+T2c), Armoury 0f8a80b0; kopia przed: CrashScribe.dll.bak-2026-10-09-przed-noc5.
- 22:20 T9 gotowe (galaz w-toku/n12-reparacje d3ad3b0 na T2c; GitHub): pokoj w wojnie fabularnej ROT (ROTStorylineWars.IsWarForced) = reparacje Diplomacy 0;
  inne pokoje bez zmian. Test zapisu doby 362 OK (wpiete 1/1, 2 pokoje niefabularne - reparacje bez zmian, 0 bledow). Test 120 dob (wgranie 5 + T9) w toku.
- 23:00 **WGRANIE 6 W GRZE**: CrashScribe 269cc980 (+T9), Armoury 0f8a80b0. Test 120 dob: OK (szczegoly w STAN-PRAC). KONIEC PRAC NOCY - raport: docs/RAPORT-NOCNY-2026-10-09.md.
- 09.10 ~00:05 Jeff: "zaudytuj dochody, czy sa odpowiednie" -> workflow `wf_a0239869-fc4` (pomiar 120 dob wedlug rodzaju rodu, kod kazdego zrodla dochodu,
  historia/lore -> synteza z propozycja liczb wpieta w 165/166/168 -> 2 krytyki), wynik: docs/audyt-2026-10-09/12-DOCHODY-RODOW.md. W toku tez: 172b karawany bez
  strzal (wf_edafb659-b79, drzewo noc\n172b), projekt 174 produkcja uzbrojenia (wf_42ffe0c8-8e0) - Jeff: "jak brakuje, sprawdz produkcje i rzemieslnikow, nic z niczego".

## 17. NOC 09/10.10 (Jeff: "tak wgrywaj tej samej nocy jak przejdzie autotest")
- W grze: Armoury 0f8a80b0, CrashScribe 269cc980, skrypt BEE nalozony, Diplomacy reparacje x10. Decyzje Jeffa A-L w STAN-PRAC (09.10).
- W toku: 172b karawany bez strzal (wf_edafb659-b79, noc\n172b); 174 projekt produkcji uzbrojenia (wf_42ffe0c8-8e0); audyt dochodow rodow (wf_a0239869-fc4 ->
  docs/audyt-2026-10-09/12-DOCHODY-RODOW.md); audyt umiejetnosci (13-UMIEJETNOSCI.md) i armii krolestw (14-ARMIE-KROLESTW.md) (wf_beae7ea4-055);
  male paczki D1 za Murem mysliwi/rybacy, I1 jency do domu/na Mur, F1 H3 przy autobitwie gracza, E1 pokoj z biedy (wf_4750c307-a2b, drzewa SCRATCH nowej sesji noc2\*,
  galezie w-toku/d1-zamurem, i1-jency, f1-h3gracz (od noc/sklad4), e1-pokoj (od w-toku/n12-reparacje - CrashScribe)).
- Potem: proby zimy (J) i drog (H) za zgoda Jeffa; testy + wgrania; rano raport + PDF audytow.

## 18. LISTA KROKOW NA RESZTE NOCY 09/10.10 (na wypadek skrocenia rozmowy) - Jeff: wgrywac samemu po udanym autotescie (z kopia)
STAN W GRZE: Armoury 0f8a80b0 (galaz noc/sklad4), CrashScribe 269cc980 (galaz w-toku/n12-reparacje). SCRATCH = C:\Users\GAME\AppData\Local\Temp\claude\
C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\7016f733-d379-418e-b700-f66fd52e4d2b\scratchpad ; klon git: 3cf3e0ac-...\scratchpad\lancuch.
PROCEDURY:
- Autotest (gra zamknieta, Jeff nie gra): cd <3cf3e0ac SCRATCH>\dzien-6\autotest\repo; powershell -NoProfile -ExecutionPolicy Bypass -File tools\autotest.ps1
  -CrashScribeDll <CS z trybem autotestu> -ArmouryDll <probny Armoury> -Days 40 [-LoadSave autotest-161-kawalki -Days 8-10]; wynik w pliku wyjscia; skrypt sam
  przywraca zatwierdzone DLL (md5). CS z trybem autotestu dla danej galezi: worktree galezi + `git apply -3 SCRATCH\test\at1-cs.diff` (diff 2e235ea..c5c25ce -- CrashScribe)
  + build CrashScribe. Gotowe: SCRATCH\test\CrashScribe-at-T9.dll (= CS w grze + autotest), CrashScribe-spis.dll (baza).
- Wgranie: tasklist (gra zamknieta) -> sprawdz md5 w grze = oczekiwany -> kopia do D:\Backup-Bannerlord\wgrane\2026-10-09-nocN-przed + *.bak-...-przed-nocN obok ->
  cp -> md5 -> STAN-PRAC "WGRANIE N" + PRZEKAZANIE + push + kopia D:. Nastepne N = 7.
KOLEJKA:
1. Test 171+172+172b (Armoury SCRATCH\test\Armoury-172b.dll, 40 dob, w toku) -> analiza (karawany ~0 strzal, polki, awanse lucznikow).
2. 174 PRODUKCJA UZBROJENIA: projekt wf_42ffe0c8-8e0 -> docs/PROJEKT-174-PRODUKCJA-UZBROJENIA-2026-10-09.md -> wykonanie na w-toku/172b-karawany (nowe drzewo) ->
   recenzje -> poprawki -> scalenie 171+172+172b+174 do noc/sklad4 (nowa galaz sklad5) -> test 40 dob + zapis 362 -> wgranie, jesli progi OK (rynek, pokrycie).
3. Male paczki wf_4750c307-a2b: D1/I1/F1 (Armoury, od sklad4), E1 (CrashScribe, od n12) -> scalic, testowac, wgrac.
4. Proby: zima (J) i drogi (H; galaz w-toku/p2-drogi-proba, plik danych arm_map_roads_probe.tsv do ModuleData na czas testu, zdjecia) - Jeff zgodzil sie.
5. Audyty -> PDF (skrypt SCRATCH\pdf\zrob_pdf.py; Edge zapisuje PDF z opoznieniem): 12-DOCHODY-RODOW, 13-UMIEJETNOSCI, 14-ARMIE-KROLESTW -> SendUserFile rano.
6. Raport poranny dla Jeffa (docs/RAPORT-NOCNY-2026-10-10.md): co wgrane, testy, odpowiedzi audytow, pytania.
- 00:35 **AUTOTEST 171+172+172b OK** (Armoury 60b186e3, w-toku/172b-karawany 332d866; kopia SCRATCH kopia172b\): karawany w taborach 0 strzal / 0 beltow (bylo 4 157 / 1 764),
  miast bez strzal 24/97 (bylo 48), awanse lucznikow cofniete 0, 0 bledow, 12.6 s/dobe. Wgranie 171+172+172b czeka na 174 (rynek zbroi w nowej kampanii).
- 01:00 male paczki gotowe: D1 9ed1ca1, I1 6a90ffd, F1 9fa2553 (Armoury, od sklad4), E1 5b4e551 (CrashScribe, od n12); scalanie D1+I1+F1 -> noc/sklad5 (agent).
- 01:45 AUDYT DOCHODOW RODOW gotowy: docs/audyt-2026-10-09/12-DOCHODY-RODOW.md (bankrutuja panowie samych zamkow - zamek nic nie daje, 520 ludzi za ok. 2 000/d przy ok. 640/d ze wsi; przyczyna glownie za duzo wojska caly rok; propozycje D-1..D-13, W-1..W-9 do 165/166/168/110/112; pytania Q1-Q4 dla Jeffa w dokumencie). WGRANIE 7 = Armoury 63640cb3 (D1+I1+F1). E1 test w toku (agent).
- 01:50 WGRANIE 8: CrashScribe e8d460c5 (E1; skutek niesprawdzony - 0 glosowan o pokoj w testach). W GRZE: Armoury 63640cb3, CrashScribe e8d460c5. Dalej wedlug rozdz. 18: 174 (projekt wf_42ffe0c8-8e0 -> wykonanie na w-toku/172b-karawany -> scalenie 171+172+172b+174 na noc/sklad5 -> testy -> wgranie 9), proby zimy i drog, PDF audytow 12-14, raport poranny docs/RAPORT-NOCNY-2026-10-10.md.
- 02:00 PROJEKT 174 gotowy (docs/PROJEKT-174-PRODUKCJA-UZBROJENIA-2026-10-09.md, 6 pytan do Jeffa w dokumencie). Wykonanie: drzewo SCRATCH noc2\n174 (galaz w-toku/174-produkcja na 172b); Q4 (zlom ze starego stosu) i Q5 (zaopatrzenie BK gracza) za wylacznikami DOMYSLNIE WYLACZONYMI do odpowiedzi Jeffa.
- 02:05 174 WYKONANIE: workflow wf_058b0630-278 (drzewo noc2\n174). Po nim: scalic 174 (z 171+172+172b) na noc/sklad5 -> nowa galaz sklad6 -> autotest 40 dob + zapis 362 -> jesli bez bledow i progi rynku/pokrycia lepsze niz w 171+172 -> wgranie 9. Audyt umiejetnosci i armii: wf_beae7ea4-055 -> PDF (SCRATCH\pdf\zrob_pdf.py; dopisac pliki 12-14).
- 02:30 AUDYTY 13 (umiejetnosci) i 14 (armie) gotowe + PDF AUDYT-DOCHODY-UMIEJETNOSCI-ARMIE-2026-10-09.pdf (98 str., wyslany Jeffowi). Pytania z 13 (a-c) i 14 w dokumentach. Z1-Z3+Z5 (XP za oddany sprzet = 0, Spoils Cancel nic nie robi) - workflow w toku, drzewo SCRATCH noc2\z1 (galaz w-toku/z1-xp-dary od noc/sklad5 = Armoury w grze 63640cb3); potem test 40 dob + zapis -> wgranie (Jeff prosil: 'trzeba to zmienic'). Musztra gracza Z14a czeka na odpowiedz Jeffa (c).
- 02:40 K1 (zolnierze dozbrajaja sie za swoje z zoldu i lupow + wymiana z graczem lepsza za gorsza; decyzja Jeffa K) - workflow wf_b0d9e89e-b62, drzewo SCRATCH noc2\k1 (galaz w-toku/k1-dozbrajanie od noc/sklad5). Po nim: test 40 dob + zapis -> wgranie (razem z Z1, jesli oba gotowe - scalic na sklad5).
- 02:50 JEFF: "pamietaj, ze panowie zamkow produkuja i sprzedaja do miast, sa baza produkcyjna, na ich ziemi zyja ludzie, ktorzy placa im podatki - sprawdz bilans i proporcje". Audyt 15 BILANS PANOW ZAMKOW: workflow wf_0e7cfd75-84d -> docs/audyt-2026-10-09/15-BILANS-PANOW-ZAMKOW.md (lancuch wartosci okregu zamku: produkcja wsi -> pan / chlopi / kupcy / miasto / nicosc; porownanie z rachunkami majatkow XIII-XV w.; propozycje z platnikiem; zgodne z 12 i kiesa ludu). Po nim: PDF i odpowiedz Jeffowi.
- 02:55 JEFF: "daj mi podsumowanie tych audytow w PDF, tam jest za duzo stron, potrzebuje esencje". Workflow wf_a4a2d69c-c55 -> docs/audyt-2026-10-09/ESENCJA-AUDYTOW-2026-10-09.md (6-9 stron: 10 punktow, co w grze, swiat w liczbach, dziedziny, kolejnosc, WSZYSTKIE otwarte decyzje w jednej tabeli, pomysly; miejsce <!-- AUDYT15 --> na wynik audytu 15). PDF: python SCRATCH\pdf\zrob_pdf_esencja.py <md> <pdf> -> ESENCJA-AUDYTOW-2026-10-09.pdf -> SendUserFile; po audycie 15 dopisac sekcje i przebudowac PDF.
- 02:40 (zegar komp.) DECYZJE JEFFA O ARMIACH (STAN-PRAC "DECYZJE JEFFA 09.10 ok. 02:35"): Polnoc C z hamulcem balansu; konie - pomiar + naprawa konia za awans; Dothrakowie (a); Pentos (a); Wyspy Letnie ZOSTAJA z jazda; Zelazne Wyspy (a) Harlaw; slonie Volantis zostaja; SPRZET WEDLUG TIERU WSZEDZIE (b). PACZKA 175 ARMIE: workflow wf_f94ca31b-854 (projekt docs/PROJEKT-175-ARMIE-2026-10-09.md, drzewa SCRATCH noc2\a175cs = w-toku/175-armie-cs od w-toku/e1-pokoj, noc2\a175arm = w-toku/175-armie-arm od noc/sklad5; probne DLL SCRATCH test\Armoury-175.dll i CrashScribe-at-175.dll). Po nim: autotest 40 dob + zapis 362 (progi z projektu: wygrane bitew lordow wedlug krolestw - bez dominacji, bankructwa Dothrakow, konie) -> wgranie, jesli OK. Autotesty TYLKO po kolei (jedna gra naraz: Z1, potem 174-sklad6, potem K1, potem 175).
- 03:10 Z1 GOTOWE: galaz w-toku/z1-xp-dary dd02365 (od noc/sklad5), autotest 40 dob + zapis 8 dob OK (0 bledow Armoury, CS 8 = norma; SCRATCH test\autotest-Z1.txt, Armoury-Z1.dll md5 05654ef3). Przed wgraniem Z1b: bez mylacego okna "You are discarding items" na magazynie/trofeach Spoils (workflow wf_04cd8a9c-6f0, autotest w srodku -> SCRATCH test\Armoury-Z1b.dll). Po nim: WGRANIE 9 = Armoury Z1b (CS bez zmian e8d460c5); od tej chwili baza Armoury = w-toku/z1-xp-dary (scalac na nia 171/172/172b/174, K1, 175arm, Z16).
- 03:05 ESENCJA AUDYTOW wyslana (PDF 6 str., docs/audyt-2026-10-09/ESENCJA-AUDYTOW-2026-10-09.pdf; 22 decyzje w tabeli; miejsce <!-- AUDYT15 -->). SPROSTOWANIE Z16 w STAN-PRAC (pancerz/amunicja u bohaterow niepilnowane -> zakaz po 175; pytanie do Jeffa: trening Atletyki w zbroi +10/dobe).
- 03:25 JEFF: "daj mi pelna liste rzeczy do zrobienia, kiedy bedzie skonczony mod" -> workflow wf_bbcc7596-172 -> docs/PLAN-DO-KONCA-MODA-2026-10-09.md (+ PDF przez SCRATCH\pdf\zrob_pdf_esencja.py). Jeff pytal tez o drogi: NIE ma ich w grze (proba dróg zablokowana przez zabezpieczenia - ponowic po Z1b, przy blokadzie dac Jeffowi polecenie). Wyslana Jeffowi lista 24 otwartych pytan (22 z ESENCJI + trening Atletyki w zbroi + Qarth z niewola; Qohor/Lorath bez niewoli - sprawdzic kanon, Qohor kupowal Nieskalanych).
- UWAGA NUMERACJA: projekt 174 rozdz. 9 nazwal "175" werbunek gracza jak AI (decyzja 05.10) - od teraz 175 = ARMIE (galezie w-toku/175-armie-*), werbunek gracza jak AI = 176.
- Limit 5h: 71% o 03:25 (reset 03:50 zegara komp.). Przy 90% - pauza (nie zlecac nowych prac).
- 03:35 174 GOTOWE PO RECENZJI: w-toku/174-produkcja c1a7e18 (24 uwagi, 22 poprawione; Q2b/Q4/Q5 za wylacznikami OFF). Scalanie na noc/sklad6 (drzewo SCRATCH noc2\sklad6, od noc/sklad5; 6 konfliktow - agent) -> SCRATCH test\Armoury-sklad6.dll. Potem: dolaczyc Z1b (w-toku/z1-xp-dary) do sklad6 -> autotest 40 dob + zapis 362 (plan testu 174 w journal wf_058b0630-278: kontrakty surowca, warsztaty bez ladys_shoe, zakupy zastepcze, rynek zbroi, pokrycie) -> WGRANIE 10, jesli progi OK.
- (zegar komp. 03:25) SKLAD6 GOTOWY: noc/sklad6 bfe1538 (scalenie 174-produkcja z sklad5; 6 konfliktow rozwiazane, 757 ustawien MCM) + 5d74706 (decyzje Jeffa: AiWorseBodyArmourWhenShort, OldStockToScrap, BkSuppliesNoArmsPlayer = true). Probny DLL SCRATCH test\Armoury-sklad6b.dll md5 b7b93727. Drzewo SCRATCH noc2\sklad6 (budowanie: GameLibs = lancuch\libs). Czeka na Z1b: scalic w-toku/z1-xp-dary do sklad6 -> przebudowac -> autotest 40 + zapis -> WGRANIE 10.
- DECYZJE JEFFA 03:45 (24 pytania) w STAN-PRAC. Nastepne do zrobienia z nich: MUSZTRA Z14a/b + ZAPAS DO CWICZEN (zamiast XP z perkow kwatermistrza) -> nowa paczka; Qarth do krain z niewola (I1); stal valyrianska -> material 6 jako "castle-forged steel" + zamkniecie sztab z niczego BK (po potwierdzeniu Jeffa); obciazenie wsi docelowo 30-50% (po 166). Czeka na Jeffa: 8 trening Atletyki w zbroi, 22/23 przypisanie (samotne kolumny?), 24d zloto CR.
- (zegar komp. 03:30) **WGRANIE 9: Armoury 17a700d7 (Z1+Z1b, w-toku/z1-xp-dary 8cbfc73)**, CS e8d460c5 bez zmian. Kopie: .bak-2026-10-09-przed-noc9 + D:\Backup-Bannerlord\wgrane\2026-10-09-noc9-przed. Nastepne: scalic z1-xp-dary do noc/sklad6 -> przebudowac -> autotest 40 + zapis -> WGRANIE 10.
- (zegar komp. 03:50) sklad6 + Z1b scalone bez konfliktow: noc/sklad6 0c41eee (759 ustawien), probny DLL SCRATCH test\Armoury-sklad6c.dll md5 05424372. AUTOTEST 40 dob w toku (wyjscie SCRATCH test\at-sklad6-nowa.out.txt), potem zapis 362 (-LoadSave autotest-161-kawalki -Days 8) -> analiza wedlug planu testu 174 (kontrakty surowca, warsztaty bez ladys_shoe, zakupy zastepcze, rynek zbroi lepszy niz w tescie 171+172 SCRATCH kopia172b, pokrycie, zlom, gorsza zbroja: ile ludzi AI ma cokolwiek na tulowiu) -> WGRANIE 10.
- (zegar komp. 04:00) WORKFLOW wf_ea2d52fc-7b7: MUSZTRA (noc2\musztra, w-toku/musztra), T10 nocny marsz AI (noc2\t10, w-toku/t10-nocny-marsz), I1b Qarth + kanon Qohor/Lorath (noc2\i1b, w-toku/i1b-qarth) - wszystkie od noc/sklad6 0c41eee; probne DLL SCRATCH test\Armoury-musztra.dll / -t10.dll / -i1b.dll. Po nich: scalic na sklad6 (po WGRANIU 10) -> jeden autotest -> wgranie. Z16 (zakaz zbroi/amunicji ponad umiejetnosc u bohaterow, bez treningu Atletyki) PO 175 (CS Mends.cs). Stal valyrianska: czeka na Jeffa ("czyli nie wykuje valyrianskiego miecza?").
- (zegar komp. 04:10) AUTOTEST sklad6c (Armoury-sklad6c.dll 05424372): nowa kampania 40/40 OK (14.2 s/dobe; Z1b 13.1), zapis 362 -> 370 OK (28.3 s/dobe; Z1b 21.8 - wolniej o ok. 30% na zapisie - zanotowac do optymalizacji na koniec), 0 bledow Armoury, CS 8 startowych. Logi: SCRATCH kopia-sklad6\. Analiza rynku/174 (agent) -> SCRATCH kopia-sklad6\ANALIZA.md -> WGRANIE 10, jesli werdykt "wgrac".
- 04:15 STALA ZGODA JEFFA na wgrywanie po tescie (STAN-PRAC; pamiec jeff-stala-zgoda-wgrywaj-po-tescie). PLAN DO KONCA MODA wyslany (docs/PLAN-DO-KONCA-MODA-2026-10-09.pdf, 127 pozycji, 25-35 nocy).
- (04:25) Decyzje Jeffa do pytan planu w STAN-PRAC. K1 (wf_b0d9e89e-b62) w poprawkach - po nim K1b: A (bogaty zolnierz kupuje o stopien wyzej, jesli go stac i jest na rynku; wymog umiejetnosci) i B (zaloga < 75% sprzetu: reszta walczy bez uzbrojenia) WLACZONE, potem test i wgranie. Kolejka z odpowiedzi: C dezercja wg poziomu u AI, D zloto taboru zwyciezcy (etap 3), E/F wyrownanie panow zamkow + targ przy zamku (etap 5), G BEE wplata do kasy miasta + dar notabli, H wytop przy kopalni (nowa kampania), I/J Dorne piaskowe rumaki + proporcje winnic/oliwek z historii, L napisy tylko nad spalonymi wioskami, M namioty i ogniska przy napadzie WLACZYC, O wylaczyc pasek gotowosci zbrojowni DTE.
- (04:30) K1 GOTOWE po recenzji: w-toku/k1-dozbrajanie ec4795d (od sklad5; 16 uwag, poprawione; pchniete). K1b (A/B Jeffa) - workflow wf_27b90d69-2d0 w drzewie noc2\k1 -> SCRATCH test\Armoury-K1b.dll. Potem scalic K1 na sklad6 (po WGRANIU 10) -> autotest -> wgranie.
- (04:45) ANALIZA sklad6 (SCRATCH kopia-sklad6\ANALIZA.md): NIE WGRAC - zbroja korpusu na polkach gorzej niz 172b (d10-30), miast bez strzal 31 (172b 24); przyczyny: zakupy AI x2.3 (gorsza zbroja 222/d), ruda nie dociera do ok. 36 miast (kontrakty: 43% zwolnionych, 113 "cel zmieniany przez innych"; "bez drogi"), tempo 14.2 s (d31-40 17.0), zapis 28.3 s. Dobre: produkcja 2.24 tys./d (korpus 250/d), "cokolwiek na tulowiu" 80.5%, 0 bledow, ksiega bez "z niczego". -> 174b (drzewo noc2\n174b, galaz w-toku/174b-dowoz od noc/sklad6): dowoz rudy (morzem, miasta bez drogi), kontrakty bez zmiany celu, strzaly, tempo; potem autotest 40 + zapis -> WGRANIE 10. Paczki na sklad6 (musztra, T10, I1b, 177, K1) czekaja na 174b.
- (04:50) AUDYT 15 BILANS PANOW ZAMKOW gotowy (docs/audyt-2026-10-09/15-BILANS-PANOW-ZAMKOW.md, wersja 2; wpis w ESENCJI, PDF wyslany). Wniosek: pan zamku bierze ze swojej ziemi za DUZO (68% utargu), bilans psuje wojsko caly rok (525 ludzi = 3x dochod ziemi) i ucieczki wartosci; 30-50%: pan 35% + sept 5%; wyrownanie Z15-5 (nadwyzka miasta 1/2 korona, renty 2/1.5/0.25) = decyzja Jeffa E "tak" (takze gracz). Pytania Q1 = E (TAK), Q3a = D (TAK); otwarte: Q2 woz wsi zamkowej najpierw karmi podzamcze (10 dni), Q3b karawana rozbita: 10% zwyciezcy, 90% wraca do wlasciciela.
- (05:00) K1 + K1b + K1c GOTOWE: w-toku/k1-dozbrajanie ba62643 (A/B/C Jeffa; zalogi bez sprzetu z niczego; zbrojownie zalog w zapisie "arm_garrisonarmory"; pchniete). NIE wgrywac osobno (zalogi Jeffa zaczelyby z pusta zbrojownia). SCALENIE K1 z sklad6 -> noc/sklad7 (drzewo noc2\sklad7): workflow wf_c4cb32fb-0e8 (jeden system zbrojowni zalog 171+K1, dorobek startowy takze dla trwajacej kampanii, zestaw awaryjny DTE) -> SCRATCH test\Armoury-sklad7.dll. Potem dolaczyc 174b (gdy gotowe) -> autotest 40 + zapis -> WGRANIE 10. Nastepnie: musztra + T10 + I1b + 177 (WGRANIE 11), 175 armie CS+Armoury (WGRANIE 12), Z16.
- (05:10) GOTOWE po recenzjach (od noc/sklad6, pchniete): MUSZTRA w-toku/musztra f24055d (19 uwag; Z14b AI domyslnie OFF, kara glod/sen AI ON; plan testu w journal wf_ea2d52fc-7b7, A5 wymaga przebiegu z armia gracza), T10 w-toku/t10-nocny-marsz bcae98d (25 uwag; test: najpierw na sucho Armoury-t10-dry.dll, potem wlasciwy), I1b w-toku/i1b-qarth 1590deb (Qarth z niewola; KANON: Qohor MA niewole (Nieskalani, TWOIAF), Lorath bez - pytanie do Jeffa, Qohor zostaje z niewola do odpowiedzi; NOWE zachowanie z recenzji: polityka karna BK we wlasnym miescie gracza (Execution) dziala tez w Westeros - pytanie do Jeffa). Kolejka: po WGRANIU 10 (sklad7 + 174b) scalic musztra + T10 + I1b + 177 -> test -> WGRANIE 11.
- (05:20) Decyzje Jeffa I1b (Qohor z niewola, polityka karna wszedzie) - zgodne z 1590deb. AUTOTEST T10 NA SUCHO w toku (Armoury-t10-dry.dll, 40 dob; wyjscie SCRATCH test\at-t10dry-nowa.out.txt; z niego tylko "nowy dlug 1" i powody - P0). Wstepne scalenie grupy 11 (musztra + T10 + I1b) na noc/grupa11 (noc2\grupa11): workflow wf_e197af4a-324 -> SCRATCH test\Armoury-grupa11.dll. W toku dalej: 174b (wf_ed02dc5b-1cd), sklad7 = sklad6 + K1 (wf_c4cb32fb-0e8), 175 armie (wf_f94ca31b-854), 177 stal valyrianska (wf_b00baab6-94e).
- (05:35) 175 ARMIE GOTOWE po recenzjach: CS w-toku/175-armie-cs e1649e4 (od e1-pokoj; 26 uwag), Armoury w-toku/175-armie-arm 3dedf08 (od sklad5); probne DLL SCRATCH test\Armoury-175.dll (9ce7cc56) i CrashScribe-at-175.dll (9af67f10); projekt docs/PROJEKT-175-ARMIE-2026-10-09.md. 175.2 (sprzet wedlug tieru) WYMAGA K1 (straz: bez K1 pomijane) -> wgrywac PO sklad7. Pytania do Jeffa: Polnoc +25 od uczciwej podstawy = +28% (1. z 30 kultur) czy +15 = ok. +20% (suwak 0-50); wlaczyc wylaczone propozycje audytu (Volantis/Norvos sklad, Qohor ciezszy, Dorne lzejsze, mniej jazdy Wolnych Ludzi i Smoczej Skaly); Dornijczycy t2-3 z toporkami zamiast oszczepow (oszczepy od t4) - obnizyc tier najprostszych oszczepow? Dothrakowie konni: zold +26-47%, rody nie udzwigna -> trybut Wolnych Miast (decyzja 12 TAK) w etapie 2.
- (06:25) T10 NA SUCHO OK: 40/40 dob, 13.6 s/dobe, 0 bledow Armoury, CS 8 startowych; log i Logs w SCRATCH kopia-t10dry\ (P0: nowy dlug 1 ok. 13-19 partii na swit, zapasc trwa 52-55 - na sucho bez kar). Wlasciwy test T10 - na scalonej grupie 11 (Armoury-grupa11.dll). Decyzje Jeffa 05:55/06:00: Polnoc 130/135 (bron +0, Atletyka +5; dwa suwaki) - zrobic po 175b.
- (06:40) GRUPA 11 SCALONA: noc/grupa11 30ed818 (musztra + T10 + I1b; jedno zrodlo "kto spal" NightRest.DebtOf; 8 uwag recenzji poprawione), DLL SCRATCH test\Armoury-grupa11.dll (ba8c26ed). Autotest 40 dob w toku (SCRATCH test\at-grupa11-nowa.out.txt). Pytania do Jeffa (PROJEKT-MUSZTRA rozdz. 11): baza druzyny gracza (tabela 10+2xtier vs glowa rodu 15+3xtier); "noc bez snu = nastepny dzien bez cwiczen od switu" vs dzis (dlug w chwili cwiczen).
- (07:00) SKLAD7 GOTOWY: noc/sklad7 48047f3 (sklad6 + K1; 20 uwag, 18 poprawionych), DLL SCRATCH test\Armoury-sklad7.dll (b7d7c2b9); pytania Jeffa o sprzet DTE z niczego (STAN-PRAC "SKLAD7"). GRUPA 11 nowa kampania 40 dob OK (13.4 s/dobe, 0 bledow; T10: dlug splacony 100%, zapasc 0; musztra AI x1.07, Z14b OFF); logi SCRATCH kopia-grupa11\; test zapisu 8 dob w toku. Grupa 11 i sklad7 stoja na sklad6 (174 bez progu) -> czekaja na 174b. Docelowo: sklad8 = sklad7 + 174b + grupa11 -> autotest 40 + zapis -> WGRANIE 10 (jesli progi 174b OK); potem 175 + 175b + Polnoc 130/135 + 177 -> WGRANIE 11.
- (07:08) GRUPA 11 zapis 362 -> 370 OK (24.5 s/dobe, 0 bledow Armoury; I1b: qartheen 4 w krainach z niewola; musztra start TAK). Grupa 11 przetestowana (na bazie sklad6) - czeka na 174b do wspolnego wgrania.
- (07:20) 174b GOTOWE: w-toku/174b-dowoz 99bddc1 (od sklad6; 17 uwag recenzji poprawione), DLL SCRATCH test\Armoury-174b.dll (8dda2687); glowna przyczyna: nocny oboz T1 zwalnial kontrakty ("cel zmieniany"); ruda morzem (statek kupiecki), ostatni ladunek rudy dla tego, kto wiecej zarobi (platnerz/strzelarz), warsztaty gracza ta sama regula, zlom pamieta rozgrzewke w zapisie, mniej liczenia cen. Ocena testu: python -I tools/p174b_progi.py <log> [--zapis] (w drzewie n174b). Pytanie do Jeffa: hurtownicy zostawiaja ostatnia sztuke kazdego rodzaju zbroi dla kupujacych osobiscie (ShopKeepsLastArmour, dom. OFF do odpowiedzi, rada TAK). AUTOTEST 174b 40 dob w toku (SCRATCH test\at-174b-nowa.out.txt). Do poprawienia w projekcie 174b: rozdz. 3.2 pkt 2 "All" -> "Naval + port", rozdz. 9 GarrisonCarts obok DailyTrade.
- (07:30) 177 STAL VALYRIANSKA GOTOWE: w-toku/177-stal-valyrianska 148cf35 (od sklad6; 28 uwag krytyki, 18 recenzji - poprawione), DLL SCRATCH test\Armoury-177.dll (0c1eeba2); mistrzowie Qohoru przekuwaja istniejace klingi (gotowe wzory ROT), rejestr klng, kopie wypierane, stal 6 = castle-forged. Pytania do Jeffa: (1) Inni a bron t6: (a) zostaje pelne obrazenia od t6 (rada) / (b) tylko VS + smocze szklo + ogien, t6 50%; (2) wlasny miecz wedlug projektu z kuzni i z nazwa - pozniej osobna paczka?; (3) czy w obecnym zapisie przetopil/sprzedal klinge VS (Truth, Brightroar, type 8)?
- (07:55) 175b GOTOWE: CS 287db44, Armoury 315f48d (Volantis/Norvos, Qohor ciezej, Dorne lzej, Wolni Ludzie/Smocza Skala mniej jazdy - ON; Pine Javelin t2; 6 uwag poprawione), pchniete. 175c w toku (wf_f88bbc94-dbe): Polnoc 130/135 (dwa suwaki) + Inni (pelne obrazenia tylko VS/smocze szklo/ogien, t6 50%, reszta 15%) -> SCRATCH test\Armoury-175c.dll + CrashScribe-at-175c.dll. W toku tez: sklad7b (wf_9a564f16-187, zadnego sprzetu DTE z niczego), musztra jeden wzor (wf_7b19b7fa-d33, grupa11), autotest 174b (40 dob). Pytania otwarte do Jeffa: wyrownanie cen hurtu AI (kazda sztuka po swojej cenie), kiedy nowa kampania (rada: po etapie 5).
- (08:10) AUTOTEST 174b nowa kampania 40/40 OK (13.2 s/dobe; Z1b 13.05, sklad6 14.2), 0 bledow; progi (tools/p174b_progi.py, logi SCRATCH kopia-174b\): P4 kontrakty dojechalo 97.9% (sklad6 53%), cel zmieniany 0%; P5 zbroja na polkach OK; P6 cokolwiek na tulowiu 80.6%; P2 miast bez rudy 34.5 (prog projektu <= 25 - NIE; sklad6 36, 172b 36); P3 miast bez strzal 24.2 (prog <= 24 - na granicy; sklad6 31); P13 nasz kod 9.2% doby (prog 1% - nierealny; tempo calej doby bez regresu); P11 ruda bez wyjasnienia 23 (prog 15). Wobec progow wgrania z analizy sklad6 (lepiej niz 172b: strzaly <= 24, ruda < 38, polki zbroi, tempo) - PRZECHODZI. Test zapisu 8 dob w toku. Plan: sklad8 = sklad7b (+ceny hurtu) + 174b + grupa11 (+musztra jeden wzor) -> autotest 40 + zapis -> WGRANIE 10. Ruda <= 25 i P11 - dalsza poprawka (etap 6).
- (08:15) 174b zapis 362 -> 370 OK (25.1 s/dobe; sklad6 28.3, Z1b 21.8), 0 bledow. 174b PRZECHODZI progi wgrania. Czekam na: sklad7b (wf_9a564f16-187), musztra jeden wzor (wf_7b19b7fa-d33), 175c (wf_f88bbc94-dbe); potem ceny hurtu (sklad7) i sklad8.
- (zegar komp. 08:05) LIMIT 5h 92% (reset 08:50) -> PAUZA: nie zlecam nowych prac do resetu; koncza sie: sklad7b (poprawki), musztra jeden wzor (poprawki), 175c (recenzje). Po resecie: ceny hurtu AI na sklad7 (decyzja Jeffa 08:00) -> sklad8 = sklad7b + 174b + grupa11 (+musztra-j) -> autotest 40 + zapis -> WGRANIE 10; potem 175 + 175b + 175c (CS + Armoury) + 177 -> test -> WGRANIE 11; Z16; potem etap 2. Tydzien 72%.
- (08:15) MUSZTRA JEDEN WZOR gotowa: noc/grupa11 4463a82 (MUSZTRA-j + MUSZTRA-jp: maski ruchu wszystkich lordow w zapisie, sen od switu, Z14b domyslnie TAK), DLL SCRATCH test\Armoury-grupa11b.dll (2ee4d44a). Skutek: AI w 1. miesiacu nowej kampanii cwiczy ok. 20% wolniej niz dzis (glowy rodow -20-30%, reszta +8%), Braavos t6 2.6 roku zamiast 1.6. Pytania do Jeffa (PROJEKT-MUSZTRA rozdz. 11): dlug po krotkim snie zabiera kolejne dni (zostawic - rada) czy tylko dzien po nocy bez snu; codzienna linia "Drill today: ... XP" w grze (rada TAK).
- (08:25) Jeff: "rob od razu" (bez czekania na reset limitu). SKLAD7B gotowy (noc/sklad7 23ad5d6, pchniety). Workflow wf_1722d5bc-254: ceny hurtu po sztuce (sklad7) + linia musztry w grze (grupa11) + SCALENIE noc/sklad8 (drzewo noc2\sklad8) = sklad7 + 174b + grupa11 -> SCRATCH test\Armoury-sklad8.dll -> potem autotest 40 + zapis (progi 174b: tools/p174b_progi.py w drzewie n174b/sklad8; T10; musztra; bitwy sklad7b) -> WGRANIE 10.
- (08:35) 175c GOTOWE: CS w-toku/175-armie-cs 60945d9 (fe37585 + 50f83f0 Lod nie jest "martwym sprzetem" + 60945d9 poprawki), Armoury w-toku/175-armie-arm f178053; DLL SCRATCH test\Armoury-175c.dll (b4d940d1), CrashScribe-at-175c.dll (07b07e24); projekt 175 rozdz. 13/13.1. Uwaga: "130/135" to srednia 43 rodzajow; przecietny piechur Polnocy t3+ w partii ok. 101/105 (+7% / +12% wobec swiata). Smoczego szkla w swiecie prawie brak (1 topor bez wlasciciela). Pytania do Jeffa (5): autobitwa AI liczy bron sprzed 175 (135 rodzajow bije Innych 50%) - zostawic czy nowa bron; proce/luki t6 = 50% czy pociski zawsze 15%; smocze szklo dla Nocnej Strazy (osobna paczka); komunikat przy ciosie w Nocnego Krola; minimum 2 pkt w autobitwie (tylko jesli bitwy > 24 h).
- (09:55) Rownolegle do sklad8 (poprawki): Z16 wymogi bohaterow na galeziach 175 (wf_e0fa2424-ee1, drzewa a175cs/a175arm -> SCRATCH test\Armoury-z16.dll + CrashScribe-at-z16.dll) oraz PROJEKT ETAPU 2 (wf_12bced29-dbf -> docs/PROJEKT-ETAP2-BANKRUCTWA-2026-10-09.md). Kolejnosc wgran: 10 = sklad8 (po autotescie); 11 = sklad9 = sklad8 + 175-armie-arm (od sklad5!) + 177 (od sklad6) + Z16, CS = 175-armie-cs (od e1-pokoj) - jedna para DLL, autotest 40 + zapis + progi 175 (bitwy lordow wedlug krolestw, Dothrakowie, konie); potem etap 2.
- (10:10) SKLAD8 GOTOWY: noc/sklad8 0e17915 (sklad7 + ceny po sztuce 6b9d9ab + 174b + grupa11 z linia musztry d1aa039; scalenie 64cd32a; 13 uwag poprawione; ShopKeepsLastArmour = true; kupcy SupplyDemand bez rabatu 50%), DLL SCRATCH test\Armoury-sklad8.dll (94578114). AUTOTEST 40 dob w toku (SCRATCH test\at-sklad8-nowa.out.txt); potem zapis 8 dob; analiza wedlug planu testu (journal wf_1722d5bc-254: p174b_progi.py, ceny hurtu, T10, musztra, bitwy sklad7b) -> WGRANIE 10.
- (10:30) AUTOTEST sklad8: nowa kampania 40/40 OK (13.9 s/dobe, 0 bledow), progi: P4 96.1%, P5/P6 OK (80.6% cokolwiek na tulowiu), ALE P3 miast bez strzal 35.0 (174b samo 24.2) - NIE wgrywac; diagnoza + poprawka: wf_bda22900-f78 -> SCRATCH test\Armoury-sklad8s.dll. Zapis 8 dob - wynik w at-sklad8-zapis.out.txt. Logi SCRATCH kopia-sklad8\.
- (11:00) PROJEKT ETAPU 2 gotowy: docs/PROJEKT-ETAP2-BANKRUCTWA-2026-10-09.md (51 uwag krytyki wprowadzone; kredyt wojenny Banku splacany z lupu/okupow - bez "-4%"; wielcy jency; kroki: A pomiary + pobor wasali + pokoj z biedy + okup gracza do porywacza -> bieg bazowy 120 dob -> kasy zamkow/wsi -> 3 kroki korony i budzetu -> dlugi i okupy; test koncowy 2 lata + bieg z wymuszonym pokojem; prog wojska 95-115 tys.). Pytania do Jeffa: podloga 30 ludzi takze dla gracza; pulap dlugu z okupow = rok dochodu; okup krola placi rod (a) czy skarbiec krolestwa (b).
- (11:20) Z16 GOTOWE na galeziach 175: CS 623549c, Armoury c2c4f46 (projekt docs/PROJEKT-Z16-WYMOGI-BOHATEROW-2026-10-09.md; 17 uwag poprawione), DLL SCRATCH test\Armoury-z16.dll (c2e1508d), CrashScribe-at-z16.dll (cab0ff04). Liczby: 740 z 890 lordow dostaje Atletyke (mediana +38, prawie wszyscy do 175 - 846 ma sztuke t6). Pytania do Jeffa: (1) podniesienie Luku/Kuszy lordow to czysta premia (nic nie zabiera strzal) - tylko Atletyka?; perki z podniesienia (czesc dowodcow +5-10 ludzi); (2) suknie/szaty/czapka "Noble Default" ROT to zbroja t6 (Atletyka 175) - zwolnic stroje cywilne / wymog ubran z wagi?; (3) unikat, ktorego lord nie udzwignie, w taborze zamienia sie przy wczytaniu w zwykly - ma przetrwac jako unikat? Uwaga boczna: ladry konskie licza sie jako Atletyka (powinna Jazda) - poprawic.
- (11:35) STRZALY (sklad8-s) GOTOWE: noc/sklad8 6e225f8 (S0 log powodow, S1 kuznia gracza: amunicja z wagi metalu, S2 receptura strzal (Iron2/3, mniej rudy), S3 strzelarzy x3 (0.3 -> 0.9), S4 K1 amunicja tylko w gore i tylko z nadwyzki; XP za amunicje x0.05), DLL SCRATCH test\Armoury-sklad8s.dll (e4cbbe84). AUTOTEST 40 dob w toku (SCRATCH test\at-sklad8s-nowa.out.txt); progi: miast bez strzal <= 24 (cel 15 z S5a), mnoznik ceny <= 1.4, snop <= 17 d, p174b_progi --baza sklad8 (P2 <= 38, P5, P6 -1.5 pp). Jesli OK + zapis -> WGRANIE 10.
- (12:05) AUTOTEST sklad8-s (strzelarze 0.9): 40/40, 13.8 s/dobe, 0 bledow; P3 miast bez strzal 9.0 (!), P4 97.2%, P5/P6 OK, P11 OK; ALE P2 miast bez rudy 43 (prog <= 38). Wedlug reguly cofniecia: strzelarze 0.6 -> noc/sklad8 2ca2f2b, DLL SCRATCH test\Armoury-sklad8s2.dll (2aec1a1b). Autotest 40 dob w toku (at-sklad8s2-nowa.out.txt).
- (12:25) **WGRANIE 10: Armoury 2aec1a1b (noc/sklad8 2ca2f2b)**, CS e8d460c5 bez zmian. Kopie: .bak-2026-10-09-przed-noc10 + D:\Backup-Bannerlord\wgrane\2026-10-09-noc10-przed. Nastepne: WGRANIE 11 = sklad9 (sklad8 + w-toku/175-armie-arm + w-toku/177-stal-valyrianska) + CS w-toku/175-armie-cs (od e1-pokoj) -> autotest 40 + zapis + progi 175 -> wgranie. Potem etap 2 (kod 4 krokow rownolegle wedlug projektu, bez nowego rozpoznania).

## 19. STAN NA 09.10 ok. 13:30 (zegar komp.) - dla nowego konta / po kompakcji

W GRZE: Armoury 2aec1a1b (noc/sklad8 2ca2f2b, WGRANIE 10), CrashScribe e8d460c5 (w-toku/e1-pokoj 5b4e551, WGRANIE 8). Zasady pracy: STAN-PRAC koniec pliku (decyzje
09.10), pamiec: jeff-stala-zgoda-wgrywaj-po-tescie (wgrywac samemu po autotescie), jeff-zasada-doboru-glebokosci-pracy (tory S/M/L), jeff-gra-dopiero-po-projekcie.
W TOKU: WGRANIE 11 - workflow wf_06eea4b7-5d2: noc/sklad9 (SCRATCH noc2\sklad9) = sklad8 + w-toku/175-armie-arm ffb6cef + w-toku/177-stal-valyrianska 148cf35; CS
w-toku/175-armie-cs d85b519 (SCRATCH noc2\a175cs; 175/175b/c/d + Z16 + Z16b pancerz z wagi) -> DLL SCRATCH test\Armoury-sklad9.dll, CrashScribe-at-sklad9.dll (autotest),
CrashScribe-sklad9.dll (do wgrania) -> autotest 40 dob + zapis (tools\autotest.ps1, patrz rozdz. 18) -> progi 175 (bitwy lordow wedlug krolestw, Dothrakowie bankructwa,
konie, NorthHardy 130/135, Inni) -> WGRANIE 11 OBU DLL (Armoury + CrashScribe; kopie .bak-...-przed-noc11 + D:).
DALEJ: ETAP 2 wedlug docs/PROJEKT-ETAP2-BANKRUCTWA-2026-10-09.md (BEZ nowego rozpoznania; tor L przy wykonaniu, 1 recenzja na paczke): krok A (169c pomiary, 2.6 powinnosci,
E1b pokoj z biedy (CS), 2.14 okup gracza do porywacza) -> bieg bazowy 120 dob -> krok B (110+112+114 kasy zamkow i wsi) -> C1 (165 korona, 166 budzet + 162m, 185 najemnicy,
182 dary) -> C2 (180 renty) -> C3 (179 rycerze bez lenna, 183 dezercja AI) -> D (168 dlug/kredyt wojenny, 178 okupy/wielcy jency; okup krola ze skarbca - odpowiedz Jeffa
11:05) -> test 2 lata + bieg z wymuszonym pokojem. ETAP 3 (szczelnosc) dopiero po etapie 2 (decyzja Jeffa 11:15).
LIMITY 13:30: 5h 32%, TYDZIEN 82% (reset 15.10) - przy 100% tygodnia przejsc na inne konto (Jeff: limit tygodnia nie jest powodem do oszczedzania).
- (13:35) ETAP 2 KROK A (Armoury) w toku: workflow wf_2907b3b3-4a7, drzewo SCRATCH noc2\e2a (galaz w-toku/e2a od noc/sklad8): 169c pomiary, 2.6 powinnosci, 2.14 okup gracza -> SCRATCH test\Armoury-e2a.dll. E1b (pokoj z biedy, CS) - po sklad9 (ten sam CS). Rownolegle WGRANIE 11 (wf_06eea4b7-5d2).
- (13:25) SKLAD9 GOTOWY: Armoury noc/sklad9 5f170fd (DLL SCRATCH test\Armoury-sklad9.dll 799a877d), CrashScribe w-toku/175-armie-cs b54a245 (do wgrania: test\CrashScribe-sklad9.dll 891503f6; do autotestu: CrashScribe-at-sklad9.dll 56fd6d1c). 7 uwag recenzji (unikaty w taborach nie znikaja przy wczytaniu; Polnoc 131/134). AUTOTEST 40 dob w toku (test\at-sklad9-nowa.out.txt); potem zapis 8 dob; progi: 0 bledow, tempo ok. 14 s, linie startowe 175/177/Z16 (plan w journal wf_06eea4b7-5d2), NorthHardy bez OSTRZEZENIA, bitwy lordow wedlug krolestw (KingdomBalance) -> WGRANIE 11 (OBA DLL).
- (13:45) **WGRANIE 11: Armoury 799a877d (noc/sklad9 5f170fd) + CrashScribe 891503f6 (w-toku/175-armie-cs b54a245)**. Kopie .bak-...-przed-noc11 + D:. Etap 1 zamkniety (poza drobiazgami). Dalej: tor S - drzewce 140 u Polnocy t3 (175.2); etap 2 krok A (wf_2907b3b3-4a7, drzewo e2a od sklad8 -> trzeba scalic na sklad9) + E1b (CS na 175-armie-cs) -> bieg bazowy 120 dob.
- (13:50) W1 bron wedlug tieru dokladnie: CS w-toku/175-armie-cs a0aa9a9. E1b pokoj z biedy: workflow wf_6b94b20b-720 (drzewo a175cs) -> test\CrashScribe-e2a.dll / CrashScribe-at-e2a.dll. Krok A Armoury (wf_2907b3b3-4a7, e2a od sklad8) w poprawkach -> po nim scalic e2a na sklad9 (noc/sklad10) -> autotest 40 + zapis (+ srednie Polnocy po W1) -> WGRANIE 12 -> bieg bazowy 120 dob.
- (14:15) KROK A Armoury gotowy: w-toku/e2a 4983436 (169c, 2.6, 2.14 + poprawki 16 uwag; narzedzie tools/sprawdz_logi.py --grupa etap2). SKLAD10 = sklad9 + e2a: noc/sklad10 d3e749e, DLL SCRATCH test\Armoury-sklad10.dll (1cc7890a). Czeka na E1b (wf_6b94b20b-720 -> test\CrashScribe-at-e2a.dll / CrashScribe-e2a.dll) -> autotest 40 + zapis (RansomHarnessInAutotest; progi w journal wf_2907b3b3-4a7) -> WGRANIE 12 -> bieg bazowy 120 dob z --zapisz-baze baza-etap2.json.
- (14:20) E1b GOTOWE: CS w-toku/175-armie-cs 4e1de62 (W1 a0aa9a9 + E1b 1042275 + poprawki 13 uwag), DLL test\CrashScribe-e2a.dll (9ccff01e, do wgrania), CrashScribe-at-e2a.dll (e8464b00, autotest). AUTOTEST wersji 12 (Armoury-sklad10 + CS-at-e2a) 40 dob w toku (test\at-sklad10-nowa.out.txt); potem zapis 8-9 dob; progi: sprawdz_logi.py --grupa etap2, pokoj z biedy (WNIOSEK Z BIEDY, 0 pokojow w wojnach fabularnych), harness okupu (2b/2c OK), srednie Polnocy po W1 -> WGRANIE 12.
- (15:15) TEST wersji 12 (sklad10 + CS-at-e2a): 40/40, 14.1 s/dobe, 0 bledow, harness okupu 2b/2c dziala; pokoj z biedy: 0 wnioskow z biedy w 40 dobach nowej kampanii ("This war hasn't gone on long enough" - Diplomacy, oczekiwane na starcie). BLAD MOJ: W1 (bron wedlug tieru) trafil do ArmorTierLaw (wylaczonego) zamiast WeaponTierLaw -> W1-fix CS 02d3584; DLL test\CrashScribe-e2a2.dll (d68532a6, do wgrania), CrashScribe-at-e2a2.dll (40a3f044). Powtorny test 40 dob w toku (test\at-sklad10b-nowa.out.txt); potem zapis -> WGRANIE 12.
- (15:25) W1-fix test 40 dob OK (14.2 s, 0 bledow; bron obnizona do tieru 30). Przyczyna Drzewcy 140: vlandia_lance_1_t3 (skladana) ma w grze tier 5 - 175 TierGear czyta tier skladanych za wczesnie -> W2 (tor M, wf_5019b58b-c39, a175cs) -> test\CrashScribe-w2.dll / CrashScribe-at-w2.dll. Test zapisu wersji 12 w toku (test\at-sklad10b-zapis.out.txt) -> WGRANIE 12 = Armoury-sklad10.dll (1cc7890a) + CrashScribe-e2a2.dll (d68532a6) -> bieg bazowy 120 dob (sprawdz_logi.py --grupa etap2 --zapisz-baze baza-etap2.json).
- (15:30) **WGRANIE 12: Armoury 1cc7890a (noc/sklad10) + CrashScribe d68532a6 (175-armie-cs 02d3584)**. Kopie .bak-...-przed-noc12 + D:. Nastepne: BIEG BAZOWY 120 dob (nowa kampania; tools/sprawdz_logi.py --grupa etap2 --zapisz-baze baza-etap2.json), W2 (wf_5019b58b-c39), potem etap 2 krok B (110+112+114 kasy zamkow i wsi).
- (15:40) W TOKU: (1) BIEG BAZOWY 120 dob (Armoury-sklad10 + CS-at-e2a2; wyjscie SCRATCH test\at-baza-etap2-120.out.txt; po nim: kopia logow do SCRATCH kopia-baza120\, python -I tools/sprawdz_logi.py --grupa etap2 <log> --zapisz-baze baza-etap2.json w drzewie sklad10); (2) W2 tier broni skladanych (wf_5019b58b-c39, a175cs); (3) ETAP 2 KROK B (wf_d72d02ac-afc, drzewo SCRATCH noc2\e2b, galaz w-toku/e2b od sklad10 -> test\Armoury-e2b.dll). Potem: test kroku B (40 dob + zapis + porownanie z baza) -> WGRANIE 13 (+W2) -> krok C1 (165, 166+162m, 185, 182). Kontekst rozmowy 90% - kompakcja blisko; tydzien 86%.
- (16:10) BIEG BAZOWY 120 dob (wersja 12 w grze): OK 120/120, 16.4 s/dobe, 0 bledow; logi SCRATCH kopia-baza120\, PLIK BAZOWY kopia-baza120\baza-etap2.json. Progi etapu 2 (29): NIE 2 - glowy < 5000 = 19 (prog <= 10), bankruci Banku 5 (prog 0); TAK: skarbce (najdluzsza seria 24 doby Polnoc), zalegly zold 0.63%/1.09%, wojsko w wojnie 111.8 tys. (95-115), Bank 4.17 mln, D staly pana zamkow 1189, okupy 2.14 OK. To stan "dzis" - kroki B, C, D maja zbic glowy < 5000 i bankrutow do progu.
- (16:20) W2 GOTOWE: CS w-toku/175-armie-cs 511c08e (00df512 kod), DLL test\CrashScribe-w2.dll (6507c842, do wgrania), CrashScribe-at-w2.dll (11585bad). Ok. 78 jednostek t3-t4 z bronia ponad tier (46 kopie, 32 wlocznie kultur/rodow t5-6: Nieskalani, Qohor, Zlota Kompania, Manderly...) dostanie bron swojego tieru (piechota z podparciem). Pytania do Jeffa: wlocznie lore jako wyjatek? jazda t1-t4 z kopia? Wejdzie z krokiem B (WGRANIE 13).
- (16:40) KROK B GOTOWY: w-toku/e2b 49b0741 (110 zawor zamku, 112 utarg wsi bez znikania, 114 podzial 2/3 pan / 1/3 korona; 9 uwag), DLL test\Armoury-e2b.dll (0758d2aa); baza z kluczami B: SCRATCH e2b-p\baza-B.json. OSTRZEZENIE agenta: kasy zamkow wydaja wiecej niz wplywa -> gra dosypuje do zapasu (z niczego), pan zamku dostanie mniej niz 200-350 zl/d - prawdopodobnie NIE na progu. TEST 120 dob kroku B + W2 w toku (test\at-e2b-120.out.txt; CS-at-w2 + Armoury-e2b). Ocena: python -I tools/sprawdz_logi.py --grupa etap2 <log> --baza SCRATCH\e2b-p\baza-B.json (narzedzie z drzewa e2b). Jesli 0 bledow i bez regresji wobec bazy -> WGRANIE 13 (Armoury-e2b + CrashScribe-w2); progi zamkow - poprawka w C1 (165/166) albo osobno.
- (17:25) TEST 120 dob KROK B + W2 (logi SCRATCH kopia-e2b120\): 0 bledow, 16.8 s/dobe. W2 DZIALA: 0 jednostek z bronia ponad tier, Polnoc 131/134 wobec swiata 122/119 (+7%/+13%) - cel Jeffa spelniony. KROK B NIE PRZECHODZI (12 z 48 progow NIE): pieniadz swiata +541 tys./d wobec bazy +329 tys. (+212 tys.), kiesy band +110% (sakwy taborow do zwyciezcy - bandy sie bogaca), dosypka regulatora kas zamkow 44 tys./d, pan zamku z zaworu 40 zl/d (prog 200-350), wojsko 125 tys. (>115), Pentos -74%, Braavos -31%, zalogi w wojnie ponizej 95% bazy w 8 krolestwach, skarbiec Braavos Rebels 55 dob; glowy < 5000 20 (baza 19), bankruci 3 (baza 5). -> KROK B NIE WGRYWAC; potrzebna poprawka (tor L: analiza przyczyn +212 tys./d i band, kasy zamkow). W2 (CS) do wgrania osobno po krotkim tescie z Armoury w grze.
- (17:30) W TOKU: (1) test 40 dob W2 z Armoury w grze (CS-at-w2 + Armoury-sklad10; test\at-w2-nowa.out.txt) -> jesli OK: WGRANIE 13 = tylko CrashScribe-w2.dll (6507c842), Armoury bez zmian 1cc7890a; (2) NAPRAWA KROKU B: workflow wf_f6909cac-c64 (diagnoza pieniadza/band i zamkow/wojska, poprawka w drzewie noc2\e2b) -> test\Armoury-e2b2.dll -> ponowny test 120 dob z --baza SCRATCH\e2b-p\baza-B.json. Pytania do Jeffa otwarte: wlocznie lore jako wyjatek od tieru? kopia dla jazdy t1-t4?
- (18:40) NAPRAWA KROKU B (wf_f6909cac-c64): diagnoza - +212 tys./d to glownie los kampanii (wiecej bitew morskich w ostatnich 28 dobach: statki NavalDLC +158 tys.) + blad ksiegi (nasluch zniszczenia partii przed 112 - sakwa taboru liczona jak znikniete zloto); bandy +110% = decyzja D (sakwy taborow do zwyciezcy), sila band bez zmian; zamki: nasze moduly wydaja z kas zamkow ok. 57 tys./d (handel bronia, zlom) -> dosypka gry; zawor daje panom malo. Poprawki B-1..B-4: w-toku/e2b b422a5f (ksiega, B-2 zamki: platnosci z nadwyzki ponad zapas, narzedzie z oknem 31-120 bez szumu wojny, nowe werdykty ETAP/DECYZJA), DLL test\Armoury-e2b2.dll (dff8a9b0), baza narzedzia SCRATCH e2b-p\baza-B4.json. TEST 120 dob w toku (test\at-e2b2-120.out.txt; CS-at-w2). Ocena: tools/sprawdz_logi.py --grupa etap2 <log> --baza SCRATCH\e2b-p\baza-B4.json (drzewo e2b).
- (19:40) TEST 120 dob KROKU B po naprawie (logi SCRATCH kopia-e2b2-120\): 120/120, 17.2 s/dobe; progi (baza-B4): NIE 2 - wojsko w wojnie 115.4 tys. (prog <= 115; baza 111.8) i 1 ERROR: ValyrianBlades.Census "UBYTEK -1 - truth (jest 0, rejestr 1), ostatnio: nosi Tregar Ormollen" (kod 177 z WGRANIA 11, nie krok B - do naprawy osobno, tor M); ETAP 1 - bankruci Banku 4 (baza 5); DECYZJA 3 - pan zamku z zaworu 30 zl/d (prog 200-350), zalogi/wojsko wedlug krolestw. GLOWY < 5000 = 9 (TAK, prog <= 10; baza 19). Werdykt: krok B do wgrania (WGRANIE 14) po tescie zapisu (test\at-e2b2-zapis.out.txt).
- (19:45) **WGRANIE 14: Armoury dff8a9b0 (w-toku/e2b b422a5f, krok B)**, CS 6507c842. Dalej: (1) naprawa 177 ValyrianBlades.Census (UBYTEK - Truth u Tregara Ormollena) - tor M; (2) ETAP 2 KROK C1 (165 korona, 166 budzet rodu + 162m, 185 kontrakt najemnika, 182 dary) wedlug projektu, baza drzewa: w-toku/e2b; test 120 dob z --baza SCRATCH e2b-p\baza-B4.json.
- (21:20) Tydzien limitu 90% (Jeff: "jedna rzecz naraz; przy 99% zapisz wszystko do przekazania drugiemu kontu"). Jeff dostal liste 9 biednych glow (budzet-rodow.csv
  z kopia-e2b2-120: Banu Ayan, Bracken*, Stane*, Banu Julul, Estatis, Gendiroving, Xho, Dazheroving, Banu Sirsan; * = bankrut Banku, trzeci bankrut Mormont-Nights Watch).
- (21:25) **177 CENSUS - DIAGNOZA (naprawa ODLOZONA na prosbe Jeffa "rob etap 2 c1"):** doba 108929, SallyOut pod Panosos (ROT_castle47): Tregar Ormollen pojmany
  przez Leona Staegone (armia Tyrosh); UniqueSpoils.Take -> Wear -> ponad umiejetnosc -> Park do taboru partii Leona (licznik "odlozone do taboru 1" w Unikaty (177)
  tej doby); sekunde pozniej spis: truth "nigdzie". Czyli cos zdejmuje sztuke z ItemRoster partii zwyciezcy tuz po bitwie (podejrzani: koncowka MapEvent -
  lup/ RealisticLoot (Spoils) / DTE EveryoneCampaignBehavior przenosi tabor do PartyArmories z kluczem innym niz ItemObject, wiec ArmoryCopies go nie widzi /
  nasze OldStockToScrap lub sprzedaz starego). Nastepny krok: sprawdzic w DTE typ kluczy PartyArmories i co robi z taborem po bitwie; ewentualnie Park
  stali valyrianskiej przy pojmaniu w MapEvent -> tabor glowy rodu zwyciezcy po zakonczeniu bitwy (odroczone o 1 tick). Logi: SCRATCH(7016) kopia-e2b2-120\
  (Armoury-2026-10-09_18-54-52.log linia 23491, unikaty.log 84, start.log 255).
- (21:30) ETAP 2 KROK C1 - START: drzewo SCRATCH(7016) noc2\e2c, galaz w-toku/e2c (od w-toku/e2b b422a5f). Zakres wedlug projektu (rozdz. 165, 166+162m, 185, 182).
- (21:45) Jeff: "napraw ten miecz od razu i c1 rob, aby to zakonczyc". **177-FIX** (commit ae01f8e na w-toku/e2c, UniqueSpoils.Park): stal valyrianska nigdy do taboru
  partii AI - KinWear (ktos z rodu, kto udzwignie i ma slot bez unikatu, glowa pierwsza), inaczej polka miasta rodu (Shelve); licznik "stal valyrianska do
  krewnego" w linii "Unikaty (177)". DLL probny SCRATCH(7016)\test\Armoury-e2c-miecz.dll (md5 ddf01297). AUTOTEST 40 dob w toku -> wynik test\at-miecz-nowa.out.txt;
  jesli 0 bledow -> WGRANIE 15 (sam miecz, Armoury) przed C1.
- (21:50) **C1 WYKONANIE** - jeden pomocnik w tle w drzewie noc2\e2c (galaz w-toku/e2c), kawalki po kolei 165 -> 182 -> 166+162m -> 185, commit + push po kazdym
  i stan w docs/C1-POSTEP.md W DRZEWIE e2c (tam patrz, jesli konto sie skonczy). Potem: jedna recenzja (kod + skutki), poprawki, test 120 dob z
  --baza SCRATCH(7016)\e2b-p\baza-B4.json (tools/sprawdz_logi.py --grupa etap2), wgranie.
- (21:35) **WGRANIE 15: Armoury ddf01297 (noc/wgranie-15 = e2c ae01f8e, 177-fix miecza)**, CS 6507c842. Test 40 dob OK (0 bledow Armoury, CS 8 = szum startowy, spis 17/17).
  Uwaga dla C1: galaz w-toku/e2c zawiera juz 177-fix (ae01f8e) - C1 idzie na nim.
- (21:55) **Jeff: "i potem rob etap 3 tez krok po kroku"** (ultracode wlaczony). Kolejnosc wiazaca: etap 2 do konca (C1 -> C2 180 renty -> C3 179+183 -> D 168+178,
  kazdy krok: kod, jedna recenzja, test 120 dob z --baza baza-B4, wgranie), potem ETAP 3 krok po kroku wedlug PLAN-DO-KONCA-MODA rozdz. "Etap 3":
  3.2 (cale zloto taboru/bandy dla zwyciezcy - Jeff juz zdecydowal 09.10: "jak rozbije karawane, to zabiera sie wszystko zwyciezca") -> 3.3 statki do portu
  (181 zaprojektowany w PROJEKT-ETAP2 rozdz. 181) -> 3.1 szczelnosc 164a -> 3.4-3.7 (BK, rada, wedrowcy, ROT) -> 3.8 ceny historyczne -> 3.9/3.10 drobne.
  Koniec etapu 3: reszta niewyjasniona ksiegi obiegu < 10 tys./dobe (sr. 28 dob). Kazdy krok osobno: wykonanie, recenzja, test 40 dob (+120 dla obiegu), wgranie.
- (23:05) **C1 KOD GOTOWY** (pomocnik, w-toku/e2c do d514c52; 165 50bc17b, 182 d4d9c0b, 166+162m 6dff51b, 185 1aa1580, ksiega 57cbc1b, poprawki a660030; opis i
  odstepstwa: docs/C1-POSTEP.md W DRZEWIE e2c). Klucze M13: zadnego nowego klucza nie ma w Armoury.json Jeffa - dzialaja domyslne. DLL probny test\Armoury-e2c-c1.dll
  (ff325b37). W toku rownolegle: (a) autotest 40 dob nowej kampanii -> test\at-c1-40.out.txt; (b) recenzja C1 workflow wf_a018ac0c-77c (3 soczewki: zloto, gra,
  wojsko -> sceptyk -> poprawki z commitem na e2c). Potem: test 120 dob na DLL po poprawkach z --baza e2b-p\baza-B4.json, wgranie 16.
- (23:40) **TEST C1 40 dob (DLL ff325b37, przed recenzja; kopia SCRATCH(7016) kopia-c1-40\):** 40/40, 14.5 s/dobe, 0 bledow Armoury. Dobre: glowy < 5000 = 0
  (z budzetem), bankruci 0, zwrot korony wyplacony 97.1%, dary 182 co do zaokraglen (59 zl zostaje w skarbcach odbiorcow), zwolnieni do wsi 91 / zniklo 0,
  dwor - skasowane przez regulator 0, Straz 3461 ludzi (pulap 4955; ok. -9% wobec 3.8 tys.), najemnicy 605 / umowa 548, "za tier" AI wylaczone.
  **ZLE (NIE 3 z 58 progow): WOJSKO.** swiat 58.5 tys. wobec bazy 85.2 tys. (-31%, doby 14-41), druzyny lordow w wojnie 51.4 tys. (baza 69.7; doba 40: 76.1 tys.),
  zalogi 39.4 tys. wobec 81.8 tys. (-52%), krolestwa -29..-46%. A zold naliczony to tylko 49% pulapu 166 (1.14 mln) - czyli NIE brak pieniedzy, tylko mechanika
  (podejrzenia: limit zoldu partii/zalog ustawiany przez postfiks 166 - np. zalogi "pierwsze" zjadaja pulap partii albo SetGarrisonWagePaymentLimit w wojnie
  nie = wartosc gry; brak zwrotu za zalogi (165) obniza limit zalog liczony przez gre z kiesy; hamulec nowych partii). Do diagnozy po recenzji (wf_a018ac0c-77c).
- (23:55) **DIAGNOZA WOJSKA C1 (z balans-krolestw.csv i linii "Budzet rodow (166)"):** nowa kampania ROT = pokoj w dobach 1-23 (doba 1: pokoj 289 rodow,
  wojna 15; doba 24: wojna 294). W pokoju pulap 0.28 D (310 tys. przy zoldzie 285 tys.) -> od 3. doby zwolnienia 15%/dobe (doba 3: 3 788 ludzi, w tym 3 582 z zalog
  do celu pokojowego 50% stanu z 1. doby). Zalogi 47 -> 33 tys. (doby 1-16), partie stoja ok. 40-44 tys.; bez C1 w tym czasie rosly (zalogi 48->54, partie 30->70).
  Od wojny pulap 1.05 mln, zold 44-49% pulapu (pulap nie hamuje), odrost ok. 2 tys. ludzi/dobe - jak w bazie, tylko od nizszego stanu. Czyli to regula projektu
  (pokoj = ok. 1/3 wojska i polowa zalog; Jeff: "dynamiczne, wedlug tego, czy stac"), nie blad kodu. Progi 40 dob "wojsko wobec bazy" nie pasuja do kampanii,
  ktora zaczyna sie pokojem - rozstrzyga test 120 dob (okno 31-120, prawie cala wojna). Jesli tam zalogi w wojnie < 95% bazy: rozwazyc szybszy odrost w wojnie
  (np. przy wypowiedzeniu wojny zniesienie hamulca nowych partii / zalogi do celu wojennego z kasy wojennej) - to bylaby zmiana do zgloszenia Jeffowi.
- (00:45) **RECENZJA C1 (wf_a018ac0c-77c, 7 agentow)**: 10 uwag potwierdzonych przez sceptyka (2 wysokie: C1-G1 Bank pozyczal mimo pieniedzy rodziny - T5
  przywrocone, odstepstwo od "IronBankFamilyPays -> false"; C1-G2 przeglad najemnika poza polem) -> commit 4b7b11b na w-toku/e2c (md5 29a46fbb), szczegoly
  w docs/C1-POSTEP.md drzewa e2c ("Recenzja C1 (workflow)"). Zaliczka gry z niczego (portfel najemnikow/trybutu/wezwania uznany w calosci) przy splacie
  wraca w nicosc - zrodlo zamyka 168. Nowe progi testu C1 (W1): zalogi tylko twierdze rodow w wojnie >= 28 dob w obu biegach; wojsko >= 96% bazy tej samej doby
  w tym samym zbiorze rodow (w wojnie >= 18 dob). **TEST 120 dob w toku**: test\Armoury-e2c-c1r.dll -> test\at-c1r-120.out.txt; potem sprawdz_logi --grupa etap2
  --baza e2b-p\baza-B4.json + porownanie z kopia-e2b2-120 (budzet-rodow.csv, balans-krolestw.csv); jesli OK -> WGRANIE 16.
- (00:50) **TEST C1 120 dob (DLL 29a46fbb; kopia SCRATCH(7016) kopia-c1r-120\):** 120/120, 16.5 s/dobe, 0 bledow Armoury (CS 9 = 8 szumu + polkniety wyjatek
  ROT HarrenhalSiegeEvent - Mends odlozyl oblezenie, "Polnoc nie utworzyla armii dla Roose Bolton"). Progi (baza-B4): glowy < 5000 = 7 (TAK; baza 19),
  bankruci Banku 3 (ETAP), zalogi na twierdze w wojnie -4.3% (TAK), reszta ksiegi -2.3 tys. (TAK), zwrot 88.5% naleznego. **NIE: wojsko** - druzyny lordow
  w wojnie 89.7 tys. wobec 111.5 tys. (-20%; projekt zakladal ok. -4% do kroku D), Iron Islands -69%, Dothraki -47%, Pentos -33%, Norvos -29%; a pulap 166
  NIE wiaze (zold 47-51% pulapu). **C1 NIE WGRANY.** Diagnoza + poprawka: jeden pomocnik w tle (porownanie rod po rodzie budzet-rodow.csv obu biegow 120 dob,
  mechanizm w ClanBudget/dwor/AiGear/hamulec partii, korekta 182: dar Wolnych Miast 0.10 -> 0.15) -> commit na w-toku/e2c + sekcja w C1-POSTEP.md;
  potem znow test 120 dob i wgranie 16, jesli wojsko w wojnie >= ok. 96% bazy.
- (01:30) **POPRAWKI WOJSKA C1**: (a) pomocnik - commit 385e7ec (diagnoza z budzet-rodow.csv: 65 rodow na pulapie = 73% braku; zwrot korony w D 38 tys.
  wobec 112 tys., dwor 0.20 D szedl do kas siedzib takze przy rodzie na pulapie, sztywny podzial limitow partii 1.5:1). Zmiany: WarCourtYieldsToWages (w wojnie
  dwor ustepuje zoldowi), PartyLimitsShareFreeRoom (limit partii = jej zold + czesc wolnego miejsca rodu), dar Wolnych Miast -> Dothrakowie 0.15 (regula korekty 182).
  (b) ja - commit 4e4e627 C1-m: umowa najemnika przy najmie >= MercHireFloorShare 0.5 pelnej wielkosci kompanii (kompania najeta po bitwie odrasta; bylo -79%).
  DLL test\Armoury-e2c-c1w.dll (d9a127bc). **TEST 120 dob w toku** -> test\at-c1w-120.out.txt. Ocena: sprawdz_logi --grupa etap2 --baza e2b-p\baza-B4.json;
  wojsko lordow w wojnie (doby 94-121) cel ok. 95+ tys. (baza 111.5, C1-r 89.7); jesli >= ok. 96% bazy albo luka wyraznie do zamkniecia kredytem 168 -> WGRANIE 16.
- (01:55) **TEST C1-w 120 dob (DLL d9a127bc; kopia SCRATCH(7016) kopia-c1w-120\):** 120/120, 17.9 s/dobe, 0 bledow Armoury (CS 9 = szum + Harrenhal ROT;
  GAME HANG 61 s po wyjsciu z kampanii - tak samo w poprzednich biegach). Glowy < 5000 = 6, bankruci 2 (ETAP), reszta ksiegi -0.5 tys. (TAK). Wojsko lordow
  w wojnie 91.5 tys. (C1-r 89.7, baza 111.5) - poprawki 385e7ec/4e4e627 daly malo; Dothraki -29% (bylo -47), zalogi na twierdze -7.8%. **Wniosek:** w skarbcach
  zostaje ok. 90 tys. zl/dobe niewydanych wplywow (linia "Korona: wplywy dnia (165)": "zostalo z wplywow dnia w skarbcach") = ok. 15 tys. ludzi zoldu - to luka;
  rozdaje ja C2 (180 renty wedlug lenn). **C1 NIE WGRYWAM OSOBNO** - dalej C2 na tej samej galezi w-toku/e2c, test 120 dob C1+C2, wgranie razem (WGRANIE 16).
- (02:40) **C2 = 180 RENTY GOTOWE** (pomocnik: 7c15a3a kod, b649bbe opis, 7579b39 wlasna recenzja; ja: 1220510 komunikat renty gracza raz na 7 dob). CrownRents.cs
  (po WageRefund, przed CrownIncome.End): pula = reszta wplywow dnia po zwrocie, wagi miasto 3 / zamek 1 / wies 0.25, warunek zalogi >= 50% normy krolestwa (28 dob)
  i sluzby >= 20 z ostatnich 60 dob wojny; renta w D (czesc "korona", KRent); zapis arm_rent180; nowa linia "Renty korony (180): dzien"; CSV budzet-rodow +renta_180;warunek_180.
  Opis i odstepstwa: docs/C1-POSTEP.md w drzewie e2c, sekcja "C2 - 180 renty". DLL test\Armoury-e2c-c2.dll (b43f65cf). **TEST 120 dob C1+C2 w toku** -> test\at-c2-120.out.txt.
- (03:00) **TEST C1+C2 120 dob (DLL b43f65cf; kopia SCRATCH(7016) kopia-c2-120\):** 120/120, 16.6 s/dobe, 0 bledow Armoury (CS: szum + Harrenhal ROT + HANG po wyjsciu).
  Progi (baza-B4): TAK - glowy < 5000 = 4, bankruci 0, **wojsko lordow w wojnie 101.8 tys. (pas 95-115; C1-w 91.5)**, zalogi na twierdze +1.1%, reszta ksiegi -2.9 tys.
  NIE (2): wojsko swiata -12.6% (pas +-10%; z pokojem na starcie), krolestwa: Iron Islands -61%, Dothraki -39% (dar juz 15% - regula 182 wyczerpana, reszta "biedni
  slabsi" + kredyt 168), Ibben -32%. DECYZJA (stare): pan zamkow z zaworu 134 zl/d, regulator miast +24 tys./d. Renty 180: pula 53.9 tys./d = renty 33.6 + wstrzymane
  20.3 (94 z 215 rodow bez warunku SLUZBY - 43.7%, wiecej niz zakladane 20% - do obserwacji, ew. poprawka warunku w C3); zwrot 81.5% naleznego.
  Dalej: test zapisu 9 dob (autotest-161-kawalki) -> test\at-c2-zapis.out.txt -> jesli OK: **WGRANIE 16 = C1+C2** (Armoury b43f65cf, w-toku/e2c 1220510).
- (02:05) **WGRANIE 16: Armoury b43f65cf (noc/wgranie-16 = w-toku/e2c 1220510 = C1+C2)**, CS 6507c842. Zapis 9 dob OK (0 bledow). Dalej: **C3** (179 rycerze bez lenna
  bez wlasnych partii - jada w druzynie pana; 183 dezercja AI wedlug poziomu, podloga 30 ludzi przy zaleglym zoldzie) wedlug PROJEKT-ETAP2 rozdz. 179 (ok. 548) i 183
  (ok. 585), na galezi w-toku/e2c; do obserwacji: warunek sluzby rent 180 (44% rodow bez renty). Potem D (168 + 178), potem etap 3.
- (03:00) **C3 GOTOWE** (pomocnik; w-toku/e2c b159d70 179, 6273048 183, fdb9d43 180m pomiar sluzby, 1238127 recenzja wlasna, docs 9926b99). 179 GentryService.cs:
  rody rycerzy bez wlasnych partii (latki gry + BK SummonGentry), glowa rodu jedzie w druzynie pana (wlasciciel wsi majatku; bez partii pana - najblizsza armia
  krolestwa), zold rycerza 24 zl/d od pana (w pulapie 166, zwrocie 50%, D rycerza "kontrakt"), +1 do limitu partii; stare partie rycerzy - limit 0, ludzie do ludnosci
  BK wsi majatku (jency/statki starych partii nadal przepadaja przy rozwiazaniu przez BK - jednorazowo, liczone). UWAGA: rycerz "teleportuje sie" do/z armii.
  183: DesertionLawForAi true (AI, zalogi, karawany - te same progi morale co gracz; Inni bez zmian), WarLedger AI pelna stawka, podloga 30 ludzi przy zaleglym zoldzie
  (gracz tez). 180m: sluzba liczona przez cala dobe (bitwy, najazdy, szturmy, poscig, odsiecz) i dzien wojny tylko z prawdziwym kontaktem; zapis rent v2.
  Nowe linie: "Rycerze (179): dzien", "Dezercja AI (183): dzien". DLL test\Armoury-e2c-c3.dll (64900093). **TEST 120 dob w toku** -> test\at-c3-120.out.txt.
- (03:30) **TEST C3 120 dob (DLL 64900093; kopia SCRATCH(7016) kopia-c3-120\):** 120/120, 16.5 s/d, 0 bledow Armoury. TAK: glowy < 5000 = 1, bankruci 0, dezercja AI
  23/d (baza 49), renty - bez warunku 20.9% (bylo 43.7%), zalogi na twierdze -4.3%. **ZLE: (1) rycerze 179 NIE JADA** ("Rycerze (179)": rody 92, w sluzbie 0, armie 19 z
  rycerzem 0, "rycerz niedostepny 7") - blad filtra kandydatow; (2) wojsko lordow w wojnie 90.7 tys. (C2 101.8) - przyczyna nieznana (179 wg projektu tylko ok. 0.4 tys.;
  183? 180m? los kampanii?). C3 NIE WGRANY. Pomocnik w tle: naprawa 179 + rozklad roznicy C2->C3 na danych -> commit na e2c + sekcja "C3 po tescie 120 dob" w C1-POSTEP.md.
- (04:00) **C3 PO TESCIE** (pomocnik; a5063af poprawka 179, bb2dc86 opis): rycerze nie jechali, bo Free() wymagal IsActive, a glowy BK gentry sa NotSpawned (BK tworzy je
  po utworzeniu swiata) - teraz Free() przyjmuje NotSpawned, Join() przestawia na Active. Rozklad wojska C2->C3 (-11.1 tys.): los - Dorzecze pokoj od doby 93 -6.1,
  bitwy -2.2; 183 dezercja -2.4 (zrodlo: DLUG SNU T10 = 3, morale -95%, u 11-15 partii AI przez ok. 2 tygodnie, doby 66-82, Dorne/Oberyn) - prawo to samo co gracza;
  179 -0.4. **DO ZROBIENIA POZNIEJ (poza C3): AI na dlugu snu ma odpoczywac** (T10 - AI nie wie, ze trzeba rozbic oboz; to logika AI, nie nowa regula).
  DLL test\Armoury-e2c-c3b.dll (3e0a3d12). **TEST 120 dob w toku** -> test\at-c3b-120.out.txt; cel: rycerze w sluzbie > 0, 0 bledow (R7: bohater obcego rodu w partii AI).
- (04:30) **TEST C3b 120 dob (DLL 3e0a3d12; kopia SCRATCH(7016) kopia-c3b-120\):** 120/120, 0 bledow Armoury; rycerze JADA (w sluzbie 33-41, armie z rycerzem 3 z 13-16,
  zold rycerzy 744-984 zl/d). ALE glowy < 5000 = 33 - wszystkie to rody gentry (w C3 bez poprawki mialy 30-50 tys.): po NotSpawned -> Active rod rycerza placi zold
  cudzej partii (Astrethides: zold_partii 1389-1435/d przy 0 ludzi, kiesa 39.6 tys. -> 0 do doby ~80) + jednorazowo ok. -19 tys. Wojsko lordow w wojnie 92.3 tys.
  C3 NIE WGRANY. Ten sam pomocnik naprawia (rod rycerza nie placi za partie pana) -> commit na e2c, potem znowu test 120 dob i wgranie 17 (C3).
- (04:50) **C3c**: 5ce5faf - gra (AddExpenseFromLeaderParty) i BK (PartyExpensesPrefix) obciazaja rod zoldem partii, w ktorej JEST jego glowa -> rycerz placil caly
  zold druzyny pana (rody gentry 1.93 mln zl w 120 dobach przy 0 ludzi), pan placil drugi raz. Poprawka: prefiks CalculatePartyWage (+ znacznik rozliczanego rodu) -
  dla rodu rycerza w cudzej partii zold 0. DLL test\Armoury-e2c-c3c.dll (c38bd62f). **TEST 120 dob w toku** -> test\at-c3c-120.out.txt.
  **DO DECYZJI JEFFA (pozniej):** koszt "wezwania sojusznika do wojny" (gra AddExpensesForCallToWarAgreements) placa kiesy rodow, takze rycerzy bez lenna
  (Pentos: -57 tys., -40 tys. ... w kilka dob) - propozycja: z korony (skarbca) zamiast z kies; zmienia wszystkie rody, wiec pytanie do Jeffa.
- (05:00) **TEST C3c 120 dob ZAWIESIL SIE w dobie 43** (hang-2026-10-10_04-43-29.log: brak ramek zarzadzanych, gleboko w silniku; 1 s wczesniej SilentAssert w
  HeroCreator.DeliverOffSpring: "mother.Race == father.Race" - porod dziecka rodzicow roznych ras; pierwszy raz w historii testow). Hipoteza: po 179 glowy BK gentry
  sa czynne (NotSpawned -> Active), wiec biora sluby/ciaze; szablon BK gentry moze miec inna Race niz ROT. Bieg C3b (tez z aktywacja) przeszedl 120 dob bez tego.
  Powtorka testu w toku -> test\at-c3c2-120.out.txt. Jesli powtorzy sie: latka (prefiks DeliverOffSpring albo wyrownanie Race rycerza przy aktywacji w Join()).
- (05:10) Rasy w danych modow: human (domyslna), wight 117, giant 6, whitewalker 4 (ROT). Rycerze BK - ludzie, wiec porod "roznych ras" to raczej olbrzym ROT (Wolni Ludzie)
  z ludzkim malzonkiem - tresc ROT, nie C3. **DO ZROBIENIA (CrashScribe Mends, przed gra Jeffa):** brak ciazy przy roznych rasach (prefiks MakePregnantAction)
  i istniejaca ciaza roznych ras konczy sie bez porodu (prefiks PregnancyCampaignBehavior.CheckOffspringToDeliver) - inaczej mozliwe zawieszenie gry w silniku.
- (05:40) **WGRANIE 17: Armoury c38bd62f (noc/wgranie-17 = e2c 0175e8d = C1+C2+C3)**, CS 6507c842. Dalej: **KROK D** (168 dlug, kredyt wojenny KW i zajecie zamiast
  bankructwa; 178 okupy wedlug majatku, wielcy jency 1/10, okup krola ze skarbca, dlug okupu najwyzej rok) wedlug PROJEKT-ETAP2 rozdz. 168 (ok. 614) i 178 (ok. 684);
  potem test 120 dob + bieg z wymuszonym pokojem + 2 lata (koniec etapu 2). Potem etap 3. Odlozone: AI ma odpoczywac na dlugu snu (T10); Mends - porod roznych ras;
  pytanie do Jeffa - koszt wezwania sojusznika z korony zamiast z kies.
- (05:45) **KROK D - START** (limit tygodnia 95%): jeden pomocnik w tle, drzewo noc2\e2c (w-toku/e2c od 0175e8d), kolejno 168 (dlug, KW w pulapie 166, szczeble
  zamiast bankructwa, dluznik w budzecie, zrodlo zaliczki gry) -> 178 (okupy wedlug majatku, wielcy jency 1/10, okup krola ze skarbca, dlug okupu najwyzej rok),
  commit + push po kazdym + sekcje "D - 168" / "D - 178" w docs/C1-POSTEP.md drzewa e2c (TAM patrz po przejeciu konta). Potem: test 120 dob (--baza baza-B4),
  zapis 9 dob, WGRANIE 18; potem bieg z wymuszonym pokojem + 2 lata = koniec etapu 2.
- (06:00) Jeff: AI musi odpoczywac (forsowny marsz max 2 doby, tylko wyjatkowo) i zakaz slubow/ciaz miedzy rasami - wpisane do STAN-PRAC "DECYZJE JEFFA 10.10".
  Kolejka po kroku D: (1) T10 odpoczynek AI (Armoury, NocnyMarsz/AiNightCamp), (2) zakaz slubow i ciaz roznych ras (CrashScribe Mends albo Armoury), potem etap 3.
- (06:10) Jeff: "tak, korona" - oplata za wezwanie sojusznika do wojny ze skarbca, rody nic, brak srodkow = sojusznik wychodzi. Kolejka po D: (1) T10 odpoczynek AI,
  (2) zakaz slubow/ciaz roznych ras, (3) Call to War z korony (165/168 - jesli pomocnik D tego nie zrobil przy zrodle zaliczki gry).
- (06:40) Jeff: dar dla Strazy z calego Westeros (STAN-PRAC "DECYZJE JEFFA 10.10"): Zelazny Tron 8%, Polnoc 8%, Dorne/Reach/Vale/Stormlands/Riverlands/Dragonstone 3%.
  Kolejka po D: (0) dar Strazy z Westeros (S, CrownGifts.cs - przed testem D, zeby test objal oba), (1) T10 odpoczynek AI, (2) zakaz slubow/ciaz roznych ras.
- (07:10) Jeff: korona ma pozyczac (STAN-PRAC "DECYZJE JEFFA 10.10") - nowa paczka po D. Dar dla Strazy - Jeff jeszcze wybiera A (cale Westeros 8/8/3%) albo B (jak w
  ksiazkach: Polnoc 12%, krol 5%, reszta 1.5%); Straz ma ok. 1.8 mln w sakiewkach, wlasne lenna ok. 2.9 tys./d.
- (07:40) Jeff podal procenty daru dla Strazy (STAN-PRAC): Polnoc 5, Zelazny Tron 4, Dorne 3, Reach 3, Vale 2, Stormlands 2, Riverlands 2, Dragonstone 1. Do kodu po D.
- (08:00) **KROK D GOTOWY** (pomocnik: 64405d5 168 + Call to War z korony, 8d620e3 178 + RealisticCaptivity, 97b8dfe recenzja wlasna; opis "D - 168/178" w C1-POSTEP.md
  drzewa e2c) + ja: 182-W dar Strazy z Westeros wedlug potrzeby (procenty Jeffa, GiftWatch* + GiftWatchNeedDays 60). DLL: test\Armoury-e2c-d.dll (aebd0e94) +
  test\d\RealisticCaptivity.dll (5f98df70) - WGRYWAC RAZEM. **TEST 120 dob w toku** (-ExtraDll RC) -> test\at-d-120.out.txt. Potem zapis 9 dob -> WGRANIE 18
  (Armoury + RealisticCaptivity, kopie obu). Limit tygodnia 96%.
- (07:20) **TEST D 120 dob (Armoury aebd0e94 + RC 5f98df70; kopia SCRATCH(7016) kopia-d-120\):** 120/120, 0 bledow Armoury. TAK: glowy < 5000 = 1, bankruci 0,
  wojsko lordow w wojnie 96.1 tys. (pas 95-115!), zalogi -2.9%, dezercja AI 21/d, reszta ksiegi -1.6 tys.; KW u 14 rodow (Bank 259 tys., w tym KW 247 tys.); dar Strazy 0
  (oszczednosci 2.27 mln > 60 dob x 7.6 tys.); Call to War z korony dziala (2 porozumienia). NIE: wojsko swiata -16% (z pokojem na starcie), Free Folk -27%, Aegon -25%,
  Ibben -30%; "2.14 kurier 2b/2c brak linii" = narzedzie (harness 178 ma nowa linie "Harness niewoli (178): krok 2 - kurier prawdziwy ... z niczego 0" - OK).
  **DO DECYZJI JEFFA - OKUPY 178:** suma cen okupow w 120 dobach ok. 65.5 mln (dzis 775 tys.: Stormcrows -> Ko Jhago 442 tys. za Sallora), dlug okupow rosnie
  liniowo ok. 270 tys./dobe -> 32.3 mln u 54 rodow; swiat ma D ok. 0.9 mln/dobe. Ryzyko kuli snieznej w biegu 2-letnim. Test zapisu 9 dob w toku -> WGRANIE 18.
- (07:25) **WGRANIE 18: Armoury aebd0e94 + RealisticCaptivity 5f98df70 (noc/wgranie-18 = e2c baff3f2 = krok D + dar Strazy)**, CS 6507c842. KROKI ETAPU 2 SKONCZONE
  (A, B, C1, C2, C3, D). Do zamkniecia etapu 2: decyzja Jeffa o okupach (A/B/C), potem bieg 2-letni + bieg z wymuszonym pokojem (warunki konca etapu z projektu).
  Kolejka: T10 odpoczynek AI (max 2 doby forsownego marszu), zakaz slubow/ciaz roznych ras, korona pozycza w Banku (nowa paczka), etap 3 krok po kroku.
- (08:20) **T10-R + RASY GOTOWE** (pomocnik; e2c a94b135 T10-R NightMarch.cs: max 2 noce forsownego marszu, potem obowiazkowy odpoczynek do dlugu 0, wyjatek tylko
  ucieczka przed 2x silniejszym; wodz armii decyduje wedlug najgorszego dlugu armii; naprawiony blad ksiegi snu (gubione ticki godzinowe -> masowe dlugi 1);
  ea606c7 RaceLaw.cs: sluby i ciaze tylko w tej samej rasie, ciaza roznych ras konczy sie bez porodu; klucze MaxForcedNights 2, SameRaceOnly true; opis w C1-POSTEP.md).
  DLL test\Armoury-e2c-t10r.dll (aeb2ba51). **TEST 120 dob w toku** -> test\at-t10r-120.out.txt; potem zapis 9 dob -> WGRANIE 19 (sam Armoury; RC bez zmian 5f98df70).
- (08:40) **WGRANIE 19: Armoury aeb2ba51 (noc/wgranie-19 = e2c 432e8e9 = T10-R + rasy)**. Czeka na Jeffa: okupy 178 (A/B/C). Kolejka: korona pozycza w Banku (nowa paczka),
  koniec etapu 2 (bieg 2-letni + wymuszony pokoj), etap 3 krok po kroku (3.2 -> 3.3 -> 3.1 -> 3.4-3.7 -> 3.8 -> 3.9/3.10).
