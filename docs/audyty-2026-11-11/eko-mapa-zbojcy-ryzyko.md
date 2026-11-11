# MAPA: zbojcy i ryzyko (audyt tylko-odczyt, 11.11)

Zrodla: kod `n367c/Armoury/src` (OutlawLaw.cs, BandFood.cs, BeatenPurse.cs, HorseFate.cs, BanditLimit.cs, CaravanGuards285*.cs, NoticeBoard.Watch.cs,
Gallows.cs, RaidTribute.cs, ReaverLaw.cs, FreeFolkRaids.cs, Devastation/DistrictRaid, ScaNoBandits320.cs, Settings.cs), dekompilacja gry
(`decout/cs_full.cs`), log gry `Modules/Armoury/Armoury-2026-11-11_13-19-13.log` (doby 109804-109815, tj. ok. 3.2 roku kampanii przy roku 364 dni;
start kampanii d108649) oraz logi 11-11_12-35 i 11-09_23-53 (doby 109734-109740).

## 1. Skad sa bandy - z ksiegi ludzi, nie z niczego (POTWIERDZONE kodem i kontrola w logu)

Pula wyrzutkow na region (miasto/zamek + wsie): `OutlawLaw._pool` (OutlawLaw.cs:74). Doplywy:
- dezerterzy: `OutlawLaw.OnTroopsDeserted` (:668) - zostaja w swoim typie/sprzecie; progi dezercji wedlug tieru `DesertionLaw` (Settings.cs:461-467: tier 1 ponizej morale 25, krok 3, 1%/pkt, max 25%/d);
- rozbici z bitew: `RoutedH3` (:914), udzial `OutlawRoutedShare 0.5` (Settings.cs:1492), reszta do domu (BK / wies);
- spalone wsie: `Devastation.Strike -> AddFled` (:1042): 2% trafionych + do 3% przy zagrozeniu (Settings.cs:1555-1556);
- bieda/wojna: `Daily` (:1215-1241): naplyw = hearth/1000 x 0.1 x nedza; nedza = (1-dobrobyt/5000) + (1-bezp/100) x 0.5 + wojna 0.5 + spalone wsie x2 + glod 1.5 (`MiseryOf` :1172-1198; Settings.cs:1485-1489) - ludzie ZDEJMOWANI z hearth wsi (`TakeCommoners` :375);
- rozwiazane bandy (`OnPartyDestroyed` :1049), odchodzacy z glodu z band (BandFood).
Odplywy: powrot do domu (prosci: 0.5%/d + 2%/d x dobrobyt w pokoju, x0.25 w glodzie, :1245; zolnierze: 1/30 d w pokoju, 1/120 d w wojnie, Settings.cs:1533-1534),
narodziny band (`RosterPostfix` :1574 podmienia sklad szablonu na ludzi z puli; `Draw` :620 z regionu + 4 sasiednich), werbunek band 2/d z puli regionu i 10%/d jencow (:1339-1385).
Bramki "nie ma ludzi = nie ma bandy": `GateClan` (:1604, >= 6 ludzi przy kryjowce klanu), `GateHideout` (>= 12), `GateGlobal` (>= 18 w swiecie; looterzy, piraci, bohater BK).
Wighty poza pula. Zloto/jedzenie z niczego zamkniete: `OutlawNoFreeGold`, `OutlawNoFreeFood`, `OutlawNoHideoutGold` (Settings.cs:1710-1721).

Log d109815 ("Pula wyrzutkow (E4/3)"): stan 5648, naplyw +163.5 = dezercja 101 + rozbici 24 + spalone wsie 9.7 + bieda/wojna 10.8 + rozwiazane bandy 18;
odplyw -176.1 = do domu 85.9 + narodziny band 67 + werbunek 24; kontrola 0.00; sredni pobyt w puli 32 doby.
12 dob: pula 5091 -> 5647 (+11%); dezercja 101-186/d, rozbitkowie 24-84/d, bieda/wojna stale 10.3-10.8/d, rabunki 3-30/d.

