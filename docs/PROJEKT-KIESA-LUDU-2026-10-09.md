# PROJEKT - KIESA LUDU: kto w swiecie gry zarabia, komu placi podatki i za co kupuje chleb (09.10.2026, wersja 2 po krytyce)

Status: SAM PROJEKT, bez kodu. Nic nie wgrane, nic nie zacommitowane, gra nieuruchamiana. Jedyne pliki: ten dokument i rachunek w katalogu sesji (rozdz. 8).
Wersja 2: po dwoch krytykach (25 uwag, rozdz. 9) model przebudowany i rachunek policzony od nowa (`sim2\sim_kiesa2.py`); wersja 1 i jej rachunek zostaja
w `sim\` do porownania.
Zlecenie Jeffa (08.10 w nocy): "pamietaj, ze teraz wrzucasz wszystko jako dochod miasta lorda, ale nie caly dochod idzie do miasta lorda - to ludzie zarabiaja,
a miasto i lordowie pobieraja tylko podatki albo wynagrodzenia, a ludzie, spoleczenstwo, po opodatkowaniu wydaje na zywnosc, kwaterunek, zdrowie itp. -
przejrzyj ten system ponownie wedlug zasad w sredniowieczu i dostosuj liczby tak, aby mialo to sens historyczny i ekonomiczny"; "wyobraz sobie, ze tworzymy
de facto symulacje modelu finansowego i ekonomicznego w sandboxie gry".

Podstawa (nie powtarzam, buduje na niej): `docs/audyt-2026-10-09/01-dochody-ludnosci.md` (dalej 01), `09-historyk-liczby.md` (dalej 09: 1 zl = 1 pens ok. 1300),
`00-AUDYT-SWIATA-2026-10-09.md` (dalej 00: Z10, 2.2 "dwie skale", 2.10, S8-S10, E11), `docs/PROJEKT-EKONOMIA-OBIEG-2026-10-08.md` (dalej PROJEKT: Z1-Z9,
5.3 zawor, 6.1 dwor, 11.2 wynik, 12 paczki), rachunek krytyka `krytyk\sim_krytyk.py`.
Kod = wersja w grze (Armoury c01a54ba = commit 2e235ea). Dekompilacje: gra 1.4.8 (`ore-supply\cs`), BK (`ore-supply\bk`, `audyt-pieniadz\bkall`).
Oznaczenia: [K] kod plik:linia, [P] pomiar z logu, [H] historia/lore ze zrodlem, [H?] z drugiej reki albo z pamieci, [S] szacunek, [R] wynik rachunku tego projektu.
Kalendarz: rok = 364 doby; 1 zl gry = 1 pens angielski ok. 1300 (`docs/AUDYT-CEN.md:16`); 1 funt (L) = 240 zl, szyling (s) = 12 zl.
Jednostka w calym dokumencie: **zl na dobe kalendarzowa**; dniowki historyczne (na dzien roboczy) przeliczam razy 250/364.

---

## 0. Dla Jeffa

1. Masz racje: dzis w miescie nikt nie zarabia - kasa miasta to jeden worek, do ktorego wpadaja place, uslugi dla zolnierzy i "zakupy" mieszczan za zloto
   z niczego, a pan bierze z tego worka 7% dziennie.
2. Proponuje "kiese ludu" w kazdym miescie (wyrobnicy, rzemieslnicy, kupcy-patrycjat) i zamku oraz nowe zasady dla kiesy wsi. Ludzie dostaja pieniadze za prace
   i uslugi, a kasa miasta zostaje kasa kupcow, ktorzy handluja towarem.
3. Ile ludzie jedza i kupuja, liczymy od LICZBY LUDZI (Banner Kings) i koszyka rodziny z Anglii ok. 1300 - a nie od "dobrobytu" miasta, jak dzis robi gra.
   Pierwsza wersja tego projektu tu sie mylila: gra liczy zakupy mieszczan od dobrobytu, wiec rosna o polowe w rok, choc ludzi nie przybywa.
4. Z czego ludzie zyja: obsluga zolnierzy (1/4 tego, co zolnierz wyda w miescie: nocleg, gospoda, pranie), robota przy towarze (mlyn, piekarnia, browar,
   woznice, tragarze - 20% ceny jedzenia, 10% sukna i narzedzi, 4% broni i koni), place w warsztatach (ok. 1/3 wartosci wyrobu), budowy, sluzba u bogatszych.
   Rodziny kupieckie zyja z 3% obrotu i 5% zysku kupcow.
5. Podatki jak w sredniowieczu: panu kary sadowe i oplaty targowe (1-3% zarobku), gminie akcyza 2% od zakupow (gmina buduje mury i bruki i placi panu
   ryczalt), w wojnie koronie (rzemieslnicy 2%, kupcy 3%), czynsz za dom wlascicielom domow.
6. Wies: zatrzymuje 30% utargu i pieniadze od markietanow, placi panu stale oplaty (sad, mlyn, targ: 4 zl na glowe rocznie), a za reszte kupuje w miescie
   sol, zelazo, sukno, garnki i piwo. Pan bierze teraz ok. 2/3 gotowki wsi (dzis ok. 85%). Historycznie bylo 30-50% - roznica to stawka Banner Kings
   "70% utargu dla pana", ktora zostawiam, bo z niej zyje duze wojsko.
7. Niewolnicy tylko tam, gdzie sa w ksiazkach (Volantis, Lys, Myr, Tyrosh, Qarth, Norvos, Zelazne Wyspy - thralls, Dothrakowie); utrzymuje ich wlasciciel.
   W Westeros "niewolni" Banner Kings to zwykla biedota z wlasna kiesa.
8. Rachunek na 4 i 8 lat (wojna, fale wojen, pokoj): zloto swiata stoi co do grosza; kazda klasa w kazdym krolestwie stac na pelny koszyk KAZDEJ doby,
   od pierwszej (kiesy ludu startuja z daru startowego miast, ktory i tak przycinamy); miasta kupuja caly plon; nie ma fali bankructw rodow.
9. Cena: rody maja ok. 9 mln zlota (10%) mniej; wojsko AI ok. 90 zamiast 95 tys. w wojnie i 33 zamiast 37 tys. w pokoju (37% stanu wojny - w Twoich 35-40%).
10. Ty jako pan miasta: dalej nie da sie zarobic na wlasnym wojsku - z kazdej monety zoldu wraca do Ciebie najwyzej 0.62.
11. Uczciwie, trzy rzeczy: (a) w wojnie lud miast zyje w 31% z wydatkow wojska (w pokoju 14%), bo nasze wojsko jest ok. 17 razy ciezsze wobec ludu niz
    sredniowieczne - to Twoja decyzja "wojsko 1:1"; (b) jesli ludzi przybywa szybciej niz ok. 2% rocznie, a pieniadza nie, lud biednieje (jak Anglia przed
    czarna smiercia) - zanim to wlaczymy, trzeba zmierzyc ludnosc Banner Kings przez rok; (c) ludzie i wsie kupia wiecej soli, sukna, zelaza i piwa, a lordowie
    mniej broni - trzeba sprawdzic w tescie, czy tego towaru starczy.
12. Wdrozenie tylko razem z grupa C projektu ekonomii, na nowej kampanii, jako paczka "172 KIESA LUDU", po pomiarze w paczce 169. Pytania sa trzy (rozdz. 7).

---

## 1. Model: kto zarabia, z czego, komu placi i na co wydaje

### 1.1 Zasady (dopisek do Z1-Z10 PROJEKT i 00)

| # | Zasada | Uzasadnienie |
|---|---|---|
| L1 | **Praca i usluga -> lud, towar -> kupcy** (Z10 z 00 2.0). Kasa miasta (`Town.Gold`) to kapital kupcow targu; ludzie maja wlasna kiese. | Zarzut Jeffa; 01 L1. |
| L2 | **Podatek od dochodu i od spozycia, nie od majatku.** Pan: kary i oplaty targowe od zarobku; gmina: akcyza przy zakupie; korona: w wojnie od zarobku rzemieslnikow i kupcow; swiatynia (po pytaniu 7.2). | Miasta zyly z oplat od spozycia i ruchu towaru (myto bramy, murage, gabelle na sol, wino i mieso; Florencja 1338 - wiekszosc wplywow gminy z gabelli, Villani XII.92) [H?]; danina od ruchomosci, nie od gotowki w skrzyni (09 2.6-2.7) [H]. |
| L3 | **Koszyk liczony od ludzi, placony z kiesy.** Towar, ktory zjada miasto = najmniej z dwoch: budzet konsumpcji gry (dziala z dobrobytu) i ludzie BK x koszyk na glowe x indeks cen polki. Placi kiesa ludu do kasy kupcow; gdy ludu nie stac, kupuje mniej - najpierw ucina sukno i zbytki, na koncu chleb. | W grze czynny jest model vanilla `DefaultSettlementEconomyModel` (log 17-59-09 l. 186 i 366 [P]); jego popyt to BaseDemand x dobrobyt + LuxuryDemand x (dobrobyt - 3000), bez ludnosci (`cs DefaultSettlementEconomyModel.cs:61-73`) [K] - rosnie 347.6 -> 520.8 tys. zl/d w rok (01 L2 [P]), choc ludzi nie przybywa. Kolejnosc Engla: jedzenie i piwo to 80% koszyka robotnika (Phelps Brown-Hopkins, 09 2.5) [H?]. |
| L4 | **Kazda kiesa ma zapas w dniach wlasnych wydatkow; nadwyzka ponad zapas (7% dziennie) kupuje PRAWDZIWY towar albo prace.** Wyrobnicy: lepsze jedzenie, piwo, sukno, swiece z polek; rzemieslnicy i patrycjat: w 70% place (sluzba, czeladz, budowa i remont domu), w 30% material i lepsze rzeczy; wies: zelazo, sukno, garnki, piwo w miescie; notable: zbytki i sprzety. Gdy towaru brak - lokata wyrobnikow u kupcow (najwyzej 30 dni wydatkow), zwracana, gdy biednieja. | Z2 (nikt poza rodami nie gromadzi bez konca) i Z1 dla towaru: nikt nie placi za nic. Po 1350 standard zycia ludu rosl przez mieso, piwo i sukno (Dyer, *Standards of Living*) [H?]. Budowa: robocizna ok. 2/3 kosztu [S]. |
| L5 | **Robocizna handlu wedlug rodzaju towaru:** z ceny kazdej sztuki sprzedanej przez kupcow do ludu idzie: jedzenie i piwo 20% (mlyn, piec, browar, woznica, przekupka), sukno, plotno, skora, narzedzia, sol, opal, garnki 10% (przewoz, sprzedaz, wykonczenie), bron, zbroje, konie, zbytki 4% (przewoz i sprzedaz; robota jest juz w placach warsztatow). Karawany: 10% od ich zakupow (ladowanie) i sprzedazy (rozladunek). | Marza kupca w grze to kara handlowa 6% w kazda strone (`cs DefaultTradeItemPriceFactorModel.cs:32, 163-167`) [K]. Assize of Bread (51 Hen. III): piekarz ok. 4 d zysku + ok. 6 d kosztow + otreby na kwarter pszenicy (ok. 67 d), mlynarz 1/16-1/24 ziarna - razem ok. 15-25% ceny chleba [H?]; z przewozem i sprzedaza ok. 20%. |
| L6 | **Dochod rodzin kupieckich (patrycjat): 3% obrotu kupcow + 5% ich zysku ponad zapas** (zawor 7%: z 5% subiekci 20%, bednarze i szkutnicy 10%, rodzina kupca 70%). Reszta zaworu jak w PROJEKT: pan 2/3, korona 1/3. Zamek: 15% zaworu kasy zamku dla podzamcza. | Udzial w obrocie dziala od 1. doby (zawor ruszy dopiero, gdy kasa kupcow jest wysoko ponad zapas - w wersji 1 patrycjat glodowal przez pierwszy rok, krytyka S1). 3% = ok. polowa marzy 6% [S]. |
| L7 | **Place rzemiosla = 35% wartosci wyrobu** kazdego warsztatu (gry, BK 147, rzemiosla miasta 148, zbrojnych WorkshopLaw), pomniejszone o place juz wyplacone za cykl; placi kasa kupcow, ktora dzis zatrzymuje "nadwyzke ceny wyrobow ponad cene sprawiedliwa" (ok. 90-96 tys./d [P]). | W suknie praca to ok. 35-50% kosztu (Munro) [H?]. Wartosc produkcji ok. 250 tys./d: nadwyzka ceny 90-96 tys. [P, 2 x 40 dob], sprzedaz warsztatow zbrojnych 32 tys. [P], rzemieslnicy BK ok. 25 tys. [S], czesc "ceny sprawiedliwej" warsztatow towarowych ok. 100 tys. [S] - do pomiaru w 169. |
| L8 | **Niewolni wedlug prawa krainy.** W krolestwach niewolniczych (Volantis, Lys, Myr, Tyrosh, Qarth, Norvos, Zelazne Wyspy - thralls, Dothrakowie) niewolni BK nie maja kiesy: utrzymanie (0.20 zl na glowe, sam chleb i grube sukno) placa wlasciciele (rzemieslnicy 40%, patrycjat 60%) i im przypada dochod z pracy niewolnych. W Westeros, Braavos, Pentos, Lorath, Qohor, Sarnorze, Ibbenie, Yi Ti, Wyspach Letnich klasa "niewolni" BK to biedota (sluzba, czeladz, gornicy) z kiesa wyrobnikow. | Niewolnictwo zakazane w Siedmiu Krolestwach (https://awoiaf.westeros.org/index.php/Slavery; Jorah wygnany za sprzedaz klusownikow) [H]; Volantis - pieciu niewolnych na jednego wolnego (https://awoiaf.westeros.org/index.php/Volantis) [H]; thralls - decyzja Jeffa 05.10. Qohor i Lorath niepewne (00 R14) - na razie wolne. |
| L9 | **Dwie skale jawnie (00 E11):** lud i pieniadz na ludnosci BK (3.09 mln), wojsko na ksiedze PopulationLaw ("wojsko 1:1", Jeff 05.10). Dochod na glowe ludu jest w rzedzie historii; dochod panow ok. 6-14 razy ponad historie (00 2.2). **Dochod ludu tez stoi czesciowo na duzym wojsku**: w wojnie 31% dochodu ludu miast pochodzi z wydatkow wojska, w pokoju 14% (4.6) - zamiast rzemiosla i handlu dalekiego, ktorych gra ma malo. | 00 2.2; krytyka H5. Tabela PopulationLaw ma blad (+72%/rok, 01 1.4) - nie uzywam jej wcale. |

### 1.2 Kto zarabia - gospodarstwa wedlug klas BK w kazdej osadzie

Ludnosc BK nie jest dzis logowana (01 L9). Licze ja wzorem BK `PopulationManager.GetDesiredTotalPop` (bkall `PopulationManager.cs:471-506`) [K] na hearth i dobrobycie
z `ludzie-regiony.csv` doby 108836 (autotest 08.10 17:59; w rachunku kopia z przebiegu 19-33-14 - ten sam zapis startowy) [P], osobno dla kazdego krolestwa: **miasta 1.650 mln, zamki 0.359 mln, wsie 1.076 mln, razem 3.085 mln** [S].
Udzialy klas - srodek przedzialow `GetDesiredPopTypes` (bkall `:517-670`) [K] z mnoznikami 1; prawdziwe udzialy zaleza od prawa i polityki BK
(`GrowthModel.CalculatePopulationClassDemand`, bkall `:517-560`) - do pomiaru A (rozdz. 5.1).

| Osada | Klasa BK | Kim jest w sredniowieczu / w lore | Udzial | Ludzi (swiat) | Kiesa |
|---|---|---|---|---|---|
| miasto | szlachta (Nobles) 1-3% | **patrycjat**: kupcy, wlasciciele statkow, domow i gospod, bankierzy (Littlefinger z burdelami, magistrowie Wolnych Miast) | 2.2% | 36 tys. | przegroda P |
| miasto | rzemieslnicy (Craftsmen) 6-8% | majstrowie cechow, piekarze, piwowarzy, cyrulicy | 7.9% | 130 tys. | przegroda R |
| miasto | serfowie albo dzierzawcy 60-70% | **wyrobnicy**: tragarze, woznice, sluzba, praczki, budowlancy, przekupki, czeladz | 73.0% | 1.21 mln | przegroda W |
| miasto | niewolni 10-20% | w Westeros i wolnych miastach: biedota (do W, +212 tys.); w krolestwach niewolniczych: niewolni domowi i warsztatowi | 16.9% | 67 tys. niewolnych | utrzymuje wlasciciel (L8) |
| zamek | szlachta 7-9% | rycerze i dworzanie - zyja z dworu pana (placi rod) | 8% | 29 tys. | - |
| zamek | rzemieslnicy, serfowie/dzierzawcy 80% (+ niewolni BK poza krolestwami niewolniczymi) | podzamcze: sluzba, kowal, stajenni, rodziny zalogi - czesc wiktu z kuchni pana | 80-92% | 323 tys. | **kiesa podzamcza** |
| wies | serfowie, dzierzawcy, rzemieslnicy wiejscy 77% (+ niewolni BK poza krolestwami niewolniczymi) | chlopi: villein i wolny dzierzawca; kowale, garncarze, drwale, gornicy | 77-97% | 1.00 mln wolnych | **kiesa wsi** (`Village.Gold`) |
| wies | szlachta 3% | gentry z majatkami BK (`EstateData`) | 3% | 0.03 mln | majatki BK - bez zmian |
| wies | niewolni (kopalnie do 70%) w krolestwach niewolniczych | niewolni kopaln Essos, thralls | - | 44 tys. | utrzymuje wlasciciel (kiesa wsi) |

Rozroznienie serf / dzierzawca zostaje stawka BK: utarg wsi 70% do pana (`BKTaxModel.cs:315`) [K] obejmuje produkcje folwarku i czynsze w naturze, ktore sprzedaje
woz wsi (00 2.5: 70% to stawka BK, nie historia).

### 1.3 Z czego zarabiaja (platnik -> klasa)

| Zrodlo | Platnik | Ile trafia do ludu | Podzial W / R / P | Uzasadnienie |
|---|---|---|---|---|
| **zycie zolnierzy w miescie** (nocleg, gospoda, pranie, kobiety, gra) | sakiewka ludzi (`MenPurse.OnLeft` :190) | **25%**, reszta 75% kupcy (jedzenie i napitek - towar z robocizna 20%, L5) | 80 / 10 / 10 | gospoda 1331: 3 ludzi za dobe - chleb 4, piwo 2, mieso 5.5, opal 2, lozka 2 zl (Jusserand, 09 2.3) [H]: lozko 13% + marza gospody ok. 10% + pranie 2% = 25% [S] |
| **zold zalogi wydany na miejscu** | glowa rodu przez kase osady (`SoldierPay.Route` :325) | 25% (jedna regula z zolnierzami) | 80 / 10 / 10 | jak wyzej |
| **markietani** (163) | sakiewka w polu | 85% miasto (dalej jak zycie w miescie), 15% wies | - | PROJEKT 5.3 bez zmian |
| **dwor pana** (162) | glowa rodu do siedziby | **20%** (sluzba, stajenni, krawcy dworu), 80% kupcy | 80 / 20 / 0 | dwor Tomasza z Lancaster 1313-14: zapasy kuchni, piwnicy, wina, wosku, przypraw 66% + liberie, futra, siodla 14% = 80% towaru (09 2.5, sites.uwm.edu/carlin) [H] |
| **budowy oplacone** | pan (`BuildFunding.Daily` :176) | 75% (murarze, robotnicy, woznice), 25% material - kupcy | 60 / 40 / 0 | dzis place 13.4 z 17.9 tys. = 75% [P] (01 1.2 wiersz 9) |
| **place rzemiosla** (L7) | kasa kupcow, przy kazdym wyrobie | 35% wartosci wyrobu (ok. 87.5 tys./d) | 70 / 30 / 0 | czeladz, robotnicy, przadki - BK liczy jako rzemieslnikow tylko 7.9% miasta, wiec majstrowie to R, reszta W [S] |
| **robocizna handlu** (L5) | kasa kupcow, od kazdej sprzedazy towaru (lista NoteSale, 5.2) | 20% / 10% / 4% ceny wedlug rodzaju towaru | 90 / 10 / 0 | L5 |
| **dochod rodzin kupieckich** (L6) | kasa kupcow | 3% obrotu + 5% zaworu | obrot: P; zawor: 20 / 10 / 70 | L6 |
| **praca u bogatszych** (L4) | nadwyzka R i P ponad zapas | 70% nadwyzki (place), 30% towar | R -> W; P -> W 60, R 40 | L4 |
| **sluzba patrycjatu** | patrycjat | 6% wydatkow w gotowce (wikt sluzby jest w koszyku towaru P) | do W | sluzba to 1/5-1/3 doroslej ludnosci miast w poll tax 1377-81 (Goldberg) [H?]; dostawala glownie wikt i kat oraz kilka szylingow rocznie |
| **czynsz za dom** | W 8%, R 13% wydatkow | polowa notable (wlasciciele domow), polowa przegroda P | - | 2.3 |
| **leczenie** | wszyscy | 1-2% wydatkow | do R (cyrulicy, zielarze, akuszerki) | maesterzy sluza panom (01 2.4) |
| **roboty gminy** | gmina (akcyza) | 2/3 wydatkow gminy: 75% place, 25% material | 50 / 50 / 0 | mury, bruki, mosty, urzednicy (murage, 09 2.7) [H] |
| kopalnie BK ("place zostaja w miescie") | gra BK (`BuildFunding.MineRevenuePostfix` :96-98) | 100% | do W | wynagrodzenie gornikow |
| praca niewolnych (L8) | w krolestwach niewolniczych: 18.8% kazdego dochodu "wyrobnikow" | do wlascicieli | R 40 / P 60 | utrzymuje wlasciciel - i jemu przypada praca |
| wies: utarg taborow | kasa kupcow placi za plon | 30% (K7, paczka 112); 70% pan | kiesa wsi | `BKTaxModel.cs:313-325` [K] |
| wies: markietani | sakiewki w polu | 15% wydatkow w polu | kiesa wsi | PROJEKT 5.3 |
| podzamcze | zaloga i dwor w zamku (25% / 20%), robocizna zamku, 15% zaworu kasy zamku | - | jedna kiesa | jak miasto |

### 1.4 Podatki i oplaty - komu i ile

| Klasa | Pan osady (od zarobku) | Gmina | Korona (tylko w wojnie, od zarobku) | Swiatynia (po pytaniu 7.2) | Zrodlo / uzasadnienie |
|---|---|---|---|---|---|
| wyrobnicy | 1% (kary sadowe, oplaty przy straganie) | akcyza 2% od zakupow | 0% (ponizej progu) | 1% | ok. 60% ludzi ponizej progu podatku od ruchomosci (01 2.2, Campbell 2008) [H]; przemial i piec sa juz w cenie chleba (robocizna L5), czynsz chaty idzie do wlasciciela domu |
| rzemieslnicy | 2% (czynsz burgage, kary) | akcyza 2% + cech 1% od zarobku | 2% | 2% | "sady-mlyny-targi" ok. 4 zl na glowe rocznie (fundament H2, 01 2.2) [H] |
| patrycjat | 3% (myta, prawo targowe) | akcyza 2% | 3% (dziesiatka od ruchomosci w miastach, 1/10 zamiast 1/15) | 3% | subsydium 1334: miasta 1/10, wies 1/15 (09 2.6) [H] |
| podzamcze | 3% (oplaty pana) | - | 1% | 1% | podzamcze nie ma gminy |
| wies (kiesa) | **70% utargu taborow** (BK, bez zmian) + **oplaty stale 4 zl na wolna glowe rocznie** (sad, mlyn, targ) | - | 1.5%/d **nadwyzki** ponad 15 dni zakupow | 5% utargu (z czesci pana: 65/5/30) | fundament H2: "sady-mlyny-targi" 4.0 d na glowe rocznie [H]; wpisowe, heriot i merchet to oplaty jednorazowe (smierc, slub, przejecie gruntu) - w sredniej rocznej sa w tych 4 zl |

**Gmina** to osobny posiadacz (nie kasa kupcow): ok. 11 tys./d (akcyza 10 + cech 1.3); wydaje 7% stanu dziennie: 1/3 jako ryczalt (farma) panu miasta,
2/3 na roboty (75% place ludu, 25% material od kupcow). Historycznie z tych pieniedzy szly mury, mosty, urzednicy i farma krola (fundament H6) [H].

Wynik [R, wojna r4]: lud miast oddaje panu 12 tys./d, koronie 6, gminie 11 (z tego 4.5 wraca do pana jako farma), czynszu 43 - razem ok. 9% dochodu
(bez czynszu: wyrobnik ok. 2.5%, rzemieslnik ok. 6%, kupiec ok. 7%; w pokoju bez korony). Historycznie: chlop ok. 12% calego dochodu (01 2.2). Pan bierze od ludu
malo bezposrednio - wiekszosc jego dochodu z miasta to zawor kupcow (L6), co jest wlasnie "pan pobiera podatki, ludzie zarabiaja".

### 1.5 Co wydaja i komu (koszyk)

| Pozycja | W / R / P (udzial wydatkow) | Odbiorca | Uwagi |
|---|---|---|---|
| **towar z polek** (jedzenie i piwo, odziez, opal, narzedzia; u P z wiktem sluzby; w krolestwach niewolniczych u R i P z utrzymaniem niewolnych) | 91% / 85% / 92% | kasa kupcow (98%) i gmina (akcyza 2%) | koszyk robotnika Phelps Brown-Hopkins: zywnosc i napoje ok. 80%, opal i swiatlo 7.5%, tekstylia 12.5% (09 2.5) [H?] |
| koszyk towaru na glowe na dobe (indeks cen 1.0 = ceny Clarka) | **0.27 / 0.50 / 1.46** zl | - | rodzina 4.4 os. wydaje rocznie: W 2.0 L, R 3.9 L, P 10.6 L (2.1) [H/S] |
| **mieszkanie** (czynsz) | 8% / 13% / 0% (wlasny dom) | polowa notable, polowa patrycjat | 2.3 |
| **zdrowie** | 1% / 2% / 2% | rzemieslnicy (cyrulicy, zielarze) | - |
| **sluzba** (gotowka) | - / - / 6% | wyrobnicy | 1.3 |
| **podatki** | 1.4 | pan, gmina, korona, swiatynia | - |
| **zapas** | 10 / 21 / 30 dni wydatkow | zostaje w kiesie | wyrobnik zyl z tygodniowki (Dyer) [H?] - 10 dni to tygodniowka i drobny zapas (swinia, worek ziarna); kupiec trzyma bogactwo w kasie kupcow, w domu miesiac |
| **nadwyzka** ponad zapas (7%/d) | W: towar; R, P: 70% place, 30% towar | kupcy / wyrobnicy | L4 |
| podzamcze | towar 0.18 zl na glowe (reszta wiktu z kuchni pana), zapas 10 dni | kasa zamku | nadwyzka: towar z kasy zamku |
| **wies** | zakupy 0.07 zl na wolna glowe dziennie (zapas 15 dni) + nadwyzka | kasa kupcow miasta targowego (sol, zelazo i narzedzia, sukno i plotno, garnki, piwo) | gospodarstwo pol-wirgaty kupowalo za ok. 8-10 s rocznie = 22-27 zl na glowe [H?/S]; 0.07 zl/d = 25.5 zl rocznie |

---

## 2. Liczby w zlocie gry: na osobe na dobe i w skali swiata

### 2.1 Na osobe (zl na dobe kalendarzowa; rok 4 rachunku [R]: wojna / pokoj) i sprawdzenie z historia

| Klasa | Wydatki na glowe (z tego towar) | Rocznie na rodzine 4.4 os. | Dochod brutto na glowe (wojna / pokoj) | Na zarabiajacego (1.4 na rodzine) | Historia | Ocena |
|---|---|---|---|---|---|---|
| wyrobnicy | **0.288** (0.262) | wydatki 461 zl = **1.9 L** | 0.373 / 0.332 = 2.5 / 2.2 L | **1.71 / 1.52 zl na dzien roboczy** (1.17 / 1.04 na dobe) | robotnik 1.5 d dziennie (1300-49), rodzina ok. 2 L (Campbell 2008 tab. 17: 39% gospodarstw; 01 2.1) [H] | pokoj zgodny; wojna +14% - nadwyzka idzie na lepsze jedzenie i sukno (L4) [S: wojna podnosi zarobki przy garnizonach] |
| rzemieslnicy | **0.618** (0.525 z utrzymaniem niewolnych w Essos) | 990 zl = **4.1 L** | 0.97 / 1.05 brutto = 6.5 / 7.0 L | 4.4 / 4.8 zl na dzien roboczy brutto | majster 3-4 d dziennie (Statut 1351); gospodarstwo nierolnicze ok. 4 L (Campbell 2008) [H] | wydatki zgodne; brutto wyzej, bo majster placi z niego czeladz i sluzbe (70% nadwyzki, 1.3) |
| patrycjat | **1.78** (1.63) | 2 850 zl = **11.9 L** | 3.87 / 3.08 = 25.9 / 20.6 L | - | kupiec, wiekszy dzierzawca 10-26 L (Campbell 2008, 01 2.1) [H] | wydatki dolna polowa, dochod gorna granica (nadwyzka: sluzba, budowa domow) |
| niewolni (Essos) | 0.20 (utrzymanie) | - | 0 | - | utrzymanie u wlasciciela (L8) | - |
| podzamcze | 0.18 (towar; reszta wiktu z kuchni pana) | - | 0.32 / 0.48 | - | sluzba zamku: 0.67-1 L rocznie + utrzymanie (giermek-sluga, 09 2.1) [H] | zgodne co do rzedu; w pokoju bogatsze (wiekszy dwor w zamkach) |
| chlop (gotowka) | zakupy **0.07** + nadwyzka = 0.152 / 0.126 | 55 / 46 zl na glowe rocznie | wplyw do kiesy 0.179 / 0.137 | - | sprzedaz chlopa 33-48 zl na glowe rocznie brutto, zakupy 22-27 zl (fundament H4, 01 4.3) [H] | ok. 2 razy historia - plon i markietani w skali wojska (L9) |
| pan ze wsi (na wolna glowe wsi) | - | - | 0.273 (70% plonu + oplaty 11 tys./d) | - | czynsz i prawa panskie ok. 15 zl na glowe rocznie (01 2.2) [H] | ok. 6.6 razy historia - dwie skale (L9); udzial pana w gotowce wsi 62% (wojna) / 68% (pokoj), historycznie 30-50% |

Koszyk na glowe wyprowadzony z historii, nie z BK (krytyka H1, S8): W 0.27 = rodzina 2.0 L rocznie x 91% towaru; R 0.50 = 3.9 L x 85%; P 1.46 = 10.6 L x 92%.
Stosunek P/W 5.4 i R/W 1.9 - historycznie kupiec 5-13 razy wyrobnik, rzemieslnik ok. 2 razy [H]. BK daje kazdej klasie 0.1 jedzenia na glowe i inne wagi zbytkow -
ale w grze model BK i tak nie dziala (L3), wiec wagi BK nie maja znaczenia. Wrazliwosc: kupiec 16 L i rzemieslnik 5 L - przechodzi (4.4).
Indeks cen: koszyk liczony w wartosci (ceny Clarka 1:1, `HistoricalPrices.cs:44-67`); gdy ceny polek sa wyzsze (zboze w dobie 40: 1.65 wartosci [P]),
ten sam pieniadz kupuje mniej - to jest drozyzna i rachunek pokazuje ja jako wariant (4.4: indeks 1.3).

### 2.2 W skali swiata (tys. zl na dobe, rok 4 [R]; wojna / pokoj)

| Wezel | Wplywy | Wydatki |
|---|---|---|
| **lud miast** (1.38 mln wolnych + 67 tys. niewolnych) | **796 / 718**: robocizna handlu 284 / 229; wojsko (uslugi) 124 / 52; rzemioslo 85 / 85; praca u bogatszych 74 / 66; obrot (P) 64 / 49; dwor 49 / 62; zawor 39 / 32; praca niewolnych 24 / 22; czynsz (P) 22 / 22; budowy 15 / 86; gmina 7 / 6; leczenie 5; sluzba 4 | towar 499 (gmina 10 z tego), czynsz 43, podatki 18 / 11, nadwyzka: W towar 115 / 59, R i P place 77 / 69 i towar 33 / 29 |
| **lud podzamczy** (0.32 mln) | 105 / 154 (zaloga 15 / 9, dwor 23 / 68, robocizna 39 / 46, zawor 27 / 31) | towar 58, pan 3 / 5, nadwyzka - towar z kasy zamku 42 / 91 |
| **kiesy wsi** (1.0 mln wolnych) | 179 / 137 (utarg 112 + markietani 67 / 25) | zakupy w miescie 70 + 82 / 56, oplaty panu 11, danina 17 / 0 |
| **gmina** | akcyza 10 + cech 1.3 | farma panu 4.5, roboty 9 |
| **kupcy miast** | towar od wszystkich 2 131 / 1 635 (NoteSale, 5.2) | plon wsi 374, lup i jency 171 / 0, robocizna 296 / 239 (13.9 / 14.6% sprzedazy), place rzemiosla 88, obrot P 64 / 49, danina i clo 129 / 30, zawor 793 / 640 (lud 39 / 32, pan 502 / 406, korona 251 / 203) |
| rody | zawor kupcow 502, zamki ok. 102, utarg wsi 262 + oplaty 11, podatki ludu 15, farma gminy 4.5, korona: zwrot 214, renty 283, zapomoga 37, lup 171 | zold 447 + 195, dwor 352, sprzet 504, budowy 25, powinnosci 36 |

Stosunek dochodu panow do dochodu ludu (miasta + podzamcza + wsie): ok. 1.6 / 1.08 = **ok. 1.5** (historycznie 0.15-0.2, 00 2.2) [R/H] - "dwie skale" (L9).
Po raz pierwszy ludzie maja prawdziwy dochod: ok. 1.08 mln zl dziennie w wojnie i 1.01 w pokoju (dzis: place ok. 22 tys. do kasy miasta i ok. 37 tys. do kies wsi,
z ktorych pan zabiera prawie wszystko - 01 1.2-1.3).

### 2.3 Zgodnosc z dzisiejszymi cenami, zoldem i warsztatami

- **Zold** zostaje (piechur 2, lucznik 3, rycerz ok. 24-26 zl na dobe, `AUDYT-CEN.md:20-28`) [K]. Historycznie (na dzien roboczy) robotnik 1.5 d, piechur 2,
  lucznik 3 (09 rozdz. 3); na dobe kalendarzowa robotnik 1.5 x 250/364 = 1.03 d, czyli piechur zarabial 1.9 raza, lucznik 2.9 raza tyle co robotnik.
  W grze wyrobnik zarabia 1.04-1.17 zl na dobe na zarabiajacego - piechur 1.7-1.9 raza, lucznik 2.6-2.9 raza tyle. Zgodne.
- **Place warsztatow** zostaja (3 zl dniowka, mistrz 3 + 1.5 na tier); dochodzi 35% wartosci wyrobu (L7), pomniejszone o te place.
- **Ceny towaru** zostaja; koszyk w wartosci razy indeks cen polki (2.1).
- **Dom** w RealisticCaptivity (miejski 2 400 zl, chata 480 zl): czynsz wyrobnika 8% z 461 zl = 37 zl rocznie (ok. 3 s; chata u Dyera 5 s - czesc biedoty mieszka
  u pracodawcy) - chata 480 zl to 13 lat czynszu, historycznie kupno = 10-15 lat czynszu (09 2.3) [H] - **zgodne**. Rzemieslnik: 13% z 990 zl = 129 zl
  (ok. 11 s; dom rzemieslnika 20 s u Dyera, polowa majstrow we wlasnym domu burgage) - zgodne co do rzedu.

---

## 3. Tabela przeplywow - kazda moneta: platnik -> odbiorca (Z1), bez zysku na wlasnym wydatku (Z8)

Wojna, stan ustalony, rok 4 rachunku, tys. zl na dobe [R]. "Dzis" = kod w grze (2e235ea) [K].

| # | Przeplyw | Dzis (kod) | Po 172: platnik -> odbiorca | Ile | Gdzie w kodzie |
|---|---|---|---|---|---|
| 1 | zold partii | glowa -> sakiewki (107) | bez zmian | 447 | `SoldierPay` |
| 2 | zycie zolnierzy w miescie | sakiewka -> kasa miasta 100% | sakiewka -> **kupcy 75% (NoteSale, jedzenie) / lud 25%** | ok. 287 / 96 (+ wies 68) | `MenPurse.OnLeft` :190 |
| 3 | markietani | (163) sakiewka -> miasto 85%, wies 15% | miasto jak wiersz 2; wies bez zmian | j.w. | 163 |
| 4 | zold zalog | glowa -> kasa osady 100% | -> **kupcy / kasa zamku 75% / lud 25%** | miasta 99 / 33, zamki 47 / 15 | `SoldierPay.Route` :325 |
| 5 | dwor | (162) glowa -> kasa siedziby | -> **kupcy 80% / lud 20%** | 352 (miasta 203 / 49) | 162 |
| 6 | budowy | pan -> kasa osady (place) + targ (materialy) | place -> **lud**, materialy -> kupcy (NoteSale) | 25 | `BuildFunding.Daily` :174-176 |
| 7 | sprzet i werbunek AI | lord -> kasa miasta | bez zmian + NoteSale (4%) | 504 (445 w miastach) | `AiGear.cs:287`, `LevyGold.cs:82` |
| 8 | **place rzemiosla** (L7) | warsztat -> kasa miasta (3-25 tys.) | kasa kupcow -> **lud** 35% wartosci wyrobu (z placami juz wyplaconymi) | 88 | `WorkshopLaw` :313, `WorkshopTrade.PayToTown` :323, BK 147, `TownCrafts.Work` :279-302 |
| 9 | **robocizna handlu** (L5) | brak | kasa kupcow -> **lud**: 20 / 10 / 4% wczorajszej sprzedazy wedlug rodzaju | 296 (+ zamki 39) | nowy `TownFolk.Daily` |
| 10 | **dochod rodzin kupieckich** (L6) | brak | kasa kupcow -> **patrycjat** 3% obrotu | 64 | `TownFolk.Daily` |
| 11 | **towar dla ludu** | z niczego -> kasa miasta (BK), K6 cofa | **lud -> kupcy** (98%) i gmina (akcyza 2%); kotwica: najwyzej koszyk ludzi | 499 (+ zamki 58) | BK `EconomyPatches.cs:637`, :1273 (`CalculateBudget`); K5/K6 `OnConsumed` |
| 12 | czynsz za dom | brak | lud -> **notable 50% / patrycjat 50%** | 43 | `TownFolk.Daily`, 164c |
| 13 | podatki ludu | brak (podatek klas BK wyzerowany, `PopulationLaw.TownTaxPostfix` :150-165) | lud -> **pan** 12 (+3 podzamcze), **korona** 6; cech -> gmina 1.3 | 22 | `TownFolk.Earn` |
| 14 | **gmina** | brak | akcyza + cech -> 1/3 farma panu, 2/3 roboty (75% place ludu, 25% material) | 11 | `TownFolk.Daily` |
| 15 | nadwyzka ludu | brak | W -> **towar z polek** (NoteSale; bez towaru - lokata); R i P -> 70% place W/R, 30% towar | 115 / 77 / 33 | `TownFolk.Daily` + kotwica (dodatkowy budzet) |
| 16 | notable (czynsz) | (164c) nadwyzka do kasy osady | -> **towar** (zbytki, sprzety; NoteSale) | 22 | 164c |
| 17 | zakup plonu | kupcy -> taborow -> 70% pan, 30% wies (K7) | bez zmian | 374 | BK `EconomyPatches.cs:1092-1099`, 112 |
| 18 | **zakupy wsi** | brak (wies nic nie kupuje) | kiesa wsi -> kupcy miasta targowego, towar z polki (NoteSale): 0.07 zl na glowe + nadwyzka | 70 + 82 | nowy `TownFolk.Villages` |
| 19 | renta wsi | 20%/d CALEJ kiesy -> pan (`PopulationLaw.cs:312-315`) | **oplaty stale 4 zl na wolna glowe rocznie** -> pan | 11 | `PopulationLaw.Daily` |
| 20 | danina wojenna wsi | 1.5%/d calej kiesy -> skarbiec | 1.5%/d **nadwyzki** ponad zapas -> skarbiec | 17 | `KingdomTreasury.Levies` :221-287 |
| 21 | zawor kupcow | (111') 7% nadwyzki: pan 2/3, korona 1/3 | **lud 5%**, z reszty pan 2/3, korona 1/3; podstawa bez lokaty ludu | 39 / 502 / 251 | `TownPurse.Daily` (111') |
| 22 | zawor kasy zamku | (110/114) pan + korona | **podzamcze 15%**, reszta jak dotad | 27 / ok. 102 / 51 | `CastlePurse.Daily` (110) |
| 23 | karawany | kupuja i sprzedaja w miastach (217 / 185 [P]) | bez zmian + NoteSale: robocizna 10% od ich zakupow i sprzedazy, obrot P 3% od zakupow | 40 + 6.5 | hak `GiveGoldAction` (169) |
| 24 | lup i jency | kasa miasta -> lordowie | bez zmian (164) | 171 | `MenPurse`, 164a/b |
| 25 | korona: zwrot, renty, zapomoga | (165) | bez zmian | 214 / 283 / 37 | 165 |
| 26 | dziesiecina (po pytaniu 7.2) | brak | 5% utargu wsi (z czesci pana) + 1-3% dochodu ludu -> kaplan-notabl -> 50% jalmuzna (lud W), 50% sept (place 75%, material 25%) | ok. 19 + 13 | `TownFolk`, `TownFolk.Villages` |

**Z1 - kazda moneta ma oba konce, a kazda moneta za towar ma towar.** Nowe przeplywy to przelewy miedzy posiadaczami; jedyne dzisiejsze "z niczego"
w tym obszarze (wiersz 11, BK `ChangeGold(num6)`) dostaje platnika - lud. Kazda nadwyzka ludu, wsi i notabli kupuje towar z polki (zdjety z polki, NoteSale)
albo prace; zaden przelew "do kupcow za nic" nie zostal. W rachunku suma zlota swiata stoi co dobe z bledem 0.00 zl (4.2).

**Z2 - zbiornik bez wyjscia: zaden.** Kupcy - zawor; lud - towar, czynsz, podatki, nadwyzka (towar albo place); lokata ludu - zwrot przy biedzie, pulap 30 dni;
podzamcze - towar i nadwyzka; wies - zakupy, oplaty, nadwyzka; notable i gmina - 7%/d; sakiewki - 10%/d; skarbiec - biezace wyplaty (165).
Rok 3 -> rok 4: w wojnie i w pokoju zaden zbiornik swiata nie zmienia sie o wiecej niz 1%; w pojedynczym krolestwie w pokoju najwiecej -4.1% (Lorath),
w wojnie -10% (Nights Watch, male liczby). 8 lat pokoju: zapasy stoja od roku 2 (4.3). Fale wojen: zapasy krolestw oscyluja z fala (do +-20%), bez trendu.

**Z8 - nikt nie zarabia na wlasnym wydatku.** (a) Pan: z monety zoldu wydanej przy wlasnym miescie wraca najwyzej 0.55 w wojnie i 0.62 w pokoju
(PROJEKT 0.62 / 0.68) - 4.5. (b) Lud: z monety wydanej na towar wraca do ludu najwyzej 0.20 robocizny + 0.03 obrotu (do P) + 0.05 x udzial w zaworze - ponizej 0.3.
(c) Kupcy: z monety robocizny wraca do nich przez zakupy ludu najwyzej ok. 0.95 (reszta to podatki, czynsz i zapas) - ponizej 1.

---

## 4. RACHUNEK: 4 lata (1456 dob) i 8 lat

### 4.1 Jak liczone

Program: `scratchpad\kiesa\sim2\sim_kiesa2.py` (ta sesja), przebudowany z `sim\sim_kiesa.py` (wersja 1) na kopii rachunku krytyka `sim_krytyk_kopia.py`
(oryginal nietkniety). Stan wyjsciowy jak w PROJEKT 11.1: 324 rody z `b2\clans2.json`, zamki z CSV `economy-2026-10-08_06-53-20.csv`, skarbce startowe z doby 108876
autotestu 40 dob. **Dane zamrozone w `sim2\dane\`**, bo w trakcie tej pracy rotacja logow Armoury skasowala log 17-59-09 (jak wczesniej 06-51-52): skarbce doby 108876
i `ludzie-regiony.csv` doby 108836 z drugiego przebiegu 40 dob (`Armoury-2026-10-08_19-33-14.log`, skarbce 55.78 mln wobec 56.16 mln w 17-59-09; ten sam zapis startowy),
rody i zamki skopiowane. Rachunek wersji 1 (`sim\`) nie da sie juz powtorzyc bez tych kopii. **Kontrola:** z wylaczona kiesa ludu (`lud=False`) program odtwarza PROJEKT 11.2:
wojna 95.6 / 94.7 tys. (11.2: 95.5 / 94.9), rody 83.3 / 88.6 mln (83.6 / 89.0), skarbce 19.5 / 14.0, fale 79.1 / 77.0, pokoj 36.1 / 36.7.

Co nowe wobec wersji 1 (szczegoly w rozdz. 9):
- **ludnosc i dobrobyt osobno dla kazdego krolestwa** z `ludzie-regiony.csv` (wzor BK) zamiast wedlug liczby miast;
- **kotwica koszyka (L3):** towar miast = najmniej z (budzet gry, ludzie x koszyk); budzet gry rosnie 347.6 -> 520.8 tys./d w 1. roku (pomiar 01 L2 [P]) i dzielony
  jest miedzy krolestwa wedlug dobrobytu miast (vanilla: BaseDemand x dobrobyt + LuxuryDemand x (dobrobyt - 3000)); zamki 61 -> 92 tys./d [S/P]; mnoznik kotwicy
  m = 0.70 (doba 28) -> 0.97 (od doby 364) - pod koniec roku gra chce ok. 3% mniej niz koszyk ludzi;
- niewolni wedlug krolestwa (L8), robocizna wedlug rodzaju towaru (L5), place 35% wartosci wyrobu (L7), karawany, dochod kupcow z obrotu (L6), gmina,
  czynsz 50/50, nadwyzki jako towar albo place (L4), oplaty wsi stale, zakupy wsi na wolnych;
- start: kiesy ludu (zapasy 10 / 21 / 30 / 10 / 15 dni) z przycinanego daru startowego miast - **6.72 mln** z ok. 12 mln, ktore 110/111 i tak kasuja (PROJEKT tab. 22);
  kasa kupcow nietknieta;
- **pomiar kazdej doby i kazdego krolestwa** dla 5 klas (W, R, P, podzamcze, wies), osobno pelny koszyk i samo jedzenie; liczba krolestw i krolestwo-dob ponizej 95%
  w kazdym roku (`years_table`).
Kolejnosc doby: rody -> sakiewki -> kasy miast (plon, place rzemiosla, lup, clo, zawor) -> lud miast (P, W, R) -> notable, gmina, swiatynie -> kasy zamkow i podzamcze ->
wsie -> robocizna i obrot kupcow (dochod ludu jutro) -> korona -> dochody rodow. Suma zlota swiata sprawdzana co dobe (blad > 50 zl = stop programu).

### 4.2 Wynik - zestaw koncowy

Stawki: uslugi 25 / 20 / 75%, robocizna 20 / 10 / 4% (karawany 10%), place rzemiosla 35% z 250 tys., obrot P 3%, zawor ludu 5% (zamek 15%), czynsz W 8% R 13%
(50/50), koszyk 0.27 / 0.50 / 1.46 / 0.18 zl, zapasy 10 / 21 / 30 / 10 / 15 dni, nadwyzka R i P 70% place, oplaty wsi 4 zl na glowe rocznie, akcyza 2%.

| Miara | PROJEKT wojna r1 / r4 | **KIESA LUDU wojna r1 / r4** | PROJEKT fale | **KL fale** | PROJEKT pokoj | **KL pokoj** |
|---|---|---|---|---|---|---|
| wojsko w partiach rodow | 95.6 / 94.7 tys. | **91.0 / 89.9** | 79.1 / 77.0 | **75.3 / 72.8** | 36.1 / 36.7 | **32.0 / 32.9** |
| glowy rodow < 5 000 (z 324) | 2 / 2 | **2 / 2** | 4 / 6 | **4 / 6** | 2 / 2 | **2 / 2** |
| zalegly zold | 0 / 0 | **0 / 0** | 0 / 0 | **0 / 0** | 0 / 0 | **0 / 0** |
| zloto rodow | 83.3 / 88.6 mln | **74.4 / 79.7** | 84.2 / 89.9 | **75.4 / 81.1** | 87.4 / 93.0 | **79.7 / 84.6** |
| skarbce (pustych) | 19.5 / 14.0 (0) | **19.5 / 14.0 (0)** | 19.6 / 14.0 | **19.6 / 14.0** | 19.7 / 14.2 | **19.7 / 14.2** |
| sakiewki zolnierzy | 4.75 / 4.71 mln | 4.52 / 4.47 | 3.93 / 3.76 | 3.75 / 3.55 | 1.79 / 1.83 | 1.59 / 1.64 |
| kasy kupcow miast | 15.73 / 15.94 mln | 17.43 / 17.65 | 15.46 / 15.59 | 17.19 / 17.33 | 13.93 / 13.85 | 15.46 / 15.62 |
| kasy zamkow | 5.32 / 5.33 | 5.37 / 5.38 | 5.43 / 5.34 | 5.43 / 5.38 | 5.97 / 5.88 | 5.81 / 5.76 |
| kiesy wsi | 0.69 / 0.68 | **2.14 / 2.14** | 0.65 / 0.64 | 2.06 / 2.01 | 0.56 / 0.56 | 1.78 / 1.79 |
| lud miast W / R / P (mln) | - | 5.55 / 2.22 / 2.82 -> 5.60 / 2.24 / 2.84 | - | 5.38 / 2.27 / 2.76 -> 5.35 / 2.30 / 2.76 | - | 4.45 / 2.38 / 2.50 -> 4.86 / 2.41 / 2.52 |
| lud podzamczy / notable / gmina | - | 1.08 / 0.29 / 0.18 | - | 1.22 / 0.29 / 0.18 | - | 1.79 / 0.29 / 0.17 |
| **stac na koszyk - KAZDA doba, KAZDE krolestwo, 5 klas** | - | **wszystkie >= 95%** (najnizej podzamcze 99%, House Baratheon, doba 483) | - | **wszystkie >= 95%** | - | **wszystkie >= 95%** |
| stac na jedzenie (osobno) | - | 100% kazdej doby | - | 100% | - | 100% |
| zakup plonu wsi | 100% | **100%** | 100% | **100%** | 100% | **100%** |
| rody samych zamkow: rodow > 10% zmiany kiesy na 28 dob (z 119) | 0 / 0 | **0 / 0** | 2 / 3 | **10 / 15** (mediana 0%, najwiecej +-20%) | 0 / 0 | **0 / 0** |
| blad sumy zlota swiata | 0 | **0.00 zl kazdej doby** | 0 | **0.00** | 0 | **0.00** |

Start (pierwsze doby): kiesy ludu majace pelny zapas z daru startowego (6.72 mln) - stac 100% od doby 1 we wszystkich krolestwach; m kotwicy 0.70 -> 0.97,
bo gra na poczatku chce mniej towaru niz koszyk ludzi (budzet z dobrobytu rosnie przez rok) - nadwyzke ludzie wydaja na lepszy towar (L4).

Uwagi do wyniku:
- **Kryteria zlecenia:** zloto zamkniete (0.00 zl przez 1456 i 2912 dob); zbiorniki bez wyjscia - brak (4.3); fala bankructw - brak (glowy < 5 000: tyle samo co PROJEKT;
  zalegly zold 0); zakup plonu 100% (prog >= 98%).
- **Kryterium S9 z 00** ("lud miast co najmniej 95% konsumpcji"): spelnione mocniej niz w zleceniu - kazda z 5 klas, w kazdym krolestwie, kazdej doby od pierwszej,
  w wojnie, falach i pokoju (4 i 8 lat).
- **Wojsko** 85-100 tys. w wojnie (PROJEKT 10.2) - spelnione (89.9; -5% wobec PROJEKT); pokoj 32.9 = 37% wojny (Jeff: 35-40%).
- **Rody samych zamkow w falach:** 10-15 ze 119 zmienia kiese o ponad 10% w 28 dob (PROJEKT 2-3; wersja 1: 7-10), mediana 0%, najwiecej +-20%. Przyczyna: oplaty wsi
  stale (11 tys./d zamiast 89) - pan zamku ma mniejsza kiese i ostrzej czuje przejscie wojna/pokoj. W stalej wojnie i pokoju: 0. Do "Budzetu rodow" (166).

### 4.3 Zbiorniki: czy cos rosnie bez konca (mln zl; wojna r1 / r2 / r3 / r4)

kupcy 17.43 / 17.64 / 17.67 / 17.65; zamki 5.37 / 5.38 / 5.38 / 5.38; wsie 2.14 przez 4 lata; wyrobnicy 5.55 / 5.60 / 5.61 / 5.60; rzemieslnicy 2.22 / 2.24 / 2.24 / 2.24;
patrycjat 2.82 / 2.84 / 2.84 / 2.84; podzamcze 1.08; notable 0.28-0.29; gmina 0.18; lokata 0; sakiewki 4.52 -> 4.47; skarbce 19.5 / 14.7 / 14.1 / 14.0.
Pokoj: wyrobnicy 4.45 / 4.90 / 4.89 / 4.86, patrycjat 2.50 / 2.53 / 2.52 / 2.52 (w wersji 1 patrycjat rosl r3 -> r4 o 27% - krytyka S3); 8 lat pokoju: wszystkie zapasy
stoja od roku 2 (`wyn\pokoj8.txt`).
**Gdzie lezy pieniadz, ktory rody "stracily" (8.9 mln wobec PROJEKT w r4 wojny):** kiesy ludu miast 10.7 mln, podzamcza 1.1, wsie +1.5, kupcy +1.7, notable 0.3, gmina 0.2 -
razem 15.4 mln, z czego 6.7 mln przyszlo z przycinanego daru startowego, a reszta od rodow (i 0.2 mln z sakiewek). To zapasy dni wydatkow, nie dziura.

### 4.4 Wrazliwosc (wojsko wojna r1 / r4; pokoj r1 / r4; klasy ponizej 95% - najgorsze krolestwo, KAZDA doba) [R]

Zestaw koncowy: wojna 91.0 / 89.9, pokoj 32.0 / 32.9, wszystkie klasy >= 95% kazdej doby. Pelne wyniki: `sim2\zestawienie.txt`, `sim2\wyn\*.txt`.

| Zmiana | Wojna | Pokoj | Klasy ponizej 95% (najgorzej) | Wniosek |
|---|---|---|---|---|
| koszyk na glowe +10% | 91.0 / 89.9 | 32.0 / 32.9 | wojna: W 1 krol. (91%), podzamcze 2 krol. (88%); pokoj: brak | margines ok. 5-10% |
| koszyk +20% | 91.0 / 89.9 | 32.0 / 33.0 | W 79-86%, podzamcze 77-80% (1-3 krol.); jedzenie >= 97% | kara tylko za jedzenie (7.1) - nie zadziala |
| ceny polek 1.3 wartosci (drozyzna) | 91.0 / 89.9 | 32.1 / 33.0 | wojna W 76%, podzamcze 73% (1-4 krol.), fale podzamcze 57%; pokoj W 80% | drozyzna zuboza - historycznie tak; zamki najslabsze |
| kupiec 16 L, rzemieslnik 5 L (wagi klas, krytyka S8) | 91.0 / 89.9 | 32.0 / 33.0 | brak | wagi z historii; margines jest |
| ludnosc BK +2% rocznie | 91.0 / 89.9 | 32.0 / 33.0 | podzamcze 90-94% (1-2 krol.) | prog ok. 2% rocznie |
| ludnosc BK +3% rocznie | 91.0 / 89.9 | 32.0 / 33.0 | W 90-94%, podzamcze 86-90% (1-2 krol.) | **wzrost ludnosci zuboza** (krytyka S7) |
| ludnosc BK +5% rocznie | 91.0 / 89.9 | 32.0 / 33.0 | W 81-84%, podzamcze 79-82% (1-3 krol.) | jw. |
| ludnosc +10% rocznie | 91.0 / 89.9 | 32.0 / 33.0 | W 73-79%, podzamcze 54-66%, jedzenie podzamcza 67-82% | jw.; kotwica trzyma miasta, zamki nie |
| ludnosc +10% i produkcja +10% (wiecej rak) | 91.0 / 90.6 | 32.0 / 33.2 | W 80-85%, podzamcze 71-73% | pomaga, nie wystarcza |
| ludnosc +10% i dobrobyt +10% rocznie | 91.0 / 90.5 | 32.0 / 33.7 | W 66% w 22-24 krol. | budzet gry rosnie - kotwica przestaje hamowac |
| polki: tylko polowa dodatkowych zakupow znajduje towar | 87.3 / 87.1 | 30.1 / 30.8 | podzamcze 93-94% (1 krol.), pokoj W 94% (1 krol.) | brak towaru zweza obieg (-2.8 tys. wojska): lokata stoi u kupcow |
| robocizna 15% / 8% (zamiast 20 / 10) | 91.2 / 90.2 | 32.8 / 33.5 | wojna W 84%; pokoj W 81% w 7 krol. | 20 / 10 / 4 to minimum |
| jedna stawka robocizny 20% (jak wersja 1) | 90.3 / 89.3 | 31.3 / 32.5 | brak | -0.6 tys.; stawka wedlug towaru wystarcza |
| bez karawan w podstawie | 91.2 / 90.1 | 32.7 / 33.4 | wojna W 82%, pokoj W 81% w 6 krol. | karawany to ok. 6% dochodu ludu - potrzebne |
| wartosc wyrobow 150 tys. (zamiast 250) | 91.1 / 90.1 | 32.6 / 33.2 | W 84-86% (1-5 krol.) | pomiar w 169 rozstrzyga |
| uslugi 30% / 40% | 90.8 / 89.8; 90.6 / 89.6 | 31.9 / 32.9; 31.7 / 32.8 | brak | liczba 25% malo wazna |
| bez udzialu w zaworze (5% -> 0) | 91.2 / 90.1 | 32.5 / 33.1 | wojna W 95% (1 krol., rok 1); pokoj W 89% (Zelazne Wyspy, ok. 70 dob roku 2) | 5% zamyka biedne krolestwa |
| zawor 20% zamiast obrotu 3% (propozycja krytyka S3) | 90.4 / 89.5 | 31.1 / 32.3 | brak | tez dziala; -0.4 tys. wojska i patrycjat 2.3-2.7 raza potrzeby |
| obrot P 2% | 91.1 / 90.0 | 32.2 / 33.0 | brak (pokoj W 95%) | - |
| zapas wyrobnikow 7 / 14 dni | 91.0 / 90.0; 90.8 / 89.8 | 32.0 / 33.0; 31.9 / 32.8 | brak | - |
| start jak w wersji 1 (z kasy kupcow, limit) | 89.4 / 88.6 | 30.4 / 31.5 | W 86-91%, podzamcze 92% | start z daru jest lepszy (krytyka S1) |
| start bez przelewu | 89.4 / 88.6 | 30.4 / 31.6 | 1. rok: W 43%, P 33%, podzamcze 38% w 20-27 krol.; po dobie 120 W 86-91% | przelew startowy konieczny |
| bez kotwicy (budzet gry jak dzis) | 91.5 / 90.4 | 33.0 / 33.4 | pokoj 1. rok W 82% (5 krol.) | kotwica wygladza 1. rok |
| oplaty wsi jak w wersji 1 (7%/d nadwyzki do pana) | 92.5 / 91.6 | 32.9 / 33.7 | pokoj W 90% (1 krol.) | +1.7 tys. wojska, ale pan bierze 79-81% gotowki wsi (krytyka H3) |
| zakupy wsi o polowe mniejsze | 91.0 / 90.0 | 31.9 / 32.8 | brak | - |
| przeciek miedzy krolestwami 40% | 91.1 / 89.4 | 32.6 / 33.1 | brak | krainy sie nie rozjezdzaja |
| swiatynie i dziesiecina (7.2) | 90.6 / 89.5 | 31.5 / 32.6 | brak | -0.4 tys. wojska |
| pulap sprzetu pana 20% zoldu (166) | 91.3 / 90.3 | 32.1 / 33.3 | brak | +0.4 tys.; pomaga |
| **dziura 10 tys./d** w kasach miast | 90.1 / **87.1** (PROJEKT 92.2) | 31.2 / 30.2 (PROJEKT 33.5) | pokoj W 91% (1 krol.) | obieg z kiesa ludu jest czulszy na dziury |
| **dziura 25 tys./d** | 88.7 / **78.4** (PROJEKT 84.8) | 29.9 / 23.8 (PROJEKT 25.2) | pokoj W 78% w 21 krol.; jedzenie 97% | szczelnosc (Z9) jest warunkiem |
| 8 lat (wojna / fale / pokoj) | 91.0 / 89.3 | 32.0 / 32.7 | brak | fale 75.3 -> 65.5 (PROJEKT 79.1 -> 69.3 - ta sama fala) |

### 4.5 Gracz (Z8): ile wraca z 1 zl zoldu (`sim2\gracz_kiesa2.py`; lenno: 1 miasto, 1 zamek, 4 wsie; partia przy wlasnym miescie - najgorszy przypadek)

| | PROJEKT wojna | **KL wojna** | PROJEKT pokoj | **KL pokoj** |
|---|---|---|---|---|
| partia 300 / 1000 | 0.62 / 0.62 | **0.55 / 0.53** | 0.68 / 0.68 | **0.62 / 0.60** |
| zaloga 200 / 600 | 0.52 / 0.55 | **0.53 / 0.55** | 0.59 / 0.62 | **0.60 / 0.62** |
| partia 1000 + zaloga 600 | 0.60 | **0.53** | 0.66 | **0.60** |

### 4.6 Porownanie z PROJEKT 11.2 i z wersja 1 - co i dlaczego

- Wojsko -5% w wojnie i falach, -10% w pokoju (wersja 1: -4% i -11%): w kiesach ludzi stoi ok. 12 mln, we wsiach +1.5 mln; z tego 6.7 mln z daru startowego.
  W zamknietym obiegu wielkosc wojska wyznacza pieniadz w rekach rodow (PROJEKT K9).
- Rody dostaja mniej z wsi (oplaty 11 zamiast 176 - wies kupuje w miescie i ten pieniadz wraca zaworem: 2/3 zaworu to pan), z zaworu (5% dla ludu, robocizna,
  place, obrot), za to wiecej od korony (renty 283 zamiast 260 w rachunku PROJEKT - korona dostaje 1/3 wiekszego zaworu i podatki ludu); kupuja mniej sprzetu
  (504 zamiast 555 tys./d) i dworu (352 zamiast 374); zwrot z monety gracza
  nizszy o 0.06-0.09 dla partii, dla zalogi bez zmian.
- **Skad lud miast ma dochod (krytyka H5)** [R]: wojna - wojsko (uslugi, robocizna i obrot od wydatkow zolnierzy, zalog i sprzetu) 31%, handel towarem ludu, wsi,
  notabli i zamkow 17%, rzemioslo i budowy 13%, dwor 10%, karawany 6%, zysk kupcow 5%, nadwyzki 2%, wewnatrz ludu (praca u bogatszych, czynsz, praca niewolnych,
  leczenie, gmina) 17%; pokoj - wojsko 14%, rzemioslo i budowy 24%, handel 18%, dwor 14%, karawany 7%, zysk kupcow 4%, wewnatrz ludu 17% (`wyn-zrodla.txt`).
  W wersji 1 wojsko dawalo 49%, rzemioslo 3.5%.
- Wies ma 3 razy wiecej pieniedzy (2.1 zamiast 0.7 mln) i kupuje w miastach 150 tys. zl towaru dziennie - nowy popyt na sol, zelazo, sukno, garnki (01 L3).
- **Towar (krytyka H1, ryzyko 6):** ludzie miast kupuja ok. 670 tys./d towaru (koszyk 499 + nadwyzki 150 + notable 22), wsie 152, podzamcza 100 - dzis "zakupy"
  miast to 310-360 tys. (40 dob) i 521 tys. (koniec roku), a wsie nic. Lordowie kupuja za to mniej broni i koni (sprzet 504 zamiast 555 tys./d w PROJEKT).
  Popyt przesuwa sie z broni i koni na jedzenie, piwo, sukno, sol i zelazo - bilans TOWARY musi to pokazac w tescie (5.4).

### 4.7 Czego rachunek nie widzi

- Pojedynczych miast: rachunek liczy krolestwa, wiec "miasto-zaulek" bez wojska i dworu moze byc biedniejsze od sredniej krolestwa. Log 172 pokazuje liczbe miast
  ze "stac" < 95%.
- Cen: indeks cen polek jest staly (wariant 1.3 w 4.4); w grze wiekszy popyt podniesie ceny jedzenia i sukna.
- Towaru: zaklada, ze dodatkowe zakupy znajduja towar (wariant "polki 50%" w 4.4).
- Rzeczywistej ludnosci BK i jej trendu (pomiar A), notabli i karawan poza czynszem i robocizna, zimy, glodu, oblezen.

---

## 5. Co trzeba zmienic w kodzie

### 5.1 Kolejnosc paczek

| Kol. | Paczka | Co dla kiesy ludu |
|---|---|---|
| 0 | **169 KSIEGA OBIEGU** (w toku) + **pomiar A**: co dobe w kazdym miescie `ConsumedValue` (BK), budzet konsumpcji gry (suma `categoryDemand`) i dobrobyt; ludnosc BK wedlug klas (`PopulationData.GetTypeCount`) i krolestw; wartosc wyrobow warsztatow (gry, BK 147, 148, WorkshopLaw); zakupy i sprzedaz karawan w kazdym miescie; przelewy `GiveGoldAction` wedlug rodzaju partii (ktore to towar) | podstawa wszystkich liczb "na glowe", kotwicy (L3), plac rzemiosla (L7) i karawan; trend ludnosci BK przez rok (krytyka S7); pomiar 172 "na sucho" bez latki |
| 1 | **164a SZCZELNOSC** | warunek: dziura 10 tys./d kosztuje z kiesa ludu -2.8 tys. wojska w 4 lata, 25 tys./d - -11 tys. (4.4) |
| 2 | **dopiski do opisow grupy C** (00 5.2, 01 C): "kasa miasta = kupcy; lud osobno (172)" | **111'**: `TownPurse.OnConsumed` - przy 172 lud placi zamiast cofania; zawor - najpierw 5% dla ludu, podstawa bez lokaty ludu. **110**: zamek 15%. **162**: dwor 80% kasa / 20% lud. **163**: miasto 75 / 25. **112**: bez zmian (30% wsi), renta 20% ustepuje oplatom stalym. **164c**: notable dostaja czynsz od ludu i wydaja go na towar |
| 3 | **GRUPA C**: 110 K5, 111' K6 tryb 1, 162 DWOR, 112 K7, 163 MARKIETANI, 114, 164c **+ 172 KIESA LUDU** (ostatnia w grupie - siedzi na ich hakach) | jedna nowa kampania dla calej grupy; bez K6 lud placilby za towar, za ktory BK i tak dopisuje zloto (podwojnie); K6 bez 172 zostawia jedzenie darmowe |
| 4 | 165 KORONA, 166 BUDZET (pulap sprzetu - pomaga, 4.4), 167, 168 | 166 liczy D z nowymi wplywami (podatki ludu, oplaty wsi, farma gminy) |
| 5 | po roku autotestu 172: swiatynie (jesli Jeff "tak" w 7.2), napis w menu miasta (01 M) | - |

### 5.2 Paczka "172 KIESA LUDU" - pliki i metody (Armoury)

**NOWY `Armoury/src/TownFolk.cs`** (statyczna klasa, wzor `TownPurse`/`MenPurse`):
- stan: na kazde miasto przegrody W, R, P, lokata W, gmina; na zamek kiesa podzamcza; na miasto i zamek "sprzedaz dzis / wczoraj" (wartosc i robocizna);
  slownik po `Settlement.StringId`. Kiesy wsi to dalej `Village.Gold`.
- **ludzie:** z `PopulationData` BK (Nobles -> P, Craftsmen -> R, Serfs + Tenants -> W; Slaves -> W poza krolestwami niewolniczymi, inaczej "utrzymywani");
  gdy BK nie ma danych - wzor `GetDesiredTotalPop` z udzialami 1.2. Lista krolestw niewolniczych w `Settings` (`TownFolkSlaveRealms`, id krolestw ROT -
  sprawdzic nazwy w danych ROT).
- `Earn(Settlement st, int amt, Kind kind)` - jedyne wejscie pieniedzy do ludu: dzieli na klasy (1.3), w krolestwie niewolniczym 18.8% czesci W do wlascicieli
  (R 40 / P 60), od razu pobiera podatki (1.4): pan -> `st.OwnerClan.Leader.ChangeHeroGold(+x)`, korona (tylko w wojnie krolestwa pana) -> `Kingdom.KingdomBudgetWallet += x`,
  cech R -> gmina, swiatynia (gdy wlaczona) -> kaplan-notabl. Kazdy przelew w `MoneyLedger.Note`.
- `NoteSale(Settlement st, int amt, ItemCategory cat)` - licznik sprzedazy kupcow: wartosc (podstawa obrotu P) i robocizna wedlug grupy kategorii
  (jedzenie i napoje 20%, sukno/plotno/skora/narzedzia/sol/opal/garnki/wosk 10%, bron/zbroje/konie/zbytki 4%).
  **Miejsca NoteSale (dokladnie jak w rachunku, krytyka S2):** `PayForGoods` (lud miasta i podzamcza), zakupy nadwyzki ludu, wydatki notabli (164c), wydatki kasy zamku
  w miescie, material robot gminy i swiatyn, `MenPurse.OnLeft` (czesc towarowa), `SoldierPay.Route` (czesc towarowa), `BuildFunding` (material), `AiGear` :287,
  `ArmyClothing` :181, :237-238, 162 (czesc towarowa), 171 (zaloga kupuje w miescie), `TownFolk.Villages`; **karawany i gracz**: z haka `GiveGoldAction` (169) -
  zakup karawany lub gracza w miescie = NoteSale; sprzedaz karawany do miasta = tylko robocizna rozladunku (10%), bez obrotu P. **Nie:** lup, jency, okupy,
  przelewy gry bez towaru, werbunek najemnikow (`LevyGold` - usluga, docelowo sakiewka najetego, 167).
- **Kotwica i "stac" (L3; krytyka H1, S4, S12):** postfiks na BK `ItemConsumptionPatch.CalculateBudget` (prywatna statyczna, `AccessTools.Method`; liczy oba czlony BK:
  popyt x indeks^0.15 i dobrobyt/1000/indeks) mnozy wynik przez `m_miasta x Afford_grupy`; gdy metody nie ma - prefiks `[HarmonyPriority(Priority.First)]`
  na `ItemConsumptionBehavior.MakeConsumption` mnozy `categoryDemand` (i log "czlon dobrobytu BK nieskalowany").
  - `m_miasta = min(1, budzet ludu / budzet gry)`; budzet ludu = suma klas (ludzie x koszyk na glowe x sredni indeks cen kategorii miasta) + utrzymanie niewolnych;
    budzet gry = suma `categoryDemand` dnia (przed skalowaniem).
  - potrzeba klasy c = budzet ludu klasy x m / udzial towaru w wydatkach (1.5); `Afford_jedzenie = suma_c s_c x min(1, kiesa_c / (potrzeba_c x udzial jedzenia_c))`,
    `Afford_reszta = suma_c s_c x min(1, max(0, kiesa_c - jedzenie_c) / reszta_c)`, s_c = udzial klasy w potrzebie miasta. Mianownik to PELNA potrzeba
    (nie `ConsumedValue` po skalowaniu - inaczej Afford liczy sam siebie, krytyka S4).
  - dodatkowy budzet z nadwyzek (W, R i P, notable) dopisany do kategorii "lepszych" (mieso, piwo, wino, plotno, sukno, garnki, wosk) - kupowane z polek przez
    ten sam kod BK.
- `PayForGoods(Town t, int cv, int cvFood)` (po konsumpcji, hak K5 `OnConsumed`): kazda klasa placi swoj udzial (s_c) najwyzej tyle, ile ma; akcyza 2% do gminy;
  **czego lud nie zaplaci - K6 cofa jak dzis i licznik "zjedzone bez zaplaty (zl/d)"** (glownie dokupienie jedzenia przy zapasie miasta < 10%, BK `EconomyPatches.cs:624-631`).
- **Kara glodu** (wylacznik osobny, pytanie 7.1): gdy `Afford_jedzenie` < 0.95 przez 7 kolejnych dob i od startu kampanii minelo co najmniej 120 dob - nasz postfiks
  odejmuje zadowolenie BK jedzenia jak przy braku towaru (`EconomicData.UpdateSatisfaction(Food, -0.0015)` przez refleksje), bo BK przy obnizonym popycie dodalby
  +0.001 (BK `EconomyPatches.cs` ok. :628-636 [K]). `Afford_reszta` < 1 obniza tylko zakupy (BK sam uzna brak towaru zbytkow/ogolnego).
- `Daily()` (raz na dobe, po `TownPurse.Daily` i `CastlePurse.Daily`): robocizna i obrot P z wczorajszej sprzedazy z `Town.Gold` -> `Earn(TradeLabour / MerchantDraw)`
  (najwyzej stan kasy); place rzemiosla 35% wartosci wyrobow dnia -> `Earn(Wages)`; czynsz W 8% / R 13% wydatkow -> polowa notable miasta wedlug `Power`, polowa P;
  leczenie (do R), sluzba P (6% do W); zapas i nadwyzka (7%/d: W -> dodatkowy budzet towaru, bez towaru lokata do 30 dni wydatkow, zwrot przy `Afford` < 1;
  R -> 70% `Earn` W, 30% budzet towaru; P -> 70% `Earn` W 60 / R 40, 30% budzet towaru); gmina 7%/d (1/3 farma panu miasta, 2/3 roboty: 75% `Earn(W 50 / R 50)`,
  25% material NoteSale); linie logu.
- `Villages()`: kazda wies raz na dobe kupuje w miescie targowym (`Village.TradeBound`) towar za najwyzej 0.07 zl x wolni ludzie BK wsi (hearth x 5, gdy brak BK;
  niewolni w krolestwach niewolniczych nie licza sie), z kategorii sol, narzedzia, plotno/sukno, garnki, piwo, po cenie miasta, najwyzej tyle sztuk, ile lezy ponad 5
  na polce; towar znika, zloto `Village.Gold -> Town.Gold` + NoteSale; oplaty stale panu: 4 zl rocznie na wolna glowe (`GiveGoldAction.ApplyForSettlementToCharacter`,
  jak dzis renta, najwyzej stan kiesy); danina 1.5%/d nadwyzki ponad 15 dni zakupow; nadwyzka 7%/d -> dodatkowe zakupy tych samych kategorii.
- **Odbiorca zapasowy (krytyka S13):** pan bez rodu albo `OwnerClan.Leader == null` -> gmina osady; rod pana bez krolestwa -> czesc korony do gminy; miasto bez notabli ->
  czynsz w calosci do P; brak notabla-kaplana (swiatynie) -> jalmuzna do W, budowa do gminy; wies bez pana -> korona krolestwa, a bez krolestwa -> kiesa wsi zostaje.
  Kazdy przypadek liczony w logu ("odbiorca zapasowy: N razy, X zl").
- `Reset()`, `Export()` / `Import()`.

**Zmiany w istniejacych plikach** (numery linii z 2e235ea; po grupie C przesuna sie):
- `MenPurse.cs` `OnLeft` (:173-200, :190): `st.Town.ChangeGold(life)` -> 75% `Town.ChangeGold` + NoteSale (jedzenie), 25% `TownFolk.Earn(st, Soldiers)`; w zamku: 75% kasa
  zamku, 25% podzamcze. Licznik "wydatki wlasnych ludzi we wlasnych osadach" (podstawa zwrotu korony, PROJEKT K3) liczy calosc, jak dotad.
- `SoldierPay.cs` `Route` (:313-326, :325): `town.ChangeGold(amt)` -> 75% kasa osady + NoteSale, 25% `Earn(Garrison)`; `TownPurse.HomePart` (111') liczy tylko 75%.
- `BuildFunding.cs` `Daily` (:174-176): `st.Town.ChangeGold(labourI)` -> `Earn(Building)`; materialy `market.Town.ChangeGold(matSpent)` + NoteSale;
  `MineRevenuePostfix` (:96-98): -> `Earn(Mines)`.
- `WorkshopLaw.cs` `CyclePrefix` (:313-314), `WorkshopTrade.cs` `PayToTown` (:300-324), BK 147 (`rzemieslnicy BK`), `TownCrafts.cs` `Work` (:268-302): place cyklu ->
  `Earn(Wages)`, a do 35% wartosci wyrobu po cenie miasta doplaca kasa kupcow (`TownFolk.CraftWage`); opis `TownCrafts` :43 "bez zlota" do zmiany.
- `AiGear.cs` (:287), `ArmyClothing.cs` (:181, :237-238), 162 (czesc towarowa), 171 - NoteSale.
- `PopulationLaw.cs` `Daily` (:300-316): galaz wsi - przy 172 renta 20% zastapiona przez oplaty stale z `TownFolk.Villages`; `TownTaxPostfix` (:150-165) bez zmian
  (podatek klas BK z niczego zostaje wyzerowany - zastepuja go podatki ludu).
- `KingdomTreasury.cs` `Levies` (:221-287): danina wsi od nadwyzki ponad zapas (`TownFolk.VillageFloor`); nowa pozycja w "Korona:" - "podatek wojenny ludu".
- `TownPurse.cs` (111'): zawor liczony od kasy ponad zapas **i ponad lokate ludu**; 5% zaworu -> `Earn(MerchantProfit)` przed podzialem pan / korona.
- `MoneyLedger.cs`: nowi posiadacze "kiesy ludu (miasta / podzamcza)", "gminy", "lokaty ludu" w "Pieniadz swiata" i w sumie zlota swiata (:641 - jedna definicja);
  w "Przeplywy osad (kasy miast)" pozycje "lud za towar +", "uslugi do ludu -", "robocizna handlu -", "obrot P -", "place rzemiosla -", "nadwyzka ludu (towar) +",
  "czynsz notabli"; nowe migawki przenumerowac po 169 i 111' (PROJEKT 12 uwaga o `Marks`).
- `ArmouryBehavior.cs`: `TownFolk.Reset()` w konstruktorze (:389); w `SyncData` klucz `arm_townfolk` przez `SaveText.Sync` we wlasnym `try` (ok. 800 osad x kilka liczb =
  ok. 35 KB - ponad 32 KB, dlatego tylko `SaveText.Sync`, ktory tnie na kawalki, paczka 161); w `OnDailyTick` (:1232-1237): `MoneyLedger.BlockOpen` ->
  `TownPurse.BeforeRents` -> `PopulationLaw.Daily` -> `TownPurse.Daily` -> `CastlePurse.Daily` -> **`TownFolk.Daily`** -> `BuildFunding.Daily`.
- `SubModuleMain.cs`: `TownFolk.ApplyAll` (postfiks `CalculateBudget` albo prefiks konsumpcji) po `TownPurse.ApplyAll`, wlasny `try`, licznik wpiecia w logu startu.
- `Settings.cs` + `McmSettings.cs` (`python tools/gen_mcm.py`): grupa "The townsfolk's purse": `TownFolkPurseEnabled` = true; `TownFolkServiceShare` 0.25;
  `TownFolkCourtServiceShare` 0.20; `TownFolkLabourFood` 0.20, `TownFolkLabourGoods` 0.10, `TownFolkLabourArms` 0.04; `TownFolkCraftWageShare` 0.35;
  `TownFolkMerchantDraw` 0.03; `TownFolkMerchantProfitShare` 0.05; `TownFolkCastleProfitShare` 0.15; `TownFolkExcise` 0.02; `TownFolkReserveDays` 10
  (R 21, P 30, podzamcze 10, wies 15 - stale w kodzie); `TownFolkAnchorBasket` = true; `TownFolkSlaveRealms` (lista); `VillagePurchasePerHead` 0.07;
  `VillageDuesPerHeadYear` 4.0; `TownFolkHungerHurts` = true; `TempleTithe` = false (do odpowiedzi 7.2). Opisy po angielsku.
- `CHANGELOG.md`: wpis 172 wedlug wzoru, Status: NIEWGRANE - DO SPRAWDZENIA.

### 5.3 Zapis i nowa kampania

- **Nowa kampania - tak, razem z grupa C** (110/111 przycinaja dar startowy ok. 12 mln). **Na starcie kiesy ludu dostaja pelne zapasy z przycinanego daru
  startowego miast** (zamiast kasowac te czesc; kasa kupcow nietknieta): wyrobnicy 10 dni, rzemieslnicy 21, patrycjat 30, podzamcze 10, wsie do 15 dni zakupow -
  razem ok. 6.7 mln [R]. Gdy dar w danym miescie jest mniejszy niz zapas - reszta z kasy kupcow ponad zapas kupcow, najwyzej polowa nadwyzki; licznik w logu.
  Rachunek: bez przelewu 1. rok to fala biedy w 20-27 krolestwach (4.4).
- **Stary zapis** (gdyby 172 wlaczyc w trwajacej grze): kiesy ludu z kasy kupcow ponad zapas kupcow, najwyzej polowa nadwyzki i najwyzej pelne zapasy dni;
  kara glodu dopiero po 120 dobach od wlaczenia. Wylaczenie 172 w trakcie: kiesy ludu, gmina i lokata wracaja do kas osad (przelew, nie kasowanie).
- Klucz `arm_townfolk` czyszczony w `Reset()` przy kazdej nowej grze i wczytaniu.

### 5.4 Test (autotest; Jeff: wgranie tylko po "wgraj")

40 dob + rok, nowa kampania. Linie co dobe:
- "Kiesa ludu: dzien N | miasta: kiesa X (W / R / P), dochod Y wedlug pochodzenia (wojsko / dwor / rzemioslo i budowy / handel / karawany / zysk kupcow /
  wewnatrz ludu), wydatki Z (towar, czynsz, podatki), stac jedzenie / pelny W / R / P %, miast ze stac < 95%: n (jedzenie / pelny), zjedzone bez zaplaty A zl,
  kotwica m (srednia, min) | podzamcze ... | wsie: zakupy B z C (%), nadwyzka kupiona D z E, oplaty panu F, danina G | gmina | lokata".
- "Podstawa robocizny: z czego" (towar ludu, zolnierze i zalogi, dwor, sprzet, wsie, karawany, notable, zamki, nadwyzki) i trend "stac" i ludnosci BK co 30 dob.

Progi: stac na jedzenie >= 95% w kazdym miescie kazdej doby; stac pelny >= 98% swiat i miast < 95% najwyzej 5%; zjedzone bez zaplaty < 1% konsumpcji;
podstawa robocizny >= 90% wartosci z rachunku dla tej samej fazy; nadwyzka kupiona >= 90% chcianej; zakupy wsi >= 90%; lokata ludu < 5 dni wydatkow
(srednio); reszta zlota swiata < 10 tys./d (Z9); zakup plonu >= 98%; wojsko w wojnie 85-100 tys.; "Pieniadz swiata" z nowymi posiadaczami bez skoku;
bilans TOWARY: polki soli, sukna, plotna, narzedzi i garnkow nie ponizej 5 sztuk w wiecej niz 10% miast.

---

## 6. Ryzyka (kontrola wedlug zasady 0 CLAUDE.md)

1. **Szczelnosc.** Obieg z kiesa ludu ma wiecej wezlow i jest czulszy na dziury: 25 tys./d dziury to po 4 latach 78 zamiast 85 tys. wojska i bieda wyrobnikow w pokoju
   w 21 krolestwach (4.4). 172 tylko po 164a i przy reszcie swiata < 10 tys./d (Z9); do tego czasu dosypka trybu 1 (111') zostaje.
2. **Mniejsze wojsko AI** (-5% wojna, -10% pokoj) - pytanie 7.3. Lagodzi je pulap sprzetu (166, +0.5 tys.).
3. **Zadowolenie BK odwrocone:** przy obnizonym popycie BK uznaje, ze towaru nie brakuje, i PODNOSI zadowolenie (+0.001) - stad nasza kara za jedzenie (5.2),
   wylacznik osobny, pytanie 7.1.
4. **Latka na konsumpcji:** postfiks na prywatnej metodzie BK `CalculateBudget` - sprawdzic nazwe i sygnature na prawdziwym DLL BK (BKROTPatch moze ja zmieniac);
   zapasowo prefiks na `MakeConsumption` z `Priority.First` (rozpoznanie 169-2 "prefiks moze nie pobiec").
5. **Druga konsumpcja BetterEconomy:** BEE ma wlasne profile konsumpcji klas (`better_economy_class_consumption_profiles.xml`, sol jako luksus - 09 L6). Jesli BEE zdejmuje
   towar i placi kasie miasta niezaleznie od BK, lud musi placic i za to albo BEE trzeba wylaczyc - sprawdzic w paczce 170.
6. **Towar:** popyt przesuwa sie z broni na jedzenie, piwo, sukno, sol, zelazo i garnki (4.6) - mozliwe puste polki (konkurencja z `ArmyClothing` 150). Gdy towaru brak,
   pieniadz stoi w lokacie i obieg sie zweza (polki 50%: -2.8 tys. wojska). Pomiar w logu i prog w tescie (5.4).
7. **Wzrost ludnosci BK** (krytyka S7): kotwica trzyma koszyk miasta na budzecie gry, ale przy wzroscie ludnosci o 2% rocznie podzamcze w 1-2 krolestwach
   schodzi do 90-94%, przy 3% wyrobnicy do 90%, przy 5-10% wyrobnicy do 73-84%, a podzamcze do 54-82% pelnego koszyka; gdy rosnie tez dobrobyt - 22-24 krolestwa
   ponizej 95%. Zamkniety obieg z wieksza liczba ludzi i ta sama moneta to mniej na glowe (jak Anglia ok. 1300). Pomiar A przed 172; gdy ludnosc BK rosnie trwale
   > 2% rocznie - osobny projekt (doplyw monety z kopaln srebra przez mennice albo zatrzymanie wzrostu hearth z BEE).
8. **Liczby ze wzoru i szacunku, nie z pomiaru:** ludnosc BK, wartosc wyrobow (250 tys.; przy 150 tys. wyrobnicy w 1-4 krolestwach 84-86%), karawany (pomiar 40 dob),
   budzet konsumpcji zamkow (61 -> 92 tys.).
9. **Miasto-zaulek** (4.7): pojedyncze miasta moga byc biedniejsze niz srednia krolestwa - w logu liczba miast ze "stac" < 95%.
10. **Rody samych zamkow w falach** wojny: 10-15 ze 119 zmienia kiese o > 10% w 28 dob (PROJEKT 2-3) - patrzec w "Budzet rodow" po wejsciu 166.
11. **Gracz:** mniejsze "od razu" z miast (25% uslug zolnierzy i 5% zaworu u ludzi, stale oplaty wsi), wiecej z podatkow ludu i farmy gminy; zwrot z monety 0.53-0.62.
    Dwor graczowi nie jest naliczany (decyzja 08.10) - jego miasto ma mniej uslug dworu.
12. **Kolizje z paczkami w toku:** 169 (`MoneyLedger` - nowi posiadacze, migawki, hak `GiveGoldAction` dla karawan i gracza), 170 (BEE - punkt 5), 171 (zaloga kupuje
    w miescie - NoteSale), H3 przegrani uchodza (bez styku), T1-T8 (bez styku). Kasy osad pisza tez: BK `HandleMarketGold` (111' zamyka galaz), notable doplacajacy
    pustej kasie, inwestycje BEE gracza, `LevyGold` - przejrzec przed 172, ktore z nich to towar, a ktore usluga.
13. **Martwy kod:** tarcza zoldu (107b) zostaje nieczynna (K6); `PopulationRentMaxShare` traci znaczenie przy 172 (zostaje dla wylaczonego 172).
14. **Pulapka MCM:** nowe klucze nie istnieja w `Armoury.json` Jeffa - domyslne zadzialaja; sprawdzic przed wgraniem (CLAUDE.md 7).
15. **Wydajnosc:** ok. 800 osad x kilka dzialan raz na dobe + jeden postfiks na budzecie konsumpcji (ok. 30 kategorii x 227 osad) - pomijalne wobec 20 s na dobe
    (optymalizacja i tak na koniec, decyzja 08.10).

---

## 7. Pytania do Jeffa (tylko to, co zmienia gre)

1. **Glod ma skutki:** gdy ludzi miasta nie stac na jedzenie przez tydzien (oblezenie, odciete miasto, dziura w obiegu, drozyzna), spada zadowolenie Banner Kings,
   a za nim dobrobyt i lojalnosc. Brak pieniedzy na sukno i zbytki tylko zmniejsza zakupy. Nie w pierwszych 120 dobach kampanii. Przy proponowanych liczbach
   w zwyklej grze jedzenia starcza wszedzie, kazdej doby (rozdz. 4). Wlaczyc? **Rekomendacja: tak** - przyczyna i skutek, wylacznik osobny.
2. **Septy i dziesiecina** (5% utargu wsi i 1-3% dochodu ludzi do kaplana; polowa wraca jako jalmuzna dla biednych, polowa na budowe septow). To kanon (Wiara bogata,
   korona winna jej prawie milion smokow), ale bierze troche panom i daje -0.4 tys. wojska. Wprowadzic? **Rekomendacja: tak, po roku autotestu 172** (00 6.2-3).
3. **Mniejsze wojsko AI:** z kiesa ludu ok. 90 zamiast 95 tys. w wojnie i ok. 33 zamiast 37 tys. w pokoju (ok. 12 mln zlota u ludzi i we wsiach zamiast u panow).
   Zgoda? **Rekomendacja: tak** - miesci sie w Twoich progach (85-100 tys., pokoj 35-40% wojny); jesli nie - pulap sprzetu panow z paczki 166 (+0.5 tys.).

Bez pytania (projekt ekonomii, uzasadnienie w tabelach): stawki podatkow ludu i akcyzy, udzial uslug 25 / 20 / 75%, robocizna 20 / 10 / 4%, place rzemiosla 35%,
obrot kupcow 3%, zysk 5%, zapasy 10 / 21 / 30 / 10 / 15 dni, koszyk na glowe, zakupy wsi 0.07 zl na glowe, oplaty wsi 4 zl rocznie na glowe, kraje niewolnicze
wedlug ksiazek (Qohor i Lorath - jak w 00 R14), kolejnosc paczek. Do wiadomosci: pan bierze ok. 2/3 gotowki wsi (historycznie 30-50%) - to stawka BK 70%
utargu, ktorej nie ruszam, bo z niej zyje duze wojsko.

---

## 8. Pliki i jak powtorzyc rachunek

Katalog: `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\7016f733-d379-418e-b700-f66fd52e4d2b\scratchpad\kiesa\`
- `sim2\sim_kiesa2.py` - rachunek wersji 2 (`python sim_kiesa2.py baza` - trzy scenariusze; `projekt` - bez kiesy ludu; wariant: `python sim_kiesa2.py "scen='pokoj',s_L=0.0"`);
  `sim2\sim_krytyk_kopia.py` - kopia rachunku krytyka (zmiany: dane wejsciowe z `sim2\dane\`).
- `sim2\dane\` - dane zamrozone (rotacja logow Armoury kasuje stare logi): `skarbce-108876.json` (z `Armoury-2026-10-08_19-33-14.log`), `ludzie-regiony-108836.csv`
  (z `Logs\2026-10-08_19-33-14`), `clans2.json` (kopia `fa2fd7a6-...\b2`), `zamki-rodow.json` (z `economy-2026-10-08_06-53-20.csv`).
- `sim2\przeglad2.py` - wariant na trzy scenariusze z tabela krolestw ponizej 95% KAZDEJ doby (`SCEN=pokoj python przeglad2.py "H=2912"`); `sim2\warianty.sh` -
  wszystkie warianty 4.4 rownolegle do `sim2\wyn\*.txt`; `sim2\zbior.py` - zestawienie (`sim2\zestawienie.txt`).
- `sim2\raport.py` (-> `sim2\wynik-baza2.txt`: migawki, przeplywy, dochody na glowe, wies, zbiorniki r3 -> r4), `sim2\zrodla.py` (-> `wyn-zrodla.txt`: dochod ludu
  wedlug pochodzenia), `sim2\zamki.py` (-> `wyn-zamki.txt`: rody samych zamkow), `sim2\diag.py` (dochod / potrzeba wedlug krolestw i klas), `sim2\flows.py`,
  `sim2\gracz_kiesa2.py` (-> `wyn-gracz2.txt`: Z8).
- `sim2\wyn-projekt-*8.txt` (PROJEKT 8 lat), `sim2\wyn\projekt-dziura10.txt` i `projekt-dziura25.txt` (PROJEKT z dziura - porownanie w 4.4).
- Wersja 1 (do porownania): `sim\sim_kiesa.py`, `sim\wynik-baza.txt`, `sim\wrazliwosc.txt` - wyniki zostaja, ale programu nie da sie juz uruchomic (czyta skasowany log
  17-59-09); rachunki krytykow: `krytyk1\` (k1-wyniki.txt, bk_popyt.py), `krytyk2\` (sim_k2.py, k2*.txt).
- Pomiary z logow (17-59-09 skasowany przez rotacje po odczycie): `Armoury-2026-10-08_17-59-09.log` (model czynny l. 186 i 366, "zakupy" mieszkancow, karawany,
  nadwyzka ceny wyrobow) i `Armoury-2026-10-08_19-33-14.log` (drugi przebieg 40 dob: karawany kupuja 218 / sprzedaja 193 tys./d, nadwyzka ceny wyrobow 89 tys./d;
  w pierwszym 216 / 182 i 96 tys./d).

Zrodla historyczne i lore - jak w 01 rozdz. 6 i 09 rozdz. 7 (Campbell 2005/2008, Clark, Dyer, Phelps Brown-Hopkins, Jusserand, rachunek dworu Lancastra
https://sites.uwm.edu/carlin/household-expenses-of-thomas-earl-of-lancaster-30-sept-1313-29-sept-1314-7-8-edw-ii/ , murage https://gatehouse-gazetteer.info/murage/muressay.html ,
multure https://en.wikipedia.org/wiki/Thirlage , Wiara Siedmiu https://awoiaf.westeros.org/index.php/A_Feast_for_Crows-Chapter_28 , niewolnictwo
https://awoiaf.westeros.org/index.php/Slavery i https://awoiaf.westeros.org/index.php/Volantis ). Bez otwartego zrodla [H?]: sluzba w miastach (Goldberg, poll tax
1377-81), zakupy gospodarstwa chlopskiego 8-10 s rocznie (Dyer), Assize of Bread (udzial piekarza i mlynarza), praca w suknie 35-50% kosztu (Munro), gabelle
Florencji 1338 (Villani XII.92) - oznaczone przy liczbach.
Kod sprawdzony w dekompilacji: gra `DefaultSettlementEconomyModel.GetDailyDemandForCategory` (:61-73), `ItemConsumptionBehavior.UpdateDemandShift` (popyt z czynnego modelu),
`DefaultTradeItemPriceFactorModel.GetTradePenalty` (:32, 6%); BK `PopulationManager.GetDesiredTotalPop`, `GetDesiredPopTypes` (bkall), `BKGrowthModel.CalculateEffect`
(wzrost do pulapu osady, glod -200%), `BKEconomyModel.GetDailyDemandForCategory` i `CalculatePopulationConsumptionDemand` (`ore-supply\bk\BannerKings.Models.Vanilla\BKEconomyModel.cs:569-630`
- w grze nieczynny), `ItemConsumptionPatch.Prefix` i `CalculateBudget` (`ore-supply\bk\BannerKings.Patches\EconomyPatches.cs:578-640, 1273-1289`).

---

## 9. Krytyka i odpowiedzi (09.10)

Dwa przebiegi krytyki: H = historia i ekonomia (12 uwag, `krytyk1\`), S = szczelnosc i rachunek (13 uwag, `krytyk2\`). Kazda sprawdzona w kodzie, logu albo rachunku;
rachunek policzony od nowa w calosci (`sim2\`, rozdz. 4). Wynik: 21 przyjetych w calosci, 4 w czesci, 0 odrzuconych w calosci.

| # | Uwaga (skrot) | Waga | Werdykt | Co zmienione / dlaczego |
|---|---|---|---|---|
| H1 | Koszyk nie na glowe: w grze czynny model vanilla (popyt z dobrobytu, bez ludzi); "0.343 zl = historyczne jedzenie" to zbieg; koszyk rosnie z dobrobytem (347.6 -> 520.8 tys. w rok), a rachunek trzymal 470 | krytyczne | **Przyjeta** (sprawdzone: log 17-59-09 l. 186 i 366, `DefaultSettlementEconomyModel.cs:61-73`, `ItemConsumptionBehavior.UpdateDemandShift`) | L3: kotwica min(budzet gry, ludzie x koszyk) na postfiksie `CalculateBudget`; budzet gry w rachunku 347.6 -> 520.8 tys. wedlug dobrobytu krolestw; koszyk z historii, nie z BK (2.1); usuniete zdanie o 0.343 i argument L8 "BK nie liczy niewolnych"; indeks cen jako wariant (4.4); pomiar A w 169 (5.1); bilans TOWARY w tescie (5.4, 4.6) |
| H2 | Nadwyzka ludu to pieniadz bez towaru (177 tys./d w wojnie), tak samo czynsz notabli i "praca u bogatszych"; wyrobnik w wojnie 2.2 zl na dzien roboczy (+60%), tabela porownywala wydatki | wazne | **Przyjeta** | L4: nadwyzka W kupuje towar z polek (NoteSale), bez towaru lokata z pulapem 30 dni; notable kupuja towar; R i P: 70% place, 30% material; 2.1 z dochodem na zarabiajacego na dzien roboczy: 1.71 (wojna, +14% - jawnie [S]) i 1.52 (pokoj) wobec 1.5 d |
| H3 | Wies dalej oddaje panu ok. 80% gotowki; regula 7%/d ponad 15 dni = 100% podatek od nadwyzki; "wpisowe, heriot, merchet" to oplaty jednorazowe; 89 tys./d = 7.5x historii | wazne | **Przyjeta** | 1.4 i 5.2: oplaty stale 4 zl na wolna glowe rocznie (11 tys./d); nadwyzka wsi kupuje towar w miescie; udzial pana 62% (wojna) / 68% (pokoj) zamiast 79-81% - jawnie powiedziane Jeffowi, ze reszta ponad 30-50% to stawka BK 70% (rozdz. 0 pkt 6, rozdz. 7); koszt: -1.7 tys. wojska wobec reguly wersji 1 (4.4) |
| H4 | Robocizna handlu ma zla podstawe (sprzet panow, wydatki notabli) i zle uzasadnienie ("marza 40%", "44% ceny chleba") | wazne | **Przyjeta** (sprawdzone: kara 6%, `DefaultTradeItemPriceFactorModel.cs:32`) | L5 przepisane: stawki wedlug rodzaju towaru 20 / 10 / 4%, uzasadnienie z kary handlowej i Assize of Bread; robocizna 296 tys./d = 13.9% sprzedazy (wersja 1: 20% od wszystkiego); zdanie "20% to minimum" usuniete; wariant 15 / 8% w 4.4 |
| H5 | Polowa dochodu ludu miast z wojska, rzemioslo tylko 3.5%; L9 przemilcza, ze dochod ludu tez stoi na duzym wojsku | wazne | **Przyjeta** | L7: place rzemiosla = 35% wartosci wyrobow (ok. 250 tys./d: nadwyzka ceny wyrobow 90-96 tys. [P] + reszta [S]) - 88 tys./d zamiast 25; rozbicie dochodu ludu wedlug pochodzenia (4.6: wojsko 31% w wojnie, 14% w pokoju; rzemioslo i budowy 13 / 24%); L9 dopisane uczciwie; linia logu 172 z tym rozbiciem (5.4) |
| H6 | Jeden rozklad niewolnych dla swiata przeczy lore (Westeros zakazuje, Volantis 5:1, Braavos 0); ich jedzenie placa wolni | wazne | **Przyjeta** | L8 i 1.2: niewolni tylko w 8 krolestwach (lista), utrzymanie i dochod z ich pracy u wlascicieli (R 40 / P 60); w Westeros i wolnych miastach to biedota z kiesa W (+212 tys. ludzi); Qohor i Lorath wolne do rozstrzygniecia 00 R14; lista w MCM |
| H7 | Czynsz: chata 480 zl to 11.7 roku czynszu, nie "4-5 lat" - wniosek odwrotny | drobne | **Przyjeta** | 2.3: 13 lat czynszu - w normie 10-15 lat; rzemieslnik 13% wydatkow (polowa majstrow we wlasnym domu) |
| H8 | Pomylone jednostki: doba kalendarzowa i dzien roboczy; "1.5-4 razy, jak historycznie" myli jednostki | drobne | **Przyjeta** | jednostka zl na dobe kalendarzowa w calym dokumencie, przelicznik 250/364; 2.3 przeliczone (piechur 1.7-1.9 raza wyrobnik, historycznie 1.9) |
| H9 | Uslugi zolnierzy liczone podwojnie (40% + robocizna 20% od reszty = 52%); u Jusseranda lozka to 13% | drobne | **Czesciowo** | Uslugi 25% (lozko 13% + marza gospody 10% + pranie 2%) - przyjete. Robocizna 20% od jedzenia zolnierzy zostaje, bo to praca mlynarza, piekarza, piwowara i woznicy w cenie z polki, a nie kuchni gospody (ta jest w 25%) |
| H10 | Podatki ludu nie po sredniowieczu: miasto bralo od spozycia i ruchu towaru; gmina wplaca do kasy kupcow (dar); czynsz chaty i multure w 2% pana dubluja | drobne | **Przyjeta** | L2 i 1.4: akcyza 2% przy zakupie do osobnej gminy (1/3 farma panu, 2/3 roboty: place i material); pan od ludu tylko kary i oplaty targowe (1 / 2 / 3%); multure zostaje w cenie chleba |
| H11 | Cytat z Goldberga przekrecony (1/5-1/3 doroslych w poll tax 1377-81, nie "20-30% gospodarstw"); sluzba dostawala glownie wikt - 15% gotowka to za hojnie | drobne | **Przyjeta** | 1.3: cytat poprawiony [H?]; sluzba 6% gotowka, wikt w koszyku towaru P |
| H12 | Patrycjat dwa razy: czynsz do notabli-bohaterow, a klasa P bez czynszu | drobne | **Przyjeta** | czynsz 50% notable, 50% przegroda P (1.3, 5.2) |
| S1 | Pierwszy rok nowej kampanii to fala biedy prawie wszedzie (P < 95% w 24 z 29 krolestw, Skagos 38%); przelew startowy z kasy kupcow wylacza zawor; limit 0.5 T/3 daje W 3.4 dnia | krytyczne | **Przyjeta** (odtworzone w `sim2`: wariant "start jak wersja 1") | Start z przycinanego daru startowego (6.72 mln, pelne zapasy dni), kasa kupcow nietknieta; dochod P z obrotu dziala od 1. doby (L6); kara glodu tylko za jedzenie, po 7 dobach i nie w pierwszych 120 dobach (5.2); pomiar kazdej doby, kazdego krolestwa, 5 klas, jedzenie osobno: zestaw koncowy - wszystkie >= 95% od doby 1 (4.2) |
| S2 | Podstawa robocizny w rachunku (wszystkie wplywy kupcow) nie zgadza sie z lista NoteSale w specyfikacji; brakuje PayForGoods, notabli, zamkow; karawan i gracza nie ma w rachunku | krytyczne | **Przyjeta** | 5.2: lista NoteSale dokladnie jak w rachunku (`sale_towns` i wplywy lud/zamek/wies/notable/gmina); karawany wlaczone (pomiar 217 / 185 tys./d) i przeliczone; gracz w NoteSale z haka `GiveGoldAction` (w rachunku pominiety - maly); linia "Podstawa robocizny: z czego" z progiem >= 90% (5.4) |
| S3 | W pokoju patrycjat malych krolestw ma trwaly deficyt (Zelazne Wyspy przez caly rok 4); pokoj nie jest ustalony (P +27% r3 -> r4); s_L 0.15 to minimum swiata | wazne | **Czesciowo** | Diagnoza przyjeta; zamiast s_L 0.20 dochod P = 3% obrotu + 5% zaworu (L6) - dziala od startu i w kazdym krolestwie. s_L 0.20 sprawdzone: tez przechodzi, ale -0.4 tys. wojska i patrycjat ma 2.3-2.7 raza potrzeby (baza 1.7-2.2, nadwyzka idzie na place wyrobnikow). 8 lat pokoju: P stoi od roku 2 (r3 -> r4: -2% w najgorszym krolestwie), minimum P i R drukowane dla kazdego krolestwa |
| S4 | Afford liczy sie sam z siebie (mianownik po skalowaniu) - ok. 20% towaru zjedzone za darmo; jedna pula W+R+P, a PayForGoods stale 75/14/11% | wazne | **Przyjeta** | 5.2: mianownik = pelna potrzeba z kotwicy; Afford jak PayForGoods: suma udzialow klas x min(1, kiesa / potrzeba), osobno jedzenie i reszta; licznik "zjedzone bez zaplaty" z progiem < 1% |
| S5 | Nadwyzka wyrobnikow idzie do kupcow bez towaru (30% dochodu W w wojnie); "obciazenie 3-4%" wprowadza w blad | wazne | **Przyjeta** (jak H2) | wybrana droga (a) prawdziwy zakup z NoteSale + (b) lokata tylko przy braku towaru, z pulapem i zwrotem; 1.4 podaje realne obciazenie (ok. 9% z czynszem) |
| S6 | Jedyny pomiar z roku to 521 / 92 tys./d; przy nim S9 nie jest spelnione; "model wytrzymuje wiekszy koszyk" bledne | wazne | **Czesciowo** | Miasta: przyjete - z kotwica budzet gry dochodzi do 520.8 tys. w dobie 364 i to jest baza (m = 0.97); zdanie usuniete; koszyk +10% i +20% w 4.4. Zamki: 92 tys. nie, bo kotwica liczy koszyk podzamcza (0.18 zl na glowe, wikt z kuchni pana placi dwor) - zamki zjadaja ok. 58 tys./d |
| S7 | Gdy ludnosc BK rosnie, dochod ludu za nia nie idzie (+10% rocznie: W < 95% w 28 krolestwach) | wazne | **Czesciowo** | Kotwica (L3) trzyma koszyk miasta na budzecie gry, a wariant "wiecej rak" podnosi plon i produkcje z ludnoscia; przy +2% rocznie podzamcze 90-94% w 1-2 krolestwach, przy +3% wyrobnicy 90%, przy +10% wyrobnicy 73-85%, podzamcze 54-73% (4.4) - nie rozwiazane w calosci, bo w zamknietym obiegu wiecej ludzi przy tej samej monecie to mniej na glowe (Malthus, Anglia ok. 1300). Pomiar A przed 172, trend w logu, ryzyko 7 z warunkiem i kierunkiem (moneta z kopaln przez mennice) |
| S8 | Wagi koszyka klas (P 5.0, R 1.7) niewyprowadzone; przy P 8 i R 2.4 patrycjat ponizej 95% w 26-28 krolestwach | wazne | **Przyjeta** | Koszyk z historii (rodzina W 2.0 L, R 3.9 L, P 10.6 L; 2.1) [H/S]; wariant kupiec 16 L i rzemieslnik 5 L w 4.4 - przechodzi |
| S9 | Rody traca 14-15 mln, nie 11 | drobne | **Przyjeta** | 4.3 i 0: teraz -8.9 mln (10%); pelny rozklad: lud miast 10.7, podzamcza 1.1, wsie +1.5, kupcy +1.7, notable 0.3, gmina 0.2; 6.7 mln z daru startowego |
| S10 | Robocizna od sprzetu AI (85 tys./d) liczy drugi raz prace oplacona w WorkshopLaw | drobne | **Przyjeta** (jak H4) | bron, zbroje, konie 4% (przewoz i sprzedaz) |
| S11 | Zakupy wsi liczone na wszystkich, z niewolnymi; zakupy o polowe mniejsze psuja patrycjat | drobne | **Przyjeta** | zakupy na wolnych (1.00 mln); wariant zakupow o polowe mniejszych przechodzi (4.4); log "zakupy wsi kupione / chciane" z progiem |
| S12 | Budzet BK ma czlon niezalezny od popytu (dobrobyt/1000) i dokupuje jedzenie przy zapasie < 10% - mnozenie categoryDemand go nie zmniejsza | drobne | **Przyjeta** (sprawdzone `EconomyPatches.cs:1273-1289, 624-631`) | latka na `CalculateBudget` (oba czlony); jedzenie przy glodzie liczone jako "zjedzone bez zaplaty" |
| S13 | Brak odbiorcy zapasowego (rod bez krolestwa, miasto bez notabli, brak kaplana, Leader == null) | drobne | **Przyjeta** | 5.2: tabela odbiorcow zapasowych i licznik w logu |

**Co zostalo otwarte po krytyce:** wzrost ludnosci BK (S7, ryzyko 7) - rozstrzyga pomiar A; wartosc wyrobow i karawany - pomiar w 169; rody samych zamkow w falach
(10-15 ze 119) - do 166; Qohor i Lorath - 00 R14.
