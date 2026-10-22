# Kronika Westeros - format danych (arm_kronika_kNN.tsv) - kontrakt dla autorow tresci i silnika K0

Stan: 21.10.2026, galaz `w-toku/kronika-dane` (baza n31-calosc 59a054b7). Warstwa DANYCH, bez C#.
Poprawki po paczkach K1-K10 (21.10, commit "117 wydarzen K1-K10"): 3.3 `kr:` przy wylaczonej Kronice = FALSZ (nigdy); 8 stan par i `remove->hide`
tylko razem z K0; 10/11 jednostki dni, `hide` na ukrytym i koniec ukrycia "nigdy"; 12 uchwyty z opisem dla K0 i kolumna gdzie; 13 `@cel` w oldage
i wizja; 17 nowe reguly walidatora. Dziennik galezi: `docs/noc-1920/kronika-dane-CHANGELOG.md`.
Recenzja danych (21.10, commit "poprawki po recenzji"): 11 przyklad glowy rodu (Kevan juz nie ginie w 095.1) i `unhide` nieukrytego; sciezki
ksiazka/serial Brienne rozdzielone warunkami (047.2 `przed` = `kr:083.1`, 070.2a i 083.1a bez Brienne po drugiej sciezce) - lista w dzienniku.
Zrodla: `docs/PLANY-E5-WOJNY-KRONIKA-2026-10-21.md` (repo glowne) sekcja "## PLAN kronika" (B P1-P10, D, E, F, G)
oraz "## kronika:A" i "## kronika:B"; `docs/KRONIKA-WESTEROS-V2-2026-10-21.md` (117 wydarzen, decyzje Jeffa).
Silnik K0 (Chronicle*.cs) powstanie pozniej i ma czytac DOKLADNIE ten format. Walidator: `tools/kronika/kronika_check.py`.

Spis: 1 pliki | 2 budowa pliku | 3 #okno | 4 #wybor | 5 #tekst | 6 #banda | 7 #zadanie | 8 #para | 9 #usuniete |
10 warunki (termy) | 11 skutki (akcje) | 12 uchwyty kod: | 13 okna powracajace | 14 tematy obrazkow | 15 lista chronionych |
16 zasady tresci | 17 walidator | 18 podzial na paczki K1-K10 | 19 identyfikatory | 20 przyklad

---

## 1. Pliki

| Plik | Co | Kto czyta |
|---|---|---|
| `Armoury/ModuleData/arm_kronika_k00.tsv` | 2 okna testowe silnika (000.1, 000.2) - wczytywany TYLKO przy pliku `CrashScribe/armoury-kronika-test.flag` | K0, autotest K0a/K0b |
| `Armoury/ModuleData/arm_kronika_k01.tsv` ... `k10.tsv` | paczki K1-K10 (rozdz. 18) - jeden plik na paczke | K0 + paczki |
| `Armoury/ModuleData/arm_kronika_tematy.tsv` | tematy obrazkow -> sprite'y ROT i obrazki prologu (rozdz. 14) | Chronicle.Pictures |
| `Armoury/ModuleData/arm_canon_moves.tsv` | zmiany stron postaci kanonu (CanonCompanions) - Kronika dopisuje tu tylko termy `kr:` (rozdz. 8) | CanonCompanions |
| `Armoury/ModuleData/Lektor/EN/arm_lektor.tsv` | nagrania (pozniej): klucz `kr_NNN_w[war]`, kolumna 5 = md5 tekstu okna | Lektor |

- Kodowanie UTF-8 bez BOM, separator TAB, konce linii LF albo CRLF (git z autocrlf i tak zamienia). Bajt 0 w pliku = plik wyzerowany
  (dysk C Jeffa) - silnik wylacza cala paczke z linia `ZLY PLIK`, walidator zglasza BLAD.
- Skrypt wgrania (`wgraj-v5.ps1`) kopiuje `arm_*.tsv` z ModuleData petla - nowe pliki ida same (plan D).
- Silnik czyta pliki raz przy OnSessionLaunched. Zly wiersz = linia `ZLY WIERSZ` z powodem (reszta paczki dziala), zly plik = paczka wylaczona.

## 2. Budowa pliku

- Linia zaczynajaca sie od `# ` (krzyzyk + spacja) albo sam `#` = komentarz. Pusta linia = nic.
- Linia, ktorej PIERWSZA komorka zaczyna sie od `#` bez spacji, to naglowek sekcji: `#okno`, `#wybor`, `#tekst`, `#banda`, `#zadanie`,
  `#para`, `#usuniete`. Po nazwie sekcji (TAB) ida nazwy kolumn - DOKLADNIE jak ponizej (walidator porownuje). Kopiuj naglowki stad:

```
#okno	id	war	od	do	po	przed	warunki	gracz	forma	inni	wersja	temat	lektor	tytul	gora	dol	zmienne	wybor	skutki	powtorz
#wybor	okno	opcja	etykieta	podpowiedz	dostepna	skutki	bierna
#tekst	id	tekst
#banda	id	klan	osada	min	max	herszt	zachowanie	dni	list	po_rozbiciu
#zadanie	id	rodzaj	cel	co	ilosc	dni	nagroda	tytul	opis
#para	okno	wiersz	kierunek
#usuniete	id	powod
```

- Sekcja moze wystapic kilka razy; kolejnosc sekcji dowolna. Puste koncowe komorki wiersza mozna pominac; wiecej komorek niz kolumn = BLAD.
- Spacje na brzegach komorek sa obcinane. Nie ma cudzyslowow CSV - TAB nie moze wystapic w tekscie.

## 3. Sekcja #okno

Jeden wiersz = jedno okno albo jeden wariant okna. Okno ma **wiersz glowny** (kolumna `war` pusta, pierwszy wiersz danego `id` w pliku)
i zero lub wiecej **wariantow** (`war` = jedna mala litera a-z) zaraz pod nim (moga byc dalej w pliku, ale zawsze po glownym).

### 3.1 Kolumny

| Kolumna | Wiersz glowny | Wariant | Opis | Przyklad |
|---|---|---|---|---|
| `id` | wymagane | to samo id | `NNN.w`: NNN = numer wydarzenia 001-117 (sceny "juz w grze" 901-906, test 000), w = numer okna 1-9. **Trwale na zawsze** od wgrania paczki (stan w zapisie); usuniete okno = nagrobek w `#usuniete`. Id z `kronika_v2.json`: plik `kronika_008a.png` -> `008.1`, `b` -> `.2`, `c` -> `.3`, bez litery -> `.1`. | `008.1` |
| `war` | puste | `a`-`z` | litera wariantu; unikalna w oknie | `b` |
| `od` | wymagane | puste | najwczesniejszy dzien opowiesci (skala dokumentu Kroniki - dni od egzekucji Neda, rok 364 dni; silnik przelicza przez `k = FabulaTimeScale/4.33`). `0` = bez dolnej granicy (okno zalezne tylko od warunkow) | `35` |
| `do` | opcjonalne | puste | ostatni dzien planu. Okno spoznione o wiecej niz 30 dni po `do` (MaxLate) przepada albo idzie jako wiesc (gdy jest tekst `_W`). Puste = bez konca | `65` |
| `po` | opcjonalne | puste | warunek "po czym" (skladnia warunkow, zwykle jeden term z `+N`): okno nalezy sie dopiero, gdy jest prawdziwy | `kr:002.1+10`, `ev:RenlyDeathEvent+5`, `mv:m19` |
| `przed` | opcjonalne | puste | kotwica "przed czym": JEDEN term `ev:X`, `done:X` (X = wydarzenie ROT) albo `kr:NNN.w`. Gdy kotwica juz zaszla, okno zamienia sie w wiesc (jest `_W`) albo przepada z powodem `X juz ruszylo` (P2) | `ev:RiverlandsDeclareWarEvent` |
| `warunki` | opcjonalne | wymagane (albo `gracz`) | glowny: warunki CALEGO okna (dla kazdego wariantu); wariant: warunki DODATKOWE tego wariantu (razem z glownymi) | `alive:lord_5_1 & war:battania:vlandia` |
| `gracz` | opcjonalne | opcjonalne | warunki "gracz widzi pelna forme". Gracz spelnia -> `forma`, nie spelnia -> `inni`. Puste = kazdy gracz dostaje `forma` | `p:vassal:battania` |
| `forma` | wymagane | wymagane | `okno` (okno ROT z obrazkiem) \| `wiesc` (linia wiesci, tekst `_W`) \| `log` (tylko linia logu, bez niczego dla gracza) | `okno` |
| `inni` | wymagane | wymagane | forma dla gracza spoza `gracz`: `okno` \| `wiesc` \| `nic` | `wiesc` |
| `wersja` | wymagane | wymagane | `lekka` \| `pelna` \| `obie`. Klucz ChronicleFullVersions=NIE (domyslnie): biora udzial `lekka` i `obie`; TAK: `pelna` i `obie`, a `lekka` tylko gdy okno nie ma zadnego wiersza `pelna`. Pelna wersja = wariant z `wersja=pelna` | `lekka` |
| `temat` | wymagane gdy sie pokazuje | puste = dziedziczy | temat obrazka z `arm_kronika_tematy.tsv` (rozdz. 14) | `dorzecze` |
| `lektor` | `0`/`1` | `0`/`1` | `1` = okno ma byc nagrane (pozniej). Tylko dla okien bez `{ZMIENNYCH}`, ktore sie pokazuja (`forma` albo `inni` = okno). Klucz nagrania `kr_NNN_w[war]` | `1` |
| `tytul` | wymagane gdy sie pokazuje | opcjonalne | `{=ARM_KR_NNN_w[war]_T}Tekst` - najwyzej 45 znakow tekstu | `{=ARM_KR_008_1_T}The Wagon North` |
| `gora` | j.w. | j.w. | `{=ARM_KR_NNN_w[war]_U}Tekst` - najwyzej **4 linie** okna (miara linie.py, ok. 330-380 znakow) | |
| `dol` | j.w. | j.w. | `{=ARM_KR_NNN_w[war]_L}Tekst` - najwyzej **3 linie** okna (ok. 250-280 znakow) | |
| `zmienne` | opcjonalne | opcjonalne | `NAZWA=@token[:arg]` rozdzielone ` ; ` - podstawiane w `{NAZWA}` w tytule, gorze, dole i pytaniu (rozdz. 3.5) | `RULER=@wladca:battania` |
| `wybor` | opcjonalne | opcjonalne | pytanie listy po oknie: `{=ARM_KR_NNN_w[war]_Q}Tekst` (do 200 zn.). Puste = bez wyboru. Opcje w `#wybor` | `{=ARM_KR_002_1_Q}Will you kneel?` |
| `skutki` | opcjonalne | opcjonalne | akcje rozdzielone ` ; ` (rozdz. 11) - wykonywane raz, gdy okno sie rozstrzygnie jako okno/wiesc/log | `flag:k008_arya ; hide:lord_5_1_1` |
| `powtorz` | opcjonalne | puste | wyzwalacz okna powracajacego (rozdz. 13); puste = okno jednorazowe | `siegehunger:3 ; prolog` |

Wariant bez wlasnego tekstu (puste `tytul`, `gora`, `dol`) uzywa tekstu wiersza glownego. Jesli wariant ma tekst - to wszystkie trzy
pola z wlasnymi id (`..._1b_T`, `_1b_U`, `_1b_L`).

### 3.2 Kiedy okno sie nalezy, pokazuje i przepada (silnik K0)

1. Okno jest **nalezne**, gdy: dzien opowiesci >= `od`, `po` prawdziwe (z `+N`), `warunki` wiersza glownego prawdziwe, okno nierozstrzygniete.
   Okna osi nie ida w prologu (Prologue.BeforeStory) - wyjatek: okno powracajace z opcja `prolog`.
2. **Wybor wariantu** przy pokazaniu: warianty w kolejnosci pliku, pierwszy, ktorego `warunki` sa prawdziwe (i ktory przechodzi filtr
   `wersja`), wygrywa; zaden - wiersz glowny (zapas). Dlatego wiersz glowny = wersja ogolna (zwykle kanon), warianty = swiaty inne niz w ksiazce.
