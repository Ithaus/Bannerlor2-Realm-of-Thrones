# ANALIZA AUTOTESTU TOWARY 3 (08.10 06:51, 40 dob, Armoury 5a7074c0 + GT 1337433c + RC 3e04b89b)

## Werdykt sceptyka

TAK z uwagami. Na DLL dll-final-3 (Armoury 5a7074c0, t3-sklad 5ccb0f5) nie ma błędu, złota z niczego, ujemnej kasy ani wyłączonej łatki. Nic nie blokuje wgrania. Analiza ma jednak kilka błędów i przeoczeń (niżej). Najważniejsze: test szedł na starych wartościach trzech pokręteł 127 w Armoury.json, więc tego, co Jeff ustawi przy wgraniu, w grze nikt nie widział. Część oznaczona jako PROBLEM to granica projektu albo przewidziane ryzyko, a nie regresja.

## Dla Jeffa

TOWARY 3: TAK, można wgrać, z uwagami. 40 dni nowej kampanii: zero błędów z naszych modów, gra nie zwolniła (12,4 s na dobę), wioski wyglądają jak v5. Złota z niczego nie ma, żadna kasa miasta nie spadła poniżej zera.

Co poczujesz w grze:
- Najemnik konny z karczmy bierze konia z targu tego miasta. Jeśli koń jest na targu, płacisz za niego i pieniądze idą do kasy miasta. Jeśli nie ma, najemnik przychodzi na swoim koniu bez dopłaty. Lordowie płacą tak samo, więc są trochę biedniejsi (po 40 dniach razem 1-2 mln mniej).
- Koni na targach jest mniej niż dotąd (ok. 7600 zamiast ok. 9300), cena konia normalna (ok. 630).
- Rzemieślnicy w miastach robią filc z wełny, płótno z lnu i skórę ze skór. Wełna i skóry już nie zalegają. Za to brakuje lnu i płótna: ponad połowa miast nie ma ich wcale, bo żołnierze kupują odzież.
- Kowale w miastach naprawiają sprzęt lordów tylko z materiałem z targu. W miastach bez rudy naprawa czeka. Siły armii AI to nie zmienia.
- Warsztaty zbrojne zrobiły ok. 13% mniej sztuk (brakuje im skóry i lnu).
- Żelazny Bank: więcej długu (234 tys.) i 2 bankructwa rodów (Wylde, Errol). Bez tej paczki zwykle było 0.
- Karawany w Pyke dalej stoją tygodniami, tak jak w samej grze.

Czego test nie sprawdził:
- Twojej postaci: kuźnia, ława naprawcza, najemnicy w karczmie, kulawy koń, turnieje, niewola. Autotest stał w mieście z jednym człowiekiem.
- Trzech pokręteł z Armoury.json. Test szedł na Twoich obecnych 45 / 20 / 5, a po wgraniu mają być 35 / 13,33 / 2: kowalstwo łatwiejsze, Twoja zbroja zużywa się 1,5 raza szybciej, złom idzie za 2% wartości zamiast 5%. Tego w grze jeszcze nikt nie widział.

## Uwagi sceptyka

SPRAWDZONE SAMODZIELNIE - ZGADZA SIĘ
- sprawdz_logi.py --grupa 3t uruchomiłem jeszcze raz: kod 0, wynik identyczny z sprawdz-3t.txt (OK 35, UWAGA 2, ERROR 0, Exception 0, potknięcia 0). W logu Armoury: ERROR, WARN, NIECZYNN, WYLACZON i "NIE wpiet" po 0. Jedyne "BRAK" to "BRAK WOZU" w diagnozie wsi.
- Logi Logs\2026-10-08_06-51-52\* (11 plików): ERROR/Exception 0. RealisticCaptivity-…06-51-52.log czysty, "TownWageLink PODPIETY". GrandTourney.log (sesja 06:51) 0 błędów.
- CrashScribe: 8 sygnatur, wszystkie przed dobą 1, w tym BK EducationManager 06:53:11 i DTE/UIExtenderEx 06:53:14. Zawieszenie 07:05 przyszło po QuitGame, poza kampanią. Takie samo jest w sesjach 12:37 i 14:18.
- 156: linia 139 "w 76 kategoriach" bez "horse /25" (w 14:18 było 77 kategorii z "horse /25"). Linia 143: goose i chicken [horse] poza przelicznikiem.
- 157, liczby co do grosza. 40 linii, z czego 2 (108839, 108871) nie mają "razem … do notabli", więc prosty regex łapie tylko 38. Sumy: koń z półki 2605, miastom 2 538 816, własny koń 1900, zwrot 25 093, "bez targu" 0, gracz 0/0/0. Werbunek: 4 776 378 / 11 190 = 426.8, bez koni 200, żadna doba ujemna.
- Bank: dług 234 443, 15 dłużników, bankructwa Wylde (l. 6135) i Errol (l. 6324). Pierwsza pożyczka 108859. Drobiazg: linii "pozycza" jest 45, ale doby sumują 43 pożyczki = 253 758.
- Kasy miast: minimum 9812, 2 doby < 20 000, w dobie 40 minimum 33 538. Ujemnej kasy nie ma nigdzie. Wsie poniżej 1000 zł (ok. 500) jak w 12:37 i 14:18.
- Wioski: 5ccb0f5 MapVillagesView.cs = 8518996 (git diff pusty). Para windmill-applewick obejrzana, wygląda jak v5. W grze z powrotem jest b0e62e1f.

