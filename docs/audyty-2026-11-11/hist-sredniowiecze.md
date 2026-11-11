# Bitwy sredniowiecza - material dla projektanta AI bitewnego

Zakres: 19 bitew (1066-1461), przelozone na decyzje wodza AI dla 4 typow formacji (piechota, strzelcy, jazda, strzelcy konni), do ok. 1000 ludzi na strone.
Zrodla: glownie wiedza wlasna; cztery bitwy (Verneuil, Patay, Formigny, Mohi) sprawdzone wyszukiwaniem w sieci - wyniki to w wiekszosci Wikipedia i serwisy popularne, nie opracowania naukowe. Miejsca niepewne oznaczone [NIEPEWNE]. Liczebnosci pomijam - sa sporne prawie wszedzie.

## 1. Wspolny schemat faz

Prawie kazda z tych bitew da sie rozlozyc na te same fazy. Roznia sie tym, kto ktora faze wygral i kto ktora pominal.

| Faza | Co sie dzieje | Typowa decyzja wodza |
|---|---|---|
| 0. Wybor pola | Obronca zajmuje wzgorze / przeszkode, opiera skrzydla o las, bagno, rzeke | Bronic sie czy atakowac; czy spieszyc jazde |
| 1. Rozstawienie | 2-3 hufce w linii albo jeden za drugim, strzelcy z przodu lub na skrzydlach, odwod z wodzem | Ile zostawic w odwodzie (zwykle 1/4-1/3) |
| 2. Harce i ostrzal | Strzelcy / lekka jazda probuja rozbic szyk albo sprowokowac wyjscie z pozycji | Kto pierwszy nie wytrzyma ostrzalu i ruszy |
| 3. Pierwsze uderzenie | Szarza albo natarcie piechoty na wybrany punkt | Gdzie: w strzelcow, w skrzydlo, w sztandar |
| 4. Walka wrecz | Dlugie zwarcie, rotacja szeregow, przerwy na oddech | Kiedy rzucic odwod; czy zmienic cel |
| 5. Kryzys | Peka skrzydlo, ginie wodz, pada sztandar, przychodzi posilek z boku | Kontratak odwodem albo wycofanie |
| 6. Zalamanie i poscig | Wiekszosc strat pada tutaj | Scigac calym wojskiem, czescia, czy stac |

Dwie obserwacje wazne dla AI:
- Bitwy trwaly godzinami i mialy pauzy. Hastings i Towton - caly dzien. Strony odstepowaly, poprawialy szyk, wracaly. Ciagle zwarcie bez przerwy jest ahistoryczne.
- Strona, ktora ma przewage w ostrzale, zmusza druga do ruchu. To jest glowny mechanizm rozstrzygajacy, czy obronca utrzyma dobra pozycje (Towton, Formigny, Falkirk, Azincourt).

## 2. Tematy taktyczne

### 2.1 Sciana tarcz
- Hastings 1066: Anglosasi na grzbiecie wzgorza, skrzydla oparte o stromizny i las. Sciana wytrzymala lucznikow (strzal pod gore, w tarcze), piechote i kolejne szarze przez wiekszosc dnia.
- Pekla dopiero, gdy czesc ludzi zeszla ze wzgorza w poscigu za uciekajacymi Bretonczykami i zostala odcieta na rowninie. Potem Normanowie powtarzali to celowo (pozorowane ucieczki, wg Wilhelma z Poitiers dwukrotnie).
- Koniec: smierc Harolda pod wieczor, ale przyboczni walczyli dalej przy sztandarze.
- Warunki powodzenia: teren wyzej, skrzydla nie do obejscia, zakaz poscigu. Warunki porazki: brak wlasnych strzelcow i jazdy (nie ma czym odpowiedziec), zlamanie szyku przez poscig, smierc wodza.

