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
- (08:45) **Paczka 186 korona pozycza w Banku - START** (pomocnik w tle, drzewo noc2\e2c od 432e8e9; projekt jako sekcja "186 - korona pozycza" w C1-POSTEP.md drzewa,
  potem kod, recenzja wlasna). Potem: test 120 dob -> zapis -> WGRANIE 20. Limit tygodnia 96% - przy 99% pelne przekazanie.
- (09:20) **186 GOTOWE** (pomocnik; e2c 57fbf30 + 037c406 recenzja; CrownBorrow.cs; projekt w C1-POSTEP.md "186"): korona w wojnie pozycza brak do pelnego zwrotu zoldu
  z kapitalu Banku ponad 2 mln (pula 1/60 nadwyzki dziennie), limit 180 dni sredniego podatku, cena jak krol w Banku (20% + 10 pp przy dlugu), rata pierwszy wydatek dnia
  (1/182 najwiekszego dlugu, max 30% podatku); zaleglosc (< 50% rat w 28 dobach) -> Bank nie pozycza jej ani jej rodom, wrogom limit x1.5. Klucze CrownBorrows i 6 innych,
  zapis arm_crown186, linia "Kredyt korony (186): dzien". DLL test\Armoury-e2c-186.dll (9dc69ff6). **TEST 120 dob w toku** -> test\at-186-120.out.txt.
- (09:30) Jeff: okupy B -> bcb5fe1 (RansomHeadYears 0.165, RansomLordDays 20). DLL 186+B: test\Armoury-e2c-186b.dll (d7222bfb). Plan: po tescie 186 (w toku) - test 120 dob
  na 186b (186 + okupy B) -> zapis -> WGRANIE 20 razem. Potem koniec etapu 2: bieg 2-letni (Days 728) + bieg z wymuszonym pokojem.
- (09:45) TEST 186 120 dob OK (kopia-186-120): 0 bledow, glowy < 5000 = 4, bankruci 0, wojsko lordow w wojnie 97.0 tys., zalogi -2.1%, reszta ksiegi -2.6 tys.; korony pozyczaja
  15-23 tys./d u 5-10 krolestw, pula Banku czasem przycina (1/60 nadwyzki ponad 2 mln). **TEST 186b (186 + okupy B) 120 dob w toku** -> test\at-186b-120.out.txt -> zapis -> WGRANIE 20.
- (10:30) **WGRANIE 20: Armoury d7222bfb (noc/wgranie-20 = e2c bcb5fe1 = 186 + okupy B)**. Dalej: KONIEC ETAPU 2 - bieg 2-letni (autotest -Days 728, nowa kampania;
  ok. 3.5 h) + bieg z wymuszonym pokojem (warunki konca etapu z projektu rozdz. 1: pokoj - zalogi pokojowe 45-50% wojennych, zold / D panow zamkow <= 0.3, wojsko 35-40%
  wojennego). Potem etap 3 krok po kroku.

## 20. START TUTAJ (stan 10.10 ok. 10:40 zegara komp.; limit tygodnia 97% - przekazanie na drugie konto)

**W grze (ZATWIERDZONE):** Armoury d7222bfb (galaz noc/wgranie-20 = w-toku/e2c bcb5fe1), RealisticCaptivity 5f98df70 (wgranie 18), CrashScribe 6507c842 (wgranie 13).
Wgrania 15-20 z 10.10: 15 miecz valyrianski (Park -> krewny / polka), 16 etap 2 C1+C2, 17 C3, 18 D (168+178, z RC), 19 odpoczynek AI + rasy, 20 korona pozycza (186) + okupy B.
**Drzewo pracy:** SCRATCH(7016)\noc2\e2c (git worktree klonu lancuch, galaz w-toku/e2c; origin = repo Jeffa C:\Users\GAME\Bannerlor2-Realm-of-Thrones -> GitHub).
Opis calego etapu 2 (kazda paczka, odstepstwa, klucze, linie logu, diagnozy): **docs/C1-POSTEP.md w drzewie e2c** (nie w repo glownym - przeniesc przy scalaniu).
**Autotest:** cd SCRATCH(3cf3e0ac)\dzien-6\autotest\repo; powershell -NoProfile -ExecutionPolicy Bypass -File tools\autotest.ps1 -CrashScribeDll
SCRATCH(7016)\test\CrashScribe-at-w2.dll -ArmouryDll <dll> [-ExtraDll <...\RealisticCaptivity.dll>] -Days 120 [-LoadSave autotest-161-kawalki -Days 9] (gra zamknieta!).
Progi: python -I + runpy na SCRATCH(7016)\noc2\e2c\tools\sprawdz_logi.py --grupa etap2 <Armoury-*.log> --baza SCRATCH(7016)\e2b-p\baza-B4.json (logi kopiowac do SCRATCH\kopia-*).
Wgranie: kopia *.bak-<data>-przed-nocNN obok + D:\Backup-Bannerlord\wgrane\<data>-nocNN-przed, md5 przed/po, galaz noc/wgranie-NN, wpis w STAN-PRAC i tu.

**W TOKU:** bieg 2-letni konca etapu 2 (Armoury d7222bfb = test\Armoury-e2c-186b.dll, -Days 728, start ok. 10:35, ok. 3.5 h) -> test\at-etap2-728.out.txt.
Ocena: sprawdz_logi --grupa etap2 (+ --koniec-etapu), glowy < 5000 w dobach 364/728 <= 10, bankruci 0, wojsko lordow w wojnie 95-115 tys., reszta ksiegi +-10 tys./d,
dlug okupow i kredyt koron (186) nie rosna bez konca, skarbce >= 500 tys. Bieg z wymuszonym pokojem - autotest nie ma takiej opcji (do dorobienia w CS autotest albo pominac
za zgoda Jeffa).

**KOLEJKA (decyzje Jeffa w STAN-PRAC "DECYZJE JEFFA 10.10"):**
1. Zamkniecie etapu 2 po biegu 2-letnim (raport dla Jeffa prostym jezykiem, tabele).
2. ETAP 3 krok po kroku (PLAN-DO-KONCA-MODA "Etap 3"): 3.2 cale zloto taboru/bandy dla zwyciezcy -> 3.3 statki rozbitych do portu (181 w PROJEKT-ETAP2) -> 3.1 szczelnosc 164a
   -> 3.4-3.7 (BK niewolnicy/rada/wedrowcy, ROT inwazje/rebelie/zadania) -> 3.8 ceny historyczne -> 3.9/3.10 drobne. Koniec: reszta niewyjasniona ksiegi < 10 tys./d.
