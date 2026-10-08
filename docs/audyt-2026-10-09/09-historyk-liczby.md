# 09 - HISTORYK: tablica liczb historycznych dla calego audytu (Anglia, Francja, Rzesza, Italia 1250-1450) i przeliczenie na zloto gry

Audyt nocny 09.10 (z 08.10 na 09.10). Tylko odczyt: nic nie zmienione w kodzie, nic nie wgrane, gra nie uruchamiana.
Oznaczenia: **[K]** kod (plik:linia, kod w grze = Armoury 2e235ea), **[P]** pomiar z logu/CSV, **[H]** historia/lore ze zrodlem
(adres w tekscie albo w rozdz. 7), **[H?]** liczba z drugiej reki albo z pamieci - zrodla pierwotnego nie otworzylem, **[S]** szacunek/rachunek.
Wczesniejsze badania repo, na ktorych to stoi (nie powtarzam ich, tylko zbieram w jednej tablicy i dopisuje brakujace):
`docs/CENY-HISTORYCZNE.md` (ceny, Clark), `docs/AUDYT-CEN.md` (klucz 1 zl = 1 d), `docs/HISTORIA-KOSZT-NAPRAWY-2026-10-07.md`
(Tower, Derby, kursy walut), `B3-historia.md` (dochody panow, zold, wojna; scratchpad dzien-6/ekonomia-obieg), `docs/ZRODLA-DOCHODU.md`,
`docs/EKONOMIA-FUNDAMENT-2026-10-05.md` (Campbell: struktura dochodu), `docs/HISTORIA-GDZIE-ZYLI-LUDZIE-2026-10-07.md` (wsie),
`docs/AUDYT-CZAS-MAPA.md` (skala mapy, predkosci), `docs/HISTORIA-RABUNKU-I-BITEW-2026-10-07.md`, `docs/POPULACJA-WESTEROS-ESSOS.md`.

---

## 0. Dla Jeffa

1. Przelicznik juz mamy i trzymamy sie go: **1 moneta w grze = 1 angielski pens z ok. 1300 r.**, doba gry = jeden dzien, rok = 364 dni.
   Funt = 240 monet, szyling = 12, marka = 160. Wszystko w tym raporcie jest podane w tych monetach.
2. W tej skali **zywnosc, towary, zwierzeta, place rzemieslnikow i zold zolnierza sa w grze juz prawie dokladnie historyczne**
   (zboze 0.31 za kilogram, wol 157, lucznik 3 dziennie, rycerz ok. 24-26 dziennie) - tu nic nie trzeba ruszac.
3. Odstaja trzy rzeczy, ktore gracz odczuje: **(a) wojsko maszeruje 5-15 razy dalej na dobe niz prawdziwa armia** (armia szla 15-20 km
   dziennie, jazda 50-65 km; nasza druzyna w autoteście robi ponad 300 km na dobe, bo idzie 18 godzin i za szybko);
   **(b) okupy za glowy rodow i krolow sa smiesznie niskie** - za krola 100 000 monet to 417 funtow, a za Ryszarda Lwie Serce
   placono 100 000 funtow (2-3 lata dochodu calej Anglii), za Jana II Francuskiego 500 000 funtow; rycerz placil ok. roku swojego
   dochodu - i to w grze pasuje; **(c) w rocznym autoteście ludzi na swiecie przybylo 72%** (52.6 -> 90.5 mln) - w sredniowieczu
   przybywalo 0.2-0.8% na rok. To psuje wszystkie rachunki "na glowe".
4. Prawdziwy lord trzymal na stale garstke ludzi (10-25% dochodu), a duza swite tylko na wojne i za pieniadze krola. U nas rody
   wydaja na wojsko tyle, ile zarabiaja, caly rok - stad bankructwa. Skarbce krolestw oddaja dzis na zold dwa razy wiecej, niz dostaja.
5. Pomysly na pozniej, ktorych jeszcze nie mamy, a historia je daje: **trzecina krola z lupu i okupow** (krol bral 1/3 z czesci kapitana),
   **okup splacany w ratach przez lata** (Jan II, Dawid II), **myto od wozu i juku przy bramie miasta** (murage: 1 d od wozu,
   1/4-1/2 d od konia), **dzien marszu: wymarsz o swicie, oboz po poludniu** (armia szla 6-8 godzin, nie 18).
6. Na dzis w nocy proponuje tylko jedna bezpieczna rzecz: **dzienna linia w logu "Miara historyczna"**, ktora sama porowna gre
   z ta tablica (km na dobe armii, przyrost ludzi na rok, zold do dochodu wedlug rangi, okup do dochodu). Zmiany zasad - po Twoim slowie.

---

## 1. Stan dzis (kod [K] i pomiar [P])

### 1.1 Przelicznik waluty i czasu - jest w repo, obowiazuje
- **1 zl gry = 1 pens angielski (d) ok. 1300**; 12 d = 1 s; 240 d = 1 L; marka = 160 d [K/H]: `docs/AUDYT-CEN.md:16-32` (klucz z zoldu),
  `docs/CENY-HISTORYCZNE.md:4`, `docs/EKONOMIA-FUNDAMENT-2026-10-05.md:7`, `docs/ZRODLA-DOCHODU.md:4`.
- W kodzie: `HistoricalPricesEnabled = true` "1 coin = 1 medieval penny" (`Armoury/src/Settings.cs:498`); tabela wartosci za kg towarow
  `HistoricalPrices.cs:44-67` (zboze 0.31, chleb 0.55, piwo 0.3, wino 1.4, mieso 0.9, ser 1.3, maslo 2.4, sol 0.18, oliwa 4.0,
  przyprawy 27, welna 8.3, filc 20, aksamit 400; wol 157, krowa 113, swinia 30, owca 17, kura 1) - **to sa liczby z Clarka 1300-49 1:1**.
- Rok = 364 dni: `WeeksPerSeason = 13` (`Settings.cs:309`, Jeff: `Armoury.json` "WeeksPerSeason": 13). **1 L rocznie = 0.66 d na dobe.**
- Place: `WorkshopWagePerDay = 3` (`Settings.cs:445`, "a craftsman's day"), `HistMasterWageT1 = 3` + `HistMasterWagePerTier = 1.5`
  (`:511-512`, platnerz t6 ok. 10 d), `SmithDayWage = 10` (`:384`, mistrz + pomocnik), placa miasta skalowana dobrobytem 0.5-1.5
  (`TownWageRefProsperity = 4800`, `:514`).
- Zold: tier 1-6 = 2/3/6/10/15/21 d po mnozniku BK (`docs/AUDYT-CEN.md:20-28`); konny x1.5 (`MountedWageFactor`, `Settings.cs:303`:
  "t4 rider at 12 pence ... t6 knight at 26"); rekrut kosztuje 10 dni zoldu (`RecruitCostDays`, `:549`).
- Okupy lordow trzymanych przez gracza (RealisticCaptivity `Settings.cs:48-54`): lord 8 000, glowa rodu 25 000, krol 100 000;
  +25% za kazdy tier rodu; +10 000 za miasto, +5 000 za zamek (`FairRansom.cs:84-103`). **Okupy miedzy rodami AI zostaja vanilla**
  (`FairRansom.cs:63-66`, komentarz) - nie mierzone w logu.
- Dom: miejski 2 400 d (10 L), chata 480 d (2 L) (RealisticCaptivity `Settings.cs:77-78`).
- Lup: gracz bierze 33% tego, co zdarli jego ludzie ("captain's third", `PlayerLootSharePercent`, `Settings.cs:213`); **brak trzeciny
  krola** i brak trzecin u AI (lup AI dzieli DTE losowo - `docs/AUDYT-CEN.md:82`).
- Korona: powinnosci wasali 2% (pokoj) / 3% (wojna) dochodu rodu (`Settings.cs:606-608`); danina wojenna 1% kasy miasta i 1.5% kasy wsi
  na dobe wojny (`:609-612`); clo 10% licznika myta miasta (`:613-615`); zwrot 50% zoldu ze skarbca w wojnie (`:640-641`).
- Marsz: `WorldPacePercent` w kodzie 50 (`Settings.cs:313`), w `Armoury.json` Jeffa 75 (zapis 08.10 08:17); czapki kolumny
  `MarchFootPace 4.0`, `MarchTrainPace 4.2`, `MarchFootRiderPace 5.0`, `MarchRiderPace 6.5` (`Settings.cs:356-359`); sen 6 h
  (`SleepHoursNeeded`, `:689`). Skala mapy ROT ok. 4.77 km na jednostke; partia przechodzi `Speed/1.2` jednostki na godzine,
  ok. 18 h ruchu na dobe (`docs/AUDYT-CZAS-MAPA.md:16, 105-116`). Podloga vanilli `MinimumSpeed = 1.0` (tamze :26).

