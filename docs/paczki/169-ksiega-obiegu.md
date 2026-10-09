# Paczka 169 - KSIEGA OBIEGU (sam log) - specyfikacja do wykonania

Status: PROJEKT PO KRYTYCE (08.10.2026, noc) - 16 uwag sprawdzonych w kodzie, odpowiedzi w rozdz. 14. Drzewo: galaz `w-toku/169-ksiega-obiegu` na 2e235ea (Armoury c01a54ba w grze).
Podstawa: `docs/PROJEKT-EKONOMIA-OBIEG-2026-10-08.md` (repo Jeffa) rozdz. 4.1, 4.2, 6.1, 8, 9, 10.1, 12 wiersz 1, 14.1;
rozpoznanie `docs/rozpoznanie-2026-10-08/paczka-169-wynik-1.md` (nasz kod), `-wynik-2.md` (BK i BEE), `-wynik-3.md` (gra 1.4.8 i NavalDLC).
Numery linii ponizej: drzewo 2e235ea, dekompilacje `scratchpad/ore-supply/{cs,bk,be}` i NavalDLC ze scratcha fa2fd7a6.

Paczka 169 NICZEGO NIE ZMIENIA W GRZE: zadnego przesuniecia zlota, towaru ani ludzi, zadnej zmiany decyzji AI, zadna latka
nie zwraca false i nie zmienia `__result`/`ref`. Wszystko to odczyt stanu przed i po metodzie, liczniki zdarzen i linie logu.
Jedyny nowy zapis w save: `arm_clanincome` (D na rod) - tez tylko dane do logu (paczki 166/168 dopiero beda go uzywac).

Cel (Jeff: "jak znika to zamykamy... wszystko z czegos wynika"): dzisiejsza reszta niezmierzona zlota swiata (-144..-218 tys.
na dobe) ma sie rozlozyc na NAZWANE przyczyny, a w logu ma stac tozsamosc: suma nazwanych przyczyn + reszta = zmiana zlota
swiata co do zlotowki (ta sama doba, ci sami posiadacze). Prog akceptacji (rozdz. 9): reszta < 10 000 na dobe (srednia 28 dob).
Tozsamosc jest prawdziwa z budowy (reszta = to, czego nie nazwano), wiec SAMA niczego nie dowodzi. Ze nazwane pozycje sa
prawdziwe, sprawdzaja trzy niezalezne pomiary: probki swiata wokol okien (2.4, test T16), reszta w naszym ticku `RB` ~ 0 (T15)
i rozbicia "w tym", ktore nie moga przekroczyc calosci (T4).

---

## 0. Spis tresci

1. Decyzje glowne (kazda z uzasadnieniem)
2. Model ksiegi: tozsamosc i reguly "liczone / nie liczone"
3. Okna pomiaru (zakres a) - tabela i opis kazdego okna
4. Pozycje swiadomie pominiete (z powodem)
5. Zmiany w istniejacych plikach (dokladnie gdzie i co)
6. Linie dzienne (zakres b) - format i przyklad kazdej linii
7. D na rod (zakres c)
8. Budzet rodow i dlugi na sucho - rachunek
9. Wylaczniki MCM (zakres d)
10. Wydajnosc (zakres e)
11. Testy autotestu 40 dob (zakres f) i narzedzie `tools/obieg169_sprawdz.py`
12. Kolejnosc pracy wykonawcy i kontrola "sam log"
13. Ryzyka
14. Krytyka i odpowiedzi

---

## 1. Decyzje glowne

| # | Decyzja | Uzasadnienie (jedno zdanie) |
|---|---|---|
| D1 | Trzy nowe pliki: `Armoury/src/CirculationWindows.cs` (okna Harmony, nasluchy, liczniki), `Armoury/src/ClanIncomeBook.cs` (D na rod, budzet i dlugi na sucho, CSV), `Armoury/src/MoneyLedger.Obieg.cs` (nowe linie dzienne jako `partial` ksiegi). | Okna, rachunek rodow i druk linii to trzy rozne odpowiedzialnosci, a druk musi czytac prywatne liczniki `MoneyLedger` bez wystawiania ich na zewnatrz. |
| D2 | `MoneyLedger` staje sie `internal static partial class MoneyLedger` (jedyna zmiana deklaracji). | Plik `MoneyLedger.Obieg.cs` ma dostep do `_wage`, `_cons`, `_mark` itd. bez kopiowania ani nowych akcesorow. |
| D3 | STARE linie (`Pieniadz swiata:`, `(bilans):`, `(rody):`, `Przeplywy osad...`) zostaja bajt w bajt takie jak dzis; rozbicie idzie do NOWYCH linii wypisywanych zaraz po nich. Jedyny wyjatek: gdy cudzy kod rzuci wyjatek w srodku rozliczenia rodu, finalizer D16 zamyka okno rodu od razu (dzis zostaje otwarte do nastepnego rodu). | Skrypty roku (`krytyk\rok_log.py` i inne) parsuja stare linie, a stara "reszta" w nowej linii pozwala sprawdzic refaktor `OldBalance` (T2); wyjatek dotyczy tylko przypadku, w ktorym stare liczby i tak sa dzis zle przypisane. |
| D4 | Trzy techniki pomiaru, wszystkie O(1) na wywolanie: (a) flaga okna + istniejace zdarzenie `HeroOrPartyTradedGold`, (b) migawka 1-6 kies przed i po metodzie, (c) linie wyniku czynnego modelu finansow raz na rod na dobe. | Tak radzi rozpoznanie 3 (tabela C): flaga nie czyta kies, migawka widzi zmiany bez zdarzenia, linie modelu daja trybut/dlug/rade bez latania malych metod prywatnych. |
| D5 | `GiveGoldAction.ApplyInternal` NIE jest latany. | Zdarzenie gry juz niesie platnika, odbiorce i kwote po przycieciu, a latka dolozylaby narzut do najczestszej metody pieniedzy. |
| D6 | Kazdy prefiks okna ma `priority = Priority.First`; koniec okna to FINALIZER (nie postfiks); prefiks flagowy nie ma parametrow typu referencyjnego, gdy nie musi. | Harmony 2.4.2 pomija prefiks z parametrem referencyjnym po prefiksie zwracajacym false (BK, BKROTPatch, nasze SkipIfOn), a finalizer biegnie zawsze, takze po wyjatku. |
| D7 | Wynik okna, ktore biegnie W SRODKU rozliczenia rodu (`MoneyLedger.ClanOpen`), nie wchodzi do bilansu swiata (tylko do informacji rodow). | Rozliczenie rodu jest juz zmierzone w calosci roznica stanu swiata (`_clanUp/_clanDown`) - drugie liczenie zepsuloby tozsamosc. |
| D8 | Okna dzialaja tylko na watku glownym (`Environment.CurrentManagedThreadId == _main`, `_main` zapisany w `ApplyAll`); inne watki tylko licznik `poza watkiem`. | Statyczna flaga okna nie jest bezpieczna watkowo, a pieniadze gra rusza na watku glownym (wzor `GoodsLedger._main`). |
| D9 | Dzien we wszystkich nowych liniach = `(int)CampaignTime.Now.ToDays - 1` (jak `Pieniadz swiata`); w CSV dodatkowo `dzien_gry = (int)CampaignTime.Now.ToDays`. | Nowe linie opisuja te sama dobe co ksiega pieniadza, a CSV musi sie laczyc z `economy-*.csv` CrashScribe, ktory pisze `(int)ToDays`. |
| D10 | Nasz tick dobowy (od `MoneyLedger.BlockOpen` do konca `MoneyLedger.Daily`) dostaje wlasny pomiar zlota swiata (jeden dodatkowy `WorldTotal()` na dobe). | Reszta rozpada sie wtedy na "w naszym ticku" i "poza nim" - widac od razu, czy dziurawi nasz kod, czy cudzy. |
| D11 | D na rod liczy sie zawsze przy `ClanIncomeBookEnabled` (takze przy wylaczonym logu); okna 169 tylko przy `CirculationLedgerEnabled && LogEnabled`. | D ma miec historie 28 dob, gdy 166 zacznie go uzywac, a okna sluza wylacznie logowi. |
| D12 | Stale budzetu (6.4) i dlugow (8.2) jako `const` w `ClanIncomeBook` z komentarzem - bez nowych kluczy MCM. | To przebieg na sucho; klucze MCM dostanie paczka 166/168, ktora zacznie te liczby stosowac (zasada "jedna zmiana naraz"). |
| D13 | Nasze wlasne przeplywy kas bez licznika (rozpoznanie 1, pkt 4 koniec) dostaja `MoneyLedger.Note169` z WLASNA tablica liczb, pokazywana tylko w nowej linii `Przeplywy osad (przyczyny)`. | Cel 4.2 obejmuje tez reszte kas miast, a osobna tablica nie zmienia starej linii `Przeplywy osad (kasy miast)`. |
| D14 | Pozycje dotad liczone w "z niczego / w nicosc" dostaja tylko rozbicie "w tym" (suma czesci + "inne" = calosc); nowa pozycja bilansu powstaje tylko dla zmian BEZ zdarzenia (dotad w reszcie). | Zasada rozpoznania 1 pkt 5i: zadna kwota nie moze byc liczona dwa razy. |
| D15 | Prawdziwa kontrola okien = probka swiata wokol okna (2.4): najwyzej 8 probek na dobe (16 pelnych przegladow swiata), kazde okno najwyzej raz na 3 doby, osobny wylacznik MCM. | Tozsamosc w linii jest prawdziwa z budowy; tylko porownanie zmiany CALEGO swiata w oknie z pozycjami nazwanymi w tym oknie wykrywa okno, ktore liczy podwojnie, za malo albo ze zlym znakiem, a limit 16 przegladow to ok. 2.6% dzisiejszych 618. |
| D16 | Okno rodu (`DailyTickClan`) dostaje FINALIZER, ktory po wyjatku w cudzym kodzie zamyka okno (`_clanNow = null`, `_clanStale++`); postfiks pomiaru zostaje bez zmian. | Dzis po wyjatku `ClanOpen` zostaje prawda do konca tej chwili gry, wiec zdarzenia i okna 169 (D7) bylyby pomijane; licznik `_clanStale` rosnie tak samo jak dzis (zamiast w nastepnym prefiksie - w finalizerze). |
| D17 | Kazda nowa metoda wolana z ISTNIEJACEGO haka (`OnGold`, `NoteCounted`, `NoteLevy`, `OnEvent`, `WorkshopIn/OutPre/Post`, `NoteInflow`, `NoteWage`, `NoteModelSaldo`, `ClanTickOpen/Close/Abort`, `Note169`, `NotePurseGone`, `NoteMineWages`, `ClearLast169`) ma WLASNY try/catch ze `Stumble`. | Wyjatek w nowym kodzie nie moze pominac starego liczenia (D3) ani wyjsc do gry przez cudzy hak (np. `WorkshopTrade.OutPostfix` przed `ArtisanInputs.OutPost` - wsad rzemieslnikow 147). |
| D18 | Kapital nowej karawany = kwota NADANA (pierwszy wynik `GetInitialTradeGold` czynnego `CaravanModel` w oknie `CreateCaravanParty`), nie stan kasy po metodzie. | Po `InitializePartyTrade` metoda wchodzi do osady (`CaravanPartyComponent.cs:248-252`), a sluchacze wejscia zdejmuja z kasy oplaty, dochod BK dla wlasciciela i werbunek - stan po metodzie zanizalby zrodlo. |
| D19 | D bierze dochod BRUTTO modelu (`CalculateClanIncome`, przed zoldem); saldo modelu (`CalculateClanGoldChange`, z O40) drukujemy obok w CSV i w linii budzetu. | Saldo po zoldzie robiloby pulap zoldu zalezny od samego zoldu (rod z wiekszym wojskiem dostawalby nizszy pulap - sprzezenie), a brutto to definicja juz uzywana przez 3 nasze moduly. |
| D20 | Wszystkie liczby dnia `Last*` (KingdomTreasury, IronBank, SoldierPay) i `Day*` rent zeruje JEDNO miejsce: `MoneyLedger.ClearLast169()` jako pierwsza instrukcja `BlockOpen` (we wlasnym try). | Liczby nie moga przejsc na nastepna dobe, gdy jakis modul bloku nie zadziala albo wyjdzie wczesnym `return` - jedno miejsce zamiast pilnowania wejscia kazdej metody. |
| D21 | Budzet i dlugi na sucho NIE wolaja `IronBank.Limit`; limit Banku liczymy z tego samego wzoru na dochodzie (a) policzonym raz na rod na dobe. | `Limit` liczy dochod modelu drugi raz i dla kazdego bankruta przeszukuje wszystkie rody (`Clan.FindFirst`, `IronBank.cs:114-119`) - O(rody x bankruci x rody) na dobe. |

---

## 2. Model ksiegi: tozsamosc i reguly

### 2.1 Tozsamosc (nowa linia `Pieniadz swiata (bilans - przyczyny)`)

Oznaczenia (wszystko `long`, za jedna dobe ksiegi):
- `Z` = zmiana sumy posiadaczy (ta sama co w `BalanceLine`, `MoneyLedger.cs:669-673`).
- `S0`, `U0` = dzisiejsze zrodla i ujscia z `BalanceLine` (`:690-691`), `R0 = Z - S0 + U0` (dzisiejsza reszta).
- Nowe zrodla bez zdarzenia (dotad w reszcie): `Z1` lup z cial dla AI, `Z2` kapital NADANY nowym karawanom (D18), `Z3` BK sprzedaz
  niewolnikow, `Z4` zloto bohaterow, ktorzy wrocili do swiata (stan Disabled -> zywy), `Z5` myto (perk Tollgates zarzadcy miasta dopisuje
  licznik cel przy kazdym wejsciu karawany - i to dwa razy, bo zyja oba sluchacze: vanilla `CaravansCampaignBehavior.cs:726-728`
  i BK `BKCaravansBehavior.cs:1199-1201`; BK lata tylko `HourlyTickParty` i `DoInitialTradeRuns` vanilla).
- Korekta zrodel: `K1` = przelew z kapitalu warsztatow i kas karawan notabli, ktory gra zglasza jako zloto z niczego (wplata zdarzeniem
  `nic -> notabl` jest w `from`, a ubytek kapitalu dotad siedzial w reszcie ze znakiem minus).
- Nowe ujscia bez zdarzenia: `U1` zold karawan notabli, `U2` prowizja od sprzedazy partiom (czesc w nicosc), `U3` BK porty,
  `U4` BK kopalnie (netto po placach gornikow), `U5` BK konwoje ludnosci, `U6` BK wydatki lordow (12 pozycji, w tym kupno karawan przez
  lordow BK - `BKLordPropertyBehavior.cs:78`), `U7` zloto bohaterow, ktorzy opuscili swiat (smierc, Disabled), `U8` kiesy partii bez wodza,
  ktore zniknely z mapy, `U9` clo zdjete z licznika cel - CALA kwota `x` (nasz `KingdomTreasury.Levies`: licznik cel jest posiadaczem
  swiata, `MoneyLedger.cs:622`, i spada o `x`, `KingdomTreasury.cs:263`; wplata `paid` kasa miasta -> skarbiec to zwykly przelew, `:264`).
- Pozycje netto (ze znakiem, zmiana zlota swiata): `N1` BK rynek osady (HandleMarketGold), `N2` BK rynek wsi (HandleVillage),
  `N3` warsztaty BK - zaplata bez pokrycia w kasie, `N4` bitwy - zloto partii bez wodza (bez zdarzenia), `N5` przelewy od i do
  bohaterow poza swiatem, `N6` skarbce krolestw, ktore upadly (`-portfel`), `N7` warsztaty kupione przez lordow BK - kapital ustawiony
  od nowa (`BKLordPropertyBehavior.cs:117-119` -> `Workshop.ChangeOwnerOfWorkshop`, `Workshop.cs:140`: stary kapital znika, nowy powstaje).

Wzory:
```
S  = S0 - K1 + Z1 + Z2 + Z3 + Z4 + Z5
U  = U0 + U1 + U2 + U3 + U4 + U5 + U6 + U7 + U8 + U9
N  = N1 + N2 + N3 + N4 + N5 + N6 + N7
R  = Z - S + U - N            (reszta po oknach 169 - z DEFINICJI to, czego nie nazwano)
R0 - R = (Z1+...+Z5) - (U1+...+U9) + N - K1      (arytmetyka linii - zawsze prawdziwe)
```
Obie rownosci sa tozsamosciami algebraicznymi (`R` i `R0` sa liczone z tych samych skladnikow). W linii zostaja, bo Jeff ma widziec
"suma przyczyn + reszta = zmiana swiata", ale test T1/T2 sprawdza nimi tylko druk, parser i refaktor `OldBalance` - NIE brak podwojnego
liczenia. To sprawdzaja: probki swiata wokol okien (2.4, T16), `RB` ~ 0 (T15), "inne" >= 0 (T4) i wielkosc `R` (T3: okno liczace
podwojnie albo ze zlym znakiem przesuwa `R` o swoja kwote w strone plusa albo minusa).
`S0`, `U0` liczy JEDNA nowa prywatna metoda `OldBalance(long[] now, long[] last, out long z, out long s0, out long u0)` wydzielona
z `BalanceLine`, wolana przez stara i nowa linie (zero rozjazdu definicji). `BalanceLine` po refaktorze daje identyczny tekst.

Reszta dzieli sie na `RB` (w naszym ticku dobowym) i `RP` (poza nim): `RB = (suma posiadaczy na koniec bloku - WorldTotal() przy BlockOpen)
- _blockNamed`, `RP = R - RB`, gdzie `_blockNamed` to suma (ze znakiem: zrodlo +, ujscie -) wszystkich pozycji `from/to`, `Z*`, `U*`,
`N*` zapisanych, gdy `_inBlock == true`. Suma posiadaczy na koniec bloku to suma `hold` z `MoneyLedger.Daily` (ten sam odczyt).
`RB` to NIEZALEZNY pomiar (dwa odczyty calego swiata wokol naszego bloku minus pozycje nazwane w bloku): w bloku biegnie tylko nasz kod,
wiec `RB` != 0 oznacza dziure w Armoury albo zle nazwana pozycje (test T15).

### 2.2 Kiedy zdarzenie albo okno "liczy sie" do bilansu swiata

| Sytuacja | Do bilansu swiata? | Dlaczego |
|---|---|---|
| W srodku rozliczenia rodu (`ClanOpen`) | NIE (tylko informacja w linii rodow) | `_clanUp/_clanDown` mierzy juz cala zmiane swiata w tym oknie. |
| W naszym ticku dobowym (`_inBlock`), zdarzenie `nic -> osada` / `osada -> nic` | NIE dla flag (ksiega dzis tego nie liczy, `MoneyLedger.cs:275,283`) | Rozbicie "w tym" ma sie sumowac do liczonej kwoty `from/to`. |
| W `_inBlock`, wszystko inne (migawki, `nic -> bohater`) | TAK | Swiat sie zmienia, a migawki kas bloku (`Mark`) mierza tylko kasy osad. |
| W oknie powrotu taboru do wsi, wyplata majatku BK | jak dzis (`_winEstates`) | Bez zmian. |

W kodzie: w `MoneyLedger.OnGoldTraded` po wyliczeniu `gc`, `rc`, `clan` (`:268-272`):
```
bool counted = clan == null && (gNone ? (rc < 0 || !_inBlock) : rNone ? (gc < 0 || !_inBlock) : true);
```
(`counted` dla przelewow miedzy posiadaczami nie ma znaczenia dla flag - flagi licza tylko `gNone || rNone`.)

### 2.3 Swiat = posiadacze z `ReadHolders` (bez zmian)

Bohater "poza swiatem" = `h.HeroState == Hero.CharacterStates.Dead || h.HeroState == Hero.CharacterStates.Disabled` (dokladnie
definicja `Campaign.AliveHeroes`, `CampaignObjectManager.cs:484-494`). Pomocnik `internal static bool OutOfWorld(Hero h)` w
`CirculationWindows`. Partia "w swiecie" = jak `ReadHolders` (`:629-637`): aktywna, a jej zloto liczy sie osobno tylko, gdy
`!(mp.IsLordParty && mp.LeaderHero != null)`.

### 2.4 Probka swiata wokol okna (prawdziwa kontrola okien, D15)

`NamedRun` (`long`, w `CirculationWindows`) = biegnaca suma WSZYSTKICH pozycji bilansu nazwanych w chwili, gdy sie dzieja (ze znakiem:
zloto powstalo +, zniknelo -). Dopisuja do niej, kazde we wlasnym try (D17):
- `AddWorld` (kazda pozycja `Z*`, `U*`, `N*`, `K1`, gdy zapisana - czyli poza rozliczeniem rodu);
- `MoneyLedger.OnGoldTraded` dla zdarzen liczonych w `from/to` (`counted && (gNone || rNone)`): `gNone` +a, `rNone` -a;
- `MoneyLedger.NoteLevyBack` (+kwota; LevyGold oddaje zaplate za werbunek BEZ zdarzenia - biegnie w oknie O06).
Pozycje starej ksiegi z dziennego ticku miasta (konsumpcja `_cons`, regulator `_regIn/_regOut`) i z okna taboru (`_vHanded...`) NIE
dopisuja: biegna poza wszystkimi oknami 169 (regulator to wynik modelu, nie chwila zmiany zlota), a probka pomija okno taboru.

Probka (koszt: dwa `WorldTotal()`): w prefiksie okna, gdy bramka okna przepuscila, `ProbeArm(w)` jest prawda, gdy
`ProbeOn` (= `On && Settings.Current.CirculationProbeEnabled`) i `!_probeOn` (bez probek zagniezdzonych) i `!MoneyLedger.InBlock`
i `!MoneyLedger.InClanTick` i `!MoneyLedger.WinOpenNow` (okno taboru) i watek glowny i `_probesToday < 8` i
`dzis - _probeDay[w] >= 3`. Wtedy stan okna dostaje `P = true; W0 = MoneyLedger.WorldNow(); N0 = NamedRun`, a `_probeOn = true`,
`_probeDay[w] = dzis`, `_probesToday++`. Finalizer (gdy `P`): `dw = WorldNow() - W0`, `dn = NamedRun - N0`, `ProbeN[w]++`, `ProbeNSess[w]++`,
`if (dw != dn) { ProbeBad[w]++; ProbeBadSess[w]++; ProbeDiff[w] += dw - dn; }`, `_probeOn = false`, czas obu przegladow do `ProbeTicks`.
Wyjatek w probce: `Stumble`, `_probeOn = false` w `finally` (probka nigdy nie zostaje "otwarta" na reszte doby).
Nasluchy O37-O39 zapisuja pozycje przez `AddWorld`, wiec smierc bohatera albo zniszczenie partii W SRODKU probkowanego okna jest nazwane.
Okna bez wlasnego prefiksu (nasluchy O37-O39, linie modelu O40) nie maja probek - sa chwilowe albo tylko informacyjne.
Znaczenie: w oknie zmiana CALEGO swiata musi byc rowna temu, co nazwalismy w tej samej chwili - okno, ktore liczy podwojnie, za malo,
ze zlym znakiem albo przepuszcza zmiane bez nazwy (np. zdarzenie po stronie partii, cudzy sluchacz), daje rozjazd z nazwa okna.
Okno rzadkie (raz na kilka dob) jest probkowane przy pierwszym wywolaniu po 3 dobach; czeste - co 3 doby.

