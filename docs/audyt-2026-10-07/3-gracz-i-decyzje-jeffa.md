# AUDYT GRUPY "TOWARY 2" (115-119) - SPOJRZENIE 3: GRACZ I DECYZJE JEFFA

Audytor niezalezny, tylko odczyt. Repo Jeffa nietkniete (galaz `claude/bannerlord-rot-setup-o75kvo` na 5f94783, `git status`
pusty, jeden worktree). Proby w tym katalogu: klon `r` (galaz robocza + 5 cherry-pickow), worktree `w119` (detached na
`paczki/119`), buildy `out-norev`, `out-norev119`, `diff-all.txt` (diff kodu 5f94783 -> szczyt, 3232 linie).

## Wynik w jednym akapicie

Nic BLOKUJACEGO z mojego zakresu. Kod piec ogniw robi to, co opisuja, napisy dla gracza i opisy MCM sa po angielsku i
zgodne z dzialaniem, linie logu z "Po czym poznac" zgadzaja sie z kodem co do slowa, `Armoury.json` Jeffa nie ma zadnego
z 19 nowych kluczy ani `MarketMaxDistance`. Trzy rzeczy WAZNE: (W1) instrukcja wgrania w STAN-PRAC nie da DLL o md5
faade7bf - cherry-pick na galezi roboczej tworzy nowe commity, a numer commitu siedzi w DLL (wersja informacyjna), wiec
md5 wychodzi 9a5040b8; kod jest identyczny, ale "porownac md5" zatrzyma wgrywajacego; (W2) sztabka zlota po 118 nie
sprzeda sie nigdzie powyzej ok. 42% swojej wartosci (BK w swoim projekcie: 121%) - opis 118 twierdzi, ze to "ten sam
stosunek co w projekcie BK", co jest prawda dla klejnotow, ale NIE dla zlota (jedna kategoria z ruda zlota x8 tansza i
sztabka x4.75 drozsza); (W3) Jeff nie ma przed wgraniem opisu slowami gracza tego, co zobaczy (lup ze zlota i klejnotow
10-15 x tanszy, piekarnia moze kosztowac ponad 100 tys., jego wsie woza do obcych miast do 4 dob drogi, napad na tabor
daje 2 x wiecej towaru) - tabela "slowami gracza" w STAN-PRAC ma nazwy klas i skroty. Trzy DROBNE.

## USTALENIA

### W1 (WAZNE) - instrukcja wgrania: md5 nigdy nie bedzie faade7bf

- Proba (klon `r`, galaz `robocza` = origin/claude/bannerlord-rot-setup-o75kvo 5f94783): `git cherry-pick c49cdda 9b7a4e8
  0ffc41c 38054d7 0c7aa2d` - czysto, 5 commitow (f5255e7 na szczycie); `python tools/gen_mcm.py` - Armoury 631, nic sie nie
  zmienia; `dotnet build Armoury.csproj -c Release` - kod 0, 1 stare ostrzezenie CS0169. **md5 = 9a5040b81b1cdb179213c7ef0304d5e3**,
  nie faade7bf0dcc00de87d520a603234d15.
- Dowod przyczyny: ten sam plik z drzewa `paczki/119` (worktree `w119`, inna sciezka) daje faade7bf - sciezka nie ma
  znaczenia. Roznica 92 bajtow: naglowek PE (znacznik czasu = skrot tresci), MVID i napis wersji
  `1.0.0+f5255e76...` wobec `1.0.0+0c7aa2dd...` (SDK dopisuje numer commitu do AssemblyInformationalVersion). Z
  `-p:IncludeSourceRevisionInInformationalVersion=false` oba drzewa daja ten sam plik: 9ca7a7714376040f32bebb63537cfa25.
  `git diff origin/paczki/119-wozy-do-najlepszego-miasta HEAD -- Armoury` = pusty. Tak samo bylo z grupa 1+2: DLL w grze
  0eeb0a10 rozni sie od builda lancucha n107 (516df465) - wtedy instrukcja nie kazala porownywac md5.
