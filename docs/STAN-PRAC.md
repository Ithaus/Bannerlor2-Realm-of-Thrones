# Stan prac - przekazanie dla drugiego konta (2026-10-06 ok. 08:40, lancuch paczek 102b-109 gotowy, dzien-2 w toku)

Czytaj najpierw: `CLAUDE.md` (zwlaszcza sekcja 8, **zasada 0**: kazda zmiana = kontrola regresji, kolizji
i spojnosci calej logiki; oraz pulapka MCM w sekcji 7), potem ten plik, potem gorne wpisy `CHANGELOG.md`.

## Aktualizacja 05.10 (wpisy 98-99 + pelny audyt) - czytaj przed reszta

- **Test nowej kampanii (sesja 07:40, 12 dob)** odczytany; wynik w audycie nizej. Po sesji Jeff kazal wylaczyc
  mod **AIInfluence** (odznaczony w `LauncherData.xml` razem z `ROT_AIInfluence_Compat`; kopia
  `LauncherData.xml.bak-2026-10-05-przed-wylaczeniem-AIInfluence`) - lancuch cen moze sie zmienic.
- **Wpis 98** (mnoznik wydobycia rudy i drewna mnozy zamiast sie dodawac + ksiega faktycznego wydobycia
  "Ruda:" / "Drewno:") i **wpis 99** (Dluga Noc usunieta - Jeff: "wywal dluga noc") sa WGRANE; test 11:19 (12 dob) - dzialaja, wynik w statusach CHANGELOG: ruda i drewno NIE docieraja do miast (68 z 97 miast bez rudy, rosna zamki i tabory), BK dopisuje rude dwa razy, zapas startowy ma mase x10 i blokuje wsie.
  Wartosci mnoznikow bez zmian (3 i 3) - kalibracja po pierwszym uczciwym pomiarze.
- **Rok ma 364 dni** (`WeeksPerSeason` 13), nie 168 - starsze dokumenty licza "na dobe" 2.17x za duzo.
- **Pelny audyt** (ekonomia, wojna, surowce, lordowie; 80 ustalen po weryfikacji):
  `docs/AUDYT-2026-10-05-EKONOMIA-WOJNA-SUROWCE-LORDOWIE.md`. Sekcja 0: kolejnosc napraw (12 pozycji),
  0.2: pytania do Jeffa (skala swiata, zastaw, poczet w pokoju i 9 innych) - czekaja na odpowiedz.
- **Nastepny krok:** nowa gra bez AIInfluence, 5-10 dob; w logu linie "Ruda:" i "Drewno:" (sztuki faktycznie
  dopisane wsiom wobec modelu - rozstrzyga podwojny wpis mineralu BK; zapas "wsie" - czy wiesniacy nadazaja;
  "miast bez towaru"; "zima: nie"). Potem pozycje 2 i 10 audytu: mnoznik do wartosci historycznej
  (uzbrojenie + narzedzia ok. 90-650 ladunkow rudy na dobe, srodek ok. 260; drewna ok. 6.3 ladunku na ladunek rudy),
  ale dopiero z dowozem (wsie zamkowe woza do zamkow) i rekami warsztatow.
- Niezalezny przeglad wpisow 98-99 zrobiony (bez regresji) - wynik w `docs/EKONOMIA-FUNDAMENT-2026-10-05.md`, rozdz. 5.
- **Fundament ekonomii** (jak zarabiaja wsie, zamki, miasta; sredniowiecze; BetterEconomy; liczby dla 52.6 mln; model docelowy K1-K15):
  `docs/EKONOMIA-FUNDAMENT-2026-10-05.md`. Zagadka drewna: mod RealisticBannerlord dosypuje je kazdej osadzie co dobe z niczego.
