# AUDYT 01 - kto zarabia na pracy i dokad idzie dochod (ludnosc jako posiadacz pieniedzy)

Data: 09.10.2026 (noc z 08 na 09.10). Tylko odczyt: zadnego kodu, zapisu gry ani ustawien nie zmieniano, gry nie uruchamiano.
Kod = wersja w grze (Armoury c01a54ba = commit 2e235ea, kopia `scratchpad\audyt\repo`). Dekompilacje: gra 1.4.8 (`ore-supply\cs`), BK (`ore-supply\bk`, `audyt-pieniadz\bkall`).
Pomiary: autotest ROCZNY `Modules\Armoury\Armoury-2026-10-08_08-18-38.log` (364 doby, TOWARY 3; ostatnia doba = 109199/109200), poczatek tego samego logu (doba 108836).
Ustawienia: `Armoury.json` Jeffa NIE zawiera zadnego klucza renty, ludnosci, plac ani rzemiosla (sprawdzone grep) - dzialaja domyslne z `Settings.cs`.
Baza, ktorej ten raport nie powtarza, tylko na niej buduje: `docs/PROJEKT-EKONOMIA-OBIEG-2026-10-08.md` (Z1-Z9, dwor 6.1, zawor 5.3, paczki 162-168),
`docs/EKONOMIA-FUNDAMENT-2026-10-05.md` rozdz. 2-3 (historia Campbella H1-H9, tabela 3.2), `docs/CENY-HISTORYCZNE.md` (1 denar gry = 1 pens).
Oznaczenia: [K] kod plik:linia, [P] pomiar z logu, [H] historia/lore ze zrodlem, [S] szacunek/rachunek (rzad wielkosci, +-30%).

---

## 0. Dla Jeffa (prostym jezykiem)

1. Masz racje: dzis w miescie nie ma "ludzi z pieniedzmi". Cala kasa miasta to jeden worek "kupcy, rzemieslnicy i mieszczanie razem" - do niego trafiaja place z warsztatow, place murarzy, zold zalogi i wszystko, co zolnierze wydadza w karczmie, a pan co dzien zabiera z tego worka 7% nadwyzki. Nikt w miescie nie zarabia, nie placi podatku i nie kupuje chleba za swoje.
2. Jedzenie, ktore mieszczanie "kupuja" z polek, gra (i Banner Kings) placi zlotem z niczego - ok. 0.6 mln dziennie po roku. To jest dzis jedyny "dochod ludnosci" i jest falszywy.
3. Na wsi jest gorzej: z gotowki, ktora chlopi dostaja za plon, pan zabiera ok. 85%, 15% gra kasuje, a chlopom zostaje prawie zero (mediana kiesy wsi 76 zlotych). Chlop nie ma za co kupic soli, zelaza ani sukna - dlatego wies nic nie kupuje w miescie.
4. W sredniowieczu bylo odwrotnie: pracowali i zarabiali ludzie, a panowie, miasto, korona i Kosciol brali podatki i oplaty - razem ok. 12-25% dochodu zwyklej rodziny. Reszte rodzina wydawala: ok. 3/4 na jedzenie i piwo, reszte na odziez, opal, czynsz za dom, kosciol, leczenie.
5. Proponuje "kiese ludu" w kazdym miescie i kazdej wsi: ludzie dostaja place za prace, zarobek ze sprzedazy plonu i z obslugi zolnierzy; placa panu czynsz i oplaty, septowi dziesiecine, miastu oplaty murowe i targowe; za reszte kupuja jedzenie, sukno, sol, narzedzia z polek miasta - za prawdziwe pieniadze. Worek kupcow (dzisiejsza kasa miasta) zostaje workiem kupcow.
6. Liczby wychodza dziwnie dobrze: przy ludnosci Banner Kings (ok. 3 mln) i cenach historycznych dochod na glowe w grze (ok. 0.4 zl dziennie w miescie, ok. 0.12 zl gotowki na wsi) i to, co BK dzis "zjada", zgadzaja sie z Anglia ok. 1300 co do rzedu wielkosci. Nie trzeba niczego mnozyc ani dosypywac.
7. Co zobaczysz w grze: miasto, do ktorego nie przychodzi wojsko, dwor ani handel, ubozeje (mniej kupuje, spada zadowolenie i dobrobyt); miasto z garnizonem, warsztatami i dworem rosnie. Wies ma pieniadze i kupuje w miescie sukno i narzedzia. Pan dostaje troche mniej "od razu", ale pieniadz i tak do niego wraca przez targ.
8. Jest tez blad w liczeniu ludzi: nasza tabela ludnosci rosnie bez sensu (52.6 mln na starcie -> 90.5 mln po roku; Wyspy Letnie x12). Na razie psuje tylko liczby w logu, ale nowego modelu nie wolno na niej oprzec.
9. Tej nocy proponuje tylko pomiar w logu (ludnosc BK wedlug klas i blad tabeli ludnosci). Sam model to osobna paczka po grupie "kasy miast / dwor / markietani" z projektu ekonomii - i trzeba go wpisac do tamtych opisow, zanim beda skladane, bo inaczej zabetonuja zasade "kasa miasta = wszyscy mieszczanie".

---

## 1. Stan dzis

### 1.1 Kto w swiecie gry w ogole ma pieniadze [K][P]

| Posiadacz | Stan po roku [P] (doba 109199) | Kogo udaje | Uwagi |
|---|---|---|---|
| kasa miasta (`Town.Gold`) | 13.21 mln w 97 miastach (mediana 103 tys.) | **kupcy + rzemieslnicy + mieszczanie razem** | wprost w kodzie: `TownCrafts.cs:43` "kasa miasta = kupcy i rzemieslnicy razem"; opis paczki 111 pkt 3: "kasa miasta to jedna kiesa kupcow i mieszczan" |
| kasa zamku | 4.66 mln w 130 zamkach | podzamcze | jak miasto (BK tick zamku) |
| kiesa wsi (`Village.Gold`) | 0.16 mln w 571 wsiach, mediana 76, 532 wsie < 1 000 | chlopi okregu (symbol ok. 73 tys. ludzi wedlug tabeli) | renta 20%/dobe stanu (`PopulationLaw.cs:300-316`) |
| notable (kiesy bohaterow) | 26.35 mln | zamozni: kupcy, mistrzowie, soltysi, kaplani | dochod glownie z niczego (pasmo 4 500-10 500, `NotablePowerManagementBehavior.cs:47-60`, projekt wiersz 1: 578 tys./dobe) + zysk warsztatow i karawan |
| kapital warsztatow | 2.39 mln | firma (narzedzia, zapas) | wyplata wlascicielowi `DefaultClanFinanceModel.cs:869-872`, BK `BKClanFinanceModel.cs:39-41` (ProfitMade/2) |
| sakiewki ludzi (MenPurse) | 17.93 mln | zolnierze | jedyny "lud z kieszenia" - tylko wojsko |
| rody / skarbce / Bank | 116.7 / 38.9 / 0.7 mln | panowie, korona | - |
| **ludnosc BK (`PopulationData`)** | **0 - nie ma pieniedzy** | szlachta, rzemieslnicy, dzierzawcy, chlopi panszczyzniani, niewolni | ma tylko liczebnosc klas, zadowolenie (`EconomicData.UpdateSatisfaction`) i statystyke `ConsumedValue` (BK `EconomyPatches.cs:606, 637`) |

Odpowiedz na pytanie "czy ludnosc (gospodarstwa) istnieje jako posiadacz pieniedzy": **nie** w miastach i zamkach; **szczatkowo** na wsi (kiesa wsi, ale opodatkowana do zera); zamozna warstwa jest tylko jako notable, a ich dochod jest w wiekszosci z niczego.

### 1.2 Kazdy przeplyw dochodu z pracy i produkcji - kto placi, kto dostaje