### 2.2 Pozorowany odwrot
- Dziala, gdy: uciekajacy sa szybsi od scigajacych (jazda przed piechota, lekka jazda przed ciezka), jest przygotowany oddzial, ktory uderzy w rozciagnietych scigajacych, a przeciwnik ma slaba dyscypline.
- Hastings: odciecie scigajacych piechurow jazda.
- Legnica 1241: Mongolowie odciagneli jazde od piechoty, potem zasypali strzalami i uderzyli ciezka jazda z bokow. Zaslona dymna i okrzyk "uciekajcie" pochodza od Dlugosza, 200 lat pozniej [NIEPEWNE].
- Grunwald 1410: odwrot lekkiej jazdy litewskiej - celowy czy prawdziwy, historycy sie spieraja [NIEPEWNE]. Skutek pewny: czesc krzyzackich choragwi poszla w poscig, wypadla z bitwy i wrocila w nieladzie.
- Nie dziala przeciw wojsku, ktore ma zakaz poscigu i go slucha (Arsuf do pewnego momentu, Anglicy pod Crecy - nie ruszyli sie z pozycji nawet po zwyciestwie).

### 2.3 Szarza rycerska - warunki
Dziala, gdy spelnione jest wiekszosc z:
1. Twardy, rowny, otwarty grunt i miejsce na rozped (Verneuil, Patay, Bouvines).
2. Cel jest w ruchu, w nieladzie albo nie zdazyl sie ustawic (Patay; Arsuf - Turcy podeszli tak blisko, ze nie zdazyli odskoczyc).
3. Cel nie ma przeszkody przed frontem.
4. Uderzenie w bok lub tyl, nie w front (Poitiers - Captal de Buch; Formigny - Richemont; Towton - Norfolk).
5. Jest druga linia, ktora wykorzysta wylom, i jazda umie sie zatrzymac i zebrac (Arsuf: Ryszard trzykrotnie zbieral jazde i ponawial szarze, odwod przy sztandarze nie wszedl do walki).

Zawodzi, gdy:
- przed celem jest row, strumien, bloto, pale, wilcze doly (Courtrai, Crecy, Azincourt, Bannockburn, Nikopolis);
- szarza idzie pod gore (Hastings, Crecy, Nikopolis);
- szarze sa po kolei, bez koordynacji (Crecy - kilkanascie osobnych fal);
- jazda tratuje wlasna piechote po drodze (Crecy - kusznikow genuenskich, Courtrai);
- po przelamaniu jazda leci na tabor zamiast zawrocic (Verneuil);
- cel to zwarta piechota drzewcowa, ktora stoi (Falkirk, Bannockburn, Courtrai).

### 2.4 Spieszeni rycerze
- System angielski (Crecy, Poitiers, Azincourt, Verneuil): ciezkozbrojni pieszo w srodku jako kotwica, lucznicy na skrzydlach wysunieci do przodu, przeszkoda przed frontem, konie i tabor z tylu. Atakujacy, sciskany ostrzalem z bokow, zbija sie do srodka i trafia na ciezkozbrojnych juz zmeczony i scisniety.
- Francuska odpowiedz pod Poitiers i Azincourt: tez sie spieszyc. Blad: spieszony w pelnej zbroi, ktory musi przejsc kilkaset metrow (pod Azincourt po rozmoklej orce) pod ostrzalem, dochodzi wyczerpany. Spieszenie sluzy obronie, nie natarciu.
- Poitiers: Czarny Ksiaze w decydujacej chwili kazal czesci ludzi dosiasc koni i kontratakowac - spieszenie jest odwracalne, konie stoja za linia.
- Decyzja dla AI: spieszyc jazde, gdy sie bronimy, mamy mniej ludzi, a teren nie pozwala szarzowac. Zostawic niewielki konny odwod.

### 2.5 Strzelcy i ich neutralizacja

