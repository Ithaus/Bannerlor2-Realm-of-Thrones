## 2026-10-22 (m106-wozy: woz zawsze wraca (W2) i fach wsi pod KS (P1) - diagnoza scratchpad `wozy/diagnoza-wozow.md`, projekt m106 "W2 i P1, plik po pliku")

**Mod:** Armoury (+ `tools/sprawdz_logi.py`, harness Planu `tools/ekonomia-symulatory-2026-10-17/kmoney`) | **Galaz:** `w-toku/m106-wozy` (od `a7a767e8` = n33 = WGRANIE 103.1)
| **Status:** DO SPRAWDZENIA (nic nie wgrane, gry nie uruchamiano, folderu gry, `test/` i zapisow Jeffa nie dotykano; autotest - plan na koncu)

Dwie czesci, szesc kluczy MCM (domyslnie TAK; kazdy NIE = sciezka kodu jak w `a7a767e8`): W2 - wozy wsi (drogi ladowe, brama wroga, pamiec tras, straznik zatkanego wozu,
dosypka przy wyjezdzie) i P1 - fach wsi pod kalibracja startu (KS). Nowa grupa progow `m106wozy`.

| Commit | Co |
|---|---|
| `f093bfcf` | klucze MCM: Village Cart Land Road Only, Village Cart Enemy Gate, Village Cart Route Risk, Village Cart Stuck Guard, Village Cart Top Up On Departure, Ks Village Trade Keep (gen_mcm) |
| `cd3f9624` | W2: MarketCarts.cs, MarketRoad.cs, nowe CartRisk.cs i CartGuard.cs, F4Pomiary.cs (H1), FibreToMarket.cs (odczyt licznika), ArmouryBehavior.cs (Reset, SyncData, nasluchy, doba, start) |
| `2a3722dd` | P1: nowy StartCalibration.Fach.cs, Core.cs (Shares, Equilibrium, RegaleStep, Finish), Plan.cs, Live.cs, Rule.cs, VillageByPeople.cs (BaseRate); harness kompiluje Fach.cs |
| `24fc2d41` | grupa progow `m106wozy` (WZ-S, WZ-S2, WZ-0..WZ-10, WZ-H1, INFO); harness h4-feedswiat: KS_FACH=<plon-wsi.csv> |
| `37ba8700` | CHANGELOG galezi |
| POPRAWKI (3 commity, sekcja nizej) | po recenzji i dwoch weryfikacjach (gra: R1-R4, D1-D6; logika: Z1-Z12) - fach (R1, R2), A7 (R3/Z4, Z5, Z10), straznik (R4, D1, Z6, Z8, Z9, Z11), licznik W1 (Z12b), podpowiedz MCM (D3), progi grupy (Z1, Z2, Z3, Z7) |

---

### POPRAWKI po recenzji (22.10; weryfikacje "gra" i "logika" - kazda uwaga PRAWDA poprawiona, NIEPEWNE w planie autotestu)

| Uwaga | Werdykt weryfikacji | Zmiana |
|---|---|---|
| R1 fach zabieral rece zbozu i serowi przed progiem wyzywienia (W bez pokrycia 8.0 -> 10.0%, Wolni Ludzie 37/43%) | PRAWDA (harness) | `Fach.cs` FachDonor: w okregu objetym progiem (InFeedScope - prog liczyl PairKcal i mieszanke bloku) dawca nie jest para mieszanki jedzenia bloku (FA, FB) ani para z wiecej kcal jedzenia (PairKcal, pelna praca) niz para typu |
| R2 dawcy oddawali po rowno - najlepiej platna praca szla na tani towar typu (sprzedaz wsi W -2%) | PRAWDA (fakty); poprawka recenzenta (`inc[k] < inc[kt]`) wylacza fach (Tarth 0 ryb) - odrzucona | `Fach.cs` FachBlock: dawcy od pary z NAJNIZSZYM dochodem (IncRaw; remis - kolejnosc par), kazdy tylko tyle, ile brakuje, ziemia 0.98 x powierzchni na kazdy ruch (wariant weryfikacji r1cheap) |
| R3 = Z4 powrot fachu w A7 bez porownania dochodow | PRAWDA | `Rule.cs`: nacisk powrotu tylko przy `fInc >= incCur` (ta sama miara we wszystkich trzech galeziach dochodu) |
| Z5 A7 porownywal produkcje z czynnikami gry (zima, spustoszenie, rok urodzaju...) z celem Planu bez nich; zywiec - Own ubija go na stol | PRAWDA | `Rule.cs`: w dobie kroku `fM` z `ComputeDistrict(ld, false, null)` (jak linie Planu); zywiec - na targ + zjedzone (`LiveDist.FachLive`, Live.cs) |
| Z10 blok powrotu wybierany raz, bez warunkow hamulca | PRAWDA (drobne) | `Rule.cs`: blok powrotu = najwieksza przydatnosc ziemi typu wsrod blokow bez prac okregu (KsWorksLock), z `fInc > 0` i `>= dochod biezacy`, bez blokady progu wyzywienia (FeedKeeps) i z miejscem na ziemi (ten sam test co przebudowa); miasto bez sygnalu KS - bez bloku |
| R4 rozladowany woz zostawal z rozkazem do miasta (gra mogla wyslac pusty woz) | PRAWDA (mala waga) | `CartGuard.cs` Home: po rozladunku `MarketRoad.Move(woz, dom)`; po 20 dobach od rozladunku "bez wyjscia" tylko, gdy gra wyslala woz ponownie (juki i rozkaz), inaczej koniec epizodu bez licznika |
| D1 swiezy rozkaz w domu bez wyboru - stary cel bez testu drogi | PRAWDA (brzeg) | `CartGuard.cs` Home: przy W1 stary cel bez drogi ladowej z domu -> `NearestReachableTown`; brak - bez rozkazu |
| Z6 licznik "w obcej osadzie > 30 dob" slepy (InSince zerowany po kazdym Act), sukces liczony przy przyjeciu rozkazu, a ucieczka gry trzyma woz | PRAWDA | `CartGuard.cs`: pole `FirstIn` (pierwsza doba w osadzie, Act go nie zeruje) dla `_y2` i przykladu; rozkaz z obcej osady - "do domu / do miasta" nastepnej doby, gdy woz wyjechal, inaczej "rozkaz odrzucony" (`PendAt`, `PendGo`) |
| Z8 H1 liczyl woz w zwyklej chwili przed wyjazdem | PRAWDA (drobne) | H1 tylko `juki && rozkaz && wiek > 5 dob` |
| Z9 Y3 ("w domu ponad 30 dob") liczyl pusty woz po rozladunku | PRAWDA (drobne) | `_y3` i "w domu z jukami ponad 5 dob" (z przykladem) tylko przy jukach i rozkazie |
| Z11 linia doby przy wszystkich kluczach W2 wylaczonych | PRAWDA (drobne) | `CartGuard.Daily`: wszystkie 5 kluczy NIE - bez linii doby (liczniki zerowane: `MarketCarts.TakeM106`, `CartRisk.SkipDay`) |
| Z12b licznik "odrzucone bez drogi ladowej" liczyl kazde miasto za morzem przy kazdym wyborze | PRAWDA (drobne) | `MarketCarts.Choose`: licznik tylko dla kandydatow, ktorych dotad nie odcinal test zasiegu (wlasne miasto, NaN) - czyli prawdziwy skutek W1 |
| D3 podpowiedz Top Up On Departure | PRAWDA (kosmetyka) | "with Village Cart Whole Store or Village Cart Full Load Far" (Settings.cs, gen_mcm) |
| Z1 fach a jedzenie Wolnych Ludzi (NKS-30b) | skutek PRAWDA, mechanizm recenzji NIE | po R1+R2 harness: Wolni Ludzie 0.79/0.76 (bez fachu 0.83/0.76, stary fach 0.64/0.59), Skagos 0.93/0.91 (0.91/0.91); grupa: WZ-7d = NKS-30b grupy `npolnoc` na tym logu (niespelnione - DECYZJA, Ryzyko P1-1); `npolnoc` w T1/T2 z bazami |
| Z2 prognoza WZ-4 45-60 bez pokrycia w harnessie | PRAWDA | prog `zero` 60/70 -> 135/135 (harness po poprawkach 126, bez fachu 186-190); tabela prognoz poprawiona |
| Z3 prognoza WZ-5 liczyla tylko zysk | PRAWDA | wartosc calego targu wsi w obie strony (`m106pop/ana.py`): Westeros razem -0.6%, wzrost > 2% w 254 wsiach, spadek w 198; WZ-5 70 -> ok. 74 (niepewne, moze sie pogorszyc); prog `poor` = baza 70 / 97 |
| Z7 WZ-6 obejmowal prawie kazda dobe (dosypka) | PRAWDA | WZ-6 tylko doby rozladunku straznika; opis bazy: 103e 0, 103d do 786 w dwoch dobach (przetwory miast) |
| D2 kurs od Choose, strata w drodze powrotnej nie liczona, strata wozu zawroconego na starej trasie | czesciowo PRAWDA | bez zmiany kodu: strata wozu zawroconego zostaje przy starej trasie (woz do celu nie dojechal - to sygnal ryzyka, np. przy Murze); strata w powrocie - zalozenie projektu (naglowek CartRisk.cs) |
| D4 `_st` straznika tylko w pamieci sesji | PRAWDA (fakt) | bez zmiany - sprawdzic w T2 (plan) |
| D5 odciecie Ub x Sv zaniza licznik "wybor zmieniony" | PRAWDA (log) | bez zmiany - INFO opisane nizej |
| D6 koszt fachu 1.2-1.4 s na Plan | punkt do pilnowania | bez zmiany - T1 (NKS-2); po poprawkach harness: Plan 1 16.0 s / 17 150 rund (bez fachu 28.4 s / 34 750), Plan 2 15.9 s / 15 950 (15.5 s / 17 500) |

