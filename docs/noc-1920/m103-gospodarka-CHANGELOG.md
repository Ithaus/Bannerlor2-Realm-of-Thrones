## 2026-10-21 (m103-gospodarka: P0, P1, P6, P7a, P7b z docs/PLAN-GOSPODARKA-2026-10-21.md)

**Mod:** Armoury (+ `tools/sprawdz_logi.py`) | **Galaz:** `w-toku/m103-gospodarka` (od `ed9273fa` = WGRANIE 101) | **Status:** DO SPRAWDZENIA (nic nie wgrane; test w grze dopiero w jednym finalnym autotescie po scaleniu)

Kazdy krok to osobny commit. Wpisy ponizej w kolejnosci krokow.

---

### P0. Blad SideVillageVisit.Today przy starcie nowej kampanii (bez klucza MCM - poprawka bledu, liczby gry bez zmian)

**Problem (log):** CrashScribe `session-2026-10-21_07-17-34.log` linia 800, 07:18:26: `# ERROR IN A MOD` / `WHERE : [Armoury] Armoury.SideVillageVisit.Today` (NullReferenceException), stos ArmouryBehavior..ctor -> M97WioskiReset -> SideVillageVisit.Reset -> RaidReset -> ProtSync -> Today0 -> Today. Podsumowanie sesji (linia 7184): `1 x ERROR IN A MOD|[Armoury] Armoury.SideVillageVisit.Today`. Ten sam blok w kazdej nowej kampanii od m97-wioski (20-10_20-17, 20-10_21-49, 21-02-12, 21-04-09, 21-06-23 prolog, 21-07-17); w biegach z zapisu 0. Psuje progi etap4 T1 i etap8 E8 T0; gracz widzi czerwona notke CrashScribe na starcie kampanii z prologiem.

**Przyczyna:** `Armoury/src/SideVillageVisit.Raid.cs:77` (stary kod) - `ProtSync()` zawsze czytal zegar, takze zaraz po `_prot.Clear()` w `RaidReset` (:72). `Today0` (:82) sprawdzal tylko `Campaign.Current != null`. Dekompilacja `TaleWorlds.CampaignSystem.Campaign`: `SetLoadingParameters` (Current = this; GameStarted = true tylko dla SavedCampaign), `OnInitialize` wola `GameManager.OnGameStart` (konstruktor ArmouryBehavior) PRZED `MapTimeTracker = new MapTimeTracker(...)` (tylko nowa kampania); `GameStarted = true` dla nowej gry dopiero w pierwszym `RealTick`. `CampaignTime.Now` = `Campaign.Current.MapTimeTracker.Now` -> NRE. Finalizator CrashScribe na kazdej metodzie Armoury lapal wyjatek w `Today()` przed `catch` w `Today0`.

**Zmiana:** `SideVillageVisit.Raid.cs` `ProtSync`: zegar czytany tylko przy `_prot.Count > 0`. `Today0`: `var c = Campaign.Current; return c != null && c.GameStarted ? Today() : 0;` (wzor `Live` z `A17Fix.cs:77`). 0 = "dzien nieznany", jak dotad po wyjatku.

**Inne drogi do Today() przy starcie/zmianie kampanii (sprawdzone w kodzie):**
- `Reset()` (SideVillageVisit.cs:77) -> `DayZero` -> `RaidDayZero` -> `DefDayZero` -> `DefPrune` (Defenders.cs:142) -> `LackNow`/`Fallen` -> `Today0` - slowniki `_lack`/`_fallen` sa tu jeszcze z poprzedniej kampanii (czyszczone dopiero w RaidReset -> DefReset). Przy zmianie kampanii w jednym procesie to byla uspiona druga droga NRE - teraz `Today0` daje 0 bez wyjatku (nowy obiekt Campaign ma GameStarted = false).
- `RaidReset` -> `ProtSync` (pusta lista - zegar nieczytany) -> `DefReset` (czysci slowniki, potem DefPrune po pustych).
- `Import` (wczytanie, ArmouryBehavior.cs:1371) -> `RaidReset` (jak wyzej); sam Import zegara nie czyta (migawka bez zdejmowania wygaslych).
- `SideVillageFief.Reset` (SideVillageFief.cs:86) i `SideVillageHome.Reset` (SideVillageHome.cs:70) - zegara nie czytaja; `SideVillageFief.Today()` (:110) wolany tylko z taktow/menu/Import (Import: zegar z zapisu, GameStarted = true).
- Pozostali wolajacy `Today0` (`Spared`, `PaidShare`, `Fallen`, `LackNow`, `AddFallen`, `AddLack`) dzialaja w trakcie gry (GameStarted = true). W oknie tworzenia nowej kampanii listy ochron, poleglych i braku rak sa puste - wynik bez zmian.