| # | Przeplyw | Platnik -> odbiorca (dzis) | Kod [K] | Ile na dobe (rok, ost. doba) [P] |
|---|---|---|---|---|
| 1 | Warsztaty zbrojne (nasze, WorkshopLaw): surowiec kupiony w miescie | warsztat -> kasa miasta | `WorkshopLaw.cs:291-292` | razem z 2: +17 tys. / -33 tys. ("warsztaty zbrojne") |
| 2 | Warsztaty zbrojne: **place za roboczodni** | warsztat -> **kasa miasta** ("place wydane w miescie") | `WorkshopLaw.cs:313-314` | j.w. |
| 3 | Warsztaty zbrojne: sprzedaz wyrobu | kasa miasta -> warsztat | `WorkshopLaw.cs:315-316` | koszt 19.6 tys., sprzedaz 44.2 tys. (linia "Warsztaty") |
| 4 | Warsztaty towarowe (WorkshopTrade): **place cyklu i utrzymanie** | warsztat (albo kiesa gracza) -> **kasa miasta** | `WorkshopTrade.cs:312, 323-324` | place 4 471 + utrzymanie 1 461 |
| 5 | Warsztaty towarowe: nadwyzka ponad cene sprawiedliwa | warsztat -> kasa miasta | `WorkshopTrade.cs:490-491` | 58.8 tys. |
| 6 | Wyplata zysku warsztatu wlascicielowi | kapital -> lord (ProfitMade / wygladzanie) albo notabl (gra zglasza jak zloto z niczego) | `DefaultClanFinanceModel.cs:908-921`, BK `BKClanFinanceModel.cs:28-41` (notabl minus podatek BK `TaxExpenses`) | wynik dnia warsztatow towarowych +2 977 (291 warsztatow) |
| 7 | **Rzemioslo miasta (TownCrafts 148/149)**: welna->sukno, len->plotno, skory->skora | **bez zlota** - polka->polka; dniowka liczona tylko w bramce zysku | `TownCrafts.cs:43, 279-302` | 0 zl |
| 8 | Rzemieslnicy BK (147) - wsad dodatkowych cykli i zwrot za sztuki bez wsadu | kapital rzemieslnikow -> kasa miasta | `ArtisanInputs.cs`; linia "Przeplywy osad" | +19.1 tys. |
| 9 | Budowy oplacone (BuildFunding) | kiesa pana -> **kasa osady** ("place murarzy, robotnikow, woznic"), materialy -> targ | `BuildFunding.cs:174-176` | 17.9 tys. (place 13.4 tys.) |
| 10 | Kopalnie BK: przychod -> "place zostaja w miescie" | (gra BK) -> **kasa miasta** | `BuildFunding.cs:96-99` (`MineRevenuePostfix`) | w "pozostale" |
| 11 | Zold zalogi | glowa rodu -> **kasa miasta / zamku** | `SoldierPay.cs:315-326` | 132.5 tys. (miasta) + 52.3 tys. (zamki) |
| 12 | **Zolnierze wydaja zold** przy wyjezdzie ("karczma, jedzenie, gra, kobiety") | sakiewka -> **kasa miasta** | `MenPurse.cs:173-200` (`:190`) | **1 639.8 tys.** (sakiewki topnieja: -1.16 mln w tej dobie) |
| 13 | Odziez wojska (150) | sakiewka -> kasa miasta | `ArmyClothing.cs:181, 237-238` | 14.4 tys. |
| 14 | Werbunek: zaplata za ochotnika | lord -> **notabl** (rodzina rekruta?), najemnik -> kasa miasta | `LevyGold.cs:36-42, 74-85` | "najemnicy z karczmy" 16.8 tys. |
| 15 | **"Zakupy" mieszczan i zalog** (BK konsumpcja) | **z niczego** -> kasa miasta/zamku; towar znika z polki | BK `EconomyPatches.cs:578-640` (`ChangeGold(num6)` :637); vanilla `ItemConsumptionBehavior.cs:61-71` | **520.8 tys. (miasta) + 92.0 tys. (zamki)**; na starcie 347.6 tys. |
| 16 | Sprzedaz plonu wsi (tabory) | kasa miasta -> taborw -> 70% licznik podatku pana (`TradeTaxAccumulated`), z reszty 50% kiesa wsi, reszta w nicosc | BK `EconomyPatches.cs:1092-1099`, `BKTaxModel.cs:313-325` (0.7) | zaplata taborom 288.3 tys.; do kies wsi 36.8 tys.; zniklo 36.8 tys. |
| 17 | **Renta z miasta** (zawor) | kasa miasta ponad 20 000 x 7% dziennie -> glowa rodu pana | `PopulationLaw.cs:300-316` (`TownRentShare` 0.07, `TownRentFloorGold` 20 000) | 533.0 tys. |
| 18 | **Renta ze wsi** | kiesa wsi x 20% dziennie (najwyzej "nalezna") -> pan | `PopulationLaw.cs:312-315` (`PopulationRentMaxShare` 0.2) | 35.7 tys. |
| 19 | Podatek ludnosci miasta BK (5 klas: szlachta 1.2, rzemieslnicy 0.3, dzierzawcy 0.12, chlopi 0.07, niewolni 0.1 zl na glowe dziennie x TaxIncome) | z niczego -> pan | BK `BKTaxModel.cs:33-41, 164-197`; **wyzerowany** przez `PopulationLaw.TownTaxPostfix` (`:150-165`, `RentReplacesTownTax` = true); zostaja linie placone: podatek od cudzych warsztatow, kopalnie, materialy (`KeptTownLines :168-215`) | 0 (linie klas) |
| 20 | Korona z kas osad (danina, clo, mennica) | kasa miasta -> skarbiec | `KingdomTreasury.cs:221-287` | 113.2 tys. (miasta) |
| 21 | Regulator kasy (gra) | kasuje / dosypuje do 10 000 + 12 x dobrobyt | `DefaultSettlementEconomyModel.cs:75-79` | -259.5 / +48.2 tys. (miasta) |

**Bilans kasy miasta (cale 97 miast, doba 109199) [P]:** wplywy z pracy ludzi (pozycje 2, 4, 9: ok. 22 tys.) to **ok. 1%** wplywow; uslugi dla wojska (12: 1 640 tys.) - **ok. 70%**; "zakupy z niczego" (15: 521 tys.) - **ok. 22%**; zold zalog (11) - ok. 6%. Odplywy: renty panom 533 tys., korona 113 tys., zaplata wsiom 288 tys., regulator netto -211 tys.
Czyli: "dochod miasta" to dzis prawie wylacznie pieniadz wojska i zloto z niczego, a pan bierze z niego 7% stanu dziennie (tj. polowe nadwyzki w ok. 10 dni) - podatek od majatku, nie od dochodu.

### 1.3 Wies: ile gotowki zostaje chlopom [P]

Doba 109199: do kies wsi z utargu taborow +36 777, z utargu zniklo 36 813 (to 15% - wiec utarg taborow ok. 245 tys., a 70% = ok. 172 tys. poszlo do licznika podatku panow), renta z kies wsi -35 714, korona -1 816.
**Pan bierze ok. 172 + 36 = 208 tys. z 245 tys. = 85% gotowki wsi; 15% gra kasuje; chlopom zostaje praktycznie 0** (kiesy wsi: 0.44 mln na starcie -> 0.16 mln po roku).
Wies jest "ujsciem" pieniadza do pana, a nie gospodarstwem: nic nie kupuje (fundament P1: "wies produkuje z natury, nic nie zjada i nic nie kupuje").

### 1.4 Trzy rozne liczby ludzi - i jedna z nich jest zepsuta