| Bitwa | Sposob | Wynik | Dlaczego |
|---|---|---|---|
| Patay 1429 | Szarza jazdy na lucznikow | Sukces | Lucznicy zdradzili pozycje (okrzyk na widok jelenia), nie skonczyli wbijac pali, skrzydla odkryte, straz przednia uderzyla od razu, nie czekajac na reszte |
| Verneuil 1424 | Szarza ciezkiej jazdy lombardzkiej | Sukces miejscowy, bitwa przegrana | Twardy, spieczony grunt (pale ledwo wchodzily), dobre zbroje plytowe i ladry konskie; ale jazda poleciala rabowac tabor, a piesza bitwa rozstrzygnela sie bez niej |
| Bannockburn 1314 | Lekka jazda (Keith) w bok lucznikow | Sukces | Lucznicy wysunieci na skrzydlo bez oslony, uderzeni z boku |
| Falkirk 1298 | Jazda angielska na lucznikow szkockich | Sukces | Stali miedzy schiltronami bez przeszkody, wlasna jazda uciekla |
| Crecy 1346 | Kusznicy, potem szarze od czola | Kleska | Pod gore, kusznicy bez pawezy i przestrzeleni zasiegiem i tempem, doly przed frontem, szarze pojedyncze |
| Poitiers 1356 | Dwa wyborowe oddzialy konne na lucznikow | Kleska | Zywoploty, winnice, bagno; lucznicy przeszli na bok i strzelali koniom w boki i zady |
| Azincourt 1415 | Jazda ze skrzydel | Kleska | Lasy po obu stronach (nie da sie obejsc), bloto, pale, za malo jezdzcow; ranne konie wpadly w natarcie wlasnej piechoty |
| Nikopolis 1396 | Szarza na piesza lucznikow za polem pali | Kleska | Pod gore, pale, rycerze zsiadali je wyrywac, potem trafili na swiezy odwod sipahow ukryty za grzbietem |
| Formigny 1450 | Dwie kolubryny spoza zasiegu luku | Sukces | Lucznicy nie mogli odpowiedziec, wyszli z pozycji po dziala - opuscili umocnienia |
| Towton 1461 | Wlasni lucznicy z wiatrem w plecy | Sukces | Sniezyca w twarz przeciwnika: jego strzaly nie dolatywaly, wlasne tak; przeciwnik musial zejsc z pozycji i atakowac |
| Falkirk (faza 2) | Lucznicy przeciw nieruchomym schiltronom | Sukces | Cel stoi, nie ma tarcz ani strzelcow, zeby odpowiedziec |

Wniosek - szarza na strzelcow dziala, gdy jednoczesnie: (a) strzelcy nie maja przeszkody przed soba ani oparcia skrzydla, (b) nie stoi przy nich ciezka piechota, (c) szarza idzie z boku/tylu albo zanim sie ustawia, (d) grunt jest twardy i plaski. Brak ktoregokolwiek warunku - trzeba innego sposobu: wlasny ostrzal z wiekszego zasiegu, obejscie, natarcie piechoty pod oslona tarcz, albo po prostu nie atakowac tej pozycji.

Uwagi dodatkowe:
- Lucznicy po wystrzelaniu strzal wchodzili do walki wrecz na skrzydlach scisnietego przeciwnika (Azincourt). Strzelec bez amunicji to lekka piechota, nie stojacy cel.
- Zapas strzal to kilka-kilkanascie minut ciaglego ostrzalu; pod Towton Yorkisci zbierali strzaly przeciwnika. Ogien ciagly od poczatku bitwy do konca jest ahistoryczny.
- Kusznik bez pawezy przegrywa pojedynek z lucznikiem (Crecy). Kusznik za oslona jest grozny (Arsuf - kusznicy trzymali lucznikow konnych na dystans).

### 2.6 Piechota drzewcowa przeciw jezdzie
- Stoi, gdy jest zwarta, ma przeszkode lub oparcie i nie rusza w poscig: Courtrai (rowy i strumienie przed frontem, odwod Jana van Renesse zatkal wylom, zakaz brania jencow), Bannockburn dzien 1 (schiltron Moraya zatrzymal jazde Clifforda).
- Schiltron ruchomy w natarciu: Bannockburn dzien 2 - Szkoci sami ruszyli na Anglikow scisnietych miedzy strumieniami, ktorzy nie mieli miejsca ani na rozped, ani na wyprowadzenie lucznikow.
- Schiltron nieruchomy bez oslony ginie od ostrzalu: Falkirk - jazda najpierw rozpedzila szkocka jazde i lucznikow, potem Edward cofnal jazde, ostrzelal schiltrony i dopiero w wybite dziury wpuscil szarze.
- Bouvines: Renaud z Boulogne ustawil krag pikinierow jako schron, z ktorego jego rycerze robili wypady i do ktorego wracali odpoczac. Padl jako ostatni.
- Recepta na drzewce: nie szarzowac od czola; przyszpilic, ostrzelac, uderzyc w dziury albo w bok. Recepta drzewcow: zblizyc sie do strzelcow albo miec wlasnych, oprzec skrzydla, miec jazde do odpedzenia strzelcow.

