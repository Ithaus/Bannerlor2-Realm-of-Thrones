> SPRAWDZENIE 07.10 (kod + proba na prawdziwych DLL + logi): kara handlowa BK x5 liczona DWA razy (x25; zamek x225), bo postfiks BK siedzi na
> DefaultTradeItemPriceFactorModel i na BKROTPriceModel, ktory wola base; podlogi Armoury licza sie od wartosci bazowej (stan bez wplywu). NIC NIE ZAKODOWANE.
> Decyzje Jeffa - patrz STAN-PRAC.

## Dla Jeffa

Sprawdziłem: znalezisko jest prawdziwe. Kara BannerKings za sprzedaż broni i zbroi liczy się dwa razy (×25 zamiast ×5), a cena minimalna liczy się od ceny nowej rzeczy. Dlatego dziś za Brigandine dostajesz 115 zł, czy jest cała, czy to wrak. Po samej paczce 127 byłoby 46 zł za każdą.

Poprawka ma trzy części:
1. Kara BK liczy się raz (×5), tak jak zaplanowali autorzy BK. Nie wracam do kary samej gry. Wtedy zbroja szłaby za 20–30% wartości, a „kup wrak, odnów, sprzedaj” dawałoby zysk w każdym mieście.
2. Cena minimalna 2% liczy się od wartości rzeczy w jej stanie. Za wrak dostaniesz grosze, za czystą i lepszą rzecz więcej.
3. Kupiec nie da za żadną sztukę więcej, niż sam bierze za jej wrak. Odnawianie wraków kupionych w tym samym mieście zawsze traci.

Ile dostaniesz przy zwykłej półce (w nawiasie przy pustej):
- Brigandine: czysta 123 (492), Battered 30 (120), wrak 12 (48)
- hełm: 16 / 4 / 1
- miecz: 6 / 1 / 1
- buty: 2 / 1 / 1

Legendarny Brigandine da ok. 615 zł zamiast 130. Handel i perki znów podnoszą cenę: Brigandine przy Handlu 300 to 230 zł.

Za zwykły, zużyty łup dostaniesz ok. 40% tego, co dziś, ale tyle co po 127 albo więcej. Złota z niczego nie ma, bo płaci kasa miasta. Zarobić można tylko, wożąc rzeczy tam, gdzie ich brakuje, i naprawiając je za prawdziwy materiał i pracę.

## Tabela

Złote; Handel 0, bez perków, zwykła półka gry (bpf 0.8). Pierwsza liczba to zwykła półka (x1), w nawiasie pusta (x4).

| Rzecz (wartość) | Stan | Dziś | Po 127 | Po poprawce |
|---|---|---|---|---|
| Miecz t5 (104) | czysta | 5 (20) | 2 (4) | 6 (24) |
| | Battered | 5 (20) | 2 (4) | 1 (4) |
| | wrak | 5 (20) | 2 (4) | 1 (2) |
| Buty t2 (28) | czysta | 1 (4) | 1 (4) | 2 (8) |
| | Battered | 1 (4) | 1 (4) | 1 (2) |
| | wrak | 1 (4) | 1 (4) | 1 (1) |
| Hełm t6 (314) | czysta | 15 (60) | 6 (12) | 16 (64) |
| | Battered | 15 (60) | 6 (6) | 4 (16) |
| | wrak | 15 (60) | 6 (6) | 1 (4) |
| Brigandine t6 (2316) | czysta | 115 (460) | 46 (104) | 123 (492) |
| | Battered | 115 (460) | 46 (46) | 30 (120) |
| | wrak | 115 (460) | 46 (46) | 12 (48) |

Po poprawce, dodatkowo:
- Zawalona półka (x0.25): Brigandine 46 / 11 / 4, hełm 6 / 1 / 1.
- Handel 300 + perki, półka x1: Brigandine 230 / 57 / 23, hełm 31, miecz 11. Dziś to samo daje 115 / 115 / 115, 15 i 5.
- Legendarny Brigandine: 615 (x4: 2460); dziś i po 127: 130.
- Lordly hełm: 41 (dziś 15, po 127: 8).
- Zamek, czysty Brigandine: 46.
- Łup (Plundered + Damaged + Battered tej samej rzeczy), Brigandine: przy półce x1 dziś 345, po 127: 138, po poprawce 146; przy półce x2: 690 / 138 / 292.

