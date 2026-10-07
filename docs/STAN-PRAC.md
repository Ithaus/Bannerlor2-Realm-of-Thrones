# Stan prac - przekazanie dla drugiego konta (2026-10-07 02:47: W GRZE 101-107 + GRUPA TOWARY 2 = 115-120, NIEPRZETESTOWANE - czeka na test Jeffa: nowa kampania 20 dob, potem --grupa 2b)

Czytaj najpierw: `CLAUDE.md` (zwlaszcza sekcja 8, **zasada 0**: kazda zmiana = kontrola regresji, kolizji
i spojnosci calej logiki; oraz pulapka MCM w sekcji 7), potem ten plik, potem gorne wpisy `CHANGELOG.md`.

## GRUPA "TOWARY 3" (121-125) - W PRZYGOTOWANIU (07.10 rano; NIEWGRANA)

Lancuch na 120 (w grze): `paczki/121-kategorie-mieszane` (ed9f07d; sztabka zlota ok. 5.7 tys. zamiast 2 tys.) -> `paczki/122-woz-caly-magazyn`
(0fe4901; woz zabiera caly magazyn, udzwig na miare ladunku - zatkane wsie 15.6% -> szac. 2-6%) -> `paczki/123-cena-sprawiedliwa-warsztatu` (d0408ab;
za wyrob najwyzej koszt cyklu + 25%, nadwyzka w kasie miasta - piekarnia ok. 10-25 tys., zlotnik kilka tys. zamiast 3.56 mln) ->
`paczki/124-kapital-startowy-warsztatow` (4608b8c; seed po przeliczeniu cen - wczesniej nigdy nie zadzialal) -> 125 koniec dosypki RBL (w toku).
Kazde: autor + niezalezny recenzent; zlozenie: build 0 kazdego ogniwa, proby recenzentow na DLL grupy, kolejka p108..p114 naklada sie bez konfliktu.
Opisy `docs/paczki/121..124-*.md`. Test: NOWA kampania (123 i 124 licza start). AUTOTEST (zgoda Jeffa 07.10): tryb w CrashScribe + `tools/autotest.ps1`
(galaz at1-autotest w klonie, niewypchnieta); 1. przebieg 05:06 - menu, kreator ROT, kampania, czekanie w miescie OK (10.5 s/dobe), STANAL w 4. dobie,
gra zawiesila sie przy wyjsciu; skrypt przywrocil DLL Jeffa (md5 OK), zapisy Jeffa nietkniete. PRZYCZYNA: okno gry stracilo fokus, a przy
StopGameOnFocusLost=True (ustawienie Jeffa, nietkniete) gra otwiera menu Esc i wstrzymuje mape; wyjscie - wywrotka silnika przy zamykaniu (znana z gier Jeffa).
AT1b (f1d3705): autotest zamyka menu Esc, diagnostyka postoju, skrypt czeka 90 s na wyjscie. Kod: galaz `narzedzia/autotest` (CrashScribe + tools/autotest.ps1;
CrashScribe z autotestem NIE jest wgrany na stale - skrypt wgrywa go tylko na czas testu). Uruchomienie: `powershell -NoProfile -ExecutionPolicy Bypass -File
<worktree>\tools\autotest.ps1 -CrashScribeDll <CrashScribe z autotestem> -ArmouryDll <Armoury probny> -Days 40`.
2. PRZEBIEG 06:06 (Armoury 121-124, 40 dob): OK - 40/40 dob w 9.6 min (12 s/dobe), zapis autotest-2026-10-07-0616.sav, gra wyszla sama, DLL i zapisy
Jeffa przywrocone (md5). Log `Armoury-2026-10-07_06-06-45.log`. WYNIK 121-124 wobec testu Jeffa (115-120): cena warsztatow mediana 13.3 tys. (bylo 145 tys.),
max 192 tys. (12.27 mln), piekarnia Lannisport 12.7 tys. (373 tys.); ruda bez towaru doba 19: 45 (51), doba 40: 38; drewno 47 -> 1; ZATKANE MAGAZYNY
nadal rosna: doba 16: 29, doba 40: 60 z 571 (10.5%; u Jeffa doba 19: 89) - 122 polowa, druga polowa = dosypka RBL -> paczka 125 (w toku); KUPNO STOI:
skora, skory, welna, plotno (przesyt - K13). ERROR 0, potkniecia 0.

## PIERWSZY TEST GRUPY TOWARY 2 (115-120) - 07.10 03:18 (nowa kampania, 19 dob; log `Armoury-2026-10-07_03-18-52.log`)

Odczyt `python tools/sprawdz_logi.py --grupa 2b`: OK 70, UWAGA 14; ERROR 0, Exception 0, potkniecia 0; wszystkie ogniwa wpiete.
- RUDA: miast bez rudy 76 -> 51 (poprzedni test 74 -> 69; oczekiwane 25-30) - lepiej, ale ALARM (> 50). Wozy 116/dobe (oczekiwane 150-250),
  z ruda mediana 7/dobe, do miast bez rudy 63 z 112 kursow; karawany kupuja rudy mediana 4/dobe.
