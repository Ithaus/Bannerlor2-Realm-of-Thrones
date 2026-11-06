# -*- coding: utf-8 -*-
# geo_czytaj.py - czytnik plikow sondy geografii Armoury (MapGeoProbe.cs, NOC-GEO): geo-siatka.tsv (G1), geo-trasy.tsv + geo-trasy-punkty.tsv (G2),
# geo-drogi.tsv (G3), opcjonalnie geo-teren.tsv. Sama biblioteka standardowa (bez numpy).
#
# Uzycie:
#   python -I tools/geo/geo_czytaj.py [katalog Logs\<sesja>]            # bez katalogu: najnowszy Modules\Armoury\Logs\<sesja> z geo-siatka.tsv
#          [--pustka-wies 20] [--pustka-wioska 10]                       # "pusta ziemia": lad przejezdny dalej niz tyle od wsi gry I od wioski ksiegi
#          [--dzien 0]                                                   # jedn. mapy na dzien marszu: > 0 = szacunek miejsc na zajazdy przy traktach
#          [--top 15] [--gra "C:\...\Mount & Blade II Bannerlord"]
# Co wypisuje: udzial ladu (przejezdny / zamkniety / morze), wyspy siatki, typy terenu; pusta ziemia wedlug krolestwa i kultury najblizszej twierdzy
# (region); pokrycie ladu drogami malowanymi; dlugosci tras gry (wedlug rodzaju, polaczenia, kretosc, udzial drogi), najdluzsze trasy, polaczenia
# tylko morzem; drogi malowane (lamane, dlugosc, lad pod droga); warstwa terenu z silnika (gdy jest plik).
# Funkcje load_* mozna wolac z innych skryptow (wiersz = dict kolumna -> napis; "-" = brak).
import argparse, collections, csv, math, os, statistics, sys

csv.field_size_limit(1 << 24)
FILES = ('geo-siatka.tsv', 'geo-trasy.tsv', 'geo-trasy-punkty.tsv', 'geo-drogi.tsv', 'geo-teren.tsv')


def rows(path):
    with open(path, encoding='utf-8', newline='') as f:
        r = csv.reader(f, delimiter='\t', quoting=csv.QUOTE_NONE)
        head = next(r, None)
        if head is None:
            return
        for line in r:
            if not line:
                continue
            yield dict(zip(head, line))


def num(v):
    if v is None or v == '-' or v == '':
        return None
    try:
        return float(v)
    except ValueError:
        return None


def load_grid(d):
    return list(rows(os.path.join(d, 'geo-siatka.tsv')))


def load_routes(d):
    return list(rows(os.path.join(d, 'geo-trasy.tsv')))


def load_roads(d):
    return list(rows(os.path.join(d, 'geo-drogi.tsv')))


def pct(a, n):
    return '%.1f%%' % (100.0 * a / n) if n else '-'


def q(vals, p):
    if not vals:
        return None
    s = sorted(vals)
    k = (len(s) - 1) * p
    i = int(math.floor(k)); j = min(len(s) - 1, i + 1)
    return s[i] + (s[j] - s[i]) * (k - i)


def fmt(v, f='%.1f'):
    return '-' if v is None else f % v


def latest_dir(game):
    root = os.path.join(game, 'Modules', 'Armoury', 'Logs')
    if not os.path.isdir(root):
        return None
    best = None
    for n in os.listdir(root):
        p = os.path.join(root, n)
        g = os.path.join(p, 'geo-siatka.tsv')
        if os.path.isfile(g):
            t = os.path.getmtime(g)
            if best is None or t > best[0]:
                best = (t, p)
    return best[1] if best else None


def grid_step(g):
    xs = sorted({float(r['x']) for r in g[:5000]})
    ds = [b - a for a, b in zip(xs, xs[1:]) if b - a > 1e-6]
    return min(ds) if ds else 2.0


