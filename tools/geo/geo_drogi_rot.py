# -*- coding: utf-8 -*-
# geo_drogi_rot.py - DROGI MALOWANE ROT jako lamane (jedn. mapy) prosto z plikow sceny mapy (TYLKO ODCZYT plikow gry).
#
# Uruchomienie (python -I, numpy + lz4 + Pillow):
#   python -I tools/geo/geo_drogi_rot.py [--gra "C:\...\Mount & Blade II Bannerlord" | --mapa "...\ROT-Map\SceneObj\Main_map"] [--wyj tools/geo/dane/arm_geo_drogi_rot.tsv] [--prog 48]
#
# Skad drogi: ROT nie ma drog jako encji ani sciezek sceny (73 <path> w Main_map/scene.xscene to rzeki, fale morza i dzwieki;
# decale decal_*_path_a/b to uliczki w obrazkach osad, +-5 jedn. od srodka osady). Drogi miedzy osadami sa FARBA TERENU: warstwa
# <layer name="road"> (slot 13, tekstura vtstt_ground_roads_e_d) w scene.xscene, wagi warstw w terrain.bin (blok WGHT).
# W grze silnik nie podaje wag warstw mapy (Scene ma tylko GetTerrainPhysicsMaterialIndexData, a mapa wczytuje sie z
# UsePhysicsMaterials = false i InvalidateTerrainPhysicsMaterials - SandBox MapScene.Load), wiec lamane liczymy tutaj, a sonda
# geografii Armoury (MapGeoProbe.cs) czyta wynik i sprawdza kazdy punkt w grze (siatka ruchu, typ terenu, wysokosc).
#
# Format terrain.bin (ZGR6RTRN, wersja 2; rozpracowane 07.10 w a-teren, sprawdzone ponownie 16.10): naglowek 12 B, tabela 5 blokow
# (tag, 1, przesuniecie, rozmiar, rozmiar po rozpakowaniu) - MIDX, HGHT, NRML, WGHT, PHYM. Blok (poza MIDX) = 1024 B tabeli 256
# przesuniec wezlow w danych po rozpakowaniu + naglowek 40 B (3, 0, rozmiar_po u64, rozmiar_przed u64, ...) + strumien LZ4.
# Wezel n = iy*16 + ix (100 x 100 jedn.), obraz 257 x 257 (wspolna krawedz z sasiadem), wiersz obrazu = y.
# WGHT: 16 slotow, kazdy: u32 obecny (0/1) [+ u32 rozmiar + PNG 8 bit]. Slot = kolejnosc <layer> w scenie.
# Orientacja sprawdzona: warstwa SEA zgadza sie z morzem siatki ruchu w 95.5% (inne orientacje 51-68%).
#
# Lamane: waga warstwy road > prog (48/255), domkniecie 1 probki (8 sasiadow), scienianie Zhang-Suen, sledzenie szkieletu,
# Douglas-Peucker 0.2 jedn., ostrogi < 2 jedn. przy koncu usuniete. Kolumny: droga (id lamanej), seq, x, y, typ (nazwa warstwy -
# ROT ma JEDEN material drogi, wiec bez podzialu na bity / polny; podzial daje szer i waga), szer (szacowana szerokosc pasa farby
# w jedn.: probki z waga > prog w pasie 1.6 jedn. od szkieletu odcinka razy pole probki, przez dlugosc odcinka), waga (srednia
# waga farby 0..255 na szkielecie odcinka).
import argparse, hashlib, io, math, os, re, struct, sys, time

import numpy as np
import lz4.block
from PIL import Image

WORLD = 1600.0
TN = 4097
TSTEP = WORLD / (TN - 1)
NB8 = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]
_t0 = time.time()


def log(*a):
    print('[%6.1fs]' % (time.time() - _t0), *a, flush=True)


def chunks(b):
    o = 12
    ch = {}
    while o < 0x70:
        tag = b[o:o + 4].decode('ascii', 'replace')
        cnt, off, sz, ex = struct.unpack_from('<4I', b, o + 4)
        ch[tag] = (off, sz, ex)
        o += 20
    return ch


