## m96-bandy - bandy jedza; zapas jedzenia i kiesy w polu; lup jedzenia i zlota u zwyciezcow (Armoury)

**Dla Jeffa (prosto):** bandy od teraz jedza - jedna sztuka jedzenia na 20 ludzi dziennie, jak wojsko; maja je z lupu, kupuja przez pasera
(piraci tez, z morza) albo zabieraja z okolicznych wsi (z twojej wsi - czerwony komunikat). Banda trzyma w jukach jedzenie na kilka dni i drobne na
trzy dni zycia (ok. 60 zl przy 20 ludziach), a na zycie w miescie i do kryjowki idzie tylko reszta; biedna banda dostaje brak z nadwyzki swojej
kryjowki. Banda, ktora nic nie zarabia i ma biedna kryjowke, dalej bywa bez grosza, ale jedzenie w jukach ma. Glodna banda traci ducha, a po dwoch
dniach glodu ludzie odchodza do lasu (puli wyrzutkow), nie znikaja. Wylacznik: MCM Armoury -> "Iron Bank" -> **Bands Eat Food** (wylaczony = jak dotad).

**Problem:** Jeff 20.10: "jak pokonuje bande czy ogolnie armie, to oni powinni miec tez zloto i jedzenie, bandyci chodza bez jedzenia czy zlota?"

**Dowod (logi D:\Backup-Bannerlord\logi-testow\2026-10-20, WGRANIE 95 i 92):**
- Bandy nie jedza: dekompilacja `DefaultMobilePartyFoodConsumptionModel.DoesPartyConsumeFood` = false dla `IsBandit` (BK/BEE/ROT/NavalDLC tylko opakowuja);
  `DesertionCampaignBehavior` tylko lordowie, karawany, zalogi. Zywnosc z niczego dla band zamknieta (OutlawNoFreeFood: "zablokowana 41 nowym bandom").
- Cala zywnosc band szla do pasera: linia "Paser:" - bieg 160 dob z zapisu d240 (12-25-19) srednio **898 szt./d**, nowa kampania 120 dob (08-45-54)
  **207 szt./d**. Potrzeba band, gdyby jadly jak wojsko: ok. 16 500 ludzi w ok. 810 bandach (linia "Wyrzutki", 12-25-19) = ok. **825 szt./d**.
- Kiesy band w polu to prawie nic: "Skarbce kryjowek (nskarbiec)" d109235 - 500 band z kryjowka, **467 z pusta kiesa**, kiesy razem 8741 zl.
  Przyczyna w kodzie: `OutlawLaw.LifeDay` co dobe wydaje 10% CALEJ kiesy (co najmniej 1 zl - drobna kiesa schodzi do zera), potem `StashDay` 25% reszty
  do kryjowki. Kiesy wszystkich band razem 133 667 zl (glownie lupiezcy i piraci - bez kryjowki).
- Zloto rozbitych band do zwyciezcow juz dziala (3.2 BeatenPurse): "Kiesy rozbitych (3.2)" bandy -> zwyciezcy srednio **5205 zl/d** (160 dob),
  **1292 zl/d** (nowa 120 dob); reszta przy zniszczeniu 0, zniklo 0.
- Juki pokonanych do zwyciezcow juz dzialaja: gra `MapEvent.LootDefeatedPartyItems` (kazda sztuka z szansa 0.5 x udzial zwyciezcy; dekompilacja
  `DefaultBattleRewardModel.GetLootItemChancesForWinnerParties` ... `* 0.5f`), reszta jukow rozbitej partii - R14 (`HorseFate.Spoils`): linia K1
  "dobytek rozbitych ... do zwyciezcow" srednio **235 szt./d** (160 dob), **175 szt./d** (120 dob), ponad udzwig - na polke najblizszej warowni.