## Decyzje

1. Wgrać razem z paczką 127 (zalecam). Sama 127 da 2% za wszystko: Brigandine 46 zł w każdym stanie, legendarny ok. 130 zł.
2. Sufit „kupiec nie da więcej niż za wrak tej rzeczy” (zalecam TAK). Bez sufitu gracz z Handlem 300, perkami, stylem Gladiatora i błogosławieństwem zarabia do ok. 200 zł na każdym wraku Brigandine, który odnowi we własnej kuźni i sprzeda w tym samym mieście. Z sufitem Handel podnosi cenę broni najwyżej do 1/10 ceny nowej sztuki.
3. Handel odnowionym sprzętem między miastami (zalecam ZOSTAWIĆ). Kupujesz wraki tam, gdzie zalegają, odnawiasz je i sprzedajesz tam, gdzie ich brakuje. Pierwszy Brigandine daje do ok. +390 zł, każdy następny mniej. Płaci miasto, któremu brakuje, a sztuka i praca są prawdziwe. Druga możliwość: przyciąć ten zarobek.

---

# PROJEKT: cena sprzedazy broni i zbroi (07.10; NIC NIE ZAKODOWANE, NIC NIE WGRANE, NIC NIE ZACOMMITOWANE)

Zrodla: sprawdzenie kodu (kod\, proba\wynik_*.txt), sprawdzenie logow (logi\), decyzje Jeffa (docs/STAN-PRAC.md: paczka 127
"cena minimalna zalezna od podazy i popytu za rupiecie, wiadomo, ze duzo sie nie dostanie"; zamknieta ekonomia; "zero darmowej kasy";
koszt naprawy = robocizna stosu n131b, projekt kosztu naprawy NIE do wdrozenia). Rachunek: projekt_liczby.py (ten katalog).

## Dla Jeffa

Sprawdziłem: znalezisko jest prawdziwe. Kara BannerKings za sprzedaż broni i zbroi liczy się dwa razy (×25 zamiast ×5), a cena minimalna liczy się od ceny nowej rzeczy. Dlatego dziś za Brigandine dostajesz 115 zł, czy jest cała, czy to wrak. Po samej paczce 127 byłoby 46 zł za każdą.

Poprawka ma trzy części:
1. Kara BK liczy się raz (×5), tak jak zaplanowali autorzy BK. Nie wracam do kary samej gry. Wtedy zbroja szłaby za 20–30% wartości, a „kup wrak, odnów, sprzedaj” dawałoby zysk w każdym mieście.
2. Cena minimalna 2% liczy się od wartości rzeczy w jej stanie. Za wrak dostaniesz grosze, za czystą i lepszą rzecz więcej.
3. Kupiec nie da za żadną sztukę więcej, niż sam bierze za jej wrak. Odnawianie wraków kupionych w tym samym mieście zawsze traci.

Ile dostaniesz przy zwykłej półce (w nawiasie przy pustej):
- Brigandine: czysta 123 (492), Battered 30 (120), wrak 12 (48)
- hełm: 16 / 4 / 1
- miecz: 6 / 1 / 1
- buty: 2 / 1 / 1

Legendarny Brigandine da ok. 615 zł zamiast 130. Handel i perki znów podnoszą cenę: Brigandine przy Handlu 300 to 230 zł.

Za zwykły, zużyty łup dostaniesz ok. 40% tego, co dziś, ale tyle co po 127 albo więcej. Złota z niczego nie ma, bo płaci kasa miasta. Zarobić można tylko, wożąc rzeczy tam, gdzie ich brakuje, i naprawiając je za prawdziwy materiał i pracę.

