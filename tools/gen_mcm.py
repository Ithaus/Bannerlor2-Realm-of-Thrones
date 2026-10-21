import re, sys, os

# zakresy suwakow zgodne z przycieciem w kodzie: nazwa -> (min, max, format); reszta - wzor ogolny ponizej
# T1: godziny obozu (regula ogolna [0, max(10, 4 x domyslna)] dawala przy domyslnej 0 suwak 0..10 - nie dalo sie ustawic 22)
# recenzja 174: suwaki 174 zgodne z przycieciem w kodzie; K1: odsetki 0..100 (regula ogolna dawala 0..200 i 0..300)
RANGES = {
    # m98-plotki: kufle i pijanstwo - kod przycina prog do 1..10, kary do 0..50 % (InnDrink.Threshold / DrunkPct / Recompute)
    'DrunkAfterTankards': (1, 10, "0"),
    'DrunkSpeedPenaltyPercent': (0, 50, "0"),
    'HangoverSpeedPenaltyPercent': (0, 50, "0"),
    'HangoverMoralePenaltyPercent': (0, 50, "0"),
    # m100-kryjowki: krzywa Zwiadu (kod: Full >= Min + 1), odleglosci w jedn. mapy, pamiec rozbitych (HideoutSight.Chance / Pick / PruneCleared)
    'HideoutScoutingMin': (0, 100, "0"),
    'HideoutScoutingFull': (20, 330, "0"),
    'HideoutMinDistSettlement': (0.0, 40.0, "0.0"),
    'HideoutMinDistSideVillage': (0.0, 30.0, "0.0"),
    'HideoutClearedMemoryDays': (0, 364, "0"),
    'HideoutClearedRadius': (0.0, 100.0, "0.0"),
    # m97-posilki: posilki DR marszem - kod przycina do 0.25..6 h, 10..600 s na godzine, 0..300 s rozwiniecia (DrMarchArrival.MaxH / SecH / DepS)
    'DrMaxMarchHours': (0.25, 6.0, "0.00"),
    'DrSecondsPerMarchHour': (10.0, 600.0, "0"),
    'DrDeploySeconds': (0.0, 300.0, "0"),
    # m96-jakosc2: limity na rodzaj (min..max losowania raz na kampanie) i szanse jakosci - kod przycina do 0..50 / 0..500 i 0..0.05
    # (QualityScarcity.Range/ChanceL/ChanceM); min > max - kod bierze max jako min
    'QualityLegendaryPerTypeMin': (0, 50, "0"),
    'QualityLegendaryPerTypeMax': (0, 50, "0"),
    'QualityMasterworkPerTypeMin': (0, 500, "0"),
    'QualityMasterworkPerTypeMax': (0, 500, "0"),
    'QualityLegendaryChance': (0.0, 0.05, "0.0000"),
    'QualityMasterworkChance': (0.0, 0.05, "0.0000"),
    # m95-turniej: okup szranek jako czesc wartosci - kod przycina do 0.05..1 (TourneyRansom.ShareOf)
    'TourneyRansomShare': (0.05, 1.0, "0.00"),
    # m97-oboz: woz i siodlo transportowe - kod przycina do 100..2000 kg, 0..50 %, 0..300 kg (CampCart.CartLoad / SlowPct / SaddleKg)
    'CampWalkMaxMen': (0, 100, "0"),
    'CampWalkMenPerTent': (2, 20, "0"),
    # m99-markietani: rzad markietanow - kod przycina (CampSutlers.MinMen / PerStall / MaxStalls / Markup / StockPer / CashPer / SurgeonPer / SurgeonFee / RevelPer / RevelMorale)
    # m102-tablica: listy gonczne - kod przycina do 0..5 na miasto i 1..60 dni (NoticeBoard.PerTown / DaysUp)
    'WatchBountiesPerTown': (0, 5, "0"),
    'WatchBountyDays': (1, 60, "0"),
    'SutlerRowMinMen': (1, 5000, "0"),
    'SutlerRowMenPerStall': (10, 1000, "0"),
    'SutlerRowMaxStalls': (1, 50, "0"),
    'SutlerRowMarkupPercent': (0, 100, "0"),
    'SutlerRowStockPerStall': (1, 100, "0"),
    'SutlerRowCashPerStall': (0, 100000, "0"),
    'SutlerSurgeonPerStall': (0, 100, "0"),
    'SutlerSurgeonFee': (0, 1000, "0"),
    'SutlerRevelPerMan': (0, 50, "0"),
    'SutlerRevelMorale': (0, 50, "0"),
    'SutlerRevelMoraleCap': (0, 100, "0"),
    'SutlerArmourerPerStall': (0, 100, "0"),
    'CartLoadKg': (100, 2000, "0"),
    'CartSpeedPenaltyPercent': (0, 50, "0"),
    'PackSaddleExtraKg': (0, 300, "0"),
    'CampStartHour': (0, 23, "0"),
    'CampEndHour': (0, 23, "0"),
    'OldStockScrapYield': (0.0, 1.0, "0.00"),
    'OldStockScrapDailyShare': (0.0, 0.1, "0.000"),
    'WorkshopMunitionShare': (0.0, 1.0, "0.00"),
    'WorkshopLineShortageShare': (0.0, 1.0, "0.00"),
    'WorkshopPlanDays': (14.0, 120.0, "0"),
    # R3-09a (p5-tarcze): prog pokrycia tarcz - kod przycina do 0..1 (ShieldWarHelp.Threshold)
    'ShieldWarCoverageBelow': (0.0, 1.0, "0.00"),
    'WorkshopMunitionMaxTier': (1, 6, "0"),
    'TownMaterialOrderDays': (1, 30, "0"),
    'CarterPencePerKgPer100': (0.0, 0.15, "0.0000"),
    'SeaFreightShare': (0.0, 1.0, "0.00"),
    'TownMaterialOrderMinLoadKg': (0.0, 1000.0, "0"),
    'MenGearSavePercent': (0, 100, "0"),
    'GarrisonArmoryMinFillPercent': (0, 100, "0"),
    # 174b: suwaki zgodne z przycieciem w kodzie
    'ShelfIndexSelfCheckDays': (0, 30, "0"),
    'TownMaterialOrderSeaMaxRoute': (100.0, 3000.0, "0"),
    'ShopKeepPieces': (0, 3, "0"),
    # NW O4 (noc 16.10): ciche logi - co ile dob pomiary tylko-log; kod przycina do 1..28 (LogGate.Every)
    'QuietLogEveryDays': (1, 28, "0"),
    # zapis-d4 (noc 17/18.10): prog pakowania klucza w KB tekstu - kod przycina do 8..4096 (SaveSize.MinChars)
    'SaveCompactMinKb': (8, 4096, "0"),
    # SW-DROGI (noc 17/18.10): premia drogi ROT - kod przycina procent do 0..50 i margines do 0..2 jedn. (MapRoads.PctOf / MarginOf)
    'MapRoadSpeedPercent': (0, 50, "0"),
    'MapRoadMargin': (0.0, 2.0, "0.00"),
    # n20-drogi (recenzja, uwaga 1): szersze pole pod nowym kluczem - kod przycina do 0..2 jedn. (MapRoads.MarginOf)
    'MapRoadWideMargin': (0.0, 2.0, "0.00"),
    # n20-drogi (noc 19/20.10): premia drogi bocznej - kod przycina do 0..50 (MapRoads.SidePctOf)
    'MapSideRoadSpeedPercent': (0.0, 50.0, "0.0"),
    # m96-valyria: premia drogi Valyrii - kod przycina do 0..50 (MapRoadKinds.ValPct; nie mniej niz premia traktu)
    'MapValyrianRoadSpeedPercent': (0, 50, "0"),
    # p5-bloto (R3-07c): kara blota poza droga w deszczu - kod przycina do 0..50 (MapMud.PctOf)
    'MapMudSpeedPercent': (0, 50, "0"),
    # p6-okup (R3-03a): stawki okupu wsi (kod przycina do 0..1, gorna >= dolnej) i dni ochrony (kod przycina do 1..364)
    'RaidTributeShareLow': (0.0, 0.5, "0.00"),
    'RaidTributeShareHigh': (0.0, 0.5, "0.00"),
    'RaidTributeSpareDays': (1, 364, "0"),   # recenzja n23: suwak jak przyciecie w kodzie (rok Armoury 364 dni; 84 to rok gry bez Armoury)
    # p5-bloto (recenzja 20.10): kara na mokrym po deszczu (LightRain) - kod przycina do 0..50 (MapMud.Clip)
    'MapMudWetSpeedPercent': (0.0, 50.0, "0.0"),
    # p6-sztormy (R3-05d): mnoznik zimowych burz - kod przycina do 1..2.3 (SeaStorms.FactorOf); kara statku w burzy - kod przycina do 0..50 (SeaStorms.PctOf)
    'SeaStormWinterFactor': (1.0, 2.3, "0.00"),
    'SeaStormShipSpeedPercent': (0.0, 50.0, "0.0"),
    # p6-drogi (R3-07d): AI wybiera drogi - kod przycina marsz do 10..200 jedn. i nadklad do 0..50% (AiRoadRoutes.MinDist / Detour)
    'MapRoadAiMinDistance': (10, 200, "0"),
    'MapRoadAiDetourPercent': (0, 50, "0"),
    # f22-drogi-perf (noc 17/18.10): najdalsza odleglosc komorek bocznych drog od kamery - kod przycina do 40..400 (MapSideRoads.ChunkCap)
    'MapSideRoadsChunkRadiusCap': (40, 400, "0"),
    # A17 F5 (noc 17/18.10): stocznia z drewna i za place - pnie na 1000 wartosci kadluba (0..10), udzial plac w wartosci kadluba (kod przycina do 0..1)
    'ShipyardTimberPer1000': (0.0, 10.0, "0.0"),
    'ShipyardLabourShare': (0.0, 1.0, "0.00"),
    # T10 poprawka recenzji: prog dlugu snu w calych nocach (kod porownuje dlug calkowity) - suwak bez ulamkow w opisie
    'AiNightsAwakeInChase': (0.0, 4.0, "0"),
    # T10-R: noce forsownego marszu z rzedu (dlug konczy sie na 3, dalej liczy juz tylko seria) - 0 = bez limitu
    'MaxForcedNights': (0, 5, "0"),
    # R3-08c / R3-08a (20.10): marsz forsowny (kod przycina 0..50) i dzien odpoczynku armii (kod przycina 2..14, 0..20, 0..30)
    'ForcedMarchSpeedPercent': (0, 50, "0"),
    'ArmyRestDayAfterMarchDays': (2, 14, "0"),
    'ArmyRestDayCohesion': (0.0, 20.0, "0.0"),
    'ArmyRestDayMorale': (0.0, 30.0, "0.0"),
    # paczka 175: suwaki z projektu (rozdz. 5)
    'NorthHardyWeaponBonus': (0, 50, "0"),
    'NorthHardyAthleticsBonus': (0, 50, "0"),
    'NorthHomeEdgePercent': (0, 25, "0"),
    'DothrakiRidingBonus': (0, 50, "0"),
    # 175c: zasada stali Innych
    'OthersCastleSteelPercent': (15, 100, "0"),
    # B5 (PLAN 1.15): limit hordy Zewu Nocnego Krola w ludziach (0 = bez limitu; CrashScribe NightKingCall)
    'NightKingCallMaxHost': (0, 6000, "0"),
    # 114: udzial pana w zaworze zamku (kod przycina do 0..1; regula ogolna dalaby 0..2.68)
    'CastleDuesLordShare': (0.0, 1.0, "0.00"),
    # 165 (C1): udzialy przycinane w kodzie do 0..1; doby oddawania zapasu korony
    'CrownReparationShare': (0.0, 1.0, "0.00"),
    'CrownRefundOwnTownsCut': (0.0, 1.0, "0.00"),
    'CrownReserveReleaseDays': (30.0, 1440.0, "0"),
    # 180 (C2): udzial normy zalogi przycinany w kodzie do 0..1; doby sluzby najwyzej 60 (okno dob wojny)
    'CrownRentGarrisonShare': (0.0, 1.0, "0.00"),
    'CrownRentServiceDays': (0, 60, "0"),
    # 168 (D): udzialy przycinane w kodzie do 0..1 (raty do 0..0.5)
    'WarCreditMaxShareD': (0.0, 1.0, "0.00"),
    'WarCreditLootRepayShare': (0.0, 1.0, "0.00"),
    'IronBankMaxInstalmentShare': (0.0, 0.5, "0.00"),
    'AllInstalmentsMaxShare': (0.0, 0.5, "0.00"),
    # 178 (D): udzialy przycinane w kodzie do 0..1
    'RansomCashShare': (0.0, 1.0, "0.00"),
    'CrownGreatCaptiveReward': (0.0, 1.0, "0.00"),
    'CrownThirdsShare': (0.0, 0.5, "0.000"),
    # L5b-1 (skarbiec stolicy): czesc przycinana w kodzie do 0..1 (CapitalTreasury.Fall)
    'CapitalTreasuryShare': (0.0, 1.0, "0.000"),
    'RansomHeadYears': (0.0, 3.0, "0.00"),
    # 186: korona pozycza w Banku - udzial przycinany w kodzie do 0..1, mnoznik >= 1, doby >= 1
    'CrownLoanMaxIncomeShare': (0.0, 1.0, "0.00"),
    'CrownArrearsEnemyCredit': (1.0, 3.0, "0.00"),
    'CrownLoanLimitDays': (0.0, 728.0, "0"),
    'CrownLoanRepayDays': (30.0, 728.0, "0"),
    'CrownLoanPoolDays': (1.0, 360.0, "0"),
    # f20-d3: zysk Banku do Braavos - doby przycinane w kodzie do 1.. (wyplata) i 1..364 (pierscien rezerwy), udzial miasta do 0..1
    'IronBankDividendDays': (1, 364, "0"),
    'IronBankDividendTownShare': (0.0, 1.0, "0.00"),
    'IronBankReserveLendDays': (1, 364, "0"),
    # E5/B2 (184): podatek wojenny ludu - pulapy przycinane w kodzie do 0..0.2, krok do 0..0.05, doby zapasu wojny do 0..728 (KingdomTreasury.WarTax)
    'WarTaxMax': (0.0, 0.1, "0.000"),
    'WarTaxMaxWithPolicy': (0.0, 0.2, "0.000"),
    'WarTaxStep': (0.0, 0.02, "0.000"),
    'WarTaxReserveDays': (0, 364, "0"),
    # E4/1 (189): czynnik cieplych krain i udzial paszy - przycinane w kodzie do 0..1
    'WinterWarmRealmFactor': (0.0, 1.0, "0.00"),
    'WinterHorseFeedShare': (0.0, 1.0, "0.00"),
    # E4/5 (113n): spustoszenie - suwaki zgodne z przycieciem w kodzie (pulap najwyzej 95 - 113 ryzyko 11; procenty 0..100; powrot Q+1..; elastycznosc 0..2)
    # E4/11 (193): ludzi na osade - przycinane w kodzie do 50..5000 (VillageBook.EnsureN); suwak 50..1000
    'DistrictSettlementPeople': (50, 1000, "0"),
    'DevastationPerRaiderDay': (0.0, 2.0, "0.00"),
    'DevastationPerForagerDay': (0.0, 1.0, "0.00"),
    'DevastationMaxPercent': (0, 95, "0"),
    'DevastationKilledPercent': (0, 100, "0"),
    'DevastationKilledSlaverPercent': (0, 100, "0"),
    'DevastationOutlawBasePercent': (0, 100, "0"),
    'DevastationOutlawDangerPercent': (0, 100, "0"),
    'DevastationCaptivesPercent': (0, 100, "0"),
    'RefugeeReturnQuietDays': (0, 120, "0"),
    'RefugeeReturnDays': (30, 1092, "0"),
    'RefugeeYearlyLossPercent': (0.0, 20.0, "0.0"),
    'DevastationYieldElasticity': (0.0, 2.0, "0.00"),
    # E4/6 (191): spichlerze - suwaki zgodne z przycieciem w kodzie (udzial 0..1, prog 0..90, zgony 0..100, sufit od 0.05 roku)
    'FamineStartShare': (0.0, 1.0, "0.00"),
    'FamineThresholdPercent': (0, 90, "0"),
    'FamineDeathMaxPercent': (0, 100, "0"),
    'FamineBarnYears': (0.05, 8.0, "0.00"),
    'GranarySpoilagePercentPerYear': (0.0, 30.0, "0.0"),     # f23-spichlerze: lekkie psucie zapasu spichlerza (domyslnie 6%/rok)
    'FamineYieldPerPerson': (0.5, 3.0, "0.00"),
    'FamineRefugeRadius': (0.0, 1000.0, "0"),
    # E4/7 (164b-1): wolne rece - udzialy przycinane w kodzie do 0..1, elastycznosc 0..2 (FreeHands.Update)
    'FreeHandsShare': (0.0, 1.0, "0.00"),
    'FreeHandsHarvestFactor': (0.0, 1.0, "0.00"),
    'FreeHandsElasticity': (0.0, 2.0, "0.00"),
    'FreeHandsIronIslandsExtraMax': (0.0, 1.0, "0.00"),
    # E4/8 (164b-2): kara miasta wobec stopy wsi (pkt rocznie) i dni dopelnienia miasta ze wsi (0 = tylko ubytek naturalny)
    'TownGrowthPenaltyPercent': (0.0, 3.0, "0.0"),
    'TownMigrationRefillDays': (0, 1456, "0"),
    # E4/10 (167): weterani - udzial przycinany w kodzie do 0..1, czekanie od 1 doby (do 3 lat dlugiego roku), tier 1..7
    'VeteranShareToTavern': (0.0, 1.0, "0.00"),
    'VeteranWaitDays': (1, 1092, "0"),
    'VeteranMinTier': (1, 7, "0"),
    # E4/8 K8: limit dzienny A5 zalogi (kod przycina do 1..5; 1 = gra)
    'GarrisonAutoRecruitPerDay': (1, 5, "0"),
    # E4/9 (192): budzet pul Strazy (kod przycina 0..100 - suwak 0..12), niewolni Zelaznych na start (x PopulationScale), smierc niewolnych (kod przycina 0..100 - suwak 0..20)
    'WatchPoolSharePercent': (0, 12, "0"),
    'IronIslandsStartThralls': (0, 108000, "0"),
    'ThrallYearlyLossPercent': (0, 20, "0"),
    # E5/0: tryb kiesy ludu 0..1 (regula ogolna dalaby suwak 0..10)
    'TownFolkMode': (0, 1, "0"),
    # E5/A1 (111'): tryb regulatora kas miast przycinany w kodzie do 0..2; udzial pana w zaworze kasy miasta do 0..1 (TownPurse.Mode, ValveSplit.Unit)
    'TownPurseRegulator': (0, 2, "0"),
    'TownRentLordShare': (0.0, 1.0, "0.00"),
    # E5/B3 (Z15-5): udzial pana w zaworze kasy miasta z wyrownaniem dla panow zamkow - kod przycina do 0..1 (ValveSplit.Unit); wagi rent - regula ogolna
    'TownRentLordShareWithFolk': (0.0, 1.0, "0.00"),
    # E5/A3 (163): markietani - udzialy przycinane w kodzie do 0..1 (Sutlers); doby rozruchu 0..112
    'SutlerDailyShare': (0.0, 1.0, "0.00"),
    'SutlerTownShare': (0.0, 1.0, "0.00"),
    'SutlerRampDays': (0, 112, "0"),
    # E5/B1 (173): kiesa ludu czynna - udzialy przycinane w kodzie do 0..1 (TownFolk.Unit), akcyza i obrot P 0..0.2, zapas dni W 1..60, oplaty wsi 0..50 zl rocznie
    'TownFolkServiceShare': (0.0, 1.0, "0.00"),
    'TownFolkLabourFood': (0.0, 1.0, "0.00"),
    'TownFolkLabourGoods': (0.0, 1.0, "0.00"),
    'TownFolkLabourArms': (0.0, 1.0, "0.00"),
    'TownFolkLabourVillageBuys': (0.0, 0.5, "0.00"),   # GL-B / 11c: robocizna od skupu plonu wsi (TownFolk.Unit przycina do 0..1)
    'TownFolkCraftWageShare': (0.0, 1.0, "0.00"),
    'TownFolkMerchantDraw': (0.0, 0.2, "0.000"),
    'TownFolkMerchantProfitShare': (0.0, 1.0, "0.00"),
    'TownFolkCastleProfitShare': (0.0, 1.0, "0.00"),
    'TownFolkExcise': (0.0, 0.2, "0.000"),
    'TownFolkReserveDays': (1, 60, "0"),
    'VillageLordScale': (0.0, 1.0, "0.00"),
    'VillagePurchasePerHead': (0.0, 1.0, "0.00"),
    'VillageDuesPerHeadYear': (0.0, 50.0, "0.0"),
    # E5 decyzja 12.10 (wsie nie gromadza): dopisek pana i tempo nadwyzki przycinane w kodzie do 0..1 (TownFolk.Unit), dziesiecina wsi do 0..0.2
    'VillageLordExtraShare': (0.0, 1.0, "0.00"),
    'TitheVillageShare': (0.0, 0.2, "0.00"),
    'FolkSurplusSpendShare': (0.0, 1.0, "0.00"),
    # P1 (obieg kiesy ludu): danina z oszczednosci - kod przycina do 0..0.2 (KingdomTreasury.WarAid); suwak 0..0.1 z trzema miejscami (regula ogolna pokazalaby 0.025 jako 0.03)
    'FolkWarAidShare': (0.0, 0.1, "0.000"),
    # P1b: kara lojalnosci za danine (kod przycina koszt do 0..5, podloge i prog do 0..100)
    'FolkWarAidLoyaltyCost': (0.0, 5.0, "0.00"),
    'FolkWarAidLoyaltyFloor': (0.0, 100.0, "0"),
    'FolkWarAidMinLoyalty': (0, 100, "0"),
    # P2: lud buduje miasto - udzialy przycinane w kodzie do 0..1; sufit ceny materialu 0..5 (0 = bez sufitu)
    'FolkWorksShare': (0.0, 0.2, "0.000"),
    'FolkWorksHands': (0.0, 0.2, "0.000"),
    'BuildMaterialPriceCap': (0.0, 5.0, "0.0"),
    # P2 recenzja: najmniej dob na poziom budowli z robot ludu - kod przycina do 0..3650 (0 = bez limitu)
    'FolkWorksLevelDays': (0.0, 365.0, "0"),
    # P3-P5 (zbytki): tempo listy dostatku w dniach - kod przycina do 1..3650; cena graniczna jako wielokrotnosc ceny zwyklej - kod przycina do 1..10
    'FolkSurplusSpendDays': (10.0, 365.0, "0"),
    'FolkLadderPriceCeiling': (1.0, 5.0, "0.00"),
    # W5 (doplata korony do zoldu wedlug potrzeby): cel wypelnienia i krok werbunku jako czesc limitu druzyn - kod przycina do 0..1 (KingdomTreasury.NeedAid)
    'CrownWarAidFillTarget': (0.0, 1.0, "0.00"),
    'CrownWarAidRecruitStep': (0.0, 1.0, "0.00"),
    # W6 (tarczowe): stawka jako czesc podstawy - kod przycina do 0..1 (KingdomTreasury.Scutage)
    'ScutageRate': (0.0, 1.0, "0.00"),
    # W5b: prog braku wojska u panow jako czesc naleznego zwrotu zoldu - kod przycina do 0..1 (KingdomTreasury.WarTax); tempo skarbca ponad zapas wojny w dobach - kod: co najmniej 1 (KingdomTreasury.CrownSurplus)
    'WarAidLordsShortfallShare': (0.0, 0.5, "0.00"),
    'CrownSurplusAidDays': (1, 360, "0"),
    # W7 (stawka wojenna pana): stawka nadwyzki na dobe - kod przycina do 0..0.2, suwak 0..0.1 z trzema miejscami; koszt lojalnosci 0..10 i dobrobytu 0..50
    # na dobe przy pelnej stawce (kod przycina tak samo); prog lojalnosci 0..100 (KingdomTreasury.LordLevy)
    'LordWarLevyRate': (0.0, 0.1, "0.000"),
    'LordWarLevyLoyalty': (0.0, 5.0, "0.00"),
    'LordWarLevyProsperity': (0.0, 10.0, "0.00"),
    'LordWarLevyMinLoyalty': (0, 100, "0"),
    # kiesa2 (podatek od nadwyzki ludu): stawka na dobe - kod przycina do 0..0.1, suwak 0..0.05 z trzema miejscami; poziom normalny jako wielokrotnosc
    # zapasu dni - kod przycina do 1..20; udzial pana - kod przycina do 0..1 (TownFolk.Hoard.cs)
    'FolkHoardTaxRate': (0.0, 0.05, "0.000"),
    'FolkHoardNormalTimes': (1.0, 10.0, "0.0"),
    'FolkHoardTaxLordShare': (0.0, 1.0, "0.00"),
    # obieg2 (straznik obiegu, pozyczki od ludu, cena graniczna wedlug zasobnosci): zakresy zgodne z przycieciem w kodzie (TownFolk.Hoard.cs GdLo/GdHi/GdMax/GdCrown,
    # TownFolk.Loans.cs LnRate/LnShare/LnDays/LnRepay/LnDefault, TownFolk.Luxury.cs LxCeilFor)
    'FolkGuardBandLow': (1.0, 3.0, "0.00"),
    'FolkGuardBandHigh': (1.0, 5.0, "0.00"),
    'FolkGuardMaxTimes': (1.0, 10.0, "0.0"),
    'FolkGuardCrownShare': (0.0, 0.8, "0.00"),
    'FolkGuardCrownTarget': (0.0, 0.5, "0.00"),
    'FolkLoanRateYear': (0.0, 0.5, "0.00"),
    'FolkLoanMaxShareOfIncome': (0.0, 1.0, "0.00"),
    'FolkLoanLimitDays': (0.0, 120.0, "0"),
    'FolkLoanRepayShareOfIncome': (0.0, 0.5, "0.00"),
    'FolkLoanDefaultDays': (7, 364, "0"),
    # korona2 (15.10): pozyczki koron od miast - zakresy zgodne z przycieciem w kodzie (TownFolk.CrownLoans.cs: KlRate 0..0.5, KlLimitDays 0..364, KlDefault 7..364)
    'CrownTownLoanRateYear': (0.0, 0.5, "0.00"),
    'CrownTownLoanLimitDays': (0.0, 364.0, "0"),
    'CrownTownLoanDefaultDays': (7, 364, "0"),
    # f8-pozyczka (18.10): pozyczka miejska - zakresy zgodne z przycieciem w kodzie (TownFolk.TownLoan.cs: TlRate 0..0.5, TlRentDays 0..364, TlFolkTimes / TlCofferTimes 1..5)
    'TownLoanRate': (0.0, 0.5, "0.00"),
    'TownLoanRentDays': (0.0, 364.0, "0"),
    'TownLoanFolkReserveTimes': (1.0, 5.0, "0.0"),
    'TownLoanCofferReserveTimes': (1.0, 5.0, "0.0"),
    # m96-dlugi (20.10): dlugi rad miast - zakresy zgodne z przycieciem w kodzie (TownDebt.cs: Clamp stopy 0..0.5 / 0..1, limit 0..20, splata 0..1, dobrobyt 0..10,
    # podatek 0..0.5, przerwa 7..364, lojalnosc 0..50, dobrobyt podatku 0..0.5, relacja 0..20)
    'TownDebtRate': (0.0, 0.5, "0.00"),
    'TownDebtBankRate': (0.0, 1.0, "0.00"),
    'TownDebtLimitReserve': (0.0, 20.0, "0.0"),
    'TownDebtRepayShare': (0.0, 1.0, "0.00"),
    'TownDebtProsperityPerDay': (0.0, 10.0, "0.00"),
    'TownDebtLevyShare': (0.0, 0.5, "0.00"),
    'TownDebtLevyGapDays': (7, 364, "0"),
    'TownDebtLevyLoyalty': (0.0, 50.0, "0.0"),
    'TownDebtLevyProsperity': (0.0, 0.5, "0.000"),
    'TownDebtLevyRelation': (0, 20, "0"),
    'TownDebtNotableKeep': (0, 50000, "0"),
    'FolkLadderCeilingMax': (1.0, 10.0, "0.0"),
    # E5/B4 (199): targ przy zamku - dni jedzenia podzamcza na polce zamku (kod przycina do 1..30, CastleMarket.Days)
    'CastleMarketFoodDays': (1.0, 30.0, "0"),
    # E6/K2 (212): luz zapasu koni jako udzial jezdzcow kategorii (kod przycina do 0..1, Stables.Reserve)
    'AiSpareHorseShare': (0.0, 1.0, "0.00"),
    # E8/Z (251): sufit ceny zakupow brakow AI jako mnoznik wartosci sztuki (0 = wylaczony; kod liczy wartosci miedzy 0 a 1 jako 1 - AiBuyRules.CapFactor)
    'AiGearMaxPriceFactor': (0.0, 5.0, "0.0"),
    # A8 (14.10): odsetek polki przycinany w kodzie do 0..100 (Stables: ClampFloat 0..1) - regula ogolna dalaby przy domyslnej 40 suwak 0..160
    'AiMountMarketSharePercent': (0, 100, "0"),
    # E8/C (253): waga uprzezy do kosztu w kg na punkt pancerza konia (0 = waga gry) i dni pracy helmu plytowego t4-6 na kg (0 = wzor napiersnika)
    'HarnessKgPerArmor': (0.0, 1.0, "0.00"),
    'HelmDaysPerKg': (0.0, 3.0, "0.00"),
    # E6/K5 (213): udzwig luzaka pod jukami w kg (20 = jak w grze - dopisek 0; PackMarket.CapPostfix)
    'SpareHorseCarryKg': (20, 100, "0"),
    # E6/W3 (221): regulator wsi - sufit kroku x1.3 (kod przycina do 1..2), progi stosu w dniach zuzycia (kod: niski >= 1, wysoki > niski)
    'VillageOutputStep': (1.0, 2.0, "0.00"),
    'ValveStockDaysLow': (7, 120, "0"),
    # N5 (poprawka 5 po biegu rocznym): suwaki zgodne z przycieciem w kodzie (HarvestValve.Close, WorkshopLaw.HandsStep, WarFront)
    'ValveShortCeiling': (1.0, 8.0, "0.00"),
    'WorkshopHandsStep': (1.0, 3.0, "0.00"),
    'CivilWorksCalmDays': (1, 60, "0"),
    'CivilWorksFrontRange': (10.0, 300.0, "0"),
    'ValveStockDaysHigh': (14, 240, "0"),
    # E6/K3 (219): czesc polki miasta na konie rycerzy w jednym postoju (kod przycina do 0..1, Stables.KnightBuy)
    'KnightHorseMarketShare': (0.0, 1.0, "0.00"),
    # F5 (K3-3, test zbiorczy 5-8): udzial kiesy pana ponad rezerwe na zwykle konie z przydzialu sprzetu rodu (kod przycina do 0..1, Stables.OnSettlementEntered; 0 = wylaczone)
    'AiMountGearPurseShare': (0.0, 1.0, "0.00"),
    # E7/W1 (232): kto slyszy wiesci z wiosek - 0 nikt, 1 wlasne lenna, 2 cale krolestwo (VillageNews.Shows)
    'VillageRaidMessages': (0, 2, "0"),
    # E7/R1 (235): oddzial rabunku osada po osadzie i najdluzsza czujnosc - zgodnie z przycieciem w DistrictRaid.Start / EndFin
    'RaidDetachmentMen': (20, 2000, "0"),
    'RaidAlertMaxDays': (1, 60, "0"),
    # M6a: najkrotsza sluzba najemnika (kod przycina do 1..364), okno i podloga okupu powtornego (kod przycina do 1..364 i 0..1)
    'MercMinServiceDays': (1, 112, "0"),
    'RansomRepeatDays': (1, 364, "0"),
    'RansomRepeatFloor': (0.0, 1.0, "0.00"),
    # N7 (rozbici pod choragiew): czas czekania w osadzie zbiorki i zasieg odbioru w dobach marszu (StragglersRally.Daily)
    'StragglersRallyWaitDays': (5, 90, "0"),
    'StragglersRallyReachDays': (0.5, 15.0, "0.0"),
    # lup miast (L0-L3, CitySack): czesci monety przycinane w kodzie do 0..1, okno lupu do 0..30 dob (CitySack.ShareOf / WindowDays)
    'SackSharePillage': (0.0, 1.0, "0.000"),
    'SackShareDevastate': (0.0, 1.0, "0.000"),
    'SackNoSpendingDays': (0, 30, "0"),
    'SackRuinRebuildShare': (0.0, 1.0, "0.000"),   # LB: odbudowa ze zgliszcz - czesc kosztu poziomu (CitySack.RuinShare przycina do 0..1)
    # RP (decyzja Jeffa 14.10): co ile poziomow punkt cechy (0 = gra) i dodatkowe skupienie (0 = wylaczone); kod przycina do 0..62 (LevelPoints.Every)
    'AttributePointEveryLevels': (0, 12, "0"),
    'ExtraFocusEveryLevels': (0, 12, "0"),
    # okup zamiast szturmu (SiegeRansom): czesc monety przycinana do 0..1 (SiegeRansom.Share), prog krwawego szturmu >= 0 (BloodyRatio; 0 = powod wylaczony)
    'SiegeRansomShare': (0.0, 1.0, "0.000"),
    'SiegeRansomBloodyRatio': (0.0, 5.0, "0.00"),
    'SiegeRansomTruceDays': (0, 364, "0"),   # rozejm po okupie - kod przycina do 0..364 (SiegeRansom.TruceDays)
    # mury oblezen OB1-OB4 (SiegeWalls): zakresy zgodne z przycieciem w kodzie
    'SiegeCapitalWallsFactor': (1.0, 2.0, "0.00"),
    'SiegeAssaultRepelLoss': (0.0, 0.5, "0.00"),
    'SiegeAssaultRepelSpread': (0.0, 0.1, "0.00"),
    'SiegeRepelPauseDays': (1.0, 28.0, "0.0"),
    'SiegeReliefWallsShare': (0.0, 1.0, "0.00"),
    'SiegeBesiegerPrepPercent': (10, 100, "0"),
    'SiegeTownPrepFactor': (1.0, 4.0, "0.00"),
    'SiegeCapitalPrepFactor': (1.0, 4.0, "0.00"),
    # bitwa szturmu SZ2/SZ4 (SiegeWalls): drabiny przycinane w kodzie do 1..4, front do 0..1
    'SiegeLadderWallsFactor': (1.0, 4.0, "0.00"),
    'SiegeMachinesFront': (0.0, 1.0, "0.00"),
    'SiegeBreachFront': (0.0, 1.0, "0.00"),
    # oblezenia 2 (SiegeWalls2): zakresy zgodne z przycieciem w kodzie (CastleDays 0..60, LossPct 0..20, Share 0..0.5, Men 0..200, LadderDays 0..364, Ratio 2..20)
    'SiegeTrebuchetCastleDays': (0, 60, "0"),
    'SiegeStarveLossPercent': (0.0, 20.0, "0.0"),
    'SiegeSurrenderShare': (0.0, 0.5, "0.00"),
    'SiegeSurrenderMen': (0, 200, "0"),
    'SiegeLadderMinDays': (0, 364, "0"),
    'SiegeLadderMinRatio': (2.0, 20.0, "0.0"),
    # OB6 (noc-drobne 16.10, SiegePhantomLift): doby 1..30 i ludzie 1..50 - jak przycinanie w kodzie (SiegePhantomLift.LiftDays / MinMen)
    'SiegePhantomLiftDays': (1, 30, "0"),
    'SiegePhantomMinMen': (1, 50, "0"),
    # CR (zasada Jeffa 14.10): czas ciazy w dobach, 0 = gra (36); kod przycina do 0..364 (PregnancyYear.Days)
    'PregnancyDays': (0, 364, "0"),
    # wyspy-wolni (decyzja Jeffa 14.10): udzialy przycinane w kodzie do 0..1 (ReaverLaw.Q / Qw), doby zaplaty 1..28 (ReaverLaw.PayDays), dziesiecina 0..1,
    # pula Wolnych Ludzi 0..0.05 ludzi krainy (trzy miejsca - 0.018), udzial zalog 0..1 (ClanBudget.FfPool)
    'ReaverIronPriceShare': (0.0, 1.0, "0.00"),
    'ReaverWaterfrontShare': (0.0, 1.0, "0.00"),
    'ReaverRaidPayDays': (1.0, 28.0, "0"),
    'SeaRaidersHarbourTithe': (0.0, 1.0, "0.00"),
    'FreeFolkLevyShare': (0.0, 0.05, "0.000"),
    'FreeFolkGarrisonPoolShare': (0.0, 1.0, "0.00"),
    # PROJEKT-LENNA (14.10): punkty zdobywcy i zapas pulapu ponad przydzial - kod przycina do 0..200 i 0..10 (FiefGrants.ConquerorBonus / CapExtra)
    'FairFiefConquerorBonus': (0, 200, "0"),
    'FairFiefCapExtra': (0, 10, "0"),
    # nskarbiec (noc 15/16.10): udzial kiesy odnoszony codziennie do kryjowki - kod przycina do 0..1 (OutlawLaw.StashDay)
    'HideoutDailyStashShare': (0.0, 1.0, "0.00"),
    # ceny (noc 15/16.10): kusza wedlug luku x1.0-3.0 (Jeff 1.5-2; kod: najmniej 1)
    'XbowPriceVsBow': (1.0, 3.0, "0.00"),
    # ceny (noc 15/16.10): udzial zamiennika 0..1 (kod przycina), prog drogiego jedzenia x1-10 (kod: najmniej 1)
    'FoodSubstituteShare': (0.0, 1.0, "0.00"),
    'AiFoodDearIndex': (1.0, 10.0, "0.0"),
    # ceny (noc 15/16.10): dni zoldu w cenie werbunku towarzysza (kod: najmniej 0)
    'CompanionHireDays': (0.0, 60.0, "0"),
    # N2 (noc 16.10): waga bryly wegla w kg, 0 = waga gry (5 kg); kod przycina do 50 (ArmouryBehavior.FixCharcoalWeight)
    'CharcoalWeightKg': (0.0, 10.0, "0.00"),
    # noc-ekonomia3 (16.10): kap poziomu normalnego straznika i bezpiecznik udzialu ludu - kod przycina do 0..0.5 / 0..1 (TownFolk.Guard.cs); myto targowe - kod 0..0.2 (TownToll.cs)
    'FolkGuardNormalShareMax': (0.0, 0.2, "0.000"),
    'FolkGuardShareFuse': (0.0, 0.3, "0.000"),
    'TownMarketTollRate': (0.0, 0.2, "0.000"),
    'TownMarketTollCaravanRate': (0.0, 0.2, "0.000"),
    # noc-ekonomia3 czesc 2 (16.10): czesc puli rent na dwor krolewski - kod przycina do 0..0.5 (CourtGoods.Crown)
    'CrownCourtShare': (0.0, 0.5, "0.00"),
    # NB2 (16.10): mnozniki postury szeregowych wedlug tieru - kod przycina do 0.5..1.5 (T1) i 1.0..3.0 (T6) (BattleWind.SoldierMul)
    'SoldierPostureTier1Multiplier': (0.5, 1.5, "0.00"),
    'SoldierPostureTier6Multiplier': (1.0, 3.0, "0.00"),
    # ZC0 (16.10): skala targu sigma - kod przycina do 28..73 (DailyLife.SigmaFromMcm; widelki projektu rozdz. 4)
    'DailyLifeMarketScale': (28, 73, "0"),
    # KS-R4 r4-kupcy (noc 17/18.10): kredyt kupcow KS-H - udzialy przycinane w kodzie do 0..1, zwloka 1..364 dob (KsFoodCarts.ConsignFloor / SrcExposure / OverdueDays)
    'KsConsignFloorShare': (0.0, 1.0, "0.00"),
    'KsConsignSourceExposure': (0.0, 1.0, "0.00"),
    'KsConsignOverdueDays': (1, 364, "0"),
    # KS-R5 ceny-r5 (noc 17/18.10): zadatek kupcow celu - udzial przycinany w kodzie do 0..1 (KsFoodCarts.DepositShare)
    'KsConsignDepositShare': (0.0, 1.0, "0.00"),
    # KS (16.10): premia mapy - kod przycina do 1.0..2.0 (StartCalibration.BuildInput)
    'StartCalibrationMapInertia': (1.0, 2.0, "0.00"),
    # ks-arm (noc 17/18.10): prog zapasu bramki krainy - kod przycina do 0..728 (StartCalibration.ReserveDaysKey)
    'KsLandGateReserveDays': (0, 728, "0"),
    # recenzja ks-arm: budzet straty spichlerzy krainy pod KS - kod przycina do 0..364 (StartCalibration.LossDaysKey)
    'KsLandGateLossDays': (0, 364, "0"),
    # DS F1 (17.10): daniny wsi - zgodnie z przycieciem w kodzie (DsVillage: udzial korony 0..0.5, doby 0..3640 / 0..364, udzial BK 0..1)
    'DsWarShareVillage': (0.0, 0.5, "0.00"),
    'DsRentForgiveDays': (0, 728, "0"),
    'DsVillageChestDays': (0, 90, "0"),
    'DsSurplusSpendDays': (0, 120, "0"),
    'DsSurplusBkShare': (0.0, 1.0, "0.00"),
    'DsRampDays': (0, 112, "0"),
    'DsRentArrearsPayDays': (0, 364, "0"),   # recenzja 17.10: rata zaleglosci czynszu 1/N dziennie (DsVillage.ArrPayDays przycina 0..364)
    # DS F3 (17.10): miasta, zamki, korona, kredyt - zgodnie z przycieciem w kodzie (DsTowns / DsSeason)
    'DsFarmMeasureDays': (7, 91, "0"),
    'DsFarmRenewDays': (0, 728, "0"),
    'DsFarmRenewMax': (0.0, 0.5, "0.00"),
    'DsFarmDistraintTerms': (1, 8, "0"),
    'DsFarmForgiveWeeks': (0, 156, "0"),
    'DsFarmFloorShare': (0.0, 1.0, "0.00"),
    'DsMerchantDrawDays': (0, 120, "0"),
    'DsMerchantDrawPShare': (0.0, 1.0, "0.00"),
    'DsAidesRate': (0.0, 0.1, "0.000"),
    'DsAidesMax': (0.0, 0.3, "0.00"),
    'DsTallageRate': (0.0, 0.1, "0.000"),
    'DsTallageMax': (0.0, 0.3, "0.00"),
    'DsTallageMaxPolicy': (0.0, 0.3, "0.00"),
    'DsMovablesRate': (0.0, 0.3, "0.00"),
    'DsCustomsWarMultiplier': (1.0, 6.0, "0.0"),
    'DsVassalAidWarPercent': (0.0, 33.0, "0.0"),
    'DsGrantMax': (0.5, 4.0, "0.0"),
    'DsGrantStep': (0.1, 1.0, "0.0"),
    'DsGrantRentsKeep': (0.0, 1.0, "0.00"),
    'DsGrantSessionDays': (1, 91, "0"),
    'DsCrownLoanLimitDaysPledged': (0.0, 728.0, "0"),
    'DsFolkSurplusSpendDays': (0, 120, "0"),
    # f4-kasy-miast-i-kupcy-z (18.10): przeglad firm co kwartal i wczesna splata rachunkow zywnosci - zgodnie z przycieciem w kodzie (DsTowns.Review 0..364, 0..8;
    # KsLordFood.EarlyShare 0..1)
    'DsFarmReviewDays': (0, 364, "0"),
    'DsFarmArrearsReviewTerms': (0, 8, "0"),
    'KsLordFoodBillsEarlyShare': (0.0, 1.0, "0.00"),
    # r4-bkwies (KS runda 4, 17.10): przycinane w kodzie (VillNeeds.RichChests 1..20, CoverDays 1..91; spichlerz awaryjny 0..364)
    'KsBkRichChests': (1.0, 20.0, "0.0"),
    'VillageNeedCoverDays': (1, 91, "0"),
    'KsGranaryEmergencyDays': (0, 364, "0"),
    # r4-koszyk (17.10 noc): place za brak rak - zgodnie z przycieciem w kodzie (KsWageHands: krok 0..10%, sufit 1..2)
    'KsWageHandsStepPercent': (0.0, 10.0, "0.0"),
    'KsWageHandsMax': (1.0, 2.0, "0.00"),
    # f4-place-ludu (noc 17/18.10): place W wedlug koszyka - zgodnie z przycieciem w kodzie (KsWageHands.Floor 0.3..1; KsFolkWage: dni 1..240 / 480, udzial 0..1,
    # krok 0.1..50%/d; stawka danin 0..0.1)
    'KsWageHandsFloor': (0.3, 1.0, "0.00"),
    'KsLabourBasketLowDays': (1.0, 120.0, "0"),
    'KsLabourBasketHighDays': (2.0, 240.0, "0"),
    'KsLabourBasketMin': (0.0, 1.0, "0.00"),
    'KsLabourBasketStepPercent': (0.5, 20.0, "0.0"),
    'KsLabourBasketHoardRate': (0.0, 0.1, "0.000"),
    # f5-lud (noc 17/18.10): dochod R/P wedlug koszyka i przeglad firmy wedlug nadwyzki kasy - zgodnie z przycieciem w kodzie (KsFolkIncome: dni 1..240 / 480,
    # udzial 0..1, krok 0.5..20%/d; DsTowns: doby 7..91, podwyzka 0..1)
    'KsFolkIncomeRLowDays': (1.0, 120.0, "0"),
    'KsFolkIncomeRHighDays': (2.0, 240.0, "0"),
    'KsFolkIncomePLowDays': (1.0, 120.0, "0"),
    'KsFolkIncomePHighDays': (2.0, 240.0, "0"),
    'KsFolkIncomeMin': (0.0, 1.0, "0.00"),
    'KsFolkIncomeStepPercent': (0.5, 20.0, "0.0"),
    'KsFarmSurplusReviewDays': (7, 91, "0"),
    'KsFarmSurplusReviewMax': (0.0, 1.0, "0.00"),
    # m96-koszyk (20.10): dni pelnego koszyka R/P z placy W zatrzymanej - kod przycina do 2..20 (TownFolk.M96Days)
    'KsFolkShortBasketDays': (2.0, 20.0, "0"),
    # f7-kasy (noc 17/18.10): gersuma z zapasu kas miast - zgodnie z przycieciem w kodzie (DsTowns: udzial 0.05..1, dni firmy 0..182)
    'KsFarmCatchUpShare': (0.05, 1.0, "0.00"),
    'KsFarmCatchUpKeepDays': (0, 182, "0"),
    # r4-mur (17.10): danina zbozowa Muru i najazdy Wolnych Ludzi - zgodnie z przycieciem w kodzie (WallTribute: udzial 0..100%, psucie 0..50%/d; FreeFolkRaids: szansa 0..1, band 0..64)
    'WallTributeTargetDays': (0, 728, "0"),
    'WallTributeRefillDays': (1, 364, "0"),
    'WallTributeGiverReserveDays': (0, 728, "0"),
    'WallTributeGiverSharePercent': (0.0, 10.0, "0.00"),
    'WallTributeSpoilPercentPerDay': (0.0, 2.0, "0.00"),
    'FreeFolkRaidMaxParties': (0, 20, "0"),
    'FreeFolkRaidMaxMen': (1, 120, "0"),
    'FreeFolkRaidMinMen': (1, 120, "0"),
    'FreeFolkRaidDays': (1, 120, "0"),
    'FreeFolkRaidDailyChance': (0.0, 1.0, "0.00"),
    # ds-akumulator (18.10): stawki przycinane w kodzie (DsHoardDueRate 0..0.1, DsWorksWagePerMaterial 0..3)
    'DsHoardDueRate': (0.0, 0.1, "0.000"),
    'DsWorksWagePerMaterial': (0.0, 3.0, "0.00"),
    # sw-uchodzcy (noc 17/18.10): uchodzcy z najazdu do miast - zgodnie z przycieciem w kodzie (RaidRefugees: promien >= 0, udzial kiesy 0..1, komunikat >= 0)
    'RaidRefugeesTownRadius': (0.0, 1000.0, "0"),
    'RaidRefugeesPurseShare': (0.0, 1.0, "0.00"),
    'RaidRefugeesMessageMin': (0, 10000, "0"),
    'RaidRefugeesTownIntake': (0.0, 0.2, "0.000"),   # recenzja calosci: dobowy limit przyjec nowych kont (udzial ludzi miasta; 0 = bez limitu)
    'RaidRefugeesIntakeFirstMult': (1.0, 10.0, "0.0"),   # f4-uchodzcy: pierwsze konto doby w calosci do M x dobowy limit (przyciete w kodzie do >= 1)
    # inwest (noc 17/18.10): zajecia wiosek i prace okregu - zgodnie z przycieciem w kodzie (VillageCrafts.Rule: prog >= 1, doby >= 7, udzial regionu 0..1;
    # DistrictIcon 0..1; DistrictWorks: udzial 0..1, dodatek 0..2, lata sprzetu 0.1..10, doby >= 1 / >= 0; DistrictWorksAi: zwrot 0.01..2, udzial 0..1, tempo 0..0.1)
    'SideVillageCraftSwitch': (1.0, 3.0, "0.00"),
    'SideVillageCraftHoldDays': (7, 364, "0"),
    'SideVillageCraftChangeDays': (7, 364, "0"),
    'SideVillageCraftTreeDays': (7, 728, "0"),
    'SideVillageCraftRegionShare': (0.0, 1.0, "0.000"),
    'DistrictIconMargin': (0.0, 1.0, "0.00"),
    'DistrictWorksShare': (0.0, 1.0, "0.00"),
    'DistrictWorksBonusSide': (0.0, 2.0, "0.00"),
    'DistrictWorksBonusMain': (0.0, 2.0, "0.00"),
    'DistrictWorksKitYears': (0.1, 10.0, "0.0"),
    'DistrictWorksMinGold': (0, 10000, "0"),
    'DistrictWorksRebuildDays': (1, 728, "0"),
    'DistrictWorksKeepWageDays': (0, 364, "0"),
    'DistrictWorksAiMinReturn': (0.01, 2.0, "0.00"),
    'DistrictWorksAiSurplusShare': (0.0, 1.0, "0.00"),
    'DistrictWorksAiReserveDays': (0, 364, "0"),
    'DistrictWorksAiReserveFlat': (0, 200000, "0"),
    'DistrictWorksAiWorldPace': (0.0, 0.1, "0.000"),
    # R3-09f (noc 20/21.10): regale gornicze - kod przycina stawke do 0..0.5 (MineRegale.Share); 0.125 = 1/8 wymaga 3 miejsc
    'MineRegaleShare': (0.0, 0.5, "0.000"),
    # f5-a7 (noc 17/18.10): pasmo odchylenia rynku w sygnale A7 - kod przycina do 0..0.5 (StartCalibration.A7sBand)
    'KsSwitchMarketBand': (0.0, 0.5, "0.00"),
    # f8-bandy (noc 18.10, decyzja Jeffa "7. b"): limit band na zajeta kryjowke - kod przycina do 48, a <= 12 (ROT) to cel gry bez zmian (BanditLimit.RaisedTarget)
    'BanditLimitPerHideout': (12, 48, "0"),
    # f9-predkosc (18.10): biegi przyciskow mapy - kod przycina do 2..16 i 2..32 (MapSpeed.FastOf / VeryFastOf)
    'MapSpeedFast': (2, 16, "0"),
    'MapSpeedVeryFast': (2, 32, "0"),
}

