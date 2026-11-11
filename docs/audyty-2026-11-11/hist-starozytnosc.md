# Bitwy starozytnosci - material dla projektanta AI bitewnego

Zakres: 12 bitew (490 p.n.e. - 378 n.e.), przelozone na fazy, decyzje wodza i warunki powodzenia/porazki.
Zrodla: pisane glownie z wiedzy (Herodot, Ksenofont, Arrian, Polibiusz, Liwiusz, Cezar, Plutarch, Ammian);
cztery punkty sprawdzone w sieci (Carrhae, Farsalos, Kynoskefalaj, Adrianopol) - linki na koncu.
Liczby strat w zrodlach antycznych sa zawyzone/sporne - traktowac jako rzad wielkosci. Miejsca niepewne oznaczone [?].

## 1. Wspolny schemat bitwy (fazy)

| Faza | Co sie dzieje | Kto decyduje |
|---|---|---|
| 0. Ustawienie | Piechota ciezka w centrum, jazda na skrzydlach, lekkozbrojni z przodu, rezerwa/druga linia z tylu. Skrzydlo opiera sie o przeszkode (rzeka, wzgorze, morze) albo jest "odmowione" (cofniete skosem). | Wodz - to jest 70% jego pracy. Po starciu ma malo kontroli. |
| 1. Harcownicy | Procarze, oszczepnicy, lucznicy ostrzeliwuja sie 10-30 min, potem cofaja sie PRZEZ odstepy wlasnej linii albo na skrzydla. Rzadko rozstrzygaja; maja zmeczyc, rozdraznic, zaslonic manewr, zabic slonie/rydwany. | Automatycznie; wycofanie gdy linie zblizaja sie na ~50-100 m. |
| 2. Walka jazdy na skrzydlach | Zaczyna sie rownoczesnie z faza 1 lub przed starciem piechoty. Zwyciezca ma wybor: poscig za pobita jazda (blad czesty) albo zwrot na tyl piechoty (rozstrzyga bitwe). | Dowodca skrzydla; najwazniejsza pojedyncza decyzja w bitwie. |
| 3. Starcie piechoty | Dluga walka na froncie, MALE straty (kilka % ), linie faluja, zmeczenie. Trwa od kilkunastu minut do kilku godzin. | Tu wchodza rezerwy: luzowanie linii, latanie dziur. |
| 4. Rozstrzygniecie | Uderzenie w bok/tyl, smierc lub ucieczka wodza, przerwanie linii w jednym miejscu. Zalamanie jest NAGLE i zarazliwe - zaczyna sie od tylnych szeregow i sasiadow pobitego oddzialu. | Ten, kto ma jeszcze wolny, uporzadkowany oddzial. |
| 5. Poscig | Tu ginie 80-90% wszystkich zabitych. Zwyciezca traci zwykle 1-5%, pokonany 15-50%+ (okrazony - do 70-80%). Zabija jazda i lekkozbrojni; ciezka piechota nie dogoni. | Wodz: gonic (zniszczyc armie) czy trzymac szyk (ryzyko kontry). |

Wniosek dla AI: bitwa to nie "wszyscy na wszystkich", tylko seria pojedynkow na odcinkach, w ktorej wygrywa ten,
kto pierwszy uwolni jeden oddzial i skieruje go w bok/tyl przeciwnika zwiazanego od frontu.

## 2. Bitwy - konkret