- **Decyzje Jeffa 05.10:** (1) skala - wojsko zostaje ok. 108 tys., gospodarke dociagamy do niego; (2) plon wsi zamkowych wiezie
  chlop sam na targ miasta; (3) podwojny mineral BK - USUNAC; (4) BetterEconomy - zamknac ujscia 13 kluczami (fundament, rozdz. 4.5);
  (5) zaczynamy od dowozu. Zlecil tez: przyrost naturalny i pobor zdejmujacy ludzi z regionu jako czesc ekonomii - badanie i projekt
  w `docs/DEMOGRAFIA-SILA-ROBOCZA-2026-10-05.md` (jesli pliku nie ma - badanie nie zostalo dokonczone).
- **Wpis 100** (wsie zamkowe woza plon na targ miasta) WGRANY, test 15:22 (28 dob): dziala, zamki przestaly gromadzic; zostaje rozprowadzenie miedzy miastami i zatykanie magazynow wsi zamkowych (17% wobec 8%) - wynik w statusie wpisu 100 w CHANGELOG
  (linie "Dowoz:" i "Dowoz (skutki):").
- **Wpis 101** (woz: udzwig taboru wsi zamkowej x2) WGRANY, NIEPRZETESTOWANY. Nastepny uzgodniony krok: KARAWANY - surowce masowe (ruda, drewno, skory, len, welna) sprzedawane tam, gdzie brakuje, w ilosci wedle braku i kasy miasta, kupowane tyle, ile da sie sprzedac (BK prowadzi wlasna kopie kodu karawan: BKCaravansBehavior; vanilla jest wylaczona; dzis kupno ogranicza stala 1500 w starej monecie, a sprzedaz dzienny budzet miasta + 2 x wartosc sztuki - stad 3155 ladunkow rudy w taborach i 60 miast bez rudy). usuniecie podwojnego mineralu BK; zapas startowy x10 (K3);
  paser skupujacy lup band; potem ksiega przeplywow (K1) i dalsze kroki fundamentu.

## Gotowe paczki czekajace na wgranie (stan 06.10 ok. 08:40 - po przebudowie lancucha)

W GRZE (od 06.10 09:54): wpis 101 (woz x2) + wpis 102 (same logi) + wpis 102b (poprawka ksiegi pieniadza, sam log). DLL md5 `af2351f66adf8ac82b2cb71259145ff6` (poprzedni: `Armoury.dll.bak-2026-10-06-przed-102b`). Nastepne do wgrania: 103. Oba NIEPRZETESTOWANE - Jeff nie
zrobil jeszcze zadnego testu po 05.10 15:22. Pierwszy test: nowa kampania 15-20 dob, potem "sprawdz logi"
(`python tools/sprawdz_logi.py` daje skrot sesji: ktore ogniwo w grze, bledy, alarmy, tabela dzien po dniu).

LANCUCH PACZEK KODU (zlozony od nowa 06.10 z poprawkami po decyzjach Jeffa). Galezie `paczki/*` w repo i na origin; kazde
ogniwo to JEDEN commit na poprzednim, baza 87c8e96 (kod w grze). Kazde buduje sie z kodem 0 i ma niezalezna recenzje.
Gotowe teksty wpisow CHANGELOG: `docs/paczki/`. Klon roboczy sesji: `scratchpad\lancuch` (galezie `n102b..n109`).

