# Bledy — nasze, cudze i te, ktore sami lapiemy

Stan na 30.08.2026. Wszystko ponizej jest **potwierdzone w kodzie albo w logu**,
nie zgadywane.

---

## A. Bledy CUDZYCH modow, ktore latamy w CrashScribe (`src/Mends.cs`)

### A1. BKROTPatch dusil generowanie duchownych BannerKings — NAPRAWIONE

`BKROTPatch.Patches.ReligionGenerateClergymanPatch.Prefix` sprawdza kulture osady tak:

```csharp
MethodInfo mi = AccessTools.Method(type, "GetNameListForCulture", ...);
mi.Invoke(null, new object[] { __0.Culture, false });     // <-- Invoke(NULL)
```

`NameGenerator.GetNameListForCulture` **nie jest statyczna**. Kazde wywolanie konczy sie
`TargetException: Non-static method requires a target`, ich `catch` zwraca `false`
i **oryginalny `Religion.GenerateClergyman` nigdy nie leci**.

Skutek lawinowy: zadna osada nie dostaje duchownego → `ReligionData.Update` wywraca sie
na nullu → `PopulationData.Update` nigdy nie konczy → dane osad BK (ludnosc, milicja,
**ochotnicy do rekrutacji**) stoja martwe. W jednej sesji: **11 487 wyjatkow** i pusty
ekran rekrutacji.

**Nasza latka**: `Mends.Unhook()` zdejmuje ten jeden prefix z metody BK. Ich `Finalizer`
zostaje, reszta BKROTPatch nietknieta.

### A2. BKROTPatch przerywal inicjalizacje stylow zycia BK — NAPRAWIONE

`DefaultLifestylesInitializePatch.Prefix` zastepuje cala `DefaultLifestyles.Initialize`
(zwraca `false`) i wywraca sie w polowie na `NullReferenceException` (linia 190).
`BannerKingsConfig.Initialize()` nie dochodzi do konca → menedzery BK niekompletne.

**Nasza latka**: zdejmujemy ich prefix, robote konczy oryginal BannerKings.

### A3. Duchowni BK rodzili sie z obcej kultury — NAPRAWIONE

```csharp
Hero h = HeroCreator.CreateSpecialHero(preset, born, null, null, -1);
```

Kaplan dostaje kulture **presetu wiary**, nie osady, po czym BK sadza go w twojej wiosce.
A ochotnikow dobiera sie wedle kultury NOTABLA. Efekt w logu:

```
OSADA White Ranch | kultura osady: battania | [Greenseer Jarl / freefolk / Preacher]
OSADA Snowwood    | kultura osady: battania | [Septon Lyman  / vlandia  / Preacher]
```

czyli polnocne wsie wystawialy **wolny lud** i **Westerlands Noble Youth**.

**Nasza latka** (`Mends.LocalLevies`): przy wejsciu do osady i raz dziennie prostujemy
kulture notabla do kultury osady i zerujemy obce sloty ochotnikow. W logu:
`Mends: <imie> w <osada> byl freefolk, jest battania (Preacher).`

### A4. BannerKings: lawina NullReference w relacjach — ZALATANE BRAMAMI (30.08: zero wyjatkow)

Historycznie 37 601 NRE na sesje z okolic `HeroRelations`/`BKRelationsModel`.
Dwie galezie, obie juz obstawione (wpisy w CHANGELOG 26.08 i 28.08):

- **RelationsUpdateGate** (prefix na `HeroRelations.UpdateRelations`): odcina
  notabli bez osady/wlasciciela, zanim poleci NRE.
- **EssosTitleGate** (prefix na `BKRelationsModel.CalculateModifiers`): odcina
  bohaterow Essos bez tytulu osady.

Dowod z logu 30.08 (session-2026-08-30_07-11-12.log): "relacje notabl-lord bez
tytulu osady (Essos) - odciete 4000 razy, zero wyjatkow" oraz "pominieto
notabla bez osady/wlasciciela, lacznie 1501". **Sesja bez ani jednego NRE
z tego zrodla.**

Potencjalnie otwarta pozostaje trzecia galaz: `Kingdom.Leader` =
`RulingClan.Leader` rzuca NRE dla krolestwa bez klanu rzadzacego (linia 115
w `GetHeroesToUpdate`). Dzis NIE wystepuje (zadne krolestwo nie jest w tym
stanie); gdyby wrocila, latka to pominiecie krolestw bez wladcy w petli.

### A5. BKROTPatch: dwie latki bez celu — NIESZKODLIWE, nie ruszac

