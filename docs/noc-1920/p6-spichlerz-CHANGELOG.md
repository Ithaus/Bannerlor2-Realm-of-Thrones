## p6-spichlerz (20.10) - miasto je ze swoich wsi: spichlerz miejski (Armoury, R3-06f)

**Dla Jeffa (prosto).** Dotad kazde miasto dostawalo co dzien jedzenie "od swoich wsi", choc z ich stodol nic nie ubywalo - dlatego miasto glodowalo
tylko w oblezeniu. Teraz wies sprzedaje miastu tylko to zboze, ktore ma naprawde na zbyciu (stodola pelna ponad dwa lata wlasnego jedzenia), a miasto placi
za nie z kasy po cenie zboza na swoim targu. Gdy okreg sam zjada zapas, z wsi nic nie przychodzi i miasto zyje z targu i karawan; gdy zapas miasta spada nisko,
wsie sprzedaja drozej takze z zapasu ponad rok. Glod w miescie przychodzi dopiero wtedy, gdy puste sa i stodoly okregu, i targ (oblezenie, spalone wsie,
odciete drogi); BetterEconomy nie zabiera juz ludzi miastu, ktore ma jeszcze jedzenie w spichlerzu albo na targu. Wylacznik w MCM: "Town Granary From Villages".

**Problem / zrodlo pomyslu.** POMYSLY-35 pkt 16 (R3-06f), Jeff 20.10: "28 - nie, 29 - nie. Pozostale tak", potem "Rob od razu fale 2" (i po wstrzymaniu
"zacznij fale 2"). Projekt `docs/PROJEKT-GLOD-2026-10-07.md` rozdz. 2.13 krok B, rozdz. 4 pkt 4-5 (miasta gloduja tylko w oblezeniu), tabela BetterEconomy (:109 -
BE zabiera chlopow juz przy bilansie < -3, nie przy pustym spichlerzu). Dowod (test 160 dob na WGRANIU 91, `Armoury-2026-10-20_00-44-29.log` i
`spichlerze-regiony.csv`): bilans jedzenia miast mediana +163..+312/d (gra: (poziom wsi + 1) x 6 od kazdej wsi Normal + 15 "ziemie wokol" + jedzenie kupione
przez mieszczan z targu), zapas miast mediana ok. 29 tys. (blisko sufitu); stodoly wsi 706-726 dni (sufit 728 = 2 lata), plon 118% potrzeby przez 40 dob,
potem 94-95% - nadwyzka ponad sufit przepadala (0.004-7.7 mln racji/d). Skladnik wsi miasta nie schodzil ze stodol (jedzenie z niczego).

**Zmiana.** Nowy plik `Armoury/src/TownGranary.cs` + minimalne wpiecia (`SubModuleMain.cs` ApplyAll, `ArmouryBehavior.cs` linia Reset `_p6SpichlerzReset`,
EnsureHooks w OnSessionLaunched, Daily w bloku pieniadza po `TownFolk.FirewoodDaily` przed `Mark(MFolk)`), `Settings.cs` klucz `TownGranaryFromVillages`
(NOWY, domyslnie TAK, przy `TownFoodByVillagePeople`) + `tools/gen_mcm.py` (McmSettings.cs), `tools/sprawdz_logi.py` grupa `--grupa p6spichlerz` (SP-0..SP-13).
1. **Skladnik wsi miasta zdjety** (tylko MIASTA - 97; zamki bez zmian: woz wsi zamkowej i danina zalogi). Postfiks Last na `DefaultSettlementFoodModel
   .CalculateTownFoodStocksChange` (lancuch ROT -> BK -> baza; dekompilacja: BK dopisuje po bazie tylko "Local farmland" >= 0, ROT +100/+120 dla dwoch osad)
   odejmuje dokladnie to, co dzis daja wsie: `VillageBook.FortVillageFood` (gra albo E4/11 z czynnikiem rak - ta sama funkcja co targ przy zamku). Oblezenie - gra
   i tak nie liczy wsi. "Ziemie wokol" (+15) i pole BK miasta zostaja (wlasne pola miasta).
