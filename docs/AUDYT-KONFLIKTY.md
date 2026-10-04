# Audyt konfliktow: nasze latki vs gra i inne mody, rzeczy liczone dwa razy (2026-10-04)

Tylko odczyt - zadnych zmian w kodzie ani ustawieniach. Stan kodu: commit 533b174 (wpis CHANGELOG 44).

Dowody:
- nasz kod: `Armoury/src/`, `CrashScribe/src/`, `GrandTourney/src/` (numery linii z dzisiejszego stanu);
- dekompilacje z audytow 20.09 (scratchpad `.../scratch-2026-09-20-76570d/.../scratchpad/{tw,bk,be,rot,nv,rbl,bkrot}`)
  plus swieza dekompilacja `ArtemsLivelyAnimations.dll` i `BetterEconomy.dll` (ilspycmd 9.1). Wersje BK/BEE/RBL moga sie roznic
  od 20.09 - tam, gdzie to ma znaczenie, zaznaczam;
- `AIInfluence.dll` jest zaciemniony (ilspy: "Illegal tables in compressed metadata stream") - nie da sie go przeczytac;
- logi: `Modules/Armoury/Armoury-2026-10-04_13-00-48.log`, `Documents/.../CrashScribe/session-2026-10-04_13-00-46.log`.
  UWAGA: ten test byl PRZED wpisem 44 (popyt w nowej monecie, StartGoldAdventurer) - wpis 44 nie ma jeszcze zadnego logu.

Oznaczenia: **POTWIERDZONE** = widziane w kodzie (naszym i zdekompilowanym), **PODEJRZANE** = wniosek bez pelnego dowodu.

Rownolegle powstal `docs/AUDYT-EKONOMIA-KROLESTW.md` (13:41) z tabela zrodel zlota Z1-Z13 - tu odsylam do niej i opisuje
tylko to, gdzie NASZE systemy z tymi zrodlami sie zazebiaja.

---

## 0. Najwazniejsze w szesciu zdaniach

1. Trzy nasze latki bez licznika zagniezdzenia siedza na modelach, ktore inne mody OWIJAJA (wolaja model wewnetrzny):
   rozrzut broni miotanej idzie x2.5 do 4 razy (x39, z konia x625), wzrok w nocy -35% dwa-trzy razy (do zera),
   XP z ciosu (FairXp) dwa razy - arena i turniej daja 0 XP albo ujemne.
2. Ceny broni: hipoteza "BEE x0.3-3" jest FALSZYWA - model cen BEE nie jest zarejestrowany (BK obecny); lancuch to
   vanilla (0.8-1.3 x kara handlu) -> [AIInfluence?] -> nasza polka (0.25-2) x indeks surowcow (0.5-1.5), licznik dziala raz.
   Ale nasze PODLOGI 5% (ScrapFloor, MarketGlut) sa liczone PRZED polka, wiec polka je mnozy - podloga nie jest podloga.
3. Indeks surowcow jest binarny (1.00 albo 1.50) - ruda i drewno maja Value = 1, a cena kupna to sufit liczby calkowitej
   (log: "indeks rudy min/med/max 1.00/1.50/1.50" codziennie).
4. Zloto: kazda wyplata z kasy MIASTA ponizej celu 10 000 + 12 x dobrobyt jest w 25% dziennie dosypywana przez vanille -
   WorkshopLaw (miasto placi za wyroby 0.24-0.59 mln/dzien w logu) i hurt SupplyDemand (1.1-1.6 mln/dzien) zamieniaja to w pompe,
   a renta PopulationLaw zbiera nadwyzke u sprzedajacych. Renta od ludnosci jest liczona trzy razy (BK podatek z niczego +
   BEE renta posiadlosci/skarbiec z niczego + nasza renta), a powinnosci wobec korony dochodza do podatku BK dla suzerena.
