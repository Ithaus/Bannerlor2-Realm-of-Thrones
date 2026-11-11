# MAPA OBIEGU PIENIADZA (audyt, tylko czytanie) - stan na dobe 109814

Zrodla: log `Modules/Armoury/Armoury-2026-11-11_13-19-13.log` (doby 109804-109814, ostatnia doba w calosci) i `Armoury-2026-11-09_21-17-29.log` (50 dob, 109753-109802); kod `n367c/Armoury/src` (dalej `src/`). Liczby "na dobe" = doba 109814, chyba ze napisano "sr. 50 dob". Rok = 364 dni.

## 1. Wniosek w trzech zdaniach
1. Swiat jest zamkniety co do monety: 224 718 502 (d109753) -> 225 207 692 (d109814), +489 tys. w 61 dob = ok. +1.3%/rok. Jedyny istotny kran to mennica (sr. 11 340 zl/d = 1.83%/rok), reszta z niczego / w nicosc to setki zl dziennie. Linia "Pieniadz swiata (bilans - przyczyny)" konczy sie "ARYTMETYKA OK", reszta niezmierzona sr. 28 dob +181 zl/d.
2. Obieg jest pompowany, nie samoczynny: linia "Pompy - sztuczne przelewy" = 1 231 682 zl/d = 235.6% zoldu. Kasy miast oddaja panom i koronom 82.4% doplywu netto kupcow (sr. 28 dob; odniesienie historyczne z tej samej linii ok. 10%).
3. Zloto stoi na gorze: korony 61.66 mln (27.4%), glowy rodow 71.39 mln (31.7%), lud miast+podzamczy+wsi 20.4 mln (9.1%, 0.84 x "poziom normalny"). 46 z 97 kas miast ponizej zapasu kupcow, dlug rad miast 4.02 mln, chleb biedoty: luka 259 501 zl/d, z czego "nie stac nikogo" 218 471.

## 2. Kto trzyma zloto (linia "Pieniadz swiata", MoneyLedger.cs:969 WorldTotal, :980 StateLine)
| Posiadacz | zl | % | zmiana w 61 dob |
|---|---|---|---|
| glowy rodow | 71 390 812 | 31.7 | -285 tys. |
| skarbce krolestw | 61 662 951 | 27.4 | -982 tys. |
| notable | 20 006 004 | 8.9 | -183 tys. |
| kasy miast (kupcy) | 11 781 831 | 5.2 | -64 tys. |
| kasy zamkow | 10 439 198 | 4.6 | -22 tys. |
| kiesy ludu miast (W/R/P) | 9 330 742 | 4.1 | -979 tys. |
| kiesy wsi | 8 389 037 | 3.7 | -234 tys. |
| pozostali lordowie | 8 288 420 | 3.7 | -93 tys. |
| gracz | 4 931 763 | 2.2 | **+4 050 tys.** |
| sakiewki ludzi (wojsko) | 4 105 651 | 1.8 | -714 tys. |
| karawany (kiesy) | 3 230 464 | 1.4 | +32 tys. |
| Bank Zelazny (gotowka) | 3 227 769 | 1.4 | -246 tys. |
| kapital warsztatow | 3 001 874 | 1.3 | -43 tys. |
| podzamcza | 2 610 976 | 1.2 | +46 tys. |
| garnizony (kiesy partii) | 1 470 469 | 0.65 | +5 tys. (stoi) |
| tabory wsi 701 548; liczniki cel 124 942 i podatku wsi 186 623; wedrowcy 146 965; gminy 98 782; kryjowki 46 397; bandy 30 290; weterani w karczmach 4 178 | | | |
Poza "swiatem" ksiegi: skarbce zamkow BetterEconomy (linia "Obieg: BK i BetterEconomy": "poza swiatem", ruch 0 w 50 dobach), bohaterowie Disabled 23 486 zl, naleznosci Banku (rody 205 142, korony 28 327, rady miast 1 964 171).

