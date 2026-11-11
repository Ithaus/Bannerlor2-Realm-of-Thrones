# AUDYT AI W BITWIE I OBLEZENIU - raport koncowy (11.11.2026)

Tylko odczyt: kod Armoury (worktree n367c), dekompilacje gry / RBM / Vanilla AI Sieges, logi gry i autotestu.
Nic nie zmieniono, nic nie zbudowano, gry nie uruchamiano. Zadna z propozycji nie jest wdrozona ani sprawdzona w grze.

Skroty: `mb` = scratchpad/rally_dec/mb_full.cs, `rbmai` = scratchpad/bad3/dec/rbmai.cs, `vais` = scratchpad/bad3/dec/vais.cs,
`A/` = scratchpad/n367c/Armoury/src/, `LOG` = Modules/Armoury/Armoury-2026-11-09_21-17-29.log. Dowody z logow: scratchpad/bad3/mapa-dowody-z-logow.md.

---

## 1. WERDYKT

Na pytanie wprost: **nie, gdy twoi lucznicy rozstrzeliwuja wroga, jego jazda nie szarzuje na lucznikow.** W pieciu twoich bitwach jazda AI pod ostrzalem przez 85% czasu stala na skrzydle i zbierala strzaly (23:06: 31 jezdzcow stalo 94 s i dostalo 66 trafien, wiekszosc w konie). Rusza tylko na jazde albo na konnego strzelca, nigdy na pieszy szyk lucznikow.

AI jest madre w malej skali: pojedynczy zolnierz trzyma tarcze, rotuje w pierwszym szeregu, ranny schodzi, zlamany ucieka, samotnego strzelca jazda sciga i obchodzi. Piechota zapiera sie murem tarcz przed szarza od czola. Bitwy sie nie zawieszaja.

AI jest glupie w duzej skali, czyli tam, gdzie decyduje wodz:
- atakujacy idzie krokiem 5-8 minut przez ostrzal i dochodzi rozbity (polowa do 84% trafien to pociski);
- obronca schodzi z dobrej pozycji, gdy tylko wrog podejdzie na 75 m, nawet jesli to sami harcownicy;
- jazda szarzuje w tej samej chwili co piechota, zanim piechoty sie zwiaza, wiec zawsze trafia w front i mur tarcz;
- nikt nie trzyma odwodu, nikt nie wychodzi z zasiegu lukow, nikt nie odstepuje z przegranej bitwy;
- smierc wodza niczego nie zmienia w dowodzeniu, charakter lorda nie ma zadnego wplywu na bitwe;
- cios w plecy lamie oddzial tak samo jak cios od czola.

Bitwy sa podobne z trzech powodow. Po pierwsze: wybor planu jest w pelni wyliczalny - ta sama armia na tym samym polu zawsze wybierze to samo (6 na 6 identycznych otwarc w tescie). Po drugie: RBM przydziela plan wedlug kultury wodza i jego plany maja wagi 10x wieksze niz reszta, wiec nasze dotychczasowe reguly (najpierw ostrzal, najpierw lucznicy, obronca idzie do zwarcia) u czesci ludow w ogole nie maja na co dzialac. Po trzecie: bitwa ma tylko dwie fazy - podejscie i ogolna szarze - bez planu na "co potem".

W szturmie jest jeden skrypt: rowny podzial na trzy pasy, drabiny ruszaja minute po starcie i gina, zanim taran i wieza dojada (131 ludzi stopnialo do 4), strzelcy stoja w otwartym polu do konca, obrona nie ma odwodu ani etapow.

Zastrzezenie: twoje bitwy rozstrzyga jakosc (twoje wojsko tier 5.6 przeciw 2.5-3.4; 304 ludzi rozbilo 956 za 2 nieprzytomnych). Trudnosci samej AI na tych danych nie da sie ocenic - dlatego plan zaczyna sie od pomiarow.

---

## 2. TABELA: sytuacja -> wodz historyczny -> nasze AI -> ocena