def blob(b, ch, tag):
    off, sz, ex = ch[tag]
    base = off + 0x400
    us = struct.unpack_from('<Q', b, base + 8)[0]
    d = lz4.block.decompress(b[base + 40:base + sz], uncompressed_size=us)
    tab = struct.unpack_from('<256I', b, off)
    return d, tab


def layers(scene_txt):
    i0 = scene_txt.index('<layers version')
    i1 = scene_txt.index('</layers>', i0)
    out = []
    for m in re.finditer(r'\n\t\t\t<layer name="([^"]*)">(.*?)\n\t\t\t</layer>', scene_txt[i0:i1], flags=re.S):
        dm = re.search(r'type="diffusemap" name="([^"]*)"', m.group(2))
        out.append((m.group(1), dm.group(1) if dm else ''))
    return out


def road_weights(b, slot):
    ch = chunks(b)
    d, tab = blob(b, ch, 'WGHT')
    R = np.zeros((TN, TN), np.uint8)
    nodes = 0
    for n in range(256):
        q = tab[n]
        end = tab[n + 1] if n < 255 else len(d)
        iy, ix = divmod(n, 16)
        for s in range(16):
            present = struct.unpack_from('<I', d, q)[0]
            q += 4
            if present:
                size = struct.unpack_from('<I', d, q)[0]
                q += 4
                if s == slot:
                    a = np.array(Image.open(io.BytesIO(d[q:q + size])))
                    R[iy * 256:iy * 256 + 257, ix * 256:ix * 256 + 257] = np.maximum(R[iy * 256:iy * 256 + 257, ix * 256:ix * 256 + 257], a)
                    nodes += 1
                q += size
        if q != end:
            raise SystemExit('BLAD: wezel %d - koniec danych %d, oczekiwany %d (inny format terrain.bin?)' % (n, q, end))
    return R, nodes


def shift(a, dy, dx, fill):
    b = np.full_like(a, fill)
    H, W = a.shape
    ys = slice(max(dy, 0), H + min(dy, 0)); yd = slice(max(-dy, 0), H + min(-dy, 0))
    xs = slice(max(dx, 0), W + min(dx, 0)); xd = slice(max(-dx, 0), W + min(-dx, 0))
    b[ys, xs] = a[yd, xd]
    return b


def zhang_suen(img):
    I = np.pad((img > 0).astype(np.uint8), 1)
    while True:
        changed = 0
        for step in (0, 1):
            P2 = I[:-2, 1:-1]; P3 = I[:-2, 2:]; P4 = I[1:-1, 2:]; P5 = I[2:, 2:]
            P6 = I[2:, 1:-1]; P7 = I[2:, :-2]; P8 = I[1:-1, :-2]; P9 = I[:-2, :-2]
            C = I[1:-1, 1:-1]
            nbr = [P2, P3, P4, P5, P6, P7, P8, P9]
            B = sum(x.astype(np.int32) for x in nbr)
            A = sum(((nbr[i] == 0) & (nbr[(i + 1) % 8] == 1)).astype(np.int32) for i in range(8))
            if step == 0:
                c1 = (P2 * P4 * P6) == 0; c2 = (P4 * P6 * P8) == 0
            else:
                c1 = (P2 * P4 * P8) == 0; c2 = (P2 * P6 * P8) == 0
            m = (C == 1) & (B >= 2) & (B <= 6) & (A == 1) & c1 & c2
            k = int(m.sum())
            if k:
                I[1:-1, 1:-1][m] = 0
                changed += k
        if not changed:
            break
    return I[1:-1, 1:-1].astype(bool)