**Ryzyko / co sprawdzic:** minimalne. Gdyby ktos kiedys wolal `Today0` w `OnNewGameCreated` i potrzebowal prawdziwej daty, dostanie 0 (semantyka opisana w ProtSync/Fallen/LackNow). Przy scalaniu z galeziami ruszajacymi `SideVillageVisit.Raid.cs` sprawdzic diff `ProtSync`/`Today0` (pulapka scal.sh: latka w cudzym if). Anomalia 98nowa (brak bloku przy tym samym kodzie) bez wyjasnienia - poprawka usuwa sam wyjatek, wiec od niej nie zalezy.

**Oczekiwane progi (nowa kampania):** CrashScribe `grep -c SideVillageVisit` = 0, `# ERROR IN A MOD` = 0; etap4 T1 = TAK (bloki 0, powtorki 0); etap8 E8 T0 = TAK (0 i 0); m97wioski WP-1/WP-2 i m97obroncy OB-1 dalej TAK. Bieg z zapisu: dalej 0 blokow.

**Build:** Armoury kod 0 (6 ostrzezen jak dotad).

---

### P1. Narzedzie progow: bledy pomiaru W12-4, W12-6, GL2-8, VR-3 (bez klucza MCM - gra bez zmian poza napisami logu)

**Problem (logi 101, narzedzie przed zmiana):**
- W12-4 (dziesiecina wsi wobec utargu, sr28): 101-nowa (07-17-36) NIE "0.00% (nalezne 0, utarg 9225110)"; 101-prolog (06-23-39) NIE "0.00% (nalezne 0, utarg 8723400)". Tymczasem linia "Daniny wsi (DS)" d109015: "oddane 239217 ... dziesiecina 11965" = 5.0017%.
- W12-6 (oferta nadwyzki / lokata W): 101-nowa NIE 0.018 (oferta 183704/d, lokata 10269398), prolog NIE 0.018, prog ">= 0.15 - 0.002" z linii stanu. Linia doby: "oferta nadwyzki na jutro 199025 (tempo 0.15/d ponad zapas) (P3: oferta W 1/60 ...)".
- GL2-8 (dni oszczednosci grupy m_t > 1.5, d91-120): 101-nowa NIE "min 27 (srednio 32)", prolog NIE "min 15 (srednio 17)", prog ">= 60" (narzedzie samo pisalo "prog nieosiagalny", baza W43 bez reguly 51).
- VR-3 (audyt premii drogi Valyrii): w 101 TAK (25.00 / 25.00 / 24.97% w 100-160 - jedna probka 0.00 w d109083); w 98-160 (00-07-42, log juz zrotowany) NIE 23.39%: 62 probki po 1.01% tej samej partii przez ok. 25 dob.

