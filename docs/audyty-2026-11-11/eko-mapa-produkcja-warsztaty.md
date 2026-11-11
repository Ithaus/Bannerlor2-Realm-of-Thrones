# MAPA: PRODUKCJA I WARSZTATY (audyt, tylko odczyt)

Zrodla: kod `n367c/Armoury/src` (plik:linia), log `Modules/Armoury/Armoury-2026-11-09_21-17-29.log` (52 doby, dni 109752-109803 = doby 1104-1155 kampanii zaczetej 108648, czyli 4. rok, ZIMA; liczby ponizej z doby 109790, o ile nie napisano inaczej), `Armoury.json` Jeffa. Rok = 364 dni.

## 0. Werdykt w skrocie

Produkcja JEST dynamiczna, ale na trzech roznych "biegach":
- **wsie**: ilosc od ludzi i ziemi (szybko: zima, rabunek, pobor), a od ceny/popytu tylko WOLNO (regulator co 28 dob w pasmie x0.1-x1.3; zmiana zajec co 7 dob z hamulcem 60 dob) - i ta druga droga w logu prawie stoi (patrz 1.4, 1.5);
- **warsztaty zbrojne i rzemioslo miasta**: w pelni cenowe (ranking zysku na roboczodzien, bramka zysku, wsad z polki) - to najbardziej "zywy" kawalek;
- **warsztaty towarowe notabli**: tempo x F od ludzi i ceny, prog cyklu od ceny kupca, zmiana branzy za cena - dziala, ale 60% warsztatow stoi, a przezbrojen prawie nie ma.
Glowne waskie gardlo calego lancucha to nie surowiec, tylko **plynnosc kas miast** (kupiec nie ma za co kupic) i **rozwoz** (ruda jest, ale nie tam, gdzie jej brak).

## 1. WSIE

### 1.1 Lancuch czynnikow (jedno miejsce)
`MaterialLaw.ProdPostfix` (MaterialLaw.cs:184) mnozy wynik modelu gry po kolei: ruda x `MineOutputMultiplier` 3 (:205), drewno x `LumberOutputMultiplier` 3 (:206), `MineralOnce.Times` (:214, dubel mineralu BK - log: 68 wpisow zdjetych, 80 wsi z mnoznikiem), ludzie `VillageByPeople.ProdStep` (:229), stawki `GoodsRates.ProdStep` (:231), rece po spustoszeniu `Devastation.YieldFactor` (:235), rece w wojnie `FreeHands.YieldFactor` (:242), rok urodzaju `HarvestYear.ProdStep` (:249), stado `VillageHerds.ProdStep` (:250), regulator `HarvestValve.Factor` (:253), sufit dymarki `Bloomery.Cap` (:270). Model BK wsi NIE jest czynny (log "Budynki (338) produkcja wsi: model BK czynny NIE").

### 1.2 Plon od rak i ziemi
- **Od ludzi** (VillageByPeople.cs:15-36): plon = stawka typu x u_v x g(typ); u_v = ludzie okregu / srednia wies, sufit 6.0; g zamrozone w dniu kalibracji - "swiat, ktory sie wyludnia, wytwarza mniej". Log GL1: 560 wsi pod regula, czynnik min/p10/med/p90/max 0.078/0.269/0.873/2.046/4.295; suma swiata wobec kalibracji 0.96-1.04 (czyli ludnosc po 3 latach prawie bez zmian: przyrost +0.54%/rok).
- **KS (kalibracja startu)** (StartCalibration.cs:11-36): produkcja okregu = rece x wydajnosc x ziemia z mapy, 20 blokow na wies, dom (zboze wlasne) przed targiem. Log: stan ZASTOSOWANY od 108688; pod KS 286 z 571 okregow i 47 z 97 miast - **polowa swiata chodzi na KS, polowa na lancuchu gry** (dwie reguly na jedno zjawisko).
- **Zmeczona ziemia** (TiredLand.cs:9-26): ponad ziemie startu plon na glowe x (b/rho)^0.6, min 0.5. Log 109802: 658 okregow, ponad baze 19, srednio x0.968, min x0.772 - hamulec prawie nie pracuje (ludzi nie przybywa).
- **Pobor z pol** (FreeHands.cs:13-31): a = (sluzba + polegli...)/(0.27 x L_ref); prog 20% (`FreeHandsShare`), w zniwa x0.5; ponad progiem plon x max(0.5, ((1-a)/(1-f))^0.5) i stop nowym ochotnikom. Log: a srednio 3.15% mezczyzn, ponad progiem 5 z 227 regionow (Mur 347%, Hardhome 56%, Ten Towers 34%). **W praktyce pobor obniza plon tylko na Murze i za Murem** - w zwyklych krainach wojna 4. roku nie dochodzi do progu 20%. Uwaga: klucz "Levy By People nie" (GL4) - pobor nie idzie za liczba ludzi, tylko milicja.

