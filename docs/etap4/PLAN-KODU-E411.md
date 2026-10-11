# PLAN KODU - paczka E4/11 (193, PLAN 4.5, 4.6): ksiega wiosek pobocznych, plon okregu z pracy wiosek, zywnosc warowni od ludzi, menu okregu

Status: SAM PLAN (11.10.2026), poprawiony po krytyce (K1-K9, rozdz. 14). Kod, gra, ustawienia, zapisy nietkniete; nic nie budowane, nic nie commitowane.
Wiazace: `docs\etap4\PROJEKT-ETAP4-KSIEGA-LUDZI-ZIMA-2026-10-10.md` v2 (dalej P; E4/11 = P :529-532, R8/R9 :154-155, "odbudowa 1 rok = linia R8;
wioski poboczne tak samo" :186, prog etapu :547, koniec etapu :553-554, 5.1-5.4), `docs\etap4\KRYTYKA-E4.md` (KR: U13 rok od ostatniego najazdu,
U21 menu, U24 wywolania nie miedzy zdjeciem a dopisaniem, U25 wlasciciele latek, U26 latki wpiete zawsze), decyzje Jeffa w `docs\STAN-PRAC.md`:
07.10 ok. 11:10 "kazdy obrazek to WIOSKA Z NAZWA ... plonie na mapie ... rozstawienie NIE losowe" (SP:425-429), 07.10 ok. 15:10 "wiosek postawmy tyle,
ile ludnosc danego obszaru" - 1 wioska = gromada osad po 250 ludzi (SP:430-433), 07.10 "wgraj mi wioski" -> W2 wgrane, plik wedlug ludnosci P = 4000
(SP:387-399, :643-648), 08.10 "bierzemy wszystkie 8 do wiosek" (pkt 7 pamiec wojny po 108-113, pkt 8 menu okregu z lista wiosek - SP:377-379),
09.10 (4) "ODBUDOWA SPALONEJ WIOSKI: 1 ROK (nie 2-3) - dotyczy wiosek pobocznych (kazda ma swoich ludzi i swoja czesc plonu okregu) i glownej" (SP:760),
09.10 L "napisy na mapie tylko nad SPALONYMI wioskami" (SP:846 - etap 7.9), 11.10 (4) "spalona wioska wraca rok od ostatniego najazdu - TAK" (SP:1137),
07.10 "spladrowana wies produkuje wedlug tego, co zostalo" (SP:459-460).
Dokumenty wiosek: **WM** = `docs\PROJEKT-WIOSKI-NA-MAPIE-2026-10-07.md` (rozdz. 3, 6, 7, 9, 13), **RB** = `docs\PROJEKT-RABUNEK-OSADA-PO-OSADZIE-2026-10-07.md`
(pkt 1, 8, 10 - etap 7.11), **A02** = `docs\audyt-2026-10-09\02-wies-produkcja-krainy.md` (1.3, 1.4, A2-A5, L4, L12), **GL** = `docs\PROJEKT-GLOD-2026-10-07.md` 1.3,
**WW** = `docs\WIECEJ-WSI-WYKONALNOSC-2026-10-07.md` rozdz. 1. Wzor formatu: `docs\etap4\PLAN-KODU-E47.md`.

