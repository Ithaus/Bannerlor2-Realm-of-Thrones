## 2026-10-22 (m107-gospodarka: biedne wsie, stojace wozy, zaleglosci zamkow, wycena ziarna, zapis tablicy ogloszen - Jeff 22.10 "biedne wsie sprawdz, rozwiaz stojace wozy, zaleglosci zamkow napraw, wycena ziarna, zapis tablicy ogloszen tez zrob")

**Mod:** Armoury (+ `tools/sprawdz_logi.py`, harness Planu `tools/ekonomia-symulatory-2026-10-17/kmoney/h4-feedswiat`) | **Galaz:** `w-toku/m107-gospodarka` (od `5da5f304` = n33-calosc = WGRANIE 103.2)
| **Status:** DO SPRAWDZENIA (nic nie wgrane; gry nie uruchamialem; folderu gry, `test/` i zapisow Jeffa nie dotykalem; build 0 bledow; progi - self-test i test syntetyczny OK; po
recenzji ekonomii i kodu - poprawki `c19d1b46`, `d63ef336`, test `m107/bench3` ponownie kod 0, `sprawdz_narzedzie.py` OK; autotest - plan na koncu)

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

Zadanie 1 (biedne wsie) bez klucza: tylko pomiar (segmenty logu, wiersze INFO) - prog wiazacy surowy jak dotad (po recenzji R1), zachowanie gry zostaje; przyczyna w grze (wsie bez
utargu wozow po Planie, Zelazne Wyspy, Nocna Straz) - osobna praca; wsie pod blokada band - K1.
Nowa linia startu `Gospodarka (m107): start sesji | Village Cart Flee Unload TAK | ... | Notice Board Lean Save TAK.` (nowy `M107Start.cs`, z `ArmouryBehavior.OnSessionLaunched` obok
`CartGuard.SessionStart`, po `McmSettings.Apply`).

| Commit | Co |
|---|---|
| `69252e1f` | klucze K1-K6 (Settings.cs + gen_mcm), M107Start.cs i wywolanie - bez zmian zachowania |
| `7780d2ef` | K6 tablica: format nb3 z proba odczytu, wspolny odczyt nb2/nb3, linia doby listow, linia zapisu gry |
| `def51b68` | K1 wozy (rozladunek pod ucieczka, wstrzymanie zaladunku, sprawdzenie modelem AI, wsie bez wozu) i pomiar zad. 1 (DsVillage, TownFolk.DsAkum, VillageTakings) |
| `c630ccc6` | K2 / K3 zamki |
| `8990aa9b` | K4 / K5 ziarno + przelaczniki harnessu |
| `ae2e4acf` | progi: grupa `m107gosp`, M3W-2 / M5G5-1 w nowej mierze (cofniete w `d63ef336` - recenzja R1), WZ-1b przy K1, L-8b nb3 |
| `8371741c` | CHANGELOG galezi |
| `c19d1b46` | POPRAWKI po recenzji (kod): K3 FastCut zamku bez doplywu, pamiec doplywu zamkow w zapisie, przyciecie zaleglosci zamkow do raty; K2 ulamek raty do nastepnej raty; K6 doba zapisu i BLAD eksportu |
| `d63ef336` | POPRAWKI po recenzji (progi): M3W-2 / M5G5-1 / M7G-11 surowo, nowa miara INFO; M7G-23 wiazacy; M7G-45; M7G-52 po dobie; M107_ERR; klucze tylko z linii startu |
| (ten wpis) | CHANGELOG po poprawkach |

**Poprawki po recenzji ekonomii i kodu (22.10; weryfikacja: 11 uwag PRAWDA, R6 ekonomii NIEPRAWDA - bez zmiany):**

