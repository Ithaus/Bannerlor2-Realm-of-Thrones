# AUDYT - zloto z niczego: czy wszystko znalezione? (11.10.2026)

Pytanie Jeffa: "czy to na pewno WSZYSTKIE dosypki zlota z niczego, wszystko zostalo znalezione? co mowia logi gry?"

**Zakres.** Kod w grze: zbior 31 (dfb12df + NightRest). Porcja A etapu 5 (`w-toku/zbior-e5`, DLL testu A gotowy) sprawdzona wybiorczo tam, gdzie
zmienia odpowiedz. Logi (Armoury, linia "Pieniadz swiata (bilans - przyczyny)"):
- **z31-A**: 120 dob nowej kampanii, zbior 31 (`kopia-z31-A\Armoury-2026-10-11_08-09-11.log`);
- **zima**: 120 dob, zbior 31 (`kopia-z31-B\Armoury-2026-10-11_08-50-00.log`);
- **728**: 728 dob, starszy kod - WGRANIE 28 (`kopia-728\Armoury-2026-10-10_20-58-02.log`).

d1 = dzien kampanii 108836 (pierwsza doba ksiegi, sam stan), bilans liczony od d2. **Wszystkie liczby sa na dobe gry**, bez przeliczania
na lata (rok Armoury = 364 dni; log 728 = dwa lata Armoury). "tys./d" = tysiace zlotych na dobe.

**Metoda.** Siedem niezaleznych przegladow: ksiega, logi, kod gry 1.4.8 (+SandBox, StoryMode, NavalDLC), BK + BKROTPatch, ROT, pozostale mody
z listy 37, nasze mody. Potem 40 weryfikacji sceptyka (kazde zgloszenie proba obalenia: kod + log) i jeden krytyk kompletnosci. Zrodla
dekompilacji i skrypty - rozdz. 6. Kodu i gry nikt nie ruszal.

Wzor ksiegi: **zmiana sumy zlota = zrodla z niczego - ujscia w nicosc + netto + reszta R**. "Suma" = posiadacze z `MoneyLedger.ReadHolders`
(MoneyLedger.cs:670-714) - rozdz. 3.

---

## 0. Dla Jeffa - prostym jezykiem

**Krotko: duze - tak, wszystkie. Male - nie wszystkie byly na liscie; znalezlismy 9 malych spoza etapu 5 i 2 dziury w samym pomiarze.**

**Co jest PEWNE** (dowod w kodzie i w logu):
1. Ksiega sie domyka. W kazdej z 965 dob "ARYTMETYKA OK", a reszta (to, czego ksiega nie umie nazwac) to srednio **-2,2 tys./d** (z31-A),
   **-1,9** (zima) i **-1,2** (728). Dla porownania: zloto przeplywa ok. 1,5 mln/d, a przybywa go +432 tys./d (z31-A, 187 -> 238 mln
   w 120 dob; w 728: 186 -> 486 mln).
2. **Wszystkie DUZE dosypki maja nazwe i liczbe.** Etap 5 je zamyka (A1, A3, A6, B5):
   - "zakupy" mieszczan: 374 tys./d (A1);
   - statki NavalDLC: ok. 275 tys./d (B5) - zwrot za statki rozbitych druzyn 226, pryzy z bitew morskich 35, handel netto 13;
   - pasmo notabli: 146 tys./d (A3);
   - kapital karawan notabli: 91 tys./d (A3);
   - regulator kas: 18 tys./d (A1, tryb 2);
   - udzial wodzow pokonanych w lupie: 12 tys./d (A6).

   Razem ok. 0,93 mln/d brutto. Uwaga przy czytaniu linii: suma "zrodel" 1,57 mln/d to NIE suma dosypek. Zold oddany sakiewkom (614 tys./d)
   i wyplata dochodu notablom (112 tys./d) to przelewy, ktore ksiega pokazuje jako dwie pozycje.
3. **Poza lista etapu 5 sa male zrodla, razem ok. 9 tys./d (120 dob) do 11 tys./d (728).** Kazde ma dowod w kodzie i w logu:

   | zrodlo | ile |
   |---|---|
   | zaulki - notabl-herszt dostaje 30 zl/d za zaulek | 6,6-6,9 tys./d |
   | drugie 10 000 dla nowego notabla od BK - kazdy notabl dostaje 20 000, nie 10 000 | 0,1-2,6 tys./d |
   | lup z rabunku wsi - 4 zl za spalony dom | 0,7 tys./d |
   | dochod za tier rodow bez lenn | 0,6-0,7 tys./d |
   | 5000 dla kazdej nowej zalogi po zdobyciu twierdzy | 0,1-0,45 tys./d |
   | nagrody turniejow AI - nasza latka nie dziala, bo RBM placi pierwszy | 0,1 tys./d |
   | perki po 5000 | 0,1 tys./d |

   Do tego rzadkie zrodla z BK: nowy rod szlachty 15 000 i herszt bandytow 10 000.

   Dzis to ok. 2% przyrostu zlota swiata. Po etapie 5 zostanie jednak tylko to - i to wiecej niz prog etapu (zmiana zlota swiata
   <= ok. 8,5 tys./d).
4. **Najwieksza dziura w pomiarze to ujscie, a nie zrodlo: koszt paktow Diplomacy.** Diplomacy pobiera od krola AI zloto za pakt o nieagresji
   i za pokoj, i nikomu go nie daje (`ChangeHeroGold`, bez zdarzenia). To -2,7 tys./d i tlumaczy CALA ujemna reszte w 120 dobach. Przez to
   reszta jest ujemna i zaslania male dodatnie zrodla.
