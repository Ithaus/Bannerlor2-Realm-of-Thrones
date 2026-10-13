# PROJEKT: GOSPODARKA OD LUDZI - regula "produkuja ludzie" (2026-10-13)

Status: **PROJEKT BEZ KODU, PO RECENZJI Z POPRAWKA 13.10** (sekcja "Po recenzji" ponizej). Nic w grze, kodzie, ustawieniach ani zapisach nie zostalo zmienione; bez commita. Kod dopiero po "tak" Jeffa na pytania z rozdz. 10.
Decyzje Jeffa 13.10, na ktorych stoi projekt: "produkuja ludzie; tam, gdzie jest ich wiecej, wiecej sie produkuje"; "wioska to tylko SYMBOL wielu wiosek, ktore lacznie produkuja tyle, ilu jest ludzi"; przyklad "Polnoc 10, bo 100 tys. ludzi - Reach 37, bo 370 tys."; "mozna jeszcze uwzglednic dobrobyt, technologie, klimat".

Skroty zrodel: **A\** = nasz kod `...\ae222cb7-...\scratchpad\zb\int\Armoury\src` (galaz w-toku/zbior-e8); **G\** = dekompilacja gry `...\scratchpad\dec32\cs_full`; **BK\** = BannerKings `...\scratchpad\e4tools\bkfull`.
**RK / RD / RT** = rozpoznania w `...\dbc3d3d1-...\scratchpad\gosp-ludzie\` (rozpoznanie-kod.md, rozpoznanie-dane.md, rozpoznanie-towary.md). **SK** = rachunek tego projektu `gosp-ludzie\projekt\p01_skala.py` -> `p01_skala.out.txt`.
Bieg roczny: 364 doby (dni gry 108836-109199), log `D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_06-24-10.log`; "dzis" = srednia ostatnich 28 dob, jesli nie napisano inaczej.
Znaczniki: **[P]** pomiar (log, tabela, kod), **[S]** szacunek / rachunek na pomiarach. "Nie wiem" = nie ustalone. Linie kodu oznaczone (*) sprawdzilem sam w plikach; pozostale podaje za rozpoznaniem kodu. **[R]** i (*R) = poprawki i sprawdzenia recenzji.

## Po recenzji: co zmieniono (13.10, recenzja z poprawka)

Recenzja przeliczyla tabele od zera wlasnymi skryptami z surowych tabel biegu rocznego i z logu (bez plikow posrednich autora): `...\dbc3d3d1-...\scratchpad\gosp-ludzie\skrypty-rec\` - r01_krolestwa.py (ludzie, dochod, wojsko), r02_wsie_zywnosc.py (wsie, skala, przyklad Jeffa, udzialy towarow), r03_bilans_zywnosci.py, r04_dochod_wojsko.py (stan "po", regresje), r05_miasta.py i r06_lud_po_zmianie.py (kiesy ludu, zapasy kupcow); wyniki w plikach .out.txt obok. Kopia dokumentu sprzed recenzji: `skrypty-rec\PROJEKT-przed-recenzja.md`. Znacznik **[R]** = miejsce zmienione albo dopisane przez recenzje; **(*R)** = linia kodu sprawdzona przez recenzje. Bez kodu, bez commita.

**Werdykt.** Droga (C) - skala u korzeni, bez ruszania hearth i dobrobytu - **zostaje**: ani gra, ani BannerKings jej nie nadpisza (pkt 7), a zloto ani towar nie powstaja z niczego. **Towar pojdzie za ludzmi od pierwszego dnia. Ze pieniadz pojdzie za nim sam - tego projekt nie udowodnil**: pokazal stan docelowy jako skutek, a dojscie do niego jest powolne, ma trzy dziury przejsciowe (pkt 2 i 5) i bez pomocy moze utknac. Jeden punkt wpiecia byl opisany blednie (pkt 4). To trzeba domknac w projekcie przed pierwsza linia kodu reguly; pomiar P0 zostaje pierwszym krokiem.

1. **Liczby "dzis" na krolestwo.** Sumy swiata zgodne z autorem co do zlotowki (wplyw rodow 1 213.4 tys., ziemia 163.4 tys., korona 456.5 tys. zl/d; wojsko 162 594; limit druzyn 163 916). W krolestwach autor przypisal rod do krolestwa z ostatniej doby, recenzja - doba po dobie (jak `docs\ludnosc\kr-ludnosc-a-zarobek.py`). Roznice ponad 15%: Braavos wplyw 17.0 -> 19.8 tys. i limit druzyn 2 889 -> 3 528, Volantis limit 6 866 -> 5 721, Ibben wplyw 12.9 -> 9.7, Summer Isles 7.9 -> 2.9, Skagos 5.2 -> 2.6 tys. Ponizej 15%, ale poprawione: Volantis wplyw 59.0 -> 54.8, Qarth 39.7 -> 41.7, Iron Islands 30.0 -> 31.3 tys. Tabela rozdz. 4 podmieniona w calosci, tabela rozdz. 0 przepisana. Skala wsi, przyklad Jeffa i udzialy towarow - zgodne z autorem (roznice do 3%).
2. **Dochod i wojsko "po zmianie" to granica, nie prognoza.** Autor podzielil caly staly dochod rodow (ziemia + korona, 620 tys. zl/d) wedlug ludzi. Z tego tylko 119 tys. zl/d (19%) to podatki wziete wprost z gospodarki ludzi: podatek wsi BK 75.1, oplaty stale wsi 9.9, od zarobku ludu miast i podzamczy 12.5, farma gmin 3.5, cla i podatek miast 18.4 tys. [P linie "D staly (169c)" i "Prawa panskie", doba 109199]. Reszta to zawor i danina kas miast oraz zwrot 70% zoldu (269 tys.) - pieniadz samych panow, ktory wraca przez wojsko, miasta i korone i krazy tam, gdzie stoi wojsko. Dopisany **"pierwszy krok"** (tylko te 19% idzie od razu za ludzmi): Reach x1.17 zamiast x1.89, Polnoc x0.90 zamiast x0.46, Dorne x0.87 zamiast x0.30. Granica zostaje jako stan docelowy - jest spojna (gdy zloto lezy wedlug ludzi, obrot, podatki i zold tez ida wedlug ludzi) - ale czasu dojscia nie da sie policzyc na papierze: **nie wiem**. Jest ryzyko, ze bez pomocy nie dojdzie w rozsadnym czasie: w ludnych miastach zabraknie monety (pkt 5), ceny tam spadna i utarg wsi w zlocie stanie; wyrownac to moze wtedy juz tylko handel miedzy krainami, a ten ma plaski limit karawan. Najprostsza pomoc: nowa kampania z zlotem startowym rozdanym wedlug ludzi (pyt. 7) - wtedy swiat startuje blisko granicy.
3. **Bilans zywnosci.** "Dzis" zgodne z autorem do 2% (Reach +571 wobec +575). "Po zmianie" rozne ponad 15% w Dorne (+146 -> +181), na Zelaznych Wyspach (+62 -> +107), w Volantis (+3 -> -52) i Braavos (+29 -> +5): autor przeskalowal tez "zywnosc wsi" gry (800 szt. zboza dziennie) i ryby spoza typu wsi (240 szt.), ktorych regula nie rusza (rozdz. 3.3). Wazniejsze: suma swiata "+2 092" to nie nadwyzka - z niej jedza druzyny (2 029) i idzie przerob na chleb i piwo (1 579); po ich odjeciu swiat ma -1 516 szt./d, pokryte z uboju i tickow osad (1 955). Dopisana kolumna "po odjeciu wojska i piekarni". Qarth sprowadza 3/4 jedzenia JUZ DZIS (wlasne wsie 73 z 292 szt.); po zmianie proporcja ta sama, rosnie ilosc (219 -> 610 szt./d).
4. **Punkt wpiecia popytu miasta nie robi tego, co pisal projekt.** `AnchorBudgetPostfix` i slownik budzetu umieja dzis tylko CIAC (A\TownFolk.Live.cs:349 `Clamp01`, :423 `if (fac >= 1) return`, :736 `min(1, koszyk / wydatek)` (*R)); podniesienie budzetu w miescie ludnym (m 2.25) to nowa galaz kodu, dotad martwa (zasada 4 CLAUDE.md). Do tego cena targu liczy sie z popytu z dobrobytu (G\...GameComponents\DefaultTradeItemPriceFactorModel.cs:170-182 (*R); popyt wygladzany co dobe z `GetEstimatedDemandForCategory`, G\...CampaignBehaviors\ItemConsumptionBehavior.cs:207-216 (*R)) - sama kotwica go nie zmieni, wiec targ ludnego miasta mialby te sama "glebokosc" co pustego. Wlasciwe miejsce to zrodlo popytu `GetDailyDemandForCategory`, na ktorym juz siedzi nasz postfiks (A\HistoricalPrices.cs:1170-1174 (*R)) - karmi i budzet, i cene. Poprawione w 3.3.
5. **Trzy dziury przejsciowe, ktorych projekt nie mial** (rozdz. 5 i 9):
   - **lud ludnych miast nie udzwignie koszyka**: potrzeba x2-2.5, a zarobek prawie bez zmian - zarobek ludu to udzial w obrocie kasy miasta i wydatkach wojska i dworu (A\TownFolk.Live.cs:774-800, A\TownFolk.cs:62 (*R)), nie w liczbie ludzi. Rachunek na srednie miasto krolestwa [S z P kiesa-ludu.csv; z wiekszych zakupow wraca ludowi 16-30% jako zarobek - robocizna handlu 20% / 10% / 4% i 3% obrotu, A\Settings.cs:1636-1640 (*R)]: oszczednosci starcza w Braavos na ok. tydzien, w Pentos 2 tygodnie, w Qarth 7-8 tygodni, w Reach 10-12 tygodni, w Stormlands i King's Landing 7-10 miesiecy; potem lud stac na 30-65% koszyka (Qarth 0.3, Braavos 0.33, Reach 0.43, King's Landing 0.65). Juz dzis lud miast Reach stac na 92-94% [P]. W pustych krainach odwrotnie: lud zarabia 2-6 razy wiecej, niz bedzie potrzebowal (miasto Wysp: zarobek 3.1 tys. zl/d przy potrzebie 0.5 tys.), i odklada - a z oszczednosci bierze danina P1, czyli korona i panowie pustej krainy;
   - **zapas kupcow x m_t**: miasta ludne musza najpierw zatrzymac +2.56 mln zl (Reach +775 tys., Volantis +448, Qarth +431, King's Landing +282, Braavos +278), wiec zawor do panow i korony staje tam na ok. 1-2.5 miesiaca, a puste oddaja jednorazowo 2.28 mln swoim panom (Wyspy 379 tys., Polnoc 340, Dragonstone 278, Dorne 178) [S z P kasy-miast.csv] - przez pierwsze tygodnie skutek odwrotny do zamierzonego. W nowej kampanii to samo dotyczy kas startowych i daru startowego dla ludu;
   - **ludzie rynku skacza**: gra przypisuje wsie zamkow do targu od nowa przy kazdej wojnie, pokoju i zmianie pana (G\...CampaignBehaviors\VillageTradeBoundCampaignBehavior.cs:64 (*R)), a z nimi skakalyby glowy, budzet i zapas kupcow miasta.
6. **Rzeczy "na osade / na rod / na krolestwo", ktorych regula nie dotyka, a ktore rozstrzygna wojny** (lista "Pominiete" w rozdz. 9): normy i zold zalog (Wyspy: zalogi 14.9 tys. zl/d przy docelowym dochodzie stalym 5.6 tys.), liczba rodow (Dorne 42 rody na docelowe 13.4 tys. zl/d), skarbce koron (2 mln na start kazdemu krolestwu i 1/360 zapasu dziennie - tyle samo Reach i Wyspom Letnim), konsumpcja zamkow, rzemieslnicy BK, limit karawan.
7. **Sprawdzone, ze nikt nie nadpisze skali** [P kod]: petla produkcji BK bierze ilosc z czynnego modelu (BK\BannerKings.Patches\EconomyPatches.cs:958-962 (*R)), czynny jest model gry (log l.464, l.706); progi hearth siedza tylko w Village.cs:320-331 i w linii 31 modelu; konsumpcja BK je z tego samego slownika budzetu (EconomyPatches.cs:505-560 (*R)); czynny model popytu to model gry z dobrobytu, nie model BK z glow (log l.706; BK\...\BKEconomyModel.cs:535-554 nie biegnie); regulator kasy gry stoi (tryb 2, log "kasowanie zablokowane"); sufit kiesy wsi BK ominiety (A\TownPurse.cs:738-756 (*R)); model zywnosci BK wola model gry i doklada tylko "Local farmland" x0.15 (BK\BannerKings.Models.Vanilla\BKFoodModel.cs:46-59 (*R)) - skladnik "sprzedane mieszczanom" jest zawsze z gry, wiec neutralizacja licznika Food jest wykonalna niezaleznie od tego, co stoi pod ROT ("nie wiem" z rozdz. 9 zdjete). Pozostale odwolania plik:linia oznaczone (*) - istnieja i robia to, co pisze projekt; dwie niescislosci: `Scale` (A\TownFolk.cs:428-434) liczy ludzi WLASNYCH osady, nie rynku - przejscie na rynek to przepisanie funkcji, nie parametr; wzor zapasu kupcow ma dwie kopie w kodzie logu (A\Measure169c.cs:351, A\MoneyLedger.Obieg.cs:525).
8. **Rozdzial 0 przepisany**: tabela z trzema liczbami (dzis / pierwszy krok / granica), obietnice bez liczby zamienione na liczby albo "nie wiem" (ceny, zloto z ludnej wsi, warsztat, pobor), dopisane niepewnosci 1, 2 i 6.
9. **Pytania**: 4 poprawione (Qarth juz dzis zyje z dowozu), dopisane 6 (len: wyzsza stawka czy wiecej wsi lnianych) i 7 (zloto startowe nowej kampanii - po recenzji najwazniejsze z pytan).

## 0. Dla Jeffa - prostym jezykiem
**Jak jest dzis.** Ksiega ludzi wie, ze jedna wies na mapie stoi w Reach za ok. 154 tys. ludzi, na Polnocy za 49 tys., w Dorne za 35 tys. - ale gra liczy produkcje od "hearth", ktory jest wszedzie prawie taki sam (ok. 400), wiec kazda wies z symbolem zboza wywozi ok. 54 sztuki dziennie [P]. Dlatego pieniadz idzie za liczba osad, nie ludzi: z ziemi panowie maja srednio ok. 200 zl dziennie na osade, wszedzie podobnie [P]; na 1000 ludzi rody Reach maja 9.9 zl, Polnocy 33.8 zl, Dorne 92.4 zl dziennie [P]. Wojsko tak samo: Reach (8.3 mln ludzi) trzyma 11.6 tys., Polnoc (3.2 mln) 17.6 tys., Dorne (1.1 mln) 10.4 tys. [P].

**Regula "produkuja ludzie" w 5 zdaniach.**
1. Wies na mapie to symbol wszystkich wsi okregu: wytwarza tyle, ilu ludzi ma okreg w ksiedze ludzi, a rodzaj wsi mowi tylko, CO wytwarza (kazdy towar ma stawke na 1000 ludzi; wies o ludnosci sredniej dla swojego rodzaju daje tyle co dzis).
2. Miasto to symbol rynku okregu: kupuje, zjada, przerabia w warsztatach i trzyma zapas pieniedzy kupcow w proporcji do ludzi rynku (miasto + wsie, ktore w nim handluja).
3. Pieniedzy nikt nie mnozy: podatki, renty i zold ida same za tym, co ludzie naprawde sprzedali i zarobili - zadnych doplat.
4. Tempo werbunku i pospolite ruszenie (milicja) ida za ludzmi okregu; hearth, dobrobyt, limity druzyn panow, mury, liczba osad i warsztatow zostaja, jak sa.
5. Skala zmienia sie sama co dobe: ludzie rosna, gina, uciekaja i wracaja w ksiedze - produkcja i rynek ida za nimi.

**Co sie zmieni: towar od razu, pieniadz powoli. [R]** Towar pojdzie za ludzmi od pierwszego dnia. Twoj przyklad na liczbach biegu: wies zboza na Polnocy da ok. 39 sztuk dziennie, w Reach ok. 125 (dzis obie ok. 54) - stosunek 3.2, bo tyle wynosi stosunek ludzi na taka wies (167 tys. do 52 tys.) [S]. Suma swiata kazdego towaru zostaje, zmienia sie podzial.
Z pieniadzem jest trudniej, bo nikt go nie dosypuje, a dzis lezy rowno "na osade". Tylko ok. 1/5 stalego dochodu panow (119 z 620 tys. zl dziennie) to podatki wziete wprost od wsi i mieszczan [P]; reszta to ich wlasny zold, ktory wraca przez miasta i korone - a ten krazy tam, gdzie juz stoi wojsko. Dlatego tabela ma trzy liczby dochodu: **dzis**, **pierwszy krok** (pierwsze miesiace; za ludzmi idzie tylko ta 1/5) i **granica** (gdy zloto lezy juz wedlug ludzi - wtedy obrot, podatki i zold tez ida wedlug ludzi). Ile potrwa droga od pierwszego kroku do granicy - **nie wiem**; sama z siebie moze trwac lata gry albo utknac, bo w ludnych miastach zabraknie monety (niepewnosc 2). Skrot jest jeden i uczciwy: w nowej kampanii rozdac zloto startowe wedlug ludzi (pyt. 7) - wtedy swiat zaczyna blisko granicy. Wszystko "po" to szacunek [S].

| krolestwo | ludzie tys. | staly dochod rodow (ziemia + korona, bez lupow) tys. zl/d: dzis -> pierwszy krok -> granica | zywnosc ze wsi minus to, co zjadaja mieszczanie i zamki (szt./d): dzis -> po | wojsko tys.: dzis -> w granicy |
|---|---:|---|---|---|
| The Reach | 8 345 | 53 -> 62 -> 100 | +571 -> +1 021 (spichlerz swiata) | 11.6 -> 15-17 |
| King's Landing (Zachod + Ziemie Korony) | 4 781 | 46 -> 48 -> 58 | +167 -> +53 | 13.0 -> 14-16 |
| The North | 3 227 | 84 -> 75 -> 39 | +210 -> +352 | 17.6 -> 8-13 |
| The Vale | 3 222 | 51 -> 48 -> 39 | +76 -> +376 | 11.3 -> 9-10 |
| Stormlands | 2 757 | 37 -> 36 -> 33 | +177 -> +150 | 10.9 -> 10 |
| Riverlands | 2 741 | 35 -> 34 -> 33 | -10 -> +222 | 10.2 -> 10 |
| Dorne | 1 116 | 45 -> 39 -> 13 | +241 -> +181 | 10.4 -> 3-7 |
| Iron Islands | 469 | 20 -> 17 -> 6 | +228 -> +107 | 6.4 -> 2-4 |
| Volantis | 4 993 | 22 -> 29 -> 60 | +53 -> -52 | 5.5 -> 8-9 |
| Qarth | 3 983 | 17 -> 23 -> 48 | -219 -> -610 | 5.0 -> 5-8 |
| Braavos | 2 593 | 9 -> 13 -> 31 | +63 -> +5 | 4.0 -> 5-8 |
| Targaryen (Meereen) | 1 972 | 25 -> 24 -> 24 | +57 -> -15 | 4.5 -> 4.4 |

Jak czytac tabele: (1) do stalego dochodu dochodza lupy i okupy (w swiecie 362 tys. zl dziennie, bez zmian) - Dorne i Meereen zyja z nich bardziej niz z ziemi; (2) z "plusa" zywnosci trzeba jeszcze wykarmic wojsko i piekarnie (w swiecie razem ok. 3 600 sztuk dziennie), wiec plus ponizej ok. 150 to w praktyce "na styk"; (3) Qarth juz dzis sprowadza 3/4 jedzenia (wlasne wsie daja 73 z 292 sztuk) - po zmianie proporcja ta sama, ale dowiezc trzeba 610 zamiast 219 sztuk dziennie; (4) wojsko "w granicy" to stan po dojsciu pieniadza - w pierwszym roku liczby beda blizej dzisiejszych.

**Towary - skrot** (na 1000 ludzi swiata na dobe; "powinno" = wzorzec sredniowieczny w skali targu gry, pelna tabela w rozdz. 4A):

| towar | dzis | powinno [S] | po zmianie [S] | skad po zmianie (w nawiasie dzis) |
|---|---:|---:|---:|---|
| zboze | 0.174 | 0.449 (cale jedzenie razem sie zgadza) | 0.174, inny podzial | Reach 27% (12%), Polnoc 10% (13%), King's Landing 8% |
| ryby | 0.050 | 0.008 (6.5 raza za duzo) | 0.050, inny podzial | Reach 16% (8%), Volantis 12% (3%), Dolina 12%; Wyspy 2% (8%) |
| len | 0.0094 | 0.0073 + wojsko = ok. 0.017 | 0.017 (x1.8 - do Twojej decyzji, pyt. 6) | King's Landing 20%, Braavos 13%, Stormlands 13%, Norvos 13% |
| plotno | 0.0030 | 0.0037 + wojsko = ok. 0.0057 | ok. 0.0056 | tam, gdzie len i rece miast |
| sol | 0.0099 | 0.044 (4.5 raza za malo) | 0.020 (x2 - pyt. 6) | Volantis 30% (8%), Stormlands 26%, Dorne 11%; King's Landing 4% (19%) |
| zelazo (lupa) | 0.0050 | 0.0017 | 0.0050, inny podzial | Reach 23% (11%), King's Landing 11%; Wyspy 3% (15%) |
| drewno | 0.040 | 0.066 | 0.040, inny podzial | Reach 18%, Polnoc 13% (drwale); las wsi wedlug ludzi i klimatu |

**Co zobaczysz jako gracz. [R]** Ludna wies w Twoim lennie wywozi kilka razy wiecej towaru niz pusta (wies Reach srednio 2.1 raza wiecej niz przecietna, wies Polnocy 0.7, Dorne 0.5, Wysp 0.2); w dymku produkcji wsi dojdzie linia "People of this land". Zlota da wiecej, ale mniej niz towaru: na poczatku w ludnych krainach bedzie taniej (duzo towaru, a pieniedzy w obiegu tyle co dzis), w pustych drozej - o ile, **nie wiem** (pomiar w tescie); z czasem handel to wyrownuje. Twoj warsztat w miescie: 6 roboczodni dziennie razy skala rynku (srednio miasto Reach 13, Polnocy 4, Wysp 1). Pobor: szansa na ochotnika w pustym miejscu u notabla razy skala wsi, w granicach 0.1-3 (wies Reach srednio x2.1, Polnocy x0.7, Wysp x0.2).

**Poprawki "na glowe" (Twoj pomysl: klimat, dobrobyt, technologia).** Policzone: zima i spustoszenie juz tna plon (zostaja); budowle warowni juz daja wsi +5/10/15% (zostaja - to jest "rozwoj"); klimat decyduje, co rosnie i ile jest lasu (zostaje), ale NIE dodaje wydajnosci na glowe, bo zyzna ziemia jest juz policzona w liczbie ludzi - drugi raz bylby podwojnym liczeniem; narzedzia zelazne maja miare w grze (wsie kupuja je w miescie) - proponuje pozniej, w granicach +-10%; "technologia" kultury nie ma dzis w grze dobrej miary - zostawiam. **Czego regula NIE zmienia:** liczby ludzi w ksiedze, hearth i dobrobytu gry, cen bazowych, zoldu zolnierza, limitu druzyny pana, liczby osad, murow, glodu i spichlerzy (one juz dzis licza od ludzi), Twoich decyzji z 13.10 (danina, doplata wedlug potrzeby, tarczowe, lud buduje miasto, lup miast, rozbici wracaja).

**Co jest niepewne. [R]**
1. **Tempo pieniadza.** Towar zmienia sie od razu, zloto nie. W pierwszych miesiacach dochody panow zmienia sie o kilkanascie procent (Reach +17%, Polnoc -10%, Dorne -13%), nie dwukrotnie. Czy i kiedy dojda do granicy - nie wiem.
2. **Lud ludnych miast nie udzwignie wiekszego koszyka.** Po zmianie potrzebuje 2-2.5 raza wiecej, a zarabia prawie tyle co dzis (jego zarobek idzie za wojskiem, dworem i handlem miasta, nie za liczba ludzi). Oszczednosci starcza mu w Braavos na ok. tydzien, w Qarth na 2 miesiace, w Reach na 3; potem stac go na 30-45% koszyka, dopoki zloto nie doplynie [S]. W pustych krainach odwrotnie: lud ma kilka razy wiecej, niz potrzebuje. Lekarstwa: wlaczac skale stopniowo albo - lepiej - nowa kampania z zlotem startowym wedlug ludzi (pyt. 7); do policzenia w pomiarze na sucho, przed kodem.
3. **Sila krolestw** zmieni sie mocno - wynik wojen pokaze dopiero bieg roczny. Suma wojska swiata w granicy: 131-167 tys. wobec 163 tys. dzis, bo bogate krolestwa hamuje limit druzyn, a biedne traca; prog 85 tys. w polu trzeba sprawdzic na sucho przed kodem.
4. **Qarth** musi sprowadzac morzem prawie trzy razy wiecej jedzenia niz dzis (610 zamiast 219 sztuk dziennie) - nie wiem, czy karawany nadaza.
5. **Nocna Straz, Sarnor, Zelazne Wyspy i Wolni Ludzie** przy prostej proporcji prawie znikaja - potrzebne Twoje decyzje (rozdz. 10).
6. **Zalogi i rody sa "na twierdze" i "na krolestwo", nie "na ludzi".** Zelazne Wyspy placa dzis zalogom 14.9 tys. zl dziennie, a w granicy maja 5.6 tys. stalego dochodu; Dorne ma 42 rody na 13.4 tys. zl dziennie. Biedne krolestwa z wieloma twierdzami nie utrzymaja zalog - regula tego nie rozwiazuje, a to rozstrzygnie wojny.
7. **Wojsko je i ubiera sie w prawdziwych kilogramach**, a targ jest pomniejszony ok. 37 razy - dlatego 0.3% ludzi (wojsko) wykupuje polowe plotna swiata; samo "od ludzi" tego nie naprawi, stad propozycja wyzszej stawki lnu albo wiekszej liczby wsi lnianych (pyt. 6).

## 1. Jak jest dzis - ogniwo po ogniwie

Ksiega ludzi (A\PopulationLaw.cs): tabela 30 kultur z ludnoscia i udzialem miast (:42-74 (*): Reach 8.0 mln / 10%, Polnoc 3.0 mln / 8%, Dorne 1.5 mln / 8%, Wyspy 0.5 mln / 5%, Wolni Ludzie 0.3 mln, Nocna Straz 0.02 mln, Qarth 4.0 mln / 37%, Sarnor 0.1 mln / 20%); wies = hearth x k_v, miasto = stan zalozony raz, zamek = 0 (`PeopleOf` :224-233 (*), `KFor` :317-325 (*)); k_v zamrozone na osade w zapisie. Liczby krain to szacunki z `docs\POPULACJA-WESTEROS-ESSOS.md`, nie kanon [RK 1.4].

| ogniwo | wzor dzis | plik:linia | od czego zalezy | werdykt |
|---|---|---|---|---|
| plon i wydobycie wsi | stawka typu x (poziom hearth + 1) x 0.5; poziomy przy 200 / 600 hearth | G\...GameComponents\DefaultVillageProductionCalculatorModel.cs:31 (*), G\...Settlements\Village.cs:320-331 (*) | prog hearth: 520 z 569 wsi ma mnoznik 1.0 [P RK] | PLASKIE |
| nasze mnozniki plonu | ruda, drewno, rece po spustoszeniu, rece w wojnie, regulator swiata, hodowla | A\MaterialLaw.cs:173-238 (*) | wzgledny ubytek rak, nie liczba ludzi | PLASKIE |
| magazyn i woz wsi | magazyn = 5 dob produkcji z modelu; woz zabiera caly magazyn (poprawka 122) | G\...\Village.cs:248-257 (*), A\MarketCarts.cs:48-51 (*) | idzie za modelem produkcji | przeskaluje sie samo |
| las wsi | 4 ladunki na wies x klimat x regulator | A\VillageWoodlot.cs:187-192 (*) | klimat krainy | PLASKIE |
| spichlerz okregu i glod | plon = 1.2 x ludzie x q; jedza ludzie 1:1 | A\Famine.cs:571-581 (*), A\Settings.cs:838 (*) | ludzie ksiegi | **JUZ OD LUDZI** |
| zywnosc warowni (licznik Food) | zuzycie dobrobyt / 40 + zaloga / 20; doplyw 15 (10) + (poziom + 1) x 6 na wies + zywnosc sprzedana mieszczanom | G\...\DefaultSettlementFoodModel.cs:47-48, 65-75, 82-91 (*) | dobrobyt, liczba wsi | PLASKIE |
| popyt miasta | popyt bazowy x dobrobyt (+ zbytki ponad 3000) | G\...\DefaultSettlementEconomyModel.cs:61-73 (*) | dobrobyt (wszedzie 5-8 tys.) | PLASKIE |
| kiesa ludu: glowy klas i koszyk | glowy BK z kalibracji x r (r = ludzie dzis / ludzie przy kalibracji); potrzeba = min(wydatek gry, koszyk) | A\TownFolk.Probe.cs:711-727 (*), A\TownFolk.cs:428-434 (*), A\TownFolk.Live.cs:725-736 (*) | glowy BannerKings (12.8-19.1 tys. na miasto) | PLASKIE |
| zapas kupcow i zawor kasy miasta | zapas 10 000 + 12 x dobrobyt; 7% nadwyzki dziennie panu i koronie | A\Seal31.cs:100 (*), A\TownPurse.cs:31 (*), A\Settings.cs:745 (*) | dobrobyt, obrot | PLASKIE |
| rece rzemieslnikow broni | max(dawny wzor z dobrobytu, min(1000, 0.10 x ludzie rynku / 1000 x wskaznik placy)) | A\WorkshopLaw.cs:799-812 (*), ludnosc rynku :829-857 (*) | ludzie rynku | **JUZ OD LUDZI** (35 z 97 miast na dawnej podlodze) |
| warsztaty notabli | stale 6 roboczodni na warsztat | A\WorkshopLaw.cs:817-822 (*), A\Settings.cs:581 (*) | liczba warsztatow | PLASKIE |
| podatek wsi i oplata stala | BK: 70% utargu taboru; oplata 4 zl / rok na glowe BK | A\VillageTakings.cs:16-30 (*), A\TownFolk.Live.cs:927 (*), A\Settings.cs:1652-1653 (*) | utarg (plaski), glowy BK | PLASKIE |
| renta "nalezna" | ludzie x 40 zl / rok | A\Settings.cs:735 (*), A\PopulationLaw.cs:1038-1042 | ludzie | tylko liczba: zaplacone 2.4-2.6% [P] |
| tempo werbunku | miejsca u notabli x szansa BK x chec | A\Levy.cs:98-141 (*) | liczba notabli | PLASKIE |
| sufit werbunku | 20% mezczyzn krainy (Wyspy do 40%, Wolni Ludzie bez hamulca) | A\FreeHands.cs:119-128, 166-182 (*) | ludzie | OD LUDZI, wiaze tylko male krainy |
| milicja | 2 (wies 0.5) + hearth / 400 + dobrobyt / 1000 - 2.5% stanu; u nas z wolnych rak | G\...\DefaultSettlementMilitiaModel.cs:101-120 (*), A\ArmsFromPeople.cs:28-29 (*) | osada | PLASKIE (93 na osade [P]) |
| pulap zoldu rodu | pokoj 0.28 x D, wojna 0.60 x D + skrzynia | A\ClanBudget.cs:193-212 (*) | dochod staly D | posrednio PLASKIE |

Sufit, ktorego sie obawialismy, a ktorego juz nie ma: BannerKings kasowal 1% kiesy wsi ponad 10 000 + 3 x hearth (BK\...\BKEconomyModel.cs:610-626 (*), BK\...\BKSettlementBehavior.cs:463-491 (*)) - nasz prefiks to pomija (A\TownPurse.cs:738-756 (*), klucz `TownPurseNoBkSkimVillages` A\Settings.cs:1018 (*)). Bogatsza wies nie zgubi zlota.

## 2. Dane: rozrzut na czlowieka i korelacje (bieg roczny) [P, RD rozdz. 2-3]

- Miary wielkosci: ludzi na wies 1.3-302 tys. (p90/p10 = 10), ludzi na miasto 2.5-626 tys. (31x) - a hearth 316-512 (1.6x), dobrobyt 4.8-7.7 tys. (1.6x), glowy BK miasta 11.3-19.8 tys. (1.8x).
- Na 1000 ludzi krolestwa roznia sie 7-11 razy (p90/p10): produkcja wsi 10.6x, dochod ludu 7.1x, dochod rodow z ziemi 7.8x, wojsko 9.3x. Na osade te same miary sa rowne: milicja 1.9x, zaloga miasta 1.4x.
- Regresja po 29 krolestwach (ludzie + osady naraz): dochod z ziemi - za 1 osade 224 zl/d (t 15.5), za 1000 ludzi 0.43 zl/d [R: recenzja liczy 195 zl za osade i 0.42 zl za 1000 ludzi przy osadzie = twierdza albo wies (796 osad), R2 0.93; zwykla srednia 205 zl na osade - stad "ok. 200" w rozdz. 0]; wojsko - 203 ludzi za osade, -0.07 za 1000 ludzi; milicja 93.2 za osade (R2 0.99); zboze na targ 11.9 szt. za osade, 0.008 za 1000 ludzi. Waga osad 0.83-0.99, waga ludzi ok. 0.
- Jedyna miara rowna na czlowieka: plon ksiegi racji (1 150-1 200 racji na 1000 ludzi wsi; korelacja z ludzmi 0.98).
- Renta nalezna od glowy 5.66 mln zl/d, zaplacona 149 tys. (2.6%): Reach placi 1.4% naleznej, Polnoc 6.5%, Dorne 7.8%, Sarnor 32%, Nocna Straz 106%.
- Na 1000 ludzi dochod rodow z ziemi: Qarth 0.99, Reach 2.11, Polnoc 6.00, Dorne 9.04, Wyspy 11.4, Sarnor 33.9, Nocna Straz 125 zl/d.
- Skala swiata "na czlowieka dzis": wsie wystawiaja na targ towar za 4.17 zl na 1000 ludzi dziennie [S: wartosc produkcji w cenach bazowych; R: zmierzony utarg taborow wsi to 312-332 tys. zl/d = 6.1-6.4 zl na 1000 ludzi - linie "Obieg" i "Prawa panskie" doby 109199]; lud miast zarabia 11.4 zl; rody z ziemi maja 3.17 zl; wojsko 3.16 czlowieka na 1000 (1.17% mezczyzn).

Wniosek: "zarobek idzie za liczba osad, nie ludzi" - potwierdzone; ksiega ludzi jest gotowa, tylko gospodarka jej nie czyta.

## 3. Regula - szczegoly, parametry, punkty wpiecia

### 3.1 Trzy drogi i wybor

| droga | na czym polega | za | przeciw | werdykt |
|---|---|---|---|---|
| (A) czynnik mnozacy WYNIKI | gra i BK licza po swojemu, my mnozymy plon, spozycie, zarobek, podatki, pobor | jedno miejsce na ogniwo | mnozenie zarobku i podatkow = zloto z niczego albo w nicosc (lamie obieg zamkniety) | NIE w czystej postaci |
| (B) przestawienie miar gry | hearth = ludzie / 189.2, dobrobyt = ludzie miasta; zdjac progi | wzory gry i BK "same" ida za ludzmi | hearth czyta ok. 25 miejsc gry i ok. 15 BK naraz (milicja, tabory, wartosc osady, szkody rabunku, zadania +30..+80 hearth, sufit ludnosci BK hearth x 4 - RK 3.4); progi 200 / 600 daja trzy schodki 0.5 / 1 / 1.5 zamiast proporcji; dobrobyt sam wraca do 6-9 tys. (koszty mieszkan G\...\DefaultSettlementProsperityModel.cs:83-130, za RK); nasza ksiega ma ludzi = hearth x k_v - zmiana hearth zmienia ludzi; wszystkie nasze podlogi i kroki sa w punktach hearth; lamie "jedna zmiana naraz" | NIE |
| (C) mieszana: skala u KORZENI | nie ruszamy hearth ani dobrobytu; skalujemy tylko wielkosci fizyczne u zrodla (ile ludzie wytworza, ile rynek kupi i przerobi, jak szybko przychodza ochotnicy) w naszych istniejacych postfiksach; pieniadz idzie sam istniejacymi, zamknietymi drogami; wlasne ksiegi (glowy kiesy ludu) przestawiamy z glow BK na ludzi ksiegi | zadnego zlota z niczego; kazde ogniwo ma jeden czynnik, wylacznik i pomiar; hearth zostaje "ludzmi w jednostkach lokalnych", k_v jest mostem | wiecej paczek niz w (A); czesc BK (rzemieslnicy BK) zostaje plaska do osobnego rozpoznania | **TAK** |

### 3.2 Dwie skale i tabela stawek

**Skala wsi u_v** = ludzie okregu wsi / ludzie sredniej wsi swiata.
- ludzie okregu wsi = hearth x k_v + uchodzcy tej wsi (dom + poza domem). Uwaga na podwojne liczenie: `PeopleOf` daje samych ludzi w domu (A\PopulationLaw.cs:230 (*)), a `Devastation.YieldFactor` = (dom / (dom + uchodzcy)) ^ e (A\Devastation.cs:578-599 (*)) juz zdejmuje brakujace rece - dlatego skala liczy sie od dom + uchodzcy, a YieldFactor zostaje.
- srednia wsi swiata = 72.3 tys. ludzi [P SK, RD], zamrozona przy kalibracji kampanii i trzymana w zapisie obok `WorldPeoplePerHearth` (A\PopulationLaw.cs:356-383 (*)). Gotowa miara w kodzie: `PeopleUnit.Base` (A\PeopleUnit.cs:102-110 (*)).
- Rozklad u_v po 569 wsiach [S z P, SK]: min 0.017, 10% wsi ponizej 0.22, mediana 0.83, 90% ponizej 2.15, max 4.18; ponizej 0.10 - 28 wsi, ponizej 0.25 - 86, powyzej 2 - 75, powyzej 3 - 22, powyzej 4 - 3. Srednio wedlug krolestw: Qarth 3.16, Volantis 2.88, Reach 2.13 (0.41-4.18), Braavos 1.66, Dolina 1.23, King's Landing 1.13, Dorzecze 1.11, Stormlands 0.93, Polnoc 0.68, Dorne 0.49, Wyspy 0.22, Wolni Ludzie 0.22, Sarnor 0.08, Nocna Straz 0.02.

**Skala rynku m_t** = ludzie rynku miasta / ludzie sredniego rynku swiata.
- ludzie rynku = miasto + wsie, ktore w nim handluja (TradeBound) - gotowa funkcja `WorkshopLaw.MarketPeople` (A\WorkshopLaw.cs:829-857 (*)), liczona raz na dobe; dzis 52.04 mln w rynkach 97 miast = srednio 536 tys. [P log l.305].
- Dlaczego rynek, a nie ludzie samego miasta: w ksiedze 8 miast ma 0 ludzi wlasnych (Castle Black, Vaes Dothrak, Vaes Diaf, Thenn, Hardhome, Frostfang's Camp, Driftwood Hall, Elyria), miasta Wysp po 2.5-4.6 tys., Qarth 626 tys., New Ghys wiecej niz Krolewska Przystan [P RD 4.3] - skala wprost od ludzi miasta mialaby rozrzut 0-5.8. Rynek nie zeruje zadnego miasta i jest tym, co Jeff nazywa "okregiem".
- Srednio wedlug krolestw [S: ludzie krolestwa / liczba miast, SK]: Qarth 2.50, Braavos 2.44, Volantis 2.35, Reach 2.25, King's Landing 1.50, Stormlands 1.30, Dolina 1.01, Dorzecze 0.86, Polnoc 0.61, Dorne 0.53, Dragonstone 0.24, Wyspy 0.15, Sarnor 0.09, Nocna Straz 0.04. Rozkladu na pojedyncze miasta nie znam (do pomiaru w P0).

**Stawka towaru** = sztuk na 1000 ludzi wsi danego typu na dobe. Liczona raz, przy kalibracji: dzisiejsza produkcja typu w swiecie / ludzie wsi tego typu - tak, ze w dniu wlaczenia suma swiata kazdego typu wsi zostaje co do sztuki (razem ze zdjeciem progu hearth). Wzor wsi:

    plon(wies, towar) = baza gry(typ wsi, towar) x u_v x g(typ) x [poprawki: zima, rece po spustoszeniu, rece w wojnie, budowle warowni, regulator swiata]
    g(typ) = suma((poziom + 1) / 2) po wsiach typu / suma(u_v) po wsiach typu      (liczone raz; trzyma sume swiata typu)

Stawki wynikowe [S z P, RT 6.2]: zboze 0.765 (wies zbozowa), ryby 0.501, len 0.333, sol 0.770, glina 0.316, winogrona 0.139, welna 0.129, oliwki 0.129, ruda (lupa) 0.123, daktyle 0.118, drewno drwali 0.115, las wsi 0.045, srebro 0.044, jedwab 0.039, skory z uboju 0.029, futra 0.015. Poprawki stawek wedlug wzorca (len, sol) - rozdz. 4A i paczka P5.

### 3.3 Co sie skaluje, a co nie

| co | czym | jak | punkt wpiecia |
|---|---|---|---|
| plon i wydobycie wsi (kazdy towar listy typu, takze zwierzeta i konie) | u_v x g(typ) | zamiast progu hearth: czynnik = u_v x g / ((poziom + 1) x 0.5), wzorem "czynnik x suma" | A\MaterialLaw.cs: po mnozniku wydobycia i powtorzen BK (:182-201 (*)), przed rekami spustoszenia (:205 (*)); ryby wiosek pobocznych (:231) i sufit dymarki (:234) zostaja PO nim |
| las wsi | u_v x g(las), klimat zostaje | ten sam czynnik w `PiecesPerDay` | A\VillageWoodlot.cs:187-192 (*) |
| magazyn i woz wsi | - | same: wolaja ten sam model | G\...\Village.cs:248-257 (*), G\...\DefaultPartySizeLimitModel.cs:415-425 (*) |
| glowy kiesy ludu miasta (W / R / P) | m_t | glowy = stala symbolu x ludzie rynku, w proporcjach klas BK z kalibracji; suma swiata glow bez zmian | A\TownFolk.cs:428-434 (*) (`Scale`), A\TownFolk.Probe.cs:711-727 (*). [R] `Scale` liczy dzis ludzi WLASNYCH osady (`PeopleOf`), a miasto bez ludzi wlasnych ma 1 - przejscie na ludzi rynku to przepisanie funkcji, nie parametr |
| budzet konsumpcji miasta i glebokosc targu [R] | m_t | u ZRODLA popytu: wynik `GetDailyDemandForCategory` x m_t - karmi slownik budzetu (`UpdateDemandShift`), popyt wzoru ceny (`GetEstimatedDemandForCategory`) i czesc domowa `RawPrice.UseDemand`; osobno czlon dobrobytu wiersza BK (dobrobyt / 1000 / indeks) x m_t. Kotwica `min(wydatek gry, koszyk)` zostaje bez zmian - wydatek gry przyjdzie juz przeskalowany | nasz postfiks popytu A\HistoricalPrices.cs:1170-1174 (*R); czlon wiersza: A\TownFolk.Live.cs:417-430 (*). UWAGA [R]: dzis kotwica i slownik umieja tylko CIAC (`Clamp01` :349, `if (fac >= 1) return` :423, `min(1, koszyk / wydatek)` :736 (*R)) - wersja autora "potrzeba = min(wydatek gry x m_t, koszyk)" w tej latce nie zadziala w gore; galaz "w gore" dla czlonu wiersza to nowy kod, dotad martwy (zasada 4) |
| zapas kupcow (prog zaworu) | m_t | (10 000 + 12 x dobrobyt) x m_t | A\Seal31.cs:100 (*) - jedna funkcja `TownReserve`, czytaja ja zawor, podatki i nasze zakupy. [R] Dojscie do nowego zapasu stopniowe albo z kasami startowymi wedlug ludzi (inaczej przerwa zaworu w ludnych miastach i jednorazowa wyplata w pustych - rozdz. 9, ryzyko 12); dwie kopie wzoru w kodzie logu do poprawienia razem: A\Measure169c.cs:351, A\MoneyLedger.Obieg.cs:525 (*R) |
| rece warsztatow notabli (takze warsztatu gracza - jedna regula) | m_t | 6 roboczodni x m_t | A\WorkshopLaw.cs:820 (*) |
| licznik Food warowni | 1 / m_t na skladniku "sprzedane mieszczanom" | neutralizacja: licznik zostaje w swoich jednostkach (Famine.QOf juz przelicza ludzi na jednostke - A\Famine.cs:505-515 (*)) | postfiks na `CalculateTownFoodStocksChange` (jak A\Famine.cs:462-473 (*)); zrodlo skladnika G\...\DefaultSettlementFoodModel.cs:82-91 (*). [R] Wykonalne niezaleznie od modelu pod ROT: model BK wola model gry i doklada tylko "Local farmland" (BK\...\BKFoodModel.cs:46-59 (*R)); skladnik liczyc z `Town.SoldItems`, jak gra. Bez tej neutralizacji P2 nie wolno wlaczyc: miasto o m 0.15 glodowaloby w liczniku gry, miasto o m 2.5 mialoby wieczna nadwyzke i premie dobrobytu |
| glowy wsi (oplata stala, zakupy wsi w miescie) i podzamcza | u_v (podzamcze: ludzie wsi zamku) | glowy = srednia swiata glow BK x u_v | A\TownFolk.Live.cs:927 (*) (`VillHeads`). [R] Budzet konsumpcji zamku idzie z popytu gry (A\CastleMarket.cs:208 (*R)): bez skali u zrodla popytu takze dla zamkow kotwica tylko obetnie biedne podzamcza, a bogatym nie doda - spozycie zamkow swiata (2 038-2 228 szt. zywnosci / d [P]) by spadlo |
| tempo werbunku | u_v (wies), m_t (miasto) x udzial zbrojnych ludu | szansa pustego miejsca x min(3, max(0.1, skala)); wynik nie ponad 1 | A\Levy.cs:136-137 (*) |
| przyrost milicji | jak wyzej | skladnik przyrostu x skala; ubytek 2.5% stanu bez zmian (rownowaga proporcjonalna) | G\...\DefaultSettlementMilitiaModel.cs:104-120 (*), nasza bramka A\ArmsFromPeople.cs:28-29 (*) |
| rece rzemieslnikow broni | juz od ludzi | bez zmian; podloga "dawny wzor" do zdjecia pozniej (P7) | A\WorkshopLaw.cs:799-812 (*) |

"Udzial zbrojnych ludu" ma zrodlo w tym, co juz jest w kodzie: prog wolnych rak krainy / 0.20 (A\FreeHands.cs:119-128 (*), A\Settings.cs:863 (*)) - zwykla kraina 1, Zelazne Wyspy 2 (prog 40%), Wolni Ludzie 5 (bez hamulca; tnie gorna granica 3.0); Dothrakowie maja dzis 1, choc sa na liscie najezdzcow budzetu rodow (A\ClanBudget.cs:54 (*)) - proponuje prog 60% (x3) razem z decyzja z pytania 2; Nocna Straz poza regula (jej ludzie ida przez WatchLaw).

**Czego NIE skalujemy i dlaczego:** hearth i dobrobyt (miary gry - 40 cudzych wzorow, RK 3.4); model zywnosci wsi `CalculateDailyFoodProductionAmount` (G\...\DefaultVillageProductionCalculatorModel.cs:76-88 (*)) - czyta go spichlerz i mnozy przez ludzi, skala tutaj policzylaby ludzi dwa razy (A\Famine.cs:577-579 (*)); skladnik wsi w zywnosci warowni (A\VillageBook.cs:1653-1669 (*)); podatki, renty, zawor, daniny, zold (ida same za obrotem - mnozenie = zloto z niczego); limit druzyny pana (ranga rodu, nie ludzie); liczba warsztatow, notabli, miejsc u notabli, osad, mury; ceny bazowe; ludnosc BannerKings (zyje swoim zyciem, przestajemy jej uzywac jako "glow").

### 3.4 Skala w czasie

Skala czyta ksiege co dobe (pamiec doby podmieniana jedna referencja, jak A\FreeHands.cs:45, 187 - za RK; postfiks produkcji bywa wolany z watkow AI). Co ja zmienia: przyrost naturalny (ok. +0.5% rocznie [P]), glod, rabunek i spustoszenie (uchodzcy wracaja w rok), pobor (rekrut = 1 czlowiek z domu), powroty rozbitych i weteranow - wszystko to juz dzis idzie przez `PeopleBook.Move` (za RK 1.5). Stale odniesienia (72.3 tys. na wies, 536 tys. na rynek, g typow, stala symbolu glow) sa zamrozone przy kalibracji i w zapisie: swiat, ktory sie wyludnia, wytwarza mniej - nie wraca sztucznie do sumy.

### 3.5 Granice

| granica | wartosc | uzasadnienie liczba |
|---|---|---|
| u_v dolna | brak (czysta proporcja); przy braku przelicznika (k = 0) - dzisiejszy wzor gry | Jeff: "wies to symbol"; podloga bylaby towarem z niczego. 28 wsi ma u < 0.10 (Dar, Sarnor, Ibben) - zboze 1-5 szt./d [S] |
| u_v gorna | 6.0 (bezpiecznik, logowany) | najwieksza wies dzis 4.18 [P]; chroni przed bledem przelicznika (audyt 09.10: Lotus Bay 0.75 -> 2.2 mln w 39 dob, A\PopulationLaw.cs:150-151 (*)) |
| m_t dolna | 0.05 | miasto musi dzialac jako rynek; ponizej dzis: Castle Black 0.01, Kyth 0.05, Saath 0.06 [S, SK] |
| m_t gorna | 4.0 (bezpiecznik) | najwiekszy region miejski 1.28 mln = 2.4 sredniej [P]; z wsiami zamkow szac. do 3 [S] |
| m_t w czasie [R] | srednia z 28 dob albo przypisanie wsi do targu zamrozone przy kalibracji (wybor po pomiarze P0) | gra przypisuje wsie zamkow do targu od nowa przy kazdej wojnie, pokoju i zmianie pana (G\...CampaignBehaviors\VillageTradeBoundCampaignBehavior.cs:64 (*R)); w biegu rocznym 311 z 318 rodow bylo w wojnie [P] - bez wygladzenia glowy, budzet i zapas kupcow miasta skacza o ludzi calych okregow zamkow |
| werbunek i milicja | 0.10-3.0 | silnik: szansa na miejsce <= 1 na dobe; liczba miejsc stala |
| regulator swiata | bez zmian: 0.1-1.3 wokol stawki | A\HarvestValve.cs:31-36 (*); przy wlaczeniu pomiar od nowa (stan `arm_harvestvalve`, :39-40 (*)) |

### 3.6 Poprawki wydajnosci na czlowieka - policzone, nie dodane na slepo

| poprawka | miara w swiecie gry | stan | decyzja projektu |
|---|---|---|---|
| pora roku (zima) | `WinterBite` wedlug pory, szerokosci i krainy (A\WinterBite.cs:13-33, za RK) | jest: bieg zimy - plon -40.8% [P STAN-PRAC] | zostaje; nie dodawac drugi raz |
| bezpieczenstwo | rece po spustoszeniu i w wojnie (A\MaterialLaw.cs:205-217 (*)) | jest | zostaje |
| rozwoj osady | budowle warowni +5/10/15% plonu wsi (G\...\DefaultVillageProductionCalculatorModel.cs:63-66 (*)); wskaznik placy dobrobyt / 4800 w rekach miast (A\TownWage.cs:19-25, za RK) | jest | zostaje; po paczce "lud buduje miasto" bogate miasto szybciej podniesie te budowle - to jest dobrobyt w wydajnosci |
| klimat staly | typ wsi dopasowany do klimatu (28 podmian, A\VillageClimate.cs), klasy lasu 0.32 pustynia - 1.29 las [P RT 7] | jest dla "co" i dla lasu | NIE dodawac wydajnosci pola na glowe: historycznie plon z ziarna 3-4:1 na zimnej polnocy i 6-8:1 na najlepszych ziemiach [S RT 7] przekladal sie na gestosc ludzi, nie na bogactwo glowy - a gestosc jest juz w ksiedze (wies Reach 3.1 raza ludniejsza niz Polnocy). Drugi mnoznik = podwojne liczenie |
| narzedzia zelazne | wies kupuje narzedzia w miescie targowym: `DVNeed` / `DVBuy` (A\TownFolk.Live.cs:945-958, za RK); swiat 146 narzedzi / d, z tego wsie 87 [P RT] | miara jest, ale dla calego koszyka, nie osobno dla narzedzi | POZNIEJ (P7): mnoznik 0.90 + 0.20 x udzial zaspokojonej potrzeby narzedzi z 28 dob, czyli 0.90-1.10 (historia: +10-30% wydajnosci pracy, slaba liczba [S RT 7]); najpierw rozdzielic pomiar (P0) |
| technologia kultury | innowacje BK (plug x1.12, chomato x1.25, trojpolowka x1.4 - BK\...\LandData.cs:334-353, za RK) | siedza w modelu produkcji BK, ktory w tym zestawie nie biegnie [P log l.464]; czy innowacje postepuja - nie wiem | ZOSTAWIC: brak dobrej miary |

Laczna poprawka na glowe z historii miesci sie w ok. 0.6-1.5 [S RT 7]; wszystko ponad to jest roznica liczby ludzi - i to ona robi tu robote.

## 4. Skutki na krolestwo [S] i bilans zywnosci swiata

**[R] Tabela przeliczona przez recenzje** (`skrypty-rec\r01`, `r03`, `r04`; "dzis" = srednia 28 dob, rod liczony do krolestwa doba po dobie). Zalozenia rachunku autora zostaja, ale trzeba je czytac tak:
- **Granica** = suma swiata kazdej wielkosci jak dzis; dochod rodow z ziemi (163 tys. zl/d) i od korony (457 tys. zl/d) podzielony wedlug ludzi; reszta wplywu (lupy 362 tys. zl/d, kontrakty, majatki) bez zmian; D = ziemia + korona. To stan docelowy - spojny, gdy zloto lezy juz wedlug ludzi - a nie prognoza na pierwszy rok; czy i kiedy swiat dojdzie do niego sam, patrz rozdz. 5.
- **Pierwszy krok** = za ludzmi idzie od razu tylko ta czesc D, ktora jest wzieta wprost z gospodarki ludzi: 119 tys. z 620 tys. zl/d (19%; podatek wsi BK, oplaty stale wsi, podatki od zarobku ludu, farma gmin, cla). Reszta D (zawor i danina kas, zwrot 70% zoldu) to pieniadz panow wracajacy przez wojsko i miasta - zostaje przy dzisiejszym podziale, dopoki nie przesunie sie wojsko i obrot. To obraz pierwszych miesiecy, dopoki lud ludnych miast placi za wiekszy koszyk z oszczednosci; co potem - zalezy od tego, czy zloto doplynie do ludnych krain (rozdz. 5) - nie wiem.
- **Wojsko w granicy**: przy D rosnacym od "+35% przyrostu D" do "pelny przyrost, nie wiecej niz 64% limitu druzyn + zalogi x1.5"; przy D malejacym od "proporcjonalnie" do "polowa spadku" (rody z luzem). Widelki z reki, nie z modelu.
- **Jedzenie A** (definicja autora) = zywnosc pierwotna wsi na targ minus spozycie mieszczan i zamkow (dzis wedlug udzialu w wydatkach ludu na jedzenie, po zmianie wedlug ludzi; "zywnosc wsi" gry i ryby spoza typu wsi zostaja plaskie). **Jedzenie B** = A minus przerob na chleb i piwo (dzis wedlug liczby miast, po zmianie pol na pol z ludzmi) i minus jedzenie druzyn (wedlug dzisiejszego udzialu w druzynach). Suma swiata B jest ujemna (-1 516), bo 1 955 szt./d zywnosci powstaje poza wsiami (uboj, rzemieslnicy BK, ticki osad).
To rachunek na papierze, nie bieg gry.

| krolestwo | ludzie tys. | udzial % | u wsi | m rynku | wplyw rodow tys. zl/d: dzis -> granica | D tys. zl/d: dzis -> pierwszy krok -> granica | wojsko dzis | limit druzyn | wojsko w granicy (nisko-wysoko) | jedzenie A: dzis -> po | jedzenie B: dzis -> po |
|---|---:|---:|---:|---:|---|---|---:|---:|---|---|---|
| The Reach | 8 345 | 16.21 | 2.13 | 2.25 | 82.7 -> 130.0 | 53.2 -> 62.3 -> 100.5 | 11 636 | 12 118 | 15 255-16 716 | +571 -> +1 021 | +312 -> +690 |
| Volantis | 4 993 | 9.70 | 2.89 | 2.35 | 54.8 -> 93.1 | 21.8 -> 29.2 -> 60.1 | 5 462 | 5 721 | 8 085 (limit)-8 816 | +53 -> -52 | -76 -> -225 |
| King's Landing | 4 781 | 9.29 | 1.13 | 1.50 | 100.2 -> 112.0 | 45.9 -> 48.1 -> 57.6 | 13 003 | 11 155 | 14 164-16 320 | +167 -> +53 | -84 -> -223 |
| Qarth | 3 983 | 7.74 | 3.16 | 2.50 | 41.7 -> 72.6 | 17.1 -> 23.1 -> 48.0 | 4 976 | 3 637 | 5 418 (limit)-8 117 | -219 -> -610 | -343 -> -770 |
| The North | 3 227 | 6.27 | 0.68 | 0.61 | 108.9 -> 64.0 | 83.7 -> 75.1 -> 38.9 | 17 555 | 13 866 | 8 148-12 851 | +210 -> +352 | -165 -> +9 |
| The Vale | 3 222 | 6.26 | 1.23 | 1.01 | 68.3 -> 56.5 | 50.6 -> 48.3 -> 38.8 | 11 332 | 9 496 | 8 695-10 013 | +76 -> +376 | -165 -> +134 |
| Stormlands | 2 757 | 5.36 | 0.94 | 1.30 | 58.7 -> 55.0 | 36.9 -> 36.2 -> 33.2 | 10 899 | 8 925 | 9 807-10 353 | +177 -> +150 | -23 -> -60 |
| Riverlands | 2 741 | 5.32 | 1.11 | 0.86 | 70.7 -> 68.9 | 34.8 -> 34.4 -> 33.0 | 10 223 | 11 595 | 9 703-9 963 | -10 -> +222 | -252 -> -13 |
| Braavos | 2 593 | 5.04 | 1.66 | 2.44 | 19.8 -> 42.1 | 9.0 -> 13.3 -> 31.2 | 4 024 | 3 528 | 5 355 (limit)-7 515 | +63 -> +5 | -20 -> -101 |
| Targaryen (Meereen) | 1 972 | 3.83 | 0.95 | 0.93 | 90.6 -> 89.7 | 24.6 -> 24.5 -> 23.8 | 4 533 | 5 437 | 4 370-4 451 | +57 -> -15 | -69 -> -139 |
| Pentos | 1 487 | 2.89 | 1.06 | 1.40 | 23.1 -> 32.8 | 8.2 -> 10.0 -> 17.9 | 3 137 | 2 732 | 4 445-4 719 | +130 -> +156 | +68 -> +87 |
| Qohor | 1 487 | 2.89 | 1.06 | 1.40 | 20.4 -> 27.1 | 11.2 -> 12.5 -> 17.9 | 3 080 | 3 271 | 3 721-4 845 | -71 -> -89 | -135 -> -160 |
| Norvos | 1 479 | 2.87 | 1.02 | 1.39 | 22.5 -> 29.3 | 11.1 -> 12.4 -> 17.8 | 3 416 | 3 512 | 4 143-5 275 | -41 -> -60 | -110 -> -135 |
| Dorne | 1 116 | 2.17 | 0.49 | 0.53 | 103.1 -> 71.7 | 44.9 -> 38.8 -> 13.4 | 10 441 | 10 229 | 3 126-6 784 | +241 -> +181 | +26 -> -18 |
| Tyrosh | 1 066 | 2.07 | 0.67 | 1.00 | 29.9 -> 33.0 | 9.8 -> 10.4 -> 12.8 | 4 104 | 3 335 | 4 550-5 131 | +49 -> -18 | -38 -> -105 |
| Targaryen (Aegon) | 1 030 | 2.00 | 0.83 | 0.65 | 38.3 -> 39.6 | 11.1 -> 11.4 -> 12.4 | 3 512 | 3 770 | 3 654-3 919 | -146 -> -78 | -241 -> -164 |
| Myr | 979 | 1.90 | 0.67 | 0.92 | 25.8 -> 26.1 | 11.5 -> 11.5 -> 11.8 | 3 590 | 3 199 | 3 625-3 690 | +31 -> +14 | -48 -> -63 |
| Lys | 887 | 1.72 | 0.68 | 0.84 | 21.5 -> 26.2 | 6.0 -> 6.9 -> 10.7 | 2 394 | 3 140 | 3 051-3 641 | +81 -> -19 | +15 -> -82 |
| Dothraki Horde | 732 | 1.42 | 0.60 | 0.69 | 57.2 -> 25.4 | 40.6 -> 34.5 -> 8.8 | 4 927 | 4 600 | 1 070-2 998 | +43 -> +24 | -56 -> -70 |
| Lorath | 673 | 1.31 | 0.60 | 0.63 | 32.2 -> 27.7 | 12.6 -> 11.7 -> 8.1 | 3 371 | 3 780 | 2 168-2 770 | +123 -> +76 | +46 -> +4 |
| Dragonstone | 506 | 0.98 | 0.44 | 0.24 | 41.8 -> 31.8 | 16.0 -> 14.1 -> 6.1 | 6 484 | 7 491 | 2 462-4 473 | +24 -> +130 | -129 -> +1 |
| Iron Islands | 469 | 0.91 | 0.22 | 0.15 | 31.3 -> 16.9 | 20.1 -> 17.3 -> 5.6 | 6 449 | 8 108 | 1 813-4 131 | +228 -> +107 | +90 -> +11 |
| Ibben | 288 | 0.56 | 0.35 | 0.18 | 9.7 -> 9.3 | 3.9 -> 3.8 -> 3.5 | 2 107 | 3 277 | 1 893-2 000 | -10 -> +49 | -69 -> +9 |
| Free Folk | 287 | 0.56 | 0.22 | 0.18 | 10.9 -> 6.9 | 7.5 -> 6.7 -> 3.5 | 3 366 | 5 859 | 1 558-2 462 | +3 -> +32 | -60 -> -11 |
| Summer Isles | 108 | 0.21 | 0.45 | 0.20 | 2.9 -> 2.4 | 1.9 -> 1.8 -> 1.3 | 253 | 764 | 176-215 | +1 -> +2 | -20 -> -12 |
| Yi Ti Exiles | 100 | 0.19 | 0.37 | 0.19 | 2.7 -> 1.9 | 1.9 -> 1.8 -> 1.2 | 224 | 562 | 140-182 | +15 -> +6 | -5 -> -7 |
| Sarnor | 93 | 0.18 | 0.08 | 0.09 | 24.8 -> 12.6 | 13.2 -> 10.9 -> 1.1 | 1 957 | 4 720 | 166-1 061 | +80 -> +34 | +15 -> -17 |
| Skagos | 50 | 0.10 | 0.18 | 0.10 | 2.6 -> 1.8 | 1.4 -> 1.3 -> 0.6 | 323 | 848 | 137-230 | +1 -> +16 | -21 -> +1 |
| Nights Watch | 20 | 0.04 | 0.02 | 0.04 | 16.3 -> 7.1 | 9.4 -> 7.6 -> 0.2 | 5 817 | 5 242 | 147-2 982 (bez wyjatku) | +164 -> +27 | +44 -> -86 |
| **SWIAT** | 51 471 | 100 | 1.00 | 1.00 | 1 213 (bez zmian) | 620 (bez zmian) | 162 594 | 163 916 | 131 000-166 600 | +2 092 (bez zmian) | -1 516 (bez zmian) |

"u wsi" = srednia skala wsi krolestwa; "m rynku" = ludzie krolestwa / liczba miast wobec sredniej swiata (przyblizenie - prawdziwych rynkow miast nie znam, P0). "(limit)" = widelki dolne wyznacza limit druzyn i zalogi, nie dochod.

Co widac:
- **Ludne poludnie wystawi wiecej, Polnoc mniej - w granicy**: Reach z 11.6 na ok. 15-17 tys. (dalej hamuje limit druzyn: 5.7 tys. w druzynach przy limicie 12.1 tys. [P]), Polnoc z 17.6 na 8-13 tys., Dorne z 10.4 na 3-7 tys. Prosta proporcja "wojsko wedlug ludzi" (Reach 26.4 tys. - RD 5.5) NIE wyjdzie, bo pulap zoldu to jedno, a limit druzyny pana drugie. **[R]** W pierwszym kroku D Reach rosnie o 17%, Polnocy spada o 10%, Dorne o 13% - wojsko zmieni sie odpowiednio malo; tracacych dodatkowo trzyma skrzynia wojenna (w wojnie pulap = 0.60 x D + 0.8 x (zloto ponad rezerwe) / 45, A\ClanBudget.cs:199-207 (*R)), wiec ich wojsko topnieje wolniej niz dochod.
- **Bogate, ale waskie**: Qarth (druzyny 2 916 przy limicie 3 637 [P]), Braavos, Volantis dostaja w granicy 2.8-3.5 raza wiecej dochodu stalego, a nie maja gdzie postawic ludzi - zloto pojdzie w zalogi, oszczednosci i (nie wiem, ile) w kompanie najemne. To zgodne z lore Wolnych Miast, ale grozi spadkiem sumy wojska swiata: 131-167 tys. wobec 163 tys. dzis; partie lordow w wojnie moga zejsc ponizej progu Jeffa 85 tys. - **do policzenia na sucho w P0, przed kodem**.
- **[R] Biedne, ale z wieloma twierdzami**: zalogi sa liczone na twierdze, nie na ludzi. Zold zalog dzis [P]: Wyspy 14.9 tys. zl/d (docelowe D 5.6 tys.), Dorne 10.9 tys. (D 13.4 tys.), Polnoc 20.1 tys. (D 38.9 tys.). W granicy Wyspy nie oplaca polowy zalog, a Dorne nie ma juz za co trzymac druzyn. Liczba rodow tez jest stala: Dorne 42 rody, Wyspy 30, Reach 17 - na rod wypada w granicy 320 / 190 / 5 900 zl dziennie.
- **[R] Ceny i handel**: w wartosci podaz wsi i popyt miasta ida po zmianie razem bardziej niz dzis - stosunek (plon wsi krolestwa) / (popyt jego miast) zalezy juz tylko od udzialu ludnosci wiejskiej (0.63 w Qarth, 0.90 w Reach, 0.92 na Polnocy), a dzis zalezy od liczby wsi na miasto (3.7 w Qarth, 9.8 w Stormlands). Rozjazd robi rodzaj wsi i **pieniadz**: zloto lezy dzis rowno na osade, wiec w ludnych krainach na te sama monete przypadnie 2-3 razy wiecej towaru - tam bedzie taniej (po wyczerpaniu oszczednosci ludu nawet o ok. polowe [S]), w pustych drozej, dopoki handel nie przeniesie zlota (rozdz. 5) - a wtedy utarg wsi i podatek pana w ludnej krainie nie rosna razem z plonem. Reach (16% ludzi, 26% zboza swiata) ma nadwyzke ok. 1 000 szt. jedzenia dziennie ponad swoich mieszczan (38% swojej zywnosci na targ); Qarth (ani jednej wsi zboza, ryb, soli, rudy - RT 0.6) musi sprowadzic ok. 610 szt. dziennie zamiast dzisiejszych 219. Handel dalekosiezny przestaje byc ozdoba. Czy karawany (limit 3 na kupca, plaski na miasto) i wozy wsi to uniosa - nie wiem (prog testu w rozdz. 8).

**Bilans zywnosci swiata - dwie warstwy.**
1. Targ (sztuki, symbol): wsie wystawiaja 12 300 szt. zywnosci dziennie, mieszczanie i zamki zjadaja 10 208, druzyny 2 029, przerob (chleb, piwo) 1 579 [P RD 2.3]; reszte daja inne zrodla (uboj, ticki osad). Regula nie zmienia tych sum - zmienia, kto ma nadwyzke: osiem krolestw Westeros z tabeli rozdz. 0 razem z ok. +1 660 na ok. +2 460 szt./d (jedzenie A), a po odjeciu wojska i piekarni z -260 na +530 (jedzenie B); wielkie miasta Essos na minus [S, tabela; R: przeliczone]. [R] Roznica "wsie 12 300 minus mieszczanie i zamki 10 208 = +2 092" to nie zapas: druzyny i przerob biora razem 3 608 szt./d, reszte daja zrodla poza wsiami (1 955 szt./d).
2. Spichlerze okregow (racje 1:1, juz dzis od ludzi): plon 48.8 mln racji dziennie, zjedzone 41.3 mln, nadwyzka 7.5 mln racji przepada "ponad sufit" przy pelnych spichlerzach [P RD 2.2]. Regula tego nie dotyka. Gdyby mieszczanie i wojsko tez jedli z racji, swiat mialby -4.4% (pole musialoby dawac 1.26 zamiast 1.2 racji na wiesniaka), Westeros +8 do +14%, Qarth -24.5%, Meereen -29.6%, Lys -33.4% [S RD 5.3] - **tego NIE proponuje**: miasta jedza z targu w skali symbolu i tak zostaje.
Spostrzezenie na pozniej: nadwyzka spichlerzy (7.99 mln racji = ok. 400 tys. sztuk 1:1) podzielona przez skale targu 37 daje ok. 10.8 tys. sztuk - prawie tyle, ile wsie wystawiaja zboza (8 985) [S z P RT 2.1]. Docelowo zboze na targ mogloby BYC nadwyzka spichlerza (jedna ksiega zamiast dwoch) - osobna paczka na slowo Jeffa, nie w tym projekcie.

## 4A. TABELA TOWAROW: dzis, wzorzec, po zmianie, skad

Jak czytac. "Na 1000 ludzi" = na 1000 ludzi SWIATA (51.7 mln) na dobe. "Wzorzec" = Europa Zachodnia ok. 1300 r. (kg na glowe rocznie, RT rozdz. 4 - wiedza ogolna [S], pewny rzad wielkosci, nie druga cyfra) przeliczony na skale targu gry: **1 sztuka na targu = ok. 37 prawdziwych sztuk po 10 kg** (widelki 28-73; wyznaczone z jedzenia - RT 2.1); zywnosc kupuje z targu 21% ludzi (miasta, podzamcza, wojsko - chlop je z papierowego spichlerza), opal tylko miasta, reszte wszyscy. "Po zmianie" = po paczkach P1-P3 (podzial wedlug ludzi, suma bez zmian) i po P5 (poprawione stawki) [S]. "Skad" = udzial krolestwa w towarze: dzis -> po zmianie [S z P, SK - wedlug ludzi wsi danego typu].

### Surowce wsi (regula dziala wprost)

| towar | dzis szt./d swiat [P] | dzis na 1000 | wzorzec na 1000 [S] | dzis : wzorzec | stawka: szt. na 1000 ludzi wsi typu / d | po zmianie na 1000 | skad po zmianie (dzis -> po) | uwagi |
|---|---:|---:|---:|---:|---:|---:|---|---|
| zboze | 8 985 | 0.1737 | 0.449 | 0.39 | 0.765 | 0.174 | Reach 12 -> 27%, Polnoc 13 -> 10%, King's Landing 9 -> 8%, Dolina 7 -> 8% | cala zywnosc razem = 1.0 wzorca (z niej skala); zboza o polowe za malo wobec reszty jedzenia - mieszanki nie ruszam w tym projekcie |
| ryby | 2 600 | 0.0503 | 0.0077 | 6.5 ZA DUZO | 0.501 | 0.050 | Reach 8 -> 16%, Volantis 3 -> 12%, Dolina 9 -> 12%, King's Landing 7 -> 9%; Wyspy 8 -> 2% | zjadane (lud 1 946, wojsko 615); zapas rosnie 47 -> 104 tys. - do decyzji pozniej razem z mieszanka jedzenia |
| len (wlokno) | 484 | 0.0094 | 0.0073 + wojsko | 1.27 | 0.333 -> **0.60** | **0.017** | King's Landing 17 -> 20%, Braavos 10 -> 13%, Stormlands 10 -> 13%, Norvos 10 -> 13%; Polnoc 15 -> 10% | brak w 69 z 97 miast, regulator na suficie 1.30 od doby 85 [P]; Volantis i Qarth bez wsi lnianej; stawka x1.8 - rachunek nizej |
| welna | 317 | 0.0061 | 0.0073 | 0.83 | 0.129 | 0.0061 | Dolina 16 -> 20%, Reach 8 -> 19%, Qarth 4 -> 13%, Volantis 4 -> 12% | zapas rosnie 2.2 -> 9.4 tys.; waskim gardlem tkanie, nie welna |
| sol | 511 | 0.0099 | 0.0441 | 0.22 ZA MALO | 0.770 -> **1.54** | **0.020** | Volantis 8 -> 30%, Stormlands 19 -> 26%, Dorne 15 -> 11%, Lys 8 -> 9%; King's Landing 19 -> 4% | 95% kupuja wsie; zapas 5-8 dob, regulator na suficie od doby 57 [P]; x2, dalej reguluje sam regulator (0.1-1.3) |
| ruda / lupa 15 kg | 260 | 0.0050 | 0.0017 | 2.9 | 0.123 | 0.0050 | Reach 11 -> 23%, King's Landing 8 -> 11%, Stormlands 11 -> 10%, Volantis 4 -> 9%; Wyspy 15 -> 3% | ponad wzorzec, bo warsztaty zbrojne pracuja w skali 1:2-1:4; kopaliny ida ta sama regula (Jeff: "kazdy towar") - skutek dla Wysp w rozdz. 6 |
| drewno (ladunek 100 kg) | 2 050 (drwale 302, las wsi 1 748) | 0.0396 | 0.0661 uzytkowe; opal 0.222 | 0.57; opal 0.01 | drwale 0.115; las 0.045 x klimat | 0.040 | drwale: Reach 7 -> 18%, Polnoc 17 -> 13%, Qohor 10%, Stormlands 10 -> 8%; las: Reach 18%, King's Landing 10%, Volantis 10% | zapas x14 w rok (poprawka N5-D); opalu mieszczanie nie kupuja - brak ujscia, nie brak drewna; klimat lasu zostaje (Qarth pustynia x0.32) |
| glina | 368 | 0.0071 | 0.0294 | 0.24 ZA MALO | 0.316 | 0.0071 | Dorzecze 25 -> 27%, Volantis 6 -> 17%, Braavos 12 -> 15%, Norvos 12% | ujsciem sa garncarnie (2 na 97 miast) i budowy - nie podnosic bez ujscia |
| wapien, marmur | 130, 38 | 0.0025, 0.0007 | brak wzorca | - | z listy typu | bez zmian | jak glina / srebro | idzie na budowy |
| winogrona | 416 | 0.0080 | (wino nizej) | - | 0.139 | 0.0080 | Reach 9 -> 23%, Stormlands 23 -> 22%, Dorzecze 13 -> 15%, Qohor 8% | 113 szt./d przepada z druzynami |
| oliwki | 354 | 0.0068 | (oliwa nizej) | - | 0.129 | 0.0068 | Reach 33 -> 51%, Volantis 10 -> 19%, Dorne 26 -> 10%, Stormlands 10 -> 9% | - |
| daktyle, przyprawy | 148, 5.2 | 0.0029, 0.0001 | brak wzorca | - | 0.118 | bez zmian | Qarth 7 -> 27%, Volantis 7 -> 19%, Lys 21 -> 14%, Tyrosh 21 -> 13% | - |
| jedwab surowy | 69 | 0.0013 | zbytek, brak wzorca | - | 0.039 | 0.0013 | Volantis 21 -> 39%, Qarth 14 -> 24%, Reach 21 -> 19%, Myr 21 -> 8% | 28% wartosci produkcji wsi swiata - najwiekszy pojedynczy zysk Volantis i Qarthu |
| srebro, zloto (rudy) | 108, 7.4 | 0.0021, 0.0001 | brak wzorca | - | 0.044 | bez zmian | King's Landing 25 -> 27%, Qarth 10 -> 27%, Braavos 7 -> 11% | - |
| futra | 18 | 0.0004 | brak wzorca | - | 0.015 | 0.0004 | Polnoc 29 -> 26%, King's Landing 12 -> 25%, Reach 4 -> 13% | - |
| krowy, owce, swinie | 86, 161, 198 | 0.0017, 0.0031, 0.0038 | mieso 0.020 | mieso 1.09 | z listy typu | bez zmian | bydlo: Dorzecze 25 -> 31%, Dolina 13 -> 18%; owce jak welna; swinie: Reach 10 -> 29%, Polnoc 30 -> 27% | zrodlo miesa i skor |
| ser, maslo, jaja | 442, 328, 110 | 0.0085, 0.0063, 0.0021 | nabial 0.0155 | 0.96 | z listy typu | bez zmian | jak bydlo, owce, swinie | w skali |
| miod pitny, tran, kly | 54, 6.0, 0.9 | 0.0010 i mniej | brak wzorca | - | z listy typu | bez zmian | drwale / wielorybnicy (Polnoc 40 -> 56%, Ibben 40 -> 36%) | - |
| konie (zwykle, bojowe, szlachetne, juczne) | 370 | 0.0072 | 0.022-0.041 (8-15 na 1000 rocznie) | 0.2-0.3 | wedlug stadniny | 0.0072 | rumaki Zachodu: King's Landing 68 -> 60%, Reach 23 -> 34%; stepowe: Qarth 23 -> 57%; pustynne: Volantis 20 -> 53%, Dorne 27 -> 12%; polnocne: tylko Polnoc | hodowla wedlug popytu (K4) zostaje |

### Wyroby miast (ida za rekami i rynkiem - m_t)

| towar | dzis szt./d [P] | dzis na 1000 | wzorzec na 1000 [S] | dzis : wzorzec | po zmianie na 1000 [S] | skad po zmianie | uwagi |
|---|---:|---:|---:|---:|---:|---|---|
| chleb, placek | 1 024, 568 | 0.0198, 0.0110 | w zbozu | - | bez zmian sumy | piekarnie x m_t: wiecej w Reach, Volantis, Qarth, Braavos | 494 szt. chleba robia rzemieslnicy BK - ta czesc zostaje plaska (nie wiem, jak ja skalowac) |
| mieso | 1 136 | 0.0220 | 0.0201 | 1.09 | 0.022 | za zwierzetami i rzezniami | w skali |
| piwo | 896 | 0.0173 | 0.232 | 0.07 ZA MALO | bez zmian w tym projekcie | - | 651 szt. z rzemieslnikow BK; podniesienie = zmiana koszyka ludu -> "lista dostatku" (P3 obiegu kiesy, Jeff: zbytki TAK) |
| wino | 59 | 0.0011 | 0.062 | 0.02 ZA MALO | bez zmian | tlocznie x m_t | jak piwo |
| oliwa | 35 | 0.0007 | 0.0046 | 0.15 ZA MALO | bez zmian | tlocznie x m_t (Reach, Volantis) | jak piwo |
| plotno | 157 | 0.0030 | 0.0037 + wojsko 0.0016 + warsztaty zbrojne 0.0004 | 0.83 (cywilne) | **ok. 0.0056** (ok. 290 szt./d) | tam, gdzie len i rece rzemiosla miast (rece = 2 x rece zbrojne, juz od ludzi) | brak w 88 z 97 miast; wojsko chce 80.4 szt./d, dostaje 9.4 [P RT 5.1] |
| sukno (felt) | 190 | 0.0037 | 0.0044 + wojsko 0.0022 | 0.84 (cywilne) | 0.0037-0.0045 | tkalnie x m_t; welny starcza | wojsko chce 113.7, dostaje 83.4 [P]; jesli po P2 dalej brak - stawka welny, nie wczesniej |
| skory surowe | 371 | 0.0072 | 0.0044 | 1.63 | 0.0072 (po N5-B 510 szt./d [P test N5]) | za ubojem | - |
| skora wyprawiona | 145 | 0.0028 | 0.0037 + wojsko 0.0014 + warsztaty 0.0015 | 0.77 (cywilne) | ok. 0.0040 (po N5-B 207 szt./d [P]) | garbarnie i rzemioslo x m_t | N5-B juz zdjelo polowe braku |
| aksamit | 9.8 | 0.0002 | zbytek | - | bez zmian | tkalnie aksamitu | - |
| narzedzia | 146 | 0.0028 | 0.0018 | 1.54 | 0.0028 | kuznie x m_t | 60% kupuja wsie; po P3 zakupy wsi ida za ludzmi wsi |
| deski | 14.8 | 0.0003 | w drewnie | - | bez zmian | tartaki x m_t | - |
| ceramika | 59 | 0.0011 | 0.0147 | 0.08 ZA MALO | bez zmian | garncarnie | 2 garncarnie na swiat; jak piwo - do listy dostatku |
| klejnoty | 16.7 | 0.0003 | zbytek | - | bez zmian | zlotnicy | zapas x41 w rok - brak odbiorcy |
| bron i zbroje | 1 341 | 0.026 | rece 0.3-0.6 na 1000 (dzis 0.144) | 1:2-1:4 | bez zmian (rece juz od ludzi) | Reach 888 rak, King's Landing 547, Qarth 487, Volantis 466, Polnoc 333 [P log l.305] | polki x6 w rok - osobna sprawa |
| strzaly, belty (snopy) | 277 | 0.005 | - | - | bez zmian | jak bron | - |

### Towary bez zrodla (dziura obiegu, nie skalowac)

Jablka 204, marchew 207, pomarancze 57, miod 47 szt./d powstaja w "tickach osad" bez wsi-producenta i w 84-85% przepadaja z rozwiazanymi druzynami; razem ok. 1 114 szt. jedzenia dziennie w nicosc [P RT 8.1]. Regula ich nie dotyka; naprawa to "male poprawki" z listy raportu rocznego (pkt 6).

### Rachunek stawki lnu i soli (P5)

- **Len "z rak, nie z mnoznika"** (zastrzezenie Jeffa do N5): w wsi lnianej poza lnem jest tylko "zboze 3" (baza gry: len 18, zboze 3 - RT 2). Przesuniecie CALEGO tego zboza na len przy rownej wartosci daje 3 x 3 d / 20 d = 0.45 szt. lnu na wies dziennie = +9 szt. na swiat (+2%) [S] - "len kosztem innego plonu tej samej wsi" prawie nic nie da. Brak jest swiatowy i bierze sie ze skali: produkcja plotna jest symbolem (1:37-45), a wojsko (164 tys. ludzi = 0.32% swiata) bierze w prawdziwych kilogramach - potrzebuje 80 szt. plotna dziennie przy produkcji 157 [P RT 5.1]. Stawka lnu musi wiec pokryc: cywile wedlug wzorca 190 szt. plotna + wojsko 80 + warsztaty zbrojne 22 = ok. 290 szt. plotna dziennie = 870 szt. lnu (3 lnu na sztuke plotna [P log l.302]) wobec 484 dzis: **x1.8**, czyli 0.60 szt. na 1000 ludzi wsi lnianych. To jest stawka pracy ludzi okregu (wiecej rak przy lnie), nie mnoznik regulatora; regulator dalej moze ja tylko przyciac albo podniesc do 1.3. Szybsze kroki regulatora (wybor Jeffa "A") zostaja.
- **Sol**: wzorzec w skali gry 2 280 szt./d, dzis 511; odbiorca jest (wsie kupuja 486 z 511, zapas 5-8 dob). **x2.0** (1 020 szt./d, z regulatorem do 1 330) - polowa drogi do wzorca, reszte pokaze popyt. Zastepuje "sol pod regule A" (Jeff 13.10 TAK), bo regula A (sufit 4.0 bez kosztu) jest wylaczona do tego projektu.
- Alternatywa dla lnu i skory, ktorej nie wybieram: zmniejszyc odziez wojska do skali targu (dzielenie przez ok. 30). Usuwa brak od razu, ale zolnierz przestaje wydawac zold na odziez (ujscie zlota z sakiewek znika), a jedzenie druzyn i tak zostaje 1:1 - niespojnosc by zostala. Zapisane jako furtka.

## 5. Dowod zamknietego obiegu

Zasada projektu: skalujemy tylko rzeczy fizyczne u zrodla i u ujscia; zadnego mnoznika na zlocie.

| zmiana | co powstaje / znika | platnik -> odbiorca | dlaczego nic z niczego |
|---|---|---|---|
| plon wsi x u_v | towar w magazynie wsi | praca ludzi okregu (jak dzis) | suma swiata typu w dniu wlaczenia bez zmian (g typu); wiecej ludzi = wiecej towaru, mniej ludzi = mniej |
| sprzedaz wozu w miescie | zloto kasa miasta -> sakwa taboru -> kiesa wsi i podatek pana | ta sama droga co dzis, domknieta co do denara | A\VillageTakings.cs:16-30 (*): "nic nie znika"; cena sztuka po sztuce (A\MarketCarts.cs:41-46 (*)) |
| popyt miasta x m_t | towar schodzi z polki | kiesa ludu -> kasa miasta | lud placi tylko tym, co ma: `TownFolkBuyWithoutMinting` (A\Settings.cs:1012 (*)); wiekszy koszyk to wieksza POTRZEBA, nie wieksze zloto |
| glowy kiesy wedlug rynku | zmienia sie potrzeba i podzial na klasy | - | zloto kies nietkniete; suma glow swiata bez zmian; w nowej kampanii zapasy startowe kies licza sie od tych samych glow (ten sam dar startowy swiata, inny podzial) |
| zapas kupcow x m_t | przesuwa sie prog zaworu | kasa miasta -> pan i korona (zawor, jak dzis) | to prog, nie doplata: miasto male odda nadwyzke ponad mniejszy zapas (jednorazowo do panow), miasto duze zatrzyma wiecej kapitalu obrotowego; suma zapasow swiata +-3% [S] |
| rece warsztatow notabli x m_t | wiecej wsadu kupione, wiecej wyrobu sprzedane | kapital warsztatu <-> kasa miasta; 35% wartosci wyrobu do kiesy ludu (jak dzis) | wsad musi lezec na polce; bez surowca nic nie powstaje |
| tempo werbunku i milicja | ludzie przechodza z DOM do wojska | okreg (DOM) -> rosterze | kazdy czlowiek schodzi z ksiegi (`PeopleBook.TakeLevy`, A\ArmsFromPeople.cs:22-29 (*)); sufit 20% mezczyzn zostaje |
| licznik Food / m_t | nic (jednostki licznika) | - | bez tego duze miasto mialoby jedzenie w liczniku z niczego, a male glod bez powodu |
| podatki, renty, zawor, daniny, zold | - | bez zmian | niczego nie mnozymy; ida za prawdziwym utargiem i zarobkiem |
| wyjatek Strazy (rozdz. 6) | towar Daru ponad ludzi Daru | jawny wyjatek: dzisiejszy stan, nie nowa doplata | 285 szt. zywnosci i ok. 4.3 tys. zl towaru dziennie = 2.0% wartosci produkcji wsi swiata [P RD 2.2]; docelowo zastapic darami krolestw (platnik: korony Westeros) |

**[R] Czego ten dowod nie mowi: gdzie lezy pieniadz i skad ludna kraina ma byc bogatsza.** Tabela wyzej pokazuje, ze nic nie powstaje z niczego - to sie zgadza. Nie pokazuje, czy i jak szybko zloto przejdzie tam, dokad pojdzie towar.
- **Zapasy.** Zloto swiata (208.3 mln zl [P RAPORT-ROCZNY 2.5, doba 364]) lezy dzis "na osade, na rod i na krolestwo": glowy rodow 47.2 mln, kiesy ludu miast 44.1, skarbce krolestw 27.1, notable 21.8, sakiewki zolnierzy 16.6, kasy miast 11.0, kiesy wsi 9.5, kasy zamkow 5.4 mln. Regula zmienia strumienie towaru, nie te zapasy.
- **Skad bogactwo.** Ludna kraina wytwarza 2-3 razy wiecej i zjada 2-3 razy wiecej - na glowe nie jest bogatsza i nie ma byc. Bogatsi maja byc jej PANOWIE, bo podatek to udzial w obrocie: podatek wsi od utargu taboru, podatki od zarobku ludu, cla. Te pozycje to dzis 119 z 620 tys. zl/d stalego dochodu rodow (19%) [P]. Pozostale 81% (zawor i danina kas, zwrot 70% zoldu) to zold i wydatki samych panow, wracajace przez sakiewki zolnierzy i kasy miast: kasy miast placa wsiom i ludowi wiecej (ok. 312 + 293 tys. zl/d: utarg taborow, robocizna, obrot, doplata do wyrobow), niz dostaja od ludu (ok. 469 tys.) - roznice i caly zawor pokrywa to, co w miastach wydaje wojsko i dwor [P linie "Obieg" i "Kiesa ludu" doby 109199].
- **Zarobek ludu miasta nie rosnie z liczba ludzi.** Sklad zarobku ludu 97 miast w dobie 109199 [P linia "Kiesa ludu"]: od wojska 140.8 tys. (25% wydatkow zolnierzy, karawan, band i zalog), robocizna handlu z kas 180.2 tys. (20% sprzedazy jedzenia, 10% towaru, 4% broni - od wszystkiego, co kasa sprzedaje: wojsku, dworowi, karawanom i ludowi), place warsztatow i doplata do 35% wartosci wyrobow 120.7 tys., obrot patrycjatu 38.1 tys., dwor 20.4 tys., 5% zaworu 10.6 tys. (A\TownFolk.Live.cs:774-800, A\TownFolk.cs:62, A\Settings.cs:1635-1640 (*R)). Z wlasnych wiekszych zakupow wraca ludowi tylko ok. 16% (robocizna + obrot); reszta zarobku idzie za wojskiem i dworem tej krainy. Rosnie potrzeba, nie zarobek.
- **Rachunek na srednie miasto** [S z P kiesa-ludu.csv, `skrypty-rec\r06`]: Reach - zarobek 4.9 tys., wydatki 4.7 tys. zl/d, kiesy 393 tys. zl, dzis stac na 92-94% koszyka; po zmianie potrzeba ok. 11.5 tys. zl/d, zarobek ok. 6.0 tys. - oszczednosci starcza na 10-12 tygodni, potem stac na 43% koszyka. Qarth: 7-8 tygodni, potem 30%. Braavos: tydzien, potem 33%. Pentos: 2 tygodnie, potem 46-49%. King's Landing i Stormlands: 7-10 miesiecy, potem 64-65%. Volantis da rade (zarobek dzis dwa razy wiekszy niz wydatki). W pustych krainach odwrotnie - miasto Polnocy: zarobek 5.5 tys. przy potrzebie 2.7 tys.; miasto Wysp: 3.1 tys. przy 0.5 tys.; nadwyzka idzie w oszczednosci, a z nich danina P1 do korony.
- **Skutek.** W ludnej krainie na te sama monete przypada 2-3 razy wiecej towaru: po wyczerpaniu oszczednosci ludu ceny tam spadna (budzet miasta jest w zlocie; przy tym samym zlocie i podwojonym towarze cena schodzi o ok. polowe [S]), wiec utarg wsi w zlocie i podatek pana NIE rosna razem z plonem. W pustej krainie odwrotnie: malo towaru, duzo monety. Do granicy z rozdz. 4 swiat dochodzi wtedy juz tylko handlem miedzy krainami (wozy wsi do najlepiej placacego miasta w zasiegu, karawany - limit 3 na kupca, plaski na miasto) i lupem. Tempa projekt nie policzyl - **nie wiem**; moze trwac lata gry. To nie jest zloto z niczego ani w nicosc - ale tez nie jest to "pieniadz idzie sam za ludzmi", ktore projekt obiecywal Jeffowi.
- **Co domyka sprawe bez doplat.** Granica jest stanem spojnym: gdy zloto lezy wedlug ludzi, to obrot, zarobek ludu (robocizna od sprzedazy, place warsztatow od rak - juz od ludzi rynku - i 25% zoldu wydanego w krainie), podatki i zold tez ida wedlug ludzi. Swiat mozna w tym stanie ZACZAC: nowa kampania z zlotem startowym kas miast, kies ludu, kies wsi (i - decyzja Jeffa - skarbcow koron) rozdanym wedlug ludzi przy tej samej sumie swiata (pyt. 7). Na starym zapisie zostaje droga powolna: skala dochodzaca stopniowo. P0 ma to rozstrzygnac liczbami (dochod "pierwotny" i "zwrotny" kazdego krolestwa, zarobek i potrzeba ludu kazdego miasta).
- **Zapas kupcow x m_t przestawia 2.3-2.6 mln zl** [S z P kasy-miast.csv]: ludne miasta musza zatrzymac +2.56 mln (Reach +775 tys., Volantis +448, Qarth +431, King's Landing +282, Braavos +278), puste oddaja jednorazowo 2.28 mln swoim panom i koronom (Wyspy 379 tys., Polnoc 340, Dragonstone 278, Free Folk 207, Dorne 178). Suma zapasow swiata +3% - zgodne z autorem. Ale przez pierwsze 1-2.5 miesiaca zawor w ludnych miastach stoi (Reach ok. 31 dob dzisiejszego zaworu, Qarth 47, Braavos 76), a panowie pustych krain dostaja premie - odwrotnie do zamiaru.

Kontrola w testach: linie obiegu (Obieg, 165, kontrola kies r = 0) musza wyjsc jak przed zmiana - 0 roznych dob; nowe liczniki paczek nie moga dopisac ani zdjac zlota (progi w rozdz. 8).

## 6. Sto procent przypadkow

| przypadek | co robi regula | liczba |
|---|---|---|
| wies-olbrzym (Ramsport 302 tys.) | u = 4.18; magazyn 5 dob produkcji, woz zabiera caly magazyn bez limitu udzwigu (poprawka 122) i wybiera najlepiej placace miasto; cene trzyma polka i zapas kupcow x m_t | wies zboza tej wielkosci: ok. 210 szt./d [S RD 5.2] |
| miasto-olbrzym (Qarth: rynek 1.28 mln + wsie zamku) | m ok. 2.5-3; bezpiecznik 4.0; licznik Food bez zmian (neutralizacja) | jedzenie z dowozu: pokrycie wlasne 23% (dzis 25%) [S, R] |
| wies pusta / karzel (Dar 1.3-2.1 tys., Sarnor 4.4-7.1 tys.) | czysta proporcja: u 0.02-0.10, plon 1-5 szt./d; woz jedzie rzadko; stan wsi i rabunek jak w grze | 28 wsi z u < 0.10 |
| wies bez przelicznika ludzi (k = 0) albo blad liczby | dzisiejszy wzor gry (skala 1), licznik potkniec, bez globalnego wylacznika | dzis 0 takich wsi w linii spichlerzy [P] - do potwierdzenia w P0 |
| region spustoszony | skala od dom + uchodzcy, a rece w domu zdejmuje YieldFactor - plon spada plynnie i wraca z powrotem uchodzcow (do roku); rynek miasta maleje o ludzi, ktorzy uciekli | - |
| wies spladrowana, oblezenie | jak dzis (stan wsi w modelu gry; nasz plon wsi spladrowanej bez zmian) | - |
| zamek bez miasta | zamek nie ma ludzi w ksiedze - jego ludzie to jego wsie; wsie zamku handluja w miescie TradeBound i licza sie do tamtego rynku; podzamcze: glowy wedlug ludzi wsi zamku | zamek bez wsi: skala podzamcza = dolna granica 0.05 |
| 8 miast z zerem ludzi wlasnych | rynek = wsie handlujace w miescie; nie ma zera | Castle Black: m 0.01 -> granica 0.05 |
| nomadzi i Wolni Ludzie (0% miejskich) | wsie wedlug ludzi (Dothrakowie u 0.60, Wolni 0.22); miasta-obozy jako rynki wsi; lup liczy sie do dochodu stalego najezdzcow (A\ClanBudget.cs:54, 338 (*)) - zostaje | D Dothrakow 40.6 -> 8.8 tys. zl/d bez lupu [S] |
| Zelazne Wyspy | bez wyjatku w produkcji (u 0.22, m 0.15): biedne z ziemi, zyja z lupu; milicja i werbunek z udzialem zbrojnych x2 (prog wolnych rak 40% wobec 20% - A\FreeHands.cs:126 (*)) | zelazo Wysp 15% -> 3% swiata; wojsko 6.4 -> 2-4 tys. [S] |
| Nocna Straz | jawny wyjatek: Dar zostaje przy dzisiejszej stalej produkcji (klucz), bo Straz nie zyje z Daru - jej ludzie i tak przychodza z 10 krain Westeros (WatchLaw, A\ArmsFromPeople.cs:38-39 (*)); bez wyjatku: 62-147 zolnierzy zamiast 5 797. [R] Wyjatek musi objac takze rynek Castle Black (m_t = 1 dla tego miasta) - inaczej stala produkcja Daru (ok. 285 szt. zywnosci / d) trafia na targ pomniejszony do 0.05 | pytanie 3 |
| Sarnor | liczba w ksiedze 0.1 mln (A\PopulationLaw.cs:69 (*)) daje 5.7 tys. ludzi na wies - 12 razy mniej niz srednia i 3 razy mniej niz nastepne najrzadsze krainy (Wyspy, Wolni Ludzie po 16 tys.); jedyne krolestwo, ktore ksiega juz dzis hamuje (328 odmow ochotnika dziennie [P]) | propozycja: 0.25 mln (gestosc Wysp); pytanie 3 |
| Dorne | liczba ludzi sensowna (najmniej ludne z siedmiu); po zmianie slabsze 2-3 razy - to skutek zasady, nie blad | wojsko 0.94% ludzi -> 0.3-0.6% [S] |
| gracz | ta sama regula: lenno, warsztat, werbunek, ceny; napisy po angielsku: linia dymka produkcji "People of this land" i czynnik; istniejace okno "People of this land" pokazuje liczbe ludzi | - |
| stary zapis | odniesienia (srednia wsi, sredni rynek, g typow) liczone przy pierwszym wczytaniu z biezacej ksiegi; glowy kiesy przestawione raz (flaga w zapisie); regulator wsi zaczyna pomiar od nowa. Dziala, ale bez ochrony - Jeff i tak zaczyna nowa kampanie | - |
| osada zmienia pana albo kulture | skala nalezy do osady (k_v zamrozone na osade), nie do wlasciciela | - |
| inny mod doda typ wsi | g typu liczone z danych kampanii; typ z jedna wsia ma g takie, ze daje tyle co dzis | - |

## 7. Zgodnosc z regulami z 13.10 i z BannerKings

| regula | stan | zgodnosc | co sprawdzic po wlaczeniu |
|---|---|---|---|
| danina P1 (2.5% / d oszczednosci ludu ponad zapas) | WGRANE 39 | zgodna: "zapas" liczy sie w dniach koszyka, a koszyk pojdzie za ludzmi rynku - danina przesunie sie do ludnych krolestw | progi kiesa1; lokata ludu po przestawieniu glow |
| doplata wedlug potrzeby W5 / W5b | WGRANE 41 / w tescie | zgodna co do zasady; liczby sie zmienia: w biednych krolestwach (Polnoc, Dorne, Wyspy) wiecej rodow ponizej 60% i mniejszy garnek, w bogatych odwrotnie | progi w5 od nowa; niepokryta potrzeba wedlug krolestw |
| tarczowe W6 | WGRANE 42 | zgodne; wiecej platnikow w ludnych krolestwach (rody z luzem i zlotem) | progi w6 |
| stawka wojenna pana W7 | czeka | MA czekac na ten projekt (zmieni dochody wszystkich) - bez zmian planu | - |
| towary N5: B skora, C rece x1.3, D drewno, E budowy | w tescie | zgodne; C mnozy rece, ktore juz ida od ludzi | - |
| N5 regula A (sufit regulatora 4.0) i len | A wylaczona; len czeka | zastapione: stawka lnu i soli z wzorca (rozdz. 4A); wybor Jeffa "A" (szybsze kroki) zostaje, "C" (len kosztem zboza tej wsi) policzony - daje +2%, wiec nie on zalatwia sprawe | N5b zamknac paczka P5 |
| lud buduje miasto (P2 obiegu kiesy) | zatwierdzone | zgodne i pomocne: budowle warowni to jedyna poprawka "dobrobyt -> plon wsi" (+5/10/15%), a buduje je ludne, zamozne miasto | - |
| lup zdobytego miasta | zatwierdzone | zgodne: lup = czesc kazdej monety w osadzie; ludne miasto ma wiekszy zapas kupcow i wieksze kiesy - "prawdziwe bogactwo" samo rosnie z ludzmi | - |
| rozbici wracaja N7 | zatwierdzone | niezalezne; wracajacy do domu podnosza ludzi wsi, a z nimi plon | - |
| konie robocze mieszczan K1-3a | czeka na daninie | "1 kon na 25 ludzi" liczy od glow kiesy - po przestawieniu glow zakupy pojda za rynkiem | scalac po P2 |
| BannerKings | - | nie ruszamy hearth, dobrobytu ani ludnosci BK; wpinamy sie w miejsca, gdzie juz siedzimy: model produkcji (BK bierze ilosc z czynnego modelu - BK\...\EconomyPatches.cs:962, za RK), budzet konsumpcji BK (postfiks Priority.Low - A\TownFolk.Live.cs:413-417 (*)), szansa ochotnika BK (A\Levy.cs:115 (*)); podatek BK od utargu wsi idzie za utargiem sam | rzemieslnicy BK (chleb 494, piwo 651, mieso 474, skory 305, ceramika 55 szt./d [P RT 3.1]) zostaja plascy - osobne rozpoznanie. [R] Sprawdzone w dekompilacji, ze BK i gra nie nadpisza skali - lista w sekcji "Po recenzji", pkt 7. Rzemieslnicy BK zjadaja 1 140 szt. zboza / d i daja chleb i piwo plasko na miasto [P linie "Towary", doba 109199] - w malym rynku (m 0.15) to kilka razy wiecej niz caly przeskalowany popyt na chleb |

## 8. Paczki wdrozenia

Kolejnosc pilnuje dwoch rzeczy: (1) podaz i popyt musza wejsc do gry razem (regulator swiata po 56 dobach sciagnalby jedno do drugiego), (2) kazda paczka ma wylacznik w MCM i wlasne progi. Jeff nie gra do konca projektu - wgrywanie po udanych biegach jak dotad.

| nr | paczka | tor | klucz (MCM, po angielsku) | zawartosc | zaleznosci |
|---|---|---|---|---|---|
| P0 | pomiar na sucho | S | `PeopleScaleDryRun` | bez zmiany rozgrywki: skale u_v i m_t co dobe do logu i CSV; produkcja wsi wedlug regionow (dzis log jej nie dzieli); "co by bylo" dla 16 surowcow i krolestw; na sucho D i pulap zoldu rodow po zmianie wobec limitu druzyn (prognoza wojska krolestw i progu 85 tys.); rozklad rynkow miast; zakupy narzedzi wsi osobno; ile zywnosci "sprzedanej mieszczanom" wchodzi do licznika Food i ktory model stoi pod ROT; wyrob rzemieslnikow BK wedlug miast. [R] Dodatkowo: (a) na miasto - potrzeba ludu po zmianie wobec jego zarobku i dni oszczednosci; (b) na miasto - zmiana zapasu kupcow i dlugosc przerwy zaworu; (c) D kazdego krolestwa rozbite na "pierwotne" (podatek wsi, oplaty, podatki ludu, cla) i "zwrotne" (zawor i danina kas, zwrot zoldu); (d) ile razy w roku wies zmienia targ; (e) zold zalog wobec D po zmianie, rod po rodzie | - |
| P1 | plon wsi od ludzi | L | `VillageOutputByPeople` | u_v x g(typ) w MaterialLaw.ProdPostfix i w lesie wsi; zdjecie progu hearth z plonu; odniesienia w zapisie; regulator - pomiar od nowa; dymek "People of this land" | P0 |
| P2 | rynek miasta od ludzi | L | `MarketByPeople` | glowy kiesy wedlug rynku; budzet konsumpcji x m_t; zapas kupcow x m_t; rece warsztatow notabli x m_t; neutralizacja licznika Food. [R] Popyt u zrodla (`GetDailyDemandForCategory`), nie w kotwicy; wlaczenie dopiero z rozwiazaniem pieniadza (rozdz. 5 i 9, ryzyko 11): nowa kampania z kasami, kiesami ludu i kiesami wsi rozdanymi na starcie wedlug ludzi (pyt. 7) albo - na starym zapisie - skala dochodzaca stopniowo (rzad 180 dob [S]) | P0; do gry razem z P1 |
| P3 | wies i podzamcze: glowy od ludzi | M | `VillageHeadsByPeople` | `VillHeads` wedlug u_v (oplata stala, zakupy wsi), podzamcza wedlug ludzi wsi zamku | P1 + P2 |
| P4 | werbunek i milicja od ludzi | M | `LevyByPeople`, `MilitiaByPeople` | szansa pustego miejsca i przyrost milicji x skala x udzial zbrojnych ludu (0.10-3.0) | po biegu rocznym P1-P3 |
| P5 | stawki towarow z wzorca | M | `GoodsRatesByPattern` | len x1.8, sol x2.0 (tabela stawek w ustawieniach); zamyka N5b | po P1 + P2 |
| P6 | krainy szczegolne | S / M | `WatchKeepsFlatOutput`; liczba Sarnoru w tabeli | wyjatek Daru; Sarnor 0.1 -> 0.25 mln (tylko nowa kampania) | decyzje Jeffa (pyt. 2-3) |
| P7 | na slowo Jeffa, pozniej | M / L | - | narzedzia +-10%; zdjecie dawnej podlogi rak (zamiast niej stale minimum 6); zboze na targ = nadwyzka spichlerza; limit karawan kupca wedlug rynku; opal, piwo, wino, ceramika (z lista dostatku obiegu kiesy) | po biegu dwuletnim |

**Progi testu 120 dob** (nowa kampania; obok bieg bazowy tego samego kodu z kluczami wylaczonymi):
- kazdy: 120/120 dob, Armoury 0 ERROR, potkniecia 0, czas doby nie gorszy o wiecej niz 0.3 s; linie obiegu zgodne w kazdej dobie (0 roznych).
- P1: suma swiata kazdego z 16 surowcow w dobach 2-29 w granicach 0.95-1.05 biegu bazowego (regula ma zmienic podzial, nie sume); stosunek plonu wsi zboza Reach : Polnoc 2.9-3.5 (ludzie 3.2); korelacja plonu wsi z ludzmi wsi w obrebie typu >= 0.95 (dzis ok. 0); wsie z zatkanym magazynem nie wiecej niz baza + 10; kopalnie stojace nie wiecej niz baza.
- P1 + P2: korelacja wydatkow ludu miasta z ludzmi rynku >= 0.8 (dzis 0.20 z ludzmi regionu [P RD 3.3]); cykle warsztatow "brak zlota / kupca" nie wiecej niz baza (288 / d [P RK]); zadna warownia AI nie gloduje przed 30. doba; miast z licznikiem Food 0 nie wiecej niz w bazie; Qarth, Volantis, Braavos: zywnosc na polkach >= 7 dob spozycia w d91-120; miast z kara glodu C2 nie wiecej niz baza + 5; wywoz zywnosci z Reach (karawany + wozy poza krolestwo) >= 500 szt./d w d91-120.
- [R] P1 + P2, dopisane: lud miast o skali rynku ponad 1.5 - "stac na pelny koszyk" nie nizej niz w biegu bazowym minus 0.05, a dni oszczednosci ludu nie mniej niz 60 w d91-120 (rachunek recenzji mowi, ze Braavos, Qarth i Reach tego progu NIE przejda - to ma byc widac przed wgraniem); zawor kas do panow w krolestwach ludnych nie stoi dluzej niz 14 dob z rzedu; miast, w ktorych kasa nie starcza na zaplate taborom wsi, 0.
- P3: oplaty stale wsi swiata 0.9-1.1 bazy (9.9 tys. zl/d [P]), rozklad wedlug u_v; kiesy wsi nie ujemne, zadna wies z zalegloscia ponad 28 dob.
- P4: rekruci na 1000 ludzi: p90 / p10 krolestw powyzej 400 tys. ludzi <= 3 (dzis 8.1 dla wszystkich 29 krolestw [P RD 2.9]); odmowy hamulca wolnych rak nie wiecej niz baza x 1.2; milicja swiata 0.9-1.1 bazy.
- P5: miast bez plotna w d91-120 <= 60 (dzis 88), bez soli - nowy pomiar, cel zapas >= 14 dob; regulator lnu ponizej sufitu 1.3.

**Progi biegu rocznego** (364 doby, po P1-P3; potem po P4-P5):
- regresja dochodu rodow z ziemi po krolestwach: waga ludzi >= 0.6 (dzis 0.15), waga osad <= 0.4 (dzis 0.87); dochod z ziemi na 1000 ludzi w krolestwach powyzej 400 tys.: p90 / p10 <= 3 (dzis 7.8 dla wszystkich 29); [R] te progi sa z granicy - w pierwszym roku nie musza wyjsc, bo pieniadz dochodzi powoli; obok nich mierzyc kierunek: waga ludzi rosnie z kwartalu na kwartal i na koniec roku przekracza 0.3 - jesli nie rosnie, regula nie dziala i trzeba wrocic do projektu;
- renta zaplacona na 1000 ludzi: p90 / p10 krain <= 4 (dzis ok. 10: Reach 1.57, Dorne 8.64 zl/d);
- partie lordow w wojnie (srednia 28 dob) >= 85 tys. - prog Jeffa; jesli P0 na sucho pokaze mniej, najpierw decyzja (pyt. 1), potem kod;
- zadne krolestwo powyzej 400 tys. ludzi nie znika z mapy w pierwszym roku; liczba zdobytych twierdz w roku nie wieksza niz 2 x dzisiejsza (14 [P STAN-PRAC]);
- ludzie swiata: zmiana w roku w granicach dzisiejszej (-1.0% [P]) +-0.5 pkt; zmarli z glodu 0 poza oblezeniami;
- zloto swiata: przyrost bez zrodla 0; zadne miasto bez pieniedzy dluzej niz 28 dob.

**Co zmierzyc najpierw (P0), zanim powstanie linia kodu reguly:** (1) wojsko na sucho wedlug rodow - czy swiat utrzyma 85 tys.; (2) ludzie rynku kazdego miasta i ich rozklad; (3) plon wsi wedlug regionow i dzisiejsza suma typow (do g); (4) udzial zywnosci "sprzedanej mieszczanom" w liczniku Food kazdej warowni; (5) zaspokojenie potrzeby narzedzi wsi osobno; (6) wyrob rzemieslnikow BK wedlug miast; (7) wywoz zywnosci z Reach i dowoz do Qarthu dzis; [R] (8) zarobek, potrzeba i oszczednosci ludu kazdego miasta po zmianie; (9) zmiana zapasu kupcow i przerwa zaworu na miasto; (10) D krolestw rozbite na pierwotne i zwrotne; (11) ile razy w roku wies zmienia targ; (12) zold zalog wobec D po zmianie.

## 9. Ryzyka i czego nie wiem

Ryzyka:
1. **Wynik wojen.** Reach, Volantis, Braavos i Qarth dostaja 1.6-2.3 razy wiecej wplywu, Polnoc 0.59, Dorne 0.69, Wyspy 0.52 [S; R: w granicy - recenzja liczy 1.6-2.1 oraz 0.59 / 0.70 / 0.54]. To zamierzone, ale zmieni mape; ocena tylko biegiem rocznym.
2. **Suma wojska swiata i prog 85 tys.** Tracacy traca od razu (pulap zoldu), zyskujacy stoja na limicie druzyn (Qarth 84% limitu juz dzis). [R: nie od razu - pierwszy krok to -10 do -19% D, a skrzynia wojenna trzyma wojsko tracacych przez miesiace; ryzyko 16.] Lekarstwa do rozwazenia po P0: zalogi i najemnicy bogatych, doplata W5 z wiekszego garnka; podnoszenia limitu druzyn "od ludzi" nie proponuje bez slowa Jeffa.
3. **Qarth i handel morski.** 3/4 jedzenia z dowozu [R: juz dzis 3/4; nowa jest ilosc - 610 zamiast 219 szt./d]; jesli karawany nie nadaza - glod C2 w miastach Qarthu. Furtka: zamiana 2-3 symboli wsi Qarthu na zbozowe / rybackie (tabela lore wsi `arm_map_village_lore.tsv`) albo nizsza liczba ludzi Qarthu.
4. **Kopaliny ida za ludzmi.** Zelazo Wysp 15% -> 3% swiata, sol King's Landing 19% -> 4%. W grze nie ma miary bogactwa zloza; gdyby lore mialo zostac, potrzebna kolumna "zloze" w tabeli lore wsi - na pozniej.
5. **Regulator swiata** liczy okna 56 dob - po wlaczeniu P1 musi zaczac pomiar od nowa, inaczej porowna nowa produkcje ze starym zuzyciem.
6. **Progi narzedzia `sprawdz_logi` i ksiega rudy** maja bazy sprzed skali (RK 3.5) - do przeliczenia razem z P1, inaczej falszywe NIE.
7. **Regula budzetu rodow** uczy sie D z 28 dob (RK 3.5) - skutek dla wojska widac po miesiacu, nie od razu; testy 120 dob czytac od doby 60.
8. **Wydajnosc**: postfiks produkcji biegnie dla kazdej pary wies-towar i z watkow AI - skala musi byc czytana z pamieci doby (jedna referencja), bez wolania BK; koszt szacowany jako pomijalny [S RK 3.6], do pomiaru w P1.
9. **Dwie skale ksiazki i targu.** Wojsko jest w skali 1:1 (jedzenie, odziez, bron), targ 1:37. Regula tego nie usuwa; stawki lnu i soli to lagodzenie. Trwale wyjscie (jedna skala) to osobna, duza decyzja.
10. **Miasta w obrebie krainy sa w ksiedze prawie rowne** (ksiega dzieli ludzi krainy wedlug dobrobytu z dnia startu): Duskendale 185 tys. przy White Harbor 33 tys., Krolewska Przystan 259 tys. zamiast kanonicznych ok. 500 tys. Skala rynku lagodzi to (liczy sie okreg), ale nie naprawia.

Ryzyka dopisane przez recenzje [R]:
11. **Pieniadz nie pojdzie sam za towarem; lud ludnych miast nie udzwignie koszyka.** Potrzeba x m_t (Reach 2.25, Qarth 2.5, Braavos 2.44), zarobek ludu prawie bez zmian (rozdz. 5). Oszczednosci [S]: Braavos tydzien, Pentos 2 tygodnie, Qarth 7-8 tygodni, Reach 10-12 tygodni, King's Landing i Stormlands 7-10 miesiecy; potem 30-65% koszyka i kary glodu C2. Po wyczerpaniu oszczednosci ceny w ludnych krainach spadaja i utarg wsi w zlocie przestaje rosnac - dochod panow staje daleko przed granica. Lekarstwa do wyboru po P0: (a) nowa kampania z kasami miast, kiesami ludu i kiesami wsi rozdanymi na starcie wedlug ludzi (pyt. 7) - swiat zaczyna w stanie spojnym; (b) na starym zapisie skala wsi i rynku dochodzi stopniowo (rzad 180 dob [S]); (c) udzial ludu w obrocie kasy liczony takze od skupu plonu wsi (dzis robocizna handlu liczy sie od sprzedazy kasy, nie od zaplaty taborom - A\TownFolk.Live.cs:781-788 (*R)) - wtedy zarobek ludu rosnie razem z plonem okregu; to zmiana reguly kiesy ludu, do osobnego rachunku zamkniecia.
12. **Zapas kupcow x m_t: przerwa zaworu w ludnych miastach i jednorazowa wyplata w pustych** (liczby w rozdz. 5). W nowej kampanii to samo: gra daje kazdemu miastu podobna kase startowa (27.3 mln w dobie 1 [P RAPORT-ROCZNY 2.5]), a dar startowy ludu to nadwyzka ponad zapas - przy zapasie x m_t lud najludniejszych miast dostalby na start NAJMNIEJ. Kasy i dar startowy trzeba rozdac wedlug ludzi (ta sama suma swiata) - do projektu przed P2.
13. **Rynek miasta skacze z wojna i pokojem** (przypisanie wsi zamkow do targu - rozdz. 3.5). Bez wygladzenia: zmiana glow kiesy, budzetu i zapasu kupcow miasta z dnia na dzien.
14. **Zalogi, liczba rodow i skarbce koron zostaja "na twierdze / na krolestwo".** W granicy: Wyspy - zold zalog 14.9 tys. zl/d przy D 5.6 tys.; Dorne - 42 rody na 13.4 tys. zl/d (320 zl na rod), zold zalog 10.9 tys.; Polnoc - zalogi 20.1 tys. z D 38.9 tys. [S z P]. Mechanizmy dlugu (Bank, zajecie dochodu, zwolnienia 166) odpala sie tam masowo; doplata W5 dzieli garnek korony, ktory w tych krolestwach tez maleje. Bez decyzji o zalogach biedne krolestwa beda mialy puste zamki - to rozstrzygnie wojny bardziej niz druzyny.
15. **Cena targu nie widzi ludzi.** Wzor ceny gry liczy "glebokosc" targu z popytu z dobrobytu; jesli skala nie wejdzie u zrodla popytu (rozdz. 3.3), ludne miasto ma zapas na polce liczony w tych samych sztukach co puste, czyli 2-2.5 raza krotszy w dniach spozycia - prog "zywnosc na polkach >= 7 dob" w Qarth, Volantis i Braavos moze nie wyjsc z samej konstrukcji.
16. **Test 120 dob pokaze tylko czesc.** Sciane ludu zobaczy w Braavos, Qarth i Reach (oszczednosci na 1-12 tygodni), ale nie w King's Landing; dochodow panow nie oceni wcale: jednorazowa wyplata z kas pustych miast i skrzynie wojenne tracacych (pulap w wojnie = 0.60 x D + 0.8 x zloto ponad rezerwe / 45 - A\ClanBudget.cs:199-207 (*R)) trzymaja ich wojsko przez pierwsze miesiace. O regule rozstrzyga bieg roczny, najlepiej dwuletni.
17. **Stawki lnu x1.8 i soli x2.0 to mnoznik** - wbrew zastrzezeniu Jeffa do N5 ("len z rak, nie z mnoznika"). Uzasadnienie wzorcem jest uczciwe, ale decyzja nalezy do Jeffa (pyt. 6); droga "z rak" istnieje: wiecej wsi z symbolem lnu w ludnych krainach.
18. **Wyjatek Strazy bez wyjatku dla jej rynku** (rozdz. 6): stala produkcja Daru przy rynku Castle Black x0.05.

Pominiete w projekcie (lista recenzji) [R]:
- skarbce koron: 2 mln zl na start kazdemu krolestwu (57.9 mln swiata w dobie 1) i 1/360 zapasu ponad 0.5 mln dziennie (35-70 tys. zl/d swiata, po ok. 1.5-2.6 tys. na krolestwo niezaleznie od wielkosci) [P RAPORT-ROCZNY 2.3] - najwiekszy plaski strumien, ktorego regula nie dotyka; Skagos, Wyspy Letnie i Yi Ti zyja z niego w 83-88%;
- renty korony "wedlug lenn" (172 tys. zl/d) - wewnatrz krolestwa dzielone na lenna, nie na ludzi lenn;
- zloto startowe rodow (ok. 165 tys. zl na rod) i liczba rodow na krolestwo;
- normy zalog (na twierdze) i ich zold (191 tys. zl/d swiata);
- konsumpcja zamkow (130 zamkow, 2 038-2 228 szt. zywnosci / d) i jej kotwica "tylko w dol";
- karawany: limit 3 na kupca jest plaski na miasto - rynek 2.5 raza ludniejszy ma tyle samo karawan (w P7 jako "pozniej", a to one niosa handel, ktory ma przeniesc zloto);
- nowa kampania: wedlug czego rozdac kasy startowe miast, dar startowy ludu i kiesy wsi;
- lud pustych krain: zarabia 2-6 razy wiecej, niz bedzie potrzebowal - co z ta nadwyzka (dzis: oszczednosci i danina P1 do korony, czyli podpora panow pustej krainy);
- okres przejsciowy u gracza: tani towar w ludnych krainach i drogi w pustych to latwy zarobek na handlu; wlasne lenno w pustej krainie traci plon od razu;
- konie: hodowla idzie za ludzmi (Qarth 57% koni stepowych, Volantis 53% pustynnych) - skutek dla konnicy krolestw nie policzony;
- rzemieslnicy BK w malych rynkach (chleb i piwo plasko na miasto przy popycie x0.15) - nadwyzka bez odbiorcy;
- "People of this land": jedna linia dymka to za malo, zeby gracz zrozumial, czemu jego wies daje 0.2 dzisiejszego plonu - potrzebne zdanie w oknie osady (napisy po angielsku).

Czego nie wiem:
- [R] ZDJETE: model zywnosci pod `ROTSettlementFoodModel` - oba mozliwe (gra albo BK) licza skladnik "sprzedane mieszczanom" tym samym kodem gry (BK\...\BKFoodModel.cs:46-59 (*R)); neutralizacja licznika Food jest wykonalna;
- [R] w jakim tempie pieniadz dojdzie od "pierwszego kroku" do "granicy" (rok? trzy lata gry?) - zalezy od handlu i wojen, do zmierzenia biegiem;
- [R] skladu zarobku ludu kazdego miasta z osobna (znam sume swiata: wojsko 140.8, robocizna handlu 180.2, warsztaty 120.7 tys. zl/d) - od niego zalezy, ile z wiekszych zakupow wraca ludowi jako zarobek; rachunek oszczednosci (Reach 10-12 tygodni) przyjmuje 16-30% [S];
- rozkladu ludzi rynku na pojedyncze miasta (znam sume i srednie krolestw);
- ile zlota bogate krolestwa obroca w najemnikow i jak rosna zalogi przy wolnym pulapie;
- czy karawany i wozy wsi wywioza nadwyzke Reach (ok. 1 000 szt./d [R]) i dowioza Qarthowi 610 szt./d;
- jak skalowac wyrob rzemieslnikow BK (chleb, piwo) - nie czytalem ich wzoru;
- ile lud kupilby plotna, soli i skory, gdyby byly na polkach (popyt ukryty) - stawki P5 to pierwsze przyblizenie;
- czy innowacje BannerKings w tym zestawie modow w ogole postepuja;
- jak wyglada rok z zima przy nowej skali (w biegu rocznym zimy nie bylo; proba zimy byla na starym podziale);
- liczby ludzi krain to szacunki (Essos - fanowskie); regula uczyni z nich najwazniejsza tabele gry - warto, zeby Jeff na nia spojrzal (`A\PopulationLaw.cs:42-74`, `docs\POPULACJA-WESTEROS-ESSOS.md`).

Znalezione przy okazji (poza tym projektem, do listy poprawek): 1 114 szt. jedzenia dziennie przepada z rozwiazanymi druzynami; ok. 8 mln racji nadwyzki spichlerzy dziennie przepada ponad sufit; zapasy uzbrojenia x6, koni x1.9, klejnotow x41 w rok [P RT 8].

## 10. Pytania do Jeffa - tylko kierunek

1. **Sila krolestw.** Po zmianie najsilniejsze beda Reach, Volantis, Qarth i Braavos, a Polnoc, Dorne i Wyspy wyraznie slabsze (tabela w rozdz. 0). Czy tak ma byc bez hamulca? **Rada: tak - to sedno Twojej decyzji; wdrazac krokami, a przed kodem policzyc na sucho, czy swiat utrzyma 85 tys. wojska w polu. Jesli nie - wrocic do Ciebie z liczbami, zanim cokolwiek wgram.** [R] Sila przesunie sie powoli (pierwszy krok: Reach +17%, Polnoc -10%, Dorne -13% stalego dochodu), wiec pierwszy bieg roczny pokaze kierunek, nie stan koncowy.
2. **Ludy walczace (Zelazne Wyspy, Wolni Ludzie, Dothrakowie).** Z ziemi beda mialy 3-4 razy mniej. Maja zyc z lupu i bronic sie pospolitym ruszeniem (wiecej milicji bez zoldu, bo walczy u nich wiekszy udzial mezczyzn), czy dostac wyjatek w produkcji? **Rada: lup i milicja, bez wyjatku w produkcji - biedne wyspy i zbrojny lud to lore; sprawdzic w biegu rocznym, czy nie znikaja z mapy.**
3. **Nocna Straz i Sarnor.** Straz z Daru (20 tys. ludzi) nie wyzyje - zostawic jej dzisiejsza stala produkcje jako jawny wyjatek, a "dary krolestw dla Strazy" (placa korony Westeros) zrobic pozniej? Sarnor ma w ksiedze 93 tys. ludzi na 5 warowni i 13 wsi - podniesc do ok. 250 tys.? **Rada: dwa razy tak.**
4. **Wielkie miasta Essos. [R]** Qarth juz dzis sprowadza 3/4 jedzenia (nie ma ani jednej wsi zboza ani ryb); po zmianie musi sprowadzic prawie trzy razy wiecej (610 zamiast 219 sztuk dziennie). Zostawic liczby ludzi i sprawdzic, czy handel nadazy, czy od razu dac Qarthowi wsie zbozowe albo mniej ludzi? **Rada: zostawic i zmierzyc - miasto kupieckie zyjace z dowozu to lore; furtka (zamiana 2-3 symboli wsi) gotowa na wypadek glodu.**
5. **Poprawki "na glowe" (klimat, dobrobyt, technologia).** W pierwszym kroku tylko te, ktore juz sa (zima, spustoszenie, budowle warowni, klimat lasu); narzedzia zelazne jako nastepny krok w granicach +-10%; zyznosc ziemi NIE, bo jest juz w liczbie ludzi; technologia kultury odlozona (brak miary). Zgoda? **Rada: tak - najpierw niech ludzie zrobia swoje, potem dokladamy drobne poprawki pojedynczo, zeby bylo widac, co ktora zmienia.**
6. **[R] Len i sol - skad wziac wiecej.** Brak jest w calym swiecie, a zastrzegles "len z rak, nie z mnoznika". Dwie drogi: (a) wyzsza stawka - wies lniana daje 1.8 raza wiecej, solna 2 razy (to jest mnoznik, tyle ze policzony ze wzorca); (b) wiecej symboli - czesc wsi innych rodzajow w ludnych krainach dostaje symbol lnu (z 20 na ok. 36 wsi lnianych; Reach, Volantis i Qarth maja razem 1/3 ludzi swiata i jedna wies lniana) - wtedy len jest z rak ludzi, kosztem tego, co te wsie daja dzis. **Rada recenzji: len droga (b) - zgodna z Twoim zastrzezeniem i z zasada "wies to symbol"; sol droga (a), bo to kopalina, nie praca pola.**
7. **[R] Zloto na start nowej kampanii - po recenzji najwazniejsze pytanie.** Regula przenosi towar od razu, a zloto powoli albo wcale (dzis lezy rowno na osade; w ludnych miastach lud nie udzwignie wiekszego koszyka). I tak zaczynasz nowa kampanie - mozna w niej rozdac zloto startowe wedlug ludzi przy tej samej sumie swiata, wtedy swiat od pierwszego dnia jest blisko stanu z kolumny "granica". Kasy kupcow miast, kiesy ludu i kiesy wsi - to jest potrzebne technicznie (inaczej ludne miasta zaczynaja ponizej swojego zapasu, a ich lud dostaje na start najmniej). Osobno do Twojej decyzji: skarbce koron (dzis kazde krolestwo dostaje 2 mln - Reach i Wyspy Letnie tyle samo) i kiesy rodow (dzis ok. 165 tys. na rod). **Rada: kasy, lud i wsie - tak; skarbce koron - tak, wedlug ludzi z podloga dla najmniejszych krolestw; kiesy rodow - zostawic, niech rody bogaca sie same.**
