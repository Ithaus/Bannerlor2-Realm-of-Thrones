# Audyt ekonomii krolestw (2026-10-04, po wpisach CHANGELOG 39-44)

Tylko odczyt - kodu nie ruszalem. Pytanie Jeffa: czy gospodarka krolestw dziala dobrze wobec zasady
"zamknieta gospodarka, 1 denar = 1 pens, ceny i dochody jak w Anglii ok. 1300".

**Zrodla danych (fakty z logu):**
- `Modules/Armoury/Armoury-2026-10-04_13-00-48.log` - nowa kampania, dni 108836-108840 (5 dni).
  To jest gra PRZED wpisem 44 (wpis 44 powstal na podstawie tego logu) - wplyw wpisu 44 oceniam z kodu.
- `CrashScribe/session-2026-10-04_13-00-46.log` (linie `EKONOMIA`) i `economy-2026-10-04_13-02-16.csv`
  (315 rodow x 5 dni).
- Dekompilacje BK / BEE / vanilla / ROT z 20.09 (scratchpad), rejestracja modeli sprawdzona `ilspycmd` na
  zainstalowanych DLL.

**Kalendarz:** log mowi `Kalendarz: rok ma 364 dni` (`Settings.WeeksPerSeason = 13`). Wszystkie roczne
przeliczenia nizej sa na rok 364 dni (nie 168 i nie 84, jak pisze CLAUDE.md - CLAUDE.md jest tu nieaktualny).

**Przeliczniki:** 240 d = 1 L. Robotnik 1.5 d/dzien, rzemieslnik 3 d, baron 200-500 L/rok
(48-120 tys. d = 130-330 d/dzien), hrabia 1000-3000 L (240-720 tys. d = 660-2000 d/dzien; Lancaster 11 000 L),
korona ok. 30 000 L/rok (7.2 mln d = ~20 tys. d/dzien).

---

## 0. Werdykt w pieciu zdaniach

1. **Skala per glowa jest blisko historii, skala per rod - nie.** Dzien 5: dochod wszystkich rodow (model gry
   0.72 mln + renty 0.51 mln) = 1.23 mln d/dzien = ~3.9% PKB swiata 52.6 mln ludzi (Anglia: korona + magnaci
   ~2-3% - SZACUNEK). Ale 315 rodow dzieli dochod krainy wielkosci kilku Anglii, wiec mediana rodu
   (~3.5 tys. d/dzien z rentami = ~5 tys. L/rok) to **hrabia, nie baron**; to pasuje do wielkich domow ROT.
2. **Krol jest za slaby wobec wasali.** Krol mediana 5.1 tys. d/dzien z modelu (~7.8 tys. L/rok) =
   ~4x mediana rodu i ~1x najbogatszy rod (Frey 5.6 tys., Antaryon). Historycznie korona = ~3x najbogatszy hrabia
   i ~100x baron. Wojenna "pietnastka" (nasze `CrownDues` 10%) daje 26-48 tys./dzien na 29 krolestw (~1.3 tys. na krola).
   Krolow trzyma przy zyciu jednorazowy skarbiec 2 mln na krolestwo (x29 = 58 mln).
3. **Dochody jeszcze sie nie ustalily - spadaja co dzien.** Renty 1.44 -> 1.32 -> 0.97 -> 0.51 mln/dzien,
   "Village Demesnes" BK 1.25 -> 0.19 mln (start gry nalicza wsiom zapas `TradeTaxAccumulated`, ktory w 5 dni sie
   wyczerpal), a zold rosnie 74 -> 188 tys./dzien (wojsko 15 -> 42 tys. ludzi). Rzeczywisty przyrost zlota rodow
   (suma) 1.86 -> 1.17 -> 0.77 -> 0.25 mln/dzien. Za 1-2 tygodnie bedzie ponizej zera dla wiekszosci rodow - to
   jest dobre (wojna kosztuje), ale potrzebny pomiar, gdzie sie zatrzyma.
4. **Renty placone w 9-25% to objaw zlej kalibracji, nie biedy osad.** Nalezne 5.7 mln/dzien = 40 d na glowe
   rocznie = ~19% PKB na glowe (cala renta + dziesiecina + korona Anglii), a osady gry maja w kasach razem kilka
   milionow (cel kas 97 miast = 6.55 mln). Renta dzis = "50% kiesy wsi i nadwyzka miasta ponad cel" - kwota
   przypadkowa, wyczerpujaca kiesy wsi.
