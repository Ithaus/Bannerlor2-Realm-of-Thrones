## n20-wioska (noc 19/20.10) - start jako pan wioski pobocznej + menu wioski (Armoury)

**Problem.** Jeff 19.10 (doslownie): "czy mozemy naprawic to, ze zaczynam jako lord jakiejs wioski pobocznej! Nie wioski glownej tylko pobocznej, jest ona moja i daje mi
jakis dochod, wioska poboczna dostaje taka, w jakim krolestwie gram. I jestem tam opisany, ze ta wioska nalezy do mnie i moj herb przy niej. Czy do takiej wioski pobocznej
moge wjechac i dostac tylko dla tej wioski poboczne jakies opcje, jak nocleg, inwestuj, rozwijaj wioske". Dowod w kodzie (dekompilacja BannerKings.dll):
start BK "Indebted Lord" (`DefaultStartOptions.IndebtedLord`, opis "Start as a lord in a kingdom, with a Lordship title ... The village you own can be managed by you")
wola `ChangeKingdomAction.ApplyByJoinToKingdom(gracz, krolestwo kultury)` i `TitleManager.GiveLordshipOnKingdomJoin(krolestwo, gracz, force: true)`. Ta metoda bierze
tytul Lordship NAJMNIEJSZEJ (Hearth) wsi gry krola (albo pierwszego rodu z tytulem) i daje go graczowi (deJure i deFacto). Wioski poboczne Armoury (obrazki okregow,
ksiega wiosek) nie maja wlasciciela - gracz dostawal tytul wsi glownej, nie wioske poboczna.

**Przyczyna.** BK zna tylko wsie gry; wioski poboczne to nasz uklad (arm_map_villages*.tsv, VillageBook) - bez pojecia "wlasciciel wioski pobocznej".

**Zmiana.** Nowa klasa `Armoury/src/SideVillageFief.cs` (+ wpiecia):
- W1 START (klucz `SideVillageFiefStart`): prefiks na `TitleManager.GiveLordshipOnKingdomJoin` (cel `AccessTools.DeclaredMethod`, latka w OnSessionLaunched; jedyne
  wywolanie tej metody w BK to start "Indebted Lord" - grep dekompilacji). Dla rodu gracza tytul wsi gry POMINIETY (wies gry zostaje przy panu), gracz dostaje wioske
  poboczna: zwykla wioska okregu (nie przysiolek na pustkowiu; przysiolek i wioska uszkodzona dopiero w drugiej kolejce), stojaca w ksiedze wiosek, w okregu wsi gry, ktorej
  pan nalezy do krolestwa, do ktorego BK wlasnie przylaczyl gracza - najblizej partii gracza. Okno "Your village" + komunikat. Ksiega wiosek nieczynna - proba co godzine
  (oczekiwanie zapisane w zapisie: "sfp|krolestwo|doba"), po 3 dobach bez kandydata - tytul BK jak dotad (to samo wywolanie z obejsciem prefiksu).
- W2 ZAPIS: `arm_side_fief` przez SaveText.Sync: "sf1|uid|okreg|nazwa|doba nadania|wyplacone razem|srednia 7 dob". Brak klucza (stary zapis, Ironman Jeffa) = brak wioski.
- W3 DOCHOD (klucz `SideVillageFiefIncome`): przy powrocie wozu do wsi MACIERZYSTEJ okregu (VillageTakings.EnterPostfix, nowy krok `SideFief` po 112, F4b i pracach okregu,
  przed danina DS) udzial wioski w produkcji okregu (VillageCrafts.PartsOf: wartosc dnia wioski / suma okregu; zapas - ludzie w domu) x przyrost licznika podatku pana
  w tym oknie przechodzi z licznika pana (`TradeTaxAccumulated`) do kiesy gracza (`ChangeHeroGold`), w ksiedze pieniadza jak wyplata prac okregu
  (`MoneyLedger.CartWorksInWindow`). Zamknieta ekonomia: pan dostaje mniej dokladnie o tyle, ile dostaje gracz (to, co pan dotad bral z tej wioski). Wojna gracza z krolestwem
  okregu - bez przelewu; gracz panem okregu - bez przelewu (podatek i tak jego). Podpowiedz zlota (CalculateClanGoldChange czynnego modelu z opisami i BEZ pobrania -
  postfiks w OnSessionLaunched) - osobna linia "<wioska> (your village's share of the lord's tax)" ze srednia dob (pierwsze 7 dob srednia, potem wykladnicza 7 dob). Linia
  jest tylko widokiem: pieniadze ida przy wozie, nie z modelu finansow (bez podwojnej wyplaty).
