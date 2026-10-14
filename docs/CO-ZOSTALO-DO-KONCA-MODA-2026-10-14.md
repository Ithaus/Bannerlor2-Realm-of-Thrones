# Co zostalo do konca moda - stan 14.10.2026 ok. 09:00 UTC (w grze WGRANIE 51, Armoury fce3a3d9)

Spis zrobiony z: `docs/PLAN-DO-KONCA-MODA-2026-10-09.md` (etapy 1-8, rozdz. 2-4), `docs/STAN-PRAC.md` (WGRANIA 1-51, decyzje Jeffa),
CHANGELOG wersji w grze (drzewo `zb\int`, aca89cc), `docs/PRZEKAZANIE-2026-10-08.md` rozdz. 20-30, projekty `docs/etap*`,
`docs/test-zbiorczy/RAPORT-ROCZNY-2026-10-13.md`, `docs/test-zbiorczy/BIEG-DWULETNI-2026-10-14.md`, audyt integracji 14.10.
Pelne tabele kazdej pozycji z dowodem (numer WGRANIA, wpis) - trzy inwentarze w scratchpadzie sesji c36b73ca
(`scratchpad\inwentarz\etapy1-4\INWENTARZ-ETAPY-1-4-I-DECYZJE.md`, `...\etapy5-8\INWENTARZ-ETAPY5-8.md`, `...\noc\SPRAWY-SPOZA-PLANU.md`).
Ten plik laczy je w jedna liste bez powtorzen. Kod nie byl zmieniany.

---

## 0. Dla Jeffa - prostym jezykiem

**Ile zrobione, ile zostalo**

- Plan do konca moda z 09.10 mial **155 pozycji**: 123 zadania w etapach 1-8, 23 pytania do Ciebie i 9 krokow zakonczenia.
  - **zrobione i w grze: 122**;
  - **odpadlo: 5** - trzy razy powiedziales "nie" (tempo marszu, wylaczenie paska zbrojowni DTE, poszerzenie trzeciej kapitana), dwie pozycje staly sie nieaktualne;
  - **zostalo: 28** - 13 zaczetych, ale niedokonczonych (np. drogi: proba jest w kodzie, ale wylaczona; wiecej jazdy u AI: jest, ale za malo), 15 niezaczetych (w tym 6 z 9 krokow zakonczenia).
- Poza planem (nocne paczki, raport roczny, bieg dwuletni, audyt) zrobiono ok. 45 spraw, a zostalo ok. 60 mniejszych. Czesc z nich to te same sprawy
  co w planie, opisane w innym raporcie.
- **Po zlaczeniu powtorzen zostaje 42 pozycji do zrobienia** (czesc to paczki kilku drobnych bledow) **+ 5 pomyslow, ktore odlozyles "na pozniej".**
- Szacunek pracy: **ok. 130-210 godzin pracy Claude** (bez pomyslow na pozniej). Czesc idzie rownolegle, a sporo z tego to czas testow w grze.
  Najwiecej zajma: wojsko w drugim roku, drogi, towary (plotno, skora, zbroja), strojenie liczb i koncowy test 4-letni.
  Do tego ok. 3-5 godzin Twojej gry na testy reczne.

**Najwazniejsze, co zostalo**

1. **Wojsko w drugim roku gry.** Po ok. 450 dniach ROT przestaje wymuszac swoje fabularne wojny. 16 krolestw zawiera pokoj, rozpuszcza wojsko
   prawie do zera i juz nie wraca do wojny. Lordowie maja 55 tys. ludzi zamiast 85-100 tys. Duza czesc winy to nasza wlasna regula zoldu w pokoju.
   **Pracuje nad tym teraz.**
2. **Plotno** - nie ma go w 3/4 miast, choc lnu jest juz 2 razy wiecej. Przyczyny nie sprawdzilem, tylko ja zalozylem - to byl blad.
   **Teraz sprawdzam caly lancuch od lnu do plotna.**
3. **Drogi** - nie zrobione. W kodzie jest tylko proba na dwoch kawalkach mapy, wylaczona. Najpierw pokaze Ci zdjecia, potem zrobie pelna siec
   ("trakty + droga do kazdej wioski").
4. **Pieniadze w drugim roku.** Najbiedniejsze rody (Wolni Ludzie, Zelazne Wyspy, Pentos, Smocza Skala) nie maja na zold, a w wielkich miastach
   zima 7 miast nie stac na jedzenie. Jednoczesnie oszczednosci mieszczan leza bezczynnie (25 mln).
5. **Twoje wlasne sciezki** (lenno, tarczowe, lup przy szturmie, rozbici po klesce) nie przeszly zadnego testu w grze, bo autotest gra postacia
   bez lenna. Potrzebna jest ok. godzina Twojej gry w nowej kampanii.