## 1. Werdykt znaleziska

PRAWDZIWE w mechanizmie, z czterema poprawkami liczb (sprawdzenie kodu + logow, oba niezalezne):
- x25 jest prawdziwe: postfiks BK `GetTradePenaltyPostfix` (x5 bron/zbroja/siodla, zamek x3, Gladiator x0.8) siedzi na
  `DefaultTradeItemPriceFactorModel.GetTradePenalty` i na `BKROTPriceModel.GetTradePenalty`, a ten wola `base` -> dziala dwa razy
  (proba na prawdziwych DLL: x25.0 dla kazdego sprzetu; log 05.10: kupno x2.000 w 118/167 liniach = 0.8 x (1 + 0.06 x 25); diagnostyka
  warsztatow 15:31: 63/63 par w zakresie x25, 0/63 w zakresie x5). Zamek dzis: x225.
- Podloga 5% liczy sie od wartosci BAZOWEJ (ScrapFloor, MarketGlut, SupplyDemand: `item.Value`), stan nie ma wplywu na cene sprzedazy.
- Poprawki do krytyka: "60 = 4.9%" to podloga, nie lancuch (lancuch ok. 1%); gracz dostaje dzis 5% x max(1, polka x surowce)
  (logi: 4-15%, mediana 7.6%), nie stale 5%; Handel i perki dzis NIE zmieniaja ceny sprzetu (lancuch 2-5% < podloga); arbitraz wrakow
  jest dzis wiekszy niz pisze krytyk: +51 zl w jednym miescie (Brigandine, polka x0.25, bez zadnej pracy), +396 miedzy miastami.
- Po samej 127 (MinSell 2, OneScrapFloor): 2% wartosci bazowej prawie wszedzie i w kazdym stanie (Brigandine 46), bo lancuch x25
  (ok. 1% czystej, 0.1% wraku) jest pod podloga; intencja 127 "stan x polka, nie mniej niz 2%" nie dziala bez naprawy x25.
- Realnych zarobkow Jeffa z lupu nie da sie zmierzyc (brak wpisu na kazda sprzedaz) - patrz punkt 6 (log).

## 2. Poprawka A: jedna kara BK (x5), nie kara gry

DECYZJA PROJEKTU: zostaje JEDNA x5 (tak, jak zamierzyl BK). Uzasadnienie (decyzje Jeffa):
- "malo oplacalna": x5 daje za czysta sztuke 5.3% (t6) .. 9.1% (t1) jej wartosci przy zwyklej polce gry (bpf 0.8), x polka podazy;
  kara gry (x1) dalaby 21-31%, a przy pustej polce x4 do 84-124% wartosci - sprzedaz lupu stalaby sie glownym zrodlem zlota.
- "nie grosze za legendarna": x5 na wartosci ze stanem i jakoscia daje Legendary Brigandine (x5) ok. 615 zl przy zwyklej polce
  (2460 przy pustej); dzis 130, po 127 130.
- szczelnosc: przy x5 i Handlu 0 czysta sztuka idzie za mniej (<= 9.1% wartosci) niz sklep bierze za jej wrak (11%) - petla
  "kup wrak, odnow, sprzedaj" w jednym miescie traci zawsze. Przy karze gry (21-31%) zarabialaby w kazdym miescie (Brigandine +196 zl
  na wraku przy kowadle, polka x1), a sufit z punktu B3 wiazalby wszedzie - cena przestalaby zalezec od stanu powyzej ~50%.
- najmniej ingerencji w cudzy mod: usuwamy tylko DUBEL, BK zostaje w calosci (zamek x3, Gladiator, perki BK dzialaja raz).