- DREWNO: miast bez drewna 50 -> 11 (dobrze).
- ZATKANE MAGAZYNY WSI: zamkowe 16.5%, miejskie 14.8% (poprzednio 4.6% / 7.4%; opis 119 przewidywal +1-3 punkty) - do wyjasnienia (pelny magazyn
  wstrzymuje cala produkcje wsi).
- WARSZTATY: piekarnie pracuja (75 z 76), ale chleb x3.3-4.5 wartosci przez 19 dob -> piekarnia 363 d/dobe, w Lannisporcie 373 tys.; ZLOTNIK
  w Lannisporcie 3257 d/dobe -> cena 3 558 909 d (zrzut Jeffa z gry - ta sama liczba), tkalnia aksamitu 5107 d/dobe, tkalnia welny 1148 d;
  cena kupna warsztatow mediana 145 tys., max 12.27 mln. Przyczyna wstepnie: receptury luksusowe po przeliczeniu cen (ruda srebra 20 d ->
  bizuteria 2400 d) - sprawa K13 (receptury i ceny), nie wzoru ceny warsztatu.
- WELNA zalega ("KUPNO STOI" 8 dob, na polkach 566 -> 2600). Nadplata wozow 27.6% (oddana kasom osad).
- ANALIZA (07.10 04:06, `docs/ANALIZA-TESTU-TOWARY2-2026-10-07.md`): (1) PILNE, regresja 119: wozy jada daleko, gra laduje 59% magazynu -> 89 z 571 wsi stoi z pelnym magazynem (+5 dziennie) -> paczka 122 "woz zabiera caly magazyn"; (2) PILNE: zysk warsztatow to renta z niedoboru + receptury (ruda srebra 20 d -> 2 klejnoty po 2400), placona "zakupami" mieszczan z niczego -> paczka 123 "cena sprawiedliwa warsztatu" (koszt cyklu + 25%, nadwyzka w kasie miasta); (3) drobne 116: kapital startowy liczony przed przeliczeniem cen -> paczka 124; ruda 51 = zuzycie kuzni x2.3 zjada dostawy (poprawi 122: +40% wywozu); welna i wsad warsztatow wedle wartosci, wytop przy kopalni -> krok K13 (projekt). 07.10 04:10: 122 i 123 w toku (autor + recenzent), potem 124 i zlozenie 121-124 (121 = sztabka zlota, gotowa: `paczki/121-kategorie-mieszane`). Rownolegle: tryb autotestu (zgoda Jeffa 07.10).

## NOC 06/07.10 - GRUPA "TOWARY 2" (115-120) GOTOWA DO WGRANIA NA SLOWO JEFFA; kolejka 108-114 przeniesiona na 119 (na 120 naklada sie czysto)

WGRANE 2026-10-07 02:47 na slowo Jeffa ("wgraj"): W GRZE wpisy 101-107 + 115-120 (DLL md5 25b87631d468cb3e01a71a3578f9fc73; poprzedni
`Armoury.dll.bak-2026-10-07-przed-115` = 0eeb0a10...). Galaz robocza = kod w grze (fast-forward na commity 115-120, 8383235); wpisy
115-120 na gorze CHANGELOG. NASTEPNY KROK: test Jeffa (swiezy start gry, NOWA kampania, 20 dob, zapis) -> "sprawdz logi" =
`python tools/sprawdz_logi.py --grupa 2b` (+ --surowe przy pierwszym logu). Kolejka dalej: `paczki-na-120/108..114`.
Noc 06/07.10: nowe konto (sesja fa2fd7a6), jeden watek naraz.
Galaz robocza = kod 107 (sprawdzone 07.10 ok. 21:40 czasu komputera: diff kodu z `paczki/107-zold-i-skarbiec` pusty).

NOWY LANCUCH (kazde ogniwo: autor + niezalezny recenzent, cena: dwoch; jeden commit; build kod 0; opisy `docs/paczki/115..119-*.md`;
przeglad zlozenia `docs/paczki/PRZEGLAD-ZLOZENIA-2026-10-07.txt`):