### 2.7 Konni lucznicy stepu
Metoda (Dorylaeum, Hattin, Arsuf, Legnica, Mohi):
1. Otoczyc lub towarzyszyc z bokow, strzelac z dystansu, cofac sie przed szarza, wracac gdy sie skonczy.
2. Celowac w konie i w straz tylna (Hattin, Arsuf - tylna straz musiala isc tylem).
3. Oddzielic jazde od piechoty (Hattin, Legnica).
4. Odciac od wody, dym w oczy (Hattin - potwierdzone; Legnica [NIEPEWNE]).
5. Dopiero zmeczonego i rozciagnietego przeciwnika dobic ciezka jazda trzymana w odwodzie.
6. Mohi: przyszpilic od czola przy moscie (machiny miotajace oczyscily przyczolek), obejsc druga czescia przez rzeke ponizej, zamknac w obozie, zostawic luke do ucieczki i wybic uciekajacych w poscigu. Luka ma w wynikach wyszukiwania jedno zrodlo [NIEPEWNE co do szczegolow].

Kontra:
- Dorylaeum 1097: nie szarzowac w pustke; stanac w zwartym szyku przy przeszkodzie (bagno, oboz), piechota i tabor w srodku, wytrzymac do nadejscia glownych sil; rozstrzygnelo uderzenie oddzialu, ktory wyszedl zza wzgorz na tyly Turkow.
- Arsuf 1191: kolumna marszowa - piechota z kusznikami na zewnatrz jako tarcza, jazda w srodku, jedno skrzydlo oparte o morze. Szarza dopiero, gdy przeciwnik podszedl blisko i sie zaangazowal (tu wymuszona przedwczesnie przez joannitow, Ryszard poparl ja calym wojskiem). Krotki poscig, zatrzymanie, zebranie, powtorka.
- Hattin 1187 to katalog bledow: marsz bez wody, zatrzymanie sie na noc w otoczeniu, piechota porzucila jazde i uciekla na wzgorze, szarze przepuszczane przez rozstepujacy sie szyk (Rajmund z Trypolisu przebil sie i juz nie wrocil).

### 2.8 Hufce i odwody
- Standard: 3 hufce (przedni, glowny, tylny) ustawione obok siebie albo jeden za drugim, plus odwod przy wodzu.
- Wygrywa ten, kto ostatni ma swiezy oddzial:
  - Poitiers - maly oddzial (Captal de Buch, ok. 160 ludzi [NIEPEWNE co do liczby]) obszedl pole i pokazal sztandar na tylach w chwili czolowego kontrataku.
  - Grunwald - Jagiello dowodzil ze wzgorza i trzymal odwody; wielki mistrz poprowadzil swoj odwod (16 choragwi) osobiscie i w nim zginal.
  - Nikopolis - Bajazyd ukryl sipahow za grzbietem; ciezka jazda serbska dobila.
  - Courtrai - odwod zatkal wylom.
  - Formigny, Towton - posilek przychodzacy z boku w trakcie walki przesadza bitwe.
- Blad powtarzalny: hufce wchodza po kolei i sa bite pojedynczo (Crecy, Poitiers, Azincourt, Nikopolis - glowne sily Zygmunta za daleko za straza przednia). Pod Poitiers jeden caly hufiec odszedl z pola bez walki, pod Azincourt trzecia linia.
- Bouvines: przed szarza rycerstwa wyslano lekka jazde (konnych serwientow), zeby rozbila szyk i zmeczyla przeciwnika; linie rozciagnieto, zeby nie dac sie oskrzydlic; slonce w plecy.

