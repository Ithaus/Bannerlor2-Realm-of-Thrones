# CHANGELOG galezi w-toku/m107-kronika (Kronika Westeros: smierci z kanonu w wersji lekkiej i zmiany stron Theona, Brienne, Podricka, Jaimego)

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
3. m32: dopisany `|ev:JoffreyEvent+5` (lagodzenie z projektu) - bez niego Brienne-jeniec z m31 przy Godach przed 037.1+45 blokowala m17 (CZEKA 60 dob, potem 064.1, 904.1,
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
- **Otwarte (decyzje dla glownej sesji, opis w projekcie):** D1 Cersei w pelnym kanonie (Tommen i Myrcella martwi - zaden wiersz jej nie zabija; propozycja
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