- Skutek: wgrywajacy (zasada z pamieci "wgrywanie tylko po sprawdzonym buildzie") zobaczy niezgodnosc i stanie albo bedzie
  szukal bledu, ktorego nie ma; gorszy wariant - wgra kopie z `scratchpad` na dysku C (dysk zeruje pliki).
- Poprawka (STAN-PRAC, przed wgraniem): zamiast "porownac md5 z faade7bf" - `git diff paczki/119-wozy-do-najlepszego-miasta
  HEAD -- Armoury` ma byc pusty + build kod 0; albo oba buildy z `-p:IncludeSourceRevisionInInformationalVersion=false`
  (wzorzec 9ca7a771...). md5 w grze zapisac ten, ktory wyjdzie z builda w repo.

### W2 (WAZNE) - sztabka zlota po 118: 42% wartosci przy pustej polce, opis 118 mowi nieprawde o "projekcie BK"

- Kategoria `gold` ma dwa przedmioty o przeciwnych przelicznikach: ruda zlota 400 -> 50 (x8 tansza), sztabka 1000 -> 4750
  (x4.75 drozsza). Regula 118 = srednia geometryczna: przelicznik 1.298 dla obu.
- Rachunek wzorem gry (`DefaultTradeItemPriceFactorModel.GetBasePriceFactor`, popyt kategorii bez popytu bazowego =
  0.01 x (dobrobyt + 1000) = 58 w miescie-medianie; skrypt w tym raporcie, wynik ponizej):
  - projekt BK (wartosci z definicji, bez /100): sztabka przy pustej polce 1.21 x wartosci, ruda zlota 2.02 x;
  - po 118: sztabka 0.418 x (1983 d przy wartosci 4750), DRUGA sztabka 0.276 x (1311 d); ruda zlota 4.57 x (228 d przy 50);
  - przed 118: sztabka 6.6 x (31 393 d) - zgodne z opisem (31 021);
  - klejnoty po 118 0.119 x = projekt BK 0.119 x (tu opis ma racje). Lannisport: sztabka 0.455 x, BK 1.32 x.
- Opis 118, "Ryzyko pozostale (b)": "sztabka zlota ... -> ok. 2 tys. d, sakiewka klejnotow ... -> ok. 150 d - ... ta sama regula
  (w projekcie BK ten sam stosunek do wartosci)". Dla sztabki stosunek jest 3 x gorszy niz w projekcie BK, a ruda zlota 2.3 x
  lepsza. To ta sama klasa ograniczenia co opisane owoce (c) i sztabki zelaza w rudzie (f), ale z rozrzutem 38 x (BK: sztabka
  = 2.5 rudy; ceny historyczne: 95 rud) - wiec opis zaniza problem.
- Co zobaczy Jeff: sztabka z lupu (wartosc 4750 na ekranie) sprzeda sie najwyzej za ok. 2 tys. w kazdym miescie, nastepna za
  1.3 tys.; wozy wsi z kopalniami zlota dostana za rude 4.6-5 x jej wartosci przy pustej polce.
- Proba obalenia: czy sztabka i ruda sa w jednej kategorii - tak (log 14:08 linia 118: `gold_ore 4->50, goldingot 10->4750`;
  przelicznik "gold /1.3" w opisie 118 = sqrt(8 x 0.2105)). Czy kara handlowa BKROT zmienia obraz - nie, mnozy cene o kilka
  procent (opis: 1960 d wobec mojego 1983 d bez kary). Czy to zmienia zamknieta ekonomie - nie, tylko kwoty.
- Poprawka: przed testem - poprawic zdanie w opisie 118 i powiedziec Jeffowi (W3); po tescie (osobna paczka) - przelicznik
  popytu dla kategorii, w ktorej przedmioty przeliczono w przeciwne strony (np. wazony wartoscia przedmiotu w swiecie albo
  osobny przelicznik na przedmiot dla ceny).

