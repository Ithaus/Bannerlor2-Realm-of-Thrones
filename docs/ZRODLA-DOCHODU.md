# Zrodla dochodu krola i pana: historia (Anglia/Francja ok. 1280-1400) a gra (polityki, prawa, dekrety)

Data: 2026-10-04 (po wpisie CHANGELOG 49). Tylko odczyt - kodu nie ruszalem.
Przelicznik: 1 moneta = 1 pens, 12 d = 1 s, **240 d = 1 L (funt)**, marka = 160 d. Rok gry = 364 dni (log `Kalendarz`).
Anglia ok. 1300 ~4.7 mln ludzi (szacunek przyjety w AUDYT-EKONOMIA-KROLESTW), krolestwo ROT srednio ~1.8 mln (0.4 Anglii).

Oznaczenia: **[Z]** = liczba ze zrodla podanego na koncu (sekcja 4), **[SZ]** = szacunek z pamieci/literatury,
nie sprawdzony w tej sesji - traktowac jako rzad wielkosci.
Kod: `tw/` = TaleWorlds.CampaignSystem.dll (dekompilacja 20.09 + `scratchpad/van/` 04.10), `bk/` = BannerKings.dll
(dekompilacja z 20.09 - **sprawdzona 04.10: zainstalowany DLL z 21.08 daje identyczny `BKTaxModel`**), `be/` = BetterEconomy,
`rot/` = ROT.dll. Numery linii to linie w plikach dekompilacji (moga sie przesunac o kilka przy innej wersji ilspy).

---

## 0. Najwazniejsze w 10 punktach

1. **Historycznie krol i pan zyli z roznych kieszeni.** Pan: z ziemi (domena ~45%, czynsze ~47%, banalia/sady/mlyny ~10% -
   placili chlopi) [Z]. Krol: z wlasnej domeny i miast (farmy) - malo; glownie z **cel od eksportu welny** (placili kupcy,
   ponosili hodowcy) i **podatkow nadzwyczajnych na wojne** (pietnastka/dziesiecina od ruchomosci - placili chlopi i mieszczanie,
   NIE z dochodu panow) + dziesieciny duchowienstwa + **kredytu wloskich bankow** splacanego z cel.
2. **W grze tylko jedno zrodlo krola jest zbudowane historycznie poprawnie: CrownDuty (vanilla) = 5% licznika cel kazdego miasta
   krolestwa do rodu krola** - przeplyw zachowany (z kasy miasta przez `TradeTaxAccumulated`). Reszta polityk krola placi z niczego.
3. **War Tax (vanilla) po wpisie 49 daje 0** - liczy 5% z `CalculateTownTax` wszystkich lenn, a my ten wynik zerujemy.
   Zostala tylko kara -1 dobrobytu miast. Krol nie ma dzis zadnego "podatku wojennego" poza naszym `CrownDues` 10% od wasali.
4. **Z niczego (polityki):** Debasement of the Currency (100 d/lenno/dzien), State Monopolies (5% zysku warsztatow w miastach krola,
   zysk nie jest pomniejszany), Land Tax (5% licznika wsi wasali bez odjecia z licznika - liczone dwa razy), umowy handlowe vanilli
   (500 d za kazda wizyte obcej karawany), BK "Tax Office" we wsi (podatek klas ludnosci wsi).
5. **Wpis 49 ma trzy skutki uboczne, ktorych nie bylo w opisie:** (a) podatki od warsztatow BK nadal sa POBIERANE od wlascicieli
   (notable, obce rody), ale juz nie TRAFIAJA do pana miasta - nowe ujscie w nicosc; (b) kopalnie BK: miasto placi pelna cene rudy,
   a 50% "renty z kopalni" juz nie trafia do pana - ujscie w nicosc; (c) koszt materialow projektow BK przestal obciazac pana
   (budowy tansze). Do tego War Tax = 0 (pkt 3), a pensje rady BK nadal sa liczone od podatku, ktorego juz nie ma (male, 1-4.5%).
6. **BK "Taxes to/from suzerain" (umowa feudalna) = 0 zawsze** - `BKTaxModel.CalculateDueTax` jest pusta (`bk/BKTaxModel.cs:354-357`),
   `FeudalTitle.DueTax` nigdy nie jest ustawiany. Powinnosci wasali wobec korony robi wylacznie nasz `KingdomTreasury.Daily`.
7. **BK polityka podatkowa osady (Low/Standard/High/Exemption)** dzis steruje: udzialem wsi w utargu wiesniakow (50/70/90%),
   prowizja miasta od sprzedazy (10% -/+4 pp, Exemption 0), podatkiem od warsztatow (10/20/30% zysku), czescia dochodu posiadlosci,
   lojalnoscia i dobrobytem. **Nie steruje nasza renta** (PopulationLaw) - po wpisie 49 to najwiekszy dochod z miasta.
8. **Propozycja (sekcja 3):** podatek wojenny przeniesc z dochodu panow na **kasy osad** (pietnastka), CrownDuty zrobic **clem do
   skarbca krolestwa** (zamiast 5% do rodu krola), Debasement/Monopolies/Land Tax/umowy handlowe przestawic na przeplywy zachowane,
   poziom polityki podatkowej BK = mnoznik renty, w latce wpisu 49 zostawic linie "warsztaty" i "kopalnie".
9. **Skala docelowa (krolestwo 1.8 mln ludzi):** korona w pokoju ~1.5 d/glowe/rok = ~7.5 tys. d/dzien (z wlasnych lenn + cla),
   w wojnie +~2 d/glowe na kazda "pietnastke" = ~10 tys. d/dzien przy jednej subsydii rocznie. Wielki pan: rzad wielkosci hrabiego,
   1-6 tys. L/rok (660-4000 d/dzien) - z renty i "Village Demesnes".
10. Czego nie wiem: ktore krolestwa AI maja dzis wlaczone War Tax / Debasement / State Monopolies / Crown Duty (w logach brak) -
    pierwszy krok to wypisac `Kingdom.ActivePolicies` w CrashScribe (sekcja 3, krok 0).

---

## 1. (A) Historia: zrodla dochodu krola i wielkiego pana, ok. 1280-1400

### 1.1 Krol Anglii

