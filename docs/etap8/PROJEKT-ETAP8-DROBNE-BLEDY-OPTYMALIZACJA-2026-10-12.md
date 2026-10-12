# PROJEKT ETAP 8 - drobne bledy, Twoje sprawdzenia, optymalizacja (PLAN 8.1-8.18) - 12.10.2026, wersja 2 (po krytyce)

Status: SAM PROJEKT. Kod, gra, ustawienia i zapisy nietkniete, nic nie budowane. Wzor: `docs\etap7\PROJEKT-ETAP7-MAPA-DROGI-WIOSKI-2026-10-12.md`.
Krytyka (dwa niezalezne przeglady: logika/decyzje i kod/latki/koszt) - ZROBIONA 12.10. v2 wprowadza przyjete uwagi (oznaczone "v2"); rozliczenie w rozdz. 8 i w `docs\etap8\KRYTYKA-E8.md`.

**Decyzje Jeffa, ktore wiaza ten projekt:**
- 12.10 10:25 UTC: etap 8 osobno, NIE "przy okazji" paczek etapow 6/7; wlasne paczki i wlasny test (SP:1248-1249).
- 12.10 10:30 UTC: kod etapow 5, 6, 7 i 8 z recenzja i wylacznikami MCM, potem JEDNO scalenie i jeden dokladny test (120 dob + zapis + zima + bieg dlugi), poprawki, wgranie (SP:1263-1266);
  etap 8 obejmuje tez OPTYMALIZACJE - "gra ma dzialac szybciej" (SP:1250, PLAN 8.17).
- 12.10 09:50 UTC: "nie mamy na sile i sztucznie robic dobrze lordom" - brak pieniedzy = pozyczka albo mniejsze wojsko, zadnych doplat (SP:1242-1244); "nie robimy sztucznych praw" (SP:1197).
- 12.10 11:40 UTC: ogolny poziom cen ZOSTAJE (1 d = 1 pens, zgodny z zoldem); TAK dla odchylek cennika (rzad konski z prawdziwej wagi, helmy t4-6, luki t3-6, opcjonalnie konie
  wedlug kategorii - 8.5) i TAK dla dochodow gracza w nowych cenach (nagroda z kiesy zleceniodawcy liczona w dniach zoldu druzyny, turnieje i okupy w skali - 8.18) (SP:1279-1281).
- 08.10 (SP:354, :448): werbunek gracza "jak u AI" - rekrut od tieru 2 tylko ze sprzetem kupionym przez notabla (PLAN 1.12 - zdecydowane, NIEZROBIONE).
- 12.10 (E5 decyzja 9, RealisticCaptivity): pojmujacy bierze cale zloto gracza do siebie - lord do kiesy, milicja/zaloga/osada do kasy osady, banda do kiesy partii (KR:CaptivityBehavior.cs:494-499).
- Jeff zacznie NOWA kampanie po skonczeniu (NK dozwolone), stary zapis ma sie wczytac bez bledu. Napisy w grze i opisy MCM po angielsku.

Podstawa (dowody z rozpoznan nie sa tu powtarzane w calosci):
- rozpoznania `docs\etap8\rozpoznanie-A.md` (**RA8**: 8.1, 8.2, 8.4, 8.6, 8.7, 8.8, 8.10), `rozpoznanie-B.md` (**RB8**: 8.3, 8.5, 8.9, 8.14, 8.18), `rozpoznanie-C.md` (**RC8**: 8.11-8.13,
  8.15, 8.16, "poza zakresem" etapow 5-7, ERRORS.md), `rozpoznanie-D-optymalizacja.md` (**RD8**: 8.17);
