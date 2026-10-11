# Etap 6 - rozpoznanie A: liczby z logow (11.10)

Tylko pomiar, bez projektu. Kod nie byl zmieniany, gry nie uruchamiano.

**Zrodla**
- **Z31** = `scratchpad/kopia-z31-A/Armoury-2026-10-11_08-09-11.log` (31 756 linii), zbior 31, 120 dob kampanii
  (dzien gry 108836 = doba 1), kod etapu 4 (bez etapu 6).
- **D728** = `scratchpad/kopia-728/Armoury-2026-10-10_20-58-02.log` (183 889 linii), 728 dob, **starszy kod**
  (przed czescia poprawek etapow 3-4; liczby tylko jako tendencja dluga).
- Parser: `scratchpad/e6A/parse.py` + `report.py` (szeregi dobowe -> `z31.json`, `d728.json`).

**Notacja:** `L123` = numer linii w logu. `d31-60 313 (L8124-15693)` = srednia dobowa z dob 31-60, pierwsza i ostatnia linia
okna. Bez dopisku "D728" liczba jest z Z31. Doba 1 obu logow liczy zapas startowy jako "produkcje" (ColdStart, StartStock
L208-213) - pomijam ja w srednich.

---

## 1. Progi "Skonczone, gdy" (stan dzisiejszy, przed etapem 6)

| Prog etapu 6 | Dzis (Z31, doba 120) | Wynik | Dowod |
|---|---|---|---|
| Cos na tulowiu >= 75% zolnierzy AI | **93.6%** (partie + zalogi wazone ludzmi); same partie rodow 91% "dowolny szczebel" | **SPELNIONY** juz dzis | L31426 |
| ... ta sama miara wedlug szczebla (tier jednostki) | korpus szczebla razem 74.5%, partie 63%, zalogi 89% | na granicy / ponizej | L31426 |
| Skor nie ubywa szybciej niz 10/d | skora (leather) d30 622 -> d120 560 = -0.7/d; ostatni miesiac -4.5/d; skory surowe (hides) rosna | SPELNIONY | L7912, L23640, L31572; L31567 |
| Plotna nie ubywa szybciej niz 10/d | plotno (linen) d30 879 -> d120 948 (+0.8/d) | SPELNIONY | L7914, L31574 |
| Len nie rosnie 3 miesiace z rzedu | len (flax) +35/d, +26/d, +24/d (miesiace 2, 3, 4) | **NIE** | L7901, L15744, L23629, L31561 |
| Welna nie rosnie 3 miesiace z rzedu | welna +42/d, +27/d, +3.8/d (ostatni miesiac prawie plasko) | **NIE** (formalnie) | L7933, L15776, L23661, L31593 |
| W linii "Konie" kazdy znikajacy kon ma przyczyne | linii "Konie" **nie ma** (6.11 niezrobione); znane ujscie bez odbiorcy: LegendaryLaw kasuje ok. 9.5 egzot. wierzchowca/d | **NIE DA SIE ZMIERZYC** | L23591-31253, L31518 |

Uwaga do progu 75%: od projektu 174 (rozdz. 4.2) "cokolwiek na tulow < 75%" bylo progiem alarmu. Dzis jest 93.6%,
w D728 nawet 97.7% (d725, L182833). Braki sa w **szczeblu** (t3+), nie w ilosci: partie maja t3+ korpus 25%, bron 35% (L31426).

---

## 2. Co kuja warsztaty miast (dziennie, wedlug rodzaju)

Linia `Warsztaty:` (d1-10 L328-2645, d31-60 L8124-15693, d61-90 L15968-23571, d91-120 L23857-31497); strzaly i belty
z linii `Strzelarze (172)` (d91-120 L23858-31498); zakupy z `ZakupyAI wedlug kupujacego` (d91-120 L23775-31428).