3. **Forma**: gracz spelnia `gracz` (albo `gracz` puste) -> `forma`; nie -> `inni`. `nic` = nic dla gracza, ale skutki i tak sie wykonuja.
4. **Przepada** (linia `POMINIETE ... z powodem`): term, ktory juz nigdy nie zajdzie (np. `alive:` martwego, `kr:` okna pominietego),
   kotwica `przed` juz zaszla (gdy jest `_W` - zamiast tego wiesc), spoznienie > 30 dni po `do` (tez wiesc, gdy jest `_W`),
   zapis sprzed paczki (paczka wgrana po swoich dniach - bez lawiny).
5. Tempo: najwyzej 2 okna Kroniki na dobe, 6 h odstepu od poprzedniego okna Kroniki albo okna ROT, 2 s spokoju na mapie (P2/E).
6. Kolejnosc kilku naleznych: najnizsze `od`, potem id.

### 3.3 Skutki i klucze MCM

- Skutki okna wykonuja sie RAZ przy rozstrzygnieciu (okno/wiesc/log), przed lista wyboru. Skutki opcji - po wyborze.
- Klasa akcji: T (tekst), R (relacje/slawa), S (swiat). Klucz **ChronicleWorldChanges=NIE** pomija klase S (z linia logu).
- Klucz **ChroniclePlayerChoices=NIE** (albo gracz poza `gracz`, albo 0 dostepnych opcji) = wykonuje sie opcja **bierna** bez pytania.
- Klucz **ChronicleOfWesteros=NIE** = brak okien; termy `kr:` (w oknach i w arm_canon_moves) sa wtedy **FALSZYWE na zawsze** ("nigdy", jak dzis
  nieznany term w `CanonCompanions.BaseTrue`) - wiersz `#move` z wyzwalaczem `stary|kr:NNN.w` rusza wtedy od starego wyzwalacza, bo `|` = lub.
  (Poprawka 21.10: pierwsza wersja mowila "prawdziwe" - przy alternatywie `|` m02, m04, m18 i m22 ruszylyby w dniu startu kampanii;
  zglosili to wszyscy autorzy K1-K10.) Wiersz z samymi termami `kr:` przy wylaczonej Kronice nie rusza wcale (walidator: UWAGA).
- Skutki opcji, ktore musza znac wybor gracza (np. uchwyt `kod:wielka_rada`), ida w skutkach KAZDEJ opcji - skutki okna sa wykonane przed lista.

### 3.4 Teksty i id TextObject

- Kazda komorka tekstu zaczyna sie od `{=ID}`, potem tekst angielski. ID = `ARM_KR_` + klucz z kropka zamieniona na `_` + `_` + sufiks:

| Gdzie | Sufiks | Przyklad |
|---|---|---|
| tytul / gora / dol okna | `T` / `U` / `L` | `{=ARM_KR_008_1_T}`, wariant `b`: `{=ARM_KR_008_1b_T}` |
| pytanie listy (`wybor`) | `Q` | `{=ARM_KR_002_1_Q}` |
| etykieta / podpowiedz opcji x | `Cx` / `Hx` | `{=ARM_KR_002_1_Ca}`, `{=ARM_KR_002_1_Ha}` (dla `#wybor` z oknem `002.1b`: `{=ARM_KR_002_1b_Ca}`) |
| tekst z `#tekst` id `NNN.w[war]_X` | `X` | `005.1_W` -> `{=ARM_KR_005_1_W}` |
| herszt bandy `bNNN` | `H` | `{=ARM_KR_b008_H}` |
| tytul / opis zadania `tNNN` | `T` / `D` | `{=ARM_KR_t014_T}`, `{=ARM_KR_t014_D}` |

- Pojemnosc (walidator mierzy font Galahad jak okno ROT, szerokosc 1004 px, `tools/kronika/linie.py`): tytul <= 45 znakow, gora <= 4 linie,
  dol <= 3 linie, etykieta <= 60 znakow, podpowiedz <= 200, pytanie <= 200, tekst wiesci/plotki <= 250. `{ZMIENNA}` liczona jako 28 znakow.
- Tylko znaki z fontu Galahad; bez polskich liter; **bez cudzyslowow** (`"` i typograficzne) - zadnych cytatow (rozdz. 16).

### 3.5 Zmienne

`NAZWA` = wielkie litery/cyfry/_ ; w tekscie `{NAZWA}`. Tokeny wartosci (nazwa obiektu w grze):
`@gracz` (imie gracza), `@wladca:<krolestwo>` (obecny wladca), `@glowa:<rod>` (obecna glowa rodu), `@pan:<osada>` (glowa rodu-wlasciciela osady),
`@imie:<bohater>`, `@nazwa:<osada|rod|krolestwo>`, a w oknach powracajacych tez `@cel`, `@kto`, `@rodkto`, `@region`, `@liczba`, `@pora` (rozdz. 13).
Wiersz z `{ZMIENNYMI}` nie moze miec `lektor=1` (nagranie byloby inne niz okno). Zmienne nie wchodza do etykiet i podpowiedzi opcji.

## 4. Sekcja #wybor (lista po oknie)

Okno ROT ma przyciski Done/Cancel, a Esc zawsze wola Cancel (sprawdzone w dekompilacji) - **wybor nigdy nie lezy na przyciskach okna**.
Po zamknieciu okna, nad mapa, silnik pokazuje liste MultiSelection (radio, bez wyjscia Esc, opis z id okna).

| Kolumna | Opis | Przyklad |
|---|---|---|
| `okno` | `NNN.w` (opcje dla kazdego wiersza tego okna z wypelnionym `wybor`) albo `NNN.w<war>` (opcje tylko tego wariantu; ma pierwszenstwo) | `002.1` |
| `opcja` | `a`, `b`, `c`, `d` - kolejno, **2-4 opcje** | `a` |
| `etykieta` | `{=ARM_KR_<okno>_C<x>}` tekst na liscie, <= 60 znakow | `{=ARM_KR_002_1_Ca}Kneel before the King in the North` |
| `podpowiedz` | `{=ARM_KR_<okno>_H<x>}` skutki slowami gracza; przy opcji wyszarzonej - tez dlaczego. Wymagana | `{=ARM_KR_002_1_Ha}Robb Stark thinks better of you.` |
| `dostepna` | warunki (skladnia warunkow); niespelnione = opcja wyszarzona z podpowiedzia. Puste = zawsze | `p:rel:lord_5_1:10` |
| `skutki` | akcje opcji (rozdz. 11) | `relp:lord_5_1:5` |
| `bierna` | `1` TYLKO na ostatniej opcji, inaczej puste | `1` |

Zasady: **ostatnia opcja jest bierna** = wersja z ksiazki (bez dzialania gracza), zawsze dostepna (pusta `dostepna`). Autotest wybiera
ostatnia czynna pozycje, wiec bieg testowy idzie wersja kanoniczna. Wybor zapisuje sie jako `kr:NNN.w=x` (do warunkow innych okien).
Akcje tylko-pelne (rozdz. 11) w opcjach - tylko gdy opcje naleza wylacznie do wierszy `wersja=pelna` (`okno` = `NNN.w<war>` wariantu pelnego).

## 5. Sekcja #tekst (wiesci i plotki)

| Kolumna | Opis | Przyklad |
|---|---|---|
| `id` | `NNN.w[war]_X`, X = wielkie litery/cyfry; X nie moze byc `T`, `U`, `L`, `Q`, `Ca`-`Hd`. Okno musi istniec | `005.1_W`, `008.2_P1` |
| `tekst` | `{=ARM_KR_NNN_w[war]_X}` tekst EN <= 250 znakow | `{=ARM_KR_005_1_W}Merchants from Pentos tell of a khal's pyre.` |

- `NNN.w[war]_W` = tekst formy `wiesc` (gdy `forma` albo `inni` = wiesc - wymagany) i zapasowa wiesc przy spoznieniu/kotwicy.
  Wariant bez wlasnego `_W` bierze `NNN.w_W`.
- Inne teksty uzywa akcja `wiesc:<id>` i `plotka:<id>:<osada|krolestwo>:<dni>` (plotka trafia do ksiegi wiesci Rumours, bez lektora na zywo).
- Konwencja: `_W` wiesc formy, `_P`/`_P1` plotki, `_N1` dodatkowe wiesci.

## 6. Sekcja #banda (od K1)

| Kolumna | Opis | Przyklad |
|---|---|---|
| `id` | `bNNN[litera]` - NNN = numer wydarzenia (paczka = paczka wydarzenia) | `b008` |
| `klan` | rod bandy: bandycki (`looters` = Broken Men, `sea_raiders` ...) albo mniejszy ROT (`brotherhood`, `brave_companions`, `sons_of_the_harpy`, `stone_crows`, `faith_militant`). Inny = UWAGA (sprawdzic CreateLooterParty) | `looters` |
| `osada` | osada, przy ktorej banda powstaje (region = sufit band) | `town_V2` |
| `min`, `max` | ludzi (1-150), z puli wyrzutkow regionu (OutlawLaw.RaidDraw) - nie z niczego | `20`, `40` |
| `herszt` | `{=ARM_KR_bNNN_H}` nazwa partii na mapie (<= 40 zn.) | `{=ARM_KR_b008_H}Lorch's Raiders` |
| `zachowanie` | `patrol` \| `rabunek` \| `oboz` | `rabunek` |
| `dni` | zycie bandy 1-365 | `60` |
| `list` | `1` = list gonczy (NoticeBoard obejmuje sam), `0` = bez | `1` |
| `po_rozbiciu` | akcje, gdy banda rozbije GRACZ (skladnia skutkow) | `relk:riverlands:3 ; renown:20` |

Wywolanie: akcja `band:b008` w skutkach okna albo opcji. Sufit: 12 band Kroniki naraz + wspolny sufit band fabularnych regionu (P6).
Wydarzenie 17 bierze bande Berica z E5, gdy zyje (P6) - wtedy nie definiowac nowej.

**Stan po K0 krok B2 (Chronicle.Bands.cs, StoryActions.cs):** banda powstaje przez `StoryActions.SpawnBand` (przeniesione 1:1 z m104-e5 Prologue.World.Clans:
OutlawLaw.RaidDraw z okregu osady - najpierw pula wyrzutkow, potem domy wsi okregu; za malo ludzi = banda nie powstaje, ludzie zostaja w puli; kiesa 0,
pochodzenie i ksiega ludzi). Klan spoza `Clan.BanditFactions` (w ROT `brave_companions`, `faith_militant`, `sons_of_the_harpy` to pomniejsze frakcje, nie bandy) =
banda pod klanem `looters` z nazwa z kolumny `herszt` (linia mowi o zamianie). Sufity: 12 band Kroniki naraz i 4 bandy fabularne w okregu OutlawLaw
(`StoryActions.RegionCap`: najazdy Wolnych Ludzi, sceny E5 prologu i Kronika; sufit sprawdza tylko Kronika). Ta sama `bNNN` dwa razy naraz - nie (OK "juz na mapie").
`zachowanie`: rabunek = AI band gry wokol osady (napada podroznych), patrol = spokojniejsza (Aggressiveness 0.5) z patrolem wokol osady, oboz = prawie bez zaczepek
(0.1). Po `dni` dniach banda wraca do domow jak najazd Wolnych Ludzi (FreeFolkRaids.Home: ludzie do puli okregu, zloto i towar do wsi). `po_rozbiciu` = raz, gdy
banda przegra bitwe, w ktorej druzyna gracza jest po stronie zwyciezcy (kontekst akcji `bNNN`). `list = 0` - NoticeBoard (listy goncze) pomija bande
(`Chronicle.NoBounty`). Zapis "kb" w arm_kronika. b017 w K2 to tabor Lannisterow (nie Beric) - bez osobnej reguly dla Berica.

## 7. Sekcja #zadanie (od K2, silnik Chronicle.Tasks.cs)

