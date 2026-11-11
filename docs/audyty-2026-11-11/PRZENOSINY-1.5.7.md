# PRZENOSINY NA 1.5.7 - pomiar pracy (11.11.2026)

Pytanie Jeffa: "no ale sami mozemy zmodyfikowac mody pod wersje, nie musimy czekac".
To jest POMIAR. Nic nie zmieniono w modach ani w grze, gry nie uruchamiano, niczego nie przeniesiono.
Zrodla: cztery niezalezne analizy grup (ROT, RBM i mody bitewne, BK i reszta, nasze piec modow), porownanie bibliotek
1.4.8 (`libs`, kopie z 20.08) z 1.5.7 (`D:/Backup-Bannerlord/gra-1.5.7-dll`), notatki zmian 1.5.0-1.5.7.
Wszystkie czasy w tym dokumencie to SZACUNKI, nie pomiary.

---

## 1. ODPOWIEDZ

Tak, mozemy sami przeniesc caly zestaw na 1.5.7 i nie znalezlismy niczego, co wymagaloby zrodel ROT, RBM albo BannerKings
ani przebudowy mapy - z jednym wyjatkiem: AIInfluence jest zaciemniony i trzeba go wylaczyc albo poczekac na autora.
Praca dzieli sie na dwie bardzo rozne czesci. Pierwsza, tania i pewna: sprawic, zeby mody w ogole sie zaladowaly. Wiemy
dokladnie, co peka (kilkadziesiat miejsc w cudzych modach, okolo 30 przyczyn w Armoury) i jak to naprawic; to okolo dwoch
tygodni pracy, w duzej czesci jednym narzedziem, ktore potem posluzy przy kazdej nastepnej aktualizacji gry. Druga, droga i
niepewna: sprawdzic, czy gra zachowuje sie tak jak dotad. Gra w 1.5 przebudowala rozstrzyganie bitew, widocznosc na mapie,
postawy partii i czasy atakow w walce, a setki latek ROT, BK, RBM i naszych siedza na metodach, ktorych tresc sie zmienila.
Tego nie widac w plikach - wyjdzie dopiero w grze, w petli "crash, log, poprawka", i licze to w tygodniach (lacznie
realnie 1-2 miesiace naszym trybem, jedna zmiana = jeden test). Rzeczy, ktorych dzis nie wie nikt: czy mapa ROT wczyta sie
w 1.5.7, czy Twoj zapis z 1.4.8 w ogole sie otworzy, i czy bitwy morskie ROT dzialaja. Te trzy pytania da sie rozstrzygnac
tanio - jedna proba w osobnej kopii gry, 2-4 dni przygotowan. Dlatego nie radze ani czekac bezczynnie, ani rzucac
wszystkiego: grasz dalej na 1.4.8, a przenosiny ida w tle, zaczynajac od tej jednej proby. Twoj zapis zostaje na 1.4.8,
dopoki proba nie pokaze, ze 1.5.7 go czyta.

---

## 2. TABELA

"Miejsc peka" = pekniecia binarne (mod nie zaladuje sie albo wywroci przy pierwszym uzyciu). Ciche zmiany zachowania sa
w kolumnie ryzyka, bo ich sie nie da policzyc bez gry. Koszt: S = do godziny, M = jedna sesja, L = kilka sesji i wiecej.
Sposoby: (a) latka Harmony z naszego moda, (b) przepisanie pliku DLL narzedziem (Cecil) z malym adapterem, (c) dekompilacja
i rekompilacja, (d) zmiana XML. Pekniec twardych w cudzych modach NIE da sie naprawic sama latka Harmony - zostaje (b).