- W4 MAPA: `VillageHolders.Of` - gospodarzem tej wioski jest gracz (nowa regula `RulePlayer`): herb rodu gracza na proporczyku (drugi proporczyk - herb pana, przy
  VillageTwoPennants), w dymku i w "Look over the district"; linia dymku "This village is yours, sworn to House X." Proporczyk gracza poza limitem tekstur herbow (CapCode).
  Przy wylaczonym VillageBannerPerHolder - to samo przez zapasy w MapVillagePennants.LordCode i MapVillageTooltip.
- W5 MENU (klucz `SideVillageFiefMenu`): klik na wlasna wioske na mapie = jazda do niej (`MapState.ProcessTravel`, wzor zajazdow przy szlakach; kursor raczki tylko na
  wlasnej wiosce, inne wioski jak dotad) i menu po dojechaniu; postoj przy niej - pytanie "Ride in / Ride on" raz na postoj; w menu wsi gry okregu "Visit X (your village)".
  Menu `arm_side_fief` (zwykle, nie oczekiwania; tlo - obrazek wsi gry okregu): "Rest the night here" (po recenzji - klucz `SideVillageFiefLodging`: nocleg POD DACHEM
  za darmo = stan noclegu karczmy jakosci "wspolna izba" z uid wioski, `TownInns.HostSleep` w nowym `TownInns.Host.cs`: sen w menu do switu, mnoznik snu i premia
  "wypoczeci" do zmierzchu +5% predkosci i +2 morale po przespanej bazie, jak wspolna izba w miescie; bez klucza, bez karczm (Town Inns / Night Rest), w wojnie
  z krolestwem okregu, przy rabunku wsi gry albo przy spalonej wiosce - sen jak w obozie, a podpowiedz mowi to wprost z powodem), "Invest in its works" (istniejace prace
  okregu - DistrictWorks.OpenPartFor = okno kwot tej jednej wioski; po recenzji wylaczona z powodem gry, gdy prac tej wioski nie da sie kupic - wycena raz na godzine),
  "Look over your village" (produkcja, udzial w okregu, dochod, jak to placi), "Leave" (z mapy ExitToLast, z wsi gry - z powrotem do menu wsi).
- Pliki: SideVillageFief.cs (nowy), TownInns.Host.cs (nowy, po recenzji), TownInns.cs (pole HostUid stanu noclegu, rozgalezienia w OnSleepInit / InnSleep, komunikat
  pobudki, zapis arm_inns pole 17), Settings.cs (4 klucze w grupie "District works and side-village crafts"), McmSettings.cs (gen_mcm, 2016 ustawien), ArmouryBehavior.cs
  (Reset, SyncData, TickEvent, HourlyTick, OnSessionLaunched: EnsureHooks + AddMenus, Daily), SubModuleMain.cs (SetHarmony), VillageTakings.cs (krok SideFief),
  VillageHolders.cs (RulePlayer, Of, Line), MapVillagesView.cs (klik / kursor wlasnej wioski), MapVillagePennants.cs, MapVillageTooltip.cs, DistrictWorks.Menu.cs (OpenPartFor).

