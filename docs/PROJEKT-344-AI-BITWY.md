# Paczka 344 - AI bitwy: konnica z glowa wobec konnego strzelca, jeden rozkaz szyku

Galaz `noc/344-ai-bitwy` od `5bddce2b` (WGRANIE 171 = noc/343-rot812). Worktree `scratchpad\n344`, DLL testowe `C:\Users\GAME\bannerlord-autotest\test\m344`.
Skarga Jeffa 07.11 (log `D:\Backup-Bannerlord\logi-jeffa\2026-11-07-bitwy\Armoury-2026-11-07_00-27-28.log`, bitwy 01:01 / 01:07 / 01:15), po 333 z 06.11:

- (A) "atakuje kawaleria i jada za mna; jak zblizamy sie do mojego wojska, to zawracaja, ja wtedy jade za nimi i wybijam ich w plecy strzalami; nikt z nich
  nie atakuje mnie i moge tak wybic niemal wszystkich".
- (B) "wojsko przeciwnika chodzi przod-tyl, przod-tyl, jakby uruchamiala sie jedna komenda, a potem byla zamieniana druga, i nie wiedza, co robic".

Zasady: bez cheatow (zadnych premii statystyk, gracz celem na tych samych prawach co strzelec AI, AI zna tylko biezaca pozycje i predkosc), liczby z modeli
gry / RBM / istniejacych stalych moda, latki na Agent nie w OnSubModuleLoad, mod misji tylko po tescie z bitwa - TERAZ TAKZE BEZ PRZEWIJANIA (rozdz. 6).
Tor L (mod misji, dwa moduly, blad zalezny od watku). Nic nie wgrywac, dopoki Jeff gra.

## 1. Przyczyny (skrot diagnoz)

### (A) - bitwa 1 (00:42-01:01), szyk Cavalry obroncy AI, 15 ludzi
1. Poscig prowadzil 204 (10 jezdzcow) za szybszym konnym lucznikiem: punkt przeciecia przy `vr < vs` lezy d m przed Jeffem (Hunt.cs 1527) = pogon z tylu;
   52 s bez kontaktu, Jeff strzela z 8-15 m.
2. Przy armii Jeffa 333 `BaitGate` (g < 10 m) wola `ReleaseRiders` -> `EndHunter` -> wszyscy 10 naraz wracaja pod zachowanie szyku (BehaviorProtectFlank,
   rozkaz Move, RBM nie szarzuje na samotnego gracza) - tylem do strzelca.
3. Wracajacy jada klusem: cap marszu 128 (`MarchToContact.EnemyGap` liczy od `CachedClosestEnemyFormation`, czyli od szykow armii Jeffa, galop dopiero w 120 m).
4. Pamiec przynety blokuje obrone: ponowny poscig dopiero przy g >= 100 m przez 3 s i po 60/120/240 s; tylna straz 3 ludzi rusza dopiero przy g >= 40,
   zrywa sie przy g < 20, potem 30 s przerwy. Jeff krazyl w 20-100 m - 100 s bez zadnej odpowiedzi, trafienie z 2 m bez reakcji.
5. Trafienie jezdzca szyku (PF/Move) w 157 to "sam pomiar" - zadnego odruchu.
6. Licznik "trafienia wracajacych" zanizony (Bait333Hit przez `ShieldsUp.RiderHit` tylko dla `IsCavalryFormation`; vanguardzi w innym szyku).

Wniosek: 333 SAMO tworzy (A) - zerwanie oddaje jezdzcow zachowaniu, ktore nie zna samotnego strzelca, a pamiec przynety wylacza obrone.

### (B) - prefiks 333 nie dzialal w grze Jeffa
1. W zwyklym tempie `MissionState.OnTick` wola `TickMissionAux(..., asyncAITick: true)`: AI szykow (FormationAI.TickOccasionally -> zachowania -> RBM
   PrefixTickOccasionally -> `Formation.SetMovementOrder`) idzie w WATKU SILNIKA. `Pendulum333.Pre` mial pierwsza linie `if (watek != _mainThread) return;` -
   w bitwach Jeffa 0 rozkazow (06.11 i 07.11), w autotestach (przewijanie = AI w glownym watku) setki-tysiace. Testy 333b-333f przechodzily, gra nigdy.
