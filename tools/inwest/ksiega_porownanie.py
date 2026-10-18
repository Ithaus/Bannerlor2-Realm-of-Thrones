"""Uzycie: python -I tools/inwest/ksiega_porownanie.py <Armoury-A.log> <Armoury-B.log> <doba od> <doba do>

f5-zajecia: produkcja wsi wedlug towaru z linii "Towary: dzien N" (ksiega towarow) - srednia dob w oknie, log A wobec B, z dopiskami "BK poza typem wsi".
Np. fin86 (bez zajec) wobec fin88 (zajecia + poprawka f4b), doby 109078-109105 (przed KS)."""
import re, sys, collections
pat = re.compile(r"Towary: dzien (\d+) - (\S+) \((\d+) d\): zapas [^|]*\| produkcja (\d+) \[wsie (\d+)(?: \(([^)]*)\))?")
def load(path, d0, d1):
    tot = collections.defaultdict(float); out = collections.defaultdict(float); val = {}
    days = set()
    with open(path, encoding='utf-8', errors='replace') as f:
        for line in f:
            if 'Towary: dzien' not in line: continue
            m = pat.search(line)
            if not m: continue
            d = int(m.group(1))
            if d < d0 or d > d1: continue
            days.add(d)
            it = m.group(2); val[it] = int(m.group(3)); w = int(m.group(5))
            tot[it] += w
            if m.group(6):
                mm = re.search(r"BK poza typem wsi (\d+)", m.group(6))
                if mm: out[it] += int(mm.group(1))
    n = max(1, len(days))
    return {k: v / n for k, v in tot.items()}, {k: v / n for k, v in out.items()}, val, len(days)
a = load(sys.argv[1], int(sys.argv[3]), int(sys.argv[4]))
b = load(sys.argv[2], int(sys.argv[3]), int(sys.argv[4]))
print("dni", a[3], b[3])
rows = []
for it in sorted(set(a[0]) | set(b[0])):
    x = a[0].get(it, 0); y = b[0].get(it, 0); v = a[2].get(it, b[2].get(it, 1))
    if x * v < 200 and y * v < 200: continue
    rows.append((it, x, y, (y - x) / x * 100 if x else 0, a[1].get(it, 0), b[1].get(it, 0), v))
for r in sorted(rows, key=lambda r: -abs(r[3])):
    print("%-14s A %8.1f  B %8.1f  %+6.1f%%   pozatyp A %6.1f B %6.1f  val %d" % r)
