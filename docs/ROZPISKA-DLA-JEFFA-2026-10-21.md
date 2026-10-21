# Rozpiska dla Jeffa — 21.10.2026, ok. 10:00 (zegar komputera)

Co jest już w grze (wersje 96–101, wgrane od 20.10 wieczorem do 21.10 rano) i co jest teraz robione. Każda wersja przeszła trzy testy automatyczne bez wywrotki gry i bez błędów w logu Armoury: bitwy, nową kampanię na 180 dób oraz 160 dób na starym zapisie (przy 101 był to prolog na 230 dób). Twoje zapisy są nietknięte, testy sprawdzają to sumami kontrolnymi.

---

## CZĘŚĆ 1. CO JEST W GRZE

### Wersja 96 (20.10, 19:30) — kuźnia, przedmioty, turnieje, miasta, bandyci

**Kuźnia**
- **Nie wywraca gry.** Crash z 20.10 brał się z błędu w oknie projektowania broni. Jest naprawiony.
- **Jeden pasek czasu kucia.** Najpierw idzie wytop surowca, którego nie da się przerwać, a potem kucie, które można przerwać. Gdy wychodzisz, zabierasz to, co już jest wykute, a reszta czeka w kuźni na Twój powrót. Nocą kowale śpią, więc kucie stoi.
- **Przepisy liczone z wagi surowca.** Len, skóra, żelazo, stal i róg schodzą w kilogramach, tyle, ile faktycznie wchodzi w przedmiot, plus odpad. Napoczęta sztuka starcza na następne przedmioty. Koniec z absurdami w rodzaju „strzały za 14 sztab” czy „łuk za 8 lnu”.
- **Przetop oddaje mniej, niż wziął przepis** (zgar, żużel). Nie da się zarabiać na kuciu i przetapianiu w kółko.
- **Opłaty mówią, za co płacisz.** Komunikat „You paid 2” to była dobowa opłata za kuźnię w Seagard, a teraz każdy komunikat podaje powód.

**Rzadkie przedmioty**
- **Limit na rodzaj przedmiotu.** Legendarnych (L) jest od 1 do 6 na rodzaj, na przykład 1–6 legendarnych hełmów na cały świat, a mistrzowskich (MW) od 1 do 60. Limit jest losowany raz na kampanię.
- **Nadmiar spada do zwykłej jakości.** Jeśli na półkach jest więcej sztuk, niż pozwala limit, nadwyżka staje się zwykła.
- **W teście:** po 180 dniach na świecie było 16 legendarnych i 83 mistrzowskie przedmioty.

**Wzory łuków i kusz**
- **Bez tieru VI na start.** Wylosowanie tieru VI na starcie było błędem. Zapis jest naprawiony, a nowy wzór może być najwyżej o jeden tier wyższy od najlepszego, który już znasz.
- **Słynne łuki do wykucia.** Ravens Teeth, Goldenheart i łuk olbrzymów kujesz jak zwykłe łuki, jeśli znasz ich wzór.

**Turnieje**
- **Okup** płaci tylko ten, kogo sam powaliłeś. Płaci pełną wartość, ale tylko z tego, co ma, a jeśli go nie stać, oddaje zbroję.
- **Pula turnieju** nie jest już wypłacana dwa razy (to był błąd).

**Miasta, drogi i ludzie**
- **Menu wioski i zamku** jest uporządkowane jak menu miasta.
- **Drogi Valyrii** dają +25% szybkości. W Essos są szlaki karawan.
- **Długi miast.** Gdy kasa miasta jest pusta, rada pożycza u mieszczan albo w Banku, a w potrzebie ściąga podatek nadzwyczajny.
- **Bandyci noszą jedzenie i złoto** i jedzą codziennie. Głodna banda traci ludzi, bo część odchodzi.
- **Sakiewki zwykłych ludzi** liczą się według potrzeb. Koszyk zakupów ludu obejmuje też płótno i sukno.

### Wersja 97 (20.10, 23:20) — wioski boczne, obóz, szkolenie, posiłki, surowce