BŁĘDY W ANALIZIE
1. "W 22 z 40 dób czekających więcej niż naprawionych": w rzeczywistości 35 z 40 (np. doba 33: 378/1098, doba 20: 392/1141). Analiza zaniżyła skalę.
2. "67% prób naprawy" jest zawyżone jako miara. AiWear.MendWithMaterial (AiWear.cs:315-345) liczy "czeka" dla każdej partii co dobę, cały stos, bez sufitu godzin kowala. Ta sama sztuka liczy się każdego dnia postoju, więc 20 655 to nie liczba różnych sztuk. Metal to 17 539 z 20 655 (85% sumy, ale 78% sumy rodzajów).
3. "Obitego sprzętu 1.6-1.9x więcej" to porównanie tylko z 12:37 i 14:18. Wobec 10:40 (9924) i 09:11 (9058) jest 1.2-1.3x. Część przyrostu wynika z projektu 158: wraku AI nikt nie naprawia (AiWear.cs:238, Mendable = !IsWreck), więc wrak zostaje w "obitych razem" na zawsze. Log nie liczy wraków, więc nie da się oddzielić braku metalu od wraków.
4. "Zator lnu u karawan" to złe odczytanie. Karawany wiozą w T3 MNIEJ lnu (2881) niż we wszystkich 5 biegach bez T3 (3189-4178), a "brakuje" to 412 wobec 1229-1289. Nowe jest co innego: 52 miasta bez lnu (wcześniej 34-39) i 55 bez płótna (wcześniej 24-27). To zużycie przez 148 i 150 (ryzyko 6 z opisu 150).
5. Cena konia 630 nie jest "w paśmie". Jest niższa od każdego biegu bez T3 (654-711), o 4-11%. Wyjaśnia to 155 (kara BK raz). Szkody brak.
6. Bank porównany tylko z 12:37 i 14:18. W 5 biegach bez T3 dług w dobie 40 wynosi 96-198 tys. Bankructwa: 0/0/0/0 bez 143, 1 w 14:18, 2 w T3. Errol upada w obu biegach z płatnym koniem najemnika, więc związek z kosztem najemników jest prawdopodobny (głowy rodów -1.2..-1.8 mln wobec biegów bez T3). To jednak jedna próbka na konfigurację, a wojny są losowe.

POTWIERDZONE WOBEC WSZYSTKICH 5 BIEGÓW
- Warsztaty zbrojne: 26 931 sztuk wobec 29 732-31 299 (-9..-14%). Brak skóry 1821 (wcześniej 590-1137), brak lnu i wełny 4061 (wcześniej 1741-2503), drewna 14 917 (12.5-13.1 tys.).
- KUPNO STOI: w T3 len 10 dób i skóry surowe 7. Wełna (26-28 dób) i skóra (11-22 doby) zniknęły we wszystkich porównaniach.

PRZEOCZONE
A. Test NIE szedł na docelowych ustawieniach 127. Armoury.json Jeffa to dziś 45 / 20.0 / 5 (plik z 05.10). "Skup sprzętu" pokazuje "podłoga 5%" 40 razy na 40 dób, a po wgraniu ma być 35 / 13.33 / 2. DurabilityPerArmorPoint (ArmorPool) i SmithingSkillPerTier dotyczą tylko gracza. MinSellPercentOfValue dotyczy też AI: "inne partie" sprzedały 16 835 sztuk za 2.06 mln, z czego 12 805 na podłodze (lordowie 4.94 mln, sakiewki 0.39 mln). Przy 2% te wypłaty spadną. W grze tego nie sprawdzono.
B. Pieniądz świata: -16.02 mln wobec -13.31..-14.52 w 4 biegach bez stosu ławy (14:18: -16.85). Zmierzone ujścia są o 2.3-2.9 mln większe: GiveGold w nicość po odjęciu LevyGold +1.1-1.4 mln, rozliczenia rodów na minus +0.4-0.9 mln. To ubytek (nie źródło) odziedziczony po stosie ławy i nadal niewyjaśniony.
C. Pętla karawany z Tarth: przekierowania "dom" w dobach 25 / 26 / 40 (7 / 30 / 37 razy). Odrzuceń w ocenie było wtedy 3077 / 7677 / 8179, wobec ok. 2000 na dobę. Drobne.

DO DECYZJI, NIE BLOKUJE
- Pyke: 14 karawan stoi do 39 dób. Opis 154 to przewiduje ("karawany w Pyke dalej czekają, jak w grze"), choć lista kontrolna recenzji mówiła o "kilku dobach".
- Naprawy AI czekają na metal (39 miast bez rudy, sztab 0). Siły AI to nie zmienia (DTE nie zna modyfikatorów).
- Koń najemnika x mnożnik kupującego: średnio 975 zł za konia, w dobie 1 1372 zł.
- Konie od notabli: 164 konie, 120 927 zł.
- Parser sprawdz_logi.py nie zna linii 312 "Wozy w miastach: brama…".
- Gracza nie przećwiczono: kuźnia, ława, TroopSelfMend, karczma, Pick a piece, kulawy koń, turnieje, niewola.

Moje skrypty w scratchpadzie tej sesji (fa2fd7a6…\scratchpad): sumy.py, zuz.py, bilans.py, kupno.py, warsz.py, wyspy.py, sprawdz-moje.txt, pelny.txt. Nic nie wgrane, nic nie zacommitowane.

## Narzedzie sprawdz_logi --grupa 3t

sprawdz_logi.py Armoury-2026-10-08_06-51-52.log --grupa 3t: kod wyjscia 0. Wynik: OK 35, UWAGA 2, BRAK DANYCH 0. W calym logu ERROR 0, Exception 0, potkniecia 0, 1 alarm, 2 uwagi. Pelne wyjscie jest w analiza-autotestu\sprawdz-3t.txt.

WARUNKI TESTU OK:
- nowa kampania (linia 121 "Wyrzutki: pula poczatkowa");
- 40 pelnych dob (dni 108836-108875), jedna kampania;
- najwyzsze ogniwo 150, komplet ogniw grupy (121..150);
- zadna linia startowa nie ma BRAK / NIE wpieta / WYLACZONE.

Wszystkie ogniwa 121-150 maja "CO SPRAWDZIC" OK. Linie dnia ogniw 128, 129 (x2), 130, 133, 134, 146 (x2), 147, 148 i 150 sa co dobe, 40/40, bez linii poza wzorcem.