| Liczba | Wielkosc | Do czego sluzy | Kod |
|---|---|---|---|
| tabela PopulationLaw (krainy z `POPULACJA-WESTEROS-ESSOS.md`) | 52.6 mln na starcie -> **90.5 mln po roku** [P] | "renta nalezna" (40 zl/glowe/rok), log "Ludnosc:" i "Ludzie:" | `PopulationLaw.cs:84-124` |
| ludnosc BK (`PopulationData.TotalPop`, klasy) | ok. 3.1 mln [S] (wies = hearth x 4-6, miasto 8-15 tys. x (1 + dobrobyt/10 000), zamek 2-3 tys. x ten sam mnoznik; fundament: 2.6-3.9 mln) | popyt i konsumpcja miast, pobor, rzemieslnicy BK | BK `PopulationManager.cs:471-506` (bkall) |
| ksiega ludzi 108 | - | nie ma jej w grze | paczka 108 (paczki-na-120) |

**Blad tabeli:** `PeopleOf` = hearth x k (wies) albo dobrobyt x k (miasto), z k skalibrowanym RAZ w 1. dobie (`PopulationLaw.cs:84-110`, zapis "v1" `:333-358`). Hearth i dobrobyt rosna w grze z innych powodow niz ludnosc, wiec "ludnosc" rosnie razem z nimi:
z logu [P] - doba 108837: 52.6 mln; 108896: 61.7; 108956: 73.1; 109016: 79.0; 109076: 84.8; 109200: 90.5 mln (+72% w rok). Wyspy Letnie 0.75 -> 9.03 mln (x12; kalibracja 5 273 ludzi na punkt hearth - prawie bez wsi na starcie), Reach 8.0 -> 11.3 mln, Volantis 5.0 -> 8.2 mln.
Skutek dzis: "renta nalezna" rosnie 5.78 -> 9.94 mln/dobe, a zaplacona spada 730 -> 569 tys. (5.7% naleznej) - **renta nalezna nic nie znaczy**, placi zawsze sufit z kasy. Log "Ludzie:" pokazuje "0.74% mezczyzn pod bronia" wobec zawyzonej ludnosci.

### 1.5 Ceny, place i zold dzis (zeby liczby modelu byly zgodne) [K][P]

- 1 denar gry = 1 pens (d) Anglii ok. 1300-1349 (`CENY-HISTORYCZNE.md`). Zboze ok. 3 zl za 10 kg (log: 5.5 zl/szt. = 1.83 wartosci), piwo 1 d/galon.
- Dniowka rzemieslnika w kodzie: `WorkshopWagePerDay` 3 (`Settings.cs:445`), mistrz 3 + 1.5/tier (`:511-512`) x poziom plac miasta 0.5-1.5 (`TownWage.cs:17-24`, odniesienie dobrobyt 4 800).
- Zold: partie rodow 605 tys. na ok. 100.5 tys. ludzi = **ok. 6 zl/czlowieka/dobe** (wliczeni rycerze i jazda); zalogi 194 tys. na 79.7 tys. = **2.4 zl** [P] (historycznie piechur 2 d, lucznik 2-3 d, zbrojny 12 d).
- Robotnik 1.5 d, pomocnik budowlany 1.7 d, rzemieslnik 3.1 d (Clark 1300-49, `CENY-HISTORYCZNE.md` rozdz. 8).

---

## 2. Sredniowiecze i lore [H]

Czesc ogolna (podzial dochodu narodowego, czynsze, folwark, dziesieciny, fee farm miast, korona) jest juz zbadana i zweryfikowana w `EKONOMIA-FUNDAMENT-2026-10-05.md` H1-H9 (Campbell 2005/2008, Britnell, Postles, Oschinsky) - tu tylko to, czego tam nie ma: gospodarstwo jako posiadacz pieniedzy.

### 2.1 Z czego zyli (Anglia ok. 1290-1340)

| Kto | Z czego | Dochod | Zrodlo |
|---|---|---|---|
| wyrobnik, robotnik rolny | dniowka 1.5 d (ok. 250 dni pracy w roku), zona i dzieci dorabiaja (przedza, zniwa, piwo) | rodzina ok. 2 L/rok (480 d) = ok. 110 d na glowe | Clark (CENY rozdz. 8); Campbell 2008 tab. 17 (chalupnicy, najemnicy, biedota: 39% gospodarstw po ok. 2 L) |
| chlop (pol-wirgata do wirgaty) | wlasny plon (siew, rodzina, inwentarz), na targ 30-40% zboza, glownie zeby miec pensy na czynsz | 3-5 L/rok brutto, gotowka to mniejsza czesc | fundament H1, H4 (Britnell 2000 s. 5; Campbell 2005) |
| czeladnik | dniowka 2 d (czeladnik haubergera), czesto z utrzymaniem u mistrza | - | `HISTORIA-WARSZTATY.md` (Kirkland 2015) |
| majster | dniowka 3-4 d (murarz 1351: 4 d, ciesla 3 d) albo zysk z warsztatu; platnerz krolewski 12 d | gospodarstwo nierolnicze ok. 4 L/rok | Statut 1351; Campbell 2008 tab. 17 |
| kupiec, patrycjat | marza handlu, czynsze z domow i straganow, udzialy w statkach, pozyczki | 10-26 L/rok (wiekszy dzierzawca, kupiec), wielcy kupcy wiecej | Campbell 2008 tab. 17 (wieksi dzierzawcy 10 L); CENY rozdz. 9 |

### 2.2 Co placili i komu

| Do kogo | Oplata | Wielkosc | Zrodlo |
|---|---|---|---|
| pan (wies) | czynsz (pieniadz; odrobek juz tylko ok. 12% wartosci rent) | 11.3 d na glowe calej ludnosci rocznie; villein 6.8-7.1 d za akr | fundament H2 (Campbell 2005 tab. 3-4) |
| pan (wies) | banalitety: przemial (multure - czesc ziarna, zwykle 1/16-1/24 miary), piec, prasa; kary sadowe, wpisowe (entry fine), heriot, merchet | "sady-mlyny-targi" razem 4.0 d na glowe rocznie (13% dochodu panow) | fundament H2; multure: https://en.wikipedia.org/wiki/Thirlage ; Langdon, *Mills in the Medieval Economy* (2004); https://yorkshiredictionary.york.ac.uk/words/multure |
| pan / korona (miasto) | fee farm - staly ryczalt miasta | 2-11 d na mieszczanina rocznie = 1-5% dochodu miasta | fundament H6 (Postles; VCH Northants) |
| gmina miejska | czynsze gminy, cla i wplywy, sady i wpisowe do gildii; murage (1/2 d od wozu zboza, 1/4 d od juku) | budzet Exeter ok. 100 L/rok; 5-8 d na mieszczanina [S] | fundament H6; https://www.gatehouse-gazetteer.info/murage/muressay.html |
| Kosciol | dziesiecina (1/10 plonu i przychowku, w naturze), ofiary, oplaty pogrzebowe | dziesieciny 6.1-7.7 d na glowe; caly Kosciol 16.3 d (53% dochodu wszystkich panow) | fundament H8 (Campbell 2005 tab. 1) |
| korona | w pokoju prawie nic bezposrednio; w wojnie podatek od ruchomosci (1/10-1/15), ok. 60% chlopow ponizej progu | srednio 2.1 d na glowe rocznie, rok 1290: 6.5 d | fundament H8 (Carpenter; Campbell 2008) |

Suma obciazen zwyklej rodziny chlopskiej: czynsz + prawa panskie + dziesiecina + podatek = ok. 23-25 d na glowe rocznie z ok. 200 d dochodu = **ok. 12% calego dochodu**, ale **ok. 30-50% jej gotowki** (gotowka byla mniejsza czescia dochodu chlopa) [S z liczb H2/H4/H8]. Rodzina miejska placila mniej "panu", wiecej gminie, cechowi i w cenach (myta, akcyza).

### 2.3 Koszyk wydatkow gospodarstwa