| Kolumna | Opis | Przyklad |
|---|---|---|
| `id` | `tNNN[litera]` | `t014` |
| `rodzaj` | `deliver` (dowiez towar do osady) \| `defeat` (rozbij bande z `#banda`) \| `prisoners` (dostarcz jencow) | `deliver` |
| `cel` | osada docelowa | `town_B5` |
| `co` | deliver: id towaru; defeat: id bandy `bNNN`; prisoners: `-` | `grain` |
| `ilosc` | sztuk / jencow (defeat: 1) | `200` |
| `dni` | termin 1-365 | `60` |
| `nagroda` | akcje przy wykonaniu (zloto zawsze z platnikiem) | `gold:clan_battania_2:@gracz:1500 ; relp:lord_5_3:10` |
| `tytul`, `opis` | `{=ARM_KR_tNNN_T}` (<= 45 zn.), `{=ARM_KR_tNNN_D}` (<= 250 zn.) | |

Wywolanie: `task:t014`. Wlasny rejestr bez QuestBase, wpis na NoticeBoard (Kind.Task). Format moze dostac kolumne w K2 - wtedy zmiana tu i w walidatorze.

**Stan po K0 krok B2 (Chronicle.Tasks.cs):** wlasny rejestr (zapis "kz"), najwyzej 8 zadan naraz, to samo zadanie w trakcie = OK "juz trwa". **Bez wpisu na tablicy
ogloszen**: wpisy NoticeBoard to dzis zadania gry (QuestBase, Kind.Task) i listy goncze - nowy rodzaj wpisu wymagalby zmian karty, warunkow i przyciskow tablicy;
zadanie Kroniki idzie wiadomoscia dla gracza (EN: tytul, opis, osada, termin) i linia logu "zadanie tNNN 'tytul' przyjete". `deliver` - przy wejsciu druzyny gracza do
osady `cel` (albo w dobie, gdy w niej stoi) z co najmniej `ilosc` sztuk - towar schodzi z ekwipunku druzyny na rynek osady; `prisoners` - `ilosc` jencow (bez
bohaterow i umarlych) z lochu druzyny do lochu osady (PeopleBook.Transfer); `defeat` - banda `co` musi byc na mapie w chwili `task:` (w danych `band:` stoi
wczesniej), wykonanie = rozbicie przez gracza; zniknela bez gracza = NIEUDANE. Po terminie `dni` - NIEUDANE (linia i wiadomosc). Nagroda zwykla droga akcji
(kontekst `tNNN`). Brak przypomnienia o brakach poza jedna wiadomoscia przy wejsciu do osady (raz na sesje).

## 8. Sekcja #para (zmiany stron postaci kanonu - P1)

Zmiany stron postaci kanonu wykonuje TYLKO CanonCompanions (wiersze `#move` w `arm_canon_moves.tsv`). Kronika ich nie wykonuje
(akcja `move:` odpadla z planu - walidator ja odrzuca). Sa dwa sposoby powiazania okna z wierszem `mNN`:

| `kierunek` | Znaczenie | Co wpisac |
|---|---|---|
| `po` (domyslny) | okno IDZIE ZA ruchem | w oknie `po` (albo `warunki`) = `mv:mNN`; wariant `mv:mNN=skip` = wersja "ruch pominiety" (bez tej postaci) |
| `kr` | okno POPRZEDZA ruch albo wybor gracza go zmienia | w `arm_canon_moves.tsv` do wyzwalacza wiersza dopisac `\|kr:NNN.w[=opcja][+N]` - stary wyzwalacz ZOSTAJE (zapas przy wylaczonej Kronice) |

Kolumny: `okno` (`NNN.w`), `wiersz` (`mNN`/`pNN`), `kierunek` (`po`/`kr`). Przyklad (plan): `008.1	m22	kr` + w arm_canon_moves m22:
`ev:HarrenhalSiegeNotificationEvent|rotday:120|kr:008.1+1`.

