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

---

### P6. Piraci bez jedzenia (MB-2) - klucz `BandFoodAtSea` (MCM "Band Food At Sea", grupa Iron Bank obok Bands Eat Food; TAK; NIE = kod 101)

**Problem (log 101-nowa, `Armoury-2026-10-21_07-17-36.log`, grupa m96bandy):** MB-2 NIE "39700 / 68988 = 0.575"; MB-12 "62.6 band (na morzu), glodne 61.9 (0.989), kupily z morza 0.2". Ostatnia doba (109016): "potrzeba doby 586.8 szt., zjedzone 368 ... glodne 79 (bez jedzenia 79, ponad 2 doby 74, na morzu 79 z 82) ... morale z glodu -395". Z diagnozy: potrzeba piratow (southern/northern_pirates) 42.1% calej potrzeby band, brak u piratow = 99.3% luki; lad bez piratow 0.995. Piraci rosna bez jedzenia (2729 -> 4621 ludzi), bo na morzu nie bylo glodu dla werbunku.

**Przyczyna (kod 101):** `BandFood.cs:330` rabunek tylko `!sea`; `:324` zakup tylko z kiesa > 0 (piraci startuja z pusta - OutlawLaw.StartGoldPostfix, kryjowki brak, jedzenie startowe NavalDLC zablokowane OutlawLaw.FoodPrefix); `:169` `Starving()` = false na morzu (glodni piraci werbuja); `:362` na morzu bez odejsc. Piraci nigdy nie schodza na lad (NavalDLC PiratesCampaignBehavior.AiHourlyTick - krazenie wokol strefy patrolu).

**Zmiana (`BandFood.cs`, przy kluczu; bez klucza co do sztuki jak 101):**
- (a) Zakup z morza u pasera miasta Z PORTEM: `PortTownOf` = dom bandy (`HomeSettlement`), jesli to otwarte miasto z `HasPort`, inaczej najblizsze otwarte miasto z portem (`Town.AllTowns`); bez portu w swiecie - najblizsze miasto jak dotad. Te same zasady: wlasna kiesa, `ShelfBuy.Take` z polki, zaplata do kasy miasta, `TownFolk.Sale`, marza pasera z odleglosci.
- (b) Polow zalogi na morzu (`CrewFish`): po zjedzeniu z jukow zaloga lowi TYLKO brak dzisiejszej porcji (sztuki nalezne dzis `units - eat`; ulamek przechodzi na jutro jak dotad), najwyzej `CrewFishPerDay = 24` na dobe. Miara z dekompilacji NavalDLC `NavalFishingCampaignBehaviour.OnHourlyTickParty`: partia na morzu dostaje najwyzej 1 rybe na godzine (szansa = premia atutu Master Angler), czyli gorna granica gry 24/d - nie nowy mnoznik (pirat ok. 2.8 szt./d na bande 56 ludzi). Ryby dopisane i zdjete w tej samej dobie bandy - nic nie zostaje w jukach, nic do zapasu, nic do pasera (zadne zloto z niczego). Zaloga, ktora zjadla cala dzisiejsza porcje, jest najedzona (na morzu lowi codziennie, zapasu nie potrzebuje) - bez tego co druga doba wypadalaby "glodna" przez ulamek.
- (c) `Starving()` na morzu jak na ladzie (przy kluczu): glodna banda na morzu nie werbuje z puli (OutlawLaw.cs:1354). Odejscia z glodu na morzu dalej NIE (`if (sea) return;` - gra tez nie zdejmuje dezerterow na morzu). Skutek: glodna na morzu zostaje tylko banda ponad limit polowu (ponad ok. 480 ludzi) - piraci 101 maja srednio 56 ludzi na bande.
- Rabunek wybrzezy z morza - NIE (osobna decyzja, plan P6 c / `BandSeaRaidCoast`).
- Linia doby m96-bandy: nowy odcinek tuz przed "| potkniecia": `| z morza: paser portu X szt. za G zl (band A, miast portowych M), polow zalogi Y szt. (band Z, ponad limit 24 szt./d K)`; bez klucza `| z morza: WYLACZONE (Band Food At Sea) - paser najblizszego miasta, bez polowu`.
- `Settings.cs` (+ `McmSettings.cs` z gen_mcm: Armoury 2244 ustawien).
- `tools/sprawdz_logi.py`: regex `M103_BANDSEA_RX`, odczyt odcinka w `m96bandy_rows` (nowe pola, regex MB-0 bez zmian - ma `.*?` przed "| potkniecia"), nowy prog **MB-13** (odcinek czytelny w kazdej dobie z kluczem; srednie paser portu i polow zalogi); NKS-5u (patrz nizej).

**Ksiega towarow - ODSTEPSTWO OD PLANU (kontrola calosci, CLAUDE.md 8.0):** plan kazal zjadac ryby w ramce FFood (`Eat`). Sprawdzilem w kodzie, ze to zepsuloby KS: grupa "jedzenie partii" (GFood) to `GoodsLedger.KsArmy` -> `KsItem.Army` -> popyt krain Planu KS (`StartCalibration.Core.cs:662` OneToOne), a ramka z osada wpisuje zuzycie na rynek (`MkC[0/1]`), ktore czyta sterowanie KS (`StartCalibration.R3.cs:397`, `cu = _c28`, cel `tgt = cu x (1 + gap) / dm`). Ok. 230 szt./d ryb piratow to ok. 6% zuzycia ryb na rynkach (KS: rynki 100-160: Westeros 1737 + Essos 2051/d) - KS podnioslby podaz ryb dla popytu, ktory zaloga sama pokrywa. Dlatego: OBIE strony w ramkach polowu (FFish) z ta sama grupa "polow zalogi bandy" - zrodlo nazwane (etykieta `polow zalogi bandy|`) i ujscie ("zjedzone od razu na morzu"), BEZ osady w ramce (rynek "bez rynku": ryba zalogi nigdy nie stoi na straganie). Skutki: GFish/KsFishBoats bez przyrostu (A17Fix.cs:229), GFood/KsArmy bez przyrostu, GTick ("zrodla bez nazwy", bramka KS (3)) bez przyrostu, sterowanie KS bez zmian. Jedyny odczyt "bez rynku" z progiem to NKS-5u (zuzycie bez rynku <= 2% rynkow) - w narzedziu NKS-5u odejmuje od ryb "bez rynku" polow zalogi z linii m96-bandy z tego samego okna (tak jak juz odejmuje "wies je swoje" i straty KS-H). Na logu 100-160 (bez odcinka) wynik grupy nks identyczny jak przed zmiana.

**Sprawdzenie bez gry:** build Armoury kod 0; gen_mcm. Narzedzie: log 101-nowa (bez odcinka) - MB-0 TAK (180 dob, 0 nierozpoznanych), MB-13 INFO "brak (log sprzed m103)"; log syntetyczny (linie m96-bandy z 07-17-36 z odcinkiem w nowym formacie, co 10. doba WYLACZONE) - MB-0 TAK (0 nierozpoznanych), MB-13 TAK (162 z kluczem, 0 nieczytelnych, 18 WYLACZONE). Grupa nks na 04-57-25: wynik identyczny ze stara wersja narzedzia.

**Ryzyko / co sprawdzic:**
- Ryby nie moga zostac w jukach: dopisanie i zdjecie sa w jednym wywolaniu `CrewFish` (wyjatek miedzy nimi = potkniecie BandFood, MB-1). Sprawdzic linie Paser: "zywnosc" z band nie rosnie przez polow (MB-9), ksiega towarow: ryby "polow zalogi bandy" zrodlo = ujscie co do sztuki.
- Najedzeni piraci: brak kary morale z glodu (dzis ok. -5 na bande na dobe), werbuja jak dotad - moga czesciej bic sie na morzu: straty karawan i lordow na morzu (K1, lup, MB-6) porownac z 101-nowa. Piraci, ktorzy dotad rosli mimo glodu (2729 -> 4621), dalej rosna (werbunek bez zmian dla najedzonych) - MB-8 do obserwacji.
- Zakup u pasera portu: piraci z kiesa (zapis z kiesami - 100-160: 13.5 band/d kupowalo z morza) kupuja w porcie domowym; miasto portowe moze stracic wiecej jedzenia z polki niz najblizsze miasto dotad (te same sztuki, inny paser). Glod miast portowych (nglod, GL2-6) porownac.
- Rabunek wsi z morza nie wchodzi - wsie portowe i NC-16b bez zmian z tej strony.
- `BanditPartyComponent`/`HomeSettlement` piratow NavalDLC to port (diagnoza); gdy nie - najblizsze miasto z portem.
- Wylacznik: klucz NIE musi dac MB-2 ok. 0.575 i odcinek "WYLACZONE" (dowod, ze wylacznik dziala) - do krotkiego biegu kontrolnego.

**Oczekiwane progi (nowa kampania 180 dob):** MB-2 z 0.575 do ok. 0.97-0.99; MB-12 glodne na morzu z 61.9/d (0.989) do ok. 0 (zostaja tylko bandy ponad 480 ludzi i doba 1); MB-3 z 0.135 do ok. 0; "morale z glodu" z ok. -310..-395/d do ok. 0; MB-13 TAK, polow zalogi ok. 150-230 szt./d (potrzeba piratow, rosnie z liczba piratow); MB-0 TAK (0 nierozpoznanych); MB-1 potkniecia 0; NKS-5u bez nowego NIE; bramka KS (3) bez przyrostu.

**Build:** Armoury kod 0; gen_mcm Armoury 2244 ustawien.

**Poprawka po samoprzegladzie (osobny commit):** licznik "bez jedzenia" linii doby liczyl bande ponad limit polowu, ktora zjadla ryby, jako bez jedzenia (`eat + have <= 0`) - teraz `eat + fish + have <= 0`. Tylko liczba linii (MB-3 "bez jedzenia").

---

### P7a. Stocznia bierze drewno tylko ponad zapas rzemiosla (R1-10b) - klucz `ShipyardWoodAboveCraftStock` (MCM "Shipyard Wood Above Craft Stock", grupa "Old loose ends (A17)"; TAK; NIE = kod 101)

**Problem (log 101-nowa):** R1-10b (warsztaty bez drewna, d91-180) 222/d przy progu 177 (srednia 11 biegow 203 +-33). Linia "Stocznie (A17 F5)" 101-nowa: d1-30 397 pni/d (brak drewna 202 prob/d, 33.5 statku/d), d91-120 257 pni/d (125 prob/d "brak drewna", 20.5 statku/d), d151-180 115 pni/d (49, 8.9). W d91-120 stocznie biora wiecej drewna niz wszystkie warsztaty zbrojne (201/d - diagnoza).

**Przyczyna:** `A17Fix.cs:506-507` (PayBuild, kod 101): `hw = Count(shelf, _hardwood)`, `uh = Math.Min(hw, logs)` - stocznia brala pnie z polki portu do zera, bez zapasu rzemiosla, ktory od K1 (BuildWoodAboveCraftStock, 14.10) zostawiaja wszystkie inne budowy (`BuildFunding.cs:346/:370`, `BuildFunding.Folk.cs:224`, `BuildFunding.DsWorks.cs:54`); stocznia ksieguje to drewno jako budowe (ramka FBuild, `OreLedger.NoteBuild`). Jedna zasada na jedno zjawisko - stocznia jest budowa.