### 1.3 Sezon i pogoda
- **Zima** (WinterBite.cs:115,153; `WinterVillageOutputCutPercent` 50, cieple krainy x0.4): log "ciecie plonu dzis: Wyspy Letnie 0% (3 wsi), cieple 15.7% (131), reszta 48.6% (437)". Kopalnie i wyrab bez ciecia (297, Settings.cs:4767): zelazo 51.9, srebro 442, drewno 1516 szt./d ocalone. Zboze z pol: 107 634 kg/d przed zima -> 55 591 po zimie.
- **Rok urodzaju** (HarvestYear.cs:15-36): raz na rok w oknie zniw (dni 196-252) x0.8-1.2, 1/20 rok glodu x0.6, pole na siatce 1/8 mapy. W logu nieczynny (zima) - **nie zweryfikowany w tym biegu**.
- **Uboj jesienny** (HerdCull.cs:12-33): 0.75 przyrostu przez 28 dni jesieni jako dlug stada, sol 0.1 kg/kg miesa kupowana w miescie. Log: dlug stad krowy 267, swinie 312, owce 1238 w 193 wsiach - dzialal jesienia, zima wies oddaje polowe przyrostu.

### 1.4 Rabunek
- `Devastation` + ksiega wiosek: log - uchodzcy 94 167 z 41.8 mln (0.2%), 155 wiosek dotknietych, 11 Looted, najgorszy okreg Duskendale plon x0.2421 (50.6% uchodzcow), powrot 154 ludzi/d po 30 dobach spokoju.
- **Stada jako lup** (VillageHerds.cs:35-40): stado ginie 1.5 x szybciej niz ludzie, max 90% na rabunek, dno 10%, odrost owce/swinie 25%/rok, bydlo 12%/rok, 0.5 szt. na napastnika.
- Wies spladrowana nie wysyla wozu (woz rusza tylko z Normal): 22 zatkane z 571, w tym 14 nie-Normal; kopalnie Stoney Shore i Silver Rock stoja z 171 i 489 ruda bo Looted.

### 1.5 Czy wies reaguje na cene i popyt
- **Regulator wsi** (HarvestValve.cs:17-38; `ValveStockDaysLow/High` 30/60, `VillageOutputStep` 1.3, podloga 0.1; decyzja co 28 dob na oknie 56). Log decyzja 40 (109796): **na suficie x1.30 stoi 12 towarow** - hardwood (stos 9 dni), salt (18), silver (10), flax (14), clay (13), limestone (9), marble (10), cow (15), hog (8), hides (29), Papyrus (20), spice (15). W dol: charcoal x0.10 (stos 2440 dni zuzycia - martwy towar), whale_oil x0.39, fur x0.50, gold_ore x0.13->0.17, iron x1.02->0.86 (stos 65 dni). WNIOSEK: regulator dziala, ale dla polowy towarow jest juz na suficie i dalej nie odpowiada - niedobor zostaje cenie.
- **Rece wsi za cena** (E3-D1): przesuniecie miedzy plonami listy BK, f 1.0-2.0, c min 0.85 (zboze 0.95); 221 wsi, wartosc +1331/-1331 d (neutralne).
- **Zajecia wiosek / A7** (VillageCrafts.cs:12-33, StartCalibration.Rule.cs:11-32): zmiana gdy inne zajecie >= 1.25 x przez 60 dob, 1 wioska okregu naraz, 5% rak regionu na sezon. Log 109802: **nacisk 540 wiosek (59.3%, 60.9% rak) chce zmiany, zaczete 0, skonczone 0**, wstrzymane 266 + 232, "doby bez pelnej zaplaty 498", w przebudowie 72. Top przejscia: wheat->silk_plant 115, wheat->cattle 109, olive->silk 86. Pod KS klucz Switch Rule domyslnie WYL (Rule.cs:26-30) - tylko pomiar. **Wybor zajecia wsi prawie nie rusza mimo silnego sygnalu cen.**
- **Zamowienia wsi 363** (Settings.cs:4397-4401: 3% rak pola, zasieg 600, fracht 5%/dobe): 267 wytworcow, drewno 3382/d zrobione, 2716 sprzedane; zloto zamawiajacy -14 332 = wytworcy +14 332 (zamkniete); **popyt niezaspokojony 192 729 zl**.
- Rece kopaln srebra/zlota (360a/360c, co 28 dob +-10%) i zelaza (f5-ruda, co 7 dob x1.05/x0.95): log - srebro "bez zmian" wszedzie, zelazo W DOL w 41 kopalniach (rynek PELNY: polki 6565 wobec celu 2398).