5. Zima: CrashScribe wylacza na twardo pory roku RBL (`SeasonsEnabled` = false), a WinterSource/WesterosClimate zakladaja,
   ze RBL dalej daje zima kare do marszu i morale - dzis zima NIE spowalnia nikogo i nie rusza morale; dziala tylko leczenie
   i dobrobyt RBL (nie sa bramkowane) plus nasz WinterBite.
6. Trzy ludnosci nadal: BK PopulationData (pobor, nadwyzka rak), PopulationLaw (hearth x 21-454 ludzi) i pula wyrzutkow
   (0.5 hearth na czlowieka) - jeden wyrzutek zabiera z bazy renty 10-227 "ludzi", a werbunek nie zabiera z niej nikogo.

---

## 1. WYSOKIE

### W1. Rozrzut broni miotanej mnozony w calym lancuchu modeli - POTWIERDZONE
- `Armoury/src/Patches.cs:514-524` (`ThrownWobblePatch.Postfix`: `__result = Math.Max(__result, 0.002f) * f`, f = 2.5,
  z konia x2), rejestracja `Patches.cs:540-553` na KAZDYM `AgentStatCalculateModel.GetWeaponInaccuracy`, bez licznika zagniezdzenia.
- Log Armoury :12-18: wpiete w 7 modelach, w kampanii: `SandboxAgentStatCalculateModel`, `NavalAgentStatCalculateModel`,
  `RoTElephantAgentStatCalculateModel`, `ArtemsLivelyAnimationsAgentStatCalculateModel`.
- Owijanie: Naval `nv/NavalDLC.GameComponents/NavalAgentStatCalculateModel.cs:495-497` (`BaseModel.GetWeaponInaccuracy`),
  Elephants `rot/RoT_Elephants/RoTElephantAgentStatCalculateModel.cs:107-109` (`_previousModel...`),
  Artems (swieza dekompilacja) `GetWeaponInaccuracy => _previousModel.GetWeaponInaccuracy(...)`.
- Skutek: przy czterech poziomach mnoznik 2.5^4 = 39 (z konia 5^4 = 625) zamiast 2.5/5. Oszczepy i toporki praktycznie nie trafiaja.
- Naprawa: licznik zagniezdzenia jak w `SupplyDemand`/`Rations` (prefix Priority.First ++, finalizer --, postfix tylko przy 1).

### W2. Wzrok dzien/noc liczony 2-3 razy - POTWIERDZONE
- `Armoury/src/SightRange.cs:21-33` (`AddFactor(f-1)`, dzien +0.15, noc -0.35), `:46-58` latka na kazdym `MapVisibilityModel.GetPartySpottingRange`, bez licznika.
- Log :67 "Wzrok za sloncem ... (3 modeli)". Owijanie: `nv/NavalDLC.GameComponents/NavalDLCMapVisibilityModel.cs:56`
  (`BaseModel.GetPartySpottingRange`), `bkrot/BKROTPatch.Models/BKROTMapVisibilityModel.cs:13,29-33` (dziedziczy po Default i wola base).
- Skutek: noc -0.35 x 2 = -0.70 (x0.30) albo x3 = -1.05 (zasieg <= 0, zostaje tylko dolny limit gry, jesli jest); dzien +30-45% zamiast +15%.
  AI i gracz w nocy prawie slepi - zasadzki, ucieczki i poscigi rozstrzyga ten blad.
- Naprawa: licznik zagniezdzenia.

### W3. FairXp: nauka z ciosu liczona dwa razy - POTWIERDZONE w kodzie, krotnosc PODEJRZANA
- `Armoury/src/FairXp.cs:30-59` (`AddFactor(arena 0.2 - 1)`, `AddFactor(turniej 0.5 - 1)`, bitwa `AddFactor(waga - 1)`), `:81-87` latka
  na kazdym `CombatXpModel.GetXpFromHit`, bez licznika. Log :21 "w 3 modelach".