| Mod | Ile miejsc peka | Sposob naprawy | Koszt | Ryzyko |
|---|---|---|---|---|
| **BLSE, ButterLib, MCM, UIExtenderEx, Harmony** | 0 w naszym pomiarze; wydania pod 1.5.x istnieja (BLSE 1.7.5, ButterLib 2.12.0, MCM 5.13.1, UIExtenderEx 3.0.3, Harmony bez zmian) | podmiana na nowe wydania | S | UIExtenderEx 3.x to zmiana numeru glownego, a BK lata jego wnetrznosci i wszystkie mody sa zbudowane pod 2.13 - zgodnosci nie sprawdzono |
| **ROT-Core (ROT.dll + BannerColorPersistence.dll)** | 14 przyczyn, prawie kazda w 1-2 miejscach | (b) 13 regul + adapter; (a) 2 male latki (pojedynki i misja miejska, garnizon Bialych Wedrowcow); (d) 3 drobne zmiany XML, czesc opcjonalna | kod: 1-2 dni; test: L | WYSOKIE. 164 latki ROT, ok. 99 prefiksow, czesc zastepuje cale metody gry kopia z 1.4.8. Jedno `PatchAll` - jesli brak jednego celu przerywa calosc, odpadaja wszystkie kolejne latki (niepotwierdzone). Mapa, bitwy morskie, smoki, fabula, nowa gra - niesprawdzalne bez gry |
| **ROT-Content, ROT-Map, ROT-Dragon, ROT_Hideouts** | 0 (brak DLL) | (d) ewentualnie usunac 37 martwych wpisow nazw krolestw, 2 flagi broni do decyzji | S | WYSOKIE dla ROT-Map: format mapy, nawigacji morskiej i pamieci odleglosci w 1.5.7 nieznany; 236 plikow XML bez schematow 1.5.7 do porownania |
| **ROTBattleAtmosphere, ROT_AIInfluence_Compat** | 0 | nic | 0 | niskie |
| **RBM (RBMAI, RBMCombat)** | 3 przyczyny, ok. 25 miejsc | (b) tabela podmian + 1 adapter; (a) 1 latka uzupelniajaca nowe czasy "kontaktu"; (d) 5 flag w XML - decyzja Jeffa | kod: S; test: M | SREDNIE. Czasy atakow zmienily ZNACZENIE przy tej samej nazwie - bez poprawki wyjatek przy pierwszym trafieniu w kazdej bitwie, a czesc odczytow po cichu znaczy co innego. 17 z 192 celow latek ma zmieniona tresc. Nowy poscig druzynowy gry pod prefiksami RBM |
| **ImmersiveBattlefields** | 1 metoda | (b) + (a) wywolanie z nowego zdarzenia rozstawienia | S | niskie |
| **VanillaAiSieges, EpicSieges, DynamicReinforcements, DismembermentPlus, ArtemsLivelyAnimations, RBMConfig, RBMTournament** | 0 | nic | 0 | niskie; do obejrzenia w autotescie oblezenia |
| **StrategicCampaignAI145** | 0 | nic | 0 | srednie: nadpisuje 3 metody, ktorych wersje bazowe w 1.5.7 sie zmienily |
| **BannerKings.Redux** | 18 twardych + 1 cicha, 70 miejsc (13 z 18 to dopisany parametr albo zmiana nazwy) | (b) adapter ok. 20 metod; (a) 2-3 latki przywracajace logike (garnizon, symulacja bitwy) | kod: 1-2 dni; test: L | WYSOKIE. 236 latek + 22 dynamiczne, 191 odwolan po nazwie, ok. 100 wstawek w ekrany gry (zmieniony ekran = pusty panel bez bledu), ok. 390 pol w zapisie. Trzeba wybrac moment startu BK w nowym tworzeniu postaci (zly wybor = inicjalizacja w zlej chwili). Trzy mechanizmy gry znikly (cel partii, odswiezanie widocznosci, garnizon) |
| **BKROTPatch** | 0 wobec gry | nic | 0 | srednie: lata BK i ROT (114 odwolan po nazwie); jesli tamte przeniesiemy binarnie, nazwy zostaja |
| **Bannerlord.Diplomacy** | 4 twarde, 7 miejsc | (b) | S | srednie: 107 odwolan po nazwie, 30 wstawek w ekrany |
| **BetterEconomy** | 2 klasy + 2 wywolania | (b) z dopisaniem 2 metod albo (c) tylko tych 2 klas | M | niskie-srednie; uwaga na kolejnosc parametrow limitu partii (nowy jest w SRODKU) |
| **DynamicTroopEquipmentReupload** | 1 twarde, 2 miejsca | (b) adapter czytajacy pole | S | niskie-srednie: 93 odwolania po nazwie |
| **RealisticBannerlord, Spoils of War, TournamentsXPanded** | 0 | nic (ew. wersja w SubModule.xml) | 0-S | niskie; Spoils of War dubluje sie z nowymi pobojowiskami gry (rozdz. 4) |
| **AIInfluence** | 4 twarde | BRAK - plik zaciemniony, narzedzie go nie wczytuje | - | mod do wylaczenia albo czekamy na autora |
| **NavalDLC** | modul gry - przychodzi z 1.5.7 | - | - | ROT siega do jego prywatnych skladowych (13 latek, ok. 25 nazw); wiekszosc nazw jest, sygnatur nie porownano |
| **ForgeView** | 0 | rekompilacja | S | zalezy od BK |
| **GrandTourney** | 0 | rekompilacja + 1 turniej testowy | S | niskie |
| **RealisticCaptivity** | 0 twardych; 1 z 6 celow ma zmieniona tresc | rekompilacja | S | srednie: ucieczka konno ze spotkania, okup; nowe "wasnie krwi" (AI scina jencow) |
| **CrashScribe** | 1 twarde; 4 cele ze zmieniona trescia | poprawka w zrodle | S | Mends (latki na cudze mody) do oceny dopiero, gdy BK i ROT wstana |
| **Armoury** | 23 twarde (19 przyczyn) + 8 cichych przyczyn (latka po prostu nie wejdzie) + 49 transpilerow, z czego co najmniej 8 na zmienionych metodach | zrodlo: 13 poprawek "dopisz argument / zmien nazwe" (1-2 sesje); 8 przebudow (2-4 sesje): front wojny bez postaw partii, kryjowki, karczma, statki, incydenty, zamowienia kuzni, garnizony; potem ponowny audyt | L | NAJWYZSZE. Rozstrzyganie starc przeniesione w grze w inne miejsce: 102 uzycia spotkania gracza i 42 latki na zdarzenie bitwy. W probce 399 celow 51 ma zmieniona tresc (ok. 13% - szacunek, nie spis). To tygodnie, nie dni |

