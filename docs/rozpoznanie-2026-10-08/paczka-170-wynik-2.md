# Paczka 170 - wynik agenta 2 (workflow wf_3681591a-583)

## summary

Przeszedlem caly kod BEE (dekompilacja ore-supply/be, v1.4.5). Wszystko tylko czytalem: zadnego pliku nie zmienilem, gry nie uruchamialem. Plik ustawien BEE ma SHA 1f963cfa..., czyli jest nietkniety.

Po 13 kluczach (-ListaZFundamentu) i wpisach 18 i 49 cala strona AI i swiata jest zamknieta. Kazdy warunek sprawdzilem w kodzie.

Otwarte zostaje 13 miejsc:
- **8 akcji gracza.** Wszystkie zamyka jeden postfiks na metodach Can* (__result=false i czytelny powod). Metody Try* z BEE, ekrany BK (MilitaryVM/OverviewVM przez BetterEconomyBridge) i komendy BK najpierw pytaja Can*, wiec latka obejmuje je wszystkie. Opcje menu BEE przy false sie chowaja. Zeby byly widoczne jako wyszarzone z powodem, potrzebny jest postfiks na GameMenuOption.GetConditionsHold po IdString "bee_*".
- **5 biernych zrodel.** Wystarcza prefiksy zwracajace false: produkcja gotowych zbrojowni, XP z gotowych obozow AI, juz odblokowana druga produkcja wsi, ogon oplat 5 000 za dostep do targu oraz zdejmowanie zlota panom. To ostatnie dzis zatrzymuje tylko 21-bajtowy plik better_economy_user.cfg na dysku C.

Wirtualne skarbce zamkow i miast, ich projekty oraz gotowosc garnizonow sa tylko na papierze. Po kluczu 7 i wpisie 49 nie maja juz drogi do prawdziwego zlota, towaru ani ludzi, wiec nie potrzebuja latki. Prawdziwego platnika zamiast zamykania warto rozwazyc tylko przy wplatach do skarbca: GiveGoldAction.ApplyForCharacterToSettlement przekazuje zloto do prawdziwej kasy miasta albo zamku. Przy zbrojowni zalecam zatrzymac produkcje: prawdziwy platnik oznaczalby nowy warsztat, a w wariancie (b) nowych zbrojowni i tak nie bedzie.

## findings

## Rozpoznanie 1: zloto, towar, ludzie i XP z niczego albo w nicosc w BEE

