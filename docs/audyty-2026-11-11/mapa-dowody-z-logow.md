# MAPA: dowody z logow - jak naprawde przebiegaja bitwy i szturmy (stan 11.11.2026)

Tylko odczyt. Skroty sciezek:
- `L:` = `C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Armoury/`
- `M:` = `C:/Users/GAME/bannerlord-autotest/m113/logi/` (zarchiwizowane logi serii 312x-325a, 01-04.11)
- `T:` = `C:/Users/GAME/bannerlord-autotest/test/`
- Narzedzia i dane posrednie: `C:/Users/GAME/AppData/Local/Temp/claude/bfd708ab6e1d6456/264cfac3-8338-4c40-91b8-6c1304da3584/scratchpad/bad3/` (`ext.py` -> `bitwy.json`, `kon.py` -> `kon.txt`, `b1-summary.txt` = pelny blok konca bitwy 11.11 13:35).

## 1. Material

- `L:Armoury-*.log`: 12 plikow (09.11 16:36 - 11.11 13:19; starsze skasowala rotacja) + `M:*/Armoury-*.log` (22 katalogi). Razem 103 bloki konca misji "Bitwa (NB2): misja": 94 bitwy polowe, 7 aren, 1 kryjowka, 1 szturm (drugi szturm bez linii konca - wyjscie do menu).
- Bitew polowych z wrogiem >= 100 ludzi: 32. Z tego prawdziwa gra Jeffa: 6 skonczonych (09.11 17:50, 21:55, 22:30, 23:06; 11.11 13:35, 13:43) + 1 urwana w logu (11.11 13:55, ok. 660 wrogow, patrz 8). Reszta = autotesty.
- `T:at-*-konsola.txt`: 39 plikow, linie "AUTOTEST BITWA" ma 14 (od at-359). Bitew z lordem (200 vs 200-282): 14. Konsola NIE zawiera linii taktyk (0 trafien "zmian taktyki"), tylko liczniki.
- Autotesty at-376..395 (ostatnie 8) to tylko 316 elity vs 30-40 bandytow - dla oceny AI bezwartosciowe (czas 46-307 s, ciosow 1-327).

## 2. Kto wygrywa i jakim kosztem

- 32 duze bitwy: przegrani "obroncy" 29, "atakujacy" 3; strona gracza wygrywa wszystkie poza planem "slaba" (316x 02:57:58, 48 vs 150). 14/14 bitew z lordem w konsoli = "won".
- Powod widoczny w kazdym "Choragwie (CH) start": sredni tier gracza 5.5-5.6, wroga 2.5-3.4.
- Bitwy Jeffa (linia "poscig (148) ... straty"):

| bitwa | sily (gracz / wrog) | czas | pierwszy kontakt | straty wroga zab/nieprz/uszli z N | straty gracza | pociski w trafieniach |
|---|---|---|---|---|---|---|
| 09.11 17:50 | 317 / 148 | 216 s | 154 s | 41/105/3 z 149 | 3/1 z 323 | 45% (289/641) |
| 09.11 21:55 | 309 / 455 | 615 s | 341 s | 122/303/32 z 457 | 10/18 z 315 | 4% |
| 09.11 22:30 (gracz broni) | 295 / 534 | 418 s | 287 s | 132/400/7 z 539 | 1/7 z 302 | 57% (1314/2298) |
| 09.11 23:06 (gracz broni) | 297 / 710 (wystawiono 956) | 716 s | 474 s | 270/607/79 z 956 | 0/2 z 304 | 49% (1911/3923), strzalow 9641 |
| 11.11 13:35 | 314 / 273 | 185 s | 132 s | 47/160/69 z 276 | 0/2 z 320 | 84% (843/1001) |
| 11.11 13:43 | 313 / 143 | 171 s | 142 s | 36/81/26 z 143 | 2/6 z 319 | 37% |

- Wlasna linia moda (L:Armoury-2026-11-09_21-17-29.log:100772): "przegrani padli 91.7% (cel 15-40%), zwyciezca padli 0.7% (cel 5-15%)". Cel historyczny nie jest trafiany w zadnej z 6 bitew: zwyciezca 0.6-9%, przegrany 92-100% poza walka.
- Wniosek: bitwy NIE sa trudne. 304 ludzi T5.6 rozbija 956 T3.2 za 2 nieprzytomnych. To w pierwszej kolejnosci problem jakosci/skladu (elita + duzo strzelcow gracza), ale AI nic nie robi, zeby to zrownowazyc (patrz 3).