| Sytuacja na polu | Co robil wodz historyczny | Co robi nasze AI dzis | Ocena |
|---|---|---|---|
| Lucznicy wroga rozstrzeliwuja moja linie | Bannockburn 1314: jazda Keitha rozbija angielskich lucznikow z boku. Falkirk 1298: odwrotnie - lucznicy bez oslony niszcza szyk, ktory stoi | Jazda stoi w oslonie skrzydla (85% czasu pod ogniem), piechota idzie krokiem. Kontra tylko na konnego strzelca | ZLE - glowna luka |
| Atakujacy musi przejsc pole pod strzalami | Maraton 490 p.n.e.: ostatnie 200 m biegiem. Arsuf 1191: marsz w zwartym szyku pod tarczami, potem szarza na sygnal | Krok 1.33 m/s z 300 m; bieg pod ostrzalem istnieje, ale nie wlacza sie w planach RBM (23:06: "wejsc w bieg 0") | ZLE - jest mechanizm, zly wyzwalacz |
| Obronca przegrywa wymiane ognia | Towton 1461: Lancasterzy pod strzalami z wiatrem musza zejsc ze wzgorza i atakowac | Regula istnieje (S2/291e), weszla w 16 z 32 bitew; u ludow z planem "split" RBM nie dziala (13:35: 116 z 207 padlo od strzal bez zwarcia) | SREDNIO |
| Obronca ma wzgorze | Hastings 1066: Harold trzyma grzbiet caly dzien; przegrywa, gdy skrzydlo schodzi w poscig | Zamarza w marszu, gdy wrog przekroczy 200 m; schodzi do szarzy na 75 m, takze na samych harcownikow | ZLE |
| Jazda ma rozstrzygnac bitwe | Kanny 216 p.n.e., Zama 202 p.n.e.: jazda najpierw bije jazde, wraca i uderza w tyl zwiazanej piechoty | Szarza razem z piechota na najblizszego pieszego; cel sie obraca i stawia mur tarcz; jazda wroga nie jest celem | ZLE |
| Jazda naprzeciw pik i sciany tarcz | Courtrai 1302, Bannockburn: szarza od czola w zwarta piechote = kleska; madry wodz czeka albo obchodzi | Nie czyta szyku ani broni celu | ZLE |
| Jazda ugrzezla w piechocie | Rycerstwo wycofywalo sie, formowalo i szarzowalo ponownie (Hastings, Bouvines 1214) | Cykl RBM dziala (przejazd, zbiorka), ale nie patrzy na straty; pod ostrzalem w ogole sie nie formuje | SREDNIO |
| Konni strzelcy nekaja | Carrhae 53 p.n.e.: poscig Publiusza odciagniety i wybity; Partowie maja ciezka jazde jako mlot | Nasz poscig (204) wysyla kilku jezdzcow za strzelcem: 23:06 - 0 zabitych strzelcow, 8 jezdzcow padlo | ZLE - powtarza blad Rzymian |
| Szarza jazdy na lucznikow lub piechote z boku | Czworobok / jez (szkockie schiltrony), lucznicy za paliki lub za piechote (Azincourt 1415) | Lucznicy nie reaguja na jazde; piechota zna tylko mur tarcz od czola, czworoboku brak | ZLE |
| Linia peka w jednym miejscu | Farsalos 48 p.n.e.: czwarta linia Cezara zatrzymuje jazde. Grunwald 1410: odwody rozstrzygaja | Brak odwodu po obu stronach (uklad sztywny: piechota, strzelcy, 2x jazda) | ZLE |
| Uderzenie w tyl lub bok oddzialu | Kanny, Hastings: otoczenie i cios w plecy lamia nawet dobre wojsko | Kierunek ciosu nie ma wplywu na zalamanie kompanii | ZLE |
| Wodz ginie | Hastings: smierc Harolda konczy opor. Gaugamela 331 p.n.e.: klin na Dariusza | Wstrzas morale jest (318c), ale dowodzenie dziala bez zaklocen; nikt nie poluje na wodza | SREDNIO |
| Bitwa przegrana | Wodz wyprowadzal co sie da pod oslona tylnej strazy | Odwrot zorganizowany wylaczony (RBM zeruje); strona walczy do rozpadu, przegrany traci 92-100% | ZLE |
| Pozorowany odwrot, zasadzka | Hastings (Normanowie), Mongolowie nad Kalka 1223 | Brak | BRAK (kosztowne) |
| Charakter wodza | Porywczy Robert d'Artois pod Courtrai kontra ostrozny Saladyn | Cechy lorda nieczytane w bitwie; zero losowosci w naszych regulach | ZLE - tanie do naprawy |
| Szturm: koncentracja sil | Jerozolima 1099: wieza przetoczona na slaby odcinek, uderzenie rownoczesne | Rowno 133/133/131 na trzy pasy; drabiny 60 s po starcie, taran i wieza 2-4 min pozniej | ZLE |
| Szturm: wykorzystanie wylomu | Konstantynopol 1453: gdy jeden odcinek pekl, tam poszla cala sila | Brak przerzutu; gra dosypuje ludzi w pas, na ktorym oddzial wlasnie wyginal | ZLE |
| Szturm: strzelcy oslaniajacy | Zaslony (pawezy, mantlety), ogien skupiony na odcinku szturmu | Stoja w otwartym polu do konca (103 -> ok. 50 w 4 min) | ZLE |
| Obrona murow: odwod, wypad, stolp | Chateau Gaillard 1204: obrona etapami. Antiochia 1098: wypad | Brak odwodu, wypad praktycznie niemozliwy, odwrot do stolpu martwy, morale zalogi zamrozone | ZLE (zero dowodow z gry) |
| Zolnierz ratuje zycie | - | Tarcza, rotacja szeregu, odwrot rannych, ucieczka zlamanych, zbiorka przy wodzu | DOBRZE |
| Samotny strzelec nekajacy armie | - | Jazda go sciga i obchodzi z dwoch stron (204/283/311a) | DOBRZE |

---

## 3. PLAN URZOZMAICEN (kolejnosc wdrazania)

Zasady wspolne: jedna zmiana na wgranie, kazda z wylacznikiem MCM (nazwy po angielsku), kazda z licznikiem w linii konca bitwy. Zadnych bonusow do statystyk - tylko decyzje. Po kazdym nowym polu w Settings.cs: `python tools/gen_mcm.py`.

### Cztery fakty techniczne, ktore obowiazuja kazdy pakiet