| Nr | Galaz | Commit | Tresc (slowami gracza) | MCM |
|---|---|---|---|---|
| 115 | `paczki/115-cena-od-niedoboru` | c49cdda | surowiec drogi tam, gdzie go brakuje rzemieslnikom (ruda w miescie bez rudy: pierwszy ladunek 37 d zamiast 10, z kuznia 74 d; drewno 39 d zamiast 2); welna tanieje poza 23 miastami z tkalnia; sol, piwo, narzedzia przy pustej polce znow do x10 | 614 |
| 116 | `paczki/116-warsztaty-w-nowej-monecie` | 9b7a4e8 | warsztat kosztuje 3 lata swojego zarobku (+ kasa warsztatu); 100 d dziennie przestaje znikac - czynsz i place ida do kasy miasta; zaczynaja pracowac piekarnie, winiarnie, olejarnie, garbarnie, garncarnie | 623 |
| 117 | `paczki/117-karawany-ruda-dociera` | 0ffc41c | karawany kupuja surowce przed wyborem trasy i laduja je do pelnego udzwigu (karawany gracza 80%) | 625 |
| 118 | `paczki/118-towary-w-nowej-monecie` | 38054d7 | chleb, ciasta, miod i owoce tansze tam, gdzie je dowoza (bochen przy 10 dziennie 61 -> 17-21 d); miod pitny, futro, zloto, klejnoty, atrament, barwnik duzo tansze (lup ze zlota i klejnotow 10-15 x tanszy) | 626 |
| 120 | `paczki/120-poprawki-po-audycie` | e0dd773 | drobne poprawki po audycie: wlasny warsztat placi naprawde z kiesy takze w czasie kucia; cena warsztatu w pierwszym roku liczona z prawdziwych dob; notabl w rozmowie nie obiecuje wiecej, niz ma w sakiewce; liczniki bledow w liniach dnia | 631 |
| 119 | `paczki/119-wozy-do-najlepszego-miasta` | 0c7aa2d | woz kazdej wsi jedzie do miasta w zasiegu 250 (ok. 4 doby), ktore najlepiej zaplaci; ladunek wyceniany sztuka po sztuce (koniec ceny pierwszej sztuki za caly woz); woz x2 dla wszystkich wsi | 631 |

DLL GRUPY = build `paczki/119`: md5 faade7bf0dcc00de87d520a603234d15, 631 ustawien (kopia: `SCRATCH\dzien-5\zlozenie\Armoury-grupa-towary2.dll`,
SCRATCH = katalog roboczy sesji 3cf3e0ac); proby wszystkich recenzentow przechodza na nim. `Armoury.json` Jeffa (07.10): zadnego z 19 nowych kluczy
ani `MarketMaxDistance` (zmiana domyslnej 150 -> 250 zadziala). UWAGA: zapis ustawien w MCM PRZED wgraniem wpisalby MarketMaxDistance = 150.

JAK WGRAC (dopiero po "wgraj" od Jeffa; gra zamknieta - proces `Bannerlord.BLSE.LauncherEx`): na galezi roboczej
`git cherry-pick c49cdda 9b7a4e8 0ffc41c 38054d7 0c7aa2d e0dd773` (115-120), `python tools/gen_mcm.py` (ma nic nie zmienic, 631), build z kodem 0;
KONTROLA = `git diff paczki/120-poprawki-po-audycie HEAD -- Armoury` PUSTY (NIE md5 - numer commitu jest wpisany w DLL, wiec md5 builda
z galezi roboczej zawsze wyjdzie inny niz faade7bf; audyt 07.10: 9a5040b8 przy tym samym kodzie); w CHANGELOG zapisac md5, ktory wyjdzie;
NIE wgrywac kopii z dysku C (scratchpad); kopia `Armoury.dll.bak-2026-10-07-przed-115`; wgranie; md5 w grze; wpisy z `docs/paczki/115..119` na gore CHANGELOG ze statusem
WGRANE (bez komentarza HTML na poczatku pliku); commit + push.

TEST (decyzja Jeffa: testy grupami): swiezy start gry, NOWA kampania (115, 116 i 118 licza start kampanii), 20 dob, zapis; "sprawdz logi".
Glowna liczba: "Ruda: miast bez towaru" - bylo 74 -> 69; oczekiwane ok. 25-30 po 20 dobach (ponizej 20 po ok. 40; 16 miast lezy dalej niz 250
od kazdej kopalni - tam tylko karawany). Dalej: "Ceny surowcow:" (ruda w miescie bez rudy indeks ok. 10, pierwszy ladunek ok. 37 d; chleb),
"Warsztaty towarowe:" (piekarnie pracuja od 1. doby; cena piekarni w Lannisporcie - zalezy od doplywu chleba i ciast, przy niedoborze moze
przekroczyc 100 tys., bo tyle naprawde zarabia), "Dowoz (wozy):" ("niezgodne" 0, nadplata kilka-kilkanascie %, ms na dobe), "Karawany (przyczyny)" /
"(kierunek)". Odczyt: `python tools/sprawdz_logi.py --grupa 2b` (nowa grupa TOWARY 2: glowna liczba, kontrole kazdego ogniwa, tabela dzien po dniu; progi to
szacunki z opisow paczek - pierwszy log obejrzec tez z `--surowe`). Na starym logu --grupa 1 / 2 daja to samo co przed zmiana (bajt w bajt).