## 3. Lucznicy rozstrzeliwuja - czy konnica szarzuje na strzelcow? Z logow: NIE

- Pomiar "konnica AI pod ostrzalem (157)" w 5 bitwach Jeffa: szyko-sekund pod ostrzalem 529, z tego "oslona skrzydla trzyma" 451 (85%), "szarza konnicy RBM" 26 (5%). 23:06: 168/168 s trzyma, rozkaz Move 168/168, wrog sr. 59 m (L:...21-17-29.log:100769); 13:35: 76/76 s trzyma (b1-summary.txt linia 28 = L:Armoury-2026-11-11_13-19-13.log:2850).
- Skutek: 23:06 - 87 trafien jezdzcow AI pociskiem "bez szarzy szyku" (27 jezdzcow, 55 w konia), 0 w szarzy; szyk Cavalry 31 ludzi stal 94 s i zebral 66 trafien. 13:35 - 20 trafien (10 w konia) z 187-197 m, szyki 5 i 6 jezdzcow "PF trzyma".
- Linia sama mowi "sam pomiar, bitwa bez zmian": 157 niczego nie zmienia.
- "Jazda na strzelca (204)" reaguje TYLKO na konnego strzelca ("konny strzelec wroga zauwazony..."). 23:06: 22 epizody, 26 jezdzcow, zabitych strzelcow 0, odpedzonych 5, jezdzcy padli 8; powroty "swoi przy strzelcu 794", "smycz 107". 13:35: 1 epizod (cel = gracz), 5 jezdzcow, 2 padlo (log :2757-2773). Na pieszy szyk lucznikow nie ma zadnej kontry.
- "AI bitwy (344)" (konnica wobec strzelca): 23:06 kontry 3, "wynik dobry 2/3", "bez przewagi 13"; 13:35 kontry 1, "wynik dobry 0/1".
- Doktryna dodaje wage flanki ("wagi Flank 1-4"), ale "konnica w BehaviorFlank 0" we WSZYSTKICH 32 bitwach - flankowanie konnica nie zdarzylo sie ani razu.
- W autotestach (gracz dostaje rozkaz SZARZA) konnica wroga szarzuje czesciej: wszystkie logi razem - szarza RBM 1439 + oslona skrzydla szarzuje 1123 + wraca 939 wobec trzyma 766; trafien jezdzcow w szarzy 849, bez szarzy 276. Czyli szarza jest odpowiedzia na zblizajacego sie wroga, nie na ostrzal. Gdy gracz stoi i strzela (prawdziwa gra) - konnica stoi.
- Piechota pod ostrzalem: S2 (141, "obronca bez strzelcow idzie do zwarcia") weszlo w 16 z 32 bitew, ale tylko dla obroncy. 13:35 (b1-summary.txt linia 25): "przestrzelane 3:1", maks. 79% piechoty trafionej, wejscie w S2 1, po wejsciu taktyka FullScaleAttack -> RBMTacticDefendSplitInfantry (czyli NIE atak), "pod ostrzalem bez zwarcia do konca: druzyn 1 (padli od pociskow 116)" - 116 z 207 strat padlo od strzal bez dojscia do walki wrecz. 23:06 (wrog atakuje): S2 0 wejsc, "maks. trafionych piechoty 0.07" - prog 20% nietrafiony, choc strona dostala 1911 trafien w 716 s.
- Podejscie pod ostrzalem trwa 287-474 s (marsz krokiem sr. 1.33 m/s, start 290-314 m - log 11.11 :11617-11620). "Bitwa (291)" C: bez tarcz biegna (134 szyko-s), z tarczami "zostal krok".

## 4. Roznorodnosc - czy bitwy sa "wciaz takie same"

