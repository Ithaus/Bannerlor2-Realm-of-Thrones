# Projekt: prolog 298 AC (start od wyjazdu Roberta do Winterfell) - wersja lekka, wedlug ksiazki

Decyzje Jeffa 21.10 ok. 03:30 (doslownie): "Ok Postacie: A - tak zgadzam sie przeniesc, ale pamietaj, ze potem Tyrion zmieni barwy, zeby postacie przechodzily zgodnie z fabula do innych stron; B - ok usun; C - ok do wynajecia; D - ok"; "Ok wersje lekka robimy"; "I zrob wydarzenia wedlug ksiazki".
Wczesniej: "Robimy wczesniejszy start". Potem (03:35): wariant posredni "Krolestwa zostaja osobno, ale na czas prologu lacze je z krolestwem Roberta paktami i sojuszami (mod Diplomacy to umie)" - "ok". Lista postaci: docs/LISTA-BOHATEROW-ROT-2026-10-21.md. Rozpoznanie techniczne: docs/ROZPOZNANIE-FABULA-GLOS-JEZYK-2026-10-20.md (pyt. 1).
Szkic napisal Claude z wiedzy o ksiazkach (Gra o Tron, Starcie Kroli, Nawalnica Mieczy, Uczta dla Wron, Taniec ze Smokami). Dni sa przyblizone - kanon nie podaje dokladnych dat;
przed kodem drugie konto sprawdza id rodow, krolestw i postaci w ROT oraz dni wydarzen ROT po rozciagnieciu osi Fabuly (x4.33, CrashScribe/src/Fabula.cs).

## 1. Co to jest (dla Jeffa)
- Nowa kampania zaczyna sie w 298 AC, w dniu, gdy krol Robert wyrusza z Krolewskiej Przystani do Winterfell - ok. 190 dni gry przed egzekucja Neda (start ROT).
- Wersja lekka: krolestwa ROT zostaja na mapie jak sa, w prologu panuje miedzy nimi pokoj (bez wojen fabularnych i bez wypowiadania wojen przez AI; Nocna Straz dalej walczy z Wolnymi Ludzmi).
  Robert i Ned zyja. DECYZJA JEFFA: na czas prologu wszystkie krolestwa Westeros sa zwiazane z krolestwem Roberta paktami i sojuszami (mod Diplomacy - pakty o nieagresji / sojusze), zeby na mapie bylo widac, ze trzymaja z Robertem; na koniec prologu (smierc Roberta) pakty wygasaja i ROT idzie dalej.
- Bandyci, kryjowki i najazdy Wolnych Ludzi dzialaja normalnie; dochodza zagrozenia z fabuly (klany gorskie Doliny, ludzie Gory w Dorzeczu pod koniec prologu).
- Na koniec prologu Robert ginie (korona sama przechodzi na Joffreya), Ned trafia do lochu, a potem ROT idzie jak zwykle (egzekucja Neda, wojna Pieciu Kroli).
- Przy tworzeniu postaci wybor: "Prologue 298 - the King rides north" albo "Classic start 299".
- Postacie kanonu nie sa do wynajecia: sa w swoich rodach i zmieniaja strony zgodnie z ksiazka (tabela w pkt 3).

