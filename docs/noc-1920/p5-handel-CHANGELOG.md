## p5-handel (20/21.10) - jarmarki po zniwach (R3-05a) i barki rzeczne (R3-05b) (Armoury)

**Dla Jeffa (prosto).** Po zniwach kazda kraina (Polnoc, Dorzecze, Zachod, Reach, Dorne, miasta Essos...) ma raz w roku dwutygodniowy jarmark w swoim
najbogatszym miescie - po kolei od poludnia na polnoc, od ok. 253. dnia roku do konca roku; zniwa sa w grze raz w roku, wiec jesienia trwa naraz kilka jarmarkow,
a przez reszte roku zadnego (nie "jeden co 2 tygodnie przez caly rok", jak szacowal opis). Na jarmarku jest wiecej kupujacych, wiec miasto placi lepiej za towary
i zwierzeta - nie za bron, zbroje i narzedzia, wiec lupu z bitew tam drozej nie sprzedasz; wozy wsi i karawany same tam ciagna i placa 1% oplaty targowej do kasy
miasta (Ty nie placisz). Barki: wies nad rzeka (Trident, Mander, Miodowa, Zielona Krew, Rhoyne) moze wozic towar do miasta nad ta sama rzeka dwa razy dalej niz ladem,
ale woz dalej jedzie caly kurs droga, wiec wybierze dalekie miasto tylko wtedy, gdy naprawde zarobi wiecej; karawany licza droge wzdluz rzeki o polowe krotsza.
Czarny Nurt w ROT ma tylko Krolewska Przystan i Czerwona Twierdze tuz obok siebie, wiec barka nic na nim nie zmienia.

**Problem / zrodlo pomyslu.** `docs/POMYSLY-35-2026-10-20.md` poz. 8 (R3-05a) i 9 (R3-05b); Jeff 20.10: "28 - nie, 29 - nie. Pozostale tak". Audyt
`docs/audyt-2026-10-09/10-pomysly.md` Z8 (jarmarki Szampanii 2-6 tygodni, rozlozone w roku, z jarmarkow rolnych i bydlecych) i Z9 (Masschaele 1993: przewoz
ladem ok. 2 x drozszy niz rzeka; miasta rzeczne 1334 - ponad polowa majatku miast). Dzis (log testu 160 dob na WGRANIU 91,
`Armoury-2026-10-20_00-44-29.log`, ostatnia doba 109235): "Myto targowe (E3-G3)": wozy wsi 90 sprzedazy za 342 629, karawany 621 sprzedazy za 309 650;
"Dowoz (wozy)": 121 wyborow miasta, do wlasnego 85, do innego 36. Zadnego jarmarku ani rzeki w rachunku (losowy "jarmark" BEE wylaczony, F10/Z5).
Okno zniw w tym tescie: 13 dob "okno zniw TAK" (dni 240-252, lato/jesien) - test 160 dob od dnia roku ok. 239 obejmuje caly sezon jarmarkow.

**Zmiana.**
- `Armoury/src/HarvestFairs.cs` (nowy, klucz MCM **Harvest Fairs**, domyslnie TAK):
  - kraina = kultura miasta (`Settlement.Culture`; ROT: battania = Polnoc, vlandia = Zachod); kalendarz sesji: krainy z miastem od poludnia na polnoc (srednie Y
    miast), poczatki rowno od dnia `FreeHands.HarvestTo + 1` (253 w roku 364) do `rok - 15`; jarmark 14 dni; rok bez zniw (pora Westeros nie lato i nie jesien w dniu
    otwarcia - ta sama regula co okno zniw) - pominiety;
  - miasto: najwieksza Prosperity krainy bez oblezenia w dniu otwarcia; zapis `arm_fairs` (SaveText.Sync, "hf1|kraina,miasto,start,pominiety;..."), po wczytaniu
    to samo miasto i ten sam koniec; brak klucza (stary zapis) = brak jarmarkow w toku;
  - popyt: prefiks/postfiks `ItemConsumptionBehavior.UpdateSupplyAndDemand` (AccessTools.DeclaredMethod, wlasna Harmony "armoury.p5handel.fairs", w kampanii raz na
    proces) - w miescie jarmarku dla towarow handlowych i zwierzat dopis b x (nowy - (1 - a) x stary) = b x a x szacunek dnia, b = 0.40, a = waga wygladzania gry
    (mierzona raz na sesje jak FoodSubstitute, 0.15). To samo, co szacunek x 1.4, bez ponownego pytania modelu i bez skladania sie z doby na dobe; indeks popytu
    rosnie przez 2 tygodnie do ok. +36% i po jarmarku sam wraca. Budzet konsumpcji mieszczan bez zmian; uzbrojenie i narzedzia (po recenzji) poza regula;
  - oplata targowa: `TownToll.CartEnd` / `TakeCaravan` wolaja `HarvestFairs.CartFee` / `CaravanFee` - 1% (zaokraglone) utargu sprzedazy miastu jarmarku z sakwy
    sprzedajacego do `Town.ChangeGold`; te same zwolnienia co myto (kontrakty 174, wyprzedaz przed rozwiazaniem, dostawy); dziala takze przy wylaczonym Town Market
    Toll. Ksiega: woz - migawka okna przesunieta o myto + oplate (`MoneyLedger.CartTollInWindow`), karawana - oplata w `TownToll.LastToTown` (kontrola T1) i w
    zwracanym "myto" (utarg przed mytem w CaravanBulk/CaravanAmmo/CaravanGoods bez zmian); wplata nazwana `Note169(N169Toll)` jak myto do kasy osady;
  - wiesc dla gracza przy otwarciu: "Harvest fair in X for 14 days: more buyers, better prices.";
  - linie: "Jarmarki po zniwach (R3-05a): start sesji" (kalendarz kraina@dzien roku, latka) i "Jarmarki po zniwach (R3-05a): dzien N" (trwa, otwarte, zamkniete,
    pominiete, oplata wozow i karawan, dopis popytu, waga, potkniecia).