- Taktyki (probka jednej druzyny AI na bitwe, linia NB2 "doktryna ... [..]"): 25 z 32 bitew ma sekwencje, 7 bez (druzyny bez danych dowodcy / bandy).
  - Otwarcie: RBMTacticAttackSplitArchers 8 (6 poprzedzone "ostrzal od s. 4"), RBMTacticDefendSplitInfantry 5, RBMTacticAttackSplitInfantry 5, DefensiveEngagement 4, FullScaleAttack 2, RBMTacticDefendSplitArchers 1.
  - Wszystkie nazwy, jakie kiedykolwiek wystapily: FullScaleAttack 12, AttackSplitArchers 12, DefendSplitInfantry 8, AttackSplitInfantry 7, RangedHarrassmentOffensive 6, DefensiveEngagement 5, HoldChokePoint 2, FrontalCavalryCharge 2 (obie pozno: s. 298 i po s. 581, gdy bitwa juz przegrana), DefendSplitArchers 1. Razem 9 nazw. Ani razu: DefensiveRing (choc dodawany do listy - "+DefensiveRing"), Charge, taktyki flankowe.
  - 19 roznych sekwencji na 25 - ale roznia sie glownie czasem przelaczenia. Ten sam uklad sil = to samo otwarcie: 322x/323x "wrog 150 vs gracz 512-576" 6/6 identycznie "ostrzal od s. 4, RBMTacticAttackSplitArchers, ostrzal koniec"; "194 vs 199" 4/4 DefensiveEngagement s. 2 (potem 2/4 HoldChokePoint s. 6).
  - Zmian taktyki na bitwe: 0 w 7 bitwach, 1 w 7, 2-3 w 7, 4-7 w 11. Duzo zmian = miganie miedzy 2-3 taktykami (np. DefendSplitInfantry -> FullScaleAttack -> DefendSplitInfantry), nie plan fazowy.
- Zachowania szykow AI przed kontaktem (linie "AI bitwy (333)", n=217, logi od 09.11): ProtectFlank 88, HoldHighGround 33, RBMBehaviorArcherFlank 24, Charge 19, Advance 17, Regroup 16, ScreenedSkirmish 12, RBMBehaviorArcherSkirmish 5, Defend 3.
- Zrodla rozkazu Move (344, szyko-takty): Advance 4864, ProtectFlank 4402, HoldHighGround 3390, RBMBehaviorArcherFlank 2410, ScreenedSkirmish 2014, Defend 585, Regroup 435, ArcherSkirmish 23. Nie wystapily wcale: BehaviorFlank, BehaviorCavalryScreen, BehaviorMountedSkirmish, BehaviorVanguard, BehaviorPullBack, BehaviorSkirmishLine, rezerwa/odwod.
- Szyk (Line/Loose/ShieldWall): 57 zmian zalogowanych przed kontaktem; 11.11 :2779-2784 - szyk 13 ludzi Loose -> ShieldWall -> Line w 0.5 s; 13:35 "zmian ustawienia przed kontaktem 16", arbiter 344 zablokowal 43 z 52 wywolan. To szum, nie decyzja.
- Liczby rozrzutu przy tym samym scenariuszu (konsola, plan lord-piechota, 6 biegow): czas 194/233/247/275/430/731 s, ciosy wrecz 288-1247, strzaly 239-523. Plan lord-jazda (5): 250-312 s, ciosy 79-1104. Wynik zawsze ten sam, rozni sie dlugosc.
- Czas: 37 bitew z >= 400 trafien - mediana 300 s (171-716). Udzial pociskow w trafieniach: mediana 29% (1-84%); celnosc strzalow AI mediana 17%.
- Teren: 32 duze bitwy na 7 scenach, 16 na jednej (battle_terrain_biome_040 - zapis testowy). W grze Jeffa 4 sceny na 6 bitew.
- Zastoj bitwy (307e): 0 zastojow w 32 bitwach - bitwy sie nie zawieszaja.

## 5. Morale, panika, ucieczka (zgodne z wytyczna "panika = tylko ucieczka")

- 32 bitwy: zlamanych 4348, z tego panika silnika 92 (reszta nasze: choragwie / bol / FieldCraft). Zalaman kompanii 189; 13:35: 7 z 14 kompanii (123 ludzi), 23:06: 11 z 21.
- Los zlamanych: 13:35 - 135: zabici 13, nieprzytomni 42, uszli 69; dopadnieci w ucieczce 56 (pocisk 31, konny 13, pieszy 12) = 27% wszystkich strat. 23:06 - 401: 86/231/74; dopadnieci 358 (konny 189, pieszy 72, pocisk 97) = 41% strat. Ucieczka jest miejscem, gdzie ginie duzo, ale nie "najwiecej".
- Zbiorka (359): 13:35 rzutow 77, udane 33, wrocilo do szyku 24, padlo po powrocie 12. Razem 63 zawroconych w 32 bitwach - zjawisko marginalne.
- Poscig AI (148): 40 wejsc w 20 z 32 bitew, ale w bitwach Jeffa 0 - goni tylko strona AI, ktora wygrywa, a wygrywa gracz ("formacje gracza pominiete 2").
- Odwrot rannych (143): 13:35 44 ludzi, 23 za linia, wrocil do walki 0, zginelo w drodze/na postoju 24.
- Obronca trzyma pozycje (291): 4744 s w 32 bitwach; "puszczone: bilans ognia" 18.