**Zmiana (`A17Fix.cs` PayBuild, przy kluczu):** `hold = BuildFunding.WoodHold(town.Settlement, hardwood, false)`; pnie do uzycia = `max(0, polka - hold)`. `WoodHold` to ta sama regula K1: zapas rzemiosla miasta `CaravanBulk.TargetFor` (10 dob zuzycia miasta bez budow), przy tanim drewnie (indeks < 1, E3-B1b) polowa zapasu (najmniej 20); klucz K1 wylaczony - 0 (stocznia jak 101). Deski bez zmian. Kadluby i czesci statkow (SettlePieces) ida przez ten sam PayBuild. Linia "Stocznie (A17 F5)": nowy odcinek przed "| potkniecia": `| zapas rzemiosla (klucz ShipyardWoodAboveCraftStock TAK): zostawione rzemioslu N pni (suma przy probach), proby pominiete tylko przez zapas K` (N = suma min(polka, zapas) przy kazdej probie stoczni - ta sama polka moze liczyc sie w kilku probach doby; K = proby, ktore bez zapasu by sie udaly). `Settings.cs` + `McmSettings.cs` (gen_mcm: Armoury 2245 ustawien).

**Ryzyko / co sprawdzic (kontrola calosci):**
- Mniej statkow AI w d1-120 (dzis 33.5 / 20.5 statku/d): porty ubogie w drewno (pustynia, wyspy) wolniej dojda do liczby idealnej; AI kupuje statki od portow (ShipPortsBuyToIdeal) - linia "Statki (181)", NA17-5b (pni stoczni/d w dol), NA17-5c (czesci "brak drewna" w gore), "brak drewna" w linii Stocznie w gore.
- `WoodHold` ma liczniki pomiaru (targi, gdzie budowa pana zostawila drewno - `_woodHeldLord`, raz na targ i dobe; targi z polowa progu E3-B1b - `_woodHalf`): stocznia liczy sie teraz w nich jak budowa pana (dopisek "Roboty ludu (P2)" K1). Tylko liczby linii, nie gra.
- Zamknieta ekonomia bez zmian: nie powstaje zaden przeplyw zlota ani towaru; zmienia sie tylko, kto pierwszy bierze z tej samej polki. Place ciesli (kasa -> kiesa ludu) tylko przy zbudowanym statku - przy mniejszej liczbie statkow mniej plac.
- Narzedzie: regex NA17 `f5` (konczy sie na "statki z niczego N") i `f5p` (opcjonalny "statek poza portem") dzialaja z nowym odcinkiem (test na linii syntetycznej: f5 TAK, f5p te same grupy).
- Kolizje: P7c i P9 zmieniaja `MaterialOrders.cs` - tu nie ruszany.

**Oczekiwane progi (nowa kampania 180 dob, po P7a + P7b):** R1-10b ok. 120-160/d (dzis 222; prog 177 to jeden pomiar - nowy prog ze sredniej >= 2 biegow, CHANGELOG.md:16260); T2-K1-2 (miasta bez drewna) ok. 9-11 (dzis 14.2); NA17-5b drewno stoczni w dol (dzis 241 pni/d w 180 dobach); "brak drewna" stoczni w gore; R1-7 moze wzrosnac (P7b).

**Build:** Armoury kod 0; gen_mcm Armoury 2245 ustawien.

---

### P7b. Dymarka zostawia kuzniom miasta jedna dobe ich zuzycia drewna (R1-10b) - klucz `BloomeryShelfLeavesForges` (MCM "Bloomery Shelf Leaves Forges", grupa "Crafts, raw materials and horses (stage 6)"; TAK; NIE = kod 101)

**Problem (log 101-nowa, diagnoza R1-10b):** R1-10b 222/d (prog 177). Linia "Zelazo (lupy) (214)" d91-120: "targ 105/d" (dymarki z polki miasta handlowego) i "brak na polce 139/d" - polki tych miast ida do zera; miasta handlowe kopaln ponizej potrzeby srednio 4.9/d (Yunkai 24 z 30 dob, Lordsport 23, Meereen 23, Pyke 21, Tolos 16, Oldtown 14, Vaes Diaf 10). Szacunek diagnozy: ok. 60-80 z 222 cykli/d braku drewna w warsztatach. W probce 101: d30 "targ 139 (brak na polce 625)", d60 "targ 199 (269)", d90 "166 (7)", d120 "94 (92)".

**Przyczyna:** `Bloomery.cs:529` (Daily, krok b, kod 101): `n = Math.Min(want, shelf.GetItemNumber(wood))` - dymarka bierze cala polke miasta handlowego, gdy chce wiecej niz lezy; zakup idzie w `Bloomery.Daily` (ArmouryBehavior.cs:2667) przed budowami i przed warsztatami (DailyTickTown rozlozony w dobie), wiec kuznie miasta zostaja bez drewna.

**Sprawdzenie `CaravanBulk.UseNow` (zadanie: "sprawdz, czy liczy to, co trzeba"):** NIE - dla drewna `HandUse` (`CaravanBulk.cs:256`) przy kluczu BloomeryWoodDemand zwraca `rece x WoodPerHandForge + Bloomery.ShelfUse(town)`, czyli UseNow ZAWIERA zakupy dymarek z tej polki (srednia z NoteShelf); bez tego klucza stara stawka `g.PerHand` ma w sobie wytop rudy w miescie. ForgeKeep z UseNow zostawialby wiec dymarkom ich wlasny zakup. Dlatego nowa funkcja `CaravanBulk.UseNowNoBloomery` (dopisana obok, UseNow bez zmian): to samo co UseNow, ale drewno kuzni zawsze sama stawka wegla kowali (`rece x WoodPerHandForge`) + domy (`g.Fixed`) + rzemioslo (`TownCrafts.UseOf` / artisans) + linie warsztatow (`ShopUse`) - bez dymarek.

**Zmiana (`Bloomery.cs`, przy kluczu; wymaga Bloomery At Mine):**
- `ForgeKeep(town, wood) = ceil(CaravanBulk.UseNowNoBloomery(town, wood))` - jedna doba wlasnego zuzycia miasta, szacunek z rak (nie z pomiaru polki - nie wpada w petle przy pustej polce).
- Daily krok b: po policzeniu `n` (i po liczniku "brak na polce" - ten bez zmian, prawdziwy brak) `n = min(n, max(0, polka - ForgeKeep))`; roznica do licznika `_dForgeLeft`. Kredyt kupcow `F5Lend` i zakup za zloto biora najwyzej `n`, wiec zapas zostaje na polce.
- Kopalnia BK w miescie (`TownMineBlocked`, rezerwacja cyklu) - ta sama regula (to tez dymarka na polce tego miasta): cykl tylko, gdy `polka - ForgeKeep >= potrzeba` (w 101 tych kopaln 0 - "kopalnie BK w miastach: lupy 0, drewno 0"; regula dla 100% przypadkow).
- Linia "Zelazo (lupy) (214)": w nawiasie po "(brak na polce M" nowe pole `, zostawione kuzniom N` (+ ` i cykli kopaln BK w miastach K`, gdy > 0); bez klucza bez pola. Regexy narzedzia (`\(brak na polce (\d+)`, `brak zlota wsi (\d+)`, `, targ (\d+) za`) czytaja dalej - kazdy osobno (sprawdz_logi.py:6145-6146).
- `Settings.cs` + `McmSettings.cs` (gen_mcm: Armoury 2246 ustawien).

**Ryzyko / co sprawdzic (kontrola calosci):**
- Wymiana R1-10b na R1-7 w miastach pustynnych i wyspowych (Yunkai, Meereen, Tolos, Pyke, Lordsport): dymarka zostawia kuzni kilka ladunkow/d, czyli ok. 1 lupa na miasto-dobe mniej; R1-7 (kopalnia bez wegla, dzis 7.5%, prog 5%) moze wzrosnac do ok. 8-9% - do powiedzenia Jeffowi slowami gracza. Brak w dymarce zamawia wozy z miast z nadwyzka (krok c, CartOrders) - R1-7d (kontrola wozow) i linia wozow dymarek.
- Sprzezenie miar: `NoteShelf` liczy tylko to, co dymarka naprawde kupila -> `Bloomery.ShelfUse` spadnie -> `HandUse` (cel zapasu karawan, prog nadwyzki, cena drewna RawPrice) liczy mniejsze zuzycie miasta. Przy `FirewoodMineWoodKeep` cel K1 (`TargetFor` -> `UseMines`) dopelnia brak dymarek przez `MineShelfNeed - ShelfUse`, wiec zapas budow nie spada; bez tego klucza cel moze lekko spasc. Do sprawdzenia: T2-K1-1/-2, linia "Roboty ludu (P2)" (zapas rzemiosla miast), cena drewna.
- Zamknieta ekonomia bez zmian: ten sam towar na tej samej polce, inna kolejnosc kupujacych; zaplata wsi -> kasa miasta tylko za to, co dymarka wziela.
- Kolizja z P7a: obie reguly zostawiaja drewno rzemioslu tej samej polki (stocznia - zapas K1 10 dob, dymarka - 1 doba); nie licza sie podwojnie (kazda tylko ogranicza swoj zakup).
- Kolizje: P7c i P9 zmieniaja `MaterialOrders.cs` - tu nie ruszany.

**Oczekiwane progi (nowa kampania 180 dob, razem z P7a):** R1-10b ok. 120-160/d (dzis 222); T2-K1-2 ok. 9-11 (dzis 14.2); w linii 214 "zostawione kuzniom" > 0; "miasta handlowe kopaln ponizej potrzeby" - te same miasta, ale bez braku w warsztatach; R1-7 moze wzrosnac do ok. 8-9%; R1-7d reszta 0; R1-11 potkniecia Bloomery 0. Nowy prog R1-10b ze sredniej >= 2 biegow.

**Build:** Armoury kod 0; gen_mcm Armoury 2246 ustawien.

---

### Podsumowanie galezi (21.10, przed scaleniem)

| Krok | Commit | Klucz MCM (domyslnie) | Linia logu |
|---|---|---|---|
| P0 SideVillage NRE | `087590e2` | brak (poprawka bledu) | - |
| P1 narzedzie progow | `3881552b` | brak (narzedzie + napisy logu) | Kiesa ludu / linia stanu: "tempo W 1/60 (P3), place R/P 0.15"; Drogi Valyrii (VR): ", ponizej 5%: ..." |
| P6 piraci na morzu | `3e6fc2f2` + `392afcf8` | `BandFoodAtSea` (TAK) | m96-bandy: "\| z morza: paser portu ..., polow zalogi ..." |
| P7a stocznia | `7df2452d` | `ShipyardWoodAboveCraftStock` (TAK) | Stocznie (A17 F5): "\| zapas rzemiosla (...): zostawione rzemioslu N pni ..." |
| P7b dymarka | `01fef812` | `BloomeryShelfLeavesForges` (TAK) | Zelazo (lupy) (214): "(brak na polce M, zostawione kuzniom N" |

Build 5 modow (Release, GameLibs z lancuch/libs): Armoury 0 (6 ostrzezen jak dotad), RealisticCaptivity 0, GrandTourney 0, ForgeView 0, CrashScribe 0. gen_mcm: Armoury 2246 ustawien (WGRANIE 101: 2243), RealisticCaptivity 115, GrandTourney 44. Nic nie wgrane, nic nie wypchniete. Test w grze: jeden finalny autotest po scaleniu wszystkich galezi (nowa kampania 180 dob z `armoury-ks-auto.flag` + bieg z zapisu na regresje).

Do powiedzenia Jeffowi: GL2-8 to zmiana progu (60 dni projektu -> zapas wazony regul ludu + stac >= 95%); P7b moze podniesc R1-7 (kopalnia bez wegla) z 7.5% do ok. 8-9% w zamian za drewno dla kowali; P7a - mniej statkow AI w pierwszych 120 dobach; P6 - najedzeni piraci sa silniejsi (bez kary morale).

