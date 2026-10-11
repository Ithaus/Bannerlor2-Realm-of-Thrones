# PLAN KODU - paczka E4/9 (192): krainy kanonu - Nocna Straz z Westeros i dozywotnio, Wolni Ludzie, niewola tylko w kanonie, jency z rabunku = pojmani z R8, Zelazne Wyspy 27 tys. niewolnych, DLUG 4

Status: SAM PLAN (11.10.2026). Kod, gra, ustawienia, zapisy nietkniete; nic nie budowane, nic nie commitowane.
Wiazace: `docs\etap4\PROJEKT-ETAP4-KSIEGA-LUDZI-ZIMA-2026-10-10.md` v2 (dalej P; E4/9 = P:503-520, R14 :160, R15 :161, R5 :151, R6 :152, R8 :154, progi rozdz. 4
:536-554, ryzyka 5.1-5.4 :558-616, "Pogodzone" :173-186 - m.in. "Straz 3% (Jeff) zamiast 5% (RK:208)" :179, U22 :183-185), `docs\etap4\KRYTYKA-E4.md` (KR: U5 :115-121,
U14 :194-198, U15b :200-215, U22 :256-260), `docs\REGULY-KRAIN-I-DLUGU-2026-10-06.md` (RK: Straz 1.1-1.7 :162-321, Zelazne Wyspy 2.1-2.7 :325-455, Wolni Ludzie 3.2-3.5 :475-535,
dlug 5.2 D5 :737, 5.4 :801, 5.10 :869), decyzje Jeffa (`docs\STAN-PRAC.md`): 11.10 (SP:1132-1172 - kraina bez wolnych rak nie wystawia nowego wojska TAK, ludnosc w wojnie moze
spadac TAK, ZASADA TESTOW: kod kilku paczek -> jeden test; 07:00 "reszta etapu 4 bez testow po drodze, jeden test na koniec" - PRZEKAZANIE-2026-10-08.md), 05.10 (SP:326-375:
Straz "sluzba dozywotnia", rekrut z Westeros - najpierw pule wyrzutkow, potem ludnosc; Zelazne prog ok. 40% - pracuja niewolni), 07.10 pkt 6a (SP:444-445: jency z rabunku tylko
kultury z niewola, ludzie zdjeci z napadnietej wsi). Wzor formatu: `docs\etap4\PLAN-KODU-E47.md` (dalej E47).