### 2.9 Wodz i sztandar
- Sztandar to punkt zbiorki i sygnal "walczymy dalej". Jego upadek = sygnal do ucieczki. Grunwald: choragiew krakowska upadla i zostala natychmiast podniesiona - kryzys zazegnany. Hattin: koniec, gdy padl namiot krolewski i stracono Krzyz. Bouvines: zdobycie sztandaru cesarskiego po ucieczce Ottona.
- Smierc, niewola lub ucieczka wodza konczy bitwe: Harold (Hastings), Henryk Pobozny (Legnica), Ulrich von Jungingen (Grunwald), Jan II w niewoli (Poitiers), Edward II wyprowadzony z pola (Bannockburn) - po tym rozpad nastepuje w minuty.
- Wodz w pierwszej linii podnosi morale, ale traci kontrole (Filip August zrzucony z konia pod Bouvines, uratowany przez przybocznych). Wodz z tylu steruje odwodami (Jagiello, Ryszard, Edward III na wzgorzu pod Crecy).
- Dowodcy hufcow dzialaja samowolnie: joannici pod Arsuf, Artois pod Courtrai, straz przednia pod Nikopolis. Niesubordynacja to realny, czesty czynnik - nadaje sie na ceche wodza AI.

### 2.10 Zalamanie i poscig
- Wiekszosc zabitych pada w ucieczce, zwlaszcza na przeszkodzie za plecami: Towton (rzeczka Cock Beck), Bannockburn (strumien), Mohi (droga ucieczki), Courtrai (rowy).
- Zalamanie zaczyna sie od skrzydla lub tylu i idzie fala; wyzwalacze: uderzenie w bok/tyl, upadek sztandaru, widok uciekajacych, pojawienie sie nowego wojska (takze pozornego - ciury pod Bannockburn wziete za odwod; konie bez jezdzcow wracajace spod Nikopolis wywolaly odwrot sojusznikow).
- Zdyscyplinowany zwyciezca sciga czescia sil (jazda), reszta stoi w szyku. Poscig calym wojskiem przegrywal bitwy: Hastings (Anglosasi), Grunwald (krzyzackie skrzydlo), Verneuil (Lombardczycy), Legnica.
- Otoczeni bez drogi ucieczki walcza do konca (Szkoci pod Verneuil, przyboczni Harolda) - stad mongolska "luka" pod Mohi.
- Tabor to magnes: Verneuil, Azincourt (napad na tabor -> rozkaz zabicia jencow), Grunwald (szturm obozu-taboru na koniec).

### 2.11 Teren
| Teren | Wykorzystanie | Przyklad |
|---|---|---|
| Wzgorze | Obrona na grzbiecie; odwod za grzbietem, niewidoczny | Hastings, Crecy, Nikopolis |
| Las | Oparcie skrzydel, zwezenie pola, ukrycie oddzialu, cien dla czekajacych | Azincourt, Grunwald, Towton (zasadzka z lasu [NIEPEWNE]) |
| Bagno / bloto / orka po deszczu | Zatrzymuje szarze, meczy spieszonych | Azincourt, Falkirk (bagno przed frontem - jazda objechala), Poitiers, Dorylaeum |
| Rzeka / strumien / row | Przeszkoda przed frontem; smiertelna pulapka za plecami pokonanego | Courtrai, Bannockburn, Mohi, Towton |
| Zywoploty, winnice | Kanalizuja atak w waskie przejscia | Poitiers |
| Przeszkody sztuczne | Pale, doly, tabor z wozow | Crecy, Azincourt, Nikopolis, Mohi |
| Pogoda | Slonce i wiatr w plecy; snieg w oczy przeciwnika; upal i brak wody | Bouvines, Towton, Hattin |
| Zwezenie pola | Neutralizuje przewage liczebna, scisk uniemozliwia uzycie broni | Azincourt, Bannockburn |

## 3. Bitwy w skrocie