- `nv/NavalDLC.GameComponents/NavalDLCCombatXpModel.cs:22-28` wola `BaseModel.GetXpFromHit` (vanilla). Trzeci model (RBM/TournamentsXPanded?) nie sprawdzony.
- Skutek przy 2 poziomach: arena -0.8 x 2 -> mnoznik -0.6 (XP ujemne albo zero), turniej -0.5 x 2 -> 0 XP; w bitwie mnoznik 2w-1
  zamiast w (zabicie: 15x zamiast 8x; blok 0.25 -> -0.5). Straznik `ResultNumber <= 0` dziala tylko przed pierwszym przebiegiem.
- Naprawa: licznik zagniezdzenia. Dowodu w logu brak (FairXp nie loguje) - sprawdzic XP w arenie po poprawce.

### W4. Pompa zlota: nasze wyplaty z kasy miasta + vanillowa dosypka do celu - POTWIERDZONE mechanizm, skala PODEJRZANA
- Vanilla `tw/DefaultSettlementEconomyModel.cs:75-79`: kasa miasta dazy do 10 000 + 12 x dobrobyt, 25% luki dziennie w obie strony
  (model ekonomii osady jest vanilla - BK i BEE nawzajem wylaczaja swoje: `bk/BannerKings/Main.cs` "if (!ModCompat.BetterEconomy)",
  `be/BetterEconomySubModule.cs:108-112` "if (!isActive)").
- Nasze odplywy z kasy miasta ponizej celu sa wiec w 25%/dzien odtwarzane z niczego:
  - `WorkshopLaw.cs:176-190` - miasto placi warsztatowi cene wyrobu (`town.GetItemPrice(..., true)`, wiec z nasza polka i indeksem);
    log: "Warsztaty: ... koszt 173, sprzedaz 236657" (dzien 1), 542 579, 594 649, 574 745 - same luki. Zloto trafia do kapitalu warsztatu
    (wlasciciel - notabl/rod), a miasto dostaje dosypke.
  - `SupplyDemand.cs:345-356` (DailyTrade) - kupujace miasto placi sprzedajacemu; log 1.10 / 1.30 / 1.24 / 1.61 mln dziennie.
    Samo w sobie zero-sumowe, ale kupujacy ponizej celu dostaje dosypke, a sprzedajacy powyzej celu oddaje nadwyzke...
  - ...do `PopulationLaw.cs:165-171`: renta miasta = nadwyzka ponad ten sam cel, do pana. Lancuch: hurt/warsztat -> dosypka u kupca ->
    nadwyzka u sprzedawcy -> renta lorda. Netto zloto z niczego do lordow.
- Na start dochodzi BK `BKCampaignStartBehavior.GiveTownsResources` (dobrobyt x 40 do kasy kazdego miasta, `bk/.../BKCampaignStartBehavior.cs:232-236`)
  - cala ta nadwyzka idzie renta do panow w pierwszych dniach. Zgodne z logiem: renty 1.44 -> 1.32 -> 0.97 -> 0.51 mln/dzien.
- Naprawa (do decyzji): renta i nasze wyplaty liczone wzgledem tego samego celu (miasto ponizej celu nie placi warsztatowi / hurtu
  ponad to, co ma powyzej celu) albo latka na `GetTownGoldChange`, ktora nie dosypuje (tylko kasuje nadwyzke).

### W5. Renta od ludnosci liczona trzy razy, powinnosci wobec korony na podatku BK - POTWIERDZONE
- (1) BK/vanilla podatek miast i wsi z niczego (Z1 w AUDYT-EKONOMIA-KROLESTW: `bk/BKTaxModel.cs:164-221`, ~0.48 mln/dzien) - zostaje
  swiadomie (`PopulationLaw.cs:122-127`: "Podatki gry (BK/vanilla) zostaja, jak byly").
- (2) BEE dziala z BK w formie ZACHOWAN (nie modeli): `be/BetterEconomySubModule.cs:89-93` dodaje `FeudalEconomyCampaignBehavior`,
  `TownEconomyCampaignBehavior`, `TradeAgreementCampaignBehavior` bez warunku `isActive`; one placa `GiveGoldAction(null, lord)`:
  renta posiadlosci (`FeudalEconomyCampaignBehavior.cs:955-972`), wyplata nadwyzki wirtualnego skarbca miasta
  (`TownEconomyCampaignBehavior.cs:1970-1995`), cla traktatow (`TradeAgreementCampaignBehavior.cs:405-421`). Wersja z 20.09.
