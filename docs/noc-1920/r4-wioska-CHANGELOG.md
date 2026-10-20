## r4-wioska (20.10) - wioska poboczna gracza: dochod co dobe, linia w podpowiedzi zlota, wioska w zakladce Fiefs (Armoury)

**Dla Jeffa (prosto).** Twoja wioska (Seven Elms) placi teraz codziennie, od pierwszej doby: dostajesz podatek pana od tego, co wioska sprzedaje (jej towary po cenach
targu, z czego ok. 87% wraca jako utarg wozow, razy stawka podatku pana) - mniej wiecej 5-12 denarow na dobe przy zwyklej wiosce (w autotescie Greenton: wartosc
dnia 25 denarow x 0,87 x ok. 35%, czyli ok. 8 na dobe). To zloto nie bierze sie znikad: zabieramy je panu okregu (Paege) - z podatku, ktory pan ma jeszcze do
zebrania z okregu, a w praktyce (Banner Kings zbiera go codziennie) glownie z jego sakiewki; pan ma dokladnie o tyle mniej. Gdyby pan nie mial zlota, ekran pokaze
mniej albo "none today" z powodem - nie obiecuje kwoty, ktorej nie bedzie. W "Expected change" (podpowiedz zlota) jest linia "Seven Elms (your side village: ...)" z kwota dnia, a w
ekranie rodu w zakladce Fiefs pod zamkami - "Side villages (1)" z nazwa, okregiem i dochodem dnia. Nic nie dostaniesz tylko w wojnie z krolestwem pana okregu, gdy
okreg jest rabowany albo twoja wioska spalona - wtedy powod widac w zakladce Fiefs i w menu wioski.

