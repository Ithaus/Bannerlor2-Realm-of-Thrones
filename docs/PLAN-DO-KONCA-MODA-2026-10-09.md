# Plan do konca moda - stan 09.10.2026 ok. 04:00 (po dwoch przegladach)

Dla Jeffa: wszystko, co zostalo, z przegladow, audytow, projektow i kodu. Ta sama rzecz z kilku miejsc = jedna pozycja; nic nowego; pomysly osobno (rozdz. 3); zrodla na koncu.

**Status:** gotowe = kod czeka na autotest; w toku = robimy teraz; zdecyd. = zdecydowane, nie zaczete (takze stary kod do przeniesienia); blad = naprawa bledu; Ty = czeka na Ciebie; koniec = zakonczenie.
**Wielkosc:** M = mala (ok. 1 h), S = srednia (2-4 h: projekt, kod, recenzja, autotest), D = duza (kilka paczek, pol nocy do nocy, test 120 dob). **NK** = pelny skutek dopiero w nowej kampanii.

## 0. Krotko

| Status | Pozycji |
|---|---|
| w toku | 5 |
| gotowe | 1 |
| zdecyd. | 76 |
| blad | 30 |
| Ty | 6 |
| koniec (rozdz. 4) | 9 |
| **razem** | **127** |
| pomysly (rozdz. 3) | ok. 50 |

Do tego pytania (rozdz. 2): 4 z ESENCJI i 16 nowych.

**Najwazniejsze:**
1. **Zabezpieczyc prace z nocy** (1.1): dozbrajanie i proba drog sa tylko na dysku C, ktory zeruje pliki.
2. **Wgrac uzbrojenie, dozbrajanie i armie krolestw** (etap 1); uzbrojenie jest wlasnie w autotescie.
3. **Zatrzymac fale bankructw** (etap 2); wczesniej proba zimy, bo od niej zalezy budzet rodow.
4. **Zamknac zloto z niczego** (dzis ok. 1.1 mln zl dziennie): etap 3 zamyka majatki BK, statki i drobne zrodla; zakupy mieszczan (ok. 450 tys.) dopiero kiesa ludu (etap 5).
5. **Ksiega ludzi, potem kiesa ludu** (etapy 4-5).

**Ile pracy: ok. 25-35 nocy (srodek ok. 30). Przy jednej nocy dziennie koniec najwczesniej ok. 5.11, realnie w drugiej polowie listopada (przerwy na Twoje odpowiedzi i powtorki testow).** Zalozenia:
- noc = 8-10 h pracy jak 08/09.10, kilka paczek naraz; tempo 4-5 wgran na noc (8 wgran z nocy 08/09 to 19 malych poprawek; duze 171/172 nie przeszly progow);
- mala pozycja ok. 1/3 wgrania, srednia 1/2-1, duza 2-3 z testem 120 dob; etapy 1-8: 23 duze, 62 srednie, 33 male = ok. 115 wgran;
- autotesty po kolei: 40 dob ok. 10 min, 120 dob ok. 40 min, rok ok. 2 h (etapy 2, 5 dwa razy, 7, kalibracja), 4 lata co najmniej 8 h;
- w czasie dlugich testow robimy pozycje bez zaleznosci (konie, drobne bledy, wioski); czesc paczek nie przejdzie progow za pierwszym razem;
- nie liczymy pomyslow ani pracy z Twoich "tak" na nowe pytania (ok. 1-3 noce).

Noce na etap: 1: 2-3, 2: 3-5, 3: 2-3, 4: 4-6, 5: 2-4, 6: 3-4, 7: 3-4, 8: 2-3 (rownolegle), zakonczenie: 3-5.

**Co Ty mozesz zrobic, zeby bylo szybciej:**
- odpisac na pytania z rozdz. 2 - najpierw "przed etapem 3" i "przed etapem 5", bo blokuja prace;
- (zrobione 09.10 ok. 04:15) stala zgoda "wgrywaj sam po tescie" na cala prace - nie czekamy na Ciebie;
- proby drog i zimy: zgode dales; blokuja je zabezpieczenia folderu gry. Ponawiamy; przy blokadzie dostaniesz gotowe polecenie;
- gra zamknieta w czasie nocnych autotestow;
- Twoja nowa kampania raz, gdy wejdzie wszystko, co jej wymaga (rozdz. 4);
- sprawdzic w grze to, czego autotest nie gra (8.16);
- do czasu dozbrajania (1.3): MCM "Garrison Buys Gear Player", jesli Twoje zalogi maja kupowac bron i cwiczyc jak AI. Po 1.3 zbroja sie z wlasnej sakiewki, a suwak to tylko doplata z Twojej kiesy.

## 1. Etapy do konca

1 w toku -> 2 bankructwa -> 3 szczelnosc -> 4 ksiega ludzi i zima -> 5 kiesa ludu -> 6 rzemioslo i konie -> 7 tempo, mapa, wioski -> 8 drobne (na cala droge) -> zakonczenie.

### Etap 1. Teraz w toku: dokonczyc, przetestowac, wgrac

Cel: wojsko zbrojone z prawdziwej produkcji; zolnierze sami sie dozbrajaja; armie krolestw wedlug Twoich decyzji z 09.10; musztra zamiast kupowania doswiadczenia.