2. **Prawdziwa nadwyzka.** Co dobe, po `Famine.Daily` (migawka stodol tej doby), kazda wies miasta w stanie Normal, bez najazdu i bez glodu gospodarzy sprzedaje
   miastu to, co lezy w jej stodole ponad 98% sufitu (sufit = 2 lata potrzeby, f23) - to, co i tak przepadloby na sufice. Okreg zjada wlasny zapas (stodola
   ponizej 98%) - nic. Miasto bierze najwyzej tyle, ile ma miejsca (sufit - zapas - dzisiejszy dodatni bilans), wiec nic nie ginie na sufice gry.
3. **Niedobor miasta** (zapas ponizej 30 dni spozycia albo ponizej 30% sufitu zapasu - nad alarmem zastoju BK 25%): wies sprzedaje takze z zapasu ponad rok
   (50% sufitu), po cenie KUPNA targu (drozej), do podwojnego progu niedoboru na raz (bez zrzutu pol stodol okregu w jedna dobe).
4. **Zamknieta ekonomia.** Racje schodza ze stodoly (`Famine.TributeTake`) i od razu wchodza do `Town.FoodStocks` (racje / Q miasta - ta sama skala co Famine QOf:
   racje wsi karmia tylu mieszczan, ilu ludzi wykarmilyby na wsi). Zaplata: cena zboza na targu miasta za 1 szt. Food (gra liczy 1 szt. jedzenia z targu jako
   +1 Food), z nadwyzki kasy ponad zapas kupcow (`TownPurse.SpendableTown`; brak kasy - wies nie sprzedaje, zboze zostaje w stodole). Zloto: kasa miasta -> kiesa
   wsi (`ChangeGold` para) w naszym ticku - ksiega pieniadza lapie przelew migawkami BlockOpen .. Mark(MFolk) (kolumna "kiesa ludu" w wsie-kiesy.csv), bez nowej
   pozycji. Kontrola w linii: racje zdjete = Food x Q, zapas miast + = zakup, kasy miast - = kiesy wsi + = zaplata.
5. **Dymek i bilans.** Dzisiejszy zakup widac w bilansie Food miasta ("Grain from village barns") przez dobe; skladnik zdjety - "Villages keep their grain".
   W `Town.DailyTick` (prefiks/finalizer) zakupu nie ma - jest juz w zapasie, gra nie doda go drugi raz.
6. **Bezpiecznik BetterEconomy.** BE zabiera 0.5% chlopow i 0.4% rzemieslnikow na dobe, gdy `Town.FoodChange` < -3 (przy kazdym zapasie). Tylko w czasie
   tego testu (prefiks/finalizer `PopulationCampaignBehavior.OnSettlementDailyTick`) bilans miasta, ktore ma jedzenie (zapas Food na dobe spozycia albo tyle
   sztuk jedzenia na polce targu), nie spada ponizej progu BE. Gra, BK, Famine, glod KS i reszta widza prawdziwy bilans. Gdy zapas spadnie do 10% sufitu,
   BK sam dokupuje jedzenie z polki targu na brak (`EconomyPatches.ItemConsumptionPatch`, dekompilacja) - to jest droga "miasto je z targu"; glod BE zostaje,
   gdy nie ma nic ani w spichlerzu, ani na targu.
7. **Werbunek zalogi (regresja zdjetego skladnika).** Gra dobiera zalodze ludzi tylko przy `FoodChange > 0` (`GarrisonRecruitmentCampaignBehavior
   .CanSettlementAutoRecruit`). Dotad skladnik wsi trzymal bilans prawie kazdego miasta na plusie; teraz miasto z pelnym spichlerzem i ujemnym bilansem
   przestaloby werbowac. Tylko w tym tescie (prefiks/finalizer wpiety przy starcie gry, zanim JIT moglby wkleic te mala metode do wolajacego) bilans miasta bez niedoboru
   nie spada ponizej +0.01; miasto w niedoborze nie werbuje (jak w grze przy braku jedzenia).
