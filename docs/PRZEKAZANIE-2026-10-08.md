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
