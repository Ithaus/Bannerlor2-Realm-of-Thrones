# MAPA: bilans swiata z logow (tylko logi Armoury-*.log)

## 0. Zrodla i metoda
Katalog: `C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Armoury/` (jest tylko 12 logow z 09-11.11).
- **Gra Jeffa (lancuch ciagly, 89 dob = 0.245 roku 364-dniowego, dni kampanii 109726-109815; kalibracja startu = dzien 108648, czyli ~3.2 roku gry):**
  `Armoury-2026-11-09_16-36-21.log` (109726-743), `..._17-37-47.log` (743-752), `..._21-17-29.log` (752-803, 171 tys. linii), `Armoury-2026-11-11_13-19-13.log` (803-815).
- Autotesty (zawsze dni 109734-740, 7 dob z tego samego zapisu): 09.11 17-26, 18-47, 20-35, 23-53; 11.11 01-58, 10-43, 12-35. Alternatywna galaz: 09.11 19-04 (747-752).
- Skrypty i dane: `.../scratchpad/bad5/ext.py`, `an.py`, `an2.py`, `series.json` (ok. 130 szeregow dobowych).
- Linie-zrodla (prefiksy): `Straznik obiegu (obieg2)`, `Obieg: dzien`, `Obieg: notable i karawany`, `Pieniadz swiata (bilans - przyczyny)`, `Kiesa ludu:`, `Ludzie: dzien`, `Miasta (E4/8 R2)`, `Spichlerze (E4/6)`, `Zima (E4/0)`, `Skarbce: dzien`, `Budzet rodow (166)`, `Dlugi (168)`, `IronBank:`, `Dlugi miast (m96-dlugi)`, `Kupcy zywnosci (KS-H)`, `Mennica (311b)`, `Kasy miast (169c)`, `Kasy zaulkow (m6b)`, `Rynek karawan (294)`, `Rozklad wyniku karawan (138c)`, `Handel z roznicy cen (287)`, `Marza kupcow z karawan (P1)`, `Karawany - zysk do wlasciciela (E3-T2)`, `Wyrzutki:`, `Pula wyrzutkow (E4/3)`, `Partie (E4/0)`, `Skarbce kryjowek`, `Utarg wsi (112)`, `Warsztaty towarowe:`, `Warsztaty:`, `Wsad warsztatow (284)`, `Towary (bilans)`, `Sprzedaz miast (241)`, `Zbytki ludu (P3-P5)`.
- UWAGA metodyczna: (1) w 89 dobach zmienialy sie wersje DLL (4 sesje), (2) pierwsza linia kazdej sesji jest niepelna (np. plon 74.8%, podatek 9428) - nie brac jej za zdarzenie, (3) zawor kas miast ma **cykl 7-dobowy** (dni 730, 737, 744, 758...: zawor miast ~620 tys., nazajutrz lordowie +650..720 tys.) - trendy liczyc na wielokrotnosci 7 dob; okno autotestu 734-740 lapie jeden taki skok i dlatego zawsze pokazuje "lordowie +160..185 tys./d, kasy miast -67..-119 tys./d" (to nie regresja wersji).

## 1. Calkowite zloto swiata i podzial
`Straznik obiegu`: "swiat wczoraj N: lud ..., lordowie ..., korony ...".

| dzien | swiat | lud (miasta+podzamcza+gminy+wsie) | lordowie (z graczem) | korony | kasy miast | kasy zamkow | notable | sakiewki zolnierzy | Bank |
|---|---|---|---|---|---|---|---|---|---|
| 109730 | 224.41 M | 21.11 M (9.4%) | 78.83 M (35.1%) | 62.78 M (28.0%) | 13.19 M | 10.62 M | 20.35 M | 4.38 M | 3.54 M |
| 109770 | 224.91 M | 21.57 M | 81.57 M | 62.57 M | 12.32 M | 10.84 M | 20.10 M | 4.04 M | 3.22 M |
| 109810 | 225.21 M | 20.39 M (9.1%) | 84.60 M (37.6%) | 61.73 M (27.4%) | 12.24 M | 10.37 M | 20.01 M | 4.20 M | 3.07 M |