---

## 3. Okna pomiaru (zakres a)

### 3.1 Wspolny szkielet (CirculationWindows.cs)

```csharp
internal static class CirculationWindows
{
    internal static bool On { get { var s = Settings.Current; return s != null && s.CirculationLedgerEnabled && s.LogEnabled; } }
    private static bool Live { get { var c = Campaign.Current; return c != null && c.GameStarted; } }
    private static int _main;                         // watek glowny (ApplyAll)
    private static Harmony _harmony;

    // okno flagowe: przyczyna, oczekiwana strona, poprzednie okno (zagniezdzenie), probka czasu (1/256), probka swiata (2.4)
    internal struct Ctx { public int Kind; public Hero Side; public bool Active; public long T0; public bool P; public long W0, N0; }
    private static int _ctx;                          // 0 = brak
    private static Hero _ctxSide;                     // null = dowolna strona

    // liczniki doby (zerowane w ClearDay(), ktore wola MoneyLedger.ClearDay)
    internal static readonly long[] In = new long[Kinds], Out = new long[Kinds];   // rozbicie from/to wedlug przyczyny
    internal static readonly long[] Sum = new long[Items];                          // pozycje Z*, U*, N*, K1 i informacje
    internal static readonly long[,] Cls = new long[3, ClsItems];                   // pozycje linii kas (miasta/zamki/wsie)
    internal static readonly int[] Calls = new int[Windows], Hits = new int[Windows];
    internal static readonly long[] Ticks = new long[Windows]; internal static readonly int[] Sampled = new int[Windows];
    internal static readonly bool[] Wired = new bool[Windows];                      // latka wpieta (Reset tego NIE czysci)
    internal static int OffThread, Stumbles, Nested, InClanSkipped;
    internal static long BlockNamed;                                               // _blockNamed z 2.1
    internal static long NamedRun;                                                 // 2.4 - biegnaca suma pozycji nazwanych (nie zerowana w ClearDay)
    internal static readonly int[] ProbeN = new int[Windows], ProbeBad = new int[Windows];
    internal static readonly long[] ProbeDiff = new long[Windows]; internal static long ProbeTicks; private static int _probesToday;
    internal static readonly int[] ProbeNSess = new int[Windows], ProbeBadSess = new int[Windows];   // od startu sesji (Reset, nie ClearDay) - do 6.8 i T16
    private static readonly int[] _probeDay = new int[Windows];                    // ostatnia doba probki okna (Reset: -1000)
    private static bool _probeOn;
    ...
}
```
- Indeksy (`const int`) i nazwy polskie (`static readonly string[]`) wedlug tabel 3.2-3.4. `Kinds`, `Items`, `ClsItems`, `Windows` = liczba wpisow.
- `Reset()`: zeruje wszystko poza `Wired` i `_harmony` (latki zostaja w procesie, jak `SoldierPay`), `_ctx = 0`, `_ctxSide = null`, stany migawek
  (`_snapHero`, `_snapLeaders`, `_sellSt`, `_capOpen`, `_wsW`), `NamedRun = 0`, `ToCounterRun = 0`, `ProbeNSess`, `ProbeBadSess`, `_probeOn = false`, `_probeDay[*] = -1000`.
- `ClearDay()`: zeruje `In`, `Out`, `Sum`, `Cls`, `Model`, `Calls`, `Hits`, `Ticks`, `Sampled`, `OffThread`, `Stumbles`, `Nested`, `InClanSkipped`, `BlockNamed`,
  `ProbeN`, `ProbeBad`, `ProbeDiff`, `ProbeTicks`, `_probesToday` (nie `NamedRun` - liczy sie tylko roznica w oknie; nie `_probeDay`).
- Kazde cialo prefiksu/finalizera: `try { ... } catch (Exception e) { Stumble("CirculationWindows.<nazwa>", e); }`; `Stumble` liczy
  `Stumbles++`, pierwszy wyjatek na sesje do `Log.Error` (wzor `MoneyLedger.ClanStumble :333-339`). Nigdy nie wylaczamy okna po bledzie.
  To samo dotyczy KAZDEJ metody wolanej z istniejacego haka (D17): metoda sama ma try/catch, wolajacy nie musi go dokladac i nic nie
  zmienia w kolejnosci swoich starych instrukcji.
- Bramka na poczatku kazdego prefiksu: `if (!On || !Live) return;` + watek (D8). Gdy bramka nie przepuscila, `__state` = "nieczynne"
  (`Active`/`On` = false) i finalizer NIC nie robi - niczego nie liczy i niczego nie przywraca (stan okna zewnetrznego zostaje nietkniety).
- Pomiar kosztu (zakres e): licznik `Calls[w]++`; gdy `(Calls[w] & 255) == 0` prefiks zapisuje `Stopwatch.GetTimestamp()` w polu `T0`
  stanu (jest w `Ctx`, `CaravanState`, `SellState`, `SnapState` i stanach migawek `int`, ktore przechodza na male struktury `{ int V; long T0; bool P; long W0, N0; }`),
  finalizer dodaje roznice do `Ticks[w]` i `Sampled[w]++` (probka 1/256).
  POPRAWKA PO RECENZJI: przedzial prefiks-finalizer mierzyl metode gry i cudze latki. W kodzie: znacznik czasu w bramce (`Gate`, co 16. wywolanie od 1.),
  `CostStart` zamyka odcinek prefiksu (przed `ProbeOpen`), finalizer mierzy swoje cialo od `CostResume` do `CostEnd` (przed `ProbeClose`) - suma do `Ticks[w]`;
  osobno nasluch `OnGold` (probka 1/256). "przeliczenie doby" = suma czasow samych nowych linii + `ClanIncomeBook.Daily`.
- Probka swiata (2.4): w prefiksie po bramce `if (ProbeArm(w)) { P = true; W0 = MoneyLedger.WorldNow(); N0 = NamedRun; }`; w finalizerze
  `if (P) ProbeClose(w, W0, N0)` jako OSTATNIA instrukcja (po zapisaniu pozycji okna).
- `Hits[w]++`, gdy okno zmierzylo niezerowa kwote.

Flaga (okna typu F):
```csharp
public static void FlagFin(Ctx __state) { if (__state.Active) { _ctx = __state.Kind; _ctxSide = __state.Side; /* + koszt i probka */ } }
// prefiks: gdy bramka przepuscila: __state = new Ctx { Kind = _ctx, Side = _ctxSide, Active = true }; _ctx = K; _ctxSide = strona;
//          gdy nie: __state = default (Active = false) - flaga nie zmieniona, finalizer nic nie przywraca
```
Kazdy prefiks flagowy ma wlasna metode (inna przyczyna/strona), finalizer moze byc wspolny (`FlagFin`), bo typ `__state` ten sam.
Gdy `_ctx != 0` w chwili otwarcia: `Nested++` (informacja), nowe okno i tak przejmuje flage, finalizer przywraca stara.

Wpiecie w `MoneyLedger.OnGoldTraded` (szczegoly 5.1): `CirculationWindows.OnGold(gh, gp, rh, rp, a, gNone, rNone, gc, rc, clan != null, _inBlock, counted)`.
W `OnGold`:
1. watek inny niz glowny -> `OffThread++`, wyjscie;
2. N5 (2.3): `if (!inClan) { if (gh != null && OutOfWorld(gh)) N5 += a; if (rh != null && OutOfWorld(rh)) N5 -= a; }` (zapis przez `AddWorld`);
3. flaga: `if (_ctx != 0 && (gNone || rNone) && counted && StronaPasuje())` -> `In[k] += a` (gNone) albo `Out[k] += a` (rNone),
   `Hits[okno]++`; strona: `_ctxSide == null` albo `(gNone ? Eff(rh, rp) : Eff(gh, gp)) == _ctxSide`;
4. okno prowizji (O15): `if (_sellSt != null && rh == null && rp != null && rp.IsSettlement && rp.Settlement == _sellSt) _sellIn += a;`
5. okna migawek bohatera (O22, O24-O34, O45): `if (_snapHero != null) { var er = Eff(rh, rp); var eg = Eff(gh, gp); if (er == _snapHero) _snapEvt += a; if (eg == _snapHero) _snapEvt -= a; }`;
   okno "glowy wszystkich rodow" (O30): to samo z `er`/`eg` i warunkiem `e != null && e.Clan != null && e.Clan.Leader == e`.
   `Eff(Hero h, PartyBase p)` = `h ?? (p != null && p.IsMobile && p.MobileParty != null && p.MobileParty.IsLordParty ? p.MobileParty.LeaderHero : null)` -
   zdarzenie, w ktorym strona jest partia lorda (`hero == null`, np. `ApplyForPartyToCharacter(party.Party, ...)`), zmienia kiese WODZA przez setter
   `PartyTradeGold` (`MobileParty.cs:437-439`, `GiveGoldAction.cs:16-19, 30-32`) - musi byc odjete tak samo jak zdarzenie z bohaterem.
Szybka sciezka: `if (_ctx == 0 && _sellSt == null && _snapHero == null && !_snapLeaders && gh == null && rh == null) return;` przed pkt 2
(zdarzenia samych partii przechodza dalej tylko wtedy, gdy jakies okno jest otwarte - koszt `Eff` placi wiec tylko okno).
Cale cialo `OnGold` w jednym try/catch ze `Stumble` (D17).

`AddWorld(int item, long value, int sign)`: `Sum[item] += value` (wartosc w konwencji pozycji: `U*` dodatnie = zloto zniknelo, `Z*` dodatnie = powstalo, `N*` = zmiana swiata ze znakiem, `K1` dodatnie = przelew zgloszony jako z niczego), a gdy `MoneyLedger.InBlock` -> `BlockNamed += sign * value` (`sign`: +1 dla `Z*` i `N*`, -1 dla `U*` i `K1`). Kazda pozycja bilansu (Z, U, N, K1) jest zapisywana TYLKO przez `AddWorld`; informacje `I*` i `Cls` zwyklym `+=`. Gdy `MoneyLedger.InClanTick` - `AddWorld` NIE zapisuje pozycji (D7), tylko licznik informacji `InClanSkipped++` (wypisywany w kontrolce).
Te sama regule stosuje `CirculationWindows.NoteCounted` (wolana z `MoneyLedger.OnGoldTraded`, 5.1 pkt 3) dla zdarzen liczonych w `from/to`:
`if (counted && (gNone||rNone)) { NamedRun += gNone ? a : -a; if (inBlock) BlockNamed += gNone ? a : -a; }`.
`AddWorld` dopisuje tez `NamedRun += sign * value` (2.4) - w tym samym warunku co pozycja (nie w rozliczeniu rodu).

### 3.2 Rozbicie from/to (okna flagowe F) - przyczyny `Kinds`

| Kind (const) | Nazwa w logu | Kierunek | Okno |
|---|---|---|---|
| KNotableBand | pasmo notabli | in i out | O03 |
| KNotableNew | nowi notable | in | O04 |
| KNotableIncome | wyplata dochodu notablom | in | O01 |
| KUpgrade | awanse | out | O05 |
| KRecruitTavern | werbunek najemnikow | out | O06 |
| KRecruitNotable | werbunek ochotnikow od notabli | out | O06 |
| KRecruitMap | ochotnicy z mapy | out | O06 |
| KShips | statki: sprzedaz osadom i premie zarzadcow / kupno od osad | in / out | O07 |
| KShipsOther | statki NavalDLC inne (zwrot za statki, premia za naprawe) | in | O08 |
| KPrisonersParty | jency sprzedani przez partie | in | O09 |
| KPrisonersFort | jency do kas twierdz | in | O09 (odbiorca osada, `rc >= 0`) |
| KSiege | lup z oblezen | in | O10 |
| KTournament | nagrody turniejow | in | O11 |
| KRaidCapture | jency z rabunkow BK | in | O12 |
| KBeeContribute, KBeeCamp, KBeeInvest, KBeeMarket | BetterEconomy: wplaty do skarbcow zamkow, obozy, inwestycje we wsie, dostep wsi do targu | out | O13 |
| KBattle | bitwy (lup i strata zlota) | in i out | O14 |

"inne" w linii = `from - suma In[*]` oraz `to(przed odjeciem LevyGold) - suma Out[*]`; obie musza byc >= 0 (test T4).

### 3.3 Tabela okien

Kolumny: technika (F = flaga, M = migawka, L = nasluch zdarzenia gry, ML = linie modelu); koszt = wywolan na dobe (rozpoznanie 3) x praca.
Brak typu/metody (np. BK albo BEE wylaczone): `AccessTools.TypeByName` / `AccessTools.Method` zwraca null -> okno nie wpiete,
`Wired[w] = false`, w linii startowej "BRAK (...)" i w liniach dziennych pozycja "-" (nigdy 0 udajace pomiar).