Skroty miejsc:
- **K:** = `scratchpad\e4\47\Armoury\src\` - galaz `w-toku/e4-7`, commit 63c03d3 = zbior 31 (dfb12df) + E4/7 K1-K5 (kod E4/7 w toku, recenzji jeszcze nie bylo - linie z tego drzewa).
  Pliki nieruszone przez E4/7 sa identyczne z dfb12df (`git -C scratchpad\e4\46 show dfb12df:Armoury/src/<plik>`).
- **K46:** = `scratchpad\e4\46\Armoury\src\` (E4/6, 1f62f61, od dfb12df). **E48** = `docs\etap4\PLAN-KODU-E48.md` (kodu E4/8 jeszcze nie ma), **E410** = `PLAN-KODU-E410.md`.
- **G:** = `scratchpad\dec32\cs_full\` (TaleWorlds.CampaignSystem 1.4.8), **BK:** = `scratchpad\e4tools\bkfull\` (BannerKings.Redux), **ROT:** = `scratchpad\dec37\rot\` (ROT.dll).
- **z31A** = bieg testowy zbioru 31 (`scratchpad\kopia-z31-A\Armoury-2026-10-11_08-09-11.log`, 120 dob); **b728** = bieg 2-letni (`scratchpad\kopia-728\Armoury-2026-10-10_20-58-02.log`).

---

## 0. Streszczenie decyzji planu (co wykonawca robi, czego nie)

1. **Wlasciciel stanu = `PeopleBook` (U23).** Nowy plik czesciowy `PeopleBook.Canon.cs` (ta sama klasa `partial`, wzor E4/7 `PeopleBook.Origin.cs`): konto NIEWOLNI (`nw:`),
   konto MUR (`mw:` - ludzie przy Murze poza sluzba: skazancy I1 i bracia, ktorzy wrocili), bilans otwarcia niewolnych (`ns:`), trzy pola `rg:`. Mechanika (latki, linie)
   w dwoch nowych klasach bez stanu w zapisie: `WatchLaw.cs` (R14 + H1) i `ThrallLaw.cs` (R15 + jency z rabunku). DLUG 4 - w istniejacych `OverflowHome`/`WarLedger`.
2. **Straz = rekrutacja wedlug zapotrzebowania, nie "wpychanie" ludzi** (RK 1.3 pkt 3-5, "przechwytywac stanem, nie hakami na kanaly" RK:188-189; P "Pogodzone" :179 - 3% to
   budzet X z tabeli RK:203-211). Kanaly gry (notable Daru i Westeros, A5, szablon nowej partii, przyrost zalog z modelu, uzgodnienie doby) decyduja, ILU braci przybywa;
   E4/9 decyduje, SKAD sa: jedna funkcja `WatchLaw.Levy(n)` -> (1) konto MUR (przestepcy z I1 i bracia z powrotem - "przyjmowani zawsze"), (2) pule wyrzutkow regionow Westeros
   w budzecie `WatchPoolSharePercent` (3%) puli regionu na dobe, (3) ludnosc Westeros proporcjonalnie do ludzi regionu (DOM -1/k, `PeopleBook.Move`), (4) reszta "" z nazwa
   ("Straz bez pokrycia - podloga"). "Tylko ponizej pulapu" zapewnia juz pulap 166 - wszystkie kanaly Strazy staja na pulapie (K:ClanBudget.cs:924-927, :989-1016, :1069-1096).
3. **Dozywotnio (U5):** zwolnien 166 dla Strazy juz nie ma (`WatchNoReleases`, K:ClanBudget.cs:633 - wpis 2-n, WGRANIE 29). Klucz P `WatchLifelong` = istniejacy `WatchNoReleases`
   (bez nowego klucza - odstepstwo nazwy). E4/9 domyka reszte drog "do domu", ktore po nadaniu braciom prawdziwego pochodzenia zaczelyby dzialac (E4/7 `HomeKnown` z hearth):
   udzialy wziete z rosteru Strazy niosa znacznik `Shares.FromWatch`; `HomeKnown` przy znaczniku oddaje je do konta MUR zamiast do DOM (H3 rozbici, W4c przepelnienie,
   rozwiazanie partii E47 4.3, rozwiazane bez bitwy E48 10.4). Fizycznie tak jest dzis: zolnierz kultury `nightswatch` wraca do ludnosci BK najblizszej osady Strazy (K:LosersFlee.cs:510-538).
4. **Dezerter Strazy -> pula wyrzutkow Polnocy** (P R14): region puli dla partii Strazy = najblizszy region krainy `battania` (kultura z dnia kalibracji) - w `OnTroopsDeserted`
   i w czesci "do lasu" rozbitych (H3 i bez H3; to samo zjawisko - brat porzuca sluzbe).
5. **H1 Straz nie pali wsi:** postfiks na `CampaignEventDispatcher.AiHourlyTick` (G:...CampaignEventDispatcher.cs:1224) - kazda pozycja `AiBehavior.RaidSettlement` partii lorda
   Strazy dostaje wynik -1 przez `PartyThinkParams.SetBehaviorScore` (G:...PartyThinkParams.cs:98-109), BEZ `Reset` (RK 3.5 :522-528). Petla wyboru ROT startuje od -1 i bierze
   tylko wynik scisle wiekszy (ROT:ROT.HarmonyPatches.Core\AIThinkPatch.cs:113-118) - rabunek nie zostanie wybrany.
6. **Wolni Ludzie - nic nowego w kodzie:** tabela 300 tys. jest od E4/3 (K:PopulationLaw.cs:54), prog 100% bez hamulca i bez L od E4/7 (K:FreeHands.cs:112). E4/9 dopisuje tylko
   progi kontroli (rozdz. 11) - "nie dopisywac przy okazji" (CLAUDE.md 8 pkt 3).
7. **R15 niewola:** dzisiejsze I1/I1b juz trzymaja niewole tylko w kulturach kanonu (lista K:LosersFlee.cs:70-77, prawo K:PrisonerLaw.cs:231-305). E4/9 robi z licznika E4/7
   `Enslaved` (K:PeopleBook.Origin.cs:736-741) prawdziwe konto NIEWOLNI: osada niewoli (region trzymania) x region pochodzenia, bez narodzin, smierc `ThrallYearlyLossPercent`
   (5%/rok). Konto liczy sie do rownania regionu POCHODZENIA (jak JENCY - U1, R0 :122), smierc niewolnego = ubytek regionu pochodzenia ("smierc naturalna konta bez narodzin").
8. **Zelazne Wyspy 27 tys. na start = czesc 500 tys. tabeli** (U15b, P :511-512): w kalibracji nowej kampanii kraina `sturgia` dostaje do DOM 500 tys. - 27 tys. (k wsi mniejsze o 5.4%),
   a 27 tys. idzie na konto NIEWOLNI regionow wysp wedlug ich ludzi (pochodzenie = region wysp). Tylko nowa kampania (NK) - w tym samym wywolaniu `Calibrate`.
   Prog wolnych rak wysp (E4/7 FreeHands) czyta odtad niewolnych z ksiegi (dzis: niewolni BK z 169c - 14 193, prog 30.5%; po E4/9 w nowej kampanii 27 000 -> 40%).
9. **Jency z rabunku (U14, U22):** prefiks `bool` **Priority.First** na BK `BKRaidCaptureBehavior.ExecuteCapture` (BK:...Raids\BKRaidCaptureBehavior.cs:233) zawsze zwraca `false`
   (BK nie dopisuje jencow z niczego i nie placi "dzialki" z niczego - :290-299). Nasza regula w tym samym prefiksie: gdy rabunek wygrany (BK wola tylko wtedy - :143), najezdzca z kultury
   z niewola (ten sam znacznik co spustoszenie `RaidState.Slaver`, K:Devastation.cs:301-302) i dla gracza przelacznik BK "Take" (:235 - zostaje): J = round(pojmani R8 calego rabunku)
   **przechodzi z konta UCHODZCY napadnietej wsi** do `PrisonRoster` najezdzcy jako `villager_<kultura wsi>` z udzialem pochodzenia = region wsi. **Odstepstwo od szkicu E4/5**
   (PLAN-KODU-E45 :199, :212 - "capt -> konto JENCY przy uderzeniu"): przy uderzeniu pojmani zostaja uchodzcami, przechodza dopiero przy koncu wygranego rabunku - bez stanu "w drodze"
   (nie ginie przy zapisie w polowie rabunku), rabunek przegrany albo "Leave" gracza = zostaja uchodzcami. Zerowanie armii (bez okna BK) - bez pojmanych.
10. **DLUG 4 (RK R13, D5 :737):** "rozpuszczenie pocztu do regionow pochodzenia" juz dziala dla zwolnien 166 rodu w zajeciu (D bez dochodu wsi K:ClanBudget.cs:277-286 -> `Releases` ->
    `ReleaseFrom` -> `ToVillage` -> E4/7 `HomeOrNote` regionu pochodzenia, K:ClanBudget.cs:803-820). E4/9 dokleja jedno: rod w zajeciu (`DebtLadder.IsSeized`, K:DebtLadder.cs:143) -
    kazdy, kto odchodzi z braku pieniedzy (limit zoldu gry/BK i zalegly zold WarLedger), idzie DO DOMU (droga W4c `OverflowHome`), nie do lasu ("D5 ma zastapic dezercje dla dluznikow,
    nie dzialac obok" RK:869). Bez nowego prawa zwalniania (jedna regula 166 - odstepstwo od parametrow RK D5 "80% dochodu, 10%/d").
11. **Poza E4/9 (nie dopisywac):** weterani (E4/10), konwoje niewolnikow BK (klasa BK - E48 8.2 zostaje ""), niewolnicy BK w osadach (klasa BK - R0), rece niewolnych w plonie (RK 2.3
    pkt 7 - P go nie bierze), bilans otwarcia pochodzenia dawnych braci (zostaja "" z siewu E4/7 - rozdz. 4.9), wioski poboczne (E4/11).

---

## 1. Zaleznosci (lista do sprawdzenia przy kodzie) i punkty scalania

### 1.1 E4/7 (kod w toku, K 63c03d3) - na czym E4/9 polega
| # | Czego E4/9 uzywa | Gdzie dzis (K) | Co zaklada | Gdy inaczej |
|---|---|---|---|---|
| Z1 | `internal static partial class PeopleBook` | K:PeopleBook.cs:38, K:PeopleBook.Origin.cs:27 | czwarty plik czesciowy `PeopleBook.Canon.cs` | - |
| Z2 | `Shares` (M, Total, Unknown, Known, `Add`, `AddAll`, `Cut`, `None`) | K:PeopleBook.Origin.cs:68-111 | dopisujemy pole `bool FromWatch`; `Cut` kopiuje je do wyniku, `AddAll` je sumuje (OR) | gdy recenzja E4/7 zmieni `Cut` - flaga w kazdym konstruktorze wyniku |
| Z3 | `Take(r, n, cause)` zna `kind` wlasciciela (KWatch) | K:PeopleBook.Origin.cs:510-555, `OrgOf` :481-503, `KindOf` :435-459 (KWatch :446 = `CrownGifts.IsWatchParty`) | w `Take` jedna linia `res.FromWatch = kind == KWatch` + liczniki Strazy (rozdz. 4.8) | - |
| Z4 | `Give`, `GiveNew`, `GiveNone` | :557-586 | Levy daje `Give(roster, shares, FWatch*)` | - |
| Z5 | `HomeKnown`/`HomeOrNote` - jedyna droga powrotu z hearth | :653-690 | przekierowanie znacznika `FromWatch` w `HomeKnown` (zwraca "umieszczonych", wolajacy dalej jak dzis) | jesli E4/7 doda inne wejscie do DOM z udzialami - tez przez `HomeKnown` |
| Z6 | `EnslaveShares(Shares)` / `WallShares(Shares)` + wolajacy | :736-749; wolaja K:PeopleBook.OriginPatches.cs:423 (patrol BK), :539 (`SaleFin`), K:PrisonerLaw.cs:194 (Mur) | E4/9 zmienia sygnature na `EnslaveShares(Shares, Settlement hold)`; `WallShares` przy WatchLaw.On -> konto MUR | - |
| Z7 | `SalePre/SaleFin` okno sprzedazy (Priority.First, przed BK) | K:PeopleBook.OriginPatches.cs:521-544, wpiecie :634-635 | `SaleFin` dostaje `PartyBase __1` (kupujacy) - osada niewoli = `seller.Settlement ?? buyer?.Settlement` (G:...SellPrisonersAction.cs:12) | - |
| Z8 | punkty "Straz -> ''": `DeducePostfix` (:112 `GiveNone(..., FNoneWatch)`), `A5Post` (:164) | K:PeopleBook.OriginPatches.cs:96-120, :143-170 | zamiana na `Give(roster, WatchLaw.Levy(n, kanal), ...)`; galaz "obca partia w osadzie Daru" (:112 druga czesc warunku) zostaje "" | - |
| Z9 | `ReconcileRoster` - nadwyzka rosteru -> "" | :1043-1078 (OrgAdd "" :1064) | dla `kind == KWatch` nadwyzka -> `WatchLaw.Levy` (lapie kanaly bez okna) | - |
| Z10 | `RegRef` + `rg:` (13 pol, po kolei, `Num` znosi dluzsza liste) | :115-118, zapis :252-262, odczyt :300-310 | E4/9 dopisuje 3 pola NA KONCU, po 2 polach E4/8 (`MigIn;MigOut`, E48 3): `TRef;WRef;TDead` (indeksy 15-17) | gdy E4/8 nie wejdzie przed E4/9 - indeksy 13-15 (przy kodzie po NAZWIE pola, nie po liczbie) |
| Z11 | `RegionNum`/`RegionNow`/`RegionView`/`BuildViews` (rownanie) | :1139-1239 | dopisane `T` (niewolni wedlug pochodzenia) i `W` (MUR) do stanu i do rownania | - |
| Z12 | `EnsureSown` zapisuje odniesienia z `RegionNow` | :973-1041 (petla :1022-1028) | `r.TRef = x.T; r.WRef = x.W` w tej samej petli (nowa kampania: bilans otwarcia niewolnych PRZED siewem, bo siew czeka na `PopulationLaw.Calibrated` :975) | - |
| Z13 | `TakeLevy`-podobne zdjecie z DOM: `FromVillages(region, n, causeM, weighted)` (podloga 50 hearth) | :820-846, `FromVillage` :806-818 | Levy (ludnosc) wola `FromVillages(region, 1, MWatchLevy, true)` z partiala; warunek `LevyLive` (:159) - symetria jak E4/7 | - |
| Z14 | `PoolTake(region, n, troopsBefore)` - udzial zolnierzy w puli | :708-734 | Levy (pule) dostaje pochodzenie zolnierza z puli; prosci = region puli | - |
| Z15 | `FreeHands.RealmOf`, `FOf`, `Update` (`_ironF` z 169c) | K:FreeHands.cs:71, :106-115, :118-133 | `_ironF` z konta NIEWOLNI krainy `sturgia` (gdy ThrallLaw.On i bilans otwarcia byl); inaczej jak dzis (169c) | - |
| Z16 | menu `PeopleMenu` | K:PeopleMenu.cs:46-86 | dwie linie "Slaves..." (rozdz. 5.6) | bez E4/7 menu - pomija |
| Z17 | przyczyny `MKinds`/`MName`/`MHearth`, `F*`/`FName` | K:PeopleBook.cs:76-96; K:PeopleBook.Origin.cs:29-49 | E4/9 dopisuje NA KONCU po NAZWIE: `MWatchLevy`(true), `MDebt`(false), `MBackDebt`(true); `FWatchWall`, `FWatchPool`, `FWatchLevy`, `FNoneWatchLeft`, `FCapturedRaid` | **kolizja numerow E4/6 (K46:PeopleBook.cs: MFamineDead=20, MFamineFled=21) z E4/7 (MLevyVillage=20...)** - rozstrzyga scalanie E4/6+E4/7 (nie E4/9); E4/9 bierze nastepne wolne |
| Z18 | linia "Pochodzenie (E4/7)" liczy wejscia/wyjscia po `F*` | K:PeopleBook.Origin.cs:1279-1384 | nowe `F*` pokazuja sie same (tablica nazw); "Straz (do E4/9)" -> "Straz bez pokrycia" | - |

### 1.2 E4/8 (plan E48, kodu brak) - miejsca "Straz - do E4/9", ktore E4/9 zamyka (jedna regula: sluzba w partii, zalodze, milicji i karawanie Strazy - z Westeros przez `Levy`)
| # | Miejsce E4/8 | Dzis w planie E48 | E4/9 |
|---|---|---|---|
| Y1 | 4.1 `GarrisonGrowthPrefix/Fin` (przyrost zalog z modelu) | Straz przepuszczona, licznik "Straz zostawiona do E4/9" (E48 :131-132) | finalizer: przyrost zalogi Strazy -> `Give(zaloga, Levy(d, KanalZaloga))` (przybysze z Westeros); licznik zmienia nazwe na "Straz: przybysze z Westeros (E4/9)" - **pytanie P1 do Jeffa** (rozdz. 15) |
| Y2 | 5.2 `MilitiaDelta` przyrost milicji osady Strazy | `GiveNone(milicja, d, TMilitiaWatch)` (E48 :181) | `Give(milicja, Levy(d, KanalMilicja))` (Dar "tylko do plonu" - P R5 :151) |
| Y3 | 6.2 `SpawnPost` szablon nowej druzyny Strazy | `GiveNone(sklad, n, TSpawnWatch)` (E48 :229) | `Give(sklad, Levy(n, KanalSzablon))` (pulap juz sprawdzil `SpawnPrefix`) |
| Y4 | 7.1 `CaravanPost` karawana z osady Strazy | `GiveNone` (E48 :264) | `Give(sklad, Levy(n, KanalKarawana))` |
| Y5 | 8.2 wedrowcy i konwoje BK z Daru | `GiveNone` (E48 :296) | **normalna regula R19** (z DOM osady Daru i z powrotem) - to migracja rodzin, nie sluzba |
| Y6 | 10.4 rozwiazane bez bitwy -> `ArriveAt` -> `HomeKnown` | E48 :371-375 | znacznik `FromWatch` przekierowuje braci do MUR (rozdz. 4.4) - zero zmian w kodzie E4/8 |
| Y7 | N2 "przyrost z modelu - Straz zostawiona do E4/9 = 0" | E48 :427 | w narzedziu: "... - Straz: przybysze z Westeros (E4/9, pokryci co do czlowieka) = 0" (rozdz. 11) |
Gdy E4/8 wejdzie po E4/9 albo bez tych miejsc - E4/9 nie ma czego zmieniac (E4/7 juz idzie przez Levy); kolejnosc wedlug PRZEKAZANIE 07:00: E4/8 przed E4/9.

### 1.3 Pozostale punkty styku
- **E4/5** (w zbiorze 31): `Devastation.CaptivesLive` (K:Devastation.cs:65-66 - dzis `false`), `RaidState.CapWould/Slaver` (:69-77), `StateOf` (:286-327), `RaidCaptives` (:440-444),
  linia "Ludzie (spustoszenie)" (:731-), konto UCHODZCY (`Ref`, K:PeopleBook.cs:113-120, `RefugeesDie` :569-590 - wzor nowej `RefugeesToCaptives`).
- **E4/6** (K46, rownolegle): ta sama klasa `Ref` dostaje `FamineFled` i stan "f:" (K46:PeopleBook.cs diff :186-187, :314-321) - `RefugeesToCaptives` pod tym samym `RefGate`; scalanie recznie.
- **E4/10** (plan E410): `VetDivert` w `ClanBudget.ToVillage` (E410 3.2 W1) - DLUG 4 idzie ta sama droga (`ToVillagePop` -> `ToVillage`), wiec dostanie weteranow sam ("DLUG 4 ma wolac to samo
  VetDivert" E410 :91); Straz wylaczona z weteranow (E410 :32, :159-160) - zgodne z MUR. Gdy E4/10 wejdzie przed E4/9 - sprawdzic, ze `MDebt` jest w mapie przyczyn VetDivert.
- **E4/3/E4/4**: kalibracja (K:PopulationLaw.cs:91-138), `SeedTowns` (:137), k na osade `SetUnitsFromCalibration` (:135) - bilans otwarcia niewolnych wchodzi miedzy nie (rozdz. 5.4).

### 1.4 Wspolne pliki (scalanie; kazdy krok buduj od czubka E4/8 po jego recenzji)
`PeopleBook.cs` (MKinds, Export/Import, Reset, refugees), `PeopleBook.Origin.cs` (Shares, Take, HomeKnown, Enslave/Wall, RegRef, rownanie, Reconcile, siew), `PeopleBook.OriginPatches.cs`
(Deduce, A5, SaleFin, PatrolInPre), `ArmsFromPeople.cs` (E4/8: Y1-Y4), `Devastation.cs`, `Measure40.cs` (W11 tekst, linia Hearth), `OutlawLaw.cs` (pula: TakeForWatch, region dezercji,
kontrola linii Wyrzutki), `PopulationLaw.cs` (Calibrate), `FreeHands.cs`, `PeopleLedger.cs`, `PeopleMenu.cs`, `ClanBudget.cs`, `OverflowHome.cs`, `WarLedger.cs`, `DebtLadder.cs`,
`ArmouryBehavior.cs`, `SubModuleMain.cs`, `Settings.cs`/`McmSettings.cs` (MCM zawsze od nowa `python tools/gen_mcm.py`), `tools/sprawdz_logi.py`, `tools/gen_mcm.py`.

---

## 2. Pliki i klasy

| Plik | Zmiana |
|---|---|
| NOWY `Armoury/src/PeopleBook.Canon.cs` (ok. 450 linii) | `partial class PeopleBook`: konto NIEWOLNI `_thr` (id regionu trzymania -> id regionu pochodzenia -> ludzie, double), konto MUR `_wall` (pochodzenie -> ludzie, "" dozwolone), bilans otwarcia (`_thrSeeded`, `_thrOpening`); `EnslaveAt`, `ThrallsDaily` (smierc), `SeedThralls`, `ThrallsHeldInRealm`, `ThrallsByOrigin`, `WallAdd`, `WallTake`, `WatchFromPools`, `WatchFromPeople` (kolejka kwot), `RefugeesToCaptives`, `CaptivesToPrison`, `ExportCanon/ImportCanonPart/ResetCanon`, liczniki doby |
| NOWY `Armoury/src/WatchLaw.cs` (ok. 450 linii) | `internal static class WatchLaw`: `On`, `Live`, `Levy(double n, int kanal)` (rozdz. 4.2), `DeserterRegion(MobileParty)`, `ApplyAll(Harmony)` (H1), `AiTickPostfix`, `OnRaidCompleted`, `Daily()` (budzet pul na dobe + linia "Straz z Westeros (E4/9)"), `StartLine()`, `Reset()`, `Last*` (migawka dla PeopleLedger), `Stumble` (licznik per miejsce) |
| NOWY `Armoury/src/ThrallLaw.cs` (ok. 300 linii) | `internal static class ThrallLaw`: `On`, `OpeningFor(culture, pop)`, `ApplyAll(Harmony)` (prefiks ExecuteCapture), `CapturePrefix`, `Daily()` (smierc 5%/rok przez PeopleBook + linia "Niewolni (E4/9)"), `Reset()` |
| `PeopleBook.cs` | przyczyny Z17; `Export` (:153-197) -> `ExportCanon(sb)` we wlasnym try; `Import` (:246-) -> `ImportCanonPart(part)` obok `ImportOriginPart` (:259); `Reset` (:129-138) -> `ResetCanon()`; `RefugeesToCaptives` (wzor :569-590) + licznik `_dRefCapt` w kontroli konta UCHODZCY |
| `PeopleBook.Origin.cs` | Z2 (`FromWatch`), Z3 (`Take`), Z5 (`HomeKnown`), Z6 (`EnslaveShares(Shares, Settlement)`, `WallShares`), Z9 (`ReconcileRoster` KWatch), Z10-Z12 (`RegRef`, `rg:`, `RegionNum/View`, rownanie, siew), Z17 (F*) |
| `PeopleBook.OriginPatches.cs` | Z7 (`SaleFin(..., PartyBase __1, ...)` -> `EnslaveShares(..., hold)`), `PatrolInPre` :423 -> `EnslaveShares(..., __1)`, Z8 (Levy w `DeducePostfix` i `A5Post`) |
| `ArmsFromPeople.cs` (E4/8) | Y1-Y5 |
| `Devastation.cs` | `CaptivesLive` -> klucz (`RaidCaptivesFromVillage` i `On`); `Strike` (:216-258) - udzial pojmanych przy uderzeniu ZOSTAJE 0 (komentarz z powodem, rozdz. 6.3); nowe `TakeCaptives(Village v, out double planned)`; liczniki pojmanych dnia i odcinek w `DayLine` (:731-) |
| `Measure40.cs` | W11 (:603-622, tekst :1001): "z rabunku: pojmani z wsi (E4/9) +J w r rabunkach, z niczego (BK) +X"; linia Hearth (:867-907): `MWatchLevy` (ujemne) i `MBackDebt` w `named` i w tekscie |
| `OutlawLaw.cs` | `TakeForWatch(Settlement region, float men, out float troops)` (wzor `TakeForFloor` :401-422); region puli dla Strazy w `OnTroopsDeserted` (:543), `OnMapEventEnded` (:571) i `RoutedH3Body` (:664); licznik `_pWatch` w kontroli linii "Wyrzutki:" (:1171 `outs`, tekst :1179-1191) |
| `PopulationLaw.cs` | `Calibrate` (:91-138): `pop -= ThrallLaw.OpeningFor(kv.Key, pop)` po :114; po `SeedTowns` (:137) `PeopleBook.SeedThralls(...)` we wlasnym try |
| `FreeHands.cs` | Z15 (:126-133) i tekst linii (:293-294: "niewolni ksiegi n (BK m)") |
| `PeopleLedger.cs` | "Straz i Wolni Ludzie (4.15)" (:740-768): odcinek "Straz z Westeros (E4/9)" z `WatchLaw.Last*`; "Krainy (4.15)" (:727): "niewolni (ksiega) X" obok BK |
| `PeopleMenu.cs` | Z16 |
| `ClanBudget.cs` | `ReleaseFrom` (:717-760): licznik zwolnionych ludzi Strazy (`menUnit`) -> `LastReleasedWatch`; `ToVillage` (:803-820): mapa przyczyn + `MDebt -> MBackDebt` |
| `OverflowHome.cs` | `OverPre` (:78-97): rod w zajeciu -> notatka obejmuje WSZYSTKICH dopisanych odchodzacych (`Over = int.MaxValue`, znacznik `Debt`); nowe `NoteAll(TroopRoster)` (dla WarLedger); `SendHome` (:127-160) - przy `Debt` przyczyna `MDebt` i warunek bez `On`; liczniki `_debtHome*` + `LastDebt*` zapamietane w `NewDay` (:50) |
| `WarLedger.cs` | przed `OutlawLaw.OnTroopsDeserted(mp, left)` (:132): rod w zajeciu -> `OverflowHome.NoteAll(left)` |
| `DebtLadder.cs` | `Report` (:967-1015): odcinek "DLUG 4 (E4/9)" z `OverflowHome.LastDebt*` i `ClanBudget` |
| `ArmouryBehavior.cs` | konstruktor (:395): `WatchLaw.Reset(); ThrallLaw.Reset();`; `RegisterEvents` (po :707-710): `CampaignEvents.RaidCompletedEvent.AddNonSerializedListener(this, WatchLaw.OnRaidCompleted)`; `OnDailyTick`: po `OthersRise.Daily()` (:1526) `try { WatchLaw.Daily(); } catch ...` i `try { ThrallLaw.Daily(); } catch ...`; `OnSessionLaunched` (:1152-1216): `WatchLaw.StartLine()` we wlasnym try |
| `SubModuleMain.cs` | po `Devastation.ApplyAll` (:151): `try { WatchLaw.ApplyAll(_harmony); } catch ...` i `try { ThrallLaw.ApplyAll(_harmony); } catch ...` |
| `Settings.cs` + `McmSettings.cs` | 8 kluczy (rozdz. 9) po kluczach E4/8; `python tools/gen_mcm.py` |
| `tools/gen_mcm.py` | `RANGES`: `WatchPoolSharePercent` (0, 12), `IronIslandsStartThralls` (0, 108000), `ThrallYearlyLossPercent` (0, 20) |
| `tools/sprawdz_logi.py` | blok `etap4_e49` (rozdz. 11), wolany z `etap4_main` po E4/7/E4/8 (wzor K:../tools/sprawdz_logi.py:837-839) przy linii startowej "Krainy kanonu (E4/9):"; poprawki parsera (11.2) |
| `CHANGELOG.md`, `docs/STAN-PRAC.md` | wpis (rozdz. 16 K7) |

---

## 3. Stan w zapisie (`arm_people`, `SaveText.Sync` - K:ArmouryBehavior.cs:440)

- `|ns:<1/0>;<bilans otwarcia>` - niewolni: bilans otwarcia zalozony (1 = nowa kampania z E4/9; 0 albo brak sekcji = stary zapis - bez bilansu).
- `|nw:<idRegionuTrzymania>=<idRegionuPochodzenia>:<n>;...,...` - konto NIEWOLNI; pochodzenie "" zapisywane jako `~` (jak E4/7 `so:`); n "0.###".
- `|mw:<idRegionuPochodzenia>=<n>,...` - konto MUR ("" jako `~`).
- `rg:` - 3 pola na koncu (Z10): `TRef` (niewolni z regionu w dniu siewu), `WRef` (MUR z regionu w dniu siewu), `TDead` (zmarli niewolni z regionu od siewu).
- Prefiksy dwuliterowe nie koliduja: istniejace `s: c: v: t: u: r: f:` (E4/2-E4/6), `sw: so: jo: wo: rg:` (E4/7), `tr:` (E4/8), `vs: vt: vh:` (E4/10); dispatch K:PeopleBook.cs:259
  (`part[2] == ':'`) - `ImportCanonPart` zwraca `false` dla nie swoich (jak `ImportOriginPart` K:PeopleBook.Origin.cs:266-269).
- Id ze znakami `| , ; = :` - pomijane (`BadKey`, K:PeopleBook.Origin.cs:185), licznik "nie zapisane" (oczekiwane 0). Smieci liczbowe (NaN, ujemne, nieskonczone) - wpis pominiety.
- Rozmiar [S]: niewolni - ok. 30 regionow trzymania x ok. 40 pochodzen x 20 znakow = ok. 24 KB po roku; MUR - ok. 135 pochodzen x 15 = 2 KB. `SaveText` dzieli w kawalki (K:SaveText.cs:27-31).
- Bez zapisu (Reset w konstruktorze i `ResetCanon`): budzet pul doby, kolejka kwot ludnosci, liczniki doby, cache regionow Polnocy i zrodel Westeros (podmiana referencji raz na dobe).
- `ResetCanon` (z `PeopleBook.Reset` K:PeopleBook.cs:129-138, wolanego z konstruktora K:ArmouryBehavior.cs:395) czysci wszystko z tej listy - stan jednej kampanii nie przecieka (zasada 0).
- Linia po wczytaniu (raz): `Krainy kanonu (E4/9): z zapisu - niewolni N w R regionach trzymania (z P regionow pochodzenia), bilans otwarcia TAK/BRAK (stary zapis), Mur M; nie zapisane 0.`

---

## 4. R14 - Nocna Straz

### 4.1 Kto jest Straza (jeden predykat, juz w kodzie)
`CrownGifts.IsWatchParty(mp)` = (partia lorda, zaloga albo gracz) frakcji `nightswatch` (K:CrownGifts.cs:72-75, `IsWatch` :36) - ten sam co E4/7 `KindOf` (K:PeopleBook.Origin.cs:446),
pulap 166 (K:ClanBudget.cs:335) i E4/8 Z6. Milicja i karawany osad Strazy (Y2, Y4): osada `CrownGifts.IsWatch(st.MapFaction)`. Zrodla Westeros: regiony OutlawLaw, ktorych kraina
(`FreeHands.RealmOf`, kultura z dnia kalibracji K:FreeHands.cs:71) jest w stalej `WatchSourceRealms` = battania, river, reach, aserai, vlandia, vale, stormlands, sturgia, crownlands,
dragonstone (RK 1.3 `WatchSourceCultures` :228; bez freefolk, nightswatch, skagosi). Region Polnocy dla dezerterow: kraina `battania` (RK `WatchDeserterCultures` :257-258).

### 4.2 `WatchLaw.Levy(double n, int kanal)` -> `PeopleBook.Shares` (jedyna droga czlowieka do sluzby Strazy)
- `Live` = `WatchLawEnabled && PeopleBook.OriginLive`; gdy `!Live` -> `Shares.None(n)` z przyczyna "Straz (WatchLaw wylaczone)" (stan E4/7/E4/8). Calosc w try; wyjatek -> "" + potkniecie.
- Kolejnosc (P R14 + RK 1.3 pkt 4-5):
  1. **MUR** (`PeopleBook.WallTake(n)`): proporcjonalnie do skladu konta (z "" wlacznie - brat bez pochodzenia wraca jako ""), bez limitu dziennego ("przestepcy przyjmowani zawsze").
  2. **Pule Westeros** (`PeopleBook.WatchFromPools(rest)`): budzet doby `B_r = WatchPoolSharePercent/100 x pula regionu r` (bez kluczy wightow i kluczy postaci kultury `nightswatch` -
     dezerter nie wraca na Mur, RK:249-250), liczony w `WatchLaw.Daily` dla zrodel 4.1. Wybor regionu dla kazdego czlowieka: kolejka kwot (metoda najwiekszej reszty) z wagami =
     pozostaly budzet; zdjecie `OutlawLaw.TakeForWatch(region, 1, out troops)` - najpierw zolnierze (klucze oddzialow), potem prosci (RK:250-251); udzial: zolnierz ->
     `PeopleBook.PoolTake(region, troops, troopsBefore)` (pochodzenie z `wo:`, K:PeopleBook.Origin.cs:708-734), prosty -> region puli. Licznik `FWatchPool`.
  3. **Ludnosc Westeros** (`PeopleBook.WatchFromPeople(rest)`, tylko przy `LevyLive` K:PeopleBook.Origin.cs:159): wagi = DOM regionow-zrodel z widoku doby (`RegionView.Dom`);
     kolejka kwot `q_r += w_r / suma w` na kazdego czlowieka, region z najwieksza kwota, `FromVillages(region, 1, MWatchLevy, true)` (K:PeopleBook.Origin.cs:820-846: wsie wedlug ludzi,
     podloga 50 hearth, `Move` DOM -> SLUZBA 1/k_v); region bez miejsca - nastepny (najwyzej tyle prob, ile regionow); `q_r -= 1`. Kontrola rozkladu: RK:215 (Reach 291, Westerlands 146,
     Dorzecze 128, Polnoc 109, Dolina 109, Krainy Burzy 91, Dorne 55, Smocza Skala 31, Ziemie Korony 22, Zelazne 18 na 1000 - tabela K:PopulationLaw.cs:44-53). Licznik `FWatchLevy`.
  4. **Reszta** -> "" (`FNoneWatchLeft` "Straz bez pokrycia (podloga)"), oczekiwane ~0.
- **Bez hamulca wolnych rak** (P R5 :151 "Straz bez progu (rekrut z calego Westeros)"); brat z Westeros liczy sie do S regionu pochodzenia w R5 - [S] ok. 22 tys./rok na 7.4 mln mezczyzn
  Westeros = 0.3% rocznie rozlozone, bez wplywu na hamulec (rozdz. 13.1 pkt 9).
- Wolajacy (jedna linia w kazdym): E4/7 `DeducePostfix` (:112, partia Strazy - takze gracz w Strazy, jedna regula), `A5Post` (:164), `ReconcileRoster` (KWatch, Z9); E4/8 Y1-Y4.
  Kanal (do linii): notable/karczma, A5, szablon nowej partii, przyrost zalog z modelu, milicja, karawana, uzgodnienie doby.
- Koszt [S]: ok. 60-85 braci/d (z31A: przybylo 10 186 w 120 dobach) x (135 regionow kolejki + 1 `Move`) - ponizej 1 ms/d.

### 4.3 Konto MUR (P R14 "przestepcy z I1 - przyjmowani zawsze")
- Wejscia: (a) skazancy I1 - `WallShares` (K:PeopleBook.Origin.cs:744-749, wolane z K:PrisonerLaw.cs:194 po `LosersFlee.ToWall` K:LosersFlee.cs:594-630) przy `WatchLaw.Live` -> `WallAdd(shares)`
  zamiast licznika `ToWall` (z31A: na Mur 136 w 120 dobach = 1.1/d); (b) bracia "do domu" z przekierowania 4.4.
- Fizycznie: obie grupy siedza dzis w ludnosci BK osady Strazy (`AddBk` K:LosersFlee.cs:538-, ToWall :624; zolnierz `nightswatch` do najblizszej osady swojej kultury - `ToBk` :510-538).
  Straz werbuje u notabli tych osad - wiec MUR jest pierwszy w kolejce Levy (bez podwojnego zdjecia z Westeros: skazaniec raz, nie skazaniec + nowy czlowiek z Westeros).
- Przy pulapie (kanaly zamkniete) konto czeka - bez smierci i bez limitu (stan w linii; oczekiwane male). Rownanie: MUR jest stanem regionu pochodzenia (4.7).

### 4.4 Dozywotnio - przekierowanie w `HomeKnown`
- `Take` (K:PeopleBook.Origin.cs:510) ustawia `res.FromWatch = kind == KWatch` (kind z `OrgOf` - sklad partii/zalogi Strazy; loch nie). `Cut` (:88-110) kopiuje znacznik.
- `HomeKnown(Shares s, int cause)` (:653): na poczatku `if (s.FromWatch && WatchLaw.Live) { placed = PeopleBook.WallAdd(s czesc znana i ""); ...; return placed; }` - bez `Move`, bez hearth.
  Wolajacy dalej robia to, co dzis: ludnosc BK osady Strazy (`ToBk`), "z szablonu" (zaloga), `Note` reszty. Drogi objete (wszystkie przez `HomeKnown`):
  rozbici H3 (`RoutedH3Body` K:OutlawLaw.cs:657-727 -> `SendHome` K:LosersFlee.cs:400 -> `ToBk` :536 / pkt 2 :448), przepelnienie W4c (`OutlawLaw.OnTroopsDeserted` :531 ->
  `OverflowHome.SendHome` K:OverflowHome.cs:127 -> `ClanBudget.ToVillage` K:ClanBudget.cs:803 -> `HomeOrNote`), rozwiazanie partii (E47 4.3, K:PeopleBook.OriginPatches.cs `DisbandPost`),
  rozwiazane bez bitwy (E48 10.4 `ArriveAt`), zwolnienia 166 gdyby Jeff wylaczyl `WatchNoReleases` (wtedy do MUR, nie do domu - petla zwolnienie -> MUR -> werbunek stoi na pulapie).
- Nie objete (swiadomie, nazwane): brat w niewoli (loch zwyciezcy nie ma znacznika - uwolniony wraca do domu jak kazdy jeniec, E47 4.5/4.8; rzadkie) i dezerter w puli (powrot z puli
  to prawo wyrzutkow, E4/3 - nie zwolnienie przez Straz).
- Licznik "bracia na Mur zamiast do domu" wedlug drogi (H3, W4c, rozwiazanie, bez bitwy, 166) - prog K5.

### 4.5 Dezerter i rozbity "do lasu" -> pula Polnocy
- `WatchLaw.DeserterRegion(MobileParty mp)`: gdy `Live` i `CrownGifts.IsWatchParty(mp)` -> najblizszy pozycji partii region z kraina `battania` (lista regionow Polnocy w pamieci na dobe);
  inaczej `null`. Wolajacy: `var region = WatchLaw.DeserterRegion(party) ?? NearestNode(...)` w K:OutlawLaw.cs:543 (dezercja), :571 (rozbici bez H3, na partie zamiast na bitwe),
  :664 (rozbici H3 - region liczony per partia w petli :670-724; `LosersFlee.NoteWoods` dostaje ten region).
- Udzial pochodzenia idzie z ludzmi (`PoolShares(region, ...)` - bez zmian). Dzis: z31A dezercja Strazy 20 w 120 dobach - male liczby.

### 4.6 H1 - Straz nie pali wsi (`WatchDoesNotRaid`)
- Cel: `CampaignEventDispatcher.AiHourlyTick(MobileParty party, PartyThinkParams partyThinkParams)` (G:...CampaignEventDispatcher.cs:1224-1230, public override; wolany z petli myslenia
  ROT `PartyHourlyAiTick` - prefiks ROT zastepuje metode gry i zwraca `false`, ROT:...AIThinkPatch.cs:83-88, :110, :261; wiec wklejenia przez JIT nie ma - RK:523-524).
- Kto juz lata ten cel: ROT `Patch1` postfiks (ROT:...AIThinkPatch.cs:20-81) - filtruje liste i przy zmianie robi `Reset` + ponowne `AddBehaviorScore` (:74-80) kopiujac krotki (nasz wynik -1
  przechodzi z krotka, gdy ROT idzie po nas; gdy przed nami - poprawiamy jego liste). Pozycje rabunku dodaje tylko `AiMilitaryBehavior` (G:...AiMilitaryBehavior.cs:483 przez nasluch :27, :487).
- Latka: `public static void AiTickPostfix(MobileParty party, PartyThinkParams partyThinkParams)` **Priority.Low**. Warunek: `WatchDoesNotRaid`, `party != null && party.IsLordParty && !party.IsMainParty
  && CrownGifts.IsWatch(party.MapFaction)`. Przebieg (RK :525-528): kopia `AIBehaviorScores` do tablicy; dla kazdej pozycji `AiBehavior == RaidSettlement` z wynikiem > -1:
  `partyThinkParams.SetBehaviorScore(in data, -1f)` (G:...PartyThinkParams.cs:98-109 - zmienia pierwsza rowna pozycje); po petli bezpiecznik: pozostala pozycja rabunku z wynikiem > -1
  (ten sam cel dwa razy) -> licznik "H1: bezpiecznik" (oczekiwane 0; bez pisania do prywatnej listy - RK dopuszczal `AccessTools.Field` - nie robimy przy okazji). Bez `Reset`.
- Wybor celu ROT startuje od -1 i bierze tylko wynik wiekszy (ROT:...AIThinkPatch.cs:113-118; gra G:...AiPartyThinkBehavior.cs:81) - rabunek nie bedzie wybrany, takze przez wodza armii.
  Rabunek trwajacy w chwili wgrania AI przerwie przy najblizszym przemysleniu (RK :538-539).
- Licznik: odrzucone opcje rabunku dziennie; nasluch `RaidCompletedEvent` (`WatchLaw.OnRaidCompleted(BattleSideEnum, RaidEventComponent)` - zwyciestwo i porazka) liczy rabunki z napastnikiem
  frakcji Strazy (prog K6 = 0 od d2). Gracz w Strazy - poza H1 (rozkaz gracza z menu gry; RK :538).

### 4.7 Rownanie (R0) i liczniki Strazy
- Rownanie regionu r (E47 rozdz. 5, K:PeopleBook.Origin.cs:1226-1227) po E4/9:
  `Rest_r = (Dom + S + J + Wy + U + Carts + T + W)_r - (DomRef + SRef + JRef + PoolRef + RefRef + CartRef + TRef + WRef)_r - Born_r + Fallen_r + Civil_r + Enslaved_r + ToWall_r + TDead_r`
  [+ `MigOut_r - MigIn_r` z E4/8]. `T_r` = niewolni z regionu r (wszedzie), `W_r` = MUR z regionu r. `Enslaved`/`ToWall` (liczniki E4/7) przestaja rosnac przy czynnych prawach -
  to, co naliczyly wczesniej (stary zapis z E4/7), zostaje odplywem.
- Liczniki Strazy (dla linii i progu T6): w `Take`/`Give` przy `kind == KWatch` dodatkowe tablice `_wOut[cause]`/`_wIn[cause]` (jedno porownanie na wywolanie - koszt pomijalny);
  `ReconcileRoster` dla KWatch: nadwyzka -> Levy (kanal "uzgodnienie"), niedobor -> "w nicosc" (licznik Strazy).
  Kontrola linii: `stan Strazy dzis - wczoraj - (wejscia - wyjscia + uzgodnienie(+) - w nicosc(-)) = 0.00` - stan = suma `Men()` rosterow KWatch (bez wightow).

### 4.8 Linia i budzet - `WatchLaw.Daily()` (po `OthersRise.Daily()` K:ArmouryBehavior.cs:1526, czyli po uzgodnieniu doby i po `OutlawLaw.Daily()` :1516)
1. Linia "Straz z Westeros (E4/9)" (rozdz. 10.2) z licznikow doby; migawka `Last*` dla PeopleLedger (:1561) i narzedzia; zerowanie licznikow.
2. Budzet pul na nastepna dobe z dzisiejszych pul (po powrotach i werbunku band w `OutlawLaw.Daily`); uzgodnienie nastepnej doby korzysta z reszty tego budzetu.
3. Lista regionow Polnocy i zrodel Westeros (raz na dobe; kultura z dnia kalibracji sie nie zmienia - U3).
Po wczytaniu w srodku doby budzet liczony leniwie przy pierwszym Levy (`_budgetDay != dzis`).

### 4.9 Bilans otwarcia pochodzenia dawnych braci - NIE
Siew E4/7 dal Strazy "" (K:PeopleBook.Origin.cs:945, :1003). RK 1.3 (:268) proponowal rozlozenie stanu na regiony Westeros bez zdejmowania; odrzucone: K_m regionow (R5) liczylby smierc ludzi,
ktorzy w tej kampanii nie wyszli z ich DOM (kraina tracilaby wolne rece "za nic"). Dawni bracia zostaja "" (licznik "Straz bez pochodzenia" maleje z ich smiercia).

---

## 5. R15 - niewola tylko w kulturach kanonu, konto NIEWOLNI

### 5.1 Gdzie czlowiek staje sie niewolnym (wszystkie drogi juz istnieja - zmienia sie tylko ksiega)
| Droga | Kod | Osada niewoli | Dzis w ksiedze |
|---|---|---|---|
| sprzedaz jencow w warowni kraju z niewola (AI, loch, rozwiazanie, gracz u posrednika) - I1b Enslavement | K:PrisonerLaw.cs:255-282 (`LosersFlee.AddSlaves` :265 -> K:LosersFlee.cs:638-653, BK "po staremu" :274-279) | `__1` BK albo `MainParty.CurrentSettlement` (:236-243) | `SaleFin` -> `EnslaveShares` (K:PeopleBook.OriginPatches.cs:532-544) |
| patrol BK wraca do warowni kraju z niewola | K:PrisonerLaw.cs:328-333 (BK robi niewolnikow) | `__1` (cel patrolu) | `PatrolInPre` -> `EnslaveShares` (K:PeopleBook.OriginPatches.cs:414-426) |
| I1 wylaczone (`PrisonerLawEnabled` off) - BK niewoli w kazdej osadzie | BK SettlementPatches.cs:24-41, :79-93 (RK:337) | osada sprzedazy | `SaleFin` -> `EnslaveShares` |
- `EnslaveShares(Shares s, Settlement hold)`: `ThrallLaw.On && hold != null && LosersFlee.SlaveCulture(hold.Culture)` (biezaca kultura osady - I1b, P :133, K:PrisonerLaw.cs:47-48 opis)
  -> `EnslaveAt(OutlawLaw.RegionFor(hold), s)`: `_thr[h][o] += m` dla kazdego (o, m) z `s` (z "" wlacznie); licznik "ze sprzedazy" / "z patroli BK". Inaczej (prawo wylaczone, osada
  spoza kanonu przy I1 wylaczonym) - jak E4/7 (licznik `Enslaved`, nazwa "niewola poza kanonem / prawo wylaczone").
- Osada niewoli w `SaleFin`: `__0.Settlement ?? __1?.Settlement ?? __0.MobileParty?.CurrentSettlement` (G:...SellPrisonersAction.cs:12 + przypadek gracza jak K:PrisonerLaw.cs:236-243).
- Konwoje niewolnikow BK i klasa "Slaves" BK - poza ksiega (R0: BK nie jest ludzmi; E48 8.2 "" z nazwa) - bez zmian.

### 5.2 Smierc - `ThrallLaw.Daily()` -> `PeopleBook.ThrallsDaily()`
`d = T x ThrallYearlyLossPercent / 100 / YearDays()` (K:PopulationLaw.cs YearDays; rok 364) na kazda komorke `_thr[h][o]` (double); `TDead_o += d` (rownanie regionu pochodzenia),
licznik wedlug krainy trzymania; komorka < 1e-6 usuwana. Bez narodzin (R1, P :147). Bez wyzwolen (RK 2.3 pkt 2). Przy `!ThrallLaw.On` - nic (stan zamrozony, w zapisie).

### 5.3 Prog wolnych rak Zelaznych Wysp (E4/7 FreeHands)
`T_iron = PeopleBook.ThrallsHeldInRealm("sturgia")` (komorki `_thr[h]` z `FreeHands.RealmOf(region h) == "sturgia"`); `_ironF = share + min(FreeHandsIronIslandsExtraMax, T_iron / (0.27 x DOM wysp))`
(K:FreeHands.cs:126-132 - zmienia sie tylko zrodlo T). **Gdy ThrallLaw wylaczone albo stary zapis bez bilansu otwarcia (`ns:` brak/0)** - jak dzis: niewolni BK z 169c
(bez regresji progu w starym zapisie: ksiega zaczynalaby od 0 -> prog 20% zamiast dzisiejszych 30.5%). Tekst linii "Wolne rece (E4/7)" mowi, skad T.

### 5.4 Bilans otwarcia - Zelazne Wyspy 27 tys. (NK; U15b, RK:348-349)
- `ThrallLaw.OpeningFor(string culture, float pop)`: zwraca `min(IronIslandsStartThralls x PopulationScale, 0.5 x pop)` gdy `ThrallLaw.On && culture == "sturgia" && !PeopleBook.ThrallsSeeded
  && CampaignTime.Now.ElapsedDaysUntilNow < 2` (ta sama granica co bilans cial E4/2 - K:PeopleBook.cs:54 `OpeningDays`); inaczej 0.
- `PopulationLaw.Calibrate` (K:PopulationLaw.cs:91-138): po :114 `float thr = ThrallLaw.OpeningFor(kv.Key, pop); pop -= thr; if (thr > 0) thrOpening = thr;` - k wsi i miast wysp liczone z 473 tys.;
  log kalibracji dopisuje "sturgia 0.47M + niewolni 27 000 (czesc tabeli 0.50M, E4/9)". Po `SeedTowns` (:137): `PeopleBook.SeedThralls(thrOpening)` - rozklad na regiony wysp wedlug ludzi
  regionu (DOM wsi + stan miasta, `DomOf`), komorka `_thr[h][h]` (pochodzenie = region wysp - czesc ich tabeli), `ns:1;27000`. Ten sam warunek i to samo wywolanie = atomowo
  (nigdy odjete z DOM bez konta albo odwrotnie). `Calibrate` biegnie tylko przy pustym `_k` (:93) - w zapisie `_k` jest z `arm_population`, wiec stary zapis nie dostaje bilansu.
- Siew E4/7 (pozniej tej samej albo nastepnej doby, czeka na `Calibrated` K:PeopleBook.Origin.cs:975) zapisuje `TRef_wysp = 27 000` - rownanie wysp: DomRef 473 tys. + TRef 27 tys. = 500 tys.
- `TablePeopleOf` (`_kTab` K:PopulationLaw.cs:181 - suma tabeli z niewolnymi) - bez zmian: to "wszyscy ludzie osady" dla rak warsztatow miast (WorkshopLaw max).
- Linia raz: `Niewolni (E4/9): bilans otwarcia nowej kampanii - Zelazne Wyspy 27 000 (czesc tabeli 500 000: wsie i miasta wysp 473 000) w R regionach: Pyke x, Lordsport y, ...`.

### 5.5 Dlaczego niewolni nie licza sie do S regionu pochodzenia (R5)
Bez zmiany definicji U19 (E47 6.2): S = SLUZBA + JENCY z regionu. Niewolny wychodzi z JENCY (Take z lochu sprzedajacego) i nie wraca - E4/7 tak robi juz dzis (licznik `Enslaved`, bez K_m).
Wpisane jako znana uproszczenie (rozdz. 13.1 pkt 7), nie zmieniamy przy okazji.

### 5.6 Menu osady (Z16; napisy po angielsku)
W oknie "People of this land" (K:PeopleMenu.cs:65-80), gdy `ThrallLaw.On`:
- `Slaves held in this land: X` (komorki `_thr[region]`, gdy X >= 1; dla wysp dopisek `(they work the fields, so more free men can go reaving)`);
- `Taken from this land into slavery: Y` (zywi niewolni z pochodzeniem = region, gdy Y >= 1).

---

## 6. Jency z rabunku (U14, U22) - prefiks na BK `ExecuteCapture`

### 6.1 Dowod w dekompilacji BK
- `private void ExecuteCapture(MobileParty attackerParty, Hero leader, Clan capturingClan, Village village, int totalAttackerTroops, bool fromCheat)` (BK:...Raids\BKRaidCaptureBehavior.cs:233-305).
  Wolaja ja tylko: `OnRaidCompleted` (:141-172 - nasluch `RaidCompletedEvent` :33; wychodzi gdy funkcja BK wylaczona, wygral obronca, brak partii, banda BK, brak wodza albo rodu) i
  `ForceCapture` (:201-231, konsola `BannerKingsCheats.cs:1782`). Innego zrodla jencow z rabunku w BK nie ma (`BKBanditBehavior.OnRaidCompleted` pusty - BK:...BKBanditBehavior.cs:58-60).
- Decyzja BK (:235): gracz - polityka `Take/Leave` (domyslnie Take - BK:...RaidCapturePolicyManager.cs:76-79), AI - `ClanRealmAllowsSlavery` (:38-74: rod bez krolestwa, prawo Slavery Nord/Aserai,
  albo siedziba-miasto z polityka Enslavement - czyli wiekszosc rodow, RK 0.1.3). Liczba `ProjectedCaptives` (:241), "dzialka" w zlocie z niczego `GiveGoldAction.ApplyBetweenCharacters(null, leader, ...)`
  (:290-299), jency `villager_<kultura>` do `PrisonRoster` (:268-288), komunikat gracza (:300-304).
- Zdarzenie pada w `RaidEventComponent.OnBeforeFinalize` (G:...RaidEventComponent.cs:128-138) - PO naszym `Devastation.EndPrefix` (rozliczenie ostatnich osobodni, K:Devastation.cs:381-400)
  i PRZED `EndFinalizer` (:402-438, ktory usuwa `RaidState`) - stan rabunku z `CapWould` istnieje w chwili prefiksu.
- z31A: BK dal 2 219 jencow w 57 rabunkach (18.5/d) z niczego; R8 "pojmani byloby" przy najezdzcach z niewola - np. d120 17.6.

### 6.2 Latka `ThrallLaw.CapturePrefix` - **Priority.First**, `bool`
`public static bool CapturePrefix(object __instance, MobileParty __0, Hero __1, Clan __2, Village __3, int __4, bool __5)`:
1. `!RaidCaptivesFromVillage || !Devastation.On` -> `return true` (BK jak dzis, liczone przez W11). Wyjatek gdziekolwiek ponizej -> potkniecie, `return false` (BK nie robi ludzi z niczego;
   nasz krok jeszcze nic nie zmienil albo juz zakonczony - nic nie stoi miedzy zdjeciem a dopisaniem, U24).
2. `double plan; var rs = Devastation.TakeCaptives(__3, out plan)` - stan rabunku tej wsi (pod `_lock`, po `MapEventSettlement == __3.Settlement`), `plan = rs.CapWould` (R8: 10% trafionych calego
   rabunku tylko przy `rs.Slaver`), znacznik `rs.CapTaken` (drugie wywolanie = 0). Brak stanu (konsola BK, wies bez przelicznika) -> plan 0.
3. Gracz (`__2 == Clan.PlayerClan`): polityka BK przez refleksje `__instance.Policies.Get(__2).Mode` (BK:...BKRaidCaptureBehavior.cs:28, RaidCapturePolicyManager.cs:16-28) - `Leave` -> J = 0
   (licznik "gracz: zostaw"). AI - bez dodatkowego warunku (krolestwo z niewola = `rs.Slaver`, ta sama lista co H3/I1 - K:LosersFlee.cs:74-77).
4. `J = (int)Math.Round(plan)`, nie wiecej niz uchodzcy wsi (`PeopleBook.RefugeesOf(__3)`); `taken = PeopleBook.RefugeesToCaptives(__3, J)` (konto UCHODZCY -> w drodze; nowa funkcja wzorem
   `RefugeesDie` K:PeopleBook.cs:569-590, bez smierci, licznik `_dRefCapt` w kontroli konta).
5. Postac: `villager_<kultura wsi>` (`MBObjectManager.Instance.GetObject<CharacterObject>`), brak -> `looter` (ta sama droga zapasowa co BK :274-282); brak obu -> zwrot `taken` na konto (licznik).
6. `__0.PrisonRoster.AddToCounts(postac, taken)`; `PeopleBook.GiveNew(__0.PrisonRoster, OutlawLaw.RegionFor(__3.Settlement), taken, FCapturedRaid)` (JENCY z pochodzeniem = region wsi).
7. Gracz i `taken > 0`: `InformationManager.DisplayMessage(new InformationMessage(taken + " villagers of " + __3.Name + " were taken captive."))` (zamiast komunikatu BK).
8. Liczniki (w `Devastation`, czytane przez Measure40 przed zerowaniem w `DayLine`): rabunkow z pojmanymi, J, plan, "gracz: zostaw", "nie z niewola" (oczekiwane 0), "bez stanu".
9. `return false` - BK nie dopisuje swoich jencow i nie placi dzialki (RK:400).

### 6.3 Zmiany w `Devastation` (E4/5)
- `CaptivesLive` (:65-66) = `On && RaidCaptivesFromVillage` (czytane przez `RaidCaptives`, linie i `ThrallLaw`).
- `Strike` (:216-258): `double cap = 0.0;` zostaje na stale (dzis `CaptivesLive ? capW : 0` :232) z komentarzem "E4/9: pojmani przy uderzeniu zostaja uchodzcami, przechodza do lochu
  najezdzcy przy koncu wygranego rabunku (ThrallLaw.CapturePrefix)" - kontrola E4/5 "trafieni = zabici + pojmani + las + uchodzcy" dalej z pojmanymi 0 (parser S3 bez zmian).
- `TakeCaptives(Village, out double planned)` (nowa, obok `RaidCaptives` :440): pod `_lock`; `RaidState.CapTaken` (nowe pole :69-77).
- Zerowanie armii (`Forage` :260-273) - bez okna BK, bez pojmanych (zostaja uchodzcami) - nazwane w linii.

### 6.4 Okna na tym samym celu (kolizje)
| Kto | Gdzie | Co robi | Po E4/9 |
|---|---|---|---|
| Measure40 W11 `BkCapPre/BkCapFin` (void, finalizer) | K:Measure40.cs:100, :603-622 | roznica lochu najezdzcy = "z rabunku BK (z niczego)" | nasz prefiks First biegnie PRZED nim -> migawka W11 zawiera juz nasze J -> W11 liczy 0; tekst :1001 dostaje "pojmani z wsi (E4/9) +J" z licznika Devastation |
| CirculationWindows O12 `RaidPre/FlagFin` (void) | K:CirculationWindows.cs:616-620, :1788 | okno zlota "jency z rabunkow BK" | brak dzialki - okno widzi 0 |
| RealPayer `RaidPre/CtxFin` (void, Low) | K:RealPayer.cs:182-191, :289 | dzialka placona z kasy najblizszego miasta (E3/1) | brak dzialki - miasto nie placi; platnik zostaje wpiety (P :517) |
Harmony 2 uruchamia wszystkie prefiksy mimo `false` jednego z nich i wszystkie finalizery - okna dzialaja dalej, tylko nic nie mierza.

---

## 7. DLUG 4 - dluznik rozpuszcza poczet do regionow pochodzenia (`DebtorDisbandsHome`)

### 7.1 Co juz jest
- Rod w zajeciu (D3/D4): D budzetu bez dochodu wsi (K:ClanBudget.cs:277-286) -> nizszy pulap -> limity partii (`ApplyPartyLimits`) i zwolnienia 166 (`Releases` K:ClanBudget.cs:628-713,
  najemnicy i najnizszy tier pierwsi - `ReleaseFrom` :717-760) -> `ToVillage` -> E4/7 `HomeOrNote(org, n, MBack166, M166)` - DOM regionu pochodzenia z hearth. To jest D5 RK (zwolnieni wracaja
  do regionu pochodzenia) - jedna regula 166, bez drugiego prawa (RK:869 "nie dublowac").
- Dziura: nizszy limit zoldu (`PaymentLimit`) zdejmuje ludzi dezercja gry (G:...DefaultPartyDesertionModel.cs:50-76: limit zoldu :57-59) -> `OnTroopsDeserted` (G:...DesertionCampaignBehavior.cs:19-44)
  -> pula wyrzutkow (las); zalegly zold -> `WarLedger` (K:WarLedger.cs:114-141) -> `OutlawLaw.OnTroopsDeserted` (:132) -> las. Dezercja dziala co dobe, zwolnienia 166 dopiero po 3 dobach
  ponad pulapem (`BudgetHysteresisDays`) - wiec u dluznika wiekszosc ludzi szlaby do lasu. b728: zajecie w 554 dobach, najwyzej 6 rodow naraz.

### 7.2 Zmiana
- `OverflowHome.OverPre` (K:OverflowHome.cs:78-97; cel `DefaultPartyDesertionModel.GetTroopsToDesertDueToWageAndPartySize`, wpiecie :59-74): gdy `DebtorDisbandsHome && DebtLadder.IsSeized(__0.ActualClan)`
  (K:DebtLadder.cs:143) i partia lorda albo zaloga -> `Snap { Over = int.MaxValue, Debt = true }` takze bez przepelnienia (`over <= 0` nie wychodzi). `OverPost` (:99-122) notuje WSZYSTKICH dopisanych.
- Nowe `OverflowHome.NoteAll(TroopRoster r, bool debt)` - notatka calego rosteru; `WarLedger` przed `OutlawLaw.OnTroopsDeserted(mp, left)` (K:WarLedger.cs:132): `if (rod w zajeciu) OverflowHome.NoteAll(left, true)`.
- `OverflowHome.SendHome` (:127-160): notatka `Debt` -> przyczyna `PeopleBook.MDebt` (nie `MW4c`) i warunek `(On || note.Debt)` zamiast samego `On` (:132); wies jak W4c (`ClanBudget.OverflowVillage`
  - wies rodu dluznika; lenno zostaje, wiec jest). `ClanBudget.ToVillage` (:803-825): mapa `MDebt -> MBackDebt` w :819. Nieprzyjeci (wies bez danych BK) - do puli jak dzis (licznik).
- Liczniki `_debtHomeLimit`, `_debtHomeArrears`, `_debtNoVillage` + migawka `LastDebt*` w `NewDay` (:50; wolane z `OutlawLaw.Daily` K:OutlawLaw.cs:1152 przed `DebtLadder.Report` :1522).
- Sakiewki: jak W4c (dezerterzy nic nie biora; 166 daje wsi czesc sakiewki - znana asymetria opisana w K:OverflowHome.cs:28-29) - bez zmian (etap 4 nie rusza zlota, P :171).
- Gracz w zajeciu (PlayerSameLadder) - ta sama regula (OverflowHome obejmuje partie gracza, :25-27).

---

## 8. Wolni Ludzie - tylko kontrola (P :507-508)
Tabela 300 tys. (K:PopulationLaw.cs:54, kalibracja z31A "freefolk 0.30M (wies 41/hearth)"), prog 100% bez hamulca i L = 1 (K:FreeHands.cs:112), milicja/druzyny bez hamulca (E48 5.4, 6.2 przez
`FreeHands.Braked`), H1 (4.6) chroni ich wsie przed Straza (RK 3.3: -186 zabitych obroncow w 28 dobach). E4/9 nie dopisuje kodu Wolnym Ludziom; progi K25 (rozdz. 11).

---

## 9. Klucze Settings (po kluczach E4/8; `python tools/gen_mcm.py`; komentarze po polsku bez znakow w kodzie, podpowiedzi MCM po angielsku, bez znaku `"`)
W `Armoury.json` Jeffa (Documents\...\ModSettings\Global\Armoury\Armoury.json, 08.10) zadnego z nich dzis nie ma (grep 11.10: 0 trafien dla wszystkich 8 + `WatchNoReleases`,
`DevastationCaptivesPercent`) - przy kodzie powtorzyc (CLAUDE.md 7).