## 3. Krany i ujscia (z niczego / w nicosc)
Suma 50 dob (pola kontrolne linii "bilans - przyczyny"): zmiana swiata +423 501 (8 470/d).
| Pozycja | sr. zl/d | Gdzie | Ocena |
|---|---|---|---|
| Mennica: nowa moneta z kruszcu (Z6) | +11 340 (doba 109814: 2 848) | CrownMint.cs:107 Daily, :198-201 (kruszec z polki -> ChangeGold kasy miasta + oplata 5% do skarbca; MintSeigniorage Settings.cs:1766) | kran zamierzony, pokryty towarem; 10x wiecej niz zalozenie z naglowka CrownMint.cs:37 (0.16%/rok) |
| Zuzycie monety (U11) | -282 | CrownMint.cs:223-244, TownFolk.Mint.cs:15; 0.3%/rok tylko kiesy ludu i kasy osad (Settings.cs:1769) | ujscie zamierzone |
| "Dochod za tier" w rozliczeniu rodu | +480 (codziennie, 1 rod) | model finansow gry; linia "rody - przyczyny"; "Drobne zrodla" ucina tylko rody AI bez lenn (320 -> 0) | z niczego, nie zalatane |
| Warsztaty BK placone przy pustej kasie miasta (N3) | +793 (39 639 / 50 d) | TownPurse.cs:946 NoteN3 - sam licznik | z niczego, nie zalatane |
| Prowizja od sprzedazy partiom (U2) | -502 | linia "Przeplywy osad (przyczyny)": do licznika cel 40 595, w nicosc 628 | w nicosc |
| BK wydatki lordow - dylematy, kupno (U6) | -390 (2 zdarzenia: 4 500 i 15 000) | BK, tylko pomiar (linia "Obieg: BK i BetterEconomy", "lordowie BK (w nicosc)") | w nicosc |
| BK kopalnie (U4) | -39 | BK | w nicosc |
| Zold karawan lordow | ok. -710 (naliczone 11 585, do sakiewek 10 875) | SoldierPay.cs:463 Route / linia "Zold" | roznica niewyjasniona |
Zalatane i zmierzone jako 0 w 50 dobach: regulator kas gry (TownPurse.cs:386 OnRegulator, CastlePurse.cs:292; zablokowane kasowanie 1 725 431 + 1 043 752 zl/d), "zakupy" mieszczan z niczego (TownPurse.cs:350, CastlePurse.cs:263; cofniete 8 477 + 14 710), dosypka skarbcow krolow (KingdomTreasury.cs:25 Transpiler), pasmo notabli 4 500-10 500 i 10 000 nowego notabla (NotableGold.cs:290 GivePrefix - przez kase miasta), majatki BK z produkcji (EstateIncome187.cs:114), BetterEconomy (wszystko 0), kapital nowych karawan (20 000/d - z kies notabli 5 838 i kas miast 14 162), werbunek (LevyGold.cs:72 - 11 668 oddane sprzedajacym), awanse (3 486 do sakiewek), jency/turnieje/myto (RealPayer - "bez platnika 0").
Zold nie jest ujsciem: gra kasuje 530 844 w rozliczeniach rodow, SoldierPay oddaje 530 462 (sakiewki 446 616, kasy osad 83 846) - SoldierPay.cs:388 Settle, :463 Route.