| Rodzaj | d1-10 | d31-60 | d61-90 | d91-120 | doba 120 (L31497) | cel 174.3 (PROJEKT-174 4.1) | AI kupilo/d, d91-120 |
|---|---|---|---|---|---|---|---|
| Zbroja na tulow (BodyArmor) | 42.5 | **312.8** | 224.1 | **109.8** | 71 | 120-190 | korpus 651.9 |
| Helmy (HeadArmor) | 166.2 | 375.4 | 397.4 | 403.9 | 424 | 200-230 | 453.3 |
| Rekawice (HandArmor) | 194.9 | 171.4 | 169.0 | 217.6 | 172 | 85-100 | \ |
| Nogi (LegArmor) | 366.7 | 256.5 | 175.6 | 87.7 | 67 | 250-350 | > reszta zbroi 766.8 |
| Plaszcz (Cape) | 186.6 | 80.0 | 74.6 | 101.4 | 109 | \ 70-100 | / |
| Uprzaz (HorseHarness) | 16.7 | 17.4 | 14.2 | 20.6 | 38 | / (razem) | |
| Tarcze (Shield) | 26.2 | 129.1 | 146.7 | 149.1 | 193 | 150-240 | 210.1 |
| Bron biala (1H + drzewcowa + 2H) | 93.0 | 471.2 | 434.9 | 386.5 | 478 | 340-375 | 878.1 |
| - w tym 1H / Polearm / 2H | 16.9 / 63.9 / 12.2 | 210.0 / 258.7 / 2.5 | 193.5 / 236.8 / 4.6 | 197.1 / 181.6 / 7.8 | 251 / 215 / 12 | | |
| Luki (Bow) | 222.9 | 190.8 | 196.9 | 172.8 | 185 | 130-170 | \ bron strzel. |
| Kusze (Crossbow) | 26.9 | 40.8 | 32.6 | 30.2 | 31 | 10 | / 404.6 |
| Oszczepy (Thrown) | 2.2 | 12.8 | 14.1 | 14.9 | 14 | - | |
| Strzaly (snopy) | 109.3 | 334.1 | 348.6 | 288.0 | 197 (L31498) | 280-360 | 263.1 (L31498: 292) |
| Belty (snopy) | 128.8 | 128.4 | 92.1 | 94.7 | 79 | 70-90 | 37.2 |
| **Razem warsztaty** (bez strzal) | 1343.7 | 2057.8 | 1879.8 | **1694.4** | 1782 | 1700-2220 | ZakupyAI 2616 szt./d (L23854-31512) |
| w tym t5-6 (wszystkie rodzaje) | 36.7 | 69.0 | 63.6 | 79.3 | 191 | - | |

**Dlaczego zbroja na tulow spada z 313 do 70/d (wyjasnienie z logu, nie z kodu):** warsztat robi "wedlug braku" na polce (L31497: "wybor: wedlug braku").
W d31-60 polki byly puste (BodyArmor na polkach sr. 1243, L7879-15462), od ok. doby 92 polki zapelnil **lup i odsprzedaz**:
nowe sztuki zbroi na polkach w d91-120 (linia `Zbroja na polkach`, L23774-31427): warsztaty 814/d, kupcy (przewoz) 1301/d,
sakiewki ludzi (zolnierze sprzedaja nadwyzke) 1183/d, **przyrost polki inne (lup sprzedany przez gre) 2141/d**, zalogi 270/d.
Doba 120: korpus na polkach t1-2 **9 840**, t3-4 tylko **109**, t5-6 **66** (L31427). Wojsko kupuje 652 korpusow/d,
z czego warsztaty daja ok. 110 - reszta to lup t1-2.

**Braki surowca w warsztatach** (cykle odpuszczone/d): d1-10 1474 (ruda 1203, drewno 763, skora 47, len/welna 158) ->
d91-120 752 (ruda **584**, drewno 177, skora 69, len/welna 47); bez zysku 198/d (L23857-31497). Ruda wiaze najmocniej.

**Rece:** swiat 5922 (L328) -> 6606 (L31497, +11.6%). Cechy w dobie 120 (przydzielone/w zrobionych sztukach, L31497):
krawiec 1687/891, platnerz 1600/2145, miecznik 1283/1646, siodlarz 873/1263, lucznik 1156/926, tarczownik 358/419;
przeniesione z cechow bez roboty 999.

**Wedlug krainy: linii nie ma.** Log nie rozbija produkcji warsztatow na kraine/kulture (ani na tier korpusu). Zastepczo:
- rece wedlug krolestw przy starcie (L246): The Reach 888, House Baratheon KL 547, Qarth 487, Volantis 466, The North 333,
  The Vale 320, Riverlands 312, Braavos 283, Stormlands 255, Targaryen 222, Pentos 176, Dorne 162, Norvos 152, Qohor 152,
  Aegon 149, Iron Islands 148, Dragonstone 126, Tyrosh 113, Myr 106, Lys 99, Lorath 84, Free Folk 80, Dothraki 78,
  Ibben 66, Sarnor 55, Nights Watch 32, Skagos 18, Summer Isles 6, Yi Ti 6 (swiat 5920; Westeros 3221, Essos 2699);
