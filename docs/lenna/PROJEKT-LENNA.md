# PROJEKT-LENNA - komu krol daje zdobyty zamek (14.10.2026)

Odpowiedz na slowa Jeffa 14.10: "jeszcze do poprawy jest logika przyznawania podbitych zamkow - teraz w kolko dostaje jedna, max dwie postacie;
sprawdz logike, jak AI w krolestwie przyznaje zdobyty zamek / miasto; wiem, ze jak gralem Polnoca, to ciagle dostawal Tormund - mial chyba z 6 miast
i x zamkow, co bylo bez sensu!". Podstawa: `ROZPOZNANIE-LENNA.md` (przyczyna: ocena kazdego kandydata = 0 przez blad BKROTPatch, wiec glosuje sie
zawsze nad 3 pierwszymi rodami listy krolestwa). To projekt: kod nie jest zmieniony, nic nie wgrane, bez commitow.
Znaczniki: [P] pomiar, [K] kod (plik:linia), [S] szacunek / symulacja, [L] lore / historia (z pamieci, jak w rozpoznaniu).
Skroty: G = gra `dec32\cs_full`, BK = `e4tools\bkfull`, BR = BKROTPatch (`audyt-zloto\BKROTPatch`), R = ROT (`audyt-zloto\ROT`), LN = nasze drzewo `zb\ln`
(galaz w-toku/lenna od w-toku/zbior-e8 7bc5dd0b). Symulacja: `robocze\projekt-symulacja.py`, wyniki `robocze\projekt-fin30.txt`, `projekt-fin21.txt`, `projekt-warianty.txt`.

## 0. Dla Jeffa (prosto)

1. **Co bylo zle:** blad w laczniku BannerKings-ROT (BKROTPatch) daje kazdemu rodowi 0 punktow za zamek, wiec glosuje sie zawsze nad 3 pierwszymi rodami z listy krolestwa - stad u Ciebie Tormund, a w biegu Boltonowie i Freyowie (Frey 10 z 10 zdobyczy Dorzecza).
2. **Nowa zasada - punkty "za co sie nalezy":** kazdy rod zaczyna od 100; +50 ten, kto prowadzil armie, ktora zdobyla; +25 kazdy, kto byl w tym szturmie; +50 rod, ktory stracil cala ziemie; +100 prawowity pan tego zamku (tytul BK) - np. Brackenowie odzyskuja Stone Hedge.
3. **Im wiecej rod juz ma, tym mniej punktow:** miasto liczy sie jak 2 zamki; rod, ktory ma tyle, ile mu "przystoi" wedlug poziomu (tier - 1 punktow), ma tylko polowe punktow.
4. **Twardy pulap:** zaden rod nie dostanie twierdzy ponad tier + 1 punktow - tier 4 (Tormund) najwyzej np. 2 miasta i zamek, tier 6 najwyzej 3 miasta i zamek. "3 miasta i 3 zamki" dla Tormunda jest odtad niemozliwe.
5. **Krol:** nie bierze zamkow za wlasne wyprawy (nagradza tych, co szli z nim); ma mala premie tylko, dopoki korona trzyma mniej niz 1/5 krolestwa. Krolewska Przystan zostaje dla krola (tak chcial ROT, dotad nie dzialalo).
6. **Glos rady zostaje:** lordowie popieraja wedlug tych punktow, a sympatie przewazaja tylko przy wyrownanych ocenach; krol AI nie przeforsuje swojego i nie anuluje glosowania.
7. **Ty:** jako wasal widzisz 3 najlepszych wedlug punktow, a "Claim Strength" pokazuje te punkty z rozpisaniem; Twoj rod liczony dokladnie tak jak inni (takze pulap). Jako krol dalej wybierasz sam z trzech; zamek zdobyty Twoja armia dalej mozesz od razu wziac (Diplomacy).
8. **Na biegu fin30 (te same 60 zdobyczy):** nadania dostaje 48 rodow zamiast 23; najwiekszy rod krolestwa bierze 2 z 6-11 zdobyczy zamiast 10 z 10; Bolton tylko Harrenhal (sam go zdobyl), Krolewska Przystan - Stark; zaden rod nie ma wiecej niz 2 miast (dzis Bolton 5).
9. **Jeden przelacznik w MCM "Fair Fief Grants"** (wylaczony = jak dzis); kazde nadanie w logu: kandydaci, punkty, glosy, zwyciezca i ile ma ziemi.
10. **Ryzyko:** kto byl w szturmie i jak glosuje rada - log tego dotad nie mial, wiec zmierzymy w biegu; gdyby BannerKings zaczal oddawac nadane zamki (jego limit ziemi), linia logu to pokaze.

## 1. Regula N - liczby

Jedna regula dla kazdej decyzji "Owner of X" (`SettlementClaimantDecision`): po zdobyciu (droga z rozpoznania rozdz. 2) i po aneksji
(`SettlementClaimantPreliminaryDecision` -> ta sama decyzja z `ClanToExclude`, [K] G `SettlementClaimantPreliminaryDecision.cs:262-269`).
Bez zmian zostaje: kiedy decyzja powstaje (doba po zdobyciu, AI od razu, gracz po 48 h), kto jest kandydatem (BK: pelne parostwo, bez najemnikow i dworow),
3 nazwiska na liscie, wplyw za glos 20/60/100, prawo pierwszenstwa Diplomacy, Grant Fief Diplomacy, drogi fabularne ROT.

