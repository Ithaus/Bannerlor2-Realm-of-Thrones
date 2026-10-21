## m96-koszyk - placa dla klas w biedzie: koszyk jedzenia ludu miast (PLAN 5.5) (Armoury)

**Dla Jeffa (prosto):** w miastach, w ktorych wyrobnicy maja juz pelne kiesy, czesc ich zarobku od dawna nie trafiala do nich, tylko zostawala
u kupcow w kasie miasta - a w tych samych miastach rzemieslnicy i kupcy nie mieli na chleb na jutro (Qarth, Braavos, Pentos, Rhyos, Norvos, Selhorys...).
Teraz te pieniadze ida do rzemieslnikow i kupcow tego miasta, ktorym brakuje: najpierw na jedzenie na 2 dni, potem na pelny koszyk na tydzien.
To jest ich zarobek jak kazdy inny - placa od niego podatki, a gdy maja wiecej, kupuja tez pelniejszy koszyk (nie tylko okrojony bieda).
Nic nie powstaje z niczego - kasa miasta placi najwyzej to, co i tak zatrzymala tego dnia, i najwyzej tyle, ile ma. Wylacznik: MCM Armoury ->
"Start calibration: every town class earns by its basket (18.10)" -> **Ks Folk Wage To Short Classes** (wylaczony = jak dzis); obok **Ks Folk Short Basket Days** (7).

