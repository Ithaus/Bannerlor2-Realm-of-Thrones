# Weryfikacja rozdzialu WOJNA I SWIAT (plik analityka: wojna.md)

Praca tylko do odczytu. Nic nie uruchamialem w grze, nic nie zmienialem. Jedyna rzecz poza logami: zdekompilowalem dwa typy z ROT.dll do swojego katalogu (sam odczyt pliku gry).
Nazwa pliku w zadaniu byla nieuzupelnionym wzorcem `${l.key}-weryfikacja.md`. Zapisalem jako wojna-weryfikacja.md, obok wojna.md.

## Jak czytac

- [P] = pomiar, [S] = szacunek. Doba = dzien gry minus 108836.
- ARM = D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_06-24-10.log (L = numer linii)
- CS = ...\session-2026-10-13_06-24-09.log
- BIT / BR / LR = ...\Logs\2026-10-13_06-24-10\bitwy.log / budzet-rodow.csv / ludzie-regiony.csv
- P2 / P4 / P7 = ...\scratchpad\test\progi-rok5\etap2.txt / etap4.txt / etap7.txt (numer linii)
- ROT = moja dekompilacja ROT.dll 8.1.8 (md5 27c8957d...): skrypty\wojna-wer\rot_ROTStorylineWars.cs.txt i rot_ROTStorylineWar.cs.txt
- Moje skrypty i wyniki: ...\raport-rok\skrypty\wojna-wer\ (w1-w11, pliki .txt obok). Skrypty analityka czytalem dopiero po wlasnym wyniku.

## Najkrocej

1. Liczby analityka sa policzone rzetelnie. Z 30 sprawdzonych 23 wyszly mi co do sztuki, 3 roznia sie o 0.3-3% z powodu definicji, 3 sa bledne (mediana szturmow, liczba porozumien, cena wezwan Pentos), 1 jest dobra liczba ze zlym opisem (plon Sharp Point).
2. Glowny wniosek jest szerszy niz dane. "Wojny fabularne nie maja konca" jest prawda tylko dla 4 z 14 par. Pozostale 10 ma w kodzie ROT koniec okna: u nas doba 433, czyli 69 dob po koncu tego biegu. Analityk napisal "nie wiem, kiedy ROT ktorakolwiek zwolni". To da sie sprawdzic i sprawdzilem.
3. Przez to "podatek wlaczany flaga wojny bylby podatkiem stalym" jest prawda dla roku 1 i dla ok. 9 krolestw Westeros. Dla 17 krolestw (cale Essos, Dolina, Dorne) przymus wojny znika w dobie 433.
4. W czesci dla Jeffa brakuje najwazniejszego faktu: korona juz dzis doplaca do wojska w wojnie 274 tys. zl dziennie (zwrot 70% zoldu). Podatek wojenny ludu (14.9 tys.) to 5% tej kwoty. Brakuje tez dowodu, ze wojska jest za malo z braku zlota: 160 z 278 rodow nie wykorzystuje swojego pulapu zoldu.

---

## 1. Liczby analityka policzone od zera

