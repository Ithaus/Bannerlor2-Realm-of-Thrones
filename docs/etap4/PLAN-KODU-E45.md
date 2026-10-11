# PLAN KODU E4/5 (113n) - spustoszenie, powrot do roku, plon od uchodzcow, Looted bez zera - 11.10.2026

Status: SAM PLAN. Kod, gra, ustawienia i zapisy nietkniete. Wykonawca pisze po E4/4 (109n) na drzewie zbioru 30 + E4/4.
Wiazace: P = `docs\etap4\PROJEKT-ETAP4-KSIEGA-LUDZI-ZIMA-2026-10-10.md` (R8, R9a, E4/5 :422-443, 5.1-5.2), KR = `KRYTYKA-E4.md` (U13, U14, U23, U24),
SP = `docs\STAN-PRAC.md` "DECYZJE JEFFA 11.10" (4): spalona wioska wraca rok od ostatniego najazdu - TAK; ludnosc w ciaglej wojnie moze spadac - TAK.

Skroty miejsc (wszystkie sprawdzone 11.10):
- **Z:** = `scratchpad\e4\z30\Armoury\src` (zbior 30, c02c80f; = drzewo `44` w chwili pisania - E4/4 jeszcze bez zmian w plikach), **ZT:** = `...\z30\tools`
- **113:** = klon `lancuch`, commit `5cbb0d6` (paczka 113); kopia pliku `scratchpad\e4\p45\Devastation113.cs`, diff `p45\113.diff`, opis `docs\paczki\113-ludzie-spustoszenie.md`
- **G:** = `scratchpad\dec32\cs_full` (TaleWorlds.CampaignSystem 1.4.8; kluczowe miejsca potwierdzone swiezym ilspycmd 11.10: `p45\VGP.cs`, `p45\Village.cs`)
- **R:** = `scratchpad\e4tools\dec2\Raid.cs` (RaidEventComponent), **BK:** = `scratchpad\e4tools\bkfull`, **NV:** = `scratchpad\e4\dec\jedz\Naval.all.cs`, **ROT:** = `scratchpad\e4tools\dec\ROTOthers.cs`
- **HR:** = `docs\HISTORIA-RABUNKU-I-BITEW-2026-10-07.md` rozdz. 2; **LOG-A** = `scratchpad\kopia-z30-A\Armoury-2026-10-11_04-36-46.log` (bieg A zbioru 30, 120 dob)
- skrypt pomiaru do tego planu: `scratchpad\e4\p45\raid_stats.py` (linie "Hearth (E4/0)" LOG-A)

Rok = 364 doby (kalendarz Armoury, `Calendar.cs`, WeeksPerSeason 13). "/d" = na dobe.

---

## 0. W skrocie dla wykonawcy

1. Nowy `Devastation.cs` (~750 linii) przeniesiony z 113 z czterema zmianami regul: parametry HR 2.8 (zabici 2/5%, las 2% + 3% x niebezpieczenstwo,
   pojmani 10% tylko przy niewoli - **czynni dopiero z E4/9, do tego czasu 0**), **powrot liniowy do 364 dob od ostatniego rabunku/zerowania w regionie
   po 30 dobach spokoju** (zamiast 0.1%/d x (1 - niebezpieczenstwo)), uchodzcy -1.3%/rok, plon ^1.0, **wies Looted produkuje** (nowa latka na `TickProductions`).
2. **Stan uchodzcow i zegar regionu w `PeopleBook`** (sekcje `u:` i `r:` klucza `arm_people`), nie w Devastation i nie w PopulationLaw (U23) - inaczej niz w 113.
3. Kazdy ruch hearth przez `PeopleBook.Move` (nowe przyczyny MDevRaid, MDevForage, MRefugeeHome, MRefugeeFloor); Devastation pisze `v.Hearth` wprost tylko przy
   podmianie hearth gry na czas kroku rabunku (113 RaidState.Virt) - to nie ruch ludzi, przywracane co do bitu.
4. Kolizja do naprawy w E4/0: okno W6 (`Measure40.RaidPre`) musi pomijac wsie objete spustoszeniem - inaczej ten sam hearth liczy sie dwa razy (W6 + ksiega).
5. Siedem commitow (K1-K7), jeden wylacznik `DevastationEnabled` (+ `LootedProducesByPeople`); test 120 dob wspolny z E4/4 (zasada Jeffa 11.10 03:05) - progi nizej (rozdz. 11).

---

## 1. Reguly do zakodowania (P R8, R9a; HR 2.8; SP 11.10)

| Regula | Wzor / liczba | Zrodlo |
|---|---|---|
| trafieni (rabunek, zerowanie) | `hit = (reach - away) x (1 - e^(-R x osobodni / reach))`, `reach = pulap x (dom + away)`, nie wiecej niz `open = min(reach - away, (hearth - 11) x k)` | 113 :184-199, :221-233 (bez zmian) |
| R | rabunek 0.5 czl./zbrojnego/dobe lupienia; zerowanie 0.2 | HR 2.1 (0.1-1.0), 113 |
| pulap | 90% ludzi okregu (dom + uchodzcy tej wsi), w kodzie najwyzej 95 | HR 2.7 |
| zabici | 2% trafionych; **5% gdy najezdzca z kultury z niewola** (`LosersFlee.SlaveCulture(attacker.MapFaction.Culture)` - ta sama regula co H3, Z:LosersFlee.cs:900); nie wiecej niz 1 na zbrojnego na dobe (`dead <= osobodni`) | HR 2.2 |
| pojmani | 10% trafionych tylko przy niewoli - **w E4/5 udzial 0** (zostaja uchodzcami; BK dalej daje swoich jencow z niczego, nie wolno zdjac drugi raz - U14); liczone "byloby" do logu | HR 2.3, P :426-427 |
| las | `(2 + 3 x U)%` trafionych, U = niebezpieczenstwo wsi z przyrostu (0.4 x bezprawie + 0.3 x wojna + 0.3 x spalone wsie regionu); 0 gdy prawo wyrzutkow wylaczone | HR 2.4 |
| uchodzcy | reszta: `hit - zabici - pojmani - las` (kontrola = 0) | R8 |
| powrot | brak, gdy w regionie (warownia + jej wsie) w ostatnich 30 dobach byl rabunek albo zerowanie z trafionymi > 0 (jeden zegar `r:`); potem dziennie `back = away / max(1, 364 - d)`, d = dzis - ostatnie spustoszenie regionu; ostatni czlowiek wraca caly (`away - back < 1` -> wszyscy); kolejny najazd zeruje zegar | U13, SP 11.10 (4), HR 2.5 |
| blokada oblezenia | warownia regionu oblezona -> dzis 0 (HR 2.8 "wies spalona albo oblezona + 30 dob"); "rok" sie nie lamie, bo wzor dogania (reszta / pozostale dni) | HR 2.8 - decyzja projektu, bez klucza (rozdz. 15) |
| smierc uchodzcow | `q = 1 - (1 - 0.013)^(1/DaysInYear)` = 3.595e-5/d przy 364; przed powrotem, kazdego dnia | HR 2.5, R1 |
| plon (R9a) | towar i zywnosc wsi x `(dom / (dom + uchodzcy))^1.0`, dno `(1 - pulap)^e` | HR 2.6, SP:459-460 |
| progi 200/600 | poziom z `hearth + uchodzcy / k` (tylko podnosimy) | 113 :432-447 |
| Looted | wies spladrowana produkuje jak Normal x udzial ludzi w domu (gra daje 0: G DefaultVillageProductionCalculatorModel.cs:20, :78 i dodatkowo bramka `IsDeserted` w G VillageGoodProductionCampaignBehavior.cs:141) | P :430, SP:459-460 |
| koniec "+0.5 ponizej 40" | `ScorchedEarth.RefugeeReturn` = 0 dla wsi objetej spustoszeniem | P :425, 5.2 pkt 2 |
| "3% podstawy wsi do lasu przy spaleniu" | `OutlawLaw.OnVillageLooted` nic nie robi dla wsi objetej (las liczy Strike) | 113 pkt 5, P 5.2 pkt 2 |
| lup "4 zl za hearth" | co do monety jak w grze (RaidState.Virt) | 113 pkt 3, P :432 |

Liniowosc - liczby do testu: udzial uchodzcow z doby d+30 zostaly w dobie d (bez smierci): d = 60 -> 91.0%, 90 -> 82.0%, 119 -> 73.4%, 182 -> 54.5%
(ze smiercia 54.2%), 300 -> 19.2%, 363 -> 0.3%, 364 -> 0.

---

## 2. Stan wyjsciowy

**Z (zbior 30) - co juz jest:**
- `PeopleBook` (Z:PeopleBook.cs): konta ADom..AOutside (:62), przyczyny MSeed..MIncident, `MKinds = 15` (:64-65), `MName` (:66-70), `MHearth` (:73), `Move` (:238-261),
  `Note` (:265-269), `Count` (:271-275), `TakeHearth40` dla Measure40 (:278-282), zapis `Export`/`Import` "v1|s:|c:|v:" (:102-175), `Reset` (:92-99), linia "Ludzie (ksiega)"
  w `Daily` (:414-460, odcinek `UnitSegment` :466-513), wolana z `OthersRise.Daily` (Z:OthersRise.cs:577) w ArmouryBehavior :1517.
- `PeopleUnit.PerMan(v, legacy, out unit)` (Z:PeopleUnit.cs:86), `Shift` (:118 - jedyny zapis hearth za ludzi, `v.Hearth = set` :126), `GamePerMan = 0.5f` (:38).
- `PopulationLaw.KFor(Settlement)` (Z:PopulationLaw.cs:218 - k na osade z E4/3), `HearthPerMan` (:242, wola EnsureCalibrated), `EnsureCalibrated` (:232), `Calibrated` (:185).
  **Nie ma** `PeoplePerHearthOrWorld` z 113 (niepotrzebne - k_v ma kazda wies z tabeli; brak k = wies poza spustoszeniem).
- `ScorchedEarth.OnDaily` (Z:ScorchedEarth.cs:37-94): zerowanie z podloga 25 (:68), drain (:73-75), `Measure40.NoteHearth(SrcForage)` (:76), zboze 3.10 (:77-82).
  `HearthScarPostfix` (:97-120, +0.5 ponizej 40 :104-110). Brak `Reset` (lista `_villages` przezywa kampanie - 113 recenzja).