- `Armoury/src/RiverBarges.cs` (nowy, klucz MCM **River Barges**, domyslnie TAK) i `Armoury/ModuleData/arm_rivers.tsv` (nowy, 30 osad, recznie z mapy ROT 8.1.8
  i kanonu - tylko pewne): Trident (Riverrun, Stone Hedge, Fairmarket, The Twins, Harroway, Darry, Saltpans), Mander (Highgarden, Ashford, Bitterbridge, Tumbleton,
  Lord Hewett's Town), Honeywine (Oldtown, Honeyholt), Blackwater (King's Landing, Red Keep), Greenblood (Planky Town, Godsgrace, Vaith), Rhoyne z Noyne i Qhoyne
  (Volantis, Volon Therys, Valysar, Selhorys, Sar Mell, Chroyane, Ghoyan Drohe, Ar Noy, Qohor, Ny Sar, Norvos). Wies nalezy do rzeki swojej osady (Bound) - ok. 90 wsi.
  - woz wsi (po recenzji): `MarketCarts.Choose` - test ZASIEGU z `d x CartFactor` (CartFactor = 0.5 dla wsi i miasta nad ta sama rzeka; MarketMaxDistance 250 =
    do 500 drogi wzdluz rzeki); czas kursu `2 x d / perDay + czekanie` i przestoj magazynu z PRAWDZIWEJ drogi (woz jedzie caly kurs ladem). Pierwsza wersja mnozyla
    czas kursu przez 0.5 - recenzja: to tylko ukrywalo przestoj magazynu wsi, ktory naprawde nastepuje (patrz Recenzja);
  - karawana: postfiks `BKCaravansBehavior.GetTradeScoreForTown` (DeclaredMethod, Harmony "armoury.p5handel.barges") - osada postoju/wyjazdu i miasto celu nad ta sama
    rzeka: wynik x (d + kara(d) + dom + 8) / (d/2 + kara(d/2) + dom + 8) - formula BK z polowa drogi (progi kary jak BK/BKROT ze sredniej odleglosci miast);
    wynik -1 (odciecie BKROT/IslandRoads) zostaje;
  - linie: "Barki rzeczne (R3-05b): start sesji" (plik, osad na mapie, braki, rzeki, wsi nad rzekami, latka) i "Barki rzeczne (R3-05b): dzien N".
- Wpiecia w plikach wspolnych (minimalne): `ArmouryBehavior.cs` - Reset w osobnym polu (`_p5HandelReset`), SessionStart x2, Daily x2 (po TownToll.Daily), SyncData
  `arm_fairs`; `TownToll.cs` - oplata w CartEnd/TakeCaravan i (po recenzji) przeciazenie `CaravanRateFor(mp, st)`; `CaravanGoods.cs` - 1 linia (prog T1 z oplata jarmarku);
  `MarketCarts.cs` - CartFactor w tescie zasiegu i liczniki; `Settings.cs` - 2 pola; gen_mcm 2066 ustawien.