Trendy (regresja po 85-89 dobach; "na rok" = x364):
- **Swiat: +9.5 tys./d = +3.4 M/rok = +1.5%/rok.** Mennica (`Mennica (311b)`: "netto swiata") srednio +11.2 tys./d (0..83 tys.; "od wczytania 2.06%/rok") - czyli caly przyrost to nowa moneta z kruszcu; bez mennicy swiat lekko traci (ok. -1.7 tys./d).
- **Lordowie: +68.8 tys./d = +25 M/rok (+30%/rok stanu).** Gracz to nie jest: "kiesa naprawde 36398" (21-17-29.log, `Prognoza zlota gracza` dz. 109771).
- Lud: -11.2 tys./d (-19%/rok); same kiesy ludu miast (`Kiesa ludu`: "kiesy") 9.83 M -> min 8.83 M (dz. 109803) -> 9.26 M (dz. 109814).
- Korony: -14.4 tys./d (-8%/rok); kasy miast **-18.4 tys./d (13.77 -> 11.78 M, -14.4% w 87 dob)**; Bank -8.0 tys./d; sakiewki -3.5 tys./d; notable -2.6 tys./d; kasy zamkow ~0; kiesy wsi ~0 (8.54 -> 8.39 M).
- Bilans: ubytki (kasy miast 18.4 + korony 14.4 + lud 11.2 + Bank 8.0 + sakiewki 3.5 + notable 2.6) + mennica ~10 = ~68 tys./d = przyrost lordow. **Pieniadz plynie jednokierunkowo: miasta, lud, korony -> lordowie.**
- Ostatnia sesja (najnowszy DLL, 109803-815, tylko 12 dob): lordowie -38.7 tys./d, kasy miast +21.7 tys./d, kiesy ludu miast +26.6 tys./d - kierunek odwrocony, ale okno za krotkie (nie obejmuje 2 pelnych cykli zaworu).

Zrodla/ujscia (`Pieniadz swiata (bilans - przyczyny)`, "kontrola: Z= S= U= N= R="): S srednio 562 tys./d, U 541 tys./d, ale ~520 tys. z obu to **zold** (rozliczenie rodu "w nicosc" + SoldierPay "z niczego" - konwencja ksiegowa, nie kreacja). Prawdziwe "z niczego": mennica 11.2 tys./d, bitwy ~2 tys./d, "dochod za tier" 480/d. ARYTMETYKA OK w kazdej linii. Reszta niezmierzona R srednio -6.7 tys./d; log sam flaguje "srednia 28 dob -15231 / -16727 - prog 10000: PONAD" (21-17-29.log:113257, :141352). Skoki "inne z niczego": 431 tys. dz. 109787 (21-17-29.log:113266; skompensowane reszta -431 tys., Z=-378), 133.5 tys. dz. 109798, 63.8 tys. dz. 109795 - pochodzenie nieustalone.

## 2. Krolestwa: reakcja na wojne (`Skarbce: dzien`)
- W 89 dobach **1 nowa wojna i 1 pokoj** (`Wojna i pokoj (E7/0)`); rodow w wojnie stale 286-291 z 549. Reakcji w czasie nie da sie zmierzyc - tylko przekroj.
- Przekroj: krolestwo w wojnie traci skarbiec srednio **-6.2 tys./d**, w pokoju zyskuje **+1.2 tys./d** (539 vs 1760 krolestwo-dob).
- **King's Landing (WOJNA): skarbiec 6.51 M -> 3.07 M (-39 tys./d; do zapasu wojny 0.73 M ok. 60 dob, do zera ok. 79)**; kiesy jego rodow 14.72 M -> 22.95 M do dz. 109803 (+110 tys./d, +56% w 75 dob; po przejsciu 10 rodow 24.29 M); wojsko rodow 23.5 tys. -> 29.8 tys. Korona placi "zold krolewski 314c" 44-78 tys./d + zwrot zoldu 11-24 tys./d + W5b.
- **Polnoc (WOJNA): kiesy rodow 13.60 M -> 11.35 M (dz. 109803), wojsko 21.0 tys. -> 12.9 tys. (dz. 109760) -> 18.1 tys.; dz. 109804 rozpad: 94 -> 53 rody, powstaje Riverlands (30 rodow, 7.23 M, skarbiec 10 074).** Dni 109752-757: dezercja 2484 + 1249 + 1799 + 1173, "reszta -5518" - z partii rodow ubylo 8.7 tys. ludzi (11%) w 4 doby, pula wyrzutkow 3.1 tys. -> 5.4 tys.
- Martwe skarby pokoju: The Vale 11.07 M, Dorne 8.65 M, Targaryen 6.17 M, Aegon 6.38 M - razem 32 M (52% zlota koron), wszystkie rosna (+0.3..+0.7 M w 89 dob), wojska 1.2-3.0 tys. Skarbcow ponizej 0.5 mln: 7-8 z 26.
- Miasto zdobyte dz. 109775: `Miasta (E4/8 R2)` "zdobycie (L6) -26486.9" ludzi (21-17-29.log:75553) - jedyny duzy ubytek ludnosci.