---

### Poprawki po recenzji (21.10, osobny commit; bez nowych kluczy MCM, liczby gry bez zmian)

Recenzja: 7 uwag, w tym 2 pary tych samych (W12-6 dwa razy, stocznia dwa razy) - 5 roznych. Prawdziwe w tej galezi: 4 (R1-R4) - poprawione. Prawdziwa poza galezia: 1 (R5, M103_TAG) - tu nie ruszana, opis do scalania. Odrzucone: 0.

**R1. W12-6 mierzyl sume ofert W+R+P (uwagi 1 i 5).**
- Problem (log 101-nowa): licznik "oferta nadwyzki na jutro" to suma trzech klas (`TownFolk.Live.cs:1113`, `d.OffT += f.Offer[c]` dla c < 3). Przy P3 oferta W = `DOvC[0] / 60` (`TownFolk.Luxury.cs:358`), a `DOvC[0]` to ta sama nadwyzka po daninie i kiesa2, ktora idzie do "lokaty" (`TownFolk.Live.cs:1078-1079`). Dlatego suma / lokata >= 1/60 wynika z konstrukcji, a prog byl jednostronny. Liczby: sr28 oferta 183704/d przy lokacie W 10269398 (1/60 = 171157, R+P ok. 12.5 tys./d = 7% licznika); ostatnia doba 109015: oferta 199025, lokata 10719078 (1/60 = 178651, R+P ok. 20.4 tys. = 10%). Zawyzona oferta W (np. blad 1/6) przeszlaby; oferte W zanizona o kilka procent przykrylyby R/P (rosna z kiesami P).
- Zmiana: `TownFolk.Live.cs` - nowe pole `LDay.OffW` (suma `Offer[0]` miast, zerowane z nowym LDay co dobe), dopisek P3 w linii doby "Kiesa ludu": `(P3: oferta W 1/60 = X, R i P 0.3/60 = Y - linia Zbytki)`, Y = OffT - OffW. `tools/sprawdz_logi.py` W12-6: gdy pole jest - oferta W / lokata W (lokata z tych samych dob) = 1/N +-2% DWUSTRONNIE; oferta R+P i suma W+R+P / lokata jako INFO w wyniku. Log bez pola (101 i starsze) - stary prog jednostronny z dopiskiem "log bez oferty W osobno - licznik zawiera R/P" i "prog jednostronny, nie lapie zawyzonej oferty".
- Sprawdzenie: 101-nowa i 101-prolog (bez pola) - TAK 0.0179 jak dotad, z dopiskiem. Logi syntetyczne z 101-nowa (pole X dopisane): X = lokata/60 - TAK 0.01667 = 1/60.0 (R+P INFO 12548/d); X x10 (blad 1/6) - NIE 0.16667; X x0.85 - NIE 0.01417 = 1/70.6.
- Ryzyko: regex `P3: oferta W 1/(\d+)` dalej czyta N (" = X" stoi za liczba); innych czytelnikow napisu brak (grep tools, src). Nazwa wiersza W12-6 bez zmian.
- Oczekiwane (nowy bieg): W12-6 TAK ok. 0.0167 (1/60 +-2%), R+P INFO.

**R2. Stocznia: klucz drukowany jako TAK bez K1 (uwagi 2 i 7).**
- Problem: `BuildFunding.WoodHold` zwraca 0 przy wylaczonym `BuildWoodAboveCraftStock` (`BuildFunding.cs:60`), a linia "Stocznie (A17 F5)" drukowala "klucz ShipyardWoodAboveCraftStock TAK" z samego `ShipWoodHoldOn` (`A17Fix.cs:741`); opis MCM nie mowil o zaleznosci. U Jeffa K1 = TAK, wiec w grze bez skutku.
- Zmiana: odcinek linii: `(klucz ShipyardWoodAboveCraftStock TAK, dziala TAK)` - "dziala" = oba klucze; przy samym kluczu stoczni dopisek ` - wymaga BuildWoodAboveCraftStock`. `Settings.cs` opis: "works only while Build Wood Above Craft Stock is on" + gen_mcm (Armoury 2246 ustawien, zmienia sie tylko HintText). Zachowanie bez zmian - celowo: jedna zasada K1 dla wszystkich budow (przy wylaczonym K1 wszystkie budowy biora drewno do zera, stocznia tez).
- Ryzyko: regexy NA17 `f5`/`f5p` nie czytaja tego odcinka (sprawdzone przy P7a).

**R3. Audyt VR: podloga tylko przy premii < 5% (uwaga 4).**
- Problem: `MapRoadKinds.Audit` sprawdzal podloge gry (r0 <= LimitMin) tylko wewnatrz `if (b < 0.05)`. Probka z podlogi, ktora z premia wyszla ponad podloge (b miedzy 5% a 25%), zostawala w sredniej VR-3. Audyt traktow DR (`MapRoads.cs:1217`) wyklucza wszystkie probki z podlogi. Blad mogl dac tylko falszywe NIE (premia z podlogi <= premii pelnej).
- Zmiana: `atFloor = r0 <= lmin + 0.001f` dla kazdej probki; dopisek drukowany, gdy jest probka z podlogi albo premia < 5%: `, ponizej 5% lub na podlodze: K (na podlodze predkosci gry F, sr. X%; sufit kolumny C; inne O) [...]`. Narzedzie: `M103_VALYRIA_LOW` czyta oba napisy (`ponizej 5%:` i `ponizej 5% lub na podlodze:`); VR-3 odejmuje wszystkie probki z podlogi (bez zmian w rachunku); VR-3b: "probki z podlogi gry albo z premia < 5%".
- Sprawdzenie: log syntetyczny (101-nowa, w 35 dobach 2 probki z podlogi po 12.50%, srednia doby 20.83%): nowe narzedzie VR-3 TAK "bez podlogi 25.00% (ze wszystkimi 24.19%)", VR-3b 70 na podlodze; stare narzedzie (HEAD przed poprawka) nowego napisu nie czytalo - srednia 24.19%. Logi 101 bez dopisku - TAK 25.00% jak dotad; VR-1 nierozpoznane 0.

**R4. GL2-8 bez wykrywania regresji wobec biegu bazowego (uwaga 6).**
- Problem: w nowej sciezce progu (zapas wazony) `bdm` z `--baza-doby` tylko sie wyswietlal - spadek np. z 27 do 13 dni przy zapasie 12.2 dalby TAK.
- Zmiana: przy biegu bazowym dodatkowo srednio >= 0.9 x baza (jak w starej sciezce: ponizej = NIE); w wyniku `baza N - REGRESJA: srednio < 0.9 x baza`; bez bazy napis jak w starej sciezce ("bez biegu bazowego (--baza-doby)").
- Sprawdzenie: 101-nowa bez bazy TAK jak dotad (min 27, sr. 32, zapas 12.2, stac 100%); z baza podstawiona 60 dni - NIE "REGRESJA"; z baza 34 - TAK. 101-prolog bez bazy NIE jak dotad (stac 88%).
- Ryzyko: baza musi byc biegiem tego samego kodu z kluczami GL wylaczonymi (definicja `--baza-doby`); stary bieg W43 (51 dni) da NIE - to porownanie z innym kodem.

**R5. POZA TA GALEZIA - M103_TAG w m103-sceny i m103-lektor (uwaga 3) - PRAWDZIWA, do scalania.**
- Sprawdzone: `w-toku/m103-sceny` ma `M103_TAG = "Prolog (m103): "` (`tools/sprawdz_logi.py:37606`, uzycia :37666-37668), `w-toku/m103-lektor` ma `M103_TAG = "Lektor (m103): "` (:37962, uzycia :38013-38015) - obie stale na poziomie modulu. Po scaleniu obu obowiazuje ostatnie przypisanie i jedna z grup (m103sceny albo m103lektor) szuka cudzych linii; git tego nie zglosi.
- Tu nie poprawione: stalej nie ma w tej galezi, a tamte drzewa maja wlasne prace w toku (nie ruszam cudzych galezi).
- Do scalania: przed scaleniem zmienic nazwe w obu galeziach (np. `M103S_TAG` w m103-sceny, `M103L_TAG` w m103-lektor, wraz z uzyciami); konflikt `Armoury/src/SubModuleMain.cs` miedzy m103-sceny a m103-lektor rozwiazac recznie; `McmSettings.cs` odtworzyc gen_mcm. m103-gospodarka tych miejsc nie dotyka.

**Build:** 5 modow kod 0 (Armoury 6 ostrzezen jak dotad, RealisticCaptivity 0, GrandTourney 0, ForgeView 0, CrashScribe 0); gen_mcm: Armoury 2246, RealisticCaptivity 115, GrandTourney 44. Nic nie wgrane, nic nie wypchniete.

---

## Czesc 2: P3 (21.10, decyzja D1 po biegu P2 - journal wf_a568aafd-15a, ostatni wiersz)

**Status:** DO SPRAWDZENIA (nic nie wgrane; test: bieg A - prolog 230 dob z `armoury-ks-auto.flag`, bieg B - 160 dob z zapisu z KS; porownanie z P2 `10-08-39`, P0 `06-23-39` i `04-57-25`).

### P3. Pila firm DS pod KS: sufit z doplywu netto, obnizka z dochodu, firma od zera - klucze `KsFarmSurplusCapNet` (a, b, c, e, f) i `DsFarmFixShareCap` (d) (MCM "Ks Farm Surplus Cap Net", "Ds Farm Fix Share Cap", grupa "Town purses: the farm catches up..."; oba TAK; NIE = kod 101)

**Problem (log P2 `Armoury-2026-10-21_10-08-39.log`, prolog 230 dob z KS):**
- 270 podwyzek wedlug nadwyzki, razem +35 759 182 zl/rok, jedna obnizka (linia f5 :132081). Firmy miast 35.16 mln (108704) -> 76.63 mln zl/rok (108877, :132501); P0 bez KS 35.71 -> 36.40 mln.
- Udzial firm w doplywie netto przy ustaleniu 1.270 (:32208), w P0 1.282 - firma ponad doplyw w obu drogach.
- Zaleglosci: Dreadfort 91 147 zl (2.1 raty) z 119 773 zl miast; zamki 270 085 zl u 41 (P0 146 700). M96D-3 93 813 zl, rosnie ok. +3.1 tys./d na koncu.
- Volantis: firma 0 przy ustaleniu (kasa 28 191 zl przy zapasie 152 300), potem kasa 551 tys. (3.1 x zapas) bez ujscia - D2K-5 109 dob.
- Miasta podniesione w czasie wzrostu kas spadaja ponizej zapasu kupcow (NAKUM-27 17 -> 26, NKA-3 28): Harroway pobor 4536/d przy doplywie 1607/d, Gulltown 6385 przy 3371 (kasy-miast.csv, ostatnie 28 dob).

**Przyczyna (kod 101, `Armoury/src/DsTowns.cs`):**
- :613 `add = Math.Min(grow * YearDays, mx * F)` - bez gornej granicy "doplyw netto x 364".
- :590 kwalifikacja z kasy brutto (`g <= OverReserve * Reserve`); naleznosci (`CofferOwes`) odejmuje tylko `SurplusHolds` (:526-527).
- :344-345 przeglad wedlug nadwyzki biegnie od doby po ustaleniu, takze w rampie DS (kasa placi rampa x rate, reszta firmy wyglada jak przyrost kupcow; P2: pierwsza podwyzka 108718, rampa 108703-108731).
- :381-389 Fix: `Share = dayF / net` bez gornej granicy.
- Zapadka: w gore +25% co 14 dob, w dol tylko przeglad zwykly -15% co 91 dob (`Review`).
- :612 `!(F > 0)` konczy przeglad przed podwyzka i gersuma, `Renew`/`Review` pomijaja `F <= 0` (:433, :487) - firma 0 nie ma wyjscia.