- `OutlawLaw.OnVillageLooted` (Z:OutlawLaw.cs:630-644, nasluch VillageLooted Z:ArmouryBehavior.cs:672), prywatne `Add` (:265-271), licznik `_inRaid` (:82, :641).
- `MaterialLaw.ProdPostfix` (Z:MaterialLaw.cs:163-193) na KAZDYM modelu produkcji (licznik glebokosci, dziala raz - w najbardziej zewnetrznym, NavalDLC), `OreLedger.NoteModel` :190.
- `WinterBite.VillageFoodPostfix` na Default `CalculateDailyFoodProductionAmount` (Z:WinterBite.cs:173-182, wpiecie :203-207).
- `Measure40` okno W6 na `RaidEventComponent.Update` (Z:Measure40.cs:94 Wire; `RaidPre` :418-436 prefiks First; `RaidFin` :438-455; hearth do "rabunek gry" :448-450),
  budzet `named` (:836-840), tekst linii Hearth (:847-862).
- `OthersRise.RaidPrefix` (Z:OthersRise.cs:302-316) - prefiks bool na ROT `OnRaidCompleted` (ROT:327-341, wighty = Hearth/2) - dzis blokuje (0 "do E4/5"); `Raise` (:174-192).

**E4/4 (109n) dolozy - zalozenia E4/5 (sprawdzic po E4/4, rozdz. 13):** `PopulationLaw.GrowthOn`, `GrowthPostfix` z `ScorchedEarth.RefugeeReturn(Village)` (109: wies poza Normal = 0,
blizna gasnie przy GrowthOn), sekcja `t:` w `PeopleBook`, linia "Ludzie: przyrost naturalny" (109 `GrowthDaily`) z "ruch ludzi" i RESZTA, podloga 10 (R17) schodzaca
"najpierw z UCHODZCOW tej wsi (od E4/5), potem z WYRZUTKOW regionu".

**113 - co bierzemy, co zmieniamy:**

| 113 (Devastation113.cs) | E4/5 |
|---|---|
| `Strike` :221-260 | zostaje rachunek trafionych; nowe udzialy (rozdz. 1); hearth przez `PeopleBook.Move`, konta przez `PeopleBook.Devastated`; zegar regionu; liczniki niewoli |
| `Room` :184-199, `Open` :202-213, `Covers` :149-154 | zostaja; k = `1 / PeopleUnit.PerMan(v, GamePerMan)` (ta sama stawka co Move); `Covers` = `On && unit` |
| `RaidPrefix`/`RaidFinalizer` :305-359, `Settle` :362-368, `StateOf` :287-303 | zostaja; dopisany pomiar lupu (kiesa wodza przed/po) - przejmuje go z W6 |
| `EndPrefix`/`EndFinalizer` :373-424 | **`Settle` przeniesiony z finalizera do prefiksu** (przed cialem metody, ktore rozsyla RaidCompleted) - zeby rajd Innych (K7) widzial zabitych calego rabunku |
| `LevelPostfix` :432-447, `FoodPostfix` :450-455, `YieldFactor` :160-181 | zostaja; e domyslnie 1.0; k z `KFor` (bez kalibracji w latkach modeli) |
| `ReturnRate`/`Daily` :473-534 | **zastapione**: zegar regionu + liniowo do 364 + smierc 1.3%/rok + blokada oblezenia; bez `PopulationLaw.Danger` w tempie |
| `Abandon` :540-576 | zostaje (wylaczone = wszyscy do domu nastepnej doby, przez Move MRefugeeHome) |
| `ExportSections`/`Import` :580-624, `_scars` | **przeniesione do PeopleBook** (`u:` + nowe `r:`) |
| `DayNote` :660-681, `RegionsNote` :689-750 | linia "Ludzie (spustoszenie)" z kontrolami (rozdz. 10); odcinek w "Ludzie:" tylko ludnosc z uchodzcami |
| `ApplyAll` :753-812 | + latka `TickProductions` (Looted); wpinane zawsze (U26) |
| PopulationLaw.Danger (113.diff PopulationLaw) | potrzebne do udzialu "las" - patrz rozdz. 13 pkt 4 |

---

## 3. Pliki i kolejnosc commitow

Kazdy commit buduje sie (`cd Armoury; dotnet build Armoury.csproj -c Release -v q --nologo`), osobny wpis CHANGELOG na koncu paczki (jeden wpis E4/5 z punktami K1-K7).

| Commit | Pliki | Tresc |
|---|---|---|
| K1 | PeopleBook.cs | konto UCHODZCY, zegar regionu, 4 przyczyny, zapis `u:`/`r:`, Reset, odcinek "konto UCHODZCY" w "Ludzie (ksiega)" (rozdz. 4) |
| K2 | NOWY Devastation.cs, ScorchedEarth.cs, OutlawLaw.cs, Measure40.cs, ArmouryBehavior.cs, SubModuleMain.cs, Settings.cs (+gen_mcm) | rachunek trafionych, latki rabunku, zerowanie, las do puli, pomijanie W6, wpiecia, klucze (rozdz. 5.1-5.4, 6) |
| K3 | Devastation.cs, ScorchedEarth.cs (RefugeeReturn z E4/4), PopulationLaw.cs albo miejsce podlogi z E4/4 | powrot liniowy, smierc, oblezenie, Abandon, koniec "+0.5", podloga 10 z uchodzcow (rozdz. 5.5) |
| K4 | Devastation.cs, MaterialLaw.cs | plon: YieldFactor, LevelPostfix, FoodPostfix, czynnik w ProdPostfix (rozdz. 5.6) |
| K5 | Devastation.cs, (VillageWoodlot.cs - decyzja rozdz. 15), (Measure40.cs:979 - opcja) | Looted produkuje: latka TickProductions (rozdz. 5.7) |
| K6 | Devastation.cs, PeopleLedger.cs, ZT\sprawdz_logi.py | linia "Ludzie (spustoszenie)", ludnosc z uchodzcami w "Ludzie:", 4 kolumny CSV, progi `etap4_e45` (rozdz. 10-11) |
| K7 | OthersRise.cs, Settings.cs (opis OthersRaidWights) | rajd Innych: wighty z zabitych przy spustoszeniu (tylko za Murem / przy Murze) - P E4/2 (b) "0 do E4/5, potem zabici" (rozdz. 5.8) |

---

## 4. PeopleBook - konto UCHODZCY (K1)

**Stale** (Z:PeopleBook.cs:62-73):
```csharp
internal const int ADom = 0, AOutlaws = 1, ACarts = 2, AService = 3, AOutside = 4, ARefugees = 5;          // UCHODZCY (E4/5): konto wsi pochodzenia
internal const int ... MIncident = 14, MDevRaid = 15, MDevForage = 16, MRefugeeHome = 17, MRefugeeFloor = 18, MKinds = 19;
// MName += "spustoszenie: rabunek (trafieni = zabici + pojmani + las + uchodzcy)", "spustoszenie: zerowanie armii (trafieni)",
//          "powrot uchodzcow do swojej wsi (E4/5)", "podloga 10 z uchodzcow (hearth dopisala gra/BK)"
// MHearth += true, true, true, false     // podloga: hearth zmienila gra (okno W1 E4/0 ja mierzy) - tylko licznik, bez drugiego zapisu
```
Sprawdzic, ze nic nie zaklada `MKinds == 15`: Measure40 bierze `PeopleBook.MKinds` (Z:Measure40.cs:687), UnitSegment petla po MKinds (:494) - OK.

**Stan (w zapisie):**
```csharp
private sealed class Ref { public double Away, Dead, Died; }                 // uchodzcy poza domem; zabici przy spustoszeniu; zmarli na koncie (od poczatku kampanii)
private static readonly Dictionary<string, Ref> _ref = new Dictionary<string, Ref>();       // id osady wsi -> konto
private static readonly Dictionary<string, int> _lastStrike = new Dictionary<string, int>(); // id warowni regionu (OutlawLaw.RegionFor) -> doba (int)CampaignTime.Now.ToDays ostatniego rabunku/zerowania z trafionymi > 0
private static volatile int _refCount;                                        // wsie z Away > 0 - szybka sciezka latek modeli (bez locka)
private static readonly object RefGate = new object();                       // osobna od Gate: latki modeli (GetHearthLevel) moga isc z watkow AI
```
**API** (kazda metoda z wlasnym try i `Stumble`, nie rzuca - U24):
```csharp
internal static double RefugeesOf(Village v);                 // 0 gdy _refCount == 0 (bez locka); inaczej lock(RefGate)
internal static int RefugeeVillages { get; }
internal static double RefugeesWorld();
internal static void RegionRefugees(Settlement region, out double away, out double dead);   // CSV PeopleLedger (BoundVillages regionu)
internal static void Devastated(Village v, double refugees, double dead, Settlement region, int day); // TYLKO po Move w Strike: Away += refugees, Dead += dead, _lastStrike[region] = day
internal static void NoteStrike(Settlement region, int day);  // zegar bez trafionych? NIE uzywac - zegar rusza tylko trafieni > 0 (rozdz. 15)
internal static bool LastStrike(Settlement region, out int day);
internal static double RefugeesHome(Village v, double men, int cause, float legacy); // zdejmuje min(men, Away); MRefugeeHome -> Move(v, ARefugees, ADom, n, cause, legacy) (hearth +n/k);
                                                                                     // MRefugeeFloor -> Note(n, cause, 0) (hearth juz dopisany); zwraca n
internal static double RefugeesDie(Village v, double men);    // Away -= n, Died += n; licznik doby (bez hearth - konto bez narodzin)
internal static void RefugeesSnapshot(List<KeyValuePair<string, double>> dst);  // id -> Away (> 0) dla Devastation.Daily i linii
```
Kolejnosc w `RefugeesHome` (U24): najpierw zdjecie z konta pod lockiem, potem `Move` (Move nie rzuca, zwraca hearth); jesli Move zwrocil 0 przy MRefugeeHome (wyjatek w Shift) -
oddac ludzi na konto (`Away += n`) i policzyc potkniecie - nikt nie znika.

**Liczniki doby (bez zapisu):** `_dRefIn` (z Devastated), `_dRefHome`, `_dRefFloor`, `_dRefDied`, `_dKilled`; zerowane w `Daily` razem z `_mMen` (Z:PeopleBook.cs:456-458).