## 2. Ile ich jest i gdzie

d109815 ("Bandy i limit (f8-bandy)", "Paser"): 563 bandy, 10 995 ludzi (19.5 na bande), sredni tier 2.17; partii swiata 4781 (bandy 11.8%).
Klany (band/ludzi): northern_pirates 32/1974, wildlings 84/1398, looters 135/1385, southern_pirates 22/1241, forest_bandits 56/1071, sea_raiders 42/921,
mountain 37/696, steppe 37/609, kingswood_outlaw 36/594, desert 29/426, robber_knights 22/315, hilltribesmen 14/236, yiti 17/129. Na morzu 54 bandy.
Kryjowki: 266 w swiecie, zajete 22; cel gry = zajete kryjowki x 12 (ROTBanditDensityModel; `BanditLimitPerHideout 12`, Settings.cs:4801) = 264, band klanow 374 (1.42 celu).
Sklad puli: zolnierze 4758, bandyci 227, prosci 627, straz karawan 34 - 84% puli to byli zolnierze.
Punkty odniesienia z CHANGELOG: 28.10 (przed 285/286) 818 band / 16 590 ludzi; bieg roczny (STAN-PRAC:1994) pula 11 869 w d364, bandy 819 / 17 279.
GDZIE: tam, gdzie ROT ma kryjowki i gdzie gra rodzi looterow - nie tam, gdzie nedza. Pula jest regionalna, ale bande rodzi szablon gry w kryjowce klanu.

## 3. Czym zyja i dokad idzie lup (zamkniete, z jednym wyjatkiem)

- JEDZENIE (BandFood.cs, m96-bandy; 1 szt./20 ludzi, zapas 5 dni): d109815 potrzeba 552 szt./d: zabrane wsiom 298 szt. (1970 zl; 273 bandy, 120 wsi; najwyzej polowa zywnosci wsi na dobe,
  promien 100, wies nic nie dostaje), kupione u pasera 60 szt. za 801 zl, POLOW ZALOGI 156 szt./d (piraci na morzu - nazwane zrodlo z niczego, zjadane od razu, limit 24/d na bande).
  Glodne 64 bandy, odeszlo z glodu 34 ludzi (do puli), 1 rozpad. Glodna banda nie werbuje.
- ZLOTO: kiesy rozbitych karawan/taborow/band w calosci dla zwyciezcy (`BeatenPurse` 3.2, Settings.cs:399). "Kiesy rozbitych (3.2)" do band: 0 / 1570 / 1920 / 2510 / 3304 / 5078 / 6570 / 7444 / 14 517 / 28 952 zl
  w roznych dobach - bardzo nierowno. Kiesy band razem 30 290 zl (mediana 5 zl; 217 z 563 ma cokolwiek).
- TOWAR: lup wedlug wkladu, ponad udzwig na polke najblizszej osady (`HorseFate.Spoils` :952). Paser (`FenceSell` OutlawLaw.cs:2459): banda dostaje 50% ceny skupu przy miescie -> 25% na granicy promienia 200,
  reszta zostaje w kasie miasta, towar WRACA na polke miasta. d109815: 5 band z ladunkiem, sprzedane 32 szt. za 609 zl (wartosc 1556). W jukach band lezy 2139 szt. (30 854 zl) + 312 koni.
- WYDATKI: zycie w miastach 10% kiesy/d (bandy 2621 zl + kryjowki 1630 zl -> kasy miast), dola 25% do kryjowki (723 zl/d), sprzet u pasera (marza 1.5-2.0; d109815: 0 awansow, 37 "bez sprzetu").
- KRYJOWKI: 46 397 zl w 47 kryjowkach (skarbiec 150 + 120 x banda dla zdobywcy; `HideoutPurge`), bitew AI w kryjowkach 0.
Skala: obrot band ok. 5-10 tys. zl/d wobec sprzedazy karawan 328 tys. zl/d ("Karawany - wynik handlowy") - 2-3%.