Jak (do zakodowania): nowy krok w `SubModuleMain.OnBeforeInitialModuleScreenSetAsRoot` (tam juz wpinamy ScrapFloor/MarketGlut/
SupplyDemand; BK instaluje postfiks w swoim OnSubModuleLoad, Main.cs:245 - czyli wczesniej):
1. dla kazdej nieabstrakcyjnej podklasy `DefaultTradeItemPriceFactorModel`, ktora SAMA deklaruje `GetTradePenalty`: jesli
   `Harmony.GetPatchInfo` pokazuje postfiks `BKEconomyLayerInstaller.GetTradePenaltyPostfix` ORAZ oryginalny IL metody
   (`PatchProcessor.GetOriginalInstructions`) zawiera `call` do `GetTradePenalty` typu bazowego - zdjac ten postfiks tylko z podklasy
   (`harmony.Unpatch(metoda, postfiksBK)`); postfiks na Default zostaje i dziala raz wewnatrz `base`.
   Dzis to dokladnie jedna metoda: `BKROTPatch.Models.BKROTPriceModel.GetTradePenalty` (blogoslawienstwo ROT -10% dalej dziala raz, po BK).
2. kontrola na starcie kampanii (modele juz sa): `Campaign.Current.Models.TradeItemPriceFactorModel.GetTradePenalty(zbroja, null, null,
   true, 0, 0, 0)` / (0.2 x (0.06 + 1.5 + 0.25 x max(0, Tierf - 1))) - przy partii i kupcu null nie dzialaja Gladiator, zamek ani
   blogoslawienstwo, wiec wynik to czysty mnoznik BK. Linia logu: `Kara handlowa BK: x5.0 (raz; zdjety dubel z BKROTPriceModel)`;
   inny wynik = Log.Error z liczba (bez wylaczania czegokolwiek - "licz potkniecia").
3. wylacznik MCM `BkTradePenaltyOnce` = true (nowy klucz - Jeff nie ma go w json, zadziala domyslna); gen_mcm.
Gdyby BK sam naprawil dubel albo BKROT przestal wolac base - krok 1 nie znajdzie nic do zdjecia, a kontrola i tak pokaze x5.0.

## 3. Poprawka B: cena od stanu sztuki (na 127)

Jedna zasada dla kupca w miescie i zamku (SupplyDemand.PricePostfix, galaz sprzedazy):
  cena = lancuch gry (z jedna x5) x polka x surowce, nie mniej niz PODLOGA, nie wiecej niz SUFIT.
B1. PODLOGA = MinSellPercentOfValue (2% po 127) x WARTOSC ZE STANEM (`__0.ItemValue` zamiast `item.Value`), dalej nie wyzsza niz
    cena, jakiej ta polka zada za te sztuke (to juz jest w 127; przy podlodze od stanu wiaze dopiero przy f < 0.02 - zostaje jako bezpiecznik).
    Wrak (x0.1) ma podloge 0.2% wartosci nowej, Battered 0.5%, Legendary 10%. To samo w ScrapFloorPatch (wsie, karawany - tam, gdzie
    nie wycenia SupplyDemand; `itemRosterElement.ItemValue`) i w MarketGlut (uspiony przy 127, ale jedna zasada: `__0.ItemValue`).
    Opis MCM `MinSellPercentOfValue` (po angielsku) zmienic z "clean value" na "worth in its present condition".
B2. ULAMEK ZAMIAST MINIMUM GRY: gdy lancuch gry dal swoje minimum 1 zl, bierzemy prawdziwy ulamek
    (wartosc ze stanem x GetBasePriceFactor / (1 + GetTradePenalty) aktywnego modelu, te same argumenty co GetPrice; postfiks dostaje
    `__4..__6`), dopiero potem x polka. Zamyka "1 zl x polka": wrak tarczy (wartosc 2) szedl przy pustej polce za 4-6 zl.