8. **Linia logu co dobe** `Spichlerz miejski (p6): dzien N | ...`: zboze ze wsi do miast (szt./d Food i racje), zloto miasto->wies i cena, miasta w niedoborze,
   skladnik zdjety, miasta z bilansem < -3 (i bez oblezenia), miasta glodujace (Food 0, i bez oblezenia), BE (testy, oslony, glod BE bez jedzenia), werbunek
   zalogi, kontrola, potkniecia, czas. Start sesji: klucz, Famine, latki, BE, zaloga.

**Ryzyko / co sprawdzic (kontrola calosci).**
- Regresje: z bilansu znika (poziom + 1) x 6 na wies Normal (3-4 wsie po 6-18 = kilkadziesiat na dobe na miasto). Przy dzisiejszych liczbach wiekszosc miast dalej ma duzy plus z targu
  (mediana +163..+312/d), wiec zapas zostaje przy sufice i miasto prawie nic nie kupuje; na minus wejdzie ok. 5-20 miast (przed galezia 0-4) - zapas spada
  powoli (ok. 20-30 tys. / 50-100 na dobe), w 160 dobach nie powinny dojsc do niedoboru. Kto czyta `Town.FoodChange`: Famine (zgony mieszczan tylko przy Food 0,
  U27 dni zapasu - krocej), glod KS (tylko przy Food 0), BK dokup z polki przy <= 10% sufitu, BK ClusterFoodTracker (zastoj przy bilansie < 0 i zapasie < 25% sufitu
  przez 14 dob - nasz prog niedoboru 30% sufitu jest wyzej), BE: polityka AI "FoodRelief" i dryf zadowolenia cechow przy bilansie < -3 (bez oslony - prawdziwa
  reakcja na ujemny bilans; INFO), gra: werbunek zalogi (oslona pkt 7), dobrobyt i glod zalogi tylko przy Food 0.
