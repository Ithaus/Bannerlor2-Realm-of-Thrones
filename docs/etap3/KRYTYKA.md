# KRYTYKA - PROJEKT-ETAP3-SZCZELNOSC-STATKI-2026-10-10 (E3/0 ... E3/12) - 10.10.2026

Tylko odczyt: kod `noc2\e2c` (skrot **A:** = `e2c\Armoury\src`), dekompilacje `scratchpad\e3\dec\tcs` (gra 1.4.8, **gra:**), `dec\` (Naval, BK estates),
`dec\bkproj` (BK), `dek\` (gra/BK/DTE), galaz 3.2 `scratchpad\e3\32\Armoury\src` (**3.2:**). Log biegu 457 dob i CSV `kasy-miast.csv`, `budzet-rodow.csv`.
"w. N" = linia projektu. Nic nie zmienione ani nie budowane. Skrypty krytyki: `projekt31\py\krytyka\` (opis w rozdz. 5).

## 0. Wniosek (krotko)

1. **Liczba dla Jeffa jest zla.** Rachunek wojska (B: 91.9 tys., -7.6%) zaklada, ze porty zawsze zaplaca cale 30%. Przy regule projektu
   (25% nadwyzki, statek po statku) porty placa ok. 22-27% naleznego - wojsko po roku ok. **85 tys. (-14%)**, na samej podlodze. Do tego
   blad danych: "srednia nadwyzka portu 2.66 mln" to stosunek kasa/cel (2.65), naprawde 84 tys.
2. **E3/8a tworzy zloto z niczego:** pan miasta juz dzis dostaje podatek warsztatow BK (BK dopisuje go do podatku miasta, Armoury celowo go zostawia).
   Wplata do kasy miasta = drugi raz.
3. Gracz nie ma jednej reguly w dwoch miejscach: jency u posrednika (brak platnika) i awanse z ekranu druzyny (dalej w nicosc).
4. Port ponizej zapasu kupcow (gracz "z calej kasy", wymiana statku AI) = dosypka regulatora z niczego.
5. Reszta projektu (RealPayer, kolejnosc w bitwie, okna ksiegi, portfele wezwan, majatki BK co do zasady) jest w kodzie poprawna - lista w rozdz. 4.

---

## 1. KRYTYCZNE

### K1. Rachunek wojska 181 i pytanie do Jeffa - liczone tak, jakby porty placily zawsze 100% naleznego
**Co nie tak.** Rozdz. 0 pkt 11, rozdz. 4 (w. 367-406) i pytanie do Jeffa opieraja sie na wariancie B = "port placi 30%". W skrypcie to
`run(0.7)` (`scratchpad\e3\skrypty\statki_warianty.py`, wariant B) - 30% zawsze dochodzi do rodu. Ale E3/10 (w. 296-300) placi z **puli 25% nadwyzki**,
jeden port na statek, reszta "sprzedaz z koniecznosci" albo zlom - tej granicy rachunek nie zna.
**Dowod (symulacja na tych samych danych):** przypisanie zlota statkow do rodow z `statki_baza.py` (te same doby i rody), kasy 53 portow z
`kasy-miast.csv` (porty = osady z `port_posX` w `ROT-Map\ModuleData\settlements.xml`), nadwyzka = kasa - cel_regulatora - pod_tarcza, statek = 35 tys.
wartosci (C 1.3), najbogatszy port krolestwa pierwszy (na korzysc projektu: bez odleglosci, bez limitu stoczni, bez wspolnego licznika z jencami):

| granica wyplaty portu | nalezne (30%) | zaplacone | % |
|---|---:|---:|---:|
| 25% nadwyzki (stan kasy z konca doby) | 91.3 tys./d | 20.0 tys./d | **21.9%** |
| 1/3 nadwyzki (= 25% nadwyzki sprzed regulatora) | 91.3 | 24.2 | 26.5% |
| cala nadwyzka | 91.3 | 41.7 | 45.6% |
| cala nadwyzka sprzed regulatora | 91.3 | 45.7 | 50.0% |

Kontrfakt wojska (`run()` z `statki_warianty.py`, skala = 1 - 0.3 x zaplacone), wojsko lordow w wojnie:

| wariant | d430-457 | d365-457 | rody w wojnie z G<0 (d430-457) |
|---|---:|---:|---:|
| B z projektu (100% wyplat) | 91.9 tys. (-7.6%) | -8.2% | 15.8 |
| **pula 25% (regula projektu)** | **85.3 tys. (-14.2%)** | **-15.0%** | **25.7** |
| cala nadwyzka | 87.5 tys. (-12.0%) | -12.8% | 21.7 |
| 70% naleznego (prog testu w. 429) | 89.7 tys. (-9.7%) | -10.7% | 18.7 |
| A (port nic nie placi) | 83.7 tys. (-15.8%) | -16.8% | 32.4 |

- Najwieksza pula 25% portu w krolestwie (srednia d430-457): Reach 9.5 tys., Riverlands 6.7 tys., KL 15.6 tys., Stormlands 15.6 tys., Volantis 9.6 tys.
  - przy statku 0.3 x ok. 35 tys. = ok. 10.5 tys. i flocie ok. 33 tys. Glowni odbiorcy zlota statkow (KL, Reach, Riverlands) placa 5-20% naleznego.
- **Blad danych** (w. 71, C 5): "srednia nadwyzka 2.66 mln" to kolumna `kasa_do_celu` (stosunek kasa/cel - The Eyrie 2.65 w pierwszym wierszu CSV).
  Doba 109293: srednia nadwyzka 84 404, mediana 41 301, max 441 357; po tarczy srednia 42 568, mediana 27 735. Upada uzasadnienie "665 tys./d puli
  wobec 115 tys./d kosztu" (C 5) i "porty placa ok. 91 tys./d" (rozdz. 3) - placa ok. 20-24 tys./d.
- Progi sa sprzeczne: test (w. 429) "zaplacone >= 70% naleznego" wobec prognozy (w. 405-406) ">= 0.92 x bez 181" = 91.4 tys. - przy 70% wychodzi 89.7.

**Poprawka.** Przed pytaniem do Jeffa: przeliczyc kontrfakt z symulacja puli (dzienne pule portow z CSV, ten sam wybor portu co w kodzie, limit stoczni,
wspolny licznik z jencami); podac Jeffowi prawdziwe liczby (ok. -12..-14% po roku, ok. 85-88 tys., ok. 20-25 biednych glow - gorna granica).
Rozwazyc R1 = R2 (patrz W4: port kupuje statek z calej nadwyzki) - i tak ok. 50%. Przy takich liczbach rada (a) traci podstawe: uczciwe opcje to
(b) statki razem z 184 w etapie 5 albo (a) z jawnym ok. -14% (Jeff 09.10 10:45 odrzucil juz "-4% wojska" jako staly spadek). Oba progi testu
(zaplacone, prognoza) ustawic z tej samej symulacji.

### K2. E3/8a - podatek warsztatow BK liczony drugi raz (zloto z niczego ok. 50 tys./d)
**Co nie tak.** Projekt (w. 266-270, za B 2.8) przyjmuje, ze podatek warsztatow notabli "nie trafia do nikogo". Trafia - do pana miasta.
**Dowod.**
- BK `BKTaxModel.CalculateTownTax` (`dec\bkproj\BannerKings.Models.Vanilla\BKTaxModel.cs:198-205`) dopisuje do podatku miasta "Taxes from {WORKSHOP}"
  = `GetWorkshopTaxes(w)` dla kazdego warsztatu, ktorego wlasciciel nie jest glowa rodu pana (takze notable).
- Armoury zeruje podatki klas, ale TE linie zostawia celowo: A: `PopulationLaw.cs:226-231` ("zostaje to, co ktos naprawde placi: podatek od cudzych
  warsztatow (BK pobiera go od wlascicieli)"), `KeptTownLines` A: `PopulationLaw.cs:257-261` (suma `GetWorkshopTaxes`), x (1 - 0.6 x autonomia) :276-279.
- `GetWorkshopTaxes` = to samo `TaxExpenses()`, ktore BK odejmuje notablowi (`dek\BKClanFinanceModel.cs:28-36`, slownik wypelniany :39-50).
- Czyli dzis para: notabl -> nicosc (K1 - wyplata, 54 tys./d) i nicosc -> pan (w "rozliczenia rodow na plus"). B 2.8 sprawdzilo tylko `AddIncomes`.
  E3/8a dokladalby trzecia noge (`Town.ChangeGold(podatek)`): pan dostaje z modelu i drugi raz zaworem, reszte kasuje regulator.
- Nawet we wlasnej logice kwota jest za duza: gra wyplaca notablowi tylko `if (num > 0)` (`dek\ClanVariablesCampaignBehavior.cs:479-486`), podatek =
  ProfitMade x stawka (`bkproj\BannerKings.Extensions\WorkshopExtensions.cs:14-16`), wyplata = ProfitMade / 2 (`BKClanFinanceModel.cs:39`) - zniklo
  naprawde min(suma podatku, wyplata brutto); przy stawce > 0.5 albo przy zaulkach (+30 z niczego, `DefaultClanFinanceModel.cs:899-905`) suma podatku
  > tego, co zniknelo.
**Poprawka.** Jedna noga, nie trzy: (a) skreslic E3/8a i w ksiedze nazwac pare "podatek warsztatow BK: notabl -> pan przez model" (reszta = autonomia
i min) albo (b) prawdziwy platnik: w `TownTaxPostfix` usunac linie warsztatow i przelac panu (GiveGoldAction) dokladnie to, co zniknelo notablowi
(min z migawki kapitalu warsztatow i kas karawan przed/po). R7 "do kasy osady" tylko razem z usunieciem linii i z przeliczeniem D panow miast
(spadek dochodu ok. 40 tys./d - zmienia wojsko, K1).

---

## 2. WAZNE

### W1. Gracz: jency u posrednika - E3/1 nie ma platnika
**Dowod.** Posrednik: `SellPrisonersDoneHandler` -> `ApplyForSelectedPrisoners(MainParty, null, ...)` (gra: `Helpers\PartyScreenHelper.cs:614-617`), "sprzedaj
wszystkich" (gra: `PlayerTownVisitCampaignBehavior.cs:590-594`) - kupujacy null; w `ApplyInternal` `settlement = sellerParty.Settlement ?? buyerParty?.Settlement`
(`dek\SellPrisonersAction.cs:12`) = null dla partii ruchomej. Projekt (w. 163-164) bierze platnika z `buyerParty.Settlement`/`settlement`.
**Poprawka.** Kupujacy null i sprzedajacy ruchomy -> platnik `Settlement.CurrentSettlement` (osada, w ktorej stoi partia). Ekran posrednika pokazuje
pelna cene (tryb Ransom, `PartyScreenHelper.cs:205`, wycena `PartyScreenLogic.cs:826`) - dopisac komunikat "the town could pay only {GOLD}" albo obciac wycene.

### W2. Gracz: awanse z ekranu druzyny dalej w nicosc - E3/5 dotyczy tylko AI
**Dowod.** `UpgradeReadyTroops` pomija gracza (`dek\PartyUpgraderCampaignBehavior.cs:170`). Gracz placi w `PartyScreenLogic`: koszt do `PartyGoldChangeAmount`
(gra: `PartyScreenLogic.cs:922`), potem `GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, kwota < 0)` (:1258) = bohater -> nicosc.
**Poprawka.** Postfiks `PartyScreenLogic.DoneLogic` (juz latany: A: `Stables.cs:1107`, `GarrisonArmory.cs:1096`) - suma kosztow awansow z tego ekranu ->
`MenPurse.Add(MainParty)`; ta sama regula co AI.

### W3. E3/11: kasa portu ponizej zapasu = dosypka regulatora z niczego
**Dowod.** Regulator: `GetTownGoldChange = round(0.25 x (10 000 + 12 x dobrobyt - kasa))` (gra: `DefaultSettlementEconomyModel.cs`, linie 75-78 dekompilacji) -
ponizej celu dosypuje. Projekt w. 320-322 "Gracz: port placi z calej kasy" - sprzeczne z w. 89 ("zakup - z calej nadwyzki") i w. 472 ("osada nigdy nie
schodzi ponizej zapasu"). Inne sciezki "port kupuje" bez bramki `ConsiderSellingShips`: wymiana statku przy zakupie AI (`dec\ShipTradeCampaignBehavior.cs:167-170`),
karawany (`38dec\...CaravansCampaignBehavior.cs:831`). `GiveGoldAction` obcina do CALEJ kasy osady (gra: `GiveGoldAction.cs:21-25`), nie do nadwyzki. Prefiks
na `ConsiderSellingShips` nie zna portu - gra losuje go w srodku (`GetTownToSellShip`, `ShipTradeCampaignBehavior.cs:371-374`).
**Poprawka.** W kontekscie RealPayer dla `ApplyByTrade` (port kupuje) limit = nadwyzka (Spare(port, zakup)) dla AI i gracza. Bramke "AI nie sprzedaje bez
pieniedzy" dac jako prefiks bool na `ChangeShipOwnerAction.ApplyInternal` (pomija cala transakcje); przy wymianie w `TryPurchasingShipFromTown` pominac tez
zakup (inaczej 4. statek ponad limit).

### W4. Jedno zjawisko "port kupuje statek" - dwie ceny i dwie granice
**Dowod.** E3/10 (w. 294-295): `GetShipTradeValue(statek, druzyna, null) x 0.3`, pula 25%. E3/11 i dobrowolna sprzedaz: `GetShipTradeValue(statek, sprzedajacy,
port)` = wartosc x 0.3 - koszt naprawy, z politykami krolestwa (`dec\NavalDLCShipCostModel.cs:45-59`), cala nadwyzka. W. 386 "to tez dokladnie cena zwyklej
sprzedazy" - nie jest (bez naprawy uszkodzony statek wart wiecej przy rozbiciu niz przy sprzedazy; gracz moze to wykorzystac rozwiazujac druzyne towarzysza).
**Poprawka.** E3/10: cena = `GetShipTradeValue(statek, druzyna.Party, port.Settlement.Party)` i ta sama granica co R2 (cala nadwyzka). Jedna regula i lepszy wynik K1.

### W5. Druzyny rozwiazane (niszczyciel null) - kiesa dalej znika; etykieta U8 zajeta
**Dowod.** Rozwiazanie: `DestroyPartyAction.ApplyForDisbanding` -> `OnMobilePartyDestroyed(party, null)` (gra: `DestroyPartyAction.cs:34-41`, :23). 3.2 zostawia
to "poza regula ... jak dotad znika" (3.2: `BeatenPurse.cs:280`, przypadki "odd" :290). E3/7 lapie tylko `OnPartyRemoved` bez `MobilePartyDestroyed`
(w. 255-257, 138-141). R10 (w. 105) obiecuje "usuniecie, rozwiazanie". U8 juz istnieje = "kiesy partii bez wodza, ktore zniknely z mapy"
(A: `MoneyLedger.Obieg.cs:348`) - nowa "U8 usuniete bez zniszczenia" (w. 140, 263) i prog w. 422 sa niejednoznaczne.
**Poprawka.** E3/7 takze dla `MobilePartyDestroyed` z niszczycielem null i dla "odd" z 3.2 (kiesa -> glowa rodu wlasciciela, brak - najblizsze miasto;
sakiewka - miasto). Nowa pozycja z nowa etykieta (np. "U8r").

### W6. NavalDLC wola zwrot za statki dwa razy przy rozwiazaniu na ladzie
**Dowod.** `NavalShipDistribution.cs:24-25` slucha `OnPartyDisbanded` i `MobilePartyDestroyed`; oba wolaja `DistributePartyShipsAndRecoverGold` (:28-48);
`ApplyForDisbanding` wysyla oba zdarzenia po kolei (gra: `DestroyPartyAction.cs:40-41` -> :23); `RecoverGold...` nie zabiera statkow (:50-70), wiec drugie
wywolanie widzi te same statki. Czesc dzisiejszych 304 tys./d to podwojna wyplata - kontrfakt (K1) liczy 30% z kwoty zawyzonej.
**Poprawka.** E3/0: licznik zwrotu osobno z obu zdarzen (ta sama partia). E3/10 jest odporne dzieki przeniesieniu statkow przy 1. wywolaniu, ale
w kroku (3) "statek zostaje" liczniki zlomu licza 2x - oznaczac partie w dobie. Kontrfakt przeliczyc bez podwojenia.

### W7. E3/2: `LastIncome` skacze albo rosnie bez konca
**Dowod.** `AccumulateTradeTax` robi `LastIncome += ...` (`dec\EstateData.cs:96-97`); jedyne zerowanie bylo `LastIncome = num11` w `DailyProductionIncome`
(:181-187), ktore wylaczamy. Zerowanie co dobe (w. 187) -> "Estimated daily income" 0 w wiekszosc dni, cena majatku `LastIncome x 364`
(BKEstatesModel.cs:472-505, A 3.3) skacze od 0 do wielokrotnosci; zerowanie jednorazowe -> narasta w nieskonczonosc.
**Poprawka.** Wlasna srednia 28 dob udzialu z utargu na majatek (slownik w Armoury), w prefiksie `LastIncome = srednia`; postfiks gettera - ta sama srednia.

### W8. E3/5 i E3/6 oddaja kwote nominalna, a gra pobiera mniej, gdy w kiesie brak
**Dowod.** GiveGoldAction obcina do kiesy platnika (gra: `GiveGoldAction.cs:11-14`). Awans: gra sprawdza zloto wodza (`PartyUpgraderCampaignBehavior.cs:113-120`),
a placi Owner (:141-145) - moze miec mniej. Werbunek: A: `LevyGold.cs:35, 40` oddaje `cost` z modelu, gra pobiera `GiveGoldAction(LeaderHero, null, cost)`
z obcieciem (`dek\RecruitmentCampaignBehavior.cs:624-628`); E3/6 (w. 242) "tym samym wywolaniem modelu" to dalej kwota nominalna.
**Poprawka.** Oddawac kwote z migawki kiesy platnika przed/po (albo z `OnHeroOrPartyTradedGold` w otwartym oknie) - inaczej przy pustej kiesie zloto z niczego.

### W9. Rachunek wojska obejmuje tylko E3/10
**Dowod.** W. 380-382 - pozostale kawalki "razem ok. 0 +-1% w 120 dobach", bez liczb na rok. Brak E3/12 (pryzy - nieznane), E3/3 (2/3 lupu z cial do
sakiewek + sufit sakiewki przegranych), E3/1 (jency przy pustej nadwyzce); po stronie plus E3/4 + E3/5 + E3/8a (ok. 216 tys./d do kas miast, z tego
zaworem do panow miast rzedu 20-25%, ok. 45 tys./d D - a po K2 E3/8a odpada).
**Poprawka.** Jeden kontrfakt calego etapu (wszystkie zmiany dochodu rodow) przed pytaniem do Jeffa; E3/12 po pomiarze E3/0.

### W10. Proces: 13 kawalkow w jednym DLL i jednym tescie
**Dowod.** W. 40, 121, 412. OB rozdz. 12 ("najpierw pomiar, potem ujscia, potem zrodla"), B 5 pkt 1 ("Cel: reszta < 10 tys. przed zmianami zlota
(zasada Z9)"), CLAUDE.md zasada 2. Cel etapu (R < 10 tys.) zalezy tylko od E3/0; jesli trop DTE sie nie potwierdzi, R po 12 zmianach zlota trudno rozdzielic.
**Poprawka.** E3/0 sam (40 dob) -> potwierdzenie R (albo plan B) -> grupa E3/1-E3/9 -> E3/10-E3/12 po decyzji Jeffa (z poprawionymi liczbami K1).

### W11. E3/8b - zly odbiorca dla dwoch-trzech z pieciu oplat
**Dowod.** "Dwor BK" = koszt zmiany poziomu wydatkow dworu (`bkproj\BannerKings.Managers.Court.Grace\CourtGrace.cs:375-382`) - wydatek na dwor;
Armoury: dwor -> kasa siedziby (162, OB 12 w. 6). Przeniesienie dworu (`MoveCourtDecision.ApplyGoal`, `bkproj\BannerKings.Managers.Goals.Decisions\MoveCourtDecision.cs:101-110`)
zna nowe miasto. Pasowanie: stary rod placi do 50 000 przy tworzeniu rodu rycerza (`bkproj\BannerKings.Behaviours\BKKnighthoodBehavior.cs:228-235`) - to
wiano nowego rodu.
**Poprawka.** Dwor -> kasa siedziby, przeniesienie -> kasa nowej siedziby, pasowanie -> glowa nowego rodu; tytuly (TitleManager.cs:562, 586, 620, 727)
i dylematy (BKDilemmaBehavior.cs:399-406) -> skarbiec (OK).

### W12. E3/8d zamyka ujscie bez zrodla
**Dowod.** W. 277-278: "zrodla tego dochodu nie ustalilem"; zold nieumarlych 4.8 tys. (log:123779, d457) - 10.5 tys./d dzis rownowazy nieznany dochod.
Zamkniecie samego zoldu zamienia pare "z niczego -> w nicosc" na gromadzenie w kiesach rodow Innych (zloto swiata +5-10 tys./d).
**Poprawka.** Najpierw ustalic zrodlo w kodzie (model finansow rodow bez lenna / ROT) i zamknac obie strony albo zadnej; prog 1 000 zl/d za pozno.

### W13. E3/4: sakiewka karawany rozbitej przez bande idzie do miasta, a kiesa - do bandy (dwie reguly, [D] 09.10 04:55 Q3b)
**Dowod.** A: `MenPurse.cs:99-117` - zwyciezca bez bohatera -> najblizsze miasto; 3.2: `BeatenPurse.cs:300-305` - kiesa karawany do bandy
(`IsPartyTradeActive`). Q3b: "jedna regula (gracz, AI, bandyci)". Kwoty male (sakiewka = zold od ostatniego miasta), ale regula Jeffa.
**Poprawka.** W `MenPurse.OnPartyDestroyed` zwyciezca bez bohatera, ale handlujacy -> jego `PartyTradeGold` (jak 3.2).

---

## 3. DROBNE

- **D1. Pula 25% w zamkach nie jest "pieniedzmi regulatora".** Uzasadnienie w. 176-178 nie dziala dla zamkow: regulator zamku tylko dosypuje
  (A: `CastlePurse.cs:27, 280-282`), nadwyzka zamku to pieniadze pana (zawor 7%). Jency w zamkach (`PartiesSellPrisonerCampaignBehavior.cs:24-38` - kazda twierdza)
  i lup z oblezenia zamku placi wtedy pan. Uzyc `CastlePurse` (Spendable/Reserve) - jedna regula wydatkow kasy zamku (naglowek CastlePurse pkt 3b).
- **D2. `ApplyForSettlementToCharacter` nie obcina do kasy osady** (gra: `GiveGoldAction.cs:52-55` -> :11-14 z ujemna kwota). W. 156 "gra obcina kwote"
  dotyczy tylko sciezki RealPayer (giverParty). E3/10 musi sam pilnowac kwoty <= pula.
- **D3. E3/3 a okno O16:** prefiks okna ma `Priority.First` (A: `CirculationWindows.cs:1685`) i mierzy `mp.PartyTradeGold` (:783-806). Prefiks E3/3 musi miec
  nizszy priorytet (inaczej "LootFin odejmuje" da wynik ujemny). Odejmowac tylko to, co weszlo do `mp.PartyTradeGold` (u lorda = kiesa wodza,
  gra: `MobileParty.cs:425-433`); zwyciezca nie-lord z bohaterem (karawana) - gra daje do kasy partii, wiec 1/3 tez do kasy partii, nie do kiesy bohatera.
  Dopisac `ClanIncomeBook.NoteInflow(..., KThird)` jak `MenPurse.cs:104`.
- **D4. E3/3 bez 3.2 = bez 1/9** (w. 203) - wbrew [D] 09.10 03:45 nr 18; PLAN 3.1 "zalezy od 3.2" - zrobic E3/3 zalezne od 3.2.
- **D5. E3/5: awans zalogi do sakiewki zalogi omija limit K1** (`SoldierPay.GarrisonShare`, MenGearSaveDays, A: `SoldierPay.cs:491-501`) - zaloga nie
  wyjezdza z miasta, sakiewka rosnie. Nadwyzke ponad limit - do kasy osady zalogi (jak `coffers`).
- **D6. E3/7: konwoj DTE z kapitalem 0 dostanie od rodu doplate do 5 000** (gra: `DefaultClanFinanceModel.cs:106` `PartyGoldLowerThreshold`, :826-859) -
  rod wyklada ok. 5 000 na konwoj, wraca przy usunieciu (E3/7). Zgodne z R9, ale wpisac w log i do kontroli budzetu 166.
- **D7. E3/7: tworzenie karawany przez gracza znalezione:** `CaravanConversationsCampaignBehavior.cs:243-252` (gra) - gracz placi cene do kasy miasta
  (`ApplyForCharacterToSettlement`), kapital z niczego przez `CreateCaravanParty` -> `GetInitialTradeGold` (`dek\CaravanPartyComponent.cs:247`). Regula R9:
  kapital z zaplaty - przelew miasto -> karawana min(kapital, cena) w tym samym kontekscie.
- **D8. E3/0: portfel max(0, ...) a krolestwo naraz wzywajace i wezwane** - jedno pole netto (A: `CrownCallToWar.cs:248-250`): wplata do ujemnego portfela
  nie jest posiadaczem i nie trafi do rodow (gra: `DefaultClanFinanceModel.cs:630`). Policzyc osobno ("wplaty do portfela ujemnego").
- **D9. E3/0: oznaczanie id w `OnPartyDestroyed` przed wczesnymi `return`** (A: `CirculationWindows.cs:1504-1507` - brama, `!IsActive`, partia lorda).
- **D10. Test "karawany i inne partie -> 0"** (w. 227-228): `_dOther` zostaje dla innych partii (A: `SoldierPay.cs:487`) - prog "karawany -> 0".
- **D11. E3/12 przy fladze gry false** (zwyciezca bez bohatera, `dek\MapEvent.cs:1783, 1799-1802`) gra nie placi nikomu - zostawic zniszczenie, inaczej port
  dostaje statek za darmo.
- **D12. Komunikat E3/10** "...party has disbanded" takze dla druzyny rozbitej w bitwie - osobny tekst ("was lost").
- **D13. R11 nie jest na 100%:** glowa rodu przekazuje zloto nastepcy (gra: `ChangeClanLeaderAction.cs:24`), ale rod bez nastepcy i wedrowcy bez rodu - przepada.
  Dopisac do listy "zostaje" albo objac regula.
- **D14. E3/9 "w kazdym miescie krolestwa"** - audyt 12 D-9 mowil "w najblizszym miescie"; swiadoma zmiana - napisac.

---

## 4. Sprawdzone w kodzie - zgadza sie

- RealPayer: `GiveGoldAction.ApplyInternal` z platnikiem-osada obcina do `SettlementComponent.Gold` (gra: `GiveGoldAction.cs:21-25`), zdarzenie idzie
  z prawdziwym platnikiem (:39); ksiega odwraca ujemne kwoty (A: `MoneyLedger.cs:291`); wplata od osady trafia do `TodayEvtSettl` (A: `ClanIncomeBook.cs:166`).
- Zapas kupcow = cel regulatora 10 000 + 12 x dobrobyt (`DefaultSettlementEconomyModel`; A: `CastlePurse.Reserve` :191-199).
- Jency: kwota gry obejmuje bohaterow (`SellPrisonersAction.cs:60-64`) i jest wyplacana jednym zdarzeniem (:83 / :88) - rozdzielenie w finalizerze konieczne
  i wykonalne; `flag` (dar bohatera) blokuje zaplate. RC/178 przy sprzedazy gracza nie uzywaja GiveGoldAction (A: `Ransom178.cs:516-560`) - RealPayer ich nie zlapie.
- Lup z cial: galaz AI `PartyTradeGold += num` (`MapEvent.cs:1864-1868`), lupienie (:1423) przed `CommitGoldChanges` i przed zniszczeniem partii -
  `MenPurse.OnPartyDestroyed` odda tylko reszte (bez podwojnej wyplaty).
- Pryzy: `num` liczone przed `DestroyShipAction`, dopisek do `PlunderedGold` po petli (`MapEvent.cs:1786-1817`); 3.2 dziala wczesniej
  (`CalculatePlunderedAndLostGoldAmounts`) - postfiks E3/12 nie koliduje.
- `DestroyPartyAction`: `MobilePartyDestroyed` przed `RemoveParty`, `OnPartyRemoved` synchronicznie po `IsActive = false` (gra: `MobileParty.cs:3516-3534`) -
  zbior id w dobie dziala.
- BK `RunWeekly` wykonuje od razu (`dek\BannerKingsBehavior.cs:10-16`) - kontekst E3/7 w `OnSettlementEntered` obejmuje zakup karawany.
- Turniej: zloto tylko dla AI (`TournamentManager.cs:189-192`); `OnTournamentFinished` (pula GrandTourney) poza kontekstem (:149-151).
- Portfele wezwan: gra wyplaca rodom tylko z dodatniego portfela (`DefaultClanFinanceModel.cs:630-638`); "rozliczenia rodow" mierzone stanem swiata -
  posiadacz HCallToWar sam zamieni te wyplate w przelew (jak w w. 136-137).
- Zold karawan notabli bez GiveGoldAction (`dek\NotablesCampaignBehavior.cs:309-331`) - finalizer z migawka (jak w projekcie) jest jedyna droga.
- Udzial majatkow z utargu (`EstateData.cs:80-104`) - E3/2 go nie dotyka; podwojnej wyplaty przez model BK nie ma (`dek\BKClanFinanceModel.cs:70-74` zwraca 0 przy wyplacie).
- Okna 169 maja prefiksy void z `Priority.First` (A: `CirculationWindows.cs:1685`) - kontrolki zostaja przy prefiksach bool.

## 5. Pliki

- `projekt31\py\krytyka\pula_statki.py` - symulacja wyplat portow (argumenty: `skrypty\statki_baza.py`, log, `budzet-rodow.csv`, `kasy-miast.csv`,
  `ROT-Map\ModuleData\settlements.xml`).
- `projekt31\py\krytyka\wojsko_pula.py` - kontrfakt wojska z `statki_warianty.py` dla skal 0.7 / 0.934 / 0.863 / 0.79 / 1.0.
- `projekt31\py\krytyka\porty.py`, `porty2.py` - pule portow wedlug krolestw, srednia i mediana nadwyzki (blad 2.66 mln).
