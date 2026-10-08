# Paczka 170 - wynik agenta 1 (workflow wf_3681591a-583)

## summary

Rozpoznanie 2 (tylko odczyt, nic nie zmienione). Kazda akcja gracza z OPIS 1.2 i zbrojownia ma w BEE jeden wspolny warunek CanPlayer...(..., out string reason). Kazde wejscie przez niego przechodzi: menu osad BEE, pickery SettlementActionService, ekrany BK MilitaryVM/OverviewVM/EconomyVM i konsola BK. BK nie wola zadnej metody BEE z ominieciem tych warunkow: BannerKings.Utils/BetterEconomyBridge.cs wola TryPlayer..., a te jako pierwsze wolaja CanPlayer.... Dlatego 8 postfiksow (__result=false, reason=nasz tekst) zamyka gracza wszedzie. AI przez te metody NIE przechodzi - ma osobne sciezki, ktore juz zamykaja klucze 1/2/3/5, a w trybie BK zachowanie LordInvestment w ogole nie jest rejestrowane.
Trzy rzeczy wymagaja dodatku. (a) 7 opcji menu BEE (inwestycja w miasto 10/50/100k, inwestycja we wies 5/15/30k, dostep do targu) uzywa wyniku CanPlayer... jako warunku widocznosci. Sam postfiks sprawi, ze opcja ZNIKNIE bez powodu. Trzeba owinac OnCondition tych opcji (GameMenuOption.OnCondition to pole publiczne) w OnAfterSessionLaunched: args.IsEnabled=false, args.Tooltip=powod. To samo warto zrobic dla 6 pozostalych opcji: wplata miasto/zamek, zbrojownia, oboz, szkolenie. (b) Znalazlem dodatkowe ujscie dostepne graczowi: przelacznik "Lord wealth realism" w ksiedze BEE (LedgerVM.cs:1720). Wlacza zdejmowanie zlota panom AI w nicosc przez WealthAuditCampaignBehavior.TryRemoveHeroGold (:338-346). Dzis jest wylaczony w better_economy_user.cfg, ale gracz moze go wlaczyc jednym kliknieciem; zamyka go jeden prefiks. (c) Anulowanie szkolenia, polityki, podatki, projekty miasta i zamku oraz majatki nie biora zlota gracza. Majatki sa w trybie BK wylaczone przez samo BEE. Ekran ksiegi i podglad osady nie maja przyciskow akcji, a BEE nie ma dialogow ani komend konsoli.

## findings

Skroty: be/ = scratchpad/ore-supply/be (BEE 1.4.5), bk/ = scratchpad/ore-supply/bk, SAS = be/BetterEconomy.UI/SettlementActionService.cs, SMB = be/BetterEconomy.Behaviors/SettlementMenuBehavior.cs, Bridge = bk/BannerKings.Utils/BetterEconomyBridge.cs. Typy BEE (AccessTools.TypeByName): "BetterEconomy.Behaviors.TownEconomyCampaignBehavior", "...CastleEconomyCampaignBehavior", "...VillageInvestmentCampaignBehavior", "...VillageDevelopmentCampaignBehavior", "...WealthAuditCampaignBehavior". Typy parametrow: TaleWorlds.CampaignSystem.Settlements.Settlement, TaleWorlds.CampaignSystem.Hero, int, typeof(string).MakeByRefType() dla out.

== A. PUNKTY LATANIA (jeden na akcje, postfiks: static void Postfix(ref bool __result, ref string reason) { if (!wl) return; __result=false; reason=TEKST; } - Harmony podaje parametr out jako ref po nazwie "reason") ==

1. Wplata do skarbca MIASTA (sink Town:463)
   TownEconomyCampaignBehavior.CanPlayerContributeTreasury(Settlement settlement, Hero playerHero, int amount, out string reason) : bool - Town:390. Jest jedno takie przeciazenie.
   Wejscia:
   - Menu "bee_town_treasury_contribute" (SMB:898). Warunek pokazania to tylko wlasciciel (SMB:878-884).
   - SAS.ShowTreasuryContributionPicker: SAS:204 (opcja nieaktywna, powod w tytule "Contribute 10000g - {STATUS}" i w podpowiedzi), potem SAS:237 -> TryPlayerContributeTreasury (Town:449, wola Can w :454; przy odmowie zolty komunikat "[LivingEconomy] {REASON}").
   - BK: Bridge:513 -> MilitaryVM.ExecuteContributeTownTreasuryMilitary :652, OverviewVM.ExecuteContributeTreasury :454, BannerKingsCheats be_contribute_treasury :687.
   Co widzi gracz w BK: najpierw okno z kwota, potem czerwony komunikat "Couldn't contribute: {reason}".
   AI: nie uzywa tej metody.