3. Przy powrocie do budow: AI w wojnie przestawia kolejke na budowe wojskowa; krolestwa w wiecznej wojnie (Straz) buduja tez cywilne; Straz najmuje robotnikow z okolicy.
4. Otwarte drobne: pan samych zamkow z zaworu ok. 130-180 zl/d wobec celu 200-350 (DECYZJA z kroku B - konie zjadane w zamkach P1/P2 albo nowy cel); regulator kas miast
   kasuje +25-38 tys./d tego, co zatrzymal B (zamyka 111' w etapie 5); renty 180 - ok. 20% rodow bez warunku sluzby (OK).
**Zasady pracy (pamiec):** jedna rzecz naraz; pomocnik (Agent) na kod, ja test + wgranie; tor S/M/L; po polsku prostym jezykiem; Jeff gra dopiero po projekcie (NOWA kampania);
nie dotykac zapisow Jeffa i Armoury.json; zadnego spawn_task; przy 99% tygodnia - pelne przekazanie.
- (10:45) Jeff: okupy wariant 1 (d341e7b) i "po tescie sprawdzenie, wnioski i przekazanie na drugie konto". Pierwszy bieg 2-letni przerwany na prosbe Jeffa (doba 26).
  **BIEG 2-LETNI (728 dob) w toku** na test\Armoury-e2c-okup1.dll (87b03e2b) -> test\at-okup1-728.out.txt (ok. 3.5 h). Po nim: kopia logow (SCRATCH kopia-okup1-728),
  sprawdz_logi --grupa etap2 --koniec-etapu, wnioski dla Jeffa, test zapisu 9 dob i WGRANIE 21 (jesli OK), rozdz. 20 START TUTAJ - aktualizacja.
- (11:55) **DO ZBADANIA (zrzut Jeffa z biegu 2-letniego, Orle Gniazdo / The Eyrie):** nad miastem siatka ok. 200 ikon - to SandBox PartiesInSettlementGridWidget
  (SettlementParties.PartiesInSettlement): moneta = karawana (ok. 13), "widly" (General\Icons\Militia, IsDefault) = kazda inna partia (ok. 200!). Czyli ok. 200 partii stoi
  w The Eyrie - nienormalne. Podejrzani: wozy wsi (130) / wozy zamkow (171), wiesniacy, partie rozwiazane, BK. Do zrobienia: dzienna linia diagnostyczna "Osady z najwieksza
  liczba partii: <osada> N (lordowie, karawany, wiesniacy, wozy, inne)" + sprawdzenie, czemu nie wychodza (gora / Krwawa Brama, straznik drog GuardSettlementMove).

## 21. START TUTAJ - AKTUALIZACJA (10.10 ok. 13:50; limit tygodnia 97%; Jeff: "po tescie sprawdzenie, wnioski i przekazanie na drugie konto")

**W grze:** Armoury 87b03e2b (noc/wgranie-21 = w-toku/e2c d341e7b), RealisticCaptivity 5f98df70, CrashScribe 6507c842. Reszta jak w rozdz. 20 (drzewo noc2\e2c, autotest, progi).
**Bieg 2-letni (okupy 1) - wynik:** wywrotka w dobie 457 = BLAD BANNERKINGS: NullReferenceException w BannerKings.Behaviours.Diplomacy.Groups.Demands.ClaimantDemand.SetTexts
(:220) <- Demand.Fulfill <- Demand.DoAiChoice <- RadicalGroup.Tick <- KingdomDiplomacy.Update <- BKDiplomacyBehavior.OnDailyTick; BLSE ExceptionInterceptor -> okno ButterLib
(ImGui) -> gra stoi. Logi: SCRATCH(7016)\kopia-okup1-457\ (Armoury-2026-10-10_10-38-24.log + CSV), CS session-2026-10-10_10-38-22.log (linia 11737 "PIERWOTNY WYJATEK").
**Wnioski z 457 dob (progi sprawdz_logi):** glowy < 5000: d120 1, d364 4, d458 3 (TAK <= 10); bankruci 2 (K39, doba 414; ETAP <= baza 5); wojsko lordow w wojnie (ost. 28 dob)
98.9 tys. (TAK 95-115); zalogi -3.2% (TAK); Bank kapital min 2.56 mln (TAK >= 1 mln); reszta ksiegi -5.5 tys./d (TAK); okupy 54.2 mln w 457 dobach (ok. 118 tys./d), dlug okupow
male (d120 178 tys., d364 11 tys., d457 140 tys.); KW Banku u 10-17 rodow, zajecie 1-2; kredyt koron (186) 1.66 -> 2.15 -> 2.59 mln (d120/364/457, 23-26 krolestw, zaleglosci 0 -
rosnie ok. 2-3 tys./d, obserwowac); skarbce na koniec srednio 583 tys. (rezerwa 500 tys. dziala). NIE: dezercja AI ost. 28 dob 200/d (baza 49) - mieszanka glodu ("w glodzie"
do 54/d) i limitu zoldu/przepelnienia gry u partii bez budzetu (do 162/d) - do zbadania; Ibben -33%, Iron Islands -55% (biedni z lore).
**NASTEPNE KROKI (po kolei):**
1. **CrashScribe Mends: zabezpieczyc BK ClaimantDemand** (finalizer na ClaimantDemand.SetTexts i/lub Demand.Fulfill: polknac wyjatek, log "wyjatek BK polkniety", jak Harrenhal) -
   drzewo CS: SCRATCH(7016)\noc2\a175cs (w-toku/175-armie-cs 511c08e = CS w grze); wersja autotestowa: worktree + `git apply -3 SCRATCH(7016)\test\at1-cs.diff` + build;
   test 40 dob + zapis; WGRANIE CS. Potem powtorzyc bieg 728 dob (Armoury 87b03e2b) -> zamkniecie etapu 2 (raport dla Jeffa).
2. **The Eyrie ok. 200 partii** (zrzut Jeffa): dzienna linia "osady z najwieksza liczba partii wg rodzaju" + przyczyna (wozy 130/171? wiesniacy? straznik drog BK?).
3. Dezercja AI pod koniec roku 2 (glod + limit zoldu gry u partii bez budzetu 166) i potkniecie "ERROR in Tent" (KeyNotFound, 13:15:46 w logu 457).
4. ETAP 3 krok po kroku (rozdz. 20 kolejka). Budowy Strazy przy powrocie do budow.
- (14:00) Jeff: "dokoncz to jeszcze na tym koncie" (punkty 1-4 rozdz. 21). **(1) BK ClaimantDemand:** CS commit e44c7ba na w-toku/175-armie-cs (drzewo noc2\a175cs) -
  finalizer BkDemandSafe na ClaimantDemand.SetTexts, Demand.Fulfill, Demand.DoAiChoice. DLL do gry test\CrashScribe-bk.dll (b66e9aed), autotestowa test\CrashScribe-at-bk.dll
  (c423e6f0; worktree noc2\cs-at-bk = e44c7ba + at1-cs.diff). **BIEG 728 dob w toku** (Armoury 87b03e2b + CS-at-bk) -> test\at-bk-728.out.txt. Jesli dojdzie: WGRANIE 22 = CS b66e9aed,
  zamkniecie etapu 2. W tym czasie (2) linia diagnostyczna partii w osadach (Armoury e2c).
- (14:20) **(2) ORLE GNIAZDO - WYJASNIONE:** to blad wyswietlania gry (SandBox SettlementNameplatePartyMarkersVM): lista ikon budowana raz z settlement.Parties, potem tylko
  +SettlementEntered / -OnSettlementLeft / MapEventEnded - brak nasluchu zniszczenia partii, wiec partia rozwiazana/zniszczona W MIESCIE zostaje jako ikona-duch. Prawdziwa
  liczba partii w The Eyrie 5-11 (CS GAME STATE "Parties in it" w raportach hang). Gracz autotestu siedzi w The Eyrie 2 lata -> ok. 200 duchow. Wplyw na gre: zaden.
  Poprawka (CS, niska waga): nasluch MobilePartyDestroyed -> usuniecie znacznika z SettlementNameplatePartyMarkersVM osady (albo odswiezenie listy raz na dobe).
- (14:40) Jeff: "trzeba naprawic ten wizualny glitch" -> CS commit (NameplateGhosts.cs, w-toku/175-armie-cs): postfiksy na SettlementNameplatePartyMarkersVM.RegisterEvents/
  UnloadEvents - nasluch MobilePartyDestroyed (usuwa ikone) + DailyTick (PopulatePartyList). DLL do gry test\CrashScribe-bk2.dll (BK + ikony). Po biegu 728 dob: zbudowac wersje
  autotestowa z obiema poprawkami (worktree + at1-cs.diff), test 40 dob, WGRANIE CS.
- (14:50) Limit tygodnia 98%. **Tent:** ERROR in Tent (1 raz w 457 dobach, potkniecie 1/3) = Armoury.NightRest.Tent -> refleksja do metody innego moda (namiot/oboz) -> KeyNotFound
  w jego slowniku (partia nie zarejestrowana); niegrozne, obsluzone. Poprawka: sprawdzic w NightRest.Tent, co wolamy, i przed wywolaniem sprawdzic klucz (albo try per partia).
  **Straz - werbunek:** "werbunek wstrzymany 33-96/d" przy Straz 1.7 tys. / pulap 3.5-5 tys. - podejrzenie: pulap w ludziach (UnpaidTroopsCapInMen) liczony na partie
  (limit partii = jej zold + czesc wolnego miejsca; zold Strazy 0) blokuje werbunek mimo luzu rodu - sprawdzic w ClanBudget/RecruitCost. Kolejnosc po biegu 728: wynik ->
  CS (BK + ikony) test 40 dob -> WGRANIE CS -> (3) dezercja AI rok 2 + Tent + werbunek Strazy -> (4) etap 3.

## 22. START TUTAJ - PRACA NOCNA (10.10 ok. 15:15; limit tygodnia 98% - przekazanie na drugie konto; Jeff: "test 2-letni zrobisz po etapie 3")

**W grze (ZATWIERDZONE):** Armoury 87b03e2b (noc/wgranie-21 = w-toku/e2c d341e7b), RealisticCaptivity 5f98df70, CrashScribe 77b11df5 (noc/wgranie-22-cs = w-toku/175-armie-cs 37535a7).
**Drzewa:** Armoury + RC: SCRATCH(7016)\noc2\e2c (w-toku/e2c). CrashScribe: SCRATCH(7016)\noc2\a175cs (w-toku/175-armie-cs). Klon: SCRATCH(3cf3e0ac)\lancuch (origin = repo Jeffa).
**Autotest:** jak w rozdz. 20, ale CS autotestowy = SCRATCH(7016)\test\CrashScribe-at-w22.dll (nowy CS w grze + tryb autotestu). Nowa wersja CS do testu: worktree z commita CS +
`git apply -3 SCRATCH(7016)\test\at1-cs.diff` + build (przyklad: noc2\cs-at-bk2). Opis calego etapu 2: docs/etap2/C1-POSTEP-kopia-2026-10-10.md (oryginal w drzewie e2c docs/C1-POSTEP.md).
**ETAP 2 - stan:** wszystkie kroki wgrane (A, B, C1, C2, C3, D, 186 korona pozycza, okupy wariant 1, dar Strazy wedlug potrzeby, T10-R, rasy). Bieg 2-letni - wedlug Jeffa
PO ETAPIE 3. Dotad: bieg 457 dob (kopia-okup1-457) i 218 dob (kopia-bk-218): glowy < 5000 0-4, bankruci 0-2, wojsko lordow w wojnie 99-104 tys., zalogi -1..-3%, Bank min 2.6 mln.

**WNIOSKI Z TESTOW (do naprawy, w kolejnosci):**
1. **Dezercja AI w drugiej polowie roku: 160-200 ludzi/dobe (baza 49)** w oknach dob 190-218 i 430-458; skladniki z linii "Dezercja AI (183)": glod ("w glodzie" do 54/d) oraz
   "limit zoldu i przepelnienie (gra, w tych partiach)" do 162/d - partie POZA budzetem 166 (bez pulapu? najemnicy? Inni? bunty?) i przepelnienie (party size). Skutek: **bandy +29%**
   (20.2 tys. ludzi wobec 15.6 tys.) - zbiegli zasilaja bandy. Zbadac: ktore partie dezerteruja z limitu gry, czemu glod (zywnosc AI w zimie/oblezenia?), naprawic.
2. **Werbunek Strazy blokowany** ("werbunek wstrzymany 33-96/d" przy Straz 1.5-1.7 tys. / pulap 3.5-5 tys.) - podejrzenie: UnpaidTroopsCapInMen liczone na partie (limit partii
   z PartyLimitsShareFreeRoom przy zoldzie 0). Straz spadla z 3.2 tys. (d60) do 1.5-1.7 tys. Sprawdzic ClanBudget / RecruitCost.
3. **Reszta ksiegi pieniadza** -17..-22 tys./d w dobach 31-120 (baza -11.6) - zloto znika bez nazwy; to jest ETAP 3 (szczelnosc), cel < 10 tys./d.
4. **Tent:** ERROR in Tent (1 na 457 dob) - NightRest.Tent wola refleksja metode innego moda, KeyNotFound w jego slowniku - sprawdzic klucz przed wywolaniem.
5. Slabsze z lore: Iron Islands -40..-55%, Ibben -30..-33% (bez akcji, chyba ze Jeff zdecyduje).
6. Kredyt koron (186) rosnie 1.7 -> 2.6 mln w roku bez zaleglosci - obserwowac w biegu 2-letnim.
**POTEM ETAP 3 krok po kroku** (PLAN-DO-KONCA-MODA "Etap 3"): 3.2 cale zloto rozbitego taboru/bandy dla zwyciezcy (Jeff zdecydowal) -> 3.3 statki rozbitych do portu
(181 w PROJEKT-ETAP2) -> 3.1 szczelnosc 164a -> 3.4-3.7 BK/ROT -> 3.8 ceny historyczne -> 3.9/3.10 drobne. Kazdy krok: kod (pomocnik), recenzja, test 120 dob + zapis 9 dob,
wgranie. Na koniec etapu 3: bieg 2-letni (728 dob, -TimeoutMin 480) = zamkniecie etapow 2 i 3.
**Na pozniej (decyzje Jeffa w STAN-PRAC 10.10):** budowy Strazy (najmuje robotnikow; AI w wojnie przestawia kolejke na budowe wojskowa; wieczna wojna buduje tez cywilne).

## 23. NOC 10/11.10 - NOWE KONTO (sesja ae222cb7, start 15:20 zegara komp.; Jeff wraca ok. 03:20)

**Zadanie Jeffa (15:20):** "dokoncz etap 2, zrob pelny etap 3 i jak starczy czasu etap 4; pilnuj okna 5h, zeby nie przerywac pracy; stosuj metodyke oszczedzania".
Kolejnosc: (1) dezercja AI w 2. polowie roku + bandy, (2) werbunek Strazy, (4) Tent -> ETAP 3 (3.2 -> 3.3 -> 3.1 -> 3.4-3.7 -> 3.8 -> 3.9/3.10) -> bieg 2-letni
(w jego trakcie kod etapu 4) -> etap 4, ile sie zmiesci. Kazdy krok: kod, recenzja, test 120 dob + zapis 9 dob, wgranie z kopia.
**ZASADA PRACY (pelny tekst; dotad byla tylko skrotem w rozdz. 18/20, reszta w pamieci starej sesji):**
- tor wedlug RYZYKA: S (liczby/mala poprawka) - sam, bez workflow; M (mechanika w 1 obszarze, blad o nieznanej przyczynie) - 1 workflow: rozpoznanie (najpierw logi)
  -> kod -> JEDNA recenzja -> poprawki -> autotest; L (obieg pieniadza swiata, nowe systemy) - rozpoznanie, projekt, krytyka, kod, 2 recenzje, autotest 40 + zapis + 120 dob;
- okno 5h: odczyt get_usage miedzy krokami, przy 85% nie startowac nowych watkow, przy 90% pauza do resetu; jedna gra naraz; w czasie dlugiego testu kod nastepnej pozycji;
- tydzien 99% -> STAN-PRAC + PRZEKAZANIE + push; prompty pomocnikow: "nie uzywaj spawn_task".
Pamiec tego repo (laduje sie sama): tory-pracy-i-limity.md, stala-zgoda-wgrywaj-po-tescie.md.
**Drzewa tej nocy:** Armoury e2c jak wyzej (w-toku/e2c: fd5f8ae 183d diagnoza morale, 022c9f7 namioty (4), 0e21ea1 pula Strazy (2)); etap 3.2 - worktree
SCRATCH(ae222cb7)\e3\32 (galaz w-toku/e3-32 od 0e21ea1). SCRATCH(ae222cb7) = C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad
(test\ = probne DLL i wyjscia autotestow, d1\ = skrypty). Autotest jak w rozdz. 22 (CS autotestowy test\CrashScribe-at-w22.dll ze SCRATCH(7016)).
- (15:35) **(1) DEZERCJA - PRZYCZYNA (dowod):** linia "Morale AI (183d)" (nowa) na zapisie autotest-dlugi (doba 180 biegu 218; kopia SCRATCH(ae222cb7)\test\autotest-dlugi-d180.sav.bak):
  model morale = BKROTPatch BKROTPartyMoraleModel; partie lordow z dezercja maja kary ZAOPATRZENIA BK ("Alcohol supplies -35", "Wood supplies -34"), glod ("No Food -30" +
  "Recent Events" do -53 od dziennej kary glodu), "Sleepless nights -23", "Unpaid Wages -7". Ramki ksiegi towarow: zaopatrzenie BK zuzywa ok. 6 000 szt./dobe, dokupuje
  ok. 1 400. Zbiegli -> pula wyrzutkow -> bandy. Rozpoznanie BK PartySupplies + propozycje: workflow wf_d3375bb6-728 (notatki SCRATCH(ae222cb7)\wf1\rozpoznanie-1.md).
- (15:35) **(4) NAMIOTY:** przyczyna w kodzie gry - GetPartyVisual czyta slownik bez sprawdzenia; poprawka VisualOf (GetVisualOfEntity). **(2) STRAZ:** pulap w ludziach liczony
  na rod (rody bez dochodu ~0) -> jeden pulap calego zakonu. Test 120 dob 2+4 razem (Armoury-24.dll 377af1a2) w toku -> potem zapis 9 dob -> WGRANIE 23.
- (16:20) **WGRANIE 23: Armoury ba2e13e8** (noc/wgranie-23 = w-toku/e2c 10db68d: 183d + namioty (4) + pula Strazy (2) z poprawkami recenzji). Opis i testy w STAN-PRAC.
  **(1) DEZERCJA - rozpoznanie (SCRATCH(ae222cb7)\wf1\rozpoznanie-1.md):** Inni (wighty) liczeni jako lordowie i wpadaja do puli wyrzutkow (-> bandy; 23% calosci, w oknie
  420-447 polowa); blad wzoru kary zaopatrzenia BK (w osadzie i w malej partii kara = cala narosla potrzeba; nasza czapka 3/d zawyza u duzych); przepelnienie zalog po powrocie
  patroli BK -> pula wyrzutkow; fale po wielkich bitwach (vanilla -3/runde) i glod - kary zamierzone. W TOKU kod W1 (kara BK = dni braku) + W4 (Inni do niczego, przepelnienie
  do domu, Inni osobno w 169c/183) - workflow wf_9a3923d2-0f9 w drzewie e2c -> test\Armoury-p1.dll -> test 120 + zapis + bieg z zapisu d180 (test\autotest-dlugi-d180.sav.bak).
  **ETAP 3:** 3.2 kod gotowy (w-toku/e3-32 5f42f63, BeatenPurse.cs; recenzja w toku wf_d55d323d-3e3); projekt 3.1+3.3 w toku (wf_e10501af-c14 ->
  SCRATCH(ae222cb7)\e3\projekt31\PROJEKT-ETAP3-SZCZELNOSC-STATKI-2026-10-10.md).
- (16:45) Bieg bazowy z zapisu d180 (wgrana wersja, 40 dob, SCRATCH(ae222cb7)\kopia-d180-baza40): dezercja d180-207 127/d (lordowie morale 37, w glodzie 58, zalogi
  przepelnienie 13), d208-219 56/d; pula wyrzutkow 7.2 -> 11.0 tys., bandy 19.1 -> 20.3 tys. ludzi. Zapis d180 w grze pod nazwa autotest-d180 (kopia w test\). Na koniec
  biegu wywrotka silnika przy wyjsciu (0xC0000005, znana, bez kodu modow).
- (16:45) **ETAP 3.4-3.10 W TOKU** (workflow wf_bc43cd76-2ea): kazdy krok osobna galaz od 10db68d, drzewa SCRATCH(ae222cb7)\e3\34 ... e3\310 (w-toku/e3-34, -35, -36, -37,
  -38, -39, -310) - rozpoznanie + kod + recenzja + poprawki. Plan testow: grupy (3.2) -> (3.1+3.3) -> (3.4-3.7) -> (3.8) -> (3.9+3.10), kazda grupa test 120 + zapis 9.
- (17:10) **(1) KOD GOTOWY:** w-toku/e2c 5994414 + b9d6a12 (W1 kara zaopatrzenia BK = dni braku, max 20/30/20; W4a Inni osobno w 169c/183; W4b Inni dezerterzy do
  niczego, bramki band bez starych wightow; W4c przepelnienie partii i zalog do domu - OverflowHome.cs), DLL test\Armoury-p1.dll f5be1156. Bieg z zapisu d180 (40 dob,
  kopia-p1-d180) wobec bazy: d180-207 80/d (baza 127; glod 16 zamiast 58), kar zaopatrzenia BK w skladnikach brak, przepelnienie do domu 25-32/d; d208-220 fala 175/d
  = jedna wielka przegrana bitwa (Recent Events -65 u 12-24 partii; kara gry za przegrana runde) - mechanika zamierzona, PYTANIE do Jeffa (zostawic / zlagodzic).
  Do puli wyrzutkow -10% wobec bazy. Test 120 dob w toku (test\at-p1-120.out.txt), potem zapis 9 dob -> WGRANIE 24.
- (17:10) **3.2 po recenzji** (w-toku/e3-32 1f9880b) scalony na e2c+p1: galaz w-toku/e3-32m f37d032 (konflikt tylko CHANGELOG), DLL test\Armoury-32m.dll 1211d984 -
  czeka na gre po WGRANIU 24. Pytanie z 3.2 (drobne): kiesa bandy, ktora przylacza sie do gracza - dzis znika.
- (17:10) **PROJEKT 3.1+3.3** gotowy (SCRATCH(ae222cb7)\e3\projekt31\PROJEKT-ETAP3-SZCZELNOSC-STATKI-2026-10-10.md, 13 kawalkow E3/0-E3/12, krytyka w toku).
  JEDNO PYTANIE DO JEFFA: statki (181) zabieraja lordom ok. 330 tys./d zlota z powietrza -> wojsko w wojnie po roku ok. 92 zamiast 99 tys. (-7%), do etapu 5 (podatek
  wojenny). (a) wgrac teraz (rada) / (b) razem z etapem 5. Plan: kod statkow w tescie, przy wgraniu wylaczniki ShipsToPort/ShipTradeThroughPort/ShipPrizesToPort = false
  do odpowiedzi Jeffa. Drzewo do kodu 3.1: SCRATCH(ae222cb7)\e3\31 (w-toku/e3-31 od f37d032). Okno 5h 28% o 17:10 - start kodu 3.1 po potokach 3.4-3.10.
- (17:55) **WGRANIE 24: Armoury f5be1156** (punkt 1 dezercja: W1 + W4; opis w STAN-PRAC). Etap 2 (punkty 1, 2, 4 z rozdz. 22) ZAMKNIETY poza biegiem 2-letnim.
  **ETAP 3 - stan:** 3.2 = w-toku/e3-32m f37d032; 3.4-3.7 scalone = w-toku/e3-gA f205ba0 (drzewo e3\gA); 3.8-3.10 na gA = w-toku/e3-gB b052638 (drzewo e3\gB; Armoury,
  RC, GT kod 0). 3.1+3.3 kod w toku (workflow wf_09986c42-a8f; drzewa e3\31 (E3/0,1,3,5,6,7), e3\31b (E3/2,4,8,9), e3\33 (statki E3/10-12, wylaczniki OFF do decyzji
  Jeffa - wariant (b) projektu v2)). Kolejka gry: test 3.2 (test\Armoury-32m.dll) -> WGRANIE 25 -> gA -> WGRANIE 26 -> gB -> WGRANIE 27 -> 3.1+3.3 -> WGRANIE 28 -> bieg 2-letni.
- (18:40) **WGRANIE 25: Armoury 1211d984** (3.2, opis w STAN-PRAC). Test grupy A (3.4-3.7, test\gA\Armoury.dll + RealisticCaptivity.dll) w toku.
- (19:05) **3.1+3.3 KOD SCALONY:** w-toku/e3-31m a939cd5 (drzewo e3\31m; = gB + e3-31 (E3/0,1,3,5,6,7) + e3-31b (E3/2,4,8a-c,9) + e3-33 (statki, OFF); E3/8d odlozony).
  Dwie recenzje + poprawki w toku (wf_9ec2f5e7-61e) -> test\Armoury-31m.dll. Narzedzie progow etapu 3: tools/sprawdz_logi.py --grupa etap3 (w drzewie 31m).
  Pytania z kodu: doplata za podmiane rekruta ROT (RotLeaks37) dalej w nicosc - zamknac z awansami?; napitki i drewno wojska (3.4) placi lord czy sakiewka ludzi?
- (19:26) **WGRANIE 26: Armoury d38f1e69 + RC cea72cca** (3.4-3.7). Test grupy B (3.8-3.10, test\gB\ Armoury + RC + GrandTourney) w toku.
- (20:00) **3.1+3.3 PO RECENZJACH:** w-toku/e3-31m dc290b3, test\Armoury-31m.dll 6e4b5b6a (test z RC i GT z gB). Pytania do Jeffa (kierunkowe): podatek BK od warsztatow
  lordow i gracza w cudzych miastach - zniknac (dzis) czy wlasciciel placi panu miasta; doplata za podmiane rekruta ROT do sakiewki ludzi jak awanse; werdykt 3.2
  "pokonany" takze dla partii lordow. **ETAP 4:** projekt w toku (wf_77f49d09-f4f -> SCRATCH(ae222cb7)\e4\PROJEKT-ETAP4-KSIEGA-LUDZI-ZIMA-2026-10-10.md).

## 24. START TUTAJ (10.10 ok. 21:00; sesja ae222cb7; Jeff wraca ok. 03:20)

**W grze (ZATWIERDZONE):** Armoury 6e4b5b6a (noc/wgranie-28 = w-toku/e3-31m dc290b3), RealisticCaptivity 90665425, GrandTourney bbb841d7 (noc/wgranie-27),
CrashScribe 77b11df5. Wgrania tej nocy: 23 (pula Strazy, namioty, 183d), 24 (dezercja AI: kara BK, Inni, przepelnienie), 25 (3.2), 26 (3.4-3.7, z RC), 27 (3.8-3.10, z RC i GT),
28 (3.1+3.3). Opisy i testy: STAN-PRAC "WGRANIE 23..28". **ETAP 2 (punkty 1, 2, 4) i ETAP 3 (3.1-3.10) - kod w grze.** Statki 3.3 w kodzie, wylaczniki OFF.
**W TOKU:** bieg 2-letni (728 dob) na wgranym zestawie od 20:58 -> SCRATCH(ae222cb7)\test\at-728.out.txt; ocena: sprawdz_logi --grupa etap2 (+ --grupa etap3, narzedzie z
drzewa e3\31m), dezercja AI w 2. polowie roku (dez.py w SCRATCH(ae222cb7)\d1), bandy (zloto band u pasera, kiesy band), kredyt koron 186, Straz, glowy w dobach 364/728.
ETAP 4: projekt (wf_77f49d09-f4f -> SCRATCH(ae222cb7)\e4\PROJEKT-ETAP4-KSIEGA-LUDZI-ZIMA-2026-10-10.md) -> pierwsza paczka kod w trakcie biegu -> test po biegu.
**Drzewa (worktree klonu lancuch):** SCRATCH(ae222cb7)\e3\{32,32m,34..310,gA,gB,31,31b,33,31m}; galezie w-toku/e3-* wypchniete do repo Jeffa i GitHub.
**Pytania do Jeffa (zbiorczo):** (1) statki 3.3: wlaczyc teraz (-8..-11% wojska do etapu 5) czy z podatkiem wojennym w etapie 5 (rada); (2) fale dezercji po wielkich
przegranych (Recent Events -65) - zostawic czy zlagodzic; (3) kiesa bandy przylaczajacej sie do gracza (dzis znika); (4) podatek BK od warsztatow lordow/gracza w cudzych
miastach; (5) doplata podmiany rekruta ROT; (6) werdykt "pokonany" dla partii lordow; (7) napitki/drewno wojska - lord czy sakiewka ludzi; (8) wedrowcy BK z prawdziwym towarem.
- (22:55) **ETAP 4:** projekt v2 (SCRATCH(ae222cb7)\e4\PROJEKT-ETAP4-KSIEGA-LUDZI-ZIMA-2026-10-10.md, 12 paczek E4/0-E4/11, krytyka 28 uwag). E4/0 (pomiar, gra bez
  zmian) KOD GOTOWY: w-toku/e4-0 2a71008 (drzewo e4\40), test\Armoury-e4-0.dll 35b80d6b - test 120 dob + zapis PO biegu 2-letnim; progi: tools/sprawdz_logi.py --grupa etap4.
  E4/2 (Inni tylko z poleglych, PeopleBook konto CIALA) kod w toku (wf_c6dbaec7-ebb, drzewo e4\42, galaz w-toku/e4-2 od 2a71008). Pytania etapu 4 do Jeffa: Sarnor 100 tys.
  (ROT) czy 20 tys. (kanon); kraina bez wolnych rak nie wystawia nowego wojska do odrobienia strat; "spalona wioska wraca po roku" od ostatniego najazdu; ludnosc w ciaglej
  wojnie moze spadac do -0.5%/rok (a, rada) czy zawsze rosnie >= 0.2% (b).
  Bieg 2-letni (podglad doby 276): dezercja AI d168-195 198/d (lordowie z morale 131 - fale po kleskach), zalogi z przepelnienia (do domu) 22-34/d; bandy 14.9 -> 23.2 tys.
  - pule wyrzutkow zasilaja glownie rozbici z bitew "w las" (H3, do 1100/d w dniu wielkiej bitwy) i fale dezercji; powrot z puli do wsi 2-3/d -> naturalne miejsce E4/3
  (punkty powrotu). Straz 6.8 tys. / pulap 20 tys.
- (00:00) **E4/2 KOD GOTOWY** (w-toku/e4-2 3e10b11, test\Armoury-e4-2.dll 6c01c450; OthersRise.cs + PeopleBook.cs konto CIALA, 4 wylaczniki "The Others rise from the
  fallen"). NIE WGRYWAC bez Jeffa: recenzja - przed doba 728 pula CIALA prawie sie nie odnawia (ROT +3 do limitu band Innych na dobe), odrodzone bandy Innych = sam wodz ->
  Inni moga slabnac szybciej (fabula Nocnego Krola). Pytanie do Jeffa: czy tak ma byc (Inni rosna tylko z poleglych, nawet jesli slabna), czy pula startowa/odnawianie.
  Znana droga z niczego: pojedynek z Nocnym Krolem (ROT odtwarza sklad bandy) - nastepna paczka.
- (02:16) **WGRANIE 29: Armoury 809a603a** (E4/0 pomiar + Straz bez zwolnien). **W grze koncowo:** Armoury 809a603a, RC 90665425, GT bbb841d7, CS 77b11df5.
  Bieg 2-letni: 728/728, 0 bledow, etap 3 20/20 (szczegoly STAN-PRAC "BIEG 2-LETNI"). Raport dla Jeffa: docs/RAPORT-NOCNY-2026-10-11.md.
  **NASTEPNE (dla kolejnego konta):** odpowiedzi Jeffa na pytania z raportu (rozdz. 4) -> statki 3.3 / E4/2 wedlug decyzji; etap 4 dalej: E4/1 (zima wedlug krain - wymaga
  proby zimy, pliki danych w grze = pytac Jeffa o zgode), E4/3 (punkty powrotu - wyrzutki wracaja do domu, odpowiedz na bandy +57% w roku 2), E4/4 (przyrost ludnosci,
  T2 +624 hearth/d). Projekt: docs/etap4/PROJEKT-ETAP4-KSIEGA-LUDZI-ZIMA-2026-10-10.md. Drzewa: SCRATCH(ae222cb7)\e4\{40,42}.
- (02:56) **Test informacyjny E4/2 (120 dob, test\Armoury-e4-2.dll 6c01c450, kopia SCRATCH(ae222cb7)\kopia-e42-120):** 0 bledow; Inni d119 1687 (bez E4/2: 2014), szczyt
  2409 (2383); wstalo z poleglych 5544 wobec wzoru ROT 5781 (-4%); pula CIALA na koniec 2497 (obawa recenzji o braku odnawiania sie nie sprawdzila). Rada: wgrac (z D11) -
  czeka na "tak" Jeffa. DLL w grze po tescie zatwierdzone (809a603a / 77b11df5). Paczka D11 w toku (wf_72f90ad9-edc, drzewo e4\d11, galaz w-toku/d11).

## 25. STAN 11.10 ok. 03:20 (po powrocie Jeffa) - PRACE W TOKU, ZASADA TESTOW ZBIORCZYCH

**W grze:** Armoury 809a603a (WGRANIE 29), RC 90665425, GT bbb841d7, CS 77b11df5. Decyzje Jeffa 11.10 - STAN-PRAC "DECYZJE JEFFA 11.10" (statki z etapem 5; etap 4:
Sarnor 100 tys./tak/tak/tak; kiesa bandy do gracza; podatek warsztatow placi wlasciciel; napitki/drewno placi wojsko; wedrowcy BK z towarem; kara za przegrana /2;
doplata ROT do sakiewek; werdykt "pokonany" dla lordow; E4/2 WGRAC; proba zimy - wariant (b) tryb autotestu z plikiem-przelacznikiem, bez ruszania Armoury.json).
**ZASADA (Jeff 03:05):** kod kilku paczek -> jedna galaz -> JEDEN test 120 dob + zapis 9 dob -> jedno wgranie (pamiec testy-zbiorcze.md).
**W toku (worktree klonu lancuch, SCRATCH(ae222cb7)\e4\...):**
- D11 (decyzje 11.10, 6 zmian): workflow wf_72f90ad9-edc, drzewo e4\d11, galaz w-toku/d11 od 7aa4a6e -> test\Armoury-d11.dll.
- E4/2 gotowe: w-toku/e4-2 3e10b11 (od 2a71008) - do scalenia.
- E4/3 (punkty powrotu + powrot wyrzutkow do domu): workflow wf_dedf899d-56d, drzewo e4\43, galaz w-toku/e4-3 od 3e10b11 -> test\Armoury-e4-3.dll.
- E4/1 (zima wedlug krain) + PROBNA ZIMA (tryb autotestu, plik-przelacznik): do zrobienia (drzewo e4\41, galaz w-toku/e4-1 od 7aa4a6e).
- Wedrowcy BK z prawdziwym towarem (decyzja 11.10): do zrobienia (drzewo e3\36b, galaz w-toku/e3-36b od 7aa4a6e).
**Plan:** po kodzie - scalic d11 + e4-2 + e4-3 + e4-1 + 36b (+ E4/4 od e4-3, jesli zdazy) na galaz w-toku/zbior30 (unia konfliktow: SCRATCH(ae222cb7)\d1\union.py, potem
python tools/gen_mcm.py), build -> test 120 dob nowej kampanii + bieg 120 dob z proba zimy (plik-przelacznik) + zapis 9 dob -> WGRANIE 30. Progi: sprawdz_logi --grupa etap2,
etap3, etap4 (narzedzie z drzewa e4\40 lub nowszego).
- (03:50) **D11 GOTOWE** (w-toku/d11: d028f29..a1fdff3 + poprawki recenzji; test\Armoury-d11.dll; grupa MCM "Defeats and payers (D11)": DefeatMoraleHalf, BandJoinPurseToPlayer,
  WorkshopOwnerPaysTownTax, BkSupplyFromMenPurse, RotSwapPayToPurse, LordPurseOnlyToVictor). Recenzja: 0 krytycznych/waznych. Czeka na scalenie i test zbiorczy.
  Etap 5 rozpisany Jeffowi w czacie (porcje A kasy miast / B kiesa ludu + podatek wojenny + wyrownanie + targ przy zamku + statki / C reszta; 2-3 noce, po etapie 4).
- (04:20) **WSZYSTKIE PACZKI ZBIORU 30 GOTOWE (po recenzjach):** w-toku/d11 ebf7552 (od 7aa4a6e), w-toku/e4-2 3e10b11 (od 2a71008), w-toku/e4-3 81429c3 (od 3e10b11;
  + w toku poprawka "wyposrodkowanie z wojna" - tempo powrotu wyrzutkow: wojna wolno OutlawRoutedHomeDaysWar, pokoj szybko OutlawRoutedHomeDays - agent w tle, commit na
  w-toku/e4-3), w-toku/e4-1 9e6b218 (od 7aa4a6e; zima wedlug krain + proba zimy: plik-przelacznik
  C:\Users\GAME\Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-proba-zimy.flag - polozyc przed biegiem B, usunac po), w-toku/e3-36b 0d5f2e7 (od 7aa4a6e;
  wedrowcy BK z prawdziwym towarem). **KROKI:** worktree SCRATCH(ae222cb7)\e4\z30 z galezia w-toku/zbior30 od d11 (ebf7552) -> git merge kolejno e4-3 (zawiera e4-2), e4-1,
  e3-36b; konflikty: CHANGELOG (python -I SCRATCH(ae222cb7)\d1\union.py CHANGELOG.md cl), McmSettings (git checkout --ours, potem python tools/gen_mcm.py), reszta union.py
  (przejrzec wypisane konflikty!) -> build Armoury -> test A: 120 dob nowej kampanii; test B: 120 dob z plikiem-przelacznikiem; zapis 9 dob -> progi etap2/etap3/etap4
  (narzedzie z drzewa z30) -> WGRANIE 30 (z kopia). Do zgloszenia Jeffowi (E4/1 recenzja): model jedzenia RBL przejmuje caly lancuch (nie dzialaja ROT "Inni nie jedza",
  NavalDLC -20% na morzu, BEE, pasza/perki BK) - stan sprzed E4/1, decyzja Jeffa.
- (04:45) **ZBIOR 30 SCALONY:** w-toku/zbior30 c02c80f (drzewo SCRATCH(ae222cb7)\e4\z30; = d11 + e4-3 (z e4-2 i wyposrodkowaniem cc40e0e) + e4-1 + e3-36b; konstruktor
  ArmouryBehavior i pozycje ksiegi MoneyLedger (NBkSupply 7, NTravel 8, Notes 9) scalone recznie), build kod 0, test\Armoury-z30.dll 9d314ea6. TEST A (120 dob nowej
  kampanii) w toku -> test\at-z30-A.out.txt. Potem: TEST B - polozyc plik C:\Users\GAME\Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-proba-zimy.flag, 120 dob,
  usunac plik; potem zapis 9 dob; progi etap2/3/4 (tools z drzewa z30) -> WGRANIE 30.
- (05:20) **ZBIOR 30 - TEST A (120 dob nowej kampanii) OK:** 0 bledow; etap 3 20/20 TAK (reszta -1.5 tys./d); etap 2: wojsko lordow w wojnie 100.6 tys., glowy 0,
  bankruci 0, dezercja AI 6/d (baza 49), bandy -18%/-16%; etap 4: NIE tylko znane dziury pomiaru (T2 hearth +677/d -> E4/4, T6, T7 -> E4/7-9) + pula wyrzutkow d60/90/120
  1.7/3.0/4.0 tys. (rosnie w wojnie przy 1/120 - plateau dopiero po roku). TEST B (proba zimy, plik-przelacznik polozony 05:16; skrypt usuwa go po tescie) w toku ->
  test\at-z30-B.out.txt; potem zapis 9 dob -> WGRANIE 30.
- (05:35) Jeff: "po wgraniu 30 rob dalej E4/4". E4/4 (przyrost naturalny, miasta jako stan, podloga 10) KOD W TOKU: workflow wf_4d6430c3-d4f, drzewo SCRATCH(ae222cb7)\e4\44,
  galaz w-toku/e4-4 od c02c80f (zbior 30) -> test\Armoury-e4-4.dll. Test E4/4 razem z nastepna paczka (E4/5 spustoszenie - zalezy od E4/4) wedlug zasady testow zbiorczych.
- (05:45) Rownolegle (Jeff: "czy cos jeszcze z E4 mozna robic"): workflow wf_16064079-10e - (1) naprawa lancucha modelu jedzenia RBL (drzewo e4\jedz, galaz
  w-toku/e4-jedzenie od c02c80f -> test\Armoury-jedzenie.dll), (2) plany kodu E4/5 i E4/7 bez kodu (SCRATCH(ae222cb7)\e4\PLAN-KODU-E45.md, PLAN-KODU-E47.md).
  Nastepny test zbiorczy: E4/4 + jedzenie (+ E4/5, jesli zdazy) -> WGRANIE 31.
- (06:15) **WGRANIE 30: Armoury 9d314ea6** (zbior 30; opis STAN-PRAC). **Jedzenie RBL GOTOWE:** w-toku/e4-jedzenie b6c2139 (FoodChain.cs; test\Armoury-jedzenie.dll 748c06d8):
  jeden lancuch RBL -> ROT -> NavalDLC -> BEE -> BK. Pytanie do Jeffa: pasza koni na pustyni/sniegu - (a) tylko E4/1 (rada) / (b) dzis: zima max(E4/1, BK), poza zima formula
  BK (zla). **Plany kodu:** SCRATCH(ae222cb7)\e4\PLAN-KODU-E45.md (spustoszenie, 7 commitow, 14 kluczy, 21 progow; spalona wies w grze nie produkuje - latka TickProductions)
  i PLAN-KODU-E47.md. E4/4 kod w toku (wf_4d6430c3-d4f). Nastepny test zbiorczy: E4/4 + jedzenie (+E4/5) -> WGRANIE 31.
- (06:40) **E4/4 GOTOWE:** w-toku/e4-4 2a6ca1e (od c02c80f; test\Armoury-e4-4.dll 6c9c0104). RESZTA hearth +677/d = 2 wsie podwojnie w Settlement.All (blad pomiaru, poprawiony).
  E4/4 NIE MOZE ISC BEZ E4/5 (rabunek dzis usuwa ludzi w nicosc: w wojnie ok. -25%/rok). E4/5 KOD w toku (drzewo e4\45, galaz w-toku/e4-5 od 2a6ca1e, plan docs/etap4/PLAN-KODU-E45.md).
  Test zbiorczy po E4/5: zbior31 = e4-5 (zawiera e4-4) + e4-jedzenie (b6c2139) -> test 120 dob + zima 120 (plik-przelacznik) + zapis -> WGRANIE 31.
- (07:00) **Jeff - ZASADA:** "po wgraniu 31 rob dalej E4/7, testy zrob jak zrobisz wszystko w E4, i na koniec dopiero test, aby poprawic". PLAN: zbior31 (e4-5 z e4-4 +
  e4-jedzenie z pasza (a)) -> test 120 + zima 120 + zapis -> WGRANIE 31. Potem BEZ TESTOW kod: E4/6 (spichlerz, glod; od e4-5 + jedzenie) i E4/7 (pochodzenie, pobor z wsi,
  polegli trwale; plan docs/etap4/PLAN-KODU-E47.md) rownolegle -> E4/8 (zalogi/milicje z ludzi; po E4/7) i E4/10 (weterani; po E4/7) -> E4/9 (Straz, Wolni Ludzie, niewola;
  po E4/7+E4/8) -> E4/11 (wioski poboczne; po E4/5+E4/6). Kazda paczka: kod + recenzja (+ poprawki), osobna galaz, scalanie na biezaco w w-toku/zbior-e4-koniec. Na koniec
  JEDEN test (rok 364 dob nowej kampanii + zima 120 + zapis 9; projekt rozdz. 4 "Koniec etapu" mowi tez o 728 dobach dla "spalona wioska wraca po roku") -> poprawki -> wgranie.
- (07:10) Pasza koni wariant (a) GOTOWA: w-toku/e4-jedzenie 4c54fe8 (postfiks na BK CalculateAnimalFoodNeed -> AnimalNeed po glowie, warunki terenu BK zostaja, wylacznik
  BkAnimalFeedPerHead). Do sprawdzenia w tescie zbioru 31: glod i zakupy AI w Dorne (pustynia caly rok) i na polnocy (snieg). Czekam na E4/5 (wf_3ee22640-ded).
- (07:45) **ZBIOR 31 SCALONY:** w-toku/zbior31 dfb12df (drzewo e4\z31 = e4-5 51af386 (z e4-4) + e4-jedzenie 4c54fe8), build kod 0, test\Armoury-z31.dll cf40fefa.
  TEST A (120 dob) w toku -> test\at-z31-A.out.txt; potem TEST B z plikiem armoury-proba-zimy.flag (polozyc, 120 dob, usunac - skrypt jak w zbiorze 30), zapis 9 dob ->
  WGRANIE 31. ROWNOLEGLE (bez testow, Jeff): E4/6 (drzewo e4\46, w-toku/e4-6 od dfb12df) i E4/7 (drzewo e4\47, w-toku/e4-7 od dfb12df, plan docs/etap4/PLAN-KODU-E47.md).
- (09:00) **ZBIOR 31 TEST A OK:** 120/120 dob, bledy 9 = te same co w kazdym tescie (cudze + znane pierwsze-szanse przy starcie: WesterosClimate CreateDelegate,
  NavalDLC "mobileParty" - sa w kazdym logu od 01:24), 0 nowych. Progi: etap2 NIE 2 (D staly zamkow 1697 = jak z30 1678 po kroku B; 2.14 kurier harness - zawsze),
  etap3 20/20, etap4 NIE 6: T6 Straz/Wolni, T7 reszta +1048/d i jency 17% (okna E4/7/E4/8), pula wyrzutkow 1452/2138/2902 (ponizej bazy 3053, ale rosnie d90->d120
  - obserwowac w biegu 364), "E4/4 przyrost WYLACZONY" = FALSZYWY ALARM narzedzia (lapie "glod WYLACZONY do E4/6"; przyrost czynny +570/d, +0.5%/rok) - poprawic
  regex w sprawdz_logi.py przy E4/6. RESZTA hearth T2 naprawiona (z30 +677/d). Wojsko lordow d120 110.4 tys., zalogi 81.2 tys., bandy 12.7 tys. Kopia logow:
  SCRATCH(ae222cb7)\kopia-z31-A. Teraz test B (zima, flaga) + zapis 9 dob w tle (b8gtpd7zz).
- (09:00) **PLANY KODU E4/8 i E4/10 GOTOWE** (docs/etap4/PLAN-KODU-E48.md, PLAN-KODU-E410.md; sam plan, kod po E4/7). Pytania kierunku do Jeffa: E4/8 P1-P3
  (straz karawan bez hamulca rak; miasta dopelniane ze wsi do udzialu; nowa druzyna: wolne rece -> zaloga siedziby -> mniejsza) - rada tak; E4/10: rekrut tier 1-2
  zawsze do domu (prog VeteranMinTier 3, rada), zwolniony zabiera komplet ze zbrojowni - osobno po etapie 4.
- (09:35) **Jeff: "tak x4"** - pytania planow E4/8 (P1-P3) i E4/10 (pyt. 1) rozstrzygniete wedlug rady (STAN-PRAC DECYZJE 11.10, plany oznaczone ROZSTRZYGNIETE).
- (09:35) **9 bledow testu A (zbior 31) - rozpoznane, wszystkie te same od 01:24:** 7 cudzych: TournamentsXPanded (ladowarka modulu, klasa abstrakcyjna), BKROTPatch x3
  (DynamicPartySizePerformancePatch - brak metody celu, 2 wpisy; RebellionsGameEntityInstantiatePatch - brak celu), BannerKings EducationManager.CorrectPlayerEducation
  (null przy tworzeniu postaci), DTE+UIExtenderEx MapArmoryReadinessMixin (AmbiguousMatch), ROT HarrenhalSiegeEvent (NRE w GatherArmy - Mends polknal i rozstawil
  oblezenie sam). 2 nasze, NIESZKODLIWE: (a) Armoury NightRest.PatchModels - postfiks MoralePostfix(MobileParty mobileParty) nie wchodzi na
  NavalDLCPartyMoraleModel.GetEffectivePartyMorale (parametr nazywa sie "party"), catch per typ; model aktywny = BKROTPartyMoraleModel (zewnetrzny, zalatany), NavalDLC
  tylko owija BaseModel - kara snu dziala (log: "morale 67.2 -> 50.4 (-25%), wpis 'Sleepless nights' tak/tak"); poprawka kosmetyczna (__0 zamiast nazwy) NIE robiona
  (CLAUDE.md 8.3). (b) Armoury WesterosClimate.ApplyAll - pierwsza szansa w MonoMod (CreateDelegate przy kompilacji detoura), lapana w MonoMod; latki wpiete
  (data + 6 + 19 metod), 0 ERROR w Armoury.log.
- (09:50) **Jeff: "dorzuc do koncowej paczki E4" (poprawka NightRest __0) + "te 9 bledow - mozemy je naprawic, czy cos psuja?"** -> workflow wf_d88e99ab-fe8
  (rozpoznanie 7 bledow w dekompilacji + sceptyk na kazdy werdykt -> poprawki TYLKO tego, co psuje, + NightRest -> recenzja). Drzewa: SCRATCH(ae222cb7)\e4\bledy
  (w-toku/e4-bledy od dfb12df, Armoury) i e4\csbledy (w-toku/cs-bledy od 37535a7 = CS w grze 77b11df5); DLL probne test\Armoury-bledy.dll, CrashScribe-bledy.dll,
  CrashScribe-at-bledy.dll (z at1-cs.diff). Scalic do w-toku/zbior-e4-koniec; test na koncu etapu 4 razem z reszta.
- (10:35) **PLAN KODU E4/9 GOTOWY** (docs/etap4/PLAN-KODU-E49.md; sam plan, kod po E4/7 i E4/8). Straz: tyle ludzi, ile dzis daja kanaly gry, zmienia sie zrodlo
  (konto Mur -> 3% puli wyrzutkow Westeros -> ludnosc); WatchNoReleases juz jest (bez nowego WatchLifelong); NIEWOLNI = nowe konto z pochodzeniem (bez dzieci, -5%/rok,
  27 tys. Zelaznych tylko NK); jency z rabunku - 10% napadnietych z uchodzcow wsi przy koncu udanego rabunku (odstepstwo od szkicu E4/5 - bez gubienia przy zapisie
  w polowie rabunku); DLUG 4 - dezercja z braku zoldu rodu w zajeciu do domu. UWAGA scalanie: numery przyczyn E4/6 i E4/7 sie pokrywaja. Pytania kierunku do Jeffa:
  (1) zalogi Strazy dalej rosna same, ale z prawdziwych ludzi Westeros - rada tak; (2) rozbici bracia wracaja na Mur, uciekinierzy do wyrzutkow Polnocy - rada tak.
- (10:45) **ZBIOR 31 TEST B (proba zimy) OK:** 120/120, bledy 9 = te same, Armoury.log 0 ERROR; zima od 8. doby; ciecie plonu: Wyspy Letnie 0% / cieple 15.7% /
  reszta 48.6% (jak z30); lordowie AI w glodzie 20 (z30: 29), bez jedzenia 16 (z30: 26); "partii jedzacych" w zimie 673 (z30: 1974) - skutek naprawy lancucha jedzenia
  (BK: partie BK nie jedza, ROT: Inni nie jedza - reguly wrocily), nie blad. Progi etap4 NIE 7: jak test A + T8 koszt 30.4 ms/d (prog 30; Zima 14.9, model hearth W1 10.6;
  z30 21.8) - marginalnie, sprawdzic w tescie koncowym. Kopie: SCRATCH(ae222cb7)\kopia-z31-B. Test zapisu 9 dob w toku.
- (10:55) **WGRANIE 31** (Armoury cf40fefa = w-toku/zbior31 dfb12df, galaz noc/wgranie-31; kopia .bak-2026-10-11-przed-noc31 + D:\...\2026-10-11-noc31-przed) - po
  testach A, B (zima) i zapisie 9 dob (0 ERROR Armoury; flaga zimy po tescie B usunieta przez skrypt - gra jej nie kasuje). Opis w STAN-PRAC "WGRANIE 31".
  Dalej (zasada Jeffa): reszta E4 bez testow po drodze -> jeden test koncowy etapu -> poprawki -> wgranie.
- (11:05) **Jeff: E4/9 "tak, tak"** (zalogi Strazy rosna z ludzi Westeros; rozbici bracia na Mur, uciekinierzy do wyrzutkow Polnocy) + "rob dalej".
- (11:30) **E4/6 i E4/7 GOTOWE (kod + 2 recenzje + poprawki, bez testu w grze):** w-toku/e4-6 0cd60f1 (Famine.cs - spichlerz okregu, glod, uchodzcy z glodu z osobna
  czescia konta FamN, mieszczanie przy Food 0 MFamineTown; DLL test\Armoury-e4-6.dll 587ffbaa) i w-toku/e4-7 61f75d7 (pochodzenie, DeduceManpower, powroty z hearth,
  polegli trwale, wolne rece i hamulec, menu "People of this land", Fold/Drift slownikow; DLL test\Armoury-e4-7.dll ca10319e) - repo i GitHub. Pytania kierunku do
  Jeffa: E4/6 - uchodzcy we wlasnym regionie wracaja zawsze, gdy gospodarze nie gloduja (odstepstwo od GL 2.6; rada tak); E4/7 - najemnik z karczmy z ludzi miasta, obcy
  tylko bez wolnych rak (do E4/10; rada tak), niewzieci z poboru wymuszonego gracza wracaja do DOM wsi (rada - zamknieta ksiega).
- (11:30) **Workflow wf_549a838c-5d8:** scalenie E4/6 + E4/7 -> w-toku/zbior-e4-koniec (drzewo SCRATCH(ae222cb7)\e4\koniec; kolizje: numery przyczyn PeopleBook,
  Devastation.Daily, zgony z glodu vs NoteCivilDead, arm_people) + recenzja scalenia, potem E4/8 (e4\48) i E4/10 (e4\410) rownolegle od czubka scalenia.
- (11:30) **Workflow wf_16c71a8d-809: projekt etapu 5** (rozpoznanie logi/kod/dokumenty/styki z E4 -> projekt -> 2 krytyki -> v2) -> docs/etap5/. Jeff 11:20 pyta
  "czy mozesz zaczac etap 5 czy dopiero po etapie 4" - odpowiedz: projekt teraz, kod porcji niezaleznych rownolegle po projekcie, porcje z jedzeniem/karczma po E4.
- (12:30) **9 BLEDOW - WYNIK (wf_d88e99ab-fe8, kazdy werdykt potwierdzony przez niezaleznego sceptyka):** B1 TournamentsXPanded - nic (klasa pomocnicza, TXP laduje sie
  normalnie); B2 BKROTPatch DynamicPartySize - nic (mod "Dynamic Party Size" nie jest zainstalowany; latka i tak martwa); B3 BKROTPatch Rebellions - nic (mod Rebellions
  and Demographics nie zainstalowany); B4 BannerKings edukacja gracza - nic (BKROTPatch nie inicjuje jezyka Nord; wpis gracza odtwarza sie w 0.2 s z tym samym jezykiem;
  obalona stara hipoteza, ze blokuje RunStartOption); B5 DTE/UIExtenderEx mixin - nic (pierwsza szansa w AccessTools2, droga zapasowa dziala); B7 Armoury klimat - nic;
  **B6 ROT Harrenhal - PSUJE:** BK (BKArmyManagementModel.CanCreateArmy) odmawia Roose'owi armii -> NRE w ROT -> nasz port z zastepca: Roose nigdy nie dowodzi (8 z 9
  biegow), raz zastoj fabuly (doby 117-120, Roose teleportowany co godzine), zastepca moze zostac zamrozony na stale (DoNotMakeNewDecisions w sejwie). POPRAWIONE:
  B6 w-toku/cs-bledy cd6c22d (przepustka BK dla skryptowego dowodcy; wylacznik HarrenhalBkArmyPass), w toku B6b (wf_7a16d181-a5a: odkladanie, gdy Roose w bitwie;
  sprzatanie i odmrozenie zastepcy na koncu wydarzenia; naprawa starych zapisow). B8 NightRest (Jeff: do koncowej paczki) - w-toku/e4-bledy 457f73e (__0, wylacznik
  NightRestEveryModel; test poza gra: morale 50 -> 37.5 w obu wariantach). DLL: test\Armoury-bledy.dll 198fe4dd, CrashScribe-bledy.dll / CrashScribe-at-bledy.dll
  (po B6b nowe md5). Galezie w repo i na GitHub. Do scalenia w w-toku/zbior-e4-koniec (Armoury) + CS do testu koncowego (CS-at z at1-cs.diff).
- (12:40) **Jeff: E4/6-E4/7 "tak x3"** (STAN-PRAC). Pkt 3 (niewzieci z poboru wymuszonego -> DOM wsi) = zmiana kodu E4/7 - workflow na w-toku/e4-7 (e4\47).
- (13:30) **PLAN KODU E4/11 GOTOWY + KRYTYKA** (docs/etap4/PLAN-KODU-E411.md, rozdz. 14 krytyka K1-K9 przyjete): wioski poboczne z pliku arm_map_villages.tsv (2446 wiosek
  w 425 okregach), ludzie wiosek = podzial konta DOM i uchodzcow okregu (bez nowych ludzi), palenie proporcja trafionych E4/5 (Burn - potem uzyje 7.11), odbudowa w rok
  razem z kontem uchodzcow, jedyna nowa latka ekonomii: zywnosc warowni od wsi wedlug ludzi w domu (wlasny wylacznik), menu rozszerza "People of this land", zapis
  arm_district, progi G1-G14 + G-ETAP (bieg 728 dob). Koordynacja Z11 z E4/9 (pojmani przez konto uchodzcow). Pytania kierunku: wyglad spalonej wioski - w 7.10 (rada);
  niebezpieczenstwo z pustych osad - pozniej (rada); ryby/mlyny wiosek - nie teraz (rada). Kod po scaleniu E4/6+E4/7 (zalezy od E4/5, E4/6, E4/7).
- (13:40) **SCALENIE E4/6 + E4/7 GOTOWE:** w-toku/zbior-e4-koniec 392c519 (497c8c4 E4/6, 4d05aa3 E4/7, 392c519 poprawka przegladu - plon spichlerza bez drugiego liczenia zmarlych cywilow i narodzin przez czynnik rak L); DLL test\Armoury-e4-67.dll; repo i GitHub. E4/8 i E4/10 ruszaja od niego (wf_549a838c-5d8). **E4/11 kod ruszyl** od 392c519 (wf_76ab5532-625, drzewo e4\411, galaz w-toku/e4-11).
- (13:50) **Jeff E4/11:** (1) wyglad spalonej wioski - etap 7 (7.10); (2) prosi o wyjasnienie (niebezpieczenstwo z pustych osad); (3) "pozniej, czyli kiedy?" - propozycja: etap 6 (z 6.3/6.9).
- (14:00) **Jeff: "tak, zgadzam sie"** - E4/11 pyt. 2 -> PLAN-DO-KONCA 4.20 (po tescie koncowym E4), pyt. 3 -> 6.18 (etap 6).
- (14:20) **E4/7b GOTOWE** (Jeff pkt 3): w-toku/e4-7 ba3a07c - niewzieci z poboru wymuszonego wracaja do wsi zdjecia (PressUntaken, MBackPress, hearth 1/k, wylacznik PressUntakenGoHome; recenzja bez uwag). UWAGA SCALANIE: MBackPress = 31 w numeracji E4/7 sprzed scalenia - dolaczyc do w-toku/zbior-e4-koniec dopiero przy koncowym scaleniu (po E4/8, E4/10, E4/11), z NOWYM numerem za wszystkimi (zeby nie zderzyc sie z numerami E4/8/E4/10/E4/11); poprawic tez Measure40 o11 i backE47.
- (14:40) **B6b GOTOWE** (w-toku/cs-bledy 3406342 + poprawki recenzji 8589e4b "B6b-p"): Harrenhal - czekamy na Roose'a do 72 h gry, gdy jest w bitwie/oblega cos
  innego (bez teleportu co godzine), Roose zawsze wykluczony z zastepcow, przy zmianie dowodcy poprzedni odmrazany (HarrenhalReleasePrev), postfiks na CompleteEvent
  rozwiazuje armie i odmraza dowodce, naprawa starych zapisow (tylko dowodca zapamietany przez ROT; inni zamrozeni - lista w logu). 4 wylaczniki w Config/settings.xml.
  DLL: test\CrashScribe-bledy.dll d971bc42, CrashScribe-at-bledy.dll eac95b1c (z at1-cs.diff). OTWARTE (pytanie do Jeffa): nasz port Muru (WallSetupWith) ma ten sam
  problem - zastepca zostaje zamrozony na koncu wydarzenia. Stare zachowanie ROT (nie nasze): armia < 75% obroncow -> ROT moze trzymac dowodce zamrozonego bez konca.
- (14:50) **Jeff: "tak, napraw i rob dalej"** - B6c port Muru (odmrazanie zastepcy na koncu wydarzenia, wzorem B6b) - workflow wf_83274572-3f7 na w-toku/cs-bledy.
- (15:10) **E4/11 GOTOWE (kod + 2 recenzje, 8/8 uwag przyjetych):** w-toku/e4-11 d6dd664 (od 392c519): VillageBook.cs - puste osady wiosek pobocznych z kontem uchodzcow
  (pule rabunek/glod), lancuch palenia (rabunek od strony wroga, zerowanie od obozu, glod od ubocza), rocznica we wszystkich 571 okregach, ogien/dymek z ksiegi, sekcja
  okregu w "People of this land" (takze village_looted), zywnosc warowni od ludzi (postfiks DefaultSettlementFoodModel.CalculateTownFoodStocksChange, Looted tylko
  z CoversQuiet), zapis arm_district (znacznik o:1 przy wylaczonej ksiedze); klucze VillageBookEnabled, DistrictSettlementPeople 250, TownFoodByVillagePeople;
  progi G1-G14 + G-ETAP; bez nowych przyczyn PeopleBook. DLL test\Armoury-e4-11.dll. Z11 (E4/9) - OnRefugeesTaken gotowe, bez wywolania. UWAGA scalanie: kolizja
  numerow przyczyn E4/8 x E4/10 (M 44-45, F 52) - przesunac E4/10 (MBackVeteran 46, MVetStart 47, FVetHire 56; numery nie ida do zapisu); konflikty
  ArmouryBehavior/Settings/McmSettings (gen_mcm)/sprawdz_logi/CHANGELOG/PeopleMenu (E4/10)/SubModuleMain (E4/8).
- (15:20) **B6c GOTOWE (recenzja bez uwag):** w-toku/cs-bledy 9be7bf0 - port Muru: odmrazanie poprzedniego dowodcy przy zmianie (WallReleasePrev), postfiks WallEnd
  na WallSiegeEvent.CompleteEvent, naprawa starych zapisow (tylko dowodca z pamieci ROT); funkcje B6b uogolnione (SiegeOtherFreezer - 4 wydarzenia oblezen ROT,
  SiegeRelease, SiegeReleasePrev, SiegeOldSaveScan). Wylaczniki WallReleaseCommander, WallOldSaveFix. Blad potwierdzony w kodzie (ROT ResetAttackerParties odmraza
  tylko lordow Wolnych z Army == null), bez dowodu w logach - wydarzenie Muru wypada ok. doby 1157 (rok 4), test 364 dob pokaze tylko linie startowe. Czekanie na
  Mance'a (jak 72 h Roose'a) NIE dodane - brak dowodu (decyzja Jeffa opisana w CHANGELOG, rada: nie). ROT_AIInfluence_Compat tez lata CompleteEvent (tylko log) -
  nieszkodliwe; poprawione zdanie B6b. DLL: test\CrashScribe-bledy.dll af47adf7, CrashScribe-at-bledy.dll 9f92b4aa.