| # | Co | Status | Wiel. | Zalezy od |
|---|---|---|---|---|
| 1.1 | Zabezpieczyc: dozbrajanie i probe drog wyslac do repo i na D:; potem po kazdej nocy | zdecyd. | M | - |
| 1.2 | Uzbrojenie (171-174): zalogi kupuja bron w miescie, rekrut z tym, co ma; strzaly z drewna i rudy; karawany bez strzal; nic nie znika; rzemieslnicy przy surowcu i ludziach; ruda jedzie karawana; z Twoimi "tak" z 03:45 | gotowe (autotest trwa) | D | - |
| 1.3 | Dozbrajanie (K1): zolnierze kupuja lepszy sprzet z zoldu i lupu; wymiana z Toba lepsze za gorsze; dostosowac do 1.4 (sufit = stopien jednostki) | w toku | S | 1.2 |
| 1.4 | Armie krolestw (175): sprzet swojego stopnia; Polnoc twardsza bez dominacji; Dothrakowie konni; jazda Zelaznych Wysp tylko z Harlaw; Volantis i Norvos jak w ksiazkach; Pentos, Qarth, Dorne lzej, Qohor ciezej; Wolni Ludzie i Smocza Skala mniej jazdy; kon za awans AI do zbrojowni; test liczy oblezenia i konie | w toku | D | 1.2 |
| 1.5 | Rzeczy z domu rekruta po 1.4 spadaja o ok. 60% - liczyc je po rodzaju sprzetu | blad | M | 1.4 |
| 1.6 | Zlota Kompania 1/3 lukow (zrobione, wylaczone) - wlaczyc | zdecyd. | M | 1.3, 1.4 |
| 1.7 | Wymog Atletyki do pancerza i umiejetnosci do amunicji takze u Ciebie, towarzyszy i lordow; lordom Atletyka do ich zbroi | zdecyd. | S | 1.4 |
| 1.8 | Musztra Twojej druzyny (tempo z tabeli - Twoje "tak" z 04:00) i zapas do cwiczen z oddanego sprzetu | w toku | S | 1.2 |
| 1.9 | Musztra AI ta sama; bez doswiadczenia z niczego, ktore BK daje codziennie druzynom lordow AI | zdecyd. | S | 1.8, 1.4 |
| 1.10 | Lordowie AI maszeruja noca tylko z powodu, z tymi samymi karami co Ty (odpowiedz z 04:00) | w toku | S | - |
| 1.11 | Qarth z niewola; Qohor i Lorath wedlug kanonu (jency) | w toku | M | - |
| 1.12 | Twoi rekruci bez darmowego kompletu, jak u AI | zdecyd. | S | 1.2 |
| 1.13 | Proba drog na 2 kawalkach mapy, drogi do wiosek | zdecyd. | S | - |
| 1.14 | Proba zimy w autotescie (przed testem rocznym etapu 2) | zdecyd. | M | - |
| 1.15 | Limit hordy Zewu Nocnego Krola ok. 1 140 (dzis 5 000+) | zdecyd. | M | - |

**Skonczone, gdy:** kazde wgranie: autotest 40 dob i zapis z doby 362 - 0 bledow naszych modow (6-9 znanych bledow startowych cudzych modow nie liczymy); w dobie 120 testu: bron swojego stopnia ma co najmniej 80% zolnierzy AI, helm 40%, tarcze 35%, cos na tulowiu 50%; zadne krolestwo nie wygrywa ponad 85% bitew lordow (ani o 20 punktow wiecej niz dzis) i nie zdobywa 3 twierdz; Dothrakowie najwyzej 3 bankrutow (dzis 1).

### Etap 2. Koniec fali bankructw

Cel: lord trzyma tyle wojska, na ile go stac; korona placi z biezacych podatkow; Bank pozycza tylko temu, kto odda; zamiast bankructwa zajecie dochodu.