- (3) nasza renta `PopulationLaw.Daily` z kasy osad.
- (4) `KingdomTreasury.cs:51-68`: powinnosci wasali 2%/10% od `CalculateClanIncome` + renty. BK ma juz wlasny podatek wasala
  dla suzerena: `bk/BannerKings.Models.Vanilla/BKClanFinanceModel.cs:267-285` ("Taxes to {SUZERAIN}") i po stronie suzerena
  `:205-216` ("Taxes from {CLAN}"). `CalculateClanIncome` (BK `:154-165`) jest BRUTTO i zawiera "Taxes from" - pan srednie szczebla
  placi koronie procent od podatku, ktory jego wasal juz zaplacil od tego samego dochodu (kaskada).
- Naprawa (do decyzji): wybrac jedno zrodlo renty (np. nasza renta zamiast podatku BK albo BEE wylaczone w jego MCM) i liczyc
  powinnosci od dochodu NETTO (bez "Taxes from", po "Taxes to").

### W6. Zima: CrashScribe wylacza RBL, a Armoury liczy na RBL - POTWIERDZONE
- `CrashScribe/src/Mends.cs:3059-3073` + `:4506`: getter `RealisticSettings.SeasonsEnabled` zawsze `false`
  (log CrashScribe :188 "pory roku RB wylaczone na twardo").
- RBL bramkuje tym przelacznikiem: `rbl/.../Seasons/RealisticPartySpeedModel.cs`, `RealisticPartyMoraleModel.cs`,
  `RealisticFoodConsumptionModel.cs:44-46`, `WeatherEventBehavior.cs`. NIE bramkuje: `Medicine/RealisticHealingModel.cs:103-105`
  (zima -20% leczenia) i `Demographics/RealisticProsperityModel.cs:59-61` (zima -5 dobrobytu dziennie).
- `Armoury/src/WinterSource.cs:6-16` pisze: "Zostaje WinterBite ... i RBL dla rzeczy, ktorych nic nie dubluje (szybkosc partii,
  morale, leczenie, dobrobyt)", a `WesterosClimate.cs:311-317` transpiluje te klasy RBL na nasz klimat - czesc Seasons jest martwa.
- Skutek: w wieloletniej zimie nikt nie zwalnia i nie traci morale (Json RBL: WinterSpeedPenalty -0.2, WinterMoralePenalty -15 - nie dzialaja);
  leczenie i dobrobyt zimy RBL dzialaja. Do tego WinterBite (jedzenie +50%, wsie -50%).
- Naprawa (decyzja Jeffa): albo zdjac `SeasonsOff` (WesterosClimate juz podaje RBL nasza pore roku), albo dodac zimowa kare
  marszu/morale do WinterBite i poprawic komentarz WinterSource.

---

## 2. SREDNIE

### S1. Ceny broni: podlogi liczone przed mnoznikiem polki; co naprawde sie mnozy - POTWIERDZONE
- Aktywny lancuch `TradeItemPriceFactorModel`: BK nie rejestruje modelu cen; BEE rejestruje tylko bez BK
  (`be/BetterEconomySubModule.cs:108-110`); wygrywa BKROTPriceModel = vanilla `GetPrice` (AUDYT-TOWARY.md:45) albo AIInfluence
  (zaciemniony, PODEJRZANE). Log :6-8: nasze latki na Default, BEE, AIInfluence.
- Kolejnosc postfixow na zewnetrznym `GetPrice` (rejestracja `SubModuleMain.cs:50,58,59`): ScrapFloor (priorytet Normal) ->
  MarketGlut (Last) -> SupplyDemand (Last, pozniej zarejestrowany). ScrapFloor i MarketGlut nie maja licznika, ale sa idempotentne (podloga).