### 1.2 Pomiary z rocznego autotestu (log `Armoury-2026-10-08_08-18-38.log`, CSV `economy-2026-10-08_08-20-02.csv`)
| Co | Wartosc | Skad |
|---|---|---|
| Ludnosc swiata | dzien 108837: **52.6 mln**; 108896: 61.7; 108956: 73.1; 109016: 79.0; 109136: 88.3; 109199: **90.5 mln** = **+72% w 362 doby** | [P] log l. 209, 11434, 23952, 37020, 62859, 76424 |
| Renty: zaplacone / nalezne | 569 614 z 9 941 297 d na dobe (5.7%) | [P] l. 76424 |
| Renty na glowe rocznie | nalezne 40 d, zaplacone 2.3 d | [S] x364 / 90.5 mln |
| Wojsko rodow (koniec roku) | 100 548 ludzi = 0.11% ludnosci; zold naliczony 605 318 (partie) + 193 830 (garnizony) = 799 tys. d/dobe = 6.7 d na glowe (z kies zeszlo 671 938) | [P] CSV dzien 109200; log "Zold:" l. ~76690 |
| Dochod modelu rodu (bez rent), mediana | krol 636 d/dobe (zold 5 100); rod krolestwa 55 (zold 796, wojsko 163); rod mniejszy 200; rod z lennem 446 | [P] CSV 109200 |
| Dochod rodow razem (model + renty) | 236 690 + 569 614 = 806 tys. d/dobe = **3.2 d na glowe rocznie**; srednio 2 371 d na rod = ok. 3 560 L rocznie | [S] z [P] |
| Korona: wplywy / zwrot zoldu | wplywy 15 096 (wasale) + 105 986 (danina) + 8 996 (clo) = **130 tys./dobe**; zwrot zoldu **252 827/dobe** (niedoplata 83 098) | [P] l. ~76690 ("Korona:") |
| Danina wojenna na glowe | 105 986 x 364 / 90.5 mln = **0.43 d rocznie** | [S] |
| Kiesa rodu, mediana | 63 264 d (264 L); krolowie 484 650 d (2 019 L) | [P] CSV 109200 |
| Predkosc druzyny gracza (autotest, rownina) | Speed 4.55 przy "WorldPace: mapa 50%" | [P] l. 38, 203 |
| Predkosc (audyt 06.10, WorldPace 75%) | armia 800 ludzi ok. 113 km/dobe, lord 150 ludzi ok. 172, jazda ok. 251 | [P] `docs/AUDYT-CZAS-MAPA.md:136-148` |

Uwaga do pomiaru predkosci: autotest 08.10 (08:18) i nowa kampania (11:40) pisza **"WorldPace: mapa 50%"**, a `Armoury.json` Jeffa
(zapisany 08:17) ma `"WorldPacePercent": 75` [P/K]. Albo autotest wczytuje inne ustawienia, albo klucz z pliku nie trafia do gry -
do sprawdzenia przez watek marszu (to zmienia wszystkie km/dobe ponizej o czynnik 1.5).

---

## 2. Historia i lore - tablica liczb [H]

### 2.0 Waluty i kursy (do przeliczen Francja / Italia / Rzesza -> pens)
| Moneta | = d (pensow) | Kiedy | Zrodlo |
|---|---|---|---|
| funt (L) / szyling (s) / marka | 240 / 12 / 160 | stale | [H] AUDYT-CEN, CENY-HISTORYCZNE |
| noble (zloty) | 80 (6 s 8 d) | 1344-1412 | [H] Derby, wg HISTORIA-KOSZT-NAPRAWY:22 |
| ecu Jana II / frank (= 1 livre tournois) | ok. 40 (3 s 4 d) | 1360 (3 mln ecu = 500 000 L) | [H] Treaty of Bretigny (Wikipedia); ecu i salut 3 s 4 d tez w XV w. (Southampton, Agincourt) |
| livre tournois (l.t.) | ok. 60 d przy 4 l.t. = 1 L; ok. 40 d przy 6 l.t. | ok. 1300 / ok. 1360 | [S] (B3: kurs 4-6) |
| sou tournois (1/20 l.t.) | 2-3 | | [S] |
| floren florencki (= dukat) | ok. 36 (3 s) | 1340-1390 | [H] Derby; Spufford |
| soldo di piccioli (Florencja) | ok. 0.6 (60 soldi = 1 floren) | wczesne 1330 | [H] numismatics.org "pocketchange/florin" |
| lira wenecka | ok. 9.25 | 1393 | [H] Derby |
| grzywna pruska / scot | 74-80 / 3.1-3.3 | 1390-92 | [H] Derby |
| Lore: zloty smok (Westeros) | to moneta zlota - jak noble/floren: **36-80 d** [S]; srebrny jelen, miedziana gwiazda, grosz | wg Green Ronin (nie kanon) 1 smok = 210 jeleni | [H] awoiaf.westeros.org/index.php/Gold_dragon |

### 2.1 Place dzienne (cywilne)
| Kto | Anglia (d) | Inne kraje | Zrodlo |
|---|---|---|---|
| Robotnik rolny | 1.5 (1300-49), 3.0 (1350-99) | Florencja: robotnik niewykw. 4.5 soldi = ok. **2.7 d** (wczesne 1330) [S przelicz.] | [H] Clark; [H] numismatics.org |
| Pomocnik budowlany | 1.7 / 2.8 | | [H] Clark |
| Ciesla, murarz (prowincja) | 3.1 / 4.5; Statut 1351: mistrz murarz 4, ciesla 3 | Toskania: kamieniarz 16-18 soldi 1368-84 = ok. **8 d** [S] | [H] Clark; Statute of Labourers; Malanima wg HISTORIA-KOSZT-NAPRAWY:49 |
| Rzemieslnik w Londynie | lato 6, zima 5; robotnik 3-3.5 (1350) | | [H] Memorials of London 1350 |
| Platnerz, kolczuznik | 6 (Tower 1353-60), specjalista 12 (1399); platnerz krola 12 + izba | | [H] Richardson (HISTORIA-KOSZT-NAPRAWY:37-45) |
| Gornik, weglarz, wozak | [luka - brak liczby w repo i w sieci] | | - |
| Sluzacy w dworze | 6 d poza dworem (sluga hrabiego, Krolewiec 1392); giermek-sluga 0.67-1 L rocznie + utrzymanie | | [H] Derby; Jusserand |

Dni pracy w roku: ok. 200-250 [H] (DEMOGRAFIA-SILA-ROBOCZA H4; Ridolfi-Vasta: mnozyc przez realne dni, nie stale 250).
**Roczny zarobek robotnika ok. 1300: ok. 300-375 d = 1.25-1.6 L** [S].

### 2.2 Zold dzienny (wojsko)
| Kto | Anglia (d/dobe) | Francja / Italia | Zrodlo |
|---|---|---|---|
| Piechur z poboru, Walijczyk (wlocznik) | 2 | | [H] B3 rozdz. 2 |
| Lucznik pieszy | 2 (Edward I), 3 (Edward III); 1415: lucznik 6 (konny) | | [H] B3; Essex RO (1417: zbrojny 12, lucznik 6) |
| Kusznik | 3-4 | | [H] B3 |
| Hobelar, lucznik konny | 4-6 (ordynacja 1344: 4) | Francja (XIII w., Alfons z Poitiers): konny lucznik 5 sous t. = ok. 15 d [S] | [H] B3; [H] Wisconsin Hist. of Crusades, rozdz. IV |
| Zbrojny / giermek | 12 | Francja 1294-99: giermek z dobrym, opancerzonym koniem 12 s 6 d t. = ok. 37 d; inni 5 s t. = ok. 15 d [H?/S] | [H] B3; forum (wg Contamine?) - niepotwierdzone |
| Rycerz | 24 (2 s) | Francja (XIII w.): rycerz z wlasnym rynsztunkiem do 10 sous t. = ok. 30 d [S] | [H] B3; Wisconsin (Alfons z Poitiers) |
| Banneret | 48 (4 s) | | [H] B3 |
| Hrabia / ksiaze | 80 / 160 | Italia: kondotier Hawkwood - prowizja 300 florenow/mies. (Mediolan 1385) = ok. 360 d/dobe [S]; roczne zarobki 6 000-80 000 florenow [H] | [H] B3; [H] british-history.ac.uk "Milan: 1385"; Wikipedia "John Hawkwood" |
| Premia (regard) | +5.9 d na zbrojnego (100 marek / 30 / kwartal = +49%) | | [H] B3 (Felbrigg 1415) |
| Zaloga zamku w pokoju | odzwierny 2, straznik 3 + 1/4 za noc | | [H] B3 (Bristol) |
| Srednio na glowe w armii | Crecy ok. 6; swita Lancastra 11.5; Conwy 3.5; Calais 8.8 | | [H/S] B3 |

Uwaga: Rzesza XIV w. - liczby zoldu (Reisige, Fussknechte) nie znalezione w otwartych zrodlach; Swabian League 1376-89 bez stawek [luka].

### 2.3 Zywnosc, napoje, nocleg, czynsz (ceny w d)
| Rzecz | Cena | d/kg | Zrodlo |
|---|---|---|---|
| Pszenica | 8.4 d/buszel (67 d/kwarter), glod 1316 do 16 s 5 d/kwarter | 0.31 (glod do 0.9) | [H] Clark |
| Jeczmien / owies / groch | 5.4 / 3.2 / 7.1 d/buszel | 0.25 / 0.19 / 0.26 | [H] Clark |
| Chleb | bochenek farthingowy (1/4 d), waga wg Assize of Bread | ok. 0.5-0.6 | [H/S] CENY-HISTORYCZNE |
| Piwo (ale) dobre/srednie/slabe | 1.5 / 1 / 0.75 d/galon | 0.2-0.4 | [H] Dyer |
| Wino gaskonskie / renskie (Londyn 1331) | 4 / 8 d/galon; Clark 5.3 | 1.4 / 2.1 | [H] Clark; Myers |
| Mieso (wol., wiepr., bar.) | 0.3-0.5 d/lb | 0.7-1.1 | [S] z cen tusz |
| Ser / maslo / jaja | 0.6 d/lb / 1.08 d/lb / 0.5 d/tuzin | 1.3 / 2.4 | [H] Clark |
| Sol | 9.4 d/cwt (po 1350: 16) | 0.18 (0.31) | [H] Clark |
| Pieprz / imbir / szafran | 12.4 d/lb / 23 / 4.7 d/uncja | 27 / 51 / 165 | [H] Clark |
| Wyzywienie dzienne (ok. 1380) | pan 7 d, giermek 4, yeoman 3, pachol 1 | - | [H] Hodges |
| Gospoda 1331, 3 ludzi na dobe | chleb 4, piwo 2, mieso 5.5, opal 2, lozka 2 d; obrok 4 koni 10 d | lozko ok. **0.7 d na osobe za noc** | [H] Jusserand |
| Czynsz domu rocznie | chata 5 s (60 d), dom rzemieslnika 20 s (240 d), dom kupca 2-3 L | = 0.16 / 0.66 / 1.3-2 d na dobe | [H] Dyer |
| Kupno domu | ok. 10-15 lat czynszu: dom miejski ok. 10-15 L, chata ok. 2-3 L | | [S] (gra: 10 L / 2 L - zgodne) |

