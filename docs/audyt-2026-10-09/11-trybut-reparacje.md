Ustalone na podstawie kodu i logu. Za "trybut" odpowiada niemal w calosci mod Diplomacy, ale nie jest to trybut dzienny, tylko jednorazowe reparacje wojenne. Diplomacy wpisuje je do vanillowego licznika Kingdom.TributeWallet, a gra w ciagu ok. 3 dob sciaga je z kies rodow przegranego (do nicosci). Zwyciezca dostaje cala kwote od razu z niczego: 1/3 krol, do 1/6 najemnicy, reszta skarbiec (KingdomBudgetWallet).

Wielkosc liczy DiplomacyCostCalculator: (DefeatedGoldCost 200 + E_inny*(E_placacy-E_inny)/5 lub /10) * floor(suma tierow rodow, w granicach 5..50 * ScalingWarReparationsGoldCostMultiplier 50). Dla duzego krolestwa daje to 2 500 zl za punkt, maksymalnie ok. 1.75 mln. U Jeffa w MCM sa wartosci domyslne.

Glowna przyczyna to pompa reparacji. Zmeczenie wojna Diplomacy codziennie wymusza pokoj z reparacjami (WarExhaustionManager.cs:1686), a ROT `EnforceWars` (ROTStorylineWars.cs:234-260) jeszcze tej samej doby wypowiada wojne fabularna od nowa. W kronice zadna z tych wojen nie zniknela ani na dobe. 19 z 21 oplat (11.5 z 12.3 mln, 93%) pochodzi z takich "pokojow" trwajacych najwyzej dobe. Para Stormlands-Dragonstone zaplacila 4 razy w 120 dobach (2.5 mln), para Reach-Dorne 2.57 mln.

Na tle dochodu: pojedyncza reparacja to 6-46 dni dochodu stalego D krolestwa (2-13% roku 364-dniowego). Typowy rod placi 17 dni swojego D. W 67 z 208 przypadkow obciazenie rodu przekroczylo kiese glowy, a niedobor (ok. 2.0 mln wg modelu, 2.28 mln w logu) przeszedl w dlug wobec korony. Ten dlug gra splaca w nicosc.

Historycznie odszkodowania byly wieksze (0.5-3 lata dochodow korony), ale placila je korona ratami przez lata z podatku nadzwyczajnego, z czestym czesciowym niesplaceniem, i konczyly wojne.

Propozycja na teraz (zero kodu): Diplomacy ScalingWarReparationsGoldCostMultiplier 50 -> 10. Nastepnie jedna latka (Mend): zero reparacji, gdy ROT wymusza te wojne. Docelowo zgodnie z wierszami 11-12 projektu (paczki 165/168): reparacje jako dlug korona->korona splacany ratami z biezacych wplywow, a splata DebtToKingdom do skarbca, nie w nicosc. Decyzji projektowej Jeffa to nie wymaga; trzeba tylko jego zgody na zmiane w jego pliku MCM albo "wgraj" dla latki.

(1) KTO I JAK LICZY

- Vanilla `MakePeaceAction.ApplyByKingdomDecision` zapisuje tylko trybut dzienny (`StanceLink.SetDailyTributePaid`). Wzor jest w `DefaultDiplomacyModel.GetDailyTributeToPay` (:1034-1059): 5-15% * 0.35 * suma dobrobytu miast placacego. Jest malenki: "trybut przyjety" za cale 120 dob to 18 147 zl.
- Diplomacy 1.4.7 przejmuje kazdy pokoj:
  - `MakePeaceKingdomDecisionPatch` (prefiks na ApplyChosenOutcome) oraz wymuszony pokoj ze zmeczenia, `WarExhaustionManager.ConsiderPeaceActions` :1686 (Loss/Tie przy zmeczeniu 100), prowadza do `KingdomPeaceAction.ApplyPeace` -> `AcceptPeace` :182-195 (`diplomacyCost.ApplyCost()`).
  - Dodatkowo AcceptPeace ustawia trybut dzienny na `DaysInYear*5` dni.