- SupplyDemand (`SupplyDemand.cs:213-231`, licznik OK) mnozy wynik PO podlogach przez polke (0.25-2) x indeks surowcow (0.5-1.5):
  "5% wartosci" przy sprzedazy gracza staje sie 0.6%-15%. Log 13:04:09: "westerlands_garb SPRZEDAZ ... = x1.95 (1 -> 2, wartosc 12)";
  "KUPNO ... x2.28 (24 -> 55, wartosc 12)" - rozpietosc kupno/sprzedaz 27x.
- Pelny stos mnoznikow na sztuce uzbrojenia (kupno): vanilla 0.8-1.3 x (1 + kara handlu, w logu ~x2) x [AIInfluence -50%..+100%?]
  x polka 0.25-2 (premia wojenna wchodzi do popytu, ale polka ma sufit 2) x indeks 0.5-1.5. Teoretycznie do ~x15 wartosci;
  BEE x0.3-3 NIE wchodzi. Wynik luku ~6 700 zl w warsztatach (wpis 44) tym stosem sie nie tlumaczy - PODEJRZANY AIInfluence
  (jego mnozniki zdarzen) albo cos innego; do sprawdzenia w nowym logu po wpisie 44.
- Naprawa: podloga (MinSellPercentOfValue / MarketGlutStartPercent) jako postfix PO SupplyDemand albo wewnatrz SupplyDemand.

### S2. Indeks surowcow binarny przez Value = 1 - POTWIERDZONE
- `ArmsPricing.cs:248-256`: `LocalRatio = cena kupna / Value`, w granicach 0.5-1.5. Po HistoricalPrices ruda, drewno, wegiel,
  sztabki t1-t2 maja Value = 1 (log :247 "iron 50->1, hardwood 25->1, charcoal 9->1, ironIngot1 87->1"), a vanilla liczy kupno przez
  `Ceiling` i `Max(num, 1)` (`tw/DefaultTradeItemPriceFactorModel.cs:187-195`): wspolczynnik <= 1 -> cena 1 (indeks 1.0), > 1 -> cena >= 2
  (indeks 1.5). Nigdy ponizej 1.0.
- Log codziennie: "indeks rudy min/med/max 1.00/1.50/1.50, drewna 1.00/1.50/1.50" - prawie wszedzie sufit: +50% na kazdej sztuce z metalu/drewna.
- Do tego zaokraglenie Value w gore: ruda 0.75 -> 1 (+33%), drewno 0.35 -> 1 (+186%), wegiel -> 1.
- Naprawa: indeks liczony z ceny na 100 sztuk albo z wspolczynnika ceny (`GetBasePriceFactor`), nie z ceny jednej sztuki.

### S3. Popyt wojenny liczony dwa razy - POTWIERDZONE (drobne dzieki sufitowi polki)
- `SupplyDemand.cs:31-34` (komentarz): "popyt wojenny bierze sie z PRAWDZIWYCH zakupow armii (osobna zmiana), nie z mnoznika";
  a `SupplyDemand.cs:105`: `prosp *= 1f + ArmsPricing.WarPremium(...)` (+46..+54% w logu). Do tego AiGear naprawde wykupuje polki
  (ZakupyAI w logu). Ten sam popyt: raz jako oczekiwanie (gasnie 15 dni), raz jako realny zakup. Polka i tak ma sufit x2.

### S4. AmmoRecovery nadpisuje decyzje CrashScribe i liczy wynik bitwy gracza dla wszystkich - POTWIERDZONE
- `CrashScribe/src/Mends.cs:4245-4258`: QuiversComeBack przywraca tylko prawdziwa amunicje; "oszczepy i toporki maja zostawac na polu,
  bo tak chcial Jeff".
- `Armoury/src/AmmoRecovery.cs:35`: obejmuje tez `Thrown` - wyrzucony do konca stos oszczepow wraca teraz w 65% (zwyciestwo),
  a czesciowo uzyty moze przepasc. Odwraca wczesniejsza decyzje bez wpisu.
