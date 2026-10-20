# NOC 20/21.10.2026 - WSZYSTKIE ZADANIA (zebrane 20.10 ok. 15:20 PDT)

Kolejnosc: (1) gdy Jeff skonczy grac: scalenie galezi m96 -> wielki test (bitwa, nowa kampania ok. 180 dob, 160 dob z zapisu) -> WGRANIE 96;
(2) OBOZ (docs/ZADANIA-OBOZ-NOC.md: oboz 21-3, chodzenie po obozie najpierw - tor L, woz + kon juczny, siodla bojowe/transportowe, markietani w duzych obozach);
(3) tablica ogloszen i plotki (ponizej) - najpierw rozpoznanie, potem kod; (4) optymalizacja NA KONIEC (docs/PLAN-OPTYMALIZACJA-NOC.md). Raport rano.
Limity: tydzien 68% o 15:00 (reset 26.10) - kolejnosc wazniejsza niz zakres.

## 1. WGRANIE 96 (galezie od 848b2e80 = 95)
m96-valyria (drogi Valyrii, trakty Essos), m96-sakiewki (21 -> 15 dni zoldu), m96-koszyk (88.5 -> 95%), m96-len (len i plotno), m96-bandy (bandy i armie z jedzeniem
i zlotem), m96-zamek (menu zamku), m96-wzory (luki [VI] na starcie + audyt kuzni), m96-dlugi (dlugi miast i podatek nadzwyczajny), m96-kuznia (= 95a w grze),
m96-jakosc (Legendary/Masterwork - rzadkosc), m96-czaskuzni (jeden pasek czasu kuzni, reszta czeka). Plus drobne: licznik "bez powalajacego" w GrandTourney liczy tez
zwyklych zolnierzy (liczyc tylko rycerzy); pobudka po nocy w karczmie -> dzielnica karczm (opcjonalnie).

## 3. TABLICA OGLOSZEN (Jeff 20.10 ok. 15:20)
Doslownie: "Dopisz do nocnej pracy tablice ogloszen jak w grach RPG z misjami, aby misje byly przedstawione, i zobacz, czy sa misje, aby wybic bandytow od kapitana
strazy w danym miescie, bo tego brakuje, i jaka nagroda byla realnie, inne misje, jakie daje gra itp. Czy jest to mozliwe, aby w miescie byla tablica i jak wchodze,
mam liste lordow, kapitana strazy etc., ktorzy daja misje, i jak klikne, mam visit i rozmawiam z nim i opisuje mi misje?"
Rozpoznanie: zadania gry (IssueBase - kto daje: notable, lordowie, gang, kapitan? w 1.4.8), zadania BK/ROT, nasze; czy jest zadanie "wybij bande" od strazy/kapitana miasta
(i od soltysa wsi), realne nagrody (z kiesy zleceniodawcy - zamknieta ekonomia, PLAN 8.18 nagrody zadan w nowych cenach); mody z tablica (Nexus: quest board,
notice board, bounty board). Projekt: w menu miasta (Town affairs albo osobno) "Notice board": lista zleceniodawcow w miescie z zadaniami (lordowie, notable,
kapitan strazy - nowy dajacy, jesli brak), krotki opis i nagroda; klik -> spotkanie z ta osoba (rozmowa gry z opisem zadania). Kapitan strazy: zadanie "rozbij bande X
w okolicy" z nagroda z kasy miasta (wedlug bandy i jej lupu), dowod: banda rozbita.

## 3b. PLOTKI W KARCZMIE (Jeff 20.10 ok. 15:20)
Doslownie: "W gossip tez powinny byc turnieje, gdzie jest turniej itp., oraz jesli wystepuja, oczywiscie, i to samo, ze ktos wydaje zone albo szuka dla meza dla swojej
corki jakis lord czy ktos, i jak biore gossip, to musze kupic piwo lub ale cokolwiek, aby sluchac, nie slucham przeciez z pustym jezykiem, bo by mnie wyrzucili z karczmy."
Zmiana: Rumours.cs (plotki) - nowe rodzaje: turnieje (gdzie, kiedy, nagroda - z GrandTourney, tylko gdy trwaja/ogloszone), swaty (lord szuka meza dla corki / zony dla
syna - z kandydatow malzenstw BK/gry, tylko prawdziwe); sluchanie plotek kosztuje: kupno piwa/ale (cena z polki miasta, do karczmarza - kiesa ludu miasta), bez
napitku - nie sluchasz (opcja szara z podpowiedzia). W zajezdzie tak samo. Kruki (R3-05c) - dalekie wiesci jak dotad.

### 3c. Piwo, plotki i kac (Jeff 20.10 ok. 15:25)
Doslownie: "za kazde wypite piwo moge dostac tyle plotek, ile moja charyzma; jak mam slaba charyzme w sensie przekonywanie, to dostane 1 plotke, potem moge kupic kolejne
piwo i dostane 1; max moge dostac wszystkie plotki po jednym piwie, ale to zalezne od mojego przekonywania, taki system; i jak wypije za duzo piwa, mam minus do predkosci
i musze dospac, i nastepny dzien kac, to bylaby fajna mechanika."
Projekt (do dopracowania w nocy, liczby dobiera Claude): plotki na jedno piwo = 1 + Urok (Charm) / X (np. Urok 0-49 = 1, ... wysoki = wszystkie plotki karczmy na raz);
kazde piwo kupione po cenie z polki (do karczmarza - kiesa ludu); licznik wypitych w tej nocy: ponad prog (np. 3-4 kufle, mniej przy niskiej Wytrzymalosci/Atletyce -
opisac) -> pijany: kara predkosci na mapie do snu, potrzeba dospania (dlug snu NightRest rosnie), nastepny dzien kac (mniejsza kara predkosci/morale/wzroku do poludnia);
XP Uroku za wysluchane plotki (male). Te same zasady w zajezdzie. Tylko gracz.

