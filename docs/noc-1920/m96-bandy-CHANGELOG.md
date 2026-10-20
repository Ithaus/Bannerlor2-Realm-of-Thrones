## m96-bandy - bandy jedza; zapas jedzenia i kiesy w polu; lup jedzenia i zlota u zwyciezcow (Armoury)

**Dla Jeffa (prosto):** bandy od teraz jedza - jedna sztuka jedzenia na 20 ludzi dziennie, tak jak wojsko. Jedzenie maja z lupu (karawany, wozy wsi),
dokupuja je przez pasera za wlasne pieniadze albo zabieraja z magazynu pobliskiej wsi. Banda trzyma w jukach jedzenie na kilka dni i drobne na kilka dni
w kiesie (reszta idzie do kryjowki), wiec gdy ja rozbijesz, znajdziesz u niej i jedzenie, i troche zlota. Glodna banda traci ducha, a po dwoch dniach
glodu ludzie zaczynaja odchodzic - do lasu (puli wyrzutkow), nie znikaja. Wylacznik: MCM Armoury -> grupa "Iron Bank" -> **Bands Eat Food** (wylaczony = jak dotad).

**Problem:** Jeff 20.10: "jak pokonuje bande czy ogolnie armie, to oni powinni miec tez zloto i jedzenie, bandyci chodza bez jedzenia czy zlota?"

**Dowod (logi D:\Backup-Bannerlord\logi-testow\2026-10-20, WGRANIE 95 i 92):**
- Bandy nie jedza: dekompilacja `DefaultMobilePartyFoodConsumptionModel.DoesPartyConsumeFood` = false dla `IsBandit` (BK/BEE/ROT/NavalDLC tylko opakowuja);
  `DesertionCampaignBehavior` tylko lordowie, karawany, zalogi. Zywnosc z niczego dla band zamknieta (OutlawNoFreeFood: "zablokowana 41 nowym bandom").
- Cala zywnosc band szla do pasera: linia "Paser:" - bieg 160 dob z zapisu d240 (12-25-19) srednio **898 szt./d**, nowa kampania 120 dob (08-45-54)
  **207 szt./d**. Potrzeba band, gdyby jadly jak wojsko: ok. 16 500 ludzi w ok. 810 bandach (linia "Wyrzutki", 12-25-19) = ok. **825 szt./d**.
- Kiesy band w polu to prawie nic: "Skarbce kryjowek (nskarbiec)" d109235 - 500 band z kryjowka, **467 z pusta kiesa**, kiesy razem 8741 zl
  (dola 25% kiesy co dobe odnosi monete do kryjowki - kryjowki 78 678 zl).
- Zloto rozbitych band do zwyciezcow juz dziala (3.2 BeatenPurse): "Kiesy rozbitych (3.2)" bandy -> zwyciezcy srednio **5205 zl/d** (160 dob),
  **1292 zl/d** (nowa 120 dob); reszta przy zniszczeniu 0, zniklo 0.
- Juki pokonanych do zwyciezcow juz dzialaja: gra `MapEvent.LootDefeatedPartyItems` (kazda sztuka z szansa 0.5 x udzial zwyciezcy; dekompilacja
  `DefaultBattleRewardModel.GetLootItemChancesForWinnerParties` ... `* 0.5f`), reszta jukow rozbitej partii - R14 (`HorseFate.Spoils`): linia K1
  "dobytek rozbitych ... do zwyciezcow" srednio **235 szt./d** (160 dob), **175 szt./d** (120 dob), ponad udzwig - na polke najblizszej warowni.
- Armie i druzyny lordow (pkt 3 zadania): jedzenie z jukow - ta sama droga gry + R14 (jak wyzej); zloto - wodz traci 5% (najwyzej 10 000, gra; A6
  BattlePlunderFromLoserOnly - tylko z tego, co pokonany naprawde traci), sakiewki ludzi druzyny do przywodcy zwyciezcow (D11-6): **54 156 zl/d** z 775
  druzyn (160 dob), **35 113 zl/d** z 716 (120 dob). **Luki nie ma** - nic w tej paczce nie zmienia lupu lordow, dochodzi tylko pomiar jedzenia z ich jukow.