2. Wplata do skarbca ZAMKU (sink Castle:1149)
   CastleEconomyCampaignBehavior.CanPlayerContributeTreasury(Settlement castle, Hero playerHero, int amount, out string reason) : bool - Castle:1086.
   Wejscia:
   - Menu "bee_castle_treasury_contribute" (SMB:1198, warunek: wlasciciel, SMB:423-433).
   - SAS:357 (opcja nieaktywna z powodem) i SAS:392 -> TryPlayerContributeTreasury (Castle:1135, wola Can w :1140).
   - BK: Bridge:1071 -> MilitaryVM.ExecuteContributeCastleTreasury :720 (czerwony komunikat "Couldn't contribute: ...").
   AI: osobna sciezka TickAi Castle:351-358 (klucz 1).

3. Oboz szkoleniowy - budowa i rozbudowa (sink Castle:614)
   CastleEconomyCampaignBehavior.CanPlayerBuildOrUpgradeTrainingCamp(Settlement castle, Hero playerHero, out string reason) : bool - Castle:547.
   Wejscia:
   - Menu "bee_castle_training_camp" (SMB:1246, warunek: wlasciciel, SMB:457-467).
   - SAS.ShowCastleTrainingCampPicker: SAS:467 (powod w linii "Status:"), SAS:490 -> TryPlayerBuildOrUpgradeTrainingCamp (Castle:600, Can w :604). Przycisk Apply w InquiryData zostaje aktywny; po kliknieciu zolty komunikat z powodem.
   - BK: Bridge:987 -> MilitaryVM.ExecuteUpgradeTrainingCamp :675 (okno "Upgrade ... for Xg?", potem "Couldn't upgrade camp: ...").
   AI: TryAiBuildTrainingCamp Castle:1055-1083 (klucz 1).

4. Szkolenie wojsk (sink Castle:718; XP z niczego po zakonczeniu)
   CastleEconomyCampaignBehavior.CanPlayerStartTraining(Settlement castle, Hero playerHero, out string reason) : bool - Castle:626.
   Wejscia:
   - Menu "bee_castle_train_troops" (SMB:1270, warunek: wlasciciel, SMB:474-484).
   - SAS:515 i SAS:544 -> TryPlayerStartTraining (Castle:696, Can w :700).
   - BK: Bridge:1015 -> MilitaryVM.ExecuteStartOrCancelTraining :700, galaz bez aktywnej sesji ("Couldn't start training: ...").
   AI: nie szkoli. Ma tylko bierne XP z gotowego obozu (Castle:486-545) - to temat czesci (2).
   NIE latac CanPlayerCancelTraining (Castle:734): anulowanie nie rusza zlota i pozwala graczowi zakonczyc sesje oplacona w starym zapisie. Wejscia anulowania: menu "bee_castle_cancel_training" SMB:1294, SAS:565/575, Bridge:1043 -> MilitaryVM:695.

5. Inwestycja we WIES (sink VillageInvestment:274; hearth i chlopi z niczego)
   VillageInvestmentCampaignBehavior.CanPlayerInvest(Settlement villageSettlement, Hero playerHero, int amount, out string reason) : bool - VillageInvestment:98. Jest jedno przeciazenie.
   Wejscia:
   - Menu "bee_village_invest_5k/15k/30k" (SMB:1090/1108/1126). Warunek = OnVillageInvestmentCondition(MenuCallbackArgs, int) SMB:1329-1335, ktory zwraca wynik CanPlayerInvest -> po postfiksie opcja ZNIKA.
   - SAS.TryExecuteVillageInvestment :700 -> TryApplyPlayerInvestment(Settlement villageSettlement, Hero playerHero, int amount, out VillageInvestmentSummary summary, out string reason) VillageInvestment:255, ktory wola Can w :258 (zolty komunikat).
   - BK: Bridge.TryVillagePlayerInvest :1098-1110 (przez refleksje Invoke - postfiks i tak dziala, reason wraca w array[last]) -> OverviewVM.ExecuteInvestVillage :505, Cheats be_village_invest :748.
   - Bridge.CanVillagePlayerInvest :590 nie ma wywolan w BK.
   Uwaga: Can nie wymaga wlasnosci (dowolna nie-wroga wies).
   AI: OnClanDailyTick :313 -> TryApplyLordInvestment :354 (klucz 2).