| Bitwa | Co rozstrzygnelo | Lekcja dla AI |
|---|---|---|
| Hastings 1066 | Poscig wyciagnal obroncow ze wzgorza; smierc krola | Obronca na wzgorzu nie sciga; atakujacy rotuje ostrzal - piechota - jazda |
| Dorylaeum 1097 | Wytrwanie w obronie + odsiecz na tyly | Przeciw konnym lucznikom: stac zwarto, czekac na druga czesc wojska |
| Hattin 1187 | Wyczerpanie, rozdzielenie jazdy i piechoty | Nekanie zamiast zwarcia; piechota bez jazdy i jazda bez piechoty gina osobno |
| Arsuf 1191 | Szarza w chwili, gdy nekajacy podeszli za blisko; kontrolowany poscig | Czekac z szarza; scigac krotko, zbierac sie, powtarzac |
| Bouvines 1214 | Wygrane skrzydla, ucieczka cesarza | Lekka jazda zmiekcza przed ciezka; krag pikinierow jako baza wypadowa |
| Legnica 1241 | Pozorowany odwrot, rozdzielenie, uderzenie z bokow | Jazda nie oddala sie od piechoty za uciekajacymi |
| Mohi 1241 | Przyszpilenie + obejscie przez rzeke, zamkniecie w obozie | Atak z dwoch kierunkow; zostawic droge ucieczki i wybic w poscigu |
| Falkirk 1298 | Odpedzenie jazdy i lucznikow, ostrzal nieruchomych schiltronow | Najpierw zdjac oslone, potem strzelac, szarza na koniec |
| Courtrai 1302 | Szarza przez rowy na zwarta piechote, brak miejsca na odwrot | Nie odwolywac strzelcow, ktorzy wygrywaja; nie szarzowac przez przeszkode |
| Bannockburn 1314 | Scisk miedzy strumieniami, lucznicy rozpedzeni jazda, wyjazd krola | Piechota drzewcowa moze nacierac; wysunietych strzelcow bic z boku lekka jazda |
| Crecy 1346 | Pozycja + ostrzal przeciw nieskoordynowanym szarzom | Nie atakowac z marszu, pod wieczor, falami |
| Poitiers 1356 | Hufce pobite po kolei, manewr na tyly + kontratak | Maly oddzial na tylach w chwili natarcia od czola |
| Nikopolis 1396 | Straz przednia wyczerpana na palach, ukryty odwod | Nie wyprzedzac glownych sil; za grzbietem moze stac odwod |
| Grunwald 1410 | Zmeczenie Zakonu, odwody Jagielly, powrot Litwinow, otoczenie | Wodz steruje odwodami z tylu; skrzydlo w poscigu = skrzydlo stracone |
| Azincourt 1415 | Bloto, zwezenie, pale, scisk | Obronca moze podejsc na zasieg i sprowokowac atak |
| Verneuil 1424 | Jazda przelamala lucznikow, ale wyszla z bitwy | Po przelamaniu zawrocic na tyly linii, nie na tabor |
| Patay 1429 | Natychmiastowe uderzenie na nieustawionych lucznikow | Szybkosc wazniejsza niz zebranie calosci, gdy cel jest nieprzygotowany |
| Formigny 1450 | Dziala spoza zasiegu wyciagnely lucznikow, posilek z boku | Bron o wiekszym zasiegu zmusza obronce do wyjscia |
| Towton 1461 | Wiatr i snieg, wymuszony atak, posilek na skrzydlo, rzez w poscigu | Pogoda i przewaga ostrzalu decyduja, kto musi atakowac |

Sporne szczegoly Formigny: kto dowodzil dzialami (Clermont czy Bureau) i dokladna kolejnosc "zdobycie dzial - przyjscie Richemonta" - zrodla sie roznia [NIEPEWNE].

## 4. Reguly "warunek -> decyzja" dla wodza AI