| Zrodlo | Kto placil, z czego (kto stracil pieniadz) | Skala | Uwagi |
|---|---|---|---|
| **Domena krolewska** (czynsze z wlasnych dworow, ziemie) | dzierzawcy i chlopi z ziem krola - z produkcji rolnej | **kilka tys. L/rok** [SZ] | Kurczaca sie (nadania dla rodziny/faworytow) |
| **Farmy hrabstw i miast (fee farm, firma burgi)** | szeryf / miasto placi stala kwote roczna; miasto zbiera ja z oplat targowych, myt, czynszu parcel | Londyn ~300-400 L/rok, razem kilka tys. L [SZ]; **91 miast mialo nadana farme przed 1348** [Z] | Stala kwota - nadwyzka zostaje w miescie |
| **Incydenty feudalne** (relief, wardship, marriage, escheat) | dziedzic/wdowa/opiekun - z dochodu dziedziczonego majatku | od kilkuset do kilku tys. L/rok, nieregularne [SZ]; w XII-pocz. XIII w. razem z domena 75-80% dochodu korony [Z] | Magna Carta 1215: relief barona 100 L, rycerza 100 s |
| **Scutage** (tarczowe zamiast sluzby) | rycerze i baronowie trzymajacy od korony | w XIII w. malejace, **w XIV w. przestarzale** [Z] | Zastapione kontraktami (indenture) i placa |
| **Feudal aids** (pasowanie syna, slub corki, okup) | wasale bezposredni (i ich poddzierzawcy) | **aid 1306** na pasowanie Edwarda - rzad 1-2 tys. L [SZ] | Rzadkie, przewidziane prawem |
| **Sady i grzywny, wyprawy sadowe (eyre), lasy (forest eyre)** | stron sporow, przestepcy, naruszajacy prawo lasu | kilka tys. L w latach eyre [SZ] | Nieregularne |
| **Cla: Ancient Custom 1275** (6 s 8 d od worka welny) | eksporterzy welny (kupcy angielscy i obcy); ekonomicznie - przez nizsza cene - hodowcy welny | **~10 000 L/rok** [Z] | Pobierali je bankierzy Riccardi jako zabezpieczenie pozyczek [Z] |
| **Maltolt 1294-97** (40 s od worka) | eksporterzy, w praktyce hodowcy | kilkadziesiat tys. L/rok [SZ] | Wymuszony, cofniety 1297 [Z: Maltolt] |
| **Subsydium welniane Edwarda III** (40 s/worek od 1337) | eksporterzy (Staple), ponosili hodowcy | szczyt **>112 000 L w 1353-54** [Z]; na poczatku wojny 1337: cla ~13 000 z ~30 000 L zwyklego dochodu [Z] | "Najwazniejsza bron skarbu poznosredniowiecznego panstwa" (Ormrod) [Z] |
| **Lay subsidies - pietnastka i dziesiecina** (od ruchomosci: wies 1/15, miasta i domena krola 1/10) | **chlopi i mieszczanie** powyzej progu (zboze, bydlo, towary); panowie od wlasnych ruchomosci - maly udzial | **1290: 116 000 L** - najwiekszy podatek sredniowiecza [Z]; 1275 ~81 000 L [SZ]; od **1334 stala kwota** na wies/miasto [Z] ~38 000 L za "pietnastke i dziesiecine" [SZ] | Tylko na wojne/nadzwyczajne potrzeby, za zgoda parlamentu |
| **Dziesieciny duchowienstwa** (papieskie i za zgoda konwokacji) | beneficja koscielne - z dziesieciny i ziemi Kosciola | ~18-20 tys. L za jedna dziesiecine (Taxatio 1291) [SZ]; 1294 Edward I zadal **polowy** dochodow duchowienstwa [SZ] | Kosciol = osobna kieszen, nie chlop bezposrednio |
| **Mennica (seigniorage, przebicie)** | posiadacze monety oddajacy srebro do mennicy (oplata od wybitego funta) | w Anglii male (setki-niewiele tys. L/rok) [SZ]; recoinage 1279 dalo jednorazowy zysk i konfiskaty [Z] | Anglia NIE psula monety (inaczej niz Francja) |
| **Zydzi (tallage)** | gmina zydowska - z odsetek od pozyczek (czyli ostatecznie dluznicy) | srednio **~2 000 L/rok w XIII w.**, 1241 tallage 20 000 marek [Z] | Wypedzeni 1290 - zamiana na subsydium 116 000 L [Z] |
| **Kupcy obcy** (Carta Mercatoria 1303, New Custom) | kupcy zagraniczni - doplata do cel | kilka tys. L/rok [SZ] | |
| **Pozyczki wloskie** | Riccardi (Edward I, do bankructwa 1294) [Z]; Bardi i Peruzzi (Edward III: wg Villaniego 135 000 i 90 000 L) [Z] | przedterminowo dziesiatki-setki tys. L | **Nie dochod** - splacane z cel (Bardi/Peruzzi brali cla bezposrednio w portach) [Z] |
| **Lupy wojenne, okupy** | pokonani i ich poddani | Jan II: 3 mln ecu (~500 tys. L, splacone czesciowo) [Z]; Dawid II: 100 000 marek [SZ] | Zrodlo nieregularne, w XIV w. wazne dla magnatow |

**Udzialy (Anglia, rzad wielkosci) [SZ]:**
- Pokoj (np. lata 1280-te, 1360-te): domena + farmy + incydenty + sady ~30-40%, **cla ~30-50%**, reszta mennica/inne.
  Zwykly dochod ~25-30 tys. L (1337: ~30 000 L, z tego cla 13 000 [Z]).
- Wojna: dochodza subsydia swieckie (35-40 tys. L za kazda) i duchowne (~20 tys. L), podwyzszone cla (do 60-110 tys. L)
  i kredyt; dochod w roku wojny 80-150 tys. L, wydatek wiekszy - dlug.
- Per glowa: korona pokojowo ~1.3-1.5 d/glowe/rok, **jedna pietnastka ~2 d/glowe**, cla ~0.6-0.7 d/glowe (z hodowcow welny).

### 1.2 Krol Francji (dla porownania, ten sam okres)

- **Domena krolewska** (prevotes, baillages) - glowna "zwykla" kasa; staly dochod rzedu kilkuset tys. livres tournois [SZ].
- **Psucie monety (mutations)**: Filip IV 1295-1305 - zysk mennicy pokrywal czesc kosztow wojny; do 1306 moneta stracila ~2/3 wartosci [Z].
  Placili posiadacze monety i wierzyciele (inflacja) - to "podatek" od wszystkich trzymajacych pieniadz.
- **Decimy od duchowienstwa** - Filip IV zadal polowy rocznych dochodow kleru (konflikt z Bonifacym VIII) [Z].
- **Konfiskaty**: Zydzi 1306, Lombardczycy, templariusze 1307 [SZ] - jednorazowe.
- **Maltote / aides** - podatki od sprzedazy (od 1360: ~1/20 = 12 d od livra), **gabelle** (sol), **fouage** (od paleniska) - wprowadzone
  na stale na okup Jana II (3 mln ecu [Z]) [SZ]. Placili kupujacy na targu (aides, gabelle) i gospodarstwa (fouage).