### Maraton 490 p.n.e. (Ateny vs Persja) - szybkie zblizenie pod ostrzalem
- Ustawienie: Grecy (~10 tys. hoplitow) rozciagneli linie na szerokosc perskiej: centrum cienkie (kilka szeregow), skrzydla glebokie. Persowie: lucznicy/piechota z plecionymi tarczami, najlepsi (Persowie, Sakowie) w centrum; jazda perska nieobecna lub spozniona [?].
- Decyzja: ostatni odcinek (zasieg lukow, ~150-200 m) pokonany biegiem; Herodot podaje 8 stadiow (~1,5 km), co jest watpliwe w pelnym uzbrojeniu [?].
- Przebieg: centrum greckie przelamane i cofniete; oba skrzydla wygraly, NIE poszly w poscig, tylko zwrocily sie do srodka na zwycieskie centrum perskie.
- Straty: 192 Ateny vs ~6400 Persow (Herodot) - wiekszosc przy ucieczce do okretow i w bagnie.
- Lekcja: przeciw strzelcom piesi skracaja czas pod ostrzalem biegiem; slabe centrum + mocne skrzydla = dwustronne oskrzydlenie; warunek - skrzydla po zwyciestwie zawracaja do srodka.

### Leuktry 371 p.n.e. (Teby vs Sparta) - szyk skosny
- Ustawienie: Epaminondas skupil na LEWYM skrzydle kolumne 50 szeregow (norma 8-12) ze Swietym Zastepem na czele, naprzeciw krola Sparty i elity (12 szeregow). Reszta linii (centrum, prawe) cofnieta schodkowo - miala NIE walczyc.
- Wstep: jazda starla sie przed frontem; lepsza jazda tebanska wpedzila spartanska we wlasna falange, robiac zamet.
- Rozstrzygniecie: glebokie skrzydlo zlamalo elite, krol Kleombrotos zginal; sojusznicy Sparty, ktorych nikt nie zaatakowal, odeszli. Padlo ~400 z 700 Spartiatow.
- Lekcja: przewaga lokalna w jednym punkcie (na najlepszy oddzial i wodza wroga), reszta "odmawia" walki. Warunek: slabe skrzydlo musi naprawde unikac starcia, a jazda oslaniac jego bok.

### Issos 333 p.n.e. (Aleksander vs Dariusz III) - mlot i kowadlo w ciasnym terenie
- Teren: waska rownina miedzy morzem a gorami - przewaga liczebna Persow bez znaczenia.
- Ustawienie: falanga w centrum (kowadlo), hetajrowie z Aleksandrem na prawym, Parmenion z jazda tesalska na lewym przy morzu (zadanie: tylko wytrzymac). Persowie za rzeka Pinaros, masa jazdy nad morzem, oddzial w gorach na flance Aleksandra (odpedzony lekka piechota - mala oslona wystarczyla).
- Przebieg: Aleksander szybko przeszedl rzeke pod ostrzalem, zlamal lewe skrzydlo perskie i skrecil w strone Dariusza. W falandze przy przeprawie powstala dziura, w ktora weszli greccy najemnicy Dariusza - kryzys; uratowal go zwrot zwycieskiego prawego skrzydla w bok najemnikow. Dariusz uciekl -> jazda perska, dotad wygrywajaca nad morzem, zawrocila.
- Lekcja: skrzydlo uderzeniowe po przelamaniu skreca do srodka (na wodza / bok centrum), nie goni. Ucieczka wodza = zalamanie calej armii, nawet oddzialow wygrywajacych.

### Gaugamela 331 p.n.e. - jak slabszy liczebnie nie da sie oskrzydlic
- Problem: otwarta rownina, Persowie duzo szersi, rydwany z kosami, masa jazdy.
- Ustawienie Aleksandra: (1) DRUGA LINIA piechoty z rozkazem zwrotu w tyl przy okrazeniu; (2) oba skrzydla cofniete skosem, z oslonami bocznymi (jazda + lekka piechota przemieszane); (3) marsz calym szykiem ukosnie w prawo - poza przygotowany dla rydwanow teren.
- Przebieg: Bessos rzucil jazde na obejscie prawego skrzydla; Aleksander karmil te walke MALYMI oddzialami po kolei, nie angazujac hetajrow. Rydwany: oszczepnicy zabijali woznicow i konie, szeregi rozstapily sie w korytarze. Przesuniecie Persow w lewo otworzylo LUKE w ich linii - w nia klin hetajrow + czesc falangi prosto na Dariusza, ktory uciekl.
- Blad Persow: jazda, ktora przebila sie przez luke w falandze macedonskiej, pojechala rabowac oboz zamiast uderzyc falange w plecy. Lewe skrzydlo Parmeniona bylo bliskie kleski; Aleksander musial przerwac poscig [szczegoly sporne].
- Lekcja: rezerwa odwrocona tylem i odmowione skrzydla neutralizuja oskrzydlenie; uderzenie rozstrzygajace czeka na luke, ktora przeciwnik sam zrobi manewrujac; jazda po przelamaniu ma tendencje do rabunku obozu - to slabosc historyczna, mozna ja dac slabym wodzom AI.

