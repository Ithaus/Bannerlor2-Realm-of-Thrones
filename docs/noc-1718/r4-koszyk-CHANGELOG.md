## 2026-10-17/18 noc (KS runda 4 - r4-koszyk: tanszy koszyk kazdego domu bez pieniedzy (P9) + place rosna, gdzie brakuje rak)

**Mod:** Armoury + `tools/sprawdz_logi.py` + `tools/gen_mcm.py` (+ uprzaz `tools/ekonomia-symulatory-2026-10-17/kmoney/h4-koszyk/`) | **Galaz:** `w-toku/r4-koszyk` (na efbbf0dc = `w-toku/noc-ks4` "KS runda 4 cz. 5") | **Tor:** L (uprzaz Planu na zrzucie + budzetach fin80, bez gry) | **Klucze (nowe, grupa MCM "Start calibration: cheaper basket and wages by hands (r4-koszyk)"):** `KsCheapBasket` (tak), `KsWageByHands` (tak), `KsWageHandsStepPercent` 3, `KsWageHandsMax` 1.30 - oba dzialaja TYLKO przy zastosowanej kalibracji startu (KS); wylaczone = co do liczby jak dotad | **Zapis:** nowy klucz `arm_ks_koszyk` ("k1|miasto:wskaznik:doba decyzji,...|miasto:z=v/...") - wskazniki plac i dokupiona podstawa koszyka miast z Planu; brak klucza (stary zapis, Ironman Jeffa) = wskazniki 1 i bez dokupionej podstawy do pierwszego Planu; starsza DLL klucz pomija. `arm_ks` i wpisy "@miasto" (czesc zaplacona 0..1) - bez zmian.

**Dla Jeffa (prosto):**
- Biedni w miastach (i kazdy dom, ktorego nie stac na caly koszyk - wszedzie, nie tylko w Essos; bez zadnego prawa dla jednej krainy) najpierw kupuja tyle najtanszego jedzenia, zeby sie najesc: ziarno na kasze, chleb, placek albo ryby - to, co na ich targu daje najwiecej kalorii za grosz i lezy na straganie. Potem sol, swoj zwykly chleb, bialko (najtansze najpierw), napoj (najtanszy najpierw), a dopiero z reszty mieso, wino, ser i owoce.
- Plan i targ widza koszyk naprawde kupiony: wiecej zboza, mniej wina i miesa. Za tym ida ceny i pola (wiecej zboza, mniej winnic).
- Wynik na liczbach z ostatniego testu (fin80, Plan liczony poza gra): rzemieslnicy Essos najadaja sie w 99.4% (dotad 85%), kupcy i szlachta Essos w 99.3% (dotad 80%), w Westeros 100% (dotad 97%). Pieniedzy nie przybywa ani nie ubywa - te same kiesy kupuja inny towar.
- Place: tam, gdzie pracodawcom brakuje rak (wojna i sluzba zabraly wiecej mezczyzn niz wolny zapas regionu albo przetwory miasta pracuja na pelnych rekach), dniowka miasta rosnie o 3% na tydzien, najwyzej o 30%. Gdzie wolnych rak jest duzo, wraca do zwyklej. Doplaca kasa miasta (kupcy i mistrzowie, ktorzy zatrudniaja), nic nie powstaje z niczego. W tescie 120 dob bedzie tego malo (ponad progiem 3 regiony z 227), wiecej w dlugiej wojnie i w zniwa.
- Glod z braku jedzenia (bunt) robi osobna paczka (r4-glodbunt) - tu go nie ma.

