# AUDYT EKONOMII - raport koncowy (11.11, tylko odczyt)

Podstawa: szesc zweryfikowanych soczewek (obieg, place i ceny, rynek, produkcja, karawany i zbojcy, wladza i wojna), kod worktree n367c, Armoury.json Jeffa, logi z dob 109726-109815.
Zastrzezenia:
- Caly pomiar to ok. 88 dob jednej, wieloletniej zimy (doba zimy 637 z 1795) i wojny. Lata, zniw i pokoju w logach nie ma.
- Liczby historyczne podaje z pamieci, nie sprawdzalem ich w zrodlach. Traktuj je jako rzad wielkosci.
- Nic nie zostalo zmienione w kodzie ani w grze.

## 1. WERDYKT

1. Tak, swiat jest prawdziwym obiegiem zamknietym: w 88 dob pieniadz swiata zmienil sie z 224.37 na 225.21 mln, a caly przyrost to mennica; kontrole pokazuja rozjazd 0.
2. Jest tez dynamiczny tam, gdzie chodzi o zywnosc: oblezenie i spustoszenie widac w cenach miasta (Dragonstone zboze 1.5 -> 10 w miesiac, Duskendale wraca do normy w dwa miesiace).
3. Podatki miast same sie koryguja: gdy miasta zaczely chudnac, naleznosci panow spadly o 24% w 43 doby i kasy miast przestaly tracic.
4. Jak w sredniowieczu dzialaja: wies zyjaca w ok. 80% z wlasnego pola, renta pana ok. 10% plonu, okupy za jencow, mennica na kruszcu, myta rzedu 2-5%, rozboj rosnacy z wojna, dezercja jako zrodlo band, udzial ludnosci miast ok. 15%.
5. Uproszczone, ale rozsadne: zold eskorty karawan, jedno ryzyko drogi dla wszystkich tras, stale stawki zoldu, zboze sterowane iloscia (dowozem), a nie cena.
6. Glowna slabosc: swiat reaguje cena, ale slabo reaguje produkcja. Wioski prawie nie zmieniaja zajec, 172 z 291 warsztatow stoi, liczba karawan nie zalezy od zysku.
7. Najwiekszy korek to brak towaru, nie brak pieniedzy: panowie chca wydac ok. 112 tys. zl dziennie na sprzet i dwor, a 94% zostaje, bo nie ma czego kupic; wsie chca kupic za 431 tys., kupuja za 29 tys.
8. Jednoczesnie ponad tysiac gotowych sztuk broni czeka w warsztatach, bo kasa miasta nie ma wolnych pieniedzy na skup - to jalowa petla, ktora sama sie trzyma.
9. Pieniadz stoi na gorze: korony i glowy rodow trzymaja 59% monety przy dlugach bliskich zera. Historycznie krolowie byli w dlugach, a nie na skrzyniach.
10. Lordowie AI nie bogaca sie kosztem swiata - ich majatek jest plaski. Jedynym, kto szybko rosnie, jest gracz (+4.08 mln w 62 doby, glownie okupy).
11. Wies jest ostatnim wierzycielem: miasta sa winne wsiom 7.4 mln za sprzedane zboze, bo kasa najpierw placi panu.
12. Niespojne sa skale: wojsko liczone jest 1:1, lud na "glowach" BK, towar w przeliczniku x37. Zold roczny to 87% calej monety swiata - wojsko jest kilka razy ciezsze niz w sredniowieczu.
13. Niehistoryczne w kierunku: bandy maleja w pokoju i dobrobycie, a po prawdziwych wojnach (kompanie po 1360) bylo odwrotnie; ceny jedzenia poza zbozem sa oderwane od zboza (chleb drozszy od wsadu ok. 7.5 raza).
14. Martwe dzis: rok urodzaju i jarmarki (wylaczone przez zime), pobor z pol (prawie nigdzie nie dochodzi do progu), pokoj z biedy skarbca (liczony, ale wojny fabularne ROT go blokuja).
15. Hamulce podatkowe gubia pamiec po wczytaniu zapisu i nie obejmuja 50 z 97 miast - to najwazniejsza dziura do zalatania.
16. Jedno miejsce wymaga sprawdzenia pod katem "nic z niczego": gra dosypuje notablom zloto (linia "+116 662 z niczego" jednej doby); nie ustalono, ile z tego to prawdziwa dosypka.
17. Podsumowanie: fundament (zamkniety obieg, ceny zywnosci, podatki z hamulcem) jest solidny i blizszy sredniowieczu niz gra bazowa; brakuje odpowiedzi podazy na cene i ujscia dla pieniedzy z gory.