---

## 3. PLAN PRZENOSIN

Zasada: gra 1.4.8 Jeffa pozostaje NIETKNIETA. Wszystko dzieje sie w drugiej kopii gry w osobnym katalogu.
Kryterium kazdego kroku to autotest i log, nie "wyglada dobrze".

### Krok 0. Srodowisko testowe obok gry (0,5-1 dnia)
- Po zakonczeniu cofania do 1.4.8: zablokowac aktualizacje Steam i zrobic pelna kopie zapasowa katalogu gry 1.4.8.
- Druga, pelna instalacja 1.5.7 w osobnym katalogu (np. na D:). Kopia `gra-1.5.7-dll` NIE wystarczy - ma tylko DLL,
  brak plikow danych, parametrow i zasobow. Trzeba pobrac pelna gre 1.5.7 i odlozyc ja na bok.
- Mody skopiowane do tej drugiej instalacji; przepisywacz pracuje tylko na kopiach, oryginaly maja `.bak`.
- UWAGA: obie instalacje dziela `Documents\Mount and Blade II Bannerlord` (zapisy, ustawienia MCM, logi). Przed kazdym
  uruchomieniem 1.5.7 kopia Configs i zapisow (jak w autotescie); zapis do prob to KOPIA pod inna nazwa, nigdy oryginal.
- **Stop, jesli** nie da sie trzymac dwoch instalacji naraz (miejsce na dysku, Steam nadpisuje jedna druga) - wtedy
  najpierw rozwiazac to, bo bez tego Jeff traci gre na czas prac.

### Krok 1. Narzedzia: przepisywacz + adapter + bramka (1-2 dni)
- Jeden przepisywacz (Cecil) z tabela regul: podmiana odwolania na adapter, dopisanie metody przekazujacej, zmiana
  sygnatury, zmiana nazwy celu latki. Jeden maly DLL-adapter zbudowany pod 1.5.7. Ten sam silnik dla ROT, RBM, BK,
  Diplomacy, DTE, BetterEconomy, ImmersiveBattlefields.
- Bramka: po przepisaniu ponowne porownanie z bibliotekami 1.5.7 ma pokazac zero pekniec.
- **Stop, jesli** okaze sie, ze przepisany DLL nie przechodzi ladowania (np. BLSE albo gra odrzuca zmieniony plik).
  Wtedy caly pomysl "binarnie, bez zrodel" upada i zostaje czekanie na autorow.

### Krok 2. Biblioteki bazowe (0,5 dnia)
- BLSE 1.7.5, ButterLib 2.12.0, MCM 5.13.1, Harmony; UIExtenderEx 3.0.3 najpierw przepuscic przez to samo porownanie
  jako "nowa biblioteke" (BK lata jego wnetrze).
- Test: czysta gra 1.5.7 + same biblioteki bazowe dochodzi do menu.
- **Stop, jesli** UIExtenderEx 3.x jest niezgodny z modami zbudowanymi pod 2.13 i nie ma wydania 2.x pod 1.5 - to
  dodatkowa, niepoliczona praca we wszystkich modach z ekranami.