5. **Zamknietej gospodarki nadal nie ma**: najwieksze zrodla z niczego to podatek ludnosci BK od miast
   ("Walled Demesnes" 0.48 mln/dzien), dosypka kasy miast vanilli (niemierzona, rzedu tego, co miasta wydaja),
   pompa notabli, BEE (rente posiadlosci, wyplaty skarbca miasta, cla traktatow - niemierzone); najwieksze ujscie
   w nicosc to zold partii i garnizonow (0.33 mln/dzien) oraz ukryty "regulator bogactwa" BEE.

---

## 1. (a) Zloto i dochody - liczby

### 1.1 Rody (CSV, mediany; dzien 1 = start, dzien 5 = 108840)

| Kto | Ile | Zloto rodziny d1 / d5 | Dochod modelu/dzien d1 / d5 | Wydatki modelu d5 | Zold partii d5 | Faktyczna zmiana d5 (mediana / suma) |
|---|---|---|---|---|---|---|
| Krol | 29 | 376 tys. / 391 tys. (max 553 tys.) | 8 968 / 5 134 | -2 680 | 1 030 (235 ludzi) | +4 535 / +116 tys. |
| Rod krolestwa | 273-275 | 210 tys. / 225 tys. (p10 39 tys., p90 318 tys.) | 4 208 / 1 256 (p90 5 267) | -944 | 348 (87 ludzi) | -88 / +255 tys. |
| Rod mniejszy | 10-12 | 270 tys. / 269 tys. | 320 / 550 | -498 | 498 (163 ludzi) | -534 / -10 tys. |
| Gracz | 1 | 1 000 | 0 | 0 | 0 | 0 |
| Skarbiec krolestwa | 29 | 2.00 mln / 2.02 mln kazde (razem ~58.5 mln) | - | - | - | +~0.2 tys./dzien (powinnosci) |

Skladniki dochodu modelu (suma, dzien 1 -> dzien 5):
- **Village Demesnes** (BK, z licznika `TradeTaxAccumulated` wsi): krolowie 214 -> 38 tys., rody 1 247 -> 185 tys.
- **Walled Demesnes** (BK, podatek klas ludnosci miasta + cla): krolowie 101 -> 122 tys., rody 282 -> 359 tys.
- Wydatek **Garrison and Party Expense**: krolowie -65 -> -70 tys., rody -142 -> -250 tys., mniejsze 0 -> -5 tys.
- Council wages / Councillor role i Kingdom Budget Expense - przeplywy miedzy bohaterami i do skarbca (BK
  `BKClanFinanceModel.cs:225-265`, `EconomyPatches.cs:211-219`), nie zrodla.
- **Renty PopulationLaw** (poza modelem, z kas osad): 1 437 644 -> 513 731 d/dzien dla wszystkich panow
  (~2.3 tys./dzien na rod z lennem, 218 rodow z lennem; 86 rodow krolestwa jest bez lenna).
- **Powinnosci wobec korony**: 44.7 -> 26.1 tys./dzien od ~190 rodow.

Swiat: 97 miast, 130 zamkow, 569 wsi w rekach rodow.

### 1.2 Porownanie z historia (rok = 364 dni)