AUDYT GRUPY PRZED TESTEM (07.10 noc, zasada 0: trzech niezaleznych audytorow 115-119 RAZEM - ekonomia i przeplywy, bezpieczenstwo
w grze, gracz i decyzje Jeffa; raporty `docs/audyt-2026-10-07/1..3-*.md`): NIC BLOKUJACEGO. Zamknieta ekonomia pieciu paczek razem domknieta
(uczciwa cena wozu = cena 115 = to, co placi kasa; ksiega nic nie liczy dwa razy; kasy miast trzyma regulator gry - nikt ich nie wyczerpie),
latki bez kolizji z cudzymi modami, zapis / wczytanie bezpieczne, koszt grupy ok. 50-80 ms na dobe gry, napisy i linie logu = opisy co do
slowa. WAZNE: (a) instrukcja wgrania kazala porownac md5 - poprawione wyzej; (b) zysk wsi z wozow przejsciowy - OTWARTE (1);
(c) SZTABKA ZLOTA po 118 sprzeda sie najwyzej za ok. 42% wartosci (ok. 2 tys. przy wartosci 4750, druga 1.3 tys.), a ruda zlota za 4.6 x
wartosci - w kategorii "zloto" ruda i sztabka przeliczone w przeciwne strony (x8 tansza, x4.75 drozsza), jeden wspolny przelicznik;
poprawka po tescie osobna paczka (przelicznik na przedmiot albo wazony); opis 118 sprostowany; (d) opis dla Jeffa - nizej.
Drobne (pamiec roku ceny warsztatu, place warsztatu gracza w menu kuzni, liczniki bledow, bramka cen, suwaki MCM, sprzedaz warsztatu
notablowi bez kupca z pieniedzmi) -> OGNIWO 120 "poprawki po audycie" ZROBIONE 07.10 01:00: autor + niezalezny weryfikator ("gotowe"), wszystkie
proby recenzentow jak na 119 (roznice zamierzone i opisane), wlasne proby 20/20 (na 119: 3/20); DLL builda n120 md5 999ae178 (build z galezi
roboczej da inny md5 - patrz KONTROLA). Opis `docs/paczki/120-poprawki-po-audycie.md`. Zostaje: pamiec roku obniza zawyzona cene warsztatu po
wczesnym skoku zysku tylko czesciowo (-10% po 60 dobach) - dalej to decyzja projektu; zakresy suwakow tylko opisane (generator bierze zakres z
wartosci domyslnej).

CO JEFF ZOBACZY W GRZE PO 115-119 (slowami gracza; do powiedzenia przed "wgraj"):
1. Targ: ruda w miescie bez rudy - pierwszy ladunek 37 d (z kuznia 74 d), drewno 39 d, sol i piwo przy pustym straganie do 10 x wartosci;
   tam, gdzie tego pelno - grosze. Welna tania wszedzie poza 23 miastami z tkalnia.
2. Chleb, ciasta, miod tansze tam, gdzie je dowoza (bochen ok. 20 d zamiast 61 przy 10 bochnach dziennie); miod pitny prawie nic nie wart,
   miodosytnie stana.
3. Lup: sztabka zlota ok. 2 tys. zamiast 31 tys. (druga 1.3 tys.), sakiewka klejnotow ok. 140 d zamiast 2.2 tys., futro, atrament, barwnik
   kilka razy tansze.
4. Warsztat kosztuje 3 lata swojego zarobku plus pieniadze w jego kasie - notabl mowi w rozmowie, skad ta cena; tam, gdzie brakuje chleba i
   ciast, piekarnia moze kosztowac ponad 100 tys. (tyle naprawde zarabia). Wlasny warsztat placi miastu 4 d dziennie i czeladnikom za kazda
   partie; sprzedajac go notablowi dostaniesz najwyzej tyle, ile on ma w sakiewce.
5. Wozy: kazda wies (takze Twoja) wiezie towar do miasta do ok. 4 dob drogi, ktore zaplaci najwiecej - takze do miast innego (niewrogiego)
   krolestwa; woz wiezie 2 x wiecej, wiec napad na tabor daje 2 x wiecej lupu. Pieniadze z Twoich wsi przyjda pozniej, ale zwykle wiecej.
6. Karawany AI pakuja sie surowcami do pelna; Twoje karawany jak dotad (80%).

KOLEJKA 108-114 PRZENIESIONA NA 120 (07.10 01:15): galezie `paczki-na-120/108-...` .. `paczki-na-120/114-porzadki` (e548266 .. a14efe8; bez konfliktu,
build kod 0 kazdego, 633 -> 669 ustawien; drzewo 114 = kontrolne scalenie). Ponizej opis wersji na 119 (ta sama tresc) - AKTUALNE SA `paczki-na-120/*`.
WCZESNIEJ PRZENIESIONA NA 119: galezie `paczki-na-119/108-...` .. `paczki-na-119/114-porzadki` (1ffe021 .. 817931e; build kod 0 kazdego,
669 ustawien na 114; konflikty tylko tekstowe: lista Reset w konstruktorze ArmouryBehavior, Settings.cs przy K7; K5 i wozy w MarketRoad.cs
rozlaczne - K5 dziala tylko dla wsi zamkowej bez zadnego miasta w zasiegu). STARE `paczki/108..114` (na n107) ZOSTALY - ich nadpisanie
(force push) zablokowalo zabezpieczenie; do decyzji Jeffa (albo nadpisac, albo dalej uzywac `paczki-na-119/*`). Tresc ogniw ta sama, opisy
`docs/paczki/108..114` dalej wazne (inna baza). Grupy dalej: LUDZIE (BetterEconomy + 108 + 109), KASY (110-112 + 114), SPUSTOSZENIE (113).