**Zapis** (Z:PeopleBook.cs:102-175; format zgodny z dzisiejszym "c:"/"v:" - przecinki miedzy pozycjami, `R` dla double):
- `|u:<idWsi>=<Away>;<Dead>;<Died>,<idWsi>=...` - tylko pozycje z Away > 0 albo Dead > 0 albo Died > 0; pomijac id z `|`, `=`, `,`, `;`.
- `|r:<idWarowni>=<doba>,...` - tylko regiony, ktore maja jeszcze uchodzcow albo zegar mlodszy niz `RefugeeReturnDays` (porzadek w zapisie).
- `Import`: na poczatku `_ref.Clear(); _lastStrike.Clear(); _refCount = 0;` (przed `if (string.IsNullOrEmpty(data)) return;` - Z:PeopleBook.cs:134); smieci (NaN, ujemne,
  nieskonczone) = 0 (wzor 113 :610-611); ta sama wies dwa razy - ostatni wpis (113 :617).
- `Reset` (:92-99): to samo + liczniki. E4/4 doklada `t:` w tym samym Export/Import - scalic recznie (kolejnosc sekcji obojetna, Import ignoruje nieznane).
- Stary zapis (bez `u:`/`r:`): brak uchodzcow, zegarow brak (NK - od wgrania). Cofniecie do starszego DLL gubi `u:` (starszy Import pomija) - procedura w rozdz. 12 pkt 14.

**Odcinek w "Ludzie (ksiega)"** - w `Daily` po `UnitSegment()` (:448), przed `PeopleUnit.DayNote()` (:449), wlasny try:
`" | E4/5 konto UCHODZCY: " + N + " ludzi w " + W + " wsiach (" + R + " regionow) = wczoraj " + N0 + " + nowi " + a + " - powrot " + b + " - podloga 10 " + f + " - zmarli " + z
 + " (kontrola " + (N - N0 - a + b + f + z) + "); zabici przy spustoszeniu dzis " + k + ", od poczatku " + K + "; zegary regionow " + Z`.
`N0` - stan z konca poprzedniego odcinka (pole bez zapisu; pierwsza doba sesji: "pierwsza doba sesji - kontrola od jutra").

---

## 5. Devastation.cs (K2-K5, K7)

Szkielet jak 113 (Devastation113.cs:43-117): `internal static class Devastation`, `FloorHearth = 11f`, `GameStep = 0.05f`, `GameStepKeep = 0.975f`, `KRaid/KForage`,
flagi wpiecia `_raidPatched, _endPatched, _levelPatched, _foodPatched, _lootedPatched`, liczniki doby, `Stumble` (licz, pierwszy wyjatek miejsca do logu, nic nie gas).
Stanu w zapisie Devastation NIE ma (poza trwajacymi rabunkami - jak 113, bez zapisu).

### 5.1 Wlaczniki
```csharp
internal static bool On => s != null && s.DevastationEnabled && _raidPatched && _endPatched && PeopleUnit.On && PopulationLaw.GrowthOn;   // GrowthOn z E4/4 (113 :54-61)
internal static bool LootedOn => On && _lootedPatched && Settings.Current.LootedProducesByPeople;
internal static bool CaptivesLive => false;   // E4/9 podmieni na klucz RaidCaptivesFromVillage (P E4/9); do tego czasu pojmani = 0 (U14)
// Covers - rabunek, zerowanie, OnVillageLooted (kalibruje: pierwsza doba nowej kampanii, 113 pkt 12)
internal static bool Covers(Village v, out float per)   // On && PopulationLaw.EnsureCalibrated() && PeopleUnit.PerMan(v, PeopleUnit.GamePerMan, out unit) > 0 && unit
// CoversQuiet - z latek modeli i z RefugeeReturn (moga isc z watkow AI / z modelu hearth): bez kalibracji, k z PopulationLaw.KFor(v.Settlement)
internal static bool CoversQuiet(Village v)
```
k do rachunku = `1 / per` (per = stawka `PerMan` - ta sama, ktora `PeopleBook.Move` zdejmie; dla k < 2 stawka obcieta do 0.5 - spojnie z Move, Z:PeopleUnit.cs:86-95).

### 5.2 Strike (rdzen; 113 :221-260 przerobione)
```csharp
private static double Strike(Village v, double personDays, double perDay, int kind, bool slaver, RaidState rs /* null przy zerowaniu */)
{
    // s, v, personDays > 0, perDay > 0 (113 :223-224)
    float per; if (!Covers(v, out per)) { _dNoK++; return 0; }
    double k = 1.0 / per;
    double away, reach, open = Room(v, s, k, out away, out reach);           // 113 :184-199 (FloorHearth 11, pulap <= 95)
    if (!(open > 0)) { _dFull[kind]++; return 0; }
    double hit = Math.Min(open, (reach - away) * (1 - Math.Exp(-perDay * personDays / reach)));
    if (!(hit > 0) || double.IsNaN(hit)) return 0;
    double kill = Clamp01((slaver ? s.DevastationKilledSlaverPercent : s.DevastationKilledPercent) / 100.0);
    double capW = slaver ? Clamp01(s.DevastationCaptivesPercent / 100.0) : 0;     // "byloby" (log, E4/9)
    double cap  = CaptivesLive ? capW : 0;
    double U    = DangerOf(v);                                                    // 0..1, rozdz. 13 pkt 4
    double flee = OutlawLaw.On ? Clamp01((s.DevastationOutlawBasePercent + s.DevastationOutlawDangerPercent * U) / 100.0) : 0;
    if (kill + cap + flee > 1) flee = Math.Max(0, 1 - kill - cap);
    double dead = Math.Min(hit * kill, personDays);       // [113 KRYT 8] najwyzej 1 na zbrojnego na dobe; nadwyzka zostaje uchodzcami
    double capt = hit * cap, fled = hit * flee, refugees = hit - dead - capt - fled;
    // JEDYNY ruch hearth (U23): trafieni z DOM; nic miedzy Move a kontami (U24)
    double moved = -PeopleBook.Move(v, PeopleBook.ADom, PeopleBook.ARefugees, hit, kind == KRaid ? PeopleBook.MDevRaid : PeopleBook.MDevForage, PeopleUnit.GamePerMan);
    if (!(moved > 0)) { _dMoveFail++; return 0; }       // Move nie rzuca; 0 = wyjatek w Shift -> nikogo nie przenosimy
    double f = Math.Min(1.0, moved * k / hit);          // gdyby Move przyjal mniej (dno 0 hearth) - wszystkie konta w tej samej proporcji
    var region = OutlawLaw.RegionFor(v.Settlement); int day = (int)CampaignTime.Now.ToDays;
    PeopleBook.Devastated(v, refugees * f, dead * f, region, day);
    if (fled > 0) OutlawLaw.AddFled(v.Settlement, (float)(fled * f));     // pula regionu 1:1, bez hearth (zdjal Move)
    // pojmani (od E4/9): capt * f -> konto JENCY (E4/9); w E4/5 capt == 0
    if (rs != null) { rs.Hit += hit * f; rs.Dead += dead * f; rs.CapWould += hit * f * capW; }
    // liczniki doby: _dHit[kind], _dDead, _dFled, _dAway, _dCapWould, _dSlaverHit, _dVillages[kind]
    return hit * f;
}
```
`Room` jak 113 :184-199 z `k` z argumentu i `reach = MaxShare x (hearth x k + away)`; `Open(v)` (113 :202-213) - dla ScorchedEarth.

### 5.3 Rabunek - latki na RaidEventComponent (113 :276-424)
- `RaidState`: `PersonDays, TotalDays, Hit, Dead, CapWould, Virt, HasVirt, Steps, MaxMen, Slaver, Leader, GoldBefore`. Slownik `_raids` pod `_lock` (113 :78).
- `Slaver` liczony raz przy tworzeniu stanu: `att.LeaderParty.MapFaction` -> `LosersFlee.SlaveCulture(f.Culture)` (wzor Z:LosersFlee.cs:900); wyjatek = false + potkniecie.
- **`RaidPrefix(RaidEventComponent __instance, out Tick __state)`** - 113 :305-335 bez zmian rachunku: faza lupienia gdy `DefenderSide.TroopCount == 0` (R:162),
  `PersonDays += AttackerSide.TroopCount x CampaignTime.DeltaTime.ToDays` (ten sam czas, z ktorego gra liczy postep - G DefaultRaidModel.cs:46-49), podmiana `v.Hearth = rs.Virt`.
  Dopisac: `__state.Leader = att.LeaderParty.LeaderHero; __state.Gold = leader.Gold` (pomiar lupu przejety z W6).
- **`RaidFinalizer(Tick __state, Exception __exception)`** - 113 :337-359: `took = Virt - after`, przywrocenie `v.Hearth = real` (co do bitu), przy `took > 0` - `rs.Virt = after`,
  `Settle(v, rs)`; dopisac lup: `gold = leader.Gold - __state.Gold` (> 0 -> `_dLootAi`/`_dLootPlayer`), `_dGameHearth += took`, `_dGamePeople += took x k`.
- **`EndPrefix(RaidEventComponent __instance, out Tick __state)`** - 113 :373-390 z jedna zmiana: **najpierw `if (On && rs != null) Settle(v, rs)`** (czas od ostatniego kroku,
  takze rabunek przerwany przed pierwszym krokiem - na PRAWDZIWYM hearth), dopiero potem `__state.Real = v.Hearth` i podmiana na Virt. Powod: cialo `OnBeforeFinalize`
  (R:128-144) rozsyla `RaidCompleted` (R:138) - nasluchy (ROT/OthersRise K7, BKROT laska, BK jency, BEE, Diplomacy, NavalDLC) maja widziec hearth gry i komplet zabitych.
- **`EndFinalizer(RaidEventComponent __instance, Tick __state, Exception __exception)`** - 113 :392-424 bez `Settle` (jest w prefiksie): przywrocenie `Real + moved`
  (cudza zmiana w srodku zostaje przy wsi, licznik `_dForeign`), liczenie rabunku (`TotalDays > 0`: `_dRaids++`, `_dRaidMen += MaxMen`, Looted -> `_dRaidsFull++`), `finally` - `_raids.Remove`.
- `StateOf` - po wczytaniu w polowie rabunku Virt z `RaidDamage` (113 :287-303, `_dResumed`).
- `internal static double RaidDead(RaidEventComponent r)` / `RaidCaptives(RaidEventComponent r)` (pod `_lock`; 0 gdy brak stanu) - dla K7 i E4/9.

### 5.4 Zerowanie (ScorchedEarth -> Devastation.Forage)
```csharp
internal static void Forage(Village v, MobileParty mp)   // 113 :263-273; men = mp.MemberRoster.TotalManCount (jak dawny drain)
{ _dForagers++; _dDays[KForage] += men; Strike(v, men, Math.Max(0f, s.DevastationPerForagerDay), KForage, SlaverOf(mp), null); }
```

