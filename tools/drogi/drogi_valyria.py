# -*- coding: utf-8 -*-
# drogi_valyria.py - plik danych Armoury/ModuleData/arm_map_roads_valyria.tsv (m96-valyria, Jeff 20.10: "Rob drogi Valyrii i trakty Essos"):
# RODZAJ odcinkow drog malowanych ROT (arm_map_roads_geo.tsv): V = droga Valyrii, C = szlak karawan; reszta (bez wiersza) = zwykly trakt.
#
# Uruchomienie (python -I, sama biblioteka standardowa):
#   python -I tools/drogi/drogi_valyria.py [--drogi Armoury/ModuleData/arm_map_roads_geo.tsv] [--osady .../ROT-Map/ModuleData/settlements.xml]
#     [--wyj Armoury/ModuleData/arm_map_roads_valyria.tsv]
#
# LORE (awoiaf.westeros.org Valyrian roads / Demon road; audyt docs/audyt-2026-10-09/08-drogi-wioski-widok.md rozdz. 2): drogi Valyrii z topionego kamienia,
# proste, pol stopy nad gruntem, laczyly osrodki handlu; znane trasy: demon road Volantis - Mantarys - (ruiny Bhorash) - Meereen z odnoga do Tolos;
# Pentos - Ghoyan Drohe - Norvos - Qohor. Zadnej innej trasy zrodlo nie potwierdza (Selhorys - Volantis: brak potwierdzenia - zwykly trakt).
# REGULA V: najkrotsza trasa po sieci farby ROT miedzy kolejnymi miastami trasy (Dijkstra po punktach lamanych; konce lamanych laczone z najblizszym
# punktem innej lamanej do JOIN_R jedn. - jak siec AI drog w AiRoadRoutes.cs). Odcinki na trasie = V (dokladnie te odcinki, nie cale lamane).
# REGULA C: odcinek farby (nie V), ktorego srodek ma najblizsza twierdze (miasto / zamek) kultury glebi Essos - stepy Dothrakow (khuzait), Sarnor,
# Ibben, Yi Ti, Qarth (DEEP) - lore nie zna tam drog, tylko szlaki karawan. Reszta Essos (Wolne Miasta, Rhoyne, Zatoka Niewolnicza, Valyria) i
# caly Westeros - zwykly trakt (bez wiersza).
# Wyjscie: naglowek (wersja, roads_md5 = md5 tekstu pliku drog bez '\r' - straznik w grze: inny plik drog = rodzaje nieczynne) i wiersze
# road, seq0, seq1, kind, route, road2, seq2: V / C - odcinki lamanej road od punktu seq0 do seq1 (odcinek k laczy punkt k-1 z k, k w (seq0, seq1]),
# road2 = seq2 = -1; B (most Valyrii przez przerwe farby do GAP_R jedn.) - prosty odcinek Valyrii od punktu (road, seq0) do punktu (road2, seq2).
import argparse, hashlib, heapq, math, os, re, sys
from collections import defaultdict

VERSION = 1
JOIN_R = 0.6
GAP_R = 8.0          # przerwa farby (pod miastem, przy brodzie - lamane sondy urywaja sie) do tylu jedn. = MOST trasy Valyrii (odcinek prosty)
GAP_COST = 1.5       # most w Dijkstrze drozszy od farby (trasa woli farbe)
NEAR_CITY = 25.0
ROUTES = [
    ('demon_road', ['Volantis', 'Mantarys', 'Meereen']),
    ('demon_road_tolos', ['Mantarys', 'Tolos']),
    ('pentos_ghoyan_drohe', ['Pentos', 'Ghoyan Drohe']),
    ('ghoyan_drohe_norvos_qohor', ['Ghoyan Drohe', 'Norvos', 'Qohor']),
]
DEEP = {'khuzait', 'sarnor', 'ibbenese', 'yiti', 'qartheen'}


def read_roads(path):
    raw = open(path, 'rb').read().decode('utf-8')
    md5 = hashlib.md5(raw.replace('\r', '').encode('utf-8')).hexdigest()
    lines = defaultdict(list)
    for ln in raw.replace('\r', '').split('\n'):
        if not ln or ln[0] == '#':
            continue
        c = ln.split('\t')
        lines[int(c[0])].append((int(c[1]), float(c[2]), float(c[3])))
    for r in lines:
        lines[r].sort()
    return md5, lines


