## m103-drobne - piec drobnych zmian (punkt 7 kolejki Jeffa, 21.10)

**Mod:** Armoury (+ tools/sprawdz_logi.py) | **Galaz:** `w-toku/m103-drobne` (od `ed9273fa` = WGRANIE 101) | **Tor:** S (bez gry - nie uruchamiano gry, nic nie wgrane;
build kod 0, gen_mcm po kazdej zmianie Settings.cs) | **Status calosci:** DO SPRAWDZENIA (zbudowane, nie wgrane, nie testowane w grze).

| Krok | Zmiana | Klucz MCM | Commit | Stan |
|---|---|---|---|---|
| 1 | Rabunek druzyny gracza liczy sie w ksiedze snu jak praca (nie odpoczynek) | `RaidCountsAsWork` (TAK) | `3360ba69` | zbudowane |
| 2 | Rzad markietanow: odziez dla druzyny gracza z polki miasta (z kiesy gracza) i praczki (zuzycie odziezy -25% przez 7 dob) | `SutlerRowClothing`, `SutlerLaundry` (TAK), `SutlerLaundryCutPercent` 25, `SutlerLaundryDays` 7, `SutlerLaundryMenPerWasher` 20 | (krok 2) | zbudowane |

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