### 5.5 Doba: powrot, smierc, oblezenie (K3) - `Devastation.Daily()`
```csharp
internal static void Daily()      // ArmouryBehavior, zaraz po ScorchedEarth.OnDaily (rozdz. 8)
{
    if (!On) { Abandon(); return; }                                    // 113 :540-576 przez PeopleBook.RefugeesHome(..., MRefugeeHome, GamePerMan)
    if (PeopleBook.RefugeeVillages == 0) return;
    int today = (int)CampaignTime.Now.ToDays; int Q = clamp(s.RefugeeReturnQuietDays, 0, 360); int D = clamp(s.RefugeeReturnDays, Q + 1, 3640);
    double q = 1 - Math.Pow(1 - clamp(s.RefugeeYearlyLossPercent, 0, 100) / 100.0, 1.0 / Math.Max(28, CampaignTime.DaysInYear));
    foreach (id, away) in PeopleBook.RefugeesSnapshot:                  // kazda wies we wlasnym try
        v = ById(id) (113 :458-467, cache na kampanie, czyszczony w Reset); region = OutlawLaw.RegionFor(v.Settlement)
        died = away * q; PeopleBook.RefugeesDie(v, died); away -= died;
        if (!PeopleBook.LastStrike(region, out last)) { last = today - Q; _dNoClock++; }   // nie powinno sie zdarzyc (zegar razem z kontem) - liczyc
        d = today - last;
        if (d < Q) { _dStopQuiet++; continue; }
        if (region != null && region.IsUnderSiege) { _dStopSiege++; continue; }        // HR 2.8; rok sie nie lamie - wzor dogania
        back = away / Math.Max(1, D - d); if (away - back < 1) back = away;            // ostatni czlowiek caly
        got = PeopleBook.RefugeesHome(v, back, PeopleBook.MRefugeeHome, PeopleUnit.GamePerMan);
        kontrola liniowosci: _dLinErr = max(_dLinErr, |got - back| / max(1, back))      // 0 poza dnem konta
        liczniki: _dBack += got, _dBackVillages++, regiony w fazie powrotu
}
```
- **Koniec "+0.5 ponizej 40"** (E4/4: `ScorchedEarth.RefugeeReturn`, 109.patch :739-745): pierwsza linia po kontroli ustawien `if (Devastation.CoversQuiet(village)) return 0f;` (113.diff ScorchedEarth :374).
  `HearthScarPostfix` (Z:ScorchedEarth.cs:97-120) przy czynnym przyroscie wychodzi wczesniej (E4/4: `if (PopulationLaw.GrowthOn) return;`) - wiec "+0.5" zostaje tylko w RefugeeReturn.
- **Podloga 10 (R17, P :409)** - w miejscu, ktore zrobi E4/4 (finalizer W1 albo prefiks BK - rozdz. 13 pkt 3): ludzie dopisani przez podloge `men = dH_podlogi x k_v` schodza
  najpierw `PeopleBook.RefugeesHome(v, men, PeopleBook.MRefugeeFloor, ...)` (bez hearth), reszta z puli wyrzutkow (E4/4), reszta "podloga z niczego" (E4/4).
- `Abandon` - jedna linia logu przy pierwszym oproznieniu (113 :567-575), tekst bez zmian poza "regula +0.5" -> "jak przed E4/5".

### 5.6 Plon (K4)
- `YieldFactor(Village v)` (113 :160-181): fast path `PeopleBook.RefugeeVillages == 0` -> 1; `CoversQuiet`; `home = max(0, hearth) x KFor`; `m = (home / (home + away))^e`,
  `e = clamp(DevastationYieldElasticity, 0, 2)`; dno `(max(0.05, 1 - MaxShare))^e`; wyjatek -> 1 + potkniecie.
- **MaterialLaw.ProdPostfix** (Z:MaterialLaw.cs:163-193): po bloku mnoznika (:183-189), PRZED `OreLedger.NoteModel` (:190):
  ```csharp
  float hands = Devastation.YieldFactor(village);   // E4/5 R9a; E4/7 dolozy czynnik rak (b) w TYM SAMYM miejscu - jeden wspolny mnoznik (P 5.1)
  if (hands != 1f) { float sum = 1f + __result.SumOfFactors; if (sum > 0f) __result.AddFactor((hands - 1f) * sum, _txtRuin); }
  ```
  `_txtRuin = new TextObject("{=!}Armoury: hands lost to devastation (refugees not yet home)")` (napis w grze po angielsku - 113.diff MaterialLaw).
  Dziala raz (licznik glebokosci `_depth > 1` - :165), w najbardziej zewnetrznym modelu (NavalDLC, NV:77104-77131), po zimie i sufice BK na modelu Default.
- `FoodPostfix(Village __0, ref float __result)` na Default `CalculateDailyFoodProductionAmount(Village)` (G :76-88): `__result *= YieldFactor(__0)` (113 :450-455);
  obok `WinterBite.VillageFoodPostfix` (Z:WinterBite.cs:173) - oba mnoza, kolejnosc obojetna.
- `LevelPostfix(Village __instance, ref int __result)` na `Village.GetHearthLevel()` (G Village.cs:320-331): 113 :432-447 z `KFor` (bez kalibracji); tylko podnosi.

### 5.7 Wies Looted produkuje (K5) - NOWE wobec 113
**Dowod:** gra w `TickProductions` (G VillageGoodProductionCampaignBehavior.cs:138-154, potwierdzone p45\VGP.cs:141) produkuje tylko dla `!village.IsDeserted`, a
`IsDeserted => _villageState == VillageStates.Looted` (G Village.cs:81, p45\Village.cs:81) - spladrowana wies w ogole nie wola modelu; model Default i tak daje 0 poza
Normal (G DefaultVillageProductionCalculatorModel.cs:20, :78). `IsDeserted` nie wola zaden mod (skan 264 DLL w Modules, 11.10: brak "get_IsDeserted"); `TickProductions`
nie lata nikt (skan: brak nazwy w DLL modow). BK zastepuje tylko `TickGoodProduction` (BK EconomyPatches.cs:940-981, prefiks false) - wewnatrz TickProductions.

**Latka:** `VillageGoodProductionCampaignBehavior.TickProductions(Settlement, bool)` (private) - prefiks Priority.First + finalizer:
```csharp
private static readonly AccessTools.FieldRef<Village, Village.VillageStates> _state = AccessTools.FieldRefAccess<Village, Village.VillageStates>("_villageState");
private static int _mainThread;   // ManagedThreadId z ApplyAll (menu glowne = watek glowny)
public static void LootedPrefix(Settlement __0, bool __1, out Village __state)
{   __state = null;
    try { var v = __0?.Village;
          if (v == null || __1 || !LootedOn || _state(v) != Village.VillageStates.Looted || Thread.CurrentThread.ManagedThreadId != _mainThread || !CoversQuiet(v)) return;
          _state(v) = Village.VillageStates.Normal;    // POLE, nie wlasciwosc: setter rozsyla OnVillageBecomeNormal (G Village.cs:59-66) - tego nie chcemy
          __state = v; _dLootedOn++; (+ suma sztuk magazynu przed - pomiar) }
    catch (Exception e) { Stumble("Devastation.LootedPrefix", e); } }
public static Exception LootedFinalizer(Village __state, Exception __exception)
{   if (__state != null) { try { if (_state(__state) == Village.VillageStates.Normal) _state(__state) = Village.VillageStates.Looted; (+ sztuki po - przed) }
                           catch (Exception e) { Stumble(...); } }
    return __exception; }
```
Skutek: w srodku TickProductions wies jest Normal dla wszystkich (bramka magazynu 1.5 W, BK TickGoodProduction -> model NavalDLC -> Default z latkami BK EconomyLayer,
WinterBite, MaterialLaw; TickFoodProduction -> Default + WinterBite + FoodPostfix; postfiks `VillageWoodlot.TickPostfix` Z:VillageWoodlot.cs:198-205; ramki GoodsLedger
Z:GoodsLedger.cs:1095-1097), a plon x udzial ludzi w domu dolicza `YieldFactor` (5.6). Po wyjsciu - Looted (stan, postoj 8.3-16.7 doby G VillageHealCampaignBehavior.cs:16-31,
blokada ponownego rabunku - bez zmian). Inne wywolania modelu poza TickProductions (dymek, `GetIdealVillagerPartySize` G DefaultPartySizeLimitModel.cs:414-423,
Measure40 Z:Measure40.cs:979) dalej widza 0 dla Looted - opcja w rozdz. 15.

### 5.8 Rajd Innych (K7) - P E4/2 (b)
`OthersRise.RaidPrefix` (Z:OthersRise.cs:302-316), galaz `RaidBlocked`: zamiast samego `_raidWould` i `return false`:
```csharp
long dead = (long)Math.Round(Devastation.RaidDead(__1));            // zabici calego rabunku (Settle w EndPrefix - 5.3); 0 gdy spustoszenie wylaczone
int place = Place(st.GetPosition2D); var region = OutlawLaw.RegionFor(st);
long risen, pooled, burnt; Raise(mp, dead, region, place, PeopleBook.CRaid /*nowa przyczyna CIALA "rajd: zabici przy spustoszeniu"*/, out risen, out pooled, out burnt);
// BEZ PeopleBook.Die - ci zmarli sa juz w koncie Devastation (Dead wsi) - inaczej R3 liczylby ich drugi raz
_raidDead += dead; _raidRisen += risen; ...; return false;
```
`PeopleBook.CKinds` 5 -> 6 (`CName` + "rajd Innych: zabici przy spustoszeniu"); linia "Inni z poleglych (E4/2)" (b): "zablokowane N (ROT dalby X z niczego) -> zabici przy
spustoszeniu D, wstalo R, do puli P, spalone B". Opis MCM `OthersRaidWights` (Z:Settings.cs:1106): "Off (default) = a raid raises only the villagers killed in it, and only beyond
the Wall or at it (needs Devastation Enabled; without it nobody rises)".