### 2.4 Odziez, konie, bron, zbroje (ceny w d)
| Rzecz | Cena | Zrodlo |
|---|---|---|
| Sukno tanie / dobre / najlepsze / szkarlat | 6-12 / 31 / 60 / 120-160 d za yard | [H/S] Clark, Dyer, Munro |
| Plotno lniane | 4.5 d/yard (1350-99: 8.7) | [H] Clark |
| Buty | 6 d (1313, wies); Londyn 1350: buty cordwan 3 s 6 d (42 d), trzewiki 6 d; Colne 1442: 12-16 d | [H] Dyer; Memorials of London; Colne Priory |
| Koszula lniana | 8 d (1313) | [H] Dyer |
| Szata welniana / zarzadcy | 3 s (1313) / 6 s 4 d | [H] Dyer |
| Kon juczny / roboczy | 60-160 d (5-13 s) | [H] Farmer |
| Kon pociagowy | 120-240 d | [H] Farmer |
| Kon zwyklego jezdzca (zwrot z umowy) | srednio 6 L (1-20 L) = 1 440 d; rycerza srednio 22 L | [H] Bachrach (B3) |
| Kon zbrojnego (wycena, 13 kampanii 1282-1364) | 7.6-16.4 L = 1 820-3 940 d; minimum 5 L | [H] Ayton |
| Destrier | 40-100 L (Bohun 1339: 100 L) = 9 600-24 000 d | [H] Ayton |
| Miecz | chlopski 6 d (1340s); Tower 1365-77: 2 s - 3 s 4 d (24-40 d) | [H] Dyer; Richardson |
| Kopia z grotem / grot | 2 s (24 d) / 10 d | [H] Richardson |
| Luk bialy / malowany / kompozytowy | 11-12 d / 16-18 d / 20 s | [H] Tower 1341-43 |
| Snop strzal (24) | 10-18 d (typowo 15) | [H] Tower 1341; Strickland & Hardy |
| Kusza | 8 s (96 d) | [H] Richardson |
| Kolczuga zelazna | 16 s 1 d - 46 s 8 d; najczesciej **24-26 s 8 d (288-320 d)** (Tower 1364-69, 1 542 szt.) | [H] Richardson tab. 2 |
| Kolczuga stalowa | 66 s 8 d - 80 s (800-960 d) | [H] Richardson |
| Bascinet z czepcem / bez / palet | 17-30 s (204-360 d) / 20 s / 3 s 4 d - 10 s | [H] Richardson 1369 |
| Rekawice plytowe | zwykle 2 s 3 d - 5 s 6 d; krolewskie 20-26 s 8 d | [H] Richardson |
| "Pair of plates" (brygantyna) | 13 s 4 d (plotno) / 26 s 8 d (fustian) / 40 s (aksamit) / najlepsza 4 L 13 s 8 d | [H] Richardson |
| Zestaw konnego lucznika (kolczuga + bascinet + czepiec + rekawice) | 46 s 8 d = **560 d** | [H] Richardson 1369 |
| Komplet zbroi rycerza | 16 L 6 s 8 d = **3 920 d** (1374); najdrozsza 1370s ok. 21 L | [H] Dyer; Richardson |
| Zbroja plytowa XV w. | 8 L 6 s 8 d (Cressy) = 2 000 d; mediolanska 20-40 L | [H] Dyer / [S] |
| Przeszywanica piechura | 36-72 d | [S] (Prestwich) |
| Pawez | 6 s 9 d (robota 2 s, drewno 1 s 5 d, malowanie 3 s 4 d) | [H] Richardson 1399 |

Wyposazenie w dniach zoldu [S]: piechur z poboru (przeszywanica, kapalin, wlocznia, tarcza, noz) ok. 70-130 d = **35-65 dni zoldu 2 d**;
lucznik (luk, 2 snopy, przeszywanica, kapalin) ok. 100-150 d = **35-50 dni zoldu 3 d**; zbrojny bez konia (kolczuga, bascinet, rekawice,
miecz, kopia) ok. 700-800 d = **ok. 60 dni zoldu 12 d**, z koniem 6 L ok. 2 200 d = 180 dni. (Kotwica dla paczki 171 - zaloga kupuje w miescie.)

### 2.5 Koszyk wydatkow gospodarstwa wedlug zamoznosci
| Kto | Struktura wydatkow | Zrodlo |
|---|---|---|
| Czeladnik budowlany (koszyk Phelps Brown-Hopkins, Anglia, baza 1451-75; czynsz poza koszykiem) | chleb/zboze **20%**, mieso i ryby 25%, maslo i ser 12.5%, napoje 22.5%, opal i swiatlo 7.5%, tekstylia 12.5% | [H] 20% potwierdzone (arXiv astro-ph/0411165); reszta wag [H?] z pamieci: Phelps Brown & Hopkins, *Economica* 1956 |
| Chlop - yardlander (30 akrow) | z ok. 23 kwarterow plonu: siew 26%, dziesiecina 10%, wyzywienie domu 43%, na targ ok. 20% (z tego czynsz 17-18 s) | [S] rachunek autora EKONOMIA-FUNDAMENT:209 (oznaczony tam jako niepotwierdzony); czynsz yardlandu w Romsley 1301: 3 s 4 d [H] rhhs.org.uk |
| Wielki pan - dwor Tomasza z Lancaster 1313-14 (razem 7 957 L 13 s 4.5 d) | kuchnia, spizarnia, piwnica, wino, wosk, przyprawy, ryby, konie: **5 231 L = 66%** (w tym duze konie i stajenni 486 L = 6%; wosk 314 L = 4%; korzenie 181 L); liberie, futra, siodla: **1 080 L = 14%**; rozne: **1 207 L = 15%** (honoraria panom i rycerzom 624 L = 8%, srebro 103 L, dary 93 L, stare dlugi 89 L, jalmuzna 9 L); hrabina: **439 L = 6%** | [H] sites.uwm.edu/carlin (rachunek dworu) |
| Rycerz, giermek | [luka - tylko szacunek] wikt 40-50%, konie i zbroja 15-20%, sluzba 10-15%, odziez 10% | [S] |

Wniosek [S]: im biedniej, tym wiekszy udzial zywnosci (czeladnik ok. 80%, chlop prawie caly plon poza siewem i danina);
pan wydaje na wikt dworu ok. 2/3, a na swite (liberie + honoraria + konie) ok. 1/4.

### 2.6 Obciazenia chlopa (udzial plonu)
| Danina | Udzial | Zrodlo |
|---|---|---|
| Dziesiecina koscielna | 10% plonu brutto | [H] EHS "Estimating arable output using Durham Priory tithe receipts" (ehs.org.uk) |
| Renta i robocizna villeina (biskup Worcester 1299) | **29-33% plonu netto** (4-5 dni robocizny tygodniowo + inne powinnosci); na innych dobrach lzej | [H?] brewminate.com (za Dyerem, *Lords and Peasants*), nie otworzylem ksiazki |
| Renty i uslugi villeinow (dobra swieckie 1300-49) | ok. 57% wartosci wszystkich rent i uslug dworskich | [H?] Campbell 2005 wg handoutu Munro (EngLordsPeasants.pdf - skan, nieodczytany) |
| Inne: merchet, heriot, talia, grzywny sadowe | talia ok. 1300 skodyfikowana, stala, nie na wszystkich dobrach | [H] Bailey 2019 (ueaeprints.uea.ac.uk/71006) |
| Podatek krolewski (pietnastka i dziesiecina) | od 1334 stala kwota 38-39 tys. L = ok. 2 d na glowe w roku poboru; w latach 1290-1340 rekord ok. 6.5 d | [H] B3; ZRODLA-DOCHODU:41,76 |
| Ryszard I 1193-94 (okup) | **25% wartosci ruchomosci i rent** jednorazowo | [H] Wikipedia "Saladin tithe" i opracowania (rozdz. 2.11) |
| Italia (mezzadria) / Francja (metayage) | polowa plonu dla wlasciciela (wlasciciel daje ziemie i czesc nakladow) | [H] acoup.blog 2025-09-12 (ogolnie, bez liczb archiwalnych) |
| **Razem (rachunek)** | villein: 10% + ok. 30% + podatki 2-5% + oplaty = **ok. 40-50% plonu**; wolny chlop: 10% + czynsz 5-15% + podatki = **ok. 20-30%** | [S] |