2. Nawet dzialajaca zapadka nie objelaby: przelaczania ustawienia Line/Loose (`SetArrangementOrder` w BehaviorProtectFlank co tick przy
   UnderRangedAttackRatio 0.2/0.1 - 40 zmian w bitwie 1, glebokosc 7.7/24.5/4.5 m przesuwa cel szyku bocznego), szykow bocznych powtarzajacych kazdy ruch
   glownego szyku (ProtectFlank liczy cel od `_mainFormation.CurrentPosition`), przelaczania zachowan glownego szyku (Regroup <-> HoldHighGround, punkt
   RBM z `positionsStorage` vs punkt gry) i lucznikow (BehaviorStop <-> ScreenedSkirmish co 2-4 s).

## 2. Pojecia wspolne (A)

| Pojecie | Definicja | Skad liczba |
|---|---|---|
| S | konny strzelec wroga: gracz albo AI samotny / luzny (szyk konnych lucznikow AI = "szyk na szyk" 204 L1 - bez zmian) | 204 |
| strefa Z, g | strefa armii strzelca i odleglosc S od niej - `ZoneBuild` / `ZoneGap` 333 BEZ ZMIAN (piechota R+50, lucznicy R+0.8 x zasieg 60..120, konnica R+60) | 333 / MissileRangeAdjusted gry |
| G | grupa: jezdzcy 204 wyslani na S + jezdzcy szyku konnicy AI w smyczy, ktorych S ostrzelal w ostatnich 20 s | 204, ZBackHitWindow 20 |
| d | odleglosc S od najblizszego jezdzca G | - |
| zasieg strzelca Rs | 60 m (ZShotMin - dalej strzala w jezdzca to loteria) | 333 |
| zasieg wypadu Rw | 40 m (ZGuardGap - jezdziec z miejsca dochodzi na cios, zanim S rozpedzi konia) | 333 |
| bliski wypad Rb | 30 m (HuntScreen - oslona skrzydla RBM pod ostrzalem) | RBM / 204 |
| eskorta E | jezdzcy strony S w 40 m od S (konni sojusznicy) | Rw |
| przewaga | G >= 2 x (1 + E) - jak L2/L3 204 (samotny nekajacy = cala wolna jazda) | 204 |
| szybszy | vS > 1.05 x mediana vR (`GetMaximumForwardUnlimitedSpeed` koni - ta sama miara co Hunt.cs 1525) | gra |
| zbliza sie / goni | d maleje >= 1 m/s przez 2 s ALBO S trafil jezdzca G w ostatnich 5 s z d <= Rs, a g rosnie albo g >= 20 | pass 1 s |
| plecy | kat miedzy kierunkiem jazdy jezdzca a wektorem jezdziec->S > 120 st. | - |

Decyzja na poziomie GRUPY (zagrozenia `Threat`), jak w 333 - nie kazdy jezdziec osobno. Pass 1 s, cel jezdzca co 0.25 s (TrackEvery) jak dotad.

## 3. (A) Stany grupy wobec konnego strzelca

Zastepuja stany 333 WOLNY / POSCIG / ZERWANY / TYLNA STRAZ.

| Stan | Co robi grupa | Wejscie | Wyjscie |
|---|---|---|---|
| **P POSCIG** | 204 jak dotad (przeciecie / odciecie), punkt przyciety 10 m przed strefa (`ZoneClamp` zostaje) | WOLNY i warunek startu (3.1) | S w g < 10 -> F; brak zblizania 8 s przy "S szybszy" -> F; S w g >= 100 i daleko od nas -> koniec 204 jak dotad |
| **F FRONT** | caly G staje w szyku na linii 10 m przed strefa (punkt = obecna pozycja jezdzca przycieta `ZoneClamp`, bez zawracania), przodem do S (`SetScriptedPositionAndDirection`, kierunek na S, ShieldsUp podnosi tarcze), cel automatyczny S | z P (krawedz strefy, pogon bez szans); z PS po ostrzale | S >= Rs od najblizszego jezdzca przez 3 s albo S gleboko w strefie (g < 0) przez 5 s bez strzalu -> PS; warunek K -> K |
| **K KONTRA** | CALY G (nie straz 3 ludzi) prosto na S: d <= Rb punkt = pozycja S (bez wyprzedzenia), d > Rb przeciecie 204; punkt przyciety do strefy jak w P | z F / PS / P: S poza oslona (g >= 20) i (zbliza sie albo trafil kogos z G z d <= Rw), przewaga (2.) | S wrocil w g < 10 -> F (nie PS!); kontakt i S ucieka szybszy 8 s bez zblizania -> F; 40 s (ZGuardMax) -> F; S padl -> koniec |
| **PS POWROT SZYKIEM** | jezdzcy oddani grze (`EndHunter`), ale szyk dostaje znacznik "powrot 344" na 30 s: MarchToContact nie daje klusa (cap 128 zdjety), G dalej obserwowany | z F, gdy S poza zasiegiem (plecy bezpieczne) | jezdzcy przy szyku (R + 10 m) -> WOLNY; warunek K albo ostrzal G -> K (S poza oslona) lub F (S przy strefie) |
| **R ODRUCH** | (szyk konnicy AI nie w G, np. PF/Move pod ostrzalem) trafiony jezdziec + 2 najblizsi zawracaja na S (oddzial L1 = PerVictim + powaleni) | trafienie z d <= Rw, S g >= 20, szyk nie w zwarciu z szykiem wroga | jak K, ale tylko ten oddzial; S z d <= Rb od trafionego - natychmiast, bez passu |

