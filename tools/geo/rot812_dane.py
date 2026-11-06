# -*- coding: utf-8 -*-
# rot812_dane.py - dane wejsciowe generatorow mapy (drogi boczne, zajazdy) dla ROT 8.1.2 z danych 8.1.8 (sonda geo 16.10, a-teren) + roznic paczki (TYLKO ODCZYT).
#
# Uruchomienie (python -I, numpy + lz4 + Pillow):
#   python -I tools/geo/rot812_dane.py [--stara ".../Modules/ROT-Map"] [--nowa "D:/Backup-Bannerlord/rot-8.1.2/rozpakowane/ROT 8.1.2/ROT-Map"]
#     [--geo D:/Backup-Bannerlord/geo-dane-2026-10-16] [--teren D:/Backup-Bannerlord/drogi-siec/a-teren] [--wyj D:/Backup-Bannerlord/rot812-dane]
#
# Co sie zmienia w 8.1.2 na mapie swiata (porownanie 06.11): farba drog (warstwa road) BEZ ZMIAN (te same 311 lamanych); wysokosc terenu tylko w 3 miejscach
# (Sharp Point 648/422, Riverrun 331/503, Gulltown 657-678/550-566, max 0.5 jedn.); farba trawy przy rzece Blueburn (355-435 / 231-282); settlements.xml:
# White Harbor (459.135,767.818 -> 458.1084,767.3082) i Sharp Point (644.804,427.047 -> 646.7764,426.2932) z bramami; nowe modele Riverrun / Blizniaki / Gulltown
# (prefaby riverrun_model / twins_model / gulltown_model, w obrysie dawnych). navmesh.bin przebudowany (+2 B) - poza gra nie do odczytu.
# Wynik (<wyj>):
#   geo/geo-siatka.tsv  = sonda 16.10 z 'wys' poprawiona o roznice HGHT (wys = a * surowa + b, a z dopasowania calej mapy); nawig / teren / reszta ze starej sondy
#   geo/geo-drogi.tsv, geo-trasy*.tsv = kopie sondy 16.10 (drogi bez zmian; trasy AI miedzy twierdzami - koncowki przy WH / SP o 1-2 jedn. stare)
#   a-teren/osady.json  = osady.json z nowymi x/y/gx/gy/scene_pos miast, ktore 8.1.2 przesunelo (z settlements.xml paczki)
# Dokladne dane = sonda geografii w grze na mapie 8.1.2 (MapGeoProbe, autotest) - wtedy ten skrypt niepotrzebny.
import argparse, io, json, os, re, shutil, struct, sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import geo_drogi_rot as g


def heights(b):
    ch = g.chunks(b)
    d, t = g.blob(b, ch, 'HGHT')
    A = np.zeros((g.TN, g.TN), np.int64)
    for n in range(256):
        q = t[n]; e = t[n + 1] if n < 255 else len(d)
        raw = d[q:e]; i = raw.index(b'\x89PNG')   # 4 B przed PNG (16 bit, 257 x 257)
        iy, ix = divmod(n, 16)
        A[iy * 256:iy * 256 + 257, ix * 256:ix * 256 + 257] = np.array(Image.open(io.BytesIO(raw[i:]))).astype(np.int64)
    return A