- Armie i druzyny lordow (pkt 3 zadania): jedzenie z jukow - ta sama droga gry + R14 (jak wyzej); zloto - wodz traci 5% (najwyzej 10 000, gra; A6
  BattlePlunderFromLoserOnly - tylko z tego, co pokonany naprawde traci), sakiewki ludzi druzyny do przywodcy zwyciezcow (D11-6): **54 156 zl/d** z 775
  druzyn (160 dob), **35 113 zl/d** z 716 (120 dob). **Luki nie ma** - nic w tej paczce nie zmienia lupu lordow, dochodzi tylko pomiar jedzenia z ich jukow.

**Przyczyna:** gra nie karmi band (model jedzenia), a nasze prawo wyrzutkow konsekwentnie zamienialo kazda sztuke ich jedzenia w zloto (paser),
a kiesa co dobe schodzila na zycie w miescie (10%, co najmniej moneta) i do kryjowki (25%) - bez progu, wiec banda w polu nosila grosze albo nic.

**Zmiana:** nowy plik `Armoury/src/BandFood.cs`, klucze MCM (grupa "Iron Bank" - tam stoja dzis klucze band, obok Outlaw No Free Food): **Bands Eat Food** (TAK), **Band Food Keep Days** (5), **Band Purse Keep Days**
(3), **Band Hunger Leave Share** (0.10). Wpiecia: `OutlawLaw.Daily` (po paserze `BandFood.Daily` + `MoneyLedger.Mark(MLife)`; glodna banda na ladzie nie
werbuje z puli), `OutlawLaw.FenceSell` (paser bierze tylko zywnosc ponad zapas; "zostalo w jukach" bez zapasu), `OutlawLaw.LifeDay` (zycie w miescie tylko
z kiesy ponad zapas), `OutlawLaw.StashDay` (kieszonkowe z nadwyzki kryjowki; dola z kiesy ponad zapas; licznik "kiesa w calosci zapasem" w linii
nskarbiec), `OutlawLaw.Reset`, `BeatenPurse.SplitPostfix` (pomiar zlota z kies band, gracz osobno), `ArmouryBehavior` (sluchacz
`OnLootDistributedToPartyEvent` - pomiar). Doba bandy (po paserze, bez band w bitwie, zadaniu, nieumarlych i najazdow Wolnych Ludzi):
1. potrzeba = (ludzie + jency/2) / 20 sztuk (jak gra), ulamek przechodzi na nastepna dobe (stan tylko w sesji, nic do zapisu);
2. zapas = dzis + 5 dni; brak dokupuje przez pasera najblizszego otwartego miasta za wlasna kiese - **takze z morza** (paser skupuje lup od band na
   morzu, wiec i sprzedaje): najtansza zywnosc z polki, sztuka po sztuce (`ShelfBuy.Take`), cena targu x marza pasera z droga (OutlawFenceMarkup 1.5 ->
   OutlawFenceMarkupFar 2.0, jak sprzet); zaplata do kasy miasta, licznik sprzedazy kupcow (`TownFolk.Sale`); w ksiedze kas - odcinek "bandy - zycie";
3. czego nie kupi - **na ladzie** zabiera z magazynow wsi (stan Normal) w promieniu 100 (dzien drogi) **do pelnego zapasu**: najblizsza wies najpierw,
   z jednej wsi na dobe najwyzej polowa jej zywnosci (wszystkie bandy razem), do 3 wsi na bande; wies nic nie dostaje (rabunek); wies rodu gracza -
   czerwony komunikat raz na dobe ("Outlaws took N loads of food from your village X.");
4. je z jukow (najtansze najpierw) w ramce ksiegi towarow "jedzenie partii" (`GoodsLedger.FFood`) **z osada = najblizsze miasto bandy** (rynek KS krainy);
5. glod: morale -5 x brak na dobe (gra wygasza 10%/d); po 2 dobach glodu z rzedu odchodzi 10% x brak ludzi na dobe (zdrowi i ranni po rowno, bez
   bohaterow) - do puli wyrzutkow i ksiegi ludzi ta sama droga co dezercja z zaleglego zoldu (WarLedger: `OutlawLaw.OnTroopsDeserted`,
   `PeopleLedger.OnTroopsDeserted`); banda bez ludzi rozwiazana jak przy dezercji gry (`DestroyPartyAction` bez sprawcy - kiesa do miasta, juki na polke,
   jency do puli - reguly juz czynne). Na morzu i z jencem-bohaterem (gracz, lord) bez odejsc; **na morzu werbunek z puli jak dotad** (na ladzie glodna
   banda nie werbuje);