6. **Na koniec:** strojenie liczb, przyspieszenie gry, test 4-letni, porzadek w menu, dokumenty i instrukcja dla Ciebie.

**Pytania do Ciebie** (wystarczy jedno slowo przy kazdym; nie blokuja reszty pracy, szczegoly w grupie C):
1. Czy po koncu fabuly ROT (ok. doba 450) swiat ma sam wracac do wojen, czy zostawiamy to AI gry? Moja rada: najpierw naprawie nasza regule
   zoldu w pokoju, a fabuly ROT nie ruszam.
2. Czy okolica ma byc niebezpieczna wedlug tego, ile osad wokol opustoszalo? Najpierw dostaniesz liczby.
3. Czy zwolniony weteran zabiera swoj komplet ze zbrojowni? Rada: tak.
4. Czy po Twojej klesce tracisz tez czesc zbrojowni? Rada: tak samo jak lord AI.
5. Czy kucie ma trwac tyle co praca kowala AI? Rada: tak (jedna regula). Jesli Ci obojetne - zdecyduje sam.

---

## 1. Lista tego, co zostalo

**Godziny** = szacunek pracy Claude razem z testem. **(plan)** = wielkosc z planu (M ok. 1 h, S 2-4 h, D kilka paczek = 6-12 h i wiecej);
**(szac.)** = moja ocena, bo dokument jej nie podaje. **Stan:** W TOKU / ZACZETE (czesc zrobiona) / NIEZACZETE / CZEKA NA CIEBIE.
W kolumnie "skad" sa numery dla nas (plan, raporty) - nie musisz ich czytac.

### A. Bledy i braki do naprawy (17 pozycji, razem ok. 60 spraw; ok. 60-100 h)