| Okno | Metoda docelowa (pelna nazwa, sygnatura) | Tech. | Posiadacze | Wynik | Koszt | Uwagi |
|---|---|---|---|---|---|---|
| O01 notable-dochod | `TaleWorlds.CampaignSystem.CampaignBehaviors.ClanVariablesCampaignBehavior.DailyTickHero(Hero hero)` private void | F+M | kiesa notabla, `OwnedWorkshops[i].Capital`, `OwnedCaravans[i].MobileParty.PartyTradeGold` | `In[KNotableIncome]`; `K1 += aktywa0 - aktywa1` | kilka tys. wywolan (kazdy bohater); nie-notabl: 2 odczyty bool; notabl (~3 500): 2 x petla `for` po 0-5 aktywach | Tylko gdy `__0.IsActive && __0.IsNotable` (jak gra `:479`). Dwa rozne `DailyTickHero` - brac `typeof(ClanVariablesCampaignBehavior)`. Cel delegata - nie do wklejenia przez JIT. |
| O02 karawany-notabli (wiersz 1b) | `...CampaignBehaviors.NotablesCampaignBehavior.ManageCaravanExpensesOfNotable(Hero notable)` private void (206 B IL) | M | kiesa notabla, `PartyTradeGold` jego karawan | `U1` (z kas karawan / z kies notabli), informacje: doplaty do 5 000, zold nalezny, niedoplata | ~3 500 wywolan, ~600 z karawanami: petla po 1-3 karawanach + `MobileParty.TotalWage` (model zoldu, ~730 na dobe) | Szczegoly 3.5. Nikt tej metody nie lata (BK, BEE, ROT, BKROT, Armoury). |
| O03 pasmo-notabli (wiersz 1) | `...CampaignBehaviors.NotablePowerManagementBehavior.BalanceGoldAndPowerOfNotable(Hero notable)` private void (126 B) | F | - | `In/Out[KNotableBand]` | ~3 500 flag | Strona = notabl. |
| O04 nowy-notabl | `...CampaignBehaviors.NotablesCampaignBehavior.OnHeroCreated(Hero hero, bool isBornNaturally)` private void (173 B) | F | - | `In[KNotableNew]` | kilka | Strona = `__0`. |
| O05 awanse (wiersz 3) | `...CampaignBehaviors.PartyUpgraderCampaignBehavior.ApplyEffects(PartyBase party, TroopUpgradeArgs upgradeArgs)` private void (144 B) | F | - | `Out[KUpgrade]` | setki-3 tys. | Prefiks deklaruje tylko `PartyBase __0` (TroopUpgradeArgs to prywatna struktura). Strona = `__0.Owner` gdy zywy, inaczej `__0.LeaderHero`. NIE latac `UpgradeTroop` (93 B). |
| O06 werbunek (wiersz 3) | `...CampaignBehaviors.RecruitmentCampaignBehavior.ApplyInternal(MobileParty side1Party, Settlement settlement, Hero individual, CharacterObject troop, int number, int bitCode, RecruitmentCampaignBehavior.RecruitingDetail detail)` private void (267 B) | F | - | `Out[KRecruitTavern/Notable/Map]` | 1-3 tys. | Prefiks `(MobileParty __0, RecruitmentCampaignBehavior.RecruitingDetail __6, out Ctx __state)` - typowany enum, bez pakowania. `VolunteerFromIndividualToGarrison` -> bez flagi. Strona = `__0.LeaderHero`. Postfiks `LevyGold` na tej metodzie oddaje zloto przez `ChangeHeroGold` (bez zdarzenia) - nie koliduje; odjecie `_levyBack` zostaje jak dzis, a `NoteLevyBack` dopisuje `NamedRun` (2.4). Karawana placi najemnikowi z kasy partii BEZ zdarzenia (`RecruitmentCampaignBehavior.cs:614`); przy `RecruitGoldToSeller` (domyslnie wlaczony) `LevyGold` oddaje cala kwote miastu i zwrot karawanie (przelew, swiat 0) - przy wylaczonym rozdz. 4. |
| O07 statki (wiersz 3) | `TaleWorlds.CampaignSystem.Actions.ChangeShipOwnerAction.ApplyInternal(PartyBase newOwner, Ship ship, ChangeShipOwnerAction.ShipOwnerChangeDetail changeDetail)` private static void (482 B) | F | - | `In/Out[KShips]` | dziesiatki | Prefiks `(ChangeShipOwnerAction.ShipOwnerChangeDetail __2, out Ctx __state)`; flaga tylko przy `ApplyByTrade`; strona dowolna (w oknie biegnie tez premia zarzadcy NavalDLC `OnShipOwnerChanged :426` - stad nazwa pozycji). NIE latac `ApplyByTrade` (9 B). |
| O08 statki-inne | `NavalDLC.CampaignBehaviors.NavalShipDistributionCampaignBehavior.RecoverGoldFromRemainingShipsAfterDistribution(MobileParty party)` private; `NavalDLC.CampaignBehaviors.ShipTradeCampaignBehavior.OnShipRepaired(Ship ship, Settlement repairPort)` private | F | - | `In[KShipsOther]` | rzadko | Prefiks bez parametrow. Typy przez `AccessTools.TypeByName`; NavalDLC wylaczony -> BRAK. |
| O09 jency (wiersz 6) | `TaleWorlds.CampaignSystem.Actions.SellPrisonersAction.ApplyInternal(PartyBase sellerParty, PartyBase buyerParty, TroopRoster prisoners, bool applyConsequences)` private static (662 B) | F | - | `In[KPrisonersParty]`, `In[KPrisonersFort]` | 200-800 | Prefiks bez parametrow (RC FairRansom ma tu prefiks/postfiks/finalizer). Podzial w `OnGold`: `rc >= 0` -> twierdze, inaczej partie. |
| O10 oblezenie (wiersz 6) | `...CampaignBehaviors.SiegeAftermathCampaignBehavior.OnSiegeAftermathApplied(MobileParty attackerParty, Settlement settlement, SiegeAftermathAction.SiegeAftermath aftermathType, Clan previousSettlementOwner, Dictionary<MobileParty,float> partyContributions)` private (640 B) | F | - | `In[KSiege]` | 0-10 | Prefiks bez parametrow. |
| O11 turniej (wiersz 6) | `TaleWorlds.CampaignSystem.TournamentGames.TournamentManager.GivePrizeToWinner(TournamentGame tournament, Hero winner, bool isPlayerParticipated)` public | F | - | `In[KTournament]` | kilka | Prefiks bez parametrow. BK `BKTournamentManager.GivePrizeToWinner` martwy (pkt 4). Wykonawca: rozmiar IL `ilspycmd -il`; ponizej 100 B patrz kontrolka T6. |
| O12 rabunek-BK (wiersz 6) | `BannerKings.Behaviours.Raids.BKRaidCaptureBehavior.ExecuteCapture(MobileParty attackerParty, Hero leader, Clan capturingClan, Village village, int totalAttackerTroops, bool fromCheat)` private | F | - | `In[KRaidCapture]` | kilka w wojnie | Prefiks `(Hero __1, out Ctx __state)`, strona = `__1`. |
| O13 BEE (wiersz 3) | `BetterEconomy.Behaviors.CastleEconomyCampaignBehavior.TryAiContribute(Settlement castle, CastleEconomyState state)` :343; `...TryAiBuildTrainingCamp(Settlement, CastleEconomyState)` :1055; `BetterEconomy.Behaviors.VillageInvestmentCampaignBehavior.TryApplyLordInvestment(Settlement villageSettlement, Hero lord, Clan clan, int amount, out VillageInvestmentSummary summary)` :354; `BetterEconomy.Behaviors.VillageDevelopmentCampaignBehavior.ApplyMarketAccess(Settlement settlement, Hero payer, bool player)` :615 - wszystkie private | F | - | `Out[KBee*]` | rzadko | Prefiksy BEZ parametrow (zadnych typow BEE). BEE wylaczone -> BRAK. Skarbiec BEE (`LocalTreasuryGold`) zostaje poza swiatem: wplata = ujscie (paczka 170 go zamyka). |
| O14 bitwy | `TaleWorlds.CampaignSystem.MapEvents.MapEventParty.CommitGoldChanges()` internal void | F+M | `PartyTradeGold` partii bez wodza | `In/Out[KBattle]`; `N4 += PTG1 - PTG0` | po bitwie na partie | Prefiks `(MapEventParty __instance, out BattleState __state)`; migawka tylko gdy `__instance.Party.LeaderHero == null && __instance.Party.MobileParty != null && __instance.Party.MobileParty.IsActive`. Wykonawca sprawdza rozmiar IL; przy 0 wywolan mimo bitew -> pkt 13, R7. |
| O15 prowizja (wiersz 13 + 4.2) | `TaleWorlds.CampaignSystem.Actions.SellItemsAction.ApplyInternal(PartyBase sellerParty, PartyBase buyerParty, ItemRosterElement itemRosterElement, int number, Settlement currentSettlement)` private static (673 B) | M (+ zdarzenia) | kasa osady sprzedajacej, `Town.TradeTaxAccumulated` | `U2` (wsie / miasta i zamki), `Cls[c, Commission]`, `Cls[c, CommissionToCounter]` | kilka tys.; osada nie-sprzedajaca: 1 odczyt | Szczegoly 3.6. Osobna latka obok `GoodsLedger.SellPre` (inny wylacznik). |
| O16 lup-z-cial (wiersz 14) | `TaleWorlds.CampaignSystem.MapEvents.MapEvent.LootCasualtyCharacter(CharacterObject casualtyCharacter, MapEventParty winnerParty, MapEventParty defeatedParty, float aiTradePenalty, int maxLootedItemsPerBodyForMainParty, ItemRoster mainPartyLootFromCasualties)` private (256 B) | M | `winnerParty.Party.MobileParty.PartyTradeGold` (u lorda = kiesa wodza) | `Z1` | ok. 1-10 tys. w wojnie, 2 odczyty int | Prefiks `(MapEventParty __1, out int __state)`; `__state = int.MinValue`, gdy wodz null albo gracz. Okno na zabitego zamiast na `LootDefeatedPartyCasualties`, bo jest dokladne i niezalezne od `BattlefieldLaw.CutCasualtyLootPrefix` (ten pomija metode zewnetrzna - wtedy 0 wywolan i 0 lupu, poprawnie). |
| O17 nowe-karawany (D18) | (1) `TaleWorlds.CampaignSystem.Party.PartyComponents.CaravanPartyComponent.CreateCaravanParty(Hero caravanOwner, Settlement spawnSettlement, PartyTemplateObject templateObject, bool isInitialSpawn, Hero caravanLeader, ItemRoster caravanItems, bool isElite)` public static - prefiks First BEZ parametrow `(out CapState __state)` + finalizer `(Hero __0, MobileParty __result, CapState __state)`; (2) `GetInitialTradeGold(Hero owner, bool navalCaravan, bool largeCaravan)` CZYNNEGO `Campaign.Current.Models.CaravanModel` (dzis `DefaultCaravanModel.cs:53-61`: 10 000, duza 17 500, +5 000 dla gracza; BK i ROT go nie podmieniaja) - postfiks `(int __result)`, zakladany w kampanii w `EnsureModelHooks` jak O40 (deklaracja metody wedlug `SoldierPay.DeclOf`) | F (okno) + wynik modelu | - (kwota nadana) | `Z2` (notable / lordowie wedlug `__0.IsNotable`), `ICaravanNewN`; informacja `ICaravanCapAfter` = suma `__result.PartyTradeGold` w finalizerze (kasa po wejsciu do osady) | kilka na dobe; postfiks modelu wolany tez z modelu finansow (`DefaultClanFinanceModel.cs:866, 879`) - tam 1 porownanie flagi | Prefiks: `__state = { Prev = _capOpen, PrevSeen = _capSeen, PrevGiven = _capGiven, On = true }; _capOpen = true; _capSeen = false`. Postfiks modelu: `if (_capOpen && !_capSeen) { _capSeen = true; _capGiven = Math.Max(0, __result); }` - PIERWSZY wynik w oknie to dokladnie kwota dla `InitializePartyTrade` (`CaravanPartyComponent.cs:247`, setter `PartyTradeGold` przycina do 0, `MobileParty.cs:443`), bo wejscie do osady (`:248-252`) jest pozniej. Finalizer: gdy `_capSeen` -> `AddWorld(Z2...)`, inaczej `ICaravanNoCap++` (hak modelu niewpiety - kwota zostaje w reszcie); przywrocenie `Prev*`. Wejscie do osady w oknie (werbunek karawany `RecruitmentCampaignBehavior.cs:601, 614`, BK `AddRealisticIncome` `BKPartyBehavior.cs:946-960`, BK `AddCaravanFees` `:784-794`, zakup statkow `CaravansCampaignBehavior.cs:700-703, 792`) mierza ich wlasne pozycje (O06 + LevyGold, zdarzenia, przelew do licznika cel, `to`). Hak modelu niewpiety -> w kontrolce `kapital-karawan BRAK`. |
| O18 BK rynek osady (wiersz 15) | `BannerKings.Behaviours.BKSettlementBehavior.HandleMarketGold(Settlement settlement)` private | M | kasa osady + kiesy jej notabli | `N1`; `Cls[c, Market]` | ~1 600 (kazda osada raz na dobe); petla `for` po 2-6 notablach | Cialo w lambdzie `ExceptionUtils.TryCatch` (wyjatek lapie BK - finalizer i tak biegnie). Rozbicie w linii: kasy `dKasa`, notable `dNotable`, swiat `N1 = suma`. |
| O19 BK porty (wiersz 16) | `BannerKings.Behaviours.BKBuildingsBehavior.RunPorts(Town town)` private | M | kasa miasta | `U3 += -dKasa` (ze znakiem; dodatnie = zniknelo); `Cls[CTown, Ports]` | ~100 | Ryba z niczego na polke - towar liczy ksiega 146, nie ta. |
| O20 BK kopalnie (wiersz 16) | `BannerKings.Behaviours.BKBuildingsBehavior.RunMines(Town town)` private | M | kasa miasta | `U4 += -dKasa` (ze znakiem); informacja: placa gornikow wrocila (`BuildFunding.MineRevenuePostfix`) | ~100 | Nasz `BuildFunding.MinesPrefix` (Normal) tylko zeruje slownik. Dopisac w `BuildFunding.MineRevenuePostfix` po `town.ChangeGold(revenue)`: `CirculationWindows.NoteMineWages(revenue);` (licznik informacji "wrocilo jako place gornikow"). Dochod pana z kopalni (`miningRevenues`, BKTaxModel) przychodzi w rozliczeniu rodu - jest w `_clanUp`. |
| O21 BK konwoje (wiersz 16) | `BannerKings.Behaviours.BKPartyBehavior.AddPopulationPartyBehavior(MobileParty party, Settlement target, PopulationData data)` private | M | kasa `__1.Town` | `U5`; `Cls[c, Convoys]` | tysiace wywolan (kazde wejscie partii do osady), zloto rzadko | Prefiks `(MobileParty __0, Settlement __1, out int __state)`; bramka O(1): `__0 != null && __0.PartyComponent != null && __0.PartyComponent.GetType() == _popType` (`_popType = AccessTools.TypeByName("BannerKings.Components.PopulationPartyComponent")` raz w ApplyAll), `__1.Town != null`; inaczej `__state = int.MinValue`. Parametru `PopulationData` nie deklarowac. |
| O22 BK niewolnicy (E1) | `BannerKings.Behaviours.BKPartyBehavior.SellSurplusSlaves(Settlement settlement)` private | M (bohater) | kiesa `__0.OwnerClan.Leader` | `Z3` | raz na dobe na twierdze z dekretem | Migawka bohatera z odjeciem zdarzen (3.7). |
| O23 BK rynek wsi (E2) | `BannerKings.Behaviours.BKBuildingsBehavior.OnDailyTickSettlement(Settlement settlement)` private (wola `HandleVillage(PopulationData)` :221) | M | kiesa wsi + kasa(y) miasta, ktorym wies placi | `N2`; `Cls[CVill, VillageMarket]`, `Cls[CTown/CCastle, VillageMarket]` | ~1 400 (bramka `__0.IsVillage`) | Wykonawca czyta `BKBuildingsBehavior.cs:221-279` i migawka obejmuje DOKLADNIE osady, na ktorych `HandleVillage` wola `ChangeGold` (rozpoznanie 2: wies i `village.Bound.Town`; gdy kod uzywa `TradeBound` - ta). Okno na wolajacej, bo `HandleVillage` bierze typ BK. |
| O24-O27 BK tytuly (wiersz 17) | `BannerKings.Managers.TitleManager.AddOngoingClaim(TitleAction action)` :550, `CreateTitle(TitleAction action)` :574, `RevokeTitle(TitleAction action)` :599, `UsurpTitle(Hero oldOwner, TitleAction action)` :684 - public | M (bohater) | kiesa `action.ActionTaker` | `U6` (roszczenia / nadania / odebrania / uzurpacje) | rzadko | Prefiks `(object __0, ...)` (dla Usurp `object __1`); `ActionTaker` przez `AccessTools.Property(AccessTools.TypeByName("BannerKings.Managers.Titles.TitleAction"), "ActionTaker")` pobrane raz, odczyt `GetValue(action, null) as Hero` (rzadkie wywolania - odbicie dopuszczalne). |
| O28 BK pasowanie | `BannerKings.Behaviours.BKKnighthoodBehavior.CreateClan(Hero hero, Clan originalClan, FeudalTitle title, TextObject name)` private :213 | M (bohater) | kiesa `__1.Leader` | `U6` (pasowanie z nowym rodem) | bardzo rzadko | Prefiks deklaruje `(Hero __0, Clan __1, ...)`, nie `FeudalTitle`. |
| O29 BK dwor | `BannerKings.Managers.Court.Grace.CourtGrace.AddExpense(Clan clan, CourtExpense expense)` public :375 | M (bohater) | kiesa `__0.Leader` | `U6` (koszty laski dworu) | AI 1. dzien pory roku | - |
| O30 BK dylematy | `BannerKings.Behaviours.Diplomacy.Dilemmas.BKDilemmaBehavior.ApplyAiLevers(Dilemma dilemma, DilemmaType type)` private :378 | M (glowy rodow) | suma kies glow wszystkich rodow (`Clan.All`, bez band) | `U6` (dylematy) | na aktywny dylemat na dobe (kilka); petla ~400 odczytow na wywolanie - dopuszczalne, bo rzadkie | Prefiks bez parametrow; `_snapLeaders = true`; odejmowanie zdarzen z udzialem glow (3.1 pkt 5). |
| O31 BK przeniesienie dworu | `BannerKings.Managers.Goals.Decisions.MoveCourtDecision.ApplyGoal()` public override :101 | M (bohater) | kiesa `Clan.PlayerClan.Leader` | `U6` | rzadko, tylko gracz | - |
| O32 rycerz BKROT (E3) | `BannerKings.Behaviours.BKClanBehavior.EvaluateRecruitKnight(Clan clan, bool ignoreCosts)` private :1675 | M (bohater) | kiesa `__0.Leader` | `U6` (wyposazenie rycerza) | RunWeekly na rod | BKROTPatch `EvaluateRecruitKnightPatch.Prefix` ZAWSZE false i sam rusza zloto - nasz prefiks First czyta przed nim, finalizer po. |
| O33 BK donatywa | `BannerKings.Behaviours.Diplomacy.BKImperialLoyaltyBehavior.ProcessImperialRealm(Kingdom kingdom, KingdomDiplomacy diplomacy)` private :81 | M (bohater) | kiesa `__0.RulingClan.Leader` | `U6` (donatywa imperium) | tygodniowo | Spoza rozpoznania (B1 7.4); prefiks `(Kingdom __0, out SnapState __state)`. |
| O34 BK material budowy | `BannerKings.Behaviours.BKBuildingsBehavior.OnBuildingChanged(Town town, Building building, int levelChange)` private :123 | M (bohater) | kiesa `__0.OwnerClan.Leader` | `U6` (material na koniec budowy) | rzadko | Przy `BuildFunding.On` nasz `SkipIfOn` zwraca false i BK nic nie bierze: prefiks First ustawia wtedy `__state` = wartownik, finalizer nic nie liczy. Okno liczy tylko przy wylaczonym `PaidConstruction`. |
| O35 warsztaty - wyrob (4.2) | istniejace `WorkshopTrade.OutPrefix`/`OutPostfix` na `WorkshopsCampaignBehavior.ProduceAnOutputToTown(EquipmentElement, Workshop, bool)` ([HarmonyPriority(First)]) | M | kasa miasta warsztatu, `Workshop.Capital` | `Cls[c, WorkshopOut]` (dKasa), `N3 += dKasa + dKapital` | setki-tysiace, 2 odczyty int | Dopisac wywolania (5.8). NIE zalezy od `_cyc` (obejmuje warsztaty ukryte = rzemieslnicy BK). |
| O36 warsztaty - wsad (4.2) | istniejace `WorkshopTrade.InPrefix`/`InPostfix` na `WorkshopsCampaignBehavior.ConsumeInputFromTownMarket(ItemCategory, int, Town, Workshop, bool)` | M | kasa `__2` (Town), `__3.Capital` | `Cls[c, WorkshopIn]`, `N3` | jak wyzej | j.w. |
| O37 stan bohatera | `TaleWorlds.CampaignSystem.CampaignObjectManager.HeroStateChanged(Hero hero, Hero.CharacterStates oldState)` internal void | M (postfiks) | kiesa bohatera | `U7` (wyszedl ze swiata), `Z4` (wrocil) | setki (kazda zmiana stanu), O(1) | Postfiks `(Hero __0, Hero.CharacterStates __1)`: `wasIn = !(__1 == Dead || __1 == Disabled)`, `isIn = !OutOfWorld(__0)`; `wasIn && !isIn` -> `U7 += __0.Gold`, odwrotnie -> `Z4 += __0.Gold`. Gra wola to w `Hero.ChangeState` zaraz po zmianie stanu (`Hero.cs:1766-1768`). UWAGA: u lordow zloto jest przekazane PRZED smiercia - glowa rodu oddaje kiese nastepcy (`ChangeClanLeaderAction.cs:24`, wolane `KillCharacterAction.cs:65`), inny lord glowie rodu (`KillCharacterAction.cs:98`), a `MakeDead -> ChangeState` biegnie pozniej (`:124`, `:227`); oba przekazania to przelewy miedzy zywymi. `U7` liczy to, co ZOSTALO w chwili zmiany stanu: u lordow zwykle ~0, u notabli (bez rodu, bez przekazania) cala kiesa. Kontrolka rozdziela: zgony notabli (zloto przepada), zgony lordow (zloto przekazane przed smiercia, przy zgonie zostalo X), inni (wedrowcy, Disabled); liczba zgonow do porownania z `HeroKilled`. |
| O38 partie znikaja | nasluch `CampaignEvents.MobilePartyDestroyed` (MobileParty, PartyBase) | L | `mp.PartyTradeGold` | `U8` (karawany / tabory / bandy / garnizony / inne) | setki | `if (mp == null || !mp.IsActive || (mp.IsLordParty && mp.LeaderHero != null)) return;` kwota `mp.PartyTradeGold > 0`. Zdarzenie idzie PRZED `RemoveParty` (`DestroyPartyAction.cs:23-25`). Bandy liczy tez `OutlawLaw` (`_goneFought/_goneDisbanded`) - tylko w swojej linii, bez wplywu na bilans. |
| O39 krolestwa upadaja | nasluch `CampaignEvents.KingdomDestroyedEvent` (Kingdom) | L | `k.KingdomBudgetWallet` | `N6 += -portfel` | rzadko | Upadle krolestwo wypada z `ReadHolders` (`:618`) razem z portfelem. |
| O40 linie modelu (wiersze 11, 12, 18, 19, 20, 21) | implementacja `CalculateClanGoldChange(Clan, bool, bool, bool)` czynnego `ClanFinanceModel` (dzis `ROT.Models.ROTClanFinanceModel` -> BK) | ML (postfiks Last) | - | tablica `Model[]` (informacja rodow) | ~309 rozliczen; 1 lista `GetLines()` na rozliczenie | Szczegoly 3.8. Zakladane w kampanii (`EnsureModelHooks`). |
| O41 okno rodu (istniejace) | `MoneyLedger.ClanTickPrefix/Postfix` na `ClanVariablesCampaignBehavior.DailyTickClan` + NOWY finalizer `MoneyLedger.ClanTickFinalizer(Clan __0, Exception __exception)` (D16) | M (dopisek) | `Clan.DebtToKingdom`, `Kingdom.KingdomBudgetWallet` rodu | dlug nowy / splacony, skarbiec netto w rozliczeniach; `IClanAborted` (rozliczenia przerwane wyjatkiem) | O(1) dopisek do istniejacego okna | NIE dokladamy pelnych przegladow swiata (rozdz. 10). Finalizer: `if (__exception != null && _clanNow != null && ReferenceEquals(_clanNow, __0)) { _clanNow = null; _clanStale++; _clanAborted++; }` - void, wyjatek leci dalej bez zmian (Harmony). Bez wyjatku nic nie robi (postfiks juz zamknal okno). |
| O42 blok Armoury | `MoneyLedger.BlockOpen` | M (swiat) | wszyscy | `RB` | 1 `WorldTotal()` na dobe | D10. |
| O43 clo z licznika cel (nasz kod) | `KingdomTreasury.Levies` (`KingdomTreasury.cs:257-264`) | licznik | `Town.TradeTaxAccumulated` (posiadacz `HTownTax`, `MoneyLedger.cs:619-622`) | `U9` = cala kwota `x` zdjeta z licznika | w bloku | Licznik cel spada o `x` (`:263`) - tyle znika ze swiata; kasa miasta placi `paid = min(x, spare)` do skarbca (`:264`) - to przelew (swiat 0), linia Obieg pokazuje go osobno. Dziura Armoury do zamkniecia w 164/165. Gdyby licznik mial byc "miara, nie zloto", trzeba by wyjac `HTownTax` z posiadaczy - to zmiana definicji swiata, nie w 169. |
| O44 D: wplywy rodow | `MoneyLedger.OnGoldTraded`, `KingdomTreasury`, `MenPurse`, `MoneyLedger.WagePostfix` | licznik | - | rozdz. 7 | O(1) | Bez nowych latek. |
| O45 kupno-BK (lordowie kupuja karawany i warsztaty) | `BannerKings.Behaviours.BKLordPropertyBehavior.OnSettlementEntered(MobileParty party, Settlement target, Hero hero)` private | M (bohater) + kapital warsztatow miasta | kiesa `__0.LeaderHero`; `Workshop.Capital` warsztatow `__1.Town` (petla `for`, 0-8) | `U6` (kupno karawan BK = `-d`), `N7 += suma(cap1 - cap0)`, `ICaravanBuyN`, `IWorkshopBuyN` (warsztaty ze zmienionym wlascicielem) | tysiace wywolan (kazde wejscie partii); bramka O(1): `__0 != null && __0.IsLordParty && __0.LeaderHero != null`, inaczej `On = false`; przy bramce: 1 odczyt kiesy + do 8 odczytow kapitalu (tylko `__1.IsTown`) | Prefiks `(MobileParty __0, Settlement __1, out SnapState __state)` (rozszerzona `SnapState`: bufory kapitalu i wlasciciela w polach statycznych `_wsCap0[8]`, `_wsOwn0[8]` - `IWorkshopBuyN` = warsztaty z innym wlascicielem po metodzie; wspolbiezne wywolania niemozliwe - watek glowny, zagniezdzenie tego okna wykluczone bramka). Kod BK: `RunWeekly` biegnie od razu (14%, `BannerKingsBehavior.cs:12-14`, w `ExceptionUtils.TryCatch` - finalizer i tak biegnie); `lord.ChangeHeroGold(-cena)` bez zdarzenia (`BKLordPropertyBehavior.cs:78`), potem `CreateCaravanParty` (`:79` - zagniezdzone O17 liczy `Z2`; dochod BK karawany do wlasciciela to zdarzenie - odejmie sie samo); `BuyWorkshop` (`:117`) placi `GiveGoldAction` (odejmie sie samo), a `ChangeOwnerOfWorkshop(..., InitialCapital)` ustawia kapital od nowa (`Workshop.cs:140`) - `N7`. Kupno warsztatu GRACZA idzie przez zapytanie (`:92-95`) i biegnie pozniej, poza oknem (rozdz. 4). BK wylaczony -> BRAK. |
| O46 myto (Tollgates, `Z5`) | `TaleWorlds.CampaignSystem.CampaignBehaviors.CaravansCampaignBehavior.OnSettlementEntered(MobileParty mobileParty, Settlement settlement, Hero hero)` public (`:691`) i `BannerKings.Behaviours.BKCaravansBehavior.OnSettlementEntered(MobileParty, Settlement, Hero)` public (`:1163`) - dwie latki, ten sam prefiks/finalizer | M (licznik cel) | `__1.Town.TradeTaxAccumulated` | `Z5 += (T1 - T0) - (ToCounterRun1 - ToCounterRun0)`, `ITollN` | tysiace wywolan (kazde wejscie partii); bramka O(1): `__0 != null && __0.IsCaravan && __1 != null && __1.IsTown && __1.Town.Governor != null && __1.Town.Governor.GetPerkValue(DefaultPerks.Trade.Tollgates)`, inaczej `On = false` | Prefiks `(MobileParty __0, Settlement __1, out TollState __state)`. W oknie karawana sprzedaje (`SellGoods` -> O15 dopisuje prowizje do licznika cel) - dlatego odejmujemy biegnaca sume `ToCounterRun` (O15 dopisuje do niej `toCounter` przy kazdym pomiarze). Perk dziala na kazdym ze sluchaczy osobno (vanilla tylko karawany ladowe, BK kazda) - dwa okna licza to, co sie naprawde stalo. BK wylaczony -> tylko latka vanilla. |

Juz mierzone - bez nowego okna: "zakupy" mieszczan A1 (`MoneyLedger.ShelfPostfix/ConsumePostfix`), utarg taborow A4 (`OnBefore/AfterEntered`),
regulator (`RegulatorPostfix`), zold karawan lordow - wiersz 10 (`_wage[WCaravan]`, `SoldierPay._dOther`; nowa linia go wypisuje),
podatek BK A5 - przelew w oknie rodu (O40 podaje kwote).

### 3.4 Pozycje `Sum[]` (Items) i linii kas `Cls[,]` (ClsItems)

`Sum`: ZLoot, ZCaravanCapitalNotable, ZCaravanCapitalLord, ICaravanNewN (liczba), ICaravanCapAfter, ICaravanNoCap, ZSlaves, ZHeroesBack,
ZTolls, ITollN, K1NotableAssets,
UCaravanFromCaravan, UCaravanFromNotable, ICaravanDue, ICaravanTopup, ICaravanShort, ICaravanN, ICaravanNotables, ICaravanShortN,
UCommissionVillage, UCommissionTownLost, ICommissionToCounter, UPorts, IPortsN, UMinesNet, IMinesBack, UConvoys, IConvoysN,
ULordClaim, ULordCreate, ULordRevoke, ULordUsurp, ULordKnightClan, ULordCourt, ULordDilemma, ULordMoveCourt, ULordRecruitKnight,
ULordImperial, ULordBuildMaterial, ULordCaravanBuy, ICaravanBuyN, UHeroesLeft, IHeroesLeftNotableN, IHeroesLeftNotableGold, IHeroesLeftLordN,
IHeroesLeftLordGold, IHeroesLeftOtherN, IHeroesLeftOtherGold, UPartyCaravan, UPartyVillager, UPartyBandit,
UPartyGarrison, UPartyOther, IPartyGoneN, NMarketKasa, NMarketNotables, NVillageMarketVillage, NVillageMarketTown, NWorkshopOutKasa,
NWorkshopOutCap, NWorkshopInKasa, NWorkshopInCap, NWorkshopBuyCap, IWorkshopBuyN, NBattleNoEvent, NDeadTransfers, NKingdomGone, INotableIncomeN.
Osobno (nie w `Sum`): `ToCounterRun` (`long`, biegnaca suma `toCounter` z O15, nie zerowana - dla O46 liczy sie roznica w oknie).
(`U*` = ujscie dodatnie, `Z*` = zrodlo dodatnie, `N*` = zmiana swiata ze znakiem, `I*` = informacja, nie wchodzi do bilansu.)

`Cls[c, *]` (c = CTown/CCastle/CVill z `MoneyLedger`): Commission (zmiana kasy, ujemna), CommissionToCounter (dodatnia, informacja),
WorkshopOut, WorkshopIn, Ports, Mines, Convoys, Market, VillageMarket. Wszystkie TYLKO gdy `!MoneyLedger.InBlock`
(w bloku zmiane kas lapia migawki `Mark` - podwojnie nie liczyc).

### 3.5 O02 - zold karawan notabli (najwazniejsze okno)