B3. SUFIT = 1/10 ceny, jaka TA polka zada za NOWA sztuke tej rzeczy (tej samej jakosci): round(max(Value, ItemValue) x 1.10 x f x 0.10),
    nowy klucz `SellCapPercentOfNewAsk` = 10 (0 = wylaczony), tylko przy RetailFromWorth (wtedy cena zadana jest znana). Sufit wygrywa z podloga.
    Dlaczego 1/10: najtansza sztuka na polce to wrak (x0.1), wiec kupiec nigdy nie placi za zadna sztuke wiecej, niz sam bierze za jej
    wrak - petla w jednym miescie traci co najmniej material. Rzeczy lepsze niz zwykle (Fine..Legendary) licza sufit od swojej wartosci
    (naprawa nigdy nie podnosi jakosci, wiec petli nie ma). Przy Handlu 0 sufit NIE wiaze nigdy (lancuch x5 <= 9.1% < 11%); wiaze tylko
    przy bpf 1.3 dla t1-t2 i przy Handlu ok. 300 + perkach dla broni (np. miecz t5: 13.8% -> 11%).
B4. HURT MIEDZY MIASTAMI (SupplyDemand.DailyTrade :432): miasto-odbiorca placi `it.Value x 50% x srcFactor x surowce` - od wartosci
    bazowej, czyli za wrak jak za czysta sztuke (Brigandine-wrak o wartosci 232 kosztuje odbiorce ok. 1158 zl przy zrodle x1 i
    surowcach x1, do ok. 1737 przy surowcach x1.5). To samo zjawisko ("cena = stan"),
    jedna zmiana: `el.EquipmentElement.ItemValue`. Kasa odbiorcy przestaje przeplacac za zuzyte nadwyzki (zloto dalej tylko miedzy kasami).
Log (najpierw log): linia PodazPopyt dostaje "stan xN, wartosc ze stanem W, granice P..S, wiaze: podloga/sufit/-"; nowy wpis na
KAZDA sprzedaz gracza (CampaignEvents.PlayerInventoryExchangeEvent -> handel.log: miasto, sztuka, stan, wartosc, ze stanem, cena,
% wartosci); linia dnia "Skup sprzetu:" wedle sprzedajacego (gracz / lordowie AI przez SellItemsAction / sakiewki ludzi / notable):
sztuk, wartosc bazowa, wartosc ze stanem, zaplacono, ile na podlodze, ile na suficie.

## 4. Tabela dla Jeffa (zl; Handel 0, bez perkow, zwykla polka gry bpf 0.8; "x1" = polka i surowce neutralne, "x4" = pusta polka)

| Rzecz (wartosc) | Stan | Dzis x1 (x4) | Po 127 x1 (x4) | Po poprawce x1 (x4) |
|---|---|---|---|---|
| Miecz t5 (104) | czysta | 5 (20) | 2 (4) | 6 (24) |
| | Battered | 5 (20) | 2 (4) | 1 (4) |
| | wrak | 5 (20) | 2 (4) | 1 (2) |
| Buty t2 (28) | czysta | 1 (4) | 1 (4) | 2 (8) |
| | Battered | 1 (4) | 1 (4) | 1 (2) |
| | wrak | 1 (4) | 1 (4) | 1 (1) |
| Helm t6 (314) | czysta | 15 (60) | 6 (12) | 16 (64) |
| | Battered | 15 (60) | 6 (6) | 4 (16) |
| | wrak | 15 (60) | 6 (6) | 1 (4) |
| Brigandine t6 (2316) | czysta | 115 (460) | 46 (104) | 123 (492) |
| | Battered | 115 (460) | 46 (46) | 30 (120) |
| | wrak | 115 (460) | 46 (46) | 12 (48) |

Dodatkowo (po poprawce): zawalona polka x0.25 - Brigandine 46 / 11 / 4, helm 6 / 1 / 1; Handel 300 + Appraiser + Arms Dealer + Rumour,
x1: Brigandine 230 / 57 / 23, helm 31, miecz 11 (dzis 115/115/115, 15, 5); Legendary Brigandine 615 (x4: 2460; dzis i po 127: 130);
Lordly helm 41 (dzis 15, po 127 8); zamek (BK x3, raz): czysta Brigandine 46 (dzis 115, po 127 46). Miecz t3 o tej samej wartosci: +1 zl.

## 5. Skutki