**Test poprawek fachu w harnessie (`scratchpad wozy/m106pop/run_p.sh base fach`, wyniki `m106pop/out/`):**
- fach po poprawkach = wariant weryfikacji r1cheap **bit w bit** (CSV okregow identyczny, wydruk rozni sie tylko czasami i sciezka) - `m106pop/cmp.py`;
- klucz NIE = bieg weryfikacji ze zrodel 37ba8700 bez przelacznikow bit w bit (ta sama galaz czasu GradSecondRound: krok 5 rund 2 w Planie 1); 37ba8700 z kluczem NIE = a7a767e8 (wyzej) - sciezka NIE bez zmian;
- liczby (bez fachu -> stary fach -> po poprawkach; Plan 1 / Plan 2): W bez pokrycia 5.9/7.9 -> 8.0/10.0 -> **6.2/6.9**; E 2.5/3.1 -> 3.5/3.7 -> 2.9/3.3; Wolni Ludzie jedzenie 0.83/0.76 -> 0.64/0.59 ->
  **0.79/0.76**; Skagos 0.91/0.91 -> 0.83/0.84 -> 0.93/0.91; Nocna Straz 0.66/0.50 -> 0.46/0.35 -> 0.49/0.34 (INFO, odniesienie 0.35); sprzedaz wsi Westeros 184 429 -> 180 812 -> **186 040 zl/d**; rece w pracy domowej W
  30.0 -> 30.7 -> **29.4%** (koszt kroku 8 z Ryzyka P1-1 po poprawkach w rozrzucie bazy);
- fach w Planie 2: cel na targ w Planie 65% (dotad 71%), w pelni 243, czesciowo 238 (rece/ziemia 91, bez zysku 44, prog wyzywienia 103); rece do fachu 1.07 mln (dotad 2.13 mln; z: silk_plant 0.55,
  sheep_farm 0.22, trapper 0.07 mln - zboze i ser juz nie);
- WZ-4 (glowny towar 0 na targ, wsie z plonem przed KS): bez fachu 190 (base2 186) -> stary fach 103 -> **126** (fish 31, grain 26, grape 17, flax 14, hardwood 11);
- Tarth: ryby na targ 259 kg/d (0.7 szt./d; stary fach 590), winogrona 0, praca domowa 0.85 - WZ-4b nie przejdzie (DECYZJA - Ryzyko P1-1); Breaker's Bay: wartosc targu 53 -> 30 zl/d.

**Slowami gracza (po poprawkach):** "Wies trzyma swoj fach, ale nie kosztem chleba - rybak nie zostawia pola zboza ani obory, zeby lowic; do fachu idzie najpierw praca, ktora placi najgorzej."