```csharp
internal struct CaravanState { public bool On; public long G0, C0, Due, CarPaid; public int N, ShortN; public long T0; public bool P; public long W0, N0; }   // T0 = probka czasu (1/256); P/W0/N0 = probka swiata (2.4)
[prefiks First] public static void CaravanWagePre(Hero __0, out CaravanState __state)
//  __state.On = On && Live && watek && __0 != null && __0.OwnedCaravans.Count > 0
//  G0 = __0.Gold; dla i = 0..Count-1: mp = OwnedCaravans[i].MobileParty; p = mp.PartyTradeGold; w = mp.TotalWage;
//       C0 += p; Due += w; if (p >= w) CarPaid += w; else ShortN++; N++
public static void CaravanWageFin(Hero __0, CaravanState __state)
//  C1 = suma PartyTradeGold po __0.OwnedCaravans (ta sama petla for); gdy OwnedCaravans.Count != N (lista zmienila sie w trakcie -
//       gra tego nie robi) -> Stumbles++ i caly wynik tego wywolania pominiety (zostaje w reszcie)
//  vanish = (G0 + C0) - (__0.Gold + C1)          -> UCaravanFromCaravan += CarPaid; UCaravanFromNotable += vanish - CarPaid
//  ICaravanTopup += (C1 - C0) + CarPaid;  ICaravanDue += Due;  ICaravanShort += Due - vanish (>= 0); ICaravanN += N; ICaravanShortN += ShortN; ICaravanNotables++
```
Gra (`NotablesCampaignBehavior.cs:309-330`): karawana z kasa >= zold placi sama, inaczej placi notabl (`min(zold, kiesa)`), potem notabl
doplaca kase do 5 000 (przelew). Kolejnosc petli gry (od konca) nie zmienia sum. `vanish` jest zmierzony (roznica stanu) - rozbicie
na "z kas karawan" / "z kies notabli" wynika z regul gry. Dlaczego `TotalWage`: bez zoldu kazdej karawany nie da sie oddzielic
doplaty od zoldu, a wywolanie to ten sam odczyt, ktory gra robi linijke dalej (bez skutkow ubocznych; `MountedWage` liczy swoje
statystyki raz na dobe, nie na wywolanie).

### 3.6 O15 - prowizja od sprzedazy partiom

```csharp
internal struct SellState { public Settlement St; public int S0, Tax0; public bool On; public long T0; public bool P; public long W0, N0; }
[prefiks First] public static void SellPre(PartyBase __0, out SellState __state)
//  PIERWSZA instrukcja: __state = default (On = false); bramka On/Live/watek;
//  gdy _sellSt != null (okno juz otwarte - zagniezdzenie): Nested++, koniec (wewnetrzne okno sie NIE otwiera, nic nie zmienia)
//  gdy __0 != null && __0.IsSettlement && __0.Settlement.SettlementComponent != null:
//  St = __0.Settlement; S0 = St.SettlementComponent.Gold; Tax0 = St.Town != null ? St.Town.TradeTaxAccumulated : 0; _sellSt = St; _sellIn = 0; On = true
public static void SellFin(SellState __state)
//  gdy !On: NIC (prefiks nie ruszyl _sellSt/_sellIn - okno zewnetrzne, jesli jest, zostaje nietkniete)
//  gdy On: num2 = S0 + _sellIn - S1; toCounter = T1 - Tax0; lost = num2 - toCounter; ToCounterRun += toCounter
//    wies: UCommissionVillage += lost; miasto/zamek: UCommissionTownLost += lost; ICommissionToCounter += toCounter
//    gdy !InBlock: Cls[c, Commission] -= num2; Cls[c, CommissionToCounter] += toCounter
//    potem _sellSt = null; _sellIn = 0; probka (2.4)
```
Zagniezdzenie (sprzedaz w trakcie sprzedazy - w kodzie gry nie wystepuje, moglby to zrobic tylko cudzy sluchacz): wewnetrzna sprzedaz
tej samej osady wchodzi w pomiar okna zewnetrznego (ta sama kasa i ten sam licznik), innej osady - zostaje w reszcie; kontrolka
`zagniezdzone` pokazuje, czy to sie w ogole dzieje. Dzieki temu nie ma czego przywracac i finalizer nie moze skasowac stanu okna zewnetrznego.
Gra (`SellItemsAction.cs:64-91`): kupujacy placi osadzie zdarzeniem (`_sellIn` - zwykle przelew, liczony w "przelewy gry" linii kas),
potem osada traci `num2` bez zdarzenia (wies: stawka 1.0 - 100% zaplaty), miasto dopisuje prowizje do licznika cel (przelew do
posiadacza "liczniki cel"). W nicosc idzie `num2 - prowizja`. Zdarzenia `nic -> osada` (kupujacy bez wodza, `:80`) sa w `_sellIn` i w `from`
- tozsamosc trzyma sie poza blokiem; w bloku ten przypadek zostaje w `RB` (ryzyko R3).

### 3.7 Migawka bohatera z odjeciem zdarzen (O22, O24-O34, O45)

```csharp
internal struct SnapState { public bool On; public Hero H; public long G0; public int Item; public long T0; public bool P; public long W0, N0; }
// prefiks: PIERWSZA instrukcja __state = default (On = false); bramka On/Live/watek i bramka okna (np. O45);
//          gdy _snapHero != null || _snapLeaders -> Nested++, koniec (On = false - okno wewnetrzne sie NIE otwiera i NIC nie zmienia);
//          inaczej H = bohater; G0 = H.Gold (O30: suma kies glow); _snapHero = H (O30: _snapLeaders = true); _snapEvt = 0; On = true
// finalizer: gdy !On -> NIC (bez przywracania - stan okna zewnetrznego zostaje nietkniety)
//            gdy On: d = (H.Gold - G0) - _snapEvt   (zmiana BEZ zdarzen; zdarzenia z jego udzialem - takze po stronie jego partii, Eff w 3.1 - sa juz w from/to albo to przelewy)
//            pozycja-ujscie (U6, wszystkie O24-O34 i O45): AddWorld(Item, -d, -1)   (wartosc ze znakiem; dodatnia = zloto zniknelo)
//            pozycja-zrodlo (Z3, O22):                      AddWorld(Item, d, +1)   (wartosc ze znakiem; dodatnia = zloto powstalo)
//            potem _snapHero = null; _snapLeaders = false; _snapEvt = 0; probka (2.4)
```
`Sum` dla `U6` i `Z3` trzyma wartosc ze znakiem, a linia drukuje ja przez `S()`; gdy znak jest "nietypowy" (np. tytul daje bohaterowi zloto
bez zdarzenia), bilans dalej sie zgadza, bo `U6` wchodzi do `U` ze swoim znakiem. Zagniezdzenie: drugie okno bohatera otwarte, gdy pierwsze
trwa -> `Nested++`, wewnetrzne NIE otwiera nowej migawki i jego finalizer niczego nie przywraca (zostaje `_snapHero` zewnetrznego; zmiana kiesy
wewnetrznego bohatera - jesli to inny bohater - zostaje w reszcie, a jesli ten sam - liczy ja okno zewnetrzne). Jedyne realne zagniezdzenie
to O17 w O45 (karawana kupiona przez lorda) - O17 nie jest oknem bohatera, wiec biegnie normalnie, a wejscie tej karawany do osady
wola znow `BKLordPropertyBehavior.OnSettlementEntered`, gdzie bramka O45 odpada (karawana nie jest partia lorda). Pozostale zagniezdzenia
tych metod BK sa w praktyce niespotykane (kontrolka `zagniezdzone`).
Dla O30 (dylematy) `G0` = suma kies glow `Clan.All` (bez band, z zywa glowa), `_snapLeaders = true`.

### 3.8 O40 - linie modelu finansow

`EnsureModelHooks()` (z `ArmouryBehavior.OnSessionLaunched`, po `MountedWage.EnsureContextHooks`, we wlasnym try):
- czynny model `Campaign.Current.Models.ClanFinanceModel`; deklaracja metody jak `SoldierPay.DeclOf` (`SoldierPay.cs:252-260`, argumenty
  `{ typeof(Clan), typeof(bool), typeof(bool), typeof(bool) }`); raz na klase (`HashSet<Type>`), raz na kampanie sprawdzenie (`_modelTriedFor`);
- `h.Patch(m, postfix: new HarmonyMethod(typeof(CirculationWindows), nameof(ModelLinesPostfix)) { priority = Priority.Last })`;
- nazwy linii liczone RAZ na sesje (ten sam jezyk co gra): `GameTexts.FindText("str_finance_tribute_expenses").ToString()`,
  `"str_finance_tribute_incomes"`, `"str_finance_mercenary"`, `"str_finance_mercenary_expenses"`, `"str_finance_call_to_war_expenses"`,
  `"str_finance_call_to_war_incomes"`, `"str_finance_debt"`, `"str_finance_kingdom_support"`; BK: `new TextObject("{=7uzvI8e8}Kingdom Budget Expense").ToString()`,
  `new TextObject("{=L0Dwod0e}Council wages").ToString()`, `new TextObject("{=WvhXhUFS}Councillor role").ToString()`; kazda w osobnym try (brak = null = "-").
  NIE czytac pol statycznych `DefaultClanFinanceModel` (konstruktor statyczny wywracal wczytanie - CrashScribe Mends.cs:2528-2553).
- linia startowa: `Obieg (169): linie modelu finansow z <klasa> (wpiete teraz|wczesniej); nazwy linii: N z 11 znalezione.`

```csharp
public static void ModelLinesPostfix(object __instance, MethodBase __originalMethod, Clan __0, bool __2, ExplainedNumber __result)
// warunki jak SoldierPay.NetPostfix (:232-249): __2 == true; __instance == czynny model; __originalMethod.DeclaringType == deklaracja;
// MoneyLedger.InClanTickFor(__0); On; watek glowny.
// foreach (var line in __result.GetLines()) porownac line.name z nazwami (string.Equals Ordinal) i dodac line.number do Model[]
// tier (wiersz 18, linia bez opisu - wzor gry DefaultClanFinanceModel.cs:133-136):
//   if (__0 != Clan.PlayerClan && __0.MapFaction != null && (!__0.MapFaction.IsKingdomFaction || __0.IsUnderMercenaryService) && __0.Fiefs.Count == 0)
//       Model[MTier] += __0.Tier * (80 + (__0.IsUnderMercenaryService ? 40 : 0)); Model[MTierN]++ ; najemnik osobno Model[MTierMerc]
// Model[MClansRead]++
// D19: ClanIncomeBook.NoteModelSaldo(__0, (int)__result.ResultNumber) - saldo naliczone tego rozliczenia (tylko do CSV i linii 6.9)
```
`EnsureModelHooks` zaklada tez postfiks O17 na `GetInitialTradeGold` czynnego `CaravanModel` (ta sama procedura: deklaracja metody,
raz na klase, linia startowa `Obieg (169): kapital nowych karawan z <klasa> (wpiety|BRAK)`).
`Model[]`: MTributeOut, MTributeIn, MMercOut (udzial w kosztach najemnikow krolestwa), MMercIn (kontrakt najemnika), MCallWarOut,
MCallWarIn, MDebt (splata naliczona), MSupport (zapomoga, oczekiwane 0), MBkTax (podatek BK - przelew rod -> skarbiec), MCouncilPay,
MCouncilGet, MTier, MTierMerc, MTierN, MClansRead. Kwoty to wartosci NALICZONE w modelu (przed obcieciem pustej kiesy) - linia to mowi.
Linie z ta sama nazwa gra laczy (`StatExplainer.AddLine`), wiec suma po nazwie jest pelna.

O41 (dopisek do `MoneyLedger.ClanTickPrefix/Postfix`): prefiks zapisuje `_clanDebt0 = __0.DebtToKingdom`, `_clanKingdom = __0.Kingdom`,
`_clanWallet0 = _clanKingdom != null ? _clanKingdom.KingdomBudgetWallet : 0`; postfiks (przy zgodnym rodzie): `dd = __0.DebtToKingdom - _clanDebt0`
-> `dd > 0`: `_debtNew += dd; _debtNewN++`; `dd < 0`: `_debtPaid -= dd; _debtPaidN++`; `_walletInClan += (kingdom.KingdomBudgetWallet - _clanWallet0)`.
Wszystko w istniejacych try; pola w `MoneyLedger.Obieg.cs`, zerowane w `ClearDay`.

---

## 4. Pozycje swiadomie pominiete

| Pozycja | Powod |
|---|---|
| BK `WorkshopData.DoProduction` (rzemieslnicy, 4.2) | Martwy kod: w IL BannerKings.dll zero wywolan, `Tick()` pusty (rozpoznanie 2, B1). Prawdziwi rzemieslnicy BK = `ProduceOutputPrefix` dla "artisans" - mierzy O35. |
| BK `BKTournamentManager.GivePrizeToWinner` | Metoda "new", nikt jej nie tworzy (B13); nagrody idzie droga gry - O11. |
| BK `HandleExcessFood` / `BuyOutput` (wiersz 16) | Warunek `SettlementFoodModel is BKFoodModel` falszywy - czynny `ROTSettlementFoodModel` (B6). Linia `Obieg: BK i BetterEconomy` wypisuje nazwe czynnego modelu zywnosci jako dowod. |
| BK `HandleExcessWorkforce` | Nikt nie wola (B6). |
| BK `BKVillageSupplyAutoBehavior.RefillFromTownMarket` (majatki BK, 4.2) | Wymaga Economy Overhaul (nie ma) - martwe (B15); w linii budzetu "majatki BK -". |
| BK `OnBuildingChanged :187-188` przy wlaczonym BuildFunding | `SkipIfOn` pomija metode - ujscie 0; okno O34 liczy tylko przy wylaczonym BuildFunding. |
| `GiveGoldAction.ApplyInternal` | D5. |
| Male metody: `AddPaymentForDebts` (79 B), `AddExpensesForTributes` (70), `ApplyShareForExpenses` (57), `UpgradeTroop` (93), `GetRecruitVolunteerFromMap` (14), `ChangeShipOwnerAction.ApplyByTrade` (9), `ItemConsumptionBehavior.UpdateTownGold` (30), `BuyOutput`, `AddRevenue`, `AddExpense` | Ryzyko wklejenia przez JIT; zastapione oknem na wolajacej albo linia modelu (O40). |
| `AddIncomeFromKingdomBudget` (wiersz 20) | Dzis nie biegnie (BK `KingdomBudgetPrefix` false); tylko kontrolka z linii modelu `MSupport` (oczekiwane 0). |
| BK `Demand` - kompromis finansowy (`Demand.cs:238`) | Zloto w lambdzie `DemandResponse` bez stabilnej metody do okna; rzadkie (decyzja gracza/AI wobec grup interesu). Gdy reszta zostanie > 10 tys., wrocic. |
| BK uczty (`Feast.cs:278-279`) | Przelew gospodarz -> kasa miasta (swiat 0); tylko reszta kas miast, maly. |
| Dar startowy kas (wiersz 22) | Jednorazowy, przed doba 1 ksiegi. |
| Kiesa nowego garnizonu (`GarrisonPartyComponent.cs:96`), stawka startowa band przy wylaczonym obiegu kryjowek (`OutlawLaw._startNothing`), `RebellionsCampaignBehavior.cs:310` (50 000 dla przywodcy rebelii), `RansomOfferCampaignBehavior.cs:176` (oferta okupu graczowi) | Rzadkie; zostaja w reszcie; wymienione w opisie reszty. |
| Portfele trybutu / najemnikow / wezwania do wojny i `DebtToKingdom` jako posiadacze | To zobowiazania (wartosci ujemne), nie zloto; zmienilyby definicje "zlota swiata" z roku pomiarow. Kwoty pokazuje O40/O41 w linii rodow. |
| CrashScribe `economy-*.csv` - nowe kolumny (10.1 pkt 8) | Osobny DLL bez odwolania do Armoury; zamiast tego nasze `budzet-rodow.csv` z kluczami do laczenia (dzien_gry + nazwa + StringId). |
| Linie "Wojsko", "ROWNOWAGA 28 dob" (10.1 pkt 6-7) | Poza zakresem wiersza 1 rozdz. 12 (paczka 166/autotest roczny). |
| Perk TravelingRumors (`VillagerCampaignBehavior.cs:338-341`, licznik cel +20 na tabor) | Martwy w tym zestawie: lezy w galezi `mobileParty.IsActive && mobileParty.IsVillager` (`:325`), a prefiks BK `VillagerSettlementEnterPatch` (`EconomyPatches.cs:1083-1118`) dla tego samego warunku zwraca false - cialo vanilla nie biegnie. |
| Perk DistributedGoods w prefiksie BK (`EconomyPatches.cs:1111-1114`) | Dopisuje `MathF.Round(SecondaryBonus)`, a premia drugorzedna tego perku to -0.15 (`DefaultPerks.cs:2243`) - po zaokragleniu 0 zl. Gdyby inny mod zmienil perk, kwota zostanie w reszcie (probka O46 tego nie obejmuje). |
| RC FairRansom: nadwyzka okupu zdejmowana z kiesy glowy rodu jenca (`RealisticCaptivity/src/FairRansom.cs:113`, `:179`, `ChangeHeroGold` bez zdarzenia) | Tylko sprzedaz jencow-lordow przez GRACZA (rzadko); kazda kwota jest juz w logu RC (`Okup lorda (posrednik|ekran druzyny): rod X placi N`); Armoury nie ma odwolania do RC. Zostaje w reszcie `RP` (zrodlo dla gracza jest w `from`, ujscie u rodu jenca w reszcie). |
| Karawana najmuje najemnika przy WYLACZONYM `RecruitGoldToSeller` (`RecruitmentCampaignBehavior.cs:614`, kasa karawany bez zdarzenia) | Domyslnie wlaczony: `LevyGold` oddaje cala zaplate miastu i zwrot karawanie (`LevyGold.cs:76-82`) - przelew, swiat 0. Przy wylaczonym kwota zostaje w reszcie; linia startowa 169 wypisuje stan tego klucza. |
| Kapital karawany z zadania eskorty (`EscortMerchantCaravanIssueBehavior.cs:694`, `InitializePartyTrade` poza `CreateCaravanParty`) | Zadanie gracza, rzadkie; zostaje w reszcie. |
| Kupno warsztatu GRACZA przez lorda BK po zapytaniu (`BKLordPropertyBehavior.cs:92-95`) | Delegat biegnie po decyzji gracza, poza oknem O45: zaplata to zdarzenie (w porzadku), kapital od nowa (`Workshop.cs:140`) zostaje w reszcie; rzadkie. |
| Konsumpcja i regulator kas w `NamedRun` (2.4) | Biegna w dziennym ticku miasta, poza wszystkimi oknami 169, a regulator to wynik modelu, nie chwila zmiany zlota - dopisanie myliloby probki. |

---

## 5. Zmiany w istniejacych plikach

### 5.1 `Armoury/src/MoneyLedger.cs`
1. `internal static class MoneyLedger` -> `internal static partial class MoneyLedger`.
2. Akcesory (obok `Live`, `:164`): `internal static bool InBlock { get { return _inBlock; } }`,
   `internal static bool InClanTick { get { return ClanOpen; } }`, `internal static bool InClanTickFor(Clan c) { return ClanOpen && ReferenceEquals(_clanNow, c); }`,
   `internal static bool WinOpenNow { get { return WinOpen; } }`; w `MoneyLedger.Obieg.cs`: `internal static long WorldNow() { return WorldTotal(); }` (probki 2.4).
3. `OnGoldTraded` (`:254-294`): po `if (gNone && rNone) return;` (`:264`) dopisac
   `ClanIncomeBook.OnEvent(gh, gp, rh, a, gNone, ClanOpen, _inBlock);`; po `var clan = ...` (`:272`) dopisac obliczenie `counted` (2.2),
   `CirculationWindows.OnGold(gh, gp, rh, rp, a, gNone, rNone, gc, rc, clan != null, _inBlock, counted);` oraz
   `CirculationWindows.NoteCounted(a, gNone, rNone, counted, _inBlock);` (w srodku: `if (counted && (gNone || rNone)) { NamedRun += gNone ? a : -a; if (inBlock) BlockNamed += gNone ? a : -a; }`).
   Wszystkie trzy nowe metody maja WLASNY try/catch (D17) - wyjatek w nich nie przerwie starego `try` ani liczenia `from/to`. Reszta metody bez zmian.
4. `ClanTickPrefix` (`:303-313`): po `_clanTime = CampaignTime.Now; _clanNow = __0;` dopisac odczyty O41 i `ClanIncomeBook.ClanTickOpen(__0);`.
   `ClanTickPostfix` (`:316-330`): po obliczeniu `d` (przy zgodnym rodzie) dopisac rachunek O41 i `ClanIncomeBook.ClanTickClose(c);`.
   Nowa `public static void ClanTickFinalizer(Clan __0, Exception __exception)` (D16, cialo w O41) - w `ApplyAll` (`:580-584`) ten sam `h.Patch(tick, ...)`
   dostaje `finalizer: new HarmonyMethod(typeof(MoneyLedger), nameof(ClanTickFinalizer))`; `_clanAborted` (nowe pole, zerowane w `ClearDay`) idzie do kontrolki 6.8.
   Gdy finalizer zamknie przerwane rozliczenie, `ClanIncomeBook` dostaje `ClanTickAbort()` (`_open = null`, rod bez nowego `WageLast*`).
5. `WagePostfix` (`:518-528`): po `_wage[k] += __result;` dopisac `ClanIncomeBook.NoteWage(ClanOpen ? _clanNow : null, k, __result);`
   (`k`: WLord=0, WGarrison=1, WCaravan=2, WOther=3 - stale przekazac jako `int`).
6. `BalanceLine`: wydzielic `OldBalance(...)` (2.1) - tekst linii bez zmian.
7. `ClassLine` (`:767-841`): przed `return` zapisac `_classRest[c] = delta - known;` (nowe pole `long[] _classRest = new long[Classes]`).
8. `BlockOpen` (`:435-439`): PIERWSZA instrukcja `ClearLast169();` (D20; metoda w `MoneyLedger.Obieg.cs` z wlasnym try - zeruje
   `KingdomTreasury.Last*`, `IronBank.Last*` (`ZeroLast()`), `SoldierPay.Last*`, `PopulationLaw.DayVillageRent/DayTownRent` i `RentVillageToday`);
   po `_blockSnap = Snap();` dopisac `if (CirculationWindows.On) _blockWorld0 = WorldTotal(); else _blockWorld0 = long.MinValue;`.
   `NoteLevyBack` (`:360`): dopisac `CirculationWindows.NoteLevy(amount)` (wlasny try; `NamedRun += amount`, 2.4).
9. Nowa metoda `internal static void Note169(int kind, Settlement st, int amount)` (wzor `Note`, `:343-353`) - tablice `_n169In/_n169Out [Classes, N169]`,
   bramka `if (_inBlock || amount == 0 || !CirculationWindows.On) return;`. Rodzaje: `N169Repair` (naprawy z sakiewek ludzi), `N169Surplus`
   (skup nadwyzek zbrojowni od ludzi), `N169Kit` (braki kompletow gracza z sakiewki), `N169PurseGone` (sakiewki rozbitych partii),
   `N169Other` (inne przelewy Armoury: komplety rekrutow, unikaty, zwrot karawanom, Spoils of War, bandy). Plus
   `internal static void NotePurseGone(int purse, bool toTown)` (liczniki `_purseGoneWin/_purseGoneTown` do linii Obieg).