- ruda (L31434): 30 z 97 miast bez rudy (m.in. Braavos, Pentos, Myr, Qohor, Lorath, Ibben, Storm's End, Starfall,
  Mormont Keep, Hardhome); najwiecej cykli "brak rudy" od d115: Driftwood Hall 138, Ibben 121, Lonely Light 121,
  Volon Therys 115, Mhysa Faer 110; najwiekszy zapas: **Pyke 1151, Ten Towers 916, Barrowton 853, Stonehelm 715** -
  wywieziono z nich kontraktami 0 (L31434).
- 6.19 (kultura krainy) i 6.6 (ksiega wedlug zrodla) potrzebuja nowej linii: produkcja wedlug kultury miasta x rodzaj x tier.

**D728 (starszy kod)** - razem warsztaty: d2-30 1449, d91-120 1660, d335-364 1041, d699-728 **782** (L176329-183655);
korpus 75 / 72 / 89 / 60; helmy 314 / 395 / 130 / 162; tarcze 81 / 139 / 150 / 82; strzaly 119 / 303 / 50 / 78 snopow;
rece 6017 -> 7983. Produkcja spadala przy coraz pelniejszych polkach (BodyArmor na polkach 104 043 w d728, L183440) - zbieznosc, nie dowod.

---

## 3. Ile potrzebuje armia

**Pokrycie zbrojowni AI** (co 5 dob; doba 120 L31426):
- partie rodow AI (107 163 ludzi): wedlug szczebla korpus 63%, glowna bron 79%, helm 45%, tarcza 31%, rece 49%, nogi 90%,
  strzaly 73%, belty 76%; dowolny szczebel: korpus 91%, bron 87%, helm 49%, tarcza 33%; t3+ korpus 25%, bron 35%.
- zalogi AI (81 025): wedlug szczebla korpus 89%, bron 97%, helm 79%, tarcza 62%; dowolny korpus 96%.
- **brakuje razem 261 058 szt. (korpus 48 064, bron 64 385)**.
- brak korpusu rosnie: d5 13 725 (L1282), d30 29 713 (L7768), d60 37 404 (L15614), d90 44 907 (L23493), d120 48 064
  -> +256/d, +250/d, +105/d. Armia rosla do ok. d90 (partie 38 tys. -> 107 tys., L1282 -> L31426), potem stoi.
- D728: brak korpusu szczyt 59 504 (d400, L100792) -> 26 417 (d725, L182833) przy armii malejacej 117 tys. -> 70 tys.

**Zakupy AI** d91-120 (L23775-31428), szt./d: korpus 652 (lordowie 314, zalogi miast 135, notable dla ochotnikow 119,
zamowienia zamkow 81, zalogi zamkow 10), helm 453, reszta zbroi 767, tarcza 210, bron biala 878, strzelecka 405,
amunicja 477; zatrzymane na rezerwie kramu 5148/d. Zastepcze (pytanie 2): korpus 172/d, bron 140/d (L23854-31512).
Dozbrajanie doby 120: pominiete koszyki "brak lepszej na polce" 26 255 (L31513).

**Rekruci:** tier 1 z wlasnym dobytkiem 584/d, z zapisu notabla 287/d (d91-120, L23844-31506).

**Odziez wojska (150)** d91-120 (L24004-31674): potrzeba narasta skora 83/d, sukno 129/d, plotno 93/d; partie kupily
skora 29, sukno 50, plotno 39/d; **czeka: skora 2149, sukno 3047, plotno 2887** (L31674) i rosnie (+11, +6, +9/d w ostatnim
miesiacu, L23745 -> L31674); wizyt bez towaru na polce 40/d.

---

## 4. Surowce: zapas swiata i przeplyw

Ksiega `Towary:` (zapas wszystkich posiadaczy). Zapas w dobach 2 / 30 / 60 / 90 / 120 i zmiana na dobe w miesiacach 2-4:

| Towar | d2 | d30 | d60 | d90 | d120 | zmiana/d m2 / m3 / m4 | prod. / zuzycie d91-120 | linie |
|---|---|---|---|---|---|---|---|---|
| len (flax) | 3929 | 9754 | 10803 | 11583 | 12302 | +35 / +26 / +24 | 388 / 354 | L638, L7901, L15744, L23629, L31561 |
| welna (wool) | 2446 | 6030 | 7279 | 8090 | 8203 | +42 / +27 / +3.8 | 322 / 313 | L670, L7933, L15776, L23661, L31593 |
| skory (hides) | 150 | 3147 | 4758 | 5530 | 5674 | +54 / +26 / +4.8 | 368 / 363 | L644, L7907, L15750, L23635, L31567 |
| skora (leather) | 157 | 622 | 533 | 694 | 560 | -3 / +5 / -4.5 | 159 / 163 | L649, L7912, L15755, L23640, L31572 |
| plotno (linen) | 759 | 879 | 914 | 989 | 948 | +1 / +2.5 / -1.4 | 153 / 154 | L651, L7914, L15757, L23642, L31574 |
| filc/sukno (felt) | 438 | 3331 | 6340 | 8820 | 9862 | +100 / +83 / +35 | 240 / 202 | L636, L7899, L15742, L23627, L31559 |
| ruda (iron) | 1471 | 5537 | 8067 | 10081 | 11202 | +84 / +67 / +37 | 279 / 237 | L647, L7910, L15753, L23638, L31570 |
| drewno (hardwood) | 12827 | 23417 | 37982 | 52837 | 59167 | +486 / +495 / +211 | 2310 / 2072 | L643, L7906, L15749, L23634, L31566 |

- Skora i plotno w swiecie sa male (560 i 948 = ok. 3-6 dob zuzycia) i stoja w miejscu, bo zuzycie = produkcja;
  popyt niezaspokojony rosnie gdzie indziej (odziez wojska czeka 2149 skor, naprawy czekaja na 1700 szt. skory - rozdz. 7).
- Filc: 8801 z 9862 lezy w **taborach karawan** (posiadacze L31559), a odziez wojska czeka na 3047 sukna.
- Posiadacze lnu (L31561): miasta 4516, zamki 1671, wsie 1247, wozy 1080, karawany 3386. Welny (L31593): miasta 4015, zamki
  1413, wsie 875, wozy 514, karawany 1378. Skor (L31567): miasta 4119, karawany 1462.
- Rzemioslo miasta (148) doba 120 (L31496): len->plotno 104, welna->filc 169, skory->skora 143; rece zajete 25%;
  koniec przerobu "bez zysku / brak surowca / rece": len 42/42/12, welna 54/29/13, skory 66/17/13.
- Rzemieslnicy BK dopisuja skory 409/d (zdjete 88/d) - glowne zrodlo skor (L23861-31516).
- **Ruda** (L31544): wsie 301/d (26 wsi), kopalnie miast i zamkow +0; zuzycie: warsztaty zbrojne 242, narzedzia 32,
  strzelarze 12; zapas: miasta 5996, zamki 1021, wsie 880, tabory 3305 (karawany 2219); miast bez rudy 34 z 97.
  Ksiega Towary: przepadlo z rozbitymi partiami 34/d, bez wyjasnienia -5.3/d (d91-120, L23911-31570).
- **Drewno** (L31545): wsie 256/d + las wsi 2054/d; zuzycie: zbrojne 1472, budowy 525, strzelarze 73; zapas 59 167
  (miasta 38 598); Towary: przepadlo 89/d, gnicie BK 40.5/d, bez wyjasnienia -27/d (d91-120, L23907-31566).
- **Kontrakty surowca (174)** d91-120 (L23779-31433), szt./d: ruda 27.8, drewno 45.7, len 76.3, welna 39.8, skory 31.2,
  skora 3.5, plotno 6.0; zawarto 18.4, dojechalo 17.6/d. Doba 120: bez kontraktu "bez drogi" 98, brak karawany w zrodle 80
  (L31433).
- **D728:** len szczyt 15 393 (d268, L67271) -> 9907 (d728, L183712); welna szczyt 13 006 (d501, L126447) -> 7883
  (L183744); skory szczyt 7706 (d142, L35405) -> 1476 (d728, L183718); skora 400-960, plotno 540-940 bez trendu; filc szczyt
  12 677 (d212, L53032); ruda rosnie do 25 515 (L183721); drewno szczyt 158 470 (d437, L110255). Na dlugim biegu len i welna przestaja rosnac, skory spadaja.

---

## 5. Towar bez zbytu i kasowanie