Atak na strzelcow:
1. Strzelcy wroga zadaja wyrazne straty, nie maja przed soba przeszkody ani ciezkiej piechoty obok, grunt plaski -> jazda uderza w nich z boku lub tylu, lukiem poza zasiegiem (Bannockburn, Patay).
2. To samo, ale strzelcy stoja za przeszkoda / na stoku / wtopieni miedzy piechote -> nie szarzowac od czola. Do wyboru: wlasni strzelcy na pojedynek, natarcie piechoty z tarczami, obejscie, cofniecie sie poza zasieg i wymuszenie ruchu na przeciwniku (Crecy, Azincourt jako przestroga).
3. Strzelcy wroga dopiero sie rozwijaja albo sa w marszu -> natychmiastowa szarza tym, co jest pod reka (Patay).
4. Po rozbiciu strzelcow jazda zbiera sie i uderza w tyl linii piechoty; nie sciga rozproszonych i nie jedzie dalej (Verneuil).

Obrona wlasnych strzelcow:
5. Jazda wroga kieruje sie na naszych strzelcow -> strzelcy cofaja sie za piechote / na przeszkode, piechota drzewcowa wysuwa sie na bok, wlasna jazda kontrszarzuje w bok szarzy.
6. Strzelcy bez amunicji -> dolaczaja do walki wrecz na skrzydlach.

Wybor postawy:
7. Mamy przewage ostrzalu -> stoimy na pozycji, niech przeciwnik podchodzi (Towton, Crecy). Jesli nie chce podejsc -> podejsc strzelcami na zasieg i sprowokowac (Azincourt, Towton).
8. Przeciwnik ma przewage ostrzalu, a my stoimy -> nie stac bezczynnie; nacierac szybko albo wycofac sie poza zasieg / za zaslone terenu (Falkirk i Formigny jako przestroga).
9. Slabsi liczebnie -> wzgorze lub zwezenie, skrzydla oparte, jazda czesciowo spieszona, maly konny odwod.

Przeciw piechocie drzewcowej / scianie tarcz:
10. Nie szarzowac od czola. Kolejnosc: odpedzic jej jazde i strzelcow -> ostrzelac -> szarza w przerzedzone miejsce lub w bok (Falkirk).
11. Probowac wyciagnac ja z pozycji pozorowanym odwrotem jazdy; uderzyc w scigajacych, gdy zejda z terenu (Hastings).

Przeciw konnym lucznikom:
12. Nie gonic ciezka jazda w otwartym polu. Szyk zwarty, strzelcy piesi na zewnatrz, skrzydlo oparte o przeszkode; szarza dopiero, gdy podejda na kilkadziesiat metrow albo zaangazuja sie w zwarcie; poscig krotki, powrot do szyku (Arsuf, Dorylaeum).

Konnymi lucznikami:
13. Nekac skrzydla i tyl, celowac w konie, cofac sie przed szarza, wracac. Ciezka jazda w odwodzie wchodzi dopiero, gdy przeciwnik jest rozciagniety albo jego jazda oddzielila sie od piechoty (Legnica, Hattin).

Odwod i kryzys:
14. Trzymac 1/4-1/3 sil (najlepiej jazde) poza walka. Rzucic: w wylom wlasnej linii (Courtrai), w bok przeciwnika gdy jego ostatni odwod juz wszedl (Poitiers, Grunwald), albo jako oslone odwrotu.
15. Hufce wchodza razem albo na tyle blisko, by sie wspierac - nie po kolei (Crecy, Poitiers, Nikopolis jako przestroga).
16. Sztandar/wodz wroga odsloniety -> skoncentrowane uderzenie w ten punkt (Hattin, Bouvines, Poitiers).
17. Wlasne skrzydlo peka -> odwod tam, nie do srodka; jesli odwodu nie ma - cofnac linie do przeszkody.

Poscig:
18. Po zalamaniu przeciwnika sciga jazda i lekkie oddzialy; piechota i strzelcy trzymaja szyk, dopoki na polu jest jakikolwiek zwarty oddzial wroga.
19. Obronca na dobrej pozycji nie sciga pierwszego odwrotu - moze byc pozorowany. Wyjatek jako cecha charakteru: wodz porywczy sciga (i wpada).

Pauzy:
20. Po nieudanym natarciu oddzial odstepuje poza zasieg, porzadkuje szyk i wraca - zamiast walczyc do zera albo od razu uciekac.