| nr | Co (jedno zdanie) | Godziny | Zalezy od | Stan / skad |
|---|---|---|---|---|
| A1 | **Wojsko w drugim roku ("pulapka pokoju").** Po ok. dobie 450 ROT przestaje wymuszac wojny fabularne, 16 krolestw zawiera pokoj, rozpuszcza wojsko prawie do zera i nie wraca do wojny - 55.3 tys. zamiast 85-100 tys.; w duzej czesci to nasza regula zoldu w pokoju (pulap 0.28 dochodu zamiast 0.60). | 6-10 (plan D; kazdy wariant wymaga biegu dwuletniego, ok. 2.5 h gry) | wynik rozpoznania "kto zawiera pokoj" (w toku); pytanie C1 tylko, jesli trzeba by ruszac wojny ROT | **W TOKU:** rozpoznanie wf_1b000dae-708 + proba suwaka zoldu w pokoju wf_a52b4a96-f5a (zapis z d720 + 60 dob). Skad: PLAN 2.7 i Z.1 (zold w pokoju), PRZEKAZANIE 30 pkt 3, BD 0 pkt 4 i 8a, raport roczny R2/R27, 6 z 10 alarmow BD 2.6; pokoj z biedy (PLAN 2.15) - 0 wnioskow, sprawdza to samo rozpoznanie |
| A2 | **Plotno.** W 75-77 z 97 miast nie ma plotna, choc lnu jest 2 razy wiecej (860 szt. dziennie); to, ze winne sa tkalnie, jest tylko przypuszczeniem. | 3-6 (szac.) | rozpoznanie lancucha len -> tkalnia -> plotno (w toku) | **W TOKU** rozpoznanie (wf_1b000dae-708). Skad: BD 8d i NIE nr 36, raport roczny R8, progi etapu 6 |
| A3 | **Biedne rody w drugim roku.** Wolni Ludzie, Zelazne Wyspy, Pentos, Smocza Skala: brak na zold, ktorego nikt nie pokrywa, rosnie z 24 do 54 tys. zl dziennie, a zapas skarbcow koron prawie sie konczy (64 -> 6 tys. zl dziennie). | 4-8 (szac.) | A1 - ta sama regula budzetu; robic razem, jeden bieg dwuletni | NIEZACZETE. Skad: BD 0 pkt 8b i 2.6, raport roczny R10 |
| A4 | **Biedota wielkich miast w zimie.** Na koniec drugiego roku 7 miast (Highgarden, Oldtown, Braavos i in.) nie stac na jedzenie; "chleb swiata" 89.7% przy celu 98% i spada co kwartal - moja nocna decyzja "to cecha" byla na danych z roku 1 i jest do odwolania. | 3-5 (szac.) | po A2, A6 (towary) i A1, A5 (pieniadze) | NIEZACZETE. Skad: BD 8c i NIE nr 23, raport roczny R15, progi etapu 5, STAN-PRAC 03:55 14.10 |
| A5 | **Oszczednosci mieszczan leza bezczynnie.** Rosna do 25 mln (prog 6 mln), z tego 16.6 mln w krolestwach w pokoju, w ktorych danina nie bierze. | 2-4 (szac.) | A1 i A3 - to moze byc zrodlo pieniedzy dla biednych rodow | ZACZETE (danina WGRANIE 39, obieg kiesy WGRANIE 49). Skad: BD NIE nr 22 i 46, raport roczny R5, STAN-PRAC (danina "bez potrzeby korony") |
| A6 | **Skora i dobra zbroja.** W zimie drugiego roku 79 miast bez skory wyprawionej (przyczyna nieznana); dobrej zbroi (stopien 3-4) dalej 70% potrzeby przy celu 75%; nierozstrzygniete liczby: rece platnerzy 1.3 czy 1.5, zwierzeta rzezne pod regulator. | 2-4 (plan S) | A2 (ta sama metoda rozpoznania) | ZACZETE (skora i platnerze WGRANIE 43). Skad: BD NIE nr 17, raport roczny R9/R13, STAN-PRAC 19:40 13.10 (sprawy N5) |
| A7 | **Warunkowe paczki towarow z planu.** Wiecej rak i kopaln; chleb, piwo, wino i deski z wsadu, srebro, rzemioslo wsi BK; tarczownicy, strzaly, "dobytek osady"; do tego stojace kopalnie BK w miastach, 4 towary bez produkcji, zapas drewna 12 dni (pasmo 30-60), zamki bez wozow kupcow. Robic tylko to, co pokaze rozpoznanie A2/A6. | 6-12 (plan: 6.2 D, 6.5 D, 6.8 S) | A2, A6 | NIEZACZETE (warunek "po tescie etapu 6" - test pokazal braki; czesc pokryta: rece platnerzy x1.3, place, rece warsztatow wedlug ludzi). Skad: PLAN 6.2, 6.5, 6.8; raport roczny R6, R12, R34, R41; BD 4.5 |
| A8 | **Za malo jazdy u AI.** Ciezka jazda 17.6% przy celu 27.5%, Dothrakowie 43.9% konnych przy celu 60%; proponowana zmiana (lord moze kupic 40% zamiast 25% koni z targu) nie zrobiona. | 2-4 (plan S) | nic | ZACZETE (rycerz z koniem WGRANIE 34; `AiMountMarketSharePercent` dalej 25). Skad: PLAN 6.14, raport roczny R14/R39, STAN-PRAC "OTWARTE PO WGRANIU 34" pkt 2 |
| A9 | **Dwie rzeczy etapu 8 "w cieniu".** Zuzycie amunicji przez AI po bitwie i sufit ceny zakupow sprzetu AI - kod jest, ale wylaczony; mialy byc wlaczone po tescie zbiorczym. | 2-3 (plan S) | nic (wlaczenie + autotest + sprawdzenie zbrojowni) | ZACZETE (kod WGRANIE 34; `AiAmmoUse = false`, `AiGearMaxPriceFactor = 0`). Skad: PLAN 8.4, 8.8 |
| A10 | **Rzeczy z domu rekruta.** Po zmianie armii krolestw komplety, ktore rekrut przynosi z domu, potanialy o ok. 60% - trzeba je liczyc wedlug rodzaju sprzetu. | ok. 1 (plan M) | nic | NIEZACZETE, brak sladu w dziennikach. Skad: PLAN 1.5 |
| A11 | **Karawany wroga w zdobytym miescie.** Zabor karawan wroga nie zadzialal ani razu w 39 zdobyciach - nie wiadomo, czy kod w ogole sie odpala. | 1-2 (szac.) | nic | NIEZACZETE. Skad: BD 8f i 9 pkt 5 |
| A12 | **Drobne bledy z biegu dwuletniego (10).** Nasze sciezki spychaja kasy miast ponizej zapasu (9 razy); polowa uprzezy znika z ksiegi (-81%); wsie "zatkane" bez wozu przez 6 dob; partia zawracana ponad 3 razy na dobe; bankrut jednego rodu (d642); Braavos bez zaworu kas przez 220 dob; stos koni bojowych do 3 397 dni zuzycia; regula dlugu (dluznik "do lasu" 47 z 94); trafieni spustoszeniem; rownanie krain valyrian -0.2%. | 6-10 (szac.; przy 3 sprawach przyczyna nieznana) | nic | NIEZACZETE (STAN-PRAC 08:59 14.10: "drobne bledy etapu 8 NIE zrobione"). Skad: BD rozdz. 7 NIE nr 2-7, 20, 24, 43, 44; raport roczny R35 |
| A13 | **Komunikaty i menu po audycie (6).** Falszywe "has fallen to the enemy" przy zmianie strony Twojego rodu; ostrzezenie "the sack has left it bleeding" trafia nie tam; podpowiedzi lupu bez towaru z polek; komunikaty lupu bez udzialu korony i "purses grew by 0" przy wylaczonej kiesie; klucze bitew w grupie MCM "Iron Bank"; wyjasnienie stawki wojennej gracza po wczytaniu. | 2-4 (szac.) | nic | NIEZACZETE. Skad: audyt integracji 14.10 (gracz pkt 4, 6, 9, 10, 12), WGRANIE 50 pkt 3 |
| A14 | **Zapis, wczytanie i stany po audycie (5).** Wczytanie w trakcie doby gubi punkty i liczy zwrot dwa razy; Sarnor liczony, zanim MCM nalozy ustawienia; wylaczenie poprawki klimatu cofa 5 wsi lnianych; skarbiec krolestwa upadajacego razem ze stolica znika ze swiata; progi narzedzia porownuja okna roznych biegow. | 3-5 (szac.) | nic | NIEZACZETE. Skad: audyt integracji (zapis pkt 2-4, obieg Z2 i Z7) |
| A15 | **Pieniadze koron - drobne z raportu rocznego (5).** Korona nie doplaca do zoldu zalog (zalogi 58 tys. przy 69 tys.); wezwania sojusznikow, na ktore nie stac (Skagos 117 tys. zl); zwyciezca nic nie dostaje przy pokoju fabularnym; kraje w pokoju placa najemnikom z zapasu skarbca; +373 tys. z rozliczen rodow w ksiedze pieniadza. | 4-6 (szac.) | A1, A3 - czesc moze zniknac po tamtych zmianach | NIEZACZETE, brak sladu. Skad: raport roczny R3, R20, R21, R33, R36 |
| A16 | **Do obejrzenia w logach (13).** Dobrobyt miast +34% w roku wojny; 188 tys. uchodzcow poza domem; wyrzutki i bandy rosna; dlug koron w Banku; notable traca zloto; zalegly zold 3% (prog 2%); 2.3 lorda rusza noca (prog 2.0); brak pomiaru zgonow i narodzin bohaterow; dwa rachunki ludnosci (BK -18.4%, nasz -1%); rody z doplata nie werbuja wiecej; 41% nowych karawan bez koni jucznych; linia Strazy (Straz 3 640 przy 7 387); powracajacy rozbici ponad budzet zoldu. Najpierw ocena z logow biegu dwuletniego, potem decyzja, co jest bledem. | 4-6 (rozpoznanie; naprawy osobno) | nic | NIEZACZETE. Skad: raport roczny R16, R25, R26, R28, R30, R31, R38, R40, R42; STAN-PRAC 13-14.10 (W5-4, karawany, K8, N7) |
| A17 | **Stare zalegle sprawy z kolejek (10).** Kolejka odziezy wojska; port Saath i Lordsport bez nadwyzki; reszta koni w ksiedze; unikat "cersei_red_dress" znika z targu; 2.8% przejsc obok mostu Bliznakow; port placi za statki, ktore stocznia kasuje; ryby bez zrodla (+72 dziennie); Thenn - kasa placi taborom wiecej, niz zarabia; Twoj rynsztunek trzymany przez zywego lorda bez odkupu (niewola); Inni z niczego przy "pojedynku z Nocnym Krolem". | 8-12 (szac.) | nic | NIEZACZETE (ryby i Thenn tylko zmierzone, WGRANIE 45). Skad: STAN-PRAC "OTWARTE PO WGRANIU 34" pkt 5, PRZEKAZANIE 25, PLAN 4.18 |

