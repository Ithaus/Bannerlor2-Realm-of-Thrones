# Etap 5 - rozpoznanie B: kod (11.10.2026)

Zakres: gdzie dzis w Armoury, w grze i w cudzych modach (BK, BEE, RBL, NavalDLC) powstaja i znikaja pieniadze miast i ludu; dla kazdej
pozycji PLANU 5.1-5.13: co juz jest w kodzie (plik:linia), co trzeba zmienic, jakie latki cudzego kodu (sygnatura, kto wola, kolejnosc
z naszymi latkami). Tylko odczyt: kod, gra, zapisy i ustawienia nietkniete. Dokumenty i decyzje Jeffa - `rozpoznanie-C-dokumenty.md` (obok).

Skroty:
- SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad`
- KOD = `SCR\e4\bledy\Armoury\src` (galaz `w-toku/e4-bledy`, dfb12df = zbior 31 = WGRANIE 31, Armoury cf40fefa). Bez przedrostka plik = KOD.
- GRA = `SCR\dec32\cs_full` (TaleWorlds 1.4.8), BK = `SCR\e4tools\bkfull` (BannerKings.Redux), BEE = `SCR\e3\dec36\bee`, RBL = `SCR\r2dec\proj_RealisticBannerlord`.
- LOG = `SCR\kopia-z31-A\Armoury-2026-10-11_08-09-11.log` (120 dob nowej kampanii, wszystko co w grze). Liczby "d31-120" = srednia dob 31-120
  z linii logu (skrypty `SCR\etap5b\avg.py`, `avg2.py`, `avg3.py`). [K] kod, [P] pomiar z logu, [W] wniosek z kodu (nie sprawdzony w grze).
- KL = `docs/PROJEKT-KIESA-LUDU-2026-10-09.md`, OB = `docs/PROJEKT-EKONOMIA-OBIEG-2026-10-08.md`, E2 = `docs/PROJEKT-ETAP2-BANKRUCTWA-2026-10-09.md`,
  P111 = `docs/paczki/111-k6-kasy-miast.md`, C = `docs/etap5/rozpoznanie-C-dokumenty.md`.

---

## 0. Najwazniejsze

1. **Kodu etapu 5 w grze nie ma - sa tylko haki z wartoscia 0:** `CastlePurse.CastleDuesSuburbShare` (CastlePurse.cs:92, czesc zaworu zamku dla
   podzamcza, KL 15%), `VillageTakings.VillageRentYieldsToFees` (VillageTakings.cs:59, renta wsi 20% ustepuje oplatom stalym 173), `ValveSplit.Split`
   (ValveSplit.cs:22-28, wspolny podzial zaworu pan/korona "dla 111' i KL"), statki 181 z trzema wylacznikami OFF (Settings.cs:1258-1261).
   **`TownPurse.cs` (111') w drzewie nie istnieje.** Jest stara wersja z 06.10 na galezi `paczki-na-120/111-k6-kasy-miast` (2ac5e72, 563 linie,
   opis P111) - sprzed 110 S14/B-2/B-4, 114, 162m, 164a, 165, 166, 181; nadaje sie na wzor, nie do przeniesienia.
2. **Pieniadz miast dzis [P d31-120]:** "zakupy" mieszczan z niczego **393 tys./d** (BK `ItemConsumptionPatch` -> `town.ChangeGold`, BK EconomyPatches.cs:555),
   regulator gry kasuje **297 tys./d** i dosypuje **20 tys./d** (GRA DefaultSettlementEconomyModel.cs:75-79). Netto "zakupy - regulator" ok. +116 tys./d
   z niczego. Na tym stoi zawor (renta) miast do panow **456 tys./d** (PopulationLaw.cs:1184-1196).
3. **Zloto swiata rosnie +543 tys./d [P d31-120]** (= +0.25% na dobe, ok. +7% na 28 dob; prog "Skonczone" etapu 5: < 0.1% na 28 dob). Netto glowne:
   statki 3.3 (OFF) ok. +311 tys. (NavalDLC 293 + sprzedaz portom 28 - kupno 10), "zakupy" - regulator +116 tys., **bitwy +72 tys.** (`MapEventParty.CommitGoldChanges`,
   poza 5.x - do wyjasnienia), pasmo notabli +14 tys. (131 z niczego - 117 w nicosc). Reszta bilansu ok. 0 (-0.6 tys.) - przyczyny sa nazwane.
4. **5.1 sam (bez 5.5) oslabia kasy miast o ok. 80 tys./d** [W]: dzis zmiana kas miast +34 tys./d [P], bez "zakupow" -393, bez kasowania +277. W trybie 1
   rosnie wtedy dosypka (liczona), a zawor od nadwyzki daje panom mniej - spada wojsko AI (Jeff 09.10 C: nie zmniejszamy). Kod 5.1 mozna pisac teraz,
   ale test i wgranie razem z 5.5-5.7 (KL 5.1 kolejnosc, PLAN 5.5 "jedno wgranie z 5.6 i 5.7"), na nowej kampanii (doba 1: kasy miast 17.4 mln wobec
   celu regulatora 6.5 mln - dar startowy do przyciecia [P]).
5. **Cztery rozne "zapasy kupcow" w kodzie** (rozdz. 3): staly 20 000 (`TownRentFloorGold` - renta, danina, polityki, Spoils), 10 000 + 12 x dobrobyt
   (`Seal31.TownReserve` - prawdziwy platnik E3/1, porty 181), zamek `CastlePurse.Reserve/PayFloor/Spendable`. 111' musi dac jedna definicje.
6. **Tarcze zoldu (107b) i dworu (162m) to obejscie regulatora** (SoldierPay.cs:576-760); pod tarczami srednio 1.53 mln, w dobie 120 2.86 mln [P]. Przy 111'
   zbedne - wylaczyc i rozliczyc znaczniki; `Seal31.Spare` (Seal31.cs:101-113) i porty 181 odejmuja je od nadwyzki.
7. **Sakiewki ludzi 22.5 mln w dobie 120 = 29 dni zoldu (prog "Skonczone": 15) i rosna** [P]; jedyny wydatek ludzi to wyjazd z miasta
   (MenPurse.OnLeft, MenPurse.cs:258-310). **Markietani (163) nie istnieja** (ksiega drukuje "markietani -", MoneyLedger.Obieg.cs:567).
8. **Notable (164c):** pasmo gry (GRA NotablePowerManagementBehavior.cs:47) z niczego 131 tys./d i w nicosc 117 tys./d, nowy notabl 10 000 z niczego
   (GRA NotablesCampaignBehavior.cs:47; dzis ok. 0.1 tys./d). W kodzie tylko pomiar (okna O03/O04, CirculationWindows.cs:1757-1758).
9. **Droga dla skutkow (5.6, 5.9) istnieje:** BK wpina postfiks `BKEconomyLayerInstaller.CalculateProsperityChangePostfix` na KAZDY model dobrobytu
   (tez czynny RBL `RealisticProsperityModel`, rgl_log 09:28 "inventory"), wiec zadowolenie klas BK i "Merchants wealth" (kasa < 20 000) dzialaja na dobrobyt
   (BannerKings.json: `BKEconomyLayerStrength` 0.5, `ProsperityGrowthMultiplier` 0.3). Uwaga [W]: postfiks siedzi i na RBL, i na Default (RBL wola base) -
   delty BK moga liczyc sie dwa razy; sprawdzic przed oparciem kary na zadowoleniu.
10. **Czy zaczynac przed koncem etapu 4 (widok kodu):** TAK dla kodu 5.1, 5.2, 5.3/164c, 5.4, 5.7, 5.8, BEE G i statkow - pliki, ktorych galezie E4 nie
    ruszaja (CastlePurse, SoldierPay, KingdomTreasury, CrownRents, CrownIncome, MenPurse, Seal31, MoneyLedger, BeeSeal, ShipsToPort181). NIE przed
    E4/6-E4/8 (i E4/10): 5.5 (liczba mieszczan - E4/8 migracja zmienia stan miast; E4/10 zmienia zloto karczmy do kas miast), 5.9 (definicja glodu dzielona
    z E4/6 Famine), 5.11 (jedzenie warowni i spichlerze E4/6), 5.13 (menu osady - E4/7 PeopleMenu). Szczegoly rozdz. 5.
11. **Narzedzia:** `tools/sprawdz_logi.py` zna grupy etap2/3/4, etapu 5 nie. Z progow "Skonczone, gdy" log mierzy dzis: zakup plonu, zatkane wsie, wojsko,
    sakiewki i zold, zloto swiata; nie mierzy "stac na jedzenie/koszyk" ani "zjedzone bez zaplaty" (dopiero 173).

---

## 1. Dzisiejsza mapa pieniedzy miast i ludu

Liczby [P] LOG, doby 31-120 (w nawiasie doba 120). Kierunek: + do kasy miasta, - z kasy.

| Przeplyw | Kod | Rodzaj | zl/d | W etapie 5 |
|---|---|---|---|---|
| "Zakupy" mieszczan: towar z polki znika, kasa dostaje cene | BK EconomyPatches.cs:505-555 (prefiks bool na `ItemConsumptionBehavior.MakeConsumption`, `town.ChangeGold(num6)` :555); vanilla GRA ItemConsumptionBehavior.cs:142-175 (:170) gdy osada bez PopulationData | z niczego | +393 (425) miasta; zamki 46 cofniete przez 110 (CastlePurse.cs:251-265) | 5.1 cofnac; 5.5 placi lud |
| Regulator kasy gry: 0.25 x (10 000 + 12 x dobrobyt - kasa) | GRA ItemConsumptionBehavior.cs:73-76 -> DefaultSettlementEconomyModel.cs:75-79 (czynny - LOG l.537); BK BKEconomyModel.cs:567 (merchant income z niczego) i BEE_SettlementEconomyModel nieczynne | w nicosc / z niczego | -297 / +20 miasta; zamki: kasowanie 0 (CastlePurse.cs:272-286), dosypka 0.9 | 5.1 tryb 1, 5.8 tryb 2 |
| Tarcza zoldu i dworu (znacznik, regulator nie kasuje) | SoldierPay.cs:576-760 (Hold :648, HoldCourt :604, RegulatorPostfix :693) | - | pod tarcza 1 530 tys. (2 864) | zbedne przy 111' |
| Zawor/renta miasta: min(nalezna z ludnosci x dekret, (kasa - 20 000) x 7% x dekret), 100% pan | PopulationLaw.cs:1144-1212 (miasto :1184-1196, `GiveGoldAction.ApplyForSettlementToCharacter` :1193) | przelew | -456 (512) | 111': od nadwyzki ponad zapas, pan/korona (5.7: 1/2) |
| Podatek ludnosci miasta BK (z niczego) zastapiony | PopulationLaw.TownTaxPostfix :1017-1037 (postfiks Last na `SettlementTaxModel.CalculateTownTax(Town,bool)` kazdego modelu, :1115-1142) | (zamkniete) | 0 | podstawa rady 3.5 (CouncilWages35.TownBase :193) |
| Renta wsi 20% kiesy dziennie | PopulationLaw.cs:1173-1192 | przelew | -113 z kies wsi | 173: oplaty stale (hak VillageTakings.cs:59) |
| Danina wojenna: miasta 1%/d kasy ponad 20 000, wsie 1.5%/d, x2 przy War Tax | KingdomTreasury.Levies :362-447 (danina :392-404); Settings.cs:763-766 | przelew do skarbca | -84 z miast; razem 91 (106) | 5.6: placi kiesa ludu (184) |
| Polityki krola: War Tax 5% podatku miast, Crown Duty, Road Tolls, Land Tax - z kas ponad 20 000 | KingdomTreasury.RulingIncomePostfix :462-530 | przelew | male | 5.6: War Tax drugi raz (rozdz. 2, 5.6) |
| Clo (licznik cel -> skarbiec, 165 "clo raz") | KingdomTreasury.cs:405-425 | przelew z licznika | 12 | bez zmian |
| Dwor 162m: glowa -> kasa siedziby | ClanBudget.Court :598-624 (`seat.Town.ChangeGold(ix)` :621) | przelew | +137 (143) | 5.2 pelny; KL 80/20 |
| Zycie zolnierzy w miescie (wyjazd) | MenPurse.OnLeft :258-310 (`st.Town.ChangeGold(life)` :299) | przelew z sakiewek | +339 (689) | KL 75/25; 163 markietani |
| Zold zalog do kasy osady | SoldierPay.Route :425-473 (:473) | przelew | +66 | KL 75/25 |
| Sprzet AI, warsztaty zbrojne, odziez | AiGear.cs:705, :747; WorkshopLaw.cs:321-322; ArmyClothing.cs:181, :237-238 | przelew | +192 sprzet; warsztaty +81/-217 (d120) | NoteSale (KL) |
| Plon wsi: kasa miasta placi taborowi | BK EconomyPatches.SellGoodsPatch :816-871 (`town.ChangeGold(-...)` :859; kupuje tyle, ile ma kasa) | przelew | -306 | bez zmian |
| Powrot taboru: pan 70% (BK), wies 50% reszty (BK) + 112 dopisuje druga polowe | BK EconomyPatches.cs:906-930 (`CalculateVillageTaxFromIncome` :915, `Village.ChangeGold(num2*0.5)` :917); BK BKTaxModel.cs:271-284 (0.7 / Low 0.5 / High 0.9); VillageTakings (112) | przelew | pan 176, kiesy wsi 89 | 5.5/5.7: wies 60% (C pkt 4) |
| BK rynek osady: ponad 50 000 + 25 x dobrobyt -1% w nicosc (blad znaku: pol z kasy, pol z notabla); ponizej 20% limitu notabl doplaca 1000 | BK BKSettlementBehavior.cs:463-490 (RunStep :241); limit BKEconomyModel.cs:610 | w nicosc / przelew | -17 (d120) | 111': prefiks (P111 pkt 5) |
| Warsztaty BK placa za wyroby / wsad | BK EconomyPatches.cs:676-760 | przelew | -180 / +26 (d120) | KL L7 place 35% |
| Pasmo notabli 4 500-10 500 | GRA NotablePowerManagementBehavior.cs:47-61 (`GiveGoldAction` null<->notabl) | z niczego / w nicosc | +131 / -117 | 164c |
| Nowy notabl +10 000 | GRA NotablesCampaignBehavior.cs:41-47 | z niczego | 0.1 | 164c (5.3) |
| Majatki BK: udzial z utargu (placi kasa miasta) | EstateIncome187.cs; BK `EstateData.AccumulateTradeTax` (EconomyPatches.cs:925) | przelew | 18 (d120) | bez zmian |
| Statki: rozbita druzyna, handel z portem, pryzy | NavalDLC + gra; ShipsToPort181.cs (OFF) | z niczego / w nicosc | +293 +28 / -10 | 3.3 z 5.6 |
| Sakiewki ludzi (stan) | MoneyLedger.ReadHolders :702 (`MenPurse.TotalNow`) | zbiornik | 14.8 mln (22.5 mln) | 163, prog 15 dni zoldu |
| Kasy miast (stan) | Measure169c "Kasy miast (169c)" | zbiornik | 10.9 mln (12.6 mln; cel 7.2 mln) | 111' |

Ludnosc BK (LOG "Ludnosc BK (169c)"): 2.97 mln (d1-30) -> 2.90 mln (d120), czyli spada; zuzycie BK (`ConsumedValue`) 439 tys./d, budzet konsumpcji gry
(suma popytu kategorii) 939 tys./d - kotwica KL L3 dzialalaby od strony koszyka ludzi, nie gry [P].

---

## 2. Pozycje PLANU 5.1-5.13

### 5.1 Kasy miast (111'): bez kasowania i "zakupow z niczego"; najpierw z liczona dosypka

**Jest:**
- Wzor dla zamkow - paczka 110 (CastlePurse.cs): postfiksy `ShelfPostfix` (:236) i `ConsumePostfix` (:251, First) cofaja "zakupy" zamku (migawka kasy
  przed i po konsumpcji), `RegulatorPostfix` (:272, First) zeruje tylko wynik ujemny (S14), `Daily` (:540-) zawor 7% nadwyzki ponad zapas z podzialem
  `ValveSplit` (2/3 pan, 1/3 korona, 114), `TrimStartGift` (:466) przyciecie daru startowego raz (klucz `arm_castlepurse`), `Spendable` (:377) i
  `PayFloor` (:401) - wydatki i podatki tylko z nadwyzki (B-2, B-4).
- Miasta dzis: regulator gry kasuje 1/4 nadwyzki dziennie, tarcze (SoldierPay.cs:576-760) chronia zold i dwor, renta miasta w PopulationLaw.Daily
  (:1184-1196) bierze 7% kasy ponad 20 000, najwyzej "nalezna" z ludnosci; 100% panu (CastlePurse.cs:45 "podzial 2/3 : 1/3 dla miast dopiero z 111'").
- Pomiar: MoneyLedger mierzy "zakupy" i regulator (postfiksy :526, :537, regulator :551-640), linia "Kasy miast (169c)" (Measure169c.cs), "Przeplywy osad
  (kasy miast)" i "(przyczyny)" ze srednia reszty 28 dob (dzis +2.0 tys., prog 10 tys. - OK [P]).
- Stara 111 (2ac5e72): `TownPurse` z trybami regulatora 0/1/2, `Reserve`/`TaxFloor` (jedna granica), `OnShelf`/`OnConsumed`/`OnRegulator` wolane z latek
  CastlePurse, `CollectRent` (zawor bez pulapu naleznej), `HomePart` (zwrot korony bez zalog), `BkSkimPrefix` (HandleMarketGold), `BeforeRents` (przyciecie
  daru przed rentami), opis w P111 (pkt 1-11, rachunek udzialu pana 0.67).

**Trzeba zmienic** (wszystko pod wylacznikiem MCM, wylaczony = jak dzis):
1. NOWY `TownPurse.cs` na dzisiejszym kodzie: miasta przez te same trzy latki co zamki (CastlePurse - jedno miejsce wpiecia, jak w starej 111),
   "zakupy" cofniete do czasu 173 (wtedy placi lud - KL 5.2 `PayForGoods`), regulator tryb 1 (ujemny = 0, dodatni tylko do zapasu, liczony) -> tryb 2 w 5.8.
2. Zawor miasta: z nadwyzki ponad zapas (bez pulapu naleznej - stara 111 pkt 4; naprawia tez "10 miast bez placenia", 5.4), podzial `ValveSplit.Split`
   (5.7: pan 1/2), czesc pana do `PopulationLaw.RentToday` (D staly 169c, powinnosci, budowy - jak CastlePurse.cs:588-590), `RentOf`/`_rentBy` dla rady
   3.5 (CouncilWages35.TownBase :193-215 czyta `PopulationLaw.RentOf`), czesc korony do `CrownIncome` "wplywy dnia" (jak `CastlePurse.CrownToday`).
   `PopulationLaw.Daily` przestaje placic rente miasta, gdy 111' czynne (jak w starej 111: `TownPurse.CollectRent`).
3. Jedna definicja zapasu kupcow dla wszystkich platnikow (rozdz. 3).
4. Tarcza zoldu i dworu: przy 111' bez znacznikow (stara 111 pkt 7); `Seal31.Spare` (:107) i porty 181 bez odejmowania `HeldOf/CourtOf`.
5. Dar startowy miast: przyciecie w pierwszej dobie przed rentami (stara 111 pkt 6, `arm_townpurse`); KL 5.3 chce czesc daru (ok. 6.7 mln) przelac
   do kies ludu zamiast kasowac - decyzja w projekcie (jedno wgranie z 173).
6. BK `HandleMarketGold` - gorna galaz pominieta dla miast (i zamkow przy 110), dolna zostaje (przelew notabl -> kasa).
7. Ksiega: "zakupy" +0, nowe pozycje ticku (udzial korony z zaworu miast, przyciety dar), `MName`/`Marks` (MoneyLedger.cs:86-95).
8. Zwrot korony (Z8): czesc zoldu zalogi miasta, ktora wraca panu zaworem (stara 111 `HomePart`; dzis 165 zwraca tylko partie w polu, CrownIncome.cs
   opis; K3 `CrownIncome.NoteOwnSpend` przy MenPurse :303) - przeliczyc przy udziale pana 1/2.

**Latki cudzego kodu (sygnatura, kto wola, kolejnosc):**
- `ItemConsumptionBehavior.MakeConsumption(Town, Dictionary<ItemCategory,float>, Dictionary<ItemCategory,int>)` - private static (GRA :142). Wola
  `MakeConsumptionInTown` (:61) z `DailyTickTown` (:47, zdarzenie `DailyTickTownEvent`) oraz 2 razy przy tworzeniu kampanii (`OnNewGameCreatedFollowUp`
  :22-28, `GameStarted` jeszcze false). BK prefiks bool `ItemConsumptionPatch.Prefix` (BK EconomyPatches.cs:505) zwraca false, gdy osada ma PopulationData -
  vanilla i cudze prefiksy z parametrem-obiektem nie biegna. Nasze: `CastlePurse.ConsumePostfix` (First), `MoneyLedger.ConsumePostfix` (zwykly). Zasada
  z 110: tylko postfiksy (CastlePurse.cs:29-31).
- `ItemConsumptionBehavior.DeleteOverproducedItems(Town)` - private (GRA :79); BK prefiks return false (BK EconomyPatches.cs:606-610; cel odczytany z IL:
  `HarmonyPatch("DeleteOverproducedItems")`); nasze postfiksy `CastlePurse.ShelfPostfix`, `MoneyLedger.ShelfPostfix` (:526) - migawka kasy przed konsumpcja.
- `SettlementEconomyModel.GetTownGoldChange(Town)` - wola `UpdateTownGold` (GRA :73-76) w tym samym ticku, po `GetFoodFromMarket`. Czynny model
  `DefaultSettlementEconomyModel` (LOG l.537). Latac KAZDA deklaracje (jak CastlePurse.cs:307-321), bo przy innej kolejnosci modow czynny bylby BK
  (`GetMerchantIncome`: dobrobyt / 2.5 x wydajnosc, zawsze dosypka). Postfiksy dzis: `CastlePurse.RegulatorPostfix` (First), `SoldierPay.RegulatorPostfix`
  (First, wpinany leniwie w kampanii :662-690 - kolejnosc dwoch First = kolejnosc wpiecia), `MoneyLedger` (zwykly - widzi wynik po nas).
- BK `BKSettlementBehavior.HandleMarketGold(Settlement)` - private (BK :463), wolane z dziennego kroku BK (`RunStep("HandleMarketGold")` :241). Dzis tylko
  okno O18 (CirculationWindows.cs:1789, prefiks First + finalizer). Nowy prefiks bool musi biec po prefiksie okna (okno mierzy) - jak 181/Spoils (Priority.Last).

**Kolizje [K]:** 110/114 (te same latki - jedna klasa wpiecia), 162m (tarcza dworu), 107b (tarcza zoldu), 164a E3/1 (`Seal31.Spare`), 181 (porty z nadwyzki),
165 (wplywy dnia, zwrot bez zalog), 166 (D staly "ziemia" z `RentToday`), 3.5 (podstawa rady z renty), 169c (`_rentBy`, "wlasne"), SpoilsSeal (prog 20 000),
36b TravellerTrade36b (kasa celu placi wedrowcom), 174.2 MaterialOrders, 171 GarrisonCarts, SupplyDemand (wywoz kupcow) - wszystkie placa z kasy miasta.

### 5.2 Dwor pana pelny (162)

**Jest:** 162m `ClanBudget.Court` (ClanBudget.cs:598-624): raz na dobe glowa -> kasa siedziby (`Seat` :553), udzial "dwor i wyzywienie" (0.35 D pokoj /
0.20 D wojna, :297) minus jedzenie partii (:578), w wojnie dwor ustepuje zoldowi (:311-329, :606-612), nie ponizej podlogi kiesy rodziny (:614), dluznik -50%
(:310); reszta kiesy ponad 120 D + 50 000 po 1/180 na dwor i budowy (:305-307); znacznik tarczy dworu w kasie MIASTA (:622). Gracz bez dworu ([D] 08.10).
Niewydany przydzial sprzetu tylko lezy do 30 dni (`AiGearDaysCap`, :387-388, Settings.cs:848) - nie przechodzi na dwor.

**Trzeba (OB 6.1, KL 1.3):** (a) niewydany budzet sprzetu po 30 dniach -> dwor (OB :277); (b) podzial wplaty dworu 80% kupcy / 20% lud (KL 1.3, haku w kodzie
nie ma - inaczej niz 110 i 112); (c) bez tarczy dworu przy 111' (`SoldierPay.HoldCourt`); (d) kasa zamku jako siedziba - wplata dworu idzie do kasy zamku
(zawor ja oddaje) - podzamcze 15% przy 173.
**Latki cudzego kodu:** brak (przelew wlasny). BK ma wlasne wydatki dworu (`CourtGrace.AddExpense`) - E3/8b kieruje je do kasy siedziby (Seal31B opis).
**Kolizje:** E4/7 zmienia ClanBudget (zwolnienia :739-822, `ToVillage` z pochodzeniem), plany E4/8-E4/10 tez (nowe partie, Straz, karczma :749-759) - inne
miejsca pliku; scalanie tekstowe. 169c `ClanIncomeBook.NoteOwnPaid` (:623) - "wlasne" D stalego.

### 5.3 Markietani (163) i notable (164c)

**Markietani - jest:** wydatek ludzi tylko przy wyjezdzie z miasta (MenPurse.OnLeft :258-310: naprawy -> odziez 150 -> oszczednosci K1 -> zycie);
`MenPurse.OnDailyTickParty` (:223-233, zdarzenie `DailyTickPartyEvent`, ArmouryBehavior.cs:638) dziala tylko dla gracza w miescie; karawany wydaja cala
sakiewke (Seal31B E3/4 `CaravanSpends`). **Trzeba (OB :171, :249-250):** w polu `CampSpendPercent` 10% sakiewki dziennie: 85% kasa najblizszego niewrogiego
miasta, 15% kiesa najblizszej wsi (KL: czesc miejska 75% kupcy / 25% lud); ta sama kolejnosc co OnLeft (rezerwa na naprawy `AiWear.OutstandingCost`,
oszczednosci K1 `MenUpgrade`), te same liczniki (`ClanIncomeBook.NoteOwnPaid`, `CrownIncome.NoteOwnSpend` K3, nowa pozycja `MoneyLedger.Note`). Zwolnieni
zabieraja swoja czesc (ClanBudget zwolnienia - sakiewka do kiesy wsi juz jest, :749-759). Bez latek cudzego kodu; petla po partiach raz na dobe (ok. 1 000 partii).
**Notable - jest:** tylko pomiar (O03 `BandPre`, O04 `NewNotablePre`, CirculationWindows.cs:532-546, :1757-1758); zloto zmarlego notabla zostaje w jego
osadzie (E3/8c `DeadHeroGoldStays`, Seal31B). **Trzeba:** (a) nowy notabl bez 10 000 z niczego - platnik kasa osady domowej z nadwyzki (mechanizm
`RealPayer`: prefiks Low na `GiveGoldAction.ApplyInternal` z kontekstem, RealPayer.cs opis) albo 0; (b) pasmo: nadwyzka ponad 10 500 do kasy osady (albo na
towar - KL 1.3 "notable kupuja zbytki", po 173), brak ponizej 4 500 z czynszu ludu (KL: czynsz 50% notable) zamiast z niczego.
**Latki cudzego kodu:** `NotablePowerManagementBehavior.BalanceGoldAndPowerOfNotable(Hero)` - private (GRA :47), wolane w dziennym ticku notabla;
`NotablesCampaignBehavior.OnHeroCreated(Hero, bool)` - private (GRA :41), nasluch `HeroCreated` (:37) - dotyczy GangLeader, Artisan, RuralNotable, Merchant,
Headman (kaplan BK nie). Kolejnosc: okna 169 (prefiks First + finalizer) -> nasz kontekst RealPayer (Low) -> GiveGoldAction.

### 5.4 Drobne kas: oblezone miasto, prog podatkow zamku, 10 miast bez placenia panu

- **10 miast bez placenia** (EKONOMIA-FUNDAMENT P6/K12): [K] renta miasta pomija osade z ludnoscia 0 (PopulationLaw.cs:1166 `if (p <= 0f) continue`) -
  miasta kultur z udzialem miejskim 0 w tabeli (freefolk, nightswatch, skagosi, khuzait, valyrian - PopulationLaw.cs:50-72) [W]; Lotus Bay i Ifequeveron
  (dobrobyt 500): regulator trzyma kase przy 16 000 < prog 20 000. 111' (zawor od nadwyzki ponad zapas, kazde miasto z zywym panem) naprawia oba z definicji.
- **Oblezone miasto** [W]: renta miasta nie sprawdza `IsUnderSiege` (PopulationLaw.cs:1173-1196 - tylko wies Looted/BeingRaided), a zawor zamku
  (CastlePurse.cs:573) i danina (KingdomTreasury.cs:389) oblezona osade pomijaja - niespojnosc do zamkniecia w 111' (oblezone nie placi; zrodlo w docs - C).
- **Prog podatkow zamku:** prawdopodobnie zalatwione w B-4 - `CastlePurse.PayFloor` (CastlePurse.cs:401-411) w Crown Duty i War Tax (KingdomTreasury.cs:483-484,
  :511) i Spoils (SpoilsSeal.cs:413, :476); Road Tolls (:528) liczy tylko miasta, od 20 000. Do potwierdzenia w projekcie (zrodlo pozycji nie znalezione - C).

### 5.5 Kiesa ludu (173)

**Jest:** nic poza hakami (rozdz. 0 pkt 1). Projekt KL liczony na 2e235ea (sprzed etapow 2-4) - numery linii i miejsca z KL 5.2 nieaktualne:

| Miejsce z KL 5.2 | Dzis (KOD) |
|---|---|
| `MenPurse.OnLeft` :190 | MenPurse.cs:299 (`st.Town.ChangeGold(life)`), tarcza :305 |
| `SoldierPay.Route` :325 | SoldierPay.cs:425-473 (`town.ChangeGold(coffers)` :473) |
| `BuildFunding.Daily` :174-176, `MineRevenuePostfix` :96-98 | BuildFunding.cs:182 (material), :185 (place), :98 (kopalnie) |
| `WorkshopLaw` :313, `WorkshopTrade.PayToTown` :323, `TownCrafts.Work` | WorkshopLaw.cs:321-322, :453; WorkshopTrade.cs:329, :503 |
| `AiGear` :287, `ArmyClothing` :181, :237-238 | AiGear.cs:705, :747; ArmyClothing.cs:181, :237-238 |
| dwor 162 | ClanBudget.cs:621 |
| `PopulationLaw.Daily` :300-316, `TownTaxPostfix` :150-165 | PopulationLaw.cs:1144-1212, :1017-1037; hak :1174 |
| `KingdomTreasury.Levies` :221-287 | KingdomTreasury.cs:362-447 |
| `TownPurse.Daily` (111') | nie istnieje (5.1) |
| `MoneyLedger` :641 (jedna suma) | MoneyLedger.ReadHolders :670-706, WorldTotal :717 (wolane tez przy kazdym rozliczeniu rodu :355, :371) |
| `ArmouryBehavior` :389, :1232-1237 | konstruktor :395, tick :1451-1542 |

**Latki cudzego kodu:**
- Kotwica koszyka: BK `EconomyPatches.CalculateBudget(Town, float, ItemCategory)` - private static w klasie ZEWNETRZNEJ `EconomyPatches` (BK :1085-1101),
  nie w `ItemConsumptionPatch` (KL :416 - poprawka); wola ja tylko prefiks BK (:523). Postfiks mnozacy wynik. Osada bez PopulationData idzie vanilla
  `SettlementEconomyModel.CalculateDailySettlementBudgetForItemCategory(Town, float, ItemCategory)` (GRA DefaultSettlementEconomyModel.cs:81-84) - tez latac.
- Zadowolenie: BK `EconomicData.UpdateSatisfaction(PopulationManager.ConsumptionType, float)` - public (BK EconomicData.cs:148); BK sam robi +0.001 przy
  zaspokojonym popycie i -0.0015 przy braku towaru (EconomyPatches.cs:538-546) - obnizony popyt (kotwica) PODNOSI zadowolenie (KL ryzyko 3).
- Udzial wsi: BK `BKTaxModel.CalculateVillageTaxFromIncome(Village, int)` (BK :271-284) - wolany z BK `VillagerSettlementEnterPatch` (:915) przy powrocie
  taboru; BK ma DRUGA metode o tej nazwie `(Village, bool, bool)` (:212) - latac po pelnej sygnaturze (AmbiguousMatch z audytu 04.10, PopulationLaw.cs:985-988).
  Decyzja 15: pan 35% + sept 5% -> wies 60% (C pkt 4). Kolejnosc: 112 VillageTakings dopisuje dzis druga polowe reszty w oknie powrotu (MoneyLedger okno
  BeforeSettlementEntered/AfterSettlementEntered) - przy zmianie stawki BK 112 musi liczyc od nowej.
- Liczba ludzi: BK `PopulationData.GetTypeCount` (klasy; LOG 2.90 mln, spada) albo ksiega ludzi (stan miast E4/4 `PopulationLaw.TownPeople` :235-,
  `PeopleBook.TryTown` :333; miasta 10.47 mln w skali tabeli). KL L9 bierze BK. **Decyzja skali do projektu** (jedna zasada na zjawisko); E4/8 (migracja do
  miast, `TownPeopleFrozen`) zmieni stan miast - 173 dopiero po E4/8.
**Kolizje:** wszystkie wplaty do kas miast z rozdz. 1 (NoteSale), 111', 162, 163, 164c, 184, 112, E3/1 (Spare), 181, 36b, BEE G.

### 5.6 Podatek wojenny korony (184) + statki 3.3

**Jest:** dwie reguly na jedno zjawisko: (1) danina wojenna `KingdomTreasury.Levies` (:392-404: miasta `LaySubsidyTownShare` 1%/d kasy ponad 20 000,
wsie 1.5%/d kiesy, x `LaySubsidyWarTaxMultiplier` 2 przy polityce War Tax) - 91 tys./d d31-120, 106 tys. w dobie 120 [P] (cel PLAN ok. 100 tys. w wojnie
juz osiagniety, ale placi kasa kupcow, nie lud); (2) polityka War Tax w `RulingIncomePostfix` (:502-516): 5% `CalculateTownTax` z kas miast do krola
(po TownTaxPostfix podstawa to tylko linie zachowane). 165 liczy danine do wplywow dnia (`LeviesToday`, :400), miara potrzeby - niedoplata zwrotu
(linia "Korona: dzien ... niedoplata"; dzis 0 [P]).
**Trzeba (E2 :787-797, KL 1.4):** 184 placi kiesa ludu (R 2%, P 3% zarobku; wies 1.5%/d nadwyzki ponad 15 dni zakupow), stawka korony wedlug potrzeby;
skutki: (1) mniej zakupow ludu (Afford 173), (2) ponad znosny poziom (3-6% gotowki rocznie) spadek zadowolenia/lojalnosci, (3) z glodem - niepokoje.
Danina z kas osad i War Tax z `RulingIncomePostfix` scalic w jedna regule 184.
**Latki cudzego kodu:** lojalnosc - BK `BKLoyaltyModel` (kara "High tax policy" :102-108; czynnosc modelu w grze do sprawdzenia - linia "modele czynne"
go nie wypisuje); AI BK sam zmienia polityke podatkowa osady wedlug lojalnosci (BK BKSettlementBehavior.cs:400-415: > 80 High, < 30 Low) - nasz podatek
nie moze sie z nia dublowac (dekret skaluje juz rente: `PopulationLaw.TaxDecree` :1089-1113).
**Statki 3.3:** ShipsToPort181.cs gotowe, 3 wylaczniki OFF (Settings.cs:1258-1261), 9 latek (ShipsToPort181.cs:920-938: `RecoverGoldFromRemainingShips...`,
`OnPartyDisbanded`, `DistributeShips`, `ChangeShipOwnerAction.ApplyInternal` (transpiler), premie zarzadcy, `TryPurchasingShipFromTown`, wycena ekranu,
`MapEvent.LootDefeatedPartyShips`, `DestroyShipAction.Apply`). Dzis z niczego 293 + 28 tys./d [P]. Porty placa z `Seal31.Spare` - przy 111' przepiac na
nadwyzke TownPurse (inaczej rezerwa 10 000 + 12 x dobrobyt i tarcze zostaja w drugiej definicji). Wlaczenie = 3 klucze true (nowe klucze nie istnieja
w Armoury.json Jeffa - domyslne zadzialaja).

### 5.7 Wyrownanie dla panow zamkow (Z15-5)

**Jest:** renty korony 180 - wagi `CrownRentWeightTown` 3, `CrownRentWeightCastle` 1, `CrownRentWeightVillage` 0.25 (Settings.cs:885-887, CrownRents.cs:186-193);
zawor miasta 100% pan (PopulationLaw), zawor zamku 2/3 pan (`CastleDuesLordShare` 0.67, Settings.cs:806).
**Trzeba:** wagi 2 / 1.5 / 0.25 (zamek 1.5, nie 1 - C pkt 3), pan miasta 1/2 nadwyzki kupcow (w 111' - `ValveSplit.Split` z udzialem 0.5), takze gracz
(decyzja E). Zmiana tylko domyslnych - klucze nie istnieja w Armoury.json Jeffa (sprawdzone: brak `CrownRentWeight*`, `TownRentShare`), wiec zadziala
(CLAUDE.md 7). **Kolizje:** Z8 (gracz: zwrot z monety zoldu - `CrownIncome.NoteOwnSpend`, `CastlePurse.ComesHome` :353), 166 D staly "korona", 3.5 podstawa rady.

### 5.8 Korona bez bezpiecznika, kasy miast bez dosypki (gdy reszta < 10 tys. przez 28 dob)

**Jest:** skarbiec bez zlota z niczego od 04.10 - transpiler `KingdomTreasury.Transpiler` (:25-55) zeruje +1000/d i losowanie 100-400 tys. gry; bailout ROT
zablokowany (`RotBailoutPrefix` :549, `NoRotClanBailout`); 165 wydaje 1/360 zapasu ponad 500 000 (to wydatkowanie zapasu, nie bezpiecznik). "Bezpiecznik
korony" z OB (:255, :363: korona dopelnia kase miasta < 50% zapasu, najwyzej 2% zapasu dziennie) nie ma kodu. Dosypka trybu 1 zamkow (CastlePurse
`RegulatorPostfix` przepuszcza wynik dodatni) - 0.9 tys./d [P].
**Trzeba:** w TownPurse tryb 2 (regulator nic) i ten sam przelacznik dla zamkow; warunek z linii "Przeplywy osad (przyczyny)" (srednia 28 dob reszty kas miast;
po 173 z nowymi posiadaczami). Czy bezpiecznik korony w ogole wchodzi - decyzja projektu (C: PLAN "korona bez bezpiecznika").

### 5.9 Glod ma skutki (nie w pierwszych 120 dniach)

**Jest:** glod FIZYCZNY - E4/6 `Famine.cs` (w toku, `w-toku/e4-6` 0cd60f1, SCR\e4\46): spichlerze okregow, zgony i uchodzcy, mieszczanie przy Food 0
(`PeopleBook.TownDie`), schronienie w oblezonej warowni (postfiks na `DefaultSettlementFoodModel`); RBL "Severe Starvation" -20 dobrobytu
(RBL RealisticProsperityModel.cs:55); BK "Food Shortage" przy `IsStarving` (BK BKProsperityModel.cs:68-81, przez warstwe BK). `HungerLaw.cs` to inne zjawisko
(glodna partia AI kupuje jedzenie bez limitu ceny).
**Trzeba (KL 5.2 "Kara glodu"):** glod EKONOMICZNY - gdy `Afford_jedzenie` < 0.95 przez 7 dob i kampania ma >= 120 dob: `UpdateSatisfaction(Food, -0.0015)`
przez refleksje w naszym postfiksie po `MakeConsumption` (First); osobny wylacznik. Wymaga 173 (Afford). **Kolizja (jedna zasada na zjawisko):** nie karac
dwa razy miasta, ktore jednoczesnie gloduje fizycznie (E4/6, RBL, BK Food Shortage) - granica do projektu po scaleniu E4/6.

### 5.10 Dziesiecina (polowa jalmuzna, polowa budowa septow)

**Jest:** nic. BK nie ma dziesieciny (w BK jedyne "tithe" to opis polityki rycerzy, BK DefaultKingdomPolicies.cs:14) ani budynku swiatyni. Odbiorca:
kaplan BK - `Religion.GetClergyman(Settlement)` (BK Religion.cs:156) zwraca duchownego-bohatera osady (kaplan nie dostaje 10 000 przy tworzeniu - GRA
NotablesCampaignBehavior.cs:43). **Trzeba:** 5% utargu wsi z czesci pana (stawka BK / 112) + 1-3% dochodu ludu (`TownFolk.Earn`) -> kaplan ->
50% jalmuzna (kiesa W) / 50% "budowa septow" (wlasny licznik: place -> lud, material -> kupcy NoteSale). Po roku testu 5.5 (PLAN).

### 5.11 Targ przy zamku: wsie najpierw sprzedaja zamkowi jedzenie na 10 dni

**Jest:** `MarketRoad.RoutePrefix` (MarketRoad.cs:52, prefiks First na vanilla `VillagerCampaignBehavior.SendVillagerPartyToTradeBoundTown`; zwraca false i
pomija latke BK "najpierw Bound"), najpierw `MarketCarts.Choose` (119: najlepiej placace miasto w zasiegu), `CastlePurse.CanPayCart` (:419) dla dalekiego
targu. Sprzedaz w zamku: vanilla `SellGoodsForTradeAction.ApplyInternal` (GRA; BK `SellGoodsPatch` :823 dziala tylko dla `settlement.IsTown`) - kupuje
tyle, ile ma kasa, caly ladunek w jednym miejscu. Dzis do zamkow 2 wizyty taborow na dobe [P d120].
**Trzeba:** woz wsi zamkowej najpierw do swojego zamku z jedzeniem do 10 dni zuzycia podzamcza (`Town.FoodChange`, `FoodStocks`), reszta do miasta - albo
drugi przystanek trasy (VillagerCampaignBehavior), albo sprzedaz czesciowa w zamku (prefiks na `SellGoodsForTradeAction.ApplyInternal` z limitem kategorii
jedzenia) i dalsza droga; kasa zamku placi tylko z nadwyzki (`Spendable`). **Kolizje:** E4/6 (zywnosc warowni, spichlerz okregu - nie liczyc jedzenia
dwa razy), 112 (okno powrotu taboru), MoneyLedger (utarg taborow wedlug klas osad), CartTownExit (130).

### 5.12 Wiekszy udzial wsi w dochodzie pana, mniej z nadwyzki miast

E2 :888 kaze skreslic (zawarte w Z15-5 - 5.7). Kod: renta wsi `PopulationLaw.Daily` (:1173-1192) i stawka BK 70% (BKTaxModel :271-284) - zmienia 173
(oplaty stale, hak `VillageRentYieldsToFees`) i 5.7. Wlasnego kodu nie potrzeba.

### 5.13 Napis w menu miasta: czy ludzi stac na chleb

**Jest:** brak. E4/7 (SCR\e4\47) dodaje `PeopleMenu.cs` - opcje "People of this land" w menu wsi, miasta i zamku (`AddGameMenuOption`, PeopleMenu.cs:26,
okno `ShowInquiry`, bez `SwitchToMenu`). **Latka do wyboru:** postfiks na vanilla `PlayerTownVisitCampaignBehavior.SetIntroductionText(Settlement, bool)` -
private static (GRA :1202), ustawia `{SETTLEMENT_INFO}` (:1271) - dopisac jedno zdanie po angielsku; albo rozszerzyc okno E4/7. Po 173 (liczba "stac").

---

## 3. Jedna definicja zapasu kupcow (warunek 5.1)

| Prog | Gdzie | Uzytkownicy |
|---|---|---|
| staly 20 000 (`TownRentFloorGold`, Settings.cs:643) | PopulationLaw.cs:1186; KingdomTreasury.cs:371 (Levies), :473 (RulingIncomePostfix), :528 (Road Tolls); SpoilsSeal.cs:413, :476 | renta miasta, danina, clo, mennica, polityki, skup lupu gracza |
| 10 000 + 12 x dobrobyt (cel regulatora) | Seal31.TownReserve (Seal31.cs:97), `Spare` :101-113 (minus tarcze) | E3/1 RealPayer (jency, oblezenia, turnieje, myto), 181 porty |
| zamek: max(cel regulatora, `CastlePurseFloorGold` + x dobrobyt) | CastlePurse.Reserve :191, Spendable :377, PayFloor :401 | zawor zamku, zakupy kasy zamku, polityki (B-4) |
| BK: miasto 50 000 + 25 x dobrobyt, zamek 50 000 + 12 x | BK BKEconomyModel.cs:610 | `HandleMarketGold` (-1%) |
| BK: kasa < 20 000 -> "Merchants wealth" ujemny dobrobyt | BK BKProsperityModel.cs:88-91 (przez warstwe BK) | dobrobyt miasta |

Stara 111 dawala jedna granice `TownPurse.TaxFloor` dla renty, korony, daniny, cla, mennicy i polityk (P111 pkt 2). Dzis podatki miast biora takze z pasa
miedzy 20 000 a celem regulatora (mediana celu ok. 67 tys.) - czyli z dosypki.

---

## 4. Kolejnosc w dobie (ArmouryBehavior.cs:1451-1542) i miejsca nowych krokow

Tick osady gry (konsumpcja, "zakupy", regulator) biegnie w `DailyTickTownEvent` - poza naszym blokiem; ksiega mierzy go postfiksami.
Nasz blok: `Seal31.BlockOpen` -> `MoneyLedger.BlockOpen` -> **`PopulationLaw.Daily`** (ludzie miast E4/4, renty) -> `Seal31B.OwnerTaxDaily` -> `RentMerge` ->
Mark(MRent) -> **`CastlePurse.Daily`** -> Mark(MCastle) -> `BuildFunding.Daily` -> ... -> `GentryService` -> `KingdomTreasury.Daily` (powinnosci) ->
**`KingdomTreasury.Levies`** (danina, clo) -> `Ransom178.ThirdsSettle` -> `CrownIncome.Begin` -> `CrownBorrow` -> `CrownGifts` -> reparacje -> `MercContract` ->
`CrownCallToWar` -> `Ransom178.CrownStep` -> `WageRefund` -> **`CrownRents`** -> `CrownIncome.End` -> ... -> `SoldierPay.Daily` -> ... -> `IronBank.Daily` ->
**`ClanBudget.Daily`** (dwor) -> ... -> `SupplyDemand.DailyTrade` -> `MaterialOrders` -> ... -> `MoneyLedger.Daily`.
Miejsca etapu 5 (KL 5.2, P111): `TownPurse.BeforeRents` (dar startowy) przed `PopulationLaw.Daily`; `TownPurse.Daily` (zawor miast) po rentach, obok
`CastlePurse.Daily` (pan dostaje rente przed budowami i powinnosciami); `TownFolk.Daily` po `CastlePurse.Daily`, przed `BuildFunding`; 184 w miejscu daniny
(`Levies`, przed `CrownIncome.Begin` - wplyw dnia); markietani w `DailyTickPartyEvent` albo jednej petli przed `ClanBudget`; nowe migawki ksiegi
(`MName`, MoneyLedger.cs:86-95) i nowi posiadacze w `ReadHolders` (:670) - jedna suma swiata.

---

## 5. Etap 4 a etap 5 - kolizje w kodzie

| Galaz / plan E4 | Pliki wspolne z etapem 5 | Ocena |
|---|---|---|
| E4/6 `w-toku/e4-6` (SCR\e4\46) | PopulationLaw.cs (+23: przyrost - czlon H z `Famine.HungerOf`, nie renty), ArmouryBehavior, Settings, McmSettings, SubModuleMain | tekstowe; ale 5.9 i 5.11 dziela pojecia z Famine - po scaleniu E4/6 |
| E4/7 `w-toku/e4-7` (SCR\e4\47) | ClanBudget.cs (+20: zwolnienia :739-822), PopulationLaw.cs (+1), PeopleMenu.cs (nowe menu osady), ArmouryBehavior, Settings | ClanBudget - inne miejsca niz `Court`; 5.13 - wybrac jedno menu |
| E4/8 plan (PLAN-KODU-E48) | PopulationLaw (18 odwolan: migracja do miast, stan miast), ClanBudget (9), Seal31/CaravanCapital (karawany z ludzi), LevyGold | liczba mieszczan dla 173 - po E4/8 |
| E4/9 plan | ClanBudget (16), PopulationLaw (13) | jw. |
| E4/10 plan | ClanBudget (13), LevyGold (6), VillageTakings (2); zloto najmu do kas miast -30..-37 tys./d (PLAN-KODU-E410 :386-387); "sakiewka weterana do kasy miasta" odlozona do etapu 5 (:56, :60) | zmienia doplyw do kas miast - przed rachunkiem 173 |
| E4/11 (wioski poboczne) | PeopleMenu (rozszerzenie) | 5.13 |

Pliki etapu 5 nieruszane przez E4: CastlePurse, SoldierPay, KingdomTreasury, CrownRents, CrownIncome, MenPurse, Seal31, Seal31B, RealPayer, MoneyLedger
(poza drobnymi), BeeSeal, ShipsToPort181, MarketRoad, MarketCarts, CouncilWages35, SpoilsSeal. Galaz etapu 5 mozna zalozyc od `w-toku/zbior31`
(dfb12df) i scalac z E4 na koniec (zasada testow zbiorczych Jeffa 11.10).

---

## 6. Pomiary i narzedzia dla etapu 5

- Linie juz w logu: "Kasy miast (169c)", "Przeplywy osad (kasy miast | kasy zamkow | kiesy wsi | przyczyny)", "Pieniadz swiata" i "(bilans - przyczyny)",
  "Zawor zamkow (110)", "Utarg wsi (112)", "Korona: wplywy dnia (165)", "Renty korony (180)", "Ludnosc BK (169c)" (ludnosc BK, `ConsumedValue`, budzet gry),
  "Statki (181)" (symulacja reguly przy OFF), "Sakiewka ludzi", "D staly (169c)" (panowie samych zamkow), "Zatkane wsie".
- Brak: grupa `--grupa etap5` w `tools/sprawdz_logi.py` (zna etap2/etap3/etap4, :4-7, :1335-1351); linie "Kiesa ludu" i "Podstawa robocizny" (KL 5.4);
  miara "mediana kiesy panow samych zamkow na 28 dob wojny" i "udzial w stalym dochodzie rodow 30-35%" (dzis tylko D staly mediana 1 865 w 169c [P]).
- Stan wyjsciowy do progow (LOG d120 [P]): sakiewki 22.5 mln / zold 0.78 mln/d = 29 dni (prog 15); zloto swiata +0.24%/d (prog < 0.1% na 28 dob);
  zakup plonu - niesprzedany towar po wizycie 0 szt.; glowy < 5 000: 1.

---

## 7. Do sprawdzenia i do decyzji projektu (z kodu)

1. Skala ludzi dla kiesy ludu: BK `PopulationData` (2.9 mln, spada) czy ksiega ludzi (miasta 10.5 mln w skali tabeli) - jedna zasada na zjawisko (5.5).
2. Warstwa BK dobrobytu - czy delty BK liczone dwa razy (postfiks na RBL i na Default, RBL wola base) - wplywa na sile kary glodu i podatku (5.6, 5.9).
3. "Bitwy" +72 tys./d z niczego (`MapEventParty.CommitGoldChanges`, okno O13) - poza 5.x, ale blokuje prog "zloto swiata < 0.1% na 28 dob".
4. Czy `BKLoyaltyModel` jest czynny (linia "modele czynne" go nie wypisuje) - od tego zalezy, czy kare podatku wojennego dac przez lojalnosc BK.
5. Dar startowy miast przy 111': kasowac (stara 111) czy przelac do kies ludu (KL 5.3, ok. 6.7 mln) - zalezy od tego, czy 111' i 173 ida w jednym wgraniu.
6. 5.11: drugi przystanek taboru czy sprzedaz czesciowa w zamku (BK i gra sprzedaja caly ladunek w jednym miejscu).
7. BEE G (prawdziwa wplata gracza do kasy miasta + dar dla notabli): dzis `BeeSeal` blokuje akcje gracza postfiksami `CanPlayer*` (BeeSeal.cs opis) -
   zmiana na przelew gracz -> kasa miasta (nie w PLAN 5.x; "przed etapem 5" - PLAN :251).