- Kwota reparacji: `DiplomacyCostCalculator.DetermineReparationsForMakingPeace` :73-131 i `GetKingdomScalingFactorForReparations` :147-154.
  - Wzor: X = (DefeatedGoldCost, jesli przegrana, + E_drugi*(E_placacy-E_drugi)/(10 gdy E_drugi>=75, inaczej 5)) * floor(clamp(suma tierow, najemnik tier/3, 5..50) * ScalingWarReparationsGoldCostMultiplier), zaokraglone do 100.
  - Ustawienia MCM Jeffa (`Configs/ModSettings/Global/Diplomacy/DiplomacySettings_v1.2.json`): EnableWarExhaustion true, ScalingGoldCosts true, ScalingWarReparationsGoldCostMultiplier 50.0, DefeatedGoldCost 200, EnableFiefRepatriation true, DeclareWarCooldownInDays 21. Duze krolestwa ROT maja sume tierow >= 50, czyli mnoznik 2 500, a maksimum to (200+500)*2500 = 1.75 mln.
  - Reach w 96. dobie: 1 683 084 = 673*2500 (200 za przegrana + 473 z roznicy zmeczenia). Zgadza sie z wzorem.
- Kwota reparacji nie wplywa na decyzje AI o pokoju. Jest uzywana tylko w ApplyCost, w UI i w komunikatach (sprawdzone grepem). BK zmienia tylko oplate bogatych rodow do budzetu (0.1%), logike trybutu zostawia bez zmian (EconomyPatches.cs:190-235).

(2) SKAD I DOKAD IDZIE ZLOTO

Kod Diplomacy: `Diplomacy.Actions/GiveGoldToKingdomAction.cs`.

Placacy (A, :57-87):
- Najpierw z A.KingdomBudgetWallet, ale tylko ponad 2 000 000. Skarbce maja srednio 1.7 mln (50.9 mln na 30 krolestw), wiec prawie nigdy.
- Reszta: `A.TributeWallet -= X` (:86). To tylko licznik.
- Jesli wolne zloto rodow (zloto - min(zloto/4, 50k)) jest mniejsze od X o >= 100k, spada dobrobyt miast (niedobor/1000) i X maleje. W tescie sie nie zdarzylo.
- Codziennie vanillowe `AddExpensesForTributes` (DefaultClanFinanceModel :342-358): kazdy rod placi udzial (`CalculateShareFactor` :497-502: miasto 3, zamek 1, +1, krol +1) od biezacego licznika, do nicosci. Pierwszej doby schodzi ok. 60%, po 3 dobach ok. 95%. Log Reach, doby 96-99: 1.12 / 0.58 / 0.20 / 0.07 mln naliczone.
- Czego nie ma w kiesie glowy (Clan.Gold = Leader.Gold), to `Clan.DebtToKingdom` (ApplyShareForExpenses :381-393). Kolejnymi dobami `AddPaymentForDebts` (:219-230) zabiera na splate cala kiese, do nicosci. `ChangeKingdomAction.cs:38` zeruje dlug przy zmianie krolestwa.

Odbiorca (B, :102-132), cale X natychmiast, niezaleznie od tego, czy A kiedykolwiek zaplaci:
- X/3 dla krola B (`GiveGoldAction` z null, czyli z niczego);
- X/6 dla najemnikow B wedlug wplywu, najwyzej tier*10 000 kazdy (z niczego);
- reszta: `B.KingdomBudgetWallet += reszta` (z niczego; u nas to skarbiec korony, z ktorego idzie zwrot zoldu).
- Dowod w logu, doba 96: "wplywy spoza rodu poza rozliczeniem ... z niczego 1 001 481" (zwykle 0.15-0.5 mln) i "skarbce razem +939 669" (zwykle -0.2 mln).