# f18-mcm (Jeff 18.10: "jak wlaczylem drogi gra wyszla do pulpitu"): MCM v5 ma RequireRestart = true DOMYSLNIE (BaseSettingPropertyAttribute), a ModOptionsVM
# przy zmianie takiego pola pokazuje "Game Needs to Restart" i na "Yes" wola Utilities.QuitGame() (Cancel = zmiany nie zapisane). Generator nie ustawial
# RequireRestart, wiec KAZDA zmiana w MCM wylaczala gre. Teraz kazde pole dostaje RequireRestart = false, poza krotka lista ponizej.
# Jak wartosc wchodzi do gry (zdekompilowane MCM: suwak zmienia wlasciwosc obiektu McmSettings.Instance od razu, Done zapisuje plik bez pytania o restart):
#  - Armoury: McmSettings.Apply przepisuje MCM do Settings.Current w OnGameStart, w OnSessionLaunched i CO GODZINE GRY (ArmouryBehavior, sluchacz HourlyTick) -
#    odczyty w trakcie gry widza zmiane najpozniej po godzinie gry;
#  - RealisticCaptivity i GrandTourney: McmSettings.Apply TYLKO w OnGameStart - kazda zmiana wchodzi po wczytaniu zapisu (patrz APPLY_ON_LOAD_ONLY);
#  - odczyty przy starcie procesu (OnSubModuleLoad / OnBeforeInitialModuleScreenSetAsRoot) widza Settings.Current z Armoury.settings.xml, NIE z MCM -
#    restart gry nie wnosi wartosci z MCM, wiec takie pola restartu nie wymagaja (i tak go nie uzywaja); wyjatek: pole czytane wprost z pliku MCM przy starcie.
# Audyt (f18-mcm, analiza IL wszystkich pieciu DLL: odczyty pol Settings i McmSettings, graf wywolan, korzenie PROC/SESS/RUN) - szczegoly w
# docs/noc-1718/f18-mcm-CHANGELOG.md.