- `AmmoRecovery.cs:40,52-62`: `PlayerWon()` = wynik misji GRACZA dla kazdego agenta, ktorego sprzet DTE zwraca (takze partii AI):
  przegrany wrog odzyskuje 65%, zwycieski wrog przy porazce gracza - 0%.
- PODEJRZANE: jesli DTE wola `IsAmmoAndEmpty` dla tej samej sztuki wiecej niz raz, kazde wywolanie losuje od nowa.

### S5. Waga wegla: trzy rozne zalozenia - POTWIERDZONE
- `ArmouryBehavior.cs:909-911`: `HistoricalPrices.Apply()` a ZARAZ PO NIM `FixCharcoalWeight()` (`:616-630`, `CharcoalWeight` 0.5 kg;
  log :255 "Wegiel drzewny: waga 0.5 kg/szt.").
- `MaterialLaw.cs:100-101` (wpis 42): "wegiel liczony ze swoja PRAWDZIWA waga z gry (5 kg) ... 5 drewna = 50 kg -> 2 wegle = 10 kg".
  W grze wegiel wazy po tym 0.5 kg: 2 wegle = 1 kg (wydajnosc 2%, nie 20%). `ArmsPricing.cs:219` liczy "~1 szt. (0.5 kg) wegla na kg metalu".
- Cena wegla i tak 1 (minimum), ale bilans masy przetopu i ladownosc taboru sa sprzeczne z planem.

### S6. Trzy ludnosci, rozne jednostki - POTWIERDZONE
- BK `PopulationData` - pobor odejmuje ludzi (`bk/BannerKings.Patches.Recruitment/RecruitmentApplyInternalPatch.cs`), Levy czyta z niej
  nadwyzke rak (`Levy.cs:58-76`). Hearth wsi werbunek nie rusza.
- PopulationLaw: ludzie = hearth x k, k = 21-454 na hearth (log 13:03:18 "reach ... wies 454/hearth") (`PopulationLaw.cs:107-115`).
- OutlawLaw: wyrzutek = 0.5 hearth (`OutlawLaw.cs:162-172`, `Settings.cs:439`), ScorchedEarth: zerowanie hearth przez armie (`ScorchedEarth.cs:73-74`).
- Skutek: 1 wyrzutek zabiera z bazy renty 10-227 "ludzi"; 1 000 zwerbowanych - zero. Log: ludnosc 52.6 -> 52.1 mln w dobe.
- (BEE ma czwarta: `PopulationCampaignBehavior`/`EconomySaveBehavior` - chlopi i kupcy BEE, uzywane do plynnosci.)
- Uwaga: komentarz `Levy.cs:16-17` "BEE liczy produkcje wsi od liczby chlopow" jest nieaktualny - model produkcji wsi BEE nie jest
  zarejestrowany przy BK (`be/BetterEconomySubModule.cs:104-107`).

### S7. TerrainEase zwraca kare vanilla, ktorej vanilla nie nalozyla - POTWIERDZONE
- `TerrainEase.cs:56-60,79,84`: `Swap` zawsze oddaje +30% (las) / +10% (pustynia) / +10% (snieg) / +25% (noc), a potem odejmuje plaska kare.
- Vanilla `tw/DefaultPartySpeedCalculatingModel.cs:264-297`: las z perkiem ForestKin (>=75% pieszych) daje DODATNI czynnik zamiast -0.3;
  pustynia z cecha `AseraiDesertFeat` (Dorne = `aserai`) nie dostaje -0.1. Nasz zwrot robi z tego premie (+30%/+10%) ponad plaska kare.
- Naprawa: zwracac tylko pozycje, ktora faktycznie jest w rozpisce (szukac linii po nazwie) albo sprawdzac ten sam warunek co vanilla.

### S8. LevyGold: bez darmowego kompletu takze karawany i partie towarzyszy gracza - POTWIERDZONE (stare W4/H3, nadal otwarte)
- `LevyGold.cs:54-61`: pomija DTE `OnMobilePartyCreated` dla kazdej partii poza `MainParty` - karawany, partie rodu gracza,
  bandy, milicje. Log: "nowe partie AI bez darmowego kompletu DTE: 1515" pierwszego dnia (w tym 410 nowych band).

