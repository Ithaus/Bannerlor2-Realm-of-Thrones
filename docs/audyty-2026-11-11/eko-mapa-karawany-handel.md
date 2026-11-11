# MAPA: karawany i handel (audyt, tylko odczyt)

Zrodla: kod `n367c/Armoury/src` (naglowki klas + sygnatury), dekompilacje `decout/full/bk.cs`, `decout/cs_full.cs`, logi `Armoury-2026-11-11_13-19-13.log` (doby 109803-109815, zima Westeros) i `Armoury-2026-11-09_21-17-29.log` (52 doby, 109752-109803). Rok = 364 dni. Liczby "na dobe" z linii dobowych; gdzie srednia z 51 dob - zaznaczone.

## 1. Kto wozi towar (4 kanaly, liczby z doby 109814)

| Kanal | Wolumen | Zloto | Kod |
|---|---|---|---|
| Wozy wsi (prawdziwe partie) | 15 952 szt./d, 101 sprzedazy | utarg 447 tys./d (sr. 51 dob: 306 tys.) | gra `VillagerCampaignBehavior` + `MarketCarts.cs:476 Choose` |
| Karawany (503: notabli 369, rodow 126, konwoje DTE 8) | 2 292 szt./d (sr. 14 dob) | utarg 392 tys./d (sr. 328 tys.) | BK `BKCaravansBehavior` (bk.cs:89622) + nasze latki |
| Hurt kupcow wozem bez partii (bron, konie; 215) | 2 098 szt./d, 1 872 wozy w drodze | 132 tys./d | `TradeCarts.cs` |
| Kupcy zywnosci KS-H (bez partii) | wg niedoboru miasta | kasa celu -> kasa zrodla + fracht | `KsFoodCarts.cs` |

Popyt rynkow 25 946 szt./d ("Handel z roznicy cen (287)"), karawany pokrywaja 8.8% sztuk. Wolumen niosa wozy wsi; karawany niosa wartosc (aksamit, bawelna, futra, wino).

## 2. Karawana: wybor trasy i towaru

**Cel (BK, bk.cs:90703 `FindNextDestinationForCaravan`, :90775 `GetTradeScoreForTown`)**: ocena = (sprzedaz + kupno) x 1/(droga + kara dalekiej drogi + 8) x TradePower miasta x premia domu; odrzucone: miasto ostatnio odwiedzone, oblezone, **w wojnie z frakcja karawany** (binarnie), morskie tylko porty. Sprzedaz (:90822) = (indeks miasta - srednia x limit) x min(4000, wartosc x sztuki); kupno (:90858) = (srednia/indeks)^2 - 1.1. **W ocenie BK nie ma: ryzyka band, myta, pory roku, kosztu zoldu, kasy miasta, konkurencji.** Gra `CaravansCampaignBehavior` jest wylaczona przez BK (zostaje sprzedaz przy wjezdzie i odradzanie).

**Nasze poprawki celu:**
- 139 C `CaravanWait139.cs:581 Route` (klucz CaravanRouteRoadCost TAK): karawana z ladunkiem wybiera max (zysk sprzedazy - koszt dnia x dni)/dni wsrod miast nie dalszych niz wybor BK. Log: 245 decyzji z towarem, zmienione 18 (7%), dni drogi 3.85 -> 2.56, zysk 570 -> 2186 zl.
- 294c D1/D2 (`CaravanRoute294c.cs`; cel wg tego, co naprawde sprzeda / kupi, z towarem innych karawan w drodze) - **WYLACZONE** (Settings.cs:4717-4718 = false; log "cel wedlug sprzedazy NIE, ocena kupna wedlug regul NIE").
- Morze 308a/307a (`CaravanSea308.cs`, `CaravanSeaHold307.cs`): prawdziwa droga morska NavalDLC, cel trzymany do doplyniecia (limit 3.0 x, 6-30 dob). Log: 32 decyzje morskie, rejs sr. 2.8 doby, max 17.6 -> po regule max 7.6.
- Wyspy: karawana bez statkow bez drogi ladowej rozwiazywana, lord nie kupuje (`CaravanFlow.cs`, IslandCaravanFix). Stan 0.
- Drogi: +15% predkosci na traktach (`MapRoads.cs`, Settings.cs:1010), AI szuka traktu (`AiRoadRoutes.cs`); karawany 13% partio-godzin na drodze, objazdow 5.5% planow. Bloto -15%/-7.5% poza droga (`MapMud.cs`), zima: sztormy x2 i statek -15% w burzy (`SeaStorms.cs`).

