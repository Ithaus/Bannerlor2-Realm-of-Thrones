# -*- coding: utf-8 -*-
# drogi_plik_modu.py - plik danych Armoury/ModuleData/arm_map_roads_geo.tsv (drogi malowane ROT jako lamane w jedn. mapy) z wyniku sondy geografii.
#
# Uruchomienie (python -I, sama biblioteka standardowa):
#   python -I tools/drogi/drogi_plik_modu.py [--wej D:/Backup-Bannerlord/geo-dane-2026-10-16/geo-drogi.tsv] [--wyj Armoury/ModuleData/arm_map_roads_geo.tsv]
#     [--teren "C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/ROT-Map/SceneObj/Main_map/terrain.bin"]
#
# Skad dane: drogi ROT to FARBA TERENU (warstwa "road", slot 13, vtstt_ground_roads_e_d w terrain.bin) - lamane liczy tools/geo/geo_drogi_rot.py
# (galaz w-toku/noc-geo 19e6f041: waga > 48/255, domkniecie 1 probki, Zhang-Suen, Douglas-Peucker 0.2 jedn., ostrogi < 2 jedn. usuniete),
# a sonda geografii MapGeoProbe (G3, bieg 16.10 13:59, Armoury-2026-10-16_13-59-16.log) sprawdzila kazdy punkt w grze (geo-drogi.tsv: nawig, teren,
# wyspa, wysokosc). Wejscie: geo-drogi.tsv sondy (kolumny droga, seq, x, y, typ, szer, waga, ...) albo arm_geo_drogi_rot.tsv narzedzia (te same pierwsze 7).
# Wyjscie: naglowek (zrodlo, wersja, terrain_md5 i rozmiar terrain.bin do straznika w grze, liczby) i wiersze road, seq, x, y, width (szerokosc pasa
# farby w jedn. mapy na lamana = "szer" narzedzia). Pol-szerokosc i margines liczy mod (MapRoads.cs: half = width / 2 w 0.15..1.5, margines z MCM).
# Gdy terrain.bin jest pod --teren, md5 i rozmiar liczone z pliku (inaczej z naglowka zrodla narzedzia, gdy jest, albo stala z 16.10).
import argparse, hashlib, math, os, sys

VERSION = 1
MD5_1610 = '2d15af9565a0d22d59b31a56b41bf310'   # terrain.bin ROT-Map 8.1.8 (16.10, tools/geo/dane/arm_geo_drogi_rot.tsv)
SIZE_1610 = 50638996


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--wej', default='D:/Backup-Bannerlord/geo-dane-2026-10-16/geo-drogi.tsv')
    here = os.path.dirname(os.path.abspath(__file__))
    ap.add_argument('--wyj', default=os.path.join(here, '..', '..', 'Armoury', 'ModuleData', 'arm_map_roads_geo.tsv'))
    ap.add_argument('--teren', default=r'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\ROT-Map\SceneObj\Main_map\terrain.bin')
    a = ap.parse_args()
    md5, size = MD5_1610, SIZE_1610
    if os.path.isfile(a.teren):
        b = open(a.teren, 'rb').read()
        md5, size = hashlib.md5(b).hexdigest(), len(b)
    rows = []
    cols = None
    with open(a.wej, encoding='utf-8') as f:
        for raw in f:
            line = raw.rstrip('\n').rstrip('\r')
            if not line:
                continue
            if line.startswith('#'):
                h = line.lstrip('#').strip()
                if h.startswith('droga\t'):
                    cols = h.split('\t')
                elif h.startswith('terrain_md5:') and not os.path.isfile(a.teren):
                    md5 = h.split(':', 1)[1].strip().split(' ')[0]
                continue
            c = line.split('\t')
            if cols is None:
                cols = c   # naglowek sondy bez '#'
                continue
            d = dict(zip(cols, c))
            rows.append((int(d['droga']), int(d['seq']), float(d['x']), float(d['y']), float(d['szer'])))
    rows.sort(key=lambda r: (r[0], r[1]))
    lines = {}
    for r in rows:
        lines.setdefault(r[0], []).append(r)
    tot = 0.0
    for pts in lines.values():
        for i in range(1, len(pts)):
            tot += math.hypot(pts[i][2] - pts[i - 1][2], pts[i][3] - pts[i - 1][3])
    segs = sum(len(p) - 1 for p in lines.values())
    os.makedirs(os.path.dirname(os.path.abspath(a.wyj)), exist_ok=True)
    with open(a.wyj, 'w', encoding='utf-8', newline='\n') as f:
        f.write('# Armoury: Realm of Thrones painted roads (terrain paint layer "road", slot 13, vtstt_ground_roads_e_d) as polylines in map units - do not edit\n')
        f.write('# version: %d\n' % VERSION)
        f.write('# source: %s (geo probe MapGeoProbe G3 run 2026-10-16 13:59, branch w-toku/noc-geo 19e6f041; polylines from tools/geo/geo_drogi_rot.py: '
                'weight > 48/255, 1-sample closing, Zhang-Suen, Douglas-Peucker 0.2, spurs < 2 removed); generator tools/drogi/drogi_plik_modu.py\n' % os.path.basename(a.wej))
        f.write('# terrain_md5: %s\n' % md5)
        f.write('# terrain_size: %d\n' % size)
        f.write('# polylines: %d, points: %d, segments: %d, length: %.1f\n' % (len(lines), len(rows), segs, tot))
        f.write('# width: paint band width in map units per polyline (samples with weight > 48 within 1.6 of the skeleton x sample area / length); '
                'the mod takes half-width = width / 2 clamped to 0.15..1.5 and adds Map Road Margin\n')
        f.write('#road\tseq\tx\ty\twidth\n')
        for r in rows:
            f.write('%d\t%d\t%.2f\t%.2f\t%.2f\n' % r)
    print('zapisane: %s | lamanych %d, punktow %d, odcinkow %d, dlugosc %.1f jedn. | terrain_md5 %s, rozmiar %d' % (a.wyj, len(lines), len(rows), segs, tot, md5, size))


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    main()