### Hydaspes 326 p.n.e. (Aleksander vs Poros) - jazda i slonie
- Wstep: przeprawa noca 27 km w gore rzeki, pozorujac przez wiele dni przeprawy gdzie indziej; czesc armii (Krateros) zostala naprzeciw obozu Porosa, by go wiazac.
- Ustawienie Porosa: slonie co ~15-30 m przed piechota (konie macedonskie nie pojda na slonie), jazda i rydwany na skrzydlach.
- Przebieg: Aleksander NIE atakuje frontu. Najpierw 1000 lucznikow konnych ostrzeliwuje jazde indyjska na jednym skrzydle, potem szarza hetajrow; Kojnos z czescia jazdy obchodzi i wychodzi na tyl jazdy indyjskiej [ktorym skrzydlem - sporne]. Jazda Porosa spychana na wlasne slonie. Falanga i lekkozbrojni: strzaly i oszczepy w kornakow i oczy sloni, rozstepowanie sie przed szarza i zamykanie; ranne slonie w scisku tratuja wlasnych. Okrazenie; swiezy korpus Krateros przeprawia sie i przejmuje poscig.
- Lekcja: bron "specjalna" (slonie, tu: cokolwiek, czego konie/piechota nie zniosa czolowo) zwalcza sie ostrzalem i stloczeniem, nie czolowo. Strzelcy konni jako przygotowanie szarzy ciezkiej jazdy. Poscig prowadzi SWIEZY oddzial.

### Kanny 216 p.n.e. (Hannibal vs Rzym) - podwojne okrazenie
- Ustawienie Rzymian: ~80 tys. piechoty w szyku glebszym i wezszym niz zwykle (chcieli przebic centrum), jazda slaba na obu skrzydlach (~6 tys. vs ~10 tys.).
- Ustawienie Hannibala: centrum z Galow i Iberow WYSUNIETE lukiem do przodu (ma sie cofac, nie pekac), Afrykanie (najlepsza piechota) w kolumnach na obu koncach, cofnieci. Ciezka jazda Hazdrubala na lewym (przy rzece), Numidowie na prawym.
- Przebieg: (1) Hazdrubal rozbija jazde rzymska przy rzece; (2) NIE goni, objezdza tyl Rzymian i uderza od tylu jazde sprzymierzona, ktora Numidowie tylko wiazali harcami; (3) Numidowie przejmuja poscig, Hazdrubal zawraca na tyl piechoty. W tym czasie centrum cofa sie, Rzymianie wlewaja sie w worek, traca szyk i miejsce do wladania bronia; Afrykanie robia zwrot do srodka na oba boki.
- Straty: 48-70 tys. Rzymian (Liwiusz/Polibiusz) vs ~6-8 tys. Hannibala. Zabici glownie w scisku po zamknieciu kotla.
- Warunki powodzenia: przewaga jazdy; centrum, ktore ustepuje bez paniki (Hannibal stal tam osobiscie); dyscyplina jazdy - trzy kolejne zadania zamiast poscigu; przeciwnik prze slepo naprzod. Bez ktoregokolwiek z nich centrum pekloby i byloby odwrotnie (Trebia: 10 tys. Rzymian przebilo sie wlasnie srodkiem).