def towns(path):
    out = {}
    for m in re.finditer(r'<Settlement id="([^"]+)"[^>]*?posX="([^"]+)" posY="([^"]+)"(?:[^>]*?gate_posX="([^"]+)" gate_posY="([^"]+)")?', open(path, encoding='utf-8').read()):
        out[m.group(1)] = tuple(float(v) if v else None for v in m.groups()[1:])
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--stara', default=r'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\ROT-Map')
    ap.add_argument('--nowa', default='D:/Backup-Bannerlord/rot-8.1.2/rozpakowane/ROT 8.1.2/ROT-Map')
    ap.add_argument('--geo', default='D:/Backup-Bannerlord/geo-dane-2026-10-16')
    ap.add_argument('--teren', default='D:/Backup-Bannerlord/drogi-siec/a-teren')
    ap.add_argument('--wyj', default='D:/Backup-Bannerlord/rot812-dane')
    a = ap.parse_args()
    sys.stdout.reconfigure(encoding='utf-8')
    O = open(os.path.join(a.stara, 'SceneObj', 'Main_map', 'terrain.bin'), 'rb').read()
    N = open(os.path.join(a.nowa, 'SceneObj', 'Main_map', 'terrain.bin'), 'rb').read()
    HO, HN = heights(O), heights(N)
    os.makedirs(os.path.join(a.wyj, 'geo'), exist_ok=True)
    os.makedirs(os.path.join(a.wyj, 'a-teren'), exist_ok=True)
    rows = open(os.path.join(a.geo, 'geo-siatka.tsv'), encoding='utf-8').read().split('\n')
    hdr = rows[0].split('\t'); ix, iy, iw = hdr.index('x'), hdr.index('y'), hdr.index('wys')
    xs, ws = [], []
    for r in rows[1:]:
        c = r.split('\t')
        if len(c) <= iw: continue
        x, y = int(c[ix]), int(c[iy])
        if x % 40 or y % 40: continue
        try: w = float(c[iw])
        except ValueError: continue
        xs.append(HO[min(4096, round(y * 2.56)), min(4096, round(x * 2.56))]); ws.append(w)
    xs = np.array(xs, float); ws = np.array(ws)
    ka, kb = np.polyfit(xs, ws, 1)
    res = np.abs(ws - (ka * xs + kb))
    print('wys = %.7f * surowa + %.4f (reszta mediana %.4f, max %.3f, probek %d)' % (ka, kb, np.median(res), res.max(), len(xs)))
    D = HN - HO
    out = [rows[0]]; ch = []
    for r in rows[1:]:
        c = r.split('\t')
        if len(c) > iw:
            x, y = int(c[ix]), int(c[iy])
            dd = D[min(4096, round(y * 2.56)), min(4096, round(x * 2.56))]
            if dd != 0:
                try:
                    c[iw] = '%.2f' % (float(c[iw]) + ka * dd); ch.append((x, y, ka * dd)); r = '\t'.join(c)
                except ValueError:
                    pass
        out.append(r)
    open(os.path.join(a.wyj, 'geo', 'geo-siatka.tsv'), 'w', encoding='utf-8', newline='\n').write('\n'.join(out))
    print('geo-siatka: zmieniona wysokosc %d probek: %s' % (len(ch), ', '.join('%d/%d %+.2f' % p for p in ch)))
    for n in ('geo-drogi.tsv', 'geo-trasy.tsv', 'geo-trasy-punkty.tsv'):
        shutil.copyfile(os.path.join(a.geo, n), os.path.join(a.wyj, 'geo', n))
    to, tn = towns(os.path.join(a.stara, 'ModuleData', 'settlements.xml')), towns(os.path.join(a.nowa, 'ModuleData', 'settlements.xml'))
    moved = {k: v for k, v in tn.items() if k in to and to[k] != v}
    js = json.load(io.open(os.path.join(a.teren, 'osady.json'), encoding='utf-8'))
    for o in js['osady']:
        if o['id'] in moved:
            x, y, gx, gy = moved[o['id']]
            print('osada %s %s: %.3f,%.3f -> %.4f,%.4f (brama %s,%s -> %s,%s)' % (o['id'], o['name'], o['x'], o['y'], x, y, o.get('gx'), o.get('gy'), gx, gy))
            o['x'], o['y'], o['scene_pos'] = x, y, [x, y]
            if gx is not None: o['gx'], o['gy'] = gx, gy
    json.dump(js, io.open(os.path.join(a.wyj, 'a-teren', 'osady.json'), 'w', encoding='utf-8'), ensure_ascii=False)
    print('przesuniete osady: %d (%s); wynik w %s' % (len(moved), ', '.join(sorted(moved)), a.wyj))


if __name__ == '__main__':
    main()