6. kiesa: **zapas = 3 dni zycia** - zold rekruta (1 zl na czlowieka na dzien, `DefaultPartyWageModel.GetCharacterWage` tier 0) i dzienne jedzenie po cenie
   zboza u pasera najblizszego miasta (banda 20 ludzi: ok. 60 + 9 = ok. 69 zl). Zycie w miastach (`LifeDay`) i dola do kryjowki (`StashDay`) biora tylko
   z kiesy ponad zapas. Banda z kryjowka ponizej zapasu dostaje brak z nadwyzki kasy swojej kryjowki ponad skarbiec (klucz OutlawHoardCirculates - ta
   sama regula co kiesy startowe nowych band: "whatever lies above the kept hoard ... tops up its bands"); skarbca kryjowki (lup dla zdobywcy) nie rusza.
   W liniach nskarbiec i Paser kieszonkowe licza sie w kategorii "kiesy startowe" (bez zmiany formatu linii; NK-4 i NK-11 dalej sie zgadzaja).

**Poprawki po recenzjach (gra, technika) - kazde znalezisko sprawdzone:**
- *krytyczne (technika): piraci NavalDLC na stale glodni* - PRAWDZIWE. Dekompilacja `PiratesCampaignBehavior.InitializePirateParty`:
  `GiveFoodToBanditParty(pirateParty); pirateParty.SetLandNavigationAccess(false);`, a nasz `FoodPrefix` blokuje te zywnosc z niczego; `Band()` na morzu
  nie kupowal i nie rabowal, a paser (`Reach`, `NearestOpenTown`) dziala z morza bez warunku. Polow z gry (`NavalFishingCampaignBehaviour.OnHourlyTickParty`)
  tylko z atutem MasterAngler - bandy bez bohatera go nie maja. Piraci = 100 band, 5497 ludzi (linia Paser 12-25-19). Poprawka: zakup u pasera takze z morza;
  `Starving()` tylko na ladzie (na morzu werbunek jak dotad); na morzu dalej bez rabunku i bez odejsc. MB-12 (INFO) liczy bandy na morzu, glodne i zakupy z morza.
- *wazne (gra): zloto w kiesie prawie nie dziala* - PRAWDZIWE (`LifeDay` przed `StashDay`, 10% calej kiesy, co najmniej 1 zl; zapas liczony w dniach
  zboza = 14-19 zl). Poprawka: zapas w dniach zycia (wyzej, pkt 6), `LifeDay` i dola tylko z kiesy ponad zapas, kieszonkowe z nadwyzki kryjowki. Nowa
  kontrola MB-11: udzial band z kryjowka z pusta kiesa (linia nskarbiec, ta sama miara w bazie i biegu) mniejszy niz w bazie (baza 467/500 = 0.934).
  Granica: kryjowki z nadwyzka to 31 ze 109 z bandami (12-25-19: "z kasa co najmniej skarbiec 31"; zycie kryjowek z nadwyzki 4159 zl/d = 10% nadwyzki,
  czyli ok. 41 600 zl nadwyzki); bandy biednych kryjowek bez dochodu (lupu, paser) dalej moga nosic 0 zl - to prawda tej ekonomii, nie brak reguly.
- *wazne (gra): jedzenie band na zly rynek KS* - PRAWDZIWE (`GoodsLedger.MkOf` bierze `CurrentSettlement ?? LastVisitedSettlement` partii - kryjowka
  albo nic). Poprawka: ramka `Begin(FFood, najblizsze miasto, banda)` - `MkOfFrame` bierze rynek miasta.