- Wniosek: Francja = wiecej podatku od obrotu i od paleniska, Anglia = wiecej cla eksportowego i podatku od ruchomosci.

### 1.3 Wielki pan (hrabia/baron)

| Kto | Dochod brutto | Zrodlo |
|---|---|---|
| Tomasz Lancaster (5 hrabstw) | **~11 000 L/rok** [Z] | najbogatszy |
| Guy Beauchamp, hrabia Warwick | **~6 000 L** [Z] | |
| Gilbert de Clare, hrabia Gloucester | **~6 000 L** [Z] | |
| Zwykly baron | 200-500 L [SZ] | |

**Struktura dochodu pana w XIV w.** [Z]: **~45% domena** (wlasna uprawa, sprzedaz zboza i welny), **~47% czynsze** chlopskie,
**~10% banalia** (sad dworski, mlyn, targ). Mlyny same >=5% [Z]. Panowie brali razem ~18-23% netto calego dochodu z ziemi [Z].

Skladniki "dworskie" (czesc czynszu i banaliow) [SZ]:
- **heriot** - najlepsze bydle po smierci chlopa (w naturze lub pieniadzu, kilka-kilkanascie s),
- **merchet** - oplata za slub corki (ok. 6 s 8 d - 1 L), **leyrwite**, **gersuma/entry fine** przy objeciu gospodarstwa (od kilku s do kilku L),
- **tallage** na chlopow panszczyznianych (roczny lub doraznie, kilka s z gospodarstwa),
- **kary sadu dworskiego** (amercements), **mlyn i piec** (banalites: 1/16-1/24 ziarna), **myto i targowe**.
Wszystko placili **chlopi z wlasnej produkcji / gotowki** - przeplyw: kiesa gospodarstwa -> pan.

Dodatkowo u magnatow: wardship i relief od wlasnych rycerzy (male), **renty/annuities od korony** (w XIV w. znaczne dla dworskich),
**zold i okupy z wojny** (wojna stuletnia - glowne zrodlo bogacenia sie czesci szlachty), dlugi u kupcow.

### 1.4 Zachowanie pieniadza - kto tracil

| Przeplyw | Z czyjej kieszeni | Do kogo |
|---|---|---|
| Czynsz, heriot, merchet, tallage, mlyn, sad dworski | chlop (kiesa gospodarstwa, produkcja) | pan |
| Myto, targowe, oplaty bramne | kupiec, wiesniak przy sprzedazy | pan miasta / miasto |
| Fee farm | kasa miasta (zebrane z oplat mieszczan) | korona (stala kwota) |
| Clo welniane | eksporter -> w cenie: hodowca welny (pan i chlop) | korona (czesto wprost bankier) |
| Pietnastka/dziesiecina | ruchomosci chlopow i mieszczan | korona |
| Dziesiecina duchowna | dochod beneficjow | korona (lub papiez) |
| Relief, wardship, aids, scutage | dochod rodu pana | suzeren (krol) |
| Psucie monety | posiadacze monety, wierzyciele | mennica / krol |
| Pozyczki | bankier teraz, cla pozniej | krol (dlug) |
| Okup, lup | pokonany i jego poddani | zwyciezca |

Wniosek dla gry: **nic z tego nie bylo "z niczego"** - kazdy pens mial placacego. Podatek wojenny placili poddani, nie panowie
z wlasnego dochodu; cla placil handel; korona brala od wasali glownie sluzbe i rzadkie oplaty feudalne.

---

## 2. (B) Gra: kazde zrodlo zlota zwiazane z politykami, prawami i dekretami

Legenda kolumny "Bilans": **Z** = zachowany (ktos placi), **N** = z niczego, **U** = ujscie w nicosc, **0** = po wpisie 49 nic nie daje.

### 2.1 Polityki krolestwa vanilla (`Kingdom.ActivePolicies`)

Wplywy idace do rodu krola liczone w `tw/DefaultClanFinanceModel.AddRulingClanIncome` (l. 233-322), wywolane z
`CalculateClanIncomeInternal` (l. 131) - BK tego nie podmienia (BK prefiksuje tylko `AddSettlementIncome` i wydatki).

| Polityka | Efekt na zloto | Kto placi -> kto dostaje | Bilans | Skala |
|---|---|---|---|---|
| **Crown Duty** | (a) prowizja miasta od sprzedazy x1.05 (`DefaultSettlementTaxModel.GetTownTaxRatio`, l. 29-36; w BK baza 0.1 -> 0.105); (b) 5% `Town.TradeTaxAccumulated` **kazdego lenna krolestwa** do rodu krola, odjete z licznika (l. 263-273) | kasa miasta (prowizja od sprzedazy) -> licznik cel -> krol | **Z** | 5%/dzien licznika miast krolestwa - zalezy od handlu |
| **War Tax** | 5% sumy `CalculateTownTax` lenn krolestwa do krola (l. 253-262); -1 dobrobytu miast (`DefaultSettlementProsperityModel` ~l. 194) | z niczego | **N -> 0** po wpisie 49 (CalculateTownTax = 0) | dawniej ~5% z ~0.48 mln/dzien swiata; dzis 0, zostala kara dobrobytu |
| **Land Tax** | krol: 5% z (licznik wsi / 5) kazdej wsi wasali (l. 237-251) - **bez odjecia z licznika**; wasal: -5% dochodu wsi (`bk/BKTaxModel.cs:284-288`) | czesciowo: wasal traci 5% (zostaje w liczniku), krol liczy od licznika jeszcze raz | **N/Z** (podwojnie liczone, male) | ~1% licznika wsi dziennie |
| **Road Tolls** | licznik/30 z **wlasnych** miast rodu krola do krola, odjete z licznika (l. 288-303) | kasa miasta -> licznik -> krol | **Z** | tylko przyspiesza wyplate licznika wlasnych miast krola |
| **State Monopolies** | 5% `Workshop.ProfitMade` warsztatow w miastach rodu krola (l. 297-307) - zysk NIE pomniejszony | z niczego | **N** | maly-sredni (warsztaty Armoury robia zysk w setkach-tysiacach) |
| **Debasement of the Currency** | **100 d x liczba lenn krolestwa dziennie** do krola (l. 275-279); -1 lojalnosci (`DefaultSettlementLoyaltyModel` l. 249-251) | z niczego | **N** | krolestwo z 10 lennami = 1 000 d/dzien |
| Magistrates, Bailiffs, Tribunes of the People (-5% podatku miasta), Cantons (-10%; startowa w 19 krolestwach ROT), Council of the Commons (-5% bazy) | obnizaja `CalculateTownTax` (`DefaultSettlementTaxModel` l. 64-75, 152-177) | - | **0** po wpisie 49 | brak skutku zlota |
| Land Grants for Veterans | -5% udzialu wsi (vanilla `GetVillageTaxRatio`); **BK ten model podmienia** (`CalculateVillageTaxFromIncome` z polityka BK) | - | prawdopodobnie 0 | |
| Military Coronae | mnoznik zoldu partii (`DefaultPartyWageModel` l. 149-177) | wydatek rodu -> nicosc | U (wydatek) | |
| Pozostale (Serfdom, Senate, Lords Privy Council, Noble Retinues, Royal Guard, Sacred Majesty, Citizenship, Hunting/Grazing Rights, Imperial Towns, Forgiveness of Debts, Lawspeakers, Precarial Land Tenure...) | wplyw, lojalnosc, dobrobyt, milicja, rozmiar partii | - | brak zlota | |