| Pozycja | Wyrobnik / chlop (udzial wydatkow) | Uwagi i zrodlo |
|---|---|---|
| jedzenie i piwo | **ok. 75-80%** (chleb i pottage ok. 20%, mieso i ryby ok. 25%, nabial ok. 12.5%, piwo ok. 22.5% w koszyku budowlanca) | koszyk Phelps Brown i Hopkins (1956, "Seven Centuries of the Prices of Consumables...", *Economica* 23) - wagi cytowane z literatury, w otwartym zrodle niesprawdzone; o ich slabosci: Turvey, LSE WP 147 https://www.lse.ac.uk/asset-library/information/wp147.pdf ; Horrell 2023 (EHR 76) potwierdza dominacje cen zboza https://ehs.org.uk/household-consumption-and-the-consumer-price-index-england-1260-1869/ |
| opal i swiatlo | ok. 7.5% | j.w. (seria opalu: Munro, https://www.economics.utoronto.ca/munro5/ResearchData.html) |
| odziez i tkaniny | ok. 12.5% | j.w. |
| mieszkanie | poza koszykiem PBH; czynsz chaty 5 s/rok, dom rzemieslnika 20 s, kupca 2-3 L | `CENY-HISTORYCZNE.md` rozdz. 10 (Dyer) - przy dochodzie wyrobnika 2 L chata to ok. 12% |
| Kosciol, leczenie | kilka procent; leczenie zwyklych ludzi: zielarki, cyrulicy, przytulki przy klasztorach | [S] |
| oszczednosci | najbiedniejsi zadne (zyli z dnia na dzien; glod 1315-17), chlop zamozny - zapas ziarna, zwierze, kilka szylingow | Dyer, *Standards of Living in the Later Middle Ages* (1989) - liczby nieodczytane w otwartym zrodle |

Kontrola na liczbach [S]: osoba je ok. 300 kg zboza rocznie (z piwem; fundament tabela 3.2) = 0.82 kg dziennie x 0.31 d/kg = 0.25 d w ziarnie; z mlynem, piekarzem, piwowarem i odrobina miesa/nabialu ok. **0.35-0.43 d na osobe dziennie**. Dochod na glowe ok. 0.55-0.6 d (201-217 d rocznie, Campbell) - jedzenie to **65-78%** dochodu. Zgodne z koszykiem.

### 2.4 Lore (Gra o Tron)

- **Wiara Siedmiu jest bogata i jest wierzycielem korony:** korona byla winna Wierze prawie milion smokow; Cersei kupila nimi blogoslawienstwo i zgode na Wiare Wojujaca; Wielki Wrobel sprzedaje zlote korony i szaty Wiary na chleb dla biednych (*Uczta dla wron*, rozdz. 28): https://awoiaf.westeros.org/index.php/A_Feast_for_Crows-Chapter_28 , https://awoiaf.westeros.org/index.php/High_Sparrow . Wniosek dla modelu: septy maja prawdziwy dochod od wiernych i wydaja go na jalmuzne i budowle - dziesiecina ma odbiorce w swiecie.
- **Korona pobiera cla, myta i podatki przez urzednikow:** pod mistrzem monety sa straznicy kluczy, mennice, poborcy podatkow (tax farmers), celnicy, myto (toll collectors), faktorzy welny i wina; Littlefinger wczesniej trzymal clo w Gulltown (Lysa: dochod x10): https://awoiaf.westeros.org/index.php/Master_of_Coin (strona blokuje pobranie - fakty ze streszczenia wyszukiwarki), https://en.wikipedia.org/wiki/Petyr_Baelish . Littlefinger kupuje burdele - uslugi dla zolnierzy i miasta to biznes mieszczan (karczmy, burdele), a nie "kasa miasta".
- **Maesterzy** sluza panom w zamkach (z Cytadeli) - leczenie zwyklych ludzi to septy, zielarze, cyrulicy [lore z pamieci; w zrodle niesprawdzone].
- **Essos:** w Zatoce Niewolniczej i Volantis pracuja niewolni (bez wlasnego pieniadza, utrzymuje ich wlasciciel); Braavos zakazuje niewolnictwa [lore z pamieci]. To odpowiada klasie "niewolni" BK.
- **Zelazne Wyspy:** decyzja Jeffa 05.10 - pracuja niewolni (thralls) z rajdow (STAN-PRAC).

---

## 3. Luki i bledy logiki

| # | Luka / blad | Dowod | Skutek |
|---|---|---|---|
| L1 | **Ludnosc nie ma pieniedzy.** Place, uslugi i sprzedaz trafiaja do jednego worka "kasa miasta", z ktorego pan bierze 7% stanu dziennie | 1.1-1.2; `TownCrafts.cs:43`, opis 111 pkt 3, `WorkshopLaw.cs:313-314`, `MenPurse.cs:190`, `BuildFunding.cs:176`, `SoldierPay.cs:325` | nie ma podatku od pracy ani wydatkow rodzin; pan bierze "zysk miasta", a nie podatki - dokladnie zarzut Jeffa |
| L2 | **Jedzenie mieszczan placone z niczego** | BK `EconomyPatches.cs:637`: `town.ChangeGold(num6)` za kazda zjedzona sztuke; 521 + 92 tys./dobe [P] | najwieksze stale zrodlo z niczego w miastach; projekt (K6/111') je cofa, ale konsumpcja zostaje darmowa ("saldo 0 wewnatrz jednej kiesy") - nikt za jedzenie nie placi |
| L3 | **Wies opodatkowana do zera:** 70% utargu do licznika pana + 20% kiesy dziennie + 15% w nicosc | 1.3 [P]; `BKTaxModel.cs:315` (0.7), `EconomyPatches.cs:1094-1095`, `PopulationLaw.cs:312-315` | pan bierze 85% gotowki wsi (historycznie panowie z Kosciolem 18-23% dochodu wsi, fundament H1); chlop nic nie kupuje - brak popytu wsi na sol, zelazo, sukno, garnki |
| L4 | **Renta 20% dziennie od STANU kiesy wsi to podwojny podatek** - czynsz jest juz w 70% od utargu | `PopulationLaw.cs:312-315` wobec `BKTaxModel.cs:313-325` | z punktu L3; projekt 5.3 zostawia te stawke "bez zmian" |
| L5 | **Renta z miasta to 7% stanu kasy dziennie** - podatek od majatku kupcow, nie od dochodu | `PopulationLaw.cs:305-310`; fundament H6: historycznie fee farm 1-5% dochodu miasta rocznie | mechanizm, nie poziom, jest zly (to samo stwierdzil weryfikator fundamentu H6) |
| L6 | **Praca w rzemiosle miasta jest darmowa:** TownCrafts przerabia welne, len i skory bez zaplaty komukolwiek | `TownCrafts.cs:43, 279-302` | sukno i skora powstaja bez plac; nikt nie zarabia na najwiekszym rzemiosle (2x wiecej rak niz cechy zbrojne, `TownCrafts.cs:39-41`) |
| L7 | **Place warsztatow sa za male, zeby cokolwiek znaczyc** - ok. 22 tys./dobe w calym swiecie wobec 1.64 mln wydatkow zolnierzy | 1.2 [P] | dochod "z pracy" w grze to ok. 1% wplywow miast - miasto zyje z wojska, nie z rzemiosla |
| L8 | **Tabela ludnosci PopulationLaw rosnie z hearth i dobrobytem** (+72% w rok, Wyspy Letnie x12) | 1.4 [P]; `PopulationLaw.cs:115-124` | renta nalezna, log "Ludzie" (odsetek pod bronia) i kazdy przyszly wzor "na glowe" oparty na tej tabeli sa bledne |
| L9 | **Ludnosc BK (klasy, zadowolenie) nie jest nigdzie logowana** - mimo zalecenia audytu 05.10 ("dopisac do linii Ludnosc sume TotalPop BK") | grep: zadne `TotalPop`/`GetTypeCount` w `Armoury/src`; `AUDYT-2026-10-05-...md:567-571` | nie wiemy, ilu ludzi naprawde "je" w grze i jakie sa klasy; model nie ma podstawy |
| L10 | **Notable maja dochod z niczego zamiast prawdziwego** (czynsze z domow, zysk handlu) | projekt wiersz 1 (578 tys./dobe z niczego), `NotablePowerManagementBehavior.cs:47-60` | gdy 164c zamknie pasmo na kasach osad, notable nie beda mieli zrodla dochodu poza warsztatami i karawanami |
| L11 | **Dziesiecina / Wiara nie ma platnika ani odbiorcy** | fundament H8 ("w grze brak"); lore 2.4 | brak instytucji, ktora w lore jest jednym z najbogatszych posiadaczy |
| L12 | Podatki klas BK (szlachta 1.2, rzemieslnicy 0.3, dzierzawcy 0.12, chlopi 0.07, niewolni 0.1 zl/glowe/dobe) sa wyzerowane jako "z niczego" - slusznie, ale razem z nimi zniknela jedyna w grze roznica obciazenia miedzy klasami | `BKTaxModel.cs:33-41`, `PopulationLaw.cs:150-165` | przy kiesie ludu te stawki (po przeliczeniu na udzial dochodu) wracaja jako podatek z prawdziwych pieniedzy |

---

## 4. Propozycja: gospodarstwa wedlug klas BK jako posiadacze pieniedzy

### 4.1 Zasady (dopisek do Z1-Z9 projektu)

| # | Zasada | Uzasadnienie |
|---|---|---|
| G1 | **Kasa miasta = kapital kupcow targu**, nie "wszyscy mieszczanie". Ludzie miasta, podzamcza i wsi maja wlasna kiese ("kiesa ludu") w kazdej osadzie. | zarzut Jeffa 08.10; historia 2.1 |
| G2 | **Kazda zaplata za prace albo usluge idzie do kiesy ludu** (place warsztatow, rzemiosla, budow, kopaln; czesc uslugowa wydatkow zolnierzy i dworu); **kazda zaplata za towar idzie do kasy kupcow**. | jedna zasada: praca -> ludzie, towar -> kupcy |
| G3 | **Ludzie placa podatki od dochodu, nie od majatku**: panu, gminie (kasa miasta), septowi/swiatyni, w wojnie koronie. Stawki historyczne (4.3). | historia 2.2; L5 |
| G4 | **Ludzie kupuja z polek za swoje** - konsumpcja BK (ilosc i kategorie bez zmian) jest placona z kiesy ludu do kasy kupcow; gdy brakuje pieniedzy, kupuja mniej (spada zadowolenie BK). | zastepuje "zakupy z niczego" (L2) prawdziwym platnikiem - lepiej niz "saldo 0" K6 |
| G5 | **Kiesa ludu nie gromadzi bez konca (Z2):** zapas = N dni wlasnych wydatkow; ponad zapas 7% nadwyzki dziennie idzie do kasy kupcow ("lokata u kupcow, kupno renty, zakup na zapas") - ta sama stawka co zawor. | Z2, jedna stawka |
| G6 | **Niewolni nie maja kiesy;** ich jedzenie kupuje wlasciciel (patrycjat miasta / notable). | lore Essos; historia |

### 4.2 Posiadacze po zmianie

| Posiadacz | Kto (klasa BK) | Wplywy | Wydatki |
|---|---|---|---|
| **kiesa ludu miasta** (nowa, w zapisie Armoury, jedna na miasto i zamek z podzialem na klasy liczonym, nie osobnymi kiesami) | dzierzawcy/chlopi miejscy = wyrobnicy; rzemieslnicy; szlachta BK w miescie = patrycjat; niewolni (bez wlasnej czesci) | place (pozycje 2, 4, 7, 9, 10 z 1.2), czesc uslugowa zycia zolnierzy (12) i zalogi (11), place dworu (162), udzial ludu w zaworze kupcow (4.4) | jedzenie i towary z polek (do kasy kupcow), czynsz domow (do notabli), podatki (pan, gmina, swiatynia, korona w wojnie), nadwyzka ponad zapas (G5) |
| **kiesa wsi** (jest - zmienia sie regula) | chlopi, niewolni wsi (Zelazne Wyspy, Essos), szlachta wiejska BK (posiadlosci) | 30% utargu taborow (K7, paczka 112) + markietani 15% (163) | sol, narzedzia, sukno, garnki, piwo w najblizszym miescie (do kasy kupcow); dziesiecina; nadwyzka ponad zapas (G5) |
| **kasa kupcow** (dzis kasa miasta / zamku) | kupcy targu | zaplaty za towar (lud, zolnierze, dwor, AI, wies, warsztaty za surowiec) | zakup plonu i towaru (tabory, karawany, warsztaty), zawor 7% nadwyzki (projekt 5.3) - podzial zmieniony (4.4) |
| **notable** (sa) | patrycjat, mistrzowie, soltysi, kaplani | czynsze domow od ludu, zysk warsztatow i karawan, dziesiecina (kaplani) | jak dzis + jalmuzna (kaplani -> lud) i budowa septu (-> kasa kupcow/place) |

### 4.3 Liczby na osobe na dobe (zl gry = d), na ludnosci BK

Dlaczego ludnosc BK, a nie tabela PopulationLaw: (1) to ona dzis je, pracuje i rekrutuje sie w grze; (2) **zloto swiata (189-227 mln) = ok. 3.1 mln ludzi BK x 64 d monety na glowe (Anglia 1290, fundament F6) = ok. 200 mln** - masa pieniadza gry jest dokladnie w skali ludnosci BK [S]; (3) przy tej skali dzisiejsze przeplywy (ponizej) zgadzaja sie z historia co do rzedu wielkosci. Tabela PopulationLaw (52.6 mln) zostaje skala WOJSKA (decyzja Jeffa 05.10 - wojsko 1:1) i towaru; ludzie BK sa "probka" ok. 1:17 tej ludnosci, a pieniadz jest w skali probki. To jest jawne "dwie warstwy" z fundamentu F9, a nie nowe mnozenie.

**Klasy BK w osadach** [K] (`PopulationManager.GetDesiredPopTypes`, bkall `:517-660`): miasto - szlachta 1-3%, rzemieslnicy 6-8%, dzierzawcy/chlopi 60-70%, niewolni 10-20%; zamek - szlachta 7-9%, rzemieslnicy 3-5%, chlopi 75-80%, niewolni 10-15%; wies zywnosciowa - szlachta 3.5-5.5%, chlopi 70-80%, niewolni 10-20%.

| Klasa (BK -> sredniowiecze) | Dochod gotowkowy, zl/os./dobe (cel) | Skad (w grze) | Podatki (udzial dochodu) | Wydatki (udzial reszty) | Zapas kiesy |
|---|---|---|---|---|---|
| dzierzawcy i chlopi w miescie -> **wyrobnicy**, sluzba, tragarze, karczmarki | **0.30** (1.5 d x 250 dni x 1.4 zarabiajacych / 4.4 osoby / 364 = 0.33) | czesc uslugowa zycia zolnierzy i zalog, sluzba dworu, place budow i kopaln, sluzba patrycjatu | pan 2% (kary, oplaty), gmina 1%, swiatynia 1% = **4%** | jedzenie i piwo 75%, odziez 10%, opal 6%, mieszkanie 6% (-> notable), leczenie 1%, reszta zapas | 10 dni wydatkow |
| **rzemieslnicy** -> majstrowie i czeladnicy | **0.60** (gospodarstwo 4 L/rok / 4.4 / 364 = 0.60) | place warsztatow zbrojnych i towarowych, rzemiosla miasta (TownCrafts - nowe place), rzemieslnikow BK | pan 3% (czynsz burgage, ryczalt), gmina i cech 3%, swiatynia 2%, w wojnie korona 2% = **8% (10%)** | jedzenie 60%, odziez 12%, opal 6%, mieszkanie 8%, leczenie 2%, reszta zapas | 30 dni |
| **szlachta BK w miescie** -> patrycjat, kupcy | **3.0** (12-26 L na gospodarstwo; ok. 1/4 dochodu miasta) | udzial ludu w zaworze kupcow (zysk handlu) | pan 4% (ryczalt, myta), gmina 3%, swiatynia 3%, w wojnie korona 3% = **10% (13%)** | jedzenie 35%, odziez i zbytki 20%, sluzba 15% (-> wyrobnicy), utrzymanie niewolnych (G6), reszta zapas i lokata (G5) | 60 dni |
| **niewolni** | 0 | - | - | jedzenie 0.25 zl/dobe placi wlasciciel (patrycjat; na wsi kiesa wsi) | - |
| chlopi wsi -> **chlopi** | **0.10-0.13** gotowki (reszta w naturze - nie liczymy) | utarg taborow, markietani | przy taborze: **pan 65%** (czynsz, folwark, banalitety), **dziesiecina 5%** -> kaplan; chlopi 30% | sol, zelazo i narzedzia, sukno, garnki, piwo w miescie 85%; reszta zapas | 15 dni |
| szlachta wiejska BK -> rycerze, gentry (posiadlosci BK) | z posiadlosci (udzial w podatku utargu, BK `EstateData`) | bez zmian | - | - | - |

Sprawdzenie zgodnosci z dzisiejsza gra [S] (miasta: ok. 1.65 mln ludzi BK = 97 x ok. 17 tys.; wsie ok. 1.1 mln; zamki ok. 0.38 mln):
- konsumpcja BK miast dzis 521 tys./dobe = **0.32 zl/os./dobe** = koszt historycznego jedzenia (0.35-0.43) - gra juz "je" historycznie, tylko za darmo;
- wyrobnicy 1.2 mln x 0.30 + rzemieslnicy 0.13 mln x 0.60 + patrycjat 0.04 mln x 3.0 = **ok. 0.56 mln/dobe** dochodu ludu miast; srednio 0.34 zl/os. (historycznie dochod na glowe 0.55-0.6, z tego miejski gotowkowy wiecej - bo czesc dochodu miasta to handel miedzy mieszczanami, ktorego nie liczymy);
- wsie: 30% utargu taborow (dzis ok. 245 tys.; projekt: plon 374 tys.) + 15% markietanow = 0.11-0.18 mln/dobe = **0.10-0.16 zl/os. gotowki** wobec historycznych 33-48 d sprzedazy rocznie = 0.09-0.13 zl/dobe (fundament H4).

### 4.4 Przeplywy w swiecie po zmianie (wojna, stan ustalony z projektu 5.1; tys. zl/dobe) [S]

```
 ZOLNIERZE (zycie w miescie 404 = 85% z 475) --60%--> KUPCY 242      --40%--> LUD MIASTA 162
 ZALOGI (zold 195; miasta 132)               --60%--> KUPCY 117      --40%--> LUD 78 (miasta 53, podzamcza 25)
 DWOR PANA (373, projekt 6.1)                --75%--> KUPCY 280      --25%--> LUD 93 (sluzba, rzemieslnicy dworu)
 WARSZTATY / RZEMIOSLO / BUDOWY / KOPALNIE   ------------------------------> LUD ok. 60-90 (place; dzis 22 + nowe place TownCrafts)
 KUPCY: zawor 7% nadwyzki (projekt: 684) --> 1/3 LUD (zysk handlu, patrycjat) 228 | 4/9 PAN 304 | 2/9 KORONA 152
 LUD MIASTA (razem ok. 620) --> jedzenie i towary z polek -> KUPCY ok. 470 (dzis "zakupy z niczego" 521)
                            --> czynsz domow -> NOTABLE ok. 40 ; podatki: PAN ok. 20, GMINA (kasa kupcow) ok. 15, SWIATYNIE ok. 15 ; zapas
 TABORY WSI (plon 374) --> PAN 65% = 243 | KAPLAN 5% = 19 | KIESA WSI 30% = 112 (+ markietani 71)
 KIESA WSI (ok. 180) --> sol, narzedzia, sukno, garnki, piwo -> KUPCY MIASTA ok. 150 ; zapas
 SWIATYNIE / KAPLANI (ok. 35) --> jalmuzna -> LUD 50% ; septy i budowy -> KUPCY i place 50%
```

Wynik dla rodow wobec projektu 5.1 [S]: z miast pan dostaje 304 + ok. 20 podatkow ludu = 324 zamiast 456 (-132); ze wsi 243 zamiast 262 + 176 = 438 (-195). Razem **ok. -330 tys./dobe przeplywu "od razu"** (ok. 19% z 1.72 mln). To NIE jest ubytek pieniadza: w obiegu zamknietym te pieniadze wracaja przez targ (lud wydaje u kupcow -> zawor -> pan i korona -> renty korony). Trwale przesuwa sie tylko **zapas**: lud miast ok. 0.62 mln x srednio ok. 20 dni = ok. 12 mln, wsie ok. 0.18 mln x 15 dni = ok. 2.7 mln, razem **ok. 15 mln z 227 mln (6-7%) przechodzi z kies rodow do kies ludzi**. Z wrazliwosci projektu (K9: 29 mln w skarbcach = -5-6 tys. wojska) to **ok. -2.5..-3.5 tys. wojska w wojnie (z ok. 95 tys.)** - do sprawdzenia rachunkiem krytyka (`krytyk\sim_krytyk.py`) przed wdrozeniem, bo przesuniecie dochodu z panow zamkow (wsie) do panow miast i korony moze pogorszyc los rodow z samym zamkiem (problem znany z projektu 1.2).

Jak to sie zamyka z projektem:
- **Z1 (platnik i odbiorca):** kazda strzalka wyzej ma oba konce; "zakupy z niczego" (projekt wiersz 2) dostaja platnika - lud - zamiast "saldo 0 w jednej kiesie".
- **Z2 (zawor):** kiesa ludu i kiesa wsi oddaja nadwyzke ponad zapas tym samym 7% dziennie do kupcow (G5). Kupcy oddaja zaworem dalej (5.3).
- **Z5 (dwor):** dwor zostaje glownym kanalem pana do miasta; zmienia sie tylko podzial w miescie (75% kupcy, 25% lud).
- **Z8 (nikt nie zarabia na wlasnym wydatku):** z monety wydanej przez pana we wlasnym miescie wraca do niego mniej niz dzis (4/9 zaworu zamiast 2/3, plus 2-4% podatku ludu) - zwrot z monety spada z 0.52-0.68 (projekt 7.2) do ok. 0.35-0.5 [S].
- **Z9 (szczelnosc):** nowe przeplywy to przelewy miedzy posiadaczami - zero z niczego. Konsumpcja BK przestaje tworzyc zloto, wiec ta paczka MUSI isc razem z albo po K6 (111'), inaczej lud placilby za towar, za ktory BK i tak dopisuje zloto kupcom (podwojnie).
- **163 markietani:** 85% miasto / 15% wies zostaje; w miescie 60% kupcy / 40% lud.
- **164c pasmo notabli:** notable dostaja prawdziwy dochod (czynsze domow ok. 40 tys./dobe, dziesiecina kaplanow) - mniej potrzeby dosypki z kas osad.

### 4.5 Propozycje (kazda osobno)

| # | Co zmienic | Liczby i uzasadnienie (jedno zdanie) | Prio | Wielkosc | Zmienia gre gracza | Nowa kampania | Ryzyko | Zaleznosci |
|---|---|---|---|---|---|---|---|---|
| A | **Pomiar ludnosci BK** w linii "Ludnosc:" - suma `TotalPop` i klas (szlachta, rzemieslnicy, dzierzawcy, chlopi, niewolni) dla miast, zamkow, wsi i swiata, zadowolenie srednie, `ConsumedValue` | bez tego zadna liczba "na glowe" nie ma podstawy (L9) | **P0** | mala | nie (log) | nie | minimalne (odczyt przez refleksje, 800 osad raz na dobe) | brak; nie koliduje z 169 (inny plik) |
| B | **Pomiar dryfu tabeli ludnosci:** obok "ludnosc" krainy podac kalibracje i stosunek (dzis / kalibracja) | Wyspy Letnie x12, swiat x1.72 w rok (L8) | **P0** | mala | nie | nie | minimalne | brak |
| C | **Decyzja projektowa (zero kodu): kasa miasta = kapital kupcow;** dopisac do opisow 111' (K6), 162 (dwor), 163 (markietani), 164c przed ich skladaniem | inaczej K6 zabetonuje "jedna kiesa kupcow i mieszczan", a konsumpcja zostanie darmowa (L1, L2) | **P0** | mala (dokumenty) | nie | nie | brak | projekt rozdz. 12, grupa C |
| D | **Naprawa tabeli ludnosci:** ludnosc osady = stala z kalibracji x (hearth dzis / hearth przy kalibracji) tylko w granicach przyrostu demograficznego (np. najwyzej +0.5%/rok), albo wprost ludnosc BK x stala krainy; renta nalezna przeliczona na klasy (4.3) | +72%/rok to nie demografia - historycznie przyrost ok. 0.5%/rok przed 1300 [S] | P1 | mala-srednia | malo (renta placona i tak z sufitu kasy) | nie (stara kalibracja w zapisie, `Import`) | srednie - zmienia "Ludzie:" i pobor, jesli 108 czyta te tabele | 108 (ksiega ludzi) - najlepiej razem |
| E | **KIESA LUDU MIASTA (robocza 172):** nowa kiesa w kazdym miescie i zamku (zapis "arm_townfolk"), place i uslugi wedlug 4.4, konsumpcja BK placona z niej (prefiks First na `ItemConsumptionBehavior.MakeConsumption` skaluje popyt `categoryDemand` przez wspolczynnik stac = min(1, kiesa / koszt), postfiks przenosi `ConsumedValue` z kiesy ludu do kasy kupcow zamiast cofania z K6), podatki 4.3, zapas i nadwyzka G5; start: przelew z kasy miasta do kiesy ludu = 20 dni konsumpcji (bez dosypki) | dochod ok. 0.56 mln/dobe wobec konsumpcji 0.47-0.52 mln - lud stac na jedzenie, gdy do miasta plynie wojsko, dwor i praca | P1 | **duza** | **tak** - miasta bez wojska i dworu ubozeja; renty z miast mniejsze | zalecana (razem z grupa C, ktora i tak jej wymaga) | duze: zadowolenie BK spada przy niskim "stac" -> dobrobyt, lojalnosc; latka na tej samej metodzie co K5/K6 i BK (kolejnosc prefiksow - patrz rozpoznanie 169-2 "prefiks moze nie pobiec") | 111' (K6), 162, 163, 169 (licznik), A |
| F | **Place za prace do kiesy ludu** (zamiast kasy miasta): `WorkshopLaw.cs:313-314`, `WorkshopTrade.cs:323-324` (galaz `wages`), `BuildFunding.cs:176`, `BuildFunding.cs:98` (kopalnie), czesc uslugowa `MenPurse.cs:190` i `SoldierPay.cs:325` (40%) | jedna zasada G2: praca -> ludzie, towar -> kupcy; 40% uslug w wydatkach zolnierza to nocleg, kuchnia, pranie, kobiety, gra [S] | P1 | srednia | tak (posrednio) | nie | srednie: tarcza zoldu `SoldierPay.Hold` i licznik `MoneyLedger.Note` musza isc za pieniedzmi | E |
| G | **Place w rzemiosle miasta (TownCrafts):** za kazda sztuke sukna, plotna, skory kasa kupcow placi ludziom robocizne (dzis liczona tylko w bramce, `TownCrafts.cs:279-302`) = roboczodni x dniowka miasta | praca to 20% wartosci wyrobu (`TownCrafts.cs:34`) - dzis darmowa (L6) | P1 | mala-srednia | nie | nie | male: kupcy placa, wiec bramka zysku juz to uwzglednia | E albo F (odbiorca) |
| H | **Wies:** koniec renty 20%/dobe ze stanu (`PopulationLaw.cs:312-315`); utarg taborow: pan 65%, kaplan 5%, wies 30% (dzis BK 70% + 15% w nicosc, K7 daje 30% wsi); wies wydaje w najblizszym miescie (sol, narzedzia, sukno, garnki, piwo) przy wizycie taboru do 85% swojej kiesy ponad zapas 15 dni | dzis pan bierze 85% gotowki wsi, historycznie panowie z Kosciolem 18-23% dochodu wsi, a 30-50% jej gotowki (2.2) | P1 | srednia | tak - wsie kupuja w miastach, pan wsi dostaje mniej od razu | nie | srednie: rody z samym zamkiem traca dochod ze wsi (-195 tys./dobe swiat, 4.4) - musi isc razem z rentami korony wedlug lenn (165) | 112 (K7), 163, 165 |
| I | **Podzial zaworu kupcow:** 1/3 lud (zysk handlu - patrycjat), 4/9 pan, 2/9 korona (projekt: 2/3 pan, 1/3 korona) | historycznie miasto zylo z handlu, a panu placilo 1-5% dochodu (fundament H6); bez tego lud miast nie ma 1/3 swojego dochodu | P1 | mala (parametr) | tak - mniejsze renty z miast | nie | srednie: kalibracja wojska (4.4) | E, 111', 165 |
| J | **Dziesiecina i swiatynie:** 5% utargu wsi i 1-3% dochodu ludu -> kaplan-notabl osady (gdy brak - kasa kupcow jako "sept"); kaplan wydaje 50% na jalmuzne (-> kiesa ludu), 50% na sept (-> kasa kupcow / place budow) | Wiara w lore bogata i wierzyciel korony (2.4); historycznie dziesieciny 6-8 d na glowe (H8) | P2 | mala-srednia | tak (niewiele) | nie | sprawdzic, czy w ROT sa notable-kaplani (Occupation.Preacher) w kazdej kulturze | E, H |
| K | **Czynsz domow -> notable miasta** (6-8% wydatkow ludu, wedlug sily notabla) | prawdziwy dochod notabli zamiast pasma z niczego (L10) | P2 | srednia | nie | nie | male | E, 164c |
| L | **Zadowolenie klas BK z kiesy ludu:** "stac" < 0.8 przez 7 dni -> zadowolenie jedzenia spada jak przy braku towaru (BK `UpdateSatisfaction` -0.0015) | przyczyna -> skutek: bieda ludu obniza dobrobyt i lojalnosc (BK juz to liczy z braku towaru) | P2 | srednia | **tak** | nie | duze (spirala biedy w miastach bez wojska) - najpierw sam log | E |
| M | **Napis dla gracza** w menu miasta (po angielsku): "Townsfolk: purse X, earned today Y (wages Z), paid to you W, can afford N% of their bread" | gracz widzi, skad sa jego renty | P2 | mala | tak (UI) | nie | male | E |

### 4.6 Kolejnosc w kodzie

1. **A + B** (log, tej nocy) -> autotest 40 dob: ilu ludzi BK, jakie klasy, jak rosna; dryf tabeli.
2. **C** - wpis do opisow paczek grupy C i do PROJEKT-EKONOMIA-OBIEG (rozdz. 4.1 wiersz 2, 5.3, 6.1, 163): "kasa miasta = kupcy; lud osobno (paczka 172)".
3. **169** (w toku) - poprosic, zeby linia "Obieg" rozbijala wplywy kas miast na "praca i uslugi" (2, 4, 9, 10, 40% z 11-12, 25% dworu) i "towar" - to jest pomiar "na sucho" dla E/F bez nowej latki.
4. **108** (ksiega ludzi) + **D** (naprawa tabeli ludnosci) - jedna podstawa liczby ludzi.
5. Grupa C projektu (110, 111', 162, 112, 163) **razem z E + F + G + I** (jedna nowa kampania; E bez K6 liczylaby jedzenie podwojnie, K6 bez E zostawia jedzenie darmowe).
6. **H** razem z **165** (renty korony wedlug lenn ratuja rody z samym zamkiem).
7. **J, K, L, M** - po roku autotestu E/H.

Do sprawdzenia przed E (zasada 0 CLAUDE.md): wszyscy, ktorzy pisza do `Town.Gold`/`SettlementComponent.Gold` (lista 1.2 + BK `HandleMarketGold`, notable doplacajacy pustej kasie, BEE inwestycje gracza), tarcza zoldu (`SoldierPay.Hold`), `TownPurse.TaxFloor` (zapas kupcow liczony tylko od kasy kupcow), `MoneyLedger` (nowy posiadacz w "Pieniadz swiata"), RC/GT czytaja `TownWage.Index` i `TownRentFloorGold` przez refleksje (`TownWageLink`).

---

## 5. Do zrobienia tej nocy (male, bezpieczne, sprawdzalne autotestem 40 dob) vs na pozniej

### Tej nocy (tylko log, zadnej zmiany w grze)

1. **A - ludnosc BK w logu.** `Armoury/src/PopulationLaw.cs`, metoda `Daily()` (`:272-331`), tuz przed `Log.Info("Ludnosc: dzien ...` (`:327`): dla kazdej osady z `_popMgr`/`GetPopData` (rozwiazywane dzis w `KeptTownLines` `:182-185` tylko przy wlaczonej podmianie podatku - wydzielic rozwiazanie do malej metody `ResolveBk()`) odczytac `TotalPop` i `GetTypeCount(PopType)` dla 5 klas (refleksja: `BannerKings.Managers.PopulationManager+PopType`), zsumowac osobno miasta / zamki / wsie i wypisac nowa linie
   `Ludnosc BK: dzien N | swiat X (miasta a, zamki b, wsie c) | szlachta s, rzemieslnicy r, dzierzawcy d, chlopi ch, niewolni n | stosunek do tabeli PopulationLaw 1:k | konsumpcja (ConsumedValue) suma v`. Wszystko w `try` na osade, licznik potkniec (CLAUDE.md: nie gasic funkcji). Sprawdzenie w autoteście: linia co dobe, suma ok. 2.6-3.9 mln, 0 wyjatkow.
2. **B - dryf tabeli ludnosci.** Ta sama metoda: w `Calibrate()` (`:82-110`) zapamietac ludnosc krainy z kalibracji (jest w `Table` x `PopulationScale`); w linii "Ludnosc:" dopisac dla 5 najwiekszych odchylen "kraina dzis/kalibracja = x1.72". Bez zmiany kalibracji i bez zmiany renty. Sprawdzenie: Wyspy Letnie i swiat pokazuja wzrost od 1. doby.
3. Nic wiecej tej nocy - E-M zmieniaja gre (Jeff: "wgraj" tylko po sprawdzonym buildzie i w osobnym kroku).

Kolizje z paczkami w toku: **169** (sam log obiegu) pisze w `MoneyLedger.cs` - A i B sa w `PopulationLaw.cs`, wiec bez kolizji w pliku; uzgodnic tylko, zeby 169 nie dodawala drugiej linii "Ludnosc". **170** (BEE) - brak styku. **171** (zbrojenie zalog: zamek kupuje w miescie) - zgodne z G1/G2 (zaplata zamku za towar idzie do kasy kupcow miasta); przy E zakupy zalog sa "towarem", nie "praca".

### Na pozniej

- C (wpis do opisow paczek grupy C) - przy najblizszym przegladzie projektu, przed skladaniem 110/111'.
- D z 108; E, F, G, I z grupa C (nowa kampania); H z 165; J, K, L, M po roku autotestu.
- Rachunek krytyka (`sim_krytyk.py`) z kiesa ludu (zapas 10/30/60 dni, podzial zaworu 1/3-4/9-2/9, wies 65/5/30) przed E/H - czy wojsko w wojnie zostaje w 85-100 tys. i czy rody z samym zamkiem nie biednieja.

### Pytania do Jeffa (tylko to, co zmienia gre albo kanon)

1. **Miasta, do ktorych nie przychodzi wojsko, dwor ani praca, maja ubozec** (lud mniej kupuje, spada zadowolenie i dobrobyt) - zgoda? (Rekomendacja: tak, to przyczyna i skutek; najpierw tylko log, potem skutek.)
2. **Wiara Siedmiu / swiatynie jako odbiorca dziesieciny** (ok. 5% utargu wsi i 1-3% dochodu mieszczan; pieniadze wracaja jako jalmuzna i budowa septow) - wprowadzic? To kanon (Wiara bogata, korona winna jej prawie milion smokow), ale zmniejsza dochod panow o kilka procent.
3. **Jako pan miasta dostaniesz mniej "od razu"** (zysk handlu zostaje w 1/3 u mieszczan; pieniadz wraca do Ciebie wolniej, przez targ i podatki) - zgoda?

---

## 6. Zrodla

Kod [K]: `Armoury/src/PopulationLaw.cs:84-124, 150-215, 272-331`; `TownCrafts.cs:34-46, 132, 259-302`; `TownWage.cs:17-24`; `WorkshopLaw.cs:291-316`; `WorkshopTrade.cs:21-55, 296-330, 480-495, 700-770`; `MenPurse.cs:160-200`; `SoldierPay.cs:315-330`; `BuildFunding.cs:90-99, 170-176`; `LevyGold.cs:30-45, 70-90`; `ArmyClothing.cs:181, 237-238`; `Settings.cs:384, 435-445, 511-514, 538-546, 643`.
Gra 1.4.8: `WorkshopsCampaignBehavior.cs:279-289, 750-812`; `DefaultClanFinanceModel.cs:280-310, 865-925`; `DefaultSettlementTaxModel.cs:19-111`.
BK: `EconomyPatches.cs:578-660 (konsumpcja), 832-882 (warsztaty), 1083-1110 (tabory), 1273-1289 (budzet konsumpcji)`; `BKTaxModel.cs:31-197, 313-325`; `BKEconomyModel.cs:565-630` (popyt klas); `BKClanFinanceModel.cs:28-50, 87-94, 233-247`; `WorkshopExtensions.cs:10-15`; `WorkshopData.cs:23-24` (Tick pusty - produkcja WorkshopData martwa); bkall `PopulationManager.cs:430-660`.
Pomiary [P]: `Armoury-2026-10-08_08-18-38.log` linie "Przeplywy osad (kasy miast / kasy zamkow / kiesy wsi)", "Pieniadz swiata", "Pieniadz swiata (bilans)", "Ludnosc:", "Ludzie:", "Sakiewka ludzi", "Zold", "Warsztaty", "Warsztaty towarowe", "Towary (utarg wozow)", "Budowy oplacone", "PopulationLaw: kalibracja" - doby 108836-109200.
Historia [H]: fundament rozdz. 2 (Campbell 2005, 2008; Britnell 2000; Postles; Oschinsky; VCH) i adresy tam; `CENY-HISTORYCZNE.md` (Clark, Dyer); https://en.wikipedia.org/wiki/Thirlage ; https://yorkshiredictionary.york.ac.uk/words/multure ; https://www.gatehouse-gazetteer.info/murage/muressay.html ; Phelps Brown i Hopkins 1956 (wagi koszyka z literatury, nie z otwartego zrodla) ; https://www.lse.ac.uk/asset-library/information/wp147.pdf ; https://ehs.org.uk/household-consumption-and-the-consumer-price-index-england-1260-1869/ ; https://www.economics.utoronto.ca/munro5/ResearchData.html .
Lore: https://awoiaf.westeros.org/index.php/A_Feast_for_Crows-Chapter_28 ; https://awoiaf.westeros.org/index.php/High_Sparrow ; https://awoiaf.westeros.org/index.php/Master_of_Coin (strona blokuje pobranie - tresc ze streszczenia wyszukiwarki) ; https://en.wikipedia.org/wiki/Petyr_Baelish .
Nie sprawdzone (uczciwie): wagi koszyka PBH w zrodle pierwotnym; budzet chlopa u Dyera; maesterzy i niewolnictwo Braavos (lore z pamieci); liczba ludzi BK w grze (tylko ze wzoru - stad propozycja A); czy w kazdej kulturze ROT sa notable-kaplani.
