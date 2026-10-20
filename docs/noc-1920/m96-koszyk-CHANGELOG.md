## m96-koszyk - placa dla klas w biedzie: koszyk jedzenia ludu miast (PLAN 5.5) (Armoury)

**Dla Jeffa (prosto):** w miastach, w ktorych wyrobnicy maja juz pelne kiesy, czesc ich zarobku od dawna nie trafiala do nich, tylko zostawala
u kupcow w kasie miasta - a w tych samych miastach rzemieslnicy i kupcy nie mieli na chleb na jutro (Qarth, Braavos, Pentos, Rhyos, Norvos, Selhorys...).
Teraz te pieniadze ida do rzemieslnikow i kupcow tego miasta, ktorym brakuje: najpierw na jedzenie na 2 dni, potem na pelny koszyk na tydzien.
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

Trend w kazdym biegu 160 dob ten sam: do doby 109144 stac jedzenie W/R/P 100/100/100%; kotwica KS ("Koszyk stanow (ZC4)") wchodzi lambda 0.07 (109126)
-> 1.00 (109141), a od 109145 R spada do 87-95%, P do 94-99%, W zostaje 100%. Linia "Kiesa ludu" (12-25-19): wydatek R 38.8 (109100) -> 100-130 tys./d,
P 8.4 -> 75-114 tys./d, W 276 -> 182-250 tys./d; zapas dni W 42 -> 93 (109140) -> 33 (109230), R 30.6 -> 9.5-12.7, P 105 -> 11-27. Linia f5: doba 109160
kappa W mediana 0.100 w 96 z 97 miast, R/P ponizej dolnej granicy pasma w 40/41 miastach, "razem niezaplacone ludowi: w kasach miast 384 324" zl/d
(z zewnatrz 201 tys., z kas 48 tys., M6 136 tys.); doba 109234 kappa W/R/P mediana 0.253/0.497/0.282 (z kappa < 1: 95/62/67 miast), R/P ponizej pasma
w 35/30 miastach, niezaplacone w kasach 535 623 zl/d - R i P sa bogate w jednych miastach (mediana dni P 48.8), a puste w innych. kiesa-ludu.csv (bieg 19.10 07-51-11, doby 109150-109234): w kazdym z 102 miasto-dni ze stac jedzenie < 0.95 kiesa W jest wielka,
a R i P puste - np. Qarth W 90 700 / R 233 / P 2 064, Selhorys 73 814 / 930 / 767, Valysar 79 703 / 805 / 872, Rhyos 57 185 / 1 394 / 898, Aquos Dhaen
28 310 / 642 / 567; kasy tych miast przy 0.94-1.1 x zapasu kupcow (M6 placi dopiero od 1.5 x). Glodne miasta to glownie Essos (Rhyos, Braavos, Pentos,
Mhysa Faer, Norvos, Qarth, Lorath, Tyrosh, Volantis) oraz Oldtown, Highgarden, Arbor, Tumbleton, King's Landing. Polki nie sa przyczyna: linia ZC4
(109200) "jedzenie niekupione z braku pieniedzy: miasta 4.9% budzetu jedzenia", "brak chleba i placka" tylko w 8 miastach z ziarnem.

**Przyczyna:** kotwica KS (ZC4) liczy koszyk klas wedlug stanow (R: rzemieslnicy + 40% niewolnych, P: kupcy i szlachta + 60% niewolnych), wiec potrzeba
przeszla z W na R i P (komentarz f5 w KsFolkIncome.cs: W 240 -> 146, R 48 -> 150, P 8 -> 124 tys./d), a podzial KAZDEGO zarobku miedzy klasy
(`TownFolk.SplitOf`: uslugi 80/10/10, dwor 80/20, budowy 60/40, warsztaty 70/30...) zostal na klasach BK. f5-lud tnie wtedy dochod W (W ma 60-90 dni
koszyka, kappa 0.10), a czesc nieprzyjeta zostaje w kasie miasta (`CreditF5` -> wolajacy `town.ChangeGold(amt - folk)`); R i P ponizej pasma dostaja
pelna stawke, ale pelna stawka ich udzialu nie pokrywa ich nowego koszyka. Zadna droga nie przenosi placy, ktorej W nie przyjal, do R/P tego samego
miasta (M6 "domy najpierw" dziala dopiero od kasy 1.5 x zapasu; chleb M3 placi korona dopiero po 7 dobach glodu i nie pokrywa wszystkiego).

**Zmiana (nowy plik `Armoury/src/TownFolk.M96Koszyk.cs`, klucze `KsFolkWageToShortClasses` = TAK, `KsFolkShortBasketDays` = 7):**
- okno doby `Folk.M96Cut` - placa zatrzymana w kasie regula f5/f4 (czego klasy z pelna kiesa nie przyjely): `CreditF5` (zarobek z zewnatrz), `FromCofferF5`
  (przelewy gracza przez kase), `EarnDraw` (M6 przycieta kappa R/P) i w `TownLive` krok 2: robocizna, doplata i obrot P przy czynnikach f4/f5 < 1
  (pelne x k minus zaplacone);