- Kolizje: postfiksy na tym samym celu (VillageBook, PeopleMarket, Famine, WinterBite - addytywne; nasz Last tylko odejmuje skladnik i dodaje zakup);
  SiegeWalls2 na czynnym modelu (oblezenie: bilans <= 0) - pod oblezeniem nic nie zdejmujemy. Stodoly wsi biora tez danina Muru (WallTribute, przed nami w dobie)
  i glodni Wolni Ludzie - kazdy przez `TributeTake` z tego, co lezy (bez podwojnego liczenia). Bramka KS krainy (StartCalibration.LandGateDaily, spadek stodol
  > 1% potrzeby przez 7 dob przy racjach KS ponizej wzoru gry): zwykla sprzedaz trzyma stodole na 98% (bez spadku z doby na dobe); zakup w niedoborze to
  jedna doba skoku, nie 7 z rzedu - ale miasto, ktoremu wsie moga dac tylko po troche, kupuje kilka dob z rzedu (sprawdzic w linii KS "spichlerze: krain ze
  spadkiem > 1%"). Drzewa n22f-* - grep w n22-calosc: zadnych zmian w jedzeniu miast i stodolach.
- Start nowej kampanii: gra daje miastom zapas = sufit (FoodConsumptionBehavior), wiec pierwszego dnia miasta nie maja miejsca - bez wielkich przelewow zlota
  na starcie (kalibracja KS bez zmian). Stary zapis: stan galezi bez zapisu (zakup dnia zyje do nastepnej doby; po wczytaniu dymek bez zakupu do pierwszego Daily).
- Skala zlota: cena za 1 szt. Food = cena zboza (np. 15-30 zl). Duze miasto w niedoborze moze zaplacic jednego dnia dziesiatki tysiecy (do nadwyzki kasy ponad
  zapas kupcow) - zloto idzie do kies wsi okregu (zamkniete). Sprawdzic w linii, czy kasy miast nie leca w dol.
- Q miasta (mieszczan na 1 Food) rozni sie miedzy miastami 50-1800, wiec cena racji dla wsi tez (cena za Food jest jedna) - swiadomie: miasto placi za to, co dostaje
  w swoim liczniku Food.
- Watki: postfiks moze isc z watkow AI - tylko odczyty, slownik dnia podmieniany w calosci, liczniki Interlocked; bez alokacji (ExplainedNumber bez opisow).
  Bezpiecznik BE i werbunku tylko na watku glownym (ThreadStatic). Potkniecia liczone per miejsce, bez globalnego wylacznika.
- Oslona BE pod oblezeniem `town_EW4` (ROT +100): SiegeWalls2 odejmuje dopisek ROT po naszym postfiksie, wiec ta jedna twierdza pod oblezeniem moze zejsc pod prog
  BE mimo zapasu (BE i tak ma wlasna kare oblezenia) - drobiazg, bez poprawki.
- Wylaczony klucz = gra bit w bit. Famine wylaczone - skladnik wsi zostaje (nie ma czego sprzedac), bezpiecznik BE dziala, oslona werbunku nie.

**Decyzje (sam, z wylacznikiem `TownGranaryFromVillages`).**
- Sprzedaz za cene targu, nie danina pana: danina od "nadwyzki ponad zapas" to pobor resztowy (Jeff 17.10 - bez poborow resztowych), a sprzedaz tego, czego
  wies nie zmiesci, to zwykly targ. Wzor: zamki - woz wsi zamkowej sprzedaje zamkowi jedzenie.
- Nadwyzka = ponad 98% sufitu stodoly (jak zapas Muru w WallTribute, CapFill 0.98) - wies trzyma 2 lata; w niedoborze miasta - ponad rok, drozej.
- Bezpiecznik BE jako dolna granica bilansu tylko w tescie BE (nie dopisuje jedzenia z niczego); jedzenie z targu do spichlerza miasta - istniejaca droga BK
  (dokup z polki przy <= 10% sufitu) i jedzenie kupione przez mieszczan (SoldItems), bez nowego przelewu.
- Oslona werbunku zalogi - poprawka regresji (bez niej kilka-kilkanascie miast z pelnym spichlerzem przestaloby werbowac).
- Przelew w pozycji MFolk ksiegi pieniadza (bez nowej pozycji Note169 / M) - 7 galezi fali 2 scalanych razem; liczby w naszej linii.

**Test w grze (autotest, 120-160 dob).** `python -I tools/sprawdz_logi.py --grupa p6spichlerz <Armoury-*.log>`; grep:
`Spichlerz miejski (p6): latki przy starcie gry`, `Spichlerz miejski (p6): start sesji`, `Spichlerz miejski (p6): dzien`, `ERROR in TownGranary`.
Oczekiwane: SP-1/SP-2 wpiete (BE i zaloga), SP-4 skladnik zdjety w ok. 95 miastach, SP-5 kontrola 0, SP-6 zboze > 0 (gdy wsie maja nadwyzke - przy plonie
118%), SP-8 miasta przy Food 0 bez oblezenia <= 2, SP-10 testy BE ok. 97/d i glod BE bez jedzenia tylko przy Food 0, SP-12 czas <= 10 ms, SP-13 0.
Do porownania z WGRANIEM 92: linia "Spichlerze (E4/6)" (stodoly swiata, nadwyzka ponad sufit - powinna spasc), "Glod miast (KS)" (glodne), NKS-15
(zgony z glodu poza oblezeniami 0), "Jedzenie z targu (E5/0)" (zapas warowni razem), linia KS "spichlerze: krain ze spadkiem > 1% potrzeby/d".

**Status.** DO SPRAWDZENIA (build Armoury Release: kod 0, 0 Error(s); autotestu nie uruchamialem - zakaz w zadaniu; nic nie wgrane).
