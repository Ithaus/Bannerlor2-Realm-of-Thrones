# ROZPOZNANIE: LEZACE OSZCZEDNOSCI LUDU (A5) I BIEDOTA WIELKICH MIAST W ZIMIE (A4) - 14.10.2026, tylko odczyt

Zakres: inwentarz `docs/CO-ZOSTALO-DO-KONCA-MODA-2026-10-14.md` A5 i A4. Bez zmian w kodzie, bez commitow, gra nieuruchamiana.
Znaczniki: **[P]** pomiar z logu / tabel / kodu, **[S]** szacunek albo wniosek; czego nie da sie ustalic - "nie wiem" + linia diagnozy (rozdz. 10).

**Zrodla.**
- LOG dwuletni (W49 + lup 5b, kod 43af413, 728 dob): `D:\Backup-Bannerlord\logs\2026-10-14-noc\Armoury-2026-10-13_21-14-13.log`, tabele `...\Logs\2026-10-13_21-14-13\` (`kiesa-ludu.csv`, `kiesa-obieg.csv`, `kasy-miast.csv`, `skale-ludzi.csv` - migawki co 7 dob).
- Z.1 (zapis d720 + 60 dob, fin24): `...\Armoury-2026-10-14_02-30-30.log` (doby 720-781). W52 120 dob (fin25 = Z.1 + plotno, nowa kampania): `...\Armoury-2026-10-14_02-50-28.log`.
- Porownania roczne: W43 `...\Armoury-2026-10-13_15-02-59.log`, GL (gospodarka od ludzi bez reszty kiesy) `...\Armoury-2026-10-13_19-21-12.log`.
- Kod: drzewo `zb\pr2` (galaz `w-toku/pieniadze-rok2` od 449d453); gra `dec32\cs_full`.
- Linie logu: "Kiesa ludu: dzien", "Danina z oszczednosci (P1)", "Zbytki ludu (P3-P5)", "Roboty ludu (P2)", "Stawka wojenna pana (W7)", "Korona: podatek wojenny ludu (184)", "Konie robocze (K1-3a)", "Podstawa robocizny (E5/B1)", "Glod ludu (E5/C2)", "Zima (E4/0)".

Doba = dzien gry - 108836 + 1. Kwartaly po 91 dob: Q1 d2-91, Q2 d92-182, Q3 d183-273, Q4 d274-364 (koniec roku 1), Q5 d365-455, Q6 d456-546, Q7 d547-637, Q8 d638-728 (koniec roku 2). Zima ROT od d460. Kwoty: zl na dobe calego swiata (tys. zl/d), chyba ze napisano inaczej.
"Lokata" = kiesa wyrobnikow (W) miast ponad 10 dni potrzeby (definicja linii "Kiesa ludu"). "Potrzeba" = dzienny koszyk klas (W+R+P) z kotwicy (`TownFolk.Live.cs` Anchor). "Stac" = kiesa / potrzeba dnia (1.00 = stac).

## 0. Dla Jeffa - osiem zdan

1. Mieszczanie zarabiaja prawie dwa razy tyle, ile moga kupic na targu (koniec roku 1: 761 tys. zl dziennie zarobku wobec 343 tys. na towar), a od paczki koni roboczych lud nie "zjada" juz koni z polek, wiec wydaje jeszcze o ok. 110-140 tys. zl dziennie mniej.
2. Te nadwyzke w calosci zabiera tylko danina z oszczednosci - a ona z kodu dziala wylacznie w wojnie i przy potrzebie korony; zbytki znajduja na polkach towar na 18-35% tego, co lud chce kupic, a budowy ludu biora 4-7 tys. zl dziennie, bo brakuje materialu.
3. W roku 1 danina doszla do 453 tys. zl dziennie i zbila lokate z 26 do 15.5 mln - to jest rownowaga tej reguly przy dzisiejszym zarobku, wiec prog 6 mln nie jest osiagalny nawet w pelnej wojnie.
4. W roku 2 16-20 krolestw zawarlo pokoj, danina u nich stanela i ich lokata urosla z 0.8 do 16.6 mln (Dothrakowie, Dolina, Dorne, Targaryen); z oszczednosci miast w pokoju wychodzi 0.3-0.8% dziennie, w miastach placacych 2.4-3.1%.
5. Pieniadz lezy tam, gdzie miasto ma maly rynek, a duzo handlu i wojska: 55 miast zarabia 3 i wiecej razy tyle, ile potrzebuje, i trzyma 74% lokaty (Vaes Diaf, Vaes Dothrak, Ifequeveron, Saath).
6. Biedota jest dokladnie odwrotnie: w wielkich miastach wydatek ludu rosnie z dobrobytem (gra liczy popyt od dobrobytu, a ten urosl o 45% w dwa lata), a zarobek nie - w roku 2 spadl o 19%, bo pokoj zabral wojsko; 10 miast zarabia mniej, niz wydaje, a luka urosla z 14 do 46 tys. zl dziennie.
7. Kazde z 7 miast ma swoj powod: Oldtown ma za duzy rynek (5.7 x sredni) i jest biedne od doby 183, Braavos i Rhyos maja pusta kase kupcow i ich lud stracil 70% zarobku, w Highgarden danina zdjela oszczednosci, a potem wydatek przerosl zarobek, Aquos Dhaen, Volon Therys i Selhorys stracily wojsko w pokoju; drozsze jedzenie w zimie (+7% koszyka w swiecie) tlumaczy tylko Braavos.
8. A4 i A5 to jedna sprawa: zarobek ludu nie idzie za ludzmi i dobrobytem, a wydatek tak - pieniedzy jest dosc (luka biedy 46 tys. zl dziennie przy nadwyzce ludu 250 tys.), tylko leza w niewlasciwych miastach i w krolestwach w pokoju, a Z.1 (wojsko w pokoju) jeszcze o ok. 30-40% przyspiesza ich pietrzenie.

## 1. Lokata i kiesy ludu - kwartalami, rok 1 i 2 [P]

Zrodlo: linia "Kiesa ludu" (wplywy / wydatki z kontroli zamknietej kies, r = 0 w 727 z 727 dob - kiesa1 P1-5a), podzial lokaty z linii "Danina z oszczednosci (P1)" (wiersze krolestw, "W miast" po daninie). Skrypty `lud\wyciag.py`, `lud\kwartaly.py`.

| kwartal | dob zimy | krolestw w pokoju (koniec) | wplywy kies | wydatki kies | w tym towar (miasta + zamki) | w tym danina P1 | nadwyzka przed danina | saldo kies | lokata W koniec (mln) | z tego: pokoj / wojna s = 0 / placace (mln) | kiesy ludu miast (mln) | kiesy podzamczy (mln) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|
| Q1 | 0 | 4 | 600.2 | 362.8 | 279.9 | 66.2 | 303.6 | +237.4 | 20.30 | 1.87 / 4.28 / 13.98 | 24.43 | 3.78 |
| Q2 | 0 | 3 | 655.3 | 578.4 | 324.0 | 222.4 | 299.3 | +76.9 | 26.26 | 0.43 / 7.80 / 18.09 | 30.56 | 4.64 |
| Q3 | 0 | 3 | 704.1 | 745.3 | 343.8 | 357.4 | 316.2 | -41.2 | 22.95 | 0.62 / 0.00 / 22.43 | 27.35 | 4.10 |
| **Q4** | 0 | 3 | **760.6** | 848.1 | **343.2** | **452.9** | 365.4 | -87.5 | **15.48** | 0.83 / 0.78 / 13.86 | 20.37 | 3.12 |
| Q5 | 0 | 9 | 783.9 | 795.1 | 350.5 | 393.5 | 382.3 | -11.2 | 14.95 | 3.59 / 1.27 / 10.02 | 19.73 | 2.73 |
| Q6 | 87 | 20 | 709.6 | 620.2 | 348.0 | 235.3 | 324.6 | +89.3 | 21.87 | 12.30 / 3.77 / 5.61 | 26.37 | 4.22 |
| Q7 | 91 | 17 | 623.1 | 596.3 | 337.6 | 227.0 | 253.8 | +26.8 | 24.01 | 13.53 / 0.00 / 10.54 | 28.38 | 4.66 |
| **Q8** | 91 | 16 | **613.9** | 600.5 | **329.1** | **235.1** | 248.6 | +13.5 | **25.08** | **16.63** / 0.00 / 8.39 | 29.00 | 5.27 |

- Rok 1: lokata rosnie najszybciej w Q1 (+237 tys. zl/d), bo danina startuje od zera - stawka s krolestwa rosnie o 0.5 pkt co 7 dob (linia 184: "krok 0.50 pkt co 7 dob") i bez poboru (pokoj albo s = 0) jest w Q1 srednio 60 miast, w Q2 20 [P, rozdz. 4.1]. Szczyt kies 30.6 mln ok. d182, potem danina 357-453 tys. zl/d zbija lokate do 15.5 mln w d364 [P].
- Rok 2: od fali pokojow (d449-513) danina spada do 235 tys. zl/d, a lokata w krolestwach w pokoju rosnie z 0.83 do 16.63 mln; w placacych spada z 13.9 do 8.4 mln [P].
- Narzedzie progow (kiesa1, okno 728): P1-1 lokata 25.08 mln (prog 6) NIE; P1-3 kiesy 29.00 mln DECYZJA; P1-4 danina d301-364 446.6 tys. zl/d (projekt zakladal 45-90) [P].

## 2. Skad naplyw

### 2.1 Sklad zarobku ludu, kwartalami (tys. zl/d) [P]

| zrodlo (linia "Kiesa ludu") | Q1 | Q2 | Q3 | Q4 | Q5 | Q6 | Q7 | Q8 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| robocizna handlu (z kas kupcow) | 215.0 | 223.1 | 230.3 | 241.8 | 249.3 | 227.7 | 202.5 | 197.6 |
| - w tym od skupu plonu wsi (11c) | 33.5 | 25.8 | 25.5 | 25.1 | 24.4 | 22.0 | 21.6 | 21.0 |
| wojsko razem (25% wydatkow) | 102.7 | 152.5 | 165.5 | 178.5 | 192.4 | 143.7 | 113.3 | 113.5 |
| - zycie zolnierzy / karawany / bandy / zalogi | 52.6 / 34.0 / 2.6 / 13.4 | 89.4 / 36.2 / 4.5 / 22.4 | 99.5 / 34.1 / 7.4 / 24.5 | 112.5 / 30.3 / 9.3 / 26.4 | 127.7 / 26.4 / 11.6 / 26.7 | 86.7 / 29.0 / 10.4 / 17.6 | 59.3 / 30.3 / 8.6 / 15.1 | 60.4 / 29.2 / 7.7 / 16.2 |
| doplata kupcow do 35% wyrobow | 108.3 | 104.5 | 102.8 | 104.4 | 103.3 | 109.7 | 108.1 | 103.4 |
| place warsztatow | 27.4 | 38.4 | 49.7 | 53.4 | 55.5 | 50.9 | 42.7 | 41.4 |
| obrot rodzin kupieckich (P) | 33.9 | 35.5 | 37.4 | 39.9 | 41.5 | 39.4 | 34.9 | 33.5 |
| dwor | 27.1 | 21.3 | 25.7 | 39.4 | 41.5 | 42.1 | 35.2 | 38.3 |
| podzamcza (dochod + robocizna z kas zamkow) | 57.5 | 60.4 | 69.8 | 74.3 | 71.8 | 60.1 | 57.2 | 57.8 |
| w tym praca niewolnych (przesuniecie W -> R/P, nie dodawac) / 5% zaworu / budowy | 14.4 / 11.9 / 10.4 | 16.7 / 7.4 / 7.7 | 17.5 / 10.2 / 8.1 | 19.2 / 14.2 / 10.3 | 19.1 / 16.2 / 8.3 | 15.7 / 19.3 / 12.9 | 12.8 / 13.7 / 11.9 | 12.7 / 12.4 / 12.3 |
| **wplywy razem (kontrola)** | 600.2 | 655.3 | 704.1 | 760.6 | 783.9 | 709.6 | 623.1 | 613.9 |

Podstawa robocizny - sprzedaz kupcow (tys. zl/d) [P, linia "Podstawa robocizny (E5/B1)"]:

| kupujacy | Q1 | Q4 | Q5 | Q8 |
|---|---:|---:|---:|---:|
| razem | 1 455.6 | 1 538.3 | 1 577.8 | 1 346.6 |
| lud (towar ludu) | 266.3 (18%) | 324.4 (21%) | 330.7 (21%) | 310.4 (23%) |
| zolnierze i zalogi | 308.9 | 536.5 | 578.2 | 341.4 |
| karawany i gracz | 609.7 | 347.8 | 326.7 | 373.3 |
| dwor / sprzet AI, budowy, zamki | 108.7 / 153.0 | 157.9 / 156.3 | 166.1 / 159.5 | 153.4 / 142.2 |

Wniosek [P kod + log]: lud dostaje robocizne od KAZDEJ sprzedazy kupcow (`TownFolk.Live.cs:780-830`, krok 2: podstawa = zycie, dwor, Sale, towar ludu, wsie, karawany), a 77-82% tej sprzedazy to kupujacy spoza ludu. Do tego 25% wydatkow zolnierzy i dworu. Zarobek nie zalezy od tego, ile lud moze kupic - stad nadwyzka 250-380 tys. zl/d przed danina (rozdz. 1).

### 2.2 Porownanie biegow - co podnioslo nadwyzke (tys. zl/d; lokata w mln) [P]

| bieg | wplywy d91-120 | towar d2-30 / d91-120 | danina d91-120 | lokata d120 | wplywy Q4 | towar Q4 | danina Q4 | lokata d364 |
|---|---:|---|---:|---:|---:|---:|---:|---:|
| W43 (bez gospodarki od ludzi, bez K1-3a) | 650.1 | 337.2 / 455.1 | 100.4 | 9.21 | 730.9 | 501.7 | 237.4 | 12.33 |
| GL (gospodarka od ludzi, bez K1-3a, P2, P3-P5) | 637.0 | 371.8 / 422.6 | 88.4 | 12.07 | 738.3 | 485.8 | 266.0 | 14.93 |
| **W49 (bieg dwuletni: + K1-3a, P2, P3-P5, W7)** | 625.5 | **231.8 / 311.7** | 192.8 | **22.76** | 760.6 | **343.2** | 452.9 | 15.48 |
| W52 (+ Z.1, plotno) | 623.3 | 201.3 / 299.9 | 156.2 | **25.35** | - | - | - | - |

- Zarobek we wszystkich biegach ten sam (+-4%), roznica jest w wydatku: od W49 lud placi za towar o 110-140 tys. zl/d mniej [P]. W W43 i GL nie ma linii "Konie robocze (K1-3a)" (0 wystapien) [P]. Kod K1-3a: koszyk BK ma budzet koni = 0 (`TownFolk.Horses.cs:11-17`; "pomiar F3 ... razem 343 konie/d za 194 tys. zl/d"), lud kupuje 13.5 konia roboczego/d za 1-2 tys. zl/d [P]. Wniosek: glowne nowe zrodlo lokaty od W49 to zdjecie koni z koszyka ludu (projekt obiegu kiesy przewidzial to - rozdz. 1 poz. 16-17, "nadwyzka +125 tys. zl/d") [P/S].
- "Lokata W52 d120 25.4 mln, wczesniej ok. 10" (STAN-PRAC 10:16): "ok. 10" to biegi bez K1-3a (W43 9.2, GL 12.1); bieg dwuletni W49 mial w d120 juz 22.8 mln [P]. W52 ma o 2.6 mln wiecej niz W49: towar -12 tys. zl/d, zbytki kupione 87.7 wobec 106.2 tys. zl/d w d91-120 (regula plotna FolkClothAboveReserve - lud nie kupuje plotna i skory ponizej zapasu polki) [P]. Szczegoly tej roznicy - zakres towary-2 (`docs/towary-2/ROZPOZNANIE-TOWARY-2.md`, w chwili pisania pliku jeszcze nie ma).

## 3. Co wychodzi z oszczednosci i jaki ma sufit [P]

| droga wyjscia | Q4 | Q8 | kiedy dziala | sufit (kod / log) |
|---|---:|---:|---|---|
| towar - zwykly koszyk (miasta + zamki) | 343.2 | 329.1 | zawsze | polka targu; BK bierze min(budzet / cena, stan polki) (ROZPOZNANIE-OBIEG-KIESY 1.3); budzet = popyt gry x m_t (rozdz. 8.2) |
| - w tym zbytki z oferty P3 (lista dostatku 1/60 nadwyzki dziennie) | 93.8 z oferty 319.5 (29%) | 76.5 z 412.7 (19%) | zawsze | towar na polce; d301-364 plotno to 2/3 oferty bez towaru (P1-13b: linen 95.7 / kupione 18.8 / bez wiersza na polce 62.2) |
| roboty ludu P2 (z kies klas; material + place) | 5.2 (material 1.6) | 4.0 (material 1.2) | zawsze, osada nieoblezona, budowa w kolejce | srednio 190 z 227 osad dziennie "bez materialu", 101 "na pulapie rak" (3% wyrobnikow x 3 zl), tempo koszt poziomu / 60 (linia "Roboty ludu", 728 dob); place wracaja do ludu tej osady |
| **danina z oszczednosci P1** | **452.9** | **235.1** | **tylko wojna i s > 0** | `KingdomTreasury.WarAid.cs:118`: `if (w == null \|\| sv <= 0 \|\| !AtWar(k)) return 0`; tylko ponad zapas 10 / 21 / 30 dni (`:180`) |
| podatek wojenny 184 (od zarobku) | 16.7 | 9.4 | tylko wojna | do 3% zarobku |
| stawka wojenna pana W7 (z ludu) | 15.6 | 9.5 | tylko wojna, rod z niepokryta potrzeba | 3% nadwyzki po P1 i ofercie |
| podatki panu 1/2/3% + farma gmin | 15.2 | 12.9 | zawsze | od zarobku |
| konie robocze K1-3a | 1.1 | 1.8 | zawsze | 1 kon / 25 ludzi / 15 lat; tylko ponad jutrzejszy koszyk |
| lup miast L2 (kiesy ludu) | 2.2 | 0.5 | zdobycie szturmem | - |

Wniosek: proporcjonalnie do STANU oszczednosci (a nie do zarobku) bierze tylko danina P1 i oferta zbytkow; oferta zbytkow kupuje 19-29% (polka), wiec w pokoju jedynym skutecznym odplywem z lokaty jest ok. 0.3%/d (rozdz. 4.1).

## 4. Czemu stoi - pokoj a wojna

### 4.1 Miasta placace (wojna, s > 0) i bez poboru (pokoj albo s = 0), kwartalami [P, `kiesa-obieg.csv`, powod 0 / 3]

| kwartal | grupa | miast sr. | lokata W (mln, sr.) | zarobek tys./d | z wojska | potrzeba tys./d | danina tys./d | oferta zbytkow | kupione | odplyw (danina + kupione) / lokata, %/d |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Q1 | placace | 36.8 | 5.90 | 191.3 | 45.8 | 61.5 | 50.8 | 69.4 | 31.0 | 1.39 |
| Q1 | pokoj / s = 0 | 59.9 | 6.95 | 352.5 | 51.9 | 151.7 | 0.3 | 78.0 | 40.3 | 0.58 |
| Q4 | placace | 92.9 | 17.41 | 683.5 | 175.8 | 273.3 | 388.8 | 215.6 | 90.6 | 2.75 |
| Q4 | pokoj / s = 0 | 3.4 | 0.79 | 7.5 | 1.5 | 3.0 | 0.0 | 10.3 | 2.8 | 0.35 |
| Q5 | placace | 86.6 | 13.09 | 658.5 | 178.0 | 247.8 | 335.0 | 163.9 | 75.4 | 3.13 |
| Q6 | placace | 50.8 | 8.02 | 361.1 | 96.3 | 103.7 | 206.0 | 96.6 | 31.8 | 2.96 |
| Q6 | pokoj / s = 0 | 45.6 | 10.26 | 290.3 | 45.2 | 200.9 | 4.3 | 124.7 | 47.9 | 0.51 |
| Q7 | pokoj / s = 0 | 45.8 | 14.60 | 238.0 | 34.4 | 158.7 | 1.4 | 174.1 | 52.2 | 0.37 |
| Q8 | placace | 55.6 | 8.26 | 364.3 | 85.2 | 171.7 | 204.3 | 100.9 | 30.4 | 2.84 |
| **Q8** | **pokoj / s = 0** | 41.0 | **16.13** | 196.9 | 27.6 | 133.1 | 1.6 | 193.3 | 45.9 | **0.29** |

Miasta w pokoju w Q8 zarabiaja 197 tys. zl/d przy potrzebie 133 tys. - nadwyzka ok. 64 tys. zl/d bez odplywu proporcjonalnego do stanu [P/S].

### 4.2 Krolestwa - lokata W (mln) i jej przyrost w dobach pokoju [P, linia Danina]

| krolestwo | d364 | d455 | d728 | dob pokoju w roku 2 | przyrost w dobach pokoju (tys. zl/d) | danina srednio rok 2 (tys. zl/d) | d728 |
|---|---:|---:|---:|---:|---:|---:|---|
| Dothraki Horde | 0.87 | 1.04 | **4.14** | 222 | 12.9 | 12.1 | pokoj |
| The Vale | 0.95 | 1.08 | **3.19** | 222 | 10.6 | 13.2 | pokoj |
| Dorne | 0.69 | 0.66 | **2.11** | 184 | 10.3 | 13.3 | pokoj |
| House Targaryen | 1.03 | 0.86 | **1.61** | 134 | 10.3 | 15.0 | pokoj |
| Sarnor | 0.32 | 0.28 | 1.34 | 253 | 4.2 | 2.8 | pokoj |
| Yi Ti Exiles / Duchy of Valyria | 0.50 / 0.00 | 0.61 / 0.46 | 1.05 / 1.02 | 365 / 270 | 1.4 / 2.0 | 0 / 0 | pokoj |
| The North (wojna caly czas) | 1.84 | 1.51 | 1.71 | 0 | - | 52.3 | wojna |
| Riverlands (wojna) | 1.31 | 1.15 | 1.55 | 0 | - | 38.8 | wojna |
| The Reach (pokoj d455-537) | 1.62 | 1.01 | 0.81 | 82 | 14.1 | 26.9 | wojna |
| razem d728 | | | 26.69 (linia Danina) / 25.08 (linia Kiesa) | | | | w pokoju **16.63** |

### 4.3 Z.1 (WGRANIE 52) przyspiesza pietrzenie w pokoju [P]

| okno | wojsko w zarobku ludu (tys. zl/d) | wplywy kies | danina | przyrost lokaty krolestw w pokoju (te same krolestwa, tys. zl/d) | lokata krolestw w pokoju (mln) |
|---|---:|---:|---:|---:|---|
| dwuletni d701-728 | 106.3 | 592.8 | 210.6 | 33.8 (d638-728: 36.6) | 17.28 -> 16.63 |
| Z.1 d722-751 | 115.8 | 613.2 | 223.3 | 48.4 (d722-781) | 16.55 -> 19.55 (d781) |
| Z.1 d752-781 | 119.4 | 627.2 | 225.4 | | |

Wojsko trzymane w pokoju wydaje w miastach - lud zarabia o 10-13 tys. zl/d wiecej, a w pokoju nic tego nie zabiera [P/S].

### 4.4 Rownowaga reguly daniny [S z P]

Lokata w rownowadze = nadwyzka dzienna / stawka skuteczna. Koniec roku 1: nadwyzka przed danina 365 tys. zl/d, danina 453 tys. zl/d z nadwyzki wszystkich klas i podzamczy ok. 20 mln = 2.2%/d -> rownowaga ok. 15-16 mln (pomiar d364: 15.48). Projekt (PROJEKT-OBIEG-KIESY-LUDU rozdz. 3.2) liczyl prog 6 mln na nadwyzke 49 tys. zl/d (z koniami 146 tys.) [P]; przy 250-380 tys. zl/d prog 6 mln wymagalby stawki ok. 4-6%/d [S].

## 5. Gdzie lezy - zarobek wobec potrzeby (Q8) [P]

| zarobek / potrzeba (d638-728) | miast | lokata d728 (mln) | udzial lokaty | sr. m_t (skala rynku) | ludzie W (tys.) | miast stac < 0.95 | przyklady |
|---|---:|---:|---:|---:|---:|---:|---|
| ponizej 1.0 | 10 | 0.14 | 1% | 2.61 | 356 | 7 | Oldtown 0.35, Braavos 0.38, Aquos Dhaen 0.41, Rhyos 0.48, Volon Therys 0.54 |
| 1.0-1.5 | 14 | 2.95 | 12% | 1.46 | 272 | 0 | Valysar 1.01, Volantis 1.04, Heart's Home 1.12, Arbor 1.20 |
| 1.5-3.0 | 18 | 3.35 | 13% | 1.13 | 291 | 0 | Castle Black 1.96, Riverrun 2.54, Wickenden 2.43 |
| 3.0-6.0 | 21 | 8.90 | 35% | 0.84 | 244 | 0 | Vaes Dothrak 4.95, Elyria 5.76, Yronwood 3.05, Sunspear 4.97 |
| 6.0 i wiecej | 34 | 9.75 | 39% | 0.39 | 183 | 0 | Vaes Diaf 6.99, Saath 11.07, Saltpans 10.38, Ifequeveron 53.90 |

Zarobek na glowe W w d638-728: Oldtown 0.10, Aquos Dhaen 0.10, Braavos 0.12, Rhyos / Volon Therys / Selhorys 0.18-0.19, Highgarden 0.56 - wobec Vaes Diaf 0.74, Ifequeveron 0.91, Vaes Dothrak 1.02, Saath 2.1 zl dziennie [P]. Top 10 kies ponad zapas d728 (44% lokaty): Vaes Diaf 2.25 mln, Vaes Dothrak 1.90, Ifequeveron 1.05, Elyria 1.02, Saath 0.95, Saltpans 0.92, Yronwood 0.79, The Eyrie 0.74, Sunspear 0.70, Meereen 0.68 - wszystkie w pokoju [P].

## 6. A4: chleb swiata kwartalami [P]

| kwartal | dob zimy | stac pelny koszyk, swiat | stac jedzenie W | miast jedzenie < 95% | miast pelny < 95% | osad "brak pieniedzy" 7 dob | zjedzone bez zaplaty tys. zl/d | miast z zarobkiem < potrzeby / luka tys. zl/d | najwiekszy udzial w niedoborze swiata |
|---|---:|---:|---:|---:|---:|---:|---:|---|---|
| Q1 | 0 | 100.0% | 100.0% | 0.0 | 0.0 | 0.0 | 0.0 | 1 / 4.0 (Oldtown) | - |
| Q2 | 0 | 99.4% | 99.5% | 0.3 | 0.3 | 0.1 | 0.0 | 2 / 3.8 | Oldtown 100% |
| Q3 | 0 | 97.6% | 97.8% | 0.8 | 0.9 | 0.4 | 0.1 | 1 / 8.1 | Oldtown 100% |
| **Q4** | 0 | **97.1%** | 97.6% | 1.1 | 1.4 | 0.2 | 0.5 | 4 / 13.6 | Oldtown 83%, Rhyos 17% |
| Q5 | 0 | 95.7% | 96.1% | 1.2 | 1.5 | 1.5 | 1.1 | 4 / 14.0 | Oldtown 71%, Rhyos 29% |
| Q6 | 87 | 95.3% | 95.9% | 2.0 | 2.6 | 1.9 | 0.9 | 8 / 31.6 | Oldtown 71%, Rhyos 20% |
| Q7 | 91 | 92.3% | 93.4% | 3.9 | 5.1 | 2.7 | 1.4 | 8 / 42.3 | Oldtown 41%, Braavos 30%, Rhyos 14% |
| **Q8** | 91 | **90.9%** | 92.2% | 4.2 | 5.5 | 3.5 | 1.8 | **10 / 46.0** | Oldtown 38%, Braavos 20%, Highgarden 13%, Rhyos 9%, Selhorys 7%, Volon Therys 6% |

Prog P1-6 (okna 64 dob): d301-364 97.1%, d665-728 89.7% przy celu 98% [P]. Luka biedy w Q8 (46 tys. zl/d) to ok. 1/5 nadwyzki ludu przed danina (249 tys.) i ok. 1.3 x przyrostu lokaty w pokoju (34-37 tys.) [S z P].

## 7. A4: siedem miast [P]

| miasto (krolestwo, m_t) | stan wojny rok 2 | stac pelny Q4 / Q5 / Q8 | zarobek Q5 -> Q8 (zl/d) | potrzeba Q5 -> Q8 | wydatek gry Q5 -> Q8 | kiesa W Q4 -> Q8 (tys.) | kasa kupcow / zapas Q8 | indeks cen Q5 -> Q8 | dobrobyt Q1 -> Q8 | glowny powod |
|---|---|---|---|---|---|---|---|---|---|---|
| Oldtown (Reach, 5.74) | pokoj d455-537, potem wojna (danina od d582) | 0.54 / 0.70 / 0.58 | 8 320 -> 7 971 (Q1: 12 047) | 14 951 -> 19 340 | 5 899 -> 8 000 | 7.6 -> 9.8 | 0.78 | 5.11 -> 5.65 | 5 637 -> 8 949 | za duzy rynek: zarobek/potrzeba 0.75 juz w Q1, ponizej 0.95 od d183; 38-100% niedoboru swiata |
| Braavos (Braavos, 2.72) | pokoj od d495 | 1.00 / 1.00 / 0.56 | 17 207 -> 4 627 (-73%) | 13 813 -> 10 153 | 15 101 -> 3 913 | 156 -> 4.9 | **0.02** (kasa 289 -> 6 tys.) | **3.73 -> 5.24** | 6 008 -> 8 776 | pusta kasa kupcow; warsztaty i doplata 8 023 -> 1 217, robocizna 5 057 -> 1 997 zl/d; drozszy koszyk |
| Highgarden (Reach, 3.28) | pokoj d455-537, potem wojna (danina od d582) | 1.00 / 1.00 / 0.93 (d728: 0.33) | 26 332 -> 26 612 | 18 216 -> 28 853 | 21 009 -> 24 357 | 363 -> 114 (d728: 9.8) | 1.18 | 4.86 -> 4.97 | 6 372 -> 12 605 | danina zdjela kiese W ponad 10 dni (419 tys. d588 -> 247 tys. d637), potem wydatek > zarobek zjadl zapas |
| Rhyos (Braavos, 1.93) | pokoj od d495 | 0.80 / 0.75 / 0.77 | 5 873 -> 5 008 | 8 092 -> 8 859 | 6 001 -> 4 740 | 7.1 -> 6.9 | **0.04** | 4.35 -> 4.91 | 5 075 -> 8 268 | kasa kupcow ponizej 0.2 zapasu od Q4; potrzeba x2.2 od Q1 przy zarobku -34% |
| Selhorys (Volantis, 2.99) | pokoj d460-565 i d684-713, poza tym wojna | 1.00 / 1.00 / 0.83 | 8 970 -> 7 413 (-17%) | 6 688 -> 9 012 | 8 189 -> 7 259 | 67 -> 8.6 | 1.07 | 4.71 -> 4.75 | 5 037 -> 6 838 | mniej wojska (z wojska 1 710 -> 474), potrzeba +35% |
| Volon Therys (Volantis, 1.71) | jw. | 1.00 / 1.00 / 0.77 | 4 555 -> 3 925 (-14%; Q1 6 166) | 4 271 -> 5 754 | 4 955 -> 4 599 | 62 -> 4.0 | 0.81 | 3.87 -> 3.65 | 4 662 -> 6 712 | rozjazd od Q1: zarobek -37%, potrzeba +77% |
| Aquos Dhaen (Lys, 1.05) | pokoj od d451 | 1.00 / 1.00 / 0.56 | 2 335 -> 1 306 (-44%) | 1 786 -> 2 536 | 2 043 -> 1 151 | 19.5 -> 1.1 | 0.96 | 5.07 -> 4.91 | 5 021 -> 6 918 | pokoj: z wojska 536 -> 44, robocizna 913 -> 531 zl/d |

Tygodniowo Highgarden (kiesa-ludu.csv / kiesa-obieg.csv): kiesa W d588 418 914, d637 247 004 (nadwyzka ponad 10 dni 0 od d637), d700 99 692, d728 9 771; stawka daniny 0.42% (d588) -> 2.5% (d623); danina okna 1.1-5.1 tys. zl/d w d588-623; zarobek 21-31 tys. zl/d caly rok 2; wydatki dnia 14-60 tys. W tym czasie kasa kupcow Highgarden byla ponad zapasem i oddawala zaworem panu 4.5 i koronie 3.5 tys. zl/d (Q8) [P].

## 8. A4: lancuch - co pcha biede

### 8.1 Zarobki biedoty spadaja w roku 2 - glownie przez pokoj [P]

| tys. zl/d | Q4 | Q5 | Q6 | Q7 | Q8 | Q5 -> Q8 |
|---|---:|---:|---:|---:|---:|---:|
| wplywy ludu razem | 760.6 | 783.9 | 709.6 | 623.1 | 613.9 | -170.0 |
| wojsko (25% wydatkow zolnierzy, karawan, band, zalog) | 178.5 | 192.4 | 143.7 | 113.3 | 113.5 | -78.9 |
| robocizna handlu | 241.8 | 249.3 | 227.7 | 202.5 | 197.6 | -51.7 |
| - w tym od skupu plonu wsi (11c) | 25.1 | 24.4 | 22.0 | 21.6 | 21.0 | -3.4 |
| place warsztatow | 53.4 | 55.5 | 50.9 | 42.7 | 41.4 | -14.1 |
| podzamcza | 74.3 | 71.8 | 60.1 | 57.2 | 57.8 | -14.0 |
| sprzedaz kupcow wojsku (podstawa robocizny) | 536.5 | 578.2 | 432.0 | 341.0 | 341.4 | -236.8 |

Mediana miast: zarobek Q4 -> Q8 x0.81 [P]. Wojsko w zarobku i sprzedaz kupcow wojsku spadaja razem z fala pokojow (krolestw w pokoju 3 -> 16-20) [P]; ze to glownie pokoj, a nie zima - [S] (zima zaczyna sie d460, pokoje d449-513; skup 11c, ktory zalezy od plonu, spada tylko o 3.4 tys.).

### 8.2 Wydatek ludu idzie za dobrobytem, zarobek nie [P kod + log]

- Kod gry `DefaultSettlementEconomyModel.cs:61-73`: popyt kategorii = BaseDemand x dobrobyt + LuxuryDemand x max(0, dobrobyt - 3000); budzet = popyt x indeks^0.3 (`:81-84`). Nasz GL-2: popyt u zrodla x m_t (`PeopleMarket.cs:24`). Potrzeba ludu = min(wydatek gry, koszyk glow) (`TownFolk.Live.cs` Anchor) - wiec rosnie z dobrobytem i skala rynku.
- Zarobek ludu (rozdz. 2.1) to robocizna od sprzedazy kupcow, 25% wydatkow wojska i dworu, place warsztatow - zaden skladnik nie jest liczony od dobrobytu ani od glow [P kod `TownFolk.Live.cs:780-830`].
- Dobrobyt miast (suma, `kasy-miast.csv`): Q1 482.8 tys., Q4 594.7, Q5 627.4, Q8 684.0 (+42%) [P]. 97 miast, mediana Q1 -> Q8: dobrobyt x1.45, potrzeba x1.40, zarobek x0.95; korelacja log(potrzeba) ~ log(dobrobyt) 0.60 (nachylenie 1.22), log(zarobek) ~ log(dobrobyt) 0.30; w roku 2 (Q4 -> Q8): dobrobyt x1.17, potrzeba x1.01, zarobek x0.81, korelacja zarobku z dobrobytem -0.02 [P].
- Wielkie rynki (m_t > 1.5, 20 miast) [P]:

| | Q1 | Q2 | Q3 | Q4 | Q5 | Q6 | Q7 | Q8 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| zarobek tys. zl/d | 197.2 | 200.7 | 212.6 | 216.5 | 227.4 | 210.4 | 187.4 | 171.7 |
| potrzeba tys. zl/d | 127.5 | 132.4 | 145.6 | 164.2 | 167.1 | 190.8 | 185.0 | 183.0 |
| zarobek / potrzeba | 1.55 | 1.52 | 1.46 | 1.32 | 1.36 | 1.10 | 1.01 | 0.94 |
| stac pelny (wazone potrzeba) | 1.000 | 0.991 | 0.964 | 0.937 | 0.945 | 0.923 | 0.881 | 0.856 |
| reszta miast: zarobek / potrzeba | 4.05 | 4.36 | 4.27 | 4.25 | 4.10 | 3.92 | 3.27 | 3.20 |

Przyczyna wzrostu dobrobytu (+42% w 2 lata, raport roczny R16) - nie wiem (rozdz. 10).

### 8.3 Ceny w zimie [P]

| | Q4 | Q5 | Q6 | Q7 | Q8 |
|---|---:|---:|---:|---:|---:|
| indeks cen koszyka miast (wazony glowami W; koszyk / glowy x stawka) | 4.69 | 4.72 | 4.85 | 5.00 | 5.06 |
| wydatek gry na jedzenie w miastach (tys. zl/d) | 150.1 | 151.3 | 142.1 | 145.2 | 141.4 |
| wydatek gry razem (tys. zl/d) | 331.4 | 337.6 | 334.9 | 324.0 | 315.6 |

Koszyk drozeje o 7% w Q5 -> Q8, wydatek na jedzenie nie rosnie - ceny nie sa glowna przyczyna biedy w swiecie [P/S]. W 7 miastach indeks Q5 -> Q8: Braavos +40%, Rhyos +13%, Oldtown +11%, Highgarden +2%, Selhorys +1%, Aquos Dhaen -3%, Volon Therys -6% [P]. Indeks to cena calego koszyka (jedzenie to ok. 45% budzetu gry); osobnego indeksu jedzenia w logu nie ma (rozdz. 10).

### 8.4 Dowoz [P]

- Zima tnie plon wsi o 40.8% od d460; zboze w swiecie 9 436 -> 6 532 szt./d (d337-364 -> d701-728), na polkach miast 44.7 tys. (d91) -> 8.9 tys. szt. (d728), mieszczanie kupuja 5 224 -> 2 336 szt./d zboza (gospodarka\RAPORT.md rozdz. 2, 8).
- Kasy kupcow placa taborom wsi za plon: skup ok. 251 tys. zl/d w Q4, 210 w Q8 (11c = 0.10 x skup: 25.1 -> 21.0 tys.) - lud traci 4 tys. zl/d robocizny [P].
- Jedzenie na polkach (GL2, minimum kwartalu, doby spozycia): Reach 9.9 (Q4) -> 4.9 (Q8), Braavos 5.6 -> 3.5, Polnoc 6.0 -> 1.4 [P]. Lud dalej je (zjedzone bez zaplaty 1.8 tys. zl/d = 0.5%), z glodu nikt nie umarl [P].
- Dowozu do konkretnego miasta (Braavos, Oldtown) log nie mierzy - nie wiem.

### 8.5 Kiesy - bufor zabiera danina, nie bieda [P kod + log]

Danina i W7 biora tylko ponad zapas 10 dni potrzeby (`KingdomTreasury.WarAid.cs:180`; W7 - "lud po daninie P1 i ofercie na jutro"), wiec same nie spychaja ludu ponizej chleba. Ale zabieraja oszczednosci, ktore pozwolilyby przetrwac rozjazd zarobku i wydatku: Highgarden 419 -> 247 tys. w 49 dobach poboru, potem deficyt ok. 2.6 tys. zl/d zjadl 10-dniowy zapas w 90 dob (rozdz. 7) [P/S].

### 8.6 Pusta kasa kupcow tnie place [P kod; skutek S]

Robocizna, obrot P i doplata do 35% ida z kasy miasta i sa przycinane do jej stanu: `if (all > gold && all > 0) { k = gold / all; ... }` (`TownFolk.Live.cs:824`). Kasa / zapas kupcow: Braavos 0.99 (Q5) -> 0.07 (Q7) -> 0.02 (Q8), Rhyos 0.16 (Q4) -> 0.04 (Q8); wielkie rynki z kasa < 0.25 zapasu (srednio miast): 0.1-1.0 w roku 1, Q5 1.5, Q6 0.2, Q7-Q8 2.1-2.3 [P]. Linia "Kiesa ludu" - miast przycietych do stanu kasy srednio na dobe: Q1-Q5 0.10-0.13, Q6 0.01, Q7 0.31, Q8 0.62 [P]. Ze spadek zarobku Braavos (-73%) to przyciecie do kasy, a nie spadek produkcji wyrobow - [S], rozdz. 10.

## 9. Przyczyny z dowodem (lista)

| # | przyczyna | dowod | liczba |
|---|---|---|---|
| A5-1 | Zarobek ludu nie zalezy od tego, ile lud moze kupic - robocizna od calej sprzedazy kupcow (77-82% spoza ludu) i 25% wydatkow wojska | [P] linie "Kiesa ludu", "Podstawa robocizny"; kod `TownFolk.Live.cs:780-830` | Q4: 761 wobec 343 tys. zl/d; nadwyzka przed danina 250-380 tys. zl/d |
| A5-2 | Konie robocze K1-3a zdjely konie z koszyka ludu - wydatek ludu nizszy o 110-140 tys. zl/d | [P] W49 wobec GL i W43 (brak linii K1-3a); kod `TownFolk.Horses.cs:11-17` | lokata d120: 22.8 mln (W49) wobec 9.2 / 12.1 mln |
| A5-3 | Danina P1 dziala tylko w wojnie i przy s > 0 | [P] kod `KingdomTreasury.WarAid.cs:118`; linia Danina | lokata krolestw w pokoju 0.83 -> 16.63 mln (d364 -> d728); odplyw 0.29-0.51%/d wobec 2.4-3.1%/d |
| A5-4 | Inne drogi wyjscia maja sufit: zbytki (polka), budowy ludu (material, rece), W7 / 184 tylko w wojnie | [P] linie Zbytki, Roboty ludu, W7, 184 | zbytki 19% oferty (Q8), budowy 4-7 tys. zl/d, 190 osad dziennie bez materialu |
| A5-5 | Rownowaga reguly przy dzisiejszej nadwyzce to ok. 15 mln nawet w pelnej wojnie; prog 6 mln liczony na nadwyzke 49-146 tys. | [P] d364 15.48 mln przy daninie 453 tys. zl/d; [S] rachunek 4.4 | 2.2%/d od ok. 20 mln nadwyzki klas |
| A5-6 | Z.1 przyspiesza pietrzenie w pokoju (wojsko wydaje w miastach) | [P] log Z.1 | +48.4 wobec 33.8-36.6 tys. zl/d; wojsko w zarobku +10-13 tys. |
| A4/A5-7 | Wydatek ludu idzie za dobrobytem x m_t, zarobek nie - pieniadz lezy w malych rynkach, bieda w wielkich | [P] kod gry `DefaultSettlementEconomyModel.cs:61-73`, `PeopleMarket.cs:24`; tabele 5 i 8.2 | dobrobyt x1.45, potrzeba x1.40, zarobek x0.95; 55 miast >= 3x trzyma 74% lokaty; 10 miast < 1x (sr. m_t 2.61) |
| A4-8 | W roku 2 zarobek spada o 170 tys. zl/d, glownie wojsko i robocizna od sprzedazy wojsku (pokoj) | [P] linia "Kiesa ludu", "Podstawa robocizny"; [S] ze to pokoj, nie zima | wojsko -79, robocizna -52, skup 11c -3.4 tys. zl/d |
| A4-9 | Oldtown: rynek 5.7 x sredni, zarobek od poczatku ponizej potrzeby | [P] kiesa-obieg.csv | zarobek/potrzeba 0.75 (Q1) -> 0.35 (Q8); 38-100% niedoboru swiata |
| A4-10 | Braavos i Rhyos: pusta kasa kupcow, zarobek ludu leci | [P] kasy-miast.csv, kiesa-obieg.csv; [S] mechanizm przyciecia | Braavos kasa/zapas 0.02, zarobek 17.2 -> 4.6 tys. zl/d; Rhyos 0.04 |
| A4-11 | Highgarden: danina zdjela oszczednosci ponad 10 dni, potem wydatek przerosl zarobek | [P] tygodniowo kiesa-ludu.csv / kiesa-obieg.csv | 419 -> 247 tys. (d588-637), 9.8 tys. w d728; wydatek gry +16%, dobrobyt +20% (Q5 -> Q8) |
| A4-12 | Aquos Dhaen, Volon Therys, Selhorys: mniej wojska w pokoju, potrzeba rosnie | [P] kiesa-obieg.csv | zarobek -44% / -14% / -17% (Q5 -> Q8) |
| A4-13 | Drozsze jedzenie w zimie to nie glowna przyczyna (poza Braavos) | [P] indeks koszyka i wydatek gry na jedzenie | swiat +7%, jedzenie 151 -> 141 tys. zl/d; Braavos +40% |

## 10. Czego nie wiem (+ linia diagnozy)

1. Czemu dobrobyt miast rosnie o 42% w 2 lata (R16 raportu rocznego, "przyczyna nieznana") - a on podnosi popyt gry i potrzebe ludu. Linia: co 7 dob w `kasy-miast.csv` skladniki zmiany dobrobytu z modelu (gra / BK) per miasto.
2. Braavos i Rhyos: czy zarobek spadl przez przyciecie plac do pustej kasy (`TownFolk.Live.cs:824`), czy przez spadek produkcji wyrobow (doplata do 35% od wartosci wyrobow), i czemu kasa kupcow Braavos sie wyczerpala (doplyw netto -1.9 tys. zl/d w Q7). Linia: per miasto w `kiesa-obieg.csv` kolumny "przyciete do kasy (zl)" i "wartosc wyrobow rzemiosla", w `kasy-miast.csv` sprzedaz i zakupy kasy osobno.
3. Ceny samego jedzenia w miastach - log ma tylko indeks calego koszyka. Linia: co 7 dob indeks kategorii zywnosci (grain, fish, meat, cheese, butter...) per miasto.
4. Dowoz jedzenia do konkretnych miast (Braavos, Oldtown, Qarth) - brak miary. Linia: sztuki zywnosci dowiezione wozami wsi i karawanami do miasta co 7 dob.
5. "Potrzeba" w miescie z Afford < 1 jest liczona z wydatku gry podzielonego przez czynnik kotwicy (`TownFolk.Live.cs` Predict) - nie wiem, ile z wzrostu potrzeby w biednych miastach (Highgarden d728: 45 tys. przy wydatku gry 13 tys.) to to dzielenie, a ile prawdziwy popyt. Linia: w `kiesa-obieg.csv` potrzeba przed dzieleniem i czynnik kotwicy.
6. Rozbieznosc miar budow: "Roboty ludu" - srednio 190 osad "bez materialu"; kiesa1 P1-17 - 91 miast z budowa i materialem na polce. Linia: w "Roboty ludu" rozbic "bez materialu" na "brak sztuki" i "cena ponad 2 x wartosc".
7. Ile w zarobku daja rece warsztatow liczone x m_t (`WorkshopLaw` 174.3) - zarobek wedlug zrodel nie rozdziela plac warsztatow od doplaty do 35%. Linia: osobne kolumny w `kiesa-obieg.csv`.
8. Jak bedzie z lokata przy zmeczeniu wojna Diplomacy / 4.33 (od 14.10 10:24) - wiecej wojny w roku 2 = wiecej daniny; brak biegu dwuletniego z ta zmiana.
9. Roznica W52 / W49 w d120 (+2.6 mln) - zakres towary-2; moj pomiar w rozdz. 2.2.

## 11. Zaleznosci i uwagi dla projektow (bez parametrow - to rozpoznanie)

- Danina P1 to ok. polowa tego, co skarbce koron z danina wydaja tej samej doby (d301-364: danina 446.6 tys. zl/d przy wydatkach 942 tys.: zwrot zoldu 341, renty 511, pozostale 91 - kiesa1 P1-7b) [P]. Alarm 2 rachunku 85 tys. (WYNIK-RACHUNEK-85-TYS 7.6: kiesy < 18 mln albo danina < 170 tys. zl/d w 2 oknach) byl w roku 2 na granicy (d533-560: 152.5) [P] - kazda zmiana A5 musi zachowac platnika dla W5 / rent (A3 biedne rody).
- Pieniadze ludu w pokoju (16.6 mln) leza w krolestwach, ktorych rody w pokoju nie maja z czego trzymac wojska (BIEG-DWULETNI 2.2; Z.1 trzyma je ze skrzyni rodow) [P].
- Ta sama nierownosc (zarobek bez zwiazku z ludzmi i dobrobytem) tlumaczy oba objawy - rozwiazanie dla 100% miast musi ruszyc obie strony: miasta z zarobkiem >= 3x potrzeby (55) i miasta < 1x (10), w wojnie i w pokoju [S].
- Pomysly odlozone przez Jeffa (kasy miast z dlugami, pozyczka miejska, karta miejska, sakiewka turniejowa) - nie brane pod uwage.

## 12. Pliki i polecenia

Skrypty i wyniki: `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\c36b73ca-1de0-4b1b-80ce-71bd02afe545\scratchpad\lud\` (dysk C - tylko tam):
- `wyciag.py <log> <wy.json> [dzien0]` - jedno przejscie po logu (linie z rozdz. Zrodla) -> `dw.json`, `z1.json`, `w52.json`, `w43.json`, `gl.json`;
- `kwartaly.py <dw.json> q` - tabele rozdz. 1-3, 6, 8.1, 8.3 (`dw_q.json`);
- `miasta.py <Logs\run>` - siedem miast kwartalami (`miasta_dw.txt`); `lancuch.py <Logs\run>` - udzial w niedoborze, wielkie rynki, zarobek/potrzeba, kasa/zapas, dobrobyt (`lancuch_dw.txt`);
- reszta tabel (4.1, 4.2, 4.3, 5, 8.2 korelacje, luka biedy, bandy) - polecenia `python -I -c` w tej sesji na tych samych plikach CSV / JSON.
Wszystkie skrypty `python -I`, logi czytane strumieniowo.