### S9. GrandTourney placi w skali gry, przy cenach w pensach - POTWIERDZONE
- `GrandTourney/src/Settings.cs:26,33-37,47-49`: nagrody 3 000 / 8 000 / 15 000, oplata 2 000 + dobrobyt x 0.5, utarg 200 za lorda
  + dobrobyt x 0.1 - z niczego (`TourneyBehavior.cs:552-553`). HistoricalPrices przeliczyl nagrody-przedmioty (/4), ale nie te kwoty.
  15 000 d = ~62 funty = kilkanascie plyt t6.

---

## 3. NISKIE

- **N1. StartKit tylko dla awanturnika** (`StartKit.cs:133-145`): BK `start_mariner` daje dalej 10 000 d (`bk/.../DefaultStartOptions.cs:210`),
  `start_mercenary` 250. BK `RunStartOption` ustawia zloto na `option.Gold` (`BKCampaignStartBehavior.cs:158`). POTWIERDZONE.
- **N2. Dwie identyczne podlogi 5%** (`Patches.cs:84-97` MinSellPercentOfValue i `MarketGlut.cs:80-101` MarketGlutStartPercent) -
  ten sam efekt dwa razy, plus licznik "-0.25 pp za sztuke" jest wylaczony przy SupplyDemand. Do uproszczenia. POTWIERDZONE.
- **N3. HouseLevies podmienia ochotnika po zakupie sprzetu** (`HouseLevies.cs:66-75`): notabl zaplacil (VolunteerKit) za komplet
  szlachcica kultury, potem ten sam slot staje sie czlowiekiem rodu tego samego tieru z innym kompletem - bez rozliczenia. Drobne. PODEJRZANE (skala).
- **N4. RecruitCost dubluje BK**: `BKPartyWageModel.GetTroopRecruitmentCost` ma juz `LimitMin(zold x 10)` (`bk/.../BKPartyWageModel.cs:383`);
  nasza cena = zold x 10 (najemnik x 20). Licznik zagniezdzenia dziala (6 modeli), konflikt tylko pojeciowy. LevyGold placi notablowi
  te sama kwote, ktora vanilla zdjela (`tw/RecruitmentCampaignBehavior.cs:608-630`) - spojne. POTWIERDZONE.
- **N5. WinterSource neutralizuje martwe funkcje BEE**: plony i ceny jedzenia BEE siedza w modelach, ktore przy BK nie sa zarejestrowane;
  zywa jest tylko predkosc karawan (BEE_PartySpeedModel jest rejestrowany bez warunku, `BetterEconomySubModule.cs:113-139`). Nieszkodliwe.
- **N6. HistDemandScaling (wpis 44)**: latka siedzi na kazdym `SettlementEconomyModel.GetDailyDemandForCategory` z licznikiem - przy
  aktywnym vanilla dziala raz. Popyt broni/zbroi (nie towar handlowy) i tak nie rusza wspolczynnika ceny (vanilla zaciska 0.8-1.3,
  `DefaultTradeItemPriceFactorModel.cs:179-182`) - zmienia tylko budzet zakupow miasta. Zgodne z celem. Brak logu.

---

## 4. Sprawdzone - efekt nakladany RAZ (bez uwag)

