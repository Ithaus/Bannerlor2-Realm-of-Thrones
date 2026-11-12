# Wsie poboczne i karczmy: rozpoznanie i projekt

Odpowiedz krotko: wiekszosc rzeczy dla wsi pobocznych juz jest w kodzie albo da sie je tanio dopisac. Wejscie 3D do zajazdu da sie zrobic, a dziedziniec tylko jako drugi etap, bo gotowej sceny dziedzinca nie ma. Niczego nie zmienialem w repo ani w grze. Zaden z ponizszych pomyslow nie byl sprawdzony w grze.

Sprawdzilem w plikach siedem twierdzen z raportow i wszystkie sie zgadzaja:
- **Lupienie tylko z okna wsi pobocznej:** `n367c/Armoury/src/DistrictRaid.cs:82`, komentarz "pladrowanie wioski trzeba robic kazda osobno" (Jeff 20.10).
- **Menu wsi pobocznej:** `SideVillageVisit.cs:349-376` ma nocleg, oboz, handel, "Invest in its works", "Take a hostile action", "Recruit men of the village" i wyjazd.
- **Wlasna misja z mapy:** `CampWalk.cs:173` otwiera ja przez `MissionState.OpenNew("Camp", ...)`, bez osady.
- **Zajazdy ROT:** `ROT-Map/ModuleData/settlements.xml:241` (The Inn of the Kneeling Man) i `:515` (Crossroads Inn). Obie to zwykle wsie kultury `river`.
- **Levy Blackwoodow:** `ROTHouseTroopsBehavior.cs:223` przypisuje RaventreeHall do `blackwood_levy` u kasztelana `blackwoodcast`.
- **Karczmarz z gry poza miastem:** `TavernEmployees.cs:260,265,299` czyta `Settlement.CurrentSettlement.OwnerClan` bez sprawdzenia null. Poza osada skonczyloby sie to wywrotka (NRE).
- **Pobor z wsi pobocznej:** `SideVillageVisit.Levy.cs:43-44` (regula P6) przepuszcza tylko chlopow: tier powyzej 1 i konni sa wykluczeni.

## 1. Wsie poboczne

| Punkt Jeffa | Stan | Jak to bedzie wygladac w grze |
|---|---|---|
| Lupienie tylko z okna wsi pobocznej | **Juz jest** od WGRANIA 97 | Rabunek wsi glownej pali tylko jej zagrody. Wioske poboczna rabuje sie z jej menu: "Take a hostile action", potem "Raid the village". Bronia jej mieszkancy. Mozna dopisac w menu rabunku wsi glownej zdanie po angielsku, ze wioski poboczne rabuje sie osobno. |
| Spacer jak po wsi glownej | Nie ma, **da sie zrobic** | Opcja "Walk through the village" w menu otwiera wlasna misje jak spacer po obozie. Scena wsi kultury okregu jest wybierana zawsze tak samo dla danej wioski. Chodza po niej chlopi tej kultury, wyjscie przez Tab. Drogi przez spotkanie z wsia gry nie wybieram: odpala zdarzenie wejscia do osady, na ktorym wisi wiele latek, i ryzyko regresji jest wysokie. **Wada:** wioska wyglada jak inna wies tej samej kultury, bo wlasnych scen nie ma. |
| Inwestycje z ikona glownego surowca i lista x, y, z | **Czesciowo** | Okno prac juz jest. Dochodzi linia "Makes: grain (main), flax, ..." w menu i dymku oraz ikona glownego towaru. Glowny towar to ten, ktory daje najwiecej pieniedzy, ta sama regula co ikona okregu (`DistrictIcon`). Ikona to gotowy widzet gry z napisow osad. |
| Nazwy pod klawiszem | Nie ma, **tanie** | Przytrzymany lewy Alt pokazuje nazwy i ikony produktu ok. 40-60 najblizszych widocznych wiosek. Uzyjemy gotowej warstwy, ktora juz rysuje napisy nad spalonymi wioskami. Alt pasuje, bo juz rozszerza dymek. Gry nie ma klawisza "pokaz nazwy", a N to encyklopedia. |
| Rekrutacja zwyklych i rodowych (Blackwood, Stark itd.) | **Czesciowo**: dzis sami chlopi | Wioska, ktorej pan nalezy do rodu z tabeli ROT (35 siedzib, np. Blackwood, Bracken, Stark, Tully, Mallister), da tez levy tego rodu. Warunek: relacja z glowa rodu co najmniej 30 albo wlasny rod. Ludzie ida z tej samej puli wioski, wiec ekonomia zostaje zamknieta. Levy beda drozsze i stanowia np. 1 na 5 rekrutow. Wymaga wyjatku od reguly P6. **Niepewne:** dla rodow spoza tabeli ROT (ROT ma 134 typy `*_levy`/`*_footman`) przypisanie trzeba zrobic recznie. |
| To samo okno co we wsi glownej | **Da sie, sredni koszt** | Ekran poboru gry z dwoma wierszami: "Men of X" z portretem soltysa i "House Blackwood" z portretem glowy rodu. Ryzyko: okno gry czyta osade, ktorej z mapy nie ma (wywrotka), a zatwierdzenie ma juz 3 latki (BK i dwie nasze). Potrzebna jest nasza latka przed nimi oraz licznik potkniec. |