def trace_skeleton(sk):
    ys, xs = np.nonzero(sk)
    P = set(zip(ys.tolist(), xs.tolist()))
    adj = {}
    for (y, x) in P:
        nb = []
        for dy, dx in NB8:
            q = (y + dy, x + dx)
            if q not in P:
                continue
            if dy and dx and (((y + dy, x) in P) or ((y, x + dx) in P)):
                continue
            nb.append(q)
        adj[(y, x)] = nb
    nodepix = [p for p, nb in adj.items() if len(nb) != 2]
    nid = {p: i for i, p in enumerate(nodepix)}
    nodes = [[p[0], p[1]] for p in nodepix]
    used = set()
    edges = []

    def walk(p, q):
        path = [p, q]
        used.add((p, q)); used.add((q, p))
        cur = q
        while cur not in nid:
            nxt = [r for r in adj[cur] if (cur, r) not in used]
            if not nxt:
                break
            r = nxt[0]
            used.add((cur, r)); used.add((r, cur))
            path.append(r)
            cur = r
            if cur == p:
                break
        return path

    for p in nodepix:
        for q in adj[p]:
            if (p, q) in used:
                continue
            path = walk(p, q)
            e = path[-1]
            if e not in nid:
                nid[e] = len(nodes); nodes.append([e[0], e[1]])
            edges.append((nid[p], nid[e], path))
    for p, nb in adj.items():          # petle bez wezlow
        for q in nb:
            if (p, q) in used:
                continue
            if p not in nid:
                nid[p] = len(nodes); nodes.append([p[0], p[1]])
            path = walk(p, q)
            e = path[-1]
            if e not in nid:
                nid[e] = len(nodes); nodes.append([e[0], e[1]])
            edges.append((nid[p], nid[e], path))
    return nodes, edges


def douglas_peucker(pts, eps):
    pts = np.asarray(pts, float)
    if len(pts) < 3:
        return pts.tolist()
    keep = np.zeros(len(pts), bool)
    keep[0] = keep[-1] = True
    st = [(0, len(pts) - 1)]
    while st:
        i, j = st.pop()
        if j <= i + 1:
            continue
        a, b = pts[i], pts[j]
        ab = b - a
        L = math.hypot(ab[0], ab[1])
        seg = pts[i + 1:j]
        if L < 1e-9:
            dd = np.hypot(seg[:, 0] - a[0], seg[:, 1] - a[1])
        else:
            dd = np.abs(ab[0] * (seg[:, 1] - a[1]) - ab[1] * (seg[:, 0] - a[0])) / L
        k = int(dd.argmax())
        if dd[k] > eps:
            keep[i + 1 + k] = True
            st.append((i, i + 1 + k)); st.append((i + 1 + k, j))
    return pts[keep].tolist()