### 1.6 Las, drewno
`VillageWoodlot` (VillageWoodlot.cs:13-34): kazda wies nie-drwali 2.5 ladunku/d. Tydzien 109795-801: produkcja 60 098 (wsie drwali 25 227, zamowienia 363 21 778, las 12 596), zuzycie 60 682: budowy 23 776, roboty 363 21 778, mieszczanie 9 072, dymarki 4 852, warsztaty zbrojne 880. Zapas swiata 52.7 tys. = 6 dni. Drewno to rynek napiety "na styk".

## 2. WARSZTATY

### 2.1 Zbrojne (WorkshopLaw.cs:18-42)
Regula: wyrob z rankingu zysku na roboczodzien; receptura wedlug wagi (1 ruda = 1.5 kg surowki, 5 drewna na rude, +0.25 drewna/kg); rece `WorkshopWorkers` 6 x m_t (:836), miasta `TownHands` = max(dawny, 0.10 na 1000 ludzi rynku x indeks placy) x krok 1.30 (:815-820); placa 3 d/roboczodzien wg tieru; produkuje tylko z zyskiem >= 5%.
Log: 500 szt./d, koszt 39 849, sprzedaz 53 567; rece 11 563 roboczodni, **bezczynne 2.2%**; odpuszczone: bez zysku 177, brak surowca 55 (ruda 18, drewno 12, skora 31), **"brak zlota/kupca" 1214** = gotowa sztuka czeka, bo `town.Gold - TownPurse.BuyFloor < rev` (WorkshopLaw.cs:340); w toku 1362. W 52 dobach stale 1030-1160 (poza 1. doba). Rece przenoszone miedzy cechami wedlug braku: 2446/d. Zlom z nadmiaru: 417 szt./d skupione (glownie bron jednoreczna t1), "nadmiar swiata ponad rok popytu ok. 42 810 szt." - **nadprodukcja taniej broni t1 obok braku 20 099 korpusow i 23 856 broni wedlug szczebla** (pokrycie partii AI: korpus 78%, rece 65%).
Popyt wojska: zamowienia AI otwarte korpus 3766, helm 2029 szt.; cena wczoraj korpus 210 zl, helm 25 zl.

### 2.2 Towarowe notabli (WorkshopTrade.cs:22-64)
Utrzymanie 4 d/d do kasy miasta, place cyklu z kapitalu, kapital startowy 2000, cena warsztatu = 3 lata zysku; **cena kupca (241)**: miasto placi srednia sprzedaz z 28 dob, nie cene pustej polki.
Log: 291 warsztatow, **pracowalo 115 (40%)**, 454 cykle, wynik +24 687 d. Wedlug typu: bakery 78 (52 pracuje, +59 d/warsztat), smithy 39 (19), **wool_weavery 34 (4 pracuja, -4 d)**, wood 29 (11, -4.3), linen 24 (9, +45), silversmithy 19 (4, +332), armorsmithy 17, wine_press 10 (3), **weaponsmithy 7 / barding 6 / fletcher 6 / pottery 4 / butcher 2 - 0 cykli**, velvet 6 (4, **+1854 d/warsztat**). Utarg polki 118 460, zaplacono warsztatom 82 236, kasom miast zostalo 36 224, czeladnikom 28 890. Bankructwa 0. Mediana branzy: velvet 797, srebro 346, piekarnia 35, kuznia 5.6, welna -3.3, drewno -5.4. Rozrzut zysku 1000:1 miedzy branzami i brak wyrownania (wejscie nowych) - to nie jest rynek w rownowadze.
- **Tempo** (WorkshopPace.cs:16-35): F = clamp(m_t/srednia, 0.3, 4) x clamp(sqrt(indeks wyrobu), 0.7, 1.4). Log: F srednio 1.28, cykle/d 861 -> 1090 (x1.27); czesc ceny 1.29, czesc ludzi 1.00.
- **Zmiana branzy** (WorkshopRetool.cs:13-37): strata >= 60 dob w obu pamieciach, nowy typ >= max(+10 d, x1.3), kiesa >= 4500 po sprzecie, 3/dobe, miasto 30 dob, warsztat 120 dob; typy z linia broni wylaczone. Log: pod regula 147, **kandydaci 2, zmiany 0 (2 w sesji 52 dob)**, "stoja, ale rok na plusie" 12. Warsztat, ktory nie pracuje, traci tylko 4-6 d/d utrzymania - sygnal za slaby, zeby wymusic ruch.
- **Wsad** (284): cykle bez wsadu 125 wobec 454 udanych; braki welna 8, srebro 8, len 4, glina 3; jedzenie: zboze 56, jablka 23.