**1. Wioski boczne działają jak wioski główne, tylko bez bohaterów**
- **Co można:** wjechać, handlować ich towarem, inwestować w ich prace, przespać się u wieśniaków, rozbić obóz obok, obrabować je albo wziąć okup.
- **Każda osobno.** Rabunek wioski głównej pali już tylko ją, a każdą wioskę boczną rabujesz osobno, z jej własnego menu.
- **Bronią ich własni mieszkańcy**, tak jak chciałeś:
  - jeden obrońca na 10 mieszkańców, najwyżej 60 (chłopi, włócznicy i łucznicy);
  - polegli ubywają z tej wioski i przez około 60 dni brakuje jej rąk do pracy;
  - przy przewadze 4:1 wioska poddaje się bez walki;
  - do tej bitwy nie dochodzą posiłki z Dynamic Reinforcements;
  - jeśli przegrasz, Twój łup trafia do magazynu wsi, a Twoi jeńcy są uwalniani.

**2. Obóz**
- **Pora obozu:** cały świat obozuje od 21:00 do 3:00. Twoje ustawienia w MCM zmieniłem z 0:00–6:00 na 21:00–3:00, a kopia starego pliku leży obok.
- **„Walk through the camp”:** spacer po obozie. Namioty ROT stoją według liczby ludzi całej armii, są ogniska, a konie stoją uwiązane.
  - **WAŻNE: ze sceny wychodzisz tylko klawiszem Tab.** Esc otwiera menu gry, a tam „Exit to Main Menu” porzuca kampanię bez zapisu.
- **Wóz taborowy:** wiezie 500 kg, ale kolumna idzie 10% wolniej. Zbuduje go stolarz w menu „Go to the smithy” → „Visit the cartwright and saddler”.
- **Siodło transportowe** dodaje koniowi 50 kg udźwigu.

**3. Szkolenie wojska tylko w obozie**
- **W marszu nikt nie ćwiczy.** W obozie wybierasz „Train the men”: najwyżej 8 godzin dziennie, za dnia.
- **Przykład:** 60 ludzi drugiego poziomu przy Przywództwie 150 dostaje za 8 godzin 27,8 doświadczenia na głowę. Dawniej dzień postoju dawał 18,5, a dzień marszu 11,1.
- **Mistrz musztry:** do Twojego Przywództwa dochodzi połowa Przywództwa najlepszego towarzysza.
- **Oblężenie i cudza armia:** ludzie ćwiczą sami w godzinach postoju.
- **Perki:** 12 perków treningu ma nowe działanie i nowe opisy.
- **Lordowie komputera** też ćwiczą tylko w obozie (włączone w wersji 100, Twoja decyzja).

**4. Posiłki Dynamic Reinforcements według odległości**
- **Czas dojścia:** 30 sekund plus 2 minuty bitwy na każdą godzinę marszu.
- **Limit:** kto ma dalej niż 2 godziny marszu, nie przychodzi wcale („is too far to reach us in time”).

**5. Czardrzewo i smocza kość**
- **Skąd:** czardrzewo pochodzi z 6 osad Wolnych Ludzi za Murem (około 4000 d za sztukę), a smocza kość z 5 osad Valyrii i Essos (około 14 000 d).
- **Dopływ jest mały:** mniej więcej 1 sztuka na kilka–kilkanaście dni na region.
- **Kucie:** z jednej sztuki wykujesz łuk z czardrzewa albo łuk ze smoczej kości.
- **Bez darmowego surowca:** zadania i zdarzenia gry nie dają już tych surowców z niczego.

### Wersja 98 (21.10, 00:55) — plotki za kufel, pijaństwo i kac

**Plotki w karczmie**
- **Kufel:** plotek słuchasz dopiero, gdy postawisz kufel piwa albo ale (około 1 d). Pieniądze dostaje karczmarz.
- **Ile plotek z kufla** zależy od Uroku:

  | Urok | Plotek z kufla |
  |---|---|
  | 20 | 1 |
  | 80 | 2 |
  | 150 | 3 |
  | 200 | 4 |
  | 250 | wszystkie |

- **Nowe plotki:** gdzie trwa turniej i który lord szuka żony dla syna albo męża dla córki. Wszystkie plotki są prawdziwe, a raz usłyszana nie wraca.
- **Doświadczenie:** każda plotka daje 5 doświadczenia Uroku.