- kod: SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad`; **K:** = `SCR\zb\58\Armoury\src`
  (galaz `w-toku/zbior-5-8`; rozpoznania na b461cc5, dzis a119682 - roznica to recenzja E5/A8 i E5/BITWY, bez wplywu na pozycje 8.x poza jednym wpisem "poza zakresem"), **KC:** CrashScribe\src,
  **KR:** RealisticCaptivity\src, **KF:** ForgeView\src, **KG:** GrandTourney (to samo drzewo); **CH** = `SCR\zb\58\CHANGELOG.md`;
- dekompilacje: **G:** `SCR\dec32\cs_full` (gra 1.4.8), **BK:** `SCR\e4tools\bkfull`, **BR:** BKROTPatch (`bkrotdec`), **DTE:** `SCR\audyt-zloto\DTE`, **RBM:** `SCR\e8A\rbmcombat.cs`,
  **SW:** Spoils of War (`SCR\e8A\donate.cs`, `lootcol.cs`), **SB:** SandBox (`SCR\e410\sandbox`), **BEE:** `SCR\a6dec\bee`;
- logi: **L:N** = `Modules\Armoury\Armoury-2026-10-12_02-44-09.log` (120 dob nowej kampanii, WGRANIE 33; doba 1 = dzien 108836); **CS:N** = `Documents\...\CrashScribe\session-2026-10-12_02-44-08.log`;
  **L728:N** = `SCR\kopia-728\Armoury-2026-10-10_20-58-02.log` (dlugi bieg 728 dob, starszy DLL); **J:N** = Armoury.json Jeffa (`Documents\...\Configs\ModSettings\Global\Armoury\Armoury.json`);
  **ID** = `CrashScribe\items-dump.csv` (12.10 03:22, ceny po HistoricalPrices); skrypty analizy czasu `SCR\e8d\*.py` (RD8);
- dokumenty (docs\): **PLAN** = PLAN-DO-KONCA-MODA-2026-10-09.md (etap 8: :220-245), **SP** = STAN-PRAC.md, **CD** = CENY-A-DOCHODY-2026-10-12.md, **a13** = audyt-2026-10-09\13-UMIEJETNOSCI.md,
  **A05** = AUDYT-2026-10-05-EKONOMIA-WOJNA-SUROWCE-LORDOWIE.md, **OT** = OTWARTE-2026-09-01.md, **LAWA** = przekazanie-lawa-2026-10-07\PRZEKAZANIE-DLA-DRUGIEGO-CZATU.md:74-90.

Oznaczenia: [K] kod plik:linia, [P] pomiar z logu, [S] szacunek, [W] wniosek z kodu (niesprawdzony w grze), [D] decyzja Jeffa. Rok gry = 364 doby (kalendarz Armoury). Linie logu po polsku,
napisy w grze po angielsku. Kultury ROT zawsze przez obiekt kultury (CLAUDE.md 2). "W CIENIU" = paczka w kodzie, klucz domyslnie wylaczony, linia logu liczy, co by zrobila;
wlaczamy po odczycie testu zbiorczego (faza poprawek), bez nowego pytania do Jeffa.

---

## 0. Dla Jeffa - prostym jezykiem

1. **Etap 8 to 29 malych paczek.** Kazda ma wlasny wylacznik w MCM i wpis w CHANGELOG. Nic nie jest dokladane "przy okazji" do etapow 5-7. Kod powstaje teraz razem z reszta,
   a sprawdzamy wszystko w jednym tescie zbiorczym, tak jak chciales.
2. **Szybkosc gry (8.17) - uczciwie.** Doba autotestu trwa dzis srednio 17.6 s: pierwsza 13.5 s, setna dwudziesta 18.3 s (L:686, L:35846). W dlugim biegu jest to 21.5 s, a najdluzsza doba 38.8 s.
   Czas rosnie razem z wielkoscia swiata (z 2 319 do 5 201 partii na mapie w 120 dob), a nie z naszymi petlami. Nasze mody biora razem ok. 2.5-3.1 s na dobe (15-18%), a nie 7%, jak liczylismy
   do tej pory - "7%" obejmowalo tylko czesc z nich. Pewne poprawki (polka, warsztaty, wywoz kupcow, zapis logu) dadza **ok. 0.3-0.7 s na dobe [S], czyli kilka procent** (v2: wywoz bez wycen, ktore liczy juz polka). Gdyby nasze mody nie kosztowaly nic,
   gra bylaby najwyzej o ok. 15-18% szybsza. Reszta czasu to gra, AI i cudze mody. Najwiecej daloby zmniejszenie liczby partii na swiecie, ale to decyzja o rozgrywce - pytanie 11.
   Najpierw jedna linia w logu z profilem doby (E8/0): pokaze, co naprawde kosztuje i ile trwa "przyciecie" raz na dobe (szacunek 0.8-1.5 s w jednej klatce).
3. **Dochody gracza (8.18).** Dzis prawie kazda nagroda za zadanie bierze sie z niczego, w starej skali. Kryjowka daje 3 000, czyli ok. 7 miesiecy dochodu rycerza (5 000 = rok). Bedzie tak (v2):
   - zaplaci zleceniodawca ze swojej kiesy, a jesli ma mniej, dostaniesz mniej; to, co Ty placisz w zadaniu (lapowki, zakupy, oddane podatki), tez do kogos trafia, a nie w nicosc;
   - kwota = praca Twojej druzyny w dniach zoldu, ustalona w chwili przyjecia zadania (ile obiecano, tyle placa), na przyklad kryjowka ok. 440, szkolenie zbrojnych ok. 1 600; zadania ze stala kwota tez w tej skali;
   - towar w zadaniach przestaje znikac i brac sie z niczego: dostawa trafia do zleceniodawcy albo na polke osady, rzecz na wymiane idzie z magazynu osady;
   - nagrody turniejow AI i jency sprzedawani w miescie (takze Twoi u posrednika) juz od etapu 3 ida z kasy miasta - bez zmian; nowa jest tylko Twoja nagroda turnieju: przedmiot z polki miasta-gospodarza albo jego cena od miasta;
   - zaklady turniejowe, lapowka przy odbijaniu wieznia, gra planszowa i nagroda areny - z kiesy drugiej strony albo z kasy miasta;
   - lapowka strazy spadnie do 60 / 36 / 6, golibroda do 3, karczmarz do 12 - pieniadze ida do zalogi albo do ludu miasta, a nie w nicosc.
   Zlecenia kowalskie (wykucie broni dla notabla) placa dzis tysiace z niczego, bo ich cena nie przechodzi przez nasze ceny historyczne. Bedzie normalna cena i zaplaci zleceniodawca; wykuta bron trafi do niego albo na polke miasta, a lord nie dostanie juz zolnierza z niczego.
4. **Ceny (8.5).**
   - Barda t6 kosztuje dzis 11 522, bedzie ok. 2 500. ROT liczy ja jako 140 kg zelaza, my bierzemy wage prawdziwej bardy.
   - Siodlo t0: 588 -> ok. 145. Helmy t4/t5/t6: 97/176/298 -> 39/80/127. Luki t3-t6: 38-159 -> 19-48.
   - Oszczepy kosztuja dzis 5x tyle co strzaly (srednio 107 wobec 20), bo cennik liczy je jak snop strzal. Bedzie to wyrownane.
   - "Premia wojenna" kupcow (do +63% w Tyrosh) liczy wojne drugi raz, obok prawdziwych zakupow armii - wylaczamy ja.
   - Konie wedlug kategorii (zwykly t3 taniej, rumak t4 drozej) to tylko opcja - pytanie 2.
5. **Lordowie i ich sprzet (8.8, 8.4).**
   - Zaleglosc obitych sztuk u AI rosnie bez konca: 90 -> 71 432 w 120 dob, 122 054 w dobie 728. Naprawy staja na pierwszej sztuce, na ktora nie stac lorda.
   - Kazda bitwa obija 4% sprzetu, takze swiezo kupionego, niezaleznie od tego, czy byla potyczka, czy rzez.
   - Zaloga miasta zaplacila w dobie 120 srednio 931 zl za zbroje korpusu, lordowie 63 (L:36029). Sufit ceny najpierw zmierzymy, potem wlaczymy.
   - AI w ogole nie zuzywa strzal w swoich bitwach.
   Po zmianie: naprawy ida po kolei, zuzycie zalezy od tego, ilu ludzi padlo, strzaly schodza po bitwie. Lordowie zaplaca za to sami - bez doplat.
6. **Lup (8.6, 8.2).**
   - Wraki i cala zdobyta zbrojownia pokonanych ida dzis w 100% do Ciebie, bez trzeciej kapitana - pytanie 1.
   - DTE kopiuje sprzet pokonanego, ktory przezyl (zaloga po oblezeniu), zamiast go przenosic. Dla koni naprawilismy to w etapie 6, reszte robimy teraz.
   - "Donate to town" w Spoils kasuje oddane sztuki. Beda trafiac do zbrojowni zalogi tego miasta.
7. **Doswiadczenie (8.1).**
   - RBM daje bohaterowi XP za kazdy wypuszczony pocisk, takze chybiony i na arenie. Bedzie tylko w prawdziwej bitwie i mniej.
   - Zarzadca uczy sie od ilosci zjedzonego jedzenia, wiec zima uczy sie o polowe szybciej. Zwiad rosnie z liczba ludzi.
   - Najpierw ksiega umiejetnosci: dzis nikt nie liczy, skad lordowie biora XP.
8. **Porzadek (8.11, 8.13).**
   - Po wczytaniu innego zapisu w tej samej sesji stajnie AI nie kupuja koni przez tyle dob, o ile stara kampania byla dalej. Zostaje tez stary oboz, kryjowka i pozycja przy Bliznakach.
   - Klimat Westeros gasnie do konca sesji po jednym bledzie.
   - BannerKings rzuca wyjatkiem przy tworzeniu postaci (CS:1521). BKROTPatch go polyka, wiec gra i inne mody dzialaja dalej (v2 - poprawiona diagnoza); przerywa sie tylko korekta edukacji BK.
     Najpierw sprawdzimy w zapisie nowej kampanii, czy postac ma jezyk; latamy tylko, jesli go nie ma.
   Reszte naprawiamy.
9. **Karawany i najemnicy (8.12).**
   - Latka BKROT pozwala karawanie BK decydowac raz na dobe. Karawana stoi przez to w kazdym miescie srednio ok. 3 doby zamiast ok. 3 godzin. Dzis 29 karawan stoi w jednym miescie 7+ dob (L:35942).
   - BK co dobe wyrzuca wodza klanu najemnikow z jego druzyny i wstawia zolnierza z niczego. Klan bez sluzby dostaje 3-4 ludzi na druzyne dziennie, tez z niczego.
   - Dwie karawany lordow czekaja na wyspach 118 dob. Rozwiazujemy tylko karawany bez drogi (wyspa, rod bez statkow), nie "po czasie postoju" (v2).
   Pytania 3 i 4, rada TAK.
10. **Bliznaki.** Twoje ustawienie "the bridge watch also turns back hostile AI lord parties" (masz je wlaczone, J:154) dzis nie dziala w ogole, bo petla AI biegnie tylko wtedy, gdy Ty stoisz
    przy moscie. Ozywiamy ja na zegarze gry. Spiacych lordow nie rusza.
11. **Start nowej kampanii (8.10, NK).**
    - Platnerze traca czesc budzetu startowego przy pechowym losowaniu.
    - Ok. 240 partii lordow z pierwszej doby nie dostaje "dorobku stuleci", wiec pierwsze dni to fala zakupow.
    - StartKit moze zostawic konia bez pasujacej uprzezy.
    - Do tego Twoja decyzja z 08.10: rekruci t2+ bez darmowego kompletu - jak u AI: ochotnik ma to, co kupil mu notabl, i swoj dobytek, a najemnik z karczmy swoj dobytek oraz konia z rzedem (v2).
12. **BetterEconomy (8.14).** Dopisujemy do skryptu jeden klucz: losowe "susze" i "zarazy bydla" BEE na 0. Skrypt uruchamiasz Ty albo agent przy wgraniu koncowym.
    "Sol i piwo jako potrzeby" nie da sie ustawic w pliku BEE (lista jest zaszyta w kodzie BEE i przy BK nic nie zmienia). U nas sol i piwo i tak juz sa potrzebami wsi - odrzucamy ten punkt.
13. **Twoje testy (8.16)** - lista krok po kroku na koncu rozdzialu 3 (29 punktow). Autotest nie gra kuzni, misji, zadan, turniejow gracza ani niewoli.
14. **Pytania** (rozdz. 6) - 11, kazde z rada. Pilne dla kodu sa 1-5. Reszte rozstrzyga projekt.

---

## 1. Co dzis (liczby)

Z33 = WGRANIE 33, 120 dob nowej kampanii (L:N, CS:N), dlugi bieg L728. "Cel" = prog projektu (rozdz. 4).

| Pozycja | Dzis | Cel etapu 8 | Dowod |
|---|---:|---|---|
| **doba gry (zegar)** | d1 13 454 ms, d120 18 310 ms; mediana 120 dob 17.6 s; 728 dob: mediana 21.5 s, maks. 38.8 s | nasze mody -0.3 s/dobe (v2) bez zmiany liczb ekonomii | L:686, L:35846, L728:568-183678; RD8 2.1 |
| "nasze 171-174b" (podzbior) / nasze razem | 1 264 ms (6.9%) d120 / ok. 2.5-3.1 s (15-18%) | pelny profil w logu (E8/0) | L:35846; RD8 2.2 |
| WorkshopLaw.CyclePrefix | 840 ms / 7 093 cykli (d120), srednio 806 ms | <= 500 ms | L:35846; RD8 K2 |
| ceny sprzetu (PricePostfix, poza suma) | 617 ms/d (45 tys. wywolan), 728 dob 1 015 ms, rekord 7 553 | <= 450 ms | RD8 2.2, K3 |
| wywoz kupcow (jedna klatka) | 608 ms (d1) -> 312.5 ms (d120); 728 dob maks. 1 621 | <= 220 ms po O3, <= 150 ms po O1+O3 (v2) | L:581, L:36032; RD8 K4 |
| przebudowy pamieci polki (ShelfIndex) | ok. 13.8 tys./dobe | <= 9 tys. | RD8 K1 |
| takt doby `ArmouryBehavior.OnDailyTick` (jedna klatka, ok. 108 wywolan `.Daily()`) | niezmierzony; szacunek 0.8-1.5 s | zmierzony; <= 1.0 s po O3/O5 | K:ArmouryBehavior.cs (108 x `.Daily()`); RD8 2.3 |
| towar bez odbiorcy w zasiegu 250 | 1 925 -> 13 909 szt. | INFO (8.5, polki) | L:581, L:36032 |
| partie na mapie | 2 319 -> 5 201 (r = 0.89 czasu doby z ludzmi w sluzbie) | INFO (pytanie 11) | RD8 2.1; L:36093 |
| **zaleglosc obitych sztuk AI** | 90 (d1) -> 52 431 (d90) -> 71 432 (d120, 651 partii); 728 dob 122 054 | v2: naprawione / nowo obite (zuzyte w bitwach + lup obity) w d91-120 >= 1.2 x Z33 (Z33: 1 083 / (519 + 1 636) = 0.50); linia W CIENIU "stare zuzycie" | L:336, L:26804, L:35835, L728:183645 |
| naprawy AI / czeka na material | 1 083 szt./d / 2 792 szt. | naprawy >= Z33 | L:35835 |
| zuzycie w bitwie AI | stale `TroopWearPercent` 4 (Jeff 4.0), obija tylko sprawne | wedlug natezenia | K:AiWear.cs:255, :280-281; J:20 |
| **zakupy AI d120: cena za sztuke** | zalogi miast: korpus 31 szt. za 28 873 zl (931/szt.), helm 64 za 15 276 (239); lordowie: korpus 190 za 12 022 (63), helm 106 za 6 492 (61) | pomiar cena/wartosc, sufit po tescie | L:36029; RA8 6.2 |
| amunicja AI zuzyta w bitwach | 0 (AiWear pomija pociski) | linia "zeszloby N"; po wlaczeniu > 0 | K:AiWear.cs:275, K:ArmouryBehavior.cs:2301-2304 |
| odzysk amunicji w misji | jedna linia startu; wynik gracza dla obu stron | wedlug strony agenta | L:94; K:AmmoRecovery.cs:40-41 |
| lup DTE - konie / reszta sprzetu | konie przenoszone (E6/K1); reszta - kopia u ocalalego pokonanego, bez licznika | przeniesienie, kopie 0 | L:36063; DTE EveryoneCampaignBehavior.cs:684-708, :747-781 |
| monopole korony | 0 przez 120 dob; wzor 5% STANU zysku dziennie | przyczyna w linii; 5% dziennej wyplaty | L:492, L:35950; K:KingdomTreasury.cs:444-452 |
| premie wojenne kupcow | do +63% (Tyrosh), Myr +41%, Pentos +37% | brak (klucz domyslnie false) | L:35865; K:Settings.cs:482 |
| indeks surowcow (ruda / drewno) | mediana 0.64 / 0.75 | bez zmian (8.5a odrzucone) | L:35865 |
| ceny (dump): barda t6 / siodla ROT t2 / t0 mediana | 11 522 / 1 626 / 588 | ok. 2 531 / 237 / 145 | RB8 2.4 (ID) |
| helmy t4 / t5 / t6; luki t3 / t4 / t5 / t6 | 97 / 176 / 298; 38 / 63 / 101 / 159 | 39 / 80 / 127; 19 / 26 / 35 / 48 | RB8 2.5-2.6 (ID) |
| oszczepy / strzaly (wiazka / snop) | srednio 107 / 20 | oszczepy z `DaysThrown` 0.5-3 (bez x8) | RA8 3.3 (ID); K:HistoricalPrices.cs:292-303 |
| ksiazki BK | 24 szt. po 7-10 zl (BKROT /100); kupno: zloto w nicosc, ksiazka z niczego | 700-1 000 zl, zaplata do ksiegarza | RB8 1.1; BK BKEducationBehavior.cs:396-398 |
| zlecenia kowalskie gry | nagroda w starej monecie (Value gry x 1.3), z niczego; klasy zamkniete brama | cena historyczna, placi zleceniodawca; bron do zleceniodawcy, zolnierz z niczego 0 (v2) | G CraftingOrder.cs:107-122, CraftingCampaignBehavior.cs:1150-1154 |
| brama kucia | 7 klas zamknietych do przetopu (decyzja 01.09); opis MCM obiecuje inaczej | regula zostaje, opis prawdziwy | CS:1418; K:Settings.cs:1214 |
| nagrody zadan | ok. 50 wyplat w grze + 11 w SandBox `GiveGoldAction(null, gracz, ...)`; kryjowka 3 000 | v2: z kiesy zleceniodawcy w obu kierunkach, dni zoldu zamrozone przy przyjeciu (kryjowka ok. 438), towar z platnikiem i odbiorca | RB8 5.1 |
| jency sprzedani w miescie, nagrody turniejow AI | juz z kas miast (RealPayer E3/1, 164a-1): 120 dob turnieje AI 29 796 / zaplacone 29 644; okna "jency 13/0", "turniej 1/0" (z niczego 0) | bez zmian - poza E8 (v2) | L:35777, L:36056, L:36077; K:RealPayer.cs:20-33 |
| ksiega umiejetnosci | brak (0 linii); FairXp tylko trafienia | linia dobowa, zrodla XP | L:14; RA8 1.1 |
| karawany bez statkow 7+ dob w tym samym miescie / na wyspie | 29 / 2 (najdluzej 118 dob) | <= 10 / 0 bez drogi (v2) | L:35942 |
| klany najemnikow BK | blokada tylko dla klanu Spoils | 5 klanow ROT tez | L:35931; BK BKClanBehavior.cs:480-540 |
| CrossingLaw AI | 0 zawrocen - petla martwa (return przed `AiTick`); u Jeffa `CrossingLawAi` true | zawraca wrogich lordow | K:CrossingLaw.cs:73-76, :100; J:154 |
| ColdStart / nowe partie AI bez kompletu | 261 partii (227 zalog -> 34 lordow) / 541 w dobie 1 | ok. 280-300 partii lordow z dorobkiem (NK) | L:231, L:341 |
| bledy startowe | BK `CorrectPlayerEducation` (wyjatek pierwszej szansy; finalizer BKROT go polyka, zdarzenie biegnie dalej - v2); DTE pasek 1 raz; falszywa linia Mends o DTE | jezyk gracza sprawdzony; BK 0 po latce albo wyjatek odfiltrowany; linia prawdziwa | CS:1521, CS:1536, CS:1568, CS:240 |
| Inni (doba 120) | brakuje 25 osad za Murem do inwazji | test od doby 728 (procedura) | CS:4363 |
| namioty przy napadzie na oboz (E7/O2) | u Jeffa `CampBattlePropsEnabled` false | wpis w pliku przy wgraniu (E7) | J:302 |

---

## 2. Reguly (jedna na zjawisko)

| # | Regula | Dotyczy paczek |
|---|---|---|
| R1 | **Nic z niczego, nic w nicosc - zloto, towar i ludzie (v2).** Kazda wyplata ma platnika (zleceniodawca, kasa miasta, kiesa pojmujacego, przeciwnik), kazda wyplata gracza ma odbiorce (zleceniodawca, kasa osady zadania, zaloga, lud). Towar ma zrodlo i odbiorce (polka, magazyn, tabor) - nie `AddToCounts` z niczego ani w nicosc. **Osada placi tylko z nadwyzki `Seal31.Spare`** (D4: kasa ponad zapas kupcow minus tarcze - ta sama regula co RealPayer E3/1); `GiveGoldAction` tnie do CALEJ kasy osady (G GiveGoldAction.cs:21-25), wiec nadwyzke liczymy sami. Bohater i partia - `GiveGoldAction` tnie do kiesy. Brak = mniej, komunikat i licznik "ponizej zapasu przez nasza wyplate" = 0; bez doplat (SP:1242). Jeden wrapper `PayFromGiver` w obu kierunkach. Zdarzenia zamkniete juz przez RealPayer (jency, nagroda turnieju AI, lup oblezen, jency rabunkow BK, myto) NIE sa celem E8. | K, D1, D2, RC, G1, G2, B, L |
| R2 | **Praca wyceniona w dniach placy (v2).** Nagroda zadania = min(kwota gry, N ludzi x D dni x stawka), liczona w wrapperze wyplaty (nie w getterze) i ZAMROZONA przy przyjeciu zadania: obiecane = zaplacone, z dokladnoscia do kiesy platnika. N i D z rozwiazania zastepczego; dla zadan bez niego i dla stalych kwot - tabela typ zadania -> N x D w paczce (kazdy typ w linii "kwota gry / po skali"). Stawka = zold na czlowieka druzyny gracza w chwili przyjecia (fallback 5 zl). Towar dostaw osobno, po wartosci, od odbiorcy towaru. Oplaty gracza w dniach placy (straznik 2 zl, robotnik 3 zl, szyling 12). | D1, D2 |
| R3 | **Jeden predykat "pocisk"** = nowy `IsMissile` (Arrows, Bolts, Thrown) - w `NoWear`, odzysku i AiWear. `IsAmmo` (i `CleanseAmmo`) zostaje dla Arrows/Bolts - inaczej obite oszczepy gracza wracalyby do stanu fabrycznego za darmo (v2, K:ArmouryBehavior.cs:2314-2347). W cenniku mnoznik snopa (x8) tylko dla Arrows/Bolts. | C, W, A |
| R4 | **Jedna regula zuzycia.** Gracz i AI: natezenie bitwy = (polegli + ranni) / zdrowi na starcie / `TroopWearBaseCasualtyShare`, 0.1-3 (wzor gracza K:ArmouryBehavior.cs:1950). Obijane sa sztuki noszone (dopasowanie N1), obita moze zejsc o stopien (do Mangled). Arena i turniej nie obijaja. | W, A |
| R5 | **Naprawy nie staja.** Kolejka od najtanszej naprawy; sztuka, na ktora nie stac, czeka (`continue`). Zakup nie zjada rezerwy napraw. Robocizna napraw kazdej drogi (lawa gracza, kwatermistrz Spoils, kowale AI) idzie do kiesy ludu (regula N1 `MendPayToFolk`). | W, B |
| R6 | **Lup to przeniesienie, nie kopia.** To, co DTE dopisalo zwyciezcy, schodzi z pokonanego, ktory przezyl. Trzecia kapitana obejmuje kazda zdobycz (po decyzji, pytanie 1). Porzucony albo oddany dobytek trafia do zalogi albo na polke najblizszej przyjaznej warowni, bez zaplaty (regula HorseFate (2)). | L |
| R7 | **Jedna wycena.** `ArmsPricing.Compute` liczy Value, warsztat (`WorkshopLaw.Needs`), kwit kucia (`Recipes.BuildRecipe`) i material naprawy (`MendMaterial`) z tej samej wagi do kosztu. | C |
| R8 | **Stan sesji czyszczony w konstruktorze behavior.** Wyjatek w jednym obiekcie = licznik potkniec, nigdy globalny wylacznik (CLAUDE.md 7). | S, wszystkie nowe |
| R9 | **Optymalizacja = te same liczby.** W probce 1/64 liczona jest stara i nowa droga (wynik i czas); licznik rozjazdow w linii. Rozjazd > 0 w tescie = paczka wylaczona. | O1-O4, O6 (v2: O5 zmienia kolejnosc w dobie - prog wzgledny O5-2) |
| R10 | **Zmiana "po liczbach" pokazuje dwie drogi w jednej linii** (gra / po zmianie) w tym samym biegu. Przy ozywianym martwym kodzie linia liczy, ile pracy przybedzie (CLAUDE.md 8 pkt 4). Paczka bez pewnych liczb idzie W CIENIU. | P1-P3, X, W, Z, R, Q, G1 |
| R11 | **Cudzy kod:** kazda latka pisze linie "wpiete N/M"; gdy nie znajdzie celu, pisze "nie znaleziono" i nic nie psuje. IL sprawdzany w 1.4.8 przy paczce. | M, X, A, K, D1, D2, G1, G2, T |
| R12 | **XP za skutek:** trafienie, przywrocony stan, sprzedaz - nie za wypuszczony pocisk, zaplacona kwote ani dlugosc kolumny. Arena i turniej wedlug FairXp (L:14). | X |
| R13 | **MCM:** kazda zmiana zachowania dostaje NOWY klucz. Domyslnych zapisanych w J nie zmieniamy (CLAUDE.md 7). Wyjatki: `WarExpectationEnabled` (nie ma go w J - zadziala domyslna) i `CrossingLawAi` (Jeff ma true - ozywienie zgodne z jego ustawieniem; nowy klucz `CrossingLawAiClock` pozwala je zgasic). | wszystkie |
| R14 | **NK tylko na starcie** (ColdStart, StartKit, dorobek partii z doby 1-3). Kazda nowa sekcja zapisu z wersja; stary zapis bez sekcji = stan pusty, bez bledu. | T, wszystkie |

**Pogodzone bez pytania Jeffa / poprawki do PLAN i rozpoznan:**

| Miejsce | Bylo | Jest (dowod) |
|---|---|---|
| PLAN 8.7 "zalogi nie zuzywaja sprzetu" | blad | ZROBIONE w E6/K1: `AiWear` obejmuje `IsGarrison` (K:AiWear.cs:264), naprawy zalog (K:AiGear.cs:276-291). Zostaje dopisek "w tym zalogi" w linii (E8/P2) |
| PLAN 8.9 "wraki" | blad | ZROBIONE (paczka 158 `WrecksToScrap`); w etapie 8 tylko test 8.16 |
| PLAN 8.13 "kara snu z dodatkiem morskim" | blad | NAPRAWIONE 11.10 ("morale w 4 modelach", K:NightRest.cs:1279-1297); test 8.16 |
| PLAN 8.13 "krolestwo bez rodu rzadzacego" | blad | zabezpieczone (K:KingdomTreasury.cs:74-78, :387; K:CirculationWindows.cs:1220); bez kodu |
| PLAN 8.14 "sol i piwo jako potrzeby" w BEE | do skryptu | nie ma takiego klucza w pliku BEE; lista w kodzie (BEE TownEconomyCampaignBehavior.cs:124, sol to tam luksus :3735), przy BK bez skutku; u nas juz potrzeba wsi (K:TownFolk.Live.cs:42) - odrzucone, Jeff dostaje wyjasnienie (rozdz. 0 pkt 12) |
| RC8 pytanie 3 (okup gracza pojmanego bez bohatera) | pytanie | rozstrzygniete decyzja 9 (12.10): zloto przy pojmaniu idzie juz do kiesy lorda / kasy osady / kiesy bandy (KR:CaptivityBehavior.cs:568-592). Zostaja dwie drogi z `GetCaptorHero()` == null: splata dlugu okupu (:443) i wykup rynsztunku (:829) - ta sama regula w E8/RC |
| CD rada 4 "sakiewki turniejow 3-15 tys." | dochod gracza | to pula, ktora gracz SAM wystawia jako gospodarz (KG HostMenu.cs:54-73) - nie dochod; zostaje bez zmian (RB8 0) |
| RB8 B4 pytanie 2 "nagroda w dniach zoldu" | pytanie | decyzja Jeffa 12.10 11:40 (SP:1280-1281) - bez pytania |
| RC8 C7 "drobne zrodla z niczego" jako jedna paczka | jedna paczka | rozdzielone wedlug obszaru (poz. 28 -> E8/Z, 30 -> E8/B, 1 -> E8/G2, 4 -> E8/D2, 20 i 22 -> E8/B), zeby nie robic dwa razy z 8.8/8.9/8.18 |
| RD8 numery O1-O7 | O1-O7 | E8/O1 = O1 + O6 (polka i delegaty), E8/O2 = O2, E8/O3 = O3, E8/O4 = O4, E8/O5 = O7, E8/O6 = O5 bez utraty linii (plik otwarty, flush po linii); "lzejsze logi" - pytanie 10 |
| CH:20 (E5/BITWY "poza zakresem") | etap 8 | `sprawdz_logi` grupa etap2 liczy "bitwy" brutto jako zrodlo z niczego - poprawka narzedzia w E8/0 (kolumna netto); w grze bez zmian |
| PLAN 1.12 | etap 1 | nikt go nie robi (SP bez wpisu po 08.10) - dolaczone do E8/T (to samo zjawisko co 8.10c, ta sama regula) |

---

## 3. PACZKI etapu 8 w kolejnosci

Numeracja robocza 242-270 (etap 7 uzyl 224-241). Przed kodem grep CHANGELOG, STAN-PRAC i docs - jesli R1/R2/W4 etapu 7 albo etap 5 dostana nowe numery, przesunac.
Kazda paczka: osobny commit, wpis CHANGELOG z "Ryzyko / co sprawdzic", wlasny wylacznik, autor + recenzent. `McmSettings.cs` generuje `python tools/gen_mcm.py` po kazdym scaleniu.
Galaz `w-toku/e8` od glowy `w-toku/zbior-5-8` po zamknieciu workflow wf_42f8dda9-fa3 (E7/R1, R2, W4 i przeglady). Pliki R1/R2/W4 (`Devastation`, `VillageBook`, `MapVillagesView`)
nie sa celem zadnej paczki E8. **E8/0 scalamy PIERWSZA** (przepisuje ok. 108 wywolan `.Daily()` w `OnDailyTick`); dopiero potem ruszaja galezie (v2). Dwa watki rownolegle wedlug DLL:
- watek 1: wszystko w Armoury/src, po kolei (ekonomia, ceny, sprzet, G1, G2, Q, S, optymalizacja) - wspolne `Settings.cs`, `ArmouryBehavior.cs`, `tools/sprawdz_logi.py`;
- watek 2: tylko CrashScribe (E8/M bez czesci w `ArmouryBehavior`), RealisticCaptivity (E8/RC) i ForgeView; linie `MapVillageLore` z E8/M dopisuje watek 1.
Blok `sprawdz_logi` kazdej paczki pisze watek 1; watek 2 podaje tylko wzorce linii.

Kolumna "Kiedy":
- **TERAZ** - kod teraz, klucz wlaczony;
- **CIEN** - kod teraz, klucz wylaczony, linia liczy skutek; wlaczenie w fazie poprawek po tescie zbiorczym;
- **PO TESCIE** - kod dopiero po liczbach z testu;
- **PO TAK** - klucz wlaczony tylko po "tak" Jeffa.

| Paczka | Nr | PLAN | Co | Wiel. | NK | Zalezy od | Kiedy |
|---|---|---|---|---|---|---|---|
| **E8/0** | 242 | 8.17 | profil doby w logu + grupa `etap8` w narzedziu | S | nie | - | TERAZ, pierwsza |
| E8/P1 | 243 | 8.1a | ksiega umiejetnosci (sam log, goracy punkt) | S | nie | E8/0 | TERAZ |
| E8/P2 | 244 | 8.4a, 8.6b, 8.7a-b, 8.8a-c | pomiary sprzetu AI, lupu i korony (sam log) | S | nie | E8/0 | TERAZ |
| E8/P3 | 245 | 8.15, 8.12c, przed G1/G2 | pomiary swiata (NavalDLC, Underequipped, owce, postoj karawan, najemnicy BK, zrodla BK) | S | nie | E8/0 | TERAZ |
| E8/S | 246 | 8.11 | porzadek stanu: Resety, obozy, klimat bez globalnego wylacznika | S | nie | - | TERAZ |
| E8/M | 247 | 8.13 | cudze mody drobne: BK Education (tylko po sprawdzeniu jezyka gracza - v2), ladry = Jazda w CrashScribe, linie PODPIETY/BRAK | S | czesc | - | TERAZ |
| E8/X | 248 | 8.1b-d, 8.2c | doswiadczenie: strzal tylko w bitwie, zarzadca, zwiad, paser, samonaprawa | S | nie | P1 | TERAZ |
| E8/W | 249 | 8.8c-d, 8.4a, lawa 15 | zuzycie i naprawy AI | S-M | nie | P2 | TERAZ (amunicja AI: CIEN) |
| E8/A | 250 | 8.4b, 8.8e | misja gracza: odzysk wedlug strony, arena nie obija | S | nie | - | TERAZ |
| E8/Z | 251 | 8.8a-b, poz. 28 | zakupy i nadwyzki AI: bez dodatnich stanow, sufit ceny, uprzaz poleglych | S | nie | P2 | TERAZ (sufit: CIEN) |
| E8/L | 252 | 8.6a-c, 8.2a-b | lup i dary Spoils: wraki wedlug zabojcy, przeniesienie, Send troops, dar do zalogi | S-M | nie | P2, pytanie 1 | TERAZ (trzecia zbrojowni: PO TAK) |
| E8/C | 253 | 8.5d-g, 8.4c | cennik: rzad konski, helmy, luki, oszczepy, (konie) | S-M | nie | - | TERAZ (konie: PO TAK) |
| E8/R | 254 | 8.5b-c, 8.7b | rynek i korona: bez premii wojennej, kon przy sprzedazy, monopole | S | nie | P2 | TERAZ |
| E8/K | 255 | 8.3 | kuznia: ksiazki BK, zlecenia kowalskie, opis bramy | M | nie | - | TERAZ |
| E8/D1 | 256 | 8.18 a | nagrody i wyplaty gracza w zadaniach (oba kierunki), dni zoldu zamrozone przy przyjeciu, towar zadan z platnikiem (v2) | M | nie | K (wspolny wrapper wyplat) | TERAZ |
| E8/D2 | 257 | 8.18 b, poz. 4 | nagroda turnieju gracza, zaklady, oplaty i drobne zrodla gracza (jency i turnieje AI - juz RealPayer, v2) | S-M | nie | D1 | TERAZ |
| E8/B | 258 | 8.9, lawa, poz. 20, 22, 30 | warsztaty i kowadlo: wyroby uboczne, pekniecie zbroi, material napraw, kopalnia gracza | S-M | nie | - | TERAZ |
| E8/RC | 259 | lawa 7/16, 8, 10; RC8 poz. 32 | RealisticCaptivity: praca a kasa miasta, dlug okupu do pojmujacego | S | nie | - | TERAZ |
| E8/G1 | 260 | 8.12a-b | karawany: BK co godzine w miescie (postfiks na prefiksie BKROT), wyspy bez drogi (v2) | S-M | nie | P3, pytanie 3 | PO TAK (wyspy: TERAZ) |
| E8/G2 | 261 | 8.12e, poz. 1 | najemnicy BK bez ludzi z niczego; uczta BK bez towaru z niczego | S | nie | P3, pytanie 4 | PO TAK (uczta: TERAZ) |
| E8/Q | 262 | ERRORS/E7, poz. 9 | przeprawa: CrossingLaw AI na zegarze gry | S | nie | S, E7/O1, E7/B | TERAZ |
| E8/T | 263 | 8.10a-c, 1.12 | start kampanii (NK) i rekruci gracza jak u AI (sciezka RecruitKit, v2) | S-M | **tak** (8.10) | - | TERAZ |
| E8/E | 264 | 8.14 | BEE: klucz 14 w skrypcie (bez DLL) | S | nie | - | TERAZ, uruchomienie przy wgraniu |
| E8/O1 | 265 | 8.17 | polka po wlasnym zdjeciu sztuki (kopia przy zapisie); delegaty DTE | S | nie | E8/0, Z (v2) | TERAZ |
| E8/O2 | 266 | 8.17 | WorkshopLaw bez refleksji i powtorzen | S | nie | E8/0, B (v2) | TERAZ |
| E8/O3 | 267 | 8.17 | wywoz kupcow: cele w zasiegu z pamieci z kluczem | S | nie | E8/0, R, O1 (v2) | TERAZ |
| E8/O4 | 268 | 8.17 | musztra: jeden odczyt pozycji lordow na godzine | S-M | nie | podzial E8/0 | PO TESCIE |
| E8/O5 | 269 | 8.17 | skok doby: GarrisonArmory po godzinach | S | nie | E8/0 | TERAZ |
| E8/O6 | 270 | 8.17 | zapis logu: jeden slownik pisarzy dla Info/Error/Csv, flush po linii (v2) | S | nie | E8/0 | TERAZ |
| 8.16 | - | 8.16 | Twoje reczne sprawdzenia (lista na koncu rozdzialu) | - | - | kod etapu | po tescie zbiorczym |

**Pulapka MCM** (CLAUDE.md 7) - w J SA (12.10): `TroopWearPercent` 4.0 (:20), `PlayerLootSharePercent` 33 (:128), `MinSellPercentOfValue` 2 (:127), `ForgePartsFreeBelowTier` 2 (:149),
`TroopSelfMendEnabled` true (:152), `CrossingLawAi` true (:154), `CrossingLawEnabled` true (:155), `LogEnabled` true (:201), `SmithingSkillPerTier` 35 (:205),
`CampBattlePropsEnabled` false (:302 - sprawa E7/O2). Zadnej z tych wartosci nie zmieniamy. `WarExpectationEnabled` nie ma w J, wiec nowa domyslna false zadziala.

---

### E8/0 - Profil doby w logu (242, PLAN 8.17 pomiar) - S - TERAZ, PIERWSZA

**Cel.** Dzis nie widac, co zjada dobe. `Koszt 171-174` (L:35846) obejmuje ok. 1.2 s z 2.5-3.1 s naszego kosztu (RD8 1). Brakuje: taktu doby, sluchaczy godzinowych, wejsc do miast
i podzialu CyclePrefix i musztry. E8/0 daje podstawe dla O1-O6 i rozroznia prawdziwy koszt od pauzy sprzatania pamieci (probka 1/16 mnozy pauze gen2 x16 - RD8 1). Gra bez zmian.

**Pliki / okna**
| Plik | Zmiana | Dowod |
|---|---|---|
| NOWY `DayProfile.cs` | stopery i tablice bez alokacji; linia raz na dobe; `Reset()` | wzor `Cost174.cs` |
| `ArmouryBehavior.cs` `OnDailyTick` | opakowanie ok. 108 wywolan `X.Daily()` (czas kazdego, top 15 w linii) i calosci taktu; to samo dla ok. 16 sluchaczy `HourlyTickEvent` | K:ArmouryBehavior.cs (108 x `.Daily()`); RD8 2.3 |
| `MenPurse.OnEntered`, sluchacze `SettlementEntered` / `MobilePartyDestroyed` / `MapEventEnded` | probka 1/16 czasu, pelna liczba zdarzen; rozbicie MenPurse: SellArmorySurplus / AiWear.MendInTown / AiGear.TryBuy | RD8 1, 5 pkt 3 |
| `WorkshopLaw.CyclePrefix`, `Drill` | podzial: pierwszy cykl doby warsztatu (BuildPlan/Candidates) i kolejne; musztra: godziny ruchu / tik treningu / zuzycie / kowale / linie | RD8 K2, K5 |
| `SupplyDemand.PricePostfix` | `[ThreadStatic]` wolajacy (AiGear, wywoz, VolunteerKit, warsztaty, reszta = gra i cudze) | RD8 K3 |
| `ShelfIndex` | przebudowy wedlug zrodla zmiany (wlasne Take / reszta) | RD8 K1 |
| `Cost174.cs` | najdluzsza pojedyncza probka w kazdej pozycji; `GC.CollectionCount(0/1/2)` przyrost, `GC.GetTotalMemory(false)` | RD8 1 |
| `Log.cs` | laczny czas zapisu linii | RD8 K8 |
| KC `Blame.cs` / `Scribe.cs` | liczba FirstChance na dobe (zgloszone / odfiltrowane), czas `Blame.Culprits` | RD8 K9 |
| `tools/sprawdz_logi.py` | nowa grupa `--grupa etap8` (szkielet, T0 bledy z CS); grupa etap2: "bitwy" jako netto (para ZRODLA/UJSCIA), nie "zrodla z niczego" | CH:20 |

**Klucz:** `Stage8ProfileLog` (true; hint: "Writes one daily line with the time each part of the mod takes (diagnostics only).").
**Log:** `Profil doby (E8/0): dzien N | takt doby X ms (top: ...) | godzina: suma/maks. ... | zdarzenia ... | CyclePrefix pierwszy/kolejne | musztra ... | ceny wedlug wolajacego ... | polka przebudowy wlasne/reszta | log zapis X ms | GC 0/1/2 +a/+b/+c, pamiec M MB | najdluzsza probka ...`.
**Test (blok etap8_0):** E80-1 linia w kazdej dobie okna; E80-2 suma pozycji "naszych" >= 80% sumy z `Koszt 171-174` + pozycji poza suma (pokrycie); E80-3 koszt samego profilu <= 1 ms/dobe (stoper wlasny);
E80-4 INFO: takt doby mediana i maks., udzial pauz GC w szczytach. Do tego autotest `-Profile` na scalonym DLL (narzedzie z 08.10, SP "08.10 POPOLUDNIE"), jesli dziala na 1.4.8 - podzial na mody.
**Ryzyka:** nie liczyc dwa razy tego samego, co liczy `Cost174` (suma "nasze" musi byc rozlaczna); stoper tylko w takcie doby (raz) i probkowany w zdarzeniach. **Koszt:** < 1 ms/d. **NK:** nie.

### E8/P1 - Ksiega umiejetnosci (243, PLAN 8.1a) - S - TERAZ

**Cel.** Ilosc i zrodla XP bohaterow i wojska - stan "przed" i "po" dla E8/X (R10: obie drogi w tym samym biegu).
**Okna:** prefiks-licznik na `HeroDeveloper.AddSkillXp` (umiejetnosc x zrodlo x grupa: gracz / towarzysze / lordowie AI) i `TroopRoster.AddXpToTroop`. Zrodlo z `[ThreadStatic]` znacznika
ustawianego w krotkich prefiksach znanych wolajacych (strzal RBM, `OnFoodConsumed`, `OnTraverseTerrain`, musztra, kuznia, Spoils). Tablica `[umiejetnosc, zrodlo]`, bez napisow w prefiksie
(RA8 1.1; brak dzis latki - grep). Raz w tygodniu mediana i p90 poziomow lordow.
**Klucz:** `SkillLedgerLog` (true). **Log:** `Umiejetnosci (E8/P1): dzien N | gracz ... | lordowie ... | zrodla ...`; `XP wojska (E8/P1): ...`.
**Test (etap8_p1):** P1-1 linia w kazdej dobie; P1-2 koszt <= 30 ms/dobe (stoper probkowany 1/64); P1-3 zrodlo "inne" <= 20% XP lordow (pokrycie znacznikow).
**Ryzyka:** goracy punkt (kazdy cios, strzal, godzina marszu kazdej partii) - zero alokacji; po E8/X ksiega zostaje (obie drogi). **Koszt:** cel < 30 ms/d. **NK:** nie.

### E8/P2 - Pomiary sprzetu AI, lupu i korony (244, PLAN 8.4a, 8.6b, 8.7a, 8.7b, 8.8a-c) - S - TERAZ

**Cel.** Liczby, bez ktorych E8/W, E8/Z, E8/L i E8/R bylyby zgadywaniem (RA8 8 A1). Gra bez zmian.
| Plik | Co liczy | Dowod |
|---|---|---|
| `AiWear.cs` | "w tym zalogi: zuzyte N, naprawione M" (8.7a); powody stania napraw: sakiewka / material / godziny / cap; "Amunicja AI: zeszloby N kolczanow" (natezenie x strzelcy x 0.35 zwyciezca / 1.0 przegrany) | K:AiWear.cs:264, :275, :503-544; L:35835 |
| `AiGear.cs`, `ShelfBuy` | mediana i p90 stosunku cena / wartosc sztuki ze stanem wedlug kupujacego; kupione dodatnie stany (Fine/Masterwork) - szt. i doplata; "sufit 2.0 zablokowalby N szt. / M zl" | K:AiGear.cs:146, :608, :610, :800; L:36029 |
| `HorseFate.cs` (okno DTE `OnMapEventEnded`) | kopie u ocalalych pokonanych - wszystkie rodzaje (dzis zdejmowane tylko konie) | K:HorseFate.cs:34-38; L:36063 |
| `KingdomTreasury.cs` | krolow z StateMonopolies, warsztatow z ProfitMade > 0, suma ProfitMade, "nowy wzor (5% dziennej wyplaty) wzialby X" | K:KingdomTreasury.cs:444-452; L:35950 |
**Klucz:** `Stage8MeasureLog` (true). **Test (etap8_p2):** P2-1 linie w kazdej dobie; P2-2 zgodnosc: zalogi + partie = razem (linia "Zuzycie AI"); P2-3 INFO: kopie u ocalalych (szt./d), monopole - przyczyna 0.
**Koszt:** ok. 5-10 ms/d [S]. **NK:** nie.

### E8/P3 - Pomiary swiata (245, PLAN 8.15, 8.12c; "przed" dla G1/G2) - S - TERAZ

| Pomiar | Co dopisac | Dowod / gdzie |
|---|---|---|
| produkcja wsi pod NavalDLC | nazwa `BaseModel` czynnego `NavalDLCVillageProductionCalculatorModel` (refleksja) i iloraz wynik/baza dla 10 typow wsi, raz w dobie 1 | RC8 5 (a02 N1) |
| kara "Underequipped" (DTE `MarkUnderEquipped`) | po bitwie gracza: ilu ludzi z kara 0 / 2-10 / 12-20 u gracza i u przeciwnika | RC8 5 (P174:156, :641) |
| dochod owiec | stada wsi owczych (24 wsie), welna/dobe, utarg welny i miesa | RC8 5 |
| postoj karawan w miescie | sredni postoj (h), wyjazdy/dobe, karawany 7+ dob (jest w L:35942) | RC8 3.1 |
| najemnicy BK | prefiks/postfiks liczacy na `ConvertTroopsMercenaries`: bohaterowie zamienieni, ludzie dosypani (5 klanow ROT) | BK BKClanBehavior.cs:480-540 |
| zrodla BK bez platnika | `GiveFood` bandom hersztow (ilosc/d); ujscia `Demand.FinancialCompromise`, `BKCourtierBehavior.DrainGold`, `BuyGoodsAction` (kwoty/d) - dopisek do linii "BK i Diplomacy (E5/A8)" | CH:50 |
| wies Normal bez wozu | zatkane Normal bez wozu > 7 dob (z linii diagnozy) | RC8 3.3; G VillagerCampaignBehavior.cs:122-157 |
| rozkazy morskie po switcie | "rozkaz po switcie" vs przed snem dla partii ze statkami (`RememberOrder`) | CH:452 |
**Klucz:** `Stage8WorldLog` (true). **Test (etap8_p3):** P3-1 linie obecne; P3-2 "Normal bez wozu > 7 dob" = 0 (zamyka 8.12c); P3-3 INFO pozostale. **Koszt:** < 10 ms/d [S]. **NK:** nie.

### E8/S - Porzadek stanu (246, PLAN 8.11; RC8 poz. 11, 15, 17) - S - TERAZ

| Plik | Zmiana | Dowod |
|---|---|---|
| `HideoutPurge.cs` | `Reset()` w konstruktorze `ArmouryBehavior` (`_pending*`, `_reprisalPack.Clear()` bez `SetInitiative`, `_searchLeave`); catch w `OnTick` liczy potkniecia (najwyzej 3 proby), nie kasuje `_pending` | K:HideoutPurge.cs:34-42, :170, :360 |
| `NightRest.cs` `ResetWorld` | + `_tentStrikes`, `_tentChildren`, `_promptDay`, `PlayerCamped` | K:NightRest.cs:55, :254-273, :942-952 |
| `TerrainEase.cs` | `_lastAuditDay` w `Reset` | K:TerrainEase.cs:63, :177, :253 |
| `Stables.cs` | `Reset()`: `_lastBuy.Clear()` (dzis roznica dni ujemna = blokada zakupow) | K:Stables.cs:788, :818-819, :1006 |
| `CrossingLaw.cs` | `Reset()`: `_lastSafe`, `_haveSafe`, `_aiCooldown` | K:CrossingLaw.cs:36-37, :75-82 |
| `WesterosClimate.cs` | `_initBroken` -> licznik potkniec, ponowna proba `Ready()` w kolejnej dobie (najwyzej 3; potem ERROR raz na dobe); bez drugiego segmentu lata (`_segs.Count > 0`) | K:WesterosClimate.cs:39, :72, :87 |
| `NightMarch.cs` | `EnterDebt` w osadzie usuwa martwy wpis `_orders` przy `Order == null` | K:NightMarch.cs:1496 |
**Klucz:** `StateHygieneEnabled` (true). **Log:** start sesji `Porzadek stanu (E8/S): wyzerowane N klas; klimat potkniecia K`.
**Test (etap8_s):** bieg "nowa kampania -> zapis -> wczytanie innego zapisu w tej samej sesji" (procedura "zapis 9 dob"): S-1 stajnie AI kupuja w pierwszej dobie po wczytaniu; S-2 linia startu;
S-3 klimat: potkniecia 0 albo ponowione i czynne. **Ryzyka:** Reset przed SyncData (jak reszta); nie zdejmowac sluchaczy menu (`AddMenus`). **Koszt:** 0. **NK:** nie.

### E8/M - Cudze mody drobne (247, PLAN 8.13a, d, e; RC8 poz. 5) - S - TERAZ (latka BK Education - po sprawdzeniu jezyka gracza, v2)

| Plik | Zmiana | Dowod |
|---|---|---|
| KC `Mends.cs` | **v2:** wyjatek BK juz dzis polyka finalizer BKROT (`EducationManagerCorrectPlayerEducationPatch`, Finalizer zwraca null); zdarzenie `OnCharacterCreationIsOver` NIE jest przerwane (okno BK "Welcome" CS:1536 po wyjatku; StartKit L:310). Przerywa sie tylko sama korekta edukacji. **Najpierw sprawdzenie:** zapis nowej kampanii - czy gracz ma jezyk BK. Jesli nie ma: prefiks `Priority.First` robi korekte sam (pomija jezyki bez kultur; jezyk kultury gracza albo Calradian, jak BK) i zwraca false - prefiks BKROT zostaje pominiety, finalizer BKROT zostaje. Jesli ma - bez latki, tylko filtr CS dla tego znanego wyjatku | BK EducationManager.cs:119; BKCampaignStartBehavior.cs:229, :266; BR EducationManagerCorrectPlayerEducationPatch.cs:44-97; CS:1521, :1536 |
| KC `Mends.cs` `ReqSkill` | `HorseHarness -> Riding` przed `HasArmorComponent` (jak Armoury) | KC:Mends.cs:1044-1052; K:ItemReq.cs:78 |
| KC `Mends.cs`, KF `ArmourFilterMixin.cs` | przy pierwszym odczycie ustawien innego moda jedna linia "<link>: PODPIETY / BRAK (<pole>)"; fallback bez zmian | KC:Mends.cs:886-894, :1693-1697, :1806; KF:ArmourFilterMixin.cs:189-234, :256-296 |
| KC `Mends.cs` | linia o pasku DTE: sprawdzenie po rejestracji DTE (dzis falszywe "nieobecny") | CS:240, CS:1568 |
| K `ArmouryBehavior.OnSessionLaunched` | `MapVillageLore.SessionStart()` po `McmSettings.Apply()` (tylko linia startu) | CH:126 |
| `docs/ERRORS.md` | A5 + `DynamicPartySizeBattleSkipPatch`; TournamentsXPanded loader x2; MonoMod first-chance przy klimacie; BK Education | RC8 9 |
**Klucz:** `MendBkEducation` (true; kod tylko, jesli sprawdzenie pokaze brak jezyka); reszta to log albo 1 linia. **Test (etap8_m):** M-1 CS w nowej kampanii: 0 wyjatkow `CorrectPlayerEducation` (po latce) albo wyjatek odfiltrowany jako znany (bez latki); M-2 linia "Mends: edukacja BK gracza - pominiete jezyki bez kultur N, jezyk gracza X";
M-3 linie PODPIETY dla 5 odczytow; M-4 licznik przydzialu ladrow w warcie DTE wedlug Jazdy.
**Ryzyko (v2):** ozywianego kodu NIE ma - sluchacze zdarzenia biegna juz dzis (finalizer BKROT), wiec flaga "juz wydane" dla StartKit jest zbedna (skreslona). Prefiks First musi zrobic cala korekte BK
(inaczej gracz zostaje bez korekty) - kod korekty z dekompilacji BK, IL sprawdzony przy paczce.
**NK:** czesc (edukacja tylko przy tworzeniu postaci). **Koszt:** 0.

### E8/X - Doswiadczenie (248, PLAN 8.1b-d, 8.2c) - S - TERAZ

| Okno | Zmiana | Dowod |
|---|---|---|
| RBM `CampaignChanges+OverrideOnAgentShootMissile.Postfix` (klasa zagniezdzona, `AccessTools.TypeByName`; RangedRework nietkniety) | prefiks `return false`; nasz postfiks w FairXp: tylko `Mission.CombatType == Combat` i `MapEvent.PlayerMapEvent != null`; luk 10, kusza 20, miotana 17, proca 10; arena/turniej 0 | RBM:3628-3677; K:FairXp.cs:27-100 |
| `DefaultSkillLevelingManager.OnFoodConsumed` (NavalDLC przekazuje) | 50 x sqrt(ludzie) x (rodzaje - 2) / 3, rodzaj od pol dnia zapasu, zima x1.25 (z pory roku; WinterBite tylko czytany) | G DefaultSkillLevelingManager.cs:457-464; K:WinterBite.cs:204 |
| `OnTraverseTerrain`, `OnAIPartiesTravel` | 15/h rownina, 25/h trudny teren, karawany polowa, bez progu, premia tylko-AI 0 | G :418-440 |
| K `SpoilsSeal.cs` | paser: 25 Roguery za udana sprzedaz (zamiast 5% zaplaty) | K:SpoilsSeal.cs:531 |
| K `SmithMenu.cs` | samonaprawa: XP x udzial przywroconego stanu (min. 25%) | K:SmithMenu.cs:1096, :1132 |
| RBL (szpieg, sabotaz, glos za zloto), GT gospodarz | najpierw dekompilacja; zmiana tylko po potwierdzeniu, osobny klucz | RA8 2.3 (a13 - niesprawdzone) |
**Klucze:** `ShotXpFair`, `StewardFoodXpFair`, `ScoutXpFair`, `SmallXpFair` (true). **Log:** start "FairXp: strzal RBM wpiety / nie znaleziono"; P1 pokazuje obie drogi.
**Test (etap8_x):** X-1 XP ze strzalu na arenie i turnieju = 0 (P1, gra Jeffa); X-2 Steward lordow z jedzenia: po/gra 0.4-0.6 dla partii 300+ (RA8 1.3: ok. -47%); X-3 Scouting lordow 300+: po/gra 0.4-0.6;
X-4 wszystkie latki "wpiete". **Ryzyka:** wolniejszy rozwoj lordow AI - to wynik uczciwej reguly, nie kara; nazwa klasy RBM. **Koszt:** < 5 ms/d. **NK:** nie.

### E8/W - Zuzycie i naprawy AI (249, PLAN 8.8c, 8.8d, 8.4a; LAWA 15) - S-M - TERAZ (amunicja: CIEN)

| Okno | Zmiana | Dowod |
|---|---|---|
| `AiWear.OnMapEventEnded` | share x natezenie tej partii (R4); obijane noszone (`UsedByMen` z N1); obita schodzi o stopien | K:AiWear.cs:255, :280-281; K:ArmouryBehavior.cs:1950 |
| `AiWear` naprawy | kolejnosc od najtanszej; `continue` zamiast `break` (:511), sciezka z materialem (:544) tak samo | K:AiWear.cs:503, :511, :544 |
| `AiGear` zakup | z sakiewki najwyzej "sakiewka - zalegle naprawy", reszta z kiesy lorda | K:AiGear.cs:789-791 |
| `AiWear` amunicja | zdjecie kolczanow/snopow po bitwie (wzor P2); AiGear dokupi (BuyOrder ma Arrows/Bolts/Thrown) | K:AiWear.cs:275 |
| `AiWear` rezerwa | rezerwa AI na naprawy przy poziomie plac miasta (dzis poziom 1) | LAWA 15 |
**Klucze:** `AiWearByIntensity`, `AiMendQueueContinue` (true), `AiAmmoUse` (**false - CIEN**).
**Test (etap8_w, v2 - progi wzgledne):** W-1 kolejka napraw: naprawione / (zuzyte w bitwach + lup obity) w d91-120 >= 1.2 x Z33 (Z33 d120: 1 083 / (519 + 1 636) = 0.50, L:35835); W-2 linia W CIENIU "stare zuzycie 4% zuzyloby N" w tym samym biegu - stosunek nowe/stare INFO, a naprawy na sztuke zuzyta w bitwie >= Z33 (2.09) - rozdziela skutek kolejki od mniejszej liczby obic;
W-3 INFO: zuzyte w bitwach a ranni (korelacja > 0); W-4 przy wlaczonej amunicji (faza poprawek): kolczany zuzyte > 0, polki strzal d120 >= 50% Z33 (2 853, L:35865).
**Ryzyka:** kiesy lordow placa wiecej (zgodnie z SP:1242; granica: wspolny prog rodow na minusie, rozdz. 4 pkt 5); jedna definicja "noszonego" z E6/N1 `AiMendsWornOnly` (K:Settings.cs:1196); popyt na strzaly rosnie - dlatego CIEN. **Koszt:** < 5 ms/d. **NK:** nie.

### E8/A - Misja gracza: odzysk wedlug strony, arena nie obija (250, PLAN 8.4b, 8.8e) - S - TERAZ

| Okno | Zmiana | Dowod |
|---|---|---|
| DTE `Global.ProcessAgentEquipment` | prefiks: `[ThreadStatic]` biezacy agent (finalizer czysci) | DTE Global.cs:262-279; DynamicTroopMissionLogic.cs:230, :290, :587 |
| K `AmmoRecovery` | strona agenta (`agent.Team.Side`) wobec strony zwycieskiej; w trakcie misji (wynik nieznany) - jak dzis | K:AmmoRecovery.cs:40-41, :51-61 |
| K `FieldCraft.OnAgentHit` / `OnAgentShootMissile` | pomijac ksiege bohatera i `TroopWearLedger`, gdy `CombatType != Combat` (smierc konia juz sprawdza, :194) | K:FieldCraft.cs:333-380 |
| K `ArmouryBehavior.OnMapEventStarted` | zerowac `WearLedger` gracza; kleska bez zmian (A05 W6) | K:ArmouryBehavior.cs:1799-1809, :1827-1834, :1878 |
**Klucze:** `AmmoRecoveryBySide`, `ArenaNoWear` (true). **Log:** "AmmoRecovery: po bitwie - strona gracza odzysk X, przeciwnik Y".
**Test:** autotest nie gra misji - A-1 linie "wpiete"; reszta w 8.16 (punkty 8-9). **Ryzyko:** odczyt `MissionResult` z TaleWorlds.MountAndBlade sprawdzic przy paczce (brak w `cs_full`). **NK:** nie.

### E8/Z - Zakupy i nadwyzki AI (251, PLAN 8.8a-b; RC8 poz. 28) - S - TERAZ (sufit: CIEN)

| Okno | Zmiana | Dowod |
|---|---|---|
| `AiGear` wybor i `BuyLoop` | pomijac kandydatow z `PriceMultiplier > 1` (dodatnie stany zostaja dla gracza i bohaterow); pomijac `IsCivilian` | K:AiGear.cs:146, :608, :800; K:AiWear.cs:150-156 |
| `AiGear`, `ShelfBuy.Take` | sufit `AiGearMaxPriceFactor` x wartosc sztuki ze stanem; brak idzie jako zamowienie | K:AiGear.cs:610 |
| `MenPurse` | uprzaz poleglych jezdzcow ponad potrzebe - na polke miasta za zaplata (jak konie K2) | CH:658 |
**Klucze:** `AiSkipFineGoods` (true), `AiGearMaxPriceFactor` (0 = wylaczony; **CIEN** - wartosc po P2, np. 2.0), `AiHarnessSurplusSell` (true).
**Test (etap8_z):** Z-1 kupione dodatnie stany = 0; Z-2 po wlaczeniu sufitu: p90 cena/wartosc <= sufit, zalogi nie zostaja bez korpusu (pokrycie zbrojowni zalog >= Z33); Z-3 uprzaz w zbrojowniach AI d120 <= d60 x 1.2.
**Ryzyko:** zalogi kupuja wyzsze tiery - sufit liczony do wartosci tej samej sztuki, nie do sredniej. **Koszt:** 0. **NK:** nie.

### E8/L - Lup i dary Spoils (252, PLAN 8.6a-c, 8.2a-b) - S-M - TERAZ (trzecia zbrojowni pokonanych: PO TAK)

| Okno | Zmiana | Dowod |
|---|---|---|
| K `BattlefieldLaw.QueueWreck` | wraki wedlug zabojcy: bohater 100%, zolnierz `PlayerLootSharePercent`, reszta do `ArmyArmory` (Mangled); wlasny polegly - do `ArmyArmory` | K:BattlefieldLaw.cs:633-646 |
| K `BattlefieldLaw.AfterCollectLoot` | zbrojownia i tabor pokonanych (gainedLoots) - ta sama trzecia (**pytanie 1**), unikaty dalej UniqueSpoils | K:BattlefieldLaw.cs:522-542 |
| DTE `EveryoneCampaignBehavior.OnMapEventEnded` (okno HorseFate) | to, co DTE dopisalo zwyciezcom, schodzi z ocalalych pokonanych - wszystkie rodzaje | DTE :684-708, :747-781; K:HorseFate.cs:34-38 |
| DTE `DistributePlayerSimulationLoot`, K `AfterGenerateLoot`, `CutCasualtyLootPrefix` | "Send troops": jedna droga - lup DTE, trzecia na ekran, reszta do `ArmyArmory`; loteria Spoils wycieta jak w misji | DTE :471-598; K:BattlefieldLaw.cs:283, :583 |
| SW `DonateEquipmentBehavior.OnDonateScreenClosed` | prefiks: kopia `_donateScreenRoster`; postfiks: sztuki do zbrojowni zalogi (`AiGear.AddToArmory` + `AiWear.NoteBought`); brak zalogi - na polke miasta | SW donate.cs:142-237 (:171) |
| SW `LootCollectionBehavior.TryCreateBattlefieldRemnant` | gdy pozostalosc nie powstala (mala bitwa) - resztki na polke najblizszej przyjaznej warowni; wygasanie - najpierw sprawdzic, czy Spoils ma takie zdarzenie | SW lootcol.cs:1957, :3372-3390 |
**Klucze:** `WrecksByKiller`, `LootIsTransfer`, `SendTroopsOneLoot`, `SpoilsGoodsHome`, `SpoilsRemnantsHome` (true), `LootShareAllSpoils` (po decyzji 1).
**Test (etap8_l):** L-1 kopie u ocalalych po zmianie = 0, przeniesione = dopisane zwyciezcom (P2); L-2 INFO: przybytek zbrojowni zalog po oblezeniach; reszta 8.16 (punkty 7, 13).
**Ryzyka:** `AiWear._battleSince` (przybytek po bitwie = lup) i GarrisonArmory (zbrojownia zalogi po zdobyciu przechodzi na nowa zaloge) - ten sam stan, sprawdzic kolejnosc; milicja z daru - pytanie 6.
**Koszt:** < 5 ms/d. **NK:** nie.

### E8/C - Cennik (253, PLAN 8.5d-g, 8.4c) - S-M - TERAZ (konie: PO TAK)

| Okno | Zmiana | Dowod |
|---|---|---|
| `ArmsPricing.Compute` uprzaz | waga do kosztu `wr = min(waga gry, max(3, 0.4 x BodyArmor))` | K:ArmsPricing.cs:204-212; RB8 2.4 |
| `ArmsPricing.Compute` helmy | `HeadArmor` + `Plate`, t >= 4: dni = w x 1.0 + 0.5 x (t - 3) | K:ArmsPricing.cs:209; RB8 2.5 |
| `ArmsPricing` luki | `DaysBow` {1.5, 1.5, 1.5, 1.7, 2, 2.5} (t1-2 razem, inaczej t2 > t3) | K:ArmsPricing.cs:66; RB8 2.6 |
| `HistoricalPrices` | mnoznik x8 tylko Arrows/Bolts (Thrown z `DaysThrown` 0.5-3); konie wedlug kategorii t3 `horse` x0.4, t4 `t2_*` x1.5 (opcja) | K:HistoricalPrices.cs:28, :292-303; K:Settings.cs:637 |
| `Recipes.BuildRecipe`, `MendMaterial.UnitKg` | ta sama `wr` (inaczej siodlo ROT kuje sie ze strata: material ok. 250 > wartosc 237) | K:Recipes.cs:262-310; K:MendMaterial.cs:74 |
| `ArmouryBehavior` - nowy `IsMissile` (v2) | Arrows/Bolts/Thrown; uzywany w `NoWear`, `AmmoRecovery` i AiWear (R3). `IsAmmo` bez zmian (Arrows/Bolts), wiec `CleanseAmmo` (start sesji :1303, po bitwie :1894) nie czysci obitych oszczepow i toporow miotanych gracza; `TownFletchers`, `CaravanAmmo`, `HistoricalPrices` maja wlasne `IsAmmo` - nietkniete | K:ArmouryBehavior.cs:2266-2270, :2301-2347; K:AmmoRecovery.cs:35 |
**Klucze:** `HarnessKgPerArmor` 0.4, `HelmDaysPerKg` 1.0, `HistBowDaysNew` (true), `ThrownAsMissile` (true), `HistHorsePrices` (wedlug pytania 2).
**Log:** start "HistoricalPrices: uprzaz z wagi prawdziwej N szt., helmy t4-6 N, luki N, oszczepy N, konie N".
**Test (etap8_c, dump na starcie; v2 - siodla osobno):** C-1 mediany: siodla (uprzaz bez kropierzy i bard) 24-60 wedlug CD, z tolerancja do 90; kropierze INFO; bardy t4-t5 <= 2 400; barda t6 <= 2 600 (CD: 480-2 400, "najlepsze wyzej" - t6 to najlepsze; maks. INFO);
helm t4 30-50 / t5 60-100 / t6 100-160, luk t3 15-24 / t4 20-32 / t5 28-44 / t6 38-60, oszczep <= 40. Gdy wzor `wr` nie da siodel 24-60 - poprawiamy wzor (dni pracy skory dla siodel), nie prog;
C-2 material kucia siodla ROT < wartosc; C-3 polki lukow d120 <= 2 x Z33 (2 995), helmy INFO (warsztaty zrobia 2.4-4.8x wiecej na roboczodzien);
C-4 (v2) suma kas miast d120 >= 80% Z33 (10 264 881, L:36070), kasy ponizej zapasu przez nasza wyplate 0, rody na minusie <= Z33 + 10%; C-5 (v2) oszczep Battered w sakwach -> zapis -> wczytanie -> stan zostaje.
**Ryzyka:** mniej zlota do kas miast (zakupy helmow lordow i zalog ok. -10-15 tys. zl/d [S], granica C-4); `TownFletchers` (linia ranged) i wartosc lupu broni miotanej spada 4-6x; XP Jazdy za konia w Spoils czyta wartosc.
**Koszt:** 0 (przy starcie sesji). **NK:** nie.

### E8/R - Rynek i korona (254, PLAN 8.5b, 8.5c, 8.7b) - S - TERAZ

| Okno | Zmiana | Dowod |
|---|---|---|
| `Settings.WarExpectationEnabled` | domyslnie false (kod oczekiwan zostaje) | K:Settings.cs:482; K:SupplyDemand.cs:188-204; L:35865 |
| `SupplyDemand.PricePostfix` (sprzedaz) | kon: podstawa = Value ze stanem x mnoznik sprzedazy gry bez czynnika podazy, potem polka i `SellByCondition` | K:SupplyDemand.cs:381-403; G DefaultTradeItemPriceFactorModel.cs:170-181 |
| `KingdomTreasury` monopole | 5% dziennej wyplaty wlascicielowi (`ProfitMade / RevenueSmoothenFraction x 0.05`), z kapitalu warsztatu | K:KingdomTreasury.cs:444-452; G Workshop.cs:36; G DefaultClanFinanceModel.cs:871 |
**Klucze:** `WarExpectationEnabled` (false), `HorseSellFromWorth`, `MonopolyFromDailyProfit` (true).
**Test (etap8_r):** R-1 brak "premie wojenne" w linii "Rynek broni"; R-2 proba kup-sprzedaj konia w jednym miescie: zysk <= 0 (jak 155); R-3 monopole: jesli > 0, to <= 1/20 dziennej wyplaty warsztatow (P2).
**Ryzyko:** monopole maja dzis 0 i przyczyna nie jest znana. Nowy wzor bierze 5x mniej niz stary, wiec ozywienie (CLAUDE.md 8 pkt 4) moze tylko zmniejszyc pobor. **Koszt:** 0. **NK:** nie.

### E8/K - Kuznia: ksiazki BK, zlecenia kowalskie, opis bramy (255, PLAN 8.3) - M - TERAZ

| Okno | Zmiana | Dowod |
|---|---|---|
| `HistoricalPrices.Apply` | ksiazki BK z Value < 100 -> x100 (24 szt.), do `_target` (blokada `ValueSetPrefix`) | BR ok. :7017; RB8 1.1 |
| BK `OnBuyBookConsequence` (lambda) | transpiler `ChangeHeroGold(-x)` -> `GiveGoldAction(gracz, ksiegarz)`; ksiazka schodzi z rosteru ksiegarza (szukac `ChangeHeroGold`, nie stalej - `BookTranspiler` juz zamienil 1000) | BK BKEducationBehavior.cs:385-398; K:HistoricalPrices.cs:969-1028 |
| konstruktor `CraftingOrder` | postfiks: `PriceOne(projekt)` i `BaseGoldReward` = Value historyczna x 1.3 x (0.9-1.1) (refleksja - readonly); zlecenia z zapisu przeliczone przy wczytaniu, gdy nagroda > 3 x nowej | G CraftingOrder.cs:107-122; G DefaultTradeItemPriceFactorModel.cs:196-203 |
| `CraftingCampaignBehavior.CompleteOrder` | `null` -> `OrderOwner` (wspolny wrapper wyplat `PayFromGiver` - uzyje go D1); przy niepelnej zaplacie: "{0} could pay only {1} of the agreed {2}." | G CraftingCampaignBehavior.cs:1150-1154 |
| `CompleteOrder` - towar i ludzie (v2) | wykuta bron: zlecenie lorda - jak gra (`ChangeCraftedOrderWithTheNoblesWeaponIfItIsBetter`), a gdy nie lepsza - do taboru lorda; zlecenie notabla - na polke miasta zlecenia (dzis znika); `GiveTroopToNobleAtWeaponTier` (zolnierz z niczego) wyciety prefiksem z licznikiem - regula G2. [W] gdzie gra zdejmuje sztuke z ekwipunku gracza - sprawdzic przy paczce | G CraftingCampaignBehavior.cs:1150-1184, :1264-1274 |
| `CreateTownOrder`, `CreateCustomOrderForHero` | szablon tylko z klas, w ktorych kazdy wymagany slot ma otwarta czesc gracza (`IsOpened`); tier czesci <= najwyzszy otwarty + 1 | G :944-956, :986-1001; OT 5 |
| `Settings.cs` opis `ForgePartsFreeBelowTier` | prawdziwy opis (klasy z pula powyzej progu zamkniete do przetopu; log je wymienia) | K:Settings.cs:1214; CS:1418 |
**Klucze:** `BookPriceFix`, `CraftOrdersHistorical`, `CraftOrdersOpenedOnly` (true). **Log:** "ksiazek BK przeliczonych N"; okna obiegu "zlecenia kowalskie" i "ksiegarz".
**Test (etap8_k):** K-1 "ksiazek BK przeliczonych 24"; K-2 zlecen z zapisu przeliczonych N (bieg zapisu 9 dob); K-3 okno "zlecenia kowalskie": z niczego 0, zolnierz z niczego 0, bron w nicosc 0 (gra Jeffa - 8.16 punkt 6).
**Ryzyka:** XP kowalstwa za zlecenie musi dalej brac `Orig` (K:HistoricalPrices.cs:339, :991); nasze zamowienia zbroi (`Orders.cs`) to inny system - nietkniety. **Koszt:** 0. **NK:** nie.

### E8/D1 - Nagrody i wyplaty w zadaniach (256, PLAN 8.18 a) - M - TERAZ (v2)

**Zmiana (R1, R2; v2 - oba kierunki, skala w wrapperze, kwota zamrozona, towar):** przy starcie skan typow `IssueBase`/`QuestBase` ze wszystkich zestawow (gra, SandBox, BK, ROT, NavalDLC).
W metodach, ktorych IL wola `GiveGoldAction.ApplyBetweenCharacters` / `ApplyForCharacterToParty` albo `ChangeHeroGold` gracza, transpiler zamienia wywolanie na `PayFromGiver(giver, recipient, amount, show)`.
Prefiks metody zapamietuje `__instance` (`[ThreadStatic]`), finalizer czysci.
- **null -> gracz:** placi zleceniodawca (`IssueOwner` / `QuestGiver`); kwota = min(kwota gry, kwota zamrozona przy przyjeciu), do kiesy platnika.
- **gracz -> null:** odbiorca: koszt rozwiazania zastepczego i zakupy (narzedzia, konie, bydlo, bron) - kasa osady zadania (zakup na jej targu); lapowki w zadaniach - kasa osady zadania; reszta - zleceniodawca. Kwota bez zmian (to cena gracza).
- **RevenueFarming (wyjatek):** wplata godzinowa z kiesy wsi zadania (do jej wysokosci, G RevenueFarmingIssueBehavior.cs:610-611); oddanie zebranej sumy (:527) - do zleceniodawcy; strata w zasadzce (:1041) i oplata zbrojnych (:1077) - do kiesy wsi. Inaczej lord placilby graczowi podatki wsi, a gracz oddawalby je w nicosc.
- **Skala w wrapperze, nie w getterze:** obejmuje tez stale kwoty (BettingFraud 250 / 2 500 / 4 500, RaidAnEnemyTerritory +3 000 za wies, LandLordCompanyOfTrouble 1 000 + 150/czl., `_counterOfferGold`) i zadania bez rozwiazania zastepczego (rywal, podboj) - bez wyjatku "kwota gry".
  N x D: z rozwiazania zastepczego, inaczej z tabeli typ zadania -> N x D w paczce (np. rywal - druzyna gracza x 14 dni, podboj - x 30 dni [S]); stawka = zold na czlowieka druzyny gracza (`TotalWage / ludzi`, fallback 5 zl). Getter `RewardGold` dostaje postfiks tylko do wyswietlenia oferty (ta sama liczba).
- **Zamrozenie przy przyjeciu:** slownik zadanie -> kwota (przyjecie zadania albo wybor rozwiazania zastepczego), sekcja zapisu `arm_e8_questpay` z wersja (R14); stary zapis bez sekcji = kwota liczona przy wyplacie.
- **Towar:** dostawy - towar schodzi od gracza na polke osady zadania albo do taboru zleceniodawcy (wzor ArmyNeedsSupplies :364-365), zleceniodawca placi wartosc; rzecz na wymiane (`GiveTradeOrExchangeRewardToMainParty`) - z magazynu osady (bez `AddToCounts` z niczego), gdy jej brak - zloto od zleceniodawcy; bron oddana bandzie (GangLeaderNeedsWeapons :1118, :1136) - na polke osady zadania.
**Dowod:** RB8 5.1 (35 plikow `Issues`, 23 + 10 + ok. 17 wyplat z `null`; SandBox 11); gracz -> null: G ArtisanOverpricedGoods :327, GangLeaderNeedsToOffloadStolenGoods :649, GangLeaderNeedsWeapons :257, :1106, HeadmanVillageNeedsDraughtAnimals :245,
LordNeedsHorses :264 (`ChangeHeroGold`), Smugglers :829, VillageNeedsTools :272, RevenueFarming :527, :1041, :1077; stale kwoty: BettingFraud :662, :761, :797, RaidAnEnemyTerritory :524-527, LandLordCompanyOfTrouble :392; towar: VillageNeedsTools :594, :622-624.
Przyklady skali: kryjowka 10 x 7 -> 438 (gra 3 000), szkolenie 18 x 14 -> 1 575 (4 000), bunt szlachty 43 x 13.5 -> 3 628.
**Klucze:** `QuestRewardFromGiver`, `QuestRewardInWageDays`, `QuestPlayerPaysSomeone`, `QuestGoodsReal` (true). **Log:** start "Nagrody zadan (E8/D1): wpiete N metod w M typach"; doba "obiecane X, zaplacone Y, z niczego Z, w nicosc W, towar z niczego / w nicosc T"; dla kazdego typu "kwota gry / po skali".
**Test (etap8_d1):** D1-1 linia startu, N >= 40; D1-2 z niczego = 0 i w nicosc = 0 (oba kierunki); D1-3 obiecane = zaplacone, gdy platnik ma pieniadze; D1-4 towar z niczego / w nicosc = 0.
Autotest nie robi zadan - sztuczne zakonczenie jednego zadania i RevenueFarming wywolaniem w autotescie, jesli tanio; inaczej 8.16 punkt 13.
**Ryzyka:** `PeopleUnit.QuestSites` patchuje te same metody zadan wsi (K:PeopleUnit.cs:320-340, :455-475) - transpiler nie rusza ich prefiksow; okno w `CirculationWindows`; spadek dochodu gracza jest celem decyzji;
nowa sekcja zapisu (stary zapis bez niej - bez bledu). **Koszt:** 0 w dobie (skan przy starcie < 1 s). **NK:** nie.

### E8/D2 - Nagroda turnieju gracza, zaklady, oplaty i drobne zrodla gracza (257, PLAN 8.18 b; RC8 poz. 4) - S-M - TERAZ (v2)

| Okno | Zmiana | Dowod |
|---|---|---|
| **poza E8 (v2):** jency sprzedani miastu (takze gracz u posrednika) i nagroda turnieju AI | juz zamyka RealPayer (E3/1, 164a-1, `TownPaysRewards` true): kasa miasta z nadwyzki `Seal31.Spare`, podzial zolnierze/bohaterowie, postfiks First przed 178 (Last). E8 tego nie dotyka | K:RealPayer.cs:20-33, :271-288; L:36077 |
| `TournamentManager.GivePrizeToWinner` - gracz | przedmiot nagrody schodzi z polki miasta-gospodarza; gdy go tam nie ma - zamiast przedmiotu jego cena od miasta z nadwyzki (jak u AI). Postfiks albo ten sam kontekst co RealPayer - bez drugiego prefiksu przed RealPayer (High) i RBMTournament (Normal); sciezke gracza w RBMTournament sprawdzic przy paczce | G TournamentManager.cs:186-188; K:RealPayer.cs:285-288 |
| TXP `TXPManager` (zloto 700 + premie) | przy paczce sprawdzic, czy idzie przez `GiveGoldAction(null, ...)` w kontekscie RealPayer; jesli osobno - ten sam kontekst (kasa miasta), nie nowa regula | TXPManager.cs:182; RB8 5.3 |
| SB zaklady turniejowe | jeden kontrahent - kasa miasta (w grze juz do niej idzie przegrana :277): postawienie (gracz -> null :343) do kasy, wygrana (null -> gracz :300) z kasy z nadwyzki, reszta - mniej | SB TournamentBehavior.cs:277, :300, :343 |
| SB lapowka przy odbijaniu wieznia | kwota w dniach placy straznika (jak lapowka strazy), do sakiewki zalogi (`MenPurse`), bez zalogi - kasa osady; dzis 1 000 + okup / 2 000 x bezpieczenstwo x 40, w nicosc | SB PrisonBreakCampaignBehavior.cs:499; G DefaultPrisonBreakModel.cs:34-39; KR FairRansom.cs:62-66 |
| SB gra planszowa | zaklad od i do przeciwnika (jego kiesa, do jej wysokosci) | SB BoardGameCampaignBehavior.cs:151, :159 |
| SB nagroda areny treningowej (5-60) | z kasy miasta (nadwyzka) | SB ArenaMasterCampaignBehavior.cs:443 |
| `DefaultBribeCalculationModel`, `BribeGuardsAction` | 60 / 36 / 6 do sakiewki zalogi (`MenPurse`), bez zalogi - kasa osady | G :50-62; G BribeGuardsAction.cs:17-18 |
| SB golibroda, karczmarz, kolejka | 3; 12; 0.3 x ludzi - do kiesy ludu miasta (transpiler stalych, wzor `BookTranspiler`) | SB BarberCampaignBehavior.cs:145; TavernEmployeesCampaignBehavior.cs:482, :913, :926-929 |
| gra: akcja wrogosci gracza wobec wsi; utrata warsztatu gracza z wojny; NavalDLC Coastal Guard Edict | hearth x4 z niczego -> z kiesy wsi (do jej wysokosci); zwrot ceny warsztatu -> od nowego wlasciciela (do jego kiesy); edykt - przy paczce sprawdzic platnika | G VillageHostileActionCampaignBehavior.cs:556; G ChangeOwnerOfWorkshopAction.cs:17; CH:82 |
**Klucze:** `PlayerTourneyPrizeFromTown`, `PlayerBetsCounterparty`, `PlayerFeesHistorical`, `PlayerSmallSourcesClosed` (true). v2: `TourneyPrizeFromHost` i `PrisonerSaleTownPays` skreslone - to robi juz `TownPaysRewards`. Pule GrandTourney gracza bez zmian (paczka 140).
**Test (etap8_d2, v2 - progi na tym, co D2 zmienia):** D2-1 linia "Wydatki gracza (E8/D2)": oplaty i zaklady - kwota, odbiorca, w nicosc 0; D2-2 nagroda turnieju gracza: przedmiot z polki / cena od miasta, z niczego 0;
D2-3 kontrolka: "gracz u posrednika: wycena / zaplacone" (164a) i okna "jency" / "turniej" bez zmian wobec Z33 - dowod, ze E8 nie dubluje RealPayer; reszta 8.16 punkty 12, 14, 15.
**Ryzyka:** zwrot ceny warsztatu siedzi w oknie E5/A7 SmallGold, ktore celowo go nie tnie (CH:74). Jeden kontrahent zakladu i jedna regula platnika osady (R1, `Seal31.Spare`). TXP i RBM to cudze mody - IL sprawdzic przy wpinaniu. **NK:** nie.

### E8/B - Warsztaty i kowadlo (258, PLAN 8.9; LAWA 2, 3, 6, 14; RC8 poz. 20, 22, 30) - S-M - TERAZ

| Okno | Zmiana | Dowod |
|---|---|---|
| `ArtisanInputs` | wyrob uboczny z `added < _cycles` - dopisac `_cycles - added` szt. na polke, kasa miasta placi warsztatowi z nadwyzki `Seal31.Spare` (R1, v2: dopisujemy tyle sztuk, ile kasa oplaci; reszta - licznik "niesprzedane"); welna z dodatkowych cykli uboju z p = 0.15 albo pelna (**pytanie 5**) | K:ArtisanInputs.cs:36-37, :182; CH:7023 |
| pekniecie zbroi w zakladce CRAFT (BK) | zwrot 50% kwitu do sakw (jak bron w grze); sciezke BK znalezc w `BKArmorCraftingVM`/`BKSmithingModel` | K:ArmourQuality.cs:225-242; K:FletchForge.cs:207, :265 |
| `SmithMenu.DoMendOne` | miekki surowiec zdejmowany przez `Recipes.Take` (wedlug kategorii), nie dokladnym przedmiotem | K:SmithMenu.cs:1064-1129; K:Recipes.cs:644; CH:508 |
| lawa gracza, kwatermistrz Spoils | robocizna napraw do kiesy ludu (R5, klucz N1 `MendPayToFolk`), material jak dotad | CH:560; K:Settings.cs:1197 |
| `WorkshopLaw` | kopalnia BK gracza z `BulkLineSkip` (dzis x10 metalu z niczego) | K:WorkshopLaw.cs:114; CH:683 |
| `MendMaterial` (2), dwie ceny kowala (3), ekran utrzymania warsztatu (6), pusta kiesa tlumi popyt zamowien (14) | 1e9 w drugiej sciezce; jedna cena; tekst `UpkeepAt`; wpis popytu niezalezny od kiesy | K:MendMaterial.cs:793, :830; K:WorkshopTrade.cs:131; K:SmithMenu.cs ok. :1877 |
| `RecruitCost` (12-13) | sprawdzic, czy martwe po 160 - bez zmiany, jesli martwe | K:RecruitCost.cs:45-49 |
**Klucze:** `ArtisanByproductsFull`, `ArmourBotchHalfBack`, `MendTakeByKind`, `PlayerMineBulkSkip`, `SmallFixes8` (true).
**Test (etap8_b):** B-1 linia ArtisanInputs: wyroby uboczne dopisane N/d, zaplacone przez kasy miast, ponizej zapasu przez nasza wyplate 0 (v2); B-2 indeks welny bez zalegania ponad Z33 (INFO); reszta 8.16 (punkty 2-4). **Koszt:** < 5 ms/d. **NK:** nie.

### E8/RC - RealisticCaptivity drobne (259, LAWA 7/16, 8, 10; RC8 poz. 32) - S - TERAZ

| Okno | Zmiana | Dowod |
|---|---|---|
| KR `Work.cs` | dniowki i warta patrza na nadwyzke miasta wedlug R1 - `Seal31.Spare` Armoury przez nowe `TownWageLink.SpareD4` (refleksja; bez Armoury - jak dzis, rezerwa 20 000); stare `TownWageLink.Spare` i inne uzycia `TownRentFloorGold` bez zmian (rozdz. 7) (v2); bez `Math.Max(1)` nad `RoundRandomized`; pusta kasa zatrzymuje prace w toku, niezaplacone dni nie licza sie do nasycenia | KR:Work.cs:118, :131, ok. :334 |
| KR `CaptivityBehavior` | splata dlugu okupu i wykup rynsztunku przy pojmujacym bez bohatera -> `GoldToCaptor` (regula decyzji 9) | KR:CaptivityBehavior.cs:443, :829, :568-592 [W] |
**Klucze:** `WorkRespectsReserve`, `DebtToCaptorHome` (true). **Test:** RealisticCaptivity.log 0 ERROR; 8.16 punkty 14, 17. **NK:** nie.

### E8/G1 - Karawany (260, PLAN 8.12a-b) - S-M - PO TAK (pytanie 3); wyspy TERAZ

| Okno | Zmiana | Dowod |
|---|---|---|
| BR `BKCaravansHourlyThrottlePatch.Prefix` (v2) | **postfiks na samej metodzie prefiksu BKROT** (static, prywatna - Harmony ja lata): w miescie (`CurrentSettlement?.Town != null`, bez oblezenia) `__result = true`; w drodze wynik BKROT bez zmian. Wpiecie po zaladowaniu BKROT, linia "wpiete" (R11). Prefiks z wyzszym priorytetem NIE dziala: Harmony (MethodCreator.AddPrefixes) przed kazdym prefiksem wplywajacym na oryginal sprawdza `runOriginal`, a wynik bool nadpisuje go - prefiks BKROT i tak zwroci false | BR BKCaravansHourlyThrottlePatch.cs (`Prefix(MobileParty __0)`); 0Harmony MethodCreator; BK BKCaravansBehavior.cs:858-860 |
| BK zakup karawany lorda | wyspa z jednym miastem i rod bez statkow -> zakup pominiety (zloto zostaje u lorda) | BK BKLordPropertyBehavior.cs:69-79 |
| karawana bez drogi (v2) | rozwiazana TYLKO przy dowiedzionym braku drogi: miasto bez innego miasta w zasiegu ladu (`IslandRoads._lonely`) i rod bez statkow - stan, nie czas postoju (bez "prawa na zegar", SP 12.10 00:20); towar do miasta za kase miasta (nadwyzka, R1), kiesa + utarg do wlasciciela, ludzie do karczmy/wsi (sciezka VetPurse). **Przed kodem:** w dekompilacji BK sciezka rozwiazania karawany i zwrot ceny (podwojny zwrot = zloto z niczego) - wynik dopisac tutaj | L:35942; K:IslandRoads.cs:75 |
**Klucze:** `CaravanTownHourly` (wedlug pytania 3), `IslandCaravanFix` (true).
**Test (etap8_g1):** G1-1 karawany 7+ dob w tym samym miescie <= 10 (Z33 29); G1-2 sredni postoj w miescie <= 12 h (P3); G1-3 karawany bez drogi na koniec biegu 0; G1-4 koszt dodatkowych tikow <= 5 ms/d;
G1-5 (v2 - prog, nie INFO): suma kas miast d120 w +-10% Z33 (10 264 881, L:36070), kasy ponizej zapasu przez nasza wyplate 0, kiesy karawan notabli (902 karawany) w +-20% Z33; zysk karawan, `CaravanBulk` wizyt/d (dzis 115), zuzycie `CaravanAmmo` - INFO.
**Ryzyka:** najwieksze ryzyko ekonomii w etapie 8 - wiecej handlu, myta i podatku (granice G1-5). Bez sekcji zapisu: rozwiazanie wedlug stanu, licznik postoju (`IslandRoads._inTown`, zerowany przy wczytaniu) tylko do linii (v2). Przeplyw jest prawdziwy (kasa karawany -> kasy miast). IslandRoads i postfiksy BuyGoods biegna w tym samym tiku - kolejnosc bez zmian. **NK:** nie.

### E8/G2 - Najemnicy i uczta BK (261, PLAN 8.12e; RC8 poz. 1) - S - PO TAK (pytanie 4); uczta TERAZ

| Okno | Zmiana | Dowod |
|---|---|---|
| BK `ConvertTroopsMercenaries` (v2 - okno wprost) | rozszerzony `SpoilsCompany.BkConvertPrefix` (jeden prefiks, licznik w nim) dla wszystkich klanow najemnikow: **klan w krolestwie** - prefiks sam robi zamiane 1:1 jak BK, z warunkiem `!troop.IsHero` (szansa `MercenaryConversionChance` prywatna - refleksja), i zwraca false; **klan bez krolestwa** - zwraca false (dosypka 3-5 ludzi z niczego nie zachodzi). Dzis prefiks Spoils zwraca false i pomija cala metode BK razem z zamiana 1:1 - stad dwie galezie | K:SpoilsCompany.cs:405-417; BK BKClanBehavior.cs:480-540; L:35931 |
| BK `Feast.Automanage` | po zakupie `-n` z rynku (dzis `+n` - towar z niczego); ten sam plik co E5/A8 `FeastTownGetsPaid` | BK Feast.Automanage :216-219; CH:50 |
**Klucze:** `MercClansRealSoldiers` (wedlug pytania 4), `FeastNoFreeGoods` (true). **Test (etap8_g2):** G2-1 bohaterowie zamienieni 0, dosypani 0 (P3); G2-2 INFO wielkosc druzyn klanow; G2-3 uczta: towar z niczego 0.
**Ryzyko:** mniejsze druzyny najemnikow bez sluzby - zgodnie z "bez sztucznych doplat". **NK:** nie.

### E8/Q - Przeprawa: CrossingLaw AI na zegarze gry (262, ERRORS/E7; RC8 poz. 9) - S - TERAZ

**Zmiana:** `AiTick` niezalezny od pozycji gracza (dzis `return` przed nim - K:CrossingLaw.cs:73-76, :100). Biegnie raz na godzine gry, a nie na zegarze klatek i `DateTime.Now` (K:CrossingLaw.cs:92).
v2: petla po `MobileParty.AllLordParties` (ok. 600; dzis `MobileParty.All` - 5 201 partii x 3 przeprawy x 24 h, ok. 375 tys. przebiegow na dobe), odleglosc od przeprawy liczona pierwsza. Spiacych pomija PRZED `SetMoveGoToPoint` i `_aiCooldown` (inaczej SleepGate E7/O1 zablokuje ruch, a licznik zawrocen i tak urosnie - K:CrossingLaw.cs:119-154). `Measure169c` szuka Bliznakow po id `ROT_town3` (CH:279). Licznik zawrocen idzie do linii E7/B.
**Klucze:** `CrossingLawAi` (istnieje, J:154 true), nowy `CrossingLawAiClock` (true - pozwala zgasic nowa petle bez ruszania klucza Jeffa).
**Test (etap8_q):** Q-1 INFO: zawrocone N/d (> 0 przy wojnie z Freyami); Q-2 koszt <= 5 ms/d; Q-3 zadna partia zawracana > 3 razy na dobe (zakleszczenie).
**Ryzyka (ozywiany kod):** ile pracy = wrogie partie w promieniu przeprawy na godzine (licznik E7/B). Zmieni sie geografia wojny Dorzecza (to zamiar ustawienia). Stawki myta liczyc dopiero po E8/Q (SP 15:00). **NK:** nie.

### E8/T - Start kampanii (NK) i rekruci gracza (263, PLAN 8.10a-c, 1.12) - S-M - TERAZ

| Okno | Zmiana | Dowod |
|---|---|---|
| `ColdStart.Markets` | po tierach w dol szukac w gore (t+1..5); koniec dopiero, gdy cech nie ma zadnej sztuki; licznik budzetu cechu: dany / zuzyty / przepadly | K:ColdStart.cs:205-212 |
| `StartKit.Substitute` | kon tylko tej samej rodziny co uprzaz w slocie 11 (albo para kon + uprzaz) - interpretacja "kon bez uprzezy" (**pytanie 7**) | K:StartKit.cs:57-70; L:142 (`MountMeshGuard`) |
| `LevyGold.PartyCreatedPrefix` | partia lorda AI w dobach 1-3 kampanii dostaje dorobek `ColdStart.FillToTemplate(mp)`, jak 34 pierwsze | K:LevyGold.cs:171-184; K:GarrisonArmory.cs:1041; L:231, L:341 |
| DTE `RecruitmentPatch.AddStartingEquipmentToPlayerArmory`, `TavernMercenaryEquipmentPatch`, `TownMenuMercenaryEquipmentPatch` (v2) | gracz ta sama sciezka `RecruitKit` co AI: ochotnik t2+ - zapisany komplet notabla (kupione + dobytek); tier 1 - dobytek; najemnik z karczmy - dobytek oraz kon i rzad ze wzorca (171/160). Regula "tylko to, co kupil notabl" NIE tnie najemnikow; NIE NK | DTE RecruitmentPatch.cs:36-70, TavernMercenaryEquipmentPatch.cs:20, TownMenuMercenaryEquipmentPatch.cs:41; K:RecruitKit.cs:14-26; SP:354, :448 |
**Klucze:** `ColdStartTierUp`, `StartKitHorseFamily`, `ColdStartLateLordParties`, `PlayerRecruitNoFreeKit` (true).
**Test (etap8_t, nowa kampania):** T-1 "ColdStart: zbrojownie" obejmuje 280-300 partii lordow; T-2 budzet przepadly 0; T-3 nowe partie AI bez kompletu w dobie 1 <= 100 (Z33 541); T-4 zakupy AI w dobach 1-3 <= 50% Z33 (2 451 / 6 123 / 5 037).
**Ryzyka:** dorobek startowy to sprzet "z niczego" - celowo, raz, jak dzis dla 261 partii (dorobek stuleci). Rekruci gracza: pierwsze dni kampanii trudniejsze (decyzja Jeffa). **NK:** tak (8.10a-c), 1.12 nie.

### E8/E - BetterEconomy: klucz 14 w skrypcie (264, PLAN 8.14) - S - TERAZ, uruchomienie przy wgraniu

`tools/bee/zamknij-ujscia-bee.ps1` (lista `Nowy-Klucz` :50-58): klucz 14 `EconomicEventDailySpawnChance` float 0.14 -> 0, "World: BEE random events"; ten sam w `cofnij-ujscia-bee.ps1`.
Skrypt musi przyjac plik, w ktorym 13 kluczy ma juz wartosci docelowe (`Stan-Klucza` :193+). Plik Jeffa: `Modules\BetterEconomy\ModuleData\better_economy_settings.xml:361`, SHA 3a24554b... (RB8 4).
Bez DLL. Uruchamia Jeff albo agent za zgoda przy wgraniu koncowym: gra zamknieta, kopia .bak. **Test:** proba na kopii pliku - XML sie parsuje, zmienia sie tylko klucz 14, ponowne uruchomienie nic nie zmienia.
Instrukcja (Z.9): 1 linia.

### E8/O1 - Polka po wlasnym zdjeciu sztuki; delegaty DTE (265, PLAN 8.17; RD8 O1 + O6) - S - TERAZ

**Zmiana:** po wlasnym `AddToCounts` (`ShelfBuy.Take`/`PutBack` - 9 miejsc: AiGear x2, wywoz, Stables x2, HorseFate, PackMarket, BuildFunding, BkSupplyMarket) indeks polki dostaje -n/+n
i nowe (Ver, Count), jesli przed zmiana byl aktualny. v2: **kopia przy zapisie** - nowy `Idx` z `Array.Copy` dwoch tablic (typy x 8), -n/+n w kopii, potem podmiana `Holder.Cur` (zasada 174b.5: nigdy zmiana opublikowanych tablic - odczyty biegna tez poza glownym watkiem, K:ShelfIndex.cs:16-17, :25; K:IslandRoads.cs:28, :49, :262); dalej znacznie taniej niz `Build`. Gdy wersja skoczyla o > 1 - zwykla przebudowa. Samokontrola (co 64./4096. odczyt) zostaje (RD8 K1).
Delegaty `AccessTools.MethodDelegate` zamiast `MethodInfo.Invoke` w AiGear (`_add`, `_blTest`) i MaterialOrders (RD8 K6).
**Klucze:** `ShelfIndexOwnUpdate`, `DteDelegates` (true). **Test (etap8_o1):** O1-1 rozjazdy samokontroli 0; O1-2 przebudowy <= 9 tys./d (z 13.8); O1-3 ceny sprzetu <= 450 ms/d (z 617). **Zysk:** 70-150 ms/d [S].

### E8/O2 - WorkshopLaw bez refleksji i powtorzen (266, RD8 O2) - S - TERAZ

**Zmiana:**
- `_wip` i `_rank`: klucz z `IEquatable` albo slownik w slowniku (format zapisu `arm_wip` bez zmian);
- pamiec `LineKey` (Production -> napis);
- `SupplyDemand.Factor` raz na koszyk w `Candidates`;
- pamiec `Needs` i `HistoricalPrices.DayWageOf(it)` na sztuke, z wersja ustawien; `TownWage.Index(town)` przy kazdym wywolaniu albo raz na miasto i dobe (v2 - zalezy od dobrobytu miasta, K:WorkshopLaw.cs:153-159, K:TownWage.cs:19-25);
- `Forbidden` bez `ToLowerInvariant()` na sztuce.
Dowod: RD8 K2 (jedyne klucze `KeyValuePair` w repo; 136 us/cykl, L:35846).
**Klucz:** `WorkshopLawFast` (true). **Test (etap8_o2):** O2-1 rozjazdy 0 (probka 1/64: wynik starej i nowej drogi); O2-2 CyclePrefix <= 500 ms/d; O2-3 zapis -> wczytanie: "Warsztaty (zapis 174)" ta sama liczba sztuk. **Zysk:** 150-400 ms/d.

### E8/O3 - Wywoz kupcow (267, RD8 O3) - S - TERAZ

**Zmiana:** lista celow w zasiegu dla kazdego zrodla z pamieci z kluczem (zasieg z ustawien, `GarrisonCarts.On`, zbior osad), nie "raz na sesje" bez warunku (v2); kolejnosc `places` (te same remisy, warunek `profit <= bestProfit`).
`Demand(dst)` i `Multiplier(dst, probe)` liczone raz na koszyk. `TradeCarts.Incoming` i oblezenia zostaja w petli.
v2: drogie liczenie celu juz dzis biegnie tylko dla osad w zasiegu (K:SupplyDemand.cs:545-554 - `dist > range` przed `Demand`); O3 oszczedza tani przebieg po 227 osadach i powtorzony popyt/mnoznik. Wiekszosc czasu wywozu to wyceny `ShelfBuy.Take` z przebudowa polki po kazdej sztuce (:565, :582) - teren O1.
**Klucz:** `TradeExportFast` (true). **Test (etap8_o3):** O3-1 rozjazdy 0 (probka: te same pary zrodlo-cel i sztuki); O3-2 czas wywozu <= 220 ms po O3 i <= 150 ms po O1+O3 (z 312.5, L:36032); O3-3 sztuki i zaplacone w probce identyczne.
**Zysk:** ok. 50-110 ms w jednej klatce [S] (v2; bylo 200-450 - liczylo drugi raz wyceny, ktore obejmuje O1).

### E8/O4 - Musztra: jeden odczyt pozycji lordow na godzine (268, RD8 O4) - S-M - PO TESCIE

Kod dopiero po podziale E8/0 (godziny ruchu / tik treningu / zuzycie / kowale / linie). Wspolny odczyt pozycji lordow na godzine dla `Drill`, `NightMarch.AiSleepLedger` i `WorldMeasure`
(dzis 3 petle + SnapshotPositions - zapowiedz SP "DECYZJE JEFFA 08.10"). Petle po rosterach raz na partie i dobe. **Klucz:** `DrillFast`. **Test:** jak O2 (rozjazdy 0), musztra <= 60% Z33 po 120 dobach.
**Zysk:** 100-300 ms/d po roku. **Ryzyko:** srednie (trzy uzytkownicy tej samej migawki).

### E8/O5 - Skok doby: GarrisonArmory po godzinach (269, RD8 O7) - S - TERAZ

**Zmiana:** `SellWeek` po godzinach (`Id % (7 x 24)`) zamiast 32 zalog w takcie doby (dzis 0.1-0.18 s, szczyty 2.8 s - RD8 K7).
**Klucz:** `GarrisonSellHourly` (true). **Test (etap8_o5, v2 - poza R9):** O5-1 maks. taktu doby (E8/0) <= 1.0 s; O5-2 sprzedane sztuki i utarg zalog tygodniowo w +-15% Z33 (sprzedaz trafia na inny stan polek i ceny, wiec sumy nie moga byc identyczne - to zmiana kolejnosci w dobie, nie ta sama droga);
czesc ksiegi obiegu przechodzi z "Armoury tick dobowy" do "poza tickiem" (progi R w `sprawdz_logi` przeliczone).
**Zysk:** 0 CPU, znika zamrozenie 0.1-2.8 s. **Ryzyko:** srednie (godzina zdarzen w ksiedze).

### E8/O6 - Zapis logu otwartym plikiem (270, RD8 O5 bez utraty linii) - S - TERAZ

**Zmiana (v2):** jeden slownik pisarzy (plik -> `StreamWriter`, `AutoFlush`, `FileShare.ReadWrite` - Jeff moze czytac log w trakcie gry) pod `Gate` dla WSZYSTKICH drog zapisu: `Info` (glowny plik i 11 plikow tematycznych w `Logs/<sesja>`), `Error` i `Csv`.
Gdyby otwarty byl tylko glowny plik `Info`, `File.AppendAllText` w `Log.Error` dostalby blad wspoldzielenia, ktory polyka `catch {}` - linie ERROR znikalyby bez sladu, a T0 przechodzilby falszywie (K:Log.cs:69, :88, :120).
Przy CTD nic nie ginie (flush po linii). Zamkniecie przy wyjsciu z gry i zmianie sesji. Dzis kazda linia otwiera i zamyka plik (`File.AppendAllText`, ok. 0.125 ms na linie - RD8 K8); linii jest ok. 700 na dobe (36 tys. glownych + ok. 50 tys. tematycznych w 120 dob), nie 300.
Przed kodem: sprawdzic, czy narzedzia autotestu nie czytaja `Armoury-*.log` w trakcie gry przez `File.ReadAllText` / `File.Copy` (FileShare.Read - zawioda przy otwartym pisarzu).
**Klucz:** `LogKeepOpen` (true). **Test (etap8_o6):** O6-1 czas zapisu (E8/0) <= 20 ms/d (z ok. 90 [S]); O6-2 liczba linii taka sama jak przy wylaczonym kluczu (probka 3 dob); O6-3 wymuszony `Log.Error` w probce jest w pliku. "Lzejsze logi" (mniej linii w grze Jeffa) - pytanie 10.
**Zysk:** ok. 60-80 ms/d [S] (v2: 700 linii/d).

**Razem 8.17 (v2):** O1-O3, O5, O6 TERAZ: ok. 0.3-0.7 s/d mniej [S] (O1 70-150, O2 150-400, O3 50-110, O6 60-80 ms; wyceny wywozu liczone raz - w O1) i bez przyciecia raz na dobe. O4 po tescie: do +0.1-0.3 s. Wiecej daje tylko mniej partii na swiecie (pytanie 11) albo cudzy kod
po `-Profile` (osobna decyzja, wysokie ryzyko AI - rozdz. 7).

---

### 8.16 - Twoje reczne sprawdzenia (krok po kroku; nazwy opcji po angielsku, opis po polsku)

Po tescie zbiorczym, na DLL do wgrania. Zapisz przed kazdym punktem, ktory moze cos zepsuc. Gdy cos wyglada zle, przyslij log Armoury i CrashScribe z tej sesji.

**Nowa kampania i start**
1. **Nowa kampania.** Przejdz tworzenie postaci i okna BannerKings (start, religia). Sprawdz w BK (okno edukacji postaci), czy postac ma jezyk - od tego zalezy latka E8/M (v2).
   Kon i uprzaz w ekwipunku sa tej samej rodziny, wiec kon nie jedzie "goly" (E8/T).
2. **Rekrut t2+ u notabla** (po E8/T): rekrut przychodzi tylko z tym, co kupil mu notabl - bez darmowego kompletu.

**Kuznia**
3. **Kuznia (Smithy -> zakladka CRAFT):**
   - szablon z kazdym slotem: przycisk kucia przy bramie T2 swieci tylko wtedy, gdy kazdy slot ma darmowa czesc;
   - po wykuciu panel wyniku (dzwiek, "Done" bez crasha);
   - w MCM Armoury "Smithing Skill Per Tier" = 35 (masz 35) - wymog umiejetnosci zgadza sie z panelem;
   - topory miotane ROT sa zamkniete do przetopu (CS: "Mends: brama kucia - prog T2 ...", bez "SAMOKONTROLA przywrocila").
4. **Kowal w miescie:**
   - "Pick a piece" - wrak idzie na zlom, sztuka ze zbroi zostaje na grzbiecie;
   - "Mend it yourself" kilka razy na tej samej sztuce, potem zapis i wczytanie - ulamki materialu sa w pamieci, a sztuka materialu schodzi dopiero, gdy sie nazbiera;
   - "Mend everything you wear" - cena kowala.
5. **Sprzedaz u kupca:** sztuka czysta, Battered i wrak - cena wedlug stanu.
6. **Zlecenie kowalskie** (u notabla albo lorda w miescie):
   - nagroda w setkach, a nie w tysiacach;
   - po oddaniu broni zloto przychodzi od zleceniodawcy;
   - przy pustej kiesie pojawia sie komunikat "... could pay only ...";
   - zlecenia sa tylko z klas, w ktorych masz otwarte czesci (E8/K).
7. **Ksiegarz BK:** ksiazka kosztuje 700-1 000, znika z jego oferty, a jego kiesa rosnie.

**Lup, bitwy, misje**
8. **Spoils of War:**
   - "Done" i "Cancel" (Cancel nic nie zmienia);
   - kwatermistrz naprawia z materialem;
   - "Donate to town" - oddane sztuki pojawiaja sie w zbrojowni zalogi (linia Armoury z darem, E8/L).
9. **Bitwa z lucznikami po obu stronach:**
   - linia "AmmoRecovery: po bitwie" wedlug stron;
   - linia kary "Underequipped" (E8/P3);
   - wraki sa w lupie wedlug zabojcy (po E8/L).
10. **Arena albo turniej w pozyczonym sprzecie, potem wygrana bitwa:** Twoj rynsztunek nie jest obity ciosami z areny (E8/A).
11. **"Send troops" w bitwie:** jeden lup, bez podwojnego (zbrojownia + loteria).
12. **Turniej (GrandTourney):** zapis, walka, nagroda. Turnieje w miastach co trzeci tydzien. Po Twojej wygranej przedmiot znika z polki miasta albo dostajesz jego cene od miasta; wygrany zaklad placi kasa miasta (E8/D2, v2).

**Pieniadze gracza**
13. **Zadanie od notabla:** oferta w nowej skali (np. kryjowka ok. 440 zamiast 3 000). Po wykonaniu zloto od zleceniodawcy, tyle, ile obiecal przy przyjeciu (chyba ze ma mniej), linia "Nagrody zadan (E8/D1)". Zadanie z dostawa: towar trafia do zleceniodawcy albo na polke osady (v2).
14. **Oplaty:** lapowka strazy przy lochu albo sali lorda (60 / 36 / 6), golibroda (3), karczmarz - wiesci (12), kolejka dla druzyny (0.3 na czlowieka), lapowka przy odbijaniu wieznia (setki, nie tysiace), gra planszowa (wygrana z kiesy przeciwnika) (v2).
15. **Sprzedaz jencow** posrednikowi w karczmie: placi kasa miasta z nadwyzki (przy biednej - mniej). Dziala od etapu 3 (RealPayer) - sprawdzenie, ze E8 tego nie zepsul (v2).

**Niewola i karczma (RealisticCaptivity)**
16. **Niewola:** ucieczka, okup, praca. Pojmujacy bierze juki i zloto. Splata dlugu okupu idzie do pojmujacego (do kasy osady, gdy to milicja albo zaloga - E8/RC).
17. **Jency do domu** i ekran po bitwie: jency z krain bez niewoli ida do domu.
18. **Rezerwa miasta 20 000:**
    - sprzedaz w miescie z mala kasa - kupiec odmawia ponad rezerwe;
    - praca RC przy pustej kasie staje.
19. **Karczma - najemnicy:** najemnik z wlasnym koniem albo bez konia, bez doplaty za konia; przychodzi ze swoim dobytkiem (E8/T, jak u AI - v2).
20. **Kulawy kon:** leczenie i cena kulawego.
21. **Samonaprawa:** w MCM "Troop Self Mend Enabled" wlaczone (masz) - ludzie naprawiaja obity sprzet z zapasu.

**Mapa, oboz, swiat**
22. **Przeszukanie kryjowki:** "Call the search off", a drugim razem do konca - bez crasha.
23. **Napad na Twoj oboz w nocy ("Make camp"):** namioty i ognisko za Twoja linia. Wymaga "Camp Battle Props" w MCM - w Twoim pliku jest dzis wylaczone, wlaczymy przy wgraniu (E7/O2).
24. **Sen w armii AI:** lordowie nie maszeruja noca. Sen na statku daje kare morale raz.
25. **Wioski:**
    - dymek z herbem, rycerzem i wiara (Alt - wiecej);
    - "Look over the district";
    - komunikat "Smoke over ..." przy Twojej plonacej wiosce;
    - proporczyki nad wioskami z bliska.
26. **Bliznaki w wojnie z Freyami:** zawracaja Ciebie i wrogich lordow (E8/Q).

**Zapis**
27. **Wczytanie STAREGO zapisu** na koncowym DLL: wczytuje sie, w logu Armoury nie ma ERROR.
28. **Nowa gra -> zapis -> wczytanie innego zapisu** w tej samej sesji: nie zostaje stary oboz ani kryjowka, stajnie AI kupuja od razu (E8/S).
29. **Zbrojownia przez zapis:** stan obitych sztuk w zbrojowni jest taki sam po wczytaniu (`arm_armory_wear`).

---

## 4. Test i progi etapu ("Skonczone, gdy")

**Test:** jeden test zbiorczy etapow 5-8 (SP:1263-1266):
- 120 dob nowej kampanii;
- zapis 9 dob, a w nim bieg "nowa kampania -> zapis -> wczytanie innego zapisu w tej samej sesji" (E8/S);
- zima (gdy Jeff zdecyduje);
- bieg dlugi, w tym test Innych od doby 728 - procedura bez kodu, na zapisie tej doby (kandydat `autotest-dlugi.sav` - przed uzyciem sprawdzic, z ktorej jest doby; RC8 5);
- autotest `-Profile` na scalonym DLL (E8/0).
Autotest musi kopiowac pliki danych e7-b (SP 16:35 "WAZNE").
Narzedzie: `python -I tools/sprawdz_logi.py --grupa etap8 <Armoury-*.log> --crashscribe <session-*.log> [--do-doby 120]`. Bloki: etap8_0, _p1, _p2, _p3, _s, _m, _x, _w, _z, _l, _c, _r, _k, _d1, _d2, _b,
_g1, _g2, _q, _t, _o1, _o2, _o3, _o5, _o6 (progi w paczkach).

**Etap 8 jest skonczony, gdy:**
1. **T0:** 0 ERROR Armoury, RealisticCaptivity, GrandTourney; w CS 0 blokow "# ERROR IN A MOD" z naszych modow; 0 wyjatkow BK `CorrectPlayerEducation`.
2. **Kazda latka cudzego kodu** pisze "wpiete N/N" (R11); "nie znaleziono" tylko dla RBL/GT (jesli nie potwierdzone w dekompilacji).
3. **Kazda pozycja 8.1-8.18 ma dowod:** wiersz `sprawdz_logi` albo punkt 8.16 potwierdzony przez Jeffa. 8.14 - plik BEE po skrypcie (SHA w STAN-PRAC).
4. **Szybkosc (8.17), wobec Z33 w tych samych warunkach (nowa kampania, 120 dob):**
   - CyclePrefix <= 500 ms/d;
   - wywoz <= 150 ms (po O1+O3);
   - ceny sprzetu <= 450 ms/d;
   - takt doby maks. <= 1.0 s;
   - zapis logu <= 20 ms/d;
   - wszystkie rozjazdy R9 = 0 (O1-O4, O6); O5 - prog wzgledny O5-2 (v2).
   Czas calej doby to tylko INFO, bo etapy 5-7 dokladaja swoje koszty. Do Jeffa idzie liczba "nasze mody: bylo X, jest Y s/dobe" z E8/0.
5. **Progi etapow 2, 3, 5 i 7** bez zmian, poza zamierzonymi skutkami - kazdy z liczbowa granica wobec Z33 w tych samych dobach (v2; narzedzie czyta log Z33 jako wzor):
   - wspolne: suma kas miast d120 >= 80% i <= 110% Z33 (10 264 881, L:36070); kasy ponizej zapasu przez nasza wyplate 0 (i E5/C1-3: kasy < 1 000 - 0); rody na minusie d120 (suma linii "Skarbce") <= Z33 + 10% (Z33 197 [P]); z niczego / w nicosc w nowych oknach E8 = 0;
   - E8/C - mniej zlota z zakupow helmow i lukow: C-4;
   - E8/R - ceny broni w wojnie bez premii: R-1, sredni mnoznik cen broni w miastach wojny <= Z33;
   - E8/D1, D2 - wydatki i dochody gracza: D1-2..4, D2-1..3 (kasy miast i kiesy zleceniodawcow - przelewy, nie zrodla);
   - E8/G1 - wiecej handlu karawan: G1-5;
   - E8/W - lordowie placa za naprawy: W-1, W-2 i wspolny prog rodow na minusie.
6. **Paczki W CIENIU** (amunicja AI E8/W, sufit ceny E8/Z) - po odczycie linii "zeszloby" wlaczone w fazie poprawek. Krotki bieg kontrolny 30 dob: W-4, Z-2.
7. **E8/O4** zakodowane po podziale E8/0 i sprawdzone tym samym biegiem kontrolnym.

---

## 5. Ryzyka / kontrola calosci (zasada 0)

### 5.1 Wspolne cele latek i stan (plik:linia)

| Cel | Paczki (kolejnosc) | Uwaga |
|---|---|---|
| `AiWear.cs` (OnMapEventEnded :250-300, naprawy :503-544, NoteBought :150-156) | P2 -> W -> Z | P2 tylko liczy; W zmienia share i petle; Z czyta NoteBought (dodatnie stany) |
| `AiGear.cs` (:146, :608-610, :789-800) | P2 -> W (zaplata zakupu) -> Z (wybor, sufit) -> O1 (delegaty) | jeden plik - jedna galaz, kolejne commity |
| `ShelfBuy.Take` / `ShelfIndex` | Z (sufit ceny kolejnej sztuki) -> O1 (indeks) | O1 musi widziec zdjecie z sufitem (te same AddToCounts); kopia przy zapisie, nie zmiana opublikowanego indeksu (v2) |
| `SupplyDemand.cs` (PricePostfix :381-403, Demand :188-204, DailyTradeCore) | E8/0 (wolajacy) -> R (kon, premia) -> O3 (wywoz) -> O1 (Factor/indeks) | O3 i R: `Demand` bez premii - liczyc rozjazdy po R |
| `ArmsPricing.cs` / `HistoricalPrices.cs` | C (Compute, IsAmmo, konie) -> K (ksiazki, CraftingOrder PriceOne) | `BookTranspiler` (:969-1028) i nowy transpiler ksiegarza - rozne instrukcje; `Orig` przed PriceOne (:339) |
| `ArmouryBehavior.IsAmmo` (:2266) / `NoWear` (:2301) / `CleanseAmmo` (:2314-2347) | C (nowy `IsMissile`) -> W (amunicja AI) | v2: `IsAmmo` bez zmian (CleanseAmmo); `IsMissile` w NoWear, AmmoRecovery, AiWear; zuzycie broni miotanej gracza przestaje byc "bronia" |
| `Recipes.cs` / `MendMaterial.cs` / `SmithMenu.cs` | C (wr) -> B (Take, 1e9, platnik) -> X (XP samonaprawy :1096, :1132) | SmithMenu: B i X w roznych metodach |
| `SpoilsSeal.cs` | X (paser :531) -> L (resztki :1205-1220) -> B (kwatermistrz platnik) | trzy rozne miejsca pliku |
| `BattlefieldLaw.cs` / `HorseFate.cs` / DTE OnMapEventEnded | L (calosc) | HorseFate juz zdejmuje konie - rozszerzenie, nie druga latka |
| `GiveGoldAction` (wrapper `PayFromGiver`; RealPayer `GivePrefix` Low) | K (CompleteOrder) -> D1 (zadania, oba kierunki) -> D2 (nagroda gracza, zaklady, oplaty) | jeden wrapper; v2: jency i nagroda turnieju AI zostaja w RealPayer (kontekst + `Seal31.Spare`) - E8 nie lata `SellPrisonersAction` ani galezi AI `GivePrizeToWinner`; E5/A7 okno SmallGold i O14 ksiegi widza te same zdarzenia - platnik zmienia sie przed oknem |
| `CirculationWindows` | K, D1, D2 (nowe okna) | okna tylko licza |
| `PeopleUnit.QuestSites` (:320-340, :455-475) | D1 | transpiler nie rusza prefiksow liczenia ludzi |
| `KingdomTreasury.cs` (:444-452) | P2 -> R | |
| `ColdStart.cs`, `LevyGold.cs`, `StartKit.cs` | T | v2: E8/M nie ozywia sluchaczy (finalizer BKROT juz dzis puszcza zdarzenie dalej) - flaga StartKit skreslona |
| `RecruitKit.cs`, DTE werbunek / karczma / menu najemnikow | T | gracz ta sama sciezka co AI (v2) |
| `CraftingCampaignBehavior.CompleteOrder` (`GiveTroopToNobleAtWeaponTier`) | K | wyplata (wrapper), bron i zolnierz (v2) - jedna paczka |
| `NightRest`, `NightMarch`, `HideoutPurge`, `TerrainEase`, `Stables`, `CrossingLaw`, `WesterosClimate` | S -> Q (CrossingLaw) | po E7/O1 i E7/C (juz scalone); Reset przed SyncData |
| KC `Mends.cs` | M | jedna paczka CrashScribe |
| BK `ConvertTroopsMercenaries` (prefiks `SpoilsCompany`) | P3 (licznik) -> G2 | jeden prefiks z szerszym warunkiem, licznik w nim |
| BK `Feast.Automanage` (E5/A8 `FeastTownGetsPaid`) | G2 | ta sama metoda - jedna latka albo kolejnosc prefiks/transpiler sprawdzona |
| BR/BK karawany (tik BK; postfiks na `BKCaravansHourlyThrottlePatch.Prefix`) | P3 -> G1 | IslandRoads, BuyGoods, `CaravanBulk` w tym samym tiku |
| `WorkshopLaw.cs` (:114, CyclePrefix, Candidates, `_wip`) | E8/0 (podzial) -> B (:114) -> O2 | O2 po B (BulkLineSkip zmienia plan linii) |
| `GarrisonArmory.SellWeek` | O5 | ksiega obiegu: kolumna "tick dobowy" |
| `Log.cs` (Info, Error, Csv) | E8/0 (czas) -> O6 | v2: jeden slownik pisarzy dla trzech drog |
| KR `TownWageLink` | RC | nowe `SpareD4` tylko dla Work.cs; stare `Spare` (20 000) bez zmian (v2) |
| `Settings.cs`, `McmSettings.cs`, `ArmouryBehavior.cs` (konstruktor Reset, OnDailyTick), `tools/sprawdz_logi.py` | wszystkie | gen_mcm po kazdym scaleniu; bloki etap8 dopisywane wedlug numeru paczki |

### 5.2 Spojnosc logiki

- **Zamknieta ekonomia:** K, D1, D2, G2 (uczta, najemnicy), B (kopalnia, wyroby uboczne), RC, L (przeniesienie) usuwaja zloto, towar i ludzi z niczego albo w nicosc - w obu kierunkach (v2: wyplaty gracza w zadaniach, towar dostaw, bron i zolnierz zlecen). Osada placi tylko z nadwyzki `Seal31.Spare` (R1). C, R to tylko ceny. W, Z zmieniaja platnika
  (sakiewka/kiesa lorda) i kolejnosc, a nie ilosc zlota. Zadna paczka nie dodaje doplat (SP:1242). Spadek dochodu gracza (D1, D2, K) i lordow (W) jest zamierzony.
- **Ludzie z niczego:** G2 (najemnicy) zamyka zrodlo ludzi. Milicja z daru Spoils zostaje do decyzji (pytanie 6). Dorobek startowy (T) dotyczy sprzetu, nie ludzi.
- **Jedna zasada na zjawisko:** R3 (pocisk), R4 (zuzycie), R5 (naprawy i platnik robocizny), R6 (lup), R7 (wycena) - kazda zamyka dzis istniejace "dwie reguly" (RA8 3.3, 6.4; RB8 2.4; RC8 poz. 22).
- **Kolejnosc w dobie:** O5 przenosi sprzedaz nadwyzek zalog z taktu doby na godziny (suma podobna - prog wzgledny O5-2, v2); E8/Q na godzine gry; G1 dodaje tiki BK w godzinach. Reszta bez zmian kolejnosci.
- **Stan miedzy kampaniami:** kazda nowa pamiec (LineKey, cele w zasiegu, Needs, profil, ksiega XP, liczniki) czyszczona w `Reset` (R8). Nowe sekcje zapisu (v2): jedna - `arm_e8_questpay` (E8/D1, kwota zamrozona przy przyjeciu; z wersja, stary zapis bez niej = liczone przy wyplacie). E8/G1 rozwiazuje wedlug stanu (brak drogi), nie czasu.
- **Martwy kod ozywiany:**
  - E8/Q - petla AI CrossingLaw (licznik E7/B; po `AllLordParties`, koszt Q-2);
  - (v2: E8/M skreslone - sluchacze po BK biegna juz dzis, finalizer BKROT);
  - E8/R - monopole (nowy wzor bierze mniej niz stary);
  - E8/W - amunicja AI (CIEN).
  Wylaczniki gaszace funkcje: zaden nie przybywa. E8/S zdejmuje dwa istniejace (klimat, HideoutPurge).

### 5.3 Cudzy kod (sprawdzone w rozpoznaniach; przy paczce IL w 1.4.8)

| Cel | Mod | Ryzyko |
|---|---|---|
| `OverrideOnAgentShootMissile.Postfix` | RBM | nazwa klasy zagniezdzonej moze sie zmienic - "nie znaleziono" |
| `OnFoodConsumed`, `OnTraverseTerrain`, `OnAIPartiesTravel` | gra (NavalDLC przekazuje) | prefiks na Default - sprawdzic, czy NavalDLC nie liczy wlasnej wersji |
| `ProcessAgentEquipment`, `EveryoneCampaignBehavior.OnMapEventEnded`, `DistributePlayerSimulationLoot`, `RecruitmentPatch`, `TavernMercenaryEquipmentPatch`, `TownMenuMercenaryEquipmentPatch` | DTE | przy aktualizacji DTE - linie "wpiete" |
| `OnDonateScreenClosed`, `TryCreateBattlefieldRemnant` | Spoils of War | prywatne pole `_donateScreenRoster` |
| `CorrectPlayerEducation`, `OnBuyBookConsequence`, `ConvertTroopsMercenaries`, `Feast.Automanage`, karawany, `BKLordPropertyBehavior` | BK | lambda w BK - nazwa kompilatora `<...>b__`; szukac po IL |
| `BKCaravansHourlyThrottlePatch.Prefix` | BKROTPatch | v2: postfiks na metodzie prefiksu BKROT (priorytet Harmony nie dziala - wynik bool kolejnego prefiksu nadpisuje runOriginal); BKROT moze zmienic nazwe/sygnature - "nie znaleziono" |
| `EducationManagerCorrectPlayerEducationPatch` (Prefix + Finalizer) | BKROTPatch | v2: nasz prefiks First (jesli potrzebny) omija prefiks BKROT, finalizer BKROT zostaje |
| `CraftingOrder`, `CraftingCampaignBehavior`, zadania (`Issues`), `SellPrisonersAction`, `DefaultBribeCalculationModel`, `VillageHostileActionCampaignBehavior`, `ChangeOwnerOfWorkshopAction` | gra | transpilery na stalych i `null` - liczyc trafienia w linii |
| golibroda, karczma, zadania, zaklady turniejowe, odbijanie wieznia, gra planszowa, arena | SandBox | jw. |
| `TXPManager`, `GivePrizeToWinnerPrefix` | TournamentsXPanded, RBM | loader TXP rzuca 2 wyjatki przy starcie (nieszkodliwe) - latka po zaladowaniu; kolejnosc z RealPayer (High) i RBMTournament (Normal) bez drugiego platnika (v2) |
| `better_economy_settings.xml` | BEE | tylko skrypt; plik Jeffa z kopia .bak |

### 5.4 Gra Jeffa

- Wszystko poza E8/T (8.10) dziala tez na starym zapisie. Stary zapis wczytuje sie: zlecenia kowalskie przeliczone przy wczytaniu, zadania w toku zmieniaja tylko platnika (bez sekcji `arm_e8_questpay` kwota liczona przy wyplacie - v2).
- Pulapka MCM: domyslnych zapisanych w J nie zmieniamy. `WarExpectationEnabled` nie ma w J, wiec zadziala nowa domyslna false. `CrossingLawAi` true w J ozywi petle (zgodnie z jego ustawieniem).
  `CampBattlePropsEnabled` false - wpis przy wgraniu (E7/O2, nie E8).
- Plik BEE (E8/E) i plik Diplomacy (decyzja 3 etapu 7) - przy wgraniu koncowym, gra zamknieta, kopia .bak.
- Logi: E8/O6 nie gubi linii przy CTD. Jeff moze czytac log w trakcie gry (`FileShare.ReadWrite`).

---

## 6. Pytania do Jeffa (tylko kierunek; liczby wybiera projekt)

1. **Trzecia kapitana z kazdej zdobyczy?** Dzis wraki i cala zdobyta zbrojownia oraz tabor pokonanych ida w 100% do Ciebie, a trzecia (33%) obejmuje tylko sprzet z cial powalonych przez Twoich ludzi.
   Rada: **TAK** - wraki wedlug zabojcy, a zbrojownia i tabor pokonanych z ta sama trzecia (zolnierz oddaje kapitanowi trzecia czesc kazdej zdobyczy).
2. **Konie wedlug kategorii?** Zwykly kon t3 x0.4 (rouncey 160-480 zamiast ok. 1 000), rumak t4 x1.5 (kon zbrojnego). Bilans zakupow lordow ok. 0. Rada: **TAK**.
3. **Karawany BK w miescie co godzine?** Dzis latka BKROT pozwala im decydowac raz na dobe, wiec stoja w kazdym miescie ok. 3 doby zamiast ok. 3 godzin. Bedzie wiecej handlu.
   Rada: **TAK** (dlawik zostaje w drodze).
4. **Klany najemnikow ROT:** koniec 3-4 ludzi z niczego dziennie na druzyne bez sluzby i koniec wyrzucania wodza z jego druzyny? Rada: **TAK** (jak dla klanu Spoils).
5. **Welna z dodatkowych cykli uboju:** pelna sztuka jak w recepturze gry czy fizyka - ok. 0.15 sztuki runa z owcy? Rada: **0.15**.
6. **Milicja z niczego przy "Donate to town" (Spoils):** dzis +0.4 milicjanta za kazda oddana sztuke, a ludzie biora sie znikad. Rada: **wyciac milicje z daru**. Bezpieczenstwo zostaje
   (to nie zasob), sztuki ida do zalogi.
7. **"Kon bez uprzezy" (8.10):** czy chodzilo o start postaci (StartKit zamienia konia bez sprawdzenia, czy pasuje do uprzezy)? Rada: **tak, robimy to**. Jesli chodzilo o co innego - napisz.
8. **N z PLAN - kuznia: wybor klasy zbroi czy filtr?** Nie znalezlismy zgloszenia z opisem. Rada: **zostaje filtr ForgeView** (dziala). Nowy wybor tylko, jesli podasz przyklad.
9. **O z PLAN - pasek gotowosci zbrojowni DTE:** sypie dzis 1 wyjatkiem na sesje (bylo 604). Rada: **NIE wylaczac**. Poprawiamy tylko falszywa linie w logu.
10. **Lzejsze logi w Twojej grze** (drogie ksiegi i pomiary tylko w autotescie): ok. 0.25-0.3 s na dobe mniej, ale mniej danych do szukania bledow. Rada: **NIE teraz** - wrocimy w Z.4.
    Szybszy zapis bez utraty linii (E8/O6) robimy od razu.
11. **Liczba partii na swiecie** (najwiekszy koszt czasu doby: 2 319 -> 5 201 partii w 120 dob; nowe bandy 8 063, konwoje BK 3 031) - to decyzja o rozgrywce, nie optymalizacja.
    Rada: **nie w etapie 8**. Po tescie pokazemy pomiar `-Profile` (gra / ROT / BK / nasze), a wtedy zdecydujesz, czy w ogole.

---

## 7. Odlozone / odrzucone (z powodem)

| Pozycja | Decyzja | Powod |
|---|---|---|
| 8.5a surowce a cena broni (wagi skladnikow `MaterialIndex`) | ODRZUCONE | indeks juz dziala (L:35865), roznica +-2 pp ceny; historycznie material to 10-25% ceny - mala reakcja jest prawdziwa (RB8 2.1); "nie dodawac przy okazji" (CLAUDE.md 8 pkt 3) |
| 8.12c wies bez wozu | ZAMKNIETE dowodem | regula gry: nowy woz tylko przy pelnym magazynie i stanie Normal (G VillagerCampaignBehavior.cs:122-157); 18 z 22 zatkanych to wsie spladrowane albo najezdzane; prog P3-2 |
| 8.12d wozy nie widza karawan | ODRZUCONE | usprawnienie, nie blad - nic nie powstaje z niczego; jesli test pokaze "KUPNO STOI" przy wozach i karawanach razem, osobny krok |
| 8.13a pozostale bledy startowe (BKROT A5, BK A7 klasy 1-6, TournamentsXPanded, MonoMod przy klimacie) | ODRZUCONE (wpis do ERRORS) | brak wplywu na gre; latki klimatu 25/25 wpiete |
| BK `AiDecisionTracePatches` SetMove* (ERRORS A7 3-5, SP 16:35 pkt 4) | ODRZUCONE | to tylko sledzenie decyzji BK i jego watchdog; straznicy BK w innych klasach dzialaja; nasza bramka snu E7/O1 (SleepGate) jest od nich niezalezna (RC8 9) |
| atuty BK z klasy 7 (B10b) | bez zmian | decyzja Jeffa (ERRORS A7) - zmienilyby wplyw i slawe AI w calym swiecie |
| 8.14 "sol i piwo jako potrzeby" w BEE | ODRZUCONE | lista w kodzie BEE, przy BK bez skutku; u nas juz potrzeba wsi (K:TownFolk.Live.cs:42) |
| LAWA 4 (stawka godzinowa BK przy wylaczonym `ForgeOneClock`), 9 (sprzedaz domu przy wylaczonym `HistoricalTownRates`), 11 (rzut dniowki w menu pracy) | ODRZUCONE | dotycza wylaczonych kluczy albo kosmetyki (RB8 3.4) |
| LAWA 12-13 (`RecruitCost` i kon) | SPRAWDZIC w E8/B | po 160 prawdopodobnie martwe - bez zmiany, jesli martwe |
| `TroopSelfMend` naprawia tez lup ponad potrzebe (RC8 poz. 24) | ZOSTAJE | Twoj material, Twoj wybor; zmiana tylko na Twoje slowo |
| zuzycie zalog w pokoju (rdza, cwiczenia) | ODRZUCONE | nowa mechanika (RA8 5.1) |
| ksiega dodatnich stanow w zbrojowniach AI | ODLOZONE | wieksza zmiana; E8/Z po prostu nie kupuje dodatnich stanow |
| Z13 zwiad noca | ODRZUCONE | nowa mechanika (a13), tylko na slowo Jeffa |
| zapis przerwanego przeszukania kryjowki | ODRZUCONE | rzadkie, kasa kryjowki schodzi dopiero przy przeszukaniu - nic nie ginie (RC8 2.1) |
| `HideoutPurge` - SwitchToMenu z tiku menu czekania (CH:384) | ODRZUCONE | to wzorzec gry (G PlayerArmyWaitBehavior.cs:189); zakaz CLAUDE.md dotyczy opcji menu; test 8.16 punkt 22 |
| RememberOrder bez drogi i portu (CH:452) | POMIAR (P3) | kod tylko przy rozjezdzie w tescie |
| dwie definicje "w uzyciu" (NeedBuckets / FitBest), zastepcza sztuka `AiAnyMeleeWhenShort` (CH:560 pkt 2-3) | czesciowo | E8/W bierze "noszone" z N1 do zuzycia; reszta - refaktor bez dowodu skutku |
| ksiega E6/0 bez zamowien zamkow w drodze i `CastleMarket` (CH:658) | Z.4 | tylko log; do etapu 8 tylko, gdyby prog `sprawdz_logi` z niej korzystal (nie korzysta) |
| zasieg kupcow w linii prostej, nie droga (CH:658) | ODRZUCONE | zmiana handlu bez bledu |
| kosmetyka i komentarze: `VilHalfempty`, nazwa najezdzcy zamrozona w dymku, `bearded_god`, `{=!}Forest`, `WorldPace` 0-4%, komentarze "168 dni", log `StartStock`, `TownCrafts` naglowek, "Las wsi (126)", "BK stadniny", `AiNightCamp` czlonkowie armii (CH:166, :226, :196/:294, :416, :384, :634, :683, :612, :587, :483) | Z.4 / Z.5 / Z.7 | bez wplywu na gre |
| liczba partii na swiecie | poza 8.17 | pytanie 11 |
| przyspieszanie cudzego kodu (AI lordow, ROT malzenstwa, znaczki) | Z.3 | dopiero po `-Profile` i dekompilacji; wysokie ryzyko AI (RD8 4) |
| puchnace polki (13 909 szt. bez odbiorcy) | obserwacja | przyczyna ekonomiczna (cennik, zasieg wywozu); E8/C moze ja zmniejszyc (helmy, luki) - INFO w etap8_c |
| rozmiar zapisu (26-31 MB) i czas autozapisu | Z.4 | niezmierzone; ewentualnie czas SyncData w E8/0 (tylko licznik) |
| milicja z daru Spoils | pytanie 6 | ludzie z niczego - decyzja Jeffa |
| jency i nagrody turniejow AI w E8/D2 (v1) | SKRESLONE (v2) | juz zamkniete przez RealPayer E3/1 (164a-1, `TownPaysRewards` true) - druga latka bylaby druga regula platnika |
| rozwiazywanie karawan po 14 dobach postoju (v1) | ODRZUCONE (v2) | prawo na zegar (SP 12.10 00:20); zostaje rozwiazanie przy dowiedzionym braku drogi |
| jedna rezerwa kas dla starych uzyc: 14 plikow Armoury i RC czyta `TownRentFloorGold` 20 000 (paser, CaravanBulk, MarketRoad, ...), RealPayer i nowe wyplaty E8 - `Seal31.Spare` | Z.4 (refaktor) | krytyka W4: nowe wyplaty E8 tylko `Seal31.Spare`; zmiana 14 starych miejsc bez dowodu szkody = "przy okazji" (CLAUDE.md 8 pkt 3) - osobny krok z pomiarem |

---

## 8. Krytyka - co przyjeto/odrzucono (v2, 12.10)

Dwa przeglady: **P1** logika/decyzje/ekonomia (13 uwag: K1, W1-W5, D1-D7), **P2** kod/latki/koszt (12 uwag: K2-1..K2-12). Kazda sprawdzona w kodzie, dekompilacji albo logu;
pelne rozliczenie z dowodami - `docs\etap8\KRYTYKA-E8.md`. Odrzuconych w calosci: 0. Przyjetych czesciowo: 2 (W4 - zakres, D3 - prog bardy t6).

| Uwaga | Waga | Werdykt | Gdzie w v2 |
|---|---|---|---|
| K1 D2 dubluje RealPayer (jency, turnieje AI) | krytyczne | PRZYJETE | R1, E8/D2 (wiersz "poza E8"), rozdz. 0 pkt 3, rozdz. 1, 5.1, 8.16 p. 12/15, rozdz. 7 |
| W1 D1 tylko "z niczego -> gracz" (RevenueFarming, wyplaty gracza w nicosc) | wazne | PRZYJETE | E8/D1 (oba kierunki, wyjatek RevenueFarming) |
| W2 czesc wyplat w starej skali | wazne | PRZYJETE | R2, E8/D1 (skala w wrapperze, tabela typow) |
| W3 towar i ludzie w zadaniach, zleceniach, turnieju | wazne | PRZYJETE | R1, E8/D1 (towar), E8/K (bron, zolnierz), E8/D2 (nagroda gracza) |
| W4 GiveGoldAction tnie do calej kasy osady; trzy reguly | wazne | CZESCIOWO | R1 (`Seal31.Spare` dla nowych wyplat), E8/B, E8/RC (`SpareD4`); stare uzycia 20 000 - rozdz. 7 (Z.4) |
| W5 progi ekonomii niemierzalne | wazne | PRZYJETE | rozdz. 4 pkt 5, W-1/W-2, C-4, G1-5, D2-1..3 |
| D1 trzy przeplywy zakladu | drobne | PRZYJETE | E8/D2 (kontrahent - kasa miasta) |
| D2 lapowka przy odbijaniu, gra planszowa, arena | drobne | PRZYJETE | E8/D2 |
| D3 progi siodel i bard ponad CD | drobne | CZESCIOWO | C-1: siodla 24-60 (tol. 90) osobno, bardy t4-5 <= 2 400; t6 <= 2 600, bo CD: "najlepsze wyzej" |
| D4 rozwiazanie karawany po 14 dobach = prawo na zegar | drobne | PRZYJETE | E8/G1 (tylko brak drogi; najpierw BK zwrot ceny) |
| D5 O5 sprzeczne z R9 | drobne | PRZYJETE | R9, E8/O5, rozdz. 4 pkt 4 |
| D6 najemnicy gracza bez dobytku | drobne | PRZYJETE | E8/T (sciezka RecruitKit) |
| D7 "3 000 = rok rycerza" | drobne | PRZYJETE | rozdz. 0 pkt 3 |
| K2-1 priorytet prefiksu nie przebije BKROT | wazne | PRZYJETE | E8/G1 (postfiks na prefiksie BKROT), 5.3 |
| K2-2 diagnoza BK Education bledna | wazne | PRZYJETE | rozdz. 0 pkt 8, rozdz. 1, E8/M, 5.1, 5.2, 8.16 p. 1 |
| K2-3 IsAmmo + Thrown = darmowa naprawa oszczepow | wazne | PRZYJETE | R3, E8/C (`IsMissile`, C-5), 5.1 |
| K2-4 zysk O3 zawyzony | wazne | PRZYJETE | E8/O3, Razem 8.17, rozdz. 0 pkt 2, rozdz. 1 |
| K2-5 Log.Error gubi linie przy otwartym pliku | wazne | PRZYJETE | E8/O6 (slownik pisarzy, O6-3), 5.1 |
| K2-6 DayWage zalezy od miasta | drobne | PRZYJETE | E8/O2 |
| K2-7 G2: prefiks Spoils pomija tez zamiane 1:1 | drobne | PRZYJETE | E8/G2 (dwie galezie) |
| K2-8 licznik postoju zerowany przy wczytaniu | drobne | PRZYJETE | E8/G1 (stan zamiast czasu), 5.2 |
| K2-9 AiTick po MobileParty.All | drobne | PRZYJETE | E8/Q (AllLordParties, spiacy przed SetMove) |
| K2-10 indeks polki - kopia przy zapisie | drobne | PRZYJETE | E8/O1, 5.1 |
| K2-11 zaleznosci O1-O3, watki | drobne | PRZYJETE | rozdz. 3 tabela i watki |
| K2-12 kwota obiecana != wyplacona | drobne | PRZYJETE | R2, E8/D1 (zamrozenie, sekcja `arm_e8_questpay`), 5.2, 5.4 |
