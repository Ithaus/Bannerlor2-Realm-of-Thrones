# PACZKA 174b - DOWOZ (specyfikacja dla wykonawcy, 09.10.2026)

Status: PROJEKT PO KRYTYCE (26 uwag - rozdz. 11) I WYKONANY 09.10: kod w galezi `w-toku/174b-dowoz` (commity lokalne, rozdz. 11 "Wykonanie"),
build Release kod 0, NIE wgrane, gra i autotest nie uruchamiane, bez pushu. Sekcje zmienione po krytyce maja dopisek "Po krytyce".
Baza kodu: galaz `w-toku/174b-dowoz` (od `noc/sklad6` 0c41eee = Armoury w grze 17a700d7 + 171 + 172 + 172b + 174 + decyzje Jeffa 09.10:
gorsza zbroja, zlom, zaopatrzenie BK gracza), drzewo `SCR\noc2\n174b`.
SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\7016f733-d379-418e-b700-f66fd52e4d2b\scratchpad`,
SCR3 = `...\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad`.
Sciezki `[K]` bez katalogu - wzgledem `SCR\noc2\n174b\Armoury\src`. Gra 1.4.8 - `SCR3\ore-supply\cs`, BK - `SCR3\ore-supply\bk`,
BKROTPatch - `SCR3\ore-supply\bkrot`, NavalDLC i Harmony 2.4.2 - `SCR\a174b\dec`.

Oznaczenia: **[K]** przeczytane w kodzie, **[P]** pomiar z logu, **[H]** historia, **[S]** szacunek albo rachunek (zalozenia podane).
Rok gry = 364 dni. Ladunek rudy/drewna = 100 kg. "Miasto" = jedno z 97 miast ROT (bez zamkow).

Podstawa (przeczytane w calosci): wynik testu `SCR\kopia-sklad6\ANALIZA.md` (40 dob nowej kampanii + 8 dob zapisu 362, porownanie
z `SCR\kopia172b`), cztery diagnozy: A `SCR\a174b\kontrakty.md` (kontrakty), B `SCR\a174b\geografia.md` (ruda i strzaly),
C `SCR\a174b\rynek.md` (rynek zbroi i zakupy AI), D `SCR\a174b\tempo.md` (tempo doby); `docs/PROJEKT-174-PRODUKCJA-UZBROJENIA-2026-10-09.md`
(rozdz. 3.3 - kontrakty 174.2), `docs/PLAN-K13-2026-10-07.md` (133 - lupa). Kluczowe miejsca kodu sprawdzone ponownie przy pisaniu (rozdz. 10).

Zasady wiazace (Jeff):
- zamknieta ekonomia - "nic z niczego": ruda jedzie prawdziwa karawana albo statkiem, ktore mozna obrabowac; "brak = sprawdz produkcje
  i rzemieslnikow, nic z niczego" (09.10);
- jedna regula dla gracza i AI (09.10);
- zachowania z biezacego zysku, bez sztucznych list i priorytetow (06.10);
- optymalizacja gry dopiero na koniec (08.10) - ALE paczka nie moze sama spowalniac gry: regres wobec wersji w grze to blad paczki do naprawy;
- liczby dobiera projekt z uzasadnieniem; pytania tylko o zmiany rozgrywki (praca nie czeka - domyslnie rekomendacja).

---

## 0. Dla Jeffa - prostym jezykiem

1. Test pokazal, ze kowale robia juz ponad dwa razy wiecej uzbrojenia, ale ruda nadal nie dociera do ok. 36 miast - i w tych samych
   miastach brakuje strzal (31 miast zamiast najwyzej 24).
2. Glowny winowajca to nasz wlasny nocny oboz: karawana z ruda, ktora stanela na noc, byla liczona jak karawana, ktorej ktos zmienil
   trase, i po trzeciej nocy w drodze tracila zlecenie - teraz sen to tylko sen, a zlecenie trwa do celu.
3. Do wysp i dalekich wybrzezy ruda poplynie prawdziwym statkiem kupieckim (piraci moga go zlupic), kupiec wezmie mniejszy ladunek, gdy
   duzy sie nie oplaca, i moze zawiezc rude, ktora juz wiezie w jukach - nic nie pojawia sie z niczego.
4. Gdy w miescie brakuje strzal, ostatni ladunek rudy dostaje ten, kto na nim wiecej zarobi - platnerz albo strzelarz; dzis zawsze bral
   platnerz, bo pracowal pierwszy.
5. Zbroi na tulow malo lezy na straganach, bo zolnierze lordow ja nosza (cos na tulowiu ma ok. 80% z nich - tyle, ile w sredniowieczu);
   zeby i Ty mogl cos kupic, kto kupuje hurtem dla oddzialu (lord, zaloga, notabl, kupcy, takze Twoi ludzie z wlasnej sakiewki), nie zabiera
   ostatniej sztuki zbroi danego rodzaju - te bierze ten, kto kupuje osobiscie przy straganie. Osobiscie kupujesz w praktyce tylko Ty, wiec to
   WYJATEK od "jednej reguly" - pytanie 1 (wlaczone domyslnie, wylacznik w MCM). Twoje karawany moga teraz brac zlecenia na rude jak karawany
   lordow (pytanie 2, domyslnie tak).
6. Gra zwolnila przez nasze nowe liczenie cen (ok. 1 s na dzien w nowej grze, ok. 6 s na Twoim zapisie) - poprawiam to tak, zeby wynik
   byl ten sam, a liczenia mniej.
7. Skup starego nadmiaru na zlom bedzie pamietal swoje 30 dni rozgrzewki w zapisie gry - w Twojej kampanii ruszy po 30 dniach gry raz,
   a nie dopiero po 30 dniach bez wczytywania.
8. Przed wgraniem: test 40 dni nowej gry i 8 dni Twojego zapisu; progi sa w rozdziale 6, dwa pytania w rozdziale 8.

---

## 1. Skad ta paczka - wynik testu sklad6 [P]

| Miara | 172b | sklad6 | Cel 174b (prog) | Krok |
|---|---|---|---|---|
| Bledy Armoury / CrashScribe | 0 / 8 | 0 / 8 | 0 / 6-8 startowych | - |
| Produkcja z amunicja (sr. d31-40) | ok. 1 036/d | ok. 2 244/d (korpus 250) | nie spada > 10% (pomocniczo) | - |
| Miast bez rudy d40 (sr. d31-40); zapis 362 | 36 (38.8) | 36 (37.3); zapis 32-43 | sr. d31-40 <= 25; zapis - trend (rozdz. 6) | 174b.1, .2 |
| Brak rudy w warsztatach (cykle/d) | 796 | 787 | <= 500 (pomocniczo, cel 174) | 174b.1, .2 |
| Miast bez strzal d40 (sr. d31-40) | 24 (23.1) | 31 (31.9) | sr. d31-40 <= 24 | 174b.2, .3 |
| Kontrakty: zawarte / dojechalo | - | 308 / 164 (55% zakonczonych) | dojechalo >= 80% zakonczonych | 174b.1, .2 |
| Kontrakty: "cel zmieniany przez innych" / "rozkaz odrzucony" | - | 113 / 16 | <= 5% / 0 | 174b.1, .2 |
| Bez kontraktu, najczestszy powod | - | "bez drogi" 98/d (d31-40) | - | 174b.2 |
| Zbroja korpusu na polkach d10/20/30/40 | 8 860 / 6 115 / 1 983 / 1 067 | 2 862 / 872 / 331 / 1 998 | zastapione miara gracza - nowa sztuka w 7 dobach (P5) | 174b.0, .4 |
| "Cokolwiek na tulowiu" razem / partie / zalogi (d40) | brak miary | 80.5 / 73 / 93% (sr. d30/35/40: 79.8 / 71.7 / -) | sr. d30/35/40: razem >= 78.3%, partie >= 70.2% | - |
| ZakupyAI sr. d31-40 | 1 174 szt. / 76.8 tys. zl | 2 650 szt. / 194.6 tys. zl | bez petli, miara M2 | 174b.0, .4 |
| Tempo nowej kampanii sr. / d31-39 (s/dobe) | Z1b 13.03 / 15.22 | 14.21 / 17.11 | informacyjnie, mediany (P7); twardy prog: koszt "nasze 171-174b" <= 1% doby (P13) | 174b.5 |
| Tempo zapisu 362 (s/dobe, 7 pelnych dob) | Z1b 22.1 (mediana 22), Z1 24.7 (23) | 28.0 (27) | parami z Z1b w tej samej sesji: mediana <= Z1b + 1 s (P8) | 174b.5 |
| Zlom: rozgrzewka 30 dob | - | w sesji (zapis 362: 8/30) | w zapisie gry | 174b.6 |

Werdykt testu sklad6: NIE WGRAC (ANALIZA.md rozdz. 4). 174b to poprawka sklad6, nie nowa funkcja: wszystko, co sklad6 dobrze robi
(produkcja, koniec znikania, ksiega bez "z niczego"), zostaje bez zmian.

---

## 2. Zasady paczki

- **Nic z niczego.** Kazdy ladunek ma sprzedawce i kupca: kontrakt kupuje surowiec w zrodle przez `SellItemsAction` (jak dzis) albo wiezie
  towar, ktory karawana JUZ ma w jukach (nowe - bez zakupu); dostawa sprzedaje go miastu przez `SellItemsAction` (jak dzis,
  `MaterialOrders.cs:417-453`). Statek to prawdziwa karawana BK ze statkami (konwoj) - mozna go zlupic. Zadnej sztuki ani zlota z niczego.
- **Jedna regula.** Karawana z kontraktem spi jak kazda karawana; karawany rodu gracza moga brac kontrakty jak karawany AI (pytanie 2). Rezerwa kramu dzieli
  kupujacych wedlug rodzaju zakupu (hurt dla oddzialu / zakup osobisty) - ludzie gracza (sakiewka) to hurt jak ludzie lorda; ale zakup osobisty przy
  straganie ma w grze tylko gracz, wiec po krytyce (uwaga 21) to jawny WYJATEK od jednej reguly - pytanie 1 z wariantami.
- **Z biezacego zysku.** Morze, wielkosc ladunku, ruda z jukow i ruda dla strzelarzy - wszystko wybiera rachunek zysku po cenach targu
  w tej chwili; zadnych stalych list miast ani priorytetow.
- **Paczka nie spowalnia gry.** Kazdy krok ma podany koszt; 174b.5 zdejmuje regres 171-174 bez zmiany wyniku; linia "Koszt 171-174"
  mierzy to w tescie. Bez `Thread.Suspend`.
- **Kazdy krok = osobny commit** (po polsku, bez polskich znakow, ostatnia linia `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`),
  wpis w `CHANGELOG.md` ze statusem NIEWGRANE, build `dotnet build Armoury/Armoury.csproj -c Release -p:GameLibs=<SCR3>\lancuch\libs` z kodem 0;
  po zmianie `Settings.cs` - `python tools/gen_mcm.py`. Nowe klucze MCM: Jeff ich nie ma w `Armoury.json` - dzialaja wartosci domyslne.
- Kazdy nowy napis w `SyncData` - przez `SaveText.Sync`. Komentarze po polsku bez polskich znakow, teksty w grze po angielsku.
- Wykonawca NIE uruchamia gry ani autotestu, NIE wgrywa, NIE pushuje.

**Kolejnosc commitow** (numer = punkt zadania; kolejnosc = od najmniejszego ryzyka i tak, zeby pomiar stal przed zmianami zachowania):

| Kolejnosc | Krok | Co | Zmienia rozgrywke? |
|---|---|---|---|
| 1 | 174b.0 | pomiary i nazwy miast w logu | nie (sam log) |
| 2 | 174b.6 | zlom: rozgrzewka i srednie w zapisie | nie (to samo, tylko przetrwa wczytanie) |
| 3 | 174b.5 | tempo: mniej liczenia cen, ten sam wynik | nie |
| 4 | 174b.1 | kontrakt trzyma cel (oboz, postoj, BK Shipping) | naprawa bledu |
| 5 | 174b.2 | dowoz: trasa przed karawana, morze, mniejszy ladunek, ruda z jukow | tak (wiecej dowozu) |
| 6 | 174b.3 | ruda dla tego, kto wiecej na niej zarobi (strzelarze) | tak (maly zakres) |
| 7 | 174b.4 | rezerwa kramu dla zbroi | tak (pytanie 1) |

174b.5 przed 174b.4, bo rezerwa liczy sztuki pasma z pamieci polki z 174b.5 (zero dodatkowych przejsc po polkach).

---

## 3. Poprawki

### 3.0 174b.0 POMIARY I NAZWY (sam log, bez zmiany rozgrywki)

Po co: log sklad6 podaje tylko liczby miast bez rudy i bez strzal, bez nazw (B rozdz. 1); polki zbroi tylko suma; zakupy AI bez podzialu;
czasu modulow 171-174 log nie mierzy (D rozdz. 1.3 - `MoneyLedger.Mark` to migawki zlota, nie czasu). Bez tego nie da sie sprawdzic
progow 174b ani przypisac zmian krokom.

| Linia | Co | Gdzie | Koszt |
|---|---|---|---|
| "Miasta bez rudy i strzal (174b)" co 5 dob | nazwy miast bez rudy i bez strzal; 10 miast z najwiecej cykli "brak rudy" (warsztaty + strzelarze) od ostatniej linii; 10 miast z najwiekszym zapasem rudy; liczba konwojow (karawan ze statkami) stojacych w portach miast z nadwyzka rudy | nowa metoda w `MaterialOrders` (dane z petli `Line` `:543-551`, ktora juz przechodzi `Town.AllTowns`; licznik cykli per miasto - `NoteMissMask` `:115-120`) | 1 przejscie 97 miast na 5 dob |
| "Kontrakty surowca (174)" - rozbicie | "cel zmieniany" na: w obozie (godzin), ruszona z postoju, inny cel [3 przyklady: karawana -> miasto, w osadzie / na morzu], BK Shipping zablokowany; "bez drogi" na: wyspa bez portu, droga ladem > zasieg, morze poza zasiegiem, brak konwoju w porcie; osobno kontrakty "morzem" i "z jukow" | `MaterialOrders.Line` `:516-559` | bez zmian |
| M1 "Zbroja na polkach (174b)" codziennie | korpus i helm wedlug pasm t1-2 / t3-4 / t5-6: sztuk na polkach miast; liczba MIAST z >= 1 sztuka (bez unikatow) w kazdym pasmie; sztuk w rezerwie kramu (po 174b.4) | `ArmsPricing.Census` `:391-447` (juz przechodzi wszystkie polki raz na dobe) | w tym samym przejsciu |
| M2 "ZakupyAI: dzien" - rozbicie | sztuki i zloto wedlug kupujacego (lordowie / zalogi miast / zamowienia zamkow / notable dla ochotnikow / ludzie gracza) x grupa (korpus, helm, reszta zbroi, bron biala, bron strzelecka, amunicja) | liczniki w lambdach dostawy `AiGear.cs:474-482, 488-496, 519-526`, `VolunteerKit.cs:247-254, 279-283`, `MenPurse.cs:311-316`; wypis w `AiGear.FlushDay` `:574-581` | liczniki |
| M3 "Sakiewka ludzi" - rozbicie | nadwyzki sprzedane z sakiewek wedlug grup (jak M2) | `MenPurse.SellAiSurplus` `:328-378` | licznik |
| "Koszt 171-174 (doba)" | patrz 174b.5 F6 | - | probka 1/16 |

Wykonawca przenosi do repo skrypty pomiaru z diagnoz jako `tools/p174b_*.py` (tempo: `SCR\a174b\tempo-skrypty\doby.py`, `koszty.py`;
rynek: `SCR\a174b\skrypty\rynek.py`) - w tym samym commicie.

### 3.1 174b.1 KONTRAKT TRZYMA CEL (punkt 1 zadania)

**Problem [P].** Ze 132 zwolnionych kontraktow 113 to "cel zmieniany przez innych", 16 "rozkaz odrzucony", 2 wrogosc; dojechalo 164 z 296
zakonczonych (55%). "Cel przywrocony": nowa kampania 340 w 40 dob, zapis 362 - 1 w 8 dob (zwolnien "cel" 113 wobec 5).

**Przyczyna 1 - nasz nocny oboz [K][P] (A rozdz. 2).**
- `NightRest.AiNightCamp` (`NightRest.cs:601-705`): o 0:00-6:00 karawana w polu dostaje `RememberOrder` (`:681`), `Ai.DisableForHours(1)` (`:682`)
  i `SetMoveModeHold()` (`:683`) - cel = brak. `HoldSleepers` (`:366-418`, co 0.1 h gry) kladzie z powrotem kazdego spiacego z rozkazem
  (`:396-397`). Swit (`:607-628`) oddaje zapamietany cel (`GiveOrderBack` `:559-599`).
- Doba gry biegnie o godzinie zaleznej od chwili utworzenia albo wczytania kampanii (`Campaign.cs:1002-1012`, `CreateCampaignEvents`: pierwsze
  odczekanie z reszty `(Now - CampaignStartTime)`). W nowej kampanii sklad6 `MaterialOrders.Daily` (`ArmouryBehavior.cs:1318`) wypadal miedzy
  taktem 5:00 a switem (noc.log "AiNightCamp: 5:00" [03:52:53] -> "Kontrakty surowca" [03:52:54] -> "NightRest swit 6:00" [03:52:54], kazdej doby).
- `Keep` (`MaterialOrders.cs:194-220`) widzial `TargetSettlement == null != Dest` (`:210`), dawal `Move` i `c.Retarget++` (`:215`, "cel
  przywrocony"), `HoldSleepers` po 0.1 h kladl karawane z powrotem, a trzeciej nocy `:213` zwalnial kontrakt ("cel zmieniany przez innych").
  Kazdy kontrakt, ktory spedzil w polu 3 noce, ginal; srednia dostarczonych 2.0-2.9 doby drogi pasuje do progu "<= 2 noce".
- Blad jest sesyjny: na zapisie 362 doba wypadala PO swicie - stad 1 przywrocenie w 8 dob. U Jeffa moze wrocic po dowolnym wczytaniu.

**Przyczyna 2 - nasz prefiks biegnie raz na dobe [K] (A rozdz. 2.3).** `HourlyPrefix` (`MaterialOrders.cs:493-513`) siedzi na BK
`BKCaravansBehavior.HourlyTickParty`, a BKROTPatch wpina tam wczesniej dlawik `BKCaravansHourlyThrottlePatch` (`SCR3\ore-supply\bkrot\
BKROTPatch.Patches\BKCaravansHourlyThrottlePatch.cs:40-75`, `true` tylko w godzinie `hash(StringId) % 24`). Harmony 2.4.2 pomija kolejne
prefiksy `bool` po pierwszym `false` (`SCR\a174b\dec\harmony\HarmonyLib\MethodCreator.cs:225-236` - skok `Ldloc runOriginal; Brfalse`
dla kazdego prefiksu, ktory `AffectsOriginal`; `MethodCreatorTools.cs:202-207` - kazdy prefiks zwracajacy `bool`). Skutek: karawana z kontraktem
postawiona na Hold po bitwie (`MapEvent.cs:903`) stoi do 24 h; `DestLost` sprawdza sie raz na dobe.

**Przyczyna 3 - BK Shipping zmienia cel w obcym porcie [K] (A rozdz. 3.2).** `BKShippingBehavior.AfterSettlementEntered_Caravan`
(`SCR3\ore-supply\bk\BannerKings.Behaviours.Shipping\BKShippingBehavior.cs:879-899`) przy kazdym wjezdzie karawany (nie na morzu) do miasta
z portem pyta `ThinkNextDestination` i wola `RouteCaravanHopByHop` (`:1704-1843`, `SetMoveGoToSettlement :1750, :1807, :1819`). Karawana
z kontraktem, ktora ucieka do obcego portu, dostaje nowy cel. Stary stan "hop-by-hop" sprzed kontraktu prowadzi ja przez `TickParty`
(`:2146-2210`) -> `AdvanceHopByHopWaypoints` (`:1845-1889`, `:1883`). Prefiks 174 tego nie obejmuje.

**Zmiana.**
1. `NightRest.IsCamping(MobileParty mp)` = `_orders.ContainsKey(mp)` (`NightRest.cs:541`; wpis w `RememberOrder` `:544-557` przy zasnieciu `:681`,
   kasowany o swicie `:623-627` i przy alarmie `GiveOrderBack :677`). Odczyt slownika O(1), zadnej nowej listy.
2. Nowe `MaterialOrders.Hourly()` wolane w delegacie godzinowym ZARAZ PO `NightRest.OnHourly()` (`ArmouryBehavior.cs:634`) - stan obozu tej
   godziny jest juz ustawiony (o 0:00 karawana juz spi, o 6:00 juz dostala cel z powrotem). Petla po `_contracts` (w tescie 20-45 pozycji), dla
   kazdego kontraktu w tej kolejnosci:
   - karawany nie ma / nieaktywna -> przepada jak dzis (`:202`); w bitwie -> nic (`:203`); rozwiazywana -> zwolnienie (`:204`);
     `DestLost` -> zwolnienie (`:206`); stoi w celu -> `Deliver` (`:209`);
   - **`IsCamping` -> nic i nic nie liczymy** (licznik "w obozie", godziny) - jedna regula: karawana z kontraktem spi jak kazda;
   - w obleganym miescie -> nic (`:212`);
   - **cel = brak albo `DefaultBehavior == Hold` (postoj po bitwie `MapEvent.cs:903`, zmiana wlasciciela miasta `SettlementHelper.cs:384-404`)
     -> wyjazd z osady, jesli w niej stoi (`:214`), i `Move` do celu BEZ `c.Retarget++`** (licznik "ruszona z postoju");
   - **cel = INNE miasto -> jak dzis**: `c.Retarget >= MaxRetarget` -> zwolnienie "cel zmieniany przez innych", inaczej `Move` i `c.Retarget++`
     (licznik "cel przywrocony" + przyklad do linii: karawana -> miasto, w osadzie?, na morzu?).
   `Keep` w dobie (`:194-220`) zostaje tylko z limitem 30 dob (`:207`) i wylacznikiem (`:208`); logika celu jest tylko w takcie godzinowym.
3. `HourlyPrefix` (`:493-513`) tylko blokuje decyzje BK: kontrakt -> `DestLost` ? zwolnienie i `return true` : `return false`. Bez `Move` i bez
   licznikow (to robi `Hourly`). Dlawik BKROT nie ma juz znaczenia dla kontraktu - biegniemy wlasnym taktem co godzine.
4. `ReleasePrefix` (`:468-480`, BK `ReleaseCaravanFromHold` po wczytaniu i po oblezeniu): `IsCamping` -> `return false` bez `Move` (swit odda cel).
5. **BK Shipping:** (a) prefiks na `BKShippingBehavior.RouteCaravanHopByHop(MobileParty, Settlement)` (`:1704`, `public bool`): karawana
   z kontraktem i `intendedDest != c.Dest` -> `__result = false; return false` (licznik "BK Shipping zablokowany"). Lapie oba wejscia
   (`:894` i `:1883`); BK loguje wtedy "leaving target unchanged" (`:896`) - cel zostaje nasz. (b) W `Place` po wyjezdzie z miasta (`:357`)
   wolac publiczne `InvalidateRedirectCache(car)` (`:967-984`) na instancji `BKShippingBehavior` (typ przez `QuartermasterLaw.FindType`,
   metoda i instancja zapamietane raz) - kasuje stary `hopByHopState`. Brak typu/metody -> jedna linia "BRAK" przy starcie, bez wywrotki.
   Ratunku BK (`TickParty :2158-2190` - cel oblegany/wrogi; `RedirectAIToShippingPort :2681-3415` - konwoj na mieliznie) NIE ruszamy: nasz
   `DestLost` zwalnia kontrakt wczesniej, a po ratunku konwoju wraca regula "inny cel".
6. `MaxRetarget = 2` (`:55`) bez zmian - liczy juz tylko prawdziwe przestawienie na inne miasto. Uzasadnienie: na zapisie 362 (doba po swicie)
   prawdziwych zmian celu bylo ok. 0 (1 przywrocenie w 8 dob) - prog chroni tylko przed ping-pongiem z ratunkiem BK.
7. AI gry (ucieczka) nietkniete: nie ustawiamy `DoNotMakeNewDecisions` (`SetHold :387-392` jak dzis przy wpietym prefiksie), `MobilePartyAi`
   zmienia tylko cel krotkoterminowy (`MobilePartyAi.cs:738-803`), a `Move` po ucieczce przywraca dlugoterminowy.

"Rozkaz odrzucony" (16, konwoj wyslany ladem) usuwa 174b.2 (karawana dobierana do drogi).

**Liczby.** Koszt `Hourly`: <= 50 kontraktow x 24 h = ok. 1 200 odczytow pol i slownikow na dobe - ponizej 1 ms. Oczekiwanie [S]:
"cel zmieniany przez innych" 113 -> <= 10 w 40 dob (<= 3% zakonczonych); "cel przywrocony" 340 -> <= 40; dojechalo 55% -> 85-92% zakonczonych
(gorna granica 164 + 113 + 16 z 296 = 99%, minus prawdziwe straty - bandy, wojna, oblezenie celu - 2-5%, minus resztka ping-pongu).
Postoj po bitwie: do 1 h zamiast do 24 h.

**Po krytyce (uwagi 1, 2, 3, 5, 6, 9, 14, 15 - rozdz. 11).** (pkt 1) `IsCamping` = wpis `_orders` ORAZ wlaczony oboz (`NightRestEnabled`, `AiCampsAtNight`) i godzina obozu.
(pkt 3-4) `ReleasePrefix`, `HourlyPrefix` i `Hourly` najpierw odtwarzaja kontrakty z zapisu (BK wola `ReleaseCaravanFromHold` w `OnGameLoaded`, przed
`OnSessionLaunched`); licznik w `ReleasePrefix` wedlug reguly `Hourly`. (pkt 5a) prefiks BK Shipping blokuje KAZDA karawane z kontraktem i od razu wola
`InvalidateRedirectCache` na `__instance`; (pkt 5b) w `Place` instancja brana przy kazdym wywolaniu (`GetCampaignBehavior<T>` przez `MakeGenericMethod`), bez
zapamietanej instancji. Nowe: `NightRest.ForgetOrder` przy zwolnieniu/dostawie/zniszczeniu kontraktu; 6 kolejnych godzin ruszania z postoju = zwolnienie
"postoj wymuszany" (jedna linia z nazwa).

### 3.2 174b.2 DOWOZ: TRASA PRZED KARAWANA, MORZE, MNIEJSZY LADUNEK, RUDA Z JUKOW (punkt 2 zadania)

**Problem [P].** Miast bez rudy 36 na d40 (sr. d31-40 37.3; 172b 36; zapis 362 32-43). Bez kontraktu najczesciej "bez drogi" (98/d w d31-40,
4 100 w 40 dob). Ruda zakontraktowana 915 ladunkow w 40 dob (23/d). Rudy nie brakuje: zapas swiata rosnie o ok. +126 ladunkow/d, w jukach
karawan jedzie 1 010 ladunkow, a 57 miastom ponizej zapasu brakuje razem 421 (B rozdz. 0, 2.3). "Bez zysku" 1 040 razy w 40 dobach, takze na
krotkich trasach (Highgarden - Ashford 93: "ruda bez zysku (5/2)").

**Przyczyna [K] (`MaterialOrders.Order :222-298`; A rozdz. 4.1, B rozdz. 5.2, 6).**
1. Kandydaci z linii prostej (`:245`), takze przez wode.
2. Przewoznik = karawana z najwiekszym wolnym miejscem bez wzgledu na trase (`:254-261`). **Po krytyce (uwaga 8) - poprawione:** konwoj (statki bez ladu,
   `CaravanPartyComponent.cs:54`) w porcie zostaje `IsCurrentlyAtSea = true` (`EnterSettlementAction.cs:78-81` zeruje to tylko partiom z ladem), wiec
   `MobileParty.InventoryCapacity` (`:892`) dolicza mu ladownie - konwoj wygrywal wolnym miejscem i dostawal rozkaz ladowy (16 "rozkaz odrzucony").
   Zysk morza daje dobor przewoznika do trasy, nie inny wzor miejsca (wzor zostaje).
3. Morze tylko, gdy w ogole nie ma drogi ladowej, i tylko jesli WYBRANA w pkt 2 karawana ma statki, a oba miasta port (`:265-272`). Droga ladem
   dluzsza niz zasieg (Celtigar Keep 327, Tyrosh 465, Sunspear 405 do kopalni) = morza w ogole nie probuje.
4. Zasieg sprawdzany na pelnej dlugosci rejsu (`:273`); `SeaFreightShare` (0.25) zmniejsza tylko oplate (`:285`).
5. Ilosc: q = min(brak na 10 dob, nadwyzka, miejsce, kiesa) (`:277-278`), marza na SRODKOWEJ sztuce duzego q (`:282-286`) - przy duzym q cena
   celu spada ponizej zakupu + oplaty, choc mniejszy ladunek by przeszedl.
6. Karawana, ktora JUZ ma surowiec w jukach, nie moze go dowiezc - zrodlem jest tylko polka miasta.
7. Konwoj (statki bez ladu: `CaravanPartyComponent.cs:54` `SetLandNavigationAccess(false)`) wybrany po wolnym miejscu dostaje rozkaz ladowy -
   straznik BK `GuardSettlementMove` (`SCR3\ore-supply\bk\BannerKings.Patches\AiDecisionTracePatches.cs:683-707`, tylko `Default`) go odrzuca;
   ruda jest juz kupiona (`Place :349` przed `:359`) i zostaje w konwoju - 16 "rozkaz odrzucony".

Geografia (pamiec drog gry `ROT-Map/ModuleData/DistanceCaches/*.bin`, zgodna z logiem co do jednostki; A rozdz. 4.2, B rozdz. 3): z 788 par
zrodlo-cel rudy w linii <= 200 tylko 336 ma droge ladem <= 200; 244 to wyspy, 194 objazd > 200, 14 Westeros-Essos. **16 miast to wyspy,
do ktorych ruda moze przyjsc tylko statkiem** (Arbor, Blacktyde, Dragonstone, Driftwood Hall, Elyria, Evenfall Hall, Hull, Ibben, Lonely Light,
Lotus Bay, Lys, Mormont Keep, New Ghys, Pebbleton, Sisterton, Ten Towers - wszystkie maja port). 27 z 36 miast bez rudy ma port. Zelazne Wyspy
maja 4 z 26 kopaln (ok. 16% wydobycia) - ruda opusci je tylko statkiem.

**Zmiana (`Order` przepisane; regula "kontrakt tylko z zyskiem ponad koszt drogi" i wzor oplaty bez zmian).**
1. **Zrodla z `Town.AllTowns`** (97) zamiast `Settlement.All` (1 065): zrodlem i tak moze byc tylko miasto (`:244`) - ten sam wynik, 11x mniej
   iteracji (do ok. 680 zamowien na dobe).
2. **Najpierw trasa, potem karawana, ktora nia pojedzie.** Dla kazdego zrodla:
   - **lad:** `dL = GetDistance(src, dest, false, false, Default)` (jak `:264`); wazna, gdy `dL < BkLimit` (50 000, `CartTownExit.cs:45`)
     i `dL <= zasieg`; **koszt = dL**; filtr linii prostej `<= zasieg` (jak `:245`).
   - **morze** (oba miasta z portem, wylacznik `TownMaterialOrderBySea`): `dS = GetDistance(src, dest, true, true, Naval)`
     (`NavalDLCMapDistanceModel.cs:46-60`, tablica Naval z pamieci drog); wazna, gdy `dS < BkLimit`, `dS <= TownMaterialOrderSeaMaxRoute`
     (1 000) i **koszt = dS x SeaFreightShare (0.25) <= zasieg**; filtr linii prostej `<= min(1000, zasieg / 0.25)`.
   - **przewoznik** z `src.Parties` (`Eligible :302-310`; po krytyce uwaga 26 - takze karawany rodu gracza, wylacznik): trasa ladowa - tylko
     `HasLandNavigationCapability` (`MobileParty.cs:308`), trasa morska - tylko `HasNavalNavigationCapability` (`MobileParty.cs:312`); wolne miejsce jak
     dzis (`:258` - konwoj w porcie ma juz ladownie, uwaga 8). Rozkaz morski jak BK: `NavigationType.Naval` + port.
   - gdy obie trasy sa mozliwe - licza sie obie; wygrywa najwieksza marza na kg (jak `:288-289`).
   - rozkaz jak dzis: morze = `NavigationType.All` + port (`:370-371`); straznik BK sprawdza tylko `Default`, wiec rozkaz morski nie jest odrzucany.
3. **Ilosc wedlug zysku:** q0 jak dzis (`:277-278`); jesli marza q0 <= 0, probujemy q0/2, q0/4, ..., dopoki `q x kg >= TownMaterialOrderMinLoadKg`
   (100 kg); pierwsze q z marza > 0 wygrywa. Marza liczona jak dzis (srodkowa sztuka, prawdziwy model cen `PriceAt :314-327`, oplata
   `CarterPencePerKgPer100 x kg x q x koszt / 100`). Uzasadnienie: oplata na sztuke jest stala (0.0375 x 100 kg x 93 / 100 = 3.5 d na ladunek
   Highgarden - Ashford), a rozjazd cen srodkowej sztuki maleje z q - do miasta z pusta polka (pierwszy ladunek ok. 29 d) maly ladunek
   oplaca sie nawet z daleka (B rozdz. 6). Bez nowego klucza - to poprawka wyboru ilosci.
4. **Surowiec z jukow** (wylacznik `TownMaterialOrderFromPacks`): przewoznikiem moze byc takze karawana stojaca w DOWOLNYM miescie w zasiegu
   (nie tylko z nadwyzka), ktora ma surowiec m w jukach (>= 100 kg): q = min(brak, w jukach) (+ polowienie z pkt 3), **bez zakupu**;
   marza = q x cena w celu (srodkowa sztuka) - q x cena, za jaka sprzedalaby go TU (`PriceAt(src, m, selling: true, shift)`) - oplata. Czyli kontrakt
   tylko wtedy, gdy dowiezienie daje karawanie wiecej niz sprzedaz na miejscu - z biezacego zysku. Kontrakt z `Paid = 0`, dostawa jak dzis
   (`Deliver :417-453`). Format zapisu `arm_matorders` bez zmian (9 pol, `Paid` 0). Prawdziwy towar, prawdziwa droga.
5. **Zasiegi i stale:** ruda 200 (koszt), reszta 600 (`Settings.cs:857-858`, bez zmian) - "ruda z bliska" zostaje regula, morze jest tansze, jak bylo
   naprawde. `SeaFreightShare` 0.25 bez zmian ([H]: koszt przewozu morze : lad ok. 1 : 28-56 wedlug edyktu Dioklecjana - 0.25 to ostroznie;
   ruda plynela woda tam, gdzie trzeba - Elba -> Toskania). Nowy `TownMaterialOrderSeaMaxRoute` = **1 000** jednostek: 164 dostawy jechaly srednio
   2.7 doby, czyli ok. 65 jednostek/d [P/S] - 1 000 to ok. 15 dob, polowa limitu kontraktu 30 dob (`:207`). Dla rudy i tak wiaze koszt
   (200 / 0.25 = 800); limit dlugosci dotyczy surowcow z zasiegiem 600 (bez niego rejs do 2 400 = 37 dob).
6. **Liczniki "bez drogi"** rozbite (174b.0): wyspa / inna czesc ladu bez portu, droga ladem > zasieg, morze poza zasiegiem albo dlugoscia,
   brak konwoju w porcie zrodla.

**Liczby [S] (model B rozdz. 3 i 9, cache drog gry).** Zasieg kontraktu obejmie 29 z 36 miast bez rudy (dzis najwyzej 16, z czego 8 wysp tylko
"przypadkiem"). Glowne porty-zrodla: Blacktyde / Lordsport / Ten Towers (Zelazne Wyspy -> Pebbleton, Lonely Light, Flint's Finger, Mormont Keep),
Heart's Home (Sisterton, White Harbor, Driftwood Hall), Sharp Point (Dragonstone, Hull, Celtigar Keep, Duskendale, Tyrosh, Pentos, Myr), Tolos /
Volantis (Elyria, New Ghys, Lys, Aquos Dhaen, Lotus Bay), Oldtown (Arbor). Poza zasiegiem zostaje 7: Ibben i New Ibbish (morzem 850-880, koszt
> 200), Planky Town, Ifequeveron, Vaith, Winterfell, Bolozo (ladem 226-353) - dla nich tylko karawana z ruda w jukach przejezdzajaca w zasiegu
(pkt 4) albo paczka 133 (rozdz. 9).
Oczekiwanie (po krytyce uwaga 16 - z punktem zamowienia; bez niego rachunek krytyki dawal 19-28): miast bez rudy sr. d31-40 37.3 -> **13-22**
(7 miast poza zasiegiem + 2-5 bez zdolnego zrodla, np. Sharp Point dla 10 miast + ok. 20 obsluzonych x 0.15-0.3 czasu na zerze); ruda w kontraktach
23 (d31-40: 10.4) -> 30-60 ladunkow/d; "bez drogi" 98 -> <= 40/d; "rozkaz odrzucony" 16 -> 0; kontrakty morskie > 0 (liczbe pokaze pomiar konwojow
w portach zrodel). Brak rudy w warsztatach 787 -> 400-600 cykli/d.

**Po krytyce (uwaga 16) - dodane:** (7) **punkt zamowienia** (`TownMaterialOrderAhead`): codziennie miasto, ktore surowca uzywa, zamawia, gdy zapas + w drodze
< (D + 2) x zuzycie (D = srednia dob drogi 3 ostatnich dostaw tego surowca do miasta, domyslnie 4); rachunek marzy bez zmian, przerwa jak dotad. (8) **ilosc
z obecnych rak**: zapas na max(10, D + 4) dob zuzycia celu z `CaravanBulk.UseNow` (obecne rece); prog nadwyzki zrodla z dawnych rak (zrodla nie znikaja), ale
nie ponizej punktu zamowienia samego zrodla (`SourceKeep` - miasto nie jest naraz zrodlem i zamawiajacym). (9) **audyt ilosci** kontraktu (uwaga 20).
(10) Linia 174b.0: zrodla rudy z nadwyzka (zapas/nadwyzka, wywiezione kontraktami, konwoje w porcie) i ruda dostarczona wedlug miasta. Kontrakt dwuetapowy
(konwoj plynie pusty po ladunek) - rozdz. 9.

**Koszt.** Na zamowienie: zrodla w zasiegu (10-30) x (1-2 odczyty pamieci drog + petla po partiach w miescie, jak dzis) + polowienie ilosci
(najwyzej 3 wyceny surowca po ok. 5 us, tylko gdy q0 nie przeszlo) - razem ok. 20-50 ms/d; oszczednosc z `Town.AllTowns` (ok. 680 x 968
pominietych osad na dobe) jest tego samego rzedu. Zadnej nowej petli co godzine ani co klatke.

### 3.3 174b.3 RUDA DLA TEGO, KTO WIECEJ NA NIEJ ZAROBI - STRZELARZE (punkt 3 zadania)

**Problem [P].** Miast bez strzal 31 na d40 (sr. d31-40 31.9; 172b 24). Strzelarze koncza prace "brak surowca" w 42-53 miastach, z tego ruda
31-38 - prawie dokladnie miasta bez rudy (B rozdz. 4). Bezczynne rece strzelarzy 956 roboczodni/d; AI bez strzal 145 razy/d, cofniete
awanse z braku amunicji 62/d. W kampanii Jeffa miast bez strzal 0-2 (trzyma je stary zapas).

**Przyczyna [K].** Snop strzal potrzebuje 0.060 ladunku rudy (`WorkshopLaw.Needs :157-171`, pomiar 20.1 ladunku na 333 snopy), a strzelarz
bierze z polki tylko caly ladunek (`TownFletchers.cs:424` - `floor(dlug + potrzeba)`; ladunek starcza na ok. 16 snopow). Strzelarze pracuja
w postfiksie `DailyTickTown` PO warsztatach (`TownFletchers.cs:234-235`) - platnerz zabiera kazdy ladunek pierwszy, niezaleznie od tego, kto
na nim wiecej zarobi. To kolejnosc w kodzie, nie rynek.

**Zmiana.**
1. Glownie 174b.2: ruda dociera do miast, w ktorych strzelarze stoja.
2. **Ostatni ladunek rudy dla tego, kto wiecej na nim zarobi** (wylacznik `FletchersBidForOre`):
   - `TownFletchers.Work` (`:370-472`): gdy kandydat strzal/beltow jest oplacalny (`:420`), ale zablokowany brakiem rudy (`:424-428`, bit 0),
     strzelarze zapisuja w stanie miasta (`St`, `:42`) "oferte za ladunek" = najwieksze `(rev - cost) / need[ruda]` z dzisiejszego przebiegu
     (`rev`, `cost` jak `:411-419`) i dobe tej oferty.
   - `WorkshopLaw.TryStart` (`:337-363`): po policzeniu przychodu i kosztu (`:357-359`), gdy sztuka bierze rude (`Take[0] > 0`), a po jej starcie
     na polce zostalby mniej niz 1 caly ladunek (`Available(ruda) - Take[0] < 1`), i oferta strzelarzy z dzis albo wczoraj jest wieksza niz zysk
     tej sztuki na ladunek rudy (`(przychod - surowce - place) / need[ruda]`) -> sztuka czeka ("brak surowca": `return 2` z bitem rudy, ten sam
     sygnal zamowienia `MaterialOrders.NoteMissMask`).
   - Najwyzej 1 ladunek na miasto: tyle strzelarze potrzebuja na polce, zeby ruszyc. Gdy go nie wezma (brak rak albo zysku), nastepnego dnia
     nie sa zablokowani ruda - oferta znika i ladunek wraca do platnerzy. Bez listy miast, bez stalego priorytetu - rachunek zysku na
     wspolnym, rzadkim surowcu (koszt utraconej okazji).
3. Bez zmian: rece strzelarzy (`TownFletcherHandsPerArmsHand` 0.3), receptury, ceny.

**Liczby [S].** 1 ladunek = ok. 16 snopow albo ok. 1 zbroja na tulow t2-t3 (B rozdz. 9 B4). Oferta wygrywa w miastach, gdzie strzal brak
(mediana indeksu ceny strzal 2.43). Koszt: jeden odczyt slownika na `TryStart` z ruda - pomijalny.

**Po krytyce (uwagi 7 i 22) - zmienione:** zamiast "najwyzej 1 ladunek, tylko ostatni" - oferta za kazdy ladunek az do dzisiejszych rak strzelarzy:
platnerz czeka, gdy po starcie na polce zostaloby mniej rudy niz `ceil(rece strzelarzy / dni na snop x ruda na snop - dlug)` (bezpiecznik 5 ladunkow), a jego zysk na
ladunek `(przychod - surowce - place) / ruda na sztuke` jest mniejszy niz oferta. Oferta = wczorajsza (strzelarze pracuja po warsztatach). Cykl czekajacy to
kod 5 "czeka na strzelarzy" - nie "brak surowca" i nie lista miast z "brakiem rudy"; sygnal zamowienia rudy idzie. Ryzyko: zatrzymany ladunek moga zabrac
linie narzedzi gry i warsztat gracza - licznik "ladunkow wzietych z zatrzymanych". Przewidywanie: przy dostawie co kilka dni strzelarze dostaja 1-2 ladunki
na cykl, +50-150 snopow/d; miast bez strzal 31.9 -> **18-26** (bylo 10-18 - nieuzasadnione).

### 3.4 174b.4 RYNEK ZBROI: MIARA GRACZA I REZERWA KRAMU (punkt 4 zadania)

**Problem [P].** Zbroja korpusu na polkach d10/20/30/40: 2 862 / 872 / 331 / 1 998 wobec 8 860 / 6 115 / 1 983 / 1 067 w 172b; od d25 ponizej 400 szt.
ZakupyAI x2.3 (2 650 szt. i 194.6 tys. zl/d w d31-40), gorsza zbroja 222 szt./d.

**Co naprawde sie dzieje (C rozdz. 1-5).**
- **Kupowania w kolko nie ma.** AI zdejmuje z polek 58-114% tego, co na nie trafia (produkcja + odsprzedaz) - tak samo w 172b i sklad6; zakupy
  ograniczaja braki koszykow (`AiGear.Deficit :287-333`) albo luka "dowolnego szczebla" (`BuySubstitutes :78-86`), nie pieniadz (mediana
  wizyty wydaje 0.8% budzetu). Brakuje 307 tys. sztuk (korpus 65 tys.). Nadwyzki 1.6-3.8 tys. szt./d w d36-40 to lup z bitew (DTE oddaje
  zbrojownie pokonanych), sprzedawany od najgorszych (`MenPurse.SellAiSurplus :328-378`, ponad 110% potrzeby typu). **Po krytyce (uwaga 12):**
  sluchacze `SettlementEntered` ida w ODWROTNEJ kolejnosci rejestracji (`MbEvent.cs:100-106`), wiec `AiGear` kupuje PRZED sprzedaza nadwyzek przez
  `MenPurse` (dotad zakladano odwrotnie) - budzet AiGear nie widzi dzisiejszego utargu; petli i tak nie ma (sprzedaz po typie ponad 110%). Kolejnosci nie
  zmieniamy w 174b - rozdz. 9. Jedyny "obieg" to tanie garby (zastepcza ok. 13 zl, odkup ok. 1 zl) - sztuka nie znika, strata
  lordow < 10 tys. zl/d.
- **Gorsza polka w d10-30 wyjasniona co do sztuki:** start ColdStart mniejszy o 3 799 szt. + 8 891 zastepczych garbow, ktore w 172b lezaly na
  polce, a teraz ubraly zolnierzy - minus wieksza produkcja i mniejszy odplyw (174.0).
- **Problem gracza:** zbroi na tulow t3 lezy na polkach calego swiata 0-5 szt., t4 0-1; kowale robia jej 2-18 i 0-6 dziennie przy popycie
  5 045 i 3 219 (warsztaty.log d40). Ok. 250 zbroi/d to w ponad 90% t1-t2.

**Miara (decyzja projektu - zamiast "polka korpusu nie ponizej 172b").** Suma polek to bufor miedzy kowalem a kupcem; przy niedoborze i wlaczonej
gorszej zbroi pusta polka znaczy "wszystko poszlo do wojska" - porownanie z 172b karaloby za to, ze zolnierze nosza zbroje. Dlatego:
- **wojsko AI:** "cokolwiek na tulowiu" razem 75-95% (pasmo historyczne ochrony tulowia), partie >= 72%, korpus szczebla razem >= 60%
  (sklad6: 80.5 / 73 / 61.2);
- **gracz (M1, nowa; po krytyce uwaga 19):** od d15 w kazdym spisie **>= 49 z 97 miast (50%) dostalo w ostatnich 7 dobach NOWA zbroje na tulow t1-2
  i >= 24 z 97 (25%) - t3-4** (bez unikatow; nowa = wyrob warsztatu, dostawa kupcow, odsprzedaz z sakiewek, nadwyzka albo zawrocony towar zalogi,
  przyrost polki pasma miedzy spisami). Dotychczasowa miara "jest >= 1 sztuka" przechodzilaby z samego zapasu startowego (ColdStart rozdaje ok. 30%
  t3-4, rezerwa go zamraza) - zamrozona sztuka idzie osobno ("sztuk w rezerwie", bez progu). Uzasadnienie bez zmian: gracz odwiedza ok. 1 miasto
  dziennie; przy 50% trafia w 2 wizytach z szansa 75%; przy 25% w 4 wizytach 68%, w tydzien 87% - "rzadko i drogo, ale da sie", jak na wojnie;
- polka korpusu - tylko diagnostycznie, w pasmach t1-2 / t3-4 / t5-6 (M1), bez progu.

**Hamulec - rezerwa kramu (C rozdz. 6A; wylacznik `ShopKeepsLastArmour`, liczba `ShopKeepPieces` = 1).**
- **Po krytyce (uwagi 13 i 21):** zakup osobisty ma w grze tylko gracz - regula jest jawnym WYJATKIEM od jednej reguly (pytanie 1, warianty);
  awans ochotnika cofniety przez rezerwe ma osobny powod i licznik.
- Regula: w MIESCIE (nie w zamku - zamek to nie targ, 171 C2.3) kupujacy HURTEM dla oddzialu nie zabiera ostatniej sztuki (N = 1, bez unikatow)
  pasma t1-2 / t3-4 / t5-6 zbroi: tulow, glowa, nogi, rece. Hurt to: `AiGear.BuyLoop` (`:343-391`, takze zamowienia zamkow na polce miasta `:519`),
  `AiGear.BuySubstitutes` (`:70-136`), `VolunteerKit.BuyCore` (`:203-287`, notable dla ochotnikow), `MenPurse.BuyPlayerGaps` (`:283-325`,
  sakiewka ludzi GRACZA), `SupplyDemand.DailyTrade` (`:422-533`, wywoz z polki zrodla zostawia 1 sztuke pasma). Zakup osobisty przy straganie
  (ekran handlu) - bez limitu.
- Bron, tarcze i amunicja - bez rezerwy: bron t1 jest w nadmiarze, a rezerwa strzal falszowalaby miare "miast bez strzal".
- Sygnal dla kowali zostaje: `OnShelf` (`AiGear.cs:394-402`) liczy tylko sztuki PONAD rezerwe - gdy zostala sama rezerwa, zamowienie
  `NoteUnmetOnce` (`:540`, `:556`) idzie jak przy pustej polce; w `VolunteerKit` sztuka z rezerwy nie jest kandydatem -> `best < 0` ->
  zamowienie jak dzis (`:236-240`).
- Licznik pasma z pamieci polki 174b.5 F1 (osobny licznik "bez unikatow") - zero dodatkowych przejsc po polce. Nowa linia "Rezerwa kramu (174b)":
  zakupy hurtowe zatrzymane na rezerwie (wedlug kupujacego), sztuk w rezerwie.
- Uzasadnienie historyczne [H]: prawo miejskie XIII-XIV w. przeciw wykupywaniu towaru (forestalling, engrossing) i obowiazek cechu sprzedawac
  przy straganie kazdemu; Assize of Arms 1181 i Statut z Winchesteru 1285 kazaly wolnym ludziom miec bron wedlug majatku - miejski platnerz
  obslugiwal klienta z ulicy, nie tylko dostawce dla wojska.

**Liczby.** Rezerwa najwyzej 97 x 4 x 3 = 1 164 sztuk zbroi na swiat (korpus 291, helm 291; w nogach i rekach t1-2 i tak lezy nadmiar).
Koszt dla wojska jednorazowo <= ok. 600 szt. korpusu i helmow (<= 0.3 pp "cokolwiek na tulowiu" przy 105 tys. ludzi w partiach), potem strumien
bez zmian - wszystko ponad rezerwe kupuja jak dzis. Cena dla gracza: rezerwa nie zbija ceny przy suficie mnoznika - znajdzie, ale drogo,
zgodnie z niedoborem. Oczekiwanie [S] (po krytyce - miara nowej sztuki): miasta z NOWYM korpusem t1-2 w 7 dobach 55-80 z 97, t3-4 15-35 (t3-4 powstaje
dzis 15-25/d na swiat - prog 24 moze nie przejsc; wtedy to sygnal produkcji t3-4, nie blad rezerwy).

**Petli nie bedzie:** rezerwa tylko zmniejsza zakupy hurtowe; sztuka z rezerwy nie trafia do zbrojowni AI, wiec nie moze wrocic jako nadwyzka.
Straznik w tescie: jesli sprzedane nadwyzki korpusu (M3) przekraczaja 50% kupionego korpusu (M2) przez 5 dob z lupem < 1 tys./d - zatrzymac
i zbadac.

**Czego NIE robimy (C rozdz. 6B-E):** sufitu ceny dla AI (ceny korpusu juz x3.4-3.5 wartosci, premie wojenne 5-44% - armie przestalyby kupowac
akurat na wojnie); pierwszenstwa partii przed zalogami (najpierw M2); "sprzedazy nadwyzek tylko raz" (petli nie ma); zmiany zastepczej zbroi
(pancerz >= 10, sufit ceny) - osobno, po tescie (rozdz. 9).

### 3.5 174b.5 TEMPO: MNIEJ LICZENIA, TEN SAM WYNIK (punkt 5 zadania)

**Problem [P] (D rozdz. 1-2).** Nowa kampania 14.21 s/dobe wobec Z1b 13.03 i Z1 13.33 (+7-9%), d31-39 17.11 wobec 15.22 / 15.89 (sklad6 rosnie
16 -> 19 s); zapis 362: 28.0 wobec 22.1 / 24.7 przy identycznym swiecie (karawany 1 029-1 031, lordowie 641-665) - tam caly regres
(+3.3..5.9 s/d) to kod. Regres rozlozony na godziny (+49 ms/h, d31-39 +79 ms/h, zapis +200-245 ms/h); blok `OnDailyTick` Armoury < 1 s i nie urosl.
Wielkosc swiata tlumaczy ok. 0.3 s/d w nowej kampanii; reszta ok. 0.8 s/d (0.5-1.1) i ok. 4 s/d na zapisie to kod. Zmierzone dzis koszty
(ksiegi 169, ramki towarow, H3, I1, Pokrycie) urosly razem tylko o ok. +20 ms/d.

**Przyczyna [K].** Kazda cena sprzetu w osadzie (`TownMarketData.GetPrice` -> `SupplyDemand.PricePostfix :340`) liczy `Factor` (`:302-311`):
`Stock` (`:282-298`) przechodzi CALA polke, `Substitution` (`:210-232`) druga raz, `ShelfView` -> `TradeScreenOpen` (`:241-249`) dwa razy, `Demand`
skleja napis klucza (`:98`, `:203`) przy kazdym wywolaniu. Lancuch modeli kosztuje ok. 4-5 us na cene, nasza warstwa +1-2 us w nowej kampanii
i +4-6 us na zapisie (36 tys. stosow uzbrojenia, ok. 300 na miasto). 171-174 dokladaja nowe petle cen (D rozdz. 4, ranking):
`AiGear.BuySubstitutes` (150-350 / 2 000-3 000 ms/d nowa / zapis), `WorkshopLaw.CyclePrefix/TryStart/Quickest` (100-200 / 30-60), `VolunteerKit.BuyCore`
(50-100 / 300-500), zamowienia zamkow (30-60 / 100-200), `TownFletchers.Work` (30-50 / 50-100), ramka `GoodsLedger` przed filtrami (13 zmierzone).

**Zmiana (F1-F6; zadna nie zmienia liczby, ceny ani wyboru kupowanej sztuki).**
- **F1 pamiec polki.** Na kazdy `ItemRoster` (`ConditionalWeakTable`) indeks: `count[typ*10 + tier]` (wszystkie sztuki) i `countNoUnique` (dla
  rezerwy 174b.4), klucz waznosci `(VersionNo, Count)`. W grze 1.4.8 kazda zmiana rosteru idzie przez `AddToCounts` -> `UpdateVersion()`
  (`ItemRoster.cs:194-220`, `:218`; `AddNewElement :181`; `Clear :282`; `RemoveIf/Add/Remove :359-394` wolaja `AddToCounts`), `VersionNo` -
  `:28`. `Stock` (galaz zywej polki) = odczyt koszyka; `Substitution` "jest sztuka tieru t+1" = koszyk (typ, t+1) > 0 - ta sama definicja co
  petla z `break`. **Bez zmian:** galaz zamrozonej polki ekranu handlu (`ShelfView :251-265`) i korekta wyceny zamowienia `_heldShelf` (`:294`).
  `TradeScreenOpen()` raz na klatke (zapamietany wynik dla tej samej referencji `ScreenManager.TopScreen`). `Demand` bez sklejania napisu (klucz
  zlozony z osady, typu i tieru; slownik `_unmet` i format zapisu `arm_unmet` bez zmian). Przebudowa indeksu do NOWEGO obiektu i podmiana
  referencji (bez wspoldzielonej mutacji - `[ThreadStatic]` przy `_depth` i `_heldShelf` mowi, ze wycena bywa wolana z wiecej niz jednego watku).
  **Samokontrola (po krytyce uwaga 4 - probkowana):** `ShelfIndexSelfCheckDays` = 1 - w pierwszej dobie sesji co 64. odczyt, potem co 4096., liczymy
  tez pelne przejscie; rozjazd -> wynik z przejscia, przebudowa, jedna linia "Pamiec polki (174b.5): ROZJAZD", licznik "rozjazdow od startu sesji" (ma byc 0).
  **Po krytyce (uwaga 11):** uchwyt w CWT z referencja `volatile` (net472 bez `AddOrUpdate`), VersionNo i Count czytane PRZED budowa; `TradeScreenOpen`
  zapamietany tylko dla tej samej referencji `TopScreen`; wylacznik `ShelfIndexEnabled` (false = petla jak dotad).
  Szacunek: -0.1..0.25 s/d nowa, -1.5..2.5 s/d zapis (dotyczy tez cen BK, karawan, gracza, `WorkshopLaw.Revenue`, `TownFletchers.Fac`).
- **F2 `AiGear.BuySubstitutes` i `BuyLoop`.** (a) wczesne wyjscie przed pierwszym przejsciem polki (`:86-91`): gdy w `need` nie ma koszyka
  z brakiem, ktory moglby kupic zastepcza (bron biala przy `gapMelee > 0` albo tulow przy `gapBody > 0` - ten sam warunek co `:112-113`), druga
  petla i tak nic nie kupi -> `return 0` bez wyceny polki; (b) w drugiej petli cena kandydata z pierwszego przejscia (`:98`) jest uzywana,
  dopoki nic nie kupiono; po kazdym zakupie (`:124`) pamiec cen kasowana w calosci i cena liczona od nowa jak dzis (`:119`) - miedzy dwiema
  wycenami tego samego stosu bez zakupu stan polki, kiesy i ksiegi jest ten sam. To samo w `BuyLoop` (`:359-373`): cena stosu zapamietana do
  najblizszego zakupu (`:380`) - stosy tieru t-1 nie sa wyceniane drugi raz przy koszyku t-1, gdy koszyk t nic nie kupil. NIE zawezac pierwszego
  przejscia do potrzebnych tierow (`cands.Sort :105` jest niestabilny - mniej elementow moze zmienic kolejnosc remisow i kupiony modyfikator).
  Szacunek: -0.1..0.25 s/d nowa, -1.5..2.5 s/d zapis (z F1 nie sumuje sie liniowo).
- **F3 `VolunteerKit.BuyCore`.** W petli `need` (`:220-245`) polka nie zmienia sie az do `AddToCounts` (`:249`) - cena stosu `i` liczona raz na
  wywolanie (`price[i]`); w petli dodatkow (`:257-285`) pamiec kasowana po kazdym zakupie (`:279`). Szacunek: -30..60 ms/d nowa, -200..350 zapis.
- **F4 `WorkshopLaw.TryStart`.** `matCost` tylko dla `need[m] > 0` (`:357`; 0 x skonczona cena = 0 - dokladne; odpada 2-3 z 4 wycen surowca na
  kazdy `TryStart`); `MatPrice` (`:1112-1125`) i `Available` (`:174-177`) zapamietane w jednym `CyclePrefix` wedlug `shelf.VersionNo` (`DoStart`
  zmienia wersje - przeliczy). Szacunek: -50..120 ms/d nowa.
- **F5 `AiGear.TryBuy`.** `GoodsLedger.Begin(FArmsBuy)` (`:407`) dopiero po tanich filtrach (`On`, rodzaj partii, `_lastDay :432`) - ramka bez
  `AddToCounts` rozlicza sie na zero, ksiega ta sama. Zmierzone ramki 43 -> ok. 30 ms/d.
- **F6 linia "Koszt 171-174 (doba)"** (wzor `CirculationWindows`: `Stopwatch.GetTimestamp()` co 16. wywolanie x16, liczniki wywolan pelne;
  BEZ `Thread.Suspend`): `SupplyDemand.PricePostfix` (wywolan, w tym sprzet w osadzie, czas `Factor`), `AiGear.TryBuyCore` (czas `BuyLoop`
  i `BuySubstitutes`, cen policzonych), `VolunteerKit.BuyCore`, `WorkshopLaw.CyclePrefix` (`TryStart`, `Quickest`), `TownFletchers.Work`, zamowienia
  zamkow, `CaravanAmmo` (3 latki), `MaterialOrders.Hourly` i `Order`, rezerwa kramu, `GarrisonArmory`; suma "nasze" wobec czasu doby z zegara.

**Koszt nowosci 174b [S]:** `MaterialOrders.Hourly` < 1 ms/d; `Order` +20-50 ms/d minus oszczednosc `Town.AllTowns`; oferta strzelarzy - odczyt
slownika; rezerwa kramu - odczyt indeksu F1; M1-M3 - liczniki w istniejacych petlach; linie co 5 dob. Razem < 50 ms/d.

**Oczekiwanie [S] (D rozdz. 5):** nowa kampania -0.4..0.8 s/d (14.21 -> 13.4-13.8), zapis -3..4.5 s/d (28.0 -> 23.5-25).
**Po krytyce (uwaga 17) - werdykt tempa:** (1) zapis 362 PARAMI: Z1b i 174b uruchomione kolejno w tej samej sesji autotestu, mediana 7 pelnych dob;
regres = mediana 174b > mediana Z1b + 1 s (1 s = rozrzut median Z1/Z1b) - P8; (2) twardy budzet F6: suma "nasze 171-174b" (BuySubstitutes, zamowienia
zamkow, VolunteerKit.BuyCore, WorkshopLaw.CyclePrefix z TryStart/Quickest, TownFletchers.Work, CaravanAmmo, MaterialOrders, GarrisonArmory; rezerwa kramu
osobno, bo zagniezdzona w zakupach) <= 1% czasu doby (ok. 0.13 s/d nowa, ok. 0.22 s/d zapis) - P13; (3) P7 tylko informacyjnie, zawsze mediany. **Jesli P8
albo P13 nie przejdzie, nastepny krok wybiera linia F6** (np. F2(c) - odrzut "za drogi" z pamieci; jedna lista kandydatow na wizyte w `BuyLoop`), nadal bez
zmiany zachowania i bez wgrania.

### 3.6 174b.6 ZLOM: ROZGRZEWKA I SREDNIE W ZAPISIE (punkt 6 zadania)

**Problem [P][K].** `ArmsScrap` (wylacznik `OldStockToScrap`, wlaczony decyzja Jeffa) rusza po 30 dobach pomiaru zakupow W SESJI (`ArmsScrap.cs:32`
`_days`, `:77`); licznik i srednie (`_ema :28`, `_today :29`, `_acc :30`, `_oreAcc :31`) nie sa zapisywane. Zapis 362: po 8 dobach "rozgrzewka 8/30".
W kampanii Jeffa (ok. 1 mln sztuk na polkach) skup ruszylby dopiero po 30 dniach gry bez wczytywania. W nowej kampanii skupil 134 szt. od d31 -
tam starego nadmiaru prawie nie ma (zgodnie z zalozeniem).

**Zmiana.**
- Klucz `arm_scrap` przez `SaveText.Sync`, wzorem `ArmouryBehavior.cs:518-519` (`Export` tylko przy zapisie, wlasny `try`, `Import` -> napis
  odlozony, rozwiazanie `ArmsScrap.ResolvePending` w `OnSessionLaunched` obok `MaterialOrders.ResolvePending` `:1018`, a gdyby doba przyszla
  wczesniej - na poczatku `ArmsScrap.Daily` `:60`).
- Tresc: wersja formatu, `_days` (do 9 999), `_ema`, `_today`, `_acc`, `_oreAcc`. Klucze wedlug **StringId osady + koszyk**, nie `MBGUID.InternalValue`
  z `Key()` (`:45`) - po wczytaniu przeliczone na klucz sesji. Wartosci z 4 cyframi znaczacymi; wpisy `_ema < 0.001` pomijane (to <= 0.36 sztuki
  "roku popytu" - pomijalne). `_oreAcc` (ulamki rudy ze zlomu) zapisane - po wczytaniu metal nie przepada (zamknieta ekonomia).
- Brak klucza (stary zapis) -> jak dzis, od zera, linia "brak klucza - rozgrzewka od zera"; klucz nieczytelny -> to samo + `Log.Error` raz.
- Linia po wczytaniu: "Zlom z nadmiaru (zapis 174b): odtworzono N par z M osad, rozgrzewka X/30".

**Liczby [S].** Par (osada, koszyk) z zakupami: najwyzej 227 osad x 84 koszyki, realnie 3-8 tys. x ok. 12 znakow = 40-100 KB - `SaveText.Sync`
dzieli na kawalki po 8 000 znakow (`SaveText.cs:21-45`), ponizej limitu 32 KB na napis. Koszt: tylko przy zapisie i wczytaniu.
Skutek u Jeffa: po wgraniu 174b rozgrzewka 30 dni gry raz (stary zapis nie ma pomiaru), potem skup dziala po kazdym wczytaniu.

**Po krytyce (uwagi 10 i 25):** w napisie takze `_lastDay` (gra po wczytaniu przesuwa faze doby - pierwsza doba moze miec ten sam numer co ostatnia przed
zapisem i liczylaby sie drugi raz); `Export` rozwiazuje odlozony napis; przy kazdym zapisie probny odczyt tym samym parserem (liczba par, doby, sumy
srednich i rudy) - linia "probny odczyt zgodny / ROZJAZD" (P10). Format: `1|doby|ostatnia doba~osada:koszyk=srednia,dzis,ulamek;...~miasto=ruda;...`.

---

## 4. Przewidywany wynik [S]

| Miara (nowa kampania, 40 dob) | sklad6 | Prog 174b (po krytyce) | Przewidywanie |
|---|---|---|---|
| Miast bez rudy sr. d31-40 | 37.3 | <= 25 | 13-22 (z punktem zamowienia) |
| Miast bez strzal sr. d31-40 | 31.9 | <= 24 | 18-26 |
| Brak rudy w warsztatach (cykle/d, d31-40) | 787 | <= 500 (pomocniczo) | 400-600 |
| Kontrakty: dojechalo / zakonczone | 55% | >= 80% | 85-92% |
| Kontrakty: "cel zmieniany" / "rozkaz odrzucony" | 113 / 16 | <= 5% / 0 | <= 10 / 0 |
| Kontrakty morskie, z jukow (40 dob) | ok. 1 / - | > 0 (pomocniczo) | > 0; liczba zalezy od konwojow w portach zrodel (pomiar 174b.0) |
| Ruda w kontraktach (ladunki/d) | 23 (d31-40: 10.4) | - | 30-60 |
| Miasta z NOWYM korpusem t1-2 / t3-4 w 7 dobach (od d15) | brak miary | >= 49 / >= 24 | 55-80 / 15-35 |
| "Cokolwiek na tulowiu" sr. spisow d30/35/40 partie / razem | 71.7 / 79.8% | >= 70.2 / >= 78.3% | 71-73 / 79-81% |
| Produkcja z amunicja / korpus (sr. d31-40) | 2 244 / 250 | >= 2 000 / >= 200 (pomocniczo) | 2 200-2 400 / 220-270 |
| Snopy strzal/d | 260 (d31-40) | - | 300-400 |
| Tempo mediana d2-40 / d31-39 (s/dobe) | 14.0 / 17.0 | informacyjnie (P7) | 13.2-13.8 / 15.5-16.5 |
| Tempo zapisu 362 parami (mediana 7 pelnych dob) | 27 wobec Z1b 22 | <= mediana Z1b + 1 s (P8) | Z1b + 0..2 s |
| Koszt "nasze 171-174b" (P13) | brak miary | <= 1% doby | 0.5-2% - moze nie przejsc (wtedy kolejny krok F6) |
| Zlom po wczytaniu | rozgrzewka od zera | ciaglosc, probny odczyt zgodny | ciaglosc |
| Bledy / potkniecia Armoury | 0 | 0 | 0 |

Przewidywanie siega za prog w trzech miejscach: tempo i koszt (P8, P13 - dlatego F6 i plan z rozdz. 3.5), miasta bez strzal (P3 - zalezy od dowozu rudy
174b.2) i nowa sztuka t3-4 (P5 - zalezy od produkcji t3-4, ktorej 174b nie zmienia).

---

## 5. Pliki, metody, klucze MCM, linie logu

| Krok | Pliki i metody | Nowe klucze MCM (domyslne) | Linie logu |
|---|---|---|---|
| 174b.0 | `MaterialOrders.cs` (`Line :516-559`, `NoteMissMask :115-120`, nowa linia co 5 dob), `ArmsPricing.cs` (`Census :391-447`), `AiGear.cs` (lambdy `:474-482, 488-496, 519-526`, `FlushDay :574-581`), `VolunteerKit.cs` (`:247-254, 279-283`), `MenPurse.cs` (`:311-316`, `SellAiSurplus :328-378`), `tools/p174b_*.py` | - | "Miasta bez rudy i strzal (174b)", "Zbroja na polkach (174b)", rozbicia w "Kontrakty surowca (174)", "ZakupyAI: dzien", "Sakiewka ludzi" |
| 174b.6 | `ArmsScrap.cs` (`Export/Import/ResolvePending`, `:28-32, :45, :60`), `ArmouryBehavior.cs` (`SyncData` obok `:518-519`, `OnSessionLaunched` obok `:1018`) | - | "Zlom z nadmiaru (zapis 174b)" |
| 174b.5 | `SupplyDemand.cs` (`Key :98`, `Demand :188-204`, `Substitution :210-232`, `TradeScreenOpen :241-249`, `Stock :282-298`, `Factor :302-311`), NOWY `ShelfIndex.cs`, `AiGear.cs` (`BuySubstitutes :70-136`, `BuyLoop :343-391`, `TryBuy :405-410`), `VolunteerKit.cs` (`BuyCore :203-287`), `WorkshopLaw.cs` (`TryStart :337-363`, `CyclePrefix :202-335`), NOWY albo w `CirculationWindows.cs` - licznik kosztu | `ShelfIndexSelfCheckDays` (1) | "Koszt 171-174 (doba)", "Pamiec polki: rozjazdy" (doba samokontroli) |
| 174b.1 | `MaterialOrders.cs` (`Keep :194-220`, nowe `Hourly`, `HourlyPrefix :493-513`, `ReleasePrefix :468-480`, `Place :331-362`, `ApplyAll :660-682` - prefiks BK Shipping), `NightRest.cs` (nowe `IsCamping`, `_orders :541`), `ArmouryBehavior.cs` (`:634`) | - | rozbicie "cel zmieniany" w "Kontrakty surowca (174)"; start: "BK Shipping RouteCaravanHopByHop wpiety / BRAK", "InvalidateRedirectCache znaleziony / BRAK" |
| 174b.2 | `MaterialOrders.cs` (`Order :222-298`, `Eligible :302-310` bez zmian, `Place :331-362` - kontrakt z jukow bez zakupu) , `Settings.cs` | `TownMaterialOrderBySea` (true), `TownMaterialOrderSeaMaxRoute` (1000), `TownMaterialOrderFromPacks` (true) | "morzem", "z jukow", rozbicie "bez drogi" |
| 174b.3 | `TownFletchers.cs` (`St :42`, `Work :370-472`), `WorkshopLaw.cs` (`TryStart :337-363`), `Settings.cs` | `FletchersBidForOre` (true) | w "Strzelarze (172)": ofert strzelarzy, ladunkow zatrzymanych dla strzelarzy, z tego wzietych |
| 174b.4 | NOWY `ShopReserve.cs` (licznik pasma z `ShelfIndex`), `AiGear.cs` (`BuyLoop`, `BuySubstitutes`, `OnShelf :394-402`), `VolunteerKit.cs` (`BuyCore`), `MenPurse.cs` (`BuyPlayerGaps :283-325`), `SupplyDemand.cs` (`DailyTrade :422-533`), `Settings.cs` | `ShopKeepsLastArmour` (true), `ShopKeepPieces` (1, zakres 0-3) | "Rezerwa kramu (174b)" |

Opisy MCM po angielsku (przyklady): `TownMaterialOrderBySea` - "A town short of a raw material may also hire a merchant ship in a port that has it to spare: the
sea leg costs a quarter of the same distance by road, and the ship can be taken by pirates"; `TownMaterialOrderFromPacks` - "A caravan that already carries
the material may take the order and deliver it, if that pays better than selling it where it stands"; `FletchersBidForOre` - "When a town lacks arrows, its
last load of iron ore goes to whoever earns more on it - the armourers or the fletchers"; `ShopKeepsLastArmour` - "Buyers for a whole company (lords, garrisons,
notables, your own men) leave the last piece of each kind of armour on the stall for whoever buys in person".

Wpisy `CHANGELOG.md` (po jednym na krok, status NIEWGRANE - DO SPRAWDZENIA) z "Ryzyko / co sprawdzic" wedlug rozdz. 7.

**Po krytyce i wykonaniu - roznice wobec tabeli:** nowe pliki `Measure174b.cs` (M1-M3, 174b.0), `Cost174.cs` (F6), `ShelfIndex.cs` (F1), `ShopReserve.cs`;
nowe klucze MCM ponad tabele: `ShelfIndexEnabled` (true), `TownMaterialOrderAhead` (true - punkt zamowienia), `TownMaterialOrderPlayerCaravans` (true - pytanie 2);
nowe linie: "Zbroja na polkach (174b)" (z "nowa w 7 dob" i zrodlami nowych sztuk), "ZakupyAI wedlug kupujacego (174b)" (M2, M3, zatrzymane na rezerwie),
"Koszt 171-174 (doba)" (z pamiecia polki), w "Pokrycie zbrojowni AI (171)" - "razem (partie i zalogi wazone liczba ludzi)", w "Miasta bez rudy i strzal (174b)" -
ruda dostarczona wedlug miasta i zrodla z konwojami; w "Kontrakty surowca (174)" - bloki "174b.1" i "174b.2" (w tym "audyt ilosci: rozjazdy"); w "Strzelarze (172)" -
"ruda dla strzelarzy (174b.3)"; w "Warsztaty" - "czeka na strzelarzy"; w "Ochotnicy" - "w tym przez rezerwe kramu". Skrypty: `tools/p174b_progi.py`,
`tools/p174b_doby.py`, `tools/p174b_koszty.py` (rynek z diagnozy C nie przeniesiony - sciezki na sztywno, miary sa teraz w logu).

---

## 6. Test i progi (autotest: 40 dob nowej kampanii + 8 dob zapisu 362; gre uruchamia sesja glowna, wykonawca NIE)

Pomiar tempa jak w D: doba = od "Warsztaty: dzien N" do "Warsztaty: dzien N+1" (znaczniki czasu logu), bez pierwszej doby sesji (samokontrola pamieci
polki), zawsze MEDIANA (`tools/p174b_doby.py`). Zapis 362 PARAMI: Z1b (md5 17a700d7) i 174b uruchomione kolejno w tej samej sesji autotestu. Progi liczy
`tools/p174b_progi.py <log> [--zapis] [--baza <log sklad6>]` (sprawdzony na logach sklad6 i 172b). **Po krytyce (uwagi 17-20, 23-25) tabela przepisana.**

| # | Prog | Zrodlo w logu | Nowa kampania | Zapis 362 |
|---|---|---|---|---|
| P1 | 0 bledow Armoury ("ERROR in"), 0 potkniec nowych modulow; CrashScribe 6-8 tylko startowe | caly log, session-*.log | tak | tak |
| P2 | Miast bez rudy | "Kontrakty surowca (174)" | sr. d31-40 <= 25 | trend: sr. d6-8 <= 0.8 x sr. d1-2 ORAZ ruda dostarczona kontraktem do >= polowy miast z pierwszej listy "bez rudy" ("Miasta bez rudy i strzal (174b)") |
| P3 | Miast bez strzal | "Strzelarze (172)" | sr. d31-40 <= 24 | informacyjnie (u Jeffa 0-2 z zapasu) |
| P4 | Kontrakty: dojechalo / (dojechalo + zwolnione + rozbite) >= 80%; "rozkaz odrzucony" 0; "cel zmieniany przez innych" <= 5% zakonczonych | "Kontrakty surowca (174)" (suma) | tak | informacyjnie; po wczytaniu "cel przywrocony" ok. 0 |
| P5 | Gracz: miasta, do ktorych w 7 dobach trafila NOWA zbroja na tulow: t1-2 >= 49, t3-4 >= 24 (bez unikatow) | "Zbroja na polkach (174b)" | kazdy spis od d15 | kazdy spis od 8. doby |
| P6 | "Cokolwiek na tulowiu" (wzor "razem": partie i zalogi wazone liczba ludzi) | "Pokrycie zbrojowni AI (171)" | srednia spisow d30/35/40: partie >= 70.2%, razem >= 78.3% (sklad6 - 1.5 pp) | razem i korpus szczebla razem nie nizej niz pierwszy spis biegu - 1 pp, bez gornej granicy |
| P7 | Tempo (informacyjnie): mediana d2-40 i d31-39 | `p174b_doby.py` | tak | - |
| P8 | Tempo zapisu parami | `p174b_doby.py --zapis <174b> <Z1b>` | - | mediana 174b <= mediana Z1b + 1 s |
| P9 | Pamiec polki: 0 rozjazdow | "Koszt 171-174 (doba)" ("rozjazdow od startu sesji"), brak "Pamiec polki (174b.5): ROZJAZD" | tak | tak |
| P10 | Zlom: przy kazdym zapisie "probny odczyt zgodny"; po wczytaniu "odtworzono N par" (nowa kampania z zapisem) albo "brak klucza w zapisie - rozgrzewka od zera" (zapis 362) | "Zlom z nadmiaru (zapis 174b)" | tak | tak |
| P11 | Nic z niczego: ruda (ksiega towarow, iron) - suma dodatnich dob "bez wyjasnienia" <= 15 i zadna doba > +10 (sklad6 9, 172b 14); reszta R (bilans - przyczyny) - suma dodatnich dob <= 10 tys. d (sklad6 4.7 tys.), zapis <= 25 tys. d (sklad6 11.1 tys.); ruda i drewno ZGODNA; audyt kontraktow "rozjazdy 0" | "Towary: dzien - iron", "Pieniadz swiata (bilans - przyczyny)", "Towary (bilans)", "Kontrakty surowca (174)" | tak | tak |
| P12 | Linia startowa MaterialOrders: ReleaseCaravanFromHold, HourlyTickParty (BK), RouteCaravanHopByHop wpiete, InvalidateRedirectCache znaleziony, bez "BRAK" | start | tak | tak |
| P13 | Koszt "nasze 171-174b" <= 1% doby (srednio) | "Koszt 171-174 (doba)" | tak | tak |

Pomocnicze (bez automatycznego "NIE", ale do opisu w analizie): brak rudy w warsztatach <= 500 cykli/d; produkcja z amunicja >= 2 000/d
i korpus >= 200/d w d31-40 (regres kowali przez 174b.3 i 174b.4); kontrakty morskie > 0 i "z jukow" > 0; "czeka na strzelarzy" > 0 tylko w miastach
z oferta, "ladunkow wzietych z zatrzymanych" > 0; "Rezerwa kramu" - sztuk w rezerwie <= 1 164; straznik petli M3 / M2 (rozdz. 3.4); "w tym przez rezerwe
kramu" w "Ochotnicy"; linia "Koszt 171-174" - ranking kosztow do nastepnego kroku tempa (`tools/p174b_koszty.py`).

---

## 7. Ryzyka / co sprawdzic (kontrola wedlug zasady 0 CLAUDE.md)

**Regresje i kolizje.**
- 174b.1: `NightRest` czyta tylko swoje pola; `MaterialOrders.Hourly` wolane PO `NightRest.OnHourly` w tym samym delegacie (`ArmouryBehavior.cs:634`) -
  stala kolejnosc w godzinie. `HoldSleepers` nie walczy juz z naszym `Move` (spiacych nie ruszamy). Alarm nocny (`:663-679`) oddaje cel od reki -
  karawana nie jest juz "w obozie", takt kontraktu widzi cel = Dest. Kontrakt zawarty w godzinach obozu (doba o 5:00): karawana wychodzi z miasta
  (`Place :357`), zasypia na nastepnym takcie z zapamietanym celem Dest - poprawnie.
- 174b.1: prefiks na `RouteCaravanHopByHop` dziala tylko dla karawan z kontraktem (`_byCar`), pozostale karawany BK bez zmian. Sygnatura z dekompilacji
  BK (`public bool RouteCaravanHopByHop(MobileParty caravan, Settlement intendedDest)`); brak -> "BRAK" przy starcie, bez wywrotki.
  `InvalidateRedirectCache` - publiczna, ale wolana przez refleksje; wyjatek - licznik potkniec, nie wylacznik.
- 174b.1: `HourlyPrefix` przestaje ruszac karawane - jedyne miejsce ruchu to `Hourly`; `Keep` w dobie bez logiki celu - nie ma dwoch licznikow tego
  samego zdarzenia.
- 174b.2: `CaravanBulk` (`:205`, `:391`), `CaravanAmmo` (`:156`, `:390`) i `IslandRoads` (`:211`) juz przepuszczaja karawane z kontraktem
  (`HasContract`) - kontrakt morski i "z jukow" tez. Konwoj na morzu nie obozuje (`NightRest.cs:657`). BK wysyla karawany ze statkami tylko do
  portow (`BKCaravansBehavior :1322`) - cel morski zawsze ma port (warunek trasy).
- 174b.2 "z jukow": juki mogly byc kupione przez `CaravanBulk` po drodze - to zwykly towar karawany; `Deliver` sprawdza, ile naprawde jest
  w jukach (`:429-430`). Ksiega towarow: brak zakupu w zrodle (nic sie nie zmienia), sprzedaz w celu przez `SellItemsAction` jak dzis - ksiega rudy
  ma zostac ZGODNA (P11).
- 174b.2 morze: zapis `arm_matorders` ma juz pole "morzem" (`:574`, `:643`) - po wczytaniu rozkaz morski odtwarzany jak dzis.
- 174b.3: oferta strzelarzy to stan sesji (doba dzis/wczoraj) - po wczytaniu brak oferty przez 1 dobe = zachowanie jak dzis; nie wymaga zapisu.
  Sygnal "brak rudy" z `TryStart` przy zatrzymaniu ladunku idzie do `MaterialOrders` (miasto i tak ma za malo rudy) - to prawda, nie podwojenie:
  `_miss` liczy cykle, `Order` zamawia raz na `TownMaterialOrderDays` (3).
- 174b.4: rezerwa nie dotyczy zamkow ani zakupu osobistego; `ArmsScrap` moze skupic sztuke rezerwy tylko w koszyku bez zadnego popytu (nadwyzka ponad
  rok popytu) - to zamierzone (kowale miasta, nie hurt dla oddzialu). Ranking warsztatow liczy polke 1 sztuki wobec popytu ok. 50 na miasto -
  pomijalne. `GarrisonArmory` (zaloga oddaje/zabiera rzeczy) nie kupuje z polki - bez zmian.
- 174b.5 F1: wszystkie zmiany rosteru w grze ida przez `AddToCounts` -> `UpdateVersion`; zmiana przez refleksje innego moda bez `UpdateVersion` -
  lapie ja klucz `Count` tylko przy zmianie liczby stosow, dlatego samokontrola P9. F2-F4: pamiec tylko do najblizszej zmiany polki - to samo
  wejscie, ten sam wynik; kolejnosc kandydatow (niestabilny `Sort`) nietknieta.
- 174b.6: `Key()` z `InternalValue` tylko w sesji, zapis po `StringId`; osada, ktorej po wczytaniu nie ma (inny zestaw modow) - para pominieta.

**Spojnosc calej logiki.** Ta sama kolejnosc w godzinie (oboz -> kontrakty); jedna regula snu; zakup surowca i dostawa zawsze przez handel gry;
rezerwa - jedna regula "hurt / zakup osobisty" dla gracza i AI; stan czyszczony miedzy kampaniami (`MaterialOrders.Reset`, `ArmsScrap.Reset`,
nowe slowniki oferty strzelarzy i indeks polki - `Reset` w konstruktorze `ArmouryBehavior` `:389`).

**Cudzy kod sprawdzony w dekompilacji:** BKROT dlawik (`BKCaravansHourlyThrottlePatch.cs:40-75`), Harmony 2.4.2 (`MethodCreator.cs:225-236`),
BK Shipping (`:879-899, :967-984, :1704-1843, :1845-1889, :2146-2210`), straznik BK (`AiDecisionTracePatches.cs:683-707`), NavalDLC (odleglosc
`NavalDLCMapDistanceModel.cs:46-60`, ladownia `NavalDLCInventoryCapacityModel.cs:31, :58`), gra (`Campaign.cs:1002-1012`, `MapEvent.cs:903`,
`ItemRoster.cs:28, 181, 194-220, 282, 359-394`, `CaravanPartyComponent.cs:54`). NavalDLC, StrategicCampaignAI(145), BetterEconomy, DTE,
DynamicReinforcements, ROT, Diplomacy, RBL i RealisticLoot nie zmieniaja celu cudzych karawan (A rozdz. 3.4).

**Co moze nie wyjsc.**
- Konwojow w portach-zrodlach moze byc za malo (log tego dzis nie mowi - 174b.0 to policzy). Wtedy miasta-wyspy zostana "bez drogi" mimo trasy;
  rozwiazaniem nie jest morze "bez statku", tylko wiecej konwojow (BK spawnuje je wedlug portu wlasciciela) - osobna sprawa po pomiarze.
- Sharp Point (male miasto) wychodzi jako port dla Waskiego Morza - czy ma nadwyzke, pokaze 174b.0.
- Tempo moze zostac 0.1-0.3 s ponad progiem (rozdz. 3.5) - wtedy nastepny krok wedlug F6, bez wgrywania.
- Na zapisie 8 dob nie zejdzie sie do <= 25 miast bez rudy (rachunek krytyki: ok. 23 dopiero pod koniec) - dlatego po krytyce prog zapisu to trend
  i dostawy do polowy miast z listy (P2), a nie liczba.
- (po krytyce) Punkt zamowienia (174b.2) zwieksza liczbe kontraktow wszystkich surowcow (do 97 x 7 sprawdzen na dobe, zamowienie najwyzej raz na 3 doby
  na miasto i surowiec) - wiecej karawan na kontraktach; hamulcem jest rachunek marzy (cena celu spada z zapasem). Sprawdzic "w drodze" i "bez zysku".
- (po krytyce) Oferta strzelarzy (174b.3) moze zatrzymywac rude, ktora zabierze linia narzedzi gry albo warsztat gracza - "ladunkow wzietych z zatrzymanych".
- (po krytyce) P5 t3-4 zalezy od produkcji t3-4 (15-25/d na swiat), ktorej 174b nie zmienia - nieprzejscie to sygnal produkcji, nie blad rezerwy.
- (po krytyce) P13 (<= 1% doby) moze nie przejsc przy obecnych petlach 171-174 - wtedy nastepny krok F6, bez wgrania.

---

## 8. Pytania do Jeffa (zmiana rozgrywki; praca nie czeka - domyslnie rekomendacja)

1. **Ostatnia sztuka zbroi na straganie (WYJATEK od jednej reguly - po krytyce uwaga 21).** Kupujacy hurtem dla oddzialu (lordowie, zalogi, notable,
   kupcy wywozacy nadwyzke, takze Twoi ludzie z wlasnej sakiewki) zostawiaja w miescie ostatnia sztuke kazdego rodzaju zbroi (tulow, helm, nogi, rece;
   osobno tanie, srednie i drogie) dla kupujacego osobiscie. Osobiscie przy straganie (ekran handlu) kupujesz w praktyce TYLKO TY - Ty mozesz wziac takze
   ostatnia sztuke, nawet kupujac hurtem dla oddzialu. Koszt: armie AI dostaja jednorazowo ok. 600 sztuk mniej na caly swiat, potem nic; notabl, ktoremu
   zostala tylko sztuka z rezerwy, nie awansuje ochotnika (osobny licznik).
   Warianty: **(b) tak jak teraz** - wyjatek dla zakupu osobistego (historycznie: prawo miejskie przeciw wykupywaniu chronilo klienta z ulicy przed
   hurtownikiem); **(a) jedna regula wedlug wielkosci zakupu** - takze Ty, kupujac hurtem na ekranie handlu, nie wezmiesz ostatniej sztuki (wymaga osobnej
   latki ekranu handlu - kolejna paczka); **(c) bez rezerwy** - zostaje sam pomiar, P5 dalej mierzy dostawy nowych sztuk.
   **Rekomendacja: (b)** (wlaczone domyslnie, wylacznik `ShopKeepsLastArmour` w MCM); jesli wolisz jedna regule bez wyjatku - (a) jako osobna paczka.
2. **Karawany Twojego rodu i zlecenia na rude (po krytyce uwaga 26).** Czy Twoje karawany moga brac zlecenia miast na rude i inne surowce tak jak karawany
   lordow (kupuja w miescie z nadwyzka albo wioza to, co juz maja, i sprzedaja miastu, ktore zamowilo - zysk do ich kiesy, a wiec Twojego dochodu z karawan;
   Twoja druzyna nigdy)? **Rekomendacja: TAK** (jedna regula; wlaczone domyslnie, wylacznik `TownMaterialOrderPlayerCaravans`). Jesli NIE - zostaje jawny
   wyjatek w rozdz. 2.

---

## 9. Poza paczka (zapisane, nie ruszane w 174b)

- **133 - lupa przy kopalni (nowa kampania; `docs/PLAN-K13-2026-10-07.md` 133).** Jedyna droga do 100% miast z zelazem bez lamania "ruda z bliska":
  lupa 15 kg / ok. 21 d jedzie 600 jednostek za ok. 16% wartosci; obejmie 7 miast poza zasiegiem 174b (Ibben, New Ibbish, Planky Town, Ifequeveron,
  Vaith, Winterfell, Bolozo). Sztab `ironIngot` NIE uzywac jako towaru (to material kuzni gracza, na polkach miast 6 szt.; B rozdz. 7), grotow jako
  nowego towaru NIE dodawac (lupa daje to samo prosciej). Warunek wejscia: wynik 174b (miasta bez rudy po nazwach z 174b.0).
- **`SupplyDemand.DailyTrade` (`:422-533`)** przenosi bron i zbroje miedzy osadami bez partii na mapie (kupcy "w tle", z zaplata). Dla rudy Jeff
  zazadal prawdziwych partii; dla uzbrojenia to stary mechanizm - do decyzji po 174b, czy tez ma jezdzic prawdziwa karawana.
- **Zastepcza zbroja tylko od pancerza tulowia >= 10 i z sufitem ceny** (C rozdz. 6E): ok. 10% zastepczych to habity i tuniki (pancerz < 10), jedna
  "Raider Clothes" kupiona za 1 470 zl. Osobny wpis po tescie 174b (zmienia tez definicje miary "cokolwiek na tulowiu").
- **Zaloga miasta kupuje raz na 3 doby jak zamek** (C rozdz. 6C) - dopiero gdy M2 pokaze, ile korpusu zabieraja zalogi (dzis 37% zlota AI).
- **Konwoje** - ile ich jest w portach-zrodlach i czy BK spawnuje ich dosc (po pomiarze 174b.0).
- **Ogolna optymalizacja gry** - na koniec (decyzja Jeffa 08.10); 174b.5 naprawia tylko regres 171-174.
- **Kontrakt dwuetapowy** (krytyka uwaga 16): konwoj stojacy do ok. 1 doby rejsu od portu-zrodla plynie po ladunek pusty i kupuje na miejscu - prawdziwa
  partia, zgodnie z zasada Jeffa. Po pomiarze konwojow w portach zrodel (linia 174b.0); dotyczy glownie Sharp Point i Heart's Home.
- **Kolejnosc sluchaczy `SettlementEntered`** (krytyka uwaga 12): `AiGear` kupuje PRZED sprzedaza nadwyzek przez `MenPurse` (sluchacze w odwrotnej kolejnosci
  rejestracji) - budzet zakupow nie widzi dzisiejszego utargu. Zmiana kolejnosci to zmiana rozgrywki - do decyzji po tescie 174b.
- **Rezerwa kramu wariant (a)** (pytanie 1): latka ekranu handlu gracza - blokada ostatniej sztuki pasma przy zakupie hurtem.
- **`SupplyDemand.DailyTrade`**: sztuki zatrzymane rezerwa kramu licza sie w linii jako "bez odbiorcy w zasiegu" - do rozdzielenia w linii przy okazji.

---

## 10. Zrodla

- Wynik testu: `SCR\kopia-sklad6\ANALIZA.md`; logi `SCR\kopia-sklad6\Armoury-2026-10-09_03-48-35.log` (40 dob), `_04-03-18.log` (zapis 362),
  `Logs-03-48-35\*` (warsztaty, zakupy, noc, handel); porownanie `SCR\kopia172b\`; skrypty `SCR\analiza-sklad6-skrypty\`.
- Diagnozy: A `SCR\a174b\kontrakty.md` (+ `skrypty\drogi.py, pary.py, essos.py`, `wyniki\drogi.txt, drogi.tsv, pary.txt, essos.txt`); B `SCR\a174b\geografia.md`
  (+ `geo.txt, zrodla.txt, tabela36.md, kopalnie.md`); C `SCR\a174b\rynek.md` (+ `skrypty\rynek.py, diag.py, items.py`); D `SCR\a174b\tempo.md`
  (+ `tempo-skrypty\fazy2.py, fazy_doby.py, doby.py, swiat.py, regr.py, regr_doba.py, koszty.py`).
- Kod przeczytany przy pisaniu (drzewo `SCR\noc2\n174b`, 0c41eee): `MaterialOrders.cs` (calosc), `NightRest.cs:170-200, 360-420, 534-705, 1060-1085`,
  `AiGear.cs` (calosc), `SupplyDemand.cs:1-80, 180-360, 422-533`, `VolunteerKit.cs:190-295`, `WorkshopLaw.cs:150-400, 1112-1125`, `TownFletchers.cs:225-240,
  360-480`, `ArmsScrap.cs` (calosc), `ArmsPricing.cs:316-450`, `MenPurse.cs:283-330`, `SaveText.cs:1-60`, `ArmouryBehavior.cs:389-525, 625-640, 1010-1020,
  1255-1320`, `Settings.cs:447, 699, 718-724, 856-875`, `CartTownExit.cs:45`.
- Gra 1.4.8 (`SCR3\ore-supply\cs`): `TaleWorlds.CampaignSystem\Campaign.cs:1002-1012`, `TaleWorlds.CampaignSystem.Roster\ItemRoster.cs`,
  `TaleWorlds.CampaignSystem.Party\MobileParty.cs:308-312`. BK: `BannerKings.Behaviours.Shipping\BKShippingBehavior.cs:879-899, 967, 1704, 1845, 2146`.
  BKROT: `BKROTPatch.Patches\BKCaravansHourlyThrottlePatch.cs`. Harmony: `SCR\a174b\dec\harmony\HarmonyLib\MethodCreator.cs:225-236`,
  `MethodCreatorTools.cs:202-207`. NavalDLC: `SCR\a174b\dec\NavalDLC.GameComponents.NavalDLCMapDistanceModel.cs:40-60`,
  `NavalDLC.GameComponents.NavalDLCInventoryCapacityModel.cs:31, 58`.
- Projekty: `docs/PROJEKT-174-PRODUKCJA-UZBROJENIA-2026-10-09.md` (3.3 kontrakty, 3.6 gracz, 8 pytania), `docs/PLAN-K13-2026-10-07.md` (133).
- Historia [H]: edykt cenowy Dioklecjana (stosunek kosztu przewozu morze : rzeka : lad, A.H.M. Jones / R. Duncan-Jones); prawo miejskie przeciw
  forestalling/engrossing (Anglia XIII-XIV w.); Assize of Arms 1181; Statut z Winchesteru 1285 (B rozdz. 7, C rozdz. 6A).

---

## 11. Krytyka i odpowiedzi (09.10, przed wykonaniem; wykonanie w tej samej sesji)

Krytyka: 26 uwag (3 krytyczne, 8 waznych, 15 drobnych), numeracja wedlug kolejnosci zgloszenia - te numery nosza komentarze kodu i wpisy CHANGELOG
("krytyka N"). Kazda uwaga sprawdzona w kodzie (drzewo `SCR\noc2\n174b`, gra `SCR3\ore-supply\cs`, BK `SCR3\ore-supply\bk`) albo w logach sklad6/172b
(skrypt `tools/p174b_progi.py`). Werdykt: **P** przyjeta, **C** czesciowo, **O** odrzucona.

| # | Waga | Uwaga (skrot) | Sprawdzenie | Werdykt i co zrobione |
|---|---|---|---|---|
| 1 | krytyczna | Instancja `BKShippingBehavior` zapamietana raz psuje sie po wczytaniu innego zapisu i trzyma w pamieci cala poprzednia kampanie | BK sam bierze instancje przy kazdym wywolaniu (`BKCaravansBehavior.cs:391`) [K] | **P.** Zapamietane tylko `MethodInfo` (`InvalidateRedirectCache`, `Campaign.GetCampaignBehavior<T>` przez `MakeGenericMethod` raz); instancja przy kazdym wywolaniu (`MaterialOrders.ShipForget`), w prefiksie - `__instance` (ta, ktora wola). |
| 2 | wazna | BK `ReleaseCaravanFromHold` po wczytaniu biegnie w `OnGameLoaded` PRZED `OnSessionLaunched` - `_byCar` pusty, BK daje karawanom z kontraktem wlasny cel | `Campaign.cs:1670-1671` (OnGameLoaded -> OnSessionStart), `BKCaravansBehavior.cs:246-262` [K] | **P.** Na poczatku `ReleasePrefix`, `HourlyPrefix` i `Hourly`: `if (_pending != null) ResolvePending(...)`; `ResolvePending` nie gubi napisu, gdy przedmioty nie sa jeszcze gotowe. Karawana zapisana w obozie dostaje rozkaz bez licznika zmian celu ("ruszona z postoju"). |
| 3 | wazna | Blokada `RouteCaravanHopByHop` tylko przy `intendedDest != Dest` zostawia prowadzenie przez wezel posredni (ping-pong) | `BKShippingBehavior.cs:1801-1836` - przy celu = Dest BK i tak robi `SetMoveGoToSettlement(val3)` / `SetMoveGoToPoint` i zapisuje stan [K] | **P.** Blokada dla KAZDEJ karawany z kontraktem (`__result = false`), w tym samym miejscu `InvalidateRedirectCache` na `__instance` - zdarzenie liczy sie raz, `AdvanceHopByHopWaypoints` nie wraca. |
| 4 | wazna | Doba samokontroli pamieci polki (obie wersje dla kazdej ceny) wolniejsza niz dzis - przestawia werdykt tempa | rachunek [S] | **P.** Samokontrola probkowana: pierwsze `ShelfIndexSelfCheckDays` (1) dob sesji co 64. odczyt, potem co 4096.; `tools/p174b_doby.py` pomija pierwsza dobe sesji i liczy mediane. |
| 5 | drobna | `_orders` czysci tylko galaz switu - przy wylaczonym obozie albo martwym bohaterze gracza wpisy zostaja i karawana "spi" do 30. doby | `NightRest.cs:87-93`, `:607-628` [K] | **P.** `IsCamping` = `NightRestEnabled && AiCampsAtNight && InCamp(godzina) && _orders.ContainsKey`. |
| 6 | drobna | Kontrakt zwolniony w nocy zostawia zapamietany rozkaz do utraconego celu - swit kieruje tam karawane | `NightRest.cs:559-599`, `:624-626` [K] | **P.** `NightRest.ForgetOrder` przy kazdym zwolnieniu, dostawie i zniszczeniu kontraktu (`MaterialOrders.Drop`). |
| 7 | drobna | Ladunek zatrzymany dla strzelarzy liczy sie jak brak rudy; "oferta z dzis" nie istnieje; wyciek do linii narzedzi i warsztatu gracza | `WorkshopLaw.cs:298`, `TownFletchers.cs:234-235` [K] | **P.** Osobny kod 5 "czeka na strzelarzy" (linia "Warsztaty"), nie "brak surowca" i nie lista miast z "brakiem rudy"; sygnal zamowienia rudy idzie (`NoteMissMask(..., names: false)`). Oferta = wczorajsza (przy zapasowym sluchaczu przed warsztatami - takze dzisiejsza). Wyciek w ryzykach + licznik "ladunkow wzietych z zatrzymanych". |
| 8 | drobna | "Konwoj w porcie ma ok. 1 t miejsca" przeczy kodowi gry | `CaravanPartyComponent.cs:54` (konwoj bez ladu), `EnterSettlementAction.cs:78-81` (IsCurrentlyAtSea zeruje tylko partiom z ladem), `MobileParty.cs:892` [K] | **P.** Opis przyczyny poprawiony (3.2 pkt 2): 16 "rozkaz odrzucony" to konwoje wygrywajace wolnym miejscem i wysylane ladem; zysk morza daje dobor przewoznika do trasy. Wzor miejsca bez zmian. Oczekiwanie kontraktow morskich - nie liczba, tylko > 0 (pomocniczo), dopoki 174b.0 nie policzy konwojow w portach zrodel. |
| 9 | drobna | `ReleasePrefix` liczy postoj po oblezeniu jako "cel przywrocony" | `MaterialOrders.cs:475-476` [K] | **P.** Ta sama regula co `Hourly`: brak celu albo Hold = "ruszona z postoju", inne miasto = "cel przywrocony" (po 2 - zwolnienie). |
| 10 | drobna | `_lastDay` zlomu nie w zapisie - pierwsza doba po wczytaniu moze miec ten sam numer; `Export` nie mowi o `_pending` | `Campaign.cs:1004-1009` (pierwsze odczekanie z reszty), `ArmsScrap.cs:57-58` [K] | **P.** `_lastDay` w napisie `arm_scrap`; `Export` rozwiazuje odlozony napis jak `MaterialOrders.Export`. |
| 11 | drobna | F1/F4: brak `AddOrUpdate` w net472, VersionNo przed budowa, klucz MatPrice z przedmiotem, TradeScreenOpen dla referencji ekranu | [K] | **P.** Uchwyt w CWT z referencja `volatile`, VersionNo i Count czytane przed budowa; pamiec MatPrice/Available wedlug przedmiotu (ruda, drewno, skora, len, welna); `TradeScreenOpen` zapamietany tylko dla tej samej referencji `TopScreen`. |
| 12 | drobna | `MenPurse.OnEntered` biegnie PO `AiGear` (sluchacze w odwrotnej kolejnosci) - przeslanka 3.4 bledna | `MbEvent.cs:100-106` (`AddNonSerializedListener` wstawia na poczatek) [K] | **P.** Przeslanka w 3.4 poprawiona (AiGear kupuje przed sprzedaza nadwyzek, budzet nie widzi dzisiejszego utargu); kolejnosci NIE zmieniamy w 174b (zmiana rozgrywki) - rozdz. 9 do decyzji. |
| 13 | drobna | W `VolunteerKit` sztuka z rezerwy cofa awans, nie tylko sklada zamowienie | `VolunteerKit.cs:233-241` [K] | **P.** Osobny powod "rezerwa kramu ..." i licznik "w tym przez rezerwe kramu" w linii "Ochotnicy"; pytanie 1 mowi wprost "osobiscie kupujesz w praktyce tylko Ty". |
| 14 | drobna | 174b.0 obiecuje liczniki, ktore istnieja dopiero po 174b.1 | kolejnosc commitow [K] | **P.** W 174b.0 `NightRest.IsCamping` i "w obozie przy przegladzie doby" w starym `Keep`; licznik "BK Shipping zablokowany" w 174b.1; rozbicie "bez drogi", kontrakty morskie i z jukow - w 174b.2 (tam powstaje nowy `Order`). |
| 15 | drobna | Ruch z postoju bez limitu - cudzy kod moze stawiac karawane na Hold co godzine | [K] | **P.** Po 6 kolejnych godzinach ruszania z postoju (nocny oboz sie nie liczy) - zwolnienie "postoj wymuszany" i jedna linia z nazwa karawany. ("> 24 na dobe" przy takcie godzinowym jest nieosiagalne - stad godziny z rzedu.) |
| 16 | krytyczna | "Miast bez rudy 12-20" nieuzasadnione: zamowienie dopiero po zerze, ilosc z dawnych rak, zrodla (Sharp Point) za male, baza 23/d to srednia | logi sklad6/172b, model B [P/S] | **P.** Punkt zamowienia z biezacego zysku (`TownMaterialOrderAhead`): zapas + w drodze < (D + 2) x zuzycie, D = srednia dob drogi 3 ostatnich dostaw (domyslnie 4); ilosc = zapas na max(10, D + 4) dob zuzycia z OBECNYCH rak (`CaravanBulk.UseNow`); prog nadwyzki zrodla z dawnych rak, ale nie ponizej jego punktu zamowienia (`SourceKeep`). Linia 174b.0: zrodla rudy (zapas/nadwyzka, wywiezione, konwoje w porcie) i ruda dostarczona wedlug miasta. Kontrakt dwuetapowy (konwoj po ladunek) - rozdz. 9 (po pomiarze konwojow). Przewidywanie poprawione (rozdz. 4): 13-22. |
| 17 | krytyczna | Progi tempa dopuszczaja regres wobec Z1b i nie odrozniaja zmiany od szumu (Z1 oblalby P8) | `a174b/tempo.md` [P] | **P.** Werdykt tempa: (1) zapis 362 PARAMI - Z1b i 174b kolejno w tej samej sesji autotestu, mediana 7 pelnych dob, regres = mediana 174b > mediana Z1b + 1 s (P8); (2) twardy budzet F6: "nasze 171-174b" <= 1% doby (P13, linia "Koszt 171-174 (doba)"); (3) P7 informacyjnie, zawsze mediany (`tools/p174b_doby.py`). |
| 18 | wazna | P6 na zapisie 362 nie przejdzie z definicji (96-97%); brak wzoru "razem" | spisy zapisu 362 [P] | **P.** Zapis: "razem" i korpus szczebla razem nie nizej niz pierwszy spis biegu - 1 pp, bez gornej granicy. Wzor "razem" = srednia partii i zalog wazona liczba ludzi - w linii "Pokrycie zbrojowni AI (171)" i w skrypcie (z logu sklad6 wychodzi 79.8%). |
| 19 | wazna | P5 nie moze oblac - ColdStart rozdaje ok. 30% t3-4 w kazdym miescie, rezerwa je zamraza | `ColdStart.cs:153` [K] | **P.** P5 = miasta, do ktorych w ostatnich 7 dobach trafila NOWA sztuka pasma (wyrob, kupcy, sakiewki, zalogi, przyrost polki) - t1-2 >= 49, t3-4 >= 24 od d15 (zapis - od 8. doby); zamrozona sztuka startowa - "rezerwa kramu: sztuk N" bez progu. |
| 20 | wazna | P11 nie mowi "kazda doba czy suma": dzien po dniu oblewa baze, srednia jest slepa; kontrakty to nowa droga towaru | logi: sklad6 ruda +2, +7, R dodatnie 4.7 tys. (zapis 11.1 tys.), 172b ruda 14 - potwierdzone skryptem [P] | **P.** P11: ruda (ksiega towarow, iron) suma dodatnich dob "bez wyjasnienia" <= 15 i zadna doba > +10; reszta R suma dodatnich dob <= 10 tys. d (zapis <= 25 tys.); ruda i drewno ZGODNA; audyt kontraktow (zakup: przyrost jukow == ubytek polki zrodla; dostawa: ubytek jukow == przyrost polki celu; z jukow: dostarczono <= bylo) - "audyt ilosci: rozjazdy 0". |
| 21 | wazna | Rezerwa "hurt / osobiscie" to w praktyce podzial gracz / AI - ukryty wyjatek od jednej reguly | AiGear, VolunteerKit, MenPurse, GarrisonArmory, BK `BKPartyBehavior:536-570` (tylko zywnosc) [K] | **C.** Zgoda, ze to wyjatek - zapisany WPROST (rozdz. 0, 2, 3.4, pytanie 1). Wariant (a) zalecany w krytyce ("ostatnia sztuka tylko przy zakupie 1 sztuki pasma w miescie i dobie, dla kazdego") w dokladnym brzmieniu nic nie chroni - pierwszy lord, ktory przyjdzie do miasta z 1 sztuka, kupuje wlasnie jedna; w brzmieniu "wedlug potrzeby kupujacego" wymaga latki ekranu handlu gracza (blokada przeniesienia ostatniej sztuki przy zakupie hurtem) - osobna praca z ryzykiem UI. Wykonane (b) z wylacznikiem; wybor (a)/(b)/(c) - pytanie 1 do Jeffa. |
| 22 | wazna | Strzelarze: limit "1 ladunek, tylko ostatni" to sztuczne ograniczenie; +150-300 snopow i 10-18 miast nieuzasadnione; zalozenie "bez strzal = bez rudy" niezmierzone | `TownFletchers.cs:403-459` [K], rachunek [S] | **P.** Oferta za kazdy ladunek az do dzisiejszych rak strzelarzy: platnerz czeka, gdy po starcie zostaloby mniej rudy niz `ceil(rece / dni na snop x ruda na snop - dlug)` (bezpiecznik 5), a jego zysk na ladunek jest mniejszy niz oferta. "Czeka na strzelarzy" poza "brak rudy" i `_miss` linii nazw (sygnal zamowienia zostaje - miastu brakuje rudy dla obu). Przewidywanie: 18-26 miast bez strzal. Linia 174b.0 wypisuje czesc wspolna "bez rudy" i "bez strzal". |
| 23 | drobna | Pojedynczy d40 za szumny; P2 na zapisie z gory nieosiagalny | logi [P] | **P.** P2, P3 - srednia d31-40; zapis: trend (sr. d6-8 <= 0.8 x sr. d1-2) i ruda dostarczona kontraktem do >= polowy miast z pierwszej listy "bez rudy" (linia nazw ma liste dostaw wedlug miasta). |
| 24 | drobna | P6 z jednego spisu d40 ma 1 pp zapasu | logi [P] | **P.** P6: srednia spisow d30/35/40, prog sklad6 - 1.5 pp (partie >= 70.2%, razem >= 78.3%). |
| 25 | drobna | Ciaglosc zlomu po wczytaniu sprawdzana tylko "jesli autotest umie" | [K] | **P.** Probny odczyt przy kazdym zapisie (ten sam parser, porownanie par, dob, sum) - linia "probny odczyt zgodny / ROZJAZD"; autozapis gry wola to w obu biegach (P10). |
| 26 | drobna | Karawany rodu gracza nigdy nie dostaja kontraktu - jedna regula nie zastosowana | `MaterialOrders.cs:306` [K] | **P.** Pytanie 2 do Jeffa; domyslnie rekomendacja TAK (`TownMaterialOrderPlayerCaravans`): karawany rodu gracza jak karawany AI (BK nie ma dla nich osobnej logiki, `BKCaravansBehavior.AddDialogs` pusty), druzyna gracza nigdy. |

**Co sie zmienilo w projekcie (poza tabela):** rozdz. 0 pkt 5 i rozdz. 2 - rezerwa nazwana wyjatkiem; 3.2 - przyczyna 2 poprawiona, punkt zamowienia, `SourceKeep`; 3.3 - oferta za kazdy ladunek; 3.4 - przeslanka kolejnosci sluchaczy, P5; 3.5 - samokontrola probkowana, P13; 3.6 - `_lastDay`, probny odczyt; rozdz. 4 i 6 - przepisane; rozdz. 8 - pytania 1 i 2; rozdz. 9 - nowe pozycje.

**Wykonanie (09.10, ta sama sesja):** galaz `w-toku/174b-dowoz`, commity lokalne (od `0c41eee`): `17365c0` 174b.0, `c5b21c9` 174b.6, `7990590` 174b.5, `88a3ba0` 174b.1, `776d19f` 174b.2, `3539303` 174b.3, `d6b5bcf` 174b.4, `6c481c3` skrypty progow, `13f4c27` poprawka ilosci kontraktu (wszystkie stosy). Build Release kod 0 (1 stare ostrzezenie `BattlefieldLaw`), gen_mcm: 769 ustawien Armoury. NIE wgrane, gra nie uruchamiana, bez pushu. Nowe klucze MCM (Jeff ich nie ma w `Armoury.json` - dzialaja domyslne): `ShelfIndexEnabled`, `ShelfIndexSelfCheckDays`, `TownMaterialOrderBySea`, `TownMaterialOrderSeaMaxRoute`, `TownMaterialOrderFromPacks`, `TownMaterialOrderAhead`, `TownMaterialOrderPlayerCaravans`, `FletchersBidForOre`, `ShopKeepsLastArmour`, `ShopKeepPieces`. Nowy klucz zapisu: `arm_scrap` (SaveText.Sync). Nowe pliki: `Measure174b.cs`, `ShelfIndex.cs`, `Cost174.cs`, `ShopReserve.cs`, `tools/p174b_progi.py`, `tools/p174b_doby.py`, `tools/p174b_koszty.py`.