# pola, ktore naprawde wymagaja restartu gry (zostaja RequireRestart = true): modul -> {klucz: dlaczego}
RESTART_KEYS = {
    'Armoury': {
        # MapSpeedUi.Register (OnSubModuleLoad) czyta "MapSpeedButtons" WPROST z pliku Armoury.json MCM i od tego zalezy rejestracja UIExtenderEx
        # (wstawka MapBar + mixin MapTimeControlVM) - raz na proces; przyciski dochodza/znikaja z paska dopiero po ponownym uruchomieniu gry
        # (chowanie przyciskow i klawisze 4/5/6 dzialaja od razu - opis w Settings.cs juz to mowi)
        'MapSpeedButtons': 'rejestracja UIExtenderEx przy starcie procesu z pliku MCM',
    },
}

# pola czytane TYLKO przy starcie sesji kampanii (OnGameStart / OnSessionLaunched / OnNewGameCreated / RegisterEvents i to, co z nich wolane) - wystarczy
# wczytac zapis: RequireRestart = false i dopisek " (takes effect after loading a save)" w opisie, o ile opis juz o tym nie mowi (ALREADY_SAID)
SESSION_KEYS = {
    'Armoury': {
        'LootPriceFollowsCondition',    # LootPrices.Apply (OnSessionLaunched)
        'RichQualityModifiers',         # QualityRich.Enrich (OnSessionLaunched)
        'TroopSkillAutoFit',            # TroopFit.Run (LegendaryLaw, start sesji) - opis: "audited on load"
        # MaterialLaw.Apply (OnSessionLaunched, zaraz po McmSettings.Apply) - wartosci sztab
        'CharcoalValue', 'CrudeIronValue', 'WroughtIronValue', 'IronValue', 'SteelValue', 'FineSteelValue', 'CastleSteelValue',
        'StartStockInLoads',            # StartStock.Run - nowa kampania (opis juz mowi)
        'ColdStartMarketDays', 'ColdStartTierUp', 'ColdStartLegacyHands',   # ColdStart.Markets - nowa kampania (opisy juz mowia)
        # HistoricalPrices.Apply / XbowPrice.Build / BulkUnit (OnSessionLaunched) - cennik raz na sesje
        'HistCharcoalPerKg', 'HistBulkUnitFactor', 'HistTradeGoods', 'HistLivestockPrices', 'HistHidesPerKg', 'HistFlaxPerKg', 'HistHorsePrices',
        'XbowPriceVsBow', 'HistDemandFromDefinition', 'HistMixedCategoryShelf', 'BulkUnitPerCampaign',
        'BkWorkshopInputsFix',          # WorkshopInputsFix.Apply (OnSessionLaunched)
        'VillageClimateFix',            # VillageClimate.Apply / GoodsRates.Apply (OnSessionLaunched)
        'FlaxVillagesWithoutClimateFix',  # GoodsRates.Apply
        'VineOliveByClimate',           # VineOlive (nowa kampania - opis juz mowi)
        'BloomeryAtMine',               # Bloomery.CanApply (nowa kampania - opis juz mowi)
        'WorkshopRecipesByValue',       # WorkshopRecipes.Apply
        'SlaughterKeepsHides',          # SlaughterHides.Apply
        'MidDayLoadKeepsDay',           # DayHeld.SessionStart
        'CombatAiRealistic',            # CombatAiLevel.Apply (OnSessionLaunched)
        'CharcoalWeightKg',             # FixCharcoalWeight (OnSessionLaunched)
        # CrashScribe (Army175Gear przy wczytaniu, klucze przez Mends.ArmouryFloat) - opisy juz mowia "takes effect on the next load"
        'Army175IronbornFoot', 'Army175NorthFoot', 'Army175VolantisNorvos', 'Army175MinorLore',
        # f18-mcm, poprawka po recenzji:
        'DesertionLawEnabled',          # TierDesertionModel.Install (OnGameStart, po McmSettings.Apply) dodaje model tylko przy kluczu; model klucza juz nie czyta
        'BkProsperityLayerOnce',        # LayerOnce.Sync tylko z A16Signals.OnSessionLaunched (Unpatch / Patch postfiksu BK raz na sesje)
        'FoodModelChain',               # FoodChain.Build - stan raz na kampanie (_st wedlug Campaign.Current); opis mowi "after loading a save" (dotad: "Restart the game")
    },
}