### 2.7 Oplaty miejskie i myta
| Oplata | Stawka | Zrodlo |
|---|---|---|
| Murage (na mury) - Shrewsbury 1220 | lodz 4 d, woz 1 d (z Shropshire 1/2 d), juk 1/4 d | [H] gatehouse-gazetteer.info/murage/muressay.html |
| Murage - Northampton 1301 | juk zboza 1/4 d; woz ryby morskiej 1 d, juk 1/2 d; juk sukna 1/2 d, bela sukna wozem 3 d | [H] ORB, the-orb.arlima.net (Florilegium Urbanum, ectol13/14) |
| Murage - Oksford (XIV w.) | woz zelaza 1 d, juk 1/2 d; ryba morska 1 d bez wzgledu na srodek | [H] ORB jw. |
| Liczba nadan murage | 677 w kalendarzach rol patentowych + ok. 200 innych | [H] gatehouse muressay |
| Clo krola od welny | 6 s 8 d od worka (1275); od 1342 subsydium + 40 s; szczyt wplywow 113 400 L (1353-54) | [H] B3 (Ormrod) |
| Ren (Srodkowy) | ok. 60 komor celnych w XIV w.; 79 miejsc mytnych na Renie i doplywach 800-1800; 1250: 12 miedzy Moguncja a Kolonia; 1241: zwykly statek 8 denarow, wieksze wiecej; myto w naturze (olow, miedz, wino) znacznie ciezsze | [H] medievalists.net (Gardner, Gaston, Masson 2002); medievalists.net "Castles along the Rhine" |
| Myto/renta miasta dla pana (Anglia) | farma 2-11 d na mieszczanina rocznie (1-5% dochodu miasta), myta do 1% obrotu | [H] EKONOMIA-FUNDAMENT:56 |

Rachunek [S]: woz zboza (ok. 0.5-0.75 t pszenicy, 155-230 d) placi przy bramie 1 d = **0.4-0.6% wartosci**; woz sukna (bela 3 d) ok. 0.1%;
woz zelaza (0.5 t, ok. 1 250 d) 1 d = 0.1%. Myto bylo male od ladunku, ale gestosc bramek (Ren: co kilka-kilkanascie km) robila swoje.

### 2.8 Dochody panow i ich struktura
| Kto | L/rok | d/dobe gry | Struktura | Zrodlo |
|---|---|---|---|---|
| Gentleman (prog) | 10 | 6.6 | ziemia | [H] B3 (Gray 1934) |
| Giermek (prog) | 20 | 13 | ziemia | [H] B3 |
| Rycerz (prog przymusu pasowania) | 40 (mniejsi rycerze 1436: 40-100) | 26 (26-66) | ziemia | [H] B3 |
| "Wiekszy" rycerz 1436 | ok. 200 | 130 | | [H] B3 (Gray) |
| Baron | XIII w. 100+; XIV w. 200-500 | 66 / 130-330 | | [H] B3 |
| Par Anglii, srednio (1436) | 865 | 570 | z rentami od korony | [H?] B3 |
| Hrabia Salisbury (nowy, 1337) | 1 333 | 880 | nadanie krola | [H] B3 (IPM) |
| Hrabiowie Warwick, Gloucester ok. 1310-20 | ok. 6 000 | 3 960 | | [H] ZRODLA-DOCHODU |
| Tomasz z Lancaster (5 hrabstw) 1313-14 | ok. 11 000 | 7 250 | renty, dzierzawy, sprzedaz plonow 70-90%; sady i oplaty 10-15% | [H] B3, ZRODLA-DOCHODU |
| Krol Anglii, zwykly dochod (Edward III) | do 30 000 | 19 780 | domena, cla, sady, prawa feudalne | [H] B3 (ORB) |
| ... z podatkami parlamentu | do 57 000 | 37 580 | + podatek od ruchomosci | [H] B3 |
| Krol Francji Filip IV (1298-1301) | 120 000-225 000 L (700-900 tys. l.t.) | 79-148 tys. | do 2/3 z mennicy (psucie monety) | [H] B3 (Mechoulan) |
| Kondotier Hawkwood | 6 000-80 000 florenow = 900-12 000 L | 590-7 900 | zold miast wloskich | [H] Wikipedia "John Hawkwood" |
| **Struktura dochodu panow (Anglia ok. 1300)** | czynsze 37%, folwark (sprzedaz plonow) 30%, sady-mlyny-targi 13%, dziesieciny (Kosciol) 20% | | | [H] EKONOMIA-FUNDAMENT H2 (Campbell 2005) |
| Dochod panow na glowe (Anglia ok. 1300) | wszyscy panowie z Kosciolem i Korona 30.6 d; sami swieccy 14.3 d | | | [H/S] EKONOMIA-FUNDAMENT:148, 260 |

### 2.9 Koszt wojny dziennie (umowy indenture, kampanie)
| Co | d/dobe | Zrodlo |
|---|---|---|
| Swita Lancastra pod Calais 1346-47 (1 377 jezdnych) | 15 848 (11.5 d na glowe) | [H] B3 (rola Calais) |
| Armia pod Crecy (14 000) | ok. 85 000 (ok. 6 d na glowe) | [S] B3 |
| Oblezenie Calais (szczyt 32 000) | ok. 192 000 | [S] B3 |
| Rachunek garderoby IV 1344 - XI 1347 | 337 400 L / 3.6 roku = ok. 62 000 d/dobe | [H] B3 |
| Wojna walijska 1282-83 | 98 421 L razem (piechota 24 730, jazda 17 686, zamki 23 166, robotnicy 9 414) | [H] castlewales.com |
| Marchia Wschodnia 1399 | 3 000 L/rok w pokoju, 12 000 L w wojnie (x4) | [H] B3 |
| Swita wojenna pana / jego dochod dzienny | **1.4-2.7 razy** na kazdym szczeblu; z wlasnej kiesy starczalo na 40-80 dni | [S] B3 rozdz. 5 |
| Stale wojsko pana w pokoju | **10-25% dochodu** (Lancaster ok. 20%) | [H] B3 |

### 2.10 Okupy (przyklady z liczbami)
| Kto | Okup | d | Wobec dochodu | Zrodlo |
|---|---|---|---|---|
| Ryszard I (1193-94) | 150 000 marek = 100 000 L | 24 mln | **2-3 lata dochodu korony**; 25% ruchomosci poddanych | [H] opracowania (np. Wikipedia "Saladin tithe"; erenow "The Plantagenets") |
| Jan II Francuski (Poitiers 1356; Bretigny 1360) | 4 mln ecu (1358) -> 3 mln ecu = ok. 500 000 L; zaplacono ok. 1/3 | 120 mln | **ok. 2 lata dochodu Francji** | [H] Wikipedia "Ransom of John II of France"; Pen & Sword (Hewitt); B3 |
| Dawid II Szkocki (1357) | 100 000 marek = 66 667 L w ratach przez 10 lat | 16 mln | | [H] B3 (Treaty of Berwick) |
| Karol z Blois | ok. 500 000 ecu (ok. 83 000 L) | 20 mln | | [H] B3 |
| Ksiaze Burgundii (1360) | 200 000 zlotych "moutons" | | | [H] B3 |
| Bertrand du Guesclin (Auray 1364; drugi raz Najera 1367) | 100 000 frankow (ok. 16 700 L), placil krol Karol V | 4 mln | jeden z najlepszych wodzow Francji | [H] Wikipedia "Bertrand du Guesclin"; waluta niejednolita w zrodlach |
| Arcybiskup Sens (Poitiers 1356, jeniec Warwicka) | 8 000 (waluta nieznana; jesli marki: 5 333 L) | do 1.3 mln | | [H?] (XIX-wieczna biografia Warwicka) |
| Karol Orleanski (Agincourt 1415, 25 lat niewoli) | 1440: 80 000 livres + obietnica 140 000 koron | ok. 4-9 mln [S] | | [H?] Tufts "Memorials of the Tower" |
| Giermek John Clifton (1455) | 800 marek = 533 L - wiecej niz wartosc wszystkich jego ziem | 128 000 | **wiele lat dochodu** | [H] Southampton (Ambuhl) |
| Lucznik William Callowe (Agincourt) | dostal ok. 100 L z okupu waznego jenca (= 11 lat zoldu lucznika) | 24 000 | - | [H] southampton.ac.uk 2013 |
| Krol za Agincourt: jego czesc od kapitanow | np. 33 s 4 d; 55 L 11 s 4 d za 2 jencow | | trzecina krola | [H] Southampton (tabela jencow Agincourt) |
| Chaucer (valettus, 1360) | 16 L (wykupil krol) | 3 840 | ok. 1 rok zoldu giermka | [H] B3 |
| Rycerz - regula | ok. roczny dochod (20-40 L) | 4 800-9 600 | 1 rok | [H?] AUDYT-CEN:74 |
| Najnizsze stopnie | od Agincourt stala skala okupu dla najnizszych, potem liczona od zoldu | | | [H] Ambuhl wg medievalists.net 2013 |

**Regula historyczna [S]:** okup = **ok. 1 rok dochodu** dla rycerza i pana, **2-3 lata dochodu korony** dla krola; placony w ratach,
czesto tylko w czesci (Jan II ok. 1/3, Dawid II poczatkowo 2 raty); dla biednego giermka moglo to byc wiecej niz caly majatek
(Clifton) - wtedy dlug i zastaw. Okup zbierali poddani (25% ruchomosci za Ryszarda), a krol bral swoja trzecine od kapitana.