Bilans 120 dob:
- powstalo u odbiorcow ok. 12.3 mln;
- zniklo u placacych 10.99 mln ("trybut zaplacony") + 0.74 mln splat dlugu;
- dlug wobec korony na koniec: 1.54 mln u 38 rodow, z czego 31 placilo reparacje.
- W sumie prawie na zero, ale to nie jest przelew: to dwa niezalezne strumienie (nicosc i nic), z inna chwila, a niewyplacalnosc placacego nie zmniejsza wplywu odbiorcy.

POMPA (przyczyna glowna):
- ROT `ROTStorylineWars.EnforceWars` (DailyTick, :234-260) wola `DeclareWarAction.ApplyByDefault` dla wojen fabularnych z pominieciem cooldownu Diplomacy. Kazda z nich ma `Enforced` (ROTStorylineWar.cs:63-110): pary (5,100), np. Reach-Dorne, Braavos-Lorath, Tyrosh-Lys, Norvos-Qohor, Volantis-Aegon, Qarth/Dothraki-Targaryen, Sarnor-Ibben, oraz wieczne (0,0): Dragonstone-Stormlands i NightsWatch-FreeFolk.
- KRONIKA WOJEN (CrashScribe WarReport, codzienny przeglad Kingdom.All) pokazuje te pary bez przerwy w dobach 23-121, a liczba rodow w pokoju nie drgnela przy zadnej oplacie.
- 19 z 21 oplat to taki "pokoj" trwajacy najwyzej dobe. Prawdziwe pokoje byly dwa: Aegon-Targaryen (doba 95, 685 tys.) i Myr-Tyrosh (106, 116 tys.).
- Pary placace wielokrotnie: Stormlands+Dragonstone 2.50 mln (4 razy), Reach+Dorne 2.57, Norvos+Qohor 1.21, Lorath 1.24 (2 razy), Free Folk 1.08 (2 razy), Tyrosh 1.06 (2 razy).
- Placacy splaca licznik takze po wznowieniu wojny, bo AddExpensesForTributes nie sprawdza wojny.

(3) WOBEC DOCHODU (CSV + budzet-rodow.csv)

- 21 zdarzen, suma 12.26 mln, czyli 102 tys. na dobe = 7.6% dochodu stalego D wszystkich rodow swiata (1.35 mln na dobe).
- Krolestwo: pojedyncza reparacja = 6-46 dni D (Reach 15 dni, 4% roku; Free Folk 46 dni, 13%). Wobec samego dochodu modelu gry (bez zaworu i zwrotow): 35-231 dni.
- Rod (208 obciazen): mediana 17 dni wlasnego D, p75 40 dni.
  - 123 razy obciazenie > 50% kiesy glowy, 67 razy > cala kiesa.
  - Modelowy niedobor do dlugu ok. 2.0 mln; w logu nowy dlug 2.28 mln.
- 14 z 25 bankrutow Banku placilo reparacje (22-386 tys. kazdy) - potwierdzone.
- Wiecznie wymuszone pary licza sie rocznie jak staly podatek wojenny: Free Folk ok. 33-58% rocznego D, Lorath ok. 20-25%, Stormlands/Dragonstone ok. 11-17%.

(4) HISTORIA