### B. Zatwierdzone przez Ciebie, a niezrobione (8 pozycji; ok. 25-45 h)

| nr | Co (jedno zdanie) | Godziny | Zalezy od | Stan / skad |
|---|---|---|---|---|
| B1 | **Drogi.** Chciales "trakty + droga do kazdej wioski" (po zdjeciach). Krok 1: wlaczyc probe na 2 kawalkach mapy i zrobic Ci zdjecia; krok 2: po Twoim "tak" pelna siec drog z widokiem na mapie (premie +15% za droge odrzuciles). | krok 1: 2-4 (plan S); krok 2: 8-16 (plan D) | krok 2 - Twoje potwierdzenie po zdjeciach | ZACZETE (proba w kodzie od WGRANIA 34, `MapRoadsProbeEnabled = false`, zdjec nie bylo); **W TOKU** rozpoznanie "jak zrobic zdjecia i co trzeba do pelnej sieci". Skad: PLAN 1.13, 7.5, rozdz. 2 pyt. 20 |
| B2 | **Okup miasta zamiast szturmu.** "Tak" 13.10, jako ostatni etap lupu: obroncy moga sie wykupic od szturmu - AI z AI, Ty jako obronca i Ty jako oblegajacy. Projekt gotowy, kodu brak. | 4-6 + test (pomiar + 3 mniejsze paczki) | lepiej po A1 (wojny) | NIEZACZETE. Skad: `docs/lup-miast/OKUP-ZAMIAST-SZTURMU-PROJEKT.md`, STAN-PRAC 03:05 14.10 |
| B3 | **Myto na Bliznakach.** "Chce, ale policz": licznik jest (418 przejsc w 120 dob; myto, gdyby bylo - 27 316 zl). Krok 1: stawki z liczbami dla Ciebie; krok 2: kod. | 1 + 2-4 (szac.) | krok 2 po pokazaniu Ci liczb (stawki ustala Claude) | NIEZACZETE (sam licznik WGRANIE 34). Skad: PLAN 7.12, ESENCJA 21, STAN-PRAC 12.10 |
| B4 | **Zlota Kompania: 1/3 lukow.** Kod gotowy od 09.10, wylaczony "do czasu dozbrajania" - a dozbrajanie jest w grze od WGRANIA 10, wiec nic juz nie blokuje. | ok. 1 (plan M) + autotest | nic | NIEZACZETE (`Army175GoldenBows = false`). Skad: PLAN 1.6 |
| B5 | **Limit hordy Zewu Nocnego Krola ok. 1 140** (bez limitu ok. 5 000+). Zew budzi sie dopiero w dobie 728, wiec dotad nikt tego w grze nie widzial. | 1-2 (plan M) | test D6 | NIEZACZETE (klucza limitu nie ma w kodzie). Skad: PLAN 1.15 |
| B6 | **Proporcje winnic i oliwek** (Dorne i poludnie) - "rzeczywiste proporcje ustal". | 2-3 (plan S) | nic | NIEZACZETE, brak sladu. Skad: PLAN rozdz. 2 pyt. J (do 6.9) |
| B7 | **Smoki jedza owce** (kanon; "tak" 09.10). | 2-4 (szac.) | nic | NIEZACZETE, brak sladu w CHANGELOG. Skad: STAN-PRAC 09.10 (kanon 24c) |
| B8 | **Zloto Casterly Rock wyczerpane** (kanon; "OK po naprawie dochodow rodow" - ta naprawa juz jest). | 1-2 (szac.) | nic | NIEZACZETE, brak sladu. Skad: STAN-PRAC 09.10 (kanon 24d) |