### N1 - ocena kandydata S (rod C, twierdza X)

`S = (100 + Z + U + B + J + K) x P / (P + W)`

| Czlon | Wartosc | Kiedy | Skad |
|---|---|---|---|
| W - ciezar | 2 x miasta + 1 x zamki rodu C (bez X; wsie nie, ida za twierdza) | zawsze | miasto = 2 zamki, jak waga domeny BK (miasto 2, zamek 1 [K] BK `BKStabilityModel.cs:241-244`) |
| P - przydzial | tier rodu - 1, najmniej 1 (tier 3: 2, tier 4: 3, tier 5: 4, tier 6: 5) | zawsze | rod, ktory ma P punktow, ma polowe oceny; start ROT [P] T30 dzien 1: 59 z 62 rodow tieru 3 ma W 1, 99 z 100 tieru 4 ma W 1-2, 22 z 29 tieru 6 ma W 2 - prawie kazdy zaczyna ponizej przydzialu (wyjatki: 3 rody tieru 2 z miastem, 1 rod tieru 3 z W 3) |
| Z - zdobywca | +50 | rod wodza armii, ktora zdobyla (`Town.LastCapturedBy` [K] G `ChangeOwnerOfSettlementAction.cs:111-114`), tylko przy decyzji po zdobyciu (`IsOwnerUnassigned`), **nie dla rodu krola** | przy +50 zdobywca wygrywa z "przecietnym" rodem (W 1, tier 4: 75 pkt), dopoki sam ma mniej niz P - "dostaje, dopoki nie ma tyle, ile mu przystoi" |
| U - uczestnik | +25 | kazdy inny rod z druzyna lorda po stronie atakujacej w zwycieskim szturmie (bitwa Siege / SallyOut), nie rod krola, nie razem z Z | polowa zdobywcy; nagroda dla tych, co szli z krolem |
| B - bez ziemi | +50 | rod C nie ma zadnej twierdzy (W = 0) | "wierni bez ziemi" [L]; kandydatami sa tylko rody z pelnym parostwem BK, wiec to rody, ktore stracily swoje twierdze (gentry BK nie wchodzi) |
| J - de jure | +100 | czlonek rodu C ma tytul de jure tej twierdzy (BK `TitleManager.GetTitle(X).deJure.Clan == C`) | BK nie przenosi tytulu przy zdobyciu ani nadaniu (tylko dar / barter [K] BK `BKTitleBehavior.cs:522-538`) - twierdza wraca do swojego pana |
| K - domena korony | +25 | C = rod krola i jego ciezar < 20% ciezaru twierdz krolestwa (bez X) | Wilhelm Zdobywca: ok. 1/5 ziemi przy krolu [L]; mala premia - korona dobiera ziemie, ale nie bierze wszystkiego |
| ROT | x 999 | Krolewska Przystan, C = rod krola (postfiks ROT [K] R `KingdomDecisionPatches.cs:21-34`, bez zmian; dotad martwy, bo 999 x 0 = 0) | zamysl ROT "stolica dla korony"; Czerwona Twierdza idzie za KP fabula ROT |
| remis | + 0.00-0.99 (skrot z id osady, id rodu i doby decyzji) | tylko w ocenie do listy (merit), nie w podpowiedzi | dzis remis = kolejnosc listy krolestwa (sedno bledu); skrot jest staly w ramach decyzji i nie faworyzuje nikogo |

Blogoslawienstwo Nieznajomego x 1.2 z BKROTPatch - nie przenosimy (dzis tez nic nie daje: 0 x 1.2 = 0; zalezne od wnetrza BKROTPatch).
Dwor BK ("Courtiers of X") - S = 0 i glos -100 000 jak w BK [K] BK `FiefOwnerPatches.cs:82-86`.

### N2 - twardy pulap

Rod C, dla ktorego `W + w(X) > P + 2` (w: miasto 2, zamek 1), liczy ocene z bazy 1 zamiast sumy czlonow (`S = 1 x P / (P + W)`): wchodzi na liste
tylko, gdy nikt w pulapie nie zostal (wtedy wygrywa najmniej obciazony). Pulap: tier 3 - 4 pkt, tier 4 - 5, tier 5 - 6, tier 6 - 7 (najwyzej 2 miasta
dla tieru 3-4, 3 miasta dla tieru 5-6). Pulap blokuje tylko nowe nadania - nikomu nic nie zabiera (start ROT: 0 rodow ponad pulapem [P] T30 dzien 1).
Dotyczy tez rodu krola (poza KP z ROT) i rodu gracza. W symulacji fin30 i fin21 pulap nie zadzialal ani razu (spadek oceny z N1 wystarczyl) - to siatka
na przypadki, ktorych symulacja nie widzi: uczestnicy szturmu, jeden wodz prowadzacy kazda wyprawe, tier rosnacy z renomy.