| # | Co | Status | Wiel. | Zalezy od |
|---|---|---|---|---|
| 2.1 | Pomiary w logu (gra bez zmian): okupy i niewola lordow, sluby AI, towar wedrowcow BK, kasy miast, wzrost Innych, ludnosc BK (warunek kiesy ludu), przyrost zalog bez werbunku, miara historyczna cz. 2 | zdecyd. | S | - |
| 2.2 | Przeniesc stary kod 108-114 (z 06.10, nigdy w grze) na dzisiejszy; powtorzyc proby | zdecyd. | D | 1.2-1.4 |
| 2.3 | Korona (165): zwrot polowy zoldu z biezacych wplywow; renty wedlug lenn, takze dla Ciebie; Ty placisz koronie jak wasal AI; trybut na raty; clo bez drugiego poboru | zdecyd. | D | 2.2 |
| 2.4 | Biedne krolestwa slabsze; dar Polnocy dla Strazy, Wolnych Miast dla Dothrakow | zdecyd. | M | 2.3 |
| 2.5 | Nocna Straz bez zoldu, jedzenie i sprzet z daru Polnocy | zdecyd. | S | 1.4, 2.4 |
| 2.6 | Powinnosci wobec korony od dochodu brutto; jeden blad zatrzymuje pobor u wszystkich | blad | M | 2.3 |
| 2.7 | Budzet rodu (166): wojsko wedlug dochodu, zalogi w pokoju o polowe, rycerze bez lenna bez oddzialow; w druzynach lordow w wojnie 85-100 tys. (dzis 98 tys. w 40. dobie i rosnie) | zdecyd. | D, NK | 2.3, 1.14 |
| 2.8 | Dwor pana minimalny (Tobie nie) | zdecyd. | S | 2.7 |
| 2.9 | Dezercja AI z braku zoldu bez limitu dziennego | blad | M | 2.7 |
| 2.10 | Kontrakt najemnika AI staly, w pokoju polowa | zdecyd. | S | 2.7 |
| 2.11 | Kasy zamku i wsi (110, 112, 114): zamek nie kasuje ok. 710 zl dziennie, nadwyzka 2/3 panu, 1/3 koronie; wies nie gubi 15% utargu | zdecyd. | S, NK | 2.2 |
| 2.12 | Dlug (168): Bank pozycza na zdolnosc splaty; zajecie dochodu zamiast bankructwa; raty do 15% dochodu; kapital Banku w kasie Braavos (dzis 5 mln z niczego); limit z zalogami i rentami | zdecyd. | D | 2.7 |
| 2.13 | Okupy wedlug majatku dla Ciebie i AI: glowa rodu ok. pol roku dochodu, reszta na raty, bez minimow; korona bierze 1/9 | zdecyd. | D | 2.12, 2.1 |
| 2.14 | Okupy: Twoj trafia do porywacza; kurier nie dosypuje zlota | blad | M | 2.13 |
| 2.15 | Pokoj z biedy: 0 glosowan - czy Diplomacy wycina wnioski | blad | M | - |
| 2.16 | Zima moda RBL dubluje nasza | blad | M | 1.14 |

**Skonczone, gdy** (nowa kampania 120 dob, potem rok): glow rodow ponizej 5 000 zl najwyzej 10 z ok. 310 (dzis 92 po roku); 0 bankructw, takze u Dothrakow - zamiast nich najwyzej 5 zajec dochodu rocznie (na Twoim zapisie jednorazowo ok. 7); zaden skarbiec w wojnie ponizej 0.25 mln dluzej niz 28 dob; zalegly zold najwyzej 2%.

### Etap 3. Szczelnosc: nic z niczego, nic w nicosc

Cel: kazda moneta ma platnika i odbiorce (dzis z niczego ok. 1.1 mln zl dziennie, w nicosc ponad 0.6 mln). Po budzecie rodu.

| # | Co | Status | Wiel. | Zalezy od |
|---|---|---|---|---|
| 3.1 | Szczelnosc (164a): karawany, awanse, ochotnicy, zloto z cial, jency, oblezenia i turnieje BK, majatki BK (ok. 290 tys. dziennie), kapital nowych karawan, zysk karawany pana bez miasta | zdecyd. | D | etap 2, 3.2 |
| 3.2 | Cale zloto pokonanego taboru i bandy dla zwyciezcy (dzis 10% i 50%) | Ty | M | - |
| 3.3 | Statki rozbitej druzyny sprzedane portowi (ok. 270 tys. dziennie z niczego) | zdecyd. | M | 2.7 |
| 3.4 | BK: zloto za niewolnikow, wies z Rynkiem placi wiecej, niz ma, zaopatrzenie partii AI i wyposazenie rycerza w nicosc | blad | S | 3.1 |
| 3.5 | Rada BK: pensje od podatku, ktorego nie ma (ok. 70 tys. dziennie) | blad | S | 3.1 |
| 3.6 | Wedrowcy BK bez sztab z niczego; stal valyrianska skonczona | zdecyd. | S | 3.1 |
| 3.7 | ROT: inwazje, rebelia, zadania, wycena lorda i podmiana rekruta - z niczego | blad | S | 3.1 |
| 3.8 | Ceny historyczne: stale kwoty w skali; zloty smok = 36-80 zl | zdecyd. | S, NK | etap 2 |
| 3.9 | Drobne w nicosc: lapowka w niewoli, Twoj rynsztunek za darmo na targ, "bandyci ukradli" w Spoils, zbrojownia ponad 600 sztuk, trzecia lorda 13% zamiast 33% | blad | S | - |
| 3.10 | Drobne z niczego (najpierw sprawdzic): boss kryjowki, furaz przy spalonej ziemi, kopalnie BK bez wsadu, start "zeglarz" 10 000 zl, zold ROT razy liczba bohaterow | blad | S | - |

**Skonczone, gdy:** reszta niewyjasniona w ksiedze obiegu ponizej 10 tys. zl dziennie (srednia 28 dob; dzis ok. 27 tys.), dosypka kas miast policzona. "Zloto swiata stoi" - dopiero w etapie 5.

### Etap 4. Ksiega ludzi i zima

Cel: kazdy czlowiek policzony; plon z pracy ludzi; rabunek, pobor i smierc zabieraja prawdziwych ludzi; zalogi nie rosna z niczego; zima wedlug krain.