### C. Czeka na Twoja decyzje (5 pozycji + 1 do wiadomosci; ok. 8-16 h kodu po odpowiedziach)

| nr | Pytanie | Godziny | Zalezy od | Rada / skad |
|---|---|---|---|---|
| C1 | **Wojny po koncu fabuly ROT.** Czy po ok. dobie 450 swiat ma sam wracac do wojen (np. silne i bogate krolestwo wypowiada wojne), czy zostawiamy to AI gry? I czy 4 pary wojen fabularnych "bez konca" maja dostac rozejm? | Twoja odpowiedz: minuty; kod w A1 | wynik rozpoznania (w toku) | **Rada:** fabuly ROT nie ruszac, najpierw naprawie nasza regule zoldu w pokoju (to moja sprawa, bez Ciebie); wroce z pytaniem tylko, jesli to nie wystarczy. Skad: PRZEKAZANIE 30 pkt 3 (a), raport roczny R1 |
| C2 | **Niebezpieczenstwo okolicy z pustych osad.** Czy okolica ma byc niebezpieczna wedlug tego, ile osad wokol opustoszalo (zamiast stanu "spladrowana" z gry)? Obiecales decyzje po liczbach - liczb jeszcze nie dostales. | ok. 1 (liczby) + 2-4 kod (plan S) | liczby z biegu dwuletniego | **Rada:** przygotuje liczby, zdecydujesz jednym slowem. Skad: PLAN 4.20 (Jeff 11.10) |
| C3 | **Sprzet weterana.** Czy zwolniony weteran zabiera swoj komplet ze zbrojowni (dzis bierze pieniadze i konia)? | 3-5 (szac.) | nic | **Rada:** tak (rada z projektu etapu 4). Skad: PRZEKAZANIE 25, `docs/etap4/PLAN-KODU-E410.md` |
| C4 | **Twoja strata po klesce.** Dzis pojmujacy zabiera Ci cale zloto, rynsztunek i juki; czy masz tracic tez czesc zbrojowni (zapas DTE)? | 1-3 (szac.) | nic | **Rada:** jedna regula - sprawdze, co traci pokonany lord AI, i zrobie tak samo. Skad: PLAN rozdz. 2 "strata po Twojej klesce" |
| C5 | **Szybkosc kuzni.** Czy Twoje kucie ma trwac tyle, co praca kowala w warsztacie AI, czy zostaje szybkie, a wyrownuje to nizsza zaplata? | 1-3 (szac.) | nic | **Rada:** jak u kowali AI (jedna regula); jesli Ci obojetne - zdecyduje sam. Skad: PLAN rozdz. 2 "szybkosc kuzni" |
| C6 | **Do wiadomosci (nie musisz odpowiadac):** rozbici wracaja do armii do 19-27% jej sily, nie do 35-45% jak w projekcie (cel byl nieosiagalny przy tej regule); moje nocne decyzje: najemnicy 28 dob bez przejscia do wroga, skarbce koron wedlug ludzi z podloga 1 mln, prawdziwy pobor od ludzi na razie wylaczony, okup zamiast szturmu tylko jako projekt. | - | - | Skad: STAN-PRAC 22:30 13.10 - 03:55 14.10, 23:21 13.10 |