## 3. Ludnosc i wojsko (`Ludzie: dzien`, `Hearth`, `Miasta`)
- Ludnosc 52.114 -> 52.070 mln (-44 tys. = -0.085% w 88 dob, **-0.35%/rok**); bez jednego zdobycia miasta -17.5 tys. (-0.14%/rok). Hearth 217 942 -> 217 715 (-0.43%/rok). Miasta: stopa naturalna -0.55%/rok, wsie +0.44%/rok; udzial miejski 19.9% (odniesienie 32.7%).
- **Uchodzcy poza domem 71.9 tys. -> 103.6 tys. (+375/d, +44%)** - powroty ~145/d nie nadazaja za nowymi 114-271/d.
- Pod bronia 1.01-1.05% mezczyzn. Partie rodow 77.4 -> 69.2 tys. (-10.6%), garnizony 69.4 -> 73.1 tys., milicje 87.6 -> 89.8 tys.
- Zabici 16 618 w 89 dob (187/d): partie rodow 6045, bandy 3803, tabory wsi 2663, milicje 1738, karawany 1181, garnizony 373, inne 815. Werbunek sr. 424/d, dezercja sr. 212/d (max 2484), zwolnieni z braku budzetu sr. 203/d ("ponad 1.10 x pulap 45-67 rodow").
- Najazdy: "rabunki zakonczone" = 0 przez 89 dob; spustoszenie idzie przez "zerowanie armii" i "rabunek osada po osadzie" - trafieni sr. 554/d (max 1644).

## 4. Zywnosc (`Spichlerze`, `Zima`, `Kiesa ludu`)
- Zima: doba 549 -> 637 z 1795 (do konca 1159 dob); ciecie plonu wsi srednio 40.8% (Polnoc 56%, freefolk 62%, poludnie Essos ~15%).
- Plon = 85-86.6% potrzeby wsi; spichlerze 442 -> 411 dni potrzeby (**-0.36 dnia/dobe**; 411 / 0.36 = ok. 1150 dob = dlugosc reszty zimy - zapas schodzi do zera dokladnie na koniec zimy, bez marginesu). Psucie 6%/rok.
- Glod: wsie 0. Warownie przy Food 0: 1-3 stale (Axe Castle, Elyria, New Ibbish); z ujemnym bilansem 19-31 z 227; zgony mieszczan 4.5-6/d (od poczatku kampanii 234). Zamki: polka targu < 10 dni zuzycia w 73 (56-90) ze 130.
- Lud miast: stac jedzenie W srednio 84.9% (70.8-93.3), pelny koszyk swiat **77.5%** (64.7-85.5); miast z "stac < 95%": 17-21. R/P bez jedzenia na jutro: Braavos, Rhyos, Lorath, Pentos, Bolozo (13-19-13.log, `Placa dla klas w biedzie` dz. 109814).
- Produkcja vs zuzycie towarow (`Towary (bilans)`): 40.9 tys. vs 40.8 tys. szt./d, "bez wyjasnienia +0".