| Kto | Gra (dzien 5) | Rocznie | Historia | Ocena |
|---|---|---|---|---|
| Krol (bez skarbca) | model 5.1 tys. + renta ~2.4 tys. = ~7.5 tys./dzien | ~2.7 mln d = ~11 tys. L | korona 30 tys. L | 1/3 korony Anglii, ale krolestwo ROT ma srednio 1.8 mln ludzi (0.4 Anglii) - **per glowa OK**; **wobec wasali za slaby** |
| Skarbiec krolestwa | 2 mln | jednorazowo | skarbiec Edwarda I zwykle pusty, wojny na kredyt (Riccardi) | 8 300 L na krolestwo - takze dla malych miast-panstw Essos; za duzo dla malych |
| Rod krolestwa z lennem | model ~1.3 tys. + renta ~2.3 tys. = ~3.5 tys./dzien | ~1.3 mln d = ~5.3 tys. L | baron 200-500 L, hrabia 1-3 tys. L (Lancaster 11 tys.) | **hrabia** (10-25x barona). Dla wielkich domow Westeros (Bolton, Frey, Tyrell) - rozsadne |
| Rod bez lenna (86) | ~0 dochodu, zloto p10 39 tys. | - | rycerz 20-40 L | zyje ze startu BK; pojdzie do Banku - realistyczne |
| Rod mniejszy | 550/dzien (karawany, konwoje) | ~830 L | kompania najemna | OK |
| Miasto (kasa) | cel 10 000 + 12 x dobrobyt; mediana dobrobytu ROT 4 800 -> 67.6 tys. (282 L); razem 6.55 mln | - | dochod miasta (oplaty, myto) dziesiatki-setki L/rok | rzad wielkosci OK; kasa nie jest "dochodem", tylko buforem z dosypka |
| Wies (kasa) | brak pomiaru; BK limit rynku 10 000 + 3 x hearth (mediana hearth 350 -> ~11 tys.) | - | - | renta nalezna z jednej wsi Reach = 454 os./hearth x 350 x 40 / 364 = **~17.5 tys. d dziennie** - wiecej niz cala kasa wsi |
| Notabl (2731) | kiesa trzymana przez gre w pasie 4 500-10 500 (19-44 L) | - | bogaty chlop 2-5 L ruchomosci, kupiec miejski 50-500 L | wiejscy notable x5-10 za bogaci, miejscy OK |
| Robotnik | praca jenca: wies 1 + hearth/1000 = ~1.35 d, miasto 1.5 + dobrobyt/5000 = ~2.5 d | - | 1.5-2 d | **OK** |
| Zolnierz | t1 2 d ... t6 21 d; faktyczna srednia z CSV 4.0-4.9 d/czlowieka (Bolton 881 ludzi 3 537 d, Tarth 591 ludzi 2 920 d) | - | piechur 2 d, lucznik 3, zbrojny 12, rycerz 24 | **OK** |

### 1.3 Ile lat dochodu lordowie trzymaja w gotowce

- Start: zloto rodow razem 62.5 mln, z tego **BK `GiveClansResources` (Tier x 25 000, `BKCampaignStartBehavior.cs:240-251`) = 26.0 mln (42%)**,
  reszta z danych startowych ROT/vanilli. Do tego skarbce 29 x 2 mln = 58 mln i notable 2731 x 10 000 (start, `NotablesCampaignBehavior.cs:47`) = ~27 mln.
- Zloto / dochod modelu x 364 (mediana): dzien 1 - krol 0.11 roku, rod 0.09 roku (dochod zawyzony zapasem startowym wsi);
  **dzien 5 - krol 0.22 roku, rod 0.47 roku, rod mniejszy 1.4 roku**. Z rentami (dochod ~2x) - rod ~0.2-0.25 roku, krol ~0.15 roku
  (+ skarbiec 2 mln = ~0.7 roku dochodu krola).
- Zloto / dzisiejszy zold (mediana): krol 382 dni, rod 436 dni, rod mniejszy 450 dni.
- Wniosek: **stosunek gotowki do dochodu jest historyczny** (magnaci trzymali w monecie ulamek rocznego dochodu);
  w liczbach bezwzglednych rod ma ~940 L - "2-5 lat barona", ale ~1/5 roku hrabiego, za ktorego tu robi.
- Uwaga: najbogatsi stoja na 520-553 tys. - prawdopodobnie sufit BEE `LordWealthRealism` (sekcja 3.3), nie decyzja gospodarki.

---

## 2. (b) Czy lorda stac na wojsko i wojne

**Koszty wojny w grze (po wpisach 42-43):**
- Zold: t1 2, t2 ~3, t3 ~5, t4 ~8, t5 ~13, t6 21 d/dzien. Armia 300 ludzi w typowym skladzie (30/25/20/15/7/3%)
  = ~5.1 d/czlowieka = **~1 530 d/dzien = 557 tys. d/rok (2 300 L)**.
- Werbunek: 10 dni zoldu (`RecruitCost.cs`, najemnicy x2) - 300 ludzi = ~15 tys. d. Dane: AI placi notablom 27-60 tys./dzien,
  najemnikom (do miast) 88-142 tys./dzien.
- Sprzet: zakupy AI 1 698 szt. za 101 tys. pierwszego dnia (60 szt. za 2-3.5 tys. d - ~40 d/sztuka), potem 29-133 tys./dzien
  na swiat. Komplet t2-t3 = 200-500 d (historycznie przeszywanica + helm + bron 1-2 L = 240-480 d - **zgodne**).
