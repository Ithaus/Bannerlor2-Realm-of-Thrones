# MAPA: PLACE I LUDZIE (audyt, tylko odczyt, 11.11.2026)

Zrodla: kod `scratchpad/n367c/Armoury/src` (dalej sama nazwa pliku), log `Modules/Armoury/Armoury-2026-11-11_13-19-13.log`
(ostatnia doba 109814/109815 = 1167. doba kampanii, zima Westeros dzien 637 z 1795), docs/CENY-A-DOCHODY-2026-10-12.md (ceny sprzetu -
STARSZE, z 12.10). Wyciagi logu: `bad5/log-last.txt`, `bad5/log-last2.txt`. Rok = 364 dni. [S] = moj szacunek / rachunek z liczb logu.

## 1. Ludnosc - ksiega ludzi

- Tabela krain: `PopulationLaw.cs:43-75` (Reach 8 mln, Polnoc 3 mln, Volantis 5 mln ...), kalibracja raz na kampanie: ludzie = hearth x k (wies),
  stan miasta zamrozony (E4/4). Log "Ludzie (ksiega)": zywi 52 492 552; wsie 41 696 472 (569 wsi, 217 715 hearth, ok. 191 ludzi/hearth [S]);
  miasta 10 373 838 (89 miast); uchodzcy 103 641; SLUZBA 269 271; JENCY 1 839; wyrzutki 5 648; weterani 3 094; NIEWOLNI 32 371.
- Udzial miejski swiata 19.9% (log "Miasta (E4/8 R2)"); tabela: Westeros 5-10% (Ziemie Korony 75%), Essos 25-40%.
- Stany (ZC2, `DailyLife.Basket.cs:147-170`, log "Zycie (ZC0)"): niewolni 10.70%, chlopi 71.08%, biedota 8.41%, rzemieslnicy 6.94%, kupcy 1.39%,
  szlachta 1.46%; Westeros chlopi 86.65%, Essos niewolni 22.58%.
- TRZY SKALE NARAZ (najwazniejsze dla sensu calosci):
  - ksiega ludzi 1:1 (52.1 mln),
  - glowy BK, na ktorych stoi pieniadz ludu: BK 2 893 498 (szlachta 58 406, rzemieslnicy 287 103, chlopi 1 897 010, niewolni 159 208,
    dzierzawcy 491 771); tabela / BK = 18.02 (log "Miara historyczna cz. 2"); glowy kiesy: miasta 1 552 285, wsie 1 033 640, podzamcza 326 542,
  - targ: 1 sztuka na polce = 37 prawdziwych (`Settings.cs:3919` DailyLifeMarketScale = 37; log "sigma 37").
  Zolnierz, kon, miecz, zold sa 1:1.

## 2. Place

| Kto | Regula (plik:linia) | Liczba z logu |
|---|---|---|
| Zolnierz partii rodu | gra `DefaultPartyWageModel.GetCharacterWage` (cs_full.cs:66103): tier 0-6 = 1/2/3/5/8/12/17 zl/d, najemnik x1.5; konny x1.5 (`MountedWage.cs:27`, `Settings.cs:845`); BK BaseWage x1.25 | naliczone 355 176 zl/d na ok. 68 100 ludzi = ok. 5.2 zl/d [S]; do sakiewek ludzi 351 924 (`SoldierPay.cs:14-36`, log "Zold") |
| Zaloga | to samo, BK garnizon -50% | 167 716 zl/d na 72 864 = 2.30 zl/d [S]; 83 721 do sakiewek, 83 846 do kas miast i zamkow |
| Straz karawan | polowa zoldu (288) | 54 810 zl/d na 15 011 = 3.65 zl/d (t1-3 2.14, t4 3.60, t5+ 5.43) |
| Rycerz BK | `Settings.cs:2094` GentryKnightWage 24 | nalezny 2 328 = zaplacony 2 328 |
| Zalogi okretow Wysp | pol zoldu w lupie (WW-J) | w zlocie 25 177 z 56 375 |
| Straz, Wolni Ludzie | zold 0 (`MountedWage.cs:41`) | - |
| Rzemieslnik (dniowka) | `Settings.cs:1234` 3 zl; mistrz 3 + 1.5/tier (`:1354-1355`); x poziom plac miasta = dobrobyt/4800 w 0.5-1.5 (`TownWage.cs:10`) | place warsztatow 23 304 zl/d (291 warsztatow, pracowalo 119) |
| Lud miast W/R/P | robocizna handlu 20%/10%/4% sprzedazy kupcow, podzial W90/R10, obrot P 3%, 35% wartosci wyrobow, 25% wydatkow wojska w miescie, 20% dworu (`TownFolk.cs:59-62`, `Settings.cs:2899-2906`) | dochod z zewnatrz 181 462 zl/d; robocizna z kas 168 104; zaplacone przez lud 331 126 (W 251 324 / R 54 416 / P 25 386) |
| Chlop | 65% utargu wozow (`TownFolk.cs:61`) + plon zjedzony z wlasnego pola | utarg oddany 397 232: pan 33.9%, majatki BK 18 508, wies razem 61.4% |
| Werbunek | 10 dni zoldu, najemnik x2 (`RecruitCost.cs:19`, `Settings.cs:1453`), zloto do notabla / miasta (`LevyGold.cs:13-15`) | - |