| Uwaga | Co bylo zle | Poprawka |
|---|---|---|
| Ekonomia R1 | nowa miara M3W-2 (kiesa < min(1000, skrzynia)) chowala spadek dochodu wsi - skrzynia idzie za dochodem; teza "prog nieosiagalny z konstrukcji" falszywa | wiaze surowe "< 1000 zl" (M3W-2, M5G5-1, M7G-11); nowa miara INFO (M3W-2n, M5G5-1n, M7G-11n); zad. 1 zostaje NIE |
| Ekonomia R2 | pamiec doplywu zamkow tylko w sesji - FastCut najwczesniej od 29. doby, w sesjach Jeffa nie dziala | pola 24-25 `~NF~srednia` zamkow w zapisie (K3), wypelnienie pamieci przy pierwszej probce sesji |
| Ekonomia R3 | po obnizce zaleglosci z kas skalowane, ale ta sama liczba rat - flaga trzyma, zamek tylko w dol | AL + AC przyciete do raty po obnizce - 1 zl (K3), licznik w segmencie; skala - autotest |
| Ekonomia R4 | przy K1 WZ-1b bez DECYZJI, M7G-23 INFO, HoldCheck bez limitu dob | M7G-23 wiazacy: 0, > 0 - DECYZJA |
| Ekonomia R5 / kod R2 | ulamek raty ludu darowany co tydzien i liczony jako "zamek z darowaniem"; `_noLordAmt` z darowana rata | ulamek < 1 zl do nastepnej raty, darowany tylko brak >= 1 zl; `_noLordAmt` bez czesci darowanej; segment sumuje ALf >= 1 zl |
| Ekonomia R7 | K4 + K5 w Planie 5 bramki 27.6 s; skutek uboczny K5 (welna, len) poza projektem | M7G-45 (czas Planu wlaczenia i cieplych osobno), regula autotestu: najpierw K5 NIE; opis bramki uscislony |
| Kod R1 | FastCut zamku przy doplywie <= 0 zerowal firme na zawsze | zamek z doplywem <= 0 - bez szybkiej obnizki (IncomeCut z podloga) |
| Kod R3 | M7G-52 parowal wczytanie z ostatnim zapisem - autozapis miedzy nimi dawal falszywe NIE | linia zapisu z "doba zapisu D", para po dobie |
| Kod R4 | M7G-9 nie widzial bledow sciezek m107 lapanych wyzej; wyjatek w Export logowany jako "pusty" | M107_ERR rozszerzone; "BLAD eksportu" (M7G-51 / M7G-9) |
| Kod R5 | bez linii startu wszystkie klucze liczone jako TAK - werdykty na logu sprzed m107 | klucz TAK tylko z linii startu; opis grupy uscislony |
| Kod R6 | CHANGELOG: "nb2 czytane jak dotad" - przy uszkodzonym F= odczyt idzie dalej (dawniej przerywal) | wpis w zad. 5 |

---

### 1. Biedne wsie (M3W-2 7.9% w 103h; cel <= 5%) - zadanie bez klucza

**Problem (log 103h):** M3W-2 (wsi z kiesa < 1000 zl, srednio 7 dob) 7.9%.

**Przyczyna (diagnoza `m107/diag-wsie-zamki.txt`, poprawiona po recenzji R1 - skrypty `m107/rec-ekon/w_r1*.py`, `m107/popr/an_poor_plon.py`):** 25 z 41 biednych wsi ma pelna
wlasna skrzynie na czarna godzine mniejsza niz 1000 zl (RentPay bierze czynsz tylko ponad skrzynie - kiesa stoi na skrzyni). To NIE jest artefakt miary: skrzynia zalezy od dochodu
wsi (`DsVillage.ChestOf` = 21 x (koszyk `VillNeeds.ChestBasket` z ilosci zaplaconych w Planie + czynsz)), wiec wies, ktora biednieje, ma tez mniejsza skrzynie. Ta grupa ROSNIE
w biegu: 103h 1-3 wsie przed Planem -> 25-32 na koncu, 103d 0-4 -> 22; mediana skrzyni tej grupy 2226 -> 462 zl, mediana utargu wozu netto 78.8 -> 13.2 zl/d (reszta wsi 179 -> 234
zl/d); wsi ze skrzynia < 1000 zl 80 przed Planem, 156 w dobie 108717. Prog surowy jest osiagalny: 69 ze 102 wsi ze skrzynia < 1000 zl ma kiese >= 1000 zl.
Przyczyna w grze (103h, ostatnia doba): z 41 biednych wsi 30 ma plon glowny 0 po wlaczeniu Planu (plon-wsi.csv: 8 wsi przed Planem, 104 od doby 108710, 187-198 na koncu), 19 nie
miala kursu wozu w ostatnim oknie (mediana utargu wozu netto biednych 37 zl wobec 1063 zl wsi z plonem); krolestwa: Zelazne Wyspy 14, Nocna Straz 9 (103d: 48 biednych, 23 z plonem 0,
26 bez kursu wozu; Zelazne Wyspy 12, Polnoc 7, Wolni Ludzie 6). To osobna praca (Plan / fach / handel wysp) - ta galaz jej nie robi; zadanie 1 zostaje NIE (uczciwy stan).

