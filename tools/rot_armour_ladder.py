# -*- coding: utf-8 -*-
"""423: lista korpusow ROT objetych drabina pancerzy (Armoury/src/ArmourLadder423.Ids.cs).

Zbior = <Item Type="BodyArmor"> z Modules/ROT-Content/ModuleData (ROTassets.xml, items.xml) o materiale Leather / Chainmail / Plate.
Tylko te sztuki dostaja przy wczytaniu mnoznik lacznika ROT-RBM (x2.2 / x2.1 / x2.4); korpusy RBM i gry sa juz w skali RBM.

  python tools/rot_armour_ladder.py            wypisuje liczby i przyklady mapowania (nic nie zapisuje)
  python tools/rot_armour_ladder.py --write    zapisuje Armoury/src/ArmourLadder423.Ids.cs
  python tools/rot_armour_ladder.py --check    porownuje plik .cs z XML; roznica = kod wyjscia 1
  [--modules "<katalog Modules gry>"]

Wezly tabeli sa w ArmourLadder423.cs (Knots*); kopia ponizej sluzy tylko do wydruku kontrolnego."""
import os, re, sys
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "Armoury", "src", "ArmourLadder423.Ids.cs")
MODULES = r"C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules"
FILES = ("ROT-Content/ModuleData/ROTassets.xml", "ROT-Content/ModuleData/items.xml")
MATS = ("Leather", "Chainmail", "Plate")
ROT_MULT = {"Leather": 2.2, "Chainmail": 2.1, "Plate": 2.4}   # lacznik ROT-RBM (rot_all.cs:17505-17510) - tylko do wydruku
KNOTS = {
    "Leather": [(0, 0), (19, 8), (33, 12), (44, 15), (55, 20), (61, 26), (66, 29), (70, 32), (74, 36), (77, 39), (79, 42), (81, 45), (83, 48),
                (85, 50), (88, 52), (92, 54), (99, 58), (132, 72), (176, 90)],
    "Chainmail": [(0, 0), (42, 30), (52, 36), (58, 40), (71, 46), (79, 50), (88, 55), (92, 58), (98, 62), (104, 66), (113, 72), (207, 130)],
    "Plate": [(0, 0), (72, 55), (96, 72), (100, 75), (105, 79), (110, 83), (115, 87), (120, 91), (127, 96), (132, 100), (136, 103), (160, 120)],
}


def target(mat, v):
    k = KNOTS[mat]
    if v <= 0:
        return float(v)
    for (x0, y0), (x1, y1) in zip(k, k[1:]):
        if v <= x1:
            return y0 + (y1 - y0) * (v - x0) / float(x1 - x0)
    (x0, y0), (x1, y1) = k[-2], k[-1]
    return y1 + (y1 - y0) / float(x1 - x0) * (v - x1)


def mapv(mat, v, s=1.0):
    return min(v, int(round(v + s * (target(mat, v) - v))))


def scan(modules):
    rows, seen = [], set()
    for rel in FILES:
        p = os.path.join(modules, rel)
        if not os.path.isfile(p):
            sys.exit("brak pliku: " + p)
        root = ET.fromstring(open(p, encoding="utf-8-sig", errors="replace").read().encode("utf-8"))
        for it in root:
            if it.tag != "Item" or it.attrib.get("Type") != "BodyArmor":
                continue
            a = it.find("./ItemComponent/Armor")
            if a is None:
                continue
            mat = a.attrib.get("material_type", "None")
            i = it.attrib.get("id")
            if mat not in MATS or not i or i in seen:
                continue
            seen.add(i)
            rows.append((i, mat, int(a.attrib.get("body_armor", 0))))
    return rows


def render(rows):
    ids = [r[0] for r in rows]
    n = dict((m, sum(1 for r in rows if r[1] == m)) for m in MATS)
    out = ["// PLIK GENEROWANY: python tools/rot_armour_ladder.py --write (kontrola: --check). Nie edytowac recznie.",
           "// Korpusy ROT-Content (ROTassets.xml, items.xml) typu BodyArmor o materiale Leather / Chainmail / Plate: %d sztuk" % len(ids),
           "// (Leather %d, Chainmail %d, Plate %d). Material i wartosci czytane sa w grze z ArmorComponent - tu tylko id." % (n["Leather"], n["Chainmail"], n["Plate"]),
           "namespace Armoury", "{", "    internal sealed partial class ArmourLadder423", "    {",
           "        internal const int IdsExpected = %d;" % len(ids), "",
           "        private static readonly string[] Ids =", "        {"]
    line = "           "
    for i in ids:
        tok = ' "%s",' % i
        if len(line) + len(tok) > 150:
            out.append(line)
            line = "           "
        line += tok
    if line.strip():
        out.append(line)
    out += ["        };", "    }", "}", ""]
    return "\n".join(out)


def main():
    args = sys.argv[1:]
    modules = MODULES
    if "--modules" in args:
        modules = args[args.index("--modules") + 1]
    rows = scan(modules)
    text = render(rows)
    if "--check" in args:
        cur = open(OUT, encoding="utf-8").read().replace("\r\n", "\n") if os.path.isfile(OUT) else ""
        have = re.findall(r'"([^"]+)"', cur.split("Ids =", 1)[1]) if "Ids =" in cur else []
        want = [r[0] for r in rows]
        if have != want:
            print("ROZNICA: w XML %d, w pliku %d; brak w pliku: %s; nadmiar w pliku: %s" % (
                len(want), len(have), sorted(set(want) - set(have))[:20], sorted(set(have) - set(want))[:20]))
            sys.exit(1)
        print("OK: %d id zgodnych z XML" % len(want))
        return
    for m in MATS:
        xs = sorted(int(round(r[2] * ROT_MULT[m])) for r in rows if r[1] == m)
        if xs:
            med = xs[len(xs) // 2]
            print("%-9s %3d sztuk, w grze ok. %d-%d, mediana %d -> %d" % (m, len(xs), xs[0], xs[-1], med, mapv(m, med)))
    for m in MATS:   # tabela tylko w dol i monotoniczna
        prev = -1
        for v in range(0, 260):
            y = mapv(m, v)
            assert y <= v and y >= prev, (m, v, y, prev)
            prev = y
    print("razem %d; tabela monotoniczna i tylko w dol dla 0..259" % len(rows))
    if "--write" in args:
        with open(OUT, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        print("zapisano " + os.path.normpath(OUT))


if __name__ == "__main__":
    main()