Regulatory plac (tylko miasta z KS - 47 z 97):
- brak rak: wskaznik +3%/7 dob do x1.30, przy wolnych rekach w dol do x0.60 (`KsWageHands.cs:10-17`, `Settings.cs:4245-4257`).
  Log "Place za brak rak": w gore 2 miasta (Sharp Point, Castle Black 1.300), na podlodze 0.600 - 45 miast.
- placa wedlug koszyka: do 15 dni koszyka w kiesie pelna stawka, przy 30 dniach 10% stawki (`KsFolkWage.cs:18-24`, `Settings.cs:4260-4263`).
  Log "Place W wedlug koszyka": kappa mediana 0.700, min 0.100; place W z kas pelne 95 933 = zaplacone 27 199 + zostalo w kasach 68 734.
- to samo dla R i P (`KsFolkIncome.cs:20-26`): z zewnatrz pelne 100 062 = przyjete 41 775 + oddane kasom 58 287.
- zamkniety pieniadz zachowany: czego lud nie dostal, zostaje u placacego (kontrole w liniach = 0).

Dochod na glowe w prawdziwych pensach (log "Zycie (ZC0)", dochod gry x sigma + plon z pola): swiat 558 zl/rok; niewolni 199, chlopi 508,
biedota 846, rzemieslnicy 891, kupcy 1 085, szlachta 1 413, wielkie rody 70 036. Dostatek (dochod / koszyk przetrwania): swiat 3.65,
chlopi 3.56, biedota 3.86, rzemieslnicy 6.44, kupcy 5.64, szlachta 9.07, niewolni 1.03.

## 3. Koszyk konsumpcji

- Koszyk pieniezny klasy na glowe BK na dobe (`TownFolk.cs:49-50`): W 0.27, R 0.50, P 1.46, niewolni 0.20, podzamcze 0.18, wies 0.07 zl;
  jedzenie w koszyku W 80%, R 70%, P 45%. Czynsz W 8% / R 13%, leczenie 1-2%, podatek pana W 1% / R 2% / P 3%, akcyza 2% (`:51-56`).
  Zapas dni W 10 / R 21 / P 30 (`:54`).
- Koszyk w kg (ZC0/ZC4, `DailyLife.Basket.cs:30-39`): chlop 0.52 kg zboza + 0.30 l piwa + 25 g miesa; biedota 0.50 kg; kupiec 0.42 kg zboza,
  200 g miesa, 0.9 l piwa. Koszyk przetrwania Allena 0.47 kg zboza (`:12-13`): miasto 0.566 zl/d, wies 0.322 zl/d (mediany z logu).
- Popyt od koszyka (ZC4, `StartCalibration.Zc4.cs:20-33`): potrzeba = ludzie stanow x koszyk w kg / (37 x kg sztuki); kotwica = min(potrzeba x cena, kiesa);
  drabina: ziarno, sol, napoj, bialko, reszta.
- Log "Kiesa ludu": koszyk ludu 1 080 080 zl/d, wydatek gry 344 799 (jedzenie 195 290 = 57%); kiesy 9 260 105 (W 5.40 mln / R 1.71 / P 2.15);
  zapas dni W 15.3 / R 16.5 / P 64.2; stac jedzenie W/R/P 87.2 / 78.2 / 87.7%, pelny koszyk swiat 77.1%; miast ze "stac jedzenie" < 95%: 17.
