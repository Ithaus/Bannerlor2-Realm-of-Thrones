# PROJEKT ETAP 2 - koniec fali bankructw (09.10.2026, wersja 2 po krytyce)

Synteza do wykonania etapu 2 planu (`docs/PLAN-DO-KONCA-MODA-2026-10-09.md`, pozycje 2.1-2.16). Tylko projekt: kod, gra, ustawienia i zapisy
nietkniete, gra nieuruchamiana, nic nie wypchniete. Podstawa: rozpoznanie `scratchpad\etap2\stan.md` (stan kodu i logow), `projekty.md`
(co zaprojektowane, 26 sprzecznosci, braki M1-M18), `historia.md` (przeliczenie historii na zloto gry), PROJEKT-EKONOMIA-OBIEG (OB), audyty
04, 05, 11, 12, 15 i ESENCJA, PROJEKT-KIESA-LUDU (KL), decyzje Jeffa ze STAN-PRAC **do 09.10 ok. 10:50** (w wersji 2 doszly dwie: 10:45 "bez stalego -4%,
w wojnie na kredyt splacany z lupow" i 10:50 "wielcy jency"). Wersja 2 = wersja 1 + 51 uwag krytyki (rozdz. 7) + te dwie decyzje.

Kod: Armoury `noc2\sklad8` (0e17915 = sklad7 + 174b + grupa11, przed wgraniem 10; pliki ekonomii takie same jak w grze, poza K1 w SoldierPay),
CrashScribe `noc2\a175cs`, RealisticCaptivity `sklad8\RealisticCaptivity`. Skroty sciezek: **A** = `sklad8\Armoury\src`, **CS** = `a175cs\CrashScribe\src`,
**RC** = `sklad8\RealisticCaptivity\src`, **gra** = dekompilacja 1.4.8 `ore-supply\cs`, **BK** = `ore-supply\bk`, **Naval** = `dzien-2\k7\dec\naval`,
**Dip** = dekompilacja Diplomacy `scratchpad\tryb\dip`.

Oznaczenia: **[K]** kod plik:linia, **[P]** pomiar z logu, **[S]** szacunek/rachunek, **[H]** historia, **[D]** decyzja Jeffa (data, godzina).
**D** = dochod staly rodu (definicja W-1 z audytu 12 z poprawkami nizej, paczka 166); **D169** = to, co dzis liczy ksiega 169;
**G** = kiesy rodziny (glowa + dorosli czlonkowie); **R** = rezerwa wojny = max(20 000; 20 x D) + 5 000 x liczba doroslych czlonkow; **KW** = kredyt wojenny (168).
1 zl = 1 pens; rok gry = 364 doby. **Bieg bazowy** = autotest 120 dob na sklad8 + wgranie 10 i 11, bez paczek etapu 2 (krok A) - od niego licza sie progi "wobec dzis".

---

## 0. Dla Jeffa

1. Dzis po roku gry 72 rody bankrutuja, a 88 glow rodow ma ponizej 5 000 zl, bo lordowie AI trzymaja wojsko bez zwiazku z dochodem, a korona oddaje polowe zoldu z zapasu, ktory sie konczy (wplywy ok. 151 tys. zl dziennie, zwrot ok. 321 tys.).
2. Lord AI trzyma tyle wojska, na ile go stac z ziemi i od korony, a w wojnie - jak chciales o 10:45 - moze isc ponad dochod na kredyt wojenny w Banku z Braavos, ale tylko do granicy, ktora splaci z lupow, okupow i ziemi; po wojnie splaca i odsyla nadwyzke ludzi do domu, wiec stalego "-4% wojska" nie ma.
3. W pokoju zalogi AI sa o polowe mniejsze, w wojnie pelne jak dzis; zwolnieni wracaja do wsi, nikt nie znika.
4. Korona oddaje polowe zoldu wojska w polu z biezacych podatkow i z zapasu oddawanego powoli (starcza na ok. 2 lata), czego nie ma - tego nie oddaje, a reszte podatkow rozdaje panom jako renty wedlug lenn - takze Tobie, gdy masz lenno i naprawde stawiasz sie na wojne.
5. Bank pozycza tylko temu, kto odda, a zamiast bankructwa wierzyciel bierze dochod wsi dluznika az do splaty; nikt nie traci lenna, a Ciebie dotyczy ta sama drabina (w ostatecznosci wyprzedaz nadwyzek zbrojowni, karawan i warsztatow - nigdy Twojego ekwipunku).
6. Okupy wedlug majatku, jedna regula dla Ciebie i AI: glowa rodu pol roku dochodu, kazdy inny lord ok. 2 miesiace, gotowka z nadwyzki, reszta na raty; pojmanego krola albo nastepce tronu przejmuje korona zdobywcy, a zdobywca dostaje od razu 1/10 okupu; z pozostalych okupow i z lupu wasali korona bierze 1/9 - to bedzie Twoj duzy dochod (np. 3 glowy panow zamkow i 1 glowa pana miasta to ok. 1.7 mln zl, w tym ok. 0.3 mln od razu).
7. Rycerze bez lenna jada w druzynie swojego pana (24 zl dziennie), Polnoc karmi Straz, Wolne Miasta placa Dothrakom, najemnicy AI maja staly kontrakt od korony, a dezercja wedlug poziomu dziala takze u AI.
8. Biedne z lore krolestwa (Zelazne Wyspy, Smocza Skala, Sarnor) beda slabsze; reszta swiata ma w wojnie wojsko w druzynach lordow 95-115 tys. - to zamiast 85-100 tys. z planu, bo tyle wynika z Twojej decyzji 05.10 "ok. 108 tys." (85 tys. zostaje twarda podloga testu).
9. "0 bankructw" i "najwyzej 10 biednych glow" to cel, ktory sprawdzi autotest, a nie wynik rachunku - krytyka pokazala, ze trzeba bylo poprawic dwie rzeczy: rodzina dopelnia kiese glowy, a kolejny okup nie trzyma lorda w niewoli latami.
10. Pokoj z biedy, ktory jest juz w grze, nie dziala, bo nikt nie sklada wnioskow o pokoj - naprawiamy to na poczatku etapu.
11. Statki rozbitych druzyn (zloto z niczego) zamykamy dopiero w etapie 3, bo dzis ok. polowa wojennego zoldu panow zamkow stoi na tym zlocie i bez wyrownania ich wojsko spadloby o ok. 1/3.
12. Wgrywamy w szesciu krokach, kazdy po autotescie (pomiary; kasy zamkow i wsi; korona z budzetem rodu; renty; rycerze i dezercja; dlugi i okupy), potem proba zimy i test na nowej kampanii przez dwa lata gry - pytam tylko o trzy drobiazgi (rozdz. 5).

---

## 1. Cel i warunek "skonczone, gdy"

**Cel (PLAN, etap 2):** lord trzyma tyle wojska, na ile go stac; korona placi z biezacych podatkow; Bank pozycza tylko temu, kto odda; zamiast bankructwa
zajecie dochodu.

**Warunek z PLAN** (nowa kampania 120 dob, potem rok): glow rodow ponizej 5 000 zl najwyzej 10 z ok. 310; 0 bankructw, takze u Dothrakow - zamiast nich
najwyzej 5 zajec dochodu rocznie (na starym zapisie jednorazowo ok. 7); zaden skarbiec w wojnie ponizej 0.25 mln dluzej niz 28 dob; zalegly zold najwyzej 2%.

**Jak mierzymy** (zeby warunek byl jednoznaczny; kazda miara to kolumna `budzet-rodow.csv` albo linia logu, progi czyta `tools/sprawdz_logi.py --grupa etap2` z krokiem A):

| Miara | Definicja w logu | Dzis T9 d120 / po roku [P] | Prog |
|---|---|---|---|
| glowy < 5 000 | linia "Budzet rodow", glowa rodu AI; bez dworzan BK ("Courtiers of") i bez rodow nieumarlych (Inni, `ROTclan_126` "Others" - kiesa zawsze 0) | 32 (31 bez Innych) / 88 | <= 10 w dobie 120, 364 i 728 |
| bankructwo | rod AI z kiesa glowy 0 i zoldem przycietym z braku w kiesie przez >= 7 dob z rzedu (kiesa bohatera nie bywa ujemna - gra `Hero.cs:771`); do wgrania 168 - takze bankrut Banku | 15 / 72 | 0 |
| zajecie dochodu | rod wchodzi w szczebel D3 (168); D4 (wyprzedaz nadwyzek bez utraty lenna) liczy sie tu, nie jako bankructwo | - | <= 1 nowe w 120 dobach, <= 5 w roku |
| skarbiec w wojnie | "Skarbce": krolestwo w wojnie z zapasem < 0.25 mln | KP i Polnoc puste 22-26 dob / 7 pustych | 0 serii > 28 dob |
| **niedoplata korony** (po 165 skarbiec nie schodzi ponizej 0.5 mln, wiec miara skarbca przestaje cos mowic) | "Korona": 1 - zwrot dany / nalezny, srednia 28 dob, na krolestwo; okno T9 doby 31-120 | 13% / 25% (swiat) | rok 1: swiat >= 65% naleznego wyplacone; rok 2 (stan ustalony): swiat >= 50%; w wojnie zadne krolestwo > 50% przez 28 dob (poza lista biednych z lore) |
| zalegly zold w zlocie | "Zold": przyciete z braku w kiesie glowy / naliczone | 3.6% / 4.0% | <= 2% |
| zalegly zold w partiach | "Budzet rodow": partie z `HasUnpaidWages` / rozliczone (A `MoneyLedger.cs:109-110, 553`) | 3.7% / 9.2% | <= 2% |

**Progi dodatkowe** (bez nich warunek PLAN mozna spelnic, psujac gre). "Wobec dzis" = wobec biegu bazowego (krok A), na krolestwo, srednia 28 dob.
Progi pokoju mierzy osobny bieg z **wymuszonym pokojem** (E): w dobie 60 pokoj we wszystkich wojnach niefabularnych, potem 60 dob pomiaru - w zwyklym biegu
po 30. dobie w pokoju jest tylko 4-20 rodow z ok. 315 (T9: wojna 19 rodow w dobie 1, 307 w 31, 338 w 120 [P]).

| Obszar | Prog | Dzis [P] | Skad |
|---|---|---|---|
| wojsko w druzynach lordow, wojna | **95-115 tys.** (decyzja 05.10 "ok. 108 tys." +-10%); twarda podloga 85 tys. z PLAN | 110.5 tys. (T9 d120), 106 tys. (grupa11 d40) | [D] 05.10, [D] 10:45 (bez stalego spadku); informacja dla Jeffa w rozdz. 0 pkt 8 |
| wojsko w pokoju | 35-40% wojennego (ok. 33-44 tys.); bieg z wymuszonym pokojem, doby 88-120 | - | [D] 05.10; PLAN rozdz. 4 "30-40 tys." |
| po wybuchu wojny | 90% stanu wojennego w ok. 14 dob (bieg z wymuszonym pokojem: wojny wracajace po dobie 60) | - | OB 10.2, W-10 (rachunek: 13 dob) |
| krolestwa w wojnie (wobec dzis) | zadne ponizej -25% (Polnoc i Dothrakowie tez); Zelazne Wyspy, Smocza Skala, Sarnor do -35%; zadne powyzej +20% | - | Q4(a) - biedni tylko z lore; prog -15% z audytu 12 dopiero po etapie 5 |
| krolestwa w pokoju | zadne ponizej 20% swojego wojska wojennego; lista biednych z lore 15% | - | bieg z wymuszonym pokojem |
| zalogi | w wojnie >= 95% dzisiejszych (na krolestwo); w pokoju 45-50% wojennych | 78.3 tys. (T9 d120) | [D] 08.10 "w pokoju o polowe, w wojnie pelne" (OB 14.1 pkt 3) |
| panowie zamkow | w pokoju zold <= 0.3 D; KW <= 0.4 D dziennie; wojsko panow zamkow w wojnie >= 90% dzisiejszego | zold 0.81-0.88 D169 | 12 rozdz. 5.7, [D] 10:45 |
| najemnicy AI | ludzie w wojnie i w pokoju stali +-10% wobec umowy | 1 885 w wojnie | D-6 |
| gracz | z 1 zl zoldu (partia w polu i zaloga) wraca mu kazda droga razem < 1 (linia "Obieg", `gracz2.py` przed C1) | - | Z8 |
| Bank | wolny kapital >= 1 mln w kazdej dobie; po roku 3-6 mln | 1.88 mln (d120), 1.11 mln (rok) | OB 10.2 (4-6 mln) zmienione o KW |
| pieniadz swiata | B: zmiana tempa wobec biegu bez B = suma zamknietych ujsc (regulator zamkow + utarg wsi, oba w logu) +-30 tys./dobe; C1-C3 i D: +-50 tys./dobe wobec biegu bez paczki (paczki tylko przenosza pieniadze) | bieg bazowy - krok A (sklad6/174b/grupa11/sklad8 w dobach 31-40: +214..+355 tys./dobe; T9 w tym samym oknie +102 tys., w dobach 31-120 -52 tys.) | zamknieta ekonomia; 12 rozdz. 4.4 (liczba -41 tys. byla sprzed zamkniecia ujsc BEE 08.10 23:40 - B18) |
| bandy po 112 | sredni tier, zloto, awanse u pasera, rozbite tabory dziennie: najwyzej +20% wobec biegu bez 112 | - | S16, 15 rozdz. 5 |
| okupy | lordow w niewoli > 60 dni najwyzej dzis + 20%; suma rat rodu <= 15% D; rody z dlugiem okupow na pulapie (5.2) <= 5%; zadne krolestwo +20% wojska ani ponad 85% wygranych bitew lordow wobec biegu bez 178 (kula sniezna, prog etapu 1) | brak licznika - 169c | 04 W2, S4, PLAN etap 1 |
| dezercja AI | z morale i z zaleglego zoldu razem najwyzej dzis + 50% (ok. 30 ludzi/dobe w swiecie) | gra ok. 16/dobe, WarLedger ok. 5/dobe | S19 |
| zamknietosc | dwor skasowany przez regulator kas 0 zl; przelewy "bez odbiorcy" 0 zl | - | Z1 (nic w nicosc) |
| bledy | kazde wgranie: 0 bledow naszych modow w autotescie 40 dob i na zapisie z doby 362 | - | PLAN |

---

## 2. Paczki etapu 2 w kolejnosci

Podrozdzialy nazwane numerem paczki; krok wgrania w tytule; liczby w nawiasach = pozycje PLAN (2.1-2.16). Kazda paczka: regula, liczby z uzasadnieniem,
platnik -> odbiorca, kod (plik:linia), wylaczniki, skutek dla gracza i AI, test.

### 2.0 Zestawienie

| Krok | Paczka | Pozycja PLAN | Co (slowami gracza) | Wylacznik MCM (glowny) | Zalezy od | Test |
|---|---|---|---|---|---|---|
| A | **169c** pomiary, D staly w logu, bieg bazowy, narzedzie progow | 2.1 | gra bez zmian; nowe linie: niewola i okupy, D staly w 8 czesciach, przyrost zalog, kasy miast, wydatki rycerzy, ludnosc BK, dezercja wedlug przyczyny | `ClanIncomeBookEnabled` (jest) | wgranie 10/11 | 40 dob + zapis 362; bieg bazowy 120 dob |
| A | **2.6** powinnosci bez przerwanej petli; kazdy krok korony we wlasnym `try` | 2.6 | blad: jeden wyjatek konczy pobor u wszystkich | - | - | j.w. |
| A | **E1b** pokoj z biedy naprawdy | 2.15 | biedne krolestwo sklada wniosek o pokoj | `PovertyPeace` (CS, jest) | - | zapis 362 + 40 dob; po C3 120 dob |
| A | **2.14** Twoj okup do porywacza; kurier bez dosypki | 2.14 | dwa przeplywy z/w nicosc | `PlayerRansomToCaptor`, `RansomCourierNoTopUp` | - | harness niewoli w autotescie |
| B | **110 + 112 + klucz 114** | 2.2, 2.11 | zamek nie kasuje kasy, 2/3 nadwyzki panu, 1/3 koronie; wies nie gubi 15% utargu | klucze z galezi | A | 40 + 120 dob |
| C1 | **165** korona z biezacych wplywow | 2.3, 2.6 | zwrot z podatkow dnia i powoli oddawanego zapasu, reparacje korona-korona, clo raz | `CrownCurrentIncome` | B, rachunek E2 | 40 dob |
| C1 | **166** budzet rodu (z wylaczeniem dezercji gry z limitu zoldu) + **162m** dwor | 2.7, 2.8, czesc 2.9 | wojsko wedlug dochodu, zalogi w pokoju polowa, w wojnie pelne; dwor do kasy siedziby | `ClanBudgetEnabled`, `HouseholdMinimal` | 165, 169c | j.w. |
| C1 | **185** kontrakt najemnika AI | 2.10 | staly kontrakt od korony zamiast zlota "za tier" i kontraktu gry | `MercContractEnabled` | 165 | j.w. |
| C1 | **182** dary, Straz bez zoldu, biedne krolestwa | 2.4, 2.5 | Polnoc karmi Straz, Wolne Miasta placa Dothrakom | `CrownGifts`, `WatchUnpaid` | 165, 166 | j.w. |
| C2 | **180** renty korony wedlug lenn | 2.3 | reszta podatkow do panow wedlug lenn, za sluzbe | `CrownRents` | C1 | 40 dob |
| C3 | **179** rycerze bez lenna bez oddzialow | 2.7 | rycerz jedzie w druzynie pana | `GentryNoParties` | 166 | 40 dob, potem 120 dob calego C |
| C3 | **183** dezercja AI wedlug poziomu | 2.9 | dezercja z morale takze u AI; podloga 30 ludzi przy zaleglym zoldzie | `DesertionLawForAi` (jest) | 166 | j.w. |
| D | **168** dlug, kredyt wojenny i zajecie | 2.12 | Bank pozycza na zdolnosc splaty, takze na wojne; zajecie dochodu zamiast bankructwa | `DebtLadderEnabled`, `WarCredit` | 166 | 40 + 120 dob |
| D | **178** okupy wedlug majatku, wielcy jency, prawo trzecich | 2.13, 2.14 | jedna regula okupu, raty, krol do korony zdobywcy, korona 1/9 | `LordRansomByIncome`, `CrownThirds` | 168, 169c | j.w. + harness |
| - | proba zimy (1.14), potem **2.16** | 2.16 | zima RBL nie dubluje naszej | - | 1.14 | test roczny |
| koniec | **test etapu 2** | - | nowa kampania 364 doby + kontynuacja do doby 728; bieg z wymuszonym pokojem; zapis 362 | - | wszystko wyzej | 2 lata gry |
| etap 3 | **181** statki rozbitych do portu | (3.3) | zaprojektowane tutaj, wgranie z 164a i z wyrownaniem | `ShipsToPort` | 166, 164a | etap 3 |
| etap 5 | **184** podatek wojenny korony | (5.6) | tu tylko miejsce w 165 | - | 173 | etap 5 |

Numery: 165, 166, 168 - numery projektu OB; 162m - minimalna wersja 162; 169c - trzecia czesc ksiegi 169 (169b jest w grze); **178-185 nowe**,
bez kolizji (173 kiesa ludu, 174/174b uzbrojenie, 175 armie, 176 werbunek gracza, 177 stal valyrianska; 178+ nie wystepuja w repo ani w galeziach).

### 2.0a Dlaczego taka kolejnosc

1. **Budzet przed szczelnoscia.** Rachunek audytu 12 (tab. 3.3): zamkniecie zlota z niczego (164) z korona 165, ale bez budzetu 166, daje w rok 176 rodow
   na minusie (104 ze 118 panow samych zamkow). W etapie 2 nie zamykamy zadnego zrodla "z niczego" poza malymi, ktore dostaja prawdziwego platnika
   (kontrakt najemnika zamiast "za tier", reparacje korona-korona, dosypka kuriera, czesc posrednika).
2. **165 nigdy bez 166.** Zwrot tylko z biezacych wplywow to dla rodow ok. -170 tys. zl dziennie (zwrot 321 tys. przy wplywach ok. 151 tys., T9 doby 31-120)
   bez hamulca wojska. Osobne wylaczniki, jedno wgranie (C1).
3. **Krok C w trzech czesciach** (R13): C1 = 165 + 166 + 162m + 185 + 182 - to, co musi wejsc razem (korona bez budzetu i budzet bez korony psuja rody;
   182 w C1, bo budzet bez daru tnie Straz i Dothrakow juz w tescie C1); C2 = 180 (renty - jedna zmiana, latwa do odczytu w "Korona"); C3 = 179 + 183
   (wojsko i ludzie, bez pieniedzy korony). Kazda czesc: autotest 40 dob + zapis 362; po C3 120 dob calego C.
4. **168 po 166** (limit, rata i kredyt wojenny z D stalego), **178 po 168** (dlug okupu siedzi w tej samej ksiedze). Miedzy C1 a D wojsko AI w wojnie moze
   byc ok. 4% mniejsze (budzet bez kredytu wojennego) - to tylko stan testow, Jeff nie gra do konca projektu ([D] 08:05).
5. **Pomiar przed zmiana:** D staly najpierw jako kolumna w logu obok D169 (krok A), przyrost zalog, czestosc pojman, wydatki rycerzy i dezercja zmierzone
   przed 166, 178, 179 i 183; bieg bazowy 120 dob na sklad8 (+10, +11) - od niego licza sie wszystkie progi "wobec dzis" i "Pieniadz swiata".
6. **B przed C:** 110 i 112 tylko dokladaja panom (zamek +270-310 zl/dobe, wies +130) i nie zaleza od budzetu; zawor zamku jest w logu, zanim D staly
   zacznie odejmowac z niego wlasne pieniadze rodu (W-1).
7. **Warunek wejscia C1** (M11, S21): rachunek E2 powtorzony na logu biegu bazowego z: miara PLAN (glowy < 5 000 w dobach 120 i 364, nie rodziny), regulatorem
   kas miast dla dworu 162m, czestoscia pojman z 169c i ratami okupow 178, ratami 168 i kredytem wojennym, utrata lenn, konnymi Dothrakami 175 (zold +26-49%),
   Straza bez zoldu z pulapem w ludziach, 1/9, warunkiem sluzby rent, dezercja C3 i rokiem 2 (zapas korony prawie zuzyty). Jesli E2 da > 0 rodow na minusie
   albo glowy < 5 000 > 10 - poprawic parametry przed kodem.
8. **Kazde wgranie:** autotest 40 dob + zapis z doby 362 (0 bledow); po C3 i po D 120 dob; na koncu test dwuletni na nowej kampanii po probie zimy
   ([D] 08:05: Jeff gra dopiero po skonczeniu projektu, nowa kampania - pozycje "tylko nowa kampania" wlaczamy domyslnie).
9. **Kolejnosc wobec OB rozdz. 12** (zatwierdzonej 08.10): zmieniona w PLAN 09.10 (04:00; Jeff 04:15 "zgoda na cala prace", 04:25 odpowiedzi na pytania PLANU) -
   111' i 162 pelny przeszly do etapu 5 razem z kiesa ludu; skutki tego rozdzielenia dla dworu - paczka 166 (162m, tarcza dworu).

### 2.0b Zasady wspolne dla wszystkich paczek

- **Nic z niczego, nic w nicosc** ([D] zamknieta ekonomia): w kazdej paczce rozpisany platnik i odbiorca; kazdy przelew przez `GiveGoldAction`
  albo `ChangeHeroGold` w parze, z licznikiem w "Obieg" i w "Pieniadz swiata". **Zapasowy odbiorca**, gdy odbiorcy juz nie ma (partia rozwiazana, rod wymarly,
  rod bez wsi): kiesa glowy rodu-odbiorcy, potem nowy pan jego wsi (jak w 168), potem kasa najblizszej osady; licznik "bez odbiorcy" w "Obieg" z progiem 0.
- **Jedna regula gracz/AI**: gracz placi koronie jak wasal (jest), dostaje zwrot i renty na tych samych warunkach, placi 1/9, okupy, Bank i drabina dlugu
  wedlug tych samych wzorow. Budzet 166 i dwor dotycza tylko AI (gracz sam decyduje o wojsku; [D] 08.10 "dwor graczowi nie"); kontrakt najemnika 185 tylko AI
  (pozycja PLAN 2.10 "kontrakt najemnika AI"; kontrakt gry gracza ma prawdziwego platnika - wasali).
- **Z8** - nikt nie zarabia na wlasnym wydatku: renty wedlug stalych wag lenn (nigdy wedlug zalogi), zwrot bez zalog i bez wydatkow wlasnych ludzi we wlasnych
  osadach, dar 182 wedlug stalych wag (nigdy wedlug liczby ludzi), D bez wlasnych pieniedzy wracajacych zaworem **kazdej** osady rodu (zamek i miasto).
- **Wylacznik = stan sprzed paczki**; ustawienia czytane przy kazdym zdarzeniu; nowe klucze sprawdzone w `Armoury.json` Jeffa przed wgraniem (pulapka M13 -
  plik Jeffa nie ma kluczy Banku, korony i dezercji, dzialaja domyslne z `Settings.cs`).
- **Haki kiesy ludu** (KL, etap 5) od razu w kodzie, z wartoscia 0: 110 "15% zaworu dla podzamcza", 112 "renta ustepuje oplatom", 162 "80/20 lud";
  D staly rozszerzalny o nowe zrodla (podatki ludu, oplaty wsi).
- **Kolejnosc dobowa** (A `ArmouryBehavior.cs:1313-1354`, dzis: WarLedger -> PopulationLaw -> KingdomTreasury.Daily/Levies/WageRefund + KingdomLedger w jednym
  `try` (`:1348`) -> SoldierPay -> IronBank -> ClanIncomeBook): po C kazdy krok korony we **wlasnym `try`** (zasada wpisu 86): renty wsi i miast -> powinnosci,
  danina, clo, 1/9 z licznikow doby (178) -> dary 182 -> raty reparacji 165 -> kontrakty 185 -> nagroda za wielkiego jenca (178) -> zwrot 165 -> renty 180
  -> SoldierPay -> Bank 168 (raty, KW) -> budzet 166 (na D z wczoraj, czesc "ziemia" z dzisiejszych lenn) -> ksiega 169.

---

### 169c: pomiary etapu 2, D staly w logu, bieg bazowy (krok A; 2.1)

**Regula.** Gra bez zmian. Nowe linie i kolumny, bez ktorych 166, 168, 178, 179 i 183 nie maja podstawy ani progu.

| Linia / kolumna | Co | Po co |
|---|---|---|
| "Niewola lordow i okupy" (04 N8, 05 A4) | lordowie w niewoli wedlug rangi (krol, nastepca tronu, glowa, lord), **pojmania na dobe i na rod w roku**, dni w niewoli (mediana, > 60 dni), okupy AI-AI (kwota, kto komu - z barteru gry), okupy gracza i **okupy gracza / D gracza**, dosypka kuriera (L4), czesc posrednika z niczego (L5) | prog i rachunek 178 (T9 d120: w niewoli naraz 16-20 glow - "na okup glowy"; czestosci pojman nie znamy) |
| D staly w `budzet-rodow.csv` obok D169 - **8 czesci** | **ziemia**: podatek wsi (licznik BK "Village Demesnes"), renta wsi, zawor miast, cla, zawor zamkow (po B) - bez czesci "wlasne" (nizej), liczona z **dzisiejszych** lenn (pierscien 28 dob na osade, nie na rod) i bez dochodu zajetego w D3; **korona**: zwrot, mennica i monopole (po C: renty, dary); **kontrakt** (po C); **majatek**: warsztaty i zysk karawan rodu (prawdziwy platnik - handel); osobno, poza D: **jednorazowe** (lup, jency, statki, trzecia, zdarzenia spoza rodu), **wlasne** (to, co rod wplacil wczoraj do kasy kazdej swojej osady - zamku i miasta: zold i sprzet zalogi, dwor, wydatki swoich ludzi - x udzial pana w zaworze x odplyw), **inne z modelu** (majatki BK, "za tier"), **przelewy w rodzie** (10% kies partii syna do ojca - 141 zl/dobe u pana zamku) i **podwojne** (majatek BK liczony dwa razy - 12 B12) | W-1, M1: D169 liczy 31% zdarzen i 23% zwrotu z zapasu; u panow samych zamkow ziemia 475 zl przy D169 2 152 [R] |
| "Zalogi: przyrost bez werbunku" | przyrost zalog wedlug drogi: werbunek gry, milicja/BK, ROT, przeniesienie z partii, jency | M17: zalogi +66% w 120 dob; czy pulap 166 (`SetGarrisonWagePaymentLimit`) to zatrzyma |
| "Kasy miast" (PLAN 2.1) | na miasto: kasa wobec celu regulatora gry, skasowane przez regulator, pod tarcza (zold, potem dwor), zawor do pana | 162m (dwor do kasy miasta), Z1 |
| "Wydatki rycerzy" | rody gentry: wydatki wedlug drogi (BK zaopatrzenie wsi majatku - BK `BKVillageSupplyAutoBehavior.cs:349`, niewolnicy majatku - BK `BKEstateAutoSlavePurchaseBehavior.cs:106`, inne) | 179: rycerze bez wojska traca ok. 116 zl/dobe (37 rodow, T9 doby 40-120), np. Fyomanoving 39 388 -> 3 994 przy zoldzie 0 [P] |
| "Dezercja AI wedlug przyczyny" | morale (DesertionLaw), zalegly zold (WarLedger), limit zoldu (gra), glod | baza 183 (dzis ok. 20/dobe) |
| "Ludnosc BK" (KL pomiar A) | ludnosc wedlug klas i krolestw, `ConsumedValue`, budzet konsumpcji, wartosc wyrobow | warunek kiesy ludu (etap 5) |
| "Sluby AI", towar wedrowcow BK, wzrost Innych, miara historyczna cz. 2 | jak w PLAN 2.1 | PLAN 2.1 |
| "Korona" (jest) + niedoplata na krolestwo z 28 dob | 1 - zwrot dany / nalezny | wejscie E1b i prog etapu |

**Bieg bazowy** (krok A, po wgraniu 169c): autotest 120 dob + zapis 362 na sklad8 + 10 + 11 - baza "Pieniadza swiata" (dzisiejsza liczba -41 tys./dobe pochodzi
z T9 sprzed zamkniecia ujsc BEE), wojska i zalog na krolestwo, dezercji AI i pojman. **Narzedzie progow:** `tools/sprawdz_logi.py --grupa etap2` + plik bazowy
(bieg bazowy na krolestwo) - kazdy prog z rozdz. 1 jako jedna regula odczytu (M12).

**Liczby.** Koszt logu: najwyzej +3% czasu doby (dzis 13.1 s/dobe w 40 dobach); CSV +12 kolumn. Rody nieumarlych (Inni) poza linia "Budzet rodow" i CSV.

**Platnik -> odbiorca.** Brak przeplywow (tylko liczniki).

**Kod.** A `ClanIncomeBook.cs:290-301` (wplyw doby: `inflow = a + b + r.Today`, gdzie `a` = dochod modelu z warsztatami, karawanami, majatkami BK i "za tier" -
dopisac rozbicie na 8 czesci; `inflow` D169 zostaje do porownania), `:562-579` i `:641-643` (CSV i naglowek), `:591-638` (linie); `MoneyLedger.Obieg.cs`
(zalogi, dezercja, kasy miast, "bez odbiorcy"); A `SoldierPay.cs:456-470` (tarcza - licznik skasowanego); RC `FairRansom.cs:117-127` (log tylko dla lordow gracza -
dopisac AI-AI) albo nasluch barteru gry w 169; CS (sluby, Inni). D staly jako nowa funkcja obok `StableIncome` (`ClanIncomeBook.cs:224`) - API dla 166/168/178.

**Wylacznik.** `ClanIncomeBookEnabled` (A `Settings.cs:434`) + `ClanIncomeBookStableD` (kolumny D stalego).

**Skutek.** Gracz i AI: brak.

**Test.** 40 dob + zapis 362: 0 bledow; wszystkie linie obecne; D staly pana zamku w dobie 40 w przedziale 400-1 300; **zamkniecie sumy**: ziemia + wlasne + korona +
kontrakt + majatek + jednorazowe + inne z modelu + przelewy w rodzie - podwojne = D169 co do 1%; bieg bazowy 120 dob zapisany jako plik bazowy narzedzia.

### 2.6: powinnosci bez przerwanej petli (krok A)

**Regula.** Wyjatek przy jednym rodzie nie przerywa poboru u nastepnych. Podstawa powinnosci zostaje do C1 (dochod modelu + renty), w C1 przechodzi na
**czesc "ziemia" D stalego** (bez pieniedzy od korony - korona nie bierze 3% z wlasnego zwrotu i rent; klucz 165 `CrownDuesFromLand`).
Stawki bez zmian: 2% pokoj, 3% wojna ([H] tarczowe 1-3 grzywny = 3-10% dochodu lenna; 3% to dolna granica - zgodne).
Od razu: kazdy krok korony w `ArmouryBehavior` we wlasnym `try` (dzis `KingdomTreasury.Daily(); Levies(); WageRefund(); KingdomLedger.Daily();` w jednym
`try`, A `ArmouryBehavior.cs:1348` - wyjatek w jednym kroku zatrzymuje reszte rozliczenia korony; po C bedzie tam 6 nowych krokow).

**Kod.** A `KingdomTreasury.cs:51-84` (jeden `try` na cala petle -> `try` na rod, licznik potkniec w linii "Korona"); komentarz `:45` "wojna 10%" -> 3%
(`Settings.cs:646`); A `ArmouryBehavior.cs:1348` (rozbicie na osobne `try`). **Skutek:** tylko w razie bledu. **Test:** w linii "Korona" potkniecia 0
i "rodow pominietych przez blad 0".

### E1b: pokoj z biedy, ktory dziala (krok A; 2.15)

**Stan.** E1 (CS `PovertyPeace.cs`, w grze od wgrania 8) zmienia tylko glos rodu i ocene wypowiedzenia wojny. Po roku 9 krolestw w biedzie i 91 rodow
"za pokojem", a w 9 dobach 0 glosowan o pokoj - wniosek o pokoj nie powstaje (prog BK `GetScoreOfDeclaringPeace >= 0` w `ConsiderPeace`, `IsPeaceSuitable`
gry, zgody Diplomacy - CS `PovertyPeace.cs:33-36`).

**Regula.**
1. **Diagnoza w logu** (pierwsze wgranie E1b): ile razy wolane `ConsiderPeace`, ile odrzucone i ktorym warunkiem (prog BK, `IsPeaceSuitable`, cooldown
   i model zgod Diplomacy), ile wnioskow BK `ForceProposePeaceFromLosingSide`.
2. **Wniosek z biedy:** krolestwo z bieda T >= 0.5, w wojnie niefabularnej (ROT `IsWarForced` = nie), raz na 7 dob sklada wniosek o pokoj z tym wrogiem,
   z ktorym ma najgorszy wynik wojny; glosuja rody jak dzis (czlon E1 w glosie zostaje). Jeden czlon na jedno zjawisko: wniosek z biedy, glos z biedy.
3. **Miara T po 165** (skarbiec przestaje schodzic do zera, wiec "zapas w dniach ubytku" `:69-74` nic nie powie): T z niedoplaty korony n (28 dob),
   **wzgledem progu i sredniej swiata**: T = (n - max(25%; n swiata)) / 15%, przyciete do 0-1. Po 165 srednia niedoplata to ok. 25-35% w roku 1 i ok. 40-50%
   w roku 2 [S, E2]; miara bezwzgledna (10% -> 0, 33% -> 1) dawalaby T ok. 0.65 przecietnemu krolestwu i wniosek co 7 dob w kazdej wojnie. Wniosek skladaja
   krolestwa wyraznie biedniejsze od reszty i od progu etapu ([H] Esplechin 1340: niezaplaceni sojusznicy rozeszli sie). Do wgrania 165 zostaje dzisiejsza miara.

**Kod.** CS `PovertyPeace.cs:117-170` (wpiecie), `:200-233` (glos zostaje), `:249-260` (miara T - druga galaz po 165), nowy postfiks na metode wniosku
(miejsce ustalic w diagnozie: Dip `ConsiderPeace` albo BK `GetScoreOfDeclaringPeace` tylko dla wywolania wniosku).

**Wylacznik.** `PovertyPeace` (CS `Config.cs:64`) + `PovertyPeaceProposals`, `PovertyPeaceArrearsMeasure`.

**Skutek.** AI: biedne krolestwa koncza wojny niefabularne szybciej. Gracz: jego krolestwo (gdy nie jest krolem) moze glosowac o pokoj z biedy; gdy jest
krolem - wnioski AI widzi jak inne.

**Test.** Zapis 362 (9 krolestw w biedzie): w 9 dobach co najmniej 1 glosowanie o pokoj w krolestwie z T = 1; 0 pokojow w wojnach fabularnych ROT.
Nowa kampania 40 dob: decyzji "z biedy" <= 2. **Po C3 (120 dob, miara po 165):** wnioskow z biedy na krolestwo <= 1 na 28 dob; pokojow z biedy w swiecie 1-6;
0 pokojow w wojnach fabularnych.

### 2.14: Twoj okup do porywacza, kurier bez dosypki (krok A)

**Regula.** (W3) Okup gracza z menu gry trafia do porywacza: wodz partii porywacza; partia bez wodza - glowa jej rodu; loch osady - pan osady; banda - kasa
jej kryjowki; zapasowy odbiorca jak w 2.0b. Kwota bez zmian do 178. (L4) Kurier: gdy placacy AI nie ma calej ceny w dniu przyjecia, gra nie dosypuje mu zlota
(dzis cena + 1 000 z niczego) - oferta przepada, jeniec zostaje; od 178 placacy placi gotowke i raty.

**Platnik -> odbiorca.** Gracz -> porywacz (dzis -> nikt); placacy AI -> gracz (bez dosypki).

**Kod.** gra `PlayerCaptivityCampaignBehavior.cs:242-246` (prefiks na `game_menu_captivity_end_by_ransom_on_consequence`, `:244` `GiveGoldAction(MainHero, null)`);
gra `RansomOfferCampaignBehavior.cs:168-180` (prefiks na `AcceptRansomOffer`, `:176`).

**Skutek.** Gracz placi tyle samo, ale pieniadz nie znika; rzadziej (przy biedzie placacego) przepada oferta okupu za jego jenca.

**Test - harness niewoli w autotescie** (Jeff nie gra do konca projektu, wiec "reczny test Jeffa" przesuwalby sprawdzenie do jego koncowej kampanii):
(1) w dobie 5 `TakePrisonerAction` na `MainHero` do partii lorda AI, w dobie 6 wywolanie konsekwencji menu okupu - w logu kwota, porywacz, kiesa gracza
i porywacza w parze, 0 zl w nicosc; (2) w dobie 8 lord AI wroga jako jeniec w partii gracza, wywolanie `AcceptRansomOffer` - placacy, kwota, 0 zl z niczego,
przy pustej kiesie placacego oferta przepada. Po 178 ten sam harness sprawdza gotowke, raty i 1/9. Reczny test zostaje jako dodatek.

---

### 110 + 112 + klucz 114 (krok B; 2.2, 2.11)

**Regula.**
- **110 zawor zamku** (D-1, Z15-1): regulator gry zeruje kase zamku **tylko w dol** (kasowanie nadwyzki = 0, dosypka do zapasu zostaje jako tryb 1, liczona - Z9);
  "zakupy z niczego" w zamku cofniete; 7% nadwyzki ponad zapas kupcow zamku dziennie wychodzi z kasy.
- **klucz 114:** z zaworu zamku 2/3 dla pana, 1/3 do skarbca krolestwa (`CastleDuesSplitWithCrown`, `CastleDuesLordShare` 0.67). Z paczki 114 tylko klucz i poprawki
  ksiegi - podzial przez wspolny pomocnik bez `TownPurse.Split` (114 wymagalo 111, ktora jest w etapie 5 - S15).
- **112 utarg wsi** (D-2, Z15-2): pan bierze podatek wedlug dekretu, cala reszta utargu zostaje w kiesie wsi (dzis 15% znika); zaplata lorda za zywnosc kupiona
  we wsi zostaje (dzis 100% w nicosc); sakwa rozbitego taboru do zwyciezcy (decyzja D - [D] 04:25), rozwiazanego - do wsi.

**Liczby i uzasadnienie.**
- Zamek: regulator kasuje dzis ok. 105 tys./dobe (doplyw prawdziwy: zold zalog 60 tys., sprzet AI 23 tys.) [P]. Po 110 pan zamku +270-310 zl/dobe, w ok. 90% to
  jego wlasny zold zalogi i dwor - zaloga w zamku kosztuje go ok. 63% zoldu zamiast 100% [S, 12 rozdz. 3.2]. 7% = stawka renty miast (jedna zasada).
- 2/3 : 1/3 - jak zawor miast (K6); Z8 gracza: z 1 zl zoldu zalogi w zamku wraca 0.66-0.70 [S]. Pelne 100% dla pana (110 jak zbudowana) dalo 0.84-0.88 i mniej dla korony.
- Wies: +47 tys./dobe do kies wsi -> renta ok. +65 zl na wies dziennie, pan zamku ok. +130 [S].
- Dosypka trybu 1 do zapasu zamkow ok. 3.7 tys./dobe - zostaje i jest liczona, dopoki reszta niezmierzona swiata nie spadnie ponizej 10 tys./dobe (Z9, etap 3).
- Dar startowy kas zamkow przyciety w 1. dobie (NK - nowa kampania).
- Pieniadz swiata: B zamyka dwa ujscia (regulator zamkow ok. 105 tys., utarg wsi ok. 41 tys. + zaplata lorda za zywnosc), zrodla zostaja - swiat rosnie o tyle
  szybciej niz w biegu bazowym. To swiadomy stan do etapu 3 (R10).

**Platnik -> odbiorca.** Kasa zamku (zold zalogi, sprzet, dwor) -> pan 2/3, skarbiec 1/3. Kasa miasta (zakup plonu) -> tabor -> pan (podatek dekretu) i kiesa wsi
(reszta) -> pan (renta 20%/dobe). Sakwa taboru -> zwyciezca (lord, banda) albo wies.

**Kod.** Galezie `paczki/110-k5-kasa-zamku` (39f5bdf), `paczki/112-k7-utarg-wsi`, `paczki/114-porzadki` (a14efe8), przeniesione na sklad8+10+11. Zmiana wobec galezi
110: regulator w dol (S14). Konflikty (M2): `ArmouryBehavior` (konstruktor, `OnDailyTick`), `SubModuleMain.ApplyAll`, `Settings`/`McmSettings` (gen_mcm),
`MoneyLedger.Obieg.cs` (po 169), `MarketRoad` po 171/174b (`CastleCartsNeedCoin` a `GarrisonCarts`), `SoldierPay` po K1. Opis A `PopulationLaw.cs:26-28`
("Zamki bez zmian") dotyczy dochodu, nie kasy - kase zamku zeruje regulator gry (`DefaultSettlementEconomyModel.GetTownGoldChange`, opisany w A `SoldierPay.cs:457-459`);
w opisie wskazac regulator i galaz 110 (`CastlePurse`). Haki KL: `CastleDuesSuburbShare` = 0 (110), `VillageRentYieldsToFees` = false (112).

**Wylaczniki.** Klucze z galezi (110: wlacznik kasy zamku; 114: `CastleDuesSplitWithCrown`, `CastleDuesLordShare`; 112: `VillageTakingsWhole`,
`VillageFoodSalesKept`, `VillagerPurseSurvives`).

**Skutek.** Gracz: jego zamki daja mu 2/3 nadwyzki kasy, 1/3 idzie do skarbca (jak wasal AI); jego wsie wiecej renty; rozbity tabor daje cala sakwe.
AI: panowie zamkow +400-440 zl/dobe razem; bandy bogatsze z taborow (77% taborow rozbijaja bandy - 15 Z15-3).

**Test (40 + 120 dob).** "Zawor zamkow": kasowanie 0, dosypka trybu 1 w logu (ok. 3.7 tys./dobe); pan zamku: wplyw z zaworu 200-350 zl/dobe; "Pieniadz swiata":
zmiana tempa wobec biegu bazowego = suma zamknietych ujsc z logu (regulator zamkow + utarg wsi) +-30 tys./dobe; bandy: najwyzej +20% (S16); zakupy plonu przez
miasta >= 95% (bez zmian); 0 bledow.

---

### 165: korona z biezacych wplywow (krok C1; 2.3, 2.6)

**Regula.**
1. **Wplywy dnia** = powinnosci, danina wojenna, clo, 1/3 zaworu zamkow (114), podatek BK 0.1% ponad 100 tys., 1/9 lupu i okupow (178), raty reparacji
   i okupow krolewskich przyjete (178), **1/360 dziennie zapasu ponad `CrownReserveGold` 500 000** (zawiera sie we wplywach - nigdzie nie liczony drugi raz).
2. **Kolejnosc wydatkow:** dary (182) -> raty reparacji -> kontrakty najemnikow (185) -> nagroda za wielkiego jenca (178) -> zwrot zoldu -> renty (180, od C2).
   Do C2 reszta zostaje w skarbcu. Zapomogi nie ma (BK wylacza zapomoge gry - S6; renty robia to samo dla wszystkich).
3. **Zwrot:** 50% zoldu ([D] 05.10) **partii w polu**, bez zalog (D-5, S7: zaloga to koszt pana; wraca mu zaworem). Podstawa = zold partii minus **100%** tego, co
   jej ludzie wydali wczoraj w osadach tego samego rodu (K3, klucz 1.0). W pokoju i dla najemnikow - nic (jak dzis). Gdy wplywow nie starcza - zwrot dzielony
   proporcjonalnie do naleznego; **niedoplata przepada** ([D] 05.10 "dopoki ma z czego", PLAN 2.3 "z biezacych wplywow"). Dlug korony wobec rodow
   (wersja 1) - tylko pomysl na slowo Jeffa (rozdz. 3); luke rodu w wojnie zamyka kredyt wojenny 168 ([D] 10:45).
4. **Reparacje Diplomacy** (11 krok C): dlug korona A -> korona B; rata najwyzej 50% wplywow dnia A; B dostaje dokladnie rate (bez 1/3 krola i 1/6 najemnikow
   z niczego); rody nie placa, `DebtToKingdom` z reparacji nie powstaje.
5. **Clo bez drugiego poboru** (D-7): z licznika cel do skarbca, bez drugiego zdjecia z kasy miasta.
6. **Powinnosci** od czesci "ziemia" D stalego (2.6).
7. Miejsce na **podatek wojenny 184** (etap 5): jedna linia wplywow wiecej; do tego czasu danina wojenna 1% / 1.5% bez zmian.

**Liczby i uzasadnienie** (jedno okno: T9 doby 31-120; "dany/nalezny").
- Dzis: wplywy ok. 151 tys./dobe, zwrot dany 321 tys., nalezny 370 tys. (w tym zalogi ok. 97 tys.) [P]. Bez zalog i po 166 nalezny ok. 270-290 tys. [S].
- 500 000 rezerwy = ok. 2 miesiace zwrotu sredniego krolestwa (OB 7.1). Start nowej kampanii: 29 x ok. 2 mln, nadwyzka ok. 43.5 mln. **1/360 zamiast 1/180:**
  przy 1/180 rok 1 stal na jednorazowym zejsciu zapasu (43.5 mln x (1 - e^(-364/180)) = ok. 37.8 mln, ok. 104 tys./dobe, 40-50% wyplaconego zwrotu), a rok 2
  spadal do ok. 57% naleznego [S, 12]. Przy 1/360: rok 1 ok. 27.7 mln (ok. 76 tys./dobe, na starcie 121 tys.), rok 2 ok. 10 mln (ok. 28 tys./dobe) - zejscie
  lagodniejsze, zapas starcza na ok. 2 lata. [H] korona nie trzymala wielkich zapasow, zyla z biezacych podatkow i kredytu (Riccardi, talie asygnacyjne).
- Wyplacone [S, E2 do powtorzenia]: rok 1 ok. 65-75% naleznego; **rok 2 (stan ustalony)** ok. 55-65% (wplywy ok. 170-190 tys. = 151 + 1/3 zaworu zamkow + 1/9 -
  przy naleznym 270-290 tys.). Luke stanu ustalonego zamyka podatek wojenny 184 w etapie 5 (ok. 30-60 tys./dobe), a do tego czasu w rodzie - kredyt wojenny 168.
- 50% wplywow na rate reparacji: [H] okupy i odszkodowania placono ratami 10-30% rocznego dochodu korony; 50% dnia przy 2 prawdziwych pokojach na 120 dob
  (11) daje kilka-kilkanascie dni splaty.
- Rok wojny pana: E2 - pan zamku placi sam 0.8-1.35 D (z zapasem wojny), korona oddaje polowe zoldu partii w polu najwyzej w 75%; wojna caly rok jest
  ciezsza niz w historii (2-8 x - 12 rozdz. 5.3), bo kampanie w grze trwaja caly rok - stad kredyt wojenny i pokoj z biedy.

**Platnik -> odbiorca.**

| Moneta | Platnik | Odbiorca |
|---|---|---|
| powinnosci 2/3% | glowa rodu (gracz tez) | skarbiec |
| danina wojenna | kasa miasta (ponad 20 000), kiesa wsi | skarbiec |
| clo | licznik cel miasta | skarbiec |
| 1/3 zaworu zamku | kasa zamku | skarbiec |
| zwrot 50% | skarbiec (wplywy dnia z 1/360 zapasu) | glowa rodu albo rycerz, ktory oplacil partie |
| rata reparacji | skarbiec A | skarbiec B |
| splata `DebtToKingdom` (stary zapis: dlug z reparacji) | glowa rodu | skarbiec (dzis w nicosc); dlug z niezaplaconego zoldu - patrz 168 |

**Kod.** A `KingdomTreasury.cs:149-232` (`WageRefund`: zrodlo `:191` `have = KingdomBudgetWallet` -> wplywy dnia z 1/360 zapasu; podzial proporcjonalny;
kolejnosc), `:47-85` (powinnosci, podstawa), `:275-284` (clo: bez `ChangeGold` z kasy miasta), `:408-440` (wpiecie); A `SoldierPay.cs:380`
(`AddPaid` zalog - wylaczone kluczem) i rejestr wydatkow ludzi we wlasnych osadach (MenPurse) dla K3; Dip `GiveGoldToKingdomAction.cs:57-87, 102-132`
(prefiks: dlug korona-korona zamiast `TributeWallet` i zlota z niczego); gra `DefaultClanFinanceModel.cs:219-230` (postfiks `AddPaymentForDebts`: splata
do skarbca). Kazdy krok w osobnym `try` (2.6). Zapis gry: dlugi reparacji przez `SaveText.Sync` (limit 32 KB na napis - pamiec "Zapis: limit napisu").

**Wylaczniki.** `CrownCurrentIncome` (glowny; wylaczony = zwrot ze stanu skarbca jak dzis), `CrownReserveGold` 500000, `CrownReserveReleaseDays` 360,
`CrownWageRefundGarrisons` false (A `Settings.cs:681`), `CrownRefundOwnTownsCut` 1.0, `CrownDebtToClans` false (kod bez tej galezi do slowa Jeffa),
`CrownReparationsRealm` true, `CrownReparationShare` 0.5, `CrownCustomsSingleTake` true, `CrownDuesFromLand` true.

**Skutek.** Gracz: zwrot tylko za partie w polu (dzis tez za zalogi) - przy duzych zalogach mniej zwrotu, ale wiecej zaworu (110); reparacje placi jego
korona, nie jego kiesa; gdy jest krolem - placi i dostaje raty skarbcem. AI: zwrot staje sie wplywem, ktory budzet moze liczyc; skarbce nie pustoszeja.

**Test.** 40 dob: zwrot wyplacony >= 80% naleznego (zapas startowy); "Korona" pokazuje wplywy dnia, 1/360, reparacje, podzial proporcjonalny. 120 dob (po C3):
swiat >= 65% naleznego; zadne krolestwo w wojnie > 50% niedoplaty przez 28 dob (poza lista biednych); reparacje: suma wyplacona B = suma zaplacona A
(co do 1 zl); `DebtToKingdom` z reparacji 0. Rok 2 (doby 365-728): swiat >= 50%. Z8 gracza dla partii w polu < 1 (`gracz2.py` przed kodem, potem "Obieg").

### 166: budzet rodu + 162m dwor minimalny (krok C1; 2.7, 2.8, czesc 2.9)

**Regula** (OB 6.1 z poprawkami W-1..W-9 audytu 12 i uwagami krytyki). Raz na dobe dla kazdego rodu AI, na D z wczoraj (czesc "ziemia" z dzisiejszych lenn):

| Pozycja | Pokoj | Wojna |
|---|---|---|
| pulap zoldu (partie + zalogi + karawany rodu) | **0.28 x D** | **0.60 x D** + 0.8 x (G - R) / 45, gdy G > R; + kredyt wojenny KW (168, od kroku D) |
| zalogi w pulapie | cel 50% zalogi wojennej, najwyzej 80% pulapu | **pierwsze**: pulap zalog jak dzis w grze (`UpdateClanSettlementsPaymentLimit`), partie dostaja reszte |
| dwor i wyzywienie (najpierw jedzenie partii, reszta do kasy siedziby) | 0.35 x D | 0.20 x D |
| sprzet i werbunek (z zaopatrzeniem majatkow BK) | 0.17 x D | 0.17 x D + 0.2 x (G - R) / 45, gdy G > R |
| budowy | 0.10 x D (jak dzis) | 0 |
| powinnosci | 2% | 3% |
| reszta | do zapasu do 60 x D; ponad 120 x D + 50 000 po 1/180 na dwor i budowy | - |
| bieda | G < 60 D: udzialy x (1 - 0.1 x (1 - G / 60D)) | G < R: x (0.8 + 0.2 x G / R); **G < 0.25 R: x 0.5** |

- **D staly** (W-1, 169c): ziemia + korona + kontrakt + majatek; bez jednorazowych (ida do G i wracaja przez zapas wojny), bez "inne z modelu" (majatki BK
  i "za tier" - zrodla z niczego do etapu 3, budzet nie moze na nich stac), bez wlasnych pieniedzy wracajacych zaworem **kazdej** osady rodu (zamku i miasta;
  dla krolow i panow miast zold zalogi wlasnego miasta - ok. 130 tys./dobe w swiecie - i dwor 162m wracaja zaworem miasta, ktory w etapie 2 oddaje panu 100%).
  **Ziemia z dzisiejszych lenn:** utrata lenna, spalenie wsi albo zajecie D3 zmniejsza D od razu (pierscien 28 dob na osade, nie na rod) - w T9 5 rodow
  stracilo lenno w 120 dob, a srednia rodu trzymalaby stare D przez 4 tygodnie. Wyjatek lore: u najezdzcow (Zelazne Wyspy, Dothrakowie, Wolni Ludzie)
  lup od prawdziwego platnika liczy sie do D. Start: D(0) = G / 60.
- **R = max(20 000; 20 x D) + 5 000 x liczba doroslych czlonkow:** zapas wojny nie schodzi ponizej podlog rodziny (bez tego pan zamku z 3-4 doroslymi
  czlonkami mial w dlugiej wojnie glowe ok. 0-5 000). 20 dni D = czas, w ktorym budzet zwalnia nadwyzke po koncu wojny (2-3 tygodnie) - rod nie zostaje z wojskiem
  bez pieniedzy; 20 000 = podloga dla rodow z malym D (tydzien zoldu malej druzyny i dom). Uzasadnienie "R = okup glowy" z wersji 1 nieaktualne (okup glowy
  pana zamku to teraz ok. 182 D).
- **Kiesa rodziny najpierw** (W-4, poprawione): dorosly czlonek AI z kiesa ponad 5 000 **dopelnia kiese glowy do max(5 000; koszt dnia rodu)**, sam nie
  schodzac ponizej 5 000. Glowa siedzi ponizej 5 000 tylko wtedy, gdy cala rodzina nie ma juz z czego (T9 d120: 20 z 32 glow < 5 000 mialo rodzine >= 10 tys.,
  np. Royce 1 094 / 36 070, Greyjoy 446 / 32 876 [P]). Zastepuje T5 (`IronBankFamilyPays` = false w tym samym wgraniu - jedna regula).
- **Zwolnienia zamiast dezercji** (OB 6.2): **w tym samym wylaczniku** dezercja gry z limitu zoldu = 0 dla rodow AI z budzetem (`AiWageLimitDesertionOff`;
  dzis gra przy `TotalWage > PaymentLimit` zdejmuje do 20 ludzi na partie dziennie - gra `DefaultPartyDesertionModel.cs:50-76`, wolane przez A `DesertionLaw.cs:144-145`;
  dotyczy tez zalog, bo ich limit to `GarrisonWagePaymentLimit` - gra `GarrisonPartyComponent.cs:43`). Bez tego obnizony pulap wyrzucalby ludzi do lasu szybciej,
  niz zwolnienia odeslaliby ich do domu, i test nie mierzylby 166. Regula: zold > 1.10 x pulap przez 3 doby -> codziennie 15% nadwyzki; **najpierw zalogi
  ponad cel pokojowy** (w pokoju), potem najemnicy z karczmy, potem najnizszy tier poboru. **Dokad** (M6, do ksiegi ludzi 108 i karczm 167 z etapu 4):
  ludzie partii - do ludnosci BK najblizszej wsi rodu (BK `DismissParties` -> `UpdatePopFromSoldiers`); ludzie zalogi - do ludnosci BK wsi tej twierdzy
  (zamek bez wsi - podzamcze, czyli najblizsza wies krolestwa); sprzet do zbrojowni zalogi rodu albo na targ (`MusterOut`), czesc sakiewki partii proporcjonalnie
  do kiesy tej wsi. Nikt nie znika (Z6).
- **Zalogi w wojnie pelne** ([D] 08.10, OB 14.1 pkt 3): w wojnie budzet nie tnie zalog - pulap zalogi jak dzis w grze, finansowany pierwszy; partie
  dostaja reszte pulapu rodu. W pokoju cel 50% zalogi wojennej (srednia z ostatnich 28 dob wojny; przed pierwsza wojna - stan z 1. doby).
- **Dluznik** (OB 8.2, 05 B4, C7; stan z 168): budowy 0, dwor -50%, sprzet tylko braki; zakup karawany i warsztatu BK tylko przy G >= 60 D i bez dlugu (B5).
- **Zakupy zalog w miescie (171)** i zaopatrzenie majatkow BK (B3, B6) w pulapie sprzetu; jesli 169c pokaze, ze wydatki majatkow rycerzy (BK zaopatrzenie wsi,
  niewolnicy) nie mieszcza sie w tym pulapie - osobny pulap "majatek" 0.10 D (rycerz bez wojska nie traci kiesy szybciej, niz ma dochodu).
- **Najemnik AI:** pulap = ludzie z umowy (185). **Rycerz bez lenna:** bez wlasnej partii (179).
- **Wojsko bez zoldu (Straz, 182):** pulap 166 liczony w zlocie nie hamuje ludzi z zoldem 0, wiec dla nich **pulap w ludziach** = pulap zoldu rodu /
  nominalny zold jednostki (zold, jaki mialaby bez `WatchUnpaid`); werbunek i nowe partie Strazy staja na tym pulapie.
- **162m dwor:** przelew glowa -> kasa siedziby raz na dobe (udzial "dwor i wyzywienie" minus jedzenie partii kupione wczoraj); siedziba = `Clan.HomeSettlement`;
  wies -> jej miasto albo zamek; rod bez lenna - miasto, w ktorym jest glowa, albo najblizsze miasto krolestwa. **Tarcza dworu:** wplata do kasy MIASTA dostaje
  znacznik tarczy (A `SoldierPay.Hold`), ktory **nie wygasa z czasem** - schodzi tylko wtedy, gdy zawor renty albo danina wojenna wyciagna zloto z kasy, i nigdy
  nie jest wiekszy niz nadwyzka kasy ponad cel regulatora. Bez tego regulator gry (`GetTownGoldChange`: 25% nadwyzki ponad cel dziennie) zjadalby ok. 83% wplaty
  (A `SoldierPay.cs:456-462`: do pana wraca ok. 17%), a dzisiejsza tarcza zoldu wygasa po ok. 2 tygodniach - przy D169 rodow z miastem ok. 1.03 mln/dobe
  (T9 d120 [P]) do kas miast plynie ok. 150-300 tys./dobe dworu. 111' (K6) zostaje w etapie 5 (PLAN 5.1); tarcza dworu to wariant zgodny z PLAN (a w OB 12 162 szlo
  razem ze 111' wlasnie z tego powodu). Linia "niewydane udzialy budzetu" i licznik "dwor: wplacone / skasowane przez regulator". Gracz bez dworu ([D] 08.10).

**Liczby i uzasadnienie.**
- 0.60 / 0.20 / 0.17 / 0.03 w wojnie (W-8): z jedzeniem w budzecie rachunek przestaje byc za dobry (bez tej linii 3-4 rody na minusie).
  0.28 w pokoju (W-2): pokoj 36% wojny (decyzja 35-40%); po etapie 3 ok. 0.23.
- 45 dni zapasu wojny: 40 dni wlasnej wojny = 10-30% rocznego dochodu pana [H] (36-110 dni D).
- 1.10 / 3 doby / 15%: przejscie wojna -> pokoj w 2-3 tygodnie bez skokow; 30 ludzi na nowa partie = swita banneretu [H].
- Zalogi 80% w pokoju (W-6): zalogi pokojowe zamkow 45% wojennych (E2) - decyzja "polowa".
- **Skad dzis zold wojenny panow zamkow** (12 B3, 5.3): w E2 pan zamku placi zold ok. 1 894/dobe przy D bez wlasnych ok. 1 414 - pulap 0.6 D to ok. 850, a ok. 1 000
  (ponad polowa) idzie z zapasu wojny, ktory karmia jednorazowe ok. 1 786/dobe, z czego ok. 1 200 to statki rozbitych (zloto z niczego). **Ok. polowa wojennego
  zoldu panow zamkow stoi dzis na zlocie z niczego** - zamkniecie statkow (181, etap 3) bez wyrownania utnie im ok. 1/3 zoldu.
- E2 wersji 1 (rok, nowa kampania): 0 rodow na minusie (miara: rodzina), wojsko w wojnie 166 tys. z zalogami (-4%), pokoj 59 tys., panowie zamkow 51.3 / 12.8 tys. [S].
  To **gorna granica**: E2 nie znal dworu w kasie miasta z regulatorem, konnych Dothrakow 175, Strazy bez zoldu, 1/9, okupow z ratami, dezercji C3, roku 2,
  miary glow. Po [D] 10:45 stalego "-4%" nie ma: luke miedzy budzetem a dzisiejszym wojskiem zamyka kredyt wojenny (168), w granicy zdolnosci splaty.
  W dobie 120 T9 pulap na sucho tnie 102 rody (ok. 32 tys. ludzi ponad pulapem), z D stalym 116-125 rodow - swiat nie ma za duzo wojska, ma je zle rozlozone.

**Platnik -> odbiorca.** Rod -> sakiewki ludzi (zold partii, jest), kasy osad (zold zalog, jest), kasa siedziby (dwor, pod tarcza), kasy miast i wsi (jedzenie,
sprzet); czlonek -> glowa (kiesa rodziny); zwolnieni zabieraja czesc sakiewki partii -> kiesa wsi; zwolnieni z zalogi -> ludnosc wsi (sakiewka zalogi zostaje w kasie osady).

**Kod** (miejsca z OB 6.1, sprawdzone w dekompilacji; M18: sprawdzic na bazie po wgraniu 11):
- pulap partii: postfiks gra `ClanVariablesCampaignBehavior.MakeClanFinancialEvaluation` (`:353`, wolana z `DailyTickClan :389-412`; BK prefiks `Prefix2Impl`
  BK `EconomyPatches.cs:117-156` zwraca false - postfiks biegnie) -> `SetWagePaymentLimit` kazdej partii; dzialaja hamulce gry (brak werbunku i awansow ponad limit);
- pulap zalog: postfiks `UpdateClanSettlementsPaymentLimit` (`:458`) -> `SetGarrisonWagePaymentLimit` (w wojnie wartosc gry bez zmian, w pokoju cel 50%);
- dezercja z limitu: A `DesertionLaw.cs:144-145` (wywolanie `GetTroopsToDesertDueToWageAndPartySize` - dla rodow AI z budzetem pominiete) i prefiks gra
  `DefaultPartyDesertionModel` dla partii, ktorych `DesertionLaw.Governs` nie obejmuje (A `DesertionLaw.cs:115-117` - galaz `base.GetTroopsToDesert`);
- nowa partia: prefiks gra `HeroSpawnCampaignBehavior.ConsiderSpawningLordParties` (`:175-201`) - nie, gdy wolne miejsce w pulapie < 30 ludzi x sredni zold (wspolny z 179);
- werbunek: prefiks `RecruitmentCampaignBehavior.CheckRecruiting` obok latek A `MountedWage.cs:118` i `RecruitCost.cs:377` (pulap w ludziach dla zoldu 0);
- sprzet: A `AiGear.cs:647` (budzet wizyty = niewydany pulap sprzetu z dni od ostatnich zakupow, najwyzej 30 dni, zamiast 25% kiesy); budowy A `BuildFunding.cs:155-159` (dochod = D);
- dwor, kiesa rodziny, zwolnienia: nowy `ClanBudget.cs`; zwolnienia przez A `MusterOut.cs`; tarcza dworu: A `SoldierPay.cs` (`Hold` z rodzajem "dwor", `DecayHeld` `:557` bez wygasania dla tego rodzaju);
- jedna formula pulapu dla logu i gry: stale A `ClanIncomeBook.cs:27-29` -> `Settings`, `:453-473` wola `ClanBudget`;
- BK `BKLordPropertyBehavior.ShouldHaveCaravan` / `ShouldHaveWorkshop` (prefiksy B5).

**Wylaczniki.** `ClanBudgetEnabled` (z nim `AiWageLimitDesertionOff`), `ClanBudgetStableD`, `PeaceWageShare` 0.28, `WarWageShare` 0.60, `HouseholdSharePeace/War` 0.35/0.20,
`GearSharePeace/War` 0.17/0.17, `WarChestToWages` 0.8, `WarChestDays` 45, `ReserveDaysPeace` 60, `ReserveCapDays` 120, `WarReserveDays` 20, `WarReserveFloor` 20000,
`WarReservePerAdult` 5000, `PovertyDeepShare` 0.25, `PovertyDeepFactor` 0.5, `BudgetHysteresis` 1.10, `BudgetHysteresisDays` 3, `ReleasePerDay` 0.15, `MinNewPartyMen` 30,
`GarrisonPeaceShare` 0.5, `GarrisonWarFull` true, `GarrisonMaxShareOfBudgetPeace` 0.8, `AiGearDaysCap` 30, `GearUnspentToHouseholdDays` 30, `FamilyPurseFloor` 5000,
`FamilyTopsUpHead` true, `UnpaidTroopsCapInMen` true, `HouseholdMinimal` (162m), `HouseholdShield` true, `IronBankFamilyPays` -> false.

**Skutek.** AI: wojsko wedlug dochodu - w pokoju polowa zalog i ok. 1/3 wojska; w wojnie zalogi jak dzis, a partie z budzetu, zapasu wojny i (od D) kredytu;
zwolnieni wracaja do wsi (wiecej ludzi w regionach). Gracz: przeciwnicy i sojusznicy liczniejsi tam, gdzie bogaci; budzet go nie dotyczy.

**Test (C1, 40 dob).** Glowy < 5 000: 0; "zwolnieni: do wsi N, zniklo 0"; dezercja AI z limitu zoldu 0; zalogi w wojnie >= 95% biegu bazowego na krolestwo;
"dwor: skasowane przez regulator" 0 zl; Straz w ludziach <= pulap w ludziach; wojsko w druzynach lordow w wojnie >= 90 tys. (bez KW). **Po C3 (120 dob):** progi
z rozdz. 1 poza tymi, ktore wymagaja D (bez 168: bankrutow Banku <= 3, dluznikow <= 20 - dzis 15 i 83). Pokoj (bieg z wymuszonym pokojem, E): zalogi pokojowe
45-50% wojennych, zold / D panow zamkow <= 0.3, wojsko 35-40% wojennego.

### 185: kontrakt najemnika AI (krok C1; 2.10)

**Regula** (D-6). W dniu najmu kontrakt K = 1.3 x dzienny zold kompanii (zold 1.0 + jedzenie ok. 0.15 + sprzet ok. 0.15). W pokoju kontrakt "w oczekiwaniu":
polowa K i polowa ludzi. Przeglad co 28 dob tylko w dol: gdy kompania ma < 75% ludzi z umowy, K do stanu faktycznego. Pulap 166 najemnika = ludzie z umowy
(nie 0.55 D, bez udzialu "dwor"). Bez zwrotu 50%. **Dla AI w sluzbie najemnej gra nie placi nic z siebie:** dochod "za tier" = 0 i kontrakt gry
(`AddMercenaryIncome`: wplyw x mnoznik, z `MercenaryWallet` krolestwa, ktory splacaja wasale) = 0 - bez dopisku do `MercenaryWallet`, wiec wasale nie placa
drugi raz (dzis ok. 100 zl/dobe na rod - 12). Kontrakt placony w kolejnosci 165 przed zwrotem; gdy wplywy nie starcza - proporcjonalnie, niedoplata przepada;
niedoplata > 50% przez 28 dob z rzedu - kompania odchodzi ze sluzby. **Gracz-najemnik zostaje na kontrakcie gry** - pozycja PLAN 2.10 dotyczy najemnikow AI,
a kontrakt gry ma prawdziwego platnika (wasali); obie drogi bez zlota z niczego.

**Liczby.** 11 rodow najemnych: kontrakty ok. 13 tys./dobe w wojnie, 6.5 tys. w pokoju; ludzie 1 885 w wojnie, ok. 1 025 w pokoju; kiesa stabilna
(S3: 291 -> 300 tys.) [S, 12]. Wersja "1.15 x biezacy zold" odrzucona - kazdy wzrost wojska podnosil kontrakt (Z8), a pod 166 kompanie topnialy do 0 w 90 dob.
[H] kondotier na okreslona liczbe kopii, sprawdzanej na przegladach; w pokoju "condotta in aspetto". Lore: wolne kompanie dostaja kontrakt tylko w sluzbie.

**Platnik -> odbiorca.** Skarbiec pracodawcy (wplywy dnia, przed zwrotem) -> glowa rodu najemnego. Gracz: `MercenaryWallet` (wasale) -> gracz, jak dzis.

**Kod.** gra `DefaultClanFinanceModel.cs:133-137` (postfiks: "za tier" = 0 dla AI), `:504-513` (prefiks `AddMercenaryIncome`: dla AI bez wplywu i bez zmiany
`MercenaryWallet`; gracz bez zmian), `:325-338` (`AddExpensesForHiredMercenaries` - wasale placa tylko za kontrakty gry, czyli za gracza); nowy `MercContract.cs`
(zapis umowy przez `SaveText`); A `KingdomTreasury.cs:170` (najemnicy dalej bez zwrotu).

**Wylaczniki.** `MercContractEnabled`, `MercContractFactor` 1.3, `MercPeaceShare` 0.5, `MercReviewDays` 28, `MercReviewFloor` 0.75, `MercUnpaidLeaveDays` 28,
`MercGameContractAiOff` true.

**Skutek.** AI: kompanie stale co do liczby; biedna korona nie utrzyma najemnikow. Gracz: brak zmian (wrogowie i sojusznicy-najemnicy przewidywalni).

**Test.** 40 dob: "za tier" AI 0 zl, kontrakt gry AI 0 zl, `MercenaryWallet` zmienia sie tylko o kontrakt gracza. 120 dob: ludzie najemnikow w wojnie i pokoju
+-10% umowy; kontrakty w linii "Korona".

### 182: dary miedzy koronami, Straz bez zoldu, biedne krolestwa (krok C1; 2.4, 2.5)

**Regula.**
- **Polnoc -> Nocna Straz:** do 25% wplywow dnia skarbca Polnocy. **Wolne Miasta -> Dothrakowie:** kazde Wolne Miasto 10% swoich wplywow dnia ([D] 03:45 nr 12 = Q4a).
- Dar idzie przez skarbiec odbiorcy **tego samego dnia do rodow odbiorcy wedlug stalych wag** - nigdy wedlug liczby ludzi (Z8: wiecej ludzi nie moze dawac
  wiekszego kawalka daru): Straz - twierdza Muru albo zamek 1, wies 0.25, rod bez lenna 0.5; Dothrakowie - rowno na kazdy rod khalasaru. Lore: dar karmi khalasar
  i braci, nie lezy w skarbcu (renty wedlug lenn by go nie rozdaly - Dothrakowie i Straz maja malo lenn). Liczy sie do ich D (czesc "korona").
- **Straz bez zoldu** ([D] 07.10): zold jednostek Strazy w partiach i zalogach = 0; jedzenie i sprzet Straz kupuje z daru i swoich wsi (S11: dar nie jest zoldem).
  Liczebnosc Strazy trzyma **pulap 166 w ludziach** (pulap zoldu / nominalny zold jednostki), nie jedzenie: przy darze 3.5-3.9 tys./dobe i jedzeniu 0.6-1.1 zl
  na czlowieka samo jedzenie pozwalaloby na 3.2-6.5 tys. ludzi (dzis 3.8 tys.), a glod oscylowalby (glod -> morale -> dezercja wedlug poziomu). Werbunek
  z wyrzutkow - etap 4 (4.16).
- **Biedne krolestwa slabsze** (Q4a): bez osobnego mechanizmu - to skutek D stalego (W-1) i budzetu 166. W tescie lista biednych z lore: Zelazne Wyspy,
  Smocza Skala, Sarnor (osobne progi, rozdz. 1). Polnoc i Dothrakowie tej listy nie maja - ten sam prog co reszta (-25%).

**Liczby.** Dar Polnocy ok. 3.5-3.9 tys./dobe, Wolnych Miast ok. 3-10 tys./dobe razem [S, 12 D-5]. 25% / 10% - [H?] Straz zyje z darow panow Polnocy i korony;
Wolne Miasta oplacaja khalasary "darami", zeby nie zlupily miast. E2 wersji 1 liczyl dar Strazy jako zold (S11) i Dothrakow pieszych (S12): przy zoldzie konnych
z 175 x1.26-1.49 Dothrakowie wychodza ok. -38..-48% w wojnie [S] - przeliczyc na biegu bazowym (warunek wejscia C1). **Regula korekty, nie pytanie:** jesli
po 40 dobach C1 albo po 120 dobach Dothrakowie maja glowy < 5 000, bankruta **albo wojsko w wojnie ponizej -25% wobec dzis**, udzial Wolnych Miast rosnie do 15%
(gora lore "dar za spokoj") i test sie powtarza; jesli i to nie wystarczy - lup Dothrakow (juz w D, wyjatek najezdzcow) i kredyt wojenny 168 dzialaja jak u innych,
a reszta luki zostaje jako "biedni slabsi" z informacja dla Jeffa.

**Platnik -> odbiorca.** Skarbiec Polnocy -> glowy rodow Strazy (wagi lenn); skarbce Wolnych Miast -> glowy rodow Dothrakow (rowno na rod).

**Kod.** Nowy krok w `KingdomTreasury` (przed zwrotem, wlasny `try`); id krolestw z danych ROT do sprawdzenia przed kodem (M3: Polnoc, Straz, Dothrakowie; Wolne Miasta
w ROT - Braavos, Pentos, Myr, Lys, Tyrosh, Volantis, Norvos, Qohor, Lorath - te, ktore sa krolestwami; Qarth nie jest Wolnym Miastem);
zold Strazy 0 - postfiks w modelu zoldu obok `MountedWage` (A `MountedWage.cs:43, 78-128`, kontekst partii); pulap w ludziach - 166.

**Wylaczniki.** `CrownGifts`, `GiftNorthToWatchShare` 0.25, `GiftFreeCitiesToDothrakiShare` 0.10 (korekta 0.15), `WatchUnpaid` true, `GiftSplitFixedWeights` true.

**Skutek.** AI: Straz i Dothrakowie nie bankrutuja; biedne krolestwa mniej liczne. Gracz: gdy jest krolem Polnocy albo Wolnego Miasta - placi dar z
wplywow; w Strazy - bracia bez zoldu.

**Test.** 40 i 120 dob: 0 bankrutow i <= 1 glowa < 5 000 u Dothrakow i w Strazy; Straz -25%..+10% wojska wobec dzis; Dothrakowie nie ponizej -25%;
dary w "Korona" co do 1 zl; glod w Strazy (dni z jedzeniem < 1 dnia) <= dzis.

### 180: renty korony wedlug lenn (krok C2; 2.3)

**Regula** (D-4, Q3a, [D] 03:45 nr 10). Co zostanie z wplywow dnia po darach, reparacjach, kontraktach, nagrodzie za wielkiego jenca i zwrocie, korona rozdaje
rodom krolestwa wedlug stalych wag lenn: **miasto 3, zamek 1, wies 0.25** (od 173: 2 / 1.5 / 0.25 - Z15-5, etap 5; wagi jako ustawienia). Rod krola tez (skarbiec to
nie kiesa krola). **Warunek sluzby** (M4 - mierzalny i naprawde mierzacy sluzbe):
1. **zaloga:** w kazdej twierdzy rodu co najmniej 50% **stalej normy** = sredniej zalogi twierdz tego rodzaju (miasto / zamek) w krolestwie z ostatnich 28 dob
   (nie wlasnej sredniej rodu - stale mala zaloga nie przechodzi);
2. **sluzba:** w ostatniej wojnie krolestwa (albo w biezacej, z ostatnich 60 dob) partia glowy albo czlonka rodu byla co najmniej 20 dob **w armii krolestwa,
   przy oblezeniu albo na ziemi wroga** (najblizsza osada nalezy do krolestwa, z ktorym jest wojna); niewola na wojnie liczy sie jako sluzba. Karawana,
   tabor albo partia stojaca we wlasnych osadach - nie.
Rod bez warunku - jego udzial zostaje w skarbcu (zapas) na nastepny dzien. Gracz na tych samych warunkach.

**Liczby.** Renta na udzial (mediana krolestw) w E2 wersji 1 (1/180): wojna ok. 35, pokoj ok. 330 zl/dobe; po calym projekcie (S3) 390 / 740 [S, 12]. **W roku 1
renty placi w duzej czesci zapas koron** (1/360 dziennie) - przy 1/360 renta pokojowa roku 1 wyjdzie ok. 1/3 nizsza niz w E2 wersji 1, a w roku 2 jeszcze
ok. 20% nizsza [S]; to nie jest staly dochod w tej wysokosci. Gracz z 1 miastem, 1 zamkiem i 4 wsiami (5 udzialow): w etapie 2 ok. 100-200 zl w wojnie
i ok. 1 000-1 400 w pokoju roku 1 [S, do przeliczenia w E2]. Bez rent (S3): skarbce rosna do 137 mln w rok, wojsko -19%, zalogi pokojowe zamkow 34% - renty sa
kanalem, ktorym nadwyzka korony wraca do obiegu. Stale wagi (nigdy wedlug zalogi): renta od zalogi zwracala graczowi 1.43-2.88 z monety (K2) - maszyna do
pieniedzy (Z8). 20 dob = polowa "sluzby 40 dni" [H]; 50% zalogi = decyzja "zalogi w pokoju polowa" ([D] 08.10). To mechanizm gry, nie historia (najblizszy
wzor: renty z nadania krola, Salisbury 1337 [H]).

**Platnik -> odbiorca.** Skarbiec (reszta wplywow dnia) -> glowy rodow wedlug udzialow; udzialy wstrzymane zostaja w skarbcu.

**Kod.** Nowy krok w `KingdomTreasury` po zwrocie (wlasny `try`); licznik sluzby w nowym `CrownRents.cs` (dni w armii, przy oblezeniu i na ziemi wroga na rod;
norma zalogi na rodzaj twierdzy i krolestwo, zapis przez `SaveText`); linia "Korona": renty, udzial, rody bez warunku. D: renty w czesci "korona".

**Wylaczniki.** `CrownRents`, `CrownRentWeightTown` 3, `CrownRentWeightCastle` 1, `CrownRentWeightVillage` 0.25, `CrownRentServiceDays` 20, `CrownRentGarrisonShare` 0.5,
`CrownRentGarrisonNormKingdom` true.

**Skutek.** Gracz: dochod z lenna od korony, ale tylko gdy trzyma zalogi i naprawde jezdzi na wojne; w pokoju to glowna czesc rent. AI: panowie zamkow maja dochod
w pokoju (renty to ok. 30% ich D pokojowego w E2), rody "siedzace w domu" nie dostaja nic.

**Test (C2, 40 dob; 120 dob po C3).** Suma rent + udzialy wstrzymane = reszta wplywow (co do 1 zl); rody bez warunku < 20%; Z8 gracza < 1 (linia "Obieg").

### 179: rycerze bez lenna bez oddzialow (krok C3; 2.7)

**Regula** ([D] 03:45 nr 9 = Q1a; potwierdzone 10:20). 85 rodow BK "gentry" (jeden majatek, bez lenna) nie prowadzi wlasnych druzyn. Na wezwanie choragwi
glowa rodu rycerza **jedzie w druzynie swojego pana** (wlasciciel osady majatku) jako czlonek partii; gdy pan nie ma partii albo nie jest w armii - w druzynie
wodza armii krolestwa. Pan placi mu **24 zl dziennie** ("jak rycerzowi swity", w pulapie zoldu pana 166, ze zwrotem 50% korony jak zold partii). Po rozwiazaniu
armii albo w pokoju rycerz wraca do majatku (BK juz sadza go tam co tydzien). Ludzie majatku zostaja w ludnosci BK. Ta sama regula dla rycerzy-wasali gracza:
na wezwanie gracza jada w jego druzynie i gracz placi 24 zl. **Kiesa rycerza bez wojska:** odplyw majatku (zaopatrzenie wsi i niewolnicy BK - 169c) w pulapie
166 (sprzet albo osobny pulap "majatek"); bez tego sam odplyw zjada prog glow.

**Liczby.** 24 zl = 2 szylingi, zold rycerza w kontraktach 1282-1300 [H] (w skali gry miedzy t5 a t6 konnym). Dzis rycerze: D 16-99 zl, 12-18 rodow z partia
jednego dnia (ok. 24 ludzi), zold 4-7 tys./dobe, 8 glow < 5 000 i 1 bankrut w T9 d120 [P]. **Odplyw bez wojska:** 37 rodow gentry bez ludzi (srednio < 1 w dobach
40-120) traci srednio 116 zl/dobe, na Zelaznych Wyspach ok. 300/dobe od 6. doby przy zoldzie 0 (Fyomanoving 39 388 -> 3 994); liniowo w dobie 364 to 14 glow
< 5 000 wsrod samych rycerzy bez wojska (50 z 85 razem z tymi, ktorzy maja druzyny) [P, S]; zapis 362 ma 41 glow bez lenna < 5 000. Kanal do potwierdzenia
w 169c: zakupy majatku BK (BK `BKVillageSupplyAutoBehavior.cs:349`: kiesa wlasciciela -> kasa miasta) albo niewolnicy (BK `BKEstateAutoSlavePurchaseBehavior.cs:106`:
kiesa -> osada). Po 179: zold rycerzy 0, dochod rycerza w sluzbie +24/dobe (wiecej niz caly dzisiejszy D), w swiecie ok. 0.4-0.7 tys./dobe od panow do rycerzy [S].
Mniej druzyn na mapie - mniej pracy gry.

**Platnik -> odbiorca.** Glowa rodu pana (z budzetu 166) -> glowa rodu rycerza; skarbiec zwraca panu 50% w wojnie. Kiesa rycerza -> kasa miasta (zaopatrzenie
majatku, w pulapie) - prawdziwy odbiorca, tylko szybkosc pod pulapem.

**Kod.** Prefiks BK `BKGentryBehavior.SummonGentry` (`:493-537`, wolany z BK `CallBannersGoal.cs:319`): zamiast `SpawnLordParty` i `TakeRetinue` -
`AddHeroToPartyAction` do partii pana; prefiks gra `HeroSpawnCampaignBehavior.ConsiderSpawningLordParties` (`:175`) - nic dla rodu z BK `IsGentryClan` (`:472`)
bez lenna; powrot: nasluch rozwiazania armii -> zdjecie z partii i `EnterSettlementAction.ApplyForCharacterOnly` do osady majatku; zold rycerza w nowym
`GentryService.cs`. Istniejace partie rycerzy: bez nowych rekrutow (pulap 0), BK rozwiazuje je w majatku (`FinishParty :467`). Pulap majatku: prefiksy na
BK `BKVillageSupplyAutoBehavior` (`:337-352`, limit kwoty dnia) i `BKEstateAutoSlavePurchaseBehavior` (`:103-108`).
**Do sprawdzenia przed kodem:** czy gra i BK toleruja bohatera obcego rodu w partii (ekran druzyny, niewola, smierc pana, BK `OnPartyDailyTick :407-436`).
Gdy nie - **wariant zapasowy:** rycerz zostaje w majatku, a na wezwanie jego pan dostaje z majatku 1 rycerza-zolnierza tieru 5 konnego w swoja druzyne
(ten sam zold 24, rod rycerza dostaje zold) - to dalej "bez wlasnych druzyn".

**Wylaczniki.** `GentryNoParties`, `GentryKnightWage` 24, `GentryEstateSpendCap` true.

**Skutek.** AI: rycerze nie trzymaja wojska, a kiesa ich majatku nie topnieje szybciej, niz pozwala dochod; panowie maja na wojnie rycerzy w druzynie.
Gracz: jego rycerze-wasale jada z nim, za oplata.

**Test.** 40 dob: partie rodow gentry w dobie 10+ = 0; rycerzy w druzynach panow > 0 przy kazdej armii; 0 bledow (zwlaszcza niewola i smierc pana z rycerzem w partii).
120 dob: **glowy gentry < 5 000 w dobie 120: 0**; sredni odplyw kies rycerzy bez wojska <= ich D.

### 183: dezercja AI wedlug poziomu (krok C3; 2.9)

**Regula** ([D] 04:25 C "dezercja wedlug poziomu takze u AI - TAK").
1. Prawo dezercji wedlug poziomu (progi morale: t1 < 25, -3 na tier, podloga 10; 1%/pkt, sufit 25%/dzien) **dla AI jak dla gracza**.
2. Dezercja gry z limitu zoldu - wylaczona juz w 166 (C1, ten sam wylacznik co budzet); tu bez zmian.
3. WarLedger (zalegly zold) - jedna regula: bez polowy stawki dla AI (z budzetem zaleglosc u AI jest rzadka); sufit 8 dni zostaje; **partia nie schodzi
   ponizej 30 ludzi** przez zalegly zold (OTWARTE nr 2: "minimalna liczebnosc" - tam dla AI; 30 = swita banneretu). Dla AI od razu; dla gracza - pytanie 5.1
   (domyslnie tak, jedna regula).
4. Pozycja PLAN 2.9 ("bez limitu dziennego") w kodzie jest juz nieaktualna - limit WarLedger istnieje (sufit 8 dni, A `WarLedger.cs:77-78`); brakowalo podlogi i wlaczenia C.

**Liczby.** Dzis dezercja AI ok. 20 ludzi/dobe w swiecie (gra 16/dobe w d40, WarLedger 624 ludzi w 120 dobach - ok. 5/dobe) [P]. **Prog bezpieczenstwa:**
dezercja AI z morale i z zaleglego zoldu razem najwyzej dzis + 50% (ok. 30 ludzi/dobe w swiecie, srednia 28 dob). Wersja 1 (0.3% ludzi dziennie = ok. 315/dobe,
ok. 110% rocznie) byla 15 razy wyzsza od dzis, a kazdego dezertera trzeba odkupic z udzialu sprzetu 0.17 D. Wiecej niz prog znaczy, ze morale AI jest za niskie
(np. T10 niewyspani) - naprawiamy morale, nie dezercje. [H] niezaplaceni odchodzili calymi oddzialami (1340); odejscia najpierw najslabiej zwiazanych sa zgodne z historia.

**Platnik -> odbiorca.** Ludzie: dezerterzy -> pula wyrzutkow regionu i ksiega ludzi (T4, jest); zwolnieni -> wies (166).

**Kod.** A `Settings.cs:250` `DesertionLawForAi` -> true (MCM `McmSettings.cs:825`), A `DesertionLaw.cs:81-93` (`Governs`); A `WarLedger.cs:87` (bez x0.5; opis
`Settings.cs:369`), `:97` i `:160` (podloga 30 w `DesertElitesFirst`; T4 do puli wyrzutkow `:94-128` bez zmian); linia "Dezercja AI wedlug przyczyny" (169c).

**Wylaczniki.** `DesertionLawForAi` true, `WarLedgerMinMen` 30, `WarLedgerMinMenPlayer` true (5.1), `WarLedgerAiHalf` false.

**Skutek.** AI: oddzialy z niskim morale traca ludzi jak u gracza; bez "topnienia" partii do samego lorda. Gracz (gdy 5.1 = tak): jego partia nie spada ponizej
30 ludzi z powodu zaleglego zoldu (dalej traci ludzi ponad to) - to nowa ochrona dla Ciebie.

**Test.** 40 i 120 dob: dezercja AI (morale + zalegly zold) <= bieg bazowy + 50%; WarLedger AI <= 1 000 ludzi w 120 dobach; wojsko 95-115 tys. (po D).

---

### 168: dlug, kredyt wojenny i zajecie zamiast bankructwa (krok D; 2.12)

**Regula** (OB 8.1-8.3 z W-5, S24-S26; [D] 05.10 lord nigdy nie traci lenna, "w ostatecznosci wyprzedaje wszystko"; 07.10 wariant A - wierzyciel bierze caly
dochod wsi i kiese ponad 38 tys., odsetki zamrozone bez umorzenia; 08.10 dlug wymarlego rodu na nowego pana wsi, takze gracza; **10:45 w wojnie na kredyt,
splacany z lupow i okupow, z limitem zdolnosci splaty**).
- **Kredyt wojenny (KW)** - jedyna pozyczka AI. Warunek: rod w wojnie, G < R, zold naliczony > pulap 166 bez kredytu (ten sam warunek co dzisiejsza kolumna
  `kandydat_pozyczki`, A `ClanIncomeBook.cs:546`). Wyplata dzienna z kapitalu Banku do kiesy glowy: brak do zoldu naliczonego, **najwyzej 0.40 x D dziennie**,
  do limitu. Splata: w wojnie **50% kazdego jednorazowego wplywu** rodu (lup, okupy, sakwy, trzecia, statki) najpierw na KW - poza limitem rat z D, bo to nie
  dochod staly; po pokoju rata 10% D + 100% jednorazowych do splaty, a budzet 166 zwalnia nadwyzke ludzi do pulapu pokojowego. Odsetki jak D1.
  Nigdy: rod w zaleglosci (D2+), rod po D3 przez 182 doby bez zaleglosci, rod, ktorego D nie pokrywa kosztow stalych (dwor + powinnosci), Bank z wolnym kapitalem
  < 1 mln (wtedy wojsko wraca do budzetu - zwolnienia 166). Pozyczek "na okup" nie ma (wersja 1): gotowka okupu idzie z nadwyzki kiesy, a rata Banku i rata okupu
  i tak siedza w tym samym limicie 15% D - pozyczka tylko zamienialaby jeden dlug na drugi. Pozycza tylko Bank (skarbiec po 165 nie ma wolnych pieniedzy).
- **Limit:** 15 x D (czesc "ziemia") + 10 000 za miasto + 5 000 za zamek + **30 x srednie jednorazowe od prawdziwego platnika z 84 dob** (lup, okupy, sakwy,
  trzecia; bez statkow i innych zrodel z niczego), x wiarygodnosc. **Rata Banku** najwyzej 10% D dziennie; **wszystkie raty rodu z D razem** (Bank, okupy,
  dlug zoldu) najwyzej 15% D - gdy suma wieksza, najpierw Bank, potem okupy w kolejnosci powstania (S4).
- **Drabina:** D1 kredyt (20/30/45% rocznie jak dzis, +10 pp przy biegnacym dlugu) -> D2 zaleglosc (placi, ile ma ponad 3 dni zoldu; wiarygodnosc x0.8, +2 pp;
  KW wstrzymany) -> D3 zajecie po 3 zaleglosciach: wierzyciel bierze u zrodla **caly dochod wsi** rodu (renta wsi i podatek wsi z licznika "Village Demesnes")
  + kiese glowy ponad **38 000** ([D] 07.10 pyt. 7); odsetki zamrozone od 1. dnia D3; kredyt odciety; D liczy ziemie bez zajetego dochodu, wiec poczet sam
  maleje (zwolnienia 166) -> D4 wyprzedaz co 7 dni, **tylko gdy zajecie dalo 0 przez 14 dni albo prognoza splaty > 728 dni**: nadwyzki zbrojowni i konie -> targ,
  karawany -> targ, warsztaty -> najbogatszy notabl; przychod do wierzyciela; lenno zostaje. D3 i D4 licza sie jako **zajecie**, nie bankructwo (rozdz. 1).
- **Bez umorzenia:** dlug wymarlego rodu przechodzi na nowego pana jego dawnych wsi (takze gracza; [D] 08.10). Rod bez wsi - dlug zostaje w ksiedze Banku jako
  niesciagalny (bez umorzenia, bez platnika), linia "dlug bez platnika"; korona go nie przejmuje (to bylo poza decyzja - tylko pomysl, rozdz. 3).
- **Dlug zoldu** (`DebtToKingdom` z niezaplaconego zoldu): gra dopisuje brak do dlugu wobec korony, a SoldierPay obcina ludziom wyplate o te kwote
  (A `SoldierPay.cs:346-355`) - prawdziwym wierzycielem sa ludzie partii. Dlug wchodzi do ksiegi z wierzycielem "sakiewka ludzi" i ta sama drabina; splata
  (gra `AddPaymentForDebts`) idzie do sakiewek partii rodu wedlug obciec; partia rozwiazana - zapasowy odbiorca (2.0b), nigdy w nicosc.
- **Kapital Banku** (S26): 5 mln na starcie nowej kampanii zapisane w ksiedze jako dar startowy (raz, jak kasy osad); Bank jako posiadacz w "Pieniadz swiata";
  zysk ponad 5 mln -> kasa Braavos po 1/180 dziennie.
- **Gracz-dluznik - ta sama drabina** (jedna regula i [D] 05.10 "w ostatecznosci wyprzedaje wszystko"; wersja 1 dawala graczowi "25% kiesy dziennie bez
  wyprzedazy" - inna regula niz AI, wziete z propozycji REGULY 5.4, a nie z decyzji): pozycza recznie w Braavos z tym samym limitem (takze KW); D3 - zajecie
  renty i podatku jego wsi i kiesy ponad 38 000; D4 - wyprzedaz nadwyzek zbrojowni druzyny, karawan i warsztatow; **nigdy** ekwipunku gracza i towarzyszy,
  koni pod wierzchem ani rzeczy z jego ekwipunku.
- Rod w dlugu: budowy 0, dwor -50%, sprzet tylko braki (166).

**Liczby i uzasadnienie.**
- Limit swiata z ziemi: 15 x D = 10.2 mln wobec dzisiejszego wzoru 14.6 mln i "15 x D169" 22.0 mln [R, T9 d120] - pozycza pod staly dochod z ziemi [H] (H-7);
  dzis limit z dochodu jednego dnia skacze x5 w kilka dob (Stark 36-203 tys.). Czesc z lupow (30 dni jednorazowych) - [D] 10:45 "splacaja sie z lupow wojennych";
  [H] kapitanowie Wojny Stuletniej trzymali poczty na kredyt pod przyszle okupy i lupy. Bez statkow - zamykamy je w etapie 3, limit nie moze stac na zrodle,
  ktore zniknie.
- KW 0.40 D: dzisiejsze wojsko panow zamkow kosztuje ok. 0.25-0.3 D wiecej, niz daje budzet z zapasem wojny (E2: panowie zamkow -16% w wojnie) [S]; 0.40 D domyka
  luke z zapasem na poczatek wojny. Pan zamku (D ok. 1 000-1 400): limit ok. 30 tys. = ok. 75 dni pelnego KW, a splata z 50% jednorazowych (ok. 900/dobe ze statkami)
  wyprzedza wyplate - kredyt sie obraca. Swiat: luka "-4%" to ok. 30-35 tys./dobe, ok. 4 mln na 120 dob przy kapitale 5 mln [S] - Bank starcza, dopoki lupy wracaja;
  gdy nie - prog wolnego kapitalu 1 mln zatrzymuje KW.
- 10% / 15%: rata miesci sie w czesci dworu i budow, ktora dluznik tnie; pozyczka 15 dni D z odsetkami do 45% splaca sie w ok. pol roku.
- Zajecie z podatkiem wsi: mediana splaty 323 dni (maks. 875) wobec 832-1 266 dni z samych rent [R]. Prog D4 728 dni, nie 364: przy 364 prawie polowa zajec
  przechodzilaby w D4. [H] Statut Kupcow 1285 (cala ziemia do splaty z jej dochodu) = wariant A; Magna Carta kl. 9: najpierw ruchomosci; elegit: bez wolow
  do pluga - podloga 38 000 ([D] 07.10; ok. 10 dni zoldu pocztu 135 ludzi, z ktorego ta liczba pochodzi).
- Odsetki 20/30/45% - miedzy "darami" Wlochow a lichwa kryzysowa 40-80% [H].

**Platnik -> odbiorca.** Bank (kapital) -> dluznik (KW: kiesa glowy -> zold); dluznik / zajety dochod wsi / wyprzedaz / 50% jednorazowych w wojnie -> Bank;
dlug zoldu: dluznik -> sakiewki ludzi (zapasowy odbiorca 2.0b); zysk Banku -> kasa Braavos.

**Kod.** A `IronBank.cs:138-162` (limit z D z ziemi + 30 dni jednorazowych), `:300-332` (kiedy pozycza - warunek KW zamiast "brak na zold"; wyplata dzienna do
0.40 D), `:342` (odsetki zamrozone od D3 - dzis liczone takze bankrutom), `:372-392` (3 zaleglosci -> D3 zamiast "bankructwa 50% + 25% dziennie"), `:405-410`
(po splacie po D3 kredyt dopiero po 182 dobach), `:413-417` (wymarly rod -> nowy pan wsi; bez wsi - "bez platnika"), `:178-181` (kapital), `:220-282` (T5
wylaczone przez 166), `:433-530` (menu gracza: limit, KW, zajecie, D4); splata KW z jednorazowych - nasluch `ClanIncomeBook.NoteInflow` (kategoria jednorazowe);
gra `DefaultClanFinanceModel.cs:219-230` (postfiks splaty dlugu zoldu); szczebel, prognoza i kandydat pozyczki juz liczone na sucho A `ClanIncomeBook.cs:474-486,
536-560` - przejsc na wspolna funkcje (jeden warunek w kodzie 168 i w kolumnie `kandydat_pozyczki`).

**Wylaczniki.** `DebtLadderEnabled`, `WarCredit` true, `WarCreditMaxShareD` 0.40, `WarCreditLootRepayShare` 0.5, `WarCreditLootDays` 30, `BankFreeCapitalFloor` 1000000,
`IronBankIncomeDays` 15 (D z ziemi - zmiana domyslnej z 60), `IronBankMaxInstalmentShare` 0.10, `AllInstalmentsMaxShare` 0.15, `SeizeFloorGold` 38000,
`SaleForecastDays` 728, `SaleAfterZeroSeizeDays` 14, `CreditAfterSeizureDays` 182, `BankProfitToBraavosDays` 180, `WageDebtToMen` true, `PlayerSameLadder` true.

**Skutek.** AI: pozyczki tylko na wojne i tylko do zdolnosci splaty; wojsko w wojnie jak dzis, dopoki lupy splacaja kredyt; rod w klopocie traci dochod wsi na
rok, nie lenno. Gracz: kredyt wojenny w Braavos na tych samych zasadach; nizszy limit, gdy ma male lenno; zamiast "bankructwa" (-100 renomy) zajecie jego wsi
i kiesy ponad 38 000, w ostatecznosci wyprzedaz nadwyzek (nie ekwipunku).

**Test (40 + 120 dob).** Bankructw 0 (miara z rozdz. 1); nowych D3 <= 1 w 120 dobach; D4 tylko po 14 dobach zerowego zajecia albo przy prognozie > 728;
dluznikow <= 30; wolny kapital Banku >= 1 mln w kazdej dobie; KW: wyplacone, splacone z jednorazowych, rody na limicie <= 20%; suma rat rodu <= 15% D u 100%
rodow; dlug zoldu splacany do sakiewek ("bez odbiorcy" 0); "dlug bez platnika" w logu; wojsko w druzynach lordow w wojnie 95-115 tys.

### 178: okupy wedlug majatku, wielcy jency i prawo trzecich (krok D; 2.13, 2.14)

**Regula** ([D] 00:00 okupy wedlug majatku, jedna regula, gotowka z nadwyzki, reszta na raty; 03:45 nr 13 bez minimow, nr 17 glowa pol roku zostaje, nr 18 korona
1/9; 04:55 Q3b 1/9 lupu wasali; **10:50 wielcy jency**).
- **Cena** (wszystkie drogi: AI-AI, kurier, posrednik, ekran, okup gracza) = W x D x 364, D rodu jenca, **dwa stopnie jak w decyzji**: glowa rodu W = 0.5
  (pol roku); kazdy inny lord albo dama W = 60/364 (ok. 2 miesiace; takze dziedzic i malzonek - stopnia 0.3 z wersji 1 w decyzji nie ma). **Krol** jest glowa
  swojego rodu: 0.5 x D rodu krola x 364, placi rod krola (wzor z wplywami korony i placacym skarbcem z wersji 1 - pytanie 5.3). Bez minimow i bez wzrostu x3.4
  za czas niewoli. Stawki RealisticCaptivity tylko przy wylaczonym przelaczniku.
- **Zaplata:** gotowka najwyzej 50% (G - 5 000) od razu; reszta to dlug okupu wobec odbiorcy w ksiedze 168, rata w lacznym limicie 15% D (Bank pierwszy).
  **Jeniec wolny po gotowce zawsze:** gdy limit rat jest pelny (kolejna niewola), nowy dlug staje w kolejce za starym - ta sama rata dzienna, dluzszy termin;
  nikt nie siedzi w niewoli dlatego, ze rod juz splaca inny okup. **Pulap dlugu okupow rodu** (pytanie 5.2, domyslnie wlaczony): razem najwyzej 364 x D - okup
  ponad pulap maleje do pulapu. Zajecie D3 obejmuje dlug okupu.
- **Szansa zgody:** jedna regula - stale 10% dziennie (jak barter gry); czas niewoli nie zmienia ani ceny, ani szansy.
- **AI-AI:** wlasny przeplyw zamiast barteru gry: placi glowa rodu jenca, dostaje glowa rodu porywacza (zapasowy odbiorca - 2.0b).
- **Wielcy jency** ([D] 10:50, jedna regula dla gracza i AI): pojmanego **krola albo nastepce tronu** (pierwszy w kolejce dziedziczenia rodu krola) przejmuje
  **korona zdobywcy** - caly okup (gotowka i raty) do skarbca krolestwa zdobywcy; zdobywca dostaje od razu **nagrode 1/10 okupu** ze skarbca (wplywy dnia,
  potem zapas ponad rezerwe; czego brak - z pierwszych wplat okupu). Gracz oddaje pojmanego krola swojemu krolowi i dostaje nagrode; zdobywca bez krolestwa
  (gracz albo rod niezalezny) - okup dla niego jak przy innych jencach. Wszyscy inni jency (takze wodz armii spoza rodu krolewskiego): okup dla zdobywcy, korona 1/9.
- **Kurier (jeniec u gracza):** placacy placi gotowke + raty graczowi, bez dosypki z niczego. **Posrednik:** cala cena od rodu jenca (gotowka + dlug wobec gracza),
  nic z niczego. **Okup gracza:** ten sam wzor z D rodu gracza, do porywacza (2.14), reszta na raty jak "dlug honorowy" RC.
- **Prawo trzecich:** korona krolestwa, ktore placi rodowi zwrot (jest w wojnie), bierze **1/9** z okupow otrzymanych przez jego rody (gotowka i kazda rata;
  poza wielkimi jencami), z sakwy rozbitej partii przypadajacej zwyciezcy i ze sprzedanego lupu - **bez progu kwoty** (decyzja progu nie ma; prog 1 600 zl
  z wersji 1 liczony od sztuki wycinal prawie wszystko, bo lup sprzedaje sie sztuka po sztuce - lord ok. 405 zl, sakiewka ludzi ok. 1.6 zl za sztuke).
  **Rozliczenie raz na dobe z licznikow** (bez zaokraglen na sztuce): u lorda AI 1/3 z jego trzeciej z dnia (sakwy - A `MenPurse.cs:104`, nadwyzki zbrojowni -
  `:498-531`; licznik juz jest: `ClanIncomeBook.NoteInflow(..., KThird)`), czyli lord 2/9, ludzie 2/3; do tego 1/9 sprzedazy lupu lorda AI przez gre
  (`PartiesSellLootCampaignBehavior` -> `SellItemsAction`, kasa miasta -> lord; ok. 47 tys. zl/dobe w dobie 364 [P]). U gracza-wasala: 1/9 sakwy z sakiewki
  jego ludzi (gracz nie bierze trzeciej) i 1/9 wartosci lupu z ekranu po bitwie (cena miasta po karze sprzedazy), z kiesy przy rozliczeniu doby jak powinnosci.

**Liczby i uzasadnienie.**
- [S] na D stalym (E2): pan zamku (D ok. 1 000) - glowa ok. 182 tys., lord ok. 60 tys.; pan miasta (D ok. 6 500) - glowa ok. 1.18 mln, lord ok. 390 tys.; rycerz
  (D ok. 20-30) - glowa ok. 4-5 tys. Dzis AI-AI glowa tier 4 ok. 5.8 tys. (3-4% rocznego dochodu - wojna nic nie kosztuje przegranych), u gracza 50-80 tys.
  (dwie skale za tego samego lorda - L7).
- **Raty w dlugiej wojnie** [S]: w rownowadze wojny G spada do ok. R (20 D), wiec gotowka 50% (G - 5 000) to ok. 7.5 D, a reszta ok. 174 D po 0.15 D dziennie
  = ok. 3.2 roku (wersja 1 liczyla 2.2 roku przy G 131 tys. z E2). W niewoli siedzi naraz 16-20 glow rodow (T9 d120 [P]) - przy niewoli 20-40 dni to rzedu
  100-300 pojman glow rocznie [S]; rod pojmany czesciej niz raz na ok. 3 lata ma dlug okupow rosnacy bez konca. Stad: kolejka bez blokowania uwolnienia, pulap
  364 D (5.2) i czestosc pojman z 169c w rachunku E2 przed kodem 178 (warunek wejscia D).
- **Kula sniezna:** rata 15% D przez lata to staly przeplyw od przegranych do zwyciezcow (rzedu dziesiatek tys./dobe), ktory przez zapas wojny i KW powieksza
  armie wygrywajacych - prog z rozdz. 1 (zadne krolestwo +20% wojska ani ponad 85% wygranych bitew lordow wobec biegu bez 178).
- **Gracz** lapie wiecej lordow niz przecietny rod AI: np. 3 glowy panow zamkow i 1 glowa pana miasta to ok. 1.73 mln dlugu, ok. 0.32 mln gotowki od razu i ok. 1.4
  tys./dobe rat przez ok. 2.7 roku (wobec renty ok. 100-200 zl/dobe w wojnie) [S] - najwiekszy nowy dochod gracza, z prawdziwym platnikiem; licznik "okupy gracza /
  D gracza" w 169c, liczby do rozdz. 0 po biegu bazowym.
- [H] pan ok. roku dochodu, krol 2-3 lata, raty 6-10 lat i czesto niesplacone; regula Jeffa to polowa stawki historycznej - "na miare majatku, bez ruiny".
  50% (G - 5 000): gotowka nie schodzi z kiesy ponizej podlogi rodziny. Wielki jeniec do korony z nagroda dla zdobywcy ([D] 10:50): David II 1346 (Copeland
  - renta 500 funtow rocznie), Jan II 1356 odkupiony od zdobywcow przez korone angielska [H].
- [H] indentury 1415: krol 1/3 zysku kapitana i 1/3 z jego trzecich = 1/9 zdobyczy ludzi; dzielono tylko lupy ponad 10 grzywien - tego progu nie bierzemy
  (w grze lup liczy sie sztuka po sztuce, decyzja Jeffa progu nie ma).

**Platnik -> odbiorca.** Glowa rodu jenca (gotowka, potem raty) -> glowa rodu porywacza (gracz tez) -> 1/9 do skarbca jego krolestwa; krol/nastepca: rod krola ->
skarbiec krolestwa zdobywcy -> nagroda 1/10 do zdobywcy; lup: trzecia lorda -> 1/3 z niej do skarbca (lord 2/9, ludzie 2/3); sprzedaz lupu lorda AI w miescie ->
1/9 do skarbca; gracz: sakiewka jego ludzi i kiesa -> 1/9 do skarbca.

**Kod.** gra `RansomOfferCampaignBehavior.cs:72-110` (prefiks `ConsiderRansomPrisoner`: AI-AI wlasny przeplyw zamiast `ExecuteAiBarter :107`), `:168-180` (kurier);
RC `FairRansom.cs:71-135` (`LordPrice`: nowy wzor pod `LordRansomByIncome`; usunac ograniczenie "tylko gracz" `:82`; posrednik `:107-118` cala cena od rodu),
RC `Patches.cs:141-171` (okup gracza), RC `CaptivityBehavior.cs:418-466` (raty gracza - ta sama ksiega 168); gra `SellPrisonersAction.cs:83, 88` (czesc z niczego
dla lordow); A `MenPurse.cs:104` i `:498-531` (trzecia - licznik `KThird`; 1/9 raz na dobe); gra `PartiesSellLootCampaignBehavior.cs:19-38` (`SellItemsAction.Apply`
`:38` - licznik sprzedazy lupu lorda AI); gra `PlayerEncounter.cs:1671-1682` (`DoLootInventory`: wartosc `RosterToReceiveLootItems` gracza-wasala); nastepca
tronu - kolejka dziedziczenia rodu krola (miejsce w grze/BK do sprawdzenia przed kodem); linia "Niewola lordow i okupy" (169c) z kwotami, 1/9 i nagrodami.

**Wylaczniki.** `LordRansomByIncome` (RC), `RansomHeadYears` 0.5, `RansomLordDays` 60, `RansomCashShare` 0.5, `RansomQueueNoBlock` true, `RansomDebtCapDays` 364 (5.2),
`CrownGreatCaptives` true, `CrownGreatCaptiveReward` 0.10, `CrownThirds` true, `CrownThirdsShare` 0.111, `CrownThirdsPlayerLoot` true.

**Skutek.** AI: przegrana bitwa kosztuje rod wedlug majatku (raty latami); lordowie nie siedza w niewoli dluzej z powodu starych dlugow; krol w niewoli to sprawa
dwoch koron. Gracz: bogaci jency drodzy, biedni tani; okupy w duzej czesci ratami - duzy dochod; jako wasal oddaje 1/9 lupu i okupow; pojmanego krola oddaje swojemu
krolowi za 1/10 okupu; jego wlasny okup liczony od jego dochodu.

**Test (40 + 120 dob, harness 2.14).** Okupy AI-AI > 0 w logu, 0 zl z niczego (L4, L5); lordow w niewoli > 60 dni <= bieg bazowy + 20%; jency wolni po gotowce
100%; suma rat <= 15% D; rody na pulapie dlugu okupow <= 5%; 0 bankructw z powodu okupu; 1/9 / (sakwy + trzecie + sprzedany lup wasali) = 11.1% +-0.5 pp i 1/9
w "Korona" co do 1 zl; kula sniezna jak w rozdz. 1; harness: okup gracza i jenca gracza z ratami, krol AI jako jeniec lorda AI i gracza (okup do skarbca zdobywcy,
nagroda 1/10 do zdobywcy, co do 1 zl).

### E. Proba zimy, 2.16 i test etapu 2

- **Proba zimy (1.14)** przed testem koncowym (S20: kod 166 jej nie potrzebuje; rok testu zimy nie zobaczy - wchodzi po ok. 1.6 roku). Potem 2.16 (zima RBL nie
  dubluje naszej) - jesli dubluje, wylaczyc ja w RBL przed testem.
- **Test etapu 2:** (1) nowa kampania 364 doby na DLL po kroku D i **kontynuacja do doby 728** (rok 2: zapas korony prawie zuzyty, renty i zwrot w stanie ustalonym);
  (2) **bieg z wymuszonym pokojem**: w dobie 60 pokoj we wszystkich wojnach niefabularnych (przelacznik testowy `TestForcePeaceDay`, domyslnie 0), potem 60 dob
  pomiaru - progi pokoju, zalogi pokojowe, renty pokojowe i powrot do stanu wojennego po nowych wojnach; (3) kontynuacja zapisu z doby 362 (dlugi Jeffa: jednorazowo
  ok. 7 zajec). Progi: rozdz. 1 w calosci; rok 2: glowy < 5 000 <= 10 w dobie 728, zwrot swiata >= 50%, wojsko w wojnie 95-115 tys. Czas: ok. 4-5 h autotestu.
  Gdy prog nie przejdzie - zmieniamy jedna liczbe naraz (kalibracja Z.1 w PLAN).

---

### Z. Zaprojektowane tutaj, wgrywane pozniej

**181 statki rozbitych do portu (Q2b, [D] 03:45 nr 11; wgranie: etap 3, z 164a).**
- *Regula:* statki partii rozbitej na ladzie albo rozwiazanej najpierw do innych partii rodu (jak gra); reszta przechodzi na najblizsze miasto portowe krolestwa rodu
  (wlasnosc stoczni miasta), ktore placi glowie rodu wartosc x kara sprzedazy gry - **ta sama kara dla AI i gracza** - najwyzej 25% nadwyzki swojej kasy ponad zapas
  kupcow (gdy nie ma wiecej, placi tyle - sprzedaz z koniecznosci). Czesc 181b: kupno i sprzedaz statkow w miescie przez kase miasta (dzis z niczego / w nicosc,
  33 tys./dobe - B4).
- *Kod:* Naval `NavalShipDistributionCampaignBehavior.cs:48-67` (prefiks na `RecoverGoldFromRemainingShipsAfterDistribution`, `:67` `GiveGoldAction(null, glowa)`),
  `ChangeShipOwnerAction.ApplyInternal` (galaz handlu z osada). Do sprawdzenia: limit statkow w stoczni miasta (czy gra kasuje nadmiar).
- *Dlaczego nie w etapie 2 - wojsko, nie pieniadz swiata:* statki to najwieksze wojenne zrodlo z niczego (ok. 269 tys./dobe w T9 doby 31-120; 34% wplywu panow
  zamkow). Wersja 1 uzasadniala odlozenie "dziura w pieniadzu swiata" (zamkniecie samych statkow: -170..-220 tys./dobe), liczac od bazy -41 tys./dobe z T9 -
  sprzed zamkniecia ujsc BEE 08.10 23:40 (12 B18). W obecnej wersji swiat rosnie: doby 31-40 +214..+355 tys./dobe (sklad6, 174b, grupa11, sklad8), zapis z doby 362
  ma ok. 228 mln wobec ok. 190 mln na starcie [P]; statki daja w tym oknie ok. 100-136 tys./dobe. Rachunek na nowej bazie - po biegu bazowym (krok A).
  **Prawdziwy powod:** ok. polowa wojennego zoldu panow zamkow stoi na tym zlocie (166 - jednorazowe -> G -> zapas wojny -> zold). Budzet nie liczy statkow do D
  ani do limitu KW, ale nadwyzka G ponad R wraca na zold przez zapas wojny - zamkniecie statkow **zmieni wojsko**: bez rent etapu 5 i K6 panom zamkow zniknie
  ok. 1/3 zoldu, ok. -15 tys. ludzi, -9..-10% swiata [S].
- *W planie etapu 3, przed 181:* policzyc wojsko po zamknieciu statkow bez K6 na biegu po D i wybrac: (a) wyrownanie wczesniej (renty 2/1.5/0.25 i zawor miast z Z15-5
  albo 184), (b) jawnie przyjac spadek i pokazac Jeffowi liczbe ([D] 10:45: wojsko wedlug tego, czy stac). Rada na dzis: (a), bo prog wojska 95-115 tys. bez tego nie przejdzie.
- *Test (etap 3):* "Pieniadz swiata" wobec biegu bez 181 + 164a = zamkniete ujscia - zamkniete zrodla (oba z logu) +-30 tys./dobe; kasy portow nie ponizej zapasu
  dluzej niz 28 dob; zakupy plonu w portach >= 95%; glowy < 5 000 dalej <= 10; wojsko w wojnie 95-115 tys. albo zgoda Jeffa na spadek.

**184 podatek wojenny korony ([D] 00:10 C; wgranie: etap 5, pozycja 5.6, razem z 173).**
- W etapie 2 tylko miejsce w 165 (linia wplywow) i miara potrzeby: niedoplata korony.
- *Dlaczego nie teraz:* placi go kiesa ludu (173), ktorej jeszcze nie ma; brany dzis z kas miast zmniejsza zakup plonu, a kazde 10% mniej kupionego plonu to ok. -65 zl/dobe
  u pana zamku (Z15-8); skutki, ktorych chcial Jeff ("im wyzsze podatki, tym wplyw na gospodarke"), wymagaja modelu zakupow ludu z 173 (zbytki, odziez, jedzenie).
  Do etapu 5 luke rodu w wojnie zamyka kredyt wojenny 168 ([D] 10:45), a luka korony w stanie ustalonym (zwrot ok. 55-65% naleznego w roku 2) zostaje.
- *Liczby na etap 5* [H][S]: znosny podatek wojenny od ludu 3-5% gotowki rocznie (ok. 32-54 tys. zl/dobe), 6% (poglowne 1381) = granica buntu (ok. 65 tys.);
  potrzebne ok. 30-60 tys./dobe (zwrot korony z 55-65% do >= 70% naleznego) to ok. 3-6% - skutki musza byc wyrazne: do 3% tylko mniejsze zakupy, 3-6% zakupy ciete
  (zbytki, potem sukno), ponad 6% przez kwartal - spadek zadowolenia BK i ucieczka czesci podatnikow; od majatku, nie rowno od glowy.

---

## 3. Co odlozone do etapu 3 i 5 (i dlaczego)

| Co | Dokad | Dlaczego |
|---|---|---|
| 181 statki rozbitych | etap 3 (3.3), z 164a | ok. polowa wojennego zoldu panow zamkow stoi na tym zlocie (przez zapas wojny) - zamkniecie bez wyrownania utnie ich wojsko o ok. 1/3 (swiat -9..-10%); rachunek wojska i wyrownanie przed 181 (rozdz. 2, Z) |
| zloto taboru, bandy i karawany dla zwyciezcy (Z15-3, decyzja D, Q3b) | etap 3 (3.2) | zrodlo/ujscie z niczego; w etapie 2 tylko sakwa taboru wsi z 112 (S16) z pomiarem band |
| lup z cial, jency, oblezenia, turnieje BK, majatki BK, handel statkami z osada, kapital nowych karawan | etap 3 (164) | zrodla z niczego - zamykane razem z ujsciami; majatki BK 286 tys./dobe (dlatego poza D - "inne z modelu") |
| clo i pensje rady BK, karawana pana bez miasta (D-8, D-9) | etap 3 | rachunek i wydatki poza budzetem rodu; male |
| 184 podatek wojenny | etap 5 (5.6) | brak kiesy ludu (173); rozdz. 2, Z |
| 111' kasy miast (K6), 162 pelny, 163 markietani, 164c notable | etap 5 (5.1-5.3) | zawor miast stoi dzis na "zakupach z niczego" 445 tys./dobe; najpierw szczelnosc i kiesa ludu (Z15-8); do tego czasu dwor 162m pod tarcza (166) |
| renty 2 / 1.5 / 0.25 i zawor miast 1/2 (Z15-5) | etap 5 (5.5, 5.7) z 173 | wyrownanie dla panow zamkow ma sens dopiero, gdy kiesa ludu przeniesie pieniadze do miast (chyba ze rachunek przed 181 wskaze (a) - wtedy etap 3) |
| obciazenie wsi 30-50% (pan + sept, Z15-0; [D] 03:45 nr 15 "2/3 tylko przejsciowo do 166") | etap 5 (5.7) z 173 | w etapie 2 udzial pana z utargu wsi sie nie zmienia: bez kiesy ludu to, czego pan nie wezmie dekretem, zostaje w kiesie wsi i wraca do niego renta 20%/dobe (dzis 68% utargu + renta = ok. 85-100% gotowki wsi) - obnizka przy 166 zmienilaby tylko ksiege. 30-50% wchodzi od razu z 173, ktora daje wsi na co wydac reszte. Do pokazania Jeffowi jednym zdaniem i do STAN-PRAC (rozdz. 6) |
| `PeaceWageShare` 0.23 | po etapie 3 | po zamknieciu zrodel D pokojowy sie zmienia |
| zwolnieni do wsi pochodzenia i do karczmy (108, 167) | etap 4 (4.1, 4.14) | brak ksiegi ludzi; do tego czasu ludnosc BK najblizszej wsi rodu (zaloga - wsi twierdzy) |
| ponowna kalibracja `WarChestDays`, R i udzialow po tempie ksiazkowym (W-11) | etap 7 (7.3) | dzis kampanie trwaja 5-15 x krocej w dniach niz w ksiazkach |
| zamki przygraniczne 75% zalogi w pokoju (W-10) | pomysl po tescie | rachunek: 90% stanu wojennego w 13 dob wystarcza |
| dlug korony wobec rodow (H-2, wersja 1: niedoplata zwrotu jako dlug korony z pulapem 60 dni, splacany przed rentami), Q4b (korona pozycza w Banku, kapital z lokat Wolnych Miast), 04 W5 (armia tylko za pieniadze), 04 W6 (zmeczenie wojna od biedy), N1 pomoc wasali na okup, N2/C4 zakladnik, C5 korona przejmuje dlug rodu bez ziemi, tarczowe 10% D dla rodow bez wojska, relief (oplata dziedzica 1/4 D) | pomysly - tylko na slowo Jeffa | zmieniaja rozgrywke ponad decyzje ([D] 05.10 "zwrot, dopoki ma z czego"); etap 2 ich nie potrzebuje - luke rodu w wojnie zamyka kredyt wojenny ([D] 10:45) |

---

## 4. Ryzyka

| # | Ryzyko | Skad wiemy | Zabezpieczenie | Miara |
|---|---|---|---|---|
| R1 | 165 wlaczone bez 166 - rody traca ok. 170 tys./dobe | 12 S1, stan 2.2 | jedno wgranie C1; wylaczenie 166 wymusza w kodzie stary zwrot (165 sprawdza `ClanBudgetEnabled`) | linia "Korona" |
| R2 | D staly zle policzony - budzet za ciasny albo za luzny | D169: 31% zdarzen, 23% zwrotu z zapasu; D169 ma warsztaty, karawany, majatki BK, przelewy w rodzie i podwojne | 169c w kroku A: D staly w 8 czesciach w logu 40-120 dob przed C; zamkniecie sumy co do 1%; E2 na biegu bazowym | `budzet-rodow.csv` |
| R3 | zalogi rosna inna droga (BK, ROT, milicja) i pulap 166 ich nie zatrzyma | +66% zalog w 120 dob bez werbunku | 169c mierzy droge przyrostu; jesli poza `SetGarrisonWagePaymentLimit` - osobna poprawka przed C1 (albo 4.7 wczesniej) | "Zalogi: przyrost" |
| R4 | okupy pol roku D na raty - rody wielokrotnie pojmane maja dlug rosnacy bez konca | ESENCJA 16a; M8; rachunek 178 (3.2 roku rat w wojnie) | jeniec wolny po gotowce, kolejka rat; pulap 364 D (5.2); czestosc pojman w E2 przed kodem; zajecie D3 zamiast bankructwa | "Niewola lordow i okupy" |
| R5 | kula sniezna: raty okupow i KW powiekszaja armie wygrywajacych | rachunek 178 | prog: zadne krolestwo +20% wojska ani > 85% wygranych bitew wobec biegu bez 178; jesli przekroczy - gotowka 60% zamiast 50% (jedna liczba) | "Bitwy", wojsko na krolestwo |
| R6 | E1b konczy wojny za czesto albo wcale | nowa miara T | T wzgledem progu 25% i sredniej swiata; wnioski tylko w wojnach niefabularnych, raz na 7 dob; prog po C3: pokoje z biedy 1-6 na 120 dob | linia E1 |
| R7 | rycerz obcego rodu w partii - gra/BK tego nie lubi | brak precedensu w kodzie | harness przed kodem; wariant zapasowy (rycerz-zolnierz z majatku) | 0 bledow, linia 179 |
| R8 | dezercja wedlug poziomu u AI za silna (T10 obniza morale niewyspanym) | S19 | prog: bieg bazowy + 50% (ok. 30/dobe); wylacznik `DesertionLawForAi` | "Dezercja AI wedlug przyczyny" |
| R9 | bandy bogatsze po 112 (sakwy taborow) | 15 Z15-3: zysk band z taborow x10 | pomiar band w B; wylacznik `VillagerPurseSurvives` | prog +20% |
| R10 | pieniadz swiata rosnie w etapie 2: baza dodatnia (doby 31-40 +214..+355 tys./dobe), B dodaje zamkniete ujscia (ok. +150 tys./dobe) | biegi 09.10, 12 B18 | przyjete do etapu 3 (zrodla zamyka 164a); progi jako roznice wobec biegu bez paczki; jesli bieg bazowy 120 dob pokaze wzrost > 1% pieniadza na 28 dob przez caly bieg - pokazac Jeffowi przed C i rozwazyc wczesniejsze 164a | "Pieniadz swiata" |
| R11 | dzwignie MCM: plik Jeffa nie ma nowych kluczy, a stare domyslne sie zmieniaja (`IronBankIncomeDays` 60 -> 15, `CrownWageRefundGarrisons`, `CrownRefundOwnTownsCut`) | M13 | lista kluczy w opisie wgrania; sprawdzenie `Armoury.json` przed wgraniem | - |
| R12 | Dothrakowie po 175 (konni, zold +26-49%) wychodza ok. -38..-48% w wojnie | ESENCJA, S12, rachunek 182 | E2 na biegu bazowym; regula korekty daru 10% -> 15% takze przy progu wojska; lup w D (najezdzcy) i KW | prog Dothrakow |
| R13 | duze wgranie C trudno diagnozowac | wersja 1: 7 paczek naraz | krok C w trzech czesciach (C1-C3), kazda z testem 40 dob; osobne wylaczniki; linia startu z lista wlaczonych | - |
| R14 | zapis: dlugi okupow, KW, kontrakty, liczniki sluzby i norm zalog rosna w napisie zapisu | limit 32 KB na napis | `SaveText.Sync` w kawalkach (paczka 161); test zapisu z doby 362 i 728 | 0 napisow > 32 767 B |
| R15 | wydajnosc (dzienne petle po rodach i krolestwach) | doba 13-24 s | liczenie raz na dobe, bez petli godzinnych; optymalizacja na koniec ([D] 08.10) | czas doby +<5% |
| R16 | kredyt wojenny: lupy nie splacaja, Bank pustoszeje, po dlugiej wojnie fala zajec D3 | wojny trwaja prawie caly rok (T9: 338 z 344 rodow w wojnie w dobie 120) | limit ze zdolnosci splaty (ziemia + 30 dni lupow, bez statkow); 0.40 D dziennie; 50% lupow na splate; wolny kapital Banku >= 1 mln; pokoj z biedy (E1b) | KW w "Dlugi", D3 w roku |
| R17 | tarcza dworu: kasy miast puchna od zlota pod znacznikiem | 162m | znacznik nigdy wiekszy niz nadwyzka kasy ponad cel; zloto w kasie miasta dalej kupuje plon i towar | "Kasy miast": pod znacznikiem dworu |
| R18 | rok 2: zapas koron zuzyty, zwrot spada do ok. 55-65% naleznego | rachunek 165 | zapas po 1/360 (2 lata zamiast 1); prog roku 2 >= 50%; KW w rodzie; podatek wojenny 184 w etapie 5 | "Korona" w dobach 365-728 |
| R19 | zalogi pelne w wojnie zjadaja pulap - pan zamku bez partii w polu | 166 (zaloga pierwsza) | pomiar w C1: partie panow zamkow w wojnie >= 90% dzisiejszych (z KW po D); jesli nie - zaloga w wojnie najwyzej 70% pulapu (jedna liczba) | "Budzet rodow" |

---

## 5. Pytania do Jeffa (tylko zmiany rozgrywki spoza decyzji)

Twoje dwa pytania z wersji 1 sa rozstrzygniete: o 10:45 (bez stalego "-4% wojska" - w wojnie kredyt splacany z lupow; paczka 168) i o 10:50 (krol i nastepca
tronu do korony zdobywcy, zdobywca 1/10; paczka 178). Zostaja trzy drobne:

**5.1. Podloga 30 ludzi takze dla Twojej druzyny?**
Gdy nie placisz zoldu, ludzie odchodza (WarLedger) - u AI partia nie spadnie przez to ponizej 30 ludzi (swita banneretu; dzis potrafi stopniec do samego lorda).
(a) **ta sama podloga dla Ciebie** - jedna regula; (b) tylko AI - Ty mozesz stracic wszystkich. Rada: (a). Domyslnie (a), do zmiany jednym suwakiem.

**5.2. Okupy: rod nie jest winien wiecej niz rok swojego dochodu?**
Glowa rodu kosztuje pol roku dochodu. Rod, ktorego glowa wpada w niewole kilka razy z rzedu, mialby dlug okupow rosnacy bez konca (raty i tak sa ograniczone
do 15% dochodu dziennie, wiec splata trwa latami). (a) **pulap: razem najwyzej rok dochodu** - kolejny okup ponad to jest mniejszy (porywacz dostaje mniej);
(b) bez pulapu - dlug rosnie, az wierzyciel zajmie dochod wsi. Rada: (a), "wedlug majatku, bez ruiny". Domyslnie (a).

**5.3. Okup za krola: placi jego rod czy jego krolestwo?**
Twoja regula: glowa rodu placi pol roku dochodu swojego rodu - krol tez (domyslnie tak zrobimy). Historycznie okup krola placil caly kraj (podatek nadzwyczajny),
wtedy bylby liczony takze od podatkow korony (ok. 2-2.5 mln zl u duzego krolestwa) i placony ze skarbca. (a) **jego rod** - jedna regula, prosciej;
(b) skarbiec jego krolestwa. Rada: (a).

**Informacje dla Ciebie (bez pytania, wynikaja z Twoich decyzji):** prog wojska w testach 95-115 tys. w druzynach lordow zamiast 85-100 tys. z planu (Twoje
"ok. 108 tys." z 05.10; 85 tys. zostaje twarda podloga); jako dluznik masz ta sama drabine co AI (zajecie dochodu wsi i kiesy ponad 38 tys., w ostatecznosci
wyprzedaz nadwyzek, nie Twojego ekwipunku); korona bierze 1/9 takze z Twojego lupu z ekranu po bitwie; renta od korony tylko za prawdziwa sluzbe (armia,
oblezenie, ziemia wroga - karawana z towarzyszem nie wystarczy); Twoj kontrakt najemnika - jak dzis w grze.

Bez pytania (projekt z uzasadnieniem w rozdz. 2): rezerwa korony 0.5 mln i oddawanie zapasu 1/360, niedoplata zwrotu przepada, wagi rent 3/1/0.25 i warunek sluzby,
dary 25% / 10% (korekta 15%) dzielone stalymi wagami, kontrakt 1.3 x zold, udzialy budzetu, R z podlogami rodziny, zold rycerza 24 zl, kredyt wojenny 0.40 D
dziennie i limit (15 dni D z ziemi + 30 dni lupow), raty 10% / 15%, prog wyprzedazy 728 dni, podloga zajecia 38 000 (Twoja), wspolczynniki okupu (Twoje),
kolejnosc wgran (6 krokow).

---

## 6. Poprawki w dokumentach (przy najblizszej edycji)

| Miejsce | Jest | Ma byc |
|---|---|---|
| PLAN rozdz. 2, pytanie C | otwarte | TAK (04:25) - paczka 183 |
| PLAN 2.2 | caly 108-114 | 110, 112, klucz 114 w etapie 2; 108/109/113 w 4.1/4.4; 111' w 5.1 |
| PLAN 2.3 | "trybut na raty" | reparacje korona -> korona (11 krok C), rody nie placa |
| PLAN 2.7 | zalezy od 1.14; "85-100 tys." | kod bez czekania na 1.14 (test koncowy po 1.14); **zmiana kryterium odbioru** (informacja dla Jeffa w rozdz. 0 pkt 8 i 5): 95-115 tys. (decyzja 05.10 "ok. 108 tys." +-10%), twarda podloga 85 tys. |
| PLAN rozdz. 4 "Test roczny zaliczony, gdy" | "w wojnie 85-100 tys., w pokoju 30-40 tys." | w wojnie 95-115 tys. (podloga 85 tys.), w pokoju 35-40% wojennego (bieg z wymuszonym pokojem); test etapu 2 dwuletni |
| PLAN 2.9 | "bez limitu dziennego" | limit jest; dezercja gry z limitu zoldu wylaczona razem z budzetem 166; w 183 wlaczenie C i podloga 30 ludzi |
| PLAN 2.12 | "raty do 15% dochodu"; "kapital Banku w kasie Braavos" | Bank 10% D, wszystkie raty 15% D; kredyt wojenny ([D] 10:45); kapital jak w 168 (dar startowy w ksiedze, zysk do Braavos) |
| PLAN 2.13 | "korona bierze 1/9" | + krol i nastepca tronu do korony zdobywcy, zdobywca 1/10 ([D] 10:50) |
| PLAN 3.3 | statki | paczka 181 zaprojektowana w PROJEKT-ETAP2 rozdz. 2, Z (z rachunkiem wojska przed wgraniem) |
| PLAN 5.7 / 5.12 | "zamek 1"; D-12 | zamek 1.5 (Z15-5); 5.12 skreslic |
| PLAN 3.2, 5.7, 5.11, 6.1 | "Ty" | zdecydowane 04:25-04:55 |
| PLAN etap 5 "skonczone, gdy" | kiesa panow zamkow -10% na 28 dob | -2% na 28 dob i udzial panow zamkow w D swiata >= 30% (15 rozdz. 5) |
| OB rozdz. 12, KL 5.1 | kolejnosc paczek (zatwierdzona 08.10) | zmieniona w PLAN 09.10 (04:00; Jeff 04:15 "zgoda na cala prace", 04:25 odpowiedzi na pytania PLANU): 111' i 162 pelny do etapu 5; skutek dla dworu - tarcza dworu w 166 |
| OB 10.2 a STAN-PRAC 05.10 | "wojna 85-100 tys." a "ok. 108 tys." | zrodla sprzeczne; w projekcie 108 +-10% z podloga 85 tys. |
| OB 7.1, 12 W-5 | trybut 0.5 D z kies rodow | reparacje korona -> korona (S5) |
| OB K4, 7.1, wiersz 20 | zapomoga gry | nie istnieje (BK ja wylacza - S6) |
| audyt 05 L8, D1, C1 | "okupow AI-AI nie ma" | sa (barter gry, 10% dziennie) - S10 |
| `15-BILANS-PANOW-ZAMKOW.md:137` | "1415: zold po 40 dniach" | w 1415 zold od pierwszego dnia (kontrakt); 40 dni - XII-XIII w. |
| STAN-PRAC | dwa zapisy o udziale korony (1/3 i 1/9; "za krola/wodza 2/3") | 1/9 wasali (03:45); krol i nastepca - korona zdobywcy, nagroda 1/10 (10:50); wodz armii spoza rodu krola - 1/9 |
| STAN-PRAC (03:45 nr 15) | "2/3 tylko przejsciowo do 166" | dopisac: w etapie 2 udzial pana sie nie zmienia (bez kiesy ludu renta i tak oddaje reszte), 30-50% wchodzi od razu z 173 - pokazac Jeffowi |
| A `PopulationLaw.cs:26-28` (opis w kodzie, przy B) | "Zamki bez zmian" | dotyczy dochodu; kase zamku zeruje regulator gry, a od 110 - tylko w dol |
| raport 09 (Bardi, Peruzzi; Jan II) | liczby jak pewne | Villani [H?]; Jan II zaplacono 400 tys. - ponad 1 mln ecu z 3 mln |

---

## 7. Krytyka i odpowiedzi

51 uwag krytyki wersji 1 (09.10). Kazda sprawdzona w kodzie (A, gra, BK), w logach (`kopiaT9-120`, `kopia-sklad6`, `kopia-174b`, `kopia-grupa11`, `kopia-sklad8`)
albo w STAN-PRAC. Wynik: wszystkie 51 przyjete w calosci albo w istocie; czesci odrzucone - jednym zdaniem przy uwadze. Dwie decyzje Jeffa wydane po napisaniu wersji 1
(10:45, 10:50) zamknely pytania 5.1 i 5.2 i zmienily odpowiedz na K6, K14 i K26.

**Krytyczne**

- **K1** (pieniadz swiata, baza -41 tys.) - PRZYJETE. Sprawdzone: "Pieniadz swiata" w dobach 31-40 rosnie we wszystkich biegach po 08.10 (sklad6 +214, 174b +286,
  grupa11 +236, sklad8 +355 tys./dobe), zapis 362 ma 227.7 mln wobec ok. 190 mln na starcie; -41 tys. pochodzi z T9 sprzed zamkniecia ujsc BEE (12 B18). Zmiany:
  bieg bazowy w kroku A (169c), progi jako roznice wobec biegu bez paczki (rozdz. 1, B, C, D), 181 i rozdz. 0 pkt 11 uzasadnione wojskiem, nie dziura w pieniadzu.
  Odrzucona tylko ekstrapolacja "+300 tys./dobe po B, ok. 50% pieniadza rocznie": okno 31-40 zawyza (T9 w tym oknie +102 tys., w dobach 31-120 -52 tys.), liczbe da bieg bazowy.
- **K2** (glowy a rodziny, W-4) - PRZYJETE. Sprawdzone: T9 d120 32 glowy < 5 000, z nich 20 z rodzina >= 10 tys. (Royce, Greyjoy, Smallwood...). Zmiany: czlonek dopelnia
  glowe do max(5 000; koszt dnia) i R = max(20 000; 20 D) + 5 000 x dorosli (oba warianty z uwagi, bo dzialaja na rozne drogi: kiesa dnia i zapas wojny); E2 z miara
  PLAN (glowy w dobach 120, 364, 728) jako warunek wejscia C1; rozdz. 0 pkt 9: "cel", nie "wynik".
- **K3** (okup "nowy czeka", R = okup glowy) - PRZYJETE. Rachunek potwierdzony (gotowka ok. 7.5 D, reszta ok. 3.2 roku rat). Zmiany: jeniec wolny po gotowce zawsze,
  kolejka rat; czestosc pojman w 169c i w E2 przed 178; prog kuli snieznej (+20% wojska, 85% bitew z etapu 1); nowe uzasadnienie R. Dodane ponad uwage: pulap
  dlugu okupow 364 D (pytanie 5.2), bo sama kolejka przy pojmaniu czesciej niz co ok. 3 lata rosnie bez konca.
- **K20** (dwor 162m w kasie miasta a regulator gry) - PRZYJETE, wariant (a). Sprawdzone: A `SoldierPay.cs:456-462` (do pana wraca ok. 17%, tarcza wygasa po ok.
  2 tygodniach), OB 12 trzymal 162 ze 111'. Zmiana: tarcza dworu bez wygasania (schodzi tylko zaworem i danina, nie wieksza niz nadwyzka), licznik "dwor: skasowane
  przez regulator" z progiem 0, E2 z regulatorem kas miast.

**Wazne**

- **K4** (Z/181 "nie zmieni wojska") - PRZYJETE. Zdanie usuniete; w 166 i Z wprost: ok. polowa wojennego zoldu panow zamkow stoi na zlocie z niczego; plan etapu 3:
  rachunek wojska przed 181 i wybor wyrownania albo jawnego spadku.
- **K5** (rok 1 na zapasie, rok 2 ponizej 70%) - PRZYJETE. Oddawanie zapasu 1/360 (zapas na ok. 2 lata), stan ustalony roku 2 opisany (ok. 55-65%), progi rok 1 >= 65%
  i rok 2 >= 50%, test do doby 728; w 180 dopisane, ze renty roku 1 placi w duzej czesci zapas koron.
- **K6** (-4% to gorna granica; Dothrakowie; prog dezercji 0.3%) - PRZYJETE w istocie. Prog dezercji AI: bieg bazowy + 50% (ok. 30/dobe); regula korekty Dothrakow
  takze przy progu wojska; E2 z 175 przed C1. Czesc "podac przedzial -4..-10% w pytaniu 5.2" odpada: Jeff o 10:45 odrzucil staly spadek, luke zamyka kredyt wojenny.
- **K7** (Straz bez zoldu: pulap w zlocie nie hamuje, dar wedlug ludzi) - PRZYJETE. Pulap w ludziach dla wojska bez zoldu (166), dar dzielony stalymi wagami (182).
- **K8** ("wlasne" tylko dla zamkow) - PRZYJETE. "Wlasne" = wplaty do kasy kazdej osady rodu (zamek i miasto) x udzial pana x odplyw; ten sam licznik dla gracza w "Obieg".
- **K9** (rycerze traca kiese bez wojska) - PRZYJETE. Sprawdzone: 37 rodow gentry bez ludzi traci srednio 116 zl/dobe, Fyomanoving 39 388 -> 3 994 przy zoldzie 0;
  oba miejsca BK istnieja (`BKVillageSupplyAutoBehavior.cs:349`, `BKEstateAutoSlavePurchaseBehavior.cs:106`). Zmiany: linia "Wydatki rycerzy" w 169c, pulap majatku
  w 166/179, test "glowy gentry < 5 000 w dobie 120: 0".
- **K10** (warunek sluzby rent nie mierzy sluzby; okupy gracza; gracz-dluznik) - PRZYJETE. Sluzba = armia, oblezenie, ziemia wroga; zaloga wobec stalej normy krolestwa;
  licznik "okupy gracza / D gracza" i liczby gracza w 178 i rozdz. 0 pkt 6; gracz-dluznik na tej samej drabinie (patrz K25).
- **K11** (D4 jako bankructwo przy prognozie > 364) - PRZYJETE. D3/D4 licza sie jako zajecie; D4 tylko po 14 dobach zerowego zajecia albo prognozie > 728 dni.
- **K12** (mnoznik biedy nie schodzi ponizej 0.8; D opoznione o 28 dob) - PRZYJETE. Ziemia z dzisiejszych lenn i bez zajetego dochodu; G < 0.25 R -> x 0.5; jeden warunek
  pozyczki (K35).
- **K21** (dezercja z limitu zoldu wylaczana dopiero w 183; zalogi) - PRZYJETE. Sprawdzone: gra `DefaultPartyDesertionModel.cs:50-76` (do 20 ludzi dziennie), wolane
  z A `DesertionLaw.cs:144-145`, limit zalogi `GarrisonPartyComponent.cs:43`. `AiWageLimitDesertionOff` w wylaczniku 166; zwolnienia obejmuja zalogi (najpierw nadwyzka
  zalog w pokoju, ludzie do wsi twierdzy); test zalog pokojowych.
- **K22** (progi pokoju niemierzalne) - PRZYJETE. Sprawdzone: w T9 wojna u 19 rodow w dobie 1, 307 w 31, 338 w 120. Bieg z wymuszonym pokojem (E), kazda miara jako
  kolumna albo linia logu, definicje w rozdz. 1.
- **K23** (PLAN rozdz. 4 dalej 85-100 / 30-40; -4% jako fakt) - PRZYJETE. Poprawka PLAN rozdz. 4 w rozdz. 6; rozdz. 0 pkt 8 przepisany. Pytanie 5.2 wersji 1 rozstrzygnal
  Jeff o 10:45, wiec osobny prog dla odpowiedzi (b) nie jest potrzebny.
- **K24** (1/9 od sztuki, prog 1 600 zl; sprzedaz lupu przez gre) - PRZYJETE. Sprawdzone: A `MenPurse.cs:498-531` sprzedaje sztuka po sztuce, gra
  `PartiesSellLootCampaignBehavior.cs:38` -> `SellItemsAction`. Prog usuniety (decyzja go nie ma), rozliczenie raz na dobe z licznikow, latka na sprzedaz lupu lordow AI,
  test 11.1% +-0.5 pp. Dodane ponad uwage: 1/9 z lupu gracza z ekranu po bitwie (jedna regula).
- **K25** (gracz-dluznik inna drabina) - PRZYJETE. Ta sama drabina z D4 (nadwyzki zbrojowni, karawany, warsztaty; nigdy ekwipunek gracza) - [D] 05.10 "w ostatecznosci
  wyprzedaje wszystko"; informacja dla Jeffa w rozdz. 5 zamiast pytania, bo to wykonanie decyzji.
- **K26** (trzeci stopien okupu, wzor krola; 5.1 domyslnie 2/3) - PRZYJETE w istocie. Dwa stopnie jak w decyzji (glowa 0.5, kazdy inny lord 60/364); krol liczony jak
  glowa rodu, placi jego rod; wzor ze skarbcem - pytanie 5.3. Czesc "5.1 domyslnie 2/3" nieaktualna: Jeff o 10:50 rozstrzygnal wielkich jencow (krol i nastepca do
  korony zdobywcy, 1/10 dla zdobywcy, wodz armii spoza rodu krola - 1/9).
- **K27** (dlug korony wobec rodow poza decyzja) - PRZYJETE w istocie. `CrownDebtToClans` false, niedoplata przepada ([D] 05.10 "dopoki ma z czego"), mechanizm na liscie
  pomyslow w rozdz. 3; nie jako pytanie 5.x, bo po [D] 10:45 luke rodu zamyka kredyt wojenny i etap 2 tego dlugu nie potrzebuje. E2 liczy jeden wariant (bez dlugu).
- **K28** (dlug rodu bez ziemi na krolestwo) - PRZYJETE. Dlug zostaje w ksiedze Banku jako niesciagalny, linia "dlug bez platnika"; sprzecznosc z rozdz. 3 usunieta.
- **K29** ("reczny test Jeffa") - PRZYJETE. Harness niewoli w autotescie (2.14, 178): niewola gracza, jeniec AI u gracza, krol jako jeniec.
- **K30** (miara T: 25% niedoplaty -> T ok. 0.65) - PRZYJETE z inna kalibracja: T = (n - max(25%; srednia swiata)) / 15% - wzgledem progu i sredniej, bo w roku 2
  srednia niedoplata (ok. 40-50%) przy progu bezwzglednym 25-40% dawalaby T = 1 wszedzie; test po C3 (pokoje z biedy 1-6 na 120 dob, 0 w wojnach fabularnych).
- **K31** (decyzja 15 "2/3 tylko przejsciowo do 166") - PRZYJETE. Wyjasnienie w rozdz. 3 i w rozdz. 6 (wpis do STAN-PRAC i jedno zdanie dla Jeffa).
- **K32** ("Polnoc mniej liczna - tak jak chciales") - PRZYJETE. Sprawdzone: Q4(a) wymienia tylko Zelazne Wyspy, Smocza Skale i Sarnor; 02:35 dotyczylo piechoty.
  Zdanie usuniete, Polnoc ma prog -25% jak reszta.
- **K33** (zalogi w wojnie pelne bez progu) - PRZYJETE. W wojnie zaloga jak dzis w grze i finansowana pierwsza; prog >= 95% biegu bazowego; R19 dla partii panow zamkow.
- **K34** (kategorie D a D169) - PRZYJETE. Sprawdzone: A `ClanIncomeBook.cs:290-301` (`inflow = a + b + r.Today`, `a` z warsztatami, karawanami, majatkami BK, "za tier").
  Kategorie "majatek" (w D) i "inne z modelu" (poza D); zamkniecie sumy na wszystkich czesciach (z K17).
- **K35** (warunek pozyczki martwy) - PRZYJETE. Jeden warunek z A `ClanIncomeBook.cs:546` (wojna, G < R, zold naliczony > pulap) - teraz warunek kredytu wojennego.
- **K36** (kontrakt gry dla AI) - PRZYJETE. Sprawdzone: gra `DefaultClanFinanceModel.cs:504-513` placi kazdemu rodowi w sluzbie najemnej, takze AI. Dla AI kontrakt gry 0
  i bez dopisku do `MercenaryWallet`; gracz na kontrakcie gry - pozycja PLAN 2.10 dotyczy AI, a platnik jest prawdziwy (bez pytania).

**Drobne**

- **K13** (liczby korony bez okna, zdanie o 27%) - PRZYJETE. Jedno okno T9 31-120, "dany/nalezny"; zdanie o 27% zastapione liczba z E2 i "wojna caly rok jest ciezsza
  niz w historii"; jeden prog wojska w calym projekcie (95-115 tys.).
- **K14** (okup krola bez miejsca w kolejnosci wydatkow) - PRZYJETE w istocie, inna droga: krol placi jak glowa rodu (K26), wiec "dlugu okupu korony" nie ma; w kolejnosci
  165 jest za to nagroda 1/10 dla zdobywcy ([D] 10:50), a raty krolewskie wplywaja do skarbca zdobywcy jak inne wplywy.
- **K15** (`AddMercenaryIncome` dla AI) - PRZYJETE (jak K36).
- **K16** (K3 0.5 i Z8 gracza w polu) - PRZYJETE. Klucz 1.0 (`CrownRefundOwnTownsCut`): z monety wraca najwyzej 0.5 + s x (v - 0.5) <= 1; sprawdzenie w `gracz2.py` przed C1.
- **K17** (test zamkniecia sumy) - PRZYJETE (z K34): kategorie "przelewy w rodzie" i "podwojne".
- **K18** (odbiorca, ktory zniknal) - PRZYJETE. Zapasowy odbiorca w 2.0b, licznik "bez odbiorcy" z progiem 0.
- **K19** (zmiana progu PLAN w tabeli "bez zmiany decyzji"; sprzeczne zrodla) - PRZYJETE. Rozdz. 6 nazywa to zmiana kryterium odbioru, informacja dla Jeffa w rozdz. 0
  i 5; 85 tys. twarda podloga; sprzecznosc OB 10.2 / STAN-PRAC 05.10 zapisana.
- **K37** (jeden `try` dla krokow korony) - PRZYJETE. Sprawdzone: A `ArmouryBehavior.cs:1348`. Osobne `try` w 2.6 i 2.0b.
- **K38** (bledne odwolanie `PopulationLaw.cs:157-171`) - PRZYJETE. Sprawdzone: tam `PeopleOf`; odwolanie do regulatora gry i galezi 110 w B.
- **K39** (ujemna kiesa glowy) - PRZYJETE. Sprawdzone: gra `Hero.cs:771` (`MathF.Max(0, value)`); bankructwo = kiesa 0 i zold przyciety przez >= 7 dob.
- **K40** (Inni w liczniku glow) - PRZYJETE. Sprawdzone: `ROTclan_126` "Others" z kiesa 0 w T9 d120; rody nieumarlych poza linia i CSV.
- **K41** (test 180 a udzialy wstrzymane) - PRZYJETE. Suma rent + udzialy wstrzymane = reszta wplywow.
- **K42** (D3 <= 2 w 120 dobach luzniejsze niz PLAN) - PRZYJETE. <= 1 w 120 dobach i <= 5 w roku.
- **K43** (104 a 151 tys.) - PRZYJETE. Wplywy 151 tys., zwrot 321 tys. (T9 31-120); 104 tys. to zejscie zapasow, nie wplywy.
- **K44** (brak "kas miast" w 169c) - PRZYJETE. Linia "Kasy miast" (cel regulatora, skasowane, tarcza, zawor).
- **K45** (1/180 liczone dwa razy przy racie reparacji) - PRZYJETE. Rata = najwyzej 50% wplywow dnia, ktore juz zawieraja oddawany zapas (teraz 1/360).
- **K46** (podloga 30 ludzi dla gracza bez pytania) - PRZYJETE. Sprawdzone: OTWARTE-2026-09-01 pkt 2 "do decyzji" (dla AI). Pytanie 5.1.
- **K47** (dar wedlug liczby ludzi - Z8) - PRZYJETE (jak K7): stale wagi.
- **K48** (pozyczka na okup martwa; szansa zgody) - PRZYJETE. Pozyczek na okup nie ma; jedna regula: stale 10% dziennie.
- **K49** (podloga zajecia max(38 000; 10 dni zoldu)) - PRZYJETE. 38 000 jak w decyzji 07.10 pyt. 7, z uzasadnieniem (10 dni zoldu pocztu 135 ludzi).
- **K50** (OB 12 "nieaktualne" bez zrodla) - PRZYJETE. Zrodlo zmiany (PLAN 09.10 04:00, Jeff 04:15 i 04:25) w 2.0a pkt 9 i rozdz. 6.
- **K51** (podzial kroku C, narzedzie progow) - PRZYJETE z jedna zmiana: C1 = 165 + 166 + 162m + 185 + 182, C2 = 180, C3 = 179 + 183. 182 idzie z C1, nie z C2,
  bo budzet 166 bez daru tnie Straz i Dothrakow juz w tescie C1 i ten wynik bylby falszywy. Narzedzie progow i plik bazowy - krok A.

---

## 8. Zrodla

- Plan: `docs/PLAN-DO-KONCA-MODA-2026-10-09.md` (etap 2, warunek "skonczone, gdy", rozdz. 4 test roczny, etap 1 prog bitew); decyzje: `docs/STAN-PRAC.md`
  (05.10, 07.10 pyt. 7-8, 08.10 11:50, 09.10 00:00-10:50, w tym 10:45 kredyt wojenny i 10:50 wielcy jency).
- Projekty: `docs/PROJEKT-EKONOMIA-OBIEG-2026-10-08.md` (Z1-Z9, K3, 6.1-6.4, 7.1, 8.1-8.3, 10.2, 12, 14.1), `docs/PROJEKT-KIESA-LUDU-2026-10-09.md` (pomiar A, haki),
  `docs/OTWARTE-2026-09-01.md` (pkt 2).
- Audyty `docs/audyt-2026-10-09/`: 04 (W2-W6, L3-L15, N1-N8), 05 (L1-L9, B1-B7, C1-C7), 11 (kroki A-C), 12 (B1-B18, D-1..D-13, W-1..W-11, Q1-Q4, E2, 5.3-5.7),
  15 (Z15-0..Z15-8), ESENCJA.
- Rozpoznanie (scratchpad sesji 7016f733): `etap2\stan.md`, `etap2\projekty.md` (S1-S26, M1-M18), `etap2\historia.md` (przeliczenia [H]); skrypty `etap2\narzedzia\`,
  `etap2\skrypty\`, `krytyka-etap2\` (a1-a4, ps.py - sprawdzenie uwag); wersja 1 projektu: `etap2v2\PROJEKT-ETAP2-v1.md`.
- Pomiary: `kopiaT9-120\` (T9 120 dob), `kopia-sklad6\`, `kopia-174b\`, `kopia-grupa11\`, `kopia-sklad8\` (40 dob i zapis z doby 362), `budzet-rodow.csv`,
  linie "Pieniadz swiata" i "Pieniadz swiata (bilans)".
- Kod: A `KingdomTreasury.cs`, `IronBank.cs`, `ClanIncomeBook.cs`, `SoldierPay.cs`, `MenPurse.cs`, `WarLedger.cs`, `DesertionLaw.cs`, `Settings.cs`, `ArmouryBehavior.cs`,
  `PopulationLaw.cs`; CS `PovertyPeace.cs`; RC `FairRansom.cs`; gra `RansomOfferCampaignBehavior.cs`, `PlayerCaptivityCampaignBehavior.cs`, `HeroSpawnCampaignBehavior.cs`,
  `ClanVariablesCampaignBehavior.cs`, `DefaultClanFinanceModel.cs`, `DefaultPartyDesertionModel.cs`, `GarrisonPartyComponent.cs`, `PartiesSellLootCampaignBehavior.cs`,
  `PlayerEncounter.cs`, `Hero.cs`; BK `BKGentryBehavior.cs`, `CallBannersGoal.cs`, `BKVillageSupplyAutoBehavior.cs`, `BKEstateAutoSlavePurchaseBehavior.cs`;
  Naval `NavalShipDistributionCampaignBehavior.cs`, `ChangeShipOwnerAction.cs`; galezie `paczki/110-k5-kasa-zamku`, `paczki/112-k7-utarg-wsi`, `paczki/114-porzadki`.

---

## Odpowiedzi Jeffa (09.10 ok. 11:05) - wiazace przy wykonaniu

1. Podloga 30 ludzi w druzynie przy niezaplaconym zoldzie - takze dla druzyny gracza (jedna regula).
2. Pulap dlugu z okupow: razem najwyzej rok stalego dochodu rodu; kolejny okup ponad pulap mniejszy (porywacz dostaje mniej).
3. Okup za KROLA placi SKARBIEC jego krolestwa (wariant b, jak w historii), nie jego rod; okup wiekszy - ok. 2-2.5 mln zl u duzego krolestwa (wylicz z tego samego wzoru
   majatku, ale od dochodu korony); gdy skarbca nie starcza - raty z biezacych podatkow korony (jak "okup krola Jana" w ratach) i dlug korony wobec porywacza.
   Nastepca tronu i pozostali czlonkowie rodu krolewskiego - jak glowa/lord rodu (placi rod); przejecie przez korone zdobywcy (decyzja 10:50) bez zmian.
