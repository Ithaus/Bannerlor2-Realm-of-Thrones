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
- Recenzja (dopisane): `grep "Mends: wielblady w przydziale DTE (r2-konie)"` w logu CrashScribe - pierwsza linia "PODPIETY
  (Armoury.MountLaw.AllowedForParty)", potem (tylko w bitwach, najwyzej 12 linii) partie Qarthu "zostaje N, wyciete 0";
  `grep "do taboru partii jako towar"` w Armoury - "w nicosc 0"; `grep "CleanseTrashInBags (r2-konie)"` - "skasowane 0" (tylko gdy gracz
  mial egzotyk nie u swoich w zbrojowni DTE). Bitwa z lordem Qarthu (bieg-bitwa.ps1 -Battles albo gracz): jezdzcy Qarthu na wielbladach.

## Recenzja (19.10, po 96f50cbd) - co poprawione
- WAZNE, prawdziwe - CrashScribe `Mends.CamelCulling` (postfiks DTE `GenerateHorseAndHarnessList`, wpiety bez wylacznika od WGRANIA 89)
  wycinal z przydzialu DTE kazdy wielblad, ktorego kultura z XML (aserai) jest inna niz kultura partii. Sprawdzone w kodzie (Mends.cs, petla
  `ic == pc`) i w dekompilacji DTE: `DoAssignAsync` jest synchroniczne (watek glowny, start misji), `AssignHorseAndHarness` bierze konie tylko
  z `_horseAndHarnesses` po kolei dla jezdzcow. Zakupy z punktu (3) nie mialy wiec efektu w bitwie, a CrashScribe mial regule przeciwna do MountLaw.
  POPRAWKA (commit 33056eed): przy wlaczonym kluczu Armoury `CamelsOnlyQarth` CamelCulling pyta o wielblada Armoury `MountLaw.AllowedForParty`
  (refleksja, delegat wiazany raz, jak `ArmouryUniqueIs`) - jedna regula w obu modach, bez lustra do pilnowania. Bez Armoury, przy innej
  sygnaturze albo przy wylaczonym kluczu - stara regula. Rydwany i slonie - stara regula (poza zakresem). Linie: "Mends: wielblady w przydziale
  DTE (r2-konie): PODPIETY/BRAK" (raz) i do 12 linii z partiami.
- DROBNE, prawdziwe - `CamelsOnlyQarth` dzialal tez bez CamelLaw (stary zapis, CamelLaw.On=false): Dorne rodzi wtedy wielblady, nie moglby ich
  ani dosiasc, ani trzymac, a kazdy jechalby do Qarthu. POPRAWKA (33056eed): `MountLaw.CamelsQarthOnly = klucz && CamelLaw.On`; opis klucza
  w MCM mowi, ze dziala razem z "Sand Steeds In Dorne" w nowej kampanii. CrashScribe dostaje to samo przez delegata (bez CamelLaw - aserai i qartheen).
  Kampania Jeffa ma CamelLaw (log 14:41 "poza Qarthem 0") - dla niej bez zmian.
- DROBNE, prawdziwe - `LegendaryLaw.SweepAiArmories` kasowal egzotyk nie u swoich ze zbrojowni DTE AI (po CamelsOnlyQarth takze wielblady
  partii aserai). POPRAWKA (0a62d659): przy `ExoticMountsNeverVanish` sztuka schodzi ze zbrojowni do TABORU partii jako towar. Nie wraca
  do zbrojowni: DTE przenosi z taboru tylko bron i pancerz (dekompilacja EveryoneCampaignBehavior :400-422), Stajnia/MenPurse/Remount tylko
  `IsPlainMount`. Sprzedany w miescie trafi na polke, a SweepMarkets odda go targowi, ktory go trzyma. Lista `kill` bez zmian (kontrola R9
  starej drogi `KillListOld` porownuje te same sztuki). Linia "magazyny AI ... (r2-konie: do taboru partii jako towar N, w nicosc 0)".
- DROBNE, prawdziwe - `CleanseTrashInBags` (przy kazdym menu) kasowal egzotyk nie u swoich z sakw i zbrojowni gracza: gracz placil miastu,
  a zwierze znikalo; po CamelsOnlyQarth dotyczyloby to tez wielbladow gracza z kultury aserai. POPRAWKA (0a62d659): przy `ExoticMountsNeverVanish`
  egzotyk zostaje w sakwach jako towar, ze zbrojowni DTE gracza schodzi do sakw (DTE i tak go nie przydzieli - CamelCulling). Slonie-towar
  wedlug id (Jeff 29.08 "slonie ma Zlota Kompania") - jak dotad, poza zakresem.
- NIE ZMIENIONE (decyzja): `BattlefieldLaw.CleanseTrash` - to sito ekranu lupu, nie zabieranie wlasnosci: egzotyk nie u swoich nie trafia
  do lupu gracza, tak jak wraki ponizej progu zostaja na polu (decyzje Jeffa 29.08 i 14.09). Nikt za niego nie placi, wiec to nie jest
  "zloto za nic"; zmiana wpuscilaby do lupu gracza wielblady, rydwany i mamuty z kazdej bitwy.
- Piate znalezisko recenzji (plik "Armoury/src/St...", zapewne Stables.cs) dotarlo uciete - nie znam tresci; nie poprawiane. Przejrzalem
  `Stables.CamelBuy` ponownie: kupno tylko do braku, zloto pan -> miasto, odmowa DTE oddaje sztuki na polke - bez zmian.

## Ryzyko po recenzji
- CamelCulling przez delegata: partia Qarthu z wielbladami w zbrojowni dostaje je w przydziale DTE; DTE rozdaje konie po kolei jezdzcom (nie
  per jednostka), wiec wielblad moze trafic tez jezdzcowi konnemu Qarthu - regula MountLaw jest na poziomie partii (kultura qartheen), wiec to
  zgodne. W bitwie `DragonUnmount` (MountLaw.Allowed per zolnierz) dalej zdejmuje z wielblada obcego najemnika w partii Qarthu.
- Tabor AI: egzotyki w taborze licza sie do stada partii (predkosc mapy) - skala mala (w biegu 160 dob 0 linii "egzotycznych wierzchowcow nie u swoich").
- Sakwy gracza: gracz moze teraz trzymac i sprzedac wielblada/rydwan/mamuta; sprzedany na polce obcego miasta - SweepMarkets odwiezie go do targu,
  ktory go trzyma, albo zostanie na polce.

## Status po recenzji
DO SPRAWDZENIA. Build Release: Armoury kod 0 (0 bledow, 4 stare ostrzezenia), CrashScribe kod 0 (0 bledow, 0 ostrzezen); gen_mcm uruchomiony
po zmianie Settings.cs. Autotestu nie uruchamialem (zakaz w tym zadaniu). Do wgrania razem: Armoury.dll i CrashScribe.dll (CamelCulling).