def report_grid(g, a):
    print('== G1 SIATKA (geo-siatka.tsv): %d punktow' % len(g))
    if not g:
        return
    st = grid_step(g)
    cell = st * st
    kinds = collections.Counter(r['sciana'] for r in g)
    n = len(g)
    land = kinds.get('L', 0) + kinds.get('X', 0)
    print('  krok %.2f jedn. (komorka %.2f jedn.^2); lad przejezdny %s, lad zamkniety (np. Mountain - pasy rzek, urwiska) %s, morze %s, poza siatka %s'
          % (st, cell, pct(kinds.get('L', 0), n), pct(kinds.get('X', 0), n), pct(kinds.get('M', 0), n), pct(kinds.get('-', 0), n)))
    print('  udzial ladu (przejezdny + zamkniety) %s; ladu przejezdnego %.0f jedn.^2' % (pct(land, n), kinds.get('L', 0) * cell))
    nav = [r for r in g if r['sciana'] == 'L']
    tt = collections.Counter(r['teren'] for r in nav)
    print('  teren ladu przejezdnego: ' + ', '.join('%s %s' % (k, pct(v, len(nav))) for k, v in tt.most_common(8)))
    isl = collections.Counter(r['wyspa'] for r in nav)
    print('  wyspy siatki (FaceIslandIndex) ladu przejezdnego: %d; najwieksze: %s' % (len(isl), ', '.join('%s %.0f jedn.^2' % (k, v * cell) for k, v in isl.most_common(6))))
    # pusta ziemia
    A, B = a.pustka_wies, a.pustka_wioska
    has_side = any(r['d_wioska'] != '-' for r in nav[:20000])

    def empty(r):
        dv = num(r['d_wies_gry'])
        ds = num(r['d_wioska'])
        return (dv is None or dv > A) and (not has_side or ds is None or ds > B)

    emp = [r for r in nav if empty(r)]
    print('  PUSTA ZIEMIA (lad przejezdny > %.0f jedn. od wsi gry%s): %d komorek = %.0f jedn.^2 (%s ladu przejezdnego)'
          % (A, (' i > %.0f od wioski ksiegi' % B) if has_side else ' (brak danych wiosek ksiegi)', len(emp), len(emp) * cell, pct(len(emp), len(nav))))
    for key, label in (('krolestwo', 'krolestwo najblizszej twierdzy'), ('kultura', 'kultura najblizszej twierdzy')):
        tot = collections.Counter(r[key] for r in nav)
        ec = collections.Counter(r[key] for r in emp)
        print('  -- wedlug: %s (komorki pustki / lad przejezdny regionu, udzial; mediana odl. od drogi malowanej w pustce)' % label)
        dr = collections.defaultdict(list)
        for r in emp:
            v = num(r['d_droga'])
            if v is not None:
                dr[r[key]].append(v)
        for k, v in ec.most_common(a.top):
            md = q(dr.get(k, []), 0.5)
            print('     %-28s %7d / %7d  %6s   %.0f jedn.^2   droga mediana %s' % (k, v, tot[k], pct(v, tot[k]), v * cell, fmt(md)))
    far = [num(r['d_twierdza']) for r in emp if num(r['d_twierdza']) is not None]
    if far:
        print('  pustka - odleglosc do twierdzy: mediana %s, p90 %s jedn.' % (fmt(q(far, 0.5)), fmt(q(far, 0.9))))
    # drogi malowane
    dd = [num(r['d_droga']) for r in nav]
    known = [v for v in dd if v is not None]
    if known:
        cov = ', '.join('<= %d jedn. %s' % (t, pct(sum(1 for v in known if v <= t), len(nav))) for t in (2, 5, 10, 20))
        print('  lad przejezdny blisko drogi malowanej: %s (dalej niz 60 jedn. albo brak drog: %s)' % (cov, pct(len(nav) - len(known), len(nav))))
    else:
        print('  odleglosc do drogi malowanej: brak (sonda bez pliku arm_geo_drogi_rot.tsv)')
    dvs = [num(r['d_wies_gry']) for r in nav if num(r['d_wies_gry']) is not None]
    dss = [num(r['d_wioska']) for r in nav if num(r['d_wioska']) is not None]
    print('  lad przejezdny - odl. do wsi gry: mediana %s, p90 %s; do wioski ksiegi: mediana %s, p90 %s jedn.'
          % (fmt(q(dvs, 0.5)), fmt(q(dvs, 0.9)), fmt(q(dss, 0.5)), fmt(q(dss, 0.9))))


