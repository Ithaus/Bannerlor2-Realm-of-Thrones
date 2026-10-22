# CHANGELOG galezi w-toku/m107-kronika (Kronika Westeros: smierci z kanonu w wersji lekkiej i zmiany stron Theona, Brienne, Podricka, Jaimego)

## 2026-10-22 (m107-kronika POPRAWKI po recenzjach "kanon" i "technika" - wszystkie PRAWDA poprawione, NIEPEWNE w planie autotestu)

**Mod:** Armoury (+ `tools/kronika/kronika_check.py`, `tools/sprawdz_logi.py`, `docs/KRONIKA-FORMAT.md`) | **Galaz:** `w-toku/m107-kronika` (od `fafe9567`) | **Tor:** L |
**Status:** DO SPRAWDZENIA (nic nie wgrane, gry nie uruchamiano, folderu gry, `test/` i zapisow Jeffa nie dotykano)

| Zarzut | Werdykt | Poprawka | Commit |
|---|---|---|---|
| A1 Cersei w pelnym kanonie nie ginie (Tommen i Myrcella martwi) | PRAWDA | nowy 115.2e (smierc): `succeed:lord_4_2:@dziedzic ; hide:ROTwanderer1`; silnik: token `@dziedzic` (Actions.PickHeir - kandydaci Clan.GetHeirApparents, punkty potem wiek, bez postaci z #move, gracza, ukrytych, dzieci) | `65512528`, `e6a8c4af` |
| A2 Euron, Qyburn, Gora nie gina nigdzie | PRAWDA | nowe okno 115.3 (po 115.2, ostra wersja): glowny log/nic; a smierc Euron -> Yara + Qyburn (hide), b smierc sam Euron, c obie sam Qyburn; nowe okno 115.4 (po 115.2d, log/nic): a smierc Gora -> Amory Lorch. 115.2d bez zmian (nagranie) | `e6a8c4af` |
| A3 `prisoner:lord_4_2:empire_s` nie patrzy na miejsce | PRAWDA | 115.2b/c/e: osobny skladnik `near:lord_4_2:town_EW4:60` | `e6a8c4af` |
| A4 Stannis ginie po uratowaniu Shireen (dziedzic Estermont) | PRAWDA | 081.2a/b: `dead:lord_1_36` (zgodnie z (A): uratowana = gracz zmienil bieg, Stannis zyje ranny) | `e6a8c4af` |
| B1 m28 sprzeczne z V2 nr 72 (niewola Stannisa) | PRAWDA | nowy m40 `kr:072.3~0+2` `prisoner:clan_empire_west_3` po m28 (patrz nizej - dlaczego bez wiersza uwolnienia), komentarze k06, naglowek arm_canon_moves (kolejnosc Captor) | `6146a933` |
| B2 / T2 m32 `ev:JoffreyEvent+5` - Brienne na dworze Lannisterow bez 037.1 | PRAWDA | m32 = `kr:037.2\|kr:037.1+45`; nowy m39 `ev:JoffreyEvent+5` `free:clan_battania_1` przed m32 | `6146a933` |
| B3 m38 `clan:clan_vlandia_1` = Casterly Rock (350 od Przystani) | PRAWDA | m38 `court:clan_nord_1` (Czerwona Twierdza ok. 5); 115.2b/c/e `near:ROTwanderer1:town_EW4:60` | `6146a933`, `e6a8c4af` |
| B4 (a) m37 court zamiast wedrowca (V2 nr 107) | PRAWDA | odstepstwo opisane (FORMAT 8, ponizej) - bez zmiany danych | `6146a933` |
| B4 (b) kolejnosc 104.1 / 103.2 wobec 107.1 | 104.1 NIEPRAWDA, 103.2 NIEPEWNE | bez zmian; 103.2 w planie autotestu (AT4) | - |
| C1 / T5 niewola kampanii w `_held` - linia doby E5 co 10 dni | PRAWDA | RebuildHeld tylko wiersze trybu prolog (p01, p10 - jedyne ze straza HoldKey); jedna poprawka zamiast HeldAny (ten sam skutek) | `65512528` |
| T1 klucz World Changes NIE nie blokuje kr: w #move | PRAWDA | CanonCompanions.TermTrue: `kr:` "nigdy" takze przy `!Chronicle.KeyWorld` (Terms.KrTrue bez zmian); FORMAT 3.3, UWAGA walidatora | `65512528` |
| T3 wiersz smierc wygrywa, gdy cel jest po stronie gracza | PRAWDA (CZEKA - NIEPEWNE) | Chronicle.PickRow: wiersz smierc z celem succeed (obaj) / die / hide po stronie gracza albo bez dziedzica `@dziedzic` nie bierze udzialu (Actions.DeathBlock), powod w linii "smierc kanonu ... nie:"; CZEKA 30 dob w planie (AT4) | `65512528` |
| T4 ochrona "seen" na terminie `kr:NNN.w=x` | PRAWDA | Fires: bez ochrony dla termow z `=` (tylko m02) | `65512528` |
| T6 KW-13: instancja 115.1#... nie trafia do pary | PRAWDA | sprawdz_logi: first_res liczy instancje pod NNN.w; KW-13b z m39/m40 | `151d95e6` |
| - (test) | - | k00 000.7a: `succeed:lord_7_6:@dziedzic` (AT1 sprawdza wybor dziedzica przez gre) | `dc677248` |

### Co sie zmienia dla gracza (slowami gry)

**Kto ginie i kiedy (klucz Chronicle Canon Deaths TAK, World Changes TAK; tylko gdy swiat poszedl jak w ksiazce/serialu):**
- **Stannis** (ok. dnia opowiesci 1325-1440, pod Winterfell) - jak dotad, ale TYLKO gdy Shireen splonela na stosie (okno 081.1, opcja "milcz" - bierna). Gdy gracz ja
  uratowal, Stannis przezywa zlamany i ranny (90 dni), Smocza Skala zostaje przy nim.
- **Cersei i Jaime** (dzien po upadku Przystani, gdy krolowa ja spalila - ostra wersja 115.1): jak dotad z dziedzicem Tommenem albo Myrcella, a TERAZ takze w pelnym
  kanonie, gdy oboje dzieci juz nie zyja (Tommen splonal w 095.1, Myrcella otruta w 086.2) - rod korony przejmuje ten, kogo wybierze gra (Gwardia Krolewska: Meryn Trant,
  Osmund Kettleblack albo Boros Blount), on tez zostaje wladca. Warunek: Cersei w Przystani albo jej okolicy (60) - wolna przy samym miescie (20) albo jencem
  Targaryenow - i Jaime wolny przy niej (60; po powrocie na Poludnie jest teraz dworzaninem w Czerwonej Twierdzy, nie w Casterly Rock).
- **Euron Greyjoy** (dzien po upadku Czerwonej Twierdzy, ostra wersja): ginie na plazy pod Twierdza, gdy jest glowa Greyjoyow i krolem Wysp, wolny, pod Przystania (40),
  a Yara zyje wolna w rodzie Greyjoyow - Yara przejmuje rod i Wyspy. Okno "The Kraken on the Sand".
- **Qyburn** (ten sam dzien): ginie w ruinach Twierdzy (znika z gry), gdy jest dworzaninem korony przy Przystani (20). Razem z Euronem albo osobno ("The Queen's
  Maester"); jak Varys - takze przy kluczu Canon Deaths NIE (smierc postaci z #move = ukrycie, wersja obie).
- **Gora** (dzien po walce braci Clegane z 115.2d): ginie, gdy jest pod Przystania (40) i ma dziedzica - Amory Lorch zyje wolny w jego rodzie (Lorch przejmuje rod
  Clegane). Gracz nie dostaje nowego okna - walke opisalo juz 115.2d. Bez Lorcha Gora zostaje w grze (rod bez dziedzica).
- Smierc nie wchodzi, gdy umierajacy, dziedzic albo ktos, kto ma zginac razem z nim (Jaime, Tommen, Margaery...) jest po stronie gracza (gracz, jego rod, towarzysz,
  druzyna, jeniec gracza) - okno idzie wtedy wersja bez smierci (dotad tekst mowil o smierci, a gra ja pomijala).

**Kto przechodzi do jakiego rodu (zmiany stron jak w ksiazce):**
- **Theon**: po skoku z murow Winterfell (072.3) jest wolny i dalej Greyjoyem (m28), a gdy Stannis zyje, dwa dni pozniej lapia go zwiadowcy Stannisa - jeniec rodu
  Baratheonow ze Smoczej Skaly (m40, loch Smoczej Skaly). Wychodzi tak, jak gra wypuszcza jencow (ucieczka, okup, pokoj); jako jeniec nie plynie z Yara do
  Daenerys (087.1 - sama Yara). Gdy Stannis juz nie zyje - zostaje wolny (moze plynac z Yara, jak dotad).
- **Brienne**: bez Kroniki albo bez okna Harrenhal nie trafia juz na 5 dni na dwor Lannisterow; gdy jest jencem Dzielnych Kompanionow 5 dni po Purpurowych Godach,
  wychodzi na wolnosc do Starkow (m39), a 5 dni pozniej rusza szukac Sansy (m17). Przy zwyklym biegu (037.2) - jak dotad: z Jaimem na dwor Lannisterow, potem szuka Sansy.
- **Jaime**: po powrocie do siostry (113.1 albo upadek Przystani) jest dworzaninem rodu korony w Czerwonej Twierdzy (Gwardia Krolewska), bez wlasnej druzyny - dotad
  byl lordem Lannisterow z siedziba w Casterly Rock.
- Klucz Chronicle World Changes NIE: zadna zmiana strony z okien Kroniki (wiersze #move z samymi `kr:`) nie wchodzi - tak jak przy wylaczonej Kronice.

### B1 - dlaczego m40 bez wiersza "uwolnienie po smierci Stannisa"
Propozycja recenzji (`dead:lord_1_11 -> free`) i jej wariant `kr:081.2~ab` maja ten sam problem: wyzwalacze #move znaja tylko `|` (lub), a wiersz pozniejszej postaci,
ktory zajdzie, wyprzedza wczesniejsze niewykonane. 081.2 (od 1325, po 081.1+5) przychodzi zwykle PRZED 072.3 (od 1340, po 072.2+10) - smierc Stannisa wyprzedzilaby m28 i m40
i uwolnila Theona od Boltonow, zanim 072.1 (wymaga jenca Boltonow) sie pokaze. Do tego `prisoner` na jencu daje "juz jeniec" (POMINIETA), wiec m40 musi isc po ucieczce
(m28). Theon w lochu Smoczej Skaly wychodzi tak, jak inni jency lordowie (ucieczka z gry - PrisonerReleaseCampaignBehavior - okup, pokoj), V2 nr 72 i 87 tego nie wymagaja.

### Odstepstwa (dopisane)
1. (B4 a) m37 Jaime `court:clan_battania_1` zamiast "jako wedrowiec" (V2 nr 107): dworzanin Starkow = jedzie do Winterfell (111.1 pasowanie Brienne tam); `wander` wybiera
   losowe miasto kultury.
2. Odstepstwo 3 z pierwszego wpisu (m32 `ev:JoffreyEvent+5`) - ZASTAPIONE m39 (uzasadnienie "w ksiazce jest wtedy w Przystani" bylo bledne - recenzja B2/T2).
3. A4: wybrany wariant zgodny z (A) (`dead:lord_1_36`), nie z V2 nr 81 ("ginie takze po uratowaniu Shireen, dziedzica decyduje gra") - decyzja Jeffa (A) jest nowsza.
4. 115.3c (sam Qyburn) w wersji obie, nie smierc - jak Varys 114.1 (hide postaci z #move nie jest "smiercia z dziedzicem"; walidator: wiersz smierc bez die/succeed = UWAGA).
5. Lektor: 115.2b/c/e maja tekst identyczny z nagranym 115.2a - kopie pliku `kr_115_2a.ogg` jako `kr_115_2b/c/e.ogg` + wiersze manifestu (ten sam md5 tekstu i pliku;
   Lektor i walidator sprawdzaja md5, nagraj_kronika.py uzna je za nagrane) - zamiast trzech nowych nagran tego samego tekstu.
6. T3 dane: zamiast `!prisoner:<dziedzic>` w kazdym wierszu (zaweza warunek do rodu) - regula w silniku (DeathBlock: tylko postac po stronie gracza; jeniec AI moze dziedziczyc
   jak w grze).

### Testy (bez gry)
- Build Release (GameLibs lancuch/libs): Armoury 0 bledow, 7 starych ostrzezen; kod 0.
- `kronika_check`: **WYNIK OK** - okien 221, wierszy 374, wyborow 43, tekstow 75, par 47 | bledy 0, uwagi 21 (9 lektor - wiersze bez nagrania; 12 wierszy #move z samymi `kr:`:
  m28-m38, m40). `kronika_silnik`: WYNIK OK. `gen_mcm`: bez zmian (Settings.cs nietkniety). `sprawdz_narzedzie tools/sprawdz_logi.py`: dispatch poza main [] | zle wstawki [] |
  zdublowane def [] | zdublowane grupy [] | kompilacja ok.
- Kopia CanonCompanions.Parse (`cc_parse.py`): 50 wierszy #move, zlych 0; id m39/m40 bez kolizji (m104-e5, m106-fach, m106-wozy, m107-gospodarka, n33-calosc).
- `sym_ruchy.py` v2 (scratchpad 7aee5f12/m107k): **13/13** - kanon (m40 dwa dni po m28, ucieczka z gry przed 087.1), Kronika NIE (m32 i m40 nigdy, m39 bez zmian), **2b World
  Changes NIE (T1)**, 047.2, alarm 032.1=a (m39 bez zmian), pozne 037.1 (m39 wyprzedza m31, m17 wyprzedza m32), 072.3a (m40 nigdy), **6b Theon jencem przy 087.1** (m29 nigdy),
  113 przed 107, 087.1 bez Theona, niewola Brienne przed Godami (m39 uwalnia do Starkow), stary zapis z dnia 1500 (m39 LaterExecuted, m40 "seen"), **11 T4** (m02 z `kr:032.1=b`
  po wczytaniu). Ta sama kopia ze stara logika T1/T4: 2b i 11 ZLE (testy wykrywaja blad).
- `neg4.py` v2: 23/23 (nowe: `succeed:<b>:@dziedzic` dobry; `@dziedzic` jako umierajacy i w `die` - BLAD).
- `test_kw_m107.py` v2: 21/21 (nowe: T6 instancja 115.1#... przed/po #m38 - TAK/NIE, para 072.3-m40 - po/przed oknem, linie T3 i @dziedzic bez NIE, KW-13b z m40);
  ze stara wersja sprawdz_logi 3 ZLE. `kr-fix/test_kw_fix.py` 46/46.

### Ryzyko / co sprawdzic (kontrola calosci, CLAUDE.md 8.0)
- **Regresje:** T1 zmienia tylko TermTrue wierszy #move (klucz World Changes NIE - domyslnie TAK; u Jeffa TAK) - m02, m04, m18, m22 ruszaja wtedy od starych wyzwalaczy,
  wiersze z samymi `kr:` POMINIETA w pierwszej dobie (na stale, jak przy Kronice NIE). T3 - PickRow tylko dla wierszy smierc (14 w paczkach k01-k10, 17 z k00); inne
  wiersze bez zmian. T4 - jedyny term z `=` to m02 (stary wiersz). T5 - HeldRow/HeldArg/HeldDay czytaja tylko Tyriona i Jaimego (p01/p10), HeldCount tylko linia doby E5.
- **Zapis Jeffa (103.2, kampania od 20.10):** nowe wiersze m39/m40 dostana `seen` = dzien wczytania; m39 (`ev:JoffreyEvent+5`) - Gody jeszcze przed nim (jak m17); m40 -
  072.3 daleko przed nim. Okna 115.3/115.4 - dzien opowiesci 2000+. 081.2 (1325+) - jeszcze przed nim. Format zapisu bez zmian.
- **Kolizje:** m107-gospodarka - wspolne pliki Settings.cs/McmSettings.cs/sprawdz_logi.py: ta galaz Settings.cs nie ruszala; sprawdz_logi - inna grupa (m105kronika), po
  scaleniu sprawdz_narzedzie. m104-e5 - straz p01/p10 bez zmian (T5 zaweza `_held` dokladnie do nich). Lista chronionych (FORMAT 15) bez zmian (Euron, Gora, Lorch,
  Qyburn poza lista; Cersei done:JoffreyEvent - spelnione przy 115).
- **Spojnosc:** @dziedzic - gra wybiera dziedzica w chwili okna (PickRow) i ponownie w akcji (Succeed); miedzy nimi moze wybrac innego (np. ktos umarl) - zawsze z rodu,
  bez postaci z #move i gracza. Gwardia Krolewska jako krol po Cersei jest poza kanonem (w serialu korone daje Wielka Rada) - ale 117.1 i tak przychodzi po smierci
  Daenerys. Promien 60 (Cersei-jeniec, Jaime) do kalibracji z KW-17b. Theon w lochu Smoczej Skaly - siedziba niezaleznie od wlasciciela (SeatOf); gdy Smocza Skala jest
  gracza - druzyna Stannisa, bez niej CZEKA do 60 dob.
- **Cudzy kod:** Clan.GetHeirApparents (gra: pomija wylaczonych; uzyte juz w Ransom178 i CanonCompanions.BeforeHeroKilled) - kompiluje sie z bibliotekami 1.4.8;
  kolejnosc wyboru (punkty, potem wiek) jak Ransom178.HeirOf. EndCaptivityAction -> TakePrisonerAction tej samej postaci dwa dni pozniej (m28 -> m40) - jak m02 -> m35 (Jaime: uwolnienie, potem Kompanioni).

### Plan autotestu (glowna sesja; gdy Jeff nie gra; kopia logow Jeffa przed 1. biegiem; DLL z HEAD do test/) - uzupelnienie AT1-AT3
- **AT1** jak w pierwszym wpisie (linia startu teraz `Canon Deaths TAK (wierszy smierci 17)` - 14 w k01-k10 + 3 w k00), z jedna zmiana linii 000.7: `akcja 000.7 #1 succeed:lord_7_6:@dziedzic -> OK (dziedzic glowa rodu clan_nord_6 (... -> <dziedzic>)
  (@dziedzic - wybor gry) | ... | smierc kanonu)` - albo (gdy gra nie ma dziedzica Hornwoodow) `smierc kanonu 000.7a nie: succeed:lord_7_6:@dziedzic: @dziedzic: rod
  clan_nord_6 bez dziedzica ...` i `log 000.7` (wiersz glowny) - wtedy NIEPEWNE do wyjasnienia (GetHeirApparents w ROT). `sprawdz_logi --grupa m105kronika` 0 NIE.
- **AT2** bez zmian (Canon Deaths NIE; `wierszy smierci 17, flaga testu ...`).
- **AT3** (prolog 230 dob, bez flagi) jak w pierwszym wpisie + `Canon Deaths TAK (wierszy smierci 14)` + linia startu CanonCompanions `wiersze #move nowe w zapisie w tej sesji 50`; linia doby E5 "co 10 dni" po prologu tylko
  przy niewoli p01/p10 (T5 - wczesniej tez przy niewoli Theona z m19); KW-13b z m39 (POMINIETA albo OK po Godach, jesli bieg je osiagnie).
- **AT4 (NIEPEWNE z weryfikacji, do sprawdzenia w dlugim tescie albo grze Jeffa - nie da sie wymusic w 230 dobach):**
  - B4 (b) 103.2: czy 103.1 czeka na `own:castle_V5`/`own:town_V5` do dnia 2100-2130 (linie "czeka" 103.1, potem 103.2 przepada na `clanof`); jesli tak - decyzja
    o `przed=kr:107.1` w 103.2.
  - T3 CZEKA: `akcja 081.2 #N succeed:lord_1_11:lord_1_111 -> CZEKA (... w bitwie albo oblezeniu | smierc kanonu)` dluzej niz 30 dob (prog KW-17 a) - jesli sie zdarzy,
    rozwazyc dluzszy limit dla wierszy smierc.
  - Theon m40: `zmiana strony #m40 - Theon Greyjoy ... akcja prisoner:clan_empire_west_3 ... jeniec Dragonstone` 2 dni po `#m28` (tylko gdy 072.1-072.3 sie pokaza - Theon
    zwykle ucieka z lochu Boltonow sam, wtedy 072.1 przepada).
  - Promienie: `smierc kanonu 115.2b/c/e nie: near:...` (KW-17b) - czy 60 nie odrzuca Cersei/Jaimego, ktorzy sa w Przystani.

### Nagrania do dogrania (lektor EN David v2; teksty nagranych okien bez zmian - md5 bez bledow)
`kr_081_2a`, `kr_081_2b`, `kr_094_1a`, `kr_094_2a`, `kr_104_2a`, `kr_116_2a` (z pierwszego wpisu, teksty bez zmian) oraz nowe `kr_115_3a`, `kr_115_3b`, `kr_115_3c` -
razem 9 nagran, 3772 znaki. `kr_115_2b`, `kr_115_2c` (byly na liscie) i nowy `kr_115_2e` - kopie `kr_115_2a.ogg` (ten sam tekst), bez nagrywania.

**Status:** DO SPRAWDZENIA (nic nie wgrane).

## 2026-10-22 (m107-kronika: (A) smierci z kanonu, (B) przejscia do innych rodow jak w ksiazce)

**Mod:** Armoury (+ `tools/kronika/kronika_check.py`, `tools/kronika/kronika_silnik.py`, `tools/sprawdz_logi.py`, `docs/KRONIKA-FORMAT.md`) | **Galaz:** `w-toku/m107-kronika`
(od `5da5f304` = WGRANIE 103.2 + `0e9fe92f` nagrania lektora) | **Tor:** L | **Status:** DO SPRAWDZENIA (nic nie wgrane, gry nie uruchamiano, folderu gry, `test/`
i zapisow Jeffa nie dotykano; autotest AT1-AT3 - plan na koncu)

**Problem (decyzje Jeffa 22.10):**
(A) "tak, gina postacie, jesli historia mowi, ze gina; jesli bedzie inne zakonczenie i nie gina, to nie gina" - kanoniczne smierci glow rodow i wladcow (Stannis, Doran,
Walder Frey, Mace, Randyll, Cersei, Daenerys) byly tylko w wierszach `wersja=pelna`, wylaczonych kluczem ChronicleFullVersions=NIE (domyslnie) - w wersji lekkiej nie ginal
nikt z nich, nawet gdy swiat szedl dokladnie jak w ksiazce.
(B) "Kronika: maja przechodzic do innych rodow jak w ksiazce" - zmiany stron Theona (ucieczka od Boltonow, z Yara do Daenerys, pod Winterfell), Brienne (Kompanioni, dwor
Lannisterow z Jaimem, przysiega Sansie), Podricka (z Brienne do Sansy) i Jaimego (Harrenhal, powrot, Polnoc, powrot do siostry) byly tylko w tekstach okien i flagach.

**Przyczyna:** (A) silnik K0 znal tylko lekka/pelna/obie, a `succeed` dzialal tylko w wierszu pelna (wyjatki 42, 49, 52, 80); (B) zmiany stron postaci z wierszami `#move`
robi tylko CanonCompanions (P1), a tabela `arm_canon_moves.tsv` nie miala wierszy po K10 dla tych okien (FORMAT 8: "NOWY wiersz #move w paczce silnika").

| Commit | Co |
|---|---|
| `97f8d01e` | silnik: wersja `smierc`, klucz ChronicleCanonDeaths (gen_mcm), termy `near:`/`region:`, `kr:NNN.w~R`, linie "smierc kanonu", ochrona "seen" wierszy #move; kronika_check, kronika_silnik, sprawdz_logi KW-0b/KW-13b/KW-17/KW-17b, FORMAT |
| `e3dd6afd` | dane A: 9 wierszy pelna -> smierc z warunkami "swiat jak w ksiazce", nowy 095.1a (Mace), wiesc 095.1_N3 |
| `e14b0491` | dane B: wiersze #move m28-m38, nowy wyzwalacz m17, 14 par #para (kr), naglowek arm_canon_moves |
| `c2209316` | silnik: wiersz #move dopisany przed wykonanym wierszem tej samej postaci - POMINIETY (LaterExecuted); flaga testu AT2 (Canon Deaths NIE bez MCM) |
| `488082e7` | testy: okna k00 000.7-000.9, `arm_canon_moves_test.tsv` (t01/t02) |

### Zmiana (A) - smierci z kanonu w wersji lekkiej

**Silnik:**
- `Settings.ChronicleCanonDeaths = true` (MCM "Chronicle Canon Deaths", opis EN: postacie gina jak w ksiazkach i serialu, ale tylko gdy swiat poszedl tak samo; inaczej
  zyja; Off = gina tylko w wersjach pelnych). `Chronicle.KeyCanon`.
- Wersja `smierc` (tylko wariant): `Chronicle.VersionOk` = `obie || (lekka && (!Full || !HasFull)) || (pelna && Full) || (smierc && World && (Full || Canon))` - dla
  lekka/pelna/obie wynik bez zmian wobec K0 (sprawdzone galaz po galezi). `HasFull` liczy tez smierc (przy Full TAK okno z wierszem smierc nie bierze wierszy lekka -
  jak okno z wierszem pelna; zadne z 8 okien nie ma wierszy lekka). Data: wiersz glowny `smierc` = ZLY WIERSZ.
- Akcje (Chronicle.Canon.cs): `succeed` w wierszu smierc (i w opcjach tylko wierszy pelna/smierc) - przez `DeathWhy` (Canon albo Full TAK), reszta jak w pelnej (dziedzic
  glowa rodu i wladca, potem smierc, CanDie, lista chronionych, CZEKA w bitwie); `die` glowy rodu z wiersza smierc - POMINIETA "wiersz smierc: glowa rodu ginie tylko
  przez succeed"; own/ruler/leader/alliance/truce w wierszu smierc - POMINIETE (FullOnlyWhy).
- Termy (Chronicle.Terms.cs): `near:<bohater>:<osada>:<1-500>` (HeroPos: osada bohatera, loch/druzyna, ktora go wiezi, jego druzyna; ukryty/bez pozycji = falsz, martwy =
  nigdy), `region:<bohater>:<krolestwo>` (najblizsza osada polozenia).
- Logi: linia startu `..., Player Choices X, Canon Deaths X (wierszy smierci N)`; przy rozstrzygnieciu okna z wierszami smierc `smierc kanonu NNN.wX TAK - okno ... wierszem
  smierci (klucze: ...)` i `smierc kanonu NNN.wX nie: <pierwszy falszywy term> | okno ... wierszem ...`; akcje wiersza smierc z dopiskiem `| smierc kanonu`; succeed/die
  POMINIETE z wiersza smierc - `KANON: smierc z NNN.wX nie weszla - <akcja> (<powod>)`.

**Dane (tekst okien BEZ zmian - md5 lektora bez zmian):**

| Wiersz | Kto ginie -> dziedzic | Dopisane warunki ("swiat jak w ksiazce") | Zapas bez smierci |
|---|---|---|---|
| 081.2a | Stannis lord_1_11 -> Andrew Estermont lord_1_111 (wyrok Brienne) | head + leads dragonstone, !prisoner, `near:lord_1_11:town_B2:120`, Winterfell u Boltonow, dziedzic w rodzie, **`near:ROTwanderer9:town_B2:150`** | 081.2 (rana 90 dni) |
| 081.2b | Stannis -> lord_1_111 (w bitwie) | jak 081.2a bez Brienne | 081.2 |
| 086.1a (opcje b, c) | Doran lord_3_1 -> Obara lord_3_10 | head, !prisoner, `near:lord_3_1:town_A1:40`, Obara w rodzie, peace:aserai:vlandia | 086.1 b/c (Doran ranny) |
| 094.1a | Walder Frey lord_1_5 -> Stevron lord_1_1_8 | head, !prisoner, `near:lord_1_5:ROT_town3:20`, Stevron w rodzie | 094.1 (ranny) |
| 094.2a | Freyowie (lord_1_1_9, 1_1_11, 1_33) | `flag:k094_walder_dead` (tylko po smierci z 094.1a) | 094.2 |
| 095.1a (nowy, opcja b) | Mace lord_1_7 -> Willas lord_1_24 (+ Margaery, Loras, Tommen) | cw:reach, head, !prisoner, `near:lord_1_7:town_EW4:30`, Willas zyje w rodzie | 095.1 (Mace zyje) |
| 104.2a | Randyll lord_4_3 -> Talla lord_4_3_1 (+ Dickon) | Talla w rodzie (niewola u Targaryenow juz byla) | 104.2b / 104.2 |
| 115.2b, 115.2c | Cersei lord_4_2 -> Tommen lord_4_7 / Myrcella lord_4_10 (+ hide Jaime) | dziedzic w clan_nord_1, `near:lord_4_2:town_EW4:20 \| prisoner:lord_4_2:empire_s`, Jaime wolny | 115.2a/d/glowny |
| 116.2a | Daenerys lord_1_14 -> Szary Robak lord_1_47 (Jon jencem) | Szary Robak w rodzie, oboje wolni, `near:` obojga przy town_EW4 (30) | 116.2 (Jon w lancuchach) |

095.1a = wariant bez wlasnego tekstu (tekst i nagranie 095.1, lektor=0), wlasne pytanie `{=ARM_KR_095_1a_Q}` (ten sam tekst co 095.1), te same skutki okna co 095.1
(wariant ma wlasne skutki), opcje `095.1a a` (ostrzezenie - Mace, Margaery i Loras uchodza; `relp:lord_1_7:10` dopisane do relacji 095.1 a) i `095.1a b` bierna:
`succeed:lord_1_7:lord_1_24 ; die:lord_1_8 ; die:lord_1_44 ; die:lord_4_7 ; relk:clan_empire_west_1:clan_nord_1:-80 ; wiesc:095.1_N3 ; flag:k095_burned ; flag:k095_mace`.
Nowe teksty (bez lektora): 095.1a_Q, 095.1a_Ca/Ha/Cb/Hb, 095.1_N3. Komentarze paczek k07-k10 zaktualizowane (PELNA -> SMIERC).
Lista chronionych (FORMAT 15) bez zmian: Stannis (done:EastwatchEvent), Walder (done:RedWeddingEvent), Cersei (done:JoffreyEvent), Daenerys (done:DanyInvasionEvent) maja
wymagany term osobnym skladnikiem w wierszu glownym; Doran, Mace, Randyll, Margaery, Loras, Tommen - poza lista.

### Zmiana (B) - zmiany stron jak w ksiazce

**Silnik:** term `kr:NNN.w~R` (okno rozstrzygniete wierszem z R: `0` = glowny, litery = warianty; inny wiersz = nigdy) w warunkach okien i w wyzwalaczach #move
(Terms.KrTrue/RowIn, KnownFirst i Chronicle.ResolvedDay licza `+N` tylko przy wierszu z R; okno powracajace = nigdy). Dwie ochrony wierszy dopisanych do trwajacej kampanii
(CanonCompanions; projekt ich nie mial - patrz "Odstepstwa"):
1. `seen` (zapis arm_canon_companions, linie `seen id dzien`; starsza DLL pomija prefiks): dzien, od ktorego wiersz jest w kampanii (nowa kampania - start, stary zapis
   albo nowy wiersz - dzien wczytania). Term `kr:` okna rozstrzygnietego przed tym dniem, ktorego zaden wiersz nie widzial prawdziwym (brak pamieci `_first`), jest dla
   tego wiersza "nigdy" (wiersz w toku - bez ochrony). Linia startu CanonCompanions: `| wiersze #move nowe w zapisie w tej sesji N (dzien D; ...)`.
2. `LaterExecuted`: wiersz, za ktorym w pliku jest WYKONANY pozniejszy wiersz tej samej postaci, zostal dopisany po nim - POMINIETA "wyprzedzona przez #X wykonana
   wczesniej (wiersz dopisany ...)". W biegu kampanii wczesniejsze wiersze sa zawsze rozstrzygniete przed wykonaniem pozniejszego (Moves: wyprzedzone, "nigdy", CZEKA =
   break), wiec regula nie dotyka zadnej sciezki dzisiejszej gry (pominiete wiersze - "nigdy", tryb prolog/classic - sie nie licza: m01/p10).

**Dane (arm_canon_moves.tsv; kazdy blok pod ostatnim wierszem postaci; 14 par `kr` w plikach paczek okien, k10 - nowa sekcja #para):**

| Wiersz | Postac | Wyzwalacz | Akcja |
|---|---|---|---|
| m28 | Theon | `kr:072.3` | `free:clan_sturgia_1` |
| m29 | Theon | `kr:087.1~a` | `clan:clan_empire_south_1` |
| m30 | Theon | `kr:108.1` | `clan:clan_battania_1` |
| m31 | Brienne (pod m16, przed m17) | `kr:037.1` | `prisoner:ROT_town32` |
| m32 | Brienne | `kr:037.2\|kr:037.1+45\|ev:JoffreyEvent+5` | `free:clan_vlandia_1` |
| m17 (zmiana) | Brienne | `ev:JoffreyEvent+10\|rotday:1150` (bylo `done:RedWeddingEvent\|dead:lord_5_1`) | `wander:river` |
| m33 | Brienne | `kr:083.1~0b` | `court:clan_battania_1` |
| m34 | Podrick (pod m07) | `kr:083.1~0b` | `court:clan_battania_1` |
| m35 | Jaime (pod m02) | `kr:037.1` | `prisoner:ROT_town32` |
| m36 | Jaime | `kr:037.2\|kr:037.1+45` | `free:clan_vlandia_1` |
| m37 | Jaime | `kr:107.1` | `court:clan_battania_1` |
| m38 | Jaime | `kr:113.1\|kr:115.1` | `clan:clan_vlandia_1` |

Pary: 037.1-m31/m32/m35/m36, 037.2-m32/m36 (k04), 072.3-m28 (k06), 087.1-m29, 083.1-m33/m34 (k07), 108.1-m30, 107.1-m37, 113.1-m38, 115.1-m38 (k10). Petli brak (zadne
z tych okien nie czeka na `mv:` tych wierszy). Kopia CanonCompanions.Parse: 48 wierszy #move, zlych 0; najwyzsze id na galeziach m27/p10 - m28-m38 bez kolizji.

### Narzedzia
- `kronika_check.py`: wersja `smierc` (BLAD w wierszu glownym; BLAD, gdy glowny okna nie jest `obie`; z akcji tylko-pelnych tylko `succeed`, takze w opcjach tylko wierszy
  pelna/smierc; UWAGA bez die/succeed w skutkach i opcjach; lektor jak obie), termy `near`/`region`, `kr:NNN.w~R` (warianty istnieja, nie razem z `=x`, BLAD dla okna
  powracajacego), wyzwalacze #move (rodzaj znany CanonCompanions.BaseTrue; `kr:` z opcja i wariantami, ktore istnieja).
- `kronika_silnik.py`: rodzaje termow z danych (kolumny warunkow i koniec ukrycia) wobec Terms.Kinds/PlayerKinds - 26 rodzajow, bledy 0.
- `sprawdz_logi.py --grupa m105kronika`: KW-0b (pole Canon Deaths, wierszy smierci >= 1; log sprzed m107 - INFO), KW-13b (stan m17, m28-m38, INFO), KW-17 (a: succeed z wiersza
  smierc POMINIETA 0 - takze CZEKA ponad 30 dni; b: wiersz smierci przy Canon NIE i Full NIE 0; c: przy World Changes NIE 0; d: die glowy rodu z wiersza smierc 0; e:
  own/ruler/leader/truce OK przy Full NIE z linii startu 0), KW-17b (odrzucone wiersze smierci z pierwszym falszywym termem, die pominiete, linie KANON - INFO).
  KW-13/KW-15/MP-7 bez zmian (obejmuja nowe pary i ruchy). `sprawdz_narzedzie.py`: dispatch poza main [] | zle wstawki [] | zdublowane def [] | zdublowane grupy [] | kompilacja ok.

### Testy (bez gry)
- Build Release (GameLibs lancuch/libs): Armoury 0 bledow, 7 starych ostrzezen (BattlefieldLaw, CampWalk, SideVillageFief, StartCalibration x3, StoryWindow.CancelText); kod 0.
- `kronika_check`: **WYNIK OK** - okien 219, wierszy 367, wyborow 43, tekstow 75, par 46 | bledy 0, uwagi 18 (8 lektor: wiersze smierc z lektor=1 bez nagrania; 10 wierszy
  #move z samymi `kr:` - m28-m31, m33-m38, swiadomie: przy Kronice NIE zostaje stan sprzed m107). `kronika_silnik`: WYNIK OK.
- `neg4.py` (scratchpad 7aee5f12/m107k): 20/20 - kazda nowa regula walidatora w obie strony. Stary `kr-final/neg3.py` - te same bledy tresci (6) co 21.10 (dodatkowe BLEDY
  pochodza z prawdziwego arm_canon_moves w syntetycznym repo z 3 paczkami - nowe wiersze kr: bez okien w tych paczkach).
- `sym_ruchy.py` (wierna kopia regul Moves/Fires/TermTrue/ApplyAction w skrocie, dane z repo): 10/10 - 1 kanon (wszystkie 18 wierszy po kolei: m35 813, m36 828, m17 1092,
  m33/m34 1421, m28 1361, m29 1461, m30 2201, m37 2121, m38 2211), 2 Kronika NIE (wiersze z samymi kr: pominiete, m32 od ev:JoffreyEvent+5, m17 +10), 3 sciezka 047.2
  (083.1a - m33/m34 nigdy), 4 alarm 032.1=a (m31/m35/m36 nigdy), 5 pozne 037.1 (m32 wyprzedza m31, Jaime dalej), 6 072.3a bez Stannisa, 7 113 przed 107 (m38 wyprzedza m37),
  8 087.1 bez Theona (m29 nigdy), 9 niewola Brienne przed Godami (m32 uwalnia przed m17), 10 stary zapis z wierszami dopisanymi w dniu 1500 (m31/m32 - LaterExecuted,
  m28/m33-m36 - "seen", m29/m30/m37/m38 normalnie).
- `test_kw_m107.py`: 15/15 (log dobry 0 NIE; KW-0b, KW-17 a-e, KW-13 para m28 - kazdy zly log NIE tylko w swoim progu; log sprzed m107 - INFO). Stary `kr-fix/test_kw_fix.py`
  46/46 (bez regresji progow KW-0..KW-16).

### Odstepstwa od projektu (kazde z powodem)
1. 081.2a: dopisany `near:ROTwanderer9:town_B2:150` - tekst wiersza mowi, ze Stannisa znajduje Brienne (FORMAT 16.3: tekst nie twierdzi niczego, czego warunki nie
   sprawdzaja). Po (B) Brienne zwykle wedruje po Dorzeczu (m17), wiec smierc Stannisa idzie zwykle wierszem 081.2b (smierc w bitwie, ten sam succeed).
2. Walidator: okno z wierszem `smierc` wymaga wiersza glownego `obie` (projekt: obie/lekka) - glowny `lekka` przy Full Versions TAK nie bierze udzialu (HasFull liczy smierc),
   wiec okno zostaloby bez wiersza. Zadne okno nie ma dzis glownego lekka.
3. (ZASTAPIONE poprawka B2/T2 - m39, wpis wyzej) m32: dopisany `|ev:JoffreyEvent+5` (lagodzenie z projektu) - bez niego Brienne-jeniec z m31 przy Godach przed 037.1+45 blokowala m17 (CZEKA 60 dob, potem 064.1, 904.1,
   047.2 przepadaja). Koszt: przy 037.1, ktore nie przyszlo (alarm 032.1=a, Kronika NIE), Brienne na 5 dni trafia na dwor Lannisterow (siedziba rodu) przed m17 - w ksiazce
   jest wtedy w Przystani, wiec zgodne z kanonem.
4. Ochrony "seen" i LaterExecuted (rozdz. B) - projekt ich nie mial; bez nich nowe wiersze w zapisie, ktory minal okna 037/072/083 (dzien opowiesci 800+), ruszylyby wstecz
   (np. Brienne z wedrowki do niewoli i na dwor Lannisterow). Zapis Jeffa (WGRANIE 103.2, kampania od 20.10) tych okien nie minal - ochrona na przyszle dopisywanie wierszy.
5. KW-17 (a) liczy tylko `succeed` POMINIETY (smierc glowy rodu, ktora warunki gwarantuja); `die` pominiete w wierszach smierc (np. Frey juz martwy) - INFO w KW-17b.
6. Linie: osobne `smierc kanonu ... TAK/nie` zamiast zmiany linii "okno ... pokazane" (stare regexy KW-2..KW-13 bez zmian).
7. 095.1a a: podpowiedz mowi o Mace'u i `relp:lord_1_7:10` (ostrzezeni Tyrellowie uchodza razem z ojcem).
8. Test AT1/AT2: 000.8a warunek `kr:000.7` (flaga 000.7 nie powstaje, gdy wygra 000.7a) i `die:ROTwanderer35` (wedrowiec D); AT2 przez plik flagi
   `CrashScribe/armoury-kronika-kanon-nie.flag` (tylko z flaga testu i trybem 1/2 - w grze Jeffa bez znaczenia).
9. `kronika_silnik.py` - dodatkowo rodzaje termow (projekt: "aktualizacja"); `nagraj_kronika.py` (repo glowne) bez zmian - juz nagrywa `wersja != pelna`, wiec wiersze smierc.

### Ryzyko / co sprawdzic (kontrola calosci, CLAUDE.md 8.0)
- **Regresje:** VersionOk dla lekka/pelna/obie - ten sam wynik; HasFull + smierc - zmienia tylko okna z wierszami lekka (brak). Akcje z wierszy innych niz smierc - te same
  galezie (FullOnlyWhy/Succeed/CanonAct: nowy warunek tylko dla wersji smierc). Fires: nowe `out stale`, jedno wywolanie (Moves). Zapis Kroniki bez zmian formatu; zapis
  arm_canon_companions + linie `seen` (ok. 20 B na wiersz, 48 wierszy). Linia startu Kroniki: pole po "Player Choices" - regex KW-0 bez zmian; linia startu CanonCompanions:
  segment miedzy "w toku N" a "prolog" - regexy MP-0..MP-3b bez zmian.
- **Zapis Jeffa (103.2):** m17 rusza teraz od Purpurowych God (+10) zamiast od Krwawych - Brienne zostaje dworzanka Starkow po Krwawych Godach do dnia Joffreya +10 (albo dwor
  Lannisterow przez m31/m32). Okna za m17 (047.2, 064.1, 904.1) sa po Godach - zgodne. Wiersze smierc (081+) i nowe #move (037+) daleko przed nim.
- **Kolizje:** m104-e5 - straz niewoli prologu tylko p01/p10 (HoldKey), m35 bez strazy (jak w projekcie); RebuildHeld zobaczy m35 jako "niewola do free m36", ale HeldByScene
  zwraca null (HoldKey) - bez blokady ucieczki i okupu. Harrenhal startuje u clan_vlandia_5 (Clegane, Lannisterowie): Jaime moze byc "jencem wlasnego krolestwa" do m36
  (15-45 dni; gra uwalnia jencow takich stron tylko przy pokoju/zmianie krolestwa, ucieczka 4%/dobe) - nieszkodliwe, m36 "juz wolny" + rod. Wiezienie z druzyna: LeaveParty
  rozwiazuje druzyne Jaimego (jak m01/p10 - ludzie do osady). Galaz w-toku/m107-gospodarka: wspolne pliki Settings.cs/McmSettings.cs/sprawdz_logi.py - po scaleniu gen_mcm
  i sprawdz_narzedzie.
- **Spojnosc:** smierc wchodzi tylko z dziedzicem (succeed: najpierw glowa rodu/wladca, potem smierc); wiersz smierc wygrywa tylko przy prawdziwych warunkach akcji -
  KW-17 (a) = 0 w biegu; mozliwe wyjatki: postac w rodzie/druzynie gracza, weto CanHeroDie (ROT, sceny prologu) - wtedy linia KANON i NIE w KW-17. Promienie `near:`
  (120/150/40/20/30) do kalibracji z KW-17b w dlugim tescie. Smierc Daenerys w lekkiej otwiera 117.1 (Wielka Rada - kod:wielka_rada, lekka = relacje) - zamierzone.
- **Cudzy kod:** PrisonerReleaseCampaignBehavior (dekompilacja TaleWorlds.CampaignSystem: ucieczka 4%/dobe, zwolnienie przy pokoju i zmianie krolestwa); TakePrisonerAction
  i EndCaptivityAction jak w m01/p10/m02. ROT StannisDeathEvent po smierci Stannisa z 081.2 - lista chronionych dopuszcza po done:EastwatchEvent (decyzja K0).
- **Otwarte (decyzje dla glownej sesji, opis w projekcie) - ZAMKNIETE poprawkami A1/A2 (wpis wyzej: 115.2e, 115.3, 115.4; z D3 zostaja Jorah, Edd, Lyanna, Beric,
  Thoros - wcale):** D1 Cersei w pelnym kanonie (Tommen i Myrcella martwi - zaden wiersz jej nie zabija; propozycja
  `succeed:<b>:@dziedzic` + `heir:`), D2 Gora 115.2d (bez dziedzica rodu Clegane - zostaje w tekscie), D3 smierci tylko z V2 (Euron, Qyburn, Jorah, Edd, Lyanna, Beric, Thoros).
- **Lektor:** teksty nagranych okien BEZ zmian (kronika_check: 0 bledow md5). Do dogrania (nowe nagrania, wiersze smierc z lektor=1, dotad pelna - skrypt ich nie nagrywal):
  kr_081_2a, kr_081_2b, kr_094_1a, kr_094_2a, kr_104_2a, kr_115_2b, kr_115_2c, kr_116_2a - razem 3448 znakow (tekst nagrania jak nagraj_kronika.py). Do czasu nagrania okna
  bez glosu (UWAGA walidatora, nie blad). 095.1a gra nagranie 095.1 (wariant bez wlasnego tekstu).
- `arm_canon_moves_test.tsv` trafi do folderu gry skryptem wgrania (petla arm_*.tsv) - czytany tylko z flaga testu Kroniki w trybie autotestu.

### Plan autotestu (glowna sesja; gdy Jeff nie gra; kopia logow Jeffa przed 1. biegiem; DLL z HEAD do test/)
- **AT1** - klasyczny start z flaga `armoury-kronika-test.flag` (k00 + plik testowy #move), ok. 60 dob (okna 000.1-000.9 ida co dobe po 000.6). Oczekiwane linie:
  start `... Canon Deaths TAK (wierszy smierci 13)`; CanonCompanions `test Kroniki ... arm_canon_moves_test.tsv: wierszy #move +2, zlych 0`; `log 000.7 wariant a`,
  `smierc kanonu 000.7a TAK - okno 000.7 wierszem smierci (klucze: Canon Deaths TAK, Full Versions NIE, World Changes TAK)`, `smierc kanonu 000.7b nie: near:lord_7_6:town_K1:5`,
  `akcja 000.7 #1 succeed:lord_7_6:lord_7_5_1 -> OK (... | smierc kanonu)` (gdy Halys nie stoi na ziemiach Polnocy - 000.7a nie: region:... i wiersz glowny; INFO), `000.8 wariant a`,
  `akcja 000.8 #1 die:ROTwanderer35 -> OK|POMINIETA (... | smierc kanonu)`, `000.9 wariant a`, `zmiana strony #t01 - ... akcja lead`, `zmiana strony #t02 POMINIETA ... nie moze zajsc`.
  `python -I tools/sprawdz_logi.py --grupa m105kronika <Armoury-*.log> --crashscribe <session-*.log>` - 0 NIE (KW-0b TAK, KW-17 TAK); `--grupa m101postacie` - MP-0 'plik wczytany'.
- **AT2** - jak AT1 + plik `CrashScribe/armoury-kronika-kanon-nie.flag`: start `Canon Deaths NIE (wierszy smierci 13, flaga testu armoury-kronika-kanon-nie.flag)`, `log 000.7`
  (wiersz glowny), `smierc kanonu 000.7a nie: klucze Chronicle Canon Deaths NIE i Full Versions NIE`, `000.9 wariant b`, #t02 wykonany, #t01 POMINIETA; KW-17 INFO/TAK, 0 NIE.
- **AT3** - regresja: prolog 230 dob bez flagi: linie startu (Kronika z polem Canon Deaths, CanonCompanions `wiersze #move nowe w zapisie w tej sesji 48`), MP/KW/PR jak dotad,
  rozmiar zapisu; po zapisie i wczytaniu `... nowe w zapisie w tej sesji 0`. Pierwsza nowa rzecz osiagalna w grze to 037.1 (dzien opowiesci 800+) - prolog 330 dob niepotrzebny.
  Opcjonalnie: wczytanie zapisu testowego WGRANIA 103 - `nowe w zapisie 11`, bez linii "zmiana strony" w dniu wczytania.

**Status:** DO SPRAWDZENIA (nic nie wgrane).