10. `ClearDay` (`:143-162`): dopisac `CirculationWindows.ClearDay();` oraz zerowanie pol z `MoneyLedger.Obieg.cs` (`ClearDay169()`).
11. `Reset` (`:135-141`): dopisac `Reset169()` (pierscien reszty 28 dob, `_blockWorld0`).
12. `Daily` (`:861-894`), galaz `else` (`:874-889`): kolejnosc wydruku:
    `StateLine`, `BalanceLine`, `BalanceCausesLine` (nowa, gdy `CirculationWindows.On`), `ClanLine`, `ClanCausesLine` (nowa, gdy On), `VillagerLine`,
    3 x `ClassLine`, `ClassCausesLine` (nowa, gdy On), `ObiegLine`, `ObiegNotablesLine`, `ObiegBkLine`, `ObiegWindowsLine` (wszystkie gdy On).
    Kazda nowa linia we wlasnym `try { Log.Info(...) } catch (Exception e) { Log.Error("MoneyLedger.<linia>", e); }` - blad jednej nie gasi reszty.
    W galezi `_first` (pierwsza doba po wczytaniu) nowe linie NIE sa drukowane (jak przeplywy).

### 5.2 `Armoury/src/SoldierPay.cs`
W `Daily` (`:508-541`) na poczatku `finally` PRZED `ClearDay()`: `LastLordAcc = _dLordAcc; LastLordTaken = _dLordTaken; LastGarAcc = _dGarAcc;
LastGarTaken = _dGarTaken; LastToPurse = _dToPurse; LastToTowns = _dToTowns; LastToCastles = _dToCastles; LastOther = _dOther; LastWatching = Watching;`
(nowe pola `internal static long ...; internal static bool LastWatching;`, zerowane w `Reset` i przez `ClearLast169()` na poczatku bloku - D20). Bez zmian logiki.

### 5.3 `Armoury/src/KingdomTreasury.cs`
- Nowe pola `internal static long LastDues, LastRefundGiven, LastRefundDue, LastRefundPaid, LastSubsidy, LastCustoms, LastCustomsTaken, LastMint, LastMonopoly;`
  zerowane w `Reset` i przez `MoneyLedger.ClearLast169()` (pierwsza instrukcja `BlockOpen`, D20) - JEDNO miejsce, niezaleznie od tego, ktora metoda
  wyjdzie wczesnym `return` albo sie nie wykona (Daily, Levies i WageRefund stoja w jednym try `ArmouryBehavior.cs:1254`).
- `Daily` (`:47-77`): przed `Log.Info` `LastDues = total;`.
- `WageRefund` (`:134-214`): po `h.ChangeHeroGold(give[i]);` (`:183`) dopisac `ClanIncomeBook.NoteInflow(h, give[i], ClanIncomeBook.KRefund);`;
  przed `Log.Info` `LastRefundGiven = totalGiven; LastRefundDue = totalDue; LastRefundPaid = totalPaid;`.
- `Levies` (`:221-287`): w galezi cla (`:257-264`) PO linii `if (x > 0) st.Town.TradeTaxAccumulated -= x;` (`:263`, bez zmian) dopisac osobna linie
  `if (x > 0) taken += x;` (nowa zmienna `long taken = 0` obok `sub, cus, deb, mon`) - CALA kwota zdjeta z licznika cel (O43); po `ruler.ChangeHeroGold(x)` w mennicy (`:271`) i monopolach (`:279`)
  `ClanIncomeBook.NoteInflow(ruler, x, ClanIncomeBook.KCrownLevies);`; przed `Log.Info` `LastSubsidy = sub; LastCustoms = cus; LastCustomsTaken = taken; LastMint = deb; LastMonopoly = mon;`.
  `U9 = LastCustomsTaken` czyta linia (pozycja w bloku: `MoneyLedger.Obieg` przy druku robi `BlockNamed -= LastCustomsTaken` PRZED liczeniem `RB`).
  `cus` (= suma `paid`) to przelew kasa miasta -> skarbiec (swiat 0) - w linii Obieg osobno jako "skarbce dostaly z kas miast".

### 5.4 `Armoury/src/PopulationLaw.cs`
Nowe `internal static long DayVillageRent, DayTownRent;` i `internal static readonly Dictionary<Clan, int> RentVillageToday`. Zerowane tam,
gdzie `RentToday.Clear()` (`:76` w `Reset`, `:274`, `:283`). Po `RentToday[st.OwnerClan] = r0 + pay;` (`:316`): `if (st.IsVillage) { DayVillageRent += pay; int v0; RentVillageToday.TryGetValue(st.OwnerClan, out v0); RentVillageToday[st.OwnerClan] = v0 + pay; } else DayTownRent += pay;`.

### 5.5 `Armoury/src/IronBank.cs`
- `internal static int LastLent, LastPaidN, LastMissed, LastDefaults; internal static long LastLentSum, LastPaidSum;` + `internal static void ZeroLast()`.
  Zerowanie TYLKO w `Reset` i w `MoneyLedger.ClearLast169()` (D20); w `Daily` jedynie przypisanie wartosci doby tuz przed koncowym `Log.Info` (`:284`).
- `internal static bool TryGetDebt(Clan c, out double principal, out int missed, out bool defaulted, out int dueDay)` - odczyt `_debts[c.StringId]`
  (O(1)); `false` gdy brak albo `Principal < 1`.
- `internal static bool TryGetTrust(Clan c, out float trust)` - odczyt `Of(c, false)` (O(1); `Defaulted` -> 0) dla limitu na sucho (rozdz. 8, D21).
- `internal static void DefaultedKingdoms(HashSet<Kingdom> into)` - raz na dobe: krolestwa rodow z `Defaulted` (petla po `_debts`, rod przez slownik
  `StringId -> Clan` zbudowany raz w `ClanIncomeBook.Daily` i przekazany parametrem - bez `Clan.FindFirst`). `IronBank.Limit` zostaje bez zmian i 169 go NIE wola.

### 5.6 `Armoury/src/MenPurse.cs`
- `OnPartyDestroyed` (`:63-81`): galaz zwyciezcy AI po `win.LeaderHero.ChangeHeroGold(third);` -> `ClanIncomeBook.NoteInflow(win.LeaderHero, third, ClanIncomeBook.KThird);`;
  obie galezie zwyciezcy -> `MoneyLedger.NotePurseGone(purse, false);`; galaz miasta (`:78`) po `ChangeGold(purse)` ->
  `MoneyLedger.NotePurseGone(purse, true); MoneyLedger.Note169(MoneyLedger.N169PurseGone, t, purse);`.
- `SellAiSurplus` (`:346-348`): po `st.Town.ChangeGold(-unit);` -> `MoneyLedger.Note169(MoneyLedger.N169Surplus, st, -unit);`; po `mp.LeaderHero.ChangeHeroGold(third);` ->
  `ClanIncomeBook.NoteInflow(mp.LeaderHero, third, ClanIncomeBook.KThird);`.
- Gracz: `:254` (`ChangeGold(-unit * n)`) -> `Note169(N169Surplus, st, -unit * n)`; `:301` (`ChangeGold(bestPrice)`) -> `Note169(N169Kit, st, bestPrice)`.

### 5.7 Pozostale nasze przeplywy kas (tylko `Note169`, kwota ze znakiem jak `ChangeGold`)
`AiWear.cs:299` i `:338` -> `N169Repair` (`+unit`, `+paid`); `TroopSelfMend.cs:117` i `:179` -> `N169Repair`; `RecruitKit.cs:89` -> `N169Other` (`-price`);
`UniqueSpoils.cs:211` -> `N169Other` (`+price`); `CaravanBulk.cs:629` -> `N169Other` (`-refund`); `SpoilsSeal.cs:392` -> `N169Other` (`-due`);
`OutlawLaw.cs:1147` -> `N169Other` (`+spend`), `:1329` -> `N169Other` (`-pay`) (w bloku `Note169` sam wraca - nie szkodzi).
Wykonawca sprawdza kazde miejsce w drzewie przed wpisaniem (numery z rozpoznania 1); `Settlement` = osada, ktorej kase rusza `ChangeGold`.

### 5.8 `Armoury/src/WorkshopTrade.cs`
Wszystkie cztery metody `CirculationWindows.Workshop*` maja WLASNY try/catch ze `Stumble` (D17) - nie moga rzucic do `OutPostfix`,
gdzie `ArtisanInputs.OutPost` stoi poza starym `try` (`WorkshopTrade.cs:442-447`; jego pominiecie zmienialoby wsad rzemieslnikow 147, czyli gre).
- `OutPrefix` (`:436-440`): PIERWSZA linia ciala `CirculationWindows.WorkshopOutPre(__1);` (przed `ArtisanInputs.OutPre`).
- `OutPostfix` (`:442-447`): PIERWSZA linia `CirculationWindows.WorkshopOutPost(__1);` (przed istniejacym `try` i przed `ArtisanInputs.OutPost` -
  wplaty rzemieslnikow 147 nie moga wejsc do okna).
- `InPrefix` (`:424`): zamienic wyrazenie na cialo `{ CirculationWindows.WorkshopInPre(__2, __3); __state = ...jak dzis...; }` - dodac parametr `Town __2`.
- `InPostfix` (`:426-430`): pierwsza linia `CirculationWindows.WorkshopInPost(__2, __3);` - dodac parametr `Town __2`.
- W `CirculationWindows`: pola statyczne `_wsW, _wsTown(Settlement), _wsK0, _wsC0` (metody nie sa wspolbiezne, kazda Pre ma swoja Post);
  Post liczy tylko przy `ReferenceEquals(w, _wsW)`; `dK = kasa1 - _wsK0`, `dC = cap1 - _wsC0`; `Cls[c, WorkshopOut|In] += dK` (gdy !InBlock),
  `Sum[NWorkshopOut|InKasa] += dK`, `Sum[NWorkshopOut|InCap] += dC` (N3 = suma czterech); `c = ClassOf(workshop.Settlement)` (zamki przez BK TickCastle).

### 5.9 `Armoury/src/BuildFunding.cs`
`MineRevenuePostfix` (`:96-99`, wpiety `:307`): po `town.ChangeGold(revenue)` dopisac `CirculationWindows.NoteMineWages(revenue);` (tylko licznik `IMinesBack` w oknie O20).

### 5.10 `Armoury/src/ArmouryBehavior.cs`
- Konstruktor (`:389`): na koncu listy (przed `}`) `CirculationWindows.Reset(); ClanIncomeBook.Reset();`.
- `SyncData` (`:391-497`): NOWY osobny try po glownym (przed `MendStock`):
  `try { string ci = ClanIncomeBook.Export(); SaveText.Sync(dataStore, "arm_clanincome", ref ci); if (dataStore.IsLoading) ClanIncomeBook.Import(ci); } catch (Exception e) { Log.Error("SyncData.ClanIncome", e); }`
- `RegisterEvents` obok nasluchow ksiegi (`:542-544`): `CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, CirculationWindows.OnPartyDestroyed);`
  i `CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, CirculationWindows.OnKingdomDestroyed);`.
- `OnSessionLaunched` (`:987`, po `MountedWage.EnsureContextHooks`): `try { CirculationWindows.EnsureModelHooks(); } catch (Exception e) { Log.Error("CirculationWindows.EnsureModelHooks", e); }`.
- `OnDailyTick`: miedzy `IronBank.Daily()` (`:1259`) a `SupplyDemand.DailyTrade()` - `try { ClanIncomeBook.Daily(); } catch (Exception e) { Log.Error("ClanIncomeBook.Daily", e); }`
  (po rentach, zwrocie, mennicy i Banku dnia; przed `MoneyLedger.Daily`).

### 5.11 `Armoury/src/SubModuleMain.cs`
Przed `GoodsLedger.ApplyAll` (`:136`): `try { CirculationWindows.ApplyAll(_harmony); } catch (Exception e) { Log.Error("CirculationWindows.ApplyAll", e); }`.
Dlaczego tu: po wszystkich naszych latkach na te same metody (BuildFunding, WorkshopTrade, MoneyLedger), a przed ksiega towarow, ktora musi byc ostatnia.
W SRODKU `CirculationWindows.ApplyAll` kazde okno wpinane jest we WLASNYM try (wzor `MoneyLedger.ApplyAll :533-590`): pomocnik
`Wire(int w, Type t, string method, Type[] args, string pre, string fin, string post)` robi `AccessTools.Method`, `h.Patch(...)`, `Wired[w] = true`
i do listy `done`; brak typu/metody -> `miss` z "BRAK (brak typu X | brak metody Y)", wyjatek `h.Patch` (np. zly typ parametru `__N`) -> `miss` z
"BRAK (blad: <e.Message>)" i `Wired[w] = false` - dalsze okna wpinaja sie normalnie. Linia startowa 6.1 wymienia kazde okno osobno.

### 5.12 `Armoury/src/Settings.cs` + `tools/gen_mcm.py`
Trzy linie po `GoodsLedgerEnabled` (`:411`) - rozdz. 9; potem `python tools/gen_mcm.py` z korzenia drzewa (regeneruje `McmSettings.cs`; RC i GT bez roznic).

### 5.13 Bez zmian
`Log.cs` (nowe linie w glownym logu, CSV przez `Log.Csv`), `KingdomLedger.cs`, `CrashScribe`, `RealisticCaptivity`, `GrandTourney`.

---

## 6. Linie dzienne (zakres b)

Wszystkie: jedna linia tekstu, "dzien N" wedlug D9, liczby calkowite bez separatorow tysiecy (jak dzisiejsza ksiega), znak `+`/`-` przez
istniejace `S()`/`Neg()`, `[P]` pomiar, `[R]` reszta, `-` = pozycja przyszlej paczki albo okno BRAK. Na koncu linii z rachunkiem segment
`kontrola:` z liczbami w postaci `klucz=wartosc` (dla narzedzia z rozdz. 11). Przyklady maja liczby spojne arytmetycznie.

### 6.1 Linia startowa (raz, z `CirculationWindows.ApplyAll`)
```
Obieg (169): ksiega obiegu (tylko log) - okna wpiete 44: notable-dochod, karawany-notabli, pasmo-notabli, ..., kupno-BK, myto (vanilla i BK); BRAK: statki-inne (brak typu NavalDLC.CampaignBehaviors.ShipTradeCampaignBehavior); latki zakladane zawsze, liczenie przy CirculationLedgerEnabled i LogEnabled, probki swiata przy CirculationProbeEnabled (najwyzej 8 na dobe); zaplata karawan za najemnikow do miast (RecruitGoldToSeller): tak; okno rodu z finalizerem: tak.
```
Kazde okno ma swoj wpis: wpiete -> nazwa; nie -> `nazwa BRAK (brak typu X | brak metody Y | blad: <komunikat>)` (5.11). Okna zakladane
w kampanii (linie modelu O40, kapital karawan O17) maja wlasna linie z `EnsureModelHooks` (3.8).

### 6.2 `Pieniadz swiata (bilans - przyczyny)` (po starej linii bilansu)
```
Pieniadz swiata (bilans - przyczyny): dzien 41 | zmiana sumy +187732 [P] = zrodla +2359720 - ujscia 2162288 + netto -1600 + reszta -8100 [R] | ZRODLA z niczego +2359720 [P]: "zakupy" mieszkancow 578000, regulator kas dosypal 77000, rozliczenia rodow na plus 95000, zold oddany do obiegu przez SoldierPay 782000, GiveGoldAction z niczego poza rozliczeniami 793000 (w tym: pasmo notabli 412000, nowi notable 20000, wyplata dochodu notablom 160500, jency sprzedani przez partie 40100, jency do kas twierdz 21100, lup z oblezen 5000, nagrody turniejow 1200, jency z rabunkow BK 3000, statki: sprzedaz osadom i premie zarzadcow 800, statki NavalDLC inne 0, bitwy 0, inne 129300), minus przelew z kapitalu warsztatow i kas karawan notabli zgloszony jako zloto z niczego 21300, lup z cial dla AI 18400, kapital nadany nowym karawanom 37500 (notable 27500, lordowie 10000; 3 karawany), BK sprzedaz niewolnikow 0, zloto bohaterow, ktorzy wrocili do swiata 0, myto (perk Tollgates zarzadcow) 120 | UJSCIA w nicosc 2162288 [P]: rozliczenia rodow na minus 1040388, regulator kas skasowal 350000, z utargu wsi zniklo 47000, GiveGoldAction w nicosc poza rozliczeniami 552000 (w tym: pasmo notabli 367000, awanse 61000, werbunek najemnikow 20000, werbunek ochotnikow od notabli 24000, ochotnicy z mapy 1000, statki: kupno od osad 3000, BetterEconomy 70000 (skarbce zamkow 50000, obozy 10000, inwestycje we wsie 8000, dostep do targu 2000), bitwy 0, inne 6000) minus oddane przez LevyGold 60000, zold karawan notabli 109600 (z kas karawan 81300, z kies notabli 28300), prowizja od sprzedazy partiom 12900 (wsie 11400, miasta i zamki 1500), BK porty 4100, BK kopalnie 6300, BK konwoje ludnosci 11800, BK wydatki lordow 21200 (dylematy 6200, kupno karawan 15000, pozostale 0), zloto bohaterow, ktorzy opuscili swiat 3900 (zgony notabli 2: 1800; zgony lordow 5: 0 - zloto przekazane przed smiercia; inni, w tym Disabled, 2: 2100), kiesy partii bez wodza, ktore zniknely z mapy 8700 (karawany 6100, tabory 700, bandy 1900, garnizony 0, inne 0), clo zdjete z licznika cel 54400 (cala kwota; skarbce dostaly 52000 z kas miast - to przelew) | NETTO (zmiana swiata) -1600 [P]: BK rynek osady -1900, BK rynek wsi +300, warsztaty BK bez pokrycia w kasie 0, bitwy - zloto partii bez wodza 0, przelewy od i do bohaterow poza swiatem 0, skarbce upadlych krolestw 0, warsztaty kupione przez lordow BK (kapital od nowa) 0 | RESZTA -8100 [R] (w naszym ticku dobowym -600, poza nim -7500); bez okien 169 byloby -207880; srednia 28 dob -9400 (z 28 dob) - prog 10000: OK | kontrola: Z=187732 S=2359720 U=2162288 N=-1600 R=-8100 R0=-207880 RB=-600 from=793000 fromW=663700 to=552000 toW=546000 ARYTMETYKA OK.
```
`ARYTMETYKA OK` gdy `Z - S + U - N - R == 0` i `R0 - R == (Z1+...+Z5) - (U1..U9) + N - K1` (w kodzie dwa porownania `long`; inaczej `ARYTMETYKA BLAD <roznica>`).
To tozsamosci z budowy (2.1) - napis lapie tylko blad druku albo przepelnienie, nie blad pomiaru; dlatego nie nazywa sie "TOZSAMOSC OK".
Czy nazwane pozycje sa prawdziwe, mowia: probki swiata w linii 6.8 (T16), `RB` (T15) i "inne" >= 0 (T4). W linii zostaje "suma przyczyn + reszta
= zmiana swiata", bo Jeff ma widziec, ile zlota ma nazwe, a ile nie - wielkosc reszty to wynik pomiaru, nie arytmetyki.
Prog: `OK` gdy `|srednia| < 10000`, `PONAD` inaczej; przy mniej niz 28 dobach dopisek `(z K dob)`. Okno BRAK -> zamiast liczby `-`.

### 6.3 `Pieniadz swiata (rody - przyczyny)` (po starej linii rodow)
```
Pieniadz swiata (rody - przyczyny): dzien 41 | rozliczenia rodow zmienily zloto swiata o -945388 [P] w 309 rozliczeniach | w tym naliczone w modelu finansow (odczytane 309 rozliczen): zold partii -597000, zalog -185000, karawan lordow -33000, trybut zaplacony -0, trybut przyjety +0, udzial w kosztach najemnikow krolestwa -12400, kontrakty najemnikow +41800, wezwanie do wojny zaplacone -0, przyjete +0, splata dlugu wobec korony -6100, dochod za tier (z niczego) +3920 (14 rodow, w tym najemnicy 1680), zapomoga ze skarbca +0 (oczekiwane 0 - BK ja wylacza), podatek BK od bogatych do skarbcow -38900 (przelew), rada: place -5200 / urzad radnego +4100 [P] | dlug wobec korony w trakcie rozliczen: nowy +2300 (3 rodow), splacony -6100 (4 rodow), razem u 41 rodow 612000 | skarbce krolestw w trakcie rozliczen +38900 (podatek BK minus zapomoga i inne) | kontrola: model=309 rozliczenia=309.
```
Zold partii/zalog/karawan z `MoneyLedger._wage[]` (te same liczby co `Przeplywy osad`). "razem u N rodow" = petla raz na dobe po `Clan.All` (`DebtToKingdom > 0`).

### 6.4 `Przeplywy osad (przyczyny)` (po trzech liniach kas)
```
Przeplywy osad (przyczyny): dzien 41 | kasy miast: reszta bez okien 169 -174907, nowe pozycje -132100: prowizja od sprzedazy partiom -29800 (do licznika cel 28300, w nicosc 1500), warsztaty BK - kasa placi za wyroby -92400, warsztaty placa za wsad +8100, BK porty -4100, BK kopalnie -6300, BK konwoje ludnosci -11800, BK rynek osady -1500, BK rynek wsi (wsie placa miastom) +2400, Armoury bez licznika: naprawy +6400, skup nadwyzek zbrojowni -3100, komplety gracza 0, sakiewki rozbitych partii +900, inne -900; reszta po oknach 169 -42807 [R] | kasy zamkow: reszta bez okien 169 +1100, nowe pozycje -200: BK rynek osady -200 (pozostale 0); reszta po oknach 169 +1300 [R] | kiesy wsi: reszta bez okien 169 -11400, nowe pozycje -11300: prowizja od zakupow we wsi -11400 (100% w nicosc), BK rynek wsi -2100, BK rynek osady +2200; reszta po oknach 169 -100 [R] | kontrola: TM0=-174907 TM1=-42807 TZ0=1100 TZ1=1300 TW0=-11400 TW1=-100.
```
`reszta po = reszta bez - nowe pozycje` dla kazdej klasy (`_classRest[c]` z 5.1 pkt 7). Pozycje warsztatow to przelewy z kapitalem
(dla swiata 0 poza `N3`) - stad duze liczby przy malej zmianie swiata.

