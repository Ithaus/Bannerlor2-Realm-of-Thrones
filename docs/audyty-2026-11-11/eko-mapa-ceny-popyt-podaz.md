# MAPA: ceny - popyt - podaz (audyt, tylko czytanie)

Dowod z logow: Armoury-2026-11-09_16-36-21 / 17-37-47 / 21-17-29 i Armoury-2026-11-11_13-19-13 (Modules/Armoury), doby 109726-109814
(88 dob, kampania od 108648, czyli doby ok. 1078-1166; CALY okres to zima - "doba zimy 637, do konca 1159 dni"). Lata w logach nie ma.

## 1. Jak powstaje cena (lancuch czynny wedlug logu)

Czynne modele (log linia "Przeplywy osad: modele czynne" i "Ceny surowcow"): cena = BKROTPriceModel : DefaultTradeItemPriceFactorModel
(decout/full/bkrot.cs:12646 - nadpisuje tylko GetTradePenalty), ekonomia osad = DefaultSettlementEconomyModel. Modele BetterEconomy
(beco.cs:4963 BEE_ItemPriceFactorModel.GetPrice - odchylenie regionalne, sezonowe jedzenia, plynnosc kupcow; beco.cs:5708
BEE_SettlementEconomyModel.ConsumptionMult - zima) i BKEconomyModel.GetDailyDemandForCategory (bk.cs:39165, dzieli przez DaysInYear)
NIE sa czynnym modelem - ich reguly sezonowe/regionalne nie dzialaja; latki Armoury sa na nich wpiete "na zapas".

Wzor gry (cs_full.cs:71806 GetBasePriceFactor): indeks = (D / (0.1*S + 0.04*I + 2))^0.6 (zwierzeta ^0.3), towar handlowy 0.1-10,
reszta 0.8-1.3. D i S wygladzane 15%/d (cs_full.cs:67525 GetSupplyDemandForCategory; ItemConsumptionBehavior.UpdateSupplyAndDemand ok. :195013).

Nasze poprawki na tym wzorze:
- RawPrice.cs:197 FactorPrefix - (a) stala "+2" w nowej monecie (x przelicznik kategorii, tylko gdy towar potanial), (b) wagi 299/299c:
  swieza sztuka wazy od razu 0.0084, pamiec podazy 0.1316 (razem 0.14 jak gra), tempo 0.15/d (Settings.cs:1415, :1417);
  RawPrice.cs:324 UseDemand - dla 7 surowcow masowych popyt = prawdziwe zuzycie (mieszczanie x TownUse + rzemioslo + odbior wojska/wywoz).
- PriceBand306.cs:157 BandPostfix - towar handlowy nie-jedzenie w pasmie [1/k, k], k = min(10,(1+marza)x(1+przewoz/cena kg));
  w logu marza 2.00, droga 2000 km (Settings.cs:5485-5486): aksamit/bizuteria/bawelna 0.33-3.0, welna 0.27-3.67, ruda 0.19-5.21,
  sol/drewno/glina/kamien pasmo gry 0.1-10. Jedzenie i zwierzeta BEZ pasma.
- HistoricalPrices.cs:1083 DemandPostfix - popyt / przelicznik monety, x m_t (PeopleMarket: ludzie rynku), a dla koszyka ZC4
  popyt = potrzeba miasta i wsi kupujacych w nim x wartosc (StartCalibration.Zc4Demand).
- Sprzet (bron, zbroje, konie): osobne prawo polki SupplyDemand.cs:202-225 (popyt = 4 szt. x dobrobyt/3000 [0.3-3] x waga tieru
  1.0..0.15 + zamowienia niezaspokojone) i :363-374 mnoznik ((D+1)/(S+1))^0.5 w 0.25-4 (Settings.cs:2114-2126); wywoz 15% nadwyzki/d
  (SupplyDemand.cs:686 DailyTrade).

Wniosek: cena jest funkcja ZAPASU NA POLCE w dobach popytu (zgodnie z zasada "cena od polki, nie od odleglosci"); odleglosc wchodzi
tylko jako szerokosc pasma 306a. Zapas 7 dob popytu = wartosc bazowa; 10 dob x0.82; 20 dob x0.54.