### D. Zakonczenie (10 pozycji; ok. 35-50 h)

| nr | Co (jedno zdanie) | Godziny | Zalezy od | Stan / skad |
|---|---|---|---|---|
| D1 | **Strojenie liczb.** Jedna liczba naraz, testem rocznym: zapas korony, rezerwa wojenna, dwor, podatek wojenny; do tego odlozone liczby - czas wypuszczania skarbca korony (360 czy 180 dni), sufit wyrebu wsi (1.3 czy 1.8), cena bardy stopnia 6, prog sakiewek zolnierzy, wlaczenie "prawdziwego poboru od ludzi", progi narzedzia sprawdzajacego. O liczbach decyduje Claude (Twoja zasada). | 10-14 (plan D: ok. 5 liczb po ok. 2 h + biegi) | A1-A7 (stroic po naprawach) | ZACZETE (biegi roczne i dwuletni; zold w pokoju = A1). Skad: PLAN Z.1; noc D3, I5, F9, F12-F13, C14, B5c |
| D2 | **Przyspieszenie naszych modow (reszta).** Doba trwa dzis ok. 12 s i przez 2 lata nie zwalnia; zostaly drobne koszty: musztra 114 ms na dobe, kiesa ludu 13.5 ms, zbytki 5.6 ms i in. | 4-6 (szac.; plan D, glowne zrobione) | D1 - po zamrozeniu liczb (Twoja decyzja 08.10: wydajnosc na koniec) | ZACZETE (WGRANIE 34: 17.5 -> 11.8 s). Skad: PLAN Z.2, BD NIE nr 42 |
| D3 | **Przyspieszenie cudzego kodu (reszta).** Swaty ROT dalej kosztuja ok. 0.5 s na dobe; znaczki i kafelki druzyn pod tabliczka miasta. AI lordow - wedlug rozpoznania nie ruszac. | 3-6 (plan S) | D1 | ZACZETE (swaty WGRANIE 37/38). Skad: PLAN Z.3, `docs/zakonczenie/ROZPOZNANIE-Z3-CUDZY-KOD-2026-10-13.md` |
| D4 | **Logi ciszej i rozmiar zapisu.** Logi pomiarowe 20-40 KB na dobe, zapis po roku ok. 31.5 MB. | 1-2 (plan M) | D2 | NIEZACZETE (Twoje "nie teraz, w Z.4", 12.10). Skad: PLAN Z.4 |
| D5 | **Menu MCM.** Wylaczniki paczek zostaja, martwe suwaki i martwy kod wyciac. | 2-4 (plan S) | po ostatniej zmianie kodu | NIEZACZETE. Skad: PLAN Z.5 |
| D6 | **Test Innych od doby 728.** Z zapisu z doby 728 dalej: Zew i pochod Nocnego Krola, Inni tylko z poleglych, limit hordy z B5. | 2-3 (szac.) | B5 | NIEZACZETE. Skad: PLAN 8.15 (reszta) |
| D7 | **Koncowy test 4-letni z zima** na nowej kampanii i ostatnim DLL. | co najmniej 8 (plan D) | wszystko powyzej (A, B, D1-D6) | NIEZACZETE (jest bieg dwuletni 728/728). Skad: PLAN Z.6 |
| D8 | **Dokumenty.** Historia, bledy, opisy paczek, CHANGELOG, jednolite numery paczek, usuniecie nieaktualnych zdan. | 2-4 (plan S) | na koniec | NIEZACZETE. Skad: PLAN Z.7 |
| D9 | **Porzadki i wersja koncowa z numerem.** Wersja koncowa w repo z numerem, narzedzia, stare kopie - usuwane tylko po Twojej zgodzie. | 2-4 (plan S) | D7 | NIEZACZETE (jest galaz `noc/wgranie-51`). Skad: PLAN Z.8 |
| D10 | **Instrukcja dla Ciebie.** Co zrobic po aktualizacji BetterEconomy (skrypt), suwak Diplomacy, najwazniejsze ustawienia MCM. | ok. 1 (plan M) | D5 | NIEZACZETE. Skad: PLAN Z.9 |