## 2. SCHEMAT OBIEGU I BILANS

### Kto komu placi
1. Wies -> miasto: zboze i surowce wozami (utarg 466 tys. zl/d); miasto placi wsi gotowka albo zapisem na rachunek.
2. Lud miast -> kupcy (kasa miasta): jedzenie i wyroby (316-349 tys. zl/d).
3. Kasa miasta -> pan i korona: stala naleznosc z miasta ("firma") i nadwyzka ponad zapas kupcow (w dobie raty 595 tys.: panom 353 tys., koronie 242 tys.).
4. Pan -> wojsko: zold 537 tys. zl/d; zolnierz wydaje w miastach i wsiach.
5. Pan -> rzemieslnicy: sprzet i dwor (chce ok. 112 tys./d, wydaje ok. 7 tys.).
6. Korona -> rody: renty (132 tys. z 323 tys. naleznych; reszta wstrzymana bogatym), zwrot zoldu, zold krolewski w wojnie (KL: 64 tys./d).
7. Rod z malym wojskiem -> korona -> rody walczace: tarczowe (tylko gdy skarbiec nie wystarcza).
8. Warsztat -> rzemieslnik (place 23 tys./d) i wlasciciel (21 tys./d); kupiec bierze ok. 25% utargu.
9. Kupiec-notabl -> karawana: kapital i doplaty (56 tys./d); karawana -> notabl: zysk (ok. 76 tys./d); notable -> kasa miasta 134 tys./d nadwyzek.
10. Karawana -> eskorta: zold 55 tys./d; karawana i woz -> pan i korona: myto 26 tys./d.
11. Lud i wies -> pan: danina od oszczednosci 127 tys./d (w tym wsie 109 tys.), dziesiecina 5% utargu.
12. Miasto -> Bank i mieszczanie: raty dlugu rad (dlug 4.02 mln w 23 miastach).
13. Wrogi rod -> zwyciezca: okup; korona bierze 1/9.
14. Wies i karawana -> banda: lup (zywnosc 2.2 tys. zl/d, kiesy rozbitych karawan); banda -> paser w miescie.
15. Kopalnia -> mennica -> kasy: nowa moneta 2.8-11 tys. zl/d (1.5-1.9% rocznie), oplata 5% dla korony.

### Gdzie lezy zloto (doba 109814, swiat 225.2 mln)
| Kto | Stan | Udzial | Zmiana w ok. 62 doby | Tempo na rok (364 dni) |
|---|---|---|---|---|
| Skarbce koron | 61.66 mln | 27% | -0.91 mln | ok. -5 mln (wojna; 4 korony w pokoju rosna) |
| Glowy rodow AI | 71.39 mln | 32% | -0.04 mln | plasko |
| Pozostali lordowie AI | 8.29 mln | 4% | -0.10 mln | plasko |
| Gracz | 4.93 mln | 2% | +4.08 mln | ok. +24 mln (okupy ok. 3.5 mln, skup sprzetu 0.6 mln) |
| Notable | 20.01 mln | 9% | -0.16 mln | ok. -1 mln |
| Lud (kiesy) | 20.40 mln | 9% | -1.15 mln | ok. -7 mln - do wyjasnienia |
| Kasy miast | 11.83 mln | 5% | 13.78 -> 11.78, potem stabilnie | ok. -2 mln po korekcie firm |
| Karawany (moneta) | 2.52 mln | 1% | brak szeregu | + towar w jukach 2.62 mln |
| Bank | 3.23 mln | 1.5% | zysk 384 zl/d | plasko |
| Reszta (zamki, wsie, zalogi, bandy) | ok. 21 mln | 9% | nie zestawiono | - |