### W3 (WAZNE) - Jeff nie ma przed wgraniem opisu "co zobacze w grze" (decyzja 06.10 nr 7 i nr 2)

- STAN-PRAC, tabela grupy (kolumna "Tresc (slowami gracza)"), wiersz 118: "BKROTPatch dzieli ich wartosc przez 100, przez co
  popyt zostal w starej monecie" - nazwy modow i skroty, ktorych Jeff nie zna. Brak w calym STAN-PRAC zmian, ktore Jeff
  zobaczy pierwszego dnia: lup, sprzedaz warsztatu, wozy jego wsi, napady na tabory. Decyzja 7: "kazde podsumowanie
  tlumaczyc slowami gracza"; decyzja 2: mowic o tym, co zmienia to, w co Jeff gra (wygoda gracza).
- Proponowany tekst dla Jeffa (do wiadomosci przed "wgraj"; liczby z opisow, sprawdzone rachunkiem tam, gdzie zaznaczono):
  1. Targ: ruda w miescie bez rudy - pierwszy ladunek 37 d (z kuznia 74 d), drewno 39 d, sol i piwo przy pustej polce do 10 x
     wartosci; tam, gdzie tego pelno - grosze. Welna tania wszedzie poza 23 miastami z tkalnia.
  2. Chleb, ciasta, miod tansze tam, gdzie je dowoza (bochen 61 -> ok. 20 d przy 10 bochnach dziennie); miod pitny prawie nic
     nie wart (102 -> ok. 10 d), miodosytnie stana.
  3. Lup: sztabka zlota ok. 31 tys. -> ok. 2 tys. (druga 1.3 tys.; W2), sakiewka klejnotow ok. 2.2 tys. -> ok. 140 d
     (sprawdzone), futro ok. 2 tys. -> 1.2-1.4 tys. przy pustej polce, ok. 250-300 przy kilku na polce, atrament 1000 -> 250,
     barwnik 3000 -> 470.
  4. Warsztat kosztuje 3 lata swojego zarobku plus pieniadze w jego kasie - notabl mowi w rozmowie, z czego to wynika; tam,
     gdzie brakuje chleba i ciast, piekarnia moze kosztowac ponad 100 tys. (tyle naprawde zarabia). Wlasny warsztat placi
     miastu 4 d dziennie i czeladnikom za kazda partie; sprzedajac go notablowi dostaniesz tyle, ile on ma w sakiewce (D1).
  5. Wozy: kazda wies (takze Twoja) wiezie towar do miasta do ok. 4 dob drogi, ktore zaplaci najwiecej - takze do miast
     innego krolestwa (nie wrogiego); woz kazdej wsi wiezie 2 x wiecej. Na drogach wiecej i dalej jadacych taborow: bandy
     maja wiecej celow, a Ty przy napadzie na tabor wroga zabierasz 2 x wiecej towaru. Pieniadze z Twoich wsi przyjda
     pozniej, ale zwykle wiecej.
  6. Karawany AI pakuja sie do pelna surowcami; Twoje karawany jak dotad (80%).
- Proba obalenia: szukalem w STAN-PRAC i opisach 115-119 zdania dla Jeffa o lupie i wozach jego wsi - jest tylko w czesciach
  technicznych opisow (118 "Ryzyko pozostale (b)", 119 "Kontrola"), nie w tekscie dla gracza.

### D1 (DROBNE) - sprzedaz warsztatu notablowi: gracz dowiaduje sie o niedoplacie dopiero po transakcji

- Rozmowa z czeladnikiem (gra, `WorkshopsCharactersCampaignBehavior` "workshop_35"): "you can get {PRICE}", PRICE =
  `GetCostForNotable` = 0.8 x wartosci dla notabla + kasa warsztatu (`WorkshopTrade.CostNotablePostfix`, WorkshopTrade.cs:691-699).
  Kupca losuje gra dopiero w konsekwencji (`GetNotableOwnerForWorkshop`), a `SalePostfix` (WorkshopTrade.cs:546-561) placi
  `min(cena, sakiewka kupca)` i dopiero wtedy pokazuje "The buyer could raise only X of the Y denars agreed for your
  workshop." - warsztat juz przeszedl.