### E. Twoje testy reczne (2 listy, 39 punktow; ok. 3-5 h Twojej gry)

| nr | Co | Czas | Zalezy od | Stan / skad |
|---|---|---|---|---|
| E1 | **10 Twoich sciezek z nocy 13/14.10:** tarczowe i doplata dla Twojego rodu, stawka wojenna (opcjonalnie), okno lupu przy szturmie, 3 doby bez zakupow po lupie, towar z polek i karawany wroga, skarbiec stolicy, rozbici wracaja, danina i budowy w Twoim lennie (z "Savings" i "War aid"), dymki wsi, zapis z czekajacymi rozbitymi. Autotest gra postacia bez lenna, wiec zadna z tych sciezek nie byla sprawdzona. | ok. 1 h | nowa kampania; najlepiej na wersji po A1-A2 | CZEKA NA CIEBIE. Krok po kroku: BD rozdz. 9; "Savings"/"War aid" - STAN-PRAC WGRANIE 39 |
| E2 | **29 punktow etapu 8:** kuznia i kowal, sprzedaz wedlug stanu, zlecenia kowalskie, ksiegarz, okna Spoils, bitwy z lucznikami, arena, "Send troops", turniej, zadania i oplaty, sprzedaz jencow, niewola, karczma, kulawy kon, samonaprawa, kryjowka, napad na oboz, sen w armii, wioski, Bliznaki, trzy proby zapisu. | 2-4 h | najlepiej na ostatnim DLL (po D7) | CZEKA NA CIEBIE. Krok po kroku: `docs/etap8/PROJEKT-ETAP8-DROBNE-BLEDY-OPTYMALIZACJA-2026-10-12.md` rozdz. 3 "8.16" (punkt 23 - namioty - wlaczony juz przy WGRANIU 34) |

### F. Na pozniej - tylko te, ktore sam odlozyles "na pozniej" (5 pozycji; ok. 10-19 h)

| nr | Co | Godziny | Zalezy od | Skad |
|---|---|---|---|---|
| F1 | **Kasy miast z dlugami, pozyczkami i podatkiem nadzwyczajnym** - gdy miasto nie ma z czego placic, samo podnosi podatki, a dobrobyt i zadowolenie spadaja. | 3-5 (szac.; zaczac od rozpoznania) | Twoje slowo | Jeff 07.10 "zapisz na pozniej"; PLAN rozdz. 3 |
| F2 | **Pozyczka miejska** - pan pozycza od rady miasta (ok. 12% rocznie), Ty tez, z menu miasta. | 2-4 (szac.) | Twoje slowo | Jeff 13.10 "pozniej, potwierdzone"; `docs/kiesa-ludu/PROJEKT-POZYCZKI-2026-10-13.md` (K3) |
| F3 | **Karta miejska** - miasto wykupuje przywilej (ok. 5 tys. zl dziennie); w projekcie niezalecana. | 1-2 (szac.) | Twoje slowo | jw. (K4) |
| F4 | **Sakiewka turniejowa od mieszczan.** | 1-2 (szac.) | Twoje slowo | Jeff 13.10 "pozniej"; `docs/kiesa-ludu/PROJEKT-WYDATKI-2026-10-13.md` (P5) |
| F5 | **Wlasny miecz z Qohoru** wedlug projektu z kuzni, z wlasna nazwa. | 3-6 (szac.) | Twoje slowo | Jeff 09.10 "tak, osobna paczka, pozniej"; STAN-PRAC (177-2) |

Reszta pomyslow z rozdz. 3 planu (np. okup okolicy zamiast palenia wsi, jarmarki, kruki, sztormy, posag) nie zostala przyjeta - tylko na Twoje slowo; nie liczone.

---

## 2. Proponowana kolejnosc