Skroty miejsc:
- **B:** = kod w grze, zbior 31 (`git -C scratchpad\e4\46 show dfb12df:Armoury/src/<plik>`; WGRANIE 31, Armoury cf40fefa) - linie z tego drzewa.
- **E46:** = `scratchpad\e4\46` @ 0cd60f1 (galaz `w-toku/e4-6`, Famine.cs; w recenzji - linie moga sie przesunac), **E47:** = `scratchpad\e4\47` @ 61f75d7
  (`w-toku/e4-7`, PeopleMenu.cs, FreeHands.cs). Kopie plikow do tego planu: `scratchpad\e4\411\`.
- **G:** = `scratchpad\dec32\cs_full\` (TaleWorlds.CampaignSystem 1.4.8), **BK:** = `scratchpad\e4tools\bkfull\`, **ROTd:** = `scratchpad\e4\dec\jedz\ROT.all.cs`.
- **TSV** = `Modules\Armoury\ModuleData\arm_map_villages.tsv` w grze (to samo w repo dfb12df `Armoury/ModuleData/arm_map_villages.tsv`).
- Baza liczb: bieg zbioru 31 A (`scratchpad\kopia-z31-A\Armoury-2026-10-11_08-09-11.log`, 120 dob) = **z31A** (srednie z linii, skrypt w tej sesji).

---

## 0. Streszczenie decyzji planu (co wykonawca robi, czego nie)

1. **Skad wioski poboczne: z pliku TSV, nie ze sceny ROT i nie od nowa z ludnosci.** Scena ROT nie ma zadnych punktow wiosek pobocznych - ma tylko
   571 wsi gry (WW rozdz. 1: `ROT-Map\ModuleData\settlements.xml`, scena 53 550 encji, wies = 10.9 encji). Wszystkie wioski poboczne to wynik naszego
   generatora offline (drogi z terrain.bin, rzeki ze scene.xscene, navmesh, flora - WM rozdz. 2), wybrany przez Jeffa wariant "wedlug ludnosci" P = 4000
   (SP:393) i juz wgrany (W2 v4/v5, SP:643-648). TSV w grze: **2 446 wiosek w 425 okregach** (146 okregow bez wiosek - Zelazne Wyspy, Qarth, step,
   Skagos, Straz; WM 1.7), kolumny `id village_id name x y road_deg settlements order kind uid class side level road_dist toward_village neighbors
   front_deg half_len half_wid face model`; `settlements` = 16 (1 981 wiosek), 27 (280), 8 (35), 14-15 (114), 19-26 (36) - razem 42 092 osad = 10.52 mln
   przy 250 ludziach (A02 1.3: 25.1% ludzi wsi); wiosek na okreg 1-18 (mediana 5); `order` 0..n-1 w kazdym z 425 okregow, `road_dist` w 2 446,
   `neighbors` w 2 398 [pomiar TSV w tej sesji]. **Liczba i polozenie wiosek - stale z pliku** (generator offline, gra nic nie losuje - WM 2.10).
2. **Ludzie wiosek - z ksiegi, nie z pliku.** Dzis dymek pokazuje `settlements x 250` na sztywno (B:MapVillagesView.cs:4596; A02 L4). Odtad:
   osada = ok. 250 ludzi (RB pkt 1); okreg (wies gry + jej wioski) ma **n = max(1, round(L0 / 250)) osad**, L0 = ludzie okregu (w domu + uchodzcy E4/5)
   przy pierwszym odczycie po kalibracji, **n zamrozone w zapisie**; wielkosc osady S = L / n plywa z ludnoscia. Wioska w ma n_w osad = jej kolumna
   `settlements`; wies gry (glowna wioska okregu) ma reszte, **co najmniej tyle, co najwieksza wioska** - gdy okreg za maly, wszystkie wioski
   pomniejszone w tej samej proporcji (WM rozdz. 3; wzor 3.2). "Gromada po ok. 250 ludzi" z zadania = osada; wioska poboczna = gromada osad
   (16 osad = ok. 4 000 ludzi przy S = 250).
3. **Jak wioska plonie przy najezdzie (gry, BK, AI, Innych, gracza): proporcja trafionych z E4/5, nie krok "osada po osadzie" z 7.11.** Kazdy
   `Devastation.Strike` (rabunek i zerowanie armii, B:Devastation.cs:216-258) zamienia trafionych na spalone osady: **dB = trafieni x n / L_przed**
   i rozdziela je **lancuchem palenia** (WM 6.2): pierwsza wioska sesji od strony wroga, potem najblizsze, wies gry na koncu; kazda do pulapu
   `DevastationMaxPercent` (90%) swoich osad - ten sam pulap co Room E4/5 (B:Devastation.cs:134, :167-182). 7.11 podmieni potem tylko ZRODLO
   osad (krok po kroku), ta sama funkcja `VillageBook.Burn` - miejsce zostawione (rozdz. 11).
4. **Zadnego ruchu ludzi i hearth w E4/11.** Ksiega wiosek trzyma tylko mape pustych osad (b_w - gdzie w okregu sa puste osady) i n. Ludzi wiosek
   liczy z PeopleBook: w domu_w = DOM x (n_w - b_w) / (n - B), poza domem_w = konto UCHODZCY rozdzielone wedlug b_w. **Suma po wioskach = DOM i konto
   okregu z definicji** (zamknieta ksiega, kontrola w linii). Bez `PeopleBook.Move`, bez nowego konta - wlascicielem ludzi zostaje PeopleBook (U23).
5. **Dwie pule pustych osad na wioske**: b_r (rabunek i zerowanie) i b_f (opuszczone z glodu, E4/6) - lustro podzialu konta z E4/6 (FamN, recenzja
   E4/6 R1-1: czesc z glodu wraca rok od odzyskania wsi, reszta rok od ostatniego najazdu). Puste osady maleja **proporcjonalnie do ubytku swojej
   czesci konta** (powrot, smierc uchodzcow, podloga 10), z wagami odbudowy wezel 1.5 / linia 1.0 / ubocze 0.5 / wies gry 1.5 (WM 6.4);
   **konto 0 -> wszystkie b = 0**. Bo E4/5 sprowadza wszystkich zywych uchodzcow najpozniej 364 doby po ostatnim najezdzie w regionie
   (B:Devastation.cs:615-670, U13), **kazda spalona wioska wstaje najpozniej w rok** - decyzja Jeffa SP:760, SP:1137 bez osobnego zegara. Wyjatki nazwane (nie bledy
   E4/11): oblezenie regionu albo miejsca pobytu i brama E4/6 "wies nie przyjmuje" wstrzymuja cale konto (E46:Devastation.cs:656-659) - rocznica rozroznia je przez
   `Devastation.StopOf` (Z3, krytyka K5).
6. **Stany wioski:** stoi / czesciowo pusta / **spalona (b_r >= 50% osad - decyzja Jeffa, stala w kodzie, WM 10)** / opuszczona (b >= 50%, z glodu) /
   w odbudowie / odbudowana (zdarzenie) / **plonie** (nakladka: dostala osady w trwajacym rabunku). Histereza 3%.
7. **"Plon okregu = suma pracy wiosek" (4.6) liczy sie juz przez czynnik E4/5 - E4/11 NIE dodaje drugiego mnoznika.** Przy elastycznosci 1.0
   `YieldFactor = dom / (dom + uchodzcy)` (B:Devastation.cs:451-472) jest dokladnie suma po wioskach (pracujacy_w / ludzie_w). E4/11 tylko przypisuje
   plon wioskom (udzial_w = czynnik okregu x w domu_w / DOM; menu, linia). Towar (MaterialLaw.ProdPostfix x YieldFactor x FreeHands L, B:MaterialLaw.cs:196,
   E47:MaterialLaw.cs +7) i zywnosc wsi (FoodPostfix E4/5 i E4/7) bez zmian.
8. **"Zywnosc warowni tak samo" (4.6) - jedyna nowa latka ekonomii.** Dzis warownia dostaje od kazdej swojej wsi `(poziom + 1) x 6` Food na dobe tylko w stanie
   Normal, 0 w kazdym innym (G:...GameComponents\DefaultSettlementFoodModel.cs:66-73) - poziom, nie ludzie. Postfiks na `CalculateTownFoodStocksChange`
   dodaje korekte: wies Normal -> skladnik x czynnik rak (E4/5 x E4/7), wies Looted przy `LootedProducesByPeople` -> skladnik Normal x czynnik rak zamiast 0
   (ta sama regula co towar wsi spladrowanej w E4/5 K5). BeingRaided i przymus gracza - jak w grze (0). **Zimy nie dokladamy** (jedno zrodlo zimy warowni:
   `WinterBite.TownFoodPostfix`, B:WinterBite.cs:188-211; P R11). Osobny klucz `TownFoodByVillagePeople`.
9. **Widok (W2) tylko czyta ksiege:** ogien na wiosce, ktora naprawde plonie (B:MapVillagesView.cs:1869-1904 dzis "pierwsza z lancucha"), dymek z ludzmi
   i stanem z ksiegi (:4596). **Zadnych nowych encji ani zmian wygladu** - zgliszcza/przyciemnienie spalonej (7.10), napisy nad spalonymi (7.9),
   komunikaty (7.10) - poza paczka (CLAUDE.md 7: wizerunki mapy; historia AccessViolation W2, SP:400-404).
10. **Menu:** rozszerzenie pozycji "People of this land" z E4/7 (E47:PeopleMenu.cs:46-88, `InformationManager.ShowInquiry`, bez `SwitchToMenu` -
    CLAUDE.md 7) o "The {DISTRICT} district" z lista wiosek; **dodatkowo w menu `village_looted`** - gracz we wsi spladrowanej trafia do niego zamiast do
    "village" (G:...PlayerTownVisitCampaignBehavior.cs:1104-1115; RB K9). Miasto i zamek - linia na kazdy okreg regionu.
11. **Stan w zapisie:** nowy klucz `arm_district` przez `SaveText.Sync` (n, puste osady b_r/b_f, flagi i doby stanow, konto z ostatniego uzgodnienia,
    crc ukladu liczone z uid i osad - **niezalezne od CRLF**: plik w grze ma CRLF, repo LF, md5 b742f307 vs 08e0475b przy tej samej tresci [pomiar]).
    Sesje rabunku - bez zapisu (gra trzyma MapEvent); sekcje `r:`/`c:` zarezerwowane dla 7.11.
12. **Poza E4/11 (nie dopisywac "przy okazji" - CLAUDE.md 8 pkt 3):** wyglad spalonej wioski i trwale spalona wies gry (WM 5.5 -> 7.10), komunikaty
    (7.10), napisy (7.9), dymek z herbem/rycerzem/wiara (7.8-7.9), rabunek osada po osadzie i okienko (7.11), lup od trafionych (7.11), mieszanka
    okregu z obrazkow - rybacy daja ryby (A02 A4), `PopulationLaw.Danger` z udzialu pustych osad zamiast stanu Looted (RB ryzyko 14), poziomy obrazka z ludzi (WM 5.3).

---

## 1. Zaleznosci od E4/5 / E4/6 / E4/7 - punkty styku i miejsca scalania

E4/11 koduje sie **od galezi z E4/6 i E4/7 po ich recenzjach** (obie z dfb12df; obie zmieniaja Settings.cs w tym samym miejscu B:Settings.cs:722,
McmSettings, ArmouryBehavior, SubModuleMain, PeopleBook, Measure40 - scalenie najpierw ich, potem E4/11).
| # | Od kogo | Czego E4/11 potrzebuje | Gdzie (scalanie) | Gdy brak |
|---|---|---|---|---|
| Z1 | E4/5 (gra) | wpiecie w `Strike` PO kontach (U24): po `AddFled` (B:Devastation.cs:249) i po `Famine.Burn` (E46:Devastation.cs:253), przed `double h = hit * f` (E46 :255): `VillageBook.OnStrike(v, hit * f, refugees * f, kind, rs != null ? rs.Comp : null, src, lBefore)` we wlasnym try | E46 :244 ma juz `lBefore` (L okregu przed ruchem) - uzyc tego samego | bez E4/6: `lBefore` liczony w E4/11 tym samym wzorem (hearth x k + away przed Move) |
| Z2 | E4/5 | zrodlo zerowania: `Strike(..., RaidState rs, MobileParty src = null)`; `Forage` (B:Devastation.cs:260-272, wywolanie E46 :273) podaje `mp`; rabunek (`Settle`, E46 :378) - `null` (napastnik z MapEvent); **klucz sesji (K2): nowe pole `RaidState.Comp` (klasa E46:Devastation.cs:69) = `raid` ustawiane przy `new RaidState()` w `StateOf` (:296) -> `OnStrike` dostaje `rs.Comp` (RaidFinalizer nie ma `__instance`)** | 4-5 linii w Devastation | - |
| Z3 | E4/5 | odczyty: `PeopleBook.RefugeesOf` (B:PeopleBook.cs:413), `RefugeesSnapshot` (:572), `LastStrike` (:488), `TryUnit` (:672), `PopulationLaw.KFor` (B:PopulationLaw.cs:308), `Devastation.YieldFactor` (B:Devastation.cs:451), `LootedOn` (:60), `MaxShare` (:134 - zrobic `internal`); **[krytyka K5] NOWY odczyt `Devastation.StopOf(Village v)`** - powod wstrzymania powrotu konta tej wsi w DZISIEJSZYM `Devastation.Daily` (0 brak, 1 cisza Q dob, 2 oblezenie regionu, 3 brama E4/6 "wies nie przyjmuje" = `famStop == 1`, 4 oblezenie miejsca pobytu = `famStop == 2`, 5 czesc z glodu czeka `famDay < 0`); slownik id -> kod czyszczony na poczatku `Daily`, wypelniany w tych samych `continue` (E46:Devastation.cs:656-659, :669) | E4/5: `MaxShare` internal + slownik i odczyt (logika powrotu bez zmian). `Famine._gateTouched` sie nie nadaje - Famine.Daily czysci go PRZED VillageBook.Daily (E46:Famine.cs:525-526) | bez E4/6: kody 3-5 nie wystepuja |
| Z4 | E4/6 | wpiecie w `Famine.TryFlee` **na koncu udanej sciezki - po bloku `lock (Gate) { SyncFam(b, f + n, away + n); ... }` (E46:Famine.cs:864-869), tuz przed `return true`** (nie miedzy `FamineFled` :855 a synchronizacja FamN - U24 "po kontach"; poprawka krytyki K8): `VillageBook.OnFamineFled(x.V, n)` we wlasnym try | E46 :853-870 | bez E4/6 - nie ma uchodzcow z glodu (b_f zawsze 0) |
| Z5 | E4/6 | dwa odczyty (nowe, w Famine.cs pod `Gate`): `double FamPartOf(Village v)` = czesc konta z glodu (`Barn.FamN` przez `FamPart`, E46:Famine.cs:66-87) i `bool DaysOf(Village v, out double days)` = dni spichlerza dla gospodarzy (menu) | E46 Famine.cs - po `HungerOf` (:234) | b_f = 0 i menu bez linii spichlerza |
| Z6 | E4/6 | kolejnosc doby: `Famine.Daily` (E46:ArmouryBehavior.cs:1437) -> **`VillageBook.Daily`** (uzgodnienie po powrotach i ucieczkach z glodu tej doby) | jedna linia po E46 :1437 | po `Devastation.Daily` (B :1436) |
| Z7 | E4/6 | ta sama metoda co `Famine.ShelterPostfix` (E46:Famine.cs:386-397, wpiecie :1065) i `WinterBite.TownFoodPostfix` (B:WinterBite.cs:188, :207-211) | postfiksy addytywne (Add), kolejnosc obojetna; nasz wychodzi przy `IsUnderSiege` (gra i tak nie daje wtedy skladnika wsi) | - |
| Z8 | E4/7 | `PeopleMenu.Consequence` (E47:PeopleMenu.cs:46-88) - dopisanie sekcji okregu przed `ShowInquiry` (:88); `AddMenus` (:21-30) - opcja w `village_looted` | E47 PeopleMenu.cs | bez E4/7: E4/11 dodaje wlasna pozycje "People of this land" tylko z sekcja okregu (ta sama nazwa, ten sam id - w E4/7 scalic w jedna) |
| Z9 | E4/7 | `FreeHands.YieldFactor(v)` (E47:FreeHands.cs:177) do czynnika rak warowni i udzialu plonu wioski | E47 | czynnik L = 1 |
| Z10 | W2 (gra) | `MapVillageData.Parse/ByDistrict/CompareOrder` (B:MapVillagesView.cs:152-206, :286-305) - czyste, bez typow gry; `DataPath` (:1589-1601, dzis prywatne w widoku) przeniesc do `MapVillageData` jako `internal static`; **[krytyka K1] `MapVillageRecord` (:70-91) NIE ma pola `road_dist` i `Fill` (:239-265) go nie czyta** (kolumna jest w TSV - 2 446 z 2 446 wierszy, w `DefaultColumns` :147-148, ale nie w `Required`): dopisac `public float RoadDist = -1f;` i w `Fill` `if (Num(Get(c, col, "road_dist"), out v) && v >= 0f) r.RoadDist = v;` (kolumna opcjonalna - wiersz bez niej NIE odrzucany; widok pola nie czyta) | MapVillagesView.cs | brak kolumny: odleglosc w linii prostej (x, y) od wsi gry |
| Z11 | E4/9 (rownolegle, PLAN-KODU-E49 6.2-6.3) | **[krytyka K7]** E4/9 zostawia pojmanych przy uderzeniu na koncie UCHODZCY i zdejmuje ich przy koncu wygranego rabunku `PeopleBook.RefugeesToCaptives` (E49 :314) - to NOWE wyjscie z konta, ktore 3.5 bralo by za powrot (b spada o ok. 10% trafionych rabunku z niewola dzien po spaleniu - wioska spalona przy 50% spada pod 47% = "w odbudowie" nazajutrz). Wpiecie po `RefugeesToCaptives` (wlasny try, po kontach): `VillageBook.OnRefugeesTaken(v, taken)` -> `ARexp -= taken` (nie mniej niz 0), b bez zmian (pojmani jak zabici - w dB, nie wracaja) | ThrallLaw.CapturePrefix (E4/9) | bez E4/9 - nic (pojmani = 0) |
Lista do sprawdzenia przy kodzie (E4/6 i E4/7 moga sie jeszcze zmienic po recenzji): linie Z1/Z4-Z6/Z8; czy E4/6 dalej trzyma `FamN` (R1-1) - jesli nie,
b_f liczyc z ucieczek (OnFamineFled) i powrotow `Famine.Returned` (E46:Famine.cs:336); czy E4/7 nie zmienil id pozycji menu `arm_people_<menu>`.

---

## 2. Dane: plik ukladu po stronie kampanii

- `VillageBook` wczytuje TSV raz na sesje (`OnSessionLaunched`, po SyncData) tym samym parserem co widok (`MapVillageData.Parse`, B:MapVillagesView.cs:152-206;
  widok dalej wczytuje sam - bez zmian w jego `Load` :1603-1693, poza `DataPath`; **parser dostaje opcjonalne pole `RoadDist` - Z10, krytyka K1: dzis
  `road_dist` z pliku nie trafia do rekordu, a lancuch palenia 3.3 i kolejnosc glodu 3.4 go uzywaja**). Bez TSV / zle kolumny -> ksiega dziala z sama wsia gry w kazdym okregu
  (100% osad w niej - A02 L12), wpis w logu. CRC sceny - jak widok (plik z inna scena: wioski pominiete, okregi same).
- **Okregi:** wszystkie wsie gry (`Settlement.All`, `IsVillage`; 571) - takze 146 bez wiosek. Wioski okregu po `CompareOrder` (order, id). Wiersz z wsia,
  ktorej nie ma w grze - pominiety (licznik).
- **crc ukladu** (`L:` w zapisie) = crc32 z ciagu `uid:settlements;` wszystkich wierszy w kolejnosci pliku - zmiana CRLF/LF, komentarzy i kolumn widoku
  (model, front_deg) go nie rusza; zmiana rozstawienia - tak (wtedy przydzial zastepczy, 3.7).
- **n_w (WM rozdz. 3):** f_w = `settlements` z pliku; Sf = suma f_w, Mf = max f_w; gdy Sf + Mf <= n: n_w = f_w, n_wsi = n - Sf; inaczej c = n / (Sf + Mf),
  n_w = f_w x c, n_wsi = Mf x c (= najwieksza wioska). Liczby rzeczywiste (2 miejsca), w menu zaokraglone (>= 1). Okreg bez wiosek: n_wsi = n.
- **Pulap wioski** cap_w = MaxShare x n_w (90%; ten sam co Room E4/5).
- Geometria: x, y wioski; polozenie wsi gry `Settlement.GetPosition2D` (G:...Settlements\Settlement.cs:199); `road_dist` z pliku; `class` (H/R/F) do wag odbudowy
  i kolejnosci opuszczania; `kind` do opisu miejsca w menu.

---

## 3. Model ksiegi wiosek (`VillageBook.cs`, NOWY, ok. 800 linii)

### 3.1 Pamiec
```
sealed class Dist { string Id; Village V; double N; bool NSet; double[] Nw; int[] Cls; string[] Uid; float[] X, Y, Rd; string[] Kind;   // [0..W-1] wioski, [W] wies gry
                    double[] Br, Bf; byte[] Flag; int[] BurnDay, RebuiltDay; double ARexp, AFexp; Sess S; int SmokeIdx = -1; }   // SmokeIdx - ostatnia wioska sesji do konca czujnosci (bez zapisu, K3)