**Wydajnosc.** Na mapie dochodza tylko napisy, i to tylko przy wcisnietym Alcie. Bez Altu jest jedno sprawdzenie flagi na klatke. Misja, okno i lista towarow licza sie dopiero po kliknieciu. Nie dodajemy zadnego licznika czasu na kazda z 1957 wiosek.

## 2. Karczmy

**Czy sa gotowe modele zajazdu z dziedzincem i stajnia? NIE.**
- Gra ma 7 scen karczm, ROT ma 5 (`ROT_riverrun/kingslanding/harrenhal/dragonstone/castle_black_tavern`). Wszystkie to same wnetrza.
- `ROT_castle_black_tavern` to w praktyce sala z punktami obroncow, nie karczma.
- Zajazdy ROT na mapie (Crossroads Inn, Kneeling Man) maja zwykle sceny wsi i nie maja karczmy w srodku.

**Czy da sie zrobic dziedziniec? TAK, ale jako drugi etap.**
- Dziedziniec powstalby na scenie terenu z miejsca zajazdu (jak nasz oboz). Postawimy na nim prefaby ROT `Tavern_Village`, `Stable`, koryta i wozy z sianem, a konie druzyny uwiazemy jak w obozie.
- Drzwi prowadza do drugiej misji, czyli wnetrza. To oznacza dwa ekrany ladowania i duzo kodu ukladu.

**Co wybieramy.** Najpierw wejscie od razu do srodka, co Jeff sam dopuscil. Dziedziniec pozniej.

| Punkt Jeffa | Odpowiedz |
|---|---|
| Wejscie do srodka zamiast okna | **Miasta:** gra juz ma karczme 3D ("Visit the tavern"), dokladamy tylko rozmowy. **Zajazdy przy szlakach (135):** wlasna misja wnetrza ze scena regionu. Sa w niej barman, kelnerka, muzycy i bywalcy na gotowych punktach sceny (np. `empire_house_c_tavern_a` ma 9 miejsc dla bywalcow i 64 krzesla). Nocleg zalatwia sie u barmana, a sen nastepuje po wyjsciu z misji. |
| Plotka u barmana za kufel | **TAK.** W miescie dopisujemy linie gracza do rozmowy z karczmarzem gry, np. "Pour me an ale. What's the news?". Korzysta z obecnych kufli i plotek, wiec nic nie liczy sie podwojnie. W zajezdzie barman ma wlasne linie, nie karczmarza z gry, bo ten wywraca gre poza osada. |
| Dosiadanie sie do plotkujacych | **Raczej TAK.** Gra pozwala siasc na krzesle i obsluguje rozmowe z kilkoma osobami naraz. **Niepewne**, jak wyglada kamera przy rozmowie na siedzaco: trzeba sprawdzic w grze. |
| Gwar i muzyka | Odglos tlumu jest juz w czesci scen, do pozostalych da sie go dostawic. **Muzyka:** gra ma utwory tylko dla 6 swoich kultur, wiec w Dorzeczu moze dzis grac muzyka Aserai. Poprawka: region na kulture z gry, np. Polnoc -> battania/sturgia, Dorne -> aserai. |
| Karczmy wedlug regionu | **TAK.** Polnoc: wnetrza battania i sturgia. Dorzecze: Riverrun, Harrenhal oraz wnetrza empire i vlandia. Zachod i Reach: vlandia. Dorne: aserai. Zelazne Wyspy: nord. Essos: khuzait/aserai. Zajazd bierze karczme najblizszego miasta tej samej kultury. Trzy sceny ROT (Riverrun, Harrenhal, Kings Landing) nie maja punktu startu gracza, wiec trzeba zapasowego punktu. **Niepewne.** |