| Nr | Galaz | Commit | Tresc | Nowa kampania |
|---|---|---|---|---|
| 102b | `paczki/102b-ksiega-pieniadza` | 0ca94bf | TYLKO LOG: ksiega pieniadza mierzy rozliczenia rodow - zold liczony raz; nowa linia "Pieniadz swiata (rody):" | nie |
| 103 | `paczki/103-karawany` | c387528 | karawany woza surowce masowe wedle brakow miast; kolejnosc kupna i sprzedazy wedle prawdziwego zysku w denarach | zalecana |
| 104 | `paczki/104-zapas-startowy` | 46248e6 | ruda i drewno startu kampanii w ladunkach (flaga `arm_startstock`) | WYMAGANA |
| 105 | `paczki/105-mineral-bk` | 52d5827 | BK bez powtorzonego mineralu; wydobycie rudy, soli, gliny, srebra BEZ ZMIAN (jeden wpis liczony tyle razy, ile bylo powtorzen); `MineOutputMultiplier` zostaje 3 | nie |
| 106 | `paczki/106-paser` | 8213b26 | paser dla wszystkich band: promien 200, sprzedaz temu miastu, ktore zaplaci najwiecej (0.50 -> 0.25 ceny z odlegloscia), skup zywnosci, koniec zlota i zywnosci band z niczego, kasy kryjowek w obiegu, bandy wydaja 10% kiesy dziennie w miescie | nie |
| 107 | `paczki/107-zold-i-skarbiec` | 6ace39a | zold do sakiewek ludzi i kas osad, skarbiec w wojnie zwraca 50% zoldu, tarcza zoldu WLACZONA | nie |
| 108 | `paczki/108-ludzie-jednostka` | fff2a3b | demografia krok 2: 1 czlowiek = 1/k hearth (wyrzutki, tabory, pobor gracza, zadania); pula band liczona od ludnosci | nie |
| 109 | `paczki/109-ludzie-przyrost` | 1d3459f | demografia krok 3 + 9a: przyrost naturalny wsi (-1.3..+0.8% rocznie) zamiast stalej gry; ludnosc miast zamrozona (klucz `arm_people`) | nie |

JAK WGRAC OGNIWO (po "wgraj NNN" od Jeffa, gra zamknieta): na galezi roboczej `git cherry-pick <commit ogniwa>` (ogniwa PO KOLEI;
galaz robocza ma po 87c8e96 same commity dokumentow, wiec wchodzi czysto), `python tools/gen_mcm.py`, build z kodem 0, kopia
`Armoury.dll.bak-<data>-przed-NNN`, wgranie, md5, wpis z `docs/paczki/NNN-*.md` na gore CHANGELOG ze statusem, commit + push.
Mozna wgrac kilka ogniw naraz (grupa testowa) - cherry-pick wszystkich po kolei, jeden build.

UWAGI DO WGRYWANIA I TESTOW:
- 102b to sam log - najlepiej wgrac PRZED pierwszym testem Jeffa (bez niej bilans "Pieniadz swiata" odejmuje zold dwa razy).
- Przed KAZDYM wgraniem DLL z ogniwem 106 lub pozniejszym sprawdzic w `Armoury.json` Jeffa klucz `OutlawFenceRadius`: ma go nie byc
  zadnego klucza tych paczek; gdyby Jeff zapisal ustawienia w MCM, plik dostanie komplet kluczy i domyslne z kodu przestana dzialac.
- 109 wymaga zamknietych ujsc BetterEconomy (skrypt `tools/bee/zamknij-ujscia-bee.ps1`, NIE uruchomiony) - bez tego inwestycje
  BEE dopisuja wsiom 65-135 hearth dziennie z niczego, 20-40 razy wiecej niz caly przyrost naturalny.
- Po 104 karawany przez pierwsze doby nie maja nadwyzek do wiezienia (rozruch, nie blad). Test 103 bez 104 pokazuje mechanike,
  nie skutecznosc.
- Po 109 spalona wies nie odrasta az do kroku 4 (w toku); zostaje regula "+0.5 hearth ponizej 40" (ludzie z niczego) - kroku 4
  nie odkladac.
- Etykiety dnia w logu: "Wyrzutki / Paser / Zold / Korona / Skarbce dzien D" to ten sam tick co "Ruda / Karawany / Dowoz /
  Przeplywy osad / Pieniadz swiata / Ludzie dzien D-1".
