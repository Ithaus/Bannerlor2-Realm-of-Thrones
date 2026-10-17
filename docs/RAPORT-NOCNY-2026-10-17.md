# Raport nocny 16/17.10.2026 (dla Jeffa)

> SZKIC - uzupelniany. Ostatnia aktualizacja: 17.10 ok. 02:00 zegara komp. (PDT) = 11:00 u Ciebie.

## Najkrocej

- W grze jest **WGRANIE 75** (od 17.10 12:40 zegara komp.: WGRANIE 74 + daniny sredniowieczne wsi). Gra sie uruchamia, autotesty przeszly, w logach Armoury 0 bledow. **Mozesz wczytac zapis i grac.**
- Twoj zapis Ironman jest nietkniety - autotesty uzywaja wylacznie zapisu testowego F.
- **Kalibracja startu rynku (KS - "kazdy towar 95-105%") NIE jest jeszcze w grze.** Pierwszy test 120 dni jej nie przeszedl, wiec jej nie wgralem. Naprawa trwa (rozdz. 3).
- Kazda nowa rzecz ma wlasny wylacznik w MCM (Armoury).

## 1. Co jest w grze (wgrania 16.10)

| Wgranie | Co |
|---|---|
| 67 | SI walki Realistic. Twoja kampania juz miala wszystko na Realistic, wiec nic sie nie zmienilo. |
| 68 | Bitwa: panika i ucieczka rannych. Battle Atmosphere i Immersive Battlefields wlaczone i ustawione. Nasze okrzyki ustepuja tym modom. Autotest gra teraz prawdziwe bitwy (lapie crashe w misji). |
| 69 | Turniej: nagrode i zaklad placi kasa miasta, potem rod gospodarza, potem rod panujacy. Czego nie maja, to dlug wobec Ciebie, splacany codziennie. Wyrownanie Seagard 1522 zlota (raz). |
| 70 | Wioski: o 20% mniej, nie tak gesto, rozne wielkosci (6-36 osad). Herb gospodarza (rycerza albo pana), nie tylko krolestwa. Stary zapis przechodzi na nowy uklad. |
| 71 | Kaluze krwi na ziemi (wlasne, poprawne - bez crasha MoreBloodPoolMod, ktory zostaje wylaczony). |
| 72 | Karczmy w miescie, 3 jakosci. Ludzie placa za siebie i swoje konie, Ty za siebie i konie luzem. Premia do predkosci i morale do zmierzchu. |
| 73 | Herby kanoniczne GoT: 540 rodow, 1188 wiosek z 319 rodow. Dwie flagi: gospodarz i senior. |
| 74 | Zycie codzienne - pomiar (stany: chlopi, mieszczanie, szlachta...; Dostatek, spizarnia). Naprawy dziwnych drog towarow (mieso wieloryba, piwo, deski, wegiel, sztabki, jedzenie z niczego). Stany ludzi na ekranie wioski i miasta. |

## 2. Na co patrzec przy pierwszym wczytaniu

- Turniej: komunikat o wyrownaniu Seagard (1522) i splaty w kolejnych dniach.
- Karczma: rachunek w menu, pobudka o swicie, premia do predkosci.
- Bitwa: krzyk paniki raz na czlowieka, ranni uciekaja, czy dzwiekow nie jest za duzo.
- Wioski: herby rycerzy i dwie flagi. Po dlugim zwiedzaniu bez bitwy czesc wiosek moze pokazac herb pana (limit 160 tekstur herbow - znane, do poprawki).

## 3. Kalibracja startu (KS) - stan

**Cel (Twoje slowa):** "musimy zaczac rynkowo ... czegos moze byc mniej o 5%, czegos wiecej o 5%". Produkcja z prawdziwych rak do pracy i ziemi, bez sztucznych mnoznikow.

**Co dziala:**
- Plan liczy dla kazdego okregu, ilu ludzi pracuje i przy czym (miejsca pracy x wydajnosc ziemi).
- Na papierze 30 towarow wychodzi w pasmie 95-105%. Poza nim jest tylko welna Westeros (107%).

**Co nie przeszlo w tescie 120 dni:**
- Po wlaczeniu produkcja wychodzi 2-4 razy wieksza niz zakupy, a zapasy w miastach rosna.
- Przyczyna: plan liczyl, ile ludzie *potrzebuja*, a nie ile *moga kupic za swoje pieniadze*. Kiesy ludzi sa za male na pelny koszyk.

**Druga proba (noc, test 01:15-01:53):** plan liczy juz tylko to, za co ktos placi; czego wsi nie stac - robia w domu z wlasnej ziemi. Na calym swiecie wychodzi rowno (100-105%), ale **towar nie dojezdza tam, gdzie sa ludzie**: polowa miast ma zboza na mniej niz 1,5 dnia (ceny pod sufitem), a kilka miast tonie (Vaes Dothrak - 302 dni zapasu). Wsie przy pustych miastach nie maja gdzie kupic - ok. 1/3 ich jedzenia bez pokrycia. Tego nie wgrywam.

**Trzecia proba (w toku):** rozklad miedzy miastami - kazde miasto liczone osobno (jego wsie + to, co realnie da sie przywiezc) i kupcy zbozowi wozacy jedzenie tam, gdzie drogie. Najpierw sprawdzam to na symulatorze 97 miast poza gra (nie zajmuje gry - mozesz grac), dopiero potem test w grze. Wgrywam tylko, gdy kazdy towar miesci sie w 95-105% i nikt nie glodzi.

## 4. Co zostalo na pozniej (kolejka)

- Inwestycje w okreg i specjalizacja wiosek (projekt zatwierdzony; lordowie AI na razie nie inwestuja).
- Przysiolki na pustkowiach, zajazdy przy drogach z plotkami (bandy, kryjowki, skarby).
- +15% predkosci na drogach ROT (dane drog juz zebrane z mapy).
- Uchodzcy z palonych wsi ida do miast (czeka na Twoje slowo).
- Mobilnosc spoleczna i blizny wojny (kod gotowy, wylaczony do czasu udanej KS).

## 5. Dopisane 17.10 po poludniu

- **WGRANIE 75 - daniny sredniowieczne wsi** (Twoje "Tak, wgraj"): wies placi przy wozie - pan jak dotad, dziesiecina 5% (polowa wraca biednym), w wojnie korona 1/10 czesci wsi;
  czynsz staly pierwszy, zaleglosci zamiast zabierania; znikaja sztuczne pobory (danina od kiesy, podatek od oszczednosci, rynek BK 5% kiesy). Przejscie 28 dni gry.
  Test 120 dni: 38/38 bramek, korona 97-100% dochodu, zold zawsze 100%, krolestwa 4x rzadziej w Banku, kiesy wsi stabilne. Wylacznik: MCM Armoury "Dues Medieval".
- **Dalej:** miasta, korona i podatki wojenne (z pozyczkami od mieszczan i Banku, bez darowania dlugow), potem KS runda 4 (Twoje decyzje: dowoz na Polnoc i danina dla Muru, tanszy koszyk biednych).
- **WGRANIE 76 - daniny miast, korony i podatki wojenne** (Twoje "Tak, wgraj"): stale oplaty miast i zamkow, podatek wojenny co kwartal tyle, ile trzeba, pozyczki od mieszczan
  i Banku na braki, dlugi korony u miast nie przepadaja. Testy 160 dni: zold 100%, korona ok. 97% dzisiejszego dochodu, panowie 99%, dlug koron w Banku rosnie 6x wolniej.
  W Twojej grze zmiana idzie etapami: ok. 3 miesiace gry do pelnej. Wylacznik: MCM Armoury "Ds Towns".
