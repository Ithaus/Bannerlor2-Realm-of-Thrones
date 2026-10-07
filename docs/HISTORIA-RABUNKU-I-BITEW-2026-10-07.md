# Rabunek, glod i bitwy: dane historyczne a zalozenia gry (2026-10-07)

**Pytanie Jeffa (07.10)** o paczke 113: "rabunek dotyka pol czlowieka na napastnika na dobe; 5% ginie, 5% idzie w las, 90% to uchodzcy, ktorzy wracaja latami - czy to dane historyczne, a jak bylo naprawde?"

**Co zestawiono.** Trzy zalozenia projektu z danymi ze zrodel:
1. Rabunek okregu: `docs/DEMOGRAFIA-ANEKS-2026-10-05.md` rozdz. 1.2-1.5 i `docs/paczki/113-ludzie-spustoszenie.md`.
2. Smiertelnosc przegranych w bitwach rozstrzyganych przez gre. Dzis ginie 51% przegranych (log "Bitwy: dzien 108864"), w 13 prawdziwych bitwach 42.6%. Przegrany traci 100% ludzi: zabitych albo rannych do niewoli, nikt nie uchodzi (`AUDYT-2026-10-05-EKONOMIA-WOJNA-SUROWCE-LORDOWIE.md` wojna/W8; hamulec H3 w `REGULY-KRAIN-I-DLUGU-2026-10-06.md`).
3. Glod po najezdzie: dzis nie jest liczony.

**Material.** Trzech badaczy:
- "chevauchee": Francja 1339-1450;
- "pogranicze": Polnoc Anglii 1069-1600, wikingowie, Litwa, Mongolowie;
- "bitwy": 25 bitew 1119-1487.

Do tego synteza dolozyla dane o glodzie: Wielki Glod 1315-17, Outram 2001, Hanlon 2012.

**Oznaczenia:**

| Znak | Znaczenie |
|---|---|
| [PEWNE] | dokument, spis, rejestr, archeologia |
| [HIST] | szacunek historyka |
| [KRON] | liczba albo opis kronikarza, zwykle zawyzone |
| [RACH] | rachunek wlasny z liczb zrodla |
| [PROJ] | decyzja projektu, nie dane |

Rachunki: `scratchpad\historia-rabunku\calc.py` (sesja fa2fd7a6).

---

## 0. Odpowiedz w skrocie

**Liczby z paczki 113 nie sa danymi historycznymi.** To decyzje projektu, ustawione w rzedzie wielkosci, ktory zrodla pozwalaja zrekonstruowac. Zadne zrodlo nie podaje wprost ani "ludzi na napastnika na dobe", ani podzialu "zabici / w las / uchodzcy" dla calego okregu. Da sie je tylko odtworzyc z kilku dobrze udokumentowanych przypadkow:
- rejestr pomocy papieskiej z 1340 r.;
- inkwizycja w Easingwold z 1326 r.;
- rachunki dworow Polnocy Anglii;
- spis konfiskat po Cassel (1328);
- archeologia Visby i Towton.

| Zalozenie | Co mowia zrodla | Werdykt | Propozycja |
|---|---|---|---|
| R = 0.5 czlowieka na napastnika na dobe lupienia | rekonstrukcje 0.1-1.0: najazdy po lup i okup (Szkoci 1319, Edward III 1339) 0.1-0.2, systematyczne palenie (Langwedocja 1355) 0.3-0.95 - liczone na wszystkie dni wyprawy, z marszem i postojami | **miesci sie**, w gornej polowie; w grze R mnozy tylko godziny lupienia, wiec tak ma byc | **zostaje 0.5** (widelki 0.2-1.0) |
| 5% trafionych ginie | wies: ok. 1% ludzi (La Capelle 1339), ok. 1% na najazd (Easingwold 1311-22), 0-1 zabity na najazd reiverow (1592); miasto brane szturmem 15-30% | **za wysoko jako norma** - to gorny kraniec dla wsi | **2%** (1-5%); 5% przy kulturach lowiacych niewolnikow |
| 5% idzie w las | brak jakiejkolwiek stopy; bandytyzm rosl z dluga wojna i okupacja (Normandia 1417-36), a pojedynczy najazd robil zebrakow, nie bandytow (Easingwold) | **nie da sie potwierdzic ani obalic** | **2% + 3% x niebezpieczenstwo regionu** (2-5%) |
| 90% to uchodzcy | wiekszosc uciekala do miast, zamkow i sasiadow - potwierdzone wszedzie | **potwierdza** | reszta, ok. 93-96% |
| powrot 0.1% dziennie x (1 - niebezpieczenstwo), polowa po 2-4 latach | pojedynczy najazd w pokoju: kilka lat (ulgi 5-7 lat, 174 ze 174 parafii odbudowane, welna 7-8 lat po rozejmie); przy ciaglych najazdach nie wracal nikt; po dlugiej wojnie odbudowali wies glownie przybysze (50-100 lat) | **potwierdza dla pokoju, za szybko dla trwajacej wojny** | 0.1% zostaje; **brak powrotu przez 30 dob po rabunku albo zerowaniu w regionie**; **uchodzcy -1.3% rocznie** (dzis 0) |
| plon spada z brakiem rak, elastycznosc 0.5 | w pustoszonych okregach plon spadal bardziej niz liczba ludzi: spalone woly, ziarno i mlyny, pola odlogiem "ze strachu" | **za lagodnie** dla spustoszenia | **elastycznosc 1.0** dla rak brakujacych po spustoszeniu |
| pulap 90% okregu | Yorkshire 1086: 25% ludzi zostalo (sporne); Normandia na dnie 25-30% stanu z 1314 (wojna + zaraza + glod) | **zgodne** | zostaje |
| bitwy: dzis 51% zabitych przegranych, cel 15-40% | 25 bitew: odwrot w porzadku 5-25%, kleska z poscigiem konnicy albo w pulapce 38-63%, okrazone pospolite ruszenie 85-90%; mediana slawnych klesk ok. 20-40%; jency 1-20%, reszta uchodzi | **cel potwierdzony, ale jako regula warunkowa, nie stala** | wzor od poscigu konnicy, terenu, przewagi i jakosci (rozdz. 4.3); typowo 15-30%, skrajnie do 65% |
| glod po najezdzie - nie liczony | w najgorszych przypadkach glod zabijal wiecej niz miecz (Harrying 1069-70, Wegry 1241-43; w XVII w. Niemcy: glod 12% raportow o kryzysach, dzialania wojsk 3%); pojedynczy najazd glodu nie wywolywal (1339: zebracy i pomoc, bez zgonow glodowych w rejestrze) | **luka w modelu** | **glod regionu od udzialu spustoszonych w ostatnim roku**, prog 15%, do 12% rocznie (rozdz. 3.3) |

---

## 1. Jak czytac liczby ze zrodel

**Kronikarze zawyzaja 3-20 razy.** Tam, gdzie mozna porownac z dokumentem albo liczeniem:

| Przypadek | Liczba wiarygodna | Kronika |
|---|---|---|
| Cassel 1328 | spis konfiskat: 3 185 zabitych | 9-22 tys. |
| Limoges 1370 | ok. 300 cywilow (Sumption) | 3 000 (Froissart) |
| Grunwald 1410 | 8 000 "po obu stronach" (list z sierpnia 1410) | 50 tys. (Dlugosz) |
| Yorkshire 1069-70 | cale Yorkshire w 1086: 28.5 tys. (Russell), x2 = ok. 57 tys. | 100 tys. zmarlych z glodu (Orderic Vitalis) |