Brak stanu "ochloniecie": po F / K / PS NIE MA blokady 60/120/240 s. Pamiec "przyneta" (ile razy S zaprowadzil nas pod armie) wplywa TYLKO na start nowego
POSCIGU (3.1), nigdy na F, K, PS i R (obrona zawsze wolna).

### 3.1 Kiedy w ogole gonic (start P)
Wszystkie naraz:
- S nie w zasiegu luku swojej armii: g >= 30 m (ZStartGap 333) i przewidywany punkt przeciecia >= 10 m przed strefa (inaczej poscig konczylby sie od razu F);
- przewaga (2.);
- strona nie lamana, nie panika, szyk nie w zwarciu (204 / 283 `CavAllowed` bez zmian);
- "S szybszy" i S ucieka (nie zbliza sie): poscig tylko oddzialem L1 i tylko gdy d <= Rw (szansa na cios); szyk reszty F w miejscu (stoi przodem, nie
  rozjezdza sie za szybszym). Rowny albo wolniejszy S - 204 pelny.
- pamiec przynety tego S >= 2 zerwania: P tylko L1 (W4 333 zostaje w tej jednej roli), F/K pelne.

### 3.2 Macierz (zagrozenie x oddzial x stan)

Oddzialy: CK = ciezka/zwykla konnica wolniejsza od S; LK = lekka, rowna albo szybsza od S; KL = konni lucznicy AI; PI = piechota/lucznicy piesi.

| Zagrozenie \ oddzial | CK | LK | KL | PI |
|---|---|---|---|---|
| S daleko od swojej armii (g >= 30), ucieka | L1 tylko w Rw, reszta F | P pelny 204 | strzelaja (gra), nie gonia wrecz | gra (strzaly, tarcze ShieldsUp) |
| S daleko, nadjezdza / strzela z bliska | K calego G | K | gra | gra; 283 piechota na nekajacego pieszego bez zmian |
| S przy krawedzi swojej armii (0 <= g < 10) | F frontem, bez zawracania | F | gra | gra |
| S w swojej strefie (g < 0) | F do Rs, potem PS | F, potem PS | gra | gra |
| S wychodzi za nami poza oslone (g >= 20, zbliza sie / trafia) | K natychmiast | K | gra | - |
| S strzela w plecy wracajacym (PS) | K (g >= 20) albo F (g < 20) | jw. | - | - |
| S z eskorta (E >= 1) | K/P tylko przy G >= 2 x (1+E), inaczej F i powrot PS | jw. | - | - |
| szyk konnicy w zwarciu z szykiem wroga | bez zmian (zwarcie wazniejsze), R tylko trafiony + 2 przy d <= Rb | jw. | - | - |
| strona przegrywa (`Broke`) | P zakazany; F, K i R dozwolone (obrona, nie atak) | jw. | - | - |
| szyk w odwrocie / panika | nic (ucieczka gry) | nic | nic | nic |
| wodz w G (335 HeroCaution) | wodz NIE jedzie w K/P (zostaje przy straze 335), reszta G jak wyzej | jw. | - | - |
| S = wodz wroga | jak S, ale 204 L3 (R6) zostaje | jw. | - | - |