def report_routes(rt, a):
    print('== G2 TRASY (geo-trasy.tsv): %d' % len(rt))
    if not rt:
        return
    kinds = collections.Counter((r['rodzaj'], r['polaczenie']) for r in rt)
    for (k, p), v in sorted(kinds.items()):
        print('  %-18s %-13s %5d' % (k, p, v))
    for kind in ('twierdza-twierdza', 'twierdza-wies'):
        land = [r for r in rt if r['rodzaj'] == kind and r['polaczenie'] == 'lad']
        L = [num(r['dl_trasy']) for r in land if num(r['dl_trasy']) is not None]
        if not L:
            continue
        kr = [num(r['kretosc']) for r in land if num(r['kretosc']) is not None]
        dg = [(num(r['dl_trasy']), num(r['dl_gry'])) for r in land if num(r['dl_gry']) is not None and num(r['dl_trasy'])]
        ud = [num(r['udzial_drogi']) for r in land if num(r['udzial_drogi']) is not None]
        print('  %s (lad, %d): dlugosc min %s, p25 %s, mediana %s, p75 %s, max %s, srednia %s jedn.; kretosc mediana %s, p90 %s'
              % (kind, len(L), fmt(min(L)), fmt(q(L, .25)), fmt(q(L, .5)), fmt(q(L, .75)), fmt(max(L)), fmt(statistics.mean(L)), fmt(q(kr, .5), '%.2f'), fmt(q(kr, .9), '%.2f')))
        if dg:
            ratio = [t / g for t, g in dg if g > 0]
            print('     trasa sondy / odleglosc gry (pamiec drog): mediana %s, p10 %s, p90 %s' % (fmt(q(ratio, .5), '%.3f'), fmt(q(ratio, .1), '%.3f'), fmt(q(ratio, .9), '%.3f')))
        if ud:
            print('     udzial punktow trasy na drodze malowanej (<= 1 jedn.): mediana %s, srednia %s; tras > 50%% po drodze: %d z %d'
                  % (fmt(q(ud, .5), '%.2f'), fmt(statistics.mean(ud), '%.2f'), sum(1 for v in ud if v > 0.5), len(ud)))
        ter = collections.Counter()
        for r in land:
            if r['teren'] in ('', '-'):
                continue
            pts = num(r['punktow']) or 0
            for part in r['teren'].split(';'):
                if ':' in part:
                    k, s = part.rsplit(':', 1)
                    ter[k] += float(s) * pts
        tot = sum(ter.values())
        if tot:
            print('     teren po drodze: ' + ', '.join('%s %s' % (k, pct(v, tot)) for k, v in ter.most_common(7)))
        if a.dzien > 0 and kind == 'twierdza-twierdza':
            slots = sum(int(x // a.dzien) for x in L)
            nb = [num(r['dl_trasy']) for r in land if r['sasiad_gry'] == '1' and num(r['dl_trasy'])]
            print('     ZAJAZDY (szacunek, %.1f jedn. = dzien marszu): tras dluzszych niz dzien %d z %d; miejsc co dzien marszu na wszystkich parach %d, na parach sasiadow gry %d'
                  % (a.dzien, sum(1 for x in L if x > a.dzien), len(L), slots, sum(int(x // a.dzien) for x in nb)))
    land = sorted((r for r in rt if r['polaczenie'] == 'lad' and num(r['dl_trasy'])), key=lambda r: -num(r['dl_trasy']))
    print('  najdluzsze trasy ladem:')
    for r in land[:a.top]:
        print('     %7s jedn. (prosto %6s, gra %6s)  %s %s -> %s %s  [%s]' % (r['dl_trasy'], r['prosta'], r['dl_gry'], r['od'], r['od_nazwa'], r['do'], r['do_nazwa'], r['rodzaj']))
    sea = [r for r in rt if r['polaczenie'] == 'morze']
    if sea:
        print('  tylko morzem (%d): %s' % (len(sea), '; '.join('%s -> %s %s' % (r['od_nazwa'], r['do_nazwa'], r['dl_morze']) for r in sea[:a.top])))
    none = [r for r in rt if r['polaczenie'] in ('brak', 'brak_sciany', 'lad_niepelna')]
    if none:
        print('  bez pelnej trasy (%d): %s' % (len(none), '; '.join('%s -> %s (%s)' % (r['od_nazwa'], r['do_nazwa'], r['polaczenie']) for r in none[:a.top])))


def report_roads(rd):
    print('== G3 DROGI MALOWANE (geo-drogi.tsv): %d punktow' % len(rd))
    if not rd:
        print('  brak (sonda bez pliku arm_geo_drogi_rot.tsv - patrz tools/geo/geo_drogi_rot.py)')
        return
    lines = collections.OrderedDict()
    for r in rd:
        lines.setdefault(r['droga'], []).append((float(r['x']), float(r['y'])))
    length = sum(sum(math.hypot(p[i + 1][0] - p[i][0], p[i + 1][1] - p[i][1]) for i in range(len(p) - 1)) for p in lines.values())
    nav = sum(1 for r in rd if r['nawig'] == 'tak')
    tt = collections.Counter(r['teren'] for r in rd)
    sz = [num(r['szer']) for r in rd if num(r['szer']) is not None]
    print('  lamanych %d, dlugosc %.0f jedn.; punkty na ladzie przejezdnym %s; teren: %s; szerokosc farby mediana %s jedn.'
          % (len(lines), length, pct(nav, len(rd)), ', '.join('%s %s' % (k, pct(v, len(rd))) for k, v in tt.most_common(6)), fmt(q(sz, .5), '%.2f')))
    xs = [float(r['x']) for r in rd]; ys = [float(r['y']) for r in rd]
    print('  zasieg: x %.0f..%.0f, y %.0f..%.0f' % (min(xs), max(xs), min(ys), max(ys)))


def report_native(d):
    p = os.path.join(d, 'geo-teren.tsv')
    if not os.path.isfile(p):
        return
    c = collections.Counter()
    n = 0
    for r in rows(p):
        c[(r['warstwa'], r['nazwa'])] += 1
        n += 1
    print('== geo-teren.tsv (warstwa terenu z silnika): %d punktow, %d wartosci: %s' % (n, len(c), ', '.join('%s %s %s' % (k[0], k[1], pct(v, n)) for k, v in c.most_common(16))))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('katalog', nargs='?')
    ap.add_argument('--gra', default=r'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord')
    ap.add_argument('--pustka-wies', type=float, default=20.0)
    ap.add_argument('--pustka-wioska', type=float, default=10.0)
    ap.add_argument('--dzien', type=float, default=0.0)
    ap.add_argument('--top', type=int, default=15)
    a = ap.parse_args()
    sys.stdout.reconfigure(encoding='utf-8')
    d = a.katalog or latest_dir(a.gra)
    if not d or not os.path.isdir(d):
        raise SystemExit('BLAD: brak katalogu z plikami sondy (podaj Logs\\<sesja>)')
    print('Katalog:', d)
    for f in FILES:
        p = os.path.join(d, f)
        if os.path.isfile(p):
            with open(p, encoding='utf-8') as fh:
                k = sum(1 for _ in fh) - 1
            print('  %-22s %9d wierszy  %8.1f MB' % (f, k, os.path.getsize(p) / 1e6))
        else:
            print('  %-22s brak' % f)
    if os.path.isfile(os.path.join(d, 'geo-siatka.tsv')):
        report_grid(load_grid(d), a)
    if os.path.isfile(os.path.join(d, 'geo-trasy.tsv')):
        report_routes(load_routes(d), a)
    if os.path.isfile(os.path.join(d, 'geo-drogi.tsv')):
        report_roads(load_roads(d))
    report_native(d)


if __name__ == '__main__':
    main()