| # | Liczba analityka | Moja | Zgodne? | Moje zrodlo |
|---|---|---|---|---|
| 1 | silniejszy wygrywa 1849 z 1869 bitew lordow (98.9%) | 1849 z 1869 (98.93%) | TAK | BIT, linie B175 "lordowie tak", pole moc (w1) [P] |
| 2 | kubly: 94/111, 146/149, 1609/1609 | 97/114, 146/149, 1606/1606 | PRAWIE | on bierze pole "stosunek sil" zaokraglone do 0.01, ja dziele moc przez moc; suma ta sama [P] |
| 3 | 23 szturmy, 14 udanych, najmniejsza przewaga 3.6 do 1 | 23 / 14 / 3.60 | TAK | BIT L13406 (Sombazmion) [P] |
| 4 | mediana przewagi udanych szturmow 6.8 | 6.47 | NIE (drobne) | wzial 8. z 14 wartosci zamiast srodka (k14: sorted[len//2]) [P] |
| 5 | 73 pokoje fabularne, 73 wznowione tego samego dnia, 0 nie | 73 / 73 / 0 | TAK | ARM linie "Wojna i pokoj (E7/0)", po numerach linii (w2); CS L9240 [P] |
| 6 | 86 wojen ROT, 17 z wezwania, 80 pokojow z decyzji, 8 zerwanych | 86 / 17 / 80 / 8 | TAK | ARM, 191 zdarzen E7/0 (w2) [P] |
| 7 | 25 z 29 krolestw w wojnie 342-364 doby, Riverlands 277 | 25 z 29; Riverlands 277 | TAK | ARM linie "Skarbce", flaga (WOJNA) (w4) [P] |
| 8 | 12 313 400 zl reparacji nienaliczone | 12 313 400 w 72 liniach z kwota | TAK (liczba), NIE (znaczenie - patrz 2.4) | CS linie "Reparacje (T9)" (w6) [P] |
| 9 | 440 z 528 uwolnien "przez pokoj" w dobach pokoju fabularnego | 440 z 528 (434 + 6 w dobach z oboma rodzajami pokoju) | TAK | ARM "Niewola lordow i okupy (169c)", odcinki miedzy liniami (w4, w8) [P] |
| 10 | uchodzcy 187 910; przybylo 216 068, wrocilo 24 232; ok. 675 na dobe | 187 910; 216 068; 24 232; 676 na dobe | TAK | ARM L129266, L129272; suma 354 linii (w4) [P] |
| 11 | pula wyrzutkow 11 869; tempo +40 / +53 / +4.8 na dobe | 11 869; regresja +40.2 / +53.2 / +4.8 | TAK | ARM L129135 (w4). Miedzy koncami okna +10.0 na dobe: ostatnia doba sama dala +695 [P] |
| 12 | bandy 819 / 17 279; +62 / +29 / +20 ludzi na dobe | 819 / 17 279; +62.0 / +28.6 / +19.8 | TAK | ARM L129134 (w4) [P] |
| 13 | 212 z 227 regionow "w wojnie" (pisze: caly rok) | 212 w 277 dobach; 198-199 w 65; mniej w 22 pierwszych | TAK z uwaga | ARM pole "N reg. z pula" w 364 liniach puli (w4) [P] |
| 14 | wygrane band 373 / 700 / 808 / 893 na kwartal; razem 2774 / 2392 | 378 / 708 / 821 / 904; razem 2811 / 2441 | PRAWIE (1.3%) | BIT linie H3 (w7). Jego lista band nie ma Hill Tribesmen, Pirates, Lengii Slavers [P] |
| 15 | najemnicy: 764 zmiany, mediana 3 doby, 151 jednodniowych, 11 prosto do wroga | 762; 3; 151; 11 | PRAWIE (0.3%) | BR (w5, w8). Roznica 2 zmian z liczenia wyjsc na bezrobocie [P] |
| 16 | 14 zmian pana twierdzy, zadna dwa razy; 7 z 14 wojen bez zmiany twierdzy | 14; 0; 7 z 14 | TAK | LR kolumna krolestwo (w5) [P] |
| 17 | 0 krolestw upadlych, 0 rodow zniklo z 347, 28 nowych "Courtiers" | 0; 0 z 347 (319 na starcie + 28); 28 | TAK | BR; ARM "Skarbce" 29 nazw caly rok (w5, w4) [P] |
| 18 | podatek wojenny ludu: 3 882 375 w roku; 14 922 zl dziennie w ost. kwartale; 20 z 26 na pulapie | 3 882 375; 14 922; 20 z 26 | TAK | ARM linie "podatek wojenny ludu (184)", L129100 (w4) [P] |
| 19 | zold roczny 228.6 mln; podatek = 1.7% | 228 634 676; 1.70% | TAK | ARM linie "Zold:" (w4) [P] |
| 20 | skarbcom ubywa 51 tys. dziennie, mieszczanom przybywa 75 tys. | -51 480 i +75 120 zl dziennie | TAK | ARM L97256 i L129205 (w4) [P] |
| 21 | 5.7 zl na zolnierza; brak 32-65 tys. zl dziennie | 5.70 (ost. doba), 5.49 (28 dob); 32-65 tys. | TAK (rachunek) | ARM L129132, L129266 [S]. Zalozenie "brak wojska = brak zlota" - patrz 4.3 |
| 22 | wojsko 79 409 wobec 64 013 | 79 409 wobec 64 013 | TAK | ARM "Skarbce", srednia 28 dob, oba biegi (w4) [P] |
| 23 | zmiany pana twierdzy 14 wobec 41; sluby 215 wobec 113 | 14 wobec 41; 215 wobec 113 | TAK | LR i "Sluby AI (169c)" obu biegow (w5, w4) [P] |
| 24 | morale 70.2; w glodzie 2.3 z 624; glodnych warowni 0 | 70.2; 2.3 z 624; 0 | TAK | ARM "Morale AI (183d)", "Ludzie:" (w9) [P] |
| 25 | Zelazne Wyspy: 342 doby wojny, 1 bitwa lordow, 0 rabunkow | 342; 1; 0 | TAK z uwaga | BIT (w1). W B175 jest ich 15 razy: 5 napadow na karawany, 10 razy napadnieci [P] |
| 26 | wezwania: 8 zerwanych, zaplacono 1 790 085, skreslono 5 396 992, 4 wojny jednodniowe | 8; 1 790 085; 5 396 992; 4 | TAK | ARM linie "Wezwania do wojny (168)" (w4, w2) [P] |
| 27 | "17 porozumien" | 18 podpisanych, 17 dalo wojne | NIE (drobne) | 18 linii "X wzywa Y"; Skagos bez wojny (ARM L18251) [P] |
| 28 | Pentos 5 razy wzywa Tyrosh "za ok. 0.9-1.0 mln" | 4 razy za 873 982-1 029 618; pierwszy raz za 15 436 (85 zl dziennie) | NIE | ARM L17900, L40827, L61036, L80628, L107600 [P] |
| 29 | Sharp Point: 31.5% ludzi poza domem, plon x0.43 | 31.52% to caly okreg; x0.4281 to jedna wies (Eastpoint, 57% poza domem) | NIE (opis) | ARM L129272: "wsi z >= 1/3 uchodzcow 1" [P] |
| 30 | zalegly zold 2.49% | 2.49% to udzial w partiach; w zlocie 0.12% | TAK z uwaga | P2:10-11 [P] |

Wynik: 23 zgodne co do sztuki, 3 zgodne w granicach definicji (2, 14, 15), 3 bledne (4, 27, 28), 1 dobra liczba ze zlym opisem (29). Z tych zgodnych dwie znacza co innego, niz pisze analityk (8 i 30).

---

## 2. Co obalilem albo poprawilem

### 2.1 "Nie wiem, kiedy ROT zwolni wojny" - da sie sprawdzic. 10 z 14 konczy sie w dobie 433

ROT trzyma liste wojen fabularnych z dniem startu i konca (ROT: rot_ROTStorylineWars.cs.txt L79-L105). Wojna jest wymuszana, dopoki dzien kampanii jest mniejszy niz EndDay; EndDay 0 = bez konca (rot_ROTStorylineWar.cs.txt L63-L93). Nasza Fabula mnozy oba dni x4.33 (CS L285; Fabula.cs L245-L262).

| Para | ROT (start, koniec) | u nas | w logu start |
|---|---|---|---|
| Vale - Iron Islands, Reach - Dorne, Braavos - Lorath, Tyrosh - Lys, Pentos - Myr, Norvos - Qohor, Volantis - Aegon, Qarth - Targaryen, Dothraki - Targaryen, Sarnor - Ibben (10 par) | 5, 100 | doba 22 do 433 | doba 22 - zgadza sie (w2) |
| KL - Riverlands | 30, 0 | od doby 130, bez konca | doba 130 - zgadza sie |
| Dragonstone - Stormlands | 0, 0 | bez konca | doba 1 |
| Nights Watch - Free Folk | 0, 0 | bez konca | od startu |
| The North - KL | wyzwalacz: smierc Neda; gasi Krwawe Gody | do Krwawych Godow (ok. doba 953 wg CHANGELOG L14088) | doba 9 |

Zrodla: [P] dla kodu i startow. To samo stoi w repo: docs\AUDYT-KALENDARZ.md L241 ("5-100 -> 22-433").

Co z tego wynika:
- Bieg skonczyl sie 69 dob przed koncem okna 10 wojen. Czy po dobie 433 pokoj sie utrzyma - nie wiem, tego bieg nie objal [S: powinien, bo krolestwa same uchwalaja pokoj srednio 47 dob po wypowiedzeniu].
- Po dobie 433 bez wymuszonej wojny zostaje 17 krolestw: 13 z Essos (bez Summer Isles i Yi Ti, ktore i tak nie walcza), House Targaryen, Dothrakowie, Dolina, Dorne [S z listy ROT].
- W wymuszonej wojnie zostaje rdzen Westeros: Dragonstone, Stormlands, KL, Riverlands, Polnoc, Nocna Straz, Wolni Ludzie. Dochodza nowe: Zelazne Wyspy - Polnoc od doby 433 do 1018, a od doby 585 Dragonstone - KL, Stormlands - KL i Reach - Dragonstone (ROT L79, L83, L84, L86; 135 x 4.33 = 585) [P z kodu].
- Czyli "wojna bez konca" to w 4 parach zamysl ROT (wojna pieciu krolow), a w 10 parach obraz pierwszego roku.

### 2.2 "Nie wiem, czy Skagos weszlo do wojny i ile wzielo"

Nocna Straz placila Skagos 3 359 zl dziennie przez 35 dob (doby 52-86) = 117 565 zl [P, ARM linie dobowe "Wezwania do wojny (168)", w4]. W dobie 87 gra sama zakonczyla porozumienie i skreslila 493 769 zl (ARM L30746). Stalo sie to dzien po pierwszym "pokoju na zero dni" Wolni Ludzie - Nocna Straz (doba 86). Wojny Skagos - Wolni Ludzie nie ma w zadnej linii E7/0 ani w flagach. Czyli Skagos wzielo 117 565 zl i nie walczylo [P].

### 2.3 Wezwania Pentos i liczba porozumien

- Podpisanych porozumien jest 18, nie 17. 17 dalo wojne, osiemnaste to Skagos [P].
- Pentos wezwal Tyrosh 5 razy, ale pierwszy raz za 15 436 zl (85 zl dziennie) i ta wojna trwala 61 dob do prawdziwego pokoju. Dopiero 4 kolejne kosztowaly 0.87-1.03 mln i wszystkie 4 zerwano [P].
- Ceny wezwan sa dwojakie: 4-39 tys. (6 sztuk) albo 0.56-1.14 mln (12 sztuk). Nie wiem, od czego to zalezy [P, 18 linii "X wzywa Y" w ARM].
- "Nie stac" znaczy tu: rata wieksza niz wplyw tego dnia (nasza regula 168 "z wplywow dnia"). Skarbiec Pentos mial na koniec 1 045 697 zl (ARM L129100). To zderzenie dwoch regul, nie bieda [P].

### 2.4 12.3 mln reparacji to ta sama wojna liczona wiele razy

12 313 400 zl to suma z 72 "pokojow" w 13 wojnach (CS, w6) [P]. Ta sama para konczyla wojne do 10 razy.
- Jeden pokoj na wojne dalby: 2 071 200 zl (pierwszy pokoj kazdej pary) do 2 832 100 zl (najwiekszy kazdej pary) [P]. Srednio 171 019 zl na pokoj.
- Zdanie analityka "dzis 12.3 mln rocznie przepada" jest wiec zawyzone ok. 5 razy i zle nazwane: nic nie przepada, kwoty nigdy nie naliczono.
- Zero reparacji w wojnach fabularnych to nasza wlasna latka T9 (decyzja Jeffa 08.10), bo Diplomacy placil z niczego: odbiorca dostawal cala kwote od razu, a placacy splacal w nicosc (CrashScribe\src\StorylineReparations.cs L14-L23).

### 2.5 Kierunek reparacji to nie sprzecznosc

We wszystkich 72 liniach placi ta strona, ktora zawiera pokoj - pierwsza w linii (w6) [P]. Placi wiec ten, kto pierwszy ma dosc wojny, a nie ten, kto przegral wiecej bitew. Dothrakowie wygrywaja bitwy (37 z 55), ale 6 razy to oni prosili o pokoj. To definicja Diplomacy, nie blad.

### 2.6 Progi band i puli maja baze z doby 120

Prog P4:81 (pula 3 053) i P4:84 (bandy 826 / 14 328) to stan z doby 120 biegu bazowego (tools\sprawdz_logi.py L1690-L1694). Analityk porownal z nimi dobe 364.
- W dobie 120 tego biegu: bandy 652 / 12 153 - ponizej bazy; pula 5 022 - ponad baze [P, ARM L42455, L42456].
- Wobec poprzedniego biegu rocznego w dobie 364: bandy 819 / 17 279 wobec 937 / 20 501 - lepiej; pula 11 869 wobec 9 788 - gorzej [P].

### 2.7 Uchodzcy: szkoda dla plonu dotyczy jednej wsi

Mnoznik x0.43 ma jedna wies swiata (Eastpoint). Wsi z co najmniej 1/3 ludzi poza domem jest 1 z 2446 (ARM L129272; P4 S12: najwyzej 1 wies przez 59 dob) [P]. Powroty rosna co kwartal: 1 -> 20 -> 82 -> 164 ludzi na dobe, a nowi uchodzcy przestali rosnac: 181 -> 573 -> 833 -> 796 na dobe (w4) [P]. Prognoza "430 tys. na koniec roku 2" jest prosta linia przez dane, ktore sie wyginaja - to gorna granica [S].

### 2.8 Zelazne Wyspy: wojsko topnieje tez tam, gdzie wojny nie bylo

| Krolestwo | dob wojny | wojsko: szczyt -> koniec | kiesy rodow rok |
|---|---|---|---|
| Iron Islands | 342 | 5 902 -> 1 455 (25%) | -59% |
| Skagos | 0 | 853 -> 253 (30%) | +454% |
| Yi Ti Exiles | 1 | 698 -> 68 (10%) | +90% |
| Summer Isles | 0 | 606 -> 557 (92%) | +1153% |
| Ibben | 342 | 2 590 -> 521 (20%) | -29% |

Zrodlo: ARM linie "Skarbce" (w6) [P]. Uwaga: kiesy malych krolestw licza najemnikow w sluzbie, wiec plus Skagos i Wysp Letnich nie jest czysty.
Wniosek: sam spadek wojska nie dowodzi, ze "zaplacili za wojne". Spadek kies o 59% jest drugi najwiekszy na swiecie (po Qohor -62%) i to jest prawdziwy sygnal. Przyczyny (zold, brak lupu, brak zwrotu) analityk nie rozdzielil.

### 2.9 Bandy: wzrost wygranych to glownie wiecej band

Wygrane band na jedna bande dziennie: 0.012 w 2. kwartale, 0.0127 w 4. (7.8 i 9.9 wygranych przy ok. 650 i 780 bandach) [S z w7 i w4]. Pierwszy kwartal to rozruch (bandy rodza sie w dobie 1). Od 2. do 4. kwartalu wygrane +27%, band +20%. 2 492 z 2 811 wygranych to tabory wsi (89%) [P]. Szkody dla handlu wsi analityk nie zmierzyl - ja tez nie.

---

## 3. Niespojnosci analityka - werdykt

| # | Tytul | Werdykt | Dlaczego |
|---|---|---|---|
| 1 | Wojny fabularne nie maja konca | CZESCIOWO | Pomiar dobry (73 z 73). Ale 10 z 14 par ma koniec okna w dobie 433 (2.1) - to obraz pierwszego roku. 4 pary bez konca to zamysl ROT. Naprawa "wznawiac po 87 dniach" dla 10 par jest zbedna, a dla 4 zmienia fabule - to decyzja Jeffa, nie blad do latania. |
| 2 | Pokoj na zero dni wypuszcza jencow | PRAWDZIWA | 440 z 528 potwierdzone inna metoda. Tlo: w dobach bez pokoju wychodzi 0.18 jenca dziennie, czyli ok. 12 z 440 wyszloby i tak [S]. Po dobie 433 zostaje w 4 parach. |
| 3 | Zwyciezca nic nie dostaje | CZESCIOWO | Kwota 12.3 mln jest liczona wiele razy (2.4). Skarbiec nie mierzy wygranej: 15 z 29 skarbcow stoi na tej samej linii 1.03-1.05 mln, razem ze Skagos i Wyspami Letnimi bez wojny (ARM L129103-L129131) [P]. Zysku z 4 zdobytych twierdz (renty) nie policzyl. Prawdziwe jadro: przy pokoju fabularnym nikt nie placi - z naszej latki T9. |
| 4 | Wojna bez walki kosztuje jak wojna | CZESCIOWO | Fakty dobre (342 doby, 1 bitwa lordow). Koszt nie jest wykazany (2.8). Wymuszenie Vale - Iron Islands konczy sie w dobie 433, potem ROT daje Zelaznym Wyspom Polnoc. |
| 5 | Uchodzcy nie wracaja | CZESCIOWO | Stan i tempo dobre. Ale powroty podwajaja sie co kwartal, naplyw stanal, a szkoda dla plonu to jedna wies (2.7). To 0.46% ludnosci wsi [P]. |
| 6 | Wyrzutki wracaja w tempie wojny | CZESCIOWO | Mechanizm prawdziwy: 19 298 powrotow z regionow "w wojnie", 55 z regionow w pokoju (w4) [P]. Ale progi maja zla baze (2.6), pula stoi od doby 245, a po dobie 433 duza czesc regionow wyjdzie z wojny sama. |
| 7 | Najemnicy zmieniaja pana co 3 doby | PRAWDZIWA | 762-764 zmian, mediana 3 doby, 11 przejsc do wroga - wszystko potwierdzone. Jest w obu biegach (681 i 764). Pomiar z migawki dobowej, wiec to dolna granica. |
| 8 | Wezwania, na ktore nie stac | PRAWDZIWA | Z poprawkami z 2.3: 18 porozumien, pierwszy Pentos tani, "nie stac" = rata ponad wplyw dnia przy skarbcu 1 mln. |
| 9 | Bandy bija coraz wiecej malych partii | CZESCIOWO | Trend jest, ale 1. kwartal to rozruch, a potem wygrane rosna prawie tak jak liczba band (2.9). Szkoda dla handlu nie zmierzona. |
| 10 | Kierunek reparacji nie zgadza sie z bitwami | ARTEFAKT | Placi strona zawierajaca pokoj - 72 z 72 (2.5). |
| 11 | Dothrakowie nie sa horda konna i nie ruszaja Wolnych Miast | CZESCIOWO | Jazda 43.9% - prawda, sprawa znana (P5:366-368). "Nie ruszaja Wolnych Miast" to nie dziw: Dothrakowie nie mieli przez rok wojny z zadnym Wolnym Miastem (tylko z House Targaryen), wiec licznik 0 jest oczekiwany [P, w2]. |
| 12 | Wezwanie Skagos bez wojny | PRAWDZIWA | I policzalna: 117 565 zl za 35 dob bez wojny (2.2). |
| 13 | Brak pomiaru smierci i narodzin bohaterow | PRAWDZIWA | To brak pomiaru, nie dziw swiata. Potwierdzam: w obu logach nie ma takiej linii; dorosli lordowie AI 1227 -> 1662 (ARM L683, L129182) [P]. |
| 14 | Drobne bledy pomiaru w pojedynczych dobach | CZESCIOWO | Dwa to pojedyncze doby (175 i 229), jeden to 4 noce. Ale "2.3 na noc wobec progu 2.0" (P7:17) to srednia z 333 nocy, nie jedna doba. |

Razem: 4 prawdziwe, 9 czesciowo, 1 artefakt.

---

## 4. Czego brakuje

### 4.1 Wniosek szerszy niz dane: wojna "na zawsze"

Rok 1 lezy w calosci w oknie wojen ROT. Nie da sie z niego powiedziec, jak swiat wyglada, gdy pokoj jest mozliwy. Brakuje biegu do ok. doby 550 (okno 433 + czas na pokoj). Bez tego naprawy 1, 2, 5, 6 z listy analityka latalyby cos, co w 10 parach moze zniknac samo.

### 4.2 Pytanie Jeffa o podatki "na czas wojny" - czego nie ma w rozdziale

1. Nie ma glownej doplaty wojennej. Korona zwraca rodom w wojnie 70% zoldu partii: 95 681 216 zl w roku, 273 877 zl dziennie w ostatnim kwartale (ARM linie "Korona: dzien ... zwrot zoldu", ostatnia L129096; w11) [P]. Podatek wojenny ludu (14 922 zl) to 5.4% tej kwoty. Kazda propozycja podatku powinna byc liczona wobec zwrotu, nie wobec zoldu.
2. Flaga wojny wlacza dzis cztery rzeczy naraz: zwrot 70% zoldu, podwojny pulap zoldu rodu (0.28 dochodu w pokoju, 0.60 w wojnie - ClanBudget.cs L26), podatek wojenny ludu i wolniejszy powrot wyrzutkow. Analityk opisal tylko dwie ostatnie.
3. Gotowa miara "krolestwo naprawde walczy" juz jest w kodzie i w logu: styk z wrogiem w linii "Renty korony (180)". W ostatnim kwartale z 26 krolestw w wojnie srednio 14.4 nie mialo tego dnia styku (ARM, ostatnia L129098; w11) [P]. Propozycja A analityka (okno 30 dob) jest duzo luzniejsza: wedlug mojego liczenia spelnia ja 84.6% krolestwo-dob wojny, a 18 z 26 krolestw ponad 95% czasu (w6) [P]. Z oknem 30 dob to dalej podatek staly dla prawie wszystkich. Odroznia tylko Zelazne Wyspy (9%), Doline (20%), Straz i Wolnych Ludzi (po 44%).
4. Zadne krolestwo AI przez caly rok nie mialo wyzszego pulapu z polityki War Tax (6%): 0 linii z "pulap 6.0%" w 364 dobach (ARM, linie 184) [P]. Wyzszy pulap juz istnieje w kodzie, tylko AI po niego nie siega.
5. Wplyw z podatku jest bardzo nierowny: przy tej samej stawce 3% Polnoc zbiera 1 742 zl dziennie, a Lys 120 (ARM L129100) [P]. Rachunek "1 punkt = 6 tys. zl na swiat" nie mowi, czy pieniadz trafi tam, gdzie brakuje wojska.

### 4.3 Zalozenie "brak wojska = brak zlota" nie jest wykazane

- Zold do pulapu zoldu w ostatnich 28 dobach: 636 tys. z 965 tys. zl dziennie = 66% (BR kolumny pulap, zold_partii, zold_zalog, zold_karawan; w10b) [P].
- 118 z 278 rodow AI stoi na pulapie albo ponad nim (maja 32.2 tys. ludzi). 160 rodow ma zapas, razem ok. 357 tys. zl dziennie niewykorzystanego pulapu, i trzyma 44.3 tys. ludzi przy limicie wielkosci partii 70.6 tys. [P].
- Zalegly zold w zlocie to 0.12% (P2:10) [P].
- Druzyny lordow traca w bitwach srednio 404 ludzi dziennie (146 593 w roku - prawie dwa razy tyle, ile ich stoi pod bronia), a wodzowie werbuja 808 dziennie w roku i 589 w ostatnim kwartale (ARM linie "Ludzie:", bilans partii rodow; w6) [P].
- Wniosek [S]: dla ok. 40% rodow zloto jest sufitem i doplata pomoze. Dla ok. 60% sufitem jest cos innego (werbunek wobec strat). Sam wyzszy podatek nie da 85 tys. Tego analityk nie sprawdzil, a kwota 32-65 tys. zl dziennie zaklada, ze kazda zlotowka zamienia sie w zolnierza.

### 4.4 Skarbce korony: spadek 51 tys. dziennie nie jest opisany

15 z 29 skarbcow ma prawie ten sam stan (1.03-1.05 mln), tez krolestwa bez wojny [P]. Wyglada to na wspolna regule, nie na nacisk wojny. Nie wiem, jaka - to pytanie do rozdzialu o gospodarce. Przy 27.1 mln i -51 tys. dziennie zapas starcza na ok. 530 dob [S]. Jesli zwrot zoldu idzie w czesci z zapasu, to dzisiejsze 79 tys. wojska tez nie jest oplacone na stale.

### 4.5 Prog 85 tys. liczy tylko krolestwa w wojnie

Miara P2:12 sumuje wojsko krolestw z flaga wojny (srednio 26.0). Gdy po dobie 433 czesc krolestw zawrze pokoj, ta suma spadnie sama, a pulap zoldu rodow w pokoju spada z 0.60 do 0.28 dochodu. Nowy cel (70-80 tys.) trzeba zapisac "na krolestwo w wojnie" albo "przy 26 krolestwach w wojnie", inaczej w roku 2 prog bedzie mierzyl liczbe wojen, nie wojsko [S].
Pomiar pomocniczy: w ostatnim kwartale wojsko krolestw w wojnie bylo ponad 75 tys. w 90 z 91 dob, ponad 80 tys. w 54 z 91; ostatnia doba ponad 85 tys. to doba 276 (ARM "Skarbce", w4) [P]. Pasmo 70-80 tys. zgadza sie z tym, co swiat dzis trzyma.

### 4.6 Liczby bez zrodla albo slabe

- "W 5 z 6 par ziemie wziela strona bogatsza" - probka 6, sam pisze, ze to nie dowod. Nie powinno trafic do raportu.
- "Jesli tak zostanie, Dragonstone zejdzie do samych miast" - prosta linia z 4 zdarzen [S].
- Zaraza obozowa przy 31 oblezeniach - bez liczby zmarlych; sam pisze "nie wiem".
- Rozdzial nie mowi, ilu ludzi kosztuje jedna zdobyta twierdza w skali roku: 14 twierdz za 146 593 zabitych z druzyn lordow [P] - to mocna liczba dla pytania, czy wojna ma sens, i jej brakuje.

---

## 5. Co z tego dla raportu dla Jeffa (jednym ciagiem)

- Zostawic: bitwy trzymaja logike; mapa spokojna (14 zmian wobec 41); wojsko lepsze niz w poprzednim biegu (79 409 wobec 64 013); nikt nie znika; jency wychodza za darmo przy pokoju na zero dni; najemnicy co 3 doby; Skagos wzielo 117 565 zl za nic.
- Poprawic: "wojny bez konca" -> "w roku 1 tak; 10 z 14 ma koniec okna w dobie 433, 4 sa bez konca z zamyslu ROT"; "12.3 mln reparacji" -> "ok. 2-3 mln, gdyby kazda wojna konczyla sie raz"; "Pentos 5 razy za milion" -> "4 razy".
- Wyrzucic z listy dziwow: kierunek reparacji; Dothrakowie a Wolne Miasta.
- Przed decyzja o podatku: (a) bieg do doby ok. 550, (b) w rozdziale o gospodarce policzyc podatek wobec zwrotu zoldu 274 tys. zl dziennie i sprawdzic, ktore rody stoja na pulapie, (c) "czas wojny" liczyc gotowa miara styku, nie flaga i nie oknem 30 dob.