OTWARTE PO NOCY (osobne kroki, nic z tego nie zrobione): (1) welna tanieje poza miastami z tkalnia (115); wozy (119) rownowaza to
TYLKO PRZEJSCIOWO (audyt 07.10, symulacja 200 dob: utarg wsi owczarskich wobec dzis x3.0 w dobach 1-20, x1.9 w 21-60, x1.15 w 61-200;
polki rudy, welny i lnu puchna liniowo, bo wsie produkuja kilka razy wiecej, niz ktokolwiek zuzywa) - PILNY po tescie krok skali K13
(produkcja wsi wobec prawdziwego zuzycia, w tym MineOutputMultiplier 3); sukno welniane jako rzemioslo miast - decyzja projektu przy K13; (2) dosypka drewna
RealisticBannerlord (towar z niczego) jest teraz warta 3-5x wiecej - zamknac (fundament B1); (3) ksiazki BK kosztuja 7-10 d (BKROTPatch /100 +
nasz BookTranspiler); (4) dlawik BKROTPatch: decyzja karawany raz na 24 h = 2-3 kursy na 20 dob (cudzy kod); (5) wozy nie widza karawan (mozliwy
podwojny dowoz); (6) proby K5 / K6 z dzien-2: atrapa wsi bez typu - odswiezyc przed grupa KASY; (7) kosmetyka: linia startowa 117 mowi
"poprawka 115"; (8) bez zmian: "reszta" bilansu pieniadza ok. -320 tys./dobe, pytanie o Spoils of War, 8 pytan kanonu.

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

W GRZE (od 06.10 13:12): wpisy 101, 102, 102b + GRUPA 1 TOWARY (103 karawany, 104 zapas startowy, 105 mineral BK) + GRUPA 2 PIENIADZ (106 paser, 107 zold i skarbiec). DLL md5 `0eeb0a105dc5ae4221809e222e327afe` (poprzedni: `Armoury.dll.bak-2026-10-06-przed-106` = grupa 1). WSZYSTKO NIEPRZETESTOWANE - Jeff wybral jeden test dla grup 1 + 2: swiezy start gry, NOWA kampania, 20-30 dob, zapis na koncu, "sprawdz logi" (`python tools/sprawdz_logi.py --grupa 1` i `--grupa 2`). Nastepne do wgrania: grupa 3 (BetterEconomy + 108 + 109, nowa kampania).
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
| 110 | `paczki/110-k5-kasa-zamku` | 2a405c9 | K5: kasa zamku jako prawdziwy pieniadz - bez dosypki i kasowania przez gre; nadwyzke ponad zapas kupcow pan zamku odbiera po 7% dziennie; na starcie nowej kampanii jednorazowo znika ok. 5 mln "daru" (klucz `arm_castlepurse`) | zalecana |
| 111 | `paczki/111-k6-kasy-miast` | 61a3453 | K6: kasy miast bez kasowania nadwyzek i bez zlota za "zakupy" mieszczan; z nadwyzki 7% dziennie: 2/3 pan, 1/3 skarbiec krolestwa; zwrot zoldu z korony nie obejmuje zalogi, ktorej zold i tak wraca panu; tarcza zoldu z 107 staje sie zbedna; start kampanii: kasy miast przyciete o ok. 14 mln (klucz `arm_townpurse`) | zalecana |
| 112 | `paczki/112-k7-utarg-wsi` | 306eafd | K7: utarg wsi nie znika - reszta po podatku pana zostaje we wsi; zaplata wojsk za zywnosc kupiona we wsi i sakwa rozbitego taboru tez (zwyciezca bierze cala sakwe) | nie |
| 113 | `paczki/113-ludzie-spustoszenie` | 7b5b894 | demografia krok 4 + 6a: rabunek i marsz armii wyganiaja ulamek ludzi okregu (uchodzcy wracaja latami), koniec odrostu z niczego, plon spada z brakujacymi rekami; zawiera `ScorchedEarth.Reset` | zalecana |
| 114 | `paczki/114-porzadki` | de5ced5 | porzadki: napis startowy SoldierPay, licznik zamiast pustego catch w paserze, pozycje ksiegi (naprawy, sakiewki rozbitych partii, wyplaty notabli); ZMIANA ZASAD: danina podzamcza dzielona z korona jak w miescie (pan 2/3, skarbiec 1/3; wlacznik Castle Dues Split With Crown) | nie |

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