5. **Pulapka w kodzie porcji A (juz napisanym):** A3 przechwytuje tylko 10 000 od gry. Drugie 10 000 od BK przejdzie, a test A3
   ("nowi notable 0 z niczego") i tak da TAK. Trzeba to poprawic przed testem A.

**Co jest PRAWDOPODOBNE** (wzor w logu zgodny z kodem, ale nie zmierzony wprost):
- 3 skoki reszty +36..43 tys. to nowy rod szlachty BK: licznik "gentry" rosnie w tej samej dobie.
- 7 skokow po +10 tys. to herszt bandytow BK: skok "bohaterowie: inni" o 10 000.
- Paczki po 5000 w "inne" to perk Insurance Plans.
- Ujemne skoki ok. -10,5 tys. to BK "kompromis finansowy" (zloto krola znika).
- Tygodniowy minus ok. -3 tys. to BK, ktore co tydzien zeruje kiese dworzan.

**Czego NIE DA SIE sprawdzic bez pomiaru albo testu:**
- Stale male tlo reszty +0,15..0,6 tys./d w dobach bez zdarzen. Nie wiadomo, z czego jest.
- 2 doby bez wyjasnienia: z31-A d39 +10 125; 728 d353 +41 855 (tego dnia upadlo krolestwo).
- 728 d251 -129 175. Kandydat: kompromis BK.
- Utajona dziura G24, ktora ozyje dopiero po A1 tryb 2: lord sprzedaje miastu za wiecej, niz miasto ma w kasie, a roznica bierze sie
  z niczego. To samo dotyczy warsztatow BK (N3). Dzis kasy miast maja co najmniej 26,7 tys., wiec tego nie widac.
- Rzadkie zdarzenia, ktorych 120 dob nie pokazuje: upadki krolestw, rebelie, fabula ROT, polityka NavalDLC "Coastal Guard Edict".
- Zloto dopisane komus spoza sumy (np. wirtualne skarbce BetterEconomy). Ksiega z zalozenia go nie widzi, dopoki nie wroci do swiata.
- Zrodla tylko gracza (zadania, arena, okup, turnieje gracza): w autotescie gracz nic nie robi, wiec zawsze daja 0.

**Wniosek:** lista etapu 5 jest dobra, ale niepelna. Proponuje:
- dopisac do A3: drugie 10 000 BK i zaulki;
- naprawic latke turniejow (RealPayer);
- dodac jedna mala paczke "drobne zrodla gry": rabunek, zaloga, tier, perki;
- dodac jedna paczke "BK i Diplomacy bez platnika albo odbiorcy";
- dopisac do pomiaru E5/0 8 brakujacych okien (rozdz. 5).

---

## 1. TABELA - zrodla zlota z niczego

Kolumna "ile": z31-A / zima / 728, srednia na dobe calego biegu (w nawiasie okres, jesli sie mocno rozni). "Ksiega": czy linia bilansu ma
na to pozycje. "Pewnosc": P = kod + log, S = wzor w logu zgodny z kodem, ? = nieznane.

### 1a. Duze - w etapie 5

| # | zrodlo (miejsce w kodzie) | mod | ile tys./d (z31-A / zima / 728) | ksiega mierzy | kto zamyka | pewn. |
|---|---|---|---|---|---|---|
| 1 | "zakupy" mieszczan: towar znika z polki, kasa miasta dostaje cene (BK `ItemConsumptionPatch` EconomyPatches.cs:555, zastepuje gre ItemConsumptionBehavior.cs:170) | BK (gra) | 373,5 / 313,5 / 469,5 (728 d365-728: 506,6); zamki cofa 110 (43 / 35 / 74, poza swiatem) | tak - "zakupy" mieszkancow (zgodne z BK ConsumedValue co do zl, 119/119 dob) | **A1** R1 cofniecie (w `zbior-e5`), potem **B1** (placi lud) | P |
| 2 | statki rozbitej/rozwiazanej druzyny dla glowy rodu, przy rozwiazaniu na ladzie placone 2 razy (NavalDLC NavalShipDistributionCampaignBehavior.cs:67; premia za naprawe ShipTradeCampaignBehavior.cs:426) | NavalDLC | 226,4 / 210,7 / 277,8 (z31-A d31-120: 291,9) | tak - "statki NavalDLC inne"; niezalezny licznik 181 zgodny co do zl | **B5** (181: ShipsToPort, ShipTradeThroughPort) | P |
| 3 | pasmo notabli: dosypka do 4 500 (NotablePowerManagementBehavior.cs:58) | gra | brutto 146,0 / 147,7 / 110,8; d1 jednorazowo ok. 2 mln (ujscie :52 - rozdz. 2) | tak - "pasmo notabli" | **A3** (w `zbior-e5`, NotableGold) | P |
| 4 | kapital nowej karawany notabla (CaravanPartyComponent.cs:247 `InitializePartyTrade`, bez GiveGoldAction) | gra (+NavalDLC model) | 91,3 / 82,5 / 68,1 (d1-30 ok. 170) | tak - Z2 notable (lordowie BK: S31 = 164a-6, co do zl) | **A3** (w `zbior-e5`, NotableCaravanCapitalReal) | P |
| 5 | pryzy za statki zatopione w bitwach morskich (MapEvent.cs:1785-1817 -> PlunderedGold) | gra + NavalDLC | 35,2 / 23,6 / 70,3 | tak - w "bitwy" (+ info 164a-0) | **B5** (ShipPrizesToPort) | P |
| 6 | handel statkami: sprzedaz portowi + premia MerchantPrince (ChangeShipOwnerAction.cs:51-64, ShipTradeCampaignBehavior.cs:412) | gra + NavalDLC | brutto 26,7 / 25,4 / 25,4; netto po kupnie w nicosc +13,1 / +12,2 / +17,1 | tak - "statki: sprzedaz osadom..." (licznik 181 zgodny) | **B5** (ShipTradeThroughPort) | P |
| 7 | regulator kas: dosypka do 10 000 + 12 x dobrobyt (DefaultSettlementEconomyModel.cs:75, ItemConsumptionBehavior.cs:76) | gra | 18,3 / 16,9 / 17,0 | tak - "regulator kas dosypal" | **A1** tryb 2 (TownPurseRegulator = 2 w `zbior-e5`) | P |
| 8 | udzial wodzow pokonanych w lupie: gra zdejmuje wodza z partii (RemovePartyLeader, MapEvent.cs:1979) PRZED `CommitGoldChanges`, wiec jego 5% (do 10 000) nie schodzi, a zwyciezcy i tak dostaja (MapEventParty.cs:512) | gra | 12,0 / 9,8 / 12,9 | tak - w "bitwy" (= bitwy - N4 - statki) | **A6** (w `zbior-e5`, BattlePlunder; wodz zapamietany w SplitPostfix) | P |
| 9 | nowy notabl 10 000 od gry (NotablesCampaignBehavior.cs:47) | gra | 0,08 / 0,34 / 2,3 | tak - "nowi notable" | **A3** | P |

