## 2026-10-22 (m107-gospodarka: biedne wsie, stojace wozy, zaleglosci zamkow, wycena ziarna, zapis tablicy ogloszen - Jeff 22.10 "biedne wsie sprawdz, rozwiaz stojace wozy, zaleglosci zamkow napraw, wycena ziarna, zapis tablicy ogloszen tez zrob")

**Mod:** Armoury (+ `tools/sprawdz_logi.py`, harness Planu `tools/ekonomia-symulatory-2026-10-17/kmoney/h4-feedswiat`) | **Galaz:** `w-toku/m107-gospodarka` (od `5da5f304` = n33-calosc = WGRANIE 103.2)
| **Status:** DO SPRAWDZENIA (nic nie wgrane; gry nie uruchamialem; folderu gry, `test/` i zapisow Jeffa nie dotykalem; build 0 bledow; progi - self-test i test syntetyczny OK; autotest - plan na koncu)

Projekt: scratchpad `m107/projekt-m107.txt` (diagnozy `m107/diag-wsie-zamki.txt`, `m107/diag-wz-103h.txt`, `m107/sim_nb3.py`). Prosbe Jeffa o Kronike ("Kronika ma przechodzic do innych rodow jak
w ksiazce") robi rownolegla galaz `w-toku/m107-kronika` - tych plikow (Chronicle*, CanonCompanions) ta galaz nie rusza.

Szesc nowych kluczy MCM, wszystkie domyslnie TAK; kazdy NIE = sciezka kodu jak w `5da5f304`. Nowych kluczy nie ma w `Armoury.json` Jeffa - dzialaja domyslne.

| Klucz (MCM) | Zadanie | Plik |
|---|---|---|
| K1 `VillageCartFleeUnload` (Village Cart Flee Unload; przy Village Cart Stuck Guard) | 2 stojace wozy | CartGuard.cs, MarketRoad.cs (Wire) |
| K2 `DsCastleFolkPartNoArrears` (Ds Castle Folk Part No Arrears) | 3 zaleglosci zamkow | DsTowns.cs Pay (2b) |
| K3 `DsCastleFastCut` (Ds Castle Fast Cut; przy Ds Castle Income Cut) | 3 zaleglosci zamkow | DsTowns.cs CastleIncomeCut, FastCut |
| K4 `KsPlanMarketBalance` (Ks Plan Market Balance) | 4 wycena ziarna | StartCalibration.Core.cs (Equilibrium, MoneyStep), Plan.cs |
| K5 `KsFachMarketRoom` (Ks Fach Market Room; przy Ks Village Trade Keep) | 4 wycena ziarna | StartCalibration.Fach.cs (FachStep), Plan.cs |
| K6 `NoticeBoardLeanSave` (Notice Board Lean Save) | 5 zapis tablicy | NoticeBoard.cs, nowy NoticeBoard.Nb3.cs, NoticeBoard.Watch.cs, ArmouryBehavior.cs |

Zadanie 1 (biedne wsie) bez klucza: zmienia sie miara progu i segmenty logu, zachowanie gry zostaje; naprawa gry dla wsi pod blokada band to K1.
Nowa linia startu `Gospodarka (m107): start sesji | Village Cart Flee Unload TAK | ... | Notice Board Lean Save TAK.` (nowy `M107Start.cs`, z `ArmouryBehavior.OnSessionLaunched` obok
`CartGuard.SessionStart`, po `McmSettings.Apply`).

| Commit | Co |
|---|---|
| `69252e1f` | klucze K1-K6 (Settings.cs + gen_mcm), M107Start.cs i wywolanie - bez zmian zachowania |
| `7780d2ef` | K6 tablica: format nb3 z proba odczytu, wspolny odczyt nb2/nb3, linia doby listow, linia zapisu gry |
| `def51b68` | K1 wozy (rozladunek pod ucieczka, wstrzymanie zaladunku, sprawdzenie modelem AI, wsie bez wozu) i pomiar zad. 1 (DsVillage, TownFolk.DsAkum, VillageTakings) |
| `c630ccc6` | K2 / K3 zamki |
| `8990aa9b` | K4 / K5 ziarno + przelaczniki harnessu |
| `ae2e4acf` | progi: grupa `m107gosp`, M3W-2 / M5G5-1 w nowej mierze, WZ-1b przy K1, L-8b nb3 |
| (ten wpis) | CHANGELOG galezi |

---

### 1. Biedne wsie (M3W-2 7.9% w 103h; cel <= 5%) - zadanie bez klucza

**Problem (log 103h):** M3W-2 (wsi z kiesa < 1000 zl, srednio 7 dob) 7.9%.

**Przyczyna (diagnoza `m107/diag-wsie-zamki.txt`):** 25 z 41 biednych wsi (4.4 pp) ma PELNA wlasna skrzynie na czarna godzine mniejsza niz 1000 zl - RentPay (m105 gosp5) bierze
czynsz tylko ponad skrzynie, wiec kiesa takiej wsi stoi dokladnie na skrzyni; 102 wsie maja skrzynie < 1000 zl - prog 1000 zl jest dla nich nieosiagalny z konstrukcji (artefakt miary).
Prawdziwa bieda - 16 wsi (2.8%): B1 woz stoi albo go nie ma (Antler Village, Avasinton, Akirys), B2 wsie bez wlasnego jedzenia kupujace ziarno i sol z oszczednosci, B3 male wplywy.

**Zmiana:**
- `tools/sprawdz_logi.py`: M3W-2 (m103wsie) i M5G5-1 (m105gosp5) licza "kiesa < min(1000 zl, skrzynia)" z segmentu `m105 wsie biedne` linii `Akumulator kies` (ta sama miara co
  M5G5-2 i M5G-7b), srednio 7 ostatnich dob, prog 5% bez zmian (wspolna funkcja `m107_m3w2`). Log bez segmentu - stara miara (Przeplywy osad). Nowe wiersze M3W-2r / M5G5-1r (INFO):
  surowo "< 1000 zl" i rozklad A (pelna mala skrzynia) / B (bez zapasu). M5G5-1 zostawia DECYZJA dla biegu ze starego zapisu bez prologu (wsie bez wozow).
- `DsVillage.AkNote`: liczniki `_akFullSmall` (skrzynia < 1000 i skrzynia <= kiesa < 1000) i `_akSmallChest` (skrzynia < 1000), `AkPoorMix`; `TownFolk.DsAkum.cs`: za dotychczasowym
  napisem `m105 wsie biedne ... P z N wsi DS` (bez zmian - czyta go M5G5-2) dopisek `; m107: pelna mala skrzynia (skrzynia <= kiesa < 1000 zl) A, skrzynia ponizej 1000 zl S`.
- `VillageTakings.LordRelief` (POMIAR hipotezy "licznik podatku ujemny"): woz wsi DS ponizej skrzyni, a licznik pana w oknie bez przyrostu (got <= 0) - `DsVillage.NoteReliefNoTake`;
  segment `m105 gosp5` linii `Daniny wsi (DS)` dostaje `; bez ulgi - licznik pana bez przyrostu w oknie N (brak skrzyni X zl)`. Zmiana zachowania (ulga przed wyplata prac okregu, W3)
  dopiero po danych z autotestu.
- Wsie bez wozu (Akirys) - segment K1 w linii wozow (zadanie 2), tylko pomiar.

**Na logach bazowych (nowa miara, bez kodu m107):** 103h 3.15% (TAK), 103d 5.00% (NIE - na granicy: 26 z 571). Surowo "< 1000 zl" 103h 7.9%, 103d 8.7%.

### 2. Stojace wozy (WZ-1b = 3; H1 "cel chwilowy = osada" 1355 z 1355) - K1 `VillageCartFleeUnload`

**Problem (103h):** 3 wozy w domu z jukami i rozkazem ponad 30 dob; H1: "AI wylaczone 17, Hold 0, cel chwilowy = osada 1355".

**Przyczyna (gra - dekompilacja 1.4.8 `m107/dec`, `m107/dec2`):** `MobilePartyAi.GetBehaviors` (MobilePartyAi.cs:488) bierze inicjatywe z `MobilePartyAIModel.GetBestInitiativeBehavior`
- wynik > 1 i zachowanie ucieczki, a `GetFleeBehavior` (:738-744) dla partii w osadzie daje "GoToSettlement = ta osada" - zaladowany woz nie wyjezdza, bo bandy z domem w tej wsi
kraza wokol niej. Nasz straznik (m106): po 5 dobach nowy rozkaz (bez skutku - ucieczka ma pierwszenstwo), po 10 dobach rozladunek - cofany przez gre: `VillagerCampaignBehavior.
ThinkAboutSendingItemToTown` (dec2:131-170; co godzine 15%, magazyn >= W) laduje i wysyla woz ponownie; po 20 dobach straznik sie poddaje.

**Zmiana (`CartGuard.cs`, przy Village Cart Stuck Guard):**
- `Home()`, przed krokami 5/10/20: woz z jukami i rozkazem, ktorego `ShortTermTargetSettlement == dom` (warunek H1), po `FleeUnloadDays` = 3 dobach w tym stanie (wies Normal, bez bitwy
  wozu i wsi): `Unload` (juki do magazynu tej samej wsi), `MarketCarts.ForgetPending`, `MarketRoad.Move(dom)`, wies do `_fleeHold`, koniec epizodu (`ClearHome`, takze `FleeSince`).
- `HoldCheck()` raz na dobe (przed `Guard`): woz brak / nieczynny / nie w domu - zdjecie; inaczej `Campaign.Current.Models.MobilePartyAIModel.GetBestInitiativeBehavior(woz)` -
  zagrozenie = wynik > 1 i `MobileParty.IsFleeBehavior` (dec MobileParty.cs:2903); brak zagrozenia albo wyjatek - zdjecie. Bez limitu dob.
- Prefiks `ThinkPrefix` na `VillagerCampaignBehavior.ThinkAboutSendingItemToTown(Village)` (`CartGuard.Wire` z `MarketRoad.ApplyAll`, `AccessTools.DeclaredMethod`): wies wstrzymana i
  klucz TAK - `return false` (bez zaladunku, dobierania ludzi i nowego wozu); brak metody - linia `Wozy (m107): BRAK ...` (rozladunek dalej dziala).
- Pomiar: wies bez wozu ponad 14 dob - przyczyna wedlug warunku gry (magazyn < W, ogniska <= `MinimumNumberOfVillagersAtVillagerParty`, wies nie Normal, inne).
- Linia `Wozy (m106): dzien` - nowy segment przed `| czas`: `| m107 ucieczka w domu (Village Cart Flee Unload TAK): rozladowane N (szt. U), wsie wstrzymane H (nowe a, zwolnione f,
  sprawdzenia c, wrog dalej s, ponad 30 dob L), odmowy zaladunku B | bez wozu ponad 14 dob K (magazyn < W a, ogniska ponizej minimum b, wies nie Normal c, inne d)`; klucz NIE - `NIE -
  WYLACZONE` i zera; wszystkie klucze W2 NIE - bez linii doby (jak dotad). Co 5 dob najwyzej 3 + 3 przyklady `Wozy (m107) wstrzymana: ...` i `Wozy (m107) bez wozu: ...`.
- Zamknieta ekonomia: towar tylko juki <-> magazyn tej samej wsi, zloto bez ruchu.

### 3. Zaleglosci zamkow (M5G-13: 17.8-29 tys. zl, +300..+575 zl/d) - K2 `DsCastleFolkPartNoArrears`, K3 `DsCastleFastCut`

**Problem:** M5G-13 (zaleglosci zamkow <= 20 tys. zl i MNK 28 dob <= +200 zl/d): 103h 17.8 tys. (+324 zl/d), 103d (zapis) 113 tys., Haystack Hall 49.7 tys. = 9.8 raty.

**Przyczyna (diagnoza):** 79-100% zaleglosci zamkow w nowej kampanii to czesc firmy z oszczednosci podzamcza (ALf): FLf ustalone w oknie szczytu kies podzamczy, bez k swiata i bez G6;
lud placi tylko ponad zapas (`TownFolk.DsFolkRate`), brak -> ALf, a zajecie (Distrain) dziala tylko dla miast. Reszta - kasy zamkow przy podlodze: G6 schodzi po 25% co 14 dob, zaleglosci
narosle na zawyzonej firmie zostaja.

**Zmiana (`DsTowns.cs`):**
- K2, `Pay()` 2b, zamek przy `DsFarmFolkFromSavings`: lud placi z nadwyzki ponad zapas (`FolkToLord(floor(ALf + iLf))`, zaleglosc najpierw), reszta DAROWANA, `ALf = 0` (jak czynsz wsi
  ponad skrzynie w m105 gosp5). Zaleglosc z zapisu splacana z nadwyzki w pierwszej racie sesji, reszta darowana raz (licznik "z zapisu"). Bez zywego pana - rata ludu zamku darowana
  (nie czeka w ALf). Ksiegowanie: `before -= forgOld` - darowanie nie liczy sie jako "splacone". Miasta bez zmian.
- K3, `CastleIncomeCut`: kasa po naleznosciach < `CastlePurse.Reserve` albo zaleglosci >= raty, bez oblezenia - `FastCut(f, today, castle: true)` (firma od razu do doplywu netto 28 dob
  x 364, raz na 7 dob, bez `KsFolkIncome.NoteFastCut`); bez 28 probek / firma juz przy doplywie / odstep - `IncomeCut` jak dotad. Po obnizce (FastCut albo IncomeCut) zaleglosci z kas
  AL i AC w stosunku firmy po / przed (ALf bez zmian - FLf placi lud).
- Linia `Firmy miast i zamkow (DS)`, za `m105 zamki`: `| m107 zamki (Ds Castle Folk Part No Arrears TAK, Ds Castle Fast Cut TAK): czesc z oszczednosci podzamcza niezaplacona i darowana
  dzis X zl (N zamkow), w sesji Y zl (M), w tym zaleglosci z zapisu Z zl; szybka obnizka zamkow dzis a (-b zl/rok), w sesji c (-d zl/rok); zaleglosci z kas zamkow zmniejszone z firma
  dzis e zl, w sesji f zl; zaleglosci zamkow: z kas G zl, z oszczednosci podzamcza H zl`; oba klucze NIE - linia jak w `5da5f304`.
- Zloto sie nie rusza - zmienia sie nalezna rata (zamknieta ekonomia; zaleglosc to roszczenie, nie pieniadz).

### 4. Wycena ziarna (NKS-3: Westeros grain 1.10 w Planie wlaczenia, pasmo 0.95-1.05 x f 1.04) - K4 `KsPlanMarketBalance`, K5 `KsFachMarketRoom`

**Przyczyna (diagnoza):** Plan sprawdza zbieznosc |S/D - 1| <= 0.02 od calego popytu (Core.cs:945), a wsie zjadaja ok. 78% ziarna Westeros - 2% lacznie to ok. 9% na targu; ziarno W
NA TARG 1.08-1.12 we wszystkich Planach 103e i 103h. Fach (m106) liczy wolne miejsce na targu bez zjedzonego tylko we wsiach z fachem (Fach.cs:151) - po wlaczeniu dokladal ziarno.

**Zmiana:**
- K4 (`StartCalibration.Core.cs`): `KsInput.MarketBalance` (z MCM w Plan.cs), `Ctx.EqEat` / `Ctx.EqMkt`. W petli kroku 8 (`MoneyStep`) przed kazda rownowaga `x.EqEat = eat` (zjedzone tej
  rundy), `x.EqMkt = MarketBalance && it == iters - 1` (ostatnia rownowaga kroku 8 - stan oddany do bilansu). `Equilibrium`, punkt kontrolny (co 50 rund): towar z E >= `MktShare` (0.25)
  x D, E = min(zjedzone, 0.9 min(D, S)) - odchylenie |(S - E) / max(1, D - E) - 1|, prog `MktTol` 0.03; w ocenie "zbiezna", w sumie mt (najlepszy stan, wyjscie "stoi"). Krok ceny
  (D + 1) / (S + 1) i normalizacja BEZ zmian (punkt staly S = D ten sam). Wynik `R.MktOn/MktN/MktConv/MktRounds/MktWorst`.
- K5 (`StartCalibration.Fach.cs` FachStep): `avail = max(0, dm - sr - max(sumOwn, E))`, E = zjedzone we WSZYSTKICH okregach rynku (towar gi; zywiec wobec miesa) - z poprzedniego Planu
  (`KsInput.PrevEaten = _plan.Eaten`, Plan.cs), w pierwszym Planie sesji z rundy kroku 8 (`x.EqEat`); przed krokiem 8 pierwszego Planu - jak dotad. `FachRoomPrev` (harness:
  `KS_FACHROOMEQ=1` - odwrotna kolejnosc zrodel).
- Linia Planu: `| m107 rownowaga na targ (Ks Plan Market Balance TAK): towarow z miara targu n, ostatnia rownowaga kroku 8 zbiezna TAK/NIE (rund r), najwieksze odchylenie targu <rynek
  towar> x.xx` (przed `_snapNote`); linia fachu: `| miejsce na targu od popytu - zjedzone we wszystkich okregach (Ks Fach Market Room TAK)` przed `| czas fach` (regex WZ bez zmian).

### 5. Zapis tablicy ogloszen (L-8: arm_board 12.5-14.3 KB > 12288 B; L-9: 3.36 ms > 3 ms) - K6 `NoticeBoardLeanSave`

**Przyczyna:** L-8 - 71% napisu to szkody band (slaby podzial id `ROT_town12` -> `^7_town12`, doba i rodzaj w osobnych polach, pelne miasto w 73% wpisow). L-9 - koszt doby listow
obejmowal `Export()` budowany co dobe tylko po to, zeby wpisac rozmiar do linii (ok. 0.47 ms i 0.5 MB alokacji dziennie - pauzy GC 6-8% dob w biegach 160).

**Zmiana:**
- Nowy `NoticeBoard.Nb3.cs` (bez typow gry - ten sam plik kompiluje test poza gra): `IdTable` przeniesiona z NoticeBoard.cs bez zmian dla nb2 + tryb v3 (podzial na granicy litera/cyfra
  koncowki z samych cyfr: `ROT_town12` -> przedrostek `ROT_town`, kod `^k-12`, cyfry doslownie; krotszy kod wygrywa), `Dec3` (osobna funkcja - odczyt nb2 bez zmian), `ExportV3`
  (`nb3|D|P|T|F|W|H`: T - miasta ids.Enc, w W i H numer base36 w T; rodzaj 0 w listach pusty; wpis szkody base36(przesuniecie x 2 + rodzaj)), wspolny `Parse(parts, v3, ...)` do podanych
  kontenerow (Import2 i nowy Import3 - statyczne; Probe3 - osobne), `Probe3` - proba odczytu przy KAZDYM eksporcie nb3, porownanie pole po polu z zywym stanem filtrowanym jak eksport.
  Niezgodnosc albo wyjatek - zapis nb2 i linia `Tablica (m107): proba odczytu nb3 NIEZGODNA (pierwsza roznica: ...) - zapis nb2`.
- `Import()`: `nb3` -> Import3 (niezaleznie od klucza); nb2 i nb1 czytane jak dotad.
- `NoticeBoard.Watch.cs` WatchDaily przy K6: koszt doby liczony PRZED eksportem; eksport (do linii) w pierwszej dobie sesji i co 10 dob, nb1 co 30 dob (i w pierwszej) - poza kosztem;
  linia `...; zapis arm_board N B; sklad zapisu nb3 (pomiar doby D): naglowek z przedrostkami i miastami (T) ..., stary format nb1 w tym samym stanie X B (p%); ...; koszt X ms; ...`
  (fragmenty czytane przez M102_LG_DAY bez zmian). K6 NIE - kolejnosc i napis jak dotad.
- `ArmouryBehavior.SyncData`: `NoticeBoard.ExportSave()` - przy K6 linia przy kazdym zapisie gry `Tablica (m107): zapis gry arm_board nb3 N B (sklad), proba odczytu zgodna TAK,
  eksport X ms; proby w sesji: zgodne a, niezgodne b` (pusty - `zapis gry arm_board pusty (0 B; ...)`). WatchStartLine przy K6: `zapis arm_board nb3 (odczyt takze nb2, nb1; ...)`.
- Komentarz: SaveText kawalkuje napisy od 8000 znakow (SaveText.Part), nie od 16 KB.

**Test poza gra (`m107/bench3`, .NET 4.7.2, ten sam `NoticeBoard.Nb3.cs` + zastepniki; wynik `m107/bench3/wynik.txt`, kod wyjscia 0):**
- `board-1344.txt` (prawdziwy zapis nb2 z gry): Parse nb2 -> eksport nb2 (kopia kodu 5da5f304) == wejscie (5194 B); nb3 z proba zgodna 3965 B (76.3%); Parse nb3 -> nb2 == nb2 sprzed.
- `real-1344.txt` i `synth-103c.txt` (stan 103c-160: 105 listow, 285 band, 758 wpisow): nb2 14101 B -> nb3 9387 B (66.6%); odczyt nb3 -> nb2 identyczny co do bajtu.
- Mutacje: rodzaj szkody 2, doba w przyszlosci, przecinek w miescie listu - proba NIEZGODNA (zapis nb2); wsie (miasto z wsi i inne), flagi, rodzaj listu 1, wpisy > 60 dob - zgodne.
- Rozmiar wobec `sim_nb3.py` (wariant B): listy i szkody co do bajtu (2917 / 5777 B); naglowek mniejszy (T przez ids.Enc) - 103c 9387 wobec 9541 B (-1.6%), board-1344 3965 wobec 4122 B
  (-3.8% - lepiej niz symulator).
- Czas: nb3 + proba 0.65 ms i ok. 1 MB alokacji na wywolanie (nb2: 0.37 ms, 0.5 MB) - tylko co 10 dob i przy zapisie gry.

---

### Grupa `m107gosp` (`python -I tools/sprawdz_logi.py --grupa m107gosp <Armoury-*.log> [--do-doby N]`)

M7G-S self-test wyrazen na liniach kontrolnych (napisy jak w C#; sprawdza tez wyrazenia grup m105gosp5 (poor, g5), m105gosp4 (zam), m106wozy (h1, ms, fach), nks (NKS_PLAN, niezbiezne)
na tych samych liniach z segmentami m107), M7G-S2 test syntetyczny (dobry i zly bieg), M7G-0 linia startu z 6 kluczami TAK; wsie M7G-11 (M3W-2 nowa miara <= 5%), M7G-12 (straznik:
pelna mala skrzynia MNK 28 dob <= +0.05 wsi/d), M7G-13 / 14 / 15 INFO; wozy M7G-21 (H1 "cel chwilowy = osada" w 28 liniach srednio <= 0.5, najwiecej <= 2), M7G-22 (rozladunki > 0
przy ucieczkach, "bez wyjasnienia" <= 4 szt., NKS-8 TAK), M7G-23 INFO, M7G-24 (mediana linii Wozy <= 2 ms); zamki M7G-31 (format), M7G-32 (ALf zamkow <= 1 zl od 14. doby sesji), M7G-33
(najwieksza zaleglosc zamku <= 4 raty), M7G-34 / 35 INFO; ziarno M7G-41 (segment w kazdym Planie; zbiezna >= 80% - mniej INFO), M7G-42 (ziarno W i E NA TARG od wlaczenia: mediana
kazdego rynku <= 1.06, najwiecej <= 1.05 x max(1, f)), M7G-43 INFO, M7G-44 (dopisek K5); tablica M7G-51 (kazdy zapis gry <= 12288 B i proba zgodna), M7G-52 (wczytanie nb3 = liczby
ostatniego zapisu w tym logu), M7G-53 (mediana kosztu doby listow <= 1.0 ms, doby >= 1.5 ms <= 2%), M7G-54 (wczytanie nb2 -> nastepny zapis nb3 zgodny); M7G-9 ERROR nowych funkcji,
zly format segmentu wozow, proby niezgodne 0.

Zmiany innych grup: m103wsie M3W-2 + M3W-2r i m105gosp5 M5G5-1 + M5G5-1r (nowa miara, zad. 1); m106wozy WZ-1b - przy segmencie m107 z K1 TAK prog 0 bez wyjatku DECYZJA, linia
kontrolna M106_TEST["day"] z segmentem m107 (WZ-S / WZ-S2 OK); m102tablica L-8b - `sklad zapisu nb[23]( \(pomiar doby D\))?:`. `sprawdz_narzedzie.py` OK.

**Na logach bazowych (bez kodu m107; wydruki scratchpad `m107/progi-out`):** 103h - M7G-S/S2 TAK, M7G-11 3.15% TAK, M7G-42 NIE (mediana W 1.05 / E 0.98, najwiecej 1.13 - Westeros
doba 108703), M7G-9 TAK, reszta INFO (log sprzed m107); m106wozy 103h WZ-S/S2 TAK, WZ-1b DECYZJA (bez segmentu K1 - jak dotad); M3W-2 103h 3.15% TAK / 103d 5.00% NIE (na granicy:
26 z 571 wsi DS); m102tablica L-8 103d 12482 B NIE, 103h 5666 B; L-9 103d 2.93 ms, 103h 1.76 ms.

---

### Bramka symulatora K4 / K5 (harness `kmoney/h4-feedswiat` z tej galezi; zrzut KS 16.10 + plon-wsi.csv 103e jako baza fachu, klucze jak w grze; skrypt `m107/gate/run_gate.sh`, wyniki `m107/gate/out_*.out`)

Baza ostatniej rownowagi kroku 8 = bieg z `KS_MKTBAL=1 KS_MKTSHARE=99` (zaden towar bez miary targu - ten sam przebieg co baza, liczby rund w linii MKT; rundy calych Planow = baza co do rundy).

| Plan (5 Planow, cieply start; Plan 5 = ApplyPlan) | baza: czas / rundy / ostatnia rownowaga | K4 | K4 + K5 |
|---|---|---|---|
| 1 | 23.1 s / 27050 / 950 | 22.6 s / 26950 / 700 (-26%) | 22.5 s / 25800 / 1200 |
| 2 | 13.3 s / 15200 / 1200 | 13.4 s / 15300 / 1300 (+8%) | 13.7 s / 15300 / 2650 |
| 3 | 12.7 s / 14450 / 1000 | 13.0 s / 14800 / 1200 (+20%) | 12.8 s / 14450 / 900 |
| 4 | 12.1 s / 13550 / 1250 | 12.6 s / 14200 / 950 (-24%) | 14.3 s / 16150 / 2200 |
| 5 | 11.3 s / 12700 / 950 (krok 5 rund 1) | 20.9 s / 23300 / 1550 (+63%; krok 5 rund 2) | 27.6 s / 31400 / 1350 (krok 5 rund 2) |
| razem ostatnia rownowaga | 5350 | 5700 (+6.5%) | 8300 (+55%) |

- Wynik: K4 srednio +6.5% rund ostatniej rownowagi (prog bramki +15% - TAK), mediana czasu Planu +0.3 s (TAK), ale pojedyncze Plany od -26% do +63%, a w Planie 5 (ApplyPlan) inny stan
  oddany przestawia DRUGA runde gradientu (warunek `GradDMax > 5%` i czas < 15 s: zmiana gradientu 4.8% -> 5.8%) - pelny krok 8 jeszcze raz, +9.6 s. To efekt progowy (kazda zmiana
  stanu moze go przestawic), nie koszt samej miary. W grze (103h) druga runda dotyczy tylko Planu do wlaczenia (Plany przed wlaczeniem 12-21 s, Plan wlaczenia 108682 15.3 s); Plany po
  wlaczeniu (20-29 s, max 28.7 s) nie maja drugiej rundy. Ryzyko NKS-2 (<= 30 s) - w autotescie; plan awaryjny: K4 NIE (albo `MktShare` 0.5 - w harnessie bez poprawy: ostatnia
  rownowaga 700 / 1600 / 1200 w 3 Planach).
- Ziarno: zrzut 16.10 NIE MA problemu z gry - ziarno W NA TARG 0.98-1.00 juz w bazie (103e / 103h 1.08-1.13), k fachu W grain 1.00 we wszystkich wariantach (K5 nie zmienia celu
  ziarna, bo miejsca na targu starcza); dzialanie K4 / K5 na ziarno sprawdzi dopiero autotest (NKS-3, M7G-42). "Plan bez pokrycia" W: baza 6.1-6.5%, K4 6.1-6.5%, K4+K5 5.6-6.8%
  (szum przebiegu).
- K5 (zrodlo zjedzonego): `FachRoomPrev` = TAK (poprzedni Plan, staly w Planie) - w 2 Planach 21.3 s / 24350 rund wobec 26.5 s / 30500 przy rundzie kroku 8 (`KS_FACHROOMEQ=1`) i bazy
  22.6 s / 25450; w 5 Planach K4+K5 wolniej niz sam K4 (ostatnia rownowaga +55% wobec bazy) - tez szum przebiegu, ale do autotestu: NKS-2 i M7G-41 (rundy w segmencie Planu).
- Decyzja w tej pracy: K4 i K5 domyslnie TAK (jak w projekcie) do autotestu; NKS-2 > 30 s albo M7G-42 bez poprawy -> ta czesc NIE (decyzja w raporcie).

---

### Ryzyko / co sprawdzic (zasada 0 - kontrola calosci)

**Regresje (kto czyta / pisze ten sam stan):**
- K6: `Export()` wolaja tylko SyncData (teraz przez `ExportSave` - przy NIE sam `Export`) i WatchDaily; przy NIE sciezka i kolejnosc jak w 5da5f304 (Export -> ms -> ExportV1 ->
  SaveMixText). `IdTable` / `Dec` przeniesione bez zmian - nb2 bit w bit (test: nb2 -> Parse -> nb2 == wejscie). `WatchImport2` zastapiony wspolnym `Parse` - te same reguly; roznica tylko
  przy wyjatku w czesci F= (dotad przerywal caly odczyt, teraz potkniecie tej czesci i dalej). `_sec*` dopisywane takze w sciezce nb2 (pola tylko do linii K6).
- K6: starsza DLL (103.2) nie czyta nb3 ("zapis arm_board w nieznanej wersji ... - pominiety"): listy wracaja w kilka dob, szkody band przepadaja na 60 dob (mniejsze doplaty do listow).
  Cofniecie do 103.2 po grze z m107 - tylko ta strata (bez wyjatku).
- K1: `Home()` - blok ucieczki przed logika 5/10/20; `FleeSince` zerowany w `ClearHome` (woz poza domem - od nowa). Prefiks na prywatnej metodzie vanilli `ThinkAboutSendingItemToTown` -
  ta sama klasa, ktora m106 lata (`SendVillagerPartyToTradeBoundTown` - wolana z `LoadAndSendVillagerParty`, wiec przy wstrzymaniu tez nie biegnie). Wstrzymanie zdejmowane, gdy woz
  zniknal - gra moze wystawic nowy. Linia doby: segment przed `| czas` - wyrazenia m106 (h1, ms) sprawdzone self-testem.
- K1: `GetBestInitiativeBehavior` czysci statyczna `MobilePartyAi.DangerousPartiesAndTheirVecs` - wolane w dobie kampanii (watek glowny, poza tickiem AI, ktory buduje ja od nowa przy
  kazdym wywolaniu). NavalDLC opakowuje model bazowy (`BaseModel`) - wolamy model czynny (`Campaign.Current.Models`), jak gra.
- K2: tylko zamki i tylko przy `DsFarmFolkFromSavings` (przy NIE ALf i tak schodzi do AL - bez zmian); `CofferOwes` nie liczy ALf przy ludzie placacym - bez zmian. Zaleglosc z zapisu
  darowana w pierwszej racie (nie w Import - kolejnosc zdarzen jak dotad). `_arrPaid` bez darowania (before - forgOld); `_arrNew` bez darowanej raty bez pana.
- K3: zamki tylko przy `CastleCutOn` (Ds Castle Income Cut + Ks Farm Surplus Cap Net + kalibracja); `FastCut` wymaga pelnych 28 probek doplywu (pamiec sesyjna - po wczytaniu najwczesniej
  od 28. doby, wczesniej IncomeCut jak dotad); zamki nie licza sie w `KsFolkIncome` (segment f5 miast bez zmian). Skalowanie AL / AC - tylko roszczenia (zloto bez ruchu); krok 5
  (darowanie ponad DsFarmForgiveWeeks) bez zmian.
- K4: tylko ostatnia rownowaga kroku 8 (`EqMkt`), takze w powtornym kroku 8 (druga runda gradientu); `EqEat` zerowane po kroku 8 (i przy przerwaniu) - pozostale rownowagi jak dotad.
  `OpenAbsM` / `Open` (stara miara) bez zmian - rundy pierwszej rownowagi nastepnego Planu jak dotad.
- K5: k fachu zalezy od zjedzonego we wszystkich okregach - mniejszy cel fachu przy pelnym targu (ziarno, ryby); WZ-7b (fach w Planie >= 50% celu) liczy wzgledem nowego celu.

**Kolizje:** linia `Daniny wsi (DS)` - dopisek za `czynsz darowany do skrzyni ... wsi)` (regex g5 bez zmian, sprawdzone); linia `Akumulator kies` - dopisek za `... z N wsi DS` (regex poor
bez zmian); linia Planu - segment przed `_snapNote` (NKS_PLAN, `niezbiezne: ([^|]*)` bez zmian); linia fachu - przed `| czas fach` (regex fach WZ - `.*\| czas fach (\d+) ms\.$`).
Rownolegla galaz `w-toku/m107-kronika` - pliki rozlaczne (Chronicle*, CanonCompanions); wspolne przy scalaniu: Settings.cs / McmSettings.cs (gen_mcm), ArmouryBehavior.cs (jedna linia
w OnSessionLaunched, jedna w SyncData), tools/sprawdz_logi.py (dispatch i opis grupy - `scal.sh` + `sprawdz_narzedzie.py`).

**Zamknieta ekonomia:** K1 - towar juki <-> magazyn tej samej wsi; K2 / K3 - zmienia sie nalezna rata (roszczenie), nie zloto; K4 / K5 - tylko Plan (rece, ceny cienia), bez przelewow;
K6 i zad. 1 - tylko zapis i log.

**Cudzy kod (dekompilacja 1.4.8):** VillagerCampaignBehavior (TickVillageThink: wies Normal i bez bitwy -> ThinkAboutSendingItemToTown; 15% / magazyn >= W / CreateVillagerParty /
AddVillagersToParty / LoadAndSendVillagerParty), MobilePartyAi.GetBehaviors (wynik > 1), GetFleeBehavior (partia w osadzie -> GoToSettlement ta osada), DefaultMobilePartyAIModel.
GetBestInitiativeBehavior (Clear statycznej listy), NavalDLCMobilePartyAIModel (BaseModel), MobileParty.IsFleeBehavior (publiczne statyczne).

**NIEPEWNE (do autotestu):**
- 103d przy granicy w dwoch progach: M5G-13 (prognoza 8-20 tys. przy progu 20 tys.) i M3W-2 / M7G-11 (baza 5.00%, prognoza 4.6-5.0%).
- K4 nie jest sprawdzony na symulatorze co do ziarna (zrzut 16.10 nie ma problemu - ziarno W NA TARG 0.98-1.00 juz bez K4); czas Planu - patrz bramka.
- Akirys (wies bez wozu) - przyczyna nieustalona, tylko pomiar (M7G-15). Hipoteza "licznik podatku ujemny" - tylko pomiar (M7G-14).
- M7G-52 porownuje wczytanie z ostatnim zapisem w TYM SAMYM logu - wczytanie w nowym procesie (nowy log) daje INFO; porownac recznie z linia zapisu poprzedniego logu.

---

### Plan autotestu (gry nie uruchamialem - zakaz w tej pracy; ta sama zgoda co dotad: tylko gdy Jeff nie gra, kopia WSZYSTKICH logow Jeffa z dnia przed pierwszym biegiem, DLL z
`test/n107` tylko na czas testu (testy-103.ps1 / bieg-bitwa.ps1), potem przywrocone 103.2; zapisy Jeffa nietkniete)