## 4. Glowna petla (doba 109814)
```
kasy miast/zamkow --raty firm DS (zawor)--> panowie 353 017+15 024 / korony 242 404+7 255
korony --zwrot zoldu 35 885, renty 132 218, W5b 43 072, W5c 6 449, chleb 23 149--> rody / lud
rody --zold 534 477--> sakiewki ludzi 446 616 + kasy osad 83 846
sakiewki --zycie w miastach 153 978, markietani 120 910, zalogi 37 099--> kasy miast, kiesy wsi, lud
kasy miast --utarg taborow 440 711 (+zamki 24 946)--> wsie: kiesa 58.5%, pan 36.9%, majatki BK 4.7%
kasy miast --karawanom 386 192; robocizna/obrot/doplata ludowi 257 966--> karawany, lud
lud --towar 331 126 (miasta) + 27 919 (podzamcza)--> kasy; lud --danina od skarbca 127 165--> panowie 85 199 / kasy 41 966
```
| Przeplyw | Plik / metoda | Regula i stawka | zl/d |
|---|---|---|---|
| Firmy miast i zamkow (dawny zawor) | DsTowns.cs:386 Fix, :1885 Daily; TownPurse.cs:425 Valve; CastlePurse.cs:213 Dues | stala suma roczna = 364 x srednia z 28 dob pomiaru (DsFarmMeasureDays, Settings.cs:4187), rata 1/52 co 7 dob, do podlogi 0.5 x zapas kupcow (:4192); miasto pan 50 / korona 50, zamek 67 / 33. Razem 104.24 mln/rok z kas miast + 12.33 mln zamki + 23.25 mln z oszczednosci ludu = 62% calego pieniadza swiata rocznie | 595 421 + 22 279 |
| Danina pana od skarbca ludu | TownFolk.Hoard.cs:91 PayHoard; TownFolk.DsAkum.cs; TownFolk.Guard.cs:222 | nadwyzka ponad 3 x zapas dni (W 30 / R 63 / P 90 / podzamcze 30 dob); pan 67%, kasa osady 33%. Stawka: linia kiesa2 mowi 1.00%/d, "Akumulator" 3.00%/d, "Straznik" 3.70%/d; faktycznie 127 165 / 6 076 017 = 2.09%/d | 127 165 |
| Prawa panskie od zarobku ludu | TownFolk.Live.cs:914 TownLive | W 3 276 / R 2 187 / P 3 278, cech 1 093, farma gmin 2 473; czynsz wsi 9 816 (571 wsi), czynsz gruntowy 10 080 | ok. 32 000 |
| Podzial utargu wsi | VillageTakings.cs:251 EnterPostfix | pan (licznik podatku) 36.9%, majatki BK 4.7%, wies 58.5%, zniklo 0 | 397 232 |
| Regale gornicze | MineRegale.cs:276 TakeDue | 25% wartosci kruszcu po rachunku za drewno (log; domyslna w Settings.cs:3211 to 0.125) | 13 720 |
| Dziesiecina | TownFolk.Tithe.cs; DsVillage.cs | 5% utargu wsi (19 864) i zarobku ludu (8 716); polowa jalmuzna wraca do kies | 28 580 |
| Myto targowe | TownToll.cs:91-98 | wozy wsi 4%, karawany 2% (Settings.cs:3871-3872) -> glowy rodow 13 225, korony 12 925 | 26 150 |
| Myto Bliznakow | LordToll.cs:16 | 2 zl od czlowieka, 1 zl od zwierzecia, najwyzej dzien zoldu | 0 (sesja 357) |
| Powinnosci wasali | KingdomTreasury.cs:450 Levies | od D stalego rodu, 177 rodow | 17 096 |
| Clo | KingdomTreasury.cs:450; CrownIncome.cs:306 Begin | licznik cel -> skarbiec jednym poborem | 17 559 |
| Podatek BK od bogatych rodow | gra/BK (1% ponad 100 tys.) | przelew glowa -> skarbiec | 39 378 |
| Podatek wojenny ludu (184), danina P1, M4 | KingdomTreasury.WarTax.cs; TownFolk.Savings.cs; KingdomTreasury.PeaceTribute.cs | 184: s do pulapu, krok 0.5 pkt / 7 dob; P1: 2.5%/d x s/3%; M4: do 0.5% lokaty W | 471 + 2 434 |
| Tarczowe W6 | KingdomTreasury.Scutage.cs | 25% wolnego pulapu, najwyzej 1/45 nadwyzki (Settings.cs:3025) | 316 |
| Prawo trzecich 1/9 | Ransom178.cs:947 ThirdsSettle | 1/9 okupow i lupu do korony | 6 161 |
| Podatek warsztatow BK | Seal31B.cs:1093 OwnerTaxDaily | 25.9% wyplaty notabla -> pan miasta | 25 488 |
| Zwrot zoldu z korony | KingdomTreasury.cs:257 WageRefund; KingdomTreasury.RefundNeed.cs | 70% zoldu (Settings.cs:1800) x f = (2R-G)/R; pelny 109 326, wyplacone 35 885 | 35 885 |
| Renty korony | CrownRents.cs:374 Daily; CrownRents.Need.cs | reszta wplywow dnia wg lenn (miasto 2 / zamek 1.5 / wies 0.25) x f; pelne 323 282, wyplacone 132 218, zostaje w skarbcach 191 064 | 132 218 |
| Wplywy dnia korony | CrownIncome.cs:306 | 345 976 + 1/450 zapasu ponad 500 000 (111 009) | 456 985 do wydania |
| Budzet rodu, dwor | ClanBudget.cs:785 Daily, :1323 Court | pulap zoldu z D; R = max(20 000; 20 x D) + 5 000 x doroslych; dwor do kas siedzib | dwor 111 403 |
| Sakiewki ludzi | MenPurse.cs:314 OnLeft; Sutlers.cs:204 | zycie przy wyjezdzie z miasta; markietani 10%/d wolnej sakiewki, 85% miasto / 15% wies | 153 978 + 120 910 |
| Kiesa ludu | TownFolk.Live.cs:765 DailyLive | lud placi kasie za towar; kasa placi robocizne 168 104, obrot P 26 315, doplate 49 067; 5% zaworu miast i 15% zamkow do ludu (69 409 + 51 759) | zob. obok |
| Notable | NotableGold.cs:290 | pasmo przez kase miasta: do kas 134 500, z kas 50 000, uciete 1 193 000 (kasa bez nadwyzki u 404 notabli) | |
| Dochod notabli z warsztatow i karawan | WorkshopTrade.cs:818 ExpensePrefix; CaravanSettle.cs | z kapitalu warsztatow i kas karawan (gra zglasza "z niczego", ksiega koryguje K1) | 98 268 |
| Karawany | CaravanSettle.cs; CaravanCapital.cs | zold polowiczny 109 883 -> 54 810; kapital obrotowy max(10 000, 30 dni zoldu); zysk do wlasciciela 75 759 (25 z 503 karawan), rody 13 631; marza miasta na towarze karawan +125.9% | |
| Bandy | OutlawLaw.cs:1200 Daily, :2545 SellStack | paser placi 50% -> 25% ceny skupu miasta (609 zl; 947 zostaje w kasie), dola 25% do skarbca kryjowki (723), zycie w miastach 1 630; kiesy startowe ze skarbca kryjowki 472 | razem bandy + kryjowki 76 687 zl = 0.03% swiata |
| Chleb biedoty M3 | TownFolk.Bread.cs:164 | korona 23 149 + pan 17 881 -> kiesy ludu | 41 030 |