**Wyceny podatkowe zawyzaja szkody, bo ulga byla ich celem.**
- Nova Taxatio diecezji Carlisle podaje -85% (1291 -> 1318).
- Briggs (2005) pokazal jednak, ze w Cumberland 1332-48 spadek podatku swieckiego wynikal w rownym stopniu z oporu podatnikow, co ze zniszczen.
- "Waste" w Domesday to czesto kategoria rachunkowa (brak nadwyzki dla pana), a nie zgliszcza (Lewis 2023).

**Po 1348 wojny nie da sie oddzielic od zarazy.** Saint-Lo stracilo 47% dymow w latach 1343-1365: tu najazd 1346 i Czarna Smierc 1348 dzialaly razem.

**Probka jest stronnicza.** Dobrze opisano wielkie, jednostronne kleski i slawne rajdy. Dla malych starc i drobnych najazdow - czyli wiekszosci tego, co gra rozstrzyga sama - liczb prawie nie ma.

---

## 2. Rabunek okregu (zalozenie 1)

### 2.1 R - ilu ludzi dotyka jeden napastnik na dobe

| Przypadek | Skladniki | R [RACH] | Pewnosc skladnikow |
|---|---|---|---|
| Langwedocja 1355 (Czarny Ksiaze) | 4-6 tys. ludzi (Sumption), 59 dni (5 X - 2 XII), ok. 500 wsi (Wagner 2006; przypisywane listowi Wingfielda, nie sprawdzone u zrodla); wies 200-450 ludzi (ZALOZENIE) | **0.28-0.95** | armia i czas [HIST], liczba wsi [HIST?], wielkosc wsi zalozona |
| Cambresis i Thierache 1339-40 (Edward III, potem Jan z Hainaut) | 174 parafie wspomozone w 1340 (tylko czesc krolewska, cztery diecezje; zawiera tez najazd 1340); palenie ok. 30 dni (od 20 IX); armia 10-15 tys. (ZALOZENIE - liczebnosci nie potwierdzono); parafia 240-400 ludzi | **0.09-0.23** | 174 parafie [PEWNE] (Carolus-Barre 1950), reszta zalozona |
| Yorkshire 1319 (Szkoci) | 106 wsi zwolnionych z podatku po spaleniu (49 North Riding, 57 West Riding) w ok. 3 tygodnie; 5-10 tys. ludzi (ZALOZENIE); 190 ludzi na wies | **0.10-0.19** | aneks 1.2; wsie [PEWNE] |
| Brandenburgia 1326 (Litwini) | 6 000 jencow / 1 200+ ludzi / ok. 30 dob | **do 0.17 - same jency** | [KRON] |
| Reiverzy 1592 (Burgh by Sands, Newby) | 23 jencow na 300 napastnikow; 16 jencow na 80 - w jedna noc | 0.08-0.2 jenca na napastnika na najazd | [PEWNE], skarga strony |

**Szerokosc pasa spalen.** 50-65 km w 1339 (list Edwarda III: 12-14 lig), ok. 64 km w 1346, ok. 40 km i 18 000 km2 w 1355 (Rogers). Lancaster w 1356: 2 300 ludzi, 22 dni, 530 km.

**Wniosek.** Historyczny zakres to 0.1-1.0:
- najazdy po lup, bydlo i okup (Szkoci, 1339): 0.1-0.2;
- wielka wyprawa palaca systematycznie (1355): 0.3-0.95.

Zrodlowe R to srednia z calej wyprawy, liczona z dniami marszu, postoju i oblezenia (np. Cambrai 1339). W grze R mnozy tylko godziny lupienia, a obecnosc armii liczy osobno zerowanie (0.2). Srodek 0.5 w czasie samego lupienia zgadza sie wiec z 0.1-0.5 liczonym na wszystkie dni wyprawy.

**Werdykt: R = 0.5 zostaje.** Widelki 0.2-1.0. Wyzej niz 1.0 to juz poza wszystkimi przypadkami.

### 2.2 Ilu ginie przy rabunku

| Przypadek | Liczby | Na ludzi [RACH] | Pewnosc |
|---|---|---|---|
| La Capelle 1339 (wies spalona razem z kosciolem) | 88 wspomozonych gospodarstw; 3 wdowy po zabitych ("occisus", raz "per Anglicos") | 3.4% wspomozonych gospodarstw, ok. 1% ludzi przy 3-4 osobach na gospodarstwo; wobec calej wsi jeszcze mniej, bo pomoc dostawali tylko najbiedniejsi | [PEWNE] Carolus-Barre 1950, s. 222-227 |
| Easingwold 1326 (84 dzierzawcow niezdolnych placic czynsz; najazdy 1311-1322) | 9 zabitych przez Szkotow w najazdach; 22 polegli w bitwach jako pospolite ruszenie (Myton 13, Byland 9); 10 zmarlo w nedzy; 7 wygnanych bieda; 17 zebrakow; 17 zubozalych i spalonych | zabici w najazdach to 11% tych 84 w ciagu dekady, czyli kilka procent na najazd wsrod zrujnowanych i ok. 1% wobec wszystkich dzierzawcow dworu | [PEWNE] McNamee 1997, rozdz. 3 |
| Lanercost, 1311 | Bruce "zabil niewielu poza tymi, ktorzy stawiali opor"; najazd wrzesniowy zabil wiecej | - | [KRON] |
| Reiverzy 1592 | Newby: 80 napastnikow, 1 zmarl od ran, 16 w niewoli; Burgh: 300 napastnikow, 3 rannych, 23 w niewoli | 0-1 zabitych na najazd | [PEWNE] |
| Miasto wziete szturmem: Caen 1346 | co najmniej 2 500 cial w masowych grobach (Sumption 1990, s. 510) | - | [HIST] |
| Miasto wziete szturmem: Limoges 1370 | ok. 300 cywilow i 60 zalogi; "moze 1/6 zwyklej ludnosci" (Sumption 2009); Froissart podaje 3 000 | ok. 15-20% | [HIST] |
| Najazdy litewskie XIII-XIV w. | zwyklych mezczyzn zwykle zabijano na miejscu, kobiety i dzieci brano w niewole | - | [HIST] Baronas |

**Wniosek.**
- Rabunek wsi zabijal zwykle ok. 1% ludzi okregu, najwyzej ok. 5%. 5% to gorny kraniec, nie norma.
- 15-30% to miasta brane szturmem. W grze liczy to juz BK (rzez po oblezeniu 10-24%, `DEMOGRAFIA-SILA-ROBOCZA` G3), co zgadza sie ze zrodlami.
- Wyzej bylo tylko przy najazdach po niewolnikow, gdzie mezczyzn zabijano.
- Obroncy wsi gineli osobno, w starciu. W Easingwold na 22 poleglych w bitwach przypada 9 zabitych w najazdach. Gra liczy milicje osobno i to sie zgadza.

**Propozycja:**
- zabici **2%** trafionych (widelki 1-5%);
- **5%** dla kultur lowiacych niewolnikow (zabijaja opornych mezczyzn);
- limit "nie wiecej niz jeden na napastnika na dobe" zostaje.

### 2.3 Jency (tylko kultury z niewolnictwem - decyzja Jeffa 07.10, pkt 6a)

| Przypadek | Liczby | Pewnosc |
|---|---|---|
| Najazdy litewskie na Polske do 1376 | 500-600 jencow na wiekszy najazd, lacznie ok. 12 500 | [HIST] Baronas (za Lietuvos istorija t. 3, 2011) |
| Najazd litewski na Polske 1376 | 23 tys. jencow - wiecej niz cala suma wg historykow, wiec przesada | [KRON], Wikipedia bez przypisu |
| Brandenburgia 1326 | 6 000 jencow, do 0.17 jenca na napastnika na dobe | [KRON] |
| Irlandia (Annals of Ulster) | 895: 710 z Armagh; 951: 3 000+ z Kells (klasztory skupialy ludzi, wiec to gorna granica) | [KRON] |
| Reiverzy 1592 | 0.08-0.2 jenca na napastnika na najazd (pod okup) | [PEWNE] |
| Tatarzy krymscy XVI-XVII w. (spoza epoki) | ok. 10 tys. rocznie | [HIST] Kolodziejczyk 2006 |