### 5.9 ApplyAll (113 :753-812 + TickProductions)
Cele (wszystkie wpinane zawsze - U26; brak celu = "BRAK" w linii startowej, mechanika nieczynna, bez wyjatku):
`AccessTools.Method(typeof(RaidEventComponent), "Update", new[] { typeof(bool).MakeByRefType() })` (jak Z:Measure40.cs:94), `"OnBeforeFinalize"` (0 parametrow, DeclaringType ==),
`Village.GetHearthLevel` (int, 0 parametrow), `DefaultVillageProductionCalculatorModel.CalculateDailyFoodProductionAmount(Village)`,
`AccessTools.Method(typeof(VillageGoodProductionCampaignBehavior), "TickProductions", new[] { typeof(Settlement), typeof(bool) })`. `_mainThread` = biezacy watek.

---

## 6. Zmiany w istniejacych plikach (plik:linia w Z)

| Plik:linia | Zmiana |
|---|---|
| SubModuleMain.cs:137-139 | `try { Devastation.ApplyAll(_harmony); } catch (Exception e) { Log.Error("Devastation.ApplyAll", e); }` PO `PopulationLaw.ApplyGrowth` (E4/4, wpiety po :137) i po `OthersRise.ApplyAll` (:139). **Musi zostac w `OnBeforeInitialModuleScreenSetAsRoot` (:42-204)** - JIT wkleja `GetHearthLevel`/`IsDeserted`, jesli wolajacy skompiluje sie przed latka (113 ryzyko 13, recenzja R1) |
| ArmouryBehavior.cs:395 | konstruktor: `Devastation.Reset(); ScorchedEarth.Reset();` (113.diff :17; lista `_villages` ScorchedEarth to obiekty kampanii) - konto UCHODZCY czysci `PeopleBook.Reset()` (juz jest na tej linii) |
| ArmouryBehavior.cs:1432-1434 | po `finally { GoodsLedger.End(gfForage); }` (:1434): `try { Devastation.Daily(); } catch (Exception e) { Log.Error("Devastation.Daily", e); }` (po zerowaniu - zegar dzisiejszego zerowania juz stoi) |
| ArmouryBehavior.cs:1552 | po `PeopleLedger.Daily()`: `try { Devastation.DayLine(); } catch (Exception e) { Log.Error("Devastation.DayLine", e); }` - linia "Ludzie (spustoszenie)" i `NewDay()` w `finally` (PeopleLedger czyta liczniki przed zerowaniem) |
| ScorchedEarth.cs:37-94 | blok zerowania wedlug 113.diff ScorchedEarth (:325-361) na dzisiejszym kodzie: `bool dev = Devastation.On; if (dev) PopulationLaw.EnsureCalibrated();` (przed petla, :53); `:68 if (!dev && v.Hearth <= floor) continue;`; po warunku promienia (:71): `bool people = dev && Devastation.Covers(v, out _); if (dev && !people && v.Hearth <= floor) continue; if (people) { if (!Devastation.Open(v)) continue; Devastation.Forage(v, mp); } else { drain (:73-75) + Measure40.NoteHearth (:76) }` - **NoteHearth tylko na starej sciezce** (ruch ksiegi liczy Measure40 z PeopleBook); zboze 3.10 (:77-82) bez zmian (dalej z zapasu wsi - Leaks310.Forage) |
| ScorchedEarth.cs (nowe) | `internal static void Reset() { _villages = null; _playerForageShown = -1; }` (113.diff :315-316); `RefugeeReturn` (E4/4) - linia z 5.5; opis `ApplyAll` (:131-132) "+ przy czynnym spustoszeniu marsz pustoszy ulamek okregu w ludziach" |
| OutlawLaw.cs:634 | w `OnVillageLooted` po `if (!On ...) return;`: `if (Devastation.Covers(v, out _)) return;` (las liczy Strike - inaczej drugi raz; 113.diff OutlawLaw :404-409) |
| OutlawLaw.cs:644 (nowa metoda) | `internal static void AddFled(Settlement village, float men) { if (!On || village == null || !(men > 0f)) return; Add(RegionOf(village), Commoner, men); _inRaid += men; }` (113.diff :418-430) - bez hearth (zdjal Move). Linia "Wyrzutki" (:996-1006): "rabunki" = ludzie, "hearth za tych ludzi: rabunki" = 0 przy spustoszeniu - dopisac w tekscie "(przy spustoszeniu hearth w ksiedze: spustoszenie)" |
| Measure40.cs:418-436 `RaidPre` | po ustaleniu wsi (:430): `try { if (Devastation.CoversQuiet(st.Village)) { lock (Gate) _raidCovered++; return; } } catch { }` - krok pod spustoszeniem: hearth rusza tylko ksiega (MDevRaid), lup mierzy Devastation. Bez tego finalizer W6 liczy przywrocony hearth jako "rabunek gry" i ten sam ruch drugi raz z PeopleBook (albo smieci, jesli finalizer W6 pobiegnie przed naszym) |
| Measure40.cs:836-840 | `double o8 = mv[PeopleBook.MDevRaid] + mv[PeopleBook.MDevForage], o9 = mv[PeopleBook.MRefugeeHome];` i `named += o8 + o9`. MRefugeeFloor NIE do named (hearth podlogi jest w ticku W1) |
| Measure40.cs:857-862 | tekst: `" - rabunek gry -" ... + (pod spustoszeniem E4/5 krokow N - w ksiedze)"`; po zerowaniu: `", spustoszenie (E4/5: rabunek, zerowanie) " + S(o8) + ", powrot uchodzcow +" + F2(o9)`; "lup z niczego" = W6 + `Devastation.TakeLoot()` (przekazanie licznika - U8) |
| Measure40.cs:979 (opcja, rozdz. 15) | `food += Devastation.FoodToday(model, st.Village)` - model z ta sama podmiana stanu, gdy Looted (pomiar T9 nie gubi wsi spladrowanych) |
| MaterialLaw.cs:36-40, :183-190 | `_txtRuin` + czynnik (5.6); opis klasy (:27-32) + "E4/5: ten sam postfix mnozy plon x udzial ludzi w domu (uchodzcy)" |
| PeopleLedger.cs:40-42 | naglowek CSV + `;uchodzcy;spustoszenie_proc;zabici_spustoszenie;dni_od_spustoszenia` (NA KONCU, po kolumnach E4/4, jesli doloza) |
| PeopleLedger.cs:596-603 | wiersz: `PeopleBook.RegionRefugees(st, out a, out d)`; `spustoszenie_proc = 100 x a / (r.PeopleVillages + a)`; `dni_od_spustoszenia = LastStrike ? dzis - dzien : ""` (kazde w try - niepelny wiersz i tak nie trafia do pliku, :604) |
| PeopleLedger.cs:507-510 | po nawiasie "[PopulationLaw: ...] ... (27%)": `"; uchodzcy poza domem " + N + ", razem z nimi " + X + " mln (" + Sg(zmiana) + " ludzi)"` (113.diff PeopleLedger :61-67; `_lastAway` jak 113). **`StockNote` zostaje na koncu linii** (:545 - parsuje ja narzedzie) |
| OthersRise.cs:302-316, PeopleBook.cs:35-36 | K7 (5.8) |
| VillageWoodlot.cs:208 (decyzja rozdz. 15) | `n = RoundRandomized(PiecesPerDay(wood, v) x Devastation.YieldFactor(v))` - las wsi to tez praca rak (jedna regula plonu) |
| Settings.cs (po :692 albo po kluczach E4/4) + McmSettings.cs | 14 kluczy (rozdz. 9), opisy 5 starych (`ForageHearthPerDay`, `ForageFloor`, `RefugeeFloorHearth`, `OutlawRaidFleePercent`, `ScorchedEarthEnabled` - "used only with Devastation Enabled off" - 113.diff Settings), `OthersRaidWights`; `python tools/gen_mcm.py` |
| tools/gen_mcm.py RANGES | `DevastationMaxPercent (0, 95, "0")` (113 ryzyko 11: suwak do 360), procenty `(0, 100, "0")`, `DevastationYieldElasticity (0.0, 2.0, "0.00")`, `RefugeeReturnQuietDays (0, 120, "0")`, `RefugeeReturnDays (30, 1092, "0")`, `RefugeeYearlyLossPercent (0.0, 20.0, "0.0")` |

---

## 7. Latki - cele, sygnatury, kolejnosc z istniejacymi

| Cel (dekompilacja) | Dzis na celu | E4/5 | Kolejnosc / uwaga |
|---|---|---|---|
| `RaidEventComponent.Update(ref bool finish)` internal override (R:160-190; hearth :174-175, zloto `num x GoldRewardForEachLostHearth x mnoznik` :179-188) | Measure40 W6: prefiks First + finalizer Normal (Z:Measure40.cs:73-74, :94) | prefiks Normal `RaidPrefix(RaidEventComponent __instance, out Tick __state)`, finalizer Normal `RaidFinalizer(Tick __state, Exception __exception)` | W6 First czyta pierwszy i pomija wies objeta -> kolejnosc finalizerow obojetna. Cudzych latek brak (6 modow ma nazwe tylko jako nasluch RaidCompleted: BK, BKROT, NavalDLC, BEE, ROT, Diplomacy - skan 11.10) |
| `RaidEventComponent.OnBeforeFinalize()` protected override (R:128-144; stan :130-137, RaidCompleted :138); wolane z G MapEvent.cs:2138 po `OnMapEventEnded` :2079 | brak (nazwa "OnBeforeFinalize" w zadnym DLL modow) | prefiks `EndPrefix(RaidEventComponent __instance, out Tick __state)` (Settle, potem podmiana), finalizer `EndFinalizer(RaidEventComponent __instance, Tick __state, Exception __exception)` | nasluchy RaidCompleted i VillageLooted (setter stanu w srodku) widza hearth gry; OthersRise (prefiks na nasluchu ROT) widzi `RaidDead` |
| `Village.GetHearthLevel()` (G Village.cs:320-331) | brak latek (wolaja BK NotablePatches.cs:212-214, NavalDLC NV:80891, nasze VillageClimate.cs:207) | postfiks `LevelPostfix(Village __instance, ref int __result)` | latki w menu glownym (JIT) |
| `DefaultVillageProductionCalculatorModel.CalculateDailyFoodProductionAmount(Village)` (G :76-88) | `WinterBite.VillageFoodPostfix` (Z:WinterBite.cs:207); NavalDLC tylko owija baze (NV:77133-77136); model BEE nierejestrowany przy BK | postfiks `FoodPostfix(Village __0, ref float __result)` | mnozenie przemienne |
| `...CalculateDailyProductionAmount(Village, ItemObject)` (G :17-74) | MaterialLaw prefiks First/postfiks Last/finalizer na KAZDYM modelu (Z:MaterialLaw.cs:219-222), WinterBite postfiks (Default), BK EconomyLayer postfiks (Default, BK BKEconomyLayerInstaller.cs:99-142) | **bez nowej latki** - czynnik w `MaterialLaw.ProdPostfix` (depth 1) | E4/7 (b) w tym samym miejscu |
| `VillageGoodProductionCampaignBehavior.TickProductions(Settlement, bool)` private (G :138-154) | brak | prefiks First `LootedPrefix(Settlement __0, bool __1, out Village __state)`, finalizer `LootedFinalizer(Village __state, Exception __exception)` | wewnatrz: BK prefiks TickGoodProduction, VillageWoodlot postfiks, GoodsLedger ramki - widza Normal |
| ROT `ROTOthersCampaignBehavior.OnRaidCompleted(BattleSideEnum, RaidEventComponent)` (ROT:327-341) | OthersRise prefiks bool (Z:OthersRise.cs:114, :302) | zmiana ciala (K7) | wewnatrz OnBeforeFinalize - po naszym EndPrefix |
| `DefaultSettlementProsperityModel.CalculateHearthChange` | BK 400, BKROT 200, ScorchedEarth Last, E4/4 GrowthPostfix Last | bez latki; `RefugeeReturn` -> 0 | - |