- **Kon to glowny koszt jazdy**: charger 10 973 d (46 L), hunter 5 980, kon wierzchowy t4 1 114, juczny 99
  (historia: destrier 40-100 L, courser 10-20 L, rouncey 1-5 L - **zgodne**). 50 jezdnych t4+ = 50-300 tys. d = cala kiesa rodu.

**Wyplacalnosc:**
- Mediana rodu z lennem: dochod ~3.5 tys./dzien, wydatki stale ~0.9 tys. (garnizon, rada, budzet) -> ~2.5 tys./dzien
  na wojsko = **stale ~500 ludzi** albo 300 ludzi + odnawianie sprzetu; zapas 225 tys. = 150 dni 300 ludzi bez dochodu.
  Historycznie baron (200 d/dzien) utrzymywal stale ~20-30 ludzi, hrabia setki - **ROT-owy rod jest na poziomie hrabiego, armie
  50-900 ludzi na rod (CSV) sa z tym spojne.**
- Krol: ~7.5 tys./dzien + skarbiec 2 mln = 300-450 ludzi przez kilka lat. Historycznie krol prowadzil wojne z podatkow nadzwyczajnych
  (pietnastka/dziesiecina 35-50 tys. L) i kredytu - u nas ten kanal jest za slaby (10% od wasali = ~1.3 tys./dzien na krola).
- Rody bez lenna (86, zloto p10 39 tys.): 300 ludzi = 26 dni. Pojda do Banku Zelaznego (dobrze - tak mialo byc po wylaczeniu
  zapomogi ROT), ale w 5 dniach `IronBank:` = 0 pozyczek - za wczesnie na ocene.
- **Trend**: wojsko x2.8 w 4 dni, zold x2.5, dochody /2.7. Rzeczywista zmiana sumy rodow spada ~0.4-0.5 mln dziennie. Jesli tak
  zostanie, za ~1-2 tyg. rody krolestwa beda na minusie - w wojnie to historyczne (dlug, Bank), w pokoju nie powinno.

---

## 3. (c) Skad zloto wchodzi i dokad znika (stan kodu 04.10)

Skala: z CSV/logu tam, gdzie jest; "?" = niemierzone.

### 3.1 Zrodla z niczego (aktywne)