sealed class Sess { object Key; int Cur; string Raider; Vec2 From; bool HasFrom; int Day; }      // trwajacy rabunek (bez zapisu); Key = RaidEventComponent (K2)
Dictionary<string, Dist> _d (id wsi -> okreg); object Gate; liczniki doby (bez zapisu); string _pending (napis z zapisu do Resolve)
```
Flag: bit Burned, bit Abandoned (ustawione przy przekroczeniu 50%, kasowane przy b < 1% osad = zdarzenie "odbudowana").

### 3.2 n okregu (`EnsureN`, leniwie - pierwsza doba albo pierwszy Strike)
`PopulationLaw.EnsureCalibrated()` (B:PopulationLaw.cs:322); L0 = hearth x `KFor` + `RefugeesOf`; n = max(1, round(L0 / DistrictSettlementPeople)); NSet = true.
Nowa kampania - doba 1 (ludzie z tabeli krain). Stary zapis - pierwszy odczyt po wgraniu. Bez kalibracji (k = 0) - czekamy (licznik).

### 3.3 Spalenie osad (`OnStrike` -> `Burn`) - lancuch palenia (WM 6.2)
- dB = h x n / lBefore (h = trafieni po `f`, lBefore = L okregu przed ruchem - Z1). `ARexp += refugees` (to, co Strike dopisal na konto).
- **[krytyka K2] Klucz sesji = `RaidEventComponent` rabunku** (= `v.Settlement.Party.MapEvent.Component`, G:...MapEvents\MapEvent.cs:114 - ten sam obiekt, ktorym
  Devastation indeksuje `_raids`), NIE `RaidState`: widok `rs` nie widzi. Sesje otwiera to, co przyjdzie pierwsze - `FireUid` z widoku (wies `IsUnderRaid`, takze w walce
  z milicja) albo `OnStrike` - ta sama funkcja wyboru pierwszej wioski (ponizej), na watku glownym; miedzy otwarciem a pierwszym Strike b sie nie zmienia, wiec wybor
  jest ten sam przy wlaczonym i wylaczonym widoku. Bez tego ogien przez pierwsze godziny rabunku (walka z milicja + do pierwszego kroku gry, kiedy Settle wola Strike - tylko
  przy `took > 0`, B:Devastation.cs:356-361, :369-375) stoi na `FirstUsable` (kolejnosc 0), potem skacze na wioske z ksiegi, a log "ogien nad X" (tylko przy starcie ognia, B:MapVillagesView.cs:1884-1888)
  zawsze mowi "(lancuch gry)" - G6 nie moglby przejsc.
- **Rabunek** (`rs != null`): sesja = klucz wyzej (jeden rabunek = jeden `RaidEventComponent`, jak RaidState E4/5 B:Devastation.cs:286-304). Nowa sesja: napastnik =
  `v.Settlement.Party.MapEvent.AttackerSide.LeaderParty` (ta sama droga co `RaiderName` widoku, B:MapVillagesView.cs:1912-1923); strona wroga = najblizsza wsi
  warownia frakcji napastnika (`Settlement.All`, `IsFortification`, `MapFaction` - raz na sesje); brak (bandy, Inni bez twierdz) - bez kierunku.
  Pierwsza wioska = min `road_dist_w x (1 - 0.3 cos t_w)` wsrod wiosek z miejscem (t_w - kat miedzy wioska a strona wroga, patrzac od wsi gry).
  Nastepne w tej samej sesji: najblizsza (x, y) wioska z miejscem do biezacej (remis - `order`); **wies gry na koncu**. `S.Cur` = wioska, ktora wlasnie dostala osady.
- **Zerowanie** (`src` = partia): najblizsza obozowi wioska z miejscem, dalej jak wyzej; bez ognia (wioska tylko maleje - WM 6.2).
- Kazda wioska do cap_w - (Br + Bf); reszta dalej; gdy wszystko pelne - "nadmiar" (licznik; **nie 0 w okregach przy pulapie** - Room ogranicza do 90% tylko
  UCHODZCOW (B:Devastation.cs:167-182), a dB liczy tez zabitych i las (ok. 7-10% wiecej [S: zabici 2-5% + las 2-5%]); przyklad [S]: L 1000, n 4, jedno uderzenie 900 ->
  dB 3.6 = pulap, konto 855, L 955, Room jeszcze 4.5 -> drugie uderzenie +0.019 osady ponad pulap. Male, nazwane w G5 - krytyka K9).
- Przekroczenie 0.50 n_w przez Br -> Flag Burned, BurnDay = dzis (zdarzenie "spalona", napastnik z sesji).

### 3.4 Opuszczenie z glodu (`OnFamineFled`, Z4)
dB = n_g x N / L (L = hearth x k + konto - po ruchu); `AFexp += n_g`; kolejnosc: ubocze (F), potem linia (R), potem wezel (H) - od najdalszej (`road_dist`),
wies gry na koncu (WM 6.2 "glod oproznia najpierw ubocze"). Br + Bf >= 0.50 n_w przy Br < 0.50 n_w -> Flag Abandoned.

### 3.5 Uzgodnienie doby (`Daily`, Z6) - jedyny spadek pustych osad
Dla okregow z B > 0 albo kontem > 0 albo ARexp/AFexp > 0:
- A = `RefugeesOf(v)`, A_f = `Famine.FamPartOf(v)` (0 bez E4/6), A_r = A - A_f.
- czesc r: **A_r <= 1e-6 (konto puste - powrot "ostatni czlowiek caly" i RefugeesHome/RefugeesDie zeruja konto co do 1e-9, B:PeopleBook.cs) -> Br = 0**
  (poprawka krytyki K6: dawny prog 0.5 czlowieka zostawial konto do 0.5 bez pustych osad - r2 do 0.5 i k2 > 0, sprzeczne z G3/G4; prog "< 1 czlowieka" tylko w wyniku rocznicy); inaczej gdy A_r < ARexp: zdejmij (1 - A_r / ARexp) x sum(Br) z wiosek proporcjonalnie do Br_w x waga (H 1.5, R 1.0,
  F 0.5, wies gry 1.5), nie ponizej 0 (dwa przebiegi); gdy A_r > ARexp + 0.5 -> "doplyw bez zrodla" (licznik, oczekiwane 0) i przydzial jak 3.7 dla nadwyzki.
  Potem ARexp = A_r. To samo dla czesci f (A_f, AFexp, Bf).
- Stany (3.6), zdarzenia "odbudowana" (Flag skasowany, RebuiltDay), sesje rabunku: usun, gdy wies nie `IsUnderRaid` (G Settlement.cs:446) - przy usunieciu
  `SmokeIdx = S.Cur`; `SmokeIdx = -1`, gdy wies nie `IsRaided` (koniec czujnosci) - dym zostaje na tej samej wiosce (krytyka K3).
- **Rok po najezdzie** (prog etapu): region = `OutlawLaw.RegionFor` (B:OutlawLaw.cs:243); gdy `LastStrike(region)` i dzis - last = D (`RefugeeReturnDays`, ta sama
  obcinka `Clamp(.., Q + 1, 3640)` co E46:Devastation.cs:630) (364): wynik okregu = konto_r < 1 i wszystkie Br_w < 0.01 n_w; przyczyna NIE (z `Devastation.StopOf(v)` tej
  doby - Z3, krytyka K5): oblezenie regionu (2), oblezenie miejsca pobytu (4), brama E4/6 - wies nie przyjmuje (3; wstrzymuje CALE konto, takze czesc z rabunku -
  E46:Devastation.cs:656), czesc z glodu czeka / inny zegar (5 albo A_f > 0), bez przyczyny. **[krytyka K4] Rocznice sprawdzac we WSZYSTKICH okregach (571 x `LastStrike`,
  < 0.1 ms), nie tylko w petli "B > 0 albo konto > 0"** - okreg, ktory wstal w dobie last+363 (B = 0, konto 0), wypadalby z petli i G-ETAP nie mialby ani jednego wiersza TAK.
  (Powrot E4/5: w dobie last+363 `backR = raidAway / max(1, 364 - 363)` = cale konto - E46:Devastation.cs:664-665; w dobie last+364 konto = 0.)
- Spustoszenie wylaczone (`Abandon`, B:Devastation.cs:672) -> konto 0 nastepnej doby -> wszystkie b = 0; zdarzenia "odbudowana" zbiorczo (jedna linia, bez listy).

### 3.6 Ludzie, plon i stan wioski (odczyty - pod Gate, nie rzucaja)
- dom = hearth x KFor; w domu_w = dom x (n_w - Br_w - Bf_w) / (N - B); poza domem_w = A_r x Br_w / sum(Br) + A_f x Bf_w / sum(Bf) (0/0 = 0); ludzie_w = suma.
- udzial plonu_w = Y x w domu_w / dom, Y = `Devastation.YieldFactor(v)` x `FreeHands.YieldFactor(v)` (Z9); utracone_w = poza domem_w / (dom + A).
- stan: Standing (b < 1%), Damaged (1% <= b < 50%, bez flagi), Burned (Flag Burned i Br >= 47%), Abandoned (Flag Abandoned i b >= 47%), Rebuilding (flaga,
  b < 47%), Burning (nakladka: `S.Cur == w` i `v.Settlement.IsUnderRaid`).
- API (tez dla etapu 7): `StateOf(string uid, out VState st)` (stan, ludzie w domu / poza, osady, BurnDay, RebuiltDay), `FireUid(string districtId)`,
  `Summary(Village v, out DistView dv)`, `Burn(Village v, double settlements, int kind, object session, MobileParty src)`, `Plan(Village v, ..., int m)` (7.11 - tylko szkielet z lancucha 3.3).

### 3.7 Przydzial zastepczy (stary zapis, inny uklad, wlaczenie klucza, doplyw bez zrodla)
Br0 = min(cap okregu, A_r x N / (L x (1 - (DevastationKilledPercent + DevastationOutlawBasePercent) / 100))) (WM 7.2 "T0") wzdluz `order` od 0 (bez kierunku);
Bf0 = A_f x N / L od ubocza. ARexp = A_r, AFexp = A_f. Linia: "przydzial zastepczy w N okregach (przyczyna)".

---

## 4. Stan w zapisie `arm_district` (SaveText.Sync)

- Wpiecie: B:ArmouryBehavior.cs:440-441 zaraz po `arm_people`: `try { string ds = dataStore.IsSaving ? VillageBook.Export() : null; SaveText.Sync(dataStore,
  "arm_district", ref ds); if (dataStore.IsLoading) VillageBook.Import(ds); } catch (Exception e) { Log.Error("SyncData.arm_district", e); }`.
  `VillageBook.Reset()` w konstruktorze (B:ArmouryBehavior.cs:395) - stan jednej kampanii nie przecieka do drugiej. `Import` tylko odklada napis;
  `VillageBook.Resolve("wczytanie"/"nowa")` w `OnSessionLaunched` obok `RecruitKit.ResolvePending` (B:ArmouryBehavior.cs:1148) - TSV + `Settlement.Find`.
- Format: `v1|L:<crc ukladu hex>|P:<ludzi na osade przy n>|n:<idWsi>=<N>;...|w:<idWsi>=<br0>/<bf0>,<br1>/<bf1>,...,<brWsi>/<bfWsi>;...`
  `|x:<idWsi>=<i>:<flag>:<burnDay>:<rebuiltDay>,...;...|y:<idWsi>=<ARexp>/<AFexp>;...` (liczby "0.##", tylko okregi z czymkolwiek > 0 w w/x/y; n: - wszystkie).
  Zarezerwowane (nie pisane w E4/11): `r:` sesje rabunku, `c:` czujnosc, `u:` uprowadzeni (RB pkt 10, etap 7.11).
- Id ze znakami `| ; , = : /` - pomijane (licznik; wzor `Famine.BadId` E46:Famine.cs:124). Zepsuty wpis (NaN, ujemne, ponad cap, zla dlugosc listy) - okreg do 3.7.
- Rozmiar [S]: n: 571 x ok. 28 znakow = 16 KB; w/x/y przy 75 okregach z uchodzcami (z31A d120) x ok. 120 = 9 KB; wojna w 300 okregach ok. 50 KB - kawalki SaveText.
- Stary zapis bez klucza: n z pierwszego odczytu, b - 3.7. Cofniecie DLL: gra nie zapisze klucza przy nastepnym zapisie (dane zachowan od nowa) -
  po ponownym wgraniu 3.7 (te same sumy, inny rozklad - WM 7.2). Linia po wczytaniu: `z zapisu: n N okregow, puste osady M okregow, uklad zgodny/inny`.

---

## 5. Okna latek i wpiecia (dowod w dekompilacji)

### 5.1 Zywnosc warowni od ludzi - postfiks na `DefaultSettlementFoodModel.CalculateTownFoodStocksChange(Town, bool, bool)`
- Cel: G:...GameComponents\DefaultSettlementFoodModel.cs:38-41 (public override -> `CalculateTownFoodChangeInternal` :43); skladnik wsi :66-73:
  `if (!town.IsUnderSiege) { ... foreach (Village boundVillage in town.Owner.Settlement.BoundVillages) { value = 0; if (VillageState == Normal) value = (GetHearthLevel() + 1) * 6; bonuses.Add(value, boundVillage.Name); } }`.
- Czynny lancuch: ROT owija BK, BK wola baze (GL 1.3 :55-58; BK:BannerKings.Models.Vanilla\BKFoodModel.cs:37-48 `base.CalculateTownFoodStocksChange`, +BK "Local farmland" :57;
  ROTd:16151-16157 - warownie Innych zwracaja 0 PRZED baza, wiec postfiks ich nie dotyka). Precedens wpiecia na tej metodzie w grze: B:WinterBite.cs:207-211 (dziala - bieg B z31).
- `public static void TownFoodPostfix(Town town, ref ExplainedNumber __result)` (Priority domyslne; addytywny): wyjscie gdy klucz off, `town == null`, `town.IsUnderSiege`,
  szybka sciezka (`PeopleBook.RefugeeVillages == 0` i zadna wies warowni nie Looted i FreeHands bez L < 1). Dla kazdej `town.Owner.Settlement.BoundVillages`:
  gra_v = Normal ? (lvl + 1) x 6 : 0; nasze_v = (Normal albo (Looted i `Devastation.LootedOn`)) ? (lvl + 1) x 6 x Y_v : 0 (lvl = `v.GetHearthLevel()` - z postfiksem
  progow E4/5 B:Devastation.cs:478, ten sam co w grze); `__result.Add(suma(nasze - gra), _txtHands)` gdy |suma| > 0.001. Opis: `"{=!}Village fields: people away or back at work"`.
  Jedna funkcja `TownVillageTerm(Village, out gra, out nasze)` dla postfiksu i linii doby (rozdz. 8). Moze isc z watkow AI - tylko odczyty pod zamkami PeopleBook/FreeHands.
- Wpiecie: `VillageBook.ApplyAll(_harmony)` we wlasnym try w SubModuleMain po `Famine.ApplyAll` (E46:SubModuleMain.cs:149; B:SubModuleMain.cs:148 `Devastation.ApplyAll`) -
  **wpiete zawsze, liczy przy kluczu (U26)**; linia startowa z wlascicielami latek celu (`Harmony.GetPatchInfo`, U25).

### 5.2 Wpiecia w nasz kod (bez Harmony)
- `Devastation.Strike` (Z1, Z2) - jedno wywolanie po kontach (U24); `Forage` z `mp`.
- `Famine.TryFlee` (Z4) - po `FamineFled`; dwa odczyty (Z5).
- `ArmouryBehavior`: konstruktor (Reset), SyncData (4), `OnSessionLaunched` (Resolve), `OnDailyTick` (Z6).
- `MapVillagesView` (widok, watek glowny, 4 razy na sekunde - bez pracy co klatke):
  - `PollRaids` (B:MapVillagesView.cs:1869-1904): `target` = slot o uid `VillageBook.FireUid(d.Id)` (slownik uid -> Slot budowany w `Load`), gdy ksiega czynna i zwraca uid;
    `null` przy plonacej wsi gry (lancuch doszedl do niej) - ognia na wioskach brak (wies gry plonie sama, jak dzis); ksiega wylaczona - `FirstUsable` jak dzis (:1906-1910).
    **Brak sesji przy `IsUnderRaid` (walka z milicja, przed pierwszym krokiem gry): `FireUid` OTWIERA sesje (klucz `MapEvent.Component`, ta sama funkcja wyboru co 3.3 -
    krytyka K2)**; `target` liczy sie przy kazdym odpytaniu (4/s, :1878-1880), wiec `FireUid` z otwarta sesja = sam odczyt slownika pod Gate. Dym w czujnosci - `SmokeIdx`
    (ostatnia wioska sesji, 3.5), nie `FirstUsable`. `ApplyFx` tylko przy zmianie (:1925-1948, bez zmian).
    Log "Wioski: ogien nad X" dostaje dopisek "(ksiega)" albo "(lancuch gry)".
  - `OnHover` (:4587-4613): ludzie = `StateOf(uid)` (w domu + poza, zaokraglone; ksiega wylaczona - stary wzor `settlements x 250`); linia stanu z istniejacych tekstow
    (`VilTipBurned` "Burned {DAYS} days ago. {RETURNED} of {PEOPLE} souls have come back.", `VilHalfempty`, `VilAbandoned`, `VilTipBurning`) + nowy "Rebuilt {DAYS} days after the burning."
- `PeopleMenu` (Z8) - rozdz. 6.

### 5.3 Menu `village_looted` (dowod)
G:...CampaignBehaviors\VillageHostileActionCampaignBehavior.cs:118-119 (`AddGameMenu("village_looted", ...)`, jedyna opcja "Leave..." isLeave; NavalDLC/gra dokladaja
"Disembark"/"Set Sail" - PlayerTownVisitCampaignBehavior.cs:163-164); wejscie: `game_menu_village_on_init` przelacza na nie wies Looted (:1104-1115, :1118-1127).
`AddGameMenuOption(..., index)` wstawia na `index`, gdy `index <= liczba opcji`, inaczej na koniec (G:...GameMenus\GameMenu.cs:146-156) -> dla `village_looted` **index 0**
(nad "Leave..."), dla reszty jak E4/7 (6). `GetPresumedGameMenu` tworzy menu, gdy go jeszcze nie ma (G:...CampaignGameStarter.cs:98-107) - kolejnosc rejestracji obojetna.

---

## 6. Menu okregu - rozszerzenie "People of this land" (E47:PeopleMenu.cs; napisy po angielsku, okno `ShowInquiry`)

- Wies (takze `village_looted`): po tekscie E4/7 pusty wiersz i sekcja okregu tej wsi (gdy `VillageBookEnabled`; ksiega bez n - `DistNotYet`):
  - `The {DISTRICT} district: {SETTLEMENTS} settlements in {VILLAGES} villages` (nowy `DistHead`);
  - podsumowanie - istniejacy `DistSummary` ("{STANDING} of {TOTAL} villages still stand: {BURNED} burned, {BURNING} burning. {PEOPLE} souls are at home and
    {REFUGEES} have fled. The granary holds food for about {DAYS} days.", B:VillageTexts.cs:34) gdy `Famine.DaysOf` (Z5), inaczej nowy `DistSummaryNoGranary` (bez zdania spichlerza);
  - `The district's harvest stands at {PCT}% of a full one.` (nowy `DistHarvest`, Y okregu; tylko gdy < 100%);
  - wiersz na wioske (najpierw plonace, spalone, opuszczone, w odbudowie, czesciowo puste; potem stojace - najwyzej 12 wierszy, reszta `...and {N} more villages, all standing.`):
    istniejace `DistLineBurning` "{VILLAGE}: burning ({RAIDER})", `DistLineBurned` "{VILLAGE}: burned {DAYS} days ago, {RETURNED} of {PEOPLE} souls back",
    `DistLineRebuilding` "{VILLAGE}: rebuilding, {PEOPLE} souls back", `DistLineWhole` "{VILLAGE}: {PEOPLE} souls, {SETTLEMENTS} settlements" (B:VillageTexts.cs:35-39) + nowe
    `DistLinePart` "{VILLAGE} {PLACE}: {BURNED} of {SETTLEMENTS} settlements are ash, {PEOPLE} souls at home, {FLED} fled", `DistLineAbandoned` "{VILLAGE} {PLACE}: abandoned in
    the famine, {PEOPLE} souls left", `DistLineMain` "{VILLAGE}, the district village: {PEOPLE} souls at home in {SETTLEMENTS} settlements"; na koncu wiersza "({SHARE}% of the harvest)";
  - miejsce z `kind`: "by the bridge", "at the ford", "at the crossroads", "on the road", "by the river", "by the lake", "on the coast", "among the fields" (WM rozdz. 9).