## 3. Tresc i glosy

Plotki z prawdziwego stanu swiata, kufel, lektor z nagraniami i glosy regionow z bitwy juz mamy. Brakuje trzech warstw tekstu:

1. **Barman.** Ok. 600 krotkich ram wypowiedzi, czyli poczatek i koniec zdania wedlug regionu, np. "Lean closer..." oraz "...but you never heard it from me." W srodek wchodzi prawdziwa plotka z obecnej puli.
2. **Stolik.** Ok. 500 scenek po 3-6 kwestii (ok. 2000 zdan). Na koniec gracz wybiera: "Ask more", "Buy them a round" albo "Leave". Okolo 60% scenek to ogolne tematy regionu, 40% to tematy biezace: wojna, oblezenie, glod, zima, smierc lorda, slub, wydarzenia fabuly ROT.
3. **Gwar.** Ok. 1150 krotkich okrzykow z roznych stolow plus petle odglosow tla.

**Razem ok. 3800 zdan po angielsku**, w pliku `arm_tavern_lines.tsv` z tematem, regionem i warunkiem.
- Tematy wybiera "migawka swiata" liczona raz na dobe, wiec na mapie nie ma zadnej pracy.
- Bez cytatow z GRRM i serialu.

**Glosy w ElevenLabs.**
- Nagrywamy z gory zdania bez imion i liczb, czyli ok. 85-90% puli. Kazdy region dostaje swoj glos z mapy glosow bitwy.
- Zdania z `{LORD}` albo `{TOWN}` zostaja tekstem albo ida przez lektora na zywo, ktory ma dzienny limit znakow.
- Nagranie gra tylko wtedy, gdy zgadza sie suma kontrolna tekstu (md5), jak w Kronice. Poprawione zdanie nie odpali starego glosu.
- **Koszt:** ok. 210 tys. znakow, wiec nagrywamy etapami. Najpierw ok. 25 tys. znakow jednego regionu i test w grze.
- **Zanim zaczniemy nagrywac:** skrypt nagran bitwy `nagraj359.py` lezy tylko w scratchpadzie i trzeba go przeniesc do repo.

## 4. Dodatkowe pomysly do wsi
1. **Glod:** opcja "Give grain from your stores" przy glodzie. Zboze z taboru idzie do wsi, relacja rosnie, mniej uciekinierow.
2. **Soltys:** opcja "Ask about the roads" daje plotki o bandach i kryjowkach w okregu.
3. **Przewodnik:** wynajety mysliwy przyspiesza jazde po drogach bocznych okregu, zold idzie do kiesy wsi.
4. **Odbudowa:** opcja "Help rebuild" po rabunku. Drewno i zloto skracaja odbudowe spalonej wioski.
5. **Jarmark po zniwach:** tansze plody tej wioski przez 3 dni i morale druzyny przy noclegu.
6. **Kowal wiejski:** naprawa zuzycia sprzetu taniej niz w miescie, ale wolniej.
7. **Pastwisko:** konie odpoczywaja szybciej, oplata za trawe idzie do kiesy wsi.
8. **Straz wioski:** gracz zostawia kilku ludzi na N dni, wiec wioska ma wiecej obroncow.
9. **Kapliczka, sept albo godswood** wedlug wiary okregu (BK): mala premia morale i pobozosci.

## 5. Plan w paczkach (jedna zmiana na wgranie)