**Problem:**
- P9 (PLAN-KS-RUNDA4 rozdz. 4): klasy miast R/P w Essos placa 41-54% koszyka (Westeros 88-95%) i Plan liczy je jako "na pol glodne", choc biedak kupilby tansze kalorie. Dowod - linia "KS: pieniadze planu" fin80 (`Armoury-2026-10-17_16-26-10.log`, doba 109193): Essos R 68.6% (budzet 69741 / koszyk 81025), P 61.3% (64540 / 78433); Westeros R/P 92.2%. NKS-3c (wartosc koszyka R/P >= 95%) NIE na kazdym logu rundy 4.
- W grze (linia ZC4 fin80): drabina (kiesy nie stac na cale jedzenie) w 26 z 97 miast, jedzenie niekupione z braku pieniedzy 1.5% budzetu jedzenia (6158 zl).
- Decyzje Jeffa 17.10: "Tanszy koszyk" (biedni jedza tansze: chleb, kasza, ryby zamiast miesa i wina; koszyk dopasowuje sie do kieszeni, place rosna same tam, gdzie brakuje rak); noc: "Essos - nie robmy sztucznego prawa, jak gloduja to znaczy ze musza jesc tansze jedzenie ... to powinna ustawiac ekonomia podazy i popytu", "Poprawka 2 - tak".

**Przyczyna:**
- Plan (`KsBasket.Ladder`, `StartCalibration.Deliver.cs`): szczebel po szczeblu w calosci, szczebel bez pieniedzy po rowno, dalsze 0 - zadnej zamiany kalorii miedzy szczeblami (kto nie ma na mieso, nie kupi wiecej zboza), napoj przed bialkiem.
- Gra (`Zc4Ladder`, `StartCalibration.Zc4.cs`): przetrwanie 0.47 kg ziarna, sol, napoj, bialko, reszta zboza dopiero na koncu razem z owocami - tez bez zamiany kcal miedzy kategoriami.
- Place: dniowka miasta = stawka historyczna x poziom plac z dobrobytu (`TownWage`); nic nie patrzy na brak rak.
- Harness P9 (podzial niezaplaconego R/P na kcal jedzenia i rzeczy) nie istnial - NKS-3c mierzyl wartosc calego koszyka.