## 5. Pozyczki i odsetki
| Kto od kogo | Plik | Oprocentowanie / limit | Stan |
|---|---|---|---|
| Rody w Banku (kredyt wojenny) | IronBank.cs:139 LendKw, :233 Lend; DebtLadder.cs:483 LendWar, :891 Seize | krol 20%, rod z lennem 30%, bez lenna 45%, +10 pp przy biegnacym dlugu, +15 pp po zaleglosci (Settings.cs:1785-1789); log: 40% | 205 657 u 44 rodow, dzis 1 307 |
| Korony w Banku (186) | CrownBorrow.cs | 20-30%, oplata 2%, limit 180 dob podatku | 28 327 u 3 krolestw |
| Rady miast (m96-dlugi) | TownDebt.cs | 10% (kupcy, rzemieslnicy, notable), 20% Bank (Settings.cs:2998-2999) | 4 024 046 w 23 miastach (Bank 1 964 171), 4 niewyplacalne |
| Rody od wlasnych miast (f8) | TownFolk.TownLoan.cs | 10%, limit 120 dni renty osady | 362 418 u 25 rodow; 10 roszczen ponad limitem (dlug/limit do 34.0) |
| Rody od ludu (obieg2) | TownFolk.Loans.cs | 12% | 0 (wygaszone, nowe tylko przez f8) |
| Korony od miast (korona2) i zaliczki DS | TownFolk.CrownLoans.cs, TownFolk.CrownLoans.Ds.cs | 10% | 239 295 + 34 842 |
| Okupy na dlug | Ransom178.cs | pulap 364 D | 267 358 |
Bank: start 5 000 000 (Settings.cs:1780, jednorazowo z niczego), gotowka 3 227 769, zysk zrealizowany dzis 384, dywidenda do Braavos co 28 dob (IronBank.Dividends.cs). Wszystkie linie dlugow maja kontrole 0.

## 6. Zold
Naliczony 534 477: druzyny 355 176 (481 partii) -> sakiewki ludzi 351 924; zalogi 167 716 -> kasy miast 56 970, kasy zamkow 26 876, sakiewki zalog 83 721; karawany lordow 11 585. Przyciete, bo saldo nie zmiescilo sie w kiesie glowy: 2 617 (6 rodow). Sakiewki ludzi = 7.9 dnia zoldu. Zold to 86% calego pieniadza swiata na rok (194.5 mln / 225 mln) - to on napedza obieg.