- Przeglad kolizji NOWEGO lancucha 102b-109 (06.10, z proba obalenia): w kodzie bez kolizji; zlozenie zgodne ze zrodlami (37 plikow,

SKUTKI, O KTORYCH JEFF WIE (powiedziane 06.10): ruda nie idzie w karawanach pierwsza (tania - 8 d za 100 kg; wedle zysku wygrywaja
welna i plotno), ale jest wozona; drewna karawany czesto nie woza (zysk bliski zera); w kryjowkach nowej kampanii prawie nie ma
zlota do znalezienia; pula band idzie za ludnoscia (Reach 486 -> 1130, Nocna Straz 163 -> 3).

POZA LANCUCHEM:
- **BetterEconomy, 13 kluczy:** `tools/bee/` (zamknij / cofnij / skoki-bee.py / OPIS.md); wywolanie
  `powershell -NoProfile -ExecutionPolicy Bypass -File <skrypt>` (najpierw `-NaSucho`); zbrojownia zamknieta tylko dla AI.
- **Reguly krain i dlugu:** `docs/REGULY-KRAIN-I-DLUGU-2026-10-06.md` (projekt bez kodu; 8 pytan w rozdz. 9 - to sprawy kanonu,
  wiec dla Jeffa: czy jency Innych wstaja jako trupy, bracia Strazy bez zoldu, kto bierze jencow z rabunku).
- **Narzedzie logow:** `tools/sprawdz_logi.py` czyta uklad wpisu 102 i starego lancucha; dopasowanie do 102b-109 w toku.
- **Drobne, do zrobienia:** pozycje ksiegi dla napraw i sakiewek rozbitych partii, pomiar wyplat notablom (dzis w "reszcie").

## Decyzje Jeffa 05.10 - komplet (wiazace przy dalszych krokach)

**Skala i towar**
- Wojsko zostaje ok. 108 tys. (skala 1:1), gospodarke dociagamy do niego: najpierw dowoz, potem dziury w pieniadzu, potem produkcja
  wsi stopniami x1.3 / x1.6 / x1.9 razem z popytem. Nie mnozymy do pelnych wielkosci "na glowe" tam, gdzie gra nie ma odbiorcy.
- Plon wsi zamkowych wiezie chlop sam na targ miasta (wpis 100); woz: udzwig taboru x2 (wpis 101).
- Karawany maja rozwozic surowce masowe (ruda, drewno, skory, len, welna) tam, gdzie brakuje - prawdziwe partie na mapie, nie
  niewidzialny przerzut: sprzedaz wedle braku miasta i jego kasy, kupno tylko z nadwyzki i tyle, ile da sie rozwiezc (wpis 102 w przygotowaniu).
- Podwojny mineral BK - usunac. BetterEconomy - zamknac ujscia 13 kluczami (fundament, rozdz. 4.5); funkcje gracza bez zmian.
- Paser: bandy sprzedaja zrabowany ladunek w miescie - tak.
- Strzaly i belty maja kosztowac drewno, zelazo i prace warsztatow - tak, ale razem z wieksza liczba rak.

**Pieniadz**
- Zold partii do sakiewek ludzi (wydaja w miastach), zold garnizonu do kasy jego osady - takze u gracza.
- Skarbiec krolestwa w czasie wojny zwraca lordom polowe dziennego zoldu, dopoki ma z czego.
- Dlug: lord NIGDY nie traci lenna. Wierzyciel pobiera dochod z jego wsi az do splaty; lord splaca z tego, co ma - w ostatecznosci
  wyprzedaje wszystko.

**Ludzie**
- Jedna ksiega ludzi na region (w domu / w sluzbie / wyrzutki / uchodzcy / polegli). Przyrost naturalny z dobrobytu, glodu
  i niebezpieczenstwa w granicach historycznych. Rekrut odejmuje czlowieka we wsi werbunku, polegly to ubytek trwaly, zwolniony
  i dezerter wracaja. Plon zalezy od liczby ludzi. Ksiega ma byc do sprawdzenia: log "Ludzie:", plik CSV regionow, pozycja w menu osady.
- Prog wolnych rak: 20% mezczyzn (historyczny). Okno zniw: w kazdym roku kalendarzowym ok. 8 tygodni (np. dni 196-252) w latach
  lata i jesieni - pobor trudniejszy (polowa puli); w latach zimy zniw i okna nie ma.
- Spalona wies to SYMBOL wielu wsi: rabunek niszczy tylko czesc okregu, zalezna od liczby napastnikow i czasu; odbudowa latami
  dotyczy tylko tej czesci (wzor: `docs/DEMOGRAFIA-ANEKS-2026-10-05.md`, jesli plik jest).
- Zaraza tylko z oblezen. Najemnicy z karczmy tylko sposrod zwolnionych z wojska.
- Lordowie w pokoju rozpuszczaja wiekszosc pocztu (do ok. 35-40% stanu) i zbieraja go przed wojna; zwolnieni wracaja do wsi.
- Werbunek gracza: rekrut od tieru 2 tylko z tym, co kupil mu notabl (jak AI) - koniec darmowego kompletu.
- Bandyci sa miejscowi (pula wyrzutkow regionu, takze bandy z kryjowek); zabity bandyta = "polegli" regionu pochodzenia.

**Krainy**
- Nocna Straz: rekrut zawsze z krolestw Westeros - najpierw z pul wyrzutkow ("przywdzial czern"), potem z ludnosci proporcjonalnie
  do liczby ludzi regionu; sluzba dozywotnia; polegly = ubytek regionu pochodzenia; dezerter do puli wyrzutkow Polnocy; ludzie Daru
  (20 tys.) licza sie tylko do plonu.
- Wolni Ludzie: walcza wszyscy dorosli (pula ok. polowy doroslych) ORAZ ludnosc w tabeli 150 -> 300 tys.
- Zelazne Wyspy: prog wolnych rak ok. 40% (pracuja niewolni); niewolni to jency z rajdow, przenoszeni z ksiegi napadnietego regionu.
- Nieumarli: zasada "rosna tylko z poleglych za Murem i przy Murze"; Jeff: "tak juz jest, trzeba sprawdzic". SPRAWDZONE 05.10
  (kod ROT i gry + 5 sesji, badanie z niezalezna weryfikacja; pelny wynik: task w781xei9u w katalogu tasks sesji): NIE - dzis rosna
  glownie z niczego, z pokonanych pochodzi najwyzej co czwarty. Zrodla (5 sesji razem): narodziny bandy = 53 z szablonu + 100
  dosypanych przez ROT + wodz = 154 (kazda sesja startuje od 4 x 154 = 616; narodzin min. 38, ok. 5850 ludzi) - z niczego; ROT +2
  trupy dziennie na bande (do 488) - z niczego; ochotnicy "z mapy" ok. 10 dziennie (700-1000, SZACUNEK) - z niczego; nekromancja
  ROT - najwyzej 2358 = 1115 zabitych + 1243 rannych jencow, ktorych ROT kasuje - z pokonanych; odbici jency pokonanej partii
  wchodza wprost do bandy. Zabity tez nie znika: Wedrowiec przed inwazja nie ginie i wraca z nowa banda 154; dezerterzy (ok. 1000)
  i polowa rozbitych (538-700) trafiaja do puli wyrzutkow, skad biora ich zwykle bandy. Dezercje wightow wywoluje nasz Zew
  (NightKingCall: limit partii liczony przed transferem i bez mnoznika BannerKings x1.5-2). Inni nie zdobyli zadnej osady w zadnej
  sesji (rajd AI nieosiagalny dla rodu bez krolestwa). DO ZMIANY (osobny watek, po ksiedze ludzi): wylaczyc +100 i +2; sklad nowej
  bandy z puli poleglych (pusta = sam wodz); zablokowac Innym ochotnikow z mapy i odbitych jencow; nekromancje ksiegowac i
  ograniczyc do Muru (decyzja Jeffa: czy jency Innych licza sie jako polegli); wightow nie wpuszczac do puli wyrzutkow, wylaczyc im
  dezercje i niewole; naprawic limit w Zewie.
## Decyzje Jeffa 06.10 (wiazace; dopisane po raporcie nocnym)

1. **Handel wedle zysku w danej chwili.** "To ma byc to, co sie oplaca w danej chwili, a nie sztucznie" - zadnych stalych list
   ani sztucznych priorytetow. Karawany kupuja i sprzedaja w kolejnosci najwiekszego zysku na kilogram liczonego z biezacych cen
   (poprawka `l103b-karawany-zysk` do ogniwa 103).
2. **O parametrach ekonomii decyduje projekt, nie Jeff.** "Ty tworzysz ekonomie i jej sens, to co mnie pytasz" - NIE pytac
   Jeffa o poziomy produkcji, ceny, mnozniki, promienie. Wybierac to, co ma sens ekonomiczny i historyczny, krotko uzasadnic i
   robic. Pytac tylko o rzeczy, ktore zmieniaja to, w co Jeff gra (fabula, kanon, wygoda gracza).
3. **Poprawka bledu nie zmienia po cichu poziomu produkcji.** Ogniwo 105 po przerobce (`l105b-mineral-bez-zmian`): powtorzony
   mineral BK znika z listy, ale wynik modelu jest liczony tyle razy, ile bylo powtorzen - ruda, sol, glina i srebro bez zmian;
   `MineOutputMultiplier` zostaje 3 (uwaga o pliku `Armoury.json` przed wgraniem 105 jest NIEAKTUALNA, w logu ma byc "x3.0").
   Wlasciwe poziomy produkcji wszystkich towarow: jeden krok skali (K13) liczony od zuzycia i danych historycznych.
4. **Paser dla 100% band.** "Nie moze byc tak, ze sa bandyci, ktorzy nie moga opchnac towaru"; "po co czesc band siedzi na
   skarbach jak smok w jaskini". Pomiar z mapy: promien 20 = 4% kryjowek, 50 = 39%, 100 = 92%, 150 = 99%, 200 = 100% (najdalsza
   kryjowka 170 od miasta). Przerobka ogniwa 106 (`l106b-paser-wszyscy`, w toku): promien 200 z cena malejaca z odlegloscia,
   skup zrabowanej zywnosci (po zamknieciu dosypki z niczego), kasy kryjowek z odplywem (nie moga tylko rosnac).