Inne kanaly krolestwa vanilla (nie polityki, dla pelnosci):
- **Wsparcie z budzetu** (`AddIncomeFromKingdomBudget`, l. 517-528): rod < 30 000 dostaje 500-2000 d/dzien z `KingdomBudgetWallet` (odjete) - **Z**.
- **Wplata do budzetu**: vanilla 1% zlota ponad 100 tys., BK podmienia na **0.1%** (`bk/EconomyPatches.cs` ~l. 211-219) - **Z**.
- **Najemnicy** (`MercenaryWallet`) i **trybut** (`TributeWallet`) - dzielone na rody wedlug `CalculateShareFactor` - **Z**.
- **Umowy handlowe vanilla**: kazda wizyta karawany obcego krolestwa w miescie partnera = **+500 d** (`DefaultTradeAgreementModel.GetProfitPerCaravanVisit`, l. 354-357),
  dzielone po rowno na rody (`AddIncomeFromTradeAgreements`, l. 587-626) - **N** ("clo" bez placacego).
- **Rod bez lenna** (poza krolestwem albo najemnik): Tier x 80 (najemnik x 120) d/dzien (`CalculateClanIncomeInternal` l. 133-137) - **N**.
- Skarbiec krolestwa: dosypki 1000 d/dzien i losowe 100-400 tys. - **wylaczone** naszym transpilerem (`Armoury/src/KingdomTreasury.cs:21-35`).

### 2.2 Dochod z osad (podstawa, na ktorej dzialaja polityki)

| Kanal | Mechanika | Bilans |
|---|---|---|
| **Prowizja miasta od sprzedazy** | gdy miasto sprzedaje towar, oddaje z utargu `GetTownTaxRatio` (BK: 0.1 x [1.05 CrownDuty] +/-0.04 wg polityki, Exemption/dekret `decision_tariff_exempt` = 0) do `Town.TradeTaxAccumulated` (`tw/SellItemsAction.cs:69-92`; niska bezpieczenstwo zjada do 10%) | **Z** (kasa miasta -> licznik) |
| **Oplata od karawany BK** | karawana wchodzaca do miasta placi `EconomicData.CaravanFee` z wlasnego zlota do licznika (`bk/BKPartyBehavior.cs:784-795`) | **Z** (myto bramne) |
| **Wyplata licznika miasta panu** | BK: licznik/5 dziennie (+perki) jako "tariff" w "Walled Demesnes" (`bk/EconomyPatches.cs:424-437`) | **Z** |
| **Podatek miasta** (`CalculateTownTax`) | vanilla 0.35 x dobrobyt + budynki + perki; BK dodaje: klasy ludnosci (szlachta 1.2, rzemieslnicy 0.3 x merkantylizm, dzierzawcy 0.12, chlopi 0.07, niewolnicy 0.1 na glowe x ustawienie BK `TaxIncome`), **podatki od cudzych warsztatow**, **taryfa od konsumpcji ludnosci**, **kopalnie**, minus **materialy projektow**, autonomia, koszty administracji (`bk/BKTaxModel.cs:164-236`) | **N** (glowna czesc) -> **0 po wpisie 49** (cala wartosc zerowana `PopulationLaw.TownTaxPostfix`) |
| **Utarg wiesniakow -> licznik wsi** | wiesniak sprzedaje w miescie (miasto placi), w domu BK bierze udzial polityki (Low 50%, Standard 70%, High 90%: `bk/BKTaxModel.cs:313-326`) - z niego czesc dla posiadlosci (estates), reszta do `Village.TradeTaxAccumulated`; z reszty utargu polowa do kasy wsi, 15% znika (`bk/EconomyPatches.cs:1086-1108`, `bk/EstateData.cs:80-104`) | **Z** (z kasy miasta), 15% U |
| **"Village Demesnes"** | pan dostaje licznik wsi x 0.8 (rezerwa BK) +/- perki, Land Tax -5%, minus koszty administracji; odjete z licznika; jesli tytul de iure ma kto inny - placone jemu (`bk/BKTaxModel.cs:238-311`, `bk/EconomyPatches.cs:447-462, 476-486`) | **Z** |
| **BK "Tax Office" we wsi** | budynek wsi: podatek szlachty i rzemieslnikow wsi x 0.33 x poziom (max 50 000) doliczany do dochodu wsi (`bk/BKTaxModel.cs:272-279, 328-340`) | **N** (nadal czynne po wpisie 49) |
| **Posiadlosci (estates) BK** | wlasciciel posiadlosci dostaje czesc udzialu wsi z utargu wiesniakow (`EstateData.AccumulateTradeTax`), formalnie `GiveGoldAction(null, owner)`, ale odjete od tego, co trafia do licznika | **Z** |
| **Renta PopulationLaw (nasza)** | wies: min(nalezna, 20% kiesy wsi), miasto: 7% kasy ponad 20 000; `GiveGoldAction.ApplyForSettlementToCharacter` (`Armoury/src/PopulationLaw.cs:183-238`) | **Z** |
| **Powinnosci wobec korony (nasze)** | 2% (pokoj) / 10% (wojna) dziennego dochodu rodu wasala (model + renta) z kiesy glowy rodu do `KingdomBudgetWallet` (`Armoury/src/KingdomTreasury.cs:45-76`) | **Z** |

### 2.3 Prawa demesne BK (umowa tytulu, `bk/DefaultDemesneLaws.cs`) - co rusza zloto