### 2.3 Ukryci rzemieslnicy
- **Rzemieslnicy BK** (ArtisanInputs.cs:15-52): kazdy dodatkowy cykl z wlasnego wsadu; log 109802: moc 1451 cykli, z wsadem 1255, bez wsadu 195; zdjete z niczego 1223 szt., zwrot do kas miast 49 500 zl.
- **Rzemioslo miasta 148** (TownCrafts.cs:14-35): welna 1.45, len 3.0, skory 2.4 na sztuke, marza 25%. Log: filc 84, plotno 404, skora 137; **rece zajete 19% (4164 z 21 816)**; koniec: bez zysku 53/31/66 miast, brak surowca 43/58/28. Indeksy: welna 1.85, filc 2.06, len 1.51, plotno 1.00, skory 1.12.
- **Przetwory ZC5** (StartCalibration.Crafts.cs:10-27): rece 45 256, **zajete 1671 (3.7%)**; koniec glownie "brak wsadu".
- **Strzelarze 172** (TownFletchers.cs:17-35): 45 snopow strzal + 37 beltow, **rece zajete 3%**, 88 miast konczy "bez zysku"; polki 7306 strzal, 0 miast bez strzal - rynek nasycony, produkcja slusznie stoi.
- Linie "z niczego" zamkniete: 608 cykli/d rzemieslnikow, 191 cykli linii strzal.

## 3. KOPALNIE, RUDA, KRUSZEC, MENNICA

- **Dymarka** (Bloomery.cs:17-38): 5 ladunkow drewna na lupe, las okregu albo woz z miasta; bez drewna - bez lup. Log: 282 lupy/d (model 315.6), spalone 1410 drewna = 5 x 282, bez wegla 28 lup (Sunmine/Starfall), rachunek za drewno otwarty 19 548 zl w 26 kopalniach.
- **Zelazo wedlug ladow**: Westeros 150 lup/d, zuzycie 173, zapas miast 6616, **8 z 59 miast bez zelaza**; Essos 132/d, zuzycie 97, 0 z 38 bez. Miasta bez rudy: Dreadfort, Last Hearth, Karhold, Deepwood Motte (cena 87-121 d wobec 44-52 d w miescie kopalni), a Starfall ma 2551, Draconys 1957. Kontrakty odmawiane: "kasa miasta przy zapasie kupcow" - najczestszy powod (ruda 10 z 11, drewno 23, len 11, welna 17). **Waskie gardlo = rozwoz + kasa kupca, nie wydobycie** (regulator rudy wlasnie tnie x0.86, bo swiat ma 65 dni zapasu).
- **Regale** (MineRegale.cs:15-36; u Jeffa `MineRegaleShare` 0.25, domyslna 0.125): 3728 zl/d kiesa wsi -> licznik pana, 96% z jednego rodu (Prestayn/Braavos 3592). KS: 99 wsi, 6469 zl/d sredniej. "Lannister >> inni" dalej NIE zachodzi: Zloto Casterly Rock - wydobycie 0.00.
- **Mennica** (CrownMint.cs:14-35; oplata 5%, zapas 30 dni, zuzycie monety 0.3%/rok): 96 mennic, bije 0-5 dziennie, 0-1900 szt. srebra/d; 39 dob: nowa moneta 482 300, zuzycie 10 878 = **+1.95%/rok** przy 225.08 mln w swiecie. Srebro Westeros: wydobycie 842.6/d, zuzycie 1059.8/d (mennica 490.2, reszta 569.6), zapas 10.3 dnia. Mennica jest realnym odbiorca polowy srebra - lancuch kopalnia -> moneta zamkniety i ksiegowany (kontrola swiata rozjazd 0).
- **Cena kruszcu widzi caly zapas** (OrePrice360.cs:11-28): spadek najwyzej 5%/d; mediana indeksu srebra 1.63 / 1.33.

