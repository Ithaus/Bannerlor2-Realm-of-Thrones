# PLAN KODU - paczka E4/10 (167): weterani - karczma tylko z puli WETERANI, zwolnieni 2/3 do domu i 1/3 do karczmy, niewynajety po 120 dobach do domu, start z pula starych zolnierzy

Status: SAM PLAN (11.10.2026). Kod, gra, ustawienia, zapisy nietkniete; nic nie budowane, nic nie commitowane.
Wiazace: `docs\etap4\PROJEKT-ETAP4-KSIEGA-LUDZI-ZIMA-2026-10-10.md` v2 (dalej P; E4/10 = P rozdz. 3 :522-527, R0/R6/R16 w rozdz. 2 :116-166,
progi rozdz. 4 :536-554, ryzyka 5.1-5.4), `docs\etap4\KRYTYKA-E4.md` (KR: U1 - konto WETERANI w rownaniu, U15c - niewynajety wraca, U20 - wojsko),
decyzje Jeffa w `docs\STAN-PRAC.md`: 05.10 "Najemnicy z karczmy tylko sposrod zwolnionych z wojska" (SP:352), 08.10 kon najemnika - "system zamkniety,
zeby konie nie pojawialy sie magicznie" (SP:656-660), 08.10 "karczmy na start z pula starych zolnierzy - tak" (SP:711), 11.10 "kraina bez wolnych rak
nie wystawia nowego wojska do odrobienia strat - TAK; ludnosc w ciaglej wojnie moze spadac (do -0.5%/rok) - TAK" (SP:1137-1138), 09.10 C "mniej wojska
AI - NIE" (SP:771). Wzor formatu: `docs\etap4\PLAN-KODU-E47.md` (dalej P47). E4/10 wchodzi PO E4/7 (P rozdz. 3 tabela: "Zalezy od E4/7").