## 2. Wydarzenia prologu wedlug ksiazki (dzien prologu -> scena)
Dzien 0 = Robert wyrusza z Krolewskiej Przystani. Rodzaj: T = okno z tekstem, S = zmiana swiata.
| Dzien | Wydarzenie | Rodzaj | Skutek w grze |
|---|---|---|---|
| 0 | Smierc Jona Arryna; krol z dworem rusza na polnoc Krolewskim Traktem | T + S | orszak Roberta (Robert, Cersei, dzieci, Jaime, Tyrion, Ogar) jedzie po mapie do Winterfell |
| 5 | Dezerter z Nocnej Strazy sciety przez Neda; dzieci Starkow znajduja wilkory | T | - |
| 12 | Daenerys poslubia khala Drogo w Pentos; Viserys liczy na armie Dothrakow | T | - |
| 35 | Krol przybywa do Winterfell, uczta | T + S | orszak w Winterfell; Robert proponuje Nedowi urzad Reki |
| 40 | Upadek Brana z wiezy | T | Bran ranny (w lozku) |
| 48 | Ned przyjmuje urzad Reki Krola | T | - |
| 55 | Krol rusza na poludnie; Ned, Sansa, Arya z orszakiem; Jon, Benjen i Tyrion jada na Mur | T + S | Ned w orszaku; Tyrion na Mur |
| 62 | Zamach na Brana (najemny morderca, wilkor go zabija); Catelyn potajemnie jedzie na poludnie | T | - |
| 75 | Trydent: Joffrey, Arya i syn rzeznika; Lady zabita, Nymeria przegnana, Ogar zabija Mycaha | T (wybor gracza, jesli w orszaku) | relacje Starkow i Lannisterow |
| 80 | Benjen Stark rusza na zwiad za Mur i znika | T + S | Benjen znika (usuniety) |
| 95 | Ned w Krolewskiej Przystani, mala rada (Varys, Baelish, Pycelle, Renly, Stannis) | T | - |
| 100 | Catelyn w Krolewskiej Przystani; Baelish i sztylet z valyrijskiej stali | T | - |
| 110 | Catelyn pojmuje Tyriona w gospodzie na rozstajach | T + S | Tyrion jencem Catelyn (w droge do Doliny) |
| 118 | Klany gorskie napadaja na Gorskim Trakcie | S | bandy klanow gorskich w gorach Doliny |
| 125 | Turniej Reki w Krolewskiej Przystani (Gora, Rycerz Kwiatow, Ogar) | S | turniej GrandTourney w Krolewskiej Przystani - gracz moze startowac |
| 132 | Proces Tyriona w Orlim Gniezdzie, Bronn jego czempionem | T + S | Tyrion wolny; Bronn i Shagga przy Tyrionie |
| 138 | Jaime napada Neda na ulicy (ginie Jory Cassel); Ned ze zlamana noga | T | Jaime wyjezdza na Zachod |
| 145 | Viserys "ukoronowany" plynnym zlotem w Vaes Dothrak | T | - |
| 155 | Gora pustoszy Dorzecze | S | grabiezcy Lannisterow w Dorzeczu (spalone wsie) |
| 160 | Ned jako Reka wysyla Berica Dondarriona na Gore | T + S | oddzial Berica w Dorzeczu |
| 172 | Robert kaze zabic Daenerys; zamach z winem | T | - |
| 182 | Robert smiertelnie ranny przez dzika na polowaniu | T | - |
| 186 | Smierc Roberta; Joffrey krolem; zamach Cersei, Ned w lochu; Arya ucieka (Syrio) | T + S | Joffrey dziedziczy rod i krolestwo; Ned uwieziony |
| 188 | = dzien 0 ROT: egzekucja Neda i dalej jak w ROT | ROT | Robb krolem Polnocy |