## 5. Place, podatki, budzet rodow
- Zold naliczony sr. 538 tys./d (partie 361, zalogi 166, karawany lordow 11) = 52% pulapu 1.03 M; sakiewki = 7.9 dnia zoldu. D (staly dochod rodow AI) **441 -> 394 tys./d (-10.6%)**. Zold > D o 100-140 tys./d; luke pokrywaja: podatek od nadwyzki ludu do glow rodow ~84 tys./d, renty 180 sr. 60 tys./d, zwrot zoldu 36 tys./d, W5b 29 -> 43 tys./d (rosnie), W5c 6-8 tys./d, zawor miast sr. 195 tys./d.
- Podatek od nadwyzki ludu sr. 126 tys./d (= 0.6% zlota ludu na dobe). **Mnoznik straznika 3.700 = sufit przez 88 z 89 dob**; udzial ludu 9.0-9.6% wobec pasma 13.6-19.1%; "kiesy z potrzeba = 0.83 x poziom normalny". Regulator jest nasycony - nie steruje.
- Chleb biedoty ze skarbcow do kies ludu sr. 36 tys./d (14-76 tys.).
- "panowie / lud" (`Miara historyczna cz. 2`): 0.73-2.21, srednio 1.14 - panowie biora tyle, ile caly lud wydaje na zycie.

## 6. Warsztaty
- Towarowe: 291, pracuje srednio **121 (42%)**; wynik po placach +21.1 tys./d = 73 zl/warsztat/d (bakery 74, smithy 19, tartak -3, armorsmithy -10.8, velvet 1784); cykle bez wsadu 109 wobec 528 udanych (17%; brak: srebro, welna, len, glina + jedzenie).
- Zbrojne: 545 szt./d (1176 -> 578); cykle odpuszczone "brak zlota/kupca" **1092/d**, "bez zysku" 176/d; pula niesprzedanych wyrobow miast stale 3.26 M zl (49.8 tys. szt.) przy wyrobie 418 tys. zl/d.
- Place warsztatow do ludu sr. 44 tys./d (4-87 tys.); robocizna z kas 146 tys./d.
- Sprzedaz z polek miast (zl/d): konsumpcja 151.5 tys., partie 74.8 tys., uzbrojenie AI 57.0 tys., lud 44.2 tys. Zbytki: oferta 77 tys./d, kupione 35 tys. (45%), 907 par miasto-kategoria ponad cena graniczna.

## 7. Karawany
- Liczba 490-504 (notabli 360-370, lordow 127-137), 14.7-15.0 tys. ludzi; nowe 2.27/d (202 w 89 dob) przy stanie +13 -> **ok. 189 straconych (2.1/d, 0.43%/d)**; zabitych straznikow 1181.
- Oplacalnosc: `Rynek karawan (294)` dz. 109811 (13-19-13.log:8594): **placace 218, tracace 273 (44.4%)**, mediana (wynik - zold) -24 zl/d; p10 -153, p90 ~574; 10% najlepszych = 48-52% zysku; bez sprzedazy w 7 dobach 106-155 z ~499. Klucze "stratne karawany zwijane NIE, nowe karawany gdy handel placi NIE" - **liczba karawan nie reaguje na oplacalnosc**.
- Zysk do wlascicieli sr. 71 tys./d; doplaty notabli do 5000: +67 tys. (probka dz. 109770) - sektor dla wlascicieli blisko zera. Kiesy 2.28-2.52 M = 66% kapitalu obrotowego.
- Towar: popyt rynkow 25.8 tys. szt./d, karawany sprzedaja 2.28 tys./d (**8.8%**); w jukach 45.5 tys. szt. (koszt 2.5 M = 20 dob sprzedazy), 3-5 z 35 kategorii "KUPNO STOI". Marza miasta na dostawie karawany sr. +104% (44-198%; zywnosc +363%).
- Tabory wsi: utarg sr. 286 tys./d z kas miast; zabitych woznicow 2663 (30/d); zatkane wsie 16-36.

## 8. Zbojnicy
- Band 539-585 (-0.29/d), ludzi 10.6-11.5 tys.; **rotacja: 48 nowych band/d, 44 rozwiazanych do puli/d** (zycie bandy ~12 dob). Pula wyrzutkow 3.0 -> 5.6 tys. (+35/d), sredni pobyt 31 dob.
- Pieniadz: kiesy band 20-96 tys. (sr. 46 tys. = 4 zl na glowe), skarbce kryjowek 67 -> 46 tys.; paser ~300 zl/d, zycie w miastach ~2.9 tys./d. Razem ok. 0.04% zlota swiata - bandy sa presja wojskowa (43 zabitych bandytow/d, 30 woznicow/d, 13 straznikow karawan/d), nie gospodarcza.
- Szok wojenny dz. 109752-757 podniosl pule o 2.3 tys. i kiesy band do 96 tys., ale liczba band nie wzrosla (560-565).