Skroty sciezek:
- **be/** = `C:/Users/GAME/AppData/Local/Temp/claude/C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord/3cf3e0ac-5529-4b68-a794-0edec69cfda7/scratchpad/ore-supply/be/`
- **B/** = be/BetterEconomy.Behaviors/
- **Town** = TownEconomyCampaignBehavior, **Castle** = CastleEconomyCampaignBehavior, **VDev** = VillageDevelopmentCampaignBehavior, **VInv** = VillageInvestmentCampaignBehavior, **Car** = CaravanCampaignBehavior

Typy maja przestrzen nazw `BetterEconomy.Behaviors`, chyba ze napisano inaczej. Wartosci ustawien czytalem z prawdziwego XML (tylko odczyt). Wpisy 18 i 49 z CHANGELOG sa juz w pliku:
- WorkshopDailyInputDrawPerShop = 0
- VillageSecondaryBoundTownShare = 0
- TownTreasurySurplusPayoutFraction = 0 i TownTreasuryWarPayoutFraction = 0
- TradeAgreementCustomsRate = 0
- EstateOwnerPayoutFraction = 0
- w user.cfg: LordWealthRealism = 0

Czynne przy BK (BetterEconomySubModule.cs:73-101): EconomySave, RosterSanitizer, SettlementIntel, SettlementMenu, CulturalMarket, VDev, VInv, Castle, VillageSupply, RouteDanger, EconomicEvent, Feudal (ale `IsEnabled = !HasBannerKings`, :39), Town, TradeAgreement, Caravan, Stats i WealthAudit.

Przy BK nie sa rejestrowane: Population, RecruitmentAudit, Migration i LordInvestment. Jego statyczna metoda `ApplyInvestment` dziala jednak dla gracza. Nieczynne sa tez modele dobrobytu, produkcji wsi, cen i ekonomii. Latka Workshop.ChangeGold konczy sie przy BK (WorkshopProductionPatch.cs:17), pobor ludzi przy werbunku tez (RecruitmentPatch.cs:127).

### A. Juz zamkniete (kluczami albo wpisami 18 i 49) - sprawdzone w kodzie

| Nr | Plik:linia | Metoda | Co | Kto | Czym zamkniete (warunek w kodzie) |
|---|---|---|---|---|---|
| A1 | B/CastleEconomyCampaignBehavior.cs:343-364 (358) | Castle.TryAiContribute | zloto pana do null, potem do wirtualnego skarbca zamku | AI (:326 pomija rod gracza) | Klucz 1: `num = Gold - 1e9 < 0` |
| A2 | B/Castle...cs:1055-1084 (1074) | Castle.TryAiBuildTrainingCamp | 30/60/100 tys. do null | AI | Klucz 1: `Gold - koszt >= 1e9` nigdy nie zachodzi |
| A3 | B/VillageInvestmentCampaignBehavior.cs:311-352, 354-384 (375), 386-443 (hearth :400, ludnosc BK :403) | VInv.OnClanDailyTick, TryApplyLordInvestment, ApplyVillageEffects | zloto do null; hearth i chlopi BK z niczego; relacje | AI | Klucz 2: `Gold - ComputeReserve(1e9 + wojsko x 15) < 8000` daje return (:317) |
| A4 | B/TownEconomyCampaignBehavior.cs:2081-2145 (2137) | Town.TryApplyAiArmory | doplata pana do null; zbrojownia z wirtualnego skarbca | AI | Klucz 3' (artisans < 1e9, :2098); dodatkowo klucz 2 (:2129) |
| A5 | B/Town...cs:1953-2000 (1990) | Town.TryPayTreasurySurplus | wirtualny skarbiec daje prawdziwe zloto z null wlascicielowi (takze graczowi) | swiat | Wpis 49: oba ulamki = 0, wiec num5 = 0 |
| A6 | B/Town...cs:2702-2725, 3034-3083 (3076) | Town.EnsureSnapshot, ConsumeInputs | wsad warsztatow zdejmowany z targu w nicosc | swiat | Wpis 18: `ceil(0 x n) = 0` (:2878) |
| A7 | B/VillageDevelopmentCampaignBehavior.cs:380-394, 660-671 | VDev.TryUnlockSecondaryProduction | odblokowanie drugiej produkcji | swiat | Klucz 6: StableDays (sufit 10 000) >= 1e9 nigdy. NOWE odblokowania zamkniete, stare dzialaja (B3) |
| A8 | B/VDev...cs:438-446 | VDev.TickSecondaryProduction (czesc dla miasta) | kopia 45% na targ miasta | swiat | Wpis 18: share = 0 |
| A9 | B/VDev...cs:458-511, 514-573 (551) | VDev.TickGrievanceAndDiversion, ApplyDiversionStock | towar z niczego na targ innego miasta | swiat (takze wsie gracza) | Klucze 4 i 5: warunek :489 zawsze daje return |
| A10 | B/CaravanCampaignBehavior.cs:640-650, 924-928 | Car.TryDeliverContract | towar ze wsi do miasta bez zaplaty, marza z niczego; tez procurement z wirtualnych skarbcow (Town :2626, Castle :1448) i eksport zbrojowni (Town :653) | wszystkie karawany | Klucz 7 (:650) |
| A11 | B/Car...cs:1001-1046 | Car.TryHireEscort | zloto karawany w nicosc, zbrojni z niczego | wszystkie | Klucz 8 (:1003) |
| A12 | B/Car...cs:1069-1174 | Car.TryPromoteRoster | zloto w nicosc, awans (XP) z niczego | wszystkie | Klucz 9 (:1076) |
| A13 | B/Car...cs:1366-1393 | Car.ApplyDangerPressure | do 7.6% kiesy dziennie w nicosc | wszystkie | Klucz 10: num2 = 0, num3 < 1 daje return |
| A14 | B/TradeAgreementCampaignBehavior.cs:400-422 (wywolanie Car :943-950) | TradeAgreement.AccrueCustoms | clo z null dla krolow | krolowie | Klucz 11 i wpis 49: clo = 0, wiec return na :405 |
| A15 | B/TradeAgreement...cs:836-858 | ApplyEndpointProsperity | dobrobyt z niczego | miasta korytarzy | Klucz 12: num = 0, wiec return |
| A16 | B/VillageSupplyCampaignBehavior.cs:121-124, be/BetterEconomy.Compatibility/BannerKingsAdapter.cs:138-160 | VillageSupply.ApplyRaidImpact, TryApplyRaidFlight | 8% chlopow BK w nicosc | swiat | Klucz 13: num = 0, wiec return na :140 |
| A17 | B/VDev...cs:575-613 -> 615-636 (625) | VDev.TickAiMarketAccess, ApplyMarketAccess | 5 000 do null i relacje | AI | Klucze 4 i 5: platnosc tylko przy DiversionFraction >= 0.1. **Ogon** (najwyzej raz na wies, stary zapis) - patrz B4 |
| A18 | B/FeudalEconomyCampaignBehavior.cs:894-973 (969) | Feudal.TickEstateRent, PayEstateOwner | renta z null | swiat | Nieczynne przy BK (IsEnabled, :39, :634) i wpis 49 |
| A19 | B/WealthAuditCampaignBehavior.cs:72-80, 197, 337-346 | WealthAudit.OnDailyTick, TryRemoveHeroGold | zloto panow do null | AI | TYLKO user.cfg LordWealthRealism=0; wartosc wbudowana to true (Config/RuntimeSettings.cs:13). Patrz B5 |

### B. Otwarte po 13 kluczach - do latki 170

| Nr | Plik:linia | Metoda (pelna nazwa) | Co | Kto | Czy klucze zamykaja | Najprostsza latka |
|---|---|---|---|---|---|---|
| G1 | B/Castle...cs:1086-1133 (Can), 1135-1158 (Try, zloto :1149) | BetterEconomy.Behaviors.CastleEconomyCampaignBehavior.CanPlayerContributeTreasury(Settlement, Hero, int, out string) | wplata do skarbca zamku: zloto do null, skarbiec tylko wirtualny | gracz | NIE (klucz 1 dotyczy tylko AI) | Postfiks: `__result=false; reason="Closed: ..."`. Try wola Can (:1140). Alternatywa z prawdziwym odbiorca: zamienic GiveGoldAction(player, null) na GiveGoldAction.ApplyForCharacterToSettlement(player, castle, amount) - zloto do prawdziwej kasy zamku |
| G2 | B/Town...cs:390-447 (Can), 449-475 (zloto :463) | Town.CanPlayerContributeTreasury(Settlement, Hero, int, out string) | jak G1; przy wyplacie 0 (wpis 49) zloto nigdy nie wraca | gracz | NIE | Jak G1. Wolaja to: menu BEE (SettlementActionService.cs:204/237), BK MilitaryVM.cs:652, OverviewVM.cs:454, BannerKingsCheats.cs:687 (przez BetterEconomyBridge.cs:496 -> Try -> Can) |
| G3 | B/Town...cs:497-566 (Can), 568-583 (zloto :577) | Town.CanPlayerBuildOrUpgradeArmory(Settlement, Hero, out string) | 100-400 tys. do null, potem bron z niczego (B1) | gracz | TAK (klucz 3', :542), ale z powodem "Not enough artisans. Need 1000000000, have N." | Postfiks podmienia tylko powod (i dla pewnosci __result=false). Wolaja: picker BEE :262/:287, BK MilitaryVM.cs:632, OverviewVM.cs:481, BannerKingsCheats.cs:716 |
| G4 | B/Town...cs:848-893 (4-argumentowa :853; 3-argumentowa :848 tylko deleguje), wykonanie be/BetterEconomy.UI/SettlementActionService.cs:633-680 -> B/LordInvestmentCampaignBehavior.cs:127-176 | Town.CanPlayerInvest(Settlement, Hero, int, out string) | 10/50/100 tys. do null (:140); dobrobyt (:157), zywnosc (:158), towar z niczego (InjectRegionalGoods :257); relacje z notablami (SAS :678); rzemieslnicy BEE x(1+k) (:159-162, tylko wirtualnie) | gracz | NIE (rezerwa nieliczona) | Postfiks na przeciazeniu 4-argumentowym. Menu: OnTownInvestmentCondition (SettlementMenuBehavior.cs:1320-1327) -> opcja ukryta, patrz uwaga o menu nizej |
| G5 | B/VInv...cs:98-133 (Can), 255-282 (zloto :274), skutki :386-443 (hearth :400, chlopi BK :403 -> BannerKingsAdapter.cs:114-136, relacje :432) | VInv.CanPlayerInvest(Settlement, Hero, int, out string) | 5/15/30 tys. do null; hearth +10/25/50 i chlopi BK z niczego; relacje; patronat (mnozniki tylko wirtualne) | gracz | NIE | Postfiks. Wolaja: menu (SettlementMenuBehavior.cs:1329-1335), SAS :658/:700, BK BetterEconomyBridge.cs:590 (Can) i :1098 (TryApplyPlayerInvestment -> Can). Opcjonalny bezpiecznik: prefiks false na BetterEconomy.Compatibility.BannerKingsAdapter.TryAddVillagePeasants (jedyny wolajacy to :403) |
| G6 | B/VDev...cs:230-271 (Can), 273-282 -> 615-636 (zloto :625, relacje :650) | VDev.CanPlayerNegotiateMarketAccess(Settlement, Hero, out string) | 5 000 do null i relacje z niczego. Korzysc (tlumienie odplywu) po kluczach 4 i 5 jest zerowa | gracz | NIE | Postfiks. Menu :356/:1057 przy false chowa opcje |
| G7 | B/Castle...cs:547-598 (Can), 600-624 (zloto :614) | Castle.CanPlayerBuildOrUpgradeTrainingCamp(Settlement, Hero, out string) | 30/60/100 tys. do null | gracz | NIE | Postfiks. Wolaja: SAS :467/:490, BK Bridge :987 |
| G8 | B/Castle...cs:626-694 (Can), 696-732 (zloto :718); skutek :448-485 -> 879-951 (XP :924) | Castle.CanPlayerStartTraining(Settlement, Hero, out string) | 3/5/10 tys. do null i XP z niczego dla wojska gracza | gracz (tylko z gotowym obozem, np. zdobyty zamek AI albo stary zapis) | NIE | Postfiks. CanPlayerCancelTraining zostawic otwarte (bez zlota). Ogon: sesja juz oplacona konczy sie po 1-3 dobach (XP :924) - zalecam zostawic |
| B1 | B/Town...cs:2152-2266 (wsad :2220, wyrob :2254), wolane z :1694 | BetterEconomy.Behaviors.TownEconomyCampaignBehavior.TickArmoryProduction(Settlement, TownEconomyState, int) - prywatna | gotowa zbrojownia: zelazo, drewno, skora i narzedzia z targu w nicosc (nikt nie dostaje zaplaty); bron z niczego (do 900 x poziom d na dobe); bez warunku wlasciciela | swiat, AI i gracz (stare zapisy) | NIE (zaden z 13 kluczy). W nowej kampanii zbrojowni nie bedzie (klucz 3' i G3) | Prefiks `return false`. Prawdziwy platnik wymagalby nowego warsztatu (kto placi Town.Gold za wsad, kto dostaje utarg) - zalecam zamknac, bo wariant (b) i tak zamyka budowe. Gotowa zbrojownia zostaje martwa |
| B2 | B/Castle...cs:486-545 (XP :533), wolane z TickTraining :454-457 | Castle.ApplyAiTrainingCampPassive(Settlement, CastleEconomyState) - prywatna | 1-3 XP na zolnierza na tick, z niczego | AI (zamki spoza rodu gracza) | NIE. Klucz 1 blokuje tylko NOWE obozy; obozy w budowie sie koncza (TickTrainingCampConstruction :420-446) | Prefiks `return false` |
| B3 | B/VDev...cs:396-456 (towar :418) | VDev.TickSecondaryProduction(Settlement, VillageDevelopmentState, SettlementPopulation, VillageSupplyLink, int) - prywatna | do 8 szt. na tick z niczego do skladu wsi (potem tabor wiezie na targ za prawdziwe zloto) | swiat (takze wsie gracza) | NIE dla wsi juz odblokowanych (klucz 6 blokuje tylko nowe) | Prefiks `return false` |
| B4 | B/VDev...cs:615-636 (lub 575-613) | VDev.ApplyMarketAccess(Settlement, Hero, bool) - prywatna | ogon AI z A17: 5 000 do null | AI (stary zapis) | Czesciowo | Prefiks `return false` na ApplyMarketAccess zamyka AI i gracza naraz (powod dla gracza i tak daje G6) |
| B5 | B/WealthAudit...cs:337-353 (zloto :346) | WealthAuditCampaignBehavior.TryRemoveHeroGold(Hero, int) - prywatna, statyczna, zwraca bool | zloto panow do null | AI | Zamyka tylko 21-bajtowy user.cfg na dysku C. Przy wyzerowanym albo brakujacym pliku wraca true | Prefiks `__result=false; return false` (bezpiecznik niezalezny od pliku) |

Uwagi do latek z grupy B:
- **Menu BEE chowa opcje, gdy warunek jest false.** Dotyczy to inwestycji w miasto i wies (OnTownInvestmentCondition/OnVillageInvestmentCondition) oraz "Negotiate market access" (lambda `<>c.<OnSessionLaunched>b__8_30`). Zeby opcja byla widoczna i wyszarzona z powodem: postfiks na TaleWorlds `GameMenuOption.GetConditionsHold(Game, MenuContext)` (GameMenuOption.cs:127-139). Dla IdString ze zbioru: `__instance.SetEnable(false)`, Tooltip ustawiony przez AccessTools (prywatny setter, :95), `__result=true`. Identyfikatory (SettlementMenuBehavior.cs:898-1294):
  - bee_town_treasury_contribute, bee_town_armory, bee_town_invest_10k / _50k / _100k
  - bee_village_market_access, bee_village_invest_5k / _15k / _30k
  - bee_castle_treasury_contribute, bee_castle_training_camp, bee_castle_train_troops
  
  Okna wyboru (pickery) BEE pokazuja powod z Can* same (SAS :204, :262, :357, :467, :515, :596).
- **Typy przez AccessTools.TypeByName:**
  - "BetterEconomy.Behaviors.TownEconomyCampaignBehavior", ".CastleEconomyCampaignBehavior", ".VillageInvestmentCampaignBehavior", ".VillageDevelopmentCampaignBehavior", ".WealthAuditCampaignBehavior"
  - "BetterEconomy.Compatibility.BannerKingsAdapter"
  
  Przy CanPlayerInvest trzeba podac typy argumentow (dwa przeciazenia; out string = typeof(string).MakeByRefType()). Wszystkie latki to jeden test na wywolanie - tanie.

### C. Zrodla wirtualne albo nieczynne - bez latki (sprawdzona droga do prawdziwego stanu)

- **Wirtualny skarbiec zamku z hearth** (Castle :119-128, :142-155). Wydaje go tylko:
  - projekt zamku: :289-322 - skutki to gotowosc, patrole, mnozniki;
  - procurement karawan: :1448, zamkniety kluczem 7.
  
  Wyplaty do prawdziwego zlota nie ma; gotowosc i patrole czytaja tylko ekrany i systemy BEE juz zamkniete (Car :791/:796, RouteDanger :76, VillageSupply :52 - przeplyw wirtualny).
- **Wirtualny skarbiec miasta z podatku od ludnosci BK** (Town :1858-1878). Wydaje go: projekty (:2002-2047), zbrojownia AI (A4), procurement (A10) i wyplata (A5 = 0). Projekty miast, polityka podatkowa i gildie zmieniaja tylko wartosci wirtualne. Gracz CanPlayerStartProject/TrySetPolicy/TrySetTaxPolicy - bez prawdziwego zlota, zostawic.
- **Castle.CanPlayerStartProject** (:1160) - placi wirtualny skarbiec, zostawic.
- **Wynajeta juz eskorta karawan** zostaje (to prawdziwi ludzie; Car :424-467 tylko liczy).
- **RosterSanitizer** :171/:181 - naprawa uszkodzonych wpisow armii (ten sam oddzial z powrotem; gdy brak postaci - wpis usuniety). Techniczne, zostawic.
- **PartyTrainingPatch** - XP = 0 tylko dla uszkodzonych wpisow.
- Przy BK nieczynne:
  - **WorkshopProductionPatch**;
  - **pobor ludzi przy werbunku** (RecruitmentPatch);
  - **Feudal** (renta, akcje majatkow);
  - **Population/Migration/LordInvestment AI** - przy odwrotnej kolejnosci BK i BEE wlaczylby sie "neglect decay": dobrobyt i zywnosc w nicosc (LordInvestment :192-219). Ryzyko znane z OPIS 7.
- **Modele czynne przy BK** (PartyWage, PartySizeLimit, MobilePartyFoodConsumption, PartySpeed) tylko opakowuja modele gry; zrodel nie tworza.
- **EconomicEvent, CulturalMarket, RouteDanger, SettlementIntel, Stats** - same mnozniki i odczyty. Zmiana kultury (CulturalMarket :257) jest wylaczona (EnableVanillaSettlementCultureConversion = false).
- **Komend konsoli BEE nie ma.** Komendy BK (BannerKingsCheats.cs:687, :716) ida przez Can*.

### Podsumowanie
- **8 akcji gracza (G1-G8):** postfiks Can* (8 metod, 5 typow) plus postfiks GetConditionsHold dla wyszarzonych opcji menu. Po stronie gracza nic wiecej nie trzeba.
- **5 biernych zrodel:** prefiks false na B1 TickArmoryProduction, B2 ApplyAiTrainingCampPassive, B3 TickSecondaryProduction, B4 ApplyMarketAccess, B5 TryRemoveHeroGold.
- **Opcjonalnie:** bezpiecznik na BannerKingsAdapter.TryAddVillagePeasants.
- **Prawdziwy platnik zamiast zamykania** ma sens tylko przy G1 i G2: GiveGoldAction.ApplyForCharacterToSettlement, zloto trafia do prawdziwej kasy miasta albo zamku. Reszta to efekty z niczego, wiec zamknac.

