## m103-drobne - piec drobnych zmian (punkt 7 kolejki Jeffa, 21.10)

**Mod:** Armoury (+ tools/sprawdz_logi.py) | **Galaz:** `w-toku/m103-drobne` (od `ed9273fa` = WGRANIE 101) | **Tor:** S (bez gry - nie uruchamiano gry, nic nie wgrane;
build kod 0, gen_mcm po kazdej zmianie Settings.cs) | **Status calosci:** DO SPRAWDZENIA (zbudowane, nie wgrane, nie testowane w grze).

| Krok | Zmiana | Klucz MCM | Commit | Stan |
|---|---|---|---|---|
| 1 | Rabunek druzyny gracza liczy sie w ksiedze snu jak praca (nie odpoczynek) | `RaidCountsAsWork` (TAK) | `3360ba69` | zbudowane |
| 2 | Rzad markietanow: odziez dla druzyny gracza z polki miasta (z kiesy gracza) i praczki (zuzycie odziezy -25% przez 7 dob) | `SutlerRowClothing`, `SutlerLaundry` (TAK), `SutlerLaundryCutPercent` 25, `SutlerLaundryDays` 7, `SutlerLaundryMenPerWasher` 20 | `e31227d0` | zbudowane |
| 3 | Zapis tablicy ogloszen arm_board w formacie nb2 (ok. 53% nb1 w modelu; odczyt nb1 bez zmian) | - (format zapisu, bez klucza) | `ea848ab0`, `19f6fcff` | zbudowane, test w obie strony OK |
| 4 | KP-1 (m98plotki) liczy bledy zapisu kufli `ERROR in SyncData.arm_drink` (i arm_rumours), KP-1b z logu CrashScribe | - (narzedzie) | `4c9bb421` | sprawdzone na logach 101 |
| 5 | `DrillCampOnlyAi` domyslnie TAK (u Jeffa juz true) | `DrillCampOnlyAi` (TAK) | `0fa1695d` | zbudowane |

---

### Krok 1. Rabunek jako praca (ksiega snu gracza)

**Po ludzku (dla Jeffa):** dotad, gdy Twoja druzyna rabowala wioske (wioske poboczna z jej menu albo wies na mapie), stala w miejscu - a ksiega snu
liczyla kazda taka godzine jak postoj, czyli odpoczynek. Rabunek dawal wiec sen. Teraz godzina rabunku liczy sie jak cwiczenia "Train the men": to praca,
ludzie nie odpoczywaja. Kto rabuje cala noc, rano ma dlug snu, tak samo jak po nocnym marszu. Dzien rabowania wioski pobocznej nie jest tez dniem
odpoczynku armii, ktora prowadzisz (rabunek wsi gry juz nim nie byl). Klucz w MCM: **Raid Counts As Work** (grupa "A night's rest"), wylaczony = jak dotad.

**Problem:** propozycja z kolejki Jeffa (pkt 7, bez sprzeciwu): godzina rabunku wioski liczy sie w ksiedze snu jak postoj. Recenzja styku 97 (n26) dodala
`raidNow = SideVillageVisit.RaidActive` w 4 warunkach `NightRest.OnHourly` (namiot automatyczny, pomijanie pytania ZB-6, pytanie "Night Falls", licznik
marszu nocnego i ostrzezenie), ale nie w samym dodawaniu odpoczynku.

**Przyczyna (kod):** `NightRest.OnHourly`: `resting = Drill.RestHour(mp, step) || ...` - `Drill.RestHour` daje true przy kroku <= 0.35 jedn. (`Drill.RestStep`),
a w rabunku partia stoi (krok 0). Odpoczynek godziny (`_restTonight += noc 1 / dzien DayRestFactor`, `InnDrink.RestHour` - odsypianie kufli) wylaczaly tylko
`forced` (marsz forsowny) i `drilled` (`CampDrill.DrilledLastHour`, recenzja 4 m97-szkolenie). Rabunek:
- wioska poboczna (m97-wioski, `SideVillageVisit.Raid.cs`): menu oczekiwania `arm_sidevil_raid`, bez MapEvent, stan `_raid != null` (`RaidActive`);
- wies gry (vanilla + `DistrictRaid` E7/R1-R2): zdarzenie `MapEvent.IsRaid`, menu `raiding_village`; gracz po stronie napastnika - sam, jako wodz armii albo
  doczepiony do armii AI (dekompilacja `MobileParty` 1.4.8: doczepiona partia dostaje `MapEventSide` wodza - `Party.MapEventSide = _attachedTo.Party.MapEventSide`).
  Te same 4 warunki wylaczaly go juz przez `mp.MapEvent == null`; odpoczynek - nie.

**Zmiana:**
- `NightRest.PlayerRaidingNow(out side)`: druzyna gracza rabuje teraz (`SideVillageVisit.RaidActive` albo `mp.MapEvent.IsRaid && mp.MapEventSide == me.AttackerSide`).
  Sam odczyt, bez alokacji.