## 2. Popyt: kto kupuje i za co

- Slownik budzetu dnia: ItemConsumptionBehavior.UpdateDemandShift (cs_full.cs ok. :194988) z GetDailyDemandForCategory
  (BaseDemand x dobrobyt + LuxuryDemand x (dobrobyt-3000), cs_full.cs ok. :67576) x nasze skale.
- Konsumpcja: BK ItemConsumptionPatch.Prefix (bk.cs:30601) + CalculateBudget (bk.cs:31185): budzet = popyt x indeks^0.15
  (0.10 wysoki / 0.20 niski podatek) + dobrobyt/1000/indeks; sztuk = budzet/cena -> elastycznosc ilosci ok. -0.85 (wydatek prawie staly).
  Przy Food <= 10% BK dokupuje jedzenie bez wzgledu na budzet.
- Zloto: BK robi town.ChangeGold z niczego; TownFolk.Live.cs (PayForGoods, ok. :585-640) zamienia to na zaplate z kies W/R/P
  (najwyzej stan kiesy, akcyza 2% Settings.cs:2908), reszta przyrostu kasy jest kasowana. Kotwica TownFolk.Live.cs:541
  AnchorBudgetPostfix tnie/podnosi czlon dobrobytu (czynnik jedzenie/reszta).
- Jedzenie = potrzeba (koszyk stanow ZC4, kcal), nie budzet: drabina "najtansze kcal najpierw" (TownFolk.Ks.cs naglowek), zamienniki
  (FoodSubstitute.cs, udzial 0.5), glod = brak kcal na polce, nie brak pieniedzy (KsHunger.cs).
- Zbytki: TownFolk.Luxury.cs - 1/60 kiesy ponad zapas dni, wagi sukno 55% / srebro-klejnoty-korzenie 25% / lepszy stol 12% / dom 8%,
  cena graniczna 1.5 (Settings.cs:3090; do 8.0 dla filcu, plotna, skory) - powyzej lud nie kupuje.
- Wojsko: zywnosc partii przez model gry (Rations.cs -40%, zima +20% Settings.cs:966); lordowie przy cenie > 2.5 x wartosci biora tansze
  (FoodThrift.cs, linia "Lordowie a drogie jedzenie (B3)").

Liczby doby 109814 (linia "Kiesa ludu"): koszyk ludu 1 080 080 zl/d, wydatek gry 344 799 (jedzenie 195 290), zaplacone przez lud
331 126, zjedzone bez zaplaty 11 643 (3.10%); kiesy 9.26 mln, zapas dni W/R/P 15.3/16.5/64.2.
Udzial odbiorcow w zuzyciu zboza (16 134 szt./d): mieszczanie 5 615, wsie kupujace w miescie 3 869, przetworstwo 3 550, partie 1 916, zamki 1 181.

## 3. Podaz i co wyrownuje ceny miedzy regionami

Wyrownuja: tabory wsi ("wozy do osad"), karawany (490 zmierzonych), kupcy zywnosci KS-H (KsFoodCore.cs: lot tylko gdy indeks celu
>= 1.03 x koszt i zysk <= 97% ceny celu), zboze ze spichlerzy wsi 322c/324/325 (13 101 szt./d z 249 wsi do 56 miast, komis i rachunek wsi),
wywoz sprzetu SupplyDemand.DailyTrade, regulator wsi HarvestValve.cs (wies robi tyle, ile swiat zuzywa).
Sezon: WinterBite.cs / Settings.cs:967 (plon wsi -50% zima), HarvestYear.cs (rok x0.8-1.2, 1/20 rok glodu x0.6), Famine.cs
(spichlerz 2 lata, plon 1.2 x potrzeby, psucie 6%/rok), zima wieloletnia (Settings.cs:1740-1749: 2-6 lat).

## 4. Rzeczywiste rozrzuty z logow