**Zmiana (`DsTowns.cs`, `KsFolkIncome.cs`, `Settings.cs` + `McmSettings.cs` z gen_mcm):**
- (a) `SurplusReview`: `add = Min(grow*364, mx*F, Max(0, NetAvg(f, 7)*364 - F))`; bez pomiaru doplywu (NaN) albo doplyw <= 0 - podwyzka 0. Przy podwyzce 0 gersuma (`CatchUpDecide`) dalej idzie - zapas kasy ponad sufit ma ujscie. Licznik "przyciete sufitem N (-X zl/rok)" = o ile podwyzka mniejsza.
- (b) kwalifikacja: `gq = g - CofferOwes(f, s)` zamiast `g` (ta sama miara co `SurplusHolds`: czesc najblizszej raty zebrana od dnia raty, zaleglosci, pomoc M7, rata M9, reszta gersumy); licznik powodu f7 (`CatchUpWhy`) dostaje `gq` (ten sam warunek). Pomiar przyrostu (SG0) bez zmian.
- (c) `inRamp = NewShare < 1`: w rampie DS kazde miasto z KS - SG0/SGDay/SLow od nowa i dalej (bez pomiaru, podwyzek, gersumy i obnizek); pomiar od pierwszej doby po rampie.
- (d) [ZASTAPIONE po recenzji - wspolczynnik swiata, patrz "Czesc 2 - poprawki po recenzji"] `Fix` przy `DsFarmFixShareCap`: gdy doplyw netto okna > 0 i `dayF > net` - `FL` i `FC` x `net/dayF` (udzial 1), `Share = Min(1, dayF/net)`; czesc z oszczednosci ludu (FLf) bez zmiany (placi lud). Doplyw okna <= 0 albo bez pomiaru - bez sufitu (jak dotad; Share NaN, przeglad zwykly z udzialem swiata) - liczone w linii ustalenia. Miasta i zamki, obie drogi (z KS i bez).
- (e) `IncomeCut` (nowe): w galezi "kasa po naleznosciach <= 1.5 x zapas albo zaleglosci (firma albo dlug rady m96)" PRZED `continue` - cykl `SLow` (pole sesji): pierwsza doba w tym stanie zaczyna cykl, po `KsFarmSurplusReviewDays` (14) dobach w tym stanie bez przerwy - jesli `F > NetAvg*364`: `F = Max(NetAvg*364, (1-mx)*F)` (FL i FC w tym samym stosunku, FLf bez zmiany - jak podwyzka), nastepny cykl od tej doby. Kasa ponad progiem, oblezenie / zdobycie, rampa, utrata KS - cykl od nowa. `Share > 1` (stary zapis albo ustalenie bez sufitu) schodzi do 1 przy obnizce - przeglad zwykly nie podniesie firmy z powrotem ponad doplyw.
- (f) `FromZero` (nowe): przy `F == 0` po pelnym cyklu (te same warunki co podwyzka) i przyroscie > 0: doplyw > 0 - firma `Min(grow*364, NetAvg*364)` w podziale pan/korona jak suma firm miast swiata (`WorldLordShare`, raz na dobe), `Share = Min(1, F/364/net)`, potem gersuma; doplyw <= 0 albo bez pomiaru - firma dalej 0, gersuma. `CatchUpPay` przy F = 0 dzieli gersume pan/korona jak w swiecie (dotad calosc panu). Kasa bez przyrostu - nic (jak przy F > 0).
- `Fix` zeruje `SLow` (stan czyszczony przy nowych firmach); `Reset` zeruje pamiec `WorldLordShare`.

**Linie logu:**
- f5 "Dochod ludu wedlug koszyka (f5-lud)": za "| potkniecia N" (regex NLU_RX bez zmian - `match` od poczatku) przy kluczu: `| P3 sufit firm (Ks Farm Surplus Cap Net TAK): przyciete sufitem N (-X zl/rok), obnizki z dochodu N (-Y zl/rok), firmy od zera N (+Z zl/rok); od startu sesji: przyciete N (-X), obnizki N (-Y), od zera N (+Z)`. Firma od zera liczy sie tez w "podwyzek dzis". Klucz NIE - linia jak 101.
- DS "Firmy miast i zamkow (DS)": przed "| kontrola" przy ktoryms kluczu: `| udzial firm w doplywie netto (m103 P3): swiat X (miasta A, zamki B); osad z firma ponad doplyw N (o Y zl/rok), z firma bez doplywu (doplyw <= 0 albo bez pomiaru) M (firmy Z zl/rok)` - ta sama miara co przy ustaleniu (osady z doplywem > 0 i firma > 0, pamiec 28 dob).
- FIRMY USTALONE: za "udzial firmy w doplywie netto swiata X" przy `DsFarmFixShareCap`: ` (Ds Farm Fix Share Cap TAK: przed sufitem Y; firma ponad doplyw netto okna przycieta do doplywu: miasta N o X zl/rok, zamki M o Y zl/rok; bez sufitu - doplyw netto okna <= 0 albo bez pomiaru: K osad z firmami Z zl/rok)`.

**Narzedzie (`tools/sprawdz_logi.py`, osobny blok na koncu + 1 linia dispatch + opis grupy; nic nie przestawione):** nowa grupa `--grupa m103p3` (M3P-0..11): segment f5 w kazdej linii, zero podwyzek w rampie DS (rampa < 1 z linii DS tej samej doby), sumy przycietych / obnizek / firm od zera (INFO), udzial przy ustaleniu <= 1.00, firmy przy ustaleniu (INFO), udzial na koniec <= 1.00, zaleglosci zamkow <= 200 tys., firmy na koniec (INFO), czas f5 <= 0.2 ms, potkniecia i ERROR 0, pobor wobec doplywu Volantis/Dreadfort/Harroway/Gulltown (INFO). Sprawdzone na P2, P0 i 04-57-25 (kod 0; bez segmentow - INFO, M3P-7 NIE 270 085 / 433 778 jako punkt odniesienia) i na liniach syntetycznych w nowym formacie (wszystkie regexy czytaja; NLU_RX, `byset` nkasy, M96D-3 i kontrola "platnicy - odbiorcy" czytaja linie z nowymi segmentami tak samo). Grupy nie ma w `grupy101.txt` - dopisac do listy porownania.

**Kontrola calosci (CLAUDE.md 8.0):**
- Te same pola: SG0/SGDay (SurplusReview, Renew/Review - reset przy zmianie, `NoteCofferLoan` - tylko przy SG0 != MinValue, w rampie MinValue = bez poprawki, CatchUpDecide/CatchUpPay - bez zmian); SMin (bez zmian); Share (Fix (d), Renew/Review czytaja, SurplusReview podnosi do 1, IncomeCut tnie > 1 do 1, FromZero ustawia; zapis jak dotad); CLeft (CofferOwes - teraz takze w kwalifikacji (b), HireKeep, CofferLoanFloor, CatchUp - bez zmian); FL/FC (Pay, Terms, Review/Renew, CofferOwes, CatchUpKeep, HireKeep, TdArrears/TdKeep m96, LordFarmPerDay zamkow (Z8), linia DS, zapis).
- Przeglad f4 (Review) i (e) nie walcza: Review celuje w `Share x NetAvg x 364` z Share <= 1 (po (d)/(e)), (e) tnie tylko ponad `NetAvg x 364`; `_renewN > 0` - w dobie przegladu zwyklego dalej bez SurplusReview (jedna zmiana firm na dobe, NKA-2). Wymuszone -15% i (e) moga zejsc w tym samym kwartale - zamierzone (firma ponad zysk schodzi szybko).
- m96-dlugi: `ArrearsLike` (dlug rady) dalej blokuje podwyzke i teraz wlacza obnizke (e); mniejsza rata po (e)/(d) = mniejsze `TdKeep`/`TdArrears weekly` - rada splaca szybciej; `NoteTerm`/`TermsLike` bez zmian.
- f7 gersuma: przy podwyzce przycietej do 0 dalej `CatchUpDecide` (ujscie zapasu); firma od zera - gersuma po ustaleniu firmy; F = 0 bez doplywu - sama gersuma; podzial przy F = 0 jak w swiecie. Prog gersumy `CatchUpKeep` z F po zmianie (jak dotad).
- KsFolkIncome: `NoteRaise`/`NoteShareUp`/`NoteCutAfterRaise` bez zmian; nowe liczniki tylko linii (doba i sesja, `Zero`/`Reset`).
- Zamki i Z8: (d) obniza FL zamku ponad doplyw -> `LordFarmPerDay` (rownowaznik dnia firmy w cieciu zwrotu zoldu zalog Z8) mniejszy -> korona zwraca panu wiecej zoldu - ta sama regula ("zwrot tnie to, co wrocilo z zamku").
- Zamknieta ekonomia: zloto sie nie rusza poza gersuma (ta sama droga co dotad: kasa -> pan / skarbiec, kontrola "zdjete - panom - koronie"); zmienia sie tylko nalezna rata. Kontrola linii DS "platnicy - odbiorcy" bez zmian.
- Klucz NIE: `gq = g` (long -> double, ta sama liczba), `RemitOf` liczony zawsze (bez skutkow ubocznych), SLow nieczytane, linie bez segmentow - co do monety jak 101.

**Skutki (d) w wersji sufitu na osade (ZASTAPIONEJ po recenzji - liczby nizej to powod zmiany) - sprawdzone na kasy-miast.csv (okno 28 dob przed ustaleniem; tylko miasta - zamkow w CSV nie ma):**
- P2 (108703): przycietych 31 miast, -10.06 mln zl/rok; 44 miasta z doplywem okna <= 0, z tego 40 z firma bez sufitu (7.56 mln); firmy miast przy ustaleniu ok. 25.2 mln (decyzja zakladala 28-33 mln z udzialu swiata 1.27 - per miasto tnie glebiej); udzial miast przed sufitem 1.433.
- P0 (108703, droga bez KS): 36 przycietych, -10.46 mln; 40 bez sufitu (5.44 mln).
- 04-57-25 (109131, z zapisu): 44 przycietych, -18.48 mln z 140.2 mln.
- Okno ustalenia w P2/P0 przypada na wlaczenie KS i spadek kas (doplyw swiata w oknie ok. 0) - doplyw okna jest szumny. Miasta przyciete ponizej 25% dawnej firmy: P2 6 (Myrth 502 tys. -> 44 tys., pozniejszy doplyw x 364 = 3.27 mln; Meereen 1.47 mln -> 256 tys., pozniej 7.29 mln), P0 5 (Harroway 841 tys. -> 2 tys., pozniej 306 tys.; Yronwood 69 tys. -> 1 tys.; Last Hearth 350 tys. -> 31 tys., pozniej 1.51 mln).