**Pijaństwo i kac**
- **Kiedy się upijasz:** po 3–5 kuflach, zależnie od Wytrzymałości i Atletyki.
- **Pijany:** idziesz po mapie o 15–25% wolniej i musisz dospać (6 godzin plus 1 godzina za każdy kufel ponad próg).
- **Kac:** następnego dnia do 18:00 tracisz 10% szybkości i 10% morale, a łóżko nie daje wtedy premii.

### Wersja 99 (21.10, 03:50) — markietani w dużym obozie

- **Gdzie:** w menu obozu jest opcja „Visit the sutlers' row”, gdy masz co najmniej 150 ludzi albo jesteś w armii (w cudzej też, opcja jest wtedy w menu armii).
- **Ile kramów:** 1 na 100 ludzi, najwyżej 12.
- **Cyrulicy** leczą rannych: 8 rannych na kram na dobę. Za człowieka płacisz 15 d razy jego poziom plus 1, czyli żołnierz 1. poziomu kosztuje 30 d, a 5. poziomu 90 d.
- **Płatnerze polowi** naprawiają zużyty sprzęt Twoich ludzi, 6 sztuk na kram na dobę. Płacisz jak u kowala w mieście plus 30%, a materiał schodzi z półki miasta.
- **Zabawa (ale, kości):** morale +6, najwyżej +10, raz na dobę, około 2 d na człowieka.
- **Sklep:** towar pochodzi z półek najbliższego miasta, z narzutem 30%. Markietani skupują też Twój łup, do 1500 d na kram na dobę.
- **Nic nie bierze się z niczego:** pieniądze trafiają do kasy miasta i do wsi, a towar znika z półki miasta.

### Wersja 100 (21.10, 05:50) — kryjówki bandytów

- **Widzenie zależy od Zwiadu (Scouting):**

  | Zwiad | Szansa, że zobaczysz kryjówkę |
  |---|---|
  | do 10 | 0% (nie widzisz jej, nawet gdy na nią najedziesz) |
  | 30 | około 10% |
  | 100 | około 47% |
  | 200 | 100% |

  Rzut jest raz, gdy kryjówka wejdzie w zasięg wzroku. Po odjeździe i powrocie rzut jest nowy.
- **Plotka w karczmie:** gdy usłyszysz przy kuflu, że koło X jest obozowisko bandytów, kryjówka wyświetli się normalnie, bo wiesz, gdzie szukać. Ta wiedza zostaje do rozbicia kryjówki.
- **Miejsce:** kryjówka nie powstaje bliżej niż 10 jednostek mapy od miasta, zamku czy wsi i bliżej niż 6 od wioski bocznej. Z 266 miejsc kryjówek w ROT zostają 183.
- **Po rozbiciu** nowa kryjówka pojawia się co najmniej 25 jednostek dalej (około 120 km), więc zawsze trzeba szukać od nowa. Bandy komputera trzymają się tych samych zasad.
- **Szkolenie lordów komputera tylko w obozie** — Twoja decyzja, ustawione w MCM.

### Wersja 101 (21.10, 08:05) — prolog 298, postacie kanonu, tablica ogłoszeń

**Prolog 298 (tylko w NOWEJ kampanii)**
- **Wybór startu:** przy starcie nowej kampanii pojawia się okno „Choose your start”:
  - **„Prologue 298 – the King rides north”:** gra zaczyna się w dniu, gdy król Robert wyjeżdża z Królewskiej Przystani do Winterfell, czyli 188 dni przed startem fabuły ROT;
  - **„Classic 299”:** start jak dotąd, od egzekucji Neda.
- **W prologu:**
  - **Robert i Ned żyją.** Robert jest królem i głową Baratheonów z Królewskiej Przystani, a Ned głową Starków i panem Północy.
  - **Królestwa Westeros żyją w pokoju:** nie ma wojen fabularnych, a komputer nie wypowiada wojen. Wyjątek: Nocna Straż dalej walczy z Wolnymi Ludźmi.
  - **Pakty:** wszystkie królestwa Westeros mają z królestwem Roberta pakty i sojusze (mod Diplomacy), więc na mapie widać, że trzymają z królem.
  - **Bandyci, kryjówki i najazdy Wolnych Ludzi** działają normalnie.