**Przeliczenie [RACH].** 0.02-0.2 jenca na napastnika na dobe, srodek ok. 0.05. Przy R = 0.5 daje to 4-40% trafionych, srodek ok. 10%.

**Propozycja dla kultur z niewolnictwem:** **jency 10% trafionych** (widelki 5-30%), wzieci z czesci uchodzcow. Dla pozostalych kultur 0.

**Uwaga o BK.** BK bierze `min(10% chlopow, 0.5 x (N - 5))`, do 150 jencow. To 0.5 jenca na napastnika na najazd, 2.5-6 razy wiecej niz u reiverow.

### 2.4 W las (do bandytow)

**Stopy nie ma w zadnym zrodle.** Sa tylko dane jakosciowe:
- **Normandia 1417-36** (Evans 1992):
  - bandy siedzialy w lasach;
  - 49 z 246 listow laski (20%) dotyczy bandytyzmu;
  - wsrod 115 osob o znanym zawodzie 43% to laboureurs;
  - szczyty egzekucji przypadaja na lata 1419-20, 1424 i 1436;
  - Bois: do lasu szli raczej zamozniejsi chlopi, ktorym wojna zniszczyla gospodarstwa.
- **Easingwold 1326:** wsrod 84 zrujnowanych nie ma ani jednego wyjetego spod prawa. Jest 17 zebrakow i 7 wygnanych bieda.
- **Pogranicze szkockie:** teza Frasera (Steel Bonnets, 1971), ze rozboj rodzil sie z ciaglej wojny, jest jakosciowa i nie byla czytana.

**Wniosek.** Pojedynczy najazd robil zebrakow. Bandytyzm rosl przy dlugiej wojnie i okupacji.

**Propozycja [PROJ]:** w las idzie **2% + 3% x niebezpieczenstwo regionu**, czyli 2% w spokojnym regionie i do 5% w dlugiej wojnie. Niebezpieczenstwo to ta sama miara 0-1, ktora hamuje przyrost i powroty. To tylko kierunek ze zrodel, nie liczba ze zrodel.

### 2.5 Uchodzcy: dokad uciekali i kiedy wracali

**Dokad.**
- Do miast murowanych i zamkow: Saint-Quentin i Laon w latach 1339-40, Mulhouse i Strasburg w XVII w.
- Na wsi "sauve-qui-peut": opor stawialy tylko zamki i miasta.
- Cumberland 1313: "wielka czesc" mezczyzn uciekla z dobytkiem; w Acomb i Wall zostalo po 2 ludzi.
- Po Harrying (1070) uchodzcow notuje kronika Evesham, ok. 150 mil na poludnie.

**Pojedynczy najazd, potem pokoj** (1339-40):
- wszystkie 174 parafie odbudowano;
- krol dal ulgi podatkowe, a opactwu Saint-Amand moratorium na dlugi na 5 lat (1341); Avignonet zwolniono z podatkow na 7 lat (po 1355);
- ok. 12% parafii figuruje pozniej jako "pauper", ale to nie musi wynikac z najazdu (po drodze byla Czarna Smierc);
- [PEWNE] / [HIST].

**Najazdy co rok, potem rozejm 1323** (Northumberland, McNamee 1997):
- Stenton stala pusta 4 lata; Tarset w 1326 nie miala dzierzawcow;
- welna wrocila do poziomu z 1312 r. 7-8 lat po rozejmie (Ponteland 1329/30, Norhamshire 1330/31);
- mlyny po 13 latach dawaly ok. 1/3 dawnego dochodu;
- czynsze Durham Priory spadly o 14%;
- dziesieciny zbozowe Holy Island "nigdy w pelni" nie wrocily;
- [PEWNE].

**Dluga wojna** - powrot prawie zerowy, odbudowa przez przybyszow:
- Ile-de-France: od 1417-18 ubytek ludnosci wsi wynikal glownie z ucieczek; uchodzcy "zalali Paryz". Odbudowa po 1450 przyszla z przybyszami (mieszkancy Liege, Bretonczycy od 1475). Pelna uprawa wrocila ok. 1520. Dekanat Montmorency mial w 1470 mniej niz 1/3 stanu z 1328 [HIST] (Fourquin 1964).
- Yorkshire po Harrying: w 1086 wciaz 1/3 ziemi "waste". W XII w. wsie odbudowano od nowa na planie regularnym (kolonizacja) [HIST].
- Mnisi z Noirmoutier nigdy nie wrocili: 39 lat wedrowki (836-875) [PEWNE].
- Wegry po 1242: sciagano osadnikow [HIST].

**Jak zyli uchodzcy.**
- La Capelle: ok. 30% wpisow to zebranie; jeden czlowiek "nie smie z powodu wojen wrocic do swoich posiadlosci".
- Strasburg 1622: 9 812 doroslych uchodzcow, 1/5 zmarla w kilka miesiecy (Lammert, za Outram 2001, s. 178).
- Mulhouse: uchodzcy umierali z glodu, chowano ich w masowych grobach.
- W dolinie Werry chrzty spadly do 60%, potem ponizej 25% normy; w Augsburgu do 55-65% (Outram 2001, s. 154-155).

**Wnioski:**
1. Dzisiejszy powrot (0.1% dziennie x (1 - niebezpieczenstwo); [RACH]: polowa po 1.9 / 2.4 / 3.8 roku przy niebezpieczenstwie 0 / 0.2 / 0.5; 87% po 7 latach przy 0.2) **zgadza sie z pojedynczym najazdem w pokoju**: ulgi 5-7 lat, welna 7-8 lat.
2. **Przy trwajacych najazdach nikt nie wracal** (Stenton, La Capelle, Cumberland). Dzis w wojnie polowa wraca po 3.8 roku, nawet miedzy kolejnymi rabunkami - to za szybko.
3. Uchodzcy mieli mniej dzieci i wiecej zgonow. Dzis konto uchodzcow ma przyrost 0, czyli tyle samo urodzen co zgonow.

**Propozycja:**
- Tempo **0.1% dziennie x (1 - niebezpieczenstwo) zostaje**.
- **[PROJ] Nowe: nikt nie wraca do wsi, jesli w jej regionie w ostatnich 30 dobach byl rabunek albo zerowanie armii** (strach przed powrotem). Dzis blokada dziala tylko w stanie "spalona", czyli 8-17 dob.
- **Nowe: konto uchodzcow ma przyrost -1.3% rocznie** - to istniejace dno przyrostu z kroku 3, Wielki Glod w Anglii 1315-25. Uzasadnienie: chrzty spadaly o 35-75%, a zgony nie malaly.
- **Nowe: przyrost uchodzcow -20% rocznie, gdy warownia regionu gloduje albo jest oblezona.** W Ypres zmarlo 10% calego miasta od maja do pazdziernika 1316; w Strasburgu 1/5 uchodzcow w kilka miesiecy 1622 r.
- [RACH] Przy -1.3% rocznie i powrocie jak dzis ginie albo odplywa 3.4 / 4.9 / 6.7% uchodzcow (niebezpieczenstwo 0 / 0.3 / 0.5). Reszta wraca.

### 2.6 Plon i kapital