- `DynamicPartySizePerformancePatch.TargetMethod()` szuka moda **DynamicPartySize**,
  nie znajduje i **zwraca `null`** zamiast uzyc `Prepare() => false`. Harmony:
  `returned an unexpected result: null`.
- `RebellionsGameEntityInstantiatePatch` celuje w `"RebellionsAndDemographics..."`
  po nazwie tekstowej — mod nieobecny → `Undefined target method`.

~20 wyjatkow raz, przy ladowaniu. Zero kosztu w grze.

### A6. DTE: AmbiguousMatchException na ticku UI mapy — NIENAPRAWIONE (do decyzji)

`DynamicTroopEquipmentReupload.GUIExtensions.MapArmoryReadinessMixin` (widget
gotowosci zbrojowni na pasku mapy) -> `UIExtenderEx BaseViewModelMixin<MapBarVM>`
cctor -> `AccessTools2.Method` -> `Type.GetMethod` konczy sie
**AmbiguousMatchException** na 1.4.8 (niejednoznaczna metoda na MapBarVM).
Cctor pada raz, po czym KAZDE odswiezenie paska mapy (MapBarVM.Tick) rzuca
ponownie: **604 wyjatki w sesji 29.08 (session-2026-08-29_15-09-41.log:467),
8 w krotkiej sesji 30.08** — to obecnie najwiekszy zywy generator wyjatkow,
koszt budowy sladu stosu na ticku UI.

Gra nie pada (UIExtenderEx lapie), ale widget gotowosci najpewniej martwy.
**Ewentualny mend** (do decyzji Jeffa): wylaczyc odswiezanie tego jednego
mixina DTE — kosztem widgetu na pasku (przycisk otwierania zbrojowni ma
osobna droge). Ostroznie: ForgeView tez zyje na UIExtenderEx, mend nie moze
dotykac cudzych mixinow.

### A7. BK 1.4.8 - klasy latek pominiete przy starcie (BKROT/BK) - NIESZKODLIWE poza klasa 7 (decyzja Jeffa)

Zrodlo: diagnoza testu koncowego E4 d120 (B10, B10b, sprawdzone przez sceptykow), `rgl_log_20288.txt:4408-4414`
(14:47:01-14:47:02, BK `Main.OnGameStart`): `[BK] Skipped Harmony patches on <klasa>: HarmonyException: Patching
exception in method ...`. Te same 7 linii jest w rgl_log_35224 i 8228 oraz w biegach z31 (rgl_log_25684, 19560) -
stan sprzed etapu 4, NIE regresja E4. Raz na uruchomienie gry (BK `_patchesInstalled`), nie przy kazdym wczytaniu.
CrashScribe do 11.10 pokazywal z nich tylko pierwsza (`countChange`, session-2026-10-11_14-45-58.log:253), reszte
liczyl jako "(powtorka #5)" - patrz SCRIBE-KLUCZ-POWTOREK w CHANGELOG.

Mechanizm: BK naklada latki klasa po klasie, kazda w try/catch (`BannerKings/Main.cs:64-76`,
`CreateClassProcessor(type).Patch()`). Harmony 2.4.2 (ten sam 0Harmony.dll w Bannerlord.Harmony, RBM, BattlefieldImmersion)
w `PatchClassProcessor.PatchWithAttributes` robi zadania klasy po kolei (kolejnosc metod w metadanych); pierwszy blad
przerywa reszte KLASY (`ReportException` zawsze rzuca). Zadania przed bledem sa nalozone, po bledzie - nie. Nieudane
zadanie nie zostawia smieci w stanie Harmony (PatchInfo to kopia), wiec cudze i nasze latki na tych metodach dzialaja.

BKROT: dwie klasy z A5 (`DynamicPartySizePerformancePatch`, `RebellionsGameEntityInstantiatePatch`) - przy ladowaniu
modulu, nieszkodliwe.

BK (7 klas):

