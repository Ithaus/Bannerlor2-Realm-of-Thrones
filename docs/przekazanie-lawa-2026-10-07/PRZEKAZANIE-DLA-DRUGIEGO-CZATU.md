# PRZEKAZANIE: stos n131 (naprawy, koszty w miescie, konie, zamowienia) - z sesji f16095a4 do czatu "Realm of Thrones mody - przeglad stanu prac"

Data: 2026-10-07, ok. 14:30. Decyzja Jeffa: "dokoncz i wszystko przekaz jemu" - ten czat konczy prace; skladanie paczek, STAN-PRAC i dalsze kroki sa po Twojej stronie.

STAN: NIC nie wgrane na stale, NIC nie wypchniete, repo Jeffa (C:\Users\GAME\Bannerlor2-Realm-of-Thrones) nietkniete (tylko odczyt).
Armoury.json w Documents - nietkniety (tylko odczyt). Autotesty wgrywaly DLL probne tylko na czas testu i przywracaly zatwierdzone (md5 sprawdzone).

Skroty sciezek:
- TU = C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\f16095a4-010b-4254-b1b4-9353fc402da7\scratchpad\lawa
- LANCUCH = ...\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad\lancuch (klon repo Jeffa; galezie w-toku Jeffa pobrane jako glowne/w-toku/*). Wszystkie commity ponizej sa w tym klonie.
- Build: `dotnet build <Mod>.csproj -c Release -v q --nologo "-p:GameLibs=C:/Users/GAME/Bannerlor2-Realm-of-Thrones/libs"` (sciezka ze slashami! z backslashami MSB sie wywraca).

## 1. Galezie i commity - kolejnosc skladania

Baza: f0aef78 = paczka kwatermistrza (glowne/w-toku/spoils-kwatermistrz-z-materialem). Kazda galaz na poprzedniej:

| Galaz | Commity | Co |
|---|---|---|
| n131b | 81109ab, b36a6d6 | lawa naprawcza (za monete robota + material z targu, bez wrakow) + robota kowali z dniowek x dobrobyt (lawa, kwatermistrz, ludzie, AI) |
| n131c | cb21de2 | warsztaty: dniowka mistrza wedle tieru, place i utrzymanie x poziom plac; NOWY TownWage.cs |
| n131d | a9bb933 | wynajem kuzni w skali historycznej x poziom plac |
| n131e | 3290b01 | budowy: place wedle dniowki miasta targowego |
| n131f | 14504a5 | niewola (RealisticCaptivity): dniowka, warta, dom w stawkach historycznych, placi kasa osady |
| n131g | e010a25 | turnieje (GrandTourney): oplata do kasy miasta, pula dla zwyciezcy, utarg z kasy miasta |
| n131h-puste-kasy-kuznia-za-dzien | 5b121ea, c471705, **1fe5c35, 100112a, ad6f6ce** | kuznia za kazdy dzien + puste kasy; jedna cena dnia kuzni (bez tieru) + rezerwa miasta 20 000; **poprawki z przegladu** (doba kuzni w jednym rejestrze z karnetem BK; naprawa sztuki w ksiedze + apel kwatermistrza; pula turnieju przy oblezeniu) |
| n131i-konie-i-zwierzeta (od c471705) | e10b449, 22336e0, **7d919ee** | 7a konie po cenie targu (rekrut konny, hodowca); 7b zywy inwentarz w cenach historycznych; **poprawka z przegladu: jedna cena konia (ShelfPrice)** |
| n131j-zamowienie-sprzetu-z-polki (od c471705) | ce26642, a645723 | paczka 8: zamowienie sprzetu dla ludzi z polki miasta (cena polki + chodzenie kowala, wybor z polki, brak -> popyt dla warsztatow) |

Worktree: TU\repo (n131h), TU\repo-konie (n131i), TU\repo-zamowienia (n131j).

**Proba zlozenia (zrobiona):** galaz `n131k-sklad-proba` (worktree TU\repo-sklad) = ad6f6ce + merge n131i + merge n131j (2f476d5). BEZ konfliktow; gen_mcm bez zmian w plikach
(Armoury 647, GrandTourney 40, RealisticCaptivity 104); build 3 x kod 0; proby 118/118, 136/136, 130/130 (ponizej); DLL: TU\dll\sklad (Armoury md5 4111b77e, GrandTourney def7574d,
RealisticCaptivity 03dca428). To nie jest galaz do wypchniecia - tylko dowod, ze stos sie sklada.

**Kolizje z galeziami w toku Jeffa (merge-tree n131k-sklad-proba, 07.10 14:25):**
- bez konfliktu: k13-2, k13-3, spoils-kwatermistrz-z-materialem, spoils-najemnicy-prawdziwi;
- konflikt TYLKO w ArmouryBehavior.cs (stara linia konstruktora / rejestracji zdarzen): diagnoza-zatkanych-wsi, k13-1, k13-4, wozy-nie-utykaja-w-miastach, zbroja-craft-jak-bron
  (zbroja-craft: wczesniej sprawdzone - RepairAll uzywa jednej petli przywracania, proba scalenia 59/60, tylko licznik MCM);
- szeroki konflikt (HistoricalPrices, McmSettings, MoneyLedger, RawPrice, CaravanBulk, Settings): l115-l119 (galezie z 06.10, "nowa moneta") - starsze niz baza tego stosu;
  sprawdz, czy sa jeszcze zywe, zanim cokolwiek z nimi skladasz.
- Nowa galaz Jeffa `w-toku/pamiec-drog-rot-uzupelniona` (12:21) - nie sprawdzana.
- Po kazdym scaleniu: `python tools/gen_mcm.py` (Settings.cs jest zrodlem; licznik Armoury 647 na tym stosie).

## 2. Wpisy CHANGELOG (gotowe teksty, format docs/paczki: Problem / Przyczyna / Zmiana / Przyklady / Ryzyko)

- TU\CHANGELOG-wpis.md - paczka 1: lawa naprawcza + robota kowali z dniowek x dobrobyt (z liczba z autotestu: AI naprawy 4.06 d/szt. zamiast 12-13)
- TU\CHANGELOG-wpis-2-warsztaty.md - paczka 2: warsztaty (z autotestem 12:15)
- TU\CHANGELOG-wpisy-3-6.md - paczki 3-6: kuznia, budowy, niewola, turnieje + na koncu c471705 (jedna cena dnia kuzni, rezerwa miasta). Liczby kuzni w pierwszym wpisie sa nieaktualne (dopisek w naglowku).
- TU\raporty\CHANGELOG-wpis-7ab-konie-zwierzeta.md - 7a + 7b (z dopiskiem o zmianie wzoru ceny konia po przegladzie)
- TU\raporty\CHANGELOG-wpis-8-zamowienia.md - paczka 8
- TU\raporty\CHANGELOG-wpis-9-przeglad-poprawki.md - 4 poprawki z przegladu (1fe5c35, 100112a, ad6f6ce, 7d919ee)
Numery NN nada skladanie.

## 3. Proby poza gra (prawdziwy Harmony, DLL gry z libs, prawdziwy DLL modu przez refleksje, atrapa swiata)

| Proba | Uruchomienie | Wynik |
|---|---|---|
| TU\proba | `powershell -NoProfile -ExecutionPolicy Bypass -File TU\proba\uruchom.ps1 -DllNowa <Armoury.dll> -Out <katalog>`; zmienne PROBA_RC i PROBA_GT = sciezki DLL RealisticCaptivity / GrandTourney (sekcje 14, 15) | n131h z poprawkami: 118/118; stos zlozony: 118/118; baza f0aef78: 7/7 |
| TU\proba-konie | jw. | n131i z poprawka: 136/136; stos zlozony: 136/136 |
| TU\proba-zamowienia | jw. | n131j: 130/130; stos zlozony: 130/130 |
| TU\proba-kwatermistrz | zmienna REGULA_KOWALI 0/1 | z paczki kwatermistrza |

Licznik ustawien w sekcji 1 wszystkich prob liczy sie teraz z obecnosci pol (641 + nowe pola) - dziala na kazdej galezi i na zlozeniu.
Stary DLL c471705 na nowej probie: 107/108 (oblewa sekcje 16 - dowod, ze test lapie blad naprawy sztuki).

## 4. Autotesty w grze (zgoda Jeffa 07.10; skrypt LANCUCH-owy: ...\3cf3e0ac...\scratchpad\dzien-6\autotest\repo\tools\autotest.ps1, CrashScribe probny ...\dzien-6\autotest\do-gry\CrashScribe.dll md5 cafd9ae8)

- 12:15 - n131c (naprawy + warsztaty), 40 dob, OK: warsztaty broni 29 732 szt. (31 358 / 30 284 bez paczki), koszt/szt. 17.9 (13.7-14.7), sprzedaz/koszt 2.55 (3.11), naprawy AI 4.06 d/szt. (12-13), ERROR 0; zdrowie ekonomii w granicach rozrzutu (bez bankructw, zloto lordow ok, sprzet ok, ruda ok).
- 14:18 - CALY STOS ZLOZONY (TU\dll\sklad: Armoury + GrandTourney + RealisticCaptivity), run at-20261007-141807: OK 40/40 dob, kod 0, 12.8 s/dobe; DLL Jeffa przywrocone (md5 OK), zapisy Jeffa nietkniete; CrashScribe: bledy 8, okna zamkniete 3, hang 14:29:45; silnik wywrocil sie dopiero po QuitGame (0xC0000094, znany blad wyjscia). Logi: Armoury-2026-10-07_14-18-31.log, CrashScribe session-2026-10-07_14-18-30.log, RealisticCaptivity-2026-10-07_14-18-31.log; wynik skryptu TU\autotest-sklad.txt. Analiza logow (bledy, ekonomia wobec 12:15/12:37/10:40/08:03, nowe funkcje, konie) - workflow wf_283fe6bf-e3d, wynik doslany osobna wiadomoscia i w sekcji 10.
- Zatwierdzone DLL Jeffa (przywracane po kazdym tescie): Armoury 25b87631, CrashScribe 11fa0214, GrandTourney 0aa5d0ef, RealisticCaptivity c393f4fb.

## 5. Przeglad niezalezny (07.10, workflow wf_bd58f0e5-4ca; 59 agentow: 5 wymiarow + 3 sceptykow na zgloszenie)

34 zgloszenia, 10 potwierdzonych (6 roznych usterek) - WSZYSTKIE poprawione (sekcja 1, wpis 9). 8 odrzuconych. 16 niskich bez weryfikacji - do Twojej oceny:
1. HarnessLabor przy wylaczonej regule rozni sie od starego RepairCost o 1 zl (zaokraglenie) - ArmouryBehavior.cs ok. :2073.
2. MendMaterial.cs:446 - brak 1e9 w szacunku przepelnia int przy liczeniu sztuk (porzadek "najtansze najpierw").
3. Ta sama zalozona sztuka ma dwie ceny kowala: "Pick" od modyfikatora, naprawa uprzezy od stanu w ksiedze (SmithMenu.cs:413). Projekt kosztu naprawy drugiego czatu (pkt 1 "CONDITION") to rozwiazuje.
4. Stawka godzinowa BK przy ForgeHireHistorical = dzien/8 = 0.375 d - BK obcina do 0 (tylko gdy ForgeOneClock wylaczony i brak karnetu).
5. (poprawione w 1fe5c35) karnet BK bez pieniedzy.
6. Ekran klanu pokazuje utrzymanie warsztatu bez poziomu plac, a placi sie z nim (WorkshopTrade.cs:763).
7. Niewola: dniowki i warta nie patrza na rezerwe miasta 20 000 (skup domu i utarg turnieju patrza) - Work.cs:109. Do decyzji: dniowka to drobna kwota; Jeff kazal "puste kasy pilnowac" - teraz pilnuje zera, nie rezerwy.
8. Niewola: Math.Max(1) nad RoundRandomized przeplaca dniowki ponizej pensa i kasuje polowe placy po nasyceniu (Work.cs:118).
9. Niewola: przy wylaczonym HistoricalTownRates sprzedaz domu nieaktywna przy cenie 0 (Homes.cs:216).
10. Niewola: pusta kasa nie zatrzymuje pracy w toku, niezaplacone dni licza sie do nasycenia (Work.cs:336).
11. Niewola: menu pracy pokazuje wlasny losowy rzut dniowki, nie kwote wyplacona (Work.cs:312).
12. Konie: kon po cenie targu moze wypchnac koszt najemnika konnego ponad prog gry 5000 dla AI (RecruitCost.cs:53) - AI go nie zaciagnie.
13. Konie: mnozniki prawa BK i perkow mnoza tez konia (RecruitCost.cs:57).
14. Zamowienia: pusta kiesa tlumi wpis popytu, gdy polce tez brakowalo sztuk (SmithMenu.cs:1877).
15. AiWear.cs:235 - rezerwa AI na naprawy liczona przy poziomie plac 1, naprawa przy poziomie miasta.
16. = 7 (drugi wymiar).
Wyniki: ...\f16095a4-...\subagents\workflows\wf_bd58f0e5-4ca\journal.jsonl; skrypt: ...\.claude\projects\...\f16095a4-...\workflows\scripts\przeglad-stosu-n131-wf_bd58f0e5-4ca.js.

## 6. Decyzje Jeffa 07.10 (z tej sesji, doslownie tam, gdzie wazne)

- Robota kowala zalezna od dobrobytu miasta; "wszelkie koszty w danym miescie ... zalezne od dobrobytu i stawek historycznych"; "wszystko co dotyczy pieniadza placenia musi byc spojne".
- "za kazdy dzien kuznia"; "koszt kuzni to koszt kuzni, a co ja kuje to moja sprawa" (dzien kuzni bez tieru).
- Puste kasy pilnowac wszedzie.
- Rezerwa miasta 20 000 (TownRentFloorGold) - zostaje; pomysl miast z dlugami / pozyczkami / podatkiem nadzwyczajnym - "zapisz to na pozniej" (PLAN-KOSZTY-MIASTA-2026-10-07.md sekcja 6; pamiec jeff-pomysl-kasy-miast-dlugi).
- Paczki 7a, 7b, 7c - TAK. Paczka 8: "cena tak, wybor tak, brak towarow tak".
- Audyt produkcji: ile czego sie hoduje / wytwarza i gdzie, "bez oliwek w sniegu", warsztaty w miastach dobrane do surowcow okolicznych wsi (wsie z ruda -> cos z ruda w miescie), zgodnie z lore.
- LORE: "trzymajmy sie lore swiata" - smoki TYLKO Daenerys + misja gracza; mamuty tylko Za Murem / Polnoc; wielblady tylko w cieplych krainach; (jednorozce - Skagos). Dorne: lore mowi o piaskowych rumakach - do decyzji Jeffa, czy wielblady tam zostaja.
- Konie (raport DIAGNOZA-NADMIARU-KONI): P0 pomiar (linia "Konie:") - TAK. P1 hodowla wedle popytu - TAK + "moze stadnin jest za duzo" (sprawdzic 71 stadnin wobec lore regionow i zbytu). P2 ZMIENIONE: "kon ginie jak ginie, jak nie ginie to nie ginie" - bez sztucznego odsetka; kon ginie tylko zabity, ocalaly kon poleglego / pokonanego = lup zwyciezcy (takze AI); bitwy z graczem - liczyc naprawde zabite; AI-AI - regula z proporcji zmierzonych w prawdziwych bitwach; przegrani odkupuja konie. P3 juczne dla karawan i taborow z targu - TAK ("nie ma nic za darmo, zadnego sztucznego dosypywania"). Higiena ceny konia - zalecane.
- "Musza miec zapasowe konie" + "zwieksza udzwig, masz konia i juki": (1) srednia cena konia gry (PartiesBuyHorseCampaignBehavior.CalculateAverageHorsePrice) liczona tylko ze zwyklych wierzchowcow w handlu (dzis z wpisow ROT: smoki, jednorozce, rydwany, slon, mamut -> ok. 11 000 zamiast ok. 1 100, wiec lordowie nie kupuja koni; po poprawce kupuja zapasowe wedle zasady gry - ok. 8% zlota, max ok. 7 przy 100 tys.);
  (2) udzwig: gra (DefaultInventoryCapacityModel, sprawdzone): czlowiek 20 kg, zapasowy kon wierzchowy 20 kg, juczny / mul 100 kg, kon pod jezdzcem 0. Historycznie kon niesie ok. 20-25% swojej wagi (100-120 kg): pod jezdzcem prawie pelny (+ ok. 10 kg juk), zapasowy z jukami 60-80 kg, juczny 100-120, mul 90-110.
  PROPOZYCJA (do wykonania przez Ciebie): zapasowy kon wierzchowy 60 kg, jezdziec +10 kg, juczne bez zmian. UWAGA: MarchPace.cs (Armoury) czyta udzwig przy tempie marszu - zmiana przesunie tempo; sprawdz czynny model udzwigu (RBM / BK / RealisticBannerlord moga go podmieniac).

## 7. Styki z Twoja praca (WAZNE)

**DECYZJE JEFFA 07.10 ok. 14:25 (po zobaczeniu, ze oba czaty robily koszt naprawy):**
- "nasza robocizna jest lepsza" - robocizna z tego stosu (n131b b36a6d6: 0.2 x ubytek stanu x robota wykonania x dobrobyt miasta) ZOSTAJE.
  Projektu cedac4d (koszt naprawy wedle godzin pracy) NIE wdrazac w jej miejsce; inne elementy tego projektu (godziny zajmujace rzemieslnikow, limit AI, rabat do 30%) - tylko na wyrazne slowo Jeffa.
- "nie puszczaj zadnych innych prac w drugim czacie, wszystko idzie w jednym" - od teraz WSZYSTKO w jednym czacie (u Ciebie). Zadnych rownoleglych prac w innych czatach;
  przed kazda praca sprawdzic, czy temat nie jest juz robiony.

Tematy, ktore oba czaty ruszaly (do pilnowania przy skladaniu): koszt naprawy (wyzej); warsztaty (Twoje 116/123/124 - komu i za ile; tu n131c - place i utrzymanie x dobrobyt,
te same pliki WorkshopTrade/WorkshopLaw); rzemioslo miasta k13-3 (3 d bez dobrobytu - sprzeczne z decyzja "wszystkie place w miescie x dobrobyt", przelaczyc);
odziez wojska k13-4 i paczka 8 tutaj (obie kupuja z polki miasta dla wojska - sprawdzic, czy polka nie liczy sie dwa razy); woz zabiera caly magazyn (Twoje 122) - raport koni
wskazuje to jako jedna z przyczyn nadmiaru koni w miastach; skala produkcji K13 i audyty produkcji z 04.10 vs audyt geografii i lore tutaj (poprawki w jednym miejscu);
zbroja z kuzni jak bron vs oplata za kuznie (ten sam Forge/RepairAll - scalenie sprawdzone); badanie strat przegranych w bitwach (Twoje 08:35) vs P2 koni (konie gina tylko zabite).

1. **Projekt kosztu naprawy wedle godzin pracy** (repo Jeffa cedac4d, docs/PROJEKT-KOSZT-NAPRAWY-2026-10-07.md): ten stos juz prowadzi WSZYSTKIE naprawy w miescie (lawa, "Pick a piece", uprzaz, polki wojska, ludzie - TroopSelfMend, AI - AiWear) przez jedna funkcje robocizny `MendMaterial.Labor(it, share, st, fallback)` / `LaborF` = share x robocizna wykonania (HistoricalPrices.MakingLabor = HistDays x DayWageOf) x (1 + zysk) x `TownWage.Index(miasto)`, gdzie share = MendMaterialMaxShare (0.2) x brak stanu; material = MendMaterial.Bench (0.2 x brak przepisu, z polki, cale sztuki). Twoj projekt ma te same skladniki (D x dniowka x (1+zysk), material 131 bez zmian), inne udzialy godzin (z historii) i nie ma dobrobytu miasta. DECYZJA JEFFA: zostaje robocizna tego stosu - projektu nie wdrazac w jej miejsce (patrz wyzej). Gdyby Jeff kiedys kazal zmienic udzialy, to jest JEDNO miejsce (`MendMaterial.LaborF`), z zachowaniem `TownWage.Index`. Dla porownania obecny stos: miecz Damaged robota ok. 9-10 zl (Twoj projekt 5), helm Damaged ok. 25 (13), Brigandine Battered ok. 194 (113) - przy poziomie plac 1.
2. k13-3 (TownCrafts / rzemioslo miasta) placi 3 d bez TownWage - przy skladaniu przelaczyc na `WorkshopLaw.DayWage(it, town)` / `TownWage.Index` (spojnosc place w miescie).
3. Linia konstruktora / rejestracji zdarzen ArmouryBehavior - jedyny konflikt z k13-1, k13-4, diagnoza-zatkanych-wsi, wozy-nie-utykaja, zbroja-craft (patrz sekcja 1).
4. MarchPace.cs czyta udzwig - przy zmianie udzwigu koni (sekcja 6).
5. GrandTourney i RealisticCaptivity czytaja poziom plac i rezerwe z Armoury przez refleksje (TownWageLink.cs; bez Armoury - 1 i cala kasa). Nazwy `Armoury.TownWage.Index(Settlement)` i `Armoury.Settings.TownRentFloorGold` sa kontraktem - nie zmieniac nazw bez tych plikow.

## 8. Otwarte (do zrobienia - nic z tego nie zaczete w kodzie)

1. Konie wg DIAGNOZA-NADMIARU-KONI.md (TU\raporty): P0 pomiar, P1 hodowla wedle popytu + przeglad liczby stadnin (71: europe 20, desert 15, steppe 13, vlandian 10, sturgian 7, battanian 6), P2 straty koni tylko zabite + lup, P3 juczne z targu, higiena sredniej ceny konia (zapasowe konie), udzwig 60 kg / +10 kg.
2. Audyt produkcji (TU\raporty\AUDYT-PRODUKCJI-I-WARSZTATOW.md, 674 linii): bawelna w zimnych krainach (10 z 13 wsi), 12 wsi wbrew klimatowi, tlocznie oliwy w sniegu w 62% kampanii (losowy wybor warsztatow), 24 miasta z kopalniami - kuznia tylko w ok. 55%, 76 z 291 warsztatow bez surowca z wlasnych wsi. Zmiany danych swiata = nowa kampania i decyzje Jeffa.
3. Blad CrashScribe Mends.cs:2134 / :2140 - sprawdza zamek wsi zamiast miasta handlowego; dziala po przydziale warsztatow (z audytu).
4. Dorne: wielblady vs piaskowe rumaki (lore) - decyzja Jeffa.
5. Kasy miast w tarapatach (dlugi, pozyczki, podatek nadzwyczajny) - NA POZNIEJ, bez slowa Jeffa nie zaczynac.
6. 16 niskich zgloszen z przegladu (sekcja 5).
7. Do obserwacji w logach po wgraniu: hodowca sprzedaje zwykle przy pustej polce miasta - po 7d919ee cena idzie za podaza i popytem (do SupplyDemandMaxFactor; szacunek: hunter ok. 8650 w miescie-medianie); AI zaciaga mniej konnych (prog 5000); dochod wsi z hodowli spada (owce 320 -> 68 / dobe); dwie nowe latki na prywatnych metodach gry (RecruitCost - w logu "wpiete w 2/2").

## 9. Dokumenty tej sesji (TU)

STAN-SESJI.md (pelny stan i decyzje), PLAN-KOSZTY-MIASTA-2026-10-07.md (plan paczek 1-8 i sekcja 6 "na pozniej"), raporty\DIAGNOZA-NADMIARU-KONI.md, raporty\AUDYT-PRODUKCJI-I-WARSZTATOW.md,
wpisy CHANGELOG (sekcja 2), wyniki prob: TU\wyniki-przeglad*, TU\wyniki-sklad-*; autotest: TU\autotest-sklad.txt. Skrypty latek: TU\patch_*.py (kotwice, CRLF).

## 10. Wynik autotestu calego stosu (14:18)

Przebieg OK 40/40 (szczegoly w sekcji 4). Analiza logow w toku - wynik doslany wiadomoscia i dopisany tutaj.