Wnioski z bilansu:
- Pieniadz nie splywa do lordow AI. Splywa do gracza i powoli ubywa ludowi.
- Cztery korony w pokoju (Vale, Dorne, Targaryen, Aegon) trzymaja 32.3 mln = 14% swiata i rosna.
- Kasy miast maja w sumie tyle, ile trzeba (11.83 wobec celu 11.57 mln), ale zle rozlozone: 46-47 z 97 ponizej zapasu kupcow, 31 ponizej polowy.
- Suma "pomp" (pieniadz wracajacy w dol) to 1.23-1.40 mln zl w dobie raty, ponad dwa razy zold.

## 3. TABELA: u nas a w sredniowieczu

| Zjawisko | U nas | Sredniowiecze (rzad wielkosci, z pamieci) | Ocena |
|---|---|---|---|
| Zamkniecie obiegu | rozjazd 0; przyrost monety 1.5-1.9%/rok z mennicy | podaz kruszcu rosla ok. 1-2%/rok w dobrych okresach | wierne |
| Oplata mennicza | 5% | 1-5% w Anglii, wiecej we Francji przy psuciu monety | wierne |
| Udzial ludnosci miast | ok. 15% ludzi ksiegi | Anglia ok. 15%, Flandria i Italia 25-35% | wierne |
| Wies z wlasnego pola | ok. 80% utrzymania, 86% kalorii | wiekszosc plonu zjadana na miejscu | wierne |
| Renta pana | ok. 10% produkcji + dziesiecina 5% utargu | dziesiecina 10% plonu, czynsze i powinnosci kolejne 10-20% (Dyer) | nieco za lekka, zostawic |
| Jedzenie w wydatkach ludu | 56-57% | 60-80% u najemnika i biedoty | blisko |
| Chleb a zboze | wsad to ok. 30% ceny, wyrob 7.5x wsadu | asyza chleba: zboze 75-80% ceny | oderwane |
| Wahanie ceny zboza w roku | +2.9% w 88 dob | 20-30% miedzy zniwami a przednowkiem | za plaskie (opis, nie blad) |
| Placa fachowca do robotnika | stawka wyzsza, dochod stanu 891 wobec 846 zl/rok | 1.5-2x (ciesla 3d, robotnik 1.5-2d ok. 1300) | stawka dobra, brak pracy dla 84% rak |
| Zold a pieniadz swiata | 87% monety rocznie | ciezka wojna Anglii 1294-98: ok. 20% monety rocznie | wojsko 4x za ciezkie (skutek trzech skal) |
| Skarby i dlugi koron | 27% monety, dlug 0.27 mln | krolowie stale zadluzeni (Edward III, Bardi i Peruzzi) | odwrotnie niz w historii |
| Pan a kasa miasta | 69-82% doplywu netto kas | odniesienie z logu ok. 10% (zrodlo nieustalone) | bardzo ciezkie, ale z dzialajacym hamulcem |
| Myto i clo | chlop 4%, kupiec ok. 5% lacznie | 1-5% | wierne |
| Straty karawan | ok. 25-35% floty rocznie, 0.8% na odcinek | w wojnie 15-20% rocznie | blisko, nieco wysoko |
| Liczba kupcow a zysk | sztywna (odradzanie 75%/d) | kupcy wchodzili i wychodzili z handlu | uproszczone |
| Zysk handlu dalekiego | sektor 380-440%/rok (szacunek) | 15-40% na udanej wyprawie, kilka obrotow w roku | za wysoki, bo kapital za maly |
| Skad bandy | dezercja 101, rozbitkowie 24, bieda i wojna 11 na dobe | dezerterzy i rozpuszczone kompanie | wierne |
| Bandy a pokoj | w pokoju wracaja do domu 4x szybciej | po 1360 kompanie rosly w pokoju | odwrotnie (decyzja projektu) |
| Haracz i okup wsi | okupy AI 0, lup 2.2 tys. zl/d | "patis" - staly haracz od wsi | brak, zostawic |
| Okup jenca | 0.5 rocznego dochodu, pulap rok | zwykle roczny dochod z dobr | wierne; powtarzalnosc u gracza do decyzji |
| Zmiana upraw za cena | 0-1 wioska na tydzien | powolna, ale realna w skali lat | martwe |
| Rok urodzaju i glodu | wylaczony cala zime | zly rok co kilka lat, glod 1315-17 | martwe do wiosny |