| # | Zrodlo | Gdzie | Skala (dzien 5) |
|---|---|---|---|
| Z1 | **Podatek ludnosci BK od miast** ("Walled Demesnes": klasy ludnosci + cla) - `GiveGoldAction(null, glowa)` w rozliczeniu rodu | `bk/BannerKings.Models.Vanilla/BKTaxModel.cs:164-221` (`CalculateTownTax`), `bk/BannerKings.Patches/EconomyPatches.cs:420-471` (`VillageIncomePrefix`), `tw/ClanVariablesCampaignBehavior.cs:413-414` | **0.48 mln/dzien** (najwieksze mierzone) |
| Z2 | **Dosypka kasy miasta** do celu 10 000 + 12 x dobrobyt (25% luki dziennie). Aktywny model ekonomii osady to **vanilla** - BK nie rejestruje swojego, gdy widzi BEE (`BannerKings.Main` l.205), BEE nie rejestruje swojego, gdy widzi BK (`BetterEconomySubModule` l.108; bee_log: "defers ... economy ... to BannerKings") - sprawdzone na zainstalowanych DLL | `tw/DefaultSettlementEconomyModel.cs:75-79`, `tw/ItemConsumptionBehavior.cs:73-77` | ? - w rownowadze = tyle, ile miasta netto wydaja (warsztaty Armoury 0.24-0.59 mln/dzien przed wpisem 44, wiesniacy, renty z nadwyzki) |
| Z3 | **BK placi miastu za konsumpcje ludnosci** | `bk/EconomyPatches.cs:609-657` (l.649-651) | ? (duza; po wpisie 44 mniejsza dla broni) |
| Z4 | **Notable**: 10 000 przy tworzeniu; dosypka gdy < 4 500 (za wplyw); dochod z aktywow | `tw/NotablesCampaignBehavior.cs:47`, `tw/NotablePowerManagementBehavior.cs:50-60`, `tw/ClanVariablesCampaignBehavior.cs:477-487` | ? |
| Z5 | **BEE**: renta posiadlosci (`PayEstateOwner`), wyplata nadwyzki wirtualnego skarbca miasta co 7 dni (35%, w wojnie 20%), cla traktatow handlowych - wszystko `GiveGoldAction(null, ...)` | `be/FeudalEconomyCampaignBehavior.cs:955-972`, `be/TownEconomyCampaignBehavior.cs:1970-1995`, `be/TradeAgreementCampaignBehavior.cs:405-421` | ? (nie ma w CSV - CSV liczy tylko model finansow) |
| Z6 | Zloto startowe: BK Tier x 25 000 (26 mln), skarbce 2 mln x 29 (58 mln), notable 27 mln | jw. | jednorazowo ~111 mln |
| Z7 | Jency: AI i posrednik placa z niczego; kurier okupu dosypuje placacemu do ceny + 1000 | `tw/SellPrisonersAction.cs:70-85`, `tw/RansomOfferCampaignBehavior.cs:176` | ? |
| Z8 | Zdobycie miasta, najazd na wies, rebelia (+50 000), zdarzenia, rozmowy, zadania | `tw/SiegeAftermathCampaignBehavior.cs:164-168`, `tw/VillageHostileActionCampaignBehavior.cs:556`, `tw/RebellionsCampaignBehavior.cs:310` | okazjonalnie |
| Z9 | Kryjowki: +25% wartosci lupu bandy do bandy i kryjowki przy wejsciu | `tw/BanditSpawnCampaignBehavior.cs:149-179` | mala |
| Z10 | ROT: inwazje Aegona/Daenerys 1 mln do osady; Enlistment (lup -> przedmioty) | `rot/AegonInvasionEvent.cs:100`, `rot/DanyInvasionEvent.cs:100` | zdarzenia fabuly |
| Z11 | Bank Zelazny: pozyczki z kapitalu 5 mln spoza swiata | `Armoury/src/IronBank.cs:170` | 0 w 5 dniach |
| Z12 | Nasze drobne: praca jenca (`RealisticCaptivity/src/Work.cs:289,325,341`), sprzedaz domu + skrzynia (`Homes.cs:240,284`), GrandTourney zwroty/wygrane (`GrandTourney/src/TourneyBehavior.cs:435,455,492,553`), `Uniques.cs:146` | | mala, tylko gracz |
| Z13 | Przedmioty z niczego: lup z szablonu, DTE dopelnia sloty, najemnicy z karczmy odrastaja ze sprzetem | vanilla/DTE | srednia-duza (przedmioty) |

Wylaczone (dzialaja wedle logu): zapomoga ROT Tier x 5000 (`KingdomTreasury: zapomoga ROT ... przechwycona`), dosypki skarbca
1000/100-400 tys. (`podmienionych stalych 4`), dzienne zloto band, darmowy komplet DTE nowej partii AI (185-1515/dzien zablokowanych),
rzemieslnicy bez wsadu (560-2576 cykli/dzien zablokowanych).

### 3.2 Przeplywy (nie zrodla) - dla porzadku

Renty (osada -> pan, 0.51 mln), powinnosci (rod -> skarbiec, 26-48 tys.), LevyGold (lord -> notabl 27-60 tys., lord -> miasto 88-142 tys.),
ZakupyAI (lord -> miasto 29-133 tys.), PodazPopyt (miasto -> miasto 1.1-1.6 mln), Warsztaty (miasto -> warsztat), VolunteerKit (notabl -> miasto),
Stables (lord -> wies/miasto), cla miasta (kasa miasta -> `TradeTaxAccumulated` -> pan), "Village Demesnes" (miasto -> wiesniak -> licznik
70% -> pan; wiesniak traci 15% w nicosc), Council wages, Kingdom Budget (0.1% nadwyzki ponad 100 tys. do skarbca).

### 3.3 Ujscia w nicosc