5.1 Paczka 127 (2%): zostaje w calosci (MinSell 2, OneScrapFloor, limit "nie wiecej niz polka zada"). Dopiero A+B spelniaja jej
intencje: "stan x polka, nie mniej niz 2%" - lancuch x5 niesie stan (5.3% wartosci ze stanem), polka dziala w obie strony, podloga 2%
(od stanu) wiaze tylko na zawalonej polce. Sama 127 bez A daje 2% za wszystko (Brigandine 46 w kazdym stanie, przy pustej polce
104 zamiast dzisiejszych 460) - dlatego A i B powinny wejsc razem z 127 albo przed nia (decyzja 1).

5.2 Dochod gracza z lupu (3 sztuki Plundered + Damaged + Battered tej samej rzeczy; dzis / po 127 / po poprawce):
- Brigandine: x1 345 / 138 / 146; przy medianie logow x2: 690 / 138 / 292
- helm: x1 45 / 18 / 19; x2 90 / 18 / 38
- miecz: x1 15 / 6 / 6; x2 30 / 6 / 12
Zuzyty lup: ok. 40-45% dzisiejszego, 1-2 x tyle co po 127. Czyste i lepsze sztuki: wiecej niz dzis (Brigandine 123 zamiast 115,
Legendary 615 zamiast 130). Handel i perki znow dzialaja (dzis nie dzialaja wcale).

5.3 AI sprzedajace nadmiar: lordowie (PartiesSellLoot gry: caly lup poza jedzeniem, w miescie, kasa miasta placi), sakiewki ludzi
(MenPurse: najpierw najnizszy tier i najgorszy stan, dzis ok. 5-6 tys. szt. dziennie po ok. 1.5 zl), notable (RecruitKit, partia null:
gra mnozy kare x0.2, po A = kara gry; sufit B3 trzyma ich zwrot <= 11% wartosci x polka). Ten sam lancuch i te same granice co gracz:
tanie zuzyte sztuki dalej ok. 1 zl (minimum), drogie zuzyte mniej niz dzis, czyste wiecej. Kierunek netto dla lordow: raczej mniej
(lup AI jest glownie zuzyty), skala: przelewy miasta -> lordowie ok. 74 tys./dobe przy 61 mln zlota lordow - pomijalne, ale do pomiaru
(linia "Skup sprzetu"). Petli AI nie ma: AI kupuje po 110% x stan x polka, sprzedaje po <= 11% x polka.

5.4 Kasy miast (zamknieta ekonomia): kazda sprzedaz to przelew kasa miasta -> sprzedajacy (gracz: InventoryLogic, AI: SellItemsAction,
ludzie: Town.ChangeGold); A i B zmieniaja tylko WYSOKOSC przelewu, nie tworza ani nie kasuja zlota. Miasto placi mniej za zuzyte, wiecej
za czyste; odsprzedaje po 110% x stan x polka (marza x10-x20 zostaje w kasie). B4: odbiorca hurtu przestaje przeplacac za zuzyte
nadwyzki (dzis 4242 szt./dobe za 180 tys.). Kasy trzyma regulator gry (dosypal 138 tys. / skasowal 62 tys. dobe) - zmiany sa w jego skali.

5.5 Petla "kup tanio zuzyte - napraw - sprzedaj" (naprawa = robocizna n131b przy plac 1: Battered miecz 13, buty 5, helm 35,
Brigandine 223 razem z materialem 131; wraki kowale miasta nie odnawiaja - tylko wlasne kowadlo: material 2 / 2 / 4 / 35, robota 0 zl):
- W JEDNYM MIESCIE, kazda polka, Handel 0: zawsze strata. Battered + kowal: -11 (buty) .. -737 (Brigandine) przy x1, do -2279 przy x4;
  wrak + kowadlo: -3 .. -167 (x1), -53 (x0.25), -564 (x4). Dzis na zawalonej polce Brigandine-wrak bez naprawy: +51 - zamkniete.