**Zmiana (po recenzji R1):**
- `tools/sprawdz_logi.py`: M3W-2 (m103wsie) i M5G5-1 (m105gosp5) - wiersz wiazacy jak w `5da5f304` (surowo "kiesa < 1000 zl", linia Przeplywy osad; M5G5-1 z DECYZJA dla biegu
  ze starego zapisu bez prologu). Miara "kiesa < min(1000 zl, skrzynia)" i rozklad A (pelna mala skrzynia) / B (bez zapasu) - tylko INFO w nowych wierszach M3W-2n / M5G5-1n
  (funkcja `m107_m3w2`). Dotychczasowe M3W-2r / M5G5-1r usuniete. (Pierwsza wersja tej galezi wiazala nowa miare - recenzja R1 slusznie: chowala spadek dochodu wsi.)
- `DsVillage.AkNote`: liczniki `_akFullSmall` (skrzynia < 1000 i skrzynia <= kiesa < 1000) i `_akSmallChest` (skrzynia < 1000), `AkPoorMix`; `TownFolk.DsAkum.cs`: za dotychczasowym
  napisem `m105 wsie biedne ... P z N wsi DS` (bez zmian - czyta go M5G5-2) dopisek `; m107: pelna mala skrzynia (skrzynia <= kiesa < 1000 zl) A, skrzynia ponizej 1000 zl S`.
- `VillageTakings.LordRelief` (POMIAR hipotezy "licznik podatku ujemny"): woz wsi DS ponizej skrzyni, a licznik pana w oknie bez przyrostu (got <= 0) - `DsVillage.NoteReliefNoTake`;
  segment `m105 gosp5` linii `Daniny wsi (DS)` dostaje `; bez ulgi - licznik pana bez przyrostu w oknie N (brak skrzyni X zl)`. Zmiana zachowania (ulga przed wyplata prac okregu, W3)
  dopiero po danych z autotestu.
- Wsie bez wozu (Akirys) - segment K1 w linii wozow (zadanie 2), tylko pomiar.

**Na logach bazowych (bez kodu m107):** M3W-2 / M5G5-1 / M7G-11 surowo "< 1000 zl" 103h 7.9% (NIE), 103d 8.7% (NIE; M5G5-1 - DECYZJA: bieg ze starego zapisu bez prologu); nowa miara (INFO) 103h 3.15%,
103d 5.00%.

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

**Poprawki po recenzji (te same klucze K2 / K3; klucz NIE - kod jak w `5da5f304`):**
- K3 (kod R1): `FastCut` dla zamku przy doplywie 28 dob <= 0 - bez szybkiej obnizki (`return false`), dalej `IncomeCut` z podloga (1 - mx) x F. Dotad firma zamku schodzila do 0
  i nie wracala nigdy (przeglad pomija F <= 0, podwyzka mnozy, FromZero tylko miasta; na koncu 103h 26 osad z firma bez doplywu - prawie same zamki).
- K3 (ekonomia R2): pamiec doplywu zamku w zapisie - `Export` dopisuje dla zamkow pola 24-25 `~NF~srednia` (tylko przy kluczu TAK), `Import` przy `a.Length >= 26` zapamietuje je
  (`NImpN`, `NImpAvg`), `CastleSample` przy pierwszej probce sesji (klucz TAK, pamiec pusta) wypelnia `Net[0..NF-1]` srednia z zapisu. Szybka obnizka zamkow (wymaga 28 dob) dziala
  wtedy od pierwszych dob sesji, nie od 29. (sesje Jeffa 12-24 doby). Starsza DLL czyta pola do 23 (`a.Length >= 24`) - nadmiar pomija; stary zapis bez pol - jak dotad.