### 2.11 Lup - "trzeciny"
- Zolnierz zatrzymuje lup, oddaje kapitanowi **1/3**; kapitan oddaje krolowi **1/3 swoich trzecin** (czyli krol ma 1/9 lupu ludzi kapitana
  + 1/3 lupu samego kapitana); u Jana z Gandawy zwykle 1/3 dla pana, czasem 1/2 [H] B3 (historicalbritainblog; Lewis).
- Jeniec "znaczny" (krol, wodz) przechodzi na korone za odszkodowaniem [H] AUDYT-CEN:72-75.
- Przy zdobyciu szturmem lup wolny; miasto poddane - nie [H] AUDYT-CEN:76.

### 2.12 Naprawa zbroi i broni wobec nowej
| Rodzaj | lekkie (% ceny / % straty wartosci) | srednie | ciezkie | Zrodlo |
|---|---|---|---|---|
| Metal (kolczuga, plyta, ostrze) | 1-4% / 2-8% | 3-12% / 5-20% (typowo ok. 10%) | 9-30% / 12-40% (typowo 20-25%) | [H/S] HISTORIA-KOSZT-NAPRAWY:198-204 (Tower 1353-99, Derby 1390-93) |
| Tkanina, skora (podszycie, pokrycie brygantyny, buty) | 1-7% / 2-16% | 5-17% / 8-28% | 30-66% / 40-90% (material 60-75%) | jw. |
| Stara kolczuga na zlom | 7-14% ceny nowej | | | [H] Richardson (Tower 1330s) |

### 2.13 Plony
| Co | Liczba | Zrodlo |
|---|---|---|
| Siew pszenicy | 2-2.5 buszla na akr (minimum wg Waltera z Henley) = ok. 135-170 kg/ha | [H] oxoniensia.org (Postles 1979); [S] przelicz. |
| Plon z ziarna (Winchester 1209-1349) | pszenica ok. 3.8-4 : 1, jeczmien ok. 3.5-4 : 1, owies ok. 2.4-2.6 : 1 | [H?] Titow, *Winchester Yields* 1972 (z pamieci; dane: bahs.org.uk/crop-yields-database) |
| Plon z ha (zboza, Anglia sredniowieczna) | ok. **500 kg/ha** brutto | [H] Our World in Data wg Wikipedia "Crop yield" |
| Netto po siewie i dziesiecinie | ok. 300-350 kg/ha = ok. 95-110 d/ha rocznie przy 0.31 d/kg | [S] |
| Niderlandy XIV w. | do 1:14 (przy intensywnym rolnictwie) | [H] Wikipedia "Crop yield" (Bornewasser) |
| Spozycie | ok. 200-250 kg zboza na osobe rocznie (z piwem) = ok. 60-80 d | [S] |

### 2.14 Ludnosc wsi i miast
| Co | Liczba | Zrodlo |
|---|---|---|
| Anglia ok. 1300 | 4-6 mln; w miastach 15-20% | [H] POPULACJA-WESTEROS-ESSOS:155; HISTORIA-GDZIE-ZYLI:26 |
| Wies | zwykle 30-60 domow, 150-300 ludzi; vill (z przysiolkami) ok. 270; odstep 1.5-3 km | [H/S] HISTORIA-GDZIE-ZYLI:122-135 |
| Miasteczko targowe (Anglia 1377) | ok. 540 miast, wiekszosc 500-2 000 ludzi; 1 na 7-8 tys. ludzi; targ co 8-15 km | [H] HISTORIA-GDZIE-ZYLI:116 (Tiller) |
| Londyn / Paryz ok. 1300 | 70-100 tys. / 200-250 tys. | [H] POPULACJA-WESTEROS-ESSOS:157-158 |
| Florencja 1338 | ok. 90 000 (1 500 obcych, podroznych, zolnierzy) | [H] HISTORIA-GDZIE-ZYLI:110 (Villani) |
| Wlochy | 15% w miastach od 10 tys.; Flandria 35-40% | [H] HISTORIA-GDZIE-ZYLI:27 |
| Przyrost naturalny | +0.2..+0.8% rocznie we wzroscie; glod 1315-22 -12% w 10 lat; zaraza 1348 -46% w 3 lata | [H] DEMOGRAFIA-SILA-ROBOCZA:25, 242-268 |
| Wojsko pod bronia | pokoj 0.03-0.1%, wojna 0.2-0.7% ludnosci | [H/S] B3; POPULACJA-WESTEROS-ESSOS:160-161 |

### 2.15 Tempo marszu i dzien marszu
| Co | km/dobe | Zrodlo |
|---|---|---|
| Czarny Ksiaze, wielka chevauchee 1355 (Bordeaux - Narbona - Bordeaux) | 975 km w 59 dni = **16.5 km/dobe** (z odpoczynkami i rabunkiem) | [H] Madden, *The Black Prince and the Grande Chevauchee* (Cambridge, zakonczenie); recenzja deremilitari.org 2019 |
| Kolumna z taborem 1355 | ok. 15 km/dobe; oddzialy rabunkowe do 25 km od trasy | [H?] Nicolle wg forum myarmoury.com |
| Crecy 1346: La Hougue -> Caen | ok. 160 km, wymarsz 18 VII, szturm Caen 26 VII = **ok. 20 km/dobe** | [H] Cambridge (Ayton/Prestwich, rozdz. "Crecy campaign"), Hoskins/Barber; [S] rachunek |
| Duza armia piesza | 8-12 mil = **13-19 km/dobe**; 20 mil (32 km) dziennie przez dluzszy czas "nadzwyczajne"; 30 mil (48 km) przez 7 dni "niespotykane" | [H] acoup.blog 2019-10-06; encyclopedia.com "Travel" (Harold 1066) |
| Woz z wolami | ok. 2 mile/h = do 26 km/dobe | [H?] lorehaven.com (blog) |
| Jazda na dluzszym dystansie | 50-65 km/dobe; goniec ze zmiana koni 100-200 | [H/S] AUDYT-CZAS-MAPA:147 |
| Dwor krola Roberta (lore, ok. 1 600 mil KP - Winterfell) | "ok. miesiac" w serialu; realnie 35-40 dni ciezkiej jazdy = 65-75 km/dobe | [H] acoup.blog 2019-10-06 (krytyka lore) |
| Dzien marszu | pobudka ok. 5:00, zwijanie obozu, marsz nominalnie 8 h, realnie 5-7 h (czekanie na ogon kolumny 1-3 h); oboz rozbijany po poludniu; sen ok. 21:00 | [H] acoup.blog 2019-10-06 |
| Dni odpoczynku | bez stalego rytmu; przyklad marszu z Akki: 11, 12, 13, 3, 7 mil z dniami postoju miedzy | [H?] medieval.substack.com (Traveling in the Middle Ages 7) |

**Wniosek [S]:** prawdziwa armia byla w ruchu **6-8 h na dobe**, a w obozie 16-18 h; szla **15-20 km/dobe** (z taborem), jazda 50-65.

### 2.16 Lore Gry o Tron - liczby, ktore warto znac (i jak je czytac)
| Co | Liczba | Zrodlo |
|---|---|---|
| Dlug Zelaznego Tronu (poczatek AGOT) | ponad 6 mln smokow (3 mln u Lannisterow, reszta m.in. Zelazny Bank); pozniej "dziesiatki milionow" | [H] awoiaf (Gold dragon / Money) - Martin sam przyznal niespojnosc liczb |
| Turniej Reki 298 AC | 40 000 smokow za kopie, 20 000 drugi, 20 000 bitwa, 10 000 lucznictwo - "wyjatkowo wysokie" | [H] awoiaf "Hand's tourney" |
| Przelicznik lore -> gra [S] | jesli smok = zlota moneta 36-80 d, dlug 6 mln smokow = 0.9-2 mln L = 30-67 lat zwyklego dochodu Anglii (Edward III mial w 1339 dlug 10 lat dochodu); turniej 40 000 smokow = 6 000-13 000 L | [S] |

W grze zloto nazywa sie po lore, ale liczy w pensach - **kwot z ksiazek nie wolno wpisywac 1:1** (dlug 6 mln smokow w grze to 25 000 L,
czyli rok dochodu krola; turniej 40 000 = 167 L). Do kanonu pasuje relacja: dlug korony >> roczny dochod, nagroda turnieju krolewskiego
= majatek hrabiego.

---

## 3. Tabela "historia -> gra" (1 zl = 1 d, doba = dzien, rok = 364 dni)