5. **Tarcza zoldu wlaczona** (`TownWageShield` = true, `l107b-zold-tarcza`). Skutek uboczny do naprawy razem z regulatorem kas
   (K6): zaloga we wlasnym miescie prawie nic pana nie kosztuje (zold wraca w rentach); kas ZAMKOW tarcza nie obejmuje - zold
   zalog zamkow dalej w wiekszosci kasuje regulator. Nastepny krok: K6 / tarcza takze dla zamkow.
6. **Tempo: pelna para.** Limit tygodniowy tego konta ma starczyc do konca dnia 06.10 - sa inne konta. Zostaje zasada pauzy
   przy 90% limitu 5-godzinnego. Dlatego ten dokument musi byc aktualny po kazdym etapie (przekazanie na inne konto).
7. **Mowic prostym jezykiem.** Jeff nie zna nazw klas ani naszych skrotow ("tarcza zoldu", "regulator") - kazde pytanie i
   podsumowanie tlumaczyc slowami gracza: co sie dzieje w grze i co on zobaczy.

### Prace w toku 06.10 (katalogi `scratchpad\dzien-1`, `scratchpad\dzien-2`; klon `scratchpad\lancuch`)

- ZAKONCZONE 06.10 rano: workflow `dzien-1` i `dzien-1b` (ksiega 102b, karawany wedle zysku, mineral bez zmiany wydobycia,
  paser dla wszystkich band, demografia krok 2 i 3, narzedzie logow, kolizje 106-107) - wszystko w lancuchu `paczki/102b..109`.