### Zama 202 p.n.e. (Scypion vs Hannibal) - kontra na Kanny
- Slonie (80) na czele Hannibala: Scypion ustawil manipuly jeden za drugim (korytarze zamiast szachownicy), welici w korytarzach; traby i krzyk splosszyly czesc sloni na wlasna jazde kartaginska, reszta przebiegla korytarzami.
- Jazda: Leliusz i Masynissa (przewaga po stronie Rzymu) zmiatali oba skrzydla i POJECHALI W POSCIG - pole na dluzszy czas zostalo bez jazdy. Mozliwe, ze jazda Hannibala celowo odciagala [hipoteza nowozytna].
- Piechota Hannibala w 3 liniach: najemnicy, obywatele, a weterani z Italii ~200 m z tylu jako prawdziwa rezerwa, ktora nie wpuszczala uciekajacych w swoje szeregi (kazala im obchodzic skrzydlami).
- Decyzja Scypiona: po rozbiciu dwoch linii ZATRZYMAL walke sygnalem, uporzadkowal szyk, przesunal principes i triarii na skrzydla (wydluzenie linii przeciw oskrzydleniu) i dopiero uderzyl. Walka rowna, dopoki jazda nie wrocila z poscigu na tyl weteranow.
- Lekcja: wodz przerywa natarcie, by sie przeformowac; rezerwa trzymana daleko nie daje sie porwac panice; poscig jazdy to ryzyko - bitwa wisiala na tym, czy zdazy wrocic.

### Kynoskefalaj 197 p.n.e. (Rzym vs Macedonia) - bitwa spotkaniowa
- Poczatek: mgla, starcie oslon na grzbiecie wzgorz, obie strony dosylaja posilki, walka eskaluje bez planu.
- Przebieg: prawe skrzydlo falangi (Filip V) zdazylo sie rozwinac, szarzowalo z gory i spychalo rzymskie lewe. Lewe skrzydlo macedonskie dopiero wchodzilo na grzbiet w szyku marszowym - Flamininus uderzyl na nie prawym skrzydlem ze sloniami i rozbil, zanim sie sformowalo.
- Rozstrzygniecie: bezimienny trybun z wlasnej inicjatywy zabral 20 manipulow (~2-2,5 tys.) ze zwycieskiego skrzydla i uderzyl w TYL wygrywajacej falangi Filipa. Falanga z sarisami nie obroci sie.
- Lekcja: atakuj oddzial w marszu/nieuformowany natychmiast; inicjatywa nizszego dowodcy (formacja AI po wygraniu swojego odcinka sama szuka tylu sasiedniego wroga); formacja "frontowa" (piki, mur tarcz) jest bezbronna z tylu.

### Pydna 168 p.n.e. - teren rozrywa szyk
- Start: przypadkowa bojka przy rzece. Falanga Perseusza ruszyla szybko i na rownym terenie spychala legiony - Emiliusz Paulus przyznal potem, ze widok go przerazil.
- Zwrot: w miare natarcia na nierowny teren pod wzgorzami front falangi poszarpal sie w luki. Paulus rozkazal manipulom dzialac OSOBNO i wciskac sie w luki - z bliska miecz i duza tarcza wygrywa z pikinierem. Slonie i sprzymierzeni zlamali lewe skrzydlo macedonskie. Jazda macedonska odjechala prawie bez walki.
- Straty: ~20 tys. Macedonczykow zabitych w ok. godzine, Rzymian ~100 [zanizone].
- Lekcja: zwarta formacja jest silna tylko na rownym i dopoki stoi lub idzie wolno; cofanie sie na trudny teren to legalna taktyka przeciw niej; dzielenie wlasnej linii na male oddzialy do infiltracji.