- Miasto i zamek: `Villages of the {REGION} lands` + wiersz na kazda wies regionu (`BoundVillages`): nowy `DistRegionLine` "{DISTRICT}: {STANDING} of {TOTAL} villages stand,
  {PEOPLE} souls at home, {REFUGEES} fled".
- Teksty: stale w `VillageTexts.cs` dopisane recznie, konwencja `{=arm_...}` (plik generowany z VillageTexts.xml, ktorego nie ma w repo - B:VillageTexts.cs:16).
- Liczby z ksiegi na zywo (odczyt pod Gate, O(wiosek okregu)); bez stanu menu, bez `SwitchToMenu` (CLAUDE.md 7).

---

## 7. Klucze Settings (po kluczach E4/6/E4/7 - grupa ksiegi ludzi; `python tools/gen_mcm.py`; w `Armoury.json` Jeffa dzis zadnego z nich - grep 11.10, przy kodzie powtorzyc)
| Klucz | Domyslna | Uzasadnienie | Podpowiedz MCM (EN) |
|---|---|---|---|
| `VillageBookEnabled` | true | P E4/11; SP:760 | every map village keeps its own people and its share of the district's harvest: raids and foraging armies empty its settlements (the side facing the raiders burns first), famine leaves them abandoned, and they stand again as their people come home - within a year of the last raid in the land. Shown in 'People of this land' and in the map village tooltip. Needs Devastation Enabled to burn anything. Off = all map villages stand, as before |
| `DistrictSettlementPeople` | 250 | RB pkt 1 (Yorkshire 1319 190, Langwedocja 200-450, parafia 1328 ok. 370) | people of one settlement (a village with its fields) when a district is first reckoned; a district's villages share its settlements. Change it only when you start a new campaign |
| `TownFoodByVillagePeople` | true | P 4.6 "zywnosc warowni tak samo"; SP:459-460 | the food a town or castle gets from each of its villages follows the people at home and at work there, as the village's own harvest does: villagers away cut it, and a plundered village keeps feeding its castle with the people who stayed instead of nothing. Off = as in the game |
Stale w kodzie (nie ustawienia): prog spalenia 0.50 (decyzja Jeffa, WM 10), histereza 0.03, wagi odbudowy 1.5/1.0/0.5/1.5, waga kierunku 0.3.