**Co kupuje (BK :91016 BuyGoods - 5 kategorii z najwyzszym CalculateBuyValue, limit 1500 zl/kategorie) + nasze bramki `CaravanTrade284.cs`**: przesyt (juki > 10 dob wlasnej sprzedazy, jedzenie 7), kapital obrotowy, rezerwa zoldu 3 dni, zysk osiagalny po mycie i drodze, glebokosc rynku 294b. Odrzucenia doby: przesyt 363, kapital w towarze 453, rezerwa zoldu 83, bez zysku 416 (na 151 wizyt). Surowce masowe (ruda, drewno, skory, len, plotno, welna) wg braku miast: `CaravanBulk.cs` (zapas 10 dob, nadwyzka x2, Settings.cs:1318-1324). Amunicja zakazana (`CaravanAmmo.cs`). "Gabka" BK przy wyjezdzie (wszystko < 33% wartosci) cofana / ksiegowana.

**Co sprzedaje**: T1 `CaravanGoods.cs:1034 Sell` - tylko gdy cena po mycie > koszt zakupu (ksiega kosztu w zapisie); na stracie tylko: psujace sie > 7 dob (1.5%/d), > 30 dob w jukach, brak na zold, przeladowanie. Doba: z zyskiem 635 szt. za 60 282 (koszt 37 920, marza +59%), na stracie 174 szt. (strata 1 444). Zysk zrealizowany wszystkimi drogami 143 202 zl/d.

**Kiedy nie jedzie**: w oblezonym miescie (Hold), ranni > 40%, BK w miescie rusza z szansa 1/3 na tik (nasz E8/G1 otwiera tik co godzine: postoj mediana 1.4 h, p90 5.6 h). Bez sprzedazy w 7 dobach 113 z 503 (71 "czeka na rynek z zyskiem", 30 pusta kiesa).

## 3. Ile zarabia i komu

- **Wynik handlu swiata** (f4-pomiary, 7 okien po 7 dob): +109..150 tys./d; zold zaplacony 38-39 tys./d (notable) + 11 tys. (rody). Zold eskorty **x0.5** dla calego swiata (`MountedWage.cs:190 CaravanHalf`, Settings.cs:4667): 110 070 -> 54 904 zl/d, 3.65 zl/czlowieka.
- **Rozklad**: placi na swoj zold 244 z 490 (**0.50**; w 52 dobach 0.52-0.56), mediana (wynik - zold) -2..+25 zl/d, p10 -158, p90 +671; 10% najlepszych ma 48% zysku. Cel Jeffa 0.60 nieosiagniety.
- **Do wlasciciela**: notabl - codziennie nadwyzka ponad max(10 000, 30 dni zoldu) (`CaravanGoods.cs:1490 ProfitToOwners`): 25 karawan oddalo 75 759 zl; rod - `CaravanSettle.cs` (prefiks AddIncomeFromParty): wyplata = max(0, min(wartosc - kapital, kiesa - podloga)): 9 z 126 karawan, 13 631 zl; 57 ponizej kapitalu, 59 "wartosc > kapital, ale kiesa <= podloga" (zysk stoi w towarze).
- **Doplaty notabli do 5000**: 43-50 tys./d (przelew notabl -> karawana, `Seal31B.cs:30`), wobec zysku do wlascicieli 87-114 tys./d. Netto kupcy swiata +45..65 tys./d, ale polowa karawan jest stale dotowana przez wlasciciela.
- **Zold -> sakiewki strazy -> wydane w miescie przy wyjezdzie** (69 696 zl/d do kas miast i ludu, Seal31B E3/4). Jedzenie w drodze z jukow (1 szt./20 ludzi/d, `CaravanEscort.cs`): zjedzone 581 szt., glodnych 21 karawan.
- **Myto targowe** (`TownToll.cs:120 TakeCaravan`, :98 CartEnd; Settings.cs:3871-3872): wozy 4%, karawany 2% utargu; polowa panu, polowa koronie. Sr. 51 dob: wozy 12 207 zl/d, karawany 6 305 zl/d. Gra sama nie bierze nic przy sprzedazy miastu; clo przy zakupie od miasta zostaje.
- **Myto Bliznakow** (`TwinsCrossing.cs:62`: 2 zl/czlowiek, 1 zl/zwierze): 0 zl tej doby, 2 736 zl w 52 doby (~53 zl/d) - karawany przez most praktycznie nie chodza. Straz przeprawy (`CrossingLaw.cs`) zawraca tylko partie lordow, nie karawany.
- **Marza kupcow miasta** (P1): kasy placa karawanom 386 tys., ten towar po cenie polki 873 tys. = +126% (zywnosc +217%).

