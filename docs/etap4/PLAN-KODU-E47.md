# PLAN KODU - paczka E4/7 (164b-1): pochodzenie ludzi, pobor z wsi, polegly trwale, wolne rece, okno zniw, ksiega w menu osady

Status: SAM PLAN (11.10.2026). Kod, gra, ustawienia, zapisy nietkniete; nic nie budowane.
Wiazace: `docs\etap4\PROJEKT-ETAP4-KSIEGA-LUDZI-ZIMA-2026-10-10.md` v2 (dalej P; E4/7 = P rozdz. 3, R0/R3-R6/R9/R12/R20 w rozdz. 2, 5.1),
`docs\etap4\KRYTYKA-E4.md` (KR: U4, U7, U16, U17, U19, U21), decyzje Jeffa 11.10 (`docs\STAN-PRAC.md:1137-1138`: kraina bez wolnych rak
nie wystawia nowego wojska do odrobienia strat - TAK; ludnosc w ciaglej wojnie moze spadac do -0.5%/rok - TAK; spalona wies wraca rok od
ostatniego najazdu - TAK, to E4/5).

Skroty miejsc:
- **z30:** = `scratchpad\e4\z30\Armoury\src\` (zbior 30 = gra + D11 + E4/0..E4/3 + 36b; galaz `w-toku/zbior30`, c02c80f) - linie z tego drzewa.
- **BK:** = `scratchpad\e4tools\bkfull\` (BannerKings.Redux), **G:** = `scratchpad\dec32\cs_full\` (TaleWorlds.CampaignSystem 1.4.8).
- **DOK** = `docs\DEMOGRAFIA-SILA-ROBOCZA-2026-10-05.md`, **RK** = `docs\REGULY-KRAIN-I-DLUGU-2026-10-06.md`, **SP** = `docs\STAN-PRAC.md`.
- Baza liczb: bieg testowy zbioru 30 (`scratchpad\kopia-z30-A\Armoury-2026-10-11_04-36-46.log`, 120 dob, `progi-etap4.txt`) = **z30A**.

---

## 0. Streszczenie decyzji planu (co wykonawca robi, czego nie)

1. **Wlasciciel stanu = `PeopleBook` (U23).** Nowy plik `PeopleBook.Origin.cs` jako `partial` tej samej klasy (`internal static partial class
   PeopleBook` - zmiana deklaracji w z30:PeopleBook.cs:30; wzor podzialu klasy w repo: `ClanIncomeBook.StableD.cs`, `MoneyLedger.Obieg.cs`).
   Osobne pliki: `FreeHands.cs` (R5/R9b/hamulec/okno zniw) i `PeopleMenu.cs` (pozycja menu). Stan tylko w `arm_people`.
2. **Pochodzenie = udzialy, nie ludzie z imienia.** Kazdy roster szeregowych (sklad partii i loch partii/osady) ma slownik
   `region -> liczba` (double). Kazde wyjscie zdejmuje proporcjonalnie (`Take`), kazde wejscie ze znanym zrodlem dopisuje (`Give`). Raz na dobe
   **uzgodnienie** (`Reconcile`) slownika ze stanem rosteru: nadwyzka rosteru -> koszyk **""** ("bez pochodzenia", nazwany wedlug rodzaju partii),
   niedobor -> zdjecie proporcjonalne ("w nicosc", nazwane). To robi z ksiegi pomiar dziur (dzis T7 z30A +1072 ludzi/d bez nazwy), a nie zgadywanie.
3. **Koszyk "" (bez pochodzenia)** = ludzie spoza DOM: szablony gry (nowe partie rodow, zalogi i milicja z modelu, karawany - zamyka E4/8),
   Nocna Straz (rekrut "z calego Westeros" - E4/9), rody bez ziemi przy siewie, ochotnik z mapy, jency z rabunku BK (E4/9). Ich smierc - licznik,
   ich powrot - bez hearth (jak dzis w E4/3). Nie licza sie do R5 zadnego regionu i nie wchodza do rownania regionu.
4. **Punkt zdjecia rekruta = postfiks na BK `MilitaryData.DeduceManpower(PopulationData,int,CharacterObject,Hero)`** (U7; BK:...Managers.Populations\MilitaryData.cs:151-160),
   jedyne dwa wywolania w BK: AI BK:...Patches.Recruitment\RecruitmentApplyInternalPatch.cs:20, gracz BK:...UI.Patches\RecruitmentOnDonePatch.cs:33.
   Partia - z okna na `RecruitmentCampaignBehavior.ApplyInternal` (AI) i na `RecruitmentVM.OnDone` (gracz). Do tego osobno: A5 (autowerbunek zalogi),
   karczma gracza, pobor wymuszony gracza.
5. **Powrot czlowieka ze znanym pochodzeniem dostaje hearth** (konczy odstepstwo 1 z E4/3 - CHANGELOG z30 wpis E4/3 "Odstepstwa" pkt 1): czesc
   znana -> `OutlawLaw.ReturnHome(region pochodzenia, ...)` (ta sama najslabsza wies regionu co dzis - z30:OutlawLaw.cs:342-356) przez NOWE przyczyny
   z `MHearth = true`; czesc "" -> `Note` jak dzis. Ludnosc BK (sklad) dalej dostaje powracajacych tam, gdzie dzis (bez zmiany geografii BK).
6. **Polegly ubywa na stale:** smierc zdejmuje udzial z SLUZBY/JENCOW i dopisuje POLEGLYCH regionu pochodzenia (DOM juz go nie mial od werbunku).
7. **Pula wyrzutkow zostaje geograficzna** (region miejsca, jak dzis - regula Jeffa 11.10 03:30 "bandy rosna z wojna" i tempo wojny E4/3 licza sie
   po regionie puli). Zolnierz ze znanym pochodzeniem w puli niesie udzial (`wo:` = region puli -> region pochodzenia). **Odstepstwo od litery R6**
   ("dezerter -> pula regionu pochodzenia"), zgodne z jej celem: wraca do DOM regionu pochodzenia, rownanie regionu pochodzenia sie zamyka, banda nie
   "teleportuje" zrodla. Do wpisu w CHANGELOG jako odstepstwo z powodem.
8. **Sekcje zapisu: `so:` `jo:` `wo:` `rg:` `sw:`** - NIE `s:`/`j:` z P. Powod: `s:` jest juz zajete przez konto CIALA (z30:PeopleBook.cs:105
   `"v1|s:"`, odczyt :139), a E4/4/E4/5/E4/6 biora `t:`/`u:`/`f:`. Dwuliterowe przedrostki nie koliduja z `StartsWith("s:")`.
9. **P1 (obowiazkowe w E4/7)** - wszystko z rozdz. 3-8. **P2 (jesli czas, inaczej nazwane w Reconcile):** ucieczki jencow ponad limit
   (G:...PrisonerReleaseCampaignBehavior.cs:261-309), zwolnienie jencow przy pokoju (:95-195), jency rozwiazanej partii we wsi (G:...DisbandPartyCampaignBehavior.cs:362-370),
   lup ludzi gracza na ekranie Aftermath (oczekujace udzialy - rozdz. 4.13).
10. **Poza E4/7 (nie dopisywac "przy okazji" - CLAUDE.md 8 pkt 3):** zalogi/milicje/nowe druzyny/karawany z ludzi (E4/8), Straz z Westeros i niewolni
    jako konto (E4/9), prefiks `ExecuteCapture` (E4/9), weterani (E4/10), migracja do miast (E4/8), rece warsztatow miast (WorkshopLaw - nie w R9).

---

## 1. Co zalezy od E4/4 (i co dochodzi z E4/5/E4/6) - punkty styku

| # | Od kogo | Czego E4/7 potrzebuje | Gdzie | Gdy brak |
|---|---|---|---|---|
| D1 | E4/4 | **stan ludzi miasta** ("t:" w PeopleBook) i droga zdjecia mieszczanina: rekrut w miescie (notabl miejski), najemnik z karczmy AI i gracza = -1 stanu | funkcja E4/4 (np. `PeopleBook.MoveTown(Settlement, from, to, men, cause)`); jesli E4/4 jej nie ma - E4/7 ja dopisuje w PeopleBook obok `Move` (z30:PeopleBook.cs:238) | rekrut miejski i karczma: tylko `Note` (licznik "miasta bez stanu - do E4/4"), pochodzenie "" |
| D2 | E4/4 | **narodziny wedlug regionu od siewu** (R5: K_m = ... - 0.27 x narodziny) | E4/4 liczy przyrost wsi (GrowthPostfix / okno W1); E4/7 dodaje akumulator `PeopleBook.NoteBorn(Settlement village, double men)` i jedno wywolanie tam, gdzie E4/4 liczy "przyrost naturalny +N ludzi" | K_m bez narodzin (ostrozniej: a rosnie szybciej) + dopisek w linii "narodziny: brak danych E4/4" |
| D3 | E4/4 | **RESZTA hearth ~0** (dzis +677/d - z30A T2 NIE) | linia "Hearth (E4/0)" | prog rownania krain (T2, rozdz. 9) = INFO |
| D4 | E4/4 | `PeopleOf` miasta = stan (do DOM regionu i L_ref) | z30:PopulationLaw.cs:196 | DOM miasta = dobrobyt x k (L_ref rosnie z dobrobytem - INFO w linii) |
| D5 | E4/4 | zmiany w tych samych metodach: `PeopleBook.Export/Import/Daily` (z30:PeopleBook.cs:102-175, :414-460), `Measure40` linia Hearth (z30:Measure40.cs:836-840), `Settings.cs`/`McmSettings.cs` | - | scalac recznie; MCM zawsze od nowa `python tools/gen_mcm.py` |
| D6 | E4/5 | `Devastation.YieldFactor(v)` w `MaterialLaw.ProdPostfix` i `Devastation.FoodPostfix` - R9(a) | stara 113: `lancuch` origin/paczki-na-120/113 MaterialLaw.cs (+9 linii po "mines and woods") | E4/7 dopisuje sam czynnik L (rozdz. 6.4); przy E4/5 - jedna linia `hands = Devastation.YieldFactor(v) * FreeHands.YieldFactor(v)` |
| D7 | E4/5 | zabici cywile przy spustoszeniu wedlug regionu (K_m: 0.27 x zmarli cywile) i konto UCHODZCY (menu, rownanie) | wywolanie `PeopleBook.NoteCivilDead(region, n, cause)` w Devastation (113 ma `RegionOf(node, out away, out dead)`) | K_m bez cywilow; menu bez "Refugees"; rownanie bez UCHODZCOW |
| D8 | E4/6 | zmarli z glodu wedlug regionu (R10) | to samo `NoteCivilDead` | jw. |
| D9 | E4/2 (jest) | trzy miejsca smierci z rak Innych | z30:OthersRise.cs:225, :386, :517 | - (rozdz. 4.10) |
| D10 | E4/3 (jest) | `PeopleBook.Move`, `MHearth`, trzy punkty powrotu, `OutlawLaw.ReturnHome`, `PeopleUnit.On` | z30:PeopleBook.cs:62-73, :238-261; z30:OutlawLaw.cs:342-356 | - |

Wniosek: E4/7 da sie napisac zaraz po E4/4 bez E4/5/E4/6 - punkty D6-D8 to po jednej linii w ich kodzie (albo w ich paczce, jesli powstana pozniej).
Kolejnosc wgrywania z P (E4/5 -> E4/6 -> E4/7) zostaje: E4/7 przed E4/5 w grze dalby R9 bez czesci (a) i K_m bez cywilow (patrz "Gdy brak").

---

## 2. Stan w PeopleBook (zapis `arm_people`, `SaveText.Sync` - z30:ArmouryBehavior.cs:440)

### 2.1 Pamiec (nowe pola w `PeopleBook.Origin.cs`)
```
internal sealed class Shares { Dictionary<string,double> M; double Total; double Known (bez ""); Shares Slice(double n); ... }   // udzialy w ruchu
private sealed class Org { Dictionary<string,double> M; double Sum; }                    // slownik jednego rosteru
private static readonly Dictionary<PartyBase, Org> _mem  = ...;   // sklad partii (tylko szeregowi, bez wightow) - konto SLUZBA
private static readonly Dictionary<PartyBase, Org> _pris = ...;   // loch partii i osady - konto JENCY
private static readonly Dictionary<string, Dictionary<string,double>> _poolOrg = ...;   // region puli -> region pochodzenia -> zolnierze ze znanym pochodzeniem
private sealed class RegRef { double DomRef, SRef, JRef, LRef; double Fallen, FallenMen, Civil, Born, Enslaved, ToWall; }   // na region (warownia OutlawLaw)
private static readonly Dictionary<string, RegRef> _rg = ...;
private static bool _sown; private static int _sowDay = -1;
private static string _pendingOrigin;                                  // surowy napis z zapisu do rozwiazania w OnSessionLaunched (partii w SyncData jeszcze nie ma)
private static readonly Dictionary<MobileParty, Shares> _pending = ...; // udzialy czekajace na roster (narodziny bandy, szablon lorda) - pierwszy dodatni Reconcile je zjada
// liczniki doby (bez zapisu) - tablice wedlug przyczyny i rodzaju partii; cache R5 (FreeHands) - podmiana referencji raz na dobe
```
Klucz regionu = `OutlawLaw.RegionFor(osada).StringId` (z30:OutlawLaw.cs:241 - ta sama geografia co pula wyrzutkow i ksiega); "" = bez pochodzenia.
Rodzaje partii poza ksiega pochodzenia: tabory wsi i lodzie (konto TABOR z E4/3), konwoje BK `PopulationPartyComponent` (E4/8), partie Innych
(`Undead.Party`, z30:Undead.cs:50) - ich SKLAD nie ma slownika; loch partii Innych MA (jency z ludzi).

### 2.2 Format zapisu (dopisane w `Export`, czytane w `Import`; stary zapis bez sekcji = siew przy pierwszej dobie)
- `|sw:<doba siewu>;1` - siew zrobiony (wersja formatu 1).
- `|so:<idPartii>=<reg>:<n>;<reg>:<n>,<idPartii>=...` - SLUZBA; region "" zapisywany jako `~`; n "0.##".
- `|jo:<idPartii>=...,@<idOsady>=...` - JENCY (`@` = loch osady).
- `|wo:<regionPuli>=<regPoch>:<n>;...,...` - zolnierze ze znanym pochodzeniem w puli.
- `|rg:<region>=<DomRef>;<SRef>;<JRef>;<LRef>;<Fallen>;<FallenMen>;<Civil>;<Born>;<Enslaved>;<ToWall>,...`
- Id ze znakami `| , ; = :` - pomijane (licznik "nie zapisane", oczekiwane 0; BK patrol ma id `"bkGarrisonParty_" + Name` -
  BK:...Components\GarrisonPartyComponent.cs:27-29 - nazwa osady moze miec apostrof, ale nie te znaki).
- Rozmiar [S]: ok. 2.5 tys. partii x ok. 60 znakow = ok. 150 KB -> `SaveText` dzieli na kawalki po 8000 znakow (z30:SaveText.cs:27-31). Linia po wczytaniu:
  `Pochodzenie (E4/7): z zapisu N partii, M lochow, regionow R; nie znaleziono partii K (porzucone).`
- `Import` (z30:PeopleBook.cs:130) tylko odklada napis sekcji `so/jo` do `_pendingOrigin`; `rg/wo/sw` czyta od razu. **Rozwiazanie w
  `OnSessionLaunched`**: nowe `PeopleBook.ResolveOrigin("wczytanie")` obok `RecruitKit.ResolvePending` (z30:ArmouryBehavior.cs:1148) - slownik
  `MobileParty.All` po `StringId` budowany raz.
- `Reset` (z30:PeopleBook.cs:92-99, wolany z konstruktora z30:ArmouryBehavior.cs:395): czysci wszystko z 2.1 (stan jednej kampanii nie
  przecieka do drugiej - zasada 0).

### 2.3 Siew (`EnsureSown`, raz na kampanie; NK: "siew przy wlaczeniu")
Warunek: `PeopleOriginEnabled`, `Campaign.Current` gotowa, `PopulationLaw.Calibrated` (z30:PopulationLaw.cs:185) i k na osade zalozone
(`PeopleBook.TryUnit`). Wolane w `PeopleBook.Daily` (z30:ArmouryBehavior.cs:1517 przez OthersRise.Daily) - pierwsza doba sesji bez `sw:`.
Przed siewem wszystkie latki E4/7 sa no-op (BK i gra jak dzis).
- partia rodu (takze gracza): rozklad na regiony warowni i wsi rodu **wedlug ludzi regionu** (DOK krok 5 poprawka krytyka: "naplyw nieznany
  rozkladac na wszystkie warownie rodu wg ludzi"; P mowi "region siedziby" - to szczegolny przypadek jednego lenna), brak lenn -> "" (rody
  najemne, bez ziemi - "bez ojczyzny" z P);
- zaloga, milicja: region wlasnej osady; karawana: region `HomeSettlement`; banda: `OutlawLaw.RegionAt(pozycja)` (z30:OutlawLaw.cs:242);
  patrol BK (`GarrisonPartyComponent`): region osady macierzystej; patrol gry (`PatrolParty`, odnawiany z szablonu - G:...PatrolPartiesCampaignBehavior.cs:110-117) -> "";
- **Nocna Straz** (`CrownGifts.IsWatchParty`, z30:CrownGifts.cs:72-74, i jej zalogi) -> "" (P R5 "Straz bez progu - rekrut z calego Westeros";
  DOK krytyk krok 5 pkt 4: rekruci Strazy po 0.27 hearth wykrwawiliby Dar -4.4..-5.9% rocznie);
- jency w chwili siewu -> "" (nowa kampania: 22 jencow; stary zapis ok. 2-3 tys.).
- `rg:` dla kazdego regionu: DomRef = ludzie wsi regionu (hearth x k_v) + ludzie miasta (D4); SRef/JRef = suma udzialow regionu; **LRef = DomRef + SRef + JRef**
  (P R5: "L_ref = ludzie regionu w dniu siewu (dom + sluzba + jency)", poprawka krytyka DOK krok 6).
- Linia: `Pochodzenie (E4/7): siew dnia D - partii P (ludzi S z pochodzeniem w R regionach, bez pochodzenia B: Straz b1, rody bez ziemi b2, patrole gry b3), lochow L (jency J bez pochodzenia), L_ref swiata X mln.`

### 2.4 API (jedna droga - kazde cialo we wlasnym try, licznik potkniec `Stumble`, nigdy nie rzuca - U24)
| Funkcja | Co robi |
|---|---|
| `Shares Take(TroopRoster r, double n, int cause)` | zdejmuje proporcjonalnie n z slownika rosteru (`r.OwnerParty`; sklad albo loch po `ReferenceEquals(r, owner.MemberRoster)`); brak slownika / przed siewem -> Shares calosc "" |
| `void Give(TroopRoster r, Shares s, int cause)` | dopisuje udzialy (przenosiny) |
| `void GiveNew(TroopRoster r, Settlement region, double n, int cause)` | rekrut ze znanego regionu |
| `void GiveNone(TroopRoster r, double n, int cause)` | koszyk "" z nazwana przyczyna (szablon lorda, ochotnik z mapy, Straz) |
| `void Pending(MobileParty mp, Shares s)` | udzialy dla rosteru, ktorego jeszcze nie ma (narodziny bandy - z30:OutlawLaw.cs:1127-1149) |
| `void DieShares(Shares s, int cause)` | POLEGLI i FallenMen regionu pochodzenia (R3); "" - licznik |
| `double HomeKnown(Shares s, double n, int cause)` | czesc znana n -> `OutlawLaw.ReturnHome(region, ..., PeopleBook.AService, cause)` (DOM +n/k); zwraca ilu umiescil; gdy `!LevyLive` -> 0 |
| `void HomeOrNote(Shares s, double n, int causeKnown, int causeLegacy)` | znana -> HomeKnown, reszta -> `Note(reszta, causeLegacy, 0)` (jak dzis) |
| `void PoolShares(Settlement poolRegion, Shares s)` / `Shares PoolTake(Settlement poolRegion, double soldiers)` | udzial zolnierzy w puli |
| `void EnslaveShares(Shares s)`, `void WallShares(Shares s)` | jency -> niewola (licznik regionu do E4/9), na Mur (migracja do Strazy, licznik) |
| `double TakeLevy(Settlement s, double n, int cause, out Settlement region)` | DOM -n/k: wies - `Move(v, ADom, AService, n, cause, PeopleUnit.GamePerMan)` z podloga 50 hearth (jak z30:OutlawLaw.cs:328); miasto - D1; zamek - wsie zamku wedlug `PeopleUnit.Base`; brak - inne wsie regionu; nic - licznik "pobor na podlodze" |
| `double UnderArms(Settlement region)` | S z cache doby (U19 - jedna definicja: linia, hamulec, plon, menu, PeopleLedger) |
| `void NoteCivilDead(Settlement region, double n, int cause)`, `void NoteBorn(Settlement village, double men)` | punkty styku D2, D7, D8 |
| `void ReconcileDaily()` | rozdz. 4.14 |
`LevyLive` = `_sown && PeopleOriginEnabled && LevyFromVillages && PeopleUnit.On` - **ten sam warunek dla zdjecia i dla powrotu z hearth** (symetria; przy
`PeopleUnitEnabled` off `Move` liczylby 0.5 hearth/czl. - z30 CHANGELOG E4/3 poprawka R1-1). Przy `!LevyLive` rekrut idzie do "" (bez zdjecia), a powrot
- `Note` jak w E4/3: przelaczenie w trakcie kampanii nie robi ludzi z niczego.

### 2.5 Nowe przyczyny ruchu DOM (rozszerzenie z30:PeopleBook.cs:64-73; `MKinds` 15 -> 24)
`MLevyVillage=15` (pobor z wsi), `MLevyA5=16` (autowerbunek zalogi), `MBackH3=17` (zolnierze z pochodzeniem z LosersFlee.SendHome), `MBack166=18`,
`MBackW4c=19`, `MBack179=20` (ClanBudget.ToVillage), `MBackPool=21` (z puli, zolnierze), `MBackFreed=22` (jency uwolnieni/wypuszczeni/sprzedani w Westeros),
`MBackDisband=23` (rozwiazane partie). Wszystkie `MHearth = true`. Stare `MSoldier/M166/MW4c/M179` (false) zostaja dla czesci "" - `UnitSegment`
(z30:PeopleBook.cs:466-513) dostaje je w "trzech punktach powrotu" (suma bez podwojenia jak po recenzji E4/3). Pobor miejski (D1) - przyczyna E4/4.

---

## 3. Punkt zdjecia ochotnika (R4, U7) - latki i kolejnosc

### 3.1 Okno partii AI - `RecruitmentCampaignBehavior.ApplyInternal`
- Cel: G:...CampaignBehaviors\RecruitmentCampaignBehavior.cs:607-640 (private; parametry `MobileParty side1Party, Settlement settlement, Hero individual,
  CharacterObject troop, int number, int bitCode, RecruitingDetail detail`; detale :120-126: `MercenaryFromTavern, VolunteerFromIndividual,
  VolunteerFromIndividualToGarrison, VolunteerFromMap`).
- `public static void RecruitWinPre(MobileParty __0, object __6, out RecruitWin __state)` **Priority.First** (stos - zagniezdzenie jak LevyGold :36-48) +
  `public static Exception RecruitWinFin(Exception __exception, RecruitWin __state)` (finalizer zawsze).
- Ten cel lataja juz (wszystkie void, bez zmiany wyniku): LevyGold prefiks/finalizer/postfiks (z30:LevyGold.cs:36-48, :70-162, wpiecie :198-),
  CirculationWindows `RecruitPre/FlagFin` (z30:CirculationWindows.cs:1760), BK postfiks z `DeduceManpower` (BK RecruitmentApplyInternalPatch.cs:15-21).
  Nasz finalizer biegnie po postfiksie BK - okno jest otwarte, gdy BK wola `DeduceManpower`.
- `VolunteerFromMap` (settlement null - BK nie wola DeduceManpower; dzis blokuje CS T2b): w finalizerze `GiveNone(side1Party.MemberRoster, number,
  "ochotnik z mapy")`.

### 3.2 Okno gracza - `RecruitmentVM.OnDone`
- Cel: typ `TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment.RecruitmentVM` (po nazwie, `AccessTools.TypeByName`), metoda `OnDone`;
  BK ma tam prefiks bool zwracajacy false (BK UI.Patches\RecruitmentOnDonePatch.cs:19-44). Nasz prefiks void **Priority.First** (przed BK) ustawia
  okno = `MobileParty.MainParty`, finalizer zamyka.
- Zapas (gdy typu nie ma): w postfiksie `DeduceManpower` bez otwartego okna, a `Settlement.CurrentSettlement == data.Settlement` i
  `MainParty.CurrentSettlement == data.Settlement` -> gracz (licznik "gracz bez okna").

### 3.3 Postfiks BK `MilitaryData.DeduceManpower`
- Typ `BannerKings.Managers.Populations.MilitaryData`, metoda `DeduceManpower` z parametrami (`BannerKings.Managers.Populations.PopulationData`, `int`,
  `CharacterObject`, `Hero`) - druga przeciazona wersja `(PopulationData, int, PopType)` (:162) NIE (nikt jej nie wola z werbunku).
- `public static void DeducePostfix(object __0, int __1, CharacterObject __2, Hero __3)`; osada = wlasciwosc `Settlement` PopulationData
  (BK:...PopulationData.cs:104, `PropertyInfo` w pamieci). Nie zmienia BK (sklad i klasa zostaja - R0: "BK steruje skladem, ksiega iloscia").
- Cialo: `EnsureSown`; partia z okna; Straz (`CrownGifts.IsWatchParty(party)` albo kultura osady nightswatch) -> `GiveNone(..., "Straz - do E4/9")`;
  `LevyLive` -> `TakeLevy(osada, __1, MLevyVillage, out region)` i `GiveNew(party.MemberRoster, region, ile zdjeto)`; reszta (podloga) -> "" z licznikiem;
  `!LevyLive` -> "".
- Rodzaj werbunku z okna (do linii "Pobor z ludzi"): notabl (wies/miasto/zamek), karczma (notabl null, miasto - stan miasta do E4/10; P), karawana
  (karczma), gracz.
- Brak typu BK -> linia startowa "BRAK DeduceManpower" i nic nie zdejmujemy (P ryzyko E4/7).

### 3.4 A5 - autowerbunek zalogi (bez BK)
- Cel: `GarrisonRecruitmentCampaignBehavior.TickAutoRecruitmentGarrisonChange(Town)` (G:...GarrisonRecruitmentCampaignBehavior.cs:76-95: bierze sloty notabli
  miasta i jego wsi BEZ zdarzenia i BEZ BK). Lataja: RecruitSources (z30:RecruitSources.cs:87-132 - migawka tylko przy `GarrisonRecruitKeepsKit` i DTE,
  wiec NIE nadaje sie do ksiegi), Measure169c (z30:Measure169c.cs:94), ClanBudget prefiks bool Strazy (z30:ClanBudget.cs:916), RecruitCost (z30:RecruitCost.cs:383).
- Wlasna migawka (prefiks void: kopia `VolunteerTypes` notabli osady i wsi - wzor RecruitSources :96-105) + postfiks: kazdy slot `before!=null && after==null`
  = 1 rekrut z osady notabla -> `TakeLevy(notabl.CurrentSettlement, 1, MLevyA5)` + `GiveNew(zaloga, region)`; prefiks Strazy zwrocil false -> 0 slotow (ok).
- **Klasa BK (P: "A5 ... zdejmuje tez BK DeduceManpower"):** przy `GarrisonLevyFromBk` wolamy `MilitaryData.DeduceManpower(popData, 1, troop, notabl)` z flaga
  `[ThreadStatic] _selfDeduce` - nasz postfiks 3.3 przy fladze nic nie robi (bez podwojnego zdjecia DOM). Ryzyko: pula BK maleje szybciej (A5 151/d w z30A) - pkt 10.

### 3.5 Karczma gracza i pobor wymuszony
- `RecruitmentCampaignBehavior.BuyMercenaries` i `buy_mercenaries_on_consequence` (G:...RecruitmentCampaignBehavior.cs:705-716, :789-797; lata je juz RecruitCost
  z30:RecruitCost.cs:393): migawka szeregowych MainParty + miasto; postfiks: przyrost -> stan miasta -1 (D1) i `GiveNew(MainParty, region miasta)`.
- Pobor wymuszony gracza: z30:PeopleUnit.cs:286-301 (`PressPostfix`) robi juz `Move(DOM -> SLUZBA, MPress)`; po nim jedna linia
  `GiveNew(MobileParty.MainParty.MemberRoster, region wsi, w.Men, MPress)`.

### 3.6 Liczniki kontroli punktu zdjecia
Linia "Pobor z ludzi (E4/7)" (rozdz. 8.3) ma kontrole: zdjecia BK `DeduceManpower` (wywolania x quantity) = rekruci od notabli + z karczmy z
`PeopleLedger.OnTroopRecruited/OnUnitRecruited` (z30:PeopleLedger.cs:280-311) bez echa ROT; "DeduceManpower poza oknem" (prog 0).

---

## 4. Przenosiny, smierc, powroty - latki (R3, R6, R12, R20)

Zasada kazdego okna: prefiks void zapisuje liczby szeregowych przed (bez bohaterow i wightow), postfiks/finalizer liczy, ile przeszlo, i robi
`Take(zrodlo)` -> `Give(cel)`. Nigdy nie zmienia rosteru gry ani wyniku. Wszystkie cele ponizej sprawdzone w z30 - nikt nie ma na nich latki `bool`
zmieniajacej przeniesienie (wyjatek: prefiksy Strazy w ClanBudget - zwracaja false przed metoda, wtedy przeniesienia nie ma i my liczymy 0).

### 4.1 Partia <-> zaloga (AI)
`GarrisonTroopsCampaignBehavior.LeaveTroopsToGarrison/TakeTroopsFromGarrison(MobileParty, Settlement, int, bool)` (G:...GarrisonTroopsCampaignBehavior.cs:542-581, :583-610).
Lataja: GarrisonArmory (z30:GarrisonArmory.cs:357-406, wpiecie :1067-1077 - tylko przy `KitMoves`), LosersFlee (:1225-1240, :1559-1565), Measure169c (:96-97).
Nasze wlasne prefiksy/postfiksy (niezalezne od `KitMoves`): Leave: przeszlo = sklad partii przed - po -> zaloga; Take: zaloga przed - po -> partia.

### 4.2 Ekran druzyny gracza
`PartyScreenLogic.DoneLogic(bool)` - lataja GarrisonArmory (z30:GarrisonArmory.cs:623-687, :1096-1099), Stables (:1107), UpgradePurse (:149).
Prefiks Priority.First: lewa i prawa strona (`LeftOwnerParty`, `RightOwnerParty == PartyBase.MainParty`) - liczby szeregowych skladu i lochu z
`_initialData` (wzor GarrisonArmory.ScreenDonePrefix :623-650; "Donate troops" - lewa pusta, zaloga osady). Postfiks (tylko `__result`):
- sklad lewy <-> prawy: t = min(ubytek jednej strony, przyrost drugiej) -> Take/Give;
- loch lewy <-> prawy (dawanie jencow do lochu osady / branie) - tak samo;
- wcielenie jencow w ekranie - NIE tu: zdarzenie `CampaignEvents.OnMainPartyPrisonerRecruitedEvent` (G:...CampaignEvents.cs:1033) -> Take(loch MainParty) Give(sklad MainParty);
- wypuszczenie - NIE tu: `OnPrisonerReleasedEvent` (PrisonerLaw.OnReleased, rozdz. 4.8);
- na koniec postfiksu `Reconcile` tylko tych dwoch partii, licznik "ekran druzyny - roznica" (oczekiwane 0).
Nowa partia rodu: `Helpers.PartyScreenHelper.OpenScreenAsCreateClanPartyForHeroPartyScreenClosed` (lata GarrisonArmory :689-720, :1110-1113) - migawka MainParty,
po: partia bohatera z `__1` (jak GarrisonArmory.CreatePartyPostfix) -> Take(MainParty) Give(nowa).

### 4.3 Rozwiazanie partii
`DisbandPartyCampaignBehavior.OnPartyDisbanded(MobileParty, Settlement)` (lata GarrisonArmory :408-466, :1085-1086). Gra (G:...DisbandPartyCampaignBehavior.cs:300-385):
do warowni swojej frakcji - sklad do zalogi (:342-354), loch - `SellPrisonersAction.ApplyForAllPrisoners` (:339-342 - okno 4.8); do wsi Normal - sklad znika,
milicja +0.5 x ludzie (:372-376), loch `Clear` (:367-370); inaczej - sklad znika.
Nasz prefiks: sklad, loch, zaloga przed. Postfiks: zaloga zyskala -> Take/Give; reszta skladu -> **`HomeOrNote(Take(sklad), MBackDisband, MSoldier)`** (R6: rozwiazana
partia -> DOM pochodzenia); milicja +0.5 z gry zostaje (E4/8 - wpisac w Ryzyko jako "milicja z niczego"); loch wyczyszczony we wsi -> P2 (do tego czasu Reconcile:
"jency w nicosc - rozwiazane we wsi").

### 4.4 Patrole BK (ok. 600 ludzi/d w obie strony - z30A T7 "partie rozwiazane i usuniete" 1091/d; P E4/8 ryzyko "+271 824 w 457 dobach")
- Wyjscie: `BannerKings.Components.GarrisonPartyComponent.CreateParty(Settlement)` (public static; BK:...GarrisonPartyComponent.cs:37-66 - polowa zdrowych zalogi
  losowo). Prefiks: zaloga przed; postfiks (`__result` != null): ubytek zalogi -> Take(zaloga) Give(patrol). Bez tej latki pochodzenie zalog "wyciekaloby" do "" codziennie.
- Powrot: `BannerKings.Behaviours.BKPartyBehavior.AddPatrolBehavior(MobileParty, Settlement, PopulationData)` (private; BK:...BKPartyBehavior.cs:590-614).
  Lataja: PrisonerLaw.PatrolPrefix void (z30:PrisonerLaw.cs:302-), Measure169c (:99). Nasz prefiks: sklad patrolu przed + zaloga przed; postfiks: przyrost zalogi
  -> Take(patrol) Give(zaloga). Loch patrolu - w PrisonerLaw.PatrolPrefix (4.8). Patrol znika (`DestroyPartyAction`, :613) - resztki w 4.12.

### 4.5 Jency po bitwie (U1, U16) - `MapEvent.CaptureDefeatedPartyMembers` i `LootDefeatedPartyPrisoners`
Kolejnosc gry: G:...MapEvents\MapEvent.cs:1423-1427 (Casualties -> Items -> **Prisoners** -> Ships -> **Capture**), oba przed `OnMapEventEnded` (:2079).
Lataja: Measure40 W10 (z30:Measure40.cs:97-98, :508-580), OthersRise (f) `RescuePre/RescueFin` **Priority.High** (z30:OthersRise.cs:117, :449-526).
- Prefiks (oba): liczby szeregowych skladu/lochu kazdej partii pokonanej i rosterow odbiorcow zwyciezcow (`RosterToReceiveLootMembers/Prisoners`;
  dla NPC = roster partii - G:...MapEventParty.cs:75-95; gracz - rostery PlayerEncounter, rozdz. 4.13).
- **Finalizer Priority.Last** (po RescueFin i W10):
  - Capture: kazda partia pokonana: ubytek skladu -> `Take` (laczne udzialy pokonanych); przyrost lochow zwyciezcow G_i -> `Give(loch_i, udzialy x G_i / suma)`;
    reszta (bez odbiorcy - G:...MapEvent.cs:2010-2032 zdejmuje skladu bez dopisania, gdy brak szans) -> `HomeOrNote(..., MBackFreed, MSoldier)` (R6 - rozbity do domu).
  - Loot: ubytek lochow pokonanych -> Take; przyrost skladow zwyciezcow -> Give (jency przechodza do wojska zwyciezcy - takze do Innych, gdzie E4/2 (f) zdejmuje zywych);
    zywi zdjeci przez RescueFin: nowe pole `[ThreadStatic] OthersRise.LastRescuedKilled` ustawiane w RescueFin (przy :517) i zerowane przez nas -> `DieShares(..., DRescued)`;
    reszta bez odbiorcy -> `HomeOrNote(..., MBackFreed, ...)`. W bitwach z H3 uwolnieni bez odbiorcy zeszli juz wczesniej (LosersFlee.Apply, 4.7) - oczekiwane ~0.
- Bitwa bez zwyciezcy (odwrot: `RetreatingSide`, :1957-1960) - Capture nic nie robi, my tez.

### 4.6 Smierc w bitwie, rozbici (R3, R6)
- Nowy nasluch `CampaignEvents.MapEventEnded` -> `PeopleBook.OnMapEventEnded` dopisany po z30:ArmouryBehavior.cs:697 (PeopleLedger). Zdarzenie biegnie przed
  usunieciem partii (G:...MapEvent.cs:2079 przed `HandleMapEventEnd`) - slowniki jeszcze sa. Na kazda `MapEventParty`:
  - polegli = `DiedInBattle.TotalRegulars` bez wightow (H3 juz poprawil listy w `ResultsPrefix` - z30:LosersFlee.cs:1034-1100) -> `DieShares(Take(sklad, polegli), DBattle)`;
    tabory wsi i lodzie (konto TABOR): polegli -> `DieShares` regionu ich wsi bezposrednio (zdjeci z DOM przy wyjsciu taboru, nie wracaja);
  - rozbici: przy H3 (`LosersFlee.On`) - NIE tu (4.7); bez H3 - `Take` -> udzial do puli wedlug dzisiejszego `OutlawRoutedShare` (z30:OutlawLaw.cs:487-500)
    jako `PoolShares`, reszta - licznik "rozbici w nicosc (H3 wylaczone)" (dzis tez znikaja).
- Kolejnosc z innymi nasluchami (OutlawLaw :669, BattleChronicle :670, AiWear :671, PeopleLedger :697, ROT): wszystkie `Take` sa proporcjonalne - kolejnosc
  nie zmienia udzialow.

### 4.7 H3 (LosersFlee/OutlawLaw) - rozbici, uwolnieni bez odbiorcy, powroty
- `OutlawLaw.RoutedH3Body` (z30:OutlawLaw.cs:564-627): na partie przed petla `var org = PeopleBook.Take(mep.Party.MemberRoster, routedRegulars, TRouted)`
  (rozbici zeszli z rosteru w bitwie - G:...MapEventParty.cs:316-328 - a slownik jeszcze ich ma); na element `org.Slice(n)`: czesc do puli ->
  `PoolShares(region, slice)`; `SendHome(..., slice)` (nowy opcjonalny parametr, nizej); "bez domu" -> `PoolShares`.
- `LosersFlee.SendHome(MobileParty, CharacterObject, int, Vec2, IFaction, int src, out int cat, PeopleBook.Shares org = null)` (z30:LosersFlee.cs:388-451) -
  `org == null` = dzisiaj bez zmian. Z `org`:
  - tabor/rybacy (pkt 1, :407-420) - bez zmian (TABOR);
  - "z szablonu" (pkt 2, :424-435: karawana, zaloga, milicja, patrol, straz karawan): czesc znana -> `HomeKnown(org, n, MBackH3)`, reszta "z szablonu" jak dzis;
  - zolnierz/najemnik (pkt 3, :437-442 -> `ToBk` :467-493): BK jak dzis, a `PeopleBook.Note(got, MSoldier, 0)` (:490) -> `HomeOrNote(org, got, MBackH3, MSoldier)`;
  - prosci/bandyci (pkt 4, `ToCommon` :453-465): czesc znana -> `OutlawLaw.ReturnHome(region pochodzenia, ...)`, reszta - region miejsca jak dzis.
  - `LawHomePos` (:405, :535-544) zostaje tylko dla czesci "" (U16: "LawHomePos tylko dla ludzi bez pochodzenia").
- Uwolnieni bez odbiorcy: z30:LosersFlee.cs:1104-1131 - przed `fp.Roster.AddToCounts(fp.T, -n)` (:1115) `var org = PeopleBook.Take(fp.Roster, n, TFreed)`,
  `SendHome(..., SrcFreed, out cat, org)` (:1119), reszta -> `PoolShares`. **Nie wstawiac niczego miedzy zdjecie (:1115) a SendHome** (U24) - `Take` przed zdjeciem.
- Powrot z puli (z30:OutlawLaw.cs:833-903): klucze zolnierzy/strazy (`SendHome(..., SrcPool)` :862, :879) - przed wywolaniem
  `var org = PeopleBook.PoolTake(r, n)` (udzial znany = `_poolOrg[r]` / zolnierze w puli r) i `SendHome(..., org)`; prosci (:903, `ReturnHome` regionu puli) - bez zmian
  (pochodzenie prostego = region puli: brany z wsi tego regionu - `TakeCommoners`).
- Dezercja: `OutlawLaw.OnTroopsDeserted` (z30:OutlawLaw.cs:455-479) - na poczatku `var org = PeopleBook.Take(party.MemberRoster, dezerterzy bez wightow, TDesert)`
  (zdarzenie gry biegnie po zdjeciu ze skladu); czesc W4c do domu: `OverflowHome.SendHome(party, roster, org)` -> `ClanBudget.ToVillagePop(..., org.Slice)` (4.9);
  reszta do puli -> `PoolShares(region, slice)`.
- Banda rozwiazana (z30:OutlawLaw.cs:646-668, `AddRoster` sklad i loch do puli): `PoolShares(region, Take(sklad))`, loch -> `PoolShares(region, Take(loch))`
  (jency bandy zostaja w lesie - znane pochodzenie zostaje).
- Banda z puli: `Draw` (:409-440) i `DrawOnly` (:1102-) zwracaja dodatkowo `Shares` (prosci = region puli, klucze zolnierzy = `PoolTake` tego regionu);
  `RosterPostfix` (:1127-1149) -> `PeopleBook.Pending(party, shares)`; werbunek dzienny (:945-946) -> po `p.MemberRoster.Add(got)` `Give(p.MemberRoster, shares)`;
  jency wcieleni do bandy (:925-933) -> Take(loch) Give(sklad).
- Ubytek kluczy puli (wygasanie, `_pTrim`) - proporcjonalne zdjecie z `_poolOrg` (licznik "pula: wygasle klucze").

### 4.8 Prawo jenca (I1) i sprzedaz
- Okno sprzedajacego: prefiks void **Priority.First** na `SellPrisonersAction.ApplyForAllPrisoners(PartyBase, PartyBase)` i `ApplyForSelectedPrisoners(PartyBase, PartyBase, TroopRoster)`
  (G:...SellPrisonersAction.cs:102-110) - PRZED prefiksami BK na tych dwoch metodach (BK:...Patches\SettlementPatches.cs:70-85, ktore wolaja `SendOffPrisoners`);
  finalizer zamyka. `[ThreadStatic] SaleSeller`. Lataja tez `ApplyInternal` (prywatne): Ransom178 (z30:Ransom178.cs:355), CirculationWindows (:1765) - inny cel.
- `PrisonerLaw.SendOffPrefix` (z30:PrisonerLaw.cs:224-300): `var org = PeopleBook.Take(SaleSeller.PrisonRoster, Regulars(__0), TSale)` raz na poczatku decyzji;
  Execution (:244) -> `DieShares(org, DExecuted)`; Enslavement (`AddSlaves` :255) -> `EnslaveShares`; BK "po staremu" (:266, :272) -> `EnslaveShares`
  (BK robi niewolnikow/dzierzawcow - P R15); `Route` (:173-218; wolany :292) dostaje `PeopleBook.Shares org`: Mur (`ToWall` :186) -> `WallShares(slice)`; `SendHome(..., SrcLaw, out cat, slice)` (:197);
  reszta do puli (:210) -> `PoolShares`.
- Zapas w finalizerze okna: ubytek lochu sprzedajacego minus to, co wziete w SendOffPrefix (BK bez latki, sprzedaz bez I1) -> `EnslaveShares` z licznikiem "sprzedani bez I1".
- `OnReleased` (z30:PrisonerLaw.cs:340-364): `Take(MobileParty.MainParty.PrisonRoster, n)` -> Route z udzialami.
- `PatrolPrefix` (z30:PrisonerLaw.cs:302-): `Take(patrol.PrisonRoster, n)` -> Route.
- `LootEndPrefix`/`Free` (:406-470) - udzialy oczekujace gracza (4.13); bez nich `org = null` (dzis).
- Przekazanie jenca: `TransferPrisonerAction.ApplyInternal(CharacterObject, PartyBase, PartyBase)` (G:...TransferPrisonerAction.cs:7-16; private static; nasze latki - brak):
  prefiks void, szeregowy -> Take(loch zrodla, 1) Give(loch celu, 1) (patrole gry G:...PatrolPartiesCampaignBehavior.cs:562, dezerterzy gry G:...DesertersCampaignBehavior.cs:86).
- Wcielenie jencow AI: `RecruitPrisonersCampaignBehavior.RecruitPrisonersAi(MobileParty, CharacterObject, int, int)` (G:...RecruitPrisonersCampaignBehavior.cs:111-117) -
  wlasny postfiks (obok LosersFlee z30:LosersFlee.cs:1190-1193, 1535 i RecruitSources :161): Take(loch, num) Give(sklad).

### 4.9 Punkt powrotu (2) - ClanBudget.ToVillage (166, W4c, 179)
- Sygnatura: `ToVillage(Settlement v, CharacterObject ch, int n, int cause)` (z30:ClanBudget.cs:803-812) -> `+ PeopleBook.Shares org`; po dopisaniu BK
  (`_fromSoldiers.Invoke` :809) zamiast `PeopleBook.Note(n, cause, 0.0)` (:812): `HomeOrNote(org, n, MBack166|MBackW4c|MBack179, cause)`.
- Wolajacy: `ReleaseFrom` (z30:ClanBudget.cs:715-760; :742) - `var org = PeopleBook.Take(mp.MemberRoster, n, T166)` tuz PRZED `ToVillage` (`r.AddToCounts(ch, -n, ...)` :743 jest po nim -
  `Take` nie stoi miedzy zdjeciem a dopisaniem, U24); gdy `ToVillage` zwroci false - `Give` z powrotem (ludzie zostali w partii).
  `ToVillagePop` (:796-799) z `org`: OverflowHome (z30:OverflowHome.cs:144 - org z dezercji 4.7), GentryService (z30:GentryService.cs:647 - `Take(r, el.Number)` przed).

### 4.10 Inni (E4/2) i zaraza
- z30:OthersRise.cs:225 (jency Innych skasowani): `PeopleBook.Die(region, p.Pris, DCaptives, place)` zostaje jako licznik miejsca (linia E4/2), a obok
  `DieShares(Take(p.P.PrisonRoster, p.Pris), DOthers)`; **przed** `p.Clear.Clear()` (:228).
- :386 (milicja przejetej osady): `DieShares(Take(milicja.MemberRoster, liv), DOthers)` przed zdjeciem ROT.
- :517 (odbici) - przez `LastRescuedKilled` (4.5), bez drugiego `DieShares`.
- `PeopleBook.Die(region, ...)` (z30:PeopleBook.cs:400-410) zostaje licznikiem miejsca; POLEGLI regionu pochodzenia tylko przez `DieShares` (bez podwojenia).
- Zaraza (R12, U9): z30:CampFever.cs:89 i :113 - obok `Measure40.NoteDisease` druga linia we wlasnym try:
  `PeopleBook.DieShares(PeopleBook.Take(mp.MemberRoster, m0 - Measure40.MenOf(mp), TPlague), DPlague)` (roster przed/po - dokladnie zmarli, HitRandom moze
  nie trafic - z30:CampFever.cs:165-190).

### 4.11 Powrot do DOM - jedna funkcja
`HomeKnown(org, n, cause)`: dla kazdego `(region, m)` czesci znanej (proporcjonalnie do n) -> `OutlawLaw.ReturnHome(RegionNode(region), m, PeopleBook.AService, cause, out placed)`
(z30:OutlawLaw.cs:342-356: najslabsza wies regionu; warownia bez wsi - najblizszy region z wsiami; `Move` z 1/k_v). Nie umieszczeni -> "" (licznik). Ludnosc BK -
bez zmian (dopisuja wolajacy jak dzis). Region nieznany w `Settlement.Find` (stara osada) -> "".

### 4.12 Koniec partii
`PeopleBook.OnPartyDestroyed` (z30:PeopleBook.cs:293-310, nasluch :677) - dopisac: partia ze slownikiem -> resztka skladu i lochu do licznika "w nicosc przy
zniknieciu" wedlug rodzaju, slownik usuniety. Nowy nasluch `OnPartyRemovedEvent` -> to samo (obok PeopleLedger z30:ArmouryBehavior.cs:704). Oczekiwane male: rozbitych
oproznia Capture (4.5), rozwiazanych - 4.3, patrole BK - 4.4.

### 4.13 Gracz - lup ludzi (P2)
Rostery `PlayerEncounter` (G:...MapEventParty.cs:75-95) zamiast partii: w finalizerze 4.5 udzialy dla gracza -> `_playerLootMembers/_playerLootPrisoners`;
dodatni `Reconcile` MainParty (sklad/loch) zjada je najpierw; `PrisonerLaw.LootEndPrefix` (lewe listy) -> Route/Free z nimi; resztka na koniec doby -> licznik
"lup gracza bez rozliczenia". Bez P2: przyrost MainParty po bitwie -> "" (nazwane "gracz - lup").

### 4.14 Uzgodnienie doby (`ReconcileDaily`, w `PeopleBook.Daily` z30:ArmouryBehavior.cs:1517 - po OutlawLaw :1507 i ClanBudget :1512, przed Measure40 :1551 i PeopleLedger :1552)
- Petla `MobileParty.All` (bez TABOR, konwojow BK, skladu Innych) + lochy `Settlement.All`: roster szeregowych (bez wightow; `TotalRegulars` minus `Undead.Character`)
  wobec `Sum` slownika:
  - nadwyzka rosteru: najpierw `_pending[party]`, potem "" z nazwa wedlug rodzaju i zrodla doby (nowa partia rodu z szablonu - licznik z
    `LosersFlee.LordInitPostfix` z30:LosersFlee.cs:1213-1223; zaloga z modelu - Measure169c `ModelFin` :95; milicja; karawana; banda spoza puli; jency z rabunku BK - Measure40 W11);
  - niedobor: proporcjonalne zdjecie, licznik "w nicosc" wedlug rodzaju (dla LOCHOW: do P2 - "jency: ubytek bez zdarzenia (ucieczki, pokoj)").
- Potem cache R5 (FreeHands, rozdz. 6) i linie (rozdz. 8). Koszt [S]: jak petla PeopleLedger (ok. 5 tys. partii) - kilka ms.

---

## 5. Konto POLEGLI i rownanie regionu (R0, R3)
- `rg:` trzyma od siewu: `Fallen` (wszyscy), `FallenMen` (polegli z SLUZBY/JENCOW i tabory - wszyscy to mezczyzni), `Civil` (D7/D8), `Born` (D2), `Enslaved`, `ToWall`.
- Rownanie na region r i na kraine (suma regionow kultury regionu - `RealmOf`, 6.1), raz na dobe:
  `DOM_r + SLUZBA_r + JENCY_r + WYRZUTKI_r + UCHODZCY_r - (DomRef + SRef + JRef + PoolRef + RefRef) - Born_r + Fallen_r + Civil_r + Enslaved_r + ToWall_r = reszta_r`,
  gdzie WYRZUTKI_r = prosci w puli r + `_poolOrg[*][r]` (PoolRef - z siewu), UCHODZCY - D7 (do E4/5 = 0). Reszta = ludzie z niczego/w nicosc w regionie
  (podloga 10, RESZTA hearth - D3, ruchy bez nazwy). Prog - rozdz. 9.

---

## 6. Wolne rece (R5), plon (R9 b), hamulec, okno zniw - `FreeHands.cs`

### 6.1 Kraina regionu
`RealmOf(region)` = `PeopleBook.CultureAtCalibration(region)` (z30:PeopleBook.cs:223-229; miasta i wsie maja kulture kalibracji), dla zamku - kultura kalibracji
pierwszej wsi zamku, w ostatecznosci `Measure40.CultureAtStart(region)` (z30:Measure40.cs:176 - ta sama migawka co E4/1 `WinterRealm.RegionCulture` z30:WinterRealm.cs:117-125).
Uwaga: E4/1 liczy kraine z migawki sesji - ujednolicenie WinterRealm na `RealmOf` NIE w tej paczce (jedna zmiana naraz); roznica dzis 0 (0 zmian kultury - z30A T10).

### 6.2 Liczba a (jedna definicja - U19)
`a_r = (S_r + K_m,r) / (0.27 x LRef_r)`; S_r = udzialy r we wszystkich skladach (partie rodow, zalogi, milicje, karawany, bandy; bez TABOR - praca wsi; bez "")
+ udzialy r we wszystkich lochach ("jency z regionu u wroga"); `K_m,r = max(0, FallenMen_r + 0.27 x Civil_r - 0.27 x Born_r)` (P R5; U4: mezczyzni; "przyrost
uzupelnia straty" - pamiec Jeffa levy-from-the-fields). 0.27 = `PeopleLedger.MenShare` (z30:PeopleLedger.cs:36) - jedna stala, przeniesc do FreeHands jako `internal const`.

### 6.3 Prog f
| Kraina (kultura regionu) | f | Hamulec | Zrodlo |
|---|---|---|---|
| zwykla | `FreeHandsShare` 0.20 | tak | Jeff 05.10 "czesc byla wolna" (SP:347), DOK:53; dzis stala z30:PeopleLedger.cs:37 |
| `sturgia` (Zelazne Wyspy) | 0.20 + min(`FreeHandsIronIslandsExtraMax` 0.20, niewolni / (0.27 x ludzie krainy)) | tak | RK:401 (RK 2.3 pkt 6), SP:362 "ok. 40%, pracuja niewolni"; P rozdz. 8 W2. Niewolni: do E4/9 BK z 169c `Measure169c.SlavesOf("sturgia", ...)` (uzywa juz z30:PeopleLedger.cs:688); brak danych 169c -> 0.20 |
| `freefolk` | 1.0 (L = 1, bez hamulca) | nie | P R5 "pula = polowa doroslych"; walcza wszyscy (E4/9) |
| `nightswatch` (Dar) | 0.20 tylko do L | **nie** | P R5 "Straz bez progu (rekrut z calego Westeros); Dar tylko do plonu"; S nie zawiera Strazy (""), wiec L ~ 1 |
**Okno zniw:** dzien roku `CampaignTime.Now.GetDayOfYear` (G:...CampaignTime.cs:154; rok 364 doby - `LongYearTimeModel.DaysPerYear`, z30:Calendar.cs:61-69) w [196; 252]
(8 tygodni - P R5) **i** pora Westeros lato albo jesien (`WesterosClimate.Now()`, z30:WesterosClimate.cs:260; P: "w latach lata i jesieni ... w latach zimy bez okna") ->
f x `FreeHandsHarvestFactor` 0.5 (DOK:53 "w zniwa pula x0.25-0.5", P R5). Wiosna Westeros - poza oknem (litera P; pytanie do Jeffa tylko jesli test pokaze sens).
Stale 196/252 w kodzie (`const`, z komentarzem) - bez nowych kluczy MCM.

### 6.4 Czynnik plonu L (R9 b)
`L = a <= f ? 1 : max(0.5, ((1 - min(a, 0.9)) / (1 - f)) ^ FreeHandsElasticity)`; f >= 0.999 -> 1; e = 0 -> 1 (wylacznik). (DOK krok 6 poprawka: a <= 0.9, L >= 0.5;
e = 0.5 - DOK:52 "-10% ludzi = -5% rolnictwa"; kontrola: a = 0.30, f = 0.20 -> 0.935 = "plon -6.5%" z P rozdz. 0 pkt 2.)
- Towary wsi: `MaterialLaw.ProdPostfix` (z30:MaterialLaw.cs:163-197, Priority.Last, licznik `_depth`) - po bloku "mines and woods" (:182-188), przed `OreLedger.NoteModel` (:190), **bez progu 0.001**
  (DOK krytyk krok 6 pkt 3): `float hands = FreeHands.YieldFactor(village); if (hands != 1f) { float sum = 1f + __result.SumOfFactors; if (sum > 0f) __result.AddFactor((hands - 1f) * sum, _txtHands); }`
  (ten sam wzor mnozenia co wpis 98, :182-188). Napis: `"{=!}Armoury: hands away at war (men under arms and the fallen)"`. Z E4/5 (D6) - jedna linia
  `hands = Devastation.YieldFactor(village) * FreeHands.YieldFactor(village)` i wspolny napis (P: "(a) i (b) mnoza sie w jednym czynniku").
- Zywnosc wsi: wlasny postfiks `FreeHands.FoodPostfix(Village village, ref float __result)` na `DefaultVillageProductionCalculatorModel.CalculateDailyFoodProductionAmount`
  (ten sam cel co z30:WinterBite.cs:173, :203, :207 - postfiksy mnoza sie, kolejnosc bez znaczenia); z E4/5 - w `Devastation.FoodPostfix` jak wyzej.
- `YieldFactor(v)`: `OutlawLaw.RegionFor(v.Settlement)` -> cache doby (slownik id regionu -> L, podmiana referencji raz na dobe; odczyt bez blokady).

### 6.5 Hamulec (Jeff 11.10 TAK) i chec (Levy)
- `Levy.ProbabilityPostfix` (z30:Levy.cs:93-107) - tylko PUSTE miejsce (galaz :101 zostaje): `FreeHandsBrake && FreeHands.Braked(region osady)` -> `__result = 0`,
  licznik "odmowione przez ksiege" wedlug krainy (U17). Region = `OutlawLaw.RegionFor(__2)`. Ochotnicy juz stojacy u notabli zostaja do zwerbowania (najwyzej 6 na notabla).
- `Levy.Willingness` (z30:Levy.cs:78-91): `ex` = `FreeHands.Excess(region)` = `max(0, f - a) x min(1, DOM_r / DomRef_r)` (DOK krok 7 + poprawka krytyka "wykrwawiona kraina
  daje mniej ochotnikow"; P "nadwyzka rak x (ludzie / start)") zamiast BK `ExcessShare` (:58-76; zostaje jako zapas, gdy FreeHands bez danych - przed siewem). Wzor checi bez zmian
  (`RecruitBaseWilling + ex x RecruitExcessWeight + nedza x RecruitMiseryWeight`). Oczekiwane [S]: ex ok. 0.19 (DOK) wobec BK ok. 0.16 -> "Pobor: chec" x0.3-0.4 (z30A: 0.30-0.32).
- Pula BK = 0 przy wolnych rekach (U17, drugi hamulec): skan dobowy osad z notablami - `MilitaryData.Manpower <= 0` (BK `CanHaveRecruits` - BK:...BKVolunteerModel.cs:236-249 - wtedy
  notabl jest pomijany w G:...RecruitmentCampaignBehavior.cs:224, nasz postfiks go nie widzi) przy `a < f` -> licznik "hamulec BK" wedlug krainy.

### 6.6 PeopleLedger na jednej definicji (U19)
- z30:PeopleLedger.cs:37 (`FreeHands = 0.20f`), :481 (`r.Burden > FreeHands`), :633 (`ProgOf`), :662-692 ("Krainy (4.15)" - obciazenie `a[1]`): obciazenie = `FreeHands.A(region)`,
  prog = `FreeHands.F(region)`; tekst "(zaloga + wyrzutki + bandy) / mezczyzni" -> "(sluzba i jency z regionu + polegli netto) / mezczyzni w dniu siewu (R5, E4/7)".
- Linia "Ludzie:" "regionow ponad prog 20% mezczyzn" -> "regionow ponad prog wolnych rak (R5, wedlug krain)"; **parser** `tools/sprawdz_logi.py:2038` (klucz `regionow_ponad`) i CSV
  `obciazenie_proc_mezczyzn` (:7389-7422) - kolumna zostaje, znaczenie nowe (komentarz w parserze). Przed siewem/wylaczone - stara definicja (bez zmiany tekstu).

---

## 7. Menu osady "People of this land" (U21; Jeff 05.10 SP:347) - `PeopleMenu.cs`
- Wpiecie: `PeopleMenu.AddMenus(starter)` w `OnSessionLaunched` obok z30:ArmouryBehavior.cs:1210-1212 (wzor `GarrisonKit.AddMenus` z30:GarrisonKit.cs:110-128).
  `starter.AddGameMenuOption(menu, "arm_people_" + menu, "{=!}People of this land", Condition, Consequence, false, 6)` dla `village`, `town`, `castle`.
- Condition: `PeopleMenuEntry` i `Settlement.CurrentSettlement != null`; `args.optionLeaveType = GameMenuOption.LeaveType.Manage`. Bez stanu, bez obliczen ciezszych niz odczyt cache.
- Consequence: **tylko** `InformationManager.ShowInquiry(new InquiryData(...))` (wzor z30:QohorMasters.cs:295) - **bez `GameMenu.SwitchToMenu`** (CLAUDE.md 7; to nie jest menu oczekiwania).
- Tresc (po angielsku, liczby z cache doby; region osady = `OutlawLaw.RegionFor`):
  - tytul `"The people of " + region.Name` (dla wsi: `region.Name + " and its villages"`);
  - `At home: X (villages Y in N villages, town Z)`;
  - `Under arms: X men from this land serve in parties, garrisons, militias, caravans and bands`;
  - `Held captive: X`;
  - `Outlaws from this land: X` (prosci puli regionu + `_poolOrg[*][r]`);
  - `Refugees: X` (tylko gdy jest konto E4/5), `Slaves: X` (tylko od E4/9);
  - `Fallen since the reckoning began: X` (`Fallen_r`), `Born since the reckoning began: X` (gdy D2);
  - `Free hands used: a% of f%` + `(harvest: f is halved)` w oknie; przy hamulcu `"No new levies: this land has no free hands left."`; Dar: `"The Watch takes men from all of Westeros - no limit here."`;
  - `Harvest of the fields: L%` (gdy L < 1);
  - przed siewem / wylaczone: `"The people of this land have not been reckoned yet."`.
- E4/11 rozszerzy te sama pozycje o wioski poboczne (P).

---

## 8. Klucze, linie logu

### 8.1 Klucze (Settings.cs po z30:Settings.cs:692; `python tools/gen_mcm.py` - Armoury z30: 1012 pozycji; w `Armoury.json` Jeffa przy kodzie `grep` kazdego klucza - CLAUDE.md 7)
| Klucz | Domyslna | Uzasadnienie | Podpowiedz MCM (EN, skrot) |
|---|---|---|---|
| `PeopleOriginEnabled` | true | P E4/7; bez niego nic z rozdz. 2-6 (stan zamrozony, latki no-op) | every soldier and captive remembers the land he came from ... |
| `LevyFromVillages` | true | P R4/U7; symetria z powrotem (2.4) | a recruit leaves his village (1/k of a hearth) or town; a soldier from that land who comes home brings him back. Change it only when you start a new campaign |
| `GarrisonLevyFromBk` | true | P E4/7 "A5 ... zdejmuje tez BK DeduceManpower"; osobny wylacznik, bo zmienia pule BK (ryzyko 10.6) | a garrison filling its ranks from the notables takes the man from Banner Kings' population too, as any other recruit |
| `FreeHandsShare` | 0.20 | Jeff 05.10, DOK:53, z30:PeopleLedger.cs:37 | share of a land's men who can leave the fields without the harvest suffering |
| `FreeHandsHarvestFactor` | 0.5 | P R5, DOK:53 (x0.25-0.5) | during the harvest weeks only this part of that share is free |
| `FreeHandsElasticity` | 0.5 | DOK:52 (-10% hands = -5% crops); 0 = plon bez rak | how much the harvest falls when more men are away than the land can spare (0 = not at all) |
| `FreeHandsIronIslandsExtraMax` | 0.20 | RK:401, SP:362 | Iron Islands: thralls work the land, so up to this much more of the men may go reaving |
| `FreeHandsBrake` | true | Jeff 11.10 (SP:1137) | a land with no free hands left raises no new volunteers until births make up its losses |
| `PeopleMenuEntry` | true | SP:347, U21 | "People of this land" in village, town and castle menus |
Opis `PeopleUnitEnabled` (z30:Settings.cs:681) - bez zmian (juz ma "Change it only when you start a new campaign").

### 8.2 Linia startowa (SubModuleMain - `PeopleBook.ApplyOrigin(_harmony)` we wlasnym try po `OthersRise.ApplyAll` z30:SubModuleMain.cs:139; latki wpiete zawsze, licza przy kluczu - U26)
`Pochodzenie (E4/7): wpiete: punkt zdjecia BK DeduceManpower, okno werbunku AI, okno werbunku gracza (RecruitmentVM.OnDone), autowerbunek zalogi (A5), karczma gracza 2/2, zaloga <-> partia 2/2, ekran druzyny, nowa partia rodu, rozwiazanie partii, patrole BK 2/2, jency po bitwie 2/2, sprzedaz jencow 2/2, przekazanie jenca, wcielenie jencow AI, zywnosc wsi (rece); BRAK: -; wlasciciele latek celow: <cel: Harmony id, ...>; ustawienia startowe przed MCM (stan gry - linia dzienna).`
(`Harmony.GetPatchInfo` jak E4/0 U25; "BRAK" przy braku typu BK.)

### 8.3 Linie dzienne (przy LogEnabled; kolejnosc: w `PeopleBook.Daily`, po linii "Ludzie (ksiega)")
- `Pochodzenie (E4/7): dzien N | stan: SLUZBA S (z pochodzeniem w R regionach) + bez pochodzenia B (Straz b1, szablony b2, rody bez ziemi b3), JENCY J + B4 | wejscia: pobor z DOM a (wsie a1, miasta a2, karczmy a3, A5 a4, gracz a5, pobor wymuszony a6), z puli do band p, wcieleni jency c | przenosiny: partia->zaloga t1, zaloga->partia t2, ekran druzyny t3, rozwiazane do zalog t4, patrole BK wyjscie t5 / powrot t6, jency po bitwie: wzieci w1, odbici w2, przekazani w3 | wyjscia: polegli d (bitwa d1, zaraza d2, straceni d3, Inni d4, tabory d5), do domu h (H3 h1, 166 h2, W4c h3, 179 h4, z puli h5, jency h6, rozwiazane h7), do puli o, na Mur m, do niewoli e | bez nazwy: z niczego +z (partie rodow z1 [szablon klanu z1t], zalogi z2 [model z2m], milicje z3, karawany z4, bandy z5, inne z6, jency z7 [rabunek BK z7r], gracz z8), w nicosc -q (wedlug rodzaju ...; przy zniknieciu partii q1) | kontrola: zmiana stanu - (wejscia - wyjscia + z - q) = 0.00 | DeduceManpower: wywolan x (ludzi y), poza oknem 0, pobor na podlodze 0 | potkniecia P | czas T ms.`
- `Wolne rece (E4/7): dzien N | prog: zwykly 20%, Zelazne Wyspy x% (niewolni BK n), Wolni Ludzie 100%, Straz bez progu (Dar - tylko plon); okno zniw (dni 196-252, lato/jesien): TAK/NIE - prog x0.5 | swiat: a srednio x% mezczyzn, regionow ponad progiem n z 227 (hamulec - bez nowych ochotnikow), L: min y (region), regionow z L < 1 k | top 8: region (kraina) a = (S + K_m) / (0.27 x L_ref) = x% [S s, K_m = polegli m + 0.27 x cywile c - 0.27 x narodziny b], prog f%, L l | siew dzien D, L_ref swiata X mln.`
- `Pobor:` (z30:Levy.cs:109-115 `Flush`) dopisek: `| E4/7: nadwyzka rak z ksiegi srednio e (dotad BK WorkforceExcess) | odmowione przez ksiege (a >= f) R losowan w K regionach (krainy: ...) | pula BK = 0 przy wolnych rekach O osad w K2 regionach (krainy: ...) | okno zniw TAK/NIE.`
- `Ludzie (ksiega)` (z30:PeopleBook.cs:414-460) - nowy odcinek: `| E4/7: konto SLUZBA S + "" B, JENCY J + "" B4, POLEGLI od siewu P (dzis p), cywile od siewu C, narodziny od siewu N | pobor z DOM dzis a ludzi / -h hearth, powroty z pochodzeniem +b ludzi / +h2 hearth | rownanie krain: najgorsza kraina x reszta r (q% ludzi krainy), swiat r0`.
- `Hearth (E4/0)` (z30:Measure40.cs:836-840): **dopisac** `o8 = mv[MLevyVillage] + mv[MLevyA5]` (ujemne) i `o9 = suma mv[MBackH3..MBackDisband]` do `named` i do tekstu
  ("pobor z wsi (E4/7) -o8, powroty z pochodzeniem (E4/7) +o9") - inaczej RESZTA hearth wchlonie ok. -8 / +4 hearth/d [S: 1 500 / 750 ludzi przy sredniej 191 ludzi/hearth] (T2 E4/0).

### 8.4 Narzedzie `tools/sprawdz_logi.py --grupa etap4` - blok E4/7 (rusza, gdy jest linia startowa "Pochodzenie (E4/7)")
Progi z rozdz. 9 jako wiersze TAK/NIE/INFO (wzor bloku E4/3: stan z linii dziennej, nie startowej - poprawka R2-1 E4/3). Parser :2038 i CSV - 6.6.

---

## 9. Test 120 dob (nowa kampania, ustawienia jak u Jeffa) + zapis z doby 362 (9 dob)
Wspolny test zbiorczy (Jeff 11.10 03:05) - jesli E4/7 idzie z innymi paczkami, progi ponizej dochodza do ich progow.
| # | Prog | Jak | Baza / skad liczba |
|---|---|---|---|
| T1 | 0 ERROR naszych modow; linia startowa "BRAK: -"; potkniecia PeopleBook/FreeHands 0 (najwyzej 5 z nazwa miejsca) | log | standard paczki |
| T2 | "Pochodzenie": kontrola \|.\| <= 1 czlowiek kazdej doby | linia dzienna | z definicji (kontrola kodu) |
| T3 | rownanie krain: \|reszta\| <= 0.1% ludzi krainy kazdej doby (najgorsza kraina) | "Ludzie (ksiega)" | P E4/7 test; **przy D3 niespelnionym (RESZTA hearth != 0) - INFO** |
| T4 | "w nicosc" (bez nazwy, wszystkie rodzaje) <= 1% ruchu brutto SLUZBA + JENCY (srednia 28 dob) | "Pochodzenie" | P rozdz. 4 ("reszta ksiegi ludzi < 1% ruchu brutto"); z30A T7 dzis +1072/d bez nazwy |
| T5 | "z niczego" tylko w kategoriach nazwanych (szablony, model zalog, milicja, rabunek BK, Straz, gracz); nienazwane <= 1% ruchu | "Pochodzenie" | INFO oczekiwane: nowe partie ok. 680/d, zalogi z modelu ok. 105/d, milicja ok. 92/d, rabunek BK ok. 35/d (z30A T7) - zamyka E4/8/E4/9 |
| T6 | pobor z DOM = werbunek od notabli + karczma ("Ludzie:") + A5: \|roznica\| <= 2%; "DeduceManpower poza oknem" 0; "pobor na podlodze" 0 | "Pochodzenie", "Ludzie:" | z30A: werbunek ok. 1 339/d (T7), A5 151/d |
| T7 | d1 (pierwsza doba po siewie): L = 1.00 we wszystkich regionach z a <= f; lista regionow ponad progiem (oczekiwane <= 5; Straz poza lista) | "Wolne rece" | P E4/7 test (DOK krok 6 ryzyko); z30A stara definicja: 6 z 227 (w tym Eastwatch z wightami) |
| T8 | "Pobor: chec" srednio x0.25-0.45 (d28-d120); hamulec ksiegi i BK osobno w linii; hamulec BK > 30% losowan -> zapis do STAN-PRAC (prog odlozonego dopasowania BEE, P rozdz. 7) | "Pobor:" | z30A x0.30-0.32; P: x0.3-0.4 |
| T9 | wojsko lordow w wojnie 95-115 tys. srednio d90-d120 (podloga 85 tys.) | progi etapu 2 | P E4/7; z30A partie rodow 107 tys. (d120) |
| T10 | "Ludnosc BK (169c)" d1 -> d120 przeliczone na rok <= +2% (i INFO trendu - A5 z BK) | 169c | P rozdz. 4 |
| T11 | netto "Ludnosc:" d28-d120 miedzy -720/d (-0.5%/rok, Jeff 11.10) a +1 150/d; skladniki INFO: przyrost naturalny (E4/4), pobor -a, powroty +b, polegli (bez wplywu na DOM) | "Ludzie:", "Ludzie (ksiega)" | 52.5 mln x 0.5% / 364 = 721; x 0.8% / 364 = 1 154 |
| T12 | koszt: linie E4/7 + Reconcile <= 15 ms/dobe; s/doba <= +3% wobec z30A | "czas" w linii | z30A E4/0 calosc 10.8 ms/d |
| T13 | zapis z doby 362, 9 dob: 0 bledow; po wczytaniu "z zapisu N partii ..., nie znaleziono 0-2%"; rozmiar `arm_people` (SaveText) w logu | log | 161 |
| T14 | menu: reczna proba Jeffa - wies/miasto/zamek -> "People of this land" -> okno; liczby = linia dzienna tego regionu | gra | P E4/7 test |
| T15 | progi etapow 1-3 (zadne krolestwo > 85% bitew lordow ani 3 twierdze; glowy < 5 000 <= 10; 0 bankructw; reszta ksiegi pieniadza < 10 tys. zl/d) | progi-etap2/3 | P rozdz. 4 |
Nie przechodzi -> wylaczamy kawalek w `Armoury.json` autotestu (`FreeHandsBrake`, `GarrisonLevyFromBk`, `LevyFromVillages`, `PeopleOriginEnabled` - w tej kolejnosci) i powtarzamy.

---

## 10. Ryzyka i kolizje (kontrola calosci - zasada 0; wpisac do "Ryzyko / co sprawdzic" w CHANGELOG)

1. **Wspolne cele latek (grep z30 + BK + G):** `ApplyInternal` werbunku (LevyGold :198, CirculationWindows :1760, BK postfiks) - nasze void First + finalizer;
   `TickAutoRecruitmentGarrisonChange` (RecruitSources :171, Measure169c :94, ClanBudget :916 bool Strazy, RecruitCost :383); `Leave/TakeTroopsFromGarrison`
   (GarrisonArmory :1067-1077, LosersFlee :1559-1565, Measure169c :96-97); `PartyScreenLogic.DoneLogic` (GarrisonArmory :1096, Stables :1107, UpgradePurse :149);
   `OnPartyDisbanded` (GarrisonArmory :1085); BK `AddPatrolBehavior` (PrisonerLaw :302, Measure169c :99); `Capture/LootDefeated...` (Measure40 :97-98, OthersRise :117 High);
   `SellPrisonersAction` publiczne (BK prefiksy SettlementPatches.cs:70-85) i `ApplyInternal` (Ransom178 :355, CirculationWindows :1765); `RecruitPrisonersAi`
   (LosersFlee :1535, RecruitSources :161). Wszystkie nasze nowe latki: void/finalizer, bez `ref` na wyniku, bez zapisu pola gry. Linia startowa wypisuje wlascicieli.
2. **Martwy kod ozywa:** odstepstwo 1 E4/3 (SLUZBA -> DOM bez hearth) konczy sie - powroty 166/W4c/179/H3/pula dostaja hearth (ok. +750 ludzi/d [S z z30A: H3 468 + 166 294]),
   ale tylko czesc znana; jednoczesnie pobor zdejmuje ok. 1 500/d. Prog E4/4 (290-1 150/d) w biegu zbiorczym E4/4+E4/7 moze wyjsc ujemny - to decyzja Jeffa 11.10 (wojna do
   -0.5%/rok), T11 ma te granice. Opisac w CHANGELOG i STAN-PRAC.
3. **Symetria zdjecie/powrot:** jeden warunek `LevyLive` (2.4); przy przelaczeniu w trakcie kampanii rekrut idzie do "" - zero ludzi z niczego. `PeopleUnitEnabled` off -> obie strony `Note`.
4. **Nic drugi raz:** BK `DeduceManpower` (sklad) + 1/k z DOM (ilosc) - nie podwojenie (R0, P 5.2 pkt 2); A5 z BK tylko pod flaga `_selfDeduce`; `PeopleBook.Die(region,...)` E4/2
   zostaje licznikiem miejsca - POLEGLI tylko z `DieShares`; odbici przez Innych - jedno `DieShares` (przez `LastRescuedKilled`); R9 (a) liczy uchodzcow, (b) sluzbe i poleglych (U4).
5. **Kolejnosc w dobie:** OutlawLaw.Daily (:1507 - powroty z puli) -> ClanBudget.Daily (:1512 - zwolnienia 166) -> OthersRise/PeopleBook.Daily (:1517 - Reconcile, cache R5, linie) ->
   Measure40.Daily (:1551 - linia Hearth czyta `TakeHearth40` z nowymi przyczynami) -> PeopleLedger.Daily (:1552 - "Krainy" czyta `FreeHands.A`). Levy (chec, hamulec) czyta cache
   z poprzedniej doby (pierwsza doba sesji - liczony leniwie).
6. **Pula BK maleje szybciej:** A5 z BK ok. 150/d = ok. 1.9%/rok z 2.9 mln (z30A) przy dzisiejszym -2.6%/rok -> mniej ochotnikow (hamulec BK), mniej wojska (T9). Wylacznik
   `GarrisonLevyFromBk`. Odlozone dopasowanie BEE do ksiegi (P rozdz. 7) ma prog liczbowy - T8.
7. **Geografia band:** pula zostaje w regionie miejsca (0 pkt 7) - tempo wojny E4/3 (z30:OutlawLaw.cs:833, `AtWarForHome`) i regula Jeffa 03:30 bez zmian. Bandy biora pochodzenie z puli.
8. **Zalogi i patrole BK:** bez latek 4.4 pochodzenie zalog codziennie odplywaloby do "" (ok. 600 ludzi/d w obie strony). Sygnal bledu w T4/T5: jednoczesnie "w nicosc - zalogi" i
   "z niczego - zalogi" po kilkaset/d.
9. **Regiony ponad progiem od d1:** siew bez zdejmowania - regiony z duzymi zalogami i druzynami rodow (np. Krolewska Przystan/Red Keep, Smocza Skala, Sarnor - male krainy)
   moga miec a > f w d1 -> L < 1 i hamulec od pierwszej doby. T7 wypisuje liste; to zgodne z regula (P rozdz. 0 pkt 7: "male krainy moga slabnac latami").
10. **Male krainy w wojnie:** K_m rosnie ok. 2-3% mezczyzn rocznie w wojnie [S: polegli 0.6-0.8% ludnosci/rok = 2.2-3% mezczyzn, narodziny ok. 0.5% mezczyzn/rok] - po kilku latach
    ciaglej wojny hamulec w wielu regionach. To decyzja Jeffa 11.10 ("do odrobienia strat"); obserwowac w biegu 728 dob (koniec etapu).
11. **Straz:** rekrut i siew do "" - Dar bez ubytku (DOK krytyk krok 5 pkt 4), Straz bez hamulca; prawdziwe zrodlo Strazy - E4/9. Polegli Strazy - licznik "".
12. **Wolni Ludzie f = 1:** bez hamulca i bez L; DOM moze spadac (rekrut 1/k, k ok. 42/hearth) - "male krainy moga slabnac" (P). Podloga 50 hearth na wies (TakeLevy).
13. **Zapis:** ok. 150 KB w `arm_people` (SaveText.Sync w kawalkach); starszy DLL pomija nieznane sekcje (`Import` czyta po przedrostku - z30:PeopleBook.cs:137-172) - cofniecie DLL
    gubi pochodzenie (nowy siew przy ponownym wgraniu, bez skoku hearth). `Reset` w konstruktorze ArmouryBehavior.
14. **Watki:** latki werbunku, bitew i ekranu - watek glowny; `YieldFactor` (model produkcji) czyta cache bez blokady (podmiana referencji raz na dobe w Daily).
15. **Wyjatki:** kazde wywolanie ksiegi w zywym kodzie we wlasnym try i nigdy miedzy zdjeciem a dopisaniem ludzi (U24) - lista do recenzji: LosersFlee :490, :1115-1119; OutlawLaw :455-479,
    :564-627, :646-668, :862, :879, :925-946, :1127-1149; ClanBudget :742-743, :809-812; OverflowHome :144; GentryService :647; PrisonerLaw :186-210, :244-273, :340-364, :406-470, :302;
    CampFever :89, :113; OthersRise :225, :386, :517; PeopleUnit :299; MaterialLaw :188-190; Levy :87, :103.
16. **Gracz = AI:** ten sam punkt zdjecia (BK) i te same powroty; karczma gracza i pobor wymuszony osobnymi latkami; lup ludzi gracza - P2 (do tego czasu "" z nazwa).
17. **Galezie rownolegle:** E4/4 (`w-toku/e4-4`) zmienia PeopleBook (Export/Import/Daily), PopulationLaw, Measure40 (linia Hearth), ScorchedEarth, Settings - E4/7 zaczynac od czubka E4/4
    po jego recenzji (D5). E4/5/E4/6 - punkty styku D6-D8 (po jednej linii).

---

## 11. Kolejnosc kodowania (galaz `w-toku/e4-7` od czubka E4/4; kazdy krok: build Release kod 0, osobny commit; jeden test na koncu)
1. **K1 ksiega bez skutku w grze:** `partial` PeopleBook + `PeopleBook.Origin.cs` (2.1-2.4: Shares, Take/Give, siew, Reconcile, zapis, Resolve, Reset, linia "Pochodzenie") + nasluchy
   konca partii (4.12) + `MapEventEnded` (tylko smierc - 4.6) + klucz `PeopleOriginEnabled`. Hearth nie rusza.
2. **K2 przenosiny i jency:** 4.1-4.5, 4.7 (bez hearth: `SendHome` z `org`, ale `LevyLive` jeszcze false), 4.8, 4.10. Kontrola T2/T4 w autotescie 40 dob (opcjonalnie).
3. **K3 punkt zdjecia i powroty z hearth:** 3.1-3.6, 4.9, 4.11, nowe przyczyny 2.5, `Measure40` o8/o9 (8.3), klucze `LevyFromVillages`, `GarrisonLevyFromBk`.
4. **K4 wolne rece:** `FreeHands.cs` (6.1-6.5), MaterialLaw, postfiks zywnosci, Levy (chec, hamulec, linia), PeopleLedger U19 (6.6), klucze `FreeHands*`.
5. **K5 menu:** `PeopleMenu.cs`, `PeopleMenuEntry`.
6. **K6:** `Settings.cs` (komentarze po polsku bez znakow, podpowiedzi EN) + `python tools/gen_mcm.py`, parser (8.4, 6.6), CHANGELOG (Problem z liczbami z30A, Przyczyna, Zmiana, Odstepstwa:
   sekcje `so:`/`jo:` zamiast `s:`/`j:`, pula geograficzna z udzialem pochodzenia, Straz do "", siew rodow wedlug ludzi lenn; Ryzyko = rozdz. 10; Status DO SPRAWDZENIA), dwie recenzje diffu
   (ksiega ludzi; kod i kolizje), potem test zbiorczy 120 dob + zapis.