| Rzecz | Historia (d) | Gra dzis | Ocena |
|---|---|---|---|
| Robotnik / rzemieslnik / platnerz (dzien) | 1.5-3 / 3-6 / 6-12 | warsztat 3, mistrz 3 + 1.5 na tier, kowal z pomocnikiem 10; x0.5-1.5 wg dobrobytu [K] | zgodne |
| Zold: piechur / lucznik / konny lucznik / zbrojny / rycerz / banneret | 2 / 3 / 4-6 / 12 / 24 / 48 | t1-t6: 2/3/6/10/15/21; konny x1.5 (t4 12, t6 26) [K] | zgodne |
| Zold sredni na glowe | 6 (Crecy) - 11.5 (jazda) | 6.7 [P] | zgodne |
| Zaliczka rekruta | ok. pol kwartalu z gory kapitanowi | 10 dni zoldu [K] | zgodne co do rzedu |
| Zboze / chleb / piwo / wino / mieso (d/kg) | 0.31 / 0.5-0.6 / 0.2-0.4 / 1.4 / 0.7-1.1 | 0.31 / 0.55 / 0.3 / 1.4 / 0.9 [K] | 1:1 |
| Wol / krowa / owca / swinia | 157 / 113 / 17 / 24-36 | 157 / 113 / 17 / 30 [K] | 1:1 |
| Kon juczny / rouncey / destrier | 60-160 / 160-1 440 / 9 600-24 000 | 99 / ok. 1 000 / 10-11 tys. (AUDYT-CEN 04.10) | zgodne |
| Kolczuga / bascinet / miecz dobry | 288-320 / 204-360 / 24-40 (Tower), dobry do 104 | (131) helm 314, miecz 104, brygantyna na kolczudze 2 316 [P] | zgodne |
| Komplet rycerza | 3 920 | ok. 2 500-4 000 model ArmsPricing | zgodne |
| Naprawa metalu (robota) | ok. 10% utraconej wartosci | 25% (lawa, AI), 50% (bohater u kowala), 10% (racki wojska) [K] `Settings.cs:137` | za drogo 2.5-5x (znane, HISTORIA-KOSZT-NAPRAWY:228-237) |
| Dom miejski / chata | 10-15 L / 2-3 L | 2 400 / 480 d [K] | zgodne |
| Nocleg w gospodzie | ok. 0.7 d/os. + wikt 3-4 d | [luka - nie sprawdzone] | - |
| Myto bramy (woz / juk) | 1 d / 1/4-1/2 d | myto miasta liczone od handlu (licznik gry) | brak myta od ladunku |
| Renta na glowe (panowie razem) | 30.6 d (swieccy 14.3) | nalezne 40 d, zaplacone 2.3 d; z modelem 3.2 d [P/S] | zaplacone 5-10x za malo |
| Podatek wojenny na glowe | ok. 2 d w roku poboru (do 6.5) | 0.43 d [P/S] | 5x za malo |
| Dochod korony na glowe | zwykly 1.6 d (30 000 L / ok. 4.5 mln); z podatkiem ok. 3 d | 0.52 d (130 tys./dobe) [P/S] | 3-6x za malo; a zwrot zoldu 252 tys. > wplywy 130 tys. |
| Stale wojsko pana w pokoju | 10-25% dochodu | ok. 100% dochodu caly rok (799 tys. zoldu / 806 tys. dochodu) [P/S] | **4-10x za duzo** (znane, B3) |
| Rycerz (40 L) / baron (200-500 L) / hrabia (1 333-11 000 L) / krol (30 000 L) - d/dobe | 26 / 130-330 / 880-7 250 / 19 780 | mediana rodu krolestwa 55 (bez rent), z lennem 446, krol 636 (bez rent); srednio z rentami 2 371 [P] | rod gry = od rycerza do wielkiego hrabiego; krol 10-30x biedniejszy niz korona |
| Okup: lord / glowa rodu / krol | rycerz ok. 1 rok (4 800-9 600); baron-hrabia ok. 1 rok (48 tys.-2.6 mln); krol 2-3 lata (24-120 mln) | 8 000 / 25 000 x (1 + 0.25 tier) + fiefy / 100 000 [K] | lord zgodny; glowa rodu 2-40x za malo; krol 240-1 200x za malo (wobec gry: 1-2 mies. dochodu rodu) |
| Lup: kapitan / krol | 1/3 / 1/3 z 1/3 | gracz 33%, krol 0, AI losowo (DTE) [K] | brak trzeciny krola i trzecin AI |
| Ludnosc: przyrost roczny | +0.2..+0.8% | **+72%** w roku testu [P] | blad (rozdz. 4, L1) |
| Wojsko / ludnosc | pokoj 0.03-0.1%, wojna 0.2-0.7% | 0.11% (90.5 mln) / ok. 0.19% (52.6 mln) [P] | zgodne |
| Marsz armii (km/dobe) | 15-20 | 113 (armia 800, 75%), druzyna gracza ok. 325 przy 18 h ruchu (S 4.55) [P/S] | **5-15x za szybko** |
| Godziny marszu na dobe | 6-8 | ok. 18 (sen 6 h) [K] | 2-3x za dlugo |
| Plon (d z ha rocznie, netto) | ok. 95-110 | produkcja wsi to stala z tabeli typu wsi (EKONOMIA-FUNDAMENT:118) | nie przeliczalne wprost |

**Przelicznik predkosci [S]** (dla watku marszu): km/dobe = Speed / 1.2 x godziny_ruchu x 4.77 = **3.975 x Speed x h**.
- przy 18 h ruchu: km = 71.6 x Speed -> armia 16.5 km wymaga Speed **0.23**; jazda 55 km - Speed **0.77**;
- przy 8 h ruchu: km = 31.8 x Speed -> armia 16.5 km = Speed **0.52**; jazda 55 km = Speed **1.73**; piechur 25 km = Speed 0.79.
- podloga vanilli `MinimumSpeed = 1.0` daje co najmniej **72 km/dobe przy 18 h** i 32 km przy 8 h - kazde podejscie do historycznych
  km musi te podloge obnizyc albo skrocic godziny ruchu.
- Mapa jest scisnieta (wies srednio ok. 150 km od miasta, AUDYT-CZAS-MAPA:19, 99): historycznie 16.5 km/dobe oznacza 9 dni do wlasnej
  wsi - to decyzja rozgrywki dla Jeffa (pkt pytan), nie liczba do wpisania w ciemno.

---

## 4. Luki i bledy logiki

**L1. Ludnosc swiata rosnie o 72% rocznie (historia: 0.2-0.8%).** [P] log roczny: 52.6 mln (dzien 108837, l. 209) -> 90.5 mln
(dzien 109199, l. 76424); przyrost najszybszy na poczatku (+9.1 mln w 59 dob), potem wolniej (+2.1 mln w ostatnich 60). Renty nalezne rosna
razem z nia (5.78 -> 9.94 mln/dobe). Skutek: wszystkie miary "na glowe" (renta, podatek, wojsko) spadaja same, a "nalezne" renty
odrywaja sie od kas osad. Prawdopodobna przyczyna (wg STAN-PRAC:247-248): hearth dopisywany wsiom przez inwestycje BEE (65-135 dziennie,
"z niczego") i stala gry; paczka 109 (przyrost) wymaga zamkniecia ujsc BEE. **Koliduje/zalezy od paczki 170 (domkniecie BEE).**

**L2. Okupy glow rodow i krolow nie maja zwiazku z dochodem.** [K] RealisticCaptivity `Settings.cs:49-54`, `FairRansom.cs:84-103`:
stale kwoty 25 000 / 100 000 + tier + fiefy. Historia [H]: okup ok. roku dochodu (rycerz), 2-3 lata dochodu korony (Ryszard I, Jan II).
Przy srednim rodzie gry 2 371 d/dobe (= 863 tys. rocznie) okup glowy rodu tier 3 z 1 miastem i 2 zamkami (63 750) to **27 dni dochodu**;
krol (100 000 + tier) to kilka tygodni dochodu krola. Dodatkowo okupy AI-AI zostaja vanilla (`FairRansom.cs:63-66`) i nikt ich nie mierzy -
nie wiadomo, czy nie przychodza "z niczego" (handlarz okupow AUDYT 04.10 B4 - poprawione tylko dla gracza, `FairRansom.cs:105-110`).

**L3. Wojsko pokonuje 5-15 razy wieksze dystanse niz historyczne.** [P] Speed druzyny 4.55 (log l. 203) x 71.6 = ok. 325 km/dobe;
armia 800 ok. 113 km/dobe przy 75% (AUDYT-CZAS-MAPA:145). Historia [H]: 15-20 km armia (1355: 16.5; Crecy ok. 20), jazda 50-65.
Dwa zrodla bledu: (a) 18 h ruchu na dobe zamiast 6-8 (sen 6 h, `SleepHoursNeeded`, `Settings.cs:689`); (b) baza predkosci i podloga 1.0.
Do tego niezgodnosc ustawien: log pisze "WorldPace: mapa 50%", plik Jeffa ma 75 (rozdz. 1.2).

**L4. Swita wojenna trzymana caly rok, a skarbiec korony placi wiecej, niz dostaje.** [P] zold 799 tys./dobe wobec dochodu rodow
806 tys. (ok. 100%) - historycznie 10-25% w pokoju, 1.4-2.7x dochodu tylko na kampanie 40-80 dni (B3). Skarbce: wplywy 130 tys./dobe,
zwrot zoldu 252.8 tys./dobe (`CrownWageRefundPercent 50`, `Settings.cs:641`) - **deficyt 122 tys./dobe**; danina 0.43 d na glowe wobec
ok. 2 d historycznie. To potwierdza B3 i jest juz w projekcie obiegu (paczki 162-168) - tu tylko liczby kontrolne.

**L5. Trzeciny: brak czesci krola i brak trzecin u AI.** [K] `PlayerLootSharePercent = 33` (`Settings.cs:213`) obejmuje tylko gracza
jako kapitana; lup AI dzieli DTE losowo (AUDYT-CEN:82); okupy nie odprowadzaja nic do skarbca. Historia [H]: krol bral 1/3 z czesci
kapitana (1/9 lupu ludzi + 1/3 lupu kapitana) i swoja czesc z okupow (Agincourt: 33 s 4 d, 55 L 11 s 4 d). W grze skarbiec traci wiec
naturalne zrodlo wojenne, ktore historycznie lagodzilo koszt wojny.