## 4. Straz, zbojcy, strata

- Gra: banda atakuje wg lokalnej przewagi sily (DefaultMobilePartyAIModel, cs_full.cs:63259), ladunek nie gra roli. Nasze: straz wg sily najsilniejszej bandy przy drodze (`CaravanGuards285.cs:131 Need`, `.Road.cs`): potrzeba sr. 25.0 ludzi, 29.9 na karawane, slabszych niz banda drogi 34 ze 137; najem 16 ludzi/d, zwolnionych 11.
- Straty: karawany rozbite ~1.1/d (9 w 8 probkach "Kiesy rozbitych"; 2 w dobie 109809), tj. rzad **400+/rok przy 500 karawanach**; wozy wsi 4.1/d (bandy 3.3, wrogowie 0.8) przy 76 dojazdach/d - przezywalnosc 0.972 na kurs.
- Po stracie: kiesa do zwyciezcy (14 517 zl do kies band tej doby, `BeatenPurse`), ladunek lupem gry, ludzie w ksiedze ludzi. **Odrodzenie**: gra `DailyTickHero` (cs_full.cs:170232) - kupiec bez karawany wystawia nowa z szansa 75%/dobe; kapital z kiesy notabla ponad 4 500 + **z nadwyzki kasy miasta** (`NotableGold.cs:386 CarCap`, `CaravanCapital.cs` pkt 5). Liczba karawan stala 491-504 przez 63 doby.
- Zwijanie stratnych (CaravanLosingWoundUp) i wejscie tylko gdy handel placi (CaravanEntryWhenTradePays) - **oba WYLACZONE** (Settings.cs:4688-4689).
- Karawany wroga w zdobytym miescie (`EnemyCaravans.cs`): regula martwa - 0 karawan w murach w 39 zdobyciach (wyjezdzaja wczesniej).

## 5. Wozy wsi

`MarketCarts.cs:476 Choose`: miasto w zasiegu 250 jedn. drogi (Settings.cs:1266) z najwiekszym utargiem na dobe kursu, cena sztuka po sztuce (nadplata 11.3% wraca miastu), wiesc z drogi (towar i zloto innych wozow), caly magazyn na woz. `CartRisk.cs`: **pamiec przezywalnosci tras** (polowa po 56 dobach) i brama wroga (34 odrzucenia/d). `CartEscort297.cs`: milicja wsi eskortuje pod bandami. `CastleMarket.cs`: wies zamkowa najpierw karmi zamek. Doba: 117 wyborow, 50 do innego miasta niz wlasne, oczekiwany utarg 329 622 wobec 200 832 we wlasnym. Utarg wsi (112): pan 33.9%, wies 61.4%. Zatkane wsie 24 z 571 (11 spladrowanych).

## 6. Czy handel wyrownuje ceny i reaguje na wojne / zbojcow

**Ceny NIE sa wyrownane.** Mediana marzy miedzy cwiartka tansza a drozsza 65-75% (kara 3%, myto 2%); "mediana marzy unosi 34 doby drogi" przy odcinku 3.7 doby. Indeks p25-p75: ryby 1.93-7.02, wino 2.34-6.99, garnki 2.33-6.80, zboze 1.53-2.84. Zysk jest na stole, karawany go nie zbieraja: wiaza kapital w towarze (juki 2.62 mln zl po koszcie = 18 dob sprzedazy), kasy miast (BuyFloor 249 odrzuconych krokow, limit 1/20 52) i bramki zakupu.