## 4. LISTA POPRAWEK (od najbardziej oplacalnych; jedna naraz)

### A. Dziury i martwe petle
| Nr | Poprawka | Co zmienia dla gracza i swiata | Ryzyko | Koszt | Jak zmierzyc |
|---|---|---|---|---|---|
| A1 | Same linie logu: gracz osobno od "lordow"; Straznik pokazuje stawke obowiazujaca (3%), nie cien 3.7%; w "Pompach" brakujaca danina wsi i czesc ludu; "brak zlota/kupca" na trzy powody; powod postoju przebudowy wsi; ile rodow ponad prog wydawania skarbu; dlugi miast na krolestwa; kto rozbija bandy; znacznik oblezenia w ks-miasta.csv | Nic w grze; kolejne decyzje przestaja byc zgadywaniem | brak | S | linie pojawiaja sie w logu |
| A2 | Sprawdzic dosypke gry do kies notabli (+116 662 "z niczego" w dobie) | Jesli to prawdziwa dosypka, jest to jedyny znany kran lamiacy "nic z niczego" | brak (sledztwo) | S | podzial: przelew z kapitalu a dosypka |
| A3 | Pamiec doplywu miast zapisywana w zapisie, tak jak u zamkow | Po wczytaniu gry miasta nie sa przez 28 dob bez hamulca podatkow; mniej miast z pusta kasa | niskie (zmienia tylko nalezna rate) | S-M | w pierwszych 12 dobach sesji "szybka obnizka" > 0 i "z firma ponad doplyw" rozne od 0 |
| A4 | Przebudowy wsi: po liczniku z A1 zwolnic blokade okregu i regionu przez przebudowe stojaca N dob (albo skrocic porzucenie z roku), z bezpiecznikiem zboza w zimie | Wsie zaczynaja zmieniac zajecia za cena; wiecej drewna, lnu, soli; polki wyrobow sie zapelniaja | srednie: masowe odejscie od zboza w zimie | M | "zaczete/skonczone" > 0 na tydzien, "wstrzymane" spada z 496 |
| A5 | Bron w komis: sztuka idzie na polke, warsztat i rzemieslnik dostaja zaplate przy sprzedazy | Lordowie i partie AI lepiej uzbrojeni (korpus 76% -> wyzej); gracz widzi pelniejsze kramy; rzemieslnicy maja place | srednie: zalew polki, spadek cen, zlom; pelna kontrola ksiegi | M-L | "brak zlota/kupca" 1098 w dol, "bez towaru" 105 tys. w dol, pokrycie AI w gore |
| A6 | Sprzedaz gracza: cena spada z kazda sztuka w jednej transakcji albo pulap wobec kasy miasta | Koniec wyplat 430 tys. za 11 sztuk z jednej kasy; gracz sprzedaje lup w kilku miastach | niskie; gracz zarabia mniej na lupie | S-M | sprzedaz gracza jako % wartosci, zadna doba ponad 100% |
| A7 | Sufit ceny x3 takze dla notabli zbrojacych ochotnikow (i trzech sciezek "lepsze za swoje"); notable nie kupuja sztuk z dodatnim stanem | Notable nie przeplacaja do 4.9x; ich kiesy przestaja chudnac | niskie: wiecej cofnietych awansow ochotnikow | S | linia zakupow AI: "ponad 3.0x" = 0 |
| A8 | Hamulce firm dla 50 miast poza regula i dla czesci z oszczednosci ludu; kolejnosc wierzycieli: wies wobec pana | Dlug miast wobec wsi (7.43 mln) przestaje rosnac; wsie maja gotowke na roboty | srednie: nizszy dochod panow, wiecej zwolnien wojska | M | rachunki wsi w dol, D rodow, liczba zwolnionych |