## PRZEKAZANIE NA INNE KONTO - stan 06.10 15:05 (limit tygodniowy tego konta 92%)

CZYTAJ NAJPIERW TO. W grze: wpisy 101-107 (DLL md5 `0eeb0a105dc5ae4221809e222e327afe`), test zrobiony (wynik w nastepnej sekcji).
Wszystko, co gotowe, jest w repo i na origin (galezie `paczki/102b..114`, opisy `docs/paczki/`). Katalog roboczy poprzedniej sesji
(na tym samym komputerze, dysk C, katalog tymczasowy - moze zniknac): `
C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad
`
- klon lancucha: `scratchpad\lancuch` (galezie `n102b..n114` = to samo co `paczki/*`); skrypt skladania ogniwa: `scratchpad\dzien-1\nowy\ogniwo.ps1`
  (cherry-pick -n, generator MCM, build, commit; numery commitow podawac W CUDZYSLOWIE - PowerShell czyta `6155e9d` jako liczbe).

TRZY PACZKI W TOKU (06.10 15:05 pisza je autorzy, potem niezalezni recenzenci; baza kazdej = `n107-zold-i-skarbiec` = kod w grze):
1. `scratchpad\dzien-4\cena` -> galaz `l117-cena-od-niedoboru`: CENA SUROWCOW OD NIEDOBORU (uwaga Jeffa: "ruda jest zle wyceniona,
   w miescie bez rudy powinna kosztowac krocie"). Ustalone: wspolczynnik ceny gry = (popyt / (0.1 x podaz + 0.04 x wartosc zapasu
   + 2))^0.6; popyt to tylko konsumpcja mieszczan (podzielona przez nasz przelicznik: ruda /23.5), zuzycie kuzni nie wchodzi; stala 2
   zostala w starej monecie - ruda przy pustej polce ma indeks ok. 1.5 (cena zbytu 10.4 d za ladunek), plotno 10. NAJWAZNIEJSZA z trzech.
2. `scratchpad\dzien-4\warsztaty` -> `l116-warsztaty-w-nowej-monecie`: cena kupna i koszty dzienne warsztatow towarowych w nowej
   monecie (zgloszenie Jeffa: piekarnia w Lannisporcie 31 000 d).
3. `scratchpad\dzien-4\karawany3` -> `l115-karawany-ruda-dociera`: zakup surowcow przed zakupami BK, kierunek jazdy wedle zysku
   (w tescie 72 z 92 wyjazdow karawan dziennie bez wolnego miejsca; 736 karawan niesie razem 290 ladunkow rudy). Moze okazac sie
   w czesci zbedna po paczce 1 - ocenic po recenzjach.
Kazda paczka zostawia: worktree `<paczka>\repo` (zmiany wobec n107), `CHANGELOG-wpis.md`, proby poza gra; recenzent zapisuje commit na
galezi w klonie. Jesli sesja padla przed koncem: stan jest w worktree (git diff wobec n107), dokonczyc recenzje i commit recznie.

CO ZROBIC PO ICH ZAKONCZENIU: (a) zlozyc na n107 w kolejnosci cena -> karawany3 -> warsztaty (ogniwa 108a-c albo nowe numery),
build kazdego; (b) powiedziec Jeffowi prostym jezykiem, co zmieniaja, i wgrac NA JEGO SLOWO (gra zamknieta, kopia .bak, md5, wpisy
CHANGELOG); (c) test: nowa kampania 20 dob, `python tools/sprawdz_logi.py --grupa 1` - glowna liczba: "ruda: miast bez towaru"
(bylo 74 -> 69, cel ponizej 20); (d) przeniesc ogniwa n108..n114 na nowy szczyt (cherry-pick po kolei, konflikty zwykle tylko w
konstruktorze `ArmouryBehavior`, liscie `ApplyAll`, `Settings.cs`, `MoneyLedger.cs`) i zaktualizowac `paczki/*`.
UWAGA O KOSZCIE: na tym koncie jedna runda narzedzi glownego watku kosztowala pod koniec ok. 0.3 punktu limitu tygodniowego (kontekst
740 tys. tokenow) - nowa sesja jest tania; nie ciagnac dlugich sesji.

## WYNIK PIERWSZEGO TESTU W GRZE - 06.10 14:08 (grupy 1 + 2 = wpisy 101-107; nowa kampania, 20 dob; log `Armoury-2026-10-06_14-08-11.log`)

Odczyt: `python tools/sprawdz_logi.py --grupa 1` / `--grupa 2`. ERROR 0, wyjatki 0, potkniecia 0; wszystkie latki wpiete; parser
narzedzia rozpoznal wszystkie linie. Grupa 1: OK 61, UWAGA 5; grupa 2: OK 31, UWAGA 2.