**Przyczyna:** gra nie karmi band (model jedzenia), a nasze prawo wyrzutkow konsekwentnie zamienialo kazda sztuke ich jedzenia w zloto (paser),
a kiesa co dobe w czwartej czesci wedrowala do kryjowki - bez progu, wiec banda w polu nosila grosze.

**Zmiana:** nowy plik `Armoury/src/BandFood.cs`, klucze MCM (grupa "Iron Bank" - tam stoja dzis klucze band, obok Outlaw No Free Food): **Bands Eat Food** (TAK), **Band Food Keep Days** (5), **Band Purse Keep Days**
(3), **Band Hunger Leave Share** (0.10). Wpiecia: `OutlawLaw.Daily` (po paserze `BandFood.Daily` + `MoneyLedger.Mark(MLife)`; glodna banda nie werbuje z
puli), `OutlawLaw.FenceSell` (paser bierze tylko zywnosc ponad zapas; "zostalo w jukach" bez zapasu), `OutlawLaw.StashDay` (dola z kiesy ponad zapas),
`OutlawLaw.Reset`, `BeatenPurse.SplitPostfix` (pomiar zlota z kies band, gracz osobno), `ArmouryBehavior` (sluchacz `OnLootDistributedToPartyEvent` - pomiar).
Doba bandy (po paserze, bez band w bitwie, zadaniu, nieumarlych i najazdow Wolnych Ludzi):
1. potrzeba = (ludzie + jency/2) / 20 sztuk (jak gra), ulamek przechodzi na nastepna dobe (stan tylko w sesji, nic do zapisu);
2. zapas = dzis + 5 dni; brak dokupuje przez pasera najblizszego otwartego miasta za wlasna kiese: najtansza zywnosc z polki, sztuka po sztuce
   (`ShelfBuy.Take`), cena targu x marza pasera z droga (OutlawFenceMarkup 1.5 -> OutlawFenceMarkupFar 2.0, jak sprzet); zaplata do kasy miasta,
   licznik sprzedazy kupcow (`TownFolk.Sale`, jak zaopatrzenie BK); w ksiedze kas - odcinek "bandy - zycie";
3. czego nie kupi - zabiera z magazynu najblizszej wsi (stan Normal) w promieniu 100 (dzien drogi), najwyzej na 2 dni; wies nic nie dostaje (rabunek);
   na morzu bez zakupu i rabunku;
4. je z jukow (najtansze najpierw) w ramce ksiegi towarow "jedzenie partii" (`GoodsLedger.FFood`);
5. glod: morale -5 x brak na dobe (gra wygasza 10%/d); po 2 dobach glodu z rzedu odchodzi 10% x brak ludzi na dobe (zdrowi i ranni po rowno, bez
   bohaterow) - do puli wyrzutkow i ksiegi ludzi ta sama droga co dezercja z zaleglego zoldu (WarLedger: `OutlawLaw.OnTroopsDeserted`,
   `PeopleLedger.OnTroopsDeserted`); banda bez ludzi rozwiazana jak przy dezercji gry (`DestroyPartyAction` bez sprawcy - kiesa do miasta, juki na polke,
   jency do puli - reguly juz czynne). Na morzu i z jencem-bohaterem (gracz, lord) bez odejsc;
6. kiesa: dola do kryjowki tylko z kiesy ponad 3 dni jedzenia po cenie zboza u pasera najblizszego miasta.