| Przypadek | Liczby | Pewnosc |
|---|---|---|
| Wark-on-Tweed 1323 | 936 akrow ziemi dworskiej, ktorej "nikt nie smial uprawiac ze strachu przed Szkotami", i 416 akrow lanow chlopskich odlogiem | [PEWNE] |
| Penrith 1317/18 | wydzierzawiono 15 ze 120 akrow (12.5%) | [PEWNE] |
| Castle Sowerby | cale 254.5 akra i 664 akry karczunkow odlogiem | [PEWNE] |
| Mlyny Norhamshire | 3 L 10 s; wszystkie zniszczone w 1316/17; 1 L 3 s w 1329, czyli 1/3 po 13 latach | [PEWNE] |
| Dziesieciny zbozowe Holy Island | 112 -> 47 -> 21 -> 15 L (1326-28), czyli -87%; "nigdy w pelni" nie wrocily | [PEWNE] |
| Stada | welna wrocila po 7-8 latach, bo stada ewakuowano; Jarrow -96% owiec (1313 -> 1326, razem z pomorem); Bolton: zabrano 43 woly | [PEWNE] |
| Jean de Venette, 1358-59 | winnice nieprzyciete, pola niezasiane, bydla brak | [KRON] |
| Gorna Normandia po dnie 1463-64 | produkt rolny +45% w ok. 30 lat; w 1500 r. produkcja z ok. 1400 r. | [HIST] Bois 1976, wg Le Roy Ladurie 1978 |

**Wniosek.** W pustoszonym okregu plon spadal bardziej niz liczba ludzi:
- ginal kapital: woly, ziarno siewne, mlyny, stodoly;
- pozostali nie uprawiali dalszych pol ze strachu.

Elastycznosc 0.5 (Broadberry po 1348) opisuje ubytek ludzi przy calym kapitale i nadmiarze ziemi, czyli zaraze, a nie spustoszenie. Stan "spalona" w grze (8-17 dob bez plonu) niesie tylko ulamek tej straty, bo mlyny i woly wracaly latami.

**Propozycja:** elastycznosc plonu **1.0 dla rak brakujacych po spustoszeniu**: -10% rak = -10% plonu. Tak proponowal tez aneks w rozdz. 1.4 (eS = 1).

### 2.7 Pulap 90% okregu

Dane:
- Yorkshire 1086: 60% gospodarstw "waste", zostalo 25% ludzi (Muir; sporne - Lewis 2023);
- Gorna Normandia na dnie: 25-30% stanu z 1314 r. (wojna, zaraza i glod razem);
- Montmorency 1470: ponizej 1/3 stanu z 1328 r.;
- trwale opuszczonych osad na Polnocy Anglii bylo "bardzo malo" (Mortham), ale Noirmoutier opuszczono na zawsze.

**Pulap 90% wyrzuconych z domu zgadza sie z danymi. Zostaje.**

### 2.8 Parametry rabunku - zestawienie

| Pokretlo | Paczka 113 | Propozycja | Widelki | Podstawa |
|---|---|---|---|---|
| R rabunku (ludzi na napastnika na dobe lupienia) | 0.5 | **0.5** | 0.2-1.0 | 2.1 |
| Zabici z trafionych | 5% | **2%** (kultury z niewolnictwem 5%) | 1-5% | 2.2 |
| Limit zabitych | 1 na napastnika na dobe | bez zmian | - | - |
| Jency (tylko kultury z niewolnictwem) | - | **10%** trafionych | 5-30% | 2.3 |
| W las | 5% | **2% + 3% x niebezpieczenstwo** | 1-5% | 2.4, bez danych liczbowych |
| Uchodzcy | 90% | reszta: ok. 93-96%, przy niewolnictwie ok. 83-88% | - | 2.5 |
| Powrot | 0.1% dziennie x (1 - niebezp.) | bez zmian | 0.07-0.1% | 2.5 |
| Blokada powrotu | tylko wies spalona albo oblezona | **+ 30 dob po kazdym rabunku albo zerowaniu w regionie** | 14-60 dob | 2.5 |
| Przyrost uchodzcow | 0 | **-1.3% rocznie; -20% rocznie, gdy warownia gloduje albo jest oblezona** | -1..-2% / -10..-60% | 2.5, 3.1 |
| Elastycznosc plonu | 0.5 | **1.0** | 0.5-1.0 | 2.6 |
| Pulap | 90% | bez zmian | - | 2.7 |

**Co to daje w liczbach gry** (pelny rabunek pocztu 112 ludzi = 135 trafionych, wies-mediana):

| | Paczka 113 | Propozycja |
|---|---|---|
| Zabici | 6.8 | 2.7 |
| W las | 6.8 | 2.7-6.8 |
| Uchodzcy | 121.5 | 125-130 |

W skali swiata zabitych przy spustoszeniu bedzie 2.5 raza mniej: ok. 0.14-1.3 tys. rocznie zamiast 0.35-3.3 tys. To dalej ponizej 1-2% tego, co gina w bitwach.

---

## 3. Glod po najezdzie (zalozenie 3)

### 3.1 Dane

| Przypadek | Co sie stalo | Liczby | Pewnosc |
|---|---|---|---|
| Harrying of the North, zima 1069-70 | spalone zbiory i stada w calym Yorkshire; Symeon z Durham: jedzono konie, psy i ludzi, ludzie sprzedawali sie w niewole; kronika Evesham: uchodzcy w Worcestershire umierali | Orderic: ponad 100 tys. zmarlych z glodu - przesada, cale Yorkshire mialo 28.5-57 tys.; Domesday 1086: 60% gospodarstw "waste", 1/3 ziemi nadal "waste" po 16 latach | [KRON]; Lewis 2023 [HIST]: rutynowa operacja, ktora wyjatkowo wypadla w srodku zimy, stad lokalny glod i kryzys uchodzcow |
| Wegry 1241-42 (Mongolowie) | Tomasz ze Splitu: chlopi nie zebrali w 1241 i nie zasiali w 1242; Szymon z Saint-Quentin: "w kazdym kraju, ktory niszcza Tatarzy, zawsze przychodzi glod"; Rogerius: obiecano zycie tym, ktorzy wroca do pracy w polu, a po zniwach ich wymordowano | straty ludnosci 15-20% (Fugedi, Engel) do 50% (Gyorffy); zniszczenia skupione na Wielkiej Nizinie | [HIST] Laszlovszky i in. 2018. Zapis austriacki "glod 1243 zabral wiecej ofiar niz poganie" - podany przez badacza, w artykule nie odnaleziony: **niesprawdzone** |
| Polnoc Anglii 1316 i 1322 | 1316: Flores Historiarum wymieniaja naraz najazdy Szkotow, glod i zaraze; 1322: Szkoci spalili stodoly pelne zboza, nastepnego lata przyszedl glod | brak rozdzialu liczb; kwarta pszenicy po 40 s (1316) | [KRON] McNamee 1997; Lanercost |
| Irlandia 1315-18 (Edward Bruce) | niszczono cala zywnosc poza potrzebami armii, w samym srodku Wielkiego Glodu; armia Bruce'a sama ginela z glodu | brak liczb | [KRON], opis popularny (Joyce) |
| Thierache 1339-40 (pojedynczy najazd i pomoc) | pomoc papieska (6 000 florenow) i krolewska, ulgi podatkowe | ok. 30% wpisow w La Capelle to zebranie; w Saint-Quentin nedzarze "trahens ad mortem"; zgonow glodowych rejestr nie liczy; wszystkie parafie odbudowane | [PEWNE] |
| Langwedocja 1355, Normandia 1346 | zrodla nie notuja glodu po rajdzie | - | badacz "chevauchee": nie znaleziono |
| Normandia 1415-1450 | nieurodzaje 1420-21 i inflacja razem z wojna; minimum produkcji zboza ok. 1430 i wielkie glody lat 1420-30 | w latach 1422-23 "ledwie polowa" ludnosci z 1400 r. | [HIST] Bois wg Evans 1992; Le Roy Ladurie 1975 |
| Skala glodu bez wojny: Wielki Glod 1315-17 | plony spadaly do ok. 2:1 (zwykle ok. 4:1); pszenica w Lotaryngii +320% | poludniowa Anglia 10-15% (Freedman 2000), polnocna Francja ok. 10% (Leguay 2000), miasta 10-25%; Ypres stracilo 10% ludnosci od maja do pazdziernika 1316; Anglia 4.69 -> 4.12 mln w latach 1315-1325 (-12%, czyli -1.3% rocznie przez dekade) | [HIST] Wikipedia za Freedman, Leguay, Ruiz; medievalists.net; Broadberry i in. |
| Niemcy 1618-48 (spoza epoki) | 348 raportow o przyczynach kryzysow smiertelnosci: zaraza 64%, glod 12%, dzialania wojsk 3%; zgony w bitwach to ponizej 5% ubytku ludnosci | Grandenborn (Hesja) 1626: 187 pochowkow wobec najwyzej 25 rocznie (7x), a zolnierze zabili 4 cywilow; Strasburg 1622: 1/5 z 9 812 uchodzcow zmarla w kilka miesiecy | [HIST] Outram 2001, s. 152-160, 177-179 |
| Parma i Piacenza 1635-37 (spoza epoki) | kilka miesiecy okupacji hiszpanskiej | nadwyzka zgonow ok. 4% calej ludnosci, glownie glod i choroby, nie przemoc | [HIST] Hanlon 2012 |