0. **Od razu:** Twoje odpowiedzi na pytania C1-C5 (jedno slowo przy kazdym) - nie blokuja reszty.
1. **W toku:** rozpoznanie "kto zawiera pokoj / czemu brakuje plotna / co z drogami" i proba zoldu w pokoju na zapisie z doby 720.
2. **Towary:** plotno (A2), skora i zbroja (A6), potem tylko potrzebne paczki towarow (A7).
3. **Wojsko i pieniadze w drugim roku razem:** A1 + A3 + A5 + A15; na koniec jeden bieg dwuletni.
4. **Biedota wielkich miast (A4)** - po krokach 2-3, bo zalezy od towarow i pieniedzy.
5. **Szybkie wlaczenia z jednym autotestem:** jazda AI (A8), amunicja i sufit ceny AI (A9), rzeczy rekruta (A10), Zlota Kompania (B4), limit hordy (B5), sprawdzenie karawan wroga (A11).
6. **Drogi:** zdjecia (B1 krok 1) -> Twoje "tak" -> pelna siec (B1 krok 2).
7. **Drobne paczkami:** A12, A13, A14, A17; ocena A16; kanon B6-B8; myto B3 (najpierw liczby); odpowiedzi C2-C5 w kod.
8. **Okup zamiast szturmu (B2)** - ostatni etap lupu, jak chciales.
9. **Zakonczenie:** strojenie liczb (D1) -> szybkosc i logi (D2-D4) -> menu MCM (D5) -> test Innych (D6) -> test 4-letni (D7) -> dokumenty, porzadki, instrukcja (D8-D10).
10. **Twoje testy reczne:** E1 (moze byc wczesniej, kiedy zechcesz) i E2; potem Twoja nowa kampania.

Pomysly z grupy F - tylko na Twoje slowo.

---

## 3. Liczby i zlaczone powtorzenia (dla nas)

| Zrodlo | Pozycji | Zrobione | Odpadlo | Zostalo |
|---|---|---|---|---|
| Plan: etapy 1-4 + rozdz. 2 | 84 | 72 | 2 (O, trzecia kapitana) | 10 (1.5, 1.6, 1.13, 1.15, 2.7, 4.20, pyt. 20, J, strata po klesce, szybkosc kuzni) |
| Plan: etapy 5-8 | 62 | 50 | 3 (5.12, 7.2, 7.3) | 9 (6.2, 6.5, 6.8, 6.14, 7.5, 8.4, 8.8, 8.15, 8.16) |
| Plan: zakonczenie Z.1-Z.9 | 9 | 0 | 0 | 9 (Z.1-Z.3 zaczete) |
| **Plan razem** | **155** | **122** | **5** | **28** |
| Spoza planu (inwentarz "noc": PRZEKAZANIE 25-30, raport roczny, bieg dwuletni, audyt, STAN-PRAC 13-14.10, pomysly) | ok. 150 wierszy | 46 wierszy (czesc to te same sprawy) | 7 (nieaktualne albo Twoje "nie") | ok. 60 spraw -> zlaczone w grupy A12-A17 i pozycje A-F |

Kazda z 28 pozostalych pozycji planu jest na liscie powyzej (A1, A7-A10, B1, B4-B6, C2, C4, C5, D1-D10, E2).

Zlaczone powtorzenia (ta sama sprawa w kilku dokumentach):
- **wojsko w drugim roku** = PLAN 2.7 = Z.1 (zold w pokoju) = PRZEKAZANIE 30 pkt 3 = raport roczny R2 (rok 2), R27 = BD alarmy 2.6 -> **A1** (+ pytanie C1 = R1);
- **plotno** = progi etapu 6 = BD 8d = raport roczny R8 -> **A2**;
- **biedota miast** = progi etapu 5 (chleb) = BD 8c = raport roczny R15 -> **A4**; **lokata** = progi etapu 5 = R5 = danina "bez potrzeby korony" -> **A5**;
- **skora / zbroja** = raport roczny R9, R13 = kolejka po WGRANIU 34 (skora) = progi etapu 6 -> **A6**;
- **jazda AI** = PLAN 6.14 = raport roczny R14, R39 = kolejka po WGRANIU 34 pkt 2 -> **A8**;
- **drogi** = PLAN 1.13 = 7.5 = rozdz. 2 pyt. 20 = kolejka C5 -> **B1**;
- **myto** = PLAN 7.12 = pomysly P3 = kolejka C6 -> **B3**;
- **okup zamiast szturmu** = pomysly P2 = PRZEKAZANIE 30 (5b) -> **B2**;
- **testy reczne** = PLAN 8.16 = PRZEKAZANIE 30 pkt 2 i 6 = audyt gracz pkt 2 = "Savings"/"War aid" -> **E1, E2**;
- **ryby i Thenn** = poprawka 6 (A4b, A4c) = raport roczny R23, R24 -> **A17**;
- **sakiewki zolnierzy** = progi etapu 5 = kolejka C14 = raport roczny R32 -> **D1**;
- **kasy miast z dlugami i pokrewne** = PLAN rozdz. 3 = pomysly P1 = pamiec 07.10 = I1-I4 -> **F1-F4**.

Uwagi do pozycji "zrobionych" (zrobione, ale z zastrzezeniem; nie liczone jako "zostalo"): pokoj z biedy (PLAN 2.15) - 0 wnioskow w obu testach, sprawdza to rozpoznanie
z A1; "powinnosci od dochodu brutto" (2.6) - brak osobnego sladu; Lordowie bez Atletyki do zbroi (1.7) i zyznosc krain (4.11) - Twoje "nie".