## 6. Szturm w misji (gracz broni; 2 misje tego samego oblezenia: Goldentooth, Glover 13752 vs 529, w misji 500 vs 500 + fale)

Zrodlo: sonda "Oblezenie misja (370)" co 30 s. L:Armoury-2026-11-09_19-04-47.log:11504-11692 (bieg 2) i L:Armoury-2026-11-09_17-37-47.log (bieg 1, przed 371b).
- Taktyka przez cala misje bez zmiany: atak BreachWalls (AssaultUnderRangedCover, pasy 7 / 6), obrona DefendCastle (ProperDefense). 26 i 51 odczytow - 0 zmian.
- Otwarcie obu biegow identyczne co do szyku: F0 133 i F2 133 UseSiegeMachines (wieza, taran), F3 131 WaitForLadders, F1/F4/F5 35+35+33 strzelcow SparseSkirmish. Obrona: DefendCastleKeyPosition + ShootFromCastleWalls.
- Bieg 2, os czasu: t=240 drabiny zwolnione (372, 60 s od startu szturmu); t=271 F3 sam na murach (AssaultWalls); t=331 taran pod brama, wieza jeszcze 49 m; F3 topnieje 131 -> 4 do t=481 (210 s); F2 wchodzi do walki dopiero t=421, F0 t=481. Czyli natarcie na raty: grupa drabin ginie, zanim wieza i taran wejda do gry.
- Uzyte 2 z 4 drabin (2 "wyl"), druga wieza "nieczynna" przez cala misje; machiny miotajace 0 (bieg 1: 3, obsada 1 -> 0).
- Strzelcy ataku 103 -> ok. 50 w 4 min, pod murem w otwartym polu; strzaly calej misji A 3831 / O 3331.
- 372: natywny ShouldRetreat zablokowany 48 razy; zalamanie z progu 371 w 905 s: straty ataku 676 (koniec 714) / obrony 250 (253), wymiana 2.70-2.82:1, odwrot 295 ludzi, wynik DefenderVictory, przerwa 7.6 dob.
- Bieg 1 (prog 34.8%, bez 371b): "prog strat osiagniety (598/598), ale szturm idzie dalej - wszystkie bramy rozbite"; wieza ZNISZCZONA 84 m od muru; po 1713 s nadal trwal - ten sam schemat fal F2/F3 (Charge / AssaultWalls) co 2-3 min; koniec = wyjscie do menu.
- Szturmy liczone na mapie (SZ, bez gracza), 3 w logach: Dragonstone 2x odparty (4215 vs 1405: 600 / 126, wymiana 4.76; 4523 vs 1340: 558 / 137, wymiana 4.07 - L:Armoury-2026-11-11_13-19-13.log:1708-1709, :5655), 1x zdobyte przez wyrwe (wymiana 0.12). Koniec oblezenia (KO), 14 przypadkow: 10 "odejscie z obozu (zmiana celu)", 2 zdobycie, 1 oblegajacy pokonani, 1 mysl wodza - AI glownie zwija oblezenia.

## 7. Co z tego wynika (tylko z liczb)

1. AI nie odpowiada na ostrzal manewrem. Konnica pod ogniem w 85% czasu stoi w oslonie skrzydla; flankowanie konnica 0/32; kontra istnieje tylko na konnego strzelca. Odpowiedz na pytanie Jeffa: nie, druga strona nie probuje szarzy jazdy na lucznikow.
2. Atakujacy idzie krokiem 5-8 min przez strefe ostrzalu i dochodzi rozbity (49-84% trafien to pociski).
3. Repertuar jest waski: 9 taktyk, 8 zachowan, otwarcie zdeterminowane ukladem sil. Rozne sa czasy, nie plany. Brak odwodu, brak flanki, brak pozorowanego odwrotu, brak zmiany planu po stracie strzelcow/jazdy.
4. Szturm to jeden skrypt bez zmiany taktyki, wchodzacy na raty; nasze 371/372 decyduja o tym KIEDY odstapic, nie JAK szturmowac.
5. Wynik bitew gracza jest przesadzony jakoscia (T5.6 vs T3) - trudnosci nie da sie ocenic na tych danych.