Kolejnosc wpiec w `SubModuleMain`: ... WinterBite (:102) ... MaterialLaw (:68) ... VillageWoodlot (:134), ScorchedEarth (:137), [E4/4 ApplyGrowth], Measure40 (:138), OthersRise (:139),
**Devastation** - priorytety jawne (First/Normal), wiec kolejnosc ApplyAll na tych samych celach nie zmienia wyniku.

---

## 8. Kolejnosc w dobie (ArmouryBehavior.OnDailyTick, Z:ArmouryBehavior.cs:1418-)

1. Miedzy dobami (tick mapy): rabunki - `Strike` przy kazdym kroku gry i na koniec rabunku; zegar regionu.
2. :1433 `ScorchedEarth.OnDaily` - zerowanie (Strike KForage, zegar) -> **`Devastation.Daily`** (smierc, powrot - zegar widzi dzisiejsze zerowanie).
3. :1445 LosersFlee, :1507 OutlawLaw.Daily (pula: AddFled w "rabunki"), :1517 OthersRise.Daily -> `PeopleBook.Daily` ("Ludzie (ksiega)" z kontem UCHODZCY; zeruje liczniki ksiegi).
4. :1551 Measure40.Daily ("Hearth (E4/0)" - `TakeHearth40` z MDev*, MRefugeeHome), :1552 PeopleLedger.Daily ("Ludzie:" + CSV; E4/4 "przyrost naturalny"),
   **`Devastation.DayLine`** ("Ludzie (spustoszenie)", `NewDay`).
5. Spichlerz (E4/6) - po spustoszeniu, przed przyrostem nastepnej doby (P 5.2 pkt 5) - nie dotyczy E4/5.

---

## 9. Klucze (Settings.cs; napisy po angielsku; Jeff `Armoury.json` 11.10: zadnego z tych kluczy NIE MA - wejda domyslne z kodu; sa `ScorchedEarthEnabled: true`, `ForageMinMen 100`, `ForageRadius 3.0` - uzywane dalej)

| Klucz | Domyslnie | Uzasadnienie | Opis MCM (ang.) - skrot |
|---|---|---|---|
| `DevastationEnabled` | true | P E4/5; Jeff 05.10 "spalenie wsi to symbol - szereg wsi" | a burnt village stands for a whole countryside: raiders and foraging armies drive out only as many people as their numbers and days allow; a few die, a few take to the woods, the rest are refugees who come home within a year of the last raid in the region; the harvest falls with the share of people away. Needs People Unit Enabled and Natural Growth Enabled. Off = as before (refugees still away come home the next day) |
| `DevastationPerRaiderDay` | 0.5 | HR 2.1: 0.1-1.0 (1355: 0.28-0.95, 1319: 0.10-0.19) | people driven out by each raider for each day of looting |
| `DevastationPerForagerDay` | 0.2 | 113 (zerowanie zabiera mniej niz najazd palacy); pomiar w linii | people driven out by each soldier of a hostile army camped by the village for a day |
| `DevastationMaxPercent` | 90 | HR 2.7 (Yorkshire 1086, Normandia, Montmorency) | most of a countryside that can lie devastated at once (at most 95) |
| `DevastationKilledPercent` | 2 | HR 2.2 (La Capelle 1339 ok. 1%, Easingwold kilka % na najazd; 1-5%) | share of the people struck who are killed (never more than one per soldier per day) |
| `DevastationKilledSlaverPercent` | 5 | HR 2.2 (lowcy niewolnikow zabijali opornych mezczyzn) - **jedyny klucz spoza listy P :433-435** (parametr R8; inaczej stala w kodzie) | the same when the raiders' realm keeps slaves |
| `DevastationOutlawBasePercent` | 2 | HR 2.4 [PROJ] | share of the struck who take to the woods in a quiet land |
| `DevastationOutlawDangerPercent` | 3 | HR 2.4 (do 5% w dlugiej wojnie) | extra share at the worst danger (lawless town or castle, war, burnt villages) |
| `DevastationCaptivesPercent` | 10 | HR 2.3 (5-30%; 0.02-0.2 jenca/napastnika/dobe) | share of the struck carried off as captives by raiders whose realm keeps slaves - works only together with raid captives taken from the village (later package); until then they stay refugees |
| `RefugeeReturnQuietDays` | 30 | HR 2.5 (14-60 dob; "przy trwajacych najazdach nikt nie wracal") | no refugee goes home while a raid or foraging army struck the region in this many days |
| `RefugeeReturnDays` | 364 | Jeff 09.10 "odbudowa spalonej wioski: 1 rok" (SP:760), 11.10 (4) TAK; 364 = rok Armoury | after the quiet days refugees come home evenly, all of them by this many days after the last raid in the region (364 = one year) |
| `RefugeeYearlyLossPercent` | 1.3 | HR 2.5 (chrzty -35..-75%; dno przyrostu 1315-25), R1 korytarz -1.3 | share of refugees dying each year while away (no births on the road) |
| `DevastationYieldElasticity` | 1.0 | HR 2.6 (z ludzmi gina woly, ziarno, mlyny - Holy Island -87%, mlyny 1/3 po 13 latach); 113 mialo 0.5 | how hard missing people cut a village's output: x (people at home / people of the village) to this power; 1 = falls as fast as the people |
| `LootedProducesByPeople` | true | Jeff 07.10 "spladrowana wies produkuje wedlug tego, co zostalo" (SP:459-460) | a plundered village keeps working with the people who stayed instead of producing nothing for two weeks. Works only with Devastation Enabled |

Usuniete wobec 113 (nie wprowadzac): `DevastationOutlawPercent`, `RefugeeReturnPercent`.

---

## 10. Log

**Linia startowa** (ApplyAll): `Devastation (E4/5): spustoszenie jako ulamek okregu <CZYNNE|wylaczone w ustawieniach|NIECZYNNE - wymaga People Unit Enabled, Natural Growth Enabled i obu latek rabunku>
 - rabunek R1 i zerowanie R2 czlowieka na zbrojnego na dobe, pulap P%, zabici K% (przy niewoli Ks%), las B% + D% x niebezpieczenstwo, pojmani 0 (od E4/9; przy niewoli bylby C%),
 powrot po Q dobach spokoju liniowo do D dob od ostatniego najazdu w regionie (oblezenie wstrzymuje), smierc uchodzcow L%/rok, plon ^E, wsie spladrowane produkuja: TAK/NIE;
 wpiete: krok rabunku, koniec rabunku, progi produkcji, zywnosc wsi, produkcja wsi spladrowanej; BRAK: -.` (stan z ustawien startowych; prawdziwy - w linii dziennej).

**Linia dzienna** (`DayLine`, pisana gdy czynne i cokolwiek sie dzialo albo konto niepuste; parsuje `etap4_e45`):
```
Ludzie (spustoszenie): dzien N | CZYNNE | rabunki zakonczone R (spalone F, przerwane P), napastnikow srednio M, krokow gry S; osobodni lupienia X x 0.5 -> trafieni A;
gra zdjelaby G hearth = L ludzi ksiegi; lup z niczego Z zl (gracz z) | zerowanie: partii Q, osobodni Y x 0.2 -> trafieni B | okreg nasycony n razy |
trafieni T = zabici D + pojmani 0 + las O + uchodzcy U (kontrola +0.000) [najezdzcy z niewola: trafieni Ts, zabici Ds, pojmani byloby Cs] |
powrot: ludzi H do V wsi, regionow w fazie powrotu r; wstrzymany: 30 dob spokoju w1 wsi, oblezenie w2 wsi; kontrola liniowosci max e |
konto UCHODZCY: wczoraj W0 + nowi U - powrot H - podloga 10 f - zmarli z = N (kontrola +0.000) w v wsiach |
ludnosc wsi w domu X1 + uchodzcy N = razem X2 mln | plon: wsi z uchodzcami v, najnizszy mnoznik m (wies), wsi z >= 1/3 uchodzcow k (najwyzszy mnoznik wsrod nich mk) |
wsie spladrowane: Looted dzis L, produkowaly P (sztuk J) | regiony najbardziej spustoszone (8): <nazwa [rodzaj, kultura] uchodzcy a z b (c%), dni od najazdu d, plon x.., zabici od poczatku e>; ... |
bez przelicznika n | hearth dopisany przez kogos w srodku kroku rabunku f | rabunki podjete po wczytaniu g | potkniecia s | czas t ms.
```
Wylaczone: jedna krotka pozycja " | spustoszenie WYLACZONE (powod) - rabunek i zerowanie jak przed E4/5; na koncie jeszcze N uchodzcow (wroca jutro)" (113 :666-671).

**Inne linie:** "Ludzie (ksiega)" - odcinek UCHODZCY (rozdz. 4) i nowe przyczyny w "ruch dzis"; "Hearth (E4/0)" - "spustoszenie (E4/5)", "powrot uchodzcow", "pod spustoszeniem krokow N";
"Ludzie:" - "uchodzcy poza domem N, razem z nimi X mln (+-d)"; "Wyrzutki" - "rabunki" z AddFled; "Inni z poleglych (E4/2)" (b) - K7;
E4/4 "Ludzie: przyrost naturalny" - "ruch ludzi (..., spustoszenie i powroty uchodzcow)", RESZTA bez rabunkow (113.diff PopulationLaw :285-293 - wzor tekstu).
**CSV** `ludzie-regiony.csv`: 4 kolumny na koncu (rozdz. 6).