# f18-mcm, poprawka po recenzji: ceny historyczne - HistoricalPrices.Apply (OnSessionLaunched) ustawia ItemObject.Value kazdej broni i zbroi raz na sesje
# (z HistCost), a blokada cen (_target, ValueSetPrefix / Recheck) trzyma te wartosc do wczytania. Te klucze czytaja jednak takze miejsca NA ZYWO
# (dniowka mistrza WageFor -> TownWage / Qohor / zlecenia, dni roboty HistDays -> ForgeClock / WorkshopLaw, robota naprawy MakingLabor, cena surowca
# warsztatu WorkshopLaw.MatPrice, wycena nowo wykutych PriceOne / PriceDesign) - stad osobny dopisek: przeliczenie cen przedmiotow juz istniejacych
# po wczytaniu, reszta po godzinie gry (Apply co godzine). Klucze czytane wylacznie w Apply (HistCharcoalPerKg, HistHidesPerKg, HistFlaxPerKg) sa w SESSION_KEYS.
PRICE_KEYS = {
    'Armoury': {
        # MetalPerKg -> HistMaterial (sztaby wedlug gatunku)
        'HistCrudeIronPerKg', 'HistWroughtIronPerKg', 'HistIronPerKg', 'HistSteelPerKg', 'HistFineSteelPerKg', 'HistCastleSteelPerKg',
        # HistMaterial / FuelPerMetalKg / Apply (surowce) i WorkshopLaw.MatPrice
        'HistIronOrePerKg', 'HistWoodPerKg', 'HistLeatherPerKg', 'HistLinenPerKg', 'HistSpecialFactor',
        # WageFor / HistCostOf / HistDays
        'HistMasterWageT1', 'HistMasterWagePerTier', 'HistProfitPercent', 'HistAmmoLaborMultiplier', 'HistBowLaborMultiplier', 'HistThrownOwnDays',
        # Apply / PriceOne / PriceDesign - unikaty imienne
        'HistUniquePrestige',
    },
}
PRICE_SUFFIX = " (prices of items already in the world are recalculated after loading a save)"