**Ryzyko / co sprawdzic.**
- Regresje: obecny zapis Jeffa nie ma klucza arm_side_fief = brak wioski; prefiks BK dziala tylko na wywolanie dla rodu gracza (jedyne wywolanie w BK to start), wiec
  w toczacej sie kampanii nic sie nie zmienia. Klik / kursor / menu tylko przy wlasnej wiosce. Bez wioski gracza krok przy wozie konczy sie na pierwszym warunku.
- Kolizje: CalculateClanGoldChange patchuja tez CirculationWindows (O40) i SoldierPay - oba czytaja wynik tylko przy rozliczeniu rodu (applyWithdrawals = true), my
  dopisujemy tylko przy false + opisy (podpowiedz) - ich pomiary bez zmian. Linia "Daniny wsi (DS)" liczy "udzial pana" jako utarg - (kiesa + prace), wiec udzial gracza
  wpada tam do "pana" (tylko log). Prace okregu placa przed nami - nasz udzial jest od tego, co zostalo panu (bez podwojnego liczenia).
- Cudzy kod sprawdzony w dekompilacji: BK DefaultStartOptions.IndebtedLord, BKCampaignStartBehavior (start wybierany w oknie po kreatorze, RunStartOption po "Finish" - latka
  z OnSessionLaunched juz stoi), TitleManager.GiveLordshipOnKingdomJoin / GrantTitle (wies gry nie zmienia wlasciciela), MapScreen.HandleMouse / HandleLeftMouseButtonClick
  (selectedVisual -> OnMapClick, IsMobileEntity = kursor), MapState.ProcessTravel, ClanVariablesCampaignBehavior (dzienne saldo: includeDescriptions false, applyWithdrawals
  true), CampaignUIHelper (podpowiedz: includeDescriptions true, applyWithdrawals false).
- Watki: postfiks modelu finansow wolany tez z watkow AI - pierwszy warunek (opisy i bez pobrania) odpada bez alokacji; reszta tylko watek glowny.
- Nocleg pod dachem (po recenzji): ten sam stan noclegu `_rest` co karczma i zajazd (jeden stan naraz - wybor noclegu w wiosce zastepuje stan z karczmy, jak nowy nocleg
  w karczmie). Premia predkosci liczona rownolegle czyta tylko faze 2 (Live) - bez zmian; rozgalezienie HostNear (odleglosc partii od wioski) tylko w watku glownym
  (SleepTick, pobudka). Zamknieta ekonomia: nocleg bez oplaty - nikt nie placi i nikt nie dostaje, bez przeplywu zlota (jak stajnia/oboz). Zapis arm_inns: pole 16 zostaje
  puste (zajazd), pole 17 = uid wioski; starsza DLL czyta 15/16 pol - stan wyglada jak nocleg w wsi gry okregu i schodzi bez premii poza nia; stary zapis Jeffa nie ma
  pola 17 - jak dotad. Na mapie partia stoi poza osada, wiec przy snie stoi namiot (NightRest.SleepInit bez zmian - tak samo jak przy zajezdzie przy szlaku; zwykly nocny
  postoj i tak stawia namiot co godzine) - swiadomie bez zmian w NightRest.cs.
- Do sprawdzenia w grze: (1) nowa kampania, start "Indebted Lord" - okno "Your village", linia NADANIE, brak tytulu BK wsi gry; (2) herb gracza przy wiosce; (3) klik -> jazda
  -> menu; (4) linia w podpowiedzi zlota po pierwszej dobie z wozem; (5) nocleg z menu na mapie i z menu wsi gry (pobudka - mapa / menu wsi; po pelnym snie komunikat
  "Rested under your villagers' roofs: until ..." i w rozpisce predkosci "Slept in a bed"); (6) opcja "Invest in its works" przy wiosce z konmi - szara z powodem.