- K3 (ekonomia R3): po obnizce i przeskalowaniu zaleglosci z kas (AL, AC) przyciete do max(0, rata po obnizce - 1 zl), ten sam stosunek pan / korona; przycieta kwota w licznikach
  "zaleglosci z kas zamkow zmniejszone z firma". Dotad przy czesci ludu FLf ~ 0 mnozenie zostawialo te sama liczbe rat - flaga zaleglosci trzymala, a zamek mial tylko ruch w dol
  (przeglad bez podwyzki, wymuszony -15%, na splate nic nie zostaje). Teraz flaga gasnie i przeglad kwartalny moze podniesc firme za zyskiem (+15% na kwartal). Skala - NIEPEWNE
  (dowod 10.64 -> 6.21 mln zl/rok pochodzi z bazy, gdzie flage trzymalo glownie ALf, ktore K2 usuwa) - autotest M7G-33 / M7G-34 / M5G-13.
- K2 (ekonomia R5, kod R2): ulamek raty ludu ponizej 1 zl (przy pelnej wplacie gF = floor) nie jest darowany ani liczony w "(N zamkow)" - przechodzi do nastepnej raty (`ALf = miss`,
  jak dotad); darowany tylko brak >= 1 zl (w calosci, ALf = 0). `_noLordAmt` bez darowanej raty ludu (`iL + iC + addLf`). Segment linii: "z oszczednosci podzamcza H zl" sumuje tylko
  ALf >= 1 zl (ulamki to nie zaleglosc) - M7G-32 (<= 1 zl) bez zmiany progu.

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
- `Import()`: `nb3` -> Import3 (niezaleznie od klucza); nb2 i nb1 czytane jak dotad - z jedna roznica (recenzja kodu R6): przy USZKODZONYM nb2 blad w czesci F= nie przerywa juz
  calego odczytu (dawniej wyjatek szedl do catch w Import i W / H przepadaly), tylko potkniecie `Watch.WatchImport2(F=)` i odczyt dalej. Ta sciezka nie zalezy od K6 (wspolny `Parse`),
  wiec K6 NIE nie przywraca dawnego zachowania przy uszkodzonych danych; zgodnosc nb2 -> odczyt -> nb2 co do bajtu (bench3) bez zmian.
- `NoticeBoard.Watch.cs` WatchDaily przy K6: koszt doby liczony PRZED eksportem; eksport (do linii) w pierwszej dobie sesji i co 10 dob, nb1 co 30 dob (i w pierwszej) - poza kosztem;
  linia `...; zapis arm_board N B; sklad zapisu nb3 (pomiar doby D): naglowek z przedrostkami i miastami (T) ..., stary format nb1 w tym samym stanie X B (p%); ...; koszt X ms; ...`
  (fragmenty czytane przez M102_LG_DAY bez zmian). K6 NIE - kolejnosc i napis jak dotad.
- `ArmouryBehavior.SyncData`: `NoticeBoard.ExportSave()` - przy K6 linia przy kazdym zapisie gry `Tablica (m107): zapis gry arm_board nb3 N B (sklad), proba odczytu zgodna TAK,
  eksport X ms; proby w sesji: zgodne a, niezgodne b; doba zapisu D` (pusty - `zapis gry arm_board pusty (0 B; ...); doba zapisu D`; wyjatek w Export - `zapis gry arm_board BLAD
  eksportu (0 B; ...)` zamiast "pusty" - poprawki recenzji kodu R3 i R4). WatchStartLine przy K6: `zapis arm_board nb3 (odczyt takze nb2, nb1; ...)`.
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
na tych samych liniach z segmentami m107), M7G-S2 test syntetyczny (dobry i zly bieg oraz bieg bez linii startu), M7G-0 linia startu z 6 kluczami TAK; wsie M7G-11 (M3W-2 surowo
"kiesa < 1000 zl" <= 5%, linia Przeplywy osad - po recenzji R1), M7G-11n (nowa miara - INFO), M7G-12 (straznik: pelna mala skrzynia MNK 28 dob <= +0.05 wsi/d), M7G-13 / 14 / 15 INFO;
wozy M7G-21 (H1 "cel chwilowy = osada" w 28 liniach srednio <= 0.5, najwiecej <= 2), M7G-22 (rozladunki > 0 przy ucieczkach, "bez wyjasnienia" <= 4 szt., NKS-8 TAK), M7G-23 (wsie
wstrzymane > 30 dob 0, > 0 - DECYZJA; po recenzji R4), M7G-24 (mediana linii Wozy <= 2 ms); zamki M7G-31 (format), M7G-32 (ALf zamkow >= 1 zl razem <= 1 zl od 14. doby sesji), M7G-33
(najwieksza zaleglosc zamku <= 4 raty), M7G-34 / 35 INFO; ziarno M7G-41 (segment w kazdym Planie; zbiezna >= 80% - mniej INFO), M7G-42 (ziarno W i E NA TARG od wlaczenia: mediana
kazdego rynku <= 1.06, najwiecej <= 1.05 x max(1, f)), M7G-43 INFO, M7G-44 (dopisek K5), M7G-45 (INFO: czas Planu wlaczenia i cieplych Planow po wlaczeniu osobno - po recenzji R7);
tablica M7G-51 (kazdy zapis gry <= 12288 B, proba zgodna, BLAD eksportu 0), M7G-52 (wczytanie nb3 = liczby z zapisu TEJ SAMEJ doby - po recenzji kodu R3; linie bez doby - ostatni
zapis przed wczytaniem), M7G-53 (mediana kosztu doby listow <= 1.0 ms, doby >= 1.5 ms <= 2%), M7G-54 (wczytanie nb2 -> nastepny zapis nb3 zgodny); M7G-9 ERROR nowych funkcji (po
recenzji kodu R4 takze `CartGuard.Guard(woz)`, `CartGuard.Unload`, `DsTowns.*`, `NoticeBoard.Export (`, `Watch.WatchImport2(`), zly format segmentu wozow, proby niezgodne i BLAD
eksportu 0. Klucz liczy sie jako TAK tylko z linii startu (recenzja kodu R5) - log sprzed m107: progi kluczy INFO; bez klucza werdykt daja M7G-11, M7G-9 i progi formatu / czasu /
poziomu przy obecnych liniach (M7G-12, M7G-24, M7G-31, M7G-33, M7G-41, M7G-44, M7G-53).