---

## 11. Test 120 dob (+ autotest 40 + zapis z doby 362 / 9 dob; wspolny z E4/4 - zasada Jeffa 11.10 03:05)

**Skala z pomiaru (LOG-A, zbior 30 bez E4/4-E4/5, d1-119; `p45\raid_stats.py`):** krokow rabunku gry 2 731 (= ok. 137 pelnych rabunkow), wsio-dni pod rabunkiem 617,
"rabunek gry" 21 036 hearth = **3.43 mln ludzi tabeli** (28.8 tys./d; d1-30 tylko 7% tego), lup z niczego 82 788 zl; zerowanie 505 razy (4.2/d), -141.6 hearth.
Szacunek E4/5: rabunek ok. 137 x 135 trafionych (113: pelny rabunek 112 ludzi = 270 osobodni) = **ok. 18 tys. (ok. 150/d)**; zerowanie: drain sredni 0.28 hearth/zdarzenie
-> ok. 175 ludzi/partie -> ok. 88 tys. osobodni x 0.2 = **ok. 18 tys. (ok. 150/d)**; razem ok. 0.09% ludzi wsi w 120 dob; zabici 2-5% (ok. 6-15/d), las 2-5%.

| # | Prog | Jak (narzedzie `--grupa etap4`, nowa funkcja `etap4_e45`) |
|---|---|---|
| S1 | 0 bledow naszych modow; potkniecia Devastation 0 (najwyzej 5 w biegu, z nazwa miejsca) | jak T1 |
| S2 | linia startowa: CZYNNE, "BRAK: -" (5 celow) | "Devastation (E4/5):" |
| S3 | kontrola trafionych = 0 kazdej doby (abs <= 0.01 czl.); pojmani = 0 (do E4/9) | "Ludzie (spustoszenie)" |
| S4 | kontrola konta UCHODZCY = 0 kazdej doby (w "Ludzie (spustoszenie)" i w "Ludzie (ksiega)" - ta sama liczba) | obie linie |
| S5 | "rabunek gry" w "Hearth (E4/0)" = 0.00 hearth kazdej doby przy CZYNNYM (wszystkie wsie objete); "pod spustoszeniem krokow" > 0 w dobach z rabunkami | Hearth |
| S6 | T2 bez pogorszenia: srednia RESZTA "Hearth (E4/0)" rozni sie od biegu E4/4 (bez E4/5) o <= 0.2 hearth/d; |RESZTA| <= 1 w >= 95% dob - jesli E4/4 to osiaga | Hearth (bazowy bieg E4/4 - `--baza`) |
| S7 | trafieni z rabunku 50-500/d (srednio d31-120; szacunek 150 - pas 3x); na pelny rabunek (kroki/20) 60-400 | linia dzienna; ponizej/ponad = sprawdzic okno, nie stroic na slepo |
| S8 | trafieni z zerowania 50-450/d (szacunek 150); **> 1 150/d (1% ludzi wsi rocznie) = `DevastationPerForagerDay` w dol** (113 ryzyko 2) - decyzja, nie blad | linia dzienna |
| S9 | zabici / trafieni 1.5-5% (okno); las / trafieni 2-5% przy prawie wyrzutkow; najezdzcy z niewola - zabici/trafieni ok. 5% | linia dzienna |
| S10 | powrot: 0 ludzi w regionach z najazdem w ostatnich 30 dobach (CSV: `dni_od_spustoszenia` < 30 => `uchodzcy(t) >= uchodzcy(t-1) x (1 - q)` - 0.5 czl.) | CSV |
| S11 | liniowosc: kontrola w linii `max e <= 1e-6`; CSV dla regionow z >= 1 000 uchodzcow, 30 <= d < 364, bez nowego najazdu: `u(t) / [u(t-1) x (1-q) x (1 - 1/(364-d))] - 1` w +-1%; regiony spalone w d1-d60 bez kolejnego najazdu - w d120 udzial z d+30 = (364 - d)/334 (np. 73.4% przy d = 119) +-1% | CSV |
| S12 | plon: kazda wies z >= 1/3 uchodzcow - mnoznik <= 0.67 (P "Skonczone, gdy" pkt 2); gdy takiej wsi nie ma w 120 dobach - INFO i kontrola wzoru: najnizszy mnoznik = 1 - udzial najslabszej wsi (+-1e-3) | linia dzienna |
| S13 | Looted produkuja: P >= 0.8 x L w dobach z L > 0 (reszta - magazyn pelny 1.5 W); "Towary" (GoodsLedger) - produkcja wsi nie spada razem z liczba wsi Looted | linia dzienna, "Towary" |
| S14 | Ruda/Drewno/Towary w d1 bez skoku (+-3% wobec biegu E4/4; w d1 brak uchodzcow, mnoznik 1) | "Towary", OreLedger |
| S15 | ludnosc: netto "razem z uchodzcami" d28-d120 w pasie progu E4/4 (290-1 150/d, P :398) albo nizej tylko o zabitych i zmarlych uchodzcow (SP 11.10 (4): w wojnie moze spadac) - **prog E4/4 liczyc z "razem z nimi", nie z samej ludnosci w domu** (inaczej uchodzcy ok. 280/d wyjda jako ubytek) | "Ludzie:" |
| S16 | lup z niczego (Hearth E4/0 + Devastation) w +-30% wobec LOG-A na krok rabunku (ok. 30 zl/krok) - co do monety jak w grze | Hearth, linia dzienna |
| S17 | K7: wighty z rajdu tylko za Murem / przy Murze; wstalo + do puli + spalone = zabici rajdow Innych (kontrola 0); poza R13 wstalo 0 | "Inni z poleglych (E4/2)" |
| S18 | T8: czas linii dziennej <= 3 ms/d; s/dobe biegu <= +3% wobec biegu E4/4 | linia dzienna, autotest |
| S19 | zapis 9 dob: konto UCHODZCY po wczytaniu = stan z doby zapisu (+- ruch jednej doby); zegary regionow wczytane ("zegary regionow" w Ludzie (ksiega)) | autotest zapisu |
| S20 | progi etapow 1-3 bez zmian (wojsko lordow w wojnie 95-115 tys., glowy < 5 000 <= 10, 0 bankructw, reszta ksiegi pieniadza < 10 tys./d) | `--grupa etap2/etap3` |
| S21 | wylacznik (autotest 40 dob z `DevastationEnabled=false` w probnej kopii `Armoury.json`): pierwsza doba - jedna linia "WYLACZONE ... wrocilo N", potem "rabunek gry" wraca, konto 0 | linia dzienna, Hearth |

Bieg 728 dob konca etapu (P rozdz. 4): wsie spalone w d1-d364, w ktorych regionie nie bylo kolejnego najazdu przez 364 doby -> >= 95% ludzi w domu, zywi uchodzcy 0.

---

## 12. Ryzyka i kolizje (kontrola calosci - CLAUDE.md 8 pkt 0)

**Regresje / ten sam stan**
1. **Podwojne liczenie rabunku w E4/0** - W6 (Z:Measure40.cs:418-455) + PeopleBook MDevRaid. Naprawa: pomijanie w `RaidPre` (rozdz. 6). Bez niej przy kolejnosci finalizerow
   Measure40 -> Devastation W6 zapisze `Real - Virt_po` (setki hearth smieci); przy odwrotnej - ten sam ruch dwa razy. Kontrola S5, S6.
2. **Zerowanie w E4/0** - `NoteHearth(SrcForage)` (Z:ScorchedEarth.cs:76) zostaje tylko na starej sciezce; nowa idzie przez PeopleBook (o8). Inaczej podwojnie w `named` (:836-840).
3. **"+0.5 ponizej 40"** - jedno miejsce (E4/4 `RefugeeReturn`); blizna gasnie przy GrowthOn (E4/4) - sprawdzic, ze po E4/4 `HearthScarPostfix` nie dopisuje juz +0.5 przy czynnym przyroscie (Z:ScorchedEarth.cs:104-110), inaczej E4/5 musi tam tez dopisac `CoversQuiet`.
4. **3% w las przy spaleniu** (Z:OutlawLaw.cs:630-644) - wylaczone dla wsi objetej; `_inRaid` dalej rosnie (AddFled) - T7 skladnik "pula z hearth (rabunki, bieda)" (Measure40 TakeOutlaw) dalej poprawny.
5. **Rzeczy, ktore szly za spadkiem hearth po rabunku, juz nie spadaja** (113 ryzyko 4): renta nalezna, milicja BK (Hearth/400, dec BKMilitiaModel :99), pojemnosc BK (Hearth x 4), tabor wsi,
   wartosc wsi dla AI, lup kolejnych rabunkow (kazdy ok. 4 zl x 0.4 x hearth gry - wies juz nie traci 40% na stale). Pomiar: S16, S20, ksiega pieniadza.
6. **Ludnosc w domu spada o uchodzcow** - PeopleOf (renty, Z:PopulationLaw.cs:196-205, :472), wiersze regionow PeopleLedger, `TablePeopleOf` - uchodzcy poza hearth; renta wsi spustoszonej nizsza
   o udzial uchodzcow (zaplacona i tak <= 20% kiesy). Prog E4/4 - S15.

**Kolizje z innymi zmianami**
7. **MaterialLaw.ProdPostfix** (Z:MaterialLaw.cs:163-193) - czynnik rak (b) z E4/7 MUSI isc do tego samego mnoznika (jeden czynnik, P 5.1), a (a) liczy tylko uchodzcow (U4) - bez podwojenia.
8. **WinterBite** (Z:WinterBite.cs:161-182) - ta sama zywnosc wsi; mnoza sie (P 5.1). Zima tnie tez produkcje wsi Looted (po podmianie stanu wynik > 0) - zamierzone.
9. **VillageWoodlot** (Z:VillageWoodlot.cs:198-205) - w TickProductions wies Looted jest Normal -> las wsi zacznie dawac drewno wsiom spladrowanym (dotad `_notNormal`). Spojne z "Looted produkuje";
   rozdz. 15 - pomnozyc przez YieldFactor.