- W TOKU: workflow `dzien-2` (run `wf_2556af79-033`, kazda paczka autor + recenzent, baza `n109-ludzie-przyrost`):
  K5 kasa zamku (`l110-k5-kasa-zamku`) -> K6 kasy miast bez kasowania nadwyzek + pulap zaworu (`l111-k6-kasy-miast`);
  K7 utarg wsi bez znikania (`l112-k7-utarg-wsi`); demografia krok 4 + 6a spustoszenie (`l113-ludzie-spustoszenie`);
  dopasowanie `sprawdz_logi.py` (`dzien-2\logi`); przeglad kolizji nowego lancucha z propozycja grup testowych.
- PO ZAKONCZENIU: dolozyc nowe paczki do lancucha jako 110-113 (skrypt `dzien-1\nowy\ogniwo.ps1`: cherry-pick, MCM, build,
  commit), wypchnac `paczki/*`, zapisac opisy w `docs/paczki/`, zaktualizowac ten dokument.
- DECYZJA JEFFA 06.10 ("3 razy"): testy GRUPAMI, nie po jednym ogniwie. Grupa 1 TOWARY = 103 + 104 + 105 (nowa kampania
  wymagana przez 104); grupa 2 PIENIADZ = 106 + 107; grupa 3 LUDZIE = skrypt BetterEconomy + 108 + 109. Kazda paczka ma
  wlacznik w MCM - gdy cos nie dziala, wylaczac po kolei. Wgranie grupy: cherry-pick wszystkich jej ogniw po kolei, jeden
  build, jedna kopia .bak, osobny wpis CHANGELOG na kazde ogniwo. Grupe wgrywac dopiero na slowo Jeffa ("wgraj grupe N") i po
  czystym przegladzie kolizji nowego lancucha (dzien-2). Paczki 110-113 (K5, K6, K7, krok 4) utworza grupy 4-5.