1. **Waga 12 nie wygra z RBM.** Zachowanie aktywne ma premie x1.2-2.0 (mb:25301-25311), a oslona skrzydla RBM ma 10 na sztywno (rbmai:3959-3974), czyli wazy 12-20. Nasze zachowanie musi zwracac wiecej niz 20 (bezpiecznie 100) i oddawac szyk przez zwrot 0.
2. **RBM zeruje wagi przy kazdej zmianie fazy** (rbmai:12792-12860). Rozwiazanie zalecane: jedno wlasne "zachowanie roli" Armoury z nadpisanym `ResetBehavior` (metoda wirtualna, mb:20406-20409), dodawane tam, gdzie RBM dodaje swoje (postfiks `OnUnitAddedToFormationForTheFirstTime`, rbmai:9961-9977). Alternatywa z dwoch soczewek (dopychanie wag co tick, wzor S4 w A/Doctrine.cs:770-790) dziala, ale daje 0.5-1 s cudzego zachowania po kazdym resecie.
3. **RBM steruje pojedynczym jezdzcem wbrew celowi rozkazu.** Jezdziec dalej niz 60 m jedzie na najblizszego pieszego z dowolnego szyku (rbmai:5091-5105, 10731-10743). Rozkaz "szarza na lucznikow" skonczy sie w piechocie, jesli lucznicy stoja za nia. Potrzebny punkt posredni (dojazd tak, by lucznicy byli najblizsi) albo postfiks na `RBMAI.Utilities.GetCorrectTarget`.
4. **Plany "split" RBM maja wage 5-10, plany gry okolo 1** (rbmai:559-571, 934-946, 1186-1198, 1746-1758) i sa przydzielane wedlug vanillowego id kultury (rbmai:10050-10090): battania (Polnoc), sturgia/nord, aserai, empire. U tych ludow nasze mnozniki S1/S2/S3 nie maja na co dzialac.

Dodatkowo: postfiksy zachowan szyku biegna w zwyklej grze w watku silnika, w autotescie w glownym (A/Pendulum333.cs:73-76) - skan terenu tylko w `OnMissionTick`, w postfiksie sam odczyt. Arbiter 333/344 nie obsluguje jazdy (A/Pendulum333.cs:331), ale kazdy nasz rozkaz Move dla jazdy trafia na limit klusa z MarchToContact - potrzebny wyjatek.

---

### PAKIET 0 - Pomiary (najpierw, bez zmiany zachowania)

Opis w rozdziale 4. Koszt S-M, ryzyko niskie (tylko odczyty, w glownym watku). Bez tego nie da sie stwierdzic, czy cokolwiek z ponizszych zadzialalo.

### PAKIET 1 - Reakcja piechoty na ostrzal (S, najtansze i z najlepszym dowodem)

| # | Regula | Punkt wpiecia | Koszt |
|---|---|---|---|
| 1.1 | Bieg pod ostrzalem ma dzialac takze w planach ataku RBM | Dopisac AttackSplitArchers / AttackSplitInfantry / AttackSplitSkirmishers / Embolon do `Offensive()` (A/Doctrine.Hold291.cs:164-167) po nazwie typu | S |
| 1.2 | Wyzwalacz biegu i S2 od skumulowanych strat od pociskow, nie od chwilowego odsetka trafionych | Sygnal wejscia OutshotRush i S2: przyrost `DeathByRangedCount` + licznik trafien 140 (A/MarchToContact.cs:755; A/Doctrine.S2.cs:45, 151, 168) | S |
| 1.3 | Obronca przegrywajacy wymiane ognia wychodzi takze z planow "split" RBM | Postfiks `GetTacticWeight` na obronnych splitach RBM (przyciecie do skali gry, nie mnozenie) + dopisanie ich do S2Mul. Dotyczy tylko battania i sturgia/nord | S |
| 1.4 | Ustalic, co hamuje bieg (srednio 1.9 m/s, LOG:88577), zanim ruszymy progi | Sam pomiar; kandydaci: oddech AI, mur tarcz, 140 | S |

- Dowod przyczyny 1.1 wprost w logu: 23:06 plan AttackSplitArchers przez 579 s (LOG:100757) i "szykow w biegu 0, wejsc w bieg 0" (LOG:100777).
- RBM: latki na typy RBM sa wykonalne (`GetTacticWeight` zadeklarowane w ich klasach - `AccessTools.DeclaredMethod`).
- Nasze systemy: cofanie linii poza zasieg koliduje z zapadka kierunku 333b i zegarem 291 - nie w tym pakiecie.
- MCM: `Outshot Rush In RBM Attack Plans`, `Outshot Trigger By Missile Losses`, `Defender Leaves RBM Split When Outshot`.
- Pomiar: "wejsc w bieg", szyko-sekundy biegu, srednia predkosc, czas podejscia, udzial pociskow w trafieniach, "pod ostrzalem bez zwarcia do konca".
- Ryzyko: 1.1 ozywia kod dotad martwy u czesci ludow - sprawdzic, ile szykow naraz wejdzie w bieg (zasada 4 z CLAUDE.md).

### PAKIET 2 - Obronca trzyma pozycje (S, potem M)

| # | Regula | Punkt wpiecia | Koszt |
|---|---|---|---|
| 2.1 | Obronca w trzymaniu nie schodzi do szarzy na 75 m; puszcza dopiero zwarcie (25 m od pieszego wroga albo 35% w walce) | Postfiks (Priority.Last, po com.rbmai) na prywatnych `TacticDefensiveEngagement.HasBattleBeenJoined` (mb:39836) i `TacticDefensiveLine.HasBattleBeenJoined` (mb:40035); warunek: druzyna w trzymaniu 291 | S |
| 2.2 | Sami strzelcy wroga w 75 m nie przelaczaja obrony w szarze - od tego jest regula ostrzalu | Ten sam postfiks | S |
| 2.3 | Obronca zajmuje wzgorze: najpierw znaczniki HighGround sceny (25 z 28 scen ROT je ma), potem zapytanie silnika o stok za szykiem | Postfiks `BehaviorHoldHighGround.CalculateCurrentOrder` (mb:21682), punkt gotowy przed pierwszym rozkazem; skan w glownym watku | M |