Gracz i AI-strzelec - te same reguly (zadna galaz `IsMainAgent` w decyzji; `IsMainAgent` tylko w liniach logu).

### 3.3 Przypadki Jeffa jako testy macierzy
- "jada za mna" - P (Jeff g >= 30, przewaga 10:1). Jeff szybszy (horse archer vs banner knight) -> po 8 s bez zblizania F, nie 52 s pogoni; w Rw oddzial L1.
- "jak zblizamy sie do mojego wojska, to zawracaja" - F: staja frontem 10 m przed strefa, NIE zawracaja.
- "ja jade za nimi i wybijam w plecy" - plecow nie ma (F stoi przodem); jesli PS juz jechal, Jeff g >= 20 i trafia -> K calego G w <= 1 s.
- "nikt z nich nie atakuje mnie" - K / R bez ochloniecia; trafienie z 2 m -> R natychmiast.
- "moge wybic niemal wszystkich" - po K Jeff albo wraca pod armie (F), albo traci konia / ginie; cykl F -> PS -> K -> F nie ma fazy plecami do S w Rs.

## 4. (B) Jeden rozkaz szyku - Arbiter 344

### 4.1 Gdzie i w jakim watku
- Prefiksy `Formation.SetMovementOrder` (jak 333) i NOWY `Formation.SetArrangementOrder`, Priority.Last, after com.rbmai. Wpiecie jak dotad w
  `Doctrine.ApplyMissionPatches` (pierwsza bitwa), nie w OnSubModuleLoad.
- USUNAC sprawdzenie watku. Prefiks dziala w glownym watku i w watku AI silnika (`TickAgentsAndTeams`). Stan arbitra (slownik `Forms`, PF, liczniki) pod
  jednym `lock` (krotkie sekcje; tick AI i pomiar glownego watku moga biec rownolegle do `WaitTickCompletion`).
- W prefiksie tylko odczyty bezpieczne w watku AI (te same, ktore czyta gra w tym ticku): `CachedMedianPosition`, `CachedAveragePosition`, `CurrentPosition`,
  `Depth`, `QuerySystem`, `Team.FormationsIncludingSpecialAndEmpty`, `CreateNewOrderWorldPositionMT`. Zadnych zapytan o ludzi, zadnych wywolan HorsePursuit:
  zbior szykow gracza (`PlayerForm`) i kontakt konnicy przepisywane w glownym watku co pass do niezmiennej tablicy (podmiana referencji), czytane w AI.
- Licznik wywolan wedlug watku (glowny / AI) w linii - miara, ktora by zlapala blad 333.

### 4.2 Fazy i pierwszenstwo
| Faza | Wejscie | Kto rzadzi |
|---|---|---|
| ROZSTAWIENIE | przed `IsDeploymentFinished` | gra |
| PRZED KONTAKTEM | najblizszy szyk wroga > 75 m (miara RBM), wyjscie z kontaktu > 90 m, bez szarzy konnicy w 60/120 m (333 W1 zostaje) | ARBITER (4.3) |
| KONTAKT | j.w. odwrotnie, 4 s od ostatniego pomiaru konnicy | gra/RBM (przepuszczone); tylko histereza ustawienia 4.4 slabsza (5 s) |
| ODWROT | Retreat, panika, `FallBack`, Stop gracza | gra |

Pierwszenstwo w kazdej fazie: (1) szyki druzyny gracza i oddane F6 - nigdy nie dotykane; (2) rozkazy nie-Move (Charge, Retreat, FallBack, Stop) - przepuszczone,
po nich P zapomniany; (3) wyjatek kontaktu konnicy - przepuszczone; (4) zachowania AI przed kontaktem - przez arbitra. Moduly agentowe (204, 283, 335) nie
ida przez rozkaz szyku i nie sa arbitrowane.

### 4.3 Reguly rozkazu Move przed kontaktem
Zostaja z 333f (dzialaly w testach, w grze nigdy nie wlaczone): punkt przyjety P, martwa strefa max(12 m, glebokosc), zapadka kierunku od pozycji szyku
(okno 30 s podwajane do 240 s), trzymanie 10 s, przesuniecie P przy minieciu, obciecie do ruchu w bok. Dochodza:

