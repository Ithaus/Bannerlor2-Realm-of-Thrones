import re, sys, os

def gen(module_dir, ns, display):
    src = open(os.path.join(module_dir,'src','Settings.cs'), encoding='utf-8').read()
    group = "General"
    props = []
    for line in src.splitlines():
        g = re.match(r'\s*//\s*---\s*(.+?)\s*---', line)
        if g:
            group = g.group(1).strip().capitalize()
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
        if typ == "bool":
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

gen('/home/claude/rc', 'RealisticCaptivity', 'Realistic Captivity')
gen('/home/claude/gt', 'GrandTourney', 'Grand Tourney')
gen('/home/claude/ar', 'Armoury', 'The Armoury')