- **Dzień 186:** Robert ginie i królem zostaje Joffrey. Ned trafia do lochu w Królewskiej Przystani, a pakty wygasają.
- **Dzień 188 = start ROT:** ROT wykonuje egzekucję Neda, Północ przejmuje Robb i dalej fabuła idzie jak zwykle (wojna Pięciu Królów). Wszystkie wydarzenia ROT są przesunięte tak, że zaczynają się dopiero po prologu.
- **Sprawdzone testem:** prolog na 230 dób przeszedł. Robert zginął w dniu 186, Ned w lochu, egzekucja 9 dni później, Robb władcą Północy.

**Postacie kanonu w swoich rodach (tylko w NOWEJ kampanii)**
- **22 postacie** (Tyrion, Jaime, Bronn, Theon, Brienne, Varys, Littlefinger, Ogar i inni) nie są już do wynajęcia w karczmie. Są członkami swoich rodów.
- **9 postaci jest usuniętych** (lista B, np. Hodor), bo ich miejsce jest w fabule, a nie w drużynie gracza.
- **Zmiany stron według książki:** 36 zmian przypiętych do wydarzeń ROT. Przykłady:
  - Tyrion po Purpurowych Godach ucieka do Essos, na stronę Daenerys;
  - Brienne po śmierci Renly’ego trafia do Catelyn;
  - Bronn dostaje Stokeworth.
- **Jeyne Westerling zamiast Talisy** (Twoja decyzja) poślubia Robba.
- **Ratunek:** jeśli ROT zniszczy ród, jego ludzie przechodzą tam, gdzie powinni według książki, na przykład ludzie Renly’ego do Tarthu.

**Tablica ogłoszeń w miastach**
- **„Read the notice board”** w menu miasta: lista zleceń dostępnych w okolicy. Każde ma kartę: co trzeba zrobić, za ile, kto płaci, do kiedy i gdzie. Przyciski:
  - „Go and see” (idź na miejsce);
  - „Talk now” (porozmawiaj od razu);
  - „Mark on the map” (zaznacz na mapie).
- **Kapitan straży miasta:** wywiesza listy gończe na bandy z okolicy. Nagrodę płaci kasa miasta z nadwyżki, a pan miasta może dołożyć. Wypłata jest po rozbiciu bandy. W Królewskiej Przystani listy wywieszają Złote Płaszcze.
- **Sołtysi wsi** wywieszają swoje ogłoszenia. Płacą z kiesy wsi, a za wykonanie dostajesz +2 relacji.

**Poza wersjami**
- **VoiceActingPatchRemake jest wyłączony** (Twoja decyzja). Kopia listy modów leży obok.

### Do sprawdzenia przez Ciebie w grze (testy automatyczne tego nie klikają)
- Spacer po obozie (pamiętaj: wychodzisz klawiszem Tab).
- Wjazd do wioski bocznej, handel, nocleg i bitwa z mieszkańcami.
- „Train the men” i komunikat dnia „Drill today”.
- Bitwa blisko i daleko od swoich, czyli kiedy dochodzą posiłki.
- Kucie łuku z czardrzewa (surowiec najpierw trzeba kupić we wsi za Murem).
- Nowa kampania z prologiem: okno wyboru startu, pokój i pakty na mapie.

---

## CZĘŚĆ 2. CO TERAZ ROBIĘ (Twoja decyzja z 09:40: „rób 1, 2, 4, 5, 6 i 7”, a po opłaceniu planu dochodzi też 3)