- Kiedy to boli: warsztat drogi (piekarnia przy niedoborze chleba 100 tys.+), notable srednio ok. 17 tys. (log 14:08 linia 220:
  notable razem 29.3 mln). W normalnym warsztacie (ok. 20 tys.) zwykle nie.
- Proba obalenia: w grze bez paczki gracz dostaje cene z niczego (`GiveGoldAction(null, gracz)`), wiec niedoplata to skutek
  zamknietej ekonomii (zgodny z zasada 0) - zostaje tylko brak ostrzezenia. Poprawka: zdanie w rozmowie, gdy najbogatszy
  notabl miasta ma mniej niz cena, albo sprzedaz odmowiona do czasu, az bedzie kupiec z pieniedzmi.

### D2 (DROBNE) - suwaki MCM szersze niz to, co kod przyjmuje

- `Caravan Bulk Fill Limit` suwak 0.00-4.00 (McmSettings.cs), kod przycina do 0.8-1.0 (`MBMath.ClampFloat(s.CaravanBulkFillLimit,
  FillLimit, 1f)`; opis to mowi). `Workshop Trade Resale Share` suwak 0.00-3.20, kod przycina do 0-1 (WorkshopTrade.cs:697; opis
  nie mowi). Ustawienie 2.0 nic nie zmienia ponad 1.0. Poprawka: zakres w generatorze albo zdanie w opisie.

### D3 (DROBNE) - STAN-PRAC: "NOWA kampania (115, 118 i 119 licza start kampanii)"

- W kodzie start kampanii licza 115 (`RawPrice.SeedNewCampaign`), 116 (`WorkshopTrade.SeedNewCampaign` - kapital tkalni
  aksamitu) i 118 (przelicznik + przeliczenie pamieci rynku przez 115). `MarketCarts` (119) nie ma zadnej logiki nowej kampanii
  (stan w pamieci, bez `CampaignGameLoadingType`). `tools/sprawdz_logi.py` mowi poprawnie "115, 116 i 118". Poprawic numer w
  STAN-PRAC (test i tak idzie na nowej kampanii - bez skutku dla gry).

## CO JEFF ZOBACZY (przejscie po nowej kampanii, z kodu)

- Start gry (log): linie RawPrice / WorkshopTrade / CaravanBulk "poprawka 115" (znane) / HistoricalPrices z definicji /
  MarketCarts - teksty w kodzie = teksty w opisach (tabela nizej).
- Rozmowa kupna warsztatu: nasza linia (pierwszenstwo 200, stan `workshop_owner_notable_single_response`) obejmuje obie
  sciezki gry - "I wish to buy your X" i "one of your workshops" -> wybor (gra przechodzi do tego samego stanu). Cena w zdaniu
  = cena, ktora sprawdza przycisk "Yes" (`GetCostForPlayer`). Tekst po angielsku, gracz bez zdania o podatku (u Jeffa czynny
  model warsztatow NavalDLC -> TaxShare 0).
- Ekran rodu: "Daily Wage 4" dopiero po latce w kampanii (inaczej 0 - gracz nie placi dwa razy).
- Wlasne warsztaty: utrzymanie i place z kasy warsztatu, ponizej 500 w kasie - z sakiewki gracza bez komunikatu (jak gra przy
  5000). Zmiana produkcji: komunikat o zaplacie miastu.
- Wlasne wsie: wybor miasta pomija miasta oblezone i wrogie (ten sam warunek co gra w `HourlyTickParty`); przy wojnie w trakcie
  kursu gra przekierowuje, my liczymy od miejsca na mapie. Zamki gracza: w tescie 14:08 bez zywnosci "z polki" ujemny bilans
  mial 1 zamek na 130 (linia 1572) - wiekszy zasieg wozow nie zaglodzi zamkow.