**Ryzyko / co sprawdzic:**
- R1 (d) [NIEAKTUALNE - po recenzji wspolczynnik swiata i jeden krok (a)/(f); czasy nizej liczone do dawnej firmy, a nie do pozniejszego zysku - zanizone]: firmy bliskie 0 przy chwilowym doplywie bliskim 0. Z KS odrabiaja (a) po +25%/14 dob (Meereen z 256 tys. do ok. 1.5 mln - ok. 8 cykli, 112 dob; w tym czasie zapas zdejmuje gersuma); BEZ KS (P0, krainy wrocone) tylko przeglad zwykly +15%/kwartal - Harroway z 2 tys. praktycznie nigdy. (f) obejmuje tylko F = 0. Nieciaglosc: doplyw okna -1 zl/d - firma bez sufitu, +1 zl/d - firma ok. 364 zl/rok. Do decyzji po tescie (klucz osobny): np. (f) dla "F ponizej doplywu x 364 x udzial" albo sufit (d) z doplywu dluzszego okna.
- R2 (d): dochod panow z firm miast przy ustaleniu ok. -29% (P2/P0) - C-4c (rody na minusie, P2 250, granica 250) i NAKUM-7 do sprawdzenia; zamki nieoszacowane (brak CSV). Pieniadz zostaje w kasach -> M6 do P/R albo (z KS) gersuma i podwyzki (a).
- R3 (a)+(c): mniej podwyzek -> kasy pod KS moga rosnac (NAKUM-26 P2 +7.8 tys./d, granica +30; G1-5a <= 160%); ujsciem zostaje gersuma f7 (przy podwyzce 0 tez) i M6.
- R4 (e): obnizka wymaga 14 dob ciaglego stanu "nisko albo zaleglosci" - miasto oscylujace wokol 1.5 x zapas (raty co 7 dob) moze jej nie dostac; (b) odejmuje zebrana czesc raty, co wygladza tydzien. Sprawdzic Dreadfort, Harroway, Gulltown w kasy-miast.csv (M3P-11) i licznik obnizek.
- R5 (b): wiecej miast poza przegladem w gore (kasa po naleznosciach) - f7 "kasa do 1.5 x zapas" w gore (to ten sam warunek).
- R6: FLf przy `DsFarmFolkFromSavings` NIE placi kasa, a sufit (a)/(d)/(e) liczy tylko FL + FC (jak Share dotad) - u Jeffa klucz TAK (domyslny).
- Koszt: (e)/(f) w petli SurplusReview (ok. 97 miast, NetAvg 28 liczb tylko w dobie cyklu), ShareToday raz na dobe (ok. 230 x 28) - ponizej 0.1 ms; czas f5 (M3P-9) bez zmian.

**Oczekiwane progi (cele D1; bieg A wobec P2):** M96D-3 <= 50 000 zl (P2 93 813), w ostatnich 28 dobach <= +0.5 tys./d; NAKUM-27 <= 19 (P2 26); NKA-3 <= 16 (P2 28); D2K-5 <= 14 dob (P2 109, Volantis) [POPRAWIONE po recenzji: <= 21 dob]; udzial firm w doplywie netto <= 1.00 przy ustaleniu i na koniec (M3P-4, M3P-6; P2 1.270) [po recenzji: przy ustaleniu z konstrukcji - M3P-4 sprawdza arytmetyke wspolczynnika, firmy bez doplywu M3P-4b/6b INFO]; zaleglosci zamkow <= 200 tys. (M3P-7; P2 270 085); M96D-4 niewyplacalnych 0-1, miast z dlugiem <= 20; M3P-1 0 podwyzek w rampie; M3P-0 segment w kazdej linii f5. Bez pogorszen: NAKUM-26 kasy <= +30 tys./d, G1-5a <= 160% Z33, KR-9 >= 1.0, C-4c <= 250 (docelowo <= 217), NAKUM-7 zaden nowy posiadacz ponad +30 tys./d, czas f5 <= 0.2 ms, NAKUM-7s ARYTMETYKA OK, "platnicy - odbiorcy = 0", 0 potkniec DsTowns. Bieg B (z zapisu): M96D-3 <= 105 tys. (558-873 tys.), niewyplacalnych <= 2 (9), dlug miast <= 0.5 mln (1.06), firmy miast < 231 mln, NAKUM-27 <= 14. ~~Bieg B wczytuje zapis z firmami juz ustalonymi - (d) nie dziala (brak Fix), (e) zetnie Share > 1 przy pierwszej obnizce.~~ BLAD (recenzja): w 04-57-25 okno CZEKA na rampe F1 (:1127), start okna 109103 (:17463), FIRMY USTALONE 109131 w trakcie biegu (:34064) - w biegu B (d) dziala; plan testu poprawiony w "Czesc 2 - poprawki po recenzji".

**Build:** Armoury kod 0 (6 ostrzezen jak dotad); gen_mcm Armoury 2248 ustawien (bylo 2246).

---

### D4. Narzedzie: NKS-7e i NKS-7 licza pare "poza" tylko, gdy KS oddalil cene od celu (bez klucza MCM - tylko `tools/sprawdz_logi.py`, gra bez zmian)

**Problem (log P2, flaga `armoury-ks-przerwij.flag.p2-prolog-ks-1056`):** NKS-7e NIE "w pasmie 3 z 24, wynik PRZERWAC", NKS-7 ETAP "poza pasmem 36". Ta sama miara bez KS: P0 3/24 (108682, 108703), 4/24 i 5/24; P2 przed wlaczeniem 3/24; biegi 160 z zapisu 3-4/24 - flaga PRZERWAC w kazdym biegu (95-160 ... P2: 19-21 z 24).

**Przyczyna:** ceny startu z Planu (`KsCore.StartIndex`) sa przyciete do 0.7-1.5 (owoce, sol, oliwki), a targ nie mial takich cen nigdy, takze bez KS (journal wf_a568aafd-15a, diagnoza ks-flaga (a); D4). Kontrola wczesna w grze nic nie wylacza (`StartCalibration.Ramp.cs:384-400` - w grze tylko linia), wiec to blad miary narzedzia, nie gry.

**Zmiana (`tools/sprawdz_logi.py`, nks_main; gra bez zmian):**
- Baza: linia "KS: ceny miast" z ostatniej doby przed A (`a` = rampa A albo doba wlaczenia; P2 108682, 04-57-25 109109). Linia sprzed recenzji (sam swiat) albo brak linii przed A (bieg z zapisu po wlaczeniu) - progi jak dotad, z dopiskiem "(bez linii cen przed A - pasmo bezwzgledne)".
- NKS-7e: z linii "KS: kontrola wczesna D+7" pary poza pasmem (cena od D i cena startu z tej linii); para jest "poza" tylko, gdy |cena - cel| > 0.3 i |cena - cel| > |cena przed A - cel| + 0.3; wynik jak w grze - PRZERWAC przy ponad polowie par. Nowy wiersz INFO "NKS-7e (cel KS)" - dawna miara (w pasmie X z N, wynik linii gry). Krotka `early` ma 6. pole (linia) - inne odczyty bez zmian.
- NKS-7: ta sama regula dla mediany linii okna (cel = cena startu z linii polek startu; bez niej 1.0 - srodek pasma 0.7-1.3 jak dotad); ETAP liczony z liczby par oddalonych (prog rundy 3 `NKS_R3["nks7"]` = 45 bez zmian). Nowy wiersz INFO "NKS-7 (cel KS)" - dawne pasmo bezwzgledne. Klucze wierszy dla porownaj.py bez zmian ("NKS-7", "NKS-7e"); nowe wiersze maja klucze "NKS-7 (cel KS)" i "NKS-7e (cel KS)".
- Opis grupy nks (pomoc) - dopisek D4.

**Wyniki na istniejacych logach (gra nie uruchamiana):**

| Log | NKS-7e przed -> po | NKS-7 przed -> po | nks razem NIE |
|---|---|---|---|
| P2 `10-08-39` | NIE (w pasmie 3 z 24, PRZERWAC) -> **TAK** (poza 7 z 24, baza 108682, dalej: Westeros grain, cheese, butter, olives, orange; Essos grain, wool) | ETAP 36 poza -> ETAP 7 (Westeros cheese, cow, hog, sheep; Essos hardwood, hog, sheep) | 15 -> 14 |
| `04-57-25` | NIE (4 z 24, PRZERWAC) -> **TAK** (poza 4 z 24, baza 109109, dalej) | ETAP 41 -> ETAP 8 | 14 -> 13 |
| P0 `06-23-39` (bez KS) | bez wlaczenia - bez zmian | INFO "sam Plan" - bez zmian | 3 -> 3 |

Zgodne z decyzja (P2: 7/24, "dalej") i z przeliczeniem `decyzja/pasmo_wzgl.py` (KS 5-7 par, bez KS 11-13).

**Ryzyko / co sprawdzic:** miara wzgledna nie lapie towaru, ktory byl daleko od celu juz przed A i KS go nie przybliza (np. sol 4.34 przy celu 1.00) - to zostaje w wierszu INFO "cel KS" i jest osobnym tematem (ceny samej KS: polki startu pomijaja 76% pozycji `StartCalibration.Ramp.cs:235`, puste polki soli, sera i oliwek - NKS-7g). Gdy linia cen przed A jest daleko od A (np. co 7 dob - do 6 dob roznicy), baza moze zawierac poczatek rampy KS - w P2 i 04-57-25 baza 3 doby przed A. Plik flagi w autotescie dalej powstaje z miary gry (kod gry bez zmian) - odklada sie recznie jak dotad.

**Oczekiwane (bieg A po P3):** NKS-7e TAK (ok. 5-7 z 24, dalej), NKS-7 ETAP albo TAK (kilka par oddalonych), wiersze "cel KS" INFO jak dotad.

---

## Czesc 2 - poprawki po recenzji (21.10; P3 - klucze bez zmian nazw: `DsFarmFixShareCap` (d), `KsFarmSurplusCapNet` (a, f, przeglad zwykly); oba TAK; NIE = kod 101; status DO SPRAWDZENIA - nic nie wgrane)

Recenzja 9 uwag (w tym 2 pary powtorzen: 1 = 6, 3 = 7). Liczby uwag sprawdzone na kasy-miast.csv skryptem recenzenta (`scratchpad/rec/d_cap.py`) i wlasnym (`scratchpad/p3rev/cnt.py`) oraz w logach - zgadzaja sie (jedna roznica: w P2 44 miasta z doplywem okna <= 0, z tego 40 z firma - stad 40 w uwadze 1 i 44 w uwadze 6).

