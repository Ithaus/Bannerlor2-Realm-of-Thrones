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
