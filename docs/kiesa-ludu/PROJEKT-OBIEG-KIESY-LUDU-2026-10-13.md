# PROJEKT: OBIEG KIESY LUDU - sedzia i synteza trzech kierunkow (13.10.2026)

Status: PRACA PROJEKTOWA. Kodu, gry, ustawien i zapisow nie ruszano, nic nie budowano, bez commita. Ten plik ocenia trzy niezalezne projekty
odpowiedzi na pytanie Jeffa z 13.10 ("co zrobic z kasa, ktora lezy; w prawdziwej ekonomii pieniadz cyrkuluje i ma zamkniety obieg - ludzie moga
oszczedzac, ale tez wydaja, inwestuja itp. Jak to zrobic w ramach silnika gry, aby mialo to sens?") i sklada z nich JEDEN projekt.

**Zrodla.** ROZP = `ROZPOZNANIE-OBIEG-KIESY-2026-10-13.md`, A = `PROJEKT-WYDATKI-2026-10-13.md`, B = `PROJEKT-INWESTYCJE-2026-10-13.md`,
C = `PROJEKT-POZYCZKI-2026-10-13.md` (wszystkie w `docs\kiesa-ludu\`). LOG = `D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_02-15-21.log`
(bieg roczny 364 doby, WGRANIE 36; `:NNN` = numer linii). LOGK = `...\noc34-35\Armoury-2026-10-13_04-46-00.log` (bieg 120 dob z regula koni roboczych
K1-3a). CSV = `...\noc34-35\Logs\2026-10-13_02-15-21\` (kiesa-ludu.csv, budzet-rodow.csv, kasy-miast.csv). SRC = kod WGRANIA 36
(`...\ae222cb7-...\scratchpad\zb\int\Armoury\src`; numery linii z tej kopii - w `Settings.cs` sa o 0-45 linii wyzsze niz u autorow A/B/C, tresc ta sama).
G = dekompilacja gry, BK = dekompilacja BannerKings. TRIAZ = `docs\test-zbiorczy\TRIAZ-3-bieg-roczny-2026-10-13.md`. SP = `docs\STAN-PRAC.md`.
E5 = `docs\etap5\PROJEKT-ETAP5-KIESA-LUDU-KASY-MIAST-2026-10-11.md`. PLAN = `docs\PLAN-DO-KONCA-MODA-2026-10-09.md`.
Skrypty sedziego (do powtorzenia kazdej liczby): `...\dbc3d3d1-...\scratchpad\kiesa\sedzia\v1.py` - `v10.py`.
**Oznaczenia:** [P] = pomiar z logu albo csv, [S] = rachunek (nie bieg). Kwoty w zl na dobe calego swiata, 1 zl = 1 pens, rok = 364 doby.
Bieg roczny mierzyl gospodarke WOJENNA (przez wiekszosc roku 25-28 krolestw z ok. 30 w wojnie - LOG, linie "Korona: podatek wojenny ludu (184)"); pokoju nie zmierzyl.

---

## 0. Dla Jeffa - prostym jezykiem

1. Dzis mieszczanin ma dla pieniedzy jedno wyjscie - towar z polki - a z polki bierze juz wszystko, co targ ma; zarabia na wojsku i dworach wiecej, niz targ ma dla niego towaru, wiec w rok odlozyl 30 mln i pod koniec roku dalej odklada 40-50 tys. zl dziennie.
2. Proponujemy jedna regule: mieszczanin robi z pieniedzmi po kolei to, co robil w sredniowieczu.
3. Najpierw chleb i zwykle zakupy, potem zapas na czarna godzine (mniej wiecej miesiac zwyklych zakupow) - to jego oszczednosci i nikt ich nie rusza.
4. Z tego, co ma ponad zapas, najpierw zyje lepiej: kupuje sukno, srebro, klejnoty i lepszy stol z polki swojego miasta, po cenie targu.
5. Potem doklada do budowy swojego miasta - murow, spichlerza, portu: kupuje drewno i kamien z targu i placi murarzom.
6. Na koncu, tylko w czasie wojny, oddaje koronie czterdziesta czesc dziennie z tego, co mu zostalo, a korona tego samego dnia placi tym zlotem rodom za sluzbe; czego nikt nie wzial, lezy dalej jako oszczednosc.
7. Ta danina to nie nowe prawo: kupcy i wsie placa ja od 04.10, a 09.10 zgodziles sie na podatek wojenny od ludzi - tylko ze dzis lud placi go od zarobku (13 tys. zl dziennie), a jego skrzynie zostaly poza poborem.
8. Sprawdzilismy trzy pomysly osobno i zaden sam nie wystarcza: zbytkow swiat wytwarza tyle, ze lud wyda na nie najwyzej ok. 60 tys. zl dziennie wiecej (i to niepewne, bo zalezy od karawan); budowa wezmie 10-20 tys., z czego trzy czwarte wraca do miasta jako place; pozyczka dla pana niczego nie zamyka, bo odsetki plyna do ludu.
9. Dlatego skladamy wszystko, ale wdrazamy od konca: najpierw danina (maly i pewny krok, zamyka obieg od razu), potem budowa, na koncu zbytki (najwieksza zmiana - rusza ceny i karawany, wejdzie dopiero po pomiarze).
10. Co sie zmieni: oszczednosci ponad zapas spadna z 24 mln do ok. 3 mln, skrzynie mieszczan stana na ok. 15-16 mln zamiast 36.5 mln i przestana rosnac; zapas na czarna godzine (ok. 13 mln) zostaje caly.
11. Z kazdych 10 zl, ktore mieszczanin ruszy z oszczednosci w wojnie, ok. 6 zl wezmie korona, 2-3 zl pojdzie na lepsze zycie, 1.5 zl na budowe; w pokoju 8.5 zl na zycie, 1.5 zl na budowe i nic koronie.
12. Do panow wroci ok. 50-65 tys. zl dziennie.
13. Co zobaczysz: w oknie mieszkancow zdanie, ile lud ma oszczednosci i na co je wydal; bogate miasta buduja sie same - w Twoim miescie mieszczanie oplaca budowe, ktora ustawisz na czele kolejki; zbytki drozeja tam, gdzie lud ma pieniadze, wiec oplaca sie kupic klejnoty u zlotnika i zawiezc je do bogatego miasta; w wojnie Twoje miasta placa koronie.
14. Ile wojska: uczciwie malo - ok. 1.5-2.5 tys. ludzi wiecej (najwyzej 6-7 tys.), a ubylo 31 tys.
15. Powod: dwa rody na trzy maja pieniadze i niepelne druzyny z innych przyczyn, wiec z kazdych 3 zl, ktore wroca, tylko 1 zl trafia do pana, ktoremu naprawde brakuje na zold - sprawdzilismy, ze tak jest bez wzgledu na to, czy pieniadz idzie przez korone, czy wprost do pana miasta.
16. Wojsko odbudowuja poprawki z rundy "wojsko" (pulapka rent, kolejnosc zwolnien, kompanie najemne) - sa juz napisane i w tescie 120 dob daly 101 tys. ludzi zamiast 88 tys., bieg roczny trwa; ta paczka zamyka obieg pieniadza ludu.
17. Wazne: regula koni roboczych, ktora czeka na te paczke, podnosi nadwyzke ludu o ok. 125 tys. zl dziennie (pomiar 120 dob: 225 zamiast 101 tys. zl dziennie w czwartym miesiacu, skrzynie 32.9 mln zamiast 19.7 mln, wojsko -14.6 tys.); danina jest policzona tak, zeby to zloto wracalo do panow (oszczednosci ponad zapas stana wtedy na ok. 9 mln; bez daniny po 120 dobach bylo ich juz 17 mln i roslo) - dopiero z nia te regule mozna wgrac, co sprawdzimy biegiem 120 dob.
18. Czego nie wiemy: jak to dziala w pokoju (bieg mial wojne caly rok; dwa male krolestwa bez wojny tez odkladaly), ile zbytkow naprawde dojedzie do bogatych miast i ile zlota osiadzie u bogatych rodow (szacunek: jedna trzecia tego, co wroci) - dlatego pierwszy krok zawiera pomiary.
19. Koszt dla szybkosci gry: razem ok. 1 ms na dobe.

Pytania o kierunek - rozdz. 11.

---

## 1. Co sprawdzilem sam na danych biegu (przed ocena)

| # | Twierdzenie | Wynik sedziego | Zrodlo |
|---|---|---|---|
| 1 | Bilans kiesy ludu: wplywy 539 133 / 524 782, wydatki 449 194 / 486 462, saldo +89 940 / +38 320 (rok d2-364 / koniec d301-364) | zgodne co do zlotowki | [P] 364 linie "Kiesa ludu" (LOG :701-:129003), v1.py |
| 2 | Kiesy miast 6.44 -> 36.59 mln; lokata (kiesa W ponad zapas) 23 783 334 | zgodne; "kiesy ludu (E5/B1): miasta 36 585 403, podzamcza 3 037 346, gminy 123 749" | [P] LOG :676, :128970, :129003 |
| 3 | Przyrost LOKATY w oknach 30 dob (tys. zl/d): 141, 80, 52, -1, 72, 93, 56, 57, 71, 49, 59, 39 | zgodne; d301-364: lokata +47.4, kiesy miast +43.0, podzamcza -4.7. Czesc "zapas" (kiesy minus lokata) stoi na 12.7-13.6 mln od d240 - rosnie juz tylko lokata | [P] v1.py |
| 4 | Zloto lezy w niewielu miastach | lokate ma 48 z 97 miast; 10 miast = 51% lokaty, 30 miast = 91%; wedlug krolestw: The North 13.9%, Baratheon KL 13.5%, Riverlands 8.6%, Reach 8.5%, Vale 7.6%, Dothraki 7.1% (potrzeba W kalibrowana do sumy z logu: 0.699 x koszyk miasta) | [P/S] kiesa-ludu.csv, v2.py |
| 5 | Na pulapie zoldu jest 117 z 332 rodow (TRIAZ) | 121 z 332 (zold >= 0.95 pulapu, srednia 28 dob); zold partii 376 665 zl/d na 60 893 ludzi = 6.19 zl | [P] budzet-rodow.csv, v3.py |
| 6 | NOWE: ile daniny oddanej koronie dojdzie do rodu, ktoremu brakuje na zold | renty 180 (204 260 zl/d): do rodow na pulapie idzie 32.6%; wazone udzialem krolestw w lokacie 30.5%. W Polnocy 0% (14 z 26 rodow na pulapie, cala renta u rodow z luzem - pulapka rent z TRIAZ O1-2) | [P] v3.py |
| 7 | NOWE: a gdyby pieniadz szedl wprost do pana miasta (zawor, "trzecia") | 32.1% lokaty lezy w miastach panow na pulapie zoldu, 67.9% u panow z luzem (Tully, Bolton, Yronwood, Tyrell...); zawor miast: 41.2% do panow na pulapie (z 138 609 zl/d) | [P] kasy-miast.csv + budzet-rodow.csv, v4.py |
| 8 | Wniosek z 6 i 7 | kazda droga zamienia zloto na zold w ok. 1/3: 10 000 zl/d u panow x 0.31..0.41 x 0.60 (udzial zoldu w pulapie, Settings.cs:1034) / 6.19 zl = 0.30-0.40 tys. ludzi. Zgodne z TRIAZ (0.3-0.45). Pytanie C "koronie czy panu miasta" rozstrzyga sie samo: bez roznicy dla wojska - zostaje korona (jedna regula) | [S] z [P] |
| 9 | Danina wojenna nie siega kies ludu | petla daniny czyta tylko `SettlementComponent.Gold`: miasto ponad zapas kupcow 1%, wies 2.5% (KingdomTreasury.cs:366, :391-411; Settings.cs:951-954); kiesa ludu jest poza `Gold` (E5 :139) | [P] kod |
| 10 | Gmina placi kupcom za "material" bez zdjecia sztuki z polki (dziura z B 1.3) | potwierdzone: `town.ChangeGold((int)mat)` bez towaru (TownFolk.Live.cs:793-808, linia :805) | [P] kod |
| 11 | NOWE - blad w A: "R i P wyplacaja 70% nadwyzki jako place w nowym tempie 1/60" | place z nadwyzki R/P = 0.7 x tempo x nadwyzka (TownFolk.Live.cs:817, :824); dzis 155 716 zl/d (LOG :129003). Tempo 1/60 zamiast 0.15 obcina je jednego dnia do 155 716 x (1/60) / 0.15 = 17 302 zl/d - wyrobnicy traca ok. 138 tys. zl/d zarobku na kilka miesiecy. W zlozeniu tempo plac R/P ZOSTAJE 15% | [S] z [P] |
| 12 | Kolejnosc doby | TownFolk.Daily -> BuildFunding.Daily -> KingdomTreasury.Levies -> CrownIncome.Begin -> CrownRents.Daily -> ClanBudget.Daily (ArmouryBehavior.cs:1601, :1609, :1639, :1642, :1650, :1673) - danina pobrana w dobie ludu jest tego samego dnia w rentach | [P] kod |
| 13 | BK: karawana sprzedaje miastu najwyzej 20 x popyt targu; cena = (popyt / (0.1 x podaz + 0.04 x wartosc polki + 2))^0.6 | potwierdzone (BK BKCaravansBehavior.cs:1359-1362; G DefaultTradeItemPriceFactorModel.cs:176) | [P] dekompilacja |
| 14 | Zbytki: lud bierze juz prawie cala produkcje | klejnoty: produkcja 16-21 szt./d, mieszczanie 17-19; bawelna 45-50 / 40-43; aksamit 6 / 1-4; polki miast 1 001 klejnotow, juki karawan 753 (LOG "Towary: dzien", ostatnie doby). Dodatkowy wydatek A to glownie powrot ceny z 0.5 do ok. 0.9 wartosci, nie nowe sztuki | [P] |
| 15 | NOWE - pokoj: czy lud odklada bez wojny | dwa krolestwa bez wojny caly rok, po jednym miescie: Lotus Bay (Summer Isles) kiesa W 20 324 -> 266 629, zarobek 1 407 wobec wydatkow 620 zl/d; Ifequeveron (Yi Ti Exiles) 20 094 -> 412 095, 1 247 wobec 681. Odkladaja 45-56% zarobku. Tylko 2 miasta, odciete - wskazowka, nie dowod | [P] kiesa-ludu.csv, v6.py |
| 16 | NOWE - regula koni roboczych (K1-3a; czeka na te paczke, SP :1595-:1600) | saldo kies w oknach 30 dob: 316 / 270 / 248 / 225 tys. zl/d wobec 196 / 127 / 126 / 101 w biegu rocznym; lud placi za towar 248 zamiast 426 tys. zl/d (d92-120); kiesy w d120 32.87 mln wobec 19.73; lokata 17.25 mln wobec 8.87 | [P] LOGK :702-:42575, v7.py, v8.py |
| 17 | Wniosek z 16 | nadwyzka, ktora paczka ma zawrocic, to nie 55-80 tys. zl/d (zalozenie A, B, C), tylko do ok. 165-225 tys., jesli regula koni wejdzie. Zbytki (miejsce +72 tys. przy cenie historycznej, 150 tys. przy 1.5 - A 4.1) i budowa (9-24 tys.) tej skali nie udzwigna; udzwignie ja tylko odplyw proporcjonalny do zapasu | [S] z [P] |

Nie sprawdzalem ponownie: udzialow "dokad idzie zloty wydany na zbytki" (A 4.3), rachunku drewna (B 3.1), zdolnosci pozyczki (C 1.2) - biore je od autorow.

---

## 2. Ocena trzech projektow

### 2.1 Piec kryteriow

| Kryterium | A: lud wydaje wedlug zamoznosci | B: lud buduje miasto | C: danina z oszczednosci (K1) i pozyczka miejska (K3) |
|---|---|---|---|
| 1. Zasady Jeffa | Najczystszy: zadnego prawa, kazda sztuka z polki, "nie ma towaru - pieniadz zostaje". Jedyna ingerencja: cena zbytkow widzi sakiewke - naturalna | Budowa z kiesy mieszczan historyczna (mury, bruki). "Trzecia pana" od robot nie ma pokrycia w historii - sam autor: "bardziej oplata niz inwestycja" | Danina to podatek, ale historyczny (dziesiata od ruchomosci, z wolna kwota) i juz przyjety: kupcy i wsie 04.10, lud 09.10 (SP :766) - brakuje tylko podstawy "oszczednosci". Pozyczka historyczna, ale odlozona 07.10 "na slowo" (PLAN rozdz. 3) |
| 2. Czy zatrzymuje odkladanie; ile do panow | Tak, JESLI towar znajdzie >= 40% oferty (nikt tego nie mierzyl): lokata 5-9 mln, do panow ok. 30 tys. (24-36). Twarda granica: produkcja zbytkow (poz. 14). Przy nadwyzce 165-225 tys. (poz. 17) - nie zatrzyma | Nie: 9-24 tys. netto z 37-84 tys.; granica to drewno (790 wolnych ladunkow/d) | Danina: tak, z rachunku (odplyw = stawka x zapas) i w kazdej skali; do rodow 91%. Tylko w wojnie i tylko przy potrzebie korony (s > 0). Pozyczka nie zamyka nic (odsetki plyna do ludu) |
| 3. Ryzyko dla ekonomii i szybkosci | Wysokie: ceny, karawany BK, marza kupcow na towarze z karawan, blad tempa plac (poz. 11). Czas < 0.5 ms | Srednie: narzedzia dla wsi, drewno w pokoju, bogate miasta bogatsze. Czas ok. +0.5 ms | Danina: male (jeden przelew, < 0.1 ms); jedna trzecia zwroconego zlota osiada u rodow z luzem. Kara lojalnosci: nieznane (lojalnosci nie ma w zadnym logu). Pozyczka: nowy rodzaj roszczenia w zapisie |
| 4. Koszt wykonania | L: pomiar na sucho + 4 paczki | M | Danina S (+ S kara lojalnosci); pozyczka M |
| 5. Gracz zrozumie i poczuje | Najmocniej: handel zbytkami, ceny, okno mieszkancow | Widac golym okiem: miasto buduje sie samo | Zrozumie (podatek wojenny); poczuje dopiero przez lojalnosc. Pozyczka = nowa opcja w menu miasta |

### 2.2 Werdykt

1. **Zaden kierunek sam nie spelnia pieciu kryteriow.** A odpowiada na pytanie, ale jest drogi, niepewny i ma sufit w produkcji zbytkow. B jest tani i widoczny,
   ale maly. C (danina) zamyka obieg na pewno, tanio i w kazdej skali, ale niczego ludowi nie daje i dziala tylko w wojnie.
2. **Trzon: danina wojenna z oszczednosci ludu (C, regula R1).** Powody: (a) jako jedyna zamyka obieg z rachunku, nie z nadziei - zapas w rownowadze = nadwyzka / stawka;
   (b) jako jedyna dziala takze przy nadwyzce 3 x wiekszej (regula koni roboczych, poz. 16-17); (c) tor S, bez ruszania cen, towaru i zapisu; (d) to nie nowe
   prawo: zasada "wojna kosztuje poddanych, kazdy platnik od swojej podstawy" juz jest (E5 R15, :164) - kupcy i wsie placa od nadwyzki ponad potrzebe, a ludowi
   wybrano dotad sama podstawe "zarobek", ktora daje 13 tys. zl/d zamiast oczekiwanych 30-60 tys. (E5 :184); dochodzi druga podstawa tego samego platnika.
3. **Danina stoi jednak na KONCU drabiny, nie na poczatku.** Lud najpierw wydaje (A) i buduje (B), korona bierze z tego, co zostalo. Im wiecej lud potrafi kupic
   i zbudowac, tym mniej zostaje koronie - bez zmiany reguly. To jest odpowiedz na pytanie Jeffa: oszczedzaja, wydaja, inwestuja, placa podatki - po trochu.
4. **A jest druga noga i jedynym ujsciem pokoju** - wchodzi po pomiarze na sucho, z poprawka z poz. 11. **B jest trzecia noga** - mala liczba, ale naprawia dwie
   dziury (roboty gminy bez punktow budowy, material bez towaru) i daje graczowi cos, co widac.
5. **Pozyczka miejska, karta, sakiewka turniejowa - tylko na slowo Jeffa, po biegu rocznym.** Po daninie mieszczanie maja ponad zapas 2-9 mln, wiec i tak mniej do pozyczenia.

### 2.3 Co biore, co zmieniam, co odkladam

| Element | Skad | Decyzja sedziego | Dlaczego |
|---|---|---|---|
| Danina z nadwyzki ponad zapas dni, skalowana stawka potrzeby s (184), do skarbca, tego samego dnia w zwrocie zoldu i rentach | C R1 | BIORE jako P1; stawka 2.5% zamiast 1%; jedna stawka dla W, R, P i podzamczy zamiast 1 / 2 / 3% | stawka - rozdz. 3.2 (regula koni); wagi 2x i 3x na R i P zabralyby 25-33% plac z nadwyzki R/P, zanim dojda do wyrobnikow (5 / (15 + 5), 7.5 / (15 + 7.5)); jedna stawka zabiera 12% |
| Pomiar lojalnosci w logu | C K1 | BIORE do P1 | dzis nie ma go nigdzie |
| Kara lojalnosci za danine (do -10; osada z lojalnoscia <= 30 nie placi) | C R3 | BIORE jako osobny krok P1b, po pomiarze | to warunek Jeffa z 09.10 ("im wyzsze podatki, tym wplyw na gospodarke"), ale jedyna latka na cudzy model i wojna trwa caly rok |
| Pozyczka miejska, karta miejska | C R2, R4 | ODKLADAM (na slowo) | obiegu nie zamykaja; C sam: pomost na 150 dob, karta 5 tys. zl/d |
| Prawdziwe roboty gminy (punkty budowy, material z polki), lud doklada 2% nadwyzki, pulap rak 5%, sufit ceny materialu 2 x, najwyzej reszta poziomu | B1 | BIORE jako P2 | naprawia dziury z poz. 10; wojna nie wstrzymuje robot mieszczan |
| Trzecia czesc dla pana od robot mieszczan | B | NIE (pytanie 3) | oplata bez pokrycia w historii; trafia do panow 12-29 najbogatszych miast, z ktorych 2/3 ma luz (poz. 7); gotowke dla panow niesie danina. Farma od wlasnych wydatkow gminy (akcyza) zostaje jak dzis |
| Sufit wyrebu wsi 1.3 -> 1.8 | B2 | ODKLADAM do wyniku P2 | parametr dotyczy wszystkich towarow wsi poza zywnoscia |
| Udzialy ludu w warsztatach i karawanach | zadanie B | ODRZUCONE (za B rozdz. 2) | zysk wraca do inwestora z nawiazka - kiesy rosna szybciej |
| Lista dostatku (zbytki, sukno, lepszy stol, dom) w tempie 1/60, poprawka Predict / Share | A P1 | BIORE jako P3; tempo 1/60 tylko dla oferty na towar | poz. 11 |
| Targ widzi sakiewke (popyt z oferty, cena graniczna 1.5) | A P2 | BIORE jako P4, pod warunkiem pomiaru | sedno A; bez niego karawany nie maja jak sprzedac |
| Podzamcza i wsie kupuja z listy w miescie targowym | A P3 | BIORE jako P5 | ta sama regula, ten sam targ |
| Lud buduje za 1/240 nadwyzki | A P4 | ZASTAPIONE przez B1 | jedna regula na roboty |
| Sakiewka turniejowa | A P5 | ODKLADAM (na slowo) | lista "tylko na Twoje slowo" |
| Pomiary na sucho | A P0, B0, C K1 | BIORE wszystkie do P1 (sam log) | bez nich nie da sie ocenic P3-P4 |

---

## 3. Projekt zlozony: drabina nadwyzki

### 3.1 Regula - jedna na zjawisko "pieniadz ludu ponad potrzebe"

Jedno miejsce w dobie ludu (krok 5: TownFolk.Live.cs:809-829; podzamcze :890-893). Dla kazdej klasy (wyrobnicy W, rzemieslnicy R, rodziny kupieckie P)
i dla podzamcza, co dobe, w tej kolejnosci:

| Szczebel | Co lud robi | Ile | Granica naturalna | Skad |
|---|---|---|---|---|
| 0 | je i kupuje zwykly koszyk | jak dzis | polka targu | bez zmian |
| 1 | trzyma ZAPAS na czarna godzine | W 10 / R 21 / P 30 / podzamcze 10 / wies 15 dni potrzeby (TownFolk.cs: stale RdW-RdV; Settings.cs:1642) | - | bez zmian; tego nie rusza zadna regula |
| 2 | ZYJE LEPIEJ: oferta na liste dostatku z polki swojego miasta, po cenie targu | 1/60 nadwyzki dziennie (sukno i odzienie 55%, srebro-klejnoty-korzenie 25%, lepszy stol 12%, dom 8%); R i P jak dzis: 70% nadwyzki jako place wyrobnikom w DZISIEJSZYM tempie 15% | towar na polce; cena zbytkow najwyzej 1.5 zwyklej | A R-A2, R-A3 (P3, P4) |
| 3 | BUDUJE swoje miasto (zamek: podzamcze): material z polki, place murarzom z tego miasta | do 2% nadwyzki dziennie; 25% material, 75% place (Settings.cs:566) | budowa w kolejce, material na polce (sztuka najwyzej 2 x wartosci), rece (5% wyrobnikow x 3 zl), koszt reszty poziomu, brak oblezenia | B1 bez trzeciej pana (P2) |
| 4 | w WOJNIE oddaje koronie danine z tego, co zostalo ponad zapas | 2.5% dziennie x (s / 3%), s = stawka potrzeby korony z podatku 184 (0-3%, z polityka War Tax do 6% - wtedy x 2) | wojna i potrzeba korony; osada nieoblezona; (P1b) lojalnosc > 30 | C R1 (P1) |
| 5 | reszta LEZY jako oszczednosc i jutro znow przechodzi szczeble 2-4 | - | - | - |
| (6) | pozycza swojemu panu | - | - | C R2 - tylko na slowo Jeffa |

Korona wydaje danine tego samego dnia tymi drogami, ktore juz dzialaja: dopelnia zwrot zoldu, reszta idzie rentami wedlug lenn (CrownRents.cs; rozdz. 1 poz. 12).
Wies: bez zmian w szczeblu 4 (placi danine 2.5% od 04.10), szczebel 2 w miescie targowym (P5). Zadnej reguly dla "biednych" - kto nie ma nic ponad zapas, nie robi nic.

**Dlaczego to jedna regula, a nie trzy.** Wszystkie trzy projekty wpinaja sie w ten sam punkt kodu i te sama liczbe (`ov` = kiesa klasy minus zapas dni).
Roznia sie tylko odbiorca: kupiec (towar), murarz i kupiec (budowa), korona (danina). Kolejnosc szczebli 2 -> 3 -> 4 wynika z doby: zakupy ida z wczorajszej
oferty w ticku konsumpcji, roboty i danina w bloku doby od tego, co zostalo, oferta na jutro z reszty.

### 3.2 Parametry z rachunkiem

**Stawka daniny 2.5% (klucz `FolkWarAidShare` = 0.025; ta sama liczba, ktora od 04.10 placa wsie - Settings.cs:953).**
Zapas w rownowadze = nadwyzka dzienna / stawka skuteczna; stawka skuteczna = stawka x srednie s / 3% = stawka x 0.85 (s srednio 2.54%, LOG :128861).
Rachunek nowej kampanii, sama danina (v10.py; nadwyzka = zmierzone okna z poz. 3 i 16; 23% zwroconego zlota wraca do ludu jako zarobek - C 1.3):

| Stawka (skuteczna) | Dzisiejsze reguly: lokata w d364 / danina srednio | Z regula koni roboczych: lokata w d364 / danina srednio |
|---:|---|---|
| bez daniny | 23.2 mln / 0 | 55.9 mln / 0 [S - przedluzenie pomiaru 120 dob] |
| 1.0% (0.85%) - propozycja C | 7.7 mln / 54 tys. | 20.7 mln / 122 tys. |
| 1.5% (1.27%) | 5.3 mln / 62 tys. | 14.6 mln / 144 tys. |
| 2.0% (1.69%) | 3.9 mln / 67 tys. | 11.0 mln / 156 tys. |
| **2.5% (2.12%)** | **3.0 mln / 70 tys.** | **8.8 mln / 164 tys.** |
| 3.0% (2.54%) | 2.4 mln / 72 tys. | 7.3 mln / 169 tys. |

Wymog: lokata ponizej 10 mln w obu ukladach (10 mln to 16 dni zoldu swiata, 610 tys. zl/d - ROZP 3.3). Przy 1% regula koni zostawia w skrzyniach 20.7 mln - tyle,
ile dzis. Najmniejsza stawka, ktora spelnia wymog w obu ukladach, to 2.5%. Gdyby regula koni roboczych NIE weszla, wystarczy 1% i lud ma wtedy wiecej na zycie
(rozdz. 3.3) - to jedyna liczba projektu, ktora zalezy od decyzji spoza niego (pytanie 6).
Strumien daniny w rownowadze nie zalezy od stawki (rowna sie nadwyzce); stawka ustala tylko, ile zlota lezy i jak szybko wraca (polowa drogi po 33 dobach zamiast 82).

**Jedna stawka dla wszystkich klas.** Rozroznienie klas juz jest w zapasie (10 / 21 / 30 dni). R i P oddaja dzis wyrobnikom 155 716 zl/d plac z nadwyzki (LOG :129003)
w tempie 15%; danina 2.12% zabierze z tego strumienia 2.12 / (15 + 2.12) = 12%, czyli wyrobnicy dostana ok. 19 tys. zl/d mniej (3.6% zarobku) [S].

**Tempo dostatku 1/60, cena graniczna 1.5, wagi listy** - za A 2.1, bez zmian, z jedna poprawka: tempo 1/60 dotyczy tylko oferty na towar (poz. 11). Po daninie lokata
to 2-9 mln, wiec oferta wynosi 33-150 tys. zl/d i miesci sie w tym, co swiat wytwarza (157 tys. wedlug wartosci x 1.5). Ostateczne tempo (30-60 dni) po pomiarze fi w P1.

**Roboty: 2% nadwyzki, rece 5%, sufit ceny materialu 2 x** - za B 1.2. Bez trzeciej pana: z lokaty wychodzi 18 tys. zl/d w roku 1 i 11 tys. potem (800 i 490 wolnych
ladunkow drewna x 23 zl robot na ladunek - B 3.1), z tego 75% to place wracajace do tego samego miasta; naprawde ubywa ludowi 24% (material 25% x 0.87 + podatki od plac
75% x 0.035) = 4.3 i 2.8 tys. zl/d [S].

**Kara lojalnosci (P1b)** - za C R3: -1.0 na dobe x (s / 3%) w dniu poboru, trwale do -10; osada z lojalnoscia <= 30 nie placi i nie ma kary.

### 3.3 Proporcje: co lud robi z nadwyzka (rownowaga, nadwyzka 49 tys. zl/d = koniec biegu rocznego; v10.py) [S]

| Uklad | Lokata | Danina | Lepsze zycie (brutto) | Budowa (brutto) | Z 10 zl ruszonych z oszczednosci: korona / zycie / budowa |
|---|---:|---:|---:|---:|---|
| dzis | 23.8 mln, +47 tys./d | 0 | 0 (oferta nie kupuje) | 0 | - |
| po P1 | 2.9 mln (2.3-3.5) | 62 tys. (49-75) | 0 | 0 | 10 / 0 / 0 |
| po P1 + P2 | 2.8 mln | 59 tys. | 0 | 11 tys. | 8.5 / 0 / 1.5 |
| po P1-P4, wojna (towar znajduje 40-70% oferty) | 1.9-2.2 mln | 40-46 tys. | 15-22 tys. | 11 tys. | 5.5-6.5 / 2-3 / 1.5 |
| po P1-P4, pokoj (bez daniny) | 5.8-10.1 mln | 0 | 67 tys. | 11 tys. | 0 / 8.5 / 1.5 |
| po P1-P4, wojna, z regula koni roboczych (nadwyzka 146 tys.) | 5.8-6.7 mln | 122-142 tys. | 45-67 tys. | 11 tys. | 6-7 / 2.5-3.5 / 0.5 |
| dla porownania: stawka 1%, P1-P4, wojna, bez reguly koni | 3.2 mln | 27 tys. | 37 tys. | 11 tys. | 3.5 / 5 / 1.5 |

Przedzialy w nawiasach: nadwyzka 39-59 tys. zl/d (okna z poz. 3). Kiesy ludu miast = czesc "zapas" 12.7 mln (W 7.85 + R 3.51 + P 1.36, LOG :129003) + lokata:
po P1 ok. 15.6 mln, po wszystkim ok. 14.6-14.9 mln (dzis 36.5 mln). W pokoju z regula koni zbytki nie udzwigna nadwyzki (trzeba by 206 tys. zl/d przy miejscu na
150 tys.) - lokata 17-31 mln; to jest dziura pokoju, opisana w rozdz. 10.

Nowa kampania, rok 1 (start lokata 0): po P1 lokata w d364 3.0 mln (szczyt 4.0), danina srednio 70 tys. zl/d, do panow 64 tys.; po P1-P4 lokata 1.8-2.1 mln, danina
47-54 tys., lepsze zycie 17-26 tys., budowa 18 tys., do panow 60-62 tys. zl/d.

---

## 4. Tabela przeplywow w rownowadze (wojna, dzisiejsze reguly, zl na dobe)

| Od -> do | Dzis, koniec roku [P] | Po P1 [S] | Po P1-P4 [S] | Droga |
|---|---:|---:|---:|---|
| Lud -> kupcy: zwykly koszyk (miasta + zamki) | 455 747 | bez zmian | bez zmian | polka targu (ROZP 1.1) |
| Lud -> kupcy: lista dostatku (dodatkowo) | 0 | 0 | 15-22 tys. | tick konsumpcji BK, zaplata z kiesy |
| - z tego wraca ludowi tego miasta (robocizna, obrot, akcyza) 19% | - | - | 3-4 tys. | A 4.3 |
| - ludowi miast, ktore wytwarzaja 18% | - | - | 3-4 tys. | place w wyrobach, robocizna przy karawanach |
| - panom wprost 24% / koronie 28% / notablom i karawanom 11% | - | - | 4-5 / 4-6 / 2 tys. | zawor kas kupcow, podatek wsi, danina kas i wsi |
| Lud -> budowa miasta (brutto) | 0 | 0 | 11 tys. (rok 1: 18) | drugi platnik robot (BuildFunding) |
| - material do kas kupcow 25% / place murarzy do ludu tego miasta 75% | - | - | 2.8 / 8.5 tys. | polka targu; kiesy W 60% / R 40% |
| **Lud -> korona: danina z oszczednosci** | **0** | **62 tys. (49-75)** | **40-46 tys.** | wplyw dnia 165 |
| Lud -> korona: podatek 184 od zarobku | 13 447 | ok. 14 tys. | ok. 14 tys. | bez zmian |
| Lud -> pan: podatki 1/2/3% i farma gmin | 17 268 | ok. 17.5 tys. | ok. 17.5 tys. | bez zmian |
| Korona -> rody z daniny (91%: dopelnienie zwrotu zoldu 7%, renty 84%; 9% zastepuje kredyt Banku - C 3.2) | 0 | 56 tys. (45-68) | 36-42 tys. | zwrot zoldu i renty 180, ten sam dzien |
| - w tym do rodow na pulapie zoldu (30.5%, poz. 6; po poprawce pulapki rent wiecej) | 0 | 17 tys. | 11-13 tys. | - |
| **Do panow razem (danina + czesc dostatku + material budow)** | **0** | **56 tys. (45-68)** | **50-52 tys.** | - |
| Wraca do ludu jako zarobek (23% tego, co doszlo do panow) | - | 13 tys. | 12 tys. | zold wydany w miescie, dwor, sprzet |
| **Przyrost lokaty** | **+47 tys.** | **0** | **0** | - |
| Lokata (stan) | 23.8 mln, rosnie | 2.9 mln | 1.9-2.2 mln | - |
| Kiesy ludu miast (stan) | 36.5 mln | 15.6 mln | 14.6-14.9 mln | - |
| Wojsko: realnie / gorna granica | - | +1.7 do +2.5 tys. / +5.6 do +7.3 tys. | +1.5 do +2.3 tys. / +5 do +6.8 tys. | 0.3-0.45 (granica 1.0-1.3) tys. ludzi na 10 tys. zl/d; wlasny rachunek: 56 000 x 0.305 x 0.60 / 6.19 = 1.7 tys. |

Z regula koni roboczych: danina 164-185 tys. zl/d, do panow 149-168 tys. - to jest zloto, ktore ta regula dzis panom zabiera (wojsko -14.6 tys. w 120 dob, SP :1597);
danina je zawraca, ale dopiero po urosnieciu lokaty do ok. 9 mln, czyli w pierwszych 3-4 miesiacach kampanii czesc straty zostanie. Do sprawdzenia biegiem, nie rachunkiem.

Wszystkie liczby [S] pochodza z rownania lokaty (v10.py), nie z biegu - tak samo jak u autorow A, B i C.

---

## 5. Dowod zamknietego obiegu

Kazdy nowy ruch ma platnika, odbiorce i (gdy to towar) sztuke z polki. Nowych posiadaczy zlota nie ma. Kontrole bez zmiany definicji: "Kiesa ludu: zmiana kies =
wplywy - wydatki, r = 0", "Pieniadz swiata (bilans)", "Towary (bilans)".

| Ruch | Platnik (zdjete) | Odbiorca (dopisane) | Towar | Wzor w kodzie |
|---|---|---|---|---|
| Danina z oszczednosci (P1) | kiesa klasy / podzamcza, tylko ponad zapas dni | skarbiec krolestwa wlasciciela osady, wplyw dnia 165 | - | jak `FolkWarTax` (KingdomTreasury.WarTax.cs:147-179): te same bramki, to samo okno wplywow; ulamek ponizej 1 zl czeka |
| Skarbiec -> rody | skarbiec | glowy rodow (zwrot zoldu, renty 180) | - | istniejace, bez zmian |
| Material robot (P2) | kiesy klas | kasa kupcow miasta | sztuki zdjete z polki, kazda po swojej cenie, najwyzej 2 x wartosci | `BuildFunding.BuyMaterials`, `ShelfBuy.Take` |
| Place robot (P2) | kiesy klas (oszczedzajacy) | kiesy W 60% / R 40% tego miasta (pracujacy) | punkty budowy = zaplacone / cena punktu | `TownFolk.Earn(KBuildLabour)`, `PointsFor` |
| Roboty gminy z akcyzy (P2 - naprawa) | gmina | jak wyzej; farma 1/3 panu jak dzis | sztuki z polki (dzis: przelew bez towaru, poz. 10) | TownFolk.Live.cs:793-808 |
| Zakup z listy dostatku (P3) | kiesa klasy | kasa kupcow 98% + gmina 2% (akcyza) | sztuki zdjete przez tick konsumpcji BK | `PayForGoods` (bez zmian) |
| Sygnal popytu (P4) | brak - zmienia sie liczba "popyt" w danych targu | - | - | jak `RawPrice` ("sama cena niczego nie tworzy") |
| Zakup podzamcza i wsi w miescie (P5) | kiesa podzamcza / wsi | kasa kupcow miasta targowego | sztuki z polki tego miasta | `BuyGoods` (bez zmian) |

Czego lud nie wyda, nie zbuduje i czego korona nie wezmie - zostaje w kiesie. Nic nie powstaje i nic nie znika.

---

## 6. Sto procent przypadkow

| Przypadek | Co sie dzieje |
|---|---|
| Miasto biedne (49 z 97 bez lokaty; 31 ponizej 2 dni koszyka) | nic nie placi, nic nie buduje, nic nie kupuje ponad koszyk; chleba nikt nie rusza. Zysk tylko posredni (place w wyrobach, gdy bogate miasta kupuja jego towar). Tego projektu biedne miasta NIE ratuja - osobna sprawa (ROZP 2) |
| Podzamcze zamku | te same szczeble: danina od kiesy ponad 10 dni, budowa zamku z materialu najblizszego miasta (jak pan), zakupy z listy w miescie targowym (P5) |
| Wies | danine juz placi (2.5%); lista dostatku w miescie targowym (P5); budowle wsi BK - poza projektem |
| Krolestwo w pokoju | danina 0 (s spada do zera w 4 tygodnie); dzialaja tylko szczeble 2 i 3. Lokata rosnie do 6-10 mln (dzisiejsze reguly) i czeka na wojne - jak skarb na pierwsza kampanie. Z regula koni roboczych 17-31 mln [S] - patrz rozdz. 10 pkt 2 |
| Krolestwo w wojnie bez potrzeby (s = 0: w d364 Iron Islands, Free Folk; s = 0.5%: Dothraki, Nocna Straz) | danina 0 albo mala - "korona nie potrzebuje, wiec nie bierze" (decyzja 09.10: stawka wedlug potrzeby, SP :766-:767; LOG :128861). Ich udzial w lokacie: ok. 10% (v2.py: Dothraki 7.1%, Iron Islands 2.5%, Nocna Straz 1.0%) - tam zostaja tylko szczeble 2 i 3 |
| Rod bez krolestwa, buntownicy | bez daniny (nie ma korony - jak 184) |
| Osada oblezona | bez daniny (bramka jak 184), bez robot (jak budowy pana) |
| Osada zmienia pana | danina od nastepnej doby do korony nowego pana; gra czysci kolejke budow, punkty poziomu zostaja |
| Brak budowy w kolejce / pusta polka materialu / brak rak | bez robot tego dnia; pieniadz zostaje i przechodzi do szczebla 4 |
| Brak towaru z listy na polce | bez zakupu; pieniadz zostaje i przechodzi do szczebli 3-4 |
| Miasto niewolnicze | bez wyjatku: placa kiesy klas, ktore maja zloto |
| Kiesa ludu wylaczona (`TownFolkLive`) | cala drabina martwa, stan sprzed etapu 5 |
| Gracz | rozdz. 7 |

---

## 7. Gracz i to, co widzi

**Te same reguly.** W jego miastach i zamkach lud wydaje, buduje i w wojnie placi koronie jego krolestwa wedlug tej samej stawki. Jesli jest krolem, danina wchodzi
do skarbca krolestwa (nie do jego kiesy) i idzie do rodow - takze jego - zwrotem zoldu i rentami na zwyklych warunkach. Bez krolestwa - jego miasta nie placa nikomu.
Roboty mieszczan posuwaja budowe z czola JEGO kolejki - to on wybiera, na co ida ich pieniadze; pusta kolejka = lud nie buduje. Handel: cena zbytkow w bogatym miescie
odpowiada prawdziwemu popytowi, wiec trasa "tanio u wytworcy, drogo tam, gdzie lud ma pieniadze" dziala dla niego jak dla karawan AI; placi mu kasa kupcow, wiec
sprzeda tyle, ile kasa ma. Jego dlug, warsztaty i karawany - bez zmian.

**Napisy (po angielsku).** Okno "People of this land" (zdanie obok "Bread: ..."), liczby z ostatniej doby osady:

- `Savings: the townsfolk hold coin for 26 days of living. Above 10 days they spend it: 1,420 coins a day on cloth, silver and a better table (380 found no goods), 350 toward the Granary, and 610 in war aid to the crown.`
- `Savings: the townsfolk hold coin for 8 days of living - nothing to spare.`
- `Works: the townsfolk would pay 350 coins a day, but the market has no timber or stone.` / `Works: nothing is waiting to be built here.` / `Works: stopped - the town is under siege.`
- podzamcze i wies: `Savings: the villagers hold coin for 31 days of their purchases; they shop at the market of Riverrun.`

Komunikat, raz na wojne, osada gracza: `War aid: while the realm is at war, the people of {TOWN} give the crown one part in forty of their savings each day. Their bread money is not touched.`
Krol-gracz, raz na tydzien: `War aid from the towns of the realm brought the treasury {N} coins this week.`
Dymek mocy budowy, druga linia: `Town works (paid by the townsfolk)`. Dziennik (miasta gracza): `The townsfolk of Riverrun have finished the Marketplace (level 2) at their own cost.`
P1b - dymek lojalnosci: `War aid from the people's savings`; komunikat: `The tax gatherers have left {TOWN}: the town is too close to revolt to pay the war aid.`
MCM (po `gen_mcm.py`): `Folk War Aid`, `Folk War Aid Share`, `Folk War Aid Loyalty`, `Townsfolk Build Their Town`, `Townsfolk Works Share`, `Townsfolk Works Hands`,
`Building Material Price Cap`, `Folk Buy By Wealth`, `Folk Spend Surplus In Days`, `Folk Ladder Price Ceiling`.

---

## 8. Paczki w kolejnosci wdrazania

Jedna zmiana naraz (CLAUDE.md 8.2). Kazda paczka ma wlasny wylacznik, ktory przywraca stan sprzed niej; zadna z P1-P5 nie dodaje pola do zapisu.

| # | Paczka | Tor | Tresc | Skutek [S] | Warunek wejscia | Czas doby |
|---|---|---|---|---|---|---|
| **P1** | **Danina z oszczednosci + pomiary** | **S** | szczebel 4 (C R1 ze stawka 2.5%, jedna dla klas); zdanie "Savings" w oknie mieszkancow; SAM LOG: lojalnosc osad (srednia, ile <= 30 i <= 25), kupione z oferty wedlug kategorii, popyt i indeks "na sucho", marza kupcow na towarze z karawan, miasta z warsztatami zbytkow, lokata miast z budowa w kolejce i material na polce, zloto rodow z luzem, sklad zarobku miasta | lokata 23.8 -> ok. 3 mln; do koron 49-75 tys. zl/d; najwiekszy skutek calego projektu | slowo Jeffa (pyt. 1); w jednym zbiorze z poprawkami wojska rundy 3 (pulapka rent - bez niej danina Polnocy trafia w 0% do rodow na pulapie, poz. 6) | < 0.1 ms |
| P1b | Skutek daniny: lojalnosc | S | szczebel 4 - kara i granica 30 (C R3); latka na CZYNNY model lojalnosci, zakladana dopiero w kampanii (CLAUDE.md 7 - `BKLoyaltyModel` ma pole statyczne czytajace teksty gry) | do -10 lojalnosci w osadach placacych | pyt. 2; pomiar lojalnosci z biegu P1 | ok. 0.01 ms |
| P2 | Lud buduje miasto | M | szczebel 3 (B1 bez trzeciej): drugi platnik w `BuildFunding`, roboty gminy z akcyzy jako prawdziwe punkty i prawdziwy material, sufit ceny 2 x dla obu platnikow, zakup w wozach po 10 | 11-18 tys. zl/d robot; ok. 120 poziomow budowli rocznie zamiast 69; dwie dziury zamkniete | pyt. 3 | ok. +0.5 ms |
| P3 | Lista dostatku | M | szczebel 2 (A P1): lista z wagami, tempo 1/60 tylko dla oferty na towar, poprawka Predict / Share (A 9.9) | lud kupuje zbytki tam, gdzie leza (na polkach miast 4.4 mln zl) | pyt. 4; pomiar "kupione z oferty" z P1 | < 0.2 ms |
| P4 | Targ widzi sakiewke | M | A P2: oferta ludu dopisana do popytu targu (bez zywnosci, piwa, plotna i skory), cena graniczna 1.5 | karawany rozladowuja 8.1 mln zl zbytkow; lepsze zycie 15-22 tys. zl/d w wojnie, do 67 tys. w pokoju | marza kupcow na towarze z karawan z pomiaru P1 nie gorsza niz -10% | < 0.3 ms |
| P5 | Podzamcza i wsie na targu miasta | S | A P3 | ta sama lista dla 130 podzamczy i 571 wsi | P4 | pomijalny |
| na slowo | pozyczka miejska (C K3, M), karta miejska (C K4, S), sakiewka turniejowa (A P5, S), sufit wyrebu wsi 1.8 (B2, S - po wyniku P2) | - | - | - | slowo Jeffa, po biegu rocznym | - |

Razem ok. +1.1 ms na dobe wobec dzisiejszych 10.7 ms kiesy ludu (LOG :129003) i 8.7-9.6 ms budow (B rozdz. 9).

**Kolejnosc testow:** P1 - bieg 120 dob (nowa kampania); potem scalenie reguly koni roboczych (galaz czeka, SP :1600) i drugi bieg 120 dob z obiema - to on rozstrzyga,
czy regula koni moze wejsc; P1b, P2, P3, P4 - kazda bieg 120 dob; bieg roczny po P2 (albo po P4, jesli pomiary pozwola).

**Jesli Jeff powie NIE na danine (pyt. 1):** zostaja P2 -> P3 -> P4 -> P5. Lokata staje dopiero po P4, na 6-10 mln (towar znajduje 70-40% oferty), do panow 28-36 tys. zl/d
zamiast 50-65 tys., a regula koni roboczych nie moze wejsc (lokata 17-31 mln [S]).

---

## 9. Progi testu rocznego (nowa kampania, 364 doby; w nawiasie bieg z 13.10)

| # | Co | Prog | Paczka |
|---|---|---|---|
| 1 | Lokata W w d364 | <= 6 mln (23.8); z regula koni roboczych <= 12 mln | P1 |
| 2 | Przyrost lokaty, srednia d301-364 | w przedziale +-10 tys. zl/d (+47.4 tys.) | P1 |
| 3 | Kiesy ludu miast w d364 | <= 20 mln (36.5); z regula koni <= 26 mln | P1 |
| 4 | Danina z oszczednosci, srednia d301-364 | 45-90 tys. zl/d; z regula koni 130-220 tys. | P1 |
| 5 | Zamkniecie: kontrola kies r = 0 kazdej doby; "Pieniadz swiata" bez skoku (28 dob w progu 0.1%) | 0 | wszystkie |
| 6 | "Stac jedzenie": swiat >= 98%, miast ponizej 95% <= 10; "zjedzone bez zaplaty" <= 2.5% wydatku gry | (98.2% / 10 / 2.27%) | wszystkie |
| 7 | Zwrot zoldu wyplacony / nalezny; renty 180, srednia d301-364 | >= 98% (95.2%); >= 200 tys. zl/d (159-188 tys.) | P1 |
| 8 | Dlug koron w Banku w d364 | <= d182; pozyczaja tylko krolestwa bez lokaty miast | P1 |
| 9 | Zloto rodow z luzem pulapu (nowy zbiornik) | przyrost roczny <= +6 mln | P1 |
| 10 | Kiesy 31 najbiedniejszych miast razem | >= 0.53 mln (0.53) | wszystkie |
| 11 | Partie rodow w d364 - INFO, nie prog | +1.5 do +2.5 tys. wobec biegu z tymi samymi poprawkami wojska bez P1; z regula koni: nie mniej niz w biegu bez reguly koni minus 3 tys. | P1 |
| 12 | Lojalnosc: srednia osad placacych; osady <= 25; bunty w osadach placacych | >= 40; nie wiecej niz bieg P1 + 2; 0 | P1b |
| 13 | Roboty mieszczan d91-364; poziomy ukonczone w roku; budowy stojace w d364 | >= 8 tys. zl/d; >= 110 (69); <= 175 z 227 (198) | P2 |
| 14 | Zaplata za material bez sztuki; punkty ponad koszt poziomu | 0 zl; 0 | P2 |
| 15 | Drewno: stos swiata; narzedzia: polki miast ponizej 5 szt. w <= 10% miast; zakupy narzedzi przez wsie | 30-60 dni zuzycia (76); >= 42 szt./d | P2 |
| 16 | Kupione z oferty / oferta, srednia d120-364 | >= 50% | P3-P4 |
| 17 | Place z nadwyzki R/P dla wyrobnikow | >= 120 tys. zl/d (134-156 tys.) - pilnuje poprawki z poz. 11 | P3 |
| 18 | Indeks ceny kategorii z sygnalem w 20 najbogatszych miastach; ceny zywnosci tamze | <= 1.5; +-5% wobec biegu bez P4 | P4 |
| 19 | Marza kupcow na towarze z karawan; kasy kupcow ponizej zapasu >= 7 dob | >= -10%; <= 4 miasta, zadne z 20 najbogatszych | P4 |
| 20 | Cena zbytu klejnotu / aksamitu u wytworcy; zbytki w jukach karawan | >= 0.7 wartosci (0.50 / 0.87); klejnoty <= 400, bawelna <= 2 000, filc <= 5 000 (747 / 4 151 / 9 286) | P4 |
| 21 | Czas: kiesa ludu (latki + blok doby); `BuildFunding.Daily` | <= 11.9 ms (10.7); <= 11 ms (8.7-9.6) | wszystkie |

Bieg 120 dob po P1 (przed rocznym): progi 1 (<= 4.5 mln w d120; bylo 8.9), 4 (>= 40 tys. zl/d w d91-120), 5, 6, 9 i sam odczyt lojalnosci.

---

## 10. Ryzyka i to, czego nie wiem

1. **Zloto przeniesie sie w jednej trzeciej od ludu do bogatych rodow.** Renty dziela wedlug lenn; 67% trafia do rodow z luzem pulapu (poz. 6). Z 56 tys. zl/d
   ok. 18-25 tys. osiadzie w kiesach glow (C 3.2). Lekarstwo jest w obszarze wojska (rozdzial rent, werbunek AI), nie tutaj. Prog 9.
2. **Pokoj.** Danina nie dziala; bieg pokoju nie zmierzyl; dwa miasta bez wojny odkladaly 45-56% zarobku (poz. 15). Przy dzisiejszych regulach P3-P4 trzymaja lokate na
   6-10 mln [S]; z regula koni roboczych nie utrzymaja (17-31 mln). Swiat ROT ma wojne przez caly rok, wiec to dzis ryzyko dlugiej kampanii, nie pierwszego roku -
   ale "100%" bedzie prawda dopiero po zmierzeniu pokoju.
3. **Regula koni roboczych potraja skale.** Mieszczanie wydaja dzis na konie ok. 194 tys. zl/d (SP :1564) - to byl ich "zbytek". Bez koni nadwyzka rosnie o ok. 125 tys. zl/d
   (poz. 16), a danina staje sie jedna trzecia zarobku ludu (164-185 z ok. 500 tys. zl/d). Rachunek mowi, ze zloto wraca; czy wojsko wraca - pokaze tylko bieg.
4. **Lojalnosc.** Wojna trwa caly rok, wiec kara -10 jest stala. Miasta z obcym panem sa blisko progu. Dlatego P1b jest osobno i po pomiarze.
5. **A stoi na rzeczach nie zmierzonych:** ile oferty znajdzie towar, jaka jest marza kupcow na zbytkach z karawan (pan bogatego miasta moze dostac z zaworu MNIEJ),
   gdzie stoja warsztaty zbytkow. Jesli pomiar z P1 wyjdzie zle, P4 nie wchodzi i szczebel 2 zostaje maly (4.4 mln zl towaru jednorazowo).
6. **B: narzedzia i drewno.** Roboty moga wykupic narzedzia wsiom (budowy biora 5-20 z 84-110 szt./d); w pokoju panowie zuzywaja cale drewno. Prog 15.
7. **Dwa pobory od ludu w wojnie** (184 od zarobku, 13 tys. zl/d, i danina od oszczednosci). 184 bierze takze od miast, ktorych nie stac na chleb. Po biegu rocznym
   do decyzji projektu: wygasic 184 i zostawic jedna podstawe.
8. **Pierwsze miesiace kampanii.** Lokata musi urosnac, zanim danina zacznie plynac (polowa drogi po 33 dobach). W tym czasie zloto nie wraca.
9. **Stary zapis.** Wlaczenie P1 na zapisie z d364: 23.8 mln x 2.12% = ok. 500 tys. zl/d do skarbcow pierwszego dnia (wplywy koron x 2.2). Testowac na nowej kampanii.
10. **Rownowaga to rachunek, nie bieg.** Nadwyzka ludu jest roznica dwoch duzych liczb (525 - 486 tys.); spadek wplywow o 7% ja zeruje. Kazda liczba [S] z rozdz. 3-4
    ma blad rzedu +-30%.
11. **Kolizje w kodzie do sprawdzenia przed kazda paczka (CLAUDE.md 8.0):** P1 - `FolkSurplusSpendShare` moze byc zapisany w `Armoury.json` Jeffa (CLAUDE.md 7);
    danina przed oferta nadwyzki czy po (przyjete: po robotach, przed oferta na jutro). P2 - `Covered` odcina lenna gracza bez `PaidConstructionPlayer`; `_warSkipped`
    dotyczy tylko pieniedzy pana. P3 - Predict / Share licza oferte oferowana, nie kupiona (A 9.9).

Nie ustalone przez nikogo: sklad zarobku pojedynczego miasta, zachowanie w roku 2 (czy nadwyzka zgasnie sama - ROZP 1.2), ile drewna wsie dadza przy trwale wyzszym
zuzyciu, czy AI przy szybkich budowach wybierze same wojskowe.

---

## 11. Pytania do Jeffa - tylko kierunek

1. **Danina z oszczednosci.** Czy mieszczanie maja w wojnie oddawac koronie czesc tego, co maja ponad zapas na czarna godzine (czterdziesta czesc dziennie przy pelnej
   stawce - tyle, co wsie)? To rozszerzenie Twojej zgody z 09.10 z zarobku na oszczednosci i krewny "podatku nadzwyczajnego miast" z listy "tylko na Twoje slowo".
   **Rada: TAK.** To jedyny krok, ktory zamyka obieg na pewno; bez niego w skrzyniach zostaje 6-10 mln nawet po zbytkach i budowie.
2. **Skutek daniny.** 09.10 postawiles warunek "im wyzsze podatki, tym wplyw na gospodarke i dobrobyt". Czy miasto placace danine ma tracic lojalnosc do pana (do -10;
   na skraju buntu przestaje placic)? **Rada: TAK, ale jako osobny maly krok po pierwszym pomiarze** - wojna trwa tu caly rok, a lojalnosci miast nikt jeszcze nie mierzyl.
3. **Budowa.** Czy mieszczanie maja budowac miasto pana z wlasnej kiesy - takze mury w wojnie i takze w Twoich miastach (Ty ukladasz kolejke)? I czy pan ma przy tym
   dostawac trzecia czesc w gotowce? **Rada: budowa TAK, trzecia NIE** - pan dostaje budowle i nie wydaje na nie wlasnych pieniedzy, a gotowke dla panow niesie danina.
4. **Zbytki.** Czy bogaty lud ma kupowac klejnoty, aksamit, futra i srebro, a ich cena ma rosnac tam, gdzie lud ma pieniadze (najwyzej do poltorej ceny zwyklej; chleb,
   piwo, plotno i skora bez zmian)? **Rada: TAK, po pomiarze z pierwszego kroku** - to glowne ujscie pieniadza ludu w pokoju i jedyna czesc, ktora daje Ci nowy handel.
5. **Pomysly "na Twoje slowo".** Pozyczka pana od rady wlasnego miasta (12% rocznie, takze dla Ciebie w menu miasta), karta miejska, sakiewka turniejowa od mieszczan -
   teraz czy pozniej? **Rada: POZNIEJ, po biegu rocznym** - obiegu nie zamykaja (odsetki plyna do ludu), a po daninie mieszczanie maja mniej do pozyczenia.
6. **Konie robocze.** Regula "mieszczanie kupuja tylko konie robocze" czeka na te paczke i potraja nadwyzke ludu. Wgrywamy ja razem z danina (wtedy stawka czterdziesta
   czesc), czy zostaje poza gra (wtedy wystarczy setna czesc i lud ma wiecej na zycie: 5 zl z 10 zamiast 2-3)? **Rada: razem, ale dopiero po biegu 120 dob z obiema.**

---

## 12. Zaleznosci od innych prac

- **Poprawki wojska rundy 3** (pulapka rent `CrownRentServiceAtMeans`, `CrownRentContactOnlyWhenCalled`, zwolnienia `ReleaseAcrossTiers`, kompanie najemne
  `MercBkDownPaymentAiOff` i `MercContractPaysMenUnderArms`, blad wezwan `CallToWarBreakRestoresWallet`) - zakodowane i scalone (SP :1602-:1609); test 120 dob:
  wojsko lordow w wojnie 101 144 wobec 88 171 bez nich, rody z lennem bez renty 7.2% (SP :1617-:1619); bieg roczny w toku, niewgrane.
  P1 po nich albo razem z nimi: bez pulapki rent danina Polnocy trafia w calosci do rodow z luzem (poz. 6), a bez poprawki kompanii uwolnione zloto konczy u najemnikow
  (ok. 49 tys. zl/d, TRIAZ O1-4).
- **P1 to dopracowana "poprawka B" z TRIAZ O1-7** (danina takze z kies ludu; w SP :1605 "do decyzji Jeffa") - nie robic obu.
- **Regula koni roboczych K1-3a** (galaz `w-toku/k13a-konie-robocze`, SP :1595-:1600: "nie wgrywac przed paczka obiegu kiesy ludu") - po P1, bieg 120 dob z obiema; stawka
  daniny 2.5% jest policzona pod nia.
- **Podatek 184** - bez zmian teraz; po biegu rocznym decyzja z rozdz. 10 pkt 7.
- **Plan kodu** - osobny dokument dla P1 (tor S): wpiecia za C rozdz. 2 R1 i rozdz. 10, z dwiema zmianami sedziego (stawka 2.5%, jedna dla klas) i z lista pomiarow z rozdz. 8.
