# -*- coding: utf-8 -*-
# rysuj.py - obrazek planu bocznych drog (f11-drogi) z wyniku podgladu (tools/drogi/boczne-podglad, Program.cs) - sama geometria, nie wyglad z gry:
# siatka ladu (rownina / step / pustynia / las / brod, ciemniej = bardziej stromo; granatowe = bez drogi ladem: morze, gory ROT, woda), trakty ROT (szare,
# szerokosc farby), stemple bocznych drog (brazowe elipsy), wioski ukladu (biale), wsie gry (czerwone), miasta i zamki (czarne).
# Uruchomienie z katalogu repo: python -I tools/drogi/boczne-podglad/rysuj.py <wynik> <srodek x> <srodek y> <bok jedn.> <plik.png> [piksele na jedn., 12]
import math, os, sys
from PIL import Image, ImageDraw

def main():
    d, cx, cy, size, out = sys.argv[1], float(sys.argv[2]), float(sys.argv[3]), float(sys.argv[4]), sys.argv[5]
    S = float(sys.argv[6]) if len(sys.argv) > 6 else 12.0
    md = os.path.join('Armoury', 'ModuleData')
    W = int(size * S)
    x0, y0 = cx - size / 2, cy - size / 2

    def P(x, y):
        return ((x - x0) * S, (y0 + size - y) * S)

    rows = [l.rstrip('\n') for l in open(os.path.join(md, 'arm_map_side_roads_grid.tsv'), encoding='utf-8') if not l.startswith('#')]
    img = Image.new('RGB', (W, W), (40, 60, 90))
    dr = ImageDraw.Draw(img, 'RGBA')
    cols = {0: (150, 170, 110), 1: (185, 180, 120), 2: (210, 190, 140), 3: (80, 120, 70), 4: (100, 140, 180)}
    for j in range(int(y0 // 2) - 1, int((y0 + size) // 2) + 2):
        for i in range(int(x0 // 2) - 1, int((x0 + size) // 2) + 2):
            if j < 0 or j >= len(rows) or i < 0 or i >= len(rows[j]) or rows[j][i] == '.':
                continue
            c = ord(rows[j][i]) - 65
            f = 1.0 - 0.12 * (c & 3)
            col = tuple(int(v * f) for v in cols[c >> 2])
            dr.rectangle([P(i * 2 - 1, j * 2 + 1), P(i * 2 + 1, j * 2 - 1)], fill=col)
    pts = []
    for l in open(os.path.join(md, 'arm_map_roads_geo.tsv'), encoding='utf-8'):
        if l.startswith('#') or not l.strip():
            continue
        c = l.split('\t')
        pts.append((int(c[0]), float(c[2]), float(c[3]), float(c[4])))
    for i in range(1, len(pts)):
        if pts[i][0] != pts[i - 1][0]:
            continue
        h = min(1.5, max(0.15, pts[i][3] * 0.5))
        a, b = P(pts[i - 1][1], pts[i - 1][2]), P(pts[i][1], pts[i][2])
        if -50 < a[0] < W + 50 and -50 < a[1] < W + 50:
            dr.line([a, b], fill=(70, 70, 75, 255), width=max(1, int(2 * h * S)))
    for l in open(os.path.join(d, 'stamps.tsv')):
        x, y, yaw, ac, al, alpha, r = l.split('\t')
        x, y = float(x), float(y)
        if not (x0 - 2 < x < x0 + size + 2 and y0 - 2 < y < y0 + size + 2):
            continue
        yaw, ac, al, alpha = float(yaw), float(ac), float(al), float(alpha)
        poly = []
        for k in range(16):
            t = 2 * math.pi * k / 16
            lx, ly = math.cos(t) * al * 0.85, math.sin(t) * ac * 0.85
            poly.append(P(x + lx * math.cos(yaw) - ly * math.sin(yaw), y + lx * math.sin(yaw) + ly * math.cos(yaw)))
        dr.polygon(poly, fill=(170, 95, 30, int(170 * alpha)))
    for l in open(os.path.join(d, 'nodes.tsv')):
        x, y, gm, _ = l.rstrip('\n').split('\t')
        x, y = float(x), float(y)
        if x0 < x < x0 + size and y0 < y < y0 + size:
            a, r = P(x, y), (4 if gm == '0' else 6)
            dr.ellipse([a[0] - r, a[1] - r, a[0] + r, a[1] + r], fill=(255, 255, 255) if gm == '0' else (220, 40, 40), outline=(0, 0, 0))
    for l in open(os.path.join(d, 'obst.tsv')):
        x, y = map(float, l.split('\t'))
        if x0 < x < x0 + size and y0 < y < y0 + size:
            a = P(x, y)
            dr.rectangle([a[0] - 8, a[1] - 8, a[0] + 8, a[1] + 8], fill=(20, 20, 20))
    img.save(out)
    print('obrazek', os.path.abspath(out))


if __name__ == '__main__':
    main()