- W JEDNYM MIESCIE, skrajny gracz (Handel 300 + Appraiser + Arms Dealer + Gladiator + blogoslawienstwo): z sufitem -2 .. -35 (strata
  = material); BEZ sufitu przy bpf 1.3: +3 .. +206 na wraku (Brigandine) - to jedyna dziura, ktora zamyka sufit B3 (decyzja 2).
- MIEDZY MIASTAMI (kupno przy x0.25, sprzedaz przy x4): wrak + kowadlo +5 (buty) .. +393 (Brigandine); Battered + kowal +1 .. +110;
  wrak bez pracy -16 (dzis +396, po 127 -18). Skrajnie (x0.125 -> x6): +671 / +435. To nie jest zloto z niczego: placi miasto z brakiem
  za prawdziwa, naprawiona sztuke (material z targu + praca), zrodlo dostaje zaplate, kazda sprzedana sztuka obniza polke odbiorcy
  (s+1), kazda kupiona podnosi polke zrodla - zysk sam maleje. Tak samo dziala wywoz nadwyzki AI (DailyTrade). Decyzja 3.
- Z robocizna z odrzuconego projektu kosztu naprawy (tansza: Battered 14 / 9 / 44 / 142) wnioski te same: jedno miasto -15 .. -656,
  miedzy miastami Brigandine +191.

## 6. Kontrola zasady 0

Regresje (kto czyta to samo):
- GetTradePenalty (A): GetPriceFactor gry (kazde kupno i sprzedaz sprzetu), zachowania BK, BE w trybie zgodnosci; AIInfluence nie jest
  ladowany. Kupno w miastach i zamkach nie zmienia sie (SupplyDemand zastepuje je wartoscia ze stanem x 1.10 x f, RetailFromWorth).
  Kupno we wsiach i u karawan tanieje: x(1 + 5 x 0.16) = x1.8 x bpf zamiast x5 x bpf - dalej >= 144% wartosci ze stanem, wiec petli
  "kup u karawany, sprzedaj w miescie" nie ma (miasto placi <= 66% przy x6).
- Sprzedaz z partia null (RecruitKit, OutlawLaw.FencePrice): paser band skupuje tylko towary, zwierzeta i konie - sprzetu nie dotyczy;
  notable zwracaja komplety ochotnikow po kara gry x polka, nie wyzej niz sufit.
- MinSellPercentOfValue czytaja 3 miejsca: ScrapFloorPatch (Patches.cs:89-98), SupplyDemand (:305-317), Settings/MCM - wszystkie w B1.
- Konie: BK ich nie mnozy, A ich nie dotyczy; B1 liczy podloge od ItemValue (modyfikatory koni tez maja cene) - spojne.
- Strzaly i belty: maja komponent broni, wiec tez x5 zamiast x25 (lancuch; w miastach i tak SupplyDemand).
Kolizje: B dziala w tym samym postfiksie co 127 (ta sama galaz kodu - B na 127, nie obok); ScrapFloor milczy w miescie (127), wiec
podloga stoi w jednym miejscu; MarketGlut uspiony. Wywoz nadwyzki (B4) i sprzedaz graczowi licza teraz te sama wartosc ze stanem.
Spojnosc: "cena = stan" (wpis 97, Jeff 05.10) obejmuje teraz takze sprzedaz kupcowi i hurt miedzy miastami; jedna podloga, jeden
sufit, jedna kara BK; zamknieta ekonomia - kazda zlotowka ma platnika (kasa miasta) i odbiorce.
Cudzy kod (sprawdzony w dekompilacji): BK Main.cs:245 + BKEconomyLayerInstaller :411-438, :511-545; BKROTPriceModel (woła base w obu
galeziach); gra DefaultTradeItemPriceFactorModel :28-193, EquipmentElement.ItemValue = round(Value x PriceMultiplier), PartiesSellLoot
(lordowie sprzedaja w miescie wszystko poza jedzeniem, SellItemsAction, kasa miasta).

## 7. Ryzyka / co sprawdzic