# f18-mcm, poprawka po recenzji: klucze czytane TYLKO przy starcie procesu (ApplyAll w OnBeforeInitialModuleScreenSetAsRoot, z Armoury.settings.xml albo
# domyslnych z kodu), bez odczytu na zywo w latkach - przelacznik MCM nie zmienia nic, takze po restarcie (start procesu nie czyta MCM); RequireRestart = true
# nic by nie dal. Uczciwy dopisek w opisie zamiast obietnicy restartu.
STARTUP_KEYS = {
    'Armoury': {
        # BattleWind.ApplyAll: latki staminy i tickow RBMAI.Stance oraz BattleWind.Active tylko przy kluczu; Active czyta FieldCraft (stary liniowy bonus
        # Endurance przy !Active) - odczyt na zywo wymagalby zmian w BattleWind i FieldCraft naraz (podwojny bonus), wiec bez zmiany kodu
        'BattleStaminaEnabled',
    },
}
STARTUP_SUFFIX = " (read once at game start from ModuleData/Armoury.settings.xml - changing it here has no effect)"

# moduly, w ktorych McmSettings.Apply idzie TYLKO w SubModuleMain.OnGameStart (bez sluchacza godziny jak w Armoury) - kazde ich pole wchodzi
# po wczytaniu zapisu; dopisek do kazdego opisu (do zdjecia, gdy modul dostanie Apply co godzine)
APPLY_ON_LOAD_ONLY = {'RealisticCaptivity', 'GrandTourney'}