**Przyczyna:**
- W12-4: przy DS dziesiecine wsi bierze woz (`DsVillage.cs:361` -> `TownFolk.TitheVillageAtCart`), a `VillageTithe` odejmuje ten utarg (`TownFolk.Tithe.cs:158`, `vh -= DsTiSeen`), wiec "Dziesiecina (E5/C3)" ma zawsze "wsie 0 (0 wsi; nalezne 0)"; narzedzie (`sprawdz_logi.py` etap5_wsie) czytalo "nalezne" tylko z tej linii.
- W12-6: `TownFolk.Luxury.cs:353-361` (LadderOffers, P3) nadpisuje oferte: W = nadwyzka / FolkSurplusSpendDays (60), R i P = 0.3 x nadwyzka / 60. Udzial 0.15 (`SpendRate`) to przy P3 juz tylko place R/P, a linie logu (`TownFolk.Live.cs:1930` linia stanu, `:1991` linia doby) drukowaly "tempo 0.15" i z tego prog.
- GL2-8: zapas ludu W 10 / R 21 / P 30 dni (`TownFolk.cs:54`), nadwyzke ponad niego zdejmuja z reguly danina P1, kiesa2, oferta P3 1/60 i place R/P (`TownFolk.Live.cs` krok 5) - prog 60 dni projektu [R] jest sprzeczny z regulami.
- VR-3: `MapRoadKinds.Audit` (`MapRoadKinds.cs:327-357`) bral co dobe pierwsze 6 partii z `MobileParty.All` (te same partie dzien w dzien) i liczyl do sredniej takze partie z wynikiem bez premii na podlodze gry (LimitMin 1.0 - premia ginie w modelu gry); audyt traktow DR robi to dobrze (`MapRoads.cs` AuditList: losowanie i "na podlodze LimitMin (poza srednia)").

**Zmiana:**
- `tools/sprawdz_logi.py` (male bloki, nic nie przestawione):
  - W12-4: do "nalezne" i "zaplacone" doliczona "dziesiecina N" z linii "Daniny wsi (DS)" z tych samych dob okna; w wyniku "w tym przy wozie DS N". Podstawa bez zmian ("oddane" z "Utarg wsi (112)").
  - W12-6: gdy linia doby "Kiesa ludu" ma "P3: oferta W 1/N" - prog = 1/N (tolerancja 2% w dol; R/P na wierzch); bez P3 - stary prog z tempa linii stanu. Odczyt tempa z linii stanu zna tez nowy napis ("place R/P X/d").
  - GL2-8 - **ZMIANA PROGU (do powiedzenia Jeffowi):** dotad ">= 60 dni" (projekt [R]); teraz "dni oszczednosci grupy m_t > 1.5 >= zapas dni wazony potrzeba klas (W TownFolkReserveDays / R 21 / P 30) i GL2-8b srednio >= 95%". Wagi = potrzeby klas z linii doby "Kiesa ludu" (kiesa / zapas dni) tej samej doby co tabela GL2 albo wczesniejszej - to potrzeby wszystkich miast, nie tylko grupy m_t > 1.5 (przyblizenie; tabela GL2 nie dzieli grupy na klasy). Stara sciezka (prog 60) zostaje, gdy w logu brak linii "Kiesa ludu". `SkupDaysGoal = 60` (`TownFolk.Skup.cs:73`, licznik "ponizej 60" w linii Kiesa ludu) NIE zmieniony - regex grupy (sprawdz_logi ok. :21919) czyta "ponizej 60", zmiana napisu zepsulaby inny prog.
  - VR-3: nowy regex `M103_VALYRIA_LOW` czyta dopisek audytu (nizej); srednia progu bez probek "na podlodze predkosci gry", nowy wiersz VR-3b (INFO): ile probek < 5%, z tego na podlodze / sufit kolumny / inne, przyklad z id partii. Log bez dopisku - wynik jak dotad.
- Gra (tylko napisy logu, liczby bez zmian):
  - `TownFolk.Live.cs` linia stanu (StateLine, ok. :1930): przy kluczu FolkLuxuryList "tempo W 1/60 (P3), place R/P 0.15/d" zamiast "tempo nadwyzki 0.15/d"; linia doby "Kiesa ludu" (ok. :1991): przy P3 "(tempo W 1/60 (P3), place R/P 0.15/d ponad zapas)", a dopisek "(P3: oferta W 1/N, R i P 0.3/N - linia Zbytki)" liczony z `LxDays` i `SurpWage` (dotad stale "1/60" w tekscie).
  - `MapRoadKinds.Audit`: start listy przesuwany z doba (`(doba x 7919) mod liczba partii`), dopisek za "oczekiwane X%": ", ponizej 5%: K (na podlodze predkosci gry F, sr. X%; sufit kolumny C; inne O) [id b% powod (bez premii r0, z premia r1); ... do 3]". Srednia w linii gry dalej ze wszystkich probek (regex VR-1/VR-3 bez zmian). `MapRoads.cs`: nowe przeciazenie `ProbeFor(..., out lmin, out lmax)` (stare bez zmian).

