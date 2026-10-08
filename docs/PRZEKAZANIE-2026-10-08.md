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