- Do rozstrzygniecia z Jeffem jednym slowem: pierwszy test to samo "101 + 102 + 102b" (wtedy 4 testy) czy od razu z grupa 1
  (3 testy; ksiegi z tego testu sa wtedy stanem "przed" dla grup 2 i 3 - rekomendacja).

## Gdzie jestesmy

- Ostatni wpis: **100** (98-100: aktualizacja wyzej). Wszystkie wpisy 75-97 sa **WGRANE** do gry (DLL Armoury), **ale NIE PRZETESTOWANE**
  w grze - Jeff jeszcze nie gral po nich. Nastepny krok: **test w NOWEJ grze** (dorobek stuleci - wpis 79 -
  dziala tylko w nowej kampanii).
- Plik MCM Jeffa: `Documents\Mount and Blade II Bannerlord\Configs\ModSettings\Global\Armoury\Armoury.json`
  - zapisane tam klucze NADPISUJA domyslne z `Settings.cs`. We wpisie 95 recznie poprawione:
  ConditionPenaltyExponent 2.0, TroopWearPercent 4, WearWeaponPerHit 0.25, PlayerLootSharePercent 33
  (kopia `Armoury.json.bak-2026-10-05-przed-95`).
- Kopie DLL przed kazdym wpisem: `Modules\Armoury\bin\Win64_Shipping_Client\Armoury.dll.bak-2026-10-05-przed-NN`.

## Co zrobiono 05.10 (skrot; szczegoly w CHANGELOG)

| Wpisy | Temat |
|---|---|
| 75 | Sprawca 7 lukow ROT: `ROT.dll ROTRBMCompatibility` pisze Value wprost do pola - postfiks przywraca ceny |
| 76, 78 | Awans ochotnika po kupnie czesci kluczowych (zbroja, glowna bron, kon+rzad, kolczan) |
| 77 | Ksiega krolestw (log "Skarbce:") |
| 79 | Dorobek stuleci: zbrojownie startowe + 14 dni zapasu na targach (nowa gra) |
| 80, 81 | Budowy: moc tylko w oplacanych osadach; petla drozenia zamowien |
| 82 | Lup wedlug tego, kto powalil (Ty 100%, zolnierze 1/3) |
| 83, 89 | Jeden zegar kuzni (godziny z ekranu BK, po kolei, tylko w osadzie) + naprawa kolejki |
| 84 | Sakiewka ludzi: nadwyzki na targ, naprawy/braki z ich pieniedzy |
| 85, 89, 95 | Zuzycie i naprawy sprzetu AI |
| 86-88 | Audyt 1-18 (budowy, renty, stan kampanii, ceny, polityki krolestw, kopalnie BK) |
| 89-90 | Po pelnym audycie 1-88: garnizony (DTE kasowal zbrojownie), konie AI, petla tier nizej, clo z niczego, wykuta bron w pensach |
| 91, 93 | Wspolne rece kowali (16 h dziennie) |
| 92 | Komplet rekruta AI = to, co notabl kupil |
| 94 | Ksiega rudy (log "Ruda:") |
| 95-97 | Zuzycie sprzetu: stan lupu Spoils, lagodniejsza kara, zuzycie wojska z przebiegu walki, cena lupu = stan |