| Klucz | Domyslna | Uzasadnienie | Podpowiedz MCM (EN) |
|---|---|---|---|
| `WatchLawEnabled` | true | P R14 :160, RK 1.3 | every man who joins the Night's Watch comes from the Seven Kingdoms: first the convicts sent to the Wall, then outlaws who take the black, then common folk of every land by its numbers. A brother never goes home: routed or disbanded he walks back to the Wall, and a deserter hides among the outlaws of the North |
| `WatchPoolSharePercent` | 3 | P :179 (Jeff 3% zamiast RK 5%), RK:203-211 | percent of a land's outlaws who may take the black in a single day |
| `WatchDoesNotRaid` | true | P :507, RK 3.4 H1 :511 | the Night's Watch holds the Wall: its rangers fight beyond it but never burn villages |
| `ThrallLawEnabled` | true | P R15 :161, RK 2.3 | captives sold where slavery is the custom (the Iron Islands, Slaver's Bay, the Free Cities that keep slaves, Qarth) become slaves of that land: they bear no children and some die each year; the thralls of the ironborn let more free men go reaving |
| `ThrallYearlyLossPercent` | 5 | P :512, RK:350 | slaves who die each year |
| `IronIslandsStartThralls` | 27000 | P :511-512, RK:348-349 | thralls already working the Iron Islands when a new campaign begins - counted out of the islands' people, not added to them. Takes effect only in a new campaign |
| `RaidCaptivesFromVillage` | true | P :509-511, U14, U22 | captives carried off in a raid are villagers taken from the plundered village - a tenth of those the raid struck - and only raiders whose realm keeps slaves take them; the Banner Kings captives out of thin air end (your own Take or Leave choice stays) |
| `DebtorDisbandsHome` | true | P :513, RK D5 :737 | a house whose creditors hold its villages sends the men it can no longer pay home to their own lands instead of letting them desert to the woods |
Bez nowego klucza: `WatchLifelong` z P = istniejacy `WatchNoReleases` (K:Settings.cs:862); pojmani 10% = istniejacy `DevastationCaptivesPercent` (K:Settings.cs:716 - jego opis juz wspomina RaidCaptivesFromVillage).
Kolejnosc wylaczania przy niezdanym progu (jedna zmiana naraz): `DebtorDisbandsHome`, `WatchDoesNotRaid`, `RaidCaptivesFromVillage`, `ThrallLawEnabled`, `WatchLawEnabled`.

---

## 10. Linie logu (przy LogEnabled)

### 10.1 Startowa (OnSessionLaunched; wlasciciele latek przez `Harmony.GetPatchInfo` jak E4/0 U25)
`Krainy kanonu (E4/9): wpiete: jency z rabunku BK (BKRaidCaptureBehavior.ExecuteCapture - prefiks bool First), Straz nie pali wsi (CampaignEventDispatcher.AiHourlyTick - postfiks Low); BRAK: -; wlasciciele latek celow: <cel: Harmony id, ...>; ksiega pochodzenia E4/7: TAK/BRAK; spustoszenie E4/5: TAK/BRAK; zrodla Strazy: 10 krain Westeros (R regionow), region dezercji: Polnoc (N regionow); niewolni: bilans otwarcia TAK (27000) / BRAK (stary zapis - prog Zelaznych z BK 169c); ustawienia startowe przed MCM (stan gry - linie dzienne).`

### 10.2 Dzienne
- `Straz z Westeros (E4/9): dzien N | stan S (partie a, zalogi b; z pochodzeniem k%, bez pochodzenia u), pulap 166 P | przybylo n = z Muru m + z pul wyrzutkow p (zolnierze ps, prosci pc; budzet 3%: B w R regionach, wykorzystano x%) + z ludnosci l (reach l1, vlandia l2, ...) + bez pokrycia z | kanaly: notable i karczma c1, A5 c2, nowe partie c3, zalogi z modelu c4, milicja c5, karawany c6, uzgodnienie c7 | Mur: stan W (skazancy dzis s, bracia z powrotem dzis r: rozbici r1, przepelnienie r2, rozwiazani r3, bez bitwy r4, zwolnieni 166 r5) | ubylo: polegli d (Westeros d1, bez pochodzenia d2), zaraza e, dezercja i rozbici do lasu f (pula Polnocy: region), do niewoli g, w nicosc q | zwolnieni 166 do domu 0 (WatchNoReleases TAK), do domu z hearth 0 | H1: odrzucone opcje rabunku o, rabunki Strazy dzis 0 (od startu 0) | od startu: z Muru M, z pul P, z ludnosci L (rozklad: reach 29%, ...) | kontrola: S - Swczoraj - (n + wejscia inne - wyjscia) = 0.00 | potkniecia P | czas T ms.`
- `Niewolni (E4/9): dzien N | stan T (bilans otwarcia T0) w krainach: sturgia x (prog wolnych rak wysp f% z ksiegi), ghiscari y, volantine z, ... | dzis: ze sprzedazy jencow +s (osad a), z patroli BK +b, smierc -d (5%/rok) | wedlug pochodzenia (top 8): reach ..., freefolk ..., ...; bez pochodzenia u | poza kanonem (prawo wylaczone) e | kontrola: T - Twczoraj - (s + b - d) = 0.00 | potkniecia P.`
- "Ludzie (spustoszenie)" (K:Devastation.cs:824-): odcinek po kontroli trafionych: `| pojmani (E4/9): rabunkow wygranych z pojmanymi r, z uchodzcow wsi do lochu najezdzcy +J (wyliczone 10% trafionych przy niewoli c; nie wzieci - zostaja uchodzcami: gracz "zostaw" g, bez stanu rabunku h); zerowanie armii bez pojmanych`. Konto UCHODZCY: `wczoraj + nowi - powrot - podloga 10 - zmarli - pojmani = dzis (kontrola)`.
- "Jency (E4/0)" (K:Measure40.cs:1001): `| z rabunku: pojmani z wsi (E4/9) +J w r rabunkach, z niczego (BK) +X w y rabunkach (gracz z)`.
- "Dlugi (168)" (K:DebtLadder.cs:995-): `| DLUG 4 (E4/9): rodow w zajeciu z poczetem z, odeszli do domu h (limit zoldu h1, zalegly zold h2; bez wsi z danymi BK - do lasu h3), zwolnieni 166 rodow w zajeciu h4`.
- "Straz i Wolni Ludzie (4.15)" (K:PeopleLedger.cs:740-768): `| Straz z Westeros (E4/9): z Muru m, z pul p, z ludnosci l, bez pokrycia z; zwolnieni 166: 0; rabunki Strazy: 0`.
- "Krainy (4.15)" (K:PeopleLedger.cs:727): `niewolni (ksiega E4/9) X / niewolni BK Y`.
- "Wolne rece (E4/7)" (K:FreeHands.cs:293-294): `Zelazne Wyspy f% (niewolni ksiegi T / BK B - zrodlo: ksiega|BK)`.
- "Hearth (E4/0)" (K:Measure40.cs:867-907): do `named` i tekstu `pobor do Strazy z ludnosci Westeros (E4/9) -x, dluznicy do domu (E4/9) +y` (z `mv[MWatchLevy]`, `mv[MBackDebt]` po nazwie) - inaczej RESZTA hearth wchlonie [S] ok. -0.1..-0.3 hearth/d.
- "Wyrzutki:" (K:OutlawLaw.cs:1171-1191): `outs += _pWatch`; tekst `, do Strazy (E4/9) x` - **bez tego kontrola linii Wyrzutki przestanie byc 0**.
- "Ludzie (ksiega)" (odcinek E4/7): `| E4/9: NIEWOLNI T (z regionow n), Mur W, rownanie z T i W`.

---

## 11. Narzedzie `tools/sprawdz_logi.py --grupa etap4`

### 11.1 Blok `etap4_e49` (rusza przy linii startowej "Krainy kanonu (E4/9):"; stan z linii dziennych, nie startowej - poprawka R2-1 E4/3)
| # | Prog | Jak | Baza / skad liczba |
|---|---|---|---|
| K1 | 0 ERROR naszych modow; "BRAK: -"; potkniecia E4/9 <= 5 z nazwa miejsca | log | standard |
| K2 | Straz: przybylo = Mur + pule + ludnosc + bez pokrycia kazdej doby; bez pokrycia <= 1% przybylych (28 dob) | "Straz z Westeros" | z definicji (podloga 50 hearth) |
| K3 | pule: wykorzystano <= 100% budzetu kazdej doby; z pul > 0 w d2-d120 | jw. | RK 1.2 (X = 3%) |
| K4 | rozklad "z ludnosci" od startu: 3 najwieksze krainy w +-2 pkt od udzialu w ludziach Westeros (Reach ok. 29%, Westerlands ok. 15%, Dorzecze ok. 13%) | jw. | RK:215, tabela K:PopulationLaw.cs:44-53 |
| K5 | zwolnieni 166 do domu = 0 i "do domu z hearth" = 0 kazdej doby (bracia tylko do Muru) | jw. + "Budzet rodow (166)" | P :515, U5 |
| K6 | rabunki Strazy = 0 od d2 (d1 - rabunek w toku dopuszczony); H1 bezpiecznik 0 | jw. | RK 0.1.2: 10 rabunkow Strazy w 28 dobach (05.10) |
| K7 | Straz w d120 >= 85% biegu bazy (zbior bez E4/9 przy tych samych ustawieniach; zapas z31A d120: 8 691) - nie slabnie | "Straz i Wolni Ludzie (4.15)" | z31A |
| K8 | T6 Strazy: kontrola linii "Straz z Westeros" \|.\| <= 1 czlowiek kazdej doby i "w nicosc" <= 10% obrotu (28 dob) | jw. | P E4/0 T6 :304 (dzis 60-67% ruchu bez nazwy - STAN-PRAC WGRANIE 29) |
| K9 | dezercja i "do lasu" Strazy: 100% do regionow Polnocy | jw. | P R14 |
| K10 | pula wyrzutkow Westeros i bandy: INFO wobec bazy; prog bandy E4/3 (plateau w wojnie <= 1.5 x pokoj - Jeff 11.10 03:30) dalej musi przejsc | "Wyrzutki:", progi E4/3 | RK 1.2: 2% -> bandy -13%, 5% -> -28% [S: 3% ok. -17..-19%] |
| K11 | nowa kampania d1: niewolni Zelaznych = 27 000 x PopulationScale (+-1); kalibracja "sturgia 0.47M + niewolni" | "Niewolni (E4/9)", kalibracja | P :515 |
| K12 | kontrola "Niewolni" \|.\| <= 0.01 kazdej doby | "Niewolni" | z definicji |
| K13 | smierc niewolnych: d/T x 364 = 5% +-0.1 | jw. | P :512 |
| K14 | przyrost niewolnych tylko w krainach z listy kanonu; "poza kanonem" = 0 przy I1 wlaczonym | jw. | R15, K:LosersFlee.cs:74-77 |
| K15 | prog wolnych rak Zelaznych w d1 nowej kampanii 39-40% (zrodlo: ksiega) | "Wolne rece (E4/7)" | RK 2.3 pkt 6; z31A dzis 30.5% (BK 14 193) |
| K16 | pojmani w linii spustoszenia = pojmani z wsi w linii "Jency (E4/0)" kazdej doby (kontrola 0); "z niczego (BK)" = 0 kazdej doby przy czynnych `RaidCaptivesFromVillage` i spustoszeniu | dwie linie | P :516; z31A BK 18.5/d |
| K17 | pojmani tylko u najezdzcow z niewola ("nie z niewola" 0); J <= plan + 0.5 na rabunek | "Ludzie (spustoszenie)" | HR 2.3, R8 |
| K18 | E4/5 S3 bez zmian (pojmani przy uderzeniu 0); kontrola konta UCHODZCY z czlonem pojmanych \|.\| <= 0.01 | jw. | E4/5 |
| K19 | rownanie krain (E47 T3) z T i W: \|reszta\| <= 0.1% ludzi krainy (przy RESZTA hearth != 0 - INFO) | "Ludzie (ksiega)" | P E4/7 test |
| K20 | DLUG 4: przy rodach w zajeciu > 0 - "do lasu" (bez wsi) <= 5% odchodzacych, do domu > 0; bez zajec - INFO "brak zajec w biegu" | "Dlugi (168)" | b728: zajecia w 554 dobach, max 6 rodow |
| K21 | rownanie jencow E4/0 z czlonem pojmanych (parser 11.2) - jak dotad | "Jency (E4/0)" | E4/0 |
| K22 | koszt: linie i Levy E4/9 <= 3 ms/dobe; s/dobe <= +2% wobec biegu bez E4/9 | "czas" | E47 T12 |
| K23 | zapis z doby 362 (9 dob): 0 bledow; po wczytaniu linia "z zapisu - niewolni N, Mur M" = stan doby zapisu | log | 161 |
| K24 | progi etapow 1-3 (zadne krolestwo > 85% bitew lordow ani 3 twierdze; glowy < 5 000 <= 10; 0 bankructw; reszta ksiegi pieniadza < 10 tys. zl/d); wojsko lordow w wojnie 95-115 tys. | progi-etap2/3 | P rozdz. 4 |
| K25 | Wolni Ludzie: kalibracja "freefolk 0.30M"; "Wolne rece": prog 100%, zaden region `freefolk` z hamulcem; INFO stan Wolnych Ludzi d1 -> d120 | kalibracja, "Wolne rece" | P :507-508, RK 3.3 |

### 11.2 Poprawki parsera (inaczej falszywe NIE)
- Rownanie jencow E4/0 (K:../tools/sprawdz_logi.py:743-747): `jin += L(d, r"pojmani z wsi \(E4/9\) \+(\d+)", jen)` - nasze J nie sa juz w "z rabunku BK".
- E4/5 S3 (:1281-1282): opis "pojmani 0 (do E4/9)" -> "pojmani przy uderzeniu 0 (E4/9: pojmani z uchodzcow przy koncu rabunku - K16)" - warunek liczbowy bez zmian.
- T6 (:674-688): dla Strazy, gdy jest linia "Straz z Westeros (E4/9)" - ocena na jej kontroli (K8); Wolni Ludzie - jak dotad.
- E4/8 N2 (blok E4/8): etykieta "Straz zostawiona do E4/9" -> "Straz: przybysze z Westeros (E4/9)" (obie akceptowane - test zbiorczy moze miec E4/8 bez E4/9).
- Wiersze TAK/NIE/INFO (wzor bloku E4/5 K:../tools/sprawdz_logi.py:833-836).

---

## 12. Test (wspolny test koncowy etapu - Jeff 11.10 07:00; bez osobnego testu E4/9)
Autotest 40 dob + zapis z doby 362 (9 dob) - 0 bledow; bieg 120 dob nowej kampanii (K11, K15 wymagaja nowej kampanii) z progami K1-K25 oraz progami E4/5-E4/8 i etapow 1-3; bieg 364 dob konca etapu
(P rozdz. 4 :553). Reczna proba Jeffa: menu wsi Zelaznych Wysp i wsi w Reach - linie "Slaves..."; rabunek gracza w krolestwie z niewola (Take/Leave) - komunikat "villagers ... were taken captive".

---

## 13. Kontrola calosci (zasada 0) - do "Ryzyko / co sprawdzic" w CHANGELOG

### 13.1 Regresje (co juz dzialalo)
1. **Straz nie slabnie:** kanaly werbunku bez zmian (zmienia sie tylko zrodlo), pulap 166 bez zmian, zwolnien 0 (2-n), bracia z rozbic i rozwiazan wracaja na Mur i sa pierwsi w kolejce -
   Straz moze byc nieco silniejsza (K7). Jedyna utrata: dezerterzy do Polnocy zamiast do regionu bitwy (20 w 120 dobach z31A).
2. **Pule wyrzutkow Westeros mniejsze** ("przywdzial czern"): budzet 3% x ok. 1.7 tys. [S: 60% puli swiata 2 902 z31A d120] = ok. 50/d przy potrzebie Strazy 20-85/d -> bandy w Westeros [S] -15..-20%
   (RK 1.2). Kolizja z regula Jeffa 11.10 03:30 (bandy rosna z wojna, plateau <= 1.5 x pokoj) - prog E4/3 musi przejsc dalej (K10). Kontrola linii "Wyrzutki" - `_pWatch` w `outs` (rozdz. 10.2).
3. **Jencow mniej:** BK dawal 18.5/d z niczego KAZDEMU rodowi z siedziba-miastem (RK 0.1.3); po E4/9 tylko najezdzcy z kanonu i tylko 10% trafionych - mniej sprzedazy jencow poza krainami
   z niewola, mniej wcielen jencow (`RecruitPrisonersAi`); w krainach z niewola wiecej niewolnych na koncie.
4. **Zloto:** znika dzialka BK z niczego (BK :290-299) i jej platnik E3/1 (RealPayer :182-191 - miasto przestaje placic); mniej sprzedazy jencow = mniej wyplat kas miast za jencow (E3/1).
   Progi etapu 3 (reszta ksiegi pieniadza) - K24. Zadnego innego przeplywu zlota.
5. **Zelazne Wyspy w starym zapisie:** prog z BK jak dzis (5.3 zapas) - bez regresji; w nowej kampanii DOM wysp -5.4% (renty wysp tyle mniej - K:PopulationLaw.cs renty od ludzi), prog 40%.
6. **H1:** wsie Wolnych Ludzi przestaja plonac od Strazy - mniej spustoszenia i uchodzcow za Murem (z31A: Craster's Keep 8.3% uchodzcow, Hornfoot 3.6%), mniej zabitych obroncow. Partie Strazy
   czesciej patroluja/oblegaja (ROT pozwala oblegac tylko Fist i Craster's Keep po 1.5 roku - ROT:...AIThinkPatch.cs:53) - liczba bitew INFO.
7. **Niewolni a R5:** niewolny nie liczy sie do S regionu pochodzenia (jak E4/7 dzis) - uproszczenie; zolnierz sprzedany w niewole "zwalnia" wolne rece regionu pochodzenia. Male liczby
   (z31A: 37 niewolnych/d na swiecie). Nie zmieniamy definicji U19 przy okazji.
8. **Jency-cywile w S:** pojmani wiesniacy w lochu licza sie do S regionu jak zolnierze (E4/7 nie odroznia) - zawyza a regionu napadnietego o ok. 1/0.27 na pojmanego [S: 18/d swiat]; INFO w linii
   "Wolne rece" (regiony napadane przez Zelaznych).
9. **R5 Westeros:** bracia z Westeros w S regionow pochodzenia - [S] 0.3%/rok mezczyzn Westeros rozlozone; hamulec bez zmian (Straz bez progu, R5 P :151).
10. **DLUG 4:** u rodow w zajeciu wiecej ludzi wraca do DOM (+hearth), mniej do lasu - male liczby (max 6 rodow). Poczet dluznika topnieje tak samo szybko (ta sama dezercja gry), tylko w inna strone.

### 13.2 Kolizje - wspolne cele latek i stan (nasze i cudze)
| Cel / stan | Kto dzis | E4/9 | Co pilnowac |
|---|---|---|---|
| BK `ExecuteCapture` | Measure40 W11 (K:Measure40.cs:100), CirculationWindows O12 (:1788), RealPayer (:289) - void | prefiks bool **First** | 6.4; W11 dostaje odcinek "pojmani z wsi" |
| `CampaignEventDispatcher.AiHourlyTick` | ROT Patch1 postfiks (Reset + Add przy filtrze) | postfiks Low, `SetBehaviorScore` bez Reset | 4.6; linia startowa wypisuje wlascicieli |
| `RaidEventComponent.OnBeforeFinalize` / `Update` | Devastation End/Raid pre+fin (K:Devastation.cs:381-438) | stan rabunku czytany w prefiksie ExecuteCapture (w srodku OnBeforeFinalize) | `TakeCaptives` przed `EndFinalizer` - G:...RaidEventComponent.cs:138 |
| `RaidCompletedEvent` | BK (2 nasluchy), ROT/OthersRise, BKROT, BEE, Diplomacy, NavalDLC | nowy nasluch `WatchLaw.OnRaidCompleted` (tylko licznik) | bez zmiany stanu gry |
| `SellPrisonersAction.ApplyFor*` | E4/7 SalePre/SaleFin (First), BK prefiksy -> `PrisonerLaw.SendOffPrefix`, RealPayer/Ransom178/FairRansom na `ApplyInternal` | tylko cialo `SaleFin` (osada niewoli) | `SaleFin` dostaje `__1` (buyer) |
| `DefaultPartyDesertionModel.GetTroopsToDesertDueToWageAndPartySize` | OverflowHome pre/post (K:OverflowHome.cs:59-122) | warunek dluznika w `OverPre` | notatka na obiekcie rosteru - UI-owe wolania znikaja z rosterem (K:OverflowHome.cs:22-24) |
| `PopulationLaw.Calibrate` | nasz (E4/3 pulap gestosci, E4/4 SeedTowns) | odjecie bilansu otwarcia + `SeedThralls` | jedno wywolanie, jeden warunek (5.4) |
| `PeopleBook.HomeKnown` | E4/7 (H3, 166, W4c, 179, pula, jency, rozwiazane), E4/8 ArriveAt, E4/10 VetDivert przed | przekierowanie `FromWatch` -> MUR | VetDivert E4/10 Strazy nie bierze (E410 :32) - zgodne |
| `PeopleBook.ReconcileRoster` | E4/7 | KWatch nadwyzka -> Levy | koszt Levy w uzgodnieniu (ok. kilkadziesiat ludzi/d) |
| `OutlawLaw` pula (`_pool`) | E4/3 (powroty, siew), E4/4 (podloga), E4/7 (`wo:`), bandy | `TakeForWatch` | ten sam watek glowny; kontrola linii Wyrzutki z `_pWatch` |
| `ClanBudget.ToVillage` | 166, W4c, 179, E4/10 VetDivert (plan) | `MDebt -> MBackDebt` | mapa przyczyn w jednym miejscu (:819) |
| konto UCHODZCY (`Ref`) | E4/5 (Devastated, powrot, smierc, podloga), E4/6 (FamineFled - K46) | `RefugeesToCaptives` | ten sam `RefGate`; kontrola konta z nowym czlonem w obu liniach (Devastation i "Ludzie (ksiega)") |

### 13.3 Spojnosc logiki
1. **Nic drugi raz:** pojmani zdjeci z DOM raz (jako trafieni R8, potem z konta UCHODZCY do lochu) - BK nie dopisuje nikogo (prefiks false); skazaniec na Murze zjada miejsce w kolejce Levy przed
   nowym czlowiekiem z Westeros; brat wracajacy na Mur nie dostaje hearth w Westeros i nie jest drugi raz zdejmowany z Westeros przy ponownym werbunku (MUR pierwszy); DLUG 4 - jedna droga
   (W4c/166), nie drugie prawo zwalniania; smierc niewolnego tylko w ThrallsDaily (`TDead`), smierc brata tylko przez E4/7 `DieShares`.
2. **Martwy kod ozywa:** `HomeKnown` dla braci (dzis "" -> `Note`) zaczalby dzialac po nadaniu pochodzenia - dlatego przekierowanie (4.4) wchodzi w tym samym kroku co Levy (K2). `CaptivesLive`
   (dzis zawsze false) - czytane tylko przez `RaidCaptives`/linie i prefiks; `Strike` nie korzysta (6.3). Watch-galaz `ClanBudget.Releases` (:636-713) dalej martwa przy `WatchNoReleases`.
3. **Ta sama kolejnosc w dobie:** zdarzenia (werbunek -> Levy, sprzedaz -> NIEWOLNI, rabunek -> pojmani, dezercja -> DLUG 4) w ciagu doby; nasze `OnDailyTick`: ... `OutlawLaw.Daily` (:1516 - pule,
   `OverflowHome.NewDay` z migawka `LastDebt*`) -> `ClanBudget.Daily` (:1521) -> `DebtLadder.Report` (:1522 - odcinek DLUG 4) -> `OthersRise.Daily` (:1526: `PeopleBook.Daily` - uzgodnienie (Levy KWatch),
   widoki z T i W, `FreeHands.Update` z niewolnymi wysp) -> **`WatchLaw.Daily`** (linia + budzet nastepnej doby) -> **`ThrallLaw.Daily`** (smierc + linia) -> `Measure40.Daily` (:1560 - W11 czyta licznik
   pojmanych z Devastation) -> `PeopleLedger.Daily` (:1561 - czyta `WatchLaw.Last*`) -> `Devastation.DayLine` (:1562 - odcinek pojmanych, zerowanie). Hamulec i FreeHands czytaja cache doby (jak E4/7).
4. **Gracz = AI:** Levy dla partii gracza w Strazy (DeducePostfix), pojmani gracza ta sama regula (+ przelacznik BK), DLUG 4 gracza w zajeciu, niewola gracza u posrednika (I1b) - jedna regula; H1 tylko AI.
5. **Stan czyszczony miedzy kampaniami:** `ResetCanon` (konstruktor), `WatchLaw.Reset`, `ThrallLaw.Reset`, `OverflowHome.Reset` (liczniki dluznikow).
6. **Kultura osady (U3):** zrodla Strazy, Polnoc i kraina wysp - z kultury z dnia kalibracji; prawo niewoli (czy osada trzyma niewolnych) - biezaca kultura osady (I1b, jak dotad).

### 13.4 Zamknieta ksiega ludzi (kazdy czlowiek ma zrodlo i ujscie)
| Przeplyw | Dzis (z31A / zbior 31 + E4/7 + E4/8) | Po E4/9 |
|---|---|---|
| rekrut Strazy (notable, A5, szablon, zaloga z modelu, milicja, karawana) | "" z niczego (E4/7 FNoneWatch, E4/8 Y1-Y4) | MUR / pula Westeros (WYRZUTKI -> SLUZBA) / DOM Westeros (-1/k, `MWatchLevy`); reszta "" nazwana (podloga) |
| skazaniec na Mur | `ToWall` licznik - odplyw regionu pochodzenia | konto MUR (stan regionu pochodzenia) -> SLUZBA Strazy |
| brat rozbity / rozwiazany / przepelnienie | "" -> `Note` (bez hearth) | MUR (bez hearth) -> znow SLUZBA |
| dezerter Strazy | pula regionu miejsca | pula Polnocy (z udzialem pochodzenia) |
| polegly brat z Westeros | - | POLEGLI regionu pochodzenia (E4/7 `DieShares`) |
| jeniec sprzedany w kraju z niewola | licznik `Enslaved` (odplyw) | konto NIEWOLNI (stan regionu pochodzenia, trzymany w osadzie niewoli) |
| smierc niewolnego | - | `TDead` regionu pochodzenia (5%/rok) |
| bilans otwarcia niewolnych | - | 27 tys. = czesc tabeli wysp (start, nie z niczego) |
| jency z rabunku | BK 18.5/d z niczego | UCHODZCY wsi -> JENCY najezdzcy (z pochodzeniem) |
| dluznik: odchodzacy z braku pieniedzy | pula (las) | DOM regionu pochodzenia (`MBackDebt`) |
Zostaje "z niczego" (nazwane): Straz bez pokrycia (podloga 50 hearth w calym Westeros - oczekiwane 0), dawni bracia "" z siewu (tylko maleja), konwoje niewolnikow BK (klasa BK), konsola BK.

### 13.5 Zamknieta ksiega zlota
E4/9 usuwa jedno zrodlo z niczego (dzialka BK z ExecuteCapture) i jego platnika E3/1 - nic nie dodaje. Levy, NIEWOLNI, MUR, pojmani, DLUG 4 - bez zlota. Zaplata za jencow sprzedanych (RealPayer,
FairRansom) bez zmian w dzialaniu - zmienia sie tylko liczba jencow.

### 13.6 Watki i wyjatki
Wszystkie cele na watku glownym kampanii (myslenie AI godzinowe, werbunek, sprzedaz, koniec rabunku, dezercja dobowa). Kazde wywolanie ksiegi we wlasnym try, licznik potkniec per miejsce
(bez globalnych wylacznikow - CLAUDE.md 7); nic nie stoi miedzy zdjeciem a dopisaniem w kodzie gry (U24): Levy po dopisaniu rekruta przez gre (zmienia tylko slownik i DOM/pule), pojmani - konto
-> loch w jednym miejscu z cofnieciem przy braku postaci, DLUG 4 - notatka przed zdarzeniem, wyjscie przez istniejace `ToVillage`. Lista do recenzji: `Levy`, `WatchFromPools`, `WatchFromPeople`,
`WallAdd/WallTake`, `HomeKnown` (galaz), `EnslaveAt`, `ThrallsDaily`, `SeedThralls` + `Calibrate`, `CapturePrefix`, `RefugeesToCaptives`, `OverPre/NoteAll/SendHome`, `AiTickPostfix`.

---

## 14. NK (co dziala dopiero w nowej kampanii)
- **Tylko nowa kampania:** bilans otwarcia 27 tys. niewolnych Zelaznych Wysp (i k wysp z 473 tys.) - P :520; prog wysp 40% z ksiegi (w starym zapisie prog z BK jak dzis).
- **Stary zapis od pierwszej doby po wgraniu:** Levy Strazy (od siewu E4/7), MUR, dozywotnio, dezerterzy do Polnocy, H1, konto NIEWOLNI (od zera), pojmani z rabunku, DLUG 4.
- Wolni Ludzie 300 tys. - NK od E4/3 (bez zmian w E4/9).

---

## 15. Pytania i informacje

**Pytania do Jeffa (tylko kierunek):**
- **P1. Zalogi Nocnej Strazy:** dzis zalogi Strazy rosna co dzien same (przyrost z gry), a od E4/8 wszystkie inne zalogi juz nie. Po E4/9 kazdy taki brat bylby prawdziwym czlowiekiem z Westeros
  (najpierw skazancy, potem wyrzutki, potem ludzie z krain wedlug ich liczby). Zostawic ten doplyw Strazy (Mur nie slabnie), czy jak wszedzie - zaloga Strazy rosnie tylko z werbunku
  u notabli i z partii? Rada: zostawic (bez odpowiedzi - zostaje; wylaczenie to jedna galaz Y1).
- **P2. Rozbici bracia wracaja na Mur, nie do domu** ("sluzba dozywotnia" - brat, ktory po bitwie idzie do swojej wsi, bylby dezerterem); ci, ktorzy uciekli w las - do wyrzutkow Polnocy. Zgoda?
  Rada: tak (bez odpowiedzi - tak).

**Informacja (bez pytania - decyzje projektu albo Twoje wczesniejsze):**
- 3% wyrzutkow krainy dziennie moze "przywdziac czern" - Straz bierze najpierw z nich, wiec band w Westeros bedzie mniej [S: ok. 15-20%]; reguly "bandy rosna z wojna" to nie zmienia.
- Jency z rabunku biora tylko krolestwa z niewola wedlug Martina (Twoje 07.10), 10% napadnietych, i tylko po udanym rabunku; Twoj przelacznik "Take/Leave" zostaje.
- Dluznik, ktoremu wierzyciel zajal wsie, odsyla ludzi do domu, a nie do lasu.
- Niewolni 27 tys. Zelaznych Wysp i prog 40% - tylko w nowej kampanii; w starym zapisie prog liczy sie jak dzis.

**Techniczne - rozstrzygnac przy kodzie (nie dla Jeffa):**
- T1. Ksztalt kodu E4/8 (nazwy finalizerow Y1-Y4) - dopasowac po jego recenzji; gdy E4/8 nie bedzie gotowe - E4/9 bez Y1-Y5.
- T2. Wielkosc "Straz zostawiona do E4/9" z pomiaru E4/8 (jesli > 30/d - P1 ma wiekszy ciezar; podac liczbe Jeffowi).
- T3. Czy `villager_<kultura>` istnieje dla wszystkich kultur wsi ROT (BK ma te sama droge zapasowa - `looter`); policzyc w linii startowej, ile kultur wsi nie ma postaci.
- T4. Numeracja `MKinds` po scaleniu E4/6 + E4/7 (Z17) - E4/9 tylko po nazwie.
- T5. `Num(a, i)` w `rg:` zwraca 0 poza dlugoscia (sprawdzic przy kodzie - E4/8 i E4/9 dopisuja pola).

---

## 16. Kolejnosc kodowania (galaz `w-toku/e4-9` od czubka E4/8 po jego recenzji; kazdy krok: build Release kod 0, osobny commit; jeden test na koncu etapu)
0. **K0** sprawdzenie Z1-Z18 i Y1-Y7 na kodzie E4/7/E4/8 (lista w opisie commita K1); grep kluczy w `Armoury.json`.
1. **K1 ksiega bez skutku w grze:** `PeopleBook.Canon.cs` (konta NIEWOLNI i MUR, `ns: nw: mw:`, `rg:` +3 pola, `RegionNum/View`, rownanie, siew TRef/WRef, Reset), szkielety `WatchLaw`/`ThrallLaw`
   (Reset, linie, StartLine), wpiecia w ArmouryBehavior i SubModuleMain, klucze (wszystkie `true`, ale latki jeszcze puste).
2. **K2 Straz:** `Levy` (MUR, pule z `OutlawLaw.TakeForWatch` + `_pWatch` w kontroli Wyrzutki, ludnosc z kolejka kwot), wolajacy E4/7 (Deduce, A5, Reconcile) i E4/8 (Y1-Y5), znacznik `FromWatch`
   + przekierowanie `HomeKnown` (4.4) **w tym samym commicie** (13.3 pkt 2), `WallShares` -> MUR, region dezercji (4.5), liczniki KWatch, linia "Straz z Westeros", odcinek PeopleLedger.
3. **K3 H1:** `AiTickPostfix`, nasluch `RaidCompletedEvent`, linia startowa z wlascicielami.
4. **K4 niewola:** `EnslaveShares(Shares, Settlement)` + `SaleFin __1` + `PatrolInPre`, `ThrallsDaily`, bilans otwarcia (`OpeningFor` + `Calibrate` + `SeedThralls`), FreeHands (5.3), menu, linia "Niewolni".
5. **K5 jency z rabunku:** `CapturePrefix`, `Devastation.TakeCaptives/CaptivesLive/CapTaken`, `RefugeesToCaptives` + kontrola konta, odcinki linii spustoszenia i W11.
6. **K6 DLUG 4:** OverflowHome (OverPre/NoteAll/SendHome), WarLedger, `ToVillage` (MDebt), odcinek "Dlugi (168)", Hearth (MWatchLevy, MBackDebt).
7. **K7:** `Settings.cs` (komentarze po polsku bez znakow, podpowiedzi EN) + `python tools/gen_mcm.py` (RANGES), parser (11.1-11.2), CHANGELOG (Problem z liczbami z31A/b728; Przyczyna; Zmiana;
   **Odstepstwa:** `WatchLifelong` = `WatchNoReleases`, pojmani przy koncu rabunku zamiast przy uderzeniu (E4/5 szkic), DLUG 4 przez 166/W4c bez parametrow D5, bez bilansu pochodzenia dawnych braci,
   prog wysp z BK w starym zapisie, zerowanie bez pojmanych; Ryzyko = rozdz. 13; Status DO SPRAWDZENIA), dwie recenzje diffu (ksiega ludzi; kod i kolizje), potem wspolny test konca etapu.
