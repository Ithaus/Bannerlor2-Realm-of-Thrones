## p6-spichlerz (20.10) - miasto je ze swoich wsi: spichlerz miejski (Armoury, R3-06f)

**Dla Jeffa (prosto).** Dotad kazde miasto dostawalo co dzien jedzenie "od swoich wsi", choc z ich stodol nic nie ubywalo - dlatego miasto glodowalo
tylko w oblezeniu. Teraz wies sprzedaje miastu tylko to zboze, ktore ma naprawde na zbyciu (stodola pelna ponad dwa lata wlasnego jedzenia), a miasto placi
za nie z kasy po cenie zboza na swoim targu; gdy zapas miasta spada nisko, wsie sprzedaja drozej takze z zapasu ponad rok. Glod w miescie przychodzi dopiero
wtedy, gdy puste sa i stodoly okregu, i targ (oblezenie, spalone wsie, odciete drogi) - BetterEconomy nie zabiera ludzi i nie oglasza "glodu" miastu, ktore
ma jeszcze jedzenie, a zaloga dalej sie werbuje. Do obejrzenia w tescie: miasta, ktore jedza wiecej, niz daja ich wsie, trzymaja nizszy zapas i czesc z nich
straci premie Banner Kings do lojalnosci za syte miasto (zapas od 80% sufitu) - wylacznik w MCM: "Town Granary From Villages".

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
   pozycji. Kontrola w linii (mierzona, nie z tych samych zmiennych): racje zdjete = ubytek stodol wsi (`Famine.FoodOf` przed i po), zapas miast + = zakup,
   kasy miast - = kiesy wsi + = zaplata.
5. **Dymek i bilans.** Dzisiejszy zakup widac w bilansie Food miasta ("Grain from village barns") do konca doby zakupu i tylko do wysokosci dzisiejszego braku
   (bilans bez zakupu ponizej 0) - nadwyzke zakupu pokazuje sam zapas; skladnik zdjety - "Villages keep their grain". W `Town.DailyTick` (prefiks/finalizer)
   zakupu nie ma - jest juz w zapasie, gra nie doda go drugi raz. Rano nastepnej doby (Famine, glod KS przed naszym Daily) wczorajszego zakupu w bilansie nie ma.
6. **Bezpiecznik BetterEconomy.** BE zabiera 0.5% chlopow i 0.4% rzemieslnikow na dobe, gdy `Town.FoodChange` < -3 (przy kazdym zapasie); ten sam prog
   przelacza miasto AI na polityke FoodRelief, wybiera projekt Granaries i obniza poparcie cechow (pospolstwo -0.22/d, wlasciciele ziemi -0.15/d). Tylko
   w czasie dobowych tickow BE (prefiks/finalizer `PopulationCampaignBehavior.OnSettlementDailyTick` i `TownEconomyCampaignBehavior.OnSettlementDailyTick`)
   bilans miasta, ktore ma jedzenie (zapas Food na dobe spozycia albo tyle sztuk jedzenia na polce targu), nie spada ponizej progu BE. BE dalej sam patrzy
   na FoodStocks < 20 (prawdziwy pusty spichlerz). Gra, BK, Famine, glod KS i reszta widza prawdziwy bilans. Gdy zapas spadnie do 10% sufitu, BK sam dokupuje
   jedzenie z polki targu na brak (`EconomyPatches.ItemConsumptionPatch`, dekompilacja) - to jest droga "miasto je z targu"; glod BE zostaje, gdy nie ma nic ani
   w spichlerzu, ani na targu.
7. **Werbunek zalogi (regresja zdjetego skladnika).** Gra dobiera zalodze ludzi tylko przy `FoodChange > 0` (`GarrisonRecruitmentCampaignBehavior
   .CanSettlementAutoRecruit`). Dotad skladnik wsi trzymal bilans prawie kazdego miasta na plusie; teraz miasto z pelnym spichlerzem i ujemnym bilansem
   przestaloby werbowac. W czasie dziennego ticku zalogi (prefiks/finalizer na `OnDailySettlementTick`, wpiety przy starcie gry) i w samej
   `CanSettlementAutoRecruit` (dymek zalogi) bilans miasta bez niedoboru, ktore ze starym skladnikiem wsi mialoby plus, nie spada ponizej +0.01. Miasto
   w niedoborze nie werbuje (jak w grze przy braku jedzenia); miasto z deficytem takze ze starym skladnikiem wsi - regula gry bez zmian.