| System | Gdzie | Jak |
|---|---|---|
| SupplyDemand (cena) | `SupplyDemand.cs:206-215` | licznik `_depth`, postfix tylko na zewnatrz |
| RecruitCost | `RecruitCost.cs:21-29` | licznik, 6 modeli |
| Rations (-40% jedzenia) | `Rations.cs:47-53` | licznik, 5 modeli (BK, BEE, Naval, ROT, RBL) |
| WinterBite jedzenie | `WinterBite.cs:112-122` na `Default...CalculateDailyBaseFoodConsumptionf` | baze liczy tylko najglebszy model (BK nie nadpisuje, ROT/Naval/BEE owijaja) |
| MaterialLaw (wydobycie) | `MaterialLaw.cs:142-147` | licznik, 5 modeli |
| OutlawLaw (sklad band) | `OutlawLaw.cs:537-542` | licznik, 5 modeli |
| HistoricalPrices (popyt) | `HistoricalPrices.cs:194-207` | licznik |
| MarchPace, TerrainEase, Nocleg (predkosc) | `MarchPace.cs:73`, `TerrainEase.cs:72`, `NightRest.cs:792` | `SpeedDepth.OutermostFinal`, 6 modeli |
| WorldPace | `WorldPace.cs:52,129` | `OutermostBase` / `OutermostSiege` |
| Nocleg (morale) | `NightRest.cs:809` | `OutermostMorale`, 4 modele |
| Levy (chec do sluzby) | `Levy.cs:84-97` | jedna metoda BK; SlowMuster spi (log :106 "100% - vanilla tempo"), VolunteerRegenPercent 100 - podwojny hamulec (H4) zamkniety |
| Levy vs VolunteerKit | `Levy.cs:89-90`, `VolunteerKit.cs:66-75` | rozlaczne: puste miejsce vs awans |
| KingdomTreasury | log :47-48 | 4/4 stale, zapomoga ROT przechwycona |
| ChargeTemper | `Charge.cs:44-50` | jedna metoda statyczna |
| ScrapFloor, MarketGlut, BkSupplyTemper, WeaponXp | | podlogi/sufity - powtorzenie nie zmienia wyniku |

---

## 5. Zloto - tylko styki z naszymi systemami (reszta: AUDYT-EKONOMIA-KROLESTW.md Z1-Z13)

Nasze `GiveGold/ChangeGold` bez pary (z niczego / w nicosc) - wszystkie juz znane: `IronBank.cs:170` (kapital 5 mln spoza swiata),
`RealisticCaptivity/src/Work.cs:289,325,341` i `Homes.cs:240,284` (gracz), `GrandTourney/src/TourneyBehavior.cs:435,455,492,553` (gracz),
`Uniques.cs:146`. Reszta ma pare (AiGear, WorkshopLaw, SupplyDemand, VolunteerKit, Stables, LevyGold, OutlawLaw fence, KingdomTreasury, PopulationLaw).
Nowe w tym audycie to tylko interakcje: W4 (dosypka do celu kasy miasta jako pompa dla warsztatow, hurtu i renty) i W5 (potrojna renta).

Zewnetrzne zrodla nadal aktywne (sprawdzone w kodzie): BK start `GiveClansResources` Tier x 25 000 (`BKCampaignStartBehavior.cs:240-248`)
i `GiveTownsResources` dobrobyt x 40 (`:232-236`) - w skali pensow lord ma na start 187-340 tys. (log ZakupyAI: Tarth 187 332, Bolton 339 687),
czyli kilkaset plyt t6; vanilla cel kasy miasta (W4); BEE renty i cla (W5).

---

## 6. Proponowana kolejnosc (jedna zmiana = jeden DLL = jeden test)

1. W1 ThrownWobble - licznik zagniezdzenia (5 linii, wzor z Rations). Test: oszczep w bitwie.
2. W2 SightRange - licznik. Test: zasieg wzroku w nocy (dymek).
3. W3 FairXp - licznik. Test: XP w arenie > 0.
4. W6 zima - decyzja Jeffa (RBL z naszym klimatem czy kary w WinterBite).
5. S1 podloga po SupplyDemand; S2 indeks surowcow z wspolczynnika ceny.
6. W4/W5 - decyzja ekonomiczna (jedno zrodlo renty, cel kasy miasta bez dosypki albo wyplaty tylko z nadwyzki).
7. S4 AmmoRecovery: bez `Thrown` (albo wpis, ze Jeff zmienia zdanie) i strona agenta zamiast wyniku gracza.
8. S5 waga wegla - jedno zalozenie (0.5 kg) i przeliczenie przepisow MaterialLaw.