## 4. MAJATKI BK
`EstateIncome187.cs:13-34`: wyplata "z produkcji" (ok. 293 tys./d z niczego) zablokowana; dochod tylko z udzialu w utargu wozu. Log: 1856 majatkow, udzial 22 361 zl w 178 majatkach, srednia 28 dob 28 946 zl/d, 1222 majatki z dochodem. Utarg wsi (112): 79 powrotow, 270 822 zl = pan 37.1%, majatki BK 12 782, wies 58.1%; "bez odbiorcy" 0.

## 5. Ocena: dynamiczne czy stale, zgodne ze sredniowieczem

Dziala i jest sensowne historycznie: ludzie i ziemia jako zrodlo plonu; zima -49%; uboj jesienny z sola; stada jako pierwszy lup; rabunek = uchodzcy = mniej rak; receptury wedlug wagi (len 3:1, welna 1.45:1); dymarka z weglem drzewnym; regale 1/8-1/4; mennica z oplata 5%; zakaz produkcji bez wsadu; cena kupca zamiast renty z pustej polki.

Slabe punkty (wedlug wagi):
1. **Kasy miast jako korek**: 1214 gotowych sztuk broni czeka na kupca, 50 z 97 miast ponizej zapasu kupcow, kontrakty surowca odrzucane "kasa miasta". Produkcja reaguje na cene, ale sprzedaz staje na plynnosci.
2. **60% warsztatow towarowych nie pracuje**, a przezbrojen jest 2 na 52 doby; 5 branz z zerem cykli. Zysk branz rozni sie 1000 x (aksamit 1854 d wobec welny -4 d) bez wyrownania.
3. **Wybor zajec wsi zablokowany**: 59% wiosek pod naciskiem, 0 zmian. Podaz surowcow nie przestawia sie strukturalnie.
4. **Regulator na suficie dla 12 towarow** (drewno, sol, srebro, len, glina, kamien, bydlo, swinie): trwaly niedobor, ktorego produkcja juz nie goni.
5. **Ukryte rece miast bezczynne w 81-97%** (rzemioslo 19%, ZC5 3.7%, strzelarze 3%), a place W na dnie x0.60 w 45 z 47 miast KS - nadmiar rak bez wsadu.
6. **Dwa systemy naraz**: 286 okregow KS, 285 lancuch gry.
7. Wegiel drzewny (charcoal) 60 903 szt. = 2440 dni zuzycia - towar bez odbiorcy; konie zwykle 16 096 na targu przy hodowli x0.10.
8. Nadmiar broni t1 (42.8 tys. ponad rok popytu, 417/d na zlom) obok braku wyzszych szczebli.

## 6. Czego nie udalo sie ustalic
- Rok urodzaju i zmeczona ziemia w dzialaniu (log to zima; okno zniw NIE).
- Bieg roczny: mam 52 doby zimy 4. roku - nie widzialem lata/zniw ani trendu wielu lat.
- BetterEconomy (Modules) - nie czytany; w ksiedze towarow ma 2130 ramek/d, tresc nieznana.
- Dekompilacje gry/BK (cs_full.cs, bk.cs) nie byly czytane w tym przebiegu - opis zachowania cudzego kodu pochodzi z komentarzy naszych plikow, nie z wlasnego sprawdzenia.
- Dlaczego dokladnie 498 "dob bez pelnej zaplaty" blokuje przebudowe zajec (kiesa wsi czy limit czwartej czesci) - nie rozebrane.
- Dlaczego wool_weavery (34) stoi przy indeksie filcu 2.06 i welny 1.85 (cena kupca? brak welny w 43 miastach? konkurencja ukrytych rak) - nie rozstrzygniete.
- Kuznia gracza (CRAFT) jako producent - poza linia "Kuznia (E8/K)" nie badana.
- Ustawienia MCM czytalem tylko dla 15 kluczy; reszta wedlug domyslnych z Settings.cs - mozliwe nadpisania w Armoury.json.
