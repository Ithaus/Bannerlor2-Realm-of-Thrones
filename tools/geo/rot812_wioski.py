# -*- coding: utf-8 -*-
# rot812_wioski.py - pliki wiosek Armoury (arm_map_villages*.tsv) dla mapy ROT 8.1.2 bez nowego ukladu: te same wioski, uid i osady (crc ukladu "uid:osady;"
# bez zmian - zapis kampanii, arm_district i lore przechodza 1:1), tylko:
#   1. naglowek "# scene_xml_crc:" -> 0 + "# scene_terrain_size: <B>" (CRC sceny liczy silnik - IMapScene.GetSceneXmlCrc, nie CRC32 scene.xscene - i poza gra jest nieznane;
#      mod przyjmuje plik, gdy terrain.bin ROT-Map ma ten rozmiar - MapVillageData.CheckScene; w logu Armoury "CRC sceny gry N - do naglowka");
#   2. wioska blizej przesunietego miasta niz D_TOWN (12, jak v6/v7) albo zamku niz D_CASTLE (9) - odsunieta promieniscie od NOWEGO srodka na swoja dawna odleglosc
#      (siatka 0.5 jedn. jak v7), z kontrola: inne wioski >= min_distance pliku, wsie gry, zajazdy >= 12.26, malowana droga >= 1.5, lad (geo-siatka nawig);
#   3. sprawdzenie obrysow nowych modeli 8.1.2 (riverrun_model, twins_model, gulltown_model + molo/skaly Gulltown) - wioska w obrysie + 1 jedn. = przesuniecie (jak 2).
# Uruchomienie (python -I, z katalogu repo):  python -I tools/geo/rot812_wioski.py [--dane D:/Backup-Bannerlord/rot812-dane] [--dry]
import argparse, io, json, math, os, sys, zlib

HERE = os.path.dirname(os.path.abspath(__file__))
MD = os.path.join(HERE, '..', '..', 'Armoury', 'ModuleData')
FILES = ['arm_map_villages.tsv', 'arm_map_villages_v6.tsv', 'arm_map_villages_v5.tsv', 'arm_map_villages_v4.tsv']
TERRAIN_SIZE = 50639409          # ROT-Map 8.1.2 terrain.bin (md5 82fbc6999da5120b5c7d0fea43aabb4b)
OLD_CRC = '146201570'            # ROT 8.1.8
D_TOWN, D_CASTLE, D_INN, D_PAINT = 12.0, 9.0, 12.26, 1.5
# obrysy nowych modeli (srodek instancji w scene.xscene + zakres dzieci prefabu z Prefabs/ROT_north.xml, bez obrotu, skala 1)
MODELS = {'riverrun_model': (330.8, 339.0, 497.8, 505.4), 'twins_model': (368.0, 375.6, 622.3, 632.8), 'gulltown_model+molo': (659.9, 666.5, 550.3, 557.7)}
# miasta przesuniete w 8.1.2 (settlements.xml): stary srodek -> nowy
MOVED = {'town_B3': ((459.135, 767.818), (458.1084, 767.3082)), 'ROT_town31': ((644.804, 427.047), (646.7764, 426.2932))}


def read(path):
    raw = open(path, 'rb').read().decode('utf-8')
    lines = raw.replace('\r\n', '\n').split('\n')
    hdr = None; rows = []
    for i, l in enumerate(lines):
        if l.startswith('#id\t'): hdr = l[1:].split('\t'); continue
        if l.startswith('#') or not l: continue
        rows.append((i, dict(zip(hdr, l.split('\t')))))
    return lines, hdr, rows


def layout_crc(rows):
    return zlib.crc32(''.join('%s:%s;' % (r['uid'], r['settlements']) for _, r in rows).encode('utf-8')) & 0xffffffff