def plen(pl):
    return sum(math.hypot(pl[i + 1][0] - pl[i][0], pl[i + 1][1] - pl[i][1]) for i in range(len(pl) - 1))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--gra', default=r'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord')
    here = os.path.dirname(os.path.abspath(__file__))
    ap.add_argument('--wyj', default=os.path.join(here, 'dane', 'arm_geo_drogi_rot.tsv'))
    ap.add_argument('--prog', type=int, default=48)
    ap.add_argument('--mapa', default='', help='folder Main_map wprost (np. rozpakowana paczka ROT 8.1.2: .../ROT-Map/SceneObj/Main_map); pierwszenstwo przed --gra')
    a = ap.parse_args()
    sys.stdout.reconfigure(encoding='utf-8')
    gm = a.mapa or os.path.join(a.gra, 'Modules', 'ROT-Map', 'SceneObj', 'Main_map')
    tb = os.path.join(gm, 'terrain.bin')
    sx = os.path.join(gm, 'scene.xscene')
    b = open(tb, 'rb').read()
    if b[:8] != b'ZGR6RTRN':
        raise SystemExit('BLAD: %s nie zaczyna sie od ZGR6RTRN' % tb)
    md5 = hashlib.md5(b).hexdigest()
    txt = open(sx, encoding='utf-8', errors='replace').read()
    L = layers(txt)
    slot = next((i for i, (n, _) in enumerate(L) if n.lower() == 'road'), -1)
    if slot < 0:
        raise SystemExit('BLAD: brak warstwy "road" w %s (warstwy: %s)' % (sx, [n for n, _ in L]))
    log('warstwy terenu:', ', '.join('%d %s' % (i, n) for i, (n, _) in enumerate(L)), '| droga = slot', slot, L[slot][1])
    R, nodes = road_weights(b, slot)
    m = R > a.prog
    log('waga drogi > %d: %d probek z %d (wezlow z warstwa drogi: %d)' % (a.prog, int(m.sum()), m.size, nodes))
    md = m.copy()
    for dy, dx in NB8:
        md |= shift(m, dy, dx, False)
    sk = zhang_suen(md)
    log('szkielet: %d probek' % int(sk.sum()))
    nodes_, edges = trace_skeleton(sk)
    # szerokosc pasa farby: probki maski (waga > prog) w pasie 4 probek (1.6 jedn.) od szkieletu odcinka, przez dlugosc odcinka
    lab = np.full(m.shape, -1, np.int32)
    for i, (na, nb, path) in enumerate(edges):
        ys = np.array([p[0] for p in path]); xs = np.array([p[1] for p in path])
        lab[ys, xs] = i
    for _ in range(4):
        for dy, dx in NB8:
            sh = shift(lab, dy, dx, -1)
            put = (lab < 0) & (sh >= 0)
            lab[put] = sh[put]
    area = np.bincount(lab[m & (lab >= 0)], minlength=len(edges)) * TSTEP * TSTEP
    deg = {}
    segs = []
    for i, (na, nb, path) in enumerate(edges):
        pts = [(x * TSTEP, y * TSTEP) for y, x in path]
        ys = np.array([p[0] for p in path]); xs = np.array([p[1] for p in path])
        w = float(R[ys, xs].mean())
        pl = douglas_peucker(pts, 0.2)
        L_ = plen(pl)
        segs.append(dict(a=na, b=nb, pts=pl, len=L_, w=w, szer=(area[i] / L_) if L_ > 0.05 else 0.0))
        deg[na] = deg.get(na, 0) + 1
        deg[nb] = deg.get(nb, 0) + 1
    kept = [s for s in segs if not (s['len'] < 2.0 and (deg[s['a']] == 1 or deg[s['b']] == 1))]
    tot = sum(s['len'] for s in kept)
    log('lamane: %d (usuniete ostrogi < 2 jedn.: %d), dlugosc %.0f jedn.' % (len(kept), len(segs) - len(kept), tot))
    os.makedirs(os.path.dirname(os.path.abspath(a.wyj)), exist_ok=True)
    rows = 0
    with open(a.wyj, 'w', encoding='utf-8', newline='\n') as f:
        f.write('# Armoury: drogi malowane ROT (farba terenu, warstwa "%s" slot %d, %s) z terrain.bin - generator tools/geo/geo_drogi_rot.py; nie edytowac\n' % (L[slot][0], slot, L[slot][1]))
        f.write('# terrain_md5: %s (rozmiar %d B)\n' % (md5, len(b)))
        f.write('# prog_wagi: %d; domkniecie 1 probki; scienianie Zhang-Suen; Douglas-Peucker 0.2 jedn.; probka %.6f jedn.\n' % (a.prog, TSTEP))
        f.write('# lamanych: %d, dlugosc %.1f jedn.\n' % (len(kept), tot))
        f.write('# warstwy_terenu: %s\n' % ','.join(n for n, _ in L))   # kolejnosc slotow WGHT = indeks warstwy (sonda GEO: teren_natywny)
        f.write('#droga\tseq\tx\ty\ttyp\tszer\twaga\n')
        for i, s in enumerate(kept):
            for j, (x, y) in enumerate(s['pts']):
                f.write('%d\t%d\t%.2f\t%.2f\t%s\t%.2f\t%.0f\n' % (i + 1, j, x, y, L[slot][0], s['szer'], s['w']))
                rows += 1
    log('zapisane: %s (%d wierszy)' % (a.wyj, rows))


if __name__ == '__main__':
    main()
