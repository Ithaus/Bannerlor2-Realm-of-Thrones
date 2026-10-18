import re
import sys
import os

# podsumowanie biegow harnessu (python -I summ.py <tag>...): niezbiezne na Plan, czas, NA TARG poza 0.95-1.05 (bez wyjatkow projektu), wyzywienie krain zimnych
# (ostatni Plan); recenzja feed-swiat: nadwyzka targu Essos po kroku 8 (mediana Planow po pierwszym), kcal R/P miast do gry (ceny startu) i przy narzucie Planu,
# kcal wsi bez pokrycia (Westeros / Essos), narzut Essos, towary na granicy ceny z odchyleniem > 2% (ostatni Plan)
EXC = {"carrot", "apple", "fur", "honey", "spice", "whale_oil", "walrus_tusk", "Egg", "hides"}
H = os.path.dirname(os.path.abspath(__file__))


def path_of(tag):
    for d in (os.path.join(H, "out"), os.path.join(H, "h", "out")):
        p = os.path.join(d, tag + ".out")
        if os.path.isfile(p):
            return p
    return None


def seg(line, mk):
    for part in line.split(" | "):
        if part.startswith(mk + ":"):
            return part
    return ""


def grab(rx, t, d="-"):
    m = re.search(rx, t)
    return m.group(1) if m else d


for f in sys.argv[1:]:
    p = path_of(f)
    if not p:
        print(f, "brak")
        continue
    t = open(p, encoding="utf-8").read().splitlines()
    nz = [int(m.group(1)) for l in t for m in [re.match(r"  NIEZB \d+: (\d+)", l)] if m]
    tm = [float(m.group(1)) for l in t for m in [re.match(r"PLAN \d+: ([\d.]+) s", l)] if m]
    bad = []
    na = [l for l in t if re.match(r"  [WE] NA TARG: ", l)][-2:]
    for l in na:
        m = re.match(r"  ([WE]) NA TARG: (.*)", l)
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
    cl = [l for l in t if "KS: rynki Planu (feed-swiat)" in l]
    exE = []
    for l in cl[1:]:
        v = grab(r"nadwyzka targu / popyt targu [-\d.]+% -> ([-\d.]+)%", seg(l, "Essos"), None)
        if v is not None:
            exE.append(float(v))
    exm = sorted(exE)[len(exE) // 2] if exE else float("nan")
    lastc = cl[-1] if cl else ""
    e, w = seg(lastc, "Essos"), seg(lastc, "Westeros")
    med = sorted(nz[1:])[len(nz[1:]) // 2] if len(nz) > 1 else (nz[0] if nz else -1)
    print(f"{f:16s} niezb {nz} med(po 1.) {med} | czas max {max(tm) if tm else 0:.1f}s (cieple {', '.join(f'{x:.0f}' for x in tm[1:])}) | NA TARG poza: {', '.join(bad) or 'brak'} | {' '.join(cov)}")
    print(f"{'':16s} Essos nadwyzka po kroku 8 mediana {exm:.1f}% | kcal W/R/P do gry E {grab(r'kcal jedzenia W/R/P przy cenach startu ([-\d./]+)%', e)} / przy narzucie {grab(r'przy narzucie Planu ([-\d./]+)% \(tyle', e)}"
          f" | wsie bez pokrycia W {grab(r'kcal koszyka bez pokrycia ([\d.]+)%', w)}% E {grab(r'kcal koszyka bez pokrycia ([\d.]+)%', e)}% (E w domu {grab(r'robione w domu ([\d.]+)%', e)}%, drabin wsi z narzutem {grab(r'drabiny koszyka z narzutem (\d+)', e)})"
          f" | narzut E: {grab(r'towary z narzutem \d+ \(([^)]*)\)', e, 'brak')} | granica E: {grab(r'na granicy ceny z odchyleniem ponad 2% (\d+(?: \([^)]*\))?)', e)}")