6. Inwestycja w MIASTO 10/50/100 tys. (sink LordInvestment:140; dobrobyt, zywnosc i towar z niczego, a do tego relacje z notablami z niczego, SAS:678)
   TownEconomyCampaignBehavior.CanPlayerInvest(Settlement settlement, Hero playerHero, int amount, out string reason) : bool - Town:853. Latac TYLKO wersje 4-parametrowa; 3-parametrowa (Town:848) i tak wola ja z amount=0.
   Wejscia:
   - Menu "bee_town_invest_10k/50k/100k" (SMB:964/982/1000). Warunek OnTownInvestmentCondition SMB:1320-1327 zwraca wynik Can -> opcja ZNIKA.
   - SAS.TryExecuteTownInvestment :658: Can, potem bezposrednio LordInvestmentCampaignBehavior.ApplyInvestment (static, :128) w SAS:664.
   - BK: brak wejscia.
   AI: w trybie BK LordInvestmentCampaignBehavior NIE jest rejestrowany (BetterEconomySubModule.cs:94-96), wiec AI w ogole nie inwestuje w miasta. ApplyInvestment to wspolne ujscie, ale prefiks na nim zostawilby graczowi komunikat "+0" i darmowe relacje - dlatego latac Can.

7. Dostep do targu 5 000 (sink VillageDevelopment:625; relacje z notablami z niczego :634)
   VillageDevelopmentCampaignBehavior.CanPlayerNegotiateMarketAccess(Settlement villageSettlement, Hero playerHero, out string reason) : bool - VillageDevelopment:230.
   Wejscia:
   - Menu "bee_village_market_access" (SMB:1072). Warunek, lambda <>c.<OnSessionLaunched>b__8_30 (SMB:351-357 / :1052-1058), zwraca wynik Can -> opcja ZNIKA.
   - SAS.ShowVillageMarketAccessPicker :596 i :622 -> TryPlayerNegotiateMarketAccess (:273, Can w :275) -> ApplyMarketAccess(Settlement settlement, Hero payer, bool player), prywatna, :615.
   - BK: brak wejscia.
   AI: osobny warunek :599-609, ale TEN SAM ApplyMarketAccess. Prefiks na ApplyMarketAccess (return false) zamknalby gracza i AI jednym miejscem, ale gracz nie zobaczylby wtedy powodu. Zalecam Can dla gracza; AI zamyka juz klucz 5. ApplyMarketAccess to opcjonalny pas bezpieczenstwa.

8. Zbrojownia (sink Town:577; potem bron z niczego, Town:2152-2266 - temat czesci 2)
   TownEconomyCampaignBehavior.CanPlayerBuildOrUpgradeArmory(Settlement settlement, Hero playerHero, out string reason) : bool - Town:497.
   Wejscia:
   - Menu "bee_town_armory" (SMB:922, warunek: wlasciciel).
   - SAS.ShowArmoryPicker :262 (Status) i :287 -> TryPlayerBuildOrUpgradeArmory (Town:568, Can w :571). Apply zostaje aktywny, po kliknieciu zolty komunikat.
   - BK: Bridge:541 -> MilitaryVM.ExecuteUpgradeArmoryMilitary :632 i OverviewVM.ExecuteUpgradeArmory :481 (okno "Upgrade ... for Xg?", potem "Couldn't upgrade armory: {reason}"), Cheats be_upgrade_armory :716.
   AI: TryApplyAiArmory Town:2083 (klucz 3).

