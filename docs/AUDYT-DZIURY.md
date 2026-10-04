# Audyt dziur w naszej ekonomii (2026-10-04)

Zgloszenie Jeffa: "przeaudytuj raz jeszcze nasz system ekonomiczny, czy nie ma w nim dziur".
Trzy niezalezne przeglady kodu (obieg towarow i zlota, wykorzystanie przez gracza, zderzenia z modami)
plus rozpoznanie werbunku. Liczby to wyliczenia z kodu, NIE sprawdzone w grze.

Status: NAPRAWIONE (wpis CHANGELOG) / DO ZROBIENIA / DECYZJA JEFFA.

## A. Naprawione dzis

| # | Dziura | Wpis |
|---|---|---|
| A1 | Ciecie jedzenia -40% naliczalo sie 3-4 razy (ROT -> NavalDLC -> BEE woluja sie nawzajem) - partie jadly 13-22% normy | 23 |
| A2 | Stany statyczne (WorkshopLaw - stare przedmioty, IronBank, MarketGlut, ArmsPricing, StartKit, AiGear) przeciekaly do nastepnej kampanii w tej samej sesji - ryzyko zepsucia save | 24 |
| A3 | Renty od ludnosci: latka sie nie wpinala (dwie wersje metody BK), a gdyby sie wpiela - zloto z niczego ~7 mln/dzien. Teraz przeplyw z kasy osad | 25 |
| A4 | Ochotnicy zawsze (szansa ~0.5 dziennie na miejsce) - teraz od nadwyzki rak i nedzy | 26 |
| A5 | Awans ochotnika i jego sprzet z niczego - teraz notabl kupuje na targu | 27 |
| A6 | Zloto AI za werbunek znikalo; nowa partia AI dostawala darmowy komplet (DTE) | 28 |

## B. Gracz moze wykorzystac system

| # | Dziura | Waga | Propozycja |
|---|---|---|---|
| B1 | **Ekran handlu**: nasza cena czyta zywa polke; w jednym ekranie mozna wykupic polke "na kredyt" (zloto sprawdzane dopiero przy Done), sprzedac swoje drogo, odkupic z powrotem po tej samej cenie - x0.25..x2 = 8x rozrzutu | KRYTYCZNA | stan polki z chwili otwarcia ekranu |
| B2 | **Arbitraz miedzy miastami** po decyzji 10%-x3: plyta t6 od ~730 do ~146 tys.; praca to 2-3% kosztu, wiec dolny prog naprawde dziala; indeks da sie popchnac wykupem drewna (waga 0.7 w metalu) | KRYTYCZNA | DECYZJA JEFFA: zawezic (np. 0.7-1.5) albo zostawic; srednia z 7 dni, mniejsza waga drewna |
| B3 | **Kucie w BK CRAFT**: plyta t6 kosztuje ~2.7 tys. surowca, rynek wycenia ja na ~23 tys. (zysk 2.5-15x). Odwrotnie nasza kuznia: t6 liczy stal valyrianska (40 sztabek ~40 tys.) | WYSOKA | jeden wspolny rachunek metalu dla kucia, przetopu i ceny |
| B4 | **Okup u posrednika w karczmie**: "sprzedaj wszystkich" placi wedle naszej ceny lorda (krol do 250 tys.) z niczego - mozna lapac, sprzedawac, lapac znowu | WYSOKA | u posrednika cena vanilla albo zloto z kiesy rodu jenca |
| B5 | **Bank Zelazny**: kazda nowa pozyczka przesuwa termin calego dlugu (rolowanie w nieskonczonosc, AI tez); pozyczka i splata tego samego dnia bez odsetek; bankructwo boli malo | WYSOKA | termin per pozyczka, oplata 1-2%, przy bankructwie zajecie dochodu |
| B6 | **Konie**: nasza polka (x0.25-2) na wierzchu vanilli (konie to towar handlowy) - handel konmi miedzy miastami latwy zysk | SREDNIA | waski przedzial dla koni albo wylaczyc je z naszego prawa |
| B7 | Przetop sztabek oplacalny na kazdym stopniu (surowka 58 vs 87 ... valyrianska 428 vs 1000) | NISKA | ceny sztabek = koszt lancucha x1.15 |