| Uwaga | Werdykt | Co zrobione |
|---|---|---|
| 1, 6 - (d) sufit na osade z doplywu jednego okna to szum (P0 bez KS: 38 z 97 miast z doplywem <= 0; Myrth 502 tys. -> 44 tys. przy pozniejszym zysku 3.27 mln; skok na zerze: Harroway P2 -153 zl/d - 664 tys., P0 +5.6 zl/d - 2 tys.; 04-57-25 przy udziale swiata 0.979 przycinal 44 miasta o 18.5 mln; firmy miast 25.2 mln wobec celu 28-33 mln) | PRAWDZIWA | (d) = jeden wspolczynnik swiata (nizej) |
| 2, 6 - krok (a) od F: firma po (d) albo po seriach (e) (F x 0.75^n nigdy 0) dochodzi do zysku kilkanascie cykli (Myrth 19.3, Meereen 15), a firma 0 dostaje od razu caly zysk | PRAWDZIWA | jeden krok (a)/(f): `mx x max(F, doplyw x 364)` |
| 2 (czesc) - to samo w przegladzie zwyklym bez KS (gorna granica `max(F, cel) x (1 + m)` dla Share = 1) | ODRZUCONA | Powodem byly gleboko przyciete firmy z (d) na osade; wspolczynnik swiata nie robi firm bliskich 0 (kazda x k, P2 0.787). Firma 0 bez KS to luka sprzed P3, poza zakresem (D2: luka miast z firma 0 tylko w P3(f) z KS). Bez nowego zachowania drogi bez KS - "nie dodawaj przy okazji" |
| 3, 7 - CHANGELOG:279 i raport: "w biegu B (d) nie dziala" | PRAWDZIWA | 04-57-25: okno CZEKA (:1127), start okna 109103 (:17463), FIRMY USTALONE 109131 (:34064) - (d) dziala; plan testu poprawiony (nizej), zdanie w czesci 2 przekreslone |
| 4 - M3P-4 spelnia sie z konstrukcji, firmy bez doplywu sa poza miara (takze M3P-6) | PRAWDZIWA | M3P-4 sprawdza arytmetyke wspolczynnika; nowe INFO M3P-4b i M3P-6b z firmami bez doplywu. Wiersz "przyciete ponizej 25% dawnej firmy" niepotrzebny: przy wspolczynniku swiata kazda firma x k (k w linii) |
| 5 - przeglad zwykly (Review/Renew) i `old` w SurplusReview biora udzial > 1 (stary zapis 101: udzial swiata 1.27, Myrth 11.5; klucz (d) NIE) - +15%/kwartal ponad doplyw, potem (e) w dol (pila) | PRAWDZIWA | `CapNetTown`: miasto z KS przy Ks Farm Follows Surplus + Ks Farm Surplus Cap Net - udzial ponad 1 liczony jak 1 w Review, Renew i `old` |
| 8 - cel D2K-5 <= 14 dla miast z firma 0 nieosiagalny z konstrukcji | PRAWDZIWA we wniosku; rachunek recenzenta czesciowo bledny | D2K-5 pomija doby z `pobrane` <= 0 (narzedzie, blok D2K-5) - Volantis w P2 ma `pobrane` > 0 dopiero od 108769 (kasa ponad 1.5 x zapas od 108768; 108769-108877 = 109 dob = D2K-5 w P2), wiec seria liczy sie praktycznie od 1.5 x zapas, nie od 1.02 x (nie 46 dob). Najkrotsza droga (f) = pelny cykl 14 dob + do 7 dob do dnia raty (gersuma placi od doby (f)) = 14-21 dob, wiec <= 14 tylko przypadkiem. Cel biegu A: D2K-5 <= 21; nowe INFO M3P-12. Progu (f) nie zmieniam: ten sam warunek co podwyzka, a cykl 14 dob i tak wyklucza <= 14 |
| 9 - opis MCM "Ds Farm Fix Share Cap" nieprawdziwy dla osad bez doplywu | PRAWDZIWA (dla starej wersji (d)) | nowy opis (wspolczynnik swiata obejmuje tez osady bez doplywu); opis "Ks Farm Surplus Cap Net" uzupelniony o krok i przeglad zwykly |

**Zmiana (`DsTowns.cs`, `Settings.cs` + `McmSettings.cs` z gen_mcm, `tools/sprawdz_logi.py`):**
- (d) `Fix` - dwa przejscia. Pierwsze jak dotad liczy firmy z okna i udzial swiata przed sufitem `sF0/sN` (osady z doplywem netto > 0 i firma > 0 - ta sama miara co `_worldShare` w 101). Przy `DsFarmFixShareCap` i `sF0 > sN`: `k = sN / sF0`; drugie przejscie mnozy `FL` i `FC` WSZYSTKICH osad z firma > 0 (miasta i zamki, takze z doplywem okna <= 0 albo bez pomiaru) przez `k`; FLf bez zmiany (placi lud). `Share = Min(1, dayF/net)` przy doplywie > 0, inaczej NaN; `_worldShare` po wspolczynniku = `min(1, przed)`. Kolejnosc osad zostaje, nie ma firm bliskich 0 ani skoku na zerze. Osade ponad wlasny doplyw sprowadza potem (e) z KS albo przeglad zwykly (Share <= 1). Klucz NIE: `k = 1`, Share i sumy jak 101 co do monety.
- (a) `SurplusReview` przy `KsFarmSurplusCapNet`: `add = Min(grow*364, mx*Max(F, doplyw*364))`, potem sufit `room = Max(0, doplyw*364 - F)` jak dotad (licznik "przyciete sufitem" - to samo znaczenie). Firma ponizej zysku dochodzi do niego najwyzej w 1/mx cyklach (mx 0.25 - 4 cykle, 56 dob); firma przy zysku - dalej mx x F.
- (f) `FromZero`: `add = Min(grow*364, mx*doplyw*364)` - ten sam krok (dotad caly doplyw od razu); `Share = add/364/doplyw` (<= mx), kolejne cykle podnosza go razem z firma (jak dotad przy podwyzce).
- Przeglad zwykly: `CapNetTown(f, s)` (Ks Farm Follows Surplus + Ks Farm Surplus Cap Net + miasto z KS) - w `Review` i `Renew` udzial > 1 liczony jak 1; w `SurplusReview` `old` > 1 jak 1. Zamki, miasta bez KS, klucze wylaczone - jak 101.
- Linia FIRMY USTALONE przy (d): `(Ds Farm Fix Share Cap TAK: przed sufitem X (miasta A, zamki B); wspolczynnik swiata k - firmy z kasy wszystkich osad x wspolczynnik: miasta N o Y zl/rok, zamki M o Z zl/rok; firmy bez doplywu netto okna (doplyw <= 0 albo bez pomiaru): K osad, W zl/rok; udzial z firmami bez doplywu U; osad z pomiarem doplywu P z Q)` - P = osady z >= 7 dobami pamieci doplywu (po wczytaniu w oknie pamiec jest krotsza; 0 osad - `k = 1`, bez sufitu).
- Linia DS, odcinek m103 P3: na koncu `; swiat z firmami bez doplywu X` (wszystkie firmy z kasy / doplyw osad z doplywem > 0).
- MCM (gen_mcm, Armoury 2248 ustawien - bez nowych kluczy): "Ds Farm Fix Share Cap" - "all farms paid from the purse together are set no higher than what the merchants took in over those weeks - if the old dues took more, every farm is lowered by the same share (each place keeps its rank, also a place whose merchants took in nothing in those weeks), and a place still above its own takings is brought down later by the farm reviews"; "Ks Farm Surplus Cap Net" - krok "at most Ks Farm Surplus Review Max of the farm or of those takings, whichever is larger", firma od zera "at that same pace", "The quarterly farm review of such a town never aims above its merchants' takings either".
- Narzedzie `--grupa m103p3`: regex `fixcap` w nowym formacie; M3P-4 = udzial po == min(1, przed) i wspolczynnik == min(1, 1/przed) (+-0.0015) - arytmetyka zamiast miary z konstrukcji; M3P-4b INFO firmy bez doplywu przy ustaleniu i udzial z nimi (odniesienie kasy-miast.csv, kod 101: P2 40 miast 7 563 088 zl/rok, P0 38 / 5 435 105, 04-57-25 13 / 3 559 595); M3P-5 opis z modelem wspolczynnika; M3P-6b INFO udzial z firmami bez doplywu na koniec; M3P-12 INFO najdluzsza seria bez wplaty panu przy kasie > 1.5 x zapas (kod 101: P2 Volantis 110, Highgarden 53; P0 Starfall 10; 04-57-25 Selhorys 64, Valysar 41, Menetragos 40, New Ghys 31, Bolozo 28, Sharp Point 25 - 8 serii ponad 21 dob). Sprawdzone: P2, P0, 04-57-25 kod 0 (bez segmentow - INFO jak dotad); linie syntetyczne w nowym formacie (`scratchpad/p3rev/mk.py`): M3P-4 TAK przy k 0.787 i przed 1.270, NIE przy k 1.000; M3P-4b, M3P-6, M3P-6b czytaja.

**Model wspolczynnika swiata (udzial przed sufitem z logu: P2 1.270, P0 1.282, 04-57-25 0.979):**
- Firmy przy ustaleniu: P2 miasta 35.16 -> ok. 27.7 mln zl/rok, zamki 10.25 -> ok. 8.1 mln (k 0.787); P0 miasta 35.71 -> ok. 27.9 mln, zamki 10.73 -> ok. 8.4 mln (k 0.780); 04-57-25 bez zmiany (k 1.000; wersja na osade ciela tu 44 miasta o 18.5 mln). Dochod panow z firm przy ustaleniu ok. -21% (wersja na osade: miasta -29%).
- Myrth (P2): 502 tys. -> 395 tys.; do pozniejszego zysku 3.27 mln krokiem `mx x max(F, doplyw x 364)` ok. 4 cykle przy dosc duzym przyroscie kasy (wersja na osade: 44 tys. i ok. 19 cykli).

**Kontrola calosci (CLAUDE.md 8.0):**
- `Fix`: te same pola co dotad (FL, FC, FLf, AidCut, Share, Fixed, ATerms, SLow, `_worldShare`); dwa przejscia, kazda osada we wlasnym try w obu. DsSeason (nadania wojenne - `SwitchDue` tej doby) i `LordFarmPerDay` zamkow (Z8) czytaja FL/FC po wspolczynniku - tak samo jak dotad po sufitcie na osade.
- (a)/(f): `CatchUpDecide` liczy prog gersumy z F po podwyzce (wieksza firma - mniejsza gersuma; jednorazowe przechodzi w staly dochod szybciej); liczniki KsFolkIncome (`NoteRaise`, `NoteCapped`, `NoteFromZero`, `NoteShareUp`) - te same znaczenia. Klucz NIE - `add = Min(grow*364, mx*F)` jak 101.
- `CapNetTown`: czyta tylko klucze i `StartCalibration.KsTownApplied` (ten sam warunek, ktorym SurplusReview wybiera miasta); NaN nie wchodzi (NaN > 1 to falsz). `SurplusHolds` dalej wstrzymuje obnizke przy kasie ponad 1.5 x zapas, przeglad wymuszony bez zmian.
- Zamknieta ekonomia: zloto sie nie rusza - zmienia sie tylko nalezna rata (jak w calym P3); kontrola "platnicy - odbiorcy" bez zmian.
- Inne czytniki zmienionych linii: `FIRMY USTALONE` i odcinek "udzial firm w doplywie netto (m103 P3)" czyta tylko grupa m103p3 (grep po `tools/`).

**Ryzyko / co sprawdzic:**
- R1' (d): wspolczynnik jest jeden dla miast i zamkow. Z kasy-miast.csv (same miasta) udzial miast przed sufitem: P2 1.434, P0 1.412, 04-57-25 1.018. Miasta razem zostaja wiec ponad doplywem (P2 ok. 1.13), a zamki (szacunek z udzialu swiata: ok. 0.96) schodza ponizej (ok. 0.76). Nadwyzke miast zdejmuje (e) z KS albo przeglad zwykly (-15%/kwartal, Share <= 1). Zamki placa mniej: mniejsze zaleglosci zamkow (cel M3P-7), mniejszy dochod panow z zamkow. Linia FIRMY USTALONE podaje udzial miast i zamkow przed sufitem osobno. Jesli zamki wyjda wyraznie ponizej 1, do decyzji wspolczynnik osobno dla miast i zamkow (miasta P2 wtedy ok. 24.6 mln, ponizej celu 28-33).
- R2' dochod panow z firm przy ustaleniu ok. -21% - C-4c (P2 250 przy granicy 250) i NAKUM-7 dalej do sprawdzenia.
- R3' jeden krok (a): szybsze podwyzki miast z firma duzo ponizej zysku (do 4 cykli) - mniej gersumy, wiecej stalej raty; kasy: NAKUM-26, G1-5a, NKA-3, NAKUM-27 (podwyzka tylko przy przyroscie kasy > 0 i kasie po naleznosciach > 1.5 x zapas, sufit zysku, (e) w dol).
- R4' D2K-5 dla miast z firma 0: 14-21 dob zgodnie z regula. Dluzsza seria - sprawdzic w kasy-miast.csv restart cyklu (kasa po naleznosciach pod 1.5 x zapas), domy najpierw albo kase bez przyrostu (M6 zdejmuje nadwyzke - wtedy firma i gersuma 0 z reguly).
- R5' stary zapis (firmy ustalone kodem 101): (d) nie biegnie; miasta z KS - udzial > 1 liczony jak 1 (przeglad zwykly i SurplusReview), (e) tnie przy niskiej kasie; zamki i miasta bez KS zachowuja udzial > 1 w przegladzie zwyklym (jak 101 - poza zakresem uwagi 5).
- R6' po wczytaniu w oknie pomiaru: `k` z 7-27 dob pamieci (na poziomie swiata szum sie usrednia); mniej niz 7 dob - `k = 1`, firmy bez sufitu (linia: "osad z pomiarem doplywu 0 z N"). Zapisu pierscienia doplywu nie dodaje (zmiana formatu zapisu dla rzadkiego przypadku).