### B. Realizm proporcji
| Nr | Poprawka | Co zmienia | Ryzyko | Koszt | Jak zmierzyc |
|---|---|---|---|---|---|
| B1 | Karawany: jedna regula kapitalu obrotowego (prog oddawania zysku i podloga kapitalu razem) | Karawany woza wiekszy ladunek; roznice cen miedzy miastami maleja; gracz ma mniejszy arbitraz | srednie: chwilowo mniej dla notabli i kas; czesciej "przesyt" | M | marza 71% w dol, "unosi 34 doby" w dol, odrzucenia "kapital w towarze" 453 w dol, kontrola roznica 0 |
| B2 | Chleb zwiazany z cena zboza | Koszyk biedoty tanszy, ale place piekarzy nizsze | srednie: piekarnie to najwiekszy platnik plac | M | koszt koszyka i dochod rzemieslnikow naraz |
| B3 | Miara dostatku bez przelewow wewnatrz ludu (18% liczone dwa razy), z nowymi progami awansu | Awans biedoty do rzemiosla przestaje byc stale otwarty | srednie: przesuwa awanse i kupno ziemi | S-M | Dostatek biedoty, liczba awansow |
| B4 | Zold krolewski w wojnie wedlug potrzeby rodu | Korona w wojnie traci wolniej; bogate rody nie dostaja doplat | dotyka decyzji Jeffa z 01.11 i gracza | S | skarbiec KL (-55 tys./d), kiesy rodow KL |
| B5 | Podloga rat firm 0.5 -> 1.0 (tylko jesli A3 nie wystarczy) | Kasy miast nie placa rat z kapitalu kupcow; uruchamia istniejaca obnizke firm | srednie: wiecej pozyczek miast, mniejsze wojska | S | kasy ponizej zapasu (46), dlug miast, dwa przeglady kwartalne |
| B6 | Drobne: rece nie schodza z towaru, ktorego brakuje (len); drwale nie pala drewna na niechciany wegiel | Mniej marnotrawstwa surowca | niskie | S | len -8.8 szt./d -> 0, wegiel drwali 23 -> 0 |
| B7 | Parametry wydawania skarbu rodow w pokoju (prog 120 dni, dzielnik 180) - po pomiarze z A1 i po A5 | Pieniadz z gory wraca szybciej, o ile jest co kupic | bez towaru to przelew za nic | S | "bez towaru", udzial glow rodow w monecie |

### C. Urozmaicenia
| Nr | Poprawka | Co zmienia | Ryzyko | Koszt | Jak zmierzyc |
|---|---|---|---|---|---|
| C1 | Karawana wycenia ryzyko trasy (zero, gdy silniejsza od bandy) | Niebezpieczne miasta sa rzadziej odwiedzane | niskie, efekt maly | S | zmienione decyzje (dzis 18 z 245) |
| C2 | Kupiec czeka 14 dob po rozbiciu karawany | Rozboj realnie zmniejsza handel o 3-4% | niskie | S | liczba karawan, nowe na dobe |
| C3 | Losowosc plonu w zimie (rok lepszy i gorszy) | Lokalne glody, skoki cen | wysokie: zapas zimy ma zerowy margines ponad racjonowanie | M | wsie na przednowku, indeks zboza |
| C4 | Bieda rodzi bandy w wiekszej skali (w obu miejscach wzoru naraz) | Biedne krainy staja sie niebezpieczne | srednie: wiecej band | S | naplyw "bieda/wojna" (dzis 10.8/d) |
| C5 | Pulap i powtarzalnosc okupow gracza | Pieniadz znow jest dla gracza decyzja | gracz zarabia mniej | S | zloto gracza na rok |