### 6.5 `Obieg: dzien N` (10.1 pkt 1 - tylko to, co juz dziala w grze)
```
Obieg: dzien 41 | rody wydaly [P]: zold partii 597000 (z kies zeszlo 597000), zold zalog 185000 (zeszlo 185000), zold karawan lordow 33000, powinnosci do korony 39100, budowy do kas osad 21800, dwor -, sprzet i werbunek - | rody dostaly [P]: renta wsi 176000, zawor miast 342000, zawor zamkow -, zwrot zoldu od korony 265000 (nalezny 340000), mennica i monopole krolow 4100, trzecia lordow z nadwyzek ludzi 9100, wplywy spoza rodu poza rozliczeniem 182000 (z niczego 75000, od osad 98000, od innych 9000), renty od korony -, zapomoga 0, dochod modelu gry (naliczony, do D) 1412000 | sakiewki ludzi [P]: stan 4807656 (+163490), zold wplynal 597000, wydaly w miastach na zycie 371000, naprawy 6400, z rozbitych partii 12300 (do zwyciezcow 11400, do miast 900), markietani -, ze zwolnionymi - | kasy miast [P]: stan 7838940 (+27289), zapas kupcow 6612000, nadwyzka ponad zapas 3110000 w 61 miastach, ponizej polowy zapasu 11 miast, zawor do pana 342000, do korony -, utarg taborow wsi zaplacony 374000 (2140 wizyt; towar niesprzedany 812 szt.), dosypka regulatora 71000 (tryb 1 -), bezpiecznik korony - | kasy zamkow [P]: stan 3303732 (+11202), zold zalog do kas 41000, zawor - | kiesy wsi [P]: stan 286388 (+370), renta do panow 176000 | korona [P]: wplywy - powinnosci 39100, danina wojenna 61000, clo: z licznika cel zdjeto 54400 (w nicosc w calosci - licznik jest posiadaczem), skarbce dostaly 52000 z kas miast (przelew), podatek BK 38900, trybut przyjety 0; wyplaty - zwrot zoldu 265000 (50% z 680000 zaplaconego zoldu), zapomoga 0, dochod za tier najemnikow (dzis z niczego) 1680, renty wedlug lenn -; skarbce razem 56216183 (-188000), ponizej 0.5 mln: 4 z 29 | Bank [P]: pozyczki 2 (41200), splaty 14 (9320), spoznienia 1, bankructwa 0, kapital 4773092.
```
Zrodla liczb: zold - `_wage[]` i `SoldierPay.Last*` (gdy `!LastWatching` -> "zeszlo -"); powinnosci/zwrot/danina/clo/mennica/monopole - `KingdomTreasury.Last*`;
renty - `PopulationLaw.DayVillageRent/DayTownRent`; budowy - suma `_mark[c, MBuild]`; trzecia i wplywy spoza rodu - `ClanIncomeBook.Day*`; dochod modelu -
`ClanIncomeBook.LastModelIncomeSum` ("-" gdy ksiega D wylaczona); sakiewki - `hold[HPurses]`, `SoldierPay.LastToPurse`, suma `_noteIn[*, NLife]`,
`_n169In[*, N169Repair]`, `_purseGone*`; kasy - `hold`, petla raz na dobe po `Town.AllTowns` (zapas `10000 + 12 * Prosperity`, jak regulator);
utarg/regulator - `_vPaid`, `_vVisits`, `_vUnsold`, `_regIn`; zold zalog do kas - `SoldierPay.LastToTowns + LastToCastles` (w linii kas zamkow tylko `LastToCastles`);
podatek BK, trybut, tier - `Model[]`; skarbce - `hold[HKingdoms]` + petla po `Kingdom.All` (`!IsEliminated`, `KingdomBudgetWallet < 500000`); Bank - `IronBank.Last*`, `CapitalNow`.

### 6.6 `Obieg: notable i karawany: dzien N` (10.1 pkt 2)
```
Obieg: notable i karawany: dzien 41 | notable [P]: kiesy razem 29706759 (+211000) | pasmo 4500-10500: nadwyzka w nicosc -367000, dosypka z niczego +412000 (okien 3502) | nowi notable +20000 | dochod z aktywow: wyplata zgloszona jako z niczego +160500 (3388 notabli), z kapitalu warsztatow i kas karawan zeszlo -21300, zloto z niczego netto (zaulki, majatki BK, minus podatek warsztatow BK) +139200 | karawany notabli [P]: 731 karawan u 598 notabli, zold nalezny 158000, zold w nicosc -109600 (z kas karawan -81300, z kies notabli -28300), niedoplata 48400 (karawan, ktorych kasa nie starczyla na zold: 212 - placil notabl), doplaty notabli do 5000 +23100 (przelew); po 164: zold do sakiewek ludzi karawan | karawany lordow: zold naliczony -33000 (152 karawan) | nowe karawany: 3 (notable 2, lordowie 1, w tym kupione przez lordow BK 1), kapital nadany z niczego +37500 (w kasach zaraz po wejsciu do osady 30000 - reszta to przelewy tej samej chwili: dochod BK do wlasciciela, oplaty BK do licznika cel, werbunek) | karawany, ktore zniknely z mapy: 9, ich kiesy przepadly -6100 | zmarli notable: 2, zloto przepadlo -1800 (zmarli lordowie 5: zloto oddane nastepcom przed smiercia, przy zgonie 0).
```
"karawan, ktorych kasa nie starczyla" = liczba karawan z `p < w` w prefiksie (wtedy placi notabl - `ICaravanShortN`, liczone w petli prefiksu); "niedoplata" = `zold nalezny - zold w nicosc` (dokladnie, z pomiaru) - czesc zoldu, ktorej nikt nie zaplacil, bo notabl nie mial.

### 6.7 `Obieg: BK i BetterEconomy: dzien N` (4.2 "Obieg: BK" i "Obieg: lordowie BK")
```
Obieg: BK i BetterEconomy: dzien 41 | osady BK [P]: porty -4100 (12 miast), kopalnie -6300 (kasa zaplacila 12600, wrocilo jako place gornikow 6300), konwoje ludnosci -11800 (37 konwojow), rynek osady -1900 (kasy +500: miasta -1500, zamki -200, wsie +2200; notable -2400), rynek wsi +300 (wsie -2100, miasta +2400), sprzedaz niewolnikow 0 | lordowie BK [P] (w nicosc): tytuly 0 (roszczenia 0, nadania 0, odebrania 0, uzurpacje 0), pasowanie z nowym rodem 0, koszty laski dworu 0, dylematy 6200, przeniesienie dworu (gracz) 0, wyposazenie rycerza (BKROTPatch) 0, donatywa imperium 0, material na koniec budowy - (BuildFunding wlaczony), kupno karawan 15000 (1 karawana; jej kapital 10000 jest w zrodlach) | warsztaty kupione przez lordow BK [P]: 0 (kapital od nowa 0; zaplata dawnemu wlascicielowi to przelew) | BetterEconomy [P] (w nicosc): skarbce zamkow 50000, obozy 10000, inwestycje we wsie 8000, dostep wsi do targu 2000; skarbce zamkow BEE poza swiatem | nieczynne w tym zestawie modow: rzemieslnicy WorkshopData, BKTournamentManager, nadwyzka zywnosci BK (czynny model zywnosci: ROT.Models.ROTSettlementFoodModel), zaopatrzenie majatkow BK (bez Economy Overhaul).
```

### 6.8 `Obieg: okna (kontrolka): dzien N`
```
Obieg: okna (kontrolka): dzien 41 | notable-dochod 6120/3388, karawany-notabli 3502/598, pasmo-notabli 3502/1874, nowy-notabl 2/2, awanse 1140/980, werbunek 2610/2390, statki 14/3, statki-inne BRAK, jency 412/398, oblezenie 1/1, turniej 3/2, rabunek-BK 5/5, BEE 61/40, bitwy 88/0, prowizja 5120/3010, lup-z-cial 2210/2190, nowe-karawany 3/3, kapital-karawan 3/3, rynek-osady 1604/37, porty 97/12, kopalnie 97/31, konwoje 9120/37, niewolnicy 0/0, rynek-wsi 1402/14, tytuly 0/0, pasowanie 0/0, dwor 0/0, dylematy 3/2, przeniesienie-dworu 0/0, rycerz-BKROT 41/0, donatywa 0/0, material-budowy 0/0, kupno-BK 2140/1, myto 31/2, warsztaty-wyrob 3810/3810, warsztaty-wsad 2240/2240, stan-bohatera 312/9, partie-znikaja 160/52, krolestwa 0/0, linie-modelu 309/309 | poza watkiem 0, potkniecia 0, zagniezdzone 4, pominiete w rozliczeniach rodow 0, rozliczenia rodow przerwane wyjatkiem 0, nowe karawany bez odczytu kapitalu 0 | probki swiata wokol okien [P]: 8 (zgodne 7; rozjazd 1: konwoje -350), od startu 312 probek, rozjazdy w 3 oknach (konwoje 2, rynek-osady 1) | zgony [P]: notable 2 (zloto przepadlo 1800), lordowie 5 (zloto przekazane przed smiercia, przy zgonie 0), inni 2 (2100) | koszt [P]: przeliczenie doby 6.1 ms (budzet rodow 4.3 ms), okna ok. 9.7 ms (probka 1/256), probki swiata 4.2 ms (16 przegladow, 0.26 ms na przeglad) | bohaterowie Disabled: 3, zloto 0 (poza swiatem, informacja).
```
Format pozycji: `nazwa wywolan/trafien`; okno niewpiete: `nazwa BRAK`. "probki swiata": dzisiejsze probki 2.4 (zgodne = `dw == dn` co do zlotowki;
rozjazd = nazwa okna i `dw - dn`), plus licznik od startu sesji i lista okien z rozjazdem (`ProbeBad` sumowane w polu sesji, nie zerowane w `ClearDay`).
Koszt okien = suma `Ticks[w] * Opened[w] / max(1, Sampled[w])` w ms - same nasze ciala latek (poprawka po recenzji, patrz 3.1)
(`Stopwatch.Frequency`). "przeliczenie doby" = Stopwatch wokol `ClanIncomeBook.Daily` + nowych linii `MoneyLedger.Daily`.
"bohaterowie Disabled" = petla raz na dobe po `Campaign.Current.CampaignObjectManager.DeadOrDisabledHeroes` z `HeroState == Disabled`
(tylko informacja do R6).

### 6.9 `Budzet rodow (na sucho): dzien N` (z `ClanIncomeBook.Daily`)
```
Budzet rodow (na sucho): dzien 41 | rody AI 316 (pokoj 41, wojna 275; najemnicy i pomniejsze 38; bez zmierzonego zoldu 2) | D - dochod staly 28 dob [P] (dochod brutto modelu + renty + wplywy spoza rodu, D19): mediana 2150, 10% 610, 90% 9800, razem 1412000; dla porownania saldo modelu (po zoldzie, z rozliczen) dodatnie u 121 rodow, razem +388000, ujemne u 189, razem -611000; pierscien: pelny u 0 rodow, srednio 15 zmierzonych dob z 28 (brakujace doby = G/60) | pulap zoldu (6.1) razem 702000 a zold naliczony (partie + zalogi) 781000 (111%) | ponad pulapem 133 rodow (ponad 1.10 x: 118, od 3 dob: 104), nadwyzka zoldu 196000, w tym na suficie partii 21 | zwolnionych by dzis (15% nadwyzki od 3. doby): 5380 ludzi w 104 rodach | na suficie partii (90% limitu wielkosci): 171 rodow | ponizej rezerwy wojny R: 88 rodow | glowy < 5000: 92 (z miastem 9, z zamkiem 42, bez lenna 41), rodziny < 5000: 21 (bez dworzan BK) | najwiekszy nadmiar: Tully 9100, Frey 7400, Bolton 6900 | dwor -, sprzet -, majatki BK - (kod BK nieczynny bez Economy Overhaul) | plik: budzet-rodow.csv | kontrola: rody=316 pokoj+wojna=316 ponad<=rody TAK.
```

### 6.10 `Dlugi (na sucho): dzien N` (rozdz. 8; ksztalt REGULY 5.9 + R1)
```
Dlugi (na sucho): dzien 41 | szczeble wg 8.3 (dzisiejszy stan Banku): kredyt 19, zaleglosc 1, zajecie 3, wyprzedaz 0 | zajeloby dzis 2940 (renty wsi 1310 z 9 wsi, kiesy ponad podloge 1630; utarg wsi -) | prognoza splaty w zajeciu: mediana 41 dni, najdluzej 118 (Errol) | wierzyciele: Bank 402100, korona (dlug wobec korony z gry) 612000 u 41 rodow, skarbce -, rody - | pozyczyliby wg 8.2: na wojne 12 (limit 15 x D razem 391000), na okup glowy 3, na trybut -; zablokowani: zaleglosc 4, dlug wobec korony 41, bez nadwyzki (D <= zold) 97 | Bank dzis a 8.2: dluznikow 23, dlug 512400; z dlugiem ponad 15 x D: 17 (nadwyzka 318000); limit Banku wg jego wzoru (dochod modelu x IronBankIncomeDays + lenna, x zaufanie, x1.5 dla wrogow bankrutow) razem 81200000, wg 8.2 (15 x D) razem 21180000 | zapas w dniach zoldu (R1): ponizej 10: 61, 10-20: 33, 20-45: 58, 45-90: 71, ponad 90: 93.
```

### 6.11 `budzet-rodow.csv` (`Log.Csv`, katalog sesji, separator `;`, nazwy przez `Clean` jak `PeopleLedger`)
Naglowek:
`dzien;dzien_gry;rod_id;rod;krolestwo;rodzaj;wojna;kiesa_glowy;G;D;dni_pomiaru;wplyw_doby;dochod_modelu;saldo_modelu;renty;zwrot_korony;mennica_monopole;zdarzenia;trzecia;pulap;zold_partii;zold_zalog;zold_karawan;ludzi_partie;ludzi_zalogi;limit_partii;na_suficie;dni_ponad;zwolnieni_na_sucho;R;dlug_bank;spoznienia;bankrut;dlug_korona;szczebel;kandydat_pozyczki;limit_8_2`
`rodzaj` = `miasto` / `zamek` / `bez lenna` / `gracz` / `najemnik`; rod gracza ma pusty `pulap` i dalsze kolumny budzetu; wiersz z bledem nie trafia do pliku
(wzor `PeopleLedger.cs:339-367`). Pierwszy zapis -> jedna linia `Budzet rodow: plik CSV <sciezka>.`.
`saldo_modelu` = saldo naliczone przy ostatnim rozliczeniu rodu (O40, `NoteModelSaldo`; puste, gdy O40 BRAK albo rod bez rozliczenia) - tylko do porownania z D (D19).

---

## 7. D na rod (zakres c) - `ClanIncomeBook.cs`

Definicja (6.1): D = srednia 28 ostatnich dob wplywow do kies rodu z zewnatrz rodu, bez pozyczek i wyprzedazy. Wplyw doby rodu `c`:
- (a) `max(0, Campaign.Current.Models.ClanFinanceModel.CalculateClanIncome(c, false, false, false).ResultNumber)` - ta sama definicja co
  `KingdomTreasury.cs:60`, `BuildFunding.cs:154`, `IronBank.cs:110` (dochod BRUTTO modelu: podatki osad, warsztaty, karawany, kontrakty najemnikow, tier, trybut).
  Odstepstwo od slow projektu 6.1 ("dodatnie saldo modelu gry") jest swiadome (D19): saldo liczy sie PO zoldzie, wiec D zalezalby od zoldu,
  a pulap zoldu od D - rod z wiekszym wojskiem dostawalby nizszy pulap i zwalnialby dalej (sprzezenie), a rod, ktory zwolnil, dostawalby wyzszy.
  Brutto mowi, ile rod ZARABIA, niezaleznie od tego, ile wydaje. Saldo naliczone (z O40, `NoteModelSaldo`) idzie obok do CSV (`saldo_modelu`)
  i do linii 6.9, zeby 166 mogl porownac obie wielkosci. Wartosc (a) liczona RAZ na rod na dobe i trzymana w `IncomeToday` (slownik `Clan -> float`,
  wielokrotnego uzytku) - z niej korzysta tez limit Banku na sucho (rozdz. 8, D21);
- (b) `PopulationLaw.RentToday[c]` (renta wsi i zawor miast z dzis);
- (c) zwrot korony (`NoteInflow` KRefund, `KingdomTreasury.cs:183`);
- (d) mennica i monopole krola (`NoteInflow` KCrownLevies, `:271`, `:279`);
- (e) zdarzenia gry poza rozliczeniem rodu i poza naszym tickiem: odbiorca `rh` zywy (`!OutOfWorld`), `rh.Clan == c`, `c` nie banda,
  platnik spoza rodu (`gh == null || gh.Clan != c`) - jency, oblezenia, turnieje, skup lupu, okupy, majatki BK (`OnEvent`, 5.1 pkt 3), z podzialem
  na `z niczego` (gNone) / `od osad` (platnik osada) / `od innych`;
- (f) trzecia lorda z nadwyzek i z sakiewek rozbitych partii (`NoteInflow` KThird, `MenPurse`).
Pozyczka Banku (`IronBank.Lend`, `ChangeHeroGold` bez zdarzenia) wypada sama; zwroty wlasnych zaplat (`LevyGold`, `RecruitCost`) tez (bez zdarzenia, poza NoteInflow).
G = kiesa glowy + kiesy doroslych zywych lordow rodu (`c.AliveLords`, `!h.IsChild`) - w tym samym przebiegu.

Rekord:
```csharp
internal sealed class Rec
{
    public readonly int[] Ring = new int[28];      // wplyw kolejnych dob (int, przyciety do int.MaxValue)
    public int Head, Filled, Seed = -1, Streak;     // Seed = G/60 przy pierwszym zobaczeniu rodu; Streak = doby z zoldem > 1.10 x pulap
    public long Today, TodayRefund, TodayCrown, TodayEvtNone, TodayEvtSettl, TodayEvtOther, TodayThird;   // od ostatniego Daily
    public long WageAccLord, WageAccGar, WageAccCar;          // w biezacym rozliczeniu rodu
    public int WageLastLord, WageLastGar, WageLastCar;        // z ostatniego pelnego rozliczenia rodu
    public bool HadTick;                                       // bylo choc jedno zmierzone rozliczenie
    public int SaldoLast; public bool SaldoSeen;               // D19: saldo naliczone przy ostatnim rozliczeniu (O40) - tylko CSV i linia 6.9, nie zapisywane
}
private static readonly Dictionary<string, Rec> _book = new Dictionary<string, Rec>();   // klucz Clan.StringId (zapis)
private static readonly Dictionary<Clan, Rec> _byClan = new Dictionary<Clan, Rec>();     // pamiec podreczna sesji
```
- `Of(Clan c)`: `_byClan` -> `_book[c.StringId]` -> nowy `Rec` (do obu). Bez LINQ; alokacja tylko dla nowego rodu.
- `NoteInflow(Hero h, int amount, int kind)`: `if (!On || h == null || amount <= 0) return; var c = h.Clan; if (c == null || c.IsBanditFaction) return;` -> `Of(c)` i pola `Today*`; sumy dnia `Day*` do linii Obieg.
- `OnEvent(...)`: warunki z (e); `if (inClan || inBlock) return;`.
- `ClanTickOpen(Clan c)`: `_open = Of(c); _open.WageAcc* = 0`. `NoteWage(Clan c, int k, int wage)`: gdy `c != null && _open != null && _open == Of(c)` -> `WageAcc*` wedlug k
  (WLord -> Lord, WGarrison -> Gar, WCaravan -> Car, WOther pomijany). `ClanTickClose(Clan c)`: `WageLast* = (int)WageAcc*; HadTick = true; _open = null`.
  `ClanTickAbort()` (z finalizera D16): `_open = null` bez zmiany `WageLast*` (przerwane rozliczenie nie nadpisuje ostatniego pelnego).
  `NoteModelSaldo(Clan c, int saldo)` (z O40): `Of(c).SaldoLast = saldo; SaldoSeen = true`. Wszystkie `Note*`, `On*`, `ClanTick*` maja wlasny try (D17).
- `Daily()` (raz na dobe, w bloku): dla kazdego `c` w `Clan.All` (pominac `IsBanditFaction`, `IsEliminated`, `Leader == null`):
  `G`; `if (rec.Seed < 0) rec.Seed = (int)Math.Min(int.MaxValue, G / 60)`; `inflow = (a) + (b) + rec.Today` (Today juz zawiera c, d, e, f);
  `Ring[Head] = clamp(inflow)`; `Head = (Head + 1) % 28`; `Filled = Math.Min(28, Filled + 1)`; zerowac `Today*`;
  `D = (suma Ring[0..Filled) + (28 - Filled) * Seed) / 28.0`. Rody zniklne (nie ma ich w `Clan.All` albo `IsEliminated`) -> usunac z `_book` i `_byClan`
  (lista kluczy wielokrotnego uzytku). Potem - TYLKO gdy `Settings.Current.LogEnabled` - rachunek budzetu i dlugow (rozdz. 8), linie 6.9-6.10 i CSV.
  Przy `LogEnabled = false` biegnie wylacznie pierscien D: (a) i G na rod, zapis do pierscienia, zerowanie `Today*` - bez petli po partiach,
  lennach i zalogach, bez krolestw bankrutow, bez CSV (D11: D ma miec historie, reszta sluzy logowi).
- Sumy dnia do linii `Obieg` (6.5): pola `Day*` (`DayRefund, DayCrown, DayEvtNone, DayEvtSettl, DayEvtOther, DayThird`) rosna razem z `Today*`;
  na koncu `Daily` kopiowane do `Last*` (plus `LastModelIncomeSum` = suma (a) po wszystkich rodach, `LastRentSum` = suma (b)) i zerowane.
  `MoneyLedger.Daily` (ten sam blok, pozniej) czyta tylko `Last*`. Gdy `On == false`: `Last*` = -1 i linia drukuje `-`.
- `internal static double StableIncome(Clan c)` - D albo -1 (dla przyszlych paczek; w 169 nikt go nie wola w logice gry).
- Zapis: `Export()` = `"v1;" + rekordy` rozdzielone `;`, pola `|`: `StringId|Seed|Head|Filled|Streak|Today|WageLastLord|WageLastGar|WageLastCar|HadTick(0/1)|r0,r1,...,r27`
  (liczby `InvariantCulture`). `Import(s)`: pusty albo null -> pusta ksiega (stary zapis - start D(0) = G/60 w pierwszym `Daily`); zly rekord pominac i policzyc;
  jedna linia `Budzet rodow: wczytano N rodow (bledne M).` przy pierwszym `Daily` po wczytaniu. Rozmiar: ok. 400 rodow x ok. 200 znakow = ok. 80 KB -> `SaveText` tnie na kawalki po 8000 znakow.
  Obiektow `Clan` NIE szukac w `Import` (lekcja 161) - klucz to `StringId`.
- `Reset()`: `_book.Clear(); _byClan.Clear(); _open = null; Day* = 0; Last* = 0;` (konstruktor `ArmouryBehavior`).
- `On` = `Settings.Current.ClanIncomeBookEnabled`. Przy wylaczonym: brak `Daily` i brak `Note*`, ale `Export` dalej zapisuje to, co jest (dane nie gina).

Dlaczego tak: (a)+(b) to definicja dochodu juz uzywana przez 3 nasze moduly; zdarzenia poza rozliczeniem daja lup i jency, ktorych model nie zna;
stale `Seed` (zamiast biezacego G/60) daje stabilny start, a doby z 1. dnia kampanii (sztucznie wysoki dochod modelu, K14) waza tylko 1/28.