Zmiany innych grup: m103wsie M3W-2 i m105gosp5 M5G5-1 - wiersz wiazacy jak w `5da5f304` (surowo), nowe wiersze INFO M3W-2n / M5G5-1n (nowa miara i rozklad A/B); m106wozy WZ-1b -
przy segmencie m107 z K1 TAK prog 0 bez wyjatku DECYZJA (DECYZJE przy K1 przejmuje M7G-23), linia kontrolna M106_TEST["day"] z segmentem m107 (WZ-S / WZ-S2 OK); m102tablica L-8b -
`sklad zapisu nb[23]( \(pomiar doby D\))?:`. `sprawdz_narzedzie.py` OK.

**Na logach bazowych po poprawkach (bez kodu m107; wydruki scratchpad `m107/popr/progi`):** 103h - M7G-S/S2 TAK, M7G-11 7.9% NIE, M7G-11n 3.15% INFO, M7G-42 INFO (log sprzed m107 -
bez linii startu; liczby: mediana W 1.05 / E 0.98, najwiecej 1.13 - Westeros doba 108703), M7G-45 Plan wlaczenia 15.3 s / po wlaczeniu max 28.7 s, M7G-9 TAK, reszta INFO; 103d - M7G-11
8.7% NIE, M7G-11n 5.00%, M7G-42 INFO (W 1.02 / E 0.96, najwiecej 1.04), M7G-45 12.8 s / 20.4 s, M7G-9 TAK. M3W-2 103h 7.9% NIE / 103d 8.7% NIE (jak przed m107); M5G5-1 103h 7.9% NIE,
103d 8.7% DECYZJA (bieg ze starego zapisu bez prologu); m106wozy 103h WZ-S/S2 TAK, WZ-1b DECYZJA (bez segmentu K1 - jak dotad); m102tablica L-8 103d 12482 B NIE, 103h 5666 B; L-9 103d
2.93 ms, 103h 1.76 ms.

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
- Uscislenie po recenzji R7: "+9.6 s" w opisie wyzej dotyczy samego K4 w Planie 5 (11.3 -> 20.9 s); K4 + K5 w Planie 5 to 27.6 s (+16.3 s wobec bazy), Plan 4 przy K4 + K5 14.3 s wobec
  12.1 s (+18%). Przy cieplych Planach w grze do 28.7 s ryzyko przekroczenia NKS-2 (<= 30 s) jest realne. k fachu ziarna W = 1.00 we wszystkich wariantach - projektowane "1.00 ->
  0.3-0.6" NIE jest pokazane na symulatorze. Skutek uboczny K5 (nie bylo go w projekcie): k welny W 1.00 -> 0.75-0.79, lnu W 0.33-0.34 -> 0.28-0.29 (out_both5 wobec out_mktnull5 /
  out_mkt5) - mniejszy cel fachu dla wlokna zjadanego (zuzywanego) w okregach; do obserwacji w M7G-43 (welna, len NA TARG).