| Prawo | Efekt | Po wpisie 49 |
|---|---|---|
| Tax Duties (Nobles) x1.25 / Lax Duties (Nobles) x0.6 podatku szlachty | `bk/BKTaxModel.cs:45-70` | miasto: 0; wies: tylko przez Tax Office |
| Tax Duties (Craftsmen) x1.35 / Lax Duties (Craftsmen) x0.6 | l. 72-101 | jw. |
| Domestic Duties (Slaves) x1.15 - **bledem BK dotyczy tez dzierzawcow i chlopow** (l. 128, 148) | | 0 w miescie |
| Estate tenure: Allodial (podatek posiadlosci -100%), Fee Tail (-50% przy obowiazku "Taxation"), Quia Emptores | `bk/BKEstatesModel.cs:371-395` - ile z posiadlosci idzie do licznika wsi, ile do wlasciciela | **Z** (przesuniecie miedzy pan <-> wlasciciel posiadlosci) |
| Drafting: Free Contracts (koszt werbunku x2), Hidage (x1.5) | `bk/BKPartyWageModel.cs:396-403` - wydatek na werbunek | U (wydatek) |
| Military Duties, Agricultural Duties, Hard Labor, Slavery, Tenancy, Army laws, Council laws | milicja, ochotnicy, zywnosc, wzrost ludnosci, armie | brak zlota |

### 2.4 Polityki i dekrety osady BK (`bk/BannerKings.Managers.Policies`, `...Decisions`)

**Polityka podatkowa `"tax"` (BKTaxPolicy: Low / Standard / High / Exemption):**

| Skutek | Low | Standard | High | Exemption | Gdzie |
|---|---|---|---|---|---|
| Udzial utargu wiesniakow do licznika wsi | 50% | 70% | 90% | 70% | `bk/BKTaxModel.cs:313-326` |
| Prowizja miasta od sprzedazy (baza BK 10%) | 6% | 10% | 14% | 0% | `bk/BKTaxModel.cs:359-379` |
| Podatek klas ludnosci (miasto/Tax Office) | x0.85 | x1 | x1.15 | x1 | `bk/BKTaxModel.cs:60-161` (miasto: 0 po 49) |
| Podatek od zysku warsztatu (placi wlasciciel) | 10% | 20% | 30% | 10% | `bk/BKWorkshopModel.cs:200-235` |
| Udzial posiadlosci w podatku | +5% ... (wg polityki) | | | | `bk/BKEstatesModel.cs:387+` |
| Lojalnosc | + (rzemieslnicy + 0.8 x chlopi) x wspolczynnik | 0 | - to samo | | `bk/BKLoyaltyModel.cs:62-70, 122-137` |
| Dobrobyt / przyrost wsi | +10% | 0 | -15% | +20% | `bk/VanillaModelTweakPatches.cs:1817-1832` |
| Wydajnosc niewolnikow (z dekretem `decision_slaves_tax`) | 0.85 | 0.7 | 0.65 | | `bk/BKEconomyModel.cs:669-678` |

AI zmienia polityke podatkowa sama (`bk/BKSettlementBehavior.cs:473-481`).
**Nasza renta (PopulationLaw) polityki NIE czyta** - High daje panu wiecej licznika, ale te same 7%/20% renty.

Dekrety osady: `decision_tariff_exempt` (prowizja 0 - **Z**, mniej dla pana), `decision_mercantilism` (merkantylizm: +podatek rzemieslnikow,
-podatek warsztatow), `decision_slaves_tax`, `decision_slaves_export/sell` (niewolnicy na targ - **Z**, przez rynek),
`decision_militia_subsidize` (tylko szansa weteranow milicji, **bez kosztu w zlocie**), `decision_militia_encourage`, `decision_ration`,
`decision_foreigner_ban`, patrole/zwiadowcy - bez zlota.

### 2.5 Umowa feudalna, rada, obowiazki BK

| Element | Mechanika | Bilans |
|---|---|---|
| **"Taxes from {CLAN}" / "Taxes to {SUZERAIN}"** | suma `FeudalTitle.DueTax` wasali (`bk/BKClanFinanceModel.cs:206-218, 259-282`) | **zawsze 0** - `CalculateDueTax` pusta (`bk/BKTaxModel.cs:354-357`); obowiazek `FeudalDuties.Taxation` nigdzie nie uzyty |
| **Auxilium** (pomoc wojskowa) | tylko gracz, wplyw/relacje (`bk/AuxiliumDuty.cs`) | bez zlota |
| **Ransom aid** (okup suzerena) | tylko gracz, gracz -> suzeren (`bk/RansomDuty.cs`) | **Z** |
| **Rada (council)** | pensja = 1% / 2.75% (rdzen) / 4.5% (rdzen krolewski) podatku ostatnio liczonej osady (`bk/CouncilMember.cs:296-304`, `bk/BKTaxModel.cs:342-352`); pan placi "Council wages", czlonek-lord dostaje "Councillor role", nie-lord dostaje gotowke | **Z** (przesuniecie); **po wpisie 49 pensje nadal liczone od podatku przed zerowaniem** (CalculateDueWages wola sie wewnatrz CalculateTownTax przed naszym postfiksem) |
| **Podatek od warsztatow** | wlasciciel placi 10-30% zysku: notabl (`BKClanFinanceModel.CalculateNotableDailyGoldChange` l. 28-36), rod "Workshop taxes" (l. 232-246); pan miasta dostawal "Taxes from {WORKSHOP}" w podatku miasta (`bk/BKTaxModel.cs:198-205`) | **dawniej Z, po wpisie 49 U** (placacy placi, odbiorca dostaje 0) |
| **Kopalnie BK** | miasto kupuje rude z kopalni za pelna cene z kasy (`ChangeGold(-num)`), panu 50% w podatku miasta (`bk/BKBuildingsBehavior.cs:440-453`) | dawniej 50% U; **po wpisie 49 100% U** |
| **Materialy projektow BK** | towar zdejmowany z rynku, koszt (x1.2 z innego miasta) odliczany panu w podatku miasta (`bk/BKBuildingsBehavior.cs:350-362`, `BKTaxModel.cs:214-218`) | dawniej U (pan placil w nicosc, rynek nie dostawal); **po 49 pan nie placi** |
| Polityka krolestwa BK "Limited Army Privilege", Crown Authority, Demesne Law decision | bez zlota | |

### 2.6 ROT

ROT **nie ma wlasnych podatkow ani polityk**. Startowe polityki krolestw (`ROT-Content/ModuleData/ROT_spkingdoms.xml`):
Lawspeakers (20), **Cantons (19 - dawniej -10% podatku miasta, dzis bez skutku)**, Precarial Land Tenure (1).
Zloto ROT: zapomoga Tier x 5000 dla biednych rodow (`rot/ROTCoreBehavior.cs:44`, **wylaczona** naszym prefiksem),
inwazje Aegona/Daenerys 1 mln do osady (`rot/AegonInvasionEvent.cs:100`, `DanyInvasionEvent.cs:100`), zwrot roznicy kosztu werbunku przy
podmianie oddzialu (`rot/ROTTroopRecruiter.cs:205-212`, **N**, maly), handlarze/najemnicy/kruki - tylko gracz.

### 2.7 BetterEconomy