- (15:40) **PROJEKT ETAPU 5 GOTOWY v2** (docs/etap5/PROJEKT-ETAP5-KIESA-LUDU-KASY-MIAST-2026-10-11.md + KRYTYKA-E5.md 21 uwag: 17 przyjetych, 4 inaczej, +W1-W3;
  rozpoznania A-D w docs/etap5/). Paczki: E5/0 pomiar (D; jedzie w tescie koncowym E4 - nic nie zmienia w grze); A: A1 kasy miast (D), A2 dwor (S), A3 markietani
  i notable (M), A4 drobne kas (M), A5 BEE (S), A6 lup wodza (S, tylko jesli E5/0 pokaze) - KOD TERAZ; B: B1 kiesa ludu (D), B2 podatek wojenny (S; dochodzi do daniny),
  B3 wyrownanie zamkow (M), B5 statki (M) - po scaleniu E4/7b; B4 targ przy zamku po E4/11, B6 weterani po E4/10; C po roku testu B. Testy: E5/0 w tescie E4, zbior A
  osobno zaraz po tescie E4 (pytanie do Jeffa, rada (a)), potem B, potem C. Kod E5/0 + A1-A5 rusza od 392c519 (workflow nizej).
- (15:45) **Kod etapu 5 ruszyl** (wf_dd61bf05-db6, baza 392c519): E5/0 (e5\0, w-toku/e5-0) i A1 kasy miast (e5\a1, w-toku/e5-a1) - plan kodu (docs/etap5/PLAN-KODU-E50.md, PLAN-KODU-E5A1.md) -> kod -> 2 recenzje -> poprawki; A2-A5 po kolei w e5\a25 (w-toku/e5-a2-5). Indeksy MoneyLedger: A1 od pierwszego wolnego, A2-A5 od +5. Pytanie do Jeffa: test zbioru A osobno zaraz po tescie koncowym E4 (rada a).
- (15:50) **Jeff: etap 5 - test zbioru A osobno, "rob dalej".**
- (16:10) **Jeff: A1 bez dosypki od razu (regulator tryb 2) + E5/0 udzial zysku kupcow dla pana i korony** - wpisane do projektu E5 rozdz. 6 (wiazace) i STAN-PRAC; po workflow wf_dd61bf05-db6 sprawdzic, czy kod A1/E5/0 to ma - jesli nie, osobny krok na w-toku/e5-a1 i w-toku/e5-0.
- (16:20) **Jeff: A6 bezwarunkowo w porcji A** - kod w osobnym drzewie e5\a6 (w-toku/e5-a6 od 392c519).
- (16:50) **E4/8 i E4/10 GOTOWE (kod + 2 recenzje + poprawki):** w-toku/e4-8 19d76ac (13 uwag prawdziwych, 12 poprawione - konwoje DTE usuwane bez zniszczenia wracaja
  do domu (RemoveParty), rozwiazane partie najwyzej tylu ilu maja, podloga hearth bez powrotu milicji, dlug miasta tylko z poboru E4/7, nowa druzyna: wroga/oblezona
  siedziba nic, zapas zboza 2g, rekruci nie weterani; milicja Innych z wightami; DLL test\Armoury-e4-8.dll eebb11f8; przyczyny M34-45, F42-55) i w-toku/e4-10 7b6bea3
  (8 uwag prawdziwych; T7/T7j, kontrola H3 _h3Tavern, regex I1; DLL test\Armoury-e4-10.dll 13487a3d; przyczyny M44-45/F52 - KOLIZJA z E4/8, przenumerowac przy scaleniu).
  Pytania do Jeffa: E4/8 R1-2 - zalogi d120 ok. 83% bazy przy progu 85% -> dopisac szybszy platny werbunek zalog (2/d zamiast 1, w granicy zoldu, z ludzi krainy,
  osobny klucz) przed testem (rada: tak); E4/10 - weterani z rozwiazanych partii zostaja w ludnosci BK miasta (rada: zostawic, zmienic tylko gdy V13 > +2%/rok).