Warunek A6 ("> 5 tys./d na ladzie") jest juz spelniony w logach: 10-13 tys./d.

### 1b. Male - POZA etapem 5 (nowe)

| # | zrodlo (miejsce w kodzie) | mod | ile tys./d (z31-A / zima / 728) | ksiega mierzy | kto zamyka | pewn. |
|---|---|---|---|---|---|---|
| 10 | **zaulki**: +30 zl/d za kazdy zaulek notabla-herszta w jego osadzie (DefaultClanFinanceModel.cs:897-903, wyplata ClanVariablesCampaignBehavior.cs:484). Reszta dochodu notabla to przelew (K1) | gra (SandBox zaulki) | 6,6 / 6,8 / 6,9 (190-247 zaulkow; netto co dobe dokladnie x 30) | tak, ale bez pozycji w bilansie - tylko "Obieg notable: z niczego netto" | **NOWE** - dopisac do A3: z kasy miasta zaulka (Spare) albo 0 | P |
| 11 | **drugie 10 000 nowego notabla**: prefiks BK na `HeroCreator.CreateNotable` (BK NotablePatches.cs:77-81) daje drugie 10 000 po `OnHeroCreated` gry | BK | 0,08 / 0,34 / 2,3 (728 d365-728: 2,6) | tak, ale jako "inne" (poza oknem "nowy-notabl"); dowod: 1/1, 4/4, 80/80 dob "inne" = "nowi notable" + tlo | **NOWE** - dopisac do A3 (kontekst na CreateNotable). W `zbior-e5` NIE ma | P |
| 12 | lup z rabunku wsi: 4 zl za stracony hearth x mnoznik (RaidEventComponent.cs:179-188) | gra | 0,69 / 0,70 / ok. 0,7 (rosnie z wojna: z31-A d61-90 0,95; max 2,1 tys. w dobie) | tak, ale jako "inne" (+ linia "Hearth (E4/0): lup z niczego", rowna co do zl) | **NOWE** (E5/A7) | P |
| 13 | dochod za tier: rody AI bez lenn poza krolestwem i najemnicy bez umowy 185: Tier x 80 (+40) (DefaultClanFinanceModel.cs:133-137) | gra | 0,72 / 0,59 / 0,61 (z31-A d1-39: 1,4) | czesciowo - w netto "rozliczen rodow", nazwane w "rody - przyczyny" | **NOWE** (E5/A7: rozszerzyc MercContract.TierPostfix) | P |
| 14 | kiesa startowa nowej zalogi 5 000 (GarrisonPartyComponent.cs:96, `PartyGoldLowerThreshold`; po zdobyciu twierdzy stara zaloga ginie, nowa powstaje - ChangeOwnerOfSettlementAction.cs:33-41) | gra | 0,08 / 0,13 / 0,44 (5 000 na zdobycie: 2 / 3 / 64) | **NIE** - reszta (R ok. +5 000 w kazda dobe zdobycia; mediana R - 5000 = +380) | **NOWE** (E5/A7 + pozycja w ksiedze) | P |
| 15 | nagrody turniejow AI: prefiks RBMTournament (RBMTournament.cs:592) placi sam; RealPayer.TourneyPre ma Priority.Low (RealPayer.cs:286), wiec otwiera sie za pozno | RBM | 0,095 / 0,08 / 0,12 | tak - "nagrody turniejow"; RealPayer "gra chciala 0 (0 wyplat)" przez caly bieg | **NOWE** - poprawka E3/1 (priorytet). W `zbior-e5` dalej Low | P |
| 16 | perki po 5 000: Insurance Plans (DestroyPartyAction.cs:21), Rapid Development (WorkshopsCampaignBehavior.cs:326) | gra | 0,13 / 0,08 / 0,1 (3 / 2 / 14 paczek) | tak, ale jako "inne" | **NOWE** (E5/A7) | S (przypisanie do perkow) |
| 17 | nowy rod szlachty BK: `ChangeHeroGold(cost)`, przelew cost do wlasciciela majatku, `ChangeHeroGold(15 000)` (BKGentryBehavior.cs:516-522). Majatek bez wlasciciela: ksiega widzi falszywe "w nicosc - inne" = cost | BK | 0,13 / 0 / 0,04 (1 / 0 / 2 zdarzenia; 15 000 + cost, gdy majatek ma wlasciciela) | **NIE** - reszta (R - "inne w nicosc" = 15 245..16 571) | **NOWE** (E5/A8) | S |
| 18 | herszt bandytow BK: `ChangeHeroGold(10 000)` + juki "sprzedane" po cenie miasta bez kupca (BanditHeroComponent.cs:218, :140-151) | BK | 0 / 0,08 / 0,08 (0 / 1 / 6 zdarzen) | **NIE** - reszta (skok "bohaterowie: inni" o 10 000) | **NOWE** (E5/A8) | S |
| 19 | warsztat BK przy pustej kasie: warsztat dostaje cala cene, kasa miasta spada tylko do 0 (BK EconomyPatches.cs:725-726 + SettlementComponent.ChangeGold) | BK | 0,17 (1 zdarzenie) / 0 / 0,0 | tak - N3 | **NOWE** (S). Urosnie po A1 tryb 2 | P |
| 20 | **utajone G24**: lord sprzedaje miastu konie i lup bez patrzenia na kase (PartiesBuyHorse :103/:126, PartiesSellLoot :25); `ApplyForSettlementToCharacter` nie obcina, `ChangeGold` obcina kase do 0 (SellItemsAction.cs:62-66) | gra | 0 dzis (najnizsza kasa miasta 26,7 / 29,4 tys.; w 728 raz 4 200) | **NIE** - reszta | **NOWE** - pomiar przed testem A (A1 tryb 2 jest w `zbior-e5`) | P (kod) / ? (rozmiar) |
| 21 | NavalDLC "Coastal Guard Edict": zaloga portowa dostaje doplate do 5 000, rod +15% doplaty z niczego (NavalDLCClanFinanceModel.cs:20-36, :108-139) | NavalDLC | ? - zaden log nie mowi, czy jakies krolestwo ma te polityke | czesciowo (netto rozliczen) | pomiar E5/0 | ? |
| 22 | perki: Salvage (+ licznik cel, NavalDLC ShipTradeCampaignBehavior.cs:437-440), Spring of Gold (do 1 000/d, DefaultClanFinanceModel.cs:160-164). Traveling Rumors odpada - zastepuje go prefiks BK, a ten daje Round(-0,15) = 0 | gra / NavalDLC | ?, male | NIE / w netto rozliczen | pomiar E5/0 | ? |
| 23 | uczta BK: gospodarz placi tyle, ile ma, a miasto dostaje pelna cene (Feast.cs:210-211) | BK | ?, male (tylko biedny gospodarz) | NIE - reszta | E5/A8 | P (kod) |
| 24 | trybut przyjety i kontrakty najemnikow gry: odbiorca dostaje teraz, placacy splaca ujemny portfel pozniej (DefaultClanFinanceModel.cs:560, :511) | gra (+Diplomacy ustawia trybut) | brutto 0,11 / 0,14 / 2,06; **netto** 0 / +0,007 / +0,13 (niesplacony dlug) | tak - w rozliczeniach rodow (para) | nic - platnik odroczony; obserwowac | P |