| # | Co | Ryzyko | Praca | Test |
|---|---|---|---|---|
| 0 | Powiedziec Jeffowi, ze lupienie juz jest | brak | 0 | brak |
| 1 | Nazwy wiosek pod lewym Altem, z ikona produktu | niskie (sam widok) | ok. 0.5 dnia | mapa: Alt pokazuje napisy, bez Altu FPS bez zmian |
| 2 | Linia "Makes: x (main), y, z" w menu i dymku | niskie | kilka godzin | log i menu 3 wiosek roznych kultur |
| 3 | Barman w karczmie miasta: kufel i plotka w rozmowie | niskie | ok. 0.5 dnia | Winterfell i Lannisport: zloto, kufle, "slyszane" bez podwojenia |
| 4 | Muzyka karczmy wedlug regionu | niskie | kilka godzin | karczma w Dorzeczu i na Polnocy |
| 5 | Levy rodow w poborze wsi pobocznej (wyjatek od P6, tabela ROT) | srednie (ekonomia puli) | ok. 1 dzien | wioska Blackwoodow: `blackwood_levy`, pula maleje |
| 6 | Okno rekrutacji gry dla wsi pobocznej | srednie/wysokie (3 latki na tym samym oknie) | 1-2 dni | pobor z mapy, notable wsi glownej bez zmian, brak NRE |
| 7 | Spacer 3D po wsi pobocznej | srednie (punkty postaci w scenach) | 1-2 dni | autotest wejscia i wyjscia, 3 kultury |
| 8 | Wnetrze zajazdu 3D (barman, kelnerka, muzycy, nocleg) | srednie | 2-3 dni | zajazd na Polnocy i w Dorzeczu, Tab, nocleg |
| 9 | Dosiadanie sie do stolu | srednie (kamera) | ok. 1 dzien | rozmowa na siedzaco |
| 10 | Plik zdan (etap 1, jeden region) i nagrania | niskie w kodzie, koszt kredytow | kilka dni pisania | glos przy NPC w karczmie, zgodnosc md5 |
| 11 | Dziedziniec zajazdu ze stajnia | srednie/wysokie | 3+ dni | dwa ladowania, konie, przejscie do srodka |

Przed paczkami 6-9 trzeba sprawdzic:
- czy dzwiek 3D dziala w scenie karczmy poza bitwa,
- czy rozmowa z postacia bez bohatera dziala w misji bez osady,
- czy w tekscie menu wyswietli sie obrazek (`<img>`).

## 6. Pytania do Jeffa
1. Czy nazwy wiosek maja byc **pod przytrzymanym Altem**, czy pod przelacznikiem (wcisnij raz, wcisnij drugi raz)? Jesli przelacznik, to jaki klawisz?
2. Czy levy rodow maja byc ograniczone do **rodow z tabeli ROT** (35 siedzib)? Czy mam tez recznie przypisac pozostale rody?
3. ROT dzis rekrutuje levy rodow u kasztelana z niczego: zloto znika, ludzi nigdzie nie ubywa. Czy to latac?
4. Zajazd: czy z klikniecia **od razu misja 3D**, czy zostawiamy krotkie menu (nocleg, "Ride on") z opcja "Enter the inn"?
5. Czy dodac **kobiece glosy** (barmanki, kobiety przy stolach)? Na koncie jest dzis 33 glosy przy limicie 30. Czy na razie zostaja same napisy?
6. Budzet ElevenLabs: czy zgoda na etap 1, ok. 25 tys. znakow?

**Rekomendacja:** zaczac od paczek 1-4 (napisy pod Altem, linia produktow, barman z plotka i muzyka regionu w karczmie miasta, wszystko tanie i bezpieczne), potem levy rodow, a wnetrze zajazdu 3D przed dziedzincem.

Pliki: dekompilacje karczmy sa w `C:\Users\GAME\AppData\Local\Temp\claude\bfd708ab6e1d6456\264cfac3-8338-4c40-91b8-6c1304da3584\scratchpad\tav\`, a skrypt scen karczm i wsi wedlug kultury to `C:\Users\GAME\AppData\Local\Temp\claude\bfd708ab6e1d6456\264cfac3-8338-4c40-91b8-6c1304da3584\scratchpad\tav_scan.py`.