**Plan testu (poprawiony):**
- Bieg A (prolog 230 dob z `armoury-ks-auto.flag`, wobec P2): cele D1 jak wyzej, z poprawkami: D2K-5 <= 21 dob (P2 109); M3P-4 TAK (arytmetyka), M3P-4b i M3P-6b INFO wobec P2 (40 miast, 7.56 mln); M3P-5 miasta ok. 27-28 mln (cel D1 28-33); M3P-12 dla miast z firma 0 ok. 21 dob albo mniej.
- Bieg B (160 dob z zapisu z KS, jak `04-57-25`): firmy ustalane W TRAKCIE biegu (109131) - (d) dziala; przy udziale swiata jak w 04-57-25 (0.979) wspolczynnik 1.000 - firmy bez zmian, M3P-4 i M3P-5 daja wynik (nie INFO). B sprawdza (a), (b), (c), (e), (f) na firmach ustalonych z udzialem <= 1. Cele B bez zmian (M96D-3 <= 105 tys., niewyplacalnych <= 2, dlug miast <= 0.5 mln, firmy miast < 231 mln, NAKUM-27 <= 14).
- Bieg C (nowy - sytuacja kampanii Jeffa z firmami juz ustalonymi kodem 101): zapis zrobiony PO dobie FIRMY USTALONE kodem 101 (udzial swiata 1.27 i Share > 1 w zapisie), np. autozapis biegu P2 po 108703 albo 04-57-25 po 109131, jesli zostal; zapis kampanii Jeffa tylko jako kopia i nie w czasie jego gry. Sprawdzic: brak linii FIRMY USTALONE (M3P-4 INFO), linia f4 - przeglad zwykly bez podwyzek miast z KS ponad doplyw, obnizki (e) (M3P-3), M3P-6/6b na koniec, kasy-miast.csv: pobor wobec doplywu (M3P-11).
- Wszystkie 146 grup + m103p3 przez `porownaj.py` wobec P2, P0 i 04-57-25; reszta jak w czesci 2.

**Build:** 5 modow kod 0 (Armoury 6 ostrzezen jak dotad, RealisticCaptivity 0, GrandTourney 0, ForgeView 0, CrashScribe 0); gen_mcm: Armoury 2248, RealisticCaptivity 115, GrandTourney 44. Gry nie uruchamialem, nic nie wgrane, nic nie wypchniete.

## Czesc 3: wsie pod KS (21.10; diagnoza P2 - journal wf_a568aafd-15a, diagnozy A "wsie" i B "lud miast")

**Status:** DO SPRAWDZENIA (nic nie wgrane, gra nieuruchamiana; test: bieg A - prolog 230 dob z `armoury-ks-auto.flag`, porownanie z P2 `10-08-39` i P0 `06-23-39`; bieg 102g `12-48-45` jeszcze trwal - nieuzyty).

### Sprawdzenie diagnoz (kod i log, przed zmiana)

- **Potwierdzone - Plan wycenia koszyk wsi przy cenach startu, wies placi cene polki.** `StartCalibration.Core.cs:1095-1104` (`StartIndex` przyciety do 0.7-1.5), `:1338` (`pz = ZlPerKg(StartIndex)`), `:1436-1449` (`budget = v.Alpha * salesB`, `KsBasket.Ladder` przy `pzu` = cena startu x gradient krainy); `TownFolk.Ks.cs` `ZcTake` placi `ZcPrice` = `zt.Px` (cena polki z doby, `StartCalibration.Zc4.cs:327`) i robi `b.T.ChangeGold(paid)`. Log P2, doba 108871/108877: budzet Planu W 56 495 + E 60 772 = 117 tys./d, koszyk zaplacony przy cenach startu 113 tys.; ten sam koszyk przy cenach polki (linia ZC4 "zaplacona z Planu") W 96 319 + E 94 783 = 191 tys. (x1.63); kupione 137 tys./d. Indeksy cen (linia "KS: ceny miast" 108871): grain 1.32, cheese 1.95, apple 2.32, grape 2.63, meat 2.54 - powyzej przyciecia 1.5 Planu.
- **Potwierdzone - kiesy wsi biednieja pod KS z wlasnych zakupow.** Linia "Przeplywy osad (kiesy wsi)": P2 8.80 mln (108713) -> 5.18 (108800) -> 3.91 (108877), wsi < 1000 zl 0 -> 38 -> 136; P0 9.87 -> 8.51 -> 8.12, 4-5 wsi. Akumulator: danina od skarbca wsi do panow P2 95 tys. (108705) -> 10 tys./d (108875), P0 97 -> 61 tys./d; ponad 3 x skrzynia P2 4.73 -> 0.53 mln, P0 4.85 -> 3.11 mln.
- **Potwierdzone - narzedzia i koszyk W1 to miara, nie pieniadz wsi.** Linia folk P2: "wsie 571: zakupy 137128 z 0" - potrzeba starego koszyka dnia `nvo = nv x (1 - lambda)` = 0 przy lambdzie 1, wiec NE3-16 = 0% z konstrukcji (`TownFolk.Live.cs:1270-1288`). Narzedzia (linia Towary, ostatnie 28 dob): wsie P2 9.3/d przy potrzebie wsi z koszyka ZC4 ok. 9/d (linia ZC4 "tools 15+8"), P0 51.7/d (stara lista z pieniedzy, nie z potrzeby). Narzedzi brakuje calemu swiatu: zuzycie 101-133/d przy produkcji 69-92/d, zapas -32..-41/d w obu biegach; pod KS bierze je budowa (P2 budowy 89-114/d, P0 17-23/d) - o przydziale decyduje kolejnosc, nie kiesa wsi. P2-5b (prog 42) i NE3-16 (15%) to progi starej gospodarki.
- **Diagnoza B (lud miast) - bez zmian kodu w tej czesci.** Potwierdzony brak przeplywu z kies ludu do kies wsi (utarg do kies wsi P2 169.0 wobec P0 167.9 tys./d). Kierunek (1) z diagnozy B (podzial zarobku ludu wedlug potrzeby) to osobna regula ludu miast - zostaje do osobnej decyzji (jedna zmiana naraz); jej skutki dla panow trzeba zmierzyc dopiero po P3 (bieg A).

### W1. Drabina 1 wsi przy cenach polki z budzetu z biezacego wplywu - klucz `KsVillageBasketAtShelf` (MCM "Ks Village Basket At Shelf", grupa "Start calibration (KS)"; TAK; NIE = kod 101)

**Problem:** pod KS (u Jeffa wlacza sie sama ok. 37. doby) kiesy wsi spadaja -0.205%/d (NDS-3a; P0 -0.064), wsi z kiesa < 1000 zl 24.3% (NDS-3d; P0 1.0%), danina od skarbca wsi do panow 9.8 tys./d (P0 61-99), ponad 3 x skrzynia 0.53 mln (P0 3.11). Roznica idzie do kas miast (`ZcTake`: `b.T.ChangeGold`).

**Przyczyna:** wies kupuje sztuki koszyka zaplaconego z Planu po cenie polki, a Plan przeliczyl budzet na sztuki przy cenach startu (przycietych do 0.7-1.5). Ten sam koszyk kosztuje 1.32-1.68 x budzet Planu. Drabina 1 nie ma zadnej granicy pieniadza poza pusta kiesa (`resGS` = tylko 7 dob ziarna i soli dla towarow po nich) - wies kupuje, az zejdzie do zera.

**Zmiana (`TownFolk.Ks.cs`, `StartCalibration.Money.cs`, `StartCalibration.R3.cs`, `StartCalibration.Zc4.cs`, `Settings.cs` + `McmSettings.cs` z gen_mcm):**
- Regula (100% wsi z koszykiem ZC4 i Ks Village Paid First): wies nie wydaje na drabine 1 wiecej, niz biezaco zarabia. Budzet zakupu = `lam x min(budzet Planu na dobe, pieniadze na koszyk z biezacego utargu)`, gdzie `lam` = lambda x dni zakupu (co 2. doba - 2).
- Pieniadze na koszyk z biezacego utargu = ten sam licznik co alfa Planu tej wsi, liczony na srednich 28 dob dzis (`VilBasketMoney`, wspolny dla Planu i gry): wies DS - `0.95 x (udzial z praw DS x oddane - czynsz staly)` (bez poborow z oszczednosci - danina od skarbca i podatek od nadwyzki biora z zapasu, nie z dochodu); klucz Ks Alpha Net - `0.95 x (do kiesy + fracht - wydatki spoza koszyka)`; inaczej `0.95 x do kiesy`. Mniej niz 7 dob pomiaru - sam budzet Planu. `VilAlphaFor` czyta licznik z tej samej funkcji - te same dzialania w tej samej kolejnosci (liczby Planu bez zmian).
- Ziarno i sol zawsze w calosci (glod) - ich koszt idzie z budzetu, ponad budzet z oszczednosci (jak dotad do pustej kiesy). Reszta (napoj, bialko najtansze za kcal, reszta, miasta obok F2) po kolei drabiny projektu ZC 8.4 do konca budzetu - jak przy pustej kiesie dalsze szczeble czekaja. Zakup zastepczy (piwo za wino, ziarno na piwo) w tym samym budzecie.
- Przyciete sztuki nie przechodza na jutro (pieniadz zostaje w kiesie). Licza sie w chcianych drabiny 1 i pelnego koszyka (Want, KcalW, ZlW - bez pokrycia, racje pracy domowej i NKS-13 dalej licza pelny koszyk), osobno od "bez kiesy" i "bez towaru". W miastach obok przyciete schodzi z braku towaru (nie liczy sie drugi raz i nie idzie do sterowania rynku jako popyt bez towaru - `KsUnmet`).
- Drabina 2 (ponad plan, z oszczednosci ponad skrzynie i tylko przy cenie <= 0.8 x startu), wies je swoje (G3 - wobec kcal zaplaconych z Planu: brak kcal z ceny pokrywa ta sama regula co brak towaru), krok 5, danina od skarbca - bez zmian.

**Linia logu:** w "Koszyk stanow (ZC4)" za "zakupy wsi w miescie obok (brak na polce targu) N szt." przy kluczu (i zakupach drabiny 1): `| m103 wsie - drabina 1 przy cenach polki z budzetu z wplywu (Ks Village Basket At Shelf TAK): Westeros koszt koszyka zaplaconego z Planu przy cenach polki X / budzet Y zl (budzet Planu P, wplyw netto x 0.95 N), przyciete K szt. (Z zl); Essos ...; zakupow V, przycietych C, bez pomiaru wplywu B; ziarno i sol ponad budzet G zl w M zakupach`. Klucz NIE - linia jak 101.