- DZIALA: zapas startowy (ruda 7541 szt. -> 754 ladunki, drewno 20795 -> 2080); mineral BK ("wsie dopisaly" 182 = "model" 194,
  kopie 24 z 26 wsi - dotad srednio 15); woz (zatkane magazyny wsi zamkowych 4.6% wobec 7.4% miejskich; bylo 17% wobec 8%);
  ksiegi 102 / 102b (komplet linii, rozliczen rodow 321 na dobe, zold naliczony 397 tys. mediana); zold 107 (naliczone 445 tys.,
  do sakiewek 272 tys., do kas miast 97 tys., zamkow 42 tys.; tarcza: regulator nie skasowal 163 tys.); paser 106 (skup w strefach,
  kasy kryjowek 0); kolejnosc kupna karawan wedle zysku (ruda pierwsza w 29 z 420 wyjazdow).
- NIE DZIALA DOSTATECZNIE - KARAWANY A RUDA: miast bez rudy 74 -> 69 (cel <= 20), choc rudy przybywa (zapas swiata 887 -> 3685,
  targi miast 247 -> 1740 - lezy w miastach przy kopalniach). Karawany kupuja 2-69 ladunkow rudy dziennie; "brak miejsca w
  jukach" 32-72 dziennie (nasz zakup idzie po zakupach BK). Warsztaty zbrojne: 1482 cykle "brak surowca [ruda]". Poprawka w toku:
  workflow `dzien-4`, paczka `karawany3` (galaz `l115-karawany-ruda-dociera`, baza n107).
- ZGLOSZENIE JEFFA: piekarnia w Lannisporcie kosztuje 31 000 d przy 100 d gracza na starcie i chlebie po 6 d. Cena to wzor gry + BK
  w starej monecie (sprzet 6000 + dobrobyt x 4 + "place" = wydatki dzienne BK x 15 x dni roku), Armoury jej nie przelicza;
  wydatki dzienne warsztatow BK (12-50 d + 0.5% dobrobytu) tez sa w starej monecie - "kapital warsztatow" spada 34.7 tys.
  dziennie. Poprawka w toku: `dzien-4`, paczka `warsztaty` (galaz `l116-warsztaty-w-nowej-monecie`, baza n107).
- PIENIADZ (pomiar bazowy): zloto swiata 191.1 -> 178.8 mln w 20 dob (gra kasuje dar startowy kas: miasta 17.1 -> 6.9 mln, zamki
  7.1 -> 3.2 mln - zamkna to K5 / K6), od ok. 15. doby +40..+140 tys. dziennie. Zrodla z niczego w dobie 20: "zakupy" mieszczan
  526 tys., regulator +134 tys., GiveGoldAction z niczego 642 tys.; ujscia: rozliczenia rodow 443 tys., regulator 122 tys., utarg
  wsi 62 tys. (15%), GiveGoldAction w nicosc 689 tys. RESZTA NIEWYJASNIONA ok. -320 tys. dziennie (10% ruchu) - do zbadania.
- DREWNO: miast bez drewna 41 -> 17 -> 40; "bez wyjasnienia" +1.8..+2.5 tys. dziennie (dosypka RealisticBannerlord), budowy
  biora 1.6-2.4 tys.; karawany prawie go nie woza (zgodnie z przewidywaniem - brak zysku).
- PO PACZKACH Z `dzien-4`: wgrac je na n107 (w grze), potem przeniesc ogniwa n108..n114 na nowy szczyt (cherry-pick, MCM, build).

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
## Decyzje Jeffa 07.10 (wiazace)

1. Pytanie kanonu 1 (`docs/REGULY-KRAIN-I-DLUGU-2026-10-06.md` rozdz. 9): jency Innych wstaja jako trupy - TAK (Inni nie biora jencow; pojmany i odbity jeniec
   to polegly swojego regionu i cialo dla Innych, tylko za Murem i przy Murze).
2. Pytanie kanonu 4: bracia Nocnej Strazy BEZ ZOLDU (zold oddzialow Strazy = 0; zostaje wyzywienie i sprzet).
3. Pytania 2, 3, 5, 6, 7, 8 - Jeff poprosil o wyjasnienie z liczbami i mozliwosci (07.10); czekaja na jego wybor.
4. "zgoda na autotest" (pamiec: jeff-zgoda-na-autotest); przewijac dluzej, gdy to ma sens (40 dob na test zmiany, rok na skutki dlugie).

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

### Prace w toku 06.10 (katalogi `scratchpad\dzien-1..3`; klon `scratchpad\lancuch`, galezie `n102b..n114`)

- STAN 06.10 13:15: zadnych prac w tle. W grze grupy 1 + 2 (wpisy 101-107), Jeff JESZCZE NIE TESTOWAL. Lancuch do wgrania: 108-114.
  Limit tygodniowy tego konta 81% - nie puszczac kolejnych partii wieloosobowych na tym koncie; zostawic zapas na "sprawdz logi".