8. **Linia logu co dobe** `Spichlerz miejski (p6): dzien N | ...`: zboze ze wsi do miast (szt./d Food i racje), wsie z nadwyzka / bez miejsca w miescie /
   z nadwyzka ponizej 0.05 Food / bez zaplaty, zloto miasto->wies i cena, miasta w niedoborze, skladnik zdjety, miasta z bilansem < -3 (i bez oblezenia), miasta
   glodujace (Food 0, i bez oblezenia), miasta ponizej 80% sufitu bez oblezenia (BK "Well fed"), BE (testy, oslony, glod BE bez jedzenia), BE gospodarka miasta
   (odczyty, oslony, bez jedzenia), werbunek zalogi (dzienne ticki miast i odczyty bilansu w nich, testy, oslony, deficyt jak w grze, niedobor), kontrola,
   potkniecia, czas. Start sesji: klucz, Famine, latki, BE (ludnosc i gospodarka miasta), zaloga.

**Ryzyko / co sprawdzic (kontrola calosci).**
- Regresje: z bilansu znika (poziom + 1) x 6 na wies Normal (3-4 wsie po 6-18 = kilkadziesiat na dobe na miasto). Przy dzisiejszych liczbach wiekszosc miast dalej ma duzy plus z targu
  (mediana +163..+312/d), wiec zapas zostaje przy sufice i miasto prawie nic nie kupuje; na minus wejdzie ok. 10-30 miast (przed galezia 1-9) - zapas spada
  (np. -180 - 50 = ok. -230/d przy ok. 29 tys.: 20% sufitu w ok. 25 dob), az miasto wejdzie w niedobor i kupi z zapasu wsi ponad rok. Kto czyta `Town.FoodChange`:
  Famine (zgony mieszczan tylko przy Food 0, U27 dni zapasu - krocej), glod KS (tylko przy Food 0), BK dokup z polki przy <= 10% sufitu, BK ClusterFoodTracker
  (zastoj przy bilansie < 0 i zapasie < 25% sufitu przez 14 dob - nasz prog niedoboru 30% sufitu jest wyzej), BK `BKRationDecision` (zapas < 20% sufitu
  i bilans < 0), BE (pkt 6 - oslona; bez oslony zostaja progi "bilans < 0": FeudalEconomy :459/:694 i VillageInvestment :480 - inwestycja w pola wsi miasta
  z ujemnym bilansem, zwykla reakcja gospodarcza, INFO), gra: werbunek zalogi (oslona pkt 7), dobrobyt i glod zalogi tylko przy Food 0.
- **BK lojalnosc "Well fed populace"** (BKLoyaltyModel :326-329: +0.5/d tylko przy zapasie >= 80% sufitu): miasta z ujemnym bilansem po zdjeciu skladnika
  wsi zejda ponizej 80% w kilka tygodni i strace +0.5/d lojalnosci (z czasem ryzyko buntu BK). To skutek realizmu (miasto zyje z zapasu, a nie z jedzenia
  z niczego), nie poprawiam modelu BK - liczba w linii doby ("miasta ponizej 80% sufitu zapasu bez oblezenia") i SP-9b; porownac z WGRANIEM 92, decyzja Jeffa.