Indeks cen jedzenia, mediana miast ("Zamienniki jedzenia (B2)"), doby 109727 -> 109815:
apple 5.75 -> 4.94, honey 6.57 -> 5.60, Eggs 6.29 -> 5.48, cheese 4.44 -> 4.62, butter 4.03 -> 3.89, meat 3.71 -> 3.59, fish 3.57 -> 3.77,
bread 3.42 -> 3.31, pie 2.68 -> 2.75, grain 1.71 -> 1.76. Stoi w miejscu przez 88 dob (bez trendu). CHANGELOG.md:10306 (wczesniej
w kampanii): mediana 1.34-1.5 - czyli zima podniosla jedzenie nietrwale 2.5-4 x, zboze o ok. 20-30%.
Utarg wozow wsi (doba 109813): ryby 5.26 x wartosci, jajka 4.62, maslo 3.53, ser 3.58, chleb 3.17, zboze 1.68, sol 2.08.

Surowce ("Ceny surowcow", 109809; min/mediana/max indeksu miast): ruda 0.19/0.64/2.75 (31 miast z nadwyzka, 6 bez towaru),
drewno 0.10/0.78/2.04, skory 0.12/1.11/5.56, skora 0.67/1.21/4.40, len 0.17/1.50/5.80 (57 miast bez lnu), plotno 0.46/0.97/3.56,
welna 0.27/1.66/3.67. Przez 12 pomiarow mediany w waskim korytarzu (ruda 0.58-0.74, drewno 0.73-0.78, len 1.45-1.75).

Pasmo 306a (109810): 2619 par miasto x kategoria, 832 na suficie pasma (31.8%), 127 na podlodze (4.8%); przez 12 pomiarow sufit
832-898, podloga 116-154 - stale. Mediana miast NA SUFICIE: aksamit 3.01, bawelna 3.06, tran 5.33; rozpietosc max/min po pasmie:
len x33.6, tran x28.4, wino x21.3, welna x13.5, filc x10.8, bawelna x9.3, aksamit x8.6 (identyczne w 109789).
Bawelna: 3 044 szt. na polkach miast, zuzycie 70/d (58 dob zapasu swiata), a karawany przewoza 28-34 szt./d.

Zapasy swiata 109727 -> 109814 (ksiega "Towary"): zboze 267 410 -> 268 967 (+0.6%), chleb +11.7%, mieso +25%, ryby -6.9%, piwo +12%,
sol -12%, drewno +7.6%, ruda zelaza +16.3% (68 dob zuzycia), plotno +8%, welna +5%, narzedzia +11%, aksamit +3%;
odstaja: glina +65%, miod +79%, srebro -46%, ruda zlota -88%, wegiel drzewny 62 053 szt. przy zuzyciu 11/d (5 641 dob - martwy zapas).
"Bez wyjasnienia" +0 we wszystkich wierszach doby. Zapas jedzenia swiata na polkach/w jukach to 4-11 dob zuzycia (zboze 17);
prawdziwy bufor to spichlerze wsi: 411 dni potrzeby, plon zimowy 86.4% potrzeby.
Zapas unieruchomiony w zamkach: miod 1 207 z 1 627 szt. (74%), maslo 1 228 z 3 271, placek 1 019 z 3 371, daktyle 511 z 1 637.

Rynek od ludzi (GL2): m_t miast min/med/max 0.09/0.74/5.79; budzet dnia swiata 1.86 -> 2.02 mln; zapas kupcow 11.44 -> 11.57 mln;
miast z kasa ponizej zapasu 40-49 z 97 (dzis 46, w tym 27 ponizej 1/4) - stale przez 88 dob. Srednio te miasta traca 42.6 tys./d
(tabory wsi -148 tys., karawany netto -92 tys., nasz tick -79 tys., zakupy ludu +202 tys.).
Kotwica: m srednio 0.88-0.92, czynnik jedzenie/reszta 0.85/0.73-0.82, miast z czynnikiem < 1: 49-64.
Stac na jedzenie W/R/P 87.2/78.2/87.7%, pelny koszyk swiata 72-83% (cel projektu m96: 95% / 98%), miast < 95%: 11-20.
ZC4: jedzenie niekupione z braku pieniedzy 21.5-31.1% budzetu jedzenia miast; wsie: kcal bez pokrycia 5-17%.
Glod miast (KS): mediana braku 0%, q90 0-79%, 3-12 miast glodnych (Dragonstone 94.7%, Lorath, Braavos, Tolero Aeksio - wyspy/importerzy).
Zgony z glodu: wsie 0, miasta 338 od poczatku kampanii.
Karawany: 0.47-0.55 zarabia na zold, mediana wyniku 64-120 zl/d przy zoldzie ok. 98.
Pieniadz: swiat 224.46 -> 225.20 mln (+0.33%/80 dob); lud 9.4 -> 9.1%, lordowie 35.5 -> 37.4%, korony 28.0 -> 27.4%.
Rachunki wsi za zboze (dlug kas miast): 6.58 -> 7.43 mln zl (+13%/88 dob, 26-35 miast).