- *wazne (technika): zapas jedzenia tylko dla band z kiesa* - PRAWDZIWE (`RobDays = 2`, po zjedzeniu ok. 1 doba). Poprawka: rabunek do pelnego zapasu,
  z limitem polowy zywnosci wsi na dobe i do 3 wsi na bande (rozklada rabunek, kilka band jednej kryjowki nie oprozni jednej wsi). Wszystkie 266
  kryjowek ROT-Map maja wies w promieniu 76 (recenzja), wiec bandy z kryjowka na ladzie siegna do wsi.
- *drobne (gra): MB-4 za luzny* - poprawione: rozpady z glodu <= 20% nowych band w tym samym biegu ("bandy nowe" z linii Wyrzutki; baza 13.1/d),
  bez tej liczby <= 0.002 band jedzacych na dobe.
- *drobne (gra): stary licznik glodu po wylaczeniu i wlaczeniu* - poprawione (`_st.Clear()` przy wylaczonym kluczu).
- *drobne (gra): rabunek bez sladu* - poprawione czesciowo: komunikat dla gracza (wies jego rodu, raz na dobe, EN, czerwony) i limit na wies; stosunki
  i bezpieczenstwo wsi bez zmian (bandy nie sa frakcja, z ktora gracz ma stosunki - swiadomie poza zakresem).
- *drobne (technika): przydzialy lupu bez jedzenia liczone* - poprawione (`OnLoot` liczy tylko n > 0).
- *drobne (technika): kiesa w calosci zapasem znika z rozbicia nskarbiec* - poprawione: licznik ", kiesa w calosci zapasem N" na koncu nawiasu
  "pominiete" (tylko przy czynnym kluczu; regex `NS_BANDS` z opcjonalna grupa - stare logi czytaja sie jak dotad, sprawdzone na 12-25-19).
- *drobne (technika): FoodKeep/PurseKeep polykaja wyjatek* - poprawione (`Stumble` - licznik potkniec i MB-1).
- *drobne (technika): GFood to "zuzycie wojska" planu KS* - prawdziwe, bez zmiany kodu (to prawdziwy popyt): dopisane do ryzyka i do testu (`--grupa nks`).

**Linia logu (log_grep):** `Bandy - jedzenie i kiesy (m96-bandy): dzien` - bandy jedzace, pominiete, potrzeba / zjedzone / kupione (zl, z morza, miasta) /
zabrane wsiom (wsie, wsie gracza), najedzone / glodne (bez jedzenia, ponad 2 doby, na morzu z wszystkich na morzu, jeniec-bohater), morale z glodu,
odeszli z glodu i rozpady, zapas w jukach (dob potrzeby), kiesy band w polu (suma, z kiesa, mediana; z kryjowka: z kiesa, suma), zapas zostawiony przez
dole i przez zycie (w calosci zapasem), kieszonkowe z nadwyzki kryjowek (zl, band, brakowalo, nie starczylo), lup u zwyciezcow (jedzenie z band AI /
gracz, zloto z kies band / gracz, jedzenie z druzyn lordow AI / gracz), potkniecia. Narzedzie: `python -I tools/sprawdz_logi.py --grupa m96bandy
[--baza-doby <log bazy>] <log>`.

**Oczekiwane w autotescie (160 dob z zapisu d240, baza = WGRANIE 95 `Armoury-2026-10-20_12-25-19.log`; P - pierwsze progi, do korekty z pomiaru):**
linia w kazdej dobie z "Paser:", potkniecia 0 (MB-0, MB-1); potrzeba ok. 800-900 szt./d; zjedzone / potrzeba >= 0.85 (MB-2); glodne <= 25% band jedzacych
(MB-3); rozpady z glodu <= 20% nowych band (MB-4); zapas w jukach >= 2 dob potrzeby od doby 10 (MB-5); lup jedzenia z band > 0 i zloto z kies band > 0
(MB-6); band i ludzi w bandach na koniec >= 80% bazy (MB-8; baza ok. 820 band, 16 835 ludzi); paser - zywnosc sprzedana spada z ok. 900 do kilkuset
szt./d (MB-9, INFO); bandy z kryjowka z pusta kiesa mniej niz 0.934 (MB-11); morze (MB-12, INFO); `--grupa nskarbiec` bez nowych NIE (NK-3, NK-4, NK-11;
NK-6 nie przechodzi juz w bazie - 0.25/0.30/0.17); `--grupa nks` NKS-5u i NKS-5 jak w bazie (TAK w 12-25-19).