## C. Rzeczy z niczego / w nicosc (nasz kod)

| # | Dziura | Waga | Propozycja |
|---|---|---|---|
| C1 | Stajnie: hodowca daje konia z niczego, gdy targ pusty (Stables.cs:447) | SREDNIA | kon tylko z polki/wsi, inaczej brak zakupu |
| C2 | Kowal: zamowienie kompletu (SmithMenu DoOrderKit) - zloto w nicosc, przedmioty z niczego; oplaty kowala i naprawy - zloto w nicosc (SmithMenu, Forge) | SREDNIA | towar z polki miasta, zloto do miasta |
| C3 | Warsztaty: vanilla/BK potraca dzienny koszt warsztatu W NICOSC obok naszych plac (~10 tys./dzien) | SREDNIA | koszt dzienny 0 dla warsztatow uzbrojenia (place juz ida do miasta) |
| C4 | Bank: kapital 5 mln poza swiatem; odsetki nigdy nie wracaja | SREDNIA | skarbiec = kasa Braavos |
| C5 | DTE niszczy zbrojownie rozwiazanej partii i co tydzien kasuje "nadmiar" zbroi; lup kopiuje zbrojownie przegranych bez czyszczenia | SREDNIA | zbrojownia rozwiazanej partii -> tabor osady |
| C6 | Kryjowka: 150 + 120 zl za bande i 3 przedmioty z niczego; scalone bandy traca zloto | NISKA | lup = prawdziwe zloto i tabor band |
| C7 | Spalona ziemia: zboze z furazu z niczego | NISKA | z polki wsi |
| C8 | Samonaprawa wojska bez materialow | NISKA | - |
| C9 | AiGear: kupiona uszkodzona sztuka trafia do zbrojowni jako cala | NISKA | kupowac tylko bez modyfikatora |
| C10 | Bandy: 2 zl na glowe przy narodzinach; sprzet do awansu kasowany, a potem odtwarzany z szablonu przy smierci | NISKA | - |

## D. Zderzenia z modami i wydajnosc

| # | Problem | Waga | Propozycja |
|---|---|---|---|
| D1 | Zima wieloletnia: WinterBite (-50% na polnocy) x BEE pory roku (zima 0.7) = 0.35 produkcji wsi; partie +50% jedzenia + RBL | SREDNIA | DECYZJA JEFFA: jedno zrodlo zimy |
| D2 | Pula wyrzutkow zjedzona 1. dnia (6248 z 6562), potem 70-380 pustych band dziennie tworzonych i usuwanych | SREDNIA | bramka: ludzi >= rozmiar szablonu, nie MinBand |
| D3 | AiGear: kazdy krok zakupu skanuje polke i liczy cene (O(n^3) na wizyte, 70-80 wizyt dziennie); handel nadwyzka o polnocy ciezki - przy x8 dwa razy czesciej | SREDNIA | cache stanu polki per wersja rosteru |
| D4 | ZakupyAI: 1-2 mln zl dziennie przy ~0.9 mln dochodu rodow; ceny sierpa x2.6 w dzien; marza warsztatow ~5x (cena liczy metal po sztabkach, koszt warsztatu po rudzie) | SREDNIA | dzienny budzet od zoldu; jeden rachunek kosztu |
| D5 | WorkshopLaw: pula rak i dlugi warsztatow nie sa w save - po wczytaniu od zera | NISKA | zapis w save |
| D6 | Renty od ludnosci: kalibracja per kultura - asymilacja BK przestawia stawke wsi | NISKA | kalibracja per osada |
| D7 | Dwie ludnosci: nasza (wies-symbol ~50 tys.) dla rent, BK (wies 2-3 tys.) dla poboru i produkcji | DO ZROBIENIA | ujednolicic |

## E. Pobor - dalsze kroki (docs/PLAN-POBOR.md)

- Garnizon: auto-werbunek omija pule BK (nie zabiera ludzi).
- Demobilizacja: rozwiazane partie nie oddaja ludzi do ludnosci (BK ma `UpdatePopFromSoldiers`).
- Wyrzutki: zabierani tylko z hearth vanilli, nie z ludnosci BK.