---

## 8. Linie logu i CSV

- Start (SubModuleMain): `VillageBook (E4/11): ksiega wiosek pobocznych - wpiete: zywnosc warowni od ludzi (DefaultSettlementFoodModel.CalculateTownFoodStocksChange); BRAK: -;
  wlasciciele latek celu: <Harmony id: postfiksy/prefiksy> (U25); wpiecia w kod: Devastation.Strike, Famine.TryFlee, MapVillagesView (ogien, dymek), PeopleMenu (okno, village_looted);
  ustawienia startowe przed MCM (stan - linia dzienna).`
- Pierwsza doba sesji: `Ksiega wiosek (E4/11): uklad '<tytul TSV>' (crc ukladu X, plik crc32 Y), wiosek W w D okregach (odrzucone r, wsi gry brak m) | okregi 571: z wioskami a, sama wies gry b |
  osady: z zapisu c okregow, nowe e (ludzie / 250: mediana m1, min m2 (nazwa), max m3 (nazwa)), okregi z wioskami pomniejszonymi k | puste osady: z zapisu p okregow, przydzial zastepczy q
  (stary zapis / inny uklad / wlaczenie) | czas T ms.`
- Doba (przy LogEnabled; pisana, gdy czynna i cokolwiek > 0): `Ksiega wiosek (E4/11): dzien N | okregi z pustymi osadami D (pustych osad B: rabunek i zerowanie Br, glod Bf; wiosek dotknietych W:
  plonie p, spalone s, opuszczone o, w odbudowie r, czesciowo puste c) | dzis: +x osad (rabunek a w wioskach v1 / wsiach gry v2, zerowanie b), opuszczone z glodu +y, wstalo -z, zastepczy +z0,
  nadmiar ponad pulap e | spalone dzis n1: wioska (okreg) - napastnik; ... | odbudowane dzis n2: wioska (okreg) po D dobach; ... | kontrola: w domu wiosek - DOM max |r1|, poza domem wiosek - konto
  max |r2|, puste osady przy pustym koncie k1, konto bez pustych osad k2, ponad pulap k3, osady dzis x S - trafieni dzis |r3| (doplyw bez zrodla d0) | rok po najezdzie: okregow q (TAK q1,
  NIE q2: oblezenie q3, glod q4, bez przyczyny q5) | zywnosc warowni od ludzi: warowni t, Food swiata +f/d (spladrowane +f1 w g1 wsiach, uchodzcy i rece -f2 w g2) | potkniecia P (razem P0) | czas T ms.`