- Log "Spizarnia (ZC)": potrzeba 0.885 kg i 0.313 l na osobe, 2 314 kcal; z pola i domu 76.7%, z targu 23.2%;
  z targu brakuje 50.1% (zboze 2 374 z 6 173 t, ryby 1 013 z 1 270 t, wino 1 250 z 1 278 tys. l). To pomiar - nikt od tego nie umiera.
- Zbytki (`Settings.cs:3088-3095`): 1/60 nadwyzki na dobe; oferta 79 307 zl, kupione 34 563 (43.6%), 850 par miasto-kategoria ponad cene graniczna.
- Chleb biedoty (M3): luka 259 501 zl w 26 osadach, zaplacono 41 030 (korona 23 149, pan 17 881), nie stac nikogo 218 471 w 18 osadach
  (Braavos, Qarth, Volantis, Qohor, Norvos).

## 4. Placa wobec cen

Ceny: `HistoricalPrices.cs:44-67` (pensy za kg): zboze 0.31, chleb 0.55, piwo 0.3, mieso 0.9, ser 1.3, sol 0.18, narzedzia 4; wol 157, krowa 113, owca 17.
Bron i zbroje = material + dni pracy x dniowka mistrza + 25% zysku (`:22-25`, `Settings.cs:1354-1356`).

| Rzecz | Cena | Dni zoldu przecietnego zolnierza (5.2) | Dniowek rzemieslnika (3) | Historia |
|---|---|---|---|---|
| zboze 10 kg | 4.6 placone (log "Zboze zima (322c)"), wartosc 3 | 0.9 | 1.5 | 3.1 |
| koszyk przetrwania 1 osoby, miasto | 0.566 / dobe | zold karmi 9 osob | dniowka karmi 5 osob | robotnik 1.5-2 d ledwo karmil rodzine [S] |
| miecz t3 / t5 | 30 / 116 (12.10) | 6 / 22 | 10 / 39 | 12-36 / 60-240 |
| kolczuga t4 | 468 wartosc, ok. 1 075 placone (12.10) | 90 / 207 | 156 / 358 | 240-1 200 |
| plyta t6 | 2 798 (12.10) | 538 | 933 | 1 920-3 920 |
| kon juczny | 99 | 19 | 33 | 60-160 |
| rouncey t3 | 1 049 (12.10) | 202 | 350 | 160-480 - za drogi 2-6x |
| destrier | 10 000 | 5.3 roku | 9 lat | 9 600-24 000 |

Ceny sprzetu sa z dokumentu z 12.10 - dzisiejszych cen z polek nie zmierzylem.

## 5. Pobor z pol

- Ochotnik schodzi z DOM (hearth) przy werbunku: `PeopleBook.Origin.cs` (LevyLive), log "Pochodzenie (E4/7)": pobor z DOM 360 ludzi/d
  (wsie 34, miasta 246, zamki 50, autowerbunek zalog 30), powroty z pochodzeniem +300/d; hearth -2.147 / +2.245.
- Milicja z wolnych rak 312/d, do domu 210/d; nowe druzyny 97; karawany 33 (log "Wojsko z ludzi (E4/8)", kontrola 0.00).
- Wolne rece (`FreeHands.cs:15-25`): a = (sluzba + polegli i niewola od siewu) / (0.27 x ludzie regionu); prog 20% (`Settings.cs:1622`),
  Wyspy do 40%, Wolni Ludzie 100%, zniwa x0.5 (dni 196-252). Ponad progiem: plon x max(0.5, ((1-a)/(1-f))^0.5) i brak nowych ochotnikow.
- Plon wsi liniowo od ludzi (`VillageByPeople.cs:19-22`); spustoszenie: plon x (ludzie w domu / ludzie wsi) (`Settings.cs:1561`).
- Chec do sluzby: 0.05 + nadwyzka rak + 0.2 x nedza (`Levy.cs:109`); log "Pobor": srednio x0.31 szansy BK.
- STAN DZIS (log "Wolne rece"): a srednio 3.17% mezczyzn, ponad progiem 5 z 227 regionow (Ziemie Korony, Dragonstone - 310 losowan odmowionych),
  plon obciety w 10 regionach (glownie Mur). Sluzba 269 271 = 0.51% ludzi [S].
  Wniosek: regula "rekruci z pol" jest w kodzie i domknieta, ale przy tej mobilizacji prawie nie dziala; produkcje tnie spustoszenie
  (Duskendale: 47% ludzi w ucieczce, plon x0.29), nie pobor.