### 3.2 Kiedy wojna rodzila glod

1. **Gdy zniszczenie objelo duza czesc regionu**, a nie pas 40-60 km przez kraj: cale Yorkshire (1069-70), Wielka Nizina (1241-42), cala Normandia przez lata (1417-50). Po wielkich chevauchees (1339, 1346, 1355) glodu nie notowano.
2. **Gdy trafilo w zapasy i ziarno siewne:** zima (Harrying), stodoly po zniwach (1322), brak zniw i siewu (1241-42).
3. **Gdy nalozylo sie na nieurodzaj:** 1315-22 na Polnocy Anglii i w Irlandii, 1420-21 w Normandii.
4. **Gdy brakowalo pomocy i rynku.** W latach 1339-40 pomoc i ulgi daly zebrakow, nie trupy.
5. **Glod zabijal miesiace po zniszczeniu**, na przednowku albo nastepnego lata (1322 -> 1323, 1242 -> 1243). Najpierw umierali najslabsi i uchodzcy stloczeni w miastach (Evesham, Strasburg, Mulhouse).

**Pojedynczy rabunek na ulamku okregu glodu nie wywolywal.** W najgorszych przypadkach glod i choroby zabijaly jednak wielokrotnie wiecej niz miecz.

### 3.3 Propozycja: glod liczony od skali spustoszenia regionu

**(a) Glod wsi po spustoszeniu [PROJ z kotwicami].** To jest czlon "glod wsi z jej wlasnego stanu (zima, spalenie)", ktory krytyk kroku 3 kazal zrobic zamiast spichlerza warowni (`DEMOGRAFIA-SILA-ROBOCZA` rozdz. 4.1, [KRYT 5]). Tu dostaje liczby.

1. **F** = (trafieni w regionie w ostatnich 364 dobach, kazdy razy waga pory roku rabunku) / ludzie wsi regionu (w domu i uchodzcy).
2. **Waga pory roku [SZAC]:**
   - jesien 1.0 i zima 1.0: zapasy i ziarno siewne, nie ma czym zastapic do zniw (Harrying, 1322);
   - lato 0.75: zboze na polu, zniwa (1241);
   - wiosna 0.5: zapasy prawie zjedzone, pole mozna obsiac jeszcze raz.
3. **Dodatkowe zgony** wsrod ludzi wsi regionu (w domu i na koncie uchodzcow): **15% x max(0, F - 0.15) rocznie, najwyzej 12% rocznie**. Liczone co dobe (/364), az F spadnie. Zmarli ida do ksiegi zmarlych regionu jako osobna pozycja "z glodu".
4. **Kotwice:**
   - Ponizej 15% regionu glodu nie ma: 1339 i 1355 (pomoc, rynek, sasiedzi); zwykla zmiennosc plonow (+-20%) nie zabijala masowo.
   - F = 0.5 daje 5.25% rocznie, przez 2 lata ok. 10%. To Wielki Glod: plony ok. polowy przez dwa lata, 10-15% zgonow.
   - F = 1.0 daje sufit 12% rocznie. To Wegry 1241-43: dwa sezony bez zniw i siewu, laczne straty 15-20% wg Engla.
   - Parma 1635-37: 4% w kilka miesiecy, z chorobami - rzad sufitu.
5. **Dzis w kroku 3** czlon glodu H ma dno -1.3% rocznie. To srednia dziesieciolecia Wielkiego Glodu, a w ostrym roku umieralo 5-15%. Dla wsi ten czlon zastepuje H; jeden glod nie moze byc liczony dwa razy.

**(b) Uchodzcy** (rozdz. 2.5): -1.3% rocznie; -20% rocznie, gdy warownia regionu gloduje albo jest oblezona.

**Co to zmienia w grze [RACH]** (przyklady z paczki 113, F liczone na wies):

| Przypadek | F | Glod rocznie |
|---|---|---|
| jeden rabunek pocztu 112 ludzi na wsi-medianie (0.22%) | ok. 0.002 | 0 |
| armia 2000 przez tydzien w okregu (4.3%) | 0.04 | 0 |
| najgoretsza wies sesji, Wrath Village (ok. 6-7% trafionych rocznie) | ok. 0.05 | 0 |
| wies Wolnych Ludzi, 12 rabunkow rocznie (ok. 22% trafionych, waga ok. 0.8) | ok. 0.18 | ok. 0.4% |
| wies Nocnej Strazy, 4 rabunki rocznie (ok. 30%, waga ok. 0.8) | ok. 0.24 | ok. 1.4% |
| region spustoszony w polowie jesienia (wielka armia stojaca tygodniami) | 0.5 | 5.25% |
| region spustoszony prawie caly zima | 0.9-1.0 | 11-12% |

Glod pojawia sie tylko tam, gdzie region spustoszono masowo - tak jak w historii. Na co dzien w duzych krainach glodu nie ma, a w malych, nekanych bez przerwy (Nocna Straz, Wolni Ludzie) daje 0.5-1.5% zgonow rocznie.

**Do pomiaru przed wlaczeniem:** jak czesto warownie gloduja (`IsStarving`). Zaden log tego dotad nie podaje, a od tego zalezy czlon -20% dla uchodzcow.

---

## 4. Bitwy rozstrzygane przez gre (zalozenie 2)

### 4.1 Dane: zabici wsrod przegranych

Liczby z prac Rogersa, Ayton, Curry, Sumptiona, Verbruggena, DeVriesa, Given-Wilsona i Beriac oraz z archeologii Visby i Towton. Wiekszosc za Wikipedia, z numerami stron autorow (patrz rozdz. 7).

