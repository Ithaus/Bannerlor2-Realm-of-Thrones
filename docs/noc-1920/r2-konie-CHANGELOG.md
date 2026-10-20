# r2-konie - wierzchowce egzotyczne i wielblady (PLAN 6.9), galaz w-toku/r2-konie na bc5a1e0d (WGRANIE 89)

## Problem (dowod)
- Audyt 19.10: `MountLaw.cs` :109 i :171 dawal wielblada kulturze `aserai` (Dorne) - wbrew decyzji D 09.10 I/J ("Dorne - piaskowe rumaki",
  projekt etapu 6 v2 M2(d), krytyka K1-11: wielblad = qartheen).
- `LegendaryLaw.SweepMarkets` kasowal egzotyczne wierzchowce z polek W NICOSC (`AddToCounts(-n)` bez odbiorcy). Log 160 dob na WGRANIU 89
  (`Modules/Armoury/Armoury-2026-10-19_19-25-02.log`): 128 linii "egzotycznych wierzchowcow zdjetych z polek nie u swoich", razem 398 szt.
  Produkcja wielbladow byla juz dobra (CamelLaw: "wielblady 7 dob: w Qarthu 34, poza Qarthem 0") - to nie produkcja, tylko przewoz.
- Stajnia AI nie kupowala wielbladow w Qarthu.

## Przyczyna
- MountLaw: `case Fam.Camel: return culture == "aserai" || ownTemplate;` i `cul == "aserai" || cul == "qartheen"` dla targu. W ROT jezdzcy na
  wielbladach sa tylko w Qarthu (`qartheen_camel_rider`, `qartheen_cameleer` - ROT-Troops.xml, slot Horse = Item.camel); wielblady w XML maja
  kulture `aserai` (SandBoxCore horses_and_others.xml: camel, war_camel, pack_camel, noble_camel, special_camel).
- Polki - zrodla sztuk nie u swoich: (a) wywoz kupcow `SupplyDemand.DailyTradeCore` - koszyk = typ x tier, wiec "kon tieru t" z Qarthu wiozl
  wielblady razem z konmi do miast, ktore ich nie trzymaja (bez filtra MountLaw); (b) karawany gry (CaravansCampaignBehavior handluje kategoriami
  IsAnimal - dekompilacja :1010, :1132); (c) lup lordow sprzedany w miescie (PartiesSellLootCampaignBehavior: juczne i z modyfikatorem). Na celu
  SweepMarkets je kasowal.
- Stajnia: `Stables.IsPlainMount` wymaga Monster "horse" - wielblad (Monster "camel") nigdy nie byl kandydatem; test `camel ... it.Culture !=
  settlement.Culture` w petli targu (Stables.cs :970) byl martwy, a i tak odrzucalby wielblady w Qarthu (aserai != qartheen). Jezdzcy wielbladow
  sa poza Riders/Remount/zapasem K2 - nikt im wielbladow nie kupowal.

## Zmiana (Armoury; trzy klucze, domyslnie TAK, grupa MCM "Crafts, raw materials and horses (stage 6)")
1. `CamelsOnlyQarth` - MountLaw: wielblad tylko kultura `qartheen` albo wlasny szablon (jezdziec, partia); targ trzyma wielblady, gdy kultura osady
   albo kultura z kalibracji (`PeopleBook.CultureAtCalibration`, ta sama co CamelLaw) to qartheen. Wylaczony - jak dotad (takze aserai).
2. `ExoticMountsNeverVanish` - zero kasowania:
   - zrodlo: wywoz kupcow wiezie sztuke egzotyczna tylko do celu, ktory ja trzyma (`MountLaw.AllowedForSettlement(cel)`), inaczej zostaje na polce
     zrodla (licznik w linii "PodazPopyt: ... egzotyczne wierzchowce zatrzymane u zrodla");
   - polka: `SweepMarkets` (doba) - sztuke nie u swoich z polki MIASTA kupuje najblizsze miasto, ktore ja trzyma (nie oblezone, kasa ponad zapas
     kupcow `CastlePurse.Spendable`), sztuka po cenie straganu zrodla (`ShelfBuy.Take`), zloto do kasy zrodla, sztuka jedzie wozem kupcow
     (`TradeCarts.Ship`; bez wozow - od razu) - ta sama droga co wywoz kupcow. Bez kupca (mamut - zaden targ go nie trzyma, wies, zamek, biedny
     odbiorca, wczytanie gry) sztuka ZOSTAJE na polce. Wylaczony - kasowanie jak dotad.
3. `QarthStablesBuyCamels` - `Stables.CamelBuy` (wolane z OnSettlementEntered po Remount, tylko miasto, tylko przy TroopsFightWithOwnKitOnly jak
   Remount): lord AI, ktorego jezdzcom wielbladow (wzorzec z wielbladem w slocie konia) brakuje wielblada w zbrojowni DTE, kupuje wielblady-wierzchowce
   (nie juczne) z polki miasta, ktore trzyma wielblady, do ZBROJOWNI DTE - najwyzej brak i AiMountMaxPerVisit, z AiMountPurseShare kiesy pana,
   najtansze najpierw po cenie straganu, zloto do miasta; partia musi miec prawo do wielblada (AllowedForParty). Odmowa DTE - sztuki wracaja na polke.