10. **GoodsLedger** (Z:GoodsLedger.cs:1095-1097) - ramki na TickGoodProduction/TickFoodProduction policza produkcje Looted (wiecej "wsie" w "Towary") - pomiar, nie blad.
11. **VillageClogDiag / MarketCarts** (bramka 1.5 W) - wies Looted dostaje pojemnosc z produkcji Normal (GetWarehouseCapacity G Village.cs:248-257 w srodku podmiany) - spojne.
12. **E4/2 (b)** - K7 zmienia znaczenie `OthersRaidWights = false` (z "nikt" na "zabici, tylko R13"); bez K7 rajd Innych dalej 0 (bezpiecznie).
13. **E4/4 podloga 10** - "najpierw uchodzcy" wymaga konta z K1; jesli E4/4 juz ma krok "z uchodzcow" jako zaslepke - wypelnic, nie dublowac.
14. **Cofanie** - starszy DLL gubi `u:`/`r:` przy pierwszym zapisie. Procedura przed cofnieciem: `DevastationEnabled = false`, przeczekac dobe (Abandon - wszyscy w domu), zapisac, potem podmiana DLL.

**Spojnosc logiki**
15. **Zamknieta ksiega ludzi**: trafieni = zabici (smierc, R3; licznik `Dead` per wies do E4/7) + pojmani (0) + las (pula, 1:1) + uchodzcy (konto) - nic z niczego; powrot i podloga schodza
   z konta; smierc uchodzcow - zgon konta bez narodzin. Kontrole S3, S4.
16. **Jeden zegar na region** dla rabunku i zerowania (HR 2.5 "rabunek albo zerowanie"); "rok od ostatniego najazdu" (SP 11.10) liczony od tego samego dnia - zero dwoch zegarow.
17. **Gracz = AI**: rabunek i zerowanie gracza - ta sama regula (MainParty jest IsLordParty w ScorchedEarth; lup gracza jak w grze); jency z rabunku gracza - E4/9 (U22).
18. **Spirala na pograniczu** (P :439): powrot stoi, dopoki najazdy co < 30 dob; konto rosnie do pulapu, plon spada (dno 0.1). Pomiar: regiony > 25% uchodzcow i z zegarem < 30 przez > 90 dob - INFO w linii; decyzja Jeffa dopiero z liczbami.
19. **Male krainy** (113 ryzyko 12): okreg Nocnej Strazy 1.3-3 tys., Wolnych Ludzi 7-11 tys. - poczet 112 ludzi pustoszy 7.5% / 1.8%; pokretlo `DevastationMaxPercent`. Regiony tych krain beda na czele "najbardziej spustoszonych".
20. **Martwy kod staje sie zywy**: produkcja Looted to nowa praca (LOG-A: ok. 137 pelnych rabunkow / 120 dob x 8-17 dob = srednio ok. 10-20 wsi Looted naraz; P szacowal 30-80 w wojnie) - koszt znikomy (te same wywolania co dla Normal); towar w magazynie wsi rosnie do 1.5 W i nastepny rabunek ma wiecej do zabrania (R:204-250) - pomiar "lup towaru".

**Cudzy kod (sprawdzone w dekompilacji)**
21. Model produkcji czynny: NavalDLC owija Default (NV:77097-77137; BK i BEE swoich nie rejestruja - audyt-2026-10-09\02 rozdz. 1.1); produkcje towaru robi prefiks BK `TickGoodProduction` (BK EconomyPatches.cs:949-981) wolajac `Campaign.Current.Models` - podmiana stanu w TickProductions obejmuje go.
22. Podmiana `_villageState` - tylko pole (setter rozsyla zdarzenia - G Village.cs:51-79), tylko watek glowny (DailyTickSettlement z CampaignPeriodicEventManager - poza sekcjami TWParallel), tylko na czas jednego wywolania, przywracana w finalizerze (takze przy wyjatku). Zapis gry nie biegnie w srodku synchronicznego wywolania. Ryzyko: nasluch OnItemProduced w srodku widzi Normal (OreLedger, BK WorkshopData - nie czytaja stanu wsi).
23. JIT: `GetHearthLevel` i `IsDeserted` to male metody - latki musza byc zalozone przed pierwszym skompilowaniem wolajacych (menu glowne) - 113 ryzyko 13; po wgraniu: wies Strazy/Wolnych Ludzi z uchodzcami tuz pod 200 hearth - produkcja w dymku spada tylko o linie "hands lost to devastation", nie o polowe.
24. `CampaignTime.DeltaTime` (G CampaignTime.cs:66) - ten sam czas co postep rabunku (G DefaultRaidModel.cs:49); `MapEventSide.TroopCount` przelicza strone przy kazdym wywolaniu (G MapEventSide.cs:112) - prefiks woluje go 2x na tykniecie trwajacego rabunku (W6 tez) - koszt maly (kilka rabunkow naraz), mierzony S18.
25. Rabunek z milicja: najpierw starcie, potem osobne lupienie (113 przyczyna); przerwane lupienie zdejmowalo hearth bez VillageLooted - latka na kroku, nie na zdarzeniu (bez zmian).
26. BK `BKRaidCaptureBehavior.ExecuteCapture` (W11) dalej daje jencow z niczego do E4/9 - dlatego pojmani = 0 (U14); E4/9 dostaje `Devastation.RaidCaptives(raid)` = dokladnie te osoby.

---

## 13. Co zalezy od E4/4 (sprawdzic w kodzie E4/4 PRZED pisaniem; kazdy punkt - plik:linia w raporcie wykonawcy)

1. **`PopulationLaw.GrowthOn`** (nazwa i warunek) - `Devastation.On` go wymaga (113 :59). Inna nazwa -> dostosowac; brak -> E4/5 nie wchodzi.
2. **`ScorchedEarth.RefugeeReturn(Village)`** - czy jest i czy jest JEDYNYM miejscem "+0.5" (109.patch :739-745; blizna z `if (PopulationLaw.GrowthOn) return;` 109.patch :756). E4/5 dopisuje `CoversQuiet`.
3. **Podloga 10 (R17)** - gdzie E4/4 liczy "co dopisala podloga" (finalizer W1 `Measure40.VillageFin` Z:Measure40.cs:254-276 albo osobna latka) i w jakiej kolejnosci zdejmuje (uchodzcy -> wyrzutki -> "z niczego"); E4/5 podlacza `PeopleBook.RefugeesHome(v, men, MRefugeeFloor, ...)` jako pierwszy krok.
4. **Niebezpieczenstwo U** - czy E4/4 wydzielil je z `GrowthOf` (113 zrobil `PopulationLaw.Danger(Village, out security, out war, out burnt)` - 113.diff PopulationLaw :214-262, bez zmiany rachunku, proby co do bitu). Jesli nie - wydzielic tak samo (osobny maly commit przed K2) i sprawdzic, ze linia "przyrost naturalny" E4/4 daje to samo.
5. **Sekcja `t:` w PeopleBook** (Export/Import) - scalic z `u:`/`r:`; Import czysci wszystkie konta przed `IsNullOrEmpty`.
6. **Linia "Ludzie: przyrost naturalny"** (109 `GrowthDaily`, 109.patch :530-710) - skad liczy "ruch ludzi": jesli z sumy przyczyn PeopleBook z MHearth - nowe przyczyny wejda same; jesli z listy - dopisac MDevRaid, MDevForage, MRefugeeHome; RESZTA ma stracic "rabunki, zerowanie armii" (113.diff PopulationLaw :285-293).
7. **Prog E4/4 ludnosci** w `sprawdz_logi.py` - czyta "razem z nimi" z "Ludzie:" gdy jest (S15); E4/5 dopisuje odcinek - wspolny bieg z E4/4.
8. **Kolumny CSV E4/4** (jesli doloza) - E4/5 dopisuje swoje ZA nimi.
9. **Klucze E4/4 w Settings.cs** - klucze E4/5 wstawic za nimi (grupa MCM obok przyrostu - jak 113 "Iron bank").
10. **Wynik testu E4/4**: RESZTA "Hearth (E4/0)" (dzis +677 hearth/d w LOG-A - stala gry i model, T2 NIE) - S6 porownuje z biegiem E4/4, nie z LOG-A.

---

## 14. Poza E4/5 (zostawione z nazwa)

- E4/6: spichlerz okregu - uchodzcy jedza (P :447); uchodzcy z glodu (GL 2.6) dostaja miejsce pobytu - format `u:` rozszerzalny (`;miejsce`); powrot "rok od dnia, gdy spichlerz >= 90 dni";
  HR 2.5 "-20%/rok uchodzcow, gdy warownia gloduje albo oblezona" - decyzja E4/6 (glod); spichlerz czytajacy model produkcji musi widziec Looted przez te sama podmiane (rozdz. 15).
- E4/7: czynnik rak (b) w `MaterialLaw.ProdPostfix`; `Dead` wsi -> trwaly licznik POLEGLI (R3) regionu pochodzenia; menu osady "Refugees" (P :478).
- E4/9: pojmani (`CaptivesLive`, klucz `RaidCaptivesFromVillage`), BK `ExecuteCapture` = `Devastation.RaidCaptives`.
- E4/11: wioski poboczne - ten sam zegar regionu (`PeopleBook.LastStrike`) i ta sama regula roku.
- Etap 7.11: lup liczony od trafionych (ksiega pieniadza), rabunek osada po osadzie.

---

## 15. Drobne decyzje (rekomendacja wykonawcy; zadna nie jest pytaniem do Jeffa)

1. **Blokada oblezenia** (HR 2.8) - TAK, bez klucza: uchodzcy siedza w oblezonej warowni; liniowy wzor dogania po oblezeniu, wiec "rok" trzyma (oblezenie dluzsze niz rok -> wszyscy dzien po nim).
2. **Zegar od trafionych > 0** (nie od samej obecnosci armii): zerowanie wsi nasyconej (Open = false) nie rusza zegara - spojnie z tym, ze nie pustoszy.
3. **Las wsi (VillageWoodlot)** x YieldFactor - TAK (jedna regula plonu: "towar i zywnosc wsi"); jedna linia, wlasny try.
4. **Model poza TickProductions dla Looted** (dymek, Measure40:979, przyszly spichlerz) - w E4/5 tylko pomiar Measure40 przez `Devastation.FoodToday(model, v)` (ta sama podmiana, watek glowny); dymek i rozmiar taboru zostaja jak w grze (0 dla Looted) - mniej zmian w obcym kodzie.
5. **Killed slaver 5%** - klucz `DevastationKilledSlaverPercent` (widoczny parametr zamiast stalej); gdyby recenzja wolala zgodnosc 1:1 z lista P - stala 5 w kodzie z komentarzem HR 2.2.