- Decyzja w tej pracy: K4 i K5 domyslnie TAK (zasada zadania - kazdy klucz domyslnie TAK) do autotestu. Regula autotestu (po recenzji R7): NKS-2 czytane osobno dla Planu wlaczenia
  i cieplych Planow po wlaczeniu (M7G-45); NKS-2 > 30 s albo M7G-42 bez poprawy wobec samego K4 -> NAJPIERW K5 NIE (brak zmierzonego zysku), potem - jesli dalej zle - K4 NIE.

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
  darowana w pierwszej racie (nie w Import - kolejnosc zdarzen jak dotad). `_arrPaid` bez darowania (before - forgOld); `_arrNew` bez darowanej raty bez pana. Po poprawce R5 ALf
  zamku trzyma tylko ulamek < 1 zl (przeniesiony) - flaga zaleglosci (AL + AC + ALf >= raty) go praktycznie nie widzi; `_noLordAmt` liczy rate bez czesci ludu darowanej.
- K3: zamki tylko przy `CastleCutOn` (Ds Castle Income Cut + Ks Farm Surplus Cap Net + kalibracja); `FastCut` wymaga pelnych 28 probek doplywu - po poprawce R2 pamiec doplywu ZAMKOW
  przechodzi przez zapis (pola 24-25 wpisu `f~`, tylko przy kluczu TAK; srednia wpisana jako NF probek - po wczytaniu NetAvg = srednia z zapisu, nowe probki wypieraja ja po kolei),
  wiec szybka obnizka, obnizka z dochodu i przeglad zamku dzialaja od pierwszych dob sesji; miasta - pamiec sesyjna jak dotad. Pamiec stosowana w `CastleSample` (pierwsza probka
  sesji), wiec CastleIncomeCut pierwszej doby po wczytaniu widzi jeszcze pusta pamiec (jak dotad). Doplyw <= 0 - bez szybkiej obnizki (R1). Zamki nie licza sie w `KsFolkIncome`
  (segment f5 miast bez zmian). Skalowanie i przyciecie AL / AC (R3) - tylko roszczenia (zloto bez ruchu); krok 5 (darowanie ponad DsFarmForgiveWeeks) bez zmian; przeglad wymuszony
  (Review) nadal nie skaluje AL / AC - jego obnizka nie gasi flagi, robi to dopiero najblizsza obnizka K3.
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
- M5G-13 w Biegu B przy granicy (prognoza 8-20 tys. przy progu 20 tys.); skala R3 (przyciecie zaleglosci zamkow i podwyzki przegladu po zgaszeniu flagi) - dowod 10.64 -> 6.21 mln
  zl/rok byl z bazy, w ktorej flage trzymalo ALf (K2 je usuwa), wiec przewidywanie M7G-33 / M7G-34 w Biegu B jest niepewne.
- R2: pamiec doplywu zamkow z zapisu - skutek w krotkiej sesji widac dopiero w Biegu C (wczytanie co 20 dob); Bieg B (160 dob bez wczytania) zawyza skutek K3 wobec gry Jeffa.
- K4 nie jest sprawdzony na symulatorze co do ziarna (zrzut 16.10 nie ma problemu - ziarno W NA TARG 0.98-1.00 juz bez K4); czas Planu z K4 + K5 (Plan 5 bramki 27.6 s) - NKS-2.
- K5: skutek uboczny na welne i len (k fachu W 1.00 -> 0.75-0.79, 0.33 -> 0.28) - M7G-43; zysk na ziarnie niezmierzony.
- Zad. 1 (wsie): M3W-2 / M7G-11 surowo zostanie NIE (ok. 7-9%) - m107 nie usuwa przyczyny (wsie bez utargu wozow po Planie); K1 moze dac najwyzej ulamek punktu.
- Akirys (wies bez wozu) - przyczyna nieustalona, tylko pomiar (M7G-15). Hipoteza "licznik podatku ujemny" - tylko pomiar (M7G-14).
- M7G-52 porownuje wczytanie z zapisem tej samej doby w TYM SAMYM logu - wczytanie w nowym procesie (nowy log) daje INFO; porownac recznie z linia zapisu poprzedniego logu (ta sama doba).