1. `Harmony.Unpatch(metoda, postfiks)` zdejmuje latke innego wlasciciela - sprawdzic w probie na prawdziwych DLL (kod\proba: po
   instalatorze BK i naszym kroku GetPatchInfo ma pokazac postfiks BK tylko na Default, mnoznik 5.0).
2. Aktualizacja BK/BKROT: wykrycie przez IL + pomiar na starcie; linia logu pokaze kazdy inny mnoznik.
3. Tierf liczy RBM (+-0.5 = +-4% ceny); bpf 0.8 przyjete z logow (118/167 linii); przy bpf 1.3 ceny x1.6 i sufit moze wiazac dla t1-t2.
4. Dochod lordow z lupu i sakiewek - kierunek oszacowany, wielkosc tylko z nowej linii "Skup sprzetu" (autotest 40 dob).
5. Ekran zbrojowni DTE (QuartermasterEscrow - SupplyDemand go pomija) pokazuje lancuch gry: po A ceny tam x~4.5. Sprawdzic, czy w tym
   ekranie plynie zloto (wedlug kodu MenPurse gracz placi ludziom MenPurse.SellPrice, czyli cene miasta - wtedy bez znaczenia).
6. Gladiator i blogoslawienstwo dzialaja raz (dzis podwojnie: x0.64) - gracz z Gladiatorem traci czesc ulgi wzgledem dzis, ale i tak
   dostaje wiecej niz dzis za czyste sztuki.
7. Sufit B3 ogranicza korzysc z Handlu 300 dla taniej broni (miecz t5 x1: 14 -> 11; Brigandine bez zmian 230) - zamierzone.
8. B2 liczy ulamek modelem aktywnym (GetBasePriceFactor + GetTradePenalty) tylko wtedy, gdy gra dala 1 zl; mod, ktory nadpisze sam
   GetPrice, moglby dac inny ulamek - dzis takiego nie ma.

## 8. Kolejnosc i test

Paczka "cena sprzedazy sprzetu" na `paczki/127-pokretla-jeffa` (95baa63), numer nada skladanie, trzy commity: CS-A (jedna kara BK),
CS-B (B1-B3), CS-C (B4 hurt od stanu). Kazdy: autor + niezalezny recenzent, build kod 0, gen_mcm (+2 klucze: BkTradePenaltyOnce,
SellCapPercentOfNewAsk). Proba na prawdziwych DLL (rozszerzyc kod\proba: tryb "armoury" z nowym DLL) ma dac tabele z punktu 4 co do
zlotowki. Potem autotest 40 dob (zgoda 07.10): linia "Kara handlowa BK: x5.0", "Skup sprzetu" dzien po dniu, "Przeplywy osad (kasy
miast)" i zloto lordow wobec autotestu 6, ERROR 0. Test Jeffa: sprzedac w miescie czysta, Battered i wrak tej samej rzeczy - handel.log
pokaze kazda sprzedaz. Wgranie tylko na "wgraj", razem z 127 (zmiana json 5 -> 2 przy wgraniu, gra zamknieta, .bak).

## 9. Decyzje Jeffa (tylko te, ktore zmieniaja gre)

1. Wgrac razem z 127 (zalecam). Sama 127 da 2% za wszystko: Brigandine 46 zl w kazdym stanie, takze legendarny ok. 130.
2. Sufit "kupiec nie da wiecej niz za wrak" (zalecam TAK). Bez niego gracz z Handlem 300, perkami, Gladiatorem i blogoslawienstwem
   zarabia do ok. 200 zl na kazdym wraku Brigandine odnowionym we wlasnej kuzni i sprzedanym w tym samym miescie. Z sufitem Handel
   podnosi cene broni najwyzej do 1/10 ceny nowej sztuki.
3. Handel odnowionym sprzetem miedzy miastami (zalecam ZOSTAWIC). Kupic wraki tam, gdzie zalegaja, odnowic, sprzedac tam, gdzie ich
   brakuje: do ok. +390 zl na pierwszym Brigandine, potem coraz mniej. Placi miasto, ktoremu brakuje, a sztuka i praca sa prawdziwe.