## 3. Postacie z listy A - gdzie na starcie i jak zmieniaja strony (wedlug ksiazki)
Zmiany sa przypiete do wydarzen ROT (nazwy z ROT.Events) albo do dni prologu.
| Postac | Start (prolog) | Zmiany stron |
|---|---|---|
| Tyrion | Lannisterowie (orszak, potem Mur) | jeniec Catelyn (prolog 110-132) -> Lannisterowie; Reka Krola po smierci Neda; po Purpurowych Godach (PurpleWedding) oskarzony, zabija Tywina, ucieka z Varysem do Essos -> Druga Synowie / strona Daenerys |
| Jaime | Lannisterowie (Gwardia Krolewska) | jeniec Robba (Szepczacy Las, poczatek wojny) -> uwolniony przez Catelyn, droga z Brienne -> Lannisterowie (Lord Dowodca Gwardii w Krolewskiej Przystani) |
| Bronn | Lannisterowie (od procesu Tyriona) | po Czarnym Nurcie rycerz; po Purpurowych Godach odmawia walki za Tyriona -> rod Stokeworth (Ziemie Korony) |
| Podrick Payne | Lannisterowie (giermek Tyriona) | po ucieczce Tyriona -> przy Brienne |
| Shagga | klany gorskie Doliny | przy Tyrionie (od procesu) -> po upadku Tyriona wraca w gory (Krolewski Las jako banita) |
| Qyburn | Lannisterowie (Dzielni Kompanioni) | po smierci Tywina -> dwor Cersei (Mistrz Szeptow) |
| Sandor Clegane | dwor Roberta, potem Joffreya | po Czarnym Nurcie (BlackwaterSiege) dezercja -> wedrowiec w Dorzeczu (mozna go spotkac) -> po Krwawych Godach znika (Cicha Wyspa) |
| Varys | dwor korony | po smierci Tywina ucieka z Tyrionem -> po wydarzeniu Aegona (AegonEvent) strona Aegona |
| Petyr Baelish | dwor korony | po Czarnym Nurcie lord Harrenhal -> slub z Lysa -> Lord Protektor Doliny (Dolina) |
| Jacelyn Bywater | korona (Zlote Plaszcze) | ginie w bitwie nad Czarnym Nurtem |
| Brienne | rod Renly'ego (Tęczowa Gwardia) | po smierci Renly'ego (RenlyDeath) -> przy Catelyn (Starkowie/Tullyowie) -> po Krwawych Godach wedrowiec (szuka Sansy, z Podrickiem) |
| Theon | Starkowie (wychowanek) | poczatek wojny przy Robbie -> wojna Zelaznych Wysp z Polnoca (dzien 100 ROT) -> Greyjoyowie -> po wydarzeniu Ramsaya jeniec Boltonow |
| Osha | Wolni Ludzie -> jencem w Winterfell (prolog ok. 60) -> sluzba Starkow | po upadku Winterfell ucieka z Rickonem -> znika |
| Jeyne Westerling (zamiast Talisy - Jeff 21.10 04:00 "tak") | rod Westerlingow (Turnia, Zachod) | slub z Robbem po zdobyciu Turni -> Starkowie -> po Krwawych Godach wraca do Westerlingow; Talisa usunieta |
| Benjen | Nocna Straz | znika za Murem (prolog 80) |
| Samwell | Nocna Straz (przybywa na Mur w prologu) | po Wielkim Zwiadzie i wydarzeniu Jona (JonEvent) -> wyjazd do Starego Miasta (znika jako wojownik) |
| Yoren | Nocna Straz (werbownik) | wyjezdza z Krolewskiej Przystani z Arya i Gendrym po zamachu Cersei -> ginie (ludzie Amory'ego Lorcha) wkrotce po starcie ROT |
| Quentyn Martell | Martellowie | przed inwazja Daenerys rusza do Meereen -> ginie przy smokach |
| Hizdahr zo Loraq | szlachta Meereen | maz Daenerys (strona Daenerys) |
| Rago | khalasar Daenerys | zostaje przy Daenerys |
| Myranda, Locke | Boltonowie (serial) | zostaja przy Boltonach |
| Wun Wun | Wolni Ludzie | po wydarzeniu Jona (przepuszczenie Wolnych Ludzi przez Mur) -> przy Nocnej Strazy |

## 4. Etapy dla programisty (drugie konto)
- E0 (S): wybor startu w kreatorze postaci ("Prologue 298" / "Classic 299"); tylko nowa kampania.
- E1 (M/L): przesuniecie startu - kalendarz Armoury (Calendar.cs) start = start ROT - 188 dni prologu; zegar opowiesci Fabuli (CrashScribe Fabula) przesuniety; jedna latka na odczyty CampaignStartTime w ROT (38 miejsc w 28 typach - rozpoznanie pyt. 1) -> "start opowiesci"; wojny fabularne ROT (ROTStorylineWars) wylaczone do startu opowiesci; AI nie wypowiada wojen miedzy krolestwami Westeros w prologu (Nocna Straz - Wolni Ludzie bez zmian); pakty/sojusze Diplomacy kazdego krolestwa Westeros z krolestwem Roberta na czas prologu (sprawdzic API Bannerlord.Diplomacy: NonAggressionPact / Alliance, wygasanie przy smierci Roberta), bez wciagania sojusznikow do wojen z Wolnymi Ludzmi.
- E2 (M/L): Robert i Ned zyja na starcie (ROT_heroes.xml: ROTlord_506 Robert, ROTlord_512 Eddard alive="false" -> przy nowej grze ozywieni kodem albo wlasna latka xslt): Robert glowa rodu Baratheonow z Krolewskiej Przystani (clan_nord_1, ojciec Joffreya) i wladca krolestwa korony; Ned glowa Starkow (clan_battania_1) i wladca Polnocy, Robb dziedzic. Koniec prologu: smierc Roberta (dziedziczy Joffrey), Ned uwieziony; NedEvent ROT ma go zabic (dzis zaklada, ze nie zyje).
- E3 (M): postacie - lista A do rodow (lordowie/czlonkowie rodow), B usunieci, C i D do wynajecia; tabela zmian stron z pkt 3 przypieta do wydarzen ROT (ROT.Events.*) i dni prologu; tylko nowa kampania.
- E4 (M): sceny T z pkt 2 jako okna w wygladzie ROT (EventBase / EventPopupState - rozpoznanie), tekst EN (PL pozniej z wersja polska), obrazki: Jeff 21.10 04:00 "Generuj przez AI" - nowe obrazki scen generowane przez AI (w stylu obrazkow wydarzen ROT; najpierw lista opisow/promptow dla kazdej sceny z pkt 2; do generowania potrzebna usluga obrazow z kontem i kluczem Jeffa - Claude nie zaklada kont i nie wpisuje kluczy; do tego czasu obrazki z ROT).
- E5 (L): sceny S - orszak Roberta na mapie (Krolewski Trakt tam i z powrotem), Turniej Reki przez GrandTourney, jeniec Tyrion (RealisticCaptivity), klany gorskie Doliny, grabiezcy Gory i oddzial Berica w Dorzeczu.
- E6: autotest nowej kampanii z flaga prologu, co najmniej 220 dob (przejscie w start ROT), progi; pod kluczami MCM.
Szacunek: E0-E2 ok. 1-2 nocy, E3 1 noc, E4 1 noc, E5 1-2 nocy, testy.