## 5. Ocena: rownowaga czy dryf

Rownowazy sie: poziomy cen, zapasy wiekszosci towarow, liczba miast pod kreska, pokrycie koszyka, wynik karawan - wszystko stoi
w korytarzu przez 88 dob; ksiega towarow zamknieta. Rynek reaguje na zime wlasciwym kierunkiem i skala (zboze +20-30%, nietrwale x2.5-4).
Dryfuje powoli: (1) dlug miast wobec wsi +9.7 tys./d, (2) udzial lordow w pieniadzu +1.9 pkt / 80 dob kosztem ludu i koron,
(3) kiesy ludu 9.83 -> 9.26 mln, (4) zapasy bez odbiorcy (wegiel, ruda zelaza, glina, miod w zamkach), (5) srebro i ruda zlota topnieja.
Nie dziala prawo jednej ceny dla drogich towarow nie-jedzenia: 1/3 par na suficie pasma, rozpietosci x9-x34 trwale; karawany wioza za malo.
Cel "95% ludzi stac na jedzenie" nie jest spelniony zima (78-91%).

## 6. Zgodnosc ze sredniowieczem

Zgodne: cena z miejscowego zapasu, zboze sztywne i tanie w ruchu dzieki spichlerzom (wzrost x1.2-1.8 zima; 1315-17 pszenica x2-3),
nietrwale drozsze i miejscowe, zbytki dostepne w kilku miastach, biedni schodza na ziarno, glod = brak towaru, miasta-importerzy
zadluzone u wsi, kredyt/komis/zadatek kupiecki.
Niezgodne / watpliwe: jajka, jablka, miod x5 przez setki dob (zima wieloletnia to fantastyka, ale poziom wynika z polki 2-4 doby
wobec kotwicy 7 dob); 22-31% budzetu jedzenia miast niekupione z braku pieniedzy jako stan trwaly; brak sezonowosci cen z modeli
BetterEconomy (nieczynne); zapasy miast 4-11 dob (historycznie miasta trzymaly tygodnie-miesiace zboza) - bufor jest po stronie wsi.

## 7. Czego nie udalo sie ustalic

- Cen latem i przejscia lato -> zima (logi obejmuja tylko zime; jedyny slad to CHANGELOG.md:10306).
- Cen w konkretnym oblezonym / spladrowanym miescie: logi nie maja wiersza cen per miasto; Riverrun oblega gracz (linia "Mury oblezen"),
  ale indeksu jego polki w logu nie ma. Mechanizm znany tylko z kodu (gra zdejmuje jedzenie z polki do Food, wies spladrowana produkuje 0).
- Dlaczego "koszyk ludu" 1.08 mln/d jest 3 x wiekszy niz zaplacone 331 tys. (rozne definicje koszyka w liniach "Kiesa ludu" i ZC4).
- Czy BKEconomyModel na pewno nie jest w lancuchu popytu (opieram sie na linii logu o czynnym modelu, nie na przejsciu po Models).
- Dokladnych numerow linii PayForGoods i metod gry (podane "ok.").
- Nie czytalem w calosci: KsFoodCarts, CaravanTrade284, HarvestValve, TownGranary.Zima322, SiegeMisery (tylko naglowki).