### Krok 3. ROT - proba rozstrzygajaca (1-2 dni regul + 1 popoludnie proby)
- Reguly 1-13 dla ROT-Core, zmiana celu latki karawan morskich, ewentualnie wersja w SubModule.xml.
- Proba: gra 1.5.7 + biblioteki + sam ROT. Trzy pytania: (1) czy kampania startuje i mapa ROT sie wczytuje, (2) czy
  wszystkie latki ROT weszly (czy jedno `PatchAll` nie urwalo sie w polowie), (3) czy kopia zapisu Jeffa sie otwiera
  (ta czesc dopiero z pelnym zestawem, krok 6, ale sam zapis nowej kampanii ROT mozna sprawdzic juz tu).
- **Stop, jesli** mapa ROT nie wczytuje sie w 1.5.7 (nawigacja morska, pamiec odleglosci) - tego bez narzedzi autorow
  ROT nie naprawimy rozsadnym kosztem. Wtedy czekamy na ROT pod 1.5 i konczymy na tym kroku, tracac 3-5 dni.
- **Stop, jesli** bitwy morskie ROT wywracaja sie w sposob wymagajacy przepisania ich latek na NavalDLC od zera.

### Krok 4. RBM i mody bitewne (1 dzien kodu + 2-3 sesje testow)
- Tabela podmian czasow atakow (recznie, bo to zmiana znaczenia), adapter perkow, ImmersiveBattlefields, decyzja Jeffa
  o 5 flagach machin.
- Test: autotest bitwy armii (200 vs 240) i oblezenia; porownanie z logami z 1.4.8 (straty, czas, zachowanie linii).
- **Stop, jesli** AI RBM po zamianie czasow zachowuje sie wyraznie inaczej i nie da sie wskazac przyczyny w logu -
  wtedy RBM zostaje wylaczony w srodowisku testowym do czasu wydania autora, a reszta idzie dalej.

### Krok 5. BannerKings i reszta cudzych (2-3 dni kodu + testy L)
- Adapter ok. 20 metod, wybor momentu startu BK w tworzeniu postaci, latki przywracajace garnizon i symulacje;
  potem Diplomacy, DTE, BetterEconomy; BKROTPatch bez zmian; AIInfluence wylaczony.
- Test: nowa kampania 30-60 dni autotestem; kontrola ekranow BK (tytuly, rada, kuznia, krolestwo), log BKROTPatch.
- **Stop, jesli** puste albo rozjechane ekrany BK ida w dziesiatki - to znak, ze ekrany gry zmienily sie za bardzo
  i taniej poczekac na BK pod 1.5.

### Krok 6. Nasze mody (Armoury: tygodnie)
- 6a. ForgeView, GrandTourney, RealisticCaptivity, CrashScribe: rekompilacja pod 1.5.7 (1 sesja).
- 6b. Armoury - kompilacja: 13 prostych poprawek (1-2 sesje).
- 6c. Armoury - przebudowy: front wojny, kryjowki, karczma, statki, incydenty, zamowienia, garnizony (2-4 sesje).
- 6d. Armoury - ponowny audyt latek na zmienionych metodach i calego obszaru starc, transpilery wedlug linii startu
  ("Latki czynnego modelu", liczniki trafien), autotesty: bitwa, oblezenie, rajd, morze, kuznia, ekonomia.
- Dopiero tu proba z KOPIA zapisu Jeffa na pelnym zestawie.
- **Stop, jesli** autor ROT albo BK wyda w tym czasie wersje pod 1.5 - przechodzimy na wydanie autora (nasze reguly
  dla tego moda ida do kosza, narzedzie i bramka zostaja) i sprawdzamy od nowa wszystkie nasze latki na ten mod.

### Krok 7. Decyzja o przesiadce
- Kryterium: pelny test zbiorczy na 1.5.7 bez nowych bledow wzgledem 1.4.8 + rozstrzygnieta sprawa zapisu.
- Przesiadka Jeffa dopiero po tym, z pelna kopia 1.4.8 (gra + mody + Documents) do powrotu.

**Razem (szacunek):** kroki 0-3, czyli odpowiedz "czy to w ogole ma sens": 3-5 dni. Do stanu "wszystko sie laduje
i kampania startuje": ok. 2 tygodnie. Do stanu "mozna grac jak na 1.4.8": 1-2 miesiace, z duzym rozrzutem.

---

## 4. CO ZYSKUJEMY NA 1.5.7 I CO SIE DUBLUJE