- raz na dobe w kazdym miescie (`TownLive` po placach z kas, przed czynszem, nadwyzka i kotwica jutra) - `M96Short`: z kasy miasta najwyzej ta placa
  zatrzymana i najwyzej stan kasy do kies R i P, ktorym brakuje: najpierw jedzenie na 2 dni (2 x udzial jedzenia klasy x potrzeba dzienna z kotwicy),
  potem pelny koszyk do 7 dni; w kazdym etapie wedlug luk klas. Cel przyciety do 2-20 i ponizej dolnych granic pasm f5 (21 / 30) - klasa w biedzie ma
  kappa 1, wiec nigdy nie jest jednoczesnie dawca; placa z nadwyzki R/P do W (krok 5, od 21 / 30 dni) nie rusza z tych pieniedzy;
- zarobek okna R/P jak robocizna (`CreditParts(..., ELabour)` - podatek pana, cech, 184, dziesiecina nastepnej doby), ksiega kies `_ctlIn` i `LDayIn`
  (kontrola "Kiesa ludu" r = 0); bez pola zapisu (okno jednej doby); wyjatek per miasto - licznik potkniec;
- linia `Placa dla klas w biedzie (m96-koszyk): start sesji` (raz) i `...: dzien N` (gdy klucz TAK i sa miasta z kotwica);
- `tools/sprawdz_logi.py --grupa m96koszyk` (MK-0..MK-10); `tools/gen_mcm.py` - zakres suwaka 2-20; McmSettings.cs z generatora.
Wpiecia w plikach wspolnych: Settings.cs (2 pola), TownFolk.cs (1 pole), TownFolk.F5.cs (2 linie), TownFolk.Ds.cs (1), TownFolk.Live.cs (3 wywolania).

**Ryzyko / co sprawdzic (kontrola calosci):**
- regresje: klucz NIE - okno zerowane, zadnego ruchu zlota (kod co do monety jak dotad; jedyna roznica - dodawanie do pola Folk). Klucz TAK, ale bez
  kotwicy KS (nowa kampania do ok. 120 dob) - placy zatrzymanej prawie nie ma i luk R/P nie ma (stac 100%) - regula bezczynna;
- kasy miast z luka R/P dostaja mniej (szacunek: sredniej doby przelane 30-80 tys./d na swiat, pierwszy tydzien po wejsciu KS wiecej - dopelnienie do
  7 dni): sprawdzic zaleglosci firm DS (`nds`, `nakum` NKA), przeglad firm wedlug nadwyzki (f5 line "podwyzek dzis"), "miasta kupuja >= 98% plonu"
  (`nks`), kasy ponizej zapasu kupcow; dosypki nie ma (regulator kas tryb 2 - nic nie dosypuje), wiec zloto swiata bez zmian;
- linia f5 "oddane kasom miast" / "zostalo w kasach" liczy dalej PRZED przelewem m96 - kasy zatrzymuja o "przelane" mniej (wiersz m96 to pokazuje);
  `nakum` NAKUM-18 (R/P bez ujscia) - R/P z przelewu stoja ponizej pasma, wiec "bez ujscia" sie nie zmienia;
- chleb M3 i glod E5/C2: luki R/P mniejsze -> korona placi mniej, osady schodza z chleba po 7 kotwicach (spodziewane);
- rozklad: miasto bez placy zatrzymanej (W ponizej pasma, kappa 1) - nic; licznik "bez pokrycia" w linii (spodziewane ok. 0-2 miast), tam zostaje chleb M3;
- cudzy kod: nic nie latane; Town.ChangeGold jak robocizna (`FromCoffer`), ksiega MoneyLedger przez migawki bloku doby ludu (jak robocizna);
- watki: zapis okna tylko w `Earn` / `EarnFromTown` / `EarnDraw` (bramka `OffMain`), przelew w bloku doby (watek glowny); bez alokacji w Earn; raz na dobe.

**Status:** DO SPRAWDZENIA (zbudowane: kod 0 wszystkie 5 modow; nic nie wgrane; gra nie uruchamiana).

**Test w grze (autotest nocny, przed WGRANIEM 96):** 160 dob z zapisu autotest-F-d240 x8 (kotwica KS wchodzi ok. doby 50-65 biegu) i nowa kampania
120 dob. `python -I tools/sprawdz_logi.py --grupa m96koszyk <Armoury-*.log>` - oczekiwane w biegu 160 dob: MK-0 TAK (klucz TAK, cel 7 dni), MK-1/2/3 TAK,
MK-4 stac jedzenie W/R/P >= 95% kazdej doby okna (baza: 14-22 dob z 28 ponizej, najnizej 85-87%), MK-5 miast < 95% srednio <= 1, max <= 3 (baza 6.7-9.2 /
10-15), MK-6 pelny swiat >= 98% (baza 91.4-92.4% - najmniej pewny prog: zalezy, czy placa zatrzymana starczy na pelny koszyk w najwiekszych miastach
Essos), MK-7 zjedzone bez zaplaty < 1% sr i max (baza 0.70-0.84% / 2.4-3.0%), MK-8 przelane << placa zatrzymana (ok. 380-560 tys./d), bez pokrycia ok. 0-2
miast, MK-9 chleb M3 w dol (baza zaplacono 17.9-23.4 tys./d, nie stac nikogo 14.7-22.0 tys./d), MK-10 <= 2 ms. W nowej kampanii 120 dob: MK-1..7 INFO
(bez kotwicy KS), linia startu jest. Dodatkowo bez regresji: `nks`, `nds`, `nakum`, `nkoszyk`, `nglod`, `nzapis`, `nperf`. Grep:
`grep "Placa dla klas w biedzie (m96-koszyk)"`, `grep -o "stac jedzenie W/R/P [^|]*" Armoury-*.log`.