- Nie latac `RBMAI.Utilities.HasBattleBeenJoined` - wolaja ja tez plany ataku i splity.
- Obronny split RBM ma wlasne wywolanie (rbmai:865) - osobna latka albo swiadome pominiecie.
- MCM: `Defender Holds Until Melee Contact`, `Defender Seeks High Ground`.
- Pomiar: istniejacy licznik "Engage (RBM 75 m)" ma spasc do 0 (dzis niezerowy w 11 z 73 linii autotestu); wysokosc pozycji obroncy wzgledem wroga w chwili kontaktu.
- Ryzyko: obronca stojacy pod ogniem bez reakcji, jesli pakiet 1 nie zadziala - dlatego po pakiecie 1.

### PAKIET 3 - Wodz AI: rozne otwarcia i charakter (S)

| # | Regula | Punkt wpiecia | Koszt |
|---|---|---|---|
| 3.1 | Los bitwy: raz na druzyne mnoznik 0.8-1.25 na wagi planow | Istniejaca tablica mnoznikow doktryny (postfiksy `GetTacticWeight`, A/Doctrine.cs:188-195); losowanie w pierwszym takcie po rozstawieniu, nie w AfterStart (sklad = 0, A/Doctrine.cs:52-54) | S |
| 3.2 | Profil wodza z cech (Valor minus Calculating): OSTROZNY / ZWYKLY / PORYWCZY - najpierw sam wpis do logu | Bohater `Team.GeneralAgent`; brak bohatera = ZWYKLY | S |
| 3.3 | Profil przesuwa progi innych pakietow: porywczy wczesniej szarzuje i dluzej goni, ostrozny trzyma odwod i dluzej strzela | Parametr w pakietach 1, 4, 6 | S na pakiet |
| 3.4 | Plany RBM w zasiegu losu | Postfiksy na `GetTacticWeight` typow RBM (te same co 1.3) | S |
| 3.5 | Po smierci wodza 45 s bez zmiany planu, potem lista planow wedlug najlepszego zywego taktyka | Jeden prefiks na `TeamAIComponent.MakeDecision` (RBM go nie latuje), cel przez `DeclaredMethod` | M |

- Rozklad cech w ROT_lords.xml (642 postacie): ostroznych ok. 43%, porywczych ok. 8% - profil roznicuje.
- Realna liczba planow z istniejacych taktyk to 3-5 na strone, nie "6-8". Skosne skrzydlo, lowy, przyneta to role szykow (pakiet 4), nie mnozniki.
- "Zamek planu x3" odrzucony: nie obejmuje splitow, przegrywa z RBM-owym 100 (rbmai:12869-12881), kasuje S2 i 291.
- MCM: `Battle Plan Variety`, `Commander Personality In Battle`, `Command Confusion After Commander Death`.
- Pomiar: histogram planu otwarcia na druzyne; cel - ta sama armia w 6 biegach daje co najmniej 3 rozne otwarcia.
- Ryzyko: niskie; przy 3.5 sprawdzic, ze S2/313b dzialaja mimo zamrozenia.

### PAKIET 4 - Jazda (serce odpowiedzi na pytanie Jeffa; M-L, w pieciu krokach)

| Krok | Regula | Punkt wpiecia | Koszt |
|---|---|---|---|
| 4.0 | Szkielet: "zachowanie roli" jazdy, ktore samo nic nie robi | Fakt 2 wyzej; wyjatek limitu klusa dla Move | M |
| 4.1 | Wybor celu szarzy z ocena: strzelcy odslonieci > szyk zwiazany walka (>= 50% w zwarciu, x1.5) > skrzydlowy szyk piechoty; szyk, ktory sie zaprze (srodkowy, niezwiazany, moja jazda blizej niz moja piechota) x0.2 | Postfiks na wirtualnym `TickOccasionally` / `CalculateCurrentOrder` RBMBehaviorCavalryCharge (nie na `CheckForNewChargeTarget` - ryzyko wklejenia przez JIT) + geometria albo `GetCorrectTarget` (fakt 3). Martwe S3 wyciac w tym samym wpisie | M |
| 4.2 | Rajd na strzelcow: gdy strzelcy wroga zadaja straty (takze trafienia w KONIE - dzis niewidoczne dla silnika, 55 z 87 trafien), sa odslonieci i jazda ma droge - dojazd z boku i szarza; gdy nie ma drogi - jazda chowa sie za wlasna piechote do zwarcia | Rola RAID / SCHOWAJ w zachowaniu roli; sygnal z kolejki 140/157; zapadka 30 s | M |
| 4.3 | Kowadlo i mlot: jazda czeka, az cel ma >= 50% ludzi w walce, i dopiero wtedy uderza z boku lub tylu | Rola WAIT / STRIKE; licznik zwarcia wlasny, raz na 1-2 s | M |
| 4.4 | Najpierw jazda wroga, gdy mamy przewage konna; kontrszarza na jazde idaca na naszych strzelcow | `ChargeToTarget(jazda)` - RBM honoruje taki cel, o ile w 30 m nie ma pieszego wroga | M |
| 4.5 | Wyjscie z grzezawiska: jazda stojaca w piechocie i tracaca ludzi odrywa sie i formuje | Rola DISENGAGE; uwaga: jezdzcy juz zwiazani nie posluchaja (rbmai:5074-5088) | M |