- Wlasne karawany: zakup surowcow przed wyborem trasy dotyczy tez ich (jak 103), kreska udzwigu zostaje 80% (`Players()`,
  CaravanBulk.cs) - zgodnie z opisem MCM.

## DECYZJE JEFFA - KONTROLA

- Handel z biezacego zysku, bez list (06.10 nr 1): wybor miasta przez wozy = utarg na dobe kursu z ceny modelu i czasu
  (MarketCarts.Choose), remis = blizsze; karawany - kolejnosc i kierunek z cen (BK + nasz zakup przed celem). Jedyna lista to
  7 surowcow masowych w cenie "od zuzycia" (115) - z decyzji Jeffa 05.10 o surowcach masowych; pozostale wsady warsztatow
  (zboze, winogrona, oliwki, glina) maja w cenie pelny apetyt mieszczan (`TownUse` = 1 poza 7), a polka oprozniana przez
  warsztat podnosi cene - sygnal dla wozow jest. OK.
- 100%, nie ulamek (06.10 nr 4): wozy - kazda wies (miejska zawsze z wlasnym miastem, zamkowa z miastem w 250); warsztaty -
  wszyscy wlasciciele poza 97 ukrytymi rzemieslnikami (znane, K13); karawany - wszystkie AI, gracza 80% (wygoda gracza).
- Bez cichej zmiany produkcji (06.10 nr 3): ruszaja piekarnie i inne (opisane jawnie), miodosytnie stana (opisane), przestoj
  wsi gorniczych i drwali przy dalekich kursach (opisany w 119; pilnuje go linia "Dowoz (skutki)").
- Bez niewidzialnego przerzutu (05.10): wozy i karawany to partie na mapie; "pelny woz" = magazyn wsi -> juki w tej wsi;
  "wiesc z drogi" to wiedza, nie towar; zwrot nadplaty = sakwa taboru -> kasa osady, w ktorej tabor stoi.
- Zold, paser, dlug: zadne ogniwo ich nie rusza. Paser (106) wycenia sztuka po sztuce (OutlawLaw.cs:1212-1225), karawany BK
  sprzedaja przez `SellItemsAction` sztuka po sztuce - po 119 jedna zasada ceny ladunku dla wszystkich (tylko tabory wsi mialy
  cene pierwszej sztuki).

## OPISY "PO CZYM POZNAC" WOBEC KODU (kazda linia)

