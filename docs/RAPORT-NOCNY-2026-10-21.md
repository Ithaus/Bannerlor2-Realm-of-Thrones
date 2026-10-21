# Raport nocny 20/21.10 dla Jeffa

Godziny podaję według zegara komputera. Szczegóły techniczne są w STAN-PRAC ### 102-104, a przekazanie dla drugiego konta w PRZEKAZANIE-2026-10-21.md.

## W skrócie

- **WGRANIE 96** jest w grze od 20.10 19:30. Przeszło trzy testy: bitwy, nową kampanię na 180 dób i 160 dób na starym zapisie. Wszystkie bez wywrotki i z 0 błędów w logu Armoury.
- **WGRANIE 97** jest w grze od 20.10 23:20 (wioski boczne i ich obrońcy, obóz, szkolenie, posiłki, surowce do łuków). Przeszło dwa pełne zestawy testów (97 i 97b z poprawkami po recenzji scalenia), wszystkie bez błędów. W Twoich ustawieniach MCM zmieniłem obóz na 21:00-3:00 (była 0:00-6:00); kopia starego pliku jest obok.
- **Twoje zapisy są nietknięte.** Testy sprawdzają to za każdym razem (104 pliki, sumy kontrolne bez zmian).
- **Limit tygodniowy:** około 92% przy pisaniu. Rzeczy, których nie zdążyłem, są rozpisane do przekazania na końcu raportu.

## Co weszło w 96 (już w grze)

- **Kuźnia nie wywraca gry** (crash z 20.10 to był błąd w oknie kuźni). Poprawione już w 95a.
- **Legendarne i mistrzowskie przedmioty są rzadkie.** Każdy rodzaj przedmiotu ma swój limit: legendarnych od 1 do 6 (np. 1-6 legendarnych hełmów), mistrzowskich od 1 do 60. Limit jest losowany raz na kampanię. Nadmiar na półkach spada do zwykłej jakości. Po 180 dniach testu było na świecie 16 legendarnych i 83 mistrzowskie.
- **Wzory łuków i kusz:**
  - Tier VI na start był błędem i zapis jest naprawiony.
  - Ravens Teeth, Goldenheart i łuk olbrzymów to zwykłe łuki do wykucia, jeśli znasz ich wzór.
  - Czardrzewo i smocza kość są w 96 jeszcze zablokowane, a w 97 można je wykuć z surowca.
- **Czas kucia to jeden pasek.** Najpierw wytop (nie da się go przerwać), potem kucie, które można przerwać. Wychodząc, zabierasz to, co już jest wykute, a reszta czeka w kuźni. Nocą kowale śpią.
- **Przepisy z wagi surowca, jak drewno i węgiel.** Każdy surowiec (len, skóra, żelazo, stal, róg) zużywa się w kg według tego, ile go wchodzi w przedmiot, plus odpad. Napoczęta sztuka starcza na kolejne. Strzały nie biorą już 14 sztab, a łuk 8 lnu.
- **Przetop oddaje mniej, niż przepis wziął** (zgar, żużel). Nie da się na nim zarobić.
- **Płatności mówią, za co płacisz.** „You paid 2” to była opłata za kuźnię na dobę.
- **Menu wioski i zamku** są uporządkowane jak menu miasta.
- **Turnieje:** okup płaci tylko ten, kogo sam powaliłeś, w pełnej wartości i tylko z tego, co ma (inaczej oddaje zbroję). Naprawiłem też podwójną wypłatę puli.
- **Drogi Valyrii (+25%) i trakty karawan w Essos.**
- **Długi miast:** rada miasta pożycza u mieszczan i w Banku, a w potrzebie ściąga podatek nadzwyczajny.
- **Bandyci noszą jedzenie i złoto**, jedzą codziennie, a gdy głodują, część z nich odchodzi.
- **Sakiewki ludzi według potrzeby, koszyk ludu, płótno i sukno** w planie wsi.

## Co weszło w 98 (już w grze, od 21.10 00:55)

- **Plotki w karczmie i zajeździe za kufel.**
  - Słuchanie: plotek słuchasz dopiero po kupieniu kufla piwa albo ale, około 1 d. Pieniądze idą do karczmarza.
  - Ile plotek z kufla, według Uroku: przy 20 jedna, przy 80 dwie, przy 150 trzy, przy 200 cztery, przy 250 wszystkie.
  - Co nowego słychać: gdzie trwa turniej i który lord szuka męża dla córki albo żony dla syna. Plotki są tylko prawdziwe.
  - Powtórki: plotka, którą już słyszałeś, nie wraca.
- **Pijaństwo i kac.**
  - Kiedy się upijasz: po 3-5 kuflach, zależnie od Wytrzymałości i Atletyki.
  - Pijany: tracisz 15-25% szybkości na mapie i musisz dospać.
  - Kac: następnego dnia do 18:00 tracisz 10% szybkości i 10% morale. Nocleg w łóżku nie daje wtedy premii.
- **Testy:** przeszło wszystkie trzy, bez błędów, a progi wypadły lepiej niż w 97.

## Co weszło w 97 (już w grze)

1. **Wioski boczne jak wioski główne** (bez bohaterów).
   - Co można: wjechać, handlować ich towarem, inwestować w prace, przespać się u wieśniaków, rozbić obóz obok, rabować każdą osobno albo wziąć okup.
   - Twój rabunek wioski głównej pali już tylko ją. Wioski boczne rabujesz osobno, każdą z jej menu.
