# p5-urodzaj - rok urodzaju i zly rok (R3-05e), jesienny uboj i solenie (R3-05f)

Galaz `w-toku/p5-urodzaj`, baza 5189c0fe (kandydat WGRANIA 92). Jeff 20.10: "28 - nie, 29 - nie. Pozostale tak". Status: **DO SPRAWDZENIA** (zbudowane, bez autotestu i bez wgrania).

## Dla Jeffa (prosto)

Co roku w czasie zniw kazda okolica ma swoja pogode: pola i sady rodza od 0,8 do 1,2 zwyklego plonu, sasiednie okregi podobnie, a mniej wiecej raz na 20 lat w jakiejs okolicy plon pada do 0,6 i przychodzi glod. Taki rok trwa do nastepnych zniw; zima tnie plony jak dotad i wtedy rok urodzaju sie nie liczy, zeby zly rok nie nalozyl sie na dluga zime. Pod koniec jesieni (ostatnie 4 tygodnie) wsie hodowlane ubijaja czesc stada: na targ idzie wiecej miesa i skor, a wies kupuje w swoim miescie sol do solenia, wiec sol jesienia znika z polek i drozeje. Za to zima ta wies wysyla na targ mniej zywych zwierzat, dopoki ubite sie nie odrobia - nic nie bierze sie z niczego. Oba pomysly maja wlasne przelaczniki w MCM (Harvest Year Enabled, Herd Cull Enabled), domyslnie wlaczone.

## R3-05e Rok urodzaju i zly rok (`Armoury/src/HarvestYear.cs`)

**Problem / zrodlo:** POMYSLY-35 nr 12; audyt `docs/audyt-2026-10-09/10-pomysly.md` Z6, F10 (zly zbior BEE byl bez przyczyny i bez skutku dla plonow - wylaczony). Plony wsi byly co roku takie same; spichlerze okregow (Famine), ceny i wozy nie mialy czego wyrownywac.

**Zmiana:**
- Losowanie raz na rok kalendarza, w pierwszej dobie okna zniw (`FreeHands.HarvestWindow`: dni 196-252 roku 364 i pora Westeros lato albo jesien; w latach zimy nie ma okna ani losowania).
- Pogoda jako pole na mapie: siatka wezlow co 1/8 dluzszego boku prostokata osad; wezel = zwykly rok `1 + 0.2 x (u1 + u2 - 1)` (trojkatnie 0.8-1.2, srednia 1) albo z szansa 1/20 rok glodu 0.6. Okreg (warownia wsi, `Village.Bound`) bierze wartosc pola w polozeniu swojej warowni (interpolacja dwuliniowa z wygladzeniem) - caly okreg ten sam rok, sasiedzi podobnie, dalekie okregi niezaleznie. Pola mieszczan (KS) - polozenie miasta. Ziarno losowania z id kampanii i roku; wartosci wezlow w zapisie.
- Jeden czynnik na wszystkich drogach plonu pol: lancuch wsi `MaterialLaw.ProdPostfix` (krok `HarvestYear.ProdStep` po rekach E4/5 i E4/7, wzor "czynnik x suma") i jego odczyt `ChainFactor`; zywnosc wsi gry (nowy postfiks na `DefaultVillageProductionCalculatorModel.CalculateDailyFoodProductionAmount`, `AccessTools.DeclaredMethod`) - to plon spichlerza okregu, bez wsi rybackich, wielorybniczych i lowcow morsow; rachunek KS `ComputeDistrict` (czynniki gry) dla wsi i pol mieszczan.
- Towary: zboze, len, winogrona, oliwki, daktyle, bawelna, papirus, jablka, pomarancze, marchew (+ zywnosc wsi). Zwierzeta, kopaliny, ryby, las - bez zmian.
- Zima Westeros: czynnik 1 (plon tnie tylko WinterBite). Rok wazny 364 doby od losowania. Start neutralny: do pierwszego losowania x1.
- Wiesc dla gracza przy losowaniu (najlepszy i najgorszy kraj; osobno glod), linia logu.
- Zapis: `arm_harvest_year` przez `SaveText.Sync` (brak klucza = stary zapis -> x1 do nastepnego okna zniw).

**Decyzje (projektuje Claude):** pole pogody zamiast stalej listy regionow (sasiedztwo bez recznej mapy, pokrywa 100% wsi i pol mieszczan); zakres i szansa glodu z audytu (Z6: +-20% "zwykly zly rok", 0.6 = 1315-17, "ok. raz na 20 lat" - szacunek); w zimie x1 (ryzyko audytu: zly rok na wieloletniej zimie); srednia swiata ok. 0.98 (glod jest rzadkim ogonem w dol, bez wyrownywania w gore).

**Ryzyko / kontrola calosci:**
- Kolizje: jeden czynnik w trzech drogach plonu (lancuch gry, zywnosc wsi, KS) - te same miejsca, w ktorych mnozy zima i rece E4/5/E4/7; KS Plan (bez czynnikow) nie dostaje roku - Plan liczy rece, nie pogode (jak zima). `VillageCrafts.Now` i `DistrictWorks` (widoki wartosci czesci okregu) czynnika roku nie maja - tylko odczyt, roznica w wycenie.
- KS w stanie Plan (u Jeffa) liczy ksiege towarow z rynku - rok urodzaju przesunie ksiege tego roku (zboze); po wlaczeniu KS czynnik dziala w `ComputeDistrict`.
- Regulator `HarvestValve` nie reguluje zywnosci - nie zgasi roku; regulator towarow (len, bawelna) moze czesc roku wyrownac po 28 dobach.
- Watki: odczyt z watkow AI tylko z migawki (slownik po publikacji niezmieniany, budowa pod blokada); flaga doby ustawiana w Daily. Pierwsza czesc doby po wczytaniu - x1 (bez odczytu czasu w SyncData).
- Zamknieta ekonomia: tylko ilosc plonu z pracy ludzi okregu; zadnego zlota.