**Linia logu (log_grep):** `Bandy - jedzenie i kiesy (m96-bandy): dzien` - bandy jedzace, pominiete, potrzeba / zjedzone / kupione (zl, miasta) /
zabrane wsiom, najedzone / glodne (bez jedzenia, ponad 2 doby, morze, jeniec-bohater), morale z glodu, odeszli z glodu i rozpady, zapas w jukach (dob
potrzeby), kiesy band w polu (suma, z kiesa, mediana), zapas zostawiony przez dole, lup u zwyciezcow (jedzenie z band AI / gracz, zloto z kies band /
gracz, jedzenie z druzyn lordow AI / gracz), potkniecia. Narzedzie: `python -I tools/sprawdz_logi.py --grupa m96bandy [--baza-doby <log bazy>] <log>`.

**Oczekiwane w autotescie (160 dob z zapisu d240; P - pierwsze progi, do korekty z pomiaru):** linia w kazdej dobie z "Paser:", potkniecia 0;
potrzeba ok. 800-900 szt./d; zjedzone / potrzeba >= 0.85; glodne <= 25% band jedzacych; rozpady z glodu <= 2% band na dobe; zapas w jukach >= 2 dob
potrzeby od doby 10; lup jedzenia z band > 0 i zloto z kies band > 0; band i ludzi w bandach na koniec >= 80% bazy (WGRANIE 95: ok. 810 band, 16 500
ludzi); paser - zywnosc sprzedana spada z ok. 900 do kilkuset szt./d (INFO); "Paser:" i "Skarbce kryjowek" bez nowych NIE (NK-3, NK-11).

**Ryzyko / co sprawdzic (kontrola calosci):**
- regresje: paser (FenceSell) - przy kluczu off keep 0, kod jak dotad; NK-3 (dola / kiesy) - "kiesy przed dola" licza kiese ponad zapas, nadwyzka ponizej
  zlotowki doli poza suma (bez tego NK-3 "bez doli przy kiesach"); NK-11 - kasy kryjowek bez nowej kategorii (jedzenie nie bierze z kryjowek);
- kolizje: model jedzenia gry nietkniety (BK/BEE/ROT/NavalDLC bez zmian, UI, AI); FoodSnapshot/FoodRestore (zywnosc z niczego w bitwie) - nasza doba
  biegnie poza tickiem gry; najazdy Wolnych Ludzi poza regula (ich kontrola ludzi i towar do domu bez zmian); nieumarli nie jedza; zadania nietkniete;
- zamknieta ekonomia: zakup - polka miasta -> juki, kiesa -> kasa miasta; rabunek - magazyn wsi -> juki; jedzenie - ksiega towarow "jedzenie partii";
  odchodzacy - pula wyrzutkow i ksiega ludzi; nic z niczego, nic w nicosc;
- skala: bandy zjedza ok. 825 szt./d przy ok. 31 000 szt./d zywnosci, ktora wozy wsi sprzedaja miastom (linia "Towary (utarg wozow)": zboze 28 811 szt.)
  - ok. 2.7%; paser odda miastom mniej zywnosci (do sprawdzenia koszyk jedzenia ludu, dzis 88.5%); rabunek obniza magazyny wsi blisko kryjowek;
- liczba band: glodne bandy przestaja werbowac i traca ludzi - jesli wsi z jedzeniem w promieniu 100 jest malo (kryjowki w gorach, bandy na morzu),
  band moze ubyc (MB-8; wylacznik przywraca stare);
- wydajnosc: raz na dobe, bez nic co klatke; rabunek przeglada wsie swiata tylko dla bandy z brakiem (ok. 570 odleglosci); stan w slowniku po referencji,
  czyszczony co dobe z partii nieaktywnych; zapis gry bez nowych kluczy (zapis Jeffa z 94/95 wczytuje sie bez zmian);
- bez latek Harmony; wszystko w watku glownym (doba, zdarzenie lupu).

**Status:** DO SPRAWDZENIA (build Armoury kod 0; autotest w nocy - WGRANIE 96).

**Test w grze:** rozbij bande (najlepiej taka, ktora krecila sie kilka dni) - w lupie powinno byc jedzenie (zboze, ryby) i troche zlota; w MCM
"Bands Eat Food" wylaczony -> po wczytaniu bandy znow nie jedza, a paser bierze cale ich jedzenie.