| # | Ujscie | Gdzie | Skala |
|---|---|---|---|
| U1 | **Zold partii i garnizonow** | BK `EconomyPatches.cs:300-396` ("Garrison and Party Expense") | **0.33 mln/dzien** (partie 0.19 + garnizony ~0.14), rosnie z wojskiem |
| U2 | **Kasowanie nadwyzki kasy miasta** (25%/dzien ponad cel) | `tw/DefaultSettlementEconomyModel.cs:75-79` | ? |
| U3 | **BEE "LordWealthRealism"** - domyslnie WLACZONE (brak `better_economy_user.cfg`): co dzien zabiera 10% nadwyzki ponad cel (cel krola ~600 tys., rodu ~300 tys.), a "niewyjasniony przyrost" ponad pulap zabiera w 68-90%. BEE nie zna naszych rent, LevyGold ani zwrotow - dla niego to "untracked windfall" | `be/WealthAuditCampaignBehavior.cs:160-215, 263-330`, `be/BetterEconomy.Config/RuntimeSettings.cs:13`, sufit dzienny `LordWealthDailyCorrectionCap` 250 000 | ? - dotyka najbogatszych (sufit ~520-550 tys. w CSV) |
| U4 | Notabl > 10 500 traci nadwyzke (za wplyw) | `tw/NotablePowerManagementBehavior.cs:50-55` | ? |
| U5 | Wiesniak: 15% utargu znika (BK oddaje wsi polowe reszty po podatku) | `bk/EconomyPatches.cs:1094-1099` | ? |
| U6 | Werbunek gracza, najemnicy gracza, lapowki, budowy BK, decyzje BK, inwestycje BEE gracza | vanilla/BK/BEE, `RealisticCaptivity/src/Patches.cs:96`, `Homes.cs:184` | mala |
| U7 | Bank: odsetki i zajecia do `_capital` spoza swiata | `IronBank.cs:243,261` | 0 w 5 dniach |
| U8 | Przedmioty w nicosc: konsumpcja miast, VolunteerKit (`VolunteerKit.cs:160`), zuzycie amunicji | | |

**Bilans rodow (dzien 5, szacunek):** wplywy 1.23 mln (Z1 0.48 + wsie 0.22 + cla w Walled + renty 0.51) - zold/garnizony 0.33
- werbunek ~0.16 - sprzet/konie ~0.05-0.15 - powinnosci (przeplyw) = **+0.36 mln/dzien netto wedle CSV** (spada z dnia na dzien).
Bez Z1 rody bylyby juz na zero.

---

## 4. (d) Renty placone w 9-25% - czy to problem?

**Tak, ale nie dlatego, ze osady sa biedne - dlatego, ze "nalezna renta" nie pochodzi z zadnego przeplywu w grze.**

1. **Kalibracja:** `PopulationRentPerHead = 40` (`Settings.cs:416`) x 52.6 mln / 364 = 5.78 mln d/dzien = 2.1 mld d/rok = **8.7 mln L/rok**.
   To jest cala renta feudalna, dziesiecina i korona razem (~19% PKB na glowe 210-240 d) - a nasze 315 rodow to tylko wielka szlachta
   i krolowie. Historycznie korona + magnaci (bez rycerstwa i Kosciola) braly w Anglii ~105 tys. L z 4.7 mln ludzi = **~5 d na glowe**
   (SZACUNEK z Dyera). Placone dzis 0.51 mln/dzien = **3.5 d na glowe** + model gry ~5 d = ~8.5 d/glowe - czyli **realnie
   zaplacona kwota jest juz blisko historii**, a "nalezna" jest 5-8x za duza.
2. **Mechanika:** wies placi `min(nalezna, 50% kiesy)` (`PopulationLaw.cs:168-170`), miasto tylko nadwyzke ponad cel. Nalezna z jednej
   wsi Reach (~17.5 tys./dzien) przekracza cala kiese wsi, wiec w praktyce **renta = 50% kiesy wsi dziennie** - zalezy od plynnosci wsi,
   nie od ludnosci. Stad spadek 1.44 -> 0.51 mln: pierwszego dnia wsie mialy zapas startowy, potem renta zjada to, co wiesniak przyniosl
   (15% jego utargu, sekcja 3.3 U5).
3. **Skutki uboczne:** wsie bez kasy (BEE/BK projekty wsi, kupno koni hodowcy przez `Stables` idzie DO wsi - ok), miasta z nadwyzka
   ponad cel i tak traca 25%/dzien (vanilla), wiec renta z miast lapie glownie dni szczytu handlu. Raport `zaplacone/nalezne` w logu
   jest mylacy - pokazuje "9%", choc zaplacona kwota jest sensowna.
4. **Podwojne liczenie:** pan dostaje z tego samego miasta podatek BK od klas ludnosci (Z1, z niczego) **i** rente z nadwyzki kasy;
   z tej samej wsi - "Village Demesnes" (70% utargu wiesniakow przez licznik) **i** rente z kiesy wsi (z pozostalych 15%).