### N3 - lista do glosowania

3 najwyzsze S (gra bierze 3 - bez zmian [K] G `KingdomDecision.cs:190-198`), remis rozstrzyga skrot z N1, nie kolejnosc listy krolestwa.

### N4 - glos rady (kazdy rod AI, takze krol AI)

Poparcie rodu V dla kandydata C z listy: `D = 100 x M(C) / M_max + relacja(V, C) / 10 + 10 (to samo stronnictwo BK) + 15 (glos na siebie)`,
M = ocena z listy (merit, z ROT x999 i skrotem remisu), M_max = najwyzsza ocena kandydatow tej decyzji; relacja przycieta do +-100, wiec sympatie
i glos na siebie daja od -10 do +35. Zastepuje glos gry i BK (BK: 2 x relacja + 200 za stronnictwo + hamulec x0.15-1 tylko na glos na siebie [K] BK `FiefOwnerPatches.cs:87-106`).
Dwor BK -100 000 zostaje. Sila glosu (20/60/100 wplywu), koszt wplywu i zwyciezca = najwiecej punktow - jak w grze [K] G `KingdomDecision.cs:274-350`,
`KingdomElection.cs:343-410`. Skutek: wygrywa 1. w ocenie, chyba ze 2. ma co najmniej ok. 0.7 jego oceny i wiekszosc rady go woli. Krol AI nie przeforsuje
innego kandydata: roznica jego poparcia < 150, a gra wymaga > 300 + koszt [K] G `KingdomElection.cs:349-366`.

### N5 - krol nie anuluje glosowania

Decyzja "Owner of X" jest anulowana tylko z twardych powodow gry: krolestwo upadlo, wnioskodawca poza krolestwem, brak kandydata (`IsAllowed`),
twierdza stracona (`ShouldBeCancelledInternal`). Odpada anulowanie "rod krola na liscie stoi z boku" [K] G `KingdomDecision.cs:232-272` (petla:
decyzja co dobe od nowa, twierdza zostaje przy krolu - rozpoznanie rozdz. 8 pkt 2, niezmierzone). Przy N4 krol prawie zawsze kogos popiera, ale przy
wysokiej pewnosci wyniku i malym wplywie gra zmniejsza jego poparcie ponizej 20 i anuluje - N5 zamyka te furtke.

### N6 - gracz

- **Wasal:** lista = 3 najwyzsze S (dotad zawsze Ty, bo Twoj rod jest pierwszy w `Clan.All`); podpowiedz BK "Claim Strength" [K] BK `UIHelper.cs:626-635`
  pokazuje S z rozpisaniem (napisy po angielsku, rozdz. 4); Twoje poparcie 20/60/100 wplywu dziala jak dotad. Twoj rod liczony ta sama ocena i pulapem.
- **Krol:** po 48 h wybierasz sam z 3 nazwisk (gra); "Abstain" = wybor rady wedlug N4. Twoj rod na liscie tylko wedlug S (jak AI-krol).
- **Bez zmian:** prawo pierwszenstwa Diplomacy (twierdza zdobyta armia gracza - "claim this fief?") [U] `EnableFiefFirstRight: true`, Grant Fief Diplomacy
  (krol-gracz oddaje swoje lenna). To wybory, ktore gra daje graczowi - zostaja.

## 2. Dlaczego tak - uzasadnienie i odrzucone

Wzor laczy cztery rzeczy, ktore lore i historia robia zawsze [L] (rozpoznanie rozdz. 7): (1) zwyciezca / dowodca dostaje nagrode (Z, U), (2) wierni bez
ziemi i dawni panowie wracaja (B, J), (3) korona trzyma stolice i dobiera domene do ok. 1/5 (ROT x999, K), (4) nikt nie skupia wielu twierdz (P / (P + W), pulap).
Wszystko liczone z biezacego stanu w chwili decyzji (twierdze, tier, tytuly, kto byl w szturmie) - bez historii nadan.

| Wariant (fin30 / fin21) [S] | Rody z nadaniem / nadania | Najwiekszy udzial (krol. >= 4 zdobycze) | Korona | Zdobywca spoza korony | Bliskie oceny | Werdykt |
|---|---|---|---|---|---|---|
| gra dzis | 0.38 / 0.51 | 100% (Frey 10/10) / 100% (Renly 6/6) | - | 6 z 38 / 5 z 23 | - | blad |
| **N (wybrany)** | **0.80 / 0.90** | **50% (Targaryen 2/4), 33% przy >= 6 / 60% (Volantis 3/5), 33% przy >= 6** | **13 / 11** | **26 z 38 / 17 z 23** | 43 z 60 / 34 z 39 | wybrany |
| korona +50 | 0.75 / 0.85 | 60% (Renly 3/5) / 60% | 16 / 13 | 24 / 17 | 46 / 34 | korona za mocna |
| korona tez zdobywca | 0.77 / 0.85 | 75% (Targaryen 3/4), 50% North / 60% | 17 / 14 | 25 / 17 | 42 / 28 | krol bierze swoje wyprawy - odrzucone (lore: Aegon, Robert oddaja zdobycze) |
| zdobywca +100 | 0.80 / 0.92 | 50% / 40% | 11 / 10 | 29 / 19 | 27 / 22 | mniej bliskich ocen, ale zdobywca wygrywa az do pulapu (W < 1.67 P) - jeden marszalek prowadzacy wyprawy zbiera do pulapu; zapas na strojenie (klucz) |
| bez de jure | 0.80 / 0.90 | jak N | 13 / 11 | 27 / 18 | 49 / 35 | Stone Hedge, Pinkmaiden, Wayfarer's Rest nie wracaja do Brackenow, Piperow, Vance'ow (remisy) |
| bez pulapu | jak N | jak N | jak N | jak N | jak N | w symulacji pulap nie dziala; zostaje jako siatka (N2) |

