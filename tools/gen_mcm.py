import re, sys, os

# zakresy suwakow zgodne z przycieciem w kodzie: nazwa -> (min, max, format); reszta - wzor ogolny ponizej
# T1: godziny obozu (regula ogolna [0, max(10, 4 x domyslna)] dawala przy domyslnej 0 suwak 0..10 - nie dalo sie ustawic 22)
# recenzja 174: suwaki 174 zgodne z przycieciem w kodzie; K1: odsetki 0..100 (regula ogolna dawala 0..200 i 0..300)
RANGES = {
    'CampStartHour': (0, 23, "0"),
    'CampEndHour': (0, 23, "0"),
    'OldStockScrapYield': (0.0, 1.0, "0.00"),
    'OldStockScrapDailyShare': (0.0, 0.1, "0.000"),
    'WorkshopMunitionShare': (0.0, 1.0, "0.00"),
    'WorkshopLineShortageShare': (0.0, 1.0, "0.00"),
    'WorkshopPlanDays': (14.0, 120.0, "0"),
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
    # T10 poprawka recenzji: prog dlugu snu w calych nocach (kod porownuje dlug calkowity) - suwak bez ulamkow w opisie
    'AiNightsAwakeInChase': (0.0, 4.0, "0"),
    # T10-R: noce forsownego marszu z rzedu (dlug konczy sie na 3, dalej liczy juz tylko seria) - 0 = bez limitu
    'MaxForcedNights': (0, 5, "0"),
    # paczka 175: suwaki z projektu (rozdz. 5)
    'NorthHardyWeaponBonus': (0, 50, "0"),
    'NorthHardyAthleticsBonus': (0, 50, "0"),
    'NorthHomeEdgePercent': (0, 25, "0"),
    'DothrakiRidingBonus': (0, 50, "0"),
    # 175c: zasada stali Innych
    'OthersCastleSteelPercent': (15, 100, "0"),
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
    'RansomHeadYears': (0.0, 3.0, "0.00"),
    # 186: korona pozycza w Banku - udzial przycinany w kodzie do 0..1, mnoznik >= 1, doby >= 1
    'CrownLoanMaxIncomeShare': (0.0, 1.0, "0.00"),
    'CrownArrearsEnemyCredit': (1.0, 3.0, "0.00"),
    'CrownLoanLimitDays': (0.0, 728.0, "0"),
    'CrownLoanRepayDays': (30.0, 728.0, "0"),
    'CrownLoanPoolDays': (1.0, 360.0, "0"),
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
    # E5/B4 (199): targ przy zamku - dni jedzenia podzamcza na polce zamku (kod przycina do 1..30, CastleMarket.Days)
    'CastleMarketFoodDays': (1.0, 30.0, "0"),
    # E6/K2 (212): luz zapasu koni jako udzial jezdzcow kategorii (kod przycina do 0..1, Stables.Reserve)
    'AiSpareHorseShare': (0.0, 1.0, "0.00"),
    # E6/K5 (213): udzwig luzaka pod jukami w kg (20 = jak w grze - dopisek 0; PackMarket.CapPostfix)
    'SpareHorseCarryKg': (20, 100, "0"),
    # E6/W3 (221): regulator wsi - sufit kroku x1.3 (kod przycina do 1..2), progi stosu w dniach zuzycia (kod: niski >= 1, wysoki > niski)
    'VillageOutputStep': (1.0, 2.0, "0.00"),
    'ValveStockDaysLow': (7, 120, "0"),
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
}

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
        if typ != "bool" and name in RANGES:
            lo, hi, fmt = RANGES[name]
            if typ == "int":
                attr = '        [SettingPropertyInteger("%s", %d, %d, "%s", HintText = "%s")]' % (label, lo, hi, fmt, hint_txt)
            else:
                dec = len(fmt.split('.')[1]) if '.' in fmt else 2
                attr = '        [SettingPropertyFloatingInteger("%s", %.*ff, %.*ff, "%s", HintText = "%s")]' % (label, max(2, dec), lo, max(2, dec), hi, fmt, hint_txt)
        elif typ == "bool":
            attr = '        [SettingPropertyBool("%s", HintText = "%s")]' % (label, hint_txt)
        elif typ == "int":
            d = int(float(default))
            lo = min(0, d*3) if d < 0 else 0
            hi = max(10, abs(d)*4) if d >= 0 else 0
            attr = '        [SettingPropertyInteger("%s", %d, %d, "0", HintText = "%s")]' % (label, lo, hi, hint_txt)
        else:
            d = float(default.rstrip('f'))
            lo = 0.0
            hi = max(1.0, d*4) if d > 0 else 1.0
            attr = '        [SettingPropertyFloatingInteger("%s", %.2ff, %.2ff, "0.00", HintText = "%s")]' % (label, lo, hi, hint_txt)
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
    print("%s: %d ustawien -> %s" % (ns, len(props), path))

# sciezki wzgledem korzenia repo - skrypt wolac z katalogu glownego
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
gen(os.path.join(root, 'RealisticCaptivity'), 'RealisticCaptivity', 'Realistic Captivity')
gen(os.path.join(root, 'GrandTourney'), 'GrandTourney', 'Grand Tourney')
gen(os.path.join(root, 'Armoury'), 'Armoury', 'The Armoury')