| # | Co | Status | Wiel. | Zalezy od |
|---|---|---|---|---|
| 4.1 | Ksiega ludzi i przyrost wsi (108, 109) | zdecyd. | D | 2.2 |
| 4.2 | Naprawa tabeli ludnosci (dzis +72% rocznie) | blad | S | 4.1 |
| 4.3 | Do czasu ksiegi wies nie rosnie sama z siebie | zdecyd. | M | - |
| 4.4 | Spustoszenie (113): rabunek wygania ulamek ludzi, uchodzcy wracaja, spalona wioska wraca w 1 rok | zdecyd. | S, NK | 4.1 |
| 4.5 | Ksiega wiosek: kazda wioska poboczna ma ludzi i czesc plonu; plonie, spalona, odbudowa | zdecyd. | D | 4.4 |
| 4.6 | Plon okregu z pracy ludzi we wszystkich wioskach; zywnosc warowni tak samo | zdecyd. | D, NK | 4.5 |
| 4.7 | Ludzie nie znikaja i nie biora sie z niczego (164b): jeniec zostaje mieszczaninem, ochotnik ubywa ze wsi, rozwiazane druzyny wracaja; zalogi i milicje tylko z ludzi regionu | zdecyd. | D | 4.1, 3.1 |
| 4.8 | Pobor z ludzi: rekrut z jego wsi, polegly ubywa na stale, okno zniw | zdecyd. | D | 4.4 |
| 4.9 | Odrodzony lord bez 55 ludzi i zboza z niczego | zdecyd. | S | 2.7, 4.8 |
| 4.10 | Tabela krain: Wyspy Letnie 4 500 ludzi na punkt | blad | M | 4.1 |
| 4.11 | Spichlerz okregu; glod i uchodzcy wedlug historii; zyznosc krain | zdecyd. | D | 4.4, 1.14 |
| 4.12 | Zima wedlug krain: lzej na poludniu, Wyspy Letnie bez zimy, Skagos owce, konie jedza zima | zdecyd. | S | 4.11 |
| 4.13 | Zaraza tylko z oblezen | zdecyd. | M | 4.1 |
| 4.14 | Weterani (167): najemnicy z prawdziwych ludzi; karczmy ze starymi zolnierzami | zdecyd. | S, NK | 2.7, 4.1 |
| 4.15 | Log krain (wolne rece, Straz, Niewolni, Umarli) i tabela strat Strazy i Wolnych Ludzi | zdecyd. | M | - |
| 4.16 | Nocna Straz: rekrut z wyrzutkow i ludnosci, dozywotnio; nie pali wsi za Murem | zdecyd. | S | 4.15, 2.5 |
| 4.17 | Wolni Ludzie 300 tys., walcza wszyscy dorosli; niewola tylko w kulturach kanonu (Zelazne Wyspy 27 tys.) | zdecyd. | S, NK | 4.15 |
| 4.18 | Inni tylko z poleglych za Murem i przy nim; jency Innych wstaja; sprawdzic inne drogi trupow z niczego | zdecyd. | S | 4.1 |
| 4.19 | Po poborze: Straz z ludnosci, jency z rabunku od BK, dluznik rozpuszcza poczet | zdecyd. | D | 4.8 |

**Skonczone, gdy:** ludnosc rosnie 0.2-0.8% rocznie (w pomiarze BK najwyzej 2%); wies, z ktorej uciekla 1/3 ludzi, daje mniej plonu; spalona wioska (takze poboczna) wraca po roku; przyrost zalog bez werbunku = 0; test z zima od poczatku 120 dob: zadna warownia AI nie glodzi przed 30. doba zimy, mediana zapasu co najmniej rok.

### Etap 5. Kiesa ludu, kasy miast i glod

Cel: placa za prace trafia do ludzi, a oni kupuja za swoje; miasto nie zyje ze zlota z niczego; pan z septem bierze 30-50% gotowki wsi (dzis ok. 85%). Pelny skutek w nowej kampanii.