- `NightRest.OnTickCore` (klatka, przy kluczu): klatka z rabunkiem zapisuje chwile `_lastRaidAt` (jak `CampDrill._lastDrillAt`, bez zapisu) - rabunek skonczony w
  srodku godziny i tak czyni ja godzina pracy (ta sama zasada co cwiczenia z menu: jakakolwiek praca w minionej godzinie = godzina pracy).
- `NightRest.OnHourly`: `raided = klucz && !sluzba ROT && (PlayerRaidingNow || RaidedLastHour)`; `work = drilled || raided`; odpoczynek godziny i odsypianie kufli
  tylko przy `resting && !forced && !work`. Sluzba ROT bez zmian (o snie decyduje lord - `resting` jak dotad). `bool enl = RotEnlisted()` przeniesione wyzej
  (ten sam jeden odczyt na tick, czyta go teraz takze rabunek). 4 warunki z `raidNow` (namiot, ZB-6, pytanie, marsz) bez zmian.
- `ArmyRestDay.One`: doba armii gracza z godzina rabunku wioski pobocznej (bez MapEvent) liczy sie jak doba z zajeciem (`BusyH`) - tak jak rabunek wsi gry
  (`Busy`: `lead.MapEvent != null`). Bez tego dzien rabunku wioski pobocznej dawal armii gracza premie dnia odpoczynku (spojnosc +2, morale +5). Ta sama zasada,
  ten sam klucz (`NightRest.RaidWorkHour()`).
- Reset `_lastRaidAt`, licznika i dnia linii w `ResetWorld` (nowa kampania / wczytanie - rabunek z poprzedniej kampanii nie jest praca tej godziny).
- Klucz `RaidCountsAsWork` (domyslnie TAK) w `Settings.cs` -> `McmSettings.cs` (gen_mcm), grupa "A night's rest". W Armoury.json Jeffa go nie ma - wchodzi domyslna.

**Linie logu:**
- `Nocleg: gracz - godzina H:00 rabunku (wioska poboczna | wies gry, gracz po stronie napastnika) liczona jak praca - bez odpoczynku X h (Raid Counts As Work); odpoczynek doby Y h, dlug D - kolejne godziny rabunku tej doby w linii switu.` - raz na dobe (od switu gracza), przy pierwszej godzinie rabunku, ktora bez zmiany bylaby odpoczynkiem.
- Linia `NocnyMarsz: gracz o swicie - ...` - nowe pole `godziny rabunku bez odpoczynku (Raid Counts As Work) N` (przed "; komunikat:"; regex NO_PLAYER/NO_PMARCH w sprawdz_logi czyta poczatek linii i inne pola - bez zmian).
- sprawdz_logi grupa `m97wioski`: nowy prog **WP-7** (suma godzin w liniach switu >= liczba linii doby - 1; log sprzed zmiany - INFO).

**Ryzyko / co sprawdzic (kontrola calosci):**
- Regresje: zmiana tylko ZABIERA odpoczynek godzinom rabunku druzyny gracza; klucz wylaczony = dokladnie stara sciezka (`raided` false, `RaidWorkHour` false).
  Cwiczenia (`drilled`), marsz forsowny, sen w menu (`arm_sleep_wait` - w rabunku niedostepny), karczmy, stamina kowalska (menu rabunku i tak poza `InCampMenu` -
  stawka marszu, jak cwiczenia) - bez zmian.
- Wszystkie miejsca czytajace stan rabunku gracza: `NightRest.OnHourly` (4 warunki `raidNow` - bez zmian; odpoczynek i kufle - nowe `work`), `ArmyRestDay.One`
  (nowe), `SideVillageVisit` (sam stan). `Drill.Observe` (maska postoju musztry) liczy godzine rabunku jako postoj, ale dla gracza godziny cwiczen to tylko
  `CampDrill.PlayerOwed` (menu), a `CampDrill.AutoHour` (oboz oblezenia / cudza armia) konczy sie przy `mp.MapEvent != null` - rabunek wsi gry w cudzej armii nie
  daje cwiczen; rabunek wioski pobocznej w cudzej armii jest zablokowany (`SideVillageVisit`: "You march in another lord's army - he decides.").
- Kolizje: `ForcedMarch.Hour` liczy sie przed `work` (godzina marszu forsownego i rabunku naraz - i tak bez odpoczynku). Tick nadrabiany (NightClock.CatchUp)
  powtarza `resting` poprzedniej godziny, a `raided` jest z czasu (jak `drilled`) - spojne.
- Swiadomie BEZ zmian (do decyzji, jesli Jeff chce): ksiega snu AI (`NightMarch.AiHourly`) - partie AI, takze doczepione do armii gracza, rabujac licza postoj
  jako odpoczynek, jak dotad. Zadanie dotyczylo druzyny gracza; jedna zasada dla AI to osobna zmiana (rabunki AI sa czeste - wplyw na dlug snu lordow).