- Mlot na scigajacych konnych strzelcow (Carrhae) i "klin na biernego wodza" - po 4.1, przez sterowanie pojedynczymi jezdzcami (wzor 204), we wspolnej puli z 204/148 z jawnym pierwszenstwem.
- RBM: piechota wroga nie postawi muru tarcz przeciw rajdowi na lucznikow (wymaga, by cel jazdy == ten szyk, rbmai:12986, 13020). Stanie obok piechoty nie "przyszpila" jej - wariant THREAT odrzucony.
- Nasze systemy: szyk w roli wylaczyc z 204/148/344/283 (jeden wlasciciel); szarza wlacza u wroga "kontakt" arbitra na 4 s - pozadane.
- MCM: `Cavalry Commander`, `Cavalry Target Choice`, `Cavalry Raids Archers`, `Cavalry Hides From Missiles`, `Hammer And Anvil`, `Cavalry Fights Cavalry First`, `Cavalry Disengage`.
- Pomiar: szyk ofiary pierwszego ciosu jezdzca, kat wejscia szarzy, liczba rajdow na strzelcow i ich wynik, trafienia jazdy "bez szarzy szyku" (dzis 87 w jednej bitwie - ma spasc), licznik "cel podmieniony N".
- Ryzyko: wysokie technicznie (trzy warstwy RBM do przebicia), srednie dla gry. Kandydat na osobny blad RBM do sprawdzenia w osi czasu: zwarta jazda przelacza sie co tick miedzy szarza, Move za cel i Move w miejscu (rbmai:136-161) - mozliwa przyczyna szarpanego dojazdu.

### PAKIET 5 - Obrona przed jazda (M)

| # | Regula | Punkt wpiecia | Koszt |
|---|---|---|---|
| 5.1 | Lucznicy widzacy jazde, ktora dojedzie za mniej niz ok. 8 s (z dowolnej strony), chowaja sie za piechote | Rozszerzenie S4 o prog czasowy (A/Doctrine.cs:692-716); flaga gry `IsUnderCavalryChargeFromFront` tylko pomocniczo - nie widzi szarzy z boku i tylu | S-M |
| 5.2 | Piechota bez oslony, szarzowana z boku lub tylu, staje w czworobok / kolo na okreslony czas | Decyzja w passie glownego watku (jak CloseRanks); blokada nadpisania przez RBM w prefiksie `Formation.SetArrangementOrder` (rozszerzyc PreArr 344, A/Pendulum333.cs:749-765) | M |
| 5.3 | Limit lacznego czasu w czworoboku (bieg ograniczony do 0.3) | Ten sam stan | - |

- MCM: `Archers Shelter From Cavalry`, `Infantry Square Against Flank Charge`.
- Pomiar: ile razy lucznicy zdazyli sie schowac, straty lucznikow od jazdy, czas w czworoboku, liczba prob nadpisania przez RBM.
- Ryzyko: migotanie szyku, jesli blokada nie obejmie wszystkich trzech miejsc RBM (rbmai:4687, 13016, 13050). Wdrazac razem z 4.2, inaczej gracz tylko zyskuje albo tylko traci.

### PAKIET 6 - Morale kierunkowe, zbiorka (M)

| # | Regula | Punkt wpiecia | Koszt |
|---|---|---|---|
| 6.1 | Ciosy w plecy i z boku lamia kompanie latwiej; zalamanie sasiada tez | Nowy `OnAgentHit` w BannerCompaniesMission z kolejka pod zamkiem (wzor 313d, A/FieldCraft.cs:896-904); kierunek liczony na czlowieku (napastnik za plecami), nie z kierunku szyku | M |
| 6.2 | Nowe zrodlo jako zdarzenie z wlasnym dolnym progiem | Dopisac do `evt` i dac prog M ok. 1.0 (A/BannerCompanies.cs:1104-1127); powiekszyc tablice `BreakBySrc` | - |
| 6.3 | Zbiorka przy stojacym sztandarze poza zasiegiem lukow, powrot grupa do szyku z tylu, nie pojedynczo na front | Kolejnosc wyboru szyku po postoju w RallyRouted359 | S |

- Nie liczyc dwa razy z morale gry (`OnAgentFleeing` juz zabiera morale sasiadom, mb:140737-140762).
- To nie jest bonus do statystyk: regula dziala w obie strony, takze na wojsko gracza.
- MCM: `Rear And Flank Hits Break Companies`, `Rally At Standing Banner`.
- Pomiar: zalamania wedlug zrodla TYL / BOK / SASIAD; zawroceni, ktorzy przezyli (dzis 12 z 24 padlo po powrocie).
- Ryzyko: falszywe "TYL" od jazdy przejezdzajacej przez szyk - stad prog na liczbie trafien w oknie.

### PAKIET 7 - Szturm (kazdy krok wchodzi bez autotestu)