## 5. Zroznicowanie bitew - plany wodza do losowania wedlug skladu, terenu i charakteru

| Plan | Wzor | Kiedy |
|---|---|---|
| Kotwica i skrzydla strzelcow | Crecy, Azincourt | Duzo strzelcow, jest wzgorze lub zwezenie |
| Mur i cierpliwosc | Hastings (obrona), Courtrai | Duzo piechoty, malo jazdy |
| Jez w natarciu | Bannockburn | Piechota drzewcowa, przeciwnik scisniety lub bez strzelcow |
| Zmiekczenie i mlot | Bouvines, Falkirk | Sily mieszane: lekkie oddzialy i ostrzal najpierw, ciezka jazda na koniec |
| Nekanie i rozdzielenie | Hattin, Legnica | Przewaga strzelcow konnych |
| Przyszpilenie i obejscie | Mohi, Poitiers, Formigny | Przewaga jazdy lub ruchliwosci; czesc sil wiaze front |
| Blyskawiczne uderzenie | Patay | Przeciwnik nieustawiony, mamy jazde pod reka |
| Pozorowany odwrot | Hastings, Legnica | Przeciwnik na pozycji, znany z porywczosci |
| Ukryty odwod | Nikopolis, Grunwald | Jest grzbiet lub las do schowania jazdy |
| Kolumna obronna | Arsuf, Dorylaeum | Przeciwnik ma konnych lucznikow, my piechote z kusznikami |

Cechy wodza warte odwzorowania, bo historycznie decydowaly: ostrozny (Jagiello, Ryszard) / porywczy (Artois, straz przednia pod Nikopolis); posluch u podwladnych wysoki / niski (samowolne szarze); wodz w pierwszej linii / z tylu przy odwodzie.

## 6. Najczestsze "glupie" zachowania, ktorych AI nie powinno robic bez powodu

1. Szarza od czola na strzelcow za przeszkoda lub pod gore.
2. Jazda stoi bezczynnie, gdy strzelcy wroga stoja odslonieci.
3. Piechota stoi w ostrzale bez tarcz i bez ruchu.
4. Cale wojsko rusza w poscig, lamiac szyk.
5. Oddzialy wchodza do walki pojedynczo.
6. Brak odwodu - wszystko rzucone w pierwszej minucie.
7. Jazda po przelamaniu znika z pola.
8. Jazda goni konnych lucznikow w nieskonczonosc.
9. Strzelcy stoja w miejscu, gdy jedzie na nich jazda, choc obok jest wlasna piechota.
10. Walka do ostatniego czlowieka bez prob odstapienia i przegrupowania.

Kazdy z tych bledow ma historyczny pierwowzor, wiec moze sie zdarzac jako skutek charakteru wodza (porywczy, nieposluszni podwladni), ale nie jako zachowanie domyslne.

## 7. Zrodla sprawdzone w sieci

- Verneuil: https://en.wikipedia.org/wiki/Battle_of_Verneuil , https://britishbattles.com/one-hundred-years-war/battle-of-verneuil
- Patay: https://en.wikipedia.org/wiki/Battle_of_Patay , https://britishbattles.com/one-hundred-years-war/battle-of-patay , https://www.jeanne-darc.info/location-and-places/the-battle-of-patay-1429/
- Formigny: https://en.wikipedia.org/wiki/Battle_of_Formigny , https://britishbattles.com/one-hundred-years-war/battle-of-formigny , https://warhistory.org/2015/11/30/battle-of-formigny-1450/
- Mohi: https://newworldencyclopedia.org/entry/Battle_of_Mohi , https://britannica.com/print/article/2096032

Pozostale 15 bitew - z wiedzy wlasnej, bez weryfikacji w sieci w tej sesji. Najslabiej udokumentowane szczegoly: Legnica (dym, okrzyk), Grunwald (charakter odwrotu Litwinow), Towton (zasadzka z lasu), Mohi (luka w okrazeniu), Poitiers (liczebnosc oddzialu obchodzacego), Hastings (ostrzal stromotorowy pod koniec - pominiety jako tradycja).
