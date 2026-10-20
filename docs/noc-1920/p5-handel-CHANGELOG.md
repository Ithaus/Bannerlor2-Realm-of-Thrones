## p5-handel (20/21.10) - jarmarki po zniwach (R3-05a) i barki rzeczne (R3-05b) (Armoury)

**Dla Jeffa (prosto).** Po zniwach kazda kraina (Polnoc, Dorzecze, Zachod, Reach, Dorne, miasta Essos...) ma raz w roku dwutygodniowy jarmark w swoim
najbogatszym miescie - po kolei od poludnia na polnoc, od ok. 253. dnia roku do konca roku. W czasie jarmarku przyjezdza wiecej kupujacych, wiec miasto placi
lepiej za towary i zwierzeta: wozy wsi i karawany same tam ciagna, a Ty mozesz tam korzystniej sprzedac towar. Od kazdej sprzedazy wozu i karawany 1% idzie
do kasy miasta jako oplata targowa. Barki: wies i miasto nad ta sama rzeka (Trident, Mander, Miodowa, Czarny Nurt, Zielona Krew, Rhoyne) licza droge o polowe
taniej, wiec wozy i karawany chetniej handluja wzdluz rzek; na mapie nic sie nie zmienia - jada dalej droga.

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
    rosnie przez 2 tygodnie do ok. +36% i po jarmarku sam wraca. Budzet konsumpcji mieszczan bez zmian; uzbrojenie poza regula;
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
  - woz wsi: `MarketCarts.Choose` - czas kursu `2 x d x CartFactor / perDay + czekanie`, CartFactor = 0.5 dla wsi i miasta nad ta sama rzeka; zasieg dalej droga ladowa;
  - karawana: postfiks `BKCaravansBehavior.GetTradeScoreForTown` (DeclaredMethod, Harmony "armoury.p5handel.barges") - osada postoju/wyjazdu i miasto celu nad ta sama
    rzeka: wynik x (d + kara(d) + dom + 8) / (d/2 + kara(d/2) + dom + 8) - formula BK z polowa drogi (progi kary jak BK/BKROT ze sredniej odleglosci miast);
    wynik -1 (odciecie BKROT/IslandRoads) zostaje;
  - linie: "Barki rzeczne (R3-05b): start sesji" (plik, osad na mapie, braki, rzeki, wsi nad rzekami, latka) i "Barki rzeczne (R3-05b): dzien N".
- Wpiecia w plikach wspolnych (minimalne): `ArmouryBehavior.cs` - Reset w osobnym polu (`_p5HandelReset`), SessionStart x2, Daily x2 (po TownToll.Daily), SyncData
  `arm_fairs`; `TownToll.cs` - oplata w CartEnd/TakeCaravan; `MarketCarts.cs` - CartFactor w czasie kursu i licznik wyboru; `Settings.cs` - 2 pola; gen_mcm 2066 ustawien.

**Ryzyko / co sprawdzic (kontrola calosci).**
- Zamknieta ekonomia: jarmark nie tworzy zlota - wyzsze ceny placi kasa miasta (sprzedajacym) i kupujacy w miescie (miastu); oplata to przelew sakwa -> kasa miasta
  (najpierw zdjecie, wyjatek oddaje zloto). Barki nie przenosza niczego - zmieniaja tylko wybor celu.
- Kolizje: dopis popytu biegnie po wygladzaniu gry, przed UpdateDemandShift (FoodSubstitute, NoMakerBudget, KsHunger przesuwaja popyt potem - bez zmian); RawPrice
  (szacunek surowcow) nie jest ruszany - dopis liczony z wyniku. BK MakeConsumption/zamki: tylko miasta jarmarku. Oplata nie dubluje myta (osobna stawka, osobny
  licznik); "Myto targowe" liczy jak dotad tylko myto. Ocena BK: IslandRoads i BKROT bez zmian (wynik <= 0 pomijany).
- Przeplywy: jarmark sciaga wozy z zasiegu (250) i karawany - na 2 tygodnie inne miasta krainy moga dostac mniej (ruda, drewno); po jarmarku polka miasta jarmarku
  pelna i tania, karawany rozwoza. Obserwowac "Dowoz (wozy)" i "Ruda/drewno" w miastach w sezonie jarmarkow.
- Barki: rachunek, nie ruch - woz dalej jedzie caly kurs ladem, wiec magazyn wsi nad rzeka moze stac dluzej niz liczyl miernik (dlugi kurs do miasta rzecznego).
  Bledna para w pliku = dziwny handel - plik ma tylko pewne osady; Blackwater w ROT ma tylko Krolewska Przystan i Czerwona Twierdze (malo skutku).
- Prog zysku karawan T1 (`TownToll.CaravanRateFor`) nie zna oplaty 1% jarmarku (ceny na jarmarku sa wyzsze, wiec margines rosnie bardziej niz 1%).
- Starsza DLL pomija klucz `arm_fairs` (jarmark w toku przepada, nic wiecej).

**Status.** DO SPRAWDZENIA (build 0 bledow; bez testu w grze - zakaz uruchamiania w tym zadaniu).

**Decyzje (projektuje Claude).** Kraina = kultura miasta (FreeHands.RealmOf nie wolany - jego pamiec kraina sprzed kalibracji); jeden jarmark na kraine w roku, w
najbogatszym miescie; od poludnia na polnoc po oknie zniw (Szampania: rozlozone w roku); +40% szacunku popytu (cena ok. +20% na koniec); oplata 1% (Jeff/audyt
"np. 1%"); tylko towary handlowe i zwierzeta; Wielki Turniej przy jarmarku - NIE (nic ponad zadanie; GrandTourney osobno). Barki: polowa drogi (Masschaele x2),
tylko rachunek (opis Jeffa), wies przez swoja osade, zasieg wozu bez zmian; tylko osady pewne z kanonu (Goldengrove, Old Oak, Raventree, Wayfarer's Rest,
Pinkmaiden, Acorn Hall - pominiete jako niepewne).

**Test w grze (autotest 160 dob od nowej kampanii obejmuje sezon jarmarkow).**
- `Jarmarki po zniwach (R3-05a): start sesji` - "latka UpdateSupplyAndDemand wpieta", kalendarz z ok. 30 krainami;
- `Jarmarki po zniwach (R3-05a): dzien` - od dnia roku 253 "trwa" 1-5, "otwarte" z nazwami miast, "oplata targowa: wozy sprzedazy" > 0 i "karawany" > 0,
  "dopis w miastach" > 0; "potkniecia 0";
- `Barki rzeczne (R3-05b): start sesji` - "osad na mapie 30", bez "BRAK na mapie", "latka ocena miast BK wpieta", "wsi nad rzekami" ok. 90;
- `Barki rzeczne (R3-05b): dzien` - "w tym miasto nad rzeka wsi" > 0, "ocen miast z barka" > 0, "potkniecia 0";
- `Myto targowe (E3-G3)` - kontrola "roznica 0" jak dotad; `Karawany - towar tam, gdzie maja zysk (E3-T1)` - kontrola kas bez roznic.