**Narzedzie (`tools/sprawdz_logi.py`, blok na koncu + 1 linia dispatch + opis grupy):** `--grupa m103wsie` (M3W-0..13): M3W-0 segment w kazdej linii ZC4 z zakupami wsi; M3W-1 kiesy wsi MNK 5 okien 26 dob >= -0.10%/d (miara NDS-3a); M3W-2 wsi < 1000 zl <= 5% (miara NDS-3d); M3W-3 zakupy / (wplyw netto + danina od skarbca wsi tej doby) <= 0.95 ("netto" linii ZC4 odejmuje tez danine od skarbca - pobor z oszczednosci, 60-100 tys./d przy kiesach jak w P0); M3W-4 ponad 3 x skrzynia >= 1.5 mln; M3W-5 danina od skarbca wsi do panow > 40 tys./d; M3W-6 koszyk wsi ZC4 (drabina 1 kupione / chciane - zamiast NE3-16) >= 15%; M3W-7 bez pokrycia <= 10% (W, E; z magazynu INFO); M3W-9 miasta stac na jedzenie >= 78% (P2 w tej mierze 78.1%); M3W-10 narzedzia wsi >= 30/d - DECYZJA, gdy koszyk ZC4 wsi chce mniej niz cel; M3W-11/12 budzet, przyciete (INFO); M3W-13 potkniecia i ERROR 0.
- Bieg 102g (czesc 1+2 bez W1, `Armoury-2026-10-21_12-48-45.log`, w toku - odczyt przy dobie 108861 o 13:31): M3W-1 -0.302..-0.173%/d, M3W-2 18.1%, M3W-4 0.44 mln, M3W-5 7 863 zl/d, M3W-7 W 10.6% / E 3.2%, M3W-3 0.92, M3W-9 78.0% - P3 nie naprawia wsi (ta sama przyczyna: koszyk przy cenach polki), W1 potrzebny niezaleznie od P3.
- Odniesienie: P2 NIE 4 (M3W-1 -0.213..-0.189%/d, M3W-2 24.3%, M3W-4 0.53 mln, M3W-5 9 812 zl/d), DECYZJA 1 (M3W-10 9.3/d przy potrzebie ZC4 9/d), TAK M3W-3 0.93 (surowo 1.03), M3W-6 82.4%, M3W-7 W 8.8% / E 4.8%, M3W-9 78.1%, M3W-13; P0 (bez KS) M3W-1 -0.082%/d, M3W-2 1.0%, M3W-4 3.15 mln, M3W-5 63 699 zl/d, M3W-10 51.7/d - wszystkie TAK. Linie syntetyczne (P2 z wstawionym segmentem): M3W-0 178/178, M3W-11 i M3W-12 czytaja. Segment czyta tylko m103wsie (grep po `tools/`: zaden inny regex nie siega za "brak na polce targu ... szt.").

**Kontrola calosci (CLAUDE.md 8.0):**
- Te same miejsca: `ZcTake` (budzet tylko przy `CapOn`, ustawianym wylacznie w `Zc4BuyCore` wokol `ZcLadder` - drabina 2 i inne wywolania bez budzetu), `ZcLadder` (ziarno i sol - `NoCap`; F2 - `InF2`), `Zc4BuyCore` (try/finally - liczniki i flagi zerowane takze przy wyjatku drabiny; wyjatek idzie dalej jak w 101 do `Villages(wies)`), `VilAlphaFor` (licznik z `VilBasketMoney`).
- Czytniki zmienionych liczb: `Zc4NotePass1` (chciane z przycietymi, bez towaru po F2 bez przycietych), `Zc4NoteTown`, `Zc4NoteBought` (KcalW/ZlW pelnego koszyka), `KsBoughtKcal` (mniej kcal kupionych -> `HomeKcal = want x min(frac, 1 - kupione)` rosnie najwyzej do czesci domowej Planu - racje spichlerza nie spadaja), `OwnFirst` (G3 - wiecej zjedzone z magazynu), `KsUnmet` (bez przycietych), `VillWin` (mniej zakupow - mniejsza podstawa robocizny targu miasta), `KsVilOut` (bez zmian - koszyk ZC4 nie jest wydatkiem spoza koszyka).
- Zamknieta ekonomia: zadnego nowego przeplywu - mniej zlota kiesa wsi -> kasa miasta; towar zostaje na polce. Kontrole ksiegi towarow i przeplywow osad bez zmian.
- Kolejnosc w dobie: srednie utargu (`MoneyEma`) przed `VillageTakings.Daily` i przed zakupami wsi (TownFolk) - budzet z tej samej doby co alfa.
- Stan: nic nowego w zapisie (srednie utargu, `_vtDays` i `_vilOut` juz sa w zapisie KS); liczniki linii zerowane w `Zc4Zero` (takze przy nowej sesji przez `Zc4Off`).
- Klucz NIE: `CapOn` falsz - `ZcTake` i drabina co do monety jak 101; `VilAlphaFor` te same liczby; linia bez segmentu.
- Koszt: `ShelfBudget` - jedna petla po ok. 40 towarach na zakup (ok. 288 zakupow na dobe) i dwa odczyty slownikow - ponizej 0.1 ms/d.

**Czego NIE zrobilem (z propozycji diagnozy A) i dlaczego:**
- (b) podloga kiesy (towary po ziarnie i soli tylko z kiesy ponad skrzynie): skrzynia to zapas NA koszyk (21 dob koszyka i czynszu - na czarna godzine); po W1 kiesa nie schodzi przez koszyk, a krok 5 i danina od skarbca zdejmuja tylko to, co ponad skrzynie / 3 x skrzynie. Podloga ze skrzyni wstrzymalaby napoj i bialko u kazdej wsi z kiesa ponizej skrzyni (P2 koniec: mediana kiesa / skrzynia 1.55, 136 wsi < 1000 zl) - zapas na czarna godzine nie bylby wydawany wlasnie wtedy, gdy wies go potrzebuje, a nadwyzka ponad skrzynie dalej szlaby na zbytki kroku 5. Do ponownej oceny, jesli bieg A pokaze M3W-2 > 5% (wolne odrabianie biednych wsi).
- (c) skrzynia z koszyka kupionego: po W1 kupione <= budzet Planu, wiec skrzynia z Planu jest tylko wyzsza o przyciete; zmiana dotyka czynszu, kroku 5 i danin od skarbca - osobny krok, jesli M3W-4/M3W-5 nie dojda do celu.
- (d) narzedzia >= 30/d: przy koszyku ZC4 potrzeba narzedzi wsi to ok. 8-9/d (tabela stanow `DailyLife.Basket` NonFood), wsie kupuja ja w calosci (9.3/d), a narzedzi brakuje calemu swiatu (zuzycie > produkcja, budowy biora 89-114/d). 30/d wymagaloby zmiany tabeli potrzeb stanow (caly Plan KS i wszystkie koszyki) albo kolejnosci przydzialu budowa/wies - decyzja projektowa, nie poprawka pieniadza wsi. Narzedzie: M3W-10 DECYZJA z liczbami. Koszyk W1: M3W-6 liczy drabine 1 ZC4 (NE3-16 przy lambdzie 1 zawsze 0%).

**Ryzyko / co sprawdzic (bieg A):**
- R1 jedzenie wsi: przyciete napoj, bialko i owoce (ziarno i sol nie). M3W-7 bez pokrycia <= 10% (P2 W 8.8%, E 4.8%), NKO-1 / MK-4 (kcal wsi, koszyk) nie gorzej. Racje spichlerza sie nie zmniejszaja (czesc domowa rosnie do granicy Planu), ale G3 je wiecej z magazynu - NKS-25 (wies je swoje <= 8%; P2 z magazynu W 2.1%, E 7.6%) i mniej towaru wsi na targ (utarg, NKS-24 dowoz).
- R2 kasy miast: mniej doplywu od wsi (szacunek 20-50 tys./d: P2 koniec zakupy 137 tys./d przy budzecie Planu 117 tys./d; diagnoza A 30-50 tys./d) - dobrze dla G1-5a (P2 +49.5%) i NAKUM-26 (P2 +7.8 tys./d), ale mniej podwyzek firm i gersumy - C-4c (P2 250, granica 217-250), NAKUM-7, KR-9, NKA-3/NAKUM-27 (miasta ponizej zapasu kupcow).
- R3 rynek KS: popyt ceny (`QV`) i zamowienia KS-H dalej z koszyka zaplaconego z Planu - wies kupi mniej, polki sie zapelnia, cena spadnie (to zamierzona droga powrotu do rownowagi), ale do tego czasu KS-H wozi ponad zakupy: NKS-7 / NKS-7e (ceny wobec celu), NKU, "nie wystawione (polka ponizej celu)" drabiny 2. Alternatywa (czystsza, wiecej ryzyka): Plan wycenia koszyk wsi mediana cen polki 28 dob zamiast przycietego `StartIndex` - po biegu A, jesli R3 wyjdzie zle.
- R4 miasto targowe wsi: mniejsze `VillWin` -> mniejsza podstawa robocizny targu (place ludu miasta z kasy) - linia f4/f5 ludu (diagnoza B: place W i tak w duzej czesci zatrzymywane f4/f5).
- R5 NKS-13: "kupione >= 90% zaplaconego z Planu" i "kupione / chciane" spadna z zalozenia (przyciete sa w chcianych); NKS-13b (bez towaru i bez kiesy) - bez przycietych. Oceniac M3W-12 i M3W-3.
- R6 wsie bez pomiaru utargu (< 7 dob w sesji i w zapisie) - sam budzet Planu (jak dotad co do sztuk przy cenach startu, ale w zl). Wies bez zadnego utargu od 28 dob (wozy rozbite, wojna) - budzet 0, kupuje tylko ziarno i sol.
- R7 zapis Jeffa z KS juz zastosowana: od pierwszej doby wsie przestaja zjezdzac w dol; biedne odrabiaja oszczednoscia ok. 5-15% wplywu na dobe (M3W-2 moze dochodzic do celu wolniej niz w nowym biegu).

**Oczekiwane progi (bieg A wobec P2):** M3W-1 / NDS-3a >= -0.10%/d (P2 -0.205); M3W-2 / NDS-3d <= 5% (P2 24.3%); M3W-3 <= 0.95; M3W-4 >= 1.5 mln (P2 0.53); M3W-5 > 40 tys./d (P2 9.8); M3W-6 >= 15%; M3W-7 <= 10%; M3W-9 >= 78%; M3W-0 segment w kazdej linii, M3W-13 0/0. Bez pogorszen: NAKUM-26 <= +30 tys./d, G1-5a <= 160% Z33, C-4c <= 250, NKS-13 (kiesy wsi, stac na jedzenie) i NKS-24 (P2 25 z 59 ponizej 0.8) nie gorzej, NKS-25 <= 8%. M3W-10 (narzedzia) - DECYZJA, nie cel tej zmiany.

**Build:** 5 modow kod 0 (Armoury 6 ostrzezen jak dotad, RealisticCaptivity 2 i ForgeView 4 - stare ostrzezenia w nieruszanych plikach, GrandTourney 0, CrashScribe 0); gen_mcm: Armoury 2249 ustawien (bylo 2248), RealisticCaptivity 115, GrandTourney 44. Gry nie uruchamialem, nic nie wgrane, nic nie wypchniete.