**Test w grze / log_grep:** `Rok urodzaju (R3-05e):` - przy losowaniu `LOSOWANIE roku N`, siatka i wezly glodu, wsie srednio / min / max, liczba okregow w urodzaju, zlym roku i glodzie, srednia wsi wedlug krolestw; dalej co 28 dob i przy zmianie stanu (`czynny TAK/NIE`). Start testu 160 dob z WGRANIA 91 wypada w oknie zniw (dzien roku 239) - losowanie w pierwszej dobie. Sprawdzic: `Harvest is in:` w wiadomosciach, ceny zboza miedzy krolestwami (linia plotek/cen), `Glod` (Famine) w okregach z x0.6, `potkniecia 0`.

## R3-05f Jesienny uboj i solenie (`Armoury/src/HerdCull.cs`)

**Problem / zrodlo:** POMYSLY-35 nr 13; audyt Z7 ("sprawdzic, czy stado wsi ma stan, inaczej uboj z niczego"). Mieso, skory i sol nie mialy pory roku.

**Sprawdzenie stanu stada:** gra (VillageGoodProductionCampaignBehavior, dekompilacja 1.4.8) nie trzyma liczby zwierzat wsi - tylko dzienny przyrost z modelu dopisywany do magazynu wsi (`TickGoodProduction` -> `OnItemProduced`); BK tez nie. Dlatego stado ma stan w naszym kodzie: **dlug stada** na wies i zwierze (w zapisie).

**Zmiana:**
- Okno uboju: ostatnie 28 dni jesieni (pora gospodarki `WesterosClimate.Now`, dlugosc z kalendarza Westeros; bez stanu klimatu - pora gry).
- W oknie, przy kazdym przyroscie krow, swin i owiec wsi (nasluch `OnItemProducedEvent`): dodatkowo ubijane 0.75 przyrostu (ulamki na nastepna dobe) -> mieso, skory, welna do magazynu wsi wedlug linii ukrytych rzemieslnikow BK (krowa 6 miesa + 2 skory, owca 1 + 1 + 1 welna, swinia 2 + 1) - woz wsi wiezie je na targ jak kazdy towar.
- Sol: 0.1 kg soli na kg miesa; wies kupuje cale worki w miescie targowym (`TradeBound`, inaczej `Bound`) po cenie targu - zloto kiesa wsi -> kasa miasta, potem towar z polki (polka zostawia 5 workow). Bez soli albo zlota zwierzeta zostaja zywe (bez uboju i bez dlugu). Resztka worka czeka w beczce wsi.
- Kazde ubite zwierze = +1 dlug stada. Po oknie (zima i dalej, az do splaty) wies oddaje polowe dziennego przyrostu: te zwierzeta zdejmujemy z magazynu zaraz po dopisaniu przez gre. Suma zwierzat w cyklu bez zmian.
- Wies spladrowana / opuszczona - bez uboju i splaty tej doby. Zapis `arm_herd_cull` (`SaveText.Sync`, tylko wsie z dlugiem).

**Decyzje:** liczby, ktorych zrodlo nie podaje (audyt: "liczby trzeba ustalic"), ustalam sam: okno 28 dni przed zima (propozycja z opisu), uboj 0.75 przyrostu dziennie (ok. 3 tygodnie przyrostu na okno), splata 0.5 przyrostu, sol 10% wagi miesa (solenie na sucho), przelicznik miesa i skor z BK (ten sam co uboj w miastach - SlaughterHides). Uboj bez soli nie zachodzi - przyczyna popytu na sol jest prawdziwa.

**Ryzyko / kontrola calosci:**
- Zamknieta ekonomia: mieso i skory tylko z prawdziwego (przyszlego) przyrostu, splacone zima; sol z polki miasta za zloto wsi (przelew zerowy). Cofniecie DLL albo wylaczenie klucza w trakcie splaty = dlug przepada (zwierzeta wracaja na targ wczesniej) - jedyna nieszczelnosc, ograniczona do jednej zimy.
- Kolizje: KS przypisuje mieso i skory z uboju zywca miastom - jesienia czesc uboju przechodzi do wsi, a miasta zima dostaja mniej zywca (ta sama liczba zwierzat). Ksiega pieniedzy wsi (wsie-kiesy.csv) zobaczy zakup soli jako "poza tickiem" (zmiana w ticku osady gry). Zakup soli nie karmi licznika potrzeb wsi `VillNeeds` (to sol do miesa, nie do domu).
- Okno zalezy od konca jesieni Westeros: u Jeffa jesien trwa 428 dni (w logu testu WGRANIA 91 dzien 31-38 pory), wiec pierwszy uboj wypadnie dopiero pod koniec tej jesieni.
- Watek glowny (tick osad i Daily); bez odczytow z watkow AI; wyjatki per obiekt liczone.

**Test w grze / log_grep:** `Uboj jesienny (R3-05f):` - `okno uboju TAK/NIE`, `do konca jesieni N dni`, za dobe miniona: ubite ponad przyrost (krowy/swinie/owce), mieso, skory, welna, sol kupiona (worki, zl), zwierzeta zostawione bez soli, splata stada, dlug stad. Poza oknem i splata linia co 28 dob. W tescie 160 dob z WGRANIA 91 okna nie bedzie (jesien do konca ok. 390 dni) - zobaczymy tylko `okno uboju NIE` i odliczanie; pelny test wymaga zapisu blisko konca jesieni.
