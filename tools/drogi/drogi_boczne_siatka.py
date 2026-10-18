# -*- coding: utf-8 -*-
# drogi_boczne_siatka.py - plik danych Armoury/ModuleData/arm_map_side_roads_grid.tsv (f11-drogi, Jeff 18.10: boczne drogi od traktow do dalszych wiosek):
# siatka ladu przejezdnego 2 x 2 jedn. z wyniku sondy geografii (geo-siatka.tsv) - po niej mod (MapSideRoads.Plan.cs) prowadzi boczne drogi omijajac
# wode, gory i strome stoki. Mod niczego nie mierzy w grze do trasy; wioski bierze w grze z ukladu sesji (VillageBook), wiec trasa idzie za ukladem v6 / v7.
#
# Uruchomienie (python -I, sama biblioteka standardowa):
#   python -I tools/drogi/drogi_boczne_siatka.py [--wej D:/Backup-Bannerlord/geo-dane-2026-10-16/geo-siatka.tsv]
#     [--wyj Armoury/ModuleData/arm_map_side_roads_grid.tsv] [--teren ".../Modules/ROT-Map/SceneObj/Main_map/terrain.bin"]
#
# Wejscie: geo-siatka.tsv sondy MapGeoProbe (G3, bieg 16.10 13:59, ten sam bieg co geo-drogi.tsv): kolumny x, y (co 2 jedn., od 0), nawig (tak / nie -
# sciana ladowa siatki nawigacyjnej), teren (Plain / Steppe / Desert / Forest / Fording / Mountain / Water / CoastalSea ...), wys (wysokosc terenu).
# Wyjscie: naglowek (wersja, terrain_md5 i rozmiar terrain.bin do straznika w grze, komorka, poczatek, wymiary, opis kodow) i ny wierszy po nx znakow
# (wiersz j = y 2j, znak i = x 2i): '.' = bez drogi ladem (nawig nie: morze, gory ROT, woda), litera 'A' + klasa * 4 + stok:
#   klasa 0 rownina (Plain, RuralArea), 1 step (Steppe), 2 pustynia (Desert), 3 las (Forest), 4 brod (Fording);
#   stok 0 < 0.12, 1 < 0.25, 2 < 0.45, 3 bardziej stromo (zmiana wysokosci na 1 jedn. do najbardziej stromego z 4 sasiadow).
import argparse, hashlib, os

VERSION = 1
MD5_1610 = '2d15af9565a0d22d59b31a56b41bf310'   # terrain.bin ROT-Map 8.1.8 (16.10) - jak arm_map_roads_geo.tsv
SIZE_1610 = 50638996
CELL = 2
CLASS = {'Plain': 0, 'RuralArea': 0, 'Steppe': 1, 'Desert': 2, 'Forest': 3, 'Fording': 4}
SLOPES = (0.12, 0.25, 0.45)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--wej', default='D:/Backup-Bannerlord/geo-dane-2026-10-16/geo-siatka.tsv')
    here = os.path.dirname(os.path.abspath(__file__))
    ap.add_argument('--wyj', default=os.path.join(here, '..', '..', 'Armoury', 'ModuleData', 'arm_map_side_roads_grid.tsv'))
    ap.add_argument('--teren', default=r'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\ROT-Map\SceneObj\Main_map\terrain.bin')
    a = ap.parse_args()
    md5, size = MD5_1610, SIZE_1610
    if os.path.isfile(a.teren):
        b = open(a.teren, 'rb').read()
        md5, size = hashlib.md5(b).hexdigest(), len(b)
    cells = {}
    hts = {}
    nx = ny = 0
    with open(a.wej, encoding='utf-8') as f:
        cols = f.readline().rstrip('\n').rstrip('\r').split('\t')
        ix, iy, inav, iter_, iwys = cols.index('x'), cols.index('y'), cols.index('nawig'), cols.index('teren'), cols.index('wys')
        for raw in f:
            c = raw.rstrip('\n').rstrip('\r').split('\t')
            if len(c) <= max(ix, iy, inav, iter_, iwys):
                continue
            i, j = int(c[ix]) // CELL, int(c[iy]) // CELL
            nx, ny = max(nx, i + 1), max(ny, j + 1)
            try:
                hts[(i, j)] = float(c[iwys])
            except ValueError:
                pass
            if c[inav] == 'tak' and c[iter_] in CLASS:
                cells[(i, j)] = CLASS[c[iter_]]
    rows = []
    counts = [0] * 21
    for j in range(ny):
        line = []
        for i in range(nx):
            k = cells.get((i, j))
            if k is None:
                line.append('.')
                counts[20] += 1
                continue
            h = hts.get((i, j))
            s = 0.0
            if h is not None:
                for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    h2 = hts.get((i + di, j + dj))
                    if h2 is not None:
                        s = max(s, abs(h2 - h) / CELL)
            b = 0
            while b < len(SLOPES) and s >= SLOPES[b]:
                b += 1
            code = k * 4 + b
            counts[code] += 1
            line.append(chr(ord('A') + code))
        rows.append(''.join(line))
    land = sum(counts[:20])
    with open(a.wyj, 'w', encoding='utf-8', newline='\n') as o:
        o.write('# Armoury: passable land grid for the side roads to far villages (f11-drogi) - do not edit\n')
        o.write('# version: %d\n' % VERSION)
        o.write('# source: geo-siatka.tsv (geo probe MapGeoProbe G3 run 2026-10-16 13:59, branch w-toku/noc-geo 19e6f041); generator tools/drogi/drogi_boczne_siatka.py\n')
        o.write('# terrain_md5: %s\n' % md5)
        o.write('# terrain_size: %d\n' % size)
        o.write('# cell: %d\n' % CELL)
        o.write('# origin: 0 0\n')
        o.write('# size: %d %d\n' % (nx, ny))
        o.write('# land cells: %d of %d\n' % (land, nx * ny))
        o.write("# codes: one char per cell, row j = y %d*j, char i = x %d*i; '.' = no land route (sea, ROT mountains, water); letter 'A' + class * 4 + slope:\n" % (CELL, CELL))
        o.write('#   class 0 plain (Plain, RuralArea), 1 steppe, 2 desert, 3 forest, 4 ford; slope 0 < 0.12, 1 < 0.25, 2 < 0.45, 3 steeper (height change per map unit)\n')
        for r in rows:
            o.write(r + '\n')
    print('siatka %d x %d, lad %d, morze/gory %d, wyjscie %s' % (nx, ny, land, counts[20], os.path.abspath(a.wyj)))
    names = ['rownina', 'step', 'pustynia', 'las', 'brod']
    for k in range(5):
        print('  %-8s %s' % (names[k], ' '.join(str(counts[k * 4 + b]) for b in range(4))))


if __name__ == '__main__':
    main()