### 1c. Tylko gracz (w autotescie 0; w grze Jeffa - tak)

| zrodla | mod | ksiega |
|---|---|---|
| nagrody zadan (Issues, ok. 40 miejsc), arena, gry planszowe, zaklady, LordConversations (czesc wprost w `PartyTradeGold`), Incidents, rzemioslo | gra, SandBox | GiveGoldAction - "inne"; wpisy wprost - reszta |
| okup: placacy dostaje kiese = cena + 1 000 (RansomOfferCampaignBehavior.cs:174-177) | gra | reszta |
| TXP premie, GrandTourney zwroty i utarg, RealisticCaptivity Vault/Work, akcje BK gracza (BKSettlementActions :1541 bez obciecia kasy), pojedynki ROT | mody | "inne" albo reszta |
| Spoils of War (RealisticLoot) - zablokowane przez SpoilsSeal (128); StoryMode - nie dotyczy (Sandbox) | mody | 0 |

### 1d. Juz zamkniete (0 we wszystkich trzech logach)

- ROT zapomoga Tier x 5 000 (`RotBailoutPrefix`, "przechwycona"; bez licznika);
- ROT zwrot przy podmianie rekruta (zablokowane 66 861 / 58 049 / 312 424 zl przez bieg);
- ROT zold zaciagu (Leaks310);
- DTE doplata z taboru (AiGear);
- BEE: renta, wyplaty skarbcow, clo, dostawy (170; klucze 19/19);
- odszkodowania Diplomacy (165 CrownIncome.ReparationPrefix);
- jency, lup z oblezen, myto, jency z rabunkow BK (RealPayer 164a-1);
- lup z cial (CasualtyLoot 164a-2);
- niewolnicy BK (BkSeal34; dekret dostepny tylko graczowi);
- majatki BK (187);
- skarbiec krolestwa +1 000/d i losowe 100-400 tys. (KingdomTreasury, 4 z 4 stalych);
- umowy handlowe vanilla (KingdomTreasury);
- rebelia 50 000 (RotLeaks37, 0 rebelii);
- kapital warsztatu przy bankructwie i wojnie (WorkshopTrade). Przejecie warsztatu przez BK w wojnie (BKWorkshopBehavior -> ApplyByWar)
  idzie ta sama metoda gry - prawdopodobnie tez zamkniete, do potwierdzenia;
- bandy, dezerterzy, piraci (OutlawLaw);
- kapital karawan lordow BK i DTE (164a-6).

### 1e. Wygladaja na zrodla, ale NIMI NIE SA (przelewy albo granica sumy)