2. **Obrońcy wioski bocznej to jej mieszkańcy**, tak jak chciałeś.
   - Ilu: 1 na 10 mieszkańców, najwyżej 60, czyli chłopi, włócznicy i łucznicy.
   - Straty: zabici ubywają z tej wioski i przez około 60 dni brakuje jej rąk do pracy.
   - Poddanie: przy przewadze 4:1 wioska poddaje się bez walki.
   - Posiłki: do tej bitwy nie dochodzą posiłki Dynamic Reinforcements.
   - Przegrana: jeśli przegrasz, Twój łup trafia do magazynu wsi, a Twoi jeńcy są uwalniani.
3. **Obóz.**
   - Pora obozu: świat obozuje od 21:00 do 3:00.
   - Spacer: opcja **„Walk through the camp”**. Namioty ROT stoją według liczby ludzi całej armii, są ogniska, a konie są uwiązane. **Ze sceny wychodzisz tylko klawiszem Tab.** Esc otwiera menu gry, a tam „Exit to Main Menu” porzuca kampanię bez zapisu.
   - Wóz taborowy: 500 kg ładunku, kolumna idzie 10% wolniej. Buduje go stolarz w menu „Go to the smithy” → „Visit the cartwright and saddler”.
   - Siodło transportowe: dodaje 50 kg.
4. **Szkolenie wojska tylko w obozie.**
   - W marszu wojsko nie ćwiczy.
   - W obozie ćwiczy z opcji **„Train the men”**, najwyżej 8 godzin dziennie, za dnia.
   - Przykład: 60 ludzi drugiego poziomu przy Przywództwie 150 dostaje za 8 godzin 27,8 doświadczenia na człowieka (dawniej dzień postoju dawał 18,5, a marsz 11,1).
   - Mistrz musztry: do Twojego Przywództwa dochodzi połowa Przywództwa najlepszego towarzysza.
   - Oblężenie i cudza armia: ludzie ćwiczą sami w godzinach postoju za dnia.
   - Perki: 12 perków treningu ma nowe działanie i poprawione opisy.
5. **Posiłki Dynamic Reinforcements według odległości marszu.**
   - Czas dojścia: 30 s plus 2 minuty na każdą godzinę marszu.
   - Limit: dalej niż 2 godziny marszu nikt nie przychodzi („is too far to reach us in time”).
   - Noc: piechota idzie wolniej.
6. **Czardrzewo i smocza kość.**
   - Czardrzewo pochodzi z 6 osad Wolnych Ludzi za Murem, około 4000 d.
   - Smocza kość pochodzi z 5 osad Valyrii i Essos, około 14 000 d.
   - Dopływ jest mały, mniej więcej 1 sztuka na kilka-kilkanaście dni na region.
   - Z 1 sztuką surowca wykujesz łuk z czardrzewa albo ze smoczej kości.
   - Zadania i zdarzenia gry nie dają tych surowców z niczego.

## Decyzje, które są Twoje

1. **Lordowie komputera i szkolenie.** Na razie ćwiczą po staremu, bo przy Twojej zasadzie mieliby około 32% mniej doświadczenia (prawie ciągle maszerują). Jeśli chcesz jednej zasady dla wszystkich, w MCM jest klucz **Drill Camp Only Ai**.
2. **Pytania o fabułę, lektora, wersję polską i filmy:** wszystko jest w docs/ROZPOZNANIE-FABULA-GLOS-JEZYK-2026-10-20.md. Najkrócej:
   - **Lektor wydarzeń:** łatwy. Kosztuje 6 USD za jeden miesiąc ElevenLabs. Konto zakładasz Ty.
   - **Polski tekst:** około 8-10 nocy pracy.
   - **Prolog od wyjazdu Roberta:** wymaga nowej kampanii, około 2-6 nocy.
   - **Nowe wydarzenia:** szkielet 1-2 dni.
   - **Film na cały ekran przed wydarzeniem:** pewny. Do przeróbki filmów potrzebny jest program ffmpeg, a jego pobranie wymaga Twojej zgody.
3. **Rabunek wioski bocznej w nocy:** godzina rabunku liczy się jak postój, czyli odpoczynek, tak samo jak przy rabunku gry. Czy ma się liczyć jako praca, jak ćwiczenia?

## Co sprawdzić w grze (testy tego nie klikają)

- Spacer po obozie: pierwszy raz otworzysz go Ty.
- Wjazd do wioski bocznej, handel, nocleg i bitwa z mieszkańcami.
- Opcja „Train the men” i komunikat dnia „Drill today”.
- Bitwa blisko i daleko od swoich: kiedy dochodzą posiłki.
- Kucie łuku z czardrzewa: surowiec trzeba najpierw kupić we wsi za Murem.

## Do poprawy później (z testów)

Testy tego nie psują, to rzeczy do strojenia:
- W kasach miast i w sakiewkach mieszczan przybywa za dużo pieniędzy (próg 30 tys. na dobę, jest około 35-47 tys.). Podejrzewam pożyczki z długów miast.
- Zaległości firm w biegu na starym zapisie.
- Bandy zjadają 56-66% tego, czego potrzebują.
- Warsztaty częściej stają bez drewna.
- Chleb jest trochę za drogi.

## Czego nie zdążyłem (do przekazania)

Kolejność według Twojej listy:
1. Tablica ogłoszeń: rozpoznanie i projekt są gotowe (docs/PROJEKT-TABLICA-OGLOSZEN-2026-10-21.md). Gra ma 43 rodzaje zadań. Kapitana straży nie ma ani w grze, ani w ROT, ani w BK, więc dodamy go sami: będzie wywieszał listy gończe na bandy z okolicy, a nagrodę zapłaci kasa miasta (za bandę 20 ludzi ok. 375 d). Kod robi drugie konto.
3. Obóz etap 2: markietani z usługami, rozrywką i sklepem w dużych obozach.
4. Optymalizacja gry (na koniec).
5. Strojenie gospodarki miast, opisane wyżej.