9. NOWE ujscie dostepne graczowi: przelacznik "Lord wealth realism" w ksiedze BEE (LedgerVM.cs:1720-1724, zapis RuntimeSettings.Save do better_economy_user.cfg).
   Wlaczony zdejmuje zloto panom AI w nicosc (MainHero pominiety, WealthAudit:239): ApplyLordWealthControl :160-206 -> TryRemoveHeroGold(Hero hero, int amount), private static bool, WealthAudit:338, GiveGoldAction(hero, null) :346.
   Dzis cfg ma LordWealthRealism=0, ale gracz wlaczy to jednym kliknieciem.
   Punkt: prefiks na TryRemoveHeroGold: __result=false; return false. Zamyka wszystkich (tylko AI jest tu celem); log BEE dalej liczy "affected" tylko przy sukcesie, wiec tez wyjdzie 0.

== B. WIDOCZNOSC POWODU W MENU BEE (potrzebny dodatek, nie da sie tego zalatwic samym postfiksem) ==
- MenuCallbackArgs.IsEnabled i .Tooltip (TextObject) to pola publiczne (cs/TaleWorlds.CampaignSystem.GameMenus/MenuCallbackArgs.cs:10,14). GameMenuOption.GetConditionsHold (:127-139) kopiuje je do opcji, a UI pokazuje opcje wyszarzona z podpowiedzia. Settery IsEnabled/Tooltip w GameMenuOption sa prywatne, wiec postfiks na GetConditionsHold odpada.
- Zalecenie: w CampaignEvents.OnAfterSessionLaunchedEvent (menu BEE juz istnieja - starter.AddGameMenuOption wpisuje opcje od razu do GameMenuManager, CampaignGameStarter:93-105) pobrac Campaign.Current.GameMenuManager.GetGameMenu("bee_town_menu" / "bee_village_menu" / "bee_castle_menu"). W MenuOptions znalezc opcje po IdString i podmienic publiczne pole OnCondition na owijke. Owijka: bool vis = orig(args) || regulaWidocznosci; jesli wylacznik wlaczony i vis, to args.IsEnabled=false, args.Tooltip=new TextObject(TEKST) i return true.
- Reguly widocznosci (bo po postfiksie orig zwraca false):
  - opcje miasta i zamku: Settlement.CurrentSettlement?.OwnerClan == Clan.PlayerClan (tak jak oryginal);
  - inwestycja we wies: CurrentSettlement.IsVillage i brak wojny z frakcja wsi (oryginal :116-120);
  - dostep do targu: wies wlasna albo miasto, do ktorego nalezy wies, jest wlasne (oryginalny IsOwnerEligible :937).
- Id opcji: bee_town_treasury_contribute, bee_town_armory, bee_town_invest_10k, bee_town_invest_50k, bee_town_invest_100k, bee_village_market_access, bee_village_invest_5k, bee_village_invest_15k, bee_village_invest_30k, bee_castle_treasury_contribute, bee_castle_training_camp, bee_castle_train_troops. NIE: bee_castle_cancel_training.
- Delegaty BEE sa statycznie keszowane (<>c.<>9__8_N) i ponownie uzywane w kazdej sesji, a GameMenuOption jest nowy w kazdej kampanii. Owijac raz na sesje, z ochrona przed podwojnym owinieciem (np. sprawdzic, czy OnCondition.Method.DeclaringType to nasz typ). Wywolywanie GetMenuOptionTooltip/IsEnabled: GameMenuManager:196, :321.
- Ekrany BK nie maja wiazania IsEnabled przy przyciskach (bk GUI MilitaryPanel.xml:285-340, OverviewPanel.xml:152-179), tekst pochodzi z wlasciwosci VM. Opcjonalna kosmetyka: postfiksy na gettery MilitaryVM.UpgradeArmoryText / ContributeTownTreasuryText / UpgradeTrainingCampText / StartTrainingText (gdy brak sesji) / ContributeCastleTreasuryText oraz OverviewVM.ContributeTreasuryText / UpgradeArmoryText / InvestVillageText (dopisek " (closed)"). Mozna tez dodac prefiksy na Execute..., zeby nie pytac o kwote. Bez tego dziala i tak: po potwierdzeniu czerwony komunikat "Couldn't ...: {reason}".