gen_mcm: Armoury 2301 -> 2307 ustawien (6 nowych kluczy; RC 115, GT 44 bez zmian). Build Armoury: kod 0, 0 bledow (7 ostrzezen jak dotad). Harness kmoney (h4-feedswiat, h4-koszyk,
port5): build kod 0 (Fach.cs czysty C#). `sprawdz_narzedzie.py tools/sprawdz_logi.py`: dispatch, wstawki, def, grupy, kompilacja - OK. Nowe klucze nie istnieja w `Armoury.json` Jeffa -
dzialaja domyslne z kodu (TAK). Nowy klucz zapisu `arm_cart_risk` (starsza DLL go pomija; brak klucza = pamiec pusta).

---

### W1. Drogi ladowe (klucz `VillageCartLandRoadOnly`)

**Problem (103e/103d, linie "Zatkane wsie (przyklad)"):** Harbay [village_S2_3] i Volmark [village_S2_1] (Harlaw) - woz stoi na brzegu 165-170 dob, 41 jedn. od Lordsport (Pyke), ostatni powrot
w oknie 108703 / 108696, potem 0 kursow przez 25 okien. Pamiec drog gry (`settlements_distance_cache_Default.bin`) daje dla par wies - Lordsport 1e30 (brak drogi ladowej).
**Przyczyna:** `MarketCarts.Choose` przepuszczal wlasne miasto wsi miejskiej bez testu drogi (`isOwn`), `FibreToMarket` kierowal tam woz z welna / skorami bez testu drogi, `MarketRoad.TooFar`
uznawal za "bez drogi" tylko `d <= 0` (1e30 szlo sciezka "daleki targ" 110), a `RoutePrefix` bez wyboru oddawal woz latce BK - znow do Lordsport.
**Zmiana:** Choose - kandydat z droga >= prog BK (50000; obejmuje 1e30 i 1e8) albo <= 0 / NaN odpada, takze wlasne miasto (wlokno dziedziczy: `ownC == null`); TooFar - `noRoad` takze
dla >= progu BK; RoutePrefix - gdy nie ma wyboru, a Bound wsi nie ma drogi ladowej z domu: najblizsze miasto osiagalne ladem (niewrogie, nieoblezone, bez bramy wroga;
`CartGuard.NearestReachableTown`), przystanek w zamku pominiety, gdy zamek bez drogi; jednorazowo w pierwszej dobie sesji woz w polu, ktorego cel nie ma drogi ladowej z miejsca,
gdzie stoi - do domu (`CartGuard.Once`, zapisy z wozami juz stojacymi).
**Slowami gracza:** "Wozy z Harlaw nie probuja juz jechac na Pyke - jada do Ten Towers i wracaja."

### Brama wroga (klucz `VillageCartEnemyGate`)

**Problem (103e bitwy.log H3):** 129 wozow Wolnych Ludzi rozbitych (91 przez Straz, 90 w regionie The Wall); 103d: 59 (45). 14 z 18 wsi Wolnych Ludzi ma w zasiegu bogate Last
Hearth, do ktorego jedyna droga prowadzi przez zamek Strazy "The Wall".
**Przyczyna:** `MarketCarts.Hostile` sprawdzal tylko frakcje miasta celu.
**Zmiana:** `CartRisk.GateBlocked`: kandydat odlozony, gdy wroga twierdza F (zamek albo miasto frakcji w wojnie z frakcja wozu) spelnia d(zrodlo, F) + d(F, miasto) - d <= max(2, 2% d)
(wstepnie d(zrodlo, F) < d; droga ladowa z pamieci drog). Wynik wozu w domu pamietany (wies, miasto, wersja wojen - WarDeclared, MakePeace, zmiana wlasciciela osady, zmiana krolestwa
rodu); woz w polu liczony na biezaco. Gdy kazde miasto w zasiegu lezy za brama - wybor z odlozonych (wynik x przezywalnosc; licznik "wszystkie za brama").
Sprawdzenie projektu na pamieci drog (`w2_brama.py`): odrzuca 14/14 par wies Wolnych Ludzi -> Last Hearth, zostawia Hardhome, Thenn, Frostfang's Camp.

### Ryzyko trasy (klucz `VillageCartRouteRisk`)

**Zmiana (`CartRisk.cs`):** para "region wsi (StringId warowni) | miasto" -> {dojechalo A, kursow N} z ostatnich kursow: przyjazd = sprzedaz w miescie celu kursu (`MarketCarts.SellPrefix`;
postoj w zamku po drodze nie konczy kursu), strata = woz rozbity przez sprawce w kursie do miasta (`MarketRoad.OnPartyDestroyed`); rozwiazanie bez sprawcy nic nie liczy. Wygasanie
0.5^(doby/56) przy kazdym odczycie i zapisie. Przezywalnosc s = (A + 2 sw) / (N + 2), sw - swiat (bez danych 1) - bez wiesci s = sw dla wszystkich miast wozu, wiec wybor taki sam jak
bez pamieci (start neutralny). Choose: wynik i gorna granica x s (klucz wylaczony: x 1.0 - mnozenie dokladne, wynik bit w bit); czysta wycena (rezerwacje zlota G4, wiesc z drogi,
linia "Dowoz (wozy)") bez zmian. Zapis `arm_cart_risk` (SaveText; `m106r1~doba~Aw:Nw~region|miasto:A10:N10:doba!...`; przycinanie N < 0.1, > 364 dob bez kursu, > 1500 par);
klucz wylaczony - napis z zapisu oddawany bez zmian. Linia przy zapisie: "Wozy (m106): zapis pamieci tras arm_cart_risk - par N, znakow M, przyciete K."

### Straznik zatkanego wozu (klucz `VillageCartStuckGuard`)

**Problem:** 103d: 18 wozow stoi w domu z jukami i rozkazem (do 6296 szt. drewna; King's Valley 154 doby), wozy w obcych zamkach (Antler Village i Hauntwood w Hornfoot, Redrock
w Goldengrove) 10-80 dob; gra nie wystawi wsi nowego wozu, dopoki stary istnieje poza domem.
**Zmiana (`CartGuard.cs`, raz na dobe po CartTownExit):** woz w polu - postep = droga do celu krotsza o > 1 jedn. od najlepszej w kursie; bez postepu > max(2T, 3) dob (T - szacunek
kursu z Choose, bez wyboru 2 d(dom, cel)/tempo + czekanie) albo cel bez drogi ladowej z miejsca wozu -> do domu (gdy osiagalny ladem), inaczej najblizsze osiagalne niewrogie miasto bez
bramy; najwyzej 2 razy na kurs (kurs: od wyjazdu do powrotu albo sprzedazy w miescie), pomijany tej doby przy bitwie, tratwie i ucieczce (FleeToPoint). Woz w obcej osadzie (zamek,
nie-domowa wies; miasta - CartTownExit) > max(2T, 3) dob -> to samo (nie przy oblezeniu i bitwie; POPRAWKI Z6: "do domu / do miasta" liczone nastepnej doby, gdy woz wyjechal,
inaczej "rozkaz odrzucony"; "ponad 30 dob" od pierwszej doby w osadzie). Woz w domu z jukami i rozkazem (GoToSettlement, cel nie dom): po 5 dobach AI wlaczone i swiezy wybor
(`MarketRoad.PickTown`; bez wyboru - stary cel, przy W1 bez drogi ladowej najblizsze miasto ladem - POPRAWKI D1), po 10 dobach towar (bez koni) z jukow do magazynu wsi, skreslona
dosypka i rozkaz do domu (POPRAWKI R4); po 20 dobach od rozladunku, gdy gra wyslala woz ponownie, a on stoi - "bez wyjscia", koniec dzialan w epizodzie (niewyslany - koniec epizodu). Rozkazy przez `MarketRoad.Move` (trasa gry), kontrola jak w CartTownExit (GoToSettlement i cel = nadany; inaczej "rozkaz odrzucony"). Zamknieta ekonomia:
tylko rozkazy gry i towar miedzy jukami a magazynem tej samej wsi.
**H1 (tylko log; czemu woz w domu z rozkazem nie rusza):** liczniki warunkow gry `MobileParty.CheckExitingSettlementParallel` (AI wylaczone, Hold, cel chwilowy = osada), tratwa,
predkosc < 0.1 - tylko woz z jukami i rozkazem stojacy ponad 5 dob (POPRAWKI Z8; "w domu ponad 30 dob" tez tylko z jukami i rozkazem - Z9); co 5 dob 5 przykladow "Wozy (m106) przyklad" (zachowania, cel chwilowy, AI, tratwa, predkosc, waga/udzwig, juki, magazyn/W, najblizszy wrog w 5 jedn.); w przykladach
f4-pomiarow "stoi w domu" te same pola (przy kluczu).

### Dosypka przy wyjezdzie (klucz `VillageCartTopUpOnDeparture`, przy Village Cart Whole Store / Full Load Far)

**Problem:** `Choose` przy kazdym wyborze w domu przelewal do wozu reszte magazynu (`TopUp`), takze do wozu, ktory od dawna nie ruszyl (log: "doladowano 335-1133") - magazyn spadal
ponizej W i gra nie wysylala wozu ponownie.
**Zmiana:** przy wyborze w domu dosypka czeka (`_pending`: wies, cel, udzwig, doba); `MarketCarts.OnLeft` (CampaignEvents.OnSettlementLeftEvent - LeaveSettlementAction, watek glowny)
przy wyjezdzie z domu z celem poza domem robi `TopUp` z biezacego magazynu (ta sama regula: caly magazyn albo do udzwigu). Wyjazd z innej osady / bez celu / do domu, woz nieczynny,
poza domem albo wpis > 7 dob - dosypka przepada (licznik). Wycena wyboru i rezerwacja zlota z pelnym ladunkiem jak dotad.

### Linie logu W2

- start: `Wozy (m106): start sesji | Village Cart Land Road Only TAK | Village Cart Enemy Gate TAK | Village Cart Route Risk TAK (par z zapisu N, przezywalnosc swiata s, polowa po 56 dobach,
  start neutralny k=2) | Village Cart Stuck Guard TAK (...) | Village Cart Top Up On Departure TAK.`
- doba (zawsze, takze przy cichych logach; POPRAWKI Z11: przy wszystkich 5 kluczach W2 wylaczonych - bez linii): `Wozy (m106): dzien D | W1 drogi: ... | brama wroga: ... | ryzyko
  trasy: ... | straznik: ... | dosypka: ... | H1 w domu: ... | czas t ms (brama w wyborach g ms) | potkniecia N.` - segment klucza wylaczonego z dopiskiem " - WYLACZONE/A/Y" (liczby 0).
  "odrzucone bez drogi ladowej" (POPRAWKI Z12b) - tylko kandydaci, ktorych dotad nie odcinal test zasiegu (wlasne miasto wsi miejskiej, NaN): prawdziwy skutek W1, nie kazde miasto za morzem.
- przyklady co 5 dob: `Wozy (m106) przyklad: dzien D - i/n wies Name [id] - ...`.

---

### P1. Fach wsi pod KS (klucz `KsVillageTradeKeep`)

**Problem (diagnoza P1, plon-wsi.csv 103e):** po wlaczeniu KS (doba 108685) w 185 z 568 wsi glowny towar spadl do 0 (fish 44, grain 31, grape 24, flax 17, olives 14, hardwood 12,
wool 11) - Tarth wies rybacka bez ryb (winnice i bydlo), Red Watch wies drwali bez drewna. Ryby nie zniknely ze swiata (Plan daje na targ 2630 + 880 szt./d), lowia je bloki przy brzegu
wsi innych typow. A7 nie oddaje fachu (NKS-19b: 0 zmian, nacisk najwyzej 49 dob przy progu 60).
**Przyczyna:** rownowaga Planu ustawia zajecia blokow sama (logit dochodu), typ wsi nie wchodzi do rachunku; po wlaczeniu stawka KS zastepuje produkcje gry (`Live.cs` ComputeDistrict /
ProdStep).
**Zmiana (`StartCalibration.Fach.cs`, czysty C#):** fach jako DOLNA GRANICA rak przy zajeciu typu wsi w rownowadze Planu, jak prog "najpierw wyzywic siebie" (ApplyFloors), ktory ma
pierwszenstwo (biegnie po fachu):
- `FachStep` (raz na rownowage, po FloorStep): okregi z fachem (wies gry, glowny towar typu = towar KS wytwarzany przez zajecie typu; bez pol mieszczan i bez regale), k rynku i towaru
  = min(1, (popyt Planu - podaz regale i spoza tabeli - zjedzone w okregach fachu) / suma baz) - jedyna skala, wprost z podazy i popytu (zywiec - wobec rynku miesa: zywiec daje mieso
  x 0.5, popyt Planu nie ma zywca osobno), cel okregu T = k x baza gry na targ (`VillageByPeople.BaseRate` = baza_reguly pliku plon-wsi.csv x sigma x kg sztuki) + chec zjedzenia w okregu
  (NeedsOfKeep ludzi w domu x lepszy stol, jak KsDeliver.Own);
- `ApplyFach` (kazda runda, po logicie, przed progiem wyzywienia): okreg z produkcja towaru ponizej T przesuwa udzial rak bloku na pare zajecia typu - najpierw bloki, w ktorych typ
  daje najwiecej towaru na udzial, tylko gdzie dochod pary typu przed podloga > 0 (praca daje wiecej, niz kosztuje); w bloku (`FachBlock`, POPRAWKI R1/R2) dawcy od pary z najnizszym
  dochodem, bez pary mieszanki jedzenia progu i bez pary z wiecej kcal jedzenia niz para typu (okreg objety progiem), kazdy tylko tyle, ile brakuje, najwyzej do ziemi bloku (ha zajete
  <= 0.98 x powierzchni). Suma udzialow bloku = 1 - nic z niczego;
- RegaleStep: wiersze okregow typu tego zajecia regale (drwale, kopalnie, daktyle) najpierw - okreg dostaje min(sufit swoich wierszy, baza), razem najwyzej uzycie krainy (jedna skala na
  kraine), reszta uzycia krainy po staremu proporcjonalnie do wolnego sufitu; podaz krainy bez zmian;
- `FachFinish`: `KsResult.DistFach` (okreg -> towar, cel na targ po k, produkcja w Planie, flaga) i linia `KS: fach wsi (m106) plan doba N | klucz TAK | wsi z fachem X (regale Y) |
  k: ... | cel na targ A szt./d, w Planie B (C%), w pelni D, czesciowo E (rece/ziemia F, bez zysku G, prog wyzywienia H) | rece do fachu I tys. (z: ...) | regale: wsie typu najpierw J t/d
  z celu K t/d | najwiekszy niedobor (...) | czas fach Q ms.`
- stan zastosowany (`Live.cs`, `Plan.cs`): cel fachu (indeks towaru dostawy, kg/d na targ) z kazdego Planu (`FachToLive`, takze przy wlaczeniu); po wczytaniu do pierwszego Planu - brak
  celu (A7 jak dotad);
- regula A7 (`Rule.cs`, doba kroku): produkcja okregu bez czynnikow gry (POPRAWKI Z5; zywiec - na targ + zjedzone); blok z zajeciem typu jako glownym trzyma rece (Held = 0), dopoki
  praca typu daje wiecej, niz kosztuje, a okreg robi na targ <= 1.05 x cel; blok powrotu (najwieksza przydatnosc ziemi typu wsrod blokow bez typu jako glownego, ktore hamulec
  przepusci - POPRAWKI Z10) w okregu z niedoborem (< 0.95 x cel) dostaje nacisk ku typowi bez progu x1.25, gdy praca typu daje nie mniej niz biezaca (POPRAWKI R3) - dalej ten sam hamulec
  Jeffa (60 dob nacisku, jedna czesc okregu naraz, prog wyzywienia, ziemia, 5% rak regionu na sezon, przebudowa placona z kiesy wsi, przejscie 60 / 364 dob). Segment linii produkcji:
  `| fach (m106): bloki trzymane T, nacisk powrotu P, przebudowy do fachu zaczete S (od wczytania razem S0), wsi z niedoborem N (cel A szt./d, jest B)`.
**Slowami gracza:** "Wies rybacka lowi ryby, drwale scinaja drewno - kalibracja nie przerabia wsi z dnia na dzien na cos innego; zmiana zajec idzie tylko powoli, jak u ludzi."

---

### Grupa `m106wozy` (`tools/sprawdz_logi.py --grupa m106wozy <Armoury-*.log> [--tabele <katalog sesji>]`)

WZ-S self-test wyrazen (linie kontrolne jak w kodzie C#), WZ-S2 test syntetyczny (dobry i zly bieg przez te same funkcje progow - log, wsie-kiesy.csv, plon-wsi.csv, bitwy.log
w katalogu tymczasowym; oczekiwane werdykty: dobry 14 x TAK, zly 8 x NIE - zgodne). WZ-0 start z 5 kluczami TAK, linia doby bez luk, linia fachu przy kazdym Planie; WZ-1a w drodze
i w obcej osadzie > 30 dob 0; WZ-1b w domu > 30 dob 0 (DECYZJA przy przyczynie gry w H1); WZ-2 Harbay i Volmark >= 1 woz w kazdym bloku 28 dob od 28. doby i 0 przykladow z Lordsport;
WZ-3a Wolni Ludzie rozbici przez Straz <= 15/230 (nowa kampania) / 8/160 (zapis), WZ-3b wszyscy <= 50 / 30 (bitwy.log H3 i linie doby - wieksza liczba), WZ-3c Last Hearth (INFO);
WZ-4 wsie z glownym towarem 0 <= 135 / 135 (POPRAWKI Z2: harness po poprawkach 126; KS z zapisu - INFO), WZ-4b Tarth >= 10 szt./d; WZ-5 utarg wozow < 30 zl/d <= baza 70 / 97
(POPRAWKI Z3); WZ-4b i WZ-5 niespelnione przy WZ-4 TAK - DECYZJA (Ryzyko P1-1); WZ-6 bez wyjasnienia w dobach rozladunku straznika (POPRAWKI Z7) <= 4 szt. i NKS-8 TAK; WZ-7 grupa nks
na tym logu (NKS-2, NKS-3, NKS-19b TAK, NKS-5 <= baza + 3: 26 / 32), WZ-7b fach w Planie >= 50% celu, WZ-7c A7 (INFO), WZ-7d NKS-30b grupy npolnoc na tym logu (POPRAWKI Z1;
niespelnione - DECYZJA, Ryzyko P1-1); WZ-8 wycena miast
<= 1.3 x baza (44.0 / 35.7 ms/d) i linia m106 <= 2 ms/d; WZ-9 ERROR nowego kodu i potkniecia 0; WZ-10 arm_cart_risk <= 80 KB; WZ-H1, INFO (rozbite tabory "Dowoz:", NKS-8e).
Na logu 103e (sprzed galezi): WZ-S/WZ-S2 TAK, WZ-0 INFO, reszta INFO "log sprzed paczki" - bez wyjatku. Bazy z logow (sprawdzone): Wolni Ludzie przez Straz 91 / 45, wszyscy 129 / 59
(bitwy.log H3), wycena 44.0 / 35.7 ms (mediana), rozbite tabory 830 (103e).

---

### Test P1 w harnessie Planu (A/B; zrzut KS 16.10 + plon-wsi.csv 103e jako baza fachu; klucze jak w grze; 2 Plany, cieply start) - PRZED POPRAWKAMI (po poprawkach - sekcja POPRAWKI na gorze)

`scratchpad wozy/m106impl/run_h.sh base fach` (wyniki `out_base.out`, `out_fach.out`):
- zbieznosc i czas: bez fachu 48.3 s / 31 050 rund, z fachem 38.9 s / 25 150 rund (fach 1.2-1.3 s na Plan, ok. 6%); niezbiezne w 2. Planie 3 -> 4 (W fish, W whale_oil, W walrus_tusk,
  E wool);
- bilans NA TARG Westeros z fachem: zboze 0.99, ryby 0.97, mieso 0.98, ser 0.99, maslo 0.99, wino 0.99, oliwki 0.99, welna 0.99, len 0.99 (futra 0.94 - wyjatek projektu); Essos w pasmie
  jak bez fachu (pomarancze i marchew - wyjatki);
- fach: 481 okregow (regale 114), k: wszedzie 1.00 poza W/E flax 0.29, E cotton 0.48, E wool 0.92, W walrus_tusk 0.27; cel na targ 10 366 szt./d, w Planie 7 366 (71%), w pelni 289,
  czesciowo 192 (rece/ziemia 92, bez zysku 44, prog wyzywienia 56); rece do fachu 2.13 mln (z: wheat_farm 0.70, silk_plant 0.36, sheep_farm 0.36, cattle_farm:maslo 0.27 mln);
  regale: wsie typu najpierw 2 178 t/d z celu 2 305;
- **koszt fachu w Planie (krok 8):** sprzedaz wsi Westeros 184 488 -> 180 812 zl/d (-2.0%), rece w pracy domowej 29.3 -> 30.7%, "Plan bez pokrycia" W 8.0 -> 10.0%, E 3.3 -> 3.7%.
  Tarth: ryby na targ 0 -> 590 kg/d (1.6 szt./d), winogrona 3 614 -> 0 kg/d, praca domowa 0.63 -> 0.85; Breaker's Bay: len 0 -> 257 kg/d, praca domowa 0.44 -> 0.85. Mechanizm: krok 8
  liczy prace domowa z wartosci rak w cenach cienia (ryby W 0.24) - fach przy tanim towarze robi wies biedniejsza w Planie, wiec wiecej rak idzie na prace domowa i na targ zostaje
  mniej (patrz Ryzyko P1-1).

---

### Ryzyko / co sprawdzic (zasada 0 - kontrola calosci)

**Regresje (kto czyta / pisze ten sam stan):**
- Choose: wolajacy - `MarketRoad.PickTown` (RoutePrefix, drugi odcinek z zamku `CastleMarket.SellPrefix`), nowy `CartGuard.Home` (swiezy wybor po 5 dobach - liczy sie w "Dowoz (wozy)"
  jak kazdy wybor). Odciecie po gornej granicy: granica x przezywalnosc (spojne z wynikiem); bez wiesci ta sama przezywalnosc dla wszystkich - kolejnosc i odciecie jak dotad. Licznik
  "wybor zmieniony przez ryzyko" liczy tylko kandydatow wycenionych (moze zanizac - INFO).
- TopUp: dotad tylko Choose, teraz takze OnLeft; liczniki `_full / _fullUnits / _wholeKg` linii "Dowoz (wozy)" licza dosypke w chwili wyjazdu. Wycena, wiesc z drogi i rezerwacja zlota
  dalej z pelnym ladunkiem w chwili wyboru (jak dotad, gdy dosypka szla od razu); woz, ktory nie wyjedzie, trzyma rezerwacje do 30 dob (jak dotad).
- Wozy w zamkach: BK `VillagersCastleTick` odsyla co godzine woz w zamku do domu, CastleMarket trzyma straz 6/24 h - straznik m106 wydaje ten sam rozkaz (dom) dopiero po max(2T, 3) dobach
  i najwyzej 2 razy na kurs; przyczyna postoju (ucieczka przed Innymi - `MobilePartyAi` GetFleeBehavior trzyma woz w osadzie) zostaje - wypuszczenie z zamku otoczonego przez wroga to
  ryzyko straty (liczone w "rozbite").
- Wozy w miastach - tylko CartTownExit (straznik je pomija; kurs konczy sie sprzedaza).
- `MarketRoad.OnPartyDestroyed`: dopisane CartRisk.Lost / CartGuard.Forget / MarketCarts.ForgetPending (kolejnosc nasluchow MobilePartyDestroyed bez zmian).
- P1 - `KsCore.Shares` (oba ksztalty logitu), `Equilibrium`, `RegaleStep`, `Finish`: przy kluczu NIE `FachStep` wychodzi od razu (`FachEq` false), Shares nie pisze IncRaw i nie wola
  ApplyFach, RegaleStep liczy kwote wiersza starym wzorem (`FachRegale` zwraca null) - Plan bit w bit. KsDistrict stal sie `partial` (pola w Fach.cs). FeedStats (suma udzialow bloku = 1)
  liczy po fachu - fach zachowuje sume.
- P1 - A7 (f5-a7, sygnal KS): ceny cienia Planu z fachem sa inne (ryby i len nizej), wiec dochody sygnalu A7 tez; diagnoza A7 f4 (F4.cs) liczy nacisk po staremu - tylko INFO.
- P1 - NKS-19b (zmiany A7 w oknie D+22..D+49): powrot fachu zaczyna przebudowe po 63 dobach nacisku od D - poza oknem.

**Kolizje z innymi zmianami:** Choose dziela z m105 G4 (rezerwacje zlota - przezywalnosc mnozy tylko wynik wyboru, `best0` G4 tez x przezywalnosc, wiec "wybor zmieniony" G4 liczy tylko
rezerwacje); FibreToMarket - tylko odczyt licznika `NoOwnSeen`. CartTownExit, IslandRoads, CastleMarket - bez zmian. Prog wyzywienia (r4-polnoc / feed-swiat Slow) ma pierwszenstwo
przed fachem (biegnie po nim w tej samej rundzie).

**Spojnosc (zamknieta ekonomia):** W2 - zloto sie nie rusza (tylko rozkazy gry i wybor celu); towar tylko magazyn wsi <-> juki tego samego wozu (dosypka, rozladunek). P1 - fach
przesuwa tylko rece bloku miedzy jego zajeciami (suma = 1), produkcja dalej rece x tabela swiata x ziemia; regale - podaz krainy bez zmian; po wlaczeniu zmiany tylko hamulcem A7
(przebudowa placona z kiesy wsi do kasy targu, jak dotad).

**Cudzy kod (sprawdzone w dekompilacji 1.4.8):** `LeaveSettlementAction.ApplyForParty` -> `CampaignEventDispatcher.OnSettlementLeft` (takze dla AI z `CampaignTickCacheDataStore`
- watek glowny); `MobilePartyAi.EnableAi` (`_isDisabled = false`); warunek wyjazdu `MobileParty.CheckExitingSettlementParallel` (AI wylaczone, Hold, cel chwilowy = osada);
`VillagerCampaignBehavior` (nowy woz tylko gdy stary w domu, wyslanie przy magazynie >= W 15%/h, HourlyTickParty odsyla woz bez GoToSettlement); `MobileParty.Speed` (assert przy
nieczynnej partii - straznik czyta tylko czynne).

**Ryzyka P1 (do sprawdzenia w T1/T2):**
1. **Praca domowa kroku 8 zjada fach przy tanim towarze.** Po POPRAWKACH R1/R2 koszt w Planie w rozrzucie bazy (sprzedaz wsi W 186 040 wobec 184 429 zl/d, rece w pracy domowej
   W 29.4 wobec 30.0%, "Plan bez pokrycia" W 6.2/6.9 wobec 5.9/7.9%), ale w wsiach z tanim towarem zostaje: Tarth hw 0.63 -> 0.85, ryby na targ 0.7 szt./d, winogrona 0; Breaker's Bay
   wartosc targu 53 -> 30 zl/d; Westeros: wartosc targu wsi rosnie w 254 wsiach, maleje w 198 (razem -0.6%). WZ-4b (Tarth >= 10) nie przejdzie w zadnym wariancie z fachem; WZ-5 (prognoza
   70 -> ok. 74) i NKS-30b (WZ-7d; Wolni Ludzie harness 0.79/0.76 wobec 0.83/0.76, prog Jeffa 0.60, baza 103d 0.70 - T2 ok. 0.66-0.70) sa NIEPEWNE. Gdy WZ-4 przejdzie, a WZ-4b / WZ-5 /
   WZ-7d nie - DECYZJA (Jeff) (narzedzie pokazuje DECYZJA): fach przy tanim towarze to prawdziwy koszt "wies trzyma swoj fach" (zasada "zmiany powoli" wobec "z biezacego zysku");
   mozliwe dalsze kroki: krok 8 liczy V0 bez fachu, fach tylko do poziomu, przy ktorym wies nie biednieje, albo regale-first bez Wolnych Ludzi.
2. Czas Planu: harness po poprawkach - szybciej (Plan 1 16.0 s / 17 150 rund wobec 28.4 s / 34 750 bez fachu; Plan 2 15.9 s / 15 950 wobec 15.5 s / 17 500); fach 1.2-1.4 s na Plan
   (NKS-2 max 27.1 s w 103e) - czas Planu z fachem nie jest suma (mniej rund), pilnowac NKS-2 w T1 (D6). A7: `ComputeDistrict` bez czynnikow dla okregow fachu w dobie kroku (co 7 dob).
3. Zbieznosc: fach to zalamania w rownowadze; harness - 4 niezbiezne zamiast 3, bilans NA TARG w pasmie. Pilnuje NKS-3.
4. Darzu i wsi zbozowych P1 nie naprawi (okreg zjada swoje - w harnessie najwiekszy niedobor to wsie zbozowe: cel 152 szt./d, na targ 0).
5. Fach dzieli towar, nie dodaje go (k <= 1): ryby przechodza od innych wsi do rybackich.
6. Zywiec (bydlo, swinie): k wobec rynku miesa i chec zjedzenia 0 (Own zjada zywiec dopiero po dziczyznie i wielorybie) - A7 porownuje produkcje (na targ + zjedzone, POPRAWKI Z5).
7. Fach a prog wyzywienia (POPRAWKI R1): dawcy chronieni progiem -> flaga "prog wyzywienia" w linii fachu 56 -> 103 okregow, cel fachu w Planie 71 -> 65% (WZ-7b >= 50%).
8. Nocna Straz (NKS-30b INFO, odniesienie 0.35): harness 0.50 -> 0.34 w Planie 2 (bez fachu -> po poprawkach) - INFO, ale patrzec w T1/T2.

**Ryzyka W2:**
1. Falszywe trafienia bramy w duzych wojnach (projekt: przy wojnie ze wszystkimi obcymi kulturami 21% par) - wyjscie awaryjne: wybor za brama, licznik "wszystkie za brama".
2. Zimny start przezywalnosci (pierwsze straty, zanim pamiec sie nauczy); klucz regionu laczy rozne drogi.
3. Nowy klucz zapisu (starsza DLL go pomija; limit 1500 par ~ 50-70 KB w kawalkach SaveText).
4. Petla "nowy cel i znow stoi": najwyzej 2 dzialania na kurs, potem "bez wyjscia".
5. Rozladunek w domu daje magazyn >= 1.5 W (wies stoi do wyjazdu wozu - godziny); jesli H1 to waga / predkosc, caly magazyn wroci przy wyjezdzie (dosypka) i woz moze utknac w polu -
   wtedy decyzja o limicie ladunku przy Village Cart Whole Store (poza m106).
6. Gdy OnSettlementLeft nie przyjdzie, dosypka przepada - liczniki "przepadla" i "Zatkane wsie".
7. Klucz Stuck Guard wylaczony = brak pamieci i dzialan (linia z zerami) - pomiaru "bez postepu" przy wylaczonym kluczu nie ma.
8. POPRAWKI R4 - rozkaz do domu po rozladunku: woz w domu z GoToSettlement = dom; `VillagerCampaignBehavior.HourlyTickParty` nic nie robi przy celu, ktory nie jest miastem,
   `ThinkAboutSendingItemToTown` (woz w domu) laduje i wysyla przy magazynie >= W (15%/h) - sprawdzone w dekompilacji 1.4.8 (weryfikacja "gra"). Pusty kurs nie grozi.
9. POPRAWKI Z6 - woz w obcej osadzie, ktory przyjal rozkaz, ale stoi (ucieczka gry przed wrogiem w poblizu), liczy sie teraz jako "rozkaz odrzucony" nastepnej doby, a "ponad 30 dob"
   (WZ-1a) go widzi - WZ-1a moze pokazac > 0 tam, gdzie dotad byl slepy.

**Odstepstwa od projektu (decyzje Claude przy implementacji):**
- k zywca wobec rynku miesa (popyt Planu nie ma zywca osobno - z Dm zywca k byloby 0 i fach bydla / swin nie dzialalby); chec zjedzenia zywca 0.
- "od D razem S0" w segmencie A7 -> "od wczytania razem" (licznik sesji - bez zmiany formatu zapisu arm_ks).
- Linia doby m106 pisana zawsze (takze przy cichych logach Jeffa - linia czytana przez test), segment klucza wylaczonego z dopiskiem "- WYLACZONE".
- Straznik przy wylaczonym kluczu nie mierzy (wylaczenie = kod jak dotad).
- WZ-3: wieksza z liczb bitwy.log H3 i linii doby (linia doby pokazuje do 8 par na dobe).
- Root `CHANGELOG.md` i `docs/STAN-PRAC.md` - bez zmian w galezi (jak galezie m105-*: wpis przy scaleniu do n-calosci; STAN-PRAC prowadzi glowna sesja).

### Plan autotestu (nie uruchamiany - zakaz uruchamiania gry w tej pracy; ta sama zgoda co dotad: nie gdy Jeff gra, logi Jeffa skopiowac przed pierwszym biegiem)

0. **Statyczny - zrobione:** build kod 0; gen_mcm; sprawdz_narzedzie OK; WZ-S / WZ-S2 TAK; harness A/B (wyzej); przeglad: kazda nowa sciezka pod `if` klucza ze starym kodem w galezi NIE
   (W1 `landOnly`, brama `gateOn`, ryzyko `riskOn` / `CartRisk.On`, straznik `on`, dosypka `s.VillageCartTopUpOnDeparture`, fach `x.In.FachOn` / `fach`). **Plan z kluczem fachu NIE =
   a7a767e8 bit w bit** (harness h4-feedswiat zbudowany ze zrodel a7a767e8 wobec tej galezi bez KS_FACH, 2 Plany: plik okregow `KS_OUT` identyczny co do bajtu, wydruk rozni sie tylko
   czasami w linii korytarzy - tak samo jak dwa biegi a7a767e8). Uwaga: harness (i Plan w grze) ma galaz zalezna od czasu - druga ciepla runda gradientu tylko przy < 15 s
   (`GradSecondRound`), wiec dwa biegi tego samego kodu moga sie roznic (pierwszy bieg "base" mial 2 rundy kroku 5, powtorka 1); A/B fachu wyzej porownane przy tej samej galezi (2 rundy
   kroku 5 w obu). Do zrobienia przed biegiem: harness z KS_FACH na zrzucie Planu nowszym niz 16.10 (gdy bedzie).
   **POPRAWKI - statyczny, zrobione:** build kod 0 (0 bledow, 7 ostrzezen jak dotad); gen_mcm (Armoury 2307 - tylko podpowiedz D3); sprawdz_narzedzie OK; WZ-S / WZ-S2 TAK (zly bieg:
   WZ-4b DECYZJA przy WZ-4 TAK); test WZ-7d / WZ-4b przez wstrzykniete wiersze npolnoc (`m106pop/t_wz7d.py`: TAK / DECYZJA / INFO); grupa na 103e bez wyjatku; harness: fach = r1cheap
   weryfikacji bit w bit, klucz NIE = 37ba8700 bit w bit (`m106pop/cmp.py`); kazda nowa sciezka pod kluczem (fach `FachEq`/`fach`, straznik `on`, W1 `landOnly`; Z11 - wszystkie klucze NIE).
1. **T1: prolog 230 dob** (nowa kampania, flagi jak w 103e: `armoury-ks-auto.flag`). Grupy: `m106wozy`, `nks`, `npolnoc`, `m103wsie`, `m105gosp4`, `m105gosp5`, `m105miasta` - porownanie
   z bazami 103e (scratchpad `wozy/m106/nks_103e.txt`, `npolnoc_103e.txt`, `m103wsie_103e.txt`, `m105gosp4_103e.txt`, `m105gosp5_103e.txt`, `m105miasta_103e.txt`): nie gorzej niz nks
   17/114 nie przeszlo (NIE 12, ETAP 5), npolnoc 0 NIE (NKS-30b Wolni Ludzie 0.82, Skagos 0.96), m103wsie 1 NIE, m105gosp4 3 NIE, m105gosp5 1 NIE, m105miasta 1 NIE. Szczegolnie: M3W-7 /
   M5G-1b i WZ-7d (Ryzyko P1-1), WZ-4 / WZ-4b / WZ-5, WZ-3, WZ-2, NKS-2 (czas Planu z fachem - D6).
2. **T2: 160 dob z zapisu Jeffa** (ten sam co 103d; KS wlacza sie w biegu ok. +36 dob). Te same grupy wobec baz 103d (nks 18/114, npolnoc 1 NIE - NKS-30m; NKS-30b Wolni Ludzie 0.70,
   Skagos 0.92, m103wsie 2 NIE, m105gosp4 3 NIE + 1 DECYZJA, m105gosp5 1 NIE + 1 DECYZJA, m105miasta 1 NIE). Szczegolnie WZ-1b i H1 (King's Valley i 17 pozostalych wozow w domu), WZ-3,
   WZ-7d (Wolni Ludzie z bazy 0.70 - najblizej progu 0.60). D4: pamiec straznika `_st` jest tylko w sesji - po wczytaniu zapisu progi bez postepu licza sie od nowa (do 2T opoznienia);
   sprawdzic, czy WZ-1a/1b schodza do 0 w czasie biegu.
3. **T3: 120 dob z zapisu z KS juz wlaczonym, zrobionego bez m106** (ostatni zapis biegu T1-bazy albo 103e). Sprawdza: jednorazowe zawrocenie Harbay i Volmark w pierwszej dobie
   ("jednorazowo zawrocone" >= 1, WZ-2 od 28. doby), powrot fachu przez A7 (WZ-7c: nacisk powrotu, start przebudow ok. doby 63, glowny towar > 0 w okregach z niedoborem ok. doby
   100 - WZ-4 INFO trend), straznika na wozach juz stojacych (WZ-1a/1b do 0 w ok. 2T).
4. Gdy cos nie przejdzie - wylaczyc klucz tej jednej czesci i powtorzyc bieg. Wgranie dopiero po T1-T3 i po odbiorze progow.
5. **NIEPEWNE z weryfikacji (do odczytu w T1-T3):** WZ-4 na koncu T1 (powrot fachu przez A7 po ok. 63 dobach moze odjac kilka wsi); WZ-5 (prognoza 70 -> ok. 74); WZ-7d / NKS-30b
   Wolni Ludzie w T2; Tarth (WZ-4b - DECYZJA Jeffa); ile razy woz w domu lapie sie w H1 po zmianie Z8; czy po rozladunku (R4) gra wysyla woz w ciagu godzin (magazyn >= 1.5 W) -
   "bez wyjscia" w domu powinno zostac 0; Z5 - liczba nacisku powrotu w zimie (WZ-7c) przy produkcji bez czynnikow; D5 - "wybor zmieniony przez ryzyko" zanizony (INFO).

### Przewidywane wyniki (z projektu i harnessu - nie z biegu; po POPRAWKACH)

| Prog | Baza 103e / 103d | Przewidywane |
|---|---|---|
| WZ-1a (w drodze i w obcej osadzie > 30 dob) | Harbay, Volmark 165-170 dob | w drodze 0; w obcej osadzie (POPRAWKI Z6 - licznik juz nie slepy) wozy trzymane ucieczka gry w zamku otoczonym przez wroga moga zostac > 0 (Hornfoot) - NIEPEWNE |
| WZ-2 (Harbay, Volmark) | 0 kursow przez 25 okien | kurs co 7-14 dob do Ten Towers (maly utarg - mala nadwyzka KS na Wyspach) |
| WZ-3a / WZ-3b (Wolni Ludzie) | 91 / 129 (230 d), 45 / 59 (160 d) | prawie 0 przez Straz; wszystkie <= 50 / 30 |
| WZ-4 (glowny towar 0) | 186 / 184 | harness po poprawkach 126 (bez fachu 186-190; stary fach 103; prog 135); koniec T1 NIEPEWNY (A7) |
| WZ-4b (Tarth) | 0 | harness 0.7 szt./d (praca domowa 0.85, winogrona 0) - nie przejdzie: DECYZJA Jeffa (Ryzyko P1-1) |
| WZ-5 (utarg < 30 zl/d) | 70 / 97 | ok. 74 (wartosc calego targu wsi w obie strony; niepewne, moze sie pogorszyc - W2 moze czesc odrobic; prog = baza) |
| WZ-7d (NKS-30b Wolni Ludzie / Skagos) | 0.82 / 0.96 (103e), 0.70 / 0.92 (103d) | harness: -0.04 / 0.00 w Planie 1, 0.00 w Planie 2 -> T1 ok. 0.78-0.82, T2 ok. 0.66-0.70 (prog 0.60) |
| WZ-8 (wycena) | 44.0 / 35.7 ms/d | bez wiesci tak samo; brama w wyborach w domu z pamieci (wersja wojen) |
| M3W-7 / M5G-1b (bez pokrycia W) | 8.0% / 5.9% | harness po poprawkach 6.2/6.9% wobec 5.9/7.9% bez fachu - w rozrzucie bazy (stary fach +2 pp) |