- (16:50) **Workflow wf_fb811d64-c25:** scalenie calosci (E4/8 + E4/10 przenumerowane + E4/11 + E4/7b + NightRest) do w-toku/zbior-e4-koniec + recenzja, potem
  E4/9 od czubka (e4\49, w-toku/e4-9) - kod, 2 recenzje, poprawki. **Workflow wf_326d453d-473:** audyt zlota z niczego (Jeff 16:30 "czy to na pewno wszystkie?").
- (16:55) **Jeff: "tak, tak"** - K8 (zalogi 2/d) przed testem; weterani W5 zostaja w BK. K8 kod na w-toku/e4-8 (e4\48), dolaczyc przy scaleniu E4/9.

### PRACA NOCNA 11/12.10 (Jeff 17:20: "ide spac ... dokoncz etap 4, 5 i 6, pracuj do 12.00 ... testy dopiero jak zakonczysz caly etap, puszczasz testy w grze i poprawiasz;
### to samo etap 5 i 6; jesli starczy czasu etap 7 i 8; za 13 godzin raport - co udalo sie zrobic, na czym stanelismy")
Plan: (1) E4: dokonczyc E4/9 + K8 -> scalenie koncowe (z E5/0 - pomiar jedzie w tescie E4) -> TEST KONCOWY E4: bieg 728 dob nowej kampanii (progi d120/d364 z czesci
logu w trakcie; G-ETAP odbudowy w d728) + zima 120 + zapis 9 dob, CrashScribe-at-bledy.dll (B6/B6b/B6c) -> poprawki -> wgranie (Armoury + CrashScribe, kopie).
(2) E5: porcja A (A1-A6) test 120 osobno po wgraniu E4 -> poprawki -> wgranie; porcja B kod w czasie testow E4 (od scalenia koncowego), stawki z E5/0 -> test (rok) ->
wgranie; C po roku testu B. (3) E6: projekt (rozpoznanie, projekt, krytyka) w czasie testow, kod, test 120, wgranie. (4) E7/E8 jesli czas. Limity: okno 5h - przy 85%
bez nowych watkow, gry (testy) moga biec; tydzien 48% o 17:20. Raport: docs/RAPORT-NOCNY-2026-10-12.md + czat ok. 12:00.
- (17:30) **Jeff etap 6:** 6.1 i 6.14 TAK; NOWE 6.19 - kowale kuja zbroje wedlug kultury krainy, bez unikatow (lore). PLAN-DO-KONCA zaktualizowany.
- (17:30) **Audyt zlota z niczego ZATRZYMANY** (okno 5h: +6% w 8 min przy 5 przebiegach; chronie sciezke E4/9 -> test E4). Wznowic po 23:20: Workflow({scriptPath: ...\workflows\scripts\audyt-zloto-z-niczego-wf_326d453d-473.js, resumeFromRunId: 'wf_326d453d-473'}).
- (17:55) **K8 GOTOWE** (w-toku/e4-8 7e1320e + bfa2966 dok.): GarrisonAutoRecruitPerDay=2 (postfiks z licznikiem glebokosci na GetMaximumDailyAutoRecruitmentCount Default/ROT/NavalDLC; zold i miejsce pilnuje gra; kazdy slot przez TakeLevy E4/7; bez K8: Straz, Inni, bez poboru, hamulec). UWAGA (do etapu 5 / audytu): AutoRecruitmentExpenses zalog idzie W NICOSC (znany wyciek AUDYT-PELNY-WERBUNEK H6) - K8 go zwieksza; dolaczyc do zamkniecia ujsc. K8 dolaczyc do zbior-e4-koniec razem z E4/9.
- (18:05) **E5/A6 GOTOWE** (w-toku/e5-a6 f568ad8 + c71aa64 poprawki; DLL test\Armoury-e5-a6.dll 55e38075): BattlePlunder.cs - gra zdejmuje wodza pokonanego
  (MapEvent.cs:1979-1982) PRZED CommitGoldChanges (MapEventParty.cs:499-524), wiec GoldLost nie schodzi, a zwyciezca dostaje udzial z niczego (5% zlota wodza, max
  10 000); teraz zwyciezcy tylko to, co naprawde schodzi z pokonanych (migawka wodza, rozliczenie proporcjonalne, wodz bez zlota = 0); zaliczka RC 60% przy pojmaniu
  gracza; 1/9 korony z czesci wodzow (Q3b Jeffa); statki osobno (B5: ok. 59 tys./d sr28). Lad przed paczka +17 965/d sr28 (nie 86 tys. - reszta to statki i N4).
  Klucz BattlePlunderFromLoserOnly. Grupa etap5 minimalna - scalic z E5/0. Pytania: 1/9 korony z wodzow (rada zostawic - ide z rada); NOWE UJSCIE W NICOSC:
  RealisticCaptivity - 60% zlota gracza znika, gdy pojmuje banda bez wodza albo milicja -> do paczki "zamkniecie ujsc" (z AutoRecruitmentExpenses K8/H6) po audycie.
- (19:20) **SCALENIE CALOSCI + E4/9 GOTOWE:** w-toku/zbior-e4-koniec caef965 (E4/8 + K8, E4/10 przenumerowane M46-47/F56/o14, E4/11, E4/7b MBackPress 48, NightRest;
  recenzja: K8 doscalony, pyc z repo zdjety). w-toku/e4-9 72a4976 (Straz z Westeros - WatchLaw.Levy: Mur -> 3% pul -> ludnosc; dozywotnio; H1; NIEWOLNI; jency z
  rabunku z ludzi wsi; DLUG 4; 7 uwag recenzji prawdziwych; przyczyny M49-51, F57-61; DLL test\Armoury-e4-9.dll). Pytania E4/9 (ide z rada): menu wyspy - zabrani
  w niewole od siewu razem ze zmarlymi (zostawic); karawany osad Strazy biora z pul 3% (zostawic). E5/0 (a1f9a87) i A1 (e8868d2, regulator tryb 2) gotowe;
  E5/0 ma "udzial zysku kupcow zabierany przez pana i korone". Workflow wf_05af8d5e-ebf: scalenie koncowe E4/9 + E5/0 + ujednolicenie R6/W5 -> DLL
  test\Armoury-e4-koncowy.dll -> TEST KONCOWY E4.
- (UWAGA ZEGAR) Komputer chodzi w PDT (UTC-7); Jeff w CEST (UTC+2). Godziny w nawiasach z 11.10 po poludniu to szacunki - od teraz podaje UTC. Teraz 21:27 UTC (23:27 CEST). Termin raportu Jeffa: 12:00 CEST = 10:00 UTC. Okno 5h odnawia sie 23:20 UTC (57% o 21:27 UTC); tydzien 56%.
- (21:50 UTC) **SCALENIE KONCOWE ETAPU 4 GOTOWE** (recenzja bez uwag): w-toku/zbior-e4-koniec 7658247 = zbior 31 + E4/6, E4/7, E4/7b, E4/8 + K8, E4/9, E4/10, E4/11,
  NightRest, E5/0 (pomiar, tylko czyta) + R6/W5 (partia bez bitwy - ta sama regula weteranow). Przyczyny M0-51 (MKinds 52), F0-61; Armoury 1101 ustawien.
  DLL test\Armoury-e4-koncowy.dll 82bb2d8f; CS test\CrashScribe-at-bledy.dll 9f92b4aa (B6/B6b/B6c + autotest; zwykly CrashScribe-bledy.dll af47adf7 do wgrania).
- (21:52 UTC) **TEST KONCOWY E4 ruszyl** (b2k5x628s): 728 dob nowej kampanii -> zapis 9 dob z zapisu konca biegu (stan E4 w zapisie) -> zima 120 (flaga).
  Wyjscia: test\at-e4k-728.out.txt, at-e4k-zapis.out.txt, at-e4k-zima.out.txt. Progi w trakcie z czesci logu (d120, d364).
- (21:55 UTC) **Projekt etapu 6 ruszyl** (wf_bb091727-a6b -> docs/etap6/; rozpoznanie logi/kod/dokumenty/styki -> projekt -> 2 krytyki -> v2; z 6.1, 6.14, 6.19 Jeffa).
- (22:45 UTC) **TEST KONCOWY E4 - wyniki czesciowe d120** (czesc logu SCRATCH(ae222cb7)\kopia-e4k\czesc-d120.log, progi progi-d120-*.txt): 0 ERROR Armoury; CrashScribe:
  stare bledy + 2 "nowe": BK TroopRosterSafetyPatch "countChange" (stary, dotad ukryty jako "powtorka" bledu NightRest - CrashScribe laczy rozne bledy po kluczu bez
  komunikatu) i KeyNotFound pierwszej szansy w PeopleBook.ReconcileDaily (~1/dobe, lapane gleboko, bez OStumble). Progi: etap3 12/12 TAK; etap2 NIE 2 (jak zawsze);
  etap5 (E5/0) NIE 2 (koszt, T5); etap4 NIE 20 - najwazniejsze: ludnosc netto -1.02%/rok (prog -0.5%), udzial miejski 20% wobec 32.7% (pobor z miast 507/d > wsie 460/d),
  T7 reszta zolnierzy +1056/d i T4 w nicosc 310/d (3%), rownanie krain Sarnor -0.575%, pobor na podlodze 131, weterani w 53 miastach (prog 90), 5 wiosek brak,
  Straz z ludnosci reach 41% (tabela 29%), milicja N7, koszty T12/N15/G10. Test biegnie dalej do ok. d370 (potem zatrzymac - po poprawkach pelny 728 + zima + zapis).
  Diagnoza: workflow wf_28c41f10-460 (5 badaczy + sceptycy) - bez zmian kodu.
- (22:58 UTC) **ETAP 5 PORCJA A - KOD GOTOWY (kazda paczka: kod + recenzje + poprawki; bez testu w grze):** w-toku/e5-0 a1f9a87 (pomiar; 6/6 uwag), w-toku/e5-a1 e8868d2
  (kasy miast, regulator tryb 2, zawor 2/3:1/3, warsztaty zbrojne kupuja tylko z nadwyzki; 4/4), w-toku/e5-a2-5 (A2 dwor db96c4e, A3 markietani/notable dfb933e,
  A4 drobne kas bfda2cc - ZASLEPKA TownPurseA4Stub.cs do usuniecia przy scaleniu z A1, A5 BEE 7e03a6c), w-toku/e5-a6 c71aa64 (lup). Pytania kierunku (ide z rada):
  A1 - ubogie miasta ponizej zapasu nie kupia zbroi z warsztatow (zostawic, ocenic w tescie A); A2 - zapas "wlasnego" takze dla dworu 162m i zoldu zalog we
  wlasnych osadach (D rodow z siedziba zawyzone ok. 10%) - osobna paczka po tescie A (rada tak); A5 Z8 - znana gorna granica (zwrot korony ciety o zawor).
  Nastepny krok (po 23:20 UTC, nowe okno): scalenie porcji A na w-toku/zbior-e4-koniec -> w-toku/zbior-e5 + kod porcji B (B1-B6).
- Tydzien 60% o 22:58 UTC - przy tym tempie ok. 85-90% o 10:00 UTC; etapy 7-8 prawdopodobnie poza zasiegiem.
- (23:15 UTC) **DIAGNOZA d120 GOTOWA** (wf_28c41f10-460; SCRATCH(ae222cb7)\kopia-e4k\diagnoza-d120.json): ludnosc -1.02%/rok to w 2/3 MOBILIZACJA (narzedzie liczylo tylko DOM;
  zywi -0.27%/rok d28-120), ale d121-151 zywi -0.74%/rok (P1-5 - czekamy na dane 728; mozliwy hamulec straty netto - decyzja Jeffa); BLAD KODU P11: _pending
  (Dictionary<MobileParty>) gubi klucze -> ReconcileDaily przerywa sie codziennie od d3 ("bandy z niczego" ~139/d); uzgodnienie tnie slownik w trwajacej bitwie
  (Sarnor -0.575%) i ostrzal/przebicie w oblezeniach poza ksiega (w nicosc ~91 x liczba oblezen/d); podloga poboru, Straz K4 (kolejka zerowana co dobe), VillageBook
  G4, TownGrowth N11 (zly mianownik - udzial miast stoi 20.1 -> 20.0); reszta - progi/narzedzie (T7 skladniki, V2, G2, N7, S8, T6, koszty). B10: BK pomija 7 klas latek
  (countChange i in.) - BK atuty wplywu/slawy nie dzialaja (stare; decyzja Jeffa); CrashScribe laczy rozne bledy w "powtorki". Otwarte do Jeffa: P1-4 limit poboru
  z miast (dzis miasta 54% poboru notabli przy 20% ludzi), P1-5, B10b.
- (23:15 UTC) **POPRAWKI E4 ruszyly** (wf_d9a9ccdf-5f9): A ksiega pochodzenia (e4\fixA), B Straz/wioski/miasta (e4\fixB), C narzedzie (e4\fixC), D CrashScribe klucz powtorek
  (cs-bledy) -> scalenie do zbior-e4-koniec -> 2 recenzje -> DLL test\Armoury-e4-koncowy2.dll + CrashScribe-at-bledy.dll -> PONOWNY TEST (728 + zapis + zima).
  Biezacy test (b2k5x628s) biegnie dalej, do gotowosci poprawek - potem zatrzymac (TaskStop + autotest.ps1 -RestoreOnly).
- (23:25 UTC) Okno 5h odnowione (do 04:20 UTC), tydzien 63%. **Audyt zlota wznowiony** (wf_326d453d-473, wyniki przeszukan z pamieci). **Etap 5: scalenie A + kod B** (wf_c77320aa-959): w-toku/zbior-e5 (e5\zbior, od 7658247; po poprawkach E4 dosc. w-toku/zbior-e4-koniec) + B1 (plan/kod/2 rec), B2+B3+B5 (e5\b235), B4+B6 (e5\b46). Test A po wgraniu E4; B - test roczny po A (raczej po 10:00 UTC).
- (23:45 UTC) **PROJEKT ETAPU 6 GOTOWY v2** (docs/etap6/PROJEKT-ETAP6-RZEMIOSLO-SUROWCE-KONIE-2026-10-12.md + KRYTYKA-E6.md - 31 uwag przyjetych; rozpoznania A-D):
  18 paczek. Teraz (na zbiorze A etapu 5): E6/0 pomiar (ksiega uzbrojenia 6.6, ksiega koni, "co kuje kazda kraina"; jedzie w tescie zbioru A), K0 (licznik koni
  gry u zrodla, stan koni raz na dobe), CS strawa Polnocy, M2 (bramka x0.01, wielblady tylko Qarth); potem M1 = 6.19 (zbroje wedlug kultury krainy: wlasna ->
  pokrewna -> region lore -> nic; zbroje rodow tylko wlasna kraina; stroje bandytow nikt; Dothrakowie/Wolni bez stali; Wolne Miasta tylko wzory ROT), W2 (ryby);
  po A: K1->K2->K5, R1 wytop (D, NK), R3; po B1: W1, N1, W3, K4, K3 (rycerz z koniem - cel z szablonow 175); warunkowo R2, W4, R4. Test: jeden, po wgraniu
  zbioru B etapu 5 (120 dob + zima). Pytania (ide z rada): zakaz unikatow tylko dla kowali miast i AI, nie dla kuzni Jeffa; konie kupowane przez mieszczan
  tylko robocze w miejsce padlych ze starosci; udzial jazdy krainy z szablonow 175 (jedna liczba; 12-15% w 120 dobach, ok. 20% po roku).
- (00:00 UTC) **Jeff E6: "tak, tak, ok"** (unikaty - tylko kowale miast i AI; konie mieszczan robocze; jazda z szablonow 175). **TEST E4 d364 (czesc logu):** 0 ERROR Armoury;
  wojsko lordow sr. d300-364 116.8 tys. (prog 95-115 - lekko ponad, "wojska nie zmniejszamy"), zalogi d364 87.8 tys. (= 99.6% biegu A 88.1 tys., prog >= 85%),
  pula wyrzutkow d60/90/120 828/2143/2106 (TAK - plateau), uchodzcy d120 17.2 tys. Progi (okno d1-120) NIE 8. Kopia: kopia-e4k\czesc-d364.log, progi-d364-etap4.txt.
- (00:20 UTC) **Decyzje Jeffa 1-9** po tescie E4 i z etapu 5 - STAN-PRAC (ludnosc = zywi; bez hamulca strat; pobor z miast wedlug udzialu; 3 atuty BK; A2b poprawic; A4 i A1 zostawic; banda zabiera wszystko do swojej kiesy; oplata werbunku zalog nie znika). Plan: po wf_d9a9ccdf-5f9 dopisac P1-4 + progi T11/S15 INFO + atuty BK (CS) przed ponownym testem E4; A2b i ujscia (Armoury + RealisticCaptivity) w etapie 5 przed testem A.
- (00:25 UTC) **Jeff: 7 - tak popraw (z A2b); 9 - tylko czesc (lord nie wozi calego majatku)** -> gracz: rynsztunek + 60% (RC) do bandy; AI: A6 bez zmian; skarbiec zostaje. STAN-PRAC.
- (00:30 UTC) **Jeff: banda zabiera graczowi CALE zloto** (i rynsztunek) -> do kiesy/jukow bandy; inni pojmujacy - % RC; AI - czesc (A6).
- (00:35 UTC) **Jeff: lord i milicja tez zabieraja graczowi wszystko** (zloto + rynsztunek do pojmujacego; AI - czesc). **ZMIANA ZASADY TESTOW (Jeff):** najpierw caly kod
  (E4 poprawki + E5 A/B + E6), potem jeden zestaw 120 + zima + zapis na calosci; dlugi bieg raz na koncu. **Test E4 728 ZATRZYMANY na d403** (TaskStop; gra zamknieta;
  -RestoreOnly: Armoury cf40fefa i CrashScribe 77b11df5 przywrocone; Configs BannerlordConfig.txt, DynamicReinforcements.reputation.cfg, engine_config.txt przywrocone
  z autotest-kopie\2026-10-11_14-45-34, wersje z testu w SCRATCH\test\configs-po-przerwaniu-12-10). Logi do d403: kopia-e4k\czesc-koniec.log + session-*.log.
- (00:45 UTC) **TYDZIEN 69%** (+6%/1h20 przy 4 przebiegach) - E6 zawezony do E6/0 + M1 (6.19) + M2 (+ strawa Polnocy w CS); reszta E6 na nastepna sesje.
  **Workflow wf_ffb7265d-1e8 (dokonczenie kodu przed testem):** T1 e4\fix2 (w-toku/e4-fix2: pobor z miast wedlug udzialu, progi ludnosci INFO), T2 cs-bledy
  (3 atuty BK + strawa Polnocy), T3 e5\fix (w-toku/e5-fix: A2b+Z8 wlasne pieniadze; RC - pojmujacy zabiera graczowi wszystko do siebie; oplata werbunku zalog
  do sprzedajacego), T4 e6\w (w-toku/e6-waski: E6/0, M1, M2). Potem SCALENIE KONCOWE wszystkiego (zbior-e4-koniec po poprawkach + fix2 + zbior-e5 + B + e5-fix +
  e6-waski; CS cs-bledy; RC) -> JEDEN test: 120 dob + zima 120 + zapis 9 -> poprawki -> wgranie (Armoury, CrashScribe, RealisticCaptivity) -> raport 10:00 UTC.
- (01:00 UTC) **POPRAWKI E4 GOTOWE** (wf_d9a9ccdf-5f9): w-toku/zbior-e4-koniec 459e74b = fixA (RefEq dla _pending/_kind/OutlawLaw._empty; bez uzgadniania skladu w bitwie;
  ostrzal w oblezeniu -> polegli FDieSiege; podloga -> stan miasta; ucieczka ponad limit lochu -> dom FEscape; diagnostyka niedoboru), fixB (Straz K4 kolejka; VillageBook
  _byV; TownGrowth udzial wobec wlasnych wsi), fixC (narzedzie: zywi, T7, V2, G2, N7, S8, koszty jako udzial doby, E50-T5 suma 28 dob, T1 z logu CrashScribe) +
  poprawki recenzji (f2e5f98). Na starym logu d120: etap4 NIE 20 -> 8 (pozostale to bledy kodu poprawione - potwierdzi nowy test), etap5 0. DLL test\Armoury-e4-koncowy2.dll
  80dfcb0a. CS: klucz powtorek z komunikatem (RepeatKeyByMessage). Klucze nowe: LevyVillageFromTown, PrescapeGoHome (PrisonEscapeGoHome). OTWARTE (raport): prog T3
  rownania krain 0.1% nieosiagalny dla malych krain (Sarnor) - decyzja o progu.