Odrzucone tez (z kodu, bez symulacji): **naprawa samego BKROTPatch** (wolanie `base`) - przywraca wzor BK [K] BK `BKDiplomacyModel.cs:493-561`: +150 dla
obecnego wlasciciela (po szturmie to krol - korona bralaby duzo), kara wedlug limitu domeny BK (nie logowany - nie da sie sprawdzic na biegu), prawa podboju
rozne dla rodzajow wladzy; **czynnik odleglosci gry** [K] G `SettlementClaimantDecision.cs:178-211` - nagradza rody przy froncie (Bolton, Frey) za kazdym
razem; **roszczenia BK (+250/+500)** - nie wiemy, kto je ma; de jure wystarczy na "powrot do pana"; **limit domeny BK zamiast tieru** - jedno zjawisko
powinno miec jedna zasade, ale limitu BK nie mamy w logach (tytuly, zarzadzanie), a tier widac w grze i w danych; linia nadania wypisuje domene i limit BK
(rozdz. 5), a LN-11 mierzy, czy BK oddaje nadane twierdze - jesli tak, przejdziemy na limit BK w drugim kroku.

## 3. Skutek na fin30 - kto dostalby co [S]

Symulacja na tabelach fin30 (`ludzie-regiony.csv`, `budzet-rodow.csv`, `bitwy.log` - wodz zwycieskiego szturmu): te same 60 zdobyczy w tej samej
kolejnosci (bez fabuly d536), wlasciciele wedlug reguly krok po kroku (kolejne oceny widza wczesniejsze nadania reguly). Wynik pelny: `robocze\projekt-fin30.txt`.

**Polnoc** (gra: Bolton 5, Stark 1):

| Doba | Twierdza | Zdobyl (wodz) | Gra dala | Regula N daje | Dlaczego |
|---|---|---|---|---|---|
| 129 | Harrenhal (miasto) | Roose Bolton | Bolton | **Bolton** | zdobywca: (100 + 50) x 5/7 = 107 > Stark 78 |
| 530 | Krolewska Przystan (miasto) | Robb Stark | Bolton | **Stark** | ROT x999 dla rodu krola (krol nie dostaje Z) |
| 530 | Czerwona Twierdza | - | Stark | **Stark** | idzie za KP (ROT) |
| 541 | Rosby | Robb Stark | Bolton | **Cerwyn** (remis 75 z Hornwoodami i Norreyami - rada) | krol nie bierze za swoja wyprawe; w grze uczestnicy wyprawy Robba (+25) wyprzedza remis |
| 560 | Duskendale (miasto) | Robb Stark | Bolton | **Hornwood** (remis - rada) | jw. |
| 612 | Lonely Light (miasto) | Medger Cerwyn | Bolton | **Cerwyn** | zdobywca: 150 x 3/5 = 90 |

**Wedlug krolestw** (zdobycze -> rody):

| Krolestwo | Gra | Regula N |
|---|---|---|
| The Reach (11) | Ashford 7, Footly 4 | Tarly 2, Tyrell 2, Oakheart, Hightower, Caswell, Bulwer, Crane, Florent, Cuy po 1 |
| Riverlands (10) | Frey 10 | Ryger 2, Tully 2, Darry, Bracken, Vance, Smallwood, Piper, Cox po 1 (4 twierdze wracaja do panow de jure) |
| KP - Baratheon Joffrey (7) | Lannister 7 | Crakehall 2, Lydden, Lannister, Baratheon-Joffrey, Kenning, Lefford po 1 |
| The North (6) | Bolton 5, Stark 1 | Stark 2 (KP + Czerwona Twierdza), Cerwyn 2, Bolton 1, Hornwood 1 |
| Stormlands (5) | Renly 4, Tarth 1 | Renly 2, Selmy, Caron, Buckler po 1 |
| House Targaryen (4) | Second Sons 2, Targaryen, Stormcrows | Targaryen 2, Stalwart Shields, Stormcrows |

