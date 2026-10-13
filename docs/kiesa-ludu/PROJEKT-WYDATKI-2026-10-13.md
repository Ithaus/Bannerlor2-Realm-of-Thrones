# PROJEKT A: LUD WYDAJE WEDLUG ZAMOZNOSCI (13.10.2026)

Praca projektowa bez kodu. Jeden z trzech niezaleznych kierunkow odpowiedzi na pytanie Jeffa z 13.10: "co zrobic z kasa, ktora lezy; w prawdziwej
ekonomii pieniadz cyrkuluje i ma zamkniety obieg - ludzie moga oszczedzac, ale tez wydaja, inwestuja itp. Jak to zrobic w ramach silnika gry, aby
mialo to sens?" Ten kierunek: **bogatszy lud zyje lepiej i wydaje nadwyzke na prawdziwy towar z prawdziwych polek.**

**Zrodla.** LOG = `D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_02-15-21.log` (bieg roczny 364 doby, WGRANIE 36; `:NNN` = numer
linii). ROZP = `docs\kiesa-ludu\ROZPOZNANIE-OBIEG-KIESY-2026-10-13.md`. TRIAZ = `docs\test-zbiorczy\TRIAZ-3-bieg-roczny-2026-10-13.md` (O1-7).
SRC = kod WGRANIA 36 (`...\scratchpad\zb\int\Armoury\src`). G = dekompilacja gry (`dec32\cs_full`), BK = dekompilacja BannerKings (`e4tools\bkfull`).
Skrypty tego projektu (do powtorzenia rachunku, katalog `...\dbc3d3d1-...\scratchpad\kiesa\`): `a1_goods.py` (tabela towarow z linii "Towary: dzien",
srednia z 28 dob), `a2_luxprice.py` (cena zbytu wyrobow warsztatow z linii "Warsztaty towarowe"), `a3_lokata.py` (rachunek lokaty).
Oznaczenia: [P] = pomiar z logu, [S] = szacunek z rachunku (nie z biegu). Kwoty w zl na dobe calego swiata, 1 zl = 1 pens, rok = 364 doby.
Bieg mierzyl gospodarke WOJENNA (25 z 30 krolestw w wojnie caly rok); pokoju nie zmierzyl.

---

## 0. Dziesiec zdan dla Jeffa

1. Lud nie wydaje nie dlatego, ze nie chce, tylko dlatego, ze gra nie pozwala mu kupic niczego ponad to, co juz bierze: dzis oferuje 3.6 mln zl na dobe
   na osiem towarow (mieso, piwo, wino, plotno...), ktorych polki sa puste, i nie kupuje za to ani sztuki wiecej.
2. Jednoczesnie zbytki tego swiata leza i tanieja: klejnotow na polkach miast przybylo w rok z ok. 100 do 1 000 sztuk, aksamitu z 10 do 280, karawany
   woza 750 klejnotow i 4 150 bel bawelny, ktorych nie maja gdzie sprzedac, a klejnot w miescie, ktore go wyrabia, spadl z 1.4 do 0.5 ceny historycznej.
3. Przyczyna jest jedna: cena i handel w grze nie widza sakiewki ludu - popyt miasta liczy sie z liczby mieszkancow, a karawanie wolno sprzedac miastu
   najwyzej "20 dni popytu", ktory dla zbytkow jest prawie zerem, chocby lud mial milion w kiesie.
4. Proponuje jedna regule zamiast dzisiejszej: lud trzyma zapas na ok. miesiac zwyklych zakupow, a z tego, co ma ponad zapas, wydaje co dobe jedna
   szescdziesiata (nadwyzka rozchodzi sie w dwa miesiace) na "liste dostatku": sukno i odzienie, srebro, klejnoty i korzenie, lepszy stol, sprzety
   domowe - zawsze z polki swojego miasta, po cenie targu; kazde 6 dni zapasu ponad prog to o 10% bogatszy koszyk.
5. Te oferte widzi targ: miasto z bogatym ludem ma prawdziwy popyt na te towary, cena w nim rosnie (najwyzej do poltorej ceny zwyklej - wiecej lud nie
   da), karawany maja po co tam jechac i wolno im tam sprzedac, a warsztaty zbytkow, z ktorych dzis pracuje 11 z 18 i 3 z 7, maja zbyt.
6. Do tego lud doklada do budowy swojego miasta kwote rowna cwierci tego, co przeznacza na towar: material z polki, robocizna dla wlasnych ludzi -
   dzis 198 z 227 budow stoi, bo placi je sam pan.
7. Rachunek: swiat wytwarza zbytkow i sukna za ok. 157 tys. zl na dobe (wedlug cen historycznych), lud placi za nie dzis ok. 85 tys.; zeby kiesy
   przestaly rosnac, musi wydawac o ok. 60 tys. zl na dobe wiecej (48-73 tys.) - to sie miesci po cenach NIZSZYCH niz historyczne (ok. 0.9), bez
   dodruku towaru i bez dodruku zlota.
8. Skutek w nowej kampanii: kiesy mieszczan staja na ok. 18 mln zamiast 36.5 mln (lokata ponad zapas 5-6 mln zamiast 23.8 mln); z kazdych 10 zl,
   ktore lud wyda z nadwyzki, 2.4 zl trafia wprost do panow, 2.5 zl do panow przez korone, 1.9 zl wraca od razu ludowi tego samego miasta jako
   robocizna, 1.8 zl dostaje lud miast, ktore towar wytwarzaja (dzis najbiedniejszych), 1.1 zl kupcy-notable i karawany, 0.3 zl idzie na kontrakty
   najemnikow i raty koron.
9. Uczciwie: to NIE odbuduje wojska - do panow dojdzie ok. 30 tys. zl na dobe (49 tys., gdyby z kies wychodzilo cale 80 tys.), czyli realnie
   0.9-1.3 tys. ludzi wiecej (gorna granica 3-4 tys.), a brakuje 31 tys.; pieniadz plynie do miast, ktore wytwarzaja, i do wlascicieli karawan,
   a pan miasta bogatego zyskuje niewiele; do tego cala rzecz stoi na cenach gry, wiec wymaga pomiaru na sucho przed wlaczeniem.
10. Za to nie wymaga zadnego nowego prawa ani podatku, dziala tak samo w pokoju i w wojnie, ozywia karawany i biedne miasta rzemieslnicze, daje graczowi
    prawdziwy handel zbytkami (kupic u wytworcy, sprzedac tam, gdzie lud ma pieniadze) i miasta, ktore same sie buduja - i odpowiada wprost na pytanie
    "na co lud ma wydawac".

**Pytania o KIERUNEK (tylko te trzy):** (1) czy bogaty lud ma kupowac zbytki - klejnoty, aksamit, futra (projekt zaklada: tak); (2) czy lud ma
dokladac z wlasnej kiesy do budow swojego miasta, takze do murow w wojnie (projekt zaklada: tak); (3) czy miasto ma fundowac sakiewke turniejowa
(paczka P5 - pomysl z listy "tylko na Twoje slowo", domyslnie NIE).

---

## 1. Dlaczego dzis nie wydaje - cztery liczby

| Co | Liczba | Zrodlo |
|---|---|---|
| Kiesy ludu miast w d364 | 36.50 mln (W 31.63 / R 3.51 / P 1.36), z tego "lokata" ponad zapas 10 dni: 23.78 mln; podzamcza 3.03 mln | LOG :129003 [P] |
| Oferta nadwyzki na dobe (15% lokaty) | 3.63 mln zl, a towar wziety z polek miast stoi na 417 tys. zl od d271 | LOG :129003; TRIAZ O1-7 pkt 2 [P] |
| Lista oferty | 8 kategorii: meat, beer, wine, linen, velvet, pottery, cheese, oil; zapas na polkach: piwo 0.2 doby, plotno 0.3, mieso 1.8 | SRC TownFolk.Live.cs:41; ROZP 1.3 pkt 3 [P] |
| Zbytki i sukno swiata (srednia d337-364, wartosc sztuki x sztuki) | produkcja 163 tys. zl/d (klejnoty 51, bawelna 50, filc 33, aksamit 22, futra, korzenie, srebro 6), mieszczanie biora z tego za 121 tys. wg wartosci; na polkach miast 4.4 mln, w jukach karawan 8.1 mln (klejnoty 747 szt., bawelna 4 151, filc 9 286, futra 899) | LOG "Towary: dzien" :128769-:128816, a1_goods.py [P] |

Trzy mechanizmy, ktore to tlumacza (wszystkie sprawdzone w dekompilacji):

- **Popyt targu = glowy, nie sakiewki.** Cena kategorii = (popyt / (0.1 x podaz + 0.04 x wartosc polki + 2))^0.6, w granicach 0.1-10
  (G DefaultTradeItemPriceFactorModel.cs:170-182). Popyt targu gra wygladza co dobe (85% starego + 15% szacunku; G DefaultSettlementEconomyModel.cs:53-59,
  G ItemConsumptionBehavior.cs:207-218), a szacunek to liczba ludzi BK x stala klasy + dobrobyt (BK BKEconomyModel.cs:494-554). Kiesa nie jest wejsciem.
- **Karawana sprzedaje miastu najwyzej 20 x popyt targu** (BK BKCaravansBehavior.cs:1358-1362: `if (num14 > demand * 20f) num14 = demand * 20f`), po jednej
  cenie za cala partie (:1355) i najwyzej za cala kase kupcow (:1371-1374). Dla zbytkow popyt targu jest drobny, wiec karawany nie maja jak sie
  rozladowac: w jukach 8.1 mln zl towaru, sprzedaz klejnotow 6.1 szt./d przy zakupie 9.8 (a1_goods.py) [P].
- **Oferta ludu idzie tylko w budzet zakupow, nie w popyt targu.** AddOffer dopisuje ja do slownika budzetu dnia (SRC TownFolk.Live.cs:380-399), a BK zdejmuje
  z polki najwyzej tyle, ile lezy (BK EconomyPatches.cs:514-560). Pusta polka = pieniadz zostaje w kiesie (PLAN-KODU-E5B1.md:40-42).

Skutek widac w cenach: klejnot w miastach, ktore go wyrabiaja, 3 366 zl (1.40 wartosci) w pierwszych 28 dobach -> 1 196 zl (0.50) w ostatnich 28;
aksamit 8 083 zl (2.02) -> 3 481 zl (0.87); warsztatow zlotniczych pracuje srednio 10.6 z 18, tkalni aksamitu 2.8 z 7 (LOG "Warsztaty towarowe",
ostatnia :128727, a2_luxprice.py) [P]. Zbytki tanieja i zalegaja, bo ten, kto ma pieniadze, nie moze o nie zabiegac.

---

## 2. Reguly prostym jezykiem

**R-A1. Zapas.** Lud trzyma w kiesie zapas na czarna godzine: wyrobnicy 10 dni pelnego koszyka po cenach swojego miasta, rzemieslnicy 21, rodziny
kupieckie 30, podzamcze 10, wies 15 dni zakupow (bez zmian - SRC TownFolk.cs:54, Settings.cs:1613). Dla wyrobnikow calego swiata to 9.9 mln zl
(31.63 mln / 31.9 dnia x 10) = ok. 29 dni tego, co dzis naprawde wydaja na towar (344 tys. zl/d - LOG :129003). Historycznie wyrobnik nie mial
w skrzyni wiecej niz kilka tygodni zycia (ROZP 5 pkt 2).

**R-A2. Dostatek (zamiast dzisiejszej "oferty 15%").** Z tego, co klasa ma PONAD zapas, przeznacza co dobe 1/60 na liste dostatku (klucz
`FolkSurplusSpendDays` = 60). Lista i podzial (wagi = udzial w produkcji swiata wg wartosci, tabela 4.1):

| Grupa | Kategorie | Waga |
|---|---|---:|
| sukno i odzienie | felt, cotton, velvet, linen, leather, fur, garment | 55% |
| srebro, klejnoty, korzenie | jewelry, silver, spice, walrus_tusk | 25% |
| lepszy stol | meat, cheese, butter, beer, wine, mead, oil, honey | 12% |
| dom | pottery, whale_oil, Papyrus | 8% |

W grupie podzial jak dzis - proporcjonalnie do budzetu gry dla kategorii (Live.cs:384-397). Zakup idzie zwykla droga: tick konsumpcji BK zdejmuje
sztuki z polki miasta po cenie targu, lud placi z kiesy (PayForGoods, Live.cs:448-503). Czego nie ma na polce - tego lud nie kupuje, pieniadz zostaje
w kiesie i jutro znow jest w ofercie. Koszyk rosnie plynnie z zamoznoscia: kazde 6 dni zapasu ponad prog to +10% dziennego wydatku (6/60).
Rzemieslnicy i rodziny kupieckie jak dzis: 70% swojej nadwyzki wyplacaja jako place, 30% na towar (Live.cs:814-828) - w nowym tempie.
Uslugi rosna same: czynsz 8% i leczenie 1% licza sie od tego, co klasa zaplacila za towar (Live.cs:787-791).

**R-A3. Targ widzi sakiewke.** Dzienna oferta ludu na kategorie NIEZYWNOSCIOWA z listy (sukno bez plotna i skory; srebro, klejnoty, korzenie; dom) jest
dopisywana do popytu targu tego miasta - tego samego, z ktorego gra liczy cene i z ktorego karawana liczy, ile wolno jej sprzedac. Lud ma cene graniczna:
jego oferta nie podnosi ceny kategorii ponad 1.5 ceny zwyklej (klucz `FolkLadderPriceCeiling` = 1.5), a gdy cena jest wyzsza z innego powodu, lud tej
kategorii z nadwyzki nie kupuje. Zywnosci, piwa, plotna i skory (wsad zbrojowni i wojska) sygnal nie dotyczy - pieniadz ludu nie podbija ceny chleba
ani zbroi.

**R-A4. Podzamcza i wsie - ta sama regula na targu swojego miasta.** Podzamcze i wies nie maja wlasnego targu: nadwyzke ponad zapas (10 / 15 dni)
wydaja w tym samym tempie 1/60 w miescie targowym, z tej samej listy, ta sama droga co dzisiejsze zakupy wsi (BuyGoods, Live.cs:964-998); ich oferta
dolicza sie do popytu tego miasta jak w R-A3.

**R-A5. Lud buduje swoje miasto.** Oprocz zakupow lud wklada co dobe w budowe stojaca na czele kolejki kwote rowna cwierci swojej oferty na towar
(1/240 nadwyzki; klucz `FolkWorksShare` = 0.25): 25% material z polki (wapien, drewno, glina, narzedzia, marmur, deski), 75% place murarzy i woznicow
- te same stawki i ta sama metoda co budowa pana (SRC BuildFunding.cs:121-202; punkt 24 zl cywilny / 48 zl wojskowy, Settings.cs:566-568). Nie ma
budowy albo materialu - pieniadz zostaje w kiesie.

**P5 (tylko na slowo Jeffa). Sakiewka turniejowa.** Miasto-gospodarz doklada do nagrody turnieju jedna dzienna oferte swojego ludu; dostaje ja
zwyciezca (glowa rodu, tak jak dzis cene nagrody z kasy kupcow - SRC RealPayer.cs:170-181, G TournamentManager.cs:180-194).

Czego NIE proponuje i dlaczego: wyzszej akcyzy od zbytkow (to nowe prawo - obszar innego kierunku), "uslug" jako osobnego ujscia (w modelu to
przelew wewnatrz ludu: W -> R/P -> place -> W; pieniadz nie wychodzi), dosypywania towaru (kazda sztuka jest z polki).

### 2.1 Parametry z rachunkiem

- **Tempo 1/60.** Dzis 15%/d daje oferte 3.63 mln zl/d wobec 163 tys. zl/d towaru - liczba bez pokrycia. Rachunek lokaty (a3_lokata.py):
  dL/dt = S0 - 0.81 x fi x L / T. S0 = przyrost lokaty bez reguly, z linii "Kiesa ludu" w oknach 30 dob: 141, 80, 52, -1, 72, 93, 56, 57, 71, 49, 59,
  39 tys. zl/d (srednia roku 65, ostatnie 90 dob 49) [P]; 0.81 = czesc wydatku, ktora NIE wraca od razu do ludu tego samego miasta (4.3);
  fi = jaka czesc oferty znajduje towar (nieznane - zmierzy P0).

  | T | fi | Nowa kampania: lokata w szczycie / w d364 | Stary zapis: lokata 23.8 mln -> po roku / po 2 latach | Wydatek dodatkowy, srednia roku (nowa kampania) |
  |---:|---:|---|---|---:|
  | 60 | 0.4 | 9.6 / 9.2 mln | 11.1 / 9.4 mln | 48 tys./d |
  | **60** | **0.7** | **6.4 / 5.6 mln** | **5.8 / 5.2 mln** | **60 tys./d** |
  | 60 | 0.9 | 5.2 / 4.3 mln | 4.3 / 4.0 mln | 64 tys./d |
  | 91 | 0.7 | 8.6 / 8.2 mln | 9.5 / 8.0 mln | 51 tys./d |
  | 120 | 0.7 | 10.5 / 10.1 mln | 12.8 / 10.8 mln | 44 tys./d |

  T = 60 trzyma lokate ponizej 10 mln takze wtedy, gdy towar znajdzie tylko 40% oferty; najwyzsza oferta nowej kampanii to ok. 107 tys. zl/d
  (6.4 mln / 60), czyli razem z dzisiejszymi 85 tys. miesci sie w 157 tys. x 1.5 = 235 tys. zl/d (pulap ceny). [S]
- **Cena graniczna 1.5.** Bez niej wzor gry przy pustej polce daje indeks do 10: kupcy placa karawanie wielokrotnosc wartosci za cala partie (jedna cena
  za partie, BK :1355), a po dostawie cena spada - strata kupcow, czyli pana. Wzor gry w dobach popytu: zapas na 7 dob = cena zwykla
  ((1 / (0.04 x 7 + 0.1 x 7))^0.6 = 1.01; to samo RawPrice.cs:42-45), zapas ponizej ok. 3.5 doby = pulap 1.5. Srednio zbytki mieszcza sie nizej:
  (85 + 60) / 157 = 0.92 w rownowadze i w nowej kampanii; na starym zapisie pierwszy kwartal stoi na pulapie (oferta 150-400 tys. zl/d). [S]
- **Cwierc na budowe.** Przy lokacie 5.2 mln to 5.2 mln / 240 = 22 tys. zl/d robot (pan placi dzis 13.7 tys. zl/d - ROZP 4 pkt 1), czyli ok. 900
  punktow budowy na dobe wobec 319 dzis. Wiecej nie ma sensu: material budow (drewno, glina, wapien, narzedzia, marmur, deski) to ok. 9.6 tys. zl/d
  produkcji wg wartosci i 484 tys. zl na polkach (a1_goods.py) - przy 25% materialu wystarcza najwyzej na ok. 40 tys. zl/d robot.

---

## 3. Jak to siedzi w silniku i w kodzie

Nowego systemu nie ma: zmienia sie jedna istniejaca regula (krok 5 doby ludu) i dochodza dwa wpiecia w istniejace mechanizmy.

| Paczka | Plik i metoda | Co sie zmienia |
|---|---|---|
| P0 pomiar | `TownFolk.Live.cs` AddOffer (:380-399), BuildLines; `TownFolk.Probe.cs` ConsumePostfix (:252-306, slownik `_brTrack`) | tylko log: na kategorie - oferta, kupione z oferty (wydatek wiersza ponad budzet gry), wartosc polki, indeks ceny; "na sucho" popyt i indeks po R-A3; marza kupcow na towarze od karawan; lista miast z warsztatami zbytkow; sprzedaze karawan przyciete regula "20 x popyt"; oferta bez towaru wedlug miasta |
| P1 dostatek | `TownFolk.Live.cs`: `BetterIds` (:41) -> tabela grup z wagami; krok 5 TownLive (:809-829), zamek (:890-893); `TownFolk.Tithe.cs` SpendRate (:41); `Settings.cs` (:1628 `FolkSurplusSpendShare` zostaje dla wylacznika, nowy `FolkSurplusSpendDays`); Predict (:680-688) i udzialy klas (:737) | tempo 1/T, lista i wagi; POPRAWKA: Predict i Share licza dzis oferte OFEROWANA, nie kupiona (9.9) |
| P2 targ widzi sakiewke | `RawPrice.cs` EstimatePostfix (:137-151; juz dzis podmienia szacunek popytu dla 7 surowcow) - dopisek `TownFolk.OfferDemand(town, cat)`; pulap przez wlasny wzor modelu (`GetBasePriceFactor`); AddOffer pomija kategorie z indeksem ponad pulap | popyt targu = szacunek gry + oferta ludu, wsi i podzamczy tego targu na kategorie, obciety tak, by indeks nie przekroczyl 1.5 |
| P3 podzamcza i wsie | `TownFolk.Live.cs` Villages (:910-945), ShelfOf / BuyGoods (:948-998), CastleLiveDay (:890-893) | podzamcze kupuje w miescie targowym jak wies; lista wsi z nadwyzka = lista dostatku; tempo 1/T |
| P4 budowa | `BuildFunding.cs` Daily (:121-202): drugi platnik w tej samej petli; `TownFolk` - zdjecie z kies klas | 1/240 nadwyzki na budowe z czola kolejki; wojna nie wstrzymuje robot ludu (dzis `_warSkipped`, :142) |
| P5 (na slowo) | `RealPayer.cs` TourneyPre (:170-181) | drugi platnik nagrody: kiesa ludu gospodarza |

**Co sprawdzilem w dekompilacji (kazda rzecz, na ktorej projekt stoi):**

1. BK zdejmuje z polki `budzet / cena`, najwyzej stan polki, i dopisuje kasie `sztuki x cena` (BK EconomyPatches.cs:514-555); budzet wiersza =
   popyt x indeks^0.15 + dobrobyt / 1000 / indeks (:1085-1100). AddOffer juz dzis przelicza zlote oferty na te jednostke (Live.cs:392-394).
2. Slownik budzetu dnia ma WSZYSTKIE kategorie (G ItemConsumptionBehavior.cs:178-184) - zbytki tez, wiec oferta ma dokad trafic.
3. Popyt targu: `UpdateSupplyAndDemand` (G ItemConsumptionBehavior.cs:207-218) bierze `GetEstimatedDemandForCategory` (G DefaultSettlementEconomyModel.cs:91-94;
   BK tej metody nie nadpisuje - w BKEconomyModel.cs jej nie ma) i wygladza 85/15 (:53-59; BK tylko przycina podaz, BKEconomyModel.cs:556-565). Postfiks na
   tej metodzie juz mamy (RawPrice.cs:137, wpinany na kazdy model, ktory ja deklaruje - :443-453). Zmiana wchodzi i wychodzi w ok. 3 tygodnie, bez skoku.
4. Karawany BK (BK zastepuje tik gry: `CaravansCampaignBehavior_HourlyTickParty_Skip`): cel wedlug `GetPriceFactor(kategoria) - srednia swiata x prog`
   (BKCaravansBehavior.cs:1239-1250), sprzedaz gdy cena miasta / srednia >= ok. 1.1, ilosc w zlocie najwyzej `popyt targu x 20` (:1343-1362), zakup tam,
   gdzie indeks ponizej sredniej, najwyzej 1 500 zl na kategorie na wizyte (:1485-1494, :1601-1608). Wszystkie trzy wejscia (cena, popyt, srednia) reaguja
   na R-A3. Ten sam ksztalt ma kod gry (G CaravansCampaignBehavior.cs:971-1058, :1134-1216).
5. Warsztat towarowy rusza cykl tylko, gdy utarg wedle ceny miasta > wsad + place (SRC WorkshopTrade.cs, opis klasy pkt 2) - wyzsza cena zbytu = wiecej
   pracujacych warsztatow; nadwyzke ceny ponad koszt x 1.25 zatrzymuje kasa kupcow ("cena sprawiedliwa", pkt 6) i oddaje zaworem panu i koronie.
6. Zbytki w BK to silver, velvet, jewelry, spice, fur; "przemyslowe" cotton, leather, pottery, linen, tools, flax, wool (BK Helpers.cs:574-589) - ta sama
   piatka jest juz w kodzie jako `LuxIds` (SRC TownFolk.Probe.cs:34).
7. Zakup podnosi zadowolenie BK danej grupy o 0.001 na wiersz polki (BK EconomyPatches.cs:547-550), a zadowolenia wchodza do stabilnosci i dobrobytu
   (BK BKStabilityModel.cs:149, BKProsperityModel.cs:59, :195) - skutek uboczny 9.6.
8. Nagroda turnieju AI: gra daje glowie rodu zwyciezcy cene nagrody (G TournamentManager.cs:186-193); u nas placi miasto-gospodarz z nadwyzki kasy
   (RealPayer.cs:34, :170-181) - w roku 54 416 zl z 80 716 naleznych (LOG "Szczelnosc (164a)", suma 364 linii, ostatnia :129016) [P].

NIE sprawdzone: w ktorych miastach stoja warsztaty zbytkow i czyje sa wsie bawelny (log nie podaje - pomiar P0); cena bawelny (brak linii); ile sztuk
`garment` lezy na polkach (ksiega towarow nie obejmuje odziezy - ROZP 1.3 pkt 4).

---

## 4. Przeplywy z liczbami

### 4.1 Czy towaru starczy (wszystko wedlug wartosci sztuki = ceny historycznej; srednia d337-364, a1_goods.py) [P]

| Towar (wartosc) | Produkcja szt./d | Lud bierze szt./d | Produkcja zl/d | Polki miast | Juki karawan | Cena zbytu u wytworcy (a2_luxprice.py) |
|---|---:|---:|---:|---:|---:|---|
| jewelry (2 400) | 21.3 | 19.3 | 51 086 | 1 007 szt. = 2.42 mln | 747 szt. = 1.79 mln | 0.50 wartosci (start 1.40) |
| cotton (1 000) | 50.4 | 42.8 | 50 429 | 683 = 0.68 mln | 4 151 = 4.15 mln | brak pomiaru |
| felt (200) | 165.9 | 84.7 | 33 179 | 671 = 0.13 mln | 9 286 = 1.86 mln | 1.09 |
| velvet (4 000) | 5.6 | 3.8 | 22 286 | 283 = 1.13 mln | 4 szt. | 0.87 (start 2.02) |
| fur, spice, silver, walrus_tusk | - | - | 6 052 | 0.07 mln | 0.25 mln | - |
| **zbytki i sukno razem** | | | **163 tys.** | **4.4 mln** | **8.1 mln** | |
| linen, leather, pottery, whale_oil, Papyrus | - | - | 19 124 | 0.03 mln | 0.02 mln | plotno 1.78 |
| lepszy stol (meat, cheese, butter, beer, wine, mead, oil, honey) | - | - | 22 875 | 0.05 mln | 0.09 mln | wino 2.96, oliwa 2.32 |

Rachunek pojemnosci (cztery glowne towary: 157 tys. zl/d wg wartosci): lud placi za nie dzis ok. 85 tys. zl/d (klejnoty 46.4 tys. x 0.50 + aksamit
15.3 x 0.87 + filc 16.9 x 1.09 + bawelna 42.8 x ok. 0.7 [S - cena bawelny przyjeta]). Miejsce na dodatkowy wydatek: **72 tys. zl/d przy cenie
historycznej, 150 tys. przy cenie granicznej 1.5**, plus jednorazowo ok. 12.5 mln zl towaru, ktory juz lezy (polki 4.4 + karawany 8.1). Do tego
nieczynne warsztaty: srednio 7 z 18 zlotnikow i 4 z 7 tkalni aksamitu (w d364 10 i 5 - :128727) ruszaja, gdy cena zbytu wroci ponad koszt.
Potrzeba: ok. 60 tys. zl/d (48-73 tys.). **Starczy bez wyjscia ponad cene historyczna (sredni indeks ok. 0.92).** [S]

### 4.2 Kiesy ludu: dzis i w rownowadze

| Pozycja (lud miast i podzamczy, zl/d) | Dzis, koniec roku [P] (ROZP 1.1) | Rownowaga z regula [S] |
|---|---:|---:|
| Wplywy | 524 782 | ok. 547 tys. |
| Wydatki | 486 462 | ok. 547 tys. |
| - w tym dodatkowy wydatek na liste dostatku | 0 (oferta nie kupuje) | ok. 60 tys. (48-73) |
| - w tym roboty ludu (material do kupcow / place wracaja do ludu) | 0 | ok. 5 tys. / 17 tys. |
| Przyrost kies | +38 320 (lokata +49 tys., reszta kies -11 tys.) | **ok. 0** |
| Kiesy ludu miast | 36.5 mln i rosna | **ok. 18 mln** (poza lokata 12.8 mln + lokata 5.2 mln) |
| Lokata ponad zapas | 23.8 mln = 24 dni wydatkow W (TRIAZ O1-7) | ok. 5 mln = 5 dni (3-11 mln w calym przedziale fi i S0) |

Rachunek rownowagi: lokata stoi, gdy to, co naprawde wychodzi z kies bogatych miast, rowna sie doplywowi: 0.81 x E = S0, czyli E = 49 / 0.81 = 60 tys.
zl/d (dla S0 39-59 tys.: 48-73 tys.), a lokata = E x 60 / fi = 5.2 mln przy fi 0.7. Wplywy calego ludu rosna o 37% x 60 = 22 tys. (robocizna u
kupujacych 19%, place u wytworcow 18%), wydatki o 60 tys.; reszte roznicy zamyka dzisiejszy spadek kies biednych miast (-11 tys./d), ktory place
u wytworcow zatrzymuja. Kiesy: W poza lokata 31.63 - 23.78 = 7.85 mln + R 3.51 + P 1.36 + gminy 0.12 = 12.8 mln, plus lokata.
**Nowa kampania (cel Jeffa):** lokata w ogole nie narasta do 23.8 mln - szczyt ok. 6.4 mln kolo doby 270, w d364 ok. 5.6 mln; kiesy miast w d364 ok. 18 mln.
Z 83 tys. zl/d, ktore w tym biegu przybywaly w kiesach miast (ROZP 1.1), 18 tys. to dopelnianie zapasu (6.4 -> 12.7 mln - to sa te "rozsadne
oszczednosci" i one zostaja), 65 tys. to lokata; z regula ok. 50 tys. z tych 65 wraca do obiegu (lud wydaje 60 tys., 11 tys. wraca mu od razu
jako robocizna), ok. 15 tys./d zostaje jako mala lokata pierwszego roku. [S]

### 4.3 Dokad idzie zloty wydany na liste dostatku

Udzialy z regul, ktore juz dzialaja (kazda z kodu i z linii logu):

- przy zakupie ludu: akcyza 2% do gminy (Settings.cs:1612), robocizna handlu 4% zbytki / 10% towar / 20% jedzenie i obrot rodzin kupieckich 3% - z kasy
  kupcow do ludu (Settings.cs:1605-1609; LOG :129004: robocizna 153 812, obrot P 32 113);
- przy sprzedazy i zakupie karawany w miescie: 10% obrotu karawany do ludu (`CaravanLab`, TownFolk.cs:62; Live.cs:773-774);
- w miescie wytworcy: place w wyrobach do 35% wartosci wyrobu po cenie targu (Settings.cs:1608; LOG :129004 doplata 76 924), warsztat dostaje koszt x 1.25,
  reszte ceny zatrzymuje kasa kupcow (LOG :128727: z utargu 78 222 warsztaty 25 945, kasy 52 277; srednia 28 dob: zlotnicy 98% marzy, aksamit 54%, filc 30%);
- zysk kupcow: zawor 7% nadwyzki kasy dziennie - panu 53.1%, koronie 42.5%, ludowi 4.5% (LOG :128914: 119 168 / 95 469 / 10 003), i jest zabierany
  w calosci (pobrano 240 952 z doplywu netto 225 516, tamze);
- utarg wsi: pan 35.9%, majatki BK 9.1%, wies 55%, a kiesy wsi w wojnie oddaja koronie danine (LOG :129026; :128983: korona 61.2% wplywow wsi);
- korona wydaje wplywy dnia tego samego dnia: zwrot zoldu 216 930 + renty 159 280 z 434 203 do wydania (LOG :128862); kazdy dodatkowy zloty po zwrocie
  idzie w renty wedlug lenn.

Trzy drogi towaru i ich podzial [S - rachunek z powyzszych udzialow; dla towaru od karawany przyjeta cena u wytworcy = 0.6 ceny u kupujacego]:

| Droga (udzial w wydatku) | Lud kupujacego miasta | Lud miasta wytworcy | Panowie wprost | Korona | Notable, karawany, majatki |
|---|---:|---:|---:|---:|---:|
| wyrob warsztatu przywieziony karawana (45%) | 20% | 27% | 16-19% | 15-17% | 17-19% |
| wyrob warsztatu z wlasnego miasta (20%) | 51% (razem z placami) | - | 22% | 22% | 5% |
| towar wsi z taboru: bawelna, futra, nabial (35%) | 15% | - | 31% | 48% (w pokoju ok. 0 - zostaje wsi) | 8% |
| **razem** | **19%** | **18%** | **24%** | **28%** | **11%** |

Z 28% korony ok. 25 punktow idzie tego samego dnia do rodow (renty, zwrot zoldu), ok. 3 punkty na kontrakty najemnikow i raty.

### 4.4 Ile z 80 tys. zl/d trafia do kogo i ile to wojska

Dwa rachunki: (A) gdyby z kies bogatych miast wychodzilo cale 80 tys. zl/d - tyle, ile w tym biegu srednio w nich przybywalo; lud musialby wtedy
wydawac 80 / 0.81 = 99 tys. zl/d; (B) to, czego sie naprawde spodziewam w nowej kampanii i w rownowadze: wydatek 60 tys. zl/d (4.2). [S]

| Odbiorca | (A) z 80 tys. netto | (B) nowa kampania i rownowaga | Droga |
|---|---:|---:|---|
| Panowie wprost | ok. 24 tys. | ok. 14 tys. | zawor kas kupcow (pan miasta wytworcy i kupujacego), podatek wsi 35.9%, myta od karawan, farma gmin |
| Korona -> rody | ok. 25 tys. | ok. 15 tys. | pol zaworu, danina wojenna kas i wsi, clo -> renty wedlug lenn (180) i zwrot zoldu (165) |
| Lud miast, ktore wytwarzaja | ok. 18 tys. | ok. 11 tys. | place w wyrobach (35% wartosci), robocizna przy karawanach |
| Kupcy-notable, karawany, wlasciciele warsztatow i majatkow | ok. 11 tys. | ok. 7 tys. | zysk karawan i 25% marzy warsztatu; po zapelnieniu pasma kies notabli (10 500 zl) nadwyzka idzie do kasy miasta i dalej zaworem |
| Kontrakty najemnikow, raty koron | ok. 3 tys. | ok. 2 tys. | wydatki koron |
| (lud tego samego miasta - wraca od razu) | (19 tys. z 99) | (11 tys. z 60) | robocizna, obrot, akcyza |
| **Razem do panow** | **ok. 49 tys. zl/d** | **ok. 30 tys. zl/d** (24-36) | |

Przelicznik TRIAZ O1-7 pkt 5: 10 tys. zl/d u panow = realnie 0.3-0.45 tys. ludzi w partiach (na pulapie zoldu jest 117 z 332 rodow), gorna granica
1.0-1.3 tys.

| Przypadek | Do panow | Wojsko realnie | Gorna granica |
|---|---:|---:|---:|
| (B) nowa kampania i rownowaga | ok. 30 tys. zl/d | **+0.9 do +1.3 tys. ludzi** | +3.0 do +3.9 tys. |
| (A) cale 80 tys. wychodzi z kies | ok. 49 tys. zl/d | +1.5 do +2.2 tys. | +4.9 do +6.4 tys. |
| stary zapis, pierwszy rok (lokata 23.8 mln schodzi do 5.8 mln; wydatek sr. 122 tys./d) | ok. 60 tys. zl/d | +1.8 do +2.7 tys. | +6.0 do +7.8 tys. |
| w pokoju (bez daniny wojennej wsi) | ok. 2/3 tego | - | - |

Drugi obieg (lud miast wytworcow wydaje nowy zarobek na chleb, zolnierz wydaje zold w miescie: 25% uslugi dla ludu, reszta kupcom) doklada ok. 30% [S].
Wojsko spadlo o 31 tys. (93 -> 62 tys.) - ten kierunek sam tego nie odrobi i nie obiecuje tego.

### 4.5 Gdzie osiada pieniadz, ktory przestaje lezec

[S] Najpierw u tych, ktorzy w tym roku stracili: karawany (kasy 3.77 -> 2.29 mln; notable doplacaja im do zoldu 56.5 tys. zl/d - LOG :128981) i notable
(28.9 -> 22.5 mln; pasmo gry miesci jeszcze 12.8 mln = 3 369 x 10 500 - 22.53 mln, :128727); potem przez kasy kupcow do panow i koron, przez zold do
sakiewek zolnierzy (24.7 dnia zoldu, :128980) i z powrotem do miast. Jesli kompanie najemne dalej zbieraja ok. 49 tys. zl/d bez wyjscia (TRIAZ O1-4),
to tam ten pieniadz skonczy - kierunek A, tak jak kazdy inny, wymaga poprawki O1-4.

---

## 5. Dowod zamknietego obiegu

Kazdy nowy albo zmieniony ruch ma platnika, odbiorce i polke; nic nie powstaje i nic nie znika.

| Ruch | Platnik -> odbiorca | Towar | Kod (istniejacy) |
|---|---|---|---|
| Zakup z listy dostatku w miescie | kiesa klasy -> kasa kupcow 98% + gmina 2% (akcyza) | sztuki zdjete z polki miasta przez tick BK | PayForGoods (Live.cs:448-503); BK EconomyPatches.cs:551-555 |
| Zakup podzamcza i wsi w miescie targowym | kiesa podzamcza / `Village.Gold` -> kasa kupcow | sztuki z polki miasta targowego (ponad 5 ostatnich) | BuyGoods (Live.cs:964-998): `ChangeGold(-koszt)`, `town.ChangeGold(+)`, `roster.AddToCounts(-n)` |
| Material budowy ludu | kiesy klas -> kasa kupcow | sztuki z polki (ShelfBuy, kazda po swojej cenie) | BuildFunding.BuyMaterials (:239-260) |
| Place budowy ludu | kiesy klas (oszczedzajacy) -> kiesy W 60% / R 40% (pracujacy) | punkty budowy = zaplacone / cena punktu | TownFolk.Earn(KBuildLabour) (Live.cs:156-173), PointsFor (BuildFunding.cs:209-212) |
| Sygnal popytu (R-A3) | brak - zmienia sie tylko liczba "popyt" w danych targu | brak | jak RawPrice (B): "sama cena niczego nie tworzy" (RawPrice.cs:54-55) |
| Sakiewka turniejowa (P5) | kiesy klas gospodarza -> glowa rodu zwyciezcy | brak | kontekst RealPayer (:170-181) |

Wszystko dalej to drogi, ktore juz sa w ksiedze pieniadza: robocizna i obrot z kasy do ludu, zaplata karawanom i taborom, warsztaty, zawor, myta, podatek
wsi, danina, renty. Kontrole bez zmian: "Kiesa ludu: zmiana kies = wplywy - wydatki, r = 0" (:129003), "Pieniadz swiata (bilans)" (:128971),
"Towary (bilans)" (:128818). Oferta niewydana nie jest przelewem - zostaje w kiesie (jak dzis).

---

## 6. Miasta biedne, zamki, wsie

- **Biedne miasta (31 miast ma 1.5% zlota ludu; 15 nie stac na pelny koszyk, 10 na chleb - ROZP 2).** Regula niczego im nie zabiera: oferta jest tylko
  z nadwyzki ponad zapas, wiec miasto ponizej zapasu nie wydaje nic ponad chleb. Sygnal popytu nie dotyczy zywnosci, wiec cena chleba sie nie rusza.
  Zysk biednych jest posredni i tylko dla tych, ktore cos wytwarzaja: place w wyrobach (35% wartosci), 10% obrotu karawan, wiecej pracujacych
  warsztatow. Wyspy i kresy bez rzemiosla i bez karawan (Karhold, Hardhome, Sisterton) nie zyskuja nic - tego ten kierunek nie rozwiazuje.
- **Bogate miasta, do ktorych karawany nie docieraja.** Pieniadz, ktory nie znajdzie towaru, zostaje w kiesie (uczciwie: "jesli nie ma, to nie ma");
  roboty ludu ida z miejscowego drewna i gliny, a cena 1.5 wartosci przyciaga karawany. Ile takich miast - pokaze P0 ("oferta bez towaru wedlug miasta").
- **Zamki (130 podzamczy, 3.03 mln; 8 najwiekszych ma 42.8% - ROZP 2).** Zamek nie ma targu (gra nie prowadzi mu popytu - RawPrice.cs:165-169), wiec
  podzamcze z nadwyzka kupuje w najblizszym miescie jak wies (R-A4) i doklada do budowy swojego zamku (R-A5; material z miasta targowego, jak dzis u pana -
  BuildFunding.cs:168). Podzamcza ponizej zapasu (60 ma mniej niz dzien koszyka) - bez zmian, karmi je kuchnia pana.
- **Wsie (571, 10.2 mln; chca kupic za 1.46 mln zl/d, kupuja za 4.6 tys. - 500 wsi "bez towaru", :129003).** Ta sama regula i lista; ich oferta dolicza
  sie do popytu miasta targowego, wiec karawany maja powod wiezc sukno i garnki takze do miast otoczonych zamoznymi wsiami. W wojnie nadwyzke wsi
  i tak sciaga danina (2.5%/d), w pokoju - ktorego bieg nie zmierzyl - to bedzie jedyne ujscie ich kies.

---

## 7. Gracz

Te same reguly, bez wyjatkow:

- **Jego miasta i zamki.** Lud wydaje i buduje jak wszedzie. Gracz jako pan dostaje to samo co pan AI: swoja czesc zaworu kasy kupcow, myta, farme gminy,
  podatek od zarobku ludu. Roboty ludu posuwaja budowe z czola JEGO kolejki bez jego zlota - on wybiera, co stoi na czele (przy wylaczonym
  `PaidConstructionPlayer` jego lenna ida starym modelem gry i roboty ludu ich nie dotycza - BuildFunding.cs:40-45).
- **Handel.** Cena zbytkow w bogatym miescie odpowiada prawdziwemu popytowi, wiec trasa "tanio u wytworcy, drogo tam, gdzie lud ma pieniadze" dziala
  dla niego tak jak dla 439 karawan AI; placi mu kasa kupcow, wiec sprzeda tyle, ile kasa ma (jak dzis). Zysk znika, gdy karawany wyrownaja ceny.
- **Jego warsztaty i karawany.** Warsztat zlotniczy czy tkalnia aksamitu ruszaja czesciej, ale zysk warsztatu ogranicza "cena sprawiedliwa" (koszt x 1.25)
  - renta z drogiego wyrobu idzie do kasy miasta, czyli do pana miasta. Jego karawany zarabiaja na roznicy cen jak karawany notabli.
- **Jego dlug i inwestycje.** Bez zmian - ten kierunek nie rusza Banku ani drabiny dlugu.
- **Turniej (P5, jesli Jeff zechce).** W bogatym miescie nagroda ma sakiewke od mieszczan - takze dla niego.

---

## 8. Co widzi gracz (napisy po angielsku)

Okno "People of this land" (PeopleMenu, zdanie obok "Bread: ..." - TownFolk.Live.cs:1463-1508), liczby z ostatniej doby osady:

- miasto z nadwyzka: `Savings: the townsfolk hold coin for 26 days of living. Above 10 days they spend it - 1,420 coins a day on cloth, silver and a better table; 380 found no goods to buy.`
- miasto przy zapasie: `Savings: the townsfolk hold coin for 8 days of living - nothing to spare.`
- budowa: `Works: the townsfolk pay 350 coins a day toward the Granary.` albo `Works: the townsfolk would pay 350 coins a day, but there is no stone or timber at the market.`
- wies i podzamcze: `Savings: the villagers hold coin for 31 days of their purchases; they shop at the market of Riverrun.`

Dymek mocy budowy (dzis jedna linia `Paid works (wages and materials from the owner's purse)` - BuildFunding.cs:28): druga linia `Town works (paid by the townsfolk)`.
Ceny na ekranie handlu pokazuja skutek bez dodatkowych napisow. Ustawienia MCM (po `gen_mcm.py`): `Folk Spend Surplus In Days`, `Folk Ladder Price Ceiling`,
`Folk Works Share`, wylacznik `Folk Buy By Wealth`. P5: komunikat `The townsfolk of Riverrun put up a purse of 2,300 coins for the champion.`

---

## 9. Skutki uboczne, ryzyka i slabosci (uczciwie)

1. **Kupcy bogatych miast moga tracic na towarze od karawan.** Kasa placi ludowi 10% obrotu karawany przy dostawie i 7-13% przy sprzedazy ludowi, a roznica
   miedzy cena skupu i sprzedazy w grze to ok. 4%; do tego karawana sprzedaje partie po cenie sprzed dostawy, a lud kupuje po cenie po dostawie (BK :1355).
   Na sprowadzanym zbytku kasa kupujacego miasta moze tracic kilkanascie procent - czyli pan miasta BOGATEGO dostaje z zaworu mniej, nie wiecej. Dzis
   caly handel karawan to 117 tys. zl/d (:128976) i 35 tys. z tego kasy placa z pasa ponizej zapasu (:129009). Hamulce: cena graniczna 1.5, wygladzanie
   popytu 15%/d, zakup ludu co dobe. Pomiar P0 "marza kupcow na towarze od karawan" jest warunkiem wlaczenia P2; prog w rozdz. 12.
2. **Pieniadz idzie do tych, ktorzy wytwarzaja i woza, nie do tych, ktorzy wojuja.** Z kazdych 10 zl panowie dostaja ok. 4.9 zl (wprost 2.4, przez korone
   2.5), lud 3.7 zl, notable i karawany 1.1 zl. To mniej na zold niz przy prostym poborze (cala kwota do skarbca) i trafia do innych panow: do pana
   miasta zlotnikow, nie do pana Riverrun. Nie wiem dzis, gdzie stoja warsztaty zbytkow (P0).
3. **Wojsko: realnie +0.9 do +1.3 tys. ludzi (gorna granica 3-4 tys.).** Brakuje 31 tys. Ten kierunek odpowiada na pytanie "na co lud wydaje", nie na
   pytanie "skad zold".
4. **Niepewne fi.** Ile oferty znajdzie towar, zalezy od karawan BK (439 sztuk, 80 wjazdow na dobe, najwyzej 1 500 zl zakupu na kategorie na wizyte,
   wyspy bez drog). Przy fi = 0.4 lokata nowej kampanii to 9 mln zamiast 5-6. Dlatego najpierw P0 i bieg 120 dob po P2.
5. **Kompanie najemne (O1-4) zbieraja ok. 49 tys. zl/d niezaleznie od tego kierunku** - bez tamtej poprawki uwolniony pieniadz skonczy u nich.
6. **Bogate miasta rosna w dobrobyt.** Wiecej zakupow = wyzsze zadowolenie BK = stabilnosc i dobrobyt (BK BKStabilityModel.cs:149), a dobrobyt
   podnosi zapas kupcow (10 000 + 12 x dobrobyt, :128914) - kazdy +1 000 dobrobytu w 97 miastach to +1.2 mln zl uspionego w kasach. Do obserwacji.
7. **Ceny.** Zbytki i sukno w bogatych miastach ida w strone 1.0-1.5 wartosci; u wytworcow rosna z 0.5-0.9 w strone 0.8-1.0. Zywnosc, piwo, plotno, skora,
   narzedzia - bez sygnalu, bez zmiany. Odziez wojska bierze filc (53 szt./d, :128782) - filcu jest nadmiar (9 286 w jukach), ryzyko male.
8. **Glod i lojalnosc.** Bez wplywu na chleb: oferta tylko z nadwyzki, sygnal nie dotyczy zywnosci. "Lepszy stol" kupuje z polki jak dzis (bez sygnalu)
   i ma mniejsza oferte niz dzis, wiec nie zabiera jedzenia bardziej niz obecna regula.
9. **Kolizje w kodzie do sprawdzenia przed P1** (CLAUDE.md 8.0): (a) Predict odejmuje od wydatku oferte OFEROWANA (`OfferFood`, `OfferRest` -
   Live.cs:684), nie kupiona - po zmianie tempa "potrzeba" bogatych miast przestanie byc rowna pelnemu koszykowi i zmieni sie zapas dni; trzeba odejmowac
   kupione (licznik P0) i przeliczyc progi linii "zapas dni". (b) Udzial klas w zaplacie (`Share`, :737) tez liczy oferte oferowana - dzis wyrobnicy placa
   88.5% rachunku (:129003: 343 969 z 388 862), choc ich koszyk to ok. 85% potrzeby, a rzemieslnicy 8.3% przy ok. 14%; mniejsza oferta to wyprostuje,
   wiec kiesy R drgna. (c) RawPrice (B) juz ustawia popyt plotna i skory z prawdziwego zuzycia - dlatego sa poza sygnalem. (d) Robota ludu w wojnie
   obchodzi `_warSkipped` - decyzja Jeffa z 05.10 "w wojnie 0, chyba ze mury" dotyczyla pieniedzy pana; tu placi lud (pytanie 2).
10. **Gracz-kupiec w pierwszym roku** zarobi na roznicy cen zbytkow wiecej niz dzis (to samo robia karawany AI; okno zamyka sie, gdy rozladuja 8 mln towaru).
11. **Pierwsze tygodnie na starym zapisie:** oferta 23.8 mln / 60 = 396 tys. zl/d wobec 163 tys. towaru - ceny zbytkow stana na pulapie 1.5 w
    najbogatszych miastach, fi bedzie niskie. W nowej kampanii (cel Jeffa) lokata nie narasta i tego skoku nie ma.

---

## 10. Koszt czasu doby, wylacznik, zapis

- **Czas.** Dzis kiesa ludu: latki 8.3 ms + blok doby 2.4 ms (:129003). Dochodzi: tabela grup w AddOffer (ta sama petla), jeden odczyt slownika na
  (miasto, kategoria) w EstimatePostfix - 97 miast x ok. 12 kategorii z sygnalem + jedno wywolanie wzoru ceny dla pulapu; zakupy 130 podzamczy ta sama
  metoda co 571 wsi; drugi platnik w petli 227 budow. Szacunek: ponizej 0.5 ms na dobe [S]; pomiar w linii "Kiesa ludu: czas".
- **Wylacznik.** `Folk Buy By Wealth` (bool). Wylaczony = stan dzisiejszy co do linii: oferta 15%/d na 8 kategorii, bez sygnalu, bez robot ludu. Sygnal
  popytu wygasa sam w ok. 3 tygodnie (wygladzanie gry), bez resztek. Osobno `Folk Works Share` = 0 gasi same roboty.
- **Zapis.** Zadnego nowego stanu. Oferta liczona co dobe z kies (zapis `arm_townfolk` bez zmian), popyt targu jest w danych rynku gry (zapis gry), punkty
  budowy w kolejce budow gry. Bez zmian w SaveDefiner i bez paczki SaveText.
- **Pulapka MCM.** `FolkSurplusSpendShare` moze byc zapisany w `Armoury.json` - sprawdzic przed testem (CLAUDE.md 7); nowe klucze po `gen_mcm.py`.

---

## 11. Wielkosc i paczki

Calosc: **L** (ceny + kiesy + budowy; pelny lancuch), w pieciu paczkach, kazda osobno do testu:

| Paczka | Wielkosc | Tresc | Warunek wejscia |
|---|---|---|---|
| P0 | S | pomiar na sucho (tylko log - lista w rozdz. 3) | - |
| P1 | M | regula dostatku: tempo 1/60, lista z wagami, poprawka Predict / Share | P0 w logu z 28 dob |
| P2 | M | targ widzi sakiewke (sygnal popytu z pulapem 1.5) | P1 po autotescie; marza kupcow z P0 nie gorsza niz -10% |
| P3 | S | podzamcza i wsie na targu miasta | P2 |
| P4 | M | lud buduje miasto | zgoda Jeffa na pytanie 2 |
| P5 | S | sakiewka turniejowa | tylko na slowo Jeffa (PLAN-DO-KONCA-MODA rozdz. 3) |

P1 bez P2 dziala tylko tam, gdzie zbytki juz leza na polce (jednorazowo do 4.4 mln zl towaru) - karawany dalej nie maja jak sprzedac. P2 jest sednem.

---

## 12. Progi do testu rocznego (nowa kampania, 364 doby)

| # | Co | Prog | Dzis |
|---|---|---|---|
| 1 | Kiesy ludu miast w d364 | <= 24 mln (cel ok. 18) | 36.5 mln |
| 2 | Lokata ponad zapas w d364 | <= 10 mln (cel 5-6) | 23.8 mln |
| 3 | Przyrost kies, srednia d302-364 | <= 10 tys. zl/d | +38.3 tys. |
| 4 | Kupione z oferty / oferta (nowy licznik), srednia d120-364 | >= 50% | ok. 0 |
| 5 | Indeks ceny kategorii z sygnalem w 20 najbogatszych miastach | 0.6-1.5; ani razu ponad 1.5 z powodu oferty | - |
| 6 | Cena zbytu klejnotu / aksamitu u wytworcy (a2_luxprice.py), d337-364 | >= 0.7 / >= 0.7 wartosci | 0.50 / 0.87 |
| 7 | Zbytki w jukach karawan | klejnoty <= 400, bawelna <= 2 000, filc <= 5 000 | 747 / 4 151 / 9 286 |
| 8 | Kasy kupcow: miast ponizej zapasu >= 7 dob | <= 4; zadne z 20 najbogatszych | 4 |
| 9 | Marza kupcow na towarze od karawan (P0) | >= -10% | brak pomiaru |
| 10 | Miasta ze "stac jedzenie" < 95% | <= 10 | 10 |
| 11 | Ceny zywnosci w 20 najbogatszych miastach wobec biegu bez reguly | +-5% | - |
| 12 | Kiesy 31 najbiedniejszych miast razem | >= 0.53 mln | 0.53 mln |
| 13 | Budowy stojace w d364 / poziomy ukonczone w roku | <= 150 z 227 / >= 100 | 198 / 69 |
| 14 | Dochod panow "ziemia" i wplywy koron, d337-364 (INFO) | >= 175 tys. / >= 415 tys. zl/d | 161 / 401 tys. |
| 15 | Partie rodow w d364 (INFO - nie prog) | +1 tys. wobec biegu bez reguly | 62.1 tys. |
| 16 | Zamkniecie: "zmiana kies - (wplywy - wydatki)", "Towary: bez wyjasnienia" dla listy dostatku | 0 / bez zmiany | 0 |
| 17 | Czas: latki + blok doby kiesy ludu | <= 11.5 ms | 10.7 ms |

Przed biegiem rocznym: bieg 120 dob po P2 z progami 4, 5, 8, 9.

---

## 13. Czego nie wiem

- Jak gra rozlozy oferte miedzy kategorie w grupie i ile z niej naprawde kupi (fi) - zaden bieg tego nie mierzyl; liczby rozdz. 4 to rachunek z udzialow,
  nie symulacja obiegu.
- Gdzie stoja warsztaty zbytkow i czyje sa wsie bawelny - od tego zalezy, ktorzy panowie dostana pieniadz.
- Jaka jest dzis marza kas kupcow na towarze od karawan (ryzyko 1).
- Jak to wyglada w pokoju: danina wojenna znika, wsie zatrzymuja swoje 55% utargu.
- Czy wzrost cen zbytkow nie uruchomi podmiany popytu gry miedzy kategoriami (G ItemConsumptionBehavior.cs:185-204, `CanSubstitute`) w sposob, ktorego nie
  przewidzialem - do obejrzenia w P0.