| # | Klasa BK | Blad w 1.4.8 | Co nie dziala | Wplyw |
|---|---|---|---|---|
| 1 | `TroopRosterSafetyPatch` | postfiks sledzenia `AddToCounts` ma parametr `countChange`, w 1.4.8 `count` (`TroopRoster.cs:428`) | tylko postfiks sledzenia (ostatnie zadanie klasy). 3 finalizery zabezpieczajace (`RemoveTroop`, `AddToCountsAtIndex`, `GetCharacterAtIndex`: IndexOutOfRange -> null/0) SA nalozone 3/3 | brak: sledzenie i tak wylaczone (`LogRosterMutationsTrace: false` w BannerKings.json). Nasz `ManLedger.LossPrefix` na `AddToCountsAtIndex` (parametry `__0/__1`) - bez wplywu. Po aktualizacji BK sprawdzic kolejnosc metod: jesli postfiks trafi przed finalizery, finalizery przepadna |
| 2 | `Diag.TraceMbEventClanInvoke` | `TargetMethod()` zwraca null (celowo) | diagnostyka BK | brak |
| 3-5 | `AiDecisionTracePatches+SetMovePatrolAroundSettlementPostfix`, `+SetMoveGoToPointPostfix`, `+SetMovePatrolAroundPointPostfix` | nowe sygnatury 1.4.8: `NavigationType` (w 1.4.8 `TaleWorlds.CampaignSystem.Party.NavigationType`, BK: `MobileParty.NavigationType`), `isTargetingPort` (BK: `isTargetingThePort`), `CampaignVec2 point` (BK: `Vec2 position`) | sledzenie decyzji AI i FreezeWatchdog BK dla tych 3 metod | brak na gre: prefiks i postfiks sa w jednym zadaniu i padaja razem (Enter/Exit sie nie rozjezdzaja); straznicy `GuardSettlementMove` sa w innych, nalozonych klasach |
| 6 | `DiplomacyPatches+DeclareWarVMPatch` | prefiks `CalculateWarSupport` z `ref int __result`, a metoda zwraca `TextObject` | cala klasa: 3 prefiksy ekranu dyplomacji gracza (`CalculateWarSupport`, `GetIsProposingWarEnabledWithReason`, `OnDeclareWar`) | tylko ekran gracza (`KingdomDiplomacyVM`): okno gry/Diplomacy zamiast okna BK (sojusz, rozejm, pakt handlowy z przycisku wojny). Uwaga: `ModCompat.DiplomacyMod` szuka Id `Diplomacy`/`DiplomacyFixes`, a u nas jest `Bannerlord.Diplomacy` - flaga jest FALSE, wiec gdyby latka weszla, BK przejalby ten ekran (nie oddalby go grze). Na AI, ekonomie i test brak wplywu |
| 7 | `VanillaModelTweakPatches+BKBattleRewardTweakPatches` | postfiks `CalculateInfluenceGain` ma `party`, `influenceValueOfBattle`, `contributionShare`; w 1.4.8 `winnerParty`, `influenceValueOfBattleForWinnerSide`, `contributionShareOfWinnerParty` (`DefaultBattleRewardModel.cs:73`; `:48` Renown - te same nazwy) | `CalculateInfluenceGain` pada, `CalculateRenownGain` nie zostaje juz nalozony (przerwanie; padlby i tak). `GetLootedItemFromTroop` (pierwsze zadanie) DZIALA | atuty BK bez dzialania: CommanderWarband +25% wplywu z bitwy, MercenaryFamousSellswords +20% slawy, styl zycia Cataphract +12% slawy (drugie efekty tych atutow z innych klas dzialaja). Lup: `LootScale: 0.5` w BannerKings.json -> ok. 50% lupionych przedmiotow znika w calym swiecie (dziala dzis i dzialalo w z31; wniosek z kolejnosci zadan, bez bezposredniego dowodu w logu) |

Decyzje i zasady:
- **B10b - przywrocenie atutow z klasy 7: ZROBIONE 14.10 (decyzja Jeffa "tak napraw") - CrashScribe `BkBattlePerks.cs` (galaz w-toku/zbior-e8 e0b2ccb1), klucz BkBattlePerksFix, grupa progow b10b.** Dawny opis: Zmienia ekonomie wplywu i slawy AI w calym
  swiecie, wiec nie robic tego przy okazji.
- Gdyby latka w CrashScribe Mends: TYLKO postfiksy na `DefaultBattleRewardModel.CalculateInfluenceGain` /
  `CalculateRenownGain` z nazwami `winnerParty` / `contributionShareOfWinnerParty` albo pozycyjnymi `__0` / `__2`.
  NIGDY ponowne `CreateClassProcessor` na klasie BK (podwoilby postfiks lupu: 50% -> 25%). Przed nalozeniem
  `Harmony.GetPatchInfo` - czy nie ma juz postfiksu wlasciciela "BannerKings" (po poprawce w BK bonus liczylby sie
  podwojnie). Lancuch modeli w grze: `NavalDLCBattleRewardModel` -> `BaseModel = DefaultBattleRewardModel`
  (Armoury-2026-10-11_14-45-59.log l.368 i 567), wiec latka na Default zadziala. Wpis w CHANGELOG jako zmiana ekonomii.
