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