| pozycja w linii | dlaczego nie dosypka |
|---|---|
| "zold oddany do obiegu" 613,9 / 599,5 / 807,1 | druga polowa "rozliczen rodow na minus" (619,0 / 603,1 / 808,5). Oddane nigdy nie przekracza zoldu naliczonego (0 z 965 dob). Netto to ujscie ok. 5-6 tys./d: zold Innych 4,2, przyciete z braku salda 1,5 |
| "wyplata dochodu notablom" 112,5 brutto | 105,9 zeszlo z kapitalu warsztatow i kas karawan (K1). Z niczego tylko zaulki (wiersz 10) |
| "bitwy" 17,3 dla wodzow z kies partii bez wodza | przelew 3.2 BeatenPurse (N4 -16,8) |
| Z4 "bohaterowie wracajacy" 0,34 / 2,25 / 1,08 | powrot zlota, ktore wczesniej wyszlo (U7, N5), plus jednorazowy zapas startowy 30 / 253 / 346 tys. (vanilla "Marnotrawny syn" wylacza mlodego lorda na 50 dob). Netto 0,25 / 1,07 / 0,18 i nie rosnie |
| N31 portfele wezwan +0,09 / +0,003 / +0,43 | para z U31 "wplaty do ujemnego portfela" (z31-A: 10 871 = 10 871 co do zl) |
| RB5 w 728 (+156/d, stary kod) | polowa pary reszt: plus w RB5, minus w RB1/RB3 w ciagu ok. 10 dob |

---

## 2. Ujscia w nicosc (krotko)

### 2a. Zmierzone (z nazwa w linii)

| ujscie | ile tys./d (z31-A / zima / 728) | kto zamyka / uwaga |
|---|---|---|
| regulator kas skasowal | 361,1 / 345,6 / 410,8 | A1 tryb 2 (zloto zostaje w kasach). To zmienia bilans bardziej niz zdjecie dosypki |
| rozliczenia rodow na minus (zold) | 619,0 / 603,1 / 808,5 | para z "zold oddany"; prawdziwe ujscie ok. 5-6 tys./d (Inni bez monety) |
| pasmo notabli - nadwyzka ponad 10 500 | 106,2 / 84,2 / 134,4 | A3 (do kasy miasta). Od d121 w 728 pasmo netto wiecej zabiera niz daje - A3 musi zamknac oba kierunki naraz (tak jest w kodzie) |
| werbunek, awanse, doplata podmiany ROT (to) | ok. 67 / 64 / 55 brutto | w wiekszosci oddane: LevyGold 50,5 / 47,3 / 32, 164a i D11-5 25,2 / 24,7 / 15 |
| N1 BK rynek osady (-1% kasy + blad znaku z kiesy notabla) | -15,9 / -16,0 / -81,0 (728: -12 / -67 / -113 kolejno) | A1 R6 (w `zbior-e5`) |
| N4 kiesy partii bez wodza w bitwach | -16,8 / -15,0 / -16,7 | para z czescia "bitwy" (3.2) |
| statki: kupno od osad | 13,6 / 13,2 / 8,3 | B5 (do kasy portu) |
| U6 BK wydatki lordow (cena karawan, donatywa) | 9,2 / 9,1 / ok. 5 | cena karawan w wiekszosci wraca (164a-6); donatywa ok. 1 tys./d w nicosc |
| N6 skarbce upadlych krolestw | 0 / 0 / -5,1 (rok 2: -10,1) | podejrzenie falszywego ujscia (rozdz. 4) |
| U8 kiesy znikajacych partii (garnizony) | 0,15 / 0,13 / 1,4 | prawdziwe; para z wierszem 14 |
| U7, U2, "to/inne" (gracz), N5 | ponizej 1,1 | - |

### 2b. NIEzmierzone (siedza w reszcie)

| ujscie | ile | pewn. | propozycja |
|---|---|---|---|
| **Diplomacy - koszt paktu o nieagresji i pokoju** (GoldCost.cs:84 `_giver.ChangeHeroGold(-Value)` bez odbiorcy; NAP 500 + 100 x n do 25 500, pokoj do 500) | z31-A -2,66 tys./d (39 NAP + 18 pokojow; -316 tys. w dobach z paktem, wiecej niz cala reszta -261 tys.); zima -2,1 (-248 tys.); 728 m.in. d275 -103 917 (pakt Yi Ti) | P (kwoty dokladnie ze wzoru; R w dobach z paktem srednio -9 308, bez zdarzen Diplomacy +1 131) | pomiar E5/0 + E5/A8 (odbiorca) |
| BK `Demand.FinancialCompromise`: krol placi (10 000 + 10 x dochod) x (1 - 0,2 x hojnosc), nikt nie dostaje (Demand.cs:147-150) | ok. -10,5 tys. na zdarzenie, ok. -0,4 tys./d; kandydat na 728 d251 -129 175 | S | jw. |
| BK `BKCourtierBehavior.DrainGold`: co tydzien zeruje kiese dworzan (:128-134) | ok. -3 tys./tydzien (dzien %7 == 0 wyraznie bardziej ujemny) | S | jw. |
| BK zakupy dworu na sezon: towar z polki, miasto nic nie dostaje (BuyGoodsAction.cs:61) | kilka tys. na sezon | S | jw. |
| ROT podmiana rekruta przy pustej kiesie pana: zdarzenie podaje pelna kwote, a kiesa tnie do 0 | ujscie zawyzone, wiec reszta wychodzi na plus | P (kod) | pomiar: faktyczny spadek kiesy |
| gracz: RealisticBannerlord (oplaty), TXP losowanie nagrody, ROT zakupy | 0 w autotescie | P | - |

---

## 3. Posiadacze zlota spoza sumy i co z nimi

Ksiega widzi tylko zmiany SUMY. Zloto dopisane komus spoza niej jest niewidoczne, a jego wyplata do swiata wyglada jak "z niczego".