- Brakujacego postfiksu z klasy 1 NIE nakladamy (sledzenie wylaczone, goraca sciezka `AddToCounts`). Linia kontrolna
  "finalizery 3/3" - dopiero przy aktualizacji BK: po `OnGameStart` BK (nie w `OnSubModuleLoad` - falszywe 0/3), cele
  przez `AccessTools.Method` z dokladnymi typami, liczyc Finalizers z owner "BannerKings", tylko odczyt, raz na sesje.
- Sygnal regresji po aktualizacji BK albo gry: inna liczba albo nowe nazwy linii `[BK] Skipped Harmony patches on`
  w rgl_log (dzis 7, lista wyzej). CrashScribe od SCRIBE-KLUCZ-POWTOREK (11.10) pokazuje kazda klase osobno
  (2 bloki: blad wewnetrzny + otoczka HarmonyException).

---

## B. Bledy NASZE, ktore juz naprawilismy (nie powtarzaj ich)

| Blad | Objaw | Przyczyna | Naprawa |
|---|---|---|---|
| Tier o jeden za maly | luk t6 kuty z materialu t5 | `ItemTiers.Tier1 == 0` | `Recipes.Grade()` |
| Podwojne liczenie materialu | przetop dawal wiecej niz kosztowalo kucie | BK polowil, my nie | wspolne `ArmourUnits` |
| Swiezy pancerz ze zuzyciem | nowa zbroja miala 14% | ksiega zuzycia per SLOT | klucz po `StringId` |
| AI lazilo bez celu | armie chodzily tam i z powrotem | `SetMoveModeHold()` kasuje rozkaz | zapis i oddanie rozkazu o switku |
| Pompa zlota na koniach | 3,7 mln zlota, 5905 koni w 20 min | kupowanie % sily zamiast realnej potrzeby | zakup tylko pod awanse + cooldown |
| Ucieczka a niewola | uciekl konno, trafil do niewoli | zly test (`IsWounded`) | znacznik `Mission.RetreatMission` |
| Jedzenie ze zuzyciem | ryba i maslo mialy "(14%)" | Spoils of War daje modyfikatory wszystkiemu | `IsGoods()` + `CleanseAmmo` |
| Znikajacy namiot | oboz bez namiotu do konca sesji | `catch { _tentBroken = true; }` — globalny wylacznik | (cofniete, do zrobienia na nowo) |
| CTD w menu obozu | crash przy "Rouse the men early" | `SwitchToMenu` z wnetrza opcji menu oczekiwania | `GameMenu.ExitToLast()` |
| CTD przy obozowaniu band | crash w `GameMenuVM.OnFrameTick` | setki namiotow AI odswiezanych co tick | (cofniete) |

---

## C. Jak czytac log CrashScribe

Blok bledu wyglada tak:

```
# ERROR CAUGHT FURTHER UP   2026-08-25 15:21:02
WHERE   : FirstChanceException
BLAME   : SandBox                 <- ktory mod/assembly obwiniamy
TYPE    : System.NullReferenceException
MESSAGE : ...
--- GAME STATE ---                <- data, bohater, zloto, osada, MENU (bardzo wazne)
--- STACK ---
--- CALLER (zywy stos watku) ---  <- prawdziwy lancuch wywolan
```

Na koncu sesji jest `SUMMARY` z licznikami — tam widac, co sypie tysiacami.

**Wskazowka**: jesli szukasz, czy blad jest nowy, przeszukaj WSZYSTKIE stare logi:

```bash
for f in session-*.log; do echo "$f : $(grep -c 'SZUKANA_SYGNATURA' "$f")"; done
```

Tak wlasnie ustalilismy, ze crash w `GameMenuVM.OnFrameTick` byl nasz: zero w 21 starych
sesjach, dwie w tych po zmianie.

## Pulapka: Harmony na klasie, ktorej cctor zaglada do Game.Current (14.09.2026)

`DefaultClanFinanceModel` inicjuje pola statyczne przez
`Game.Current.GameTextManager.FindText(...)`. Zalozenie latki Harmony na JAKAKOLWIEK
jej metode sprawia, ze wejscie przez wrapper `_Patch1` odpala konstruktor statyczny
wczesniej niz robi to oryginalne cialo. Na sciezce wczytywania zapisu ROT tworzy ekran
mapy zanim `Game.Current` stoi (`RealmOfThronesGameModeManagerPatch.Prefix` ->
`GameStateManager.CreateState` -> `MapScreen..ctor` -> NavalDLC `MapInfoVM`), cctor
rzuca `NullReferenceException`, a .NET zapamietuje typ jako martwy na stale.
Skutek: `TypeInitializationException` przy kazdym dostepie i CTD przy wczytywaniu.