- "SPRAWDZ LOGI" po tescie grupy N: `python tools/sprawdz_logi.py --grupa N` (zna ogniwa 100-113 i grupy 1-5; nowych linii
  ogniwa 114 jeszcze nie - pokaze je surowo). Nie istnieje zaden prawdziwy log ogniw 101+, wiec pierwszy log obejrzec takze z
  `--surowe` / `--temat NAZWA`; pusta kolumna albo "formaty nierozpoznane" = poprawic parser, nie ufac alarmom tego tematu.
- PRZED GRUPA 2 (106 + 107): sprawdzic `OutlawFenceRadius` w `Armoury.json` (ma go nie byc albo 200). Opis 107 juz przepisany.
- PRZED GRUPA 3 (BetterEconomy + 108 + 109): skrypt `tools/bee/zamknij-ujscia-bee.ps1` (-NaSucho, potem naprawde; gra zamknieta);
  `sprawdz_logi.py --grupa 3` sam liczy, ile z 13 kluczy jest zamknietych (06.10: 0 z 13).
- PRZED GRUPA 4 (110 + 111 + 112, NOWA kampania; razem z nia mozna wgrac 114): (a) poprawic opisy `docs/paczki/112-*.md` i
  `113-*.md` wedle listy w `docs/paczki/PRZEGLAD-KOLIZJI-110-113-2026-10-06.txt` (K9, K10: renty wsi czytac jako "Ludnosc: renty
  zaplacone" minus "Kasy miast: ... panom"; 640 / 648 ustawien; skok kies band po 112 pochodzi z sakw taborow); (b) powtorzyc
  proby poza gra K7 i ludzie4 na ZLOZONYCH DLL (`dzien-1\nowy\dll\Armoury-n112-*.dll`, `-n113-*.dll`; harnessy w
  `dzien-2\k7\harness`, `dzien-2\ludzie4\harness` + `recenzja\harness`) - oczekiwane 0 nieudanych (K11); proba K5 + K6 na n113
  juz powtorzona: 261 z 261. Najwazniejsza liczba testu grupy 4: "Pieniadz swiata: razem" - po zamknieciu ujsc zostaje jedno
  zrodlo z niczego (dosypka regulatora miast ponizej zapasu kupcow), wiec suma zlota swiata bedzie rosla o te dosypke.
- GRUPA 5 (113): na TYM SAMYM zapisie co grupa 4.
- DROBIAZGI NA POZNIEJ (z przegladow, decyzje projektu): oblezone miasto nie powinno oddawac nadwyzki (K12); prog podatkow zamku =
  zapas kupcow (K13); etykiety logu po zlozeniu (K14); dwa miasta z zapasem kupcow ponizej rezerwy karawan 20 000 - Ifequeveron,
  Lotus Bay (K15); reszta kiesy rozbitej bandy przepada (ujednolicic z K7 i MenPurse); bezpiecznik kas miast z prawdziwym
  platnikiem (druga polowa K10); skarbce krolestw w pokoju tylko rosna.
- DALEJ WG PLANU (na inne konto): K8, K12 fundamentu, K13 skala produkcji; demografia kroki 5 (pobor), 6b, 7 (okno zniw);
  reguly krain R1-R13 po odpowiedziach Jeffa na pytania o kanon (`docs/REGULY-KRAIN-I-DLUGU-2026-10-06.md`, rozdz. 9).
- SKUTKI NOWYCH PACZEK, O KTORYCH JEFF WIE (06.10): zaloga we wlasnym zamku przestaje byc prawie darmowa dopiero po 114; w zamku
  sprzeda sie mniej lupu; spokojne miasto daje panu malo, miasto z wojskiem sporo; na starcie kampanii kupcy miast maja ok. 67 tys.
  zamiast 210 tys.; za rozbity tabor chlopski zwyciezca bierze cala sakwe (bandy szybciej sie dozbrajaja); po rabunku liczba
  palenisk prawie stoi, szkode widac w plonie. PYTANIE OTWARTE do Jeffa: czy uzywa w Spoils of War automatycznej sprzedazy
  magazynu wojennego we wlasnym zamku (utarg wracalby do niego - zloto z niczego).

- DECYZJA JEFFA 06.10 ("3 razy"): testy GRUPAMI, nie po jednym ogniwie. Grupa 1 TOWARY = 103 + 104 + 105 (nowa kampania
  wymagana przez 104); grupa 2 PIENIADZ = 106 + 107; grupa 3 LUDZIE = skrypt BetterEconomy + 108 + 109. Kazda paczka ma
  wlacznik w MCM - gdy cos nie dziala, wylaczac po kolei. Dalsze grupy (propozycja): grupa 4 KASY = 110 + 111 + 112 (nowa
  kampania zalecana - jednorazowe przyciecie kas na starcie), grupa 5 = 113 (nowa kampania zalecana). Wgranie grupy: cherry-pick wszystkich jej ogniw po kolei, jeden
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