### Farsalos 48 p.n.e. (Cezar vs Pompejusz) - rezerwa przeciw jezdzie
- Sily: Pompejusz ~2x wiecej piechoty i ~7 tys. jazdy vs ~1 tys. Cezara. Jedno skrzydlo obu armii oparte o rzeke, cala jazda Pompejusza (Labienus) + lucznicy i procarze na otwartym skrzydle. Plan: zmiesc jazde Cezara, wejsc na tyl.
- Kontra Cezara: wyciagnal 6 kohort (wg Cezara; inne zrodla 8) z trzeciej linii i ustawil jako CZWARTA linie skosem za wlasna jazda, ukryta. Rozkaz: nie rzucac pilum, tylko klus jak wlocznia (wg Plutarcha - w twarze jezdzcow [szczegol z Plutarcha, nie z Cezara]).
- Przebieg: jazda Cezara ustapila zgodnie z planem; rozpedzona, nieuporzadkowana jazda Labienusa wpadla na czwarta linie, spanikowala i uciekla; kohorty wyciely pozostawionych lucznikow i procarzy, obeszly lewe skrzydlo Pompejusza. Wtedy Cezar wprowadzil swieza trzecia linie od frontu.
- Szczegol: Pompejusz kazal piechocie STAC i przyjac szarze (by wrog zmeczyl sie biegiem); weterani Cezara sami staneli w pol drogi, odetchneli, wyrownali i dopiero uderzyli.
- Lekcja: piechota z bronia drzewcowa, zwarta i nieoczekiwana, zatrzymuje jazde; strzelcy bez oslony po ucieczce wlasnej jazdy sa martwi; rezerwe (trzecia linia) wprowadza sie dopiero w chwili kryzysu wroga.

### Carrhae 53 p.n.e. (Krassus vs Surena) - dlaczego szarza na strzelcow konnych zawiodla
- Sily: ~35-40 tys. Rzymian (7 legionow, ~4 tys. jazdy, ~4 tys. lekkich) vs ~9 tys. lucznikow konnych + 1 tys. katafraktow + ~1 tys. wielbladow z zapasem strzal.
- Teren: otwarta, bezwodna rownina; Rzymianie zmeczeni marszem, Krassus nie dal odpoczac.
- Przebieg:
  1. Krassus zwinal linie w wielki czworobok (bezpieczny, ale bierny, ciasny cel).
  2. Katafrakci podeszli, uznali szyk za za gleboki do zlamania i odstapili; lucznicy konni otoczyli czworobok i strzelali z dystansu.
  3. Wypady lekkozbrojnych - odparte; lucznicy konni cofali sie strzelajac w tyl.
  4. Rzymianie liczyli, ze strzaly sie skoncza - wielblady donosily zapas (kluczowa innowacja Sureny).
  5. Testudo chronilo przed strzalami, ale stloczonych i nieruchomych atakowali katafrakci; rozluznienie szyku = znow strzaly. Kombinacja dwoch broni, na ktora jedna formacja nie ma odpowiedzi.
  6. Wypad Publiusza Krassusa: 1300 jazdy (w tym 1000 Galow), 500 lucznikow, 8 kohort. Partowie UDALI UCIECZKE, odciagneli go poza zasieg wsparcia, zawrocili; katafrakci zwiazali od frontu, lucznicy konni okrazyli. Lekka jazda galijska bez pancerza przeciw kopiom katafraktow - wybita, piechota na pagorku wystrzelana. Glowe Publiusza pokazano armii.
  7. Noc: odwrot, porzucenie rannych; Krassus zabity przy rokowaniach. ~20 tys. zabitych, ~10 tys. jencow.
- Dlaczego szarza zawiodla (warunki):
  - cel szybszy od scigajacego i nie musi przyjac walki - szarza trafia w pustke;
  - scigajacy odrywa sie od piechoty/strzelcow, ktorzy go oslaniali;
  - wrog ma ciezka jazde jako "kowadlo" do kontry na rozciagnieta, zmeczona grupe;
  - brak przeszkody terenowej, do ktorej mozna strzelcow przycisnac;
  - wlasnych strzelcow za malo i o mniejszym zasiegu.