| # | Regula | Punkt wpiecia | Koszt |
|---|---|---|---|
| 7.0 | Sonda na pas: stan pasa, pojemnosc, liczniki atakujacych / obroncow, straty wg zrodla, cel kazdej fali, `IsSplittableByAI` | Rozszerzenie sondy 370 (A/SiegeMissionLog370.cs) | S |
| 7.1 | Drabiny czekaja na pierwsza machine zamiast 60 s | Zero kodu: `SiegeLadderWaitSeconds` 60 -> ok. 240 w MCM (uwaga na Armoury.json). Wersja kodowa: zwolnienie, gdy machina jest 25-30 m od muru | 0 / S |
| 7.2 | Wysilek glowny: 60/20/20 w fazie podejscia, pas glowny losowany sposrod wylomu, wiezy i bramy; pamiec ostatniego pasa w oblezeniu | Jednorazowe `TransferUnits` po rozstawieniu (gra toleruje niedobor do 20% calosci, mb:37223-37244) | M |
| 7.3 | Pas-lejek (oddzial wyginal, brak przyczolka) przestaje dostawac ludzi | Postfiks prywatnego `DetermineCurrentLanes` (mb:37669): z zapadka, nigdy pusta lista, nie usuwac pasa z naszymi w srodku | M |
| 7.4 | Fale posilkow ida na pas z przyczolkiem | Reczne `Agent.Formation` (wzor A/SiegeArchers372b.cs:26) - `TransferUnits` odmawia, gdy szyk ma kogos w zamku (mb:69113-69122, 70755-70760) | M |
| 7.5 | Obrona: odwod w szyku specjalnym 8, wychodzi na prawdziwy przyczolek | Wzor 381; wyzwalacz `Conceited` albo `InsideAttackerCount >= N`, nie `IsOpen` (prawdziwe juz przy podniesionej drabinie) | M-L |
| 7.6 | Ogien obrony proporcjonalnie do nacisku na pas; strzelcy ataku cofaja sie, gdy przegrywaja wymiane | Prefiks `ArcherShiftAround`; postfiks `BehaviorSparseSkirmish.TickOccasionally` (mb:24154) | M |

- Przyczolek = co najmniej 15 atakujacych przy punkcie obrony i mniej niz 15 obroncow (mb:44992-45022). Liczniki maja pamiec 5 s i nie licza druzyny sojuszniczej.
- Vanilla AI Sieges odpina tylko wybrane latki RBM (vais:27-142); reszta RBM dziala w szturmie, w tym zerowanie PullBack/Reserve.
- Odrzucone: dobor szykow wedlug tarcz (szyki to rowne przypadkowe wycinki), wymuszenie odwrotu do zamku rozkazem (RBM gasi odwrot - nasz dowod: szyki staly 15 minut, A/SiegeHold372.cs:26), `TotalAttack` jako "ostatnia fala strzelcow" (miesza lucznikow z piechota, nieodwracalne).
- Zalamanie obrony: gra ma gotowy etap sali lorda wyzwalany 20 zbieglymi obroncami (mb:79453-79456) - mozna na nim zbudowac "odwrot do stolpu", ale najpierw sonda na sucho i sprawdzenie scen ROT.
- MCM: `Ladders Wait For Engines` (istniejacy klucz czasu), `Siege Main Effort`, `Siege Abandon Dead Lane`, `Siege Waves Reinforce Success`, `Wall Defence Reserve`, `Defender Fire Follows Assault`, `Siege Archers Fall Back`.
- Pomiar: czas miedzy wejsciem pierwszej a drugiej grupy na mur (dzis 150-210 s, cel ponizej 60 s dla drabin wzgledem wiezy), udzial pasa glownego w pierwszych 3 min, straty grupy drabin, czas reakcji odwodu.
- Ryzyko: wysokie z braku testu - po 09.11 nie ma w logach zadnej misji szturmu z nowym kodem, a obrony AI przeciw graczowi nie ma wcale.

### PAKIET 8 - Duze rzeczy na koniec (L)

| # | Regula | Warunek wstepny |
|---|---|---|
| 8.1 | Odwod piechoty w szyku 8 (nie 0-7: gra go rozbierze), wychodzi przy pekajacej linii albo szarzy na strzelcow | Sprawdzic, czy wrog widzi szyk 8 jako cel; kompanie nie widza szyku 8 (A/BannerCompanies.cs:310, 346, 1287); prog `LocalPowerRatio` 0.8, nie 0.6 |
| 8.2 | Odwrot wodza z przegranej bitwy pod oslona tylnej strazy | Regula na mapie (dzis strona, ktora zeszla, i tak jest pobita: cs:99459, 98912-98917); zamrozenie rzutow kompanii (inaczej odwrot wyglada jak strata polowy skladu i wywoluje lawine paniki); wlasny znacznik dla SurvivalMode |
| 8.3 | Pauza w walce / wymiana linii | Tylko obustronnie albo po 8.1; jednostronne oderwanie = rzez w plecy |
| 8.4 | Pozorowany odwrot, przyneta | Po calym pakiecie 4 |
| 8.5 | Omijanie stoku w natarciu, szerokosc szyku (gleboki / dopasowany) | Jeden prefiks `Formation.SetFormOrder`; zysk do zmierzenia, nie pewny |