**Problem.** Jeff 20.10 (doslownie, zrzuty ekranu): "NIe ma poakzanego moje thiefts i mam na minusie, wioska nic nie zarabia?" - zakladka Fiefs: "Clan Does Not Own Any
Fiefs" (Towns 0, Castles 0), finanse: przychod 0, wydatki -37/d. Dowod: log Jeffa `D:\Backup-Bannerlord\logi-jeffa\2026-10-20\Armoury-2026-10-20_02-17-13.log`
- NADANIE Seven Elms (village_B1_2#183f3aba, okreg Mudgrave, pan Paege) i linia doby "wozy okregu 0, podatek pana w oknach 0, udzial 0.0% (-), do gracza 0"; autotest
`Modules\Armoury\Armoury-2026-10-20_00-11-12.log` - 5 dob z Greenton: codziennie "wozy okregu 0, podatek pana w oknach 0, udzial 8.9% (wartosc dnia 25 z 284), do
gracza 0".

**Przyczyna (sprawdzona w kodzie i logu).**
1. Dochod szedl TYLKO przy powrocie wozu do wsi gry okregu (VillageTakings.EnterPostfix -> AtCart: udzial x przyrost licznika podatku pana w oknie wozu). Diagnoza
   ze zlecenia ("licznik gry stoi, bo pan dostaje daniny DS z kiesy wsi") jest NIEPRAWDZIWA w polowie: licznik pana rosnie przy wozach (linia "Utarg wsi (112)" dnia
   109076: pan (licznik podatku) 61203 z 67 powrotow), ale powrotow jest ok. 67 na dobe na 571 wsi - jeden woz okregu mniej wiecej co 8 dob. W 5 dobach autotestu
   (i w pierwszej dobie Jeffa) woz okregu nie wrocil wcale - "wozy okregu 0" (licznik `_dCarts` liczony dopiero za warunkiem `taxInWindow > 0`, wiec log nie
   odroznial "brak wozu" od "zero podatku"). Do tego linia w podpowiedzi zlota pokazywala srednia 7 dob wyplat - na starcie 0, wiec linii nie bylo.
2. Udzial "-" w pierwszej dobie: ShareOf liczono tylko przy wozie (pierwsze wywolanie dopiero przy powrocie wozu).
3. Zakladka Fiefs: ClanFiefsVM.RefreshAllLists bierze tylko `_clan.Settlements` (miasta i zamki) - wioska poboczna to wiersz ukladu wiosek, nie osada gry
   (dekompilacja TaleWorlds.CampaignSystem.ViewModelCollection: ClanFiefsVM, ClanSettlementItemVM wymaga Settlement).

**Zmiana.**
- `SideVillageFief.cs`:
  - `Due(out why, fresh)` - naleznosc dnia: wartosc dnia produkcji wioski (VillageCrafts.PartsOf - ta sama co okno prac i T9; zapas: udzial ludzi w domu x wartosc
    okregu) x H okregu (utarg oddany / wartosc produkcji - `DistrictWorks.HandedRatio`, ta sama liczba co okno prac: zmierzona albo 0.868; po recenzji) x stawka pana
    z utargu (`LordRate`: czynny model podatku wsi `CalculateVillageTaxFromIncome`, ta sama regula co BK, 112 i "udzial pana" DS przy wozie; po recenzji we wlasnym
    try - wyjatek = stawka nieznana, dochod 0, a nie 100%). Liczona od razu (takze w 1. dobie), raz na dobe (cache; po recenzji od nowa takze przy zmianie podpisu
    bramek `DueSig`: klucz, pan, wojna, rabunek), od nowa przy wyplacie. 0 tylko: klucz wylaczony, gracz panem okregu, wojna z krolestwem okregu,
    wies gry okregu BeingRaided / Looted (gra tez nie daje wtedy panu podatku - DefaultClanFinanceModel.CalculateVillageIncome), wioska gracza spalona, pan nie bierze podatku, wioska
    wytwarza dzis za malo (< 0,5 - np. zima; po recenzji osobny powod), stawka nieznana; powod po angielsku (Fiefs, menu) i po polsku (log).
  - `Expect` (po recenzji) - widok dochodu (podpowiedz zlota, Fiefs, menu): naleznosc obcieta do tego, co pan ma teraz (`LordCanPay`: licznik podatku wsi + kiesa
    glowy rodu), z powodem "Its lord has only N to give today." / "Its lord has no money to pay it today." Pieniadze plyna tylko z Pay.
  - `Pay()` w `Daily()` (OnDailyTick, przed srednia i linia doby): naleznosc z licznika podatku pana wsi gry okregu (`TradeTaxAccumulated` - podatek z wozow, ktorego
    pan jeszcze nie zebral), reszta z kiesy glowy rodu pana; brak u obu - "brak u pana" (nic z niczego). `ChangeHeroGold` gracza o sume. POPRAWKA PO RECENZJI: z BK
    pan zbiera licznik wsi codziennie w 80-100% (BannerKings EconomyPatches -> BKTaxModel.CalculateVillageTaxFromIncome: `TradeTaxAccumulated * num`, num 1.0 albo
    0.8, potem pobranie), a nie 1/5 jak waniliowe DefaultClanFinanceModel - wiec zwykle placi glownie KIESA pana ("z kiesy pana" w linii doby), licznik to resztka.
  - `AtCart` - BEZ przelewu (inaczej podwojnie), tylko licznik wozow okregu i podatku z ich okien (teraz liczony zawsze - log odroznia "brak wozu"). Zwraca 0.
  - `GoldTipPostfix` - linia "<wioska> (your side village: its share of the lord's tax)" z naleznoscia dnia (nie srednia).
  - Menu wioski i "Look over" - dochod dnia albo powod; opis "How it pays" i okno nadania - "every day" zamiast "when the carts come back".
  - Linia doby: `| dochod dnia (r4): nalezne N (wartosc wioski V x H h x stawka pana R%), z licznika podatku pana A, z kiesy pana B, brak u pana C, do gracza D ...`;
    w czesci latek: `zakladka Fiefs: tekst wpiety/BRAK, lista - <stan UIExtenderEx>, klucz WL/WYL`.
  - Test autotestu: nowy T11 "dochod dnia i zakladka Fiefs" (naleznosc > 0 od razu, tekst zakladki i - po recenzji - rejestracja wstawki i mixinu UIExtenderEx
    przy kluczu WL; wygladu bloku test nie sprawdza).
  - `DistrictWorks.cs` (po recenzji): `HandedRatio(v, vd)` - wewnetrzny dostep do HOf (H okregu), bez zmiany zachowania prac okregu.
  - `NoFiefsPostfix` (Harmony, EnsureHooks): postfiks `ClanFiefsVM.RefreshValues` (metoda bazowa - takze NavalClanFiefsVM, ktory jej nie nadpisuje) - przy wiosce
    gracza prawa czesc zakladki (widoczna tylko bez miast i zamkow) mowi "Your clan holds no towns or castles. Your side village: X - lands of Y, sworn to House Z.
    Income: about N a day ..." zamiast "Clan Does Not Own Any Fiefs". Bez UIExtenderEx.
- `SideVillageFiefUi.cs` (nowy): UIExtenderEx - osobny UIExtender "Armoury.SideVillage" (przyciski predkosci mapy u Jeffa nierejestrowane przez Better Time - nie
  mozna sie pod nie podpiac), rejestracja w OnSubModuleLoad (SubModuleMain, metoda NoInlining, wlasny try). Wstawka do prefabu ClanFiefs jako siostra `CastlesList`
  w lewej kolumnie: "Side villages (1)", "Seven Elms - lands of Mudgrave, sworn to House Paege", "Income: about N a day (its share of the lord's tax)" - same
  TextWidgety z pedzlami tego prefabu (Clan.Management.Collapser.Text, Clan.Tuple.Name.Text), bez zdarzen i nawigacji. Mixin `ClanFiefsVM` z `handleDerived`
  (NavalDLC podmienia VM na NavalClanFiefsVM; zestawy modulow sa ladowane przed OnSubModuleLoad - Module.LoadSubModules), wypelnienie w try (potkniecie liczone,
  blok ukryty).
- `Settings.cs` + `McmSettings.cs` (gen_mcm, 2057 ustawien): opis `SideVillageFiefIncome` (co dobe), nowy klucz `SideVillageFiefClanTab` = TAK (lista w Fiefs - czytany
  przy starcie gry z pliku MCM, jak MapSpeedButtons; tekst prawej czesci - od razu).
- `VillageTakings.cs`, `DsVillage.cs` - tylko komentarze (krok SideFief przy wozie = 0).

**Ryzyko / co sprawdzic.**
- Regresje: zapis `arm_side_fief` bez zmian (sf1|uid|okreg|nazwa|doba|wyplacone|srednia) - zapis Jeffa z Seven Elms wczytuje sie jak dotad i od pierwszego OnDailyTick
  po wczytaniu dostaje dochod. Bez wioski gracza Pay/Due/ClanTab koncza sie na pierwszym warunku; postfiks RefreshValues bez wioski nic nie zmienia; blok Fiefs
  niewidoczny (IsVisible = false).
- Kolizje: brak podwojnej wyplaty - przy wozie przelewu juz nie ma (AtCart zwraca 0, DsDues dostaje side = 0 - linia "Daniny wsi (DS)" liczy calosc jako "pan i
  majatki", tylko log). Prace okregu (DistrictWorks) placa z licznika przy wozie - nasz pobor dzienny jest z tego, co zostalo. Czynsz staly i daniny DS zostaja przy
  panie (swiadomie - patrz decyzje). Model finansow: linia podpowiedzi tylko przy includeDescriptions i bez pobrania (jak dotad), saldo doby gry bez zmian.
- Zamknieta ekonomia: licznik podatku wsi i kiesy bohaterow sa w "Pieniadz swiata" (MoneyLedger.Holders: HVillageTax, HLeaders, HPlayer) - przelew poza oknem wozu,
  suma swiata bez zmian; brak pieniedzy u pana = brak wyplaty (licznik "brak u pana"), nie zloto z niczego.
- Cudzy kod sprawdzony w dekompilacji: DefaultClanFinanceModel.CalculateVillageIncome (1/5 licznika dziennie, Looted/BeingRaided = 0) - z BK zastapione przez
  EconomyPatches (AddSettlementIncome -> CalculateVillageTaxFromIncome, 80-100% licznika dziennie; sprawdzone po recenzji), ROTClanFinanceModel (przekazuje do BK), BKClanFinanceModel (bez CalculateVillageIncome), ClanFiefsVM (konstruktor, RefreshValues, NoFiefsText, RefreshAllLists), NavalClanFiefsVM (tylko
  CreateSettlementItem), ClanFiefs.xml (CastlesList, NoFiefsText), UIExtenderEx ViewModelComponent (handleDerived), ViewModelWithMixinPatch (OnRefresh po RefreshValues
  i z konstruktora), UIExtender.Register (osobna nazwa = osobna instancja), ExplainedNumber.Add (0 = bez linii - dlatego powod zera nie w podpowiedzi zlota).
- UI (najwieksze ryzyko): wstawka i mixin NIE byly widziane w grze. Gauntlet przy zlej nazwie pedzla / wiazaniu loguje, nie wywraca; mixin w try. Jesli UIExtenderEx
  nie obejmie NavalClanFiefsVM, wiazania bloku nie istnieja - teksty puste (nic nie widac), a prawa czesc zakladki (NoFiefsPostfix) i tak mowi o wiosce. Gdyby ekran
  rodu sie wywracal - `Side Village Fief Clan Tab` = NIE w MCM i restart gry.
- Watki: Due/ShareOf (VillageCrafts.PartsOf) tylko z watku glownego - podpowiedz zlota (pierwszy warunek odcina AI), ekran rodu, menu, OnDailyTick.

**Status: DO SPRAWDZENIA** (zbudowane: Armoury 0 bledow, kod wyjscia 0; autotest nie uruchamiany - Jeff gra; nic nie wgrane).

**Decyzje (podjete samodzielnie; wylaczniki `Side Village Fief Income`, `Side Village Fief Clan Tab`).**
1. Podstawa dochodu: podatek pana od utargu z produkcji wioski (wartosc dnia x H okregu x stawka modelu podatku wsi) - "to, co pan bierze z tej wioski" w naszej
   ekonomii, znane od 1. doby. Nie mierzony podatek z wozow (woz okregu co ok. 8 dob - dochod skakalby i zaczynal sie po tygodniu). Po recenzji z H (podatek nalicza
   sie od utargu oddanego przez woz, ok. 87% wartosci) - bez H gracz dostawalby ok. 15% wiecej, niz pan ma z tej wioski.
2. Zrodlo: najpierw licznik podatku pana wsi gry okregu (pieniadz z utargow okregu, jeszcze nie zebrany), potem kiesa glowy rodu pana (tam trafia zebrany podatek) - tak,
   by dochod plynal codziennie. Z BK licznik jest zwykle prawie pusty (pan zbiera go codziennie), wiec w praktyce placi kiesa pana - to ten sam pieniadz (zebrany
   podatek okregu), decyzja bez zmian. Kiesa wsi okregu NIE (wtedy placilaby wies, a nie pan).
3. Czynsz staly DS (W3, ok. 20 zl na wies na dobe) i daniny DS (dziesiecina, korona) zostaja przy panie / wsi - udzial gracza bylby ok. 2 zl, a liczylby drugi raz te sama
   wioske.
4. Bez zaleglosci: dzien bez pieniedzy u pana przepada (licznik "brak u pana"), nie roszczenie na pozniej.
5. Zakladka Fiefs: osobny blok tekstu pod zamkami zamiast pozycji listy (ClanSettlementItemVM wymaga osady gry: zaznaczenie, namiestnik, projekty, teleport - za duze
   ryzyko wywrotki ekranu); prawa czesc zakladki - tekst o wiosce, gdy rod nie ma miast ani zamkow.

**Test w grze.**
- Autotest 5 dob na zapisie autotest-F-d240 z plikiem `Documents\Mount and Blade II Bannerlord\CrashScribe\armoury-wioska-gracza.flag` (usunac po biegu):
  - `Wioska gracza (n20): zakladka Fiefs (UIExtenderEx) - wstawka ClanFiefs i mixin ClanFiefsVM (z pochodnymi) zarejestrowane (...)` przy starcie gry;
  - linia testu `wynik TAK 11, NIE 0` - w tym `T11 dochod dnia i zakladka Fiefs TAK (nalezne N (wartosc wioski V x H 0.87 x stawka pana R%), Fiefs: Side villages (1) | ... | Income: about N a day ..., lista - wstawka ClanFiefs i mixin ClanFiefsVM (z pochodnymi) zarejestrowane (...))`;
  - KAZDA linia doby `Wioska gracza (n20): dzien N | ...` z wioska: `dochod dnia (r4): nalezne N > 0 (wartosc wioski V x H h x stawka pana R%)`, `do gracza D` z D > 0
    (D = A + B, "brak u pana 0"; z BK zwykle A male, B prawie calosc), dla Greenton ok. 8,
    `zakladka Fiefs: tekst wpiety, lista - wstawka ... zarejestrowane, klucz WL`; potkniecia 0;
  - "Pieniadz swiata" i "Utarg wsi (112)" bez nowego "zniklo".
- U Jeffa po wgraniu (gra zamknieta, kopia): wczytac zapis z Seven Elms - po pierwszym nowym dniu linia doby z "do gracza" > 0; podpowiedz zlota "Seven Elms (your side
  village: its share of the lord's tax) +N"; ekran rodu -> Fiefs: po prawej "Your clan holds no towns or castles. Your side village: Seven Elms - lands of Mudgrave, sworn
  to House Paege. Income: about N a day ...", po lewej pod "Castles (0)" blok "Side villages (1)" z ta sama nazwa i dochodem. Wyglad bloku (i czy przyklejony
  przycisk Castles nie wisi nad nim przy przewijaniu) - tylko recznie, autotest go nie widzi.

**Recenzja (20.10, HEAD 9c7db590: 0 krytycznych, 0 waznych, 6 drobnych) - wszystkie prawdziwe, wszystkie poprawione.**
1. Zrodlo wyplaty z BK (sprawdzone w dekompilacji BannerKings: EconomyPatches `village.TradeTaxAccumulated -= MathF.Min(village.TradeTaxAccumulated, income)`,
   BKTaxModel `TradeTaxAccumulated * num`): opis poprawiony (glownie kiesa pana); widok dochodu `Expect` obciety do tego, co pan ma (licznik + kiesa), z powodem;
   cache Due liczony od nowa przy zmianie podpisu bramek (`DueSig` - wojna wypowiedziana w ciagu dnia nie pokazuje juz +N).
2. Dwie kropki w prawej czesci Fiefs: `i.TrimEnd('.', ' ') + "."`.
3. Mylny powod przy malej produkcji: osobno "stawka pana 0" / "brak wartosci" / "za mala wartosc dnia" ("Your village makes too little today to owe the lord
   anything.").
4. Stawka 100% przy wyjatku modelu (DsVillage.VillageShareOfTakings daje 0 przy wyjatku): `LordRate` we wlasnym try, wyjatek = stawka nieznana, dochod 0 ("Its
   lord's tax is not known today.", w logu "stawka pana nieznana").
5. Podstawa od wartosci, nie od utargu: x H okregu (`DistrictWorks.HandedRatio` = HOf, ta sama liczba co okno prac), w linii doby "x H h".
6. T11 obiecywal za duzo: warunek takze `SideVillageFiefUi.State` = "wstawka ..." (przy kluczu WL), stan w szczegolach testu; wyglad - tylko recznie.

## Dopisek prowadzacego (20.10 ok. 03:20): przychod w panelu Finance ekranu rodu
- Panel Finance ekranu rodu (ClanManagementVM.RefreshDailyValues) liczy "Total Income" z ClanFinanceModel.CalculateClanIncome (bez opisow, bez pobrania) - z sama
  podpowiedzia zlota Jeff dalej widzialby tam przychod 0. Dodany IncomeViewPostfix na CalculateClanIncome czynnego modelu (ROTClanFinanceModel; cel przez
  DeclaringType jak przy podpowiedzi), te same warunki: rod gracza, applyWithdrawals = false, instancja = czynny model, klucz SideVillageFiefIncome - ta sama
  naleznosc dnia (Expect). Tylko widok. Bez podwojnej wyplaty: dzienny przelew gry idzie przez CalculateClanGoldChange(applyWithdrawals = true) i
  CalculateClanIncomeInternal (DefaultClanFinanceModel), ROTClanFinanceModel.CalculateClanGoldChange wola _previousModel.CalculateClanGoldChange, nie swoje
  CalculateClanIncome - podpowiedz zlota nie liczy wioski drugi raz (sprawdzone w dekompilacji 1.4.8 i ROT.dll).