### 1. Sceny prologu: 23 okna według książki (W TOKU — rozpoznanie techniczne)
- **Jak to będzie wyglądać:** w nowej kampanii z prologiem, w określone dni wyskakuje okno identyczne z oknami wydarzeń ROT: duży obrazek u góry, tytuł, dwa akapity tekstu po angielsku i przycisk „Continue...”. Czas gry stoi, dopóki okna nie zamkniesz.
- **Dni i sceny:**

  | Dzień | Scena |
  |---|---|
  | 0 | Orszak króla wyrusza |
  | 5 | Wilkory w śniegu |
  | 12 | Ślub Daenerys i Drogo |
  | 35 | Król w Winterfell |
  | 40 | Upadek Brana |
  | 48 | Ned Ręką Króla |
  | 55 | Wyjazd na południe i na Mur |
  | 62 | Zamach na Brana |
  | 75 | Trydent |
  | 80 | Benjen za Murem |
  | 95 | Mała rada |
  | 100 | Catelyn i sztylet |
  | 110 | Catelyn chwyta Tyriona |
  | 118 | Klany górskie |
  | 125 | Turniej Ręki |
  | 132 | Proces Tyriona |
  | 138 | Jaime napada Neda |
  | 145 | Korona Viserysa |
  | 155 | Góra w Dorzeczu |
  | 160 | Beric wyrusza |
  | 172 | Zamach na Daenerys |
  | 182 | Robert ranny |
  | 186 | Ned aresztowany |

- **Kolejka:** jeśli okno nie mogło się pokazać (bo trwała bitwa albo rozmowa), pokaże się zaraz po powrocie na mapę. Kilka zaległych okien idzie po kolei. Gra pamięta w zapisie, które już widziałeś.
- **Obrazki:**
  - na razie pasujące obrazki z ROT;
  - gdy wygenerujesz prolog_01…prolog_23.png według promptów (docs/PROMPTY-OBRAZKI-PROLOG-2026-10-21.md) i wrzucisz je do assets/prolog/, okna same wezmą Twoje obrazki, bez przebudowy moda.
- **Teraz:** sprawdzam w kodzie gry, jak wczytać obrazek z pliku i jak otworzyć okno ROT z naszego moda. Potem piszę teksty i kod, recenzja, test w grze i wgranie.

### 2. Sceny zmieniające świat (PO punkcie 1)
To nie są same okna, tylko rzeczy dziejące się na mapie:
- **Orszak króla Roberta** jedzie Królewskim Traktem do Winterfell (Robert, Cersei, dzieci, Jaime, Tyrion, Ogar), a potem wraca na południe już z Nedem.
- **Turniej Ręki** w Królewskiej Przystani: prawdziwy turniej GrandTourney, możesz wystartować.
- **Tyrion jeńcem Catelyn** (dni 110–132), w drodze do Doliny.
- **Klany górskie** napadają na Górskim Trakcie w Dolinie (bandy na mapie).
- **Ludzie Góry** palą wsie w Dorzeczu.
- **Oddział Berica** wyrusza na nich z Królewskiej Przystani.

### 3. Lektor po angielsku (W TOKU — Twój nowy klucz działa, plan Starter)
- **Przygotowanie:** dodałem do Twojego konta 8 profesjonalnych głosów narratorów z biblioteki ElevenLabs (na darmowym planie było to zablokowane). Nagrałem po dwie wersje każdego głosu: nowy model v3 z bardziej naturalną intonacją i starszy v2, równiejszy. Zmierzyłem tempo czytania i pauzy.
- **Mój wybór:**

  | Miejsce | Głos | Dlaczego |
  |---|---|---|
  | 1 | David Deep Storyteller (v3) | Głęboki, dojrzały, brytyjski. Najspokojniejsze tempo (około 120 słów na minutę z pauzami). Krótsze pauzy między zdaniami i dłuższe między akapitami, tak jak czyta prawdziwy lektor. |
  | 2 | Old Wizard (v3) | Podobny klimat, trochę szybszy. |
  | 3 | Older Joe (v3) | Spokojny, autorytatywny, najdłuższe pauzy na akapitach. |

  Starszy model v2 robi pauzy krótsze i bardziej równe, więc brzmi bardziej „czytająco”. Dlatego wybrałem v3.
- **Co dalej:** wybierasz głos, a ja nagrywam:
  - 35 okien ROT (około 10 tys. znaków);
  - 23 okna prologu (około 15 tys. znaków).

  Zmieszczę się w planie (zostało około 32 tys. znaków). Potem kod: lektor zaczyna czytać, gdy okno się otwiera, i milknie, gdy je zamykasz. Będzie wyłącznik w MCM, a głośność idzie suwakiem dźwięku gry.