**Zmiana:**
- `KsCheap.cs` (nowy, czysty C# - takze w uprzezy Planu): `KsCheap.Core` - drabina tanszego koszyka:
  1. sol;
  2. KALORIE: kcal calego jedzenia koszyka (z napojami i z pozycjami wycietymi regula zasiegu) z podstawy najtanszej za kcal (ziarno, chleb, placek, ryby; w grze najwyzej tyle, ile lezy na polce, potem nastepna; czego nie ma - brak pokrywaja dalsze szczeble); budzet za maly na kalorie - tyle podstawy, ile starczy (glod prawdziwy);
  3. pozycje tansze za kcal niz podstawa, ktora zastepuja - od razu;
  4. zboze w formach koszyka (zwykly chleb i placek) do potrzeby;
  5. bialko najtansze za kcal najpierw, 6. napoj najtanszy za litr najpierw, 7. reszta jedzenia po rowno - kazda pozycja zastepuje kcal podstawy (podstawa maleje o jej kcal, najpierw najdrozsza);
  8. przedza, 9. rzeczy - jak dotad szczebel po rowno.
  Stac na caly koszyk - caly koszyk (jak dotad). Czesc ponad 1 tylko na podstawie (dokupione kalorie). Czesciowe podniesienie - szukanie polowkowe z kosztem nie wiekszym niz reszta budzetu (nigdy ponad budzet).
- `KsCheap.PlanLadder` (krok 8 Planu, klasy miast W/R/P; `StartCalibration.Core.cs` MoneyStep): przy kluczu wylaczonym ta sama `KsBasket.Ladder` co dotad (co do liczby) + podzial P9; przy kluczu - drabina tanszego koszyka; straznik: stara drabina daje wiecej kcal - zostaje stara (licznik w linii). Czesc zaplacona zostaje 0..1 (`R.TownPaid`, zapis "@" bez zmian); podstawa ponad koszyk:
  - do popytu Planu (`KsCheap.ExtraToNeed`: zboze -> NG_, ryby -> NFish; w kazdej rundzie punktu stalego, jak `UnpaidToNeed`);
  - do gry jako `R.TownExtra` (czesc koszyka ponad 1) -> `_townExtra` (`TownPaidFromPlan`, ta sama bramka pomiaru) -> `Zc4Build`: potrzeba miasta Q (popyt ceny, slownik budzetu, wsad ZC5, KS-H) i koszt klas QC x (czesc zaplacona + podstawa ponad koszyk).
  Wsie: brak pieniedzy pokrywa juz praca domowa z towarem zastepczym 8b/8c (najwiecej kcal z reki na wlasnej ziemi) - ta sama zasada, druga regula dla wsi nie dodana.
- `KsCheapBasket.cs` (nowy): gra - `Zc4Ladder` przy kluczu wola `Zc4LadderCheap` (kategorie jedzenia slownika: najtanszy za kg towar kategorii z polka, kcal = zloto kategorii x kcal na zl, podstawa najwyzej polka; zadna podstawa nie lezy na polce - stara drabina). Liczniki ZC4 (`_zLadderN`, `_zLadCut`, czas drabiny) - te same znaczenia. Linia "Tanszy koszyk (r4-koszyk): dzien N" co dobe (po linii ZC4): miasto-doby drabiny wedlug rynku, kcal kupione / potrzebne, budzet = wydane + niewydane, wydane wedlug grup, podstawa ponad koszyk, niekupione wedlug grup, dokupiona podstawa Planu w popycie miast. Linia Planu "KS: tanszy koszyk miast (P9, r4-koszyk) plan doba N" (po linii pieniedzy Planu, takze przy kluczu wylaczonym): klasy W/R/P - kcal jedzenia i rzeczy zaplacone, sklad kcal kupione wobec koszyka (zboze/bialko/napoje/reszta), stara drabina lepsza, dokupiona podstawa (szt. gry/d).
- `KsWageHands.cs` (nowy): wskaznik plac miasta z KS (mapa ZC4), decyzja co 7 dob (od pierwszej doby miasta):
  - W GORE o `KsWageHandsStepPercent` (do `KsWageHandsMax`), gdy brak rak: region miasta FreeHands a >= f (sluzba, polegli, niewola, Mur >= prog wolnych rak krainy; ta sama definicja, z ktorej Plan bierze rece wsi; w oknie zniw prog nizszy) albo przetwory ZC5 zajete >= 90% rak przez tydzien;
  - W DOL o ten sam krok (najwyzej do 1.0), gdy rak duzo: f - a >= f/2 i ZC5 < 50%; inaczej bez zmiany.
  Placi pracodawca: `TownFolk.TownLive` krok 2 - robocizna handlu (W90/R10) i doplata do wartosci wyrobow (W70/R30) z kasy miasta x wskaznik, to samo przyciecie do stanu kasy (FromCoffer: zloto schodzi z kasy i trafia do kies). Przetwory ZC5 licza w koszcie pracy ta sama place (`StartCalibration.Crafts.cs`); licznik N4 (stara regula doplaty) x ten sam wskaznik. Warsztaty gry, rzemioslo 148, naprawy, budowy - bez zmian (inne sciezki, poza KS). Linia "Place za brak rak (r4-koszyk): dzien N" co dobe (tylko gdy sa miasta z KS): decyzja tygodnia (brak rak: wolne rece / ZC5, duzo wolnych rak, bez zmiany), wskaznik min/mediana/max i miasta > 1 z nazwami, TOZSAMOSC doby: kasy miast zaplacily robocizne i doplate X zl = kiesy ludu przyjely X zl (roznica), z tego ponad stawke.
- `Settings.cs` / `McmSettings.cs` (gen_mcm) / `tools/gen_mcm.py` (zakresy krok 0..10%, sufit 1..2) - 4 klucze w nowej grupie. `StartCalibration.Plan.cs`: `inp.CheapBasket = SwCheapBasket`. `StartCalibration.cs` Reset: `CheapReset`, `KsWageHands.Reset`. `ArmouryBehavior.cs` SyncData: `arm_ks_koszyk` (wlasny try, po `arm_ks_r3`; straznik rozmiaru: 600 tys. znakow, 4000 miast, wskaznik 1..2, czesc ponad koszyk 0..50).
- `tools/sprawdz_logi.py`:
  - NKS-3c (grupa nks) wedlug P9: klasy R i P - kcal JEDZENIA >= 95% z linii P9 (ostatni Plan <= doba NKS-3c), rzeczy INFO; log bez linii P9 - dawna regula (wartosc koszyka). Na fin78 i fin80 grupa nks bez zmian (18 NIE + 6 ETAP i 17 NIE + 5 ETAP z 53 - jak w STAN-PRAC ### 80 i ### 82), nds na fin78 41 progow bez wyjatku.
  - nowa grupa `nkoszyk` (NKO-0..14): linie paczki; NKO-1 (P9) R/P kcal >= 95% na obu rynkach; NKO-2 sklad kcal R/P Essos/Westeros wobec koszyka + dokupiona podstawa (INFO); NKO-3 rzeczy (INFO); NKO-4 straznik "stara drabina lepsza" (INFO); NKO-5 drabina w grze kcal >= 95%; NKO-6 tozsamosc drabiny (budzet = wydane + niewydane, wydane <= budzet); NKO-7 podstawa Planu w popycie (INFO); NKO-8/13 potkniecia 0; NKO-9 place zaplacone = przyjete kazdej doby; NKO-10 wskaznik w [1, sufit]; NKO-11 krok miasta <= krok tygodnia; NKO-12 wskaznik wedlug miast i udzial plac ponad stawke (INFO); NKO-14 ERROR paczki 0. Na `Armoury-2026-10-17_15-37-31.log` (i fin80): bez wyjatku, linie paczki - INFO "BRAK". Proba linii z formatem kodu: wszystkie wyrazenia lapia.
- Uprzaz `tools/ekonomia-symulatory-2026-10-17/kmoney/h4-koszyk/` (kopia kmoney/h4 + `KS_CHEAP=1`, P9 klas, dokupiona podstawa w tozsamosci targu).

**Uprzaz Planu (zrzut ks_dump.json + budzety klas miast i alfa z gry fin80 `Logs/2026-10-17_16-26-10/ks-budzety.csv`, KS_ITEMS1/KS_ALPHA/KS_GOLD/KS_WORLD jak h4, 3 Plany):**

| | stara drabina (klucz NIE) | tanszy koszyk (klucz TAK) |
|---|---|---|
| Essos R: kcal jedzenia / wartosc koszyka | 85.1% / 69.1% | **99.4%** / 68.6% |
| Essos P: kcal / wartosc | 79.6% / 61.5% | **99.3%** / 61.1% |
| Westeros R / P kcal | 96.9% / 96.9% | **100% / 100%** |
| sklad kcal Essos R kupione (zboze/bialko/napoje/reszta), koszyk 63/13/7/17 | 74/10/7/8 | 79/10/4/7 |
| dokupiona podstawa (kg Planu/d) | 0 | Essos 800.9 t, Westeros 20.2 t (zboze; 42 miasta) |
| bilans Planu Essos S/D | winogrona 1.42, mieso 0.93, zboze 0.97 | winogrona 0.96, mieso 1.00, zboze 0.95 (reszta 0.95-1.00) |
| tozsamosc (popyt zaplacony + zjedzone) / D | 0.991-1.012 | 1.000 wszystkie towary obu rynkow |
| punkt staly Essos | NIE (brak rak do oddania) | TAK (brak rak do oddania) |
| Plan bez pokrycia (wsie) W / E | 10.2% / 1.9% | 10.4% / 2.7% |
| czas 3 Planow | 47.2 s | 53.3 s (rundy rownowagi 27900 / 23800 - drabina ulamek ms) |

Straznik "stara drabina lepsza w kcal" 0 we wszystkich klasach. Drabina na koszyku Essos R (test odbicia na DLL): budzet 50% koszyka -> kcal 100%, chleb 1.00, bialko 1.00, wino 0.23, owoce 0; 30% -> kcal 100%, mieso 0.24, wino 0; 15% -> kcal 81% (stara 49%); budzet >= koszyk - caly koszyk jak dotad.

**Ryzyko / co sprawdzic (kontrola calosci z zasady 0):**
- REGRESJE: oba klucze wylaczone - Plan wola te sama `KsBasket.Ladder` z tymi samymi argumentami, `tex` = 0, `R.TownExtra` puste, `TownExtraOf` = null, `Zc4Ladder` po staremu, `KsWageHands.Day` = 1 i bez pomiaru tozsamosci, ZC5 x 1, N4 x 1 - co do liczby jak dotad. Bez KS zastosowanego (gra Jeffa, WGRANIE 76) - nic z paczki nie dziala (ZTownOf), linii plac nie ma; w zapisie tylko "k1||".
- KOLIZJE: `UnpaidToNeed` + `ExtraToNeed` - ta sama runda, czesc zaplacona <= 1, wiec nic nie liczone drugi raz; NKS-8c (`_zLadCut` / `_zFoodBud`) i linia ZC4 - te same znaczenia; wies (8b/8c) bez zmian; konkurencja z innymi paczkami nocy: r4-glodbunt (glod -> bunt: czyta Afford/Hunger, nie sklad koszyka), ds-akumulator (W z oszczednosci; place za brak rak dokladaja W i R w miastach z brakiem rak - ograniczone x1.30), r4-ceny / r4-kupcy (ceny i KS-H zobacza wiekszy popyt zboza w Essos - KS-H dowiezie wiecej zboza). Scalenie: dopiski w Zc4.cs (4 miejsca), Core.cs (petla klas miast), TownFolk.Live.cs (krok 2), Settings.cs (nowa grupa na koncu pol), ArmouryBehavior.cs (klucz po arm_ks_r3), sprawdz_logi.py (NKS-3c i nowa grupa przed nds_main).
- ZAMKNIETA EKONOMIA: tanszy koszyk - te same kiesy, inny towar (BK kupuje z polki najwyzej budzet kategorii i najwyzej to, co lezy - `ItemConsumptionPatch.Prefix` sprawdzone w dekompilacji BannerKings.Redux; placi `TownFolk.PayForGoods` z faktycznego przyrostu kasy), niewydane zostaje w kiesie; Plan: podstawa ponad koszyk w popycie (tozsamosc 1.000). Place: kasa miasta -> kiesy (FromCoffer), tozsamosc w linii kazdej doby.
- DO SPRAWDZENIA W GRZE (120 dob z KS): (1) Essos zboze NA TARG 0.90 w uprzezy (wiecej popytu zboza, Essos "brak rak do oddania") - ceny zboza w Essos w gore, KS-H i dowoz; NKS-3 zboze E i NKS-7 ceny; (2) winogrona / wino Essos - mniej popytu wina, pary ZC5 wina i winnice; (3) NKS-13 / koszt klas QC: dokupiona podstawa dzielona na klasy wedlug ich potrzeby zboza (jak czesc zaplacona - jedna liczba na miasto), W dostaje troche zboza R/P w koszcie klasy; (4) linia "Tanszy koszyk" - kcal >= 95% w miasto-dobach drabiny, stara drabina rzadko (bez podstawy na polce); (5) place: ile miast > 1 (oczekiwane malo - fin80: 3 regiony z 227 ponad progiem; w oknie zniw wiecej), kasy miast nie na zero (NKS-16), tozsamosc 0; (6) czas linii ZC4 "drabina ms".
- NIE wiem bez gry: czy w grze polka zboza w miastach Essos uniesie podstawe ponad koszyk (gdy nie - "stara drabina" / brak, ceny podniosa sie i KS-H dowiezie). Brak dowodu z gry - tylko uprzaz i test odbicia drabiny.
- Kontrole: build Armoury rc 0 (ostrzezenia jak dotad), gen_mcm (1735 ustawien), sprawdz_logi nks/nkoszyk/nds bez wyjatku.

**Status:** DO SPRAWDZENIA (kod + uprzaz; test 120 dob z KS i grupy nks + nkoszyk + nds - test nocy).