| Element | Stan po wpisie 49 |
|---|---|
| Wyplata nadwyzki wirtualnego skarbca miasta panu (35%, wojna 20%) - `GiveGoldAction(null, lord)` (`be/TownEconomyCampaignBehavior.cs:1970-1995`) | **0** (`TownTreasurySurplusPayoutFraction`/`TownTreasuryWarPayoutFraction` = 0) |
| Cla traktatow handlowych BEE (`be/TradeAgreementCampaignBehavior.cs:405-421`) | **0** (`TradeAgreementCustomsRate` = 0) |
| Renta posiadlosci BEE (`be/FeudalEconomyCampaignBehavior.cs:955-972`) | **0** (`EstateOwnerPayoutFraction` = 0) |
| Regulator bogactwa (`be/WealthAuditCampaignBehavior.cs`) | **wylaczony** (`LordWealthRealism=0`) |
| Wirtualne skarbce miast/zamkow BEE (`LocalTreasuryGold`) - liczone z "migawki" gospodarki, nie z `Town.Gold` | rosna z niczego, ale juz nie wyplacaja; **wplaty AI pana do skarbca zamku i na zbrojownie** (`be/CastleEconomyCampaignBehavior.cs:343-361`, `be/TownEconomyCampaignBehavior.cs:2081-2140`) = **U** (prawdziwe zloto -> liczba wirtualna) |

### 2.8 Co zmienil wpis 49 - bilans

**Zniknelo (z niczego):** podatek miasta vanilla (0.35 x dobrobyt), podatek klas ludnosci BK, taryfa od konsumpcji ludnosci BK,
budynki "Tax per day", premie gubernatora - lacznie ~0.36-0.48 mln d/dzien swiata (audyt). **War Tax** razem z nim.
**Zastapione:** renta z kasy miasta (7% ponad 20 000) - **Z**.
**Nowe ujscia (skutek uboczny, do naprawy):** podatek od warsztatow (placony, nie odbierany), 50% oplaty za rude z kopalni.
**Zniknely wydatki:** materialy projektow BK. **Bez sensu, ale male:** pensje rady liczone od nieistniejacego podatku.
**Zostalo z niczego (zwiazane z prawem/polityka):** Debasement (100 d/lenno), State Monopolies (5% zysku), Land Tax (podwojne liczenie),
umowy handlowe vanilli (500 d/karawane), Tax Office we wsi BK, zold rodu bez lenna (Tier x 80). Spoza polityk (patrz audyt): dosypka
kasy miasta vanilli (Z2), BK zaplata za konsumpcje (Z3), notable (Z4), jency (Z7), startowe zloto.

---

## 3. (C) Propozycja: mapowanie historyczne

Zasada: **kazdy dochod krola i pana ma placacego w grze**. Zrodla "z ziemi" placi kasa osady (renta), "z handlu" licznik cel
(prowizja miasta + myto karawan), "wojenne" kasy wszystkich osad krolestwa, "feudalne" kiesy rodow wasali. Kolejnosc = priorytet,
**jedna zmiana = jeden DLL = jeden test** (CLAUDE.md 8.2).

### Krok 0 - pomiar (bez wplywu na gre)
`CrashScribe/src/EconomyAudit.cs` (linia `EKONOMIA`): dla kazdego krolestwa wypisac `ActivePolicies` (id), `KingdomBudgetWallet`,
sume `TradeTaxAccumulated` miast i wsi; dla rodu krola rozbicie `CalculateClanIncome(..., includeDetails:true)` linii "str_policies".
Bez tego nie wiemy, ile dzis daja Debasement/Monopolies/CrownDuty. Do tego w `PopulationLaw.Daily` podzial rent na miasta i wsie.

### Docelowe zrodla i skala (krolestwo ~1.8 mln ludzi, rok 364 dni)

| Historyczne | W grze (docelowo) | Placi -> dostaje | Skala docelowa |
|---|---|---|---|
| Domena + czynsze + banalia pana | renta PopulationLaw + "Village Demesnes" + licznik cel wlasnych miast | kasa osady / licznik -> pan lenna | pan: ~5 d/glowe/rok lacznie z krolem (audyt); hrabia-dom ROT 1-6 tys. L/rok |
| Domena korony + fee farm | to samo dla lenn rodu krola | jw. -> krol | ~1 d/glowe/rok = ~5 tys. d/dzien |
| Cla (custom 1275, subsydium welniane) | **Crown Duty** = udzial licznika cel WSZYSTKICH miast krolestwa do skarbca | licznik cel (kasa miasta) -> `KingdomBudgetWallet` | 0.5-0.7 d/glowe/rok = ~2.5-3.5 tys. d/dzien w pokoju |
| Pietnastka i dziesiecina (wojna) | **War Tax** = danina z kas osad w czasie wojny | kasy miast (1/10) i wsi (1/15) -> skarbiec | ~2 d/glowe na rok wojny = ~10 tys. d/dzien rozlozone na rok |
| Aids, relief, scutage | nasze `CrownDues` pokojowe 2% (aids) + relief przy dziedziczeniu (opcja) | kiesa rodu wasala -> skarbiec | male: 1-3% dochodu rodu |
| Mennica | **Debasement** = oplata menniczna z kas miast, nie z niczego | kasy miast -> krol | Anglia: male; Francja 1295-1305: duze, kosztem lojalnosci |
| Monopole / dzierzawy | **State Monopolies** z kapitalu warsztatow | kapital warsztatu -> krol | 5% zysku jak dzis, ale odjete |
| Dziesiecina duchowna | brak kieszeni duchowienstwa w gospodarce gry - **nie robic** | | |
| Pozyczki | Bank Zelazny (`IronBank.cs`) - istnieje | | |
| Lupy/okupy | poza zakresem (audyt Z7/Z8) | | |

### Zmiany w kodzie (konkretnie)

**C1. Naprawa skutkow ubocznych wpisu 49 (najpierw - naprawia nowe ujscia).**
`Armoury/src/PopulationLaw.cs:141-150` (`TownTaxPostfix`): zamiast `new ExplainedNumber(0f, ...)` zostawic w wyniku tylko przeplywy zachowane:
suma `BannerKingsConfig.Instance.ClanFinanceModel.GetWorkshopTaxes(w)` dla warsztatow miasta z wlascicielem innym niz pan
(jak `bk/BKTaxModel.cs:198-205`) + `BKBuildingsBehavior.GetMiningRevenue(town)` (l. 209-213) - przez refleksje, typy BK
(`BannerKings.BannerKingsConfig`, `BannerKings.Behaviours.BKBuildingsBehavior`). Materialy projektow (`GetMaterialExpenses`) -
decyzja Jeffa: albo odliczac (pan placi, ale w nicosc - jak dawniej), albo przekazac te kwote do kasy miasta, z ktorego zdjeto towar
(to bylby zakup - **Z**; wymaga latki w `BKBuildingsBehavior` l. 350-362, wiec pozniej).
Ryzyko: minimalne, przywraca dwa przeplywy do stanu sprzed 49.