**Ryzyko / co sprawdzic (kontrola calosci).**
- Zamknieta ekonomia: jarmark nie tworzy zlota - wyzsze ceny placi kasa miasta (sprzedajacym) i kupujacy w miescie (miastu); oplata to przelew sakwa -> kasa miasta
  (najpierw zdjecie, wyjatek oddaje zloto). Barki nie przenosza niczego - zmieniaja tylko wybor celu.
- Kolizje: dopis popytu biegnie po wygladzaniu gry, przed UpdateDemandShift (FoodSubstitute, NoMakerBudget, KsHunger przesuwaja popyt potem - bez zmian); RawPrice
  (szacunek surowcow) nie jest ruszany - dopis liczony z wyniku. BK MakeConsumption/zamki: tylko miasta jarmarku. Oplata nie dubluje myta (osobna stawka, osobny
  licznik); "Myto targowe" liczy jak dotad tylko myto. Ocena BK: IslandRoads i BKROT bez zmian (wynik <= 0 pomijany).
- Przeplywy: jarmark sciaga wozy z zasiegu (250) i karawany - na 2 tygodnie inne miasta krainy moga dostac mniej (ruda, drewno); po jarmarku polka miasta jarmarku
  pelna i tania, karawany rozwoza. Obserwowac "Dowoz (wozy)" i "Ruda/drewno" w miastach w sezonie jarmarkow.
- Barki: rachunek, nie ruch - woz dalej jedzie caly kurs ladem. Po recenzji miernik liczy przestoj magazynu z prawdziwej drogi, wiec dalekie miasto rzeczne
  (250-500) wygrywa tylko, gdy mimo przestoju daje wiecej na dobe (przy d = 400 kurs ok. 13.5 doby, wies musi dostac ok. 2.7 x tyle na sztuke co blisko) - rzadko,
  glownie gdy bliskie miasta sa zawalone tym towarem. Taki woz dluzej jest na drodze (bandyci) i dluzej nie ma go we wsi - obserwowac "z tego poza zasiegiem ladowym"
  i "Dowoz (skutki)" (zatkane magazyny wsi).
  Bledna para w pliku = dziwny handel - plik ma tylko pewne osady; Blackwater w ROT ma tylko Krolewska Przystan i Czerwona Twierdze (malo skutku).
- Prog zysku karawan T1 zna oplate jarmarku (po recenzji: `TownToll.CaravanRateFor(mp, st)` = myto + 1% w miescie jarmarku, te same zwolnienia).
- Starsza DLL pomija klucz `arm_fairs` (jarmark w toku przepada, nic wiecej).

**Status.** DO SPRAWDZENIA (build 0 bledow; bez testu w grze - zakaz uruchamiania w tym zadaniu).

**Decyzje (projektuje Claude).** Kraina = kultura miasta (FreeHands.RealmOf nie wolany - jego pamiec kraina sprzed kalibracji); jeden jarmark na kraine w roku, w
najbogatszym miescie; od poludnia na polnoc po oknie zniw (Szampania: rozlozone w roku); +40% szacunku popytu (cena ok. +20% na koniec); oplata 1% (Jeff/audyt
"np. 1%"); tylko towary handlowe i zwierzeta; Wielki Turniej przy jarmarku - NIE (nic ponad zadanie; GrandTourney osobno). Barki: polowa drogi (Masschaele x2),
tylko rachunek (opis Jeffa), wies przez swoja osade, u wozu (po recenzji) polowa drogi tylko do zasiegu - czas i przestoj z prawdziwej drogi; tylko osady pewne z kanonu (Goldengrove, Old Oak, Raventree, Wayfarer's Rest,
Pinkmaiden, Acorn Hall - pominiete jako niepewne).

**Test w grze (autotest 160 dob od nowej kampanii obejmuje sezon jarmarkow).**
- `Jarmarki po zniwach (R3-05a): start sesji` - "latka UpdateSupplyAndDemand wpieta", kalendarz z ok. 30 krainami;
- `Jarmarki po zniwach (R3-05a): dzien` - od dnia roku 253 "trwa" 1-5, "otwarte" z nazwami miast, "oplata targowa: wozy sprzedazy" > 0 i "karawany" > 0,
  "dopis w miastach" > 0; "potkniecia 0";
- `Barki rzeczne (R3-05b): start sesji` - "osad na mapie 30", bez "BRAK na mapie", "latka ocena miast BK wpieta", "wsi nad rzekami" ok. 90;
- `Barki rzeczne (R3-05b): dzien` - "miast w wyborze tylko dzieki barce" > 0 (miasta rzeczne 250-500 od wsi weszly do wyboru), "ocen miast z barka" > 0,
  "potkniecia 0"; "(z tego poza zasiegiem ladowym N)" - ile wozow naprawde pojechalo dalej niz 250 (moze byc 0 albo malo - to wynik uczciwego rachunku, nie blad);