5. **Wpis 44 (niesprawdzony w grze) - skutki dla rent:** popyt miast na przeliczone kategorie podzielony przez stosunek cen
   (`HistoricalPrices.cs:196-204`, wpiecie l.318-322) -> miasta kupuja tyle samo SZTUK broni/zbroi, wydajac ~40x mniej zlota;
   sprzedaz warsztatow do miast spadnie z 0.24-0.59 mln/dzien do dziesiatek tysiecy. Wiecej nadwyzki w kasach miast = **renta z miast
   wzrosnie**, dosypka z niczego (Z2) spadnie, zaplata BK za konsumpcje broni (Z3) spadnie. Z drugiej strony skora/len/skory/lnianka
   taniej -> wsie zarabiaja mniej na tych towarach -> mniejsze "Village Demesnes" i mniejsza renta z wsi; notable z warsztatow
   uzbrojenia zarobia mniej -> czesciej dosypka < 4 500 (Z4). Zywnosc i reszta towarow zostaje w skali gry (1-10x historii), wiec
   pieniadz wiesniakow i miast dalej plynie glownie przez zywnosc. Do sprawdzenia po 7-14 dniach nowej gry: `Ludnosc: renty zaplacone`
   (powinno przestac spadac), `Warsztaty:` (sprzedaz w tysiacach, nie setkach tysiecy), EKONOMIA "faktycznie/dzien".

---

## 5. (e) Zalecenia (kolejnosc = priorytet; jedna zmiana = jeden DLL = jeden test)

1. **Pomiar pieniadza swiata, zanim cokolwiek zamkniemy** (bez wplywu na gre).
   `CrashScribe/src/EconomyAudit.cs` (linia `EKONOMIA`, ok. l.128): dopisac sumy `Town.Gold` (97 miast), `Village.Gold` (569 wsi),
   `Hero.Gold` notabli (2731), `Kingdom.KingdomBudgetWallet`, kasy band/kryjowek, `IronBank._capital`; w Armoury liczniki dzienne:
   postfix na `DefaultSettlementEconomyModel.GetTownGoldChange` (suma dosypek i kasowan Z2/U2), prefix/postfix na
   `NotablePowerManagementBehavior.BalanceGoldAndPowerOfNotable` (Z4/U4), a w `PopulationLaw.Daily` (`Armoury/src/PopulationLaw.cs:178-184`)
   podzial rent na miasta / wsie. W BEE: plik `Modules/BetterEconomy/better_economy_user.cfg` z `WealthAuditLogging=1`
   (zobaczymy Z5 i U3). Bez tego kazda nastepna zmiana jest zgadywaniem.
2. **Nowa gra 14 dni z wpisem 44 i odczyt** (decyzja Jeffa, zero kodu): czy renty i "Village Demesnes" sie ustalaja, gdzie staje
   "faktycznie/dzien" rodow przy rosnacym wojsku, czy `IronBank:` zaczyna pozyczac rodom bez lenna.
3. **Renta z przeplywu, nie z "naleznej od glowy".** `Armoury/src/Settings.cs:416` `PopulationRentPerHead` 40 -> ~5-6 (udzial
   wielkiej szlachty i korony, ~5 d/glowe) i `:418` `PopulationRentMaxShare` 0.5 -> ~0.2, zeby renta nie zjadala kiesy wsi w jeden dzien;
   wtedy "nalezne" ~0.7-0.9 mln/dzien i wskaznik zaplacone/nalezne zacznie cos znaczyc. (Wariant docelowy: renta wsi = udzial w
   dziennym przyroscie kiesy wsi zamiast udzialu w calej kiesie - `PopulationLaw.cs:160-170`.)
4. **Jeden kurek z niczego zamiast trzech: podatek BK od ludnosci miasta zastapiony renta.** Postfix na
   `BannerKings.Models.Vanilla.BKTaxModel.CalculateTownTax` (`bk/BKTaxModel.cs:164-221`) zerujacy skladniki klas ludnosci, gdy
   `PopulationRentEnabled`; w zamian w `PopulationLaw.cs:168` miasto placi rente z kasy az do np. 50% celu (nie tylko z nadwyzki),
   a dosypke vanilli (Z2) uznajemy jawnie za jedyny "kurek produkcji" swiata - mierzony (zalecenie 1). Efekt: ~0.48 mln/dzien z niczego
   przechodzi przez kasy miast; biedne, oblegane, spladrowane miasto daje panu mniej. Ryzyko: przy zlej proporcji rody straca
   ~40% dochodu - dlatego dopiero po pomiarze.