**C2. Poziom polityki podatkowej BK = mnoznik renty.**
`Armoury/src/PopulationLaw.cs:215-221`: `takeShare *= mnoznik(polityka)`; polityka przez refleksje:
`BannerKingsConfig.Instance.PolicyManager.GetPolicy(st, "tax")` -> pole/wlasciwosc `Policy` (enum `BKTaxPolicy.TaxType`).
Nowe ustawienia (`Settings.cs` przy l. 418-424 + `tools/gen_mcm.py`): `RentLowFactor` 0.7, `RentHighFactor` 1.3, `RentExemptFactor` 0.
Historycznie: wyzsza renta/tallage = mniej lojalnosci - BK juz to robi (`BKLoyaltyModel.cs:122-137`), wiec to jedyna potrzebna zmiana.
Uwaga: polityka juz skaluje licznik wsi (50/70/90%) - razem efekt jest mocniejszy; ewentualnie mnoznik tylko dla miast.

**C3. War Tax = lay subsidy z kas osad (zamiast 10% od dochodu panow).**
`Armoury/src/KingdomTreasury.cs:45-76` (`Daily`): gdy krolestwo jest w wojnie (i opcjonalnie ma `DefaultPolicies.WarTax` w `ActivePolicies` -
wtedy krol musi ja przeglosowac, jak parlament), pobrac z kazdej osady krolestwa (nie spladrowanej, nie oblezonej):
miasto `LaySubsidyTownShare` (np. 1%/dzien kasy ponad `TownRentFloorGold`), wies `LaySubsidyVillageShare` (np. 1.5%/dzien kiesy) -
`st.SettlementComponent.ChangeGold(-x)`, `kingdom.KingdomBudgetWallet += x`. Jednoczesnie `CrownDuesWarPercent` (`Settings.cs:476`) 10 -> 2-3
(tarczowe/aids od panow zostaje male). Placa poddani, nie panowie - jak 1290/1334.
Skala: kasy 97 miast swiata ~6.5 mln przy celu; 1% nadwyzki dziennie to dziesiatki tysiecy na swiat - **za malo wobec ~10 tys. d/dzien
na krolestwo**; po kroku 0 dobrac stawke albo uznac, ze krol wojuje ze skarbca 2 mln + kredytu (historycznie tez: dlug).
Ryzyko: kasa miasta ponizej 20 000 = BK zabiera dobrobyt - dlatego tylko nadwyzka ponad prog.

**C4. Crown Duty = clo do skarbca krolestwa.**
W `KingdomTreasury.Daily`: jesli `kingdom.ActivePolicies.Contains(DefaultPolicies.CrownDuty)` - z kazdego miasta krolestwa
`CrownCustomsShare` (np. 0.15) x `Town.TradeTaxAccumulated` -> `KingdomBudgetWallet`, odjac z licznika. Vanilla 5% do rodu krola
(`tw/DefaultClanFinanceModel.cs:263-273`) mozna zostawic (krol jako pan) albo wyzerowac transpilerem stalej 0.05 w `AddRulingClanIncome`.
Lepsze odwzorowanie (clo od obcych kupcow): pobierac od karawan obcych krolestw przy wejsciu do miasta (jak BK `AddCaravanFees`,
`bk/BKPartyBehavior.cs:784-795`) - pozniej.
Bez Crown Duty wolno rozwazyc to samo jako stale "clo" kazdej korony (Anglia miala clo od 1275 zawsze).

**C5. Umowy handlowe vanilla: 500 d z niczego -> 0 albo z karawany.**
Postfix na `DefaultTradeAgreementModel.GetProfitPerCaravanVisit` (`tw/DefaultTradeAgreementModel.cs:354-357`) -> 0, albo prefix na
`TradeAgreementsCampaignBehavior.SettlementEntered` (l. 155-161) pobierajacy kwote z `party.PartyTradeGold`. Najpierw sprawdzic w kroku 0,
czy w ogole sa umowy (BEE ma wlasne - wyzerowane).

**C6. Debasement of the Currency: oplata menniczna z kas miast.**
Transpiler na `DefaultClanFinanceModel.AddRulingClanIncome` (l. 275-279): stala 100 -> 0 (jak `KingdomTreasury.Transpiler` - tylko w tej
metodzie i tylko `ldc.i4.s 100` po `get_Count` - sprawdzic IL przed latka, w tej metodzie sa tez inne stale). W `KingdomTreasury.Daily`:
przy aktywnej polityce `DebasementSeigniorageShare` (np. 0.5%/dzien kasy kazdego miasta krolestwa) -> skarbiec. Kara -1 lojalnosci zostaje (vanilla).
Historycznie Filip IV - duzy dochod, utrata wartosci monety o 2/3; w grze nie mamy inflacji, wiec tylko udzial kasy + lojalnosc.

**C7. State Monopolies z kapitalu warsztatow.**
Ta sama metoda (l. 297-307): transpiler stalej 0.05 -> 0; w `Daily` dla miast rodu krola `w.ChangeGold(-x)` (kapital warsztatu) i krolowi
`x = 0.05 x ProfitMade` (ograniczone kapitalem). Male - mozna odlozyc.

**C8. Land Tax bez podwojnego liczenia.**
Albo transpiler (l. 237-251) i w `Daily` pobranie 5% licznika wsi wasali z `Village.TradeTaxAccumulated` do krola (odjete), albo zostawic
(male, ~1% licznika dziennie). Niski priorytet.

**C9. BK "Tax Office" we wsi - wyzerowac podatek klas ludnosci wsi.**
Prefix `return false` na `BannerKings.Models.Vanilla.BKTaxModel.AddVillagePopulationTaxes` (`bk/BKTaxModel.cs:328-340`), za tym samym
przelacznikiem co `RentReplacesTownTax` (renta z ludnosci juz jest). Najpierw zmierzyc (ile wsi ma Tax Office - CrashScribe).

**C10. Pensje rady od renty.**
Po wpisie 49 `CouncilMember.DueWage` liczy sie od podatku sprzed zerowania. Zmiana: postfix na `BKTaxModel.CalculateTownTax` z
`Priority.Last` jest juz nasz - po wyzerowaniu przeliczyc `DueWage = renta_dzisiaj_z_osady x AdministrativeCosts()`. Male (1-4.5%), niski priorytet.

