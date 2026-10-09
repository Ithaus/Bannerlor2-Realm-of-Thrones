"""Koszty zmierzone w logu Armoury (174b.5 F6 i starsze linie kosztu) - ranking do nastepnego kroku tempa.

Uzycie (Python 3, tylko odczyt): python -I tools/p174b_koszty.py <Armoury-*.log> [...]
Linia "Koszt 171-174 (doba)": ms na dobe kazdej pozycji (srednio caly bieg i ostatnie 10 dob) i udzial "nasze 171-174b" w dobie.
Pozostale pozycje - wzor skryptu diagnozy D (a174b/tempo-skrypty/koszty.py).
"""
import re
import sys

P = [
    ("Obieg przeliczenie doby", r"przeliczenie doby ([0-9.]+) ms"),
    ("Obieg okna (nasze latki)", r"okna ok\. ([0-9.]+) ms"),
    ("Obieg nasluch zlota", r"nasluch zdarzen zlota ok\. ([0-9.]+) ms"),
    ("Towary ramki", r"ramki ([0-9.]+) ms, linie"),
    ("Pokrycie 171 (co 5 dob)", r"Pokrycie zbrojowni AI \(171\).*czas przegladu (\d+) ms"),
]


def main():
    for path in sys.argv[1:]:
        print(path.replace("\\", "/").split("/")[-1])
        lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
        ko = [l for l in lines if "Koszt 171-174 (doba): dzien" in l]
        if ko:
            items = {}
            pct, wall = [], []
            for l in ko:
                m = re.search(r"doba (\d+) ms \(zegar\); nasze 171-174b ([0-9.]+) ms \(([0-9.]+)% doby", l)
                if m:
                    wall.append(float(m.group(1))); pct.append(float(m.group(3)))
                for name, ms, calls in re.findall(r"([A-Za-z][^,\[\]]*?) ([0-9.]+) ms/(\d+)", l):
                    items.setdefault(name.strip(), []).append(float(ms))
            print("  Koszt 171-174: dob %d, doba srednio %.0f ms, nasze srednio %.2f%% (ostatnie 10: %.2f%%)" % (len(ko), sum(wall) / max(1, len(wall)), sum(pct) / max(1, len(pct)), sum(pct[-10:]) / max(1, len(pct[-10:]))))
            for name, v in sorted(items.items(), key=lambda kv: -sum(kv[1]) / len(kv[1])):
                last = v[-10:]
                print("    %-45s sr. %8.1f ms/d | ostatnie 10 %8.1f" % (name, sum(v) / len(v), sum(last) / len(last)))
        for name, p in P:
            v = [float(m.group(1)) for l in lines for m in [re.search(p, l)] if m]
            if v:
                last = v[-10:]
                print("  %-28s sr. caly %7.1f | sr. ostatnie 10 %7.1f | n=%d" % (name, sum(v) / len(v), sum(last) / len(last), len(v)))


if __name__ == "__main__":
    main()