UWAGA 1 (parser, nie gra): "nieznany rodzaj linii: Wozy w miastach (inny rodzaj linii)".
- Zrodlo: jednorazowa linia startowa 312: "Wozy w miastach: brama poza pamiecia drog ROT (...): zadna (0 z 799 osad); latka straznika BK CZYNNA".
- Wzorzec Temat 'Wozy w miastach' (sprawdz_logi.py:1585) zna tylko r'^dzien \d+ - straznik'.
- Ta sama UWAGA jest w tescie narzedzia na logu 12:37 (naprawa\narzedzia\test\nowy-2026-10-07_12-37-02.txt, linia 205).
- Tresc linii to dobra wiadomosc: pamiec drog bez dziur (131 dziala).
- Wniosek: poprawic parser. Kontrole 130 ida dalej po liniach dnia (40/40).

UWAGA 2 (alarm calego logu, linia 6664): "Karawany (stan): len - KUPNO STOI przez 6 dob z rzedu (prog 3)".
- Seria trwa do konca testu (108870-108875). Lacznie KUPNO STOI: len 10 dob, skory surowe 7 dob.
- Logi bez T3: 12:37 welna 28 / skora 22 / skory 12 / plotno 4; 14:18 welna 26 / skory 29 / skora 20 / plotno 8.
- Przesyt welny i skory zniknal (148 + 150). Nowy jest zator lnu: doba 40 len "bez towaru 52 miast", "w jukach karawan 2881", wjazdy z lnem 18, w tym do miasta z brakiem tylko 1 (linie 7618, 7620).

Uwagi narzedzia (nie UWAGA grupy):
- linia 5716 (dzien 108866): "Dowoz (wozy): cena rosla 1" - jeden raz. W 12:37, 14:18 i 10:40 jest 0 przez 40 dob, wiec to nowe.
- linia 7653: "Zold: najwieksza sakiewka rosnie 7 dob z rzedu 66.0k -> 101.4k" (lord_1_14_party_1). Na dobe 40: 12:37 53k, 14:18 79k, 10:40 127k - w pasmie.

## Lista kontrolna

START (linie logu Armoury-2026-10-08_06-51-52.log)
- OK. Wszystkie linie startowe z WYNIK sekcja 9 sa obecne:
  - SpoilsSeal "| wszystkie sciezki wpiete" (101);
  - SpoilsCompany "| wszystkie drogi wpiete" (102);
  - Zbroja z kuzni "CZYNNE ... | wszystko wpiete" (82);
  - GoodsLedger (146) CZYNNA (114);
  - Rzemieslnicy BK (147) WLACZONA (34);
  - Rzemioslo miasta (148) WLACZONE w 97 miastach "x poziom plac" / "zastapione rzemioslem" (154);
  - Odziez wojska (150) WLACZONA (173);
  - SmithAudit XP z sufitem (16);
  - SlowHealing "gracz 50%, AI 100% (2/2 metod)" (109);
  - Wioski "...v4 ... 4000 people per village" crc32 89612CA7 (177).
  Zadna linia startowa nie ma BRAK / NIECZYNNE / WYLACZONE.
- OK. Nowe linie z WYNIK sekcja 8:
  - "IslandRoads: wpiete ... (CZYNNE)" (26);
  - "Kara handlowa BK: dubel ... ZDJETY" (6) i "x5.0 (raz; zdjety dubel z BKROTPatch.Models.BKROTPriceModel)" (157);
  - "Skup sprzetu: ... SellItemsAction wpiety" (7);
  - "HistoricalPrices: popyt miast przeliczony ... w 76 kategoriach" bez "horse" (139; jest cow /1.8, hog /2, sheep /4.7, horse_equipment*; 14:18 mialo 77 kategorii z horse /25);
  - "zywy inwentarz [...] poza przelicznikiem ...: goose [horse], chicken [horse]" (143);
  - "horse (moneta popytu /1): goose 4 -> 50, chicken 1 -> 50" (144);
  - "RecruitCost: ... MercHorseFromShelf, poprawka 157 - zakup gracza wpiety w 2/2 metodach" (66).
  ERROR (w tym BkPenaltyOnce / SlotWreck / PlayerBuyPostfix) = 0 w Armoury, w Logs\2026-10-08_06-51-52\* i w RealisticCaptivity-2026-10-08_06-51-52.log; GrandTourney.log 0.

CO DOBE
- Konie rekrutow (157) - OK wedle regul 157, liczby nizej w "Zloto za najemnikow". Linia jest 40/40 (pierwsza 358, ostatnia 7664). Suma 40 dob:
  - najemnicy konni AI: kon z polki 2605 szt., miastom za konie 2 538 816 zl (srednio 975 zl za konia; doba 1: 73 konie, srednio 1372; doba 40: 44, srednio 737);
  - z wlasnym koniem bez doplaty 1900;
  - nadplata zwrocona 25 093 zl;
  - "bez targu (stala gry)" 0 przez 40 dob;
  - gracz: 0 / 0 / 0 (nie najmowal);
  - ochotnicy konni od notabli: 164 konie, 120 927 zl do notabli (to sprawa "157 - poza zakresem" z WYNIK 7.2 - widoczna w grze).
- Zloto za najemnikow - DO SPRAWDZENIA. Oczekiwane ok. 290 zl na najemnika, jest 426.8 (4 776 378 zl / 11 190 z karczmy). W dekadach: 415 / 453 / 429 / 407 (14:18: 613 / 440 / 422 / 379). Doba 1: 174 338 (12:37: 124 162; 14:18: 364 851).
  - Bez zaplaty za konie wychodzi 200.0 zl na najemnika ((4 776 378 - 2 538 816) / 11 190). Nadwyzka ponad 290 to w calosci prawdziwe konie zdjete z polek, czyli zgodnie z litera 157 ("wyzej tylko za konie zdjete z polek").
  - Kon najemnika (975 zl) kosztuje jednak 2.5 x wiecej niz dawna stala gry w koszcie. Dla lordow to +1.41 mln zl przez 40 dob wobec 12:37.