def read_forts(path):
    t = open(path, encoding='utf-8', errors='replace').read()
    out = []
    for m in re.finditer(r'<Settlement\b([^>]*)>(.*?)</Settlement>', t, re.S):
        a, body = m.group(1), m.group(2)
        if '<Town' not in body:
            continue
        g = lambda k: (re.search(r'\b' + k + r'="([^"]*)"', a) or [None, ''])[1]
        name = re.sub(r'\{=[^}]*\}', '', g('name'))
        out.append((name, float(g('posX')), float(g('posY')), g('culture').replace('Culture.', '')))
    return out


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    ap = argparse.ArgumentParser()
    ap.add_argument('--drogi', default=os.path.join(here, '..', '..', 'Armoury', 'ModuleData', 'arm_map_roads_geo.tsv'))
    ap.add_argument('--osady', default=r'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\ROT-Map\ModuleData\settlements.xml')
    ap.add_argument('--wyj', default=os.path.join(here, '..', '..', 'Armoury', 'ModuleData', 'arm_map_roads_valyria.tsv'))
    a = ap.parse_args()
    md5, L = read_roads(a.drogi)
    forts = read_forts(a.osady)
    byname = {f[0]: f for f in forts}
    # wierzcholki: (road, idx w lamanej)
    V = []
    vid = {}
    for r in sorted(L):
        for i, (s, x, y) in enumerate(L[r]):
            vid[(r, i)] = len(V)
            V.append((r, i, s, x, y))
    adj = defaultdict(list)
    for r in L:
        p = L[r]
        for i in range(1, len(p)):
            u, v = vid[(r, i - 1)], vid[(r, i)]
            d = math.hypot(p[i][1] - p[i - 1][1], p[i][2] - p[i - 1][2])
            adj[u].append((v, d)); adj[v].append((u, d))
    # rozstaje: konce lamanych do najblizszego punktu innej lamanej (komorki 2 jedn.)
    cell = defaultdict(list)
    for k, (r, i, s, x, y) in enumerate(V):
        cell[(int(x // 2), int(y // 2))].append(k)
    joins = 0
    for r in L:
        for i in (0, len(L[r]) - 1):
            k = vid[(r, i)]
            x, y = V[k][3], V[k][4]
            best, bd = -1, GAP_R * GAP_R
            gc = int(GAP_R // 2) + 1
            for cx in range(int(x // 2) - gc, int(x // 2) + gc + 1):
                for cy in range(int(y // 2) - gc, int(y // 2) + gc + 1):
                    for j in cell.get((cx, cy), ()):
                        if V[j][0] == r:
                            continue
                        d2 = (V[j][3] - x) ** 2 + (V[j][4] - y) ** 2
                        if d2 <= bd:
                            bd, best = d2, j
            if best >= 0:
                d = math.sqrt(bd)
                w = d if d <= JOIN_R else d * GAP_COST
                adj[k].append((best, w)); adj[best].append((k, w)); joins += 1

    def nearest(x, y):
        return min(range(len(V)), key=lambda k: (V[k][3] - x) ** 2 + (V[k][4] - y) ** 2)

    def dijkstra(s, t):
        dist = {s: 0.0}
        prev = {}
        h = [(0.0, s)]
        while h:
            d, u = heapq.heappop(h)
            if u == t:
                break
            if d > dist[u]:
                continue
            for v, w in adj[u]:
                nd = d + w
                if nd < dist.get(v, 1e18):
                    dist[v] = nd; prev[v] = u; heapq.heappush(h, (nd, v))
        if t not in dist:
            return None, None
        path = [t]
        while path[-1] != s:
            path.append(prev[path[-1]])
        return dist[t], path[::-1]

    bridges = {}     # (wierzcholek, wierzcholek) -> trasa: przerwa farby na trasie Valyrii (odcinek prosty dokladany w grze jako droga Valyrii)
    seg_kind = {}    # (road, k) -> (kind, route); odcinek k = punkty k-1..k lamanej road (indeks w lamanej)
    notes = []
    for route, cities in ROUTES:
        for c0, c1 in zip(cities, cities[1:]):
            f0, f1 = byname[c0], byname[c1]
            s, t = nearest(f0[1], f0[2]), nearest(f1[1], f1[2])
            ds = math.hypot(V[s][3] - f0[1], V[s][4] - f0[2]); dt = math.hypot(V[t][3] - f1[1], V[t][4] - f1[2])
            if ds > NEAR_CITY or dt > NEAR_CITY:
                notes.append('%s: %s - %s BRAK farby przy miescie (%.1f / %.1f jedn.)' % (route, c0, c1, ds, dt)); continue
            d, path = dijkstra(s, t)
            if path is None:
                notes.append('%s: %s - %s BEZ POLACZENIA po farbie' % (route, c0, c1)); continue
            n = 0; nb = 0; lb = 0.0
            for u, v in zip(path, path[1:]):
                if V[u][0] == V[v][0] and abs(V[u][1] - V[v][1]) == 1:
                    r = V[u][0]; k = max(V[u][1], V[v][1])
                    if (r, k) not in seg_kind:
                        seg_kind[(r, k)] = ('V', route); n += 1
                elif V[u][0] != V[v][0]:
                    g = math.hypot(V[u][3] - V[v][3], V[u][4] - V[v][4])
                    if g > JOIN_R:
                        key = (min(u, v), max(u, v))
                        if key not in bridges:
                            bridges[key] = route; nb += 1; lb += g
            notes.append('%s: %s - %s trasa %.1f jedn. (prosta %.1f), odcinkow farby %d, mostow przez przerwy farby %d (%.1f jedn.), miasto od farby %.1f / %.1f' % (
                route, c0, c1, d, math.hypot(f1[1] - f0[1], f1[2] - f0[2]), n, nb, lb, ds, dt))
    # C: glab Essos (najblizsza twierdza kultury DEEP), poza V
    def cult(x, y):
        return min(forts, key=lambda f: (f[1] - x) ** 2 + (f[2] - y) ** 2)[3]
    for r in L:
        p = L[r]
        for k in range(1, len(p)):
            if (r, k) in seg_kind:
                continue
            if cult((p[k][1] + p[k - 1][1]) / 2, (p[k][2] + p[k - 1][2]) / 2) in DEEP:
                seg_kind[(r, k)] = ('C', 'deep_essos')
    # wiersze: ciagle przedzialy tego samego rodzaju i trasy w lamanej
    rows = []
    lenk = defaultdict(float)
    for r in sorted(L):
        p = L[r]
        k = 1
        while k < len(p):
            if (r, k) not in seg_kind:
                k += 1; continue
            kind, route = seg_kind[(r, k)]
            k0 = k
            while k + 1 < len(p) and seg_kind.get((r, k + 1)) == (kind, route):
                k += 1
            for q in range(k0, k + 1):
                lenk[kind] += math.hypot(p[q][1] - p[q - 1][1], p[q][2] - p[q - 1][2])
            rows.append((r, p[k0 - 1][0], p[k][0], kind, route))
            k += 1
    total = sum(math.hypot(L[r][k][1] - L[r][k - 1][1], L[r][k][2] - L[r][k - 1][2]) for r in L for k in range(1, len(L[r])))
    out = []
    out.append('# Armoury: kinds of the painted roads of the Realm of Thrones map (m96-valyria): V = Valyrian road (fused stone), C = caravan trail (deep Essos).')
    out.append('# Segments without a row are ordinary roads. Generated by tools/drogi/drogi_valyria.py from arm_map_roads_geo.tsv and ROT-Map settlements.xml - do not edit.')
    out.append('# version: %d' % VERSION)
    out.append('# roads_md5: %s' % md5)
    out.append('# routes (lore - awoiaf Valyrian roads): ' + '; '.join(rt + ' = ' + ' - '.join(cs) for rt, cs in ROUTES))
    out.append('# caravan trails: segments whose nearest town / castle has a deep Essos culture (' + ', '.join(sorted(DEEP)) + ')')
    out.append('# rows: %d, valyrian: %.1f, caravan: %.1f, all painted: %.1f map units' % (len(rows), lenk['V'], lenk['C'], total))
    for nline in notes:
        out.append('# ' + nline)
    out.append('# bridges (kind B): %d, %.1f map units - gaps of the paint on a Valyrian route (the probe polylines break under towns and at fords); in the game'
               ' a straight Valyrian segment from point (road, seq0) to point (road2, seq2)'
               % (len(bridges), sum(math.hypot(V[u][3] - V[v][3], V[u][4] - V[v][4]) for u, v in bridges)))
    out.append('#road\tseq0\tseq1\tkind\troute\troad2\tseq2')
    for r, s0, s1, kind, route in rows:
        out.append('%d\t%d\t%d\t%s\t%s\t-1\t-1' % (r, s0, s1, kind, route))
    for (u, v), route in sorted(bridges.items()):
        out.append('%d\t%d\t%d\tB\t%s\t%d\t%d' % (V[u][0], V[u][2], V[u][2], route, V[v][0], V[v][2]))
    with open(a.wyj, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(out) + '\n')
    print('\n'.join(out[:13 + len(notes)]))
    print('rozstajow %d, wierszy %d -> %s' % (joins, len(rows), a.wyj))


if __name__ == '__main__':
    main()