**Wyniki przed / po (te same logi, bez nowego biegu):**

| Prog | 101-nowa (07-17-36) | 101-prolog (06-23-39) | 100-160 (04-57-25, `--start-kampanii 109116`) |
|---|---|---|---|
| W12-4 | NIE 0.00% -> **TAK 5.00%** (nalezne 461324 przy wozie DS, utarg 9225110) | NIE 0.00% -> **TAK 5.00%** (436214 / 8723400) | TAK 5.00% (359220 / 7183611) |
| W12-6 | NIE 0.018 -> **TAK 0.0179** (prog 1/60 = 0.0167 -2%) | NIE 0.018 -> **TAK 0.0179** | TAK 0.0198 |
| GL2-8 | NIE (min 27, prog 60) -> **TAK** (min 27, sr. 32; zapas wazony 12.2 dni; stac 100%) | NIE -> **NIE** (min 15 >= 12.2, ale stac 88% < 95%) | NIE (min 14 < zapas wazony 20.1; stac 88%) |
| VR-3 | TAK 25.00% -> TAK 25.00% (bez dopisku w starym logu) | TAK 25.00% -> TAK | TAK 24.97% -> TAK |

Bez `--start-kampanii` bieg 100-160 ma okno d1-120 poza logiem (W12 "brak linii", GL2-8 "brak probek") - jak dotad.
VR-3 na nowym formacie sprawdzony na logu syntetycznym (linie VR z 07-17-36, w 25 dobach jedna probka 1.01% "podloga gry"): VR-1 nierozpoznane 0, VR-3 TAK "poza srednia na podlodze 25; premia sr. bez podlogi 25.00% (ze wszystkimi 24.44%)", VR-3b INFO "na podlodze 25 (sr. 1.01%)".

**Ryzyko / co sprawdzic:**
- GL2-8 to zmiana progu, nie gry. Prolog i stan z KS dalej NIE przez GL2-8b (lud stac na 88% koszyka) - to prawdziwy sygnal (osobny temat KS/ZC4, plan "Uwaga: z KS jest gorzej"), nie blad pomiaru.
- W12-6: niekupione 60% oferty P3 i kiesy P rosnace do 283 dni zostaja tematem NAKUM (narzedzie tego nie zamyka).
- Napisy: linia stanu zmienia tekst segmentu "decyzja 12.10" - W12-0 (INFO) pokazuje nowy tekst; odczyt tempa ma fallback. Inne grupy nie czytaja "tempo nadwyzki" ani "(tempo X/d ponad zapas)" (grep narzedzia). Dopisek VR jest za "oczekiwane X%" i bez " | " (regex dnia VR `audit .*? \| siec AI drog`).
- Start audytu przesuwany z doba zmienia, ktore partie trafiaja do probek (tylko miara). Koszt: ta sama liczba partii przegladanych (do pelnej listy, gdy malo partii na drogach).
- Scalanie: m103-drobne zmienia `sprawdz_logi.py` (m98plotki KP-1) w innym miejscu pliku.

**Oczekiwane progi (nowy bieg):** W12-4 5.00% TAK; W12-6 ok. 0.018 TAK przy progu 0.0167; GL2-8 TAK w nowej kampanii bez KS (27-32 dni wobec ok. 12 dni zapasu, stac 100%); VR-3 TAK 25.00 +-1.5 bez probek z podlogi, VR-3b INFO, "inne" ok. 0; VR-1 nierozpoznane 0.

**Build:** Armoury kod 0.