## 8. Czego w logach brakuje (pomiary do dodania)

- Os czasu bitwy polowej jak sonda 370 w szturmie: co 15-30 s, dla KAZDEJ druzyny AI: taktyka, dla kazdego szyku klasa/ludzi/zachowanie/rozkaz/odleglosc od wroga/trafienia pociskami w oknie. Dzis jest tylko "probka" jednej druzyny i zachowania przed kontaktem.
- Zachowania PO kontakcie (333/344 koncza sie na kontakcie): ile szyko-sekund w kazdym Behavior* do konca bitwy.
- Histogram taktyk: czas w kazdej taktyce na druzyne, powod zmiany (wagi GetTacticWeight top-3 w chwili zmiany).
- Reakcja na ostrzal jako zdarzenie: "szyk stracil X% od pociskow w 30 s -> co zrobil w nastepnych 30 s" (stal / szedl / biegl / szarza / odwrot), osobno piechota, jazda, strzelcy; plus licznik "strzelcy wroga bez oslony w zasiegu szarzy jazdy, a jazda stala".
- Zabici wedlug zrodla i fazy: pociski / wrecz / szarza konna / w ucieczce, osobno przed kontaktem i po. Dzis trzeba to skladac z 4 linii.
- Sklad stron na starcie wedlug klas (piechota / strzelcy / jazda / konni strzelcy) dla obu stron w jednej linii; w "AI bitwy (333)" etykieta typu jest mylaca (szyki HorseArcher / Skirmisher / HeavyInfantry opisane "piechota", a 157 pokazuje w nich jezdzcow).
- Bitwy AI vs AI rownych sil bez elity gracza (autotest: gracz widz albo 200 T3 vs 200 T3) - jedyny sposob, zeby zmierzyc trudnosc i to, czy przegrywajaca AI kombinuje.
- Autotest: konsola nie zapisuje linii taktyk; logi Armoury z bitew armii at-359..373 juz nie istnieja (rotacja 12 plikow) - archiwizowac jak w m113/logi.
- Szturm: pomiar na pas (SiegeLane): ilu atakujacych na drabine/wieze/brame w czasie, czas od wejscia pierwszej grupy do wejscia drugiej, straty strzelcow ataku od murow, uzycie machin miotajacych.

## 9. Czego nie udalo sie ustalic

- Wynik i przebieg trzeciej bitwy Jeffa 11.11 13:55 (332 piechoty + 156 strzelcow + 2x83, potem fala 316): w logu sa zalamania kompanii do 14:03, brak bloku konca misji (L:Armoury-2026-11-11_13-19-13.log:11617-11902 i dalej).
- Wynik szturmu 09.11 18:03 (bieg 1) - brak linii konca.
- Czy AI kiedykolwiek wygrywa z graczem przy rownych silach - brak takiej bitwy w materiale.
- Zachowanie AI jako OBRONCY murow przeciw graczowi-szturmujacemu - 0 takich misji w logach; szturm z wyrwa / samymi drabinami w misji - 0.
- Bitwy AI vs AI w misji (gracz jako sojusznik malej partii) - brak; plan "sojusz" z 323x ma tylko liczniki.
- Co robia taktyki druzyn innych niz "probka" (sojusznicy AI gracza, druga druzyna wroga) - nie logowane.
- Czy "konnica w BehaviorFlank 0" to brak zachowania, czy brak pomiaru po kontakcie - z logu nie do rozstrzygniecia, trzeba kodu RBM (RBMBehaviorCavalryCharge / ProtectFlank) i naszej doktryny.
- Dlaczego S2 po wejsciu wybralo taktyke obrony zamiast ataku (13:35) - log pokazuje fakt, nie wagi.
- Logi sprzed 01.11 (seria 113-124 "bitwa sredniowieczna") - nie zachowane; porownanie "przed/po" niemozliwe.