Zyski z notatek (te, ktore wyszly w analizach): start zaawansowany nowej gry, porty i blokady morskie zamkow, lepszy
marsz kolumny i wybor drogi posilkow, poscig malym oddzialem za samotnym napastnikiem, lepsze tarcze pod ostrzalem
(nowe czasy "kontaktu"), cechy charakteru z realnymi skutkami, wasnie krwi, pobojowiska i wraki po bitwach,
odradzanie partii klanowych, +15% predkosci na mapie, nowe progi lojalnosci i buntu, poprawki flag broni w XML.

Co z tego dubluje albo psuje nasze systemy - kazda pozycja to decyzja Jeffa (wylaczyc / zostawic / wpiac w nasza ksiege):

| Nowosc gry | Z czym sie zderza | Co trzeba zrobic |
|---|---|---|
| Pobojowiska i wraki: denary, towary, ranni do werbunku znikad | zamknieta ekonomia ("nic z niczego"), BattlefieldLaw, CasualtyLoot, LootTally, Spoils of War | wylaczyc albo zasilac z naszej puli lupow |
| Cechy charakteru: mnozniki okupu, lupu z rajdu, jencow, zoldu, lojalnosci | okupy, prawo jencow, Devastation, zoldy | kalibracja - efekty sie MNOZA z naszymi |
| Wasnie krwi: AI w wasni zawsze scina jencow | straznik fabuly, niewola i okupy RealisticCaptivity | sprawdzic, czy straznik to lapie; grozi wycieciem postaci fabuly ROT |
| Odradzanie partii klanowych "w tej samej konfiguracji" | pobor z pol, brak sztucznych doplat dla lordow | najpewniej wylaczyc; z notatek nie wynika, czy dotyczy tylko gracza |
| +15% predkosci na ladzie i morzu | MapSpeed, marsz kolumny, trasy karawan | ponowna kalibracja |
| Progi lojalnosci i buntu | FolkAidLoyalty, nasze reguly buntu, model BK | zalezy od tego, co odziedziczy BK |
| Poscig za samotnym napastnikiem | HorsePursuit, ostroznosc bohatera, prefiksy RBM | mozliwa podwojna reakcja - obejrzec w bitwie |
| Postawy partii zastapione przelacznikami | WarFront334 stoi na postawach | nie dublowanie, tylko znikniecie fundamentu - przebudowa |
| Usuniety ekran finansow klanu | nasze linie w finansach (ClanIncomeBook, GoldForecast) | znalezc nowe miejsce albo wlasny panel |
| Porty i blokady zamkow | prefiksy ROT z kopia logiki AI z 1.4.8 | zamki ROT nie maja portow - nowosc najpewniej martwa, nieszkodliwa |
| Nowy start zaawansowany | przycisk ROT "Into the Realm" go omija | zostaje po staremu (adapter z domyslnymi opcjami) |

Wniosek: wiekszosc "zyskow" 1.5.7 dla naszego zestawu to rzeczy, ktore juz mamy we wlasnej wersji albo ktore trzeba
bedzie wylaczyc. Czysty zysk to poprawki silnika i AI formacji oraz to, ze przyszle wersje modow beda wychodzic pod 1.5.

---

## 5. REKOMENDACJA

**Przenosic rownolegle w tle, etapami, z twarda bramka po kroku 3. Nie przesiadac sie teraz. Nie czekac bezczynnie.**

Uzasadnienie:
1. **Nie teraz jako glowna praca**: koszt to 1-2 miesiace, zysk dla rozgrywki maly (rozdz. 4), a seria 413-423 czeka
   w kodzie na test na 1.4.8. Przesiadka dzis = brak gry na tygodnie.
2. **Nie samo czekanie**: trzy najwieksze niewiadome (mapa ROT, zapis, zaladowanie przepisanych DLL) rozstrzyga jedna
   tania proba, 3-5 dni. Po niej wiemy, czy droga jest otwarta, zamiast zgadywac.
3. **Praca sie nie marnuje**: przepisywacz, adapter i bramka sluza przy kazdej nastepnej aktualizacji gry. Poprawki
   zrodel Armoury pod 1.5.7 beda potrzebne niezaleznie od tego, kto przeniesie ROT i BK. Do kosza moga pojsc tylko
   reguly dla moda, ktorego autor wyda wlasna wersje - to 1-2 dni na mod.