== C. Proponowane teksty (krotkie, bo w pickerach wplat trafiaja do tytulu opcji) ==
- treasury (miasto i zamek): "Closed: gold paid into this treasury would simply vanish. In this economy gold must go to someone."
- armory: "Closed: an armory here would make weapons out of nothing. Arms come from smiths and workshops."
- training camp: "Closed: the camp's cost would vanish. In this economy gold must go to someone."
- training: "Closed: paid drill would burn gold and create experience from nothing. Troops learn by fighting."
- village invest: "Closed: in this economy gold must go to someone - build through the town instead."
- town invest: "Closed: the investment would vanish and conjure goods from nothing. Build through trade and workshops instead."
- market access: "Closed: the brokers' fee would vanish. In this economy gold must go to someone."

== D. Sprawdzone i bez zmian (nie biora zlota gracza) ==
- Polityka miasta (SAS:54, TrySetPolicy Town:895, BK EconomyVM:703) i podatki (CanPlayerSetTaxPolicy Town:777, BK EconomyVM:744): tylko stan wirtualny.
- Projekty miasta (CanPlayerStartProject Town:936 / TryStartProject :985; BK Bridge:846 -> EconomyVM:796) i projekty zamku (Castle:1160 / :1202): placi wirtualny skarbiec, nie kiesa gracza. AI startuje je przez TryStartProject(playerAction:false) z pominieciem Can. Jesli czesc (2) uzna wirtualny podatek skarbca (Town:1858-1865, Castle:121-125, "z niczego") za zrodlo do zamkniecia, to punkty gracza sa wlasnie tu.
- Wyplaty skarbcow wlascicielom sa juz zerowane w XML: TownTreasurySurplusPayoutFraction=0, TownTreasuryWarPayoutFraction=0, EstateOwnerPayoutFraction=0.
- Majatki: FeudalEconomy.CanApplyEstateAction :516 zwraca false w trybie BK (IsEnabled => !HasBannerKings, :39, :521).
- Ksiega BEE (LedgerVM/RowVM/TabVM, GUI BetterEconomyLedger.xml): Command.Click tylko ExecuteClose, ExecuteSelect (podglad osady SettlementDetailView.Show - samo okno Close, :192) i ExecuteSort. Jedyna akcja z ekonomia to przelacznik z punktu 9.
- BEE nie ma dialogow (zero AddPlayerLine/AddDialogLine) ani komend konsoli. Umowa handlowa przez barter (TradeAgreementBarterPatch, TradeAgreementBarterable.Apply) nie rusza zlota; clo zamyka klucz 11.
- Pozostale miejsca BK, ktore wolaja BEE (DemesneVM, EstateVM, ClanIncomeEstateVM, BKEstatesModel, BKManagerBehavior, PopulationData, Estate, EstateData, BKEconomyLayerInstaller): odczyty, synchronizacja klas ludnosci, wlasciciele parcel, postfiksy na getterach Feudal. Zaden nie wola akcji placacych.

== E. AI a nasze latki ==
Zadna metoda CanPlayer... ani TryPlayer... nie jest wolana przez AI (grep po be/: tylko Try..., SAS, SMB i Bridge BK). Latki z punktow 1-8 zamykaja tylko gracza. AI zamykaja klucze:
- 1: wplata zamku :351-358, oboz :1055-1083;
- 2: wies :313/:354, doplata do zbrojowni;
- 3: zbrojownia :2083;
- 5: targ :599-609;
- inwestycji AI w miasto nie ma w trybie BK.
Wspolne ujscia AI i gracza, gdyby chciec pasa bezpieczenstwa niezaleznego od pliku: VillageDevelopment.ApplyMarketAccess(Settlement, Hero, bool) :615 oraz LordInvestmentCampaignBehavior.ApplyInvestment(Hero, Town, SettlementPopulation, int, string) :128. Ta druga jest dzis u AI martwa. Latka z punktu 9 dotyczy tylko AI.

== F. Uwagi do kodu ==
- Postfiks zawsze ustawia false, takze gdy oryginal juz odmowil (np. brak wlasnosci). Powod "Closed" jest wtedy prawdziwy i prostszy.
- Male metody warunkow menu (OnTownInvestmentCondition, OnVillageInvestmentCondition, lambdy <>c) moga zostac wkompilowane przez JIT - dlatego owijamy delegat w GameMenuOption, a nie latamy tych metod.
- Te same 8 metod Can z BEE sa wolane przy kazdym otwarciu pickera i przy odswiezaniu menu. Postfiks jest O(1): bool i przypisanie stalej.