- `Myto targowe (E3-G3)` - kontrola "roznica 0" jak dotad; `Karawany - towar tam, gdzie maja zysk (E3-T1)` - kontrola kas bez roznic.

**Recenzja (PELNA, 20.10) - poprawki (HEAD po commicie "p5-handel: poprawki po recenzji").**
- WAZNE (prawdziwe) - barki w wyborze wozu wsi ukrywaly przestoj magazynu: `T` (czas kursu) wchodzi tylko do czlonu przestoju `tf / T`, wiec polowa `T` przy
  d < ok. 139 nic nie zmieniala, a przy 139-250 udawala, ze magazyn nie stoi. W mierniku wozu nie ma innego kosztu drogi niz czas, a czasu barka (bez barek na mapie)
  nie skraca - nie ma czego uczciwie potaniac. Poprawka: barka poszerza ZASIEG (droga nad ta sama rzeka liczy sie do MarketMaxDistance w polowie), a czas kursu i
  przestoj licza sie z prawdziwej drogi. Daje to to, co mowi pomysl ("sprzedaja dalej") bez falszu w rachunku. Nie wybralem premii do wartosci ladunku (wies
  wybieralaby miasto, ktore placi mniej) ani szybszej jazdy wozow nad rzeka (to zmiana na mapie, a opis Jeffa mowi "zmienia sie tylko rachunek"). Linia "Barki
  rzeczne": nowe liczniki "miast w wyborze tylko dzieki barce" i "z tego poza zasiegiem ladowym"; opis klucza River Barges w MCM poprawiony.
- drobne (prawdziwe) - drugi jarmark krainy w roku po wczytaniu: test "juz byl" porownuje teraz kraine i ROK (`f.Start / y == year`), a nie dokladny dzien startu.
- drobne (prawdziwe) - Czarny Nurt: w ROT stoja nad nim tylko King's Landing i Red Keep obok siebie; innej osady w gornym biegu pewnej z kanonu nie ma, wiec nie
  dopisalem nic (bledna para = dziwny handel). Wpisy zostaja, w arm_rivers.tsv jest komentarz, a Jeff dostaje to wprost w opisie.
- drobne (prawdziwe) - kalendarz i lup wobec opisu: kalendarz zostaje "po zniwach" (tytul i pierwsze zdanie pomyslu; okno zniw w grze jest jedno w roku, dni 196-252),
  wiec 30 jarmarkow w dniach 253-362, nowy co ok. 3.3 doby, 4-5 naraz. Rozlozenie na caly rok dawaloby jarmarki prawie rok po zniwach. Opis dla Jeffa sprostowany:
  bron, zbroja i narzedzia (lup) nie sa objete dopisem popytu (osobny rynek broni Armoury), a gracz sam oplaty nie placi (tylko wozy wsi i karawany).
- drobne (prawdziwe) - narzedzia: kategoria "tools" wyjeta z dopisu popytu (ToolsByUse czyta Demand jako dzienne zuzycie - bramka 30 dni; TownFolk.Savings dzieli
  wydatki wedlug Demand w grupie, a wszystkie towary grupy rosna razem, wiec udzialy prawie bez zmian - bez poprawki).
- drobne (czesciowo) - raport oplaty: linia jarmarkow podpisuje teraz liczniki "dzien N-1" i "doba N-1 - oplata targowa" (jak Myto targowe i Barki), a stan
  (trwa, otwarte, zamkniete) jako "stan od doby N". Wlasnej pozycji Note169 i wiersza w Pompach F NIE dodalem: to zmiana tablic MoneyLedger.Obieg/Pumps, ktore
  scala 18 galezi naraz (ryzyko kolizji numerow N169), a bilans jest poprawny. Oplata jest w Note169 pod "myto do kas osad (E3-G3)" i w linii jarmarkow ("do kas
  miast"); do rozdzielenia przy scaleniu, jesli trzeba.
- drobne (prawdziwe) - prog zysku T1: `TownToll.CaravanRateFor(mp, st)` (nowe przeciazenie) dolicza 1% w miescie jarmarku, z tymi samymi zwolnieniami co TakeCaravan;
  CaravanGoods.Sell wola je z osada sprzedazy (jedna linia).
- Build Armoury po poprawkach: kod 0, 0 Error(s); gen_mcm (2066 ustawien) i build jeszcze raz.