| posiadacz | stan w logach | co z nim |
|---|---|---|
| bohaterowie Dead i Disabled (`Hero.AllAliveHeroes` ich nie ma) | Disabled: 30 tys. (z31-A), 253-322 tys. (zima), 346-703 tys. (728, max d53); przejscia Z4 / U7 / N5 | nie dosypka (rozdz. 1e). Do rozwazenia: wliczyc Disabled do sumy (Z4 i U7 znikna) albo pokazywac Z4 netto |
| skarbce krolestw wyeliminowanych | N6: 728 razem -3,7 mln (rok 2 -10 tys./d) | sprawdzic, dokad trafia skarbiec przy upadku (728 d353: N6 -31 745 i tego samego dnia R +41 855) |
| ujemny `Kingdom.CallToWarWallet` | Seal31: U31 / N31 - para, domyka sie | nic |
| `Kingdom.TributeWallet`, `Clan.TributeWallet`, `Kingdom.MercenaryWallet` (dlugi, <= 0) | trybut: z31-A +108 / -108; zima niesplacone 817; 728 netto +0,13 tys./d | zobowiazanie - obserwowac. Dodatni TributeWallet (Diplomacy) gra nigdy nie wyplaca - utkniety (ujscie) |
| vanilla umowy handlowe (`Kingdom1GoldGained` / `Kingdom2GoldGained`) | zerowane przez KingdomTreasury (PolicyIncomeConserved) | zamkniete |
| BetterEconomy: skarbce miast i zamkow (`LocalTreasuryGold`, `ActiveProjectProgressGold`, `PlayerTreasuryGrantLockedGold`), clo umow (`AccruedCustomsA/B`), `RentDue` | rosna z niczego (podatek wirtualny, renta majatkow), ale 0 wyjsc do swiata (klucze 19/19) | A5: nie ruszac `TownTreasurySurplusPayoutFraction` / `TownTreasuryWarPayoutFraction` (musza zostac 0) |
| partie nieaktywne (`!IsActive`) | BK SetTravel i ROT HideMainParty - tylko gracz (jego zloto w `Hero.Gold` jest liczone) | nic |
| ukryte pole `_partyTradeGold` partii lorda z wodzem (getter zwraca kiese wodza, MobileParty.cs:425-445) | niezmierzone; wraca do sumy, gdy partia traci wodza | kandydat na stale tlo reszty - pomiar E5/0 |
| RealisticCaptivity `Homes.Vault`, pula GrandTourney, zaklad turnieju vanilla, RealisticLoot `RemainingGold` / `_lootGold` | tylko gracz (Spoils zablokowane) | nic |
| naleznosci: `DebtToKingdom`, pozyczki IronBank, CrownBorrow, DebtLadder, Ransom178, `CrownIncome._debts` | to kwoty nalezne, nie zloto | nic |
| IronBank - obciecie ujemnego kapitalu do 0 | kapital >= 2,65 mln, nie dziala | nic |
| `MobileParty.PartyTradeTaxGold` | licznik statystyczny, nikt go nie wydaje | nic |
| pule Armoury (`_held`, `_court`, zamowienia Qohor, MaterialOrders, CastlePurse) | tarcze i znaczniki wewnatrz kas | nic |

---

## 4. Reszta ksiegi w trzech logach

| log | okres | dob | zmiana sumy sr. | **R sr.** | sr. abs(R) | max abs(R) (doba) | dob abs(R) > 10 tys. | RB (nasz tick) |
|---|---|---:|---:|---:|---:|---|---:|---:|
| z31-A | d1-30 | 29 | +90,6 tys. | -7 264 | 7 467 | -36 240 (d5) | 9 | -264 |
| z31-A | d31-120 | 90 | +542,5 tys. | -561 | 2 291 | +36 148 (d80) | 5 | -206 |
| z31-A | **calosc** | 119 | +432,4 tys. | **-2 195** (suma -261 155) | 3 552 | -36 240 (d5) | 14 | -220 |
| zima | d1-30 | 29 | +51,1 tys. | -4 983 | 5 500 | -34 331 (d10) | 7 | -153 |
| zima | d31-120 | 90 | +477,2 tys. | -930 | 2 273 | -23 803 (d57) | 4 | -320 |
| zima | **calosc** | 119 | +373,4 tys. | **-1 918** (suma -228 203) | 3 060 | -34 331 (d10) | 11 | -279 |
| 728 | d1-120 | 119 | +422,0 tys. | -1 663 | 3 850 | +41 139 (d82) | 14 | -146 |
| 728 | d121-364 | 244 | +486,4 tys. | -1 261 | 4 375 | -129 175 (d251) | 23 | -166 |
| 728 | d365-728 | 364 | +360,5 tys. | -943 | 2 810 | -30 957 (d658) | 28 | -162 |
| 728 | **calosc** | 727 | +412,8 tys. | **-1 168** (suma -848 889) | | | 65 | -161 |

Dodatkowe dane:
- Bez nowego pomiaru stara ksiega (R0) dawalaby -33,4 / -21,7 / -131,2 tys./d.
- Probki swiata wokol okien: 690 w z31-A, 0 rozjazdow.
- Nasz tick: w kodzie zbioru 31 tylko renty RB1 (-213 / -270/d); RB5 = 0.

**Co tlumaczy reszte:**
- **Ujemna:**
  - koszt paktow Diplomacy - cala ujemna reszta w 120 dobach;
  - BK kompromis finansowy i dworzanie [S];
  - najwiecej w d1-30 (start kampanii; w zimie i 728 rownolegle N5).