**C11. (opcja) Relief przy dziedziczeniu.** Zdarzenie zmiany glowy rodu (`CampaignEvents.OnClanLeaderChangedEvent`): nowy lider placi do
`KingdomBudgetWallet` relief = np. 100 L x Tier/6 (Magna Carta: baron 100 L) - **Z**. Historycznie istotne dla korony XIII w., w XIV w. malejace.

**Czego nie ruszac:** Crown Duty vanilla jest juz zachowane; Road Tolls zachowane; budzet krolestwa i wsparcie biednych rodow zachowane;
startowe 2 mln skarbca (zapas na wojne - "kredyt"). Dziesiecina duchowna - brak kieszeni Kosciola w gospodarce gry.

### Kolejnosc proponowana
0 (pomiar) -> C1 (naprawa 49) -> C3 (war tax z osad, `CrownDuesWarPercent` w dol) -> C4 (clo) -> C2 (mnoznik renty) -> C6/C5/C9 -> reszta.

---

## 4. Zrodla

Historia (wyniki wyszukiwania 04.10.2026):
- Clo 1275 ~10 000 L/rok, Riccardi pobierali cla, bankructwo 1294:
  [Cambridge: Credit Finance in Thirteenth-Century England - Ricciardi and Edward I](https://www.cambridge.org/core/books/abs/thirteenth-century-england-xiii/credit-finance-in-thirteenthcentury-england-the-ricciardi-of-lucca-and-edward-i-127294/5F47BA4445934D72CFB57BAF2A0E625F),
  [Britannica: Edward I](https://www.britannica.com/place/United-Kingdom/Edward-I-1272-1307),
  [CEPR VoxEU: The credit crunch of 1294](https://cepr.org/voxeu/columns/credit-crunch-1294-causes-consequences-and-aftermath)
- Subsydium 1290 = 116 000 L w zamian za wypedzenie Zydow: [History Skills](https://www.historyskills.com/classroom/modern-history/jews-england-expulsion-1290/),
  [Oxford History Faculty](https://www.history.ox.ac.uk/why-were-the-jews-expelled-from-england-in-1290-0)
- Pietnastka i dziesiecina, 1334 kwoty stale: [Wikipedia: Fifteenth and tenth](https://en.wikipedia.org/wiki/Fifteenth_and_tenth),
  [FamilySearch: Tenths and Fifteenths 1334-1623](https://www.familysearch.org/en/wiki/England_Taxation_Tenths_and_Fifteenths_1334_to_1623_-_International_Institute),
  [medievalgenealogy.org.uk: Subsidies](http://www.medievalgenealogy.org.uk/guide/tax.shtml)
- Dochod zwykly 1337 ~30 000 L (cla 13 000); Bardi 135 000 / Peruzzi 90 000 L (Villani):
  [The Hundred Years War: financing](https://thehundredyearswar.co.uk/how-did-edward-iii-finance-the-conflict-with-france-in-its-initial-stages/),
  [BHO: Bardi and Peruzzi and Edward III](https://www.british-history.ac.uk/manchester-uni/london-lay-subsidy/1332/pp93-135),
  [BHO: Taxation of wool 1327-1348](https://www.british-history.ac.uk/manchester-uni/london-lay-subsidy/1332/pp137-177)
- Cla welniane >112 000 L w 1353-54, 40 s/worek od 1337, Ormrod:
  [Cambridge LHR: Wool Smuggling c.1337-63](https://www.cambridge.org/core/journals/law-and-history-review/article/wool-smuggling-and-the-royal-government-in-england-c133763-law-enforcement-and-the-moral-economy-in-the-late-middle-ages/FFA1B0757290B2C00E34FD3013066ECA),
  [Wikipedia: Maltolt](https://en.wikipedia.org/wiki/Maltolt)
- Incydenty feudalne, scutage przestarzale w XIV w., 91 miast z farma do 1348, 75-80% dochodu korony w XII w.:
  [Wikipedia: History of the English fiscal system](https://en.wikipedia.org/wiki/History_of_the_English_fiscal_system),
  [Britannica: Scutage](https://www.britannica.com/topic/scutage), [IPM: feudal system and royal prerogative](https://inquisitionspostmortem.ac.uk/the-feudal-system-and-the-royal-prerogative/)
- Zydzi: srednio ~2 000 L/rok w XIII w., 1241 20 000 marek: [Koyama: regulation of Jewish moneylending](https://mason.gmu.edu/~mkoyama2/About_files/Koyama10c.pdf),
  [Wikipedia: Exchequer of the Jews](https://en.wikipedia.org/wiki/Exchequer_of_the_Jews)
- Lancaster ~11 000 L, Warwick ~6 000 L, Gloucester ~6 000 L:
  [Wikipedia: List of earls in the reign of Edward II](https://en.wikipedia.org/wiki/List_of_earls_in_the_reign_of_Edward_II_of_England),
  [Wikipedia: Honour of Clare](https://en.wikipedia.org/wiki/Honour_of_Clare)
- Struktura dochodu pana (domena ~45%, czynsze ~47%, banalia ~10%; mlyny >=5%; panowie 18-23% netto z ziemi):
  [Past & Present: Seigneurial Lordship in Flanders c.1250-1570](https://academic.oup.com/past/article/267/1/3/7716080),
  [Past & Present: Lordship and peasant consumerism in the milling industry](https://academic.oup.com/past/issue-pdf/145/1/4208311)
- Francja: psucie monety 1295-1305, -2/3 wartosci do 1306, kler 1/2 dochodu:
  [Wikipedia: Coinage of Philip IV](https://en.wikipedia.org/wiki/Coinage_of_Philip_IV_of_France),
  [Tontine Coffee-House: Philip IV the Counterfeiter King](https://tontinecoffeehouse.com/2020/11/30/philip-iv-the-counterfeiter-king/)
- Okup Jana II 3 mln ecu (Bretigny 1360): [Wikipedia: Ransom of John II](https://en.wikipedia.org/wiki/Ransom_of_John_II_of_France),
  [Wikipedia: Treaty of Bretigny](https://en.wikipedia.org/wiki/Treaty_of_Br%C3%A9tigny)

Liczby oznaczone [SZ] (1275 ~81 000 L, 1334 ~38 000 L, dziesiecina duchowna ~18-20 tys. L, aides 1/20, farma Londynu, oplaty dworskie)
- z literatury (Ormrod, Prestwich, Dyer, Kaeuper), nie zweryfikowane w tej sesji.

Kod: dekompilacje w scratchpadzie sesji 2016d5c4 (`bk/`, `be/`, `van/`) i sesji 20.09 (`tw/`, `rot/`); repo `Armoury/src/PopulationLaw.cs`,
`Armoury/src/KingdomTreasury.cs`, `Armoury/src/Settings.cs:418-424, 474-476`.