## 7. Przeplywy bez platnika albo odbiorcy; kreacja i niszczenie mimo latek
1. **Sprzedaz sprzetu przez gracza w oknie ekwipunku** (InventoryLogic.DoneLogic -> GiveGoldAction "nikt -> gracz", linia "Komunikaty platnosci" log 09.11 wiersz 112410). Ksiega widzi to jako "inne z niczego" i taka sama ujemna reszte: 430 829 (d109787), 54 751 (d109795), 133 326 (d109798). Suma swiata tych dob sie nie zmienila (-378), wiec placi kasa miasta - brak tylko nazwanej pozycji. Przy okazji: 11 sztuk o wartosci 242 000 sprzedane za 430 829 = 178% wartosci (linia "Skup sprzetu (doba)") - do sprawdzenia przez audyt cen.
2. **Kasy miast: reszta niezmierzona +28 632 zl/d, sr. 28 dob +28 012 do +31 987 ("PONAD" progu 10 000)** - linia "Przeplywy osad (przyczyny)". Bilans swiata sie zamyka, wiec to przelew bez licznika, nie kreacja; zrodla nie ustalilem.
3. **Dochod za tier +480/d** i **warsztaty BK przy pustej kasie +793/d** - jedyna stala kreacja poza mennica (razem ok. 465 tys./rok).
4. **Prowizja 502/d, dylematy BK 390/d, kopalnie BK 39/d** - w nicosc (ok. 340 tys./rok).
5. **Kiesy garnizonow 1 470 469** - stoja (zmiana +0 do +400/d); pieniadz martwy.
6. **Trzy rozne stawki tej samej daniny od skarbca ludu** (1.00 / 3.00 / 3.70%/d w trzech liniach, faktycznie 2.09%/d); mnoznik straznika stoi na suficie 3.7 "bezpiecznik udzialu ludu 9%", choc lud ma 0.84 x poziomu normalnego. Ktora liczba rzadzi poborem - nie ustalilem w kodzie.
7. **Podatek od nadwyzki wsi (obieg2) = same zera** (linia "Podatek od nadwyzki wsi": wsi z potrzeba 0, kiesy 0/0) - zastapiony przez DS wsi; linia jest martwa.

## 8. Uwagi o dynamice (z liczb, nie z kodu)
- Gracz +4.05 mln w 61 dob (0.88 -> 4.93 mln), gdy wszyscy inni razem -3.56 mln. Zrodla z linii "Prognoza zlota gracza": rata firmy jednego miasta 76 364 zl w dobie raty, danina od ludu 7 317/d, skrzynie wsi 3 217/d, sprzedaz sprzetu 619 tys. w 3 dobach. Gracz nie ma budzetu 166 ani ciec "wedlug potrzeby".
- Korony: 191 064 zl/d rent zatrzymane w skarbcach + zwrot mniejszy o 73 441. The Vale: skarbiec 11.07 mln przy kiesach wszystkich 33 rodow 3.39 mln, 29 rodow na minusie, renty 5 809/d.
- Rody ponad 2R: 62 rody trzymaja 38.1 mln; sprzet niewydany 125 326 zl u 261 rodow, z tego 105 519 "bez towaru" na polkach.
- Kasy miast: 46 z 97 ponizej zapasu kupcow, 27 ponizej 1/4, 40 miast >= 7 dob z rzedu; zaleglosci firm 237 381 u 17 miast (Rhyos 52 raty).
- Lud: stac na pelne jedzenie W/R/P 78.9 / 69.6 / 81.4%; 35 osad na chlebie, glownie Wolne Miasta (Braavos, Qarth, Volantis), gdzie korony i panowie nie maja nadwyzki.
- Wsie chca wydac 436 038 zl/d nadwyzki, wydaja 55 760 (towar 31 842, roboty 23 918), 314 101 "niewydane" z braku towaru.

## 9. Czego nie udalo sie ustalic
- Zrodla reszty kas miast ok. +30 tys./d (pkt 7.2) i roznicy zoldu karawan lordow 710/d.
- Ktora stawka daniny od skarbca ludu jest faktycznie stosowana w kodzie (pkt 7.6) - czytalem naglowki i linie logu, nie cala TownFolk.Hoard / DsAkum / Guard.
- Ktory rod dostaje "dochod za tier" 480/d.
- Stan skarbcow zamkow BetterEconomy (poza ksiega) - ruch 0, salda nie widac w logu.
- Wartosci z `Armoury.json` Jeffa: stawki bralem z linii logu (czynne) i z domyslnych w Settings.cs; rozjazd widac np. przy regale (log 0.250, Settings 0.125).
- Dekompilacji gry, BK i ROT nie otwieralem: twierdzenia o cudzym kodzie pochodza z naglowkow naszych plikow i z pomiaru w logu. Kod czytalem na poziomie naglowkow klas i sygnatur metod (numery linii = poczatek metody), nie linia po linii.
- Okno pomiaru to 61 dob jednej kampanii z wojnami 7 krolestw; nie wiem, jak bilans wyglada w dlugim pokoju ani po upadku krolestwa (pozycje "skarbce upadlych krolestw", "kiesy partii, ktore zniknely" byly 0).