5. **BEE: wylaczyc wlasne kurki i ujscia, ktorych nie widzimy** (bez kodu, ustawienia). `Modules/BetterEconomy/ModuleData/better_economy_settings.xml`:
   `TownTreasurySurplusPayoutFraction` 0.35 -> 0, `TownTreasuryWarPayoutFraction` 0.20 -> 0, `TradeAgreementCustomsRate` 0.06 -> 0
   (cla traktatow i wyplaty wirtualnego skarbca z niczego); `better_economy_user.cfg`: `LordWealthRealism=0` - BEE kasuje w nicosc
   10%/dzien nadwyzki i 68-90% "niewyjasnionych" wplywow, a naszych rent i LevyGold nie rozpoznaje (`be/WealthAuditCampaignBehavior.cs:160-215`).
   Rente posiadlosci BEE (`FeudalEconomyCampaignBehavior.PayEstateOwner`, l.955-972) - po pomiarze: prefix przekierowujacy wyplate z kasy miasta.
6. **Korona silniejsza wobec wasali - cla do skarbca, nie do pana miasta.** `Armoury/src/KingdomTreasury.cs:45-76` (`Daily`): obok
   powinnosci przekazac do `KingdomBudgetWallet` czesc cla miast (`Town.TradeTaxAccumulated`, historycznie myto/custom na welne ~10 tys. L
   = 1/3 dochodu korony) - przed BK `VillageIncomePrefix` (`bk/EconomyPatches.cs:424-433`, cla = TradeTaxAccumulated/5); ewentualnie
   `Settings.cs:469` `CrownDuesPeacePercent` 2 -> 5. Cel: krol ~3x najbogatszy wasal, wojna krola z podatku, nie ze startowych 2 mln.
7. **Zold do kasy osady, nie w nicosc** (U1, 0.33 mln/dzien i rosnie). Postfix na `ClanVariablesCampaignBehavior.DailyTickClan`
   (`tw/ClanVariablesCampaignBehavior.cs:413-414`): po rozliczeniu rodu suma zoldu kazdej partii (`PartyWageModel.GetTotalWage`) do
   `CurrentSettlement ?? LastVisitedSettlement ?? HomeSettlement` (`SettlementComponent.ChangeGold`), zold garnizonu do kasy jego miasta/zamku.
   Historycznie zolnierz wydawal zold na targu. Uwaga: to dosypie miastom ~0.3 mln/dzien - robic RAZEM z ograniczeniem dosypki Z2
   (zalecenie 4), inaczej zwiekszy nadwyzki kasowane przez vanille.
8. **Kiesa notabla z kasy osady** (stare W4 z AUDYT-PELNY-EKONOMIA). Prefix na
   `NotablePowerManagementBehavior.BalanceGoldAndPowerOfNotable` (`tw/NotablePowerManagementBehavior.cs:50-60`): nadwyzka > 10 500
   do `HomeSettlement` zamiast w nicosc, dosypka < 4 500 tylko z kasy osady (gdy ma). Po wpisie 44 ma wieksze znaczenie (notable
   z warsztatow broni zarobia mniej).

Czego NIE zalecam teraz: ruszania startowego zlota BK (26 mln) i skarbcow 2 mln - to zapas, ktory i tak zjada wojna; stosunek
gotowki do dochodu jest historyczny. Ewentualnie pozniej skarbiec proporcjonalny do ludnosci krolestwa (male miasta-panstwa Essos maja
tyle co Polnoc).

---

## 6. Czego nie wiem (brak dowodu w logu)

- Ile dosypuje vanilla miastom (Z2) i kasuje (U2), ile notablom (Z4/U4), ile BEE wyplaca i zabiera (Z5/U3) - zero linii w logach.
- Ile kosztuje swiat zakup koni do awansow (Stables pokazuje pojedyncze wpisy).
- Czy wpis 44 sie wpial (`HistoricalPrices: popyt miast przeliczony ...`) - brak logu po wpisie 44.
- Dekompilacje BK/BEE/vanilli sa z 20.09; rejestracje modeli ekonomii osady sprawdzilem na zainstalowanych DLL, reszte nie.
