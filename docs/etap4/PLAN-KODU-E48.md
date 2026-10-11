# PLAN KODU - paczka E4/8 (164b-2): zalogi, milicje, nowe druzyny lordow, karawany, konwoje i wedrowcy BK z ludzi; migracja do miast (R2)

Status: SAM PLAN (11.10.2026). Kod, gra, ustawienia, zapisy nietkniete; nic nie budowane.
Wiazace: `docs\etap4\PROJEKT-ETAP4-KSIEGA-LUDZI-ZIMA-2026-10-10.md` v2 (dalej P; E4/8 = P:489-501, reguly R0 :116-143, R2 :148, R4 :150, R5 :151,
R6 :152, R19 :165, progi rozdz. 4 :536-554, ryzyka 5.1-5.4 :558-616), `docs\etap4\KRYTYKA-E4.md` (KR: U15f :209/:213, U19 :237-242, U20 :244-249),
`docs\etap4\PLAN-KODU-E47.md` (dalej E47 - interfejs ksiegi, na ktorym ta paczka stoi), decyzje Jeffa (`docs\STAN-PRAC.md`):
11.10 (SP:1137-1138) "kraina bez wolnych rak nie wystawia nowego wojska do odrobienia strat - TAK", "ludnosc w ciaglej wojnie moze spadac (do -0.5%/rok) - TAK";
09.10 C (SP:771) "mniej wojska AI - NIE"; PLAN-DO-KONCA :132 (4.7 "zalogi i milicje tylko z ludzi regionu"), :134 (4.9 "odrodzony lord bez 55 ludzi i zboza z niczego").