Razem: rodow z nadaniem 48 (gra 23); najwiecej miast w jednym rodzie na koniec 2 (gra 5 - Bolton); najwiekszy udzial w krolestwach z >= 6 zdobyczami
18-33% (gra 64-100%); zdobywca spoza rodu krola dostal 26 z 38 (gra 6); korona 13 z 60 (w tym KP i Czerwona Twierdza); nadan ponad pulapem 0;
remisow na czele 6, a "bliskich" (2. >= 0.7 x 1.) 43 z 60 - tu rozstrzyga rada (N4). fin21 (39 zdobyczy): rozproszenie 0.90 (gra 0.51), najwiekszy
udzial przy >= 6 zdobyczach 33% (gra 100% - Renly 6/6), zdobywca 17 z 23 (gra 5).

**Kampania Jeffa (Polnoc, Ithauser)** [P] `robocze\kampania-jeffa-polnoc-183389.csv`: Tormund (Giantsbane, tier 4) mial 3 miasta + 3 zamki = 9 pkt;
regula: przydzial 3, pulap 5 - najwyzej np. 2 miasta i zamek, a od 3 pkt jego ocena spada do polowy. Twoj rod (Karda, tier 3, 1 zamek): przydzial 2,
pulap 4; na liscie tylko, gdy ocena miedzy 3 najlepszymi (dowodzac zdobywca: 150 x 2/3 = 100, w szturmie: 125 x 2/3 = 83, przecietny rod tieru 4: 75).
Krol (Snow, 1 zamek) dostaje +25, dopoki korona ma < 1/5 Polnocy. Przebiegu tamtej kampanii nie da sie odtworzyc (brak tabel zdobyczy).

Granice symulacji [S]: tier ze startu ROT (`spclans.xml`; w grze rosnie z renoma), uczestnicy szturmu nieznani (log mial tylko wodza i liczbe partii - U
nie liczony, stad duzo remisow), glos rady przyjety jako "wygrywa 1. w ocenie", de jure = wlasciciel w dniu 1, wladca krolestwa z dnia zdobycia (Volantis
- przyjety Melane), odejscia rodow z lennem i fabula jak w biegu.

## 4. Wykonanie (projekt kodu - tor M)

Nowy plik `Armoury\src\FiefGrants.cs`, klucze w `Armoury\src\Settings.cs` + `python tools/gen_mcm.py`. Latki zakladane w kampanii (OnSessionLaunched),
nie w SubModuleMain (CLAUDE.md 7 - typy BK / BKROT ladowane pozniej). Kazde wywolanie w try/catch: blad = wynik obcego kodu zostaje, potkniecie liczone, latka dziala dalej.

| Hak | Cel | Co robi |
|---|---|---|
| H1 prefiks, Priority.First | `BKROTPatch.Models.BKROTDiplomacyModel.CalculateHeroFiefScore` (gdy typ jest) i `BannerKings.Models.Vanilla.BKDiplomacyModel.CalculateHeroFiefScore` | zwraca S z N1-N2 jako `ExplainedNumber` (z opisami, gdy `explanations`), `return false`. Jedno zrodlo dla listy (prefiks BK `FiefOwnerPatches.cs:52-60`) i dla podpowiedzi BK `UIHelper.cs:634`. Wylacznik off -> `return true` (stan 7bc5dd0b) |
| H2 postfiks, Priority.Last | `SettlementClaimantDecision.CalculateMeritOfOutcome` | po BK (prefiks) i ROT (x999): + skrot remisu (N1/N3); zapamietuje M_max tej decyzji (`ConditionalWeakTable<KingdomDecision, ...>`) |
| H3 postfiks, Priority.Last | `SettlementClaimantDecision.DetermineSupport` | `__result = D` z N4 (M = `InitialMerit` kandydata); dwor BK -100 000 bez zmian; Priority.Last - BK zaklada latki w OnGameStart, wiec przy rownej wadze szedlby po nas (jak `CrashScribe\src\PovertyPeace.cs`) |
| H4 prefiks | `KingdomDecision.ShouldBeCancelled` (tylko `SettlementClaimantDecision`) | N5: tylko twarde powody gry |
| H5 zdarzenie | `CampaignEvents.MapEventEnded` | bitwa Siege / SallyOut wygrana przez atakujacego, `MapEventSettlement` != null -> pamiec 10 dob: rody lordow strony atakujacej; bez zapisu w grze (po wczytaniu w trakcie 48 h gracza: tylko Z z `LastCapturedBy`, linia "uczestnicy: brak") |
| H6 prefiks (tylko log) | `KingdomElection.ApplyChosenOutcome` (prywatna) | linia nadania: lista 3 z ocenami i punktami glosow, kto wybral (rada / gracz-krol / rada bez gracza), stan zwyciezcy przed i po |
| H7 zdarzenie (tylko log) | `CampaignEvents.KingdomDecisionCancelled` | licznik anulowanych "Owner of X" (LN-9) |

Ocena (szkic, liczby z N1-N2):

```
S(X, C): k = C.Kingdom; dwor BK -> 0
  W = sum(C.Fiefs bez X: miasto 2, zamek 1); P = max(1, C.Tier - 1); pulap = P + FairFiefCapExtra
  krol = (k.RulingClan == C); poZdobyciu = X.Town.IsOwnerUnassigned
  s = 100
  if (poZdobyciu && !krol && X.Town.LastCapturedBy == C) s += FairFiefConquerorBonus      // 50
  else if (poZdobyciu && !krol && uczestnicy(X).Contains(C)) s += 25
  if (W == 0) s += 50
  if (tytulBK(X)?.deJure?.Clan == C) s += 100
  if (krol && ciezar(C) / ciezar(k bez X) < 0.20) s += 25
  if (W + w(X) > pulap) s = 1
  return s * P / (P + W)
```