## 6. Wzrost i ubytek

- Wies: g [%/rok] = 0.5 + 0.6 x dobrobyt - 0.6 x niebezpieczenstwo - 1.5 x glod, w granicach +-1.3 (`PopulationLaw.cs:423, 600-605`,
  `Settings.cs:1538-1546`). Log "Ludzie": +618/d, +0.539%/rok; pokoj +0.625%, wojna +0.443%; rosnie 548 wsi, kurczy sie 5.
- Miasto: stopa wsi - 1 pkt (`TownGrowth.cs:14`, `Settings.cs:1648`): -155/d (-0.54%/rok); migracja z wlasnych wsi +287/d (`:16-18`).
- Wojna: polegli 212/d (bitwa 164, Inni 39, zaraza obozowa 9); od siewu 239 313 w 1 166 dob. Spustoszenie: 0.5 czlowieka na zbrojnego na dobe
  lupienia, 2% zabitych (5% u lowcow niewolnikow) (`Settings.cs:1550-1557`); dzis trafieni 284 = zabici 7 + las 10 + uchodzcy 267.
- Glod (`Famine.cs:21-30`): plon = 1.2 x ludzie x q; spichlerz do 2 lat; ponizej 90 dni wszyscy jedza 90%; zgony 70%/rok x ((brak - 10%)/90%)^2.
  Log "Spichlerze": zapas 411 dni potrzeby, plon dzis 86.4% potrzeby (zima tnie 40.8%), glodnych wsi 0, zmarli mieszczanie 2.26/d
  (New Ibbish), od poczatku kampanii 338. Do konca zimy 1 158 dni: zapas 17 096 mln racji, brak do wiosny 10 471 mln (log "Glod zimy (324)").
- Zly rok: plon x0.8-1.2, raz na ok. 20 lat x0.6 (`HarvestYear.cs:18-28`); losowanie tylko w oknie zniw lata/jesieni - w latach zimy go nie ma.
- Zmeczona ziemia (`TiredLand.cs:15-18`): ponad ziemie startu plon na glowe spada; dzis 20 okregow, srednio x0.967.
- Zaraza: tylko obozowa w oblezeniu (`CampFever.cs:17-31`: 0.6%/d po 14 dniach, co dziesiaty chory umiera). `PlagueWatch.cs` to meldunek o chorobie
  bohatera z AI Influence. Zarazy ludnosci cywilnej nie ma.
- Niewolni: 5%/rok zgonow; uchodzcy 1.3%/rok.
- Bilans doby [S]: +618 wsie -155 miasta -212 polegli -7 -4 -4 -2 = ok. +230/d = +0.16%/rok.

## 7. Migracje i ruchliwosc

- Wies -> wlasne miasto: 287/d (wyzej).
- Uchodzcy z najazdu (`Settings.cs:1558-1592`): do miasta w promieniu 250, miasto przyjmuje 1% swoich ludzi na dobe, powrot po 30 dobach spokoju
  rowno przez 364 dni; biora swoja czesc kiesy wsi. Log "Uchodzcy do miast": 103 641, w miastach 86 077 (Maidenpool 10 926); przyjac moze 47 z 97 miast.
- Uchodzcy z glodu: w kodzie (`Famine.cs:32-40`), dzis 0.
- Wyrzutki (`Settings.cs:1484-1534`, `OutlawLaw.cs:26-33`): pula 5 648; naplyw 163.5/d (dezercja 101, rozbici 24, spalone wsie 9.7, bieda i wojna 10.8),
  odplyw 176; band 563, ludzi 10 995, kiesy band 30 290 zl.
- Dezercja AI 67/d, z tego 48 "morale w glodzie" (log "Dezercja AI"); brak zoldu: 2 dni laski, potem 0.5%/d (`Settings.cs:993-995`).
- Weterani: 33% zwolnionych od tieru 3 do karczmy, 120 dni czekania (`Settings.cs:1653-1655`); pula 3 094.
- Awans i spadek stanow (`DailyLife.Mobility.cs:17-30`): w gore 3%/rok, w dol 6%/rok, wyzwolenia 0.5%/rok. Log "Awans (ZC6)" za 7 dob:
  w gore 2 067, w dol 1 570. Powiazanie Dostatku z dobrobytem gry: WYLACZONE ("dobrobyt nie").