## 4. Wplyw na handel i ceny

- Rozbite karawany: 0.33/d (4 w 12 dobach, 503 karawany), w oknie 109735-109740: 4 w 6 dob. Historia: 2.23/d (133a) -> 3.83/d (285) -> ok. 0.3-0.7/d teraz (286).
- Jedyny kanal kosztu ryzyka: straz karawan wedlug sily najsilniejszej bandy na drodze (`CaravanGuards285.Road`, 286). d109815: zagrozenie drogi srednio 30.6, sila karawany 44.1,
  36 z 139 wyjazdow slabszych niz banda drogi; 15 011 straznikow, zold 54 810 zl/d (po -50% z 288) wobec wyniku handlu +125 657 zl/d.
- Tabory wsi: glowna ofiara (d109815: 71 ludzi taborow pobitych, 94% rozbitych, 5.6% zabitych).
- CENY: brak premii za ryzyko. `CaravanRoute294c`, `CaravanTrade284`, `CaravanMarket294`, `MarketCarts` nie czytaja band (0 trafien grep). AI bandy napada wedlug przewagi sily, nie ladunku.
  Zrabowany towar wraca na polki przez pasera, wiec podaz prawie nie spada - rabunek to przelew, nie strata.

## 5. Reakcja prawa

- Szubienice (`Gallows.cs`): CZYSTY WYGLAD - nic nie zmieniaja w puli, bezpieczenstwie ani ekonomii (14 aktywnych / 200).
- Listy goncze (`NoticeBoard.Watch.cs`): nagroda N x 3 dni x zold zalogi x 1.25 (+do 100% za szkody), z wolnej kasy miasta / od pana; bezp. +2. d109815: czynne 42 w 30 miastach, wyplacone 0.
- Patrole BK: 8 twierdz, 25 ludzi ("Patrole BK (364)") - sladowe. Strategiczne AI celowo nie liczy band jako wroga (`ScaNoBandits320`) - lordowie bija bandy tylko przy okazji.
- Bezpieczenstwo (gra, cs_full.cs:68563): kryjowka w poblizu -2, spalona wies -2, zaloga +-3, dryf do 50. Liczba band w polu nie wchodzi.
- Pobity bandyta: zabici 26.9%, jency 0%, rozbici 73.1% (H3) - polowa rozbitych wraca do puli (`OutlawBandRoutedShare 0.5`). Zlapani: "przestepcy bez Muru (do domu) 0", na Mur 2.
Wniosek: pokonanie bandy prawie nie zmniejsza liczby wyrzutkow; kara nie odstrasza (brak sprzezenia "wiecej ladu -> mniej chetnych").

## 6. Najezdzcy i rabunek wsi

- Rabunek wsi robia lordowie, nie bandy. E7/R1 (`DistrictRaid`) + `Devastation` (Settings.cs:1549-1561): 0.5 czlowieka na najezdzce na dobe; trafieni d109814: 283.9 = zabici 7.1 + las 9.7 + uchodzcy 267.2.
  Konto uchodzcow 103 641 w 123 wsiach (z 41.8 mln), plon wsi x (ludzie w domu / ludzie) - najnizszy 0.2925 (Mallery), Duskendale 47% uchodzcow; powrot po 30 dobach spokoju.
- Okup okolicy (`RaidTribute`, patis): 10-20% kiesy wsi, 30 dob ochrony; d109804: okupy 0, opcje rabunku AI wyciete 103, ochron 11.
- Zelazne Wyspy (`ReaverLaw` WW1-WW4): co najmniej 50% kiesy i magazynu wsi, 10% nadwyzki nabrzeza; 12 dob: 0-2 kroki/d, zloto z kies wsi 0-6226 zl/d (razem ok. 14 100), thralle 0-50/d (126),
  nabrzeze 1286 raz. Niewolni w ksiedze 28 940; sprzedani d109815: 94. Mnoznik zysku WW3 mediana 1.02; rabunek na gorze 24 z 197 mysli.