- **BK gnicie surowcow (174.3)**: skasowano d31-60 47/d, d61-90 94/d, **d91-120 135/d** (L23939-31598), doba 120: 175
  (drewno, len, welna 0.2% i inne 2% ze stosow > 500); "oszczedzone wobec BK" 530/d. Najwiecej w d91-120: sol 82.7/d,
  drewno 40.5/d, glina 12/d, welna 0.1/d.
- **Kasowanie gry uzbrojenia: 0** przez cale 120 dob (zablokowane; bez blokady ok. 227 szt./d) - L31596; konie 0.
- **Zlom z nadmiaru (174)**: 27.6 szt./d (d91-120), nadmiar swiata ponad rok popytu ok. 5707 szt. (L31435).
- Stosy rosnace w ostatnim miesiacu (szt./d, ksiega Towary d91 -> d120): drewno +200, zboze +65, glina +43.5,
  sol +36.5, filc +36, ruda +35.6, bawelna +29.6, narzedzia +23.3, len +21, wapien +14, oliwa +9.7, bizuteria +9,
  ceramika +7.9.
- **Polki uzbrojenia**: 51 223 szt. (d2, L367) -> 83 151 (L31539): 1H 21 453, nogi 15 406, konie 11 622, korpus 10 238.
  D728: szczyt **438 tys.** (d689, L173600; korpus 114 918), koniec 379 tys. (L183690); stosy 374 tys. (L183747).

---

## 6. Konie

**Udzial jazdy** (`Zold konnych`): partie rodow AI d2 12.3% (L534), d1-10 11.1%, d31-60 9.3%, d61-90 8.5%,
**d120 8.3% = 9224 z 110 505** (L31440); garnizony 6.2% (5031 z 81 025); karawany 56.8%. Cel 6.14: ok. 20%.
D728: 7.0% (d699-728, L176296-183605).

**Wedlug krolestw** (`Musztra AI wedlug krolestw`, konni w partiach rodow; d5 L1287 -> d120 L31431):

| Krolestwo | ludzi d120 | konni d5 | konni d120 |
|---|---|---|---|
| Dothraki Horde | 2167 | 61% | **45%** |
| House Targaryen | 3523 | 28% | 30% |
| The Vale | 7376 | 14% | 13% |
| Sarnor | 2891 | 11% | 13% |
| Qohor | 2122 | 11% | 12% |
| House Targaryen, Aegon | 2497 | 11% | 11% |
| Dorne | 7028 | 19% | 10% |
| The Reach | 8908 | 13% | 8% |
| Qarth / Norvos / Tyrosh | 2889 / 2109 / 1687 | 6 / 8 / 8% | 8 / 8 / 8% |
| House Baratheon KL | 10 429 | 9% | 7% |
| Pentos / Lys | 2182 / 1716 | 12 / 7% | 7 / 7% |
| Riverlands / Dragonstone / Volantis / Myr / Lorath | 5252 / 3926 / 3583 / 2609 / 2326 | 12 / 5 / 12 / 9 / 11% | 6% kazde |
| The North | 11 100 | 10% | **5%** |
| Stormlands | 5085 | 9% | 5% |
| Ibben / Yi Ti | 2679 / 331 | 8 / 9% | 4 / 4% |
| Nights Watch / Braavos / Summer Isles | 4153 / 2523 / 351 | 6 / 7 / 11% | 3% kazde |
| Skagos | 162 | 4% | 2% |
| Iron Islands / Free Folk | 3156 / 2403 | 2 / 2% | **1% / 1%** |
| **Razem (wazone)** | 107 163 | 11.2% | **8.5%** |

D728 d725 (L182838): razem 7.2%; Dothraki 52%, Targaryen 28%, Vale 18%, Dorne 16%, Qohor 16%, North 6%, Iron Islands 0%.

**Skad i dokad** (Z31, d91-120 jesli nie napisano inaczej):
- Na polkach miast koni przybywa: 2171 (d2, L367) -> 5748 (d30, L7612) -> 8421 (d60) -> 10 187 (d90) -> **11 622**
  (d121, L31539) = **+79/d**. Mimo to lordowie: zakupy 28 koni/d, nieudane "brak koni" 22/d (doba 120: pusto 23,
  za drogie 6, L31441).
- **Czeka na konia swojej kategorii 1510 ludzi w 117 partiach** (L31441; d30 314, d60 731, d90 1432); proby awansu
  odrzucone z braku konia 1209/d; wykonane awanse na jezdzca 12.9/d.