Przed gra: build 0 bledow (5 modow przy scalaniu), gen_mcm, `sprawdz_narzedzie.py`, test `m107/bench3` (kod 0), bramka symulatora (wyzej).
- **Bieg A:** nowa kampania, prolog 230 dob z KS (jak 103h) + wczytanie +20 dob z zapisu konca biegu (odczyt nb3, ALf zamkow z zapisu = 0, pamiec wozow sesyjna).
- **Bieg B:** 160 dob z zapisu (jak 103d; zapis kampanii z prologiem - liczy sie M5G-13; pierwsze wczytanie nb2 -> zapis nb3).

Progi na obu: `m107gosp` (calosc), `m106wozy` (WZ-1b = 0, WZ-6, WZ-7, WZ-8), `m103wsie` (M3W-2 nowa miara <= 5%, M3W-1, M3W-3, M3W-7), `m105gosp4` (M5G-13), `m105gosp5` (M5G5-1 / 2),
`nks` (NKS-2, NKS-3, NKS-5), `npolnoc` (NKS-30b), `m102tablica` (L-1..L-10) + regresja jak test 103d (`m105kronika`, `m105miasta`, `m105drobne`, `m96bandy`); ERROR Armoury 0, kod wyjscia 0.

| Prog | Bieg A (oczekiwane) | Bieg B (oczekiwane) |
|---|---|---|
| WZ-1b | 0 | 0 |
| M3W-2 / M7G-11 (nowa miara) | 3.0-3.5% | 4.6-5.0% |
| M5G-13 | ok. 1 tys. zl | 8-20 tys. zl, nachylenie <= +100 zl/d |
| NKS-3 | TAK (W grain 1.00-1.05) | TAK |
| NKS-2 | <= 29.5 s | <= 29.5 s |
| L-8 | - | <= 10 KB |
| L-9 | <= 2.5 ms | <= 2.5 ms |

Niepowodzenie czesci -> poprawka albo ta czesc z kluczem domyslnie NIE (decyzja w raporcie); reszta moze isc dalej. Wgranie po udanym autotescie (stala zgoda 09.10, z kopia).