- Wyspy i drogi - CZESCIOWO OK.
  - Linia 40/40, "wpiete: ocena miast BK TAK, wybor celu BK TAK, uczta TAK, gentry TAK, droga w strazniku TAK", potkniecia 0.
  - Lordowie: odrzucen 0 przez 40 dob; "lordowie w osadzie z AI wylaczonym ... najdluzej -" codziennie; "stoja w osadzie z odrzuceniem" lordow 0 / karawan 0 codziennie. W 12:37 lord w Pebbleton stal 15.6 doby, czyli OK.
  - Lord Hewett's Town: nie wystepuje w liscie stojacych, OK.
  - PROBLEM Pyke: "karawany bez statkow w tym samym miescie od 7+ dob" rosnie z 13 (doba 9) do 32-42; Pyke 4 -> 14 od doby 26 do konca, "najdluzej 39.0 dob - Pyke" (7616; caly test). Kto (doba 40): kupiec - dom tutaj 13, kupiec - dom inne miasto 10, lord - dom wies 7, lord - dom tutaj 2.
  - Wyspy z jednym miastem "czekaja": 5 [Pebbleton 3, Lonely Light 1, Blacktyde 1], najdluzej 36.0 dob - Lonely Light (dom World's Edge). Znane, poza 154.
  - Odrzucenia karawan tylko w 3 dobach: 7 (108860), 30 (108861), 37 (108875), wszystkie "dom". Przyklad: "karawana w polu -> Cape Wrath => Evenfall Hall (dom)" powtorzony; "odrzucone w ocenie" skacze do 7677 i 8179. Wyglada na petle jednej karawany z Tarth.
- Wozy w miastach - OK.
  - Linia dnia 40/40, odrzucen wozom 0.
  - "wozy wsi w miastach teraz 18-21 (od wczoraj lub dluzej 0; najdluzej 0 dob)" - maksimum z 40 dob to 0 dob.
  - Bezpiecznik 0, potkniecia 0.
  - "sciana bez wpisu w pamieci drog 0" przez 40 dob.
- Zatkane wsie - OK.
  - Doba 40: 13 z 571 (zamkowe 5, miejskie 8); Normal 7, spladrowane 5; BRAK WOZU 4; w miescie 0; koszt 8.6 ms (7602).
  - Doby 10 / 20 / 30: 9 / 7 / 10.
  - Wobec 12:37: 14 (Normal 3); 10:40: 19.
- Skup sprzetu (doba) - OK.
  - Linia 40/40 z "[BkTradePenaltyOnce True, SellPriceByCondition True, sufit 10% ceny nowej, podloga 5%]".
  - Doba 40 (7663): lordowie AI 156 szt. = 38.4% wartosci (na podlodze 7); inne partie 1825 szt. = 57.1% (podloga 1642); sakiewki ludzi 4073 szt. = 7.7% (sufit 134); notable 4 szt.
  - Doba 1: lordowie 198 szt. za 616 781 zl = 59.6% wartosci (sprzedaz kompletow startowych).
- Zuzycie AI i material napraw - PROBLEM.
  - Naprawione w miastach przez 40 dob: 10 209 szt. za 76 560 zl (7.5 zl/szt.).
  - "Czeka na material" przez 40 dob: 20 655 szt., czyli 67% prob naprawy (czekajace / (naprawione + czekajace)); w tym metal 85%. To NIE jest ulamek.
  - Doba 40 (7475): naprawione 259, czeka 632 (metal 571, skora 106, plotno 3).
  - W 22 z 40 dob czekajacych jest wiecej niz naprawionych (np. doba 33: 1098 wobec 378; doba 20: 1141 wobec 392).
  - Obitego sprzetu u AI stale przybywa: doba 10 2012 -> doba 20 4465 -> doba 30 6965 -> doba 40 12 101 szt. w 344 partiach (12:37: 7449; 14:18: 6488).
  - Material z targu przez 40 dob: 14 402 zl w 860 zleceniach. W ksiedze towarow "naprawy kowali miasta (135)" na rudzie to 3 ladunki na dobe, a na polkach jest rudy 2717 ladunkow (39 miast bez rudy), sztabek ironIngot1-6 0.
  - Wobec 14:18 (1395 na dobe 40): doba 40 ma 259, srednia dob 31-40 470 wobec 477, suma -21%.
- Wraki jako zlom - BRAK DANYCH.
  - Linia "Zuzycie AI" nie ma licznika wrakow.
  - "Spoils - naprawa u kwatermistrza": "z polek zdjeto: nic (w tym wrakow na zlom 0)" 40/40 - kwatermistrza nie bylo.
  - Linie "Naprawa lupow / Naprawa zbrojowni wojska / Lawa naprawcza ... wraki na zlom" nie wystepuja - gracz nie byl u kowala.
- Towary (bilans) - OK.
  - "ruda ZGODNA" 39/39 i "drewno ZGODNA" 39/39; doba 1: "od jutra (pierwszy pomiar)".
  - Ramka "naprawy kowali miasta" jest co dobe (3-45).
  - Jednorazowo "bez wyjasnienia +7309 (grain +7262)" w dobie 108858 (linia 4394). Reszta dob od -728 do +946.
- Rzemioslo miasta (148) - OK.
  - Linia 40/40, potkniecia 0.
  - Suma 40 dob: plotno 1722, filc 3695, skora 2931.
  - Doba 40 (7476): 96 miast; rece 37% zajete; polki: len 5625 (indeks 1.06), plotno 730 (indeks 5.00), welna 3233, filc 576, skory 4892, skora 693.
  - Koniec przerobu len -> plotno: 22 bez zysku / 58 bez lnu / 16 rak.
- Rzemieslnicy BK (147) - OK. Potkniecia 0; doba 40: dopisal 2208, zdjetych 439, do kas miast +8249 / +7005.
- Odziez wojska (150) - DO WIADOMOSCI (niedobor).
  - Linia 40/40, potkniecia 0.
  - Zakupy przez 40 dob: partie 268 785 zl (sakiewki ludzi -> kasy miast), zamki 123 948 zl. "Sakiewka ludzi: na odziez wojska (150)" przez 40 dob: 267 345.
  - "Czeka" (skora / sukno / plotno) rosnie przez caly test: doba 10 313 / 493 / 360 (164 partie), doba 20 548 / 925 / 756, doba 30 783 / 1507 / 1243, doba 40 1128 / 2054 / 1693 (687 partii i zalog).
  - Wizyt bez towaru na polce 63 na dobe (doby 20 i 40). Plotno: indeks mediana 5.00, bez towaru 55 miast, za pierwsza sztuke 598 d.
  - To przewidziany niedobor plotna i lnu (150, ryzyko 6).
  - Zaopatrzenie BK w tekstylia: potrzeba wyzerowana.
- Spoils of War (128) - OK. "wyplacono z niczego 0 zl" 280/280 wystapien (7 na linie x 40); "Spoils sprzedal z niczego: 0 szt." 40/40; klan najemnikow "DALO Z NICZEGO" 0 przez 40/40.
- TroopSelfMend - nie wystapil. Gracz stal z 1 czlowiekiem w The Eyrie, wiec nie bylo czego naprawiac.
- Czas doby - OK. 12.4 s srednio (39 pelnych dob). Dekady: 11.0 / 11.4 / 13.1 / 13.9; autotest pisze "ZWALNIA o 26%". Wobec 12:37: 12.6 (dekada 31-40: 14.0); 14:18: 12.8 (14.3); 10:40: 12.2 (13.8). Bez spowolnienia od 157 / 155.
- Bledy - OK. CrashScribe 8 = te same 8 sygnatur startowych co w 06:19 i 14:18 (TournamentsXPanded, BKROTPatch x3, MonoMod przy WesterosClimate, NightRest, BK EducationManager, DTE/UIExtenderEx), wszystkie przed doba 1, "bledy +0" w 40/40 DOBA. Zawieszenie 07:05 to wyjscie z gry po QuitGame: rgl_log_29520 "Managed Interface deleted" -> 0xC0000005, jak w 12:37. Okna zamkniete 2 (BK, Ned Stark).

PO 40 DOBACH
- Konie na polkach - OK z uwaga. Doba 40: 7600 (7495); doby 10 / 20: 3499 / 4915. Oczekiwane ok. 9000, nie 12 953.
  - Blad 144 usuniety, ale jest 18% mniej niz bez T3 (12:37: 9306, 10:40: 9306, 09:11: 8589).
  - Roznice tlumacza 2605 koni zdjetych przez najemnikow (157).
  - Konie na polce przy zakupie Stajni AI: srednio 29.8 (12:37: 38.8; 14:18: 52.8).
- Cena konia - OK. Stajnia AI (Logs\...\zakupy.log): 83 zakupy, 148 koni, 93 203 zl = 630 zl za konia; empire_horse 579.
  - 12:37: 711 (empire 632); 10:40: 654 (610); 14:18: 523 (414).
  - Wynik jest w pasmie sprzed 144 (-5..-11%), nie x0.40.
- Zakupy mieszkancow - OK. Suma 40 dob:
  - kasy miast 12 547 615 (12:37: 12 821 447, -2.1%; 10:40: 13 101 356; 14:18: 10 822 820);
  - kasy zamkow 1 525 923 (12:37: 1 654 456, -7.8%; 09:11: 1 535 751; 14:18: 957 666);
  - utarg wozow wsi 14 323 397 (12:37: 14.06 mln; 14:18: 12.95 mln).
- Drob (goose / chicken) - BRAK DANYCH. Przez 40 dob 0 szt. na polkach ("horse /1: goose ... na polkach 0 szt., chicken ... 0 szt." 40/40), w ksiedze towarow 0 linii. Drobiu w swiecie ROT nie ma, wiec ryzyka 156 nie da sie zmierzyc.
- Zelazny Bank - PROBLEM / DO SPRAWDZENIA. Oczekiwane mniej niz w 14:18 (198 tys. / 1 bankructwo); jest gorzej niz w 14:18.
  - Doba 40 (7661): dluznikow 15 (bankrutow 2), dlug razem 234 443, kapital Banku 4 773 092.
  - Bankructwa: Wylde (dlug 14 898, linia 6135, dzien 108868) i Errol (12 054, linia 6324, dzien 108869).
  - Pierwsza pozyczka dopiero w dobie ok. 23. Dlug w dobie 30: 138 128 (12:37: 52 452; 14:18: 47 181).
  - 45 pozyczek na 253 758 zl. Najwieksi pozyczajacy: Pyke 69 395 i Harlaw 37 623 (Zelazne Wyspy w wojnie z The Vale), Waynwood 25 575, Belmore 23 486, Tarth 19 111.
- Pieniadz swiata - DO WIADOMOSCI. Doba 40: 174 132 843 (7665), zmiana przez 40 dob -16 015 090.
  - 12:37: -14.11 mln; 10:40: -14.52 mln; 12:15: -14.05 mln; 14:18: -16.85 mln. Ubytek stosu lawy zostaje, o 0.84 mln mniejszy niz w 14:18.
  - Bilans 40 dob: ujscia w nicosc 71.12 mln (12:37: 68.83), GiveGoldAction w nicosc 32.94 mln (31.23), reszta [R] -5.83 mln (-6.78).
- Kasy miast ponizej 20 000 - OK. 2 doby: doba 31 min 12 513, doba 32 min 9812 (linie 6144, 6333); doba 33 juz 30 146. "Ponizej progu rent" 1-2 w 4 dobach.
  - Doba 40: razem 7 838 940, min 33 538, mediana 76 153, ponizej 1000 zlota 0.
  - Logi bez T3: 12:15 6 dob (min 9947), 10:40 1, 14:18 1, 12:37 0.
- Kasy zamkow - OK. Doba 40: 3 303 732, min 16 462.
- Gracz (Pick a piece, kulawy kon, najemnicy w karczmie) - nie przecwiczone (autotest stoi w miescie). GrandTourney: TownWageLink nie podpiety (gracz nie organizowal turnieju). RC: "TownWageLink ... PODPIETY".

## Porownanie z autotestami bez T3

Ekonomia: TOWARY 3 (06:51) | 12:37 (autotest 6: n126 + diagnoza, wozy, pamiec drog) | 14:18 (stos lawy n131k). Sumy z 40 dob albo stan w dobie 40. Skrypt: analiza-autotestu\metryki.py; wynik z dodatkowymi kolumnami 10:40, 12:15 i 09:11 w analiza-autotestu\metryki.csv. Logu 08:03 nie ma w Modules\Armoury (jest tylko economy-2026-10-07_08-04-39.csv).

| Metryka | TOWARY 3 | 12:37 | 14:18 |
|---|---|---|---|
| Konie na polkach (Rynek broni "Horse"), doba 10 / 20 / 40 | 3499 / 4915 / 7600 | 4080 / 6532 / 9306 | 4954 / 8028 / 12953 |
| Stajnia AI: zl za konia (empire_horse); koni na polce przy zakupie | 630 (579); 29.8 | 711 (632); 38.8 | 523 (414); 52.8 |
| Zakupy mieszkancow do kas: miasta / zamki | 12.55 mln / 1.53 mln | 12.82 mln / 1.65 mln | 10.82 mln / 0.96 mln |
| Utarg wozow wsi; podatek wsi dla pana | 14.32 mln; 8.18 mln | 14.06 mln; 7.97 mln | 12.95 mln; 7.21 mln |
| Zloto za najemnikow do miast; z karczmy | 4.78 mln; 11 190 | 3.36 mln; 11 774 | 5.21 mln; 10 882 |
| Zloto na najemnika (dekady 1-4) | 427 (415 / 453 / 429 / 407); bez koni z polki 200 | 286 (285 / 290 / 283 / 283) | 479 (613 / 440 / 422 / 379) |
| Konie najemnikow z polki (157) | 2605 szt. = 2.54 mln (975 za konia) | - | - |
| Zloto za ochotnikow do notabli | 1.53 mln | 1.50 mln | 1.49 mln |
| Glowy rodow / pozostali lordowie, doba 40 | 45.92 mln / 11.33 mln | 47.15 mln / 11.60 mln | 44.97 mln / 11.44 mln |
| Rody (economy csv, doba 40): suma kies rodzin; glow < 5000 | 57.28 mln; 16 | 59.18 mln; 14 | 56.73 mln; 11 |
| Zelazny Bank, doba 40: dluznicy / dlug / bankructwa | 15 / 234 443 / 2 | 9 / 96 691 / 0 | 14 / 198 433 / 1 |
| Zelazny Bank: dlug w dobie 30; pozyczki | 138 128; 45 = 253 758 | 52 452; 24 = 115 886 | 47 181; 47 = 218 128 |
| Kasy miast, doba 40: razem / min / mediana | 7.84 mln / 33 538 / 76 153 | 8.43 mln / 23 533 / 81 271 | 7.86 mln / 32 503 / 73 860 |
| Doby z min kasy miasta < 20 000 (najnizsze) | 2 (9812) | 0 (21 194) | 1 (16 723) |
| Kasy zamkow, doba 40 (min) | 3.30 mln (16 462) | 3.38 mln (16 195) | 3.28 mln (14 898) |
| Pieniadz swiata, doba 40 / zmiana przez 40 dob | 174.13 mln / -16.02 mln | 175.35 mln / -14.11 mln | 172.20 mln / -16.85 mln |
| Sakiewki ludzi, doba 40; "zycie w miastach" przez 40 dob | 4.85 mln; 5.22 mln | 3.82 mln; 6.25 mln | 4.55 mln; 5.95 mln |
| Warsztaty zbrojne: sztuk; zl za sztuke; sprzedaz / koszt | 26 931; 20.2; 2.51 | 30 812; 14.1; 3.13 | 31 299; 17.7; 2.52 |
| Warsztaty zbrojne, brak surowca: ruda / skora / len-welna | 34 932 / 1821 / 4061 | 33 787 / 590 / 2402 | 33 913 / 1137 / 1741 |
| Warsztaty towarowe: wynik 40 dob; mediana ceny warsztatu w dobie 40 | +287 225; 13 775 | +315 842; 12 780 | +299 303; 12 953 |
| Budowy oplacone: zl / punkty (zl za punkt) | 984 268 / 30 479 (32.3) | 921 174 / 30 482 (30.2) | 917 409 / 30 217 (30.4) |
| Naprawy AI w miastach: sztuk / zl (zl za sztuke) | 10 209 / 76 560 (7.5) | 17 925 / 169 295 (9.4) | 12 985 / 81 948 (6.3) |
| Naprawy AI: doba 40 / srednio doby 31-40; obitych w dobie 40 | 259 / 470; 12 101 | 712 / 903; 7449 | 1395 / 477; 6488 |
| Czeka na material (suma 40 dob) | 20 655 (67% prob, 85% metal) | - | - |
| PodazPopyt: sztuk / zl | 168 911 / 8.48 mln | 166 854 / 7.82 mln | 159 394 / 6.73 mln |
| Zakupy AI: sztuk / zl | 140 756 / 5.52 mln | 140 845 / 5.64 mln | 136 958 / 5.13 mln |
| Ochotnicy: awanse / cofniete | 4037 / 27 188 | 4080 / 26 957 | 3740 / 27 586 |
| Ruda, doba 40: miasta / razem / miast bez rudy | 2717 / 4561 / 39 | 2426 / 4689 / 38 | 2162 / 4054 / 37 |
| Drewno, doba 40: miasta / razem / miast bez drewna | 11 003 / 20 374 / 15 | 11 900 / 21 007 / 8 | 12 096 / 21 407 / 6 |
| Zatkane wsie, doba 40 | 13 (Dowoz 5 + 8) | 14 (8 + 5) | brak linii diagnozy (Dowoz 28 + 31) |
| KUPNO STOI (dni) | len 10, skory surowe 7 | welna 28, skora 22, skory 12, plotno 4 | welna 26, skory 29, skora 20, plotno 8 |
| Wojsko partii rodow, doba 40; zwerbowani przez 40 dob | 101 145; 146 170 | 103 679; 151 095 | 103 257; 145 113 |
| Bitwy: starc / zabitych | 666 / 17 490 | 724 / 18 781 | 720 / 18 617 |
| s na dobe (dekada 31-40) | 12.4 (13.9) | 12.6 (14.0) | 12.8 (14.3) |
| ERROR / bledy CrashScribe | 0 / 8 (te same) | 0 / 8 | 0 / 8 |

Odczyt:
- Szkody z 144 cofniete: konie na polkach nie zalegaja, cena konia, zakupy mieszkancow i utarg wozow sa w pasmie logow bez T3.
- Zloto za najemnikow nadal +42% wobec 12:37. Roznica to prawdziwe konie z polek (157), ale lordowie placa 1.4 mln wiecej, maja 1.2 mln mniej zlota, a Bank ma wiekszy dlug.
- Nowe przez 148 / 150 i naprawy z materialem: warsztaty zbrojne -13% (braki skory i lnu), obitego sprzetu AI 1.6-1.9 x wiecej, zator lnu u karawan.

## Zdjecia

Zdjecia at-20261008-065139: 27/27 (9 celow x z08 / z17 / z40), 3840x2160, gra 10:15. Pomniejszone do 1600 px w analiza-autotestu\zdj\t3-*.jpg. Wzorzec v5 to przebieg 06:19 (at-20261008-061906, te same cele i kamera, gra 10:11) - zdj\v5-*.jpg. Pary obok siebie (lewa v5, prawa T3) w zdj\para-*.jpg, liczby w zdj\roznice.txt (skrypt zdj.py).

Werdykt: wyglad jak po wgranej v5 - OK.
- Roznica pikseli T3 wobec v5 (obraz 400x225): srednio 1.5-4.1 na 255; pikseli rozniacych sie o wiecej niz 40: 0.0-0.4%. Najwiecej: windmill-applewick-z08 0.4%, village-barrowthwaite-z08 0.3%. To tylko pora dnia 10:11 / 10:15 (cienie) i zwierzeta.
- Obejrzane: mill-sweet-bridge z08 (+ para), windmill-applewick z08 (para), dachy-three-roses z08, fishing-widows-horn z08, slope-poppymead z08 (para), granary-pennycross z08 (para), village-barrowthwaite z17.
  - Domy stoja na gruncie, nie zapadaja sie (Reach, dachy-three-roses).
  - Wiatrak ma skrzydla.
  - Rybacy: pomosty od brzegu, lodzie na wodzie, chaty na ladzie, zadnych domow na skalach w wodzie.
  - Mlyny przy rzece z kolem. Czesc mlynow siedzi czesciowo w wodzie (sweet-bridge, poppymead) - to znany drobiazg v5 z STAN-PRAC ("mlyny czesciowo za bardzo w wodzie, przesunac ok. 0.5 jedn."), nie nowy blad.
- Log potwierdza to samo co do liczby. Linia pliku wiosek (177) jest identyczna z 06:19: crc32 89612CA7, 600209 B, 2446 wierszy; pierwsze wypelnienie 179 w 60 ms (v5: 59 ms). Linia koncowa (7674) ma te same liczby co v5 06:19 i inne niz v4 04:43:
  - "dol BB na najnizszym gruncie ... budynkow 4593";
  - "kolo mlyna nad woda 181, nad ladem 0, mlyn bez kola 0";
  - "pomost od brzegu 182, lodzie na wodzie 214, na brzegu 5";
  - "wiatraki ze skrzydlami 59, bez skrzydel 0";
  - "niezbedne dalej w wodzie 0";
  - "potkniecia: ... diagnostyka 0" (bez AccessViolation TreeLine).
- Galaz t3-sklad HEAD = 5ccb0f5 "wioski na mapie - wyglad 3".
- Zatwierdzony DLL wrocil do gry: Armoury.dll w grze md5 b0e62e1f; dll-final-3 Armoury md5 5a7074c0. To, ze test szedl na DLL T3, potwierdzaja linie 154 / 155 / 157 w logu.

## Problemy

PROBLEM (z dowodem)
1. Naprawy AI czekaja na material.
   - Przez 40 dob 20 655 sztuk "czeka na material" wobec 10 209 naprawionych, czyli 67% prob; 85% to metal.
   - Doba 40 (linia 7475): naprawione 259, czeka 632 (metal 571).
   - Obitego sprzetu u AI przybywa liniowo do 12 101 szt. (12:37: 7449; 14:18: 6488).
   - Na polkach jest rudy 2717 ladunkow, ale 39 miast nie ma jej wcale, sztabek jest 0; na naprawy idzie 3 ladunki rudy na dobe.
   - WYNIK oczekiwal "ulamek napraw".
2. Zelazny Bank gorzej niz 14:18, a mialo byc lepiej.
   - 234 443 dlugu, 15 dluznikow, 2 bankructwa: Wylde (linia 6135) i Errol (linia 6324). W 14:18: 198 433 / 14 / 1; w 12:37: 96 691 / 9 / 0.
   - Dlug w dobie 30: 138 tys. wobec 47-52 tys.
   - Pozyczki: 45 na 253 758 zl, glownie Zelazne Wyspy w wojnie (Pyke 69 tys., Harlaw 38 tys.).
   - Lordom ciazy m.in. zaplata za konie najemnikow (+1.41 mln zl wobec 12:37); glowy rodow maja -1.23 mln wobec 12:37.
3. Pyke: karawany stoja przez caly test.
   - "Karawany bez statkow w tym samym miescie od 7+ dob": Pyke 14 od doby 26 do konca, "najdluzej 39.0 dob - Pyke" (linia 7616); razem 32-42 karawany w roznych miastach.
   - WYNIK oczekiwal "nie dluzej niz kilka dob". To znana granica 154 (STAN-PRAC: "karawany Pyke czekaja na zysk z Lordsport jak w grze"; karawany lordow BK na wyspach z jednym miastem: Pebbleton 3, Lonely Light 36 dob, Blacktyde) - potrzebna decyzja, nie regresja.

DO SPRAWDZENIA
4. Zloto za najemnika 427 zamiast ok. 290.
   - Bez zaplaty za konie to 200 zl; reszta to 2605 prawdziwych koni z polek za 2.54 mln zl, srednio 975 zl za konia (doba 1: 1372).
   - Zgodne z regula 157 ("bez targu" 0; nadplata zwrocona 25 093), ale kon najemnika x mnoznik kupujacego jest ok. 2.5 x drozszy od dawnej stalej i obciaza lordow - decyzja projektowa.
5. Warsztaty zbrojne zrobily 26 931 sztuk, czyli -13% wobec 12:37 (30 812) i 14:18 (31 299).
   - Braki surowca: skora 1821 (wczesniej 590-1137), len-welna 4061 (wczesniej 1741-2402).
   - Koszt sztuki 20.2 zl (wczesniej 14.1-17.7).
   - Rzemioslo miasta i odziez wojska biora ta sama skore i len; to ryzyko bylo przewidziane w opisie 148 (pkt 3).
6. Len i odziez wojska.
   - Alarm narzedzia: len KUPNO STOI 6 dob z rzedu do konca testu (linia 6664), lacznie 10 dob.
   - Len: 52 miasta bez lnu, ale 2881 sztuk w jukach karawan; z 18 wjazdow z lnem tylko 1 do miasta z brakiem.
   - Plotno: indeks mediana 5.00, 55 miast bez plotna.
   - Odziez wojska: zaleglosc rosnie do 1128 / 2054 / 1693 (skora / sukno / plotno) w 687 partiach; 63 wizyty na dobe bez towaru.
7. Pieniadz swiata ubywa szybciej niz bez T3: -16.02 mln przez 40 dob (12:37: -14.11; 14:18: -16.85).
   - Zolnierze wydaja mniej na zycie w miastach: 5.22 mln wobec 6.25 mln w 12:37 (14:18: 5.95 mln).
   - Sakiewki ludzi rosna do 4.85 mln; najwieksza sakiewka rosnie 7 dob z rzedu (linia 7653).
8. Karawana z Evenfall Hall (Tarth) krazy w petli: 30 i 37 odrzuconych rozkazow na dobe (doby 108861 i 108875), za kazdym razem "karawana w polu -> Cape Wrath => Evenfall Hall (dom)"; "odrzucone w ocenie" skacze do 7677 i 8179.
9. Nowe, jednorazowe:
   - linia 5716 (dzien 108866): "Dowoz (wozy): cena rosla 1" - w 12:37, 14:18 i 10:40 bylo 0 przez 40 dob;
   - linia 4394 (dzien 108858): "Towary (bilans): bez wyjasnienia +7309 (grain +7262)".
10. Konie od notabli (157 - poza zakresem, WYNIK 7.2): 164 konie, 120 927 zl do notabli przez 40 dob.
11. Konie na polkach w dobie 40: 7600 zamiast oczekiwanych ok. 9000. Blad 144 jest naprawiony; nizszy poziom (-18%) tlumaczy 2605 koni zabranych przez najemnikow. Do potwierdzenia, ze to zamierzone.
12. Parser narzedzia (UWAGA 1) nie zna linii startowej "Wozy w miastach: brama poza pamiecia drog ROT ... zadna (0 z 799 osad)" (linia 312; to samo w 12:37). Do poprawy w sprawdz_logi.py (Temat, linia 1585).

BRAK DANYCH / NIE PRZECWICZONE
- Wraki jako zlom: linia AI nie ma licznika; kwatermistrz 0; kowala gracza nie bylo.
- Drob w budzecie koni: 0 drobiu w swiecie.
- TroopSelfMend, "Pick a piece", kulawy kon, najemnicy gracza, turnieje gracza.

OK
- ERROR 0 (Armoury, Logs\*, RC, GT); CrashScribe 8 = stare sygnatury startowe; "bledy +0" w 40 dobach; zawieszenie tylko przy wyjsciu (silnik, jak w 12:37).
- Wszystkie linie startowe obecne, bez BRAK; horse /25 usuniete (76 kategorii).
- Cena konia (Stajnia AI 630, empire 579) i zakupy mieszkancow (12.55 / 1.53 mln) w pasmie logow bez T3.
- Utarg wozow 14.32 mln.
- Spoils "wyplacono z niczego 0" 280/280; Towary (bilans) ruda i drewno ZGODNA 39/39.
- Rzemioslo miasta (148) i rzemieslnicy BK (147): potkniecia 0.
- Wozy w miastach: najdluzej 0 dob, bezpiecznik 0. Zatkane wsie: 13 z 571.
- Lordowie na wyspach: 0 odrzucen, 0 stojacych.
- Kasy miast ponizej 20 000 tylko 2 doby (9812 i 12 513), w dobie 40 min 33 538.
- Czas doby 12.4 s (dekada 31-40: 13.9).
- Wioski = v5.

Pliki robocze (C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad\dzien-6\towary3\analiza-autotestu\):
- sprawdz-3t.txt - wyjscie narzedzia;
- metryki.py, metryki.csv - porownanie 6 logow;
- blok1.txt, blok2.txt, blok20.txt, blok39.txt, blok40.txt, doba40.txt - linie dob;
- zdj\ (t3-*, v5-*, para-*, roznice.txt), zdj.py.
Nic nie wgrane, nic nie zacommitowane.