**Decyzje (podjete samodzielnie; wylaczniki).**
1. Ktora wioska: najblizsza partii gracza zwykla wioska poboczna (nie przysiolek), stojaca, w okregu wsi gry pana z krolestwa, do ktorego BK przylacza gracza - wylacznik
   `Side Village Fief Start` (off = tytul BK wsi gry jak dotad).
2. Tytul BK wsi gry NIE jest dawany wcale (gracz zaczyna bez tytulu Lordship BK, wies gry zostaje przy panu) - ten sam wylacznik.
3. Dochod = udzial wioski w produkcji okregu x podatek pana z kazdego powrotu wozu (ta czesc, ktora pan dotad bral z tej wioski); renty i daniny DS zostaja przy panie;
   w wojnie z krolestwem okregu nic - wylacznik `Side Village Fief Income`.
4. Linia w podpowiedzi zlota pokazuje srednia dobowa (pieniadze przychodza z wozami, nie w rozliczeniu doby).
5. Nocleg we wlasnej wiosce za darmo (to twoi ludzie). Wejscie: klik na mapie, postoj obok (pytanie), "Visit X (your village)" w menu wsi gry - wylacznik
   `Side Village Fief Menu`.
7. (po recenzji) Nocleg pod dachem = poziom wspolnej izby karczmy (slome i dach: +5% predkosci, +2 morale do zmierzchu, sen x1, bez gojenia i bez premii cen), nie lepszy -
   darmowy, wiec skromny. W wojnie z krolestwem okregu, przy rabunku i w spalonej wiosce - sen jak w obozie (spojnie z dochodem: w wojnie wioska tez nie placi). Namiot na
   mapie zostaje jak przy zajezdzie przy szlaku. Wylacznik `Side Village Fief Lodging` (off = sen jak w obozie, jak w pierwszej wersji, ale z prawdziwym opisem).
6. Podboj: wioska zostaje twoja (w zapisie), ale w wojnie z krolestwem okregu nie placi; zostaniesz panem okregu - podatek i tak twoj (bez przelewu).

**Test w grze.**
- Autotest 5 dob + bitwa na zapisie autotest-F-d240 z plikiem flagi `Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-wioska-gracza.flag` (usunac po biegu):
  w pierwszej godzinie z czynna ksiega wiosek proba wola TE SAMA metode BK (`GiveLordshipOnKingdomJoin(krolestwo, gracz, true)`, odbiciem) dla krolestwa gracza albo
  najblizszej wsi bez wojny z graczem - prefiks, nadanie, herb, zapis, menu. Linie logu Armoury:
  - `Wioska gracza (n20): dzien N (start sesji) | klucze: start WL, dochod WL, menu WL | latki: BK GiveLordshipOnKingdomJoin wpieta, podpowiedz zlota wpieta (...)`
  - `Wioska gracza (n20): BK GiveLordshipOnKingdomJoin(` ... `POMINIETY`
  - `Wioska gracza (n20): NADANIE (` ... `wies gry zostaje przy panu.`
  - `Wioska gracza (n20): test (autotest, plik armoury-wioska-gracza.flag) | wynik TAK 10, NIE 0` (T1 latka BK, T2 prefiks przejal, T3 nadanie, T4 gospodarz = gracz i herb,
    T5 dymek, T6 zapis tam i z powrotem, T7 tekst menu, T8 latka podpowiedzi, T9 udzial wioski, T10 nocleg pod dachem: stan noclegu z uid wioski i zapis arm_inns tam
    i z powrotem, stan noclegu gracza przywrocony)
  - w linii doby `klucze: ... nocleg pod dachem WL` i `noclegi N (pod dachem M)`; przy noclegu (tylko recznie - menu w autotescie zablokowane): `Wioska gracza (n20): nocleg w X
    - pod dachem ...`, `Wioska gracza (n20): nocleg pod dachem w X (...) od ...` i po pobudce `Karczmy (KA): pobudka w X ... -> wypoczeci do ...`
  - co dobe `Wioska gracza (n20): dzien N | ... | wioska X (...) | wozy okregu A, podatek pana w oknach B, udzial C% (...), do gracza D (razem E), srednia 7 dob F`
    - oczekiwane D > 0 w ciagu 5 dob (wozy wracaja co 1-3 doby), "bez przelewu: wojna 0"; potkniecia 0.
  - ksiega pieniadza bez nowego "zniklo" w oknach wozow (linie "Utarg wsi (112)", "Pieniadz swiata").