- Kiedy szarza na strzelcow DZIALA: strzelcy piesi (nie uciekna), bez oslony wlasnej jazdy/piechoty (Farsalos), przyparci do rzeki/obozu/wlasnej piechoty, albo gdy scigajacy trzyma sie w zasiegu wsparcia i wraca po krotkim wypadzie. Pozniejsza rzymska recepta (Wentydiusz 39-38 p.n.e., Arrian przeciw Alanom): masa wlasnych procarzy/lucznikow, pozycja na wzgorzu, gleboki szyk z wloczniami, jazda trzymana ZA piechota do krotkiej kontry.

### Adrianopol 378 n.e. (Walens vs Goci) - bitwa wydana za wczesnie
- Decyzja: Walens nie czekal na armie Gracjana (zawisc + bledny meldunek o 10 tys. Gotow).
- Przebieg: marsz ~13 km w sierpniowym upale bez posilku; Goci w kregu wozow na wzgorzu, podpalaja trawe, przeciagaja ROKOWANIA, czekajac na powrot jazdy (Alateus, Safraks) z furazu [czy to byl celowy plan, czy przypadek - sporne]. Rzymianie rozwijaja sie z kolumny marszowej: prawe skrzydlo juz stoi, lewe jeszcze dochodzi. Dwa oddzialy (scutarii, lucznicy Bakuriusza) atakuja bez rozkazu i zostaja odrzucone. Wraca jazda gocka i uderza "jak piorun"; zrodla wtorne nie sa zgodne, w ktore skrzydlo najpierw [?]. Jazda rzymska lewego skrzydla doszla do wozow, nie dostala wsparcia, zostala rozbita; odslonieta piechota scisnieta tak, ze nie mogla dobyc mieczy (Ammian); piechota gocka wychodzi z taboru. Rezerwa (Batawowie) juz uciekla, gdy jej szukano. Ginie cesarz i ~2/3 armii.
- Lekcja: nie atakuj w trakcie rozwijania sie; oddzial wchodzacy do walki bez rozkazu zrywa plan; powrot nieobecnej jazdy w bok = Kanny bez planowania; armia stloczona nie walczy.

## 3. Wzorce do zakodowania

### 3.1 Jazda
1. Pierwsze zadanie: pobic jazde przeciwnika na swoim skrzydle (nie atakowac czolowo zwartej piechoty).
2. Po zwyciestwie rzut "dyscypliny" zalezny od wodza/kultury: (a) zwrot na tyl piechoty zwiazanej od frontu - rozstrzyga (Kanny, Issos); (b) poscig za jazda - zostawia pole (Zama, wraca pozno); (c) rabunek obozu (Gaugamela - Persowie).
3. Jazda lekka/strzelcy konni: wiazac i nekac, nie przyjmowac szarzy, udawana ucieczka -> kontra, gdy scigajacy oderwie sie od wsparcia (Carrhae).
4. Ciezka jazda szarzuje, gdy cel jest: w ruchu/nieuformowany, zwrocony bokiem/tylem, rozluzniony ostrzalem, albo to strzelcy piesi bez oslony.

### 3.2 Reakcja na ostrzal - drzewko decyzji (wprost pod pytanie o "szarze na lucznikow")
- Strzelcy PIESI wroga zadaja straty:
  - sa odslonieci (brak piechoty/jazdy w poblizu) -> szarza jazdy z boku; to historycznie standard i dziala.
  - stoja za/przy piechocie -> nie szarzowac czolowo; (a) szybkie zblizenie calej linii (Maraton), (b) wlasni harcownicy/strzelcy do pojedynku, (c) wycofanie poza zasieg / za zaslone terenu, (d) jazda obchodzi szeroko i wpada od tylu po zwiazaniu piechoty.
  - oslania ich jazda -> najpierw pobic te jazde.