- **Uwaga:** tekst w nagraniu ma poprawione literówki ROT („Danaerys” → „Daenerys”), więc może się minimalnie różnić od tego, co widać na ekranie.

### 4. Strojenie gospodarki (W TOKU — diagnoza na logach testów 100 i 101)
Sześć osobnych analiz, każda szuka przyczyny w kodzie i liczbach:
- **Kasy miast i sakiewki ludzi:** pieniędzy przybywa za szybko (około 35–47 tys. d na dobę ponad normę). Podejrzenie: pożyczki z długów miast albo nowe paczki (markietani, nagrody z tablicy) dosypują złoto. Do tego sakiewki karawan są o 33% za grube, a rodów na minusie jest za dużo.
- **Bandy:** zjadają tylko 57% tego, czego potrzebują (powinno być co najmniej 85%).
- **Drewno:** warsztaty częściej stają z braku drewna (222 razy na dobę przy normie 177), a kopalnie bez węgla.
- **Jedzenie:**
  - chleb jest trochę za drogi;
  - w Qarth, Braavos i Volantis jedzenia na półkach starcza na 2–6 dni zamiast 7;
  - dziesięcina wsi wynosi zero;
  - nadwyżki wsi za wolno idą na targ.
- **Zaległości danin** (w biegu na starym zapisie) i drogi Valyrii, które dają 23% zamiast 25%.
- **Błąd:** w teście 101 Armoury raz wyrzucił błąd przy wioskach bocznych, który przechwycił CrashScribe. Gra się nie wywróciła, ale szukam przyczyny.

Po diagnozie: poprawki po kolei, każda pod wyłącznikiem w MCM, a potem test.

### 5. Optymalizacja (PO gospodarce)
- **Cel:** szybsze przejście doby, przy którym liczby gry się nie zmieniają.
- **Najdroższe części doby:**

  | Część | Czas na dobę |
  |---|---|
  | Dochody rodów | około 90 ms |
  | Handel podaży i popytu | około 67 ms |
  | Skarbiec królestw | około 65 ms |
  | Markietani | około 40 ms |

  Przebudowa z dowodem, że wyniki są identyczne. Robię to po gospodarce, bo obie prace ruszają te same miejsca.

### 6. „Kronika Westeros” (W TOKU)
- **Co przygotowuję:** listę 25–30 pomysłów na nowe wydarzenia między głównymi wydarzeniami ROT, według książek (np. Harrenhal i Arya, Bractwo bez Chorągwi, Theon zdobywa Winterfell, Jaime i Brienne, Stannis na Murze) i z życia świata (głód po oblężeniu, zaraza, pierwszy śnieg, wielki turniej).
- **Każdy pomysł ma:** kiedy się pokazuje, co się dzieje, skutek w grze i ile pracy wymaga.
- **Ty zaznaczasz,** które robimy.

### 7. Drobne (W TOKU — kod i dwie recenzje)
- **Nocny rabunek wioski** liczy się jako praca, nie jako odpoczynek (nie zaprotestowałeś).
- **Markietani: odzież i praczki.** Możesz odziać swoich ludzi z towaru markietanów. Praczki zmniejszają zużycie odzieży, o ile mod ma zapisane zużycie, które da się uczciwie zmniejszyć. Jeśli nie ma, napiszę dlaczego.
- **Zapis tablicy ogłoszeń** ma być mniejszy: dziś 18,5 KB, a ma zejść poniżej 12 KB.
- **Licznik błędów plotek** w sprawdzaniu testów.
- **Szkolenie lordów tylko w obozie** jako ustawienie domyślne w kodzie (u Ciebie w MCM już jest).

### Kiedy to trafi do gry
Wszystkie gałęzie scalę w jedną wersję i puszczę jeden pełny zestaw testów (prolog 230 dób, bitwy, nowa kampania 180 dób). Wgram ją sam po udanym teście, ale nie wtedy, gdy grasz. Możliwe, że wyjdą z tego dwie wersje: najpierw prolog, drobne i lektor, a potem gospodarka i optymalizacja.