Kosztowalo: jedna sesje Jeffa (14.09). Pelny slad w CHANGELOG.md pod haslem
"COFNIETE: latka na ujemny zold najemnika".

Regula: zanim zalatasz metode cudzej/vanillowej klasy, sprawdz jej konstruktor
statyczny. Jesli siega do `Game.Current`, `Campaign.Current` albo menedzerow gry -
albo znajdz inne miejsce zaczepienia, albo wymus `RuntimeHelpers.RunClassConstructor`
w chwili, gdy te obiekty NA PEWNO istnieja (`OnSessionLaunched`).

---

## Zachowania cudzych modow, ktore wygladaja na bledy, a NIE sa (16.09.2026)

### Karawana Tyriona "nic nie zarabia" - Banner Kings Redux, ustawienie Realistic Caravan Income

`BannerKings.Patches.EconomyPatches.AddIncomeFromPartyPrefix`: dla partii `IsCaravan` zwraca
`!RealisticCaravanIncome`, czyli przy wlaczonym ustawieniu (Jeff: `BannerKings.json` ->
`"RealisticCaravanIncome": true`) vanillowy dzienny dochod z karawany
(`(PartyTradeGold - 10000) / 10`) jest CALKOWICIE pomijany. Opis ustawienia w BK: "caravan
profits will only be added when they enter a settlement owned by their owner, or where they are
situated (ie, notables)" - rod bez wlasnego miasta nie zobaczy z karawany ani grosza.
`BKClanFinanceModel.CalculateOwnerIncomeFromCaravan` tez zwraca 0 w tym trybie.
Wyjscie: MCM -> Banner Kings -> Economy -> Realistic Caravan Income = OFF (RequireRestart=false),
wtedy wraca dzienny dochod vanilla, gdy zloto handlowe karawany przekroczy 10000.

### "Blad" przy propozycji malzenstwa Jonowi Snow - to przysiega Nocnej Strazy z ROT

`ROT.dll`, tekst `{=ROTVxjbPNMV6N}`: "I shall take no wife, hold no lands, father no children.
I shall wear no crowns and win no glory. I shall live and die at my post." - ROT tak odmawia
malzenstwa czlonkom Nocnej Strazy. Po tej linii ROT nie ma dalszej sciezki dialogu, wiec nasz
CrashScribe.DialogEscape wystawia "Let us talk" / "Farewell" (log 16.09 08:18-08:21: "rozmowa
uratowana (1)..(3)"). Nie ma tu wyjatku w logach - to zamierzone zachowanie ROT.

## Wywrotka silnika przy wyjsciu z gry (TaleWorlds.Native, rozpoznanie 21.10)
- Objaw: po "Wyjdz" gra pada w kodzie natywnym silnika (0xC0000005, rzadziej 0xC0000094) w TaleWorlds.Native.dll +0x74DFFF..0x74E2B1, 5-25 ms po linii rgl_log "Managed Interface deleted." (Final_cleanup). Okno bledu silnika/WER; w autotescie proces wisi do ubicia (90 s), a straznik CrashScribe pisze hang-*.log "outside a campaign" (to echo, nie zawieszenie).
- Czestosc: ok. 55-57% wyjsc (autotesty 15-21.10: 76 ze 133), od 23.08 (najstarszy slad hang 28.08, Event Log 13.09), takze w sesjach Jeffa (LauncherEx), bez zwiazku z wgraniami 9x-101 ani z lista modow (MoreBloodPoolMod, VoiceActingPatchRemake).
- Mechanizm (prawdopodobny): zwalnianie juz zwolnionej pamieci w alokatorze silnika przy koncowym sprzataniu; winny obiekt nieznany (brak symboli), kod zarzadzany juz zakonczony.
- Szkody: brak - przychodzi PO zapisie gry, "Saving options" (engine_config safely_exited=1) i zapisie logow; skan zer czysty. Brak zwiazku z zerowaniem plikow na dysku C.
- Mozliwe: (1) CrashScribe Watchdog - flaga zamykania w OnSubModuleUnloaded (bez Suspend i bez "GAME HANG"), osobny limit hang-*.log w Prune; (2) autotest - po QuitGame wykrywac wpis w rgl_log_errors_<pid> i zamykac od razu (oszczednosc 60-80 s); (3) opcjonalnie (decyzja Jeffa) TerminateProcess po zapisie opcji, zeby nie bylo okna bledu.
- Dowody: wf_096230a7-115 (journal), C:/ProgramData/Mount and Blade II Bannerlord/logs/rgl_log_errors_*.txt, C:/Users/GAME/AppData/Local/CrashDumps/Bannerlord.BLSE.*.dmp.