- Kolizje: postfiksy na tym samym celu (VillageBook, PeopleMarket, Famine, WinterBite - addytywne; nasz Last tylko odejmuje skladnik i dodaje zakup);
  SiegeWalls2 na czynnym modelu (oblezenie: bilans <= 0) - pod oblezeniem nic nie zdejmujemy. Stodoly wsi biora tez danina Muru (WallTribute, przed nami w dobie)
  i glodni Wolni Ludzie - kazdy przez `TributeTake` z tego, co lezy (bez podwojnego liczenia). Bramka KS krainy (StartCalibration.LandGateDaily, spadek stodol
  > 1% potrzeby przez 7 dob przy racjach KS ponizej wzoru gry): zwykla sprzedaz trzyma stodole na 98% (bez spadku z doby na dobe); zakup w niedoborze to
  jedna doba skoku, nie 7 z rzedu - ale miasto, ktoremu wsie moga dac tylko po troche, kupuje kilka dob z rzedu (sprawdzic w linii KS "spichlerze: krain ze
  spadkiem > 1%"). Drzewa n22f-* i galezie fali 2 (p6-*) - grep: zadnych latek na `OnDailySettlementTick` zalogi, `CanSettlementAutoRecruit`
  i `TownEconomyCampaignBehavior.OnSettlementDailyTick` (GoodsLedger ma na nim ramke ksiegi towarow - addytywnie).
- Start nowej kampanii: gra daje miastom zapas = sufit (FoodConsumptionBehavior), wiec pierwszego dnia miasta nie maja miejsca - bez wielkich przelewow zlota
  na starcie (kalibracja KS bez zmian, nie czyta `Town.FoodChange`). Stary zapis: stan galezi bez zapisu (zakup dnia widac do konca doby zakupu; po wczytaniu
  dymek bez zakupu do pierwszego Daily).
- Skala zlota: cena za 1 szt. Food = cena zboza (np. 15-30 zl). Duze miasto w niedoborze moze zaplacic jednego dnia dziesiatki tysiecy (do nadwyzki kasy ponad
  zapas kupcow) - zloto idzie do kies wsi okregu (zamkniete). Sprawdzic w linii, czy kasy miast nie leca w dol.
- Q miasta (mieszczan na 1 Food) rozni sie miedzy miastami 50-1800, wiec cena racji dla wsi tez (cena za Food jest jedna) - swiadomie: miasto placi za to, co dostaje
  w swoim liczniku Food.
- Watki: postfiks moze isc z watkow AI - tylko odczyty, ksiega dnia (slownik + doba) podmieniana w calosci, liczniki Interlocked; bez alokacji (ExplainedNumber
  bez opisow). Bezpiecznik BE i werbunku tylko na watku glownym (ThreadStatic). Potkniecia liczone per miejsce, bez globalnego wylacznika.
- Oslona BE pod oblezeniem `town_EW4` (ROT +100): SiegeWalls2 odejmuje dopisek ROT po naszym postfiksie, wiec ta jedna twierdza pod oblezeniem moze zejsc pod prog
  BE mimo zapasu (BE i tak ma wlasna kare oblezenia) - drobiazg, bez poprawki.
- Wylaczony klucz = gra bit w bit. Famine wylaczone - skladnik wsi zostaje (nie ma czego sprzedac), bezpiecznik BE dziala, oslona werbunku nie.

**Decyzje (sam, z wylacznikiem `TownGranaryFromVillages`).**
- Sprzedaz za cene targu, nie danina pana: danina od "nadwyzki ponad zapas" to pobor resztowy (Jeff 17.10 - bez poborow resztowych), a sprzedaz tego, czego
  wies nie zmiesci, to zwykly targ. Wzor: zamki - woz wsi zamkowej sprzedaje zamkowi jedzenie.
- Nadwyzka = ponad 98% sufitu stodoly (jak zapas Muru w WallTribute, CapFill 0.98) - wies trzyma 2 lata; w niedoborze miasta - ponad rok, drozej.
- Bezpiecznik BE jako dolna granica bilansu tylko w dobowych tickach BE (ludnosc i gospodarka miasta; nie dopisuje jedzenia z niczego); jedzenie z targu do
  spichlerza miasta - istniejaca droga BK (dokup z polki przy <= 10% sufitu) i jedzenie kupione przez mieszczan (SoldItems), bez nowego przelewu.
- Oslona werbunku zalogi - poprawka regresji, tylko dla miast, ktorym zdjecie skladnika zabralo plus (miasta z deficytem sprzed galezi - regula gry).
- Zakup dnia w bilansie najwyzej do wysokosci dzisiejszego braku (stopa dobowa nie udaje jednorazowego zakupu w niedoborze).
- BK "Well fed populace" (80% sufitu) - bez zmian w modelu BK, tylko pomiar (decyzja Jeffa po tescie).
- Przelew w pozycji MFolk ksiegi pieniadza (bez nowej pozycji Note169 / M) - 7 galezi fali 2 scalanych razem; liczby w naszej linii.

**Recenzja (20.10, dwie soczewki: gra i technika) - co sprawdzone i co poprawione.**
- WAZNE (gra) - oslona werbunku moze nie dzialac w dziennym ticku: PRAWDZIWE ryzyko. Dekompilacja: `BetterEconomySubModule.OnSubModuleLoad` ->
  `_harmony.PatchAll()`, `RecruitmentPatch` ma Prefix/Postfix na `GarrisonRecruitmentCampaignBehavior.OnDailySettlementTick`; Harmony kompiluje zastepnik
  przy latce (przed nasza latka w `OnBeforeInitialModuleScreenSetAsRoot`), a JIT mogl wkleic do niego mala `CanSettlementAutoRecruit`. MonoMod.Core
  (`PlatformTripleDetourFactory` :552 `TryDisableInlining(request.Source)`, `FxCoreBaseRuntime` ma `RuntimeFeature.DisableInlining`) wylacza wklejanie
  dopiero zalatanej metody - juz skompilowanego wolajacego nie naprawia. POPRAWIONE: okno testu na samym wolajacym (prefiks/finalizer na
  `OnDailySettlementTick`, wpiety po latce `CanSettlementAutoRecruit` - Harmony buduje zastepnik od nowa); w tym ticku `FoodChange` czyta tylko
  `CanSettlementAutoRecruit` (dekompilacja gry; modele zalogi BK/ROT go nie czytaja), wiec okno dziala niezaleznie od wklejania. Licznik dziennych tickow
  i odczytow bilansu w linii doby, SP-11 - twardy prog (80-200 tickow/d, odczyty >= polowa).
- drobne (gra, technika) - "bez miejsca w miescie" liczyl tez mala nadwyzke wsi: PRAWDZIWE, POPRAWIONE (osobny licznik "nadwyzka ponizej 0.05 Food").
- drobne (gra, technika) - bezpiecznik BE tylko w `PopulationCampaignBehavior`: PRAWDZIWE (`TownEconomyCampaignBehavior` :1751 FoodRelief, :2272 Granaries,
  :2367 dryf cechow - wszystkie w `OnSettlementDailyTick` tej klasy, `TickGuilds` co dobe). POPRAWIONE: ta sama dolna granica w czasie tego ticku (pkt 6),
  licznik w linii i SP-10b. Progi "bilans < 0" (FeudalEconomy, VillageInvestment) - bez oslony, opisane w Ryzyku.
- drobne (gra) - opis MCM ok. 640 znakow: PRAWDZIWE, POPRAWIONE (ok. 250 znakow, `Settings.cs` + gen_mcm).
- drobne (technika) - oslona werbunku podnosila tez miasta z deficytem sprzed galezi: PRAWDZIWE, POPRAWIONE (warunek: bilans + zdjety skladnik wsi > 0;
  inaczej licznik "deficyt takze ze skladnikiem wsi (regula gry)").
- drobne (technika) - BK "Well fed populace" przy 80% sufitu nie opisane: PRAWDZIWE (BKLoyaltyModel :326-329), OPISANE w Ryzyku + licznik w linii i SP-9b.
- drobne (technika) - zakup w niedoborze jako stopa dobowa przez cala dobe (dymek, BK dokup, ClusterFoodTracker, Famine rano): PRAWDZIWE, POPRAWIONE
  (pkt 5: do wysokosci braku i tylko w dobie zakupu).
- drobne (technika) - kontrola "racje = Food x Q" to tautologia: PRAWDZIWE, POPRAWIONE (racje zdjete = zmierzony ubytek stodol wsi, `Famine.FoodOf` przed i po).

**Test w grze (autotest, 120-160 dob).** `python -I tools/sprawdz_logi.py --grupa p6spichlerz <Armoury-*.log>`; grep:
`Spichlerz miejski (p6): latki przy starcie gry`, `Spichlerz miejski (p6): start sesji`, `Spichlerz miejski (p6): dzien`, `ERROR in TownGranary`.
Oczekiwane: SP-1/SP-2 wpiete (bilans, Town.DailyTick, BE ludnosc i gospodarka miasta, zaloga na OnDailySettlementTick i CanSettlementAutoRecruit), SP-4
skladnik zdjety w ok. 95 miastach, SP-5 kontrola 0, SP-6 zboze > 0 (gdy wsie maja nadwyzke - przy plonie 118%), SP-8 miasta przy Food 0 bez oblezenia <= 2,
SP-10 testy BE ok. 97/d i glod BE bez jedzenia tylko przy Food 0, SP-10b odczyty BE gospodarki >= 80/d, SP-11 dzienne ticki zalogi ok. 97/d i odczyty bilansu
w nich ok. tyle samo, SP-12 czas <= 10 ms, SP-13 0. INFO do decyzji: SP-9 (miasta z bilansem < -3), SP-9b (miasta ponizej 80% sufitu - lojalnosc BK).
Do porownania z WGRANIEM 92: linia "Spichlerze (E4/6)" (stodoly swiata, nadwyzka ponad sufit - powinna spasc), "Glod miast (KS)" (glodne), NKS-15
(zgony z glodu poza oblezeniami 0), "Jedzenie z targu (E5/0)" (zapas warowni razem), linia KS "spichlerze: krain ze spadkiem > 1% potrzeby/d", BE
`town-econ: ... policyChanges` (log BE, gdy wlaczony - mniej zmian na FoodRelief).

**Status.** DO SPRAWDZENIA (build Armoury Release: kod 0, 0 Error(s); autotestu nie uruchamialem - zakaz w zadaniu; nic nie wgrane).