---

## 8. Budzet rodow i dlugi na sucho - rachunek (w `ClanIncomeBook.Daily`)

Rod AI = `c != Clan.PlayerClan && !c.IsBanditFaction && !c.IsEliminated && c.Leader != null && !c.StringId.StartsWith("bk_courtiers_")`
(dworzanie BK: `BKCourtierBehavior.cs:19`). Wojna = `c.MapFaction` ma w `FactionsAtWarWith` choc jedno krolestwo (`IsKingdomFaction`) - liczone
raz na frakcje na dobe (`Dictionary<IFaction,bool>` wielokrotnego uzytku). Zold = `WageLastLord + WageLastGar` (naliczony przez gre przy ostatnim
rozliczeniu = `min(zold, budzet)`, ta sama liczba co `Zold:` i ksiega).

Stale (`const`, z komentarzem "6.4 projektu, przebieg na sucho - klucze MCM w 166"): PeaceWageShare 0.25, WarWageShare 0.55, WarChestToWages 0.8,
WarChestDays 45, ReserveDaysPeace 60, WarReserveDays 20, WarReserveFloor 20000, BudgetHysteresis 1.10, BudgetHysteresisDays 3, ReleasePerDay 0.15,
CeilingFill 0.9, IronBankIncomeDays82 15, InstalmentShare 0.10, SeizeFloor 38000, SeizeWageDays 10.

```
R = max(20000, 20 * D)
pokoj: pulap = 0.25 * D * (G < 60*D ? 1 - 0.1 * (1 - G/(60*D)) : 1)
wojna: pulap = (0.55 * D + (G > R ? 0.8 * (G - R) / 45 : 0)) * (G < R ? 0.8 + 0.2 * G / R : 1)
D <= 0: pokoj 0; wojna tylko czesc z zapasu
Streak: zold > 1.10 * pulap -> Streak++ (raz na dobe), inaczej Streak = 0
zwolnieni (na sucho) = Streak >= 3 ? max(1, ceil(0.15 * (zold - pulap) / sredni_zold_na_czlowieka)) : 0
sredni_zold_na_czlowieka = zold / max(1, ludzi_partie + ludzi_zalogi)
ludzi_partie = suma c.WarPartyComponents[i].MobileParty.MemberRoster.TotalRegulars (aktywne); limit_partii = suma .Party.PartySizeLimit
ludzi_zalogi = suma c.Fiefs[i].GarrisonParty?.MemberRoster.TotalRegulars
na_suficie = ludzi_partie >= 0.9 * limit_partii (i limit_partii > 0)
```
Uwagi: regula 6.2 jest na partie - na sucho liczymy na rod (dokladnosc wystarczy do oceny skali); rody "bez zmierzonego zoldu" (`!HadTick`) w liczbie, bez pulapu.

Dlugi (8.2-8.3; tylko odczyt `IronBank.TryGetDebt`, `c.DebtToKingdom`, `PopulationLaw.RentVillageToday`):
- szczebel: brak dlugu `-`; `missed == 0 && !defaulted` -> kredyt; `missed 1-2` -> zaleglosc; `missed >= 3 || defaulted` -> zajecie;
  zajecie i `principal / max(1, renta_wsi_rodu_dzis) > 364` -> wyprzedaz.
- zajeloby dzis (zajecie+): `renta_wsi_rodu_dzis + max(0, kiesa_glowy - max(38000, 10 * zold))`; utarg wsi `-` (wymaga zaczepu 168 w oknie taboru).
- prognoza splaty (dni) = `principal / max(1, renta_wsi_rodu_dzis)` - mediana i maksimum z nazwa rodu.
- pozyczyliby (8.2): wykluczeni gdy `missed > 0 || defaulted` (zaleglosc), `DebtToKingdom > 0` (dlug koronie), `D <= zold` (bez nadwyzki);
  (1) na wojne: wojna i `G < R` i `zold > pulap` (rod musialby zwalniac - mierzalny odpowiednik "pulap spadlby ponizej 80% wojennego", bo mnoznik biedy
  6.1 nigdy nie schodzi ponizej 0.8 - decyzja projektowa tej paczki, ryzyko R10); (2) na okup: `c.Leader.IsPrisoner`; (3) trybut `-`.
  Limit wg 8.2 = `max(0, 15 * D - principal)`.
- Bank dzis a 8.2: dluznicy z `principal > 15 * D` i suma nadwyzki; limit dzis liczony NA SUCHO tym samym wzorem co `IronBank.Limit`
  (`IronBank.cs:101-123`), ale bez jego kosztu (D21): `(IncomeToday[c] * s.IronBankIncomeDays + miasta * s.IronBankPerTown + zamki * s.IronBankPerCastle)`
  `* (wrog bankruta ? 1.5 : 1) * zaufanie`, gdzie `IncomeToday[c]` = (a) z rozdz. 7 (liczone raz), miasta/zamki z tej samej petli po `c.Fiefs`, co zalogi,
  zaufanie z `IronBank.TryGetTrust`, a "wrog bankruta" = `c.Kingdom` w wojnie z ktorymkolwiek krolestwem ze zbioru `DefaultedKingdoms` (zbior liczony
  RAZ na dobe; sprawdzenie na rod = petla po zbiorze, zwykle 0-5 krolestw). Suma po rodach AI z krolestwem. `IronBank.Limit` nie jest wolany.
  Gdyby wzor `Limit` sie zmienil, linia startowa 169 tego nie wykryje - komentarz przy obu miejscach "wzor jak IronBank.Limit / ClanIncomeBook".
- R1: dni zapasu = `G / max(1, zold)` w przedzialach <10, 10-20, 20-45, 45-90, >90.

---

## 9. Wylaczniki MCM (zakres d)

W `Armoury/src/Settings.cs` po `:411` (grupa "Army purchases", jak inne ksiegi), kazda linia w CALOSCI w jednej linii (regex `gen_mcm.py`):
```csharp
public bool CirculationLedgerEnabled = true;       // write the daily money circulation ledger: which named causes create or destroy gold in the world (notable caravan wages, sales commission, battlefield loot, Banner Kings and BetterEconomy spending, ship trade, prisoner sales, sieges, deaths), what clans paid and earned, the crown, the Iron Bank and the notables - log only, changes nothing in the game
public bool CirculationProbeEnabled = true;        // check the circulation ledger against the whole world: at most 8 times a day count all gold in the world just before and just after one measured game action and compare the change with the causes the ledger named for it - log only, costs two full counts of the world per check
public bool ClanIncomeBookEnabled = true;          // keep every clan's steady income (average of its last 28 days, saved with the game) and write a dry run of the planned clan budget and debt rules: wage ceiling against wages paid, clans over the ceiling, men who would be released, clans that would borrow or face seizure - log only, changes nothing in the game
```
Potem `python tools/gen_mcm.py` (nie `python3`). Oczekiwane: Armoury 680 -> 683 pozycji, RC 104 i GT 40 bez roznic.
Stare pomiary `MoneyLedger` NIE sa pod nowym wylacznikiem (regresja). `CirculationLedgerEnabled = false` -> nowe linie znikaja, stare linie
identyczne jak przed paczka (poza przypadkiem D16), latki dalej wpiete (wracaja w pierwszej instrukcji). `CirculationProbeEnabled = false`
(dziala tylko razem z `CirculationLedgerEnabled`) -> zero dodatkowych przegladow swiata, w linii 6.8 "probki swiata -", reszta bez zmian.
`ClanIncomeBookEnabled = false` -> brak linii 6.9-6.10 i CSV, D zamrozone, dane zostaja w zapisie.
Dlaczego osobny klucz dla probek: to jedyny element 169, ktory robi pelne przeglady swiata czesciej niz raz na dobe - Jeff moze go wylaczyc po
udanym autotescie (T16) bez utraty ksiegi.

---

## 10. Wydajnosc (zakres e)

| Element | Wywolan na dobe (rok T3) | Praca na wywolanie | Szac. na dobe |
|---|---|---|---|
| `OnGold` + `NoteCounted` + `OnEvent` w `OnGoldTraded` | dziesiatki tysiecy | 2-6 porownan; `Eff` tylko przy otwartym oknie; slownik `Clan -> Rec` tylko dla odbiorcy-lorda | < 3 ms |
| O01 notable-dochod | 5-8 tys. | nie-notabl 2 odczyty; notabl 2 petle po 0-5 | < 2 ms |
| O02 karawany-notabli | ~3 500 (600 z praca) | 1-3 karawany + `TotalWage` | < 2 ms |
| O15 prowizja | kilka tys. | 1-3 odczyty int | < 1 ms |
| O16 lup-z-cial | 1-10 tys. w wojnie | 2 odczyty | < 1 ms |
| O21 konwoje | tysiace | 1 porownanie typu | < 1 ms |
| O45 kupno-BK | tysiace (kazde wejscie partii) | bramka 3 odczyty; partia lorda: 1 kiesa + do 8 kapitalow (tylko miasto) | < 1 ms |
| O46 myto (2 latki) | 2 x tysiace | bramka 5 odczytow; `GetPerkValue` tylko dla karawany w miescie z zarzadca | < 1 ms |
| O17 postfiks `GetInitialTradeGold` | setki-tysiace (model finansow) | 1 porownanie flagi | < 0.5 ms |
| O18 rynek osady | ~1 600 | kasa + 2-6 notabli x2 | < 1 ms |
| O35/O36 warsztaty | setki-tysiace | 4 odczyty int | < 1 ms |
| pozostale okna | setki lub rzadko | O(1); O30 ~400 odczytow, kilka razy na dobe | < 1 ms |
| O40 linie modelu | 309 | 1 `List` z `GetLines()` + petla po ~20 liniach | < 1 ms |
| probki swiata (2.4) | najwyzej 8 | 2 x `WorldTotal()` (szac. 0.2-1 ms kazdy - mierzone w 6.8) | 3-16 ms |
| `ClanIncomeBook.Daily` | 1 | ~400 rodow: (a) RAZ na rod (`IncomeToday`), petle po partiach i lennach, ~650 `PartySizeLimit`, zbior krolestw bankrutow raz; BEZ `IronBank.Limit` (D21) | 3-8 ms (przy `LogEnabled = false` tylko (a) i G: 2-5 ms) |
| nowe linie + 1 `WorldTotal()` bloku + petle po miastach/krolestwach/Disabled | 1 | jak dzisiejsze przebiegi dobowe ksiegi | 2-5 ms |
| CSV | 1 | ok. 400 wierszy, ok. 120 KB | zapis pliku |

Razem ok. 20-40 ms na dobe przy dobie gry 11.7-24 s (< 0.3%), z czego probki swiata 3-16 ms. Zasady w kodzie: zadnego LINQ, `foreach` po
`List`/`MBReadOnlyList` tylko w przebiegach dobowych, w oknach petle `for`; stany okien jako `struct` w `__state` (bez alokacji); nazwy i tablice
statyczne; zero pelnych przegladow swiata w oknach POZA probkami 2.4 (najwyzej 16 na dobe, ok. 2.6% dzisiejszych 618 okna rodu, osobny wylacznik).
O41 dopisuje tylko odczyty O(1) do istniejacego okna rodu - jego 618 przegladow swiata na dobe zostaje na liste optymalizacji "na koniec".
Na te sama liste (bez zmian w 169): `KingdomTreasury.Daily` liczy (a) drugi raz dla wasali w tym samym bloku (wspolny wynik wymagalby przestawienia
`ClanIncomeBook.Daily` przed `KingdomTreasury` - zmiana kolejnosci bloku); `IronBank.Limit` przeszukuje wszystkie rody dla kazdego bankruta
(`IronBank.cs:114-119`, wolany przy pozyczkach `:155`, `:176`, `:322`, `:339`).
Ograniczanie: kontrolka kosztu (6.8) mierzy probke 1/256 i czas probek swiata; gdy koszt okien > 50 ms/dobe albo doba gry wolniejsza o > 2% -
wylaczyc najpierw `CirculationProbeEnabled`, potem `CirculationLedgerEnabled` (gra nic nie traci) i zglosic do optymalizacji. CSV tylko przy `LogEnabled`.

---

## 11. Testy autotestu 40 dob (zakres f)

Przebiegi (zasady CLAUDE.md i pamiec "zgoda na autotest" - probny DLL na czas testu, potem przywrocenie zatwierdzonego):
- A: nowa kampania, 40 dob (`tools/autotest.ps1` z repo autotestu, galaz at1-autotest).
- B: `-LoadSave autotest-161-kawalki -Days 3` (stary zapis bez klucza `arm_clanincome`).
- C: `-LoadSave <zapis koncowy przebiegu A> -Days 2` (ciaglosc D po zapisie i wczytaniu).

Narzedzie: nowy `tools/obieg169_sprawdz.py` w drzewie (python, bez zaleznosci; `python tools/obieg169_sprawdz.py <Armoury-*.log> [--csv <budzet-rodow.csv>]`),
wypisuje `T1 OK|FAIL ...` dla kazdego testu i kod wyjscia 0/1. Parsuje segmenty `kontrola:` (klucz=wartosc) oraz liczby po etykietach z rozdz. 6.
Testy, ktore NAPRAWDE sprawdzaja pomiar: T3, T4, T15, T16 (i T6/T7 jako kontrole wielkosci). T1 i T2 sprawdzaja tylko druk, parser i refaktor.

| Test | Co sprawdza | Warunek zaliczenia |
|---|---|---|
| T1 druk: arytmetyka linii | `Pieniadz swiata (bilans - przyczyny)`, kazda doba od 2. po starcie/wczytaniu | `Z - S + U - N - R == 0` i napis `ARYTMETYKA OK` w kazdej dobie; `Z` == `zmiana sumy` starej linii bilansu tej samej doby; `Z` == zmiana `razem` w `Pieniadz swiata:`. Tozsamosc z budowy - lapie blad druku, nie pomiaru. |
| T2 druk: refaktor `OldBalance` i komplet pozycji | ta sama linia | `R0` == `reszta` starej linii bilansu tej samej doby (co do zlotowki) i `R0 - R == (Z1+...+Z5) - (U1+..+U9) + N - K1` z WYPISANYCH liczb (kazda pozycja jest w linii i we wzorze) |
| T3 rozbicie reszty (CEL) | srednia `R` z dob 11-40 przebiegu A | `|srednia| < 10000` = cel osiagniety; 10 000-30 000 = czesciowo (raport: najwieksze `RB/RP`, kandydaci z rozdz. 4); > 30 000 = niezaliczone. Dodatkowo `|srednia R| <= 0.2 * |srednia R0|`, a srednia `R` > +10 000 (stale dodatnia) = FAIL - jakies okno liczy podwojnie albo ze zlym znakiem |
| T4 rozbicie "w tym" | `from`, `to` | `fromW <= from` i `toW <= to` (czyli "inne" >= 0) kazdego dnia; suma czesci + inne == calosc |
| T5 kasy | `Przeplywy osad (przyczyny)` | `TM0` == reszta starej linii kas miast (to samo dla zamkow i wsi); `TM1 = TM0 - nowe pozycje`; srednia `|TM1|` dob 11-40 < 30 000 (cel 4.2: < 10 000 po 164) |
| T6 kontrolki okien | `Obieg: okna (kontrolka)` + linia startowa | zero `BRAK` poza oknami z nieobecnym modem (lista w linii startowej, z powodem); wywolan > 0 w sumie 40 dob dla: notable-dochod, karawany-notabli, pasmo-notabli, awanse, werbunek, prowizja, lup-z-cial (dni z bitwami), nowe-karawany, kapital-karawan (trafien == trafien nowe-karawany, `nowe karawany bez odczytu kapitalu` == 0), warsztaty-wyrob, warsztaty-wsad, stan-bohatera, partie-znikaja, linie-modelu (== liczba rozliczen w `Pieniadz swiata (rody)`), porty, kopalnie, rynek-osady, konwoje, kupno-BK, myto; 0 wywolan przy zjawisku obecnym = okno martwe (wklejenie JIT albo zla sygnatura) -> FAIL z nazwa. Trafienia kupno-BK > 0 tylko w dobach z krolestwem w pokoju (informacja). |
| T7 wielkosci | `Obieg: notable i karawany`, linia bilansu, linia `WorkshopTrade` (`zdarzenia gry dzis: do notabli z niczego +X ... w nicosc -Y`, `WorkshopTrade.cs:1169-1170`) | zold karawan notabli 60-220 tys./dobe od 10. doby; karawan w oknach 500-1 200; `niedoplata >= 0`. Notable (wzorzec = WSZYSTKIE zdarzenia `nic -> notabl` / `notabl -> nic`, czyli `_dNotFrom/_dNotTo`): `pasmo in + wyplata dochodu + nowi notable` w 0.90-1.00 x `X` tego samego bloku dobowego i w +-20% z 578 tys. (B1, doby 31-40); `pasmo out` w 0.90-1.00 x `Y` i w +-20% z 367 tys. Przy braku linii `WorkshopTrade` (modul wylaczony) - tylko porownanie z B1. |
| T8 Obieg = zrodla | `Obieg: dzien` a linie modulow tego samego bloku | zold partii == `Zold:` naliczony partii; renta wsi + zawor miast == `Ludnosc:` renty zaplacone; zwrot == `Korona: ... zwrot` (laczna kwota); powinnosci == `Korona: ... powinnosci`; danina/clo (wplata do skarbcow)/mennica/monopole == linia `Korona: ... danina`; `U9` (zdjete z licznika) >= clo do skarbcow; Bank pozyczki/splaty == `IronBank: dzien`; stany kas/sakiewek/skarbcow == `Pieniadz swiata:` |
| T9 D | `Budzet rodow (na sucho)` + CSV | A: po k-tym przebiegu `Daily` `srednio k zmierzonych dob` (k <= 28) i `pelny u 0 rodow` do 27. przebiegu; od 28. przebiegu `pelny` == liczba rodow zywych od poczatku kampanii; B: pierwszy `Daily` po wczytaniu - `wczytano 0 rodow`, `srednio 1 zmierzonych dob`, D kazdego rodu = (wplyw doby + 27 x G/60) / 28; C: D kazdego rodu w CSV w dobie zapisu i w pierwszej dobie po wczytaniu rozni sie tylko nowa doba pierscienia (rekonstrukcja z kolumn `D`, `wplyw_doby`); 0 rodow z `D < 0`; kolumna `saldo_modelu` wypelniona u rodow z rozliczeniem |
| T10 budzet i dlugi | 6.9-6.10 | `pokoj + wojna == rody`; `ponad pulapem <= rody`; `zwolnieni > 0` tylko gdy `od 3 dob > 0`; `dluznikow` i `dlug` == `IronBank: dzien` tej doby; `korona ... u N rodow` == liczba wierszy CSV z `dlug_korona > 0`; limit na sucho rodu, ktory tej doby dostal pozyczke == `limit` z linii pozyczki Banku (`IronBank.cs:176`) - kontrola wzoru D21 |
| T11 sam log | kod i log | grep (rozdz. 12) bez trafien; w logu zero `ERROR in CirculationWindows`/`ClanIncomeBook`/`MoneyLedger.<nowa linia>`; `potkniecia` == 0 (dopuszczalne < 5 na dobe, kazde z pierwszym bledem w logu) |
| T12 zapis | po przebiegu A | `tools/zapis/napisy.py` (repo autotestu): zero napisow > 32767 B; klucz `arm_clanincome` (+ `_parts`) obecny; przebieg C wczytuje sie bez ratunku |
| T13 wydajnosc | `koszt` w kontrolce, czas doby | okna < 50 ms/dobe, przeliczenie doby < 30 ms, probki swiata < 20 ms/dobe; srednia dlugosc doby gry w A w +-2% wobec ostatniego testu T3 (tylko gdy porownywalny zapis/kampania; inaczej informacja) |
| T14 wylacznik | jedna doba z `CirculationLedgerEnabled = false` i jedna z `CirculationProbeEnabled = false` (zmiana w MCM w trakcie, albo przebieg D 3 doby) | nowe linie znikaja (przy probkach: tylko "probki swiata -"), stare linie maja ten sam ksztalt; zero bledow; po wlaczeniu nowe linie wracaja od nastepnej doby |
| T15 nasz tick (`RB`) | `RESZTA ... (w naszym ticku dobowym X ...)` i `RB=` w kontroli, kazda doba od 2. | `|srednia RB|` dob 11-40 < 2 000 = OK; 2 000-10 000 = czesciowo (raport: dzien, `RB` i przeplywy bloku z linii `Przeplywy osad` - ktory modul); > 10 000 albo jakakolwiek doba z `|RB|` > 50 000 = FAIL "dziura w naszym ticku". `RB` to niezalezny pomiar: dwa odczyty calego swiata wokol bloku minus pozycje nazwane w bloku. |
| T16 probki swiata | `probki swiata wokol okien` w 6.8, wszystkie doby A | (1) w 40 dobach co najmniej 5 probek dla kazdego okna wolanego codziennie (notable-dochod, karawany-notabli, pasmo-notabli, awanse, werbunek, prowizja, rynek-osady, konwoje, warsztaty-wyrob, warsztaty-wsad, kupno-BK, myto gdy trafienia > 0); (2) >= 95% wszystkich probek zgodnych co do zlotowki; (3) okno z rozjazdem w wiecej niz 1 probce = FAIL z nazwa i kwotami (okno liczy podwojnie, za malo, ze zlym znakiem albo przepuszcza zmiane bez nazwy) - poprawa przed 164. |
| T17 okno rodu | `rozliczenia rodow przerwane wyjatkiem` w 6.8 | 0 w calym przebiegu; > 0 -> w logu musi byc blad cudzego kodu z tej chwili (informacja dla Jeffa), a doba z przerwaniem jest wylaczona z T3/T15 |

Jeff dostaje po tescie: wynik T3 slowami gracza ("z X tys. zlota, ktore codziennie znikalo bez sladu, Y tys. ma teraz nazwe: ...") i liste
pieciu najwiekszych nazwanych ujsc - podstawa paczki 164a; do tego wynik T15 ("nasz wlasny tick gubi albo tworzy Z zl na dobe") i T16
("N okien sprawdzonych na calym swiecie, M zgodnych co do zlotowki").

---

## 12. Kolejnosc pracy wykonawcy i kontrola "sam log"

1. `Settings.cs` (3 klucze) -> `python tools/gen_mcm.py` -> build.
2. Akcesory i `partial` w `MoneyLedger` (+ `WorldNow`, `WinOpenNow`, `ClearLast169`, finalizer okna rodu D16) + puste `MoneyLedger.Obieg.cs`,
   `CirculationWindows.cs` (szkielet, `Reset/ClearDay/On`, `NamedRun`, `AddWorld`), `ClanIncomeBook.cs` (szkielet) -> build.
