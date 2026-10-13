# Stan prac - przekazanie dla drugiego konta (2026-10-07 02:47: W GRZE 101-107 + GRUPA TOWARY 2 = 115-120, NIEPRZETESTOWANE - czeka na test Jeffa: nowa kampania 20 dob, potem --grupa 2b)

Czytaj najpierw: `CLAUDE.md` (zwlaszcza sekcja 8, **zasada 0**: kazda zmiana = kontrola regresji, kolizji
i spojnosci calej logiki; oraz pulapka MCM w sekcji 7), potem ten plik, potem gorne wpisy `CHANGELOG.md`.

## PLAN K13 (skala produkcji) - 07.10, `docs/PLAN-K13-2026-10-07.md`

Trzy projekty (podaz / popyt / lancuch) + sedzia: szkielet POPYT (wojsko jako odbiorca odziezy, rzemioslo miast wedle wartosci), z lancucha: receptury
wedle wartosci, wytop przy kopalni ("Iron Bloom" zamiast rudy - NOWA kampania), srebro przy kopalni; z podazy: rzemieslnicy BK robia z niczego (mnoznik
count = 1 + rzemieslnicy/45/wartosc) i zawor dla towaru bez zbytu. Kolejnosc (numery w planie 127-139 sa ROBOCZE - 127/128 zajete przez pokretla i
Spoils; numery nada skladanie): ksiega towarow (log) -> rzemieslnicy BK z wlasnego wsadu -> rzemioslo miasta wedle wartosci -> odziez wojska -> receptury
warsztatow wedle wartosci -> K13 x1.3 -> wytop przy kopalni -> zawor -> (warunkowo) zlom do kuzni, reszta receptur, srebro, rzemioslo wsi BK, naprawy z materialem.

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
3. PRZEBIEG 08:03 (Armoury 121-126, 40 dob): OK 40/40 (12.3 s/dobe), DLL i zapisy Jeffa przywrocone. Las wsi (126) +1190 ladunkow/dobe w 475 wsiach,
dosypka RBL zablokowana (125), drewno w miastach rosnie (12.1 tys.), warsztaty mediana 13.3 tys. (max 452 tys.), piekarnia Lannisport 16.2 tys.
ZATKANE MAGAZYNY BEZ POPRAWY: doba 40: 33 + 28 = 61 z 571 (10.7%; przebieg 2: 60) - modele 122 i 125/126 (1-3%) sie nie sprawdzily; przyczyna
nieznana -> najpierw diagnostyka w logu (sklad magazynu zatkanych wsi, stan wozu), potem poprawka.
4. PRZEBIEG 09:11 (126 + diagnoza, galaz w-toku/diagnoza-zatkanych-wsi): PRZYCZYNA ZATKANYCH WSI ZNALEZIONA - z 53 zatkanych 39 ma WOZ W MIESCIE
(nieoblezonym) srednio od 27 dob (max 40); gra nie wystawia nowego wozu, dopoki stary istnieje (galaz "woz poza domem > 7 dob" w grze martwa).
Magazyny zatkanych: zboze 54%, ryby 15%, drewno 8%. Reszta: 5 spladrowanych, 2 najezdzane, 6 wozow w drodze do domu, 4 bez wozu.
PRZYCZYNA (07.10): wszystkie wozy "w miescie" stoja w DWOCH miastach - Wickenden i Lord Hewett's Town. Pamiec drog mapy ROT
(settlements_distance_cache_Default.bin, 28.07) jest starsza niz siatka mapy (navmesh 17.08): bramy Wickenden, Lord Hewett's Town i zamku
Acorn Hall nie maja wpisu -> odleglosc 1e8 -> straznik BK GuardSettlementMove (>= 50000) odrzuca KAZDY rozkaz wyjazdu -> woz stoi na zawsze,
a gra nie wystawia nowego. Od 119 te miasta wybiera do 99 wsi, stad liniowy przyrost. Poprawka CartTownExit (galaz w-toku/wozy-nie-utykaja-w-miastach,
f8dced3, na diagnozie): rozkaz wyjazdu z takiej bramy przechodzi + bezpiecznik 2 doby; autotest 5 w toku. DO SPRAWDZENIA: czy lordowie / karawany
tez utykaja w tych miastach (diagnoza liczy tylko wozy wsi).
AUTOTEST 5 (07.10 10:40, DLL f8dced3 md5 ab6530b5, 40 dob, 9.6 min, 12.2 s/dobe, wynik OK, DLL Jeffa przywrocone 25b87631, zapisy nietkniete;
log Armoury-2026-10-07_10-40-16.log): WOZY NAPRAWIONE - w 40. dobie zatkanych wsi 19 zamiast 53 (zamkowe 11 z 260 zamiast 25, miejskie 7 z 311
zamiast 27); woz zatkanych W MIESCIE 1 (2.6 doby) zamiast 39 (27 dob); wozy wsi w miastach 16, najdluzej 0 dob; bezpiecznik 0 (latka wystarcza).
Z 19 zatkanych 16 to wsie spladrowane / najezdzane (wojna - gra i tak nic tam nie produkuje), naprawde zatkane Normal 3; BRAK WOZU 10 (woz zniszczony,
wies poza Normal nie wystawia nowego). NOWE: ten sam straznik BK odrzuca rozkazy KARAWAN (doba 2: 4, doba 40: 30 dziennie; Wickenden 20,
Lord Hewett's Town 8) i LORDOW (2-6 dziennie, Acorn Hall 3) - karawany i lordowie tez utykaja w tych osadach; Pyke / Farton / Hull / Arbor /
Downdelving / Pebbleton (wyspy?) - do sprawdzenia. Poprawka U ZRODLA w toku (workflow pamiec-drog-rot-uzupelniona, galaz w127-pamiec-drog):
uzupelnienie 22 scian siatki bez wpisu w pamieci drog po wczytaniu - dla wszystkich partii; potem autotest 6.
GOTOWE po recenzji (07.10): RoadMemoryFix.cs (postfiks Campaign.LoadMapScene, wylacznik MapRoadTableFix, generator gry dla scian 17842-17863:
Default 22/22, All 22/22, Naval 0), galaz w-toku/pamiec-drog-rot-uzupelniona c92f7e7 na f8dced3; DLL autotestu 6 SCRATCH\dzien-6\pamiec-drog\nArmoury-pamiec-drog.dll md5 6d43c19c. Wyspy (Pyke, Hull, Arbor, Farton, Downdelving) - INNA przyczyna w BK: karawana BK bez statkow na wyspie
moze stac na zawsze (BKCaravansBehavior :1091, :1296), lord z wyspy na uczcie BK na ladzie stoi do konca uczty (BKFeastBehavior :247) -
autotest 6 zmierzy (nowe liczniki "najdluzej D dob"), ewentualnie osobna paczka.
AUTOTEST 6 (07.10 12:37, DLL c92f7e7 md5 6d43c19c, 40 dob, 12.6 s/dobe, wynik OK, DLL Jeffa przywrocone 25b87631, zapisy nietkniete; log
Armoury-2026-10-07_12-37-02.log): w grze "kontrola generatora 137 z 137", 4 bramy naprawione, 28 ms; odrzucen z powodu sciany bez wpisu 0 przez 40 dob;
zatkanych wsi 14 (zamkowe 8, miejskie 6; z tego 11 spladrowane / najezdzane, Normal 3) - bieg 5: 19, bieg 4: 53; zamki z ujemnym bilansem zywnosci 0.
WYSPY zostaja: karawany w Pyke 3-9 (najdluzej 13.9 doby), Pebbleton, Lonely Light, Blacktyde, Lys, Mormont Keep, Kyth, Qarkash, Lhazosh; lord w
Pebbleton 15.6 doby -> poprawka w toku (workflow wyspy-karawany-lordowie, galaz w128-wyspy), potem autotest 7.
WYSPY GOTOWE po recenzji (07.10): IslandRoads.cs (wylacznik IslandRoadsFix; regula w postfiksie straznika BK: partia ze statkami - ten sam cel droga
All; karawana bez statkow - cel osiagalny ladem wedle zysku BK, dom, najblizsze; uczta i gentry BK - AI wraca, gdy rozkaz nie przeszedl; postfiks
GetTradeScoreForTown -1 dla miast bez drogi ladowej), galaz w-toku/wyspy-karawany-lordowie 90234dc na c92f7e7, proba 111/111, DLL md5 3fead49a.
NIE objete (osobny krok po autotescie): karawany lordow BK kupione w zamku / wsi na wyspie z jednym miastem (BKLordPropertyBehavior.cs:69-79) dalej
czekaja; karawany Pyke czekaja na zysk z Lordsport jak w grze. Wyspy dolacza do TOWARY 3 na koncu skladania (jeden autotest calosci).
Folder sesji lawy (f16095a4) usuniety przez Jeffa ok. 15:00 - pliki prob i raporty odtworzone pod ta sama sciezka z D:\Backup-Bannerlord\nprzekazanie-lawa-2026-10-07 (proby, przeglady, skrypty-latek, sklad) i z repo docs/przekazanie-lawa-2026-10-07.
K13 krok 3 (129 rzemioslo miasta: sukno/plotno/skora wedle oplacalnosci, galaz w-toku/k13-3-rzemioslo-miasta 9f7408f na k2) i krok 4
(130 odziez, buty i plotno wojska z sakiewki zolnierzy, w-toku/k13-4-odziez-wojska 93ac4c1 na k2 - NIE na 129; test i wgranie TYLKO razem 128+129+130
jako G1; zmiana widoczna: znika kara morale BK "Textiles supplies") - gotowe po recenzji, opisy docs/paczki/w-toku/k13-3-*, k13-4-*.
Zbroja z CRAFT jak bron (w-toku/zbroja-craft-jak-bron 2344a61 na 127; tabela szans docs/TABELE-ZBROJA-SPOILS-2026-10-07.md), Spoils klan najemnikow
"tylko prawdziwi" (w-toku/spoils-najemnicy-prawdziwi 383a724 na 128; znaleziona tez nocna podmiana wodza przez BK - naprawiona dla klanu Jeffa; 5 klanow
najemnikow ROT ma te sama wade - osobny krok swiata) i naprawa u kwatermistrza z materialem (w-toku/spoils-kwatermistrz-z-materialem f0aef78 na 128)
- gotowe po recenzji. OTWARTE: lawa naprawcza kowala (DoMendLoot) naprawia wraki bez materialu (zysk ok. 800 d na Brigandine z niczego) - Jeff
uruchomil osobna sesje "Close Armoury mending-bench wreck repair hole" (07.10); naprawy ludzi (TroopSelfMend) i AI (AiWear) bez materialu - K13 krok 139;
karczma rodzi najemnikow z niczego - krok "weterani".
Gotowe obok (na 126): `paczki/127-pokretla-jeffa` (95baa63; json Jeffa: 45->35, 20.0->13.33, 5->2 przy wgraniu), `paczki/128-spoils-bez-darmowego-zlota` (83a8b1d).
Pytania do Jeffa (07.10): zbroja z CRAFT jak bron (zepsuta/legendarna)? klan najemnikow Spoils dostaje ludzi z niczego - uszczelnic? naprawa u kwatermistrza
Spoils bez materialu - ujednolicic z kuznia?

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
- WIOSKI - IMMERSJA (08.10 ok. 02:20): "bierzemy wszystkie 8 do wiosek": (1) dymek z panem okregu i herbem, (2) proporczyk w barwach pana,
  (3) rycerz / pomniejszy rod zaprzysiezony z lore (prawdziwe rody z siedziba w poblizu, potem wymyslone w stylu krainy), (4) czym zyje wioska,
  (5) wiara i swiete miejsce, (6) zdanie historii, (7) pamiec wojny (po 108-113), (8) menu okregu z lista wiosek. Do tego (08.10 02:11): "za prosta
  linia", modele farm / wiosek / mlynow / spichlerzy (mlyny przy rzekach), drogi nieutwardzone / lesne - zbadac. W toku: wioski-wyglad-i-zdjecia
  (wyglad + roznorodnosc + bez AccessViolation + zdjecia w autotescie), wioski-uklad-i-drogi (generator v4 + badanie drog), wioski-lore-i-immersja
  (dane lore + projekt techniczny pkt 1-8). Sesja Jeffa 08.10 02:01-02:14: zapis save039 caly; 7 x AccessViolation w diagnostyce wiosek;
  wywrotka silnika przy wyjsciu (0xC0000005 po "Managed Interface deleted", ta sama co w autotestach); zalecone MCM Map Villages Enabled = off.
- WRAKI (07.10 ok. 15:35): "wraki ida na zlom" - kowale miasta NIE odnawiaja wrakow (Mangled i stany <= 0.10) nigdzie: ani u kwatermistrza, ani na
  polkach wojska (koniec decyzji 26.08 "wrak max 10% wartosci"), ani dla ludzi i lordow AI; wrak = zlom do przetopu (zrodlo materialu MendMaterial) albo
  wlasne kowadlo gracza. Do wprowadzenia w TOWARY 3 (TroopMend / TroopPieceCost i wszystkie drogi, ktore jeszcze odnawiaja wraki).
- WIOSKI 2200 W GRZE (07.10 ok. 15:30): "poczekaj z wioskami, wgraj mi wioski 2200, chce najpierw zobaczyc w grze" - TERAZ sam widok (W2) z wariantem
  ok. 2 200 wiosek na wersji z gry (n120), wgrac po sprawdzeniu (workflow wioski-2200-w-grze, galaz w2-wioski-2200). Wersja wedlug ludnosci CZEKA
  (workflow v3 zatrzymany); przy niej pamietac (Jeff): "nie wszyscy ludzie zyja w wioskach - sa ludzie w miastach, podrozni, w samotniach,
  klasztorach itp." - liczba wiosek tylko z ludzi mieszkajacych we wsiach (udzial wedlug regionu: osadnictwo skupione vs rozproszone, zagrody,
  klasztory / septy, pustelnie, mlyny, karczmy, podrozni, ludzie w lesie z ksiegi).
  STAN 07.10 17:50: paczka W2 (galaz w2-wioski-2200 3b59c75 na n120, MapVillagesView.cs) + dane wedlug ludnosci (agenci zrobili od razu warianty
  4000 / 6000 / 8000 ludzi na wioske: 2449 / 1840 / 1459 wiosek; wybrany 4000 - najblizej "2200"; pliki D:\Backup-Bannerlord\wioski-2026-10-07).
  AUTOTEST 17:40 (20 dob, OK, DLL Jeffa przywrocone 25b87631, zapisy nietkniete): plik wczytany (2449 w 422 okregach, 0 odrzuconych), ogien
  przy rabunkach dziala (18 ogni w wioski.log), 0 potkniec, ALE postawione 0 - "brak siatek" (wzory wsi-matek 0 dobrych, 128 bez wzoru) - kod nie
  znajduje modeli domow wsi ROT. Poprawka w toku (workflow wioski-brak-siatek), potem krotki autotest i wgranie (Jeff: "wgraj"). Plik
  arm_map_villages.tsv (4000) lezy juz w Modules\Armoury\ModuleData (stary DLL go nie czyta).
  WGRANE 07.10 ok. 19:06 (po autotescie 19:01: postawione 633, 0 bledow): Armoury.dll md5 62e37795 (n120 + W2), dane tsv 08ab1f16 (4000).
  ZATWIERDZONY DLL W GRZE OD TERAZ = 62e37795 (autotest przywraca ten). TOWARY 3 musi dostac paczke W2 (commity 3b59c75 + 39b60a0) przy skladaniu.
  ZRZUTY JEFFA 08.10 ok. 02:05: dymek dziala; wioska = mala szara brylka / brazowa szopa (kopiowana tylko szopa *_wm_*, domy prefabu
  fm_village* / andal_village* zwiniete przez Town Scene Manager). Jeff: "a nie mozemy uzyc ikony wioski, co jest do wyboru?" (TAK - kepa domow wsi
  okregu, ok. 70% wielkosci) i "nadal nie ma ikonki malej wioski, mlyna, aby byla wieksza roznorodnosc" (male ikony wiosek, mlyny przy rzece, stodoly,
  lodzie wedlug miejsca). BLAD: 2 x AccessViolationException w MapVillagesView.TreeLine (natywne GetOldPrefabName; diagnostyka drzewa) -
  CrashScribe session-2026-10-08_02-01-55.log; Jeffowi zalecone MCM Map Villages Enabled = off do nowej wersji. W toku: workflow wioski-wyglad-i-zdjecia
  (wyglad z prefabu domow + roznorodnosc + bez GetOldPrefabName, diagnostyka tylko w autotescie; tryb zdjec w autotescie CrashScribe).
- CENA SPRZEDAZY SPRZETU (07.10 ok. 15:20; projekt docs/PROJEKT-CENA-SPRZEDAZY-SPRZETU-2026-10-07.md): (1) "tak, jesli to poprawia realizm ekonomii" -
  wgrac razem z 127 (kara BK x5 RAZ zamiast x25, podloga 2% od wartosci ZE STANEM); (2) "tak" - sufit: kupiec nie da wiecej niz 1/10 ceny nowej sztuki
  na tej polce; (3) "tak" - handel odnowionym sprzetem miedzy miastami zostaje. Paczka w toku (workflow cena-sprzedazy-paczka, galaz w127b-cena-sprzedazy),
  GOTOWE po recenzji: galaz w-toku/cena-sprzedazy-sprzetu a87ad4a na 127 (SellByCondition.cs; wylaczniki BkTradePenaltyOnce, SellPriceByCondition,
  SellCapPercentOfNewAsk 10; sufit = min(1/10 ceny nowej, cena wraku na tej polce), obejmuje amunicje, nie konie), DLL md5 66b74be2; naklada sie
  czysto na t3; Armoury.json - nic nowego (tylko edycje 127). Dolaczy na koniec TOWARY 3. Wraki - rozstrzygniete ("na zlom").
  OTWARTE (sprzed paczki): CleanseAmmo leczy tez kulawe konie gracza - kulawy kon kupiony i sprzedany zdrowy daje zarobek przy Handlu 300;
  poprawic w tym czacie (propozycje osobnego zadania od pomocnika wycofane).
- JEDEN CZAT (07.10 ok. 14:25, przekazane przez sesje lawy f16095a4): "nie puszczaj zadnych innych prac w drugim czacie, wszystko idzie w jednym" -
  wszystko prowadzi ten czat; zadnych rownoleglych sesji (takze propozycji osobnych zadan); przed kazda praca sprawdzic, czy temat nie jest juz robiony.
- KOSZT NAPRAWY (07.10 ok. 14:25): "nasza robocizna jest lepsza" - zostaje robocizna stosu n131b (b36a6d6: 0.2 x ubytek stanu x robota wykonania
  x dobrobyt miasta, MendMaterial.Labor/LaborF, jedno miejsce dla lawy, Pick a piece, uprzezy, polek wojska, ludzi, AI). Projekt
  docs/PROJEKT-KOSZT-NAPRAWY-2026-10-07.md NIE do wdrozenia; jego inne elementy (godziny zajmujace rzemieslnikow, limit AI, rabat 30%) tylko na slowo Jeffa.
- Z sesji lawy (szczegoly docs/przekazanie-lawa-2026-10-07/PRZEKAZANIE-DLA-DRUGIEGO-CZATU.md sekcja 6): wszelkie koszty w miescie zalezne od dobrobytu
  i stawek historycznych, spojne; kuznia za kazdy dzien, dzien kuzni bez tieru; puste kasy pilnowac wszedzie; rezerwa miasta 20 000 zostaje; kasy miast
  z dlugami - NA POZNIEJ; paczki 7a/7b/7c TAK, paczka 8 TAK; audyt produkcji wedle lore (smoki tylko Daenerys + misja gracza, mamuty Polnoc/Za Murem,
  wielblady cieple krainy, Dorne - decyzja Jeffa); konie: P0 pomiar TAK, P1 hodowla wedle popytu TAK (+ czy stadnin nie za duzo), P2 "kon ginie jak
  ginie" - tylko zabite, ocalaly kon = lup zwyciezcy, przegrani odkupuja; P3 juczne z targu TAK; lordowie MUSZA miec zapasowe konie; udzwig: propozycja
  zapasowy kon 60 kg, jezdziec +10 kg (MarchPace czyta udzwig).
- WIOSKI NA MAPIE (07.10 ok. 11:10): przyjal projekt rabunku osada po osadzie (okreg = ok. 240 osad po 250 ludzi, na mapie ok. 10 obrazkow-gromad
  na okreg wedlug ludnosci). NIE nazywac "przysiolek": kazdy obrazek to WIOSKA Z NAZWA ("Wioska XYZ spalona", "plonie na mapie" - widac ogien w
  trakcie rabunku i spalona po). Rozstawienie NIE losowe: "wzdluz drog, czesc kilka po bokach", przy brodach, mostach, rzekach - ma wygladac
  na mapie z sensem. Projekt w toku: workflow wioski-na-mapie-projekt (prototyp na prawdziwej mapie ROT z obrazkami, krytyk, poprawka).
  Drogi ROT sa namalowane w terenie (terrain.bin), rzeki to sciezki w scene.xscene (trident, red fork, mander, rhoyne ...).
- WIOSKI WEDLUG LUDNOSCI (07.10 ok. 15:10): "wiosek postawmy tyle, ile ludnosc danego obszaru ... x10 to byl przyklad" - liczba wiosek w okregu =
  ludzie okregu / P (1 wioska = gromada osad po 250 ludzi, dzis P ok. 6 000 -> ok. 7 000 wiosek na swiat, Ramsport 50, typowy okreg 10, Nocna Straz 0);
  prototyp v2 (1 483 wiosek tylko przy widocznych liniach) za maly. W toku: workflow wioski-wedlug-ludnosci (v3: P 4000 / 6000 / 8000, dawne trakty
  wyliczone jako miejsca, obrazki, tabela przykladow) - wynik do wyboru P przez Jeffa.

1. Pytanie kanonu 1 (`docs/REGULY-KRAIN-I-DLUGU-2026-10-06.md` rozdz. 9): jency Innych wstaja jako trupy - TAK (Inni nie biora jencow; pojmany i odbity jeniec
   to polegly swojego regionu i cialo dla Innych, tylko za Murem i przy Murze).
2. Pytanie kanonu 4: bracia Nocnej Strazy BEZ ZOLDU (zold oddzialow Strazy = 0; zostaje wyzywienie i sprzet).
3. Pytanie 2: 0% cial niespalonych - "spalaja wszystkich, bo wiedza"; 616 dawnych umarlych na start - TAK.
   Pytanie 3: 3% puli wyrzutkow regionu dziennie do Strazy (reszta z ludnosci); Jeff chce tabeli strat i przyrostu Strazy i Wolnych Ludzi.
   Pytanie 5: hamulce H1 + H2 + H3 (Straz nie pali wsi za Murem; Inni tylko z poleglych; przegrani uchodza zamiast ginac - CALY SWIAT:
   w bitwach AI ginie dzis 51% przegranych, cel w widelkach historycznych 15-40%); H4 (wojna falami) NIE.
   Rabunek (paczka 113: R 0.5 czlowieka na napastnika na dobe, 5% ginie / 5% w las / 90% uchodzcy) - Jeff pyta o dane historyczne: to szacunek
   projektowy; badanie zrodel zlecone 07.10 (workflow historia-rabunku), potem kalibracja.
   Pytanie 6: (a) jency z rabunku biora tylko kultury z niewolnictwem w swiecie Martina, ludzie zdjeci z napadnietej wsi; Zelazne Wyspy 27 tys.
   niewolnych na start - TAK (Jeff pyta, skad 27 tys.); rabunek = ulamek okregu (paczka 113: zabici odejmowani, uchodzcy wracaja).
   Pytanie 7: A - wierzyciel bierze caly dochod wsi + z kiesy ponad 38 tys. (poczet ok. 135 na oddzial).
   Pytanie 8: (c) odsetki zamrozone w dniu zajecia, BEZ umorzenia - "dlugi trzeba splacac, Bank z Braavos na pewno nie daruje".
5. Pentos i Lorath - BEZ niewolnictwa. Werbunek gracza "jak u AI" (rekrut od tieru 2 tylko ze sprzetem kupionym przez notabla, koniec darmowego
   kompletu DTE). Repo: galezie `paczki/108..114` NADPISANE wersjami na 120 (= `paczki-na-120/*`); stare (na n107) w `paczki-na-n107/*`.
   Spoils of War: ZADNEJ automatycznej sprzedazy - "system ma byc szczelny i wszystko z czego wynika, zero darmowej kasy".
   Pokretla: SmithingSkillPerTier 45 -> 35 (pamietac: kucie zbroi ma te sama zasade co bron - zepsuta / legendarna i premie; sprawdzic
   rozklad jakosci po zmianie); DurabilityPerArmorPoint 20 -> 13 (zbroja zuzywa sie 1.5 x szybciej, NIE 3 x); MinSellPercentOfValue 5 -> 2
   (nad podloga cene rupieci ustala podaz i popyt). Klucze SA w Armoury.json Jeffa - zmiana pliku przy wgraniu (gra zamknieta, .bak).
   Zbroja z CRAFT jak bron (zepsuta / legendarna, premie RBM: Legendary +15 / x5, Lordly +10 / x2.5, Fine +5 / x1.5, Loose -10 / x0.6, Rusty -20 / x0.3)
   - TAK (Jeff chce tabeli szans wedle kowalstwa). Klan najemnikow Spoils: TYLKO PRAWDZIWI zolnierze (koniec 20 z niczego). Naprawa u kwatermistrza
   Spoils - TAK ujednolicic: placi kasie miasta (kowale), zuzywa material z targu wedle stanu, wrakow (Mangled) nie odnawia.
   Glod: z prawdziwego bilansu jedzenia - projekt `docs/PROJEKT-GLOD-2026-10-07.md`, po ksiedze ludzi (108-113). POPRAWKA JEFFA 07.10: nazwa "SPICHLERZ"
   (nie stodola) i spichlerz napelnia PRAWDZIWA produkcja zywnosci wsi regionu w grze (zboze, ryby, ser, maslo, mieso z hodowli - bydlo, owce, swinie,
   drob - oliwki, winogrona, daktyle), przeliczona na racje jedna stala (zwykly rok ok. 1.2 x potrzeby); zima, spustoszenie, brak rak, mniej bydla
   zmniejszaja spichlerz same. Razem z tym: spladrowana wies produkuje wedle tego, co zostalo (symbol), nie zero na 8-17 dob.
   OKREG Z OSAD (Jeff 07.10): menu wsi pokazuje okreg (osady, ludzie, spalone, spichlerz); RABUNEK OSADA PO OSADZIE - po kazdej osadzie komunikat
   "spalono to i to, rabowac kolejna? zajmie to tyle czasu"; jedna osada to DNI (dojazd, przeszukanie, spalenie), caly okreg tygodnie; AI tak samo.
   10 x wiecej wsi na mapie - badanie wykonalnosci w toku. Projekt rabunku: workflow rabunek-osada-po-osadzie.
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

## PRZEJECIE STOSU n131 Z SESJI LAWY (07.10 ok. 14:30)

Sesja "Close Armoury mending-bench wreck repair hole" (f16095a4) skonczyla prace i przekazala wszystko tutaj (Jeff: "dokoncz i wszystko przekaz jemu").
Dokumenty: docs/przekazanie-lawa-2026-10-07/ (PRZEKAZANIE, STAN-SESJI, PLAN-KOSZTY-MIASTA, wpisy CHANGELOG 1-9, DIAGNOZA-NADMIARU-KONI,
AUDYT-PRODUKCJI-I-WARSZTATOW, autotesty). Galezie (klon lancuch; na GitHubie w-toku/n131b..n131k), kazda na poprzedniej, baza f0aef78 (131 kwatermistrz):
n131b lawa naprawcza + robota kowali z dniowek x dobrobyt (81109ab, b36a6d6) -> n131c warsztaty, NOWY TownWage.cs (cb21de2) -> n131d wynajem kuzni
(a9bb933) -> n131e budowy (3290b01) -> n131f niewola RealisticCaptivity (14504a5) -> n131g turnieje GrandTourney (e010a25) -> n131h kuznia za dzien,
puste kasy, rezerwa 20 000 + poprawki przegladu (5b121ea, c471705, 1fe5c35, 100112a, ad6f6ce); od c471705: n131i konie i zwierzeta (e10b449, 22336e0,
7d919ee) i n131j zamowienie sprzetu z polki (ce26642, a645723); n131k-sklad-proba (2f476d5) = h + merge i + merge j: bez konfliktow, build 3 x 0,
proby 118/118, 136/136, 130/130; AUTOTEST CALEGO STOSU 14:18 OK 40/40 (DLL D:\Backup-Bannerlord\przekazanie-lawa-2026-10-07\sklad: Armoury 4111b77e,
GrandTourney def7574d, RealisticCaptivity 03dca428). Zatwierdzone DLL Jeffa przywracane po testach: Armoury 25b87631, CrashScribe 11fa0214,
GrandTourney 0aa5d0ef, RealisticCaptivity c393f4fb. Analiza logow autotestu 14:18 - dosle sesja lawy.
STYKI PRZY SKLADANIU TOWARY 3: k13-3 TownCrafts placi 3 d bez dobrobytu -> WorkshopLaw.DayWage / TownWage.Index; warsztaty 116/123/124 vs n131c
(WorkshopTrade/WorkshopLaw); k13-4 odziez wojska i paczka 8 kupuja z tej samej polki (czy nie liczy sie dwa razy); 122 (caly magazyn) - jedna z
przyczyn nadmiaru koni; konflikt linii konstruktora ArmouryBehavior z diag / k13-1 / k13-4 / wozy / zbroja-craft; l115-l119 (06.10) - stare;
pamiec-drog i wyspy - niesprawdzone ze stosem; TownWageLink (GT, RC) czyta Armoury.TownWage.Index i Settings.TownRentFloorGold przez refleksje.
OTWARTE ze stosu: konie P0-P3 + srednia cena konia + udzwig; audyt produkcji i lore (nowa kampania); blad CrashScribe Mends.cs:2134/2140;
16 niskich zgloszen przegladu (PRZEKAZANIE sekcja 5); kasy miast z dlugami - na pozniej.
ANALIZA AUTOTESTU STOSU 14:18 (od sesji lawy, wf_283fe6bf-e3d): bledy z naszych modow 0 (8 starych sygnatur sprzed doby 1), 12.8 s/dobe;
ekonomia zdrowa (warsztaty broni 31 299 szt., 17.73 d/szt., sprzedaz/koszt 2.52; budowy sredni poziom plac 1.031; kasy miast 7.86 mln, zadne < 20 000;
naprawy AI 6.31 d/szt.). DO POPRAWY PRZED WGRANIEM: (1) BLAD 22336e0 (zywy inwentarz): w ROT ges, kura, kot, pies sa w kategorii "horse" ->
HistoricalPrices.Apply (ok. :431-457) liczy dzielnik popytu kategorii z przecenionych sztuk -> "horse" /25: popyt mieszczan na zwykle konie spada 25x,
konie na polkach 12 953 wobec 8 600-9 300, cena konia x0.40, zakupy mieszkancow do kas miast -17%, zamkow -42%, utarg wozow -5..-9%; poprawka: dzielnik
ze WSZYSTKICH sztuk kategorii (nieprzecenione = 1) albo drob osobno. (2) EFEKT e10b449 (kon najemnika po cenie targu): zloto AI za najemnikow do miast
+55-64% (479 zamiast ok. 290 na najemnika), glowy rodow -4..-5%, Zelazny Bank 198 tys. dlugu / 14 dluznikow, 1 bankructwo; kon najemnika z karczmy bierze
sie z niczego (wzorzec DTE), a miasto dostaje zaplate i konia nie oddaje -> kalibracja w zamknietej ekonomii (zaplata tylko za konia zdjetego z polki
albo najemnik z wlasnym koniem bez doplaty). (3) drobne: dzienna linia cen koni rekrutow; naprawy AI bez materialu z targu (K13 krok 139);
nieprzecwiczone w autotescie: oplata za kuznie, turnieje gracza, niewola, TroopSelfMend, rezerwa 20 000 przy domu i utargu.
-> po zlozeniu TOWARY 3 (workflow towary3-skladanie) osobny krok poprawek (1) i (2) na t3-sklad.

## TOWARY 3 - ZLOZONE (08.10 ok. 05:00)

Galaz t3-sklad c1e8bea (GitHub w-toku/towary3-sklad; worktree SCRATCH\dzien-6\towary3\repo), opis docs/towary3/SKLAD.md i WYNIK.md: na n126 paczki
127 pokretla, 128 Spoils bez zlota, 129 diagnoza zatkanych, 130 wozy, 131 pamiec drog, 132 zbroja z kuzni, 133 najemnicy, 134 kwatermistrz,
135 lawa + robota kowali, 136 warsztaty x dobrobyt (TownWage), 137 wynajem kuzni, 138 budowy, 139 niewola (RC), 140 turnieje (GT), 141-142 kuznia
za dzien / puste kasy / rezerwa, 143 konie po cenie targu, 144 zywy inwentarz, 145 zamowienia z polki, 146 ksiega towarow, 147 rzemieslnicy BK,
148 rzemioslo miasta, 149 rzemioslo x dobrobyt (NOWE), 150 odziez wojska, + W2 wioski (wersja z gry, z AccessViolation w diagnostyce), 151
sufit XP rafinacji, 152 BuildFunding, 153 SlowHealing po BK, naprawy ludzi i AI z materialem (MendMaterialMenAndLords). Audyt 5 wymiarow: 7 waznych
- wszystkie poprawione; proby OK (oczekiwane roznice opisane). DLL dll-final: Armoury a08ac43a, GT 1337433c, RC 3e04b89b (kopia
D:\Backup-Bannerlord\towary3-2026-10-08). Narzedzie sprawdz_logi.py z grupa 3t - obok galezi (naprawa\narzedzia), do wniesienia do repo.
Armoury.json przy wgraniu: SmithingSkillPerTier 45->35, DurabilityPerArmorPoint 20.0->13.33, MinSellPercentOfValue 5->2 (cofniecie: odwrotnie).
Nowe klucze zapisu: arm_mendstock, arm_towncrafts, arm_armyclothing. PRZED WGRANIEM DOLOZYC: wyspy (90234dc), cena sprzedazy (a87ad4a),
poprawki 144 (dzielnik "horse" przez drob) i 143 (kon najemnika z niczego), wraki na zlom (polki wojska), CleanseAmmo leczy kulawe konie,
nowa wersja W2 (wyglad 2 + uklad v4 - workflow wioski-wyglad-2), potem autotest 40 dob + zdjecia, potem "wgraj".

WIOSKI v4 WGRANE 08.10 ok. 04:55: Armoury 70b29477 (n120 + W2 85215de), dane b742f307 (v4, 2446 wiosek, kolumna model). ZATWIERDZONY DLL OD TERAZ = 70b29477.
Zdjecia z autotestu: CrashScribe\zdjecia\at-20261008-044251 (pomniejszone scratchpad\zdj2). Do poprawy: zapadajace sie domy andal w Reach (ustawiac dol BB
kazdej siatki na terenie), mlyn przy samym brzegu z kolem w wodzie, skrzydla wiatraka, rybacy na skalach w wodzie (sprawdzac teren pod obrysem).
TOWARY 3 musi przy skladaniu dostac W2 85215de (zamiast 3b59c75 + 39b60a0 + pliku 4000).

WIOSKI v5 WGRANE 08.10 ok. 06:30: Armoury b0e62e1f (n120 + W2 8518996), dane b742f307. ZATWIERDZONY DLL OD TERAZ = b0e62e1f. TOWARY 3 przy skladaniu musi dostac W2 do 8518996.
Drobiazg na pozniej: mlyny czesciowo za bardzo w wodzie (przesunac ok. 0.5 jedn. ku ladowi).

TOWARY 3 DOKONCZONE (08.10 ok. 06:50): t3-sklad 5ccb0f5 (GitHub w-toku/towary3-sklad) = c1e8bea + 154 wyspy, 155 cena sprzedazy, 156 poprawka 144 (drob poza
dzielnikiem koni), 157 kon najemnika z polki (+ poprawka recenzji: kwota z mnoznikiem kupujacego), 158 wraki na zlom (+ poprawka: Pick a piece na grzbiecie),
159 CleanseAmmo nie leczy koni (+ poprawka: TroopSelfMend pomija konie), W2 v4 i v5 (8518996). DLL dll-final-3: Armoury 5a7074c0, GT 1337433c, RC 3e04b89b
(kopia D:\Backup-Bannerlord\towary3-2026-10-08\dll-final-3). Autotest 40 dob + zdjecia w toku. Potem propozycja "wgraj" (Armoury.json: 3 klucze 127).

DECYZJA JEFFA 08.10 ok. 08:00 (kon najemnika): "po co mam placic za konia - albo najemnik ma konia, wtedy jest konny, albo przychodzi bez konia, wtedy
pieszy; jak ma konia, to chce wiekszy zold" + "skad bierze tego konia, system zamkniety, zeby konie nie pojawialy sie magicznie". -> paczka 160 w toku
(werbunek bez doplaty za konia, konni z wiekszym zoldem - historycznie ok. x2); ZRODLO konia i calego najemnika = krok "WETERANI" (najemnicy w karczmie
tylko z prawdziwych ludzi: zwolnieni z wojska, niedobitki, dezerterzy, wypuszczeni jency - z bronia, zbroja i koniem, ktore mieli) - zaprojektowac
zaraz po 160 (dzis karczma rodzi najemnika z niczego - jak w grze).
TABELA RODOW I KAS OSAD po autotescie T3 (strona dla Jeffa): https://claude.ai/artifact/XtGSMM926wzopigdJWBHNY . ZNALEZISKO: "regulator kasy"
(GetTownGoldChange czynnego modelu gry / BK) kasuje srednio ok. 196 tys. zl na dobe z kas miast i ok. 132 tys. z kas zamkow (doby 2-39; doba 1
jednorazowo -2.64 mln) - zloto znika z gry; kasy miast 17.0 mln -> 7.84 mln w 40 dob (tak samo w testach bez T3). Do zbadania i uszczelnienia
(np. nadwyzka ponad cel idzie do pana / na rynek zamiast w nicosc).

TOWARY 3 WGRANE 08.10 ok. 08:20 (Jeff: "wgraj najpierw towary"): Armoury 5a7074c0, GT 1337433c, RC 3e04b89b, Armoury.json 35 / 13.33 / 2. ZATWIERDZONE OD TERAZ:
Armoury 5a7074c0, GrandTourney 1337433c, RealisticCaptivity 3e04b89b, CrashScribe 11fa0214 (bez zmian). Galaz paczki/towary3 (5ccb0f5). Test roczny
(punkt odniesienia przed poprawkami ekonomii) puszczony ponownie po wgraniu.

160 GOTOWE po recenzji (08.10 ok. 10:00): kon wlasnoscia zolnierza - werbunek tylko dni zoldu (bez doplaty za konia, nic nie schodzi z polki), premia zoldu
konnych x1.5 (MountedWageFactor; historycznie x2 dla tej samej sluzby, ale gra placi po tierze) w partiach rodow i garnizonach (karawany bez premii); BKROTPartyWageModel
nie rozroznial konnych. t3-sklad 2bde2bf (w-toku/towary3-sklad), Armoury 2664b691 (dll-final-4, kopia D:\Backup-Bannerlord\towary3-2026-10-08\dll-final-4).
Opis docs/paczki/w-toku/160-kon-wlasnoscia-zolnierza.md. Wgranie na "wgraj" (GT, RC bez zmian). Uwaga recenzji: zysk lordow tylko na starcie, potem jazda kosztuje
tyle co w T3 - pilnowac Banku w dlugim tescie.

## 08.10 POPOLUDNIE: TEST ROCZNY, ZAPISY, ZWALNIANIE

Test roczny T3 (364 doby, OK; zapisy Jeffa i DLL w porzadku). Zloto rodow 62.3 -> 116.6 mln, rodow < 5000 zl 1 -> 46,
wojsko ~100 tys. od doby 30, doba gry 11.7 s -> ~24 s, zapis 19.6 -> 31.5 MB. Projekt ekonomii (workflow wp16zd75l):
docs/PROJEKT-EKONOMIA-OBIEG-2026-10-08.md (nieprzesledzony w repo), 4 pytania do Jeffa w rozdz. 0/14.

ZAPISY USZKODZONE (P0): plik zapisu trzyma dlugosc napisu na 2 bajtach; napis > 32767 B psuje wczytanie calego zapisu.
Nasze SyncData: RecruitKit (komplety rekrutow) 150-220 KB w save034-039 Jeffa -> TE ZAPISY SIE NIE WCZYTAJA bez latki;
po roku RecruitKit 2.9 MB (12 927 kompletow u 2760 notabli), AiWear 1.8 MB (33 006 wpisow, 631 druzyn), OutlawLaw 150 KB.
Paczka 161 (galaz w-toku/161-zapis, 1b20d5b na 160): SaveText.Sync (19 kluczy w kawalkach po 8000 znakow, klucz+"_parts")
+ ratunek przy wczytaniu (transpiler na ArchiveDeserializer.LoadFrom). Ratunek sprawdzony w grze: zapis z doby 360 wczytany.
Narzedzie poza gra: autotest repo tools/zapis/napisy.py (ktore napisy > 32767 B).

ZWALNIANIE (pomiar klatki AT3, autotest repo a3cd214): klatka 39 ms (nowa kampania, doby 1-8) -> 176 ms (doby 360-372),
raz na dobe przyciecie 2-2.8 s. Wg modow po roku: gra 55 ms, silnik (ruch, czekanie na watki) 46, Armoury 36 (x11 wobec
startu), ROT 13, BK 10.5, StrategicCampaignAI 6. Armoury: MenPurse.OnEntered 17 ms (x17; 9600 wejsc/dobe; sprzedaz nadwyzek
i naprawy AI przegladaja zbrojownie), ArmouryBehavior.OnDailyTick 1.5 s naraz (x10), AiGear.OnDailyTickParty 4 ms (x16),
tik godzinny 2.6 ms. Gra: AiMilitaryBehavior x44, werbunek x7; ROT malzenstwa x2.4. Druzyn 2071 -> 6213 (majatki BK 1390
od doby 4, karawany 324 -> 1037, bandyci 647 -> 1137, lordowie -> 654). "Widly" pod The Eyrie = kafelki druzyn w osadzie
(moneta karawana, widly ani lord ani karawana); w srodku bylo 7 - kafelki zostaja po druzynach usunietych w miescie (UI).
Probkowanie stosu (Thread.Suspend) ZAWIESILO gre - usuniete. (Sprostowanie: zapis koncowy autotestu NIE byl uciety przy wyjsciu - nie wczytywal sie przez napisy > 32767 B, jak wszystkie; z 161 wczytuje sie.)
DO ZROBIENIA: 161 sprawdzic (zapis w kawalkach, ponowne wczytanie) -> "wgraj"; RecruitKit sprzatanie (komplety ochotnikow,
ktorych nie ma w puli; zmarli notable); przyspieszenie MenPurse/AiWear/dziennego tiku Armoury (pomiar sekcjami wewnatrz).
161 GOTOWA DO WGRANIA (08.10 11:45): galaz w-toku/161-zapis 2e235ea (na 160), Armoury c01a54ba (dll-final-5, kopia
D:\Backup-Bannerlord\towary3-2026-10-08\dll-final-5) = 160 + 161. Wgranie dopiero na "wgraj". Testy: zapis z doby 360
wczytany ratunkiem Armoury (3 napisy, najdluzszy 2.93 MB); nowy zapis 0 napisow > 32767 B; zapis w kawalkach wczytany bez
ratunku (komplety 11 496/11 496); nowa kampania 4 doby OK, 0 bledow. Drugi blad znaleziony przy okazji: RecruitKit.Import
szukal notabli w SyncData (bohaterow jeszcze nie ma) - KAZDE wczytanie gubilo wszystkie komplety; teraz ResolvePending w
OnSessionLaunched (12 624 z 12 927) + Reconcile z pulami raz na dobe. DZIURA DO EKONOMII: ~95% werbunku AI tieru 2+ to
ochotnicy "bez zapisu" (komplet wzorca z niczego): ok. 1000-1500 na dobe na starcie, ~500 po roku - pule notabli zmienia
cos, czego VolunteerKit nie widzi (do zbadania razem z projektem ekonomii).
160 + 161 WGRANE 08.10 ok. 11:48 (Armoury c01a54ba, poprzedni 5a7074c0 jako Armoury.dll.bak-2026-10-08-przed-161; CHANGELOG).

## DECYZJE JEFFA 08.10 ok. 11:50
- Ekonomia (PROJEKT-EKONOMIA-OBIEG rozdz. 14.1): gracz-wasal z lennem placi koronie jak wasal AI, dwor graczowi nie; dlug wymarlego rodu na nowego pana
  wsi - tak; zalogi AI w pokoju o polowe - tak; karczmy na start z pula starych zolnierzy - tak. Kolejnosc paczek wg rozdz. 12 (169 KSIEGA OBIEGU pierwsza).
- **OPTYMALIZACJA GRY NA KONIEC** ("zajmiemy sie na koncu, jak juz skonczymy ja modowac"). Do zrobienia wtedy (pomiar 08.10, sekcja "08.10 POPOLUDNIE"):
  MenPurse.OnEntered (sprzedaz nadwyzek AI + AiWear.MendInTown przegladaja zbrojownie przy kazdym z ~9600 wejsc do miast na dobe, 17 ms/klatke po roku),
  ArmouryBehavior.OnDailyTick (1.5 s naraz raz na dobe - rozlozyc albo przyspieszyc), AiGear.OnDailyTickParty (4 ms), tik godzinny Armoury (2.6 ms),
  smieci (sprzatania gen0: warsztaty gry 29%, MenPurse 8%, dzienny tik 6%); cudze: AI lordow (gra) x44, ROT malzenstwa NPC, warsztaty gry; kafelki druzyn pod
  tabliczka miasta po druzynach usunietych w srodku (UI). Narzedzia: autotest -LoadSave autotest-rok-360 -Profile -Census (repo autotestu at1-autotest c5c25ce).
  Dopisane 09.10 (recenzja grupa11, uwaga 8): jeden odczyt pozycji lordow na godzine, wspolny dla musztry (`Drill.Hourly`, `_tr`), ksiegi snu AI
  (`NightMarch.AiSleepLedger`, `_ai.Pos`) i `WorldMeasure.Hourly` (`_t`) - dzis trzy osobne petle po `AllLordParties` + czwarta po wszystkich partiach
  (`SnapshotPositions`), kazda z wlasnym slownikiem "krok godziny" tej samej partii; ulamek ms na godzine, wiec dopiero na koniec.

## PRZEKAZANIE 08.10 ok. 12:10 (limit konta 98%)
Pelny stan do podjecia: **docs/PRZEKAZANIE-2026-10-08.md** (co w grze, praca w toku 169 + workflow, co czeka na Jeffa: BEE wariant zbrojowni a/b, narzedzia).
Jeff 08.10 ok. 12:15: "jak znika to zamykamy, ma byc logiczny system ekonomii, ze wszystko z czegos wynika" - BEE wariant (b) -ListaZFundamentu;
akcje gracza BEE w nicosc zamknac latka Armoury (PRZEKAZANIE-2026-10-08.md rozdz. 7).
Jeff 12:30: "tak potwierdzam" - zamek kupuje bron w miescie, rekrut bez kompletu z tym co ma, szkolenie wlasna bronia -> paczka 171 (PRZEKAZANIE rozdz. 10).

## NOC 08/09.10 (sesja 7016f733) - WGRANIE 1 (Jeff: "Tak, wgraj sam" - tylko po udanym autotescie)
W GRZE od 08.10 ok. 19:07 (zegar komputera): **Armoury 28a7456e + CrashScribe aff275de** = 2e235ea + 170 (BEE domkniete) + T1 obozy 0-6 + T2 kalendarz Innych
+ T3 metal napraw + T4 dezerterzy do puli + T5 rodzina splaca Bank + T6 miara marszu (log) + T7 mlyny na brzegu i "held by" w dymku. Galaz paczki/noc-wgranie-1
(b1d8c58). Poprzednie (c01a54ba / 11fa0214): *.bak-2026-10-09-przed-noc1 obok plikow + D:\Backup-Bannerlord\wgrane\2026-10-09-noc1-przed. ZATWIERDZONE OD TERAZ:
Armoury 28a7456e, CrashScribe aff275de (autotest przywraca te). NIE w grze: T8 (poprawka bawelny), 169, 171, H3, T2b; skrypt BEE -ListaZFundamentu (zablokowany
przez zabezpieczenia Claude Code - Jeff uruchamia sam: powershell -NoProfile -ExecutionPolicy Bypass -File tools\bee\zamknij-ujscia-bee.ps1 -ListaZFundamentu).
Szczegoly: docs/PRZEKAZANIE-2026-10-08.md rozdz. 15-16, audyt docs/audyt-2026-10-09/.
**WGRANIE 2 (08.10 ok. 19:33):** Armoury 3bf72dfd + CrashScribe cfb33950 = wgranie 1 + T8 krainy (z poprawka bawelny) + T2b Inni bez dosypki z niczego.
Galaz paczki/noc-wgranie-2. Poprzednie (28a7456e / aff275de): *.bak-2026-10-09-przed-noc2 + D:\Backup-Bannerlord\wgrane\2026-10-09-noc2-przed.
ZATWIERDZONE OD TERAZ: Armoury 3bf72dfd, CrashScribe cfb33950.
**WGRANIE 3 (08.10 ok. 20:08):** Armoury 04d1cc99 (= wgranie 2 + 169 KSIEGA OBIEGU, sam log; galaz noc/sklad2 1254cd7 w klonie lancuch), CrashScribe bez zmian cfb33950.
Poprzedni Armoury 3bf72dfd: Armoury.dll.bak-2026-10-09-przed-noc3 + D:\Backup-Bannerlord\wgrane\2026-10-09-noc3-przed. ZATWIERDZONE OD TERAZ: Armoury 04d1cc99, CrashScribe cfb33950.
Testy: 169 sama 40 dob (gra niezmieniona, reszta swiata -38 tys./d zamiast -215 tys.), S3 40 dob, S3 zapis doby 362 - 0 bledow. Znane braki pomiaru -> 169b w toku.
**WGRANIE 4 (08.10 ok. 21:50):** Armoury 0f8a80b0 (= wgranie 3 + 169b poprawki pomiaru + H3 PRZEGRANI UCHODZA; galaz noc/sklad4 0ced2cd), CrashScribe bez zmian cfb33950.
Poprzedni Armoury 04d1cc99: Armoury.dll.bak-2026-10-09-przed-noc4 + D:\Backup-Bannerlord\wgrane\2026-10-09-noc4-przed. ZATWIERDZONE OD TERAZ: Armoury 0f8a80b0, CrashScribe cfb33950.
Testy: H3 sam 40 dob (przegrani zabici 19.4% zamiast 50.2%, zwyciezcy 2.3% zamiast 6.7%), S5 40 dob (13.3 s/dobe, 0 bledow, reszta swiata -27 tys./d), S5 zapis doby 362 (24.2 s/dobe, 0 bledow).
NIE wgrane: 171 + 172 (niezaliczone progi - rynek, strzaly w karawanach; decyzja Jeffa), T2c i T9 (w toku).
**WGRANIE 5 (08.10 ok. 22:13):** CrashScribe 4552ceaf (= cfb33950 + T2c: Inni nie obleagaja i nie rabuja zamknietych celow przed terminem kalendarza, trwajace oblezenia
przerywane bez strat; galaz w-toku/n11-mur f6d4e51), Armoury bez zmian 0f8a80b0. Poprzedni CrashScribe cfb33950: CrashScribe.dll.bak-2026-10-09-przed-noc5 +
D:\Backup-Bannerlord\wgrane\2026-10-09-noc5-przed. ZATWIERDZONE OD TERAZ: Armoury 0f8a80b0, CrashScribe 4552ceaf.
Testy T2c: zapis doby 362 (10 dob; Nocny Krol przerwal oblezenie Craster's Keep, 0 osad zdobytych - bez T2c 3) i nowa kampania 40 dob - 0 bledow.
**WGRANIE 6 (08.10 ok. 23:00):** CrashScribe 269cc980 (= 4552ceaf + T9 bez reparacji Diplomacy za wojny fabularne ROT; galaz w-toku/n12-reparacje d3ad3b0), Armoury 0f8a80b0.
Poprzedni CrashScribe 4552ceaf: CrashScribe.dll.bak-2026-10-09-przed-noc6 + D:\Backup-Bannerlord\wgrane\2026-10-09-noc6-przed.
**ZATWIERDZONE OD TERAZ: Armoury 0f8a80b0, CrashScribe 269cc980** (GT 1337433c, RC 3e04b89b bez zmian). Test 120 dob tej wersji: 0 bledow, 15.5 s/dobe, 21 pokojow
fabularnych bez reparacji (nie naliczono 14.1 mln), bankrutow w dobie 120: 15 (przed noca 23), glow < 5000: 32 (40), Inni 0 osad. Raport: docs/RAPORT-NOCNY-2026-10-09.md.
**09.10 (rano) - Jeff: 1) "tak" skrypt BEE, 2) "nie rozumiem" (suwak reparacji Diplomacy - do wyjasnienia), 3) "nie" - karawany NIE handluja strzalami, 4) "tak" tempo
swiata wariant ksiazkowy, ale po naprawach ekonomii.** SKRYPT BEE NALOZONY 08.10 23:40 (zegar komp.): zamknij-ujscia-bee.ps1 -ListaZFundamentu, 13 wartosci, SHA 1f963cfa ->
3a24554b; kopia: *.bak-<data>-przed-BEE-ujscia obok pliku + D:\Backup-Bannerlord\bee-2026-10-08\. Cofniecie: tools\bee\cofnij-ujscia-bee.ps1 (gra zamknieta).
**09.10 - Jeff: "ok zgadzam sie" (suwak reparacji):** Diplomacy MCM ScalingWarReparationsGoldCostMultiplier 50 -> 10 w DiplomacySettings_v1.2.json (gra zamknieta);
kopia: DiplomacySettings_v1.2.json.bak-2026-10-09-przed-reparacje10 obok + D:\Backup-Bannerlord\diplomacy-2026-10-09\. Dotyczy wojen niefabularnych (fabularne - T9 = 0).
Cofniecie: przywrocic kopie albo w grze Mod Options -> Diplomacy -> Scaling War Reparations Gold Cost Multiplier = 50.
**DECYZJE JEFFA 09.10 (ok. 00:00):** (3) OKUPY WEDLUG MAJATKU - TAK (jedna regula dla gracza i AI: glowa rodu ok. pol roku dochodu rodu, lord ok. 2 miesiace,
gotowka z nadwyzki, reszta na raty) + PRAWO TRZECICH - TAK (korona, ktora placi zold, bierze 1/3 okupow i sprzedanego lupu wasali; za krola/wodza 2/3) -> do 165/168.
(4) ODBUDOWA SPALONEJ WIOSKI: 1 ROK (nie 2-3) - dotyczy wiosek pobocznych (kazda ma swoich ludzi i swoja czesc plonu okregu) i glownej -> do 108/113.
Wczesniej tej nocy: suwak reparacji Diplomacy 10 (tak), karawany bez strzal ("nie"), tempo swiata ksiazkowe po naprawach ekonomii ("tak"), skrypt BEE ("tak" - nalozony).
Audyt w PDF: docs/audyt-2026-10-09/AUDYT-SWIATA-2026-10-09-SKROT.pdf (39 str.) i -PELNY.pdf (126 str.).
**DECYZJE JEFFA 09.10 (KIESA LUDU, paczka 173):** (A) glod ma skutki - TAK (tydzien bez stac na jedzenie -> spada zadowolenie BK, za nim dobrobyt; nie w pierwszych
120 dniach kampanii; osobny wylacznik); (B) dziesiecina dla septow - TAK (5% utargu wsi, 1-3% dochodu ludzi; polowa jalmuzna, polowa budowa septow);
(C) ok. 5% mniej wojska AI - Jeff pyta "czemu?" (wyjasnione w czacie; czeka na decyzje).
**DECYZJA JEFFA 09.10 (C): PODATEK WOJENNY KORONY OD LUDZI - TAK** (zamiast ok. 5% mniej wojska AI przy kiesie ludu), z warunkiem Jeffa: "im wyzsze podatki,
tym wplyw na gospodarke i dobrobyt". Projekt (173 + 165): stawka wojenna ustalana przez korone wedlug potrzeby; skutki: (1) z kiesy ludu mniej na zakupy (najpierw
zbytki, potem odziez, jedzenie na koncu) -> mniejszy utarg kupcow i rzemieslnikow -> wolniejszy wzrost dobrobytu; (2) obciazenie ponad "znosny" poziom -> spadek
zadowolenia (podpiac pod mechanizm BK polityki podatkowej - sprawdzic w kodzie); (3) wysokie obciazenie + glod -> niepokoje/bunt (BK); AI krol dobiera stawke
wedlug potrzeby wojny i niepokojow. Historycznie: poll tax 1381 (Bunt Chlopski), Jacquerie 1358.
**DECYZJE JEFFA 09.10 ok. 00:10 (lista A-L):** A glod ma skutki TAK; B dziesiecina TAK; C mniej wojska AI NIE -> podatek wojenny korony (ze skutkami dla gospodarki);
D za Murem bez zboza -> MYSLISTWO I RYBY; E pokoj z biedy TAK; F "przegrani uchodza" takze przy autobitwie gracza TAK; G Mur najwczesniej w 6. roku TAK (jest: doba 2184);
H proba drog TAK + po drodze szybciej (+15%) TAK; I jency w Westeros: DO DOMU, przestepcy NA MUR; J proba zimy TAK; K domyslne 171 TAK (Jeff pyta, jak jego zalogi
"zostaja poza systemem" - wyjasnione w czacie); L TEJ NOCY (09.10) WGRYWAC SAMEMU to, co przejdzie autotest (z kopia) - TAK.
NOWE ZADANIA 09.10: (1) audyt umiejetnosci - zwlaszcza: przekazywanie broni/mieczy daje doswiadczenie (do zmiany) + calosciowy audyt umiejetnosci wobec naszych zmian;
(2) audyt armii kazdego krolestwa - tabele wojsk, balans, zgodnosc z lore (np. Polnoc swietna piechota).
**WGRANIE 7 (09.10 ok. 01:30 zegara komp.; zgoda Jeffa na noc 09/10):** Armoury 63640cb3 (= 0f8a80b0 + D1 za Murem mysliwi i rybacy + I1 jency w Westeros do domu / na Mur
+ F1 H3 przy autobitwie gracza; galaz noc/sklad5 d486813), CrashScribe bez zmian 269cc980. Poprzedni: Armoury.dll.bak-2026-10-09-przed-noc7 + D:\Backup-Bannerlord\wgrane\
2026-10-09-noc7-przed. Testy: nowa kampania 40 dob OK (13.1 s/dobe, 0 bledow), zapis doby 362 8 dob OK. ZATWIERDZONE OD TERAZ: Armoury 63640cb3, CrashScribe 269cc980.
**WGRANIE 8 (09.10 ok. 01:50):** CrashScribe e8d460c5 (= 269cc980 + E1 pokoj z biedy; galaz w-toku/e1-pokoj 5b4e551), Armoury 63640cb3. Poprzedni CS: CrashScribe.dll.bak-2026-10-09-przed-noc8
+ D:\Backup-Bannerlord\wgrane\2026-10-09-noc8-przed. Testy: 40 dob + zapis 362 - 0 bledow, 0 potkniec; SKUTEK NIESPRAWDZONY (0 glosowan o pokoj w 48 dobach; pokoje daje
Diplomacy z wyczerpania; Diplomacy lata ConsiderPeace - moze wycinac wnioski AI). ZATWIERDZONE OD TERAZ: Armoury 63640cb3, CrashScribe e8d460c5.
**DECYZJA JEFFA 09.10 (K, doprecyzowanie):** "chce oba mechanizmy" - (1) zolnierze (takze w druzynie gracza i w jego zalogach) SAMI sie dozbrajaja za swoje - z zoldu i lupow
kupuja lepszy sprzet; (2) gracz moze ich dozbroic WYMIANA - daje lepsza zbroje/bron, a oni oddaja mu swoja gorsza (Jeff: "pisalem juz o tym"). Do zrobienia (paczka K1).
**DECYZJE JEFFA 09.10 ok. 02:35 (ARMIE, pytania audytu 14 rozdz. 5 + sprzet wedlug tieru) -> paczka 175 ARMIE:**
(1) "Swietna piechota" Polnocy = wariant C (lore: Polnocnych jest MNIEJ, ale sa twardsi; +25 broni/Atletyki piechoty t3-t6 i przewaga w autobitwie na sniegu/w lesie),
z warunkiem Jeffa: "zeby nie zepsuc rownowagi gry, zeby nagle jedna armia nie bila wszystkich" (autotest: udzial wygranych bitew lordow wedlug krolestw, bez dominacji).
(2) Rycerz z koniem: OK na plan - najpierw pomiar awansow AI czekajacych na konia, decyzja po pomiarze; naprawic: kon oddany przez AI za awans trafia do zbrojowni
nowego jezdzca (jak u gracza), a nie znika. (3) Dothrakowie pod murami: (a) oblegaja jak wszyscy (kara jazdy przy oblezeniu), z autotestem; Dothrakowie konni (4.1).
(4) Pentos: (a) pelna armia, slabsza przez sprzet wedlug tieru. (5) Wyspy Letnie: ZOSTAWIC jazde (bez 4.7). (6) Zelazne Wyspy: (a) tylko jezdzcy Harlaw, reszta piechota.
(7) Slonie Volantis: (a) zostaja. (8) Sprzet wedlug tieru: (b) - Jeff: "kto zrobil, ze tier 4 dostawal tarcze tieru 6 i pod to umiejetnosc podnoszono, to jakas bzdura,
trzeba wszedzie poprawic" -> zasada "umiejetnosc do sprzetu" zostaje, ale WSZYSTKIE jednostki we wzorcach dostaja bron, tarcze, amunicje i pancerz swojego tieru
(nie wyzej); dotyczy wszystkich kultur i drzew.
**DECYZJA JEFFA 09.10 ok. 02:50 (wymogi sprzetu ZOSTAJA):** "tylko zasada, ze jak nie mam danej umiejetnosci, np. atletyki, nadal obowiazuje, ze nie moge zalozyc pancerza,
ktory ma takie wymaganie". -> Paczka 175 zmienia TYLKO wzorce zolnierzy (co dostaja), NIE wymogi przedmiotow: prawa tieru (pancerz (tier-1) x 35 Atletyki, bron/tarcze/amunicja
35 na tier, konie - Jazda) zostaja bez zmian dla gracza, towarzyszy, lordow i zolnierzy. Ta sama zasada w K1 (zolnierz kupuje / bierze od gracza w wymianie tylko to,
do czego ma umiejetnosc) i Z1 - sprawdzic przy recenzji obu paczek.
**SPROSTOWANIE 09.10 ok. 03:05 (wymogi sprzetu u bohaterow):** sprawdzone w kodzie - gra pilnuje wymogu u bohaterow (Jeff, towarzysze, lordowie) tylko dla przedmiotow
z umiejetnoscia w danych silnika (bron, tarcze -> Jednoreczna, konie -> Jazda; CharacterHelper.CanUseItem); PANCERZ (Atletyka) i AMUNICJA pilnuje tylko nasz ItemReq
przy zolnierzach - bohater zalozy kazda zbroje. Wypowiedz Jeffa z 02:50 + jego zasada z 29.08 ("CALY ekwipunek") = TAK dla Z16 (audyt 13 pytanie b): zakaz takze
u bohaterow (postfiks CanUseItem przez ItemReq.Meets dla pancerza i amunicji); lordom i towarzyszom Atletyka do wlasnego sprzetu (SkillSinew dla bohaterow, nigdy
w dol), gracz bez podnoszenia; zalozona juz zbroja nie jest zdejmowana na sile. Trening Atletyki w zbroi na postoju (+10 dziennie) - pytanie do Jeffa.
Paczka Z16 PO 175 (te same miejsca w Mends.cs) - na galeziach 175.
**DECYZJE JEFFA 09.10 ok. 03:45 (lista 24 pytan z czatu = ESENCJA + trening Atletyki + Qarth):**
1 MUSZTRA: a TAK (tempo zalezy od umiejetnosci Jeffa - Przywodztwo), b TAK (wedlug broni ludzi), c TAK (potem AI); pyta o czas awansu t1->t6 przy pelnym sprzecie (odpowiedz:
  BK TroopUpgradeXp = 3.0 w BannerKings.json -> koszty 900/1650/2700/3900/5100 = 14 250 XP; musztra Z14a (10+2xtier) x Przyw./170 [0.5-1.5] x postoj 1.5: Przyw. 100 ok. 945 dob,
  170 ok. 556, 255+ ok. 369; marsz x1.67 dluzej; bitwy osobno). 2 PERKI kwatermistrza: Jeff - "wywalic albo: przekazana bron i pancerz = szybsza nauka (bonus czasowy do XP,
  bo maja wiecej broni i pancerzy do treningu)" -> wariant ZAPAS DO CWICZEN (oddany sprzet trafia do zapasu cwiczebnego druzyny, daje mnoznik musztry, zuzywa sie - nic
  z niczego; perki wzmacniaja bonus), jedna regula dla AI. 3 rzemieslnicy tam, gdzie ludzie - TAK (chcial tabelke - dana w czacie z PROJEKT-174 4.x).
  4 (174 Q2a/Q2b/Q3) a, b, c TAK - "ale sprawdzimy to" (wlaczyc, ocenic w tescie). 5 zlom = przetapianie - potwierdzone wyjasnieniem (wlaczyc Q4). 6 zaopatrzenie BK
  gracza jedna regula - TAK (Q5). 7 dorobek startowy zostaje - TAK. 8 trening Atletyki w zbroi - "nie rozumiem" (wyjasnione, czeka). 9 rycerze bez lenna bez oddzialow - TAK.
  10 renty korony dla gracza - TAK, jesli ma lenno (wyjasnione). 11 statki rozbitych sprzedawane portowi - TAK. 12 biedne krolestwa slabsze - TAK. 13 bez minimow okupu - TAK.
  14 miasto bez wojska ubozeje - TAK (wyjasnione). 15 obciazenie wsi: Jeff "bierze 30-50%, a nie 2/3" -> CEL 30-50% (pan + sept razem); 2/3 tylko przejsciowo do 166.
  16 zloty smok = 36-80 zl (a). 17 okup glowy rodu pol roku dochodu - zostaje (a). 18 korona bierze 1/9 lupu i okupow wasali. 19 Qarth z niewola - TAK.
  20 zima: a (dluzsza noc obozu zima) NIE; b lagodniejsza zima poludnia, c Wyspy Letnie bez zimy, d Skagos owce i kozy, e konie jedza zima - TAK (po probie zimy).
  21 drogi: "pokaz mi najpierw; ma wygladac rozsadnie - gdzie wioska, tam droga, a nie przez pole" (proba z drogami do wiosek). 22 "nie", 23 "tak" - NIEJASNE przypisanie
  (samotne kolumny / Inni w 1. roku / myto) - zapytane. 24 kanon: a stal valyrianska - pyta, czy wtedy nie bedzie tieru 6 (wyjasnione: material 6 w ROT nazywa sie
  "Valyrian steel" - propozycja: material 6 = "castle-forged steel" z normalnego lancucha, prawdziwa valyrianska tylko istniejace miecze + przekuwanie w Qohorze),
  b Qohor i Lorath bez niewoli TAK (UWAGA: Qohor w kanonie kupowal Nieskalanych - sprawdzic przed wdrozeniem), c smoki jedza owce TAK, d zloto Casterly Rock - wyjasnione, czeka.
**WGRANIE 9 (09.10 ok. 03:30 zegara komp.; zgoda Jeffa na noc 09/10):** Armoury 17a700d7 (= 63640cb3 + Z1 oddany sprzet nie daje XP (perki Giving Hands / Paid in Promise,
Spoils: uzbrojenie dowodcy, dar dla miasta, dar jedzenia, resztki trofeow), Spoils Cancel nic nie oddaje (7/7 ekranow), handel BK liczy tylko sprzedane, napisy bez obietnicy
treningu ("Equip the leader"), Z1b: Done bez pytania "You are discarding items" tylko na War stockpile i trofeach (tabor wroga zostaje z pytaniem - tam rzeczy przepadaja);
galaz w-toku/z1-xp-dary 8cbfc73), CrashScribe bez zmian e8d460c5. Poprzedni: Armoury.dll.bak-2026-10-09-przed-noc9 + D:\Backup-Bannerlord\wgrane\2026-10-09-noc9-przed.
Testy: nowa kampania 40 dob OK (13.1 s/dobe, 0 bledow Armoury, CS 8 startowych), zapis doby 362 8 dob OK (21.8 s/dobe). Autotest nie klika ekranow Spoils - Jeff: sprawdzic
Done/Cancel na War stockpile, trofeach, darze dla miasta. ZATWIERDZONE OD TERAZ: Armoury 17a700d7, CrashScribe e8d460c5. Baza Armoury dla dalszych scalen: w-toku/z1-xp-dary.
**DECYZJE JEFFA 09.10 ok. 03:55:** (8) trening Atletyki w zbroi - NIE potrzebny: "to juz jest, jak wlaczysz chodzenie na piechote albo chodzisz po mapie pieszo" (audyt 13: Atletyka
rosnie z marszu pieszo) -> Z16 bez +10/dobe. (15) obciazenie wsi docelowo 30-50% - OK. (24d) zloto Casterly Rock wyczerpane - OK (po naprawie dochodow rodow).
(24a) Jeff pyta: "czyli chcesz dodac 7. tier stali, z ktorej mozna wykuc tylko legendarne i mityczne zbroje i miecze?" - odpowiedz w czacie (kuznia gry ma staly zestaw
6 stali; material 6 -> "castle-forged steel"; stal valyrianska osobno, poza kuznia, tylko w istniejacych legendarnych mieczach, przekuwanie w Qohorze) - czeka na potwierdzenie.
**DECYZJE JEFFA 09.10 ok. 04:00:** (1) samotne kolumny lordow: "maszeruja noca, gdy trzeba - spiesza sie, by przerwac rabunek, uciekaja przed armia - ale maja miec takie same
kary jak gracz" -> T10: nocny marsz AI tylko z powodu (poscig/przechwycenie, obrona wlasnej wsi przed rabunkiem, ucieczka przed silniejszym), z tymi samymi karami co gracz.
(2) tempo musztry z tabeli (t1->t6 na postoju ok. 1 rok przy Przywodztwie 255+, ok. 2,6 roku przy 100) - TAK. (24a) Jeff: "czyli nie wykuje valyrianskiego miecza?" - wyjasnione,
czeka (propozycja: przekuwanie istniejacej stali valyrianskiej u mistrza w Qohorze jako jedyna droga).
**DECYZJA JEFFA 09.10 ok. 04:05 (24a STAL VALYRIANSKA) - TAK -> paczka 177:** material kuzni 6. poziomu (ironIngot6, w ROT "Valyrian steel") = "castle-forged steel" ze zwyklego
lancucha (tier 6 zostaje); prawdziwa stal valyrianska tylko w istniejacych legendarnych mieczach/zbrojach ROT; nikt nie wytapia nowej; jedyna droga do nowego miecza
valyrianskiego = PRZEKUCIE istniejacej stali u mistrza w Qohorze (za oplata dla kowala/miasta, kilka dni; wielki miecz -> dwa mniejsze jak Lod). Sztaby "valyrianskie" z niczego
(wedrowcy BK ok. 45/dobe) - zamknac (to tez czesc 164). Numeracja: 176 = werbunek gracza jak AI, 177 = stal valyrianska.
**ZGODA JEFFA 09.10 ok. 04:15 - STALA:** "zgoda na cala prace, wgrywaj po tescie, aby na mnie nie czekac" -> od teraz kazda paczka po udanym autotescie (nowa kampania 40 dob
+ zapis; progi z projektu) idzie do gry bez czekania, z kopia (bak + D:), md5, wpis WGRANIE N. Nadal: nie gdy Jeff gra; zapisy Jeffa nietkniete; zmiany rozgrywki spoza
decyzji - pytac; optymalizacja na koniec.
**DECYZJE JEFFA 09.10 ok. 04:25 (pytania z PLANU DO KONCA MODA, rozdz. 2):** 20 drogi - najpierw pokazac proba (zdjecia), "gdzie wioska, tam droga";
A (K1) bogaty zolnierz kupuje o stopien wyzej - TAK, jesli go stac i sztuka jest na rynku (wymog umiejetnosci dalej obowiazuje); B (K1) zaloga ze sprzetem dla < 75% ludzi -
"reszta walczy po prostu bez uzbrojenia" (TAK - tylko tym, co ma); C dezercja wedlug poziomu takze u AI - TAK; D zwyciezca bierze cale zloto taboru i bandy - TAK;
E wyrownanie dla panow zamkow (1/2 nadwyzki, renty), takze u gracza - TAK; F targ przy zamku - TAK; G BetterEconomy: prawdziwa wplata do kasy miasta + dar dla notabli - TAK;
H wytop przy kopalni tylko w nowej kampanii - TAK; I/J Dorne: PIASKOWE RUMAKI (nie wielblady), proporcje winnic i oliwek - "rzeczywiste proporcje ustal" (Claude dobiera z historii);
K rycerz z koniem - OK (liczby po tescie); L napisy na mapie tylko nad SPALONYMI wioskami; M namioty i ogniska przy napadzie na oboz - WLACZYC; N kuznia (okienko wyboru
rodzaju zbroi jak przy broni czy filtr ForgeView) - "nie rozumiem" (wyjasnione, czeka); O pasek gotowosci zbrojowni DTE (sypie bledem) - WYLACZYC.
**DECYZJA JEFFA 09.10 ok. 04:35 (N, kuznia):** "zostawic filtr" - bez okienka wyboru rodzaju zbroi w zakladce Craft; wystarcza filtr kategorii ForgeView (krok 5 paczki 129 skreslony).
**DECYZJA JEFFA 09.10 ok. 04:40 (K1 WYMIANA, doprecyzowanie):** "jesli dasz cos taniego, ale gorszego od tego, co maja - oni tego tez nie biora, zostaje po prostu w okienku
DTE, co oznacza, ze maja wszyscy lepszy sprzet albo wymagania byly za duze i nie spelniaja np. atletyki" -> sztuka gracza wypiera sztuke ludzi TYLKO, gdy jest lepsza i czlowiek
spelnia wymog; gorsza/rowna/za trudna zostaje w oknie DTE jako sztuka gracza, ludzie zatrzymuja swoja lepsza (koniec "pushed out better kit"). K1b = A + B + C (wf_013d4f17-3c8).
**DECYZJE JEFFA 09.10 ok. 04:55 (pytania audytu 15):** Q2 woz wsi zamkowej najpierw sprzedaje swojemu zamkowi jedzenie dla podzamcza na 10 dni, reszta do miasta - TAK
(razem z kiesa ludu, etap 5). Q3b rozbita karawana - "jak rozbije karawane, to zabiera wszystko zwyciezca" -> CALA kiesa karawany dla zwyciezcy (nie 10%), jedna regula
(gracz, AI, bandyci); korona bierze 1/9 lupu wasali; autotest pilnuje, czy bandy sie nie wzmacniaja (zloto band ma platnika i ujscie). Etap 3 (zloto taboru/band/karawan).
**I1b QARTH (09.10, NIEWGRANE, galaz w-toku/i1b-qarth w SCR noc2/i1b; drugi przeglad - 16 uwag przyjetych, wpis CHANGELOG I1b-p):** 24b Qohor - OTWARTE: kod bez zmian (Qohor z niewola, jak od 07.10), czeka na odpowiedz Jeffa na pytanie 1 PROJEKT-I1B (kanon: niewola w Qohorze jest); przy NIE - usunac qohorik z LosersFlee.SlaveCultureIds. Do wgladu Jeffa (pytanie 3): polityka karna jego osady (Execution/Forgiveness) jedna dla sprzedazy lordow i jego w kazdej krainie. Pytanie 2 (cena jencow wedlug prawa krainy osady sprzedazy, gracz i AI) - osobna paczka RealisticCaptivity.
**DECYZJE JEFFA 09.10 ok. 05:15 (I1b):** (1) Qohor ZOSTAJE z niewola (kanon: Nieskalani w strazy, TWOIAF), Lorath bez niewoli - TAK (zmienia wczesniejsze "24b Qohor i Lorath
bez niewoli": Qohor z niewola). (2) Polityka karna BK we wlasnym miescie/zamku gracza dziala tak samo w kazdej krainie (a) - przy "Execution" jency sprzedani tam u posrednika
sa straceni takze w Westeros; przy domyslnym "Enslavement" bez zmian. I1b w obecnej postaci (1590deb) zgodne z decyzjami.
**DECYZJE JEFFA 09.10 ok. 05:50 (175 ARMIE, pytania po wykonaniu):** (2) WLACZYC wylaczone propozycje audytu: sklad Volantis i Norvos wedlug ksiazek (slonie zostaja),
ciezsza piechota Qohoru, lzejsza piechota Dorne, mniej jazdy u Wolnych Ludzi i na Smoczej Skale. (3) najprostsze oszczepy dostaja tier 2 (Dornijczycy t2-3 z oszczepami,
nie z toporkami) - OK. (1) Polnoc: Jeff chcial widziec, co znaczy +15 w statystykach (wyjasnione tabela: +15 do broni glownej i Atletyki 43 rodzajom piechoty t3-t6;
srednio ok. +20% wobec swiata) - bez wyraznego wyboru; ustawiam domyslnie +15 (rada Claude, suwak NorthHardySkillBonus 0-50), do zmiany na slowo Jeffa.
**DECYZJA JEFFA 09.10 ok. 05:55 (Polnoc):** "131/130 jest wystarczajace po naprawie sprzetu" -> NorthHardySkillBonus = 0 (bez dodatku umiejetnosci; Polnoc ok. +7.5% wobec
swiata po samej naprawie sprzetu wedlug tieru). Zostaje: sklad z przewaga piechoty i +10% w autobitwie na sniegu i w lasach Polnocy. (175b liczy z 15 - po nim ustawic 0.)
**DECYZJA JEFFA 09.10 ok. 06:00 (Polnoc, ostatecznie):** "zrob 130/135 piechota Polnocy srednio" -> srednia piechoty t3+ Polnocy: bron glowna ok. 130 (po naprawie sprzetu
jest 131 - bez dodatku), Atletyka 135 (po naprawie 130 -> +5 Atletyki). Dwa osobne suwaki: dodatek do broni 0, do Atletyki 5 (zamiast jednego "+X do obu"). Po 175b.
**SKLAD7 (09.10 ok. 06:45):** noc/sklad7 48047f3 = sklad6 + K1/K1b/K1c (jeden system zbrojowni zalog 171+K1; sprzet idzie z ludzmi przy przenoszeniu druzyna <-> zaloga;
sakiewka zalogi placi za zakupy notabla; dorobek startowy zbrojowni zalog przy pierwszym wczytaniu starego zapisu - takze zalog gracza, z komunikatem w grze = swiadome
rozszerzenie decyzji 7). Zapisy zrobione na sklad6 (tylko testowe) nie sa wspierane. DLL SCRATCH test\Armoury-sklad7.dll (b7d7c2b9). Pytania do Jeffa: (1) zolnierz gracza bez
sztuki - DTE daje sprzet chlopa z niczego na bitwe (+kara morale) czy walczy tym, co ma; (2) ludzie lordow AI w bitwach gracza - DTE dopelnia wzorzec z niczego (zostaje u lorda
i w lupach): a) tak jak jest, b) tylko pozyczony na bitwe (LordBattleKitIsLent, gotowe, OFF), c) tylko to, co maja; (3) przenoszenie sprzetu z ludzmi takze dla partii towarzyszy.
**DECYZJE JEFFA 09.10 ok. 07:10 (MUSZTRA):** (1) "to ma zalezec od wielu czynnikow - przywodztwa, zmeczenia itp.; jest jedna zasada, wzor dla wszystkich, pod ten wzor podpinaja sie
dane danego lorda i wychodzi, jak szybko" -> JEDEN WZOR musztry dla gracza, lordow AI i glow rodow AI (bez osobnej bazy glowy rodu 15+3xtier): baza 10+2xtier x dowodca
(Przywodztwo) x dzien (postoj/marsz/glod/sen) x bron ludzi (171) x zapas do cwiczen x perki; Z14b (AI na tym samym wzorze) WLACZYC; skutek dla armii AI zmierzyc i pokazac.
(2) niewyspanie: "noc bez snu = nastepny dzien bez cwiczen", liczone od switu (b) - dla gracza i AI.
**DECYZJE JEFFA 09.10 ok. 07:35 (sprzet DTE z niczego):** (1) zolnierz gracza bez sztuki - "nie, jesli nie ma sprzetu, to nie ma sprzetu, nic nie dostaje z kosmosu" -> bez
awaryjnego sprzetu chlopa DTE (walczy tym, co ma); (2) ludzie lordow AI w bitwach gracza - (c) "walcza tylko tym, co maja" (bez dopelniania wzorca z niczego; nowa kampania:
na poczatku czesc bez zbroi/tarcz - Jeff akceptuje); (3) przenoszenie sprzetu z ludzmi takze dla partii towarzyszy (Manage Troops) - TAK. Do wdrozenia na noc/sklad7.
**DECYZJE JEFFA 09.10 ok. 07:45:** (177-1) Inni: (b) pelne obrazenia Bialym Wedrowcom i Nocnemu Krolowi tylko od stali valyrianskiej, smoczego szkla i smoczego ognia; bron t6 (stal
zamkowa) np. 50%, reszta 15% (jak w ksiazkach; Inni silniejsi - zmierzyc ich pochod/kalendarz). (177-2) wlasny miecz z Qohoru wedlug projektu z kuzni i z wlasna nazwa - TAK
(osobna paczka, pozniej). (177-3) "nie patrz na to, bede gral od nowa" -> Jeff zacznie NOWA KAMPANIE; obecny zapis nie wymaga ochrony (zgodnosc wstecz mniej wazna; zapytac,
kiedy startuje). (174b) hurtownicy zostawiaja ostatnia sztuke kazdego rodzaju zbroi na straganie dla kupujacych osobiscie - TAK (ShopKeepsLastArmour = true).
Pytanie Jeffa: "czy jak kupuje hurtowo, ma lepsze ceny, czy ceny zaleza od popytu i podazy?" -> odpowiedz w czacie (cena z popytu i podazy; AI dzis placi cene
z chwili zakupu za cala partie - do wyrownania: kazda sztuka jak u gracza).
**DECYZJA JEFFA 09.10 ok. 08:00 (ceny hurtu):** wyrownac - jedna zasada dla wszystkich: kazda kupowana sztuka po swojej cenie z popytu i podazy (cena rosnie, gdy ubywa
towaru), bez ukrytego rabatu hurtowego AI (dzis AiGear placi cene pierwszej sztuki za cala partie). Dotyczy wszystkich zakupow AI/zalog/notabli/sakiewek z gotowego
straganu (AiGear, BuySubstitutes, RecruitKit, GarrisonArmory, MenUpgrade K1 itd.). Po sklad7b (to samo drzewo).
**DECYZJA JEFFA 09.10 ok. 08:05:** "bede gral, jak skonczymy projekt" -> Jeff NIE gra do konca projektu; po skonczeniu zaczyna NOWA kampanie. Paczki projektujemy pod nowa
kampanie (zgodnosc ze starym zapisem drugorzedna; test na zapisie autotest-161-kawalki zostaje jako test dlugiej kampanii). Rzeczy "tylko w nowej kampanii" (kiesa ludu, ksiega
ludzi, wytop przy kopalni itd.) mozna wlaczac domyslnie bez ochrony starego zapisu.
**DECYZJE JEFFA 09.10 ok. 08:20 (MUSZTRA):** (1) dlug po krotkim snie zabiera kolejne dni, az sie wyspia - (a) ZOSTAWIC; (2) codzienna linia musztry w grze ("Drill today: ... XP
- Leadership x.., at rest x.., drill kit x.., armed ..%, perks +..") - TAK (wylacznik MCM, dom. ON); Jeff pyta, czy Przywodztwo i perki (np. +XP dla tieru x) sie nie gryza
-> wyjasnione: poziom Przywodztwa = mnoznik L, perki (Combat Tips, Raise the Meek t<3, Seven Veterans t4+, Drill Sergeant, Drills, Walk It Off, Bow Trainer itd.) = osobny
dodatek P jak w grze, nie mnozony przez L ani postoj; glod/niewyspanie zeruja wszystko. "rob od razu" - Jeff kaze dzialac bez czekania na reset limitu 5h.
SKLAD7B (08:20): noc/sklad7 23ad5d6 - zadnego sprzetu z niczego w bitwach (takze autobitwa gracza i lup), kon jako sztuka zbrojowni (jezdziec bez konia pieszo, kon za awans
do zbrojowni AI, Stables.Remount - lord sadza na konie z taboru i dokupuje, ColdStart z konmi), linia w grze na poczatku bitwy (ilu bez broni/zbroi/konia).
**DECYZJE JEFFA 09.10 ok. 08:40 (175c, Inni):** (1) autobitwa AI liczy bron PO naprawie 175 (jedna zasada) - TAK; (2) "tier 6 strzaly rania normalnie, czy belty, ale nie kamienie" ->
pociski: tier liczony z AMUNICJI - strzaly i belty tieru 6 jak stal t6 (50%), nizsze 15%; kamienie (proce, glazy) zawsze 15%; (3) smocze szklo dla Nocnej Strazy - NIE (zostawiamy);
(4) komunikat przy pierwszym ciosie w Nocnego Krola bronia inna niz stal valyrianska - TAK; (5) minimum obrazen w autobitwie - decyzja po tescie.
**175d (09.10 ok. 09:30):** CS w-toku/175-armie-cs 537e93f, Armoury w-toku/175-armie-arm 1402107 (DLL SCRATCH test\Armoury-175d.dll aa0ff2c6, CrashScribe-at-175d.dll 34656904).
Uwaga: wylaczenie OthersSteelRule NIE przywraca autobitwy sprzed 175 (AI zawsze liczy bron po naprawie - decyzja Jeffa 1); jedyna dzwignia na Innych w autobitwie = minimum
obrazen (decyzja 5, po tescie T-B1). Uderzenie tarcza/glowica w Innego = 15% bez komunikatu; komunikat tylko dla ciosow gracza i jego ludzi.
**DECYZJA JEFFA 09.10 ok. 10:20 (rycerze bez lenna - potwierdzenie po wyjasnieniu):** TAK - 85 rodow BK "gentry" nie prowadzi wlasnych druzyn; na wojne jada w druzynie swojego
pana (w jego pulapie wojska, pan placi jak rycerzowi swity) albo w armii krolestwa; w pokoju siedza w majatku; ta sama regula dla wasali-rycerzy gracza. Etap 2.
**JEFF 09.10 ok. 10:35 (strzaly):** "wykucie strzal to powinno byc grosze, strzal bylo mega duzo" -> strzaly tanie i liczne jak w historii (snop 24 ok. 16 d; grot kilkanascie g
zelaza; korona 1341-1359 1.23 mln strzal). sklad8 mial 35 miast bez strzal -> diagnoza i naprawa produkcji (wf_eefb76a2-a24): cena, zelazo na grot, wydajnosc strzelarzy.
**DECYZJE JEFFA 09.10 ok. 10:45 (projekt etapu 2):** (1) ile korona bierze za pojmanego krola/wodza armii - "nie wiem, a ile powinna?" -> Claude proponuje z historii
(prawo korony do "wielkich jencow": krol/nastepca idzie do korony, zdobywca dostaje nagrode - np. David II 1346: Copeland 500 L + renta; Jan II 1356 odkupiony od zdobywcy).
(2) "4% mniej wojska - NIE; to ma byc dynamiczne wedlug tego, czy stac na taka armie, i wedlug logiki AI, ze na czas wojny moga byc na minusie i splacac sie z lupow wojennych,
tak jak to bylo na wojnach" -> budzet rodu 166: w wojnie lord moze isc ponad dochod na kredyt/dlug wojenny (z limitem zdolnosci splaty - lup, okupy, zastaw dochodu),
po wojnie splaca z lupu i okupow i zwalnia nadwyzke; w pokoju wojsko wedlug dochodu. Bez stalego "-4%".
**DECYZJA JEFFA 09.10 ok. 10:50 (wielcy jency):** "pasuje" -> pojmanego KROLA albo NASTEPCE TRONU przejmuje korona zdobywcy (caly okup do skarbca korony), zdobywca dostaje
od razu nagrode ok. 1/10 okupu ze skarbca korony; wszyscy inni jency (takze wodz armii spoza rodu krolewskiego): okup dla zdobywcy, korona 1/9. Jedna regula dla gracza i AI
(gracz oddaje pojmanego krola swojemu krolowi i dostaje nagrode).
**DECYZJE JEFFA 09.10 ok. 11:05 (pytania projektu etapu 2):** (1) podloga 30 ludzi w druzynie (przy niezaplaconym zoldzie) takze dla gracza - TAK (jedna regula); (2) pulap dlugu
z okupow = rok dochodu rodu, kolejny okup ponad pulap mniejszy - TAK; (3) okup za KROLA placi SKARBIEC KROLESTWA (b), jak w historii (Ryszard I - podatek z calej Anglii), wiekszy
(ok. 2-2.5 mln zl u duzego krolestwa) - wpisac do projektu etapu 2 (krok dlugi i okupy).
**DECYZJA JEFFA 09.10 ok. 11:15:** projekt etapu 3 (szczelnosc) - "poczekaj, az skonczy sie etap 2" -> nie zaczynac etapu 3 (ani projektu) przed zakonczeniem etapu 2.
**DECYZJE JEFFA 09.10 ok. 12:15 (Z16):** (2) suknie, szaty, czapki i ubrania ROT jako zbroja t6 - "to jakas bzdura" -> wymog ubran (sukno, bez metalu) liczony z WAGI, nie z tieru
(dotyczy wszystkich: gracz, bohaterowie, zolnierze); (3) unikat, ktorego lord nie udzwignie, zostaje UNIKATEM w taborze (nie zamienia sie w zwykly przy wczytaniu) - TAK.
(1) podnoszenie umiejetnosci lordow - Jeff pyta, o co chodzi ("maja startowe zbroje, ktorych nie moga nosic?") - wyjasnione, czeka.
**DECYZJA JEFFA 09.10 ok. 12:25 (prawo tieru pancerza):** "trzeba obnizyc atletyke dla helmow, rekawic i nogawic i ogolnie obnizyc tier 6 do 150, plus peleryny, bo nawet dziecko
udzwignie rekawice - ten system nie ma sensu; jak najszybciej naprawic, zeby nie przebudowywac calej gry" -> PRAWO TIERU PANCERZA wedlug czesci: korpus (tier-1) x 30 (t6 150);
helm i nogawice polowa korpusu (t6 75) albo z wagi, gdy wiecej; rekawice i peleryny tylko z wagi; ubrania/suknie z wagi; ladry konskie - Jazda (nie Atletyka). Lordowie: BEZ
wyrownywania do 175 (Jeff: "nie kazdy lord lordowi rowny") - zostaja ze swoimi umiejetnosciami; kto dalej nie udzwignie korpusu - na starcie kampanii zbroja na miare (ten sam
rodzaj z tieru, ktory udzwignie); podnoszenie Luku/Kuszy - nie. Na galeziach 175 (CS Mends prawa tieru + Z16).
**DECYZJA JEFFA 09.10 ok. 12:40: "wszystko wedlug wagi"** -> wymog Atletyki pancerza WYLACZNIE z wagi: waga / 0.333 kg (50 kg plyty = 150), ustawiany dokladnie (takze w dol wobec
XML ROT: helmy 140, suknie 175-200 znikaja); prawo tieru pancerza wylaczone (0); ladry - Jazda; bohaterowie bez podnoszenia (HeroSkillToOwnGear OFF) - lordowie zostaja soba.
Zrobione szybko (bez workflow - Jeff: "nie mozesz tego szybciej sprawdzic"): CS d85b519, Armoury ffb6cef na galeziach 175 (nowe klucze ArmourKgPerAthletics/ArmourTierAthletics,
bo Armoury.json Jeffa ma stare 0.25/35). DLL SCRATCH test\Armoury-z16b.dll, CrashScribe-at-z16b.dll. Wejdzie w WGRANIE 11 (sklad9).
**ZASADA PRACY (Jeff 09.10 ok. 12:45 - "madre ocenianie pracy i przydzial zasobow bez utraty jakosci"):** kazde zadanie dostaje tor wedlug RYZYKA:
S (liczby, progi, teksty, male poprawki - sam + autotest, minuty), M (nowa mechanika w 1 obszarze, blad o nieznanej przyczynie - 1 workflow: krotkie rozpoznanie, kod,
1 recenzja, autotest, ok. 1 h), L (paczki calej ekonomii - pelny lancuch z krytykami i testem 120 dob). Dane przed decyzja, bez powtarzania badan, eskalacja przy ryzyku,
zadnego wgrania bez autotestu, tor i czas mowione Jeffowi na starcie. (Pamiec: jeff-zasada-doboru-glebokosci-pracy.)
**WGRANIE 10 (09.10 ok. 12:25 zegara komp.; stala zgoda Jeffa):** Armoury 2aec1a1b (noc/sklad8 2ca2f2b = 17a700d7 + 171 zbrojenie zalog + 172 strzaly + 172b karawany bez
strzal + 174 produkcja uzbrojenia + 174b dowoz rudy (kontrakty, morzem, rezerwa kramu) + K1/K1b/K1c dozbrajanie za swoje i wymiana (A/B/C) + sklad7b zadnego sprzetu z niczego
w bitwach, konie w zbrojowni + ceny po sztuce (bez rabatu AI i kupcow) + musztra jeden wzor (Z14a/b, zapas do cwiczen, sen od switu, linia "Drill today") + T10 nocny marsz AI
+ I1b Qarth z niewola + strzaly tanie (receptura, strzelarzy x2 = 0.6)), CrashScribe bez zmian e8d460c5. Poprzedni: Armoury.dll.bak-2026-10-09-przed-noc10 +
D:\Backup-Bannerlord\wgrane\2026-10-09-noc10-przed. Testy: nowa kampania 40 dob OK (14.2 s/dobe, 0 bledow; strzaly: miast bez 15.3; kontrakty 96.3%; zbroja na polkach OK;
cokolwiek na tulowiu 80%; miast bez rudy 41.5 - do etapu 6), zapis 362 8 dob OK (26.3 s/dobe, 0 bledow). ZATWIERDZONE OD TERAZ: Armoury 2aec1a1b, CrashScribe e8d460c5.
Baza Armoury dla dalszych scalen: noc/sklad8.
**WGRANIE 11 (09.10 ok. 13:45 zegara komp.; stala zgoda Jeffa):** Armoury 799a877d (noc/sklad9 5f170fd = sklad8 + 175 armie (sprzet wedlug tieru, sklad wedlug lore, Dothrakowie
konni, Polnoc +0/+5, Volantis/Norvos, Qohor ciezej, Dorne lzej, Pine Javelin t2, konie AI do zbrojowni, Inni - stal valyrianska/t6 50%/reszta 15%) + Z16 wymogi bohaterow + Z16b
pancerz wedlug wagi (0.333 kg/pkt, ladry - Jazda, bez podnoszenia bohaterow) + 177 stal valyrianska (castle-forged steel, mistrzowie Qohoru, rejestr klng)), CrashScribe
891503f6 (w-toku/175-armie-cs b54a245 = e1-pokoj + 175 CS + 175b/c/d + Z16/Z16b). Poprzednie: *.bak-2026-10-09-przed-noc11 + D:\Backup-Bannerlord\wgrane\2026-10-09-noc11-przed.
Testy: nowa kampania 40 dob OK (14.0 s/dobe, 0 bledow Armoury, CS 8 startowych), zapis 362 8 dob OK (26.4 s/dobe). Polnoc w grze 138/134 wobec swiata 129/119 (+7%/+13% jak w
planie; bezwzgledne wyzej niz rachunek) - OSTRZEZENIE kontroli w logu CS; do sprawdzenia (tor S): czesc jednostek Polnocy t3 ma Drzewce 140 (np. cerwyn_soldier, glover_footman)
- zamiana broni wedlug tieru moze nie objac drzewc. ZATWIERDZONE OD TERAZ: Armoury 799a877d, CrashScribe 891503f6. Bazy: Armoury noc/sklad9, CS w-toku/175-armie-cs.
**DECYZJA JEFFA 09.10 ok. 13:50 (bron wedlug tieru, tor S):** TAK - wymog broni bialej, tarcz i broni rzucanej DOKLADNIE (tier-1) x 35, takze w dol (koniec wymogow ROT
bez zwiazku z tierem, np. drzewce t3 = 140); luki, kusze, proce i amunicja - tylko w gore jak dotad. CS w-toku/175-armie-cs (W1). Do nastepnego wgrania (z krokiem A etapu 2);
po tescie sprawdzic srednie Polnocy (cel 130/135) i balans kultur.
**WGRANIE 12 (09.10 ok. 15:30 zegara komp.; stala zgoda Jeffa):** Armoury 1cc7890a (noc/sklad10 d3e749e = sklad9 + etap 2 krok A: 169c pomiary etapu 2 i D staly, 2.6 powinnosci
bez przerwanej petli, 2.14 okup gracza do porywacza / kurier bez dosypki), CrashScribe d68532a6 (w-toku/175-armie-cs 02d3584 = b54a245 + W1 bron biala/tarcze/rzucana wedlug tieru
dokladnie (30 broni obnizonych) + E1b pokoj z biedy (wniosek tylko od chetnego pana, staly dzien tygodnia, bez niszczenia krolestw bez lenn)). Poprzednie: *.bak-2026-10-09-przed-noc12
+ D:\Backup-Bannerlord\wgrane\2026-10-09-noc12-przed. Testy: nowa kampania 40 dob OK (14.1-14.2 s, 0 bledow, harness okupu 2b/2c OK), zapis 9 dob OK (26.2 s). E1b: 0 wnioskow
z biedy (biedne krolestwa w wojnach fabularnych ROT - pomijane wedlug T9); gra sama 14 wnioskow o pokoj w 9 dobach. Polnoc nadal 138/134 - przyczyna: bron skladana (np.
vlandia_lance_1_t3 = tier 5) poza zamiana 175 -> W2 w toku. ZATWIERDZONE OD TERAZ: Armoury 1cc7890a, CrashScribe d68532a6.
**WGRANIE 13 (09.10 ok. 17:35 zegara komp.):** tylko CrashScribe 6507c842 (w-toku/175-armie-cs 511c08e = W2: tier broni skladanych liczony tak jak przy starcie sesji - ok. 78 jednostek
t3-t4 bez kopii/wloczni t5-6; Polnoc 131/134 wobec swiata 122/119), Armoury bez zmian 1cc7890a. Test: 40 dob z Armoury w grze OK (14.2 s, 0 bledow, jednostek z bronia ponad
tier 0) + 120 dob z krokiem B (0 bledow). Poprzedni CS: CrashScribe.dll.bak-2026-10-09-przed-noc13 + D:. ZATWIERDZONE: Armoury 1cc7890a, CrashScribe 6507c842.
KROK B etapu 2 (w-toku/e2b) NIE wgrany - 12/48 progow NIE; naprawa wf_f6909cac-c64.
**DECYZJE JEFFA 09.10 ok. 17:40 (W2):** (1) wlocznie lore (Nieskalani, Qohor, Zlota Kompania, Manderly...) - BEZ wyjatku od tieru; (2) kopia do szarzy dopiero od tieru 5
(jazda t1-t4 bez kopii). Oba punkty zgodne z W2 w grze (wgranie 13) - bez zmian w kodzie.
**WGRANIE 14 (09.10 ok. 19:45 zegara komp.):** Armoury dff8a9b0 (w-toku/e2b b422a5f = sklad10 + etap 2 krok B: 110 zawor kasy zamku (zamek bez "zakupow" z niczego, platnosci
z nadwyzki ponad zapas), 112 utarg wsi bez znikania (zaplata lorda zostaje we wsi, sakwy taborow do zwyciezcy), 114 podzial 2/3 pan / 1/3 korona; B-1..B-4 poprawki, ksiega 169 bez
podwojnego liczenia sakw), CrashScribe bez zmian 6507c842. Testy: 120 dob nowej kampanii - glowy < 5000 = 9 (baza 19, prog <= 10 SPELNIONY), bankruci Banku 4 (baza 5),
wojsko w wojnie 115.4 tys.; 1 ERROR ValyrianBlades.Census (177, nie krok B - do naprawy); zapis 9 dob OK (25.9 s, 0 bledow). Poprzedni: Armoury.dll.bak-2026-10-09-przed-noc14
+ D:. ZATWIERDZONE: Armoury dff8a9b0, CrashScribe 6507c842. Baza Armoury: w-toku/e2b.

**WGRANIE 15 (09.10 ok. 21:35 zegara komp.):** Armoury ddf01297 (galaz noc/wgranie-15 = w-toku/e2c ae01f8e = WGRANIE 14 + 177-fix: stal valyrianska nigdy
do taboru partii AI - krewny z rodu, ktory ja udzwignie (glowa pierwsza), inaczej polka miasta rodu; przyczyna: Truth odlozony przy pojmaniu Tregara Ormollena do
taboru Leona Staegone znikal sekunde pozniej). CrashScribe bez zmian 6507c842. Autotest 40 dob nowej kampanii: 40/40, 14.0 s/dobe, Armoury 0 bledow (CS 8 = staly
szum startowy innych modow, jak w kazdym tescie), spis stali 17/17 klng. Kopie: Armoury.dll.bak-2026-10-09-przed-noc15 + D:\Backup-Bannerlord\wgrane\2026-10-09-noc15-przed.

**WGRANIE 16 (10.10 ok. 02:05 zegara komp.):** Armoury b43f65cf (galaz noc/wgranie-16 = w-toku/e2c 1220510 = WGRANIE 15 + ETAP 2 KROK C1 (165 korona z biezacych
wplywow, 182 dary Polnoc->Straz 25% i Wolne Miasta->Dothrakowie 15% + Straz bez zoldu, 166+162m budzet rodu AI + dwor z tarcza, 185 kontrakt najemnika AI) + poprawki
recenzji (4b7b11b) i wojska (385e7ec dwor ustepuje zoldowi w wojnie, limity partii z wolnego miejsca; 4e4e627 umowa najemnika >= polowa kompanii) + KROK C2 (180 renty
korony wedlug lenn z warunkiem zalogi i sluzby; 1220510 komunikat renty gracza raz na 7 dob)). CrashScribe bez zmian 6507c842. Testy: 120 dob nowej kampanii -
glowy < 5000 = 4, bankruci 0, wojsko lordow w wojnie 101.8 tys. (pas 95-115), zalogi +1%, reszta ksiegi -2.9 tys./d, 0 bledow; zapis 9 dob OK (31 s/d, 0 bledow).
Slabsze: Iron Islands -61%, Dothraki -39%, Ibben -32% (biedni z lore; dalej kredyt 168). Opis: docs/C1-POSTEP.md w galezi w-toku/e2c. Kopie: Armoury.dll.bak-2026-10-10-przed-noc16
+ D:\Backup-Bannerlord\wgrane\2026-10-10-noc16-przed. ZATWIERDZONE: Armoury b43f65cf, CrashScribe 6507c842. Baza Armoury: w-toku/e2c.

**WGRANIE 17 (10.10 ok. 05:40 zegara komp.):** Armoury c38bd62f (galaz noc/wgranie-17 = w-toku/e2c 0175e8d = WGRANIE 16 + ETAP 2 KROK C3: 179 rycerze bez lenna bez
wlasnych partii - glowa rodu gentry jedzie w druzynie pana/wodza armii, pan placi 24 zl/d (GentryService.cs; poprawki a5063af NotSpawned -> Active, 5ce5faf rod rycerza
nie placi zoldu cudzej partii), 183 dezercja AI wedlug poziomu (DesertionLawForAi, podloga 30 ludzi przy zaleglym zoldzie, takze gracz), 180m pomiar sluzby rent przez cala
dobe). CrashScribe bez zmian 6507c842. Testy: 120 dob nowej kampanii (powtorka po jednorazowym zawieszeniu silnika w dobie 43 przy porodzie roznych ras - tresc ROT,
do Mends) - 0 bledow Armoury, glowy < 5000 = 3, bankruci 0, wojsko lordow w wojnie 93.0 tys. (cel 95, podloga 85), zalogi -3.4%, dezercja AI 27/d (baza 49), reszta ksiegi
-1.3 tys./d; zapis 9 dob OK (0 bledow). Kopie: Armoury.dll.bak-2026-10-10-przed-noc17 + D:\Backup-Bannerlord\wgrane\2026-10-10-noc17-przed.
ZATWIERDZONE: Armoury c38bd62f, CrashScribe 6507c842. Baza Armoury: w-toku/e2c.

**DECYZJE JEFFA 10.10 (ok. 06:00):**
- **Odpoczynek AI (T10, dlug snu):** "musza odpoczywac; forsowny marsz najwyzej 2 dni i tylko w sytuacjach wyjatkowych". Czyli AI: nocny marsz tylko w wyjatkowych
  sytuacjach (ucieczka przed armia, przerwanie rabunku, poscig, odsiecz), najwyzej 2 doby z rzedu; potem obowiazkowy odpoczynek, az dlug snu zejdzie do 0
  (te same kary co gracz zostaja). Test 120 dob C3 pokazal 11-15 partii AI na dlugu snu 3 (morale -95%) przez ok. 2 tygodnie -> dezercja.
- **Olbrzymy:** "olbrzymy moga tylko z olbrzymami, ludzie z ludzmi; nie ma zadnej ciazy ani malzenstwa olbrzyma z czlowiekiem". Czyli: zakaz slubow i ciaz miedzy
  roznymi rasami (human / giant / wight / whitewalker), takze istniejace ciaze roznych ras - bez porodu (porod roznych ras zawiesil silnik w tescie C3c, doba 43).
- **Wezwanie sojusznika do wojny (Call to War):** "tak, korona" - oplate dla wezwanego sojusznika placi skarbiec korony z wplywow dnia (w kolejnosci wydatkow
  165), rody nic; gdy skarbca nie stac - sojusznik wychodzi z wojny (bez zlota z niczego i bez dlugu rodow wobec korony). Zamyka tez czesc "zaliczki gry" (OBIEG-1).
- **Dar dla Nocnej Strazy (182) - z calego Westeros** (Jeff 10.10: "na Nocna Straz zrzuca sie cale krolestwo, nie tylko Polnoc; w ksiazce utrzymuje ja krol i inne rody").
  Parametry (dobrane przeze mnie): Zelazny Tron (House Baratheon of King's Landing) 8%, Polnoc 8%, Dorne / Reach / Vale / Stormlands / Riverlands / Dragonstone po 3%
  wplywow dnia; Iron Islands, Free Folk i Essos 0. Razem ok. 3.6 tys./dobe (dzis 3.7 tys. z samej Polnocy 25%); Polnoc +ok. 2.7 tys./dobe na wlasny zwrot. Do zrobienia
  zaraz po kroku D (CrownGifts.cs + klucze udzialow w Settings), test razem z D.
- **Korona pozycza** (Jeff 10.10: "oczywiscie, ze korona powinna pozyczac"): nowa paczka po kroku D - skarbiec krolestwa moze pozyczyc w Zelaznym Banku na wojne
  (limit wedlug zdolnosci splaty z wplywow dnia, np. do rocznych wplywow), raty z wplywow dnia w kolejnosci wydatkow 165; przy zaleglosci Bank przestaje pozyczac
  i finansuje wrogow dluznika (lore: Stannis po niesplacaniu przez Cersei). Projekt paczki do napisania (nr wolny, np. 186), test 120 dob.
- **Dar dla Strazy - PROCENTY JEFFA (10.10, wiazace; zastepuja moje 8/8/3 i warianty A/B):** Polnoc 5%, Zelazny Tron (House Baratheon of King's Landing) 4%,
  Dorne 3%, Reach 3%, Vale 2%, Stormlands 2%, Riverlands 2%, Dragonstone 1% wplywow dnia korony; reszta 0. Razem ok. 2.35 tys./dobe (test c3c: Straz zarabia
  ok. 9 tys./dobe ponad wydatki - 1.32 -> 2.41 mln w 120 dob, wydaje glownie zold najemnikow ok. 3.9 tys./d; Polnoc +3.2 tys./d na zwrot). Do zrobienia po kroku D
  (CrownGifts.cs + klucze). Pytanie otwarte: dar tylko wtedy, gdy oszczednosci Strazy < zapas np. 60 dob wydatkow (moja propozycja) - czeka na slowo Jeffa.
- **Dar dla Strazy WEDLUG POTRZEBY - TAK (Jeff 10.10):** krolestwa placa swoje procenty (Polnoc 5, Zelazny Tron 4, Dorne 3, Reach 3, Vale 2, Stormlands 2, Riverlands 2,
  Dragonstone 1) tylko wtedy, gdy oszczednosci rodow Strazy spadna ponizej zapasu na 60 dob jej wydatkow (zold + dwor + zakupy, srednia 28 dob); powyzej - dar 0.
  Test c3c: budowy Strazy (Castle Black, Shadow Tower, Eastwatch) stoja 119 dob - moc budowy 0 (brak ludzi przy Murze). Pytanie otwarte: Straz placi za budowy
  swoim zlotem (robotnicy z najblizszych wsi/miast, zloto do ich kies) - czeka na slowo Jeffa.
- **Straz placi za budowy - TAK, ale "do budowy jeszcze wrocimy"** (Jeff 10.10): przy powrocie do budow (138 dniowki robotnikow, 152 BuildFunding) - Straz najmuje
  robotnikow z najblizszych wsi/miast (Mole's Town, wsie Darow), dniowki do ich kies wedlug 138; dzis budowy Muru stoja (moc 0 - brak ludzi przy Murze).
  Pozniej osobno: odbudowa i obsadzanie opuszczonych zamkow Muru za zloto Strazy (lore: Jon w "Tancu ze smokami"), jesli mapa ROT je ma.
- **SPROSTOWANIE (10.10, kod BuildFunding.cs):** budowy Strazy stoja nie z braku ludzi, tylko przez regule "w wojnie tylko budowy wojskowe" (BuildFunding :140-141,
  warNow && !IsMilitaryProject -> pomin) - Straz jest stale w wojnie, a w kolejce ma same cywilne (drogi, kwartal rzemieslnikow, spichlerz). Do zrobienia przy powrocie
  do budow: AI w wojnie przestawia kolejke na budowe wojskowa (jesli jest), krolestwa w wiecznej wojnie (Straz) buduja tez cywilne, Straz najmuje robotnikow z okolicy.

**WGRANIE 18 (10.10 ok. 07:25 zegara komp.):** Armoury aebd0e94 + **RealisticCaptivity 5f98df70** (galaz noc/wgranie-18 = w-toku/e2c baff3f2 = WGRANIE 17 + ETAP 2 KROK D:
168 drabina dlugu (kredyt / zaleglosc / zajecie / wyprzedaz zamiast bankructwa, kredyt wojenny KW w pulapie 166, DebtLadder.cs), wezwanie sojusznika do wojny placi
korona (CrownCallToWar.cs), 178 okupy wedlug majatku (Ransom178.cs + most w RealisticCaptivity; glowa 0.5 roku D, lord 60 D, krol 0.5 roku wplywow korony ze skarbca,
gotowka do 50% (G-5000), reszta dlug do 364 D, wielki jeniec - korona zdobywcy, 1/10 nagrody, prawo trzecich 1/9) + 182-W dar Strazy z Westeros wedlug potrzeby
(procenty Jeffa)). CrashScribe bez zmian 6507c842. Testy: 120 dob nowej kampanii - 0 bledow Armoury, glowy < 5000 = 1, bankruci 0, wojsko lordow w wojnie 96.1 tys.
(pas 95-115), zalogi -2.9%, reszta ksiegi -1.6 tys./d; zapis 9 dob OK (0 bledow; stary zapis: 55 rodow w zajeciu, 12 w wyprzedazy - stare dlugi Banku).
**Do decyzji Jeffa: wielkosc okupow 178** (65.5 mln w 120 dobach, dlug okupow 32 mln u 54 rodow, rosnie ok. 270 tys./d) - propozycja B: glowa 60 D, lord 20 D.
Kopie: *.bak-2026-10-10-przed-noc18 (Armoury i RealisticCaptivity) + D:\Backup-Bannerlord\wgrane\2026-10-10-noc18-przed.
ZATWIERDZONE: Armoury aebd0e94, RealisticCaptivity 5f98df70, CrashScribe 6507c842. Baza Armoury/RC: w-toku/e2c.

**WGRANIE 19 (10.10 ok. 08:40 zegara komp.):** Armoury aeb2ba51 (galaz noc/wgranie-19 = w-toku/e2c 432e8e9 = WGRANIE 18 + T10-R odpoczynek AI (max 2 noce forsownego
marszu, potem oboz do dlugu 0; wyjatek tylko ucieczka przed 2x silniejszym; wodz armii wedlug najgorszego dlugu armii; naprawa ksiegi snu - gubione ticki) + RaceLaw
(sluby i ciaze tylko w tej samej rasie; ciaza roznych ras bez porodu)). RealisticCaptivity 5f98df70 i CrashScribe 6507c842 bez zmian. Testy: 120 dob - 0 bledow Armoury,
brak porodu roznych ras, dlug snu 3 > 2 doby tylko przy ucieczce, wojsko lordow w wojnie 100.0 tys., zalogi +0.6%, dezercja AI 7/d (baza 49), glowy < 5000 = 4,
bankruci 0; zapis 9 dob OK. Kopie: Armoury.dll.bak-2026-10-10-przed-noc19 + D:\Backup-Bannerlord\wgrane\2026-10-10-noc19-przed.
ZATWIERDZONE: Armoury aeb2ba51, RealisticCaptivity 5f98df70, CrashScribe 6507c842. Baza: w-toku/e2c.
- **Okupy - wariant B (Jeff 10.10 "Okup - B"):** glowa rodu ok. 60 dni D (RansomHeadYears 0.165), lord 20 D, krol ok. 60 dni wplywow korony (ta sama miara - moja
  decyzja dla spojnosci). Commit bcb5fe1 na w-toku/e2c. Powod: test D - okupy 65.5 mln w 120 dobach, dlug okupow +270 tys./d (w grze niewola 4-5 x w roku na rod).

**WGRANIE 20 (10.10 ok. 10:30 zegara komp.):** Armoury d7222bfb (galaz noc/wgranie-20 = w-toku/e2c bcb5fe1 = WGRANIE 19 + 186 korona pozycza w Zelaznym Banku (CrownBorrow.cs)
+ 178-B okupy mniejsze (glowa ok. 60 D, lord 20 D, krol ok. 60 dni wplywow korony)). RealisticCaptivity 5f98df70 i CrashScribe 6507c842 bez zmian. Testy: 120 dob - 0 bledow,
glowy < 5000 = 2, bankruci 0, wojsko lordow w wojnie 95.8 tys., zalogi -2.2%, reszta ksiegi -3.9 tys./d; dlug okupow d120 4.3 mln u 16 rodow (bylo 32 mln u 54);
korony pozyczaja 15-23 tys./d u 5-10 krolestw; zapis 9 dob OK. Kopie: Armoury.dll.bak-2026-10-10-przed-noc20 + D:\Backup-Bannerlord\wgrane\2026-10-10-noc20-przed.
ZATWIERDZONE: Armoury d7222bfb, RealisticCaptivity 5f98df70, CrashScribe 6507c842. Baza: w-toku/e2c.
- **Okupy - wariant 1 (Jeff 10.10 "1 i rob test"; zastepuje B):** glowa rodu ok. 30 dni D (RansomHeadYears 0.0824), zwykly lord 10 D, krol 20 dni wplywow korony
  (nowy klucz RansomKingDays 20). Commit d341e7b na w-toku/e2c, DLL test\Armoury-e2c-okup1.dll (87b03e2b). Szacunek: okupy ok. 9.6 mln / 120 dob (B: 19.2, A: 65.5).

**WGRANIE 21 (10.10 ok. 13:50 zegara komp.):** Armoury 87b03e2b (galaz noc/wgranie-21 = w-toku/e2c d341e7b = WGRANIE 20 + okupy wariant 1: glowa ok. 30 D, lord 10 D,
krol 20 dni wplywow korony - RansomKingDays). RealisticCaptivity 5f98df70, CrashScribe 6507c842 bez zmian. Testy: bieg 2-letni dotarl do doby 457 (wywrotka BannerKings
ClaimantDemand.SetTexts NullReference w BKDiplomacyBehavior.OnDailyTick - nie nasz kod; okno ButterLib zawiesilo gre), w 457 dobach 1 potkniecie Armoury (Tent KeyNotFound,
obsluzone); zapis 9 dob OK. Kopie: Armoury.dll.bak-2026-10-10-przed-noc21 + D:\Backup-Bannerlord\wgrane\2026-10-10-noc21-przed.
ZATWIERDZONE: Armoury 87b03e2b, RealisticCaptivity 5f98df70, CrashScribe 6507c842.

**WGRANIE 22 (10.10 ok. 15:15 zegara komp.):** CrashScribe 77b11df5 (galaz noc/wgranie-22-cs = w-toku/175-armie-cs 37535a7 = CS 6507c842 + BkDemandSafe (finalizery na BK
ClaimantDemand.SetTexts, Demand.Fulfill, Demand.DoAiChoice - wywrotka biegu 2-letniego w dobie 457) + NameplateGhosts (ikony-duchy partii nad osadami)). Armoury 87b03e2b
i RealisticCaptivity 5f98df70 bez zmian. Test: 10 dob (latki wpiete 3/3 + ikony), bieg 218 dob z poprawka BK - 0 bledow Armoury. Wersja autotestowa CS:
SCRATCH(7016)\test\CrashScribe-at-w22.dll (= at-bk2, e90f192a). Kopie: CrashScribe.dll.bak-2026-10-10-przed-noc22 + D:\Backup-Bannerlord\wgrane\2026-10-10-noc22-przed.
ZATWIERDZONE: Armoury 87b03e2b, RealisticCaptivity 5f98df70, CrashScribe 77b11df5.

**WGRANIE 23 (10.10 ok. 16:20 zegara komp.; nowe konto, sesja ae222cb7):** Armoury ba2e13e8 (galaz noc/wgranie-23 = w-toku/e2c 10db68d = WGRANIE 21 + 183d diagnoza
morale AI (linia "Morale AI (183d)", tylko log) + (4) namioty bez KeyNotFound (VisualOf = GetVisualOfEntity gry) + (2) jeden pulap w ludziach calej Nocnej Strazy (werbunek,
nowe partie i zwolnienia wedlug puli; poprawki recenzji 2-r)). RealisticCaptivity 5f98df70 i CrashScribe 77b11df5 bez zmian. Testy: 120 dob nowej kampanii (na 0e21ea1, przed
poprawkami recenzji, ktore dotykaja tylko zwolnien i pulapu rodow Strazy) - 0 bledow Armoury, progi etapu 2: 54 TAK / 2 NIE (stale: harness kuriera 2b/2c, "pieniadz swiata bez
wojny") / 3 DECYZJA (te same co w WGRANIU 20: Dothraki/Iron Islands biedni z lore, pan samych zamkow 166 zl/d, regulator kas miast); wgrana wersja WGRANIE 20 w tym samym
narzedziu miala 5 NIE. Straz 1.9 -> 5.0-5.4 tys. (pulap 5-6.8 tys., werbunek wstrzymany 0; dotad 1.5-2 tys.), glowy < 5000 = 1, bankruci 0, wojsko lordow w wojnie 104 tys.,
dezercja AI 23/d (baza 49); zapis 9 dob na wersji po recenzji (ba2e13e8) OK - 0 bledow, 29.1 s/dobe. Kopie: Armoury.dll.bak-2026-10-10-przed-noc23 +
D:\Backup-Bannerlord\wgrane\2026-10-10-noc23-przed. ZATWIERDZONE: Armoury ba2e13e8, RealisticCaptivity 5f98df70, CrashScribe 77b11df5.

**WGRANIE 24 (10.10 ok. 17:55 zegara komp.):** Armoury f5be1156 (galaz noc/wgranie-24 = w-toku/e2c b9d6a12 = WGRANIE 23 + (1) dezercja AI: W1 kara zaopatrzenia BK
liczy dni braku (max 20/30/20; dotad w osadzie i w malej partii cala narosla potrzeba, nasza czapka 3/d zawyzala u duzych), W4a Inni osobno w 169c/183, W4b Inni
dezerterzy do niczego (nie do puli wyrzutkow -> band), W4c przepelnienie partii i zalog do domu (OverflowHome)). RC 5f98df70 i CS 77b11df5 bez zmian. Testy: bieg z zapisu
d180 (40 dob) wobec bazy - d180-207 80/d (baza 127), kar zaopatrzenia BK brak w skladnikach morale, przepelnienie do domu 25-32/d, do puli -10%; fala po jednej wielkiej
przegranej (Recent Events -65) zostaje - mechanika gry; 120 dob nowej kampanii - 0 bledow Armoury, dezercja AI 49 (baza 49), wojsko lordow w wojnie 99.2 tys., bandy -14%,
glowy < 5000 = 3, bankruci 0, reszta ksiegi -6.2 tys.; NIE tylko te co w WGRANIU 20 (wahania); zapis 9 dob OK (0 bledow, 28.8 s/dobe). Kopie: Armoury.dll.bak-2026-10-10-przed-noc24
+ D:\Backup-Bannerlord\wgrane\2026-10-10-noc24-przed. ZATWIERDZONE: Armoury f5be1156, RealisticCaptivity 5f98df70, CrashScribe 77b11df5.

**WGRANIE 25 (10.10 ok. 18:40 zegara komp.):** Armoury 1211d984 (galaz noc/wgranie-25 = w-toku/e3-32m f37d032 = WGRANIE 24 + ETAP 3.2: cala kiesa rozbitego taboru wsi,
karawany i bandy dla zwyciezcow wedlug wkladu (dotad gra 10% / 50%, reszta w nicosc), jedna regula gracz/AI/bandy, korona 1/9 od wodzow-wasali, takze poddanie karawany
graczowi; BeatenPurse.cs, wylacznik BeatenPurseToVictor). RC 5f98df70, CS 77b11df5 bez zmian. Testy: 120 dob - 0 bledow, bandy w normie (liczba -16%, ludzie -13%, zloto u
pasera +11% przy limicie +20%, kiesy band +38% - skutek decyzji D), dezercja AI 10/d, wojsko lordow w wojnie 100.8 tys., glowy < 5000 = 0, bankruci 0, reszta ksiegi -2.1 tys./d,
kiesy rozbitych karawan i taborow znikajace z mapy 0; zapis 9 dob OK (0 bledow, 26.8 s/dobe). Kopie: Armoury.dll.bak-2026-10-10-przed-noc25 + D:\...\2026-10-10-noc25-przed.
Pytanie (drobne) do Jeffa: kiesa bandy, ktora przylacza sie do gracza - dzis znika (jak przed 3.2). ZATWIERDZONE: Armoury 1211d984, RC 5f98df70, CS 77b11df5.

**WGRANIE 26 (10.10 ok. 19:26 zegara komp.):** Armoury d38f1e69 + **RealisticCaptivity cea72cca** (galaz noc/wgranie-26 = w-toku/e3-gA f205ba0 = WGRANIE 25 + ETAP 3.4-3.7:
3.4 BK bez zlota z niczego/w nicosc (zaopatrzenie partii wedlug zapasu partii placone kasie osady, rynek wsi placi tylko co ma, niewolnikow placi kasa osady, rynsztunek
rycerza BKROT do kasy targu; BkSupplyMarket.cs, BkSeal34.cs), 3.5 rada BK - pensje od dochodu, ktory pan ma, do kazdego radnego (CouncilWages35.cs), 3.6 wedrowcy BK bez
towaru z niczego (TravellerGoods36.cs), 3.7 ROT: podmiana rekruta bez zwrotu z niczego, 50 000 przywodcy rebelii z kasy osady, lord w lupie zaciagu wedlug ceny gry
(RotLeaks37.cs + RC)). CS 77b11df5 bez zmian. Testy: 120 dob - 0 bledow Armoury i RC, wojsko lordow w wojnie 101.1 tys., dezercja AI 14/d, glowy < 5000 = 2, bankruci 0,
bandy -12%, reszta ksiegi wobec bazy -9.2 tys. (TAK; bezwzglednie -20.8 tys./d - do domkniecia przez E3/0); zapis 9 dob OK (0 bledow, 31.4 s/dobe). Kopie: *.bak-2026-10-10-przed-noc26
(Armoury i RC) + D:\Backup-Bannerlord\wgrane\2026-10-10-noc26-przed. Pytania (kierunkowe, bez pilnosci): napitki i drewno wojska placi lord (tak dzis) czy sakiewka ludzi?;
wedrowcy BK kiedys z prawdziwym towarem?. ZATWIERDZONE: Armoury d38f1e69, RealisticCaptivity cea72cca, CrashScribe 77b11df5. GrandTourney 1337433c.

**WGRANIE 27 (10.10 ok. 20:12 zegara komp.):** Armoury 5e02c741 + RealisticCaptivity 90665425 + **GrandTourney bbb841d7** (galaz noc/wgranie-27 = w-toku/e3-gB b052638 =
WGRANIE 26 + ETAP 3.8-3.10: 3.8 ceny historyczne - spis stalych kwot (SPIS-CEN-GRY 2.0), pulap zamowien kowala i nagrody szranek GrandTourney w starej monecie, zloty smok
= 80 zl (tylko porownania w logu); 3.9 drobne w nicosc - trzecia lorda 33% (byla ok. 10%), lapowka w niewoli do trzymajacych (parol - dopiero przy "I run"), rynsztunek
zabrany przez bandytow do jukow bandy (nie na targ za darmo), "bandyci ukradli" w Spoils tylko z banda w zasiegu; 3.10 drobne z niczego - furaz z zapasu wsi, herszt
kryjowki z ludzi bandy (bez wyrzutkow - bez herszta), zold zaciagu ROT od pana). CS 77b11df5 bez zmian. Testy: 120 dob - 0 bledow Armoury, RC i GT, wojsko lordow w wojnie
101.2 tys., dezercja AI 31/d, glowy < 5000 = 1, bankruci 0, bandy -11%, reszta ksiegi wobec bazy -4.3 tys.; zapis 9 dob OK (0 bledow, 29.5 s/dobe). Kopie: *.bak-2026-10-10-przed-noc27
(Armoury, RC, GT) + D:\Backup-Bannerlord\wgrane\2026-10-10-noc27-przed. ZATWIERDZONE: Armoury 5e02c741, RC 90665425, GT bbb841d7, CS 77b11df5.

**WGRANIE 28 (10.10 ok. 20:58 zegara komp.):** Armoury 6e4b5b6a (galaz noc/wgranie-28 = w-toku/e3-31m dc290b3 = WGRANIE 27 + ETAP 3.1 + 3.3 wedlug projektu
SCRATCH(ae222cb7)\e3\projekt31\PROJEKT-ETAP3-SZCZELNOSC-STATKI-2026-10-10.md (v2 po krytyce, 2 recenzje kodu): E3/0 pomiar (portfele wezwan, U8r, statki, Inni),
E3/1 prawdziwy platnik (jency, lup z oblezenia, turnieje, myto - kasa osady z nadwyzki), E3/2 majatki BK tylko z utargu, E3/3 lup z cial z sakiewek pokonanych,
E3/4 zold karawan do sakiewek ich ludzi, E3/5 awanse do sakiewki, E3/6 werbunek - dokladny zwrot, E3/7 kapital karawan z zaplaty (DTE 0), E3/8a-c podatek warsztatow
notabli do pana, oplaty BK do odbiorcow, zloto zmarlych w osadzie, E3/9 zysk karawany pana w kazdym miescie krolestwa; 3.3 statki (E3/10-12) w kodzie, WYLACZNIKI OFF
(ShipsToPort, ShipTradeThroughPort, ShipPrizesToPort) do decyzji Jeffa - wariant (b): razem z podatkiem wojennym etapu 5; E3/8d (Inni bez zoldu) odlozony). RC 90665425,
GT bbb841d7, CS 77b11df5 bez zmian. Testy: 120 dob - 0 bledow; PROGI ETAPU 3 20/20 TAK: reszta niewyjasniona ksiegi (srednia 28 dob) -2.6 tys. w dobie 40 i +0.3 tys.
w dobie 120 (cel < 10 tys.; WGRANIE 21: ok. -19 tys.), platnik/awanse/kapital/werbunek bez odbiorcy 0, miasto ponizej zapasu 0; etap 2: wojsko lordow w wojnie 99.8 tys.,
glowy < 5000 = 2, bankruci 0, dezercja AI 46/d, bandy -13% (liczba -14%, awanse u pasera -25%, rozbite tabory -29%; zloto band u pasera +35% - NIE wobec progu +20%,
kiesy band +60% - skutek decyzji D i lupu z cial; obserwowac w biegu 2-letnim); regulator kas miast kasuje +160 tys./d wiecej (miasta dostaja zloto, ktore dotad znikalo -
etap 5: 5.1); zapis 9 dob OK (0 bledow, 28.6 s/dobe). Kopie: Armoury.dll.bak-2026-10-10-przed-noc28 + D:\...\2026-10-10-noc28-przed.
Pytania do Jeffa (3.1): podatek BK od warsztatow lordow/gracza w cudzych miastach - zniknac (dzis) czy wlasciciel placi; doplata podmiany rekruta ROT do sakiewki ludzi;
werdykt 3.2 "pokonany" takze dla partii lordow. ZATWIERDZONE: Armoury 6e4b5b6a, RC 90665425, GT bbb841d7, CS 77b11df5.
**BIEG 2-LETNI (728 dob, -TimeoutMin 480) start ok. 20:58** na wgranym zestawie -> SCRATCH(ae222cb7)\test\at-728.out.txt (koniec ok. 01:30-02:00).

**BIEG 2-LETNI (10/11.10, 20:58-01:23, WGRANIE 28):** 728/728 dob, 0 bledow Armoury (CS: 2 bledy BKROTPatch - GetCouncil AmbiguousMatch, zlapane). Etap 3: 20/20 TAK, reszta
niewyjasniona ksiegi d728 -0.7 tys./d. Glowy < 5000: d120 0, d364 6, d728 3; bankrut 1 (K39 d225); Bank min 2.65 mln; dezercja AI ost. 28 dob 15/d (baza 49) - w oknach
28 dob 40-200/d (fale po wielkich przegranych); wojsko lordow w wojnie rok 1 ok. 100 tys., rok 2 mniej wojen (145 rodow w pokoju d560) - 72 tys. razem, na krolestwo w wojnie
jak w bazie; zalogi swiata -23% (pokoj). BANDY +36% liczby, +57% ludzi (25 tys., plateau od d550) - zrodlo: rozbici z bitew "w las" (H3) i fale dezercji, powrot z puli
do wsi 2-3/d -> E4/3. STRAZ: ok. d590 pokoj z Wolnymi Ludzmi -> pulap 20 tys. -> 1.4 tys., zwolnienia 166 odeslaly 5 tys. (7.2 -> 1.2 tys.) -> poprawka 2-n (Straz bez
zwolnien, w-toku/e4-0 f41402a). Logi: SCRATCH(ae222cb7)\kopia-728\ (progi2.txt, progi3.txt).
**Raport dla Jeffa:** docs/RAPORT-NOCNY-2026-10-11.md. Projekty: docs/etap3/, docs/etap4/.

**WGRANIE 29 (11.10 ok. 02:16 zegara komp.):** Armoury 809a603a (galaz noc/wgranie-29 = w-toku/e4-0 f41402a = WGRANIE 28 + ETAP 4 paczka E4/0 (188, pomiar ludzi, jencow
i zimy - gra bez zmian; Measure40.cs, linie Hearth/Dobrobyt/Jency/Zima/Partie/Krainy/Straz i Wolni Ludzie; progi: tools/sprawdz_logi.py --grupa etap4) + 2-n Nocna Straz
bez zwolnien (WatchNoReleases)). RC 90665425, GT bbb841d7, CS 77b11df5 bez zmian. Testy: 120 dob - 0 bledow, koszt pomiaru 10.7 ms/dobe, tempo 17.4 s/dobe (bez zmian),
etap 2 jak w WGRANIU 28 (wojsko lordow w wojnie 101 tys., glowy 1, bankruci 0, bandy -10%), Straz 5.6 tys. / pulap 10.2 tys.; pomiar E4/0 pokazal dziury do etapu 4:
hearth wsi +624/d bez wyjasnienia (T2 - przyrost ludnosci z niczego -> E4/4), ludzie pod bronia +1048/d bez nazwy (T7 -> E4/7-E4/8), Straz/Wolni Ludzie 60-67% ruchu bez
nazwy (T6); zapis 9 dob OK (0 bledow, 28.5 s/dobe). Kopie: Armoury.dll.bak-2026-10-11-przed-noc29 + D:\Backup-Bannerlord\wgrane\2026-10-11-noc29-przed.
ZATWIERDZONE: Armoury 809a603a, RealisticCaptivity 90665425, GrandTourney bbb841d7, CrashScribe 77b11df5. NIEWGRANE: E4/2 (w-toku/e4-2 3e10b11) - decyzja Jeffa.

**DECYZJE JEFFA 11.10 (ok. 02:45, odpowiedzi na raport nocny):**
- (1) Statki 3.3 - RAZEM Z ETAPEM 5 (podatek wojenny 184): wylaczniki ShipsToPort/ShipTradeThroughPort/ShipPrizesToPort zostaja OFF do etapu 5.
- (2) Fale dezercji po wielkich przegranych - Jeff pyta o liczby (odpowiedz w czacie: bieg 728 dob - 51/d sredniej, 46% w 68 dniach fal; najwiekszy epizod 2.9 tys. w 8 dni
  = 2.5% wojska lordow; pobita druzyna traci ok. 1/4-1/3 skladu w 4-8 dni) - czeka na decyzje.
- (3) Inni tylko z poleglych (E4/2) - czekamy na test informacyjny (at-e42-120).
- (4) Etap 4: Sarnor 100 tys. (jak ROT); kraina bez wolnych rak nie wystawia nowego wojska do odrobienia strat - TAK; spalona wioska wraca rok od ostatniego najazdu - TAK;
  ludnosc w ciaglej wojnie moze spadac (do -0.5%/rok) - TAK.
- (5) Kiesa bandy przylaczajacej sie do gracza - do gracza (TAK); podatek BK od warsztatow lordow/gracza w cudzych miastach - placi wlasciciel panu miasta (TAK);
  napitki i drewno wojska - placi WOJSKO (sakiewki zolnierzy, jak odziez 150); wedrowcy BK z prawdziwym towarem - TAK; doplata podmiany rekruta ROT i werdykt
  "pokonany" dla druzyn lordow - Jeff prosi o wyjasnienie.
- (02:50) (2) Kara morale za przegrana bitwe - O POLOWE (Jeff: "zmniejsz kare o polowe"; fala po klesce ma zabierac ok. 1/8-1/6 skladu zamiast 1/4-1/3);
  (5) doplata za podmiane rekruta ROT - do sakiewki zolnierzy tej druzyny (jak awanse E3/5) - TAK; sakiewka zniszczonej druzyny lorda tylko dla prawdziwego zwyciezcy bitwy
  (werdykt 3.2 takze dla druzyn lordow; bez zwyciezcy - kasa najblizszego miasta) - TAK.
  Paczka "D11" (decyzje 11.10): kara za przegrana /2, kiesa bandy przylaczajacej sie -> gracz, podatek warsztatow lordow/gracza placi wlasciciel panu miasta, napitki i
  drewno wojska z sakiewek zolnierzy, doplata ROT do sakiewek, werdykt dla lordow. Osobno pozniej: wedrowcy BK z prawdziwym towarem (wiekszy krok).
- (03:00) Jeff: "wgraj" - E4/2 (Inni tylko z poleglych) WGRAC razem z D11 (wspolny test 120 + zapis -> WGRANIE 30). Kolejnosc dalej: E4/3 (punkty powrotu - bandy) ->
  E4/4 (przyrost ludnosci) -> E4/1 zima (wymaga zgody/polecenia Jeffa na zmiane 4 kluczy klimatu w plikach gry na czas testu - w ROT jesien trwa > 400 dni, w biegu
  2-letnim zima nie przyszla) -> E4/5..E4/11 + wedrowcy BK z prawdziwym towarem.
- (03:05) **Jeff - ZASADA TESTOW:** "najpierw wgrywaj wieksze paczki zmian i potem testy w grze, bo te testy zabieraja za duzo czasu; jak je uruchamiasz, to zeby testowac
  duzo zmian". Od teraz: kod kilku paczek (kazda z recenzja, wylacznikiem MCM, osobnym commitem i wpisem CHANGELOG) -> jedna galaz -> JEDEN test 120 dob + zapis -> jedno
  wgranie. Najblizszy zbiorczy test: D11 + E4/2 + E4/3 + E4/4.
- (03:30) **Jeff o bandach (E4/3):** "maja rosnac z wojna - na tym polegaja bandy, ze jak jest wojna, jest duzo bandytow - to musi byc wyposrodkowane". Regula dla E4/3
  (powrot wyrzutkow do domu): tempo powrotu ZALEZY OD WOJNY w regionie - w wojnie wolno (bandy rosna z rozbitymi i dezerterami), w pokoju szybko (amnestia, zniwa, praca
  w domu) - bandy w dlugiej wojnie ok. 1.3-1.5 x poziomu pokoju (nie bez konca, plateau), po zakonczeniu wojen wracaja do poziomu pokoju w ok. pol roku (120-180 dob).
  Prog S16 "bandy <= baza + 20%" zastapic dla E4/3: plateau w wojnie <= 1.5 x pokoj, spadek po pokoju. Poprawka po workflow E4/3 (wf_dedf899d-56d), przed testem zbiorczym.
- (05:00) **Jeff: model jedzenia RBL - NAPRAWIC** ("tak naprawic"): RealisticBannerlord (RealisticFoodConsumptionModel, laduje sie po ROT/BK, dziedziczy po Default i nie
  wola modelu wewnetrznego) przejmuje caly lancuch jedzenia partii - nie dzialaja ROT "Inni nie jedza", NavalDLC -20% na morzu, BEE, pasza i perki BK. Naprawa: RBL liczy swoje
  (pory roku, racje), a reguly ROT/NavalDLC/BEE/BK dokladaja sie do wyniku (jeden lancuch). Termin: z E4/6 (spichlerz i glod) w etapie 4. Uwaga: pasze koni zima liczy dzis
  E4/1 (WinterRealm) - przy naprawie lancucha nie liczyc paszy BK drugi raz.

**WGRANIE 30 (11.10 ok. 06:05 zegara komp.; pierwszy test zbiorczy wedlug zasady Jeffa):** Armoury 9d314ea6 (galaz noc/wgranie-30 = w-toku/zbior30 c02c80f = WGRANIE 29 +
D11 (kara morale za przegrana /2, kiesa bandy przylaczajacej sie -> gracz, podatek warsztatow lordow/gracza placi wlasciciel panu miasta, zaopatrzenie BK z sakiewek
zolnierzy, doplata podmiany ROT do sakiewek, sakiewki druzyn lordow tylko dla zwyciezcy) + E4/2 (Inni tylko z poleglych, PeopleBook CIALA) + E4/3 (jednostka 1/k na osade,
punkty powrotu, rozwiazane tabory wracaja, tabela krain: Wyspy Letnie ~0.11 mln, Wolni Ludzie 300 tys., Sarnor 100 tys.; powrot wyrzutkow do domu - wojna 1/120, pokoj 1/30
puli na dobe) + E4/1 (zima wedlug krain: Wyspy Letnie 0, cieple 0.4, reszta 1; konie jedza zima; tryb probnej zimy z plikiem-przelacznikiem w autotescie) + 36b (wedrowcy BK
z prawdziwym towarem)). RC 90665425, GT bbb841d7, CS 77b11df5 bez zmian. Testy: A 120 dob - 0 bledow, etap 3 20/20, wojsko lordow 100.6 tys., glowy 0, bankruci 0, dezercja AI
6/d, bandy -16%; B 120 dob z proba zimy - zima od 8. doby, ciecie plonu Wyspy Letnie 0% / cieple 16% / reszta 49%, zamki z ujemnym bilansem 1 z 130, pusty spichlerz 0,
glodne druzyny lordow ok. 29 z 680; zapis 9 dob OK (0 bledow, 27.8 s/dobe). Kopie: Armoury.dll.bak-2026-10-11-przed-noc30 + D:\Backup-Bannerlord\wgrane\2026-10-11-noc30-przed.
ZATWIERDZONE: Armoury 9d314ea6, RealisticCaptivity 90665425, GrandTourney bbb841d7, CrashScribe 77b11df5.
- (06:50) **Jeff: pasza koni - wariant (a):** jedna zasada przez caly rok - kazdy kon po glowie (funkcja AnimalNeed z E4/1); formula BK CalculateAnimalFoodNeed (1 kon na typ
  oddzialu) zastapiona nasza liczba zwierzat przy warunkach terenu BK (pustynia x1, snieg x0.5); zima E4/1 dolicza roznice. Kod w toku na w-toku/e4-jedzenie (agent w tle).
- (09:30) **Jeff: pytania z planow E4/8 i E4/10 - "tak, tak, tak, tak" (wszystkie wedlug rady):** E4/8 P1 straz karawan bez hamulca wolnych rak (z ludzi miasta startu);
  P2 miasta dopelniane ze wsi regionu do udzialu miejskiego w ok. rok (`TownMigrationRefillDays` 364), nie tylko o ubytek naturalny; P3 nowa druzyna lorda - najpierw wolne rece
  krainy, potem zaloga siedziby, bez obu mniejsza; E4/10 pyt. 1 - rekrut tier 1-2 po zwolnieniu zawsze do domu, do karczmy tylko od tieru 3 (`VeteranMinTier` 3).
  (E4/10 pyt. 2 - sprzet zwolnionego ze zbrojowni pana - osobny krok po etapie 4, bez zmian.)
- (11:05) **Jeff: pytania planu E4/9 - "tak, tak":** zalogi Strazy dalej rosna same, ale z prawdziwych ludzi Westeros (Mur nie slabnie); rozbici bracia wracaja
  na Mur, uciekinierzy do puli wyrzutkow Polnocy.
- (12:40) **Jeff: pytania E4/6 i E4/7 - "tak, tak, tak":** (1) uchodzcy z glodu we wlasnym regionie wracaja zawsze, gdy gospodarze nie gloduja (odstepstwo
  od GL 2.6 - przyjete); (2) najemnik z karczmy z ludzi miasta, obcy tylko w krainie bez wolnych rak (do E4/10); (3) niewzieci z poboru wymuszonego gracza WRACAJA
  do DOM swojej wsi (zamiast znikac pod nazwa) - zmiana kodu E4/7 (commit na w-toku/e4-7, potem do zbior-e4-koniec).
- (13:50-14:00) **Jeff E4/11:** wyglad spalonej wioski - etap 7 (7.10); niebezpieczenstwo z pustych osad - po tescie koncowym E4 (PLAN-DO-KONCA 4.20);
  ryby/mlyny wiosek - etap 6 (6.18). (14:45) port Muru - naprawic (B6c). (15:50) **Etap 5: test zbioru A OSOBNO, zaraz po tescie koncowym etapu 4** (E5/0 pomiar
  jedzie w tescie E4, bo nic nie zmienia w grze).
- (16:10) **Jeff: etap 5 - (1) A1 bez dosypki kas od razu (regulator tryb 2, miasta i zamki; tryb 1 jako opcja MCM) - "usun to ostatnie zrodlo z niczego";
  (2) E5/0 liczy udzial zysku kupcow zabierany przez pana i korone (porownanie z historycznym ok. 10%).** Opis w projekcie etapu 5, rozdz. 6.
- (16:20) **Jeff: A6 (lup z bitew tylko z kiesy pokonanego) od razu w porcji A, bezwarunkowo.**
- (16:55) **Jeff: E4/8 K8 - szybszy platny werbunek zalog (2/d zamiast 1, w granicy zoldu, z ludzi krainy, osobny klucz) PRZED testem - TAK; E4/10 - weterani
  z rozwiazanych partii zostaja w ludnosci BK miasta (zmienic tylko gdy V13 > +2%/rok) - TAK.**
- (17:30) **Jeff etap 6: 6.1 wytop przy kopalni + przetop - TAK; 6.14 rycerz z wlasnym koniem, jazda AI ok. 20% (Dothrakowie konni, Zelazne Wyspy malo) - TAK;
  NOWE 6.19: kowale i rzemieslnicy kuja pancerze wedlug kultury swojej krainy (Polnoc nie kuje zbroi Lannisterow, nikt nie kuje unikatow typu helm Ramsaya; kazda kraina
  swoj rodzaj zbroi - lore). Uwaga: kultury ROT (battania = Polnoc itd. - CLAUDE.md 2).**
- (00:00 UTC 12.10) **Jeff etap 6 (pytania projektu v2): (1) zakaz kucia unikatow tylko dla kowali miast i AI, NIE dla kuzni gracza - TAK; (2) mieszczanie kupuja tylko
  konie robocze w miejsce padlych ze starosci - TAK; (3) udzial jazdy krainy z szablonow armii 175 (jedna liczba; 12-15% w 120 dobach, ok. 20% po roku) - OK.**
- (00:20 UTC 12.10) **DECYZJE JEFFA (pytania po tescie E4 i z etapu 5):**
  (1) "ludnosc" w decyzji 11.10 = WSZYSCY ZYWI (dom + sluzba + jency + niewolni + Mur + weterani + uchodzcy);
  (2) BEZ sztucznego hamulca strat (P1-5 ODRZUCONE): "jak ma duze straty, to ma duze straty, ekonomia na tym cierpi - taka zasada wojny; nie robmy sztucznych praw;
      jesli ubywa, to ubywa, jesli przybywa, to przybywa" -> prog -0.5%/rok tylko INFO (pomiar), bez zmian w grze; bez korekty smiertelnosci miast;
  (3) pobor z miast wedlug udzialu miasta w ludnosci krainy (P1-4) - TAK;
  (4) przywrocic 3 atuty BK (CommanderWarband +25% wplywu, MercenaryFamousSellswords +20% slawy, Cataphract +12% slawy) latka w CrashScribe - TAK;
  (5) A2b: zwrot wlasnych pieniedzy nie jako dochod z ziemi - POPRAWIAMY, "najwyzej wojsko spadnie, trudno";
  (6) A4: podatek korony od max(20 000, zapas) - ZOSTAWIC;
  (7) A5 Z8 - Jeff prosi o wyjasnienie;
  (8) A1: ubogie miasta bez zakupow zbroi - ZOSTAWIC ("tak dziala ekonomia, niech miasta stana sie bogate");
  (9) ujscia: BANDA ZABIERA WSZYSTKO (cale zloto i rynsztunek gracza - "zostawia mnie w lachmanach") do swojej kiesy/jukow (nie w nicosc); oplata za werbunek zalog
      NIE MOZE znikac (do sprzedajacego - notabla/osady).
  (00:25 UTC) (7) A5 Z8 - POPRAWIC razem z (5): zwrot wlasnych pieniedzy gracza/lorda nie obniza dopłaty korony do zoldu zalog; (9) doprecyzowanie: "tylko czesc,
  lord nie wozi calego majatku" - gracz: caly rynsztunek + % zlota z RealisticCaptivity (60, ustawienie Jeffa) DO KIESY/JUKOW BANDY (nie w nicosc); lord AI: czesc
  (A6: 5% sakiewki, max 10 000) do zwyciezcow; skarbiec rodu zostaje. (Jeff moze podac jedna liczbe "sakiewki podroznej" dla wszystkich.)
  (00:30 UTC) **Jeff: "MI banda zabiera wszystko zloto tez!"** - GRACZ pojmany przez BANDE traci CALE zloto i caly rynsztunek -> do kiesy i jukow bandy (nowy klucz,
  np. BanditsTakeAllFromPlayer = true, niezaleznie od % w RealisticCaptivity.settings.xml); inni pojmujacy (lordowie, milicja) - % z RC jak dotad, do pojmujacego/kasy osady;
  lord AI pojmany - czesc (A6). Rozbicie tej bandy oddaje zloto i rynsztunek (BeatenPurse 3.2 + juki 3.8).
  (00:35 UTC) **Jeff: "lord i milicja tez mi wszystko zabiera"** - GRACZ pojmany przez KOGOKOLWIEK (banda, lord, milicja) traci CALE zloto i caly rynsztunek -> do pojmujacego
  (kiesa bandy / lorda; milicja i osada bez bohatera - kasa osady). Lord AI pojmany - tylko czesc (A6, "lord nie wozi calego majatku").
- (00:35 UTC) **Jeff: ZASADA TESTOW (zmiana): "testy roczne/2-letnie za dlugo trwaja; najpierw kod, potem testujemy, a nie co chwila testy 4-godzinne"** ->
  kodujemy etapy 4 (poprawki) + 5 (A, B, decyzje 5/7/9) + 6 (ile sie da), potem JEDEN krotki zestaw: 120 dob nowej kampanii + zima 120 + zapis 9 na calosci;
  poprawki; wgranie. Dlugi bieg (364/728) tylko raz na samym koncu (najlepiej, gdy Jeff nie czeka).

**WGRANIE 31 (11.10 ok. 10:55 zegara komp.; drugi test zbiorczy):** Armoury cf40fefa (galaz noc/wgranie-31 = w-toku/zbior31 dfb12df = WGRANIE 30 +
E4/4 (przyrost naturalny wsi g = 0.5 + 0.6 x dobrobyt - 0.6 x niebezpieczenstwo, korytarz -1.3..+0.8 %/rok zamiast stalej gry; miasta jako stan; podloga 10 hearth z puli
wyrzutkow; poprawka podwojnych wsi w Settlement.All - koniec RESZTY +677 hearth/d) + E4/5 (spustoszenie: najazd zabija/wypedza ludzi wsi do konta uchodzcow, powrot rok od
ostatniego najazdu, plon od uchodzcow) + lancuch jedzenia (RBL nie przejmuje calego lancucha - ROT "Inni nie jedza", NavalDLC, BEE, BK znow dzialaja) + pasza koni po glowie
(wariant a)). RC 90665425, GT bbb841d7, CS 77b11df5 bez zmian. Testy: A 120 dob nowej kampanii - bledy 9 = te same co zawsze (7 cudzych, 2 nasze nieszkodliwe -
PRZEKAZANIE 09:35), Armoury.log 0 ERROR, etap3 20/20, wojsko lordow d120 110.4 tys., zalogi 81.2 tys., bandy 12.7 tys., przyrost +0.5%/rok; B 120 dob z proba zimy -
zima od 8. doby, lordowie w glodzie 20 (z30 29), bez jedzenia 16 (z30 26); zapis 9 dob (autotest-161-kawalki) - Armoury 0 ERROR, 30.6 s/dobe. Kopie logow
SCRATCH(ae222cb7)\kopia-z31-A, -B, -zapis. Kopie DLL: Armoury.dll.bak-2026-10-11-przed-noc31 + D:\Backup-Bannerlord\wgrane\2026-10-11-noc31-przed (9d314ea6).
ZATWIERDZONE: Armoury cf40fefa, RealisticCaptivity 90665425, GrandTourney bbb841d7, CrashScribe 77b11df5.

**WGRANIE 32 (12.10 ok. 06:30 UTC; noc 11/12.10 - etap 4 caly, etap 5 caly (A+B+dokonczenia+audyt S), etap 6 waski):** Armoury 29fdca1e (galaz noc/wgranie-32 =
w-toku/zbior-final 40b975e), CrashScribe 842f64a4 (noc/wgranie-32-cs = w-toku/cs-bledy 3b33863: B6/B6b/B6c Harrenhal i Mur, klucz powtorek, 3 atuty BK, strawa Polnocy),
RealisticCaptivity 943ea0d3 (pojmujacy zabiera graczowi cale zloto i rynsztunek do siebie). GrandTourney bbb841d7 bez zmian. Testy (autotest, ta sama paczka poza ostatnia
kalibracja korony): 120 dob nowej kampanii - Armoury 0 ERROR, 17.3 s/dobe; zapis 9 dob z konca biegu (caly nowy stan) - OK; zima 120 - OK, spichlerze F6/F7 TAK. Wojsko lordow
w wojnie sr28 88.4 tys. (podloga 85, cel 95; po samym E4 106 tys. - spadek z etapu 5, czesc zaakceptowana przez Jeffa: A2b "trudno"). Pozostale NIE - kalibracja (opis w
PRZEKAZANIE 12.10 06:30 UTC). Kopie: *.dll.bak-2026-10-12-przed-noc32 + D:\Backup-Bannerlord\wgrane\2026-10-12-noc32-przed (cf40fefa, 77b11df5, 90665425).
ZATWIERDZONE: Armoury 29fdca1e, RealisticCaptivity 943ea0d3, GrandTourney bbb841d7, CrashScribe 842f64a4.

- (09:20 UTC 12.10) **Jeff: wojsko - 1.b WYZSZY PODATEK** (danina korony od nadwyzki kies wsi LaySubsidyVillageShare 0.015 -> 0.025; korona oddaje rentami na wojsko). Bez pomostu (a). Do kodu (zmiana domyslnej, klucza nie ma w Armoury.json Jeffa) w nastepnej paczce razem z E6 porcja 2, test wspolny.
- (09:30 UTC 12.10) **Jeff (raport, pytania 2-5):** (2) prog rownania krain "0.1% albo 200 ludzi, co wieksze" - OK; (3) pojmujacy zabiera graczowi takze CALE juki (towar,
  jedzenie) - TAK (dzis 75% z RC); (4) zaulki w oblezonym miescie wstrzymane jak inne oplaty - TAK, jedna zasada; (5) ubogie miasta - sprawdzimy w biegu rocznym; (6) Jeff prosi
  o wyjasnienie. Do kodu w nastepnej paczce (z E6 porcja 2 i danina wsi 2.5%).
- (09:35 UTC 12.10) **Jeff (6): czujnik zbyt szybkiego przyrostu ludnosci (+1150 ludzi/d) w narzedziu zostaje - TAK** (tylko kontrola w tescie, gra bez zmian).
- (09:50 UTC 12.10) **Jeff - ZASADA: "nie mamy na sile i sztucznie robic dobrze lordom - jak ich nie stac na wojne, to zaciagaja pozyczke albo wystawiaja mniejsze zaciagi"**.
  -> spadek wojska z prawdziwej ekonomii (koniec zlota z niczego) jest dopuszczalny; podloga 85 tys. z etapu 2 przestaje byc warunkiem (tylko pomiar); nie dokladamy sztucznych
  doplat (np. sprzet A2 w wojnie zostaje bez zmian); danina wsi 2.5% (decyzja 1b) zostaje - to prawdziwy podatek. Rody: drabina dlugu / Bank / mniejsze wojsko.
- (10:15 UTC 12.10) **Jeff: "wiesniacy najbogatsi - bzdura" -> TAK na 3 zmiany (prawdziwe przeplywy):** (1) pan bierze z utargu wsi 45% zamiast 35% (polityka zwykla; niska/wysoka
  przesuniete o +10 pkt: 35/45/55); (2) dziesiecina OD RAZU (C3 z projektu E5, R21): 5% utargu wsi + od zarobku ludu miast W 1% / R 2% / P 3% -> kaplan osady; 50% jalmuzna
  (kiesa biednych miasta / kiesa wsi), 50% budowa septow (75% place ludu, 25% material); (3) wies i lud miast szybciej wydaja nadwyzke: 15%/d zamiast 7%/d ponad zapas.
- (10:25 UTC 12.10) **Jeff: etap 8 (drobne bledy) NIE "przy okazji"** - nie dokladamy drobnych bledow do paczek etapow 6/7 (CLAUDE.md 8.3); etap 8 jako osobny etap,
  wlasne paczki i wlasny test. Propozycja z 12.10 ("po kilka do kazdej paczki") ODRZUCONA.
- (10:30 UTC 12.10) **Jeff: etap 7 - tempo marszu O POLOWE wolniej niz dzis (nie do wartosci z ksiazek - za wolno); etap 8 - takze OPTYMALIZACJA (gra ma dzialac szybciej).**
  PLAN-DO-KONCA: 7.2 zmienione, nowe 8.17.
- (10:35 UTC 12.10) **Jeff: etap 7 - "olej predkosc, zostaw predkosc poruszania sie jak jest"** -> 7.2 (tempo) i 7.3 (przeliczenia) ODRZUCONE; reszta etapu 7 bez zmian.

**WGRANIE 33 (12.10 ok. 10:30 UTC; etap 6 porcja 2 + decyzje Jeffa 12.10 rano 1-4):** Armoury abe15b3b (galaz noc/wgranie-33 = w-toku/e6-p2 9665d65: K0 licznik koni
u zrodla, W2 ryby z wiosek rybackich, K1 kon ginie tylko zabity, K2 jeden zapas koni na sprzedaz, K5 konie poleglych jezdzcow / luzaki pod jukami; danina wsi 0.025;
prog krain 0.1% albo 200 ludzi; zaulki w oblezonym miescie wstrzymane), RealisticCaptivity 9cdc53ee (pojmujacy bierze tez CALE juki gracza). CrashScribe 842f64a4
i GrandTourney bbb841d7 bez zmian. Testy (autotest): 120 dob nowej kampanii - 120/120, Armoury 0 ERROR, RC 0 ERROR (Armoury-2026-10-12_02-44-09.log); zapis 9 dob
z konca biegu - 129/129, 18.2 s/dobe, Armoury 0 ERROR (Armoury-2026-10-12_03-21-36.log); bledy CrashScribe = zlapane wyjatki cudzych modow przy starcie (BK
BKCampaignStartBehavior.OnCharacterCreationOver, UIExtenderEx BaseViewModelMixin cctor, Harmony przy ladowaniu) - te same co w poprzednich biegach. Zima POMINIETA
(Jeff: "olej zime, jak wszystko wgramy, bedziemy sprawdzac zime"). Kopie: *.dll.bak-2026-10-12-przed-noc33 + D:\Backup-Bannerlord\wgrane\2026-10-12-noc33-przed
(29fdca1e, 943ea0d3). ZATWIERDZONE: Armoury abe15b3b, RealisticCaptivity 9cdc53ee, GrandTourney bbb841d7, CrashScribe 842f64a4.

- (10:30 UTC 12.10) **Jeff - NOWA KOLEJNOSC: "po co testowac teraz, skoro potem wgrywamy nowe rzeczy, ktore moga popsuc to, co dziala - wgrac wszystko z etapow 5, 6, 7
  i 8 i potem testowac dokladnie i naprawiac".** -> od teraz: kod calej reszty etapow 5 (wsie nie gromadza, porcja C, male zrodla zlota), 6 (R1, K3, K4, W1, W3, N1, R3,
  warunkowe), 7 (bez tempa marszu) i 8 (drobne bledy + optymalizacja), kazda paczka z recenzja i wylacznikiem MCM; potem JEDNO scalenie, dokladny test (120 dob + zapis
  + zima + bieg dlugi) i poprawki; dopiero wtedy wgranie. Bez testow w grze po drodze (poza budowa i recenzja kodu).
- (10:55 UTC 12.10) **Jeff: "rob az wyczerpiesz limit, tylko na biezaco zapisuj prace"** -> praca do limitu tygodniowego; po kazdym kroku push galezi + wpis tutaj.

**W TOKU (stan na 12.10 ok. 11:00 UTC) - kod etapow 5-8 bez testow w grze, potem jeden test (zasada Jeffa 10:30):**
- GOTOWE, NIEWGRANE: `w-toku/e5-wsie` 396c033 (od WGRANIA 33): pan 45% utargu wsi (VillageLordExtraShare 0.10), dziesiecina od razu (TownFolk.Tithe.cs, TempleTithe,
  TitheVillageShare 0.05; place budowy septu wsi ida do ludu miasta targowego - wybor agenta), nadwyzka ludu i wsi 15%/d (FolkSurplusSpendShare; zamki 7%). Recenzja: poprawiony
  wyjatek w VillageTithe (dopis do slownika w petli). DLL SCRATCH\test\Armoury-wsie.dll 76e54a0e. **Do sprawdzenia w tescie:** danina korony 2.5% liczona od stanu kiesy wsi
  bierze ok. 139 tys./d wiecej niz w kopia-fin3 - kiesy wsi moga spadac szybko; material septow placony kupcom bez zdejmowania towaru z polki (jak roboty gminy, projekt R21).
- W TOKU: workflow wf_aa159ecf-f65 - etap 6 porcja 3 na `w-toku/e6-p3` (od e5-wsie, drzewo SCRATCH\e6\p3): R1 wytop+przetop, R3, W1, W3, K4, N1, K3 (kazda autor + recenzent
  z poprawka), na koncu przeglad kolizji porcji i DLL SCRATCH\test\Armoury-e6p3.dll. Galaz wypychana co 15 min (repo + GitHub). Jesli przerwane: "git -C <lancuch> log
  w-toku/e6-p3" pokazuje, ktore paczki sa w commitach; wznowienie: Workflow resumeFromRunId wf_aa159ecf-f65 (skrypt w workflows/scripts sesji ae222cb7).
- DALEJ (kolejnosc): etap 5 reszta (porcja C, male zrodla zlota z audytu, ok. 26 tys./d "bitwy"), etap 7 (bez tempa marszu: 7.1, 7.4-7.12), etap 8 (8.1-8.17 z optymalizacja),
  potem scalenie, pelny audyt, jeden dokladny test (120 dob + zapis + zima + bieg dlugi), poprawki, wgranie.
- (11:40 UTC 12.10) **Jeff - ceny a dochody (analiza docs/CENY-A-DOCHODY-2026-10-12.md):** ogolny poziom cen zgodny z modelem historycznym (1 d = 1 pens) i z zoldem - NIE ruszac.
  (1) odchylki cennika (rzad konski z prawdziwej wagi, helmy t4-6, luki wojenne t3-6; opcjonalnie konie wedlug kategorii) - **TAK, etap 8 (8.5)**; (2) dochody gracza
  (nagrody zadan, turnieje, okupy) przeliczone do nowych cen, nagroda z kiesy zleceniodawcy liczona w dniach zoldu druzyny, bez zlota z niczego - **TAK, etap 8 (nowe 8.18)**;
  (3) drogi ser/drewno/plotno/srednie zbroje to niedobor produkcji, nie cennik - **OK** (czesc robi etap 6: W1, W3).
- (12:00 UTC 12.10) **Etap 7: projekt v2 gotowy** (`docs/etap7/PROJEKT-ETAP7-MAPA-DROGI-WIOSKI-2026-10-12.md`, krytyka 23 uwagi - wszystkie przyjete; commit 7bc5db6). Paczki: E7/0, T, K, C, O1, O2,
  L, B, W1, W2, W3, R1 (NK, po E5/A7 i W1), R2 (NK), W4, W5, D0 (proba drog - przeniesienie z w-toku/p2-drogi-proba 95f05be, domyslnie wylaczona); D1/D2 po tescie i decyzji Jeffa.
  Ustalenia: Myr-Tyrosh pokoj i wojna po 3 dniach to nasze wezwanie do wojny 168 (cena wezwania 5 tys. -> 200 tys.) - do obserwacji; CampBattlePropsEnabled nadal false mimo
  "wlaczyc" Jeffa 09.10 (w kodzie i w Armoury.json :302) - robi E7/O2; CrossingLaw (petla AI martwa) do etapu 8. **Pytania do Jeffa (rozdz. 6):** (1) premia +15% po drodze -
  rada NIE (drogi tylko do patrzenia, MCM 0); (2) ile drog - po zdjeciach, rada trakty + droga do kazdej wioski; (3) plik ustawien Diplomacy Jeffa przeliczyc na rok 364 -
  rada TAK (gra zamknieta, kopia .bak; plik w folderze gry/Documents - polecenie dla Jeffa albo jego zgoda); (4) myto na Bliznakach w ogole - rada: najpierw kierunek.
- W TOKU (12:00 UTC): workflow wf_17813d40-8ac - etap 7 kod w dwoch drzewach od w-toku/e5-wsie: `w-toku/e7-a` (SCRATCH\e7\a: E7/0, O1, T, C, K, O2) i `w-toku/e7-b`
  (SCRATCH\e7\b: L, B, W1, W2, W3, W5, D0); galezie wypychane co 15 min. Rownolegle nadal wf_aa159ecf-f65 (etap 6 porcja 3, `w-toku/e6-p3`). Potem: scalenie e6-p3 + e7-a + e7-b
  (+ etap 5 reszta: porcja C, E5/A7 lup z rabunku i male zrodla), E7/R1, R2, W4, projekt i kod etapu 8, pelny audyt, test zbiorczy.
- (14:45 UTC 12.10) **Etap 6 porcja 3 GOTOWA (kod + recenzje, bez testu w grze):** `w-toku/e6-p3` 1825401 (od e5-wsie): R1 9e11115+247c946 (wytop: lupa "Iron Bloom" 15 kg zamiast rudy w NOWEJ
  kampanii, flaga `arm_e6_bloom`, dymarka Bloomery.cs - 5 ladunkow drewna na lupe z lasu okregu/polki miasta; kuznie miast pala tylko wegiel), R3 96f5173+ea7ea81 (narzedzia wedlug
  zuzycia ToolsByUse, uprzaz dla jezdzcow t4+ AiGear, hurt kupcow wozem TradeCarts `arm_tradecarts`), W1 96e2553+f98a741 (receptury wedlug wartosci WorkshopRecipes: filc 3 welny->2,
  plotno 6 lnu->2, skora 5 skor->2), W3 d6fc75f+9157e26 (regulator wsi HarvestValve `arm_harvestvalve`: co 28 dob, nadmiar -> mniej, brak -> x1.3; RawNoRot przestaje kasowac),
  K4 a883cab+6c54769 (hodowla wedlug popytu w HarvestValve: krok w gore i przesuniecie zwykle->bojowe czynne, ciecie na sucho; przeglad 71 stadnin - tylko log), N1 20be89f+b54762f
  (naprawy jedna regula - Twoje rece jak kowale miasta, AI naprawia tylko noszone, zaplata kowalom do kiesy ludu - klucz MendPayToFolk), K3 16f2f56+1825401 (jazda wedlug szablonu
  rodu 175, kon rycerza ze stada wsi rodu albo targu - Stables.KnightBuy; rod gracza bez zmian). Przeglad kolizji porcji: bez uwag. DLL SCRATCH\test\Armoury-e6p3.dll b2f6aa11.
  **Do obserwacji w tescie:** dymarki moga brac kilka razy wiecej drewna niz ok. 100 ladunkow/d z projektu; pierwsza doba nowej kampanii 0 lup; obrzedy BKROT (20 x iron) ok. 5x drozsze
  dla gracza; N1 - Twoich rak autotest nie sprawdzi (kuznia recznie). **Do decyzji Jeffa:** K4 - czynne cofniecie wlasnego kroku x1.3 -> x1 przy nadmiarze (rada TAK, bez tego krok
  zostaje na zawsze; to nie jest ciecie ponizej x1); N1 - lekka naprawa moze nie zjesc materialu (dawniej min. 1 sztuka kazdej czesci) - skutek "jednej reguly" (rada: zostawic).
- (14:45 UTC 12.10) Limit tygodniowy 92% (reset 17.10 03:00 UTC). Etap 5 reszta (A7, A8, C1, C2, C4, "bitwy" ok. 26 tys./d) NIE zaczeta, zeby nie zostawic dwoch przerwanych watkow -
  najpierw dokonczy sie kod etapu 7 (wf_17813d40-8ac).
- (15:00 UTC 12.10) **Jeff: "1. ok 2. ok 3. tak 4. chce ale policz"** -> (1)(2) zgoda z radami: etap 6 K4 cofniecie kroku x1.3 -> x1 (juz w kodzie, HarvestValve.cs:753), N1 lekka naprawa
  bez materialu zostaje; etap 7 premia drog NIE (D2 domyslnie 0), ile drog - po zdjeciach (trakty + droga do kazdej wioski). (3) plik Diplomacy Jeffa na rok 364 - TAK, przy wgraniu
  koncowym (gra zamknieta, .bak; gdy narzedzie nie pozwoli - polecenie dla Jeffa). (4) myto na Bliznakach - CHCE, ale najpierw policzyc: stawki z licznika E7/B w tescie zbiorczym
  (+ lore Freyow), propozycja z liczbami do Jeffa, potem osobna paczka myta (kiesa partii -> kiesa pana Bliznakow). Wpisane w projekt etapu 7 rozdz. 6.
- (15:15 UTC 12.10) **Jeff: hodowla - TAK (cofniecie kroku jest w kodzie); naprawy - NIE "jak jest za malo, aby zuzyc 1 sztuke, to nie zuzywa, liczy w pamieci i potem dopiero zuzywa,
  gdy sie nazbiera"** -> paczka N1b (wf_7ad424f1-19a na w-toku/e6-p3): ulamek materialu kazdej naprawy w pamieci (zrodlo x przedmiot, zapis arm_mend_frac), sztuka schodzi z tego samego
  zrodla przy >= 1; klucz MendFractionMemory.
- (15:35 UTC 12.10) **N1b GOTOWE** (`w-toku/e6-p3` 6bdf1e3, recenzja bez uwag): ulamek materialu przepadal tylko przy naprawie WLASNYMI REKAMI (MendMaterial.SelfParts - Math.Round);
  teraz licznik na material z sakw gracza (arm_mend_frac), sztuka schodzi, gdy sie nazbiera; puste sakwy - naprawa czeka, licznik bez zmian. Kowale miasta, kwatermistrz Spoils,
  ludzie gracza i AI juz liczyli w kg z zapasem kowali (arm_mendstock, zloto za zuzyty ulamek) - bez drugiego licznika. Klucz MendFractionMemory. Test reczny w kuzni (autotest nie
  cwiczy wlasnych rak). DLL test\Armoury-e6p3.dll 7627f953.
- (16:35 UTC 12.10) **Etap 7 (bez R1/R2/W4, D1/D2) GOTOWY w kodzie** (wf_17813d40-8ac, kazda paczka autor + recenzent, przeglady kolizji bez uwag):
  `w-toku/e7-a` be9cd4c: E7/0 pomiar (WorldClock7), O1 sen do switu (SleepGate - 10 metod SetMove*, SleepersHoldOrders), T kary terenu jak gra (TerrainEase), C jedna skala czasu
  (odwet kryjowek na zegarze gry), K turnieje co 3. tydzien caly rok / sojusz 364 / wezwanie 182 / przerwa po pokoju, O2 namioty i ognisko przy napadzie na oboz gracza. DLL test\Armoury-e7-a.dll c618977a.
  `w-toku/e7-b` 8e28af6: L dane lore 2 446 wiosek (arm_map_village_lore.tsv, tools\wioski-lore\l0.py; herb gry tylko u 140 wiosek - reszta pokaze herb pana), B licznik Bliznakow
  (TwinsCrossing), W1 komunikat "plonie Twoja wioska" + pamiec wojny (sekcja m: w arm_district), W2 dymek wioski, W3 menu okregu, W5 proporczyk pana, D0 proba drog (MapRoadsProbeEnabled
  domyslnie false). DLL test\Armoury-e7-b.dll 6d69845d.
  **WAZNE DO TESTU I WGRANIA:** (1) e7-b potrzebuje plikow poza DLL: Armoury\ModuleData\arm_map_village_lore.tsv, Armoury\ModuleData\arm_map_roads_probe.tsv, Armoury\GUI\Prefabs\
  ArmMapVillageTooltip.xml, Armoury\Prefabs\arm_map_roads.xml - autotest.ps1 podmienia tylko DLL, trzeba dopisac kopiowanie i przywracanie tych plikow (blad w XML prefabu = blad startu
  gry, prefab laduje sie niezaleznie od klucza); (2) u Jeffa Armoury.json "CampBattlePropsEnabled": false (:302) - bez zmiany na true O2 nie dziala (Jeff 09.10 "wlaczyc"); (3) MapVillageLore.
  SessionStart przed McmSettings.Apply - tylko linia startu sesji (poza zakresem); (4) BK latki na SetMovePatrolAroundSettlement/GoToPoint/PatrolAroundPoint nie wpinaja sie w 1.4.8 -
  etap 8; (5) AIInfluence moze miec wlasna latke SetMoveGoToSettlement (zaciemniony) - obserwowac w tescie.
- W TOKU (16:45 UTC 12.10): workflow wf_42f8dda9-fa3 na `w-toku/zbior-5-8` (SCRATCH\zb\58, od e6-p3): scalenie e7-a + e7-b (z mechaniczna weryfikacja, ze nic nie zginelo), potem
  etap 5 reszta: A7, A8, rozpoznanie "bitwy" 26 tys./d, C1 (TownPurseNoTopUp domyslnie true), C2, C4; potem etap 7 R1, R2, W4; na koncu dwa przeglady calosci (ekonomia, kod) i DLL
  test\Armoury-zbior58.dll. Galaz wypychana co 15 min. Jesli przerwane (limit): `git -C <lancuch> log --oneline w-toku/e6-p3..w-toku/zbior-5-8` pokaze zrobione kroki;
  wznowienie Workflow resumeFromRunId wf_42f8dda9-fa3. Potem: projekt i kod etapu 8, pelny audyt, autotest z plikami danych (patrz uwaga e7-b), test zbiorczy.
- (18:20 UTC 12.10) Postep wf_42f8dda9-fa3 (`w-toku/zbior-5-8`): A8 b461cc5+da51768 (nowy rod szlachty BK - majatek bez ceny, 15 000 z nadwyzki kasy miasta; herszt BK - 10 000 z kryjowki,
  juki przez paserow w miastach; uczta - miasto dostaje tyle, ile zaplacil gospodarz; koszt paktow Diplomacy do skarbca drugiej strony, przy pokoju z upadkiem krolestwa - nic nie pobrane);
  E5/BITWY c07c1d8+a119682 - ROZPOZNANIE: "26 tys./d bitwy z niczego" to wyplata gry zwyciezcom BRUTTO, para z ujsciami (wodzowie pokonanych, A6) i N4 - netto 119 dob -13 zl; statki
  z niczego 0; BEZ zmiany kodu, tylko progi BITWY-1/2; C1 a053001+d493556 - regulator bez dosypki juz jest (tryb 2 z E5/A1, decyzja Jeffa 11.10) - bez nowego klucza, tylko licznik plonu
  w miastach-zaulkach i progi C1-1..4. Dalej C2, C4, E7/R1, R2, W4. Projekt etapu 8 (wf_c0d119d4-812): 4 rozpoznania gotowe (docs/etap8, bez commita), pisze sie projekt. Tydzien 96%.
- (18:45 UTC 12.10) **Jeff: nowy rod szlachty BK - "zawsze 0"** (zamiast 15 000 z kasy miasta). Kod: `w-toku/a8-gentry0` bc5e3ef (od zbior-5-8 2b30405, osobne drzewo SCRATCH\zb\a8g,
  bo w zbior-5-8 pracuje workflow): nowy klucz GentryStartFromTown = false (kasa miasta tylko przy true), build 0, CHANGELOG. **DO SCALENIA** do `w-toku/zbior-5-8` po skonczeniu
  wf_42f8dda9-fa3 (konflikty spodziewane: CHANGELOG gora, Settings.cs, McmSettings.cs - gen_mcm).
- (19:00 UTC 12.10) **Etap 8: projekt v2 gotowy** (`docs/etap8/PROJEKT-ETAP8-DROBNE-BLEDY-OPTYMALIZACJA-2026-10-12.md`, krytyka 25 uwag - 23 przyjete, 2 czesciowo; commit 281723d).
  29 paczek (242-270): E8/0 (pierwsza), P1-P3, S, M, X, W, A, Z, L, C, R, K, D1 (nagrody zadan w obie strony, kwota zamrazana przy przyjeciu - arm_e8_questpay), D2, B, RC, G1/G2
  (po TAK Jeffa), Q, T (jedyna NK - 8.10, + PLAN 1.12 rekruci gracza t2+ bez darmowego kompletu), E (skrypt BEE), O1-O6 optymalizacja (realnie 0.3-0.7 s/dobe; nasze mody 15-18% doby;
  najwiekszy koszt - liczba partii 2 319 -> 5 201 w 120 dob). Rownolegle: watek Armoury po kolei + watek CrashScribe/RC/ForgeView (E8/M, E8/RC). 8.16 - 29 recznych testow Jeffa.
  **Pytania do Jeffa (rozdz. 6; 1-5 blokuja kod):** (1) trzecia kapitana z kazdej zdobyczy - rada TAK; (2) konie wedlug kategorii - TAK; (3) karawany BK w miescie co godzine - TAK;
  (4) klany najemnikow ROT bez ludzi z niczego - TAK; (5) welna z uboju 0.15 szt.; (6) milicja z daru Spoils - wyciac; (7) "kon bez uprzezy" = start postaci - tak; (8) kuznia - zostaje
  filtr ForgeView; (9) pasek DTE - nie wylaczac; (10) lzejsze logi - nie teraz; (11) liczba partii - nie w etapie 8 (pomiar po tescie).
- (19:15 UTC 12.10) **Jeff - odpowiedzi do etapu 8 (wpisane w projekt rozdz. 6):** (1) NIE rozszerzac trzeciej kapitana - "to, co ja zabilem, idzie do mnie" (wlasne zabojstwa gracza 100%
  graczowi - sprawdzic w kodzie); (2) konie wedlug kategorii TAK; (3) karawany BK co godzine TAK; (4) koniec ludzi z niczego u najemnikow ROT TAK; (5) welna 0.15 z owcy - przez pamiec
  ulamkow (jak N1b); (6) milicja z daru Spoils - wyciac; (7) INACZEJ: jezdziec = kon + UPRZAZ (sam kon nie wystarczy, zeby stworzyc jezdzca) - sprawdzic dzisiejsza regule awansu/
  dosadzenia i dodac wymog uprzezy (AI i gracz, jedna regula), pomiar skutku dla jazdy; (8) kuznia - zostaje jak jest; (9) pasek DTE - nie wylaczac; (10) lzejsze logi - nie; (11) liczba
  partii - nie w etapie 8.
- (19:40 UTC 12.10) **KONIEC KONTA ae222cb7 - przejscie na drugie konto (Jeff).** Pelny stan i kolejnosc: `docs/PRZEKAZANIE-2026-10-08.md` rozdz. 27 (+ 26b sciezki i sprzatanie).
  zbior-5-8 f01f47a (do E7/R1 wlacznie), a8-gentry0 bc5e3ef, e8-a 8a8df38 (E8/0), e8-b bf66298 (E8/M); recenzje E8/0 i E8/M dokanczane (wf_7d02158c-a00). W grze WGRANIE 33.
- (20:15 UTC 12.10) **DRUGIE KONTO (sesja dbc3d3d1, limit 0%) - START.** Drzewa poprzedniego konta czyste, galezie zgodne z GitHub (zbior-5-8 f01f47a, a8-gentry0 bc5e3ef, e8-a 8a8df38,
  e8-b 9237d23), gra zamknieta, w grze WGRANIE 33. **W TOKU: workflow wf_1db23a93-783** (skrypt `docs/przekazanie/wf-e8-dwa-drzewa-i-zbior.js`; wznowienie tylko w sesji dbc3d3d1),
  trzy watki, przed kazda paczka bramka limitu konta (5 h >= 82% - czeka na odnowienie; tydzien >= 97% - watek staje), po kazdej recenzji push galezi (repo + GitHub):
  (1) `w-toku/zbior-5-8` (zb\58): E7/R2, W4 (autor + recenzent), przeglad calosci ekonomia, przeglad calosci kod, scalenie `w-toku/a8-gentry0`;
  (2) `w-toku/e8-a` (e8\a): recenzja E8/0, potem P1, P2, X, W, A, Z, L, C, R, B, O1, O2, O3, O5, O6;
  (3) `w-toku/e8-b` (e8\b): E8/RC, potem NOWE drzewo `w-toku/e8-c` (e8\c, od e8-a po E8/0): P3, S, Q, G1, G2, T, E, K, D1, D2.
  **Zmiana wobec wf-e8-kod.js:** watek Armoury podzielony na dwa drzewa (A = sprzet/lup/ceny/optymalizacja, C = stan/karawany/start/zadania - pliki rozlaczne wedlug projektu 5.1;
  wzor: e7-a/e7-b), zeby skrocic czas o ok. 6 h; **E8/H (jezdziec = kon + uprzaz) dopiero PO scaleniu** (dotyka plikow obu drzew), O4 po tescie.
  **DALEJ:** scalenie zbior-5-8 + e8-a + e8-c + e8-b (weryfikacja mechaniczna "nic nie zginelo"), E8/H, przeglad kolizji etapu 8, pelny audyt (kilka niezaleznych przegladow +
  sprawdzenie znalezisk), autotest z plikami danych e7-b, test zbiorczy (120 dob + zapis 9 dob + bieg dlugi + zdjecia proby drog; zima - gdy Jeff zdecyduje), poprawki, wgranie.
- (20:55 UTC 12.10) Postep wf_1db23a93-783: recenzja E8/0 ZROBIONA (`w-toku/e8-a` f7db9c5 - koszt profilu liczony raz), E8/P1 zakodowane (bb18686, recenzja w toku); E8/RC ZROBIONE z recenzja
  (`w-toku/e8-b` bb02c3e - poprawka: licznik hanby dlugu przy braku odbiorcy); drzewo `w-toku/e8-c` zalozone od f7db9c5 (P3 w toku); `w-toku/zbior-5-8` E7/R2 zakodowane (35fb7da, recenzja w toku).
  Autotest: `at1-autotest` 486008e - nowy parametr `-ExtraFile "cel wzgledem Modules=zrodlo;..."` (pliki danych e7-b/W4 na czas testu; plik, ktorego przed testem nie bylo, po tescie znika;
  proba na sucho na atrapie gry OK). Rozpoznanie E8/H (jezdziec = kon + uprzaz) w toku -> `docs/etap8/rozpoznanie-H-jezdziec-uprzaz.md`. Jeff spi, raport za 12-14 h. Limit 5 h: 7% po 45 min.
- (22:40 UTC 12.10) **`w-toku/zbior-5-8` GOTOWE do a589def:** E7/R2 35fb7da (okienko gracza po kazdej osadzie, NK), E7/W4 0adf433 + recenzja 7c1bada (wyglad spalonej wioski, napisy;
  nowy prefab GUI\Prefabs\ArmBurnedVillageLabels.xml), przeglad calosci 1 ekonomia 69e6f70 (bez zmian kodu), przeglad calosci 2 kod 1500d97 (prog E7/C T1 falszywe NIE - poprawiony),
  scalenie a8-gentry0 bfe0f64, **scalenie e8-b a589def** (cs-bledy + E8/M + E8/RC; konflikt tylko CHANGELOG - suma, 689 naglowkow; build 5 modow kod 0).
  Pliki poza DLL od WGRANIA 33 (do testu przez `-ExtraFile`, do wgrania recznie): Armoury\GUI\Prefabs\ArmBurnedVillageLabels.xml, ArmMapVillageTooltip.xml, Armoury\ModuleData\
  arm_map_roads_probe.tsv, arm_map_village_lore.tsv, Armoury\Prefabs\arm_map_roads.xml, CrashScribe\ModuleData\CrashScribe.settings.xml (zmieniony). GrandTourney bez zmian.
  Etap 8: `e8-a` do E8/X (f61193c: P1, P2, X z recenzjami), `e8-c` do E8/Q (e5420ee: P3, S z recenzjami, Q zakodowane). Rozpoznanie E8/H gotowe (docs/etap8/rozpoznanie-H-jezdziec-uprzaz.md):
  dzis sam kon wystarcza; uprzezy ok. 7.7 tys. na 10.8 tys. jezdzcow; siodlarze stoja przez zla wage siodel ROT (naprawia E8/C); zalecany wariant A (para kon + dowolne siodlo rodziny konia).
  **PROBA STARTU (nie test zbiorczy):** 10 dob nowej kampanii na zbior-5-8 a589def (Armoury 2ce92d48, RC 468b0e66, ForgeView ca86b75b, CS-at 49670825 = a589def + at1-cs.diff, pliki danych
  przez -ExtraFile) - zeby wczesnie zlapac bledy startu etapow 5-7, ktore nigdy nie biegly w grze; wynik: SCRATCH(dbc3d3d1)\test\at-zb58-proba10.out.txt. Skrypt biegu: SCRATCH(dbc3d3d1)\bieg.ps1.
- (23:20 UTC 12.10) **PROBA STARTU 10 dob na zbior-5-8 a589def: OK** (10/10, 13.5 s/dobe, Armoury 0 ERROR, CS 0 blokow z naszych modow, 23 wyjatki startowe cudzych modow jak we wzorcu;
  pliki danych przez -ExtraFile przeszly zabezpieczenia; DLL i pliki przywrocone, zapisy Jeffa nietkniete). **BIEG 120 dob NOWEJ kampanii na zbior-5-8 a589def (wczesny, rownolegle z kodem
  etapu 8 - nie zastepuje testu zbiorczego): OK 120/120, 17.6 s/dobe (W33 17.5)**, log Armoury-2026-10-12_15-33-44.log, CS session-2026-10-12_15-33-43.log, zapis autotest-2026-10-12-1610.sav.
  Armoury ERROR 1: `ValyrianBlades.Census: Stal valyrianska: UBYTEK -1 - darksister (jest 0, rejestr 1), ostatnio: targ Vaes Diaf`. Progi (wyniki: SCRATCH(dbc3d3d1)\test\progi-zb58\ i
  progi-w33\ dla wzorca): NOWE NIE wobec W33 - R1-7 lupy niewytopione 36% (prog 5%), R1-8 miasta bez zelaza 45 (15), R1-9 stos drewna miast +114%, R1-10 brak rudy 816/d; R3-9 polki uprzezy +10%;
  W1-5 welna, W1-7 kolejka odziezy wojska rosnie, W3-6 sol i glina rosna; N1-2/3/6 czeka na material 2274/d (skora 1632/d); K3-3 jazda AI 8.7% (prog 12%), K3-5 Dothrakowie 41.7% (60%);
  K2-5 pompa koni 289 (W33 84); A3-7 koszt markietanow 109.7 ms/d (W33 38.7); A1-T14 zakup ponizej zapasu w 1 dobie; E7/R1 T5 wioski spalone 0 w 120 dob; E7/O1 T2, E7/T T4 20.5 ms, E7/B T3
  (4 przejscia dalej niz 3 jedn.), E7/B T4. BYLY JUZ W W33: K0-6, K1-3, K1-4, K5-4, A3-2, etap2 wojsko 90 tys./D staly 2254/kurier, etap4 pula wyrzutkow/T9/K22, E7/O2 T1 (klucz Jeffa).
  **W TOKU:** zapis 9 dob (wczytanie autotest-2026-10-12-1610) -> test\at-zb58-zapis9.out.txt; workflow wf_fe34e80e-003 - rozpoznanie przyczyn w 7 obszarach (tylko odczyt) + podwazenie
  kazdego wniosku BLAD/STROJENIE; potem poprawki na zbior-5-8 (autor + recenzent) rownolegle z kodem etapu 8 (wf_1db23a93-783: e8-a po X, e8-c po Q).
- (00:20 UTC 13.10) **Zapis 9 dob na zbior-5-8 a589def: OK** (wczytanie autotest-2026-10-12-1610, 129/129, 18.5 s/dobe, Armoury 0 ERROR; wywrotka silnika przy zamykaniu - znana).
  **TRIAZ GOTOWY** (wf_fe34e80e-003, 7 obszarow + podwazenie): `docs/test-zbiorczy/TRIAZ-zb58-120-2026-10-12.md` (+ .json). Wynik: wiekszosc NIE to zle progi narzedzia albo objawy
  obecne juz w W33; prawdziwe poprawki kodu: F1 spis stali/unikatow nie widzi wozow kupcow (miecz nie zginal - jechal 4 doby wozem), F2 juczne nowych karawan z niczego 13 622/120 d (slownik
  PackMarket._pend bez komparatora po referencji - K5(a) nigdy nie dzialalo, takze w W33), F6 koszt markietanow x2.8 (regresja E6/N1), F7 spiacy lordowie budzeni przed switem (zegar a tick
  xx:30; + wezwania GT/RC przez wejscie metody), F8 licznik Bliznakow wokol mostu, F5 budzet postoju pana na konie (jazda 8.7%), F4a dowoz drewna do dymarek wozem z miast z nadwyzka
  (36% lup niewytopionych, miasta bez zelaza 45 wobec 29), F4b rachunek za drewno przed udzialem pana, F3 licznik koni mieszczan (KROK 1; KROK 2 = decyzja Jeffa 12.10 "mieszczanie tylko
  konie robocze" + czynne ciecie hodowli - czeka na liczby). **W TOKU: wf_3c309990-051** (skrypt docs/przekazanie/wf-poprawki-po-biegu-zb58.js): F1, F2, F6, F7, F8, F5, F4a, F4b, F3 po kolei
  na `w-toku/zbior-5-8` (autor + recenzent, push po kazdej), rownolegle F9a narzedzie progow na `w-toku/zb58-progi` (zb\58t, od a589def), na koncu zlozenie F9b + DLL test\zb58f.
  PO SCALENIU ETAPU 8: F6 pkt 2 (pamiec UsedByMen w AiWear - plik zmienia E8/W). DO JEFFA (raport): czy kupcy maja wozic unikaty miedzy miastami (bylo tez w W33).
  Etap 8 (wf_1db23a93-783): `e8-a` po E8/A (652fe0d; zostaje Z, L, C, R, B, O1, O2, O3, O5, O6), `e8-c` po E8/G2 (c93dfe0; zostaje T, E, K, D1, D2).
- (03:00 UTC 13.10) Postep: poprawki na `w-toku/zbior-5-8` do F5 (7452951): F1 b762fe4, F2 7d8d0cd, F6 cd06bcd, F7 a11e2e7 (3 mody), F8 38cb329, F5 57819b1 - kazda z recenzja; zostaje F4a, F4b, F3,
  zlozenie F9b. Narzedzie progow F9a gotowe: `w-toku/zb58-progi` b5509fe. Etap 8: `e8-a` po E8/C (3e34ef0; R zakodowane d09215f; zostaje B, O1, O2, O3, O5, O6), `e8-c` po E8/K (ac3bd70; zostaje D1, D2).
  **NOWE DRZEWO SCALENIA `w-toku/zbior-e8` (zb\int)** - wf_f8505b0b-689 (docs/przekazanie/wf-zbior-e8-scalenie-i-H.js): wczesne scalenie e8-a@3e34ef0 + e8-c@ac3bd70 (z mechaniczna weryfikacja),
  potem E8/H (jezdziec = kon + uprzaz, wariant A z rozpoznania; klucz RiderNeedsHarness). PLAN ZLOZENIA KONCOWEGO: do `zbior-e8` dojda kolejnymi scaleniami: zbior-5-8 (etapy 5-7 + e8-b +
  poprawki F), ogon e8-c (D1, D2), ogon e8-a (R, B, O1-O6); potem F6 pkt 2 (AiWear), przeglad kolizji, pelny audyt, test zbiorczy. Limit 5 h 18%, tydzien 20%.
- (04:45 UTC 13.10) **`w-toku/zbior-e8` (zb\int) = 0641a9c:** wczesne scalenie e8-a@3e34ef0 + e8-c@ac3bd70 (3095630, weryfikacja mechaniczna: 0 brakow, 0 dubli), **E8/H 0e7a7f7 + recenzja cf59001**
  (jezdziec = kon + uprzaz: SaddleLaw.cs, klucz RiderNeedsHarness, brama awansu AI/gracz/ROT/najemnik, bitwa gracza, AiGear.BuySaddles; recenzja: brama gracza nie zamykala sie - poprawione;
  zalozenia DO POTWIERDZENIA PRZEZ JEFFA: bohaterowie poza regula, zold jazdy bez zmian, zalogi nie kupuja uprzezy, 4 wzorce ROT bez uprzezy), ogon e8-c 3f2f0df (D1, D2), e8-a do O1 0641a9c
  (R, B, O1). Moje scalenia: konflikty tylko CHANGELOG (suma) i sprawdz_logi.py (bloki dopisane w tym samym miejscu - suma; 24 def etap8_*, 0 dubli, py_compile), gen_mcm Armoury 1282, build 5 modow 0.
  **Etap 8 drzewo C SKONCZONE** (`e8-c` 136216e: P3, S, Q, G1, G2, T, E, K, D1, D2). Drzewo A: zostaje O2, O3, O5, O6 (wf_1db23a93-783). Poprawki zbior-5-8: F4a 81f81e3, F4b 25d2fbf, F3 3c47f2a
  (recenzja w toku), potem zlozenie F9b. **W TOKU wf_88688069-5bf (szwy w zbior-e8):** E8/H - E8/O1 (zakup siodel w pamieci polki) i F6 pkt 2 (pamiec AiWear.UsedByMen).
  DALEJ: scalenie zbior-5-8 (po F9b) do zbior-e8 -> bieg probny 120 dob -> ogon e8-a (O2-O6) -> przeglad kolizji + pelny audyt -> test zbiorczy -> wgranie.
- (06:00 UTC 13.10) **`w-toku/zbior-e8` = e60928a - PRAWIE CALOSC (brak tylko E8/O2, O3, O5, O6 z drzewa A):** szwy 14563c4 (E8/H - E8/O1: zakup siodel juz objety pamiecia polki) + 1a15da2
  (F6 pkt 2-3: pamiec AiWear.UsedByMen 5-8 dob, linia "Rezerwa napraw AI - pamiec dopasowania (F6)" - do obserwacji "netto pamiec - swieza") + recenzja ce6524b; **scalenie zbior-5-8 6c2c453 = e60928a**
  (poprawki F1-F8 + F9a/F9b narzedzie; konflikty: ArmouryBehavior konstruktor i takt doby - DistrictRaid.Reset/Daily ze stoperem profilu, CS SubModuleMain - liczniki E8/0 + filtr E8/M,
  sprawdz_logi opis, CHANGELOG 760 naglowkow; weryfikacja mechaniczna 139 plikow - 0 brakow, 0 dubli poza 4 liniami zlozonymi recznie; gen_mcm Armoury 1293 / RC 111 / GT 40; build 5 modow 0).
  Poprawki zbior-5-8 SKONCZONE (wf_3c309990-051): F1 b762fe4, F2 7d8d0cd, F6 cd06bcd, F7 a11e2e7, F8 38cb329, F5 57819b1, F4a 81f81e3 (+04a0968), F4b 25d2fbf, F3 3c47f2a, F9a b5509fe, F9b 3a80dd1 (+6c2c453).
  **W TOKU:** (1) BIEG PROBNY 2: 120 dob nowej kampanii na zbior-e8 e60928a (Armoury 359e8b37, RC 8bb3de5c, GT 778e4aac, ForgeView 9b14f90f, CS-at 952de067; test\at-e8r1-120.out.txt);
  (2) PELNY AUDYT wf_512814c1-89f (docs/przekazanie/wf-pelny-audyt-zbior-e8.js): 8 przegladow tylko odczyt (zloto, sztuki, latki, zapis, ustawienia, zderzenia paczek, wydajnosc, sciezki gracza)
  + niezalezne sprawdzenie kazdego znaleziska krytycznego/waznego; (3) drzewo A: O2 zakodowane (2a7ee78), zostaje O3, O5, O6.
  DALEJ: ogon e8-a -> poprawki z audytu i z biegu 2 -> test zbiorczy (120 + zapis + bieg dlugi + zdjecia drog + -Profile) -> wgranie.
- (06:45 UTC 13.10) **BIEG PROBNY 2 (zbior-e8 e60928a, 120 dob nowej kampanii): 120/120, Armoury 0 ERROR, CS 0 blokow naszych modow; ALE 21.2 s/dobe (bieg 1: 17.6, W33: 17.5).**
  Log Armoury-2026-10-12_22-38-57.log, CS session-2026-10-12_22-38-56.log, zapis autotest-2026-10-12-2322.sav; progi: SCRATCH(dbc3d3d1)\test\progi-e8r1\. Najwazniejsze:
  (1) SZYBKOSC: E8/G1 karawany BK w miescie co godzine = 1411 ms/dobe (prog 5 ms) - glowny skladnik wzrostu; profil E8/0 9.7 ms, ksiega umiejetnosci 34 ms, E8/H 10.8 ms; "nasze mody razem" 4.37 s/dobe;
  (2) WOJSKO lordow w wojnie 84 241 (bieg 1: 90 269, W33: 87 027) - ponizej twardej podlogi 85 tys.; (3) E8/H: jezdzcy z koniem bez uprzezy 20.5% (prog 10%), uprzaz wykonana 25.7/d (Z33 13.4;
  prog 40), wstrzymane z braku uprzezy rosnie; jazda t3+ 16.7% przy celu 26.6%; (4) kasy miast d120 8.67 mln (-15.6% wobec Z33); (5) 3 nowe wyjatki pierwszej szansy "Token 0x.. is not valid"
  (nasz skan IL), 2 bloki BK CorrectPlayerEducation mimo E8/M. POPRAWILO SIE po F: R1-7 dymarki TAK, miasta bez zelaza 45 -> 33.4 (prog 30), juczne z niczego 13 622 -> 325 (morskie),
  koszt markietanow w progu, czeka na skore 1632 -> 654/d. Progi: etap2 1 NIE, etap3 2, etap4 3, etap5 17, etap7 3, etap8 22 (pierwszy bieg etapu 8).
  **W TOKU:** triaz biegu 2 wf_48305665-088 (5 obszarow: szybkosc, wojsko, konie/uprzaz, etap 8 reszta, etapy 3-7 reszta; BLOKUJE / STROJENIE / PROG; z podwazeniem), pelny audyt wf_512814c1-89f,
  zapis 9 dob (autotest-2026-10-12-2322), drzewo A (O3, O5, O6). Potem: jedna runda poprawek (audyt + triaz), ogon e8-a, test koncowy, wgranie.
- (07:00 UTC 13.10) **PELNY AUDYT GOTOWY** (wf_512814c1-89f; 8 przegladow + sprawdzenie): `docs/test-zbiorczy/AUDYT-zbior-e8-2026-10-13.md`. 13 znalezisk: 0 krytycznych, 3 wazne (wszystkie
  potwierdzone), 9 drobnych. WAZNE: (1) siec CrashScribe - finalizer z parametrem __originalMethod na kazdej metodzie Armoury/RC/GT (7 834 metody) kosztuje 0.07-1.6 us + 240 B smieci na KAZDE
  wywolanie (koszt zastany, tez w W33; poprawka S w Net.cs - metoda z ramki stosu dopiero przy wyjatku); (2) HorseFate.cs:906 - przy wylaczonym LootIsTransfer uprzaz rozbitej partii powstaje
  drugi raz (S); (3) krok wgrania: USUNAC linie "CampBattlePropsEnabled": false z Armoury.json Jeffa (:302), nie zmieniac na true. DROBNE: kopalnia BK gracza wytapia bez drewna
  (WorkshopLaw.cs:1535), ksiega towarow liczy lup z rabunku jako produkcje (DistrictRaid.cs:542), Reset SaddleLaw/CrossingLaw tylko pod kluczem E8/S (StateHygiene.cs:44), flaga NK dymarek
  niezalezna od klucza (Bloomery.cs:156), 4 opisy MCM po polsku + 1 bez opisu, nowa partia lorda w dobach 1-3 dostaje konie dwa razy (HorseFate.cs:1151), licznik potkniec napisow W4 zerowany
  co klatke (MapVillageLook.cs:535), RC praca we wsi po wczytaniu - zle menu (Work.cs:144), licznik CaravanFlow. Zloto, sztuki, zapis, latki (sygnatury), sciezki gracza: bez krytycznych uwag.
  **Zapis 9 dob po biegu 2: OK** (129/129, Armoury 0 ERROR, 24.0 s/dobe). **PROFIL KLATKI (10 dob od d120, test\profil-e8r1.txt):** klatka 163.7 ms; Native 31%, silnik 20%, Armoury 18%
  (27.7 ms), BannerKings 16% (z tego tik karawan BK 17.5 ms = 11% klatki - skutek E8/G1), ROT-Core 10% (CheckNPCMarriages 14.9 ms). `w-toku/zbior-e8` = cd842e7 (+ E8/O2, O3).
- (07:25 UTC 13.10) **KOD ETAPOW 5-8 KOMPLETNY: `w-toku/zbior-e8` = e9dfa7f** (zbior-5-8 z poprawkami F1-F9 + e8-a cale [E8/0, P1, P2, X, W, A, Z, L, C, R, B, O1, O2, O3, O5, O6] + e8-b
  [M, RC] + e8-c cale [P3, S, Q, G1, G2, T, E, K, D1, D2] + E8/H + szwy). Workflow wf_1db23a93-783 SKONCZONY (89 watkow, wszystkie paczki autor + recenzent). Scalenia ogona e8-a: cd842e7
  (O2, O3), e9dfa7f (O5, O6) - konflikt tylko CHANGELOG; gen_mcm Armoury 1297 / RC 111 / GT 40; build 5 modow kod 0. NIEZROBIONE Z PROJEKTU: E8/O4 (musztra - "po tescie"),
  K1-3a krok 2 (mieszczanie tylko konie robocze + czynne ciecie hodowli - decyzja Jeffa 12.10, czeka na liczby), K5 krok 2 (juczne karawan morskich, 325 z niczego / 120 dob).
  **W TOKU:** BIEG 3 = 120 dob na komplecie e9dfa7f (Armoury b2893c27; test\at-e8r2-120.out.txt) - pierwszy bieg O2/O3/O5/O6; triaz biegu 2 (wf_48305665-088).
  Potem jedna runda poprawek (audyt: Net.cs, HorseFate :906 i drobne; triaz 2: karawany E8/G1 i reszta), test koncowy, wgranie.
- (07:50 UTC 13.10) **TRIAZ BIEGU 2 GOTOWY** (wf_48305665-088): `docs/test-zbiorczy/TRIAZ-2-zbior-e8-120-2026-10-13.md`. **JEDNA PRZYCZYNA ZRODLOWA trzech objawow (czas doby +3.6 s, wojsko
  lordow < 85 tys., kasy miast -15.6%): KARAWANY NOTABLI SIE MNOZA** - gra daje notablowi kolejna karawane, gdy zadna jego karawana nie ma wlaczonego AI (DefaultCaravanModel.CanHeroCreateCaravan),
  a nasz nocny oboz wylacza AI spiacym karawanom w polu (NightRest: Ai.DisableForHours). Karawan na notabla w d120: W33 2.80, bieg 1 2.62, bieg 2 4.74 (1570 karawan; gra bez przecieku trzyma 1).
  To STARY przeciek (jest w W33 - stad "partie 2 319 -> 5 201"), ktory E8/G1 potroilo (karawany nie stoja 3 dob w miescie, wiec spia w polu). Kapital 1 594 karawan zjadl nadwyzki kas miast ->
  renty panow i korony o polowe nizsze -> mniej wojska. Drugi skladnik czasu: tiki BK otwarte przez E8/G1 1.4-1.7 s/dobe (44 karawany bez wyjazdu tykaja co godzine).
  Pozostale: Polnoc 10.1 -> 5.8 tys. ludzi (renta korony tylko 2 z 20 rodow przez warunek sluzby), siodla - w wiekszosci krain nikt nie ma w puli zwyklego siodla (25.7 szt./d), zwiad AI
  0.79 (stale 15/25 -> 10/16), 3 wyjatki skanu IL w QuestPay8; reszta to progi narzedzia albo objawy sprzed etapu 8. NIE odpowiadaja za spadek wojska: F5, E8/W, E8/H, E8/D1/D2/K.
  **DECYZJA PROWADZACEGO (ekonomia; do potwierdzenia przez Jeffa w raporcie): NotableCaravanCap = 2** (0 = regula gry; 1 = najszybciej, czas ok. W33 i ok. 30% mniej wjazdow karawan niz W33;
  2 = ok. 750 karawan, ok. 30% wiecej wjazdow niz W33, czas szac. +1.0 s wobec W33 przed optymalizacjami; 3 = poziom W33). E8/G1 zostaje wlaczone z limitem 12 tikow na postoj.
  **W TOKU: wf_521dc7fa-0d5 - RUNDA POPRAWEK 2** (docs/przekazanie/wf-poprawki-runda-2.js), piec grup rownolegle w drzewach zb\pa..pe od e9dfa7f (galezie w-toku/p2-a..e), autor + recenzent:
  A karawany (NotableCaravanCap, CaravanTownHourlyMaxTicks, juczne karawan morskich), B siec CrashScribe (Net.cs bez __originalMethod, z testem poza gra), C siodla/konie/renty korony
  (PlainSaddleEveryRealm, zamowienia siodel, AiSaddleShelfFloor, HorseFate :906 i dorobek startowy, CrownRentServiceAfterFullDays), D drobne z audytu + skan IL QuestPay8 + stale zwiadu,
  E narzedzie progow. Rownolegle BIEG 3 (120 dob na e9dfa7f, komplet bez poprawek rundy 2). Potem: scalenie p2-a..e do zbior-e8, test koncowy, wgranie.
- (08:10 UTC 13.10) **BIEG 3 (komplet e9dfa7f z O2-O6, bez poprawek rundy 2): 120/120, Armoury 0 ERROR.** Optymalizacje dzialaja i nie zmieniaja liczb: rozjazdy R9 = 0 (warsztaty 647 tys.
  probek, wywoz 10.7 tys.), zapis logu 74.6 -> 5.0 ms/dobe, wywoz kupcow 235 -> 128 ms, WorkshopLaw.CyclePrefix 806 (Z33) -> 601 ms/dobe (prog 500 - NIE), takt doby mediana 1138 -> 875 ms
  (maks. 1081; prog 1000 - 5 dob ponad), nasze mody razem 4.37 -> 3.92 s/dobe. Log Armoury-2026-10-13_00-22-02.log; progi test\progi-e8r2\.
  **RUNDA POPRAWEK 2 SKONCZONA I SCALONA: `w-toku/zbior-e8` = 8547369** (wf_521dc7fa-0d5; 5 grup, kazda autor + recenzent - wszystkie przyjete): A `p2-a` 007512e (NotableCaravanCap = 2 -
  postfiks DefaultCaravanModel.CanHeroCreateCaravan w kampanii; CaravanTownHourlyMaxTicks = 12; juczne karawan morskich; licznik tikow BK), B `p2-b` 1cea414 (CrashScribe Net.cs: finalizery
  bez __originalMethod - proba poza gra: 70-390 ns i 240 B na wywolanie -> 1.4 ns i 0 B, metoda przy wyjatku wlasciwa 63/63 + 55/55), C `p2-c` 6e03880 (PlainSaddleEveryRealm, zamowienie
  siodel + AiSaddleShelfFloor = 1, HorseFate uprzaz/konie bez dubla + siodla z siedziby, CrownRentServiceAfterFullDays), D `p2-d` 1b9d0d3 (kopalnia BK gracza z drewnem, ksiega towarow
  a rabunek, Reset SaddleLaw/CrossingLaw, flaga dymarek nk1/nk0, opisy MCM, napisy W4, praca RC we wsi, skan IL QuestPay8 bez wyjatkow, zwiad 10/16), E `p2-e` 7153204 (narzedzie progow:
  16 x NIE -> TAK z uzasadnieniem, 3 -> INFO, 1 nowy wiersz renty). Scalenie: konflikt tylko CHANGELOG (783 naglowki), gen_mcm Armoury 1302 / RC 111 / GT 40, build 5 modow kod 0,
  weryfikacja mechaniczna 0 niezgodnosci. **W TOKU: TEST KONCOWY** - 120 dob nowej kampanii na 8547369 (Armoury e69adbd3, CrashScribe 142fdd4a [test: CS-at 8c13a935], RC f9a944c1,
  GT 035878c4, ForgeView e17e0233; test\at-fin1-120.out.txt), potem zapis 9 dob; wgranie - jesli 0 bledow i czas doby nie gorszy niz W33.
- (08:45 UTC 13.10) **TEST KONCOWY (zbior-e8 8547369, 120 dob nowej kampanii): 120/120, Armoury 0 ERROR, CS 0 blokow naszych modow, 23 znane wyjatki startowe cudzych modow (3 wyjatki skanu IL
  znikly). CZAS DOBY 11.8 s (W33 17.5; bieg 2 21.2; bieg 3 20.2) - gra o 1/3 SZYBSZA niz WGRANIE 33.** Nasze mody razem 1.17 s/dobe (bylo 4.37), takt doby mediana 316 ms (bylo 1138).
  Log Armoury-2026-10-13_01-06-48.log, CS session-2026-10-13_01-06-46.log, zapis autotest-2026-10-13-0131.sav; progi test\progi-fin1\ (NIE: etap2 1, etap3 1, etap4 1, etap5 17, etap7 2, etap8 7).
  POPRAWILO SIE: wojsko lordow w wojnie 86 917 (nad podloga; W33 87 027), T9 91 113 (W33 90 326), kasy miast 9.70 mln (94.5% Z33), rody na minusie 191 (Z33 197), renty korony 0 rodo-dob
  bez renty, zwiad AI 0.52, siodla z warsztatow 46.5/d (bylo 25.7; Z33 13.4), jezdzcy z koniem bez uprzezy 11.9% (bylo 20.5; prog 10), jazda z para 5.18% ludzi (Z33 z koniem 4.60%),
  koszt tikow BK karawan 47 ms/dobe (bylo 1411), sredni postoj karawan w miescie 3.1 h (przed E8/G1 ok. 3 doby), juczne z niczego 0.
  **POGORSZYLO SIE (skutek limitu karawan 2 - 500 karawan zamiast 962 w Z33, wjazdy do miast 98/d zamiast 138):** miasta bez zelaza 44.3 (bieg 2: 33.4; W33 26.7), miasta bez rudy 43
  (Z33 25.8), dymarki bez wegla 7.0% (prog 5), cykle warsztatow "brak drewna" 229/d (prog 177). Zostaja znane: jazda t3+ 64% celu K3 (czeka na siodla), Dothrakowie czekajacy na konia
  29.6%, kolejka odziezy wojska i skory, sakiewki 22 dni zoldu, reszta koni w ksiedze (K1-3), port Saath 37 dob bez nadwyzki, F8-2 2.8%.
  **W TOKU:** zapis 9 dob na 8547369; potem BIEG PORownawczy z NotableCaravanCap = 3 (wariant probny Armoury d8a50add, drzewo SCRATCH(dbc3d3d1)\cap3, bez commita) - czy trzecia karawana
  kupca przywraca dowoz zelaza i rudy do poziomu W33 i ile kosztuje czasu; potem wybor limitu i wgranie.

**WGRANIE 34 (13.10 ok. 09:15 UTC; CALY KOD ETAPOW 5-8 + poprawki po audycie i trzech biegach):** Armoury e69adbd3, CrashScribe 142fdd4a, RealisticCaptivity f9a944c1, GrandTourney 035878c4,
ForgeView e17e0233 (galaz `noc/wgranie-34` = `w-toku/zbior-e8` 8547369) + pliki poza DLL: Armoury\GUI\Prefabs\ArmBurnedVillageLabels.xml, ArmMapVillageTooltip.xml, Armoury\ModuleData\
arm_map_roads_probe.tsv, arm_map_village_lore.tsv, Armoury\Prefabs\arm_map_roads.xml, CrashScribe\ModuleData\CrashScribe.settings.xml. Skrypt `tools/wgranie/wgraj.ps1` (md5 kazdego pliku OK).
Kopie poprzedniej wersji (WGRANIE 33: abe15b3b / 842f64a4 / 9cdc53ee / bbb841d7 / b058c7d2): obok plikow `*.bak-2026-10-13-noc34-przed` + `D:\Backup-Bannerlord\wgrane\2026-10-13-noc34-przed`.
**Pliki ustawien Jeffa zmienione przy wgraniu (decyzje Jeffa 09.10 i 12.10; kopie `*.bak-2026-10-13-noc34` obok + D:):** Armoury.json - usunieta linia `"CampBattlePropsEnabled": false`
(dziala domyslna true z DLL); DiplomacySettings_v1.2.json - rok 364: min. wojna 21 -> 91, przerwa przed nowa wojna 21 -> 91, min. sojusz 42 -> 182, pakt 84 -> 364, przerwa miedzy wojnami
domowymi 240 -> 1000 (koniec suwaka; 1040 poza zakresem), frakcja 120 -> 520; better_economy_settings.xml (skrypt tools/bee/zamknij-ujscia-bee.ps1, E8/E): EconomicEventDailySpawnChance
0.14 -> 0 (SHA-256 po: 114635f1...c10e1; kopia `.bak-2026-10-13-przed-BEE-ujscia`; 13 kluczy ujsc bylo juz zamknietych).
**TESTY (autotest):** 120 dob nowej kampanii - 120/120, Armoury 0 ERROR, CS 0 blokow naszych modow, **11.8 s/dobe (W33 17.5 - o 1/3 szybciej)**, log Armoury-2026-10-13_01-06-48.log;
zapis 9 dob z konca biegu - 129/129, 0 ERROR, 11.6 s/dobe (Armoury-2026-10-13_01-35-30.log). Bledy CrashScribe = 22-23 znane wyjatki startowe cudzych modow (jak w W33).
Zima i bieg dlugi NIE robione (zima - po wgraniu calosci, jak chcial Jeff; bieg dlugi - nastepny krok). Zdjecia proby drog NIE robione (MapRoadsProbeEnabled domyslnie false).
ZATWIERDZONE: Armoury e69adbd3, CrashScribe 142fdd4a, RealisticCaptivity f9a944c1, GrandTourney 035878c4, ForgeView e17e0233.

- (09:15 UTC 13.10) **Porownanie limitu karawan kupca (NotableCaravanCap) - bieg 120 dob z limitem 3 (wariant probny, niewgrany; Armoury-2026-10-13_01-41-11.log):** czas doby 11.9 s
  (limit 2: 11.8 - bez roznicy); karawan 638 (limit 2: 500; W33: 962); wjazdy do miast 164/d (limit 2: 98; W33: 138); wojsko w wojnie 88 171 (86 917); kasy miast 91.4% Z33 (94.5%);
  miasta bez zelaza 45.1 (44.3), bez rudy 43.3 (43.0) - BEZ poprawy; dymarki bez wegla 13.8% (7.0%), "brak drewna" w warsztatach 284/d (229). WGRANE Z LIMITEM 2 (przetestowany takze
  zapisem). **DO DECYZJI JEFFA (kierunek):** 2 = mniej druzyn na mapie i mniej handlu niz w W33; 3 = handel o 19% wiekszy niz w W33 przy tym samym czasie doby. Suwak MCM "Notable Caravan Cap".
- **OTWARTE PO WGRANIU 34 (nastepne kroki):** (1) ZELAZO: miasta bez zelaza 44 (W33 27), bez rudy 43 (26) - nie zalezy od liczby karawan; do rozpoznania lancuch R1 lupa -> miasto
  (kontrakty 174, wozy wsi); (2) jazda t3+ 64% celu K3, jezdzcy z koniem bez uprzezy 11.9% (prog 10), Dothrakowie czekajacy na konia 29.6% - trend po kolejnym biegu, potem
  AiMountMarketSharePercent 25 -> 40 (TRIAZ-2 "K3-8" pkt 3); (3) K1-3a krok 2 - mieszczanie tylko konie robocze + czynne ciecie hodowli (decyzja Jeffa 12.10, licznik F3 juz mierzy);
  (4) E8/O4 musztra (po tescie); (5) kolejka odziezy wojska i skory (W1-7, N1-2/3/6), sakiewki 22 dni zoldu (A3-2), port Saath 37 dob bez nadwyzki, reszta koni w ksiedze (K1-3),
  unikat cersei_red_dress znika z targu (F1-3), F8-2 2.8% przejsc poza strefa mostu; (6) bieg dlugi (364-728 dob) + zima; (7) zdjecia proby drog i decyzja "ile drog"; (8) stawki myta
  na Bliznakach z licznika E7/B (Jeff: "chce, ale policz"); (9) reczne testy Jeffa 8.16 (29 punktow, projekt etapu 8) - sciezki gracza, ktorych autotest nie cwiczy: okienko rabunku R2,
  menu okregu, dymek wioski, kuznia, nagrody zadan, turniej, brama awansu na jezdzca (siodlo), rekrut gracza, dary Spoils, niewola; (10) zalozenia E8/H do potwierdzenia: bohaterowie
  poza regula uprzezy, zold jazdy bez zmian, zalogi nie kupuja uprzezy, 4 wzorce ROT bez uprzezy; (11) czy kupcy maja wozic unikaty miedzy miastami.
- (09:20 UTC 13.10) **Bieg kontrolny PO WGRANIU 34 (stan z gry + nowe ustawienia Jeffa; 10 dob nowej kampanii): 10/10, Armoury 0 ERROR, 10.7 s/dobe** (Armoury-2026-10-13_02-10-48.log).
  Linia "CampScene (E7/O2): prefaby dostepne - namioty 5/5, ogniska 3/3, pochodnie 2/2, plot 2/2" JEST (po usunieciu klucza z Armoury.json). Po biegu w grze WGRANIE 34 (md5 sprawdzone),
  ustawienia Diplomacy 91/91/182/364/1000/520 na miejscu. Limit konta na koniec nocy: tydzien ok. 35-40%.

**WGRANIE 35 (13.10 ok. 10:45 UTC; decyzja Jeffa "1. 3" - limit karawan kupca 3):** Armoury 527a9d2b (galaz `noc/wgranie-35` = `w-toku/zbior-e8` ca4aa87: jedyna zmiana wobec WGRANIA 34 to
domyslna NotableCaravanCap 2 -> 3). Reszta bez zmian: CrashScribe 142fdd4a, RealisticCaptivity f9a944c1, GrandTourney 035878c4, ForgeView e17e0233, pliki danych i ustawienia jak w WGRANIU 34.
Testy: bieg 120 dob z limitem 3 (kod identyczny - diff pusty; Armoury-2026-10-13_01-41-11.log: 120/120, 0 ERROR, 11.9 s/dobe) + zapis 9 dob na wgranym DLL (wczytanie autotest-2026-10-13-0206,
129/129, 0 ERROR, "Notable Caravan Cap 3" w logu; Armoury-2026-10-13_03-39-09.log). Kopia: `Armoury.dll.bak-2026-10-13-noc35-przed` + `D:\Backup-Bannerlord\wgrane\2026-10-13-noc35-przed`.
ZATWIERDZONE: Armoury 527a9d2b, CrashScribe 142fdd4a, RealisticCaptivity f9a944c1, GrandTourney 035878c4, ForgeView e17e0233.

- (10:45 UTC 13.10) **Jeff po raporcie: (1) limit karawan 3 - WGRANE (35); (3) kupcy woza unikaty miedzy miastami - TAK, zostaje jak jest; (2) zalozenia E8/H - "nie rozumiem"
  -> wyjasnione prostym jezykiem, czeka na odpowiedz; (4) mieszczanie i konie robocze - "wyjasnij czemu robocze i po co kupuje te konie" -> wyjasnione, czeka na odpowiedz.**
- (10:45 UTC 13.10) **BIEG ROCZNY na WGRANIU 34 (limit 2; 364 doby nowej kampanii): 364/364, Armoury 0 ERROR, srednio 13.0 s/dobe**, log Armoury-2026-10-13_02-15-21.log (113 MB), zapis
  autotest-2026-10-13-0335.sav; progi z oknem 364: SCRATCH(dbc3d3d1)\test\progi-rok364\ (NIE: etap2 3, etap4 3, etap5 25, etap8 6). **W pierwszym roku NIE MA ZIMY** (narzedzie: "363 doby
  bez zimy") - proba zimy wymaga osobnego biegu. **NAJWAZNIEJSZE: wojsko lordow w wojnie spada w ciagu roku do 64 013 (srednia ostatnich 28 dob; ostatnia doba 62 043; w dobach 90-120
  bylo 93 392; podloga etapu 2: 85 tys.)** - przyczyna nieznana, do rozpoznania przed dalszymi zmianami. Dobre: kasy miast d364 10.86 mln (105.8% Z33 d120), rody na minusie 159 (Z33 197),
  jezdzcy z koniem bez uprzezy w calym roku 8.3% (prog 10 - podaz siodel nadgania), jazda z para 5.76% ludzi (partie rodow 8.85%), nasze mody 1.33 s/dobe, takt doby mediana 354 ms.
  Zle: miasta bez zelaza 41.4 (W33 27), jazda t3+ 64% celu K3 po roku (cel >= 90%), karawan 439 w d364 (limit 2).
- (11:10 UTC 13.10) **Jeff - odpowiedzi po wyjasnieniach:** (2) zalozenia E8/H "ok zgadza sie" - POTWIERDZONE wszystkie cztery: bohaterowie poza regula uprzezy; zold jazdy bez zmian (premia
  wedlug typu jednostki, takze bez pary); zalogi nie kupuja uprzezy osobno i nie maja bramy; mamut olbrzyma i jednorozce Skagosu jada bez uprzezy (wielblady, slonie, smoki poza regula).
  (4) mieszczanie tylko konie robocze - "tak robocze i to dobra zasada" -> **K1-3a KROK 2 W TOKU: wf_e7ce012a-8c7** (docs/przekazanie/wf-k1-3a-konie-robocze.js; tor M: autor + jedna
  recenzja) w drzewie zb\k13a, galaz `w-toku/k13a-konie-robocze` od ca4aa87: klucze TownsfolkBuyOnlyWorkHorses = true (budzet BK na konie 0 + wlasny zakup: 1 kon roboczy na 25 ludzi,
  15 lat, najtanszy juczny / zwykly z polki osady, placi kiesa ludu) i HorseBreedingCutLive = true (ciecie hodowli czynne). Potem: scalenie do zbior-e8, autotest 120 dob + zapis, wgranie.
  **Rownolegle wf_b57efd4d-9ac (triaz 3, tylko odczyt; docs/przekazanie/wf-triaz-3-bieg-roczny.js):** (a) wojsko lordow topnieje w roku do 64 tys., (b) zelazo i ruda nie docieraja do miast.
  Logi z nocy zabezpieczone: `D:\Backup-Bannerlord\logs\2026-10-13-noc34-35` (621 MB; gra trzyma tylko 12 ostatnich logow Armoury - log wzorca W33 juz sie przewinal).
- (11:25 UTC 13.10) **Jeff: "rob dalej to co mozesz w tym samym czasie".** CZTERY WATKI NARAZ (limit 5 h: 2%, tydzien 45%):
  (1) wf_e7ce012a-8c7 K1-3a krok 2 konie robocze mieszczan (zb\k13a, `w-toku/k13a-konie-robocze`); (2) wf_b57efd4d-9ac triaz 3 - wojsko w roku i zelazo (odczyt);
  (3) wf_aec5979e-7e6 (docs/przekazanie/wf-o4-i-rozpoznanie-z3.js): E8/O4 musztra w zb\o4 (`w-toku/e8-o4`, autor + recenzent; "pominieta", jesli zysk < ok. 30 ms/dobe) + rozpoznanie Z.3
  cudzy kod (ROT malzenstwa 9.4% klatki itd.) -> docs/zakonczenie/ROZPOZNANIE-Z3-CUDZY-KOD-2026-10-13.md; (4) GRA: PROBA ZIMY 120 dob na WGRANIU 35 (plik-przelacznik
  Documents\...\CrashScribe\armoury-proba-zimy.flag kladziony i usuwany przez tools/wgranie/bieg-zima.ps1; wynik test\at-w35-zima120.out.txt).
  KOLEJKA GRY po zimie: test K1-3a (120 dob + zapis) -> wgranie; potem bieg 2-letni.
- (11:35 UTC 13.10) **Jeff - zgloszenie (drobny szczegol):** "jak mamy 23:59 i 2 day of XYZ i godzina przejdzie 00:01, powinien byc pokazywany nastepny dzien, a teraz dopiero okolo 10-12
  w poludnie zmienia sie dzien; plus dalbym 8 rok i 256 dzien autumn etc., bo 3659 dzien ciezko sie czyta". -> **wf_f008eb9b-7f9** (docs/przekazanie/wf-kalendarz-data.js; autor + recenzent)
  w drzewie zb\kal, galaz `w-toku/kalendarz-data` od ca4aa87: (1) dzien w dacie dla gracza zmienia sie o polnocy (tylko wyswietlanie - bieg czasu i tiki dobowe bez zmian), (2) czytelna data
  "Year N, Day N, pora roku" zamiast surowego numeru dnia, jedna funkcja formatujaca; dowod do autotestu: linia "Data na pasku (kalendarz)" co godzine przez 3 doby.
  DO POTWIERDZENIA U JEFFA: rok w dacie = rok swiata ROT (jak gra pokazuje dzis) czy rok kampanii (1, 2, 3...).
- (11:50 UTC 13.10) **E8/O4 musztra - POMINIETA (pomiar):** wspolny odczyt pozycji lordow dalby < 1 ms/dobe, cala paczka 16-22 ms/dobe (prog 30). Prawdziwy koszt musztry to trening
  114 ms/dobe, z tego ok. 96 ms liczenie nadwyzki K1 w kontekscie partii (Drill.AiSurplus -> MenPurse.AiMeleeLeft + 11 x AiSurplusPlan) - wspolny kod ze sprzedaza nadwyzek; najpierw stoper.
  **Rozpoznanie Z.3 GOTOWE:** `docs/zakonczenie/ROZPOZNANIE-Z3-CUDZY-KOD-2026-10-13.md`: (1) swaty ROT CheckNPCMarriages - 9.4% klatki (ok. 1.2 s/dobe), latka CrashScribe "te same pary,
  ten sam ciag losowy" szac. -0.8..1.0 s/dobe; (2) kafelki druzyn pod tabliczka miasta (wyglad, S); (3) pomiar wnetrza warsztatow i treningu (0.8 s/dobe razem z naszymi latkami);
  NIE ruszac: AI lordow, finanse rodow, silnik. Uwaga dla Jeffa: w ROT zonaci sa na liscie swatow, przez co sluby NPC sa rzadsze, niz zamierzal ROT - zostawione bez zmian.
  **W TOKU wf_4901dbf6-079** (docs/przekazanie/wf-z3-swaty-i-pomiary.js): swaty ROT w zb\z3 (`w-toku/z3-swaty-rot`, CrashScribe; klucze MarriageFast = true, MarriageFastShadow - bieg cienia
  z licznikiem roznic) + pomiary w zb\pm (`w-toku/z3-pomiary`, Armoury DayProfile/Drill).
- (12:20 UTC 13.10) **PROBA ZIMY na WGRANIU 35 (120 dob, kampania zaczyna sie zima od 9. doby): 120/120, Armoury 0 ERROR, 11.8 s/dobe** (Armoury-2026-10-13_03-52-01.log; flaga
  armoury-proba-zimy.flag usunieta po biegu; w grze WGRANIE 35). Zima: ciecie plonu wsi srednio 40.8%, zywnosc wsi -41% wobec startu i stoi (d30/d60/d120), zadna warownia AI nie
  gloduje przed 30. doba zimy (F6 TAK), mediana zapasu warowni 1949-2057 dni (F7 TAK), zmarli z glodu 0. Wojsko lordow w zimie d90-120: 80 353 (podloga 85 tys. - NIE; latem 91-93 tys.).
- (12:20 UTC 13.10) **Jeff: rok w dacie = "rok swiata tak jak gra pokazuje"** - zgodne z tym, co robi wf_f008eb9b-7f9 (kalendarz).
- (12:30 UTC 13.10) **Jeff o slubach NPC w ROT ("zonaci na liscie - sluby rzadsze, niz zamierzal ROT"): "mozemy to poprawic??" -> TAK, DO ZROBIENIA jako osobna paczka PO recenzji latki
  szybkosci swatow (wf_4901dbf6-079, drzewo zb\z3, galaz `w-toku/z3-swaty-rot`):** nowy klucz CrashScribe (np. MarriageOnlyFreeCandidates = true; off = losowanie ROT jak dotad) - dzienna
  proba lorda losuje kandydata tylko sposrod WOLNYCH (bez malzonka), zamiast przepadac po trafieniu w zonatego; linia doby: proby, przepadle przez zonatego (stara regula), sluby. To zmiana
  rozgrywki (wiecej slubow i dzieci NPC) - decyzja Jeffa; rownowaznosc "te same pary" dotyczy tylko trybu z kluczem wylaczonym. Po pierwszym biegu podac Jeffowi liczby (ile slubow wiecej).
- (12:40 UTC 13.10) **PROFIL KLATKI na WGRANIU 35 (10 dob od doby 120; SCRATCH(dbc3d3d1)\test\profil-w35.txt):** klatka srednio 59.3 ms (przed runda poprawek 2: 163.7 ms). Wedlug modow:
  Native 35%, silnik 22%, ROT-Core 15%, **Armoury 11% (6.0 ms; bylo 27.7 ms = 18%)**, BannerKings 9%, StrategicCampaignAI 2%, CrashScribe 1%. Najdrozszy sluchacz: swaty ROT
  CheckNPCMarriages 7.98 ms/klatke = 14.4% klatki (cel latki Z.3 - w toku), potem AiMilitaryBehavior 5.4%, takt doby Armoury 3.4%.
- (13:10 UTC 13.10) **TRIAZ 3 GOTOWY (wf_b57efd4d-9ac): `docs/test-zbiorczy/TRIAZ-3-bieg-roczny-2026-10-13.md`** (streszczenia prostym jezykiem na gorze kazdego obszaru).
  WOJSKO (62 tys. po roku): topnieje przez PIENIADZ (pulap zoldu 166), nie przez ksiege ludzi (ludzi ubylo 0.27%, ochotnicy stoja u notabli). Przyczyny: weteran drozszy o 1/3; wojna
  nie konczy sie, oszczednosci rodow wypalone w pierwszych miesiacach; budzet odprawia najtanszych (O1-3 STROJENIE, broni sie); pulapka: biedny rod < 60% druzyny nie jest wzywany do
  armii -> brak sluzby -> renta korony przepada bogatym -> druzyna 0 (O1-2 BLAD - podwazenie: stan prawdziwy, ale przyczyna i poprawka do korekty - brac "Poprawka ostateczna");
  kompanie najemne zbieraja zloto bez wydatku 2.9 -> 20.6 mln (O1-4 BLAD, broni sie); zloto odplywa do kies ludu +29 mln/rok (O1-7 STROJENIE). Bez zmian swiat zejdzie do ok. 50 tys.;
  po poprawkach szac. 72-82 tys. (stare 120 tys. trzymalo ok. 0.6 mln zl/d z niczego). ZELAZO (41-45 miast bez): zelaza nie brakuje (14.5 tys. ladunkow lezy), brak ogniwa
  miasto z nadwyzka -> miasto bez (kontrakty 174.2 martwe od krotkiego postoju karawan, zasieg, wyspy nigdy nie dostawaly - O2-1 BLAD, poprawka do korekty wedlug podwazenia);
  regulator wsi przykreca kopalnie, bo liczy martwy zapas (O2-2, broni sie); zamki = studnia (woz uciekajacy sprzedaje caly ladunek - O2-3, broni sie); 1 474 lupy znikaja z taborow (O2-7).
  **PLAN: dwie paczki poprawek rownolegle (wojsko: ClanBudget/CrownRents/kompanie; zelazo: zamowienie wozem kupcow z miasta z nadwyzka, regulator, zamki) - autor + recenzent, potem bieg roczny.**
- (13:10 UTC 13.10) **K1-3a krok 2 ZAKODOWANE I ZRECENZOWANE, NIE WGRYWAC BEZ SLOWA JEFFA:** `w-toku/k13a-konie-robocze` 137dbc3 (zb\k13a). Pomiar: mieszczanie zdejmuja 343 konie/d (100 zwyklych,
  36 bojowych, 4.6 szlachetnego, 203 juczne) za 194 tys. zl/d (miasta 153, zamki 42) - 2.6-3.2x wiecej niz liczby, na ktore Jeff sie zgodzil (150 koni, 60-75 tys.). Po regule: kasy miast
  -41% wplywu od ludu, zamkow -80%, zawor panow i koron -100..170 tys./d, utarg stadnin do -200 tys./d, hodowla do ok. 1/4; JUCZNE bez rownowagi (naplyw z niczego 105-156/d z taborow wsi,
  odbior 24/d) - potrzebna paczka "juczne z niczego". Jeff powiadomiony, zapytany o paczke jucznych; plan: bieg 120 dob na probnym DLL (zb\k13a\Armoury\bin\Release) dla zmierzonych skutkow.
- (13:10 UTC 13.10) **Kalendarz GOTOWY:** `w-toku/kalendarz-data` 7392436 scalone do zbior-e8 74a3645 (Armoury 65868156; klucz ReadableDateEnabled; "Year 299, Day 19, Summer - 11:30";
  dzien zmienia sie o 0:00; przyczyna: WesterosClimate.ToStringPrefix liczyl doby od startu pory). Test 40 dob + zapis w toku (test\at-fin3-40.out.txt) -> WGRANIE 36.
- (13:25 UTC 13.10) **W TOKU wf_a30e8861-78e - RUNDA POPRAWEK 3** (docs/przekazanie/wf-poprawki-runda-3.js; specyfikacja TRIAZ-3, wiazaca "Poprawka ostateczna"): dwa drzewa od zbior-e8
  74a3645: zb\woj `w-toku/p3-wojsko` (W1 renta korony a rod bez srodkow, W2 kolejnosc zwolnien, W3 zloto kompanii najemnych, W4 male pewne z O1-7) i zb\zel `w-toku/p3-zelazo`
  (Z1 zamowienie zelaza przewozem z miasta z nadwyzka, Z2 regulator wsi a martwy zapas, Z3 zamki-studnia, Z4 licznik lup znikajacych z taborow); kazda autor + recenzent.
  Test daty: 40 dob OK (0 ERROR, 10.9 s/dobe; dowod: 23:23 "Year 299, Day 1", 00:21 "Year 299, Day 2", stary wzor stal na 3641), zapis w toku. Rownolegle wf_4901dbf6-079 (swaty ROT + pomiary).
  KOLEJKA: WGRANIE 36 (data) -> bieg 120 dob K1-3a na probnym DLL (pomiar skutkow dla Jeffa) -> po rundzie 3: scalenie, bieg roczny.

**WGRANIE 36 (13.10 ok. 13:30 UTC; zgloszenie Jeffa - data na pasku):** Armoury 65868156 (galaz `noc/wgranie-36` = `w-toku/zbior-e8` 74a3645 = WGRANIE 35 + `w-toku/kalendarz-data`): dzien
w dacie dla gracza zmienia sie o 0:00; format "Year 299, Day 19, Summer - 11:30" (rok swiata - decyzja Jeffa; klucz ReadableDateEnabled), powiadomienia "the 256th day of the year (Autumn)",
Zelazny Bank "due by Year 299, Day 183". Reszta bez zmian (CrashScribe 142fdd4a, RC f9a944c1, GT 035878c4, ForgeView e17e0233). Testy: 40 dob nowej kampanii 40/40, 0 ERROR, 10.9 s/dobe
(Armoury-2026-10-13_04-30-18.log; dowod: 23:23 "Year 299, Day 1", 00:21 "Year 299, Day 2"), zapis 9 dob 129/129, 0 ERROR. Kopia: `Armoury.dll.bak-2026-10-13-noc36-przed` + D:.
ZATWIERDZONE: Armoury 65868156, CrashScribe 142fdd4a, RealisticCaptivity f9a944c1, GrandTourney 035878c4, ForgeView e17e0233.
- (13:50 UTC 13.10) **Jeff o zlocie w kiesach ludu (+29 mln/rok): "co zrobic z kasa, ktora lezy; w prawdziwej ekonomii pieniadz cyrkuluje i ma zamkniety obieg - ludzie moga oszczedzac,
  ale tez wydaja, inwestuja itp. Jak to zrobic w ramach silnika gry, aby mialo to sens?"** -> **wf_d4abe57d-6c8 PROJEKT BEZ KODU** (docs/przekazanie/wf-projekt-obieg-kiesy.js; tor L):
  rozpoznanie obiegu (docs/kiesa-ludu/ROZPOZNANIE-OBIEG-KIESY-2026-10-13.md) -> trzy niezalezne projekty (WYDATKI wedlug zamoznosci / INWESTYCJE w miasto, warsztaty, karawany /
  POZYCZKI dla panow i podatek nadzwyczajny - wzor IronBank, DebtLadder) -> sedzia sklada jeden: docs/kiesa-ludu/PROJEKT-OBIEG-KIESY-LUDU-2026-10-13.md z rozdzialem dla Jeffa i pytaniami
  o kierunek. Kod dopiero po "tak" Jeffa. Kierunek podany Jeffowi od reki: zapas oszczednosci w dniach koszyka, ponad nim lud po kolei wydaje lepiej, inwestuje w swoje miasto, pozycza panom.
  Jeff zapytal tez o rozwiazanie na wojsko - opisane (5 punktow z TRIAZ-3; kod w toku wf_a30e8861-78e). W grze: bieg pomiarowy K1-3a 120 dob (test\at-k13a-120.out.txt).
- (14:05 UTC 13.10) **Z.3 swaty ROT GOTOWE (kod + recenzja, bez testu w grze):** `w-toku/z3-swaty-rot` c64c3d2 (zb\z3; CrashScribe/src/MarriageFast.cs, klucze MarriageFast = true,
  MarriageFastShadow = false): te same pary i ten sam ciag losowy - dowod poza gra na prawdziwej DLL: 57 493 wywolania rodu, 20 299 par w obu drogach, 0 roznic; szac. zysk ok. 63% kosztu
  swatow (ok. 0.7 s/dobe; cel < 10% nieosiagniety - dalej tylko latkami na Hero.CanMarry / Romance.GetCourtedHeroInOtherClan, osobna decyzja). DO TESTU: najpierw BIEG CIENIA (probna CS
  z MarriageFastShadow = true - linia "Swaty ROT (Z.3): ... roznic R" ma dac 0). **Z.3 pomiary GOTOWE:** `w-toku/z3-pomiary` 58a5148 (zb\pm; DayProfile + Drill: wnetrze warsztatow
  i treningu gry, stoper nadwyzki K1 w musztrze - pola na koncu linii profilu). **W TOKU wf_16d6890e-484** (docs/przekazanie/wf-sluby-npc.js) na zb\z3: prosba Jeffa - sluby NPC:
  najpierw dowod, gdzie proby przepadaja (uwaga: zonaty lord SZUKAJACY ma szanse 0 - to nie strata; pytanie, czy przepadaja proby WOLNYCH przez kandydatow bez szans), potem klucz
  MarriageOnlyViableCandidates = true; jesli twierdzenie rozpoznania falszywe - "pominieta" z dowodem i powiedziec Jeffowi.
  KOLEJKA TESTOW W GRZE: bieg pomiarowy K1-3a (w toku) -> bieg cienia swatow (CS probna) -> po rundzie 3 (wojsko, zelazo): scalenie p3 + z3 + pm do zbior-e8, bieg roczny, wgranie.
- (14:25 UTC 13.10) **BIEG POMIAROWY K1-3a (120 dob, probny Armoury 967904a5 z zb\k13a = WGRANIE 35 + regula koni roboczych; NIEWGRANE): 120/120, 0 ERROR, 12.1 s/dobe**
  (Armoury-2026-10-13_04-46-00.log; progi test\progi-k13a\; porownanie z biegiem limitu 3 bez reguly test\progi-cap3\). Regula dziala: mieszczanie kupuja 12.3 konia/d (prawie same juczne),
  koszyk BK koni 0, ksiega koni zgodna, ciecie hodowli czynne (d57: zwykly x0.50, bojowy x0.51), koszt 0.10 ms/d. SKUTKI: **wojsko lordow w wojnie 73 555 wobec 88 171 bez reguly (-14.6 tys.,
  ponizej podlogi 85 tys.)**, kasy miast 83.1% Z33 wobec 91.4%, zawor panow z miast 148 tys./d; zakupy koni lordow 62.4/d (55.6), jazda t3+ 19.0% (17.5%; 69% celu K3 wobec 65%), jazda
  z para 5.86% ludzi (5.29%), jezdzcy z koniem bez uprzezy 13.3% (10.7%). WNIOSEK: regula daje troche wiecej jazdy, ale zabiera panom ok. 15 tys. ludzi, bo zloto za konie zostaje
  w kiesach ludu - **NIE WGRYWAC przed paczka obiegu kiesy ludu (wf_d4abe57d-6c8); kod czeka na galezi `w-toku/k13a-konie-robocze` 137dbc3.** Jeff powiadomiony.
- (14:50 UTC 13.10) **RUNDA 3 GOTOWA I SCALONA: `w-toku/zbior-e8` = b9e4d36** (WGRANIE 36 + `w-toku/p3-wojsko` a025d82 + `w-toku/p3-zelazo` 301b98a + `w-toku/z3-pomiary` 0e6bd4c; konflikty
  tylko CHANGELOG i sprawdz_logi - suma; gen_mcm Armoury 1315; build kod 0; NIEWGRANE). WOJSKO (6 kluczy): CrownRentServiceAtMeans (rod bez srodkow na druzyne do progu wezwania ma
  sluzbe zaliczona - lapie 48 z 54 rodow bez sluzby), CrownRentContactOnlyWhenCalled (doba wojny tylko przy armii w polu / oblezeniu / napadzie na wlasna osade - przyczyna Polnocy),
  ReleaseAcrossTiers (zwolnienia losowo z calej partii), MercBkDownPaymentAiOff (zadatek BK dla kompanii AI wylaczony - 12 mln/rok), MercContractPaysMenUnderArms (wyplata <= 1.3 x zold dnia),
  CallToWarBreakRestoresWallet (2.0 mln/rok przestaje isc w nicosc). Oczekiwane w biegu rocznym: partie rodow 72-80 tys. (bylo 63). DO DECYZJI JEFFA (CHANGELOG W4): danina wojenna
  z kies ludu, min. czas sluzby kompanii 28 dob, prog wojska 85 tys. nie wyjdzie bez doplat. ZELAZO (klucze): TownMaterialOrderByCart (miasto bez surowca kupuje wozem z miasta / zamku
  z nadwyzka: lad do 600, morze port-port, kasa -> kasa; MaterialCarts.cs, zapis arm_matorders), ValveHoldWhileShort + ValveShortTowns 15 (regulator nie tnie towaru, ktorego brak
  w > 15 miastach), CastleCartFoodOnly (woz w zamku sprzedaje tylko brakujace jedzenie), SpoilsNonFoodToTown, LordDiscardToShelf. Progi: bloki etap8_p3woj, etap8_p3_zelazo.
  **W TOKU: test 120 dob na b9e4d36** (test\fin4, test\at-fin4-120.out.txt), potem bieg roczny i wgranie. Czeka: z3-swaty-rot (po paczce slubow NPC wf_16d6890e-484) - bieg cienia.
- (15:10 UTC 13.10) **SLUBY NPC GOTOWE (kod + recenzja, bez testu w grze): `w-toku/z3-swaty-rot` e7d8391** (klucz MarriageOnlyViableCandidates = true). DOWOD: twierdzenie prawdziwe -
  BKMarriageModel.IsSuitableForMarriage nie pyta o malzonka, zonaci sa losowani jako kandydaci, szanse zeruje dopiero IsMarriageAdequate: 39% dziennych prob wolnych lordow trafia
  w kandydata z malzonkiem + ok. 18% w odrzuconego progiem BK = ok. 57% prob przepada. Dzis 113 slubow AI-AI na rok (0.31/d); po poprawce szac. 180-192 (sufit puli: 208 wolnych kobiet
  na starcie). Test na prawdziwej DLL: klucz off - 56 992 wywolania, 0 roznic wobec ROT; klucz on - 0 par niemozliwych, rozklad rowny. **DO WIEDZY / DECYZJI JEFFA: wolni szlachcice
  (takze kandydatki dla gracza) koncza sie ok. 2x szybciej - po roku zostaje ok. 30-40 wolnych kobiet zamiast ok. 110; wylacznik = MarriageOnlyViableCandidates false.**
  Probne DLL do testu: SCRATCH(dbc3d3d1)\test\CrashScribe-at-swaty-cien.dll (a38b68ab: bieg cienia - decyduje ROT, linia "Swaty ROT (Z.3) ... roznic R" ma dac 0) i
  CrashScribe-at-swaty.dll (3da55349: reguly czynne). KOLEJKA GRY: fin4 120 dob (w toku) -> cien swatow 40 dob -> swaty czynne 120 dob -> scalenie z3 do zbior-e8 -> bieg roczny calosci -> wgranie.
- (15:45 UTC 13.10) **TEST 120 dob RUNDY 3 (zbior-e8 b9e4d36, Armoury f2afb329, NIEWGRANE): 120/120, 0 ERROR, 11.9 s/dobe** (Armoury-2026-10-13_05-47-42.log; progi test\progi-fin4\).
  **WOJSKO lordow w wojnie 101 144 (bez rundy 3: 88 171) - W PASMIE 95-115 tys., etap2 0 NIE; kasy miast 97.8% Z33; rody na minusie 180; MIASTA BEZ ZELAZA 25.9 (bylo 45.1),
  BEZ RUDY 19.3 (bylo 43.3) - lepiej niz W33.** Rody z lennem bez renty 7.2%, z 0 ludzi 7 z 214, zadatek BK wstrzymany 250 najmow (4.3 mln zostalo u krolow), kiesy kompanii +7.7 tys./d,
  dlug 168 w nicosc 0, wozy surowcow: kontrola 0, 1.4 ms/d. NIE: P3W1-2 (bez warunku przez sluzbe 17, prog 8), P3W2-1 (zold zwalnianego 4.47, prog 5.0) - do obejrzenia po biegu rocznym.
  **`w-toku/zbior-e8` = 9e2e0e6** (+ scalone `w-toku/z3-swaty-rot` e7d8391; CrashScribe do wgrania 3ca8aa08 = test\fin5). **W TOKU W GRZE (lancuch): bieg cienia swatow 40 dob
  (test\at-fin5-cien40.out.txt; pliki danych z MarriageFastShadow = true w SCRATCH\tmp\tree-cien) -> BIEG ROCZNY 364 doby calosci z regulami czynnymi (test\at-fin5-rok364.out.txt;
  Armoury f2afb329 + CS-at-swaty 3da55349).** Po nim: progi (wojsko d337-364 cel 72-80 tys., zelazo, sluby/rok), zapis 9 dob, WGRANIE 37 (Armoury f2afb329 + CrashScribe 3ca8aa08 +
  CrashScribe.settings.xml), jesli 0 bledow i liczby nie gorsze.
- (13:25 UTC 13.10 = 15:25 czasu polskiego; UWAGA: godziny wpisow z 13.10 powyzej oznaczone "UTC" to w praktyce czas polski, kolejnosc wpisow jest dobra) **PROJEKT OBIEGU KIESY LUDU
  GOTOWY (bez kodu; wf_d4abe57d-6c8 / wfjdtpxt8):** `docs/kiesa-ludu/PROJEKT-OBIEG-KIESY-LUDU-2026-10-13.md` (sedzia; rozdz. 0 prostym jezykiem, rozdz. 8 paczki, rozdz. 9 progi,
  rozdz. 11 pytania) + ROZPOZNANIE-OBIEG-KIESY + trzy projekty (WYDATKI, INWESTYCJE, POZYCZKI) - w repo, commit po ff7e490. WERDYKT: drabina nadwyzki - zapas ok. miesiaca zakupow
  (nietykalny) -> zbytki z polki -> budowa wlasnego miasta -> w wojnie danina 2.5%/d z oszczednosci ponad zapas do korony, ktora tego dnia placi rodom. Wdrazanie od konca:
  P1 (tor S) danina + pomiary -> P1b lojalnosc -> P2 lud buduje (bez "trzeciej dla pana") -> P3 lista dostatku -> P4 targ widzi sakiewke -> P5 podzamcza i wsie; pozyczka miejska,
  karta, sakiewka turniejowa "na slowo Jeffa". Uczciwy skutek: lokata 23.8 -> ok. 3 mln, kiesy miast ok. 15-16 mln zamiast 36.5 mln, do panow 50-65 tys. zl/d, wojsko tylko +1.5-2.5 tys.
  (wojsko odbudowuje runda 3). Regula koni roboczych (k13a 137dbc3) wchodzi dopiero z P1 i po biegu 120 dob z obiema. **CZEKA NA JEFFA: 6 pytan o kierunek (rozdz. 11) - podane mu
  w czacie; KODU P1 NIE ZACZYNAC przed jego "tak".** Kolizje do sprawdzenia przed P1: rozdz. 10 pkt 11 (FolkSurplusSpendShare w Armoury.json Jeffa; kolejnosc danina / oferta).
  W GRZE: lancuch z wpisu wyzej ruszyl 13:14 UTC (bieg cienia swatow 40 dob, potem bieg roczny 364 doby; koniec ok. 14:50 UTC). Limit 5 h: 83% (zeruje sie 15:50 UTC) - do tego czasu
  bez nowych workflow; po biegu rocznym: progi, zapis 9 dob, WGRANIE 37.

**WGRANIE 37 (13.10 16:17 UTC; runda 3: wojsko + zelazo, pomiary Z.3, sluby NPC):** Armoury f2afb329, CrashScribe 3ca8aa08, CrashScribe.settings.xml ef8cd722 (reszta bez zmian:
RealisticCaptivity f9a944c1, GrandTourney 035878c4, ForgeView e17e0233, 5 plikow danych Armoury). Galaz `noc/wgranie-37` = `w-toku/zbior-e8` **6d91156** (= 9e2e0e6 + sam tekst
komentarzy w CrashScribe.settings.xml i CHANGELOG). Kopie: obok plikow `*.bak-2026-10-13-noc37-przed` i `D:\Backup-Bannerlord\wgrane\2026-10-13-noc37-przed`; md5 po wgraniu OK.
Cofniecie do WGRANIA 36: Armoury 65868156 + CrashScribe 142fdd4a + settings cb8d2908 z tych kopii.
- **BIEG ROCZNY przed wgraniem (364 doby nowej kampanii, Armoury f2afb329 + CS-at-swaty 3da55349, reguly slubow czynne): 364/364, 0 ERROR, 12.0 s/dobe** (log
  `D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_06-24-10.log`, sesja `session-2026-10-13_06-24-09.log`, tabele `Logs\2026-10-13_06-24-10\`; progi
  SCRATCH(dbc3d3d1)\test\progi-rok5\). **Wojsko lordow w wojnie (sr. 28 dob) 79 409 wobec 64 013 w biegu rocznym WGRANIA 34** (ostatnia doba 73 578; podloga 85 tys. dalej NIE);
  zalegly zold w partiach 2.49% (3.17%); **miasta bez zelaza: od d91 srednio 9.1, ostatnie 30 dob 4.2 (przedtem 40.1 i 51-61); bez rudy d91-120 8.9 (39.8)**; sluby AI-AI 215 w roku
  (przedtem 113), czas swatow ok. 685 ms/d (oryginal ok. 1 450). NIE wedlug grup (teraz / poprzedni bieg roczny): etap2 2/3, etap3 1/0, etap4 4/3, etap5 27/25, etap7 2/0, etap8 9/6.
  Nowe NIE do obejrzenia (robi to workflow raportu): E4/3 pula wyrzutkow 11 869 w d364 (baza 3 053) i bandy 819 / 17 279 ludzi; E4/11 G5 (1 doba, d229); E4/9 K8 Straz (d175: 56);
  E7/O1 T3 2.3 na noc (prog 2.0), T4 4 noce z 333; etap3 rozjazd probek 2 doby; etap5: A1-T19, A1-T21, B3-6, N1-6, W1-7; etap8: C-4b (2), P3-2, Q-3 (1 partia), G1-5c, H-4b
  (reszta uprzezy -7 395 przy zrodlach 14 995), O1-2, O5-2, P3W1-6 (14 z 205, prog 12), P3W2-4 (1.38 x, prog 1.25).
- **Test zapisu:** wczytany `autotest-dlugi` (doba 360 biegu rocznego) + 9 dob: 0 ERROR, pula wyrzutkow i klimat wczytane, sluby licza dalej (test\at-fin5-zapis9.out.txt).
- **SWATY - POMIAR W GRZE (bieg cienia NIE dal 0):** 40 dob: 3 228 roznic stanu generatora w 13 303 wywolaniach. Dwa biegi z probna DLL z diagnostyka (CrashScribe-at-diag /
  -diag2; zrodlo niecommitowane w SCRATCH\cs-at): ta sama nasza droga puszczona 2 razy z tego samego stanu rozni sie w ok. 100 rodach/d; **`NpcCoupleMarriageChance` (BK) sama
  zuzywa 1 krok generatora w 130-156 wywolaniach/d; `IsCoupleSuitableForMarriage` i `CanMarry` - 0.** Czyli w grze "ten sam ciag losowy" jest nieosiagalny (BK losuje w ocenie
  szansy + pamieci dnia BK); regula i szanse te same, pary inne niz w samym ROT - jak miedzy dwiema rozgrywkami. Opis w settings.xml poprawiony; komentarze w Config.cs i naglowku
  MarriageFast.cs do poprawy przy najblizszej zmianie kodu CS. Nie ustalone: ktore wywolanie w BK losuje. Jeff powiadomiony.
- **DECYZJE JEFFA 13.10 (po pytaniach projektu obiegu kiesy):** 1 danina z oszczednosci TAK; 2 kara lojalnosci TAK (osobny krok po pomiarze); 3 budowa - "wyjasnij dokladniej"
  (wyjasnione, czeka na tak / nie); 4 zbytki TAK; 5 "tak" = pozniej, jak w radzie (zaznaczone Jeffowi); 6 konie robocze razem z danina po biegu 120 dob TAK.
  **Jeff NIE chce obnizac progu wojska 85 tys.: "to podatki trzeba gdzies zwiekszyc, na czas wojny" - prosi o propozycje.** Podane kierunki: (1) danina z oszczednosci, (2) korona
  placi wedlug potrzeby (najpierw rody w polu na pulapie zoldu), (3) tarczowe (rod z niepelna druzyna placi koronie, korona rodom walczacym), (4) stawka wojenna podatku pana
  z kosztem lojalnosci, (5) clo od kupcow - odradzone. Fakt z biegu: 143 rody (31 tys. ludzi) maja zold >= 0.9 pulapu, 164 rody (45 tys.) maja luz i niepelne druzyny - im podatki
  nie pomoga; przyczyna do ustalenia w raporcie. Liczby i projekt po raporcie rocznym.
  **Nowe pytania Jeffa:** jak dzis dziala lup po zdobyciu miasta ("powinno dac sie pladrowac prawdziwe bogactwo miasta"); raport z biegu rocznego ("ile wojska, ile zarabiaja
  rody i panstwa, czy gra jest spojna").
- **W TOKU (workflow, kopie skryptow w docs/przekazanie):** `wf-raport-roczny.js` (wf_bd6c538c-c31: 5 analiz + 5 weryfikacji + synteza -> docs/test-zbiorczy/RAPORT-ROCZNY-2026-10-13.md;
  robocze SCRATCH\raport-rok\), `wf-lup-miast.js` (wf_063e9114-c56 -> docs/lup-miast/LUP-ZDOBYTEGO-MIASTA-2026-10-13.md; SCRATCH\lup-miast\), `wf-kiesa-p1-danina.js`
  (wf_e6948e4c-524: paczka P1 na galezi `w-toku/kiesa-p1-danina`, drzewo SC\zb\p1 od 9e2e0e6: danina FolkWarAid 2.5% x s/3%, zdanie Savings, pomiary, progi). W grze: bieg kontrolny
  10 dob po wgraniu (test\at-w37-kontrola10.out.txt). PO NICH: test 120 dob P1; scalenie k13a (konie robocze) + drugi bieg 120 dob z obiema; projekt "podatki wojenne -> wojsko".
- (16:25 UTC 13.10) **Bieg kontrolny PO WGRANIU 37 (pliki z gry, 10 dob nowej kampanii): 10/10, Armoury 0 ERROR** (Armoury-2026-10-13_09-20-34.log); swaty "PODPIETY - decyduje nasza
  droga"; po biegu w grze WGRANIE 37 (md5: Armoury f2afb329, CrashScribe 3ca8aa08, settings ef8cd722). Workflowy raportu, lupu miast i paczki P1 dalej w toku.
- (16:45 UTC 13.10) **DECYZJE JEFFA (druga tura):** (1) budowa przez lud - pyta "a czemu mieliby dokladac, jaka jest za tym logika?" (odpowiedz dana: mury chronia ich oszczednosci,
  spichlerz przed glodem, targ / port to ich zarobek, 3/4 wraca do nich jako place; propozycja doprecyzowania: lud placi tylko za budowle sluzace miastu, wojskowe placi pan -
  CZEKA na tak / nie); (2) podatki na wojsko: "1 2 i 3" = korona placi wedlug potrzeby + tarczowe + stawka wojenna podatku pana - PROJEKTOWAC wszystkie trzy (workflow po raporcie
  rocznym); (3) sluby: **"Przyhamuj, aby sluby zawieraly sie rzadziej"** - klucz MarriageChanceScale 0.7 (cel ok. 150 / rok; bylo 113 przed zmiana, 215 po), w toku wf_44f307aa-c9f
  (docs/przekazanie/wf-sluby-hamulec.js) na galezi `w-toku/z3-swaty-hamulec` (drzewo SC\zb\sh od 6d91156), przy okazji poprawka nieprawdziwych komentarzy o ciagu losowym;
  (4) pomysly dodatkowe (pozyczka miejska, karta, sakiewka turniejowa) - POZNIEJ, potwierdzone.
  **Z analizy wojska raportu rocznego (SCRATCH\raport-rok\wojsko.md, przed weryfikacja):** luka do limitu druzyn to w 71% PULAP ZOLDU (regula budzetu ClanBudget.cs 191-210: zold <=
  0.60 dochodu stalego + 0.8 nadwyzki kiesy / 45 dni), nie brak ludzi; pulap zjadaja zalogi (83.6 tys. w zalogach, placone pierwsze; 16 rodow placi zalogom wiecej niz caly pulap);
  rody na pulapie (111) trzymaja 38% limitu, z wolnym miejscem (85) 64% = rownowaga AI; sufit swiata ok. 100 tys.; **do 85 tys. trzeba 30-45 tys. zl/d CELOWANE (do rodow na
  pulapie) albo 60-190 tys. dzisiejsza droga; do 95 tys. 80-115 tys. celowane.** Moje wczesniejsze zdanie Jeffowi "164 rodom podatki nic nie dadza" bylo za mocne - poprawione.
  Limity: 5 h 24%, TYDZIEN 77% (zeruje sie 19.10) - przy 99% przekazanie na drugie konto.
- (17:10 UTC 13.10) **JEFF: budowa przez lud - TAK** (z doprecyzowaniem: lud placi tylko za budowle sluzace miastu - mury, spichlerz, targ, port, warsztaty i podobne; budowle
  wojskowe placi pan jak dzis; gdy pierwsza w kolejce jest wojskowa, lud doklada do najblizszej "swojej"; BEZ "trzeciej czesci w gotowce" dla pana). To paczka P2 projektu obiegu
  kiesy (tor M) - kodowac PO scaleniu P1 (oba ruszaja TownFolk.Live.cs). **Hamulec slubow gotowy:** `w-toku/z3-swaty-hamulec` cd675b6 (klucz MarriageChanceScale 0.7; test poza gra
  na prawdziwej DLL: 1.0 = WGRANIE 37 co do kroku generatora, 0.7 = 69.94% prob przy szansie 1); **W TOKU W GRZE: test 120 dob** (Armoury W37 + CrashScribe-at-hamulec ef7ee537,
  drzewo SC\zb\sh; test\at-hamulec-120.out.txt) - oczekiwane 52-58 slubow do d120 (bylo 78; < 40 albo > 70 = zle); po czystym wyniku WGRANIE 38 (sam CrashScribe + settings.xml;
  DLL do wgrania zbudowac z cd675b6 BEZ at1-cs.diff).
- (17:25 UTC 13.10) **LUP ZDOBYTEGO MIASTA - ROZPOZNANIE I PROPOZYCJA GOTOWE (bez kodu; wf_063e9114-c56):** `docs/lup-miast/LUP-ZDOBYTEGO-MIASTA-2026-10-13.md` (rozdz. 0 prostym
  jezykiem, 1-2 jak jest dzis z dowodami, 3 regula, 4 sto procent przypadkow, 6 paczki L0-L7 i progi, 8 pytania); robocze SCRATCH\lup-miast\ (3 rozpoznania, weryfikacja - 8 poprawek
  wniesionych). JAK JEST: gra zdejmuje dobrobyt i daje 15 zl za punkt; u nas placi to kasa kupcow zdobytej osady z nadwyzki (okno O10) - zloto prawdziwe, ale kwota nie zalezy od
  bogactwa; w roku 14 zdobyc (3 miasta, 11 zamkow), lup razem 64.7 tys. zl; wiecej idzie bokiem - sprzedaz jencow kasie zdobytej osady 399 tys.; kies ludu, zapasu kupcow, towaru,
  koni, zywnosci nikt nie rusza; zdobyte miasto w pieniadzu ZYSKUJE (zold wojska: Harrenhal kasa 210 -> 450 tys.). ZNALEZIONE BLEDY: "grabiez" gry takze po wygranej bitwie pod
  murami bez zdobycia (5 x w roku; Vaes Efe), kiesa rozbitej zalogi znika (16 249 zl - dziura obiegu), zabici przez BK poza nasza ksiega ludzi, gracz dostaje 15% mniej lupu niz AI,
  zdarzenie "Right of Pillage" pustoszy i nie daje nic. PROPOZYCJA: zdobywcy biora czesc KAZDEJ monety w osadzie (laska 0, grabiez 1/3, spustoszenie 2/3) z kasy kupcow i kies ludu,
  podzial wedlug ludzi i "prawo trzecich", 3 doby bez zoldu wydawanego w lupionym miescie, laska bez naszej blizny, AI lupi z potrzeby; towar i konie z polek - drugi etap.
  Lup NIE jest zrodlem na wojsko (ok. 1 mln zl/rok = 0.6% zoldu). **CZEKA NA JEFFA: 5 pytan o kierunek (rozdz. 8) - podane w czacie; kodu nie zaczynac przed "tak".**

**WGRANIE 38 (13.10 17:22 UTC; decyzja Jeffa "przyhamuj sluby"):** CrashScribe 04f44a0e + CrashScribe.settings.xml 3e31434f (reszta jak WGRANIE 37: Armoury f2afb329, RC f9a944c1,
GT 035878c4, ForgeView e17e0233). Galaz `noc/wgranie-38` = `w-toku/zbior-e8` **cd675b6** (= 6d91156 + `w-toku/z3-swaty-hamulec`, przewiniecie bez konfliktu). Kopie: `*.bak-2026-10-13-noc38-przed`
i `D:\Backup-Bannerlord\wgrane\2026-10-13-noc38-przed`; md5 po wgraniu OK. Cofniecie: CrashScribe 3ca8aa08 + settings ef8cd722 z tych kopii.
- **Test 120 dob przed wgraniem (Armoury W37 + CrashScribe-at-hamulec ef7ee537, drzewo SC\zb\sh): 120/120, 0 ERROR**; linia startu "CZYNNE; mnoznik szansy 0.7 (MarriageChanceScale)";
  **slubow AI-AI do d30 / d60 / d91 / d120: 8 / 25 / 35 / 46 (bez hamulca w biegu rocznym: 22 / 37 / 50 / 78)** - w pasie zaliczenia 40-70, ponizej oczekiwanych 52-58 (jeden bieg,
  rozrzut losowy); liniowo ok. 140 na rok (przed regula 113, bez hamulca 215). Czas swatow 450-560 ms/d. Logi: session-2026-10-13_09-48-02.log, Armoury-2026-10-13_09-48-04.log (kopie na D:).
- Test zapisu: wczytany `autotest-dlugi` (teraz zapis z doby 60 biegu hamulca) + 9 dob: 0 ERROR, sluby licza dalej z mnoznikiem 0.7.
- Komentarze "ten sam ciag losowy" w Config.cs / MarriageFast.cs / linii startu poprawione w tym samym commicie (cd675b6).
- **DECYZJE JEFFA - LUP MIAST (13.10 ok. 17:20 UTC): 1 TAK (lup z kazdej monety, takze z zapasow), 2 TAK (podzial: zolnierze 2/3, wodz 1/3, korona 1/3 z czesci wodza), 3 TAK (3 doby
  bez zoldu wydawanego w lupionym miescie), 4 TAK (laska = laska, AI lupi z potrzeby), 5 "tak, i tak" = (a) towar i konie z polek TAK (drugi etap) oraz (b) skarbiec korony w stolicy,
  okup zamiast szturmu, karawany wroga w murach TAK (odczytane jako: robic, ale jako ostatni etap - potwierdzone Jeffowi w czacie).** KIERUNEK DODANY PRZEZ JEFFA (wiazacy):
  "lupienie miasta to straszna rzecz, miasto traci dobrobyt, kase, ludzi - trzeba to oddac; ale w grze wojny i lupienie sa czestsze niz normalnie, wiec miasto musi potrafic sie
  odbudowac - nie za szybko, rozsadnie; ciagle lupione miasto nigdy sie nie odbuduje; **budynki tez maja byc niszczone - przy spustoszeniu masa budynkow splonie i trzeba budowac od nowa**".
  DO PROJEKTU DOPISAC: (i) niszczenie budowli wedlug wyboru (laska 0 / grabiez czesc / spustoszenie duzo) - sprawdzic, co juz robi gra (poziomy budowli) i BK; (ii) tempo odbudowy
  policzone (dobrobyt dzis wraca 230-570 dob [S]) razem z paczka P2 "lud buduje miasto" (Jeff TAK) - odbudowa ma byc mozliwa, ale wolna; (iii) ludzie: zabici przez BK takze w naszej
  ksiedze. Kod lupu = tor L, po daninie P1 i budowie P2 (te same zbiorniki i te same pliki).
- (17:50 UTC 13.10) **Bieg kontrolny PO WGRANIU 38 (10 dob nowej kampanii): 10/10, Armoury 0 ERROR**, swaty "CZYNNE; mnoznik szansy 0.7"; w grze WGRANIE 38 (Armoury f2afb329,
  CrashScribe 04f44a0e, settings 3e31434f). **RAPORT ROCZNY GOTOWY** (wf_bd6c538c-c31): `docs/test-zbiorczy/RAPORT-ROCZNY-2026-10-13.md` + rozdzialy i weryfikacje w
  `docs/test-zbiorczy/raport-roczny-2026-10-13/` - podany Jeffowi (rozdz. 0). Najwazniejsze nowe ustalenia: wojny fabularne ROT wznawiane tego samego dnia (73 x; przymus 10 z 14
  konczy sie w d433); armia oblegajaca pobita wypadem ginie cala (38 x, 55 tys. ludzi/rok); pan bez klopotu z pieniadzem trzyma ok. 60% druzyny (przyczyna nieznana; sufit pieniadza
  96-105 tys.); zolnierz zdrozal 4.64 -> 5.78 zl/d; ok. 100 z 205 panow na granicy budzetu (druzyny 37%), u 47 zalogi zjadaja caly budzet; korona dzieli wedlug ziemi (rody bez
  budzetu dostaja 45% rent); plotna brak w 89 z 97 miast, skory w 42, drewno x13; najemnicy zmieniaja pana 764 x/rok, wykup tej samej osoby do 11 x, 440 jencow-lordow za darmo;
  56 tys. koni jucznych z niczego, 125 tys. szt. zboza i ryb bez zrodla, Thenn pol roku bez pieniedzy. Kolejnosc napraw w rozdz. 0 (g).
  **LIMIT TYGODNIOWY 84% (17:40 UTC; ok. +4 pkt/h przy workflowach) - PLAN NA RESZTE KONTA (podany Jeffowi): (1) dokonczyc P1 danine (recenzja w toku wf_e6948e4c-524 -> test 120 dob ->
  wgranie), (2) projekt podatkow wojennych (korona wedlug potrzeby + tarczowe + stawka wojenna pana) oszczednie, (3) komplet do przekazania. Budowa przez lud (P2), lup miast (tor L),
  konie robocze (k13a + bieg 120 dob z danina) - dla drugiego konta, chyba ze Jeff zmieni kolejnosc.**
- (18:35 UTC 13.10) **JEFF: "Robimy poprawki od 1 do 6" (lista (g) raportu rocznego), "pilnujemy limitu 5 h i tygodniowego, zapisujemy kazda skonczona zmiane, robimy az wyczerpie sie
  konto, potem drugie konto"; "dopiero jak wszystko poprawimy, robimy bieg dwuletni".** KOLEJKA I STAN (aktualizowac po kazdym kroku):
  1a. DANINA P1 - KOD GOTOWY I SCALONY: `w-toku/kiesa-p1-danina` e872bd9 -> `w-toku/zbior-e8` **55b59d7** (Armoury ccfbf36a = test\fin7; klucze FolkWarAid, FolkWarAidShare 0.025,
      PeopleMenuShowsSavings, FolkLadderMeasureLog; progi `--grupa kiesa1`). **W GRZE: test 120 dob** (test\at-p1-120.out.txt; start 17:31 UTC). Oczekiwane (szacunek recenzji z danych
      biegu, +-30%): lokata W d120 ok. 6-7.5 mln (z tego 3-4 mln w krolestwach BEZ poboru: Dothrakowie caly rok s = 0, Nocna Straz, pokoj) -> P1-1 = DECYZJA; danina d91-120 60-75 tys.
      zl/d (prog >= 40); P1-5a r = 0, P1-5b 0 roznych. DO JEFFA: skrzyn miast krolestw "bez potrzeby korony" danina nie ruszy (zasada z 09.10) - ok. polowy lokaty po roku.
      Po czystym tescie: zapis 9 dob -> WGRANIE 39.
  1b. KORONA PLACI WEDLUG POTRZEBY - w toku (workflow, galaz `w-toku/w5-korona-wg-potrzeby` od 55b59d7, drzewo SC\zb\wn).
  2.  TARCZOWE - po 1b (ten sam garnek korony). Stawka wojenna podatku pana (trzeci wybor Jeffa) - po tarczowym.
  3.  POBITY OBLEGAJACY UCHODZI - w toku wf_5efe7e4c-7b2 (docs/przekazanie/wf-n3-oblegajacy-uchodzi.js), galaz `w-toku/n3-oblegajacy-uchodzi` od cd675b6, drzewo SC\zb\n3
      (LosersFlee.cs ma warunek IsFieldBattle; 38 wypadow = 55 tys. ludzi/rok w 100% straconych).
  4.  BIEG DWULETNI - NA SAMYM KONCU, po wszystkich poprawkach (decyzja Jeffa).
  5.  TOWARY (len, skora, platnerze, hamulec drewna, budowy z dala od frontu) - nie zaczete.
  6.  MALE POPRAWKI (najkrotsza sluzba najemnikow, przerwa po wykupie, konie juczne i zboze w ksiegach, Thenn) - nie zaczete.
  Poza lista 1-6, zatwierdzone przez Jeffa i czekajace: konie robocze k13a + bieg 120 dob z danina; P1b kara lojalnosci (po pomiarze); P2 lud buduje miasto (tylko budowle sluzace
  miastu); P3-P5 zbytki; lup miast (5 x tak + niszczenie budowli, rozsadna odbudowa).

**WGRANIE 39 (13.10 19:02 UTC; poprawka 1a - danina wojenna z oszczednosci ludu, paczka P1 obiegu kiesy):** Armoury **ccfbf36a** (reszta jak WGRANIE 38: CrashScribe 04f44a0e, settings
3e31434f, RC f9a944c1, GT 035878c4, ForgeView e17e0233). Galaz `noc/wgranie-39` = `w-toku/zbior-e8` **55b59d7** (= cd675b6 + `w-toku/kiesa-p1-danina` e872bd9). Kopie:
`*.bak-2026-10-13-noc39-przed` i `D:\Backup-Bannerlord\wgrane\2026-10-13-noc39-przed`; md5 OK. Cofniecie: Armoury f2afb329 z tych kopii (albo klucz FolkWarAid = false w MCM).
- **TEST 120 dob przed wgraniem (nowa kampania; Armoury ccfbf36a + CS-at-hamulec): 120/120, 0 ERROR, 11.5 s/dobe** (Armoury-2026-10-13_10-30-28.log + tabele, kopie na D:; progi
  SCRATCH\test\progi-p1\). **Danina d91-120: 121.7 tys. zl/d** (W 87.3 / R 6.8 / P 7.5 / podzamcza 20.1; szacunek byl 60-75 tys.), razem 6.06 mln; pierwsza doba poboru d29 (s rosnie
  tygodniami). **Zamkniecie: kontrola kies r = 0 w 119 z 119 dob; danina z kies = do skarbcow = wplyw 165 = Obieg, roznych dob 0; reszta ksiegi R 126; potkniecia 0.**
  **Wojsko lordow w wojnie (sr. 28 dob w d120): 105 589 wobec 101 144 w tescie rundy 3 bez daniny (+4.4 tys.; ostatnia doba 104 622 wobec 88 797)**; zalegly zold 0.01%; zwrot
  zoldu 100%, renty 282.5 tys. zl/d. Lokata W d120 7.94 mln (z poborem 5.91, bez poboru 2.04: Dothrakowie, Sarnor, Dolina, Qarth s = 0; pokoj Dorzecze, Yi Ti) wobec 8.9-10.6 bez
  daniny - prog 4.5 mln NIE: zloto wraca do ludu jako zarobek (zold wydany w miastach), wiec lokata rosnie dalej +18.9 tys./d (bylo +47-65). **P1-9 NIE: zloto rodow z luzem pulapu
  +7.01 mln w 118 dob (31.35 -> 38.36 mln); rodow na pulapie 81 -> 65** - danina rozdawana wedlug ziemi osiada u rodow, ktorym nie brakuje (przewidziane ryzyko; lekarstwo = poprawka
  1b "korona wedlug potrzeby" i tarczowe). Lojalnosc: miasta srednio 93, zadne <= 30 (pomiar pod P1b). Kupione z oferty nadwyzki 4.6% (oferta 1.35 mln zl/d, z tego aksamit 1.09 mln
  bez towaru na polce) - dane pod P3 / P4. Czas poboru 0.061 ms/d.
  Nowe NIE wobec testu rundy 3 (wszystkie na granicy, do obejrzenia w biegu dluzszym): R1-8 miasta bez zelaza d91-120 31.6 (prog 30; bylo 25.9 - wieksze wojsko zuzywa wiecej),
  R1-10b cykle warsztatow bez surowca 190/d (prog 177), E5/B5-7 port Lordsport 36 dob bez nadwyzki (prog 28).
- Test zapisu: wczytany `autotest-dlugi` (doba 60 biegu daniny) + 9 dob: 0 ERROR, linia daniny liczy dalej. Bieg kontrolny po wgraniu w toku (test\at-w39-kontrola10.out.txt).
- NIE SPRAWDZONE AUTOTESTEM (do recznego testu Jeffa): zdanie "Savings: ..." w oknie "People of this land" i komunikat "War aid: ..." dla osad gracza.
- (19:10 UTC 13.10) Bieg kontrolny PO WGRANIU 39 (10 dob nowej kampanii): 10/10, Armoury 0 ERROR; w grze Armoury ccfbf36a, CrashScribe 04f44a0e (md5 sprawdzone).
- (19:35 UTC 13.10) **POPRAWKA 3 "POBITY OBLEGAJACY UCHODZI" - KOD GOTOWY I SCALONY (NIEWGRANE):** `w-toku/n3-oblegajacy-uchodzi` 82f7c06 -> `w-toku/zbior-e8` **8639c21**
  (scalenie bez konfliktu; gen_mcm 1320; Armoury b2423a38 = test\fin8). Klucz `LosersFleeBesiegers` (true): bitwa IsSallyOut albo IsSiegeOutside na ladzie, pobita strona nie jest
  strona twierdzy -> ta sama droga co pole (Plan / Apply / RoutedH3). Objete tez: szturm, do ktorego doszla odsiecz (gra liczy go jako bitwe w otwartym terenie). NIE objete: zwykly
  szturm, pobita strona twierdzy, morze. Linie: "Przegrani pod murami (N3): dzien ", w bitwy.log "Bitwa: H3 pod murami (N3) dzien "; progi N3-1..N3-4 w `--grupa etap8`.
  Oczekiwane w 120 dobach: 12-13 wypadow z pobitym oblegajacym, zabici + jency ok. 34% (dzis 100%), rozbici ok. 66%. RYZYKA DO OBEJRZENIA: do lasu ok. 23 tys. rozbitych rocznie
  wiecej (bandy; pula wyrzutkow juz 11.9 tys.), rozbici NIE wracaja do druzyny (las 30 / 120 dob, ludnosc, karczmy) - wiec wiecej zdobytych twierdz paczka nie obiecuje; zaloga po
  wygranym wypadzie mocniejsza (limit 5% zabitych zwyciezcy); jencow szeregowych ok. 39 tys. rocznie mniej (sprzedaz jencow ok. -0.8 mln zl/rok).
  **W GRZE: test 120 dob** (test\at-n3-120.out.txt; start 19:33 UTC). Po czystym tescie: zapis 9 dob -> WGRANIE 40.
- (19:50 UTC 13.10) **POPRAWKA 5 (TOWARY) - W TOKU:** workflow wf_8e1aa5c4-418 (docs/przekazanie/wf-n5-towary.js), galaz `w-toku/n5-towary` od 8639c21, drzewo SC\zb\tw: piec spraw -
  plotno / len, skora wyprawiona, zbroja t3-4, zalew drewna, budowy cywilne z dala od frontu; autor ma znalezc PRZYCZYNE kazdej i nie kodowac na sile ("do decyzji" dla Jeffa).
  Rownolegle w toku: wf_c5c3b7ac-88c (korona wedlug potrzeby, zb\wn od 55b59d7) i test 120 dob poprawki 3 w grze. Limity 19:45 UTC: 5 h 58%, tydzien 86%.
- (18:40 UTC 13.10; UWAGA: godziny wpisow od "WGRANIE 39" do "19:50 UTC" powyzej sa zawyzone o ok. 1 h - WGRANIE 39 bylo ok. 18:00 UTC, kolejnosc wpisow dobra)
  **POPRAWKA 1b "KORONA PLACI WEDLUG POTRZEBY" (W5) - KOD GOTOWY I SCALONY (NIEWGRANE, BEZ TESTU W GRZE):** `w-toku/w5-korona-wg-potrzeby` ad9023d -> `w-toku/zbior-e8` **00a6496**
  (CHANGELOG - suma, gen_mcm 1323; Armoury a5e2fbf9 = test\fin9: P1 + N3 + W5). Regula: garnek krolestwa = danina P1 tej doby (najwyzej reszta wplywow dnia po zwrocie zoldu);
  dostaja rody AI w wojnie ze spelnionym warunkiem rent 180 i budzetem 166, ktorych druzyny sa ponizej 60% limitu z powodu pulapu; "od najslabszych" (poziom wyrownania); gotowka
  tylko za ludzi juz bedacych w druzynie ponad pulap wlasny, na brakujacych samo miejsce w pulapie (krok werbunku 10% limitu/d); przydzial podnosi pulap druzyn, nie wchodzi do D
  i nie jest podstawa zwrotu 70%; reszta garnka w renty. Klucze: CrownWarAidByNeed (true), CrownWarAidFillTarget 0.60, CrownWarAidRecruitStep 0.10. Linia "Doplata wedlug potrzeby
  (W5): dzien "; progi `--grupa w5`. Rachunek (szacunek): rok +3 do +6.5 tys. ludzi (79.4 -> 82.5-86 tys.; 85 tys. tylko przy gornym koncu); 120 dob: wyplacone 8-25 tys. zl/d
  u 20-34 rodow w d93-120. **DO JEFFA: Zelazne Wyspy, Wolni Ludzie, Ibben, Qohor, Pentos nie maja garnka (39% potrzeby) - pomoze dopiero tarczowe albo "wedlug potrzeby" z calej
  reszty po zwrocie (zabiera renty wszystkim).** Ryzyka: cel 60% = prog wezwania do armii w grze; falowanie werbunek / zwolnienia; awanse zamiast ludzi (W5-9).
  **POPRAWKA 2 TARCZOWE (W6) - W TOKU:** wf_99079863-f58 (docs/przekazanie/wf-w6-tarczowe.js), galaz `w-toku/w6-tarczowe` od 00a6496, drzewo SC\zb\tc: placi rod AI z wolnym pulapem
  wlasnym i zlotem ponad rezerwe (stawka 25% wolnego pulapu, najwyzej 1/45 nadwyzki dziennie), korona bierze tylko tyle, ile wynosi niepokryta potrzeba W5, i wydaje tej samej doby
  przez garnek W5. Kolejka gry: test 120 dob poprawki 3 (konczy sie) -> zapis -> WGRANIE 40 -> test 120 dob W5 (fin9) -> WGRANIE 41 -> tarczowe -> towary (wf_8e1aa5c4-418 w toku).

**WGRANIE 40 (13.10 ok. 18:45 UTC; poprawka 3 - pobity oblegajacy uchodzi):** Armoury **b2423a38** (reszta jak WGRANIE 39: CrashScribe 04f44a0e, settings 3e31434f, RC f9a944c1,
GT 035878c4, ForgeView e17e0233). Galaz `noc/wgranie-40` = **8639c21** (55b59d7 + `w-toku/n3-oblegajacy-uchodzi` 82f7c06; UWAGA: `w-toku/zbior-e8` jest juz dalej - 00a6496 z W5,
NIEWGRANE). Kopie: `*.bak-2026-10-13-noc40-przed` i `D:\Backup-Bannerlord\wgrane\2026-10-13-noc40-przed`; md5 OK. Cofniecie: Armoury ccfbf36a z kopii albo klucz
LosersFleeBesiegers = false.
- **TEST 120 dob przed wgraniem (nowa kampania; fin8 + CS-at-hamulec): 120/120, 0 ERROR** (Armoury-2026-10-13_11-11-41.log + tabele, kopie na D:; progi SCRATCH\test\progi-n3\).
  **N3-1: 13 bitew (8 wypadow, 5 pod murami), pobici oblegajacy 19 596 ludzi: zabici 20.1%, jency 7.8% (razem 27.8%), rozbici 72.2% - gra dalaby 7 099 zabitych i ok. 12 497
  jencow (100%).** Ksiega N3-2: kontrola 0, roznica 0; potkniecia 0. Zwyciezcy bitew pod murami: zabici 4.1%.
  SKUTKI UBOCZNE (zmierzone, przewidziane w recenzji): **pula wyrzutkow d60 / d90 / d120: 2 544 / 5 457 / 6 170 wobec 1 036 / 2 754 / 2 674 w biegu daniny bez N3** (prog 3 053 NIE;
  powrot do domu 55.5/d wobec 25.4/d - wzrost zwalnia); bandy 661 / 13 084 ludzi wobec 663 / 11 932 (w progu). Wojsko lordow w wojnie d120: 99 917 (ostatnia doba 102 783) wobec
  105 589 w biegu daniny i 101 144 w biegu rundy 3 - trzy rozne kampanie, roznica w szumie; N3 NIE podnosi wojska wprost, bo rozbici nie wracaja do druzyny (las, ludnosc, karczmy).
  Nowe NIE wobec biegu daniny, wszystkie na granicy: P3W1-1 rody z lennem bez warunku renty 10.3% (prog 10; bylo 6.2), P3W1-3 26 986 zl/d (prog 20 000; bylo 15 072), G1-1
  karawany bez statkow 7+ dob 14 (Pyke 12; prog 10), C-3a polki lukow x1.11. Danina P1 w tym biegu: 111.9 tys. zl/d, zamkniecie 0 roznych.
  **DO JEFFA (kierunek, nie blokuje): czy rozbici z pobitej armii maja po kilku dobach wracac do swojego pana, zamiast rozchodzic sie do lasu / karczm - dopiero to odbuduje armie
  po bitwie i zdejmie przyrost wyrzutkow.**
- Test zapisu: `autotest-dlugi` (doba 60 biegu N3) + 9 dob: 0 ERROR. Bieg kontrolny po wgraniu i test 120 dob W5 (fin9: Armoury a5e2fbf9, zbior-e8 00a6496) - lancuch w grze
  (test\at-w40-kontrola10.out.txt, test\at-w5-120.out.txt).
- (18:55 UTC 13.10) **JEFF - ROZBICI WRACAJA POD CHORAGIEW (kierunek TAK, takze bitwy w polu):** "zolnierze z pobitej armii wracaja do armii, ale nie wszyscy; wiekszosc boi sie
  dezercji, wiec wraca; czesc wybiera zycie banity, a czesc wraca do domow. Daj mi warunki, zasady i proporcje, takze bitwy w polu." PROPOZYCJA PODANA JEFFOWI (czeka na "tak" /
  zmiany; kodu nie zaczynac przed odpowiedzia): dotyczy szeregowych druzyn lordow (pole + pod murami N3; bandy, karawany, tabory wsi, zalogi - jak dzis). Zabici i jency bez zmian
  (H3: zabici 5-65% wedlug sytuacji, jency 30% weteranow / 5% reszty). Z tych, ktorzy uszli: **55% wraca pod choragiew, 30% do domu, 15% do lasu.** Poprawki: weterani (t4+) 70%
  wraca, rekruci (t1-2) 45%; zalegly zold: -10 pkt powrotu za kazdy tydzien zaleglosci (min. 20%), ci ida do lasu; pobici we wlasnym kraju: wiecej do domu, w obcym: mniej do domu,
  wiecej do lasu i pod choragiew; Nocna Straz 80% wraca, nikt do domu. Zbieraja sie 3-10 dob w najblizszej twierdzy rodu (gdy rod nie ma twierdzy - w najblizszym miescie
  krolestwa), czekaja na pana do 30 dob (pan w niewoli / bez druzyny: odbiera ich po powrocie; inna druzyna tego samego rodu tez moze); po 30 dobach polowa do domu, polowa do lasu.
  Pan przyjmuje tylko tylu, na ilu ma miejsce w druzynie i w budzecie zoldu. Gracz: jego rozbici czekaja tak samo, komunikat "Stragglers: N of your men have gathered at {TOWN}".
  Ksiega ludzi: nikt nie powstaje i nie znika. Oczekiwane (szacunek, do policzenia w projekcie): pobita armia wraca do ok. 35-45% dawnej sily zamiast zaczynac od zera; pula
  wyrzutkow z powrotem w okolice 3 tys. w d120.
- (18:55 UTC 13.10) **JEFF: "Proporcje pasuja. Zrob poprawke daniny."** Czyli dwie nowe paczki ZATWIERDZONE:
  **(N7) ROZBICI WRACAJA POD CHORAGIEW** - zasady z wpisu wyzej (55 / 30 / 15; weterani 70, rekruci 45; -10 pkt za tydzien zaleglego zoldu, min. 20; wlasny / obcy kraj; Nocna Straz
  80 / 0; zbiorka 3-10 dob w twierdzy rodu, czekanie 30 dob, pan przyjmuje w granicach miejsca i budzetu; gracz z komunikatem; pole + pod murami). Tor M/L: LosersFlee.cs /
  OutlawLaw.cs (RoutedH3Body, SendHome), ksiega ludzi; potrzebna pula czekajacych na rod i osade, ktora PRZEZYWA ZAPIS (nowe pole zapisu przez SaveText.Sync - limit 32 KB na napis,
  patrz CLAUDE.md / pamiec). NIE ZACZETE - ruszyc po zejsciu z kolejki tarczowego i towarow (limit 5 h).
  **(W5b) POPRAWKA DANINY - "potrzeba korony" ma widziec brak wojska u panow:** dzis stawka s (KingdomTreasury.WarTax.cs, krok co 7 dob) rosnie tylko przy kredycie 186, zwrocie >
  wplywy albo skarbcu < zapas wojny, a spada przy skarbcu > 2 x zapas - Zelazne Wyspy: skarbiec 1 046 588 / zapas 500 000, s = 0%, druzyny 1.6 tys. z 8.1 tys. (bledne kolo: pusty
  budzet panow = maly zwrot zoldu = "korona bez potrzeby"). Do zrobienia: (1) niepokryta potrzeba W5 krolestwa (rody ponizej 60% z powodu pulapu) liczy sie jako potrzeba korony -
  s nie spada i rosnie o krok, dopoki jest; (2) skarbiec ponad zapas wojny zasila garnek W5 (np. 1/30 nadwyzki dziennie), przed tarczowym. Zalezy od W6 (ten sam plik NeedAid) -
  ruszyc PO zakonczeniu wf_99079863-f58. Uwaga z raportu: lud Wysp odklada ok. 3.9 tys. zl/d przy potrzebie 6 tys. - sama danina nie wystarczy, reszta z tarczowego.
  STAN GRY: bieg kontrolny po WGRANIU 40 czysty (10/10); trwa test 120 dob W5 (fin9). Limity 18:51 UTC: 5 h 66% (zeruje sie 20:50), tydzien 89% - od teraz nowe workflowy po
  jednym naraz.

**WGRANIE 41 (13.10 ok. 19:20 UTC; poprawka 1b - korona placi wedlug potrzeby, W5):** Armoury **a5e2fbf9** (reszta jak WGRANIE 40). Galaz `noc/wgranie-41` = `w-toku/zbior-e8`
**00a6496** (8639c21 + `w-toku/w5-korona-wg-potrzeby` ad9023d). Kopie: `*.bak-2026-10-13-noc41-przed` i `D:\Backup-Bannerlord\wgrane\2026-10-13-noc41-przed`; md5 OK. Cofniecie:
Armoury b2423a38 z kopii albo klucz CrownWarAidByNeed = false.
- **TEST 120 dob przed wgraniem (nowa kampania; fin9 + CS-at-hamulec): 120/120, 0 ERROR** (Armoury-2026-10-13_11-46-24.log + tabele, kopie na D:; progi SCRATCH\test\progi-w5\).
  Wszystkie progi W5 TAK: zamkniecie co do zlotowki (wyplacono + reszta = garnek; linie 165 i Obieg zgodne w 119 z 119 dob), garnek <= danina, gotowka tylko rodom ponad pulap
  wlasny (W5-3b: 1.7% rodo-dob ponizej 90% pulapu), zwrot 70% nie placi dwa razy (98.0%), potkniecia 0. **Liczby (sr. 28 dob): garnek 95.7 tys. zl/d, rodow z potrzeba 37 z 187,
  potrzeba 48.9 tys., wyplacono 22.2 tys. zl/d (25 rodow), reszta do rent 73.5 tys.; wypelnienie druzyn rodow z doplata 49.8% -> 54.0%.** Wojsko lordow w wojnie d93-120:
  **106 719** (ostatnia doba 107 008) - wobec 99 917 (bieg N3), 105 589 (bieg daniny), 101 144 (runda 3); rozne kampanie, ale najwyzszy wynik z czterech. Danina 98.5 tys. zl/d.
  Zloto rodow z luzem: +4.08 mln w 118 dob (w biegu daniny bez W5: +7.01 mln) - lepiej, dalej ponad progiem P1-9 (tarczowe). Bez garnka w d120: The Reach 3.1 tys., Pentos 1.9,
  Qohor 1.7 tys. zl/d potrzeby (poprawka W5b). NIE: etap2 0, etap4 1 (pula wyrzutkow - N3), etap8 8, kiesa1 1 (P1-9).
- Test zapisu: `autotest-dlugi` (doba 60 biegu W5) + 9 dob: 0 ERROR. Bieg kontrolny po wgraniu w toku (test\at-w41-kontrola10.out.txt).
- (19:25 UTC 13.10) **POPRAWKA 2 TARCZOWE (W6) - KOD GOTOWY I SCALONY (NIEWGRANE):** `w-toku/w6-tarczowe` 8a33163 -> `w-toku/zbior-e8` **0a387b5** (gen_mcm 1325; Armoury 772918be =
  test\fin10: P1 + N3 + W5 + W6). Regula: placi rod AI krolestwa w wojnie z lennem, budzetem 166, czynna druzyna i kiesa ponad rezerwe wojenna; podstawa = pulap wlasny druzyn ponad
  zold druzyn przy 60% limitu; nalezne 25% podstawy, najwyzej 1/45 nadwyzki ponad rezerwe dziennie; KORONA BIERZE TYLE, ILE WYNOSI NIEPOKRYTA POTRZEBA W5 po daninie i wyplaca te
  gotowke tej samej doby przez W5 (platnik nigdy nie jest odbiorca; przy W5 wylaczonym i w pokoju 0). Klucze Scutage (true), ScutageRate 0.25. Linia "Tarczowe (W6): dzien ";
  progi `--grupa w6`; CSV: tarczowe_w6, podstawa_tarczowego_w6, doplata_z_tarczowego_w6, nadwyzka_ponad_rezerwe_w6. Szacunek recenzji: 120 dob - pobrane 1-8 tys. zl/d; rok -
  7-19 tys. zl/d (3-7 mln), +1 do +2.5 tys. ludzi ponad W5 (razem 84-88 tys.). **DO JEFFA: (1) tarczowe to ZAPAS, nie staly podatek - polowa po ok. 150 dobach, w wojnie > 2 lata
  prawie wygasa; (2) rod gracza ani nie placi, ani nie dostaje - czy ma?; (3) krolestwa bez platnikow (Zelazne Wyspy, Wolni Ludzie, Ibben, Qohor) dalej bez pomocy.**
  **POPRAWKA DANINY (W5b) - W TOKU:** wf_370f605f-283 (docs/przekazanie/wf-w5b-potrzeba-korony.js), galaz `w-toku/w5b-potrzeba-korony` od 0a387b5, drzewo SC\zb\w5b.
  W GRZE (lancuch): bieg kontrolny po WGRANIU 41 -> test 120 dob W6 (test\at-w6-120.out.txt). Limity 19:20 UTC: 5 h 74% (zeruje sie 20:50), tydzien 91%.
- (19:40 UTC 13.10) **POPRAWKA 5 TOWARY (N5) - KOD GOTOWY I SCALONY (NIEWGRANE, BEZ TESTU W GRZE):** `w-toku/n5-towary` 71249d4 -> `w-toku/zbior-e8` (HEAD po tym scaleniu - patrz
  "git log"; gen_mcm 1334; Armoury w test\fin11: P1 + N3 + W5 + W6 + N5). Piec regul, kazda z wlasnym kluczem:
  A PLOTNO - przyczyna: len (20 wsi) stoi na suficie regulatora x1.30 od d85, a plotno wykupuje lud (120 ze 164 szt./d) - brak PRODUKCJI; regula: surowiec z brakiem w > 15
    miastach ma sufit 4.0 (ValveRaiseWhileShort, ValveShortCeiling); skutek dopiero od d169, w roku 1 polki moga zostac puste.
  B SKORA - przyczyna INNA niz zakladana: linie uboju gry (owca -> mieso, swinia -> 2 miesa) nie daja skory, 134 zwierzat/d; regula: 1 skora na zwierze (SlaughterKeepsHides).
  C ZBROJA t3-4 - przyczyna: liczba rak platnerzy (0.10 / 1000 ludzi), nie przydzial; regula: rece x1.3 (WorkshopHandsStep).
  D DREWNO - przyczyna: regulator podniosl wyreb do x1.30 w d113 i nie mial jak cofnac; regula: krok wraca do zmierzonego zuzycia (ValveStepFollowsUse).
  E BUDOWY CYWILNE - przyczyna: wstrzymane sama flaga wojny; regula: warownia "przy froncie" tylko, gdy w 14 dobach w zasiegu 60 bylo oblezenie / wroga druzyna / bitwa / rabunek
    (CivilWorksAwayFromFront, CivilWorksCalmDays 14, CivilWorksFrontRange 60, CivilWorksNeedWageRoom - pan z zoldem przy 95% pulapu nie oplaca budowy).
  Progi N5-0..N5-5 w `--grupa etap8`. Oczekiwane w 120 dobach: B skory 394 -> 480-530/d; C t3+t4 29.8 -> 33-39/d; E budowa w 95-115 osadach (bylo 55), wydatki 35-55 tys. zl/d;
  A i D nie do oceny w 120 dobach (potrzeba >= 200). **DO JEFFA (7 spraw, zadna nie blokuje): len - wiecej wsi lnianych / wyzszy sufit / szybsze kroki / len kosztem zboza wsi;
  skora - zwierzeta rzezne pod regulator; rece 1.3 czy 1.5; warunek luzu zoldu zdejmuje 49-78 osad z budow; pasmo drewna 30-60 dni; sol stoi na suficie jak len (regula A jej nie
  obejmuje); regula A daje drewnu i zelazu ten sam sufit 4.0 (projekt budow zakladal 1.8).** Ryzyka: budowy kupia 2-3 x wiecej drewna; nowy strumien zlota od panow do miast
  (+16-21 tys. zl/d do kies ludu); pamiec frontu tylko w sesji. KOLEJKA GRY: test 120 dob W6 (w toku) -> WGRANIE 42 -> test 120 dob N5 (fin11) -> WGRANIE 43.
- (19:45 UTC 13.10) Bieg kontrolny PO WGRANIU 41 (10 dob nowej kampanii): 10/10, pliki w grze zatwierdzone (Armoury a5e2fbf9). Test 120 dob W6 w toku.

**WGRANIE 42 (13.10 ok. 19:57 UTC; poprawka 2 - tarczowe, W6):** Armoury **772918be** (reszta jak WGRANIE 41). Galaz `noc/wgranie-42` = **0a387b5** (00a6496 + `w-toku/w6-tarczowe`
8a33163; UWAGA: `w-toku/zbior-e8` jest dalej - d527d18 z N5, NIEWGRANE). Kopie: `*.bak-2026-10-13-noc42-przed` i `D:\Backup-Bannerlord\wgrane\2026-10-13-noc42-przed`; md5 OK.
Cofniecie: Armoury a5e2fbf9 z kopii albo klucz Scutage = false.
- **TEST 120 dob przed wgraniem (fin10 + CS-at-hamulec): 120/120, 0 ERROR** (Armoury-2026-10-13_12-21-21.log + tabele, kopie na D:; progi SCRATCH\test\progi-w6\). Wszystkie progi
  W6 TAK: pobrane = dopisane do garnka = wyplacone przez W5 ponad danine (rozjazd 0 w 119 dobach; CSV zgodny), platnik nigdy odbiorca, nikt ponizej rezerwy, potkniecia 0.
  **Liczby (sr. 28 dob): platnikow 115 z 199, podstawa 288 tys. zl/d, nalezne 72.0 tys., niepokryta potrzeba po daninie 27.8 tys., do pobrania 5.5 tys., pobrane = wyplacone
  2.7 tys. zl/d (razem w biegu 247 tys. zl)** - maly skutek w 120 dobach, zgodnie z szacunkiem (korona placi tylko za ludzi, ktorzy sa). Wojsko lordow w wojnie d93-120: 101 713
  (bieg W5: 106 719 - inna kampania). **W5-4 w tym biegu NIE: wypelnienie rodow z doplata 47.5% -> 46.7%** (w biegu W5 49.8 -> 54.0) - do obejrzenia w biegu dluzszym: czy rody
  z doplata rzeczywiscie werbuja. Zloto rodow z luzem +4.97 mln w 118 dob (P1-9 dalej NIE). Bez pokrycia w d120: Iron Islands 12.5 tys., Pentos 3.6, House Targaryen 2.2, Lys 0.9.
- Test zapisu: `autotest-dlugi` + 9 dob: 0 ERROR. W GRZE (lancuch): bieg kontrolny po wgraniu -> test 120 dob N5 towary (fin11; test\at-n5-120.out.txt).
- **DECYZJE JEFFA 13.10 ok. 19:55 UTC:** TARCZOWE: (1) "Jako zapas" - zostaje, bez wersji od dochodu; (2) rod gracza: "tez dotyczy, ale pod warunkiem, ze mam ziemie i sluze
  jakiemus krolowi" - gracz-wasal z lennem PLACI tarczowe i DOSTAJE doplate W5 na tych samych zasadach (z komunikatem); (3) "tak, stawka wojenna podatku pana" - trzeci kierunek
  do zaprojektowania i zakodowania. TOWARY: (1) len - "tak ma byc len, ale nie rozumiem, co proponujesz, bo nie widze liczb" - wyjasnione liczbami w czacie, czeka na wybor
  (rada: szybsze kroki co 28 dob + len kosztem innego plonu tej wsi); (2) "jesli nie ma kasy, to nie buduje" - warunek luzu zoldu przy budowach ZOSTAJE; (3) sol pod regule A - TAK.
- **KOLEJKA ZADAN (po kolei, po jednym workflow; limit 5 h 80% do 20:50 UTC, tydzien 92%):**
  a. W5b poprawka daniny - w toku (wf_370f605f-283).
  b. N7 rozbici wracaja pod choragiew (zasady we wpisie 18:55) - po odnowieniu limitu 5 h.
  c. W7 stawka wojenna podatku pana (pan podnosi podatek w swoich miastach i wsiach na czas wojny, koszt: lojalnosc i dobrobyt; u gracza jego decyzja; pieniadz idzie na zold -
     do zaprojektowania: czy do kiesy pana, czy przez garnek W5).
  d. W6b gracz-wasal z lennem w tarczowym i doplacie (komunikaty po angielsku) + N5b sol pod regule A (i decyzja o lnie po odpowiedzi Jeffa).
  e. poprawka 6 (male): najkrotsza sluzba najemnikow, przerwa po wykupie, konie juczne i zboze w ksiegach, Thenn. f. k13a konie robocze + bieg 120 dob. g. P1b, P2, P3-P5, lup miast.
  h. BIEG DWULETNI - na samym koncu.
- (20:15 UTC 13.10) **JEFF: len - "A i C" (szybkie kroki + len kosztem innego plonu), z zastrzezeniem: "produkuja ludzie; tam, gdzie jest ich wiecej, wiecej sie produkuje - dlatego
  poludnie jest bogate, a polnoc mniej; od tego powinna byc zalezna gospodarka, inaczej to nie ma sensu. Ile ludnosci ma polnoc i ile zarabia - porownaj z poludniem i przelicz."**
  POLICZONE (bieg roczny 06:24, doba 364 / srednia 28 dob; skrypt `docs/ludnosc/kr-ludnosc-a-zarobek.py`; tabele ludzie-regiony.csv, budzet-rodow.csv, kiesa-ludu.csv):
  ludzie (tys.) / miasta / wsie / wplyw rodow zl/d (na 1000 ludzi) / zarobek mieszczan zl/d / wojsko w druzynach i zalogach (na 1000 ludzi):
  The Reach 8 345 / 7 / 49 / 82.7 tys. (9.9) / 37.7 tys. / 11.6 tys. (1.4); King's Landing 4 781 / 6 / 49 / 100.2 (21.0) / 46.6 / 13.0 (2.7); **The North 3 227 / 10 / 60 / 108.9
  (33.8) / 60.8 / 17.6 (5.4)**; The Vale 3 222 / 6 / 34 / 68.3 (21.2) / 47.2 / 11.3 (3.5); Stormlands 2 757 / 4 / 39 / 58.7 (21.3) / 22.9 / 10.9 (4.0); Riverlands 2 741 / 6 / 32 /
  70.7 (25.8) / 35.9 / 10.2 (3.7); Dorne 1 116 / 4 / 29 / 103.1 (92.4) / 34.1 / 10.4 (9.4); Iron Islands 469 / 6 / 28 / 31.3 (66.8) / 22.0 / 6.4 (13.8); swiat 51 471 tys. ludzi,
  1 213 tys. zl/d, srednio 23.6 zl na 1000 ludzi. **WNIOSEK: zarobek idzie za LICZBA OSAD, nie za liczba ludzi.** Przyczyna (zmierzona): w ksiedze ludzi wies Reach ma srednio
  153.6 tys. ludzi, Polnocy 49.3 tys., Dorne 35.3 tys., Wysp 16.0 tys. - ale "hearth" gry na wies jest wszedzie prawie taki sam (Reach 406, Polnoc 364, Dorne 401, Wyspy 334),
  a dobrobyt na miasto tez (6 300-7 200); produkcja wsi w grze liczy sie od progu hearth (0.5 / 1.0 / 1.5), podatki od dobrobytu - wiec kazda osada daje podobnie bez wzgledu na
  to, ilu ludzi ksiega jej przypisuje. Gdyby zarobek rodow szedl za ludzmi (23.6 zl / 1000): Reach 197 tys. (dzis 83), Polnoc 76 (109), Dorne 26 (103), King's Landing 113 (100),
  Dolina 76 (68), Stormlands 65 (59), Dorzecze 65 (71), Wyspy 11 (31). PROPOZYCJA KIERUNKU PODANA JEFFOWI (czeka na "tak"; tor L, osobny projekt): plon wsi, rece rzemiosla,
  spozycie, podatki i pobor proporcjonalne do ludzi regionu z ksiegi ludzi (jedna skala na region), tak zeby ludne poludnie produkowalo i placilo wiecej, a polnoc mniej.
  Poprawka lnu (N5b) czeka na te decyzje - "wiecej lnu" ma wynikac z rak, nie z mnoznika.
- (20:30 UTC 13.10) **JEFF: "tak, rob taki projekt - myslalem, ze tak to projektujemy"** (gospodarka od ludzi). Doprecyzowanie Jeffa (wiazace dla projektu): "produkcja lnu jest
  zalezna od ludzi, tak samo kazdego innego towaru - nie tak, ze wioska produkuje tyle samo; **wioska to tylko SYMBOL wielu wiosek, ktore lacznie produkuja tyle, ilu jest ludzi**;
  sprawdz, ile to powinno byc, dla kazdego surowca, jak to wygladalo"; przyklad: "wioska na Polnocy z symbolem zboza produkuje np. 10, bo w jej obszarze zyje 100 tys. ludzi, a ta
  sama wioska z symbolem zboza w Reach produkuje 37, bo w jej obszarze zyje 370 tys."; "mozna jeszcze uwzglednic produkcje od dobrobytu, technologii, klimatu itp., zeby mialo to
  sens". **W TOKU: workflow projektu wf_fb11d85b-5e0** (docs/przekazanie/wf-gospodarka-od-ludzi.js; dwa pierwsze uruchomienia zatrzymane po minucie, zeby dopisac doprecyzowania):
  3 rozpoznania (kod: co dzis zalezy od ludzi; dane: produkcja / dochod na czlowieka po regionach; towary: kazdy surowiec dzis i wzorzec sredniowieczny na 1000 ludzi) -> projekt
  -> recenzja z poprawka. Wynik: `docs/gospodarka-od-ludzi/PROJEKT-GOSPODARKA-OD-LUDZI-2026-10-13.md` (robocze SCRATCH\gosp-ludzie\). Bez kodu; kod po "tak" Jeffa na pytania
  o kierunek. To jest teraz NAJWAZNIEJSZE zadanie - poprawka lnu (N5b), stawka wojenna (W7) i strojenie podatkow czekaja na ten projekt, bo zmieni dochody wszystkich krolestw.
  UWAGA DO WGRANIA N5: regula A (sufit 4.0 bez kosztu) NIE ma wejsc do gry wlaczona - przed wgraniem ustawic ValveRaiseWhileShort domyslnie false (B, C, D, E zostaja).
- (20:30 UTC 13.10) **TEST 120 dob N5 TOWARY (fin11, regula A jeszcze wlaczona, ale bez skutku przed d169): 120/120, 0 ERROR** (Armoury-2026-10-13_12-59-58.log + tabele, kopie na D:;
  progi SCRATCH\test\progi-n5\). **B skora: uboj bez skory 0, skory surowe 510/d, skora wyprawiona 207/d, miast bez skory wyprawionej srednio 16.3 (bylo 33-43) - TAK. C zbroja:
  t3+t4 45.0/d (bez paczki 29.8; prog 32.8) - TAK; brak dalej rosnie (6 253 na koniec okna - wojsko 111 tys.). D drewno: zapas d120 17 727 = 6 dni zuzycia wedlug regulatora
  (ponizej pasma 30 dni - budowy biora wiecej; do obejrzenia w dluzszym biegu), przyrost +84/d - TAK. E budowy: idzie w 95.4 osadach (bylo 55), 26 poziomow w oknie, wydatki
  28.7 tys. zl/d; dopuszczone w wojnie 74.3, przy froncie 57.1, pan bez luzu zoldu 30.4 - TAK.** Wojsko lordow w wojnie d93-120: **111 158** (najwyzszy wynik dnia). NIE: etap2 0,
  etap5 14, etap8 11 (do przejrzenia po biegu lacznym).
  **POPRAWKA DANINY W5b GOTOWA I SCALONA:** `w-toku/w5b-potrzeba-korony` eea7786 -> zbior-e8 d1c70d3; potem commit **4d33042 = regula A N5 wylaczona domyslnie**
  (ValveRaiseWhileShort false). `w-toku/zbior-e8` = **4d33042** = WGRANIE 42 + N5 (bez A) + W5b; gen_mcm 1338; Armoury 959d544d = test\fin12. W5b: (1) druga stawka SN tylko dla
  daniny z oszczednosci - rosnie o 0.5 pkt na tydzien, gdy srednia 7 dob niepokrytej potrzeby W5 > max(250 zl, 5% naleznego zwrotu), pomiar z co najmniej 4 dob; podatek 184 od
  zarobku po staremu (chleb miast); (2) skarbiec ponad zapas wojny doklada do garnka W5 najwyzej 1/30 nadwyzki dziennie, kolejnosc danina -> skarbiec -> tarczowe. Klucze
  WarAidFollowsLordsShortfall, WarAidLordsShortfallShare 0.05, CrownSurplusFeedsNeedAid, CrownSurplusAidDays 30; do zapisu doszla 1 liczba na krolestwo (29. w szostym polu 165 -
  zgodne wstecz). Progi W5b-0..5 w `--grupa w5`. Szacunek recenzji: Wyspy - skarbiec pokrywa brak ok. pol roku, potem stawka daniny rosnie; przy potrzebie z konca biegu rocznego
  ok. 53 z 59 tys. zl/d zostaje niepokryte tam, gdzie lud nie odklada (Wyspy, Wolni Ludzie, Ibben, Qohor) - danina tego nie zamknie.
  **W GRZE: test 120 dob lacznie N5 (bez A) + W5b** (fin12; test\at-w43-120.out.txt). Po czystym wyniku: progi (etap8 N5, w5 z --csv, w6, kiesa1, etap2), zapis 9 dob,
  WGRANIE 43 (`wgraj.ps1 -DllDir test\fin12 -Tree zb\int -Tag 2026-10-13-noc43`), galaz noc/wgranie-43 = 4d33042, bieg kontrolny 10 dob.

**WGRANIE 43 (13.10 ok. 21:00 UTC; poprawka 5 - towary N5 bez reguly A + poprawka daniny W5b):** Armoury **959d544d** (reszta jak WGRANIE 42: CrashScribe 04f44a0e, settings
3e31434f, RC f9a944c1, GT 035878c4, ForgeView e17e0233). Galaz `noc/wgranie-43` = `w-toku/zbior-e8` **4d33042**. Kopie: `*.bak-2026-10-13-noc43-przed` i
`D:\Backup-Bannerlord\wgrane\2026-10-13-noc43-przed`; md5 OK. Cofniecie: Armoury 772918be z kopii albo klucze (SlaughterKeepsHides, WorkshopHandsStep 1, ValveStepFollowsUse,
CivilWorksAwayFromFront, WarAidFollowsLordsShortfall, CrownSurplusFeedsNeedAid).
- **TEST LACZNY 120 dob przed wgraniem (fin12 + CS-at-hamulec): 120/120, 0 ERROR** (Armoury-2026-10-13_13-28-08.log + tabele, kopie na D:; progi SCRATCH\test\progi-w43\).
  Grupy w5 (z W5b) i w6: 0 NIE; etap2 0 NIE; etap8 8 NIE; kiesa1 2 NIE (P1-1 lokata, P1-9 zloto rodow z luzem - jak wczesniej). **W5b: ze skarbcow ponad zapas wojny wyplacono
  717 612 zl (pula 1.83 mln) w 12 krolestwach, skarbiec nigdy ponizej zapasu (0 / 0 / 0), zamkniecie 0 rozjazdow; regula 1 (stawka daniny z braku wojska) w tym biegu nie musiala
  zadzialac (0 krokow z brakiem ponad prog).** N5: A wylaczona (w logu "sufit przy braku wylaczony"), skora - miast bez skory wyprawionej 13.1, uboj bez skory 0; zbroja t3+t4
  42.6/d; budowy ida w 93.2 osadach, 18 poziomow, 26.6 tys. zl/d. Wojsko lordow w wojnie d93-120: 108 990; chleb swiata 99.0%, miast < 95% srednio 3.9.
- Test zapisu: `autotest-dlugi` + 9 dob: 0 ERROR. Bieg kontrolny po wgraniu w toku (test\at-w43-kontrola10.out.txt).
- **STAN POPRAWEK 1-6 (decyzja Jeffa "robimy 1-6"): 1a danina W39, 1b korona wedlug potrzeby W41 + poprawka daniny W43, 2 tarczowe W42, 3 oblegajacy uchodzi W40, 5 towary W43
  (len / sol - po projekcie gospodarki od ludzi), 6 male poprawki - NIE ZACZETE, 4 bieg dwuletni - na koncu.**
- (21:10 UTC 13.10) Bieg kontrolny PO WGRANIU 43 (10 dob nowej kampanii): 10/10, Armoury 0 ERROR; w grze Armoury 959d544d (md5 sprawdzone). Tydzien 97% - na tym koncie biegnie
  juz tylko workflow projektu gospodarki od ludzi (wf_fb11d85b-5e0); pliki rozpoznan w `docs/gospodarka-od-ludzi/robocze/`. START dla nastepnego konta: PRZEKAZANIE rozdz. 29.
- (22:30 UTC 13.10) **PROJEKT "GOSPODARKA OD LUDZI" GOTOWY (bez kodu; wf_fb11d85b-5e0: 3 rozpoznania, projekt, recenzja z poprawka):**
  `docs/gospodarka-od-ludzi/PROJEKT-GOSPODARKA-OD-LUDZI-2026-10-13.md` (rozdz. 0 prostym jezykiem, 4 tabela krolestw, 4A tabela towarow, 8 paczki, 10 pytania) + rozpoznania
  w `docs/gospodarka-od-ludzi/robocze/`. Regula: wies = symbol wszystkich wsi okregu (produkuje wedlug ludzi okregu z ksiegi; rodzaj wsi mowi CO), miasto = symbol rynku okregu
  (kupuje, zjada, przerabia wedlug ludzi rynku), pieniedzy nikt nie mnozy, werbunek i milicja za ludzmi; hearth, dobrobyt, limity druzyn, liczba osad bez zmian. Towar zmienia sie
  od razu (wies zboza Polnoc ok. 39 szt./d, Reach ok. 125; dzis obie ok. 54), pieniadz powoli: dochod staly rodow dzis -> pierwszy krok -> granica: Reach 53 -> 62 -> 100 tys.
  zl/d, Polnoc 84 -> 75 -> 39, Dorne 45 -> 39 -> 13, Wyspy 20 -> 17 -> 6, Volantis 22 -> 29 -> 60, Qarth 17 -> 23 -> 48. Niepewne: tempo pieniadza; lud ludnych miast nie
  udzwignie wiekszego koszyka bez doplywu zlota. **CZEKA NA JEFFA: pytania o kierunek z rozdz. 10 (podane w czacie); KODU NIE ZACZYNAC przed jego "tak".**
- (22:45 UTC 13.10) **KONIEC PRACY NA KONCIE DRUGIM (sesja dbc3d3d1) - Jeff przechodzi na nastepne konto.** Nic nie biegnie w tle; w grze WGRANIE 43. START: PRZEKAZANIE rozdz. 29,
  blok "STAN KONCOWY". Czeka: 7 odpowiedzi Jeffa do projektu gospodarki od ludzi (maja byc w jego pierwszej wiadomosci), potem kod.
- (22:55 UTC 13.10) **JEFF ODPOWIEDZIAL NA 7 PYTAN PROJEKTU GOSPODARKI OD LUDZI: 1 tak; 2 "lup i milicja bez wyjatku"; 3 tak; 4 "zostawic i zmierzyc"; 5 tak; 6 tak; 7 "tak i tak,
  majatki rodow zostawic tak, jak sa".** Pelne brzmienie w PRZEKAZANIE rozdz. 29 (blok STAN KONCOWY). Kod mozna zaczynac - najpierw rachunek na sucho 85 tys. wojska (pyt. 1).

**NOWE KONTO (sesja c36b73ca; start 13.10 ok. 22:00 UTC wedlug `date -u` - godziny wpisow wyzej z 22:45 / 22:55 byly zawyzone; limity 5 h 0%, tydzien 0%, reset tygodnia 20.10 18:00 UTC).**
Jeff (pierwsza wiadomosc): kolejnosc 1 gospodarka od ludzi (najpierw rachunek na sucho 85 tys. wojska - jesli nie wyjdzie, wrocic z liczbami przed kodem; potem paczki, kazda z testem 120 dob i wgraniem), 2 N7 rozbici wracaja (55/30/15), 3 W6b rod gracza w tarczowym i doplacie, 4 poprawka 6 (male), 5 reszta z rozdz. 29, bieg dwuletni na koncu; zapis kazdej zmiany od razu.
- (22:05 UTC) **W GRZE (lancuch w tle): BIEG ROCZNY WGRANIA 43** (fin12 + CS-at-hamulec, 364 doby; test\at-w43-rok364.out.txt) - zmierzona podstawa "dzis" dla rachunku 85 tys. (poprzedni bieg roczny byl sprzed daniny P1, W5, W6, W5b: 79.4 tys. w d337-364).
- (22:05 UTC) **W TOKU: workflow wf_1a3c45c4-ebb "sucho-85-modele"** (`docs/przekazanie/wf-sucho-85-modele.js`): 4 niezalezne rachunki na papierze (rody - model budzetu rod po rodzie; krolestwa - elastycznosci od gory; pobor - czy werbunek po P4 nadazy; garnek - danina / W5 / W5b / W6 / skarbce po regule), scenariusze S0 dzis, S1 pierwszy krok, S2 granica, S3 nowa kampania ze zlotem startowym wedlug ludzi (pyt. 7) + nakladka P4. Wyniki: SCRATCH(c36b73ca)\sucho85\{rody,krolestwa,pobor,garnek}\RAPORT.md; skrypty sparametryzowane biegiem - po biegu rocznym W43 przeliczyc na nim, potem synteza + krytyka, wynik do Jeffa. KODU REGULY nie zaczynac przed wynikiem.
- (22:15 UTC) **W TOKU rownolegle: N7 ROZBICI WRACAJA POD CHORAGIEW** - workflow wf_1a4abaee-37e (`docs/przekazanie/wf-n7-rozbici-wracaja.js`): autor -> 2 recenzje (kod/zapis/gracz; obieg ludzi/zlota/koni + rachunek skutku) -> poprawki; galaz `w-toku/n7-rozbici-wracaja` od 4d33042, drzewo SC(ae222cb7)\zb\n7. Po nim: test 120 dob + zapis 9 dob (po biegu rocznym w grze), wgranie jako osobne WGRANIE.
- (22:20 UTC) **W TOKU rownolegle: W6b ROD GRACZA W TARCZOWYM I DOPLACIE W5** - workflow wf_d608f5f3-157 (`docs/przekazanie/wf-w6b-rod-gracza.js`): autor -> recenzent z poprawka; galaz `w-toku/w6b-rod-gracza` od 4d33042, drzewo SC(ae222cb7)\zb\w6g. Autotest nie ma gracza z lennem - po wgraniu potrzebny reczny test Jeffa (3 punkty w CHANGELOG).