**Problem:** PLAN 5.5 "kazdego dnia w kazdym miescie co najmniej 95% ludzi stac na jedzenie, 98% na pelny koszyk; zjedzone bez zaplaty ponizej 1%".
RAPORT-NOCNY-2026-10-20 rozdz. 5: "koszyk jedzenia ludu 88,5% (prog 95%)". Pomiar: linia "Kiesa ludu: dzien N" - "stac jedzenie W/R/P a / b / c, pelny W/R/P
... (swiat x%), miast ze stac jedzenie < 95%: n, pelny < 95%: m" i "zjedzone bez zaplaty N (x% wydatku gry"; "stac jedzenie" klasy = kiesa klasy wobec
jedzenia na jutro (TownFolk.Anchor: min(1, kiesa / (potrzeba x udzial jedzenia))), "miast < 95%" - miasto, w ktorym najbiedniejsza klasa ma < 95%.

**Dowod (liczby z kilku biegow, nowa grupa `--grupa m96koszyk`, okno = ostatnie 28 dob z kotwica KS):**

| Bieg | MK-4 doby ze stac jedzenie < 95% | najnizej | MK-5 miast < 95% sr / max | MK-6 pelny swiat | MK-7 zjedzone bez zaplaty sr / max | chleb M3 zaplacono / nie stac nikogo |
|---|---|---|---|---|---|---|
| WGRANIE 95, 160 dob z zapisu (12-25-19) | 14 z 28 | 87.3% (R) | 6.7 / 10 | 92.4% | 0.70% / 2.37% | 21.9 / 16.5 tys./d |
| fala 1 + poprawki, 160 dob (09-14-49) | 21 z 28 | 87.0% | 9.2 / 15 | 91.4% | 0.82% / 2.99% | 23.1 / 14.7 tys./d |
| WGRANIE 92, 160 dob (04-01-39) | 22 z 28 | 85.4% | 8.7 / 13 | 91.7% | 0.83% / 2.38% | 23.4 / 17.9 tys./d |
| WGRANIE 91, 160 dob (00-44-29) | 22 z 28 | 87.0% | 7.8 / 11 | 92.2% | 0.84% / 3.04% | 17.9 / 22.0 tys./d |
| nowa kampania 120 dob (08-45-54, 03-32-17) | - | 100% | 0 | 100% | 0% | 0 (kotwica KS jeszcze nie weszla) |

Baza sprzezenia (po recenzji; te same biegi, okno 28 dob; MK-11 i MK-12):

| Bieg | MK-11 Plan KS: czesc zaplacona koszyka R / P (pierwsza -> ostatnia linia okna) | MK-12 odmowa W sr (pierwsza -> ostatnia) | kappa W2 | dni koszyka W/R/P |
|---|---|---|---|---|
| 12-25-19 | Westeros 93.5/91.9 -> 92.1/90.6%, Essos 77.7/82.7 -> 82.8/87.9% | 131.8 tys./d (140.6 -> 118.9) | 0.216 | 28.6 / 11.7 / 25.4 |
| 09-14-49 | Westeros 93.8/91.9 -> 92.4/90.2%, Essos 73.0/77.6 -> 74.8/78.6% | 131.6 tys./d (136.8 -> 118.1) | 0.206 | 29.8 / 10.9 / 24.3 |
| 04-01-39 | Westeros 96.5/97.0 -> 95.8/96.0%, Essos 74.2/79.0 -> 76.3/80.9% | 134.5 tys./d (140.7 -> 127.3) | 0.229 | 28.1 / 10.9 / 23.2 |
| 00-44-29 | Westeros 94.6/94.2 -> 96.5/96.8%, Essos 75.7/78.7 -> 76.5/81.0% | 128.5 tys./d (139.9 -> 111.8) | 0.231 | 29.2 / 11.1 / 23.8 |

Trend w kazdym biegu 160 dob ten sam: do doby 109144 stac jedzenie W/R/P 100/100/100%; kotwica KS ("Koszyk stanow (ZC4)") wchodzi lambda 0.07 (109126)
-> 1.00 (109141), a od 109145 R spada do 85-95%, P do 94-99%, W zostaje 100%. Linia "Kiesa ludu" (12-25-19): wydatek R 38.8 (109100) -> 100-130 tys./d,
P 8.4 -> 75-114 tys./d, W 276 -> 182-250 tys./d. Linia f5: doba 109160 kappa W mediana 0.100 w 96 z 97 miast, R/P ponizej dolnej granicy pasma w 40/41
miastach; doba 109234 kappa W/R/P mediana 0.253/0.497/0.282, R/P ponizej pasma w 35/30 miastach, dni koszyka W/R/P 27.7/12.1/26.4 (mediana 27.2/32.6/48.8) -
R i P sa bogate w jednych miastach, a puste w innych. Placa nieprzyjeta w kasach: odmowa samego W 188 tys. (109160) -> 119 tys./d (109234: z zewnatrz
W 164 543 pelne / 51 063 przyjete, z kas W 8 449 / 2 998), maleje razem z kappa W2; razem z M6 przycieta kappa R/P bogatych miast "niezaplacone ludowi
w kasach miast" 384-536 tys./d. kiesa-ludu.csv (bieg 19.10 07-51-11, 24 doby 109076-109234): 105 miasto-dni ze stac jedzenie < 0.95, kiesa W wielka,
a R i P puste - np. Qarth W 90 700 / R 233 / P 2 064, Selhorys 73 814 / 930 / 767, Valysar 79 703 / 805 / 872; najczesciej Rhyos 12, Aquos Dhaen 11,
Norvos 7, Pentos 7, Braavos 6, Qarth, Highgarden, Tumbleton, Tolero Aeksio, Lord Hewett's Town po 5. Kasa tych miast: mediana 1.26 x zapasu kupcow,
ponad 1.5 x w 37 ze 105 miasto-dni (Pentos 2.05, Tolos 2.18, Tolero Aeksio 2.23). Polki nie sa przyczyna: linia ZC4 (109200) "jedzenie niekupione z braku
pieniedzy: miasta 4.9% budzetu jedzenia", "brak chleba i placka" tylko w 8 miastach z ziarnem.

**Przyczyna:** kotwica KS (ZC4) liczy koszyk klas wedlug stanow (R: rzemieslnicy + 40% niewolnych, P: kupcy i szlachta + 60% niewolnych), wiec potrzeba
przeszla z W na R i P (komentarz f5 w KsFolkIncome.cs: W 240 -> 146, R 48 -> 150, P 8 -> 124 tys./d), a podzial KAZDEGO zarobku miedzy klasy
(`TownFolk.SplitOf`: uslugi 80/10/10, dwor 80/20, budowy 60/40, warsztaty 70/30...) zostal na klasach BK. f5-lud tnie wtedy dochod W (kappa W 0.10-0.25),
a czesc nieprzyjeta zostaje w kasie miasta (`CreditF5` -> wolajacy `town.ChangeGold(amt - folk)`); R i P ponizej pasma dostaja pelna stawke, ale pelna
stawka ich udzialu nie pokrywa ich nowego koszyka. Zadna droga nie przenosi placy, ktorej W nie przyjal, do R/P tego samego miasta: M6 "domy najpierw"
placi R/P ponizej pasma pelna stawka, ale wolno (1/28 nadwyzki ponad 1.5 x zapasu kupcow na dobe), a w miastach z kasa ponizej 1.5 x zapasu (68 ze 105
glodnych miasto-dni) wcale; chleb M3 placi korona dopiero po 7 dobach glodu i nie pokrywa wszystkiego.

**Zmiana (nowy plik `Armoury/src/TownFolk.M96Koszyk.cs`, klucze `KsFolkWageToShortClasses` = TAK, `KsFolkShortBasketDays` = 7):**
- okno doby `Folk.M96Cut` - placa zatrzymana w kasie regula f5/f4 (czego klasy z pelna kiesa nie przyjely): `CreditF5` (zarobek z zewnatrz), `FromCofferF5`
  (przelewy gracza przez kase), `EarnDraw` (M6 przycieta kappa R/P) i w `TownLive` krok 2: robocizna, doplata i obrot P przy czynnikach f4/f5 < 1
  (pelne x k minus zaplacone); BEZ placy przebudowy zajecia wsi A7 (`CraftsRebuildPay` -> `M96Uncut`) - te monete kasa oddaje wsiom robotami miasta (TownHire);
- raz na dobe w kazdym miescie (`TownLive` po placach z kas, przed czynszem, nadwyzka i kotwica jutra) - `M96Short`: z kasy miasta najwyzej ta placa
  zatrzymana i najwyzej stan kasy do kies R i P, ktorym brakuje: najpierw jedzenie na 2 dni (2 x udzial jedzenia klasy x potrzeba dzienna z kotwicy),
  potem pelny koszyk do 7 dni; w kazdym etapie wedlug luk klas. Cel przyciety do 2-20 i ponizej dolnych granic pasm f5 (21 / 30) - klasa w biedzie ma
  kappa 1, wiec nigdy nie jest jednoczesnie dawca; placa z nadwyzki R/P do W (krok 5, od 21 / 30 dni) nie rusza z tych pieniedzy;
- przelew to zarobek okna R/P (`CreditParts(..., EPublic)` - w skladzie zarobku C8 / kiesa-obieg.csv kolumna "inne", nie "robocizna handlu"): podatek pana,
  cech, 184 i dziesiecina nastepnej doby, ZC0 i budzet Planu KS - jak M6 i kazdy inny wplyw do kies (patrz "Decyzja" nizej); ksiega kies `_ctlIn` i `LDayIn`
  (kontrola "Kiesa ludu" r = 0); bez pola zapisu (okno jednej doby); wyjatek per miasto - licznik potkniec;
- linia `Placa dla klas w biedzie (m96-koszyk): start sesji` (raz) i `...: dzien N` (gdy klucz TAK i jest miasto z kotwica KS albo z placa zatrzymana -
  w nowej kampanii przed KS tylko linia startu); w linii doby "R/P bez jedzenia na jutro po przelewie N miast: nazwy (do 6, [oblezone])";
- `tools/sprawdz_logi.py --grupa m96koszyk` (MK-0..MK-12); `tools/gen_mcm.py` - zakres suwaka 2-20; McmSettings.cs z generatora.
Wpiecia w plikach wspolnych: Settings.cs (2 pola), TownFolk.cs (1 pole), TownFolk.F5.cs (2 linie), TownFolk.Ds.cs (1), TownFolk.DsAkum.cs (1),
TownFolk.Live.cs (3 wywolania).

**Decyzja (recenzja techniczna, wazne): przelew jako zarobek okna, nie sam transfer.** Lancuch potwierdzony w kodzie: `CreditParts` -> `EarnWin[1..2]`
-> nastepnej doby `f.ZcE` (TownFolk.Live.cs, TownLive krok 1) -> `ZcEarnNet` -> `StartCalibration.Money` `_earn` (srednia 28 dob) -> `TownBudget` (x 0.90)
-> Plan krok 8 `t.Budget` -> `KsCheap.PlanLadder` -> `TownPaid` (jedna czesc zaplacona na miasto i towar, z wszystkich klas) -> Zc4 `qz = q1 x tp` dla W, R
i P -> `Zc4ClassCost` -> `f.Need[0..2]`. Biedne R/P obcinaja dzis czesc zaplacona miasta, wiec zanizaja potrzebe wszystkich klas tego miasta (Essos R/P
w Planie placa 74.8-82.8% / 78.6-87.9% koszyka). Po paczce budzet R/P biednych miast rosnie, Plan placi pelniejszy koszyk, potrzeba W/R/P tych miast rosnie
(szacunek 5-20%), luka R/P i przelew rosna, dni koszyka W spadaja (kappa W w gore, odmowa W w dol). Zostawiam tak swiadomie: (1) M6 (`EarnDraw`) i
przelewy wewnatrz ludu (`Move`) - ta sama droga kasa -> kiesy - tez sa zarobkiem okna; jedna zasada na jedno zjawisko; (2) wariant "transfer bez EarnWin"
zostawilby Plan przy R/P Essos placacych 75-83% koszyka, a "stac jedzenie / pelny" mierzylby kiesy wobec koszyka przycietego bieda - ukrywa glod zamiast
go usunac; (3) sprzezenie jest ograniczone z obu stron (czesc zaplacona <= 1, przelew <= placa zatrzymana tej doby i stan kasy) i wolne (Plan co 7 dob,
budzet ze sredniej 28 dob - pelny skutek po ok. 4 tygodniach). Pomiar: MK-11 i MK-12 (tabela bazy wyzej).

**Ryzyko / co sprawdzic (kontrola calosci):**
- regresje: klucz NIE - okno zerowane, zadnego ruchu zlota (kod co do monety jak dotad; jedyne roznice - dodawanie i odejmowanie w polu Folk). Klucz TAK,
  ale bez kotwicy KS (nowa kampania do ok. 120 dob) - placy zatrzymanej nie ma (f5/f4 wymagaja KS), regula bezczynna, linii doby nie ma;
- sprzezenie przez Plan KS (decyzja wyzej): potrzeba W/R/P biednych miast rosnie w ciagu ok. 4 tygodni, przelew razem z nia - szacunek 30-80 tys./d na
  start (pierwszy tydzien wiecej - dopelnienie do 7 dni), potem do ok. 50-110 tys./d; zrodlo w biednych miastach to prawie wylacznie odmowa W
  (swiat 128-134 tys./d sr28, maleje), bo przy kappa R/P = 1 M6 nie zostawia tam nic w kasie. Gdy dni W spadna do pasma, odmowa W maleje - wtedy MK-8
  "bez placy zatrzymanej" i MK-5 pokaza miasta bez pokrycia. Popyt Planu na targu (sprzedaz wsi, praca domowa) rosnie razem z czescia zaplacona -
  sprawdzic `nks` (NKS, "miasta kupuja >= 98%"), linie "KS: pieniadze planu" (zbieznosc punktu stalego, "bez pokrycia" Planu);
- podatki: ok. 5-8% przelewu wraca nastepnej doby jako podatek pana, cech, 184 i dziesiecina (jak od kazdego zarobku R/P); przed paczka ta placa stala
  w kasie i szla do pana i korony firma (przeglad wedlug nadwyzki) - tor zlota inny, suma zamknieta;
- kasy miast z luka R/P dostaja mniej: sprawdzic zaleglosci firm DS (`nds`, `nakum` NKA), przeglad firm wedlug nadwyzki (f5 "podwyzek dzis" - w tych miastach
  i tak wstrzymane "R/P ponizej pasma"), kasy ponizej zapasu kupcow; dosypki nie ma (regulator kas tryb 2), wiec zloto swiata bez zmian; pieniadze R/P
  wydane na jedzenie wracaja do tej samej kasy (PayForGoods);
- podwojne liczenie (recenzje, CLAUDE.md 8.0) - naprawione: placa przebudowy A7 zatrzymana w kasie byla w oknie m96 i jednoczesnie w zwrocie TownHire
  (VillageCrafts.PayRebuild -> `TownHire.NoteRebuild(mt, r, matIn + wageCof)`); teraz `M96Uncut` zdejmuje ja z okna (licznik "zdjete z okna - placa
  przebudowy wsi A7" w linii doby; skala w 12-25-19: "do kas 116 / 15 / 139" zl/d razem z materialem). Roboty gminy DS (DaPayVillage) zostaja w oknie -
  dla nich zwrotu TownHire nie ma; CraftsRebuildPay wola tylko VillageCrafts.PayRebuild, ktory zawsze zapisuje NoteRebuild; przy wylaczonych robotach
  miast TownHire ta placa zostaje w kasie - jak przed paczka m96 (mniej przelewu, nic z niczego);
- linia f5 "oddane kasom miast" / "zostalo w kasach" liczy dalej PRZED przelewem m96 - kasy zatrzymuja o "przelane" mniej (wiersz m96 to pokazuje);
  `nakum` NAKUM-18 (R/P bez ujscia) - R/P z przelewu stoja ponizej pasma, wiec "bez ujscia" sie nie zmienia;
- chleb M3 i glod E5/C2: luki R/P mniejsze -> korona placi mniej, osady schodza z chleba po 7 kotwicach (spodziewane);
- miary: MK-4 to srednia swiata kazdej klasy, MK-5 - miasta (prog projektu 0); MK-5 daje TAK tylko przy 0 miast, przy sr <= 1 i max <= 3 - DECYZJA
  z nazwami miast z linii m96 (oblezone - chleb M3);
- cudzy kod: nic nie latane; Town.ChangeGold jak robocizna (`FromCoffer`), ksiega MoneyLedger przez migawki bloku doby ludu (jak robocizna);
  `Settlement.IsUnderSiege` tylko odczyt;
- watki: zapis okna tylko w `Earn` / `EarnFromTown` / `EarnDraw` (bramka `OffMain`) i `CraftsRebuildPay` (watek glowny), przelew w bloku doby (watek
  glowny); bez alokacji w Earn; nazwy miast do linii - najwyzej 6 dopisan do jednego StringBuilder na dobe.

**Poprawki po recenzjach (gra, technika):**
- wazne (technika) - sprzezenie przez Plan KS: PRAWDZIWE (lancuch w kodzie wyzej); decyzja (a) - zostaje zarobkiem okna, opisane w "Decyzja" i "Ryzyko",
  pomiar MK-11 / MK-12 z baza z 4 biegow, oczekiwanie MK-8 poprawione;
- drobne (obie) - podwojne liczenie placy przebudowy A7 z TownHire: PRAWDZIWE, naprawione (`M96Uncut`, 1 linia w TownFolk.DsAkum.cs);
- drobne (gra) - rodzaj zarobku "robocizna handlu": naprawione (EPublic - kolumna "inne");
- drobne (gra) - linia doby bez KS w nowej kampanii: PRAWDZIWE, naprawione (miasto liczone tylko z kotwica KS albo z placa zatrzymana > 0 - przy cut > 0
  droga bez zmian);
- drobne (gra) - liczby i opis M6 w docstringu i wpisie: poprawione (M6 dziala, ale wolno; 37 ze 105 miasto-dni ponad 1.5 x, mediana 1.26 x; odmowa W
  osobno od M6; chleb M3 i zjedzone bez zaplaty z tabeli);
- drobne (gra) - MK-5 TAK przy 3 miastach: naprawione (TAK tylko przy 0; sr <= 1 i max <= 3 - DECYZJA z nazwami z linii m96);
- drobne (technika) - MK-1 NIE przy kluczu NIE: naprawione (klucz z linii startu; NIE - INFO).

**Status:** DO SPRAWDZENIA (zbudowane: kod 0 wszystkie 5 modow; nic nie wgrane; gra nie uruchamiana).

**Test w grze (autotest nocny, przed WGRANIEM 96):** 160 dob z zapisu autotest-F-d240 x8 (kotwica KS wchodzi ok. doby 50-65 biegu) i nowa kampania
120 dob. `python -I tools/sprawdz_logi.py --grupa m96koszyk <Armoury-*.log>` - oczekiwane w biegu 160 dob: MK-0 TAK (klucz TAK, cel 7 dni), MK-1/2/3 TAK,
MK-4 stac jedzenie W/R/P >= 95% kazdej doby okna (baza: 14-22 dob z 28 ponizej, najnizej 85-87%), MK-5 TAK (0 miast) albo DECYZJA (sr <= 1, max <= 3, nazwy
w wierszu; baza 6.7-9.2 / 10-15), MK-6 pelny swiat >= 98% (baza 91.4-92.4% - najmniej pewny prog: zalezy, czy odmowa W starczy na pelny koszyk w najwiekszych
miastach Essos przy rosnacej potrzebie), MK-7 zjedzone bez zaplaty < 1% sr i max (baza 0.70-0.84% / 2.4-3.0%), MK-8 przelane 30-80 tys./d (z sprzezeniem do
ok. 50-110 tys./d), placa zatrzymana razem z M6 380-560 tys./d, bez pokrycia ok. 0-2 miast, R/P bez jedzenia po przelewie ok. 0-2 miast, MK-9 chleb M3
w dol (baza zaplacono 17.9-23.4 tys./d, nie stac nikogo 14.7-22.0 tys./d), MK-10 <= 2 ms, MK-11 czesc zaplacona R/P Planu w gore (baza ostatniej linii
Westeros R 92.1-96.5% / P 90.2-96.8%, Essos R 74.8-82.8% / P 78.6-87.9%), MK-12 dni R/P w gore (baza 10.9-11.7 / 23.2-25.4), dni W i odmowa W moga spasc
(baza 28.1-29.8 dni, 128-134 tys./d). W nowej kampanii 120 dob: tylko linia startu (linii doby nie ma - bez KS), MK-1..12 INFO. Dodatkowo bez regresji:
`nks`, `nds`, `nakum`, `nkoszyk`, `nglod`, `nzapis`, `nperf`. Grep: `grep "Placa dla klas w biedzie (m96-koszyk)"`, `grep -o "stac jedzenie W/R/P [^|]*" Armoury-*.log`,
`grep -o "miasta: W [^|]*" Armoury-*.log` (linia "KS: pieniadze planu").