**Ryzyko / co sprawdzic (kontrola calosci):**
- regresje: paser (FenceSell) - przy kluczu off keep 0, kod jak dotad; NK-3 (dola / kiesy) - "kiesy przed dola" licza kiese ponad zapas, nadwyzka ponizej
  zlotowki doli poza suma; NK-4/NK-11 - kieszonkowe w kategorii "kiesy startowe" (wyplata z kasy kryjowki ma kategorie); NK-8 (INFO) urosnie, bo
  "kiesy startowe nowych band X z Y" zawieraja teraz kieszonkowe (X i Y o te sama kwote);
- zycie w miastach: bandy wydadza mniej (bandy z kiesa ponizej zapasu nie wydaja nic; 12-25-19 bandy wydawaly 12 241 zl/d, w tym bandy z kryjowka ok.
  900 zl/d) - mniej uslug do kiesy ludu (TownFolk KLifeBand); kryjowki wydadza mniej z nadwyzki (czesc nadwyzki idzie na kieszonkowe);
- kolizje: model jedzenia gry nietkniety (BK/BEE/ROT/NavalDLC bez zmian, UI, AI); FoodSnapshot/FoodRestore (zywnosc z niczego w bitwie) - nasza doba
  biegnie poza tickiem gry; najazdy Wolnych Ludzi poza regula; nieumarli nie jedza; zadania nietkniete; piraci NavalDLC (TryRemoveWeakPirate) - na morzu
  bez odejsc i z werbunkiem, jak dotad;
- zamknieta ekonomia: zakup - polka miasta -> juki, kiesa -> kasa miasta (takze z morza); rabunek - magazyn wsi -> juki; jedzenie - ksiega towarow
  "jedzenie partii"; kieszonkowe - nadwyzka kasy kryjowki -> kiesa bandy; odchodzacy - pula wyrzutkow i ksiega ludzi; nic z niczego, nic w nicosc;
- KS: jedzenie band to popyt "zuzycie wojska" planu KS (`GoodsLedger` GFood -> `KsArmy`, `StartCalibration.Plan` Army): ok. 800 szt./d (ok. 2.6%
  wozow wsi) - ceny cienia zboza i koszyk jedzenia ludu (dzis 88.5%, prog 95%) do porownania z baza; rynek wedlug najblizszego miasta bandy;
- skala i pierwsza doba: bandy bez zapasu jednorazowo kupia i zabiora wiecej (do 6 dni potrzeby; rabunek najwyzej polowa zywnosci wsi na dobe) -
  pierwsze doby po wczytaniu zapisu z ubytkiem w magazynach wsi blisko kryjowek i na polkach miast;
- liczba band: glodne bandy na ladzie nie werbuja i traca ludzi - tam, gdzie w promieniu 100 brak wsi z jedzeniem (MB-8; wylacznik przywraca stare);
- wydajnosc: raz na dobe, bez nic co klatke; najblizsze miasto bandy (miasta i zamki) 2-3 razy na dobe na bande; rabunek przeglada wsie swiata tylko dla
  bandy z brakiem (do 3 przejsc); stan w slownikach czyszczonych co dobe; zapis gry bez nowych kluczy (zapis Jeffa z 94/95 wczytuje sie bez zmian);
- bez latek Harmony; wszystko w watku glownym (doba, zdarzenie lupu).

**Status:** DO SPRAWDZENIA (build Armoury kod 0; autotest w nocy - WGRANIE 96).

**Test w grze:** rozbij bande z kryjowki (najlepiej taka, ktora krecila sie kilka dni) - w lupie powinno byc jedzenie (zboze, ryby) i zwykle kilkadziesiat
zl; rozbij piratow na morzu - jedzenie, jesli mieli pieniadze u pasera; w MCM "Bands Eat Food" wylaczony -> bandy znow nie jedza, a paser bierze cale
ich jedzenie.