- Strzelcy KONNI wroga:
  - NIE gonic ciezka piechota ani daleko jazda; wypad jazdy tylko krotki, z powrotem do linii;
  - odpowiedz: wlasni strzelcy piesi (dluzszy zasieg, wieksza gestosc), oparcie skrzydel o teren, piechota z tarczami w zwartym szyku, ale tylko gdy wrog nie ma ciezkiej jazdy do uderzenia w stloczonych;
  - jesli wrog ma strzelcow konnych + ciezka jazde, a my nie mamy strzelcow -> natychmiast marsz do terenu (wzgorze, las, rzeka) albo natarcie na oboz/piechote wroga; bierne stanie = Carrhae.
- Stanie pod ostrzalem w miejscu jest ZAWSZE najgorsza opcja; kazda armia historyczna po kilku minutach strat albo nacierala, albo sie cofala.

### 3.3 Rezerwy i linie
- Trzymaj 15-30% sil poza pierwsza linia (triplex acies: hastati / principes / triarii; Aleksander: druga linia; Hannibal pod Zama: weterani 200 m z tylu).
- Uzycie rezerwy, w kolejnosci priorytetu: (1) kontra na jazde wchodzaca na tyl (Farsalos, Gaugamela); (2) zalatanie przerwanej linii (Issos); (3) wydluzenie linii przeciw oskrzydleniu (Zama); (4) uderzenie konczace, gdy wrog sie chwieje (Farsalos - trzecia linia).
- Rezerwa bez dowodcy z inicjatywa jest bezuzyteczna (Adrianopol - uciekla); z inicjatywa wygrywa bitwe (Kynoskefalaj - trybun).

### 3.4 Plany bitwy do losowania (warianty, zeby bitwy nie byly takie same)
| Plan | Kiedy wybierac | Wzor |
|---|---|---|
| Podwojne oskrzydlenie: slabe/cofajace sie centrum, silne skrzydla | przewaga jazdy, wrog agresywny i glebszy | Kanny, Maraton |
| Szyk skosny: jedno skrzydlo wzmocnione 2-4x, drugie odmowione | wrog ma elite/wodza w znanym miejscu; rowne lub mniejsze sily | Leuktry |
| Mlot i kowadlo: piechota wiaze, jazda z jednego skrzydla na wodza/tyl | silna ciezka jazda | Issos, Gaugamela, Hydaspes |
| Obrona z ukryta rezerwa przeciwjezdna | wrog ma duza przewage jazdy | Farsalos |
| Czworobok / podwojna linia z odmowionymi skrzydlami | wrog duzo szerszy, teren otwarty | Gaugamela (aktywny) vs Carrhae (bierny = kleska) |
| Wciagniecie na trudny teren / cofanie sie pod wzgorze | wrog ma zwarta formacje pik lub jazde | Pydna |
| Nekanie + udawana ucieczka + kontra | mamy strzelcow konnych i troche ciezkiej jazdy | Carrhae |
| Natychmiastowy atak na nieuformowanego | wrog w marszu / rozwija sie | Kynoskefalaj, Adrianopol (od strony Gotow) |
| Gra na czas (rokowania, czekanie na posilki, zmeczenie wroga upalem) | posilki w drodze | Adrianopol |

### 3.5 Zalamanie i poscig (parametry)
- Wyzwalacze paniki, od najsilniejszego: uderzenie w tyl > smierc/ucieczka wodza > uderzenie w bok > ucieczka sasiedniego oddzialu > widok ucieczki wlasnej jazdy > dlugi ostrzal bez mozliwosci odpowiedzi > straty frontowe (najslabszy).
- Panika jest zarazliwa wzdluz linii i idzie od tylu; elita i oddzialy przy wodzu trzymaja dluzej; weterani nie wpuszczaja uciekajacych w szeregi (Zama).
- Straty do chwili zalamania: zwykle 3-10%. Reszta w poscigu i w kotle. Okrazony, ktory nie ma dokad uciec, walczy do konca - dlatego niektorzy wodzowie celowo zostawiali droge ucieczki (zasada "zlotego mostu" u Wegecjusza).
- Poscig prowadza jazda i lekkozbrojni, najlepiej swiezy oddzial (Hydaspes); ciezka piechota trzyma szyk i zajmuje oboz. Poscig zbyt wczesny (zanim pekla piechota) to blad (Zama - o wlos).