- Sam start (nowa kampania): autotest nowej kampanii (autotest-bitwa.ps1 bez -LoadSave) przechodzi kreator wybierajac pierwsze opcje, a okno startu BK ("Custom Start")
  zamyka przy postoju - RunStartOption "Indebted Lord" sie tam nie wykona. Furtka z flaga sprawdza cala nasza droge od wywolania BK; nie sprawdza tylko samej lambdy BK
  (sprawdzonej w dekompilacji). Pelny start: recznie w nowej kampanii (Custom Start -> Indebted Lord -> Finish) albo zmiana AutotestUi CrashScribe (wybor "Indebted Lord"
  w oknie campaignStart) - poza ta galezia.

**Recenzja (d67cf6f0) - co sprawdzone i co poprawione.**
- WAZNE "Rest the night here = zwykly sen obozowy, a podpowiedz obiecuje dach i wypoczecie" - PRAWDZIWE. Dowod w kodzie: `NightRest.BeginInnSleep` tylko ustawia
  `_sleepReturn = "town"` i przelacza na `arm_sleep_wait`; `SleepInit` poza osada stawia namiot; `TownInns.OnSleepInit` / `SleepFactor` / `Wake` bez stanu `_rest`
  (faza 1) zwracaja null / 1 / nic - mnoznik 1, bez czekania do switu, bez premii. Poprawione: stan noclegu z uid wioski (`TownInns.Host.cs`, klucz
  `SideVillageFiefLodging`), podpowiedz mowi prawde w obu przypadkach (pod dachem z premia albo "camp by the village ...: powod"). Moj dawny opis "ten sam sen co
  w karczmie" byl nieprawdziwy - poprawiony tutaj i w raporcie.
- drobne "Invest in its works wlaczona, gdy prac nie da sie kupic" - PRAWDZIWE (`OpenPart`: `!q.Allowed || q.WKind < 0` -> tylko czerwony komunikat). Poprawione:
  `InvestOk` - ta sama wycena `DistrictWorks.QuotesOf` co okno prac, raz na godzine gry (i od nowa po oknie prac), opcja szara z powodem gry (`q.Why`).
- drobne "wyjatek w nadaniu przy starcie BK = ani wioski, ani tytulu" - PRAWDZIWE (`TryPending` lapie wyjatek i kasuje `_pendKingdom`, prefiks zwracal false).
  Poprawione: po `TryPending` w prefiksie `!HasFief && _pendKingdom == null` -> `return true` (tytul BK jak dotad) i linia logu.
- drobne "przy Finish n okregu moze nie byc policzone (StateOf -> false)" - PRAWDZIWE (`VillageBook.StateOf` wymaga `NSet`, `EnsureN` leniwe). Poprawione: `Grant`
  bierze stan wioski z `VillageBook.PartsOf(okreg)` (wola `EnsureN`), raz na okreg w slowniku tego wyboru.
- drobne "menu z mapy bez tla" - PRAWDZIWE (`GameMenuVM.Refresh`: puste `CurrentBackgroundMeshName` -> "wait_guards_stop"). Poprawione: init menu ustawia
  `WaitMeshName` wsi gry okregu (zapas "wait_fallback").
- Tekst recenzji urwal sie po czwartym drobnym znalezisku (werdykt mowi o 5 drobnych) - piatego nie znam, wiec go nie poprawialem.

**Status: DO SPRAWDZENIA** (zbudowane: Armoury 0 bledow, kod wyjscia 0; autotest nie uruchamiany - gra zajeta).