- Koszt: klatka - jedno pole statyczne i dwa odczyty pol `MapEvent` (bez alokacji); tick godzinowy - to samo raz.
- Autotest: rabunek wioski pobocznej albo wsi gry przez gracza noca -> linia "godzina ... rabunku ... liczona jak praca", w linii switu pole > 0, odpoczynek doby
  nie rosnie w godzinach rabunku; bez rabunku pole 0. Klucz NIE -> brak linii, pole 0.

**Status:** DO SPRAWDZENIA.

---

### Krok 2. Markietani - odziez i praczki (rzad markietanow)

**Po ludzku (dla Jeffa):** w rzedzie markietanow ("Visit the sutlers' row", duzy oboz) sa dwie nowe opcje.
**Clothe the men from the sutlers' carts** - Twoi ludzie zdzieraja buty, ubrania i plotno (skora, sukno, plotno sa w modzie od dawna). Mozesz ich odziac od reki
z Twojej kiesy: towar z targu miasta, z ktorym handluja markietani, po cenie miasta plus 30% za dowoz, tylko tyle calych sztuk, ile ludzie naprawde zdarli. Ich
sakiewka zostaje nietknieta, a w nastepnym miescie nie kupia tego drugi raz. Uczciwie: markietani i tak raz na dobe przywoza Twoim ludziom odziez za pieniadze
z ich sakiewki (od dawna, T3-K1) - nowa opcja przydaje sie, gdy sakiewka ludzi jest chuda albo gdy chcesz zaplacic sam; czesto bedzie szara z napisem "nic jeszcze
nie zdarli". **Hire the washerwomen for 7 days** - praczki piora i ceruja: przez 7 dni Twoja druzyna zdziera odziez o 25% wolniej. Placisz dniowke praczek
(1 praczka na 20 ludzi, 2 denary dziennie): druzyna 150 ludzi - 8 praczek x 7 dni x 2 = 112 denarow; oszczedza ok. 0.5 sztuki odziezy (przy dzisiejszych cenach
ok. 220 denarow za sztuke - ok. 120 denarow). Pieniadze ida do miasta i wsi jak za kazda usluge rzedu.

**Problem:** kolejka Jeffa pkt 7. Poprzednia recenzja (m99) odrzucila praczki, bo "pranie nie ma w modzie zadnego stanu". Zadanie: sprawdzic obieg odziezy
wojska i (a) dac w rzedzie usluge odziezy z polek miasta-zrodla (jak sklep: polka + narzut, pieniadz do kasy miasta/wsi), (b) praczki - TYLKO, jesli ArmyClothing
ma stan zuzycia na druzyne, ktory mozna uczciwie zmniejszyc.

**Przyczyna / stan (kod):** `ArmyClothing.cs` (paczka 150) MA stan zuzycia na druzyne: `_need[StringId partii]` = ulamkowe sztuki skory, sukna i plotna
czekajace na zakup, narastajace w `OnDailyTickParty` (`Accrue(id, ludzie bez bohaterow, field, share)`, stawki kg na czlowieka na rok: w polu 2/3/2.5 -
log 101: na 1000 ludzi dziennie skora 0.55, sukno 0.82, plotno 0.69 sztuki), zapisywane w zapisie (Export/Import). Zakup: przy wyjezdzie z miasta (`MenPurse.OnLeft`
-> `BuyForParty`) i raz na dobe u markietanow w polu (T3-K1, `Sutlers.One -> FromSutlers`, TAKZE druzyna gracza) - w obu z sakiewki ludzi po cenie miasta, sztuki
znikaja z polki (zdarte ubranie zastapione), zloto do kasy miasta. `Accrue` ma juz parametr `share` (czesc doby zuzycia - uzywany dla garnizonow), wiec praczki
moga uczciwie zmniejszyc narastanie potrzeby tylko druzyny gracza. Ceny z logu 101 (ostatnia linia: markietani kupili 205 sztuk za 45 930) - ok. 224 d za sztuke.

**Zmiana:**
- (a) `CampSutlers` 4. ODZIEZ - opcja "Clothe the men from the sutlers' carts (N pieces, about X denars)" (klucz `SutlerRowClothing`, wymaga `ArmyClothingEnabled`):
  `ArmyClothing.RowBuy` - cale sztuki potrzeby druzyny gracza z polki miasta-zrodla (`Sutlers.NearestTown`, jak cala reszta rzedu), ta sama kolejnosc i ta sama
  najtansza sztuka co `ArmyClothing.Buy` (skora, sukno, plotno po jednej, cena targu od nowa przed kazda sztuka), cena markietanow = ceil(cena miasta x 1.3),
  najwyzej `SutlerRowStockPerStall x kramy` sztuk kazdego towaru na dobe (ten sam licznik dnia `_boughtToday` co sklep - skora/filc/plotno nie sa w towarze sklepu)
  i najwyzej kiesa gracza. Zaplata: gracz -> `paidRow`; towar po cenie miasta `paidTown` do kasy miasta (`ArmyClothing.RowSettle`: `ChangeGold`, ksiega
  przeplywow `NCloth`, sprzedaz kupcow `MixCloth` - jak zakup partii, bez tarczy zoldu, bo placi kiesa gracza); narzut `paidRow - paidTown` obiegiem uslug rzedu
  (`Spread`: 85% miasto z hakiem 75/25, reszta wies z danina pana i udzialem korony DS). Potrzeba druzyny maleje o kupione sztuki. Zaplata nieudana -> `RowUndo`
  (sztuki wracaja na polke, potrzeba wraca). Ramka ksiegi towarow `FArmyCloth` jak przy `Buy`.
