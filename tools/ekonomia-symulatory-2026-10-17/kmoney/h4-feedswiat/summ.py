import re
import sys
import os

# podsumowanie biegow harnessu: niezbiezne na Plan, NA TARG poza 0.95-1.05 (bez wyjatkow projektu), wyzywienie krain zimnych (ostatni Plan)
EXC = {"carrot", "apple", "fur", "honey", "spice", "whale_oil", "walrus_tusk", "Egg", "hides"}
D = os.path.join(os.path.dirname(os.path.abspath(__file__)), "h", "out")
for f in sys.argv[1:]:
    p = os.path.join(D, f + ".out")
    if not os.path.isfile(p):
        print(f, "brak")
        continue
    t = open(p, encoding="utf-8").read().splitlines()
    nz = [int(m.group(1)) for l in t for m in [re.match(r"  NIEZB \d+: (\d+)", l)] if m]
    tm = [float(m.group(1)) for l in t for m in [re.match(r"PLAN \d+: ([\d.]+) s", l)] if m]
    bad = []
    for l in t:
        m = re.match(r"  ([WE]) NA TARG \(S-zj\)/\(D-zj\): (.*)", l)
        if m:
            for g, v in re.findall(r"([A-Za-z_]+) ([\d.]+)", m.group(2)):
                v = float(v)
                if g in EXC:
                    continue
                if v < 0.95 or v > 1.05:
                    bad.append(f"{m.group(1)} {g} {v:.2f}")
    fl = [l for l in t if "KS: wyzywienie krain" in l]
    cov = []
    if fl:
        l = fl[-1]
        for land in ("battania", "freefolk", "nightswatch", "skagosi", "ibbenese", "sturgia"):
            m = re.search(r"[WE] " + land + r" jedzenie ([\d.]+|-) wsie ([\d.]+) kraina ([\d.]+)", l)
            cov.append(f"{land[:5]} {m.group(1)}" if m else f"{land[:5]} -")
    med = sorted(nz[1:])[len(nz[1:]) // 2] if len(nz) > 1 else (nz[0] if nz else -1)
    print(f"{f:16s} niezb {nz} med(po 1.) {med} | czas max {max(tm) if tm else 0:.1f}s | NA TARG poza: {', '.join(bad) or 'brak'} | {' '.join(cov)}")