- **Dodatnia:**
  - +5 000 w kazda dobe zdobycia twierdzy (nowa zaloga, wiersz 14; 728: 64 doby, srednio +4 953);
  - +15 tys. (+ cost) przy nowym rodzie szlachty BK. Tlumaczy 3 skoki "nie do wyjasnienia": z31-A d80 +36 148, 728 d82 +41 139
    i d134 +43 364. R minus "inne w nicosc" = 15 324 / 15 245 / 16 571;
  - +10 tys. przy hersztach BK (zima d115; 728 d522, d567, d571 i inne);
  - artefakt podmiany ROT;
  - stale tlo w dobach bez zdarzen Diplomacy: mediana +147 (z31-A), +233 (zima), +583 (728, ze starym RB5). Kandydaci, bez pomiaru:
    ukryte `_partyTradeGold`, artefakt ROT, perk Salvage.
- **Niewyjasnione:**
  - z31-A d39 (dzien 108874) +10 125 - bez zmiany licznika gentry, "inni" i Diplomacy;
  - 728 d353 (109188) +41 855 - tego dnia N6 -31 745; wyglada na ujscie liczone, a zloto zostalo w swiecie;
  - 728 d251 (109086) -129 175 - bez zdarzenia Diplomacy w oknie doby. Kandydat: BK kompromis; "glowy rodow" tego dnia +4 tys.
    przy zwyklych +170..400 tys.
- **Korelacja** R z liczbami 139 linii dziennych slaba (abs(r) <= 0,27) - zadne jedno zjawisko nie steruje reszta.

**Granice dowodu.** R ok. 0 dowodzi tylko, ze kazda zmiana SUMY ma nazwe. Nie dowodzi kompletnosci, bo:
- zloto u posiadaczy spoza sumy jest niewidoczne;
- skrzynki netto (rozliczenie rodu, BK rynek, warsztaty BK, bitwy partii bez wodza) moga przykryc zrodlo kasowaniem w tej samej skrzynce;
- zobowiazania tworza zloto dzis, a kasuja pozniej;
- bledy przeciwnego znaku sie znosza. Wlasnie tak jest: ujemne pakty Diplomacy przykrywaja dodatnie garnizony, gentry i hersztow.

Skrzynka rodu sprawdzona osobno: bez zoldu rozliczenia zmienilyby swiat o +1,17 tys./d, z tego tier 0,72 - duzej ukrytej dosypki tam nie ma.

---

## 5. Propozycje

### 5a. Przed testem A (kod porcji A jest juz w `w-toku/zbior-e5`)

| # | co | wielk. | dlaczego teraz |
|---|---|---|---|
| A3' | NotableGold: kontekst (prefiks First + finalizer) na `HeroCreator.CreateNotable`, zeby przechwycic drugie 10 000 BK. **Decyzja Jeffa:** uciac (notabl dostaje 10 000 jak w grze, z kasy miasta) - rekomendacja - albo 20 000 z kasy. Test A3: wiersz "inne w dobach z nowymi notablami ~ tlo" | S | Dzis A3 przechwytuje tylko `NotablesCampaignBehavior.OnHeroCreated`, wiec test "nowi notable 0" da falszywe TAK. Po A3 nadwyzka z pasma idzie do kasy miasta, a 10 000 BK staje sie stalym nowym zlotem kas miast |
| RP' | RealPayer.TourneyPre: priorytet wyzszy niz Normal (np. HarmonyBefore "com.rbmt" albo High, nadal ponizej First okna O11). Test: "nagrody turniejow" w zrodlach = 0, RealPayer > 0 wyplat | S | zamkniecie E3/1 jest martwe w calym zestawie (0 wyplat w 965 dobach) |
| G24 | licznik w oknie prowizji: osada kupuje (SellItemsAction, partia sprzedaje) za wiecej, niz ma w kasie; N3 rozbic na "niedoplata kasy". Latka (obciecie kwoty do kasy) dopiero, gdy licznik > 0 | S | A1 tryb 2 jest w DLL testu A - biedne miasta moga zejsc nisko i obie dziury ozyja |
| d1 | w tescie A sprawdzic pierwszy tick pasma: ok. 2 mln z niczego w d1 przychodzi juz po GameStarted, wiec pod A3 zaplaca je kasy miast albo notable zostana ponizej 4 500 | - | ryzyko wyniku A3 i kas miast w d1 |

### 5b. Nowe paczki etapu 5 (po tescie A)