| Ogniwo | Linia w opisie | Kod | Wynik |
|---|---|---|---|
| 115 | start "RawPrice: cena surowcow od niedoboru - stala wzoru ceny w nowej monecie wpieta w 2 modelach cen (CZYNNA), popyt ... wpiety w 2 modelach ekonomii osad (CZYNNY) [...]" | RawPrice.cs:408-412 | zgodne; skan wszystkich DLL modulow: `GetBasePriceFactor` / `GetEstimatedDemandForCategory` deklaruje tylko BetterEconomy (+ gra) -> 2 i 2 |
| 115 | "RawPrice: nowa kampania - ... w 97 miastach (zamki bez zmian ...), ok. 38 kategorii" | RawPrice.cs:202-204 | zgodne; z 118 bedzie 46 (opis 118 to mowi) |
| 115 | "Ceny surowcow: dzien D - popyt z prawdziwego zuzycia CZYNNY, stala wzoru w nowej monecie CZYNNA (model cen BKROTPriceModel ...", "bez towaru N miast - za pierwsza sztuke placa", "z nadwyzka ... sprzedaja po", "popyt dobowy miasta: mieszczan ... + rzemiosla ..., w danych rynku ..., sam szacunek gry", "Inne przeliczone towary" | RawPrice.cs:273-278, 342-345 | zgodne (grain/salt/beer) |
| 116 | start "WorkshopTrade: warsztaty towarowe w nowej monecie CZYNNE - wpiete: prog cyklu notabla, ... koszt sprzetu; stala 200 podmieniona w 2 z 2 metod; latka modelu finansow rodu dojdzie w kampanii" | WorkshopTrade.cs:1028-1054 | zgodne, ta sama kolejnosc nazw |
| 116 | "WorkshopTrade: latka modelu finansow rodu (zakladana w kampanii) - drugi pobor wydatku z kiesy gracza wylaczony" | :624 | zgodne (+ ", ekran rodu pokazuje utrzymanie dzienne.") |
| 116 | "WorkshopTrade: nowa kampania - kapital startowy wedle cen nowej monety: sprawdzono ..., poprawiono ... (velvet_weavery 2000 -> 10000), kapital warsztatow +..." | :475-477, 1071 | zgodne |
| 116 | "Warsztaty towarowe: dzien N \| warsztatow ... \| wedle typu: bakery ... \| do kas miast ... \| zeszlo z kapitalu ... \| bankructwa ... \| cena kupna dla gracza: mediana ...; przyklad Lannisport ... podatek gracza 0% ... wyrob bread xA, wsad grain xB \| zasady: ...; potkniecia 0." | :910-924, 942-947, 959-962 | zgodne; `town_V9` = Lannisport (ROT-Map settlements.xml:5348) |
| 117 | start "CaravanBulk: poprawka 115 - zakup przed wyborem celu (BK BuyGoods): latka wpieta; juki na surowce do 100% udzwigu (BK konczy zakupy na 80%; karawany gracza zostaja przy 80%)" | CaravanBulk.cs (diff 462-464) | zgodne (roboczy numer - znane) |
| 117 | "Karawany (przyczyny): ... przed wyborem celu N wizyt, przy wyjezdzie M; surowce zajely X kg ..., z tego Y kg ponad 80% ...; wyjazdy z miasta z nadwyzka surowca - ruda K: kupily ..., brak miejsca ..., w drodze dosc ..." | diff 430-434 | zgodne |
| 117 | "Karawany (kierunek): ... wjazdy do ... roznych miast; karawan w miastach P, w drodze Q; ladunek surowcow - ruda: wjazdy A, w tym do miasta z brakiem B (dostawe dostalo C miast), wyjazdy E, w tym z celem w miescie z brakiem F; w jukach teraz: ..." | diff 435-437 | zgodne |
| 118 | start "HistoricalPrices: wartosc z definicji towarow BK (BKItems.InitializeTradeGood) - wpieta (przed latkami innych modow)." | HistoricalPrices.cs (ApplyAll) | zgodne |
| 118 | "przelicznik popytu od wartosci z definicji przedmiotu CZYNNY (...); wziete z definicji 18 [...] w 15 kategoriach", brak "UWAGA - ... wartosc 0" | HistoricalPrices.cs:419-425 | zgodne |
| 118 | "Towary z wartoscia z definicji przedmiotu (...): bread (bread 6 d) /3.33 ..., na polkach N szt., pusto w N miastach, budzet mieszczan X d (przy dzisiejszych polkach do Y d)" | RawPrice.cs:335-339, 345 | zgodne |
| 119 | start "MarketCarts: poprawka 119 - wozy wsi do najlepiej placacego miasta w zasiegu 250 (CZYNNE; pelny woz na daleka droge tak, wiesc z drogi tak), cena ladunku sztuka po sztuce - latka wpieta (CZYNNA), woz x2.0 dla wsi wszystkich." | MarketCarts.cs:654-658 | zgodne |
| 119 | "Dowoz (wozy): dzien D - wybor miasta CZYNNY: N wozow (wsi zamkowych M), do wlasnego miasta A, do innego B, ...; wiesc z drogi: ..., wygasle przy porzadkach N; z ruda J, w tym do miasta bez rudy K (roznych miast L); wycen ... \| cena ladunku sztuka po sztuce CZYNNA: ... niezgodne 0, cena rosla 0 \| woz x2.0 dla wsi wszystkich" | MarketCarts.cs:623-635 | zgodne |