## Linie logu (autotest)
- `Egzotyczne wierzchowce (r2-konie): dzien | nie u swoich na polkach: kupione przez targ, ktory je trzyma, M szt. w D transakcjach (zaplacone G zl,
  wozem kupcow), zostaja na polce K szt. (...), skasowane 0` - co dobe, gdy cos jest, i co 7 dob zawsze; przy wczytaniu - "wczytanie".
- `Stajnia AI (r2-konie): <lord> kupil N wielbladow do zbrojowni w <miasto> za G (jezdzcow wielbladow R, wielbladow w zbrojowni przed zakupem H).`
- Stara linia `LegendaryLaw: targi (...) - N egzotycznych wierzchowcow zdjetych z polek` ma byc 0 razy (pojawia sie tylko przy wylaczonym kluczu).
- W "PodazPopyt: kupcy wywiezli ..." dopisek "egzotyczne wierzchowce zatrzymane u zrodla ... szt.-prob" (liczy proby, nie sztuki).

## Ryzyko / co sprawdzic (kontrola calosci)
- Regresja/kolizje: MountLaw czytaja GearFit (notabl, komplety - Dornijczycy nie kupia juz wielblada), DragonUnmount (w bitwie Dornijczyk na wielbladzie
  dostanie zamiennik - zgodnie z regula), OutlawLaw/SpoilsSeal (paser i lup gracza nie sprzedaja wielblada w Dorne), LegendaryLaw.SweepAiArmories:
  UWAGA - wielblad w zbrojowni DTE partii aserai jest teraz "nie u swoich" i ta czystka zbrojowni go KASUJE (jak dotad inne egzotyki u obcych);
  w nowej kampanii Dorne nie ma wielbladow w szablonach ani w stadninach (CamelLaw), zostaje tylko lup - OTWARTE, poza zakresem tej pozycji.
  CleanseTrashInBags (sakwy gracza): egzotyk, do ktorego partia gracza nie ma prawa, jest kasowany - jak dotad; teraz takze wielblad u gracza
  spoza Qarthu.
- Zamknieta ekonomia: przeniesienie polka -> polka za zloto celu do kasy zrodla (jak wywoz kupcow); woz dojezdza albo zawraca ze zwrotem
  (TradeCarts.Back; droga dluzsza niz 30 dob dojezdza - recenzja 215). CamelBuy: polka -> zbrojownia, zloto pan -> miasto; bez pompy (kupno tylko
  do braku; MenPurse nie sprzedaje koni zbrojowni, SellSpareHorses tylko IsPlainMount).
- Wydajnosc: SweepMarkets i wywoz kupcow - raz na dobe, watek glowny; CamelBuy - przy wejsciu lorda do miasta, przeglad rosteru bez alokacji
  (wczesne wyjscie bez jezdzcow wielbladow). Nic w rownoleglym AI.
- Zapis: bez nowego stanu (wozy kupcow zapisuja sie jak dotad w arm_tradecarts).
- Nie sprawdzone w grze: czy DTE sadza jezdzca wielbladow na wielbladzie ze zbrojowni (AddToArmory sprawdza tylko przyrost zbrojowni).

## Status
DO SPRAWDZENIA (build Release kod 0, 0 bledow; autotestu nie uruchamialem - zakaz w tym zadaniu).

## Decyzje (podjete samodzielnie)
- (2): zatrzymanie u zrodla w wywozie kupcow + przeniesienie z oplata do najblizszego targu, ktory trzyma zwierze; bez kupca - zostaje na polce.
  Najprostsze poprawne: istniejaca droga kupcow (ShelfBuy, CastlePurse, TradeCarts) zamiast nowej latki Harmony na karawany gry
  (CaravansCampaignBehavior / SellItemsAction - za duze ryzyko przed ostatnia kampania). Mamut nie ma prawowitego targu - zostaje na polce.
- (3): wielblady ida do zbrojowni (stamtad DTE bierze wierzchowce), nie do taboru - tabor obsluguje tylko IsPlainMount (Remount, awanse, zapas K2),
  wiec wielblad w taborze nigdy nie trafilby pod jezdzca i bylby kupowany w kolko.
- Przy wczytaniu gry (SweepMarkets "wczytanie") tylko licznik - wozy kupcow sa odtwarzane dopiero w OnSessionLaunched.

## Test w grze
- Autotest 30+ dob: `grep "Egzotyczne wierzchowce (r2-konie)"` - "skasowane 0", liczby kupionych i zostajacych;
  `grep -c "egzotycznych wierzchowcow zdjetych z polek"` = 0; `grep "Stajnia AI (r2-konie)"` - lordowie Qarthu kupuja wielblady w Qarthu;
  `grep "wielblady 7 dob"` dalej "poza Qarthem 0".