| paczka | zakres | wielk. | decyzje Jeffa |
|---|---|---|---|
| **A3 rozszerzenie: zaulki** (moze isc z A3') | 30 zl/d za zaulek (wiersz 10): kontekst w oknie `DailyTickHero`, czesc za zaulki placi kasa miasta zaulka (TownPurse.Spare), przy braku - 0. Od B1 placi kiesa ludu | S | z kasy miasta czy 0? |
| **E5/A7 - drobne zrodla gry** | (1) rabunek wsi (wiersz 12): lup za hearth z kiesy wsi do jej wysokosci, albo 0; (2) nowa zaloga (wiersz 14): dostaje kiese starej zalogi, a gdy jej nie ma - z kasy osady albo 0; jednoczesnie znika falszywa para U8/R; (3) tier (wiersz 13): `MercContract.TierPostfix` na wszystkie rody AI bez lenn (0); (4) perki 5 000 (wiersz 16) dla AI: 0 albo z kasy | M (4 x S) | platnik rabunku; zaloga: kiesa starej czy 0; tier 0 |
| **E5/A8 - BK i Diplomacy bez platnika albo odbiorcy** (najpierw pomiar z 5c, potem kod) | zrodla: nowy rod szlachty (15 000 i cena majatku - z kasy osady majatku albo rod startuje z 0, a majatek przechodzi bez ceny), herszt BK (10 000 z kryjowki albo 0; juki przez kase miasta), uczta (miasto dostaje tyle, ile zaplacil gospodarz). Ujscia: koszt paktu Diplomacy (do drugiego krola albo do skarbca drugiej strony), kompromis BK (do skarbca), dworzanie (do pana dworu), zakupy dworu (zaplata do kasy miasta) | M | odbiorca kosztu paktu; gentry i herszt: 0 czy z kasy |
| **N3 / G24** | jesli licznik z 5a > 0: obciac zaplate do tego, co kasa naprawde ma (BK ProduceOutputPrefix, SellItemsAction ze strony osady) | S | - |

Po A1 + A3 (+A3') + A6 + B5 + A7 + A8 zostaje (ze znanych) tylko trybut netto (+0,13 tys./d, dlug) i rzeczy gracza. Bez A7 i A8
zostaje ok. 2 tys./d, a bez rozszerzen A3 (A3', zaulki) ok. 9-11 tys./d. To ponad prog etapu (ok. 8,5 tys./d), a moze trafic w prog tylko dlatego,
ze ujscia przypadkiem znosza zrodla - wbrew zasadzie "nic z niczego".

### 5c. Dopisac do pomiaru E5/0 (tylko log)

1. **Okno "nowi notable BK"** na `HeroCreator.CreateNotable` (First + finalizer) - zeby drugie 10 000 nie trafialo do "inne".
2. **Okno "rabunek wsi"** na `RaidEventComponent.Update` (liczba juz jest w "Hearth (E4/0)", trzeba ja przeniesc do bilansu).
3. **Okno "perki handlu"** na `DestroyPartyAction.ApplyInternal` i `WorkshopsCampaignBehavior.OnSettlementOwnerChanged`.
4. **Pozycja Z6 "kiesa startowa nowej zalogi"** (postfiks `GarrisonPartyComponent.OnMobilePartySetOnCreation` albo nasluch `MobilePartyCreated`
   dla `IsGarrison`), wzorem "kapital nadany nowym karawanom".
5. **Migawki kies dla `ChangeHeroGold` bez zdarzenia**, kazda jako nazwana pozycja U albo Z:
   - Diplomacy `GoldCost.ApplyCost` (bez odbiorcy);
   - BK: `Demand` (kompromis finansowy), `BKCourtierBehavior.DrainGold` / `OnWeeklyTick`, `BuyGoodsAction.BuyGoods`,
     `BKGentryBehavior.CreateGentryClan` (poprawic tez falszywe "w nicosc - inne" przy majatku bez wlasciciela),
     `BanditHeroComponent.CreateParty` / `ConsiderLeaveHideout`, `Feast.Automanage`.
6. **"inne" rozbite** wedlug odbiorcy (glowa rodu / lord / notabl / wodz / gracz) i wedlug kwot okraglych (5 000, 10 000).
7. **Liczniki zamkniec bez licznika:** `RotBailoutPrefix` (zablokowane wywolania i kwota, ktora ROT by dal).
8. **Liczniki obciecia kasy:** G24 (SellItemsAction) i N3 (niedoplata kasy przy wyrobie BK) - patrz 5a.
9. **ROT podmiana:** mierzyc faktyczny spadek kiesy pana, a nie kwote zdarzenia (artefakt dodatniej reszty).
10. **N6:** sprawdzic, dokad idzie skarbiec upadlego krolestwa (728 d353).
11. **Ukryte `_partyTradeGold`** partii lorda z wodzem: suma kontrolna (poza bilansem), zeby wiedziec, ile wraca przy utracie wodza.
12. **NavalDLC Coastal Guard Edict** i perk Spring of Gold: czy czynne i ile (w "rody - przyczyny").
13. **Kosmetyka linii** (myli czytelnika, nie zmienia rachunku):
    - "zold oddany" przeniesc z "ZRODLA z niczego" do przelewow albo pokazac jako saldo z "rozliczeniami na minus";
    - Z4 pokazac netto wobec U7 i N5, N31 jako saldo pary z U31;
    - opis "Obieg notable: z niczego netto (zaulki, majatki BK, minus podatek warsztatow BK)" zmienic na "(zaulki)";
    - "rozliczenia rodow na plus" uzupelnic brutto tierem;
    - poprawic log CrownCallToWar: `_dCancelled` jest zerowany przed wypisaniem, wiec "skreslone przez gre" zawsze pokazuje 0.

---

## 6. Pliki robocze (scratchpad sesji, `...\scratchpad\`)

- **notatki:** `audyt-zloto\ksiega.md` (definicja sumy, pozycje, skrzynki), `audyt-zloto\logi.md` (srednie wszystkich pozycji, trzy logi);
- **skrypty ksiegi i logow:** `audyt-zloto\tools\` (ksiega_parse.py, ksiega_to.py), `audyt-zloto\logi\` (ksiega_srednie.py, notable_dubel.py,
  zdarzenia_r.py, r_wobec_pozycji.py ...), `audyt-zloto-gra-tools\p1-p5.py`, `audyt-zloto-skrypty\przyczyny.py`, `nap_reszta.py`,
  `audyt-zloto-mine\`, `audyt-zloto\bk-tools\`, `krytyk\` (rdays.py, quiet.py, nap728.py, inni728.txt);
- **weryfikacje sceptyka:** `audyt-zloto\sceptyk-*\`, `sceptyk-*\`;
- **dekompilacje:** gra `dec32\cs_full\`, BK `e4tools\bkfull\`, reszta modow `audyt-zloto\<mod>\` (ROT, BKROTPatch, RBMTournament, Diplomacy,
  BetterEconomy, NavalDLC, NavalVMC, SandBox, StoryMode ...), IL `audyt-zloto\ROT.il`.