Skroty miejsc:
- **B:** = kod bazowy zbioru 31 (`git -C scratchpad\e4\46 show dfb12df:Armoury/src/<plik>`; kopia plikow zmienianych przez E4/6 w toku: `scratchpad\e410\base\`).
  Pliki niezmienione od dfb12df (ClanBudget, LosersFlee, OutlawLaw, PrisonerLaw, LevyGold, RecruitKit, RecruitCost, PeopleLedger, OverflowHome, GentryService,
  CrownGifts, Undead, VillageTakings, SpoilsCompany, CirculationWindows) - sprawdzone `git diff --quiet dfb12df` - linie z drzewa `scratchpad\e4\46\Armoury\src`.
- **G:** = `scratchpad\dec32\cs_full\` (TaleWorlds.CampaignSystem 1.4.8); **RCB** = G:`TaleWorlds.CampaignSystem.CampaignBehaviors\RecruitmentCampaignBehavior.cs`.
- **BK:** = `scratchpad\e4tools\bkfull\`; **SB:** = `scratchpad\e410\sandbox\` (ilspycmd SandBox.dll); **DTE:** = `scratchpad\e410\dte\` (ilspycmd DTE 1.4.7);
  **ROTd:** = `scratchpad\dec37\rot\` (ROT.dll); **BEE:** = `scratchpad\e4\dec\BetterEconomy.all.cs`; **CS:** = `scratchpad\e4\46\CrashScribe\src\`.
- **ROTx:** = `Modules\ROT-Content\ModuleData\spcultures.xml`; **SBC:** = `Modules\SandBoxCore\ModuleData\spnpccharacters.xml`; **ROTt:** = ROT `ROT-Troops.xml`.
- Baza liczb: bieg testowy zbioru 30 (`scratchpad\kopia-z30-A\Armoury-2026-10-11_04-36-46.log`, 120 dob) = **z30A** (srednie z linii, skrypt w tej sesji).

---

## 0. Streszczenie decyzji planu (co wykonawca robi, czego nie)

1. **Wlasciciel stanu = `PeopleBook` (U23).** Konto WETERANI w nowym pliku `PeopleBook.Veterans.cs` (`partial` - E4/7 robi `PeopleBook` partial, P47 0.1).
   Mechanika karczmy (latki Harmony, synchronizacja, typ najemnika, okna najmu) w osobnym `Veterans.cs`. Stan tylko w `arm_people` (sekcje `vs:`, `vt:`, `vh:`).
2. **Pula = miasto x klasa.** Klasa weterana = (konny tak/nie, tier oddzialu). Na klase: liczba ludzi, udzialy pochodzenia (region -> liczba, jak P47 2.1) i kolejka
   FIFO (doba wejscia, ludzi) - dokladnie 120 dob czekania, bez koszykow.
3. **Wejscie - jedna regula dla wszystkich zolnierzy wracajacych zywych ze sluzby do domu** (P R16: "zwolnieni, rozbici, wypuszczeni jency"):
   zwolnieni 166, przepelnienie W4c, rycerze 179, rozbici H3, uwolnieni bez odbiorcy, wypuszczeni i zostawieni jency (I1), rozwiazane partie (E4/7 4.3).
   Z kazdego strumienia **dokladnie `VeteranShareToTavern` (0.33)** uprawnionych idzie do karczmy najblizszego miasta (akumulator ulamka na strumien - bez losowania),
   reszta do domu jak dzis (E4/7 `HomeOrNote`). Nie wchodza: dezerterzy (R6 - do lasu), powrot z puli wyrzutkow (SrcPool - to juz wyrzutki), Straz, Inni,
   tabory/lodzie, milicje, karawany, bandyci i chlopi, bohaterowie.
4. **Prog tieru `VeteranMinTier` = 3 (ODSTEPSTWO od litery "1/3 zwolnionych", z powodem - pytanie 1).** Najnizszy najemnik karczmy w ROT ma tier 3
   (poziom 16; G:DefaultCharacterStatsModel.cs:18-25 tier = ceil((poziom-5)/5); SBC/ROTt drzewa w rozdz. 4.4). Rekrut tieru 1-2 w karczmie stalby jako tier 3 -
   awans bez cwiczen (ludzie sa policzeni, ale umiejetnosci z niczego). 166 zwalnia od najnizszego tieru (B ClanBudget.cs:729 sortowanie), wiec bez progu karczmy
   sprzedawalyby glownie podniesionych rekrutow. Rekrut ponizej progu idzie w calosci do domu.
5. **Karczma pokazuje tylko weteranow.** Prefiks `bool` na `UpdateCurrentMercenaryTroopAndCount` (RCB:342-372) - przy zywej paczce gra nie dosypuje najemnikow
   z niczego. Raz na dobe synchronizacja: w kazdym miescie pokazana jest klasa z najdluzej czekajacym czlowiekiem (FIFO), liczba = wszyscy weterani tej klasy w tym
   miescie (AN 5.2: "karczma pokazuje tylu ludzi, ilu jest weteranow"), typ = najemnik (Occupation.Mercenary - jedyny, ktorego wynajmuja lordowie AI, RCB:465)
   z drzewa najemnikow kultury miasta o tym samym "konny/pieszy" i najblizszym tierze (zapas: wspolne drzewa wszystkich kultur). Weteran traci wlasna linie
   oddzialu (AN 5.2 pkt 5), zachowuje tier i konia.
6. **Najem = jedno zdarzenie dla AI, karawan i gracza:** `CampaignEvents.MercenaryNumberChangedInTown` (G CampaignEvents.cs:669; pada w `ChangeMercenaryCount`
   RCB:70-78 - jedynej drodze ubytku w ApplyInternal RCB:612-621, `buy_mercenaries_on_consequence` RCB:716 i `BuyMercenaries` RCB:791). Ubytek nie przez nas =
   najem -> zdjecie z kolejki pokazanej klasy (FIFO) -> udzialy pochodzenia do skladu partii z okna najmu (E4/7 `Give`). Partie zna wlasne okno E4/10 (void First +
   finalizer) na ApplyInternal i na dwoch metodach gracza.
7. **Niewynajety po `VeteranWaitDays` (120) wraca do DOM regionu pochodzenia** (R16, U15c) przez E4/7 `HomeOrNote` z nowa przyczyna `MBackVeteran` (MHearth tak).
8. **Start (NK, SP:711): pula starych zolnierzy = to, co stoi w karczmach w dniu siewu** (nowa kampania: poczatkowe karczmy gry RCB:167-177; stary zapis: obecne).
   Zdjeci ze stanu ludzi miasta (E4/4 "t:") przez `MoveTown` - pochodzenie = region miasta; przy `!LevyLive` albo bez stanu miasta - bez zdjecia, koszyk "" (nazwany "start").
9. **Ludnosc BK (sklad, nie ludzie - R0):** weteran wchodzacy do puli dopisany do ludnosci BK MIASTA (`UpdatePopFromSoldiers`, BK PopulationData.cs:339-342) zamiast
   wsi; najem z karczmy BK zdejmuje juz sam (BK RecruitmentApplyInternalPatch.cs:15-21 - `DeduceManpower` przy kazdym ApplyInternal z osada). Suma BK jak dzis.
10. **Kon (160, SP:656-660):** typ konny pokazywany tylko z konnych weteranow, a konnym weteranem jest tylko ten, kto odszedl z koniem (zwolniony, rozbity,
    rozwiazany) - jeniec konia nie ma (zabral go zwyciezca, RecruitKit Z4). Wtedy kon, ktorego RecruitKit daje najemnikowi z wzorca (B RecruitKit.cs:503
    "do 167"), to kon tego czlowieka - **zero koni z niczego w karczmie**. Zbroja i bron weterana - bez zmian (pytanie 2; osobny krok).
11. **Zloto: E4/10 nie rusza zadnego przeplywu** (P 5.2 pkt 4). Zaplata za najem dalej do miasta (B LevyGold.cs:118-160), sakiewka zwolnionych dalej do kiesy wsi
    (B ClanBudget.cs:749-759) - OB 6.2 "do kasy miasta karczmy" odlozone do etapu 5 (kasy miast). Zmienia sie tylko ilosc najmu (rozdz. 10.4).
12. **Hamulec wolnych rak (E4/7, Jeff 11.10) NIE dotyczy karczmy:** weteran nie jest nowym rekrutem z pola (juz jest poza domem), wiec kraina bez wolnych rak
    moze najac weterana. WETERANI nie licza sie do S w R5 (litera P R5 i U19 - jedna definicja; wielkosc puli ok. 1-3 tys. wobec S setek tysiecy).
13. **P1 (obowiazkowe):** rozdz. 2-9. **P2 (nazwane, bez kodu w E4/10):** zwolnienie przez gracza na ekranie druzyny (wymaga, zeby E4/7 traktowal je jako R6),
    sakiewka weterana do kasy miasta (etap 5), sprzet weterana (pytanie 2), resztki E4/7 4.5 "bez odbiorcy" (oczekiwane ~0), tekst "jeszcze nie liczone:
    zwolnieni do karczmy -" w linii na sucho (B ClanIncomeBook.cs:671).
14. **Informacja dla Jeffa (poprawka liczby z P):** "najemnikow o 81-97% mniej" (P :55, AN 5.2 z 05.10) liczono, gdy AI nikogo nie zwalnialo. Dzis (z30A) do domu
    wraca ok. 515 zolnierzy/d (166: 292, H3 do BK: 180, I1: 43); z progiem tieru 3 do karczm trafi [S] 30-60/d wobec dzisiejszego najmu 200/d (= ok. 70-85% mniej),
    bez progu [S] 150-190/d (5-25% mniej). Wynik dopiero w tescie (V7).

---

## 1. Zaleznosci od E4/7 (do sprawdzenia przy kodzie - E4/7 jest w toku na `w-toku/e4-7`, dzis bez commitow ponad dfb12df)

| # | Czego E4/10 potrzebuje od E4/7 | Gdzie w P47 | Gdy brak / inaczej |
|---|---|---|---|
| Z1 | typ `PeopleBook.Shares` (slownik region -> liczba, region "" = bez pochodzenia) dostepny z innego pliku partial; konstruktor z mapy (`new Shares(map)` albo `Shares.From`) | 2.1 | E4/10 trzyma wlasne mapy (Dictionary) i zamienia na Shares tylko przy `Give`/`HomeOrNote` - potrzebny jeden publiczny (internal) sposob zbudowania Shares |
| Z2 | dzielenie udzialow: E4/10 bierze z `org` czesc t i oddaje reszte dalej. Jesli `Shares.Slice(n)` nie zmniejsza oryginalu - E4/10 dopisuje w `PeopleBook.Veterans.cs` statyczne `CutShares(Shares s, double n)` (mutujace, proporcjonalne) | 2.1, 4.7 ("org.Slice(n)") | jw. |
| Z3 | `PeopleBook.Give(TroopRoster, Shares, int cause)` z przyczyna dopisywalna (E4/10: `GVetHire` - wejscie "z karczmy (WETERANI)") | 2.4 | bez przyczyny - wejscie liczy E4/10, linia "Pochodzenie" nie zamknie kontroli (T2 E4/7) |
| Z4 | `PeopleBook.HomeOrNote(Shares, double n, int causeKnown, int causeLegacy)` i `LevyLive` | 2.4, 4.11 | powrot weterana tylko `Note` (bez hearth) - licznik "powroty bez E4/7" |
| Z5 | getter `PeopleBook.OriginSown` (pole `_sown`) i kolejnosc w `PeopleBook.Daily`: `EnsureSown` -> `ReconcileDaily` -> **tu E4/10 `VetDaily`** -> cache R5 -> linie | 2.3, 4.14 | E4/10 wolane z OthersRise.Daily po PeopleBook.Daily (B ArmouryBehavior.cs:1519) |
| Z6 | sygnatury z udzialami: `ClanBudget.ToVillage(v, ch, n, cause, Shares org)` (B ClanBudget.cs:803) i `Take` przed nim w `ReleaseFrom` (B :742), `ToVillagePop(..., org)` (OverflowHome.cs:144, GentryService.cs:647); `org` przy wywolaniach `LosersFlee.SendHome` w OutlawLaw.cs:648, LosersFlee.cs:1119, PrisonerLaw.cs:197 i :449 | 4.7, 4.8, 4.9 | E4/10 wola `VetDivert` z `org = null` (udzial "" - weteran bez pochodzenia) |
| Z7 | rozwiazanie partii (4.3): postfiks E4/7 liczy "reszte skladu" **wedlug typu oddzialu** (migawka per typ w prefiksie) i przed `HomeOrNote(..., MBackDisband, ...)` zostawia miejsce na petle `VetDivert` per typ | 4.3 | E4/10 rozszerza stan prefiksu E4/7 o liste `(CharacterObject, liczba)` - jedna lista, bez drugiej latki na tym celu |
| Z8 | **galezie karczmy E4/7 milkna przy `PeopleBook.VetLive`:** (a) postfiks `DeduceManpower` - przypadek notabl == null i miasto i okno z `MercenaryFromTavern` (P47 3.3 "karczma - stan miasta do E4/10") - bez `TakeLevy`/`GiveNew`; (b) okna gracza na `BuyMercenaries` / `buy_mercenaries_on_consequence` (P47 3.5) - bez "stan miasta -1" i `GiveNew` | 3.3, 3.5 | podwojne wejscie do SLUZBY (E4/10 `Give` + E4/7 `GiveNew`) i podwojne zdjecie ludzi miasta - T2 E4/7 i V9 to pokaza |
| Z9 | liczniki SLUZBY w linii "Pochodzenie (E4/7)" wedlug przyczyny z mozliwoscia dopisania dwoch: wyjscie "do karczmy (WETERANI) w", wejscie "z karczmy (WETERANI) v"; kontrola linii je obejmuje; "karczmy a3" = 0 przy VetLive | 8.3 | jw. Z3 |
| Z10 | rownanie regionu (P47 5) z miejscem na `+ WETERANI_r` (E4/10 daje `PeopleBook.VetByRegion(Dictionary<string,double>)`) | 5 | rownanie krain odjedzie o pule (ok. 1-3 tys.) - T3 E4/7 |
| Z11 | `PeopleBook.MoveTown(Settlement town, int from, int to, double men, int cause)` (P47 D1 - E4/4 albo E4/7) | 1 (D1) | start bez zdjecia ("" "start") |
| Z12 | rozszerzalne `MKinds`/`MName`/`MHearth` (E4/5 ma 20, E4/6 w toku dodaje `MFamineDead`, `MFamineFled` = 22, E4/7 dodaje 9) - E4/10 dopisuje na koncu `MBackVeteran` (true) i `MVetStart` (false); konto `AVeterans` = nastepny wolny numer po E4/6 (`ADead`) i E4/9 | 2.5 | - |
| Z13 | menu "People of this land" (`PeopleMenu.cs`, P47 7) - E4/10 dopisuje dwie linie (rozdz. 7) | 7 | bez linii menu |
| Z14 | prog T6 E4/7 ("pobor z DOM = notable + karczma + A5") liczony bez karczmy przy VetLive | 9 | T6 NIE w tescie zbiorczym |
| Z15 | sekcje zapisu E4/7 `sw: so: jo: wo: rg:` - E4/10 bierze `vs: vt: vh:` (Import po `StartsWith` - B PeopleBook.cs:243-308: `"v:"` nie lapie `"vt:"`, bo drugi znak) | 2.2 | - |

Punkty styku z innymi paczkami: **E4/4** - stan miasta "t:" (B PeopleBook.cs:97, `TryTown` :333) dla startu; **E4/6** (w toku, `w-toku/e4-6`) - zmienia
PeopleBook (MKinds, ADead), Measure40 (o10 glod, linia Hearth), ArmouryBehavior, Settings - E4/10 zaczynac od czubka E4/7 po jego recenzji (scalanie recznie,
MCM zawsze od nowa `python tools/gen_mcm.py`); **E4/8** - milicje i karawany z ludzi: milicja wylaczona z weteranow, karawany najmuja weteranow jak dzis
najemnikow; **E4/9** - Straz wylaczona; "dluznik rozpuszcza poczet" (DLUG 4) ma wolac to samo `VetDivert` (rozwiazana partia - R16).

---

## 2. Stan w PeopleBook - konto WETERANI (`PeopleBook.Veterans.cs`, partial)

### 2.1 Pamiec
```
private sealed class VetClass {                       // jedna klasa w jednym miescie
    public int Men;                                   // ludzie (calkowite - czlowiek w karczmie jest jeden)
    public readonly Dictionary<string,double> Org;    // region pochodzenia -> udzial ("" = bez pochodzenia); suma = Men
    public readonly List<int> Day, N;                 // kolejka FIFO: doba wejscia, ludzi (suma N = Men)
}
private static readonly Dictionary<string, Dictionary<int, VetClass>> _vet;   // id miasta -> klucz klasy (konny ? 16 : 0) + tier -> klasa
private static readonly Dictionary<string, int> _vetShown;                     // id miasta -> klucz klasy pokazanej w karczmie (brak = nic)
private static bool _vSown; private static int _vSowDay = -1;
private static readonly double[] _vAcc = new double[VSrcs];                     // akumulator udzialu do karczmy wedlug strumienia (dokladnie 0.33 w dlugim biegu)
// liczniki doby (bez zapisu): wejscia i uprawnieni wedlug strumienia, ponizej progu tieru, bez miasta, najeci wedlug rodzaju partii, konni najeci,
// awans/spadek tieru przy najmie, powroty (z pochodzeniem / "" / miasto Innych / porzucone miasto), najem bez okna, najem ponad pule, przyrost obcy,
// czekajacy w oblezonych miastach, potkniecia, czas; pula wczoraj (kontrola)
```
Strumienie `VSrc`: `V166=0, VW4c=1, V179=2, VH3=3, VFreed=4, VLaw=5, VLootFree=6, VDisband=7, VSrcs=8`. Z koniem: 166, W4c, 179, H3, Disband; bez konia:
Freed, Law, LootFree (jeniec - RecruitKit Z4, B RecruitKit.cs:23).

### 2.2 Format zapisu (dopisane w `Export` B PeopleBook.cs:141-184 przez `try { ExportVeterans(sb); }` jak E4/5 :182; czytane w `Import` :232-322)
- `|vs:<doba siewu>;<acc0>;...;<acc7>` - siew zrobiony; akumulatory (zeby udzial 1/3 byl dokladny tez przez zapis).
- `|vt:<idMiasta>#<klucz>=<reg>:<n>;<reg>:<n>@<doba>:<n>;<doba>:<n>,<idMiasta>#...` - region "" jako `~`, n udzialu "0.##", kolejka liczby calkowite.
- `|vh:<idMiasta>=<klucz>,...` - klasa pokazana (zeby najem miedzy wczytaniem a pierwsza doba bral z wlasciwej kolejki).
- Id ze znakami `| , ; = : # @ ~` - pomijane (licznik "nie zapisane", oczekiwane 0 - id miast ROT to `town_XX`).
- Rozmiar [S]: wejsc ok. 30-190/d w ok. 40 parach miasto-klasa dziennie -> najwyzej ok. 40 x 120 = 4.8 tys. wpisow kolejek x ok. 8 znakow + ok. 600 klas x 40 znakow
  = ok. 65 KB (SaveText dzieli na kawalki - B ArmouryBehavior.cs:440). Linia po wczytaniu (8.2).
- `Reset` (B PeopleBook.cs:118, wolany z konstruktora B ArmouryBehavior.cs:395): czysci wszystko z 2.1 (stan kampanii nie przecieka do nastepnej).
- Stary zapis bez `vs:` = siew przy pierwszej dobie (2.3). Starszy DLL pomija `vt/vs/vh` (Import po przedrostku) - cofniecie DLL gubi pule (rozdz. 11).

### 2.3 Siew (`VetSow`, raz na kampanie; wolany z `VetDaily`)
Warunek: `VeteransEnabled`, `Campaign.Current` gotowa, `RecruitmentCampaignBehavior` jest (`Campaign.Current.GetCampaignBehavior<RecruitmentCampaignBehavior>()`),
i - gdy `PeopleOriginEnabled` - `PeopleBook.OriginSown` (Z5; inaczej start dostalby "" mimo dzialajacego pochodzenia).
- Dla kazdego `Town.AllTowns`: `md = rcb.GetMercenaryData(town)` (RCB:157-165, publiczne); `md.TroopType != null && md.Number > 0` -> klasa
  (`md.TroopType.IsMounted`, `md.TroopType.Tier`), n = `md.Number`; kolejka (doba siewu, n).
- Pochodzenie: `LevyLive` (Z4) i stan miasta (`TryTown`, B PeopleBook.cs:333) -> `MoveTown(town, ADom, AVeterans, n, MVetStart)` (Z11), udzial = region miasta
  (`OutlawLaw.RegionFor(town)`, B OutlawLaw.cs:243); inaczej udzial "" (licznik "start bez zdjecia").
- Karczmy NIE sa zmieniane w siewie (juz pokazuja tych ludzi); `_vetShown[town]` = ich klasa. Od nastepnej synchronizacji typ moze przejsc z `CaravanGuard`
  (30% karczm gry, G DefaultTavernMercenaryTroopsModel.cs:7) na najemnika tej samej klasy (rozdz. 4.4).
- Linia siewu (8.2). Od siewu `VetLive = _vSown && VeteransEnabled`.

### 2.4 API (kazde cialo we wlasnym try, licznik `Stumble`, nigdy nie rzuca - U24)
| Funkcja | Co robi |
|---|---|
| `bool VetLive` | `_vSown && Settings.VeteransEnabled` |
| `int VetDivert(MobileParty party, CharacterObject troop, int n, Vec2 from, int vsrc, ref Shares org)` | jedna droga wejscia (rozdz. 3): uprawnienie, miasto, t = czesc z akumulatora, BK miasta, dopisanie do klasy, `org` pomniejszone o udzial t; zwraca t (0 = wszyscy dalej do domu); przy wyjatku 0 |
| `Shares VetHire(Town town, int n, out int tierDelta)` | zdejmuje n z kolejki klasy pokazanej (FIFO, od najstarszych), przy braku - z innych klas tego miasta, dalej "" (licznik "najem ponad pule", oczekiwane 0); zwraca udzialy |
| `void VetDaily(int day, bool log)` | siew (2.3), powroty (rozdz. 5), `Veterans.Sync(day)` (4.3), linia dzienna (8.3) |
| `void VetByRegion(Dictionary<string,double> dst)` | WETERANI wedlug regionu pochodzenia (rownanie Z10, menu) |
| `int VetInTown(Settlement town, out int mounted, out int oldestDays)` | menu miasta |
| `int VetWorld` | suma puli (linia "Ludzie (ksiega)") |

### 2.5 Nowe przyczyny ruchu DOM (Z12)
`MBackVeteran` - "weterani niewynajeci po 120 dobach do domu (E4/10)", MHearth = true (powrot z 1/k_v przez `OutlawLaw.ReturnHome`, jak E4/7 MBack*);
`MVetStart` - "starzy zolnierze z ludzi miast do karczm (siew E4/10)", MHearth = false (miasto to stan, nie hearth). Wejscie SLUZBA -> WETERANI i najem
WETERANI -> SLUZBA to nie ruchy DOM - liczniki E4/10 i E4/7 (Z3, Z9), nie `MName`.

---

## 3. Wejscie do puli (R16) - jedna regula, piec punktow wpiecia

### 3.1 Regula `VetDivert` (kolejnosc krokow)
1. `!VetLive` albo `n <= 0` -> 0.
2. Uprawniony (wszystkie warunki): `troop != null && !troop.IsHero`; `troop.Occupation` Soldier albo Mercenary (jak B LosersFlee.cs:249, :437); `!Undead.Character(troop)`
   (B Undead.cs:35); partia nie: milicja, tabor wsi, karawana, partia Strazy (`CrownGifts.IsWatchParty`, B CrownGifts.cs:72-76), partia Innych (`Undead.Party`, :50);
   kultura oddzialu nie `nightswatch` (Straz dozywotnio - R14). Nieuprawniony -> 0 (bez akumulatora).
3. `troop.Tier < VeteranMinTier` -> 0, licznik "ponizej progu tieru do domu" (bez akumulatora - nie licza sie do 1/3).
4. Miasto: `Veterans.TownFor(from)` - najblizsze (odleglosc na mapie, wzor B VillageTakings.cs:382-398) miasto z `Town != null`, nie oblezone (ludzie nie
   przechodza przez linie oblezenia - ta sama zasada co W4 166, B ClanBudget.cs:667-675), nie Innych (kultura frakcji albo osady `whitewalker` - wzor
   `Undead.Party`), z typem najemnika dla tej klasy (4.4). Lista miast budowana raz na dobe. Brak -> 0, licznik "bez miasta" (bez akumulatora).
5. `t = floor(_vAcc[vsrc] + n x share + 1e-9)`, `_vAcc[vsrc] += n x share - t` (share = `VeteranShareToTavern`, obciete do [0; 1]). t = 0 tez zapisuje akumulator.
6. t > 0: BK - `ClanBudget.BkPopAdd(town, troop, t)` (nowy maly helper na istniejacej refleksji B ClanBudget.cs:768-792 - `_getPopData` + `_fromSoldiers`,
   bez `PeopleBook.Note`; wlasny try; brak danych BK miasta = pominiete z licznikiem - BK to nie ludzie, R0); udzial `CutShares(org, t)` (Z2; `org == null` -> "" x t);
   klasa (konny = `troop.IsMounted && vsrc ma konia`, tier = `troop.Tier`) - dopisanie do `Org` i do kolejki (dzisiejsza doba: zwiekszenie ostatniego wpisu albo nowy)
   **jako ostatni krok** (U24); liczniki we wlasnym try.
7. Zwraca t. Wolajacy wysyla `n - t` dalej dokladnie tak jak dzis (z pomniejszonym `org`).

### 3.2 Punkty wpiecia (kazdy: jedna linia przed dzisiejszym wyslaniem do domu; nic miedzy zdjeciem z rosteru a umieszczeniem poza `VetDivert`, ktory nie rzuca)
| # | Strumien | Miejsce (B) | Wpiecie | Polozenie `from` |
|---|---|---|---|---|
| W1 | zwolnieni 166, przepelnienie W4c, rycerze 179 | `ClanBudget.ToVillage(Settlement v, CharacterObject ch, int n, int cause[, Shares org])` :803-813 - jedyny punkt dla ReleaseFrom (:742), OverflowHome.SendHome (OverflowHome.cs:144 przez ToVillagePop :796-799) i GentryService (GentryService.cs:647) | na poczatku: `t = VetPlan(...)` (krok 1-5 bez zapisu); BK wsi dostaje `n - t` (dzis `n`, :809); gdy BK wsi zawiedzie - `return false` PRZED zapisem weteranow (nikt nie zszedl, ReleaseFrom nie zdejmuje - jak dzis); potem `VetCommit` (6) dla t; `HomeOrNote(org, n - t, MBack166/W4c/179, cause)` (E4/7) albo dzis `PeopleBook.Note(n - t, cause, 0)` (:812); `return true`. vsrc z `cause` (M166 -> V166, MW4c -> VW4c, M179 -> V179) | pozycja wsi `v` (wies, do ktorej by wrocil - VillageFor :830: zaloga - wsie twierdzy, partia - najblizsza wies rodu) |
| W2 | rozbici H3 | `OutlawLaw.RoutedH3Body` - `LosersFlee.SendHome(mp, e.Character, rest, pos, fac, SrcRouted, out cat)` OutlawLaw.cs:648 | przed: `int t = PeopleBook.VetDivert(mp, e.Character, rest, pos, VH3, ref slice); rest -= t; _h3Tavern += t;` (slice = udzial elementu z P47 4.7); `rest == 0` -> `continue` | pole bitwy `pos` |
| W3 | uwolnieni jency bez odbiorcy | LosersFlee.cs:1112-1126 (`SendHome(null, fp.T, n, pos, null, SrcFreed, out cat)` :1119) | po `AddToCounts` :1115 i przed SendHome: `t = VetDivert(null, fp.T, n, pos, VFreed, ref org); n2 = n - t` -> SendHome(n2); `left = n2 - got` | pole bitwy |
| W4 | prawo jenca I1 (sprzedani/wypuszczeni w krainach bez niewoli, wolni w krainach z niewola) i zostawieni po bitwie gracza | PrisonerLaw.Route :173-218 (SendHome :197, po Murze :186-195) i PrisonerLaw.Free :440-455 (SendHome :449) | przed SendHome: `t = VetDivert(null, troop, n, pos, VLaw / VLootFree, ref org); n -= t; _vetI1 += t;` - przestepcy na Mur zostaja przed tym (R14) | miejsce sprzedazy / bitwy (`pos` przed `LawHomePos` - LawHomePos dziala dopiero w SendHome B LosersFlee.cs:405) |
| W5 | rozwiazane partie (R6) | postfiks E4/7 na `DisbandPartyCampaignBehavior.OnPartyDisbanded` (G :290-306; do wsi :364-, wrogiej warowni :309- - sklad `Clear`) | w petli per typ "reszty skladu" (Z7): `t = VetDivert(party, ch, lost, party.GetPosition2D, VDisband, ref org)`, reszta -> `HomeOrNote(..., MBackDisband, ...)` | pozycja partii |
Poza regula (swiadomie): `SendHome(..., SrcPool)` OutlawLaw.cs:924, :941 (powrot z puli wyrzutkow - R16 ich nie wymienia; to rozbici, ktorzy wybrali las, i dezerterzy);
E4/7 4.5 "reszta bez odbiorcy" (`HomeOrNote` wprost, oczekiwane ~0 - P2); `LosersFlee.ToWall` (Mur); `AddSlaves` (niewola).

### 3.3 Dopiski do linii wolajacych (bez zmiany istniejacych liczb - parser czyta po etykietach)
- "Przegrani (H3)" - linia dzienna (B LosersFlee.cs:1459-1464): na koncu odcinka "domy:" po "bez domu N (do puli)" -> `, do karczmy (E4/10) X`; linia bitwy (B :1361-1364,
  "rozbici: do puli ..., bez domu ...") - tak samo, liczba z `RoutedH3Body` ta sama droga co dzis `bk/village/template` (OutlawLaw.cs:650). Kontrola: rozbici = pula + domy + bez domu + do karczmy.
- "Prawo jenca (I1)" (PrisonerLaw.cs:492-495): po "bez domu: do puli wyrzutkow N, znikneli M" -> `, do karczmy (E4/10) X`.
- "Budzet rodow (166)" (ClanBudget.cs:1107): po "zniklo Z" -> `, z tego do karczmy (E4/10) Y` (`_dRelMen` dalej liczy wszystkich zwolnionych; parser `zwolnieni: do wsi (\d+)` bez zmian).
- W4c (OverflowHome) i 179 (GentryService) - tylko liczniki E4/10 wedlug strumienia (linia dzienna 8.3).

---

## 4. Karczma - latki, synchronizacja, typ najemnika, najem

### 4.1 Latki (wszystkie w `Veterans.ApplyAll(_harmony)`, wpinane zawsze, dzialaja przy `VetLive` - U26)
| # | Cel (G) | Rodzaj | Co | Kto jeszcze lata ten cel |
|---|---|---|---|---|
| L1 | `RecruitmentCampaignBehavior.UpdateCurrentMercenaryTroopAndCount(Town, bool)` private, RCB:342-372 (wolany z `DailyTickTown` RCB:205-208 i `OnNewGameCreatedPartialFollowUpEnd` RCB:167-177) | prefiks `bool`, Priority.Normal | `return !PeopleBook.VetLive;` - przy zywej paczce gra nie dosypuje (+10% co dobe RCB:345-348) ani nie losuje nowej paczki z niczego (RCB:351-371). Start nowej kampanii idzie przed siewem - gra wypelnia karczmy (to jest pula startowa) | **nikt** (grep `UpdateCurrentMercenaryTroopAndCount` w DLL wszystkich modulow: tylko gra; BK - brak, B - brak, ROTd - brak, DTE - brak) |
| L2 | `RecruitmentCampaignBehavior.ApplyInternal(MobileParty, Settlement, Hero, CharacterObject, int, int, RecruitingDetail)` private, RCB:607-640 | prefiks void Priority.First + finalizer | okno najmu: przy `__6 == MercenaryFromTavern` biezaca partia = `__0` (stos: pole + poprzednia w `__state`, finalizer przywraca - wzor B LevyGold.cs:36-48) | LevyGold prefiks/finalizer/postfiks (B LevyGold.cs:36-48, :70-162, wpiecie :198-203), CirculationWindows `RecruitPre` (B :562-574), BK postfiks `DeduceManpower` (BK RecruitmentApplyInternalPatch.cs:15-21), SpoilsCompany licznik (B :297), CS `MapVolunteerPrefix` bool tylko dla VolunteerFromMap (CS OthersGrowth.cs:177-189), E4/7 `RecruitWinPre/Fin` (P47 3.1). Nasze: void, bez zmiany wyniku |
| L3 | `RecruitmentCampaignBehavior.BuyMercenaries()` private, RCB:789-797 (dialog w karczmie) | prefiks void Priority.First + finalizer | okno najmu: partia = `MobileParty.MainParty` | DTE `TavernMercenaryEquipmentPatch` (DTE :7-22 - komplet do zbrojowni gracza), RecruitCost `PlayerBuyPrefix/Postfix` (B RecruitCost.cs:286-330, wpiecie :393), E4/7 3.5 |
| L4 | `RecruitmentCampaignBehavior.buy_mercenaries_on_consequence()` private, RCB:705-720 (menu "Recruit N ..." w zaulku) | jw. | jw. (metoda sama wola `GameMenu.SwitchToMenu("town_backstreet")` - gra, nie my; CLAUDE.md 7 nie dotyczy) | DTE `TownMenuMercenaryEquipmentPatch` (DTE :8, :29-44), RecruitCost (jw.), E4/7 3.5; ROT ukrywa menu przy zaciagu (ROTd EnlistmentPatches.cs:221-229 - postfiks warunku, bez wplywu) |
| E1 | zdarzenie `CampaignEvents.MercenaryNumberChangedInTown` (G CampaignEvents.cs:669) | nasluch w `ArmouryBehavior.RegisterEvents` obok B :677 | `Veterans.OnTavernNumber(Town, int old, int new)` (4.5) | SB `RecruitmentAgentSpawnBehavior` (SB :20 - odswieza postac najemnika w karczmie, tylko gdy gracz jest w tym miescie poza misja, SB :40) |
Nie latamy `TownMercenaryData` (publiczne `ChangeMercenaryType`/`ChangeMercenaryCount` RCB:54-78 - uzywamy ich). Brak typu/metody -> linia startowa "BRAK ..." i paczka
nieczynna (VetLive false - karczma jak w grze), bez wyjatku.

### 4.2 Flaga "to my"
`[ThreadStatic] static bool _self` ustawiana na czas kazdego naszego `ChangeMercenaryType`/`ChangeMercenaryCount` (try/finally). `ChangeMercenaryType` przy innym typie
ustawia liczbe bez zdarzenia liczby i rzuca `MercenaryTroopChangedInTown` (RCB:56-62); przy tym samym typie wola `ChangeMercenaryCount` (zdarzenie liczby) - oba
przypadki pod flaga (AN 5.2 [KRYT] pkt 4).

### 4.3 Synchronizacja doby (`Veterans.Sync(day)` z `VetDaily`, po powrotach)
Dla kazdego `Town.AllTowns` (97): `md = rcb.GetMercenaryData(town)`;
- miasto bez klas -> gdy `md.Number > 0`: `ChangeMercenaryCount(-md.Number)` (typ zostaje - jak po wykupieniu w grze; nic nie czyta `TroopType == null` przy
  `Number > 0`, a `buy_mercenaries_condition` RCB:677-703 i postac karczmy SB :68 wymagaja `Number > 0`);
- inaczej klasa do pokazania = klasa z najstarszym pierwszym wpisem kolejki (remis: wiecej ludzi); `troop = TypeFor(town.Culture, konny, tier)` (4.4); klasa bez typu
  -> nastepna (licznik "klasa bez typu"); `md.ChangeMercenaryType(troop, klasa.Men)` (gdy typ i liczba inne niz dzis); `_vetShown[town] = klucz`;
- miasto oblezone - bez zmian w karczmie (czekaja w srodku), liczone "w oblezonych".
Koszt: 97 miast, slowniki - ponizej 1 ms.

### 4.4 Typ najemnika dla klasy (`TypeFor`, pamiec na sesje)
- Kandydaci kultury: wszystkie oddzialy osiagalne przez `UpgradeTargets` z `culture.BasicMercenaryTroops` (G CultureObject.cs:218), `Occupation == Mercenary`, nie bohater.
  ROT (ROTx `basic_mercenary_troops`, np. :615-622 empire, :1549-1555 sturgia): `eastern_mercenary`, `western_mercenary`, `ROT_mercenary1`, `sword_sisters_sister_t3`;
  drzewa (SBC/ROTt, poziom -> tier): piesi - western_mercenary 16 (t3) -> _t4 21, crossbow_t4 21 -> _t5 26; eastern_mercenary 16 -> _t4 21 -> _t5 26;
  sword_sisters t3/t4, infantry_t5; konni - ROT_mercenary1-5 (16/21/26/31 = t3-t6), eastern_mounted_mercenary_t4/_t5, sword_sisters_sister_t5.
  Uwaga: `crownlands` (Krolewska Przystan) ma tylko `ROT_mercenary1` (konni) - piesi weterani dostaja typ z zapasu wspolnego.
- Wybor: ten sam `IsMounted`, najmniejsze |tier - tier klasy|, przy remisie nizszy (bez awansu). Brak takiego w kulturze -> zapas wspolny (suma drzew wszystkich kultur).
  Weteran pieszy **nigdy** jako konny (kon z niczego); konny bez konnego typu w zapasie -> pieszy typ (licznik "konni jako piesi" - kon odchodzi z nim do domu
  w ksiedze ludzi nic nie zmienia; w sprzecie - pytanie 2). Kultury `whitewalker` i bez drzew - tylko zapas.
- Linia startowa wypisuje liczbe kultur i tiery zapasu (test V1: najnizszy tier najemnika = `VeteranMinTier`).

### 4.5 Najem (`Veterans.OnTavernNumber(town, old, new)`)
- `_self` albo `!VetLive` -> nic. `new > old` (nie my) -> licznik "przyrost obcy" (oczekiwane 0; nastepna synchronizacja wyrowna do puli).
- `new < old`: n = old - new; `var sh = PeopleBook.VetHire(town, n, out d)` (FIFO z klasy pokazanej); partia okna (L2-L4):
  - jest -> `PeopleBook.Give(party.MemberRoster, sh, GVetHire)` (Z3) - kolejnosc wobec `AddElementToMemberRoster` (RCB:623, :793) bez znaczenia (slownik E4/7, uzgodnienie raz na dobe);
    rodzaj: `party == MainParty` -> gracz; `party.IsCaravan` -> karawany; inaczej lordowie (w tym rody najemne - osobny licznik dla 185/Spoils: `ActualClan.IsUnderMercenaryService || IsClanTypeMercenary`);
    konni najeci += n gdy typ konny; awans/spadek tieru += d;
  - brak (inny mod zmienil liczbe poza oknem) -> licznik "najem bez okna" (oczekiwane 0), udzialy do licznika "w nicosc - karczma bez okna" (E4/7 Reconcile nazwie nadwyzke tej partii).
- BK zdejmuje swoja klase sam (BK :15-21) - dla AI; gracz (dialog/menu) BK nie zdejmuje (jak dzis).
- Hamulec E4/7 (Levy.ProbabilityPostfix) nie dotyczy karczmy (0 pkt 12).

### 4.6 Gracz
Ta sama karczma, te same weterani, to samo zdarzenie (jedna regula gracz/AI). Postac najemnika w karczmie i rozmowa (RCB:722-770 wymagaja `Occupation` Mercenary/CaravanGuard/
Gangster - nasze typy sa Mercenary) dzialaja bez zmian. DTE dalej daje graczowi komplet do zbrojowni (DTE :29-44) - istniejaca dziura sprzetu, poza E4/10 (pytanie 2).

---

## 5. Powrot po 120 dobach (R16, U15c) i przypadki brzegowe (`VetDaily`, przed `Sync`)
- Kazda klasa: zdejmuj wpisy kolejki z `doba <= dzis - VeteranWaitDays`; n -> `HomeOrNote(CutShares(Org, n), n, MBackVeteran, MBackVeteran)` (Z4; znana czesc -
  `OutlawLaw.ReturnHome(region pochodzenia)` B OutlawLaw.cs:344, najslabsza wies regionu, +n/k_v; "" - `Note` bez hearth). BK - nic (juz w ludnosci BK miasta od wejscia).
- Miasto oblezone - powroty wstrzymane (nie przechodza przez linie oblezenia); po oblezeniu wszyscy przeterminowani wracaja pierwszej doby. Licznik "czekaja w oblezonych".
- Miasto zajete przez Innych (kultura frakcji/osady `whitewalker`) -> wszyscy weterani tego miasta wracaja od razu (licznik "uciekli z miasta Innych").
- Id miasta z zapisu nieznane (`Settlement.Find` null) -> powrot od razu z licznikiem "porzucone miasto".
- `VeteransEnabled` wylaczone po siewie: wejscia ustaja (W1-W5 zwracaja 0 - wszyscy do domu), karczma wraca do gry (L1 przepuszcza), najem nie zdejmuje z puli
  (E1 nic nie robi), **powroty trwaja** az pula sie oprozni (najwyzej 120 dob) - nikt nie utyka. Ponowne wlaczenie - bez nowego siewu (`vs:` jest).
- Smierc naturalna w 120 dobach - pomijana (ok. 0.4% w 120 dobach przy 1.3%/rok; pula ok. 1-3 tys. - < 15 ludzi/rok); nazwac w CHANGELOG.

---

## 6. Kon i sprzet - co E4/10 zmienia, czego nie
- RecruitKit (B RecruitKit.cs:496-507): najemnik z karczmy AI dostaje dobytek (`OwnOf`) i konia z wzorca, gdy oddzial konny (:503 "160: kon najemnika z wzorca (do 167)").
  Po E4/10 typ konny stoi w karczmie tylko z konnych weteranow (4.4) -> ten kon jest koniem weterana (160: "kon wlasnoscia zolnierza"). **Kod bez zmian, tylko etykiety**
  przy `VetLive`: :584 "konie i rzedy najemnikow z wzorca (160)" -> "konie konnych weteranow z karczmy (167)", komentarz :503 i opis :23. Parser tych etykiet nie czyta.
- 160 premia zoldu konnych (MountedWage) - dziala sama (typ konny). RecruitCost `MercShelfOn` nieczynne przy `RecruitsOwnHorse` (B RecruitCost.cs:77) - bez zmian.
- Zbroja i bron zwolnionego zostaja w zbrojowni pana (DTE/AiGear - B AiGear.cs naglowek), a najemnik przynosi dobytek tieru 1 jak dzis -> pytanie 2 (osobny krok "sprzet idzie
  z czlowiekiem", wymaga zdjecia kompletu ze zbrojowni przy zwolnieniu - inna ksiega niz ludzie).

---

## 7. Menu osady (E4/7 `PeopleMenu.cs`, Z13) - dwie linie, napisy po angielsku
- Kazda osada (region): `Veterans of this land waiting in taverns: X` (z `VetByRegion`; tylko gdy VetLive).
- Miasto, dodatkowo: `In this town's tavern: N veterans wait for a master (M mounted); the longest has waited D days.` albo `No veterans wait in this town's tavern.`
- Bez `GameMenu.SwitchToMenu` (CLAUDE.md 7) - to tylko tekst w oknie E4/7.

---

## 8. Klucze, linie logu

### 8.1 Klucze (Settings.cs po kluczach E4/7..E4/9, blok z komentarzem "E4/10 (167, PLAN 4.14)"; potem `python tools/gen_mcm.py`; przy kodzie `grep` kazdego klucza
w `Documents\...\Configs\ModSettings\Global\Armoury\Armoury.json` Jeffa - dzis brak `Veteran*`, sprawdzone 11.10 - CLAUDE.md 7)
| Klucz | Domyslna | Uzasadnienie | Podpowiedz MCM (EN) |
|---|---|---|---|
| `VeteransEnabled` | true | P E4/10, R16; wylacznik paczki | a tavern's hirelings are real men: soldiers who left service - released by their lord, routed from a lost battle, freed from captivity or sent off when their company was disbanded - and stopped at the nearest town to sell their sword. No hireling appears from nowhere; at the start the taverns hold the old soldiers already there. Off = taverns as in the game (veterans already waiting still go home in time) |
| `VeteranShareToTavern` | 0.33 | P E4/10 "2/3 do domu, 1/3 do karczmy" | share of soldiers leaving service alive who stop at the tavern of the nearest town instead of going straight home |
| `VeteranWaitDays` | 120 | P R16, U15c (jedna pora roku) | days a veteran waits in a tavern for a master before he gives up and goes home |
| `VeteranMinTier` | 3 | 0 pkt 4 (najnizszy najemnik karczmy ROT ma tier 3); pytanie 1 | only a soldier of at least this tier stops at a tavern - a green levy would stand there as a seasoned sellsword he never was; lower ranks always go home |

### 8.2 Linie startowe i jednorazowe
- SubModuleMain - `Veterans.ApplyAll(_harmony)` we wlasnym try po linii E4/7 `ApplyOrigin` (po B SubModuleMain.cs:148 i liniach E4/6, E4/7):
  `Weterani (E4/10): wpiete: karczma bez dosypki z niczego (UpdateCurrentMercenaryTroopAndCount), okno najmu AI (ApplyInternal), okno najmu gracza 2/2 (BuyMercenaries, buy_mercenaries_on_consequence), nasluch ubytku karczmy (MercenaryNumberChangedInTown); BRAK: -; wlasciciele latek celow: <cel: id, id; ...>; ustawienia startowe przed MCM (stan gry - linia dzienna).`
  (`Harmony.GetPatchInfo` jak E4/0 U25.) Typy liczone w pierwszej dobie (kultury sa juz wczytane): w linii siewu albo wczytania.
- Siew: `Weterani (E4/10): siew dnia D - starzy zolnierze z karczm N w M miastach (konni k; tiery t3 a, t4 b, t5 c, t6 d), ze stanu ludzi miast (E4/4) n1, bez zdjecia (brak stanu miasta albo pobor z ludzi wylaczony) n2 | typy najemnikow: kultur miast K (z wlasnym pieszym a, z wlasnym konnym b), zapas wspolny: piesi tiery x-y, konni tiery x-y.`
- Wczytanie (pierwsza doba sesji): `Weterani (E4/10): z zapisu N ludzi w M miastach (klas K, wpisow kolejek Q), pokazanych S; nieznane miasta U (do domu); typy najemnikow: ... (jak w siewie).`

### 8.3 Linia dzienna (przy LogEnabled; w `PeopleBook.Daily` zaraz po "Pochodzenie (E4/7)")
`Weterani (E4/10): dzien N | pula X w Y z 97 miast (konni k, piesi p; tiery t3 a, t4 b, t5 c, t6 d; z pochodzeniem xk w R regionach, bez pochodzenia x0), najdluzej czeka A dob | wejscia W: zwolnieni 166 a1, przepelnienie W4c a2, rycerze 179 a3, rozbici H3 b, uwolnieni jency c1, prawo jenca I1 c2, zostawieni po bitwie gracza c3, rozwiazane partie d (konni wk) | uprawnieni U, do karczmy W = w% (cel s%), ponizej tieru T do domu L, bez miasta M0 | najeci H: lordowie h1 (w tym rody najemne h1m), karawany h2, gracz h3; konni hk; tier karczmy wobec weterana: awans +u, spadek -s | powroty R po D dob: do DOM z pochodzeniem rk ludzi (+hh hearth), bez pochodzenia r0, z miast Innych ri, porzucone miasta rp; czekaja w oblezonych miastach o | karczmy: pokazane S miast (konny typ m), klasy bez typu kb, konni jako piesi kp, najem bez okna 0, najem ponad pule 0, przyrost obcy 0 | kontrola: pula wczoraj P0 + wejscia W - najeci H - powroty R = P (roznica 0) | potkniecia st | czas T ms.`

### 8.4 Dopiski do istniejacych linii
- `Ludzie (ksiega)` (B PeopleBook.cs:877-, odcinek E4/7): `| E4/10: konto WETERANI X (z pochodzeniem xk, bez x0) - dzis +W / -H / -R`.
- `Pochodzenie (E4/7)` (Z9): wejscia `z karczmy (WETERANI) v`, wyjscia `do karczmy (WETERANI) w`; `karczmy a3` = 0 przy VetLive.
- `Hearth (E4/0)` (B Measure40.cs:862-870): kolejne `o` (po E4/5 o8/o9, E4/6 o10, E4/7 - numer przy kodzie) `= mv[PeopleBook.MBackVeteran]` do `named` i tekstu
  `, powroty weteranow (E4/10) +oV` - inaczej RESZTA hearth wchlonie powroty [S: ok. 10-40 ludzi/d = ok. 0.1 hearth/d].
- "Przegrani (H3)", "Prawo jenca (I1)", "Budzet rodow (166)" - 3.3. "Komplet rekruta" - rozdz. 6.

### 8.5 Narzedzie `tools/sprawdz_logi.py --grupa etap4` - blok E4/10
Nowa funkcja `etap4_e410(rep, lines, start, win, lud, until)` wolana w `etap4_main` (B sprawdz_logi.py:526; wzor wywolania E4/5 :833-835), gdy jest linia
`Weterani (E4/10): wpiete`. Serie: `e4_series(lines, "Weterani (E4/10): dzien")` (wzor :485), "Ludzie: dzien" (`, z karczmy (\d+)` - PeopleLedger.cs:542),
"Budzet rodow (166): dzien", "Przegrani (H3): dzien", "Komplet rekruta: dzien", "Konie rekrutow (160". Progi - rozdz. 9 (TAK/NIE/INFO; stan z linii dziennej).

---

## 9. Test 120 dob (nowa kampania, ustawienia jak u Jeffa) + zapis z doby 362 (9 dob)
Test zbiorczy (Jeff 11.10 03:05) - progi dochodza do progow paczek w tym samym biegu (E4/7 T1-T15 z poprawka Z14).
| # | Prog | Jak | Baza / skad liczba |
|---|---|---|---|
| V1 | linia startowa "BRAK: -"; 0 ERROR naszych modow; potkniecia E4/10 0 (najwyzej 5 z nazwa miejsca); w linii siewu najnizszy tier zapasu = `VeteranMinTier` (3) | log | standard paczki; 4.4 |
| V2 | siew w d1-d2: N > 0 w >= 90 miastach, n2 = 0 (przy czynnym E4/4 i E4/7) | linia siewu | karczmy gry na starcie (RCB:167-177) |
| V3 | kontrola puli: roznica 0 kazdej doby | linia dzienna | z definicji |
| V4 | udzial do karczmy w d1-d120: suma W / suma U = `VeteranShareToTavern` +- 0.01 | linia dzienna | akumulator (3.1 krok 5) |
| V5 | najem bez okna = 0, najem ponad pule = 0, przyrost obcy = 0 kazdej doby | linia dzienna | 4.5 |
| V6 | najdluzej czeka <= `VeteranWaitDays` kazdej doby (poza miastami oblezonymi - osobno "o") | linia dzienna | R16 |
| V7 | **"z karczmy" w "Ludzie:" tylko z puli** (P E4/10 test): h1 + h2 = "Ludzie: ... z karczmy" tej doby, \|roznica\| <= 1 kazdej doby | "Weterani", "Ludzie:" | PeopleLedger.OnTroopRecruited :286-302 (`_rTavern` :294) |
| V8 | INFO: najem/d d28-d120 wobec z30A 200/d ("Ludzie:"), AI lordowie 81/d ("Komplet rekruta"), konni 81/d ("Konie rekrutow (160)") - oczekiwane [S] 30-60/d przy progu tieru 3 | serie | 0 pkt 14 |
| V9 | konni najeci (suma) <= konni weterani: start (k siewu) + wejscia konne (wk) - powroty konnych; "konie konnych weteranow z karczmy (167)" w "Komplet rekruta" = 0 poza najmem z puli | linia dzienna, Komplet | kon z niczego = 0 (SP:656-660) |
| V10 | rownanie krain z kontem WETERANI: \|reszta\| <= 0.1% ludzi krainy kazdej doby (najgorsza kraina) | "Ludzie (ksiega)" | P rozdz. 4 "rownanie krain z kontami JENCY i WETERANI < 0.1%" (U1); przy RESZCIE hearth != 0 - INFO (jak T3 E4/7) |
| V11 | E4/7 T2 ("Pochodzenie": kontrola \|.\| <= 1) i T6 bez karczmy (Z14) - dalej TAK | "Pochodzenie" | P47 9 |
| V12 | wojsko lordow w wojnie 95-115 tys. srednio d90-d120 (podloga 85 tys.); prog konca etapu d300-364 w biegu 364 dob (U20) | progi etapu 2 | z30A partie rodow 107 tys. (d120) |
| V13 | "Ludnosc BK (169c)" d1 -> d120 przeliczone na rok <= +2%; INFO roznica wobec biegu E4/7 bez E4/10 | 169c | 0 pkt 9 (BK miasta przy wejsciu) |
| V14 | INFO zloto: "najemnicy z karczmy" w ksiedze pieniadza (do miast) wobec z30A 43.9 tys. zl/d; progi etapu 3 (reszta ksiegi < 10 tys. zl/d, 0 bankructw, glowy < 5 000 <= 10) TAK | ksiega pieniadza, progi-etap3 | z30A; rozdz. 10.4 |
| V15 | koszt: czas linii E4/10 + Sync <= 3 ms/dobe; s/doba <= +1% wobec biegu bez E4/10 | "czas" | 4.3 |
| V16 | zapis z doby 362, 9 dob: 0 bledow; "z zapisu N ludzi" = pula z ostatniej linii przed zapisem; nieznane miasta 0; rozmiar `arm_people` w logu | log | 161 |
| V17 | INFO: rody najemne (185, Spoils) - h1m/d i linia kontraktu 185 (ludzie z umowy) d1 -> d120 | linie | rozdz. 10.3 |
| V18 | menu: reczna proba Jeffa - miasto -> "People of this land" -> linie weteranow = linia dzienna tego miasta | gra | rozdz. 7 |
Nie przechodzi -> wylaczamy kawalek w `Armoury.json` autotestu (`VeteransEnabled` - calosc; `VeteranMinTier` 0 tylko jako proba prog/bez progu) i powtarzamy.

---

## 10. Kontrola calosci (zasada 0 - wpisac do "Ryzyko / co sprawdzic" w CHANGELOG)

### 10.1 Wspolne cele i stan (plik:linia)
| Cel / stan | Kto dzis | E4/10 | Co pilnowac |
|---|---|---|---|
| `UpdateCurrentMercenaryTroopAndCount` (RCB:342-372) | nikt | L1 prefiks bool | jedyny bool na celu; start kampanii przed siewem przepuszczony |
| `ApplyInternal` (RCB:607-640) | LevyGold B :36-162 (zloto), CirculationWindows :562-574, BK postfiks (DeduceManpower), SpoilsCompany :297, CS bool tylko mapa, E4/7 okno | L2 okno void | finalizer zawsze (wyjatek w grze); stos przy zagniezdzeniu |
| `BuyMercenaries` / `buy_mercenaries_on_consequence` (RCB:789-797, :705-720) | DTE komplet gracza, RecruitCost (kon z polki), E4/7 3.5 | L3/L4 okna | E4/7 3.5 milknie przy VetLive (Z8) |
| `MercenaryNumberChangedInTown` (CampaignEvents.cs:669) | SB odswiezanie postaci (SB :51-57) | E1 nasluch | flaga `_self` (4.2) |
| `ClanBudget.ToVillage` (B :803-813) - punkt powrotu (2) R0 | 166 ReleaseFrom :742, W4c OverflowHome.cs:144, 179 GentryService.cs:647; BK `UpdatePopFromSoldiers` :809; `PeopleBook.Note` :812 | W1 podzial przed BK | wszystko-albo-nic jak dzis (`false` przed zapisem weteranow); E4/7 zmienia te sama metode (Z6) - scalic recznie |
| `LosersFlee.SendHome` (B :388-451) | H3, uwolnieni, I1, pula | **bez zmian** (podzial u wolajacych W2-W4) | kategorie `cat` (B :56) i liczniki wolajacych bez nowej kategorii - "do karczmy" osobno (3.3) |
| `OutlawLaw.RoutedH3Body` (B OutlawLaw.cs:640-657) | H3 (pula, domy, bez domu) | W2 | rozbici = pula + domy + bez domu + do karczmy |
| `PrisonerLaw.Route`/`Free` (B :173-218, :440-455) | I1 (Mur, dom, pula) | W4 po Murze | przestepcy na Mur przed karczma (R14) |
| `OnPartyDisbanded` (G :290-306) | GarrisonArmory (B :407-466 zbrojownia, wpiecie :1085), E4/7 4.3 | W5 w postfiksie E4/7 (Z7) | jedna lista per typ, bez drugiej latki |
| RecruitKit karczma (B :496-507, :584-585) | 160/171 kon z wzorca | etykieta | kon tylko z konnego weterana (V9) |
| `PeopleBook` Export/Import/Reset/Daily (B :118, :141-184, :232-322, :877) | E4/2-E4/6, E4/7 | sekcje `vs/vt/vh`, `VetDaily` | Reset w konstruktorze B ArmouryBehavior.cs:395; scalanie z E4/6 (w toku) i E4/7 |
| `Measure40` linia Hearth (B :862-870) | E4/0-E4/6 | o dla MBackVeteran | numer o przy kodzie (E4/6 zajal o10) |
| PeopleLedger "z karczmy" (B :294, :542) | E4/0 | bez zmian | kontrola V7 |

### 10.2 Zamknieta ksiega ludzi (R0, P 5.2 pkt 4)
- Przeplywy E4/10: SLUZBA -> WETERANI (W1-W5), WETERANI -> SLUZBA (najem, udzialy do skladu), WETERANI -> DOM (powrot po 120 dobach, +1/k_v), DOM miasta -> WETERANI
  (siew, bilans otwarcia w dniu siewu). Kazdy czlowiek w puli ma klase, miasto, dobe wejscia i udzial pochodzenia; rownanie regionu z `+ WETERANI_r` (Z10).
- Znika zrodlo "karczma z niczego" (z30A ok. 200 ludzi/d; P rozdz. 1 "48 657 najemnikow z niczego" w 457 dobach). Jedyne nienazwane: "najem ponad pule" i "przyrost obcy" -
  progi 0 (V5).
- Symetria z E4/7: weteran to czlowiek, ktory inaczej wrocilby do DOM (+1/k) - dostaje te +1/k dopiero przy powrocie (ta sama `HomeOrNote`, ten sam warunek `LevyLive`);
  najem nie zdejmuje DOM (czlowiek juz byl poza domem). Start: `MoveTown` tylko przy `LevyLive` - przy przelaczeniu w trakcie kampanii nikt nie powstaje z niczego.
- BK (sklad, R0): wejscie - BK miasta +t (zamiast BK wsi), najem - BK miasta -n (BK), powrot - bez zmian. Suma BK = dzis; zmienia sie tylko geografia (miasto zamiast wsi).
- Nic drugi raz: E4/7 nie zdejmuje stanu miasta za najem (Z8); `VetDivert` tylko przed dzisiejszym wyslaniem do domu (wolajacy wysyla `n - t`); BK miasta tylko przy
  wejsciu; RecruitKit dalej jedno zrodlo konia najemnika (teraz konnego weterana).

### 10.3 Kolizje z innymi zmianami
- **166 ClanBudget (B :627-711):** pierwsza runda zwalnia najemnikow (Occupation.Mercenary, B :664, :725) - po E4/10 ida ta sama regula (1/3 karczma, 2/3 dom), nie 100% do
  karczmy (OB 6.2 mowil "najemnicy do puli weteranow" - P E4/10 rozstrzyga jedna regule; wpisac w CHANGELOG jako swiadome). Straz nie zwalnia (B :633 `WatchNoReleases`) - bez zmian.
- **LevyGold (B :118-160):** zloto najmu dalej do miasta; kon z polki nieczynny przy 160. Mniej najmu = mniej zlota do miast (10.4).
- **MercContract 185 (B MercContract.cs:13-28):** rody najemne to partie lordow - najmuja z karczmy jak lordowie (RCB:463-499). Mniej najemnikow w karczmach -> wolniejsze
  uzupelnianie kompanii -> przeglad umowy tylko w dol (185) zmniejszy kontrakt. INFO V17.
- **Spoils klan najemnikow (B SpoilsCompany.cs:297, Settings `SpoilsClanRealSoldiers`):** "rosnie tylko z prawdziwego werbunku - notable i karczma" - karczma dalej prawdziwa (weterani). INFO V17.
- **160 (kon wlasnoscia zolnierza):** typ konny tylko z konnych weteranow - premia zoldu x1.5 tylko dla nich. RecruitCost kon z polki nieczynny - bez kolizji.
- **E4/7 punkt zdjecia:** karczma przestaje zdejmowac stan miasta (Z8) - inaczej pobor z DOM zawyzony o najem. **E4/7 hamulec:** nie dotyczy karczmy (0 pkt 12).
- **E4/3 pula wyrzutkow:** dezerterzy i czesc rozbitych dalej do lasu (OutlawRoutedShare - B OutlawLaw.cs:528); powrot z puli bez karczmy - tempo wojny E4/3 bez zmian.
- **DTE:** komplet gracza z karczmy z niczego (DTE :29-44) - istniejace, nie pogarszamy (pytanie 2). BEE: `ApplyRecruitmentDrain` wychodzi przy BK (BEE :5194).
- **ROT:** jedyna latka na karczmie - ukrycie menu w zaciagu (ROTd EnlistmentPatches.cs:221-229); BK nie lata karczmy (grep `MercenaryData` w BK: brak).
- **SandBox:** nasza synchronizacja zmienia typ/liczbe -> `CheckIfMercenaryCharacterNeedsToRefresh` (SB :38-49) tylko gdy gracz w tym miescie i poza misja - dzieje sie
  w ticku dobowym na mapie (bez misji). Bez ryzyka CTD (to nie `SwitchToMenu` z menu oczekiwania).

### 10.4 Martwy kod / nowa praca / skutki ilosciowe
- Gra przestaje losowac karczmy - pole `_selectedTroop` (RCB:132) i `FindRandomMercenaryTroop` nieuzywane (zapisywane przez gre - bez skutku).
- **Wojsko:** najem z karczmy to ok. 15% werbunku (z30A: 200 z ok. 1 339/d); lordowie po pustej karczmie werbuja od notabli (RCB:500-502) - wiecej ochotnikow,
  szybciej maleje pula BK (hamulec BK E4/7, prog odlozonego dopasowania BEE - P rozdz. 7). Jeff 09.10 C "nie zmniejszamy wojska": przy V12 NIE najpierw sprawdzic hamulce
  E4/7 (notable), nie karczme; proba `VeteranMinTier` 0 tylko jako pomiar.
- **Zloto do miast:** z30A ok. 43.9 tys. zl/d za najem; przy -70..-85% najmu ok. 30-37 tys. zl/d mniej do kas miast, wiecej u notabli (VolunteerFromIndividual - LevyGold) i w kiesach
  lordow. Suma zlota bez zmian (nic nie powstaje ani nie znika); kasy miast - regulator gry (SP 08.10 "regulator kasy") - INFO V14, wazne dla etapu 5.
- Nowa praca: Sync 97 miast/dobe, `TownFor` 97 miast na wejscie (ok. 100-600 wywolan/d) - ponizej 1 ms.

### 10.5 Kolejnosc w dobie
OutlawLaw.Daily (B ArmouryBehavior.cs:1509 - powroty z puli, bez karczmy) -> ClanBudget.Daily (:1514 - zwolnienia 166 -> W1) -> OthersRise.Daily -> PeopleBook.Daily (:1519:
EnsureSown E4/7 -> ReconcileDaily -> **VetDaily** (siew, powroty, Sync, linia) -> cache R5 -> linie) -> Measure40.Daily (:1553 - Hearth czyta MBackVeteran) -> PeopleLedger.Daily
(:1554 - "Ludzie:" z karczmy). Gra: `DailyTickTown` (RCB:205-208) przy VetLive nic nie robi (L1) - kolejnosc obojetna. Najem i wejscia H3/I1 - w ciagu doby (zdarzenia).

### 10.6 Gracz = AI
Ta sama karczma i ten sam najem (E1 dla obu); gracz w W4c (OverflowHome liczy `_homePlayer` - B OverflowHome.cs:149) i w W4 (zostawieni po jego bitwie - PrisonerLaw.Free).
Zwolnienie gracza na ekranie druzyny - P2 (E4/7 dzis go nie odsyla do domu - Reconcile "w nicosc"; gdy E4/7/E4/8 doda R6 dla gracza, ta sama `VetDivert`).

### 10.7 Wyjatki (U24) - lista miejsc do recenzji
ClanBudget.ToVillage :803-813 (VetPlan przed BK wsi, zapis po); OutlawLaw.cs:648; LosersFlee.cs:1115-1119; PrisonerLaw.cs:197, :449; E4/7 postfiks rozwiazania; `VetDivert`
dopisuje do puli jako ostatni krok, nigdy nie rzuca; `OnTavernNumber` we wlasnym try (zdarzenie gry - wyjatek przerwalby `ChangeMercenaryCount` w srodku ApplyInternal).

---

## 11. NK (co dziala dopiero w nowej kampanii)
- **Pula startowa = "starzy zolnierze" (SP:711):** w nowej kampanii to poczatkowe karczmy gry (RCB:167-177), zdjete ze stanu ludzi miast. W starym zapisie siew bierze to,
  co stoi w karczmach w dniu wlaczenia (tez nazwane, tez ze stanu miasta) - pelny obraz "od poczatku" tylko w nowej kampanii.
- Reszta dziala w starym zapisie od doby wlaczenia: zolnierze zwolnieni przed wlaczeniem juz sa w domu (bez wstecz).
- Cofniecie DLL: starszy Armoury pomija `vs/vt/vh` - pula (ok. 1-3 tys.) znika z ksiegi, karczmy wracaja do gry. Ponowne wgranie = nowy siew z karczm (bez skoku hearth).
- Wylaczenie w MCM w trakcie kampanii: 5 (wygaszanie, powroty trwaja).

---

## 12. Pytania otwarte do Jeffa (tylko kierunek; reszte decyduje plan)

**ROZSTRZYGNIETE (Jeff 11.10 ok. 09:30): pytanie 1 - TAK (rekrut tier 1-2 zawsze do domu, `VeteranMinTier` 3); pytanie 2 - osobny krok po etapie 4 (bez zmian w E4/10).**
1. **Swiezy rekrut zwolniony z wojska (tier 1-2) - zawsze do domu, czy tez co trzeci do karczmy?** W karczmie stalby jako najemnik tieru 3 (taki jest najnizszy najemnik w ROT),
   czyli dostalby umiejetnosci bez cwiczen. Plan: do karczmy tylko zolnierz od tieru 3 (`VeteranMinTier` 3) - wtedy najemnikow w karczmach ok. 70-85% mniej niz dzis; bez progu
   ok. 5-25% mniej. Rada: z progiem.
2. **Sprzet weterana:** dzis zbroja i bron zwolnionego zostaja w zbrojowni jego pana, a najemnik z karczmy przychodzi z dobytkiem tieru 1 i koniem (kon - po E4/10 tylko konny
   weteran). Zamknac obieg tak, zeby zwolniony zabieral swoj komplet ze zbrojowni, a nowy pan dostawal go razem z nim? Rada: tak, ale jako osobny krok po etapie 4 (to ksiega
   sprzetu, nie ludzi).
- **Informacja (bez pytania):** liczba "najemnikow o 81-97% mniej" z projektu (05.10) jest nieaktualna - od tego czasu lordowie zwalniaja (166) i rozbici wracaja do domu,
  wiec bylych zolnierzy jest ok. 3 razy wiecej; dokladny wynik pokaze test (V8). Zwolniony najemnik idzie ta sama regula co kazdy zolnierz (1/3 karczma, 2/3 dom).

---

## 13. Kolejnosc kodowania (galaz `w-toku/e4-10` od czubka E4/7 po recenzji; kazdy krok: `cd` do projektu, build Release kod 0, osobny commit; jeden test na koncu)
1. **K1 konto bez skutku w grze:** `PeopleBook.Veterans.cs` (2.1-2.5: stan, `vs/vt/vh` w Export/Import, Reset, `VetByRegion`, `VetWorld`, przyczyny Z12, linia dzienna z pula 0),
   klucze `VeteransEnabled` (na czas K1 false w autotescie), `VeteranShareToTavern`, `VeteranWaitDays`, `VeteranMinTier`.
2. **K2 karczma z puli:** `Veterans.cs` - L1-L4, E1 (nasluch w RegisterEvents), `_self`, `TypeFor`, `TownFor`, `Sync`, siew (2.3), najem (`VetHire` + `Give`), linia startowa;
   Z8 w kodzie E4/7 (karczma milknie). Pula rosnie tylko ze startu - autotest 40 dob: V2-V5, V7 (karczmy pustoszeja - oczekiwane).
3. **K3 wejscia i powroty:** `VetDivert` + `ClanBudget.BkPopAdd`, W1-W5 (3.2), powroty (5), dopiski linii (3.3, 8.4: H3, I1, 166, Ludzie (ksiega), Pochodzenie, Hearth).
4. **K4 menu i etykiety:** `PeopleMenu` (rozdz. 7), RecruitKit etykiety (rozdz. 6).
5. **K5:** `Settings.cs` (komentarze po polsku bez znakow, podpowiedzi EN) + `python tools/gen_mcm.py`, parser `etap4_e410` (8.5, progi rozdz. 9), CHANGELOG (Problem: "karczma z niczego"
   z30A 200 ludzi/d i 43.9 tys. zl/d do miast, kon najemnika z wzorca "do 167"; Przyczyna; Zmiana; Odstepstwa: prog tieru 3, najemnik zwolniony ta sama regula, sakiewka i sprzet
   bez zmian, powrot z puli wyrzutkow bez karczmy; Ryzyko = rozdz. 10; Status DO SPRAWDZENIA), dwie recenzje diffu (ksiega ludzi; kod i kolizje), potem test zbiorczy 120 dob + zapis.