Rzad wielkosci podaje wedlug typowych szacunkow, nie dokladnie.
- Ryszard Lwie Serce (1193-94, XII w., jako tlo): 150 tys. marek, ok. 2-3 lata dochodow korony; zebrane podatkiem 1/4 ruchomosci z calego krolestwa, w ratach.
- Jan II (Bretigny 1360): 3 mln ecu, rata 400 tys. rocznie, na co wprowadzono podatek (aides); splacono mniej wiecej polowe.
- Dawid II Szkocki (Berwick 1357): 100 tys. marek w 10 ratach rocznych, finansowane clami od welny; splacane z przerwami, nigdy w calosci.
- I pokoj torunski (1411): 100 tys. kop groszy praskich w 4 ratach; Zakon nie mial tego w skarbcu, nalozyl nadzwyczajne podatki na miasta pruskie, co rozbudzilo opor (Zwiazek Pruski).
- Picquigny (1475): 75 tys. ecu od reki + 50 tys. rocznie, kilka procent dochodu Ludwika XI.
- Okup rycerza: przyjmowano mniej wiecej roczny dochod z jego ziem.
- GoT: wojny konczy sie zakladnikami i malzenstwami (Theon), Wolne Miasta oplacaja khalasary "darami" z kas miast, a dlugi korony maja wierzyciela i nie znikaja ("Iron Bank will have its due").
- Wzor: placi skarbiec (korona) z podatku calego kraju, ratami przez lata (10-30% rocznego dochodu na rok), odbiorca dostaje to, co zaplacono, niesplacona reszta zostaje dlugiem, a odszkodowanie konczy wojne.
- U nas kwota jest mniejsza niz w historii, ale sciagana w 3 doby z kies panow, odbiorca dostaje ja z niczego od razu, dlug ginie w nicosci, a "pokoj" trwa najwyzej dobe i powtarza sie co 1-2 miesiace.

Pliki:
- dekompilacja Diplomacy: C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/7016f733-d379-418e-b700-f66fd52e4d2b/scratchpad/tryb/dip/
- skrypty t1-t7.py: tamze, katalog scratchpad/tryb/
- ROT: C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/fa2fd7a6-a098-46e5-8d1b-3c099c38c1f8/scratchpad/rot/ROT.CampaignBehaviors/ROTStorylineWars.cs

Niepewnosc:
- Ktory DLL laduje ModuleLoader (zakladam 1.4.7 dla gry 1.4.8; SubModule mowi v1.4.7).
- Wpisu pokoju w logu nie ma (Diplomacy loguje przez LogTrace). Pompe wnioskuje z kodu ROT i Diplomacy, kroniki oraz oplat bez zmiany stanu wojny.

PROPOZYCJA:
KROK A - najmniejszy i bezpieczny, zero kodu, mozna od razu i przed 165

Diplomacy MCM "Scaling War Reparations Gold Cost Multiplier" 50 -> 10 (RequireRestart=false; klucz ScalingWarReparationsGoldCostMultiplier w DiplomacySettings_v1.2.json).

Liczby na tym samym tescie:
- suma za 120 dob: 12.3 -> 2.45 mln (obciazenie swiata 7.6% -> 1.5% D);
- Reach: 1.68 mln -> 337 tys. (3 dni D krolestwa);
- maksimum: 1.75 mln -> 350 tys.;
- rod: mediana 17 -> 3.4 dnia D, p75 40 -> 8;
- obciazen wiekszych od kiesy: 67 -> 28 (zostaja rody juz puste);
- niedobor zamieniany w dlug wobec korony: ok. 2.0 -> 0.18 mln.

Dla porownania: 20 daje 4.9 mln i 35 rodow ponad kiese; 15 daje 3.7 mln i 30 rodow.

Uzasadnienie: dopoki gra sciaga reparacje w 3 doby z kies panow, a nie ratami ze skarbca, kwota musi sie miescic w kilku dniach dochodu rodu. Przy 10 pierwsza doba bierze ok. 2 dni D typowego rodu. AI nie liczy reparacji przy decyzji o pokoju, wiec przebieg wojen sie nie zmienia.

Ryzyko: brak; zmiana jest odwracalna jednym suwakiem. Struktury to nie naprawia (nicosc i nic), tylko ja zmniejsza 5 razy.

KROK B - przyczyna glowna, jedna latka (Mend w CrashScribe, jedna zmiana naraz)