## 5. CZEGO NIE RUSZAC

- **Przelicznik towaru x37 i skala ludu.** Przy mniejszym lud je 22% koszyka przetrwania; skala jest tez zamrozona w zapisie.
- **Stawki zoldu, werbunku, eskorty x0.5, myta 4% i 2%.** Zgodne z logiem i historia; kupiec i tak placi ok. 5% lacznie.
- **Mennica.** Sama sie dlawi (1 z 96 bije), zlotnicy chronieni zapasem 30 dni.
- **Zwijanie stratnych karawan i brama nowych.** Test z 29.10: handel spadl o 2/3; dzis brama zatrzymalaby wejscie calkiem.
- **Wybor celu karawany wedlug sprzedazy (stara wersja).** Wylaczona z powodu wady, zastapiona nowsza regula.
- **Cena zboza od stanu spichlerzy.** Zapas jest juz rozlozony do wiosny iloscia; mnoznik dalby stala podwyzke i wiekszy dlug miast.
- **Plan zimy i prog racjonowania.** Zejscie spichlerzy jest zaplanowane, nie jest niedoborem.
- **Sufit regulatora wsi x1.3.** Wyzszy dalby plon ponad rece i ziemie.
- **Podzial robocizny miedzy biedote i rzemieslnikow.** Rzemieslnik ma juz 3.5x na glowe; plaskosc to artefakt pomiaru.
- **Nowy kran "bogaty rod wydaje przez dwor".** Dubluje dwie czynne reguly; bez towaru to pieniadz za nic.
- **Korona jako wierzyciel wlasnych miast, przymusowa pozyczka od rodow.** Kolko korona -> kasa -> korona; kredyt koron juz dziala u biednych koron.
- **Kolejnosc danina -> skarbiec -> tarczowe.** Dziala (Riverlands), wojsko nie straci po wyczerpaniu skarbca.
- **Kapital karawany tylko z kiesy notabla, nagrody za bandy z myta.** Pierwsze daje karawany bez srodkow, drugie nic nie zmienia.
- **Wyschniete zloto Casterly Rock.** Celowa regula Jeffa, nie blad.
- **Progi przezbrajania warsztatow i bankructwo z utrzymania** - dopoki nie wyjasni sie dosypka notabli (A2).

## 6. DECYZJE DLA JEFFA