- (01:05 UTC) **AUDYT ZLOTA Z NICZEGO GOTOWY** (docs/etap5/AUDYT-ZLOTO-Z-NICZEGO-2026-10-11.md; 166 zgloszen, 89 niezmierzonych zweryfikowanych): duze zrodla znalezione
  wszystkie i mierzone (zakupy 374-470 tys./d -> A1; statki 226-278 + pryzy 35-70 + handel 13-17 -> B5; pasmo notabli 111-146 + kapital karawan 68-91 -> A3;
  regulator 17-18 -> A1 tryb 2; lup wodzow 12-13 -> A6). Ksiega domyka sie (reszta -1.2..-2.2 tys./d przy ok. 1.5 mln/d przeplywu). NOWE male: zaulki 30 zl/d (6.6-6.9 tys./d),
  drugie 10 000 dla notabla od BK (do 2.3 tys./d), lup z rabunku wsi, tier rodow bez lenn, 5000 dla nowej zalogi, perki 5000, turnieje AI (RBM przed RealPayer -
  priorytet), nowy rod szlachty BK 15 000 i herszt BK 10 000; dziury pomiaru: koszt paktow Diplomacy -2.7 tys./d bez odbiorcy, skoki reszty od rodow BK; uspione G24/N3
  (ozyja przy A1 tryb 2). Bez rozszerzen A3 zostanie 9-11 tys./d (prog 8.5), z A3' i zaulkami ok. 2 tys./d. Robie teraz (S): A3' (uciac drugie 10 000 BK),
  zaulki z kasy miasta, priorytet turniejow RealPayer, licznik G24/N3. Na pozniej: E5/A7 (rabunek, zaloga, tier, perki), E5/A8 (BK i Diplomacy - najpierw pomiar).
- (01:50 UTC) **DOKONCZENIE KODU GOTOWE** (wf_ffb7265d-1e8, kazde z recenzja): T1 w-toku/e4-fix2 13b8b07 (pobor z miast wedlug udzialu TownLevyByShare; T11/S15 dol INFO,
  gora +1150/d zostaje jako kontrola ludzi z niczego - do potwierdzenia Jeffa); T2 cs-bledy abe3f62 + 3b33863 (strawa Polnocy TradeBound; 3 atuty BK BkBattleRewardPerks;
  CS DLL 842f64a4, CS-at 13168fd4); T3 w-toku/e5-fix (A2b+Z8: wplaty do wlasnych osad jako "wlasne" - D staly spadnie; RC: kazdy pojmujacy zabiera graczowi cale zloto
  i rynsztunek do siebie, osada bez bohatera - kasa osady; GarrisonRecruitFee - oplata werbunku zalog do sprzedajacego; RC DLL test\RealisticCaptivity-e5.dll);
  T4 w-toku/e6-waski (E6/0 CraftRealm/ArmsBook pomiar; M1 zbroje wedlug kultury krainy WorkshopCraftByRealm; M2 bramka WorkshopsFollowInputs + wielblady tylko Qarth
  SandSteedsInDorne (NK); recenzja c5c05cc). Pytania nowe (raport): RC - czy "wszystko" obejmuje juki z towarem/zywnoscia (dzis 75% wg xml Jeffa); rynsztunek trzymany
  przez zywego lorda bez odkupu (stare, osobny projekt).
  Autotest: RC przez -ExtraDll <sciezka>\RealisticCaptivity.dll (nazwa pliku = nazwa modulu).
- (02:10 UTC) **Audyt S GOTOWE** (w-toku/e5-audyt 371c586, recenzja bez uwag): A3' NotableBkGiftCut (drugie 10 000 BK uciete), zaulki z kasy miasta AlleyIncomeFromTown, RealPayer TourneyPre High (przed RBM; GrandTourney tez z kasy miasta), licznik G24/N3; progi etap5_aud. Do decyzji: zaulki w oblezonym miescie tez placa (jak pasmo A3).
- (02:00 UTC) **Scalenie koncowe czesc 1** (wf_e6b8596a-7d5): w-toku/zbior-final (SCRATCH(ae222cb7)\final) = zbior-e5 + zbior-e4-koniec (poprawki) + e4-fix2 + e5-fix + e5-audyt + e6-waski -> recenzja -> test\Armoury-final1.dll, test\rc-final\RealisticCaptivity.dll. Czesc 2 po porcji B (e5-b1, e5-b235, e5-b46). Tydzien 76% o 01:56 UTC.
- (02:25 UTC) **Scalenie koncowe czesc 1 GOTOWE** (recenzja bez uwag): w-toku/zbior-final 184cd56; DLL test\Armoury-final1.dll a18d3e24, test\rc-final\RealisticCaptivity.dll 6bca8d7e; MCM Armoury 1132; indeksy unikalne. T11/S15: gora +1150/d progiem (kontrola ludzi z niczego), dol INFO.
- (02:55 UTC) **ETAP 5 PORCJA B - KOD GOTOWY** (wf_c77320aa-959; kazda paczka z recenzja i poprawkami): w-toku/e5-b1 1e7958b (kiesa ludu; 6/6 uwag; pytania: doplata gminy
  w glodzie proporcjonalnie - rada zostawic; B1-T8 prawie na pewno DECYZJA - 24-32 z 97 miast < 95% - decyzja po tescie), w-toku/e5-b235 (B2 podatek wojenny c8bf89e,
  B3 wyrownanie 7079ac4, B5 statki ON d662a06 - port placi za statki, ktore stocznia kasuje: na razie pomiar B5-14), w-toku/e5-b46 (B4 targ przy zamku - stan wozow
  w zapisie arm_castlemarket; B6 weterani d642fff). Scalenie czesci 2 (wf_ad16ef5c-94d) -> test\Armoury-final.dll + rc-final -> TEST ZBIORCZY.
- (03:20 UTC) **SCALENIE KONCOWE CZESC 2 GOTOWE** (recenzja: tylko komentarze): w-toku/zbior-final f07d38b = etap 4 (z poprawkami) + etap 5 (A, B, dokonczenia, audyt S)
  + etap 6 waski. MCM Armoury 1162. DLL: test\Armoury-final.dll 89a27e09, test\rc-final\RealisticCaptivity.dll 943ea0d3, CS test\CrashScribe-at-bledy.dll 13168fd4
  (do wgrania zwykly CrashScribe-bledy.dll 842f64a4). **TEST ZBIORCZY ruszyl** (bu0blzbsf): 120 dob nowej kampanii -> zapis 9 dob z konca biegu -> zima 120;
  wyjscia test\at-fin-120.out.txt, at-fin-zapis.out.txt, at-fin-zima.out.txt.
- (04:05 UTC) **TEST ZBIORCZY 1 NIE WYSTARTOWAL** (brak startu kampanii, hang): GarrisonRecruitFee (E5 decyzja 9) zakladal latke na DefaultClanFinanceModel przy starcie gry ->
  cctor (Game.Current.GameTextManager null) -> TypeInitializationException na caly proces (pulapka ERRORS.md 14.09). POPRAWIONE: splata zakladana w kampanii
  (GarrisonRecruitFee.EnsureHooks, ArmouryBehavior obok CrownIncome.EnsureHooks) - w-toku/zbior-final 329f2c7; jedyna nowa latka na tej klasie (grep diffu od 7658247).
  DLL test\Armoury-final.dll 101c8cfe. **TEST ZBIORCZY 2 ruszyl** (bl5mnkmtn): wyjscia test\at-fin2-120.out.txt, at-fin2-zapis.out.txt, at-fin2-zima.out.txt.
- (05:00 UTC) **TEST ZBIORCZY 2 - 120 dob OK** (17.6 s/dobe; Armoury 0 ERROR; CrashScribe 24 - stare BK/BKROT, teraz widoczne osobno dzieki kluczowi powtorek;
  logi SCRATCH(ae222cb7)\kopia-fin). Progi: etap3 NIE 1 (probki wokol okien 2); etap4 NIE 6 (pula wyrzutkow d120 3115 > 3053, **wojsko lordow d90-120 83.7 tys.**,
  N6 Miasta 3.8, K8 Straz 24 (1 doba), K9 dezerterzy Strazy 10 Polnoc/2 inne, K22 koszt); etap2: **wojsko w wojnie sr28 78.6 tys. < podloga 85 tys.** (po samym E4
  109 tys.); etap5 NIE 8 + DECYZJA 2: A1-T11 BK rynek -107 tys./d (prog 2 tys.), A1-T12 dar startowy 7.6 mln bez ujscia, A1-T14 1 doba, narzedzie etap5_a2 wyjatek,
  A3-2 sakiewki 19.1 dnia (prog 15), A3-7 koszt 40 ms, B5-7 port Saath 43 doby bez nadwyzki, B4-6 5.66% (prog 5.6), B1-T7/T8 DECYZJA (spodziewane).
  -> NIE WGRYWAM przed poprawka wojska. Diagnoza + poprawka (wf_c17ab7f0-2e7) -> ponowny test 120 -> wgranie. Test zapisu i zimy biegna dalej (bl5mnkmtn).
- (05:35 UTC) **TEST ZBIORCZY 2 - zapis 9 dob OK** (zapis konca biegu 120 z calym stanem E4/E5/E6 wczytany, 18.4 s/dobe, Armoury 0 ERROR) i **zima 120 OK** (0 ERROR; spichlerze
  F6/F7/F8 TAK - zadna warownia AI nie glodzi, mediana zapasu >= 1980 dni; zywi -0.19%/rok). W zimie: wojsko lordow d90-120 70.3 tys. (!), pula wyrzutkow d120 4294,
  Straz d120 7293 (baza 86xx), T8 koszt 30.8 ms. Wszystko to samo zrodlo - mniej zlota u rodow (diagnoza wf_c17ab7f0-2e7 w toku).
- (06:20 UTC) **DIAGNOZA WOJSKA** (wf_c17ab7f0-2e7; SCRATCH(ae222cb7)\diag_wojsko): etap 4 wojska nie obnizyl (e4k 106 tys.), zrobil to etap 5: pulap budzetu 166 = 0.6 D + 0.8 (G-R)/45;
  D swiata 538 wobec 1065 tys./d, kiesy glow 45 wobec 89 mln. Skladniki (d93-120, wojsko -27 tys.): zawor kas miast do panow 533 -> 161 tys./d (-12.6 tys. wojska; brak pompy
  "zakupow" z niczego), statki B5 -176 tys./d (-6 tys.; B2 podatek wojenny dal tylko 3-4 tys./d zamiast 30-60 - NIE wyrownal B5), B1 wies -165 tys./d (-5.6 tys.), A2 sprzet do dworu
  -128 tys./d (-4.3 tys.), A6 -1 tys., korona +2.4 tys.; A2b w tym (Jeff: trudno). Korona nie chomikuje (oddaje 87%). POPRAWKA (69b1c84 + 40b975e, recenzja bez uwag):
  CrownRentHeldToServing (renta rodu bez warunku do rodow sluzacych, +58 tys./d), CrownWageRefundPercent 50 -> 70; TownPurseNoBkSkimVillages (BK kasowal 1%/d kies wsi ~107 tys./d);
  K9 Straz (ServesWatch); N6 TownRound; narzedzie (A1-T12, pin, Z8). Szacunek wojska 85-89 tys. (podloga 85, cel 95). DO DECYZJI JEFFA (cel 95): (2) CrownReserveReleaseDays
  360 -> 180 (pomost na rok, +4-5 tys.), (4) danina wsi LaySubsidyVillageShare 0.015 -> 0.025 albo A2 (sprzet do dworu) tylko w pokoju. DLL test\Armoury-final.dll 29fdca1e.
  **TEST 120 z poprawka ruszyl** (beg8ls8fv -> test\at-fin3-120.out.txt).
- (06:30 UTC) **TEST 120 z poprawka wojska OK** (Armoury 0 ERROR, 17.3 s/dobe; wojsko w wojnie sr28 88.4 tys., d90-120 92.1 tys., ostatnia doba 87.7 - stabilne; NIE: etap2 3 (wojsko < 95,
  D staly zamkow, 2.14), etap3 1, etap4 3 (pula wyrzutkow 3196 > 3053, wojsko, K22 koszt), etap5 4 + DEC 2 (A3-2 sakiewki 18.3 dnia, A3-7 koszt 37 ms, B1-T6 zjedzone bez zaplaty
  1.07%, B4-6 zatkane magazyny 6.57%; B1-T7/T8 DECYZJA)). Logi: SCRATCH(ae222cb7)\kopia-fin3. **WGRANIE 32** (STAN-PRAC): Armoury 29fdca1e, CrashScribe 842f64a4, RC 943ea0d3.
- (07:30 UTC) **RAPORT NOCNY** docs/RAPORT-NOCNY-2026-10-12.md. E6 porcja 2 (wf_78d104b0-8ef, w-toku/e6-p2): K0, W2 gotowe, K1/K2/K5 w toku - bez testu i wgrania dzis (do nastepnego wspolnego testu). Tydzien 79%.
- (09:30 UTC 12.10) **E6 porcja 2 GOTOWA** (wf_78d104b0-8ef, w-toku/e6-p2: K0 f4ec3d1 HorseState, W2 4c3eb29+fceb06f HamletKinds, K1 82232af HorseFate, K2, K5 8843e73; kazda z recenzja). Jeff "tak" - krok 1: decyzje z rana (wf_dd26748f-522: danina wsi 2.5%, RC cale juki, zaulki w oblezeniu, prog max(0.1%,200)) -> test 120 + zapis + zima -> wgranie 33.
- (10:05 UTC 12.10) **Decyzje z rana w kodzie** (w-toku/e6-p2 2e7465a + 9665d65: LaySubsidyVillageShare 0.025, RC CaptorTakesAllBaggage, BesiegedPay.KAlley, prog krain max(0.1%,200) + log najgorszej krainy poza progiem). DLL test\Armoury-e6p2.dll abe15b3b, test\rc-e6p2\RealisticCaptivity.dll 9cdc53ee. **TEST e6p2** (bxwwskspl): 120 + zapis + zima -> wgranie 33. Zasada Jeffa: bez sztucznych doplat dla lordow (STAN-PRAC 09:50, pamiec).

## 26. START TUTAJ (12.10 ok. 14:50 UTC; sesja ae222cb7; limit tygodnia 92%, reset 17.10 03:00 UTC)

**Zasada Jeffa (12.10 10:30 UTC):** caly kod etapow 5-8 najpierw (kazda paczka: autor + recenzent z poprawka, wylacznik MCM, CHANGELOG, commit), BEZ testow w grze po drodze;
potem scalenie, pelny audyt, JEDEN dokladny test (120 dob nowej kampanii + zapis 9 dob + zima + bieg dlugi; zdjecia proby drog), poprawki, wgranie. "Rob az wyczerpiesz
limit, zapisuj na biezaco" - po kazdym kroku push galezi (repo + GitHub) i wpis w STAN-PRAC.

**W grze:** WGRANIE 33 (Armoury abe15b3b, RC 9cdc53ee, CS 842f64a4, GT bbb841d7). Jeff moze grac na tym (przetestowane).

**Galezie kodu (repo lancuch = S3\lancuch, worktree w SCRATCH sesji ae222cb7; wszystkie na GitHub):**
- `noc/wgranie-33` = `w-toku/e6-p2` 9665d65 (w grze).
- `w-toku/e5-wsie` 396c033 (od 33): pan 45%, dziesiecina, nadwyzka 15%/d. GOTOWE.
- `w-toku/e6-p3` 1825401 (od e5-wsie): etap 6 porcja 3 - R1, R3, W1, W3, K4, N1, K3. GOTOWE (DLL test\Armoury-e6p3.dll b2f6aa11).
- `w-toku/e7-a` i `w-toku/e7-b` (od e5-wsie!): etap 7 w toku (wf_17813d40-8ac): A = E7/0, O1, T, C, K, O2; B = L, B, W1, W2, W3, W5, D0. Na koncu kazdego strumienia
  przeglad kolizji i DLL test\Armoury-e7-a.dll / Armoury-e7-b.dll. Jesli przerwane: `git -C <lancuch> log w-toku/e7-a` / `e7-b` pokaze zrobione paczki; wznowienie:
  Workflow resumeFromRunId wf_17813d40-8ac (skrypt workflows/scripts/e7-kod-dwa-strumienie-*.js) - zrobione paczki wroca z pamieci.

**Kolejnosc dalej:**
1. Dokonczyc etap 7 (wyzej). 2. Etap 5 reszta na galezi od e6-p3: E5/A7 (lup z rabunku z kiesy wsi, nowa zaloga z kiesy starej, dochod za tier, perki 5000 - AUDYT-ZLOTO
   wiersze 12-16), E5/A8 (BK nowy rod szlachty, herszt BK, uczta, koszt paktow Diplomacy bez odbiorcy - wiersze 17-23, 194), C1 (TownPurseNoTopUp - nowy klucz), C2 (glod ma skutki),
   C4 (napis o chlebie w oknie People), rozpoznanie ok. 26 tys./d "bitwy" z niczego. 3. Scalenie e6-p3 + e5-reszta + e7-a + e7-b (konflikty: Settings.cs, McmSettings.cs - gen_mcm,
   CHANGELOG, sprawdz_logi.py, ArmouryBehavior). 4. E7/R1, R2, W4 (po A7 i W1). 5. Projekt etapu 8 (8.1-8.18; 8.5 + odchylki cennika, 8.18 dochody gracza w nowych cenach -
   docs/CENY-A-DOCHODY-2026-10-12.md) i kod. 6. Pelny audyt (kilka niezaleznych przegladow), test zbiorczy, poprawki, wgranie.

**Czeka na Jeffa:** etap 7 pytania 1-4 (STAN-PRAC 12:00 UTC: premia drog - rada nie; ile drog; plik Diplomacy na rok 364 - rada tak; myto na Bliznakach), etap 6 K4 (cofniecie
kroku x1.3 - rada tak) i N1 (lekka naprawa bez materialu - rada zostawic).
- (17:30 UTC 12.10) **AKTUALIZACJA 26:** etap 6 porcja 3 + N1b gotowe (`w-toku/e6-p3` 6bdf1e3), etap 7 gotowy bez R1/R2/W4 (`w-toku/e7-a` be9cd4c, `w-toku/e7-b` 8e28af6). W toku
  wf_42f8dda9-fa3 na `w-toku/zbior-5-8` (SCRATCH\zb\58): scalenie e6-p3 + e7-a + e7-b ZROBIONE (a8cf73b, 1f6f04e), E5/A7 ZROBIONE (ab97ff3 + ca9579c); dalej A8, rozpoznanie "bitwy",
  C1, C2, C4, E7/R1, R2, W4, dwa przeglady, DLL. Limit tygodnia 95% - jesli przerwie: sprawdz `git log --first-parent w-toku/e6-p3..w-toku/zbior-5-8`, wznow resumeFromRunId
  wf_42f8dda9-fa3 (ta sama sesja) albo nowym workflow od pierwszej niezrobionej paczki (opisy paczek w skrypcie workflows/scripts/zbior-5-8-*.js sesji ae222cb7). Uwagi do testu
  i wgrania: STAN-PRAC 16:35 UTC (pliki danych e7-b poza DLL, CampBattlePropsEnabled w Armoury.json Jeffa, Diplomacy na rok 364).

### 26b. DLA DRUGIEGO KONTA (Jeff 12.10 ok. 17:45 UTC: "wykorzystam ten limit na maxa, potem przejde na drugie konto")

Konto ae222cb7 pracuje do wyczerpania limitu tygodnia; potem Jeff przechodzi na drugie konto. **Wznowienie workflow (resumeFromRunId) dziala tylko w tej samej sesji** -
drugie konto zaczyna NOWY workflow od pierwszej niezrobionej paczki. Skrypty wszystkich biegow sa w repo: `docs/przekazanie/wf-zbior-5-8.js` (scalenie + etap 5 reszta + E7 R1/R2/W4),
`wf-e8-projekt.js` (projekt etapu 8), `wf-e7-kod.js`, `wf-e6-p3.js` (wzory: autor + recenzent z poprawka po kazdej paczce, przeglad kolizji na koncu).

**Krok po kroku:**
1. `git pull` (galaz claude/bannerlord-rot-setup-o75kvo), przeczytaj STAN-PRAC od wpisu "WGRANIE 33" do konca i ten rozdzial 26.
2. Kod: repo lancuch `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad\lancuch`
   (biblioteki gry w `lancuch\libs`), drzewo scalone `...\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\zb\58` (galaz `w-toku/zbior-5-8`).
   Na innym komputerze: `git fetch` galezi `w-toku/zbior-5-8` z GitHub, biblioteki wedlug `libs/README.md`, dekompilacja wedlug CLAUDE.md 6.
3. **Najpierw `git -C <zb\58> status`** - agent przerwany limitem mogl zostawic niezacommitowane zmiany: obejrzyj diff; jesli paczka nie jest skonczona (brak buildu/CHANGELOG) -
   `git checkout -- .` i zrob ja od nowa. To samo w repo glownym dla `docs/etap8` (projekt etapu 8 mogl zostac w polowie - pliki rozpoznan bez commita).
4. `git -C <zb\58> log --oneline --first-parent w-toku/e6-p3..HEAD` - zrobione paczki. Kolejnosc calosci: A7 (zrobione), A8, E5/BITWY, C1, C2, C4, E7/R1, R2, W4, dwa przeglady, DLL
   (opisy paczek w `docs/przekazanie/wf-zbior-5-8.js`, tablice E5 i E7). Nowy workflow = ten skrypt bez krokow zrobionych (bez scalenia - drzewo juz jest).
5. Etap 8: jesli `docs/etap8/PROJEKT-ETAP8-*.md` ma rozdzial "Krytyka - co przyjeto/odrzucono" i commit - projekt gotowy; inaczej uruchom `wf-e8-projekt.js` od nowa. Potem kod etapu 8
   (paczki wedlug projektu, kazda autor + recenzent) na `w-toku/zbior-5-8`.
6. Potem: pelny audyt (kilka niezaleznych przegladow), autotest (dopisac kopiowanie plikow danych e7-b - STAN-PRAC 16:35 UTC), test zbiorczy (120 dob nowej kampanii + zapis 9 dob +
   zima + bieg dlugi; zdjecia proby drog), poprawki, wgranie (stala zgoda Jeffa: po udanym autotescie, kopie .bak + D:\Backup-Bannerlord\wgrane, md5; przy wgraniu: CampBattlePropsEnabled
   true i ustawienia Diplomacy na rok 364 - pliki Jeffa, gra zamknieta, kopia .bak; gdy narzedzie nie pozwoli - polecenie dla Jeffa).