Skroty miejsc:
- **K:** = `scratchpad\e4\47\Armoury\src\` - drzewo galezi `w-toku/e4-7` w chwili pisania planu = czysty `dfb12df` (zbior 31: gra + E4/0..E4/5 + E4/6a + 36b;
  `git -C ...\47 status` puste, kodu E4/7 jeszcze nie ma) - wszystkie linie naszego kodu z tego drzewa.
- **G:** = `scratchpad\dec32\cs_full\` (TaleWorlds.CampaignSystem 1.4.8), **BK:** = `scratchpad\e4tools\bkfull\` (BannerKings.Redux),
  **D:** = `scratchpad\e4tools\dec\` (ROT/NavalDLC/BK modele, ilspycmd 9.1).
- **z30A** = bieg zbioru 30 (`scratchpad\kopia-z30-A\Armoury-2026-10-11_04-36-46.log`, 120 dob) - srednie z doby 2-120 policzone skryptem
  `scratchpad\e48tools\base.py` (linie "Partie (E4/0)", "Zalogi (169c)", "Wedrowcy BK (36b)").

---

## 0. Streszczenie decyzji planu (co wykonawca robi, czego nie)

1. **Jedna regula na wejscie do sluzby (R4): kazdy czlowiek, ktorego dzis daje szablon albo model gry, schodzi z DOM swojego regionu przez E4/7
   `PeopleBook.TakeLevy` (wies -1/k_v, miasto -1 stanu, zamek - jego wsie)**, a jego udzial pochodzenia idzie do rosteru przez E4/7 `GiveNew`.
   Czego DOM nie da (podloga 50 hearth, hamulec wolnych rak) - tego czlowieka nie ma: latka zdejmuje go z rosteru, ktory gra przed chwila napelnila.
   Zadnego "z niczego" poza nazwanymi wyjatkami (pkt 8).
2. **Zalogi:** przyrost z modelu gry = 0 (prefiks `bool` na `GarrisonRecruitmentCampaignBehavior.TickGarrisonChangeForTown`). Zaloga rosnie tylko z A5
   (platny werbunek od notabli - zdejmuje E4/7 3.4), z partii (E4/7 4.1) i z patroli BK (E4/7 4.4). A5 bez zmian; E4/8 tylko MIERZY, co go ogranicza
   (limit dzienny / brak ochotnikow / zold) - to dowod do warunkowego K8 (pkt 9).
3. **Milicja:** kazdy przyrost milicji (tick wsi i warowni, odbudowa wsi, obrona oblezenia, rozwiazana partia we wsi) = ochotnicy z wolnych rak regionu:
   przy hamulcu (E4/7 `FreeHands.Braked`) przyrostu nie ma; ubytek bez walki (model ujemny - BK "Over supported limit") wraca do DOM tej samej osady.
4. **Nowe druzyny lordow (4.9):** szablon klanu oplacony kolejno: (a) wolne rece regionu siedziby (`TakeLevy`), (b) zaloga siedziby (prawdziwe
   przeniesienie ludzi, zaloga zostawia co najmniej 25 w zamku / 50 w miescie - prog gry), (c) reszty nie ma (lord wyrusza z mniejsza druzyna).
   **Zboze 15-30 z niczego (G LordPartyComponent.cs:41) - z zapasu zywnosci siedziby** (`Town.FoodStocks`, 1 jednostka = 1 sztuka zboza).
5. **Karawany:** straz i woznice z ludzi osady startu karawany (miasto - stan "t:"), bez hamulca wolnych rak (jak najemnik z karczmy - pytanie P1),
   uzupelnienia konwojow morskich tak samo; karawana rozwiazana bez bitwy wraca do DOM osady domowej.
6. **Wedrowcy i konwoje BK (R19, U15f):** wedrowcy (cala partia z eskorta) wyjezdzaja z DOM osady pochodzenia i wchodza do DOM osady, do ktorej wejda
   (z30A: 93.7% wraca do domu po ok. 0.15 doby - netto prawie zero); eskorta konwojow zywnosci i niewolnikow - z DOM pochodzenia i z powrotem.
   Miedzy regionami = migracja (nowe pola `MigIn/MigOut` w rownaniu E47 rozdz. 5).
7. **Miasta (R2):** przyrost naturalny miasta = stopa wsi jego regionu (R1, te same W i U, bez premii gry) minus `TownGrowthPenaltyPercent` 1.0 pkt
   (DOK:524 "miasto traci ok. 0.5% rocznie"); migracja z wlasnych wsi (Normal, podloga 50 hearth), gdy region nie gloduje: pokrywa ubytek naturalny
   i luke do udzialu miejskiego regionu w ok. rok (`TownMigrationRefillDays` 364; 0 = tylko ubytek naturalny - wersja DOK; pytanie P2).
8. **Wyjatki nazwane (bez zdejmowania z DOM, koszyk "" E47 0 pkt 3):** Nocna Straz (zaloga, milicja, nowa druzyna, wedrowcy z Daru - "Straz - do E4/9";
   DOK krytyk krok 5 pkt 4: inaczej Dar -4.4..-5.9%/rok), osady Innych (milicja z modelu = wighty z niczego - przyrost odmowiony), przed siewem E4/7
   albo przy `!LevyLive` (wszystko jak dzis - Reconcile E47 4.14 nazywa). Klasy BK nie sa ruszane (R0: BK = sklad; przy milicji, szablonie, karawanie i wedrowcach
   nie ma wyboru skladu z BK).
9. **K8 warunkowy (P E4/8 ryzyko :499-500):** jesli zalogi w d120 < 85% bazy - postfiks na `GetMaximumDailyAutoRecruitmentCount` czynnego modelu (A5 do 2/dobe,
   brama = limit zoldu zalogi 166). Kodowany dopiero po tescie, jesli prog nie przejdzie (CLAUDE.md 8 pkt 3) - dlatego pomiar limitow A5 jest w K1.
10. **Poza E4/8 (nie dopisywac "przy okazji"):** konie i zywnosc z `GiveInitialItemsToParty` (G HeroSpawnCampaignBehavior.cs:280-309) - tylko licznik (etap 6, towary/konie
    - kon w taborze to droga awansu konnicy); bunt (G RebellionsCampaignBehavior.cs:205-245), rabunek gracza bez oporu (G EncounterGameMenuBehavior.cs:2188),
    incydenty (G IncidentEffect.cs:657), milicja-eskorta BK gracza (BK MilitiaComponent.cs:47) - zostaja nazwane w Reconcile E47 (P2); Straz z Westeros,
    niewolni jako konto (E4/9); weterani (E4/10).

---

## 1. Zaleznosci od E4/7 (sprawdzic przy kodzie - E4/7 jest w toku; plan opiera sie na interfejsie z E47)

| # | Czego E4/8 uzywa | Gdzie w E47 | Co zaklada | Gdy inaczej / brak |
|---|---|---|---|---|
| Z1 | `internal static partial class PeopleBook` + plik `PeopleBook.Origin.cs` | E47 0 pkt 1 | E4/8 dopisuje trzeci plik czesciowy `PeopleBook.Arms.cs` | jesli nie `partial` - E4/8 zmienia deklaracje (K:PeopleBook.cs:36) jak E4/7 |
| Z2 | `Shares` (M, Total, Known, `Slice(n)`), `Take(TroopRoster, n, cause)`, `Give`, `GiveNew(TroopRoster, Settlement region, n, cause)`, `GiveNone(roster, n, cause)` | E47 2.1, 2.4 | Take przed siewem / bez slownika = Shares calosc ""; nigdy nie rzuca | nazwy dopasowac; semantyka musi byc ta sama |
| Z3 | `double TakeLevy(Settlement s, double n, int cause, out Settlement region)` - wies z podloga 50 hearth, miasto przez D1, zamek przez jego wsie, brak - inne wsie regionu | E47 2.4 | przyjmuje DOWOLNA przyczyne M* i liczy wedlug niej (linia "Pochodzenie"); sama NIE sprawdza hamulca | gdy ma wbudowany hamulec - E4/8 dla karawan/wedrowcow potrzebuje wariantu bez niego (wydzielic cialo `TakeHome`, bez zmiany rachunku) |
| Z4 | stan miasta: D1 `MoveTown(Settlement, from, to, men, cause)` (E4/7 dopisuje, jesli E4/4 nie ma - E47 1 D1) | E47 1 D1 | jedna funkcja zmiany `_town` (K:PeopleBook.cs:97) z licznikiem wedlug przyczyny, w obie strony, nie ponizej 0 | brak - E4/8 dopisuje `TownShift(Settlement, double men, int cause)` w `PeopleBook.Arms.cs` i E4/7 ma ja wolac (jedna droga) |
| Z5 | `HomeKnown(Shares, n, cause)`, `HomeOrNote(Shares, n, causeKnown, causeLegacy)` (powrot przez `OutlawLaw.ReturnHome` K:OutlawLaw.cs:344-358) | E47 2.4, 4.11 | czesc "" - `Note`, bez hearth | - |
| Z6 | `FreeHands.Braked(Settlement region)` (a >= f przy `FreeHandsBrake`, okno zniw), regula Strazy (Dar bez progu, rekrut do "") | E47 6.3, 6.5 | cache doby, czyta bez zamka; Straz rozpoznawana tym samym predykatem co w E4/7 (`CrownGifts.IsWatchParty` K:CrownGifts.cs:72 / kultura regionu nightswatch) | E4/8 wola ten sam predykat - nie pisze wlasnego |
| Z7 | `LevyLive` (= siew && PeopleOriginEnabled && LevyFromVillages && PeopleUnit.On) i `EnsureSown` | E47 2.3, 2.4 | ten sam warunek dla zdjecia i powrotu (symetria) | - |
| Z8 | wykluczenie partii ludnosci BK (`PopulationPartyComponent`) ze slownikow skladu | E47 2.1 ("konwoje BK ... E4/8") | jeden predykat, np. `IsOriginParty(mp)` | E4/8 zmienia go: konwoje/wedrowcy WCHODZA do slownikow (przy `TravellersFromPeople`) |
| Z9 | `UnderArms(region)` = S z R5 | E47 6.2 | petla po slownikach skladow | E4/8: S pomija `PopulationPartyComponent` (w drodze, nie w sluzbie - jak TABOR) |
| Z10 | `RegRef` i sekcja zapisu `rg:` (10 pol) + rownanie regionu | E47 2.2, 5 | parser `rg:` czyta po kolei i znosi dluzsza liste | E4/8 dopisuje 2 pola na koncu (`MigIn;MigOut`); jesli parser odrzuca dluzsze - poprawic w E4/8 |
| Z11 | `NoteBorn(Settlement, double men)` (D2) | E47 1 D2, 5 | przyjmuje miasto i liczbe ujemna (netto naturalne) | brak - E4/8 dopisuje do `RegRef.Born` sama |
| Z12 | `ReconcileDaily` z kategoriami "z niczego": partie rodow z1 [szablon z1t], zalogi z2 [model z2m], milicje z3, karawany z4 | E47 4.14, 8.3 | E4/8 nie zmienia Reconcile - jej latki robia z tych kategorii ~0 (prog N3) | - |
| Z13 | okno rozwiazania partii (`DisbandPartyCampaignBehavior.OnPartyDisbanded`, prefiks: sklad, loch, zaloga przed) | E47 4.3 | prefiks zapisuje tez milicje osady (E4/8 dopisuje to pole) | E4/8 dopisuje w tym samym oknie (rozdz. 5.6) |
| Z14 | `PeopleBook.OnPartyDestroyed` (K:PeopleBook.cs:756-773 R18 + E47 4.12 "resztka w nicosc") | E47 4.12 | jeden nasluch (K:ArmouryBehavior.cs:677), biegnie przed `RemoveParty` (sklad jest) | E4/8 dopisuje galaz (rozdz. 10.4) PRZED "resztka w nicosc" |
| Z15 | numery przyczyn M* | E47 2.5 | **E47 liczyl od z30 (MKinds 15) - w zbiorze 31 E4/4/E4/5 zajely 15-19 (K:PeopleBook.cs:71-73, MKinds 20)**; E4/7 musi zaczac od 20 | E4/8 dopisuje swoje po ostatniej przyczynie E4/7 - po NAZWIE, nigdy po liczbie; `MName`, `MHearth` (K:PeopleBook.cs:74-85) tej samej dlugosci |
| Z16 | linia "Hearth (E4/0)" (K:Measure40.cs:862-903) | E47 8.3 | E4/5 zajal juz `o8`/`o9` (K:Measure40.cs:867) - E4/7 doda swoje dalej | E4/8 dopisuje swoje skladniki po nazwie (rozdz. 12.4) |
| Z17 | linia "Pochodzenie (E4/7)" i liczniki wejsc wedlug przyczyny | E47 8.3 | tablica liczona po przyczynie z `GiveNew` | nowe przyczyny E4/8 maja swoje nazwy w tej samej tablicy |
| Z18 | `PeopleMenu` (pozycja "People of this land") | E47 7 | - | E4/8 dopisuje jedna linie dla miasta (rozdz. 9.6); bez E4/7 menu - pomija |

Punkty styku z innymi paczkami: **E4/4** (w zbiorze 31) - stan miast `TryTown`/`SetTownOnce` (K:PeopleBook.cs:333-356), `TownsDaily` (K:PopulationLaw.cs:283-295),
`ProsperityIndex` (K:PopulationLaw.cs:465), `Danger(Village)` (K:PopulationLaw.cs:503-521), H w `GrowthOf` (K:PopulationLaw.cs:538); **E4/5** - konto UCHODZCY
(`RefugeesOf` K:PeopleBook.cs:413 - uchodzcy nie sa w DOM wsi przy migracji); **E4/6** (rownolegle, galaz `w-toku/e4-6` w `scratchpad\e4\46`) - glod regionu
(R2 "gdy region nie gloduje") i zgony z glodu mieszczan (ta sama `TownShift`). Bez E4/6: glod = `Settlement.IsStarving` (G Settlement.cs:410-419: `FoodStocks <= 0`).

---

## 2. Pliki i klasy

| Plik | Zmiana |
|---|---|
| NOWY `Armoury/src/ArmsFromPeople.cs` (ok. 700 linii) | `internal static class ArmsFromPeople`: `ApplyAll(Harmony)` (latki rozdz. 4-8, wpinane zawsze, licza przy kluczach - U26), `OnPartyCreated(MobileParty)` (ramka konwojow BK), `Daily(int day, bool log)` (linia "Wojsko z ludzi (E4/8)"), `StartLine()`, `Reset()`, liczniki doby, `Stumble` (licznik potkniec per miejsce, bez globalnych wylacznikow - CLAUDE.md 7) |
| NOWY `Armoury/src/PeopleBook.Arms.cs` (ok. 350 linii) | `partial class PeopleBook`: konto `ARoad` (w drodze), przyczyny E4/8 (rozdz. 10.1), `ArriveAt(...)` (rozdz. 10.3), `TakeFromRegionVillages(...)` (migracja R2), `TownShift` (gdy brak D1 - Z4), odniesienia miast `tr:` (rozdz. 3), `MigIn/MigOut` do `RegRef`, liczniki ruchow stanu miast wedlug przyczyny |
| NOWY `Armoury/src/TownGrowth.cs` (ok. 300 linii) | R2: `Daily()` (wolane z `PopulationLaw.Daily` zaraz po `TownsDaily`, K:PopulationLaw.cs:1148), `Line(int day)` (linia "Miasta (E4/8 R2)"), `Reset()` |
| `PeopleBook.cs` | `MKinds`, `MName`, `MHearth` (K:PeopleBook.cs:71-85) - przyczyny E4/8 po E4/7; `Export`/`Import` (K:PeopleBook.cs:141-184, :232-322) - wywolanie sekcji `tr:` z partiala (wlasny try, jak `ExportRefugees` :182); `Reset` (:118-127) - czysci stan E4/8; `UnitSegment` (:932-983) - punkty powrotu E4/8 w "Ludzie (ksiega)" |
| `PeopleBook.Origin.cs` (E4/7) | Z8 (konwoje do slownikow), Z9 (S bez konwojow), Z10 (`RegRef` +2 pola, rownanie + MigOut - MigIn), Z13 (milicja przy rozwiazaniu), Z14 (galaz konca partii) |
| `PopulationLaw.cs` | `DangerOf(Settlement node, ...)` wydzielone z `Danger(Village)` (K:PopulationLaw.cs:503-521) BEZ zmiany rachunku (wzor E4/5 K0, commit 98787a2); `HungerTerm(Village)` wydzielone z :538 (E4/6 zmieni H w jednym miejscu); NOWE `internal static float TownRate(Settlement town, out TownRateParts p)`; `Daily` (:1148) - po `TownsDaily()` jedna linia `try { TownGrowth.Daily(); } catch ...`; `GrowthDaily` tekst "miasta:" (:901-904) - "bez narodzin i migracji do E4/8" zamienione na odsylacz do linii "Miasta (E4/8 R2)" gdy R2 czynne (parser czyta ten odcinek tylko jako INFO - tools/sprawdz_logi.py:1099) |
| `Measure40.cs` | linia "Hearth (E4/0)" (K:Measure40.cs:862-903): skladniki E4/8 do `named` i do tekstu (rozdz. 12.4) |
| `ArmouryBehavior.cs` | konstruktor (K:ArmouryBehavior.cs:395): `ArmsFromPeople.Reset(); TownGrowth.Reset();`; nasluch `CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, ArmsFromPeople.OnPartyCreated)` obok K:ArmouryBehavior.cs:702; `OnDailyTick`: `try { ArmsFromPeople.Daily(...); } catch` zaraz po `OthersRise.Daily()` (:1519 - tam E4/7 wola `PeopleBook.Daily` z Reconcile), przed `Measure40.Daily()` (:1553) |
| `SubModuleMain.cs` | `try { ArmsFromPeople.ApplyAll(_harmony); } catch ...` po `Devastation.ApplyAll` (K:SubModuleMain.cs:148) i po `PeopleBook.ApplyOrigin` E4/7 (E47 8.2: po :144) - kolejnosc wobec cudzych latek ustala priorytet, nie miejsce wpiecia |
| `LosersFlee.cs` | tylko tekst odcinka "dziura werbunku" (K:LosersFlee.cs:1257): przy czynnym `NewPartyFromPeople` dopisek "(od E4/8 oplaceni z ludzi albo z zalogi - linia Wojsko z ludzi)". Licznik bez zmian (K:LosersFlee.cs:1213-1223) |
| `Settings.cs` + `McmSettings.cs` | 10 kluczy (rozdz. 11) po kluczach E4/7 (po K:Settings.cs:721 + E4/7); opis `TownPeopleFrozen` (K:Settings.cs:706) - wzmianka, ze R2 go wymaga; `python tools/gen_mcm.py` |
| `tools/gen_mcm.py` | `RANGES`: `TownGrowthPenaltyPercent` (0.0, 3.0, "0.0"), `TownMigrationRefillDays` (0, 1456, "0") |
| `tools/sprawdz_logi.py` | blok `etap4_e48` (rozdz. 13), wolany w `etap4_main` (wzor K:...tools/sprawdz_logi.py:825-836), gdy jest linia startowa "Wojsko z ludzi (E4/8):" |
| `CHANGELOG.md`, `docs/STAN-PRAC.md` | wpis (rozdz. 18 K7) |

---

## 3. Stan w zapisie (`arm_people`, `SaveText.Sync` - K:ArmouryBehavior.cs:440-441)

- **`|tr:<idMiasta>=<RefLudzi>;<RefLudziWsi>,...`** - odniesienie R2 na miasto: stan miasta i ludzie jego wsi (hearth x k_v, bez uchodzcow) z pierwszej doby z czynnym R2.
  Zapisywane tylko, gdy niepuste (bez sekcji = stary zapis: odniesienie zalozy sie w pierwszej dobie - bez skoku). Id ze znakami `| , ; =` pomijane (`BadId`,
  K:PeopleBook.cs:186). Liczby "R" Invariant. Import: smieci (NaN, ujemne, nieskonczone) - wpis pominiety (zalozy sie od nowa). Wylaczenie R2 NIE kasuje sekcji
  (odniesienie wraca po ponownym wlaczeniu).
- **`rg:`** (E4/7) - +2 pola na koncu kazdego regionu: `MigIn;MigOut` (ludzie od siewu E4/7). Stary zapis E4/7 (10 pol) - zero.
- Slowniki skladow konwojow i wedrowcow BK - w sekcji E4/7 `so:` (bez nowej sekcji; rodzaj partii z `PartyComponent` w chwili odczytu).
- Bez zapisu: liczniki doby, ramka tworzenia konwojow BK, cache predykatow.
- Rozmiar: `tr:` ok. 97 miast x 40 znakow = 4 KB.
- `Reset` (konstruktor ArmouryBehavior :395 -> `PeopleBook.Reset`, `ArmsFromPeople.Reset`, `TownGrowth.Reset`): czysci `tr:`, `MigIn/MigOut` (E4/7 `RegRef`), liczniki - stan
  jednej kampanii nie przecieka do drugiej (zasada 0).

---

## 4. Zalogi (K1) - przyrost z modelu = 0, pomiar A5

### 4.1 Okno `GarrisonRecruitmentCampaignBehavior.TickGarrisonChangeForTown(Town)` - prefiks `bool`
- Cel: G:...CampaignBehaviors\GarrisonRecruitmentCampaignBehavior.cs:97-108 (private; `num = (int)GetBaseGarrisonChangeExplainedNumber(town).ResultNumber`,
  `AddToCounts(BasicTroop, num)` :106 - ludzie z niczego, bez zlota). Wolany z `OnDailySettlementTick` :56-74 (gdy brak bitwy i oblezenia).
  Model czynny: `ROT.Models.ROTSettlementGarrisonModel` (z30A log:530) - owija poprzedni (D:ROT.Models.ROTSettlementGarrisonModel.cs:24-31: Inni 0); Default daje +2 buntownikom
  i efekty zadan (G:...DefaultSettlementGarrisonModel.cs:45-54); BK +1 "Enlistment" postfiksem na Default (BK:...VanillaModelTweakPatches.cs:1517-1536), polityke wlacza BK w
  kazdej warowni w wojnie z gubernatorem (BK:...BKSettlementBehavior.cs:418-428). Z30A: +105 ludzi/d (169c "przyrost z modelu"), d1 0 -> d120 +113.
- Latka: `public static bool GarrisonGrowthPrefix(Town __0, out int __state)` **Priority.Low**, + `public static Exception GarrisonGrowthFin(Exception __exception, Town __0, int __state)`.
  - `!GarrisonGrowthOnlyRecruits` -> `true` (gra jak dzis), `__state = -1`.
  - Straz (`CrownGifts.IsWatch(st.MapFaction)`, K:CrownGifts.cs:36) -> `true`, `__state` = ludzie zalogi przed; finalizer dopisuje przyrost do "Straz zostawiona do E4/9"
    (Z6 - E4/7 tez daje Strazy ""; E4/9 zdejmie ten wyjatek).
  - reszta: licznik "zablokowane" += `(int)Campaign.Current.Models.SettlementGarrisonModel.CalculateBaseGarrisonChange(st, false).ResultNumber` (gorna granica - bez limitow gry
    rozmiaru i zoldu z :206-221; sam model, bez opisow), `return false`.
- Kolizje celu: Measure169c `TownPre/ModelFin` (K:Measure169c.cs:95, :271-273 - prefiks First + finalizer; finalizer biegnie mimo false i zmierzy 0 poza Straza),
  ClanBudget `GarRecruitPrefix` (K:ClanBudget.cs:917, :1074-1086 - bool Strazy przy pulapie puli; nasz Straz przepuszcza, wiec kolejnosc bez znaczenia).
  Harmony 2: kazdy prefiks biegnie, oryginal pomijany, gdy ktorykolwiek zwroci false (P 5.1 :574 "okno void First biegnie mimo false").
- Bez zmiany: BK nadal ustawia "Enlistment" (koszt administracji BK:...BKAdministrativeModel.cs:39-40) - tylko ludzi z niczego nie ma. Dymek miasta pokazuje dalej "+1"
  (`GetGarrisonChangeExplainedNumber` :223-234) - kosmetyka, do Ryzyka, bez latki.

### 4.2 Pomiar A5 (dowod do K8) - bez zmiany gry
- `TickAutoRecruitmentGarrisonChange(Town)` (G :76-95): prefiks void + finalizer `A5Pre/A5Fin` -> `[ThreadStatic] _a5Town`. (Ten cel lataja: Measure169c :94, ClanBudget :916,
  RecruitSources :171, RecruitCost :383, E4/7 3.4 - wszystkie void albo bool Strazy; nasze void.)
- Postfiks na private `GetAutoRecruitmentGarrisonChangeExplainedNumber(Town)` (G :168-204; zwraca `ExplainedNumber`): `public static void A5LimitPostfix(Town __0, ExplainedNumber __result)`
  - tylko gdy `_a5Town == __0` (UI wola ja tez z `GetGarrisonChangeExplainedNumber` :230). `BaseNumber` = liczba ochotnikow w zasiegu (:177-178), `ResultNumber` = po limitach
  (zold :188-190, miejsce :192-195, limit dzienny :197-200 - `GetMaximumDailyAutoRecruitmentCount`, Default = 1, G DefaultSettlementGarrisonModel.cs:40-43; ROT przekazuje dalej
  D:...ROTSettlementGarrisonModel.cs:51-54). Klasy: `Base == 0` -> "bez ochotnikow"; `Result >= dzienny && Base > Result` -> "limit dzienny"; `Result < Base` (inaczej) -> "zold albo miejsce".
- Z30A: A5 +151/d (linia 169c), "autowerbunek zalog: ludzi N w N twierdzach" (log:306, :574 - 1 na twierdze) - limit dzienny prawie na pewno wiaze (227 twierdz).

### 4.3 Czego K1 NIE robi
A5 dalej 1/dobe (gra). Jesli T-prog zalog (N4) nie przejdzie - K8 (rozdz. 18).

---

## 5. Milicja (K2) - przyrost tylko z wolnych rak, ubytek do domu

### 5.1 Skad dzis milicja (wszystkie miejsca zapisu `Settlement.Militia` w grze - grep G)
| Miejsce | Co | E4/8 |
|---|---|---|
| G Village.cs:223-240 (`Militia += MilitiaChange` :235); u nas zastepuje go prefiks BK `VillageDailyTicktPatch` (BK:...NotablePatches.cs:207-225, :222 - ten sam rachunek, `return false`) | tick wsi | okno 5.2 |
| G Town.cs:596-641 (`internal DailyTick`, `Militia += MilitiaChange` :639) | tick miasta i zamku | okno 5.2 |
| G IncreaseSettlementHealthAction.cs:7-16 (`private static ApplyInternal`, +20 przy powrocie wsi do Normal :14; wola `Apply` :18-21 z VillageHealCampaignBehavior.cs:30) | odbudowa wsi | okno 5.3 |
| G MilitiasCampaignBehavior.cs:46-52 (`private OnAfterSiegeCompleted`, +`MilitiaToSpawnAfterSiege` = 90-108, G DefaultSettlementMilitiaModel.cs:36-39) | obrona oblezenia | okno 5.3 |
| G DisbandPartyCampaignBehavior.cs:290 -> `MergeDisbandPartyToVillage` :364-384 (+0.5 x ludzie :383) | rozwiazana partia we wsi | E47 4.3 + 5.6 |
| G MilitiasCampaignBehavior.cs:15-44 (nowa gra: MilitiaChange x 45) | bilans otwarcia | siew E4/7 (przed siewem no-op) |
| G RebellionsCampaignBehavior.cs:205-245 (=100, potem do zalogi x0.6 i =0); G EncounterGameMenuBehavior.cs:2188 (=0, gracz bez oporu); G IncidentEffect.cs:657; G CampaignCheats.cs:926, :2725 | bunt, gracz, incydenty, konsola | P2 - nazwane w Reconcile E47 |
Model czynny: ROT owija poprzedni (D:...ROTSettlementMilitiaModel.cs:17-45: +10 "City Watch" 3 osady, +5 14 miast), pod spodem BK (BK:...BKMilitiaModel.cs:54-: +0.25% klas
dzierzawcow i chlopow pod pulapem, **ujemny "Over supported limit" :89** - stad z30A min -2945/d). Z30A: model w ticku +91.6/d (wsie -47.6, miasta +45.9, zamki +93.2),
milicja 63 002 -> 67 283 (d1 -> d120).

Mechanika ludzi milicji (G Settlement.cs:201-225, :1257-1320): `Militia` = ludzie partii milicji + ulamek `_readyMilitia`; setter przelewa calych ludzi do partii
(`AddMilitiasToParty` :1276-1281) albo zdejmuje losowo proporcjonalnie (`RemoveMilitiasFromParty` :1300-). **Ludzie = szeregowi partii milicji** (`TotalRegulars`), ulamek to nie ludzie.

### 5.2 Okna tickow - `Village.DailyTick()` i `Town.DailyTick()`
- `public static void MilVillagePre(Village __instance, out int __state)` / `MilTownPre(Town __instance, out int __state)` **Priority.First** - `__state` = szeregowi partii milicji
  osady przed (brak partii albo nieaktywna = 0; `-1` = nic nie robic: klucz wylaczony, `!LevyLive`, kampania nie gotowa).
- `public static void MilVillagePost(Village __instance, int __state)` / `MilTownPost(Town __instance, int __state)` **postfiks** (biegnie takze, gdy prefiks BK zwrocil false;
  przed wszystkimi finalizerami - wiec finalizery pomiaru Measure40 W1/W2 K:Measure40.cs:263-287, :311-328 i podlogi E4/4 K:PopulationLaw.cs:677-690 widza stan PO korekcie).
- Cialo wspolne `MilitiaDelta(Settlement s, int men0, int src)`; `d = szeregowi po - men0`:
  - `d > 0` (przyrost): region = `OutlawLaw.RegionFor(s)` (K:OutlawLaw.cs:243).
    - Straz (Z6) -> `GiveNone(milicja, d, TMilitiaWatch)` ("Straz - do E4/9"), koniec;
    - osada Innych (`s.MapFaction` = klan Innych - ten sam test co E4/2 `OthersRise.BirthPostfix`; kultura osady whitewalker) -> zdjac d (ponizej), licznik "osady Innych";
    - `FreeHands.Braked(region)` -> zdjac d, licznik "bez wolnych rak" wedlug krainy;
    - inaczej `got = PeopleBook.TakeLevy(s, d, MLevyMilitia, out reg)`; `GiveNew(milicja.MemberRoster, reg, got, TMilitiaLevy)`; `d - got` -> zdjac (licznik "podloga").
  - `d < 0` (ubytek bez walki): `var sh = PeopleBook.Take(milicja.MemberRoster, -d, TMilitiaHome)` -> `ArriveAt(s, sh, -d, AService, MBackMilitia, MBackMilitiaNone, migrate: false)`
    (rozdz. 10.3: czesc regionu osady wraca do TEJ osady - wies +n/k_v, miasto +n stanu, zamek - najslabsza wies regionu; inne regiony - `HomeKnown`; "" - `Note`).
    **Slownik zdejmowany po fakcie** (gra juz zdjela ludzi) - `Take` nie stoi miedzy zdjeciem a dopisaniem w kodzie gry (U24).
  - "Zdjac n": z partii milicji n szeregowych bez losowania - od najliczniejszego typu, `AddToCounts(typ, -k)` (zdrowi przed rannymi); `_readyMilitia` bez zmian (ulamek zostaje).
    Ludzie, ktorych gra dodala z niczego i ktorych zdejmujemy, nigdy nie dostali udzialu - slownik zgodny (Reconcile bez roznicy).
- Koszt: ok. 1 130 tickow/d (wsie + warownie) x odczyt `TotalRegulars`; praca ksiegi tylko przy d != 0.

### 5.3 Okna jednorazowe
- `IncreaseSettlementHealthAction.ApplyInternal(Settlement, float)` (private static - NIE jednolinijkowe `Apply` :18-21, JIT moze je wkleic; wzor E4/3 `DestroyPartyAction.ApplyInternal`):
  `MilHealPre(Settlement __0, out int __state)` + `MilHealPost(Settlement __0, int __state)` -> `MilitiaDelta(__0, __state, SrcHeal)`.
- `MilitiasCampaignBehavior.OnAfterSiegeCompleted(Settlement, MobileParty, bool, MapEvent.BattleTypes)` (private): `MilSiegePre(Settlement __0, out int __state)` / `MilSiegePost`.
- Nasze latki na tych celach - brak (grep K). Cudze - brak w BK/ROT (grep BK, D).

### 5.4 Hamulec i R5
Milicja liczy sie do S (E47 6.2) - przyrost z wolnych rak podnosi a, az region trafi w hamulec; wtedy milicja stoi (decyzja Jeffa 11.10). Dar (Straz) i Wolni Ludzie (f = 1) - bez
hamulca (E47 6.3); Dar i tak do "".

### 5.5 Gracz = AI
Te same okna dla osad gracza (gracz nie ma osobnej drogi przyrostu milicji poza P2 z tabeli 5.1).

### 5.6 Rozwiazana partia we wsi (+0.5 milicji) - dopisek w oknie E47 4.3
Prefiks E4/7 na `OnPartyDisbanded` (G :290) zapisuje dodatkowo szeregowych milicji `relatedSettlement` (jesli wies). W postfiksie E4/7, zanim "reszta skladu -> HomeOrNote":
`int mil = milicja po - przed`; `Give(milicja, org.Slice(mil), TMilitiaDisband)` (ci ludzie zostali w milicji swojej wsi), a `HomeOrNote` tylko dla reszty. Bez E4/8 (klucz off) -
jak w E47 (milicja +0.5 "z niczego", nazwana w Ryzyku E47 4.3).

---

## 6. Nowe druzyny lordow (K3) - szablon z ludzi, zboze z zapasu siedziby (PLAN 4.9)

### 6.1 Skad dzis
`HeroSpawnCampaignBehavior.ConsiderSpawningLordParties` (G:...HeroSpawnCampaignBehavior.cs:175-201) -> `SpawnLordParty` (:246-278; ClanBudget `SpawnPrefix/Postfix` K:ClanBudget.cs:914,
:979-1013 - bramka miejsca w pulapie) -> `MobilePartyHelper.SpawnLordParty` (G Helpers\MobilePartyHelper.cs:17-30) -> `LordPartyComponent.CreateLordParty` (G:...LordPartyComponent.cs:134-138)
-> `MobileParty.CreateParty` (G:...MobileParty.cs:4132-4145: `Create` :4139 PRZED `OnMobilePartyCreated` :4142) -> `OnMobilePartySetOnCreation` (:118-132) ->
**`InitializationArgs.InitializeLordPartyProperties(MobileParty, Hero)`** (:29-47): wodz :31, szablon klanu (buntownicy - `RebelsPartyTemplate`) `InitializeMobilePartyAroundPosition` :38-39
(rod gracza - bez szablonu :32-35), **zboze 15-30 z niczego :41**. Potem `GiveInitialItemsToParty` (:280-309 - konie x7 i zywnosc 0.1 z produkcji okolicznych wsi, z niczego).
Te sama droga: BK rycerze (BK:...BKGentryBehavior.cs:372 `MobilePartyHelper.SpawnLordParty`), Spoils (`SubClanBehavior.SpawnLordParty` - SpoilsCompany zdejmuje szablon).
Z30A: 9.5 nowych partii rodow/d, 505 ludzi/d przy utworzeniu ("Partie (E4/0)"); szablony lordow ROT 53-56 (K:SpoilsCompany.cs:23-26).

### 6.2 Latka na `InitializeLordPartyProperties(MobileParty, Hero)`
- `public static void SpawnPre(MobileParty __0, Hero __1, out SpawnState __state)` **Priority.First**: szeregowi skladu przed, sztuki zboza (`DefaultItems.Grain`) przed.
- `public static void SpawnPost(MobileParty __0, Hero __1, SpawnState __state)` **Priority.Low** - po SpoilsCompany `TemplatePostfix` (Normal, K:SpoilsCompany.cs:198-230, wpiecie :541 - dla klanu
  Spoils szablon juz zdjety, wiec u nas 0) i PRZED LosersFlee `LordInitPostfix` (Last, K:LosersFlee.cs:1213-1223, :1549 - jego licznik widzi stan po oplaceniu).
- Cialo (n = szeregowi po - przed; kazda czesc we wlasnym try, licznik potkniec):
  1. n <= 0 albo rod gracza -> tylko zboze (pkt 6). Inni (`Undead.Party(__0)` K:Undead.cs:50 / klan Innych) -> nic (E4/2 `BirthPostfix` K:OthersRise.cs:121 sam tnie do puli CIALA).
  2. Straz (Z6) -> `GiveNone(sklad, n, TSpawnWatch)`.
  3. `!NewPartyFromPeople || !LevyLive` -> nic (Reconcile E47 nazwie "szablon klanu z1t").
  4. Siedziba = `__1.HomeSettlement`; gdy null albo nie wies/miasto/zamek - `OutlawLaw.RegionAt(pozycja partii)` (K:OutlawLaw.cs:244). Region = `RegionFor(siedziba)`.
  5. (a) `!FreeHands.Braked(region)` -> `a1 = TakeLevy(siedziba, n, MLevySpawn, out reg)`; `GiveNew(sklad, reg, a1, TSpawnLevy)`.
     (b) reszta r = n - a1 > 0 i `NewPartyFromSeatGarrison` i siedziba ma zaloge: `t = min(r, zdrowi szeregowi zalogi - (miasto 50 : zamek 25))` (ta sama rezerwa co gra -
     G DefaultSettlementGarrisonModel.cs:87-92); zdejmij t ludzi szablonu ze skladu (6.3), przenies t zdrowych z zalogi do skladu proporcjonalnie do typow
     (`AddToCounts(typ, -k)` w zalodze, `+k` w skladzie), `Give(sklad, Take(zaloga, t, TSpawnGarrison), TSpawnGarrison)`; przy `GarrisonArmory.KitMoves` - `GarrisonArmory.MoveKits(zaloga, partia, moved, "nowa druzyna (E4/8)")`
     (K:GarrisonArmory.cs:154, wzor `TakePostfix` :391-405 - sprzet idzie z ludzmi).
     (c) reszta r2 -> zdejmij r2 ludzi szablonu (6.3); liczniki: "region bez wolnych rak" (gdy (a) pominiete przez hamulec), "zaloga ponizej rezerwy", "podloga".
  6. **Zboze:** g = sztuki zboza po - przed (gra dala 15-30). `NewPartyGrainFromSeat` -> zdejmij g z partii; zrodlo zapasu = siedziba z `Town` (wies -> `Village.Bound`):
     `Town.FoodStocks >= g` -> `FoodStocks -= g`, dodaj g zboza (1 jednostka zapasu = 1 sztuka - ta sama rownowaznosc co dostawa konwoju zywnosci BK, BK:...BKPartyBehavior.cs:670-688,
     i 36b, K:TravellerTrade36b.cs:34-40); inaczej najblizsza warownia tej samej `MapFaction` z `FoodStocks >= 2g`; inaczej oddaj g z powrotem (zboze gry, licznik
     "z niczego - brak zapasu", prog N11). Bez zlota (zapas warowni to spizarnia pana - etap 4 nie rusza zlota, P :171).
- Gracz = AI: rod gracza nie ma szablonu (G :32-35) - nic do oplacenia; zboze gracza - ta sama regula (gracz dostaje :41 tak samo).

### 6.3 Zdejmowanie ludzi szablonu
Proporcjonalnie do skladu szablonu (zachowac ksztalt), bez wodza i bez bohaterow, typ po typie z kopii (wzor `SpoilsCompany.StripNew` K:SpoilsCompany.cs:154-171). Nigdy ponizej
stanu sprzed (`SpawnState`). Ludzie zdjeci nie maja udzialu - slownik zgodny.

### 6.4 Dary startowe gry (konie, zywnosc) - tylko pomiar
Prefiks/finalizer na private `HeroSpawnCampaignBehavior.GiveInitialItemsToParty(MobileParty)` (G :280-309): sztuki i wartosc dopisane (licznik w linii, "poza E4/8 - etap 6").

### 6.5 Skutek dla wojska
Szablon kosztuje DOM ok. 505 ludzi/d [S z z30A]; w regionie z hamulcem - zaloga siedziby albo mniejsza druzyna. To decyzja Jeffa 11.10 ("kraina bez wolnych rak nie wystawia
nowego wojska"), ale tez ryzyko progu "wojsko lordow w wojnie 95-115 tys." (rozdz. 15.3).

---

## 7. Karawany (K4) - z ludzi osady startu

### 7.1 Nowa karawana - postfiks na `CaravanPartyComponent.CreateCaravanParty(Hero, Settlement, PartyTemplateObject, bool, Hero, ItemRoster, bool)`
- Cel: G:...CaravanPartyComponent.cs:242- (public static). Sklad powstaje w `MobileParty.CreateParty` -> `OnMobilePartySetOnCreation` (:188-198) -> `InitializeCaravanOnCreation`
  (:31-70: szablon :56, mistrz karawany - zwykla postac kultury, gdy brak wodza-bohatera :60-65) - czyli PRZED powrotem z `CreateCaravanParty`. Jedno wejscie dla karawan gry
  (G CaravansCampaignBehavior.cs:507), kupna BK, gracza i konwoju DTE (K:CaravanCapital.cs:253-268).
- `public static void CaravanPost(Settlement __1, bool __3, MobileParty __result)` **Priority.Low**: n = szeregowi skladu; `__3` (isInitialSpawn) albo `!CaravansFromPeople || !LevyLive` -> nic;
  Straz (region nightswatch) -> `GiveNone`; inaczej `c1 = TakeLevy(__1, n, MCaravanOut, out reg)` **bez hamulca** (pytanie P1), `GiveNew(sklad, reg, c1, TCaravan)`, reszta -> zdejmij
  (bez mistrza karawany - zostaje zawsze; licznik "nie wystawieni - brak ludzi").
- Kolizja celu: CaravanCapital (Seal31.Patch, prefiks + finalizer na tym samym celu - K:CaravanCapital.cs:258) - tylko zloto kapitalu; nasz postfiks tylko ludzie.

### 7.2 Uzupelnienie konwojow morskich - `CaravansCampaignBehavior.RefillConvoyTroops(MobileParty)`
- G :752-786 (private; z szablonu z niczego do limitu partii), wolane przy kazdym wejsciu konwoju do miasta (`OnSettlementEntered` :691-703).
- Prefiks void (szeregowi przed) + postfiks: d > 0 -> `TakeLevy(convoy.CurrentSettlement, d, MCaravanRefill)` + `GiveNew`, reszta zdejmij. Nasze latki na celu - brak.

### 7.3 Powrot
Karawana rozwiazana bez bitwy - rozdz. 10.4 (`ArriveAt(HomeSettlement)`). Rozbita - E47 4.6/4.7 (H3 "z szablonu" karawana - czesc znana `HomeKnown`, K:LosersFlee.cs:424-435).
Karawana rozwiazana do zalogi - E47 4.3 (przeniesienie).

---

## 8. Wedrowcy i konwoje BK (K5) - R19, U15f

### 8.1 Skad dzis (BK, dekompilacja bkfull)
- **Wedrowcy:** `BKPartyBehavior.DailySettlementTickImpl` (BK:...BKPartyBehavior.cs:436-503, kazda osada z `Town`) -> `DecideSendTravellers` (:510-556, 5%/dobe, cel = pierwsze lenno z
  `GetTownsToTravel` :745-764) -> `SendTravellerParty` (:766-799: chlopi 30-70, rzemieslnicy 15-30, szlachta 10-15) -> **`PopulationPartyComponent.CreateTravellerParty`** (BK:...Components\PopulationPartyComponent.cs:207-271:
  cywile + eskorta z szablonu milicji/elity :214-261, sklad w `InitializeMobilePartyAroundPosition` :262 - PO `CreateParty` :209).
- **Konwoje:** `CreateFoodCaravan` (:169-205; wola BK:...ClusterFoodTracker.cs:141, miasto -> miasto) i `CreateSlaveCaravan` (:141-167, void; wola BKPartyBehavior.cs:801-808; niewolnicy jako
  jency "looter" :161) - eskorta z szablonu karawany (`InitializeMobilePartyAtPosition` :162, :200).
- **Koniec:** `OnSettlementEntered` (:558-575) -> `AddPopulationPartyBehavior` (:616-692): BK dopisuje cywilow do klas BK osady (:688-690) i `DestroyPartyAction.Apply(null, party)` (:691);
  przy oblezeniu BK niszczy partie w osadzie (BKPartyBehavior.cs:262-279); bez celu i z wrogim domem - niszczy w drodze (PopulationPartyComponent.cs `TickHourly` :463-470); zawrocenie do domu przy oblezonym/spalonym celu (PopulationPartyComponent.cs:446-461).
- Z30A (36b, 120 dob): wyruszylo 1 174 partii wedrowcow z 65 566 ludzmi (546/d, ok. 56 na partie), doszlo 1 167, **w tym 1 093 do domu (93.7%)**, srednio 0.15 doby drogi;
  "Partie (E4/0)": konwoje BK 26.1/d, przy utworzeniu 0 ludzi (zdarzenie gry przed wypelnieniem skladu).

### 8.2 Wyjazd - ramka tworzenia
- Prefiksy void na `CreateTravellerParty` (pochodzenie `__1`), `CreateFoodCaravan` (`__0`), `CreateSlaveCaravan` (`__0`): `[ThreadStatic] _bkOrigin` (stos jak LevyGold :36-48) + finalizery.
- Nasluch `MobilePartyCreated` (nowy w K:ArmouryBehavior.cs obok :702) -> `ArmsFromPeople.OnPartyCreated(mp)`: przy otwartej ramce i `mp.PartyComponent` typu `PopulationPartyComponent`
  (po nazwie typu, jak K:PeopleLedger.cs:118) - partia do listy ramki.
- Finalizer (sklad juz wypelniony): dla kazdej partii z listy: n = szeregowi skladu; `!TravellersFromPeople || !LevyLive` -> nic; Straz (Dar) -> `GiveNone`; inaczej
  `n1 = TakeLevy(pochodzenie, n, MRoadOut, out reg)` bez hamulca (to rodziny, nie rekruci), `GiveNew(sklad, reg, n1, TRoadOut)`, reszta -> `GiveNone(..., TRoadNone)` (BEZ zdejmowania -
  BK liczy u celu cywilow ze skladu; "" nie dotyka DOM u celu, wiec nic z niczego). Niewolnicy w lochu konwoju niewolnikow -> `GiveNone(PrisonRoster, ..., TSlavesBk)` ("E4/9").
- Kolizje celu: TravellerTrade36b `CreatePre/CreatePost(Low)/CreateFin` (K:TravellerTrade36b.cs:153-155), TravellerGoods36 i CastleSteel (K:CastleSteel.cs:139-147) - towar; nasze - ludzie.

### 8.3 Koniec - galaz w `PeopleBook.OnPartyDestroyed` (rozdz. 10.4)
`mp.PartyComponent` = `PopulationPartyComponent` (wlasciwosci przez `PropertyInfo` w pamieci: `PopulationType`, `Kind`, `SlaveCaravan` - te same co K:TravellerTrade36b.cs:132-136):
- wedrowcy (`PopulationType != None`, `Kind` Unset): `mp.CurrentSettlement != null` -> **`ArriveAt(CurrentSettlement, sh, n, ARoad, MRoadIn, MRoadNone, migrate: true)`** (wszyscy
  znani do DOM osady wejscia; region inny niz pochodzenia = migracja `MigOut[poch] += m`, `MigIn[cel] += m`); `CurrentSettlement == HomeSettlement` - przyczyna `MRoadBack`;
  bez osady (zniszczeni w drodze) -> `ArriveAt(HomeSettlement, ..., migrate: false)`;
- konwoje (`Kind` Food/Slaves/Raw/Finished albo `SlaveCaravan`): eskorta zawsze `ArriveAt(HomeSettlement, ..., MRoadBack, migrate: false)` (straz konwoju wraca do domu);
  loch niewolnikow - `Note` "niewolnicy BK u celu (E4/9)".
- Bitwa (smierc, jency, rozbici) - ogolne okna E47 4.5-4.7 (slownik jest od wyjazdu).

### 8.4 R5
Wedrowcy i konwoje NIE wchodza do S (Z9) - sa w drodze, jak TABOR; w rownaniu regionu sa w SLUZBA (slownik), wiec rownanie sie zamyka (rozdz. 10.5).

---

## 9. Miasta - R2 (K6): narodziny g_wsi - 1 pkt, migracja ze wsi regionu

### 9.1 Podstawa
P R2 :148, DOK:524 ("przyrost miasta = g - 1.0 pkt (miasto traci ok. 0.5% rocznie); ubytek uzupelnia migracja z wlasnych wsi, gdy region nie gloduje (swiat ok. 144 ludzi
dziennie)"), DOK:635-642 (9c; Clark: Londyn 1580-1650 0.87 urodzenia na zgon). E4/4 zostawil miasto bez ruchu (K:PopulationLaw.cs:227-230, K:PeopleBook.cs:26-27 "zmienia go dopiero E4/8").

### 9.2 Stopa miasta - `PopulationLaw.TownRate(Settlement town, out TownRateParts p)`
`g_t = clamp(GrowthBasePercent + GrowthProsperityWeight x W(town) - GrowthDangerWeight x U(town) [- GrowthHungerWeight x H_t przy GrowthHungerEnabled], -GrowthMaxDeclinePercent, GrowthMaxPercent)
- TownGrowthPenaltyPercent` [% rocznie]. W = `ProsperityIndex(town)` (K:PopulationLaw.cs:465 - ta sama mediana krainy co wsie), U = `DangerOf(town)` (wydzielone z `Danger(Village)`
:503-521, ktore dla wsi liczy wedlug `v.Bound` - wiec W i U miasta = W i U jego wsi), H_t = srednia `HungerTerm(v)` wsi miasta (dzis `WinterBite.VillageCut`, :538). **Bez premii gry**
(mnoznik `Bonus` wsi :548-556 pochodzi z modelu hearth wsi - miasto go nie ma). Zakres g_t: -2.3..-0.2 przy domyslnych (wies 0.5-0.8 -> miasto -0.5..-0.2).

### 9.3 `TownGrowth.Daily()` (raz na dobe, po `TownsDaily` K:PopulationLaw.cs:1148, przed rentami - renty czytaja stan)
Warunek: `TownGrowthEnabled && TownPeopleFrozen && PopulationLaw.Calibrated`. Dla kazdego miasta (raz - `Settlement.All` podaje w ROT duplikaty, wzor `seenS` K:PeopleBook.cs:936-943) ze stanem `TryTown`:
1. `nat = P x g_t / 100 / YearDays()` (K:PopulationLaw.cs:424); `TownShift(miasto, nat, MTownNatural)`; `NoteBorn(miasto, nat)` (Z11 - netto naturalne; K_m R5 widzi tylko max(0, ...)).
2. Odniesienie `tr:` (zalozone leniwie: Ref = P, RefWsi = ludzie wsi miasta teraz). Cel = `Ref x (LudzieWsi / RefWsi)` (RefWsi <= 0 -> Ref) - udzial miejski regionu staly;
   LudzieWsi = suma hearth x k_v wsi miasta (uchodzcy E4/5 sa poza domem - nie licza sie).
3. Migracja - tylko gdy: `PeopleUnit.On` (inaczej stawka 0.5 hearth/czl - licznik), region nie gloduje (`Settlement.IsStarving` albo hak E4/6), miasto nie oblegane:
   - `RefillDays == 0` -> chce = max(0, -nat) (wersja DOK);
   - inaczej `luka = Cel - (P + nat)`; luka <= 0 -> chce = 0 (miasto ponad udzialem kurczy sie naturalnie); luka > 0 -> chce = min(luka, max(0, -nat) + luka / RefillDays).
   - `got = PeopleBook.TakeFromRegionVillages(miasto, chce, MTownMigrate)` - wsie miasta w stanie Normal, proporcjonalnie do ludzi, kazda nie ponizej 50 hearth (ta sama podloga co
     `OutlawLaw.TakeFrom` K:OutlawLaw.cs:326-337): `Move(v, ADom, ARoad, m_v, MTownMigrate, legacy)` (hearth -m_v/k_v); potem `TownShift(miasto, +got, MTownMigrate)`.
     Brak - licznik "wsie bez nadwyzki". Region bez zmiany (miasto i jego wsie to jeden region OutlawLaw) - rownanie bez czlonu migracji.
4. Wyjatek na jednym miescie - licznik potkniec, reszta miast liczy dalej (wzor K:PopulationLaw.cs:258-271).

### 9.4 Skala [S]
Swiat: ubytek naturalny ok. 10.5 mln x 0.5% / 364 = ok. 144 ludzi/d (DOK); migracja ok. 144/d + luka po poborze miejskim E4/7, karawanach i wedrowcach (rok na dopelnienie).
"Ludnosc:" netto spada o ubytek naturalny miast (ok. -144/d) - wliczone w T-prog netto (N14, decyzja Jeffa 11.10 -0.5%/rok w wojnie).

### 9.5 Czego R2 nie robi
Miasto nie rosnie ponad udzial miejski regionu z migracji (rosnie z nim, gdy rosna wsie). Zamki (k = 0, K:PopulationLaw.cs:308-316) - bez R2. Glod miasta (zgony) - E4/6.

### 9.6 Menu (Z18)
W oknie E47 7 dla miasta jedna linia: `"Townsfolk: X (a year at today's rate: births minus deaths n, newcomers from the villages m)"` - z liczb doby, bez nowego stanu.

---

## 10. Ksiega (PeopleBook.Arms.cs)

### 10.1 Nowe przyczyny ruchu DOM (po ostatniej przyczynie E4/7 - Z15; `MHearth` w nawiasie)
`MLevyMilitia` (true) "milicja z wolnych rak (E4/8)", `MBackMilitia` (true) "milicja bez walki do domu (E4/8)", `MBackMilitiaNone` (false) "milicja bez pochodzenia do domu - bez hearth",
`MLevySpawn` (true) "nowa druzyna z wolnych rak regionu siedziby (E4/8)", `MCaravanOut` (true) "karawana z ludzi osady startu (E4/8)", `MCaravanRefill` (true) "konwoj morski uzupelniony z miasta (E4/8)",
`MRoadOut` (true) "wedrowcy i eskorta BK wyjazd (R19)", `MRoadIn` (true) "wedrowcy BK u celu (R19)", `MRoadBack` (true) "wedrowcy i eskorta BK z powrotem w domu (R19)",
`MRoadNone` (false) "wedrowcy BK bez pochodzenia - bez hearth", `MTownMigrate` (true) "migracja ze wsi do miasta (R2)", `MTownNatural` (false - tylko licznik stanu miasta) "narodziny i zgony miast (R2)".
Przyczyny Take/Give (liczniki "Pochodzenie", Z17): `TMilitiaLevy`, `TMilitiaHome`, `TMilitiaWatch`, `TMilitiaDisband`, `TSpawnLevy`, `TSpawnGarrison`, `TSpawnWatch`, `TCaravan`, `TRoadOut`, `TRoadNone`, `TSlavesBk`.
Konto: `ARoad` (nastepne wolne po E4/7) - "w drodze" (migranci i ludzie miedzy wsia a miastem; dla `Move` istotny tylko znak `to/from == ADom`, K:PeopleBook.cs:707).

### 10.2 `TownShift(Settlement town, double men, int cause)` (Z4)
Jedyna zmiana `_town` poza zalozeniem: `lock (Gate)`, nie ponizej 0 (reszta - licznik "miasto na zerze"), licznik wedlug przyczyny (`_townIn/_townOut`) - linia "Miasta" liczy z niego kontrole.
Wola: E4/7 (pobor miejski, karczma - D1), E4/8 (karawany, wedrowcy, migracja, naturalne), E4/6 (glod).

### 10.3 `ArriveAt(Settlement s, Shares sh, double n, int fromAccount, int causeKnown, int causeNone, bool migrate)`
- `migrate == false`: udzial regionu `RegionFor(s)` -> do s: wies `Move(v, from, ADom, m, causeKnown, PeopleUnit.GamePerMan)`; miasto `TownShift(s, +m, causeKnown)`; zamek
  `OutlawLaw.ReturnHome(region, m, from, causeKnown, out placed)` (K:OutlawLaw.cs:344-358); udzialy innych regionow -> `HomeKnown`; "" -> `Note(causeNone)`.
- `migrate == true`: wszystkie znane udzialy -> do s jak wyzej; dla `reg != RegionFor(s)`: `RegRef[reg].MigOut += m`, `RegRef[cel].MigIn += m`.
- Nie umieszczeni (ReturnHome bez wsi) -> "" (licznik). Nigdy nie rzuca; nie stoi miedzy zdjeciem a dopisaniem w cudzym kodzie (zdjecie robi E4/7 `Take` na slowniku).

### 10.4 Koniec partii bez bitwy - galaz w `OnPartyDestroyed` (Z14)
Kolejnosc w nasluchu (K:PeopleBook.cs:756-773 + E47 4.12): (1) R18 tabory (jest); (2) **E4/8: `PopulationPartyComponent`** - rozdz. 8.3; (3) **E4/8: `destroyer == null && mp.MapEvent == null
&& mp.SiegeEvent == null` i partia ze slownikiem** -> `sh = Take(sklad, slownik.Sum)` (NIE `TotalRegulars` - okno E47 4.3 moglo juz zdjac udzialy rozwiazanych) ->
`ArriveAt(mp.HomeSettlement ?? region, sh, ..., AService, MBackDisband (E4/7), MSoldier, migrate: false)` (R6 "rozwiazana partia -> DOM pochodzenia"); loch - bez zmian E47;
(4) reszta - "w nicosc przy zniknieciu" (E47 4.12).

### 10.5 Rownanie regionu (E47 rozdz. 5) - dopisane
`... + Fallen_r + Civil_r + Enslaved_r + ToWall_r + MigOut_r - MigIn_r = reszta_r`; `Born_r` zawiera netto naturalne miast (Z11). Migracja wies -> miasto - w regionie (bez czlonu).
Linia "Ludzie (ksiega)" E47 8.3: "migracja R19 od siewu: wyszlo X, przyszlo Y (par regionow Z)".

---

## 11. Klucze Settings (po kluczach E4/7; `python tools/gen_mcm.py`; przy kodzie `grep` kazdego klucza w `Armoury.json` Jeffa - CLAUDE.md 7; dzis zaden nie istnieje)

| Klucz | Domyslna | Uzasadnienie | Podpowiedz MCM (EN) |
|---|---|---|---|
| `GarrisonGrowthOnlyRecruits` | true | P :491-492, prog etapu :548 | garrisons grow only from men who really join them - recruits the garrison pays for and men left by parties; the game's free daily growth (enlistment policy, rebels, quests) adds no one. The Night's Watch keeps it until it gets its own recruits from all of Westeros |
| `MilitiaFromFreeHands` | true | P :493-494, PLAN 4.7 | militia grows only from the free hands of its own land: each new militiaman leaves his village (1/k of a hearth) or town, a land with no free hands left raises no new militia, and militia that shrinks without a fight goes back home |
| `NewPartyFromPeople` | true | P :494-495, PLAN 4.9, Jeff 11.10 | a lord raising a new party takes his men from the free hands of his seat's land, not out of thin air; with no free hands and no garrison to spare he rides out with fewer men and recruits the rest |
| `NewPartyFromSeatGarrison` | true | P :494 ("z zalogi siedziby albo...") | when the land has no free hands left, a new party takes its men from the garrison of the lord's seat (the garrison keeps at least 25 men in a castle, 50 in a town) |
| `NewPartyGrainFromSeat` | true | PLAN 4.9 ("i zboza z niczego") | the grain a new lord's party sets out with comes from the food stores of his seat (or the nearest fortress of his realm), not out of thin air |
| `CaravansFromPeople` | true | P :495 | a new caravan's guards and drivers are people of the town it sets out from (villagers, if it starts from a village); those who come back alive return home |
| `TravellersFromPeople` | true | P R19 :165, U15f | Banner Kings travellers and convoys are real people: they leave their home's people when they set out and join the people of the place they reach (most simply come back home); convoy escorts go back home when the convoy is done |
| `TownGrowthEnabled` | true | P R2 :148, DOK:524 | towns have their own births and deaths (a medieval town buried more than it baptised) and are refilled by people from their own villages when the land is not starving. Needs Town People Frozen |
| `TownGrowthPenaltyPercent` | 1.0 | DOK:524, :638 | how many points a year a town's natural rate stays below that of its villages (1 = London 1580-1650, about 0.87 births for each death) |
| `TownMigrationRefillDays` | 364 | P R2 "wyrownujaca ubytek", pytanie P2 | villagers move to their town to cover its natural shortfall and the gap to its share of the land's people within about this many days (0 = only the natural shortfall) |
Kolejnosc wylaczania przy niezdanym progu (jedna zmiana naraz): `TravellersFromPeople`, `CaravansFromPeople`, `NewPartyGrainFromSeat`, `NewPartyFromSeatGarrison`, `NewPartyFromPeople`,
`MilitiaFromFreeHands`, `TownGrowthEnabled`, `GarrisonGrowthOnlyRecruits` (ostatni - to prog etapu).

---

## 12. Linie logu (przy LogEnabled)

### 12.1 Startowa (pierwsza doba kampanii, wlasciciele latek przez `Harmony.GetPatchInfo` jak E4/0 U25)
`Wojsko z ludzi (E4/8): wpiete: przyrost zalog z modelu (TickGarrisonChangeForTown), A5 - pomiar limitow 2/2, milicja - tick wsi, milicja - tick warowni, milicja po odbudowie wsi (IncreaseSettlementHealthAction.ApplyInternal), milicja po obronie oblezenia (OnAfterSiegeCompleted), nowa druzyna lorda (InitializeLordPartyProperties), dary startowe druzyny - pomiar (GiveInitialItemsToParty), karawany (CreateCaravanParty), konwoje morskie (RefillConvoyTroops), wedrowcy BK (CreateTravellerParty), konwoje BK 2/2 (CreateFoodCaravan, CreateSlaveCaravan); BRAK: -; wlasciciele latek celow: <cel: Harmony id, ...>; ksiega pochodzenia E4/7: TAK/BRAK; ustawienia startowe przed MCM (stan gry - linia dzienna).`

### 12.2 Dzienna (po "Pochodzenie (E4/7)" i "Ludzie (ksiega)")
`Wojsko z ludzi (E4/8): dzien N | wlaczone: zalogi T/N, milicja T/N, nowe druzyny T/N (z zalogi T/N, zboze T/N), karawany T/N, wedrowcy BK T/N; ksiega pochodzenia: CZYNNA/BEZ SIEWU/WYLACZONA | ZALOGI: przyrost z modelu zablokowany -z w t twierdzach (gorna granica - model bez limitow gry), Straz zostawiona do E4/9 +s w u twierdzach | A5: twierdz z werbunkiem b (ludzi a), limit dzienny c, bez ochotnikow d, zold albo miejsce e | MILICJA: przyrost w oknach +m = z wolnych rak m1 (wsie m1v, miasta m1t, zamki m1c; tick m1k, odbudowa wsi m1h, obrona oblezenia m1o) + Straz "" m2; odmowione -r (bez wolnych rak r1 w k1 regionach [krainy: ...], osady Innych r2, podloga r3); ubytek bez walki do domu -h (do tej osady h1, inne regiony h2, bez pochodzenia h3); z rozwiazanych partii we wsi +j | NOWE DRUZYNY: p partii (Straz p1, Inni p2, rod gracza p3), szablon n = z wolnych rak a1 + z zalogi siedziby a2 + nie wystawieni a3 (bez wolnych rak a3h, zaloga ponizej rezerwy a3g, podloga a3f) + Straz "" a4; zboze g szt.: z zapasu siedziby g1, z innej warowni g2, z niczego (brak zapasu) g3; dary startowe gry (poza E4/8 - etap 6): i szt. za v zl | KARAWANY: k nowych (ludzi n): z ludzi osady startu c1 (miasta c1t, zamki c1c, wsie c1v), nie wystawieni c2; konwoje morskie uzupelnione c3 (nie wystawieni c4); rozwiazane bez bitwy do domu c5 | WEDROWCY BK (R19): wyjazdy w (ludzi n: wedrowcy n1w, eskorta konwojow n1e, bez pochodzenia n3), dojscia: do domu b1, do innej osady regionu b2, migracja do innego regionu b3 (par regionow r), zniszczeni w drodze do domu b4; niewolnicy BK w konwojach (E4/9) s; w drodze teraz x (z pochodzeniem x1) | ROZWIAZANE BEZ BITWY (R6): partii q, do domu q1, bez pochodzenia q2 | KONTROLA: zdjete z DOM przyczynami E4/8 - (m1 + a1 + c1 + c3 + n1w + n1e) = 0.00 | potkniecia P | czas T ms.`

### 12.3 Miasta
`Miasta (E4/8 R2): dzien N | miast M ze stanem, ludzi X (wczoraj Y, zmiana d) | naturalne n (stopa srednia g% rocznie = wies w% - kara k pkt; najnizsza gmin% miasto, najwyzsza gmax%) | migracja ze wsi +m (ubytek naturalny m1, do udzialu miejskiego m2) w a miastach, bez migracji: glod h1, oblezenie h2, wsie bez nadwyzki h3 (brakowalo h3m), jednostka ludzi wylaczona h4 | inne ruchy stanu miast: pobor i karczmy (E4/7) -p, karawany -c, wedrowcy BK -w1 / +w2, milicja -l1 / +l2, nowe druzyny -s, glod (E4/6) -f, miasto na zerze z | kontrola: X - Y - (n + m - p - c - w1 + w2 - l1 + l2 - s - f) = 0.00 | udzial miejski swiata dzis u% (odniesienie u0%, odniesien tr: T) | potkniecia P.`

### 12.4 Zmienione linie
- `Hearth (E4/0)` (K:Measure40.cs:862-903): do `named` i tekstu - "nasze (E4/8): milicja z wolnych rak -x, milicja do domu +x, nowe druzyny -x, karawany -x, wedrowcy BK -x/+x, migracja do miast -x"
  (z `mv[...]` po nazwach przyczyn 10.1) - inaczej RESZTA hearth wchlonie [S] ok. -1..-3 hearth/d.
- `Ludzie: przyrost naturalny` (K:PopulationLaw.cs:901-904): "miasta: ... stan (E4/4) - narodziny, zgony i migracja: linia Miasta (E4/8 R2)" gdy R2 czynne.
- `Ludzie (ksiega)` (E47 8.3): odcinek migracji R19 (10.5).
- LosersFlee "dziura werbunku" (K:LosersFlee.cs:1257): dopisek (rozdz. 2).

---

## 13. Narzedzie `tools/sprawdz_logi.py --grupa etap4` - blok E4/8 (rusza przy linii startowej "Wojsko z ludzi (E4/8):"; stan z linii dziennej, nie startowej - poprawka R2-1 E4/3)

| # | Prog | Jak | Baza / skad |
|---|---|---|---|
| N1 | 0 ERROR naszych modow; linia startowa "BRAK: -"; potkniecia E4/8 <= 5 z nazwa miejsca | log | standard |
| N2 | **"Zalogi (169c)": przyrost z modelu zalogi gry - "Straz zostawiona do E4/9" (E4/8) = 0 kazdej doby** | dwie linie tej samej doby | P :497, prog etapu :548; z30A +105/d |
| N3 | "Pochodzenie (E4/7)" z niczego: zalogi z2m = Straz, milicje z3 <= 1% ruchu, partie rodow z1t <= Straz + Spoils, karawany z4 ~0; "ludzie pod bronia z niczego" nienazwane <= 1% ruchu brutto (srednia 28 dob) | linia E47 | P :497, rozdz. 4 :550; z30A T7 +1048/d |
| N4 | zalogi d120 >= 85% bazy (bieg A zbioru 31 d120 - gdy gotowy; zapas: z30A d120 80 255 -> >= 68 217) | "Zalogi (169c)" | P :497-498 (78 924 w biegu 457); [S] bez modelu ok. 67.6 tys. - PROG GRANICZNY (rozdz. 15.3) |
| N5 | wojsko lordow w wojnie 95-115 tys. srednio d90-d120 (podloga 85) | progi etapu 2 | P rozdz. 4 :539 |
| N6 | KONTROLA linii E4/8 \|.\| <= 1 kazdej doby; kontrola "Miasta" \|.\| <= 1 | linie dzienne | z definicji |
| N7 | milicja: (przyrost w oknach E4/8 - ubytek) = "milicja (E4/0): z modelu w ticku" (169c) \|.\| <= 2/d gdy obie czynne; milicja d120 INFO wobec z30A 67 283 | dwie linie | spojnosc pomiaru |
| N8 | nowe druzyny: a1 + a2 + a3 + a4 = n (kontrola); udzial nie wystawionych a3/n - INFO (oczekiwane < 20% przy T7 E47 <= 5 regionow ponad progiem) | linia | z30A 505 ludzi/d szablonu |
| N9 | zboze z niczego g3 <= 5% g | linia | PLAN 4.9 |
| N10 | wedrowcy: wyjazdy = dojscia + zniszczeni + rozbici (E47) + przyrost "w drodze" (28 dob, \|.\| <= 2%); udzial "do domu" INFO (z30A 93.7%) | linia | z30A |
| N11 | R2: naturalne miast srednio -100..-250/d (d28-d120); migracja >= ubytek naturalny w miastach bez glodu (h1 = 0 w pokoju); udzial miejski swiata d120 w u0 +-2 pkt | "Miasta" | DOK:524 (144/d) |
| N12 | rownanie krain (E47 T3) z MigIn/MigOut: \|reszta\| <= 0.1% ludzi krainy | "Ludzie (ksiega)" | P E4/7 test; przy RESZTA hearth != 0 - INFO |
| N13 | "Hearth (E4/0)" RESZTA bez skoku wobec biegu E4/7 (\|zmiana sredniej\| <= 1 hearth/d) | linia Hearth | Z16 |
| N14 | netto "Ludnosc:" d28-d120 miedzy -720/d (-0.5%/rok, Jeff 11.10) a +1 150/d; skladniki INFO | "Ludzie:" | E47 T11 |
| N15 | koszt: linia E4/8 + okna <= 5 ms/dobe; s/dobe <= +3% wobec biegu E4/7 | "czas" | E47 T12 |
| N16 | zapis z doby 362 (9 dob): 0 bledow; sekcja `tr:` po wczytaniu: "odniesien tr: T" = liczba miast ze stanem | log | 161 |
| N17 | progi etapow 1-3 (zadne krolestwo > 85% bitew lordow ani 3 twierdze; glowy < 5 000 <= 10; 0 bankructw; reszta ksiegi pieniadza < 10 tys. zl/d); "Ludnosc BK (169c)" <= +2%/rok | progi-etap2/3 | P rozdz. 4 |
Wiersze TAK/NIE/INFO (wzor bloku E4/5 K:...tools/sprawdz_logi.py:833-836). A5 (4.2) - wiersz INFO "A5: limit dzienny c / bez ochotnikow d / zold e" (dowod do K8).

---

## 14. Test (wspolny test zbiorczy - Jeff 11.10 03:05, SP:1150-1152)
Autotest 40 dob + zapis z doby 362 (9 dob) - 0 bledow naszych modow; potem test 120 dob (nowa kampania, ustawienia jak u Jeffa) z progami N1-N17 i progami E4/7 (E47 rozdz. 9).
Koniec etapu (P rozdz. 4 :551-553, U20): wojsko lordow 95-115 tys. d300-364 i zalogi d364 >= 85% biegu A E4/0 - bieg 364 dob po wszystkich paczkach.
Reczna proba Jeffa: menu miasta "People of this land" - linia "Townsfolk".

---

## 15. Kontrola calosci (zasada 0) - wpisac do "Ryzyko / co sprawdzic" w CHANGELOG

### 15.1 Wspolne cele latek (nasze i cudze - plik:linia)
| Cel | Kto dzis | E4/8 | Kolejnosc / co pilnowac |
|---|---|---|---|
| `TickGarrisonChangeForTown` | Measure169c prefiks First + finalizer (K:Measure169c.cs:95, :271-273), ClanBudget bool Strazy (K:ClanBudget.cs:917, :1074) | prefiks bool Low + finalizer | finalizer 169c mierzy 0 poza Straza (N2); Straz przepuszczona |
| `TickAutoRecruitmentGarrisonChange` | Measure169c :94, ClanBudget :916, RecruitSources :171, RecruitCost :383, E4/7 3.4 | prefiks void + finalizer (flaga) | bez zmiany gry |
| `GetAutoRecruitmentGarrisonChangeExplainedNumber` (private) | brak | postfiks (odczyt) | UI tez ja wola - liczymy tylko z flaga |
| `Village.DailyTick` | BK bool prefiks (BK:...NotablePatches.cs:207-225), Measure40 W1 First + finalizer (K:Measure40.cs:87, :238-287), PopulationLaw podloga First + finalizer (K:PopulationLaw.cs:712) | prefiks First + postfiks | postfiks przed finalizerami - pomiar i podloga widza stan po korekcie; BK false nie blokuje postfiksu |
| `Town.DailyTick` (internal) | Measure40 W2 (K:Measure40.cs:88, :299-328) | prefiks First + postfiks | jw. |
| `IncreaseSettlementHealthAction.ApplyInternal`, `MilitiasCampaignBehavior.OnAfterSiegeCompleted` | brak | pary pre/post | - |
| `DisbandPartyCampaignBehavior.OnPartyDisbanded` | GarrisonArmory (K:GarrisonArmory.cs:408-466), E4/7 4.3 | dopisek w oknie E4/7 | milicja +0.5 z rozwiazanych zamiast "z niczego" |
| `InitializeLordPartyProperties` | LosersFlee First/Last (K:LosersFlee.cs:1206-1223, :1548-1549), SpoilsCompany Normal (K:SpoilsCompany.cs:185-230, :541) | prefiks First + postfiks Low | po Spoils (0 dla Spoils), przed licznikiem LosersFlee |
| `HeroSpawnCampaignBehavior.SpawnLordParty` | ClanBudget Spawn pre/post (K:ClanBudget.cs:914, :979-1013) | - (nie latamy) | bramka przed nami, limity po nas |
| `GiveInitialItemsToParty` (private) | brak | pre/fin pomiar | - |
| `CreateCaravanParty` | CaravanCapital (K:CaravanCapital.cs:258, Seal31.Patch) | postfiks Low | zloto - oni, ludzie - my |
| `RefillConvoyTroops` (private) | brak | pre/post | - |
| BK `CreateTravellerParty` | TravellerTrade36b pre/post Low/fin (K:TravellerTrade36b.cs:153-155), CastleSteel (:139-147), TravellerGoods36 (K:TravellerGoods36.cs:44-46) | pre + fin (ramka) | towar - oni |
| BK `CreateFoodCaravan`, `CreateSlaveCaravan` | brak | pre + fin | - |
| BK `AddPopulationPartyBehavior` | TravellerTrade36b `ArrivePre` First+100 (K:TravellerTrade36b.cs:160), CirculationWindows O21 (K:CirculationWindows.cs:1793) | - (nie latamy - koniec przez `MobilePartyDestroyed`) | - |
| `MobilePartyDestroyed` | 14 nasluchow (K:ArmouryBehavior.cs:620-703), PeopleBook :677 | galaz w PeopleBook | slownik `Sum`, nie roster (10.4) |
| `MobilePartyCreated` | PeopleLedger (K:ArmouryBehavior.cs:702) | nowy nasluch (ramka) | tylko przy otwartej ramce BK |

### 15.2 Regresje (co juz dzialalo)
1. **Martwy kod ozywa:** H3 "z szablonu" (K:LosersFlee.cs:424-435: karawana, zaloga, milicja, patrol, straz karawan) - od E4/8 ci ludzie maja udzial, wiec rozbici wracaja z hearth
   (E47 `HomeKnown`). Wiecej powrotow +hearth - symetryczne do zdjec E4/8 (`LevyLive` po obu stronach - Z7). Licznik w "Pochodzenie" (h1).
2. Ozywa tez "rozwiazana bez bitwy -> do domu" (10.4) - dotad E47 4.12 "w nicosc"; oczekiwane male (rozbitych oproznia Capture, rozwiazanych - E47 4.3).
3. Zalogi bez przyrostu z modelu: [S] d120 ok. 67.6 tys. wobec 80.3 tys. (84%) - granica progu N4; plus nowe druzyny z zalog przy hamulcu. Droga naprawy: K8 (A5 szybciej, za zloto,
   z ludzi), nie mniej wojska (Jeff 09.10 C).
4. Zalogi w pokoju o polowe (166, ClanBudget `Releases` -> `ReleaseFrom` -> `ToVillage`) - po pokoju zaloga odrasta tylko z A5 (1/dobe) -> slabsze zalogi na poczatku wojny, latwiejsze
   oblezenia (P :499). Pomiar A5 (4.2) pokaze, czy wiaze limit dzienny.
5. Milicja w regionach ponad progiem stoi; BK "Over supported limit" zwraca ludzi do domu (wczesniej znikali) - DOM rosnie o ten ubytek (z30A dni do -2.9 tys.).
6. LosersFlee "dziura werbunku" (K:LosersFlee.cs:1213-1223, :1257) liczy dalej ludzi szablonu - od E4/8 oplaconych; tekst dopisany (rozdz. 2), liczba bez zmian (parser).
7. Renty nalezne miast (K:PopulationLaw.cs:1148- `Daily`, ludzie x 40/364) ida za stanem miasta - R2 obniza je o ubytek naturalny i podnosi migracja; zaplacone zaleza od kasy (zawor).
   `WorkshopLaw.TownHands` = max(dawny wzor, z ludzi) (K:WorkshopLaw.cs:779-781) - rece nie spadna.
8. Dymek zalogi gry pokazuje "+1" (model) przy zerowym przyroscie - kosmetyka, bez latki.

### 15.3 Kolizje z innymi zmianami (nic drugi raz, nic nie nadpisane)
- **Pobor i BK:** E4/8 nie wola `DeduceManpower` - klasy BK nie sa ludzmi (R0); A5 z BK tylko w E4/7 (`GarrisonLevyFromBk`). Wedrowcy: BK dalej dopisuje klasy u celu (:688-690) - inna ksiega.
- **E4/7 4.3** (rozwiazanie) i 10.4 (zniszczenie): jedno zdjecie udzialow - 10.4 bierze `slownik.Sum`, wiec po 4.3 zostaje 0.
- **Pula wyrzutkow / bandy (OutlawLaw):** bez zmian (E4/7 4.7).
- **ClanBudget 166/179, GentryService:** bramki partii bez zmian; nowa druzyna rycerza BK (BK:...BKGentryBehavior.cs:372) przez te sama latke - oplacona z ludzi siedziby.
- **SpoilsCompany:** zdejmuje szablon Spoils pierwszy - E4/8 widzi 0 (bez podwojnego zdjecia).
- **E4/2 Inni:** nowe bandy Innych pominiete (OthersRise `BirthPostfix` tnie do puli CIALA); milicja osad Innych - przyrost odmowiony (wight z niczego nie powstaje); smierc milicji przejetej osady - E4/2 (d) + E47 4.10.
- **E4/5 Devastation:** uchodzcy poza domem nie sa zrodlem migracji ani milicji (TakeLevy E4/7 i `TakeFromRegionVillages` licza hearth x k_v - uchodzcy juz zdjeci z hearth); wsie Looted nie daja migrantow.
- **E4/6 (rownolegle):** glod regionu wstrzymuje migracje; zgony z glodu mieszczan przez te sama `TownShift` (jedna droga); H w stopie miasta przez `HungerTerm` (jedno miejsce).
- **36b TravellerTrade36b / 3.6 / CastleSteel:** towar i zywnosc drogi - bez zmian; E4/8 - tylko ludzie.
- **CaravanCapital (E3/7), MoneyLedger, LevyGold, MercContract (185), Measure169c, PeopleLedger:** zadnego przeplywu zlota w E4/8 (zboze z `FoodStocks` - spizarnia pana, nie towar targu, nie zloto).
  K8 (warunkowy) - wiecej platnego A5 = wiecej wydatkow rodow (istniejacy przeplyw).
- **GarrisonArmory (171):** przeniesienie zaloga -> nowa druzyna przez `MoveKits` (sprzet z ludzmi); bez tego zbrojownia zalogi zostalaby z kompletami ludzi, ktorzy odeszli.
- **Measure40 / Measure169c / PeopleLedger:** czytaja stan po korekcie (postfiks przed finalizerami); E4/8 nie liczy niczego, co juz licza (U8) - linia E4/8 to tylko jej latki.

### 15.4 Zamknieta ksiega ludzi (kazdy czlowiek ma zrodlo i ujscie)
| Przeplyw | Dzis (z30A) | Po E4/8 |
|---|---|---|
| zalogi z modelu | +105/d z niczego | 0 (Straz "" do E4/9) |
| milicja z modelu (+) | +92/d netto z niczego | DOM -> SLUZBA (TakeLevy) albo odmowione |
| milicja z modelu (-) | ludzie znikaja | SLUZBA -> DOM tej osady |
| milicja +20 / +90-108 / +0.5 | z niczego | DOM -> SLUZBA / z rozwiazanych |
| szablon nowej druzyny | +505/d z niczego | DOM -> SLUZBA, SLUZBA zalogi -> SLUZBA druzyny, albo nie ma |
| zboze nowej druzyny | 15-30 z niczego | zapas warowni -> partia (wyjatek: brak zapasu - licznik) |
| karawany | +170/d z niczego | DOM -> SLUZBA |
| konwoje morskie | z niczego | DOM -> SLUZBA |
| wedrowcy BK | +546/d z niczego i w nicosc | DOM -> W DRODZE -> DOM (migracja R19 miedzy regionami) |
| eskorta konwojow BK | z niczego, w nicosc | DOM -> W DRODZE -> DOM |
| miasta | stan bez ruchu (E4/4) | narodziny/zgony (Born_r), migracja wies -> miasto w regionie |
| rozwiazane bez bitwy | w nicosc (E47 4.12) | SLUZBA -> DOM pochodzenia |
Zostaje "z niczego" (nazwane): bunt, gracz bez oporu, incydenty, eskorta BK gracza (P2), Straz (E4/9), niewolnicy BK w konwojach (E4/9), zboze przy braku zapasu.

### 15.5 Ta sama kolejnosc w dobie
Ticki osad gry (DailyTickSettlement - zalogi, milicja; kolejnosc wobec naszego ticku obojetna, okna liczone w chwili zdarzenia) -> nasze `OnDailyTick`: ... `PopulationLaw.Daily` (K:ArmouryBehavior.cs:1453 -
`TownsDaily`, **R2**, renty) -> OutlawLaw (:1509) -> ClanBudget (:1514) -> 169c (:1518) -> OthersRise/PeopleBook.Daily (:1519, E4/7 Reconcile, cache R5) -> **linia E4/8 i "Miasta"** -> Measure40 (:1553,
Hearth czyta `TakeHearth40` z przyczynami E4/8) -> PeopleLedger (:1554, "Ludzie:" czyta `PeopleOf` miast). Hamulec milicji i druzyn czyta cache R5 z poprzedniej doby (jak Levy E4/7).

### 15.6 Watki i wyjatki
Wszystkie cele - watek glowny kampanii (ticki osad, tworzenie partii, zdarzenia). `TownShift` pod `Gate` (K:PeopleBook.cs:86). Kazde wywolanie ksiegi we wlasnym try, licznik potkniec per miejsce;
zadne nie stoi miedzy zdjeciem a dopisaniem ludzi w kodzie gry (U24) - korekty rosteru robione PO oryginale (postfiks), na kopii skladu. Lista do recenzji: `MilitiaDelta`, `SpawnPost`, `CaravanPost`,
`RefillPost`, finalizery BK, galaz `OnPartyDestroyed`, `TownGrowth.Daily`, `TakeFromRegionVillages`, `ArriveAt`.

---

## 16. NK (co dziala dopiero w nowej kampanii)
**Nic nie wymaga nowej kampanii** (P tabela :209 "NK: nie"). Stary zapis: zalogi od pierwszej doby; milicja, druzyny, karawany, wedrowcy - od siewu E4/7 (pierwsza doba po wczytaniu, gdy E4/7 ma
`sw:`; przed siewem wszystko jak dzis); wedrowcy w drodze w chwili wgrania - "" (u celu `Note`, bez DOM - nic z niczego); odniesienia miast `tr:` z pierwszej doby (bez skoku). Nowa kampania - to samo,
odniesienia miast z dnia kalibracji (udzial miejski z tabeli krain).

---

## 17. Pytania do Jeffa (tylko kierunek) i odlozone

**ROZSTRZYGNIETE (Jeff 11.10 ok. 09:30, "tak, tak, tak"): P1, P2, P3 - wszystkie TAK wedlug rady ponizej (docs/STAN-PRAC.md DECYZJE JEFFA 11.10).**

**P1. Straz karawan bez hamulca wolnych rak.** Karawana bierze ludzi z miasta, z ktorego wyrusza (miasto ma ich mniej), ale nie czeka na "wolne rece" krainy - jak najemnik z karczmy, a nie jak rekrut
z poboru. Inaczej w wyczerpanej krainie karawany ruszalyby prawie bez strazy. Zgoda? Rada: tak (bez odpowiedzi - tak).
**P2. Miasta dopelniane ze wsi w ok. rok.** Miasto ma wiecej zgonow niz urodzen (ok. -0.5% rocznie); wsie dopelniaja ten ubytek i luke po poborze miejskim, karawanach i wedrowcach, tak zeby miasto
trzymalo swoj udzial w ludziach krainy (rosnie i maleje razem ze swoimi wsiami). Alternatywa - tylko ubytek naturalny (wtedy kazdy rekrut z miasta to trwala strata miasta). Rada: dopelniac (bez odpowiedzi - tak).
**P3. Nowa druzyna lorda: najpierw wolne rece krainy, potem zaloga siedziby, bez obu - mniejsza druzyna.** (Twoje 11.10 "kraina bez wolnych rak nie wystawia nowego wojska" - tu zaloga zamku jest
"rezerwa" lorda.) Rada: tak.

**Informacja (bez pytania):** jesli zalogi spadna ponizej 85% - zaloga werbuje za zloto szybciej (2 ludzi dziennie zamiast 1, w granicy zoldu) - wedlug Twojego 09.10 C "mniej wojska - NIE"; konie
i zywnosc, ktore gra daje nowej druzynie z niczego, zostaja do etapu 6 (towary) - w logu jest ich liczba; Nocna Straz do E4/9 ma dawny przyrost zalog i milicji (inaczej slablaby przed swoim
nowym doplywem z Westeros).

**Odlozone (P2, nazwane w Reconcile E47):** bunt (G RebellionsCampaignBehavior.cs:205-245 - milicja 100 z niczego, zaloga czyszczona), rabunek gracza bez oporu (milicja = 0), incydenty gracza,
eskorta milicji BK gracza (BK MilitiaComponent.cs:47), dary startowe druzyn (etap 6), wioski poboczne (E4/11).

---

## 18. Kolejnosc kodowania (galaz `w-toku/e4-8` od czubka E4/7 po jego recenzji; kazdy krok: build Release kod 0, osobny commit; jeden test na koncu)
0. **K0 sprawdzenie Z1-Z18** na kodzie E4/7 (lista w opisie commita K1); refaktory bez zmiany rachunku: `DangerOf(node)`, `HungerTerm(v)` (PopulationLaw).
1. **K1 zalogi:** 4.1 (prefiks bool), 4.2 (pomiar A5), klucz `GarrisonGrowthOnlyRecruits`, szkielet `ArmsFromPeople` (ApplyAll, linia, Reset, startowa).
2. **K2 milicja:** `PeopleBook.Arms.cs` (przyczyny, `ARoad`, `ArriveAt`), okna 5.2-5.3, dopisek 5.6 w oknie E4/7, `MilitiaFromFreeHands`.
3. **K3 nowe druzyny:** 6.2-6.4, `NewPartyFromPeople`, `NewPartyFromSeatGarrison`, `NewPartyGrainFromSeat`, dopisek LosersFlee.
4. **K4 karawany:** 7.1-7.2, galaz 10.4 (rozwiazane bez bitwy), `CaravansFromPeople`.
5. **K5 wedrowcy BK:** 8.2-8.3, Z8/Z9, `MigIn/MigOut` (Z10), rownanie 10.5, `TravellersFromPeople`.
6. **K6 miasta:** `TownRate`, `TownGrowth.cs`, `tr:`, `TownShift` (gdy brak D1), linia "Miasta", menu 9.6, `TownGrowthEnabled`, `TownGrowthPenaltyPercent`, `TownMigrationRefillDays`.
7. **K7:** Measure40 Hearth (12.4), tekst "miasta:" w GrowthDaily, `Settings.cs` (komentarze po polsku bez znakow, podpowiedzi EN) + `python tools/gen_mcm.py` (RANGES), parser blok E4/8 (rozdz. 13),
   CHANGELOG (Problem z liczbami z30A; Przyczyna; Zmiana; **Odstepstwa**: R19 cala partia z eskorta jako migranci, eskorta konwojow wraca do domu, nowa druzyna - zaloga siedziby jako drugie zrodlo,
   R2 dopelnienie do udzialu miejskiego (ponad DOK - P2), Straz i osady Innych - wyjatki, zboze z niczego przy braku zapasu, `GiveInitialItemsToParty` poza paczka; Ryzyko = rozdz. 15;
   Status DO SPRAWDZENIA), dwie recenzje diffu (ksiega ludzi; kod i kolizje), potem test zbiorczy 120 dob + zapis.
8. **K8 (warunkowy - tylko gdy N4 nie przejdzie):** postfiks na `GetMaximumDailyAutoRecruitmentCount(Town)` typu czynnego modelu (`Campaign.Current.Models.SettlementGarrisonModel.GetType()`,
   metoda zadeklarowana w tym typie - dzis ROT D:...ROTSettlementGarrisonModel.cs:51-54, ktory wola poprzedni): gdy zaloga ponizej limitu zoldu zalogi (cel 166) - `__result = max(__result, GarrisonAutoRecruitMaxDaily)` (2);
   klucz `GarrisonAutoRecruitMaxDaily` (1 = gra). Brama zostaje: zold (G GarrisonRecruitmentCampaignBehavior.cs:172-190), miejsce, ochotnicy (A5 dalej przez E4/7 3.4 z ludzi). Osobny wpis CHANGELOG.