- Migracji miedzy krainami za lepszym zyciem nie ma.

## 8. Ocena: logika i sredniowiecze

Trzyma sie:
1. Obieg ludzi domkniety: kontrole 0.00 we wszystkich liniach ksiegi; ludzie znikaja tylko przez smierc.
2. Zold, cena werbunku, dniowka, ceny zboza i sprzetu w skali 1 zl = 1 pens; sluzba 0.51% ludnosci (Anglia ok. 1300: 0.5-0.6% [S]).
3. Demografia: +0.54%/rok wsi, miasta ujemne i zasilane ze wsi, wojna zjada ok. polowe przyrostu, glod kwadratowy, zly rok raz na 20 lat.
4. Place reaguja na brak rak; wsie zyja w 77% z wlasnego pola.

Nie trzyma sie:
1. Wojsko 1:1, lud 1:37. Zold swiata ok. 578 tys. zl/d (355 + 168 + 55) wobec 331 tys. zl/d wszystkich wydatkow 10.4 mln mieszczan. Partie w polu
   (ok. 94 tys. ludzi, 1 sztuka na 20 ludzi na dobe) zjadaja ok. 4.7 tys. sztuk/d wobec 11.4 tys. sztuk/d wszystkich mieszczan [S].
   Wojsko wazy na pieniadzu i na polkach kilkanascie razy wiecej niz w sredniowieczu.
2. Piramida dochodow plaska: rzemieslnik 891 wobec biedoty 846 zl/rok (x1.05; historycznie 1.5-2x), kupiec nizej Dostatkiem niz rzemieslnik
   (5.64 wobec 6.44), szlachta x1.7 biedoty (historycznie 5-10x [S]), a wielkie rody x126 sredniej.
3. Lud za zamozny: dochod 558 zl/rok na glowe wobec 200-240 d Anglii ok. 1300 (zrodlo: komentarz `PopulationLaw.cs:23`); biedota trzyma 15 dni
   koszyka, kupcy 64 dni. Placa W jest termostatem oszczednosci (45 z 47 miast na podlodze x0.60, kappa 0.70), nie cena pracy.
4. Dwa rezimy: 47 miast z koszykiem stanow (KS/ZC4), 50 na starym koszyku klas - dwie zasady na jedno zjawisko.
5. Dwa systemy jedzenia miast: z targu brakuje 50% (Spizarnia), a umiera sie tylko przy Food 0 warowni; glod KS daje kare lojalnosci, nie zgony.
6. Renta nominalna 40 zl/glowe/rok to 5 728 019 zl/d naleznych, zaplacone 362 833 (6.3%); panowie maja 2.8 zl/rok na glowe tabeli.
7. Dobrobyt miast oderwany od ludzi: z dobrobytu byloby 19.85 mln, ksiega ma 10.37 mln (dryf +9.48 mln, log "Ludzie").
8. Brak zarazy cywilnej, brak migracji miedzy krainami, nadmiar rak prawie wszedzie.
9. Trzy liczby niewolnych: ksiega NIEWOLNI 32 371, BK 159 208, stany ZC2 10.7% swiata (ok. 5.6 mln [S]).

## 9. Czego nie ustalilem

- Dlaczego KS obejmuje 47 z 97 miast (celowo czy niedokonczona rampa) - nie czytalem `StartCalibration.Ramp.cs` / `.Live.cs`.
- Dzisiejszych cen sprzetu i koni na polkach - tabela z dokumentu 12.10.
- Reszta rownania krain: swiat -18 271 ludzi od dnia 108648 (najgorzej sturgia -0.276%) - przyczyna nieznana.
- Udzialu jedzenia w wydatkach na prawdziwa glowe wedlug stanow (mam tylko swiat 57% i stale 80/70/45%).
- Sciezki zarobku chlopa krok po kroku (`TownFolk.Ks.cs`, `MoneyLedger.Villages.cs`) - znam tylko sumy z logu.
- Czy zima 1 795 dni ma w kodzie reguly wyjscia poza bilansem spichlerzy; nie sprawdzalem `WinterBite.cs` / `WesterosClimate.cs`.
- Kodu BK (bk.cs: klasy, DeduceManpower, WorkforceExcess) i BetterEconomy nie czytalem - reguly BK znam z komentarzy Armoury.
- Jeden log, jedna doba; trendow z 36 dob sesji nie liczylem.