| Typ starcia | Zabici przegranych | Przypadki |
|---|---|---|
| Bitwy rycerskie XII-XIII w. (rycerz wart okupu) | 0.3-13%; do niewoli nawet 49-65% | Bremule 1119: 3 na ok. 900; Lincoln 1217: 3 zabitych, 300-400 rycerzy w niewoli; Bouvines 1214: 70-169 z 1 300-1 500 rycerzy |
| Odwrot w porzadku, czesc armii uchodzi | **5-25%** | St Albans 1455: 5%; Neville's Cross 1346: 8-25% (trzeci hufiec uciekl prawie bez strat); Poitiers 1356: 16-18% (hufiec Orleanu odszedl); Halidon 1333: ok. 19%; Crecy 1346: ok. 19% zbrojnych dnia 1; Cassel 1328: co najmniej 21% (spis konfiskat [PEWNE]); Otterburn 1388: 23% |
| Kleska z poscigiem konnicy albo w pulapce terenu | **38-63%** | Sempach 1386: 38%; Verneuil 1424: 38-57% (bez pardonu); Azincourt 1415: 40-50% (scisk w blocie); Patay 1429: 40%+ (konnica na lucznikow); Courtrai 1302: 40-60% konnicy (rowy i strumien); Stoke 1487: ok. 50% (wawoz); Formigny 1450: 58-63% |
| Okrazone pospolite ruszenie chlopskie | **85-90%** | Visby 1361: 1 185 szkieletow z trzech grobow, czyli co najmniej 59% armii [PEWNE] |
| Jency przegranych | 1-20%, prawie tylko szlachta | Poitiers 13-21%; Azincourt 5-18%; Otterburn 13%; Verneuil 1%; Neville's Cross ponizej 1% |
| Zwyciezca | 1-5% | Crecy ok. 2%, Courtrai 1-4%, Roosebeke 1%, Dupplin 2%; wyjatki: Sempach 13-60%, Visby 12-15% |

**Gra dzis.** Zabici przegranych: 51% we wszystkich starciach, 42.6% w 13 prawdziwych bitwach. Przegrany traci 100% ludzi: 57.7% zabitych i 35.2% rannych do niewoli, nikt nie uchodzi. Zwyciezca traci 3.6% - to zgodne z historia.

### 4.2 Od czego zalezalo

1. **Czy przegranego oplacalo sie brac zywcem.** Od ok. 1300 bitwy rozstrzygala piechota z ludu. Prosty zolnierz nie byl wart okupu, a luk i pika zabijaja z dystansu, wiec od tego czasu ginelo zwykle 15-40% (Rogers 1993). Okup dla prostych upowszechnil sie dopiero w XV w. (Ambuhl 2013).
2. **Wiekszosc ginela po zlamaniu szyku:** w ucieczce, w rzekach, w scisku, przy dobijaniu rannych i z rak miejscowej ludnosci (Towton wg Rossa, Aljubarrota, Crecy dnia 2, 13 km poscigu pod Halidon). Dla antyku jest pomiar: do zlamania szyku straty wynosily 5% lub mniej (Sabin), przegrani tracili ok. 14%, zwyciezcy ok. 5% (Krentz 1985).
3. **Masowo gineli, gdy:**
   - ucieczke zamykal teren: rzeka, bagno, wawoz, zamknieta brama;
   - zwyciezca mial konnice do poscigu, a przegrani byli pieszo;
   - obowiazywal rozkaz "bez pardonu": wobec buntownikow, chlopow, Szkotow, w bitwach szwajcarskich;
   - zawodowcy bili pospolite ruszenie;
   - miejscowa ludnosc dobijala uciekajacych.
4. **Uchodzili, gdy:**
   - mieli konie;
   - czesc armii nie weszla do walki;
   - zapadla noc;
   - zwyciezcy byli pieszo albo zajeli sie lupem i jencami.

### 4.3 Propozycja: zabici przegranych od sytuacji [PROJ z kotwicami]

Dla kazdego zolnierza strony przegranej:

**Zabity z szansa p** = 0.15 + 0.35 x C + 0.15 x T + 0.15 x O + 0.20 x Q (+ 0.25 x N), w granicach **5-65%**:
- **C - poscig konnicy** = udzial konnych u zwyciezcy minus udzial konnych u przegranego (0-1). Konni przegrani uciekaja, piesi nie.
- **T - pulapka terenu** = 1, gdy teren bitwy to rzeka, brod, most, bagno, jezioro albo brzeg; inaczej 0 (typ terenu starcia w grze - do sprawdzenia w kodzie).
- **O - przewaga** = (sila zwyciezcy / sila przegranego - 1.5) / 1.5, w granicach 0-1. Od 3:1 wzwyz O = 1 (okrazenie).
- **Q - roznica jakosci** = (sredni tier zwyciezcy - sredni tier przegranego) / 3, w granicach 0-1 (zawodowcy na pospolite ruszenie).
- **N - bez pardonu** = 1 przy Innych, w obie strony. Wtedy jencow nie ma.

**Jeniec** (sposrod tych, co przezyli): bohaterowie jak dzis; tier 4 i wyzej (zbrojni, rycerze) 30%; reszta 5%. Lacznie wychodzi zwykle 3-15% przegranych, w zgodzie z widelkami 1-20%.

**Reszta uchodzi** i wraca do ksiegi ludzi swojego regionu, skad mozna ich znow zwerbowac [PROJ]. Historycznie uciekinierzy wracali do domu albo do swojego pana. Zbiegli w bandy to w zrodlach raczej zwolnieni najemnicy po rozejmach niz uciekinierzy z pola, a na to stopy brak.

**Zwyciezca bez zmian** (dzis 3.6%, historia 1-5%).

**Chlopi i karawany** (strona nie walczaca): zabici 5%, jency 0-5% (tylko kultury z niewolnictwem), reszta ucieka do domu. To ta sama skala co przy rabunku (1-5%).

**Sprawdzenie na bitwach [RACH]:**

| Bitwa | C | T | O | Q | Wzor | Historia |
|---|---|---|---|---|---|---|
| Neville's Cross 1346 | 0.2 | 0 | 0 | 0 | 22% | 8-25% |
| Poitiers 1356 | 0.1 | 0 | 0 | 0 | 18% | 16-18% |
| Crecy 1346, dzien 1 | 0 | 0 | 0 | 0 | 15% | ok. 19% zbrojnych |
| Halidon Hill 1333 | 0.35 | 0 | 0 | 0 | 27% | ok. 19% |
| Courtrai 1302 | 0 | 1 | 0 | 0 | 30% | 12-19% armii (40-60% konnicy) |
| Towton 1461 | 0.1 | 1 | 0 | 0 | 34% | ok. 30% (9 tys. wg Annales) |
| Patay 1429 | 0.8 | 0 | 0 | 0 | 43% | 40%+ |
| Stoke 1487 | 0.2 | 1 | 0 | 0 | 37% | ok. 50% |
| Formigny 1450 | 0.5 | 0 | 0.3 | 0 | 37% | 58-63% |
| Visby 1361 | 0 | 0.5 | 0 | 1 | 42% | 59-90% |
| Verneuil 1424 (bez pardonu, N = 1) | 0.1 | 0 | 0 | 0 | 43% | 38-57% |
| St Albans 1455 | 0 | 0 | 0 | 0 | 15% | 5% |
| Azincourt 1415 | 0 | 0 | 0 | 0 | 15% | 25-50% |

Wzor trafia w przypadki typowe. Skrajnosci zaniza:
- scisk w blocie pod Azincourt;
- podwojne okrazenie pod Formigny;
- mury zamkniete przed Gotlandczykami pod Visby.

Krotka walke uliczna (St Albans) zawyza. Gra nie zna tych okolicznosci, a dolozenie ich nie ma oparcia w danych z malych starc. Srednio wzor daje 15-30%, zamiast dzisiejszych 51%. To polowa obecnych zgonow w bitwach, zgodnie z hamulcem H3 ("zolnierze x0.5").

### 4.4 Male starcia: bandy, poczty, zasadzki

Liczb historycznych prawie nie ma, a to wiekszosc starc w grze: 148 ze 161 w logu to lordowie na bandach, karawanach i chlopach. Stosujemy ten sam wzor. Banda piesza, slabsza i gorzej uzbrojona, przegrywajaca z konnym pocztem dostaje C ok. 0.3, O ok. 1 i Q ok. 0.5, czyli ok. 50% zabitych. Zgadza sie to z obrazem zrodel: schwytanych bandytow wieszano (Normandia: nagroda 6 liwrow za wydanego bandyte, szczyty egzekucji w latach 1419-20, 1424 i 1436 - Evans 1992). To ekstrapolacja, nie pomiar.