- CSV `wioski-okregi.csv` (katalog logu, jak `spichlerze-regiony.csv`; wiersz tylko dla okregu z B > 0, kontem > 0, zdarzeniem albo rocznica tej doby):
  `dzien;okreg_id;okreg;region_id;region;gracz;n;n_wiosek;wiosek;dom;konto_r;konto_f;puste_r;puste_f;plonie;spalone;opuszczone;odbudowa;czesciowo;plon_okregu;ostatni_najazd;dni_od_najazdu;oblezona;rok;wynik_rok`
  (`rok` = 1 w dobie last + 364 - wiersz dla KAZDEGO okregu regionu z rocznica, takze bez b i konta (K4); `wynik_rok` = TAK / NIE-oblezenie / NIE-pobyt-oblezony /
  NIE-brama-E46 / NIE-glod / NIE - z `Devastation.StopOf` tej doby, K5).

---

## 9. Progi - `tools/sprawdz_logi.py --grupa etap4`, blok E4/11 (rusza przy linii startowej "VillageBook (E4/11):"; wzor bloku `etap4_e46`)

Test zbiorczy 120 dob (Jeff 11.10 03:05; nowa kampania, ustawienia jak u Jeffa) + zapis z doby 362 (9 dob) + bieg zimowy 120 dob (G9) + **bieg konca etapu 728 dob (G-ETAP)**.
| # | Prog | Jak | Baza / skad liczba |
|---|---|---|---|
| G1 | 0 ERROR naszych modow; potkniecia VillageBook 0 (najwyzej 5 z nazwa miejsca) | log | standard paczki |
| G2 | linia startowa "BRAK: -"; pierwsza doba: wiosek 2446 w 425 okregach, odrzucone 0, wsi gry brak 0, okregi 571, n dla wszystkich; okregi pomniejszone - INFO | log | TSV b742f307 |
| G3 | kontrola ksiegi kazdej doby: max \|r1\| (w domu wiosek - DOM) <= 0.01 i max \|r2\| (poza domem - konto) <= 0.01 czlowieka | linia | z definicji 3.6 |
| G4 | k1 = k2 = k3 = 0 kazdej doby (poza doba przydzialu zastepczego); d0 = 0 | linia | 3.5 |
| G5 | \|r3\| (osady x S - trafieni Devastation tej doby) <= 0.1% trafionych + nadmiar (nazwany) | linia + "Ludzie (spustoszenie)" | 3.3 |
| G6 | ogien: rabunki wsi z wioskami w biegu z widokiem - "ogien nad X (ksiega)" >= 95% ogni (sesje otwiera `FireUid` przy starcie ognia - K2); "(lancuch gry)" tylko przy ksiedze wylaczonej / bez n / wyjatku odczytu (nazwane) | "Wioski: ogien" | INFO bez ekranu mapy |
| G7 | wioski spalone / opuszczone / odbudowane / czesciowo puste - liczby (INFO) | linia | [S z z31A: 25 564 trafionych w 115 dob (222/d), konto d120 23 605 w 75 wsiach, najwiekszy udzial regionu 8.3% (Craster's Keep)] -> wiosek czesciowo pustych ok. 50-80, spalonych 0-20 w 120 dob |
| G8 | zywnosc warowni: korekta = suma (nasze - gra) z tej samej funkcji; warownie z wsia Looted z ludzmi w domu dostaja > 0 (INFO: +f/d swiata) | linia | 5.1 |
| G9 | bieg zimowy: progi E4/6 F6/F7 (zadna warownia AI nie glodzi przed 30. doba zimy; mediana U27 >= 364) - nie gorzej niz bieg E4/6 bez E4/11 | `etap4_e46` | E4/6 test |
| G10 | czas linii <= 3 ms/d srednio; wczytanie TSV po stronie kampanii <= 50 ms; s/dobe biegu <= +1% | linia | widok: wczytanie z31A w logu "Wioski: plik ... czas" |
| G11 | zapis 9 dob: po wczytaniu "z zapisu: n 571 okregow, puste M, uklad zgodny"; G3/G4 od pierwszej doby po wczytaniu; 0 bledow | log | 161 |
| G12 | menu: reczna proba Jeffa - wies, wies spladrowana (`village_looted`), miasto, zamek -> "People of this land" -> sekcja okregu; liczby = linia/CSV tego okregu | gra | P E4/7 T14 |
| G13 | wylaczniki (autotest 40 dob): `VillageBookEnabled` = false -> wszystkie b = 0 jedna linia, dymek "settlements x 250", ogien "(lancuch gry)"; `TownFoodByVillagePeople` = false -> korekta 0 | log | U26 |
| G14 | progi etapow 1-3 i E4/5 S1-S21 / E4/6 F1-F13 bez zmian (E4/11 nie rusza ludzi ani hearth: S3/S4/S6 jak w biegu bez E4/11) | `--grupa etap2/etap3/etap4` | P rozdz. 4 |
| **G-ETAP** | **bieg 728 dob, "spalona wioska (takze poboczna) wraca po roku" (P :547, U13):** wiersze CSV z `rok` = 1, ostatni najazd w d1-d364: konto_r < 1 czlowieka (zywi uchodzcy 0, w domu = 100% wobec dom + konto, czyli >= 95%) **i** puste_r = 0, spalone = 0, odbudowa = 0 (kazda wioska okregu wstala) - **100% wierszy poza nazwanymi** (NIE-oblezenie i NIE-pobyt-oblezony: powrot liczony po oblezeniu, wiersz pomijany z liczba; NIE-brama-E46 (wies nie przyjmuje -
glod gospodarzy albo pole/spichlerz dla konta w innym regionie, E46:Famine.cs:377-382) i NIE-glod (czesc z glodu ma zegar E4/6) - INFO z liczba, K5); wioski, ktore byly spalone (Flag) w d1-d364 - lista z doba spalenia i odbudowy (INFO); brak spalonych - prog na wioskach dotknietych (b > 0) | CSV + linia | zastepuje wiersz INFO E4/5 "w biegu 728 dob" (E46:tools/sprawdz_logi.py:1344-1348) |
Nie przechodzi -> wylaczamy w `Armoury.json` autotestu `TownFoodByVillagePeople`, potem `VillageBookEnabled`, i powtarzamy.

---

## 10. Kontrola calosci (zasada 0; do "Ryzyko / co sprawdzic" w CHANGELOG)

1. **Zamknieta ksiega ludzi:** E4/11 nikogo nie przesuwa (zero `PeopleBook.Move`, zero zapisu `Hearth`); ludzie wiosek to podzial DOM i konta UCHODZCY (G3 z definicji).
   Zabici i las sa w dB (puste osady), ale nie na koncie - schodza z b proporcjonalnie przy powrotach, konto 0 = b 0. **Pojmani E4/9 przechodza PRZEZ konto (E49 6.2-6.3:
   przy uderzeniu zostaja uchodzcami, `RefugeesToCaptives` przy koncu rabunku) - bez Z11 ich zdjecie wygladaloby jak powrot (b w dol, falszywa "odbudowa"); z Z11
   `ARexp -= pojmani`, b bez zmian (krytyka K7).** Wyjscia z konta, ktore 3.5 bierze za ubytek: powrot, podloga 10 (MRefugeeFloor), smierc 1.3%/rok, smierc z glodu
   uchodzcow (E46:Famine.cs:648, :701). "Hearth (E4/0)" RESZTA bez zmian.
2. **Zamknieta ksiega jedzenia:** spichlerz okregu (Famine) nietkniety - jeden na okreg, `Burn` raz w Strike (E46 :253; nasze wywolanie PO nim). Zywnosc warowni: skladnik wsi
   gry jest symbolem (nie bierze z magazynu wsi) - E4/11 go skaluje tym samym czynnikiem rak co zywnosc wsi, nie tworzy nowego przeplywu; "spichlerz miasta zywiony przez
   okregi" (GL 2.13 krok B) zostaje odlozony (P rozdz. 7). Wies Looted daje warowni jedzenie od ludzi w domu - ta sama regula co jej towar (E4/5 K5, SP:459-460).
3. **Nic drugi raz:** plon okregu - jeden czynnik E4/5 (+ L z E4/7), E4/11 tylko przypisuje (0.7); zima warowni - tylko WinterBite.TownFoodPostfix (B:WinterBite.cs:188), nasz postfiks
   bez zimy; zerowanie: ScorchedEarth pomija partie w MapEvent (B:ScorchedEarth.cs:69), wiec rabunek i zerowanie nie licza tej samej doby dwa razy; "3% w las przy
   spaleniu" OutlawLaw wylaczone przy E4/5 (B:OutlawLaw.cs:670-681 - `Devastation.Covers` -> return); BK jency z rabunku - E4/9, nie tu.
4. **Devastation (E4/5):** jedno wywolanie po kontach (U24 - lista miejsc do recenzji: Devastation.Strike 1x, Forage 1 argument, Famine.TryFlee 1x); `MaxShare` internal;
   Room i pulap te same; `Abandon` -> b 0. Sygnatura `Strike` z `src` - jedyna zmiana logiki E4/5: brak (src tylko do kierunku). `StopOf` (Z3) - slownik
   wypelniany w istniejacych `continue` `Daily`, bez zmiany kolejnosci i liczb powrotu (S-progi E4/5 bez zmian).
5. **Famine (E4/6):** dwa nowe odczyty pod jego `Gate`; b_f lustrem FamN (R1-1); `Returned`/`ReturnGate` bez zmian; kolejnosc doby Famine -> VillageBook (Z6); ShelterPostfix w oblezeniu,
   nasz postfiks w oblezeniu nic nie robi - bez interakcji. Glod obnizajacy plon (q z modelu) liczy E4/6 sam - E4/11 go nie dotyka.
6. **FoodChain (E4/6a, lancuch jedzenia PARTII - RBL):** inne cele (modele jedzenia partii), brak wspolnego celu i stanu (grep: FoodChain nie dotyka SettlementFoodModel).
7. **WinterRealm / WinterBite:** wspolny cel `CalculateTownFoodStocksChange` z WinterBite (addytywnie); linia WinterRealm o spichlerzach warowni (B:WinterRealm.cs:527) liczy tylko
   skladnik zimy - bez zmian; `VillageCut` nieczytany.
8. **MaterialLaw:** bez zmian (zadnego trzeciego czynnika w ProdPostfix, B:MaterialLaw.cs:166-196).
9. **PeopleBook:** tylko odczyty; stan E4/11 w osobnym kluczu (`arm_district`), nie w `arm_people` (ksiega wiosek nie ma ludzi - WM 6.1); Reset w konstruktorze.
10. **Martwy kod ozywa:** tekst `arm_dist_*` (B:VillageTexts.cs:32-41) dzis nieuzywany - zaczyna dzialac w menu; postfiks warowni to nowa praca w modelu wolanym czesto (Town.FoodChange,
    AI) - szybka sciezka; korekta Looted podnosi Food warowni na pograniczu (z31A: Looted 6-12 wsi dziennie) - G8/G9.
11. **Kolejnosc w dobie:** ScorchedEarth (B :1434) -> Devastation.Daily (:1436) -> Famine.Daily (E46 :1437) -> **VillageBook.Daily** -> ... -> PeopleLedger (:1554) -> Devastation.DayLine (:1555).
    Strike w ticku godzinowym (po Daily) - liczy sie do jutrzejszego uzgodnienia (ARexp).
12. **Gracz = AI:** ta sama regula palenia (gracz rabujacy i rabowany), ten sam postfiks warowni; menu i dymek tylko pokazuja.
13. **Watki:** Strike, Daily, menu, widok - watek glowny; postfiks warowni moze isc z watkow AI - czyta tylko PeopleBook/FreeHands (zamki, podmiana referencji), nie stan VillageBook.
14. **Wyjatki:** per okreg / wioska licznik potkniec, pierwszy blad miejsca do logu; nigdy globalny wylacznik (CLAUDE.md 7, `_tentBroken`). Widok: wyjatek odczytu ksiegi = stare zachowanie dla tej wioski.
15. **Widok:** zadnych nowych encji, czastek ani zmian maski; `ApplyFx` tylko przy zmianie celu (CLAUDE.md 7 "nie dotykaj wizerunkow co klatke").
16. **Menu:** tylko `ShowInquiry` (CLAUDE.md 7 - zakaz `SwitchToMenu` z menu oczekiwania); `village_looted` to zwykle menu, opcja Manage.
17. **Kultury ROT:** `IsWhiteWalker` (warownie Innych) - ROT zwraca 0 przed baza (ROTd:16153), postfiks nie biegnie; okregi Strazy i za Murem - male n, wioski pomniejszone (WM 3).
18. **Kalendarz:** "rok" = `RefugeeReturnDays` 364 (rok Armoury, CLAUDE.md 7) - ten sam w E4/5, w rocznicy i w progu; dni w menu - doby.

---

## 11. Nowa kampania (NK) i miejsce dla etapu 7

- **NK: czesciowo.** Stary zapis: n z pierwszego odczytu po wgraniu (ludzie z chwili wgrania, bez skoku), puste osady z konta uchodzcow przydzialem zastepczym (3.7), uklad z TSV
  (ten sam co widok). Pelny obraz (n z ludnosci startu po tabeli krain E4/3, historia spalen od doby 1) - nowa kampania Jeffa po etapie (P 5.4).
- **7.9 (napisy nad spalonymi, dymek lore):** czyta `StateOf(uid)` (stan Burned/Abandoned, BurnDay) - nic w zapisie nie dochodzi.
- **7.10 (komunikaty, wyglad spalonej, trwale spalona wies gry):** zdarzenia "spalona/odbudowana" sa juz w ksiedze (linia doby); 7.10 dostanie `TakeEvents()` i maske "looted" na kopii (WM 5.4-5.5).
- **7.11 (rabunek osada po osadzie):** zrodlo osad = krok rabunku zamiast proporcji - wola to samo `VillageBook.Burn(v, osady, kind, session, src)` i `Plan(...)`; sekcje `r:`/`c:`/`u:` w `arm_district`;
  straznik w RaidPrefix/EndPrefix E4/5 (RB K11) - po stronie 7.11.

---

## 12. Kolejnosc kodowania (galaz `w-toku/e4-11` od zbioru z E4/6 + E4/7 po recenzjach; kazdy krok: build Release kod 0, osobny commit; jeden test zbiorczy na koncu)
1. **K1 ksiega bez skutku:** `MapVillageData.DataPath` internal (Z10); `VillageBook.cs` (2, 3.1, 3.2, 3.6, 3.7, Reset/Export/Import/Resolve, linia pierwszej doby); ArmouryBehavior (konstruktor,
   SyncData, OnSessionLaunched, Daily po Famine); klucze `VillageBookEnabled`, `DistrictSettlementPeople`.
2. **K2 palenie i uzgodnienie:** Devastation (Z1, Z2, `MaxShare`, `StopOf` - Z3), Famine (Z4, Z5), E4/9 Z11 (gdy E4/9 w zbiorze), 3.3-3.5, stany, rocznica (wszystkie okregi), linia doby, CSV.
   W K1: `MapVillageRecord.RoadDist` + `Fill` (Z10).
3. **K3 widok:** `PollRaids` (FireUid otwiera sesje, `SmokeIdx`), `OnHover` (ludzie i stan), slownik uid -> Slot.
4. **K4 menu:** `PeopleMenu` (sekcja okregu, `village_looted` index 0), teksty `VillageTexts`.
5. **K5 zywnosc warowni:** `TownFoodPostfix`, `ApplyAll` + linia startowa (U25, U26), klucz `TownFoodByVillagePeople`, odcinek linii doby.
6. **K6:** `Settings.cs` (komentarze po polsku bez znakow, podpowiedzi EN) + `python tools/gen_mcm.py`; `tools/sprawdz_logi.py` blok `etap4_e411` (G1-G14, G-ETAP z CSV);
   CHANGELOG (Problem: A02 L4 "wioski to sam obraz", dymek 250 x osady, warownia od poziomu i 0 przy Looted - z liczbami z31A; Przyczyna; Zmiana; Odstepstwa: menu w "People of this land"
   zamiast osobnej opcji "Look over the district" (WM 1.6) - decyzja zadania/E4/7; dwie pule b_r/b_f; wielkosc wioski z kolumny `settlements` pliku v4 "wedlug ludnosci"
   (16/27/8), nie 40/24/12 wedlug klasy (WM 3) - wybor Jeffa P = 4000 (SP:393); nastepna wioska lancucha najblizsza w linii prostej, nie "po drodze" Dijkstra (WM 6.2); Ryzyko = rozdz. 10; Status DO SPRAWDZENIA); dwie recenzje diffu (ksiega i kolizje;
   widok i menu), potem test zbiorczy.

---

## 13. Pytania otwarte (tylko kierunek; reszte decyduje plan)

1. **Wyglad spalonej wioski (zgliszcza, ciemne domy) i komunikat "X has been burned" - teraz czy z 7.10?** Rada: z 7.10 (zmiany wizerunkow mapy to osobna proba ze zdjeciami;
   E4/11 = ksiega, ogien na wlasciwej wiosce, dymek ze stanem, menu). Bez odpowiedzi - 7.10.
2. **Niebezpieczenstwo okolicy (przyrost i "las" przy spustoszeniu) z udzialu pustych osad zamiast stanu "spladrowana" wsi gry?** Rada: nie w E4/11 - po pomiarze z biegu 728 dob,
   osobnym krokiem (dzis `PopulationLaw.Danger` B:PopulationLaw.cs:503-521 liczy wsie Looted/BeingRaided).
3. **Mieszanka okregu wedlug obrazkow** (wioska rybacka daje ryby, mlyn zboze - A02 A4)? Rada: nie teraz (etap 6/7 - produkcja i towary).
Informacja (bez pytania): zywnosc warowni z wiosek bez dodatkowego ciecia zimy (jedno zrodlo zimy warowni - WinterBite); wies spladrowana zywi warownie ludzmi, ktorzy zostali
(Twoja zasada z 07.10); spalona = polowa osad wioski (Twoja decyzja); spalona wioska wstaje najpozniej rok po ostatnim najezdzie w okolicy (Twoje 09.10 i 11.10).

---

## 14. Krytyka - co przyjeto/odrzucono (11.10.2026, niezalezny przeglad w kodzie i dekompilacji; kodu nie zmieniano)

Sprawdzone w zrodle (nie z cytatow): B = `git show dfb12df`, E46 @ 0cd60f1, E47 @ 61f75d7, G = dec32\cs_full, BK = bkfull, ROTd, TSV w grze (b742f307), PLAN-KODU-E49.

**Prawdziwe bledy - poprawione w miejscu:**
| # | Blad | Dowod | Poprawka (gdzie) |
|---|---|---|---|
| K1 | Lancuch palenia (3.3) i kolejnosc glodu (3.4) licza na `road_dist`, a parser widoku go nie czyta - `MapVillageRecord` nie ma pola, `Fill` nie czyta kolumny ("ten sam parser, bez zmian" nie wystarcza) | B:MapVillagesView.cs:70-91, :239-265; TSV: kolumna 14 w 2 446/2 446 wierszy | Z10, rozdz. 2, K1: pole `RoadDist` opcjonalne; brak = linia prosta |
| K2 | Sesja kluczowana `RaidState` jest niewidoczna dla widoku: przez walke z milicja i do pierwszego kroku gry (Settle tylko przy `took > 0`) ogien stoi na `FirstUsable`, potem skacze; log "ogien nad X" pisany tylko przy starcie ognia -> zawsze "(lancuch gry)", G6 nieosiagalny | B:Devastation.cs:356-361, :369-375; B:MapVillagesView.cs:1878-1888 | klucz = `RaidEventComponent` (`MapEvent.Component`, G MapEvent.cs:114; `RaidState.Comp`), sesje otwiera pierwszy z `FireUid`/`OnStrike` ta sama funkcja (Z1, Z2, 3.1, 3.3, 5.2, G6) |
| K3 | Dym w czujnosci "na ostatniej wiosce sesji", a sesja usuwana przy `!IsUnderRaid` - po najblizszej dobie dym przeskakiwal na `FirstUsable` | plan 3.5 vs 5.2 | `SmokeIdx` do konca `IsRaided` (3.1, 3.5, 5.2) |
| K4 | Rocznica liczona tylko w petli "B > 0 albo konto > 0" - okreg, ktory wstal w last+363, w last+364 z petli wypada; G-ETAP nie mialby wierszy TAK | plan 3.5 (filtr) + E46:Devastation.cs:664-665 (calosc wraca w last+363) | rocznica we wszystkich 571 okregach; wiersz CSV dla kazdego okregu z rocznica (3.5, rozdz. 8) |
| K5 | Brama E4/6 (`famStop` 1 - glod gospodarzy, dla konta w innym regionie pole/spichlerz; 2 - oblezone miejsce pobytu) wstrzymuje CALE konto, takze czesc z rabunku; plan znal tylko "oblezenie regionu" i "A_f > 0" -> takie okregi wpadalyby w "bez przyczyny" (falszywy NIE w G-ETAP). `Famine._gateTouched` nie do uzycia - czyszczony w Famine.Daily przed VillageBook.Daily | E46:Devastation.cs:655-659; E46:Famine.cs:309-321, :377-382, :525-526 | nowy odczyt `Devastation.StopOf` (Z3), przyczyny w 3.5, CSV, G-ETAP; 0 pkt 5 |
| K6 | "A_r < 0.5 -> Br = 0" zostawia konto do 0.5 czlowieka bez pustych osad: r2 do 0.5 > 0.01 (G3) i k2 > 0 (G4) z definicji | plan 3.5 vs 3.6, G3, G4; konto zerowane co do 1e-9 (B:PeopleBook.cs RefugeesHome/RefugeesDie) | prog 1e-6; "< 1" tylko w wyniku rocznicy (3.5) |
| K7 | E4/9 (plan rownolegly) przepuszcza pojmanych PRZEZ konto UCHODZCY (`RefugeesToCaptives` przy koncu rabunku) - 3.5 wzieloby to za powrot: wioska spalona przy 50% spada pod 47% nazajutrz przy kazdym rabunku z niewola; 10.1 mylnie mowilo "pojmani nie na koncie" | PLAN-KODU-E49 :51, :314, 6.3 | Z11 `OnRefugeesTaken` -> `ARexp -= n`, b bez zmian; 10.1 |
| K8 | Wpiecie Z4 miedzy `FamineFled` a synchronizacja `FamN` w tym samym udanym kroku - wbrew "po kontach" (U24) | E46:Famine.cs:853-870 | na koncu udanej sciezki, przed `return true` (Z4) |
| K9 | "nadmiar oczekiwane ~0" - w okregach przy pulapie nie 0 (Room liczy tylko uchodzcow, dB takze zabitych i las) | B:Devastation.cs Room; przyklad liczbowy w 3.3 | opis 3.3 (G5 juz go dopuszcza) |

**Sprawdzone i zgodne z planem (zarzut odrzucony):**
- Okno latki warowni: `DefaultSettlementFoodModel.CalculateTownFoodStocksChange` -> `CalculateTownFoodChangeInternal`, skladnik wsi tylko poza oblezeniem, Normal = (poziom + 1) x 6, inaczej 0 (G :38-41, :63-77). BK wola `base` (BKFoodModel.cs:37-48, FoodModel : DefaultSettlementFoodModel), ROT owija i dla Innych zwraca `default` przed poprzednim modelem (ROTd:16151-16157). Budynki `FoodProduction` w grze tylko `Add` (DefaultBuildingTypes.cs:285, Building.cs:161-183) - korekta addytywna nie gubi mnoznika (INFO: gdyby mod dodal `AddFactor`, korekta by go nie dostala).
- Nic drugi raz: przy e = 1 `YieldFactor` = suma po wioskach (w domu_w / (dom + A)); plon spichlerza E4/6 = 1.2 x L x q z modelu z `FoodPostfix` = 1.2 x dom x q0 - E4/11 nie dodaje mnoznika. Skladnik wsi warowni nie byl dotad ruszany przez zadna nasza latke (grep E46/E47: tylko `WinterBite.TownFoodPostfix` - od dobrobytu - i `Famine.ShelterPostfix` - w oblezeniu); `LevelPostfix` E4/5 tylko PODNOSI poziom, wiec czynnik rak w warowni to pierwsze ciecie; `FreeHands` L idzie tylko w towar i zywnosc wsi (E47:FreeHands.cs:338-357, MaterialLaw :203). FoodChain - inne cele.
- Menu: `PeopleMenu` E4/7 konczy `ShowInquiry` (E47:PeopleMenu.cs:88), bez `SwitchToMenu`; `village_looted` to zwykle menu (G VillageHostileActionCampaignBehavior.cs:118-119), wejscie przez `SwitchToMenu` gry w `game_menu_village_on_init` (G PlayerTownVisitCampaignBehavior.cs:1104-1115) - nasza opcja go nie wola; `AddOption` z index 0 (G GameMenu.cs:146-156), `GetPresumedGameMenu` (G CampaignGameStarter.cs:98-107) - kolejnosc rejestracji obojetna.
- Ludzie: E4/11 nie wola `Move` i nie pisze hearth; wszystkie wyjscia z konta (powrot, podloga 10, smierc, glod uchodzcow, E4/9 z Z11) i wejscia (Strike, FamineFled) obsluzone; `FamPart` dzieli ubytek proporcjonalnie, powroty wedlug czesci (E46:Famine.cs:327-358) - A_r = A - A_f spojne.
- Jedzenie: skladnik wsi warowni to symbol gry (bez platnika - jak dotad); zmiana tylko go skaluje i daje go wsi Looted wedlug ludzi (SP:459-460). Zamkniecie "spichlerz miasta z okregow" (GL 2.13 krok B) swiadomie odlozone (P rozdz. 7) - nie blad tej paczki.
- Zapis: `SaveText.Sync` tnie po 8000 znakow, brak klucza = null -> 3.7 (B:SaveText.cs:31-55); wpiecie obok `arm_people` we wlasnym try. Koszt: Daily O(okregow z b/kontem) + 571 odczytow rocznicy, widok - odczyt slownika, postfiks - <= 5 wsi z szybka sciezka. NK - jak rozdz. 11.
- TSV: 2 446 wiosek, 425 okregow, 42 092 osad, `settlements` 16 (1 981) / 27 (280) / 8 (35), klasy R 2 093 / H 318 / F 35, wiosek na okreg 1-5-18, `neighbors` 2 398 - zgodne z rozdz. 0 [pomiar w tej krytyce].

**Przyjete jako odstepstwa (do CHANGELOG, bez zmiany kodu planu):** wielkosc wioski z kolumny pliku, nie 40/24/12 (WM 3); nastepna wioska w linii prostej, nie Dijkstra (WM 6.2) - dopisane w K6 rozdz. 12. Dwie linie plonu w oknie (E4/7 "Harvest of the fields" = L regionu, E4/11 "The district's harvest" = Y okregu) - rozne miary, rozne podpisy; bez zmian.

**Otwarte (koordynacja, nie pytania do Jeffa):** Z11 zalezy od kolejnosci E4/9 i E4/11 - gdy E4/9 wchodzi pozniej, wpiecie robi E4/9 (lista scalania). Pytania 1-3 do Jeffa z rozdz. 13 bez zmian.