1. **Miasto sprzedalo zboze wsi, pieniadze poszly do pana, wies dostala kwit (7.4 mln kwitow).** Kto ma pierwszenstwo do kasy miasta: pan czy wies? Rekomendacja: wies przed panem, ale dopiero po A3, inaczej miasta zbankrutuja.
2. **Kowal ma gotowy miecz, ale kupiec miejski nie ma gotowki, wiec miecz lezy, a lord obok nie ma czego kupic.** Czy kowal moze dac miecz w komis? Rekomendacja: tak (A5).
3. **Sprzedajesz 11 zbroi w jednym miescie i dostajesz 430 tys., wiecej niz sa warte.** Czy cena ma spadac z kazda kolejna sztuka? Rekomendacja: tak (A6).
4. **Bierzesz tego samego lorda do niewoli dwa razy w kwartal i dwa razy bierzesz okup (436 tys. i 169 tys.).** Czy drugi okup ma byc nizszy albo rod ma odmowic? Rekomendacja: pulap na rod w roku; to glowne zrodlo Twoich 4 mln.
5. **Krol w wojnie doplaca do zoldu takze rodom, ktore siedza na milionach.** Czy doplata ma zalezec od potrzeby rodu? Rekomendacja: tak (B4); to Twoja regula z 01.11, wiec zmiana tylko swiadomie.
6. **Cztery krolestwa w pokoju siedza na 32 mln i nic z nimi nie robia.** Skarb wojenny czy maja wydawac? Rekomendacja: zostawic jako skarb wojenny, dopoki nie ma towaru do kupienia.
7. **Krolewska Przystan biednieje od wojny, 95 ze 114 rodow chce pokoju, ale wojna fabularna nie pozwala go zawrzec.** Czy bieda moze konczyc wojny kanonu? Rekomendacja: nie ruszac wojen fabularnych; rozwazyc tylko dla zwyklych.
8. **Piekarz zarabia na chlebie 7 razy wiecej, niz kosztuje zboze, ale z tej marzy zyja czeladnicy.** Tanszy chleb czy wyzsze place? Rekomendacja: odlozyc, najpierw A i B1.
9. **Zima potrwa jeszcze ponad 3 lata gry i kazdy rok jest taki sam.** Czy dodac lepsze i gorsze lata z ryzykiem prawdziwego glodu? Rekomendacja: tak, ale z malym rozrzutem i dopiero po odblokowaniu wsi (A4).
10. **W pokoju bandy rozchodza sie do domow cztery razy szybciej.** Czy po wojnie maja rosnac (jak kompanie najemnikow) i czy bieda ma rodzic wiecej zbojcow? Rekomendacja: zostawic powrot do domu, podniesc lekko kanal biedy (C4).
11. **Polowa miast (50 z 97) nie ma szybkiego hamulca podatkow, a lud placi czesc z oszczednosci bez zadnej obnizki.** Objac wszystkie? Rekomendacja: tak, po A3.
12. **Wsie chca przejsc ze zboza na jedwab i bydlo (ponad 200 wsi) w srodku zimy.** Pozwolic? Rekomendacja: odblokowac, ale zboze w zimie trzymac bezpiecznikiem.

## 7. CZEGO NIE SPRAWDZONO

- Lata, zniw, pokoju, jarmarkow i roku urodzaju - w logach jest tylko zima i wojna.
- Dlaczego lud traci ok. 1.15 mln w 61 dob (jedyny wyrazny spadek poza koronami).
- Reszty zrodel zlota gracza poza okupami i skupem ("inne 455 385" jednej doby).
- Dosypki notabli: przelew z kapitalu a zloto z niczego.
- Ktora bramka trzyma 70 przebudow wsi (material, kiesa wsi, kiesa ludu); licznikow zmiany zajec pod kalibracja.
- Podzialu "brak zlota/kupca" na kase miasta i kapital warsztatu; kolejnosci w dobie zaworu kas i cykli warsztatow.
- Ile rodow jest ponad prog wydawania skarbu; liczb "62 rody ponad 2R = 38.1 mln" nie odtworzyla jedna z soczewek.
- Jak liczony jest wiek rachunkow wsi ("starsze niz 30 dni 73 tys." przy 7.4 mln); "kredyt kupiecki 7.55 mln".
- Kto rozbija bandy (patrole, lordowie, Straz) i podzialu strat karawan na bandy i lordow.
- Dlugow miast w podziale na krolestwa; cen wyrobow (nie zywnosci) na miasto.
- Czy ciecie regulatora na zelazie naklada sie na lokalne reguly rudy; przyczyny postoju 30 z 34 tkalni welny.
- Lancucha modeli cen (BetterEconomy, BKROTPatch) i udzialu kontraktow BetterEconomy w popycie.
- Zrodla odniesienia "ok. 10%" w logu oraz wszystkich liczb historycznych w tabeli 3.
- Udzialu "reszty" ok. 21 mln w bilansie (zamki, wsie, zalogi, bandy).