### 3.6 Typowe bledy historyczne = lista "glupich zachowan", ktorych AI nie powinno robic (albo robic tylko jako slaby wodz)
1. Stanie w miejscu pod ostrzalem (Carrhae).
2. Daleki poscig jazdy za strzelcami konnymi / za pobita jazda, bez powrotu (Publiusz Krassus; Leliusz o wlos).
3. Zageszczanie szyku pod naciskiem z bokow - scisk odbiera zdolnosc walki (Kanny, Adrianopol).
4. Parcie centrum naprzod bez patrzenia na skrzydla (Kanny).
5. Atak czesciami, w trakcie rozwijania sie, po forsownym marszu (Adrianopol, lewe skrzydlo Filipa pod Kynoskefalaj).
6. Brak rezerwy / rezerwa bez zadania.
7. Prowadzenie zwartej formacji przez nierowny teren w pogoni (Pydna).
8. Jazda po przelamaniu jedzie na oboz zamiast na tyl linii (Gaugamela).
9. Wodz w pierwszym szeregu bez oslony - jego smierc konczy bitwe (Kleombrotos, Dariusz ucieka = to samo).

## 4. Zastrzezenia
- Skala: wiekszosc tych bitew to 30-80 tys. na strone; przy ~1000 ludzi fazy trwaja minuty, nie godziny, ale kolejnosc i logika zostaja (harcownicy -> jazda -> piechota -> bok/tyl -> poscig).
- Niepewne: liczby (zwlaszcza perskie i straty), dystans biegu pod Maratonem, obecnosc jazdy perskiej pod Maratonem, skrzydlo manewru Kojnosa, celowosc gry na czas Gotow i kierunek uderzenia ich jazdy, "klucie w twarze" pod Farsalos (Plutarch, nie Cezar), 6 czy 8 kohort czwartej linii.
- Sprawdzone w sieci (zgodne z tekstem): oddzial Publiusza 1300 jazdy / 500 lucznikow / 8 kohort i udawana ucieczka; wielblady ze strzalami; 6 kohort czwartej linii u Cezara i uzycie pilum jako wloczni; 20 manipulow trybuna pod Kynoskefalaj (Polibiusz XVIII 19-27); powrot jazdy gockiej z furazu (kierunek uderzenia - zrodla wtorne rozbiezne).

## 5. Zrodla
Antyczne: Herodot VI 102-117; Ksenofont, Hellenika VI 4; Arrian, Anabaza II 6-11, III 8-15, V 8-18; Polibiusz III 107-117, XV 9-16, XVIII 19-32 (w tym porownanie falangi i legionu); Liwiusz XXII 44-49, XXX 32-35, XXXIII 6-10, XLIV 40-42; Plutarch: Pelopidas, Emiliusz Paulus, Krassus 23-31, Pompejusz, Cezar; Cezar, Wojna domowa III 88-99; Kasjusz Dion XL 21-27; Ammian Marcellin XXXI 12-13; Wegecjusz III.
Sieciowe (uzyte do weryfikacji):
- https://www.worldhistory.org/article/1406/battle-of-carrhae-53-bce/
- https://historynet.com/what-we-learned-from-the-battle-of-carrhae/
- https://www.livius.org/articles/battle/pharsalus-48-bce
- https://www.historyofwar.org/articles/battles_pharsalus.html
- https://www.britannica.com/print/article/148467 (Kynoskefalaj)
- https://livius.org/battle/cynoscephalae-197-bce
- https://www.worldhistory.org/Battle_of_Adrianople/
- https://www.warhistoryonline.com/ancient-history/378-adrianople.html