1. **Szyk prowadzacy i szyki boczne.** Szyk z `BehaviorProtectFlank` / `BehaviorScreenedSkirmish` (cel liczony od innego szyku - `_mainFormation`,
   glowna piechota) jest NASTEPCA. Nastepca dostaje nowy P tylko, gdy P prowadzacego zmienil sie od ostatniego przyjecia nastepcy (o >= martwa strefa
   prowadzacego) albo gdy prowadzacy w kontakcie; inaczej Keep. Skok mediany prowadzacego i jego drganie nie poruszaja bokow - ruszaja je tylko przyjete
   decyzje prowadzacego. Rozpoznanie prowadzacego: pole `_mainFormation` zachowania (refleksja raz na typ, odczyt w AI), bez niego - najwiekszy pieszy szyk
   druzyny.
2. **Histereza zachowan (zmiana taktyki).** Szyk ma ZACHOWANIE PRZYJETE B. Nowe aktywne zachowanie B' jest "zmiana planu" dopiero, gdy trzyma sie
   nieprzerwanie 6 s (12 tickow 0.5 s) ALBO zmienila sie taktyka druzyny (typ `TeamAI` biezacej taktyki) ALBO B' = Charge / Retreat / Advance po kontakcie.
   Zmiana planu: zapadka i okno szyku wyzerowane, pierwszy rozkaz nowego planu przyjety (regula 1 z 333). Do tego czasu rozkazy B' ida przez zwykle reguly
   (martwa strefa, zapadka) wzgledem starego P - przelaczenie Regroup <-> HoldHighGround w kilka sekund nie cofa szyku. Powrot A -> B -> A w 6 s = brak
   zmiany planu (licznik "migniecia zachowan").
3. **Blokada sprzecznych rozkazow w oknie.** W 10 s od przyjecia P rozkaz przeciwny na osi do wroga (>= 3 m) zawsze obciety do linii szyku, takze po
   zmianie planu, chyba ze przyszedl z wyjatku kontaktu (zagrozenie realne) albo z nie-Move. Jedna zmiana kierunku na 10 s na szyk z konstrukcji.

### 4.4 Ustawienie (Line / Loose / ShieldWall ...)
- Prefiks `SetArrangementOrder` przed kontaktem: ustawienie przyjete A i czas. Zmiana na inne najwczesniej 20 s po poprzedniej (histereza czasowa); powrot
  do poprzedniego (Loose -> Line) dopiero po 20 s BEZ ostrzalu szyku (UnderRangedAttackRatio < 0.1 - prog wyjscia gry - przez caly czas). Loose pod
  ostrzalem jest rozsadne i zostaje - nie zostaje drganie Line/Loose/Line w 0.6 s.
- Wyjatki: szarza konnicy wroga (kontakt konnicy) - przepuszczone natychmiast (ShieldWall / Square gry); szyk gracza - nigdy.
- Kontakt: histereza 5 s (bez zmian w kolejnosci gry, tylko bez migotania co tick).
- Zablokowane = wywolanie prefiksu zwraca false (zostaje biezace ustawienie) - glebokosc stabilna, cel nastepcy nie przesuwa sie po osi.

## 5. Co z 333 usunac, co zastapic, co zostaje

### HorsePursuit.Bait333.cs
| Element | Decyzja |
|---|---|
| `ZoneBuild`, `ZoneGap`, `ZoneClamp`, `ZStartGap`, `ZBreakGap`, `ZClampGap`, `ZFootAdd/ZCavAdd/ZShot*`, `CavFarFromZone` | ZOSTAJE (strefa jest dobra) |
| `BaitGate` galaz 1 `ReleaseRiders(t, now, false)` + `return BNone` | ZASTAPIC: przejscie do F (jezdzcy zostaja w 204 z rola Front) |
| stan ZERWANY: `ZReChaseGap`, `ZReChaseHold`, `FarSince`, `ZCalm`, `ZCalmMax`, `Bait.Calm`, `BNone` w ZERWANYM | USUNAC (brak ochloniecia; zastepuja F/PS/K) |
| `GuardState`, `GuardTake`, `GuardSize`, `ZGuardGap/BreakGap/Max/Cool/Share/Min/MaxN`, `BGuard`, `Hunter.Guard` | ZASTAPIC stanem K (cala grupa); `ZGuardGap`=Rw i `ZGuardMax`=limit K zostaja jako liczby |
| `ZBreakCool` 5 s, `CounterCool` po strazy | USUNAC dla F/K/PS (CounterCool 30 s zostaje dla zwyklego konca 204 bez kontaktu) |
| `ZLightAfter`, `BLight`, `Bait.Breaks`, `Loops` | ZOSTAJE tylko jako ograniczenie STARTU P (3.1); petla przynety - miara |
| `Bait333Hit` | ZASTAPIC: nowe wejscie trafienia jezdzca (nizej) |
| linie "zerwanie / straz / ponowny poscig" | ZASTAPIC liniami "AI bitwy (344) front / kontra / powrot / odruch" (najwyzej 16 na bitwe) |