Nie wdrazac: zamiana szarzy na Advance dla elity (wylaczylaby rotacje pierwszego szeregu RBM), bramka na `TacticCharge` na koniec bitwy (gra juz ja ma), ustawianie celu ognia przez `Formation.SetTargetFormation` (kasowane co 0.5 s, mb:69489).

---

## 4. POMIARY DO DODANIA NAJPIERW

Wszystko jako odczyty w glownym watku, bez zmiany zachowania. Jedno wgranie.

| Pomiar | Po co |
|---|---|
| Os czasu bitwy polowej co 15-30 s dla KAZDEJ druzyny AI: plan, a dla kazdego szyku klasa, liczebnosc, zachowanie, rozkaz, odleglosc od wroga, trafienia pociskami w oknie | Dzis jest tylko probka jednej druzyny i tylko do kontaktu |
| Powod kazdej zmiany planu: trzy najwyzsze wagi i wynik `ResetTacticalPositions`; licznik zmian na druzyne bez pierwszego wyboru | Obecny licznik sumuje druzyny i zawyza |
| Histogram planu otwarcia na druzyne + profil wodza + rzut losu | Miara "czy bitwy przestaly byc takie same" |
| Jazda: trzy najwyzsze wagi zachowan RAZEM z premia aktywnego, stan szarzy RBM, szyk ofiary pierwszego ciosu, kat wejscia | Bez tego kazda zmiana w pakiecie 4 jest slepa |
| Trafienia w konie jako sygnal ostrzalu szyku jazdy | Silnik ich nie liczy |
| Licznik "strzelcy wroga odslonieci w zasiegu szarzy, a jazda stala" (szyko-sekundy) | Glowna liczba dla pytania Jeffa - ma spasc |
| Reakcja na ostrzal jako zdarzenie: szyk stracil X% od pociskow w 30 s -> co zrobil w nastepnych 30 s (stal / szedl / biegl / szarza / odwrot) | Osobno piechota, jazda, strzelcy |
| Zabici wedlug zrodla i fazy: pociski / wrecz / szarza konna / w ucieczce, przed kontaktem i po | Dzis sklada sie to z 4 linii |
| Sklad obu stron wedlug klas w jednej linii; poprawic mylaca etykiete typu w "AI bitwy (333)" | - |
| Zachowania szykow PO kontakcie (szyko-sekundy w kazdym zachowaniu) | 333/344 koncza pomiar na kontakcie |
| Licznik wywolan postfiksow wedlug watku | Autotest sam nie wykryje bledu watku |
| Szturm: sonda na pas (pakiet 7.0) | Progi 7.2, 7.3, 7.5 sa bez niej zgadywaniem |
| Autotest: bitwa rownych sil bez elity gracza (200 T3 vs 200 T3 albo gracz jako widz), zapis linii planow w konsoli, archiwizacja logow Armoury po kazdym biegu | Jedyny sposob zmierzenia trudnosci; rotacja 12 plikow juz skasowala logi bitew armii |

Miary sukcesu calego planu: (a) ta sama armia na tym samym polu daje co najmniej 3 rozne otwarcia w 6 biegach; (b) jazda pod ostrzalem stoi mniej niz 30% czasu (dzis 85%); (c) zwyciezca traci 5-15%, przegrany 15-40% (cel moda; dzis 0.7% i 91.7%).

---

## 5. DECYZJE DLA JEFFA

1. **Stoisz z lucznikami i strzelasz, jazda wroga dostaje po koniach.** Ma ruszyc na twoich lucznikow, gdy ma do nich droge, a gdy nie ma - schowac sie za wlasna piechote. Rekomendacja: TAK, obie reakcje, razem z obrona lucznikow AI przed twoja jazda (inaczej zmiana dziala tylko w jedna strone).
2. **Wrog atakuje, a twoi lucznicy go przerzedzaja.** Dzis idzie krokiem. Bieg od pierwszych strat (dzis prog 2 zabitych) we wszystkich planach ataku? Rekomendacja: TAK - to najtansza zmiana z najlepszym dowodem. Skutek: mniej twoich darmowych salw, wrog dochodzi zdyszany.
3. **Wrog broni wzgorza.** Ma stac do prawdziwego zwarcia, zamiast zbiegac do ciebie z 75 m? Rekomendacja: TAK. Bitwy z obronca beda dluzsze i trudniejsze.
4. **Ta sama armia, to samo pole - inne otwarcie.** Rzut losu na plan plus charakter lorda (porywczy / ostrozny). Rekomendacja: TAK, najpierw sam los i wpis profilu do logu, potem profil steruje progami.
5. **Jazda wroga czeka, az piechoty sie zwiaza, i dopiero uderza z boku.** Bitwa zaczyna sie spokojniej, ale konczy grozniej. Rekomendacja: TAK, po zmianie wyboru celu.
6. **Twoj oddzial dostaje cios w plecy.** Ma sie lamac latwiej - tak samo oddzialy wroga. Rekomendacja: TAK; oskrzydlenie zacznie sie oplacac obu stronom.
7. **Szturm: drabiny.** 09.11 prosiles, zeby wojsko "nie stalo jak glupie pod murami", stad 60 s. Skutek w logu: grupa drabin 131 -> 4, zanim wieza i taran weszly do walki. Wybor: dluzsze czekanie pod murem (ok. 6 m, pod lukami) albo natarcie na raty. Rekomendacja: jeden test z kluczem 240 s (bez kodu); docelowo czekanie poza zasiegiem lukow i zwolnienie na odleglosc machiny.
8. **Szturm: 60% sil na jeden odcinek zamiast po rowno.** Rekomendacja: TAK, ale dopiero po sondzie na pas.
9. **Wodz AI widzi przegrana i wyprowadza wojsko z bitwy z toba.** Dzis na mapie i tak jest pobity. Co ma znaczyc udany odwrot: uchodzi z czescia wojska czy nadal przegrywa wszystko? Rekomendacja: uchodzi z tym, co zeszlo w szyku - ale to duza zmiana (misja + mapa), na koniec.
10. **Chorazy z dowodca kompanii zawraca uciekajacych.** To zmienia twoja decyzje z 08.11 (zawracaja tylko wodz i kapitanowie-bohaterowie). Rekomendacja: zostawic, zrobic tylko punkt zbiorki przy sztandarze.
11. **Jazda AI poluje na ciebie jako wodza, gdy stoisz z tylu i nie strzelasz.** Rekomendacja: TAK z wylacznikiem, raz na bitwe, po pakiecie jazdy. Uwaga: twoja druzyna nie ma automatycznej strazy.
12. **Zaloga twierdzy sie zalamuje.** Odwrot do sali lorda (gotowy etap gry) czy kapitulacja z jencami (nowa sciezka na mapie)? Rekomendacja: sala lorda, po sprawdzeniu scen ROT.
13. **Kolejnosc.** Rekomendacja: pomiary -> ostrzal piechoty (1) -> obronca (2) -> los i profil (3) -> jazda (4) z obrona przed jazda (5) -> morale (6) -> szturm (7) -> duze (8). Szturm mozna przesunac wyzej, jesli wolisz oblezenia, ale kazdy jego krok wchodzi bez autotestu.