**L6. Sol jako towar luksusowy w BetterEconomy.** [K] `Modules/BetterEconomy/ModuleData/better_economy_class_consumption_profiles.xml`:
`<Category id="salt" group="Luxury" />` i `<Pattern contains="salt" group="Luxury" />`. Historia [H]: sol to podstawowa potrzeba
(0.18 d/kg, Clark; podstawa gabeli francuskiej 1355 na armie 30 000 - B3), kupowana przez kazde gospodarstwo do solenia miesa i ryb.
Klasa biedna w BEE nie kupuje jej jak potrzeby. Piwo nie ma grupy (Neutral), choc w koszyku czeladnika napoje to 22.5%.
**Do paczki 170 (BEE)**, nie osobno.

**L7. Naprawa metalu 2.5-5 razy drozsza od historii.** [K] `RepairCostFactor = 0.5` (`Settings.cs:137`; 25% przy lawie i AI).
Historia [H/S]: ok. 10% utraconej wartosci. Znane z HISTORIA-KOSZT-NAPRAWY:228-237; tu tylko jako wiersz tablicy (nie dubluje).

**L8. Brak myta od ladunku przy bramie.** [K] clo korony liczone od licznika myta miasta (`CrownCustomsShare 0.1`, `Settings.cs:614`);
myta za przejazd woza/juka nie ma. Historia [H]: murage 1 d od wozu, 1/4-1/2 d od juka (Shrewsbury 1220, Northampton 1301, Oksford),
ok. 0.1-0.6% wartosci ladunku, pieniadz szedl na mury miasta. Male, ale to brakujace ogniwo "kasa miasta <- ruch towarow" (zamknieta petla:
woz placi kasie miasta, kasa placi mury/budowy).

**L9. Kwoty z lore nieprzeliczone.** [S] Gra liczy w pensach, a kwoty lore sa w zlotych smokach (36-80 d): dlug korony 6 mln smokow,
nagrody turniejowe 40 000. Gra nie powinna ich przenosic 1:1 (np. ROT/ GrandTourney - `HistTournamentScale = 4`, `Settings.cs:516`
juz dzieli nagrody 1600-5000 -> 400-1250 d, co odpowiada dobremu mieczowi/kawalkowi zbroi - zgodne z historia, ale ponizej "lore'owego"
blasku turnieju krolewskiego). Do decyzji kanonu (pytanie do Jeffa).

---

## 5. Propozycje

**P-1. Linia logu "Miara historyczna" (raz na dobe).** Co: jedna linia porownujaca gre z ta tablica: (a) przyrost ludnosci w % rocznie
(z ostatnich 7 dob, annualizowany); (b) km/dobe: mediana partii lordow > 300 ludzi i partii czysto konnych (z przesuniecia pozycji
miedzy dobami x 4.77 km); (c) zold / dochod (model + renty) wedlug rangi: krol, rod z lennem, rod bez lenna; (d) wplywy i wydatki skarbcow
na glowe; (e) renta zaplacona na glowe; (f) liczba i suma okupow AI-AI i gracza z dnia oraz okup / roczny dochod rodu jenca.
Liczby: progi "historyczne" z rozdz. 3 (przyrost 0.2-0.8%, armia 15-20 km, jazda 50-65, zold pokojowy 10-25%, renta 14-31 d/glowe) -
zeby kazdy test sam mowil "w normie / x razy". Priorytet **P0** (pomiar przed zmianami). Wielkosc: mala-srednia. Rozgrywka: nie.
Nowa kampania: nie. Ryzyko: male (tylko odczyt; okupy AI wymagaja postfiksu na akcji okupu - jesli zbyt grube, punkt (f) na pozniej).
Zaleznosci: dopisac do formatu paczki 169 (log obiegu) albo osobno; nie zmienia 162-168.

**P-2. Okup = czesc rocznego dochodu rodu jenca, placony z kiesy rodu (z rata/dlugiem).** Co: lord (nie glowa) = max(8 000, 0.25 x roczny
dochod rodu); glowa rodu = max(dzisiejszy wzor, **0.5-1 x roczny dochod rodu** (model + renty z 30 dob)); krol = max(100 000,
**1 x roczny dochod rodu krola + 0.5 x roczne wplywy skarbca**). Uzasadnienie: rycerz placil ok. roku dochodu, krol 2-3 lata - bierzemy
dolna polowe, bo gra nie ma rat przez 10 lat i nie moze bankrutowac masowo (zasada Jeffa). Splata: jesli rod nie ma, reszta jako dlug
(istniejace `RansomDebtEnabled` gracza; dla AI - Zelazny Bank albo raty 10% dziennie z dochodu). **Trzecina krola**: 1/3 okupu z czesci
zwyciezcy do skarbca jego krolestwa. Priorytet **P1**. Wielkosc: srednia. Rozgrywka: **TAK** (okup za lorda wzrosnie kilka-kilkanascie razy
dla gracza) - pytanie do Jeffa. Nowa kampania: nie. Ryzyko: srednie (bankructwo rodu jenca, gracz bogaci sie na okupach; dlatego pulap
"najwyzej kiesa + 60 dni dochodu"). Zaleznosci: P-1 (pomiar okupow AI), 162-168 (budzet rodow), Iron Bank.

**P-3. Trzeciny w lupie AI i gracza.** Co: z lupu bitwy (sprzedaz lupu przez AI i gracza) 1/3 czesci kapitana do skarbca krolestwa
= 1/9 calego lupu; z okupow jak w P-2. Uzasadnienie: indentury Edwarda III i Jana z Gandawy. Priorytet **P2**. Wielkosc: srednia
(trzeba znalezc miejsce, gdzie AI zamienia lup na zloto - DTE/Spoils). Rozgrywka: tak, lekko (gracz oddaje 1/9 lupu, gdy jest wasalem).
Nowa kampania: nie. Ryzyko: male. Zaleznosci: paczki skarbca (162-168).

**P-4. Dzien marszu: oboz od poludnia/popoludnia, ruch 6-8 h; potem predkosc do 15-20 km armii.** Co (liczby dla watku marszu, nie
osobna paczka): godziny ruchu armii z piechota i taborem **8 h** (np. 6:00-14:00), jazda bez taboru **9-10 h**; Speed przy 8 h: armia
z taborem **0.5** (16 km), piechota bez taboru **0.75** (24 km), jazda **1.6-1.8** (50-57 km); podloga `MinimumSpeed` obnizona do **0.4**.
Gdyby Jeff zostal przy obozie 24-6 (18 h ruchu), te same km wymagaja Speed 0.23 / 0.33 / 0.7-0.8. Uzasadnienie: 1355 - 16.5 km/dobe,
Crecy ok. 20, duza piechota 13-19 km (acoup), jazda 50-65; dzien marszu 5-7 h realnego ruchu. Priorytet **P1** (temat Jeffa z dzis).
Wielkosc: srednia. Rozgrywka: **TAK, bardzo** (wyprawa przez Westeros 2-5 miesiecy; wies 9 dni od miasta) - wymaga decyzji Jeffa
o skali (mapa jest scisnieta). Nowa kampania: nie. Ryzyko: duze dla AI (spojnosc armii, zapasy, oblezenia - AUDYT-CZAS-MAPA:34-37).
Zaleznosci: watek marszu/obozow tego audytu; najpierw wyjasnic 50% vs 75% w ustawieniach.

**P-5. Myto bramy od ladunku (murage).** Co: woz wjezdzajacy do miasta placi 1 d, juk/kon 1/4 d, karawana od kazdego zwierzecia
jucznego 1/4 d i od wozu 1 d - do kasy miasta (nie z niczego: z kiesy wozacego). Uzasadnienie: stawki murage 1220-1350, 0.1-0.6% wartosci.
Priorytet **P2**. Wielkosc: mala-srednia. Rozgrywka: minimalnie (gracz placi grosze). Nowa kampania: nie. Ryzyko: male (wozy wsi -
VillageCarts - maja male sakiewki; najpierw pomiar). Zaleznosci: 112 (utarg wsi), MarketCarts.

**P-6. Sol do potrzeb podstawowych w BEE (i piwo do potrzeb).** Co: `salt` i `beer` w grupie Staple. Uzasadnienie: sol 0.18 d/kg,
podstawa konserwacji; napoje 22.5% koszyka czeladnika. Priorytet P2. Wielkosc: mala. Rozgrywka: nie. Nowa kampania: nie.
Ryzyko: male (popyt na sol wzrosnie - sprawdzic podaz soli). Zaleznosci: **paczka 170** - przekazac tam, nie robic osobno.

**P-7. Przeliczanie lore.** Co: zasada w CLAUDE.md/STAN-PRAC: kwoty lore (smoki) dzielimy przez ok. 36-80 przed wpisaniem do gry
(1 smok ~ 1 floren/noble). Priorytet P2. Wielkosc: mala (dokument). Rozgrywka: nie. Ryzyko: brak.

**P-8. Ludnosc: zatrzymac przyrost z niczego (L1).** Co: przyrost wsi tylko z modelu 109 (-1.3..+0.8% rocznie), hearth z inwestycji BEE
bez ludzi z niczego. Priorytet **P0**. Wielkosc: srednia. Rozgrywka: posrednio (mniej rekrutow i rent po roku). Nowa kampania: zalecana
(nadwyzka 38 mln ludzi w zapisach). Ryzyko: srednie. Zaleznosci: **170 (BEE) i 109** - to nie jest nowa paczka, tylko twardy argument
z pomiaru rocznego dla nich.

---

## 6. Do zrobienia tej nocy vs na pozniej