## 9. Dlugi i miasta z pusta kasa
- Kasy miast: ponizej zapasu kupcow 40-51 z 97, ponizej polowy 29-34, ponizej 1/4: 27 (dz. 109814), ponizej 1000: 0-6, przy zerze 0-2; Highgarden 30 dob z rzedu, 32-33 miasta >= 28 dob. Rhyos kasa 263 przy zapasie 265 468.
- Dlugi miast: 14 -> 23 miasta, **3.19 -> 4.02 M** (szczyt 4.37 M); wierzyciele Bank 1.96 M, P 0.97 M, notable 0.93 M; niewyplacalne 4-8.
- **Kredyt kupiecki za zywnosc: 6.97 -> 7.55 M otwarte** (1259 rachunkow, 50 miast; Braavos 1.80 M, Rhyos 1.16 M) - zobowiazanie rzedu 64% wszystkich kas miast, rosnie ~7 tys./d.
- Rody: dlug w Banku 135 -> 206 tys., dluznikow 34 -> 44 (max 49), okupy 196 -> 267 tys., wobec rad miast 170 -> 362 tys.; 6-7 rodow w wyprzedazy z odzyskiem 0 (Falwell: 511 dob, 0 zl/d). Caly dlug rodow < 1% ich zlota (84 M) - kredyt nie jest hamulcem; bieda jest punktowa (biednych < 1000: 1-8 na krolestwo, "na minusie" do 43 rodow w KL).
- Bank: kapital 3.52 -> 3.23 M (min 2.92 M).

## 10. Werdykt z liczb
- **Stabilne:** suma pieniadza (+1.5%/rok, w calosci mennica), ludnosc (-0.35%/rok w zimie i wojnie), bilans towarow, liczba karawan i band, brak glodu wsi, arytmetyka ksiegi.
- **Stale dryfuje:** lordowie +30%/rok; kasy miast -54%/rok (w pierwszych 76 dobach); kiesy ludu miast -24%/rok; D rodow -10.6% w kwartal; dlugi miast +26%, kredyt kupiecki +8%, uchodzcy +44%, pula wyrzutkow +88% w 88 dob; skarbiec KL do zera w ~80 dob; spichlerze na styk do konca zimy.
- **Reaguje na wojne:** tak w skarbcach (-6.2 vs +1.2 tys./d), w wojsku (dezercja, zwolnienia), w ludnosci (zdobycie miasta, uchodzcy); nie reaguje liczba band ani karawan.
- **Nie reaguje / nasycone:** straznik obiegu (mnoznik na suficie), liczba karawan (klucze NIE), skarby krolestw w pokoju (32 M lezy).

## 11. Czego nie udalo sie ustalic
1. Czy odwrocenie trendu w ostatniej sesji (109803-815) jest trwale - 12 dob, jedna wersja DLL.
2. Ktore rody KL zebraly +8.2 M (linia `Skarbce` daje tylko sume krolestwa) i ile z tego to lup/okupy z Polnocy.
3. Zrodlo "inne z niczego" 431 tys. (dz. 109787), 133.5 tys. (109798), 63.8 tys. (109795) oraz reszty R ponad prog.
4. Reakcja systemu na wybuch/koniec wojny w czasie - w oknie 1 wojna i 1 pokoj.
5. Zachowanie poza zima (cale okno to zima, doba 549-637) i w skali roku - ekstrapolacje sa liniowe z 0.245 roku.
6. Ceny w czasie (inflacja cen, nie pieniadza) - nie wyciagalem szeregu indeksow cen; `HistoricalPrices`, `Zelazna cena`, `Stada`, `Rada`, `Majatki BK`, `Myto` nie byly analizowane.
7. Srednia doplat notabli do karawan (jedna probka) i dokladna liczba zniszczonych karawan (wyliczona z roznicy nowe - przyrost).
8. Przyczyna spadku D rodow (-47 tys./d).
