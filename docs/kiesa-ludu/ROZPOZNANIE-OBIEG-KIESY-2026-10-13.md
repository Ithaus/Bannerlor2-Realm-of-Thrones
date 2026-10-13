# ROZPOZNANIE: kiesa ludu i obieg pieniadza swiata (13.10.2026, tylko odczyt)

Pytanie Jeffa (13.10): "co zrobic z kasa, ktora lezy; w prawdziwej ekonomii pieniadz cyrkuluje i ma zamkniety obieg - ludzie moga
oszczedzac, ale tez wydaja, inwestuja itp. Jak to zrobic w ramach silnika gry, aby mialo to sens?"

To jest ROZPOZNANIE STANU (liczby i istniejace mechanizmy), bez wyboru rozwiazania. Zadnej zmiany w kodzie.

**Zrodla.** LOG = `D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_02-15-21.log` (bieg roczny 364 doby, WGRANIE 36; numer linii
jako `:NNN`). CSV = `...\Logs\2026-10-13_02-15-21\` (kiesa-ludu.csv - 53 migawki tygodniowe, budzet-rodow.csv, budowy.log). SRC = kod WGRANIA 36
(`...\scratchpad\zb\int\Armoury\src`). G = dekompilacja gry (`dec32\cs_full`), BK = dekompilacja BannerKings (`e4tools\bkfull`).
TRIAZ = `docs\test-zbiorczy\TRIAZ-3-bieg-roczny-2026-10-13.md` (O1-4, O1-7). Skrypty liczace: `...\scratchpad\kiesa\p1.py` - `p10.py`, `win.py`.
**Okna:** "rok" = srednia z dob 2-364 (pierwsza doba sesji jest niepelna), "koniec" = doby 301-364. Kwoty w zl na dobe calego swiata, chyba ze
napisano inaczej. 1 zl = 1 pens (model historyczny). Rok = 364 doby.
**Uwaga do calego biegu:** srednio 25 z 30 krolestw bylo w wojnie przez caly rok (linia "Korona: podatek wojenny ludu (184)", :569-:128861) -
to jest obraz gospodarki WOJENNEJ; pokoju ten bieg nie zmierzyl. Gracz stal bezczynnie (100 -> 49 818 zl).

---

## 0. Dziesiec zdan prostym jezykiem

1. Przez rok lud miast odlozyl 30.1 mln zl (z 6.4 do 36.6 mln), a razem z podzamczami i gminami 32.6 mln; to nie jest staly strumien - w pierwszym
   miesiacu przybywalo 196 tys. zl na dobe, w ostatnim 31 tys., i z miesiaca na miesiac jest tego mniej.
2. Lud zarabia srednio 539 tys. zl na dobe, a wydaje 449 tys.; 94% wydatkow to towar z targu, a podatki panu i koronie to razem 24 tys. (4.4% zarobku).
3. Lud nie wydaje wiecej, bo nie ma czego kupic: bierze z polek wszystko, co gra pozwala mu wziac (towar za 439 tys. zl na dobe, placi za 98%),
   piwo, plotno, mieso i ser schodza z polek w ciagu 0.2-1.8 doby, a "oferta nadwyzki" (3.3 mln zl na dobe pod koniec roku) nie kupila ani sztuki wiecej.
4. Zarobek ludu nie zalezy od jego wlasnych zakupow: 71% sprzedazy kupcow (z 1.27 mln zl na dobe) to wojsko, dwory, karawany i sprzet dla armii,
   a lud dostaje od kazdej takiej sprzedazy robocizne i cwierc wydatkow zolnierzy za uslugi - zarabia na wojnie wiecej, niz targ ma dla niego towaru.
5. Zloto lezy u wyrobnikow (87%, srednio 24 zl na glowe), nie u rodzin kupieckich (4%), i w niewielu miastach: 10 miast ma 40%, 30 miast 79%,
   a 31 miast ma razem 1.5% - w 15 miastach ludzi nie stac na pelny koszyk, w 10 nawet na chleb.
6. Bogate sa miasta, przez ktore ida armie i w ktorych siedza dwory (Riverrun, Meereen, Harrenhal, Dreadfort: zarobek 0.61 zl na glowe dziennie),
   biedne sa wyspy i kresy (Karhold, Hardhome, Arbor, Sisterton: 0.21 zl); towar i pieniadz sa w roznych miastach (aksamit lezy w Wolnych Miastach,
   ktorych lud jest bez grosza), a cena w grze nie widzi kiesy ludu, wiec karawany nie maja powodu wiezc towaru tam, gdzie sa pieniadze.
7. Lud to nie jedyna studnia: kompanie najemne zebraly ok. 17.5 mln (i rosna rowno ok. 49 tys. na dobe, bez gasniecia), sakiewki zolnierzy 13.4 mln
   (stoja od ok. 120. doby), wsie 9.1 mln (stoja od 90. doby, bo korona bierze z nich danine wojenna 180 tys. na dobe); zaplacily za to skarbce
   koron (-31.6 mln), kasy kupcow (-16.4 mln), notable (-6.4 mln) i wasale.
8. Droga powrotu pieniadza od ludu do panow jest dzis jedna i waska: lud kupuje towar -> kasa kupcow -> 7% nadwyzki dziennie panu i koronie
   po polowie (265 tys. zl na dobe); kies ludu nie siega ani danina wojenna, ani Bank, a wszystko, co lud "inwestuje" w place (budowy, septy,
   czeladz), wraca do tego samego ludu.
9. W kodzie i w cudzych modach sa juz rzeczy, ktore moga brac pieniadz ludu bez nowego wielkiego systemu: budowy miast i zamkow (198 z 227 stoi,
   bo placi je tylko pan; rozpoczete poziomy kosztuja 14.2 mln zl), Zelazny Bank z cala drabina dlugu (korony chca pozyczac o 11 tys. zl na dobe
   wiecej, niz Bank moze dac, i placa 20-30% rocznie), podatek wojenny ludu (dzis do 3% zarobku = 13 tys. na dobe), danina wojenna (dzis tylko
   z kas kupcow i wsi), dziesiecina i septy, zbytki na polkach (ok. 4.5 mln zl klejnotow, aksamitu i bawelny, ktorych oferta ludu nie obejmuje),
   uczty BK (0 w roku) i turnieje (552 w roku, bez puli zlota).
10. Historycznie taki pieniadz nie lezal: wyrobnik nie mial oszczednosci, majatek miasta trzymali kupcy, a wracal on do wladcow podatkiem
    nadzwyczajnym w latach wojny (ok. 2 pensy na glowe za jedno uchwalenie), pozyczkami pod zastaw cel i rent (15-40% rocznie dla krola, 5-12%
    renty miejskie) oraz budowa murow i kosciolow - gotowka w skrzyni byla wyjatkiem.

---

## 1. Bilans kiesy ludu

Zrodlo: linia "Kiesa ludu: dzien" (LOG :701 d1 ... :129003 d364; 364 linie, skrypt p2.py), kontrola linii "zmiana kies = wplywy - wydatki, r = 0".

### 1.1 Wplywy i wydatki (zl na dobe, 97 miast + 130 podzamczy)

| Pozycja | rok | koniec | udzial (rok) | skad w kodzie |
|---|---:|---:|---:|---|
| **WPLYWY razem (kontrola linii)** | **539 133** | **524 782** | 100% | |
| Robocizna handlu z kas kupcow (20% jedzenie / 10% towar / 4% bron od kazdej sprzedazy) | 158 870 | 157 910 | 29.5% | SRC TownFolk.Live.cs:760-786, Settings.cs:1605-1607 |
| Uslugi dla wojska: 25% wydatkow zolnierzy, zalog, ludzi karawan i band w miescie | 122 454 | 125 257 | 22.7% | TownFolk.EarnService (MenPurse.cs:316, Sutlers.cs:232, SoldierPay.cs:473, Seal31B.cs:454) |
| - w tym zycie zolnierzy i markietani / karawany / bandy / zalogi | 67 553 / 27 594 / 7 968 / 19 339 | 69 358 / 23 260 / 13 017 / 19 622 | | |
| Doplata kupcow do 35% wartosci wyrobow rzemiosla | 83 338 | 79 969 | 15.5% | Live.cs:777, Settings.cs:1608 |
| Place warsztatow (z kapitalu warsztatow) | 37 128 | 40 552 | 6.9% | WorkshopTrade.cs:334, WorkshopLaw.cs:331 |
| Obrot rodzin kupieckich (3% sprzedazy) | 33 417 | 33 690 | 6.2% | Live.cs:775-776 |
| Dwor panow (20% pieniedzy dworu) | 20 390 | 17 266 | 3.8% | ClanBudget.cs:645 |
| 5% zaworu kas miast | 12 634 | 11 135 | 2.3% | TownPurse.cs:420 |
| Place budow / kopalnie | 6 925 / 10 | 4 753 / 13 | 1.3% | BuildFunding.cs:187, :98 |
| Podzamcza: uslugi, dwor, 15% zaworu zamku (7 880) | 35 936 | 28 391 | 6.7% | CastlePurse.cs:623 |
| Podzamcza: robocizna z kas zamkow | 22 568 | 20 659 | 4.2% | Live.cs:881-889 |
| Reszta kontroli (dziesiecina wsi wyplacona ludowi jako place przy septach itp.) | ok. 5 500 | ok. 5 200 | 1.0% | TownFolk.Tithe.cs |
| **WYDATKI razem (kontrola linii)** | **449 194** | **486 462** | 100% | |
| Towar z targu miast - do kas kupcow (zaplacone 380 888 minus akcyza 7 617, ktora zostaje w gminie) | 373 271 | 404 707 | 83.1% | TownFolk.PayForGoods, Live.cs:448-503 |
| Towar podzamczy - do kas zamkow | 49 162 | 51 040 | 10.9% | tamze |
| Podatki panu od zarobku 1/2/3% (W/R/P) | 9 611 | 9 730 | 2.1% | TownFolk.cs:56, Live.cs:750-755 |
| Podzamcza 3% panu + farma gmin (1/3 wydatkow gminy) | 1 729 + 2 797 | 1 448 + 3 135 | 1.0% | Live.cs:793-808, :874-879 |
| Podatek wojenny korony (184), do 3% zarobku | 9 848 | 13 447 | 2.2% | KingdomTreasury.WarTax.cs:147, Settings.cs:1578 |
| Material robot gminy i septow do kas kupcow (dziesiecina ludu 9 588: polowa jalmuzna, z budowy 75% place - wraca do ludu) | ok. 2 800 | ok. 3 000 | 0.6% | TownFolk.Tithe.cs:11-24 |
| **SALDO (przyrost kies)** | **+89 940** | **+38 320** | | |

Rachunek kontrolny: przyrost stanu z linii "Pieniadz swiata" (:676 -> :128970): miasta 6 443 470 -> 36 585 403 (+30 141 933 = 83.0 tys./d),
podzamcza 672 814 -> 3 037 346 (+2 364 532), gminy 1 023 -> 123 749 (+122 726); razem +32.63 mln; suma dziennych "zmian kies" z kontroli 32.65 mln.

Przesuniecia WEWNATRZ ludu (nie wchodza do salda, rok): place z nadwyzki rzemieslnikow i kupcow dla wyrobnikow 133 894; czynsz W i R -> P 34 343;
akcyza 2% -> gmina 7 617; leczenie 3 986; roboty gminy (place) 4 206; cech 1 127; sluzba 858.

### 1.2 Przyrost gasnie

| Okno | przyrost kies/d | wplywy | wydatki | towar wziety z polek miast | oferta nadwyzki ludu | zjedzone bez zaplaty |
|---|---:|---:|---:|---:|---:|---:|
| d2-31 | 195 693 | 518 866 | 323 174 | 294 280 | 648 549 | 15 |
| d62-91 | 125 880 | 539 054 | 413 174 | 367 765 | 1 057 312 | 2 130 |
| d122-151 | 95 862 | 552 377 | 456 515 | 388 335 | 1 548 791 | 4 711 |
| d182-211 | 96 163 | 556 307 | 460 144 | 393 424 | 2 101 645 | 9 109 |
| d242-271 | 66 950 | 545 248 | 478 298 | 410 429 | 2 715 889 | 9 516 |
| d302-331 | 41 649 | 531 934 | 490 285 | 417 668 | 3 232 112 | 12 078 |
| d332-361 | 31 183 | 517 500 | 486 317 | 415 331 | 3 436 632 | 17 478 |

Wplywy stoja (517-556 tys.), wydatki rosna z 323 do 486 tys.; przyrost spada o ok. 13 tys. zl/d na miesiac. Lustro: zasilek ze skarbcow koron
(regula 165, 1/360 zapasu ponad 0.5 mln na dobe - SRC CrownIncome.cs:18, :169; linia "Obieg", :1038-:128980) 115 888 (d1-30) -> 37 896 (d301-363). Przedluzenie prostej daje
zero ok. doby 420-450 [S - ekstrapolacja, nie pomiar]. Zapas (36.6 + 3.0 mln) zostaje.

### 1.3 Dlaczego saldo jest dodatnie

1. **Wydatek ludu ma sufit, ktorym jest polka, nie kiesa.** BK zdejmuje z polki najwyzej `budzet kategorii / cena` i nie wiecej niz lezy
   (BK EconomyPatches.cs:514-560; budzet wiersza = popyt x indeks^0.15 + dobrobyt/1000/indeks, :1085-1100). Towar wziety z polek: miasta 385 636
   + zamki 53 802 = 439 438 zl/d (rok); lud zaplacil 380 888 + 49 162 = 97.9%; reszta to "zjedzone bez zaplaty" w biednych miastach (7 491) i kuchnia pana.
2. **Oferta nadwyzki nie kupuje.** Lud oferuje 15%/d kiesy ponad zapas (Live.cs:809-829, Settings.cs:1628): 0.65 mln/d (d2-31) -> 3.44 mln/d (d332-361).
   Budzet gry po ofercie 1.20 -> 3.57 mln/d, towar wziety 294 -> 415 tys./d. Dowod w sztukach (linie "Towary: dzien", p5.py): zakupy mieszczan
   8 kategorii "lepszych" (meat, cheese, oil, linen, velvet, beer, wine, pottery - Live.cs:41) liczone wedlug wartosci sztuki: 39 544 zl/d (d2-31)
   -> 37 934 zl/d (d302-364) - bez zmiany. Zakup = produkcja swiata: piwo 755 szt./d produkcji, mieszczanie 554; mieso 523 / 422; ser 422 / 225;
   plotno 140 / 94; garnki 82 / 60.
3. **Polki "lepszego" towaru sa puste, a zbytki leza gdzie indziej.** Stan polek miast w d364 wobec dziennych zakupow mieszczan: piwo 0.2 doby,
   plotno 0.3, mieso wielorybie 1.0, garnki 1.6, ser 1.7, mieso 1.8. Jednoczesnie na polkach miast rosnie zapas: klejnoty 197 -> 1 001 szt.
   (56 dni zakupow), aksamit 9 -> 304 szt. (68 dni), bawelna ok. 600 szt. - oferta ich nie rusza (klejnoty, futra, srebro, przyprawy nie sa na liscie
   "lepszych"; aksamit jest, ale lezy w miastach bez pieniedzy).
4. **Lud je ok. polowy koszyka historycznego i placi za to podwojnie.** Zakupy mieszczan wedlug wartosci sztuk: 201.5 tys. zl/d (d302-364, suma
   towarow z ksiegi "Towary") wobec koszyka projektu przy cenach bazowych 435 tys. zl/d (W 1 337 517 x 0.27 + R 121 246 x 0.50 + P 2 641 x 1.46 +
   niewolni 46 812 x 0.20; SRC TownFolk.cs:49) = 46% [S - ksiega towarow nie obejmuje odziezy i koni]. Zaplacone 413 tys. = 2.05 x wartosc.
   "Koszyk ludu" z linii (1.90 mln/d) to ten sam koszyk x sredni indeks cen popytu 4.3 - wiec nawet 36.6 mln to 19 dni pelnego koszyka po dzisiejszych cenach.
5. **Zarobek ludu jest przywiazany do CALEJ sprzedazy kupcow, a nie do zakupow ludu.** Linia "Podstawa robocizny (E5/B1)" (:704-:129004), rok:
   sprzedaz kupcow 1 272 205 zl/d = towar ludu 372 367 (29%) + zolnierze i zalogi 367 809 (29%) + karawany i gracz 291 619 (23%) + sprzet AI, odziez,
   budowy, zamki 152 603 (12%) + dwor 81 539 (6%) + wsie i gmina 6 267. 71% podstawy to kupujacy spoza ludu.
6. **Podatki bezposrednie ludu sa male:** pan 14 137 + korona 9 848 = 23 985 zl/d = 4.4% zarobku. Danina wojenna (1%/d nadwyzki kas kupcow,
   2.5%/d nadwyzki kies wsi; KingdomTreasury.cs:398-412, Settings.cs:947-950) kies ludu nie obejmuje - sa poza `Gold` osady (ustalenie TRIAZ O1-7).
7. **Lancuch konczy sie na wyrobniku.** Rzemieslnicy (R) i rodziny kupieckie (P) 70% swojej nadwyzki wyplacaja jako place wyrobnikom (134 tys./d),
   dlatego ich kiesy stoja (R 3.5 mln, P 1.36 mln od ok. d180), a wszystko zbiera sie w kiesie W, ktora ma jedno wyjscie: towar.
8. **Ceny nie reaguja na kiese.** Cena kategorii liczy sie z podazy na polce i popytu z dobrobytu / ludnosci (G DefaultSettlementEconomyModel.cs:61-73,
   BK BKEconomyModel.cs:535-554); kiesa ludu nie jest wejsciem. Wiecej pieniedzy u ludu nie podnosi cen ani produkcji.

---

## 2. Gdzie lezy zloto ludu (d364)

Zrodlo: kiesa-ludu.csv, migawka dnia 109199 (p6.py, p7.py - srednie z 52 migawek tygodniowych); linia :129003.

**Klasy (97 miast):** wyrobnicy W 31 627 079 zl (86.6%; 1 337 517 glow = 23.6 zl/glowe), rzemieslnicy R 3 511 274 (9.6%; 29.0 zl/glowe),
rodziny kupieckie P 1 363 098 (3.7%; 2 641 glow = 516 zl/glowe); gminy 123 749. Podzamcza 130 zamkow: 3 026 326.
Na start (d1): W 4 272 200 / R 1 681 668 / P 412 424 - przyrost prawie w calosci u W (+27.4 mln).

**Ile to dni:** wobec towaru naprawde kupowanego (417 172 zl/d w d364) 87 dni; wobec pelnego koszyka po dzisiejszych cenach (1 904 998 zl/d) 19 dni;
"zapas dni" z linii: W 31.9 / R 18.6 / P 56.0 (potrzeba liczona kodem - w bogatych miastach rowna pelnemu koszykowi, Live.cs:722). "Lokata"
(kiesa W ponad 10 dni potrzeby) 23 783 334.

**10 najwiekszych kies (40.0% = 14.60 mln):**

| # | Miasto (krolestwo) | kiesa d364 | start | dni pelnego koszyka | zarobek sr./d | wydatki sr./d | wyrobnikow |
|---|---|---:|---:|---:|---:|---:|---:|
| 1 | Riverrun (Riverlands) | 1 720 537 | 86 676 | 83 | 11 665 | 7 312 | 15 178 |
| 2 | Meereen (Targaryen) | 1 682 838 | 119 207 | 62 | 9 607 | 5 752 | 21 145 |
| 3 | Harrenhal (The North) | 1 496 599 | 55 045 | 110 | 6 714 | 3 609 | 11 308 |
| 4 | Dreadfort (The North) | 1 433 774 | 61 583 | 78 | 9 406 | 4 568 | 13 286 |
| 5 | Yronwood (Dorne) | 1 424 493 | 65 472 | 93 | 9 114 | 5 055 | 12 986 |
| 6 | The Eyrie (The Vale) | 1 408 530 | 84 359 | 50 | 11 191 | 7 562 | 19 051 |
| 7 | Qarkash (Qarth) | 1 385 264 | 54 776 | 96 | 7 708 | 3 711 | 10 299 |
| 8 | Mantarys (Targaryen, Aegon) | 1 370 190 | 92 135 | 62 | 8 258 | 3 920 | 16 532 |
| 9 | Vaes Diaf (Dothraki) | 1 363 895 | 71 834 | 75 | 6 910 | 3 253 | 12 124 |
| 10 | Hornvale (Baratheon KL) | 1 316 906 | 66 018 | 89 | 7 986 | 4 627 | 13 300 |

Dalej: King's Landing 1.06 mln, Highgarden 0.97, Oldtown 0.90, Vaes Dothrak 0.89, Duskendale 0.87. Top 20 = 64.0% (23.35 mln), top 30 = 79.4%
(28.96 mln), top 50 = 95.5%.

**Rozklad wedlug dni pelnego koszyka:** ponad 40 dni - 15 miast, 18.80 mln (51.5%); 20-40 dni - 18 miast, 11.09 mln (30.4%); 10-20 dni - 13 miast,
4.13 mln; 5-10 dni - 9 miast, 1.34 mln; 2-5 dni - 11 miast, 0.57 mln; ponizej 2 dni - **31 miast, 0.56 mln (1.5%)**. Mediana 8.3 dnia, 10. centyl 0.6,
90. centyl 57.7.

**Puste kiesy obok pelnych - tak.** 31 najbiedniejszych miast zaczelo rok z 2.04 mln i skonczylo z 0.53 mln (zarobek 86 803 zl/d wobec wydatkow
91 644). Sa to: Karhold (1 870 zl), Hardhome, Arbor, Sisterton, Driftwood Hall, Mormont Keep, Thenn, Evenfall Hall, Seagard, Celtigar Keep, Astapor,
Ten Towers, Stonehelm, Mhysa Faer, Lys, New Ghys, Weeping Town, Volon Therys, Valysar, Tyrosh, Myrth, The Twins, Vaith, Bolozo, Planky Town,
Deepwood Motte, Tolero Aeksio, Last Hearth, Aquos Dhaen, Lord Hewett's Town, Fairmarket. W d364 (:129003): miast, ktorych nie stac na jedzenie
(ponizej 95%): 10, na pelny koszyk: 15; "zjedzone bez zaplaty" rosnie z 0 do 17 478 zl/d.

| Grupa miast | kiesa start -> koniec (mln) | zarobek/d | wydatki/d | saldo/d | zarobek na glowe W | towar kupiony na glowe W |
|---|---|---:|---:|---:|---:|---:|
| 10 najbogatszych | 0.76 -> 14.60 | 88 559 | 49 369 | +39 190 | 0.61 | 0.32 |
| miejsca 11-30 | 1.50 -> 14.36 | 136 727 | 101 116 | +35 610 | 0.46 | 0.32 |
| miejsca 31-66 | 2.42 -> 7.00 | 176 507 | 162 296 | +14 211 | 0.37 | 0.32 |
| 31 najbiedniejszych | 2.04 -> 0.53 | 86 803 | 91 644 | -4 841 | 0.21 | 0.22 |

Towaru na glowe jest wszedzie tyle samo (0.22-0.32 zl), zarobku nie: bogate miasto zarabia 1.9 x tyle, ile targ ma dla niego towaru.
Wedlug krolestw: The North 4.71 mln (10 miast), Baratheon KL 4.32 (5), The Reach 3.04 (7), The Vale 2.99 (7), Riverlands 2.73 (6), Dothraki 2.25 (2);
Wolne Miasta prawie nic: Norvos 0.10, Braavos 0.07, Lorath 0.06, Pentos 0.06, Lys 0.05, Tyrosh 0.04 mln (1-3 dni koszyka).
Nie zmierzone: z czego dokladnie zarabia kazde miasto (CSV ma tylko sume zarobku) - "armie i dwory" to wniosek z listy miast, nie pomiar.

**Podzamcza (130):** 3.03 mln; 8 najwiekszych = 42.8% (Demongate 210 783, Gargos 201 251, City of Bones 181 065, Redfort 145 833); mediana 1 824 zl
(2 dni koszyka); 60 podzamczy ponizej 1 dnia, 30 ponad 20 dni. Szczyt 3.95 mln w d210, potem spadek.

---

## 3. Caly obieg swiata

### 3.1 Kto ile ma (linia "Pieniadz swiata", :676 d1 i :128970 d364; p1.py)

| Posiadacz | d1 (mln) | d364 (mln) | zmiana (mln) | na dobe | uwaga |
|---|---:|---:|---:|---:|---|
| Skarbce 30 krolestw | 57.93 | 26.34 | -31.59 | -87 023 | ok. 14-15 mln to rezerwa 0.5 mln na krolestwo (Settings.cs:1018) |
| Glowy rodow razem | 52.24 | 56.66 | +4.42 | +12 186 | |
| - w tym 11 kompanii najemnych | 2.87 (TRIAZ O1-4) | 20.33 | +17.5 | ok. +48 700 | rosnie rowno caly rok |
| - w tym wasale i krolowie | ok. 49.4 | 36.33 | ok. -13.1 | | suma rezerw R rodow 34.2 mln (budzet-rodow.csv) - stoja na rezerwie |
| Pozostali lordowie | 11.98 | 10.17 | -1.81 | -4 993 | |
| Notable (3 132) | 28.94 | 22.53 | -6.40 | -17 642 | pasmo gry 4 500-10 500 zl na notabla = 14.1-32.9 mln |
| Kasy miast (kupcy) | 27.29 | 10.86 | -16.43 | -45 251 | spadek w pierwszych 30 dobach (d30: 11.51); zapas kupcow ok. 7.6 mln |
| Kasy zamkow | 6.90 | 5.12 | -1.79 | -4 925 | zapas ok. 4.3 mln |
| **Kiesy ludu miast** | **6.44** | **36.59** | **+30.14** | **+83 035** | |
| Podzamcza + gminy | 0.67 | 3.16 | +2.49 | +6 851 | |
| Sakiewki zolnierzy | 0.33 | 13.70 | +13.37 | +36 823 | szczyt 16.63 w d210; 22 dni zoldu |
| Kiesy wsi (571) | 1.11 | 10.19 | +9.08 | +25 010 | d90: 9.41 - potem stoja |
| Karawany | 3.77 | 2.29 | -1.48 | -4 086 | 440 karawan |
| Bank Zelazny (kapital) | 5.00 | 2.83 | -2.17 | -5 970 | do tego naleznosci 2.67 mln (korony 1.98, rody 0.69) |
| Kapital warsztatow | 2.57 | 2.53 | -0.04 | | |
| Garnizony / tabory wsi / bandy | 1.18 / 0.01 / 0 | 1.51 / 0.73 / 0.46 | +1.52 | | |
| Liczniki cel i podatku wsi | 2.04 | 0.21 | -1.83 | | zloto w drodze do panow |
| Gracz | 0.0001 | 0.05 | | | bezczynny |
| **Razem** | **208.40** | **206.03** | **-2.37** | -6 537 | 2.0 mln to blad wezwan 168 (TRIAZ O1-7 A) |

### 3.2 Glowne przeplywy (zl na dobe; linia "Obieg", "Przeplywy osad", "Gotowka wsi", "Podstawa robocizny"; p3.py, p4.py, p10.py)

| Od -> do | rok | koniec |
|---|---:|---:|
| Rody -> zolnierze: zold partii / zalog / karawan | 423 316 / 178 448 / 8 008 | 405 424 / 174 455 / 4 138 |
| - z tego do sakiewek ludzi / do kas miast (zalogi) / do kas zamkow | 483 407 / 77 500 / 38 042 | 459 080 / 78 643 / 38 240 |
| Rody -> kasy siedzib (dwor; w tym sprzet niewydany 76 763) | 178 193 | 141 104 |
| Rody -> kasy miast (zakupy sprzetu AI) | 115 549 | 128 366 |
| Rody -> budowy (place 10 255 + material 3 468) | 13 723 | - |
| Zolnierze -> miasta: zycie w miescie / markietani | 152 453 / 171 537 | 134 845 / 180 437 |
| Zolnierze -> wsie: markietani / ze zwolnionymi | 29 988 / 40 079 | 31 551 / 47 039 |
| Zolnierze -> zwyciezcy bitew (sakiewki rozbitych) / panowie (trzecia) | 64 540 / 29 344 | 90 355 / 40 683 |
| Kupcy -> wsie (utarg taborow) | 305 543 | 301 310 |
| Kupcy -> warsztaty za wyroby (minus wsad 30 798) | 193 184 | 191 797 |
| Kupcy -> lud (robocizna, obrot P, doplata 35%) + podzamcza | 275 625 + 22 568 | 271 569 + 20 659 |
| Wojsko, dwor, budowy, warsztaty -> lud (czesc ludu) + podzamcza | 186 906 + 28 056 | 187 841 + 21 741 |
| **Lud -> kupcy (towar): miasta / zamki** | **373 271 / 49 162** | **404 707 / 51 040** |
| **Lud -> panowie / korona** | **14 137 / 9 848** | **14 313 / 13 447** |
| Kasy miast -> pan / korona / lud (zawor 7% nadwyzki) | 145 124 / 120 460 / 12 634 | 133 245 / 105 927 / 11 135 |
| Kasy zamkow -> pan / korona / podzamcze | 30 171 / 14 864 / 7 880 | 25 510 / 12 568 / 6 650 |
| Wsie -> panowie (podatek z utargu + oplaty stale) / majatki BK | 121 468 / 22 089 | 112 717 / 24 039 |
| Wsie -> korona (danina wojenna) | 180 101 | 191 851 |
| Korona: wplywy dnia + z zapasu 1/360 | 437 203 + 71 206 | 439 493 + 37 896 |
| Korona -> rody: zwrot zoldu / renty wedlug lenn (180) | 248 192 / 213 794 | 230 493 / 187 960 |
| Korona -> kompanie najemne (kontrakty 185) / dary (182) | 23 394 / 14 602 | 26 080 / 11 182 |
| Warsztaty i karawany -> notable (dochod) | 89 385 | 84 043 |
| Notable -> kasy miast (ponad 10 500) / kasy -> notable (ponizej 4 500) | 69 032 / 112 412 | 61 278 / 85 206 |
| Notable -> ludzie karawan (doplaty do zoldu; zold karawan 125 096) | 90 637 | 59 241 |
| Bank -> korony / korony -> Bank; Bank -> rody / rody -> Bank | 10 072 / 5 445; 9 380 / 14 717 | 13 072 / 11 134; 12 482 / 28 026 |

Wplywy koron (rok): danina wojenna 205 322 (wsie 180 101, kasy miast ok. 25 tys.), pol zaworu miast 120 460, 1/9 lupow i okupow 26 489, podatek BK
od bogatych 21 929, 1/3 zaworu zamkow 14 864, podatek wojenny ludu 9 848, clo 9 436, powinnosci 4 553.
Dochod rodow (linia "D staly (169c)", :615-:128911, rok): ziemia 224 348 (koniec 161 009), korona 492 820 (439 075), jednorazowe - lup, okupy - 304 966.

### 3.3 Gdzie pieniadz staje

| Studnia | stan d364 | przyrost w roku | czy rosnie dalej | wyjscie dzis |
|---|---:|---:|---|---|
| Lud miast (wyrobnicy) | 36.6 mln | +30.1 mln | tak, coraz wolniej (31 tys./d) | tylko towar z polki; podatki 4.4% zarobku |
| Kompanie najemne (11 rodow, 2 494 ludzi) | 20.3 mln | +17.5 mln | tak, rowno ok. 49 tys./d | zold wlasnych ludzi; bez dworu, budow i pulapu z kiesy (TRIAZ O1-4) |
| Sakiewki zolnierzy | 13.7 mln | +13.4 mln | nie (od ok. d120) | zycie w miescie, markietani, naprawy; w tym "oszczednosci" 3.5-5.0 mln |
| Kiesy wsi | 10.2 mln | +9.1 mln | nie (od d90) w wojnie; w pokoju nie zmierzone | danina wojenna 2.5%/d nadwyzki; zakupy w miescie 4 869 zl/d z potrzeby 71 182 (6.8% - "wsi bez towaru 500 z 571") |
| Podzamcza | 3.0 mln | +2.4 mln | nie (spada od d210) | towar zamku |
| Rezerwy z reguly: skarbce (0.5 mln x 30), zapas kupcow (7.6 + 4.3 mln), rezerwy R rodow (34.2 mln), pasmo notabli (min. 14.1 mln) | ok. 75 mln | - | stoja z definicji | - |

Caly swiat ma 206 mln zl, a caly zold doby to 610 tys. zl - zloto swiata to 338 dni zoldu. Pieniadz stoi wiec nie tylko u ludu; lud wyroznia sie tym,
ze nie ma ZADNEJ reguly wyjscia poza towarem. Skala wobec potrzeby panow: zold partii polowych 423 tys./d; 10 tys. zl/d, ktore wroci do panow,
to realnie 0.3-0.45 tys. ludzi w partiach (TRIAZ O1-7 pkt 5).

---

## 4. Co juz istnieje (ujscie albo inwestycja bez nowego wielkiego systemu)

| # | Mechanizm | Kto placi -> kto dostaje (dzis) | Liczby z biegu | Gdzie | Ocena dla pieniadza ludu |
|---|---|---|---|---|---|
| 1 | **Budowy miast i zamkow** | pan: 10% dochodu dnia; 25% material z targu (kupcy), 75% place i wozy (kasa osady, z tego lud W60/R40); w wojnie tylko wojskowe | 13 723 zl/d (319 pkt/d); 69 poziomow w roku; d364: 198 z 227 budow STOI (176 cywilnych wstrzymanych wojna, 4 bez materialu; LOG :128765-:128766); rozpoczete poziomy: 432 814 pkt = 14.2 mln zl (miasta 9.9 mln); cena punktu 48 zl wojskowe / 24 zl cywilne | SRC BuildFunding.cs:13-24, :142-187; Settings.cs:565-568; budowy.log; p9.py | Lud nie placi nic. Pojemnosc 14.2 mln na same rozpoczete poziomy. Uwaga: 75% to place, ktore wracaja do ludu (przesuniecie miedzy klasami, nie odplyw); do kupcow idzie 25% |
| 2 | **Zelazny Bank + drabina dlugu** | kapital Banku -> rody (kredyt wojenny) i korony (186); splaty i odsetki -> kapital | kapital 5.00 -> 2.83 mln; dlug koron 1.98 mln u 25 krolestw (20-30% rocznie, rata 1/182), dlug rodow 0.69 mln u 55; korony chca 25 164 zl/d, pula daje 14 143 ("przycieta o 11 021", :128863); dlugi okupow 2.68 mln (:128908); zaleglosci 0 | IronBank.cs:15-37, DebtLadder.cs:20-42, CrownBorrow.cs:9-26; Settings.cs:965, :1113-1117 | Jedyny pozyczkodawca swiata; depozytow nie przyjmuje (kapital staly). Zasady ceny, limitu, zaleglosci, zajecia i wyprzedazy sa gotowe i dzialaja takze dla gracza |
| 3 | **Podatek wojenny ludu (184)** | lud -> skarbiec krolestwa w wojnie, od zarobku: do 3% (6% z polityka War Tax), wagi W 0.5 / R 1 / P 1.5 | 9 848 zl/d rok, 13 447 koniec; d364: 20 z 26 krolestw w wojnie na pulapie 3%, srednia stawka 2.54% | KingdomTreasury.WarTax.cs:147; Settings.cs:1578; LOG :569-:128861 | Dziala; podstawa to zarobek doby, nie zapas - 36.6 mln zapasu placi 13 tys./d |
| 4 | **Danina wojenna (pietnastka i dziesiata)** | kasy kupcow 1%/d nadwyzki, kiesy wsi 2.5%/d nadwyzki -> skarbiec | 205 322 zl/d (wsie 180 101) | KingdomTreasury.cs:386-412; Settings.cs:947-950 | Kies ludu miast nie obejmuje; to ona zatrzymala kiesy wsi na 10 mln (wniosek TRIAZ O1-7 B) |
| 5 | **Podatki pana od zarobku ludu** | lud -> pan: W 1% / R 2% / P 3%, podzamcze 3%; cech 1% R i akcyza 2% -> gmina, z niej 1/3 "farma" panu | 14 137 zl/d | TownFolk.cs:56-58; Live.cs:750-808 | Dziala; stawki wybral projekt E5 |
| 6 | **Zawor kas kupcow** | kasa miasta: 7%/d nadwyzki ponad zapas -> lud 5%, reszta pan i korona po polowie; zamek: pan 2/3, korona 1/3, podzamcze 15% | pan 145 124 + 30 171, korona 120 460 + 14 864 | TownPurse.cs:31-35 (R4), :420; CastlePurse.cs:623; Settings.cs:741, :993-995, :1588 | Jedyna dzisiejsza droga "lud -> pan": przez towar. Kazdy zloty wydany przez lud u kupcow wraca ta droga |
| 7 | **Renty korony (180) i zwrot zoldu (165)** | skarbiec -> rody tego samego dnia | 213 794 + 248 192 zl/d | CrownRents.cs:12-46; CrownIncome.cs:18, :169 | Gotowy rozdzielnik: co wejdzie do skarbca, tego dnia idzie do rodow |
| 8 | **Dziesiecina, jalmuzna, septy** | lud W/R/P 1/2/3% zarobku + wsie 5% utargu -> kaplan: 50% jalmuzna (kiesy W / wsi), 50% budowa septow (75% place ludu, 25% material kupcom) | 24 258 zl/d (lud 9 588, wsie 14 670); jalmuzna 11 962; place 8 972; material 2 990 | TownFolk.Tithe.cs:11-24; Settings.cs:1627 | Dziala; prawie w calosci zostaje w ludzie |
| 9 | **Oferta nadwyzki ludu ("lepszy towar")** | lud W: 15%/d kiesy ponad 10 dni potrzeby jako dodatkowy budzet 8 kategorii; R ponad 21 dni i P ponad 30 dni: 70% place dla W, 30% towar | oferta 3.34 mln/d (koniec), kupione dodatkowo: 0 w sztukach (rozdz. 1.3 pkt 2) | Live.cs:41, :380-399, :809-829; Settings.cs:1613, :1628 | Jedyny czynny mechanizm "wydawania oszczednosci"; trafia w puste polki |
| 10 | **Koszyk BK a zamoznosc** | - | popyt = dobrobyt / ludnosc klas; kiesa nie jest wejsciem; bogatszy lud nie kupuje ani wiecej, ani drozej poza pkt 9 | G DefaultSettlementEconomyModel.cs:61-73; BK BKEconomyModel.cs:535-554; BK EconomyPatches.cs:505-560, :1085-1100 | Silnik nie ma "bogatszy kupuje lepiej"; cena nie widzi kiesy |
| 11 | **Zbytki na polkach** | - | polki miast d364 (wartosc sztuki): srednia z 7 ostatnich dob - klejnoty 1 021 x 2 400, aksamit 300 x 4 000, bawelna 640 x 1 000, filc 656 x 200, futra 124 x 200, srebro 852 x 20, przyprawy 23 x 270 = ok. 4.47 mln zl; swiat z karawanami ok. 13 mln; produkcja ok. 150 tys. zl/d wedlug wartosci (klejnoty 20 szt./d, bawelna 48, aksamit 6, filc 160) | linie "Towary: dzien" (:128773-:128812), p5.py | Towar jest, ale poza lista oferty i nie w tych miastach, gdzie pieniadz |
| 12 | **Warsztaty** | wlasciciel (notabl; gracz i lord moga kupic): utrzymanie 4 zl/d i place do kasy miasta / ludu; zysk ponad kapital -> wlasciciel | 291 warsztatow towarowych, pracuje 194; kapital razem 2.53 mln (start 2 000 zl, zbrojne 10 000); wynik +7 622 zl/d razem (26 zl na warsztat); cena = 3 lata zysku; nadwyzka ceny wyrobow ponad koszt x 1.25 zostaje kupcom 78 846 zl/d | WorkshopTrade.cs:21-57; WorkshopLaw.cs:331; Settings.cs:581-613; LOG :425-:128727 | Lud nie jest wlascicielem. Przy "cenie sprawiedliwej" warsztat prawie nie zarabia - zysk z pustej polki zostaje u kupcow |
| 13 | **Karawany notabli** | kapital nowej: kiesa notabla ponad 4 500 + nadwyzka kasy miasta startu | 440 karawan, kasy 3.77 -> 2.29 mln; nowe 4 na dobe, kapital 28 989 zl/d (z kas miast 25 358); zold ludzi karawan 125 096 zl/d | NotableGold.cs:16-57; CaravanCapital.cs:14-34; LOG :1039-:128981 | "Kupcy inwestuja" juz jest (kasa miasta doklada kapital); kiesa ludu nie uczestniczy |
| 14 | **Czynsz, leczenie, sluzba** | W 8% i R 13% wydatku -> P; leczenie -> R; sluzba P -> W | 34 343 + 3 986 + 858 zl/d | TownFolk.cs:51-53; Live.cs:787-791 | Obieg wewnatrz ludu; do notabli czynsz NIE idzie (kod czynny daje calosc P) |
| 15 | **Zakupy wsi w miescie targowym** | wies -> kasa miasta: 0.07 zl/glowe/d + 15%/d nadwyzki, 5 kategorii (sol, narzedzia, plotno, garnki, piwo) | kupione 4 869 zl/d z potrzeby 71 182; chciane z nadwyzka 1.30 mln/d | Live.cs:42, :910-998; Settings.cs:1621 | Ten sam sufit co u ludu: brak towaru |
| 16 | **Majatki BK** | udzial z utargu wsi -> wlasciciel (notabl, lord); kupno majatku = przelew kupujacy -> wlasciciel | 1 858 majatkow, 22 089 zl/d | BK EstateAction.cs:39; LOG "Majatki BK (187)" (:129024) | Gotowa "ziemia do kupienia" z dochodem; dzis bez udzialu ludu |
| 17 | **Uczty BK** | gospodarz kupuje jedzenie, wino i piwo z targu miasta (kiesa -> kasa miasta) | 0 zakupow w 364 doby | BK Feast.cs:184-213; SRC MercFeast.cs:18-47; LOG "Najemnicy i uczta BK (E8/G2)" | Martwe u AI w tym biegu |
| 18 | **Turnieje** | nagroda = przedmiot; puli zlota dla AI nie ma; turnieje BK nieczynne | 552 turnieje w roku (1.5 na dobe), 9-10 w toku | LOG :691-:128996; BK BannerKingsTournament.cs:19-28 | Zdarzenie jest czeste; pieniadz przez nie nie plynie |
| 19 | **Gminy** | akcyza i cech -> gmina; wydaje 7%/d: 1/3 panu, 2/3 roboty (75% place, 25% material) | stan 123 749 zl; farma 2 797 zl/d | TownFolk.cs:58; Live.cs:793-808 | Istniejaca "kasa miejska ludu" - mala, bez dlugow i pozyczek |
| 20 | **Cla i myto** | prowizja od sprzedazy partiom -> licznik cel -> pan; clo -> skarbiec | 22 013 + 9 436 zl/d | LOG "Przeplywy osad (przyczyny)", "Obieg" | Bez zwiazku z kiesa ludu |
| 21 | **Gildie BK** | typ Guild (Merchants, Masons, Metalsmiths) z dochodem i wplywem | w logach brak sladu | BK Guild.cs:11-30, DefaultGuilds.cs:12-16 | NIE SPRAWDZONE, czy BK przypisuje je osadom w tej wersji |
| 22 | **BetterEconomy** (skarbce zamkow, inwestycje we wsie, umowy handlowe, dostep do targu) | - | wszystkie okna 0 trafien w roku | LOG "Obieg: BK i BetterEconomy" (:1040 ...), "Obieg: okna (kontrolka)" | Nieczynne albo zamkniete naszymi latkami |

Pomysl odlozony przez Jeffa "tylko na jego slowo" (PLAN-DO-KONCA-MODA-2026-10-09.md rozdz. 3, wpis 07.10): "kasy miast z dlugami, pozyczkami i
podatkiem nadzwyczajnym" - miasto jako podmiot z rezerwa, dlugiem i wlasna zbiorka. Z tego rozpoznania: dzisiejsze "kasy miast" to pieniadz
KUPCOW (10.9 mln, w rownowadze), a nie ludu; kasa ludu to gmina (0.12 mln). W projekcie kiesy z 09.10 byla "lokata ludu u kupcow z pulapem
30 dni" (PROJEKT-KIESA-LUDU-2026-10-09.md:60, :231 "zbiornik bez wyjscia: zaden"; prog "lokata < 5 dni", :491) - w kodzie B1 pulap pominieto
(PLAN-KODU-E5B1.md:40-42: "niewydana nadwyzka zostaje w kiesie W").

Koszt dzisiejszej kiesy ludu: latki 8.4 ms + blok doby 1.5 ms na dobe (:129003) - kazda nowa regula ma do dyspozycji ulamek tego.

---

## 5. Jak krazyl pieniadz mieszczan (Europa XIII-XV w.) - 10 zdan

Liczby z literatury przytaczane z pamieci (Mayhew, Dyer, Ormrod, Fryde, Munro, Britnell) - rzad wielkosci, nie do cytowania co do funta.
1 funt = 240 pensow (zl); glownie Anglia, bo na niej stoi nasz model cen.

1. **Monety bylo malo i krazyla szybko:** Anglia ok. 1300 miala ok. 1 mln funtow monety (240 mln pensow na 4.5-5 mln ludzi, czyli ok. 50 pensow na glowe)
   przy dochodzie kraju 4-5 mln funtow rocznie - moneta obracala sie 4-5 razy w roku; nasz swiat ma 206 mln zl, a caly zold roku to 222 mln.
2. **Wyrobnik nie mial oszczednosci:** dniowka ok. 1.5 pensa przed 1349 i 3-4 pensy po zarazie; podatek od ruchomosci nie obejmowal majatku ponizej
   10 szylingow na wsi i 6 szylingow w miescie i placila go 1/3 do 1/2 gospodarstw; w spisach z 1524 gorne 5-7% podatnikow wielkich miast
   mialo ok. polowy opodatkowanego majatku - u nas odwrotnie: 87% zlota ludu maja wyrobnicy.
3. **Podatek nadzwyczajny** (pietnastka ze wsi, dziesiata z miast, od 1334 staly kontyngent): ok. 38 tys. funtow z calej Anglii za jedno uchwalenie,
   czyli ok. 2 pensy na glowe = 1-2 dniowki wyrobnika; w latach wojny 1-3 naraz (1337-40: trzy, ok. 114 tys. funtow), w pokoju wcale.
4. **Clo jako zastaw:** stale clo od welny 6 szylingow 8 pensow od worka (ok. 30 tys. workow rocznie = 10-13 tys. funtow), w wojnie subsydium
   40-50 szylingow od worka dawalo 60-80 tys. funtow rocznie - najwiekszy pojedynczy dochod korony i zabezpieczenie jej pozyczek.
5. **Pozyczki kupcow dla krola:** Riccardi z Lukki ok. 400 tys. funtow w 22 lata (1272-94) w zamian za pobor cel; Bardi i Peruzzi w 1337-45 dlug
   rzedu 100-200 tys. funtow (krol nie splacil, oba domy upadly); William de la Pole z Hull ponad 100 tys. funtow w 1338-39; Londyn jako miasto
   pozyczal po 5-20 tys. funtow naraz; cena 15-40% rocznie, ukryta jako "dar" albo "odszkodowanie".
6. **Renty miejskie:** miasta kontynentu sprzedawaly renty - dozywotnia 10-12.5% (1 za 8-10), wieczysta wykupna 5-6.7% (1 za 15-20); kupowali je
   mieszczanie, wdowy, cechy i szpitale; w XV w. obsluga dlugu zjadala 30-60% budzetu wielu miast Flandrii i Rzeszy (Gandawa, Kolonia, Bazylea, Lubeka).
7. **Pozyczka przymusowa i dlug publiczny:** Wenecja od 1262 (Monte Vecchio, 5%), Florencja Monte Comune 1345 - ok. 0.5 mln florenow dlugu w 1345,
   ok. 3 mln w 1400, 8 mln w 1450; obywatel dostawal 5-8% i mogl swoj udzial sprzedac.
8. **Mury:** krol nadawal miastu "murage" - myto od wwozonego towaru na budowe i naprawe murow; miasta francuskie w wojnie stuletniej wydawaly
   na obwarowania 40-80% budzetu rocznego (Tours, Rennes, Dijon), mur sredniego miasta to kilka do kilkunastu tysiecy funtow rozlozone na
   dziesieciolecia, robocizna ok. 2/3 kosztu.
9. **Koscioly, cechy, fundacje:** dziesiecina 1/10 plonu; kupcy welniani fundowali koscioly (Lavenham, Long Melford - tysiace funtow, pojedyncze
   zapisy po 200 funtow); bractwa i cechy braly wpisowe od 6 szylingow 8 pensow do 3 funtow i utrzymywaly kaplice, przytulki i uczty; katedra
   rosla po 200-600 funtow rocznie przez pokolenia (Exeter, York), opactwo westminsterskie Henryka III ok. 40-45 tys. funtow w 25 lat.
10. **Handel dalekosiezny i ziemia:** majatek bogatego kupca londynskiego 1-5 tys. funtow, ladunek statku 300-1 000 funtow, eksport welny
    30-40 tys. workow = 200-250 tys. funtow rocznie; zysk szedl w towar, udzialy w statkach, domy czynszowe i ziemie pod miastem (czynsz 5-8%
    wartosci) - gotowka lezaca w skrzyni byla wyjatkiem.

---

## 6. Czego to rozpoznanie NIE ustalilo

- Ile oferty nadwyzki naprawde kupiono w zlocie - log podaje tylko oferte (brak licznika; ten sam brak wskazal TRIAZ O1-7). Wniosek "nic" opiera sie
  na sztukach z ksiegi towarow, nie na pomiarze wprost.
- Skladu zarobku pojedynczego miasta (wojsko / dwor / handel) - kiesa-ludu.csv ma tylko sume.
- Zachowania w POKOJU: 25 z 30 krolestw walczylo caly rok; danina i podatek wojenny w pokoju nie dzialaja, wiec kiesy wsi i ludu moga wtedy rosnac inaczej.
- Roku 2: czy przyrost dojdzie do zera (ekstrapolacja z 1.2), i co sie stanie, gdy zasilek 165 wygasnie.
- Czy gildie BK (pkt 21) sa zywe; dlaczego AI nie urzadza uczt BK (pkt 17).
- Sredni indeks cen 4.3 w "koszyku ludu" to srednia wazona popytem takze kategorii z pusta polka - realnie placone ceny to ok. 2 x wartosc sztuki.