### 4.5 Skutki w grze (do obserwacji)

- **Mniej jencow:** z ok. 35% przegranych do ok. 3-15%. Mniej okupow i mniej werbunku z jencow. Wiecej ludzi wraca do regionow, wiec swiat wykrwawia sie wolniej.
- **Zbrojownia:** sprzet uchodzacych nie przechodzi na zwyciezce (dzis przechodzi calosc) - do ustalenia przy kodzie (`AUDYT` wojna/W8: latac trzeba `MapEvent.CaptureDefeatedPartyMembers` albo szanse pojmania, a nie `DistributeLootRandomly`).
- **Do logu "Bitwy:"** dopisac uchodzacych przegranych i rozbicie p na C/T/O/Q, zeby widac bylo, skad bierze sie liczba zabitych.

---

## 5. Tabela zbiorcza - stare i nowe

| Pokretlo | Dzis | Propozycja | Widelki | Uzasadnienie |
|---|---|---|---|---|
| R rabunku | 0.5 | 0.5 | 0.2-1.0 | 1355: 0.3-0.95; 1339 i 1319: 0.1-0.2 na wszystkie dni wyprawy; w grze liczy sie tylko czas lupienia |
| Zabici przy rabunku | 5% | 2% (niewolnictwo 5%) | 1-5% | La Capelle ok. 1%, Easingwold ok. 1% na najazd, reiverzy 0-1 na najazd |
| Jency przy rabunku | - | 10% (tylko kultury z niewolnictwem) | 5-30% | Litwa 500-600 na najazd, 0.02-0.2 jenca na napastnika na dobe |
| W las | 5% | 2% + 3% x niebezpieczenstwo | 1-5% | brak stopy; bandytyzm z dlugiej wojny (Normandia 1417-36), nie z jednego najazdu (Easingwold) |
| Uchodzcy | 90% | reszta | - | - |
| Powrot | 0.1% dziennie x (1 - niebezp.) | bez zmian | - | ulgi 5-7 lat, welna 7-8 lat po rozejmie |
| Blokada powrotu | wies spalona albo oblezona | + 30 dob po rabunku albo zerowaniu w regionie | 14-60 dob | La Capelle "nie smie wrocic", Stenton pusta 4 lata przy corocznych najazdach |
| Przyrost uchodzcow | 0 | -1.3% rocznie; -20% przy glodzie albo oblezeniu warowni | - | chrzty -35..75% (Outram); Ypres 10% w 5 miesiecy; Strasburg 1/5 w kilka miesiecy |
| Elastycznosc plonu | 0.5 | 1.0 | 0.5-1.0 | Wark, Penrith, mlyny 1/3 po 13 latach, dziesieciny -87% |
| Pulap | 90% | 90% | - | Yorkshire 1086, Normandia na dnie |
| Glod wsi po spustoszeniu | brak | 15% x max(0, F - 0.15) rocznie, do 12%; waga pory: jesien i zima 1.0, lato 0.75, wiosna 0.5 | - | Wielki Glod 10-15% w 2-3 lata; Wegry 15-20%; pojedyncze rajdy bez glodu |
| Zabici przegranych w bitwie | 51% (wszystkie), 42.6% (prawdziwe bitwy) | 0.15 + 0.35C + 0.15T + 0.15O + 0.20Q (+0.25N), 5-65% | - | 25 bitew 1119-1487 |
| Jency przegranych | ok. 35% | tier 4+ 30%, reszta 5% z przezywajacych | 1-20% | Poitiers 13-21%, Azincourt 5-18% |
| Uchodzacy przegrani | 0 | reszta - do ksiegi ludzi regionu | - | cale hufce uchodzily (Poitiers, Neville's Cross, Bannockburn) |
| Chlopi i karawany pokonani | jak bitwa | zabici 5%, reszta ucieka | 1-5% | skala rabunku |
| Zabici zwyciezcy | 3.6% | bez zmian | 1-5% | Crecy, Courtrai, Roosebeke, Dupplin |

---

## 6. Czego zrodla nie mowia (uczciwie)

1. **"Ludzi na napastnika na dobe" nie podaje zadne zrodlo.** To rachunek z liczby wsi, wielkosci wsi (zalozonej) i armii. Dla 1339 liczebnosci armii nie potwierdzono, dla 1355 liczba 500 wsi pochodzi z drugiej reki.
2. **Odsetek zabitych w najezdzie wiejskim opiera sie na jednym pelnym przypadku** (La Capelle) i jednej grupie wybranej, bo zrujnowanej (Easingwold). Rejestr Carita ma 228 kart, ale opublikowano pelna liste tylko dla jednej wsi.
3. **"W las" - zero liczb.** Sa tylko liczby listow laski i egzekucji z Normandii, bez mianownika.
4. **Tempa powrotu uchodzcow nikt nie zmierzyl.** Zastepczo mamy welne, dziesieciny, czynsze, mlyny i ulgi podatkowe. Mieszaja one powrot ludzi z odbudowa stad i kapitalu, a po dlugiej wojnie takze z naplywem nowych osadnikow, ktorego gra nie liczy.
5. **Glod: zaden sredniowieczny przypadek nie rozdziela liczbowo zgonow z glodu i od miecza.** Wzor z rozdz. 3.3 jest skalowany na Wielkim Glodzie (glod bez wojny) i na Wegrzech (szacunki 15-50%). Dane XVII-wieczne (Outram, Hanlon) sa spoza epoki. Wagi pory roku to szacunek.
6. **Bitwy: rannych nikt nie liczyl.** Liczebnosc armii jest niepewna o 50-100%, piechote przegranych liczono rzadko (Cassel to jedyny spis). Male starcia, czyli wiekszosc tego, co rozstrzyga gra, prawie nie maja liczb.
7. **Nie sprawdzono u zrodla:**
   - Halidon "ok. 2 900 wg Rogersa";
   - Najera "5 000 policzonych przez heroldow";
   - Grunwald "14 000 jencow";
   - zapis austriacki o glodzie 1243;
   - "9 lat" pustki po Harrying;
   - sily szkockie 1319;
   - armia Edwarda III w 1339.

   Nie otwarto (paywall albo brak dostepu): Sumption t. I-III, Rogers "By Fire and Sword" (2002) i "Soldiers' Lives" (2007), Contamine, Verbruggen, DeVries 1996, Darby i Maxwell 1962, pelny Bois 1976 i Fourquin 1964 (znane z recenzji).

---

## 7. Zrodla

**Rabunek i spustoszenie: Francja**
- L. Carolus-Barre, "Benoit XII et la mission charitable de Bertrand Carit dans les pays devastes du nord de la France", Melanges de l'Ecole francaise de Rome 62 (1950), s. 165-232 - https://www.persee.fr/doc/mefr_0223-4874_1950_num_62_1_7358
- Robert z Avesbury, De gestis Edwardi Tertii (list Edwarda III, 1339) - https://www.deremilitari.org/RESOURCES/SOURCES/avesbury.htm
- C. Rogers, "By Fire and Sword", w: Civilians in the Path of War (2002), s. 33-78; tenze, War Cruel and Sharp (2000/2014)
- J. Sumption, Trial by Battle (1990), Trial by Fire (1999), Divided Houses (2009) - za Wikipedia: "Black Prince's chevauchee of 1355", "Crecy campaign", "Battle of Caen (1346)", "Siege of Limoges", "Lancaster's Normandy chevauchee of 1356"
- M. Madden, The Black Prince and the Grande Chevauchee of 1355 (2018) - https://www.cambridge.org/core/books/black-prince-and-the-grande-chevauchee-of-1355/conclusion/A6D55AD80CA1D218F4EF13F50C8C2953
- E. Le Roy Ladurie, "En Haute-Normandie: Malthus ou Marx?", Annales ESC 33/1 (1978), s. 115-124 (o G. Bois, Crise du feodalisme, 1976) - https://www.persee.fr/doc/ahess_0395-2649_1978_num_33_1_293910
- E. Le Roy Ladurie, Annuaire du College de France 1974-1975, s. 555-558 - https://www.college-de-france.fr/sites/default/files/media/document/2024-06/1974-1975_leroyladurie.pdf
- M. R. Evans, "Brigandage and Resistance in Lancastrian Normandy", Reading Medieval Studies 18 (1992), s. 103-134 - https://centaur.reading.ac.uk/84359/
- G. Fourquin, Les campagnes de la region parisienne a la fin du Moyen Age (1964), wg J. Glenisson, Annales de demographie historique 1964 - https://www.persee.fr/doc/adh_1147-1832_1964_num_1964_1_886_t1_0160_0000_2
- M. Arnoux, "Les effets de la peste de 1348 sur la societe normande" (PURH 2008) - https://books.openedition.org/purh/9969
- Jean de Venette, Chronicle (tlum. Birdsall, 1953) - https://www.deremilitari.org/RESOURCES/SOURCES/peasantsfrance.htm
- E. Hall / R. Holinshed, Chronicles (1587), Henryk VI - https://english.nsms.ox.ac.uk/Holinshed/texts_old_2009-09-17/1587/1587_5499.html

**Rabunek i spustoszenie: pogranicze i inne**
- C. McNamee, The Wars of the Bruces (1997), rozdz. 3 - https://erenow.org/ww/the-wars-of-the-bruces-scotland-england-and-ireland-1306-1328/6.php
- Chronicle of Lanercost - https://deremilitari.org/?p=2923
- C. Briggs, Economic History Review 58/4 (2005), s. 639-672 - https://ideas.repec.org/a/bla/ehsrev/v58y2005i4p639-672.html
- P. Nicholson, straty z najazdow reiverow (Calendar of Border Papers t. 1) - https://reivers.info/losses-from-reiver-raids/ ; Tarset HER A0012 - https://www.tarset.co.uk/tag-site-records/A0012_SiteRecord.pdf
- C. P. Lewis, "William the Conqueror's Harrying of the North", Cambridge World History of Genocide t. 2 (2023) - https://www.cambridge.org/core/books/abs/cambridge-world-history-of-genocide/william-the-conquerors-harrying-of-the-north-10691070/5BE39C5AD40691CBA4D57F97B6D51ECB
- Wikipedia "Harrying of the North" (Orderic Vitalis, Symeon z Durham, Muir 1997, Dalton 2002, Kapelle 1979) - https://en.wikipedia.org/wiki/Harrying_of_the_North ; Russell 1948 wg https://en.wikipedia.org/wiki/Demographics_of_England
- J. Laszlovszky i in., "Contextualizing the Mongol Invasion of Hungary in 1241-42", Hungarian Historical Review 7/3 (2018) - https://hunghist.org/83-articles/515-2018-2-laszlovszky
- D. Baronas, "Poles in Pagan Lithuania" - https://www.ldkistorija.lt/poles-in-pagan-lithuania
- Annals of Ulster (CELT) - https://celt.ucc.ie/published/T100001A/text465.html
- P. Holm, "The Slave Trade of Dublin", Peritia 5 (1986) - https://www.medievalists.net/2013/03/the-slave-trade-of-dublin-ninth-to-twelfth-centuries
- D. Kolodziejczyk, Oriente Moderno (2006), wg https://www.medievalists.net/2014/08/slaves-money-lenders-prisoner-guards-jews-trade-slaves-captives-crimean-khanate/

**Glod**
- Wikipedia "Great Famine of 1315-1317" (Freedman 2000, Leguay 2000, Ruiz) - https://en.wikipedia.org/wiki/Great_Famine_of_1315%E2%80%931317
- "10 Things to Know About the Great Famine" (Ypres 1316) - https://www.medievalists.net/tag/great-famine-of-1315-1317/
- S. Broadberry, B. Campbell, B. van Leeuwen, English medieval population - https://warwick.ac.uk/fac/soc/economics/seminars/seminars/conferences/venice3/programme/english_medieval_population.pdf (za `DEMOGRAFIA-SILA-ROBOCZA-2026-10-05.md` H3)
- Q. Outram, "The Socio-Economic Relations of Warfare and the Military Mortality Crises of the Thirty Years' War", Medical History 45 (2001), s. 151-184 - https://eprints.whiterose.ac.uk/id/eprint/385/1/outramq1.pdf
- G. Hanlon, "Wartime mortality in Italy's Thirty Years War: the duchy of Parma 1635-1637", Histoire, economie & societe 2012/4 - https://revues.armand-colin.com/histoire/histoire-economie-societe/histoire-economie-societe-42012/wartime-mortality-in-italys-thirty-years-war-the-duchy-of-parma-1635-1637
- P. W. Joyce, Edward Bruce (1315-1318) - https://libraryireland.booksulster.com/JoyceHistory/Bruce.php

**Bitwy**
- C. J. Rogers, "The Military Revolutions of the Hundred Years' War", Journal of Military History 57 (1993) - https://deremilitari.org/?p=2094
- P. Krentz, "Casualties in Hoplite Battles", GRBS 26 (1985) - https://grbs.library.duke.edu/article/viewFile/5321/5325
- P. Sabin, wywiad THE 1998 - https://timeshighereducation.com/node/17982
- R. Ambuhl, Prisoners of War in the Hundred Years War (2013) - https://www.medievalists.net/2013/01/ransoming-prisoners-of-war-became-widespread-in-the-hundred-years-war-new-book/
- M. Livingston (Crecy 2015) - https://www.medievalists.net/2015/06/who-actually-died-at-the-battle-of-crecy
- Visby: https://massakern.historiska.se/?p=583 ; Thordeman 1939 / Ingelmark; Cunha i Silva 1997 (Aljubarrota) - https://www.medievalists.net/2024/03/medieval-battle-injuries/
- Vita Edwardi Secundi (Bannockburn) - https://deremilitari.org/2014/06/the-battle-of-the-bannockburn-1314-according-to-the-vita-edwardi-secundi/
- Wikipedia z numerami stron autorow: Battle of Crecy, Poitiers, Agincourt, Golden Spurs, Cassel (1328), Bannockburn, Halidon Hill, Dupplin Moor, Neville's Cross, Visby, Grunwald, Towton, Aljubarrota, Roosebeke, Sempach, Morgarten, Mons-en-Pevele, Verneuil, Patay, Formigny, Stoke Field, Castillon, Otterburn, First St Albans, Bouvines, Lincoln (1217), Nicopolis
- T. Grabarczyk (Grunwald, 2026) - https://www.uni.lodz.pl/en/news/details/grunwald-without-legends-facts-and-myths-about-the-battle-everyone-knows

**Repo**
- `docs/DEMOGRAFIA-ANEKS-2026-10-05.md` rozdz. 1
- `docs/paczki/113-ludzie-spustoszenie.md`
- `docs/DEMOGRAFIA-SILA-ROBOCZA-2026-10-05.md` (krok 3, H3, G3)
- `docs/AUDYT-2026-10-05-EKONOMIA-WOJNA-SUROWCE-LORDOWIE.md` wojna/W8
- `docs/HISTORIA-ZBROJE-STRATY.md` 4.1
- `docs/REGULY-KRAIN-I-DLUGU-2026-10-06.md` H1-H4
- `docs/STAN-PRAC.md` (decyzje 07.10)