3. `ClanIncomeBook` w calosci (rozdz. 7-8, bez `IronBank.Limit`) + dopiski 5.1 pkt 3-5, 5.2-5.6, 5.10 (Reset, SyncData, Daily) -> build.
4. Okna F (O03-O14) i `OnGold` (z `Eff`) + `NoteCounted` -> build. 5. Okna M (O01, O02, O15-O39, O45, O46) + 5.7-5.9 -> build.
6. O40-O43 + `EnsureModelHooks` (linie modelu i kapital karawan O17) -> build. 7. Probki swiata 2.4 we wszystkich prefiksach/finalizerach -> build.
8. Linie 6.2-6.8 i CSV -> build. 9. `tools/obieg169_sprawdz.py` (T1-T17). 10. Wpis w `CHANGELOG.md` (Problem/Przyczyna/Zmiana/Ryzyko/Status DO SPRAWDZENIA) i commit.
Build zawsze: `cd ".../obieg169/repo/Armoury" && dotnet build Armoury.csproj -c Release -v q --nologo "-p:GameLibs=C:/Users/GAME/Bannerlor2-Realm-of-Thrones/libs" > build.log 2>&1; echo rc=$?`.

Kontrola statyczna (w nowych plikach trafienia dopuszczalne TYLKO w komentarzach; w zmienianych plikach - zero NOWYCH trafien w kodzie;
w `KingdomTreasury.cs` linia `:263` zostaje nietknieta, a `taken` jest w osobnej linii):
```
grep -nE "ChangeHeroGold|ChangeGold\(|GiveGoldAction|PartyTradeGold\s*[-+]?=[^=]|\.Gold\s*[-+]?=[^=]|KingdomBudgetWallet\s*[-+]?=[^=]|DebtToKingdom\s*[-+]?=[^=]|TradeTaxAccumulated\s*[-+]?=[^=]|Capital\s*[-+]?=[^=]|return false|__result\s*=|ref " Armoury/src/CirculationWindows.cs Armoury/src/ClanIncomeBook.cs Armoury/src/MoneyLedger.Obieg.cs
git diff -U0 2e235ea -- Armoury/src | grep -E "^\+" | grep -nE "ChangeHeroGold|ChangeGold\(|GiveGoldAction|PartyTradeGold\s*[-+]?=[^=]|TradeTaxAccumulated\s*[-+]?=[^=]|KingdomBudgetWallet\s*[-+]?=[^=]|return false|__result\s*="
```
Kazdy prefiks: `void`; zadnych parametrow `ref` poza `out ... __state`; wynik metody nigdy nie zmieniany; finalizery `void` (wyjatek oryginalu leci dalej).

---

## 13. Ryzyka

| # | Ryzyko | Co sprawdzic / jak ograniczone |
|---|---|---|
| R1 | Podwojne liczenie (okno w srodku rozliczenia rodu, bloku, okna taboru; flaga liczy zdarzenie nie liczone w `from/to`) | Reguly 2.2 i `counted`; testy T16 (probki swiata wokol okien), T15 (`RB`), T4 ("inne" >= 0) i T3 (`R` nie moze byc stale dodatnie). T1/T2 tego NIE sprawdzaja (tozsamosci z budowy). |
| R2 | Okno martwe: JIT wkleil metode albo inny mod podmienil sciezke (BK lambdy `ExceptionUtils.TryCatch` w RunPorts/RunMines/HandleMarketGold, `CommitGoldChanges`, `GivePrizeToWinner`) | Kontrolka wywolan T6; latki zakladane przy menu (przed kompilacja kodu kampanii). Przy 0 wywolan: okno na metodzie wolajacej (OnTownDailyTick dla portow i kopalni razem - z adnotacja w linii), dopiero potem dalsza praca. |
| R3 | Prefiks pominiety po `false` innego moda | Priority.First na kazdym prefiksie, finalizer zamiast postfiksu; prefiksy flagowe bez parametrow referencyjnych (O17 tez). O45/O46 maja parametry referencyjne - dzis nikt nie lata tych metod prefiksem `false` (sprawdzone w BK, ROT i Armoury). Zdarzenie `nic -> osada` w oknie prowizji w naszym bloku zostaje w `RB` (rzadkie). |
| R4 | Wyjatek w oknie albo w nowym kodzie wolanym z istniejacego haka | Kazde cialo i kazda metoda wolana z istniejacego haka w swoim try/catch (D17), `Stumbles` + pierwszy blad do logu; flaga przywracana w finalizerze takze po wyjatku oryginalu (tylko gdy okno sie otworzylo); okno nigdy nie gasnie. |
| R5 | Nazwy linii modelu zaleza od jezyka gry | Liczone raz na sesje z tych samych `GameTexts`/`TextObject`, co model; kontrolka `linie-modelu` i "nazwy linii: N z 11"; przy 0 trafien - pozycje `-`, bilans nietkniety (O40 to tylko informacja). |
| R6 | Bohaterowie `Disabled` i nieaktywne partie wychodza ze swiata poza oknami | O37 lapie zmiane stanu bohatera; partie nieaktywne nie maja okna - informacja `bohaterowie Disabled` i reszta `RP`; jesli T3 wyjdzie "czesciowo", to pierwszy kandydat. |
| R7 | `CommitGoldChanges` maly (inline) | T6; zloto bitew w B1 = 0, wiec brak okna nie psuje T3. |
| R8 | Wydajnosc | Rozdz. 10; kontrolka kosztu; `OnGoldTraded` dostaje tylko porownania i jedno wyszukanie w slowniku dla lorda; `IronBank.Limit` nie jest wolany (D21). |
| R9 | Zapis: nowy klucz, stare zapisy, rozmiar | `SaveText.Sync` (kawalki 8000 znakow), osobny try, `Import` bez obiektow gry; T12; starszy DLL zignoruje klucz. |
| R10 | Regula 8.2 (1) "pulap spadlby ponizej 80% wojennego" nieosiagalna wzorem 6.1 | Na sucho zastapiona "zold > pulap przy G < R"; do potwierdzenia przy 168 (bez wplywu na gre). |
| R11 | Budzet na sucho na rod zamiast na partie (6.2) | Liczby zwolnien to rzad wielkosci; dokladnie liczy 166. |
| R12 | Kolizje scalania z 170 i 171 (te same pliki od 2e235ea) | Konflikty oczekiwane w `Settings.cs`, `McmSettings.cs` (generowany - po scaleniu uruchomic `gen_mcm.py` ponownie), konstruktor `ArmouryBehavior`, `CHANGELOG.md`, `SubModuleMain`, `KingdomTreasury.cs` (jedna linia `taken`); 170 lata BEE (O13 ma prefiksy bez parametrow - dzialaja takze, gdy 170 pominie oryginal); 171 rusza zakupy zalog (`AiGear`) - bez wspolnych linii z 169. Scalanie po kolei 169 -> 170 -> 171. |
| R13 | Dosypka/ujscia wewnatrz cudzych nasluchow w oknach flagowych (zla przyczyna) | Dopasowanie strony tam, gdzie jest znana (O03-O06, O12), takze po stronie partii lorda (`Eff`); okna "dowolna strona" (statki, jency, oblezenia, turnieje, BEE, bitwy) sa rzadkie i nazwane szeroko. |
| R14 | Plik `budzet-rodow.csv` rosnie ok. 120 KB/dobe (rok ok. 44 MB) | Tylko przy `LogEnabled`; do autotestu rocznego wystarczy; w razie potrzeby co N-ta doba w 166. |
| R15 | `TotalWage` wolany drugi raz dla karawan notabli | Czysty odczyt modelu (bez licznikow, sprawdzone `MountedWage.cs`); kontrola T7 (`niedoplata >= 0`). |
| R16 | Dzisiejsza dziura w NASZYM kodzie (clo: z licznika cel znika cala kwota `x`, O43) i ewentualne inne w bloku | Teraz tylko pomiar (`U9` = cale `x`, `RB`, T15); naprawa w 164/165 - nie w 169 (zero zmian zachowania). |
| R17 | Probki swiata kosztuja (pelne przeglady swiata czesciej niz raz na dobe) | Najwyzej 8 probek (16 przegladow) na dobe, kazde okno raz na 3 doby, pomijane w bloku, w rozliczeniu rodu, w oknie taboru i przy zagniezdzeniu; osobny wylacznik `CirculationProbeEnabled`; koszt w kontrolce 6.8 i w T13; na liste optymalizacji "na koniec" (po zaliczonym T16 mozna wylaczyc). |
| R18 | Finalizer na `DailyTickClan` zmienia stare okno | Biegnie zawsze, ale dziala tylko przy wyjatku (`__exception != null`); `void` - wyjatek leci dalej jak dzis; licznik `_clanStale` rosnie tak samo (w finalizerze zamiast w nastepnym prefiksie); T17. |
| R19 | Nowe latki na wejsciu do osady (O45, O46) i na `GetInitialTradeGold` (O17) - metody wolane tysiace razy | Bramki O(1) przed jakimkolwiek odczytem; `GetInitialTradeGold` wolany tez z modelu finansow - tam tylko porownanie flagi; inny `CaravanModel` bez wlasnej metody -> deklaracja z klasy bazowej (`DeclOf`); brak -> `kapital-karawan BRAK`, `Z2` zostaje w reszcie (T6 to pokaze). |
| R20 | Limit Banku na sucho kopiuje wzor `IronBank.Limit` | Rozjazd mozliwy przy zmianie wzoru - komentarz przy obu miejscach; T10 porownuje z limitem drukowanym przez Bank przy pozyczce; tylko log. |
| R21 | D brutto zamiast salda (D19) moze nie byc tym, co Jeff mial na mysli | Saldo z O40 drukowane obok (CSV `saldo_modelu`, linia 6.9) - 166 wybierze na danych z autotestu; 169 tylko loguje. |
| R22 | `Eff(h, p)` przypisuje zdarzenie partii lorda jej wodzowi | Zgodne z gra (setter `PartyTradeGold` partii lorda pisze do kiesy wodza, `MobileParty.cs:437-439`); partie bez wodza i karawany zostaja partiami. |

---

## 14. Krytyka i odpowiedzi

Kazda uwaga sprawdzona w kodzie drzewa 2e235ea i w dekompilacjach (gra 1.4.8, BK, RC w drzewie).

| # | Uwaga (waga, miejsce) | Odpowiedz | Dlaczego |
|---|---|---|---|
| K1 | U9 = x - paid zle zdefiniowane (wazne, 5.3/O43/6.2/6.5) | PRZYJETA | Licznik cel jest posiadaczem swiata (`MoneyLedger.cs:622`), `Levies` zdejmuje z niego cale `x` (`KingdomTreasury.cs:263`), a `paid` idzie kasa -> skarbiec (`:264`), czyli przelew - swiat traci `x`. Zmienione: 2.1, O43, 5.3 (`taken += x` w osobnej linii), 6.2 (`U9` = 54 400), 6.5 (zdjeto 54 400 w nicosc, skarbce dostaly 52 000 z kas - przelew), T8. |
| K2 | Tozsamosc jest tautologia (wazne, 2.1/6.2/T1-T2) | PRZYJETA | Algebraicznie potwierdzone: `R` i `R0` liczone z tych samych skladnikow. T1/T2 przemianowane na testy druku, napis `ARYTMETYKA OK`; dodana probka swiata wokol okien (2.4, D15, osobny wylacznik, T16) i test `RB` (T15). |
| K3 | O17 czyta kase po wejsciu do osady (wazne, O17) | PRZYJETA | `EnterSettlementAction.ApplyForParty` w `CreateCaravanParty` (`CaravanPartyComponent.cs:248-252`) po `InitializePartyTrade` (`:247`); w oknie biegna `CheckRecruiting` (kasa karawany bez zdarzenia, `RecruitmentCampaignBehavior.cs:614`), BK `AddRealisticIncome` (`BKPartyBehavior.cs:941-960`, oddaje wlascicielowi `PTG - 10000`) i `AddCaravanFees` (`:784-794`). Nowe O17: flaga na `CreateCaravanParty` + postfiks na `GetInitialTradeGold` czynnego modelu, pierwszy wynik w oknie (D18). |
| K4 | Brak kupna karawan przez lordow BK (wazne, rozdz. 3-4) | PRZYJETA i rozszerzona | `BKLordPropertyBehavior.cs:78` (`ChangeHeroGold(-cena)` bez zdarzenia), `:79` `CreateCaravanParty`; `RunWeekly` biegnie od razu (`BannerKingsBehavior.cs:12-14`). Nowe okno O45; dodatkowo `BuyWorkshop` (`:117-119`) ustawia kapital warsztatu od nowa (`Workshop.cs:140`) - nowa pozycja `N7` w tym samym oknie. |
| K5 | T7 ma zly wzorzec (drobne) | PRZYJETA | 578 tys. z B1 to `_dNotFrom` (`WorkshopTrade.cs:507`) - wszystkie zdarzenia `nic -> notabl`. T7 porownuje teraz `pasmo + dochod + nowi` z 578 tys. i z linia `WorkshopTrade` tej samej doby, a samo `pasmo out` z 367 tys. |
| K6 | O37: zloto zmarlego przekazywane przed `ChangeState` (drobne) | PRZYJETA | `ChangeClanLeaderAction.cs:24` (z `KillCharacterAction.cs:65`) i `KillCharacterAction.cs:98` biegna przed `MakeDead` (`:124`) -> `ChangeState` (`:227`). Opis O37 poprawiony; kontrolka i linia 6.6 rozdzielaja notabli, lordow i innych. |
| K7 | `Last*` zerowane tylko na wejsciu metod (drobne) | PRZYJETA (z korekta uzasadnienia) | Opisany scenariusz prawie nie zachodzi: `Daily`, `Levies` i `WageRefund` maja wlasne try/catch w srodku, wiec wyjatek nie wychodzi do wspolnego try (`ArmouryBehavior.cs:1254`). Ale jedno miejsce zerowania (`ClearLast169` w `BlockOpen`, D20) jest prostsze, obejmuje wczesne `return`, `SoldierPay` i `IronBank` - przyjete. |
| K8 | Nowe wywolania w istniejacych hakach bez wlasnego try (drobne) | PRZYJETA | `OnGoldTraded` ma jeden wspolny try (`MoneyLedger.cs:256-293`), a `ArtisanInputs.OutPost` stoi poza try `OutPostfix` (`WorkshopTrade.cs:442-447`). Regula D17 + 3.1 + 5.1 pkt 3 + 5.8. |
| K9 | Koszt dobowy zanizony - `IronBank.Limit` (drobne) | PRZYJETA | `IronBank.cs:101-123`: drugi `CalculateClanIncome` i `Clan.FindFirst` w petli po bankrutach. 169 nie wola `Limit` (D21): (a) raz na rod (`IncomeToday`), krolestwa bankrutow raz na dobe, przy `LogEnabled = false` tylko pierscien D (rozdz. 7). Podwojne (a) w `KingdomTreasury.Daily` - na liste optymalizacji (wymaga przestawienia bloku). |
| K10 | Migawki bohatera nie odejmuja zdarzen po stronie partii lorda (drobne) | PRZYJETA | `GiveGoldAction.cs:16-19, 30-32` + setter `MobileParty.cs:437-439` (partia lorda pisze do kiesy wodza). `Eff(h, p)` w `OnGold` dla migawek, glow rodow (O30) i strony flag. |
| K11 | Zagniezdzenia moga skasowac stan okna zewnetrznego (drobne) | PRZYJETA | Regula "gdy `!__state.On` - nic, bez przywracania" (3.1); `SellPre`/`SnapState` - pierwsza instrukcja `__state = default`, okno wewnetrzne sie nie otwiera, wiec nie ma czego przywracac (3.6, 3.7). |
| K12 | Brak drobnych zrodel i ujsc w rozdz. 4 (drobne) | PRZYJETA CZESCIOWO | (1) Tollgates - prawda: BK nie usuwa vanilla `CaravansCampaignBehavior`, oba sluchacze dopisuja licznik (`:726-728`, BK `:1199-1201`) - nowe okno O46 (`Z5`). (2) TravelingRumors - ODRZUCONA: martwy kod, prefiks BK `VillagerSettlementEnterPatch` (`EconomyPatches.cs:1083-1118`) zwraca false dla tego samego warunku co galaz vanilla (`VillagerCampaignBehavior.cs:325`). (3) DistributedGoods - ODRZUCONA jako pozycja zlota: dopisuje `Round(-0.15) = 0` (`DefaultPerks.cs:2243`); wpis w rozdz. 4 z tym powodem. (4) RC FairRansom - prawda, rozdz. 4 (tylko gracz, kwota w logu RC, Armoury nie widzi RC). (5) Najem przez karawane przy wylaczonym `RecruitGoldToSeller` - prawda, rozdz. 4 (domyslnie przelew, `LevyGold.cs:76-82`; stan klucza w linii startowej). |
| K13 | `Ctx` bez pola czasu (drobne) | PRZYJETA | Dodane `T0` (i pola probki swiata) w `Ctx` i wszystkich stanach okien (3.1). |
| K14 | `ApplyAll` w jednym try (drobne) | PRZYJETA | Kazde okno przez `Wire(...)` we wlasnym try, `Wired[w]` i powod `BRAK` osobno (5.11, 6.1). |
| K15 | D brutto zamiast "dodatniego salda modelu" bez slowa (drobne) | PRZYJETA | Zapisane wprost (D19, rozdz. 7) z uzasadnieniem: saldo po zoldzie daje sprzezenie pulap <-> zold. Saldo naliczone (O40) drukowane obok w CSV i w 6.9 - bez drugiego pierscienia (wybor nalezy do 166). |
| K16 | D7: okno rodu zamyka postfiks, nie finalizer (drobne) | PRZYJETA (wariant finalizera) | `ClanOpen` = `_clanNow != null && _clanTime == Now` (`MoneyLedger.cs:297`); postfiks nie biegnie po wyjatku. `MbEvent` nie lapie wyjatkow, wiec scenariusz wymaga, by wyzej zlapal je inny mod - malo prawdopodobne, ale finalizer jest tani (D16, O41, T17). Wariant "169 traktuje stare `_clanNow` jako zamkniete" odrzucony: stara ksiega dalej liczylaby te zdarzenia jako "w rozliczeniu", a flagi 169 nie - "inne" wyszloby ujemne (T4). |

---

## 15. Paczka 169b - poprawki pomiaru po autotescie 40 dob (08/09.10)

Nadal SAM LOG. Wszystkie zmiany - wpis "169b" w `CHANGELOG.md` (problem, przyczyna z dekompilacji, ryzyko). Skrot dla wykonawcy kolejnych paczek:

| # | Objaw w autotescie 169 | Przyczyna | Pomiar 169b |
|---|---|---|---|
| 1 | doba 34: +1.12 mln do skarbcow bez nazwy, "trybut przyjety" 0 przy 1.41 mln "trybutu zaplaconego" | Diplomacy: odszkodowania przy pokoju (`KingdomWalletCost.ApplyCost`, portfel Reparations) - odbiorca z gory (skarbiec bez zdarzenia, glowa i najemnicy zdarzeniem z niczego), placacy dostaje dlug `TributeWallet`, ktory jego rody splacaja w rozliczeniach (linia "trybut zaplacony") | okno O47 `odszkodowania` (W40): flaga `KReparations` + migawka skarbcow wszystkich krolestw -> N8; informacje: do / ze skarbcow, dlug trybutu; sumy od startu sesji w linii "rody - przyczyny" |
| 2 | probki `nowe-karawany`: -665 / -1225 / -2130 | BEE `CaravanCampaignBehavior.OnSettlementEntered`: najem eskorty (190/95 zl), awanse zalogi - kasa karawany bez zdarzenia i bez odbiorcy | okna O48-O51 (W44-W47) `BEE-eskorta`, `BEE-awanse`, `BEE-drogi` -> U10 (ujscie), `BEE-dostawy` -> N9 (netto) - migawka `PartyTradeGold` karawany |
| 3 | "inne" zrodel z niczego ok. 285 tys. na dobe | BK `EstateData.DailyProductionIncome` (dochod majatkow), BEE `TickEstateRent` (renta majatkow), BEE `TryPayTreasurySurplus` / `AccrueCustoms` (wyplaty skarbcow BEE) - wszystko zdarzeniami "nic -> bohater" | okna flagowe O52-O54 (W41-W43) `majatki-BK`, `renta-majatkow-BEE`, `wyplaty-BEE` - rozbicie "w tym" (D14), bez nowej pozycji bilansu; do notabli osobno (T7) |
| 4 | budzet rodow 33.5 ms | ~330 x `CalculateClanIncome` (BK `AddIncomes`) | koszt rozbity w kontrolce; (a) liczone jak w 169 (D19/D21 bez zmian). Po recenzji WYCOFANE branie (a) z `KingdomTreasury.Daily`: ta liczba jest z chwili przed powinnosciami, clem, zwrotem zoldu i Bankiem (model czyta kiese glowy, skarbiec, `TradeTaxAccumulated`) - zmienialaby D. Zostaje porownanie (`NoteModelIncome`, bez dodatkowych wyliczen): ile rodow ma inna liczbe, roznica, ile ms by oszczedzila - do decyzji zlecajacego, czy przyjac "D(a) z chwili powinnosci" |
| 5 | RB ok. -2..-2.7 tys. na dobe od wojen | hipoteza: `ChangeHeroGold` naszych modulow dla bohatera poza swiatem (Disabled) - np. zwrot zoldu | `CirculationWindows.NoteHeroGold` -> N5 ("w tym nasze moduly"); RB wedlug odcinkow (granice `Mark`) - klucze `RB1..RB5` |
| 6 | T8: zold partii Obieg != Zold | dwa zbiory partii (ksiega: kazde wywolanie modelu - takze naliczenia 0 i UJEMNE, rodzaj przy naliczeniu; SoldierPay: rekord na partie tylko dla wyniku > 0, rodzaj przy rozdziale). Ujemny wynik: `AddPartyExpense` - partia z wodzem i kiesa < 500 przy rodzie < 4000 ma budzet `min(kiesa rodu + saldo, 250)`, ktory bywa ujemny; to glowna kandydatka (ksiega zawsze nizsza od Zold) | "Obieg" z SoldierPay (z liczba partii) + uzgodnienie z licznikiem ksiegi (zmiana rodzaju, powtorzenia, poza oknem, naliczenia <= 0 - po recenzji) |

Probki swiata: 12 na dobe (bylo 8). Nowe testy narzedzia: T18 (skok reszty |R| > 250 000: FAIL przy N8 = 0 tej doby, przy N8 != 0 CZESCIOWO), T19 ("inne" zrodel z niczego, cel < 50 000), T20 (RB wedlug odcinkow). T13 wypisuje dane do decyzji z punktu 4 (srednia oszczednosc ms, ile rodow ma inna liczbe, |roznica|).

Okno `nowy-notabl` (O04) zostaje na zero z powodem: `OnHeroCreated` biegnie dla kazdego nowego bohatera, 10 000 daje tylko notablom; BK (`CreateHeroAtOccupationPatch`, prefiks `HeroCreator.CreateNotable`) daje nowemu notablowi drugie 10 000 PO zdarzeniu - poza oknem. To najwyzej 10 000 na nowego notabla - nie tlumaczy 285 tys./dobe; jesli T19 > 50 000, okno na `HeroCreator.CreateNotable` (prefiks First przed BK) i dalsze szukanie.
