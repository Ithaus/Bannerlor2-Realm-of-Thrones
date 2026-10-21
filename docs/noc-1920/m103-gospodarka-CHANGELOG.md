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