- Wolni Ludzie (`FreeFolkRaids`, r4-mur): od poczatku 23 bandy / 490 ludzi (16 z puli, 474 z domow), doszlo 6, wrocila 1 (20 ludzi), zginely 22 (Straz 14), lup do wsi 0 zl. Indeks glodu teraz 0.000 -> 0 nowych.
  Lordowie WL: rabunek na gorze 0 z 89 mysli.
- Korsarze (WW4): sprzedaz w portach Wysp 0 szt. w badanych dobach.

## 7. Sprzezenie czy stale tlo? (ocena)

JEST: wojna -> glod druzyn -> dezercja ("Dezercja AI (169c)": 48 z 67 zdarzen to morale w glodzie) i rozbici -> pula -> bandy; w pokoju powrot 4x szybszy (30 vs 120 dob); bandy gloduja i sie rozpadaja;
straz karawan rosnie z sila band; spalona wies -> bezp. -2 -> nedza -> wiecej wyrzutkow.
SLABE / BRAK:
1. Kanal BIEDY jest ilosciowo pomijalny: 10.3-10.8 ludzi/d na swiat (6% naplywu), prawie staly. Nedza czyta dobrobyt i bezpieczenstwo MIASTA z gry, nie czyta nowej ekonomii (kiesa ludu, glod wsi, podatki, zalegle place).
2. Liczbe band trzyma cel gry (zajete kryjowki x 12 + looterzy), nie pula: 5.6 tys. ludzi czeka srednio 32 doby i w polowie wraca do domu. Polozenie band = kryjowki ROT, nie regiony nedzy.
3. Prawo nie dziala wstecz na przestepczosc: szubienice kosmetyczne, listy goncze rzadko wyplacane, lordowie nie poluja, 73% pobitych band ucieka.
4. Ryzyko nie jest w cenach ani w trasach - tylko w zoldzie strazy. Rabunek karawan (0.3-0.7/d) prawie nie rusza podazy.
5. Bandy nie rabuja wsi z pieniedzy ani ludzi, nie biora okupu - tylko zywnosc z magazynu.
6. Polow zalogi piratow: 156 szt./d zywnosci z niczego (jawne, nazwane w ksiedze).
Do sredniowiecza: zrodlo band (zolnierze bez zoldu, rozbitkowie, routiers/ecorcheurs), patis, paser, powrot do domu w pokoju - zgodne. Niezgodne: brak zwiazku z glodem/nieurodzajem wsi,
brak odstraszania prawem, brak premii za ryzyko w cenach dalekiego handlu, bandy rownomiernie "z kryjowek" zamiast przy szlakach i w strefach wojny.

## 8. Czego nie ustalono

- Biegu wielomiesiecznego na obecnym kodzie nie mam: tylko 12 + 6 dob; trend roczny (plateau puli, reakcja na koniec wojny) niepotwierdzony logiem.
- Udzial kradziezy zywnosci (298 szt./d) w produkcji wsi - nie zestawilem z plonem swiata.
- Co bezpieczenstwo miasta robi dalej w BK (podatki, produkcja, lojalnosc) - nie czytalem bk.cs; BKBanditBehavior (herszci) tylko z komentarzy naszych latek.
- Czy 135 band looterow rodzi sie przy wsiach regionow z pula, czy dostaje ludzi z odleglych regionow (galaz `region == null` w RosterPostfix :1588) - nie zmierzone.
- Suma wyplat listow gonczych i liczba band rozbitych przez lordow w dluzszym oknie - nie liczona.
- Mod BetterEconomy pod katem band - nie sprawdzany.