Reguly (walidator): kazde `mv:` w oknie ma pare `po`; kazdy term `kr:` w arm_canon_moves ma pare `kr` (dla napisanych paczek);
para `po` + `kr:` tego okna w wierszu = petla (BLAD). Postac z wierszami `#move` (lista: kolumna `who` w arm_canon_moves - m.in. Jaime
ROTwanderer1, Tyrion ROTwanderer8, Bronn 4, Podrick 6, Shagga 19, Qyburn 12, Sandor 3, Varys 2, Petyr 27, Jacelyn 25, Brienne 9, Theon 20,
Osha 18, Benjen 38, Sam 5, Yoren 28, Quentyn 26, Hizdahr 22, Wun Wun 33, Jeyne ROTlord_177) **nie dostaje z Kroniki** akcji
`clan/court/lead/prisoner/free/wander/die/marry/wed/succeed` - tylko przez pare. `hide/unhide/rel/item/skill/wound` - wolno.
Wiersz `p10` (m104-e5) jest znany walidatorowi, choc go tu jeszcze nie ma.
**Uwaga na wyzwalacze `remove`:** m20 Osha (`done:BlackwaterEvent+10`, ok. dzien 780), m11 Sandor (`done:RedWeddingEvent+20`, ok. 990),
m21 Sam (`done:WallEvent+20`, ok. 1200) zabijaja postac (`remove` = KillCharacterAction.ApplyByRemove). Plan zmienia je na `hide` (P3), a powroty
sa w nr 82 (Sam), 89 (Sandor) - zmiane `remove -> hide` wpisac w PIERWSZEJ paczce wgranej po K0 (K1), inaczej kampania minie te dni wczesniej.
**Stan 21.10 (po K1-K10): m11/m20/m21 dalej maja `remove` - CELOWO.** Dzisiejszy CanonCompanions nie zna akcji `hide` (`KnownActions`: clan, court,
lead, prisoner, free, wander, remove, die, marry, wed) - wiersz z `hide` bylby odrzucony jako zly (`zlych wierszy` w linii startu) i nie ruszylby
nigdy, czyli zmiana teraz zmienilaby gre Jeffa bez silnika. Kolejnosc dla K0: (1) `hide`/`unhide` w CanonCompanions.ApplyAction i KnownActions,
(2) w TYM SAMYM commicie m11/m20/m21 `remove -> hide`. Do tego czasu okna zalezne od zywych Sama/Sandora po dniach `remove` (064.2a, 082.2,
082.3, 089.1, 090.1, 101.1, 111.1a, 115.2d, 905.1) przepadaja na warunkach `alive:` z powodem - bez bledu.
**Stan po K0 krok A4 (galaz w-toku/kronika-00-silnik, 21/22.10): zrobione.** CanonCompanions zna `hide`/`unhide` (ApplyAction, rejestr ukrytych `hid`
w zapisie arm_canon_companions) i w tym samym commicie m11/m20/m21 maja `hide` (p09 Benjen - prolog - dalej `remove`). Term `kr:` w wyzwalaczach
#move liczy stan okien Kroniki (Chronicle.Resolved; klucz ChronicleOfWesteros NIE = falsz "nigdy", takze z pamiecia +N; +N od dnia rozstrzygniecia
okna); linia zmiany strony z alternatywa w wyzwalaczu ma dopisek `(zaszlo <term>)`. `unhide` dziala na kazdego z rejestru (ukrytego akcja Kroniki
albo wierszem #move - np. 082 Sam po m21, 089 Sandor po m11); postac spoza rejestru = POMINIETA `nie ukryty`. Postac ukryta: kolejne wiersze #move
tej postaci CZEKAJA na powrot (do 60 dob, potem pominiete). Jeniec, gubernator i postac w bitwie przy `hide` = CZEKA (nie ukrywamy jenca).
Pary `kr` w arm_canon_moves (stan 21.10): m02 Jaime `done:BlackwaterEvent|kr:032.1=b|kr:032.1=c`, m04 Tyrion `ev:DanyInvasionEvent|kr:099.2`,
m18 Theon `war:sturgia:battania|kr:902.1+1`, m22 Yoren `ev:HarrenhalSiegeNotificationEvent|rotday:120|kr:008.1+1` - wszystkie ze starym wyzwalaczem.
Zmiana strony postaci z `#move`, ktorej dzis nie ma w tabeli (Theon 072.3/087/108, Brienne i Podrick 083, Jaime 107), = NOWY wiersz `#move`
(najlepiej `kr:` + zapasowy stary wyzwalacz) w paczce silnika - Kronika sama tego nie robi; dzis te okna maja to tylko w tekscie i flagach.

## 9. Sekcja #usuniete (nagrobki)

`#usuniete	id	powod` - id okna wycofanego po wgraniu (np. `031.2	Hoster Tully nie istnieje w ROT`). Id nagrobka nie moze wrocic jako okno.
Przed pierwszym wgraniem paczki id mozna jeszcze zmieniac bez nagrobka.

## 10. Warunki (kolumny `po`, `warunki`, `gracz`, `dostepna`; koniec ukrycia w `hide`)

- Skladnia: termy laczone ` & ` (i), w obrebie skladnika alternatywa ` | ` (lub) - `|` wiaze mocniej: `a | b & c` = (a lub b) i c.
- `!` przed termem = negacja (`!alive:lord_5_1`). `+N` na koncu termu = N dni od PIERWSZEGO dnia, w ktorym term byl prawdziwy (pamiec w zapisie,
  jak CanonCompanions); nie z `!` i nie przy `p:`.
- Id w termach: bohater (ROT_heroes.xml albo ROTwanderer<N> = postac z szablonu), rod (spclans.xml), krolestwo, osada (ROT-Map settlements.xml).
  Tokeny `@...` w termach tylko w oknach powracajacych (`@cel`, `@kto`, `@rodkto`, `@region`).
- **Jednostki dni:** `od`/`do` = dni opowiesci w skali dokumentu (przeliczane przez `k`); wszystko inne = dni KAMPANII bez przeliczenia, jak
  w CanonCompanions: `+N` w termach, `rotday:N` (StoryDay = dni kampanii od konca prologu), `prolday:N`, oraz dlugosci w akcjach i sekcjach
  (`wound`, `plotka`, `muster`, `#banda dni`, `#zadanie dni`).

| Term | Argumenty | Prawdziwy, gdy | Zrodlo |
|---|---|---|---|
| `ev:<X>` | wydarzenie ROT | X wystartowalo (aktywne albo skonczone) | CanonCompanions |
| `done:<X>` | wydarzenie ROT | X skonczone | CanonCompanions |
| `dead:<bohater>` | | bohater nie zyje | CanonCompanions |
| `alive:<bohater>` | | bohater zyje (takze ukryty przez Kronike) | nowy |
| `clanof:<bohater>:<rod>` | | bohater jest w rodzie | nowy |
| `head:<bohater>:<rod>` | | bohater jest glowa rodu | nowy |
| `leads:<bohater>:<krolestwo>` | | bohater jest wladca krolestwa | nowy |
| `prisoner:<bohater>[:<rod\|krolestwo\|osada>]` | | bohater jest jencem (opcjonalnie: tego rodu/krolestwa albo w tej osadzie) | nowy |
| `war:<k1>:<k2>` | krolestwa | wojna (oba istnieja) | CanonCompanions |
| `peace:<k1>:<k2>` | krolestwa | oba istnieja i nie ma wojny | nowy |
| `kexists:<k>` | krolestwo | krolestwo istnieje (nie wyeliminowane) | nowy |
| `own:<osada>:<rod\|krolestwo>` | | osada nalezy do rodu / krolestwa | CanonCompanions (+ rod nowy) |
| `rotday:N` | dzien | dzien opowiesci >= N (StoryDay, ujemny w prologu) | CanonCompanions |
| `prolday:N` | dzien | dzien kampanii >= N, tylko kampania z prologiem (inaczej nigdy) | CanonCompanions |
| `kr:NNN.w` | okno | okno POKAZANE (okno/wiesc/log); okno pominiete = term nigdy | nowy |
| `kr:NNN.w=x` | okno, opcja a-d | w oknie wybrano opcje x | nowy |
| `flag:<nazwa>` | male litery/cyfry/_ | flaga ustawiona akcja `flag:` (zalecana nazwa `kNNN_slowo`) | nowy |
| `mv:<mNN>` | wiersz #move | wiersz rozstrzygniety (wykonany albo pominiety) | CanonCompanions.MoveState |
| `mv:<mNN>=done` / `=skip` | | wiersz wykonany / pominiety | j.w. |
| `cw:reach` / `cw:dorne` | | straznik Prologue.Crown trzyma sojusz korona-Reach / pakty Dorne | m104-wojny (odczyt) |
| `season:<pora>` | winter\|spring\|summer\|autumn | pora roku WesterosClimate | nowy |
| `plan:<X>-N` | wydarzenie ROT, N dni | dzien >= planowany dzien X z osi Fabuli minus N (P2). Brak planu (CrashScribe bez osi) = prawdziwy, decyduje `od` | nowy |
| `p:region:<k>` | krolestwo | gracz jest na ziemiach krolestwa (najblizsza osada nalezy do k) | nowy |
| `p:in:<osada>` | | gracz jest w osadzie | nowy |
| `p:near:<osada>:<odl>` | odleglosc 1-500 (jednostki mapy) | gracz blisko osady | nowy |
| `p:vassal:<k>` | | rod gracza jest w krolestwie (wasal albo wladca) | nowy |
| `p:army` | | gracz prowadzi armie albo jest w armii | nowy |
| `p:rel:<bohater>:<min>` | min -100..100 | relacja gracza z bohaterem >= min | nowy |

Wydarzenia ROT (29): AegonInvasionEvent, BastardsEvent, BastardsSiegeEvent, BastardsSiegeNotificationEvent, BlackwaterEvent,
BlackwaterSiegeEvent, BlackwaterSiegeNotificationEvent, DanyInvasionEvent, EastwatchEvent, FistEvent, HarrenhalEvent, HarrenhalSiegeEvent,
HarrenhalSiegeNotificationEvent, JeorMutinyEvent, JoffreyEvent (Purpurowe Gody), JonEvent, KingsLandingEvent, NedEvent, NightKingDeathEvent,
RamsayEvent, RedWeddingEvent, RenlyDeathEvent, RickardKarstarkExecutionEvent, RiverlandsDeclareWarEvent, RooseDeathEvent, StannisDeathEvent,
WallEvent, WallSiegeEvent, WallSiegeNotificationEvent. (W dokumencie v2 "RedWeddingQuest" = `RedWeddingEvent`.)

Wzorce: "N dni przed wydarzeniem ROT X" = `warunki: plan:X-N` + `przed: ev:X` + `od/do` z dokumentu jako zapas.
"Po wydarzeniu" = `po: done:X+N`. "Gdy rzadzi ktos inny" = wariant z `!leads:lord_5_1:battania` i `{RULER}=@wladca:battania`.

## 11. Skutki (kolumny `skutki`, `skutki` opcji, `po_rozbiciu`, `nagroda`)

- Akcje rozdzielone ` ; `, argumenty `:`. Token z argumentem (`@wladca:battania`) liczy sie jako JEDEN argument.
- Tokeny celu: bohater: id, `@gracz`, `@wladca:<k>`, `@glowa:<rod>`, `@pan:<osada>`, (powracajace) `@kto`; osada: id, (powracajace) `@cel`;
  rod: id, (powracajace) `@rodkto`; krolestwo: id, (powracajace) `@region`; platnik/odbiorca zlota: bohater/token bohatera, rod (glowa rodu),
  osada (kasa osady), `@skarbiec:<krolestwo>` (skarbiec korony), `@kasa:<osada>`.
- Wynik akcji w silniku: OK / POMINIETA (powod) / CZEKA (do 30 dni, np. bohater w bitwie), kazda z linia logu.

| Akcja | Argumenty | Klasa | Od | Opis / zasady |
|---|---|---|---|---|
| `wiesc:<id>` | tekst z #tekst | T | K0 | linia wiesci dla gracza |
| `plotka:<id>:<osada\|krolestwo>:<dni>` | dni 1-120 | T | K0 | wpis w ksiedze wiesci Rumours (karczmy), bez lektora na zywo |
| `flag:<nazwa>` | | T | K0 | ustawia flage (do `flag:` w warunkach) |
| `kod:<nazwa>` | z rejestru | T | wg rejestru | nazwany uchwyt C# (rozdz. 12) |
| `rel:<b1>:<b2>:<d>` | d -100..100 | R | K0 | relacja dwoch postaci, bez powiadomienia |
| `relp:<bohater>:<d>` | | R | K0 | relacja gracza z postacia |
| `relk:<grupa>:<d>` / `relk:<g1>:<g2>:<d>` | grupa = rod albo krolestwo | R | K2 | gracz <-> glowy rodow grupy / glowy rodow grupy 1 <-> grupy 2 |
| `renown:<d>` / `renown:<rod>:<d>` | -1000..1000 | R | K0 | slawa rodu gracza / rodu |
| `influence:<d>` / `influence:<rod>:<d>` | | R | K0 | wplywy |
| `gold:<platnik>:<odbiorca>:<kwota\|N%>` | 1-1000000 albo 1-100% zlota platnika | S | K1 | **zawsze z platnikiem** (zamknieta ekonomia); brak srodkow = niedoplata z linia |
| `security:<osada>:<d>` / `loyalty:<osada>:<d>` | miasto/zamek, -100..100 | S | K1 | bezpieczenstwo / lojalnosc |
| `pop:<osada>:<d>` | | S | K2 | ludzie przez ksiege ludzi (PeopleBook, przyczyna "kronika"): minus = do puli regionu, plus = z puli - nigdy z niczego |
| `ware:<z>:<do>:<towar>:<ilosc>` | osady | S | K6 | towar z rynku do rynku |
| `garrison:<osada>:<ludzi>:<skad>` | skad = osada albo `pula` | S | K3+ | wojsko do zalogi z innej zalogi albo z puli ochotnikow regionu |
| `band:<bNNN>` | z #banda | S | K1 | banda z puli wyrzutkow |
| `task:<tNNN>` | z #zadanie | S | K2 | zadanie |
| `tourney:<miasto>` | tylko miasto | S | K1 | turniej (jak GrandTourney, bez oplat gracza), gdy nie ma innego |
| `muster:<krolestwo>:<osada>:<dni>` | dni 1-60 | S | K2 | miekki cel zbiorki lordow (jednorazowo, AI moze zignorowac) |
| `hide:<bohater>[:<term konca>]` | term moze miec `:` | S | K1 | ukrycie (LeaveParty + Disable); koniec ukrycia = term (np. `kr:044.2`) albo akcja `unhide`. Zakaz: glowa rodu, wladca, gubernator, jeniec, gracz, lista chronionych (P3) |
| `unhide:<bohater>[:<osada>]` | | S | K1 | powrot (teleport do osady albo siedziby) |
| `wound:<bohater>:<dni>` | 1-120 | S | K3 | rana |
| `skill:<bohater>:<umiejetnosc>:<d>` | OneHanded, TwoHanded, Polearm, Bow, Crossbow, Throwing, Riding, Athletics, Crafting, Scouting, Tactics, Roguery, Charm, Leadership, Trade, Steward, Medicine, Engineering (+Mariner/Boatswain/Shipmaster) | S | K4 | zmiana umiejetnosci (Jaime nr 37) |
| `item:<bohater>:<przedmiot>:<skad>` | skad = bohater/rod/osada | S | K1+ | przedmiot unikatowy ROT (ROTassets.xml) PRZENOSZONY od posiadacza - nigdy z niczego; `dragon_*` zakazane (P4) |
| `clan:<b>:<rod>` `court:<b>:<rod>` `lead:<b>` `prisoner:<b>:<rod\|osada>` `free:<b>:<rod>` `wander:<b>:<kultura>` `die:<b>` `marry:<b>:<b2>` `wed:<b>:<b2>` | jak arm_canon_moves | S | K0 | akcje CanonCompanions.ApplyAction dla postaci BEZ wierszy #move (rozdz. 8). `die` glowy rodu/wladcy = BLAD (uzyj `succeed`) |
| `succeed:<umierajacy>:<dziedzic>` | | S | K4 | dziedzic zostaje glowa rodu (i wladca), potem smierc (wzor Prologue.End). **Tylko pelna**, wyjatki lekkie: nr 42, 49, 52, 80 |
| `own:<osada>:<rod>` | | S | K9 | zmiana wlasciciela - **tylko pelna** |
| `ruler:<krolestwo>:<rod>` | | S | K9 | rod rzadzacy - **tylko pelna** |
| `leader:<rod>:<bohater>` | | S | K9 | glowa rodu - **tylko pelna** |
| `alliance:<k1>:<k2>` / `truce:<k1>:<k2>` | | S | K9/K10 | sojusz / rozejm - **tylko pelna**, wymaga wylacznika straznika Prologue.Crown i wpisu w CanonAlliances (P5) |

Zakazane: `move:` (P1 - para w #para), `remove` (smierc bez sladu - `die` albo `hide`), przedmioty `dragon_*` (P4), sojusze w wersji lekkiej (P5).
Kronika nie wola StartAlliance, nie rusza smokow (tekst i flaga), nie zmienia wojen ROT.

Zasady silnika K0 ustalone po paczkach K1-K10 (21.10):
- **`hide` na postaci juz ukrytej przez Kronike**: nowy term konca ZASTEPUJE stary (linia logu `UKRYCIE: nowy koniec`). Przyklad: Arya - 008.1
  `hide:lord_5_1_1:rotday:2000`, potem 044.2 `hide:lord_5_1_1:kr:098.2`.
- **Koniec ukrycia, ktory juz nigdy nie zajdzie** (`kr:` okna pominietego, `alive:` martwego itd. - rozdz. 3.2 p. 4) konczy ukrycie: `unhide`
  do siedziby rodu postaci z linia `KONIEC UKRYCIA (term nigdy)`. Dzieki temu Rickon (026.2 do `kr:084.1`), Melisandre (092.1 do `kr:112.1`)
  i Arya (do `kr:098.2`) nie zostaja ukryci na zawsze, gdy ich okno powrotu przepadnie. `hide` BEZ terminu = do akcji `unhide` albo na zawsze
  (tak K10 zapisuje smierc postaci z wierszami `#move`: Jaime, Theon, Varys, Sandor, Qyburn - zniknieciem, nie `die`).
- **`dragon_*`** = smoki (dragon_black ... dragon_smoke2, dragon_thumbnail w ROTassets.xml). `dragonglass_axe` i `dragonstone_*` to zwykly sprzet
  (wolno w `item`, gdy ma posiadacza - dzis topora z obsydianu nikt nie nosi, wiec akcja bylaby POMINIETA).
- **Towary** (`ware`, `#zadanie co`): id z XML albo z kodu gry (DefaultItems: grain, meat, hides, tools, iron, hardwood, charcoal, ironIngot1-6).
- **`die`/`hide` na postaci, ktora W CHWILI AKCJI jest glowa rodu albo wladca** (stan gry - walidator widzi tylko glowy z XML; np. Donella
  lord_7_5_2 w 020.1a, gdyby po smierci Halysa gra zrobila ja glowa Hornwoodow): akcja z wiersza/opcji `lekka` albo `obie` = POMINIETA `glowa rodu - tylko pelna` (wersja lekka
  nie zmienia glow rodow, rozdz. 16.4); z wiersza `pelna` = smierc z dziedzicem wybranym przez gre (dla `hide` - zawsze POMINIETA).
  Dlatego okna nie zabijaja w opcjach `obie` postaci, ktora inne okno robi glowa rodu (recenzja 21.10: 095.1 nie zabija juz Kevana - po 052.1
  jest glowa Lannisterow; ginie w 080.1, ktore ma warianty `succeed`).
- **`unhide` na postaci, ktorej Kronika nie ukryla** (np. Bran lord_5_7 w 088.2 i 098.1 - zadne okno go nie ukrywa, bo po Krwawych Godach
  jest glowa Starkow) = POMINIETA `nie ukryty`, bez teleportu - postac zostaje tam, gdzie jest w grze.
- **Akcja z rodem/bohaterem, ktorego nie ma w grze** (np. `relk:brotherhood`, gdy Bractwo nie ma przywodcy; `item` z rodu, w ktorym nikt nie nosi
  przedmiotu) = POMINIETA z powodem, reszta skutkow idzie dalej.

**Stan po K0 krok B1 (galaz w-toku/kronika-00-silnik, 22.10): akcje swiata i ekonomii w silniku** (szczegoly: `docs/noc-1920/kronika-k0-CHANGELOG.md`, B1):
- `gold` - platnik placi najwyzej to, co ma (`N%` - z jego biezacego zlota), niedoplata w linii `zloto NNN.w: platnik -> odbiorca kwota (zaplacone k...)`; nic
  zaplacone = POMINIETA. Bohater ukryty (Disabled) i martwy nie placi i nie dostaje (kiesa poza obiegiem). Rod = kiesa glowy rodu (rod gracza = gracz).
- `security`/`loyalty` - tylko miasto i zamek (wies = POMINIETA), wartosc gry 0-100.
- `pop` - **dalej POMINIETA** ("akcja jeszcze niewspierana: ksiega ludzi bez przyczyny kronika"): ksiega ludzi nie ma jeszcze przyczyny `kronika`; ludzie bez zmian.
- `garrison:<osada>:<n>:pula` = ochotnicy notabli osady i jej wsi (te same miejsca, z ktorych bierze autowerbunek zalogi), najwyzej tylu, ilu jest; `:<osada>` =
  zdrowi zolnierze jej zalogi. Zaloga w bitwie = CZEKA.
- `item` - cel gracz: ekwipunek druzyny; inna postac: pasujacy slot wyposazenia (zajety - zamiana, zdjety przedmiot wraca do dawnego posiadacza).
- `wound:<b>:<dni>` - zycie 1 i rana trzymana co dobe do konca terminu (gracz tez nie walczy w tym czasie).
- `skill` - poziom 0-330 (z XP), `ware` - najwyzej tyle towaru, ile ma rynek zrodla.

**Stan po K0 krok B2 (galaz w-toku/kronika-00-silnik, 22.10): kazda akcja z tabeli ma procedure** (szczegoly: `docs/noc-1920/kronika-k0-CHANGELOG.md`, B2;
zgodnosc danych z silnikiem: `python -I tools/kronika/kronika_silnik.py`):
- `band`, `task` - rozdz. 6 i 7. `tourney:<miasto>` - turniej kanonu GrandTourney (CanonApi.ProclaimRoyal odbiciem, jak Turniej Reki prologu, bez oplat gracza);
  bez GrandTourney - POMINIETA; turniej juz jest w miescie - OK bez nowego; oblezenie albo bunt - CZEKA.
- `muster` - jednorazowy rozkaz "idz do osady" dla wolnych druzyn lordow krolestwa (bez armii, bitwy, oblezenia, morza, scen prologu, rodu gracza); AI gry moze go
  zmienic; po `dni` linia "zbiorka ... koniec: na miejscu a z n" (zapis "km").
- `clan/court/lead/prisoner/free/wander/die/marry/wed` - CanonCompanions.ApplyAction (ta sama droga co #move). Z Kroniki dodatkowo POMINIETA: postac z wierszami
  `#move` (tez druga strona marry/wed), `die` postaci z listy chronionych przed jej wydarzeniem ROT, `die` glowy rodu/wladcy w chwili akcji z wiersza lekka/obie.
- `succeed` - tylko pelna (wiersz pelna i klucz TAK), wyjatki lekkie 42/49/52/80; dziedzic musi byc zywy i w rodzie umierajacego; umierajacy glowa rodu - najpierw
  dziedzic glowa rodu (ChangeClanLeaderAction - wladca krolestwa rodu panujacego idzie za nim), potem smierc bez komunikatu (jak `die`); w bitwie - CZEKA.
- `own`, `ruler`, `leader`, `truce` - tylko pelna; osada gracza, rod gracza, gracz-wladca - POMINIETA; `truce` POMINIETA, gdy ROT wymusza wojne tej pary.
- `alliance` - **zawsze POMINIETA z powodem** (straznik Prologue.Crown pilnuje albo sojusz spoza listy CanonAlliances CrashScribe; plan P5 - decyzja K9/K10).
- `pop` - dalej POMINIETA (B1).

## 12. Uchwyty kod:<nazwa>

Najwyzej 10 na cala Kronike (bez `test`). Nowy uchwyt = wpis w `KODY` w `kronika_check.py` (dozwolone wydarzenia, `gdzie`, opis) i tutaj.
Stan po K1-K10 (21.10): **3 uchwyty tresci + `test`** - kazde inne miejsce bez slownika akcji autorzy zapisali tekstem, flaga albo przyblizeniem
(lista luk nizej). Kolumna `gdzie`: `okno` = kolumna `skutki` okna (raz przy rozstrzygnieciu, przed lista), `opcja` = `skutki` KAZDEJ opcji `#wybor`
(po wyborze; uchwyt czyta `kr:NNN.w=x`). Walidator odrzuca uchwyt w innym miejscu (tez w `po_rozbiciu` i `nagroda`).

Wspolne zasady dla programisty K0 (Chronicle.Codes.cs - jeden `switch` po nazwie):
- wywolanie `RunCode(nazwa, okno, wariant, opcja)` z biezacym stanem okna; uchwyt NIE czyta tekstu okna, tylko stan gry i `kr:`/flagi;
- klasa T (tekst) w sensie klucza ChronicleWorldChanges: uchwyt sam sprawdza klucz i przy `NIE` robi tylko czesc R (relacje) albo nic - z linia;
- zamknieta ekonomia jak w rozdz. 11: ludzie przez PeopleBook (do/z puli regionu, nigdy z niczego), zloto zawsze z platnikiem, bez mnoznikow cen;
- jedna linia logu `KRONIKA kod <nazwa> | okno NNN.w[war] | ...liczby... | OK/POMINIETA (powod)`; wyjatek = POMINIETA z powodem (okno i reszta
  skutkow ida dalej), bez globalnego wylacznika (CLAUDE.md: licz potkniecia, nie gas funkcji);
- uchwyt nieznany silnikowi danej wersji (paczka wgrana przed kodem) = POMINIETA `brak uchwytu`, okno sie pokazuje.

| Nazwa | Okna | Gdzie | Od | Co robi - opis dla programisty K0 |
|---|---|---|---|---|
| `test` | 000.1 | okno | K0 | proba silnika: tylko linia logu `kod test`, bez zmian w swiecie (autotest K0a/K0b) |
| `zaraza` | 076.2 | okno | K7 | **Blada klacz pod Meereen** (town_K1, wlasciciel z warunkow okna = empire_s). (1) Meereen traci 8-12% ludzi przez PeopleBook z przyczyna `choroba` (do puli regionu Zatoki - nie znikaja z ksiegi). (2) Partie stojace w promieniu ok. 30 jednostek od Meereen (oblegajace i obroncy, tez gracza) dostaja rannych: 10-20% zdrowych zolnierzy kazdej partii -> ranni (WoundedNumber), bez smierci. (3) security town_K1 -5. Ten sam rzut dla gracza i AI. Opcje 076.2 (oboz za zloto gracza / zamkniete bramy) sa osobno w `#wybor` - uchwyt ich nie czyta. Log: ludzi miasta przed/po, partii, rannych. Wersja lekka i pelna jednakowo. |
| `dzwony` | 115.1 (wiersz glowny = wersja ostra, przy gniewie krolowej 113; wariant a lagodny NIE ma uchwytu) | okno | K10 | **Krolewska Przystan plonie** po zdobyciu przez empire_s (wyzwalacz `captured:town_EW4:empire_s`, max 1). (1) town_EW4 traci 25-35% ludzi przez PeopleBook z przyczyna `pozar` (do puli regionu korony). (2) Dobrobyt i zabudowa przez Devastation miasta (jak lupienie, poziom maksymalny) - bez kasowania budynkow. (3) Lup rodu zdobywcy: 30% kasy miasta (`@kasa:town_EW4`) -> glowa rodu-wlasciciela miasta po zdobyciu (platnik kasa miasta - zamknieta ekonomia). Udzial GRACZA nie nalezy do uchwytu: to opcja b (`gold:@kasa:town_EW4:@gracz:5000`) wykonana PO uchwycie, z tego, co zostalo w kasie (niedoplata z linia). Wersja lekka i pelna jednakowo (smierc Cersei/Jaimego to okno 115.2). |
| `wielka_rada` | 117.1 i 117.1a - w skutkach kazdej opcji a-d | opcja | K10 | **Wielka Rada po smierci Daenerys**. Uchwyt czyta glos gracza `kr:117.1=a..d` (a Bran, b gracz, c Jon, d bez glosu - opcja bierna i autotest). LEKKA (ChronicleFullVersions=NIE): wynik rady = Bran (jak w tekscie okna), bez zmian wladcow, rodow i krolestw; uchwyt robi tylko relacje glow rodow krolestw Westeros (lista z kolumny `gracz` okna) z graczem: glos a +3, b -5, c -2, d 0 - i linie logu. PELNA (TAK): glosy glow rodow tych krolestw = slawa rodu + relacja z kandydatem; kandydaci: Bran (lord_5_7, zawsze, premia kanonu +25%), gracz (tylko przy glosie b), Jon (tylko zywy, przy glosie c); glos gracza liczy sie jak jego rod. Wynik w log i we flagach `k117_wynik_bran/gracz/jon`; zmiana korony (`ruler`/`leader` krolestwa trzymajacego town_EW4) wymaga wylacznika straznika Prologue.Crown (P5) i decyzji przy silniku K10, bo Bran jest glowa rodu Starkow (Polnoc) - korona dla niego oznacza rozdzielenie rodu albo krolestwa. Do tej decyzji pelna = jak lekka + flaga wyniku. Flagi `k117_bran/ja/jon/wstrzymal` ustawiaja same opcje. |

**Stan po K0 krok B2 (Chronicle.Codes.cs, RunCode):** wszystkie 4 uchwyty maja galaz. `zaraza` - ranni w partiach w promieniu 30 od town_K1 (jeden rzut 10-20%
dla gracza i AI, bez smierci, umarli nie choruja) i security town_K1 -5; **czesc (1) ludzie miasta BEZ ZMIAN** - ksiega ludzi nie ma przyczyny `choroba` ani drogi
miasto -> pula regionu dla Kroniki (jak akcja `pop`), linia mowi o tym. `dzwony` - lup 30% kasy town_EW4 dla glowy rodu-wlasciciela zwykla akcja `gold` (linia
"zloto"); **czesci (1) ludzie i (2) Devastation BEZ ZMIAN** - ksiega ludzi bez przyczyny `pozar`, a Devastation Armoury dziala tylko na wsie (lupienie miasta robi
CitySack przy zdobyciu; Town.Prosperity wprost gryzie sie z DailyLife). `wielka_rada` - lekka: relacje glow rodow krolestw z kolumny `gracz` okna (a +3, b -5,
c -2, d 0), bez flag; pelna: do tego glosy (slawa rodu dla kandydata z najwyzsza relacja, Bran x1.25, glos gracza = slawa jego rodu) i flaga
`k117_wynik_bran|gracz|jon`, korona bez zmian. Klucz World Changes NIE: `zaraza`/`dzwony` POMINIETA, `wielka_rada` robi relacje (czesc R).

Luki zgloszone przez autorow K1-K10 - BEZ uchwytu (zapisane tekstem/przyblizeniem; nowy uchwyt dopiero, gdy Jeff zechce, w limicie 10):
pojedynek gracza z Gora (050.2a - dzis pewna wygrana z rana 30 dni; kandydat `pojedynek`), gorsze warunki kredytu Banku w dlugach m96 (058 - dzis
przeplywy `gold`; kandydat `bank`), przejscie wojska do armii innego wodza (091 rycerze Doliny do Jona, 096 Dothrakowie - dzis `garrison`/tekst),
tabor z celem AI (104 - dzis wybor gracza i banda), znacznik najezdzcy (024), "zjedzone konie" (015.1 opcja bierna bez skutku), termy stanu,
ktorych nie ma (oblezenie Winterfell przez Innych 112.1, smierc Nocnego Krola w Winterfell 112.2, bohater w osadzie 003/033/035).

## 13. Okna powracajace (kolumna `powtorz`)

`powtorz` = `<wyzwalacz>[:arg]` i opcje ` ; prolog` (dziala tez w prologu), ` ; odstep:N` (dni miedzy instancjami tego samego klucza),
` ; max:N` (najwyzej N instancji). Instancja = `NNN.w#klucz` w zapisie; najwyzej 1 instancja na dobe, rejestr kluczy 400 (P/E).
`od` = 0 albo dzien startu. Tokeny instancji: `@cel` (osada), `@kto` (bohater), `@rodkto` (jego rod), `@region` (krolestwo regionu),
`@liczba`, `@pora` - w zmiennych i jako argumenty akcji/termow. Teksty z tokenami = bez lektora (staly akapit moze miec `lektor=1` tylko bez zmiennych).

| Wyzwalacz | Okno | Kiedy / klucz |
|---|---|---|
| `season` | 013 | zmiana pory roku WesterosClimate; `@pora` |
| `siegehunger:N` | 015 | oblezona twierdza N dob bez jedzenia; klucz osada + start oblezenia; `@cel` |
| `ironraid` | 024 | wies na wybrzezu zlupiona przez Zelaznych; klucz port + 30 dni; `@cel`, `@region` |
| `oldage` | 039 | glowa waznego rodu zmarla ze starosci; `@kto`, `@rodkto`, `@region`, `@cel` = siedziba rodu zmarlego (Clan.HomeSettlement; brak - najwieksza osada rodu) - K4 rozpoznaje krolestwo przez `own:@cel:<krolestwo>` (warianty 039.1a/039.2/039.3 z innym tematem) |
| `brokenmen` | 045 | >30% ludzi regionu poza domem i rabunek w 30 dniach; klucz region + 90 dni; `@cel`, `@region` |
| `others:N[,N...]` | 061 | liczba osad Innych osiaga N (np. `others:1,5,15,25`); `@liczba` |
| `captured:<osada>[:<krolestwo>]` | 109, 110, 112, 115 | osada zmienila wlasciciela (na krolestwo) |
| `besieged:<osada>` | 110, 112 | oblezenie osady |
| `wizja:N` | 066 (088 w K8 - okna jednorazowe, bez wizji) | okresowo co N dni, tekst z prawdziwego stanu mapy; `@cel` = najpierw oblezona twierdza Polnocy albo Muru, inaczej najblizsza (do Winterfell) osada Innych (ROTclan_126), inaczej brak instancji; `@region` = krolestwo `@cel` (propozycja K6, przyjeta) |

**Stan po K0 krok B2:** wszystkie 9 wyzwalaczy dziala (Chronicle.Recurring.Unsupported pusty). `brokenmen` (045): okreg OutlawLaw z rabunkiem w ostatnich 30 dniach
(PeopleBook.LastStrike) i ponad 30% ludzi poza domem = (uchodzcy wsi okregu + pula wyrzutkow) wobec (to + ludzie w domu we wsiach okregu); klucz okreg-okres 90 dni;
`@cel` = warownia okregu, `@region` = jej krolestwo. Instancja okna z `band:` - ta sama banda `bNNN` najwyzej raz naraz na mapie.

Tokeny w miejscach typu zlozonego (`plotka:<id>:<osada|krolestwo>`, `own:<osada>:<rod|krolestwo>`, `prisoner:...:<rod|krolestwo|osada>`)
pasuja, gdy pasuja do ktoregokolwiek typu (np. `plotka:045.1_P:@region:60`) - poprawka walidatora 21.10. Kolumna `osada` w `#banda` nie przyjmuje
tokenow (banda ma stale miejsce; 045 tworzy bande tylko w 3 krainach).

## 14. Tematy obrazkow (arm_kronika_tematy.tsv, plan P8)

Bez grafik Jeffa: kazde okno ma `temat`, silnik bierze obrazek z puli tematu "na zmiane". Pula = pliki `Images/Kronika/kronika_<temat>_<n>.png|jpg`
(jesli kiedys beda) + `Images/Prolog/prolog_NN` z kolumny `prolog` (jesli plik istnieje; dzis 01-03) + sprite'y ROT. Indeks = staly skrot klucza
okna mod n; ten sam obrazek co w poprzednim oknie sesji -> nastepny. Kolumny pliku: `temat`, `sprite` (po przecinku), `prolog` (NN po przecinku),
`tylko` (numery wydarzen NNN; puste = wszystkie), `opis`.

| Temat | Sprite'y ROT | Prolog | Do czego |
|---|---|---|---|
| `dwor` | kingslanding, StannisIronThrone, joffrey | 01,06,11,12,15,17,20,23 | Przystan, dwor, rada, turnieje |
| `intryga` | messenger-1, messenger-2, messenger-img, joffrey | 12,16 | listy, kruki, spiski |
| `oblezenie` | blackwater, harrenhal, wallfall, eastwatch | 19 | mury, szturm, glod w oblezeniu |
| `wojna` | RobbMarching2, RobbMarching3, robb, jon_stand | 09 | armie w marszu, zbiorki |
| `bitwa` | RobbMarching3, blackwater, WildlingsFail, jon_stand | 09 | starcie, rzez, poscig |
| `polnoc` | robb, RobbMarching2, ned_stark, robb_stark | 02,04,05,06,07,08 | Winterfell, Starkowie |
| `mur` | jon, jonsnow, eastwatch, jeor | 07,10 | Czarny Zamek, Straz |
| `za_murem` | freefolk1, freefolk2, fist, WildlingsFail | 10 | Wolni Ludzie, Piesc, zwiad |
| `inni` | nightking, freefolk3, wallfall, fist | | Inni, umarli, Dluga Noc |
| `zima` | freefolk3, fist, eastwatch, jon_stand | | zima, mroz, biale kruki |
| `zaswiaty` | melisandre, jon_rez, nightking | | wizje, czary, powrot zza grobu, trojoka wrona |
| `smocza_skala` | melisandre, StannisSetsSail, StannisIronThrone | | Stannis, Melisandre, Davos |
| `boltonowie` | ramsay, roose_bolton | | Boltonowie, Dreadfort |
| `essos` | dany_invasion, traveling_merchant | 03,18,21 | Daenerys, Zatoka, Wolne Miasta |
| `morze` | StannisSetsSail, aegon | | floty, Zelazne Wyspy, Braavos, Aegon |
| `dorne` | messenger-1, messenger-2, kingslanding | | Dorne (brak sprite'a ROT) |
| `dolina` | harrenhal, messenger-img | 14,16 | Dolina (brak sprite'a ROT) |
| `dorzecze` | riverlands, harrenhal, messenger-img | 13,19,20 | Dorzecze, Bractwo, karczmy |
| `smierc` | renly-slain, rickard, jeor, ramsay | 08,22 | smierc, egzekucja, pogrzeb |
| `wesele` | traveling_merchant, robb_stark | 04 | sluby, uczty, Blizniaki |
| `stannis_upadek` | stannis-slain | | TYLKO nr 81 (`tylko` = 081) |

Poza rotacja (walidator odrzuca): event-bg, BlackwaterWarning, Davos, Stannis, Ship1, Ship2, testevent, jonsnowres, RobbMarching, jon_death, ramsay_death.
Wydarzenia "wedlug regionu" (6, 39): osobne warianty z roznym `temat`.

## 15. Lista chronionych (P3/D; walidator i kod)

Akcje `die`, `hide`, `succeed` (umierajacy) na postaci z listy - tylko gdy w warunkach okna (glownego albo wariantu) albo opcji jest
**osobny skladnik** z wymaganym termem (`done:X` spelnia tez `ev:X`). Do tego zawsze: glowa rodu i wladca (XML na starcie) - `die` = BLAD (uzyj `succeed`),
`hide` = BLAD.

| Postac | id | Wolno dopiero po |
|---|---|---|
| Ramsay Snow | lord_1_29 | `done:BastardsEvent` |
| Mance Rayder | lord_2_3 | `done:WallSiegeEvent` |
| Jon Snow | lord_5_17 | `done:BastardsEvent` (plan: JonEvent; Bastards*Event tez go uzywaja) |
| Daenerys Targaryen | lord_1_14 | `ev:DanyInvasionEvent` |
| Stannis Baratheon | lord_1_11 | `done:StannisDeathEvent` albo `done:EastwatchEvent` (plan: WallEvent; EastwatchEvent uzywa Stannisa) |
| Roose Bolton | lord_1_17 | `done:RooseDeathEvent` |
| Robb, Bran, Sansa Stark | lord_5_1, lord_5_7, lord_5_13_1 | `done:RedWeddingEvent` |
| Joffrey Baratheon | lord_7_1 | `done:JoffreyEvent` |
| Aegon Targaryen | ROTlord_369 | `ev:AegonInvasionEvent` |
| uzupelnienie z dekompilacji ROT.Events: | | |
| Renly Baratheon, Cortnay Penrose | lord_1_9, lord_1_25 | `done:RenlyDeathEvent` |
| Davos Seaworth, Lancel Lannister | lord_WE9_l, lord_4_14 | `done:BlackwaterSiegeEvent` |
| Amory Lorch, Polliver | lord_4_25, lord_4_25_1 | `done:HarrenhalSiegeEvent` |
| Edmure, Brynden Tully, Walder Frey, Catelyn, Greatjon | lord_1_3, lord_1_50, lord_1_5, ROTlord_513, lord_5_16 | `done:RedWeddingEvent` |
| Rickard Karstark | lord_2_18 | `done:RickardKarstarkExecutionEvent` |
| Jeor Mormont | lord_5_3 | `done:JeorMutinyEvent` |
| Cersei Lannister | lord_4_2 | `done:JoffreyEvent` |
| Alliser Thorne, Eddison Tollett, Tormund | ROTlord_568, lord_5_21, lord_2_19 | `done:JonEvent` |
| Harald Karstark, Smalljon Umber | lord_2_23, lord_5_20 | `done:BastardsEvent` |
| Yezzan zo Qaggaz | ROTlord_486 | `ev:DanyInvasionEvent` |
| Jon Connington | ROTlord_373 | `ev:AegonInvasionEvent` |

## 16. Zasady tresci (dla 5 autorow)

1. **Wszystkie 117 wydarzen** (decyzja Jeffa). Kazde okno: tytul + 2 akapity po angielsku, **wlasnymi slowami** - zadnych cytatow
   z ksiazek ani serialu (zadnych zdan dialogu, zadnych cudzyslowow; przydomki bez cudzyslowu, mowa zalezna).
2. **Kanon**: najpierw ksiazki, po ksiazkach serial (kolejnosc i fakty jak w `KRONIKA-WESTEROS-V2`, wiersze "Co sie dzieje" i "Skutek").
3. **Piaskownica**: okno sprawdza swiat (kto zyje, kto rzadzi, czyje sa osady, gdzie gracz). Swiat inny niz w ksiazce = wariant
   (`war`) z warunkami i tekstem pasujacym do stanu, albo okno przepada (warunki w wierszu glownym). Teksty wariantow nie moga
   twierdzic czegos, czego warunki nie sprawdzaja.
4. **Wersje lekkie domyslnie**. Pelne (zamki, wladcy, sojusze, upadki rodow) tylko jako warianty `wersja=pelna` (klucz ChronicleFullVersions,
   domyslnie NIE). 16 duzych: 23, 26, 56, 59, 63, 81, 86, 94, 95, 100, 103, 107, 108, 115, 116, 117. Pelne 95/97/107 dopiero w K9/K10 (P5).
5. **Lektor pozniej**: `lektor=1` dla okien bez `{ZMIENNYCH}` (w praktyce okno 1 kazdego wydarzenia i stale akapity). Tekst po nagraniu
   jest zamrozony (md5 w arm_lektor.tsv - walidator).
6. **Obrazki**: tylko `temat` (rozdz. 14), bez promptow.
7. **Postacie nieobecne w ROT** (P7: Hoster Tully, Ellaria, Olenna Tyrell, Slynt, Dontos, Rodrik Cassel, Rorge, Edric Storm, Gilly, Sigorn;
   Pycelle tylko notabl) - wystepuja tylko w tekscie, bez akcji. Nowe postacie (NP) tylko w wersji pelnej.
8. **Poprawki faktow z badania B** (wpisac w dane): Piesc `castle_S6` nalezy do Tormunda (`clan_sturgia_7`), nie do Strazy; Arianne
   (ROTlord_372) jest w rodzie Aegona (ROTclan_84); Asha (lord_2_10) i Yara (lord_2_7_1) to dwie postacie - w calej Kronice jedna rola:
   Yara (przechodzi do serialu); po JoffreyEvent ROT robi wladczynia CERSEI (Tommen krolem tylko w tekscie); RamsayEvent sam przenosi
   Karstarkow; BastardsEvent niszczy rod Snow (nr 83: Sansa/Brienne/Podrick do `clan_battania_1`, nie do Snow); ROT nie bierze Edmure'a
   do niewoli; Yunkai trzymaja Drudzy Synowie (`clan_khuzait_2`); Halys i Daryn Hornwood w ROT zyja; Barristan ma wlasny rod
   (`clan_sturgia_9`) u Daenerys; Jorah to glowa `clan_nord_2` i pan Astaporu.
9. **Rozstrzygniecia domyslne** (plan "Pytania", wedlug ksiazki i wersji lekkiej): 46/101 Jorah - tylko relacja (bez wygnania-wedrowca,
   wtedy 101 przepada z powodem); 47 bez nowej postaci Pani Kamienne Serce; 71 tylko relacje Karstarkow; 77 bez Roberta Stronga; 86 zamach
   prowadzi Obara Sand (lord_3_10) zamiast Ellarii; 95/97/103 role Olenny - nowa glowa Tyrellow (Willas lord_1_24 albo Garlan lord_1_75),
   Olenna tylko w tekscie; 95 sama relacja Reach z korona (bez wojny); 100 bez sojuszu Wysp z korona; 106 martwy smok tylko w tekscie;
   29 (Jon), 38 (Bran), 57 (Mance), 65 (Davos), 77 (Cersei), 78 (Daenerys) - lekkie, bez ukrywania glow rodow i wladcow.
10. **Warunki obowiazkowe z planu**: nr 5 i 10 - `done:NedEvent` (wojny Essos ruszaja od kalendarza jeszcze w prologu); 19/27 to okna za
   stanem Prologue.Crown (`cw:dorne`, `cw:reach`) - Kronika nie zawiera sojuszy; 16 za `mv:p10`/`mv:m01` (Jaime w Riverrun, E5);
   17 bierze bande Berica z E5; 32 za `mv:m02` albo `kr:` dla opcji alarmu; 52 za `m03`; 75 za `m24`/`m23`; 99 za `m04`; 31 za `m26`.
11. **Zamknieta ekonomia**: zloto zawsze `gold:<platnik>:<odbiorca>:...`; ludzie przez `pop`/`band` (pula); towar przez `ware` (z rynku);
    przedmiot przez `item` (od posiadacza). Bez sztywnych mnoznikow cen (nr 58 - przez dlugi m96 albo uchwyt kod).
12. **Wydarzenie wielookienne** caloscia w paczce swojego pierwszego okna (rozdz. 18).
13. Kazda paczka przed commitem: `python -I tools/kronika/kronika_check.py` = WYNIK OK; wynik w CHANGELOG ("Ryzyko / co sprawdzic").

## 17. Walidator (tools/kronika/kronika_check.py)

```
python -I tools/kronika/kronika_check.py                     # wszystko (k00..k10, tematy, pary z arm_canon_moves)
python -I tools/kronika/kronika_check.py Armoury/ModuleData/arm_kronika_k03.tsv   # wynik dla jednego pliku (reszta czytana do odwolan)
opcje: --gra <katalog gry> (domyslnie Steam), --repo <katalog repo>, --ciche (bez UWAG), --stat (okna/wybory/bandy/znaki lektora na paczke)
```

Wypisuje `plik:wiersz: BLAD|UWAGA: ...` i linie `kronika_check: ... | bledy N, uwagi M | WYNIK OK|BLAD`. Kod wyjscia 0 = bez bledow.
Tylko biblioteka standardowa; miara linii = kopia `e4c/linie.py` w `tools/kronika/linie.py` (font Galahad z katalogu gry).
Sprawdza: naglowki i liczbe kolumn; unikalne id, wariant pod wierszem glownym, czas tylko w glownym; paczke wlasciwa dla numeru;
znane termy i akcje z liczba i typem argumentow; istnienie bohaterow/rodow/krolestw/osad/kultur/przedmiotow w XML ROT; wydarzenia ROT;
tokeny (powracajace tylko z `powtorz`); pojemnosc tekstu, prefiksy `{=ARM_KR_...}`, zmienne, znaki fontu, polskie litery, cudzyslowy;
`lektor=1` bez zmiennych i md5 tekstu z arm_lektor.tsv (kolumna 5); 2-4 opcje a-d, ostatnia bierna i zawsze dostepna; zloto z platnikiem;
akcje tylko-pelne poza `wersja=pelna` (wyjatki succeed 42/49/52/80); lista chronionych; glowy rodow i wladcy; postacie z `#move`;
`move:`/`remove`/`dragon_*`; pary `#para` w obie strony z arm_canon_moves; odwolania `kr:` (okno i opcja), `wiesc:/plotka:` (#tekst),
`band:`, `task:`; nagrobki; tematy (sprite'y z ROT-ContentSpriteData.xml, poza rotacja, `stannis-slain` tylko 081, prolog 01-23).
UWAGI (nie psuja wyniku): odwolanie do paczki jeszcze nie napisanej, nieuzyty tekst/banda, flaga sprawdzana a nieustawiana,
powtorzony tytul (lektor szuka zapasowo po tytule), towar nieznaleziony w XML, klan bandy spoza bandyckich/mniejszych, wiersz `#move` z samymi termami `kr:`.

Poprawki walidatora 21.10 (po paczkach K1-K10; kazda zgloszona przez autora i sprawdzona):
- `_W` okna z kotwica `przed` albo z koncem `do` liczy sie jako uzyty (zapasowa wiesc z rozdz. 3.2 p. 4) - wczesniej 31 falszywych UWAG;
- towary z kodu gry (DefaultItems: grain, iron, charcoal ...) sa znane - wczesniej UWAGA "nie znaleziony w XML" (id sprawdzone w TaleWorlds.CampaignSystem.dll);
- zakaz smokow to prefiks `dragon_`, nie `dragon` - wczesniej odrzucal `dragonglass_axe` i `dragonstone_*`;
- tokeny w miejscach typu zlozonego (`osada|krolestwo`, `rod|krolestwo`, `captor`, `source`) - wczesniej zawsze BLAD;
- `kod:` tylko w miejscu z rejestru (`gdzie` = okno/opcja) - nowa regula (Wielka Rada byla w skutkach okna, przed glosem);
- `--stat`: okna, wiersze, wybory, bandy, zadania, okna i wiersze z `lektor=1`, znaki lektora (tytul+gora+dol bez prefiksu `{=...}`) i RAZEM k01-k10.
Testy nowych regul: scratchpad 7aee5f12/kr-final/neg3.py (6 BLEDOW, 4 UWAGI - kazda regula raz w obie strony); stare neg.py/neg2.py/pos.py bez zmian
wyniku poza towarem `iron` (kr-final/old_*.txt vs new_*.txt).

## 18. Podzial na paczki K1-K10

Wedlug osi dni (dokument v2), po 11-12 wydarzen. Rozni sie od planu J (12/10/10/12/10/12/11/13/13/14): wyrownane do 11-12, kolejnosc osi
ta sama, nowe klocki przeniesione do paczki, ktora pierwsza ich potrzebuje. Rozdzial V (serial, 75-117) nachodzi na koniec IV, wiec K7-K9
mieszaja numery. Wydarzenie wielookienne - caloscia w paczce pierwszego okna. Sceny "juz w grze" (dopisek "zrobie z E4") dostaja id 901-906.
Walidator pilnuje, zeby okno lezalo w pliku swojej paczki. Okien razem 204 (+ sceny).

| Paczka | Wydarzenia | Dni (ok.) | Okna | Nowe klocki silnika (pierwsze uzycie) |
|---|---|---|---|---|
| K1 `k01` | 1-12 + 901 | 10-125 | 001.1 001.2 002.1 002.2 003.1 003.2 004.1 005.1 006.1 007.1 007.2 008.1 008.2 009.1 009.2 010.1 010.2 011.1 012.1 + 901.1 (19+1) | listy wyboru (2, 3, 12), `band` (8 Lorch), `tourney` (3), `security/loyalty` (9), `gold` (12), `plan:` (2), `hide` (8 Arya), m22 `+kr:008.1`, m11/m20/m21 `remove->hide` (przeniesione do commitu silnika K0 - rozdz. 8) |
| K2 `k02` | 13-24 + 902 | 100-520 (+powracajace) | 013.1 013.2 013.3 014.1 014.2 015.1 015.2 016.1 016.2 017.1 017.2 018.1 019.1 020.1 020.2 021.1 021.2 022.1 023.1 024.1 + 902.1 (20+1) | `task` (14, 20), `powtorz` season (13), siegehunger (15), ironraid (24), `relk`, `muster` (14, 22), `pop`, 16 za `mv:p10/m01`, 17 banda Berica (E5), 19 `cw:dorne`, 23 lekka |
| K3 `k03` | 25-36 + 903 | 500-930 | 025.1 025.2 026.1 026.2 027.1 028.1 028.2 029.1 029.2 030.1 031.1 031.2 031.3 032.1 033.1 033.2 034.1 035.1 036.1 + 903.1 (19+1) | `wound`, `garrison` (34), 26 lekka, 27 `cw:reach`, 31 za `m26` (031.2 Hoster - tylko tekst albo nagrobek), 32 `m02`/`kr:`, 33 za `m10` |
| K4 `k04` | 37-48 | 830-1560 | 037.1 037.2 038.1 038.2 039.1 039.2 039.3 040.1 041.1 041.2 042.1 042.2 043.1 044.1 044.2 044.3 045.1 046.1 046.2 047.1 047.2 048.1 048.2 (23) | `skill` (37), `powtorz` oldage (39), brokenmen (45), `succeed` (42 lekka), 44 `m11`, 47.2 po 64/70 (K6) |
| K5 `k05` | 49-60 + 904, 905 | 930-1460 | 049.1 049.2 050.1 050.2 051.1 051.2 052.1 052.2 053.1 053.2 053.3 054.1 055.1 055.2 056.1 056.2 057.1 058.1 058.2 058.3 059.1 059.2 060.1 060.2 + 904.1 905.1 (24+2) | 49 `hide` Sansa (po JoffreyEvent), 49 `succeed` Robin (lekka), 52 `succeed` Kevan + `m03`, 56/59 lekkie, 58 Bank (m96 albo kod), 905 za `m21` |
| K6 `k06` | 61-72 | 1300-1460 (+61 powracajace) | 061.1 061.2 061.3 062.1 063.1 064.1 064.2 064.3 065.1 065.2 066.1 066.2 067.1 067.2 068.1 068.2 069.1 069.2 070.1 070.2 071.1 071.2 072.1 072.2 072.3 (25) | `powtorz` others (61), `wizja` (66), `ware`, 63 lekka, 65 Davos bez ukrycia (glowa rodu) |
| K7 `k07` | 73-76, 81-87 + 906 | 1320-1900 | 073.1 073.2 074.1 075.1 075.2 075.3 076.1 076.2 081.1 081.2 082.1 082.2 082.3 083.1 083.2 084.1 084.2 085.1 086.1 086.2 087.1 + 906.1 (21+1) | `kod:zaraza` (76), 75 za `m24/m23`, 81 lekka (`stannis_upadek`), 82 `unhide` Sam, 86 lekka (Obara), 906 Tyrion w niewoli |
| K8 `k08` | 77-79, 88-95 | 1470-1910 | 077.1 077.2 078.1 078.2 079.1 079.2 088.1 088.2 089.1 090.1 091.1 092.1 092.2 093.1 094.1 094.2 095.1 095.2 (18) | 79 `plan:AegonInvasionEvent/DanyInvasionEvent`, 88 `unhide` Bran, 89 `unhide` Ogar, 90 `item` heartsbane, 94/95 lekkie |
| K9 `k09` | 80, 96-105 | 1700-2200 | 080.1 096.1 097.1 098.1 098.2 099.1 099.2 100.1 101.1 102.1 102.2 103.1 103.2 104.1 104.2 105.1 (16) | 80 `succeed` Lannisterowie (lekka), 99 za `m04`, 100/103 lekkie, pelne 95/97 dopiero z wylacznikiem straznika |
| K10 `k10` | 106-117 | 2050-koniec | 106.1 106.2 107.1 108.1 109.1 110.1 110.2 111.1 112.1 112.2 112.3 113.1 114.1 115.1 115.2 116.1 116.2 117.1 117.2 (19) | `powtorz` captured/besieged (109, 110, 112, 115), `kod:dzwony` (115), `kod:wielka_rada` (117), 107/108/115/116/117 lekkie |

Sceny "juz w grze" (okna Kroniki pod zmianami stron z prologu, z wyborem tylko tam, gdzie dokument go podaje):
901 Tyrion jako Reka jedzie do stolicy (K1, dni 20-60) | 902 Theon wraca na Pyke (K2, 330-430, wybor Theon/Asha->Yara dla wasala Wysp) |
903 Nowy lad po Czarnym Nurcie (K3, 776-810) | 904 Miecz dla Brienne (K5, 1130-1180) | 905 Sam plynie do Starego Miasta (K5, 1240-1420, za `m21`) |
906 Tyrion w niewoli i Drudzy Synowie (K7, 1480-1620). Kazda scena moze miec kilka okien (.1, .2 ...).

Okna wczesniejsze niz start swojej paczki (do uwagi autorow): 050.1 (930-1000, przyjazd Oberyna) i 058.1 (okno 0, opcja, ok. 800) - przy
paczce wgranej pozniej przepadna bez lawiny. 047.2 (1430-1560) czeka na 064/070 z K6.

Proponowany przydzial dla 5 autorow (po 2 paczki po kolei osi): A = K1+K2, B = K3+K4, C = K5+K6, D = K7+K8, E = K9+K10.
`arm_canon_moves.tsv` jest wspolny - kazda paczka dopisuje tylko swoje termy `kr:` (i pary w swoim pliku).

## 19. Najczestsze identyfikatory (sprawdzone w XML ROT 8.1.8)

Krolestwa: `battania` Polnoc (Robb lord_5_1), `vlandia` korona - Baratheon z Przystani (Joffrey lord_7_1), `empire` Dolina (Lysa lord_4_6),
`empire_w` Reach (Mace lord_1_7), `empire_s` Targaryen (Daenerys lord_1_14), `sturgia` Zelazne Wyspy (Balon lord_2_1), `aserai` Dorne
(Doran lord_3_1), `khuzait` Dothrakowie (Pono lord_6_1), `nightswatch`, `freefolk`, `dragonstone` (Stannis), `riverlands` (Edmure),
`stormlands` (Renly), `valyrian` Aegon, `nord` (NavalDLC), `skagosi`, Wolne Miasta: `bravos`, `pentos`, `norvos`, `qohor`, `myr`, `lys`,
`tyrosh`, `volantis`, oraz `qarth`, `sarnor`, `ibb`, `summer`, `yiti`.

Postacie: Robb lord_5_1, Catelyn ROTlord_513, Sansa lord_5_13_1, Arya lord_5_1_1, Bran lord_5_7, Rickon ROTlord_514, Jon Snow lord_5_17,
Greatjon lord_5_16, Smalljon lord_5_20, Rickard Karstark lord_2_18, Alys Karstark lord_2_18_1, Harald Karstark lord_2_23, Lyanna Mormont lord_B8_c,
Donella Hornwood lord_7_5_2, Halys Hornwood lord_7_6, Tywin lord_4_1, Cersei lord_4_2, Jaime ROTwanderer1, Tyrion ROTwanderer8, Kevan lord_4_13,
Lancel lord_4_14, Gregor lord_4_21, Amory Lorch lord_4_25, Polliver lord_4_25_1, Myrcella lord_4_10, Tommen lord_4_7, Joffrey lord_7_1,
Stannis lord_1_11, Melisandre lord_1_12, Shireen lord_1_36, Selyse ROTlord_505, Davos lord_WE9_l, Renly lord_1_9, Cortnay Penrose lord_1_25,
Mace lord_1_7, Margaery lord_1_8, Loras lord_1_44, Willas lord_1_24, Garlan lord_1_75, Randyll Tarly lord_4_3, Dickon lord_4_11,
Doran lord_3_1, Oberyn lord_3_13, Obara lord_3_10, Trystane lord_3_13_2, Areo Hotah lord_3_7, Gerold Dayne lord_2_21, Arys Oakheart ROTlord_278,
Lysa lord_4_6, Robin Arryn lord_4_121, Yohn Royce lord_1_1, Walder Frey lord_1_5, Lothar lord_1_6, Black Walder lord_1_43, Emmon Frey lord_1_1_9,
Edmure lord_1_3, Blackfish lord_1_50, Tytos Blackwood lord_NE8_l, Roose lord_1_17, Ramsay lord_1_29, Balon lord_2_1, Euron lord_2_13,
Victarion lord_2_7, Asha lord_2_10, Yara lord_2_7_1, Jeor lord_5_3, Alliser ROTlord_568, Edd lord_5_21, Qhorin lord_7_13_2, Mance lord_2_3,
Tormund lord_2_19, Ygritte lord_2_15, Styr lord_2_17, Lord of Bones lord_2_22, Daenerys lord_1_14, Missandei lord_1_47_1, Jorah lord_7_1_3,
Barristan lord_S9_l, Daario lord_6_4, Pono lord_6_1, Yezzan ROTlord_486, Egon Emeros (Qarth) ROTlord_619, Aegon ROTlord_369,
Jon Connington ROTlord_373, Arianne ROTlord_372, Jeyne Westerling ROTlord_177; postacie z szablonow (CanonCompanions): Varys ROTwanderer2,
Petyr ROTwanderer27, Sandor ROTwanderer3, Brienne ROTwanderer9, Theon ROTwanderer20, Sam ROTwanderer5, Bronn ROTwanderer4,
Podrick ROTwanderer6, Qyburn ROTwanderer12, Shagga ROTwanderer19, Hizdahr ROTwanderer22, Quentyn ROTwanderer26, Yoren ROTwanderer28,
Osha ROTwanderer18, Wun Wun ROTwanderer33.

Rody: Stark clan_battania_1, Straz (Mormont) clan_battania_2, Umber clan_battania_5, Snow clan_battania_6 (Straz), Mormont z Wyspy Niedzwiedziej
clan_battania_8, Karstark clan_sturgia_6 (Polnoc), Bolton clan_empire_south_3 (Polnoc), Lannister clan_vlandia_1, Tarly clan_vlandia_2 (Reach),
Arryn clan_vlandia_3 (Dolina), Clegane clan_vlandia_5, korona (Joffrey) clan_nord_1, Jorah clan_nord_2, Tollett (Straz) clan_nord_3,
Hornwood clan_nord_6, Tyrell clan_empire_west_1, Renly clan_empire_west_2, Stannis clan_empire_west_3, Seaworth clan_empire_west_9,
Royce clan_empire_north_1, Tully clan_empire_north_2, Frey clan_empire_north_3, Blackfish clan_empire_north_5, Blackwood clan_empire_north_8,
Targaryen clan_empire_south_1, Martell clan_aserai_1, Greyjoy clan_sturgia_1, Rayder clan_sturgia_2, Thennowie (Styr) clan_sturgia_5,
Tormund clan_sturgia_7, Selmy clan_sturgia_9, Drudzy Synowie clan_khuzait_2, Aegon ROTclan_84, Connington ROTclan_85, Westerling ROTclan_45,
Norrey ROTclan_88, Flint ROTclan_30; frakcje: brotherhood, faith_militant, brave_companions, sons_of_the_harpy, stone_crows, looters (Broken Men), sea_raiders.

Osady: Krolewska Przystan town_EW4, Smocza Skala town_EW1, Winterfell town_B2, Bialy Port town_B3, Czarny Zamek town_B5, Fosa Cailin castle_B1,
Deepwood Motte ROT_town2, Hornwood castle_K9, Piesc castle_S6 (Tormund), Twardy Dom town_S7, Riverrun town_V2, Blizniaki ROT_town3, Turnia castle_EN6,
Raventree castle_EN7, Saltpans town_EN3, Gorzki Most castle_EW1, Ashemark ROT_castle40, Casterly Rock castle_V5, Wysogrod town_V5,
Stare Miasto town_V6, Sloneczna Wlocznia town_A1, Astapor town_A2, Yunkai town_A3, Meereen town_K1, Pentos town_EN4, Braavos town_EN5,
Krwawa Brama castle_EN2. Pelne listy: `kronB/linie.txt`, `kronB/heroes.json` (scratchpad 7aee5f12), XML ROT.

## 20. Przyklad (z arm_kronika_k00.tsv, skrocony)

```
#okno	id	war	od	do	po	przed	warunki	gracz	forma	inni	wersja	temat	lektor	tytul	gora	dol	zmienne	wybor	skutki	powtorz
000.1		0				rotday:1	!p:army	okno	wiesc	obie	intryga	0	{=ARM_KR_000_1_T}A Raven at Dusk	{=ARM_KR_000_1_U}A tired raven ...	{=ARM_KR_000_1_L}This is a test window ...			flag:kr_test_1 ; kod:test
000.1	a					p:region:battania	!p:army	okno	wiesc	obie	polnoc	0	{=ARM_KR_000_1a_T}A Raven Over the Wolfswood	...	...			flag:kr_test_1 ; flag:kr_test_1a
000.2		0		kr:000.1+1	ev:NedEvent	plan:NedEvent-3		okno	okno	obie	polnoc	0	{=ARM_KR_000_2_T}Whispers Before the Storm	...	...		{=ARM_KR_000_2_Q}How do you answer the young lord of Winterfell?	flag:kr_test_2
#wybor	okno	opcja	etykieta	podpowiedz	dostepna	skutki	bierna
000.2	a	{=ARM_KR_000_2_Ca}Send him a purse of silver for his host	{=ARM_KR_000_2_Ha}You pay 200 denars ...		gold:@gracz:lord_5_1:200 ; relp:lord_5_1:2 ; flag:kr_test_2a
000.2	b	{=ARM_KR_000_2_Cb}Ride north and offer him your sword	{=ARM_KR_000_2_Hb}Needs a good standing ...	p:rel:lord_5_1:50	relp:lord_5_1:5 ; flag:kr_test_2b
000.2	c	{=ARM_KR_000_2_Cc}Keep your own counsel	{=ARM_KR_000_2_Hc}Nothing changes. ...		flag:kr_test_2c	1
#tekst	id	tekst
000.1_W	{=ARM_KR_000_1_W}A raven brings word from a distant rookery (Chronicle of Westeros test).
```

Wzor wydarzenia z para i wariantem (plan, nr 8): `008.1` z `skutki` = `hide:lord_5_1_1:kr:044.2 ; band:b008`, sekcja `#para` = `008.1	m22	kr`,
w arm_canon_moves m22: `ev:HarrenhalSiegeNotificationEvent|rotday:120|kr:008.1+1`; `008.2` z `po` = `done:HarrenhalSiegeEvent`.
