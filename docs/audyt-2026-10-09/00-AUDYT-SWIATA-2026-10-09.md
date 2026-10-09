# 00 - AUDYT SWIATA 09.10.2026: synteza (jeden model swiata, paczki tej nocy, kolejnosc, pytania)

Status: SAM DOKUMENT. Nic nie zmienione w kodzie, w grze, w zapisach ani w ustawieniach. Jedyny zapisany plik to ten.
**Wersja po krytyce (09.10):** 33 uwagi dwoch krytykow (realizm; szczelnosc i rozgrywka) sprawdzone w kodzie, logach i zrodlach - 31 przyjetych
w calosci, 2 w czesci (odpowiedzi w rozdz. 8). Zmienione: 1, 2.1-2.11, S7, S9, S16, nowe S21-S23, paczki T1, T3-T8, reguly nocy 4.0, E5, E7, E10,
E13, E16, E21, 5.2, 5.3, pytania 6.1-1, 6.1-3, 6.1-5, 6.1-6, 6.1-8, 6.2-7.
Podstawa: 10 raportow tego katalogu (01-10, przeczytane w calosci), `docs/PROJEKT-EKONOMIA-OBIEG-2026-10-08.md` (dalej PROJEKT: zasady Z1-Z9,
tabela 4.1, paczki 162-168, rozdz. 12), `docs/STAN-PRAC.md` (decyzje Jeffa 05.10, 06.10, 07.10, 08.10), `docs/PRZEKAZANIE-2026-10-08.md` rozdz. 16,
`CLAUDE.md`. Kod = wersja w grze: Armoury c01a54ba = commit 2e235ea (kopia `...\7016f733-...\scratchpad\audyt\repo`), CrashScribe 11fa0214.
Kilka rozbieznosci miedzy raportami sprawdzilem sam w dekompilacji i w logach (rozdz. 3, z dowodem).

Oznaczenia: [K] kod plik:linia, [P] pomiar z logu/CSV, [H] historia albo lore ze zrodlem, [S] szacunek (rzad wielkosci, +-30%).
Kalendarz: rok = 364 doby (`Calendar.cs`), 1 moneta gry = 1 pens angielski ok. 1300 (`docs/AUDYT-CEN.md:16`).

Zlecenie Jeffa z tej nocy (doslownie, przekazane): "realne dystanse, aby wojsko pokonywalo realne dystanse na mapie, sprawdz, plus zaudytuj obozy -
teraz jak armia idzie, to trzeba rozbic oboz - daj godziny od 24 do 6 rano. Plus rzeczy, ktorych nie mamy, a moglyby sie przydac i system pozwala,
aby to wgrac". Wczesniejsze zadania Jeffa na te noc (PRZEKAZANIE rozdz. 16): pelny audyt ekonomii jako jednej symulacji, okupy, produkcja wsi z ludnosci,
minimalne surowce do napraw i bilans surowcow, produkcja wedlug krain, AI bez fali bankructw, ekonomia wojny, Nieumarli wolniej, drogi i wioski.
Zgoda Jeffa na noc (PRZEKAZANIE rozdz. 16): "Tak, wgraj sam" - **tylko to, co przejdzie autotest bez bledow, z kopia poprzedniej wersji; nic, co wymaga
nowej kampanii albo zmienia kanon**.

---

## 1. Dla Jeffa

1. Twoje odleglosci: dzis wojsko idzie 5-6 razy za szybko. Armia 800 ludzi robi ok. 117 km na dobe i z Winterfell do Krolewskiej Przystani dochodzi w ok. 21 dni,
   a prawdziwa armia z taborem szla 15-25 km dziennie. W ksiazkach Stannis PLANOWAL 32 km dziennie, ale w sniegu szedl ok. 10-13 km (po 33 dniach
   byl jeszcze 3 dni przed Winterfell).
2. Same godziny obozu tego nie naprawia: dzis AI spi od 22 do 5 (7 godzin). Przy obozie 0-6 idzie godzine dluzej, ale godziny 22-24 sa ciemne i idzie
   wtedy o polowe wolniej - dzienny dystans zostaje praktycznie ten sam. Odleglosc ustawia predkosc, nie sen - dlatego tempo swiata to osobna decyzja
   dla Ciebie (pytanie 1), bo zmienia cala gre.
3. Obozy dzialaja: co noc staje ok. 730 oddzialow (lordowie, karawany, wodzowie armii). Ale ok. 100 na noc rusza sie mimo snu i jedzie srednio 6 km
   (najwiecej co najmniej 27 km) z namiotem, zanim nasz straznik go polozy - to jest Twoj "namiot, ktory jedzie". Tylko czesc z nich budzi rozkaz innego
   moda (albo nasza straz mostow); ponad polowa nie ma zadnego widocznego rozkazu - przyczyne dopiero szukamy. Straznik sprawdza co sekunde prawdziwa,
   a przy Twoim szybkim czasie (x8) to ok. 2.4 godziny gry.
4. Tej nocy robimy oboz 0-6 dla wszystkich, ktorzy obozuja, i **armia zawsze rozbija oboz** (dzis co noc ok. 15% wodzow armii maszerowalo dalej razem
   z cala armia; samotne kolumny lordow dalej moga w 15% isc noca - pytanie 3). Do tego pytanie o oboz u Ciebie o polnocy, straznik liczony w czasie gry
   co 6 minut gry (namioty przestaja jezdzic) i poprawny meldunek "ile dni drogi" (dzis pokazuje ok. 20% za krotko).
5. Czy nasza ekonomia sie broni? Ceny, place i zold sa w grze bardzo blisko Anglii ok. 1300 (zbroje i bron z cen po 1350, wiec raczej o 1/3 za drogie),
   a projekt zamknietego obiegu (kazda moneta ma platnika i odbiorce) jest dobry. Krytyka znalazla jednak dwie rzeczy, ktore trzeba nazwac wprost:
   wojsko liczymy w duzej skali (ok. 52 mln ludzi), a pieniadz i lud w malej (ok. 3 mln) - przez to rody sa wobec ludu kilka razy bogatsze niz w sredniowieczu,
   a sam plan "kiesy ludu" jeszcze sie nie domyka (przy obecnym podziale lud miast nie ma za co kupic chleba). To trzeba przeliczyc przed nowa kampania.
6. To, co dzis naprawde dziala w grze, sie nie broni: codziennie ok. 1.4 mln zlota powstaje z niczego i ok. 1 mln znika w nicosc, wojsko kosztuje panow
   mniej wiecej tyle, ile caly ich dochod, a Bank z Braavos pozycza tym, ktorzy nie maja z czego oddac - stad 121 bankructw w roku.
7. W miastach nie ma ludzi z pieniedzmi: kasa miasta zyje w 70% z wydatkow zolnierzy, mieszczanie "kupuja" jedzenie za zloto z niczego, a chlopom pan zabiera
   ok. 85% gotowki, wiec wies nic nie kupuje. Wies tez nie produkuje "tyle, ilu ma ludzi" - gra patrzy tylko na trzy progi wielkosci, a wsie same rosna o ok. 60% w rok.
8. Tej nocy (oprocz obozow): Inni nie zdobywaja warowni przed 3. rokiem (dzis brali 6-13 osad w roku przez nasz wlasny dodatek "Pochod"), kowal przy
   naprawie plyty, ostrzy i drzewc bierze tyle zelaza, ile naprawde (nity i plytki, nie 15% nowej zbroi; kolczuga zostaje jak jest, bo ciezko pocieta
   historycznie dostawala nawet 1/4 nowych kolek ze zlomu), dezerterzy z niezaplaconych armii nie znikaja ze swiata (to znaczy troche wiecej band w drugiej
   polowie roku), rodzina pomaga splacic rate Banku, mlyny wychodza z rzeki, a w logu pojawia sie pomiar km na dobe i zapasow warowni na zime.
9. Rzeczy, na ktore nie wpadlismy: nikt jeszcze nie widzial zimy w grze (przychodzi po ok. 1.2-2 latach, srednio ok. 1.6, i trwa 3-5 lat z polowa plonow) -
   tej nocy puszczamy probna zime w autotescie; zalogi zamkow rosna same z niczego (w roku o ok. 33 tys. ludzi); jency sprzedani w Westeros zostaja dzis
   niewolnikami (wbrew kanonowi); Freyowie nie biora myta; konie zima nic nie jedza.
10. Na pozniej, w kolejnosci projektu ekonomii: uszczelnienie pieniadza, ksiega ludzi, kasy miast z "kiesa ludu", korona z biezacych podatkow, budzet rodu
    (lord trzyma tyle wojska, na ile ma dochodu), dlugi bez fali bankructw, okupy wedlug majatku z "trzecia" dla krola, przegrani uciekaja zamiast ginac
    (Twoja decyzja z 07.10, jeszcze niezrobiona), potem dopiero wolniejszy swiat.
11. Wszystko, co tej nocy wgramy, ma wylacznik w ustawieniach, przechodzi autotest 40 dob (kazda paczka po kolei na poprzednich i jeden wspolny test
    na koniec) i nie wymaga nowej kampanii; to, co zmienia kanon albo czego autotest nie zobaczy (dymek wioski, komunikat o paleniu), czeka na Ciebie.
12. Pytania sa w rozdz. 6 - najwazniejsze: jak wolny ma byc swiat, czy zimowa noc ma byc dluzsza, Inni i Mur, okupy (najpierw zmierzymy, jak czesto
    lordowie trafiaja do niewoli), jency w Westeros, i jak przeliczac zlote smoki z ksiazek.

---

## 2. Jeden model swiata

Model jest lancuchem: ludzie -> praca i produkcja -> dochody gospodarstw -> podatki i oplaty -> wydatki na zycie -> dwor i wojsko pana -> wojna -> dlug.
Kazde ogniwo ma tabele "dzis [P/K]" i "docelowo [S]" z platnikiem i odbiorca. Liczby docelowe dla wojny w stanie ustalonym biore z rachunku krytyka
PROJEKT 5.1 (tys. zl na dobe), podzial w miastach z raportu 01 (4.4); kazda liczba docelowa to [S] +-30%.

### 2.0 Zasady

- **Z1-Z9 z PROJEKT obowiazuja bez zmian** (platnik i odbiorca, zawor ponad zapas, dochod biezacy, pan placi druzyne i dwor, dwor jako kanal do miast,
  ludzie w obiegu, dlug nie znika, nikt nie zarabia na wlasnym wydatku, szczelnosc mierzymy przed zdjeciem siatki).