- Konni bez konia w zbrojowni: **2914 z 14 255 konnych (20%)** (L31441); wolne konie w zbrojowniach 2018 (zwykle 1897,
  bojowe 113, szlachetne 8); jezdzcy bez konia po dosadzeniu 371/d (Stajnia AI, L23853-31510).
- Doplywy do partii: najemnicy konni z karczmy 29.6/d (d1-10 169/d; L24042-31710), ochotnicy konni od notabli 7.5/d,
  konie za awans do zbrojowni 13/d, z taboru pod jezdzcow 52/d, nadmiar ze zbrojowni do taboru 55/d (Stajnia AI).
- Przecieki wolnych koni ze zbrojowni (L31441, srednio d91-120): jezdzcy zostawieni w zalodze 13.3/d, dezerterzy 6.6/d.
- Bandy trzymaja w jukach 55 koni pod siodlo (stan, L31401).
- **Ujscia bez odbiorcy:** LegendaryLaw "egzotyczne wierzchowce zdjete z polek nie u swoich" 9.5/d (L23591-31253;
  doba 120: 8, L31518) - w kodzie `LegendaryLaw.SweepMarkets` robi `roster.AddToCounts(..., -el.Amount)` bez odbiorcy,
  czyli kon w nicosc. Kasowanie gry: konie 0 (L31596).
- **Ile znika bez przyczyny: nie wiadomo** - nie ma ksiegi koni (zrodla: hodowla/targ/lup/werbunek; ujscia: polegli,
  zakupy, kasowanie). To jest 6.11.
- D728: konie na polkach szczyt 19 567 (d361, L90785) -> **5353** (d728, L183440) = ok. -39/d bez znanej przyczyny
  (brak ksiegi); wolne konie w zbrojowniach 6748 (L183606).

---

## 7. Naprawy sprzetu

`Zuzycie AI` d91-120 (L23842-31511): lup obity 3667/d, zuzyte w bitwach 485/d, **naprawione w miastach 1741/d**
(doba 120: 2972 za 9230 zl z sakiewek ludzi); obitych razem 72 357 w 790 partiach (rosnie: d31-60 18.5 tys., d61-90 39 tys.);
**czeka na material 2070** (metal 625, skora **1700**, plotno 62; L31511), srednio 1975. Zuzyte na naprawy kg/d: metal 75,
skora 106 (doba 120: metal 100, drewno 839, skora 187, plotno 24). Metal na naprawiona sztuke 0.034 kg (wedlug rodzaju
34.89 kg / 2513 szt., stara regula 65.23 kg / 27 szt., L31511). Spoils - naprawa u kwatermistrza: 0 przez 120 dob (L31336).
Naprawy kowali miasta w ramkach uzbrojenia +136 (L31596).
D728: naprawione 1399/d (d699-728), czeka 7936, z tego skora 6316 (L183645) - brak skory hamuje naprawy coraz mocniej.

---

## 8. Co z tego wynika dla pozycji 6.x (fakty, bez projektu)

1. **Prog 75% jest juz spelniony** (93.6%). Braki armii to szczebel: 9840 korpusow t1-2 na polkach, 109 t3-4, 66 t5-6;
   partie maja korpus szczebla 63%, t3+ 25%. Warsztat "wedlug braku" przestaje kuc korpus, gdy polke zapelni lup t1-2.
2. Ruda: 30-34 miasta bez rudy, a w Pyke / Ten Towers / Barrowton / Stonehelm 3600 ladunkow lezy bez wywozu; brak rudy
   to 584 z 752 odpuszczonych cykli/d (6.1, 6.2, 6.7).
3. Len i welna rosna (prog NIE) przy niezaspokojonej odziezy wojska; filc lezy w karawanach; skora i plotno sa waskim
   gardlem napraw i odziezy, choc ich zapas "nie ubywa" (6.3, 6.4, 6.17).
4. Jazda 8.3% (cel ok. 20%): North 5%, Iron Islands 1%, Dothrakowie 45% i spada; 1510 ludzi czeka na konia, a na polkach
   lezy 11 622 koni (kategoria/miejsce - do sprawdzenia w B) (6.12-6.15).
5. Brakuje linii: ksiega koni (6.11), produkcja wedlug kultury miasta x rodzaj x tier (6.6, 6.19); LegendaryLaw kasuje
   konie bez odbiorcy.