**MCM** (Armoury, napisy EN, nowe klucze - nie ma ich w `Armoury.json` Jeffa, wiec dzialaja domyslne z kodu):

| Klucz | Domyslnie | Opis w MCM (EN) |
|---|---|---|
| `FairFiefGrants` | true | Fair Fief Grants: a conquered castle or town goes to the house that earned it - the one whose lord led the army that took it (+50), houses that fought in the assault (+25), houses that lost all their lands (+50), the rightful de jure holder (+100) - and a house's claim halves once it holds as much as its tier allows (tier - 1 points; a town counts 2, a castle 1). No house is granted a fief beyond tier + 1 points. The king's house gets no conqueror's share and only a small bonus (+25) while the crown holds less than a fifth of the realm; King's Landing stays with the crown. Council votes follow these claims, and the king can no longer cancel the vote. A human king still chooses from the three best claims. Off = as before (every claim scored 0, so the first three houses on the realm's list always stand). |
| `FairFiefConquerorBonus` | 50 | With Fair Fief Grants: claim points for the house whose lord led the army that took the fief (participants in the assault get half). |
| `FairFiefCapExtra` | 2 | With Fair Fief Grants: a house may hold at most (tier - 1 + this) points of castles and towns from grants (town 2, castle 1). |

Off (`FairFiefGrants = false`) = zachowanie 7bc5dd0b: H1-H4 przepuszczaja oryginal, H5-H7 tylko licza (linia startu "WYLACZONE").
Podpowiedz "Claim Strength" (EN, linie `ExplainedNumber`): "Base claim", "Led the army that took {FIEF}", "Fought in the assault on {FIEF}",
"Holds no castle or town", "De jure holder of {TITLE}", "Royal demesne under a fifth of the realm", "Holdings {W} of {P} points (tier {T})" (czynnik),
"Beyond the limit of {CAP} points - only if no other house can take it".

## 5. Linie logu (Armoury.log)

Start sesji (raz):

```
Nadania lenn (N): CZYNNE | ocena: prefiks na BKROTPatch.Models.BKROTDiplomacyModel.CalculateHeroFiefScore (model BK: BKROTDiplomacyModel) | lista: prefiks BK FiefOwnerPatches + postfiks ROT KP x999 (jest) + nasz remis | glos: postfiks DetermineSupport ostatni (przed nami: BannerKings) | anulowanie przez krola: wylaczone (N5) | P = tier - 1, pulap P + 2, Z 50, U 25, B 50, J 100, K 25 (< 20%)
```

Kazde nadanie (H6; jedna linia - wzor: oceny z symulacji KP d530, uczestnicy, glosy i domena BK zmyslone dla przykladu):

```
Nadanie lenna (N): dzien 109365 | King's Landing (miasto, 2 pkt) | krolestwo The North, krol Stark (domena 12%) | decyzja: po zdobyciu (doba 109365), wybral: rada (krol AI) | zdobyl Stark (Robb Stark), w szturmie: Umber, Karstark, Cerwyn | kandydatow 19, w pulapie 17 | 1. Stark 69375 [t6 1m/2z = 4 pkt, przydzial 5, pulap 7; 100 + K 25; x0.56; ROT x999; BK domena a/b] glosy 31 ; 2. Cerwyn 75 [t4 0m/1z = 1 pkt, przydzial 3, pulap 5; 100; x0.75; BK a/b] glosy 4 ; 3. Hornwood 75 [...] glosy 2 | zwyciezca Stark (1. w ocenie: tak) -> 2m/2z = 6 pkt (pulap 7) | gracz: -
```

Doba (gdy cos sie dzialo albo co 28 dob):

```
Nadania lenn (N) doba 109365: rozstrzygniete dzis 1 (od startu 37), z linia 37, anulowane 0, zwyciezca ponad pulapem 0 (przy kims w pulapie 0), potkniecia 0
```

## 6. Grupa "lenna" w `tools/sprawdz_logi.py` - progi

`python -I tools/sprawdz_logi.py --grupa lenna [--tabele <katalog tabel>] <Armoury-*.log>` (strumieniowo). Brak linii = INFO, nigdy wyjatek.

| Wiersz | Co | Prog |
|---|---|---|
| LN-0 | linia startu: CZYNNE, prefiks wpiety w typ modelu BK z linii, postfiks glosu ostatni, ROT KP jest | TAK / NIE |
| LN-1 | kazda rozstrzygnieta decyzja ma linie nadania (licznik doby = linie); z `--tabele`: zmiany rodu w obrebie krolestwa <= 10 dob po zdobyciu = linie nadania +- 2 (fabula ROT osobno) | rowne |
| LN-2 | zwyciezca ponad pulapem, gdy ktos byl w pulapie | 0 |
| LN-3 | decyzje AI: zwyciezca = 1. w ocenie albo jego ocena >= 0.7 x 1. | >= 95% (wybor gracza-krola - INFO) |
| LN-4 | udzial najwiekszego rodu w nadaniach krolestwa (bez KP i Czerwonej Twierdzy z ROT), krolestwa z >= 6 nadaniami | <= 40% (sym. fin30 18-33%, gra 64-100%); < 6 nadan - INFO |
| LN-5 | rody z nadaniem / nadania (przy >= 15 nadaniach) | >= 0.60 (sym. 0.80 / 0.90; gra 0.38 / 0.51) |
| LN-6 | zaden rod AI nie dostaje nadaniem miasta ponad pulap; rody AI z > 3 miastami na koniec (z `--tabele`) | 0 nadan; > 3 miasta tylko ze startu / fabuly / odejscia rodu (lista INFO) |
| LN-7 | nadania dla rodu krola (bez KP i Czerwonej Twierdzy) / wszystkie nadania | <= 35% (sym. 19% / 28%); < 10% - INFO |
| LN-8 | wodz spoza rodu krola, w pulapie: dostal / przypadki | >= 50% (sym. 68% / 74%; gra 16% / 22%) |
| LN-9 | anulowane "Owner of X"; decyzje AI <= 2 doby po zdobyciu | 0 anulowanych (N5); doby - INFO |
| LN-10 | nadania po szturmie z lista uczestnikow (nie "brak") | >= 80% (sprawdza H5); INFO ponizej, gdy byly wczytania |
| LN-11 | twierdza z linii nadania zmienia pana w obrebie krolestwa w 60 dob (dar / tytul BK) | 0 oczekiwane; > 0 -> DECYZJA (limit BK przeczy pulapowi - krok 2: limit BK zamiast tieru) |
| LN-12 | potkniecia latek | 0 |
| LN-13 | decyzje z graczem (wasal: glos, krol: wybor) - lista | INFO |

Bieg do testu: nowa kampania, >= 364 doby (fin30 mial 60 zdobyczy w 728 dob; projekt oblezen OB1-OB4, jesli wejdzie razem, zmniejszy je do ok. 25-28
na 2 lata - wtedy LN-4/5 dopiero przy biegu dwuletnim, wczesniej INFO).

## 7. Kontrola calosci (CLAUDE.md 8.0)

- **Regresje / ten sam stan:** decyzje o lennie ruszaja tylko gra, BK (`FiefOwnerPatches`: kandydaci, ocena przez model, postfiks glosu), BKROTPatch (model),
  ROT (KP x999, fabuly), Diplomacy (prawo pierwszenstwa, Grant Fief) - rozpoznanie rozdz. 1. Nasze drzewo: zadna latka nie dotyka tych metod (grep
  `SettlementClaimant`, `CalculateHeroFiefScore`, `KingdomElection` - tylko pokoj w `CrashScribe\src\PovertyPeace.cs`, inna decyzja). Kandydaci BK, `IsAllowed` BK,
  terminy decyzji, koszt wplywu - bez zmian.
- **Martwy kod ozywa:** ROT KP x999 (dotad 0) - zamierzone; podpowiedz BK "Claim Strength" pokaze liczby (dotad 0). Hamulec BK "ponad limitem domeny"
  w glosie znika (zastapiony N1-N2) - jedno zjawisko, jedna zasada. Inne 10 metod BKROTPatch z tym samym bledem - nie ruszamy (osobne rozpoznanie).
- **Kolizje:** H3 nadpisuje wynik BK - celowo, z Priority.Last; linia startu wypisuje wlascicieli latek, zeby obcy postfiks po nas byl widoczny.
  Pokoj z biedy (PovertyPeace) liczy glos innej decyzji - brak wspolnego stanu.
- **Wplyw:** glos N4 ma skale ok. 0-135 (BK dzis: 2 x relacja + 200) - wydatki wplywu na glosowania podobne albo mniejsze; kolumna `wplyw_doby`
  w `budzet-rodow.csv` - INFO w raporcie biegu.
- **Kolejnosc zdarzen:** decyzja jak w grze (doba po zdobyciu); H5 tylko zapisuje uczestnikow, nie zmienia bitwy ani zdobycia.
- **Stan miedzy kampaniami:** pamiec uczestnikow i M_max czyszczone przy OnSessionLaunched / koncu gry; brak danych w zapisie (stary zapis Jeffa bez zmian).
- **Wydajnosc:** S = petla po twierdzach rodu i krolestwa (do ok. 30) dla kazdego kandydata; glos uzywa zapamietanej oceny - pomijalne.
- **Wylaczniki:** zadnego "zepsuty = wylacz na sesje" - potkniecia liczone per wywolanie (CLAUDE.md 7).

## 8. Czego nie wiemy

1. Kto wygra w radzie przy bliskich ocenach (relacje, stronnictwa, wplyw) - pierwszy raz zobaczymy w linii nadania (glosy kazdego z 3).
2. Czy limit domeny BK przeczy pulapowi z tieru (BK oddaje co tydzien nadmiar; w 2 biegach 0 twierdz oddanych [P], wsi nie mierzylem) - LN-11.
3. Ile rodow w szturmie (U) i jak czesto pamiec H5 gubi uczestnikow po wczytaniu - LN-10.
4. Czy anulowania "krol stoi z boku" zdarzaly sie dotad (rozpoznanie rozdz. 8 pkt 2) - po N5 ma ich byc 0; przed - niezmierzone.
5. Tier rodow po 1-2 latach (renoma) - symulacja liczy tier ze startu ROT; linia nadania wypisze tier biezacy.
6. AIInfluence (akcja GrantFief) byl wlaczony w kampanii Ithauser - czy nadawal, nie sprawdzalem; dzis wylaczony [U] `LauncherData.xml`.

## 9. Tor, czas, pliki

- **Tor M** (1 workflow, 1 recenzja): kod ok. 300 linii (`FiefGrants.cs`) + 3 klucze + gen_mcm + grupa "lenna" w `sprawdz_logi.py`; ok. 3 h z recenzja.
  Test: nowa kampania, bieg roczny (ok. 1.5 h przy tempie fin30: 728 dob w 2 h 40 min), autotest wedlug zgody Jeffa z 07.10.
- Pliki robocze (`docs\lenna\robocze\`): `projekt-symulacja.py` (`python -I projekt-symulacja.py <tabele> <spclans.xml> <etykieta> [wariant]`),
  `projekt-fin30.txt`, `projekt-fin21.txt` (kazde zdobycie: kandydaci, oceny, zwyciezca reguly i gry), `projekt-warianty.txt` (tabela rozdz. 2).

## 10. Wykonanie w kodzie (galaz w-toku/lenna, 14.10) - co jest inaczej niz w szkicu

Kod: `Armoury/src/FiefGrants.cs`, klucze `FairFiefGrants` / `FairFiefConquerorBonus` / `FairFiefCapExtra` (`Settings.cs`, gen_mcm 1462), progi
`python -I tools/sprawdz_logi.py --grupa lenna [--tabele <katalog tabel>] <Armoury-*.log>` (LN-0..LN-13 jak rozdz. 6 + LN-6c, LN-9b INFO).
Sygnatury celow sprawdzone odbiciem na bibliotekach gry, BK i zainstalowanym `BKROTPatch.dll`. Odstepstwa od rozdz. 4 (powod w nawiasie):

1. **M_max** - z postfiksu `KingdomDecision.NarrowDownCandidates` (wszyscy kandydaci maja juz `InitialMerit`; najwyzsza ocena = najwyzsza w trojce),
   nie z H2 (H2 widzi kandydatow pojedynczo - nie wie, kiedy przebieg sie konczy). Brak zapisu (glos przed lista) - M = M_max, licznik "glos bez M_max".
2. **Pamiec szturmu (H5)** - do nadania tej twierdzy, najwyzej 60 dob, wazna tylko, gdy ostatnia zmiana pana X to `BySiege` w dobie szturmu (+-1),
   zamiast 10 dob (w krolestwie gracza decyzje "Owner of X" ida po jednej i kazda czeka 48 h - kolejka po wojnie bywa dluzsza).
3. **Z i U tylko po szturmie**: `IsOwnerUnassigned` ustawia gra takze po wymarciu / odejsciu rodu (`ChangeOwnerOfSettlementAction.cs:60`), a wtedy
   `LastCapturedBy` jest stary - "po zdobyciu" = bez pana i ostatnia znana zmiana pana w sesji to `BySiege` (po wczytaniu bez zapisu - jak w szkicu).
4. **Remis** - dodatek 0.00-0.99 tylko przy ocenie >= 10; ponizej (rody ponad pulapem S <= 1, dwor BK) x 0.01 - inaczej skrot przestawialby
   "najmniej obciazony pierwszy" z N2.
5. **Glos na siebie** - relacja z samym soba 100 (jak gra: `SettlementClaimantDecision.cs:263`) = +10, to samo stronnictwo BK, gdy glowa rodu jest
   w stronnictwie (+10), i +15 = najwyzej +35 (zakres z N4 "-10 do +35").
6. **H1** - prefiks na metodzie typu czynnego modelu i dodatkowo na `BKROTDiplomacyModel` i `BKDiplomacyModel` wedlug nazwy (gdyby model podmieniono
   po starcie sesji); linia startu wypisuje wszystkie typy i typ czynny (LN-0 sprawdza, ze czynny jest wsrod nich).
7. **LN-11 z wlasnego logu**: nadana twierdza zmienia pana w obrebie krolestwa w 60 dob - linia "Nadanie lenna (N) zmiana pana" (nasluch
   `OnSettlementOwnerChanged`), nie z tabel.
8. **Linia nadania** wypisuje tez ocene S obok M listy, "ROT x999", dzien zdobycia, "rodow" (glosujacych) i stan zwyciezcy po nadaniu z gry; linia
   powstaje takze przy `FairFiefGrants` = false ("regula N: nie" - progi LN-2..LN-10 licza tylko "tak").