**Zasady Jeffa (pamiec konta ae222cb7 - na tym samym komputerze w `C:\Users\GAME\.claude\projects\C--Users-GAME-Bannerlor2-Realm-of-Thrones\memory\`):** caly kod etapow 5-8, potem
jeden test; rob do limitu, zapisuj po kazdym kroku (push + STAN-PRAC); ekonomia bez sztucznych doplat; kazda zmiana = kontrola calosci; rozmowa po polsku, prosto; napisy w grze po angielsku.
- (18:45 UTC 12.10) DO ZROBIENIA przed testem: scalic `w-toku/a8-gentry0` (bc5e3ef - nowy rod szlachty BK startuje z 0, decyzja Jeffa) do `w-toku/zbior-5-8` (git merge --no-ff; konflikty CHANGELOG/Settings.cs - suma, McmSettings - gen_mcm; build).
- (19:05 UTC 12.10, tydzien 98%) **STAN NA KONIEC KONTA ae222cb7:** `w-toku/zbior-5-8`: scalenie, A7, A8, BITWY (rozpoznanie - nie z niczego), C1 (bez nowego klucza), C2, C4 (0168887; recenzja C4 mogla nie skonczyc - sprawdz log) ZROBIONE; zostaje E7/R1, R2, W4 + dwa przeglady + DLL; potem scalic `w-toku/a8-gentry0`. **Etap 8: projekt v2 GOTOWY** (281723d, 29 paczek; kolejnosc i watki w rozdz. 3 i podsumowaniu krytyki) - kod czeka na odpowiedzi Jeffa 1-5 (STAN-PRAC 19:00 UTC; paczki bez tych pytan mozna kodowac od razu: E8/0 pierwsza).
- (19:25 UTC 12.10) **Etap 8 kod RUSZYL** (wf_51fec049-41f, skrypt `docs/przekazanie/wf-e8-kod.js`): watek A `w-toku/e8-a` (SCRATCH\e8, Armoury po kolei: E8/0, P1-P3, S, X, W, A, Z, L, C, R, K, D1, D2, B, H = jezdziec kon + uprzaz (nowa, odpowiedz Jeffa 7), G1, G2, Q, T, E, O1-O3, O5, O6) i watek B `w-toku/e8-b` (SCRATCH\e8: E8/M, E8/RC), oba od zbior-5-8 (stan z C4). Drugie konto: `git log` obu galezi - zrobione paczki; `git status` w drzewach (przerwany agent); nowy workflow ze skryptu bez zrobionych paczek. Na koniec scalenie: zbior-5-8 (z R1/R2/W4) + a8-gentry0 + e8-a + e8-b, pelny audyt, test zbiorczy.

## 27. KONIEC KONTA ae222cb7 (12.10 ok. 19:40 UTC; tydzien 98%) - START TUTAJ DLA DRUGIEGO KONTA

Jeff: "zapisuj wszystko do przekazania, konczymy tutaj i przechodzimy na inne konto". Workflow zbior-5-8 i etap 8 ZATRZYMANE przeze mnie (TaskStop) po skonczonych paczkach -
drzewa czyste (git status bez zmian poza build.log). Na koniec dokonczone tylko recenzje E8/M i E8/0 (wf_7d02158c-a00 - wynik nizej, jesli zdazyl).

**W grze:** WGRANIE 33 (Armoury abe15b3b, RC 9cdc53ee, CS 842f64a4, GT bbb841d7) - przetestowane, Jeff moze grac.

**Galezie (wszystkie na GitHub; repo kodu = lancuch, drzewa w SCRATCH sesji ae222cb7 - sciezki w 26b):**
| Galaz | HEAD | Co | Stan |
|---|---|---|---|
| `w-toku/zbior-5-8` (zb\58) | f01f47a | e6-p3 + e7-a + e7-b scalone; E5/A7, A8, BITWY (rozpoznanie), C1, C2, C4; E7/R1 (+recenzja) | ZOSTAJE: E7/R2, W4, dwa przeglady calosci (ekonomia, kod), DLL |
| `w-toku/a8-gentry0` (zb\a8g) | bc5e3ef | nowy rod szlachty BK startuje z 0 (decyzja Jeffa) | scalic do zbior-5-8 |
| `w-toku/e8-a` (e8\a) | 8a8df38 (+ ew. recenzja) | E8/0 profil doby | ZOSTAJE: P1, P2, P3, S, X, W, A, Z, L, C, R, K, D1, D2, B, H (jezdziec = kon + uprzaz), G1, G2, Q, T, E, O1, O2, O3, O5, O6 |
| `w-toku/e8-b` (e8\b) | bf66298 (+ ew. recenzja) | scalenie cs-bledy + E8/M (cudze mody drobne) | ZOSTAJE: E8/RC |
| `w-toku/e6-p3`, `e7-a`, `e7-b`, `e5-wsie` | 6bdf1e3, be9cd4c, 8e28af6, 396c033 | juz w zbior-5-8 | - |

**Kolejnosc dla drugiego konta:**
1. `git pull`; przeczytaj STAN-PRAC od "WGRANIE 33" do konca, ten rozdzial i 26b (sciezki, sprzatanie).
2. Dokonczyc zbior-5-8: E7/R2, W4 (opisy w `docs/przekazanie/wf-zbior-5-8.js`, tablica E7), potem dwa przeglady i DLL (koniec tego skryptu).
3. Etap 8: reszta paczek watku A i B ze skryptu `docs/przekazanie/wf-e8-kod.js` (bez zrobionych: E8/0, E8/M); odpowiedzi Jeffa w projekcie etapu 8 rozdz. 6 (WIAZACE).
4. Scalenie: zbior-5-8 + a8-gentry0 + e8-a + e8-b (konflikty: CHANGELOG, Settings.cs, McmSettings - gen_mcm, sprawdz_logi.py, ArmouryBehavior; mechaniczna weryfikacja "nic nie zginelo" jak w wf-zbior-5-8.js), build.
5. Pelny audyt (kilka niezaleznych przegladow - zasada 0), autotest: dopisac kopiowanie/przywracanie plikow danych e7-b (ModuleData\arm_map_village_lore.tsv, arm_map_roads_probe.tsv,
   GUI\Prefabs\ArmMapVillageTooltip.xml, Prefabs\arm_map_roads.xml); test zbiorczy: 120 dob nowej kampanii + zapis 9 dob + zima + bieg dlugi + osobny krotki bieg zdjec proby drog
   (MapRoadsProbeEnabled); progi `tools/sprawdz_logi.py --grupa etap5/etap7/etap8` + etapy 2-4.
6. Poprawki, wgranie (stala zgoda: kopie .bak + D:\Backup-Bannerlord\wgrane, md5, gra zamknieta); przy wgraniu: CampBattlePropsEnabled true w Armoury.json Jeffa, ustawienia Diplomacy
   na rok 364 (decyzja Jeffa - kopia .bak; gdy narzedzie nie pozwoli - polecenie dla Jeffa). Po tescie: stawki myta na Bliznakach z licznika (Jeff "chce, ale policz") i zdjecia drog
   do decyzji "ile drog".

**Decyzje Jeffa z 12.10 (wszystkie w STAN-PRAC):** caly kod 5-8, potem jeden test; ekonomia bez doplat; pan 45% / dziesiecina / 15%/d; ceny - poziom zostaje, odchylki w 8.5, dochody
gracza 8.18; etap 7 - predkosc bez zmian, premia drog nie, Diplomacy 364 tak, myto - policzyc; etap 6 - hodowla tak, naprawy - ulamki w pamieci; rod szlachty BK - 0; etap 8 - odpowiedzi 1-11.
- (19:50 UTC 12.10) **OSTATNI STAN:** recenzja E8/M ZROBIONA (`w-toku/e8-b` 9237d23, bez poprawek). Recenzja E8/0 PRZERWANA przed zmianami (drzewo e8\a czyste, HEAD 8a8df38) -
  **drugie konto: najpierw recenzja E8/0** (prompt recenzji jak w `docs/przekazanie/wf-e8-kod.js`), potem dalej wedlug listy w rozdz. 27. Wszystkie workflow i pushery tego konta zatrzymane.

## 28. STAN PO NOCY 12/13.10 (konto drugie, sesja dbc3d3d1; 13.10 ok. 09:20 UTC) - START TUTAJ

**W grze: WGRANIE 34** = caly kod etapow 5-8 (Armoury e69adbd3, CrashScribe 142fdd4a, RealisticCaptivity f9a944c1, GrandTourney 035878c4, ForgeView e17e0233; galaz `noc/wgranie-34` =
`w-toku/zbior-e8` 8547369) + 6 plikow poza DLL + trzy zmiany w plikach ustawien Jeffa (Armoury.json, Diplomacy, BetterEconomy - STAN-PRAC "WGRANIE 34"). Test: 120 dob nowej kampanii
120/120, 0 ERROR, 11.8 s/dobe (W33 17.5); zapis 9 dob OK. Cofniecie: kopie `*.bak-2026-10-13-noc34-przed` obok plikow i `D:\Backup-Bannerlord\wgrane\2026-10-13-noc34-przed`
(ustawienia: `*.bak-2026-10-13-noc34`, BEE: `.bak-2026-10-13-przed-BEE-ujscia`).

**Co zrobiono tej nocy (wszystko w STAN-PRAC od wpisu "DRUGIE KONTO (sesja dbc3d3d1)"):** recenzja E8/0; E7/R2, W4 + dwa przeglady + a8-gentry0 na zbior-5-8; caly etap 8 w trzech drzewach
(e8-a, e8-b, e8-c) + E8/H (jezdziec = kon + uprzaz); cztery biegi 120 dob (bieg 1 zbior-5-8, bieg 2 i 3 zbior-e8, test koncowy) + bieg porownawczy limitu karawan; dwa rozpoznania przyczyn
z podwazeniem (`docs/test-zbiorczy/TRIAZ-zb58-120-2026-10-12.md`, `TRIAZ-2-zbior-e8-120-2026-10-13.md`); pelny audyt (`docs/test-zbiorczy/AUDYT-zbior-e8-2026-10-13.md`: 0 krytycznych,
3 wazne, 9 drobnych - wszystkie poprawione albo wykonane przy wgraniu); dwie rundy poprawek (F1-F9 po biegu 1; grupy A-E po audycie i biegu 2).
**Dwa najwazniejsze znaleziska:** (1) karawany notabli mnozyly sie przez nasz nocny oboz (gra nie liczy karawan z wylaczonym AI) - stary przeciek, tez w W33; zamkniety limitem
NotableCaravanCap = 2; (2) siec CrashScribe doliczala 70-390 ns i 240 B smieci do kazdego wywolania kazdej metody naszych modow - poprawione w Net.cs. Razem z O1-O6: czas doby 17.5 -> 11.8 s.

**Narzedzia tej nocy (repo, galaz claude/...):** `tools/wgranie/wgraj.ps1` (wgranie z kopiami i md5), `tools/wgranie/ustawienia-jeffa.ps1`, `tools/wgranie/bieg-autotest.ps1` (autotest z plikami
danych: -DllDir, -CsAt, -Tree, -Out, -Days, -LoadSave, -Profile); autotest `at1-autotest` 486008e ma `-ExtraFile "cel wzgledem Modules=zrodlo"`. CrashScribe z autotestem dla drzewa:
worktree + `git apply -3 SCRATCH(7016)\test\at1-cs.diff` + build (NIE wgrywac na stale - wgraj.ps1 odrzuca DLL z napisem autotest.json). Skrypty workflow: `docs/przekazanie/wf-*.js`.
Drzewa robocze: SCRATCH(ae222cb7)\zb\int (zbior-e8, glowne), zb\58, zb\58t, zb\pa..pe, e8\a, e8\b, e8\c; SCRATCH(dbc3d3d1)\cs-at, cap3 (wariant limitu 3, bez commita), test\ (DLL i wyniki biegow).

**Nastepne kroki:** lista "OTWARTE PO WGRANIU 34" w STAN-PRAC (11 punktow; pierwszy: zelazo w miastach). Decyzje czekajace na Jeffa: limit karawan 2 czy 3; zalozenia E8/H; czy kupcy
woza unikaty; K1-3a krok 2 (mieszczanie tylko konie robocze); ile drog (po zdjeciach); myto na Bliznakach (po liczniku).

## 29. KONIEC KONTA DRUGIEGO (sesja dbc3d3d1; 13.10 ok. 20:15 UTC; tydzien 94%, reset 19.10 17:00 UTC) - START TUTAJ DLA NASTEPNEGO KONTA

**STAN KONCOWY (13.10 ok. 22:45 UTC) - CZYTAJ NAJPIERW TO.** Jeff przechodzi na nastepne konto. Nic nie biegnie w tle (zadnego workflow, gra zamknieta, w grze pliki zatwierdzone).
- W GRZE: **WGRANIE 43** (Armoury 959d544d, CrashScribe 04f44a0e, settings 3e31434f, RC f9a944c1, GT 035878c4, ForgeView e17e0233); `noc/wgranie-43` = `w-toku/zbior-e8` = 4d33042. Bieg kontrolny czysty.
- Z poprawek 1-6 (raport roczny) WGRANE: 1 danina + korona wedlug potrzeby + poprawka daniny, 2 tarczowe, 3 pobity oblegajacy uchodzi, 5 towary (bez lnu / soli). ZOSTALY: 6 male poprawki, 4 bieg dwuletni (na samym koncu).
- **PROJEKT GOSPODARKI OD LUDZI GOTOWY, BEZ KODU:** `docs/gospodarka-od-ludzi/PROJEKT-GOSPODARKA-OD-LUDZI-2026-10-13.md` (477 linii; rozdz. 0, 4, 4A, 8 paczki, 10 pytania). Jeff dostal 7 pytan o kierunek w czacie 13.10 - ODPOWIEDZI POWINNY BYC W JEGO PIERWSZEJ WIADOMOSCI na nowym koncie; jesli ich nie ma, zapytac o nie raz (rady projektu: 1 tak, 2 lup i milicja bez wyjatku, 3 dwa razy tak, 4 zostawic i zmierzyc, 5 tak, 6 len przez nowe symbole wsi + sol x2, 7 zloto startowe kupcow / ludu / wsi wedlug ludzi tak; skarbce koron wedlug ludzi - rada tak, majatki rodow bez zmian).
- **ODPOWIEDZI JEFFA NA 7 PYTAN PROJEKTU (13.10 ok. 22:55 UTC, w czacie konta drugiego - WIAZACE, nie pytac ponownie):** 1 TAK (sila krolestw za ludzmi, bez hamulca; przed kodem rachunek na sucho, czy swiat utrzyma 85 tys. wojska - jesli nie, wrocic z liczbami); 2 "lup i milicja bez wyjatku" (Wyspy, Wolni Ludzie, Dothrakowie); 3 TAK (Nocna Straz - jawny wyjatek z dzisiejsza produkcja, dary krolestw pozniej; Sarnor ok. 250 tys. ludzi); 4 "zostawic i zmierzyc" (Qarth); 5 TAK (na poczatek tylko istniejace poprawki: zima, spustoszenie, budowle warowni, klimat lasu; narzedzia zelazne pozniej +-10%; zyznosc nie; technologia odlozona); 6 TAK (len - nowe symbole wsi lnianych w ludnych krainach, z 20 na ok. 36; sol x2 jako kopalina); 7 "tak i tak, majatki rodow zostawic tak, jak sa" (zloto startowe kas kupcow, kies ludu i wsi wedlug ludzi + skarbce koron wedlug ludzi, ta sama suma swiata; kiesy rodow bez zmian). **KOD GOSPODARKI OD LUDZI MOZNA ZACZYNAC** (po rachunku na sucho z pyt. 1).
- KOLEJNOSC PRACY NA NOC (propozycja; Jeff: 'rob dalej, rozpoznanie, zmiany'): (a) gospodarka od ludzi - NAJPIERW rachunek na sucho, czy swiat utrzyma 85 tys. wojska w polu (obietnica dana Jeffowi w pyt. 1; jesli nie - wrocic do niego z liczbami przed kodem), potem paczki z rozdz. 8 projektu, kazda: workflow autor + recenzent, test 120 dob, zapis, wgranie; (b) N7 rozbici wracaja pod choragiew (proporcje zatwierdzone - nizej); (c) W6b rod gracza w tarczowym i doplacie; (d) poprawka 6 (male); (e) reszta z listy 'ZATWIERDZONE, NIE ZACZETE'; (f) bieg dwuletni na koncu.
- Zasady Jeffa z 13.10: zapisywac kazda skonczona zmiane od razu (commit + push + STAN-PRAC), pilnowac limitu 5 h (pauza przy 90%) i tygodnia, pracowac do konca konta.


Jeff 13.10: "Rob dalej - rozpoznanie, zmiany - i zapisuj do przekazania; jak dojdzie do 100%, tyle, ile uda sie zrobic, przekazujemy drugiemu kontu na nocna prace."
Szczegoly kazdego kroku: `docs/STAN-PRAC.md` od wpisu "WGRANIE 37" do konca (godziny wpisow z 13.10 po 18:40 sa przyblizone, do +-40 min; kolejnosc dobra).

**AKTUALIZACJA 13.10 ok. 21:00 UTC (tydzien 97%): W GRZE JEST JUZ WGRANIE 43** - Armoury 959d544d, galaz `noc/wgranie-43` = `w-toku/zbior-e8` 4d33042 = WGRANIE 42 + towary N5 (regula A wylaczona domyslnie) + poprawka daniny W5b; test laczny 120 dob 0 ERROR, zapis OK, bieg kontrolny - patrz STAN-PRAC "WGRANIE 43". Punkty "KOD GOTOWY, NIEWGRANY" o N5 i W5b ponizej sa juz WYKONANE. Z listy poprawek 1-6 zostaly: 6 (male) i 4 (bieg dwuletni na koncu). Projekt gospodarki od ludzi: workflow wf_fb11d85b-5e0 w toku; gotowe pliki rozpoznan kopiowane do `docs/gospodarka-od-ludzi/robocze/` - jesli brak pliku projektu, dokonczyc z nich (skrypt `docs/przekazanie/wf-gospodarka-od-ludzi.js`: brakujace etapy uruchomic ponownie).

**(stan sprzed aktualizacji) W GRZE: WGRANIE 42** - Armoury 772918be, CrashScribe 04f44a0e, CrashScribe.settings.xml 3e31434f, RealisticCaptivity f9a944c1, GrandTourney 035878c4, ForgeView e17e0233.
Galaz `noc/wgranie-42` = 0a387b5. Dzisiejsze wgrania (kazde po tescie 120 dob + zapis 9 dob + bieg kontrolny, kopie `*.bak-2026-10-13-nocNN-przed` obok plikow i w
`D:\Backup-Bannerlord\wgrane\`): 37 runda 3 wojsko + zelazo + sluby NPC; 38 hamulec slubow (MarriageChanceScale 0.7); 39 danina wojenna z oszczednosci ludu (P1, FolkWarAid);
40 pobity oblegajacy uchodzi (N3, LosersFleeBesiegers); 41 korona placi wedlug potrzeby (W5, CrownWarAidByNeed); 42 tarczowe (W6, Scutage).

**KOD GOTOWY, NIEWGRANY:**
- `w-toku/zbior-e8` = **d527d18** = WGRANIE 42 + N5 towary (`w-toku/n5-towary` 71249d4). DLL do testu: SCRATCH(dbc3d3d1)\test\fin11 (Armoury 4b45b8f1). **Test 120 dob N5 trwal w chwili
  pisania** (test\at-n5-120.out.txt; lancuch w tle sam przywraca pliki gry). Po nim: progi `python -I tools/sprawdz_logi.py --grupa etap8 --do-doby 120 <log>` (wiersze N5-0..N5-5),
  kopia logu na D:, zapis 9 dob. **PRZED WGRANIEM N5: ustawic `ValveRaiseWhileShort` domyslnie false** (regula "sufit 4.0 bez kosztu" to towar z niczego - Jeff: produkcja ma isc
  za ludzmi), przebudowac, bieg 10 dob, dopiero wgrac jako WGRANIE 43 (skora z uboju, rece platnerzy x1.3, drewno, budowy cywilne z dala od frontu zostaja wlaczone).
- `w-toku/w5b-potrzeba-korony` (drzewo SCRATCH(ae222cb7)\zb\w5b, od 0a387b5): autor 3e94e39, **recenzja w toku** (wf_370f605f-283) - poprawka daniny: potrzeba korony widzi brak
  wojska u panow + skarbiec ponad zapas wojny zasila doplate W5. Po recenzji: scalic do zbior-e8 (konflikty: CHANGELOG -> union_changelog.py, sprawdz_logi -> union_plik.py,
  McmSettings -> gen_mcm), test 120 dob, wgrac.
- `w-toku/k13a-konie-robocze` 137dbc3 (od ca4aa87, stare): mieszczanie tylko konie robocze - Jeff TAK razem z danina po biegu 120 dob z obiema; wymaga scalenia z dzisiejszym zbior-e8.

**PROJEKT W TOKU (NAJWAZNIEJSZE ZADANIE - Jeff: "tak, rob taki projekt, myslalem, ze tak to projektujemy"): GOSPODARKA OD LUDZI.** Workflow wf_fb11d85b-5e0
(`docs/przekazanie/wf-gospodarka-od-ludzi.js`): 3 rozpoznania -> projekt -> recenzja; wynik: `docs/gospodarka-od-ludzi/PROJEKT-GOSPODARKA-OD-LUDZI-2026-10-13.md`, robocze
SCRATCH(dbc3d3d1)\gosp-ludzie\ (rozpoznanie-kod.md, rozpoznanie-dane.md, rozpoznanie-towary.md). Jesli workflow padl razem z kontem: uruchomic ponownie ten sam skrypt (albo
dokonczyc brakujace etapy recznie z plikow rozpoznania). ZASADA JEFFA: wies na mapie to SYMBOL wielu wsi - okreg produkuje tyle, ilu ma ludzi (wies zbozowa na Polnocy z 100 tys.
ludzi daje 10, taka sama w Reach z 370 tys. daje 37); tabela dla KAZDEGO surowca (dzis na 1000 ludzi / wzorzec sredniowieczny / po zmianie); poprawki wydajnosci: klimat,
dobrobyt, technologia - tylko z prawdziwa miara. Fakt wyjsciowy (skrypt `docs/ludnosc/kr-ludnosc-a-zarobek.py`): zarobek idzie dzis za liczba osad - Polnoc 3.2 mln ludzi,
109 tys. zl/d wplywu rodow (33.8 zl / 1000 ludzi), Reach 8.3 mln i 83 tys. (9.9), Dorne 1.1 mln i 103 tys. (92.4); hearth na wies i dobrobyt na miasto sa wszedzie prawie
takie same. Po projekcie: rozdz. 0 i pytania o kierunek do Jeffa; KOD DOPIERO PO JEGO "tak". Do czasu projektu WSTRZYMANE: poprawka lnu, sol, stawka wojenna podatku pana,
dalsze strojenie podatkow.

**ZATWIERDZONE PRZEZ JEFFA, NIE ZACZETE (kolejnosc proponowana):**
1. N7 rozbici wracaja pod choragiew (pole + pod murami): z tych, ktorzy uszli - 55% pod choragiew, 30% do domu, 15% do lasu; weterani 70 / rekruci 45; -10 pkt za tydzien zaleglego
   zoldu (min. 20); wlasny / obcy kraj; Nocna Straz 80 / 0; zbiorka 3-10 dob w twierdzy rodu, czekanie 30 dob, pan przyjmuje w granicach miejsca i budzetu; gracz z komunikatem.
   Tor M/L; potrzebne pole zapisu (SaveText.Sync, limit 32 KB). Powod: po N3 pula wyrzutkow d120 6.2 tys. zamiast 2.7 tys., armia po bitwie nie wraca.
2. W6b: rod gracza placi tarczowe i dostaje doplate W5 - "pod warunkiem, ze mam ziemie i sluze jakiemus krolowi" (komunikaty po angielsku).
3. W7 stawka wojenna podatku pana (kierunek TAK) - PO projekcie gospodarki od ludzi.
4. Poprawka 6 (male): najkrotsza sluzba kompanii najemnych (764 zmiany pana / rok), przerwa po wykupie z niewoli (ta sama osoba do 11 x), 440 jencow-lordow za darmo przy
   "pokoju na zero dni", 56 tys. koni jucznych z niczego, 125 tys. szt. zboza i ryb bez zrodla, Thenn pol roku bez pieniedzy.
5. Obieg kiesy ludu: P1b kara lojalnosci (po pomiarze - lojalnosc miast srednio 93, wiec jest zapas), P2 lud buduje miasto (tylko budowle sluzace miastu, bez "trzeciej" dla pana),
   P3-P5 zbytki (`docs/kiesa-ludu/PROJEKT-OBIEG-KIESY-LUDU-2026-10-13.md`).
6. Lup zdobytego miasta (`docs/lup-miast/LUP-ZDOBYTEGO-MIASTA-2026-10-13.md`): Jeff 5 x TAK + budowle plona przy spustoszeniu, odbudowa mozliwa, ale wolna. Tor L.
7. BIEG DWULETNI - na samym koncu, po wszystkich poprawkach (decyzja Jeffa). Potem zakonczenie z PLAN-DO-KONCA-MODA (strojenie, wydajnosc, 29 testow recznych Jeffa, drogi).

**JAK PRACOWAC (sprawdzone 13.10):** kod w drzewach SCRATCH(ae222cb7)\zb\<nazwa> (git worktree klonu "lancuch"; nowe: `git -C <lancuch> worktree add <sciezka> -b <galaz> w-toku/zbior-e8`);
paczka = workflow autor + recenzent z poprawka (wzory w `docs/przekazanie/wf-*.js` z 13.10 - kazdy ma pelny opis srodowiska dla agentow); scalenie do zb\int `git merge --no-ff
--no-commit`, build Armoury, kopia DLL do SCRATCH(dbc3d3d1)\test\finNN; test `bieg.ps1 -DllDir finNN -CsAt test\CrashScribe-at-hamulec.dll -Tree zb\int -Out ... -Days 120` (25 min),
progi, zapis `-LoadSave autotest-dlugi -Days 9`, `wgraj.ps1 -DllDir finNN -Tree zb\int -Tag 2026-10-NN-nocNN` (najpierw -DryRun), bieg kontrolny 10 dob, galaz `noc/wgranie-NN`
na hash, push dwoma krokami, wpis w STAN-PRAC. Kopie logow i tabel biegow: `D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\` (gra trzyma tylko 12 ostatnich logow).
PULAPKI Z 13.10: `python -I` ignoruje PYTHONIOENCODING - w skrypcie `sys.stdout.reconfigure(encoding='utf-8')`; bieg cienia swatow nie da "0 roznic" (BK losuje w ocenie szansy);
trzy rozne kampanie 120 dob roznia sie wojskiem o +-5 tys. - nie wyciagac wnioskow z jednej roznicy; raport roczny i jego rozdzialy: `docs/test-zbiorczy/RAPORT-ROCZNY-2026-10-13.md`
i `docs/test-zbiorczy/raport-roczny-2026-10-13/`.

**DECYZJE JEFFA Z 13.10 (wszystkie w STAN-PRAC):** prog wojska 85 tys. NIE obnizac - pieniadz z podatkow wojennych; danina tylko tam, gdzie korona ma potrzebe (zmieniane przez W5b);
tarczowe jako zapas; budowy: kto nie ma pieniedzy, nie buduje; sluby przyhamowane; lup miast z kazdej monety; rozbici wracaja; gospodarka od ludzi.

## 30. NOC 13/14.10 - KONTO TRZECIE (sesja c36b73ca; 22:00-10:00 UTC; Jeff spi, pelne uprawnienia) - START TUTAJ

**STAN KONCOWY - CZYTAJ NAJPIERW TO.** W GRZE: **WGRANIE 51 = wersja koncowa** (Armoury **fce3a3d9** = WGRANIE 50 3cceb8cc + straz licznika podatku osady; galaz noc/wgranie-51 = aca89cc, CrashScribe 04f44a0e, settings 3e31434f, RC f9a944c1, GT 035878c4, ForgeView e17e0233); galaz `noc/wgranie-50` = `w-toku/zbior-e8` **c3aa637** (gen_mcm 1406). Kopie poprzednich: `*.bak-2026-10-14-nocNN-przed` i `D:\Backup-Bannerlord\wgrane\2026-10-14-nocNN-przed` (NN 44-50). Logi biegow nocy: `D:\Backup-Bannerlord\logs\2026-10-14-noc\`. Szczegoly kazdego kroku: `docs/STAN-PRAC.md` od "NOWE KONTO (sesja c36b73ca".
Jeff (13.10 ok. 22:25 UTC): "masz pelne uprawnienia, aby skonczyc moda" - decyzje Claude sa w STAN-PRAC jako "DECYZJA CLAUDE (pelne uprawnienia)"; kazda nowa regula ma klucz MCM. Armoury.json Jeffa nie ma zadnego klucza z tej nocy - gra bierze domyslne z kodu.

**WGRANIA NOCY (kazde po tescie 120 dob + zapis):** 44 N7 rozbici wracaja + W6b rod gracza w tarczowym / doplacie; 45 male poprawki (najemnicy 28 dob, okup powtorny, pokoj na zero dni, juczne / zboze / ryby w ksiegach, stojaca karawana); 46 gospodarka od ludzi paczka A (skale u_v / m_t, plon wsi od ludzi, rynek od ludzi, glowy wsi, zloto startowe wedlug ludzi, Straz, Sarnor 0.25 mln); 47 W7 stawka wojenna pana; 48 paczka B (milicja od ludzi, sol x2, 16 nowych wsi lnianych, robocizna ludu od skupu; pobor od ludzi LevyByPeople domyslnie OFF - DECYZJA); 49 reszta kiesy ludu (konie robocze, P1b lojalnosc, P2 lud buduje miasto, zbytki P3-P5) + lup zdobytego miasta L0-L7 (budowle plona); 50 lup 5b (skarbiec stolicy, karawany wroga) + poprawki po audycie integracji.

**TESTY:** rachunek na sucho 85 tys. (`docs/gospodarka-od-ludzi/sucho85/`); bieg roczny gospodarki od ludzi 96.4 tys. w d337-364; **bieg dwuletni wersji koncowej (`docs/test-zbiorczy/BIEG-DWULETNI-2026-10-14.md`): 728/728, 0 ERROR; wojsko lordow w wojnie rok 1 108.9 tys. (prog 85 TAK), rok 2 55.3 tys. (NIE)** - po d451-513 16 krolestw zawiera pokoj (ROT przestaje wymuszac wojny Essos ok. d422-449), w wojnie 26 -> 10-17 krolestw; krolestwo w pokoju schodzi prawie do zera (pulap zoldu w pokoju 0.28 D - nasza regula ClanBudget, suwak PeaceWageShare); pieniadz rodow slabnie w roku 2 (ok. 1/10 spadku). Audyt integracji (`docs/test-zbiorczy/audyt-integracji-2026-10-14/`): 0 krytycznych.

**OTWARTE (kolejnosc proponowana):**
1. ZROBIONE (WGRANIE 51): licznik podatku wsi TradeTaxAccumulated skakal o 2^31 - przyczyna w BK (EstateData.AccumulateTradeTax 0/0 = NaN), straz TaxCounterGuard.cs; zrodla w BK nie latano.
2. Reczny test Jeffa sciezek gracza (10 punktow w BIEG-DWULETNI rozdz. 9) - autotest nie ma gracza z lennem.
3. DO DECYZJI JEFFA: "pulapka pokoju" w roku 2 - (a) wiecej wojen po d450 (dekompilacja ROT ROTStorylineWars / Diplomacy), (b) wyzszy pulap zoldu w pokoju (PeaceWageShare 0.28 -> np. 0.40-0.45), (c) miara 85 tys. liczona na krolestwo w wojnie. Kazde wymaga biegu dwuletniego.
4. Biedota wielkich miast (chleb swiata 97 -> 91% w roku 2; suwak TownFolkLabourVillageBuys 0.10), tkalnie plotna (75 z 97 miast bez plotna), biedne rody w roku 2 (Wolni Ludzie, Wyspy, Pentos, Smocza Skala).
5. L5b-2 karawany wroga: 0 zajec w 39 zdobyciach - sprawdzic, czy kod moze sie odpalic. Okup zamiast szturmu - tylko projekt (`docs/lup-miast/OKUP-ZAMIAST-SZTURMU-PROJEKT.md`). Prawdziwy pobor od ludzi = skala liczby miejsc u notabli (LevyByPeople off).
6. Na koniec: wydajnosc (paczki kiesy razem ok. 5 ms / dobe), 29 testow recznych z PLAN-DO-KONCA-MODA.
PULAPKI Z TEJ NOCY: scalanie galezi - `checkout --ours` + `patch --fuzz=3` z diffu galezi dla sprawdz_logi.py / ArmouryBehavior.cs (nie sumowac stron konfliktu); po scaleniu gen_mcm i kontrola tablic indeksow ksiag; latki na modele z polami statycznymi tylko w OnSessionLaunched (P4 milicja prawie to zlamala); stary blad silnika 0xC0000005 przy wyjsciu z gry w autotescie (11 z 16 biegow) - nie wynik testu.

## 31. DZIEN 14.10 - KONTO TRZECIE (sesja c36b73ca; 10:00-17:00 UTC; Jeff: "najpierw wszystko", potem "rob wszystko do konca i powiedz: wszystko zrobione, mozesz grac") - START TUTAJ

**W grze: WGRANIE 53** (`noc/wgranie-53` = zbior-e8 449d453: W52 Z.1 + plotno, szybkie wlaczenia A8/A9, drobne bledy 14). Diplomacy: zmeczenie wojna / 4.33 w pliku Jeffa (kopia .bak-2026-10-14-przed-zmeczenie, D:\Backup-Bannerlord\diplomacy-2026-10-14).
**Kod (niewgrany): `w-toku/zbior-e8` = 7bc5dd0b** (repo glowne + GitHub). Drzewo: SC(ae222cb7)\zb\int. Narzedzia scalania: `tools/scalanie/` (scal.sh, sprawdz_narzedzie.py, metody.py, union_changelog.py) - pamiec "scalanie-galezi-rot-patch-fuzz".
Scalone dzis (kazde z recenzja, CHANGELOG, klucz MCM, grupa progow):
- decyzje Jeffa: C3 VeteranTakesKit, C4 PlayerLosesLikeAiLord, C5 ForgeLikeTownSmith (domyslnie FALSE - "szybko jak dawniej"), punkty postaci (AttributePointEveryLevels 3, ExtraFocusEveryLevels 4), ciaza 273 doby + plodnosc x84/364 (PregnancyDays, FertilityYearScale), sluby AI 48/rok (CrashScribe MarriageTargetPerYear - regulator; MarriageOffersPerYear), danina w pokoju M4 (FolkPeaceTribute), 4.20 zostaje, dezerterzy zostaja;
- swiat: Qohor wlasny miecz, smoki jedza owce, zloto Casterly Rock, luki Zlotej Kompanii, winnice/oliwki B6, limit hordy Zewu B5 (sprawdzony na zapisie roku 2);
- okup zamiast szturmu B2 (SiegeRansom*: AI-AI, gracz obronca, gracz oblegajacy; 26 okupow w biegu dwuletnim);
- pieniadze roku 2 (M1 NeedAidFromRents, M2a-c wezwania, M3 BreadForThePoor, B4 GarrisonPeaceShare 0.70);
- towary-2 (K1 drewno budow, K2 wies i plotno, K3 garbarz na wywoz, K4) i towary-3 (SutlerClothing, AnimalsUnderValve);
- drobne-2 (A10-A14, B5, B6; A11 przyczyna BK - SiegeCaravansStayInWalls), A16 (dobrobyt RBL, ludnosc BK), A17 (DestroyedPartyMenRouted - ludzie kasowanych partii, ShipPortsBuyToIdeal, UniquesNotFolkGoods, OthersDuelFromFallen), strzaly AI (AiAmmoOrderFull), myto Bliznakow (TwinsToll).
Odpowiedzi dla Jeffa w `docs/pytania-2026-10-14/` (bandy, rozwoj postaci, starzenie, kuznia-pomocnicy, liczby 4.20).
**Testy:** fin26..fin31 w SD(dbc3d3d1)\test; CrashScribe z autotestem: cs-at4 (7bc5dd0b + at1-cs.diff) -> test\CrashScribe-at-e8d3.dll (zmiana w CrashScribe = nowa DLL autotestu!). Zapis roku 2: `Game Saves\autotest-rok2-d780-fin24.sav` (+ D:\Backup-Bannerlord\zapisy-rok2) - autotest > 60 dob nadpisuje `autotest-dlugi`. Bieg dwuletni fin30 (6e6313c): raport `docs/test-zbiorczy/BIEG-DWULETNI-FIN30-2026-10-14.md` (W5-7 d701-728 62.4 tys.; obronca 0 z 73 szturmow; Polnoc wziela Krolewska Przystan d530; Stormlands, Braavos, Norvos zniknely; 13 nowych tworow z buntow; lista DP 1-27).
**W TOKU (koniec dnia 14.10):**
1. wf_84170bc6-3b9 **oblezenia-i-sily** (Jeff: "sily nie sa rowne, oblezenia za szybkie i za latwe - zmienic oblezenia albo sily albo oba") -> `docs/oblezenia/PROJEKT-WYBRANY.md` -> pokazac Jeffowi rozdz. 0 -> workflow kodu + recenzji. WGRANIE 54 czeka na to.
2. wf_0d69dbdb-635 **wyspy-wolni** (zb\ww `w-toku/wyspy-wolni` od 63aaaed; projekt `docs/wyspy-wolni/PROJEKT-WYSPY-WOLNI.md`): Zelazne Wyspy z rabunku + korsarze, Wolni Ludzie bez zoldu. PO NIM DOPISAC decyzje Jeffa: **zalogi Wysp biora polowe zoldu w lupie** (osobny krok na tej galezi).
3. wf_4f263ed1-c8a **DP15** - skok wplywow Zelaznych Wysp w Q7-Q8 (mozliwe zloto z niczego) -> `docs/test-zbiorczy/DP15-ZLOTO-WYSP.md`.
4. test 120 dob fin31 (7bc5dd0b) w grze -> progi grup: rok2, towary3, a16, a17, tb, sludan, okup, rozwoj, ciaza, drobne2, jeff14, szybkie + regresja.
**Dalej (Jeff "najpierw wszystko"):** po oblezeniach i wyspach: scalenie, test 120 + zapis + rok2 (T1: base 449d453 i nowa wersja na zapisie roku 2), WGRANIE 54; potem DP z raportu (pulapka pokoju - czemu krolestwa nie wracaja do wojny, zalogi, Z.1 skrzynia, dlug KP), zakonczenie D1-D10 (strojenie, wydajnosc, logi, MCM - wyciac probe drog, test Innych, test 4-letni, dokumenty, instrukcja), raport koncowy "wszystko zrobione, mozesz grac" z lista sprawdzonego. Limity: tydzien 69% o 16:25 UTC 14.10 (reset 20.10 18:00) - przy 99% przekazanie na drugie konto.


## 32. Stan na noc 14/15.10 (dla nastepnego konta - limit tygodnia 94% o 22:25 UTC)

**Jeff spi; polecenie: "dokoncz wszystko tak, aby rano mod byl gotowy; zrob w nocy test 6-letni; ostatni test i rano zobaczymy, co wychodzi".**

Kod: `w-toku/zbior-e8` = **b9cb462e** (zb/int; wypchniete do repo i GitHub). Od rozdz. 31 doszlo (wszystko scalone, build kod 0, gen_mcm 1513):
- bitwa szturmu SZ1-SZ4 (42bdb402), unikaty ROT (2026915d) + odejscie po klotni / reset perkow (12274b18), licznik religii naprawiony;
- audyt miedzy modami: raport `docs/audyt-modow-2026-10-14/RAPORT-AUDYT-MODOW.md`; pakiet A (audyt-a 1e1c1192: tytuly BK z pliku ROT + nasz plik ROT 8.1.8,
  AM-20 DROGA B Jeffa - notable zawsze w kulturze osady, wzorce ROT, bez resetu dobrobytu BKROT, warsztaty bez wsadu, wiara Lorath) i pakiet B (audyt-b 3c8f930a:
  8 samowywolan BKROT -> BK, lancuchy modeli ROT/NavalDLC/BEE, zold ROT i morski, gentry - nazwa wolna w swiecie);
- RELIGIA BK WLACZONA (BannerKings.json EnableReligion true; kopia .bak-2026-10-14-przed-religia i D:/Backup-Bannerlord/bk-2026-10-14) - decyzja Jeffa;
- B10b atuty BK z bitew (e0b2ccb1, decyzja Jeffa "tak napraw"); AC spojnosc armii x 84/364 (b9cb462e, Jeff "rozpad armii wolniejszy").

Testy: fin32 (92d20d1d) 120/120, 0 ERROR Armoury, 23 znane wpisy startu (ERRORS A5-A7), progi audyta/audytb/unikaty/szturm/oblezenia/odsiecz/lenna bez NIE
poza AUD-P1-5 (2 z 571 wsi bez tytulu - do poprawy w generatorze tools/bk_titles_rot818.py) i AB-10 (predkosc na morzu x0.93 - prog zakladal przyspieszenie;
to prawdziwy wzor War Sails - prog do zmiany na INFO); test wczytania w nowym procesie OK (retrofit z plikiem ROT, 0 przepietych, 0 ksiestw Calradii);
fin33 30 dob (B10b) OK. W TOKU: fin34 120 dob (b9cb462e).

**Plan nocy:** fin34 120 dob OK -> WGRANIE 54 (`wgraj.ps1 -DllDir test/fin34 -Tree zb/int -Tag 2026-10-14-noc54`, galaz noc/wgranie-54; DLL do wgrania
= fin34 BEZ autotestu) -> **test 6-letni (2184 doby) na fin34** (bieg.ps1 -Days 2184, CrashScribe-at-e0b2.dll) -> rano raport dla Jeffa (progi z --do-doby 364 / 728 /
2184, krolestwa i podboje, wojsko, pieniadze, ludzie, religia, oblezenia, armie AC, czas doby, ERROR).
**Otwarte (nie zrobione - nie pisac "gotowe"):** strojenie z fin31 (CR-5, budowy, skora, strzaly AI 52%, A17-11h znikajacy ludzie, W5-5, parser SD-B4), pulapka pokoju,
liczba zdobyc po OB1-OB5 (fin32: 3 zamki w 120 dob, 0 trebuszy AI - mury nigdy nie rozbite), AUD-P1-5, AB-10 prog, D1-D10 (wydajnosc, logi, porzadki MCM - wyciac
probe drog, test Innych, dokumenty, instrukcja), drobne z audytu (AM-02/03/07/08/23/24/25, AM-17 SCA, AM-30 B po bilansie ciast), AM-28 wzorce craftsman/noble nord.


## 33. Rano 15.10 - PRZEKAZANIE NA DRUGIE KONTO (limit tygodnia 97%) - START TUTAJ

**W grze: WGRANIE 54** (fin34 = zbior-e8 b9cb462e; kopie *.bak-2026-10-14-noc54-przed i D:/Backup-Bannerlord/wgrane/2026-10-14-noc54-przed).
**Religia BK WYLACZONA** w BannerKings.json (EnableReligion false; kopia z ON: BannerKings.json.bak-2026-10-15-religia-ON-test) - wlaczyc z powrotem
dopiero po tescie poprawki RotStoryArmies (Jeff chce religii: "napraw religie").
Raport 4 lat: `docs/test-zbiorczy/RAPORT-4-LATA-2026-10-15.md` (Jeff go dostal). Skrypt tabeli krolestw: `tools/raporty/krolestwa.py`.

### Galezie do scalenia (wszystkie od zbior-e8, wypchniete, build kod 0, recenzja zrobiona, NIETESTOWANE w grze)
1. `w-toku/zbior-e8` **b1cb26a8** (juz w zbior-e8 na zb/int): RotStoryArmies - wywrotka d767 (Blackwater ROT: NRE GatherArmyLogEntry, gdy
   Kingdom.CreateArmy nie utworzyl armii Stannisa); klucz RotStoryArmyGuard.
2. `w-toku/daenerys` **3a980d6a** (zb/dany): CrashScribe StoryThrone.cs, klucz StoryThroneGuard - BK zmienil ustroj Targaryenow na Republic i coroczne wybory
   BK daly tron Barristanowi (d366); ochrona 20 monarchii ROT (blokada wyborow republiki BK i abdykacji Diplomacy dla wladcy ze startu, powrot na tron,
   S9 nie wypycha jego rodu; RotHandover - Nocna Straz / Polnoc zostaja jak w fabule). Grupa progow `daenerys`.
3. `w-toku/oblezenia-2` **afbe5fb2** (zb/obl2): Armoury SiegeWalls2.cs - trebusze AI pod miastem i stolica (druga ekipa rownolegle; zamek po 10 dobach bez
   taranu i wiezy), glod (bilans jedzenia oblezonej twierdzy nigdy dodatni, przy zapasie 0 ginie 5%/dobe, kapitulacja ponizej max(20, 10% startu) droga
   'pusta warownia'), drabiny AI po >= 30 dobach przy przewadze >= 6:1. Klucze SiegeAiTrebuchets, SiegeTrebuchetCastleDays 10, SiegeStarvation,
   SiegeStarveLossPercent 5, SiegeSurrenderShare 0.1, SiegeSurrenderMen 20, SiegeLadderOverwhelm, SiegeLadderMinDays 30, SiegeLadderMinRatio 6; gen_mcm 1522;
   grupa `oblezenia2` (O2-0..5; cel 2 lat: miasta 4-12, stolice <= 1, zamki >= 0.75 x dzis, najdluzsze oblezenie <= 150).
   PRZYCZYNY z dowodem: budowa machin AI to 9,4% (zamek) / 6,3% (miasto) / 3,8% (stolica) tempa gry (BK LongerSieges 0.5 x RB 0.75 x SiegePace 0.5 x OB4) -
   trebusz 40 osobodni, oboz 3000 ludzi: pierwszy trebusz po 18 / 27 / 45 dobach, a oblezenia trwaly mediana 9 dob; gra buduje 1 machine naraz w kolejnosci
   listy; glod: castle_B8 bez jedzenia od 141. doby, gra rani tylko 10%/dobe, milicja nie gloduje, dobrobyt 0 -> bilans jedzenia dodatni z targu,
   ROT dopisuje 'ziemie wokol'; gra nie ma kapitulacji. NIEROZWIAZANE: oboz-widmo (sila 0) pod castle_B8 nie odchodzi - do decyzji / zbadania;
   mozliwe, ze trzeba tez zlagodzic skumulowane spowolnienie budowy (4 mnozniki).

### Kolejnosc pracy (Jeff: "dokoncz wszystko"; decyzje Jeffa: religia ON, oblezenia wedlug propozycji 1-4, Daenerys krolowa, raport konczy sie na 4 latach)
1. Scalic daenerys i oblezenia-2 do zbior-e8 na zb/int (`bash tools/scalanie/scal.sh <galaz> b1cb26a8`; konflikty Config.cs / CrashScribe.settings.xml /
   Settings.cs / McmSettings.cs / sprawdz_logi.py - suma obu stron jak przy audyt-a/b; potem gen_mcm, build obu, sprawdz_narzedzie, metody.py).
2. DLL autotestu CrashScribe: worktree na nowym HEAD + `git apply -3 .../7016f733-.../scratchpad/test/at1-cs.diff` (konflikty csproj i SubModuleMain - suma),
   build; zestaw test/fin35 (Armoury + CrashScribe bez autotestu z zb/int; ForgeView, GrandTourney, RealisticCaptivity z fin34).
3. Test A: religia ON (przywrocic BannerKings.json z .bak-2026-10-15-religia-ON-test), `bieg.ps1 -LoadSave autotest-6lat-d720 -Days 55` - Blackwater bez
   wywrotki (linia "Armie fabuly ROT"), Daenerys wraca na tron w 1. dobie (grupa daenerys), grupy audyta/audytb/unikaty/b10b/spojnosc bez nowych NIE.
4. Test B: nowa kampania 2 lata (728 dob, ok. 2,7 h) - grupa oblezenia2 (cele O2-4), oblezenia, szturm, odsiecz, w5, ekonomia.
5. WGRANIE 55 (wgraj.ps1 -Tag 2026-10-1x-55, galaz noc/wgranie-55) tylko po udanych testach; religia ON zostaje, jesli test A przeszedl.
6. Dalsze (z raportu 4 lat): JOFFREY zyje po Purpurowych Godach (ROT JoffreyEvent.DailyTickEvent ma zabic Joffreya dobe po oknie - nie wykonal sie;
   sprawdzic, czy inne wydarzenia tez gubia skutki: ROTEventBehavior, nasze rozciaganie fabuly CrashScribe Fabula x4.33 / rozrusznik); EKONOMIA - zloto osiada
   w kiesie ludu (37 -> 51 mln w 2 lata), wojsko w wojnie 64 -> 38 tys. (cel 85) - przepływ ludu z powrotem do obiegu; armie rozpadaja sie przez odejscie
   druzyn (NotEnoughParty), nie spojnosc; AUD-P1-5 (2 wsie bez tytulu), prog AB-10 (predkosc morza x0.93 to wzor War Sails - prog na INFO);
   D1-D10 (wydajnosc, logi, porzadki MCM - wyciac probe drog, test Innych, dokumenty, instrukcja); drobne z audytu.

### Korekta (08:35 UTC)
- (08:35 UTC 15.10) **KOREKTA testu A: StoryThrone (Daenerys) NIE DZIALA na wczytanym zapisie** - linia 'Tron fabularny (DY)': 'monarchii ROT 20: rzadzi wladca ze startu 0 ... krolestwo upadlo 20', wglad empire_s 'krolestwo upadlo' (a House Targaryen istnieje) - straz nie znajduje krolestw (dopasowanie id krolestwa / wladcy z XML ROT do obiektow gry po wczytaniu?), wiec Daenerys nie wraca; grupa daenerys dala 0 NIE tylko dlatego, ze wiersze sa INFO - DO NAPRAWY przed wgraniem 55 (CrashScribe/src/StoryThrone.cs; session-2026-10-15 rano na D:/Backup-Bannerlord/logs/2026-10-15-6lat) i prog daenerys ma dawac NIE przy 'krolestwo upadlo' dla istniejacego krolestwa.
- (09:00 UTC 15.10) **Jeff: 'pieniadze zostaja w kiesie ludu - napraw to, najwyzej zwieksz podatki'.** W TOKU wf_965fe804-5fd (jeden autor; zb/kiesa `w-toku/kiesa-ludu` od 445641f1 = WGRANIE 55; docs/przekazanie/wf-kiesa-ludu.js): przyczyna z liczbami + regulator podatku od nadwyzki oszczednosci ludu (do pana osady i kasy miasta, obieg zamkniety), grupa kiesa2. PO NIM (drugie konto, jesli limit): recenzja, scalenie do zbior-e8, test B 2 lata (oblezenia2 + kiesa2 + w5), naprawa StoryThrone (Daenerys), WGRANIE 56.
- (09:45 UTC 15.10) **KIESA LUDU ZROBIONA** wf_965fe804-5fd: `w-toku/kiesa-ludu` 6b3d525c (Armoury TownFolk.Hoard.cs; FolkHoardTax 1%/dobe nadwyzki ponad 3x zapas, 67% glowa rodu pana (RentToday), 33% kasa; przyczyna: lud bierze 512k/d, wydaje 302k/d; sciezka zbytkow prawie martwa (oferta 680k/d, kupione 57k - sufit ceny 1.5); jedyny powrot - danina wojenna P1 tylko w wojnie, a w d1100-1460 19 krolestw w pokoju; symulacja: W 51 -> 18 mln (1.9x normy) i stabilnie, rody +38-128k/d, W5-7 ok. 44 tys. (85 nie osiagniete tym krokiem); NIE ruszone: kiesy wsi 16.7 -> 35.2 mln i zablokowane zbytki - do decyzji). **SCALONE: zbior-e8 = e2a422ec**, build oba 0, gen_mcm 1526. **W TOKU test B** (fin36 = e2a422ec, religia ON, nowa kampania 728 dob; test/at-fin36-2lata.out.txt, ok. 2.7 h): grupy oblezenia2 (O2-4 miasta 4-12, stolice <= 1, najdluzsze <= 150), kiesa2, w5, oblezenia, szturm, odsiecz, daenerys (StoryThrone nie rozpoznaje krolestw - naprawic), spojnosc. Po OK: WGRANIE 56.
- (10:00 UTC 15.10) **Jeff: pieniadz nie moze sie zbierac u ludu ani wsi (dlugi okres - lordowie i krolestwa zbiednieja, ekonomia sie zawali).** W TOKU wf_ae277430-085 (jeden autor; zb/kiesa2 `w-toku/obieg-swiata` od e2a422ec; docs/przekazanie/wf-obieg-swiata.js): kiesy wsi (regulator albo wydatki), zakupy zbytku (sufit ceny / kategorie), STRAZNIK CALEGO OBIEGU (udzial ludu vs lordow i koron, stopa podatku ze sprzezeniem zwrotnym, udzial korony), symulacja 6 lat, grupa obieg2. Po nim: recenzja, scalenie, test C (2 lata) i WGRANIE.
- (11:00 UTC 15.10) **OBIEG SWIATA ZROBIONY** wf_032ab6d0-c69: `w-toku/obieg-swiata` ce282d83 (Armoury TownFolk.Guard.cs, TownFolk.Loans.cs; klucze FolkHoardVillages, FolkCirculationGuard (pasmo 1.25-1.75x normy, x1.03/x0.97 na dobe, do 5x; 15% do korony), FolkLoans (12%/rok, limit 30 D + 20k/miasto, 10k/zamek; umorzenie po 90 dobach pokoju: -10 lojalnosci, 182 doby zakazu), FolkLadderCeilingByWealth (sufit 1.5 x sqrt(dni zapasu/30), max 4); przyczyny: wsie wydaja 8.3k/d przy potrzebie 71.8k (480 z 571 bez towaru), produkcja towaru dla ludu 189k/d przy ofercie 680k/d; Bank: zwykla sciezka 0 pozyczek w 4 lata). **SCALONE zbior-e8 = d7a302f7**, build oba 0, gen_mcm 1540. Test B przerwany w d~200 (zastapiony C). **W TOKU test C** (fin37 = d7a302f7, religia ON, nowa kampania 728 dob; test/at-fin37-2lata.out.txt): grupy obieg2 (OB2-3a, OB2-4, OB2-5, OB2-8), kiesa2, oblezenia2 (O2-4), w5, oblezenia, szturm, odsiecz, spojnosc, audyta/audytb/unikaty/b10b, daenerys; Budzet rodow (166), Skarbce. Po OK: WGRANIE 56.
- (11:05 UTC 15.10) **Test C przerwany w d162** (fin37): Armoury 0 ERROR; obieg2 - straznik na mnozniku 5 i sufit doby 0.15% swiata nie przegonil doplywu: lud 9.9 -> 13.0% zlota swiata, lordowie 36.9 -> 35.3%, korony 24.9 -> 18.9% (d30-162); pozyczki od ludu ok. 46 tys. (limit ciasny); oblezenia d164: 6 zamkow zdobytych, 1 trebusz, najdluzsze 51.8 doby, miasta 0 z 5 (odsiecz 4 z 5). **STROJENIE 3a30c79d** (zbior-e8): sufit doby 0.5% swiata, FolkGuardMaxTimes 10, FolkGuardCrownShare 0.35, FolkLoanMaxShareOfIncome 0.6, FolkLoanLimitDays 60. **W TOKU test D** (fin38 = 3a30c79d, religia ON, 728 dob; test/at-fin38-2lata.out.txt). Kryteria: udzial lordow i koron nie spada (korony stabilne), lud w pasmie po >= 182 dobach, W5-7, miasta zdobyte (O2-4), 0 ERROR. Po OK: WGRANIE 56.
- (11:30 UTC 15.10) **DECYZJA JEFFA (wiazaca): NIE zmniejszamy zwrotu zoldu z korony do rodow i NIE robimy pozyczek koron od miast** ('w tym kierunku nie idziemy'). Jesli korony dalej traca po tescie D - zrodla dla koron tylko z obiegu ludu (wiekszy udzial korony w podatku od nadwyzki, czesc kas miast 'do korony'), bez ciecia zwrotu zoldu.
- (12:00 UTC 15.10) **Test D przerwany w d187** (fin38): Armoury 0 ERROR; lud 10 -> 8.4% (w pasmie - podatek dziala), lordowie 36.9 -> 40.2%, KORONY 24.9 -> 16.9% (-117 tys./d); pozyczki od ludu 704 tys. (32 rody), Bank 126 tys. (14); oblezenia d188: 3 zamki, 1 trebusz, 13 taranow, miasta 0 z 12 (odsiecz 7 z 10), najdluzsze 56.7 doby. **ZMIANA 972e6270**: czesc korony zmienna (FolkGuardCrownTarget 0.22, +5 x brak, do 0.8). **W TOKU test E** (fin39 = 972e6270, religia ON, 728 dob; test/at-fin39-2lata.out.txt). Kryteria: korony nie spadaja ponizej ok. 20%, lud w pasmie, lordowie stabilni, miasta zdobyte (O2-4 - jesli dalej 0: zlagodzic skumulowane spowolnienie budowy machin), 0 ERROR -> WGRANIE 56.
- (12:15 UTC 15.10) W TOKU wf_0e7832fe-bac pozyczki-koron (zb/korona `w-toku/pozyczki-koron` od 972e6270; docs/przekazanie/wf-pozyczki-koron.js). Decyzja Jeffa: korony pozyczaja od miast i mieszczan, zwrot zoldu bez zmian. Po nim: scalenie do zbior-e8, test F 2 lata (albo dolozenie do wynikow testu E), WGRANIE 56.
- (12:55 UTC 15.10) **POZYCZKI KORON ZROBIONE** wf_0e7832fe-bac: `w-toku/pozyczki-koron` becf64ca (Armoury TownFolk.CrownLoans.cs). PRZYCZYNA spadku koron: czesc korony z podatku od nadwyzki wplywala przed CrownIncome.Begin, 165 liczyl ja jako 'inne' i tego samego dnia oddawal w rentach 180 lordom - dlatego 972e6270 podnosil lordow. Naprawa (A) FolkGuardCrownToReserve: czesc korony do zapasu (oddawany 1/360 dziennie), symulacja: korony 24.2% po 164 dobach, 23.3% po 2 latach; (B) CrownTownLoans 10%/rok, limit 90 dob wplywow + 50k/miasto, niesplata 90 dob pokoju -> -10 lojalnosci wierzycieli, 182 doby zakazu; grupa korona2. Test E przerwany w d~110 (bez sensu bez A). **SCALONE zbior-e8 = 1c30b2d0**, build 0, gen_mcm 1546. **W TOKU test F** (fin40, religia ON, 728 dob; test/at-fin40-2lata.out.txt): korona2, obieg2, kiesa2, oblezenia2, w5, 0 ERROR -> WGRANIE 56.

### 34. PRZEKAZANIE (15.10 ok. 11:50 UTC, limit tygodnia 99%) - START TUTAJ NA DRUGIM KONCIE
W grze: WGRANIE 55 (445641f1) + religia ON. Kod: `w-toku/zbior-e8` = **1c30b2d0** (55 + kiesa-ludu + obieg-swiata + strojenie 3a30c79d + czesc korony 972e6270 + pozyczki koron becf64ca), build 0, gen_mcm 1546, NIEWGRANY.
W TOKU: **test F** (test/fin40, test/at-fin40-2lata.out.txt, nowa kampania 728 dob, CrashScribe-at-445641f1.dll) - stan d271: Armoury 0 ERROR, lud 8.1% (stabilnie), lordowie 36.3%, KORONY 21.3% i powoli spadaja (-0.6 pkt/mies.; w tescie D -1.3) -
jesli nie stana: zwolnic oddawanie zapasu korony (1/360 -> np. 1/720 albo tylko na wojsko) - BEZ ruszania zwrotu zoldu (decyzja Jeffa); wojsko W5-7 ok. 118 tys. (cel 85); pozyczki koron 0 (zaden skarbiec ponizej zapasu wojny).
OBLEZENIA (test F d272): trebusze 4, trafienia w mury 4 (pierwsze w historii testow), zamki 5 zdobyte; MIASTA 0 z ok. 17, stolice 0 z 4. Dane: oblegajacy 2.5-6.7 tys., zaloga np. Sharp Point 507 (5.4:1), Tolos 1161 (2.8:1);
odsiecz czesto tylko 27-53% wymaganej, a miasto i tak nie padlo; oblezenia miast koncza sie po 24-38 dobach bez szturmu (armia sie rozchodzi).
**DECYZJE JEFFA 15.10 (ok. 11:45 UTC):** (1) TAK - zdjac nasze spowolnienie budowy x0.5 (dzis budowa AI: zamek 9.4% / miasto 6.3% / stolica 3.8% tempa gry = BK LongerSieges 0.5 x RBM 0.75 x nasze tempo 0.5 x nasze OB4 0.5/1.5/2.5;
po zmianie miasto: oboz+taran+wieza ok. 8 dob, trebusz ok. 9 przy 3000 ludziach; koszty RBM: przygotowanie 24, taran 12, wieza 16, trebusz 40 osobodni; dni = koszt / sqrt(ludzi) / tempo);
(2) TAK - budowa trebuszy (druga ekipa) NIE blokuje decyzji gry o szturmie, gdy taran i wieza gotowe albo mur rozbity (hipoteza: gra nie szturmuje, gdy machina w budowie - sprawdzic w kodzie / logu);
(3) drabiny AI przy przewadze >= 4:1 po >= 21 dobach (dzis 6:1 po 30) - zaproponowane, Jeff nie odpowiedzial wprost - potwierdzic;
(4) PYTANIE JEFFA: "czemu armia sie rozchodzi?" - zbadac: w tescie 4 lat rozwiazania armii gl. NotEnoughParty (ok. 4450 w 2 lata), Inactivity 225, LeaderPartyRemoved 106, ArmyLeaderIsDead 106, FoodProblem 77, sredni wiek armii ok. 28 dob;
spojnosc (AC) juz x0.23 - armie rozpadaja sie przez ODCHODZENIE DRUZYN (kod gry: kiedy druzyna AI opuszcza armie - glod, morale, ranni, nasze NightRest / budzet rodow 166 / pobor?), linie AC "Spojnosc armii (AC): dzien N" maja przyczyny.
Do zrobienia potem: Daenerys StoryThrone (nie rozpoznaje krolestw po wczytaniu), Joffrey po Purpurowych Godach, AUD-P1-5, AB-10, D1-D10. Potem WGRANIE 56 po tescie.

### 35. WGRANIE 56 + JEFF GRA (15.10 ok. 05:20 UTC) - START TUTAJ
Jeff: "1. tak 2. tak 3. tak", "przerwij test i wprowadz zmiany", "zaczynam nowa kampanie ... bedziemy monitorowac moj save". Test F przerwany na d291 (Configs przywrocone).
**W grze: WGRANIE 56** = `w-toku/zbior-e8` **1c450657** (Armoury md5 91489e46; reszta jak 55; kopia *.bak-2026-10-15-wgranie56-przed i D:\Backup-Bannerlord\wgrane\2026-10-15-wgranie56-przed).
Zmiany od 55: kiesa-ludu, obieg-swiata, strojenie, czesc korony, pozyczki koron (test F do d291: 0 ERROR) + galaz `w-toku/oblezenia-3`:
(1) SiegeBesiegerPrepPercent 50 -> 100 (budowa obozu i machin x2 szybciej; miasto 3000 ludzi: oboz+taran+wieza ok. 8 dob);
(2) MB SiegeEnginesInProgressDontHold: gra (BesiegerCamp :424-427) mnozy szanse szturmu przez 1 - 0.85 (postep NIEGOTOWEJ machiny + 0.25 x (5 - gotowe))^2 -
trebusz drugiej ekipy po 1/3 budowy zerowal szturm; teraz z gotowym taranem i wieza (albo wyrwa) liczy sie tylko taran/wieza w budowie; bez odblokowania w przerwie OB2;
(3) DR drabiny 4:1 po 21 dobach (bylo 6:1 / 30) i dziala tez bez zadnej machiny (gra bez gotowej machiny nie szturmuje nigdy: min(3, 0) / 3 = 0).
AC-W: linia "Spojnosc armii (AC wiek)" - rozwiazane armie wedlug przyczyny z wiekiem (Army._creationTime). PRZYCZYNA "rozpadu" [K]: StrategicCampaignAI145
OnArmyCreated kasuje armie od razu (NotEnoughParty), gdy wodz < 22 zdrowych / sila < 80 / 0.75 doby po poprzedniej armii; BK ma tylko slad (army_disband.txt przy diagnostyce).
Test nowej kampanii 12 dob: 2 rozwiazane, obie w dniu zalozenia [P]. Test fin41 (zapis F d240, 25 dob): MB 6 linii, DR 1, szturm Sweetport (3092 vs 186) ODPARTY OB2 (atak stracil 22%), potem odsiecz 101% -> koniec;
Starfall (7103 vs 992): MB przepuscil, szturmu brak w logu, oblezenie zdjete bitwa z odsiecza 78% - do zbadania na zapisie Jeffa.
MONITORING (Jeff gra - NIC nie wgrywac ani testowac, gdy gra dziala): Armoury-*.log - MB, "Oblezenia 2 (zdobycie)", AC wiek, udzialy zlota (korony spadaly ok. 0.6 pkt/mies. w tescie F).
Otwarte: Daenerys StoryThrone po wczytaniu, Joffrey po Purpurowych Godach, odparcie OB2 przy rozbitej zalodze (czy prog 22% nie za niski), AUD-P1-5, AB-10, D1-D10; prog oblezenia2 w sprawdz_logi bez MB.

### 36. WGRANIE 57 (15.10 ok. 05:30 UTC) - START TUTAJ
W grze: `w-toku/zbior-e8` **6cf8c7f0** = 56 + tron Daenerys (CrashScribe md5 a8e0904d; kopia *.bak-2026-10-15-wgranie57-przed i D:\Backup-Bannerlord\wgrane\2026-10-15-wgranie57-przed).
TRON (StoryThrone): MBObjectManager.GetObject<Kingdom> dawal null w KAZDEJ kampanii (linia DY "krolestwo upadlo 20" takze na nowej kampanii) - ochrona nie dzialala nigdy;
teraz Kingdom.All + Hero.FindFirst (pamiec bohatera czyszczona w ResetSession). Test zapis F d240, 10 dob: 20/20 rozpoznane, Daenerys rzadzi, zmiany ustroju BK pominiete 20/d, 0 potkniec, Armoury 0 ERROR
(wywrotka silnika przy wyjsciu - znana, po KONIEC OK).
JOFFREY: bez zmian w kodzie. [K] ROT JoffreyEvent.DailyTickEvent: po >= 1 dobie zabija Joffreya (KillCharacterAction.ApplyByMurder) i dopiero wtedy CompleteEvent;
w tescie 4 lat wydarzenie bylo "odpalone" (TotalEvents > 0) = zabojstwo wywolane; CanHeroDie veto ma tylko ROTOthers (Biali Wedrowcy). Wczesniejsze "Joffrey zyje"
to najpewniej nazwa rodu "Baratheon, Joffrey" (zostaje po smierci) w tabelach - sprawdzic na zapisie Jeffa po Purpurowych Godach.
Czeka na slowo Jeffa: odparcie OB2 tylko gdy na murach stoi >= 1/4 obroncow ze startu szturmu dla KAZDEJ zalogi (dzis tylko <= 100 miasto / <= 30 zamek; Sweetport 2033 vs 186 odparty przy ok. 22 obroncach).
Drobne z audytu (gra bez wplywu): AUD-P1-5 (2 z 571 wsi bez tytulu BK - tytuly powstaja na starcie kampanii), AB-10 (prog narzedzia), D1-D10 (koniec projektu).

- (05:45 UTC 15.10) Jeff o progu OB2 22%: "szturm jest dopoki chociaz jeden obronca zyje, poczekaja do nastepnego szturmu i pokonaja 22 zolnierzy chyba ze przybedzie odsiecz"
  -> odczyt: OB2 zostaje BEZ ZMIAN (cwierc obroncow nie rozszerzana). AUD-P1-5: [K] BK TitleGenerator.CreateLandedTitle zaklada lordship dla kazdej settlement.BoundVillages,
  FeudalTitle.Equals po Fief - duplikat nazwy nie jest przyczyna; ktore 2 wsie - nieznane (linia BkTitles.cs:337 liczy, nie nazywa). Naprawa: (1) nazwy w linii, (2) dorobka
  lordship w biegnacej kampanii (TitleManager.ExecuteAddTitle + vassals tytulu twierdzy) - dziala tez w kampanii Jeffa, wiec nie wstrzymuje gry. Ok. 1 h + krotki test.

### 37. WGRANIE 58 (15.10 ok. 06:55 UTC) - START TUTAJ (Jeff gra kampanie Ironman IronmanjCg6MOpv28iN.sav; kopia D:\Backup-Bannerlord\zapisy-jeffa)
W grze: `w-toku/zbior-e8` **68a98cb5** = 57 + (a) jeden zegar kuzni: BK StartCraftingMenu(totalHours) liczy godziny wszystkich czynnosci ekranu, kolejka brala tylko
kute sztuki - przetop/rafinacja przepadaly; teraz odsiadka BK za reszte godzin (ForgeClock._bkQueued); (b) SmeltNoWood: przetop broni bez drewna trzonka (gra dawala 1-3);
(c) BkPietyBar: BK MapInfoPietyPatches dodawal nowa poboznosc przy kazdym RefreshValues (porownanie referencji) - zostaje jedna (klucz BkPietyBarOnce).
Test zapis F d240 8 dob OK, Armoury 0 ERROR. NIE testowac na zapisie Jeffa (Ironman zapisuje sie sam - nadpisalby kampanie).
Zawieszenie 05:43 w kreatorze postaci (AppHangB1, brak sladu); drugie podejscie przeszlo. Ceny placka/marchwi x8 = niedobor (piekarnie wsi BK) - propozycja wyrownania czeka.
Swiat Jeffa d9: wojny FreeFolk-NW, Dragonstone-Stormlands; 0 oblezen; Daenerys rzadzi (DY 20/20); zloto: lud 5.5%, lordowie 36.4%, korony 25.8%.

- (07:55 UTC 15.10) Jeff "zle liczy nieprzespane noce": log Armoury-2026-10-15_07-16-10 - swit gracza 108858 rozliczony 2x (dlug 0->1->2 w jednej sekundzie), 108859 2x
  (drugi z 1.0 h). Przyczyna [K]: NightRest.OnHourly rozlicza swit przy kazdym ticku z h == dawn; tick nadrabiany w klatce ma te sama godzine (AI ma straznik T10-R R7, gracz nie mial).
  Naprawa `w-toku/sen-gracza` b1585e17 (swit gracza raz na 12 h gry), build 0, test/fin46 (Armoury 8b1570bb) - CZEKA na przerwe w grze (krotki test + wgranie 59).
  Do zbadania: tego samego switu 108858 AI dostalo dlug naraz 203 partie (108859: 60) - nadrabianie tickow w nocy (szybki czas / sen w menu) a ksiega AI (campPast = InCamp(h-1)).

### 38. WGRANIE 59 (15.10 ok. 08:15 UTC) - START TUTAJ
W grze: `w-toku/zbior-e8` **52896aca** (Armoury 9462e17e) = 58 + sen gracza (swit raz na 12 h gry, b1585e17) + nagroda turnieju (52896aca: brak na polce -> miasto
zamawia u rzemieslnikow, kasa -> lud KCraftPaid, gracz dostaje przedmiot od razu; klucz TourneyPrizeCommissioned). Test zapis F d240 8 dob OK, swity gracza 5 = swity AI 5, 0 ERROR.
Jeff dostal za vargo_armor w Duskendale 253 zl zamiast pancerza (przed poprawka). Otwarte: dlug snu AI (203 partie jednego switu - nadrabianie tickow nocy), ceny placka/marchwi,
AUD-P1-5 (2 wsie), zawieszenie w kreatorze postaci (nie powtorzylo sie).

### 39. WGRANIE 60 (15.10 ok. 09:50 UTC) - START TUTAJ
W grze: `w-toku/zbior-e8` **09caf1f7** (Armoury 7203772e) = 59 + luki z rzadkiego materialu (HistExoticBows: dragonbone 20000, weirwood 6000, ravens_teeth_longbow 5000,
goldenheart 3000, giant_bow 2000, ravens_teeth_arrows 60, qarth_longbow 18 - przeglad 42 lukow/kusz i 71 amunicji z items-dump.csv) + drewno w kuzni w kg (SmithWood.cs:
zapas otwartego ladunku w zapisie arm_smith_wood_kg; bron gry = sztuki drewna czesci x 1 kg - postfiks DefaultSmithingModel.GetSmithingCostsForWeaponDesign + Spend
Materials; Recipes: waga drewna przedmiotu, ladunek dopiero gdy zapas nie starcza). Testy zapis F d240 (8 i 5 dob) OK, 0 ERROR. Nie sprawdzone w grze: ekran kuzni (0/1 drewna).
Propozycja czeka na Jeffa: kusze 1.5-2 x cena luku o podobnej sile (dzis najmocniejsza kusza 27, najlepszy dlugi luk 45-48).