---

## 6. CZEGO NIE SPRAWDZONO

**Brak dowodu z gry**
- Wszystkie wnioski o wagach, celach i sterowaniu jezdzcem sa z dekompilacji, nie z logu wag.
- Zachowanie jezdzca ponizej 60 m od celu i skutek `SetTargetFormationIndex` leza w silniku natywnym - nie do odczytania.
- Obrona murow przez AI przeciw graczowi: 0 misji w logach. Szturm przez wylom albo samymi drabinami: 0. Po 09.11 brak misji szturmu z nowym kodem; autotest nie gra oblezen.
- Czy AI kiedykolwiek wygrywa z graczem przy rownych silach - brak takiej bitwy. Bitwy AI kontra AI w misji - brak.
- Logi sprzed 01.11 (seria 113-124) nie istnieja; porownanie "przed / po" niemozliwe.
- Wynik bitwy 11.11 13:55 i szturmu 09.11 18:03 - brak linii konca.

**Niewyjasnione przyczyny**
- Dlaczego S3 daje "wagi Flank 0" w 28 z 30 bitew (ktory warunek odcina).
- Dlaczego bieg pod ostrzalem ma srednio 1.9 m/s.
- Dlaczego po "ostrzal koniec (zwarcie)" druzyny przechodza NA plan ostrzalu albo split lucznikow.
- Dlaczego S2 po wejsciu wybralo plan obrony (13:35) - log pokazuje fakt, nie wagi.
- Czy JIT wkleja `CheckForNewChargeTarget` - to ryzyko, nie fakt.
- Czy przejazd jazdy RBM (Move za cel) jedzie klusem przez nasz limit marszu.
- RBM przy starcie bitwy z wrogiem blizej niz 200 m wydaje rozkaz z pozycja niewazna (rbmai:3252-3264) - mozliwy osobny blad.

**Niesprawdzone warunki techniczne**
- W jakim watku tyka AI szyku w zwyklej grze (od tego zalezy, czy wolno pisac wagi z glownego watku; S4 juz to robi).
- Czy gra i RBM widza szyk 8 jako cel; kompanie i wlasciciel pozycji czlowieka w szyku 8.
- Czy `Team.GeneralAgent` jest ustawiany dla druzyn AI pod RBM; bohater-wodz karawan i garnizonow.
- Kolizje z 335 (stanowisko wodza); ciala HorsePursuit*.cs poza naglowkami; progi L1/L2/L3 i 344.
- Dokad prowadzi ucieczka obroncow w scenach ROT i ilu trafia do sali lorda; co liczy `InsideAttackerCount` na murze.
- Los ludzi "order retreat" pobitej strony w lancuchu A17 / H3 / N7.
- Czesc dekompilacji siege (mb:37646, 19590-19679, stany pasow 45024-45070) czytana tylko przez jedna soczewke.

**Liczby**
- Wiekszosc liczb z logow pochodzi z mapy dowodow i nie byla liczona ponownie przez weryfikatorow. Sprawdzone niezaleznie: LOG:88577, 100757, 100777; 30 linii "wagi Flank"; licznik Engage i zwarcia 291 w konsolach; os czasu szturmu 09.11.
- Rozklad cech lordow jest z XML (wszystkie postacie pliku), nie z zapisu Jeffa.
- Historyczne przyklady w tabeli sa z wiedzy ogolnej, bez zrodel - sluza jako wzorzec zachowania, nie jako dane.
- Soczewki roznia sie w jednym punkcie wykonania: czy role jazdy trzymac wlasnym zachowaniem z nadpisanym resetem, czy dopychaniem wag co tick. Zalecam pierwsze; rozstrzygnie pomiar w kroku 4.0.