### HorsePursuit.Hunt.cs / React.cs
- `SetScriptedPosition` dla roli Front: `SetScriptedPositionAndDirection` (kierunek na S), punkt = pozycja jezdzca w chwili wejscia w F przycieta
  `ZoneClamp` (bez zawracania, najwyzej krok w tyl do linii 10 m przed strefa po wlasnym przodzie).
- Rola Kontra: d <= Rb prosto na pozycje S, dalej przeciecie (Hunt.cs 1514-1552 bez zmian).
- "S szybszy" - te same `GetMaximumForwardUnlimitedSpeed` (Hunt.cs 1525), licznik zblizania na zagrozenie.
- `losing` (Hunt.cs 905-910) i `CavAllowed` (React.cs 406) - nie dotycza F/K/R (obrona); dotycza P.

### ShieldsUp.Riders.cs / HorsePursuit.Counter.cs
- `RiderHit`: obok sciezki 157 (pomiar dla `IsCavalryFormation`) nowe wejscie `HorsePursuit.OnRiderShot344` dla KAZDEGO konnego czlowieka AI (warunek
  `MountAgent != null`, nie typ szyku) - liczy trafienia w plecy (kat 2.) i uruchamia R/K. Glowny watek (Hit), bez zmian w watkach.

### MarchToContact.cs
- `EnemyGap` dla konnicy: min(dotychczasowy dystans do szyku, odleglosc mediany szyku do najblizszego S z aktywnych zagrozen 204 tej strony - lista
  pozycji z passu 204, bez zapytan o ludzi). S w 120 m (`_gallop`) = galop.
- Szyk ze znacznikiem "powrot 344" (PS) - cap zdjety 30 s.

### Pendulum333.cs -> arbiter 344
| Element | Decyzja |
|---|---|
| `if (Thread.CurrentThread.ManagedThreadId != _mainThread) return;` | USUNAC; `lock` na stan; licznik wywolan wedlug watku |
| `Refresh` | ZOSTAJE (odczyty bezpieczne w AI), pod lockiem |
| reguly 0-5 (P, martwa strefa, zapadka, trzymanie, Over, Side) | ZOSTAJA |
| `HorsePursuit.PlayerForm` w `Eligible` | ZASTAPIC migawka z glownego watku |
| nowe | nastepca/prowadzacy (4.3.1), histereza zachowan (4.3.2), blokada 10 s (4.3.3), prefiks `SetArrangementOrder` (4.4) |
| pomiar wahadla (Sample, Zig, linie nawrot) | ZOSTAJE + srodek ciezkosci jako miara glowna |

Klucze: nowe `CavalryFaceShooterAtArmyEdge` (A) i `FormationOrderArbiter` (B), domyslnie TAK; stare `RidersBreakOffNearShootersArmy` /
`FormationsSteadyBeforeContact` wycofane (mapowane na nowe przy wczytaniu ustawien). Klucz NIE = 204 bez 333 i gra bez arbitra (do porownan).

## 6. Miary (linia "AI bitwy (344): misja ..." raz na bitwe) i grupa `--grupa ai344` w sprawdz_logi