---

### Plan autotestu (gry nie uruchamialem - zakaz w tej pracy; ta sama zgoda co dotad: tylko gdy Jeff nie gra, kopia WSZYSTKICH logow Jeffa z dnia przed pierwszym biegiem, DLL z
`test/n107` tylko na czas testu (testy-103.ps1 / bieg-bitwa.ps1), potem przywrocone 103.2; zapisy Jeffa nietkniete)

Przed gra: build 0 bledow (5 modow przy scalaniu), gen_mcm, `sprawdz_narzedzie.py`, test `m107/bench3` (kod 0), bramka symulatora (wyzej).
- **Bieg A:** nowa kampania, prolog 230 dob z KS (jak 103h) + wczytanie +20 dob z zapisu konca biegu (odczyt nb3, ALf zamkow z zapisu = 0, pamiec wozow sesyjna, pamiec doplywu
  zamkow z zapisu - pierwsza linia DS po wczytaniu: szybkie obnizki zamkow mozliwe od 2. doby).
- **Bieg B:** 160 dob z zapisu (jak 103d; zapis kampanii z prologiem - liczy sie M5G-13; pierwsze wczytanie nb2 -> zapis nb3).
- **Bieg C (nowy, po recenzji R2):** zapis konca Biegu A, 80 dob z zapisem i wczytaniem co 20 dob (jak sesje Jeffa) - na nim oceniac M5G-13 i M7G-33 / M7G-34 (K3 w krotkich
  sesjach) oraz M7G-52 (kazde wczytanie nb3 wobec zapisu tej samej doby).
- **Bieg D (tylko gdy trzeba, po recenzji R7):** jak Bieg A z K5 NIE (sam K4) - gdy w Biegu A NKS-2 > 30 s albo M7G-42 NIE; porownanie M7G-42 / M7G-45 z Biegiem A.

Progi na wszystkich: `m107gosp` (calosc), `m106wozy` (WZ-1b = 0, WZ-6, WZ-7, WZ-8), `m103wsie` (M3W-2 surowo, M3W-1, M3W-3, M3W-7), `m105gosp4` (M5G-13), `m105gosp5` (M5G5-1 / 2),
`nks` (NKS-2, NKS-3, NKS-5), `npolnoc` (NKS-30b), `m102tablica` (L-1..L-10) + regresja jak test 103d (`m105kronika`, `m105miasta`, `m105drobne`, `m96bandy`); ERROR Armoury 0, kod wyjscia 0.
NKS-2 czytac osobno dla Planu wlaczenia i cieplych Planow po wlaczeniu (M7G-45).

| Prog | Bieg A (oczekiwane) | Bieg B (oczekiwane) | Bieg C |
|---|---|---|---|
| WZ-1b | 0 | 0 | 0 |
| M7G-23 (wsie wstrzymane > 30 dob) | 0 albo DECYZJA (blokady band) | 0 albo DECYZJA | - |
| M3W-2 / M7G-11 (surowo) | NIE, ok. 7-8% (przyczyna poza m107) | NIE, ok. 8-9% (przyczyna poza m107) | - |
| M7G-11n (nowa miara, INFO) | 3.0-3.5% | 4.6-5.0% | - |
| M5G-13 | ok. 1 tys. zl | 8-20 tys. zl (NIEPEWNE), nachylenie <= +100 zl/d | <= 20 tys., nachylenie <= +200 zl/d |
| M7G-32 / M7G-33 | 0 zl / <= 4 raty | 0 zl / <= 4 raty (NIEPEWNE) | 0 zl / <= 4 raty |
| NKS-3 | TAK (W grain 1.00-1.05) | TAK | - |
| NKS-2 (M7G-45) | Plan wlaczenia i cieple <= 29.5 s | <= 29.5 s | - |
| L-8 / M7G-51 | <= 10 KB, BLAD eksportu 0 | <= 10 KB | <= 10 KB |
| L-9 | <= 2.5 ms | <= 2.5 ms | - |

Niepowodzenie czesci -> poprawka albo ta czesc z kluczem domyslnie NIE (decyzja w raporcie; K4 / K5: najpierw K5 NIE); reszta moze isc dalej. Wgranie po udanym autotescie (stala
zgoda 09.10, z kopia).