- (b) `CampSutlers` 5. PRACZKI - opcja "Hire the washerwomen for 7 days (X denars)" (klucz `SutlerLaundry`, wymaga `ArmyClothingEnabled`): oplata = praczki
  (ceil(ludzie druzyny bez bohaterow / `SutlerLaundryMenPerWasher` 20)) x `SutlerLaundryDays` (7) x dniowka praczki (2/3 `WorkshopWagePerDay` = 2 d) obiegiem uslug;
  przez 7 x 24 h `ArmyClothing.OnDailyTickParty` dla druzyny gracza liczy `share = 1 - SutlerLaundryCutPercent/100` (0.75). Drugi raz nie da sie najac, dopoki pracuja.
  Termin (`_laundryUntil`, godzina kampanii) - pole 8 zapisu `arm_sutlerrow` (stary zapis bez pola = bez praczek; stary kod pole pomija). Reset w nowej kampanii.
- Liczby (decyduje Claude): 25% - pranie i cerowanie wydluza zycie lnu i welny o ok. jedna czwarta (koszule i hajdawery prane zamiast zdzierane do konca; buty
  smarowane) - mniej niz polowa, bo marsz i tak niszczy buty; 1 praczka na 20 ludzi (w armiach XVII-XVIII w. 4-6 kobiet na kompanie ok. 100 ludzi do prania - ok.
  1 na 20); dniowka praczki 2/3 dniowki rzemieslnika (praca kobiety ok. 1/2-2/3 dniowki mezczyzny). Wynik: koszt ok. 0.75 d na czlowieka na tydzien, oszczednosc
  ok. 0.8 d na czlowieka na tydzien przy cenach z biegu 101 - usluga prawie na zero, oplaca sie przy drogim suknie (sukna brakuje na polkach - log 101: "bez towaru
  na polce: sukno 172").
- Linie logu: `Markietani obozu (m99): odziez | ... potrzeba skora a, sukno b, plotno c -> ... | kupione ... | gracz zaplacil X -> towar do kasy miasta Y + narzut Z -> ... | kontrola: X = Y + Z (roznica 0)`,
  `Markietani obozu (m99): praczki | ... -> praczek W (1 na 20) x 7 dob x dniowka 2 = X | zuzycie odziezy -25% do godziny H ... | kontrola: X = X (roznica 0)`;
  linia doby rzedu - pola "odziez N: sztuk ... (towar do kas miast ... + narzut ...)" i "praczki: najete N za X, czynne ..."; linia startu sesji rzedu - klucze i liczby;
  linia doby "Odziez wojska (150)" - pola "rzad markietanow (gracz, z kiesy): ..." i "praczki: druzyna gracza zdarla mniej o ... (dob z praczkami N)".
- sprawdz_logi grupa `m99markietani`: MK-2/MK-3 licza tez "odziez" i "praczki" (duzy oboz, kontrola obiegu 0); nowe **MK-8** (potrzeba druzyny spada dokladnie o
  kupione sztuki) i **MK-9** (cena praczek = praczki x doby x dniowka, w gore). Sprawdzone na sztucznym logu (obie TAK).
- Klucze w `Settings.cs` -> `McmSettings.cs` (gen_mcm, grupa "Camp and baggage (night 20/21.10)"); suwaki w `tools/gen_mcm.py` RANGES zgodne z przycieciem w kodzie.

**Ryzyko / co sprawdzic (kontrola calosci):**
- Nic z niczego: odziez - sztuka z polki miasta znika (jak kazdy zakup odziezy wojska), gracz placi `paidRow` = towar do kasy miasta + narzut w obiegu uslug
  (kontrola w linii); praczki - cala oplata obiegiem uslug, bez towaru. Mniejsze zuzycie = mniej zakupow odziezy z sakiewki ludzi (mniej zlota do kas miast za
  sukno) - w zamian oplata praczek idzie do tego samego miasta i wsi; zloto nie powstaje.
- Kolizje: T3-K1 (markietani z sakiewki ludzi raz na dobe) i zakup przy wyjezdzie czytaja te sama potrzebe `_need` - usluga ja zmniejsza, wiec nie kupia drugi raz;
  rezerwa sakiewki na odziez (`PendingCost`) spada, wiec markietani obiegu moga wziac z sakiewki ludzi gracza troche wiecej "na zycie" tej samej doby (spojne z T3-K1).
  Limit sztuk dnia wspolny ze sklepem tylko po id (skora/filc/plotno - nie ma ich w `Sold()` sklepu). Praczki dzialaja tylko na `OnDailyTickParty` druzyny gracza
  (`mp.IsMainParty`); AI bez zmian. Garnizony bez zmian.
- Menu `arm_camp_sutlers` to ZWYKLE menu - `SwitchToMenu(Menu)` po akcji jak cyrulik/platnerz (bez menu oczekiwania). Warunek opcji liczy podglad bez zmiany polki
  (`RowPreview`, cena pierwszej sztuki - podglad moze roznic sie o kilka denarow od dokladnej kwoty w `RowBuy`, stad "about").
- Klucz wylaczony / `ArmyClothingEnabled` wylaczony - opcje ukryte (`return false`), `LaundryShare` = 1 (dokladnie stare narastanie). Praczki najete, a potem klucz
  wylaczony - zuzycie wraca do pelnego (oplata przepada - jak kazda usluga).
- Zapis: pole 8 `arm_sutlerrow` (kilka znakow); stary zapis Jeffa (101) czyta sie bez zmian.
- Autotest: oboz >= 150 ludzi, kilka dob w polu z chuda sakiewka ludzi (albo `SutlerClothing` NIE), opcja odziezy - linia "odziez" z kontrola 0 i MK-8; praczki -
  linia "praczki", przez 7 dob w linii "Odziez wojska (150)" pole "praczki: druzyna gracza zdarla mniej o ...", potem znika.

**Status:** DO SPRAWDZENIA.

---

### Krok 3. Zapis tablicy ogloszen (arm_board) - format nb2

**Po ludzku (dla Jeffa):** w grze nic sie nie zmienia. Zapis listow gonczych i "kto co zlupil" zajmowal w zapisie gry 18.5 KB (wiecej niz 16 KB, przy ktorych
zapis dzieli sie na kawalki). Teraz ten sam stan zajmuje ok. polowe (model na danych biegu 101: ok. 9.7 KB). Twoje stare zapisy wczytuja sie bez zmian;
po pierwszym zapisie gra pisze juz nowy, krotszy format. Uwaga na cofanie: DLL sprzed tej zmiany (WGRANIE 101) nie przeczyta nowego formatu - listy
goncze i pamiec szkod band zaczna sie wtedy od zera (listy wroca same w ciagu kilku dni, nic wiecej nie ginie).

**Problem:** bieg 101 (`Armoury-2026-10-21_07-17-36.log`, nowa kampania 180 dob): linia "Listy goncze (m102): dzien ..." - `zapis arm_board` do 18462 B
(ostatnia doba 18403 B), prog L-8 grupy m102tablica (<= 16384 B) - NIE. SaveText kawalkuje napis powyzej 16 KB.

**Przyczyna (skad objetosc - policzone):** format nb1: listy `miasto~banda~nazwa~ludzie~dni~doplata~kwota~doba wywieszenia~termin~flagi~S*100~rodzaj~soltys~wies~rod`
(84 czynne listy x ok. 81 B = ok. 6.8 KB - wyliczone z 84 ostatnich linii "list -" logu 101) i szkody band `banda~doba,rodzaj,miasto,wies/...` (159 band, reszta
ok. 11.5 KB = ok. 72 B na bande, ok. 2.2 wpisu). Najwiecej zajmuja: id band (95% z koncowka 10-cyfrowa, np. `looters_2147131604`, srednio 19 znakow) - w listach
i w szkodach; pelne doby (6 cyfr: 109016) w kazdym wpisie; miasto powtarzane w kazdym wpisie szkody (dla rabunku wsi wynika z wsi - `Village.Bound`, tak je zapisuje
`OnRaidCompleted`); pola, ktorych po wywieszeniu nikt nie czyta (sprawdzone grep w NoticeBoard*.cs): `Days`, `BonusPct`, `S`, `Posted` (tylko eksport), `Name` (puste).

**Zmiana:**
- Nowy format zapisu `nb2|D=doba|P=przedrostki|F=...|W=...|H=...` (`NoticeBoard.Export`, `WatchExport2`); odczyt `nb2` (`Import2`, `WatchImport2`) i - bez zmian -
  `nb1` (stare zapisy Jeffa z 101; po wczytaniu nb1 linia "wczytano zapis arm_board w starym formacie nb1 ... nastepny zapis w formacie nb2").
- Id (miasto, banda, wies, rod, soltys, flaga) przez tablice przedrostkow `P`: przedrostek do ostatniego `_` raz w naglowku, w tekscie `^nr_koncowka`
  (`sea_raiders_246` -> `^1_246`), a koncowka liczbowa od 5 cyfr bez zera na przodzie - w base36 (`looters_2147131604` -> `^0:zichck`). Tylko gdy krocej; id z
  przecinkiem, kropka, `/`, `^`, `:` - bez zmian.
- Doby jako przesuniecie wzgledem `D` (doba zapisu zapisana w naglowku - odczyt nie zalezy od zegara gry w SyncData): termin listu `Due - D`, wpis szkody `D - doba`,
  flaga `Until - D`.
- Listy bez pol nieczytanych (`nazwa`, `dni`, `doplata`, `S`, `doba wywieszenia`); po wczytaniu nb2 te pola = 0 (nikt ich nie czyta; nazwa bandy liczona po
  wczytaniu jak dotad). Soltys i wies tylko przy liscie soltysa (puste koncowe pola obciete).
- Szkody: miasto puste, gdy wynika z wsi (`Village.Bound`, odczyt leniwy `HarmTown` -> `VilTown` z pamiecia), albo gdy jest takie samo jak w poprzednim wpisie
  tej bandy bez wsi; wpisy starsze niz `HarmDays` (60 dob - HarmCount liczy 60, soltysi 30) pominiete; banda bez zywych wpisow - bez klucza.
- `HarmCount` porownuje `HarmTown(h)` (zapisane miasto albo z wsi). Stary eksport nb1 (`ExportV1`) zostal tylko do pomiaru.
- Linia doby listow: po `zapis arm_board N B;` nowy segment `sklad zapisu nb2: naglowek z przedrostkami a B, flagi b B, listy c B (n), szkody d B (band m, wpisow k),
  stary format nb1 w tym samym stanie X B (Y%)` - pomiar w tym samym stanie przy kazdej dobie (poza kosztem doby L-9). Linia "wpiete" - `zapis arm_board nb2 (odczyt takze nb1)`.
- sprawdz_logi `m102tablica`: L-8 prog `<= 12288 B` (cel m103), nowy **L-8b** (INFO: nb2 wobec nb1 w tym samym stanie). Na logu 101 L-8 = NIE (18462 B, stary format) -
  tak ma byc; po zmianie oczekiwane TAK.

**Pomiar / test:**
- Model na danych biegu 101 (`model_nb.py` w scratchpadzie: 84 listy z linii "list -" logu, szkody 159 band dopasowane do sumy nb1 = 18 256 B): nb2 = 9 670 B (53%),
  same listy 6 847 -> 3 212 B. Wariant bez wyliczania miasta z wsi: 9 693 B. Dokladny wynik w tym samym stanie - nowe pole linii doby w nastepnym biegu.
- Test w obie strony bez gry (konsola net472, refleksja na Armoury.dll z HEAD): 13 id (w tym koncowki z zerami, 19 cyfr, bez `_`, krotkie) - `Enc` -> `Dec` bez zmian;
  3 listy (kapitan, soltys z wsia, termin -1, flagi TL) i szkody (wies, inne miasto, wpis 70 dob - pominiety, banda tylko ze starym wpisem - bez klucza) -
  `WatchExport2` -> `Import2` = stan wejsciowy; potem `ExportV1` -> `Import` (stara sciezka nb1) = ten sam stan. WYNIK: OK.

**Ryzyko / co sprawdzic (kontrola calosci):**
- Regresje: odczyt nb1 nietkniety (ta sama sciezka); nb2 czyta wszystkie pola, ktore kod czyta po wywieszeniu (miasto, banda, ludzie, kwota, termin, przyjety,
  pan doplaca, rodzaj, rod pana, soltys, wies). `HarmCount` - miasto z wsi tylko dla wpisow bez miasta (nb2); wpisy nb1 i nowe wpisy maja miasto jak dotad.
- Cofniecie DLL do 101 po zapisie nb2: stara wersja pisze "zapis arm_board w nieznanej wersji (nb2) - pominiety" - listy i szkody od zera (nic poza tym).
- Koszt: eksport raz na dobe (linia) i przy zapisie; drugi eksport nb1 tylko do pomiaru (ok. tyle co dotad, poza kosztem L-9). `VilTown` - `Settlement.Find` raz na wies
  (pamiec), wolany przy eksporcie i w `HarmCount` tylko dla wpisow z miastem z wsi.
- Autotest: linia "wpiete" z `nb2`; linia doby - `zapis arm_board` < 12 288 B i pole "sklad zapisu" (procent wobec nb1); po zapisie i wczytaniu - "Listy goncze" z ta sama
  liczba czynnych listow (`czynne N`) i szkod band; wczytanie zapisu z 101 - linia "wczytano zapis arm_board w starym formacie nb1"; wczytanie zapisu nb2 - linia
  "Tablica ogloszen (m102): wczytano zapis arm_board nb2 (N B, doba zapisu D, przedrostkow P) - flagi a, listy b, szkody band c (wpisow k)" (poprawka po przegladzie wlasnym,
  ta sama liczba listow i band co w linii doby przed zapisem).

**Status:** DO SPRAWDZENIA.

---

### Krok 4. Progi KP-1 (grupa m98plotki) - bledy zapisu kufli

**Po ludzku (dla Jeffa):** tylko narzedzie do sprawdzania logow (gra bez zmian). Sprawdzian "kufli i plotek" nie zauwazal bledu przy zapisie/wczytaniu stanu kufli;
teraz go liczy, a z logiem CrashScribe liczy tez bledy, ktore Armoury polknal po cichu.

**Problem:** KP-1 (`tools/sprawdz_logi.py`, `m98plotki_main`) liczyl tylko linie z `ERROR in InnDrink.` i `ERROR in Rumours.`. Zapis stanu kufli idzie przez
`ArmouryBehavior.SyncData` (`InnDrink.Export/Import` pod kluczem `arm_drink`), a wyjatek lapie `catch (Exception e) { Log.Error("SyncData.arm_drink", e); }` -
w logu `ERROR in SyncData.arm_drink: ...` (Log.Error: "ERROR in " + where), czego KP-1 nie widzial. To samo dla ksiegi plotek (`SyncData.arm_rumours` - klasa Rumours,
w ktorej siedzi tez Rumours.Drink z m98).

**Zmiana (tylko sprawdz_logi):**
- `M98P_ERR` = `ERROR in InnDrink.`, `ERROR in Rumours.`, `ERROR in SyncData.arm_drink`, `ERROR in SyncData.arm_rumours`; KP-1 podaje tez, ile z nich to zapis/odczyt.
- Nowy **KP-1b** (z `--crashscribe <session-*.log>`): bloki `# ERROR ...` logu CrashScribe, w ktorych stos przechodzi przez `Armoury.InnDrink.` albo `Armoury.Rumours.`
  (wyjatek polkniety w naszym try wychodzi w CrashScribe jako "ERROR CAUGHT FURTHER UP" ze stosem) - prog 0; bez logu CrashScribe - INFO.
  `m98plotki_main(logp, csp=None)`, dispatch przekazuje `csp`.
- Opis grupy w pomocy i docstring zaktualizowane.

**Sprawdzone na logach biegu 101:** `python -I tools/sprawdz_logi.py --grupa m98plotki Armoury-2026-10-21_07-17-36.log --crashscribe session-2026-10-21_07-17-34.log`:
KP-1 TAK (0, w tym SyncData 0), KP-1b TAK (0 blokow), reszta bez zmian (NIE 0). Sztuczny log (linia `ERROR in SyncData.arm_drink`, linia `ERROR in InnDrink.Hourly`
i blok CrashScribe ze stosem `Armoury.InnDrink.Import`): KP-1 NIE (2, w tym SyncData 1), KP-1b NIE (1) - wzorce dzialaja.

**Ryzyko:** brak (narzedzie). Wzorzec `SyncData.arm_rumours` to szersze niz samo m98 (ksiega plotek z sw-zajazdy) - to ta sama klasa Rumours, ktorej bledy KP-1 juz liczyl.

**Status:** GOTOWE (narzedzie, sprawdzone na logach 101 i na sztucznym logu).

---

### Krok 5. DrillCampOnlyAi domyslnie TAK

**Po ludzku (dla Jeffa):** u Ciebie nic sie nie zmienia - masz to juz wlaczone w MCM. Zmienia sie tylko wartosc domyslna w kodzie, zeby po resecie ustawien
albo na nowej instalacji lordowie AI tez cwiczyli tylko na postoju za dnia (tak jak w Twojej grze).

**Problem:** kolejka Jeffa pkt 7: `DrillCampOnlyAi` w `Settings.cs` mial domyslnie `false`, a u Jeffa w `Armoury.json` jest `"DrillCampOnlyAi": true`
(sprawdzone w `Documents\Mount and Blade II Bannerlord\Configs\ModSettings\Global\Armoury\Armoury.json`; `DrillLawAi` tez true).

**Zmiana:** `Settings.cs`: `public bool DrillCampOnlyAi = true;` (z komentarzem), `python tools/gen_mcm.py` -> `McmSettings.cs` `= true` (jedyna zmiana w pliku MCM).
sprawdz_logi `m97szkolenie` SZ-5 - tekst progu INFO (klucz domyslnie TAK od m103).

**Kontrola calosci (co zaklada domyslne NIE):** grep calego repo (`*.cs`, `*.py`, `*.ps1`, `*.json`, `*.xml`): klucz czytaja tylko `CampDrill.RuleFor` (`s.DrillCampOnlyAi && s.DrillLawAi`)
i linia startu CampDrill; `Drill.cs:671` - komentarz pomiaru (mierzy obie reguly niezaleznie od klucza). `Armoury/ModuleData/Armoury.settings.xml` klucza nie ma (start procesu - domyslne z kodu).
Progi `m97szkolenie`: SZ-4 sprawdza tylko doby z "zasada AI TAK" (bez zalozenia o domyslnej), SZ-5 to INFO. Log biegu 101 (klucz true u Jeffa): "zasada AI TAK" 180/180 dob,
SZ-4 TAK (XP z marszu 0), SZ-5 INFO: XP AI x0.67 (spadek 33%) - to stan, ktory Jeff juz gra. Zadnych testow ani skryptow autotestu z tym kluczem.

**Ryzyko:** u Jeffa zero (wartosc z MCM nadpisuje domyslna); nowa instalacja / reset MCM - AI cwiczy wolniej (jak u Jeffa: ok. -33% XP AI w biegu 101).

**Status:** GOTOWE (zbudowane; zachowanie u Jeffa bez zmian).

---

### Podsumowanie paczki m103-drobne (stan HEAD)

- **Build:** Armoury, CrashScribe, GrandTourney, RealisticCaptivity, ForgeView - kod wyjscia 0, 0 bledow (Armoury pelne przebudowanie `--no-incremental`,
  `Armoury.dll` md5 `e07b8266fcf765d7b9e867e8b5c1c049`). `python tools/gen_mcm.py` - bez zmian po ostatnim kroku. Pozostale mody nie czytaja zmienionych typow (grep).
- **Ustawienia Armoury:** 2249 (bylo 2243; +6). **Nowe klucze (domyslnie):** `RaidCountsAsWork` TAK ("A night's rest"); `SutlerRowClothing` TAK, `SutlerLaundry` TAK,
  `SutlerLaundryCutPercent` 25 (0-75), `SutlerLaundryDays` 7 (1-28), `SutlerLaundryMenPerWasher` 20 (5-100) ("Camp and baggage (night 20/21.10)"). **Zmieniona domyslna:**
  `DrillCampOnlyAi` NIE -> TAK. W `Armoury.json` Jeffa nowych kluczy nie ma (wejda domyslne), `DrillCampOnlyAi` juz true.
- **Zapis:** `arm_board` - format nb2 (odczyt nb1 bez zmian; DLL 101 nie czyta nb2); `arm_sutlerrow` - pole 8 (koniec praczek; stare zapisy bez zmian).
- **Nowe linie / pola logu:** "Nocleg: gracz - godzina H:00 rabunku (...) liczona jak praca ..." (raz na dobe), pole "godziny rabunku bez odpoczynku (Raid Counts As Work) N"
  w "NocnyMarsz: gracz o swicie"; "Markietani obozu (m99): odziez | ..." i "... praczki | ...", pola "odziez" i "praczki" w linii doby rzedu i w linii startu rzedu, pola
  "rzad markietanow (gracz, z kiesy)" i "praczki: druzyna gracza zdarla mniej o ..." w "Odziez wojska (150): dzien"; "Listy goncze (m102): ... zapis arm_board N B; sklad zapisu nb2: ...",
  "Tablica ogloszen (m102): wczytano zapis arm_board nb2 / w starym formacie nb1 ...", "wpiete ... zapis arm_board nb2 (odczyt takze nb1)".
- **sprawdz_logi:** WP-7 (m97wioski), MK-2/MK-3 rozszerzone + MK-8, MK-9 (m99markietani), L-8 <= 12 KB + L-8b (m102tablica), KP-1 + KP-1b (m98plotki), tekst SZ-5 (m97szkolenie).

**Co sprawdzic w autotescie (nikt jeszcze nie uruchomil gry z ta DLL):**
1. Rabunek wioski pobocznej i wsi gry przez gracza (najlepiej noca): linia "godzina ... rabunku ... liczona jak praca", w linii switu pole > 0, odpoczynek doby nie rosnie
   w godzinach rabunku; WP-7 TAK; armia gracza z rabunkiem wioski pobocznej - doba bez premii odpoczynku (linia "Dzien odpoczynku armii (R3-08a)").
2. Rzad markietanow (oboz >= 150): opcja odziezy (po kilku dobach z chuda sakiewka ludzi albo `SutlerClothing` NIE) - linia "odziez" z kontrola 0, MK-3 i MK-8 TAK;
   praczki - linia "praczki", MK-9 TAK, przez 7 dob pole "praczki" w "Odziez wojska (150)", potem znika; zapis/wczytanie w trakcie praczek - opcja dalej szara z dniami.
3. Tablica: linia doby listow - `zapis arm_board` < 12 288 B i "sklad zapisu nb2 ... (Y% nb1)", L-8 TAK; zapis i wczytanie - linia "wczytano zapis arm_board nb2" z ta sama
   liczba listow i band; wczytanie zapisu z 101 - linia "wczytano ... w starym formacie nb1"; wyplaty listow (L-3, L-10) bez zmian.
4. KP-1/KP-1b: `--grupa m98plotki <Armoury-*.log> --crashscribe <session-*.log>` - 0 i 0.
5. m97szkolenie: "zasada AI TAK" jak w 101 (u Jeffa bez zmian).
