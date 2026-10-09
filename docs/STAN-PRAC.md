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