- Prefiks Harmony na Diplomacy `DiplomacyCostCalculator.DetermineReparationsForMakingPeace` (private static, zwraca KingdomWalletCost): gdy ROT `SubModule.StorylineWars` (internal static) `.IsWarForced(a,b)` == true, zwrocic `new KingdomWalletCost(a, b, 0)`. Wojna i tak wraca najdalej po dobie.
- Wywolanie przez refleksje. Brak typu albo wyjatek oznacza, ze latka nic nie robi; potkniecia liczyc, nie gasic funkcji.
- Ta sama metoda zasila UI, wiec gracz tez zobaczy 0.
- Efekt: znika 19 z 21 oplat (11.5 z 12.3 mln). Prawdziwe pokoje (Aegon 685 tys. = 18 dni D; Myr 116 tys. = 8 dni) zostaja.
- Po B mozna wrocic do mnoznika 50, bo prawdziwych pokojow jest mniej wiecej 2 na 120 dob.
- Do sprawdzenia przy B: Diplomacy przy kazdym wymuszonym pokoju moze tez oddawac lenna (EnableFiefRepatriation), wiec pompa moze przerzucac zamki tam i z powrotem. Policzyc w logu.

KROK C - docelowo, paczki 165/168, wiersze 11-12 projektu

- Prefiks na `GiveGoldToKingdomAction` (gdy giver = ReparationsWallet): zamiast TributeWallet i "z niczego" dla odbiorcy zapis dlugu korona A -> korona B.
- Rata dzienna: najwyzej 50% biezacych wplywow korony A + 1/180 zapasu ponad CrownReserveGold. Korona B dostaje dokladnie te rate do skarbca, bez 1/3 dla krola z niczego. Rody nie placa bezposrednio, wiec nie powstaje DebtToKingdom.
- Postfiks na `AddPaymentForDebts`: splata DebtToKingdom idzie do `clan.Kingdom.KingdomBudgetWallet`, nie w nicosc.
- Ryzyko 13 w projekcie mozna zamknac: zrodlo ujemnego TributeWallet to GiveGoldToKingdomAction.cs:86 (reparacje Diplomacy) plus pompa EnforceWars ROT.

CZY DECYZJA JEFFA

- A i B nie wymagaja decyzji projektowej. To parametr i szczelnosc ekonomii (regula z 07.10: ekonomie projektuje Claude), a fabula sie nie zmienia, bo wojny ROT trwaja jak dotad.
- A wymaga tylko jego zgody na zmiane w jego pliku MCM w Documents (gra zamknieta, kopia .bak) albo przestawienia suwaka przez niego. B i C wymagaja zwyklego "wgraj".
- Decyzji Jeffa wymagaloby dopiero wylaczenie wojen fabularnych ROT (przycisk Edit Storyline Wars) albo zmeczenia wojna w Diplomacy. Tego nie proponuje.

DLA JEFFA:
Te 11 milionów „trybutu” to w rzeczywistości jednorazowe reparacje wojenne z moda Diplomacy. Przegrane królestwo spłaca je w 3 dni z sakiewek swoich lordów i to złoto po prostu znika, a zwycięzca dostaje całą kwotę od razu, wyczarowaną z niczego. Główny winowajca to błędne koło: Diplomacy wymusza pokój, gdy wojna wyczerpie stronę, a Realm of Thrones jeszcze tej samej doby wypowiada tę wojnę fabularną od nowa. Dlatego np. Stormlands i Dragonstone płaciły sobie nawzajem 4 razy w 120 dni, a 93% całej kwoty pochodzi z takich „pokojów” trwających najwyżej dobę. Najprostszy ruch to przestawić w MCM Diplomacy „Scaling War Reparations Gold Cost Multiplier” z 50 na 10: reparacje spadną pięciokrotnie, typowy lord zapłaci około 3 dni dochodu zamiast 17, a długi wobec korony z tego powodu prawie znikną. Potem zrobiłbym małą łatkę, żeby za wojnę, którą ROT i tak od razu wznawia, nie było reparacji wcale. Ani przebieg wojen, ani fabuła się nie zmieniają, więc nie potrzebuję Twojej decyzji, tylko zgody na zmianę w Twoim pliku ustawień (albo przestawisz suwak sam).