LOAD_SUFFIX = " (takes effect after loading a save)"
# opis juz mowi, kiedy zmiana wchodzi (wczytanie, start sesji, nowa kampania, restart) - bez dopisku
ALREADY_SAID = re.compile(r"next load|on load\b|after load|loading a save|is (next )?loaded|when a (game|save|campaign) (is )?(next )?(loaded|loads|starts)"
                          r"|starts or loads|at session start|new campaigns? only|of a new campaign|with a new campaign|restart", re.I)

def gen(module_dir, ns, display):
    src = open(os.path.join(module_dir,'src','Settings.cs'), encoding='utf-8').read()
    group = "General"
    props = []
    for line in src.splitlines():
        g = re.match(r'\s*//\s*---\s*(.+?)\s*---', line)
        if g:
            # 175c po recenzji: tylko pierwsza litera wielka - capitalize() psul nazwy wlasne ("The others and valyrian steel", "Iron bank")
            g1 = g.group(1).strip()
            group = g1[:1].upper() + g1[1:]
            continue
        m = re.match(r'\s*public\s+(bool|int|float)\s+(\w+)\s*=\s*([^;]+);\s*(?://\s*(.*))?', line)
        if not m: continue
        typ, name, default, hint = m.group(1), m.group(2), m.group(3).strip(), (m.group(4) or "").strip()
        if name in ("Current",): continue
        props.append((typ, name, default, hint, group))

    # f18-mcm: literowka w tabelach = blad generatora, nie cicha zmiana zachowania
    names = set(p[1] for p in props)
    restart = RESTART_KEYS.get(ns, {})
    session = SESSION_KEYS.get(ns, set())
    price = PRICE_KEYS.get(ns, set())
    startup = STARTUP_KEYS.get(ns, set())
    bad = sorted((set(restart) | session | price | startup) - names)
    if bad:
        sys.exit("gen_mcm: %s - klucze z RESTART_KEYS/SESSION_KEYS/PRICE_KEYS/STARTUP_KEYS nieobecne w Settings.cs: %s" % (ns, ", ".join(bad)))
    # klucz w dwoch tabelach = sprzeczny opis
    tables = [set(restart), session, price, startup]
    dup = sorted(k for i, a in enumerate(tables) for b in tables[i + 1:] for k in a & b)
    if dup:
        sys.exit("gen_mcm: %s - klucze w wiecej niz jednej tabeli (RESTART/SESSION/PRICE/STARTUP): %s" % (ns, ", ".join(dup)))
    n_restart = n_suffix = n_said = n_price = n_startup = 0

    out = []
    out.append("using MCM.Abstractions.Attributes;")
    out.append("using MCM.Abstractions.Attributes.v2;")
    out.append("using MCM.Abstractions.Base.Global;")
    out.append("")
    out.append("namespace %s" % ns)
    out.append("{")
    out.append("    /// <summary>Ustawienia w MCM. Plik XML dziala dalej jako wartosci startowe.</summary>")
    out.append("    public class McmSettings : AttributeGlobalSettings<McmSettings>")
    out.append("    {")
    out.append('        public override string Id => "%s";' % ns)
    out.append('        public override string DisplayName => "%s";' % display)
    out.append('        public override string FolderName => "%s";' % ns)
    out.append('        public override string FormatType => "json2";')
    out.append("")
    for typ, name, default, hint, grp in props:
        label = re.sub(r'(?<!^)(?=[A-Z])', ' ', name)
        hint_txt = hint.replace('"', "'")
        # f18-mcm: restart tylko dla RESTART_KEYS; pola wchodzace po wczytaniu zapisu - dopisek, jesli opis jeszcze o tym nie mowi
        rr = "true" if name in restart else "false"
        if name in restart:
            n_restart += 1
        elif name in startup:
            # f18-mcm (recenzja): odczyt tylko przy starcie procesu - dopisek zawsze (zaden opis tego nie mowi)
            hint_txt = (hint_txt + STARTUP_SUFFIX).strip()
            n_startup += 1
        elif name in price and ns not in APPLY_ON_LOAD_ONLY:
            # f18-mcm (recenzja): ceny przedmiotow juz istniejacych po wczytaniu, reszta na zywo
            if ALREADY_SAID.search(hint_txt):
                n_said += 1
            else:
                hint_txt = (hint_txt + PRICE_SUFFIX).strip()
                n_price += 1
        elif ns in APPLY_ON_LOAD_ONLY or name in session:
            if ALREADY_SAID.search(hint_txt):
                n_said += 1
            else:
                hint_txt = (hint_txt + LOAD_SUFFIX).strip()
                n_suffix += 1
        tail = 'HintText = "%s", RequireRestart = %s' % (hint_txt, rr)
        if typ != "bool" and name in RANGES:
            lo, hi, fmt = RANGES[name]
            if typ == "int":
                attr = '        [SettingPropertyInteger("%s", %d, %d, "%s", %s)]' % (label, lo, hi, fmt, tail)
            else:
                dec = len(fmt.split('.')[1]) if '.' in fmt else 2
                attr = '        [SettingPropertyFloatingInteger("%s", %.*ff, %.*ff, "%s", %s)]' % (label, max(2, dec), lo, max(2, dec), hi, fmt, tail)
        elif typ == "bool":
            attr = '        [SettingPropertyBool("%s", %s)]' % (label, tail)
        elif typ == "int":
            d = int(float(default))
            lo = min(0, d*3) if d < 0 else 0
            hi = max(10, abs(d)*4) if d >= 0 else 0
            attr = '        [SettingPropertyInteger("%s", %d, %d, "0", %s)]' % (label, lo, hi, tail)
        else:
            d = float(default.rstrip('f'))
            lo = 0.0
            hi = max(1.0, d*4) if d > 0 else 1.0
            attr = '        [SettingPropertyFloatingInteger("%s", %.2ff, %.2ff, "0.00", %s)]' % (label, lo, hi, tail)
        out.append(attr)
        out.append('        [SettingPropertyGroup("%s")]' % grp)
        out.append('        public %s %s { get; set; } = %s;' % (typ, name, default))
        out.append("")
    out.append("        public void ApplyTo(Settings s)")
    out.append("        {")
    for typ, name, default, hint, grp in props:
        out.append("            s.%s = %s;" % (name, name))
    out.append("        }")
    out.append("")
    out.append("        internal static void Apply()")
    out.append("        {")
    out.append("            try { var i = Instance; if (i != null) i.ApplyTo(Settings.Current); }")
    out.append("            catch (System.Exception e) { Log.Error(\"Mcm.Apply\", e); }")
    out.append("        }")
    out.append("    }")
    out.append("}")
    path = os.path.join(module_dir,'src','McmSettings.cs')
    open(path,'w',encoding='utf-8').write("\n".join(out))
    print("%s: %d ustawien -> %s (restart: %d, dopisek 'after loading a save': %d, dopisek cen: %d, dopisek 'read once at game start': %d, opis juz mowi: %d)"
          % (ns, len(props), path, n_restart, n_suffix, n_price, n_startup, n_said))

# sciezki wzgledem korzenia repo - skrypt wolac z katalogu glownego
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
gen(os.path.join(root, 'RealisticCaptivity'), 'RealisticCaptivity', 'Realistic Captivity')
gen(os.path.join(root, 'GrandTourney'), 'GrandTourney', 'Grand Tourney')
gen(os.path.join(root, 'Armoury'), 'Armoury', 'The Armoury')