| # | Co | Status | Wiel. | Zalezy od |
|---|---|---|---|---|
| 5.1 | Kasy miast (111'): bez kasowania i "zakupow z niczego" (ok. 450 tys. dziennie); najpierw z liczona dosypka | zdecyd. | S, NK | 3.1 |
| 5.2 | Dwor pana pelny | zdecyd. | S | 2.8 |
| 5.3 | Markietani (163) i notable (164c): zolnierze wydaja w polu, nowy notabl bez 10 tys. z niczego | zdecyd. | S, NK | 3.1 |
| 5.4 | Drobne kas: oblezone miasto, prog podatkow zamku, 10 miast bez placenia panu | zdecyd. | S, NK | 5.1 |
| 5.5 | Kiesa ludu (173): koszyk za swoje, podatki z czynszem ok. 9%, wies zatrzymuje 30% utargu, prawa zamku osobno - jedno wgranie z 5.6 i 5.7 | zdecyd. | D, NK | 2.1, 5.1, 5.6, 5.7 |
| 5.6 | Podatek wojenny korony (cel ok. 100 tys. dziennie w wojnie): im wyzszy, tym wolniej rosnie dobrobyt | zdecyd. | S, NK | 2.3 |
| 5.7 | Wyrownanie dla panow zamkow: pan miasta 1/2 nadwyzki kupcow; renty miasto 2, zamek 1, wies 0.25 | Ty | S | 2.3 |
| 5.8 | Korona bez bezpiecznika, kasy miast bez dosypki - gdy reszta ponizej 10 tys. przez 28 dob | zdecyd. | M | 5.5 |
| 5.9 | Glod ma skutki (nie w pierwszych 120 dniach) | zdecyd. | S | 5.5 |
| 5.10 | Dziesiecina: polowa jalmuzna, polowa budowa septow | zdecyd. | S, NK | rok testu 5.5 |
| 5.11 | Targ przy zamku: wsie najpierw sprzedaja zamkowi jedzenie na 10 dni | Ty | M | 5.5 |
| 5.12 | Wiekszy udzial wsi w dochodzie pana, mniej z nadwyzki miast | zdecyd. | S | 5.5 |
| 5.13 | Napis w menu miasta: czy ludzi stac na chleb | zdecyd. | M | rok testu 5.5 |

**Skonczone, gdy** (nowa kampania 40 dob i dwa testy roczne): kazdego dnia w kazdym miescie co najmniej 95% ludzi stac na jedzenie, 98% na pelny koszyk; zjedzone bez zaplaty ponizej 1%; miasta kupuja co najmniej 98% plonu; mediana kiesy panow zamkow nie spada o wiecej niz 10% na 28 dob wojny, ich udzial w stalym dochodzie rodow 30-35% (dzis 24%), zatkanych magazynow wsi zamkowych nie wiecej niz dzis (5.6%); wojsko jak w 2.7; sakiewki zolnierzy najwyzej 15 dni zoldu; zloto swiata zmienia sie mniej niz o 0.1% na 28 dob.

### Etap 6. Rzemioslo, surowce i konie

Cel: zbroi starczy dla armii (po 174 ok. 120-190 zbroi na tulow dziennie przy potrzebie do 360); towar bez zbytu nie puchnie; konie z hodowli, gina tylko zabite.

| # | Co | Status | Wiel. | Zalezy od |
|---|---|---|---|---|
| 6.1 | Wytop przy kopalni (zelazo zamiast rudy); przetop | Ty | D, NK | 1.2 |
| 6.2 | Wiecej rak i kopaln | zdecyd. | D | 6.1 |
| 6.3 | Wsie x1.3 lnu, welny, skor; wsad wedlug wartosci | zdecyd. | S | 1.2 |
| 6.4 | Mniej towaru bez zbytu zamiast kasowania przez BK | zdecyd. | S | - |
| 6.5 | Warunkowo: chleb, piwo, wino, deski z wsadu; srebro; rzemioslo wsi BK | zdecyd. | D | 6.3 |
| 6.6 | Ksiega uzbrojenia wedlug zrodla w logu | zdecyd. | M | - |
| 6.7 | Dziury po 174: narzedzia, ruda rozbitych partii, uprzaz bez kupca; towar wozony bez wozu (zakupy zalog, handel dzienny) | blad | S | 1.2 |
| 6.8 | Jesli test uzbrojenia pokaze braki: tarczownicy, placa miasta w cenie, strzaly, helmy, "dobytek osady" | zdecyd. | S | 1.2 |
| 6.9 | Warsztaty wedlug surowca i lore; smoki, mamuty, wielblady wedlug kanonu; blad "strawy Polnocy" (tlocznia bez winogron) | zdecyd. | S, NK | - |
| 6.10 | Klimat wsi takze dla warsztatow miast | zdecyd. | M, NK | - |
| 6.11 | Konie: linia w logu; ginie tylko zabity, ocalaly to lup zwyciezcy | zdecyd. | S | - |
| 6.12 | Hodowla wedlug popytu + przeglad 71 stadnin | zdecyd. | S, NK | 6.11 |
| 6.13 | Konie po poleglych jezdzcach ze zbrojowni AI na targ | zdecyd. | M | 1.4, 6.11 |
| 6.14 | Rycerz z wlasnym koniem / wiecej jazdy u AI (dzis ok. 8%, w ksiazkach 20-35%) - po tescie 1.4 | Ty | S | 1.4 |
| 6.15 | Konie z lupu i zbrojowni nie niosa piechura | blad | M | - |
| 6.16 | Juczne konie z targu; zapasowe konie lordow; udzwig | zdecyd. | S | - |
| 6.17 | Naprawy: material wedlug rodzaju, Twoje rece ta sama regula; AI naprawia tylko to, co nosi | zdecyd. | S | 1.2 |

**Skonczone, gdy** (120 dob): cos na tulowiu ma co najmniej 75% zolnierzy AI; skor i plotna w swiecie nie ubywa szybciej niz 10 dziennie, zapas lnu i welny nie rosnie 3 miesiace z rzedu; w linii "Konie" kazdy znikajacy kon ma przyczyne.

### Etap 7. Tempo swiata, mapa, drogi, wioski

Cel: armia idzie jak w ksiazkach (Winterfell - Krolewska Przystan ok. 77 dni, dzis 21); drogi przyspieszaja; wioski maja herb i menu okregu.

| # | Co | Status | Wiel. | Zalezy od |
|---|---|---|---|---|
| 7.1 | Najpierw blokady tempa; jedna skala czasu | zdecyd. | S | etap 3 |
| 7.2 | Tempo z ksiazek (forsowny 32 km dziennie), wozy i karawany, leczenie i starzenie | zdecyd. | D | 7.1 |
| 7.3 | Po zmianie tempa przeliczyc zapas wojenny i budzet rodu, awanse i nauke, ciaze; powtorzyc progi etapow 2 i 5 | zdecyd. | D | 7.2 |
| 7.4 | Kary terenu: zwrot kary, ktorej gra nie nalozyla | blad | M | - |
| 7.5 | Pelne drogi, +15% po drodze, drogi do wiosek i Valyrii | zdecyd. | D | 1.13 |
| 7.6 | Obozy: spiacy lord dostaje rozkazy; AI placi za nieprzespana noc; zbiorka | blad | S | - |
| 7.7 | Turnieje, sojusze i rozejmy licza czas jak reszta swiata | blad | S | - |
| 7.8 | Dane lore 2 446 wiosek (13 poprawek) | zdecyd. | M | - |
| 7.9 | Dymek wioski: herb, rycerz, wiara, historia; menu okregu | zdecyd. | S | 7.8, 4.11 |
| 7.10 | Komunikat, gdy plonie Twoja wioska; kto spalil; wyglad spalonej | zdecyd. | S | 4.5 |
| 7.11 | Rabunek osada po osadzie, AI tak samo | zdecyd. | D, NK | 4.5 |
| 7.12 | Licznik ruchu przez Bliznaki | zdecyd. | M | - |

**Skonczone, gdy:** Winterfell - Krolewska Przystan ok. 77 dni, miasta dalej kupuja co najmniej 98% plonu; po drodze szybciej o 15%; test roczny po zmianie tempa spelnia progi etapow 2 i 5.

### Etap 8. Drobne bledy i Twoje sprawdzenia (na cala droge)

Cel: kazda znana usterka zamknieta; to, czego autotest nie gra, sprawdzone przez Ciebie.

| # | Co | Status | Wiel. | Zalezy od |
|---|---|---|---|---|
| 8.1 | Ksiega umiejetnosci; doswiadczenie za strzal tylko w bitwie; zarzadca i zwiad | blad | S | - |
| 8.2 | Dary Spoils do zbrojowni zalogi; drobne doswiadczenie | zdecyd. | S | 1.2 |
| 8.3 | Ksiazki BK po 7-10 zl; kuznia lordow i brakujace czesci broni | blad | M | - |
| 8.4 | Amunicja: odzysk AI jak u Ciebie; oszczepy jak strzaly | blad | S | 1.2 |
| 8.5 | Ceny: surowce a cena broni; popyt wojenny i cena konia liczone dwa razy | blad | S | - |
| 8.6 | Lup: wraki 100% dla Ciebie; podejrzenie dubla lupu i koni | blad | S | - |
| 8.7 | Zalogi nie zuzywaja sprzetu; monopole korony | blad | M | - |
| 8.8 | Zakupy, naprawy i zuzycie AI: jakosc ginie, brak sufitu ceny, naprawy staja, zuzycie zabiera nowe sztuki; Twoj sprzet na arenie | blad | S | 1.2 |
| 8.9 | Kowadlo i warsztaty: gubione welna i skory, wraki; 16 drobnych zgloszen | blad | S | - |
| 8.10 | Start kampanii: budzet platnerzy, kon bez uprzezy; komplety rekrutow z niczego | blad | M, NK | 1.2 |
| 8.11 | Stany gubione przy zapisie; obozy nie czyszczone przy nowej grze; klimat Westeros | blad | S | - |
| 8.12 | Karawany i wozy: wyspy, wies bez wozu, karawany BK raz na dobe, wozy nie widza karawan; najemnicy ROT z wodzem z niczego | blad | S | - |
| 8.13 | Bledy startowe cudzych modow; kara snu z dodatkiem morskim; krolestwo bez rodu rzadzacego; ustawienia czytane miedzy modami bez ostrzezenia; zbroja konia liczona jak pancerz | blad | S | - |
| 8.14 | BetterEconomy bez losowych susz, sol i piwo jako potrzeby: my dopisujemy klucz do skryptu, Ty go uruchamiasz | zdecyd. | M | - |
| 8.15 | Pomiary: test Innych od doby 728, produkcja wsi z dodatkiem morskim, kara "Underequipped" w Twoich bitwach, dochod owiec | zdecyd. | M | 1.4 |
| 8.16 | Twoje testy: kuznia (Kuj przy bramie, panel, pokretlo 35, "Pick a piece"); okna Spoils (Done/Cancel); turniej; niewola; jency do domu i ekran po bitwie; sprzedaz czystej, Battered i wraku; kulawy kon; rezerwa miasta 20 000; samonaprawa; karczma | Ty | S | - |

**Skonczone, gdy:** kazda pozycja ma dowod z logu albo z Twojej gry.

## 2. Czeka na Twoja decyzje

**Z ESENCJI** (docs/audyt-2026-10-09/ESENCJA-AUDYTOW-2026-10-09.md): na wszystkie odpowiedziales 09.10 (03:45-04:05) - Inni w 1. roku pod Piescia: NIE; myto: TAK (po liczniku ruchu, 7.12); stal valyrianska: TAK (paczka 177 w toku); samotne kolumny: nocny marsz tylko z powodu i z Twoimi karami (T10 w toku). Zostaje tylko **20** - ile drog: po obejrzeniu proby (1.13), przed 7.5 ("gdzie wioska, tam droga, a nie przez pole").

**Nowe** (z terminem):
- **A/B.** Dozbrajanie: bogaty zolnierz kupuje o stopien wyzej (domyslnie nie)? Zaloga ze sprzetem dla mniej niz 75% ludzi walczy tylko tym, co ma (domyslnie nie)? - przed testem 1.3.
- **C.** Dezercja wedlug poziomu tez dla AI? - przed 2.9.
- **D.** Zwyciezca bierze cale zloto taboru i bandy (3.2)? - **przed etapem 3.**
- **E.** Wyrownanie dla panow zamkow (5.7), takze u Ciebie? - **przed etapem 5**: kiesa ludu wchodzi tylko razem z nim; bez niego panowie zamkow traca ok. 9% ludzi w wojnie i 17% w pokoju.
- **F.** Targ przy zamku (5.11)? **G.** BetterEconomy: prawdziwa wplata do kasy miasta i "dar dla notabli"? - przed etapem 5. Rada przy D-F: "tak".
- **H.** Wytop przy kopalni (6.1) tylko w nowej kampanii? - przed Twoja nowa kampania.
- **I/J.** Dorne: wielblady czy piaskowe rumaki? Proporcje winnic i oliwek? - przed 6.9.
- **K.** Rycerz z koniem (6.14) - dostaniesz liczby po tescie 1.4.
- **L.** Napisy na mapie: tylko nad plonacymi i spalonymi wioskami (rada) czy wszystkie z bliska? - przed 7.9.
- **M.** Namioty i ogniska przy napadzie na oboz - wlaczyc czy wyciac? - etap 7.
- **N/O.** Kuznia: wybor klasy zbroi czy filtr? Pasek gotowosci zbrojowni (mod DTE) sypie bledem - wylaczyc? - etap 8.
- **P.** 26 pytan aneksu demografii - sprawdzimy, ktore juz odpowiedziane; przed etapem 4.

**Starsze z audytu 05.10** rozstrzygniemy wedlug Twoich zasad; zapytamy tylko przy prawdziwym wyborze (strata po Twojej klesce, loteria lupu, trzecia kapitana, sprzet a autobitwa AI, szybkosc kuzni, pozyczka wojenna).

## 3. Pomysly - tylko na Twoje slowo

- Kasy miast z dlugami, pozyczkami i podatkiem nadzwyczajnym; posag przy slubach AI, pomoc wasali na okup.
- Reszta kosztu naprawy: godziny kowali, limit AI, rabat hurtowy.
- Okup okolicy zamiast palenia wsi, okup miasta, zakladnicy, honoraria rycerzy.
- Turnieje: wykup konia i zbroi pokonanego, pula u lordow AI, platnerz w taborze.
- Jarmarki, barki rzeczne, kruki, sztormy, rok urodzaju, uboj jesienia.
- Wies: rytm zniw, ryby, stada jako lup, ugor, przednowek, spichlerz miasta.
- Mapa: szubienice, pola wedlug pory roku, bloto poza droga, AI wybiera drogi, wioski do wejscia.
- Armia: dzien odpoczynku, napad na spiacy oboz, Twoj marsz forsowny, logistyka marszu, jazda Reach i Doliny, konni lucznicy Freyow, zamki przygraniczne z wieksza zaloga w pokoju.
- Rzemioslo: tarczownicy, helmy, opal kuzni, konopie, strzyza, regale gornicze.
- Umiejetnosci: zwiad a noc, Inzynieria z budow, Medycyna i Taktyka a smiertelnosc.
- Drobne: jedna budowa na rod, cena warsztatu, dar startowy do kas osad, renty widoczne dla Ciebie.

## 4. Zakonczenie moda

| # | Co | Status | Wiel. | Zalezy od |
|---|---|---|---|---|
| Z.1 | Kalibracja testami rocznymi, jedna liczba naraz: zapas korony, rezerwa wojenna, dwor, zold w pokoju, podatek wojenny (ok. 5 liczb, kazda ok. 2 h) | koniec | D, NK | etapy 1-7 |
| Z.2 | Gra po roku 2 razy wolniejsza - przyspieszyc nasze mody (zakupy i naprawy AI w miastach, dzienny przeglad, sprzatanie pamieci) | koniec | D | Z.1 |
| Z.3 | Przyspieszyc cudze: AI lordow (x44 po roku), malzenstwa ROT, znaczki druzyn | koniec | S | Z.1 |
| Z.4 | Logi pomiarowe ciszej (dzis 20-40 KB dziennie); zapis po roku 31.5 MB | koniec | M | Z.2 |
| Z.5 | Menu MCM: wylaczniki paczek zostaja, martwe suwaki i kod wyciac | koniec | S | - |
| Z.6 | Koncowy test 4-letni z zima na nowej kampanii, na ostatnim DLL (co najmniej 8 h) | koniec | D, NK | Z.2-Z.5 |
| Z.7 | Dokumenty: historia, bledy, opisy paczek od 161, CHANGELOG, audyt 15 w ESENCJI, jednolite numery paczek, nieaktualne zdania | koniec | S | - |
| Z.8 | Porzadki w kodzie i kopiach (dla nas): wersja koncowa w repo z numerem; narzedzia; stare kopie po Twojej zgodzie | koniec | S | Z.6 |
| Z.9 | Instrukcja dla Ciebie: skrypt BetterEconomy po aktualizacji, suwak Diplomacy, ustawienia | koniec | M | - |

**Test roczny zaliczony, gdy:** 0 bledow naszych modow i naraz progi etapow 2, 3 i 5; wojsko w druzynach lordow w wojnie 85-100 tys., w pokoju 30-40 tys.

**Optymalizacja:** po roku doba gry trwa ok. 24 s zamiast 11.7. Dopiero po skonczeniu modowania (Twoja decyzja z 08.10): kalibracja, przyspieszenie, na koncu test 4-letni na tym DLL, ktory dostaniesz.

**Nowa kampania czy stary zapis:**
- Autotest zawsze gra nowa kampanie i kopie Twojego zapisu z doby 362; Twoj zapis jest nietkniety.
- Do Twojej nowej kampanii wszystko wchodzi na Twoj obecny zapis (w nim ok. 7 rodow skonczy zajeciem dochodu); pozycje NK i etap 5 dzialaja tam tylko czesciowo.
- **Twoja nowa kampania - raz, gdy wejda wszystkie pozycje NK:** zalecamy po etapie 7. Jesli wczesniej (start etapu 5), 6.1, 6.9, 6.10, 6.12, 7.11 i 8.10 zadzialaja w niej tylko czesciowo.

**Wydanie:** ostatnie DLL po tescie 4-letnim, kopia na D:, CHANGELOG i instrukcja. Publikacji dla innych nikt nie planowal - jesli chcesz, powiedz.

---
**Zrodla** (pliki w docs/): SP STAN-PRAC; PR PRZEKAZANIE-08; ES ESENCJA; a00-a15 audyt-2026-10-09; A05 AUDYT-2026-10-05; AS starsze AUDYT-*; OB PROJEKT-EKONOMIA-OBIEG; P174, P175, KL (KIESA-LUDU), GL (GLOD), RB (RABUNEK), WM/WL (WIOSKI) - projekty; K1 opis dozbrajania; K13, PM (POBOR), PC (CENY) - plany; RK REGULY-KRAIN; OT OTWARTE; ER ERRORS; LW lawa; T3 towary3; RO/RN raporty 09.10; CH CHANGELOG; rz rozpoznanie; pk paczki; kod = kod i galezie.
- 1: kod 1.1; SP, PR 1.2-1.15; P174 1.2, 1.12; P175 1.3-1.7; K1 1.3; a13 1.7-1.9; a14 1.4; rz 1.9; ES 1-2 1.8; A05 1.12; a08, RN 1.13; a00 1.14; a06, RK 1.15.
- 2: a00, a01, a05, a09, KL 2.1; OB 2.2-2.3, 2.7, 2.12; pk 2.2, 2.11; a12 2.3, 2.7-2.11; a15 2.11; ES 11-12, 16 2.4, 2.13; P175 2.5; AS 2.6, 2.12, 2.14, 2.16; OT 2.9; RK 2.12; a04 2.13-2.14; SP 2.13, 2.15.
- 3: OB, a12 3.1, 3.5; a15, pk 3.2; ES 10, 15, 22a 3.3, 3.6, 3.8; rz, A05 3.4, 3.9; a03 3.6; AS, kod 3.7, 3.9-3.10; PC 3.8; T3 3.9.
- 4: pk 4.1, 4.4, 4.8-4.10; a01 4.1-4.2; a02, a00, WM, RO 4.3-4.6; OB, AS, PM 4.7-4.8; GL, SP 4.11-4.12; ES 17 4.12; SP 4.13-4.17; RK 4.15-4.19; a06, CH 4.18.
- 5: pk, OB, KL 5.1-5.5, 5.8; A05 5.3; SP 5.4, 5.6, 5.9-5.10; a15 5.5, 5.7, 5.11; a12 5.6, 5.12; a01, ES 13 5.13.
- 6: K13, AS 6.1-6.5; P174 6.2, 6.7-6.8; pk 6.6; LW 6.9, 6.11-6.12, 6.16; kod 6.10; P175, a14 6.13-6.14; A05 6.11; AS 6.15; a03 6.17.
- 7: a07, AS 7.1-7.4, 7.6-7.7; SP 7.2, 7.5, 7.11; a12 7.3; a08 7.5, 7.9; WL, WM 7.8-7.10; RB 7.11; a10 7.12; OT 7.6.
- 8: a13 8.1-8.3; SP 8.2-8.12, 8.16; OT 8.3, 8.11, 8.13, 8.16; A05, AS 8.4-8.5, 8.8, 8.10; a04 8.6; T3, LW 8.9, 8.12, 8.16; CH 8.11, 8.16; PR, ER, P175 8.13; a00, a10, kod 8.14; P175, a02, P174 8.15.
- Z: OB, a12, a00 Z.1, Z.6; SP, OT Z.2-Z.3, Z.5, Z.8; kod, T3 Z.4; CH, P174, ES Z.7; RN, pk Z.9.