def box_d(x, y, b):
    return math.hypot(max(b[0] - 1 - x, 0, x - b[1] - 1), max(b[2] - 1 - y, 0, y - b[3] - 1))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--dane', default='D:/Backup-Bannerlord/rot812-dane')
    ap.add_argument('--dry', action='store_true')
    a = ap.parse_args()
    sys.stdout.reconfigure(encoding='utf-8')
    osady = json.load(io.open(os.path.join(a.dane, 'a-teren', 'osady.json'), encoding='utf-8'))['osady']
    gv = [(o['x'], o['y']) for o in osady if o['kind'] == 'village']
    towns = [(o['x'], o['y']) for o in osady if o['kind'] == 'town']
    castles = [(o['x'], o['y']) for o in osady if o['kind'] == 'castle']
    inns = []
    for l in open(os.path.join(MD, 'arm_roadside_inns.tsv'), encoding='utf-8'):
        if l.startswith('#'): continue
        c = l.rstrip('\r\n').split('\t'); inns += [(float(c[3]), float(c[4])), (float(c[5]), float(c[6]))]
    roads = []
    for l in open(os.path.join(MD, 'arm_map_roads_geo.tsv'), encoding='utf-8'):
        if l.startswith('#'): continue
        c = l.split('\t'); roads.append((int(c[0]), float(c[2]), float(c[3])))
    nav = {}; dist = {}
    for l in open(os.path.join(a.dane, 'geo', 'geo-siatka.tsv'), encoding='utf-8'):
        c = l.split('\t')
        if c[0] != 'x': nav[(int(c[0]), int(c[1]))] = c[2] == 'tak'; dist[(int(c[0]), int(c[1]))] = c[11] if len(c) > 11 else '-'

    def droad(x, y):
        best = 1e9
        for i in range(1, len(roads)):
            if roads[i][0] != roads[i - 1][0]: continue
            ax, ay = roads[i - 1][1:]; bx, by = roads[i][1:]; dx, dy = bx - ax, by - ay; L = dx * dx + dy * dy
            t = 0 if L == 0 else max(0, min(1, ((x - ax) * dx + (y - ay) * dy) / L))
            best = min(best, math.hypot(ax + t * dx - x, ay + t * dy - y))
        return best

    for fn in FILES:
        path = os.path.join(MD, fn)
        lines, hdr, rows = read(path)
        crc0 = layout_crc(rows)
        mind = 6.0
        for l in lines:
            if l.startswith('# min_distance:'): mind = float(l.split(':')[1])
        moved = []
        pts = [(float(r['x']), float(r['y'])) for _, r in rows]
        for k, (li, r) in enumerate(rows):
            x, y = pts[k]
            why = None
            for sid, (o, n) in MOVED.items():
                lim = D_TOWN   # oba przesuniete to miasta
                dn, do = math.hypot(x - n[0], y - n[1]), math.hypot(x - o[0], y - o[1])
                if dn < lim + 0.01 and dn < do:
                    why = (sid, n, max(do, lim + 0.05))
            for mname, b in MODELS.items():
                if box_d(x, y, b) - math.hypot(float(r['half_len']), float(r['half_wid'])) < 0:
                    cx, cy = (b[0] + b[1]) / 2, (b[2] + b[3]) / 2
                    why = (mname, (cx, cy), math.hypot(b[1] - b[0], b[3] - b[2]) / 2 + 1 + math.hypot(float(r['half_len']), float(r['half_wid'])))
            if not why: continue
            src, c, want = why
            ux, uy = (x - c[0]), (y - c[1]); L = math.hypot(ux, uy); ux, uy = ux / L, uy / L
            best = None
            # punkty siatki 0.5 jedn. do 3 jedn. od starego miejsca, najblizszy pierwszy; na ziemi wlasnego okregu (wies_gry sondy, jak sprawdz_v7)
            cand = sorted(((math.floor(x * 2) / 2 + i * 0.5, math.floor(y * 2) / 2 + j * 0.5) for i in range(-6, 8) for j in range(-6, 8)),
                          key=lambda p: math.hypot(p[0] - x, p[1] - y))
            for _ in (0,):
                for nx, ny in cand:
                    if math.hypot(nx - x, ny - y) > 3.0: break
                    dc = math.hypot(nx - c[0], ny - c[1])
                    if dc < want - 0.001: continue
                    if dist.get((int(round(nx / 2)) * 2, int(round(ny / 2)) * 2)) != r['village_id']: continue
                    dv = min(math.hypot(px - nx, py - ny) for j, (px, py) in enumerate(pts) if j != k)
                    dg = min(math.hypot(px - nx, py - ny) for px, py in gv)
                    dt = min(math.hypot(px - nx, py - ny) for px, py in towns); dca = min(math.hypot(px - nx, py - ny) for px, py in castles)
                    di = min(math.hypot(px - nx, py - ny) for px, py in inns)
                    ok = dv >= mind and dg >= mind and dt >= D_TOWN + 0.01 and dca >= D_CASTLE + 0.01 and (fn != FILES[0] or di >= D_INN) \
                        and droad(nx, ny) >= D_PAINT and nav.get((int(round(nx / 2)) * 2, int(round(ny / 2)) * 2), False)
                    if ok: best = (nx, ny, dc, dv, dg, di); break
                if best: break
            if not best:
                print('%s: %s %s (%s) - BRAK miejsca obok %s, zostaje' % (fn, r['id'], r['name'], r['uid'], src)); continue
            nx, ny, dc, dv, dg, di = best
            print('%s: %s %s (%s) %.2f,%.2f -> %.2f,%.2f (przesuniecie %.2f; od %s %.2f, wioska %.2f, wies gry %.2f, zajazd %.2f)' % (
                fn, r['id'], r['name'], r['uid'], x, y, nx, ny, math.hypot(nx - x, ny - y), src, dc, dv, dg, di))
            r['x'], r['y'] = '%.2f' % nx, '%.2f' % ny; pts[k] = (nx, ny)
            lines[li] = '\t'.join(r[h] for h in hdr)
            moved.append(r['id'])
        out = []
        for l in lines:
            if l.startswith('# scene_xml_crc:'):
                if l.strip() != '# scene_xml_crc: ' + OLD_CRC and not l.startswith('# scene_xml_crc: 0'):
                    sys.exit('BLAD %s: nieoczekiwany naglowek %s' % (fn, l))
                # r343 (recenzja): CRC 8.1.8 zostaje - te same dane sluza obu mapom (stara: po CRC sceny, 8.1.2: po terrain.bin), kolejnosc wgrania DLL / mapy obojetna
                out.append('# scene_xml_crc: ' + OLD_CRC)
                out.append('# scene_terrain_size: %d (ROT 8.1.2: the mod takes this file when ROT-Map terrain.bin has this size - scene_xml_crc above = ROT 8.1.8 scene - '
                           'the same data serve both maps (8.1.2 game scene CRC not known outside the game); tools/geo/rot812_wioski.py, villages moved: %s)'
                           % (TERRAIN_SIZE, ','.join(moved) or 'none'))
                continue
            if l.startswith('# scene_terrain_size:'): continue
            out.append(l)
        crc1 = layout_crc(read_rows(out, hdr))
        print('%s: wiosek %d, przesuniete %d, crc ukladu %08X -> %08X%s' % (fn, len(rows), len(moved), crc0, crc1, '' if crc0 == crc1 else ' - BLAD'))
        if crc0 != crc1: sys.exit(1)
        if not a.dry:
            open(path, 'w', encoding='utf-8', newline='\n').write('\n'.join(out))


def read_rows(lines, hdr):
    return [(0, dict(zip(hdr, l.split('\t')))) for l in lines if l and not l.startswith('#')]


if __name__ == '__main__':
    main()