`tools/sprawdz_logi.py --grupa 2b` na logu 14:08: dziala, rozpoznaje brak ogniw (najwyzsze 107), tabela dzien po dniu bez
bledu; wzorce startowe 115-119 w narzedziu = teksty w kodzie.

## INSTRUKCJA WGRANIA - PROBA W KOPII

- Klon `r` (`git clone --no-hardlinks` repo, `core.longpaths`), galaz robocza 5f94783 (kod = `paczki/107`, `git diff --stat`
  pusty; DLL w grze 0eeb0a105dc5ae4221809e222e327afe zgodny ze STAN-PRAC).
- cherry-pick 5 commitow: czysto; gen_mcm: 631, plik bez zmian (takze RealisticCaptivity 98, GrandTourney 37); build kod 0;
  md5 9a5040b8 - patrz W1. Kod = `paczki/119` co do bajtu (diff pusty; buildy bez numeru commitu identyczne).

## ARMOURY.JSON JEFFA (tylko odczyt)

`Documents\...\Global\Armoury\Armoury.json`: 9874 B, 335 kluczy, md5 47d9d803..., zmieniony 05.10 05:39. Zadnego z 19 nowych
kluczy, brak `MarketMaxDistance` i `MarketCartFactor` (wejdzie 250 i 2). Ze wszystkich ustawien czytanych przez nowy kod
(RawPrice, WorkshopTrade, MarketCarts, CaravanBulk, MarketRoad, HistoricalPrices, WorkshopLaw) w pliku jest tylko
`WorldPacePercent` = 75 (tak licza opisy: 61.7 jedn. drogi na dobe). `MinSellPercentOfValue` 5 - nie dotyczy rachunkow W2
(klejnoty 11.9% > 5%).

## SPRAWDZONE I W PORZADKU

- Napisy dla gracza (rozmowa kupna, komunikat o kupcu bez pieniedzy, "Armoury: market carts" w udzwigu, 19 opisow MCM): po
  angielsku, bez znakow spoza ASCII w dodanych liniach, pliki bez BOM; opisy MCM zgodne z kodem (sprawdzone kazde z 19 zdan
  kluczowych: wymagania, wartosci "off", wyjatek karawan gracza, prog 500, 0 = zasieg gry).
- Linia rozmowy obejmuje obie sciezki gry (jeden i kilka warsztatow notabla); przycisk "Yes" sprawdza te sama cene.
- Sprzedaz gracza i bankructwo: sygnatura `ChangeOwnerOfWorkshopAction.ApplyInternal(workshop, newOwner, type, capital, cost)` -
  `__4` = cena; gra tez odbiera graczowi warsztat przy bankructwie (`HandlePlayerWorkshopExpense`) - paczka tego nie dodaje.
- Wybor miasta wozu nie tworzy petli: gra przelicza cel co godzine tylko dla celu oblezonego/wrogiego albo zachowania innego niz
  GoToSettlement, a Choose pomija te same miasta.
- Zamki gracza nie zalezne od zywnosci z polki (log 14:08: "bez polki ujemny 1 z 130").
- Repo nietkniete (status pusty, worktree tylko glowny).

## PLIKI (ten katalog)

- `r` - klon z galezia `robocza` + 5 cherry-pickow (szczyt f5255e7), `r\Armoury\bin\Release\Armoury.dll` (9a5040b8);
- `w119` - worktree detached `paczki/119` (build faade7bf);
- `out-norev`, `out-norev119` - buildy bez numeru commitu (oba 9ca7a771);
- `build*.log`, `diff-all.txt`.
- Rachunek W2: wzor gry `(D / (0.1 S + 0.04 V + 2))^0.6`, D = 0.01 x (dobrobyt + 1000), V przy sprzedazy = polka + sprzedawana
  sztuka, wszystko x przelicznik kategorii (115 A).