**Zbytki: handel martwy.** Pasmo ceny od przewozu (`PriceBand306.cs`, marza 2.0, 2000 km) trzyma 832 z 2 619 par miasto-kategoria na suficie. Przyprawy, kiel morsa, papirus: p25 = p75 = sufit (3.2-3.56) we wszystkich miastach -> marza -8% -> nikt nie wozi; aksamit mediana = sufit 3.01. To brak podazy, nie rownowaga.

**Wojna**: tylko binarnie - wrogie i oblezone miasto wypada z celow BK; woz omija brame wroga; wrogowie rozbijaja 0.8 wozu/d. Brak: blokady, cla wojennego, wzrostu ceny ryzyka. **Zbojcy**: woz wsi uczy sie tras (CartRisk), karawana - nie (BK bez ryzyka; reaguje tylko liczba strazy). **Pora roku**: zadnej reguly sezonu w karawanach; posrednio sztormy zima, bloto, jarmarki po zniwach (`HarvestFairs.cs`: popyt +40% przez 14 dni, oplata 1%) - w zimie Westeros nie ma ich wcale (log: trwa 0).

## 7. Ocena wobec sredniowiecza

Zgodne: handel z roznicy cen z ksiega kosztu, brak sprzedazy ponizej kosztu, myto targowe od obrotu, kupiec karmi i oplaca eskorte, najemnik wydaje zold w gospodzie, eskorta wg zagrozenia drogi, chlop wiezie na najlepszy targ i pamieta zle drogi, pasmo ceny od kosztu przewozu, statek tanszy od wozu, zloto zamkniete (kontrole roznica 0 w kazdej linii).

Niezgodne / slabe:
1. Liczba karawan nie zalezy od zysku - wyznacza ja liczba kupcow (odrodzenie 75%/d, brak wyjscia stratnych). Polowa traci stale i jest dotowana przez wlasciciela; brak "wejscia i wyjscia z rynku".
2. Nowa karawane wspolfinansuje nadwyzka kasy miasta - quasi-ubezpieczenie kupca.
3. Straty ~80%+ karawan rocznie - o rzad wielkosci za duzo; mozliwe tylko dzieki darmowemu odrodzeniu.
4. Karawana nie wycenia ryzyka trasy ani wojny (poza zakazem wrogich miast).
5. Ceny trwale rozjechane 2-7x przy handlu ograniczonym kapitalem i kasami miast; zbytki przy suficie wszedzie.
6. Brak sezonu handlu (zima nie wstrzymuje ladu).
7. Myto przepraw ekonomicznie nieistotne (53 zl/d); cel 294c wg prawdziwej sprzedazy wylaczony.
8. Zold eskorty x0.5 - sztuczne pokretlo trzymajace karawany przy zyciu (decyzja Jeffa 28.10).

## 8. Czego nie udalo sie ustalic

- Dokladnej liczby karawan rozbitych na dobe w dlugim okresie (linia "Kiesy rozbitych" pisana co kilka dob - 8 probek) ani liczby nowych karawan na dobe.
- Karawany gracza: w tych zapisach 0 - reguly 259/305a/139b (dowodca, dziennik, osobna linia zoldu) bez dowodu z logu.
- Dlaczego 294c D1/D2 oraz zwijanie/wejscie 294 B/C sa wylaczone (nie czytalem CHANGELOG - decyzja czy cofniecie po tescie).
- Co bandy robia z ladunkiem rozbitej karawany (poza kiesa) - poza zakresem tej mapy.
- Czy karawany BK omijaja teren wroga w drodze (lista `_previouslyChangedCaravanTargetsDueToEnemyOnWay` - nie przeczytana do konca) i jak czesto rozbijaja je lordowie w wojnie (brak podzialu w logu).
- Zachowanie latem / w czasie jarmarku (logi tylko z zimy Westeros) i w dlugim horyzoncie > 63 doby.
- BetterEconomy (BEE `TryDeliverContract` - towar ze wsi prosto na polke miasta bez taboru): nie zmierzone, ile towaru idzie ta droga.
- Trendu cen w czasie (czy rozrzut indeksow maleje) - porownalem tylko 4 punkty z 52 dob (marza 75 -> 65%, bez wyraznego trendu).