- **Z1 dotyczy takze towaru i ludzi** (CLAUDE.md zasada 0): "jak znika, to zamykamy". Dziury towaru i ludzi z tej nocy sa w 2.11.
- **NOWA Z10 (z raportu 01, przyjeta): praca -> ludzie, towar -> kupcy.** Kasa miasta (`Town.Gold`) to kapital kupcow targu, nie "wszyscy mieszczanie".
  Kazda zaplata za prace albo usluge idzie do kiesy ludzi danej osady, kazda zaplata za towar do kasy kupcow. Ludzie placa podatki od dochodu
  (nie od majatku) i kupuja z polek za swoje. Uzasadnienie: dzis pan bierze 7% STANU kasy miasta dziennie, czyli podatek od majatku kupcow
  (`PopulationLaw.cs:300-316`), a historycznie miasto placilo panu 1-5% dochodu rocznie (fee farm, `EKONOMIA-FUNDAMENT` H6). Z10 zmienia opis paczek
  grupy C (111', 162, 163, 164c) zanim beda skladane - patrz 5.2.
  **Uwaga po krytyce:** docelowy zawor PROJEKT (7% dziennie NADWYZKI kupcow ponad zapas) tez nie jest fee farm - w stanie ustalonym oddaje panu i koronie
  caly zysk kupcow ponad zapas. To zawor gry (Z2), mechanizm zamykajacy obieg, a nie odwzorowanie historii; jego podzial (S9) i wysokosc trzeba uzasadnic
  rozgrywka i rachunkiem (2.10), nie powolywac sie na fee farm.
- **Jedna zasada na jedno zjawisko w czasie i przestrzeni:** jedna noc obozu (0-6), jedna skala mapy (**4.75 km na jednostke - stala `Wayfinder.KmPerUnit`**,
  `Wayfinder.cs:23`; liczba 4.77 w raportach to zaokraglenie innej kalibracji), jeden rok (364 doby).
- **Kazda liczba "na glowe" ma podstawe:** "ksiega" (PopulationLaw, 52.6 mln na starcie) albo "BK" (ludnosc klas BK, ok. 3.1 mln [S]). Patrz 2.2 "Dwie skale".

### 2.1 Czas i przestrzen

| Co | Dzis | Docelowo | Dowod |
|---|---|---|---|
| Rok | 364 doby | bez zmian | [K] `Calendar.cs`, `Armoury.json` "WeeksPerSeason": 13 |
| Skala mapy | dwie w dokumentach: 4.77 i 2.84 km/jedn.; w kodzie 4.75 | **4.75 km/jedn.** (stala w kodzie) do marszu i drog; 2.84 tylko do gestosci osad (Mur w ROT 1.5x za dlugi) | [K] `Wayfinder.cs:23`, `WorldPace.cs:18`; [P] raport 08 3.5, 07 1.1 |
| Ruch partii | `Speed / 1.2` jedn. na godzine gry (1 dt = 1.2 h) | bez zmian | [K] sprawdzone: `Campaign.cs:874` `MapTimeTracker.Tick(4320f * num)`, `MobileParty.cs:2800` `NextMoveDistance = Speed * dt` |
| Noc obozu | szesc roznych "nocy": AI 22-5, sen gracza 21-6, namioty 21-5, bandy 23-6, warsztaty 23-5, kara nocna gry 22-2; co noc 15% kolumn lordow (takze wodzow armii z cala armia) nie staje (`AiCampSkipPercent` 15, `Armoury.json:290`) | **oboz 0-6 dla wszystkich, ktorzy obozuja; armia obozuje zawsze** (decyzja Jeffa 09.10: "jak armia idzie, to trzeba rozbic oboz"), chyba ze wrog blisko albo poscig; sen "w ciemnosci" gracza dalej 21-6 (jakosc snu), kara nocna gry 22-2 bez zmian | [K] `NightRest.cs:107, 138, 250, 406, 444-448, 512, 852, 861`; raport 07 3.6 |
| Dystans dzienny AI przy obozie 0-6 | marsz 5-22 = 17 h pelnym tempem | marsz 6-24, z tego 22-24 w ciemnosci z kara 50% = 16 + 2 x 0.5 = **17 h, bez zmian** | [K] `TerrainEase.cs:102` (kara `NightSpeedPenalty` 0.5, `Armoury.json:89`), ROT `ROTCampaignTimeModel.cs` (SunSet 22, SunRise 2), gra `CampaignTime.IsDayTime` |
| Tempo swiata | suwak 75% dziala jako DODATEK -0.25 do sumy czynnikow, nie mnoznik; podloga gry 1.0 | mnoznik wyniku (raport 07 T-1..T-4), potem wariant tempa wedlug Jeffa | [K] `WorldPace.cs:56`; [P] "World pace -1.00" = 0.25 x 3.99 |
| Marsz armii 800 | ok. 117 km/dobe [S]; Winterfell-KP 21 dni | wariant L (20%) "plan Stannisa / marsz forsowny": 32 km/dobe, 77 dni; wariant H (12%) "historia z taborem": 19 km/dobe, 129 dni | [S] raport 07 tab. 4.2; [H] acoup (13-19 km z taborem; 20 mil dziennie to wyjatek); Stannis: PLAN 300 mil w 15 dni (szacunek Artosa Flinta), naprawde w sniegu po 33 dniach ok. 3 dni przed Winterfell = ok. 10-13 km/dobe (https://awoiaf.westeros.org/index.php/Battle_in_the_ice , https://awoiaf.westeros.org/index.php/A_Dance_with_Dragons-Chapter_42 ; raport 07:160) |
| Wiesniacy, karawany, statki | to samo tempo co armie | wlasne tempo (wies 40%, karawana 30%, morze 28%) - inaczej przy wolnym swiecie miasta zglodnieja (wies srednio ok. 150 km od miasta) | [S] raport 07 T-5 |

### 2.2 Ludzie

| Warstwa | Dzis | Rola w modelu | Uwagi |
|---|---|---|---|
| Ksiega regionow (`PopulationLaw`) | 52.6 mln na starcie -> **90.5 mln po roku (+72%)**; Wyspy Letnie x12 [P] | skala wojska 1:1 i poboru (decyzja Jeffa 05.10) | BLAD: tabela = hearth x k (wies) albo dobrobyt x k (miasto), k z kalibracji 1. doby (`PopulationLaw.cs:84-124`) - rosnie z hearth, nie z demografii; historycznie +0.2..+0.8% rocznie [H] `DEMOGRAFIA-SILA-ROBOCZA` |
| Hearth wsi (gra) | 378 -> 615 srednio w rok (+63%) [P] (CSV ludzie-regiony) | wielkosc wsi dla progow plonu | zrodla wzrostu: inwestycje BEE 65-135 hearth dziennie z niczego (STAN-PRAC:247; zamyka je 170, klucz 2) + przyrost gry +4/+1.2/+0.2 (zastapi go 109) |
| Ludnosc BK (klasy) | ok. 3.1 mln [S] (wies hearth x4-6, miasto 8-15 tys.) - nigdzie nie logowana | skala konsumpcji i pieniadza: 3.1 mln x 64 d monety na glowe (Anglia 1290) = ok. 200 mln = zloto swiata 189-227 mln [S] (raport 01 4.3) | dwie warstwy jawnie: ksiega = wojsko i towar, BK = probka ok. 1:17 dla pieniadza (fundament F9) |
| Wioski poboczne W2 | 2 446 wiosek, 10.52 mln dusz (25% ludzi wsi) na sztywno z pliku [K][P] | dzis tylko widok | docelowo czesc okregu (A3, 5.1) |
| Wojsko | 100.5 tys. w partiach rodow, 79.7 tys. w zalogach [P] | razem 180 tys. = **0.34% ksiegi wyjsciowej** (same partie 0.19%) - zgodne z historia roku wojny (0.2-0.7%) [H] raport 09 2.14; ale **5.8% ludnosci BK** (ok. 17x wiecej niz historyczny szczyt) | rozmiar ustawia budzet (166), nie sufit gry |

**Dwie skale - co z nich wynika (dopisane po krytyce).** Wojsko liczymy wobec ksiegi (52.6 mln), lud i pieniadz wobec ludnosci BK (ok. 3.1 mln, ok. 1:17).
Decyzja Jeffa 05.10 ("wojsko zostaje 1:1, gospodarke dociagamy do niego"; STAN-PRAC:329) to dopuszcza, ale skutki trzeba nazwac, bo inaczej "zgodne
z historia" w jednym wierszu przeczy innemu [S, liczby z tego dokumentu]:
- cel dochodu rodow 1.72 mln/d (PROJEKT 5.1) x 364 / 3.1 mln = **ok. 202 d na glowe BK rocznie** - tyle, ile wynosil caly PKB na glowe Anglii ok. 1300
  (201 d, fundament F6); panowie z Kosciolem i korona brali 30.6 d, sami swieccy 14.3 d [H] (09:235, Campbell 2005/2008). Wobec ludu BK rody sa **ok. 6-14x
  bogatsze** niz w sredniowieczu; na ksiedze (11.9 d na glowe) - ok. 2.6x biedniejsze niz wszyscy panowie;
- ze wsi pan bierze 65-70% z 374 tys./d = **ok. 80-87 d na glowe BK wsi** (ok. 1.1 mln) wobec historycznych ok. 15 d czynszu i praw panskich (01 2.2) - ok. 5x;
  dziesiecina 5% daje 6.2 d na glowe BK wsi, czyli dokladnie historie (6.1-7.7 d, 01:122) - za duzy jest udzial pana, nie za mala dziesiecina;
  70% to domyslna stawka BK (`BKTaxModel.cs:315`), a nie historia (obciazenia rodziny chlopskiej 30-50% jej gotowki RAZEM z Kosciolem, 01:125);
- wojsko 180 tys. = 0.34% ksiegi, ale 5.8% ludnosci BK;
- stosunek dochodu panow do dochodu ludu: historycznie ok. 0.15-0.2 (30.6 / 201); u nas cel ok. 2.4 (1.72 mln / ok. 0.7 mln ludu miast i wsi z 2.4).
Wniosek: duze wojsko jest swiadomym wyborem rozgrywki, ale placi za nie lud w malej skali. Do decyzji przy grupie C (E11): jedno N dla pieniadza osad
i ludu (fundament 3.5 rekomendowal N ok. 7-10 wobec ksiegi, a kiesa ludu = dochod na glowe ksiegi / N) albo jawne zdanie w PROJEKT "rody ok. 6x bogatsze
niz w sredniowieczu, wojsko ok. 17x ciezsze wobec ludu - wybor rozgrywki". T6 i pozniejsza MIARA SWIATA wypisuja dochod panow, ludu i wojska na glowe
na OBU podstawach oraz stosunek panowie / lud (cel do sledzenia, nie do strojenia tej nocy).

Przeplywy ludzi [P] (rok autotestu): zwerbowani 617 tys. (od notabli 258 tys., z karczmy 32 tys., "bez osady" 327 tys.); zabici w partiach lordow
185.5 tys. (1.85 x stanu) - przegrani traca 46.2% zabitych, zwyciezcy 13.6% (historia 15-40% i 1-5% [H] HISTORIA-RABUNKU 4.1); dezercja 70 tys.,
w tym 5 710 usunietych przez WarLedger bez sladu; jency sprzedani w osadzie trafiaja do ludnosci BK jako niewolnicy (wszedzie, takze w Westeros - rozdz. 3 S2);
Nieumarli ok. 7 500 trupow z niczego na rok [S]. **Najwieksze zrodlo ludzi z niczego (dopisane po krytyce):** zalogi 46 872 -> 79 675 (+32.8 tys.),
milicje 62 639 -> 74 977, zalogi karawan 12 935 -> 37 930, tabory wsi 2 684 -> 19 401 w roku [P] (linie "Ludzie:" w `Armoury-2026-10-08_08-18-38.log`,
doby 108836 -> 109199). Zalogi rosna bez ochotnika i bez ubytku ludnosci: gra `GarrisonRecruitmentCampaignBehavior.cs:97-107` (`AddToCounts(GetBasicTroopForTown(town), num)`),
BK +1 czlowiek dziennie przy polityce "Enlistment" (`VanillaModelTweakPatches.cs:1756-1757`), a AI samo wlacza Enlistment w wojnie, gdy przyrost < 2
(`BKSettlementBehavior.cs:459-462`) [K]. Dla porownania T4 zamyka 5.7 tys. rocznie.
Docelowo: przegrani uchodza do ksiegi (H3, decyzja Jeffa 07.10), dezerterzy do puli wyrzutkow (Z6), jeniec wedlug prawa krainy (dom / Mur / niewola),
Nieumarli tylko z poleglych (R2/R4, decyzja Jeffa 07.10), przyrost zalog i milicji z ksiegi regionu albo od ochotnikow notabli (z ubytkiem ludzi),
zalogi nowych karawan z ksiegi miasta; ksiega 108 jako jedna jednostka czlowieka.

### 2.3 Praca i produkcja

| Co | Dzis | Docelowo | Paczka |
|---|---|---|---|
| Plon wsi | model GRY pod NavalDLC: baza typu x (poziom hearth + 1) x 0.5 (progi 200/600; z sufitem BK x0.5 / x1.0 / ok. x1.34); model BK "od robotnikow", akry, zyznosc, blogoslawienstwa BKROT i model BEE NIE dzialaja [K][P] (raport 02 1.1, ilorazy 1.24-1.37) | plon okregu = ludzie w domu we wszystkich jego wioskach: `baza x (L_dom / L_start)^e`, e = 1.0 przy spustoszeniu, 0.5 przy innym ubytku; powracajacy pierwszy rok na 50% (kapital) [H] welna 7-8 lat, mlyny 1/3 po 13 latach (HISTORIA-RABUNKU 2.5-2.6) | A1 most, potem 113 + A2/A3 |
| Wzrost plonu z niczego | +6..+21% w rok z samego wzrostu hearth [P] | 0 (przyrost tylko z ksiegi 109); do tego czasu zamrozone progi (A1) | 170, A1, 109 |
| Spladrowanie | caly okreg 0 przez 8-17 dob, potem 100% [K] | tylko spalona czesc (decyzja Jeffa 07.10) | A3 |
| Krainy | brak zyznosci; 16 wsi z towarem niemozliwym w klimacie; papirus na 128 farmach zboza; las wsi daje drewno na pustyniach (1 267 ladunkow/d) [K][P] | tabela klimatu wsi, papirus tylko na goracym poludniu, las wedlug klimatu (suma swiata bez zmian), zyznosc krain wyrownana do sredniej | T8 dzis; C1/B1 pozniej |
| Surowce | zapas KAZDEGO rosnie caly rok (produkcja 4-21% ponad zuzycie) [P]; braki lokalne: ruda w 25-28 z 97 miast, skora wyprawiona +6/d (garbowanie) [P] | rozklad przez karawany wedlug zysku (jest); wiecej garbowania | P2-G |
| Naprawy | 0.2 x strata wartosci CALEJ receptury nowej sztuki: Plundered 9%, Damaged 12%, Battered 15% (`MendMaterial.cs:17-18, 76-106`, `MendMaterialMaxShare` 0.2 takze w `Armoury.json:27`); wrakow (stan <= 10%) kowale nie naprawiaja (paczka 158); naprawy jedza 19% rudy i 15% drewna swiata [P] | metal wedlug rodzaju sztuki i szkody przy Plundered / Damaged / Battered: plyta i helm 1% / 2% / 4%, ostrza 0 / 0.5% / 1.5%; **kolczuga bez zmian** (9/12/15%) [H] plyta i ostrze: praca 75-95% rachunku; kolczuga: srednio 4% (Tower 1399, 22 haubergeony na 500 kolczug - naprawa i powiekszanie), ale ciezko pocieta = wymiana ok. 1/4 kolek ze zlomu (HISTORIA-KOSZT-NAPRAWY par. 7) | T3 |
| Rzemioslo miasta | TownCrafts przerabia welne, len i skory bez zaplaty komukolwiek [K] `TownCrafts.cs:279-302`; place warsztatow ok. 22 tys./d = 1% wplywow miast [P] | robocizna placona ludziom z kasy kupcow (Z10) | KIESA LUDU G |

### 2.4 Dochody gospodarstw

Dzis [K][P]: wlasna kiese maja tylko zolnierze (sakiewki 17.9 mln po roku, doplyw zoldu 582 tys./d) i wsie (0.44 -> 0.16 mln, mediana 76 zl, 532 z 571 wsi
ponizej 1 000). Mieszczanie nie maja nic; zamozni sa tylko jako notable (26.35 mln), a ich dochod jest glownie z niczego (pasmo 4 500-10 500, 578 tys./d).

Docelowo (raport 01 4.3; [H] Clark: robotnik 1.5 d, rzemieslnik 3.1 d; Campbell 2008: gospodarstwo nierolnicze ok. 4 L rocznie):

| Klasa BK -> sredniowiecze | Dochod gotowkowy zl/os./dobe | Skad (platnik -> odbiorca) | Zapas kiesy |
|---|---|---|---|
| dzierzawcy i chlopi miejscy -> wyrobnicy, sluzba | 0.30 | zolnierze i zalogi (40% ich wydatkow w miescie to uslugi) -> lud; sluzba dworu (25% wydatkow dworu) -> lud; place budow i kopaln -> lud | 10 dni |
| rzemieslnicy -> majstrowie, czeladnicy | 0.60 | place warsztatow zbrojnych, towarowych, rzemiosla miasta (kasa kupcow / warsztat -> lud) | 30 dni |
| szlachta BK w miescie -> patrycjat | 3.0 | zysk handlu (wariant raportu 01: 1/3 zaworu kupcow). **Przy podziale PROJEKT (2/3 pan, 1/3 korona) ta klasa nie ma platnika** - czynsz 40 tys./d idzie do notabli-bohaterow, nie do kiesy klasy (S9, 2.10) | 60 dni |
| niewolni | 0 | jedzenie 0.25 zl/d placi wlasciciel | - |
| chlopi wsi | 0.10-0.13 gotowki (reszta w naturze) | 30% utargu taborow (K7) + 15% wydatkow markietanow | 15 dni |

Skala [S] (podstawa BK): lud miast ok. 0.56-0.62 mln/d **tylko przy podziale zaworu z raportu 01** (228 tys. z tego to 1/3 zaworu kupcow); przy podziale
PROJEKT ok. 0.39-0.42 mln/d (2.10). Wsie 0.11-0.18 mln/d. Dzisiejsza konsumpcja BK miast to 0.32 zl/os./d = koszt jedzenia 0.35-0.43 d [H].

### 2.5 Podatki i oplaty

| Do kogo | Dzis [K][P] | Docelowo | Historia [H] |
|---|---|---|---|
| Pan z miasta | 7% kasy miasta ponad 20 000 dziennie = 533 tys./d (podatek od majatku) | **zawor gry (Z2)** 7% nadwyzki ponad zapas kupcow (10 000 + 12 x dobrobyt); podzial otwarty (S9: PROJEKT 2/3 pan, 1/3 korona albo 01: 1/3 lud, 4/9 pan, 2/9 korona) + 2-4% dochodu ludu (czynsz, oplaty) | historycznie pan bral z miasta kilka procent jego dochodu (fee farm 1-5%, EKONOMIA-FUNDAMENT H6); zawor, ktory oddaje caly zysk kupcow ponad zapas, to wybor gry - nie fee farm |
| Pan ze wsi | 70% utargu taborow do licznika pana (ok. 172 tys.) + 20% kiesy wsi dziennie (36 tys.) + 15% utargu w nicosc (37 tys.) = pan bierze ok. 85% gotowki wsi | 70% utargu (65%, gdy wejdzie dziesiecina), 30% zostaje we wsi (K7); renta z kiesy wsi tylko ponad zapas 15 dni (Z2 zamiast 20% calego stanu) - **realnie 0-35 tys./d, nie 176**, bo wies kupuje w miescie (2.10) | 70% to stawka BK (`BKTaxModel.cs:315`), nie historia: obciazenia rodziny chlopskiej ok. 12% calego dochodu i 30-50% jej gotowki RAZEM z Kosciolem (raport 01 2.2); villein 40-50% plonu, wolny 20-30% (raport 09 2.6). Na glowe BK wsi pan bierze ok. 5x tyle co historycznie (2.2) - koszt duzego wojska |
| Gmina miasta | brak | 1-3% dochodu ludu -> kasa kupcow ("gmina"); myto przy bramie i na mostach jedna regula (5.1) | murage 1 d od wozu, 1/4-1/2 d od juku: https://gatehouse-gazetteer.info/murage/muressay.html |
| Korona | wasale 2%/3% dochodu (15 tys.), danina wojenna (106 tys.), clo (9 tys.) = 130 tys./d; podatek BK 0.1% kiesy glowy ponad 100 000 (25-45 tys. [S]) | + udzial w zaworach (PROJEKT 1/3 = ok. 288 tys.; 01: 2/9 = ok. 152 tys.; liczby z PROJEKT 5.1 do przeliczenia - 2.10), trzecie z wojny (1/9 lupu AI - 2.8), renta wedlug lenn z biezacych wplywow | zwykly dochod Edwarda III do 30 000 L = ok. 19 800 d/dobe; podatek wojenny ok. 2 d na glowe w roku poboru (raport 09 2.8, 2.6) |
| Swiatynia / sept | brak platnika i odbiorcy | dziesiecina 5% utargu wsi + 1-3% dochodu ludu -> kaplan-notabl; 50% jalmuzna, 50% budowy (tylko po pytaniu 6.2-3) | dziesiecina 10% plonu; Wiara Siedmiu wierzycielem korony (AFFC rozdz. 28, https://awoiaf.westeros.org/index.php/A_Feast_for_Crows-Chapter_28) |
| Podatki klas BK | wyzerowane jako "z niczego" (`PopulationLaw.cs:150-165`) - slusznie | wracaja jako udzial w prawdziwym dochodzie ludu: 4% / 8-10% / 10-13% | - |

### 2.6 Wydatki na zycie

| Strumien | Dzis [P] | Docelowo (platnik -> odbiorca) [S] |
|---|---|---|
| Jedzenie i towary mieszczan (konsumpcja BK) | 521 tys./d (miasta) + 92 tys. (zamki) placone ZLOTEM Z NICZEGO kasie osady (BK `EconomyPatches.cs:637`) | kiesa ludu -> kasa kupcow ok. 470 tys./d; gdy ludu nie stac, kupuje mniej (spada zadowolenie BK) |
| Koszyk | - | jedzenie i piwo 60-75%, odziez 10-12%, opal 6%, mieszkanie 6-8% (-> notable), swiatynia i leczenie 1-3% [H] Phelps Brown-Hopkins (wagi cytowane), Dyer |
| Zakupy wsi w miescie | 0 (wies nie ma pieniedzy) | kiesa wsi -> kupcy ok. 150 tys./d: sol, narzedzia, sukno, garnki, piwo |
| Zycie zolnierzy | 1 640 tys. w dobie 364 (sakiewki -1.16 mln tego dnia) tylko przy wyjezdzie z miasta (`MenPurse.cs:190`) | w miescie: 60% kupcy / 40% lud; w polu markietani 10% sakiewki dziennie (85% miasto, 15% wies) - paczka 163 |

### 2.7 Dwor i wojsko pana

Dzis [P]: zold partii 605 tys./d (zaplacone 582 tys.), zalogi 194 tys. (185 tys.), werbunek 56 tys./d, zakupy sprzetu AI ok. 145 tys., budowy 18 tys.,
dwor 0. Wojsko kosztuje ok. 100% dochodu rodow (zold 799 tys. wobec dochodu modelu z rentami 806 tys.) [P/S] - raport 04 mowi "2-4 razy dochod",
bo liczy tylko dochod modelu bez rent; wniosek ten sam. Historia [H]: stale wojsko pana w pokoju 10-25% dochodu; w kampanii 1.4-2.7 x dochodu, ale tylko
na 40-80 dni (B3, raport 09 2.9). Armie AI powstaja tylko wedlug sily i wplywu - zaden model nie patrzy na pieniadze [K] (raport 04 1.3).

Docelowo (PROJEKT 6.1, 166; wojna, stan ustalony): zold partii 475, zalogi 195, dwor 373 (glowa -> kasa siedziby; tam 75% kupcy, 25% lud), sprzet
i werbunek 545 (z pulapu), budowy 22. Pokoj: zold 25% D, dwor 35% D, sprzet 17% D, budowy 10% D. Nadwyzka wojska zwalniana 15% dziennie do wsi
i karczmy (Z6). Dodatki z raportow: armia tylko, gdy rod wodza ma rezerwe i nie zalega z zoldem (W5); dluznik nie buduje (A3).

**Sprzet placony dwa razy (dopisane po krytyce).** W wojnie "sprzet i werbunek" 545 tys./d jest wiekszy niz caly zold partii (475). Liczba 545 nie jest
policzona z potrzeby, tylko jest reszta podzialu zapasu wojny 0.8 zold / 0.2 sprzet (PROJEKT K11). Tymczasem zolnierze juz sami kupuja z wlasnych
sakiewek to, czego im brakuje, i naprawiaja rzeczy (`MenPurseEnabled`, `SoldierPayToPurse`, `Settings.cs:171, 638`), a lord osobno kupuje im sprzet
z 25% kiesy na wizyte w miescie (`AiGearBudgetPercent`, `Settings.cs:421`) [K]. Historia [H]: komplet nalezal do zolnierza i byl oplacany z zoldu
i "regard"; pan dawal liberie, a konie stracone na wyprawie krolewskiej zwracala korona ("restor") - raport 09 2.4, 2.11. Komplet kosztuje 35-65 dni
zoldu piechura, ok. 60 dni zbrojnego bez konia i ok. 180 z koniem (09:177-179); przy dzisiejszym obrocie (185.5 tys. zabitych w roku na ok. 100 tys.
w partiach = 1.85 na rok) roczny koszt kompletow i zaliczek to ok. 35-60% zoldu [S], a po H3 (przegrani uchodza) mniej. **Docelowo (do opisu 166):**
(1) sprzet zolnierza z jego sakiewki (juz dziala); (2) budzet pana tylko na liberie, konie druzyny i uzupelnienie po kleskach, pulap ok. 15-20% zoldu [S]
(ok. 100-135 tys./d zamiast 545); (3) zakup zalogi w miescie (171) to wydatek z tego pulapu, nie osobny kanal. Zwolnione ok. 400 tys./d to glowny
kandydat na pokrycie luki z 2.10 - ale wplywy kupcow tez spadna, wiec tylko rachunkiem (sim_krytyk) przed grupa C.

### 2.8 Wojna: zold, lup, okupy, jency

| Strumien | Dzis [K][P] | Docelowo | Paczka |
|---|---|---|---|
| Zwrot zoldu korony | 50% zoldu w wojnie ze stanu skarbca: 253 tys. z naleznych 336 tys.; korona 130 tys. wplywow | z biezacych wplywow korony (PROJEKT 7.1) | 165 |
| Lup AI sprzedany | kasa miasta -> lord 47 tys./d (prawdziwe) | **1/9 ceny sprzedanego lupu** do skarbca, gdy korona zwraca zold temu rodowi, dopoki ludzie lorda nie dostaja swojej czesci (N7, po 163); po N7: 1/3 czesci lorda. Prawo trzecich [H] Hay 1954: zolnierz oddaje kapitanowi 1/3, kapitan krolowi 1/3 swoich trzecich = krol ok. 1/9 zdobyczy ludzi + 1/3 wlasnego lupu kapitana (https://historicalbritainblog.com/indentures-and-the-kings-army/, raport 09 2.11). Gracz bez zmian: 1/3 z jego 33% ("captain's third", `Settings.cs:213`) = 1/9 calosci - jedna regula | 165 + W4 (S21) |
| Zloto z cial dla AI | z niczego 10-25 tys./d [S] (`MapEvent.cs:1850-1867`) | z sakiewek poleglych i pojmanych przegranych | 164a |
| Lup z oblezenia | z niczego 15 x spadek dobrobytu (<2 tys./d) | z kasy zdobytego miasta, najwyzej 25% nadwyzki | 164a |
| Okup AI-AI | ISTNIEJE: gra co dobe z szansa 10% proponuje barter okupu miedzy rodami, zloto prawdziwe; cena gry (glowa rodu tier 4 ok. 6 tys. = 3-4% rocznego dochodu); czestosci nie mierzymy | jedna regula dla AI i gracza: max(cena gry; W x D x 364). **W NIE z gory, tylko z pomiaru** (169 N8: pojmania bohaterow na rod i rok): tak, zeby oczekiwany roczny koszt okupow rodu byl najwyzej ok. 5-10% D [S]; wstepnie lord ok. 0.1, glowa ok. 0.1 (30-40 D), krol x2 - czyli SWIADOMIE ponizej historii (rycerz ok. 1 rok, krol 2-3 lata [H] 09 2.10), bo glowy rodow w grze trafiaja do niewoli wielokrotnie, a historycznie magnat raz w zyciu albo wcale. Gotowka tylko z G ponad R, reszta raty do rodu porywacza; **jeden wspolny pulap wszystkich rat (Bank + okupy + korona) ok. 10-15% D na dobe** w jednej ksiedze dlugow (168); okup ma byc splacalny w ok. 1 rok. Podloga RC (8 / 25 / 100 tys.) - albo usunieta (za biednych okup spada, jedna regula), albo zostaje (za biednych bez zmian) - pytanie 6.1-5. Do korony jak lup (1/9) | pomiar w 169, potem W2 z 168 (pytanie 6.1-5) |
| Okup gracza w niewoli | zabrany w nicosc (`PlayerCaptivityCampaignBehavior.cs:244`), RC x5 + 30 x renoma | ta sama kwota do porywacza (wodz partii, glowa rodu, pan osady, kryjowka bandy) | W3 (test reczny) |
| Kurier okupu | gdy placacy AI ma za malo, gra dosypuje mu zloto (`RansomOfferCampaignBehavior.cs:174-176`, sprawdzone) | placi, ile ma ponad 5 000, reszta raty | L4 (test reczny) |
| Jency szeregowi sprzedani | sprzedajacy dostaje zloto z niczego; czlowiek trafia do ludnosci BK osady jako niewolnik (domyslna polityka BK kazdej osady) | kasa miasta placi (164b); prawo jenca wedlug krainy: Westeros dom albo Mur, Zelazne Wyspy thrall; z niewola wedlug kanonu: Zatoka Niewolnicza, Volantis, Lys, Myr, Tyrosh, **Norvos** (swieta straz niewolnych zolnierzy) i **Qarth** (miasto zyje z niewolnikow) [H] https://awoiaf.westeros.org/index.php/Slavery , https://awoiaf.westeros.org/index.php/Qarth ; bez niewoli: Braavos, Pentos (zakaz od ok. 100 lat, lamany przez magistrow); niepewne: Qohor (straz z Nieskalanych, ale zrodla mowia o zakazie lamanym jak w Pentos) i Lorath (dawne schronienie zbieglych) | 164b + Z2 (pytanie 6.1-6) |
| Straty w bitwie | przegrani 46%, zwyciezcy 13.6% zabitych | przegrany ginie z p = 0.15 + 0.35C + 0.15T + 0.15O + 0.20Q, **zakres 15-65%** (kazda zmienna 0-1, wiec 15% to minimum; St Albans 1455 wychodzi 15% przy historycznych 5% - przy E10 dodac czlon ujemny, np. -0.10 przy szybkiej ucieczce konnych przegranych, i sprawdzic znowu na 13 bitwach z HISTORIA-RABUNKU 4.3); reszta: jency, a z rozbitych `OutlawRoutedShare` (0.5) do puli wyrzutkow, pozostali do ksiegi - jeden podzial; zwyciezca najwyzej 5% zabitych | H3 (W1) - decyzja Jeffa jest |
| Pokoj z biedy | brak: wojna caly rok w 27-31 z 35 krolestw | pusty skarbiec i zalegly zold podnosza chec rozejmu (skala zmeczenia BK), wojny fabularne bez zmian [H] rozejm w Esplechin 1340 | D3/W6 (pytanie 6.1-7) |

### 2.9 Dlug

Dzis [K][P]: Bank pozycza, gdy w kiesie GLOWY jest mniej niz 10 dni zoldu (`IronBank.cs:206-211`), czyli na objaw deficytu; limit z dochodu jednego dnia
(Stark: 36-203 tys. w kilka dob); rata tylko z kiesy glowy (`:236`); po bankructwie odsetki rosna dalej (`:223-224`, sprzeczne z decyzja Jeffa 07.10 (c)).
Wynik roku: kapital 5.0 -> 0.13 mln (doba 183) -> 0.70 mln; 121 bankructw w 104 rodach (73 otwarte na koniec roku); 109 ze 119 ponad 30 dni przed terminem;
w dniu bankructwa rodzina (bez glowy) miala mediane 45.5 tys.
Docelowo: 168 (pozyczka na zdolnosc splaty 15 dni D, rata najwyzej 10% D; wierzyciele: skarbiec 10%, bogaty rod 20%, Bank; zajecie zamiast bankructwa;
odsetki zamrozone od 1. dnia zajecia; bez umorzenia; dlug wymarlego rodu na nowego pana wsi - decyzja Jeffa 08.10) + dopiski C1-C7 z raportu 05 (5.2).
Most do czasu 166: rodzina placi rate (T5 tej nocy). Dwie rozne rzeczy: dobrowolna rata najwyzej 10% D to wybor gry; zajecie po zwloce moze historycznie
siegac polowy dochodu z lenn. Historia [H]: elegit 1285 (Westminster II: wierzyciel bierze ruchomosci i POLOWE ziem dluznika, az dlug bedzie splacony,
https://en.wikipedia.org/wiki/Elegit) - to historyczny sufit dla "zajecia zamiast bankructwa" (168);
Tywin bral zakladnika do splaty (https://awoiaf.westeros.org/index.php/Tytos_Lannister); Bank "nie daruje" (https://awoiaf.westeros.org/index.php/Iron_Bank_of_Braavos).

### 2.10 Petla pieniadza po zmianach (wojna, stan ustalony, tys. zl/d [S]) i bilans swiata - PO KRYTYCE: PETLA JESZCZE SIE NIE ZAMYKA

```
RODY --zold 475--> SAKIEWKI LUDZI --zycie w miescie i markietani 475--> KUPCY 60% / LUD 40% (wies 15% z markietanow)
 |  --zalogi 195, dwor 373, sprzet i werbunek 545 (do zmniejszenia - 2.7), budowy 22--> KASY OSAD (kupcy; czesc uslugowa -> LUD)
 |  <--- utarg taborow: pan 70% z 374 (262) <--- TABORY WSI <--- kupcy kupuja caly plon (374)
 |  <--- renta wsi ponad zapas 15 dni (realnie 0-35, nie 176) <--- KIESY WSI (30% utargu + markietani = 183) ---> zakupy w miescie ok. 150
 |  <--- ZAWOR GRY (Z2) 7%/d nadwyzki kupcow: podzial otwarty (S9); kwota 684 + 181 z PROJEKT 5.1 do przeliczenia (zmieniaja sie wplywy kupcow)
 |  <--- skup lupu i jencow 182 (kasy miast; 1/9 do korony przy zwrocie zoldu)
 |  <--- KORONA: zwrot 50% zoldu (228) + renty wedlug lenn (281) + zapomoga (18) <--- udzial w zaworach + danina, clo, powinnosci, podatek BK, trzecie
LUD MIASTA --> jedzenie i towary -> KUPCY ok. 470 | czynsz -> NOTABLE ok. 40 | podatki: pan ok. 20, gmina ok. 15, (sept ok. 15)
```

Bilans wezlow z liczb tego dokumentu (raport 01 4.4, PROJEKT 5.1) [S] - kazdy wezel: wplyw = wyplyw + przyrost zapasu:

| Wezel | Wplywy | Wydatki | Saldo | Wniosek |
|---|---|---|---|---|
| Lud miast, podzial PROJEKT (2/3 pan, 1/3 korona) | zolnierze 162 + zalogi 78 + dwor 93 + place 60-90 = **393-423** | towar 470 + czynsz 40 + podatki ok. 50 = 560 | **-137..-167** | po czynszu i podatkach stac go na 64-71% towaru BK (bez nich 84-90%); kryterium S9 (co najmniej 95%) NIE spelnione; patrycjat (ok. 120) bez platnika |
| Lud miast, podzial 01 (1/3 lud, 4/9 pan, 2/9 korona) | jak wyzej + 228 = **621-651** | 560 | +61..+91 -> zapas ok. 12 mln w ok. 130-200 dob, potem zawor ludu do kupcow | zamyka sie |
| Kiesy wsi | 30% z 374 = 112 + markietani 71 = **183** | zakupy w miescie ok. 150 + renta | renta najwyzej **0-35** | PROJEKT 5.1 liczyl rente 176, bo wies nic nie kupowala |
| Rody | PROJEKT 5.1: 1.72 mln | 1.65 mln | +70 | renta wsi -140..-176 (kazdy podzial); przy podziale 01 dodatkowo zawor miast -132 i dziesiecina -19 (01:226) -> **ok. -0.1 (PROJEKT) do -0.26 mln/d (01)** wobec wydatkow |
| Kupcy | do przeliczenia | | | 40% wydatkow zolnierzy i zalog i 25% dworu idzie do ludu, lud i wies kupuja u kupcow - liczba 684 nie obowiazuje |

Wniosek po krytyce: **"z niczego 0, w nicosc 0" jest celem, nie wynikiem** - nie zostal wykazany. Przy podziale PROJEKT nie domyka sie lud miast,
przy podziale 01 - rody. Kierunek [S] do sprawdzenia: podzial 01 (historycznie zysk handlu zostawal u kupcow i patrycjatu) + pulap sprzetu pana
(2.7: ok. 545 -> 100-135, czyli ok. -400 tys./d wydatkow rodow, ale tez mniej wplywow kupcow) + renta wsi tylko z rzeczywistej nadwyzki; ewentualnie
produkcja wsi x1.3 / x1.6 / x1.9 z decyzji Jeffa 05.10. **Przed grupa C (E11) obowiazkowo:** rachunek `krytyk/sim_krytyk.py` z kiesa ludu i zakupami
wsi, jednym wariantem naraz; bilans kazdego wezla jak w tabeli; wynik dopisany do kryteriow S9. Grupa C bez tego rachunku moze dac deficyt rodow,
czyli mniejsze wojsko albo fale bankructw do czasu 166. (Wczesniejsze zdanie "kiesa ludu przesuwa ok. 15 mln zapasu = ok. -2.5..-3.5 tys. wojska"
dotyczy tylko jednorazowego zapasu i zostaje jako [S].)

Bilans swiata dzis [P] (PROJEKT 1.1, ostatni kwartal roku): zloto swiata 188.9 -> 227.0 mln; z niczego 1.45 mln/d ("zakupy" mieszczan 578, dosypka regulatora 77,
GiveGoldAction 793 tys.), w nicosc 1.0 mln/d (regulator 350, GiveGoldAction 552, utarg wsi 47, rozliczenia rodow 56 tys.), reszta niezmierzona -144 tys.
Cel (Z9): reszta < 10 tys./d przez 28 dob, zanim zdejmiemy dosypke regulatora.

### 2.11 Towar i ludzie tez musza byc szczelni (dziury znalezione tej nocy)

| Dziura | Dowod | Odbiorca / platnik docelowo | Gdzie |
|---|---|---|---|
| Wedrowcy BK rodza sztaby i wegiel (ok. 213 sztab/d, w tym 45 stali valyrianskiej) i dostarczaja KOPIE ladunku na polke | [K] BK `PopulationPartyComponent.cs:320-405`, `BKPartyBehavior.cs:845-858, 429-435`; [P] raport 03 1.6 | bez sztab i wegla z niczego; ladunek przelozony, nie skopiowany | P1-C razem z wierszem 16 PROJEKT i pomiarem 169 |
| BK kasuje 2% dziennie kazdego stosu towaru ponad 500 sztuk (drewno 179/d, len 60, ruda 10) | [K] BK `BKSettlementBehavior.cs:314-338`, wlaczone u Jeffa | ruda, metale, wyroby 0%; drewno, len ok. 0.2%/d; reszte lapie zawor K13 | P1-D po zaworze K13 |
| Zakup karawany przez lorda BK - cena w nicosc | [K] BK `BKLordPropertyBehavior.cs:78` (raport 05 L12); brak w tabeli 4.1 PROJEKT | kasa miasta, w ktorym kupiono | nowy wiersz 24 tabeli 4.1 -> 169 (pomiar), 164a |
| Dezerterzy WarLedger znikaja (5 710 w roku) | [K] `WarLedger.cs:133` (`AddToCounts(c, -take)` bez zdarzenia) | pula wyrzutkow regionu + ksiega ludzi | T4 tej nocy |
| Trupy Innych z niczego (+100 przy narodzinach bandy, +2/d) | [K] ROT `ROTOthersCampaignBehavior.cs:1299-1313` | tylko z poleglych za Murem i przy Murze | R2/R4 |
| Tabela ludnosci rosnie z hearth | [K] `PopulationLaw.cs:84-124`; [P] +72% | ksiega 108 / wzrost demograficzny | D z 108 |
| Hearth i chlopi BK z inwestycji BEE | [K] BEE `VillageInvestmentCampaignBehavior.cs:386-443` (rozpoznanie 170 A3) | zamkniete kluczem 2 paczki 170 | 170 (w toku) |
| **Przyrost zalog z niczego** (gra + BK "Enlistment"): +32.8 tys. ludzi w roku (46 872 -> 79 675) | [K] gra `GarrisonRecruitmentCampaignBehavior.cs:97-107`; BK `VanillaModelTweakPatches.cs:1756-1757`, `BKSettlementBehavior.cs:459-462`; [P] linie "Ludzie:" 08-18-38 | werbunek z ksiegi regionu albo od ochotnikow notabli, z ubytkiem ludzi | E2 (pomiar "zalogi: przyrost bez werbunku na dobe" w 169), E7 (108) |
| Milicja z niczego: 62 639 -> 74 977 | [P] jak wyzej | z ksiegi regionu | E7 (108) |
| Zalogi nowych karawan i taborow z szablonu: karawany 12 935 -> 37 930, tabory 2 684 -> 19 401 | [P] jak wyzej | z ksiegi miasta / wsi | E7 (108) |

---

## 3. Sprzecznosci miedzy raportami i rozstrzygniecia

| # | Sprzecznosc | Raporty | Rozstrzygniecie | Dowod |
|---|---|---|---|---|
| S1 | Czy sa okupy miedzy lordami AI? 05: "nie ma", 04: "barter gry 10% dziennie" | 04 1.6 / 05 L8, D1 | **SA.** Gra (vanilla) co dobe dla kazdego pojmanego bohatera rodu z co najmniej 2 lordami z szansa 10% zawiera barter okupu AI-AI ze zlotem prawdziwym, o ile barter oplaca sie obu stronom razem (suma wartosci > 0); BK pomija tylko jedna profesje, ROT tylko jencow Innych. Skutek: D1 raportu 05 (nowy mechanizm okupow AI) skreslony - zamiast tego pomiar w 169 i jedna regula ceny (W2); punkt (2) z PROJEKT 8.2 ("pozyczka na okup") ma czym sie wywolac. | [K] sprawdzone: gra `RansomOfferCampaignBehavior.cs:64-70, 102-108` (`ExecuteAiBarter`); BK `RansomOfferCampaignBehaviorPatches.cs` (Occupation 15); ROT `OthersPatches.cs:272-287` |
| S2 | Czy sprzedany jeniec znika? 04: znika z gry, 10: zostaje niewolnikiem | 04 L13 / 10 F9 | **Nie znika.** Prefiks BK na obu wersjach `SellPrisonersAction` dopisuje jencow do ludnosci BK osady: polityka "Enslavement" (domyslna w KAZDEJ osadzie) = niewolnicy; "Forgiveness" = dzierzawcy PIERWSZEJ osady ich kultury w `Settlement.All` (nie ich regionu - do poprawy przy Z2). Zloto dla sprzedajacego nadal z niczego (164a/164b). 164b nie moze dopisac ich drugi raz; REGULY-KRAIN pkt 4 ("jeniec znika") do sprostowania. | [K] sprawdzone: BK `SettlementPatches.cs:21-92`, `PolicyManager.cs:209` |
| S3 | WorldPace 50% czy 75%? (09: log pisze 50%, plik Jeffa 75) | 09 1.2, L3 / 07 1.2 | **Czynne jest 75%.** Linia "WorldPace: mapa 50%" powstaje w `WorldPace.ApplyAll` przy ladowaniu modulu, zanim MCM wczyta `Armoury.json`, wiec pokazuje domyslna z kodu. Audyt predkosci w tej samej sesji: "World pace -1.00" = 0.25 x 3.99, czyli suwak 75 (przy 50 byloby -2.00). Poprawka: wypisywac wartosc czynna w dziennym "Audyt predkosci" (T6). | [K] `WorldPace.cs:56, 160`; [P] `Armoury-2026-10-08_11-40-05.log:39` i `:209`; `Armoury.json:118` |
| S4 | Ktore linie zmienic dla nocy 0-6? 04: `h < 6` wszedzie, takze sen gracza i namioty; 07: klucze + `InCamp`, sen gracza liczony 21-6 | 04 T1 / 07 N-A | **Wersja 07** (klucze `CampStartHour`/`CampEndHour`, jedna funkcja `InCamp`), z dwiema decyzjami: (a) `:250` tez `InCamp` (namiot = oboz; i tak wolane tylko w oknie obozu), (b) pytanie o oboz o 0:00 (Jeff: "od 24"). Sen gracza w ciemnosci zostaje 21-6 z tickiem 6:00 liczonym jako noc - inaczej oboz 0-6 daje 5.6 h i dlug snu (pulapka 07 3.7, sprawdzona w `NightRest.cs:96-114, 154`). Bandy "dzienne" (`:512`) tez `InCamp`. | [K] `NightRest.cs:107, 118, 138, 154, 250, 406, 512, 852, 861` |
| S5 | Ile godzin marszu? 09 P-4: 6-8 h i oboz od popoludnia; Jeff i 07: oboz 0-6 (18 h marszu) | 09 / 07 | **Wiaze decyzja Jeffa (0-6).** Odleglosc na dobe ustawiamy predkoscia (tempo swiata jako mnoznik, 07 T-1..T-7). Tablica 09 (km/dobe = 3.975 x Speed x godziny) sluzy do kalibracji wariantu: przy 18 h armia 16.5 km wymaga Speed 0.23 (wariant H), 32 km - Speed ok. 0.45 (wariant L). Jedno pytanie do Jeffa: L czy H (6.1-1). | [S] raport 09 rozdz. 3; raport 07 4.2 |
| S6 | Czemu ludnosci przybywa 72%? 01: blad tabeli; 09: BEE; 02: hearth +63% | 01 1.4 / 09 L1 / 02 1.2 | **Jedna przyczyna, trzy objawy.** Hearth rosnie z niczego (inwestycje BEE 65-135/d + przyrost gry); od hearth zalezy plon (+6..21%), tabela ludnosci (+72%) i "renta nalezna". Kolejnosc: 170 zamyka BEE (klucz 2) -> pomiar -> A1 (zamrozone progi plonu) -> 108/109 (przyrost z ludzi) -> naprawa tabeli (01 D). | [K][P] jak w raportach; rozpoznanie 170 A3 |
| S7 | Wysokosc okupu: 04 W2 (glowa 0.5 roku D), 09 P-2 (glowa 0.5-1 roku, krol 1 rok + 0.5 skarbca), 05 D1 (najwyzej 90 dni D) | 04 / 09 / 05 | **Jedna regula W2, ale W z pomiaru** (poprawione po krytyce, liczby w 2.8). Pierwotne "glowa 0.5 roku D" lezalo PONIZEJ historii (rycerz ok. 1 rok, krol 2-3 lata [H]), a jednoczesnie dawalo raty na ok. 5 lat (182 D / 0.1 D dziennie = 1 820 dob), czyli wlasnie te raty na lata, ktorych AI nie udzwignie; kilka okupow naraz + rata Banku = 30% D dziennie i fala bankructw. Teraz: najpierw 169 N8 (pojmania bohaterow na rod i rok), potem W tak, by oczekiwany roczny koszt okupow rodu wynosil najwyzej ok. 5-10% D, a okup byl splacalny w ok. 1 rok (glowa rzedu 30-40 D) - **swiadomie ponizej historii**, bo w grze glowy rodow trafiaja do niewoli wielokrotnie. Gotowka tylko z G ponad R; jeden wspolny pulap wszystkich rat (Bank + okupy + korona) ok. 10-15% D na dobe (168). Pulap 90 dni D z raportu 05 wraca jako twarda gorna granica okupu (wstepne 30-40 D leza ponizej niego). | raport 09 2.10; PROJEKT 6.1, 8.2; raport 04 W2 (04:204-216) |
| S8 | Renta wsi: PROJEKT 5.3 "20% dziennie bez zmian"; 01 H: znies, utarg 65/5/30 | PROJEKT / 01 | **Renta tylko ponad zapas 15 dni** (to jest wlasnie Z2 - posiadacz oddaje to, co ma ponad zapas). Podzial 65/5/30 dopiero z dziesiecina (pytanie 6.2-3); do tego czasu 70% pan (BK), 30% wies (K7). Przy 166/165 policzyc rody samych zamkow (traca dochod ze wsi). | [K] `PopulationLaw.cs:312-315`; raport 01 1.3 |
| S9 | Podzial zaworu kupcow: PROJEKT 2/3 pan, 1/3 korona; 01: 1/3 lud, 4/9 pan, 2/9 korona | PROJEKT 5.3 / 01 I | **OTWARTE (zmienione po krytyce).** Pierwotne "domyslnie PROJEKT" oblewa wlasne kryterium juz na liczbach tego dokumentu: bez 1/3 zaworu lud miast ma ok. 0.39-0.42 mln/d przy wydatkach ok. 0.56 mln, czyli 64-71% konsumpcji BK (2.10), a patrycjat nie ma platnika; wczesniejsze "lud ok. 560-620" bylo wziete z wariantu 01 (podwojnie policzona moneta). Wariant 01 domyka lud, ale rody traca ok. 0.26 mln/d. Kierunek [S]: 01 + pulap sprzetu pana (2.7) + renta wsi z rzeczywistej nadwyzki. Rozstrzyga rachunek `sim_krytyk` z kiesa ludu i zakupami wsi PRZED grupa C. Kryteria: lud miast co najmniej 95% konsumpcji BK, kazdy wezel 2.10 bilansuje sie (wplyw = wyplyw + przyrost zapasu), wojsko w wojnie 85-100 tys., zwrot z monety gracza < 1.0, rody samych zamkow +-10% na 28 dob. | PROJEKT 7.2, 10.2; raport 01 4.4 (01:212-226) |
| S10 | Czym jest kasa miasta? PROJEKT K6: "jedna kiesa kupcow i mieszczan"; 01: tylko kupcy | PROJEKT / 01 | **Kupcy (Z10).** Bez tego K6 utrwali darmowa konsumpcje ("saldo 0 w jednej kiesie"). Kiesa ludu wchodzi razem z grupa C (jedna nowa kampania), nie osobno - bez K6 lud placilby za towar, za ktory BK i tak dopisuje zloto. | raport 01 4.4 (Z9) |
| S11 | Koszt wojska: 04 "2-4 x dochodu", PROJEKT "105%", 09 "ok. 100%" | 04 / PROJEKT / 09 | Rozne mianowniki: 04 liczy tylko dochod modelu gry, PROJEKT i 09 dochod z rentami. Wniosek ten sam: wojsko kosztuje caly dochod rodu przez caly rok. | raport 04 1.1 |
| S12 | Bankructwa: PROJEKT "73", 05 "121" | PROJEKT / 05 | 121 zdarzen w 104 rodach w roku; 73 rody w stanie bankructwa na koniec roku. | [P] raport 05 1.2 |
| S13 | Nieumarli tej nocy: 06 z latka na prywatna metode ROT (`OnAiHourlyTick`) | 06 N1 (b) | **Wersja minimalna:** kalendarz w Pochodzie + Zew spi + zawrocenie band z rozkazem na zamkniety cel. Latka na ROT dopiero, gdy test pokaze oblezenie przed terminem. Bez Zewu i Pochodu ROT przez 193 i 840 dni nie zdobyl niczego (horda < 500 nie oblega, `ROT:952`). | raport 06 1.1-1.2 |
| S14 | Tabela klimatu wsi (02 N2) zawiera 4 farmy za Murem - to kanon | 02 N2 / pytanie 02 | Tej nocy tylko papirus i las wsi (T8). Tabela klimatu (16 + 5 wsi) po odpowiedzi Jeffa o Murze i Skagos (6.2-1); zmienia tez warsztaty w trwajacej grze. | raport 02 C1, C4 |
| S15 | Naprawy: 09 L7 "robocizna 2.5-5 x za droga" vs decyzja Jeffa 07.10 "nasza robocizna jest lepsza" | 09 / STAN-PRAC:416 | **Robocizna bez zmian (Jeff).** Zmieniamy tylko metal (T3). | STAN-PRAC 07.10 |
| S16 | Myto: 10 Z3 (Bliznaki), 08 N-1 (mosty i brody), 09 P-5 (brama) | 10 / 08 / 09 | **Jedna regula myta:** placi kiesa przechodzacej partii (karawana: kasa karawany, lord: kiesa wodza, gracz) -> kasa osady wlasciciela przeprawy/bramy; stawki: murage 1 d od wozu, 1/4 d od juku [H] (Shrewsbury 1220, https://gatehouse-gazetteer.info/murage/muressay.html); pontage 1/2 d czlowiek z koniem, 1/4 d zwierze **[S] przez analogie do murage** (strona https://en.wikipedia.org/wiki/Pontage nie podaje stawek - do sprawdzenia w Calendar of Patent Rolls); Bliznaki x4 ("ciezkie myto" Freyow). Nie dublowac BK `CaravanFee` (myto bramne BK, ZRODLA-DOCHODU:167). Najpierw licznik ruchu (T6), potem pytanie 6.2-5. | raporty 08, 09, 10 |
| S17 | Dluzsza zimowa noc: 07 N-1 i 10 Z13 | 07 / 10 | Jeden pomysl, jedno pytanie (6.1-2): okno obozu wedlug pory roku w tej samej funkcji `InCamp`. | - |
| S18 | Numer "172" dla kiesy ludu w raporcie 01 | 01 | Numery nadaje sklad kolejno po 171; w tym dokumencie paczki maja nazwy (KIESA LUDU, MIARA SWIATA ...). | - |
| S19 | Ktory model liczy plon? Starsze `AUDYT-SUROWCE.md:45` i audyt lawy: BKROT, "1/4 bazy" | 02 1.1 | **Model gry pod NavalDLC** (BK i BEE wzajemnie sie wylaczaja). Dokumenty do poprawy (5.1). | [K][P] raport 02 1.1 |
| S20 | Inni nie obozuja - 06 3.7: "25% wiecej marszu" | 06 / 07 | Zostaje (wight nie spi - lore). Przy 0-6 zywi ida 18 h (17 h ekwiwalentu z kara nocna), Inni 24 h; uwzglednic przy progach Pochodu po dobie 728. | [K] `NightRest.cs:286, 449` |
| S21 | Udzial korony w lupie AI: 04 W4 "1/3 calego lupu", 09 P-3 "1/9" (dopisane po krytyce) | 04 / 09 | **1/9 ceny sprzedanego lupu**, dopoki ludzie lorda nie maja swojej czesci (N7); po N7 1/3 czesci lorda. Hay 1954: krol bral 1/3 trzecich kapitana = ok. 1/9 zdobyczy ludzi + 1/3 lupu samego kapitana; lord AI sprzedaje 100% lupu partii, wiec 1/3 tej kwoty to ok. 3x historia. Gracz: 1/3 z jego 33% = 1/9 - jedna regula. Za wielkiego jenca (krol, wodz) "2/3 do skarbca" to [S]; historycznie korona przejmowala takiego jenca w calosci za odszkodowaniem (indenture 1355, 04:144) - docelowo stale odszkodowanie dla pojmujacego. | 09 2.11; 04 W4 (04:226-231) |
| S22 | Skala: "wojsko 0.19% - zgodne" (ksiega) obok "lud zgodny z konsumpcja BK" (BK) i 09 tab. 3 "renta 5-10x za malo" (na zawyzonej ksiedze 90.5 mln) (dopisane po krytyce) | 09 / 01 / ta synteza | **Dwie podstawy, jawnie** (2.2 "Dwie skale"): na glowe BK rody biora ok. 6-14x tyle co historycznie, pan ze wsi ok. 5x, wojsko ok. 17x; na glowe ksiegi wojsko jest zgodne. 09 tab. 3 przeliczona na glowe BK: renta ok. 66.9 d, danina ok. 12.4 d, korona ok. 15.3 d, czyli 2-10x ZA DUZO, nie za malo. Kazda liczba "na glowe" z podstawa; decyzja o jednym N przy grupie C. | fundament F6, F9, 3.5 (3.5 wprost: mieszanie skal to powod, dla ktorego rody zyja z miast) |
| S23 | Metal naprawy kolczugi: T3 "1.5% / 4% / 12%" a zrodlo "ciezko = wymiana ok. 1/4 kolek" (dopisane po krytyce) | ta synteza / 03:163, HISTORIA-KOSZT-NAPRAWY par. 7 | **Kolczuga zostaje na starej regule** (9/12/15% receptury; przy Battered blizej historii niz T3). 4% z Tower 1399 to srednia hurtowej naprawy i powiekszania, nie punkt dla ciezkiej szkody; brakujace kolka powstawalyby z niczego. T3 obniza tylko plyte, ostrza, drzewca i reszte, gdzie praca to 75-95% rachunku. | [K] `MendMaterial.cs:76-89`; [H] HISTORIA-KOSZT-NAPRAWY:170-177, 202 |

---

## 4. Paczki tej nocy

### 4.0 Reguly nocy (dla kazdej paczki)

- Kolejnosc ogolna (PRZEKAZANIE 16): 169 -> autotest -> 170 + skrypt BEE -> autotest -> 171 -> autotest -> **paczki tej nocy po kolei** -> wgranie.
- Kazda paczka: osobny commit **narastajaco** (T1 na 2e235ea + 169-171, T3 na T1, itd. - nie rownolegle galezie), wylacznik w ustawieniach (opis po
  angielsku), `python tools/gen_mcm.py` po zmianie `Settings.cs` (i sprawdzenie zakresu suwakow w `McmSettings.cs` po wygenerowaniu), build z kodem
  wyjscia 0, DLL probny tylko na czas autotestu 40 dob, potem przywrocenie zatwierdzonego DLL (pamiec: "wgrywanie tylko po sprawdzonym buildzie").
  Autotest kazdej paczki idzie na DLL zbudowanym z niej i ze wszystkich poprzednich. **Przed wgraniem jeden wspolny autotest 40 dob na ostatecznym
  DLL** i kontrola md5: wgrywany DLL = DLL z ostatniego zielonego autotestu. Wgranie na stale z kopia `.bak` (zgoda Jeffa na te noc). Wpis CHANGELOG
  z kontrola wedlug zasady 0. Paczka, ktora nie przejdzie, wypada z lancucha (nastepne budowane bez niej, test powtorzony).
- Zadna z paczek nie dodaje kluczy do zapisu gry i nie wymaga nowej kampanii. Nowe klucze MCM nie istnieja w `Armoury.json` Jeffa - zadziala domyslne z kodu.
- Jedna zmiana naraz: kazda paczka ma wlasny autotest. Wyjatek dopuszczalny: T1 (Armoury) i T2 (CrashScribe) w jednym przebiegu, bo inny DLL i inne linie
  logu; przy jakimkolwiek bledzie - powtorzyc osobno.
- Odniesienie dla testow 40 dob: doby 1-40 rocznego `Armoury-2026-10-08_08-18-38.log` (108837-108876) i `Logs/2026-10-08_08-18-38/` (przebiegu 06:51
  nie ma na dysku - w `D:/Backup-Bannerlord/autotest-kopie/2026-10-08_06-51-39` sa tylko DLL i Configs).
- Do wgrania tylko to, co autotest umie sprawdzic. Rzeczy widoczne tylko oczami (dymek wioski, komunikaty gracza) - po obejrzeniu przez Jeffa.
- Kolejnosc wedlug wagi dla Jeffa: T1, T2, T3, T4, T6, T7, T5, T8; potem proby P1 i P2 (nie do wgrania).

### T1. OBOZY 0-6 + ARMIA ZAWSZE OBOZUJE + straznik w czasie gry + prawdziwy czas drogi (raport 07 N-A, N-B, N-C; S4; uwagi S3-S6, S12, S14, R9 w rozdz. 8)

- **Co:** jedna noc obozu 0:00-6:00 dla lordow, armii, karawan, band "dziennych" i pytania o oboz u gracza (o polnocy); **wodz armii nie korzysta
  z pomijania obozu** (armia staje zawsze, chyba ze wrog w promieniu albo poscig/ucieczka - jak dzis); straznik spiacych i zdejmowanie namiotow
  z kolumn, ktore ruszyly, w czasie GRY (koniec "jadacego namiotu"); meldunek "Course set" liczy prawdziwy czas jazdy.
- **Gdzie (po krytyce):**
  - `Armoury/src/Settings.cs` grupa "A night's rest" (przy `:693`): `public int CampStartHour = 0;` `public int CampEndHour = 6;` (opis: "hour the world
    makes camp / breaks camp; 0 and 6 = midnight to six; equal hours = no camp"); opis `AiCampsAtNight` (`:693`) "(22-4)" -> "(camp hours)"; opis
    `AiCampSkipPercent` (`:697`) dopisac "army leaders always camp"; opis `AiNightsAwakeInChase` (`:699`) - klucz martwy (czyta go tylko MCM):
    dopisac "(not in force yet: chasing and fleeing parties never sleep)" - wpiecie to O-4 (E21); opis `NightfallPromptEnabled` (`:703`)
    "at dusk" -> "when the camp hour strikes".
  - `tools/gen_mcm.py:38-42` - dzis zakres suwaka int to [0, max(10, 4 x domyslna)], czyli CampStartHour 0..10 (nie da sie ustawic 22) i CampEndHour 0..24:
    dodac slownik nadpisan `RANGES = {"CampStartHour": (0, 23), "CampEndHour": (0, 23)}` uzywany przed regula ogolna; po `gen_mcm.py` sprawdzic oba
    atrybuty `SettingPropertyInteger` w `McmSettings.cs`.
  - `Armoury/src/NightRest.cs`: nowa `internal static bool InCamp(int h)`: s = CampStartHour mod 24, e = CampEndHour mod 24, h mod 24; **s == e -> false**
    (bez tego 0/0 albo 6/6 uspiloby caly swiat na 24 h w trwajacej kampanii); dalej `s < e ? (h >= s && h < e) : (h >= s || h < e)`.
    `:406` `h >= 22 || h <= 4` -> `InCamp(h)`; `:852` i `:861` -> `InCamp(hh)` / `InCamp(hs)`; `:250` -> `InCamp(hh)`; `:512` -> `InCamp(h)`;
    `:118` (auto-namiot gracza) `night` -> `InCamp(h)`; `:138` `h == 21` -> `h == CampStartHour`; `:107` `h >= 21 || h <= 5` -> `h >= 21 || h <= CampEndHour`
    (tick 6:00 liczy godzine 5-6 jako noc); `:154` `h == 6` -> `h == CampEndHour`. Nie ruszac: `:1188`, `:1206` (sen w menu wedlug biezacej godziny 21-5),
    `SightRange.cs:35`, `TerrainEase.cs:102` (kara nocna gry 22-2), warsztaty 23-5.
  - Wodz armii (`:444-448`): warunek pomijania `AiCampSkipPercent` liczyc tylko, gdy `!(mp.Army != null && mp.Army.LeaderParty == mp)` (eskorta i tak idzie za wodzem, `:448`).
  - Straznik snu: `NightRest.cs:194` `_lastHoldSweep` `DateTime` -> `CampaignTime`; `:855-862` warunek -> `dh = (CampaignTime.Now - _lastHoldSweep).ToHours;
    if (dh >= 0.1 || dh < 0)` (ujemne = wczytany wczesniejszy zapis albo nowa kampania - uruchom od razu). HoldSleepers chodzi po `_camping` (ok. 730), tanio.
  - Namioty (bez zmiany znaczenia "stoi" - uwaga S3): `RefreshNearbyTents` (dobiera namioty, chodzi po `MobileParty.All`) **zostaje na zegarze realnym
    2 s** (`:847-853`, dzis), ale `IsStill` (`:301-309`) liczy prog od uplywu czasu GRY: `_stillPos` trzyma pozycje i CampaignTime pomiaru, "stoi" =
    dystans < 0.1 jedn./h x godziny od pomiaru (najmniej 0.01 jedn.); przy pierwszym pomiarze - nie stoi. Nowe `_tentPos` (pozycja przy postawieniu namiotu,
    zapis przy `Tent(mp, true)` w `:268-269` i `:287-288`) i nowa `DropMovedTents()` wolana z `OnTick` na zegarze gry 0.1 h (jak straznik; lista `_tented` <= `AiTentCap` 60):
    zdejmuje namiot kazdej partii z `_tented`, ktora odjechala > 0.3 jedn. od `_tentPos`, i liczy takie zdjecia oraz najwiekszy zjazd.
  - Reset sesji: nowa `NightRest.ResetWorld()` (czysci `_camping`, `_tented`, `_bedPos`, `_stillPos`, `_tentPos`, `_orders`, oba zegary) wolana tam, gdzie
    inne "nowa gra albo wczytanie" (konstruktor `ArmouryBehavior`, jak `SpoilsSeal.Reset` - `SpoilsSeal.cs:131-138`).
  - Log obudzen (uwaga S14): w `HoldSleepers` (`:200-238`, liczniki `:222`, probka `:224-228`) do licznikow godziny dopisac sume i maksimum zjazdu ze WSZYSTKICH obudzen oraz liczbe obudzen
    z rozkazem Hold (bez cudzego rozkazu) i z rozkazem z `_orders` (nasz zapamietany) - w linii godzinowej "AiNightCamp"; probka linii (pierwsze 3 i co 20.) zostaje.
    Stoper: czas `HoldSleepers` i `DropMovedTents` (srednia i maks. w ms na godzine gry).
  - `Armoury/src/Wayfinder.cs:63` `rideH = dist / speed` -> `dist * 1.2f / speed`; `:64` `24f / 18f` -> `24f / H`, gdzie H = 24 - godziny obozu -
    `NightSpeedPenalty` x (godziny ciemnosci 22-02 poza obozem) = 24 - 6 - 0.5 x 2 = **17**; gdy klikniecie jest w nocy (`Campaign.Current.IsNight`), meldunek
    dopisuje "(night pace)", bo predkosc z chwili klikniecia jest nocna.
- **Test (autotest 40 dob, `Logs/<sesja>/noc.log` i glowny log; odniesienie `Logs/2026-10-08_08-18-38/noc.log`):** linie "AiNightCamp" tylko dla 0:00-5:00,
  zadnej o 22:00 i 23:00; spiacych na godzine ok. 400-800 (dzis 730-770 w roku), w tym wodzow armii nie mniej niz dzis; zjazd obudzonych z licznika WSZYSTKICH
  obudzen: srednio < 0.5 jedn. (dzis w probce 1.29; zjazd ponizej progu 0.3 nie budzi), maks. < 1.0 (dzis w probce 5.69); "namioty zdjete po zjezdzie":
  najwiekszy zjazd < 0.6 jedn.; liczbe obudzen na noc i udzial "Hold bez rozkazu" zapisac jako nowe odniesienie (czestszy straznik moze lapac te same
  kolumny kilka razy - to nie blad); 0 x `Log.Error` z NightRest, HoldSleepers, DropMovedTents, RefreshNearbyTents,
  Tent; koszt HoldSleepers + DropMovedTents < 1 ms na wywolanie; km/dobe AI (T6) bez zmiany wiekszej niz +-5% (17 h ekwiwalentu jak dzis); linie gospodarki
  bez skoku. Test ustawien przed autotestem (krotka sesja albo jednostkowo): CampStartHour == CampEndHour -> AiNightCamp nie usypia nikogo. Recznie (Jeff):
  pytanie o oboz o 0:00, oboz 0-6 bez komunikatu o zarwanej nocy, wczytanie zapisu wczesniejszego niz biezacy - linie "obudzony" dalej sie pojawiaja.
- **Ryzyko:** male. Dzienny dystans AI bez zmian (marsz 6-24, z tego 22-24 o polowe wolniej = 17 h jak dzis 5-22); armia nie maszeruje juz noca w 15% nocy
  (armie wolniejsze o te noce - zgodnie z prosba Jeffa). Konflikt tylko tekstowy z 169-171 (Settings, McmSettings, CHANGELOG) - scalac po nich.
  Koszt: nowe petle chodza po listach <= 730 i <= 60 elementow; najdrozsza (`RefreshNearbyTents`) zostaje na dzisiejszym zegarze.

### T2. KALENDARZ INNYCH (CrashScribe; raport 06 N1 w wersji minimalnej, S13)

- **Co:** Pochod Nocnego Krola nie wybiera celu przed jego dniem; Zew Nocnego Krola spi do doby 728; banda z rozkazem oblezenia na zamkniety cel
  dostaje patrol przy swojej siedzibie. Kalendarz: Piesc Pierwszych Ludzi i Twierdza Crastera od doby 728 (3. rok; [H] w ksiazkach do 300 AC Inni wygrywaja
  bitwy, ale niczego nie trzymaja - https://awoiaf.westeros.org/index.php/Fight_at_the_Fist); Rogowa Stopa i Mrozny Brzeg 300 x 4.33 = 1 299,
  Thenn i Kly Mrozu 1 732, Hardhome 2 165 (kajdany samego ROT, juz przeliczone przez `Fabula`); Mur 2 184 (7. rok; serial s7 ok. 304 AC).
- **Gdzie:** `CrashScribe/src/Config.cs` po `:43`: `NightKingCalendarEnabled = true`, `NightKingSiegeFromDay = 728`, `NightKingRespectShackles = true`,
  `NightKingWallFromDay = 2184`, `NightKingCallFromDay = 728`; w `Load` po `:87` piec `case`. `CrashScribe/src/NightKingCall.cs`: `Day()` =
  `CampaignStartTime.ElapsedDaysUntilNow`; `OpenDay(Settlement)` po StringId (`ROT_castle60` Mur -> 2184; `ROT_castle45`, `castle_N6` -> 300 x k;
  `town_S6`, `town_S4` -> 400 x k; `town_S7` -> 500 x k; reszta -> 728; k = `Fabula` x4.33; wynik = max(wartosc, 728)); `PickTarget` po `:196`: cel z
  `Day() < OpenDay(s)` pominiety (w tabeli "closed until day N"); `Daily` po `:342`: przed 728 Zew nic nie robi (linia "spi do dnia 728" raz na 30 dob),
  a banda z `BesiegeSettlement` na zamkniety cel i bez zajecia dostaje `SetPartyAiAction.GetActionForPatrollingAroundSettlement(mp, ww.HomeSettlement, Default, false, false)`.
  Przy wgraniu: te same klucze w `Modules/CrashScribe/ModuleData/CrashScribe.settings.xml` (CrashScribe nie ma MCM; gra zamknieta, `.bak`).
  NIE tej nocy: latka na ROT `OnAiHourlyTick` (S13).
- **Test (nowa kampania 40 dob):** 0 linii "RUSZA NA" (dzis 7 do doby 40); "Zew ... spi do dnia 728"; `UMARLI ... 0 osad`; 0 linii "OBLEZENIE" Innych;
  trupy w polu w dobie 40 < 1 535 (dzis); "progi Innych x4.33 ... (podmienionych stalych 3)" bez zmian; 0 bledow `NightKingCall.*` w CrashScribe. Dodatkowo
  wczytanie zapisu z doby 360: Pochod i Zew spia, zdobyte osady zostaja.
- **Ryzyko:** male; Inni zachowuja sie jak ROT przed 16.09 ("stoja", bandy 150-300); po dobie 728 wraca dzisiejsze zachowanie (wtedy potrzebna naprawa Zewu R3).
  Zgodne z prosba Jeffa "Nieumarli wolniej, podboj po kilku latach"; szczegoly (Piesc w 1. roku? Mur?) - pytanie 6.1-4, liczby zmienialne w pliku.

### T3. NAPRAWY: METAL WEDLUG RODZAJU SZTUKI (raport 03 P0-A)

- **Co:** przy naprawie u kowali miasta (lawa, Pick a piece, polki wojska, uprzaz, ludzie gracza, kwatermistrz Spoils, lordowie AI) metal i opal kuzni
  wedlug rodzaju i szkody zamiast dzisiejszych 0.2 x strata (Plundered 9%, Damaged 12%, Battered 15% calej receptury). **Punkty liczone na prawdziwych
  stanach gry** (strata 0.45 / 0.6 / 0.75 = Plundered / Damaged / Battered; ponizej 0.45 liniowo od 0; powyzej 0.75 stale - wrakow kowale i tak nie
  naprawiaja, paczka 158): plyta, helm plytowy, rekawice, kropierz plytowy 1% / 2% / 4% (historycznie 0.1-1 kg na kilkunastokilogramowy pancerz = 0.7-7%);
  skorzana i tkanina (okucia) 2% / 3% / 6%; ostrza 0 / 0.5% / 1.5% (Tower 1375-7: czyszczenie i ostrzenie bez nowego metalu); drzewcowa i miotana 0 / 0.5% / 2%;
  tarcza (umbo, okucia) 4% / 8% / 12%; kusza 3% / 6% / 11%; **kolczuga (`ArmorMaterialTypes.Chainmail`) - stara regula bez zmian** (S23: ciezko pocieta
  = wymiana ok. 1/4 kolek, ze zlomu - gra juz bierze zlom z wrakow na polce); nieznany rodzaj - stara regula. Drewno sztuki, skora, plotno i ROBOCIZNA
  bez zmian (decyzja Jeffa 07.10). [H] HISTORIA-KOSZT-NAPRAWY par. 7: przy plycie i ostrzu praca to 75-95% rachunku.
- **Gdzie:** `Armoury/src/MendMaterial.cs`: nowa `MetalShare(ItemObject it, float loss)` (rodzaj jak `ArmsPricing.cs:176-213`: `ItemType` + `ArmorComponent.MaterialType`;
  dla Chainmail i nieznanych zwraca -1 = stara regula); `NeedsShare(it, share)` (`:98-106`) -> `NeedsShare(it, share, loss)`: przy ms >= 0 `crude = MetalKg x 1.25^stopien x ms`,
  wegiel kuzni `MetalKg x WorkshopForgeWoodPerMetalKg x ms`, drewno sztuki, skora i plotno x share; przy ms < 0 jak dzis. `Needs` (`:86-89`) podaje loss = 1 - PriceMultiplier
  (0..1), `NeedsFor` (`:92-96`) loss = missing. `Share` i `LaborF` bez zmian. `Settings.cs` obok `:748`: `public bool MendMetalByKind = true;` (opis po angielsku).
- **Test (40 dob, odniesienie: doby 1-40 rocznego 08-18-38):** "Zuzycie AI": metal na naprawiona sztuke nizej niz 0.18 kg (kolczugi bez zmian, wiec spadek
  mniejszy niz w pierwszej wersji - nowa liczba zapisana jako odniesienie); "czeka na metal" < 253/d; naprawione nie mniej niz 218/d; obitych w dobie 40
  nie wiecej niz 9 784. "Towary": ruda na naprawy < 4/d, drewno na naprawy < 22/d; "Warsztaty": brak rudy nie gorzej. Zadna liczba metalu nie rosnie.
  Skora: zapas dalej rosnie (ryzyko L8). 0 bledow; zapis i wczytanie `arm_mendstock`.
- **Ryzyko:** male; wiecej napraw = wiecej skory (garbowanie to waskie gardlo). Kolizja: 170 (BEE zjada rude i skore) moze przesunac liczby odniesienia
  - porownywac udzial napraw w zuzyciu rudy, nie sama liczbe. Po tescie poprawic 03:170 ("4% kolczugi na sztuke" -> "srednio 4%, ciezko ok. 25% ze zlomu").

### T4. DEZERTERZY Z NIEZAPLACONYCH ARMII NIE ZNIKAJA (raport 05 A2)

- **Co:** ludzie usuwani przez WarLedger (5 710 w roku) ida do puli wyrzutkow regionu i do ksiegi ludzi, jak kazdy dezerter (Z6).
- **Gdzie:** `Armoury/src/WarLedger.cs:113-139` (`DesertElitesFirst`) zbiera usunietych do `TroopRoster`; po wywolaniu (`:87`) bezposrednio
  `OutlawLaw.OnTroopsDeserted(mp, r)` (`OutlawLaw.cs:329`) i `PeopleLedger.OnTroopsDeserted(mp, r)` (`PeopleLedger.cs:163`) - bez strzelania zdarzeniem gry.
  Wylacznik `WarLedgerToOutlaws = true`.
- **Test:** suma "WarLedger: ... traci N" = przyrost dezercji lordow w PeopleLedger i puli wyrzutkow tego dnia; 0 wyjatkow. W 40 dobach skutku prawie nie
  bedzie (WarLedger dziala, gdy rody przestaja placic - glownie w 2. polowie roku), wiec test sprawdza tylko zgodnosc licznikow; pelny skutek w rocznym
  autotescie: "wyrzutki w puli" i "bandy" wobec 08-18-38 (pula 221 -> 10 747, bandy 6 272 -> 22 751 ludzi w roku).
- **Ryzyko:** male, ale **zmienia rozgrywke** (uwaga S11): +ok. 5.7 tys. ludzi rocznie do puli wyrzutkow, czyli ok. +50% jej rocznego przyrostu -
  wiecej band i strat karawan w drugiej polowie roku (odplyw puli 0.5% dziennie, `OutlawReturnBasePercent`; bandy werbuja do 2 ludzi dziennie,
  `Settings.cs:563, 571`). Zgodne z zasada Jeffa "jak znika, to zamykamy" i nie zmienia kanonu - dlatego zostaje tej nocy, z wylacznikiem.
  Sprzet dezerterow zostaje w zbrojowni (sprawa 108).

### T5. RODZINA POMAGA SPLACIC RATE BANKU (raport 05 A1; most do 166)

- **Co:** tylko rody AI: zanim Bank uzna rate za niezaplacona albo udzieli nowej pozyczki, brakujaca kwote oddaja glowie dorosli czlonkowie rodu z tego,
  co maja ponad prog max(5 000; 10 dni zoldu wlasnej partii). Przelew miedzy bohaterami (nic z niczego).
- **Gdzie:** `Armoury/src/IronBank.cs` przed pozyczka (`:209-211`, porownanie `c.Leader.Gold` z `target`) i przed oznaczeniem spoznienia (`:236`,
  `if (hero.Gold < pay)`); `GiveGoldAction.ApplyBetweenCharacters(czlonek, glowa, kwota)`; linia w "IronBank: dzien" "z kies rodziny: N rat, X zl;
  pozyczek mniej o M". Wylacznik `IronBankFamilyPays = true`.
- **Test (odniesienie poprawione po krytyce: doby 1-40 rocznego 08-18-38, linie "IronBank: dzien" 108837-108876):** tam 57 nowych pozyczek na 362 527 zl,
  0 spoznien, 0 bankructw. Kryterium: mniej nowych pozyczek i mniejsza ich suma, linia "z kies rodziny" > 0 rat, spoznien i bankructw 0; czlonkowie z partiami
  nigdy ponizej progu; okno IronBank w MoneyLedger bez zrodla i ujscia z przelewu; 0 bledow. Skutek dla bankructw widac dopiero w rocznym autotescie.
- **Ryzyko:** srednio-male: czlonkowie-wodzowie maja mniej na werbunek (stad prog z ich zoldu); przesuwa problem, nie leczy (deficyt zostaje do 166).
  Po wejsciu 166 (kiesa rodziny, K12) wylaczyc - jeden mechanizm. Kolizja z 169 (MoneyLedger) - sprawdzic po scaleniu.

### T6. MIARA SWIATA - CZESC 1: MARSZ I ZAPASY (sam log; raporty 09 P-1, 10 T1, 07; podzielone po uwagach S7 i S13)

Po krytyce T6 jest podzielona: tej nocy tylko to, co nie dotyka sciezek gry i nie koliduje z 169. Czesc 2 - (a) ludnosc BK i dochody na glowe na obu
podstawach ze stosunkiem panowie / lud (S22), (b) dryf tabeli ludnosci, (e) Bliznaki, (f) model produkcji - po scaleniu 169 (E2), z WLASNA inicjalizacja
refleksji BK w nowej klasie, bez ruszania `PopulationLaw.KeptTownLines` (`:170-185` jest w sciezce renty z miast ok. 533 tys./d, a 169 czyta
`PopulationLaw.RentToday`).

- **Co:** (c') "Marsz": dla kazdej partii lorda (`IsLordParty`) z ponad 300 ludzmi albo wodza armii sumujemy przesuniecie GODZINA po godzinie, tylko
  w godzinach ruchu (bez `CurrentSettlement`, `MapEvent`, `BesiegerCamp`, `AttachedTo`, morza; przesuniecie godzinowe > 0.01 jedn.). Raz na dobe:
  mediana i p90 km/dobe (suma x `Wayfinder.KmPerUnit` 4.75) dla partii z co najmniej 12 h ruchu tej doby, osobno wodzowie armii, osobno partie czysto konne;
  liczba partii w probce; czynne `WorldPacePercent` (S3). Pierwotne "przesuniecie netto miedzy dobami" odpada - stojacy w osadach, oblegajacy i patrolujacy
  w kolko zanizaliby mediane. (d) "Zapasy warowni": dni zapasu = `FoodStocks / -FoodChange` (gdy `FoodChange` >= 0 - "bez ubytku"): mediana, minimum,
  liczba < 90 i < 30 dni; osobno warownie kultury Polnocy, jesli id kultury ROT da sie ustalic przy buildzie (inaczej bez podzialu).
- **Gdzie:** (c') nowy plik `Armoury/src/WorldMeasure.cs`: `Hourly()` (slownik partia -> poprzednia pozycja, suma, godziny ruchu; czyszczony co dobe,
  martwe partie usuwane) wpiety w `ArmouryBehavior` obok innych `HourlyTickEvent` (`:597-603`), `Daily()` obok `TerrainEase.DailyAudit()` (`ArmouryBehavior.cs:1215`),
  jedna linia "Miara: marsz ..." raz na dobe. (d) `PeopleLedger.cs:257-263` (petla juz czyta `FoodChange` raz na warownie na dobe) - dopisac `FoodStocks`
  i dni; wypis na KONCU linii "Ludzie:" (`:312`), zeby nie psuc parsowania poczatku. Wylacznik `WorldMeasureLog = true`. Kazda partia i osada w `try`,
  licznik potkniec (CLAUDE.md: nie gasic funkcji).
- **Test:** linia "Miara: marsz" raz na dobe; probka co najmniej 30 partii; mediana i p90 w granicach 1-300 km/dobe (sprawdzenie sensu, nie cel - **kryterium
  dla wariantu tempa ustalamy dopiero po pierwszym pomiarze**); wodzowie armii wolniejsi niz partie czysto konne; WorldPace 75; "Zapasy warowni": mediana > 0,
  liczby < 90 i < 30 spojne z "warownie glodne"; dzienny tik dluzszy najwyzej o ok. 20 ms, godzinny o < 2 ms; 0 bledow; `tools/sprawdz_logi.py` (w repo
  Jeffa, nie w kopii 2e235ea) nadal czyta linie "Ludzie:".
- **Ryzyko:** minimalne (tylko odczyt, bez refleksji BK). Kolizja z 169 tylko tekstowa (ArmouryBehavior, CHANGELOG), jesli 169 nie rusza `PeopleLedger` -
  sprawdzic przy scalaniu. Wielkosc: mala-srednia (nowy plik + 2 wpiecia + 1 petla).

### T7. WIOSKI: MLYNY Z RZEKI (raport 08 W-1; po krytyce bez W-2 i W-3)

- **Co:** mlyn wodny stoi na brzegu, nie w korycie. **Pan w dymku (W-2) i komunikat o paleniu wioski gracza (W-3) wypadaja z tej nocy** (uwaga S18):
  autotest ich nie sprawdzi (gracz autotestu nie ma wsi, dymek wymaga najechania myszka), a zgoda Jeffa na noc obejmuje rzeczy sprawdzone - E22, po
  obejrzeniu przez Jeffa.
- **Gdzie:** `Armoury/src/MapVillagesView.cs:3287` (`k.AnchorOut = 0.6f * ...` -> `0.0f * ...`); `:2158` odjac `MillLandBack` 0.5 jedn. dla mlyna przy rzece
  (`!shore.Sea`); nowy licznik "budynek mlyna nad woda" w linii "woda (mlyn / rybacy)" (`:1823-1827`). Wylacznik `MillOnBank = true`.
- **Test (40 dob + tryb zdjec):** "kolo nad woda" i "dotyka wody" nie mniej niz dzis (81 / 74 w sesji 11-40-05), "budynek nad woda" ok. 0; zdjecia `mill-sweet-bridge`
  z08/z17 oraz Summer Mill 299.63/252.37, Berrybrook 293.30/252.47, Crossmill 312.93/246.89; 0 potkniec w linii "woda".
- **Ryzyko:** niskie (te same funkcje; gorszy wynik = kolo nad trawa - widac w liczniku). Zdjecia pokazac Jeffowi rano; jesli liczniki gorsze niz dzis - nie wgrywac.

### T8. KRAINY: PAPIRUS TYLKO NA GORACYM POLUDNIU, LAS WSI WEDLUG KLIMATU (raport 02 N3 bez punktu "za Murem", N4)

- **Co:** papirus (dopisany przez BK do kazdej farmy zboza) tylko w kulturach goracego poludnia; drewno "lasu wsi" wedlug klimatu (pustynia 0.3, step 0.5,
  srodziemnomorski 0.8, lesny 1.2, reszta 1.0) ze stala wyrownujaca do tej samej sumy swiata (ok. 1 267 ladunkow dziennie). Zboza za Murem NIE ruszamy (pytanie 6.2-1).
- **Gdzie (poprawione po uwadze S17):** `Armoury/src/MineralOnce.cs` `Postfix` (`:79-`): filtr papirusu z **wlasna bramka** `CropClimateFilter`, sprawdzana
  PRZED `if (s == null || !s.MineralsCountedOnce) return;` (`:85`) - wylaczenie latki mineralow nie gasi filtra i odwrotnie; gdy `v == null` (`:89`) lista bez zmian.
  Usunac Papyrus, gdy kultura wsi nie nalezy do {aserai, ghiscari, qartheen, volantine, lyseni, myrish, tyroshi, valyrian, summer} (id sprawdzic w kulturach
  ROT przed buildem; brak id = nie filtrowac); **waga zdjetego papirusu przechodzi na pierwszy wpis listy (glowny plon farmy)** - suma wag listy bez zmian,
  jak przy dublu mineralow (zasada klasy, czytaja ja model BK i `BKSettlementBehavior.HandleExcessFood`). `VillageWoodlot.PiecesPerDay` (`:97-102`) dostaje
  wies albo kulture, wspolczynnik z tabeli w kodzie, stala wyrownujaca liczona raz przy starcie i wypisana w logu. Wylaczniki `CropClimateFilter`, `WoodlotByClimate`.
- **Test:** "Towary: Papyrus ... wsie" ok. 18-20/d (dzis 75-80); "las wsi (126)" nadal ok. 1 250-1 300/d; linia startowa ze wspolczynnikami; z `MineralsCountedOnce`
  = false filtr dalej dziala (krotka proba); 0 bledow.
- **Ryzyko:** male (papirus drozeje - towar z daleka; drewno drozsze w Dorne i Qarth). Wlaczyc dopiero, gdy 169 zbierze pomiar bazowy (inaczej miesza dwie zmiany).
  Ostatnia w kolejce - jesli nocy zabraknie, przechodzi na pozniej bez szkody.

### Proby tej nocy (NIE do wgrania)

- **P1. Autotest zimowy** (raport 10 Z1/T3): probny DLL = T6 + domyslne `ClimateSummerDaysLeftMin/Max` = 0 i `ClimateFirstAutumnDaysMin/Max` = 7
  (`Settings.cs:592-595`; w `Armoury.json` Jeffa nie ma kluczy `Climate*`). Nowa kampania 40 dob: "Klimat" pokazuje zime ok. 8. doby; odczyt: warownie glodne,
  dni zapasu, HungerLaw, ceny zboza, IronBank, werbunek, dezercje. Prog alarmowy [S]: glodna warownia przed 30. doba zimy albo mediana zapasu < 364 dni
  (pierwsza zima trwa min. 3 lata). Po tescie przywrocic DLL Jeffa (md5). Wynik zasila PROJEKT-GLOD i kalibracje 166.
- **P2. Proba drog i dane offline** (raport 08 D-0, D-1, W-4): przeliczyc offline dane lore A/B i siec drog na plik wiosek z gry (`layout_crc32` z samych wierszy
  danych; kontrola 2 446/2 446 uid, konce sciezek w >= 95% wiosek, dzis 843); stemple decali drog na 2 komorkach (Fairmarket 392/532, Winterfell 396/845)
  wlasnym prefabem z samym `decal_component` (wzor `map_track_arrow`); zdjecia z wysokosci 8/17/40 i pomiar klatki. Wgranie dopiero po obejrzeniu przez Jeffa.

### Kolizje paczek tej nocy z 169-171

| Paczka | 169 (log obiegu) | 170 (BEE) | 171 (zbrojenie zalog) |
|---|---|---|---|
| T1 | tekstowa (Settings, McmSettings, CHANGELOG); T1 dodaje tez zmiane w `tools/gen_mcm.py` - jesli 169-171 ruszaja ten plik, scalac recznie | brak | tekstowa |
| T2 | brak | brak | 171 musi zachowac pominiecie zalog Innych (`ArmyClothing.cs:212`) |
| T3 | brak | BEE zjada rude/skore - liczby odniesienia (porownywac udzialy) | brak (P1-B dopiero po 171) |
| T4 | brak | brak | brak |
| T5 | okno IronBank w MoneyLedger - sprawdzic | brak | brak (A3 dotyka `AiGear.cs:193` - dlatego NIE tej nocy) |
| T6 (cz. 1) | tylko tekstowa (ArmouryBehavior, CHANGELOG); punkty (a), (b), (e), (f) przeniesione PO 169 (E2) - nie ruszamy `MoneyLedger` ani `PopulationLaw` | brak | brak |
| T7, T8 | T8: pomiar bazowy 169 najpierw | 170 zamyka druga produkcje wsi BEE - zgodne | brak |
| (uwaga 2.7) | - | - | **171 (zamek kupuje w miescie):** zakup zalogi to wydatek z pulapu sprzetu pana (166), nie dodatkowy kanal; dzis zolnierze i tak kupuja z sakiewek, a lord z `AiGear` - przy 171 nie dokladac trzeciego platnika tego samego kompletu bez licznika w logu |

---

## 5. Na pozniej - kolejnosc wpieta w rozdz. 12 PROJEKT

### 5.1 Kolejnosc

| Etap | Paczka (slowami gracza) | Miejsce w rozdz. 12 PROJEKT | Zalezy od | Test / kampania |
|---|---|---|---|---|
| E0 | 169 KSIEGA OBIEGU, 170 BEE (+ klucz 14 `EconomicEventDailySpawnChance` 0 - losowe "susze" BEE; sol i piwo jako potrzeby podstawowe), 171 zalogi | kol. 1-2 (w toku) | - | A/B |
| E1 | Paczki tej nocy T1-T8 | po 171 | 169-171 | 40 dob |
| E2 | Dopiski do 169 (tylko log): okupy i jency-lordowie (N8: liczba pojman na rod i rok, dni, kwoty AI-AI i gracza, dosypki gry - podstawa W z S7), sluby AI (posag hipotetyczny), "IronBank na sucho 168", towar wedrowcow BK (sztaby, wegiel, kopia na polce), zakup karawany BK (wiersz 24), wplywy kas miast rozbite na "praca i uslugi" / "towar", przyczyny wzrostu UMARLI, **"zalogi: przyrost bez werbunku na dobe" i milicja** (2.11); MIARA SWIATA cz. 2 (T6 a, b, e, f: ludnosc BK, dochody na glowe na obu podstawach i stosunek panowie / lud, dryf tabeli, Bliznaki, model produkcji) z wlasna refleksja BK | razem z 169 albo zaraz po | 169 | 40 dob |
| E3 | Pierwsze po nocy, male: okup gracza do porywacza (W3), kurier okupu bez dosypki (L4) - test reczny; zamrozone progi plonu (A1) po pomiarze 170; naprawa Zewu Innych i gorna granica premii (06 N2, N3) - przed doba 728; ustalenie, co rusza spiacych z rozkazem Hold (58% obudzen w probce 08-18-38 nie mialo cudzego rozkazu) i blokada cudzych rozkazow u spiacych (07 O-3), takze w naszym `CrossingLaw.AiTick` (`CrossingLaw.cs:111-146`: daje `SetMoveGoToPoint` wrogim lordom przy przeprawach bez sprawdzenia snu; biegnie co 4. wywolanie `OnTick`, nie "co ok. 2 s"), jesli zjazd po T1 > 0.3 | przed 164a | 170 (A1), T1 (O-3), T2 (N2) | 40 dob / recznie |
| E4 | TEMPO SWIATA 2: suwak jako mnoznik wyniku, podloga 1.0 x p, kary terenu x p, szacunki drogi AI x p x 17/24 (07 T-1..T-4) - przy 75% prawie bez skutku; jedna skala 4.75 w kodzie i dokumentach (08 M-1); komentarze roku 168 -> 364 | niezalezne od pieniadza; przed 164a | T1 | rok |
| E5 | 164a SZCZELNOSC pieniadza + towaru: **PROJEKT 164a w calosci** (tabela 4.1 wiersze 1b zold karawan notabli do sakiewek ludzi karawan ok. 120-160 tys./d, 3 awanse / ochotnik z mapy / statki, 6, 10 zold karawan lordow ok. 33 tys./d, 14, 16, 17; rozdz. 12 kol. 3) + dopiski syntezy: zloto z cial z sakiewek poleglych, oblezenie z kasy miasta, jency i turnieje BK z kas miast, wydatki lordow BK do skarbca, zakup karawany BK (wiersz 24), wedrowcy BK bez towaru i stali valyrianskiej z niczego (03 P1-C, pytanie 6.2-2). Bez wierszy 1b i 10 Z9 (reszta < 10 tys./d przez 28 dob) nie bedzie spelnione | kol. 3 | 169 | B (40 dob) |
| E6 | Prawo jenca wedlug krainy (10 Z2, polityka karna BK), takze poprawka "Forgiveness = pierwsza osada kultury" (S2) i zdjecie prawa niewoli BK dla Dorne | przed 164b | pytanie 6.1-6 | 40 dob |
| E7 | 108 ludzie jednostka + 109 przyrost + naprawa tabeli ludnosci (01 D) + **zamkniecie przyrostu zalog z niczego** (gra `GarrisonRecruitmentCampaignBehavior.cs:97-107` i BK Enlistment -> werbunek z ksiegi regionu z ubytkiem ludzi), milicji (z ksiegi regionu) i zalog nowych karawan i taborow (z ksiegi miasta / wsi) - 2.11 | kol. 4 | 170 | B |
| E8 | 164b jency i ochotnicy jako ludzie (bez drugiego dopisania jencow - S2) + jency z Westeros do Nocnej Strazy (04 N5) | kol. 5 | 108, E6 | B |
| E9 | R2 Nieumarli bez trupow z niczego (Armoury `UndeadLaw`), potem R4 pula cial | po 108 | 108, T2 | 40 dob + rok |
| E10 | H3 PRZEGRANI UCHODZA (04 W1): przegrany ginie 15-65% wedlug wzoru (minimum wzoru to 15%; czlon ujemny dla 5% - 2.8), zwyciezca najwyzej 5%; jeden podzial przegranych: zabici, jency, rozbici - z rozbitych `OutlawRoutedShare` (0.5, `Settings.cs:565`, `OutlawLaw.cs` ok. 340-357) do puli wyrzutkow, reszta do ksiegi; licznik w PeopleLedger: suma = ubytek z partii (bez podwojnego dopisania); najpierw znalezc w dekompilacji losowanie zgonu i pojmania w bitwie automatycznej | przed grupa C (zmienia koszt werbunku, na ktorym liczony jest budzet) | 108 (albo tymczasowo karczma 167) | rok |
| E11 | GRUPA C: 110 K5, 111' K6 (tryb 1), 162 DWOR, 112 K7, 163 MARKIETANI, 114, 164c (wezel notabli w 2.10) + **KIESA LUDU** (01 E, F, G: lud placi za jedzenie, place do ludu, robocizna rzemiosla) - **warunek wejscia:** rachunek `sim_krytyk` z kiesa ludu i zakupami wsi, jeden wariant naraz, bilans kazdego wezla 2.10, rozstrzygniecie S9, decyzja o jednym N albo jawnym zdaniu o dwoch skalach (S22), pulap sprzetu pana (2.7) | kol. 6 | 164a, 169 (D) | C, **nowa kampania** |
| E12 | 113 SPUSTOSZENIE + plon od rak (02 A2: e = 1.0 / 0.5, kapital rok na 50%) + wioski poboczne w rachunku (A3: Looted tylko czesc spalona, ludzie z ksiegi w dymku) + zywnosc warowni od rak (A5) + ksiega wiosek W3, potem wyglad spalonej wioski (08 W-8) | kol. 7 | 108, 109, W3 | C/D |
| E13 | 165 KORONA + prawo trzecich (1/9 lupu AI do czasu N7 - S21) + renta wsi ponad zapas 15 dni (S8) + pokoj z biedy (05 D3 / 04 W6, pytanie 6.1-7) | kol. 8 | 111', 114, 164 | D |
| E14 | 166 BUDZET RODU + dopiski 05 B1-B7 + armia tylko z pieniedzmi (04 W5) + dluznik nie buduje (05 A3) + **sprzet bez podwojnej zaplaty** (2.7: sakiewka zolnierza na komplet, pan tylko liberie, konie druzyny i uzupelnienie po kleskach, pulap ok. 15-20% zoldu; 171 w tym pulapie); T5 wylaczone | kol. 9 | 161/169, 108, 162, 165, 160 | E, zalecana nowa |
| E15 | 167 WETERANI | kol. 10 | 160, 108, 166 | E |
| E16 | 168 DLUG + dopiski 05 C1-C7 + OKUPY wedlug majatku (W2 z W z pomiaru N8, S7, pytanie 6.1-5) + **jeden wspolny pulap wszystkich rat (Bank + okupy + korona) ok. 10-15% D na dobe w jednej ksiedze dlugow** + zajecie po zwloce do polowy dochodu z lenn (elegit 1285 - 2.9) + posag przy slubach AI (05 D2) + zakladnik zamiast wyprzedazy (C4) | kol. 11 | 166, 165, BEE, 169 N8 | F (rok) |
| E17 | Autotest roczny i 4-letni + **ROK Z ZIMA** (obowiazkowo, nie tylko lato/jesien) - kalibracja jednej liczby naraz | kol. 12 | wszystkie | rok, 4 lata |
| E18 | TEMPO SWIATA 3: wariant Jeffa (L 20% albo H 12%), tempo wsi 40 / karawan 30 / morza 28, spojnosc armii x tempo, dluzsza noc zima (jesli tak), premia za droge (jesli tak) | po E17 | pytania 6.1-1, 6.1-2, 6.2-4 | rok |
| E19 | Towar i naprawy: lordowie naprawiaja tylko to, co nosza (03 P1-B, po 171); wiecej garbowania (P2-G) -> skora/tkanina/drewno sztuki w naprawie (P2-E) -> wlasne rece gracza jedna regula (P2-F); kasowanie BK -> zawor K13 (P1-D); zlom jako towar (P2-J, pytanie) | rownolegle do E11-E16 w osobnych krokach | 171, K13 | 40 dob / rok |
| E20 | Krainy i pory roku: tabela klimatu 16 + 5 wsi (02 C1, po pytaniu 6.2-1), zyznosc krain wyrownana (B1, po PROJEKT-GLOD), zima wedlug klimatu (B2, pytanie 6.2-1), konie jedza zima (10 Z4, po P1 i pytaniu), rok urodzaju z przyczyny (Z6), uboj jesienny i sol (Z7), rytm zniw (B3) | po PROJEKT-GLOD | 108-113, P1 | rok z zima |
| E21 | Mapa i obozy: AI placi za nocny marsz (07 O-4) razem z wpieciem martwego klucza `AiNightsAwakeInChase` (poscig/ucieczka spi po 1 dobie bez snu; dzis nikt go nie czyta), decyzja o 15% samotnych kolumn idacych noca (pytanie 6.1-3), gracz w armii AI bez dlugu snu (O-5), swiat obozuje niezaleznie od stanu gracza (O-6), czlonkowie w drodze na zbiorke spia (O-7), dzien odpoczynku armii co 7 dni (N-2), napad na spiacy oboz (N-3), marsz forsowny gracza (N-4), most szybszy niz brod (N-5), goniec ze zmiana koni (N-6), meldunek dni drogi dla armii (N-7) | po E18 (N-3 wczesniej) | T1 | 40 dob |
| E22 | Wioski i drogi (wyglad): pan w dymku i komunikat o paleniu wioski gracza (08 W-2, W-3 - zdjete z T7, po obejrzeniu przez Jeffa), dymek z herbem i czym zyje wioska (08 W-5), proporczyki (W-6), menu okregu (W-7), pelny widok drog z LOD (D-2), drogi Valyrii (D-3), szubienice przy rozstajach, pola wedlug pory roku, karczmy na rozstajach, ogniska przy namiotach obozu | po P2 | P2 | 40 dob + zdjecia |
| E23 | Nowe pomysly zalezne od Jeffa (5.3) | wedlug odpowiedzi | - | - |
| E24 | Optymalizacja gry (decyzja Jeffa 08.10: na koniec) | na koncu | - | - |

### 5.2 Dopiski do dokumentow (zero kodu, rano, przed skladaniem grupy C)

- PROJEKT 3: dopisac Z10 (praca -> ludzie, towar -> kupcy) i uwage "Z1 obejmuje towar i ludzi"; 4.1: wiersz 24 (zakup karawany BK w nicosc,
  `BKLordPropertyBehavior.cs:78`) i wiersze towaru (wedrowcy BK, `DeleteOverProduction`); 5.3: renta wsi ponad zapas 15 dni (S8); 8.2 pkt (2): okup AI istnieje (S1).
- Opisy 111', 162, 163, 164c: "kasa miasta = kupcy; lud osobno (KIESA LUDU)" (raport 01 C).
- Opis 164b i `REGULY-KRAIN-I-DLUGU` pkt 4: jeniec nie znika - BK dopisuje go do ludnosci osady (S2).
- Opis 166: B1-B7 z raportu 05 (D bez jednorazowych wplywow; okup i posag z G ponad R; zakupy zalog z 171 w pulapie sprzetu; stan dlugu zmienia udzialy;
  zakupy BK karawan/warsztatow tylko z nadwyzki; majatki rycerzy BK w pulapie; A1/T5 wylaczone po 166).
- Opis 168: C1-C7 z raportu 05 (odsetki zamrozone od 1. dnia zajecia - dzis `IronBank.cs:223-224` liczy je bankrutom; kredyt po zajeciu dopiero po 182 dobach;
  korona przejmuje zalegly dlug wasala; limit z D - dowod Stark 36-203 tys.).
- `AUDYT-SUROWCE.md:45` i audyt lawy rozdz. 1: czynny jest model gry z progami (S19).
- STAN-PRAC/CLAUDE.md: kwot z ksiazek nie wpisywac 1:1 (raport 09 P-7). **Liczby przelicznika (36-80 d czy ok. 280 d za smoka, czy skala "skromna")
  NIE wpisywac przed odpowiedzia Jeffa na 6.2-7** (poprawione po krytyce). W 09 2.0 / 2.16 dopisac kotwice konia z The Hedge Knight (6.2-7)
  i to, ze gra nazywa pieniadze tez "denars" (ROT `str_english.xml:6397`, "100 denars" - pojedynczy napis).
- Opis 166 (dopisane po krytyce): sprzet bez podwojnej zaplaty (2.7) - liczba 545 to reszta podzialu K11, nie potrzeba; pulap pana ok. 15-20% zoldu [S].
- Opis 168 (dopisane po krytyce): jeden pulap wszystkich rat; elegit = polowa ziem do splaty (2.9); W okupu z pomiaru N8 (S7).
- Raport 09 tab. 3: przy kazdym "na glowe" podstawa (ksiega 52.6 / ksiega 90.5 / BK 3.1 mln); wiersze renta, danina, korona na glowe BK = 2-10x za duzo (S22).
  Raport 09 2.1-2.4 i tab. 3: przy cenach z lat po 1350 (Tower 1353-1399, Richardson 1369, lucznik 1415) dopisac rok i [S +-40%] - po zarazie place
  nominalne wzrosly ok. 1.5-2x (robotnik 1.5 -> 3.0 d), wiec rzeczy, w ktorych dominuje praca (zbroja: 75-95%), sa w pensach z 1300 r. zawyzone
  ok. 1.3-1.7x wobec chleba; na razie bez zmian w grze.
- Raport 03:170 i T3: "4% kolczugi na sztuke" -> "srednio 4% (Tower 1399, naprawa i powiekszanie), ciezko ok. 25% kolek ze zlomu" (S23).
- `HISTORIA-RABUNKU-I-BITEW-2026-10-07.md:359`: zakres wzoru to 15-65%, nie 5-65% (2.8, E10).
- Raport 07:15, 26, 160, 238 i 6.1-1: wariant L = "plan Stannisa / marsz forsowny", z dopiskiem, ile ten marsz trwal naprawde (2.1).
- Raport 01 4.4 i ta synteza: "lud miast ok. 560-620" tylko przy podziale 01 (2.10).

### 5.3 Rzeczy, ktorych nie mamy, a gra na nie pozwala (ranking: wartosc / koszt)

| # | Pomysl (slowami gracza) | Dlaczego | Koszt | Zmienia Twoja gre | Kiedy |
|---|---|---|---|---|---|
| 1 | Sprawdzic zime, zanim przyjdzie (log zapasow + autotest zimowy) | pierwsza zima 3-5 lat z polowa plonow, przychodzi po 1.2-2 latach (srednio ok. 1.6: lato jeszcze 120-270 dob + pierwsza jesien 300-450 dob, `Settings.cs:592-595`), nikt jej nie widzial | mala | nie | tej nocy (T6, P1) |
| 2 | Prawo jenca wedlug krainy (Westeros dom albo Mur, Zelazne Wyspy thrall; z niewola wedlug kanonu: Zatoka, Volantis, Lys, Myr, Tyrosh, Norvos, Qarth; bez: Braavos, Pentos; niepewne: Qohor, Lorath) | kanon: w Westeros niewola zakazana | mala-srednia | tak | E6, pytanie (tylko Westeros, Qohor, Lorath) |
| 3 | Myto (Bliznaki x4, mosty, brody, brama miasta) - jedna regula | Freyowie zyja z myta; murage [H], pontage - stawki [S] (S16) | mala | tak | po T6 cz. 2, pytanie |
| 3a | **Zalogi i milicje z ludzi, nie z niczego** (dopisane po krytyce) | najwiekszy dzis kanal ludzi z niczego: +32.8 tys. w zalogach, +12 tys. w milicjach w roku (2.11) | srednia | tak (zalogi rosna wolniej, AI musi werbowac) | E7 |
| 4 | Konie jedza zima (pol paszy BK tam, gdzie BK jeszcze nie liczy) | zimowa kampania konna stala na owsie i sianie [H] | mala | tak | po P1, pytanie |
| 5 | Napad na spiacy oboz (obroncy w nieladzie) + ogniska przy namiotach | nocne napady maja sens; widac, kto obozuje | srednia | tak | E21 |
| 6 | Dluzsza noc zima (zima 22-8, wiosna/jesien 23-7, lato 0-6) | zimowy dzien krotki; marsz Stannisa stanal w sniegu | mala | tak | po T1, pytanie |
| 7 | Okup okolicy zamiast palenia wsi (appatis) i okup miasta zamiast szturmu | wojna stuletnia: zalogi zyly z okupu okolicy [H] | duza | tak | po 112, 113, 166 |
| 8 | Posag przy slubach AI i pomoc wasali na okup pana | jedyny kanal od bogatych do biednych rodow poza korona; Magna Carta kl. 12 | srednia | AI | E16 (pomoc wasali tylko na slowo Jeffa - razem z jego pomyslem o kasach miast) |
| 9 | Zlom jako towar (kowale skupuja wraki i lataja nimi) | Tower: stara kolczuga = zlom 7-14% nowej [H] | srednia | tak | E19, pytanie |
| 10 | Stal valyrianska jako zasob skonczony (tylko przekuwanie w Qohorze) | kanon | mala | tak | E5, pytanie |
| 11 | Dziesiecina i swiatynie (jalmuzna w glodzie, budowa septow) | Wiara bogata i wierzycielem korony | duza | tak | po E11, pytanie |
| 12 | Jarmark 14 dni po zniwach z oplata targowa do kasy miasta | Szampania: jarmarki 2-6 tygodni [H] | srednia | tak | po 169 |
| 13 | Barki: tansza droga wzdluz rzek w wyborze miasta przez wozy i karawany | ladem 2 x drozej niz rzeka (Masschaele 1993) [H] | srednia | malo | po 169 |
| 14 | Turniejowy wykup konia i zbroi (GrandTourney) | The Hedge Knight | mala | tak | dowolnie |
| 15 | Wiesci z opoznieniem (kruki) - tylko dla gracza | immersja | srednia | tak | pytanie |
| 16 | Smoki jedza owce z taboru, bez nich porywaja stada wsi; wyczerpane zloto Casterly Rock; zimowe sztormy na morzu; zakladnicy przy pokoju | lore | rozne | tak | pytania |

---

## 6. Pytania do Jeffa (tylko to, co zmienia rozgrywke albo kanon)

### 6.1 Pilne (od nich zalezy najblizsza praca)

1. **Jak wolny ma byc swiat?** Dzis armia z Winterfell do Krolewskiej Przystani idzie ok. 21 dni. "Plan Stannisa / marsz forsowny" (L, 32 km dziennie):
   armia ok. 77 dni, lord 49, goniec 33. "Historia z taborem" (H, ok. 19 km dziennie): armia 129, goniec 56. (Stannis planowal 32 km dziennie, ale w sniegu
   szedl naprawde ok. 10-13 km.) Wiesniacy i karawany dostana wlasne, szybsze tempo, zeby miasta nie glodowaly. We wrzesniu prosiles o przyspieszenie
   swiata o 50% - czy na pewno chcesz teraz duzo wolniej? Rekomendacja: L, ale dopiero po naprawach ekonomii (rok testu); po tej nocy w logu bedzie
   prawdziwy pomiar km na dobe (T6), wiec decyzja oprze sie na liczbie z gry, nie z szacunku.
2. **Dluzsza noc w zimie?** Oboz zima 22-8, wiosna i jesien 23-7, lato 0-6 (zimowy marsz wolniejszy o 2-4 godziny dziennie). Rekomendacja: tak.
3. **Oboz o polnocy i samotne kolumny.** Pytanie o oboz u Ciebie przychodzi o 0:00, jak powiedziales ("od 24"); po tej nocy suwak godzin w ustawieniach
   da sie przestawic na dowolna godzine 0-23 (dzis generator suwakow pozwalalby tylko 0-10). Armia rozbija oboz zawsze (chyba ze wrog blisko albo poscig).
   Samotne kolumny lordow: dzis co noc 15% z nich idzie dalej (ustawienie "Ai Camp Skip Percent") - zostawic, czy maja spac wszystkie?
4. **Inni:** w 1. i 2. roku walcza w polu za Murem, ale nie zdobywaja zadnej warowni (Piesc i Crastera od 3. roku) - czy wolno im wziac Piesc juz w 1. roku,
   jak w ksiazkach (299 AC), ale bez trzymania? Mur: najwczesniej w 7. roku (jak w serialu) czy "nigdy bez Twojej zgody" (ksiazki nie doszly do upadku Muru)?
5. **Okupy wedlug majatku**, jedna regula dla Ciebie i AI. Najpierw zmierzymy, jak czesto lordowie trafiaja do niewoli; potem okup ustawimy tak, zeby
   rod placil na okupy srednio najwyzej 5-10% rocznego dochodu, a pojedynczy okup dalo sie splacic w ok. rok (glowa rodu wstepnie ok. 1-1.5 miesiaca
   dochodu rodu - mniej niz w historii, bo w grze wodzowie trafiaja do niewoli wiele razy, a historyczny magnat raz w zyciu). Placi gotowka z nadwyzki,
   reszta w ratach, a wszystkie raty rodu razem (Bank, okupy, korona) najwyzej 10-15% dziennego dochodu - zeby nie bylo fali bankructw. Twoje okupy
   za glowy bogatych rodow wzrosna; za biednych: (a) spadna (jedna regula, bez dzisiejszej podlogi 8 / 25 / 100 tys.) albo (b) zostana jak dzis (podloga
   zostaje). Tak, i ktory wariant dla biednych?
6. **Jency w Westeros:** sprzedany jeniec wraca do domu jako chlop swojej krainy, czy idzie na Mur do Nocnej Strazy? W Essos kanon rozstrzyga sam:
   Braavos i Pentos bez niewoli; Zatoka, Volantis, Lys, Myr, Tyrosh, Norvos i Qarth z niewola. Niepewne sa tylko Qohor (straz z Nieskalanych, ale
   prawo podobno jak w Pentos) i Lorath (dawne schronienie zbieglych) - jak je traktowac?
7. **Pokoj z braku pieniedzy:** krolestwo z pustym skarbcem i biednymi wasalami chetniej zawiera rozejm, biedne rzadziej wypowiada wojne (wojny fabularne
   - Mur, Inni - bez zmian). Rekomendacja: tak.
8. **Prawo trzecich:** jako wasal krola, ktory w wojnie placi Ci polowe zoldu, oddajesz koronie trzecia czesc swojej "trzeciej" (czyli ok. 1/9 lupu
   i okupow - tak bylo w Anglii); lordowie AI tak samo, 1/9 tego, co sprzedadza. Za pojmanego krola albo wodza wroga korona przejmuje jenca i placi Ci
   odszkodowanie (czesc okupu). Tak?

### 6.2 Moga poczekac

1. **Krainy i zima:** za Murem zero rolnictwa (Wolni Ludzie zboze tylko z rabunku i wymiany)? Skagos i Smocza Skala: farmy zboza czy owce i kozy?
   Poludnie Essos i Dorne lagodniejsza zima, Wyspy Letnie bez zimy?
2. **Stal valyrianska** skonczona jak w kanonie (na targach przestana pojawiac sie nowe sztaby, przekuwanie tylko w Qohorze)?
3. **Miasta i Wiara:** miasta bez wojska, dworu i pracy maja ubozec, a Ty jako pan miasta dostajesz mniej "od razu" (pieniadz wraca przez targ)? Dziesiecina
   dla septow (ok. 5% utargu wsi i 1-3% dochodu mieszczan, wraca jako jalmuzna i budowa)?
4. **Drogi:** tylko widok, czy tez "po drodze szybciej" (+15% dla wszystkich; AI i tak nie wybiera drog)? Ile rysowac: wszystkie, bez sciezek do wiosek, same trakty?
5. **Myto:** na Bliznakach, mostach i brodach placisz jak kazdy obcy (kilkadziesiat do kilkuset zlotych z druzyna); lordowie krolestwa Freyow za darmo?
6. **Konie w zimie** jedza (kon jak ok. 2.5 czlowieka, kon bojowy jak 5)? **Smoki** jedza owce z taboru, a bez nich porywaja stada wsi?
   **Zloto Casterly Rock** wyczerpane jak w ksiazkach? **Zlom** jako towar u kowali?
7. **Kwoty z lore - jak przeliczac zlote smoki?** (poprawione po krytyce - poprzednie pytanie bylo postawione na odwrot). Jedyna kotwica cen w kanonie:
   w "The Hedge Knight" Dunk sprzedaje klacz Sweetfoot za 750 srebrnych jeleni (https://awoiaf.westeros.org/index.php/Sweetfoot), czyli ok. 3.6 smoka
   (1 smok = 210 jeleni). Dwie uczciwe opcje:
   (a) **1 smok = ok. 36-80 monet** (zlota moneta jak floren / noble): kon Dunka kosztuje 130-290 monet, czyli tyle co kon juczny; turniej Reki
       (40 000 smokow) to 1.4-3.2 mln monet = majatek hrabiego, a dlug korony (6 mln smokow) jest gigantyczny - jak w ksiazkach. Turnieje GrandTourney
       (dzis 400-1 250 monet nagrody) bylyby przy tym malenkie.
   (b) **1 smok = ok. 280 monet** (z ceny konia: wierzchowiec w grze ok. 1 000 monet [S], 1 smok = 210 jeleni wedlug AWOIAF): ceny rzeczy zgodne z gra,
       ale kwoty z ksiazek jeszcze wieksze.
   (c) **Skala "skromna"** (kwoty z ksiazek dzielone przez ok. 1 000-3 000): turnieje i dlugi jak dzis w grze, ale kon Dunka nie pasuje.
   Ktora? Do tego czasu zadnej liczby nie wpisujemy do zasad projektu.

### 6.3 Bez pytania (decyzje projektu, z uzasadnieniem w raportach)

Godziny obozu jako ustawienie (0 i 6); armia zawsze obozuje (prosba Jeffa); straznik w czasie gry; metal napraw wedlug rodzaju (robocizna i kolczuga
bez zmian); dezerterzy do lasu; rodzina pomaga splacac rate; papirus i las wedlug klimatu; lordowie AI naprawiaja tylko to, co nosza ich ludzie (lup sprzedaja -
tak bylo w historii); wedrowcy BK bez towaru z niczego; kasowanie towaru przez BK zastapione zaworem; skala mapy 4.75 km; zalogi i milicje z ludzi (E7);
sprzet bez podwojnej zaplaty (166); udzial korony 1/9 lupu (S21); stawki kiesy ludu, podatkow i myta - ale dopiero po rachunku 2.10 (S9 otwarte); kolejnosc paczek.

---

## 7. Pliki i weryfikacja

- Ten plik (jedyny zapisany). Raporty zrodlowe: `docs/audyt-2026-10-09/01-...10-*.md`.
- Sprawdzone osobno przy syntezie [K]: gra `RansomOfferCampaignBehavior.cs:64-70, 102-108, 174-176` (okup AI-AI istnieje; dosypka kuriera), `Campaign.cs:874`,
  `MobileParty.cs:2795-2800`, `MapTimeTracker.cs:49-53` (1 dt = 1.2 h); BK `RansomOfferCampaignBehaviorPatches.cs`, `SettlementPatches.cs:21-92`,
  `PolicyManager.cs:209`; ROT `OthersPatches.cs:272-287`; Armoury `WorldPace.cs:48-59, 155-163`, `NightRest.cs:83-160, 181, 194, 242-262, 395-406, 448-449,
  511-512, 840-862, 1180-1210`, `Wayfinder.cs:55-68`, `MendMaterial.cs:76-106`, `WarLedger.cs:80-92, 113-139`, `IronBank.cs:200-240`, `OutlawLaw.cs:329`,
  `PeopleLedger.cs:163, 257-263, 312`, `PopulationLaw.cs:327`, `CrossingLaw.cs:120-136`, `MineralOnce.cs:75-82`, `VillageWoodlot.cs:95-103`, `TerrainEase.cs:143`,
  `MapVillagesView.cs:1868-1880, 2158, 3287, 4418-4430`, `VillageTexts.cs:9-27`, `Settings.cs:592-595, 693, 699, 703, 748`; CrashScribe `Config.cs:28-92`,
  `NightKingCall.cs:186-200, 334-346, 400-416`.
- [P] `Modules/Armoury/Armoury-2026-10-08_11-40-05.log:39, 209`, `..._08-18-38.log:38, 203`, `..._11-32-39.log:204` (WorldPace i Audyt predkosci);
  `Armoury.json:48, 118`; STAN-PRAC 05.10-08.10; PRZEKAZANIE rozdz. 16; CHANGELOG:3537 (prosba Jeffa o szybszy swiat).
- **Sprawdzone przy odpowiedzi na krytyke [K]:** Armoury `NightRest.cs:174-181, 194-238, 240-310, 395-470, 840-862, 1240-1263` (zegary realne, IsStill
  z progiem 0.08, pomijanie obozu przed warunkiem armii, brak resetu pol statycznych w Import), `Settings.cs:171, 213, 421, 560-575, 588-600, 638, 685-712, 744-752`,
  `McmSettings.cs:2457, 2465, 3347, 3349` (AiNightsAwakeInChase czyta tylko MCM), `tools/gen_mcm.py:38-42` (zakres suwaka int), `Wayfinder.cs:23, 55-68`,
  `TerrainEase.cs:85-104`, `CrossingLaw.cs:105-146`, `PopulationLaw.cs:140-215, 290-331`, `MendMaterial.cs:1-120`, `ArmsPricing.cs:170-215`,
  `MineralOnce.cs:1-130`, `PeopleLedger.cs:250-312`, `SpoilsSeal.cs:118-138`, `ArmouryBehavior.cs:425, 502-606, 1209-1265`; gra `Campaign.cs:840-875`
  (1 s realna = 0.3 h gry przy x1, 2.4 h przy x8), `CampaignTime.cs:80-102` (noc = godzina < SunRise albo >= SunSet), `GarrisonRecruitmentCampaignBehavior.cs:80-107`;
  ROT `ROTCampaignTimeModel.cs` (SunRise 2, SunSet 22); BK `VanillaModelTweakPatches.cs:1740-1762`, `BKSettlementBehavior.cs:450-466`, `BKTaxModel.cs:313-320`.
  [P] `Logs/2026-10-08_08-18-38/noc.log` (1 963 linii "obudzony": Hold 1 142, GoToSettlement 564, GoToPoint 231; srednio 1.29, maks. 5.69 w probce;
  razem w sesji 39 200), `Armoury-2026-10-08_08-18-38.log` linie "Ludzie:" 108836 / 108874 / 109199 i "IronBank: dzien" 108837-108876; `Armoury.json:27, 89, 118, 289-294`;
  `D:/Backup-Bannerlord/autotest-kopie/2026-10-08_06-51-39` (tylko DLL i Configs). [H] https://en.wikipedia.org/wiki/Pontage (brak stawek),
  https://en.wikipedia.org/wiki/Elegit (polowa ziem), https://awoiaf.westeros.org/index.php/Slavery , https://awoiaf.westeros.org/index.php/Qarth ,
  https://awoiaf.westeros.org/index.php/Norvos , https://awoiaf.westeros.org/index.php/Sweetfoot (750 jeleni); docs `HISTORIA-KOSZT-NAPRAWY-2026-10-07.md:165-205`,
  `HISTORIA-RABUNKU-I-BITEW-2026-10-07.md:355-393`, `EKONOMIA-FUNDAMENT-2026-10-05.md:405-470`, `PROJEKT-EKONOMIA-OBIEG-2026-10-08.md:128, 271, 525-528` i tab. 4.1,
  raporty 01:195-226, 03:158-171, 07:15-27, 160, 09:170-180, 225-236, 268-276, 320-360, 440-470.

---

## 8. Krytyka i odpowiedzi (09.10)

Dwa przebiegi krytyki: R = realizm (15 uwag), S = szczelnosc i rozgrywka (18 uwag). Kazda sprawdzona w kodzie, logu albo zrodle (rozdz. 7).
Wynik: 31 przyjetych w calosci, 2 w czesci, 0 odrzuconych w calosci.

| # | Uwaga (skrot) | Waga | Werdykt | Co zmienione / dlaczego |
|---|---|---|---|---|
| R1 | 2.10 bierze zawor w podziale PROJEKT, a "lud ok. 560-620" z wariantu 01 (ta sama moneta dwa razy); patrycjat bez platnika; zawor 7% nadwyzki = podatek od zysku, nie fee farm | krytyczne | **Przyjeta** | 2.10 przepisane na bilans wezlow (lud miast przy PROJEKT: 393-423 wobec 560 = 64-71% konsumpcji); S9 otwarte z warunkiem rachunku przed grupa C; 2.0 i 2.5: zawor nazwany zaworem gry (Z2); 2.4: patrycjat bez platnika przy PROJEKT |
| R2 | Przeliczniki niejedne: wojsko na ksiedze, lud i pieniadz na BK; rody 6-14x za bogate, pan ze wsi 5x, wojsko 5.8% BK; 0.19% bez zalog; 09 tab. 3 na zawyzonej ksiedze | wazne | **Przyjeta** | 2.2 "Dwie skale" i wiersz Wojsko (0.34% ksiegi z zalogami, 5.8% BK); 2.0 zasada podstawy "na glowe"; 2.5 70% = stawka BK, nie historia; S22; 5.2 (09 tab. 3); T6 cz. 2 wypisze oba; decyzja o N przy E11 |
| R3 | T3 dla kolczugi przeczy zrodlu (ciezko = 1/4 kolek; 4% to srednia Tower); punkt 1.0 nie dziala (wraki); dzis 15% przy Battered blizej historii | wazne | **Przyjeta** (druga mozliwosc krytyka) | Kolczuga zostaje na starej regule; tabela T3 na prawdziwych stanach 0.45 / 0.6 / 0.75; nie przyjmujemy pierwszej mozliwosci (25% przy Battered), bo podnioslaby metal ponad dzisiejsze 15% w paczce "tylko obnizki"; S23, 2.3, 5.2 |
| R4 | Okup glowy 0.5 roku D od jednego przypadku; czestosci nikt nie mierzy; raty 5 lat; "dolna polowa historii" to nieprawda; podloga RC przeczy "za biednych spadna" | wazne | **Przyjeta** | 2.8 i S7: W z pomiaru N8, cel 5-10% D rocznie, okup splacalny w ok. rok, wspolny pulap rat 10-15% D; "swiadomie ponizej historii"; 6.1-5 z dwoma wariantami podlogi; E16 |
| R5 | Korona 1/3 calego lupu AI to ok. 3x historia (Hay: 1/9); gracz ma 1/9 - dwie reguly | wazne | **Przyjeta** | 2.8 i S21: 1/9 do czasu N7, potem 1/3 czesci lorda; "2/3 za wielkiego jenca" oznaczone [S], docelowo odszkodowanie; 6.1-8, E13 |
| R6 | Sprzet 545 > zold 475; zolnierz kupuje z sakiewki, a lord osobno z AiGear - dwa razy; 171 dokleja trzeci kanal | wazne | **Przyjeta** | 2.7 "Sprzet placony dwa razy" (pulap pana 15-20% zoldu [S]); E14; kolizje z 171; 5.2 opis 166; 2.10 jako kandydat na pokrycie luki |
| R7 | Pytanie o smoki postawione na odwrot (36-80 d daje ogromne turnieje, nie skromne); kon Dunka | wazne | **Przyjeta** | 6.2-7 z trzema opcjami i koniem z The Hedge Knight (750 jeleni - sprawdzone; szczegolu "3 smoki i srebro" nie potwierdzilem, wiec go nie uzywam); 5.2: liczby do CLAUDE.md dopiero po odpowiedzi Jeffa |
| R8 | "Jak w ksiazkach" to tylko PLAN Stannisa; naprawde 10-13 km/dobe | drobne | **Przyjeta** | 1 pkt 1, 2.1, 6.1-1, 5.2 (raport 07) |
| R9 | T1 "+3% drogi" zle - ma byc +5.9% | drobne | **Czesciowo** | "+3%" bylo zle, ale "+5.9%" pomija kare nocna 50% w godzinach 22-02 (`TerrainEase.cs:102`, ROT SunSet 22 / SunRise 2): marsz 6-24 = 16 + 2 x 0.5 = 17 h, tyle co dzis 5-22. Przyjeta wersja S12 (0%); 2.1, T1, 1 pkt 2 |
| R10 | Wzor strat ma minimum 15%, nie 5% (St Albans 15% vs 5%) | drobne | **Przyjeta** | 2.8 "15-65%" z propozycja czlonu ujemnego; E10; 5.2 (HISTORIA-RABUNKU:359) |
| R11 | Strona Pontage nie podaje stawek | drobne | **Przyjeta** (sprawdzone) | S16: pontage [S] przez analogie do murage; 5.3 #3 |
| R12 | Elegit = polowa ziem, nie "dochod ziemi"; rata 10% D to wybor gry | drobne | **Przyjeta** (sprawdzone) | 2.9 rozdziela rate dobrowolna i zajecie do polowy dochodu; E16 |
| R13 | Klucz cen miesza epoki (zywnosc 1300-49, zbroje 1353-1415); "prawie dokladnie jak 1300" za mocne | drobne | **Przyjeta** | 1 pkt 5 ("zbroje raczej o 1/3 za drogie"); 5.2 (rok i [S +-40%] przy cenach po 1350); gra bez zmian |
| R14 | Norvos, Qohor, Lorath, Qarth rozstrzyga kanon - nie pytac | drobne | **Czesciowo** | Norvos (swieta straz niewolnych) i Qarth (zyje z niewolnikow) - potwierdzone na AWOIAF, wpisane; Qohor NIE jest pewny (AWOIAF: straz z Nieskalanych, ale prawo "jak w Pentos", zakaz lamany) i Lorath niepewny - zostaja w pytaniu; 2.8, 6.1-6, 5.3 #2 |
| R15 | Pierwsza zima po 1.15-1.98 roku, nie 1.3 | drobne | **Przyjeta** (sprawdzone `Settings.cs:592-595`) | 1 pkt 9, 5.3 #1 |
| S1 | 2.10 nie bilansuje sie: podwojna moneta ludu; wies nie zaplaci renty 176, gdy kupuje za 150 | krytyczne | **Przyjeta** | 2.10 tabela wezlow (renta wsi realnie 0-35); 2.5 wiersz "Pan ze wsi"; S9; E11 warunek wejscia |
| S2 | Najwiekszy kanal ludzi z niczego - przyrost zalog (gra + BK Enlistment), milicje, zalogi karawan | krytyczne | **Przyjeta** (sprawdzone w dekompilacji i logu) | 2.2, 2.11 (3 nowe wiersze), E2 (pomiar), E7 (zamkniecie), 5.3 #3a, 1 pkt 9 |
| S3 | Odswiezanie namiotow co 0.1 h zmienia znaczenie IsStill; wolne kolumny noca dostalyby namiot, ktory jedzie | wazne | **Przyjeta** | T1: `RefreshNearbyTents` zostaje na zegarze realnym, `IsStill` liczy prog od czasu gry, nowe `_tentPos` + `DropMovedTents()` co 0.1 h gry, licznik zjazdu w tescie |
| S4 | Statyczne zegary CampaignTime nie zerowane - po wczytaniu wczesniejszego zapisu straznik stoi | wazne | **Przyjeta** | T1: `ResetWorld()` w konstruktorze ArmouryBehavior + ujemna roznica = uruchom od razu; test reczny wczytania |
| S5 | gen_mcm daje suwaki 0..10 i 0..24; s == e usypia swiat na 24 h | wazne | **Przyjeta** (sprawdzone `gen_mcm.py:38-42`) | T1: `InCamp` z s == e -> false i mod 24; nadpisanie zakresow 0-23 w gen_mcm; test; 6.1-3 |
| S6 | 15% wodzow armii nie obozuje (cala armia idzie); AiNightsAwakeInChase martwy | wazne | **Przyjeta** (sprawdzone `NightRest.cs:444-448`, McmSettings) | T1: wodz armii bez pomijania; opis martwego klucza; 1 pkt 4; 6.1-3 (15% samotnych kolumn); E21 (O-4) |
| S7 | Mediana przesuniecia netto zanizy km/dobe; 4.75 w kodzie | wazne | **Przyjeta** | T6 cz. 1: suma godzinowa tylko w ruchu, mediana i p90, >= 12 h ruchu, osobno wodzowie; kryterium po pierwszym pomiarze; 4.75 w 2.0 i 2.1 |
| S8 | E5 pomija pozycje 164a z PROJEKT (zold karawan notabli i lordow, awanse, ochotnik, statki) | wazne | **Przyjeta** (sprawdzone PROJEKT:525 i tab. 4.1) | E5: "PROJEKT 164a w calosci + dopiski" |
| S9 | Raty okupow 5-10 lat, brak lacznego pulapu, zbiornik dlugu | wazne | **Przyjeta** (razem z R4) | 2.8, S7, E16 |
| S10 | Przebiegu 06:51 nie ma na dysku | drobne | **Przyjeta** (sprawdzone) | T5 i 4.0: odniesienie doby 1-40 rocznego (57 pozyczek, 362 527 zl, 0 spoznien, 0 bankructw) |
| S11 | T4 to nie "bardzo male" - ok. +50% rocznego przyrostu puli wyrzutkow | drobne | **Przyjeta** | T4: ryzyko jako zmiana rozgrywki, roczny test band; 1 pkt 8 |
| S12 | Przy obozie 0-6 dystans dzienny AI bez zmian (17 h); meldunek 24/18 zawyza | drobne | **Przyjeta** | 2.1, T1 (Wayfinder 24 / 17, "(night pace)"), 1 pkt 2 |
| S13 | T6 (a)(b) zmienia `KeptTownLines` w sciezce renty - to nie odczyt | drobne | **Przyjeta** | T6 podzielone: tej nocy (c') i (d) bez refleksji BK; reszta po 169 z wlasna refleksja |
| S14 | 58% obudzen bez cudzego rozkazu; CrossingLaw.AiTick daje rozkazy spiacym; maks. z probki to nie maksimum | drobne | **Przyjeta** (sprawdzone w noc.log i `CrossingLaw.cs:111-146`) | 1 pkt 3; T1 liczy sume i maks. ze wszystkich obudzen oraz rodzaj rozkazu; E3 (przyczyna Hold, CrossingLaw pomija spiacych) |
| S15 | Paczki testowane osobno na rownoleglych galeziach - wgrywany zestaw bez testu w calosci | drobne | **Przyjeta** | 4.0: narastajaco, wspolny autotest na koncu, md5 |
| S16 | H3 "reszta do ksiegi" koliduje z OutlawRoutedShare 0.5 | drobne | **Przyjeta** | 2.8 i E10: jeden podzial przegranych, licznik sumy |
| S17 | Filtr papirusu za bramka MineralsCountedOnce; zdjecie wpisu zmienia sume wag | drobne | **Przyjeta** (sprawdzone `MineralOnce.cs:85, 89`) | T8: wlasna bramka, waga na glowny plon, test z wylaczona latka mineralow |
| S18 | Dymek i komunikat T7 nie do sprawdzenia autotestem | drobne | **Przyjeta** | T7 tylko mlyny; W-2 i W-3 do E22 po obejrzeniu przez Jeffa |

**Paczki tej nocy po krytyce (wszystkie zostaja, zmieniony zakres):** T1 (rozszerzona o armie, poprawki straznika, MCM, reset), T2 (bez zmian - brak uwag),
T3 (bez kolczugi), T4 (ryzyko opisane jako zmiana rozgrywki), T5 (nowe odniesienie testu), T6 (tylko czesc 1: marsz i zapasy), T7 (tylko mlyny),
T8 (wlasna bramka, ostatnia). Nic z grupy C, okupow ani myta - te czekaja na rachunek 2.10 i odpowiedzi Jeffa.
