## m103-drobne - piec drobnych zmian (punkt 7 kolejki Jeffa, 21.10)

**Mod:** Armoury (+ tools/sprawdz_logi.py) | **Galaz:** `w-toku/m103-drobne` (od `ed9273fa` = WGRANIE 101) | **Tor:** S (bez gry - nie uruchamiano gry, nic nie wgrane;
build kod 0, gen_mcm po kazdej zmianie Settings.cs) | **Status calosci:** DO SPRAWDZENIA (zbudowane, nie wgrane, nie testowane w grze).

| Krok | Zmiana | Klucz MCM | Commit | Stan |
|---|---|---|---|---|
| 1 | Rabunek druzyny gracza liczy sie w ksiedze snu jak praca (nie odpoczynek) | `RaidCountsAsWork` (TAK) | (krok 1) | zbudowane |

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