## Co sprawdzic w logach po tescie

`Modules\Armoury\Armoury-<data>.log` i `Modules\Armoury\Logs\<sesja>\*.log`:
- start: `ROT-RBM luki ... przechwycone`, `ColdStart: zbrojownie ...`, `ColdStart: zapas kupiecki ...`,
  `ColdStart: ochotnicy tieru 2+ ...`, `LootPrices: ... 4/4`, `ForgeClock: ... CZYNNY`
- dziennie: `Ruda: dzien ...` (do decyzji o rudzie), `Skarbce:`, `Sakiewka ludzi:`, `Zuzycie AI:`,
  `Komplet rekruta:`, `ZakupyAI: ... w tym garnizony` (powinno spasc po kilku dniach), `Ochotnicy:` (cofniec mniej)
- po bitwie gracza: `Zuzycie wojska: N sztuk ... (z trafien: ...)`, `BattlefieldLaw: powaleni przez partie gracza ...`
- kuznia: sztuki wychodza po kolei co swoje godziny (`Off the anvil`), wyjazd = kolejka stoi

## Otwarte sprawy (czekaja)

1. **Ruda (wariant B wybrany przez Jeffa)**: po kilku dniach gry odczytac `Ruda:` z logu, porownac z rachunkiem
   historycznym (ruda na bron ok. 40-90 t/dzien wobec ok. 3.8 t w grze - szacunki), zaproponowac kroki:
   ruda x3 razem z rekami x3 (`MineOutputMultiplier` + `WorkshopProsperityPerHand`), potem dalej.
2. **Werbunek GRACZA**: DTE daje pelny komplet z niczego (AI juz nie - wpis 92). Do decyzji Jeffa.
3. Drobne z audytow (bez decyzji, do zrobienia po tescie):
   - przetop zwraca za malo metalu (SmeltTab/Patches - limit od ArmourUnits vs koszt kucia z wagi);
   - AmmoRecovery liczy wynik bitwy z perspektywy gracza takze dla AI;
   - wskaznik surowcow (ArmsPricing.MaterialIndex) prawie nie rusza cen (wagi w starej skali);
   - martwe ustawienia MCM (HideoutGoldBase/PerBand, AiNightsAwakeInChase, CharcoalValue..., ArmsPriceBand, WorkshopWorkersArtisans);
   - place jencow z niczego (RealisticCaptivity Work.cs);
   - wraki z wrogow powalonych przez zolnierzy ida w 100% do gracza (omijaja 1/3);
   - WesterosClimate: jeden wyjatek gasi klimat na cala sesje (`_initBroken`);
   - HideoutPurge: stan bez zapisu/Reset;
   - konie wojska gracza moga wracac do DTE nawet po smierci (podejrzenie - sprawdzic w logu bitwy);
   - garnizony nie zuzywaja sprzetu; monopole w Levies 5% od stanu (ProfitMade), nie od dziennego zysku;
   - LevyGold: VolunteerFromMap - zloto lorda znika; przycinanie Hero.Gold przy koszcie > kiesa.
4. Starsze otwarte: `docs/ERRORS.md`, `docs/HISTORY.md`.

## Jak pracowac z Jeffem (przypomnienie)

- Po polsku; napisy w grze po angielsku; komentarze bez polskich znakow.
- Najpierw log, potem kod. Nie mow "dziala", dopoki nie widac w logu albo Jeff nie potwierdzi.
- Jedna zmiana = wpis w CHANGELOG (Problem / Przyczyna / Zmiana / Ryzyko - wynik kontroli wg zasady 0 / Status) + commit + push.
- DLL wgrywac tylko przy zamknietej grze (`tasklist | grep -i bannerlord`), z kopia `.bak-<data>-przed-NN`.
- Galaz robocza: `claude/bannerlord-rot-setup-o75kvo`; `main` przesuniety do niej 05.10.