4. **Kolejnosc wymusza zaleznosc**: Armoury, BK i BKROTPatch nie da sie nawet przetestowac, dopoki nie wstanie ROT.
   Dlatego ROT jest pierwszy i jest bramka.
5. **Koszt ukryty**: dopoki trwaja przenosiny, kazda nowa zmiana w Armoury na 1.4.8 trzeba bedzie potem sprawdzic
   drugi raz na 1.5.7. Im dluzej oba tory ida obok siebie, tym wiecej podwojnej pracy - stad etapy i punkty "stop".

**Zapis gry Jeffa:**
- Zostaje na 1.4.8. Zgodnosc zapisow 1.4.8 -> 1.5.7 NIE jest potwierdzona w notatkach, a w grze znikly pola, ktore
  zapis zawiera (nazwa krolestwa, typ bitwy, znana kryjowka, kotwica, postawy partii); BK ma ok. 390 pol w zapisie.
- W srodowisku testowym otwierac wylacznie KOPIE zapisu pod inna nazwa. Zapis raz zapisany w 1.5.7 najpewniej nie
  wroci do 1.4.8.
- Trzeba sie liczyc z tym, ze przesiadka bedzie oznaczac NOWA kampanie. Jesli obecna kampania jest wazna - dograc ja
  na 1.4.8 i przesiasc sie przy nastepnej.
- Do czasu decyzji: Steam z zablokowana aktualizacja + pelna kopia katalogu gry 1.4.8 na D:, zeby kolejna
  aktualizacja nie wymusila przenosin wbrew planowi.

---

## 6. CZEGO POMIAR NIE OBEJMUJE

- **Zachowania w grze** - niczego nie uruchomiono. Wszystkie oceny "zaladuje sie" to wniosek z plikow, nie obserwacja.
- **Tresci metod pod latkami** poza probkami: RBM porownany w calosci (192 latki), Armoury tylko probka 399 celow
  (latki z typem w zmiennej nie sa policzone), ROT (164) i BK (236 + 22) - nieporownane.
- **Odwolan po nazwie budowanych w biegu**: BK 191, BKROTPatch 114, Diplomacy 107, DTE 93, Armoury 135, CrashScribe 29.
- **Ekranow**: brak kopii 1.4.8 bibliotek interfejsu (25 bibliotek), wiec wstawek BK (ok. 100), Diplomacy (30),
  78 prefabow ROT i naszych widzetow nie porownano.
- **Plikow danych gry 1.5.7**: kopia ma tylko DLL. Nie porownano parametrow walki, schematow XML, 236 plikow XML ROT,
  488 flag "false" w danych ROT, pelnych zamiennikow parametrow RBM.
- **Mapy i scen ROT**: format mapy, nawigacji morskiej, pamieci odleglosci, 92 sceny, shadery.
- **Kodu natywnego silnika**: kolizje, luki bloku tarcza, bilans obrazen, predkosci koni.
- **Zapisow gry** - w ogole.
- **Zgodnosci UIExtenderEx 3.x** z modami zbudowanymi pod 2.13.
- **Stalych i enumow wkompilowanych** w cudze DLL (porownano tylko cztery glowne biblioteki).
- **Launchera/BLSE**: czy wymagana wersja gry w SubModule.xml blokuje, czy tylko ostrzega.
- **Modow spoza czterech grup** - z ok. 40 modow zbadano te wymienione w tabeli; pozostale drobne mody nie maja wyniku.
- **Zalozen niepotwierdzonych proba**: ze przepisany DLL przejdzie ladowanie; ze brak jednego celu przerywa `PatchAll`
  ROT; ze nadpisanie zniknietej metody garnizonu w BK zostanie martwym kodem, a nie wywroci typu; ze asercja o nazwach
  krolestw jest w wydaniu tylko wpisem do logu.
- **Rozbieznosc miedzy analizami**: jedna podaje, ze zdarzenie konca tworzenia postaci wola sie 10 razy (0..9), druga,
  ze 11 (0..10). Do sprawdzenia w dekompilacji przed pisaniem adaptera.
- **Czasow**: wszystkie szacunki pochodza z liczby miejsc i naszego dotychczasowego tempa, nie z proby.

Pliki robocze analiz (scratchpad sesji `264cfac3-8338-4c40-91b8-6c1304da3584`): `rot_dump.txt`, `rot157\`, `rbmgrp\`,
`n157\cs\`, `nasze157\` (`src.out`, `il.out`, `cmp.out`). Scratchpad jest w Temp - jesli maja przetrwac, skopiowac poza Temp.
