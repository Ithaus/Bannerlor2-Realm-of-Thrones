# Bledy — nasze, cudze i te, ktore sami lapiemy

Stan na 25.08.2026. Wszystko ponizej jest **potwierdzone w kodzie albo w logu**,
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

### A4. BannerKings: 37 601 NullReference na sesje — NIENAPRAWIONE (do decyzji)

`BannerKings.Behaviours.Relations.HeroRelations.GetHeroesToUpdate()`:

```csharp
foreach (Kingdom k in Kingdom.All)
    hashSet.Add(k.Leader);          // <-- linia 115
```

`Kingdom.Leader` = `RulingClan.Leader`. Krolestwo bez klanu rzadzacego (zniszczone,
swiezo zbuntowane) → getter rzuca NullReference. Leci to **dla kazdego bohatera**,
a kazdy przelatuje **wszystkie krolestwa**. Jedno zepsute krolestwo = tysiace wyjatkow
na godzine. Koszt: budowa sladu stosu przy kazdym rzucie.

Nasz `Mends.SafeRelations` lapie skutek dalej (`BKRelationsModel.CalculateModifiers`),
ale rzut i tak nastepuje.

**Proponowana naprawa**: latka na `GetHeroesToUpdate`, ktora pomija krolestwa bez wladcy
i raz loguje, ktore to jest. **Nie zrobiona — czeka na zgode Jeffa.**

### A5. BKROTPatch: dwie latki bez celu — NIESZKODLIWE, nie ruszac

- `DynamicPartySizePerformancePatch.TargetMethod()` szuka moda **DynamicPartySize**,
  nie znajduje i **zwraca `null`** zamiast uzyc `Prepare() => false`. Harmony:
  `returned an unexpected result: null`.
- `RebellionsGameEntityInstantiatePatch` celuje w `"RebellionsAndDemographics..."`
  po nazwie tekstowej — mod nieobecny → `Undefined target method`.

~20 wyjatkow raz, przy ladowaniu. Zero kosztu w grze.

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