**Tej nocy (male, bezpieczne, sprawdzalne autotestem 40 dob):**
1. **Linia "Miara historyczna" (P-1, punkty a-e bez okupow AI)** - tylko log. Miejsce: obok istniejacych linii dziennych:
   `Armoury/src/PopulationLaw.cs:327` (linia "Ludnosc: dzien" - tu juz jest suma ludnosci: dopisac zmiane % z 7 dob i annualizacje),
   `KingdomTreasury.cs:74, 202, 283` (linie "Korona:" - dopisac sume wplywow i wydatkow skarbcow na glowe), `TerrainEase.cs:143`
   ("Audyt predkosci" - dzis tylko partia gracza; dopisac mediane km/dobe partii lordow z przesuniecia pozycji x 4.77).
   Test 40 dob: linia pojawia sie raz na dobe; przyrost ludnosci z pierwszych 40 dob powinien wyjsc ok. +60-120% rocznie (blad L1 widoczny),
   km/dobe armii ok. 100+; zero wyjatkow w `Log.Error`.
2. **Sprawdzenie (bez zmian kodu), czemu log pisze WorldPace 50% przy pliku 75%** - porownac odczyt MCM (`McmSettings.cs:1033, 2991`)
   z kluczem w `Armoury.json:234` i z tym, co ustawia autotest. Wynik do watku marszu.

**Na pozniej (po slowie Jeffa albo w paczkach projektu):**
- P-2 okupy wg dochodu + trzecina krola z okupu (pytanie do Jeffa - zmienia rozgrywke).
- P-4 dzien marszu i predkosci (decyzja Jeffa o skali; watek marszu).
- P-8 ludnosc (paczki 109/170), P-6 sol i piwo (paczka 170).
- P-3 trzeciny lupu AI, P-5 myto bramy (po 162-168).
- P-7 zasada przeliczania lore (dokument).
- Luki zrodlowe do uzupelnienia przy okazji: zold Rzeszy XIV w., place gornikow i wozakow, koszyk rycerza, wagi koszyka PBH
  (poza 20%), plony Titowa (tabele), francuskie gages po 1350 (Contamine, Henneman).

Kolizje z paczkami w toku: **169** (log obiegu) - P-1 mozna dopisac do jej linii albo obok, bez sprzecznosci; **170** (BEE) - L1/P-8
i L6/P-6 naleza do niej; **171** (zbrojenie zalog) - tablica 2.4 daje ceny kotwiczne: komplet piechura 70-130 d, lucznika 100-150 d,
zbrojnego bez konia 700-800 d (35-65 dni zoldu) - jesli 171 liczy zakup zalogi wyraznie drozej, warto sprawdzic.

---

## 7. Zrodla (adresy)

Waluty, place, ceny:
- Repo: `docs/CENY-HISTORYCZNE.md` (Clark, gpih.ucdavis.edu; Dyer wg faculty.goucher.edu/eng211/c14_price_list.htm; luminarium.org/medlit/medprice.htm),
  `docs/HISTORIA-KOSZT-NAPRAWY-2026-10-07.md` (Richardson, Tower; Derby; Memorials of London 1350; Colne Priory), `docs/AUDYT-CEN.md`.
- Florencja, robotnik 4.5 soldi, floren 60 soldi: https://numismatics.org/pocketchange/florin
- Ridolfi, Vasta, place budowlane Florencji od 1326: https://www.deps.unisi.it/it/node/1953
- Caferro, *Petrarch's War* (recenzja, place 1349-50): https://www.cambridge.org/core/journals/renaissance-quarterly/article/petrarchs-war-florence-and-the-black-death-in-context-william-caferro-cambridge-cambridge-university-press-2018-xii-228-pp-9999/4974503437FD50F40EE52EE3E28CE1CD
- Alfons z Poitiers, rycerz 10 sous, konny lucznik 5 sous (Hist. of the Crusades, rozdz. IV): https://search.library.wisc.edu/digital/AXM6SCNSNSQBQB85/text/A2KLU7T7FBMOIF8T
- Hawkwood (Mediolan 1385, 300 florenow/mies.): https://prod.british-history.ac.uk/node/78603 ; https://en.wikipedia.org/wiki/John_Hawkwood
- Umowy Henryka V (rycerz 2 s, zbrojny 12 d, lucznik 6 d): https://www.essexrecordofficeblog.co.uk/?p=3823 ; https://historicalbritainblog.com/indentures-and-the-kings-army/
- Koszyk Phelps Brown-Hopkins (20% zboza potwierdzone): https://arxiv.org/pdf/astro-ph/0411165 ; wykresy Munro: https://www.economics.utoronto.ca/munro5/Postangraphs-2.doc

Dwor, dochody, wojna:
- Rachunek dworu Tomasza z Lancaster 1313-14: https://sites.uwm.edu/carlin/household-expenses-of-thomas-earl-of-lancaster-30-sept-1313-29-sept-1314-7-8-edw-ii/
- B3-historia.md (scratchpad dzien-6/ekonomia-obieg) - pelna lista adresow: ORB Muhlberger, Ormrod, Gray 1934, rola Calais, Bristol, Conwy, Harlech, castlewales.com.
- `docs/EKONOMIA-FUNDAMENT-2026-10-05.md` (Campbell 2005, struktura dochodu panow), `docs/ZRODLA-DOCHODU.md`.

Obciazenia chlopa, myta:
- Worcester 1299 (29-33% netto), za Dyerem: https://brewminate.com/changes-in-the-medieval-english-countryside-after-william-the-conqueror/
- Bailey 2019, talia: https://ueaeprints.uea.ac.uk/id/eprint/71006/
- Dziesiecina jako miara plonu: https://ehs.org.uk/article/estimating-arable-output-using-durham-priory-tithe-receipts-1341-1450/
- Czynsz yardlandu Romsley 1301: https://rhhs.org.uk/local-history/5-halesowen-abbey.html
- Murage: https://gatehouse-gazetteer.info/murage/muressay.html ; https://the-orb.arlima.net/encyclop/culture/towns/florilegium/economy/ectol13.html ; https://the-orb.arlima.net/encyclop/culture/towns/florilegium/economy/ectol14.html
- Ren: https://www.medievalists.net/2012/12/tolling-the-rhine-in-1254-complementary-monopoly-revisited/ ; https://www.medievalists.net/2010/04/castles-along-the-rhine-the-upper-middle-rhine-valley/

Okupy:
- Jan II: https://en.wikipedia.org/wiki/Ransom_of_John_II_of_France ; https://pen-and-sword.co.uk/The-Black-Princes-Expedition-ePub/p/7289
- Du Guesclin: https://en.wikipedia.org/wiki/Bertrand_du_Guesclin ; https://en.wikisource.org/wiki/1911_Encyclop%C3%A6dia_Britannica/Du_Guesclin,_Bertrand
- Ryszard I i podatek 25%: https://en.wikipedia.org/wiki/Saladin_tithe ; https://erenow.org/postclassical/theplantagenetsthekingswhomadeengland/21.php
- Agincourt, Ambuhl, Callowe, Clifton: https://www.southampton.ac.uk/news/2013/01/soliders-in-the-late-middle-ages.page ; https://www.medievalists.net/2013/01/ransoming-prisoners-of-war-became-widespread-in-the-hundred-years-war-new-book/ ; https://eprints.soton.ac.uk/400374/6/Agincourt_Prisoners_table.htm
- Karol Orleanski: https://en.wikipedia.org/wiki/Charles_I,_Duke_of_Orl%C3%A9ans ; https://dl.tufts.edu/teiviewer/parent/ms35tk73c/chapter/c5

Plony, ludnosc, marsz:
- Plony: https://en.wikipedia.org/wiki/Crop_yield ; https://www.bahs.org.uk/crop-yields-database/ ; https://www.oxoniensia.org/volumes/1979/postles.pdf
- Ludnosc i wsie: `docs/HISTORIA-GDZIE-ZYLI-LUDZIE-2026-10-07.md`, `docs/POPULACJA-WESTEROS-ESSOS.md`, `docs/DEMOGRAFIA-SILA-ROBOCZA-2026-10-05.md`.
- Marsz: https://acoup.blog/2019/10/06/new-acquisitions-how-fast-do-armies-move/ ; https://deremilitari.org/2019/05/mollie-m-madden-the-black-prince-and-the-grande-chevauchee-of-1355-matt-raven/ ;
  https://www.cambridge.org/core/books/black-prince-and-the-grande-chevauchee-of-1355/conclusion/A6D55AD80CA1D218F4EF13F50C8C2953 ;
  https://www.cambridge.org/core/books/abs/battle-of-crecy-1346/crecy-campaign/F3C0E506A50EE6D865A781B774758BB1 ; https://perlego.com/book/2446979/crcy-1346-a-tourists-guide-pdf ;
  https://www.encyclopedia.com/history/news-wires-white-papers-and-books/travel ; https://myarmoury.com/talk/viewtopic.php?p=289924 ; `docs/AUDYT-CZAS-MAPA.md`.

Lore:
- https://awoiaf.westeros.org/index.php/Gold_dragon ; https://awoiaf.westeros.org/index.php/Hand%27s_tourney ; https://awoiaf.westeros.org/index.php/Money

Gra (pomiary):
- `Modules/Armoury/Armoury-2026-10-08_08-18-38.log` l. 38, 203, 209, 11434, 23952, 37020, 49811, 62859, 76424, 76859 i linie "Zold:", "Korona:" z doby 109200.
- `Documents/Mount and Blade II Bannerlord/CrashScribe/economy-2026-10-08_08-20-02.csv` (dni 108836, 109018, 109200; mediany wg kategorii rodu).
- `Modules/Armoury/Armoury-2026-10-08_11-40-05.log` l. 39 (WorldPace 50%); `Configs/ModSettings/Global/Armoury/Armoury.json` ("WorldPacePercent": 75).