| Id | Miara | Prog |
|---|---|---|
| A344-0 | wywolania arbitra: glowny / AI watek; bieg bez przewijania | AI > 0 (bez tego NIE - blad 333) |
| A344-1 | zawrocenie tylem pod ostrzalem: trafienia S w plecy jezdzcow G przy d <= Rs i g >= 10, na 1 wejscie w F | mediana <= 1, razem w tescie <= 2 na wejscie |
| A344-2 | kontratak na strzelca poza oslona: zdarzenia "S g >= 20 i (zbliza sie lub trafia) przy d <= Rw" / K uruchomione, czas reakcji | 100%, mediana <= 1.5 s |
| A344-3 | wynik K: kontakt (d <= RaiseNear 6 m) albo S wypchniety w g < 10 albo S padl | >= 80% K |
| A344-4 | trafienia bez odpowiedzi: S trafil jezdzca AI z d <= Rw, g >= 20, a w 2 s ani K ani R | 0 |
| A344-5 | front: w F udzial jezdzcow z katem do S < 60 st. (probka co 1 s) | >= 80% |
| A344-6 | klus przy strzelcu: szyk konnicy z capem 128, gdy S w 60 m | 0 s |
| A344-7 | pogon bez szans: P za szybszym S dluzej niz 8 s bez zblizania | 0 |
| B344-1 | nawroty srodka ciezkosci na szyko-minute przed kontaktem, w kazdej bitwie osobno | <= 0.25, w tyl <= 0.1 (przy >= 2 szyko-min) |
| B344-2 | zmiany rozkazu na minute z podzialem na zrodla (typ zachowania) : przyjete / trzymane / obciete | INFO + trzymane > 0 |
| B344-3 | zmiany ustawienia na szyko-minute przed kontaktem; powrot Line/Loose w 20 s | <= 1 / 20 s z konstrukcji; powroty w 20 s = 0 |
| B344-4 | migniecia zachowan (A -> B -> A w 6 s) / zmiany planu | INFO |
| B344-5 | nastepcy: ruchy bokow bez zmiany P prowadzacego | 0 |
| X344 | potkniecia 344 = 0, `ERROR in` miejsc 344 = 0, latki 344 TAK | twarde |

Pomiar Jeffa z bitwy 1 jako odniesienie: 187 nawrotow mediany / 78 srodka (1.4/min), 40 zmian ustawienia, 5 trafien w plecy w 20 s po zerwaniu, 100 s bez
odpowiedzi, 0 rozkazow przez prefiks.

## 7. Testy (po zamknieciu gry przez Jeffa - nie wczesniej)

Kazdy scenariusz DWA razy: z przewijaniem (szybko, liczby) i BEZ przewijania (watek AI jak u Jeffa; w CrashScribe opcja wylaczenia
`SetFastForwardingFromUI` - dodac, jesli brak; limit `-BattleSec` w sekundach rzeczywistych podniesc). Bieg bez przewijania jest WARUNKIEM wgrania.

1. **Harasser** (przerobiony `HorsePursuit.BaitTest333.cs`, fazy podjazd / strzal / ucieczka / pogon): konny lucznik testu podjezdza do szyku konnicy
   AI na 15 m, oddaje 3 strzaly, ucieka pod stojaca armie strony testu (`TestArmyHold` 150 s), czeka az G stanie (F), potem wyjezdza za nimi 30-60 m poza
   strefe i strzela w plecy / bok. Oczekiwane: P (L1 przy szybszym), F frontem przy krawedzi, K calego G w <= 1.5 s po wyjsciu, A344-1..5 spelnione,
   zero "trafien bez odpowiedzi". Wariant: nekajacy z 2 konnymi eskortami (przewaga 2 x 3 - K tylko przy G >= 6).
2. **Harasser na lekka konnice** (szyk szybszy od strzelca): P pelny, kontakt, bez F na 8 s.
3. **Obronca stoi** (jak bitwa 1 Jeffa): armia testu Stop 150 s + konny lucznik ostrzeliwuje piechote obroncy (Loose/Line pod ostrzalem), obronca
   HoldHighGround + ProtectFlank + ScreenedSkirmish. Oczekiwane B344-1..5, A344-0 > 0 w watku AI.
4. **Regresja**: zwykle bitwy 333/204/283/335 (bieg-bitwa.ps1 -Battles) - liczby 204 kontaktow i 335 bez pogorszenia, sprawdz_logi grupy ai333 (A333-0..6
   po przemianowaniu), ai204, ai335.
5. Odczyt: `python -I tools/sprawdz_logi.py --grupa ai344 <Armoury-*.log>`; SPRZATAC zapisy autotestu po serii; logi Jeffa skopiowac przed 1. biegiem.

Koszt: arbiter jak 333 (raz na rozkaz, kilkanascie szykow) + prefiks ustawienia (rzadki); stany A na zagrozenie co 1 s, kat plecow tylko przy trafieniu.
